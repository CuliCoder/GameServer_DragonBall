using System.Net;
using System.Net.Sockets;
using System.Text;
namespace DragonBall_Server.Services;

public class TcpServerService : BackgroundService
{
    private readonly ILogger<TcpServerService> _logger;
    private TcpListener? _tcpListener;
    private readonly int _port = 8888; // Port cho TCP Server của game
    public TcpServerService(ILogger<TcpServerService> logger)
    {
        _logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _tcpListener = new TcpListener(IPAddress.Any, _port);
        _tcpListener.Start();
        _logger.LogInformation($"[TCP Server] Đang lắng nghe tại port {_port}...");

        try
        {
            // Vòng lặp liên tục chờ Client kết nối, dừng khi stoppingToken bị hủy (tắt app)
            while (!stoppingToken.IsCancellationRequested)
            {
                // Chờ một client kết nối tới
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(stoppingToken);

                _logger.LogInformation($"[TCP Server] Client mới kết nối: {tcpClient.Client.RemoteEndPoint}");

                // CHÚ Ý: Dùng _ = Task.Run(...) để xử lý client này ở một luồng riêng (Fire-and-forget).
                // Nếu không có dòng này, server sẽ bị block và không nhận được client thứ 2.
                _ = Task.Run(() => HandleClientAsync(tcpClient, stoppingToken), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Bắt lỗi khi ứng dụng đang tắt một cách an toàn (Graceful shutdown)
            _logger.LogInformation("[TCP Server] Đang tiến hành tắt server...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TCP Server] Lỗi nghiêm trọng xảy ra.");
        }
        finally
        {
            _tcpListener.Stop();
            _logger.LogInformation("[TCP Server] Đã đóng băng hoàn toàn.");
        }
    }

    // Hàm xử lý logic cho từng Client (Đây là nơi bạn áp dụng Length-Prefix)
    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client) // Đảm bảo tự động giải phóng tài nguyên khi disconnect
        {
            NetworkStream stream = client.GetStream();
            string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            _logger.LogInformation($"[TCP Server] Đã mở luồng giao tiếp với {clientEndPoint}");
            try
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    string? incomingJson = await ReceiveMessageAsync(stream, token);

                    // KHI MẠNG TRẢ VỀ NULL: Nghĩa là Client đã thoát game hoặc mất mạng
                    if (incomingJson == null)
                    {
                        _logger.LogInformation($"[TCP Server] Client {clientEndPoint} đã ngắt kết nối an toàn.");
                        break; // Thoát vòng lặp, kết thúc phục vụ Client này
                    }

                    _logger.LogInformation($"[Nhận - {clientEndPoint}]: {incomingJson}");
                    
                    string responseJson = $"{{\"action\":\"reply\", \"message\":\"Server đã nhận lệnh của bạn!\"}}";
                    await SendMessageAsync(stream, responseJson, token);

                    _logger.LogInformation($"[Gửi - {clientEndPoint}]: {responseJson}");

                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[TCP Server] Client ngắt kết nối đột ngột: {ex.Message}");
            }
        }
    }
    private async Task<string?> ReceiveMessageAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] lengthBuffer = new byte[4];

        // Đọc đúng 4 byte đầu tiên
        int bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, token);

        // ĐIỂM QUAN TRỌNG: Trong TCP, nếu hàm Read trả về 0 byte, 
        // đó là tín hiệu chuẩn cho biết đối phương đã chủ động đóng kết nối (Graceful disconnect).
        if (bytesRead == 0) return null;

        // Dịch 4 byte ra thành con số độ dài (VD: 150)
        int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);

        // Chuẩn bị mảng byte để hứng phần nội dung
        byte[] payloadBuffer = new byte[payloadLength];
        int totalBytesRead = 0;

        // Vòng lặp chống đứt gói: Cố gắng đọc cho đến khi thu thập đủ 150 byte thì thôi
        while (totalBytesRead < payloadLength)
        {
            int read = await stream.ReadAsync(payloadBuffer, totalBytesRead, payloadLength - totalBytesRead, token);
            if (read == 0) throw new Exception("Client ngắt kết nối khi dữ liệu đang tải dở dang.");
            totalBytesRead += read;
        }

        // Dịch mảng byte nội dung thành chuỗi JSON và trả về
        return Encoding.UTF8.GetString(payloadBuffer);
    }
    private async Task SendMessageAsync(NetworkStream stream, string jsonMessage, CancellationToken token)
    {
        // Bước 1: Dịch JSON ra thành mảng byte nội dung
        byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonMessage);

        // Bước 2: Dùng BitConverter tạo 4 byte chứa con số độ dài
        byte[] lengthBytes = BitConverter.GetBytes(payloadBytes.Length);

        // Bước 3: Ghép 4 byte độ dài và các byte nội dung vào chung 1 chuyến xe (fullPacket)
        byte[] fullPacket = new byte[4 + payloadBytes.Length];
        Buffer.BlockCopy(lengthBytes, 0, fullPacket, 0, 4);
        Buffer.BlockCopy(payloadBytes, 0, fullPacket, 4, payloadBytes.Length);

        // Bước 4: Gửi toàn bộ chuyến xe lên đường truyền mạng
        await stream.WriteAsync(fullPacket, 0, fullPacket.Length, token);
    }
}