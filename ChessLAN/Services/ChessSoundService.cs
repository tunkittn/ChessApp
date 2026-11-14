using System.IO;
using System.Windows.Media;

namespace ChessLAN.Services;

public sealed class ChessSoundService
{
    private readonly List<MediaPlayer> _activePlayers = [];

    public void PlayMove(bool isCapture, bool givesCheck)
    {
        var fileName = givesCheck
            ? "check.MP3"
            : isCapture
                ? "capture.MP3"
                : "move.MP3";

        var soundPath = Path.Combine(AppContext.BaseDirectory, "Sounds", fileName);
        if (!File.Exists(soundPath)) return;

        var player = new MediaPlayer { Volume = 1.0 };
        _activePlayers.Add(player);

        EventHandler? mediaOpened = null;
        mediaOpened = (_, _) =>
        {
            player.MediaOpened -= mediaOpened;
            player.Position = TimeSpan.Zero;
            player.Play();
        };

        player.MediaEnded += (_, _) =>
        {
            player.Close();
            _activePlayers.Remove(player);
        };
        player.MediaOpened += mediaOpened;
        player.Open(new Uri(soundPath));
    }
}