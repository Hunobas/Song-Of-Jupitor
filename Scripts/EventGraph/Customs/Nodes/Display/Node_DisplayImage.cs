using System;
using GraphProcessor;
using UnityEngine;

namespace EventGraph
{
    /// <summary>
    /// 지정한 Sprite 이미지를 일정 시간 동안 출력합니다.
    /// Delay / UnscaledTime / WaitUntilFinished 동작은 ActionNodeBase가 처리합니다.
    /// </summary>
    [Serializable, NodeMenuItem(EventCategories.Display + "이미지 출력")]
    public sealed class Node_DisplayImage : ActionNodeBase
    {
        [Input("Image Block Provider"), SerializeField, 
         OMR.MethodSignature(typeof(EventBlock), typeof(Sprite), typeof(float))]
        OMR.ObjectMethodReference _binder;

        [Input("Image"), SerializeField] 
        Sprite _sprite;

        [Input("Duration (sec)"), SerializeField, Min(0.01f)] 
        float _time = 1f;

        protected override string DisplayName => "이미지 출력";

        protected override IActionNode CreateAction()
        {
            return new DisplayImageAction(_binder, _sprite, _time);
        }

        sealed class DisplayImageAction : IActionNode
        {
            readonly OMR.ObjectMethodReference _binder;
            readonly Sprite _sprite;
            readonly float _time;

            EventBlock _block;
            bool _finished;

            public bool IsFinished => _finished;

            public DisplayImageAction(OMR.ObjectMethodReference binder, Sprite sprite, float time)
            {
                _binder = binder;
                _sprite = sprite;
                _time   = Mathf.Max(0.01f, time);
            }

            public void OnStart(CoroutineDelegator delegator)
            {
                if (_binder == null)
                {
                    EventGraphRuntime.Abort("ImageNode: Binder가 비어있습니다.");
                    _finished = true;
                    return;
                }

                _block = _binder.Invoke(_sprite, _time) as EventBlock;
                if (_block == null)
                {
                    EventGraphRuntime.Abort("ImageNode: EventBlock 생성에 실패했습니다.");
                    _finished = true;
                    return;
                }

                _block.PlugCallbacks(null, () =>
                {
                    _block.UnplugCallbacks();
                    _finished = true;
                });

                _block.Invoke();
            }
        }
    }
}
