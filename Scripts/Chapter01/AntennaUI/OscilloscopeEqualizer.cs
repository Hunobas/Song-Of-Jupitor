using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// - 각 채널(3개)의 이퀄라이저 상태를 기억
/// - OnWaveDataChanged 때 펄린 노이즈 기반(0~0.8)으로 진동
/// - okFreq/okAmp/okDisp 매치 개수(1/2/3)에 따라 랜덤 3/4/5개의 슬라이더를 0.95~1.0로 상승
/// - 항상 소폭 지터(0~0.1)를 추가
/// - 채널 전환 시 저장해둔 값으로 DOTween 복귀
/// </summary>
[DisallowMultipleComponent]
public sealed class OscilloscopeEqualizer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider[] sliders; // 길이 5 고정
    
    [Header("Controllers")]
    [SerializeField] private SignalAntennaController signalAntennaController;

    [Header("Noise / Jitter")]
    [SerializeField, Min(0f)] private float noiseSpeed = 1.3f;   // 시간 스케일
    [SerializeField, Range(0f, 1f)] private float baseMax = 0.8f; // 기본 노이즈 상한
    [SerializeField, Range(0f, 0.2f)] private float jitter = 0.08f; // 항상 미세 진동 폭
    [SerializeField, Min(0f)] private float tweenDur = 0.25f;

    const int Channels = WaveConstants.CctvCount;
    const int Bands = 5;

    // 채널별 현재 5밴드 값 저장
    private readonly float[,] _values = new float[Channels, Bands];

    // 채널별 “상승(부스트)”로 고정된 밴드 인덱스 집합 (매치 상태가 바뀔 때만 갱신)
    private readonly HashSet<int>[] _boosted = new HashSet<int>[Channels];

    // 슬라이더별 펄린 노이즈 시드
    private float[,] _seeds = new float[Channels, Bands];

    // 현재 화면에 표시 중인 채널
    private int _currentChannel;

    void Awake()
    {
        if (signalAntennaController == null)
            signalAntennaController = FindFirstObjectByType<SignalAntennaController>();

        if (sliders == null || sliders.Length != Bands)
            Debug.LogError("[OscilloscopeEqualizer] 슬라이더 5개를 연결하세요.");

        for (int ch = 0; ch < Channels; ch++)
        {
            _boosted[ch] = new HashSet<int>();
            for (int b = 0; b < Bands; b++)
            {
                _values[ch, b] = 0f;
                _seeds[ch, b] = UnityEngine.Random.value * 1000f + ch * 37.1f + b * 11.7f;
            }
        }
    }

    void OnEnable()
    {
        if (signalAntennaController != null)
        {
            signalAntennaController.OnInitialized       += HandleInit;
            signalAntennaController.OnWaveDataChanged   += HandleWaveChanged;
            signalAntennaController.OnMatchStateChanged += HandleMatchStateChanged;
        }
    }

    void OnDisable()
    {
        if (signalAntennaController != null)
        {
            signalAntennaController.OnInitialized       -= HandleInit;
            signalAntennaController.OnWaveDataChanged   -= HandleWaveChanged;
            signalAntennaController.OnMatchStateChanged -= HandleMatchStateChanged;
        }
    }

    // CCTV 채널 전환 시, 저장된 값으로 Tween 복귀
    public void HandleChannelChanged(int channel)
    {
        _currentChannel = channel;
        ApplyToUI(channel, tweenDur);
    }

    // ---- 이벤트 핸들러 ----

    // 초기 파형 알림 시, 시드 기반의 초기값으로 채워둠
    private void HandleInit(int channel, WaveData _)
    {
        for (int b = 0; b < Bands; b++)
            _values[channel, b] = SampleBaseNoise(channel, b, Time.time);
        
        if (channel == _currentChannel)
            ApplyToUI(channel, tweenDur * 0.5f);
    }

    // 파형이 바뀌는 매 프레임/시점마다 노이즈 기반으로 흔들어줌(현재 채널만)
    private void HandleWaveChanged(int channel, WaveData _)
    {
        // 바뀐 채널이 현재 화면 채널이면 즉시 갱신
        if (channel != signalAntennaController.CurrrentCctvIndex)
            return;

        _currentChannel = channel;

        float t = Time.time * noiseSpeed;

        for (int b = 0; b < Bands; b++)
        {
            bool boosted = _boosted[channel].Contains(b);

            float target = boosted
                ? UnityEngine.Random.Range(0.95f, 1.0f)
                : SampleBaseNoise(channel, b, t);

            target += SmallJitter(channel, b, t);

            _values[channel, b] = Mathf.Clamp01(target);
        }

        ApplyToUI(channel, tweenDur);
    }

    // okFreq/okAmp/okDisp 상태 변화 시, 채널별로 부스트 밴드 갱신(3/4/5개)
    private void HandleMatchStateChanged(int channel, bool okFreq, bool okAmp, bool okDisp)
    {
        int trueCount = (okFreq ? 1 : 0) + (okAmp ? 1 : 0) + (okDisp ? 1 : 0);

        int boostCount = trueCount switch
        {
            0 => 0,
            1 => 3,
            2 => 4,
            _ => 5
        };

        // 고정된 랜덤 선택(채널마다 다름). 상태가 바뀔 때만 다시 뽑음.
        _boosted[channel].Clear();
        if (boostCount > 0)
        {
            List<int> pool = new List<int> { 0, 1, 2, 3, 4 };
            for (int i = 0; i < boostCount; i++)
            {
                int pick = UnityEngine.Random.Range(0, pool.Count);
                _boosted[channel].Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        // 현재 보고 있는 채널이면 즉시 한 번 갱신해 반응성을 주자
        if (channel == signalAntennaController.CurrrentCctvIndex)
            HandleWaveChanged(channel, default);
    }

    // ---- 내부 유틸 ----

    float SampleBaseNoise(int ch, int band, float t)
    {
        // 0~1 Perlin → 0~baseMax 스케일
        float v = Mathf.PerlinNoise(_seeds[ch, band], t);
        return Mathf.Clamp01(v) * baseMax;
    }

    float SmallJitter(int ch, int band, float t)
    {
        // 중심 0, 폭 ±jitter/2 정도로 미세 떨림
        float j = Mathf.PerlinNoise(_seeds[ch, band] + 77.7f, t * 2.13f);
        j = (j - 0.5f) * 2f; // -1~1
        return j * (jitter * 0.5f);
    }

    void ApplyToUI(int channel, float duration)
    {
        if (sliders == null) return;

        for (int b = 0; b < Bands; b++)
        {
            float v = Mathf.Clamp01(_values[channel, b]);
            // 슬라이더별 트윈을 독립적으로 관리 (키ill by target)
            DOTween.Kill(sliders[b], complete: false);
            sliders[b].DOValue(v, duration).SetEase(Ease.OutQuad).SetTarget(sliders[b]);
        }
    }
}
