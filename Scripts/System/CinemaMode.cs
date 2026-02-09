using System;

public sealed class CinemaMode : IPlayMode
{
    readonly GameState _owner;
    IPlayMode _nextMode;
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
        _owner.PC.FreezePlayerControl();
        _owner.StaminaBarUI.enabled = false;
        
        if (!ReferenceEquals(prev, _owner.PauseMode))
        {
            _prevMode = prev; // PauseMode에서 Resume으로 돌아온 경우 _prevMode 유지
            _owner.LetterboxController.StartLetterbox(1f);
        }
    }

    public void OnExit(IPlayMode next)
    {
        OnExitEvent?.Invoke();
        _owner.PC.UnFreezePlayerControl();
        _owner.StaminaBarUI.enabled = true;
        _nextMode = null;

        // 끝내는 타이밍이 컷씬마다 다를 수 있음.
        // _owner.LetterboxController.EndLetterbox(1f);
    }
    
    // 시네마 모드는 특별하게, 다른 모드의 방해를 받지 않아야 하므로 자신이 주도해서 모드를 끝내도록 한다.
    public void ExitCinemaMode()
    {
        IPlayMode next = _nextMode ?? _prevMode;
        _owner.ChangePlayMode(next, true);
    }
    
    public void SetNextMode(IPlayMode next) => _nextMode = next;
}