using System;
using UnityEngine;

public sealed class PanelMode : IPlayMode
{
    private readonly GameState _owner;
    public event Action OnEnterEvent = null;
    public event Action OnExitEvent = null;

    public PanelMode(GameState owner) => _owner = owner;

    public PanelBase Controller;

    public void OnEnter(IPlayMode prev)
    {
        if (Controller == null)
            return;
        
        OnEnterEvent?.Invoke();
        _owner.PC.FreezePlayerControl();
        _owner.ScreenDirector.FadeOutIcons();
        _owner.StaminaBarUI.CanRun = false;
        _owner.PlayerTabUI?.ClosePlayerReportUI();
        _owner.InputManager.BindToPanel();

        if (prev == _owner.CinemaMode)
        {
            Controller.EnterPanelCore();
        }
    }

    public void OnEnterPanelRoutineComplete(Transform endPos)
    {
        _owner.PC.SetPlayerPosition(endPos);
        _owner.PC.SetPlayerCameraRotation(endPos.rotation);
        _owner.PC.LockCameraMouseInput();
    }

    public void OnExit(IPlayMode next)
    {
        if (Controller == null)
            return;
        
        OnExitEvent?.Invoke();
        _owner.ScreenDirector.FadeInIcons();
        _owner.StaminaBarUI.CanRun = true;
        _owner.InputManager.BindToWorld();
        
        if (next == _owner.CinemaMode)
        {
            Controller.EnterPanelCore();
        }
    }
    
    public void OnExitPanelRoutineComplete()
    {
        _owner.PC.UnFreezePlayerControl();
    }
}