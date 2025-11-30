// CameraBlurFeature.cs
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraBlurFeature : ScriptableRendererFeature {
    [Serializable]
    public class FeatureParams {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingPostProcessing;
        public Shader Shader;
        [Header("Fallback defaults (used if no controller)")]
        public CameraBlurSettings DefaultSettings = new();
    }
    public FeatureParams Params = new();

    CameraBlurPass _pass;

    public override void Create() {
        if (Params.Shader == null) Params.Shader = Shader.Find("Hidden/Custom/CameraBlur");
        _pass = new CameraBlurPass(Params);
        _pass.renderPassEvent = Params.Event;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data) {
        if (!_pass.Setup(renderer, ref data)) return;
        renderer.EnqueuePass(_pass);
    }
}
