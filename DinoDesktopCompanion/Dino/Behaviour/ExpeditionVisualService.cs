using System.Windows.Threading;
using DinoDesktopCompanion.Configuration;
using DinoDesktopCompanion.Dino.States;
using DinoDesktopCompanion.Expeditions;

namespace DinoDesktopCompanion.Dino.Behaviour;

public sealed class ExpeditionVisualService
{
    private readonly AppConfig _config;
    private readonly DinoStateMachine _states;
    private readonly ExpeditionService _expeditions;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Random _random = new();

    public event Action<bool>? ExpeditionWalkRequested;
    public event Action<DinoState, int>? TransientStateRequested;
    public event Action? DigRequested;
    public event Action? ItemHintRequested;

    public ExpeditionVisualService(AppConfig config, DinoStateMachine states, ExpeditionService expeditions)
    {
        _config = config;
        _states = states;
        _expeditions = expeditions;
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Tick()
    {
        if (_states.Current == DinoState.Home || _states.IsSleeping) return;
        
        var runningExp = _expeditions.Current.Expeditions.FirstOrDefault(e => e.IsRunning);
        if (runningExp == null) return;
        
        // 10% chance to trigger a visual phase every 30 seconds -> roughly every 5 mins
        if (_random.Next(100) < 15)
        {
            TriggerPhase(runningExp);
        }
    }

    private void TriggerPhase(Expedition exp)
    {
        // A phase is a sequence of actions. We will just fire an event to MainWindow
        // which will orchestrate a coroutine-like task.
        var type = _random.Next(3);
        switch (type)
        {
            case 0:
                // Just walking around
                ExpeditionWalkRequested?.Invoke(true); // true means long walk
                break;
            case 1:
                // Sniffing
                TransientStateRequested?.Invoke(DinoState.Curious, 2500);
                break;
            case 2:
                // Digging and finding something
                DigRequested?.Invoke();
                Task.Delay(1500).ContinueWith(_ => ItemHintRequested?.Invoke());
                break;
        }
    }
}
