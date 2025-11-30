using GraphProcessor;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EventGraph
{


    /// <summary>
    /// Bind sound and dialog, wait until both sound and dialog are all finished
    /// Audio source should be disabled on done
    /// </summary>
    [System.Serializable, NodeMenuItem("Custom/Dialog & Audio")]
    public class DialogAudioNode : DialogNode
    {
        [SerializeField, Input("Dialog Block Provider"), OMR.MethodSignature(typeof(EventBlock),typeof(DialogObject),typeof(AudioClip))]
        OMR.ObjectMethodReference _bindAndRun;
        [SerializeField, Input("Source")]
        AudioClip _source;

        public override string name => "Dialog & Audio";

        public override BakedEventNode GetBakedNode() => new BakedDialogAudioBlock(_bindAndRun,_dialog,_source);

        public class BakedDialogAudioBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            DialogObject _dialog;
            AudioClip _source;

            public BakedDialogAudioBlock(OMR.ObjectMethodReference block, DialogObject dialog, AudioClip source)
            {
                _binder = block;
                _dialog = dialog;
                _source = source;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_dialog,_source) as EventBlock;
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