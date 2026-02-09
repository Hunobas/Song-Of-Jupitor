using System;

public sealed class NormalMode : IPlayMode
{
    private readonly GameState _owner;
    public event Action OnEnterEvent = null;
    public event Action OnExitEvent = null;
    public NormalMode(GameState owner) => _owner = owner;

    private bool _isActive = false;
    public bool IsActive => _isActive;
    

    public void OnEnter(IPlayMode prev)
    {
        _isActive = true;
        _owner.ScreenDirector.ShowCrosshair(true);
        OnEnterEvent?.Invoke();
    }

    public void OnExit(IPlayMode next)
    {
        _isActive = false;
        _owner.ScreenDirector.HideCrosshair();
        OnExitEvent?.Invoke();
    }
}