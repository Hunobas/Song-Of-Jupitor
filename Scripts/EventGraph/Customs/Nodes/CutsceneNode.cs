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
    [System.Serializable, NodeMenuItem("Custom/Cutscene")]
    public class CutsceneNode : SequentialNode
    {
        [SerializeField, Input("Cutscene Block Provider"), OMR.MethodSignature(typeof(EventBlock),typeof(GameObject))]
        OMR.ObjectMethodReference _binder;
        [SerializeField, Input("Prefab")]
        GameObject _prefab;

        public override string name => "Cutscene Block";

        public override BakedEventNode GetBakedNode() => new BakedCutsceneBlock(_binder, _prefab);

        public class BakedCutsceneBlock : BakedEventNode
        {
            OMR.ObjectMethodReference _binder;
            GameObject _prefab;

            public BakedCutsceneBlock(OMR.ObjectMethodReference block, GameObject prefab)
            {
                _binder = block;
                _prefab = prefab;
            }

            public override void Invoke(Action<BakedEventNode> onDone = null, BakedEventNode prevNode = null)
            {
                EventBlock block = _binder.Invoke(_prefab) as EventBlock;
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