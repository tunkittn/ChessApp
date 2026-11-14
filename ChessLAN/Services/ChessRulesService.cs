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
        try
        {
            var origin = new Position(from);
            var destination = new Position(to);
            var movingPiece = Game.GetPieceAt(origin);
            if (movingPiece is null || movingPiece.Owner != Game.WhoseTurn) return false;

            var targetPiece = Game.GetPieceAt(destination);
            // A chess game ends by checkmate; the king is never captured.
            if (targetPiece is not null && (targetPiece.Owner == movingPiece.Owner || targetPiece.GetFenCharacter() is 'K' or 'k')) return false;

            var isPromotion = movingPiece.GetFenCharacter() is 'P' or 'p' && (to.EndsWith('8') || to.EndsWith('1'));
            char? promotionPiece = isPromotion
                ? (string.IsNullOrWhiteSpace(promotion) ? 'q' : char.ToLowerInvariant(promotion[0]))
                : null;
            if (promotionPiece is not null && promotionPiece is not ('q' or 'r' or 'b' or 'n')) return false;

            var legalMove = Game.GetValidMoves(origin).FirstOrDefault(m =>
                string.Equals(m.NewPosition.ToString(), destination.ToString(), StringComparison.OrdinalIgnoreCase) &&
                (m.Promotion is null && promotionPiece is null ||
                 m.Promotion is not null && promotionPiece is not null && char.ToLowerInvariant(m.Promotion.Value) == promotionPiece.Value));
            if (legalMove is null || !Game.IsValidMove(legalMove)) return false;

            return Game.MakeMove(legalMove, true) != MoveType.Invalid;
        }
        catch { return false; }
    }
    public void Load(string fen) => Game = new ChessGame(fen);
    public string PieceAt(string square)
    {
        try
        {
            var piece = Game.GetPieceAt(new Position(square));
            if (piece is null) return "";
            var key = piece.GetFenCharacter().ToString();
            return key switch { "K"=>"♔", "Q"=>"♕", "R"=>"♖", "B"=>"♗", "N"=>"♘", "P"=>"♙", "k"=>"♚", "q"=>"♛", "r"=>"♜", "b"=>"♝", "n"=>"♞", "p"=>"♟", _=>"" };
        }
        catch { return ""; }
    }
}
