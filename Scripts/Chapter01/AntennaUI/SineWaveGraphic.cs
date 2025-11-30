using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

[DisallowMultipleComponent]
public class SineWaveGraphic : Graphic
{
    // ================= Wave Parameters =================
    [SerializeField, Header("Wave Parameters")]
    [Range(WaveConstants.FrequencyMin, WaveConstants.FrequencyMax)]
    public float Frequency = 1f;

    [Range(WaveConstants.AmplitudeMin, WaveConstants.AmplitudeMax)]
    public float Amplitude = 0.5f;

    [Range(WaveConstants.DisplacementMin, WaveConstants.DisplacementMax)]
    public float BaseDisplacement = 0f;

    [SerializeField, Header("Rendering")]
    [Range(3, 200)] public int Resolution = 200;
    [Range(0.01f, 8f)] public float LineWidth = 0.5f;

    [SerializeField, Header("Scrolling Animation")]
    public bool AnimateScrolling = false;
    [ShowIf(nameof(AnimateScrolling))] public float ScrollSpeed = 1f; // [rad/sec]

    private float _animationDisplacement; // 수동 업데이트 (라디안)

    // ================= Glitch System =================
    [System.Serializable]
    public struct GlitchTuning
    {
        [Header("Gap (time between glitches)")]
        public float gapAtMin;    // intensity=0 평균 간격
        public float gapAtMax;    // intensity=1 평균 간격
        public Vector2 gapRandomMul; // (0.4, 1.8)

        [Header("Duration (one glitch total duration)")]
        public float durAtMin; public float durAtMax;
        public Vector2 durRandomMul; // (0.5, 1.6)

        [Header("Pattern Probabilities")]
        [Range(0,1)] public float pSingle;
        [Range(0,1)] public float pDouble;
        [Range(0,1)] public float pStutter;

        [Header("Misc")]
        public float noiseStep;  // 0.73
    }

    [SerializeField, InlineProperty, Header("Glitch Tuning")]
    private GlitchTuning _glitch = new GlitchTuning
    {
        gapAtMin = 1.4f, gapAtMax = 0.03f, gapRandomMul = new Vector2(0.4f, 1.8f),
        durAtMin = 0.1f, durAtMax = 0.25f, durRandomMul = new Vector2(0.5f, 1.6f),
        pSingle = 0.6f, pDouble = 0.25f, pStutter = 0.15f,
        noiseStep = 0.73f
    };

    // 글리치 파라미터
    float _glitchMul      = 1f;  // 진폭 계수 (0~1)
    float _glitchScrollMul = 1f; // 스크롤 속도 배율
    float _glitchDispAdd   = 0f; // 추가 위상 (라디안)

    bool  _glitching;
    float _glitchIntensity;   // 0~1
    float _noiseTime;
    Sequence _sched;          // 스케줄러 시퀀스
    string GlitchTweenId => $"SWG_Glitch_{GetInstanceID()}";
    
#if UNITY_EDITOR
    // 🔹 이전 프레임 값 캐싱 (에디터용)
    private float _prevFreq;
    private float _prevAmp;
    private float _prevDisp;
#endif

    // 유도 프로퍼티
    float EffectiveAmplitude   => Amplitude * _glitchMul;
    float EffectiveScrollSpeed => Mathf.Max(0f, ScrollSpeed) * _glitchScrollMul;
    float TotalDisplacement    => (BaseDisplacement + _animationDisplacement + _glitchDispAdd) % (2*Mathf.PI);

    // ================= Mesh =================
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Resolution < 2) return;

        Rect rect = GetPixelAdjustedRect();
        float width = rect.width;
        float height = rect.height;

        Vector2[] points = new Vector2[Resolution];
        for (int i = 0; i < Resolution; i++)
        {
            float x = (float)i / (Resolution - 1);
            float sineValue = Mathf.Sin(2 * Mathf.PI * Frequency * x + TotalDisplacement);
            float y = 0.5f + (EffectiveAmplitude * sineValue);
            y = Mathf.Clamp01(y);

            points[i] = new Vector2(rect.xMin + x * width, rect.yMin + y * height);
        }
        DrawSineWave(vh, points, LineWidth);
    }

    private void DrawSineWave(VertexHelper vh, Vector2[] points, float width)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 start = points[i];
            Vector2 end   = points[i + 1];

            Vector2 dir  = (end - start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);

            UIVertex a = CreateVertex(start - perp);
            UIVertex b = CreateVertex(start + perp);
            UIVertex c = CreateVertex(end + perp);
            UIVertex d = CreateVertex(end - perp);

            vh.AddUIVertexQuad(new[] { a, b, c, d });
        }
    }

    private UIVertex CreateVertex(Vector2 position)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = position;
        v.color    = color;
        return v;
    }
    
#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        Frequency        = Mathf.Clamp(Frequency, WaveConstants.FrequencyMin, WaveConstants.FrequencyMax);
        Amplitude        = Mathf.Clamp(Amplitude, WaveConstants.AmplitudeMin, WaveConstants.AmplitudeMax);
        BaseDisplacement = Mathf.Clamp(BaseDisplacement, WaveConstants.DisplacementMin, WaveConstants.DisplacementMax);
        Resolution       = Mathf.Clamp(Resolution, 10, 1000);
        LineWidth        = Mathf.Max(0.01f, LineWidth);

        // 🔹 값이 변경됐을 때만 SetVerticesDirty()
        if (!Mathf.Approximately(Frequency, _prevFreq) ||
            !Mathf.Approximately(Amplitude, _prevAmp) ||
            !Mathf.Approximately(BaseDisplacement, _prevDisp))
        {
            this?.SetVerticesDirty();
            _prevFreq = Frequency;
            _prevAmp = Amplitude;
            _prevDisp = BaseDisplacement;
        }
    }
#endif


    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!Mathf.Approximately(BaseDisplacement, _prevDisp))
            {
                this?.SetVerticesDirty();
                _prevDisp = BaseDisplacement;
            }
            return;
        }
#endif

        if (!AnimateScrolling) return;

        float d = EffectiveScrollSpeed * Time.deltaTime;
        if (d != 0f)
        {
            _animationDisplacement = (_animationDisplacement + d) % (2 * Mathf.PI);
            this?.SetVerticesDirty();
        }
    }

    private void OnDisable()
    {
        base.OnDisable();
        
        _glitching = false;
        KillGlitchTweens();
    }

    private void OnDestroy()
    {
        KillGlitchTweens();
    }

    // ================= External Wave Control =================
    public void UpdateWave(float newFrequency, float newAmplitude, float newDisplacement)
    {
        Frequency = Mathf.Clamp(newFrequency, WaveConstants.FrequencyMin, WaveConstants.FrequencyMax);
        Amplitude = Mathf.Clamp(newAmplitude, WaveConstants.AmplitudeMin,   WaveConstants.AmplitudeMax);
        SetBaseDisplacement(newDisplacement);
        this?.SetVerticesDirty();
    }

    public void SetFrequency(float value)      { Frequency = Mathf.Max(0.1f, value); this?.SetVerticesDirty(); }
    public void SetAmplitude(float value)      { Amplitude = Mathf.Clamp01(value);   this?.SetVerticesDirty(); }
    public void SetBaseDisplacement(float val) { BaseDisplacement = val % (2*Mathf.PI); this?.SetVerticesDirty(); }

    // ===================== PUBLIC API : Glitch =====================
    /// <summary> intensity: 0~1. 0이면 글리치 없음, 1이면 매우 잦고 짧게/깊게 깜빡임 + 속도/위상 흔들림. </summary>
    public void StartGlitch(float intensity)
    {
        _glitchIntensity = Mathf.Clamp01(intensity);
        if (_glitchIntensity <= 0f) { StopGlitch(); return; }

        _glitching = true;
        _noiseTime = Random.value * 10f;

        KillGlitchTweens();
        ScheduleNext();
    }

    public void StopGlitch()
    {
        _glitching = false;
        // 모든 글리치 파라미터 원복 (부드럽게)
        KillGlitchTweens();

        float t = 0.12f;
        DOVirtual.Float(_glitchMul, 1f, t, v => { if (this==null) return; _glitchMul = v; this?.SetVerticesDirty(); })
            .SetEase(Ease.OutQuad).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy);

        DOVirtual.Float(_glitchScrollMul, 1f, t, v => { if (this==null) return; _glitchScrollMul = v; })
            .SetEase(Ease.OutQuad).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy);

        DOVirtual.Float(_glitchDispAdd, 0f, t, v => { if (this==null) return; _glitchDispAdd = v; this?.SetVerticesDirty(); })
            .SetEase(Ease.OutQuad).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy);
    }

    // ===================== Glitch Scheduler =====================
    void ScheduleNext()
    {
        if (!_glitching || this == null) return;

        float noise = Mathf.PerlinNoise(_noiseTime, 0f); // 0~1
        _noiseTime += _glitch.noiseStep * (0.6f + 0.8f * Random.value);

        // Gap
        float baseGap = Mathf.Lerp(_glitch.gapAtMin, _glitch.gapAtMax, _glitchIntensity);
        float gap     = Mathf.Max(0.005f, baseGap * Random.Range(_glitch.gapRandomMul.x, _glitch.gapRandomMul.y) * Mathf.Clamp01(noise + 0.2f));

        // Duration(총)
        float baseDur = Mathf.Lerp(_glitch.durAtMin, _glitch.durAtMax, _glitchIntensity);
        float dur     = Mathf.Clamp(baseDur * Random.Range(_glitch.durRandomMul.x, _glitch.durRandomMul.y), 0.02f, 1.2f);

        // Pattern
        Pattern pattern = PickPattern();

        _sched = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy);
        _sched.AppendInterval(gap);

        switch (pattern)
        {
            case Pattern.SingleDip:
                _sched.Append(BuildBeat(dur));
                break;
            case Pattern.DoubleTap:
                _sched.Append(BuildBeat(dur * 0.55f));
                _sched.AppendInterval(Random.Range(0.02f, 0.12f));
                _sched.Append(BuildBeat(dur * 0.45f));
                break;
            case Pattern.Stutter:
                int n = Random.Range(3, 6);
                for (int i = 0; i < n; i++)
                {
                    float piece = dur / n * Random.Range(0.7f, 1.3f);
                    _sched.Append(BuildBeat(piece));
                    if (i < n - 1) _sched.AppendInterval(Random.Range(0.01f, 0.08f));
                }
                break;
        }

        _sched.OnComplete(() =>
        {
            if (_glitching && this != null && isActiveAndEnabled) ScheduleNext();
        });
    }

    enum Pattern { SingleDip, DoubleTap, Stutter }
    Pattern PickPattern()
    {
        float pS = _glitch.pSingle * (1f - 0.2f * _glitchIntensity);
        float pD = _glitch.pDouble * (0.8f + 0.4f * _glitchIntensity);
        float pT = _glitch.pStutter * (0.8f + 0.7f * _glitchIntensity);
        float sum = pS + pD + pT;

        float r = Random.value * sum;
        if (r < pS) return Pattern.SingleDip;
        if (r < pS + pD) return Pattern.DoubleTap;
        return Pattern.Stutter;
    }

    // “내렸다가 올리기” + 속도/위상 글리치 동시 구성
    Tween BuildBeat(float totalDur)
    {
        float half = Mathf.Max(0.01f, totalDur * Random.Range(0.45f, 0.65f));

        // 진폭 목표 (깊이): intensity 비례, 0쪽으로
        float depth = Mathf.Lerp(0.8f, 0.0f, Mathf.Pow(_glitchIntensity, 1.05f) * (0.7f + 0.3f*Random.value));

        // 스크롤 속도 배율: 1±range
        float speedRange = Mathf.Lerp(2.5f, 5f, _glitchIntensity);           // 강할수록 요동 큼
        float speedTo    = Mathf.Clamp(1f + Random.Range(-speedRange, speedRange), 0f, 6f);

        // 위상 튕김: [-phaseMax, +phaseMax]
        float phaseMax = Mathf.Lerp(0.15f, Mathf.PI * 1.8f, _glitchIntensity);  // 강할수록 큰 점프 허용
        float phaseTo  = Random.Range(-phaseMax, phaseMax);

        var s = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy);

        // 내려가기(진폭/속도/위상 동시)
        s.Append(DOVirtual.Float(_glitchMul, depth, half, v =>
        {
            if (this==null) return;
            _glitchMul = v; this?.SetVerticesDirty();
        }).SetEase(Ease.InBounce).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy));

        s.Join(DOVirtual.Float(_glitchScrollMul, speedTo, half, v =>
        {
            if (this==null) return;
            _glitchScrollMul = v;
        }).SetEase(Ease.InBounce).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy));

        s.Join(DOVirtual.Float(_glitchDispAdd, phaseTo, half, v =>
        {
            if (this==null) return;
            _glitchDispAdd = v; this?.SetVerticesDirty();
        }).SetEase(Ease.InBounce).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy));

        // 올라가기(원복)
        float remain = Mathf.Max(0.01f, totalDur - half);
        s.Append(DOVirtual.Float(depth, 1f, remain, v =>
        {
            if (this==null) return;
            _glitchMul = v; this?.SetVerticesDirty();
        }).SetEase(Ease.OutBounce).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy));

        s.Join(DOVirtual.Float(speedTo, 1f, remain, v =>
        {
            if (this==null) return;
            _glitchScrollMul = v;
        }).SetEase(Ease.InOutBack).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy));

        s.Join(DOVirtual.Float(phaseTo, 0f, remain, v =>
        {
            if (this==null) return;
            _glitchDispAdd = v; this?.SetVerticesDirty();
        }).SetEase(Ease.InOutBack).SetId(GlitchTweenId).SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy));

        return s;
    }

    void KillGlitchTweens()
    {
        _sched?.Kill(); _sched = null;
        DOTween.Kill(GlitchTweenId);
        DOTween.Kill(gameObject);
    }
}
