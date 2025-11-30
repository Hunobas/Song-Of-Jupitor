using System;

public sealed class DialogMode : IPlayMode
{
    readonly GameState _owner;
    public event Action OnEnterEvent = null;
    public event Action OnExitEvent = null;
    
    public DialogMode(GameState owner)
    {
        _owner = owner;
    }

    public void OnEnter(IPlayMode prev)
    {
        OnEnterEvent?.Invoke();
    }

    public void OnExit(IPlayMode next)
    {
        OnExitEvent?.Invoke();
    }
}
