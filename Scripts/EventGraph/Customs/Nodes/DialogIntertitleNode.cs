using GraphProcessor;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;
using ValueObjects;

namespace EventGraph
{

    /// <summary>
    /// Waits both dialog end and video end.
    /// Video panel should be closed on done
    /// </summary>
    [System.Serializable, NodeMenuItem("Custom/Dialog & Intertitle")]
    public class DialogIntertitleNode : DialogNode
    {
        [SerializeField, Input("Dialog Block Provider"), OMR.MethodSignature(typeof(EventBlock),typeof(DialogObject),typeof(StringValue),typeof(bool))]
        OMR.ObjectMethodReference _binder;
        [SerializeField, Input("Text")]
        StringValue _stringValue;
        [SerializeField]
        bool _useWrite = true;

        public override string name => "Dialog & Intertitle";

        public override BakedEventNode GetBakedNode() => new BakedDialogIntertitleBlock(_binder,_dialog, _stringValue,_useWrite);

        public class BakedDialogIntertitleBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            DialogObject _dialog;
            StringValue _stringValue;
            bool _useWrite = true;

            public BakedDialogIntertitleBlock(OMR.ObjectMethodReference block, DialogObject dialog, StringValue stringValue, bool useWrite)
            {
                _binder = block;
                _dialog = dialog;
                _stringValue = stringValue;
                _useWrite = useWrite;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_dialog,_stringValue,_useWrite) as EventBlock;
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