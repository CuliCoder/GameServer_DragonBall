using Shared;
public struct IncomingPacket
{
    public int SessionId { get; set; }
    public BasePacket Packet { get; set; }
}
public struct RoomInfo
{
    public string RoomId { get; set; }
    public int PlayerCount { get; set; }
    public int MaxPlayer { get; set; }
}