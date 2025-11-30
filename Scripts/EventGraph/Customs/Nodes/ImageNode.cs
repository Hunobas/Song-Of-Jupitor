using GraphProcessor;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace EventGraph
{

    /// <summary>
    /// Waits both dialog end and video end.
    /// Video panel should be closed on done
    /// </summary>
    [System.Serializable, NodeMenuItem("Custom/Image Sequence")]
    public class ImageNode : SequentialNode
    {
        [SerializeField, Input("Image Block Provider"), OMR.MethodSignature(typeof(EventBlock), typeof(Sprite), typeof(float))]
        OMR.ObjectMethodReference _binder;
        [SerializeField]
        Sprite _sprite;
        [SerializeField]
        float _time = 1f;

        public override string name => "Image Sequence";

        public override BakedEventNode GetBakedNode() => new BakedImageBlock(_binder, _sprite, _time);

        public class BakedImageBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            Sprite _sprite;
            float _time;

            public BakedImageBlock(OMR.ObjectMethodReference block, Sprite sprite,float time)
            {
                _binder = block;
                _sprite = sprite;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_sprite,_time) as EventBlock;
                if (block == null)
                {
                    onDone?.Invoke(this);
                    return;
                }
                block.PlugCallbacks(null, () => {
                    block.UnplugCallbacks();    // should be called first, to prevent loop calls
                    onDone?.Invoke(this);
                });
                block.Invoke(); // invoke event after all bindings are done
            }
        }
    }

}