using System.Collections.Concurrent;
using System.Diagnostics;
using Shared;
using System.Numerics;
// ============================================================
//  GAME ROOM
//  Mỗi room là một "thế giới" độc lập:
//    - Có danh sách session riêng
//    - Có InputQueue riêng
//    - Chạy FixedUpdateLoop riêng (physics, game logic)
//    - Broadcast state cho các player trong room đó
//
//  FLOW trong 1 tick:
//    [1] Dequeue tất cả input từ InputQueue
//    [2] Dispatcher gọi handler tương ứng → cập nhật player state
//    [3] Chạy physics simulation
//    [4] Mỗi N tick → Broadcast S_WorldState cho mọi player trong room
// ============================================================
public class GameRoom
{
    public string RoomId { get; }
    public int MaxPlayer { get; }

    private readonly ILogger _logger;
    private readonly PacketDispatcher _dispatcher;

    // Sessions trong room: sessionId → ClientSession
    private readonly ConcurrentDictionary<int, ClientSession> _sessions = new();

    // Player state trong room: sessionId → PlayerState
    private readonly ConcurrentDictionary<int, PlayerState> _playerStates = new();

    // ============================================================
    //  INPUT QUEUE — thread-safe
    //  Network Thread (mỗi client) → Enqueue
    //  Game Loop Thread (room)     → Dequeue & process
    // ============================================================
    private readonly ConcurrentQueue<IncomingPacket> _inputQueue = new();

    // Tick counter để điều tiết broadcast
    private int _serverTick = 0;
    public Dictionary<int, EnemyController> bosses { get; private set; } = new Dictionary<int, EnemyController>
    {
        [1] = new EnemyController(1, BossType.Broly, new Vector2(0, 0), 20000000, 1, 3f)
    };
    public GameRoom(string roomId, int maxPlayer, ILogger logger, Dictionary<int, EnemyController>? bosses = null)
    {
        RoomId = roomId;
        MaxPlayer = maxPlayer;
        _logger = logger;
        if (bosses != null)
        {
            this.bosses = bosses;
        }

        // Khởi tạo dispatcher — đăng ký handler cho từng PacketType
        _dispatcher = new PacketDispatcher();
        RegisterHandlers();
    }

    public int PlayerCount => _sessions.Count;

    // ============================================================
    //  ĐĂNG KÝ HANDLER CHO TỪNG PACKET TYPE
    //  Thêm loại packet mới → thêm Register ở đây, không đụng đến loop
    // ============================================================
    private void RegisterHandlers()
    {
        _dispatcher.Register<C_InputPacket>(PacketType.C_Input, HandleInput);
        _dispatcher.Register<C_LeaveRoomPacket>(PacketType.C_LeaveRoom, HandleLeaveRoom);
        _dispatcher.Register<C_ChatPacket>(PacketType.C_Chat, HandleChat);
        _dispatcher.Register<C_AttackBossPacket>(PacketType.C_AttackBoss, HandleAttackBoss);
        // Thêm packet mới: _dispatcher.Register<C_AttackPacket>(PacketType.C_Attack, HandleAttack);
    }

    // ============================================================
    //  ADD SESSION — khi player join room
    // ============================================================
    public async Task AddSessionAsync(ClientSession session)
    {
        if (_sessions.Count >= MaxPlayer)
        {
            await session.SendAsync(new S_ErrorPacket { Message = "Phòng đã đầy." });
            return;
        }

        _sessions[session.SessionId] = session;
        session.CurrentRoom = this;

        _logger.LogInformation($"[Room {RoomId}] {session.PlayerName} vào phòng. ({_sessions.Count}/{MaxPlayer})");

        await BroadcastAsync(new S_JoinRoomAckPacket
        {
            RoomId = RoomId,
            CurrentPlayers = _sessions.Values.Select(s => new PlayerInfo { PlayerId = s.SessionId, PlayerName = s.PlayerName }).ToList()
        });
    }
    public async Task AddPlayerInWorldAsync(ClientSession session, CancellationToken token)
    {
        if (!_sessions.TryGetValue(session.SessionId, out var sess) || _playerStates.ContainsKey(session.SessionId))
        {
            return;
        }

        float spawnX = -10f + _playerStates.Count * 3f;
        float spawnY = 20f;
        _playerStates[session.SessionId] = new PlayerState
        {
            PlayerId = session.SessionId,
            PlayerName = session.PlayerName,
            X = spawnX,
            Y = spawnY,
            VelX = 0f,
            VelY = 0f,
            AnimState = "idle"
        };
        _logger.LogInformation($"[Room {RoomId}] {session.PlayerName} vào world. ({_playerStates.Count} players in world)");
        await session.SendAsync(new S_JoinWorldPacket
        {
            CurrentPlayers = _playerStates.Values.ToList()
        });
        await BroadcastExceptInWorldAsync(session.SessionId, new S_JoinWorldPacket
        {
            CurrentPlayers = new List<PlayerState> { _playerStates[session.SessionId] }
        }, token);
    }
    // ============================================================
    //  REMOVE SESSION — khi player rời hoặc mất kết nối
    // ============================================================
    public async Task RemoveSessionAsync(ClientSession session)
    {
        _sessions.TryRemove(session.SessionId, out _);
        _playerStates.TryRemove(session.SessionId, out _);
        session.CurrentRoom = null;

        _logger.LogInformation($"[Room {RoomId}] {session.PlayerName} rời phòng. ({_sessions.Count}/{MaxPlayer})");

        await BroadcastAsync(new S_PlayerLeftPacket { PlayerId = session.SessionId });
    }

    // ============================================================
    //  ENQUEUE INPUT — network thread gọi
    // ============================================================
    public void EnqueueInput(IncomingPacket packet) => _inputQueue.Enqueue(packet);

    // ============================================================
    //  FIXED UPDATE LOOP — GAME LOOP THREAD của room này
    //
    //  Chạy độc lập per-room → mỗi room có thể có tick rate khác nhau
    //  Ví dụ: room PvP 64Hz, room casual 20Hz
    // ============================================================
    public async Task RunFixedUpdateLoopAsync(CancellationToken token)
    {
        const int TICK_RATE = 60;                         // 50 Hz
        const float FIXED_DELTA = 1f / TICK_RATE;             // 0.02s
        const int BROADCAST_EVERY_N_TICKS = 1;                      // broadcast mỗi tick (có thể tăng lên 2-3 nếu muốn tiết kiệm bandwidth)

        var tickInterval = TimeSpan.FromSeconds(FIXED_DELTA);
        var stopwatch = Stopwatch.StartNew();
        var nextTickTime = stopwatch.Elapsed;

        _logger.LogInformation($"[Room {RoomId}] Fixed update loop bắt đầu ({TICK_RATE}Hz)");

        while (!token.IsCancellationRequested)
        {
            var now = stopwatch.Elapsed;

            // Nếu chưa đến tick tiếp theo → sleep ngắn
            if (now < nextTickTime)
            {
                var wait = nextTickTime - now;
                if (wait > TimeSpan.FromMilliseconds(1))
                    await Task.Delay(wait, token);
                continue;
            }

            // -------------------------------------------------------
            // PHASE 1: Xử lý INPUT
            // Dequeue toàn bộ packet trong queue, dispatch tới handler
            // -------------------------------------------------------
            int inputCount = 0;
            while (_inputQueue.TryDequeue(out var incoming))
            {
                _dispatcher.Dispatch(incoming.SessionId, incoming.Packet);
                inputCount++;
            }

            // -------------------------------------------------------
            // PHASE 2: Cập nhật game logic (physics, AI, v.v)
            if (bosses.Count > 0)
            {
                foreach (var boss in bosses.Values)
                {
                    boss.UpdateBossAI(this);
                }
            }
            // -------------------------------------------------------
            // PHASE 4: BROADCAST — gửi world state cho mọi player
            // -------------------------------------------------------
            _serverTick++;
            if (_serverTick % BROADCAST_EVERY_N_TICKS == 0)
            {
                TryBroadcastWorldState(token);
            }

            // Tính thời điểm tick tiếp theo
            // Dùng accumulator thay vì reset để tránh drift
            nextTickTime += tickInterval;

            // Nếu bị trễ quá nhiều (lag spike) → reset để tránh "catch up" loop điên
            if (stopwatch.Elapsed - nextTickTime > TimeSpan.FromSeconds(1))
            {
                _logger.LogWarning($"[Room {RoomId}] Game loop bị trễ, reset accumulator");
                nextTickTime = stopwatch.Elapsed;
            }
        }

        _logger.LogInformation($"[Room {RoomId}] Fixed update loop kết thúc.");
    }
    // ============================================================
    //  PACKET HANDLERS — mỗi handler xử lý 1 loại packet
    //  Được gọi bởi Dispatcher trong game loop thread
    // ============================================================
    private void HandleAttackBoss(int sessionId, C_AttackBossPacket packet)
    {
        if (!bosses.TryGetValue(packet.BossId, out EnemyController? boss))
        {
            _logger.LogWarning($"[Room {RoomId}] Player {sessionId} tấn công boss không tồn tại: {packet.BossId}");
            return;
        }
        _ = boss.HandleAttackBossAsync(this, packet, sessionId);
    }
    private void HandleInput(int sessionId, C_InputPacket packet)
    {
        if (!_playerStates.TryGetValue(sessionId, out PlayerState? state))
        {
            _logger.LogWarning($"[Room {RoomId}] Player {sessionId} không tìm thấy trong _playerStates");
            return;
        }

        // ============================================================
        // Hằng số — phải khớp với LocalPlayerController
        // ============================================================
        const float MAX_SPEED_X = 5f;    // LocalPlayerController.SPEED
        const float FLY_VEL_Y = 0.1f;  // velocity khi nhấn bay lên
        const float GRAVITY = -1f;   // gia tốc rơi (units/s² nhân deltaTime)
        const float TOLERANCE = 2f;  // dung sai mạng (giảm xuống để chặt hơn)
        const float TELEPORT_THRESHOLD = 6f;

        float dt = packet.DeltaTime;
        var targetPos = new Vector2(packet.PlayerState?.X ?? state.X, packet.PlayerState?.Y ?? state.Y);

        // ============================================================
        // VALIDATE X — di chuyển ngang
        // ============================================================
        float deltaX = Math.Abs(targetPos.X - state.X);
        float maxDeltaX = Math.Abs(packet.DirX) * MAX_SPEED_X * dt + TOLERANCE;
        bool validX = deltaX <= maxDeltaX;

        // ============================================================
        // VALIDATE Y — bay lên hoặc rơi theo trọng lực
        //
        // Server tự tính VelY kỳ này dựa vào state VelY kỳ trước:
        //   - Fly==true  → VelY = FLY_VEL_Y (nhấn bay)
        //   - Fly==false → VelY = state.VelY + GRAVITY * dt (rơi tự do)
        //
        // Sau đó so sánh Y client gửi với Y server dự đoán.
        // ============================================================
        float expectedVelY;
        if (packet.PlayerState?.VelY > 0)
        {
            expectedVelY = FLY_VEL_Y; // đang bay lên, vận tốc cố định
        }
        else if (packet.PlayerState?.VelY < 0)
        {
            // Trọng lực tích lũy từng tick
            expectedVelY = state.VelY + GRAVITY * dt;
        }
        else
        {
            expectedVelY = 0f; // đứng yên hoặc chạm đất
        }
        float expectedY = state.Y + expectedVelY;
        float deltaY = Math.Abs(targetPos.Y - expectedY);
        bool validY = deltaY <= TOLERANCE;

        // ============================================================
        // KẾT QUẢ
        // ============================================================
        if (validX && validY)
        {
            // ✅ HỢP LỆ — cập nhật state (modify properties, KHÔNG gán lại biến)
            state.X = packet.PlayerState?.X ?? state.X;     // sync để BroadcastWorldState đọc đúng
            state.Y = packet.PlayerState?.Y ?? state.Y;
            state.VelX = packet.PlayerState?.VelX ?? state.VelX;     // lấy từ input client gửi lên (đã validate max speed)
            state.VelY = packet.PlayerState?.VelY ?? state.VelY;    // lưu lại để tính gravity tick tiếp
            state.AnimState = packet.PlayerState?.AnimState ?? state.AnimState; // sync animation state (có thể dùng để trigger hiệu ứng khác)
            if (packet.isNumber1)
            {
                state.AnimState = "boom";
                state.VelX = 0f;
            }
        }
        else
        {
            // ❌ HACK SPEED hoặc lag nặng → rubber-band về vị trí server đang lưu
            string reason = !validX ? $"X vượt ({deltaX:F2}>{maxDeltaX:F2})"
                                    : $"Y vượt ({deltaY:F2}>{TOLERANCE:F2}, expectedY={expectedY:F2})";
            float drift = Vector2.Distance(targetPos, new Vector2(state.X, state.Y));
            _logger.LogDebug($"[Room {RoomId}] Player {sessionId} lệch state: {reason}, drift={drift:F2}");

            // Chỉ teleport cứng khi lệch quá lớn để tránh giật lùi liên tục do jitter mạng.
            if (drift < TELEPORT_THRESHOLD)
            {
                state.X = targetPos.X;
                state.Y = targetPos.Y;
                state.VelX = packet.PlayerState?.VelX ?? state.VelX;
                state.VelY = packet.PlayerState?.VelY ?? state.VelY;
                state.AnimState = packet.PlayerState?.AnimState ?? state.AnimState;
                return;
            }

            _ = BroadcastOnlyInWorldAsync(new S_TeleportPacket
            {
                SessionId = sessionId,
                TargetPosition = new Vector2(state.X, state.Y)
            });
        }
    }
    private void HandleLeaveRoom(int sessionId, C_LeaveRoomPacket packet)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            // Fire-and-forget (đang trong game loop thread, không await)
            _ = RemoveSessionAsync(session);
        }
    }

    private void HandleChat(int sessionId, C_ChatPacket packet)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)) return;

        var chatPacket = new S_ChatPacket
        {
            SenderId = sessionId,
            SenderName = session.PlayerName,
            Message = packet.Message[..Math.Min(packet.Message.Length, 200)] // giới hạn 200 ký tự
        };

        // Broadcast trong game loop thread — fire-and-forget
        _ = BroadcastAsync(chatPacket);
    }

    // ============================================================
    //  BROADCAST — gửi tới tất cả hoặc trừ 1 session
    // ============================================================
    private void BroadcastWorldStateAsync(CancellationToken token)
    {
        if (_sessions.IsEmpty) return;

        var worldState = new S_WorldStatePacket
        {
            ServerTick = _serverTick,
            // Snapshot để tránh giữ reference mutable làm packet bị stale/không nhất quán.
            Players = _playerStates.Values.Select(p => new PlayerState
            {
                PlayerId = p.PlayerId,
                PlayerName = p.PlayerName,
                X = p.X,
                Y = p.Y,
                VelX = p.VelX,
                VelY = p.VelY,
                AnimState = p.AnimState
            }).ToList()
        };

        var targets = _sessions.Values
            .Where(s => s.CurrentRoom == this && _playerStates.ContainsKey(s.SessionId))
            .ToList();

        foreach (var session in targets)
        {
            _ = session.SendAsync(worldState, token).ContinueWith(t =>
            {
                if (!t.IsFaulted) return;
                _sessions.TryRemove(session.SessionId, out _);
                _playerStates.TryRemove(session.SessionId, out _);
                session.CurrentRoom = null;
            }, TaskScheduler.Default);
        }
    }

    private void TryBroadcastWorldState(CancellationToken token)
    {
        try
        {
            BroadcastWorldStateAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Room {RoomId}] Broadcast world state lỗi: {ex.GetBaseException().Message}");
        }
    }

    public async Task BroadcastAsync(BasePacket packet, CancellationToken token = default)
    {
        var tasks = _sessions.Values
            .Select(s => s.SendAsync(packet, token)
                          .ContinueWith(t =>
                          {
                              if (t.IsFaulted)
                                  _sessions.TryRemove(s.SessionId, out _);
                          }, token));

        await Task.WhenAll(tasks);
    }

    private async Task BroadcastExceptAsync(int excludeSessionId, BasePacket packet)
    {
        var tasks = _sessions.Values
            .Where(s => s.SessionId != excludeSessionId)
            .Select(s => s.SendAsync(packet));

        await Task.WhenAll(tasks);
    }
    public async Task BroadcastInWorldAsync(BasePacket packet, CancellationToken token = default)
    {
        var tasks = _sessions.Values
            .Where(s => s.CurrentRoom == this && _playerStates.ContainsKey(s.SessionId))
            .Select(s => s.SendAsync(packet, token)
                          .ContinueWith(t =>
                          {
                              if (t.IsFaulted)
                                  _sessions.TryRemove(s.SessionId, out _);
                          }, token));

        await Task.WhenAll(tasks);
    }
    private async Task BroadcastExceptInWorldAsync(int excludeSessionId, BasePacket packet, CancellationToken token = default)
    {
        var targets = _sessions.Values
            .Where(s => s.SessionId != excludeSessionId && s.CurrentRoom == this && _playerStates.ContainsKey(s.SessionId))
            .ToList();

        var tasks = targets.Select(async s =>
        {
            try
            {
                await s.SendAsync(packet, token);
            }
            catch
            {
                _sessions.TryRemove(s.SessionId, out _);
                _playerStates.TryRemove(s.SessionId, out _);
                s.CurrentRoom = null;
            }
        });

        await Task.WhenAll(tasks);
    }
    public async Task BroadcastOnlyInWorldAsync(BasePacket packet, CancellationToken token = default)
    {
        var tasks = _sessions.Values
            .Where(s => s.CurrentRoom == this && _playerStates.ContainsKey(s.SessionId))
            .Select(s => s.SendAsync(packet, token)
                          .ContinueWith(t =>
                          {
                              if (t.IsFaulted)
                                  _sessions.TryRemove(s.SessionId, out _);
                          }, token));

        await Task.WhenAll(tasks);
    }
    // ============================================================
    public ClientSession? GetSession(int sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;
    public PlayerState? GetFirstPlayer() =>
        _playerStates.Values.FirstOrDefault();
    public PlayerState? GetPlayerState(int sessionId) =>
    _playerStates.TryGetValue(sessionId, out var state) ? state : null;
    public List<PlayerState> GetAllPlayerStates() => _playerStates.Values.ToList();
}