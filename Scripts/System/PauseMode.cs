using System;
using UnityEngine;

public sealed class PauseMode : IPlayMode
{
    readonly GameState _owner;
    IPlayMode _prevMode;

    public event Action OnEnterEvent = null;
    public event Action OnExitEvent = null;

    public PauseMode(GameState owner)
    {
        _owner = owner;
    }

    public void OnEnter(IPlayMode prev)
    {
        OnEnterEvent?.Invoke();
        _prevMode = prev;
        Time.timeScale = 0f;
        SoundManager.Instance.PauseAll();
        
        _owner.OptionMenu.OpenPauseMenu();
    }

    public void OnExit(IPlayMode next)
    {
        OnExitEvent?.Invoke();
        Time.timeScale = 1f;
        SoundManager.Instance.ResumeAll();
    }

    public void Resume()
    {
        PopupManager.Instance.AllClose();
        _owner.ChangePlayMode(_prevMode ?? _owner.NormalMode);
    }
}