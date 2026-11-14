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
    private Task? _connectionTask;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private bool _disposed;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<string>? StatusChanged;
    public bool IsHost { get; private set; }
    public string SessionId { get; private set; } = "";

    public Task HostAsync(int port)
    {
        IsHost = true;
        SessionId = Guid.NewGuid().ToString("N");
        _connectionTask = HostCoreAsync(port);
        return _connectionTask;
    }

    private async Task HostCoreAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start(1);
        StatusChanged?.Invoke($"Đang chờ đối thủ tại cổng {port}…");
        _client = await _listener.AcceptTcpClientAsync(_stop.Token);
        _listener.Stop(); _listener = null;
        SetStreams(_client);
        StatusChanged?.Invoke("Đối thủ đã kết nối.");
        // The UI sends WELCOME after reading HELLO, including the chosen time control.
        _receiveTask = ReceiveLoopAsync(_stop.Token);
    }

    public Task JoinAsync(string host, int port, string nickname)
    {
        IsHost = false;
        _connectionTask = JoinCoreAsync(host, port, nickname);
        return _connectionTask;
    }

    private async Task JoinCoreAsync(string host, int port, string nickname)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, _stop.Token);
        SetStreams(_client);
        await SendAsync(new NetworkMessage { Command = "HELLO", Version = 1, Name = nickname });
        _receiveTask = ReceiveLoopAsync(_stop.Token);
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
        if (_disposed) return;
        _disposed = true;
        _stop.Cancel(); _listener?.Stop(); _client?.Close();
        if (_connectionTask is not null)
        {
            try { await _connectionTask; }
            catch (OperationCanceledException) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (_stop.IsCancellationRequested) { }
            catch (IOException) when (_stop.IsCancellationRequested) { }
        }
        if (_receiveTask is not null)
        {
            try { await _receiveTask; } catch (OperationCanceledException) { }
        }
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose(); _stop.Dispose(); _writeLock.Dispose();
    }
}
