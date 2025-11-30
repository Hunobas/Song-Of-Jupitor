using GraphProcessor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EventGraph
{

    public class EventGraphProcessorMono : MonoBehaviour
    {
        public BaseGraph Graph { get; private set; }
        public EventGraphProcessor Processor { get; private set; }

        public void Initialize(BaseGraph graph, EventGraphProcessor processor)
        {
            Graph = graph;
            Processor = processor;
        }
    }

}