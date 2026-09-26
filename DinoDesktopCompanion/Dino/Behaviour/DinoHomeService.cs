using DinoDesktopCompanion.Dino.States;

namespace DinoDesktopCompanion.Dino.Behaviour;

/// <summary>Kapselt den Home-Lebenszyklus unabhängig von der konkreten Fensterdarstellung.</summary>
public sealed class DinoHomeService
{
    private readonly DinoStateMachine _states;
    public bool IsHome => _states.Current == DinoState.Home;

    public event EventHandler? GoingHome;
    public event EventHandler? ArrivedHome;
    public event EventHandler? ReturningHome;
    public event EventHandler? ReturnedHome;

    public DinoHomeService(DinoStateMachine states) => _states = states;

    public void SendHome(Action savePosition, Action hideDesktopDino)
    {
        if (IsHome || _states.IsSleeping) return;
        GoingHome?.Invoke(this, EventArgs.Empty);
        savePosition();
        _states.Set(DinoState.Home);
        hideDesktopDino();
        ArrivedHome?.Invoke(this, EventArgs.Empty);
    }

    public void CallDino(Action showNearCursor)
    {
        ReturningHome?.Invoke(this, EventArgs.Empty);
        showNearCursor();
        _states.Set(DinoState.Wake);
        ReturnedHome?.Invoke(this, EventArgs.Empty);
    }
}
