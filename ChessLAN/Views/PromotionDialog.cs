using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ChessLAN.Controls;

namespace ChessLAN.Views;

public sealed class PromotionDialog : Window
{
    public char? SelectedPiece { get; private set; }

    public PromotionDialog(bool white)
    {
        Title = "Phong cấp tốt";
        Width = 430;
        Height = 188;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;

        var panel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(34,37,43)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(67,73,82)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20)
        };
        var layout = new StackPanel();
        layout.Children.Add(new TextBlock
        {
            Text = "CHỌN QUÂN PHONG CẤP", Foreground = Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(2,0,0,14)
        });
        var choices = new UniformGrid { Rows = 1, Columns = 4 };
        foreach (var (piece, label) in new[] { ('q', "Hậu"), ('r', "Xe"), ('b', "Tượng"), ('n', "Mã") })
        {
            var code = white ? char.ToUpperInvariant(piece) : piece;
            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new ChessPieceVisual(code) { Width = 48, Height = 54, HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(205,210,215)), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,1,0,0) });
            var button = new Button
            {
                Content = content, Background = new SolidColorBrush(Color.FromRgb(47,51,58)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(5),
                Margin = new Thickness(4,0,4,0), Cursor = System.Windows.Input.Cursors.Hand
            };
            button.Click += (_, _) => { SelectedPiece = piece; DialogResult = true; };
            choices.Children.Add(button);
        }
        layout.Children.Add(choices);
        panel.Child = layout;
        Content = panel;
    }
}
