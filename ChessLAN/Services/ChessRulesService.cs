using ChessDotNet;

namespace ChessLAN.Services;

/// <summary>Wraps ChessDotNet for legal move generation, check detection and FEN state.</summary>
public sealed class ChessRulesService
{
    public const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public ChessGame Game { get; private set; } = new();
    public string Fen => Game.GetFen();
    public bool IsCheckmate => Game.IsCheckmated(Game.WhoseTurn);
    public bool IsStalemate => Game.IsStalemated(Game.WhoseTurn);
    public IEnumerable<string> LegalDestinations(string square)
    {
        try { return Game.GetValidMoves(new Position(square)).Select(x => x.NewPosition.ToString()).ToArray(); }
        catch { return []; }
    }
    public bool Move(string from, string to, string promotion = "q")
    {
        try { return Game.MakeMove(new Move(new Position(from), new Position(to), Game.WhoseTurn, string.IsNullOrEmpty(promotion) ? 'q' : char.ToLowerInvariant(promotion[0])), true) != MoveType.Invalid; }
        catch { return false; }
    }
    public void Load(string fen) => Game = new ChessGame(fen);
    public string PieceAt(string square)
    {
        try
        {
            var piece = Game.GetPieceAt(new Position(square));
            if (piece is null) return "";
            var key = piece.ToString();
            return key switch { "K"=>"♔", "Q"=>"♕", "R"=>"♖", "B"=>"♗", "N"=>"♘", "P"=>"♙", "k"=>"♚", "q"=>"♛", "r"=>"♜", "b"=>"♝", "n"=>"♞", "p"=>"♟", _=>"" };
        }
        catch { return ""; }
    }
}
