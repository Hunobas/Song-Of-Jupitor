using System;

public sealed class CinemaMode : IPlayMode
{
    readonly GameState _owner;
    IPlayMode _prevMode;
    
    public event Action OnEnterEvent = null;
    public event Action OnExitEvent = null;
    
    public CinemaMode(GameState owner)
    {
        _owner = owner;
    }
    
    public void OnEnter(IPlayMode prev)
    {
        OnEnterEvent?.Invoke();
        _prevMode = prev;
        _owner.PC.FreezePlayerControl();
    }

    public void OnExit(IPlayMode next)
    {
        OnExitEvent?.Invoke();
        _owner.PC.UnFreezePlayerControl();
    }
    
    // 시네마 모드는 특별하게, 다른 모드의 방해를 받지 않아야 하므로 자신이 주도해서 모드를 끝내도록 한다.
    public void ExitCinemaMode(IPlayMode next = null)
    {
        if (next is CinemaMode) 
            return;

        IPlayMode nextMode = next ?? _prevMode;
        OnExit(nextMode);
        _owner.SetActiveMode(nextMode);
        nextMode.OnEnter(this);

        InputManager.Instance?.UpdateCursorLock();

        ConsoleLogger.Log($"PlayMode Changed: {this} -> {nextMode}");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(_owner);
#endif
    }
}