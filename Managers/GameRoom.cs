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

    public GameRoom(string roomId, int maxPlayer, ILogger logger)
    {
        RoomId = roomId;
        MaxPlayer = maxPlayer;
        _logger = logger;

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
            Position = new Vector2(spawnX, spawnY), // khởi tạo để HandleInput tính distance đúng
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
            // PHASE 2: PHYSICS SIMULATION
            // Cập nhật vị trí dựa trên velocity (đơn giản hóa)
            // Thực tế: dùng physics engine hoặc tính toán phức tạp hơn
            // -------------------------------------------------------
            // foreach (var state in _playerStates.Values)
            // {
            //     SimulatePlayer(state, FIXED_DELTA);
            // }

            // -------------------------------------------------------
            // PHASE 3: GAME LOGIC
            // Collision detection, damage, event triggers...
            // -------------------------------------------------------
            // ProcessCollisions();
            // ProcessCombat();
            // ProcessEvents();

            // -------------------------------------------------------
            // PHASE 4: BROADCAST — gửi world state cho mọi player
            // -------------------------------------------------------
            _serverTick++;
            if (_serverTick % BROADCAST_EVERY_N_TICKS == 0)
            {
                await BroadcastWorldStateAsync(token);
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
    //  PHYSICS — cập nhật vị trí player
    // ============================================================
    // private static void SimulatePlayer(PlayerState state, float delta)
    // {
    //     const float SPEED = 5f;

    //     state.X += state.VelX * SPEED * delta;
    //     state.Y += state.VelY * SPEED * delta;

    //     // Clamp trong map bounds (ví dụ)
    //     state.X = Math.Clamp(state.X, -100f, 100f);
    //     state.Y = Math.Clamp(state.Y, -100f, 100f);

    //     // Cập nhật animation state
    //     state.AnimState = (Math.Abs(state.VelX) > 0.01f || Math.Abs(state.VelY) > 0.01f) ? "run" : "idle";
    // }

    // ============================================================
    //  PACKET HANDLERS — mỗi handler xử lý 1 loại packet
    //  Được gọi bởi Dispatcher trong game loop thread
    // ============================================================
    private void HandleInput(int sessionId, C_InputPacket packet)
    {
        if (!_playerStates.TryGetValue(sessionId, out PlayerState state))
        {
            _logger.LogWarning($"[Room {RoomId}] Player {sessionId} không tìm thấy trong _playerStates");
            return;
        }
 
        // ============================================================
        // Hằng số — phải khớp với LocalPlayerController
        // ============================================================
        const float MAX_SPEED_X  = 5f;    // LocalPlayerController.SPEED
        const float FLY_VEL_Y    = 0.1f;  // velocity khi nhấn bay lên
        const float GRAVITY      = -1f;   // gia tốc rơi (units/s² nhân deltaTime)
        const float TOLERANCE    = 0.5f;  // dung sai mạng (giảm xuống để chặt hơn)
 
        float dt = packet.DeltaTime;
        var targetPos = new Vector2(packet.PlayerState?.X ?? 0, packet.PlayerState?.Y ?? 0);
 
        // ============================================================
        // VALIDATE X — di chuyển ngang
        // ============================================================
        float deltaX     = Math.Abs(targetPos.X - state.Position.X);
        float maxDeltaX  = Math.Abs(packet.DirX) * MAX_SPEED_X * dt + TOLERANCE;
        bool validX      = deltaX <= maxDeltaX;
 
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
        if (packet.Fly)
        {
            expectedVelY = FLY_VEL_Y; // đang bay lên, vận tốc cố định
        }
        else
        {
            // Trọng lực tích lũy từng tick
            expectedVelY = state.VelY + GRAVITY * dt;
        }
 
        float expectedY  = state.Position.Y + expectedVelY;
        float deltaY     = Math.Abs(targetPos.Y - expectedY);
        bool validY      = deltaY <= TOLERANCE;
 
        // ============================================================
        // KẾT QUẢ
        // ============================================================
        if (validX && validY)
        {
            // ✅ HỢP LỆ — cập nhật state (modify properties, KHÔNG gán lại biến)
            // state.Position = targetPos;
            // state.X        = targetPos.X;     // sync để BroadcastWorldState đọc đúng
            // state.Y        = targetPos.Y;
            // state.VelX     = packet.DirX * MAX_SPEED_X * dt;
            // state.VelY     = expectedVelY;    // lưu lại để tính gravity tick tiếp
            // state.AnimState = Math.Abs(packet.DirX) > 0.01f ? "run" : "idle";
            state = packet.PlayerState ?? state; // Cập nhật toàn bộ state từ client (giả sử đã validate)
        }
        else
        {
            // ❌ HACK SPEED hoặc lag nặng → rubber-band về vị trí server đang lưu
            string reason = !validX ? $"X vượt ({deltaX:F2}>{maxDeltaX:F2})"
                                    : $"Y vượt ({deltaY:F2}>{TOLERANCE:F2}, expectedY={expectedY:F2})";
            _logger.LogWarning($"[Room {RoomId}] Player {sessionId} bị rubber-band: {reason}");
 
            _ = BroadcastOnlyInWorldAsync(new S_TeleportPacket
            {
                SessionId      = sessionId,
                TargetPosition = state.Position
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
    private async Task BroadcastWorldStateAsync(CancellationToken token)
    {
        if (_sessions.IsEmpty) return;

        var worldState = new S_WorldStatePacket
        {
            ServerTick = _serverTick,
            Players = _playerStates.Values.ToList()
        };

        await BroadcastAsync(worldState, token);
    }

    private async Task BroadcastAsync(BasePacket packet, CancellationToken token = default)
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
    private async Task BroadcastInWorldAsync(BasePacket packet, CancellationToken token = default)
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
    private async Task BroadcastOnlyInWorldAsync(BasePacket packet, CancellationToken token = default)
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
}