// ActionNodeBase.cs
using System;
using System.Collections;
using GraphProcessor;
using UnityEngine;

namespace EventGraph
{
    public interface IActionNode
    {
        // 런타임 초기화
        void Init() { }

        // _delay 끝나고 실행 시작할 때 호출
        void OnStart(CoroutineDelegator delegator) { }

        // 작동하는 동안 매 프레임 호출. dt는 unscaledTime 옵션에 따라 전달
        void OnUpdate(float deltaTime) { }

        // 액션 종료 직전 호출(리소스 정리)
        void OnComplete() { }

        bool IsFinished { get; }
    }
    
    public enum WaitPolicy { Inherit, ForceWait, ForceNoWait }

    /// <summary>
    /// 생명주기(Init/OnStart/OnUpdate/OnComplete)를 갖는 SequentialNode 베이스
    /// </summary>
    [Serializable]
    public abstract class ActionNodeBase : SequentialNode
    {
        [Input("Delay"), SerializeField, Min(0f), Tooltip("노드 실행 전 대기 시간(초)")]
        public float delay = 0f;

        [Setting("Wait Until Finished"), Tooltip("완료 신호를 받을 때까지 다음 노드로 진행하지 않음")]
        public bool waitUntilFinished = true;

        [Setting("Unscaled Time"), Tooltip("Delay/Update에서 TimeScale의 영향을 받지 않음")]
        public bool unscaledTime = false;

        protected virtual string DisplayName => "";
        protected virtual WaitPolicy WaitBehavior => WaitPolicy.Inherit;

        /// <summary>현재 노드의 입력값을 스냅샷하여 런타임 액션을 생성</summary>
        protected abstract IActionNode CreateAction();

        public sealed override BakedEventNode GetBakedNode()
            => new BakedActionNode(CreateAction(), delay, waitUntilFinished, unscaledTime, WaitBehavior);

        sealed class BakedActionNode : BakedEventNode
        {
            readonly IActionNode _action;
            readonly float _delay;
            readonly bool _wait;
            readonly bool _unscaled;
            readonly WaitPolicy _policy;
            
            public BakedActionNode(IActionNode action, float delay, bool wait, bool unscaled, WaitPolicy policy)
            {
                _action = action;
                _delay = Mathf.Max(0f, delay);
                _wait = wait;
                _unscaled = unscaled;
                _policy = policy;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                bool mustWait = _policy switch
                {
                    WaitPolicy.ForceWait    => true,
                    WaitPolicy.ForceNoWait  => false,
                    _                       => _wait
                };

                if (mustWait) _delegator.InvokeOnMono(RunWaitThenComplete(onDone));
                else
                {
                    _delegator.InvokeOnMono(RunImmediatelyAndForget());
                    onDone?.Invoke(this);
                }
            }

            IEnumerator RunWaitThenComplete(Action<BakedEventNode> onDone)
            {
                if (_delay > 0f)
                    yield return _unscaled ? new WaitForSecondsRealtime(_delay) : new WaitForSeconds(_delay);

                _action?.Init();
                _action?.OnStart(_delegator);

                while (_action != null && !_action.IsFinished)
                {
                    var dt = _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                    _action.OnUpdate(dt);
                    yield return null;
                }

                _action?.OnComplete();
                onDone?.Invoke(this);
            }

            IEnumerator RunImmediatelyAndForget()
            {
                if (_delay > 0f)
                    yield return _unscaled ? new WaitForSecondsRealtime(_delay) : new WaitForSeconds(_delay);

                _action?.Init();
                _action?.OnStart(_delegator);
                _action?.OnComplete();
            }
        }
    }
}
