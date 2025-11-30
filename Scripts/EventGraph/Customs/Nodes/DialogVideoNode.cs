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
    [System.Serializable, NodeMenuItem("Custom/Dialog & Video")]
    public class DialogVideoNode : DialogNode
    {
        [SerializeField, Input("Dialog Block Provider"), OMR.MethodSignature(typeof(EventBlock),typeof(DialogObject),typeof(VideoClip))]
        OMR.ObjectMethodReference _binder;
        [SerializeField, Input("Video")]
        VideoClip _clip;

        public override string name => "Dialog & Video";

        public override BakedEventNode GetBakedNode() => new BakedDialogVideoBlock(_binder,_dialog,_clip);

        public class BakedDialogVideoBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            DialogObject _dialog;
            VideoClip _videoClip;

            public BakedDialogVideoBlock(OMR.ObjectMethodReference block, DialogObject dialog, VideoClip clip)
            {
                _binder = block;
                _dialog = dialog;
                _videoClip = clip;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_dialog,_videoClip) as EventBlock;
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