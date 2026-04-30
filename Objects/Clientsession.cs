using System.Net.Sockets;
using System.Text.Json;
using Shared;


// ============================================================
//  CLIENT SESSION
//  Đại diện cho 1 client kết nối.
//  Chứa network stream và chạy NETWORK THREAD riêng.
//
//  Trách nhiệm DUY NHẤT:
//    - Đọc raw bytes từ socket
//    - Deserialize → BasePacket
//    - Enqueue vào Room.InputQueue (KHÔNG xử lý logic)
//    - Gửi packet xuống client khi được yêu cầu
// ============================================================
public class ClientSession
{
    public int SessionId { get; }
    public string PlayerName { get; }
    public string RemoteEndPoint { get; }

    // Room hiện tại session đang ở, null nếu chưa join room
    public GameRoom? CurrentRoom { get; set; }

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly ILogger _logger;

    // Lock để tránh nhiều thread cùng ghi stream (WriteAsync không thread-safe)
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ClientSession(TcpClient tcpClient, ILogger logger)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        _logger = logger;
        SessionId = tcpClient.GetHashCode();
        PlayerName = $"Player_{SessionId % 10000}";
        RemoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
    }

    // ============================================================
    //  NETWORK THREAD — chạy liên tục, blocking I/O
    //  Được gọi bởi Task.Run trong TcpServerService
    // ============================================================
    public async Task RunReceiveLoopAsync(
        RoomManager roomManager,
        CancellationToken token)
    {
        _logger.LogInformation($"[Session {SessionId}] Network thread bắt đầu: {RemoteEndPoint}");

        try
        {
            while (!token.IsCancellationRequested && _tcpClient.Connected)
            {
                // Block tại đây chờ packet từ client
                byte[]? rawData = await ReceiveRawPacketAsync(token);

                if (rawData == null)
                {
                    _logger.LogInformation($"[Session {SessionId}] Client ngắt kết nối an toàn.");
                    break;
                }

                // Deserialize raw bytes → BasePacket
                BasePacket? packet = DeserializePacket(rawData);
                if (packet == null) continue;

                _logger.LogDebug($"[Session {SessionId}] Nhận: {packet.Type}");

                // ====================================================
                // QUAN TRỌNG: Không xử lý logic ở đây!
                // Chỉ enqueue vào đúng queue tương ứng.
                //
                // C_JoinRoom → RoomManager xử lý (không thuộc room nào)
                // Còn lại     → Room.InputQueue (game loop thread xử lý)
                // ====================================================
                if (packet is C_InputPacket)
                {
                    // Input packet → enqueue vào room input queue để game loop xử lý
                    if (CurrentRoom != null)
                    {
                        CurrentRoom.EnqueueInput(new IncomingPacket
                        {
                            SessionId = SessionId,
                            Packet = packet
                        });
                    }
                    else
                    {
                        _logger.LogDebug($"[Session {SessionId}] Bỏ input vì chưa vào room.");
                    }
                }
                else if (packet is C_GetRoomsPacket)
                {
                    // Yêu cầu danh sách phòng — xử lý trực tiếp ở đây
                    await SendAsync(new S_ListRoomsPacket { PlayerId = SessionId, Rooms = roomManager.GetAllRooms() }, token);
                }
                else if (packet is C_JoinRoomPacket joinPacket)
                {
                    // Join room không qua game loop — xử lý trực tiếp ở đây
                    // vì session chưa thuộc room nào
                    await roomManager.HandleJoinRoomAsync(this, joinPacket.RoomId ?? "");
                }
                else if (packet is C_JoinWorldPacket joinWorldPacket)
                {
                    _logger.LogInformation($"[Session {SessionId}] Yêu cầu vào world: {joinWorldPacket.RoomId}");
                    // Join world không qua game loop — xử lý trực tiếp ở đây
                    await roomManager.HandleJoinWorldAsync(this, joinWorldPacket.RoomId ?? "");
                }
            }
        }
        catch (OperationCanceledException) { /* Server đang tắt */ }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Session {SessionId}] Lỗi: {ex.Message}");
        }
        finally
        {
            // Rời khỏi room khi mất kết nối
            if (CurrentRoom != null)
                await CurrentRoom.RemoveSessionAsync(this);

            _tcpClient.Dispose();
            _logger.LogInformation($"[Session {SessionId}] Network thread kết thúc.");
        }
    }

    // ============================================================
    //  GỬI PACKET — thread-safe (game loop thread gọi khi broadcast)
    // ============================================================
    public async Task SendAsync(BasePacket packet, CancellationToken token = default)
    {
        byte[] data = SerializePacket(packet);
        await SendRawAsync(data, token);
    }

    public async Task SendRawAsync(byte[] data, CancellationToken token = default)
    {
        await _sendLock.WaitAsync(token);
        try
        {
            await _stream.WriteAsync(data, token);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // ============================================================
    //  ĐỌC / GHI RAW BYTES
    //  Protocol: [2 bytes length][payload bytes]
    // ============================================================
    private async Task<byte[]?> ReceiveRawPacketAsync(CancellationToken token)
    {
        byte[] lenBuf = new byte[2];
        if (!await ReadExactAsync(lenBuf, 2, token)) return null;

        ushort totalLen = BitConverter.ToUInt16(lenBuf, 0);
        if (totalLen < 4) return null; // tối thiểu: 2 len + 2 type

        byte[] payload = new byte[totalLen - 2];
        if (!await ReadExactAsync(payload, payload.Length, token)) return null;

        // Ghép lại để deserialize (lenBuf + payload)
        byte[] full = new byte[totalLen];
        Buffer.BlockCopy(lenBuf, 0, full, 0, 2);
        Buffer.BlockCopy(payload, 0, full, 2, payload.Length);
        return full;
    }

    private async Task<bool> ReadExactAsync(byte[] buf, int count, CancellationToken token)
    {
        int total = 0;
        while (total < count)
        {
            int n = await _stream.ReadAsync(buf, total, count - total, token);
            if (n == 0) return false;
            total += n;
        }
        return true;
    }

    // ============================================================
    //  SERIALIZE / DESERIALIZE
    //  Protocol: [2 bytes totalLen][2 bytes PacketType][JSON payload]
    //
    //  Thực tế production nên dùng MessagePack hoặc FlatBuffers
    //  thay JSON để giảm bandwidth và tăng tốc độ
    // ============================================================
    private static byte[] SerializePacket(BasePacket packet)
    {
        byte[] typeBytes = BitConverter.GetBytes((ushort)packet.Type);
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(packet, packet.GetType());
        ushort totalLen = (ushort)(2 + 2 + jsonBytes.Length); // lenField + type + json

        byte[] result = new byte[totalLen];
        BitConverter.GetBytes(totalLen).CopyTo(result, 0);
        typeBytes.CopyTo(result, 2);
        jsonBytes.CopyTo(result, 4);
        return result;
    }

    private static BasePacket? DeserializePacket(byte[] data)
    {
        if (data.Length < 4) return null;

        var type = (PacketType)BitConverter.ToUInt16(data, 2);
        var jsonSpan = new ReadOnlySpan<byte>(data, 4, data.Length - 4);

        return type switch
        {
            PacketType.C_Input => JsonSerializer.Deserialize<C_InputPacket>(jsonSpan),
            PacketType.C_JoinRoom => JsonSerializer.Deserialize<C_JoinRoomPacket>(jsonSpan),
            PacketType.C_LeaveRoom => JsonSerializer.Deserialize<C_LeaveRoomPacket>(jsonSpan),
            PacketType.C_Chat => JsonSerializer.Deserialize<C_ChatPacket>(jsonSpan),
            PacketType.C_GetRooms => JsonSerializer.Deserialize<C_GetRoomsPacket>(jsonSpan),
            PacketType.C_JoinWorld => JsonSerializer.Deserialize<C_JoinWorldPacket>(jsonSpan),
            PacketType.C_AttackBoss => JsonSerializer.Deserialize<C_AttackBossPacket>(jsonSpan),
            _ => null
        };
    }
}