using UnityEngine;

public enum InputState
{
    World,
    UI,
    Panel
}

//temp code
public class InputManager : Singleton<InputManager>
{
    [SerializeField]
    InputEventAsset _world;
    [SerializeField]
    InputEventAsset _ui;
    
    [SerializeField]
    InputEventAsset _panel;

    [SerializeField] bool _isChapterLobbyInput;

    InputEventAsset _current;
    public InputEventAsset Current => _current;
    public InputState CurrentState = InputState.World;

    bool _cursorLocked = true;

    private bool _ignoreMouseInput = false;
    public bool IsBindToWorld => _current == _world;
    public bool IsBindToPanel => _current == _panel;
    
    //Cashes
    public InputState PreviousState = InputState.World;
    
    private void Awake()
    {
        BindToWorld();
        UpdateCursorLock();
    }

    void OnEnable()
    {
        if (_isChapterLobbyInput && _current != null)
        {
            _current.OnESCKeyDown -= GameState.Instance.Pause;
            _current.OnESCKeyDown += GameState.Instance.Pause;
        }
    }

    public void IgnoreMouseInput()
    {
        _ignoreMouseInput = true;
    }

    public void OnMouseInput()
    {
        _ignoreMouseInput = false;
    }
    
    public void BindToWorld()
    {
        PreviousState = CurrentState;
        
        _current?.OnUnbind();
        _current = _world;
        CurrentState = InputState.World;
    }
    public void BindToUI()
    {
        PreviousState = CurrentState;
        
        _current?.OnUnbind();
        _current = _ui;
        CurrentState = InputState.UI;
    }
    public void BindToPanel()
    {
        PreviousState = CurrentState;
        
        _current?.OnUnbind();
        _current = _panel;
        CurrentState = InputState.Panel;
    }

    /// <summary>
    /// 이전에 장착되있던 인풋으로 되돌림
    /// </summary>
    public void BindPreviousInput()
    {
        switch (PreviousState)
        {
            case InputState.World:
                BindToWorld();
                break;
            case InputState.Panel:
                BindToPanel();
                break;
            case InputState.UI:
                BindToUI();
                break;
        }
    }

    private void OnDestroy()
    {
        ClearInputEventsAsset();
    }

    private void Update()
    {
        _current.OnBeforeInputUpdate?.Invoke();

        // mouse
        if (Input.GetMouseButtonDown(0))
        {
            _current.OnClickLeftDown?.Invoke();
            _current.OnMouseClick?.Invoke(true);
        }
        if(Input.GetMouseButton(0))
            _current.OnClickLeftStay?.Invoke();
        if(Input.GetMouseButtonUp(0))
            _current.OnClickLeftUp?.Invoke();

        if (Input.GetMouseButtonDown(1))
        {
            _current.OnClickRightDown?.Invoke();
            _current.OnMouseClick?.Invoke(false);
        }
            
        if (Input.GetMouseButton(1))
            _current.OnClickRightStay?.Invoke();
        if (Input.GetMouseButtonUp(1))
            _current.OnClickRightUp?.Invoke();


        // key
        if (Input.GetKeyDown(KeyCode.Escape))
            _current.OnESCKeyDown?.Invoke();
        if(Input.GetKeyUp(KeyCode.Escape))
            _current.OnESCKeyUp?.Invoke();
        
        if (GameState.Instance.IsPaused)
            return;
        
        if(Input.GetKeyDown(KeyCode.LeftArrow))
            _current.OnLeftKeyDown?.Invoke();
        if(Input.GetKeyDown(KeyCode.RightArrow))
            _current.OnRightKeyDown?.Invoke();
        if(Input.GetKeyDown(KeyCode.UpArrow))
            _current.OnUpKeyDown?.Invoke();
        if(Input.GetKeyDown(KeyCode.DownArrow))
            _current.OnDownKeyDown?.Invoke();
        
        if(Input.GetKeyDown(KeyCode.W))
            _current.OnWKeyDown?.Invoke();
        if(Input.GetKeyDown(KeyCode.S))
            _current.OnSKeyDown?.Invoke();
        if(Input.GetKeyDown(KeyCode.A))
            _current.OnAKeyDown?.Invoke();
        if(Input.GetKeyDown(KeyCode.D))
            _current.OnDKeyDown?.Invoke();

        if (Input.GetKeyDown(KeyCode.Return))
            _current.OnEnterKeyDown?.Invoke();
        if(Input.GetKeyUp(KeyCode.Return))
            _current.OnEnterKeyUp?.Invoke();

        if (Input.GetKeyDown(KeyCode.Tab))
            _current.OnTabKeyDown?.Invoke();
        if (Input.GetKeyUp(KeyCode.Tab))
            _current.OnTabKeyUp?.Invoke();

        if(Input.GetKeyDown(KeyCode.Space)) 
            _current.OnSpaceKeyDown?.Invoke();
        if(Input.GetKeyUp(KeyCode.Space))
            _current.OnSpaceKeyUp?.Invoke();

        if (Input.GetKeyDown(KeyCode.E))
            _current.OnEKeyDown?.Invoke();
        if (Input.GetKeyUp(KeyCode.E))
            _current.OnEKeyUp?.Invoke();

        if (Input.GetKeyDown(KeyCode.Q))
            _current.OnQKeyDown?.Invoke();
        if (Input.GetKeyUp(KeyCode.Q))
            _current.OnQKeyUp?.Invoke();

        if (Input.GetKeyDown(KeyCode.LeftShift))
            _current.OnLShiftKeyDown?.Invoke();
        if (Input.GetKeyUp(KeyCode.LeftShift))
            _current.OnLShiftKeyUp?.Invoke();


#if UNITY_EDITOR || DEBUGGER
        // editor
        if(Input.GetKeyDown(KeyCode.F1))
            _current.OnF1KeyDown?.Invoke();
#endif

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        _current.OnDirectionAxis?.Invoke(horizontal, vertical);
        Vector2 scroll = Input.mouseScrollDelta;
        if(scroll != Vector2.zero)
            _current.OnMouseScroll?.Invoke(scroll.x,scroll.y);

        _current.OnAfterInputUpdate?.Invoke();
    }

    private void LateUpdate()
    {
        if (GameState.Instance.IsPaused)
            return;
        
        if (_cursorLocked)
        {
            _current.OnBeforeLateInputUpdate?.Invoke();
            float mx;
            float my;
            
            if (!_ignoreMouseInput)
            {
                 mx = Input.GetAxisRaw("Mouse X");
                 my = Input.GetAxisRaw("Mouse Y");
            }
            else
            {
                mx = 0;
                my = 0;
            }
            _current.OnMouseAxis?.Invoke(mx, my);
            _current.OnAfterLateInputUpdate?.Invoke();
        }
    }
    
    void OnApplicationFocus(bool focus)
    {
        UpdateCursorLock();
    }

    public void UpdateCursorLock()
    {
        bool focused = Application.isFocused;
        bool isPlaying = !_isChapterLobbyInput && !GameState.Instance.IsPaused && focused;
        Cursor.lockState = isPlaying ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !isPlaying;
    }

    private void ClearInputEventsAsset()
    {
        _world.Clear();
        _ui.Clear();
        _panel.Clear();
    }
}
