using Shared;
public class ServerPacketQueue
{
    public static ServerPacketQueue Instance { get; } = new();

    private readonly Queue<(int sessionId, PacketType type, byte[] data)> _queue = new();
    private readonly object _lock = new();

    public void Enqueue(int sessionId, PacketType type, byte[] data)
    {
        lock (_lock) _queue.Enqueue((sessionId, type, data));
    }

    public List<(int, PacketType, byte[])> Flush()
    {
        lock (_lock)
        {
            var list = new List<(int, PacketType, byte[])>(_queue);
            _queue.Clear();
            return list;
        }
    }
}