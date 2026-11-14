using ChessLAN.Services;

namespace ChessLAN.Tests;

public sealed class ChessRulesTests
{
    [Fact]
    public void OpeningPawnMayAdvanceOneOrTwoSquares()
    {
        var game = new ChessRulesService();

        Assert.True(game.Move("e2", "e4"));
        Assert.False(game.Move("d2", "d4")); // White cannot move twice in a row.
    }

    [Fact]
    public void PawnCannotMoveThreeSquaresOrThroughAnOccupiedSquare()
    {
        var game = new ChessRulesService();
        Assert.False(game.Move("e2", "e5"));

        game.Load("4k3/8/8/8/8/4p3/4P3/4K3 w - - 0 1");
        Assert.False(game.Move("e2", "e4")); // The black pawn blocks the path.
    }

    [Fact]
    public void APlayerCannotCaptureTheirOwnPieceOrTheEnemyKing()
    {
        var game = new ChessRulesService();
        Assert.False(game.Move("e2", "e1"));

        game.Load("4k3/4R3/8/8/8/8/8/4K3 w - - 0 1");
        Assert.False(game.Move("e7", "e8"));
    }

    [Fact]
    public void EnPassantIsLegal()
    {
        var game = new ChessRulesService();
        game.Load("rnbqkbnr/ppp1pppp/8/3pP3/8/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 3");

        Assert.True(game.Move("e5", "d6"));
        Assert.Equal("♙", game.PieceAt("d6"));
        Assert.Equal("", game.PieceAt("d5"));
    }

    [Fact]
    public void CastlingAndPromotionAreLegal()
    {
        var game = new ChessRulesService();
        game.Load("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        Assert.True(game.Move("e1", "g1"));

        game.Load("4k3/P7/8/8/8/8/8/4K3 w - - 0 1");
        Assert.True(game.Move("a7", "a8", "q"));
        Assert.Equal("♕", game.PieceAt("a8"));
    }

    [Fact]
    public void PinnedPieceCannotExposeItsKingToCheck()
    {
        var game = new ChessRulesService();
        game.Load("4r1k1/8/8/8/8/8/4R3/4K3 w - - 0 1");

        Assert.False(game.Move("e2", "d2"));
    }

    [Fact]
    public void FoolsmatePositionIsDetectedAsCheckmate()
    {
        var game = new ChessRulesService();
        game.Load("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3");

        Assert.True(game.IsCheckmate);
        Assert.False(game.IsStalemate);
    }

    [Fact]
    public void FoolsmateSequenceEndsInCheckmate()
    {
        var game = new ChessRulesService();

        Assert.True(game.Move("f2", "f3"));
        Assert.True(game.Move("e7", "e5"));
        Assert.True(game.Move("g2", "g4"));
        Assert.True(game.Move("d8", "h4"));
        Assert.True(game.IsCheckmate);
    }
}
