# ChessLAN

Ứng dụng cờ vua WPF .NET 8 kết nối trực tiếp qua TCP, dành cho thực hành lập trình mạng.

## Chạy ứng dụng

Yêu cầu Windows và .NET 8 SDK:

```powershell
dotnet restore ChessLAN/ChessLAN.csproj
dotnet build ChessLAN/ChessLAN.csproj
dotnet test ChessLAN.Tests/ChessLAN.Tests.csproj
dotnet run --project ChessLAN/ChessLAN.csproj
```

Các kiểm thử luật cờ bao gồm lượt đi, nước tốt, chặn quân, không tự ăn quân, không bắt vua, nhập thành, bắt tốt qua đường, phong cấp và nước đi làm lộ vua.

Mở hai cửa sổ ứng dụng. Ở cửa sổ thứ nhất, đặt cổng 5000 và chọn **Tạo phòng**. Ở cửa sổ thứ hai, nhập `127.0.0.1`, cổng `5000`, rồi chọn **Tham gia** để thử trên cùng máy.

Để chơi trong LAN, máy chủ dùng **Tạo phòng**; người chơi còn lại nhập địa chỉ IPv4 LAN của máy chủ (ví dụ `192.168.1.25`) và cùng cổng TCP. Cho phép ChessLAN qua Windows Firewall trên mạng riêng nếu Windows hỏi. Kết nối Internet trực tiếp có thể cần cấu hình firewall, port forwarding hoặc VPN.

## Nội dung mạng trong bài

Máy chủ dùng `TcpListener` để lắng nghe một kết nối; máy khách dùng `TcpClient` để kết nối đến IP và cổng của máy chủ. Hai bên trao đổi UTF-8 JSON trên TCP. Mỗi JSON kết thúc bằng LF, do TCP chỉ cung cấp luồng byte và không bảo toàn ranh giới từng lần gửi. `StreamReader.ReadLineAsync` nhận lại từng thông điệp, còn `StreamWriter` bật `AutoFlush` để gửi ngay. Vòng nhận chạy bất đồng bộ, có hỗ trợ hủy và xử lý lỗi socket.

Thư viện luật cờ: **ChessDotNet 1.0.0**, NuGet, giấy phép MIT; thư viện cung cấp xác thực nước đi và trạng thái FEN.
