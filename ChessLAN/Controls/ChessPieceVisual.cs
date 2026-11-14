using System.Windows;
using System.Windows.Media;

namespace ChessLAN.Controls;

/// <summary>Original shaded Staunton-style chess piece drawn with scalable WPF vector geometry.</summary>
public sealed class ChessPieceVisual : FrameworkElement
{
    public char FenPiece { get; set; }
    public ChessPieceVisual(char fenPiece) => FenPiece = fenPiece;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.PushTransform(new ScaleTransform(ActualWidth / 100, ActualHeight / 100));

        var white = char.IsUpper(FenPiece);
        var kind = char.ToLowerInvariant(FenPiece);
        var shape = CreateGeometry(FenPiece);
        var fill = white
            ? new LinearGradientBrush(Color.FromRgb(255,250,229), Color.FromRgb(179,157,117), 70)
            : new LinearGradientBrush(Color.FromRgb(111,108,99), Color.FromRgb(24,24,22), 70);
        var outline = new Pen(new SolidColorBrush(white ? Color.FromRgb(70,55,37) : Color.FromRgb(8,9,9)), 2.2);
        outline.LineJoin = PenLineJoin.Round;
        dc.DrawGeometry(fill, outline, shape);

        var highlight = new Pen(new SolidColorBrush(white ? Color.FromArgb(190,255,255,239) : Color.FromArgb(125,211,206,183)), 2.1);
        var detail = new Pen(new SolidColorBrush(white ? Color.FromArgb(200,91,69,43) : Color.FromArgb(205,10,11,10)), 2);
        DrawDetails(dc, kind, highlight, detail, white);
        dc.Pop();
    }

    public static Geometry CreateGeometry(char piece) => Geometry.Parse(ShapeFor(char.ToLowerInvariant(piece)));

    private static string ShapeFor(char kind) => kind switch
    {
        'p' => "M20,88 Q22,82 34,80 L40,76 L38,70 Q31,66 33,59 Q34,54 41,51 C31,43 32,30 40,23 C47,16 60,17 67,25 C75,34 71,46 62,52 Q70,57 68,65 Q67,70 61,75 L66,80 Q79,82 82,88 L82,93 L18,93 Z",
        'r' => "M17,18 L34,18 L34,29 L43,29 L43,18 L57,18 L57,29 L66,29 L66,18 L83,18 L83,38 L76,41 L74,76 L84,82 L86,92 L14,92 L16,82 L26,76 L24,41 L17,38 Z",
        'n' => "M17,91 Q19,83 31,79 L36,70 Q31,62 34,52 Q37,43 48,35 L40,30 L45,19 Q54,27 64,28 Q76,31 80,43 L76,48 L70,46 L69,56 Q68,68 76,78 Q84,82 86,91 Z",
        'b' => "M18,90 Q20,83 34,80 L40,75 Q35,68 38,61 Q41,55 47,51 Q35,43 36,32 Q37,20 49,17 Q48,10 54,7 Q61,7 60,17 Q72,21 72,33 Q72,43 60,51 Q69,58 68,67 Q67,73 62,77 L69,81 Q81,83 83,90 L83,94 L17,94 Z",
        'q' => "M15,91 L19,82 L29,79 L25,48 L40,62 L43,38 L54,58 L64,37 L67,61 L82,47 L76,79 L84,83 L87,91 Z M24,34 A5,5 0 1 0 34,34 A5,5 0 1 0 24,34 M39,27 A5,5 0 1 0 49,27 A5,5 0 1 0 39,27 M57,26 A5,5 0 1 0 67,26 A5,5 0 1 0 57,26 M70,35 A5,5 0 1 0 80,35 A5,5 0 1 0 70,35",
        _ => "M17,91 L20,82 L34,79 L40,73 Q35,67 39,60 Q42,55 49,51 L49,33 L39,33 L39,26 L49,26 L49,13 L57,13 L57,26 L67,26 L67,33 L57,33 L57,51 Q68,56 69,64 Q70,70 63,76 L70,80 L82,82 L85,91 Z"
    };

    private static void DrawDetails(DrawingContext dc, char kind, Pen light, Pen dark, bool white)
    {
        // A fine highlight and a darker bevel make the silhouettes read like carved, polished wood.
        switch (kind)
        {
            case 'p':
                Stroke(dc, "M39,73 Q51,78 64,73 M27,85 Q50,89 74,85 M43,27 Q51,21 60,27", light);
                Stroke(dc, "M22,91 L78,91 M41,55 Q51,60 62,55", dark); break;
            case 'r':
                Stroke(dc, "M19,35 L81,35 M29,43 L71,43 M29,50 L71,50 M28,72 Q50,76 72,72 M20,87 L80,87", light);
                Stroke(dc, "M40,42 L40,69 M60,42 L60,69 M25,79 L75,79", dark); break;
            case 'n':
                Stroke(dc, "M39,36 Q53,42 66,39 M42,48 Q48,55 61,56 M32,82 Q52,87 75,82 M26,89 L78,89", light);
                dc.DrawEllipse(new SolidColorBrush(white ? Color.FromRgb(37,31,24) : Color.FromRgb(224,216,190)), null, new Point(63,39), 2.2, 2.2); break;
            case 'b':
                Stroke(dc, "M42,34 Q51,39 65,33 M44,56 Q52,61 64,56 M29,85 Q50,89 73,85 M24,91 L77,91", light);
                Stroke(dc, "M48,25 L57,25 M39,68 Q52,73 67,67", dark); break;
            case 'q':
                Stroke(dc, "M22,84 Q51,89 80,84 M28,74 L73,74 M35,62 L43,62 M54,60 L63,60", light);
                Stroke(dc, "M27,79 L73,79", dark); break;
            default:
                Stroke(dc, "M44,30 L62,30 M43,57 Q52,61 64,57 M27,84 Q51,89 75,84 M23,91 L78,91", light);
                Stroke(dc, "M51,16 L55,16 M40,67 Q52,72 66,67", dark); break;
        }
    }

    private static void Stroke(DrawingContext dc, string path, Pen pen) => dc.DrawGeometry(null, pen, Geometry.Parse(path));
}
