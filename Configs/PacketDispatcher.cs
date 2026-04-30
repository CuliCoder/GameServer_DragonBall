using Shared;
// ============================================================
//  PACKET DISPATCHER
//  Đăng ký handler cho từng PacketType, gọi khi dequeue
// ============================================================
public class PacketDispatcher
{
    // Handler nhận (sessionId, packet) → xử lý
    private readonly Dictionary<PacketType, Action<int, BasePacket>> _handlers = new();

    public void Register<T>(PacketType type, Action<int, T> handler) where T : BasePacket
    {
        _handlers[type] = (sessionId, packet) =>
        {
            if (packet is T typed)
                handler(sessionId, typed);
        };
    }

    public void Dispatch(int sessionId, BasePacket packet)
    {
        if (_handlers.TryGetValue(packet.Type, out var handler))
            handler(sessionId, packet);
    }
}