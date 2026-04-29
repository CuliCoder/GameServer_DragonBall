using System.Numerics;
namespace Shared;

// ============================================================
//  PACKET TYPE ENUM
//  C_ = Client gửi lên Server
//  S_ = Server gửi xuống Client
// ============================================================
public enum PacketType : ushort
{
    // --- Client → Server ---
    C_Input = 1,   // Di chuyển, nhảy, tấn công...
    C_JoinRoom = 2,   // Yêu cầu vào phòng
    C_LeaveRoom = 3,   // Rời phòng
    C_Chat = 4,   // Tin nhắn chat
    C_GetRooms = 5,
    C_JoinWorld = 6,
    // --- Server → Client ---
    S_WorldState = 100, // Broadcast vị trí TẤT CẢ player trong room (mỗi tick)
    S_PlayerJoined = 101, // Thông báo có người vào phòng
    S_PlayerLeft = 102, // Thông báo có người rời phòng
    S_Chat = 103, // Broadcast chat
    S_JoinRoomAck = 104, // Xác nhận vào phòng thành công
    S_Error = 105, // Thông báo lỗi
    S_ListRooms = 106, // Danh sách phòng hiện có (dùng cho lobby)
    S_JoinWorld = 107, // Xác nhận vào world thành công
    S_Teleport = 108, // Teleport player
}
// ============================================================
//  BASE PACKET — mọi packet đều kế thừa
// ============================================================
public abstract class BasePacket
{
    public abstract PacketType Type { get; }
}

// ============================================================
//  CLIENT → SERVER PACKETS
// ============================================================
public class C_InputPacket : BasePacket
{
    public override PacketType Type => PacketType.C_Input;
    public float DirX { get; set; }
    public float DirY { get; set; }
    public bool Fly { get; set; }
    public bool Attack { get; set; }
    public int Tick { get; set; }
    public float CurrentPositionX { get; set; }
    public float CurrentPositionY { get; set; }
    public float TargetPositionX { get; set; }
    public float TargetPositionY { get; set; }
    public float DeltaTime { get; set; }
}


public class C_JoinRoomPacket : BasePacket
{
    public override PacketType Type => PacketType.C_JoinRoom;
    public string RoomId { get; set; } = "default";
}

public class C_LeaveRoomPacket : BasePacket
{
    public override PacketType Type => PacketType.C_LeaveRoom;
}

public class C_ChatPacket : BasePacket
{
    public override PacketType Type => PacketType.C_Chat;
    public string Message { get; set; } = "";
}
public class C_GetRoomsPacket : BasePacket
{
    public override PacketType Type => PacketType.C_GetRooms;
}
public class C_JoinWorldPacket : BasePacket
{
    public override PacketType Type => PacketType.C_JoinWorld;
    public string RoomId { get; set; } = "";
    public int PlayerId { get; set; }
}
// ============================================================
//  SERVER → CLIENT PACKETS
// ============================================================
public class S_WorldStatePacket : BasePacket
{
    public override PacketType Type => PacketType.S_WorldState;
    public int ServerTick { get; set; }
    public List<PlayerState> Players { get; set; } = new();
}
public struct PlayerInfo
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; }
}
public class PlayerState
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float VelX { get; set; }
    public float VelY { get; set; }
    public string AnimState { get; set; } = "";
    public Vector2 Position { get; set; }
}

public class S_PlayerJoinedPacket : BasePacket
{
    public override PacketType Type => PacketType.S_PlayerJoined;
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
}

public class S_PlayerLeftPacket : BasePacket
{
    public override PacketType Type => PacketType.S_PlayerLeft;
    public int PlayerId { get; set; }
}

public class S_ChatPacket : BasePacket
{
    public override PacketType Type => PacketType.S_Chat;
    public int SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string Message { get; set; } = "";
}

public class S_JoinRoomAckPacket : BasePacket
{
    public override PacketType Type => PacketType.S_JoinRoomAck;
    public string RoomId { get; set; } = "";
    public List<PlayerInfo> CurrentPlayers { get; set; } = new();
}
public class S_JoinWorldPacket : BasePacket
{
    public override PacketType Type => PacketType.S_JoinWorld;
    public List<PlayerState> CurrentPlayers { get; set; } = new();
}
public class S_ErrorPacket : BasePacket
{
    public override PacketType Type => PacketType.S_Error;
    public string Message { get; set; } = "";
}
public class S_ListRoomsPacket : BasePacket
{
    public override PacketType Type => PacketType.S_ListRooms;
    public int PlayerId { get; set; }
    public List<RoomInfo> Rooms { get; set; } = new();
}
public class S_TeleportPacket : BasePacket
{
    public override PacketType Type => PacketType.S_Teleport;
    public int SessionId { get; set; }
    public Vector2 TargetPosition { get; set; }
}
