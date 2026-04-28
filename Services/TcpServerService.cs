using System.Net;
using System.Net.Sockets;

// ============================================================
//  TCP SERVER SERVICE
//  Entry point — chỉ làm 1 việc: chấp nhận kết nối TCP
//  Sau đó giao hết cho ClientSession và RoomManager
// ============================================================
public class TcpServerService : BackgroundService
{
    private readonly ILogger<TcpServerService> _logger;
    private readonly ILogger<RoomManager> _roomLogger;
    private readonly int _port = 8888;

    public TcpServerService(ILogger<TcpServerService> logger, ILogger<RoomManager> roomLogger)
    {
        _logger = logger;
        _roomLogger = roomLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        var roomManager = new RoomManager(
            _roomLogger,
            stoppingToken
        );

        listener.Start();
        _logger.LogInformation($"[TCP Server] Lắng nghe tại port {_port}...");

        try
        {
            // ACCEPT LOOP — chỉ chấp nhận kết nối, không làm gì khác
            while (!stoppingToken.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(stoppingToken);
                _logger.LogInformation($"[TCP Server] Client mới: {tcpClient.Client.RemoteEndPoint}");

                // Tạo session và chạy network thread riêng cho client này
                // Fire-and-forget: mỗi client chạy độc lập
                var session = new ClientSession(tcpClient, _logger);
                _ = Task.Run(
                    () => session.RunReceiveLoopAsync(roomManager, stoppingToken),
                    stoppingToken
                );
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[TCP Server] Đang tắt...");
        }
        finally
        {
            listener.Stop();
            _logger.LogInformation("[TCP Server] Đã tắt hoàn toàn.");
        }
    }
}