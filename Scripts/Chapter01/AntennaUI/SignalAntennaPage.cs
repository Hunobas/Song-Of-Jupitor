using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.Serialization;

public class SignalAntennaPage : PanelPageBase
{
    [Title("Settings")]
    [SerializeField] SignalAntennaController _signalAntennaController;

    [Title("CamUIs")]
    [SerializeField] List<Button> _camButtons = new();
    [SerializeField] List<TextMeshProUGUI> _camText = new();
    [SerializeField] List<Image> _camTextImg = new();

    [Title("UI References")]
    [SerializeField] RectTransform _bgRealTimeOperation;
    [SerializeField] RectTransform _bgSynthesisOfFrequencies;
    [SerializeField] RectTransform _roCctvView;
    [SerializeField] RectTransform _SYFXSignalBG;
    [SerializeField] RectTransform _SYFXSignalCurrent;
    [SerializeField] RectTransform _SYFXSignalAnswer;
    [SerializeField] RectTransform _controller;
    [SerializeField] RectTransform _equalizer;
    [SerializeField] RectTransform _composer;
    [SerializeField] RectTransform _modeMaintenanceText;
    [SerializeField] RectTransform _modeCombineText;
    [SerializeField] TextMeshProUGUI _systemText;
    [SerializeField] RectTransform _signalAnalysisButton;
    
    [Title("Settings")]
    [SerializeField] Color32 _selectedColor;     // 선택 버튼 배경
    [SerializeField] Color32 _normalColor;       // 일반 버튼 배경
    [SerializeField] Color32 _selectedTextColor; // 선택 텍스트
    [SerializeField] Color32 _normalTextColor;   // 일반 텍스트
    [SerializeField] Color32 _solvedColor = new(0, 200, 0, 255); // 해결됨(초록)

    // 해결 여부 캐시
    bool[] _solvedFlags;
    bool _allChannelSolved;
    
    Sequence _combBlinkSeq;
    Coroutine _recvLoop;
    
    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        _signalAntennaController.OnCCTVChanged += UpdateButtonColor;
        _signalAntennaController.OnCCTVChanged += UpdateCamTextImage;

        _signalAntennaController.OnPhase1Solved += OnPhase1Solved;
        _signalAntennaController.OnSolvedStateChanged += OnSolvedStateChanged;
        _signalAntennaController.OnPhase2Solved += OnPhase2Solved;
    }

    private void OnDisable()
    {
        _signalAntennaController.OnCCTVChanged -= UpdateButtonColor;
        _signalAntennaController.OnCCTVChanged -= UpdateCamTextImage;

        _signalAntennaController.OnPhase1Solved -= OnPhase1Solved;
        _signalAntennaController.OnSolvedStateChanged -= OnSolvedStateChanged;
        _signalAntennaController.OnPhase2Solved -= OnPhase2Solved;
        
        StopCombBlink();
        StopCombineOPText();
    }

    public void Initialize()
    {
        _solvedFlags = new bool[_camButtons.Count];
        SetCombineSignalPage(false);
        ChangeBtnColorToSelected(_signalAntennaController.CurrrentCctvIndex);
        _systemText?.gameObject.SetActive(false);
    }

    public void SetCombineSignalPage(bool show)
    {
        _bgRealTimeOperation.gameObject.SetActive(!show);
        _roCctvView.gameObject.SetActive(!show);
        _controller.gameObject.SetActive(!show);
        _equalizer.gameObject.SetActive(!show);
        _modeMaintenanceText.gameObject.SetActive(!show);
        
        _bgSynthesisOfFrequencies.gameObject.SetActive(show);
        _SYFXSignalBG.gameObject.SetActive(show);
        _SYFXSignalCurrent.gameObject.SetActive(show);
        _SYFXSignalAnswer.gameObject.SetActive(show);
        _composer.gameObject.SetActive(show);
        _modeCombineText.gameObject.SetActive(show);
        if (_solvedFlags[WaveConstants.CctvCount]) _signalAnalysisButton.gameObject.SetActive(show);

        if (show)
        {
            StopCombBlink();
            UpdateButtonColor(WaveConstants.CctvCount);
            UpdateCamTextImage(WaveConstants.CctvCount);
            _signalAntennaController.OscilloscopeController.DeselectAll();
            _signalAntennaController.CombineFilterController.SetAllInteractable(_allChannelSolved);
        }
        else if (_allChannelSolved)
        {
            StartCombBlink();
        }
    }

    public void SetCombineSuccessText()
    {
        if (_systemText == null)
            return;

        StopCombineOPText();
        _systemText.text = "전파 합성 완료!";
        _systemText.gameObject.SetActive(true) ;
    }

    void OnPhase1Solved(bool allChannelSolved)
    {
        _allChannelSolved = allChannelSolved;
        
        if (allChannelSolved)
        {
            StartCombBlink();  // Comb 버튼/텍스트 점멸
            StartCombineOPText();   // “신호 수신중 ...” 텍스트 무한 루프
        }
        else
        {
            StopCombBlink();
            StopCombineOPText();
            ResetCombToNormal();
        }
    }

    void OnPhase2Solved(bool solved)
    {
        _allChannelSolved = false;
        _solvedFlags[WaveConstants.CctvCount] = solved;
        
        StopCombBlink();
        _signalAnalysisButton.gameObject.SetActive(solved);

        if (!solved)
        {
            StopCombineOPText();
            ResetCombToNormal();
        }

        UpdateButtonColor(WaveConstants.CctvCount);
    }

    private void OnSolvedStateChanged(int camIndex, bool solved)
    {
        if (camIndex < 0 || camIndex >= _solvedFlags.Length) return;
        _solvedFlags[camIndex] = solved;
        RefreshButtonVisual(camIndex, true);
    }

    private void UpdateButtonColor(int selected)
    {
        for (int i = 0; i < _camButtons.Count; i++)
            RefreshButtonVisual(i, i == selected);
    }

    private void UpdateCamTextImage(int camNum)
    {
        for (int i = 0; i < _camTextImg.Count; i++)
            _camTextImg[i].gameObject.SetActive(i == camNum);
    }

    // === 일관된 규칙으로 버튼/텍스트 갱신 ===
    private void RefreshButtonVisual(int index, bool isSelected = false)
    {
        if (index < 0 || index > _camButtons.Count) return;

        var btn = _camButtons[index];
        var colors = btn.colors;

        bool solved = _solvedFlags != null && index < _solvedFlags.Length && _solvedFlags[index];

        // 🎯 우선순위: solved(초록) > selected(주황 등) > normal(회색 등)
        if (solved)
        {
            // 해결된 버튼은 항상 초록 유지 (선택 여부와 무관)
            colors.normalColor   = _solvedColor;
            colors.selectedColor = _solvedColor;
        }
        else if (isSelected)
        {
            // 선택되었지만 아직 미해결
            colors.normalColor   = _selectedColor;
            colors.selectedColor = _selectedColor;
        }
        else
        {
            // 미해결 + 비선택
            colors.normalColor   = index == WaveConstants.CctvCount && _allChannelSolved ? Color.white : _normalColor;
            colors.selectedColor = _normalColor;
        }

        btn.colors = colors;

        // 텍스트 색상은 항상 "선택 여부" 기준 (해결 여부와 무관)
        if (index < _camText.Count && _camText[index] != null)
            _camText[index].color = isSelected ? _selectedTextColor : _normalTextColor;
    }

    private void ChangeBtnColorToNormal(int index)  => RefreshButtonVisual(index, false);
    private void ChangeBtnColorToSelected(int index)=> RefreshButtonVisual(index, true);
    
    
    // ====== Comb 버튼 점멸 ======
    void StartCombBlink()
    {
        var btn = _camButtons[WaveConstants.CctvCount];
        var txt = _camText[WaveConstants.CctvCount];
        if (btn == null || txt == null) return;

        Graphic img = btn.targetGraphic;
        if (img == null)
            return;

        var cb = btn.colors;
        cb.normalColor   = Color.white;
        btn.colors = cb;

        _combBlinkSeq?.Kill();
        _combBlinkSeq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Append(img.DOColor(Color.white, 0.30f).SetEase(Ease.OutQuad))
            .Join(txt.DOColor(_normalTextColor, 0.30f).SetEase(Ease.OutQuad))
            .Append(img.DOColor(Color.yellow, 0.30f).SetEase(Ease.OutQuad))
            .Join(txt.DOColor(_selectedTextColor, 0.30f).SetEase(Ease.OutQuad))
            .SetLoops(-1, LoopType.Restart);
    }

    void StopCombBlink()
    {
        _combBlinkSeq?.Kill();
        _combBlinkSeq = null;
    }

    void ResetCombToNormal()
    {
        if (_camButtons.Count <= 3 || _camText.Count <= 3) return;
        var btn = _camButtons[3];
        var txt = _camText[3];
        if (btn == null || txt == null) return;

        var img = btn.targetGraphic as Graphic;
        if (img != null) img.color = _normalColor;
        txt.color = _normalTextColor;

        var cb = btn.colors;
        cb.normalColor   = _normalColor;
        cb.selectedColor = _normalColor;
        btn.colors = cb;
    }

    // ====== “신호 수신중 ...” 루프 ======
    void StartCombineOPText()
    {
        if (_systemText == null)
            return;

        StopCombineOPText();
        _systemText.gameObject.SetActive(true);
        _recvLoop = StartCoroutine(CombineOPSequence());
    }

    void StopCombineOPText()
    {
        if (_recvLoop != null)
        {
            StopCoroutine(_recvLoop);
            _recvLoop = null;
        }
        if (_systemText != null)
        {
            _systemText.gameObject.SetActive(false);
            _systemText.text = string.Empty;
        }
    }

    IEnumerator CombineOPSequence()
    {
        string[] msgs = { "신호 수신중.", "신호 수신중..", "신호 수신중..." };
        int i = 0;
        float t = 0f;
        float interval = 0.25f;
        float next = 0f;

        while (true)
        {
            t += Time.deltaTime;
            if (t >= next)
            {
                _systemText.text = msgs[i];
                i = (i + 1) % msgs.Length;
                next += interval;
            }
            yield return null;
        }
    }
}
