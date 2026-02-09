using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EasyButtons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DialogWriterType
{
    Monolog,
    Dialog
}

public class SequentialDialogFeeder : MonoBehaviour, IDialogFeeder
{
    [SerializeField] InputEventAsset _inputUI;
    [SerializeField] InputEventAsset _inputWorld;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] TextWriter _writer;
    [SerializeField] private DefaultDisplay _monologuePanel;
    [SerializeField] private DefaultDisplay _dialoguePanel;
    [SerializeField] private DialogContainer _dialogContainer;
    [SerializeField] TextWriter _monologWriter;
    [SerializeField] TextWriter _dialogWriter;
    
    [Tooltip("시네마 모드가 끝나고 돌아갈 모드를 임의로 설정")]
    [SerializeField] private PlayModeType _nextPlayMode = PlayModeType.None;
    public void SetNextPlayMode(PlayModeType next) => _nextPlayMode = next;

    private DialogData _curDialogData;
    private ETC_DialogData _curEtcData;
    bool _bundleDirtyByLocale;
    bool _autoClose = true;
    private bool _usingETC = false; //ETC 번들인지
    [SerializeField] private DialogWriterType _dialogWriterType;

    [Header("Monolog Parameters")]
    [SerializeField] private GameObject _monologueNextButton;
    [SerializeField] private VerticalLayoutGroup _verticalGroup;
    [SerializeField] private CutscenePanelBase _cutscenePanel;
    [SerializeField] private TMP_Text _textSection;
    [SerializeField] private ScreenGlitch _screenGlitch;
    [SerializeField] private float _glitchTime = 0.05f;

    EventBlock _runningBlock;

    EventBlock _startBlock;
    [SerializeField] TimedDialogEventBlock _startTimedBlock;

    public EventBlock StartDialog => _startBlock;
    public EventBlock StartDialogTimed => _startTimedBlock;

    IEnumerator _dialogEnumerator;
    IEnumerator _lineEnumerator;
    Coroutine _autoAdvanceCr;
    bool _isFinished = false;
    private bool _isBindData;

    IEnumerator _linEnumerator;
    IList<string> _currentLines;
    int _curLineIndex = -1;
    private List<DialogData> _activeBundle; // 현재 묶음 보관(스냅샷)
    private int _bundleItemIndex = -1; // 묶음 내 DialogData 인덱스

    private string _prevImagePath;

    public event Action TalkStartEvent;
    public event Action TalkEndEvent;

    private void Awake()
    {
        EventBlock self = null;
        _startBlock = self = new EventBlock(() => StartSequence(self, timeoutSeconds: null));
        _startTimedBlock?.Bind(this, b => StartSequence(b, _startTimedBlock.Seconds));
    }

    private void OnEnable()
    {
        _inputUI.OnSpaceKeyDown += FeedNext;
        _inputWorld.OnSpaceKeyDown += FeedNext;
        _inputUI.OnF1KeyDown += MoveUntilEnd;
        _inputWorld.OnF1KeyDown += MoveUntilEnd;
        LocalizationManager.Instance.OnLocaleChanged += HandleLocaleChanged;
    }

    private void OnDisable()
    {
        _inputUI.OnSpaceKeyDown -= FeedNext;
        _inputWorld.OnSpaceKeyDown -= FeedNext;
        _inputUI.OnF1KeyDown -= MoveUntilEnd;
        _inputWorld.OnF1KeyDown -= MoveUntilEnd;

        if (LocalizationManager.Instance == null) return;
        LocalizationManager.Instance.OnLocaleChanged -= HandleLocaleChanged;
    }

    public void BindDialog(DialogObject dialog)
    {
        var dials = new List<DialogObject>();
        dials.Add(dialog);
        _dialogEnumerator = dials.GetEnumerator();
    }

    public void BindDialogContainer(DialogContainer container)
    {
        _dialogEnumerator = container.PeekCurrentBundle().GetEnumerator();
    }

    public void ChangeWriterToMonolog()
    {
        _writer = _monologWriter;
        _dialogWriterType = DialogWriterType.Monolog;
        _dialoguePanel.TurnOff();
    }

    public void ChangeWriterToDialog()
    {
        _writer = _dialogWriter;
        _dialogWriterType = DialogWriterType.Dialog;
        _monologuePanel.TurnOff();
    }

    [Button]
    public void StartNextDialog() => StartDialogProcess();

    [Button]
    public void StartETCDialog(int id)
    {
        _isBindData = false;
        _usingETC = true;
        _bundleItemIndex = -1;

        // ETC 번들 스냅샷 생성
        var etcBundle = _dialogContainer.GetETCBundle(id);
        _activeBundle = etcBundle.Items;

        // 번들 없으면 바로 종료
        if (_activeBundle == null || _activeBundle.Count == 0)
        {
            DialogEndSequence();
            return;
        }

        StartDialogProcess(() => _activeBundle.GetEnumerator());
    }

    void StartSequence(EventBlock caller, float? timeoutSeconds)
    {
        _runningBlock = caller;
        StartDialogProcess();

        if (timeoutSeconds.HasValue)
        {
            if (_autoAdvanceCr != null) StopCoroutine(_autoAdvanceCr);
            _autoAdvanceCr = StartCoroutine(AutoAdvanceRoutine(timeoutSeconds.Value));
        }
    }

    private void StartDialogProcess()
    {
        _usingETC = false;
        StartDialogProcess(() =>
        {
            _activeBundle = _dialogContainer.PeekCurrentBundle();
            return _activeBundle?.GetEnumerator();
        });
    }

    private void StartDialogProcess(Func<IEnumerator> getEnumerator)
    {
        if (!_isBindData)
        {
            _dialogEnumerator = getEnumerator();
            _isBindData = true;
            _bundleItemIndex = -1;
        }

        if (_activeBundle != null && _activeBundle.Count == 0)
        {
            DialogEndSequence();
            return;
        }

        if (_dialogEnumerator == null)
        {
            DialogEndSequence();
            return;
        }

        _writer.gameObject.SetActive(true);
        _isFinished = false;
        _writer.Clear();

        if (!_dialogEnumerator.MoveNext())
        {
            DialogEndSequence();
            return;
        }

        GameState.Instance.IsPlayingDialog = true;
        _bundleItemIndex++;

        if (_dialogEnumerator.Current is DialogData dd)
        {
            _curDialogData = dd;
            _curEtcData = null;
            _currentLines = _curDialogData.Paragraphs;
            _lineEnumerator = _currentLines.GetEnumerator();
            _curLineIndex = -1;
            UpdateCutsceneImage(_curDialogData.ImagePath);
        }
        else if (_dialogEnumerator.Current is ETC_DialogData etc)
        {
            _curEtcData = etc;
            _curDialogData = null;
            _currentLines = _curEtcData.Paragraphs;
            _lineEnumerator = _currentLines.GetEnumerator();
            _curLineIndex = -1;
        }

        ShowCurDialogPanel();
        CallTalkStartEvent();
        FeedNext();
    }


    private void UpdateCutsceneImage(string newPath)
    {
        if (_dialogWriterType != DialogWriterType.Monolog || !_cutscenePanel)
            return;

        if (newPath == _prevImagePath)
            return;

        _screenGlitch.PlayRetroTransition(_glitchTime, SwapImage);

        _prevImagePath = newPath;
        return;

        void SwapImage()
        {
            var sprite = ResourceManager.Instance.Load<Sprite>(newPath);
            _cutscenePanel.ImagePanel.SetSprite(sprite);
            // _cutscenePanel.ImagePanel.Image.sprite = sprite;

            bool hasSprite = sprite != null;
            _cutscenePanel.ImagePanel.gameObject.SetActive(hasSprite);

            if (_textSection != null)
            {
                _textSection.alignment = hasSprite
                    ? TextAlignmentOptions.TopLeft
                    : TextAlignmentOptions.Left;
            }
            if (_verticalGroup != null)
            {
                _verticalGroup.padding.bottom = hasSprite ? 200 : 0;
            }
        }
    }

    private IEnumerator AutoAdvanceRoutine(float seconds)
    {
        bool ended = false;
        AddTalkEndEvent(() => ended = true);

        bool pressed = false;

        void OnPressed()
        {
            pressed = true;
        }

        _inputUI.OnSpaceKeyDown += OnPressed;
        _inputWorld.OnSpaceKeyDown += OnPressed;

        while (!ended)
        {
            pressed = false;
            float t = 0f;

            while (!pressed && t < seconds && !ended && !(!_isBindData && _isFinished))
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (ended || (!_isBindData && _isFinished)) break;

            if (!pressed)
            {
                FeedNext();
            }

            yield return null;
        }

        _inputUI.OnSpaceKeyDown -= OnPressed;
        _inputWorld.OnSpaceKeyDown -= OnPressed;
        _autoAdvanceCr = null;
    }

    public void FeedNext()
    {
        if (_lineEnumerator is null) return;
        if (_writer.TrySkip()) return;
        if (_isFinished) return;

        if (!_lineEnumerator.MoveNext())
        {
            _isBindData = true;

            bool isLastItemOfBundle = (_activeBundle == null) || (_bundleItemIndex + 1 >= _activeBundle.Count);

            if (isLastItemOfBundle)
            {
                // ETC는 커밋 없이 그냥 종료
                if (!_usingETC)
                    _dialogContainer.CommitConsumedBundle();

                DialogEndSequence();
                return;
            }

            if (_bundleDirtyByLocale)
            {
                _bundleDirtyByLocale = false;

                if (!_usingETC)
                {
                    int startIdForNext = -1;
                    if (_dialogEnumerator?.Current is DialogData cur)
                    {
                        var dict = DBManager.Instance.GetPrologueData();
                        var keys = dict.Keys.ToList();
                        int idx = keys.FindIndex(k => k == cur.ID);
                        startIdForNext = (idx >= 0 && idx + 1 < keys.Count) ? keys[idx + 1] : -1;
                    }

                    if (startIdForNext != -1)
                    {
                        var dict = DBManager.Instance.GetPrologueData();
                        var bundle = DialogueService.GetBundleFrom(dict, startIdForNext);
                        _activeBundle = bundle.Items;
                        _dialogEnumerator = _activeBundle.GetEnumerator();
                        _bundleItemIndex = -1;
                        _isBindData = true;
                    }
                    else
                    {
                        _activeBundle = null;
                        _dialogEnumerator = null;
                    }
                }
            }

            if (_activeBundle != null && _bundleItemIndex + 1 < _activeBundle.Count)
            {
                StartDialogProcess(() => _activeBundle.GetEnumerator());
            }
            else
            {
                if (!_usingETC)
                    _dialogContainer.CommitConsumedBundle();

                DialogEndSequence();
            }

            return;
        }

        // 줄 진행
        _curLineIndex = Mathf.Clamp(_curLineIndex + 1, 0,
            _currentLines?.Count > 0 ? _currentLines.Count - 1 : int.MaxValue);
        SetSpeakerNameText((_curDialogData != null) ? _curDialogData.SpeakerName : _curEtcData?.SpeakerName);
        var nextString = _lineEnumerator.Current as string;
        _writer.AddLine(nextString);
    }

    // Editor utility
    void MoveUntilEnd()
    {
        if (_lineEnumerator is null || !_lineEnumerator.MoveNext()) return; // already reached end

        string nextString = _lineEnumerator.Current as string;
        while (_lineEnumerator.MoveNext())
            nextString = _lineEnumerator.Current as string;

        if (_currentLines != null && _currentLines.Count > 0)
            _curLineIndex = _currentLines.Count - 1;

        _writer.AddLine(nextString);
    }

    // current running has end
    void OnDialogEnd()
    {
        //var currentSet = ((DialogObject)_dialogEnumerator.Current);
        //currentSet.onEnd?.Invoke();
        
        GameState.Instance.IsPlayingDialog = false;

        _runningBlock?.OnDone();
    }

    private void HandleLocaleChanged()
    {
        // === (현재 줄 교체) 기존 코드 그대로 ===
        if (_dialogEnumerator?.Current is DialogData dialogData)
        {
            var id = dialogData.ID;
            ConsoleLogger.Log($"다이얼로그 ID: {id}");
            var dict = DBManager.Instance.GetPrologueData();
            
            if (dict.TryGetValue(id, out var newData))
            {
                var line = newData.Paragraphs.ElementAtOrDefault(GetCurrentLineIndex());
                if (line != null)
                    _writer.SkipAndOverwrite(line);
            }
        }
        else if (_dialogEnumerator?.Current is ETC_DialogData etcData)
        {
            var id = etcData.ID;
            var dict = DBManager.Instance.GetETCDatas();
            if (dict.TryGetValue(id, out var newEtc))
            {
                var line = newEtc.Paragraphs.ElementAtOrDefault(GetCurrentLineIndex());
                if (line != null)
                    _writer.SkipAndOverwrite(line);
            }
        }

        _bundleDirtyByLocale = true;
    }

    void SetSpeakerNameText(string speakerName)
    {
        _nameText.text = speakerName;
    }

    void CallTalkStartEvent()
    {
        if (TalkStartEvent is null)
            return;

        TalkStartEvent.Invoke();
        TalkStartEvent = null;
    }

    void CallTalkEndEvent()
    {
        if (TalkEndEvent is null)
            return;

        TalkEndEvent.Invoke();
        TalkEndEvent = null;
    }

    int GetCurrentLineIndex()
    {
        if (_curLineIndex < 0)
            return 0;
        if (_currentLines == null || _currentLines.Count == 0)
            return 0;

        return Mathf.Clamp(_curLineIndex, 0, _currentLines.Count - 1);
    }

    public void AddTalkStartEvent(Action eventHandle)
    {
        TalkStartEvent += eventHandle;
    }

    public void AddTalkEndEvent(Action eventHandle)
    {
        TalkEndEvent += eventHandle;
    }

    public void IgnoreFeedNextInput()
    {
        _monologueNextButton?.SetInactive();
        _inputWorld.OnSpaceKeyDown -= FeedNext;
    }

    public void ConnectFeedNextInput()
    {
        _monologueNextButton?.SetActive();
        _inputWorld.OnSpaceKeyDown += FeedNext;
    }

    //대화 강제 종료
    public void ForceStop()
    {
        //TalkEnd Event는 전부 무시함.
        TalkEndEvent = null;
        DialogEndSequence();
    }

    private void DialogEndSequence()
    {
        _isFinished = true;
        _isBindData = false;
        _cutscenePanel.ClearSprite();
        OnDialogEnd();
        CallTalkEndEvent();
        CloseCurDialogPanel();
    }

    private void ShowCurDialogPanel()
    {
        switch (_dialogWriterType)
        {
            case DialogWriterType.Dialog:
                _dialoguePanel.TurnOn();
                break;

            case DialogWriterType.Monolog:
                _monologuePanel.TurnOn();
                break;
        }
    }

    private void CloseCurDialogPanel()
    {
        if (!_autoClose)
            return;
        
        switch (_dialogWriterType)
        {
            case DialogWriterType.Dialog:
                _dialoguePanel.TurnOff();
                break;

            case DialogWriterType.Monolog:
                _monologuePanel.TurnOff();
                break;
        }
    }
    
    public void SetAutoClose(bool autoClose) => _autoClose = autoClose;

    public void ResetTalkEvents()
    {
        TalkEndEvent = null;
        TalkStartEvent = null;
    }
}