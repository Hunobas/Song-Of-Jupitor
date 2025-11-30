using UnityEngine;

public class AntennaRigDriver : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    [SerializeField] private SignalAntennaController controller;

    // 채널별로 연결할 본들 (배열 인덱스 = 채널 인덱스)
    [SerializeField] private Transform[] yawBones;    // e.g. Bone  (Yaw)
    [SerializeField] private Transform[] pitchBones;  // e.g. Bone.002 (Pitch)

    [Header("Local Axes")]
    [SerializeField] private Axis yawAxis = Axis.Y;
    [SerializeField] private Axis pitchAxis = Axis.X;

    [Header("Angle Limits (deg)")]
    [SerializeField] private float yawMinDeg   = -60f;
    [SerializeField] private float yawMaxDeg   =  60f;
    [SerializeField] private float pitchMinDeg = -20f;
    [SerializeField] private float pitchMaxDeg = -75f; // 스샷과 동일(더 음수로 숙임)

    [Header("Speed (deg/sec)")]
    [SerializeField] private float yawSpeedDeg   = 15f;
    [SerializeField] private float pitchSpeedDeg = 12f;

    [Header("Smoothing")]
    [SerializeField] private bool  useLowPass = true;
    [SerializeField, Range(0f,1f)] private float lpAlpha = 0.15f;

    [Header("Init")]
    [SerializeField] private bool snapOnFirstPacket = true;

    // 채널별 상태
    float[] _yawTarget, _pitchTarget;
    float[] _yawFiltered, _pitchFiltered;
    bool[]  _gotFirstPacket;

    int ChannelCount => Mathf.Max(
        WaveConstants.CctvCount,
        yawBones != null ? yawBones.Length : 0,
        pitchBones != null ? pitchBones.Length : 0
    );

    void OnEnable()
    {
        EnsureBuffers();

        if (controller != null)
        {
            controller.OnInitialized     += HandleInitPacket;    // 초기 스냅
            controller.OnWaveDataChanged += HandleWaveChanged;   // 이후 목표각 갱신
        }
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.OnInitialized     -= HandleInitPacket;
            controller.OnWaveDataChanged -= HandleWaveChanged;
        }
    }

    void Start()
    {
        // 현재 본 각도로 필터 초기화 (채널별)
        for (int ch = 0; ch < ChannelCount; ch++)
        {
            var yBone = GetYawBone(ch);
            var pBone = GetPitchBone(ch);

            _yawFiltered[ch]   = yBone ? GetSignedLocalAngle(yBone, yawAxis)   : 0f;
            _pitchFiltered[ch] = pBone ? GetSignedLocalAngle(pBone, pitchAxis) : 0f;

            _yawTarget[ch]   = _yawFiltered[ch];
            _pitchTarget[ch] = _pitchFiltered[ch];
        }
    }

    void EnsureBuffers()
    {
        int n = ChannelCount;
        _yawTarget     = _yawTarget     ?? new float[n];
        _pitchTarget   = _pitchTarget   ?? new float[n];
        _yawFiltered   = _yawFiltered   ?? new float[n];
        _pitchFiltered = _pitchFiltered ?? new float[n];
        _gotFirstPacket= _gotFirstPacket?? new bool[n];

        // 길이가 부족하면 리사이즈
        void Resize<T>(ref T[] arr)
        {
            if (arr.Length != n)
            {
                var old = arr;
                arr = new T[n];
                int copy = Mathf.Min(old.Length, n);
                System.Array.Copy(old, arr, copy);
            }
        }
        Resize(ref _yawTarget);
        Resize(ref _pitchTarget);
        Resize(ref _yawFiltered);
        Resize(ref _pitchFiltered);
        Resize(ref _gotFirstPacket);
    }

    // === 초기 1회 패킷: 해당 채널만 즉시 스냅 ===
    void HandleInitPacket(int ch, WaveData data)
    {
        if (!IsValidChannel(ch)) return;

        ComputeTargets(data, out var yawT, out var pitchT);

        if (snapOnFirstPacket)
        {
            _yawFiltered[ch]   = yawT;
            _pitchFiltered[ch] = pitchT;
            _yawTarget[ch]     = yawT;
            _pitchTarget[ch]   = pitchT;

            var yBone = GetYawBone(ch);
            var pBone = GetPitchBone(ch);
            if (yBone) SetLocalAxisAngle(yBone, yawAxis,   yawT);
            if (pBone) SetLocalAxisAngle(pBone, pitchAxis, pitchT);
        }

        _gotFirstPacket[ch] = true;
    }

    // === 이후 연속 업데이트: 채널별 목표각만 갱신 ===
    void HandleWaveChanged(int ch, WaveData data)
    {
        if (!IsValidChannel(ch)) return;

        ComputeTargets(data, out var yawT, out var pitchT);

        if (!_gotFirstPacket[ch] && snapOnFirstPacket)
        {
            HandleInitPacket(ch, data);
            return;
        }

        _yawTarget[ch]   = yawT;
        _pitchTarget[ch] = pitchT;
    }

    void Update()
    {
        if (controller == null)
            return;

        int ch = Mathf.Clamp(controller.CurrrentCctvIndex, 0, ChannelCount - 1);
        if (!_gotFirstPacket[ch] && !snapOnFirstPacket)
            return;

        // 1) 목표 저역통과
        float targetYaw   = useLowPass ? Mathf.LerpAngle(_yawFiltered[ch],   _yawTarget[ch],   lpAlpha) : _yawTarget[ch];
        float targetPitch = useLowPass ? Mathf.LerpAngle(_pitchFiltered[ch], _pitchTarget[ch], lpAlpha) : _pitchTarget[ch];
        _yawFiltered[ch]   = targetYaw;
        _pitchFiltered[ch] = targetPitch;

        // 2) 정속 추종 (현재 선택 채널의 본만 움직임)
        var yBone = GetYawBone(ch);
        var pBone = GetPitchBone(ch);

        if (yBone)
        {
            float cur  = GetSignedLocalAngle(yBone, yawAxis);
            float next = Mathf.MoveTowardsAngle(cur, targetYaw, yawSpeedDeg * Time.deltaTime);
            SetLocalAxisAngle(yBone, yawAxis, next);
        }
        if (pBone)
        {
            float cur  = GetSignedLocalAngle(pBone, pitchAxis);
            float next = Mathf.MoveTowardsAngle(cur, targetPitch, pitchSpeedDeg * Time.deltaTime);
            SetLocalAxisAngle(pBone, pitchAxis, next);
        }
    }

    // ====== 값→목표각 (버그 수정 포함) ======
    void ComputeTargets(WaveData data, out float yawT, out float pitchT)
    {
        float a01 = Mathf.Clamp01(WaveConstants.NormalizeAmplitude(data.AmplitudeNoRender));
        a01 = Mathf.Clamp01(a01);
        yawT = Mathf.Lerp(yawMinDeg, yawMaxDeg, a01);

        float disp = Mathf.Clamp(data.DisplacementNoRender, 0f, Mathf.PI * 2f);
        float d01  = Mathf.InverseLerp(0f, Mathf.PI * 2f, disp);
        d01 = Mathf.Clamp01(d01);
        pitchT = Mathf.Lerp(pitchMinDeg, pitchMaxDeg, d01);
    }

    // ===== 유틸 =====
    bool IsValidChannel(int ch) => ch >= 0 && ch < ChannelCount;

    Transform GetYawBone(int ch)
    {
        if (yawBones == null || yawBones.Length == 0) return null;
        return ch < yawBones.Length ? yawBones[ch] : yawBones[yawBones.Length - 1]; // 부족하면 마지막 것을 재사용(선택)
    }

    Transform GetPitchBone(int ch)
    {
        if (pitchBones == null || pitchBones.Length == 0) return null;
        return ch < pitchBones.Length ? pitchBones[ch] : pitchBones[pitchBones.Length - 1];
    }

    static float GetSignedLocalAngle(Transform t, Axis axis)
    {
        var e = t.localEulerAngles;
        float raw = axis == Axis.X ? e.x : axis == Axis.Y ? e.y : e.z;
        return Mathf.DeltaAngle(0f, raw); // 0..360 → -180..180
    }

    static void SetLocalAxisAngle(Transform t, Axis axis, float degrees)
    {
        var e = t.localEulerAngles;
        if      (axis == Axis.X) e.x = degrees;
        else if (axis == Axis.Y) e.y = degrees;
        else                     e.z = degrees;
        t.localEulerAngles = e;
    }
}
