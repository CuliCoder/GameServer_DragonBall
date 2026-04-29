using System.Collections.Concurrent;

// ============================================================
//  ROOM MANAGER
//  Quản lý toàn bộ rooms, tạo/xóa room, điều phối join room
// ============================================================
public class RoomManager
{
    private readonly ILogger<RoomManager> _logger;
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly CancellationToken _serverToken;

    public RoomManager(ILogger<RoomManager> logger, CancellationToken serverToken)
    {
        _logger = logger;
        _serverToken = serverToken;

        // Tạo sẵn một số room mặc định khi khởi động
        CreateRoom("lobby", maxPlayer: 100);
        CreateRoom("room_01", maxPlayer: 4);
        CreateRoom("room_02", maxPlayer: 4);
    }

    public GameRoom CreateRoom(string roomId, int maxPlayer = 10)
    {
        var room = new GameRoom(roomId, maxPlayer, _logger);
        _rooms[roomId] = room;

        // Mỗi room chạy fixed update loop RIÊNG — độc lập nhau
        _ = Task.Run(() => room.RunFixedUpdateLoopAsync(_serverToken), _serverToken);

        _logger.LogInformation($"[RoomManager] Tạo room: {roomId} (max {maxPlayer} players)");
        return room;
    }

    public async Task HandleJoinRoomAsync(ClientSession session, string roomId)
    {
        // Rời room cũ nếu đang ở trong một room nào đó
        if (session.CurrentRoom != null)
            await session.CurrentRoom.RemoveSessionAsync(session);

        // Tìm hoặc tạo room
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            // Auto-create room nếu không tồn tại
            room = CreateRoom(roomId);
        }

        await room.AddSessionAsync(session);
    }
    public async Task HandleJoinWorldAsync(ClientSession session, string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            await room.AddPlayerInWorldAsync(session, CancellationToken.None);
        }
    }
    public GameRoom? GetRoom(string roomId) =>
        _rooms.TryGetValue(roomId, out var r) ? r : null;

    public List<RoomInfo> GetAllRooms()
    {
        var rooms = new List<RoomInfo>();
        foreach (var room in _rooms.Values)
        {
            rooms.Add(new RoomInfo
            {
                RoomId = room.RoomId,
                PlayerCount = room.PlayerCount,
                MaxPlayer = room.MaxPlayer
            });
        }
        return rooms;
    }
}