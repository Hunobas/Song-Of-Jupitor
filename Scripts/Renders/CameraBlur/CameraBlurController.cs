// CameraBlurController.cs
using System;
using UnityEngine;

public enum BlurType { Linear, Radial }
public enum BlurMethod { Gaussian, Fixed, Proportional }

[Serializable]
public class CameraBlurSettings {
    public bool Enabled = false;
    public BlurType Type = BlurType.Linear;
    public BlurMethod Method = BlurMethod.Gaussian;

    [Range(0f, 1f)] public float Intensity = 0f; // 0..1(프로파일에서 해석: px/σ 등)
    [Min(0f)] public float Clamp = 8f;          // px 상한
    public float AngleDeg = 0f;                  // Linear 전용
    public Vector2 RadialCenter01 = new(0.5f, 0.5f); // Radial 전용 (Viewport)

    [Range(1, 4)] public int Downsample = 1;     // 1=풀해상도, 2/4=저해상
    [Range(1, 4)] public int Iterations = 1;     // 반복 샘플(=강도×품질)
}

[DisallowMultipleComponent]
public class CameraBlurController : MonoBehaviour {
    [Header("Animation Parameters (directly animatable)")]
    [SerializeField] public bool enabledBlur = false;
    [SerializeField, Range(0f,1f)] public float intensity = 0f;
    [SerializeField, Min(0f)] public float clamp = 8f;
    [SerializeField] public float angleDeg = 0f;
    [SerializeField] public Vector2 radialCenter01 = new(0.5f,0.5f);
    [SerializeField, Range(1,4)] public int downsample = 1;
    [SerializeField, Range(1,4)] public int iterations = 1;
    
    public BlurType type = BlurType.Linear;
    public BlurMethod method = BlurMethod.Gaussian;
    
    public CameraBlurSettings runtime => new CameraBlurSettings {
        Enabled = enabledBlur,
        Type = type,
        Method = method,
        Intensity = intensity,
        Clamp = clamp,
        AngleDeg = angleDeg,
        RadialCenter01 = radialCenter01,
        Downsample = downsample,
        Iterations = iterations
    };
}
