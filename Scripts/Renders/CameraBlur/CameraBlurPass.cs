// CameraBlurPass.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

class CameraBlurPass : ScriptableRenderPass {
    readonly CameraBlurFeature.FeatureParams _fp;
    Material _mat;
    RTHandle _tmpA, _tmpB;

    static readonly int _Intensity = Shader.PropertyToID("_Intensity");
    static readonly int _ClampPx   = Shader.PropertyToID("_ClampPx");
    static readonly int _AngleRad  = Shader.PropertyToID("_AngleRad");
    static readonly int _Center    = Shader.PropertyToID("_Center");
    static readonly int _TexelSize = Shader.PropertyToID("_TexelSize");
    static readonly int _RadiusPx  = Shader.PropertyToID("_RadiusPx");

    static readonly ShaderTagId _tag = new("UniversalForward");

    public CameraBlurPass(CameraBlurFeature.FeatureParams fp){ _fp = fp; }

    public bool Setup(ScriptableRenderer renderer, ref RenderingData rd) {
        if (_mat == null) _mat = CoreUtils.CreateEngineMaterial(_fp.Shader);

        var cam  = rd.cameraData.camera;

        if (!cam.TryGetComponent<CameraBlurController>(out var ctrl))
            ctrl = null;

        var st = (ctrl != null && ctrl.enabledBlur) ? ctrl.runtime : _fp.DefaultSettings;

        if (!(ctrl != null && ctrl.enabledBlur) || st.Intensity <= 0f)
            return false;

        _mat.DisableKeyword("TYPE_LINEAR");
        _mat.DisableKeyword("TYPE_RADIAL");
        if (st.Type == BlurType.Linear) _mat.EnableKeyword("TYPE_LINEAR");
        else _mat.EnableKeyword("TYPE_RADIAL");

        _mat.DisableKeyword("METHOD_GAUSS");
        _mat.DisableKeyword("METHOD_FIXED");
        _mat.DisableKeyword("METHOD_PROP");
        switch (st.Method) {
            case BlurMethod.Gaussian:     _mat.EnableKeyword("METHOD_GAUSS"); break;
            case BlurMethod.Fixed:        _mat.EnableKeyword("METHOD_FIXED"); break;
            case BlurMethod.Proportional: _mat.EnableKeyword("METHOD_PROP");  break;
        }

        // px 반경 산출
        var desc = rd.cameraData.cameraTargetDescriptor;
        var ds = Mathf.Max(1, st.Downsample);
        desc.width  /= ds; desc.height /= ds; desc.msaaSamples = 1; desc.depthBufferBits = 0;

        int maxRadius = Mathf.RoundToInt(Mathf.Max(desc.width, desc.height) * 0.03f); // 3% 권장
        int radiusPx  = Mathf.Min(Mathf.RoundToInt(st.Intensity * maxRadius), Mathf.RoundToInt(st.Clamp));
        if (radiusPx <= 0) return false;

        _mat.SetFloat(_Intensity, st.Intensity);
        _mat.SetFloat(_ClampPx, st.Clamp);
        _mat.SetFloat(_AngleRad, st.AngleDeg * Mathf.Deg2Rad);
        _mat.SetVector(_Center, st.RadialCenter01);
        _mat.SetVector(_TexelSize, new Vector4(1f/desc.width, 1f/desc.height, desc.width, desc.height));
        _mat.SetFloat(_RadiusPx, radiusPx);

        RenderingUtils.ReAllocateIfNeeded(ref _tmpA, desc, name: "_BlurTmpA");
        RenderingUtils.ReAllocateIfNeeded(ref _tmpB, desc, name: "_BlurTmpB");

        // 패스 state 유지
        _iterations = st.Iterations;
        _renderer = renderer;
        return true;
    }

    ScriptableRenderer _renderer;
    int _iterations;

    public override void Execute(ScriptableRenderContext ctx, ref RenderingData rd) {
        var cmd = CommandBufferPool.Get("CameraBlur");
        var src = _renderer.cameraColorTargetHandle;

        // 다운샘플 copy
        Blitter.BlitCameraTexture(cmd, src, _tmpA);

        for (int i=0;i<_iterations;i++){
            // 한 번의 패스로 Blur 수행(Linear/Radial 내부에서 tap 샘플)
            Blitter.BlitCameraTexture(cmd, _tmpA, _tmpB, _mat, 0);
            ( _tmpA, _tmpB ) = ( _tmpB, _tmpA ); // ping-pong
        }

        // 결과를 원본으로 합성
        Blitter.BlitCameraTexture(cmd, _tmpA, src);
        ctx.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd) { /* keep RTHandle (reuse) */ }
    public override void FrameCleanup(CommandBuffer cmd) { /* no-op */ }
}
