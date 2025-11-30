// CombineFilterController.cs (교체)
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CombineFilterController : MonoBehaviour
{
    [Header("Combine Wave")]
    [SerializeField] private CombinedSineWaveGraphic _current;
    [SerializeField] private CombinedSineWaveGraphic _answer;

    [Header("UI References")]
    [SerializeField] private RotationKnob[] _channelKnobs;
    [SerializeField] private Image[] _channelActivateImages;
    [SerializeField] private Slider _consistencyGauge;
    
    [Header("Controllers")]
    [SerializeField] private SignalAntennaController _signalAntennaController;
    
    [Header("Settings")]
    [SerializeField] private float _kOLength = WaveConstants.Combine_KO_Length;
    [SerializeField] private int _consistencyGaugeSteps = 20;
    
    [Header("Hint Visuals")]
    [SerializeField] private float _hintStartGauge  = 0.6f; 
    [SerializeField] private float _hintEndGauge = 0.8f;
    [SerializeField, Range(0f, 1f)] private float _hintFarAlpha  = WaveConstants.Combine_HintFarAlpha; 
    [SerializeField, Range(0f, 1f)] private float _hintNearAlpha = WaveConstants.Combine_HintNearAlpha;
    [SerializeField, Range(0f, 1f)] private float _hintFadeSec   = 0.15f;
    [SerializeField, Range(0.05f, 1.5f)] float _gaugeNoiseInterval = 0.1f;

    private SignalWaveData _store;
    private bool _solved = false;
    
    // ===== 내부 상태 =====
    float _a, _b, _c;

    // 정답 직선 파라미터화: x=t/a, y=t/b, z=t/c
    Vector3 _v;              // 방향벡터 (1/a, 1/b, 1/c)
    Vector3 _vUnit;          // 정규화 방향
    float   _vLen;

    // K: 원점에서 KO만큼 떨어진 시작점(레이 시작점)
    Vector3 _K;
    float   _t0;             // K에 대응하는 t (aH = bI = cJ = t0)

    // G'': 노브 상한(0~100) 내에서 가능한 최대 위치
    float   _sMax;           // |KG''| = (tMax - t0) * |v|

    float _minTolerance;
    float _maxTolerance;
    
    bool _gaugeNoiseEnabled  = true;
    int  _gaugeBaseStep      = 0;
    float _gaugeNoiseElapsed = 0f;

    float _distanceRatio     = 0f;
    float _wCoef             = 1f;
    float _wAlong;
    float _lineWidthMul;
    
    Tween _hintTw;
    
    Tween[] _activatePulse;
    const float _pulseMinA = 0.25f;
    const float _pulseMaxA = 0.6f;
    const float _pulseOneWaySec = 0.6f;
    
    void Awake()
    {
        SetAllInteractable(false);

        if (_channelKnobs != null)
        {
            int hidden = WaveConstants.CctvCount;
            
            for (int i = 0; i < hidden; i++)
            {
                int ch = i;                         // 람다 캡처 시 값을 저장하기 위함
                if (_channelKnobs[ch] != null)
                {
                    _channelKnobs[ch].OnValueChanged += _ => HandleKnobChanged();
                    _channelKnobs[ch].OnPointerDownEvent += () => StartActivatePulse(ch);
                    _channelKnobs[ch].OnPointerUpEvent   += () => StopActivatePulse(ch);
                }
            }
            
            // 숨겨진 4번 채널 노브 조정 시 글리치 발생
            if (_channelKnobs.Length > hidden && _channelKnobs[hidden] != null)
            {
                _channelKnobs[hidden].OnValueChanged += _ =>
                {
                    if (_current != null)
                    {
                        float intensity = Random.Range(0.55f, 0.95f);
                        float duration  = Random.Range(0.08f, 0.2f);
                        _current.GlitchOneShot(intensity, duration);
                    }
                };
            }
        }
        
        if (_channelActivateImages != null && _channelActivateImages.Length > 0)
        {
            _activatePulse = new Tween[_channelActivateImages.Length];

            for (int i = 0; i < _channelActivateImages.Length; i++)
            {
                if (_channelActivateImages[i] == null)
                    continue;
                
                Color c = _channelActivateImages[i].color; 
                c.a = 0f; 
                _channelActivateImages[i].color = c;
            }
        }
        
        _consistencyGauge.wholeNumbers = true;
        _consistencyGauge.minValue = 0;
        _consistencyGauge.maxValue = _consistencyGaugeSteps;
        _consistencyGauge.value = 0;
    }

    void Update()
    {
        if (_gaugeNoiseEnabled == false)
            return;
        
        _gaugeNoiseElapsed += Time.deltaTime;
        if (_consistencyGauge != null && _gaugeNoiseElapsed >= _gaugeNoiseInterval)
        {
            _gaugeNoiseElapsed = 0f;
            
            int noise = Random.Range(-2, 1);
            int displayStep = Mathf.Clamp(_gaugeBaseStep + noise, (int)_consistencyGauge.minValue, (int)_consistencyGauge.maxValue);
            _consistencyGauge.value = displayStep;
        }
    }

    public void SetupWithStore(SignalWaveData store, float minTolerance, float maxTolerance)
    {
        _store = store;
        // Answer의 진폭만 사용(a,b,c). 0 방지용 epsilon
        WaveData[] ans = _store.Answers;
        if (ans == null || ans.Length < WaveConstants.CctvCount)
        {
            Debug.LogError("[Combine] Answers not ready in store.");
            return;
        }

        _a = Mathf.Max(1e-4f, ans[0].AmplitudeNoRender);
        _b = Mathf.Max(1e-4f, ans[1].AmplitudeNoRender);
        _c = Mathf.Max(1e-4f, ans[2].AmplitudeNoRender);

        // v=(1/a,1/b,1/c), 단위/길이
        _v    = new Vector3(_a, _b, _c);
        _vLen = _v.magnitude;
        _vUnit = (_vLen > 0f) ? _v / _vLen : Vector3.one;
        
        // 정답 직선 시작점 K: |OK| = KO
        float KO = Mathf.Max(0.001f, _kOLength);
        _K = _vUnit * KO;

        // 노브 한계로부터 G''(최대 t)를 계산: x,y,z ≤ 100 ⇒ t ≤ min(a*100, b*100, c*100)
        float sx = (_vUnit.x > 1e-6f) ? (100f - _K.x) / _vUnit.x : float.PositiveInfinity;
        float sy = (_vUnit.y > 1e-6f) ? (100f - _K.y) / _vUnit.y : float.PositiveInfinity;
        float sz = (_vUnit.z > 1e-6f) ? (100f - _K.z) / _vUnit.z : float.PositiveInfinity;
        _sMax = Mathf.Max(0.001f, Mathf.Min(sx, Mathf.Min(sy, sz)));

        _minTolerance = minTolerance;
        _maxTolerance = maxTolerance;

        _wAlong = _wCoef;
        _lineWidthMul = 1f;
        
        // 초깃값 반영(게이트, 게이지)
        for (int i = 0; i < _channelKnobs.Length; i++)
        {
            _channelKnobs[i].SetValueWithoutNotify(0f);
        }
        HandleKnobChanged();
        
        if (_answer != null)
        {
            var c = _answer.color;
            c.a = 0f;
            _answer.color = c;
            _answer.SetAllChannelGates(1f);
            _answer.enabled = false;
        }
    }

    // ========== 외부 제어 ==========
    public void SetChannelInteractable(int ch, bool on)
    {
        if (_channelKnobs == null || ch < 0 || ch >= _channelKnobs.Length)
            return;
        
        var knob = _channelKnobs[ch];
        if (knob != null)
        {
            knob.SetInteractable(on);
        }
    }

    public void SetAllInteractable(bool on)
    {
        if (_channelKnobs == null)
            return;
        
        for (int i = 0; i < _channelKnobs.Length; i++)
        {
            SetChannelInteractable(i, on);
        }

        if (on)
        {
            _store.SyncAnswers();
        }
        else
        {
            StopAllActivatePulses();
        }
    }

    // ========== 핵심: 노브 → 게이트/정합도 ==========
    void HandleKnobChanged()
    {
        float x = GetKnob01(0);
        float y = GetKnob01(1);
        float z = GetKnob01(2);

        _current.SetChannelGates(new[] { x, y, z });

        float gauge01 = ComputeConsistency(new Vector3(x, y, z));
        
        _gaugeBaseStep = Mathf.RoundToInt(Mathf.Clamp01(gauge01) * _consistencyGaugeSteps);
        _consistencyGauge.value = _gaugeBaseStep;

        UpdateHintByGauge(gauge01);

        bool solvedNow = _gaugeBaseStep == _consistencyGaugeSteps;
        if (solvedNow != _solved)
        {
            _solved = solvedNow;
            _gaugeNoiseEnabled = !solvedNow;

            if (solvedNow)
            {
                int clamped = Mathf.Clamp(_gaugeBaseStep, (int)_consistencyGauge.minValue,
                    (int)_consistencyGauge.maxValue);
                _consistencyGauge.value = clamped;

                _answer.SetAllChannelGates(_wCoef);
                SetAllInteractable(false);
            }

            _signalAntennaController.Phase2Solved(solvedNow);
        }
    }
    
    void StartActivatePulse(int ch)
    {
        if (_channelActivateImages == null || ch < 0 || ch >= _channelActivateImages.Length)
            return;
        
        Image img = _channelActivateImages[ch];
        if (img == null)
            return;

        _activatePulse?[ch]?.Kill();
        
        Color c = img.color;
        c.a = _pulseMinA;
        img.color = c;

        _activatePulse[ch] = img
            .DOFade(_pulseMaxA, _pulseOneWaySec)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    void StopActivatePulse(int ch)
    {
        if (_channelActivateImages == null || ch < 0 || ch >= _channelActivateImages.Length)
            return;
        
        var img = _channelActivateImages[ch];
        if (img == null)
            return;

        _activatePulse?[ch]?.Kill();
        _activatePulse[ch] = null;

        Color c = img.color;
        c.a = 0f;
        img.color = c;
    }

    void StopAllActivatePulses()
    {
        if (_activatePulse == null)
            return;
        
        for (int i = 0; i < _activatePulse.Length; i++) StopActivatePulse(i);
    }
    
    float GetKnob01(int idx)
    {
        if (_channelKnobs == null || idx < 0 || idx >= _channelKnobs.Length || _channelKnobs[idx] == null)
            return 0f;
        return Mathf.Clamp01(_channelKnobs[idx].GetValue() / 100f);
    }

    /// <summary>
    /// F(x,y,z)와 정답 레이(K, vUnit)의 최근접점 G′를 구해 거리/원뿔 반경으로 정합도(0~1)를 반환.
    /// </summary>
    float ComputeConsistency(Vector3 F)
    {
        if (_vLen <= 0f)
            return 0f;

        Vector3 fPercent = F * 100f;
        Vector3 w = fPercent - _K;
        float s   = Mathf.Max(0f, Vector3.Dot(w, _vUnit));
        Vector3 Gp = _K + s * _vUnit;
        
        _wCoef = (s + _kOLength) / (_vLen * 280f);

        float d = (fPercent - Gp).magnitude;
        
        float tMax01 = Mathf.Clamp01(s / _sMax);
        float Dp = Mathf.Lerp(_minTolerance, _maxTolerance, tMax01);

        _distanceRatio = (Dp <= 0f) ? 0f : Mathf.Clamp01(1f - (d / Dp));
        return _distanceRatio; 
    }
    
    void UpdateHintByGauge(float gauge01)
    {
        if (_answer == null)
            return;

        if (gauge01 <= _hintStartGauge)
        {
            _hintTw?.Kill();
            if (_answer.enabled)
            {
                if (_hintFadeSec > 0f)
                {
                    _hintTw = DOTween.To(
                        () => _answer.color,
                        c => _answer.color = c,
                        new Color(_answer.color.r, _answer.color.g, _answer.color.b, 0f),
                        _hintFadeSec
                    ).OnComplete(() => _answer.enabled = false).SetUpdate(true);
                }
                else
                {
                    var c0 = _answer.color; c0.a = 0f; _answer.color = c0;
                    _answer.enabled = false;
                }
            }
            return;
        }

        float targetA = _hintNearAlpha;
        if (gauge01 <= _hintEndGauge)
        {
            float t = Mathf.InverseLerp(_hintStartGauge, _hintEndGauge, gauge01);
            targetA = Mathf.Lerp(_hintFarAlpha, _hintNearAlpha, t);
            float wAlpha = Mathf.Pow(t, 2f);
            _wAlong = Mathf.Lerp(_wAlong, 1f, 1f - wAlpha);
            _lineWidthMul = Mathf.Lerp(_lineWidthMul, 1f, 1f - wAlpha);
        }
        else
        {
            float t = Mathf.InverseLerp(_hintEndGauge, 0f, gauge01);
            float wAlpha = Mathf.Pow(t, 2f);
            _wAlong = Mathf.Lerp(_wAlong, _wCoef, wAlpha);
            _lineWidthMul = Mathf.Lerp(_lineWidthMul, 2f, wAlpha);
        }

        _answer.SetAllChannelGates(_wAlong);
        _answer.LineMul = _lineWidthMul;
        if (!_answer.enabled) _answer.enabled = true;

        _hintTw?.Kill();
        if (_hintFadeSec > 0f)
        {
            _hintTw = DOTween.To(
                () => _answer.color,
                c => _answer.color = c,
                new Color(_answer.color.r, _answer.color.g, _answer.color.b, targetA),
                _hintFadeSec
            ).SetUpdate(true);
        }
        else
        {
            var c = _answer.color; c.a = targetA; _answer.color = c;
        }
    }
}