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
    [System.Serializable, NodeMenuItem("Custom/Video")]
    public class VideoNode : SequentialNode
    {
        [SerializeField, Input("Video Block Provider"), OMR.MethodSignature(typeof(EventBlock), typeof(VideoClip))]
        OMR.ObjectMethodReference _binder;
        [SerializeField, Input("Video")]
        VideoClip _clip;

        public override string name => "Video";

        public override BakedEventNode GetBakedNode() => new BakedVideoBlock(_binder, _clip);

        public class BakedVideoBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            VideoClip _videoClip;

            public BakedVideoBlock(OMR.ObjectMethodReference block, VideoClip clip)
            {
                _binder = block;
                _videoClip = clip;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_videoClip) as EventBlock;
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