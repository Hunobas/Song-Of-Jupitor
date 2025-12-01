using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using Unity.Collections;

#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
#endif

/// <summary>
/// Texture/RenderTexture/Texture2D 를 읽기 가능한 복사본으로 만든 뒤
/// 평균 밝기/색을 이용해 TMP_Text에 아스키 아트를 출력합니다.
/// - columns: 가로 문자 수
/// - fontSize: TMP 폰트 사이즈
/// - charAspect: 문자 종횡비 보정(보통 1.8~2.2)
/// - superSamples: 셀 내부 슈퍼샘플링(N×N)
/// - invertLuminance/contrast: 밝기 변환 옵션
/// - gradientPreset/invertGradient: 문자 그라디언트 프리셋 및 반전
/// - spaceDensity: 밝은 영역을 공백으로 비우는 정도(0~1)
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

    [Header("UGUI References")]
    [SerializeField] private TMP_Text asciiText;     // 메인 텍스트
    [SerializeField] private TMP_Text bgAsciiText;   // 배경 텍스트

    [Header("Font Materials")]
    [SerializeField] private Material mainFontMaterial;
    [SerializeField] private Material bgFontMaterial;
    
    [Header("Input")]
    [SerializeField] private Texture sourceTexture;
    [SerializeField] private Sprite sourceSprite;                           // 정적 스프라이트 1장
    [SerializeField] private Image sourceUIImage;            // 애니메이터가 바꾸는 UI Image
    [SerializeField] private SpriteRenderer sourceSpriteRenderer;           // 애니메이터가 바꾸는 SpriteRenderer
    
    [Header("Ascii Options")]
    [SerializeField, Min(8)] private int columns = 160;
    [SerializeField, Min(1f)] private float fontSize = 2f;
    [SerializeField, Range(0.5f, 5f)] private float charAspect = 2.0f;
    [SerializeField] private bool flipY = true;
    [Tooltip("밝기 반전(검정↔흰색)")]
    [SerializeField] private bool invertLuminance = false;
    [Tooltip("밝기 감마(>1 밝은 영역 확장)")]
    [SerializeField, Range(0f, 2f)] private float contrast = 1.0f;

    [Header("ASCII Gradient")]
    [Tooltip("프리셋 선택 (Custom은 아래 charset 사용)")]
    [SerializeField] private AsciiGradientPreset gradientPreset = AsciiGradientPreset.ExtendedHigh;
    [Tooltip("프리셋/커스텀 그라디언트를 역순으로 사용")]
    [SerializeField] private bool invertGradient = false;
    [SerializeField, TextArea, ShowIf("@gradientPreset == AsciiGradientPreset.Custom")]
    private string charset =
        "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\"^'.  ";
    [Tooltip("밝은 영역을 공백으로 더 많이 비우는 정도(0~1)")]
    [SerializeField, Range(0f, 1f)] private float spaceDensity = 0f;
    [SerializeField, Min(1)] private int superSamples = 1;
    
    [SerializeField] private char bgFillChar = '-';

    [Header("Coloring")]
    [SerializeField] private bool mainRecolor = true;
    [ColorUsage(true, true)] [SerializeField, ShowIf("@mainRecolor == true")]
    private Color mainTextColor = Color.green;
    [Range(0f, 1f)] [SerializeField, ShowIf("@mainRecolor == true")]
    [Tooltip("1=완전 재색칠, 0=원본색")]
    private float mainRecolorAmount = 1f; 
    [SerializeField, ShowIf("@mainRecolor == true")]
    private bool keepLuminance = true; 
    
    [Range(0f, 3f)] [SerializeField] private float mainIntensity = 1.5f;
    [ColorUsage(true, true)]
    [SerializeField] private Color bgColor = new Color(0.1f, 0.9f, 0.1f);
    [Range(0f, 3f)] [SerializeField] private float bgIntensity = 1.0f;
    
    [Header("Animation")]
    [SerializeField] private Animator sourceAnimator;
    [SerializeField, Tooltip("Animator 1루프 당 걸리는 시간")]
    private float loopDurationSeconds = 2f;
    [SerializeField, Tooltip("Animator.speed를 ASCII 프레임 주기와 연동")]
    private bool useAnimatorSpeed = false;

    [Header("Update")]
    [SerializeField] private bool realtime = false;
    [SerializeField, Range(1, 60), ShowIf("@useAnimatorSpeed == false && realtime == true")]
    private int targetFps = 15;

    // runtime
    Texture _srcTex;
    Texture2D _readableCopy;
    float _accum;
    StringBuilder _sb = new(256 * 256);
    StringBuilder _bgSb = new(256 * 256);
    
    AnimationClip _activeClip;

    // ── 프리셋 문자셋 정의 ─────────────────────────────────────────────────────
    static readonly string kExtendedHigh =
        "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\"^'. ";

    // 알파벳만 사용(소문자→대문자→공백 순, 밝기 분포를 위해 정렬)
    static readonly string kAlphabetic =
        "mwNQHBAXVUEOCDLJYkisrjftnlcvxzaghpduyeTIKFRSZqWGMbPo" + "  ";

    // 영숫자 위주
    static readonly string kAlphanumeric =
        "MWNXQ0O8B@#%&$9G6E5SDPAZ2U3R4C7YK1VTLJIH" + "oaqmwbdpzukxrjftnvcylie" + "  ";

    // 보편적인 10~70자 그라디언트
    static readonly string kNormal =
        "@%#*+=-:. ";

    // 기호 가중값이 높은 대체 그라디언트
    static readonly string kNormal2 =
        "&$@#%*+=-:. ";
    
    static readonly char[] kHexLUT = "0123456789ABCDEF".ToCharArray();

    void Awake()
    {
        Debug.Assert(asciiText != null);
        Debug.Assert(bgAsciiText != null);
        Debug.Assert(mainFontMaterial != null);
        Debug.Assert(bgFontMaterial != null);
        
        _activeClip = sourceAnimator.GetCurrentAnimatorClipInfo(0)[0].clip;
    }

    void Reset()
    {
        var tmps = GetComponentsInChildren<TMP_Text>(true);
        if (tmps.Length > 0) asciiText = tmps[0];
        if (tmps.Length > 1) bgAsciiText = tmps[1];
    }

    void OnEnable()
    {
        ResolveSourceTexture();
        RenderAscii();
    }

    void Update()
    {
        if (!realtime)
            return;

        _accum += Time.unscaledDeltaTime;

        float fps = Mathf.Max(1, targetFps);

        if (useAnimatorSpeed && sourceAnimator != null)
        {
            fps *= Mathf.Max(0.01f, sourceAnimator.speed);
        }

        if (_accum >= 1f / fps)
        {
            _accum = 0f;
            ResolveSourceTexture();
            RenderAscii();
        }
    }

    // ── 입력 소스 선택 + 읽기 가능한 복사본 만들기 ─────────────────────────────
    void ResolveSourceTexture()
    {
        // 1) 스프라이트 우선
        Sprite s = null;
        if (sourceUIImage != null && sourceUIImage.sprite != null) s = sourceUIImage.sprite;
        else if (sourceSpriteRenderer != null && sourceSpriteRenderer.sprite != null) s = sourceSpriteRenderer.sprite;
        else if (sourceSprite != null) s = sourceSprite;

        if (s != null)
        {
            MakeReadableCopyFromSprite(s, ref _readableCopy);
            _srcTex = s.texture;
            flipY = false;
            return;
        }

        // 2) Texture 경로
        _srcTex = sourceTexture;
        if (_srcTex == null) return;

        if (_srcTex is Texture2D t2d && t2d.isReadable)
            _readableCopy = t2d;
        else
            MakeReadableCopy(_srcTex, ref _readableCopy);

        flipY = true;
    }
    
    static void MakeReadableCopyFromSprite(Sprite s, ref Texture2D dst)
    {
        var srcTex = s.texture;
        var rectPx = s.textureRect; // 픽셀 단위 사각형
        int w = Mathf.RoundToInt(rectPx.width);
        int h = Mathf.RoundToInt(rectPx.height);

        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;

        // 정규화 UV 사각형
        var uv = new Rect(rectPx.x / srcTex.width, rectPx.y / srcTex.height,
            rectPx.width / srcTex.width, rectPx.height / srcTex.height);

        // 픽셀 좌표계로 그리기
        Graphics.SetRenderTarget(rt);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, w, 0, h);
        // sourceRect(uv)로 잘라서 0,0~w,h에 꽉 채워 그림
        Graphics.DrawTexture(new Rect(0, 0, w, h), srcTex, uv, 0, 0, 0, 0);
        GL.PopMatrix();

        RenderTexture.active = rt;

        if (dst == null || dst.width != w || dst.height != h)
            dst = new Texture2D(w, h, TextureFormat.RGBA32, false, false);

        dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        dst.Apply(false, false);

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
    }

    static void MakeReadableCopy(Texture src, ref Texture2D dst)
    {
        int w = src.width, h = src.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;

        Graphics.Blit(src, rt);
        RenderTexture.active = rt;

        if (dst == null || dst.width != w || dst.height != h)
            dst = new Texture2D(w, h, TextureFormat.RGBA32, false, false);

        dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        dst.Apply(false, false);

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
    }
    // ───────────────────────────────────────────────────────────────────────────

    [ContextMenu("RenderAscii (Manual)")]
    public void RenderAscii()
    {
        // 기본 가드
        if (asciiText == null || _srcTex == null) return;
        if (_readableCopy == null) ResolveSourceTexture();
        if (_readableCopy == null) return;

        GenerateAsciiString();

        asciiText.enableWordWrapping = false;
        asciiText.alignment = TextAlignmentOptions.TopLeft;
        asciiText.fontSize = fontSize;
        asciiText.SetText(_sb);

        asciiText.fontMaterial = mainFontMaterial;
        var mainHdr = Color.white * Mathf.Pow(2f, Mathf.Max(0f, mainIntensity));
        mainFontMaterial.SetColor(ShaderUtilities.ID_FaceColor, mainHdr);
        
        if (bgAsciiText != null)
        {
            bgAsciiText.enableWordWrapping = false;
            bgAsciiText.alignment = TextAlignmentOptions.TopLeft;
            bgAsciiText.fontSize = fontSize;
            bgAsciiText.SetText(_bgSb);
            bgAsciiText.color = bgColor;

            bgAsciiText.fontMaterial = bgFontMaterial;
            Color bgHdr = Color.white * Mathf.Pow(2f, Mathf.Max(0f, bgIntensity));
            bgFontMaterial.SetColor(ShaderUtilities.ID_FaceColor, bgHdr);

            var rtMain = asciiText.rectTransform;
            var rtBg   = bgAsciiText.rectTransform;
            rtBg.anchorMin = rtMain.anchorMin;
            rtBg.anchorMax = rtMain.anchorMax;
            rtBg.pivot     = rtMain.pivot;
            rtBg.anchoredPosition = rtMain.anchoredPosition;
            rtBg.sizeDelta = rtMain.sizeDelta;
        }

    #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(asciiText);
            if (bgAsciiText != null) EditorUtility.SetDirty(bgAsciiText);
            EditorUtility.SetDirty(this);
        }
    #endif
    }
    
    private void GenerateAsciiString()
    {
        if (_readableCopy == null)
            return;

        int texW = _readableCopy.width;
        int texH = _readableCopy.height;

        int cols = Mathf.Clamp(columns, 8, 4096);
        float cellW = (float)texW / cols;
        float cellH = cellW * Mathf.Max(0.0001f, charAspect);
        int rows = Mathf.Max(1, Mathf.RoundToInt(texH / cellH));

        NativeArray<Color32> pixels = _readableCopy.GetPixelData<Color32>(0);

        int ss = Mathf.Max(1, superSamples);
        float invSamples = 1f / (ss * ss);

        string charsetActive = GetActiveCharset();
        int charCount = Mathf.Max(1, charsetActive.Length);

        float spaceThreshold = Mathf.Clamp01(1f - spaceDensity);

        _sb.Clear();
        _bgSb.Clear();
        _sb.EnsureCapacity((cols + 1) * rows);

        for (int r = 0; r < rows; r++)
        {
            float y0 = r * cellH;
            float y1 = Mathf.Min(texH - 1, y0 + cellH);

            for (int c = 0; c < cols; c++)
            {
                float x0 = c * cellW;
                float x1 = Mathf.Min(texW - 1, x0 + cellW);

                float L = 0f;
                float Rsum = 0f, Gsum = 0f, Bsum = 0f;

                for (int iy = 0; iy < ss; iy++)
                {
                    float ty = Mathf.Lerp(y0, y1, (iy + 0.5f) / ss);
                    int sy = Mathf.Clamp(Mathf.RoundToInt(ty), 0, texH - 1);
                    if (flipY) sy = texH - 1 - sy;

                    for (int ix = 0; ix < ss; ix++)
                    {
                        float tx = Mathf.Lerp(x0, x1, (ix + 0.5f) / ss);
                        int sx = Mathf.Clamp(Mathf.RoundToInt(tx), 0, texW - 1);

                        int idx = sy * texW + sx;
                        Color32 col = pixels[idx];

                        L += (0.2126f * col.r + 0.7152f * col.g + 0.0722f * col.b) / 255f;
                        Rsum += col.r; Gsum += col.g; Bsum += col.b; // ★
                    }
                }

                L *= invSamples;

                // 밝기 처리
                L = Mathf.Clamp01(invertLuminance ? (1f - L) : L);
                L = Mathf.Pow(L, 1f / Mathf.Max(0.0001f, contrast));

                // Space Density 처리
                if (spaceDensity > 0f && L >= spaceThreshold)
                {
                    _sb.Append(' ');
                    _bgSb.Append(bgFillChar);
                    continue;
                }
                else if (spaceDensity > 0f && spaceThreshold > 0f)
                {
                    L = Mathf.Clamp01(L / spaceThreshold);
                }

                // 문자 선택
                int ci = Mathf.Clamp(Mathf.RoundToInt(L * (charCount - 1)), 0, charCount - 1);
                char ch = charsetActive[ci];

                if (ch == ' ')
                {
                    _sb.Append(ch);
                    _bgSb.Append(bgFillChar);
                }
                else
                {
                    Color avg = new Color(
                        (Rsum * invSamples) / 255f,
                        (Gsum * invSamples) / 255f,
                        (Bsum * invSamples) / 255f, 1f);

                    if (mainRecolor && mainRecolorAmount > 0f)
                    {
                        Color.RGBToHSV(avg, out var h0, out var s0, out var v0);
                        Color.RGBToHSV(mainTextColor, out var h1, out var s1, out var v1);

                        float h = Mathf.LerpAngle(h0 * 360f, h1 * 360f, mainRecolorAmount) / 360f;
                        float s = Mathf.Lerp(s0, s1, mainRecolorAmount);
                        float v = keepLuminance ? v0 : Mathf.Lerp(v0, v1, mainRecolorAmount);

                        avg = Color.HSVToRGB(h, s, v);
                    }

                    byte br = (byte)Mathf.RoundToInt(avg.r * 255f);
                    byte bg = (byte)Mathf.RoundToInt(avg.g * 255f);
                    byte bb = (byte)Mathf.RoundToInt(avg.b * 255f);

                    _sb.Append("<color=#");
                    AppendHexRGB(ref _sb, br, bg, bb);
                    _sb.Append('>').Append(ch).Append("</color>");
                    _bgSb.Append(' ');
                }
            }

            if (r < rows - 1)
            {
                _sb.Append('\n');
                _bgSb.Append('\n');
            }
        }
    }

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
            _ => charset
        };

        // 프리셋/커스텀 반전
        if (invertGradient && !string.IsNullOrEmpty(s))
        {
            var arr = s.ToCharArray();
            System.Array.Reverse(arr);
            s = new string(arr);
        }
        return s;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        if (!Application.isPlaying)
        {
            ResolveSourceTexture();
            RenderAscii();
        }
        else UpdateAnimatorSpeed();
    }
    
    void UpdateAnimatorSpeed()
    {
        if (sourceAnimator == null) return;

        var info = sourceAnimator.GetCurrentAnimatorClipInfo(0);
        if (info.Length == 0) return;

        var clip = info[0].clip;
        if (clip == null) return;

        float originalDuration = clip.length; // 클립의 원래 길이 (초)
        if (loopDurationSeconds <= 0f) return;

        sourceAnimator.speed = originalDuration / loopDurationSeconds;
    }


    [ContextMenu("Convert To ASCII (Editor)")]
    private void ConvertToAsciiInEditor()
    {
        ResolveSourceTexture();
        RenderAscii();
    }
#endif
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void AppendHexRGB(ref StringBuilder sb, byte r, byte g, byte b)
    {
        sb.Append(kHexLUT[r >> 4]); sb.Append(kHexLUT[r & 15]);
        sb.Append(kHexLUT[g >> 4]); sb.Append(kHexLUT[g & 15]);
        sb.Append(kHexLUT[b >> 4]); sb.Append(kHexLUT[b & 15]);
    }
}
