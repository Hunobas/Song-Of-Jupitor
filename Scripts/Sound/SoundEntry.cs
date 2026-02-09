using System;
using DG.Tweening;
using UnityEngine;

public class SoundEntry
{
    public AudioClip Clip { get; }
    public SoundPriority Priority { get; set; }
    public PlayOptions Options { get; private set; }
    public OverlapPolicy OverlapPolicy { get; set; }
    public bool IsFinished { get; private set; }
    public bool IsPlaying => _isPlaying && !IsFinished;
    public float ClipLength => _clipLength;

    public float FadeInDuration { get; set; }
    public float FadeOutDuration { get; set; }
    public float StretchLength { get; set; }
    public int LoopCount { get; private set; } = 0;
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = value;
            _clipLength = Clip.length / Mathf.Max(0.01f, Mathf.Abs(_pitch));
        }
    }

    public Action OnPlayCallback { get; set; }
    public Action OnFinishCallback { get; set; }

    public float RemainingTime
    {
        get
        {
            if (!_isPlaying || _audioSource == null)
                return 0f;
            return Mathf.Max(0f, _clipLength - _audioSource.time);
        }
    }

    private readonly SoundSource _source;
    private AudioSource _audioSource;
    private float _clipLength;
    private float _pitch;
    private bool _isPlaying;
    private Tween _scheduler;
    private Tween _fade;

    internal SoundEntry(
        SoundSource source, AudioClip clip,
        PlayOptions baseOptions, SoundPriority basePriority, OverlapPolicy baseOverlapPolicy,
        float basePitch, float baseFadeIn, float baseFadeOut)
    {
        Debug.Assert(source != null, "SoundSource가 null입니다.");
        Debug.Assert(clip != null, "AudioClip이 null입니다.");

        _source = source;
        Clip = clip;
        Options = baseOptions;
        Priority = basePriority;
        OverlapPolicy = baseOverlapPolicy;
        FadeInDuration = baseFadeIn;
        FadeOutDuration = baseFadeOut;
        Pitch = basePitch;
    }
    
    internal SoundEntry(SoundEntry src)
    {
        _source = src._source;
        Clip = src.Clip;
        Priority = src.Priority;
        Options = src.Options;
        OverlapPolicy = src.OverlapPolicy;
        FadeInDuration = src.FadeInDuration;
        FadeOutDuration = src.FadeOutDuration;
        StretchLength = src.StretchLength;
        Pitch = src.Pitch;
        LoopCount = src.LoopCount;
        OnPlayCallback = src.OnPlayCallback;
        OnFinishCallback = src.OnFinishCallback;

        _audioSource = null;
        _fade = null;
        _scheduler = null;
        _isPlaying = false;
        IsFinished = false;
    }

    public SoundEntry WithOptions(PlayOptions options)
    {
        Options |= options;
        return this;
    }

    public SoundEntry WithoutOptions(PlayOptions options)
    {
        Options &= ~options;
        return this;
    }

    public SoundEntry WithFadeIn(float duration)
    {
        FadeInDuration = duration;
        return this;
    }

    public SoundEntry WithFadeOut(float duration)
    {
        FadeOutDuration = duration;
        return this;
    }

    public SoundEntry WithOverlapPolicy(OverlapPolicy policy)
    {
        OverlapPolicy = policy;
        return this;
    }

    public SoundEntry WithStretchLength(float stretchLength)
    {
        StretchLength = stretchLength;
        if (StretchLength > 0f) Pitch = Mathf.Clamp(Clip.length / StretchLength, 0.1f, 3f);     // crossfade 시 길이 검사를 하므로 미리 업데이트 해놓자..
        return this;
    }

    public SoundEntry WithPriority(SoundPriority priority)
    {
        Priority = priority;
        return this;
    }

    // loopCount만큼 재생, -1이면 무한반복 (DoTween처럼)
    public SoundEntry WithLoop(int loopCount = -1)
    {
        LoopCount = loopCount;
        return this;
    }

    public SoundEntry OnPlay(Action callback)
    {
        OnPlayCallback = callback;
        return this;
    }

    public SoundEntry OnFinish(Action callback)
    {
        OnFinishCallback = callback;
        return this;
    }

    internal void Play(AudioSource audioSource)
    {
        if (_isPlaying)
            return;
        
        if (Options.HasFlag(PlayOptions.SkipIfSame) && SoundManager.Instance.IsClipPlaying(Clip))
        {
            ForceFinish();
            return;
        }

        _isPlaying = true;
        _audioSource = audioSource;

        if (Options.HasFlag(PlayOptions.Loop))
        {
            LoopCount = -1;
        }
        if (Options.HasFlag(PlayOptions.DuckOthers))
        {
            SoundManager.Instance.DuckAllExcept(this);
        }

        SoundManager.Instance.TrackEntry(this);

        _audioSource.pitch = _pitch;
        _audioSource.clip = Clip;
        _audioSource.loop = false;

        if (FadeInDuration > 0f)
        {
            _audioSource.volume = 0f;
            _audioSource.Play();
            FadeIn(FadeInDuration);
        }
        else
        {
            _audioSource.Play();
        }

        OnPlayCallback?.Invoke();
        ScheduleEnd(FadeOutDuration,() => 
        {
            _audioSource?.Stop();
            OnFinishCallback?.Invoke();
            MarkFinished();
        });
    }
    
    internal void ScheduleEnd(float fadeOutDuration, Action onComplete, GameObject linkTarget = null)
    {
        if (_audioSource == null || IsFinished)
            return;

        // 루프가 남아있을 때 자동 크로스페이드
        if (LoopCount == -1 || LoopCount > 1)
        {
            float delay = Mathf.Max(0.01f, ClipLength - _source.LoopCrossfadeDuration);
            _scheduler = DOVirtual.DelayedCall(delay, () =>
            {
                if (IsFinished || LoopCount == 0 || !ReferenceEquals(_source.CurrentEntry, this))
                    return;
                
                SoundEntry nextEntry = new SoundEntry(this);
                nextEntry.LoopCount = LoopCount > 0 ? LoopCount - 1 : LoopCount;
                _source.CrossfadeTo(nextEntry, _source.LoopCrossfadeDuration);
                
            }, false).SetLink(_source.gameObject);
        }
        // 마지막 루프. 평범하게 페이드 아웃만 필요할 때
        else
        {
            float duration = ReferenceEquals(_source.CurrentEntry, this) ? FadeOutDuration : fadeOutDuration;
            float delay = ClipLength - duration;
            _scheduler = DOVirtual.DelayedCall(delay, () =>
            {
                if (IsFinished)
                    return;

                FadeOut(duration, onComplete, linkTarget);
                
            }, false).SetLink(linkTarget ?? _source.gameObject);
        }
    }

    public void FadeIn(float duration, Action onComplete = null)
    {
        if (_audioSource == null)
            return;

        _fade?.Kill();
        _audioSource.volume = 0f;
        _fade = _audioSource.DOFade(_source.BaseVolume, duration)
            .SetEase(Ease.InCubic)
            .SetLink(_source.gameObject)
            .OnComplete(() => onComplete?.Invoke());
    }

    public void FadeOut(float duration, Action onComplete = null, GameObject linkTarget = null)
    {
        if (_audioSource == null)
            return;

        _fade?.Kill();
        _fade = _audioSource.DOFade(0f, duration)
            .SetEase(Ease.OutCubic)
            .SetLink(linkTarget ?? _source.gameObject)
            .OnComplete(() => onComplete?.Invoke());
    }

    public void Stop(float overrideFadeOut = -1f)
    {
        if (IsFinished)
            return;

        _scheduler?.Kill();
        _scheduler = null;

        float fadeDuration = overrideFadeOut >= 0f ? overrideFadeOut : FadeOutDuration;

        if (fadeDuration > 0f && _audioSource != null)
        {
            FadeOut(fadeDuration, () =>
            {
                _audioSource?.Stop();
                MarkFinished();
            });
        }
        else
        {
            _audioSource?.Stop();
            MarkFinished();
        }
    }
    
    internal void TransferTo(AudioSource newSource)
    {
        _scheduler?.Kill();
        _scheduler = null;
        _fade?.Kill();
        _fade = null;
        _audioSource = newSource;
        LoopCount = 0;
    }

    internal void ForceFinish()
    {
        if (IsFinished)
            return;

        _scheduler?.Kill();
        _scheduler = null;
        _fade?.Kill();
        _fade = null;
        MarkFinished();
    }

    private void MarkFinished()
    {
        if (IsFinished || !SoundManager.Instance)
            return;

        IsFinished = true;
        _isPlaying = false;
        _audioSource = null;
        SoundManager.Instance.UntrackEntry(this);

        if (_source.IsPlaying == _isPlaying && Options.HasFlag(PlayOptions.DuckOthers))
        {
            SoundManager.Instance.RestoreDucked();
        }
    }
}