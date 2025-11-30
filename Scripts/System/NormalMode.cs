using System;

public sealed class NormalMode : IPlayMode
{
    public event Action OnEnterEvent = null;
    public event Action OnExitEvent = null;

    private bool _isActive = false;
    public bool IsActive => _isActive;

    public void OnEnter(IPlayMode prev)
    {
        _isActive = true;
        OnEnterEvent?.Invoke();
    }

    public void OnExit(IPlayMode next)
    {
        _isActive = false;
        OnExitEvent?.Invoke();
    }
}