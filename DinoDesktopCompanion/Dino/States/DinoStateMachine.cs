namespace DinoDesktopCompanion.Dino.States;
public sealed class DinoStateMachine
{
    public DinoState Current { get; private set; } = DinoState.Idle;
    public bool IsSleeping => IsSleepState(Current);
    public event Action<DinoState>? StateChanged;
    public event EventHandler<DinoStateTransitionEventArgs>? Transitioning;
    public event EventHandler<DinoStateTransitionEventArgs>? Transitioned;

    public void Set(DinoState state)
    {
        // A sleeping Dino is protected from generic AI, timer, and animation transitions.
        if (IsSleeping)
        {
            if (state != DinoState.Sleep && state != DinoState.SleepLeft && state != DinoState.SleepRight && state != DinoState.Home)
            {
                return;
            }
        }
        TransitionTo(state);
    }

    public void WakeUp()
    {
        if (!IsSleeping) return;
        TransitionTo(DinoState.Wake);
    }

    public static bool IsSleepState(DinoState state)
        => state is DinoState.Sleep or DinoState.SleepLeft or DinoState.SleepRight;

    private void TransitionTo(DinoState state)
    {
        if (Current == state) return;
        var transition = new DinoStateTransitionEventArgs(Current, state);
        Transitioning?.Invoke(this, transition);
        Current = state;
        StateChanged?.Invoke(state);
        Transitioned?.Invoke(this, transition);
    }
}

public sealed class DinoStateTransitionEventArgs : EventArgs
{
    public DinoState Previous { get; }
    public DinoState Current { get; }
    public DinoStateTransitionEventArgs(DinoState previous, DinoState current) { Previous = previous; Current = current; }
}
