using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using DG.Tweening;
using Unity.VisualScripting;

public enum SoundPriority
{
    Low = 0,
    Normal = 50,
    High = 100,
    Critical = 200
}

public enum OverlapPolicy
{
    Override,               // 기존 오디오를 멈추고 다음 재생할 오디오를 재생
    Ignore,                 // 다음 재생할 오디오 무시하고 기존 오디오 마저 재생
    Crossfade,              // 기존 오디오의 남은 재생 시간동안 기존 오디오 페이드아웃 + 다음 오디오 페이드인
    OneShot                 // 다음 오디오를 원샷으로 재생. 원샷 오디오는 댕글링 객체이기 때문에 관리대상 밖, Duck 안먹음  
}

[Flags]
public enum PlayOptions
{
    None = 0,
    PlayOnAwake = 1 << 0,
    Loop = 1 << 1,
    SkipIfSame = 1 << 2,
    DuckOthers = 1 << 3,
    // TODO: 2DSound
}

[RequireComponent(typeof(AudioSource))]
public class SoundSource : MonoBehaviour
{
    private const string PoolTag = InGameContants.ObjectPoolTagName.SFXObj;

    [Title("Audio Source")]
    [SerializeField, Required, ChildGameObjectsOnly]
    private AudioSource _primaryAudioSource;

    [Title("Clip List")]
    [SerializeField]
    private AudioClip[] _clips;

    [Title("Default Settings")]
    [SerializeField]
    private SoundPriority _priority = SoundPriority.Normal;

    [SerializeField]
    private PlayOptions _options = PlayOptions.None;

    [SerializeField]
    private OverlapPolicy _overlapPolicy = OverlapPolicy.Override;

    [SerializeField, Min(0f)]
    private float _defaultFadeInDuration = 0.2f;

    [SerializeField, Min(0f)]
    private float _defaultFadeOutDuration = 0.2f;

    [Title("Debug")]
    [ShowInInspector]
    public bool IsPlaying => CurrentEntry != null && CurrentEntry.IsPlaying;
    
    [ShowInInspector]
    public SoundEntry CurrentEntry { get; private set; }

    public AudioSource PrimaryAudioSource => _primaryAudioSource;
    public float BaseVolume { get; private set; }
    public float BasePitch { get; private set; }
    public float LoopCrossfadeDuration => Mathf.Min(_defaultFadeInDuration, _defaultFadeOutDuration);

    private OverlapPolicy _defaultOverlapPolicy;
    private readonly List<AudioSource> _activePooledSources = new();
    private Tweener _primaryFadeTweener;
    private bool _isPaused;
    private bool _isDucked;
    private float _preDuckVolume;

    private void Awake()
    {
        Debug.Assert(_primaryAudioSource != null, $"[{gameObject.name}] AudioSource가 연결되지 않았습니다.");
        BaseVolume = _primaryAudioSource.volume;
        BasePitch = _primaryAudioSource.pitch;
        _defaultOverlapPolicy = _overlapPolicy;

        if (_options.HasFlag(PlayOptions.PlayOnAwake))
        {
            Play();
        }
    }

    private void OnEnable() => SoundManager.Instance?.Register(this);
    private void OnDisable() => SoundManager.Instance?.Unregister(this);

    public SoundEntry Play()
    {
        Debug.Assert(_primaryAudioSource.clip != null, $"[{gameObject.name}] AudioSource에 기본 클립이 없습니다.");
        return CreateEntry(_primaryAudioSource.clip);
    }
    public void InvokePlay() => Play();

    public SoundEntry Play(AudioClip clip)
    {
        Debug.Assert(clip != null, "AudioClip이 null입니다.");
        return CreateEntry(clip);
    }
    public void InvokePlay(AudioClip clip) => Play(clip);
    public void InvokePlayOneShot(AudioClip clip) => Play(clip).WithOverlapPolicy(OverlapPolicy.OneShot);
    public void InvokePlayCrossfade(AudioClip clip) => Play(clip).WithOverlapPolicy(OverlapPolicy.Crossfade);

    public SoundEntry PlayRandom()
    {
        if (_clips == null || _clips.Length == 0)
        {
            Debug.Assert(_primaryAudioSource.clip != null, $"[{gameObject.name}] 클립 리스트가 비어있고 기본 클립도 없습니다.");
            return CreateEntry(_primaryAudioSource.clip);
        }

        var selected = _clips[UnityEngine.Random.Range(0, _clips.Length)];
        Debug.Assert(selected != null, $"[{gameObject.name}] 선택된 클립이 null입니다.");
        return CreateEntry(selected);
    }
    public void InvokePlayRandom() => PlayRandom();

    public SoundEntry PlayByNameOrNull(string clipName)
    {
        if (_clips == null || _clips.Length == 0)
            return null;

        foreach (var clip in _clips)
        {
            if (clip != null && clip.name == clipName)
                return CreateEntry(clip);
        }

        return null;
    }
    public void InvokePlayByNameOrNull(string clipName) => PlayByNameOrNull(clipName);
    
    public void Stop(float fadeOutDuration = -1f)
    {
        if (CurrentEntry == null || CurrentEntry.IsFinished)
            return;
        
        CurrentEntry.Stop(fadeOutDuration);
        CurrentEntry = null;

        _primaryFadeTweener?.Kill();
        _primaryFadeTweener = null;
        
        if (_primaryAudioSource == null)
            return;

        _primaryAudioSource.volume = BaseVolume;
        _primaryAudioSource.pitch = BasePitch;
        _isPaused = false;

        foreach (var pooled in _activePooledSources)
        {
            if (pooled == null)
                continue;

            if (fadeOutDuration > 0f)
            {
                pooled.DOFade(0f, fadeOutDuration)
                    .SetLink(pooled.gameObject)
                    .OnComplete(() =>
                    {
                        if (pooled != null)
                        {
                            pooled.Stop();
                            ObjectPoolManager.Instance?.ReturnSpawnObj(PoolTag, pooled.gameObject);
                        }
                    });
            }
            else
            {
                pooled.Stop();
                ObjectPoolManager.Instance?.ReturnSpawnObj(PoolTag, pooled.gameObject);
            }
        }
        _activePooledSources.Clear();
    }

    public void Pause()
    {
        if (_isPaused)
            return;

        _isPaused = true;
        _primaryAudioSource.Pause();
        _primaryFadeTweener?.Pause();
    }

    public void Resume()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        _primaryAudioSource.UnPause();
        _primaryFadeTweener?.Play();
    }

    public void SetVolume(float volume)
    {
        BaseVolume = volume;
        _primaryAudioSource.volume = volume;
    }

    public void SetPitch(float pitch)
    {
        float clamped = Mathf.Clamp(pitch, 0.1f, 3f);
        BasePitch = clamped;

        _primaryAudioSource.pitch = clamped;
        CurrentEntry.Pitch = clamped;
    }

    private SoundEntry CreateEntry(AudioClip clip)
    {
        var entry = new SoundEntry(
            this, clip,
            _options, _priority, _overlapPolicy,
            BasePitch, _defaultFadeInDuration, _defaultFadeOutDuration
        );

        // 메소드 체이닝을 위해 1틱 늦게 딜레이를 줘서 실행하자..
        // 다만 사운드가 의도보다 1프레임 늦게 재생됨. 리듬게임 요소를 넣으면 아마도 확장 혹은 재설계 필요.
        DOVirtual.DelayedCall(0f, () => ExecuteEntry(entry), false).SetLink(gameObject);
        return entry;
    }

    private void ExecuteEntry(SoundEntry entry)
    {
        if (entry.IsFinished)
            return;

        var policy = entry.OverlapPolicy;
        var prevEntry = CurrentEntry;
        bool hasPrevious = prevEntry != null && prevEntry.IsPlaying;

        if (policy == OverlapPolicy.Ignore && hasPrevious)
        {
            entry.ForceFinish();
            return;
        }

        // 기존 재생 중이던 오디오 current 유지, 원샷 재생 오디오는 생성되자마자 댕글링 객체.
        if (entry.OverlapPolicy == OverlapPolicy.OneShot)
        {
            _primaryAudioSource.PlayOneShot(entry.Clip, BaseVolume);
            DOVirtual.DelayedCall(entry.ClipLength, entry.ForceFinish, false).SetLink(gameObject);
            CurrentEntry ??= entry;
            return;
        }

        // 기존 재생 중이던 오디오에서 크로스페이드 오디오로 current 변경, 기존 오디오는 댕글링 객체.
        if (policy == OverlapPolicy.Crossfade && hasPrevious)
        {
            CrossfadeTo(entry, prevEntry.RemainingTime);
            return;
        }

        prevEntry?.ForceFinish();
        ResetPrimary();
        CurrentEntry = entry;
        entry.Play(_primaryAudioSource);
    }
    
    // 페이드아웃 오디오는 풀에서 가져와서 댕글링 객체로 격하.
    internal void CrossfadeTo(SoundEntry next, float duration)
    {
        if (next.ClipLength < duration)
            return;
        
        AudioSource pooled = ObjectPoolManager.Instance.SpawnObj(PoolTag, go => go.SetActive())?.GetComponent<AudioSource>();
        if (!pooled)
            return;

        pooled.transform.position = transform.position;
        CopyAudioSourceComponent(_primaryAudioSource, pooled);
        _activePooledSources.Add(pooled);

        SoundEntry prev = CurrentEntry;
        prev.TransferTo(pooled);
        pooled.Play();

        prev.ScheduleEnd(duration, () =>
        {
            prev.ForceFinish();
            ObjectPoolManager.Instance.ReturnSpawnObj(PoolTag, pooled.gameObject);
        }, pooled.gameObject);
        next.WithFadeIn(duration).WithOverlapPolicy(_defaultOverlapPolicy);
        
        ResetPrimary();
        CurrentEntry = next;
        next.Play(_primaryAudioSource);
    }

    private void ResetPrimary()
    {
        _primaryFadeTweener?.Kill();
        _primaryFadeTweener = null;
        _primaryAudioSource.Stop();
        _primaryAudioSource.volume = BaseVolume;
        _primaryAudioSource.pitch = BasePitch;
    }

    internal void Duck(float targetVolume, float duration)
    {
        if (_isDucked)
            return;

        _isDucked = true;
        _preDuckVolume = _primaryAudioSource.volume;

        _primaryFadeTweener?.Kill();
        _primaryFadeTweener = _primaryAudioSource.DOFade(targetVolume, duration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }

    internal void Unduck(float duration)
    {
        if (!_isDucked)
            return;

        _isDucked = false;

        _primaryFadeTweener?.Kill();
        _primaryFadeTweener = _primaryAudioSource.DOFade(_preDuckVolume, duration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject);
    }
    
    private static void CopyAudioSourceComponent(AudioSource src, AudioSource dst)
    {
        dst.clip = src.clip;
        dst.time = Mathf.Abs(src.time % src.clip.length);
        dst.outputAudioMixerGroup = src.outputAudioMixerGroup;
        dst.bypassEffects = src.bypassEffects;
        dst.bypassListenerEffects = src.bypassListenerEffects;
        dst.bypassReverbZones = src.bypassReverbZones;
        dst.playOnAwake = src.playOnAwake;
        dst.loop = src.loop = false;
        dst.priority = src.priority;
        dst.volume = src.volume;
        dst.pitch = src.pitch;
        dst.panStereo = src.panStereo;
        dst.spatialBlend = src.spatialBlend;
        dst.reverbZoneMix = src.reverbZoneMix;
        dst.dopplerLevel = src.dopplerLevel;
        dst.spread = src.spread;
        dst.rolloffMode = src.rolloffMode;
        dst.minDistance = src.minDistance;
        dst.maxDistance = src.maxDistance;
        dst.SetCustomCurve(AudioSourceCurveType.CustomRolloff, src.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _primaryAudioSource = GetComponent<AudioSource>();
        if (_primaryAudioSource == null)
            _primaryAudioSource = gameObject.AddComponent<AudioSource>();
        _primaryAudioSource.playOnAwake = false;
    }

    private void OnValidate()
    {
        if (_primaryAudioSource == null)
            _primaryAudioSource = GetComponent<AudioSource>();
    }
#endif
}