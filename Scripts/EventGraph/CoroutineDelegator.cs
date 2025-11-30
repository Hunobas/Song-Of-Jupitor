using System.Collections;
using UnityEngine;

namespace EventGraph
{
    public class CoroutineDelegator
    {
        MonoBehaviour _targetMono;

        public CoroutineDelegator(MonoBehaviour targetMono) => _targetMono = targetMono;

        public void InvokeOnMono(IEnumerator routine)
        {
            _targetMono.StartCoroutine(routine);
        }

    }

    
}
