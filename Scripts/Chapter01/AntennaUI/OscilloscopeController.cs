using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class OscilloscopeController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider amplitudeSlider;      
    [SerializeField] private Slider displacementSlider;   
    [SerializeField] private RotationKnob frequencyKnob;  
    [SerializeField] private SineWaveGraphic[] sineWaveGraphic;
    [SerializeField] private TextMeshProUGUI[] frequencyValueTexts;
    [SerializeField] private Image[] channelBar;
    [SerializeField] private RectTransform[] chNormalText_Imgs;
    [SerializeField] private RectTransform[] chSelectedText_Texts;

    [Header("Controllers")]
    [SerializeField] private SignalAntennaController signalAntennaController;
    
    [Header("Wave Colors")]
    [SerializeField] private Color32 selectedWaveColor = new(0xF1, 0x5A, 0x23, 0xFF);
    [SerializeField] private Color32 normalWaveColor   = Color.white;
    [SerializeField] private Color32 solvedWaveColor   = new(0x00, 0xC8, 0x00, 0xFF);
    
    [Header("Hint Visuals")]
    [SerializeField] public int   ResMin = 5;
    [SerializeField] public int   ResMax = 150;
    [SerializeField] public float WMin   = 0.15f;
    [SerializeField] public float WMax   = 1.00f;
    [SerializeField] public float WSigma = 0.3f;
    [SerializeField] public float RSigma = 0.2f;
    [SerializeField] int glitchUpdateInterval = 5;

    private float currentFrequency = 1f;
    private float currentAmplitude = 0f;
    private float currentDisplacement = 0f;

    private int glitchElapsed = 99;
    private bool[] bGlitchOff = new bool[WaveConstants.CctvCount];

    private void OnDisable()
    {
        if (sineWaveGraphic != null)
        {
            for (int i = 0; i < sineWaveGraphic.Length; i++)
            {
                var g = sineWaveGraphic[i];
                if (g == null) continue;

                DOTween.Kill(g);
                DOTween.Kill(g.gameObject);
            }
        }

        DOTween.Kill(gameObject);
    }

    private void Awake()
    {
        InitializeSliders();
        InitializeKnob();
        
        if (signalAntennaController != null)
        {
            signalAntennaController.OnInitialized        += HandleInitialized;
            signalAntennaController.OnCCTVChanged        += HandleCctvChanged;
            signalAntennaController.OnWaveDataChanged    += HandleWaveDataChanged;
            signalAntennaController.OnSolvedStateChanged += HandleSolvedStateChanged;
            signalAntennaController.OnPhase2Solved       += HandlePhase2Solved;
        }
    }
    
    private void Start()
    {
        InitializeSliders();
        InitializeKnob();

        int selected = signalAntennaController != null
            ? signalAntennaController.CurrrentCctvIndex
            : 0;

        UpdateWaveColors(selected);
    }

    private void OnDestroy()
    {
        if (signalAntennaController != null)
        {
            signalAntennaController.OnInitialized        -= HandleInitialized;
            signalAntennaController.OnCCTVChanged        -= HandleCctvChanged;
            signalAntennaController.OnWaveDataChanged    -= HandleWaveDataChanged;
            signalAntennaController.OnSolvedStateChanged -= HandleSolvedStateChanged;
            signalAntennaController.OnPhase2Solved       -= HandlePhase2Solved;
        }
    }

    public void DeselectAll()
    {
        int i = signalAntennaController.CurrrentCctvIndex;
        chSelectedText_Texts[i].gameObject.SetActive(false);
        chNormalText_Imgs[i].gameObject.SetActive(true);
        frequencyValueTexts[i].color = Color.white;
        channelBar[i].color = Color.white;
    }

    public void SetAllInteractable(bool on)
    {
        amplitudeSlider.interactable = on;
        displacementSlider.interactable = on;
        frequencyKnob.SetInteractable(on);
    }

    private void InitializeSliders()
    {
        if (amplitudeSlider != null)
        {
            amplitudeSlider.minValue = WaveConstants.HandleMin;
            amplitudeSlider.maxValue = WaveConstants.HandleMax;
            amplitudeSlider.wholeNumbers = false;
            amplitudeSlider.onValueChanged.AddListener(OnAmplitudeChanged);
        }

        if (displacementSlider != null)
        {
            displacementSlider.minValue = WaveConstants.HandleMin;
            displacementSlider.maxValue = WaveConstants.HandleMax;
            displacementSlider.wholeNumbers = false;
            displacementSlider.onValueChanged.AddListener(OnDisplacementChanged);
        }
    }

    private void InitializeKnob()
    {
        if (frequencyKnob != null)
            frequencyKnob.OnValueChanged += OnFrequencyChanged;
    }

    public void SetValues(float frequency, float amplitudeNoRender, float displacementNoRender)
    {
        currentFrequency    = frequency;
        currentAmplitude    = amplitudeNoRender;
        currentDisplacement = displacementNoRender;

        UpdateControllerValues();
        UpdateValueTexts();
        UpdateSineWave(signalAntennaController.CurrrentCctvIndex);
    }

    private void UpdateControllerValues()
    {
        if (frequencyKnob != null)
        {
            float normalized = WaveConstants.NormalizeFrequency(currentFrequency);
            float handle     = Mathf.Lerp(WaveConstants.HandleMin, WaveConstants.HandleMax, normalized);
            frequencyKnob.SetValueWithoutNotify(handle);
        }

        if (amplitudeSlider != null)
        {
            float normalized = WaveConstants.NormalizeAmplitude(currentAmplitude);
            float handle     = Mathf.Lerp(WaveConstants.HandleMin, WaveConstants.HandleMax, normalized);
            amplitudeSlider.SetValueWithoutNotify(handle);
        }

        if (displacementSlider != null)
        {
            float normalized = WaveConstants.NormalizeDisplacement(currentDisplacement);
            float handle     = Mathf.Lerp(WaveConstants.HandleMin, WaveConstants.HandleMax, normalized);
            displacementSlider.SetValueWithoutNotify(handle);
        }
    }

    private void UpdateValueTexts()
    {
        if (frequencyValueTexts == null || signalAntennaController == null)
            return;

        for (int i = 0; i < frequencyValueTexts.Length; i++)
        {
            var text = frequencyValueTexts[i];
            if (text == null) continue;

            WaveData wave = signalAntennaController.GetCurrent(i);
            float normalized = WaveConstants.NormalizeFrequency(wave.Frequency);
            float handle = Mathf.Lerp(WaveConstants.HandleMin, WaveConstants.HandleMax, normalized);

            int displayInt = Mathf.RoundToInt(handle);
            text.text = $": {displayInt}_MHz";
        }
    }

    private void UpdateSineWave(int index)
    {
        var g = (sineWaveGraphic != null && index >= 0 && index < sineWaveGraphic.Length) ? sineWaveGraphic[index] : null;
        g?.SetFrequency(currentFrequency);
    }

    // === 입력 핸들러 ===
    private void OnFrequencyChanged(float handleValue)
    {
        float normalized  = Mathf.InverseLerp(WaveConstants.HandleMin, WaveConstants.HandleMax, handleValue);
        currentFrequency  = WaveConstants.DenormalizeFrequency(normalized);

        UpdateValueTexts();
        UpdateSineWave(signalAntennaController.CurrrentCctvIndex);
        NotifyValueChanged();
    }

    private void OnAmplitudeChanged(float handleValue)
    {
        float normalized  = Mathf.InverseLerp(WaveConstants.HandleMin, WaveConstants.HandleMax, handleValue);
        currentAmplitude  = WaveConstants.DenormalizeAmplitude(normalized);
        NotifyValueChanged();
    }

    private void OnDisplacementChanged(float handleValue)
    {
        float normalized      = Mathf.InverseLerp(WaveConstants.HandleMin, WaveConstants.HandleMax, handleValue);
        currentDisplacement   = WaveConstants.DenormalizeDisplacement(normalized);
        NotifyValueChanged();
    }

    private void NotifyValueChanged()
    {
        signalAntennaController?.UpdateCurrentWaveData(currentFrequency, currentAmplitude, currentDisplacement);
    }

    private void HandleInitialized(int ch, WaveData init)
    {
        if (sineWaveGraphic != null && ch >= 0 && ch < sineWaveGraphic.Length && sineWaveGraphic[ch] != null)
        {
            sineWaveGraphic[ch].SetFrequency(init.Frequency);
            var ans = signalAntennaController.GetAnswer(ch);
            ApplyHintVisuals(ch, init, ans);
        }

        if (signalAntennaController != null && ch == signalAntennaController.CurrrentCctvIndex)
        {
            currentFrequency    = init.Frequency;
            currentAmplitude    = init.AmplitudeNoRender;
            currentDisplacement = init.DisplacementNoRender;

            UpdateControllerValues();
            UpdateValueTexts();
        }
    }

    private void HandleCctvChanged(int selectedIndex)
    {
        UpdateWaveColors(selectedIndex);
        UpdateValueTexts();

        for (int i = 0; i < WaveConstants.CctvCount; i++)
        {
            bool isActive = (i == selectedIndex);
            chSelectedText_Texts[i].gameObject.SetActive(isActive);
            chNormalText_Imgs[i].gameObject.SetActive(!isActive);
            frequencyValueTexts[i].color = isActive ? new Color32(0xFF, 0x88, 0x40, 0xFF) : Color.white;
            channelBar[i].color = isActive ? new Color32(0xFF, 0x88, 0x40, 0xFF) : Color.white;
        }

        UpdateSineWave(selectedIndex);
    }

    private void UpdateWaveColors(int selectedIndex)
    {
        if (sineWaveGraphic == null) return;

        var solvedArr = signalAntennaController != null ? signalAntennaController.Solved : null;

        for (int i = 0; i < sineWaveGraphic.Length; i++)
        {
            var g = sineWaveGraphic[i];
            if (g == null) continue;

            bool solved = solvedArr != null && i < solvedArr.Length && solvedArr[i];

            Color target = solved
                ? (Color)solvedWaveColor
                : (i == selectedIndex ? (Color)selectedWaveColor : (Color)normalWaveColor);

            if (g.color.Equals(target)) continue;

            DOTween.Kill(g); 
            g.DOColor(target, 0.20f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy);

        }
    }
    
    private void HandleWaveDataChanged(int ch, WaveData cur)
    {
        if (signalAntennaController == null) return;
        if (ch != signalAntennaController.CurrrentCctvIndex) return;

        var g = (sineWaveGraphic != null && ch < sineWaveGraphic.Length) ? sineWaveGraphic[ch] : null;
        if (g == null) return;

        g.SetFrequency(cur.Frequency);
        WaveData ans = signalAntennaController.GetAnswer(ch);
        ApplyHintVisuals(ch, cur, ans);
    }

    private void HandleSolvedStateChanged(int ch, bool solved)
    {
        if (sineWaveGraphic == null || ch < 0 || ch >= sineWaveGraphic.Length) return;

        var g = sineWaveGraphic[ch];
        if (g == null) return;

        int selected = signalAntennaController != null ? signalAntennaController.CurrrentCctvIndex : -1;

        Color target = solved
            ? solvedWaveColor
            : (ch == selected ? selectedWaveColor : normalWaveColor);

        DOTween.Kill(g);

        g.DOColor(target, 0.25f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy);
        
        bGlitchOff[ch] = solved;
        glitchElapsed = 0;
        g.StopGlitch();
    }

    private void HandlePhase2Solved(bool solved)
    {
        SetAllInteractable(!solved);
    }

    private void ApplyHintVisuals(int ch, in WaveData cur, in WaveData ans)
    {
        var g = (sineWaveGraphic != null && ch < sineWaveGraphic.Length) ? sineWaveGraphic[ch] : null;
        if (g == null) return;

        // --- 1) 원시 오차/토럴런스/정규화 ---
        float aDiff = Mathf.Abs(cur.AmplitudeNoRender    - ans.AmplitudeNoRender);
        float dDiff = Mathf.Abs(cur.DisplacementNoRender - ans.DisplacementNoRender);
        float fDiff = Mathf.Abs(cur.Frequency            - ans.Frequency);

        float aRange = Mathf.Max(1e-6f, WaveConstants.AmplitudeMax    - WaveConstants.AmplitudeMin);
        float dRange = Mathf.Max(1e-6f, WaveConstants.DisplacementMax - WaveConstants.DisplacementMin);
        float fRange = Mathf.Max(1e-6f, WaveConstants.FrequencyMax - WaveConstants.FrequencyMin);

        // 근접도(0=멀다, 1=가깝다)
        float pA = aDiff <= signalAntennaController.AmplitudeTolerance ? 1f : 1f - Mathf.Clamp01(aDiff / aRange);
        float pD = dDiff <= signalAntennaController.DisplacementTolerance ? 1f : 1f - Mathf.Clamp01(dDiff / dRange);
        float pF = fDiff <= signalAntennaController.FrequencyTolerance ? 1f : 1f - Mathf.Clamp01(fDiff / fRange);

        float adSquare = (pA * pA + pD * pD) * 0.5f;
        float dG = Mathf.Sqrt((0.5f * adSquare + 1.5f * pF * pF) / 2f);

        // 정규분포 가중치 계산
        float gaussianW = Mathf.Exp(-Mathf.Pow(adSquare - 1f, 2f) / (2f * WSigma * WSigma));
        float gaussianR = Mathf.Exp(-Mathf.Pow(adSquare - 1f, 2f) / (2f * RSigma * RSigma));
        
        // 주파수 낮을수록 해상도 낮추는 보정
        float fNorm     = WaveConstants.NormalizeFrequency(cur.Frequency);
        float freqScale = Mathf.Lerp(0.167f, 1.0f, 1f - Mathf.Pow(1f - fNorm, 5f));

        g.LineWidth  = Mathf.Lerp(WMin,  WMax,  gaussianW);
        g.Resolution = Mathf.RoundToInt(Mathf.Lerp(ResMin, ResMax, gaussianR) * freqScale);

        glitchElapsed++;
        if (!bGlitchOff[ch] && glitchElapsed >= glitchUpdateInterval)
        {
            g.StopGlitch();
            g.StartGlitch(1f - Mathf.Clamp01(dG));
            
            glitchElapsed = 0;
        }
        g.SetVerticesDirty();
    }
    
    public void ForceRefreshAllVisuals()
    {
        UpdateValueTexts();

        int selected = signalAntennaController != null
            ? signalAntennaController.CurrrentCctvIndex
            : 0;

        if (sineWaveGraphic != null && selected >= 0 && selected < sineWaveGraphic.Length)
        {
            var g = sineWaveGraphic[selected];
            if (g != null)
            {
                WaveData w = signalAntennaController.GetCurrent(selected);
                g.SetFrequency(w.Frequency);
                g.SetVerticesDirty();
            }
        }

        UpdateWaveColors(selected);
    }
}
