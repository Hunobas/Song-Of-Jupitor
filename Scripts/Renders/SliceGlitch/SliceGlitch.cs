using UnityEngine;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class SliceGlitch : MonoBehaviour
{
    [Header("Renderer Data (FullScreen Feature)")]
    [SerializeField] private ScriptableRendererData _rendererData;

    [Header("UI Slice Glitch Material")]
    [SerializeField] private Material _uiSliceGlitchMat;

    [Header("Slice Params (shared)")]
    [Range(0f, 1f)] [SerializeField] private float _rectAmount = 0.25f;
    [SerializeField] private float _rectMinSizePx = 12f;
    [SerializeField] private float _rectMaxSizePx = 96f;
    [SerializeField] private float _rectMaxShiftPx = 32f;
    [Range(1, 4)]  [SerializeField] private int   _iterations = 3;
    [Range(0f, 1f)] [SerializeField] private float _aniso = 1f;

    [Header("Glitch Behavior")]
    [Range(0f, 1f)] [SerializeField] private float _glitchProbability = 0.2f;
    [SerializeField] private float _glitchInterval = 0.2f;
    [SerializeField] private int _maxSeed = 63435;

    private float _fullScreenIntensityCurrent = 0f;
    private float _uiIntensityCurrent = 0f;

    private Sequence _fullScreenSeq;
    private Sequence _uiSeq;

    private void Awake()
    {
        ApplyParams_Shader();
        ApplyParams_Material();
    }

    // -------------------- 초기 적용 --------------------
    private void ApplyParams_Shader()
    {
        if (_rendererData == null) return;

        foreach (var feature in _rendererData.rendererFeatures)
        {
            if (feature is SliceGlitchFeature sliceFeature)
            {
                var s = sliceFeature.settings;
                s.RectAmount     = _rectAmount;
                s.RectMinSizePx  = _rectMinSizePx;
                s.RectMaxSizePx  = _rectMaxSizePx;
                s.RectMaxShiftPx = _rectMaxShiftPx;
                s.Iterations     = Mathf.Clamp(_iterations, 1, 4);
                s.Aniso          = Mathf.Clamp01(_aniso);
            }
        }
    }

    private void ApplyParams_Material()
    {
        if (_uiSliceGlitchMat == null) return;

        _uiSliceGlitchMat.SetFloat("_RectAmount",     _rectAmount);
        _uiSliceGlitchMat.SetFloat("_RectMinSizePx",  _rectMinSizePx);
        _uiSliceGlitchMat.SetFloat("_RectMaxSizePx",  _rectMaxSizePx);
        _uiSliceGlitchMat.SetFloat("_RectMaxShiftPx", _rectMaxShiftPx);
        _uiSliceGlitchMat.SetFloat("_Iterations",     Mathf.Clamp(_iterations, 1, 4));
        _uiSliceGlitchMat.SetFloat("_Aniso",          Mathf.Clamp01(_aniso));
    }

    // ==================================================
    // ==============  새로 추가된 메서드  ===============
    // ==================================================

    /// <summary>
    /// 슬라이스 파라미터 재설정(풀스크린 셰이더 & UI 머티리얼 동기화)
    /// </summary>
    public void ConfigureSliceParams(
        float rectAmount, float rectMinSizePx, float rectMaxSizePx,
        float rectMaxShiftPx, int iterations, float aniso)
    {
        _rectAmount     = Mathf.Clamp01(rectAmount);
        _rectMinSizePx  = Mathf.Max(1f, rectMinSizePx);
        _rectMaxSizePx  = Mathf.Max(_rectMinSizePx, rectMaxSizePx);
        _rectMaxShiftPx = Mathf.Max(0f, rectMaxShiftPx);
        _iterations     = Mathf.Clamp(iterations, 1, 4);
        _aniso          = Mathf.Clamp01(aniso);

        ApplyParams_Shader();
        ApplyParams_Material();
    }

    /// <summary>
    /// 글리치 동작 재설정(발생 확률/주기 + 두 채널의 강도)
    /// - 실행 중이면 즉시 반영.
    /// </summary>
    public void ConfigureGlitchBehavior(
        float glitchProbability, float glitchInterval,
        float fullScreenIntensity, float uiIntensity)
    {
        _glitchProbability         = Mathf.Clamp01(glitchProbability);
        _glitchInterval            = Mathf.Max(0.01f, glitchInterval);
        _fullScreenIntensityCurrent = Mathf.Clamp01(fullScreenIntensity);
        _uiIntensityCurrent         = Mathf.Clamp01(uiIntensity);

        // 실행 중이라면 시퀀스를 새 주기로 재빌드하여 즉시 반영
        if (_fullScreenSeq != null && _fullScreenSeq.IsActive())
        {
            StartInfiniteFullScreenGlitch(_fullScreenIntensityCurrent); // 내부에서 재시작
        }
        if (_uiSeq != null && _uiSeq.IsActive())
        {
            StartInfiniteUIGlitch(_uiIntensityCurrent); // 내부에서 재시작
        }
    }

    public void StartInfiniteFullScreenGlitch(float intensity)
    {
        _fullScreenIntensityCurrent = Mathf.Clamp01(intensity);
        StopInfiniteFullScreenGlitch();

        _fullScreenSeq = DOTween.Sequence()
            .SetLink(gameObject)
            .SetLoops(-1, LoopType.Restart);

        _fullScreenSeq.AppendCallback(() =>
        {
            if (_rendererData == null) return;

            foreach (var feature in _rendererData.rendererFeatures)
            {
                if (feature is SliceGlitchFeature sliceFeature)
                {
                    var s = sliceFeature.settings;

                    if (Random.value < _glitchProbability)
                    {
                        s.Intensity = _fullScreenIntensityCurrent; // 필드 참조
                        s.Seed      = Random.Range(0, _maxSeed);
                    }
                    else
                    {
                        s.Intensity = 0f;
                    }
                }
            }
        });

        _fullScreenSeq.AppendInterval(_glitchInterval);
    }

    public void StopInfiniteFullScreenGlitch()
    {
        _fullScreenSeq?.Kill();
        _fullScreenSeq = null;
        
        if (_rendererData != null)
        {
            foreach (var feature in _rendererData.rendererFeatures)
            {
                if (feature is SliceGlitchFeature sliceFeature)
                {
                    sliceFeature.settings.Intensity = 0f;
                    sliceFeature.settings.Seed = 12345;
                }
            }
        }
    }

    public void StartInfiniteUIGlitch(float intensity)
    {
        _uiIntensityCurrent = Mathf.Clamp01(intensity);
        StopInfiniteUIGlitch();

        if (_uiSliceGlitchMat == null) return;

        // 첫 프레임 상태를 보장
        _uiSliceGlitchMat.SetFloat("_Intensity", 0f);

        _uiSeq = DOTween.Sequence()
            .SetLink(gameObject)
            .SetLoops(-1, LoopType.Restart);

        _uiSeq.AppendCallback(() =>
        {
            if (_uiSliceGlitchMat == null) return;

            if (Random.value < _glitchProbability)
            {
                _uiSliceGlitchMat.SetFloat("_Intensity", _uiIntensityCurrent);
                _uiSliceGlitchMat.SetInt("_Seed", Random.Range(0, _maxSeed));
            }
            else
            {
                _uiSliceGlitchMat.SetFloat("_Intensity", 0f);
            }
        });

        _uiSeq.AppendInterval(_glitchInterval);
    }

    public void StopInfiniteUIGlitch()
    {
        _uiSeq?.Kill();
        _uiSeq = null;
        
        if (_uiSliceGlitchMat != null)
        {
            _uiSliceGlitchMat.SetFloat("_Intensity", 0f);
            _uiSliceGlitchMat.SetInt("_Seed",12345);
        }
    }

    private void OnDestroy()
    {
        StopInfiniteFullScreenGlitch();
        StopInfiniteUIGlitch();
    }
}
