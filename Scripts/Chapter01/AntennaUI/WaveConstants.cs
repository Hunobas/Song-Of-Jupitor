using UnityEngine;

public static class WaveConstants
{
    [Header("CCTV Button Group")] public const int CctvCount = 3;
    
    [Header("Frequency Range")]
    public const float FrequencyMin = 0.1f;
    public const float FrequencyMax = 40f;
    
    [Header("Amplitude Range")]
    public const float AmplitudeMin = 0f;
    public const float AmplitudeMax = 0.5f;
    
    [Header("Displacement Range")]
    public const float DisplacementMin = 0f;
    public const float DisplacementMax = 2*Mathf.PI;
    
    [Header("UI Control Ranges")]
    public const float HandleMin = 0f;
    public const float HandleMax = 100f;
    
    [Header("Answer(Phase1) Tolerance")]
    public const float FrequencyTolerance = 0.6f;
    public const float AmplitudeTolerance = 0.02f;
    public const float DisplacementTolerance = 0.2f;
    
    [Header("Answer(Phase2) Tolerance")]
    public const float Combine_KO_Length = 10f;
    
    // 정답 원뿔의 반경(=오차 허용치)을 K(꼭짓점)~G''(최대높이) 사이에서 선형 보간
    public const float Combine_ConeRadiusAtApex = 8f;
    public const float Combine_ConeRadiusAtMax  = 20f;
    public const float Combine_HintFarAlpha     = 0.20f;
    public const float Combine_HintNearAlpha    = 1f;
    
    [Header("CCTV1 Initial Value")]
    public const float CCTV1_FrequencyInit = 5.7f;
    public const float CCTV1_AmplitudeInit = 0.392f;
    public const float CCTV1_DisplacementInit = 6.28f;
    [Header("CCTV2 Initial Value")]
    public const float CCTV2_FrequencyInit = 15f;
    public const float CCTV2_AmplitudeInit = 0.118f;
    public const float CCTV2_DisplacementInit = 1f;
    [Header("CCTV3 Initial Value")]
    public const float CCTV3_FrequencyInit = 15f;
    public const float CCTV3_AmplitudeInit = 0.274f;
    public const float CCTV3_DisplacementInit = 3.44f;
    
    [Header("Animation")]
    public static float SingleScrollSpeed = 2.5f;
    public static float CombineScrollSpeed = 1f;
    public static float SinglePhase => (Time.unscaledTime * SingleScrollSpeed) % (2f * Mathf.PI);
    public static float CombinePhase => (Time.unscaledTime * CombineScrollSpeed) % (2f * Mathf.PI);
    
    // 정규화 유틸리티 메서드
    public static float NormalizeFrequency(float value) => 
        Mathf.InverseLerp(FrequencyMin, FrequencyMax, value);
    
    public static float NormalizeAmplitude(float value) => 
        Mathf.InverseLerp(AmplitudeMin, AmplitudeMax, value);
    
    public static float NormalizeDisplacement(float value) => 
        Mathf.InverseLerp(DisplacementMin, DisplacementMax, value);
    
    // 역정규화 유틸리티 메서드
    public static float DenormalizeFrequency(float normalizedValue) => 
        Mathf.Lerp(FrequencyMin, FrequencyMax, normalizedValue);
    
    public static float DenormalizeAmplitude(float normalizedValue) => 
        Mathf.Lerp(AmplitudeMin, AmplitudeMax, normalizedValue);
    
    public static float DenormalizeDisplacement(float normalizedValue) => 
        Mathf.Lerp(DisplacementMin, DisplacementMax, normalizedValue);
}