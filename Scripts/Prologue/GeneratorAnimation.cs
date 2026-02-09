using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Sirenix.OdinInspector;
using DG.Tweening;
using InGameContants;

public class GeneratorAnimation : MonoBehaviour
{
    [SerializeField] private DOTweenAnimation[] _actuatorGroup;
    [SerializeField] private VisualEffect[] _steamVFXGroup;
    [SerializeField] private SoundSource _generatorAudio;
    [SerializeField] private SoundSource _steamAudio;
    [SerializeField] private float _defaultSteamSpawnCount = 10f;
    
    [ShowInInspector, ReadOnly]
    private bool _isStopped;

    private const string PlaySteamEventName = "Play Steam";
    private const string ParticleCoundPropertyName = "Particle Count";
    
    private float _elapsedTime;
    private bool _allowSteamEmission;
    private readonly List<Coroutine> _steamCoroutines = new();

    [Button]
    public void StopImmediately()
    {
        if (_isStopped)
            return;

        _generatorAudio.PlayByNameOrNull(GeneratorSoundName.GeneratorStop).WithLoop(0).WithoutOptions(PlayOptions.Loop);

        foreach (DOTweenAnimation actuatorAnim in _actuatorGroup)
        {
            actuatorAnim.DOGotoAndPause(0f);
        }
        
        _isStopped = true;
    }

    [Button]
    public void AccelerateToNormal(float duration)
    {
        if (!_isStopped)
            return;
        
        _generatorAudio.PlayByNameOrNull(GeneratorSoundName.GeneratorStartup)?
            .WithStretchLength(duration)
            .WithLoop(0)
            .WithoutOptions(PlayOptions.Loop)
            .OnFinish(() =>
            {
                _generatorAudio.PlayByNameOrNull(GeneratorSoundName.GeneratorLoop)?
                    .WithLoop();
            });

        foreach (DOTweenAnimation actuatorAnim in _actuatorGroup)
        {
            actuatorAnim.DORestart();

            Tween tween = actuatorAnim.tween;
            tween.timeScale = 0f;

            DOTween.To(
                    () => tween.timeScale,
                    v =>
                    {
                        tween.timeScale = v;
                        _elapsedTime += Time.deltaTime;
                    },
                    1f,
                    duration
                )
                .SetEase(Ease.Linear)
                .SetTarget(actuatorAnim)
                .SetLink(actuatorAnim.gameObject);
        }
        
        _allowSteamEmission = true;
        _isStopped = false;

        _steamAudio.SetVolume(1f);
        _steamAudio.PlayByNameOrNull(ETCSoundName.SteamOneShot1);
        foreach (VisualEffect vfx in _steamVFXGroup)
        {
            vfx.gameObject.SetActive();
            Coroutine c = StartCoroutine(SteamRoutine(vfx, duration));
            _steamCoroutines.Add(c);
        }

        // duration 끝나면 Steam 종료 단계 진입
        DOVirtual.DelayedCall(duration, StopSteamEmission).SetLink(gameObject);
    }
    
    private IEnumerator SteamRoutine(VisualEffect vfx, float duration)
    {
        // 시작 지연 (랜덤)
        yield return new WaitForSeconds(Random.Range(0f, 0.5f));

        while (_allowSteamEmission)
        {
            float alpha = _elapsedTime / Mathf.Max(duration, 1f);
            float spawnMultiplier = Mathf.Lerp(1f, 0.2f, alpha);
            
            // 재생
            vfx.SendEvent(PlaySteamEventName);
            vfx.SetFloat(ParticleCoundPropertyName, _defaultSteamSpawnCount * spawnMultiplier);
            
            _steamAudio.SetVolume(spawnMultiplier);
            float playTime = Random.Range(0.3f, 1.2f);
            _steamAudio.SetPitch(1f / playTime);
            _steamAudio.PlayByNameOrNull(ETCSoundName.SteamOneShot2);
            yield return new WaitForSeconds(playTime);

            // 멈춤 (새 스폰 차단)
            vfx.SetFloat(ParticleCoundPropertyName, 0f);

            float pauseTime = Random.Range(0.2f, 1.0f);
            yield return new WaitForSeconds(pauseTime);
        }
    }
    
    private void StopSteamEmission()
    {
        _allowSteamEmission = false;

        foreach (VisualEffect vfx in _steamVFXGroup)
        {
            vfx.SetFloat(ParticleCoundPropertyName, 0f);
        }

        _steamCoroutines.Clear();
        _elapsedTime = 0f;
    }

}