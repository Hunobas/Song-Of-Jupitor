using GraphProcessor;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EventGraph
{


    /// <summary>
    /// Bind sprite and dialog, wait until dialog finish
    /// Image panel should be closed on done
    /// </summary>
    [System.Serializable, NodeMenuItem("Custom/Dialog & Image")]
    public class DialogImageNode : DialogNode
    {
        [SerializeField, Input("Dialog Block Provider"), OMR.MethodSignature(typeof(EventBlock),typeof(DialogObject),typeof(Sprite))]
        OMR.ObjectMethodReference _binder;
        [SerializeField, Input("Sprite")]
        Sprite _sprite;

        public override string name => "Dialog & Image";

        public override BakedEventNode GetBakedNode() => new BakedDialogImageBlock(_binder,_dialog,_sprite);

        public class BakedDialogImageBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            DialogObject _dialog;
            Sprite _sprite;

            public BakedDialogImageBlock(OMR.ObjectMethodReference block, DialogObject dialog, Sprite sprite)
            {
                _binder = block;
                _dialog = dialog;
                _sprite = sprite;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_dialog,_sprite) as EventBlock;
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