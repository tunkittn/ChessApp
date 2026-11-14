using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ChessLAN.Services;
using ChessLAN.Models;
using ChessLAN.Controls;
using ChessLAN.Views;
using ChessDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

namespace ChessLAN;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ChessRulesService _rules = new();
    private readonly ChessSoundService _sound = new();
    private TcpChessService? _network;
    private string? _selected;
    private readonly HashSet<string> _legalTargets = new(StringComparer.OrdinalIgnoreCase);
    private string _sessionId = "";
    private int _sequence;
    private bool _isHost;
    private bool _waitingForState;
    private bool _gameOver;
    private bool _blackPerspective;
    private bool _gameStarted;
    private bool _closingWindow;
    private bool _roomActionBusy;
    private bool _clockRunning;
    private long _whiteMilliseconds;
    private long _blackMilliseconds;
    private long _clockTimestamp;
    private long _lastClockSync;
    private Player _clockActive = Player.White;
    private int _selectedTimeSeconds=300;
    private Player? _losingPlayer;
    private string? _lastMoveFrom;
    private string? _lastMoveTo;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool _waitingForOpponent;
    private bool _connectionPending;
    public MainWindow()
    {
        InitializeComponent();
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();
        _whiteMilliseconds=_blackMilliseconds=_selectedTimeSeconds*1000L;
        RefreshClockLabels();
        UpdateCoordinates();
        DrawBoard();
    }

    private async void Host_Click(object sender, RoutedEventArgs e)
    {
        if (_roomActionBusy) return;
        _roomActionBusy=true;
        try
        {
            await ResetForNewSessionAsync(true);
            _network = CreateNetwork(); _isHost = true;
            _blackPerspective=false;
            var lanAddresses=GetLanIpv4Addresses();
            RoomHintText.Text=lanAddresses.Length == 0
                ? "Không tìm thấy IPv4 LAN. Hãy kiểm tra Wi‑Fi/Ethernet đang bật. Cổng TCP: " + PortBox.Text
                : $"IP LAN của máy tạo phòng: {string.Join(", ",lanAddresses)}\nMáy kia nhập một IP này và cổng {PortBox.Text}. Không dùng 127.0.0.1 khi chơi giữa hai máy.";
            _waitingForOpponent=true; SetWaiting(true, $"Đang chờ đối thủ · cổng {PortBox.Text}");
            await _network.HostAsync(int.Parse(PortBox.Text));
            _waitingForOpponent=false; _connectionPending=true; SetWaiting(false, "Đối thủ đã kết nối · chờ xác nhận");
        }
        catch (OperationCanceledException) { _waitingForOpponent=false; _connectionPending=false; SetWaiting(false, "Đã hủy tạo phòng"); }
        catch (SocketException ex) { _waitingForOpponent=false; _connectionPending=false; SetWaiting(false, "Không tạo được phòng"); MessageBox.Show(ExplainSocketError(ex), "Không thể tạo phòng"); }
        catch (Exception ex) { _waitingForOpponent=false; _connectionPending=false; SetWaiting(false, "Không tạo được phòng"); MessageBox.Show(ex.ToString(), "Không thể tạo phòng"); }
        finally { _roomActionBusy=false; RefreshActionControls(); }
    }
    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        if (_roomActionBusy) return;
        _roomActionBusy=true;
        try { await ResetForNewSessionAsync(false); _connectionPending=true; SetWaiting(true, "Đang kết nối máy chủ…"); _network = CreateNetwork(); _isHost = false; await _network.JoinAsync(HostBox.Text, int.Parse(PortBox.Text), NicknameBox.Text); SetWaiting(false, "Đã kết nối · chờ thông tin ván"); }
        catch (SocketException ex) { _connectionPending=false; SetWaiting(false, "Kết nối thất bại"); MessageBox.Show(ExplainSocketError(ex), "Không thể kết nối"); }
        catch (Exception ex) { _connectionPending=false; SetWaiting(false, "Kết nối thất bại"); MessageBox.Show(ex.ToString(), "Không thể kết nối"); }
        finally { _roomActionBusy=false; RefreshActionControls(); }
    }
    private async Task ResetForNewSessionAsync(bool whitePerspective)
    {
        _gameStarted=false; _gameOver=false; _waitingForState=false; _clockRunning=false; _waitingForOpponent=false; _connectionPending=false;
        _selected=null; _legalTargets.Clear(); _sessionId=""; _sequence=0; _lastMoveFrom=null; _lastMoveTo=null;
        var previousNetwork=_network;
        _network=null;
        if (previousNetwork is not null) await previousNetwork.DisposeAsync();
        _rules.Load(ChessRulesService.InitialFen);
        _isHost=whitePerspective; _blackPerspective=!whitePerspective;
        ResultBanner.Visibility=Visibility.Collapsed; ResultBannerText.Text="";
        StatusBanner.Background=new SolidColorBrush(Color.FromRgb(34,37,43));
        GameResultLabel.Text="TRẬN ĐẤU"; GameResultLabel.Foreground=new SolidColorBrush(Color.FromRgb(129,182,76));
        HistoryBox.Clear(); ResignButton.IsEnabled=false;
        _whiteMilliseconds=_blackMilliseconds=GetTimeControlSeconds()*1000L;
        UpdateCoordinates(); RefreshClockLabels(); DrawBoard();
    }
    private static string[] GetLanIpv4Addresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
        .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
        .Select(info => info.Address)
        .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address) && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
        .Distinct()
        .Select(address => address.ToString())
        .ToArray();
    private static string ExplainSocketError(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.AddressAlreadyInUse => $"Cổng TCP đã được dùng. Hãy đổi cổng khác.\n\n{ex.Message}",
        SocketError.ConnectionRefused => $"Máy chủ từ chối kết nối. Kiểm tra máy kia đã bấm Tạo phòng và hai bên dùng cùng cổng chưa.\n\n{ex.Message}",
        SocketError.TimedOut => $"Kết nối hết thời gian chờ. Kiểm tra IP LAN, Windows Firewall và chế độ cô lập thiết bị Wi‑Fi (AP/client isolation).\n\n{ex.Message}",
        SocketError.NetworkUnreachable or SocketError.HostUnreachable => $"Không tới được máy chủ. Hai máy cần nằm trong mạng có thể liên lạc trực tiếp.\n\n{ex.Message}",
        _ => ex.Message
    };
    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_waitingForOpponent) return;
        _waitingForOpponent=false;
        var service=_network; _network=null;
        if (service is not null) await service.DisposeAsync();
        SetWaiting(false,"Đã hủy tạo phòng");
    }
    private void SetWaiting(bool waiting, string text)
    {
        StatusText.Text = text; StatusDot.Fill = waiting ? new SolidColorBrush(Color.FromRgb(129,182,76)) : new SolidColorBrush(Color.FromRgb(133,139,148));
        RefreshActionControls();
    }
    private void RefreshActionControls()
    {
        var playing=_gameStarted && !_gameOver;
        var locked=_roomActionBusy || _waitingForOpponent || _connectionPending || playing;
        HostButton.IsEnabled=JoinButton.IsEnabled=NicknameBox.IsEnabled=HostBox.IsEnabled=PortBox.IsEnabled=!locked;
        Time10Button.IsEnabled=Time5Button.IsEnabled=Time1Button.IsEnabled=!locked;
        ResignButton.IsEnabled=playing;
        CancelButton.Visibility=_waitingForOpponent ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled=_waitingForOpponent;
    }
    private TcpChessService CreateNetwork()
    {
        var service = new TcpChessService();
        service.StatusChanged += s => Dispatcher.Invoke(() =>
        {
            StatusText.Text = s; StatusDot.Fill = new SolidColorBrush(Color.FromRgb(129,182,76));
            if (_gameStarted && !_gameOver && !_closingWindow && s.Contains("ngắt kết nối",StringComparison.OrdinalIgnoreCase))
                _ = HandleOpponentDisconnectAsync();
        });
        service.MessageReceived += m => Dispatcher.Invoke(() => Receive(m));
        return service;
    }
    private Task HandleOpponentDisconnectAsync()
    {
        var winner=_isHost ? "Trắng" : "Đen";
        _clockRunning=false;
        ShowGameOver($"Đối thủ ngắt kết nối · {winner} thắng");
        return Task.CompletedTask;
    }
    private async void Receive(NetworkMessage m)
    {
        if (m.Command == "HELLO" && _isHost)
        {
            _blackPerspective=false;
            _waitingForOpponent=false; _connectionPending=false;
            _sessionId = _network!.SessionId;
            _gameStarted=true; ResignButton.IsEnabled=true;
            RefreshActionControls();
            _sound.PlayStart();
            StartClock(GetTimeControlSeconds());
            await _network.SendAsync(new NetworkMessage { Command="WELCOME", SessionId=_sessionId, Name=NicknameBox.Text, Fen=_rules.Fen, TimeControlSeconds=GetTimeControlSeconds(), WhiteMilliseconds=_whiteMilliseconds, BlackMilliseconds=_blackMilliseconds, ActiveColor="W" });
            StatusText.Text = $"Đang đấu với {m.Name} · lượt Trắng"; DrawBoard(); return;
        }
        if (m.Command == "WELCOME" && !_isHost && !string.IsNullOrWhiteSpace(m.SessionId))
        {
            _connectionPending=false; _waitingForOpponent=false;
            _blackPerspective=true; _sessionId=m.SessionId; _rules.Load(m.Fen); _sequence=0; _waitingForState=false;
            _gameStarted=true; ResignButton.IsEnabled=true; StartClock(m.TimeControlSeconds); ApplyClockMessage(m);
            RefreshActionControls();
            _sound.PlayStart();
            UpdateCoordinates();
            StatusText.Text=$"Đang đấu với {m.Name} · lượt Trắng"; DrawBoard(); return;
        }
        if (m.Command == "CLOCK" && !_isHost && _gameStarted && m.SessionId == _sessionId)
        {
            ApplyClockMessage(m); return;
        }
        if (m.Command == "GAME_OVER" && m.SessionId == _sessionId)
        {
            _whiteMilliseconds=m.WhiteMilliseconds; _blackMilliseconds=m.BlackMilliseconds;
            _clockRunning=false; RefreshClockLabels(); ShowGameOver(m.Result); return;
        }
        if (m.Command == "RESIGN" && _isHost && m.SessionId == _sessionId)
        {
            await FinishHostGameAsync("Đen đầu hàng · Trắng thắng"); return;
        }
        if (m.Command == "MOVE" && _isHost && m.Sequence == _sequence + 1)
        {
            if (m.SessionId != _sessionId) { _ = _network!.SendAsync(new NetworkMessage{Command="ERROR",Text="Sai phiên chơi."}); return; }
            var isCapture = IsCapture(m.From, m.To);
            var isCastling = IsCastling(m.From, m.To);
            UpdateHostClock();
            if (_blackMilliseconds<=0) { await FinishHostGameAsync("Hết giờ · Trắng thắng"); return; }
            if (_gameOver || _rules.Game.WhoseTurn != Player.Black || !_rules.Move(m.From,m.To,m.Promotion)) { _ = _network!.SendAsync(new NetworkMessage{Command="ERROR",Text="Nước đi không hợp lệ hoặc chưa đến lượt Đen."}); return; }
            SwitchClockTo(_rules.Game.WhoseTurn);
            _lastMoveFrom=m.From;
            _lastMoveTo=m.To;
            _sequence++;
            var result=GetGameResult();
            var moveText=FormatMove(m.From,m.To,result);
            AppendMoveHistory(_sequence,moveText);
            UpdateTurnStatus(result);
            if (string.IsNullOrEmpty(result)) PlayMoveSound(isCapture, isCastling);
            if (!string.IsNullOrEmpty(result)) _clockRunning=false;
            DrawBoard(); await BroadcastStateAsync(moveText,result,isCapture,isCastling,m.From,m.To);
            if (!string.IsNullOrEmpty(result)) ShowGameOver(result);
            return;
        }
        if (m.Command == "STATE" && !_isHost && m.SessionId == _sessionId && m.Sequence == _sequence + 1)
        {
            _sequence=m.Sequence; _rules.Load(m.Fen); _waitingForState=false; _selected=null; _legalTargets.Clear();
            _lastMoveFrom=m.From;
            _lastMoveTo=m.To;
            ApplyClockMessage(m);
            AppendMoveHistory(_sequence,m.MoveText);
            UpdateTurnStatus(m.Result);
            if (string.IsNullOrEmpty(m.Result)) PlayMoveSound(m.IsCapture, m.IsCastling);
            DrawBoard();
            if (!string.IsNullOrEmpty(m.Result)) ShowGameOver(m.Result);
        }
        if (m.Command == "ERROR") { _waitingForState=false; _selected=null; _legalTargets.Clear(); DrawBoard(); MessageBox.Show(m.Text,"Nước đi bị từ chối"); }
    }
    private async Task BroadcastStateAsync(string moveText, string result, bool isCapture, bool isCastling, string from, string to) => await _network!.SendAsync(new NetworkMessage{Command="STATE",SessionId=_network.SessionId,Sequence=_sequence,Fen=_rules.Fen,From=from,To=to,MoveText=moveText,Result=result,IsCapture=isCapture,IsCastling=isCastling,WhiteMilliseconds=_whiteMilliseconds,BlackMilliseconds=_blackMilliseconds,ActiveColor=_clockActive==Player.White?"W":"B"});
    private string GetGameResult()
    {
        if (_rules.IsCheckmate) return $"Chiếu hết! {( _rules.Game.WhoseTurn == Player.White ? "Đen" : "Trắng")} thắng.";
        if (_rules.IsStalemate) return "Hòa cờ do hết nước đi hợp lệ.";
        if (_rules.IsBareKings) return "Hòa cờ · chỉ còn hai vua.";
        if (_rules.Game.IsDraw()) return "Ván cờ hòa.";
        return "";
    }

    private int GetTimeControlSeconds() => _selectedTimeSeconds;
    private void TimeControl_Click(object sender,RoutedEventArgs e)
    {
        if (_gameStarted || sender is not Button button || !int.TryParse(button.Tag?.ToString(),out var seconds)) return;
        _selectedTimeSeconds=seconds;
        var selectedBrush=new SolidColorBrush(Color.FromRgb(129,182,76));
        var idleBrush=new SolidColorBrush(Color.FromRgb(52,57,65));
        Time10Button.Background=seconds==600?selectedBrush:idleBrush; Time10Button.Foreground=seconds==600?Brushes.Black:Brushes.White;
        Time5Button.Background=seconds==300?selectedBrush:idleBrush; Time5Button.Foreground=seconds==300?Brushes.Black:Brushes.White;
        Time1Button.Background=seconds==60?selectedBrush:idleBrush; Time1Button.Foreground=seconds==60?Brushes.Black:Brushes.White;
        _whiteMilliseconds=_blackMilliseconds=seconds*1000L; RefreshClockLabels();
    }
    private void DismissResultBanner_Click(object sender,RoutedEventArgs e)
    {
        // Dismiss only the visual notification; the completed game stays completed.
        ResultBanner.Visibility=Visibility.Collapsed;
    }
    private void StartClock(int seconds)
    {
        var milliseconds=Math.Clamp(seconds,60,600)*1000L;
        _whiteMilliseconds=milliseconds; _blackMilliseconds=milliseconds;
        _clockActive=Player.White; _clockTimestamp=Stopwatch.GetTimestamp(); _lastClockSync=_clockTimestamp;
        _clockRunning=true; RefreshClockLabels();
    }
    private void UpdateHostClock()
    {
        if (!_clockRunning || !_isHost) return;
        var now=Stopwatch.GetTimestamp();
        var elapsed=(long)Stopwatch.GetElapsedTime(_clockTimestamp,now).TotalMilliseconds;
        _clockTimestamp=now;
        if (_clockActive==Player.White) _whiteMilliseconds=Math.Max(0,_whiteMilliseconds-elapsed);
        else _blackMilliseconds=Math.Max(0,_blackMilliseconds-elapsed);
        RefreshClockLabels();
    }
    private void SwitchClockTo(Player player)
    {
        _clockActive=player; _clockTimestamp=Stopwatch.GetTimestamp(); RefreshClockLabels();
    }
    private void ApplyClockMessage(NetworkMessage message)
    {
        _whiteMilliseconds=Math.Max(0,message.WhiteMilliseconds);
        _blackMilliseconds=Math.Max(0,message.BlackMilliseconds);
        _clockActive=message.ActiveColor=="B" ? Player.Black : Player.White;
        _clockTimestamp=Stopwatch.GetTimestamp(); _clockRunning=true;
        RefreshClockLabels();
    }
    private void RefreshClockLabels()
    {
        var topColor=_blackPerspective?Player.White:Player.Black;
        var bottomColor=_blackPerspective?Player.Black:Player.White;
        TopClockText.Text=$"{(topColor==Player.White?"TRẮNG":"ĐEN")}  {FormatClock(topColor==Player.White?_whiteMilliseconds:_blackMilliseconds)}";
        BottomClockText.Text=$"{(bottomColor==Player.White?"TRẮNG":"ĐEN")}  {FormatClock(bottomColor==Player.White?_whiteMilliseconds:_blackMilliseconds)}";
        var activeBrush=new SolidColorBrush(Color.FromRgb(129,182,76));
        var idleBrush=new SolidColorBrush(Color.FromRgb(25,28,33));
        TopClockBorder.Background=_clockRunning && _clockActive==topColor ? activeBrush : idleBrush;
        BottomClockBorder.Background=_clockRunning && _clockActive==bottomColor ? activeBrush : idleBrush;
    }
    private static string FormatClock(long milliseconds)
    {
        var time=TimeSpan.FromMilliseconds(Math.Max(0,milliseconds));
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
    private async void ClockTimer_Tick(object? sender,EventArgs e)
    {
        if (!_clockRunning || !_gameStarted || _gameOver) return;
        if (_isHost)
        {
            UpdateHostClock();
            if ((_clockActive==Player.White?_whiteMilliseconds:_blackMilliseconds)<=0)
            {
                var winner=_clockActive==Player.White ? "Đen" : "Trắng";
                await FinishHostGameAsync($"Hết giờ · {winner} thắng"); return;
            }
            var now=Stopwatch.GetTimestamp();
            if (Stopwatch.GetElapsedTime(_lastClockSync,now).TotalMilliseconds>=1000 && _network is not null)
            {
                _lastClockSync=now;
                try { await _network.SendAsync(new NetworkMessage{Command="CLOCK",SessionId=_sessionId,WhiteMilliseconds=_whiteMilliseconds,BlackMilliseconds=_blackMilliseconds,ActiveColor=_clockActive==Player.White?"W":"B"}); }
                catch (IOException) { }
            }
        }
        else
        {
            var now=Stopwatch.GetTimestamp();
            var elapsed=(long)Stopwatch.GetElapsedTime(_clockTimestamp,now).TotalMilliseconds;
            _clockTimestamp=now;
            if (_clockActive==Player.White) _whiteMilliseconds=Math.Max(0,_whiteMilliseconds-elapsed);
            else _blackMilliseconds=Math.Max(0,_blackMilliseconds-elapsed);
            RefreshClockLabels();
        }
    }
    private async Task FinishHostGameAsync(string result)
    {
        if (_gameOver) return;
        UpdateHostClock();
        _clockRunning=false; ShowGameOver(result);
        if (_network is not null && !string.IsNullOrEmpty(_sessionId))
            await _network.SendAsync(new NetworkMessage{Command="GAME_OVER",SessionId=_sessionId,Result=result,WhiteMilliseconds=_whiteMilliseconds,BlackMilliseconds=_blackMilliseconds});
    }
    private async void Resign_Click(object sender,RoutedEventArgs e)
    {
        if (!_gameStarted || _gameOver || _network is null) return;
        if (_isHost) await FinishHostGameAsync("Trắng đầu hàng · Đen thắng");
        else
        {
            ResignButton.IsEnabled=false;
            await _network.SendAsync(new NetworkMessage{Command="RESIGN",SessionId=_sessionId});
            StatusText.Text="Đã gửi yêu cầu đầu hàng…";
        }
    }
    private string FormatMove(string from, string to, string result)
    {
        if (result.StartsWith("Chiếu hết", StringComparison.Ordinal)) return $"{from}–{to} #";
        if (_rules.Game.IsInCheck(_rules.Game.WhoseTurn)) return $"{from}–{to} +";
        return $"{from}–{to}";
    }
    private void AppendMoveHistory(int sequence, string moveText)
    {
        if (string.IsNullOrWhiteSpace(moveText)) return;
        var prefix=sequence % 2 == 1 ? $"{(sequence+1)/2}. " : "   ";
        HistoryBox.AppendText(prefix+moveText+(sequence % 2 == 0 ? Environment.NewLine : "    "));
        HistoryBox.ScrollToEnd();
    }
    private void UpdateTurnStatus(string result)
    {
        if (!string.IsNullOrEmpty(result)) { StatusText.Text="Ván đấu đã kết thúc"; return; }
        var side=_rules.Game.WhoseTurn == Player.White ? "Trắng" : "Đen";
        StatusText.Text=$"Đang đấu · lượt {side}{(_rules.Game.IsInCheck(_rules.Game.WhoseTurn) ? " · đang bị chiếu" : "")}";
    }
    private void ShowGameOver(string result)
    {
        if (_gameOver) return;
        _gameOver=true;
        _sound.PlayEnd();
        _clockRunning=false; ResignButton.IsEnabled=false; RefreshClockLabels();
        var isCheckmate=result.StartsWith("Chiếu hết",StringComparison.Ordinal);
        var bareKings=result.Contains("chỉ còn hai vua",StringComparison.OrdinalIgnoreCase);
        var isDraw=result.Contains("hòa",StringComparison.OrdinalIgnoreCase);
        var reason=isCheckmate ? "CHECKMATE" : result.StartsWith("Hết giờ",StringComparison.OrdinalIgnoreCase) ? "TIMEOUT" : result.Contains("đầu hàng",StringComparison.OrdinalIgnoreCase) ? "RESIGN" : result.Contains("ngắt kết nối",StringComparison.OrdinalIgnoreCase) ? "DISCONNECT" : bareKings ? "DRAW — BARE KINGS" : result.Contains("hết nước đi",StringComparison.OrdinalIgnoreCase) ? "STALEMATE — DRAW" : "DRAW";
        var blackWon=result.Contains("Đen thắng",StringComparison.Ordinal);
        var whiteWon=result.Contains("Trắng thắng",StringComparison.Ordinal);
        _losingPlayer=whiteWon ? Player.Black : blackWon ? Player.White : null;
        var banner=isDraw ? reason : $"{reason} — {(whiteWon ? "WHITE WON" : "BLACK WON")}";
        GameResultLabel.Text=reason;
        GameResultLabel.Foreground=new SolidColorBrush(isCheckmate ? Color.FromRgb(235,112,94) : isDraw ? Color.FromRgb(129,182,76) : Color.FromRgb(235,145,98));
        StatusText.Text="Ván đấu đã kết thúc";
        StatusBanner.Background=new SolidColorBrush(isDraw ? Color.FromRgb(54,75,48) : Color.FromRgb(91,42,40));
        StatusDot.Fill=new SolidColorBrush(isDraw ? Color.FromRgb(129,182,76) : Color.FromRgb(235,112,94));
        ResultBannerText.Text=banner;
        ResultBanner.BorderBrush=new SolidColorBrush(isDraw ? Color.FromRgb(129,182,76) : Color.FromRgb(235,112,94));
        ResultBanner.Visibility=Visibility.Visible;
        RefreshActionControls();
        if (_losingPlayer.HasValue || bareKings) DrawBoard();
    }
    private void DrawBoard()
    {
        Board.Children.Clear();
        UpdateCoordinates();
        var kingToTilt = _gameOver && _losingPlayer.HasValue ? FindKingSquare(_losingPlayer.Value) : null;
        var kingInCheck = _rules.Game.IsInCheck(_rules.Game.WhoseTurn)
            ? FindKingSquare(_rules.Game.WhoseTurn)
            : null;
        var grayKingSquares=_gameOver && _rules.IsBareKings;
        for(int row=0;row<8;row++) for(int col=0;col<8;col++)
        {
            var file=_blackPerspective ? (char)('h'-col) : (char)('a'+col);
            var rank=_blackPerspective ? row+1 : 8-row;
            var square=$"{file}{rank}";
            var piece=_rules.Game.GetPieceAt(new Position(square));
            var content = new Grid();
            if (_legalTargets.Contains(square))
            {
                var occupied = piece is not null;
                content.Children.Add(new Ellipse
                {
                    Width=occupied ? 48 : 16, Height=occupied ? 48 : 16,
                    Stroke=occupied ? new SolidColorBrush(Color.FromArgb(210,40,42,35)) : Brushes.Transparent,
                    StrokeThickness=occupied ? 4 : 0,
                    Fill=occupied ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(105,34,38,30)),
                    HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center,
                    IsHitTestVisible=false
                });
            }
            if (piece is not null)
            {
                var visual = new ChessPieceVisual(piece.GetFenCharacter())
                {
                    Width=58, Height=60, HorizontalAlignment=HorizontalAlignment.Center,
                    VerticalAlignment=VerticalAlignment.Center, IsHitTestVisible=false
                };
                if (square == kingToTilt) { visual.RenderTransform = new RotateTransform(90); visual.RenderTransformOrigin = new Point(0.5,0.5); }
                content.Children.Add(visual);
            }
            var lightSquare=((file-'a')+rank)%2==0;
            var squareColor=lightSquare?Color.FromRgb(238,238,210):Color.FromRgb(118,150,86);
            var isKing=piece is not null && char.ToLowerInvariant(piece.GetFenCharacter())=='k';
            var background=grayKingSquares && isKing
                ? new SolidColorBrush(Color.FromRgb(135,139,143))
                : square==kingToTilt
                ? new SolidColorBrush(Color.FromRgb(205,72,67))
                : square==kingInCheck
                ? new SolidColorBrush(Color.FromRgb(205,72,67))
                : _selected==square ? new SolidColorBrush(Color.FromRgb(246,225,95))
                : square==_lastMoveFrom || square==_lastMoveTo ? new SolidColorBrush(Color.FromRgb(154,204,239))
                : new SolidColorBrush(squareColor);
            var b=new Button{Style=(Style)FindResource("SquareButton"),Content=content,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=background,Tag=square};
            b.Click+=Square_Click; Board.Children.Add(b);
        }
    }
    private void UpdateCoordinates()
    {
        for (var i=0;i<8;i++)
        {
            ((TextBlock)RankCoordinates.Children[i]).Text=(_blackPerspective ? i+1 : 8-i).ToString();
            ((TextBlock)FileCoordinates.Children[i]).Text=((char)(_blackPerspective ? 'h'-i : 'a'+i)).ToString();
        }
    }
    private async void Square_Click(object sender,RoutedEventArgs e)
    {
        if (!_gameStarted || _waitingForState || _gameOver) return;
        var square=(string)((Button)sender).Tag;
        var localColor=_isHost ? Player.White : Player.Black;
        if (_network is null || _rules.Game.WhoseTurn != localColor) return;

        var clickedPiece=_rules.Game.GetPieceAt(new Position(square));
        if (_selected is null)
        {
            SelectOwnPiece(square, clickedPiece, localColor);
            return;
        }
        if (clickedPiece is not null && clickedPiece.Owner == localColor)
        {
            SelectOwnPiece(square, clickedPiece, localColor);
            return;
        }
        if (!_legalTargets.Contains(square)) return;

        var from=_selected;
        var movingPiece=_rules.Game.GetPieceAt(new Position(from));
        var isCapture=IsCapture(from,square);
        var isCastling=IsCastling(from,square);
        var isPromotion=movingPiece is not null && (movingPiece.GetFenCharacter() is 'P' or 'p') && (square.EndsWith('8') || square.EndsWith('1'));
        var promotion='q';
        if (isPromotion)
        {
            var chooser=new PromotionDialog(localColor == Player.White) { Owner=this };
            if (chooser.ShowDialog() != true || chooser.SelectedPiece is null) return;
            promotion=chooser.SelectedPiece.Value;
        }
        if (_isHost)
        {
            UpdateHostClock();
            if (_whiteMilliseconds<=0) { await FinishHostGameAsync("Hết giờ · Đen thắng"); return; }
            if (!_rules.Move(from,square,promotion.ToString())) { _selected=null; _legalTargets.Clear(); DrawBoard(); return; }
            _sequence++; _selected=null; _legalTargets.Clear();
            SwitchClockTo(_rules.Game.WhoseTurn);
            _lastMoveFrom=from;
            _lastMoveTo=square;
            var result=GetGameResult();
            var moveText=FormatMove(from,square,result);
            AppendMoveHistory(_sequence,moveText); UpdateTurnStatus(result);
            if (string.IsNullOrEmpty(result)) PlayMoveSound(isCapture, isCastling);
            DrawBoard();
            if (!string.IsNullOrEmpty(result)) _clockRunning=false;
            await BroadcastStateAsync(moveText,result,isCapture,isCastling,from,square);
            if (!string.IsNullOrEmpty(result)) ShowGameOver(result);
        }
        else
        {
            _waitingForState=true;
            await _network.SendAsync(new NetworkMessage{Command="MOVE",SessionId=_sessionId,Sequence=_sequence+1,From=from,To=square,Promotion=promotion.ToString()});
            _selected=null; _legalTargets.Clear(); DrawBoard(); StatusText.Text="Đang chờ máy chủ xác nhận nước đi…";
        }
    }
    private void SelectOwnPiece(string square, Piece? piece, Player localColor)
    {
        if (piece is null || piece.Owner != localColor) { _selected=null; _legalTargets.Clear(); DrawBoard(); return; }
        _selected=square; _legalTargets.Clear();
        foreach (var target in _rules.LegalDestinations(square)) _legalTargets.Add(target);
        DrawBoard();
    }
    private bool IsCapture(string from, string to)
    {
        var target = _rules.Game.GetPieceAt(new Position(to));
        if (target is not null) return true;

        var movingPiece = _rules.Game.GetPieceAt(new Position(from));
        return movingPiece is not null
            && movingPiece.GetFenCharacter() is 'P' or 'p'
            && from[0] != to[0];
    }
    private bool IsCastling(string from, string to)
    {
        var piece = _rules.Game.GetPieceAt(new Position(from));
        return piece is not null
            && (piece.GetFenCharacter() is 'K' or 'k')
            && from[1] == to[1]
            && Math.Abs(from[0] - to[0]) == 2;
    }
    private void PlayMoveSound(bool isCapture, bool isCastling) =>
        _sound.PlayMove(isCapture, _rules.Game.IsInCheck(_rules.Game.WhoseTurn), isCastling);
    private string? FindKingSquare(Player color)
    {
        for (var rank=1;rank<=8;rank++) for (var file='a';file<='h';file++)
        {
            var square=$"{file}{rank}";
            var piece=_rules.Game.GetPieceAt(new Position(square));
            if (piece is not null && piece.Owner==color && char.ToLowerInvariant(piece.GetFenCharacter())=='k') return square;
        }
        return null;
    }
    protected override async void OnClosed(EventArgs e){_closingWindow=true;_clockTimer.Stop();if(_network is not null)await _network.DisposeAsync();base.OnClosed(e);}
}
