using System.Windows.Threading;
using DinoDesktopCompanion.Configuration;
using DinoDesktopCompanion.Dino.States;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Dino.Behaviour;

public sealed class DinoBehaviourService
{
    private readonly AppConfig _config;
    private readonly DinoStateMachine _states;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly Random _random = new();
    private DateTime _lastMessage = DateTime.MinValue;
    private readonly DinoDesktopCompanion.Progress.ProgressService _progress;
    public event Action? WalkRequested;
    public event Action? RandomMessageRequested;
    public event Action<DinoState, int>? TransientStateRequested;
    public event Action? TurnAroundRequested;
    public event Action? HeartEventRequested;
    public event Action? HuntMouseRequested;
    public event Action? HighFiveRequested;
    public event Action? AreaEventRequested;

    public DinoBehaviourService(AppConfig config, DinoStateMachine states, DinoDesktopCompanion.Progress.ProgressService progress)
    {
        _config = config; _states = states; _progress = progress;
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Tick()
    {
        if (_states.Current == DinoState.Home) return;
        // Sleep remains active until an explicit WakeUp action is requested.
        if (_states.IsSleeping) return;

        var idle = IdleTimeService.GetIdleTime();

        if (idle >= TimeSpan.FromMinutes(Math.Max(1, _config.SleepAfterMinutes)))
        {
            _progress.StartSleeping(DateTimeOffset.Now);
            _states.Set(DinoState.Sleep);
            return;
        }
        if (_config.QuietMode) { _states.Set(DinoState.Idle); return; }

        var cursor = System.Windows.Forms.Control.MousePosition;
        var win = System.Windows.Application.Current.Windows.OfType<DinoDesktopCompanion.MainWindow>().FirstOrDefault();
        if (win != null)
        {
            var centerX = win.Left + win.Width / 2;
            var centerY = win.Top + win.Height / 2;
            var dist = Math.Sqrt(Math.Pow(cursor.X - centerX, 2) + Math.Pow(cursor.Y - centerY, 2));

            if (dist < 200 && _random.Next(100) < 15)
            {
                var reaction = _random.Next(3);
                if (reaction == 0) 
                {
                    TransientStateRequested?.Invoke(DinoState.Curious, 1500);
                    win.ShowSpeech("!", 1500);
                }
                else if (reaction == 1)
                {
                    AreaEventRequested?.Invoke();
                }
                else
                {
                    TransientStateRequested?.Invoke(DinoState.Sniff, 1500);
                }
                return;
            }
        }

        var chance = _config.Activity switch { "Aktiver" or "Lebhaft" => 55, "Normal" => 30, _ => 12 };
        if (_random.Next(100) < chance)
        {
            var action = _random.Next(100);
            if (action < 15 && _config.CanRoam) WalkRequested?.Invoke();
            else if (action < 18) TurnAroundRequested?.Invoke();
            else if (action < 21) HeartEventRequested?.Invoke();
            else if (action < 23) HuntMouseRequested?.Invoke();
            else if (action < 26) HighFiveRequested?.Invoke();
            else if (action < 32) AreaEventRequested?.Invoke();
            else
            {
                var (state, duration) = action switch
                {
                    < 40 => (DinoState.LookAround, 2500),
                    < 50 => (DinoState.Sniff, 2000),
                    < 60 => (DinoState.Sit, 3000),
                    < 70 => (DinoState.Curious, 1900),
                    < 75 => (DinoState.Yawn, 2600),
                    < 80 => (DinoState.Stretch, 2500),
                    < 85 => (DinoState.TailWag, 2000),
                    < 90 => (DinoState.Hop, 1000), // used as "Pfote heben" fallback
                    < 95 => (DinoState.SmallHappy, 1500),
                    _ => (DinoState.LookAround, 2000)
                };
                TransientStateRequested?.Invoke(state, duration);
            }
        }

        var messageInterval = _config.Activity switch
        {
            "Aktiver" or "Lebhaft" => TimeSpan.FromMinutes(1),
            "Normal" => TimeSpan.FromMinutes(Math.Min(4, Math.Max(1, _config.MessageFrequencyMinutes))),
            _ => TimeSpan.FromMinutes(Math.Max(1, _config.MessageFrequencyMinutes))
        };
        if (_config.RandomMessages && _config.SpeechBubbles && DateTime.Now - _lastMessage >= messageInterval)
        {
            _lastMessage = DateTime.Now; RandomMessageRequested?.Invoke();
        }
    }
}
