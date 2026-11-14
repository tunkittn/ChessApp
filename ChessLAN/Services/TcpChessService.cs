using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text.Json;
using ChessLAN.Models;

namespace ChessLAN.Services;

/// <summary>Simple one-opponent TCP service. Each UTF-8 JSON message is one LF-delimited line.</summary>
public sealed class TcpChessService : IAsyncDisposable
{
    private TcpListener? _listener;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<string>? StatusChanged;
    public bool IsHost { get; private set; }
    public string SessionId { get; private set; } = "";

    public async Task HostAsync(int port, CancellationToken token = default)
    {
        IsHost = true;
        SessionId = Guid.NewGuid().ToString("N");
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start(1);
        StatusChanged?.Invoke($"Đang chờ đối thủ tại cổng {port}…");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        _client = await _listener.AcceptTcpClientAsync(linked.Token);
        _listener.Stop(); _listener = null;
        SetStreams(_client);
        StatusChanged?.Invoke("Đối thủ đã kết nối.");
        // The UI sends WELCOME after reading HELLO, including the chosen time control.
        _ = ReceiveLoopAsync(_stop.Token);
    }

    public async Task JoinAsync(string host, int port, string nickname, CancellationToken token = default)
    {
        IsHost = false;
        _client = new TcpClient();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        await _client.ConnectAsync(host, port, linked.Token);
        SetStreams(_client);
        await SendAsync(new NetworkMessage { Command = "HELLO", Version = 1, Name = nickname });
        _ = ReceiveLoopAsync(_stop.Token);
    }

    private void SetStreams(TcpClient client)
    {
        var stream = client.GetStream();
        _reader = new StreamReader(stream, new System.Text.UTF8Encoding(false), false, 1024, true);
        _writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, true) { AutoFlush = true, NewLine = "\n" };
    }

    public async Task SendAsync(NetworkMessage message)
    {
        if (_writer is null) throw new IOException("Chưa có kết nối TCP.");
        await _writeLock.WaitAsync(_stop.Token);
        try { await _writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), _stop.Token); }
        finally { _writeLock.Release(); }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _reader is not null)
            {
                // ReadLineAsync restores application message boundaries over TCP's byte stream.
                var line = await _reader.ReadLineAsync(token);
                if (line is null) break;
                if (line.Length > 16_384) throw new InvalidDataException("Thông điệp vượt quá giới hạn.");
                var message = JsonSerializer.Deserialize<NetworkMessage>(line, JsonOptions);
                if (message is null || message.Version != 1) { StatusChanged?.Invoke("Thông điệp không hợp lệ hoặc sai phiên bản."); continue; }
                MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or SocketException or JsonException or InvalidDataException)
        { StatusChanged?.Invoke($"Lỗi kết nối: {ex.Message}"); }
        finally { StatusChanged?.Invoke("Đối thủ đã ngắt kết nối."); }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); _listener?.Stop(); _client?.Close();
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose(); _stop.Dispose(); _writeLock.Dispose();
    }
}
