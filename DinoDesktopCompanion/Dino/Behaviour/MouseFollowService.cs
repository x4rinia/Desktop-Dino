using System.Windows.Threading;
using DinoDesktopCompanion.Configuration;
using Forms = System.Windows.Forms;

namespace DinoDesktopCompanion.Dino.Behaviour;

public sealed class MouseFollowService : IDisposable
{
    private readonly AppConfig _config;
    private readonly Func<System.Drawing.Point> _companionCenter;
    private readonly Func<bool> _canFollow;
    private readonly DispatcherTimer _timer = new();
    private System.Drawing.Point _lastCursor;
    private int _stableSamples;
    public event EventHandler<MouseFollowStepEventArgs>? StepRequested;

    public MouseFollowService(AppConfig config, Func<System.Drawing.Point> companionCenter, Func<bool> canFollow)
    {
        _config = config; _companionCenter = companionCenter; _canFollow = canFollow;
        _timer.Tick += (_, _) => Tick();
    }

    public void ApplyMode()
    {
        _timer.Stop(); _stableSamples = 0; _lastCursor = Forms.Control.MousePosition;
        _timer.Interval = _config.MouseFollow switch
        {
            "Schwach" => TimeSpan.FromMilliseconds(1400),
            "Normal" => TimeSpan.FromMilliseconds(1100),
            "Neugierig" => TimeSpan.FromMilliseconds(850),
            _ => TimeSpan.Zero
        };
        if (_timer.Interval > TimeSpan.Zero) _timer.Start();
    }

    private void Tick()
    {
        if (!_canFollow()) { _stableSamples = 0; return; }
        var cursor = Forms.Control.MousePosition;
        var cursorTravel = Distance(cursor, _lastCursor);
        _lastCursor = cursor;
        if (cursorTravel > 42) { _stableSamples = 0; return; }
        if (++_stableSamples < 2) return;

        var dino = _companionCenter();
        if (Forms.Screen.FromPoint(cursor).DeviceName != Forms.Screen.FromPoint(dino).DeviceName) return;
        var dx = cursor.X - dino.X; var dy = cursor.Y - dino.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var (minimumDistance, step) = _config.MouseFollow switch
        {
            "Schwach" => (390d, 18d),
            "Normal" => (300d, 25d),
            "Neugierig" => (220d, 34d),
            _ => (double.MaxValue, 0d)
        };
        if (distance <= minimumDistance || step <= 0) return;
        StepRequested?.Invoke(this, new MouseFollowStepEventArgs(dx / distance * step, dy / distance * step));
    }

    private static double Distance(System.Drawing.Point a, System.Drawing.Point b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose() => _timer.Stop();
}

public sealed class MouseFollowStepEventArgs : EventArgs
{
    public double DeltaX { get; } public double DeltaY { get; }
    public MouseFollowStepEventArgs(double deltaX, double deltaY) { DeltaX = deltaX; DeltaY = deltaY; }
}
