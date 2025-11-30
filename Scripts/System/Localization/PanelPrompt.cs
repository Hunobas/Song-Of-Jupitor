using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public enum PromptInputStyle { PromptInput, UIControl }

public abstract class PanelPrompt : MonoBehaviour
{
    [Header("BaseSetting")] 
    [SerializeField] private PanelBase _Panel;
    protected PanelBase Panel => _Panel;

    [SerializeField] protected TMP_InputField _promptInput;
    protected InputEventAsset _worldInput;

    [Header("Debug")]
    [SerializeField] protected PromptInputStyle _promptInputStyle = PromptInputStyle.PromptInput;

    private bool _clickToFocusOnly = false;
    private bool _inputFocused;

    public bool IsInputFocused => _inputFocused;
    public event Action<bool> OnInputFocusChanged;  // true : Focus, false : None
    public event Action OnInputSubmitted;           

    protected virtual void Awake()
    {
        _worldInput = _Panel.PanelInput;

        var catcher = _promptInput.gameObject.GetComponent<InputClickToFocus>();
        if (catcher == null) catcher = _promptInput.gameObject.AddComponent<InputClickToFocus>();
        catcher.Bind(this);

        _promptInput.onSelect.AddListener(_ =>
        {
            _inputFocused = true;
            OnInputFocusChanged?.Invoke(true);
        });

        _promptInput.onDeselect.AddListener(_ =>
        {
            _inputFocused = false;
            OnInputFocusChanged?.Invoke(false);
        });
    }

    protected virtual void OnEnable()
    {
        SoftResetInput();
        AddShortcuts();
        _promptInput.onSubmit.AddListener(OnPromptSubmit);
    }

    protected virtual void OnDisable()
    {
        _promptInput.onSubmit.RemoveListener(OnPromptSubmit);
        RemoveShortcuts();
    }

    protected void Start()
    {
        _Panel.OperatingPanelEvent    += IfOperatingPanel;
        _Panel.PanelOperationCompleted += IfPanelOperationComplete;
    }

    /// <summary>
    /// 패널 조작 시작
    /// </summary>
    protected virtual void IfOperatingPanel()
    {
        _Panel.IsOperatingPanel = true;
        _promptInput.enabled = true;
        _clickToFocusOnly = false;    
        ActivatePromptInput();
        AddShortcuts();
    }

    /// <summary>
    /// 패널 조작 완료
    /// </summary>
    protected virtual void IfPanelOperationComplete()
    {
        _Panel.IsOperatingPanel = false;
        RemoveShortcuts();
        _clickToFocusOnly = true;     
        DeactivatePromptInput();
    }

    /// <summary>
    /// 입력 활성화
    /// </summary>
    public virtual void ActivatePromptInput()
    {
        if (_promptInputStyle == PromptInputStyle.UIControl) return;
        if (!_Panel.IsOperatingPanel) return;
        if (_clickToFocusOnly) return;

        _promptInput.enabled = true;
        _promptInput.interactable = true;
        _promptInput.Select();
        _promptInput.ActivateInputField();
    }

    /// <summary>
    /// 입력 비활성화
    /// </summary>
    public virtual void DeactivatePromptInput()
    {
        _promptInput.DeactivateInputField();
        EventSystem.current?.SetSelectedGameObject(null);
    }

    protected virtual void AddShortcuts()
    {
    }

    protected virtual void RemoveShortcuts()
    {
    }

    protected virtual void OnPromptSubmit(string inputText)
    {
        OnInputSubmitted?.Invoke();

        HandlePrompt(inputText);
        ClearPromptInputText();

        _clickToFocusOnly = true; 
        DeactivatePromptInput();
    }

    public void SubmitPrompt()
    {
        OnInputSubmitted?.Invoke();
        HandlePrompt(_promptInput.text);
        ClearPromptInputText();
        _clickToFocusOnly = true;
        DeactivatePromptInput();
    }

    public void SubmitPrompt(string prompt)
    {
        OnInputSubmitted?.Invoke();
        HandlePrompt(prompt);
        ClearPromptInputText();
        _clickToFocusOnly = true;
        DeactivatePromptInput();
    }

    protected virtual void ClearPromptInputText() => _promptInput.text = "";

    protected void SoftResetInput()
    {
        _promptInput.text = "";
        _promptInputStyle = PromptInputStyle.PromptInput;
    }

    /// <summary>
    /// Input Field 클릭시 포커스 주기
    /// </summary>
    internal void OnInputClicked()
    {
        if (!_Panel.IsOperatingPanel) return;
        _clickToFocusOnly = false;
        ActivatePromptInput();
    }

    /// 외부에서 구현
    protected abstract void HandlePrompt(string inputText);
    public virtual void ChangeSelectPage(PromptSelectPage promptSelectPage) { }
}
