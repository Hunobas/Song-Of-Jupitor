using UnityEngine;
using KinematicCharacterController.Examples;
using Sirenix.OdinInspector;
using Cinemachine;

public enum PlayModeType : byte
{
    None        = 0,
    PauseMode   = 1,
    CinemaMode  = 2,
    PanelMode   = 3,
    NormalMode  = 4
}

public class GameState : Singleton<GameState>
{
    [SerializeField] public InputManager InputManager;
    [SerializeField] public PlayerControlSystem PC;
    [SerializeField] public CinemachineBrain Cinema;
    [SerializeField] public OptionMenu OptionMenu;
    [SerializeField] public ScreenDirector ScreenDirector;
    [SerializeField] public StaminaBarUI StaminaBarUI;
    [SerializeField] public PlayerTabUI PlayerTabUI;
    [SerializeField] public LetterboxController LetterboxController;
    [SerializeField] public PlayerGrabController PlayerGrabController;

    private PauseMode  _pauseMode;
    private CinemaMode _cinemaMode;
    private PanelMode  _panelMode;
    private NormalMode _normalMode;

    public PauseMode PauseMode  => _pauseMode  ??= new PauseMode(this);
    public CinemaMode CinemaMode => _cinemaMode ??= new CinemaMode(this);
    public PanelMode  PanelMode  => _panelMode  ??= new PanelMode(this);
    public NormalMode NormalMode => _normalMode ??= new NormalMode(this);

    public IPlayMode ActiveMode => _activeMode;
    public bool CanPause = true;

    private IPlayMode _activeMode;

    [Title("Debug")]
    [ShowInInspector] public bool IsPaused         => ReferenceEquals(_activeMode, PauseMode);
    [ShowInInspector] public bool IsPlayingCinema  => ReferenceEquals(_activeMode, CinemaMode);
    [ShowInInspector] public bool IsOperatingPanel => ReferenceEquals(_activeMode, PanelMode);
    [ShowInInspector] public bool IsNormal         => ReferenceEquals(_activeMode, NormalMode);
    
    // 다이얼로그 모드 없애고 대신 사용
    [ShowInInspector] public bool IsPlayingDialog;

    protected override void Awake()
    {
        base.Awake();
        _pauseMode  ??= new PauseMode(this);
        _cinemaMode ??= new CinemaMode(this);
        _panelMode  ??= new PanelMode(this);
        _normalMode ??= new NormalMode(this);
        Cinema ??= Camera.main?.GetComponent<CinemachineBrain>();

        _activeMode = _normalMode;
    }

    public void ChangePlayMode(IPlayMode next, bool changeByCinema = false)
    {
        if (next == null || ReferenceEquals(_activeMode, next))
            return;
        
        // 시네마 모드는 특별하게, 일시정지 모드 이외 다른 모드의 방해를 받지 않아야 하므로 자신이 시네마 모드를 끝내기 전까지 모드 변경 무시. 
        if (!changeByCinema && IsPlayingCinema && !ReferenceEquals(next, PauseMode))
            return;
        
        var prev = _activeMode;
        prev?.OnExit(next);
        _activeMode = next;
        _activeMode.OnEnter(prev);

        InputManager.Instance?.UpdateCursorLock();
        CanPause = !IsOperatingPanel;

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
        if (Cinema.IsBlending) return;
        if (PopupManager.Instance.Count > 0) return; //팝업이 모두 정리되어야만 진입
        ChangePlayMode(PauseMode);
    }

    public void Resume()
    {
        if (IsPaused) _pauseMode.Resume();
    }

    public IPlayMode GetPlayModeBy(PlayModeType playMode)
    {
        return playMode switch
        {
            PlayModeType.PauseMode  => PauseMode,
            PlayModeType.CinemaMode => CinemaMode,
            PlayModeType.PanelMode  => PanelMode,
            PlayModeType.NormalMode => NormalMode,
            _                       => null
        };
    }

    public void SetNextModeInCinema(int next) => CinemaMode.SetNextMode(GetPlayModeBy((PlayModeType)next));
}
