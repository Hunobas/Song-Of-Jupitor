using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

public class SelectionPanelPrompt : PanelPrompt
{
    [Header("Setting")] 
    [SerializeField] private TextMeshProUGUI _systemText;
    [SerializeField] private PromptContainerBase _promptContainer;
    
    private string[] _loadingTexts = { "입력 처리중.", "입력 처리중..", "입력 처리중..." };
    private string _loadingFailedText = "입력 실패!";
    private string _loadingSuccessText = "입력 성공!";

    private string[] _loadingTextsKey = new string[3]; //배열 크기는 3개로 고정
    private string _loadingFailedTextKey;
    private string _loadingSuccessTextKey;
    
    
    private bool _isRunning;
    private bool _isPromptSuccess; //이전에 명령어 입력을 성공했는지
    private Coroutine _loadingRoutine;

    protected override void Awake()
    {
        base.Awake();
        RefreshLoadingText();
        
        SetLoadingText(
            new[]{ PromptKeys.Common.Loading1,PromptKeys.Common.Loading2, PromptKeys.Common.Loading3},
            PromptKeys.Common.Fail,
            PromptKeys.Power.DownloadedDone);
        
        LocalizationManager.Instance.OnLocaleChanged += RefreshLoadingText;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _isRunning = false;
    }

    protected override void HandlePrompt(string prompt)
    {
        if (_isRunning) return;
        if (_loadingRoutine != null)
        {
            StopCoroutine(_loadingRoutine);
        }
        if (prompt is "") return;
        _systemText.text = "";

        _isPromptSuccess = false;
        if (!_promptContainer.CheckPrompt(prompt))
        {
            _loadingRoutine = StartCoroutine(LoadingSequence(() => _systemText.text = _loadingFailedText));
            return;
        }
        
        _isRunning = true;
        _loadingRoutine = StartCoroutine(LoadingSequence(() =>
        {
            _isPromptSuccess = true;
            _systemText.text = _loadingSuccessText;
            _promptContainer.GetPromptEvent()?.Invoke(); //등록된 명령어의 이벤트 실행
        }));
    }

    private IEnumerator LoadingSequence(Action onComplete)
    {
        float time = 0f;
        int index = 0;
        float interval = 0.25f;
        float nextTime = 0f;

        while (time < 1.5f)
        {
            time += Time.deltaTime;

            if (time >= nextTime)
            {
                _systemText.text = _loadingTexts[index];
                index = (index + 1) % _loadingTexts.Length;
                nextTime += interval;
            }

            yield return null;
        }
        
        _isRunning = false;
        
        onComplete?.Invoke();
        
        if (isActiveAndEnabled && Panel.IsOperatingPanel)
            ActivatePromptInput();
    }

    /// <summary>
    /// 명령어가 유효한지 확인
    /// </summary>
    /// <returns></returns>
    private bool CheckPrompt()
    {
        return false;
    }

    protected override void ClearPromptInputText()
    {
        base.ClearPromptInputText();
    }
    
    /// <summary>
    /// 로딩 텍스트 새로고침
    /// </summary>
    public void RefreshLoadingText()
    {
        if (_systemText.text == "") return; 
        _systemText.text = L10n.T(_isPromptSuccess ? _loadingSuccessTextKey : _loadingFailedTextKey);
    }

    /// <summary>
    /// 로딩 텍스트 설정
    /// </summary>
    public void SetLoadingText(string[] loadingText, string loadingFailedText, string loadingSuccessText)
    {
        _loadingTextsKey = loadingText;
        _loadingFailedTextKey = loadingFailedText;
        _loadingSuccessTextKey = loadingSuccessText;
        
        for (int i = 0; i < loadingText.Length; i++)
        {
            _loadingTexts[i] = L10n.T(loadingText[i]);
        }
        _loadingFailedText = L10n.T(loadingFailedText);
        _loadingSuccessText = L10n.T(loadingSuccessText);
    } 
}
