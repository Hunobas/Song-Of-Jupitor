// Node_Start6DShake.cs
using System;
using System.Collections;
using GraphProcessor;
using UnityEngine;
using Cinemachine;

namespace EventGraph
{
    /// <summary>
    /// 현재 활성 가상 카메라에 Cinemachine 6D 셰이크를 적용하고,
    /// shakeDuration이 끝나면 자연스럽게 페이드아웃합니다.
    /// </summary>
    [Serializable, NodeMenuItem(EventCategories.Camera + "카메라 6D 셰이크")]
    public sealed class Node_Start6DShake : ActionNodeBase
    {
        [Input("Cinemachine Brain"), SerializeField, ShowAsDrawer]
        public CinemachineBrain brain;

        [Input("Noise Settings"), SerializeField, ShowAsDrawer]
        public NoiseSettings noiseProfile;

        [Input("Crosshair UI"), SerializeField, ShowAsDrawer]
        public CrosshairUI crosshairUI;

        [Input, SerializeField, ShowAsDrawer, Min(0f)]     public float amplitude      = 3f;
        [Input, SerializeField, ShowAsDrawer, Min(0f)]     public float frequency      = 3f;
        [Input, SerializeField, ShowAsDrawer, Min(0.01f)]  public float shakeDuration  = 2.5f;

        protected override string DisplayName => "카메라 셰이크";
        protected override WaitPolicy WaitBehavior => WaitPolicy.Inherit;

        protected override IActionNode CreateAction()
        {
            if (brain == null)
            {
                EventGraphRuntime.Abort($"{GetType().Name}: CinemachineBrain이 비어있습니다.", (UnityEngine.Object)null);
                return null;
            }
            if (noiseProfile == null)
            {
                EventGraphRuntime.Abort($"{GetType().Name}: NoiseSettings 프로필이 비어있습니다.", (UnityEngine.Object)null);
                return null;
            }

            float dur  = Mathf.Max(0.01f, shakeDuration);
            float amp  = Mathf.Max(0f, amplitude);
            float freq = Mathf.Max(0f, frequency);

            bool cancelOnComplete = waitUntilFinished;

            return new Start6DShakeAction(brain, noiseProfile, crosshairUI, amp, freq, dur, cancelOnComplete);
        }

        sealed class Start6DShakeAction : IActionNode
        {
            readonly CinemachineBrain _brain;
            readonly NoiseSettings _profile;
            readonly CrosshairUI _crosshair;
            readonly float _amp, _freq, _dur;
            readonly bool _cancelOnComplete;

            CinemachineBasicMultiChannelPerlin _perlin;
            CoroutineDelegator _delegator;
            IEnumerator _routine;
            bool _finished;
            bool _cancelRequested;

            public bool IsFinished => _finished;

            public Start6DShakeAction(CinemachineBrain brain, NoiseSettings profile, CrosshairUI crosshair,
                                      float amplitude, float frequency, float duration, bool cancelOnComplete)
            {
                _brain = brain;
                _profile = profile;
                _crosshair = crosshair;
                _amp = amplitude;
                _freq = frequency;
                _dur = duration;
                _cancelOnComplete = cancelOnComplete;
            }

            public void OnStart(CoroutineDelegator delegator)
            {
                _delegator = delegator;

                var live = _brain?.ActiveVirtualCamera as CinemachineVirtualCameraBase;
                if (live == null)
                {
                    _finished = true;
                    EventGraphRuntime.Abort($"{nameof(Node_Start6DShake)}: 활성 가상 카메라가 없습니다.", _brain);
                    return;
                }

                var vcam = live.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
                if (vcam == null)
                {
                    _finished = true;
                    EventGraphRuntime.Abort($"{nameof(Node_Start6DShake)}: 활성 VCam에 CinemachineVirtualCamera가 없습니다.", live.VirtualCameraGameObject);
                    return;
                }

                _perlin = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>()
                          ?? vcam.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

                // 초기값 세팅
                _perlin.m_NoiseProfile  = _profile;
                _perlin.m_AmplitudeGain = _amp;
                _perlin.m_FrequencyGain = _freq;

                _crosshair?.StartShake(_dur);

                // 페이드아웃은 코루틴으로 수행(그래프가 대기하지 않아도 계속 유지됨)
                _routine = FadeOut();
                _delegator?.InvokeOnMono(_routine);

                _finished = false; // 그래프가 기다리는 경우, 코루틴 종료 시점에 true로 바뀜
            }

            IEnumerator FadeOut()
            {
                float t = 0f;
                while (t < _dur && !_cancelRequested)
                {
                    // ActionNodeBase에서 unscaled 옵션을 처리하므로 여기서는 Time.deltaTime 사용
                    t += Time.deltaTime;
                    float u = Mathf.Clamp01(t / _dur);
                    if (_perlin != null)
                    {
                        _perlin.m_AmplitudeGain = Mathf.Lerp(_amp, 0f, u);
                        _perlin.m_FrequencyGain = Mathf.Lerp(_freq, 0f, u);
                    }
                    yield return null;
                }

                // 정리
                if (_perlin != null)
                {
                    _perlin.m_AmplitudeGain = 0f;
                    _perlin.m_FrequencyGain = 0f;
                    _perlin.m_NoiseProfile  = null;
                }
                _crosshair?.StopShake();
                _finished = true; // 그래프가 기다리는 경우, 여기서 다음 노드로 진행
            }

            public void OnComplete()
            {
                // 그래프가 '기다림'인 경우에만 코루틴을 중단/정리.
                // (waitUntilFinished=false면 OnComplete가 즉시 호출되므로 코루틴을 유지해야 함)
                if (_cancelOnComplete)
                {
                    _cancelRequested = true;
                    // 즉시 정리
                    if (_perlin != null)
                    {
                        _perlin.m_AmplitudeGain = 0f;
                        _perlin.m_FrequencyGain = 0f;
                        _perlin.m_NoiseProfile  = null;
                    }
                    _crosshair?.StopShake();
                    _finished = true;
                }
            }
        }
    }
}
