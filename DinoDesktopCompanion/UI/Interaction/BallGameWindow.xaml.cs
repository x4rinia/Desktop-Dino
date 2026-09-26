using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DinoDesktopCompanion.UI.Interaction;

public partial class BallGameWindow : Window
{
    private int _score = 0;
    private readonly DispatcherTimer _timer = new();
    private readonly Random _random = new();

    public event EventHandler? GameWon;

    public BallGameWindow()
    {
        InitializeComponent();
        _timer.Interval = TimeSpan.FromMilliseconds(1200);
        _timer.Tick += MoveBall;
        Loaded += (_, _) => MoveBall(this, EventArgs.Empty);
        _timer.Start();
    }

    private void MoveBall(object? sender, EventArgs e)
    {
        var maxX = GameCanvas.ActualWidth - Ball.ActualWidth;
        var maxY = GameCanvas.ActualHeight - Ball.ActualHeight;
        
        if (maxX <= 0 || maxY <= 0) return;

        Canvas.SetLeft(Ball, _random.NextDouble() * maxX);
        Canvas.SetTop(Ball, _random.NextDouble() * maxY);
    }

    private void Ball_Click(object sender, MouseButtonEventArgs e)
    {
        _score++;
        if (_score >= 5)
        {
            _timer.Stop();
            GameWon?.Invoke(this, EventArgs.Empty);
            Close();
        }
        else
        {
            ScoreText.Text = $"Klicke den Ball noch {5 - _score} Mal!";
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(500, 1200 - _score * 100));
            MoveBall(this, EventArgs.Empty);
        }
    }
}
