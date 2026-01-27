using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Sirenix.OdinInspector;

public class SoundManager : PersistentSingleton<SoundManager>
{
    [Title("Mixer")]
    [SerializeField]
    private AudioMixerController _mixerController;

    [Title("Duck Settings")]
    [SerializeField, Range(0f, 1f)]
    private float _duckVolume = 0.2f;

    [SerializeField, Range(0.1f, 1f)]
    private float _duckFadeDuration = 0.3f;

    [Title("UI Sound")]
    [SerializeField, Range(0f, 1f)]
    private float _uiVolume = 1f;

    [Title("Debug")]
    [ShowInInspector]
    private int RegisteredSourceCount => _registeredSources.Count;

    [ShowInInspector]
    private int ActiveEntryCount => _activeEntries.Count;

    [ShowInInspector]
    private int DuckedSourceCount => _duckedSources.Count;

    private readonly HashSet<SoundSource> _registeredSources = new();
    private readonly List<SoundEntry> _activeEntries = new();
    private readonly HashSet<SoundSource> _duckedSources = new();

    private AudioSource _uiAudioSource;

    public AudioMixerController MixerController => _mixerController;

    protected override void Awake()
    {
        base.Awake();
        InitializeUIAudio();
    }

    private void InitializeUIAudio()
    {
        var uiSourceObj = new GameObject("UIAudioSource");
        uiSourceObj.transform.SetParent(transform);

        _uiAudioSource = uiSourceObj.AddComponent<AudioSource>();
        _uiAudioSource.playOnAwake = false;
        _uiAudioSource.spatialBlend = 0f;
        _uiAudioSource.outputAudioMixerGroup = _mixerController.AudioMixer.outputAudioMixerGroup;
    }
    
    public void PlayUI(AudioClip clip, float volumeScale = 1f)
    {
        Debug.Assert(clip != null, "UI AudioClip이 null입니다.");

        _uiAudioSource.PlayOneShot(clip, _uiVolume * volumeScale);
    }

    public void SetUIVolume(float volume)
    {
        _uiVolume = Mathf.Clamp01(volume);
    }

    internal void Register(SoundSource source)
    {
        _registeredSources.Add(source);
    }

    internal void Unregister(SoundSource source)
    {
        _registeredSources.Remove(source);
        _duckedSources.Remove(source);
    }

    internal void TrackEntry(SoundEntry entry)
    {
        if (!_activeEntries.Contains(entry))
        {
            _activeEntries.Add(entry);
        }
    }

    internal void UntrackEntry(SoundEntry entry)
    {
        _activeEntries.Remove(entry);
    }

    public bool IsClipPlaying(AudioClip clip)
    {
        foreach (var entry in _activeEntries)
        {
            if (!entry.IsPlaying)
                continue;

            if (entry.Clip == clip)
                return true;
        }

        return false;
    }

    public void StopAll(float fadeOutDuration = 0f)
    {
        foreach (var source in _registeredSources)
        {
            source.Stop(fadeOutDuration);
        }
    }

    public void PauseAll()
    {
        foreach (var source in _registeredSources)
        {
            source.Pause();
        }
    }

    public void ResumeAll()
    {
        foreach (var source in _registeredSources)
        {
            source.Resume();
        }
    }

    public void StopByPriority(SoundPriority maxPriority, float fadeOutDuration = 0f)
    {
        for (int i = _activeEntries.Count - 1; i >= 0; i--)
        {
            var entry = _activeEntries[i];

            if (entry.Priority <= maxPriority)
            {
                entry.Stop(fadeOutDuration);
            }
        }
    }

    internal void DuckAllExcept(SoundEntry exceptEntry)
    {
        foreach (var source in _registeredSources)
        {
            if (source.CurrentEntry == exceptEntry)
                continue;

            if (!source.IsPlaying)
                continue;

            if (source.CurrentEntry != null && source.CurrentEntry.Priority >= exceptEntry.Priority)
                continue;

            source.Duck(_duckVolume, _duckFadeDuration);
            _duckedSources.Add(source);
        }
    }

    internal void RestoreDucked()
    {
        foreach (var source in _duckedSources)
        {
            source.Unduck(_duckFadeDuration);
        }

        _duckedSources.Clear();
    }
    
    public void ApplyStunMuffle()
    {
        _mixerController.SetMuffleInstant(
            cutoff: 700f,
            volumeDb: -8f,
            reverbDb: -25f
        );
    }
    
    public void RecoverFromStun(float duration)
    {
        _mixerController.TweenMuffle(
            targetCutoff: 22000f,
            targetVolumeDb: 0f,
            targetReverbDb: -80f,
            duration: duration
        );
    }

    // protected override void OnDestroy()
    // {
    //     StopAll();
    //     base.OnDestroy();
    // }
}