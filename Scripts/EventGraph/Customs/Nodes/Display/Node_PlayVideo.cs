using System;
using GraphProcessor;
using UnityEngine;
using UnityEngine.Video;

namespace EventGraph
{
    public enum VideoPlayMode { Unscaled, Scaled }

    [Serializable, NodeMenuItem(EventCategories.Display + "비디오 재생")]
    public sealed class Node_PlayVideo : ActionNodeBase
    {
        [Input("비디오 플레이어"), SerializeField, ShowAsDrawer]
        public GameObject target;

        [Input, SerializeField, ShowAsDrawer]
        public VideoClip clip;

        [Input, SerializeField, ShowAsDrawer, Range(0.05f, 4f)]
        public float speed = 1f;

        [Input, SerializeField, ShowAsDrawer]
        public VideoPlayMode playMode = VideoPlayMode.Unscaled;

        [Input, SerializeField, ShowAsDrawer]
        public bool loop = false;

        [Input, SerializeField, ShowAsDrawer]
        public bool mute = false;

        [Input, SerializeField, ShowAsDrawer, Range(0f, 1f)]
        public float volume = 1f;
        
        protected override string DisplayName => "비디오 재생";

        protected override IActionNode CreateAction()
        {
            if (target == null)
            {
                EventGraphRuntime.Abort($"{nameof(Node_PlayVideo)}: Target GameObject가 비어있습니다.", (UnityEngine.Object)null);
                return null;
            }
            if (!target.TryGetComponent<VideoPlayer>(out var player) || player == null)
            {
                EventGraphRuntime.Abort($"{nameof(Node_PlayVideo)}: Target에 VideoPlayer 컴포넌트가 없습니다.", target);
                return null;
            }
            if (clip == null)
            {
                EventGraphRuntime.Abort($"{nameof(Node_PlayVideo)}: 재생할 VideoClip이 비어있습니다.", target);
                return null;
            }

            return new PlayAction(player, clip, Mathf.Max(0.05f, speed), playMode, loop, mute, Mathf.Clamp01(volume));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 런타임 액션
        // ──────────────────────────────────────────────────────────────────────────
        sealed class PlayAction : IActionNode
        {
            readonly VideoPlayer _player;
            readonly VideoClip   _clip;
            readonly float       _speed;
            readonly VideoPlayMode _mode;
            readonly bool        _loop;
            readonly bool        _mute;
            readonly float       _volume;

            bool _finished;
            bool _pausedByTimeScale;

            public bool IsFinished => _finished;

            public PlayAction(VideoPlayer player, VideoClip clip, float speed, VideoPlayMode mode, bool loop, bool mute, float volume)
            {
                _player = player;
                _clip   = clip;
                _speed  = speed;
                _mode   = mode;
                _loop   = loop;
                _mute   = mute;
                _volume = volume;
            }

            public void OnStart(CoroutineDelegator delegator)
            {
                if (_player == null || _clip == null)
                {
                    _finished = true;
                    return;
                }

                // 콜백 연결
                _player.loopPointReached += OnLoopPointReached;
                _player.errorReceived    += OnError;

                // 구성
                _player.Stop();
                _player.clip       = _clip;
                _player.isLooping  = _loop;
                _player.playbackSpeed = _speed;

                // 오디오 설정
                try
                {
                    if (_player.audioOutputMode == VideoAudioOutputMode.AudioSource)
                    {
                        var src = _player.GetTargetAudioSource(0);
                        if (src != null) { src.mute = _mute; src.volume = _volume; }
                    }
                    else
                    {
                        if (_player.audioTrackCount > 0)
                        {
                            _player.EnableAudioTrack(0, true);
                            _player.SetDirectAudioMute(0, _mute);
                            _player.SetDirectAudioVolume(0, _volume);
                        }
                    }
                }
                catch { /* 일부 플랫폼/설정에서 오디오 트랙 미지원일 수 있음 */ }

                _player.Play();

                // Scaled 모드에서 시작 시점에 이미 timeScale=0 이면 일시정지
                if (_mode == VideoPlayMode.Scaled && Time.timeScale <= 0f && _player.isPlaying)
                {
                    _player.Pause();
                    _pausedByTimeScale = true;
                }
            }

            public void OnUpdate(float dt)
            {
                if (_player == null)
                {
                    _finished = true;
                    return;
                }

                if (_mode == VideoPlayMode.Scaled)
                {
                    if (Time.timeScale <= 0f)
                    {
                        if (_player.isPlaying)
                        {
                            _player.Pause();
                            _pausedByTimeScale = true;
                        }
                    }
                    else if (_pausedByTimeScale)
                    {
                        _player.Play();
                        _pausedByTimeScale = false;
                    }
                }
            }

            public void OnComplete()
            {
                if (_player == null) return;

                _player.loopPointReached -= OnLoopPointReached;
                _player.errorReceived    -= OnError;

                if (!_loop)
                    _player.Stop();
            }

            void OnLoopPointReached(VideoPlayer source)
            {
                if (!_loop) _finished = true;
            }

            void OnError(VideoPlayer source, string message)
            {
                EventGraphRuntime.Abort($"VideoPlayer 오류: {message}", source);
                _finished = true;
            }
        }
    }
}
