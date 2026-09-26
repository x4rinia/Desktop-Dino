using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DinoDesktopCompanion.UI.Interaction;

public partial class FeedGameWindow : Window
{
    private int _caught = 0;
    private readonly DispatcherTimer _gameTimer = new();
    private readonly DispatcherTimer _spawnTimer = new();
    private readonly Random _random = new();
    private readonly List<Ellipse> _apples = new();

    public event EventHandler? GameWon;

    public FeedGameWindow()
    {
        InitializeComponent();
        
        _gameTimer.Interval = TimeSpan.FromMilliseconds(25);
        _gameTimer.Tick += GameLoop;
        
        _spawnTimer.Interval = TimeSpan.FromMilliseconds(900);
        _spawnTimer.Tick += SpawnApple;

        Loaded += (_, _) => { _gameTimer.Start(); _spawnTimer.Start(); };
        Closing += (_, _) => { _gameTimer.Stop(); _spawnTimer.Stop(); };
    }

    private void SpawnApple(object? sender, EventArgs e)
    {
        var apple = new Ellipse { Width = 25, Height = 25, Fill = System.Windows.Media.Brushes.Red };
        Canvas.SetLeft(apple, _random.NextDouble() * (GameCanvas.ActualWidth - 25));
        Canvas.SetTop(apple, 0);
        _apples.Add(apple);
        GameCanvas.Children.Add(apple);
    }

    private void GameLoop(object? sender, EventArgs e)
    {
        var toRemove = new List<Ellipse>();
        var mouthRect = new Rect(Canvas.GetLeft(Mouth), GameCanvas.ActualHeight - Mouth.Height - 10, Mouth.Width, Mouth.Height);

        foreach (var apple in _apples)
        {
            var top = Canvas.GetTop(apple) + 5;
            Canvas.SetTop(apple, top);
            
            var appleRect = new Rect(Canvas.GetLeft(apple), top, apple.Width, apple.Height);
            
            if (appleRect.IntersectsWith(mouthRect))
            {
                toRemove.Add(apple);
                _caught++;
                InstructionText.Text = $"Gefangen: {_caught} / 3";
                
                if (_caught >= 3)
                {
                    _gameTimer.Stop();
                    _spawnTimer.Stop();
                    GameWon?.Invoke(this, EventArgs.Empty);
                    Close();
                    return;
                }
            }
            else if (top > GameCanvas.ActualHeight)
            {
                toRemove.Add(apple);
            }
        }

        foreach (var r in toRemove)
        {
            _apples.Remove(r);
            GameCanvas.Children.Remove(r);
        }
    }

    private void GameCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var pos = e.GetPosition(GameCanvas);
        var left = pos.X - Mouth.Width / 2;
        if (left < 0) left = 0;
        if (left > GameCanvas.ActualWidth - Mouth.Width) left = GameCanvas.ActualWidth - Mouth.Width;
        
        Canvas.SetLeft(Mouth, left);
    }
}
