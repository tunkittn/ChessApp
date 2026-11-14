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
using ChessDotNet;

namespace ChessLAN;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ChessRulesService _rules = new();
    private TcpChessService? _network;
    private string? _selected;
    private readonly HashSet<string> _legalTargets = new(StringComparer.OrdinalIgnoreCase);
    private string _sessionId = "";
    private int _sequence;
    private bool _isHost;
    private bool _waitingForState;
    private bool _gameOver;
    private CancellationTokenSource? _roomCancellation;
    public MainWindow()
    {
        InitializeComponent();
        DrawBoard();
    }

    private async void Host_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _roomCancellation = new CancellationTokenSource(); _network = CreateNetwork(); _isHost = true;
            SetWaiting(true, $"Đang chờ đối thủ · cổng {PortBox.Text}");
            await _network.HostAsync(int.Parse(PortBox.Text), NicknameBox.Text, _roomCancellation.Token);
            SetWaiting(false, "Đối thủ đã kết nối · chờ xác nhận");
        }
        catch (OperationCanceledException) { SetWaiting(false, "Đã hủy tạo phòng"); }
        catch (Exception ex) { SetWaiting(false, "Không tạo được phòng"); MessageBox.Show(ex.Message, "Không thể tạo phòng"); }
    }
    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        try { SetWaiting(true, "Đang kết nối máy chủ…"); _network = CreateNetwork(); _isHost = false; await _network.JoinAsync(HostBox.Text, int.Parse(PortBox.Text), NicknameBox.Text); SetWaiting(false, "Đã kết nối · chờ thông tin ván"); }
        catch (Exception ex) { SetWaiting(false, "Kết nối thất bại"); MessageBox.Show(ex.Message, "Không thể kết nối"); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _roomCancellation?.Cancel(); _ = _network?.DisposeAsync(); }
    private void SetWaiting(bool waiting, string text)
    {
        StatusText.Text = text; StatusDot.Fill = waiting ? new SolidColorBrush(Color.FromRgb(129,182,76)) : new SolidColorBrush(Color.FromRgb(133,139,148));
        HostButton.IsEnabled = JoinButton.IsEnabled = !waiting; CancelButton.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;
    }
    private TcpChessService CreateNetwork()
    {
        var service = new TcpChessService();
        service.StatusChanged += s => Dispatcher.Invoke(() => { StatusText.Text = s; StatusDot.Fill = new SolidColorBrush(Color.FromRgb(129,182,76)); });
        service.MessageReceived += m => Dispatcher.Invoke(() => Receive(m));
        return service;
    }
    private async void Receive(NetworkMessage m)
    {
        if (m.Command == "HELLO" && _isHost)
        {
            _sessionId = _network!.SessionId;
            await _network.SendAsync(new NetworkMessage { Command="WELCOME", SessionId=_sessionId, Name=NicknameBox.Text, Fen=_rules.Fen });
            StatusText.Text = $"Đang đấu với {m.Name} · lượt Trắng"; DrawBoard(); return;
        }
        if (m.Command == "WELCOME" && !_isHost && !string.IsNullOrWhiteSpace(m.SessionId))
        {
            _sessionId=m.SessionId; _rules.Load(m.Fen); _sequence=0; _waitingForState=false;
            StatusText.Text=$"Đang đấu với {m.Name} · lượt Trắng"; DrawBoard(); return;
        }
        if (m.Command == "MOVE" && _isHost && m.Sequence == _sequence + 1)
        {
            if (m.SessionId != _sessionId) { _ = _network!.SendAsync(new NetworkMessage{Command="ERROR",Text="Sai phiên chơi."}); return; }
            if (_gameOver || _rules.Game.WhoseTurn != Player.Black || !_rules.Move(m.From,m.To,m.Promotion)) { _ = _network!.SendAsync(new NetworkMessage{Command="ERROR",Text="Nước đi không hợp lệ hoặc chưa đến lượt Đen."}); return; }
            _sequence++;
            var result=GetGameResult();
            var moveText=FormatMove(m.From,m.To,result);
            AppendMoveHistory(_sequence,moveText);
            UpdateTurnStatus(result);
            DrawBoard(); await BroadcastStateAsync(moveText,result);
            if (!string.IsNullOrEmpty(result)) ShowGameOver(result);
            return;
        }
        if (m.Command == "STATE" && !_isHost && m.SessionId == _sessionId && m.Sequence == _sequence + 1)
        {
            _sequence=m.Sequence; _rules.Load(m.Fen); _waitingForState=false; _selected=null; _legalTargets.Clear();
            AppendMoveHistory(_sequence,m.MoveText);
            UpdateTurnStatus(m.Result); DrawBoard();
            if (!string.IsNullOrEmpty(m.Result)) ShowGameOver(m.Result);
        }
        if (m.Command == "ERROR") { _waitingForState=false; _selected=null; _legalTargets.Clear(); DrawBoard(); MessageBox.Show(m.Text,"Nước đi bị từ chối"); }
    }
    private async Task BroadcastStateAsync(string moveText, string result) => await _network!.SendAsync(new NetworkMessage{Command="STATE",SessionId=_network.SessionId,Sequence=_sequence,Fen=_rules.Fen,MoveText=moveText,Result=result});
    private string GetGameResult()
    {
        if (_rules.IsCheckmate) return $"Chiếu hết! {( _rules.Game.WhoseTurn == Player.White ? "Đen" : "Trắng")} thắng.";
        if (_rules.IsStalemate) return "Hòa cờ do hết nước đi hợp lệ.";
        if (_rules.Game.IsDraw()) return "Ván cờ hòa.";
        return "";
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
        var isCheckmate=result.StartsWith("Chiếu hết",StringComparison.Ordinal);
        GameResultLabel.Text=isCheckmate ? "CHECKMATE" : "HÒA CỜ";
        GameResultLabel.Foreground=new SolidColorBrush(isCheckmate ? Color.FromRgb(235,112,94) : Color.FromRgb(129,182,76));
        var winner=result.Contains("Đen thắng",StringComparison.Ordinal) ? "Đen thắng" : result.Contains("Trắng thắng",StringComparison.Ordinal) ? "Trắng thắng" : "Hòa cờ";
        StatusText.Text=isCheckmate ? $"CHECKMATE · {winner}" : result;
        StatusDot.Fill=new SolidColorBrush(isCheckmate ? Color.FromRgb(235,112,94) : Color.FromRgb(129,182,76));
    }
    private void DrawBoard()
    {
        Board.Children.Clear();
        for(int row=0;row<8;row++) for(int col=0;col<8;col++)
        {
            var file=(char)('a'+col); var rank=8-row; var square=$"{file}{rank}";
            var piece=_rules.PieceAt(square);
            var isWhite=piece.Length>0 && char.IsUpper(_rules.Game.GetPieceAt(new ChessDotNet.Position(square)).GetFenCharacter());
            var glyph=new TextBlock
            {
                Text=piece, FontFamily=new FontFamily("Segoe UI Symbol"), FontSize=60,
                FontWeight=FontWeights.Normal, HorizontalAlignment=HorizontalAlignment.Center,
                VerticalAlignment=VerticalAlignment.Center, TextAlignment=TextAlignment.Center,
                Foreground=isWhite ? new SolidColorBrush(Color.FromRgb(255,253,236)) : new SolidColorBrush(Color.FromRgb(25,29,27)),
                Effect=isWhite ? new System.Windows.Media.Effects.DropShadowEffect { Color=Color.FromRgb(30,37,25), BlurRadius=0, ShadowDepth=1, Opacity=0.9 } : null
            };
            var content = new Grid();
            if (_legalTargets.Contains(square))
            {
                var occupied = _rules.Game.GetPieceAt(new Position(square)) is not null;
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
            content.Children.Add(glyph);
            var lightSquare=(row+col)%2==0;
            var squareColor=lightSquare?Color.FromRgb(238,238,210):Color.FromRgb(118,150,86);
            var background=_selected==square ? new SolidColorBrush(Color.FromRgb(246,225,95)) : new SolidColorBrush(squareColor);
            var b=new Button{Style=(Style)FindResource("SquareButton"),Content=content,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=background,Tag=square};
            b.Click+=Square_Click; Board.Children.Add(b);
        }
    }
    private async void Square_Click(object sender,RoutedEventArgs e)
    {
        if (_waitingForState || _gameOver) return;
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
        if (_isHost)
        {
            if (!_rules.Move(from,square)) { _selected=null; _legalTargets.Clear(); DrawBoard(); return; }
            _sequence++; _selected=null; _legalTargets.Clear(); DrawBoard();
            var result=GetGameResult();
            var moveText=FormatMove(from,square,result);
            AppendMoveHistory(_sequence,moveText); UpdateTurnStatus(result);
            await BroadcastStateAsync(moveText,result);
            if (!string.IsNullOrEmpty(result)) ShowGameOver(result);
        }
        else
        {
            _waitingForState=true;
            await _network.SendAsync(new NetworkMessage{Command="MOVE",SessionId=_sessionId,Sequence=_sequence+1,From=from,To=square,Promotion="q"});
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
    protected override async void OnClosed(EventArgs e){_roomCancellation?.Cancel();if(_network is not null)await _network.DisposeAsync();base.OnClosed(e);}
}
