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

namespace ChessLAN;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ChessRulesService _rules = new();
    private TcpChessService? _network;
    private string? _selected;
    private string _sessionId = "";
    private int _sequence;
    private bool _isHost;
    public MainWindow()
    {
        InitializeComponent();
        DrawBoard();
    }

    private async void Host_Click(object sender, RoutedEventArgs e)
    {
        try { _network = CreateNetwork(); _isHost = true; await _network.HostAsync(int.Parse(PortBox.Text), NicknameBox.Text); StatusText.Text = "Đang chờ đối thủ…"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể tạo phòng"); }
    }
    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        try { _network = CreateNetwork(); _isHost = false; await _network.JoinAsync(HostBox.Text, int.Parse(PortBox.Text), NicknameBox.Text); StatusText.Text = "Đã kết nối máy chủ"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể kết nối"); }
    }
    private TcpChessService CreateNetwork()
    {
        var service = new TcpChessService();
        service.StatusChanged += s => Dispatcher.Invoke(() => StatusText.Text = s);
        service.MessageReceived += m => Dispatcher.Invoke(() => Receive(m));
        return service;
    }
    private void Receive(NetworkMessage m)
    {
        if (m.Command == "HELLO" && _isHost) { _ = _network!.SendAsync(new NetworkMessage { Command="WELCOME", SessionId=_network.SessionId, Name=NicknameBox.Text, Fen=_rules.Fen }); StatusText.Text = $"Đang đấu với {m.Name}"; return; }
        if (m.Command == "WELCOME") { _sessionId=m.SessionId; _rules.Load(m.Fen); StatusText.Text=$"Đang đấu với {m.Name}"; DrawBoard(); return; }
        if (m.Command == "MOVE" && _isHost && m.Sequence == _sequence + 1)
        {
            if (_rules.Move(m.From,m.To,m.Promotion)) { _sequence++; _=BroadcastStateAsync(); }
            else _= _network!.SendAsync(new NetworkMessage{Command="ERROR",Text="Nước đi không hợp lệ."}); return;
        }
        if (m.Command == "STATE" && !_isHost && m.Sequence == _sequence + 1) { _sequence=m.Sequence; _rules.Load(m.Fen); DrawBoard(); }
        if (m.Command == "ERROR") MessageBox.Show(m.Text,"Nước đi bị từ chối");
    }
    private async Task BroadcastStateAsync() => await _network!.SendAsync(new NetworkMessage{Command="STATE",SessionId=_network.SessionId,Sequence=_sequence,Fen=_rules.Fen});
    private void DrawBoard()
    {
        Board.Children.Clear();
        for(int row=0;row<8;row++) for(int col=0;col<8;col++)
        {
            var file=(char)('a'+col); var rank=8-row; var square=$"{file}{rank}";
            var b=new Button{Content=_rules.PieceAt(square),FontSize=42,BorderThickness=new Thickness(0),Background=(row+col)%2==0?new SolidColorBrush(Color.FromRgb(238,238,210)):new SolidColorBrush(Color.FromRgb(118,150,86)),Foreground=Brushes.Black,Tag=square};
            b.Click+=Square_Click; Board.Children.Add(b);
        }
    }
    private async void Square_Click(object sender,RoutedEventArgs e)
    {
        var square=(string)((Button)sender).Tag;
        if(_selected is null){_selected=square;return;}
        if(_isHost){if(_rules.Move(_selected,square)){_sequence++;DrawBoard();await BroadcastStateAsync();HistoryBox.AppendText($"{_sequence}. {_selected}–{square}\n");}}
        else await _network!.SendAsync(new NetworkMessage{Command="MOVE",SessionId=_sessionId,Sequence=_sequence+1,From=_selected,To=square,Promotion="q"});
        _selected=null;
    }
    protected override async void OnClosed(EventArgs e){if(_network is not null)await _network.DisposeAsync();base.OnClosed(e);}
}
