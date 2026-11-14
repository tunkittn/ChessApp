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
}
