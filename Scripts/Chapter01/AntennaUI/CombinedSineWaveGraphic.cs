using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

[DisallowMultipleComponent]
public class CombinedSineWaveGraphic : MaskableGraphic
{
    public enum SourceKind { Current, Answers }
    public enum AmplitudeScaleMode { Manual, AutoNormalize }

    [Header("Data")]
    [SerializeField] private SignalWaveData waveStore;
    [SerializeField] private SourceKind source = SourceKind.Current;

    [Header("Runtime Amplitudes (Gate 0~1 per channel)")]
    [Tooltip("각 채널의 런타임 진폭 게이트 값(0~1). waveStore의 원진폭에 곱해져 최종 진폭을 만든다.\n비워두면 waveStore 길이에 맞춰 자동으로 1로 채움.")]
#if ODIN_INSPECTOR
    [PropertyRange(0f, 2f)]
#endif
    [SerializeField] private List<float> channelAmps02 = new(); // 0..1

    [Header("Rendering")]
    [Min(0.01f)] public float lineWidthPx = 1f;
    public AmplitudeScaleMode amplitudeScaleMode = AmplitudeScaleMode.AutoNormalize;
    [Min(0.01f)] public float amplitudeScale = 1f;
    [Tooltip("AA 최소 폭(px). 0.75~1.5 권장")]
    [Min(0.1f)] public float aaMinPx = 0.75f;

    [Header("Animation")]
    public bool animateScrolling = false;

    // — Shader property IDs —
    static readonly int ID_WaveParams = Shader.PropertyToID("_WaveParams");
    static readonly int ID_WaveCount  = Shader.PropertyToID("_WaveCount");
    static readonly int ID_AmpScale   = Shader.PropertyToID("_AmpScale");
    static readonly int ID_LineWidth  = Shader.PropertyToID("_LineWidthPx");
    static readonly int ID_Phase      = Shader.PropertyToID("_Phase");
    static readonly int ID_RectSize   = Shader.PropertyToID("_RectSize");
    static readonly int ID_AaMinPx    = Shader.PropertyToID("_AaMinPx");

    // 내부 버퍼(최대 8채널로 셰이더에 전송)
    readonly Vector4[] _waveParams = new Vector4[8];
    float _phase;
    
    // Glitch runtime state
    Sequence _glitchSeq;
    
    bool  _glitching;
    float _glitchT, _glitchDur;
    float _glitchElapsed;
    float _gAmpMul = 1f;          // 1 → (0.3~0.75) → 1
    float _gLineMul = 1f;         // 1 → (1.1~2.6) → 1
    float _gPhaseAmpCur = 0f;     // 0 → (0.12~1.1) → 0
    float _gPhaseFreq = 0f;       // 18~55 rad/s
    float _gPhaseSeed = 0f;       // random
    
    bool  _noiseActive;
    float _gNoiseAmp;         // 노이즈파 진폭
    float _gNoiseFreq;        // 노이즈파 주파수(짧은 파장)
    float _gNoisePhase0;      // 시작 위상
    float _gNoiseOmega;       // 각속도(rad/s)
    
    // 셰이더로 넘길 최종 보정값
    public float AmpMul   => _gAmpMul;
    public float LineMul { get => _gLineMul; set => _gLineMul = value;  }

    float PhaseAdd => (_glitching && _gPhaseAmpCur > 0f)
        ? Mathf.Sin(_glitchElapsed * _gPhaseFreq + _gPhaseSeed) * _gPhaseAmpCur
        : 0f;
    
    float Envelope()
    {
        float t01 = Mathf.Clamp(_glitchT / Mathf.Max(0.001f, _glitchDur), 0f, 2f);
        return 1f - t01 * t01;
    }

    protected override void Awake()
    {
        base.Awake();
        if (material == null)
            material = new Material(Shader.Find("UI/CombinedSineWave (URP)"));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        SyncAmplitudeListLength();
        SetAllDirty();
        UpdateMaterial();
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        
        _glitchSeq?.Kill(); _glitchSeq = null;
        _glitching = false;
    }
    
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        _glitchSeq?.Kill(); _glitchSeq = null;
    }

    void OnValidate()
    {
        // 에디터에서 값 바뀔 때도 길이/범위 동기화
        ClampAmplitudeList01();
        SyncAmplitudeListLength();
    }

    void Update()
    {
        if (animateScrolling)
            _phase = WaveConstants.CombinePhase;

        if (_glitching)
            _glitchElapsed += Time.unscaledDeltaTime;

        if (isActiveAndEnabled)
            SetMaterialDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateMaterial(); // 사이즈 바뀌면 라인폭/AA 보정
    }

    // Quad 한 장
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();

        var v0 = UIVertex.simpleVert; v0.color = color; v0.position = new Vector2(r.xMin, r.yMin); v0.uv0 = new Vector2(0,0);
        var v1 = UIVertex.simpleVert; v1.color = color; v1.position = new Vector2(r.xMin, r.yMax); v1.uv0 = new Vector2(0,1);
        var v2 = UIVertex.simpleVert; v2.color = color; v2.position = new Vector2(r.xMax, r.yMax); v2.uv0 = new Vector2(1,1);
        var v3 = UIVertex.simpleVert; v3.color = color; v3.position = new Vector2(r.xMax, r.yMin); v3.uv0 = new Vector2(1,0);
        vh.AddUIVertexQuad(new[] { v0, v1, v2, v3 });
    }

    public override void SetMaterialDirty()
    {
        base.SetMaterialDirty();
        UpdateMaterial();
    }

    public override void Rebuild(CanvasUpdate update)
    {
        base.Rebuild(update);
        if (update == CanvasUpdate.PreRender)
            UpdateMaterial();
    }

    protected override void UpdateMaterial()
    {
        base.UpdateMaterial();
        if (!IsActive() || materialForRendering == null || waveStore == null)
            return;

        var waves = (source == SourceKind.Current) ? waveStore.Current : waveStore.Answers;
        int total = waves?.Length ?? 0;
        if (total <= 0) return;

        // channelAmps01 길이 보정
        SyncAmplitudeListLength();

        // waveStore의 F/φ는 그대로, A는 A_final = A_store * gate(0~1)
        // gate가 0이면 사실상 해당 채널은 제외된 것과 동일.
        int n = Mathf.Min(total, _waveParams.Length);
        for (int i = 0; i < n; i++)
        {
            var w = waves[i];
            float gate = Mathf.Clamp(channelAmps02[i], 0f, 2f);
            
            // 핵심: Current일 때는 waveStore 진폭을 무시하고 고정값 사용
            float baseA = (source == SourceKind.Current) ? WaveConstants.AmplitudeMax : Mathf.Max(0f, w.AmplitudeNoRender);
            float A = baseA * gate;
            _waveParams[i] = new Vector4(w.Frequency, A, w.DisplacementNoRender, 0f);
        }
        if (_glitching && _noiseActive && _gNoiseAmp > 0f && n < _waveParams.Length)
        {
            float phaseNow = _gNoisePhase0 + _glitchElapsed * _gNoiseOmega;
            _waveParams[n] = new Vector4(_gNoiseFreq, _gNoiseAmp, phaseNow, 0f);
            n++;
        }
        for (int i = n; i < _waveParams.Length; i++) _waveParams[i] = Vector4.zero;

        // AutoNormalize는 gate 적용 후의 유효 진폭 합을 기준으로 스케일 결정
        float ampScale = amplitudeScale;
        if (amplitudeScaleMode == AmplitudeScaleMode.AutoNormalize)
        {
            float sum = 0f; for (int i = 0; i < n; i++) sum += Mathf.Max(0f, _waveParams[i].y);
            ampScale = (sum <= 1f) ? 1f : (1f / sum);
        }
        ampScale *= AmpMul;

        Material mat = materialForRendering;
        mat.SetVectorArray(ID_WaveParams, _waveParams);
        mat.SetFloat(ID_WaveCount, n);
        mat.SetFloat(ID_AmpScale, ampScale);
        mat.SetFloat(ID_LineWidth, Mathf.Max(0.01f, lineWidthPx * LineMul));
        mat.SetFloat(ID_Phase, (animateScrolling ? _phase : 0f) + PhaseAdd);
        mat.SetFloat(ID_AaMinPx, Mathf.Max(0.1f, aaMinPx));

        Rect pixelRect = GetPixelAdjustedRect();
        Vector2 size = pixelRect.size;
        mat.SetVector(ID_RectSize, new Vector2(Mathf.Max(1, size.x), Mathf.Max(1, size.y)));

        canvasRenderer.SetMaterial(mat, 0);
    }

    // ===== Helpers =====
    void SyncAmplitudeListLength()
    {
        if (waveStore == null) return;
        int target = ((source == SourceKind.Current) ? waveStore.Current : waveStore.Answers)?.Length ?? 0;
        if (target <= 0) return;

        if (channelAmps02 == null) channelAmps02 = new List<float>(target);

        // 확장: 새로 생기는 채널은 1로 기본값(원래 동작과 동일)
        while (channelAmps02.Count < target) channelAmps02.Add(1f);
        // 축소: 초과분 제거
        if (channelAmps02.Count > target) channelAmps02.RemoveRange(target, channelAmps02.Count - target);

        ClampAmplitudeList01();
    }

    void ClampAmplitudeList01()
    {
        if (channelAmps02 == null) return;
        for (int i = 0; i < channelAmps02.Count; i++)
            channelAmps02[i] = Mathf.Clamp(channelAmps02[i], 0f, 2f);
    }

    // ===== 외부 제어용 API =====
    public void SetWaveStore(SignalWaveData store){ waveStore = store; SetMaterialDirty(); }
    public void SetSource(SourceKind kind){ source = kind; SetMaterialDirty(); }

    /// <summary>개별 채널의 게이트 값을 설정(0~1)</summary>
    public void SetChannelGate(int index, float gate02)
    {
        if (waveStore == null) return;
        var waves = (source == SourceKind.Current) ? waveStore.Current : waveStore.Answers;
        int total = waves?.Length ?? 0;
        if (index < 0 || index >= total) return;

        SyncAmplitudeListLength();
        channelAmps02[index] = Mathf.Clamp(gate02, 0f, 2f);
        SetMaterialDirty();
    }

    /// <summary>모든 채널을 동일한 게이트 값으로 설정</summary>
    public void SetAllChannelGates(float gate02)
    {
        SyncAmplitudeListLength();
        gate02 = Mathf.Clamp(gate02, 0f, 2f);
        for (int i = 0; i < channelAmps02.Count; i++) channelAmps02[i] = gate02;
        SetMaterialDirty();
    }

    /// <summary>외부에서 일괄 목록을 주입(길이는 자동 보정)</summary>
    public void SetChannelGates(IReadOnlyList<float> gates02)
    {
        if (gates02 == null) return;
        SyncAmplitudeListLength();
        int n = Mathf.Min(channelAmps02.Count, gates02.Count);
        for (int i = 0; i < n; i++) channelAmps02[i] = Mathf.Clamp(gates02[i], 0f, 2f);
        SetMaterialDirty();
    }

    /// <summary>현재 게이트 목록을 스냅샷(복사본 반환)</summary>
    public List<float> SnapshotChannelGates() => new List<float>(channelAmps02);
    
    /// <summary>
    /// 합성파에 짧은 원샷 글리치를 준다. intensity 0~1, duration 초.
    /// 진폭 ↓, 라인폭 ↑, 위상 소동(sin)으로 불안정 연출.
    /// </summary>
    public void GlitchOneShot(float intensity = 0.8f, float duration = 0.35f)
    {
        intensity = Mathf.Clamp01(intensity);
        duration  = Mathf.Max(0.1f, duration);
        float up   = duration * 0.45f;   // 올라가는 구간
        float down = duration - up;      // 복귀 구간

        // 오버슛: 1 → (1.15~1.80) → 1   (전역 스케일을 위로 튀게)
        float targetAmpMul   = 1f + Mathf.Lerp(0.15f, 0.80f, intensity);
        // 라인폭은 살짝만 키워서 번쩍이는 느낌
        float targetLineMul  = 1f + Mathf.Lerp(0.05f, 0.35f, intensity);

        // 위상 흔들림(글로벌)
        float targetPhaseAmp = Mathf.Lerp(0.10f, 0.60f, intensity);
        _gPhaseFreq = Mathf.Lerp(14f, 42f, intensity);
        _gPhaseSeed = Random.value * Mathf.PI * 2f;

        // 노이즈파(짧은 파장 하나)
        // 주파수: 화면에 ‘고주파’ 느낌 (기존 파들보다 훨씬 빠르게)
        _gNoiseFreq  = Mathf.Lerp(12f, 40f, Random.value) * Mathf.Lerp(1.0f, 2.0f, intensity);
        _gNoiseOmega = Mathf.Lerp(8f, 32f, intensity);        // 시간에 따라 위상 전진
        _gNoisePhase0= Random.value * Mathf.PI * 2f;
        float noiseAmpMax = Mathf.Lerp(0.04f, 0.20f, intensity); // 절대 진폭(정규화 전)
        _noiseActive = true;

        // 상태 초기화
        _glitching     = true;
        _glitchElapsed = 0f;

        _glitchSeq?.Kill();
        _glitchSeq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable | LinkBehaviour.KillOnDestroy)

            // 오버슛으로 치솟기
            .Append(DOVirtual.Float(_gAmpMul,   targetAmpMul,   up,   v => { _gAmpMul = v;   SetMaterialDirty(); })
                .SetEase(Ease.OutCubic))
            .Join(  DOVirtual.Float(_gLineMul,  targetLineMul,  up,   v => { _gLineMul = v; }))
            .Join(  DOVirtual.Float(_gPhaseAmpCur, targetPhaseAmp, up, v => { _gPhaseAmpCur = v; }))
            .Join(  DOVirtual.Float(_gNoiseAmp, noiseAmpMax,   up*0.9f, v => { _gNoiseAmp = v; })) // 노이즈 등장

            // 원래대로 부드럽게 복귀
            .Append(DOVirtual.Float(targetAmpMul, 1f, down, v => { _gAmpMul = v; SetMaterialDirty(); })
                .SetEase(Ease.InOutCubic))
            .Join(  DOVirtual.Float(targetLineMul, 1f, down, v => { _gLineMul = v; }))
            .Join(  DOVirtual.Float(targetPhaseAmp, 0f, down, v => { _gPhaseAmpCur = v; }))
            .Join(  DOVirtual.Float(noiseAmpMax, 0f, down*0.8f, v => { _gNoiseAmp = v; }))         // 노이즈 사라짐

            .OnComplete(() =>
            {
                _gAmpMul = 1f; _gLineMul = 1f; _gPhaseAmpCur = 0f;
                _gNoiseAmp = 0f; _noiseActive = false;
                _glitching = false;
                SetMaterialDirty();
            });
    }
}
