using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// AsciiImageUGUI
/// ----------------------------
/// GPU에서 입력 텍스처/스프라이트를 먼저 작은 RenderTexture로 다운샘플한 뒤,
/// AsyncGPUReadback으로 CPU로 픽셀 데이터를 비동기 수신하여 ASCII 아트로 변환하는 UGUI 컴포넌트입니다.
///
/// ● 지원 입력:
///   - Texture
///   - Sprite
///   - Image (UI, Sprite 기반)
///   - SpriteRenderer
///   - Animator (스프라이트/이미지 애니메이션)
///
/// ● 출력:
///   - TMP_Text 두 개 (포어그라운드 / 백그라운드)
///   - 포어그라운드는 컬러 태그(<color=#RRGGBB>)가 들어간 ASCII 문자열
///   - 백그라운드는 채우기 문자(bgFillChar)로 동일한 그리드를 채운 텍스트
///
/// ● 특징:
///   - AsyncGPUReadback 기반이므로 GPU → CPU 전송을 비동기로 처리
///   - 스프라이트 UV 영역만 잘라서 다운샘플 (아틀라스 대응)
///   - 문자 그라디언트 프리셋 / 커스텀 문자셋 지원
///   - 기본 컬러 유지 + 메인 컬러로 리컬러링 옵션
///   - HDR FaceColor 강도 제어(글로우 느낌) 지원
///
/// 전제 조건:
///   - Unity LTS (2020+ 권장, AsyncGPUReadback 지원 버전)
///   - TextMeshPro 패키지 임포트
///   - Hidden/Ascii/UVBlit 셰이더 에셋 포함
///   - Ascii TMP는 모노사이즈 폰트 사용 (ex. JetBrainsMono-Bold)
/// </summary>
[DisallowMultipleComponent]
public class AsciiImageUGUI : MonoBehaviour
{
    public enum AsciiGradientPreset
    {
        Custom = 0,
        ExtendedHigh,
        Alphabetic,
        Alphanumeric,
        Normal,
        Normal2,
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Inspector References
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("UGUI References")]
    [SerializeField] private TMP_Text asciiText;   // ASCII 본문을 출력할 메인 TMP 텍스트
    [SerializeField] private TMP_Text bgAsciiText; // 질감이 다른 배경 채우기용 TMP 텍스트 (null일 시 무시됨)

    [Header("Font Materials")]
    [Tooltip("메인 ASCII 텍스트에 사용할 폰트 머터리얼")]
    [SerializeField] private Material mainFontMaterial;
    [Tooltip("배경 ASCII 텍스트에 사용할 폰트 머터리얼")]
    [SerializeField] private Material bgFontMaterial;

    [Header("Input (Source)")]
    [Tooltip("정적 소스 텍스처 (Sprite를 사용하지 않을 때)")]
    [SerializeField] private Texture sourceTexture;                 // 정적 소스 텍스처
    [Tooltip("정적 소스 스프라이트 (단일 스프라이트를 ASCII화할 때)")]
    [SerializeField] private Sprite sourceSprite;                   // 정적 스프라이트
    [Tooltip("Animator가 스프라이트를 변경하는 UI Image")]
    [SerializeField] private Image sourceUIImage;                   // 애니메이터가 바꾸는 UI Image
    [Tooltip("Animator가 스프라이트를 변경하는 SpriteRenderer")]
    [SerializeField] private SpriteRenderer sourceSpriteRenderer;   // 애니메이터가 바꾸는 SpriteRenderer

    // ─────────────────────────────────────────────────────────────────────────────
    // ASCII Sampling Options
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("Ascii Options")]
    [Tooltip("가로 방향 ASCII 컬럼 수 (값이 클수록 디테일이 증가하지만 비용도 증가)")]
    [SerializeField, Min(8)]  private int   columns    = 160;
    [Tooltip("폰트 사이즈")]
    [SerializeField, Min(1f)] private float fontSize   = 2f;
    [Tooltip("문자 하나의 세로/가로 비율(h/w) (원본 이미지의 종횡비와 맞추면 왜곡이 줄어듦)")]
    [SerializeField, Range(0.5f, 5f)] private float charAspect = 2.0f;     // h/w
    [Tooltip("true일 경우 소스 이미지를 Y축으로 뒤집어서 샘플링")]
    [SerializeField] private bool  flipY = true;
    [Tooltip("true일 경우 밝기(L)을 반전하여 어두운 부분에 밝은 문자가, 밝은 부분에 어두운 문자가 배치")]
    [SerializeField] private bool  invertLuminance = false;
    [Tooltip("밝기 대비(contrast)를 조절. 1이 기본이며, 0에 가까울수록 단일 char 사용")]
    [SerializeField, Range(0f, 2f)] private float contrast = 1.0f;
    [Tooltip("밝은 영역을 공백 문자로 치환하는 정도 (0이면 항상 문자를 사용하고, 1이면 더 많은 공백)")]
    [SerializeField, Range(0f, 1f)] private float spaceDensity = 0f;
    [Tooltip("다운샘플링 시 초소형 픽셀 영역에서 샘플링하는 서브 샘플 개수 (값이 클수록 더 많은 픽셀을 대표)")]
    [SerializeField, Min(1)] private int superSamples = 1;

#if ODIN_INSPECTOR
    [HideIf("@bgAsciiText == null")]
#endif
    [Tooltip("배경 ASCII 텍스트(bgAsciiText)에 채워 넣을 문자. 포어그라운드와 대비되는 질감을 줄 수 있습니다.")]
    [SerializeField] private char bgFillChar = '-';

    // ─────────────────────────────────────────────────────────────────────────────
    // ASCII Gradient / Charset
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("ASCII Gradient")]
    [Tooltip("문자 프리셋")]
    [SerializeField] private AsciiGradientPreset gradientPreset = AsciiGradientPreset.ExtendedHigh;

    [Tooltip("밝기 반전")]
    [SerializeField] private bool  invertGradient = false;

    [TextArea(3, 6)]
#if ODIN_INSPECTOR
    [ShowIf("@gradientPreset == AsciiGradientPreset.Custom")]
#endif
    [Tooltip("밝기(어두운 → 밝은) 순서대로 나열한 사용자 정의 문자셋\n"
           + "가장 왼쪽 문자는 가장 어두운 영역에, 가장 오른쪽 문자는 가장 밝은 영역에 사용")]
    [SerializeField]
    private string charset =
        "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\"^'.  ";

    // ─────────────────────────────────────────────────────────────────────────────
    // Coloring / Intensity
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("Coloring")]
    [Tooltip("true일 경우 원본 색에서 mainTextColor로 리컬러링")]
    [SerializeField] private bool mainRecolor = true;

#if ODIN_INSPECTOR
    [ShowIf("@mainRecolor == true")]
#endif
    [ColorUsage(true, true)]
    [Tooltip("리컬러링 시 타겟이 되는 메인 컬러")]
    [SerializeField]
    private Color mainTextColor = Color.green;

    [Range(0f, 1f)]
#if ODIN_INSPECTOR
    [ShowIf("@mainRecolor == true")]
#endif
    [Tooltip("0이면 원본 컬러를 그대로 사용하고, 1이면 mainTextColor에 완전히 수렴")]
    [SerializeField]
    private float mainRecolorAmount = 1f;

#if ODIN_INSPECTOR
    [ShowIf("@mainRecolor == true")]
#endif
    [Tooltip("true일 경우 원본 밝기(L)를 유지한 채 컬러/채도만 변경")]
    [SerializeField]
    private bool keepLuminance = true;

    [Range(0f, 3f)]
    [Tooltip("메인 텍스트 폰트의 HDR 강도")]
    [SerializeField] private float mainIntensity = 1.5f;

#if ODIN_INSPECTOR
    [HideIf("@bgAsciiText == null")]
#endif
    [ColorUsage(true, true)]
    [Tooltip("배경 ASCII 텍스트 컬러")]
    [SerializeField] private Color bgColor = new(0.1f, 0.9f, 0.1f);

#if ODIN_INSPECTOR
    [HideIf("@bgAsciiText == null")]
#endif
    [Range(0f, 3f)]
    [Tooltip("배경 텍스트 폰트의 HDR 강도")]
    [SerializeField] private float bgIntensity = 1.0f;

    // ─────────────────────────────────────────────────────────────────────────────
    // Animation / Update
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("Animation")]
    [Tooltip("Animator가 할당된 경우, 이 컴포넌트는 Animator의 현재 프레임을 ASCII로 변환")]
    [SerializeField] private Animator sourceAnimator;
    
    [Tooltip("true일 경우 Animator.speed를 loopDurationSeconds 기반으로 자동 조정하여 루프 시간을 고정")]
    [SerializeField] private bool  useAnimatorSpeed = false;

#if ODIN_INSPECTOR
    [ShowIf("@useAnimatorSpeed == true")]
#endif
    [Tooltip("useAnimatorSpeed 활성 시, 이 시간에 맞춰 루프되도록 Animator.speed를 자동 조정")]
    [SerializeField] private float loopDurationSeconds = 2f;

    [Header("Update")]
    [Tooltip("true일 경우 매 프레임(또는 targetFps 기준)마다 실시간으로 ASCII를 갱신")]
    [SerializeField] private bool  realtime = true;

#if ODIN_INSPECTOR
    [ShowIf("@realtime == true")]
#endif
    [Tooltip("realtime이 true일 때 초당 ASCII 업데이트 횟수(FPS)")]
    [SerializeField, Range(1, 60)] private int targetFps = 15;

    // ─────────────────────────────────────────────────────────────────────────────
    // Runtime State
    // ─────────────────────────────────────────────────────────────────────────────

    // 소스 텍스처/스프라이트 메타
    Texture _srcTex;
    Sprite  _srcSprite;
    Rect    _spriteUv;
    bool    _hasSprite;
    bool    _srgb = true;

    // 다운샘플용 리소스
    RenderTexture _downRT;                 // cols × rows × superSamples 크기의 다운샘플 RT
    Material      _blitMat;                // 스프라이트 UV 절단/플립 전용 블릿 셰이더 머터리얼
    static readonly int _MainTexID = Shader.PropertyToID("_MainTex");
    static readonly int _UVRectID  = Shader.PropertyToID("_UVRect");
    static readonly int _FlipYID   = Shader.PropertyToID("_FlipY");

    // Readback
    AsyncGPUReadbackRequest _pendingReq;
    NativeArray<Color32>    _frame;
    bool _frameValid;

    // 문자열 빌더 (메인/배경 ASCII 버퍼)
    readonly StringBuilder _sb   = new(128 * 128);
    readonly StringBuilder _bgSb = new(128 * 128);

    // 컬러 태그 캐시 (12bit 양자화 키 -> "<color=#RRGGBB>")
    readonly Dictionary<int, string> _colorTagCache = new(256);
    
    // 프레임별 공유 머터리얼 캐시 (한 프레임에 한 번만 FaceColor 수정)
    static readonly Dictionary<Material, int> _materialFrameCache = new();
    static int _lastFrameUpdated = -1;

    // 재생 타이밍 누적 (FPS 제어용)
    float _accum;

    // 문자셋 프리셋
    static readonly string kExtendedHigh =
        "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\"^'. ";
    static readonly string kAlphabetic =
        "mwNQHBAXVUEOCDLJYkisrjftnlcvxzaghpduyeTIKFRSZqWGMbPo  ";
    static readonly string kAlphanumeric =
        "MWNXQ0O8B@#%&$9G6E5SDPAZ2U3R4C7YK1VTLJIHoaqmwbdpzukxrjftnvcylie  ";
    static readonly string kNormal = "@%#*+=-:. ";
    static readonly string kNormal2 = "&$@#%*+=-:. ";
    static readonly char[] kHexLUT = "0123456789ABCDEF".ToCharArray();

    // TMP FaceColor 프로퍼티 ID (_FaceColor)
    static readonly int FaceColorID = Shader.PropertyToID("_FaceColor");

    // ─────────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        Debug.Assert(asciiText != null,
            "[AsciiImageUGUI] asciiText reference is required. Please assign a TMP_Text.");

        Debug.Assert(
            sourceTexture || sourceSprite || sourceUIImage || sourceSpriteRenderer,
            "[AsciiImageUGUI] At least one source must be assigned (Texture/Sprite/Image/SpriteRenderer/Animator).");

        EnsureFontMaterials();
    }

    void OnEnable()
    {
        if (asciiText != null) asciiText.richText = true;
        if (bgAsciiText != null) bgAsciiText.richText = true;
        
        EnsureBlitMaterial();
        Debug.Assert(_blitMat != null,
            "[AsciiImageUGUI] Failed to create blit material. Make sure 'Hidden/Ascii/UVBlit' shader is included.");

        EnsureFontMaterials();

        ResolveSourceTexture();
        RecreateRTIfNeeded();
        KickReadback();

#if UNITY_EDITOR
        OnValidate();
#endif
    }

    void OnDisable()
    {
        ReleaseRT();
        if (_frame.IsCreated)
        {
            _frame.Dispose();
        }
        _frameValid = false;
    }
    
    void OnDestroy()
    {
        if (_frame.IsCreated)
        {
            _frame.Dispose();
            _frame = default;
        }
    }

    void Update()
    {
        if (!realtime)
            return;

        float fps = Mathf.Max(1, targetFps);
        if (useAnimatorSpeed && sourceAnimator)
        {
            fps *= Mathf.Max(0.01f, sourceAnimator.speed);
        }

        _accum += Time.unscaledDeltaTime;
        if (_accum < 1f / fps)
        {
            // 프레임 사이에도 이미 완료된 Readback이 있다면 소비
            TryConsumeReadback();
            return;
        }
        
        _accum = 0f;

        ResolveSourceTexture();
        RecreateRTIfNeeded();

        DownsampleToRT();
        KickReadback();
        TryConsumeReadback();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Initialization Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 메인/배경 폰트 머터리얼이 비어 있을 경우, 각 TMP_Text의 기본 머터리얼을 사용하도록 보정합니다.
    /// </summary>
    void EnsureFontMaterials()
    {
        if (asciiText != null && mainFontMaterial == null)
        {
            mainFontMaterial = asciiText.fontMaterial;
        }

        if (bgAsciiText != null && bgFontMaterial == null)
        {
            bgFontMaterial = bgAsciiText.fontMaterial;
        }
    }

    /// <summary>
    /// 다운샘플링에 사용할 블릿 머터리얼을 생성합니다.
    /// </summary>
    void EnsureBlitMaterial()
    {
        if (_blitMat != null)
            return;

        var shader = Shader.Find("Hidden/Ascii/UVBlit");
        if (shader != null)
        {
            _blitMat = new Material(shader)
            {
                name = "AsciiImageUVBlit (Instance)"
            };
        }
        else
        {
            _blitMat = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Source Resolution / RenderTexture Management
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 활성화된 입력(Image, SpriteRenderer, Sprite, Texture 순)에서
    /// 소스 텍스처와 스프라이트 UV 정보를 갱신합니다.
    /// </summary>
    void ResolveSourceTexture()
    {
        Sprite s = null;
        if (sourceUIImage && sourceUIImage.sprite)                    s = sourceUIImage.sprite;
        else if (sourceSpriteRenderer && sourceSpriteRenderer.sprite) s = sourceSpriteRenderer.sprite;
        else if (sourceSprite)                                           s = sourceSprite;

        if (s != null)
        {
            _srcSprite = s;
            _srcTex    = s.texture;
            _hasSprite = true;

            var tr = s.textureRect;
            _spriteUv = new Rect(tr.x / _srcTex.width, tr.y / _srcTex.height,
                                 tr.width / _srcTex.width, tr.height / _srcTex.height);
            _srgb = true;
            return;
        }

        _srcSprite = null;
        _srcTex    = sourceTexture;
        _hasSprite = false;
        _srgb      = true;
    }

    /// <summary>
    /// 소스 텍스처 기준으로 ASCII 그리드 크기를 계산하고, 그에 맞는 다운샘플 RT를 생성/재사용합니다.
    /// </summary>
    void RecreateRTIfNeeded()
    {
        if (_srcTex == null)
            return;

        GetEffectiveSourceSize(out var effW, out var effH);
        if (effW <= 0 || effH <= 0)
            return;

        ComputeGridSize(effW, effH, out int cols, out int rows);

        int ss = Mathf.Max(1, superSamples);
        int w = Mathf.Max(8, cols * ss);
        int h = Mathf.Max(8, rows * ss);

        if (_downRT != null && (_downRT.width != w || _downRT.height != h))
            ReleaseRT();

        if (_downRT == null)
        {
            _downRT = new RenderTexture(
                w, h, 0, RenderTextureFormat.ARGB32,
                _srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear)
            {
                filterMode       = FilterMode.Bilinear,
                wrapMode         = TextureWrapMode.Clamp,
                useMipMap        = false,
                autoGenerateMips = false,
                name             = $"AsciiDownRT_{w}x{h}"
            };
            _downRT.Create();
        }
    }

    /// <summary>
    /// 다운샘플 RT 리소스를 해제합니다.
    /// </summary>
    void ReleaseRT()
    {
        if (_downRT != null)
        {
            _downRT.Release();
            DestroyImmediate(_downRT);
            _downRT = null;
        }
    }

    /// <summary>
    /// 스프라이트 사용 여부에 따라 실제 유효한 소스 해상도를 반환합니다.
    /// </summary>
    void GetEffectiveSourceSize(out int effW, out int effH)
    {
        if (_srcTex == null)
        {
            effW = effH = 0;
            return;
        }
        
        if (_hasSprite && _srcSprite != null)
        {
            var tr = _srcSprite.textureRect;
            effW = Mathf.RoundToInt(tr.width);
            effH = Mathf.RoundToInt(tr.height);
        }
        else
        {
            effW = _srcTex.width;
            effH = _srcTex.height;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GPU Downsample (Sprite UV / FlipY 지원)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 스프라이트 UV/FlipY를 고려하여 소스 텍스처를 다운샘플 RT로 Blit합니다.
    /// </summary>
    void DownsampleToRT()
    {
        if (_srcTex == null || _downRT == null || _blitMat == null)
            return;
        
        var prevRT = RenderTexture.active;

        _blitMat.SetTexture(_MainTexID, _srcTex);
        _blitMat.SetVector(
            _UVRectID,
            _hasSprite
                ? new Vector4(_spriteUv.x, _spriteUv.y, _spriteUv.width, _spriteUv.height)
                : new Vector4(0f, 0f, 1f, 1f));
        _blitMat.SetFloat(_FlipYID, flipY ? 1f : 0f);

        Graphics.Blit(null, _downRT, _blitMat, 0);
        RenderTexture.active = prevRT;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Async GPU Readback
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 다운샘플 RT에 대해 AsyncGPUReadback 요청을 보냅니다.
    /// 기존 요청이 아직 완료되지 않았다면 새 요청을 보내지 않습니다.
    /// </summary>
    void KickReadback()
    {
        if (_downRT == null || _pendingReq.done == false)
            return;

        _pendingReq = AsyncGPUReadback.Request(_downRT, 0, OnReadbackComplete);
    }

    /// <summary>
    /// AsyncGPUReadback 완료 콜백. GPU 데이터를 NativeArray<Color32>로 복사합니다.
    /// </summary>
    void OnReadbackComplete(AsyncGPUReadbackRequest req)
    {
        if (req.hasError)
        {
            _frameValid = false;
            return;
        }

        var data = req.GetData<Color32>();
        if (!_frame.IsCreated || _frame.Length != data.Length)
        {
            if (_frame.IsCreated) _frame.Dispose();
            _frame = new NativeArray<Color32>(data.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
        _frame.CopyFrom(data);
        _frameValid = true;
    }

    /// <summary>
    /// Readback이 완료된 프레임이 있다면 ASCII 변환을 수행하고 TMP 텍스트를 갱신합니다.
    /// </summary>
    void TryConsumeReadback()
    {
        if (!_frameValid)
            return;
        
        _frameValid = false;
        GetEffectiveSourceSize(out var effW, out var effH);
        if (effW <= 0 || effH <= 0)
            return;

        ComputeGridSize(effW, effH, out int cols, out int rows);
        int ss = Mathf.Max(1, superSamples);

        GenerateAsciiFromFrame(cols, rows, ss);
        ApplyToTMP();
    }

    /// <summary>
    /// 텍스처 해상도와 columns/charAspect를 바탕으로 ASCII 그리드의 행/열 수를 계산합니다.
    /// </summary>
    void ComputeGridSize(int texW, int texH, out int cols, out int rows)
    {
        cols = Mathf.Clamp(columns, 8, 4096);
        float cellW = (float)texW / cols;
        float cellH = Mathf.Max(0.0001f, cellW * charAspect);
        rows = Mathf.Max(1, Mathf.RoundToInt(texH / cellH));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ASCII Generation (색 태그 러닝 / 12bit 색 양자화)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 다운샘플 RT에서 픽셀을 샘플링하여 ASCII 문자열 버퍼(_sb, _bgSb)를 생성합니다.
    /// </summary>
    void GenerateAsciiFromFrame(int cols, int rows, int ss)
    {
        if (!_frame.IsCreated || _downRT == null)
            return;

        int w = _downRT.width;
        int h = _downRT.height;
        int stepX = Mathf.Max(1, ss);
        int stepY = Mathf.Max(1, ss);

        string charsetActive = GetActiveCharset();
        int charCount = Mathf.Max(1, charsetActive.Length);

        float spaceThreshold = Mathf.Clamp01(1f - spaceDensity);

        _sb.Clear();
        _bgSb.Clear();
        _sb.EnsureCapacity((cols + 1) * rows);

        int  lastColorKey = -1;
        bool colorOpen    = false;

        for (int r = 0; r < rows; r++)
        {
            int py = Mathf.Clamp(r * stepY + stepY / 2, 0, h - 1);
            for (int c = 0; c < cols; c++)
            {
                int px = Mathf.Clamp(c * stepX + stepX / 2, 0, w - 1);
                int idx = py * w + px;
                Color32 col = _frame[idx];

                // RGB -> Luminance
                float L = (0.2126f * col.r + 0.7152f * col.g + 0.0722f * col.b) / 255f;
                L = Mathf.Clamp01(invertLuminance ? (1f - L) : L);
                L = Mathf.Pow(L, 1f / Mathf.Max(0.0001f, contrast));

                // 스페이스 밀도 처리 (밝은 영역을 공백 문자로)
                if (spaceDensity > 0f && L >= spaceThreshold)
                {
                    if (colorOpen)
                    {
                        _sb.Append("</color>");
                        colorOpen    = false;
                        lastColorKey = -1;
                    }
                    _sb.Append(' ');
                    _bgSb.Append(bgFillChar);
                    continue;
                }
                else if (spaceDensity > 0f && spaceThreshold > 0f)
                {
                    // 남은 영역은 0..1 범위로 재정규화
                    L = Mathf.Clamp01(L / spaceThreshold);
                }

                int ci = Mathf.Clamp(Mathf.RoundToInt(L * (charCount - 1)), 0, charCount - 1);
                char ch = charsetActive[ci];

                // 공백 문자는 항상 공백으로 처리 (컬러 태그 닫기)
                if (ch == ' ')
                {
                    if (colorOpen)
                    {
                        _sb.Append("</color>");
                        colorOpen    = false;
                        lastColorKey = -1;
                    }
                    _sb.Append(' ');
                    _bgSb.Append(bgFillChar);
                    continue;
                }

                // 평균색(리컬러 적용)
                Color avg = new Color(col.r / 255f, col.g / 255f, col.b / 255f, 1f);
                if (mainRecolor && mainRecolorAmount > 0f)
                {
                    Color.RGBToHSV(avg,         out var h0, out var s0, out var v0);
                    Color.RGBToHSV(mainTextColor, out var h1, out var s1, out var v1);

                    float h360 = Mathf.LerpAngle(h0 * 360f, h1 * 360f, mainRecolorAmount);
                    float s    = Mathf.Lerp(s0, s1, mainRecolorAmount);
                    float v    = keepLuminance ? v0 : Mathf.Lerp(v0, v1, mainRecolorAmount);
                    avg        = Color.HSVToRGB(h360 / 360f, s, v);
                }

                // 12bit 양자화 (각 채널 4bit) -> 캐시 키
                int key =
                    ((int)(avg.r * 15f) << 8) |
                    ((int)(avg.g * 15f) << 4) |
                    (int)(avg.b * 15f);

                // 색 구간이 바뀌면 이전 <color> 태그를 닫고 새 태그 오픈
                if (key != lastColorKey)
                {
                    if (colorOpen) _sb.Append("</color>");
                    _sb.Append(GetOrMakeColorTag(key));
                    colorOpen    = true;
                    lastColorKey = key;
                }

                _sb.Append(ch);
                _bgSb.Append(' ');
            }

            if (colorOpen)
            {
                _sb.Append("</color>");
                colorOpen    = false;
                lastColorKey = -1;
            }

            if (r < rows - 1)
            {
                _sb.Append('\n');
                _bgSb.Append('\n');
            }
        }
    }

    /// <summary>
    /// 12bit 색 키를 이용하여 "<color=#RRGGBB>" 태그를 생성/캐시합니다.
    /// </summary>
    string GetOrMakeColorTag(int key)
    {
        if (_colorTagCache.TryGetValue(key, out var tag))
            return tag;

        byte r4 = (byte)((key >> 8) & 0xF);
        byte g4 = (byte)((key >> 4) & 0xF);
        byte b4 = (byte)(key & 0xF);

        byte r = (byte)((r4 << 4) | r4);
        byte g = (byte)((g4 << 4) | g4);
        byte b = (byte)((b4 << 4) | b4);

        var sb = new StringBuilder(16);
        sb.Append("<color=#");
        AppendHexRGB(ref sb, r, g, b);
        sb.Append('>');
        tag = sb.ToString();
        _colorTagCache[key] = tag;
        return tag;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TMP 적용 / 머터리얼 업데이트
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 생성된 ASCII 버퍼를 TMP_Text에 반영하고, 폰트 머터리얼의 HDR FaceColor를 갱신합니다.
    /// </summary>
    void ApplyToTMP()
    {
        if (asciiText == null)
            return;

        asciiText.enableWordWrapping = false;
        asciiText.alignment          = TextAlignmentOptions.TopLeft;
        asciiText.fontSize           = fontSize;
        asciiText.SetText(_sb);

        asciiText.fontMaterial = mainFontMaterial;
        UpdateFontMaterialOncePerFrame(mainFontMaterial, mainIntensity);

        if (bgAsciiText)
        {
            bgAsciiText.enableWordWrapping = false;
            bgAsciiText.alignment          = TextAlignmentOptions.TopLeft;
            bgAsciiText.fontSize           = fontSize;
            bgAsciiText.SetText(_bgSb);
            bgAsciiText.color              = bgColor;

            bgAsciiText.fontMaterial = bgFontMaterial;
            UpdateFontMaterialOncePerFrame(bgFontMaterial, bgIntensity);

            // 레이아웃을 메인 텍스트와 완전히 맞추어 겹치도록 조정
            var rtMain = asciiText.rectTransform;
            var rtBg   = bgAsciiText.rectTransform;
            rtBg.anchorMin        = rtMain.anchorMin;
            rtBg.anchorMax        = rtMain.anchorMax;
            rtBg.pivot            = rtMain.pivot;
            rtBg.anchoredPosition = rtMain.anchoredPosition;
            rtBg.sizeDelta        = rtMain.sizeDelta;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(asciiText);
            if (bgAsciiText) EditorUtility.SetDirty(bgAsciiText);
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// 현재 프리셋과 invertGradient 옵션에 따라 활성 문자셋을 반환합니다.
    /// </summary>
    string GetActiveCharset()
    {
        string s = gradientPreset switch
        {
            AsciiGradientPreset.Custom       => charset,
            AsciiGradientPreset.ExtendedHigh => kExtendedHigh,
            AsciiGradientPreset.Alphabetic   => kAlphabetic,
            AsciiGradientPreset.Alphanumeric => kAlphanumeric,
            AsciiGradientPreset.Normal       => kNormal,
            AsciiGradientPreset.Normal2      => kNormal2,
            _                                => charset
        };

        if (invertGradient && !string.IsNullOrEmpty(s))
        {
            var arr = s.ToCharArray();
            Array.Reverse(arr);
            s = new string(arr);
        }
        return s;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Editor Preview / Animator Speed Sync
    // ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        if (!Application.isPlaying)
        {
            EnsureBlitMaterial();
            ResolveSourceTexture();
            RecreateRTIfNeeded();
            DownsampleToRT();
            
            if (_downRT == null)
                return;

            // 에디터 프리뷰용 동기 ReadPixels (AsyncGPUReadback 사용 안 함)
            var tmp = RenderTexture.active;
            RenderTexture.active = _downRT;

            var tex = new Texture2D(_downRT.width, _downRT.height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, _downRT.width, _downRT.height), 0, 0);
            tex.Apply(false, false);

            if (_frame.IsCreated) _frame.Dispose();
            _frame      = tex.GetRawTextureData<Color32>();
            _frameValid = true;

            TryConsumeReadback();

            DestroyImmediate(tex);
            RenderTexture.active = tmp;
        }
        else
        {
            // 재생 중에는 Animator 루프 길이를 loopDurationSeconds에 맞추도록 speed 자동 조정
            if (sourceAnimator && useAnimatorSpeed && loopDurationSeconds > 0f)
            {
                var info = sourceAnimator.GetCurrentAnimatorClipInfo(0);
                if (info.Length > 0 && info[0].clip != null)
                {
                    var clip = info[0].clip;
                    sourceAnimator.speed = clip.length / loopDurationSeconds;
                }
            }
        }
    }
#endif

    // ─────────────────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void AppendHexRGB(ref StringBuilder sb, byte r, byte g, byte b)
    {
        sb.Append(kHexLUT[r >> 4]); sb.Append(kHexLUT[r & 15]);
        sb.Append(kHexLUT[g >> 4]); sb.Append(kHexLUT[g & 15]);
        sb.Append(kHexLUT[b >> 4]); sb.Append(kHexLUT[b & 15]);
    }
    
    /// <summary>
    /// 한 프레임에 한 번만 지정된 머터리얼의 FaceColor를 HDR 강도로 갱신합니다.
    /// </summary>
    static void UpdateFontMaterialOncePerFrame(Material mat, float intensity)
    {
        if (mat == null)
            return;

        int currentFrame = Time.frameCount;
        if (_lastFrameUpdated != currentFrame)
        {
            _lastFrameUpdated = currentFrame;
            _materialFrameCache.Clear();
        }

        if (!_materialFrameCache.TryAdd(mat, currentFrame))
            return;

        var hdr = Color.white * Mathf.Pow(2f, Mathf.Max(0f, intensity));
        mat.SetColor(FaceColorID, hdr);
    }
}
