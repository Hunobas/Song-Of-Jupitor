using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public class CctvController : MonoBehaviour
{
    // ====== 설정 ======
    [Title("Camera Positions")]
    [SerializeField] private List<Transform> _robotArmCamPositions = new();

    [Title("References")]
    [SerializeField] private Camera _cam;                 // RobotArmCam 카메라
    [SerializeField] private RawImage _viewImage;         // 이 UI 이미지가 shared RT를 본다
    [SerializeField] private VideoPlayer _player;         // 글리치 영상 재생기
    [SerializeField] private RenderTexture _sharedRT;     // 단 하나만 쓰는 공유 RT

    [Title("Video Options")]
    [SerializeField] private bool _videoUseUnscaledTime = true;
    [SerializeField] private bool _videoLoopDefault = false;

    // ====== 내부 상태 ======
    RenderTexture _prevCamRT;
    Coroutine _running;

    private void OnEnable()
    {
        ReleaseRTFromVideo();
    }

    void OnDisable()
    {
        StopAllRunning();
        ReleaseRTFromVideo();
    }

    // =========================
    // 카메라 전환
    // =========================
    public void SwitchCam(Transform camPos, Action<bool> callback = null)
    {
        if (camPos == null)
            return;

        PlayVideoOneShot(callback);
        transform.SetPositionAndRotation(camPos.position, camPos.rotation);
    }

    public void SwitchCam(int camNum, Action<bool> callback = null)
    {
        if (_robotArmCamPositions == null || _robotArmCamPositions.Count == 0)
            return;
        
        int idx = Mathf.Clamp(camNum - 1, 0, _robotArmCamPositions.Count - 1);
        SwitchCam(_robotArmCamPositions[idx], callback);
    }

    // =========================
    // 단일 RT 전환: VideoPlayer가 “독점”
    // =========================
    public void PlayVideoOneShot(Action<bool> callback = null)
    {
        if (_player == null)
            return;

        StopAllRunning();
        _running = StartCoroutine(PlayVideoOneShotRoutine(callback));
    }

    public void ReleaseRTFromVideo()
    {
        if (_player != null)
        {
            if (_player.isPlaying) _player.Stop();
            _player.targetTexture = null;
        }

        if (_cam != null)
        {
            _cam.enabled = true;
            _cam.targetTexture = _prevCamRT != null ? _prevCamRT : _sharedRT;
        }

        if (_viewImage != null)
            _viewImage.texture = _sharedRT;
    }

    IEnumerator PlayVideoOneShotRoutine(Action<bool> callback = null)
    {
        // 1) 카메라 상태 백업 & 완전 차단
        if (_cam != null)
        {
            _prevCamRT      = _cam.targetTexture;
            _cam.enabled = false;
            _cam.targetTexture = null; // 같은 RT를 건드리지 못하도록
            callback?.Invoke(false);
        }

        // 2) RawImage는 계속 sharedRT를 본다 (UI 세팅 유지)
        if (_viewImage != null)
            _viewImage.texture = _sharedRT;

        // 3) VideoPlayer가 sharedRT를 “독점”하도록
        _player.isLooping = false;
        _player.targetTexture = _sharedRT;

        _player.Prepare();
        while (!_player.isPrepared)
            yield return null;

        _player.Play();
        
        while (_player.isPlaying)
            yield return null;

        ReleaseRTFromVideo();
        callback?.Invoke(true);
        _running = null;
    }
    
    void StopAllRunning()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }
    }
}
