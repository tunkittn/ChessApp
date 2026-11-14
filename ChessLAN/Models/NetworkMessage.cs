namespace ChessLAN.Models;

public sealed class NetworkMessage
{
    public int Version { get; set; } = 1;
    public string Command { get; set; } = "";
    public string SessionId { get; set; } = "";
    public int Sequence { get; set; }
    public string Name { get; set; } = "";
    public string Fen { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Promotion { get; set; } = "";
    public string Text { get; set; } = "";
    public string MoveText { get; set; } = "";
    public string Result { get; set; } = "";
    public bool IsCapture { get; set; }
    public int TimeControlSeconds { get; set; }
    public long WhiteMilliseconds { get; set; }
    public long BlackMilliseconds { get; set; }
    public string ActiveColor { get; set; } = "";
}
