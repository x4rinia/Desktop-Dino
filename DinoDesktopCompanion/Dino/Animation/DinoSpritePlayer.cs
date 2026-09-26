using System.Windows.Media;
using System.Windows.Threading;
using DinoDesktopCompanion.Dino.States;

namespace DinoDesktopCompanion.Dino.Animation;

/// <summary>Spielt nur während echter Mehrbild-Sequenzen einen sparsamen Dispatcher-Timer ab.</summary>
public sealed class DinoSpritePlayer
{
    private readonly DinoSpriteCatalog _catalog;
    private readonly Action<ImageSource?> _setFrame;
    private readonly DispatcherTimer _timer = new();
    private IReadOnlyList<ImageSource> _frames = [];
    private int _index;
    private bool _loop;

    public DinoSpritePlayer(DinoSpriteCatalog catalog, Action<ImageSource?> setFrame)
    {
        _catalog = catalog; _setFrame = setFrame;
        _timer.Tick += (_, _) => Advance();
    }

    public void Play(DinoState state, bool animationsEnabled, double speed)
    {
        _timer.Stop(); _index = 0;
        var sequence = _catalog.Get(state);
        _frames = sequence.Frames; _loop = sequence.Loop;
        _setFrame(_frames.Count > 0 ? _frames[0] : null);
        if (!animationsEnabled || _frames.Count < 2) return;
        _timer.Interval = TimeSpan.FromMilliseconds(sequence.FrameDurationMs / Math.Clamp(speed, .5, 2.0));
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void Advance()
    {
        _index++;
        if (_index >= _frames.Count)
        {
            if (!_loop) { _timer.Stop(); _index = _frames.Count - 1; }
            else _index = 0;
        }
        _setFrame(_frames[_index]);
    }
}
