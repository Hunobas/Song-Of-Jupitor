using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class SliceGlitchSettings
{
  public RenderPassEvent PassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

  [Range(0f, 1f)] public float Intensity = 0f;
  [Range(0f, 1f)] public float RectAmount = 0.25f;  // per-cell spawn prob
  public float RectMinSizePx = 12f;
  public float RectMaxSizePx = 96f;
  public float RectMaxShiftPx = 32f;
  [Range(1, 4)] public int   Iterations = 3;        // perf-safe
  [Range(0f, 1f)] public float Aniso = 1f;          // 1: fully anisotropic

  public int   Seed = 12345;
}

public class SliceGlitchFeature : ScriptableRendererFeature
{
  class SlicePass : ScriptableRenderPass
  {
    static readonly string kTag = "SliceGlitchBlocks";

    // shader props
    static readonly int _Intensity        = Shader.PropertyToID("_Intensity");
    static readonly int _Seed             = Shader.PropertyToID("_Seed");
    static readonly int _SourceTexelSize  = Shader.PropertyToID("_SourceTexelSize");

    static readonly int _RectAmount       = Shader.PropertyToID("_RectAmount");
    static readonly int _RectMinSizePx    = Shader.PropertyToID("_RectMinSizePx");
    static readonly int _RectMaxSizePx    = Shader.PropertyToID("_RectMaxSizePx");
    static readonly int _RectMaxShiftPx   = Shader.PropertyToID("_RectMaxShiftPx");
    static readonly int _Iterations       = Shader.PropertyToID("_Iterations");
    static readonly int _Aniso            = Shader.PropertyToID("_Aniso");

    readonly Material _mat;
    readonly SliceGlitchSettings _settings;

    RTHandle _source;

    public SlicePass(Material mat, SliceGlitchSettings settings)
    {
      _mat = mat;
      _settings = settings;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
      ConfigureInput(ScriptableRenderPassInput.Color);
      _source = renderingData.cameraData.renderer.cameraColorTargetHandle;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      if (_mat == null) return;
      if (_settings.Intensity <= 0f || _settings.RectAmount <= 0f || _settings.RectMaxShiftPx <= 0f)
        return;

      var cam = renderingData.cameraData.camera;
      int w = cam.pixelWidth;
      int h = cam.pixelHeight;

      var cmd = CommandBufferPool.Get(kTag);

      _mat.SetFloat(_Intensity, _settings.Intensity);
      _mat.SetInt(_Seed, _settings.Seed);
      _mat.SetVector(_SourceTexelSize, new Vector4(1f / w, 1f / h, w, h));

      _mat.SetFloat(_RectAmount,    Mathf.Clamp01(_settings.RectAmount));
      _mat.SetFloat(_RectMinSizePx, Mathf.Max(1f, _settings.RectMinSizePx));
      _mat.SetFloat(_RectMaxSizePx, Mathf.Max(_settings.RectMinSizePx, _settings.RectMaxSizePx));
      _mat.SetFloat(_RectMaxShiftPx, Mathf.Max(0f, _settings.RectMaxShiftPx));
      _mat.SetInt  (_Iterations,    Mathf.Clamp(_settings.Iterations, 1, 4));
      _mat.SetFloat(_Aniso,         Mathf.Clamp01(_settings.Aniso));

      Blitter.BlitCameraTexture(cmd, _source, _source, _mat, 0);
      context.ExecuteCommandBuffer(cmd);
      CommandBufferPool.Release(cmd);
    }
  }

  public SliceGlitchSettings settings = new();
  [SerializeField] Shader shader;

  Material _material;
  SlicePass _pass;

  public override void Create()
  {
    if (shader == null) shader = Shader.Find("Hidden/URP/SliceGlitch");
    if (_material == null && shader != null)
      _material = CoreUtils.CreateEngineMaterial(shader);

    _pass = new SlicePass(_material, settings)
    {
      renderPassEvent = settings.PassEvent
    };
  }

  public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
  {
    _pass.renderPassEvent = settings.PassEvent;
    renderer.EnqueuePass(_pass);
  }

  protected override void Dispose(bool disposing)
  {
    CoreUtils.Destroy(_material);
  }
}