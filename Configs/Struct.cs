using Shared;
public struct IncomingPacket
{
    public int        SessionId { get; set; }
    public BasePacket Packet    { get; set; }
}