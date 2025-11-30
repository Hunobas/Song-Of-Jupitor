using UnityEngine;
using System;
using DG.Tweening;
using UnityEngine.Serialization;

public class SignalAntennaController : MonoBehaviour
{
    [Header("Shared Store")]
    [SerializeField] private SignalWaveData _waveStore;

    [Header("Refs")]
    [SerializeField] private SignalAntennaPage _page;
    [SerializeField] public OscilloscopeController OscilloscopeController;
    [SerializeField] private OscilloscopeEqualizer _equalizer;
    [SerializeField] public CombineFilterController CombineFilterController;
    [SerializeField] private ScreenGlitch _sg;

    [Header("Tolerance")]
    [SerializeField] public float FrequencyTolerance = WaveConstants.FrequencyTolerance;
    [SerializeField] public float AmplitudeTolerance = WaveConstants.AmplitudeTolerance;
    [SerializeField] public float DisplacementTolerance = WaveConstants.DisplacementTolerance;
    [SerializeField] public float Combine_MinRadius = WaveConstants.Combine_ConeRadiusAtApex;
    [SerializeField] public float Combine_MaxRadius = WaveConstants.Combine_ConeRadiusAtMax;

    [Header("Current State")]
    [SerializeField] private CctvController _cctvController;
    [SerializeField] private int _currentCCTVIndex = 0;

    private bool[] _solved = new bool[WaveConstants.CctvCount];
    private bool _allSolvedPrev = false;
    private readonly bool[] _okFreqPrev = new bool[WaveConstants.CctvCount];
    private readonly bool[] _okAmpPrev  = new bool[WaveConstants.CctvCount];
    private readonly bool[] _okDispPrev = new bool[WaveConstants.CctvCount];
    private readonly bool[,] _okCacheMatrix = new bool[WaveConstants.CctvCount, WaveConstants.CctvCount];

    public int CurrrentCctvIndex => _currentCCTVIndex;
    public bool[] Solved => _solved;

    public event Action<int, WaveData> OnInitialized;
    public event Action<int> OnCCTVChanged;
    public event Action<int, WaveData> OnWaveDataChanged;
    public event Action<int, bool> OnSolvedStateChanged;
    public event Action<int, bool, bool, bool> OnMatchStateChanged; 
    public event Action<bool> OnPhase1Solved;
    public event Action<bool> OnPhase2Solved;

    private void Awake()
    {
        EnsureStoreDefaults();
    }

    private void Start()
    {
        BroadcastAllInitialWaves();

        var init = _waveStore.GetWave(_currentCCTVIndex);
        OscilloscopeController?.SetValues(init.Frequency, init.AmplitudeNoRender, init.DisplacementNoRender);
        CombineFilterController?.SetupWithStore(_waveStore, Combine_MinRadius, Combine_MaxRadius);
        OnWaveDataChanged?.Invoke(_currentCCTVIndex, init);
        SelectCCTV(_currentCCTVIndex);
        
        DOTween.Sequence()
            .SetLink(gameObject)
            .AppendInterval(UnityEngine.Random.Range(7f, 8f))
            .AppendCallback(() =>
            {
                _sg?.HackedOneShot(0.1f, 0.5f);
            })
            .SetLoops(-1, LoopType.Restart);
    }

#if DEVBUILD
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            int ch = _currentCCTVIndex;
            if (ch < 0 || ch >= WaveConstants.CctvCount) return;

            WaveData answer = _waveStore.GetAnswer(ch);
            _waveStore.SetWave(ch, answer);

            _okFreqPrev[ch] = _okAmpPrev[ch] = _okDispPrev[ch] = true;
            _okCacheMatrix[ch, 0] = _okCacheMatrix[ch, 1] = _okCacheMatrix[ch, 2] = true;
            _solved[ch] = true;

            OnMatchStateChanged?.Invoke(ch, true, true, true);
            OnSolvedStateChanged?.Invoke(ch, true);
            OnWaveDataChanged?.Invoke(ch, answer);

            if (OscilloscopeController != null)
            {
                OscilloscopeController.SetValues(answer.Frequency, answer.AmplitudeNoRender, answer.DisplacementNoRender);
                OscilloscopeController.ForceRefreshAllVisuals();
            }

            _sg?.HackedOneShot(0.25f, 0.8f);
            bool allSolvedNow = _solved[0] && _solved[1] && _solved[2];
            if (allSolvedNow != _allSolvedPrev)
            {
                _allSolvedPrev = allSolvedNow;
                OnPhase1Solved?.Invoke(allSolvedNow);
            }
        }
    }
#endif

    private void BroadcastAllInitialWaves()
    {
        for (int ch = 0; ch < WaveConstants.CctvCount; ch++)
        {
            var w = _waveStore.GetWave(ch);
            OnInitialized?.Invoke(ch, w);
        }
    }

    private void EnsureStoreDefaults()
    {
        for (int i = 0; i < WaveConstants.CctvCount; i++)
        {
            WaveData init = i switch
            {
                0 => new WaveData { Frequency = WaveConstants.CCTV1_FrequencyInit, AmplitudeNoRender = WaveConstants.CCTV1_AmplitudeInit, DisplacementNoRender = WaveConstants.CCTV1_DisplacementInit },
                1 => new WaveData { Frequency = WaveConstants.CCTV2_FrequencyInit, AmplitudeNoRender = WaveConstants.CCTV2_AmplitudeInit, DisplacementNoRender = WaveConstants.CCTV2_DisplacementInit },
                2 => new WaveData { Frequency = WaveConstants.CCTV3_FrequencyInit, AmplitudeNoRender = WaveConstants.CCTV3_AmplitudeInit, DisplacementNoRender = WaveConstants.CCTV3_DisplacementInit },
                _ => WaveData.Default
            };
            _waveStore.SetWave(i, init);

            var ans = _waveStore.GetAnswer(i);
            if (ans.Frequency <= 0f && ans.AmplitudeNoRender <= 0f && Mathf.Approximately(ans.DisplacementNoRender, 0f))
                _waveStore.SetAnswer(i, init);
        }
    }

    public void SelectCCTV(int index)
    {
        if (index < 0 || index >= WaveConstants.CctvCount) return;

        _currentCCTVIndex = index;
        _cctvController.SwitchCam(_currentCCTVIndex + 1,show =>
        {
            _equalizer.gameObject.SetActive(show);
            if (show) _equalizer.HandleChannelChanged(_currentCCTVIndex);
        });
        RestoreOscilloscopeValues();
        OnCCTVChanged?.Invoke(_currentCCTVIndex);
    }

    private void RestoreOscilloscopeValues()
    {
        WaveData data = _waveStore.GetWave(_currentCCTVIndex);
        OscilloscopeController?.SetValues(data.Frequency, data.AmplitudeNoRender, data.DisplacementNoRender);
    }

    public void UpdateCurrentWaveData(float frequency, float amplitude, float displacement)
    {
        // 1) 먼저 런타임 값을 저장
        var data = new WaveData { Frequency = frequency, AmplitudeNoRender = amplitude, DisplacementNoRender = displacement };
        _waveStore.SetWave(_currentCCTVIndex, data);

        // 2) 토럴런스 안으로 들어온 파라미터는 "정답으로 스냅" (store 내부 값이 바뀔 수 있음)
        TryCheckAnswer(_currentCCTVIndex);

        // 3) 스냅 후의 실제 값을 다시 읽어서 UI/이벤트에 사용 (진짜 '스냅된 값'을 브로드캐스트)
        var after = _waveStore.GetWave(_currentCCTVIndex);

        OscilloscopeController?.SetValues(frequency, amplitude, displacement);

        // 외부 구독자에게도 스냅된 값으로 알림
        OnWaveDataChanged?.Invoke(_currentCCTVIndex, after);
    }
    
    public WaveData GetAnswer(int index)
    {
        return _waveStore.GetAnswer(index);
    }
    
    public WaveData GetCurrent(int index)
    {
        return _waveStore.GetWave(index);
    }
    
#if UNITY_EDITOR
    public void ResetPuzzle()
    {
        // 0) 트윈/캐시 정리
        DOTween.Kill(gameObject);
        Array.Clear(_solved, 0, _solved.Length);
        Array.Clear(_okFreqPrev, 0, _okFreqPrev.Length);
        Array.Clear(_okAmpPrev,  0, _okAmpPrev.Length);
        Array.Clear(_okDispPrev, 0, _okDispPrev.Length);
        for (int r = 0; r < WaveConstants.CctvCount; r++)
            for (int c = 0; c < 3; c++)
                _okCacheMatrix[r, c] = false;

        _allSolvedPrev = false;

        EnsureStoreDefaults();
        _currentCCTVIndex = 0;
        Start();
        OscilloscopeController.SetAllInteractable(true);
        _page.Initialize();

        OnSolvedStateChanged?.Invoke(0, false);
        OnSolvedStateChanged?.Invoke(1, false);
        OnSolvedStateChanged?.Invoke(2, false);
        OnPhase1Solved?.Invoke(false);
    }
#endif

    private void TryCheckAnswer(int index)
    {
        var cur    = _waveStore.GetWave(index);
        var target = _waveStore.GetAnswer(index);

        // 토럴런스 판정 (각 파라미터 독립)
        bool okFreq = Mathf.Abs(cur.Frequency  - target.Frequency)        <= FrequencyTolerance;
        bool okAmp  = Mathf.Abs(cur.AmplitudeNoRender  - target.AmplitudeNoRender)        <= AmplitudeTolerance;
        bool okDisp = Mathf.Abs(cur.DisplacementNoRender  - target.DisplacementNoRender)  <= DisplacementTolerance;
        
        // 씨씨티비 화면에 딱 한번 해킹 이벤트 (총 9번)
        bool[] oks = { okFreq, okAmp, okDisp };
        for (int i = 0; i < oks.Length; i++)
        {
            if (oks[i] && !_okCacheMatrix[_currentCCTVIndex, i])
            {
                _sg?.HackedOneShot(0.2f, 1f);
                _okCacheMatrix[_currentCCTVIndex, i] = true;
            }
        }

        if (okFreq != _okFreqPrev[index] || okAmp != _okAmpPrev[index] || okDisp != _okDispPrev[index])
        {
            _okFreqPrev[index] = okFreq;
            _okAmpPrev[index]  = okAmp;
            _okDispPrev[index] = okDisp;
            OnMatchStateChanged?.Invoke(index, okFreq, okAmp, okDisp);
        }

        // 최종 해결 여부 판단(스냅 후 기준) + 이벤트
        bool solvedNow = okFreq && okAmp && okDisp;

        if (solvedNow != _solved[index])
        {
            _solved[index] = solvedNow;
            OnSolvedStateChanged?.Invoke(index, solvedNow);
        }

        bool allSolvedNow = _solved[0] && _solved[1] && _solved[2];
        if (allSolvedNow != _allSolvedPrev)
        {
            _allSolvedPrev = allSolvedNow;
            OnPhase1Solved?.Invoke(allSolvedNow);
        }
    }

    public void Phase2Solved(bool solved)
    {
        OnPhase2Solved?.Invoke(solved);
    }
}
