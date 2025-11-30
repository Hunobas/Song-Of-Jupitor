using GraphProcessor;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EventGraph
{


    [System.Serializable, NodeMenuItem("Custom/Dialog")]
    public class DialogNode : SequentialNode
    {
        [SerializeField, Input("Dialog Object"), ShowAsDrawer]
        protected DialogObject _dialog;

        [SerializeField, Input("Dialog Block Provider"), OMR.MethodSignature(typeof(EventBlock),typeof(DialogObject))]
        OMR.ObjectMethodReference _Binder;

        public string PreviewParagraphFirst => _dialog is not null ? _dialog.PreviewFirst : "";
        public string PreviewParagraphLast => _dialog is not null ? _dialog.PreviewLast : "";

        public override string name => "Dialog";

        public override BakedEventNode GetBakedNode() => new BakedDialogBlock(_Binder,_dialog);

        public class BakedDialogBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            DialogObject _dialog;

            public BakedDialogBlock(OMR.ObjectMethodReference block, DialogObject dialog)
            {
                _binder = block;
                _dialog = dialog;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_dialog) as EventBlock;
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