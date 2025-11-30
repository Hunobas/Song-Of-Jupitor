using KinematicCharacterController.Examples;
using Sirenix.OdinInspector;
using UnityEngine;

public enum PlayModeType : byte
{
    None        = 0,
    PauseMode   = 1,
    CinemaMode  = 2,
    DialogMode  = 3,
    PanelMode   = 4,
    NormalMode  = 5
}

public class GameState : Singleton<GameState>
{
    [SerializeField] public InputManager InputManager;
    [SerializeField] public PlayerControlSystem PC;
    [SerializeField] public OptionMenu OptionMenu;
    [SerializeField] public ScreenDirector ScreenDirector;
    [SerializeField] public StaminaBarUI StaminaBarUI;
    [SerializeField] public PlayerTabUI PlayerTabUI;

    private PauseMode  _pauseMode;
    private CinemaMode _cinemaMode;
    private DialogMode _dialogMode;
    private PanelMode  _panelMode;
    private NormalMode _normalMode;

    public PauseMode PauseMode  => _pauseMode  ??= new PauseMode(this);
    public CinemaMode CinemaMode => _cinemaMode ??= new CinemaMode(this);
    public DialogMode DialogMode => _dialogMode ??= new DialogMode(this);
    public PanelMode  PanelMode  => _panelMode  ??= new PanelMode(this);
    public NormalMode NormalMode => _normalMode ??= new NormalMode();

    public IPlayMode ActiveMode => _activeMode;
    public bool CanPause = true;

    private IPlayMode _activeMode;

    [Title("Debug")]
    [ShowInInspector] public bool IsPaused         => ReferenceEquals(_activeMode, PauseMode);
    [ShowInInspector] public bool IsPlayingCinema  => ReferenceEquals(_activeMode, CinemaMode);
    [ShowInInspector] public bool IsPlayingDialog  => ReferenceEquals(_activeMode, DialogMode) || IsPlayingCinema;
    [ShowInInspector] public bool IsOperatingPanel => ReferenceEquals(_activeMode, PanelMode);
    [ShowInInspector] public bool IsNormal         => ReferenceEquals(_activeMode, NormalMode);

    protected override void Awake()
    {
        base.Awake();
        _pauseMode  ??= new PauseMode(this);
        _cinemaMode ??= new CinemaMode(this);
        _dialogMode ??= new DialogMode(this);
        _panelMode  ??= new PanelMode(this);
        _normalMode ??= new NormalMode();

        _activeMode = _normalMode;
    }

    public void ChangePlayMode(IPlayMode next)
    {
        if (next == null || ReferenceEquals(_activeMode, next))
            return;
        
        // 시네마 모드는 특별하게, 다른 모드의 방해를 받지 않아야 하므로 자신이 시네마 모드를 끝내기 전까지 모드 변경 무시. 
        if (IsPlayingCinema)
            return;
        
        // 패널 모드 한번 더 체크 (디버깅 시 패널 모드 자동 종료)
        if (IsOperatingPanel && PanelMode.Controller != null)
        {
            PanelMode.Controller.EndPanelForcely();
        }

        var prev = _activeMode;
        prev?.OnExit(next);
        _activeMode = next;
        _activeMode.OnEnter(prev);

        InputManager.Instance?.UpdateCursorLock();

        // ConsoleLogger.Log($"PlayMode Changed: {prev} -> {next}");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void ChangePlayMode(int next)
    {
        var nextType = (PlayModeType)next;

        if (!System.Enum.IsDefined(typeof(PlayModeType), nextType) || nextType == PlayModeType.None)
        {
            Debug.LogWarning($"[GameState] Invalid play mode int: {next}");
            return;
        }

        ChangePlayMode(GetPlayModeBy(nextType));
    }

    public void Pause()
    {
        if (!CanPause) return;
        if (PopupManager.Instance.Count > 0) return; //팝업이 모두 정리되어야만, 진입
        ChangePlayMode(PauseMode);
    }

    public void Resume()
    {
        if (IsPaused) _pauseMode.Resume();
    }

    // 일단은 시네마모드만 사용.
    public void SetActiveMode(IPlayMode mode)
    {
        _activeMode = mode;
    }

    public IPlayMode GetPlayModeBy(PlayModeType playMode)
    {
        return playMode switch
        {
            PlayModeType.PauseMode  => PauseMode,
            PlayModeType.CinemaMode => CinemaMode,
            PlayModeType.DialogMode => DialogMode,
            PlayModeType.PanelMode  => PanelMode,
            PlayModeType.NormalMode => NormalMode,
            _                       => null
        };
    }
}
