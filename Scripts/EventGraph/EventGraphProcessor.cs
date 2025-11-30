// EventGraphRuntime.cs
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

namespace EventGraph
{
    /// <summary>
    /// 그래프 실행 컨텍스트. 실행 중인 프로세서를 스택으로 관리하여
    /// 중첩 진입/중단에도 안전하게 현재 컨텍스트를 추적합니다.
    /// </summary>
    public static class EventGraphRuntime
    {
        static readonly Stack<EventGraphProcessor> _stack = new Stack<EventGraphProcessor>();

        internal static EventGraphProcessor Current => _stack.Count > 0 ? _stack.Peek() : null;

        internal static void Push(EventGraphProcessor p)
        {
            if (p != null) _stack.Push(p);
        }

        internal static void Pop(EventGraphProcessor p)
        {
            if (_stack.Count == 0) return;

            if (ReferenceEquals(_stack.Peek(), p)) { _stack.Pop(); return; }

            var tmp = new Stack<EventGraphProcessor>(_stack.Count);
            while (_stack.Count > 0)
            {
                var top = _stack.Pop();
                if (!ReferenceEquals(top, p))
                    tmp.Push(top);
            }
            while (tmp.Count > 0)
                _stack.Push(tmp.Pop());
        }

        public static void Abort(string message, Object context = null)
            => Current?.Abort(message, context);
    }
    
    public sealed class EventGraphProcessor : BaseGraphProcessor
    {
        const int MaxCallDepth = 1000;

        public EventBlock GraphBlock => _graphBlock;
        public bool UseLock => _useLock;
        public bool Locked  => _locked;

        CoroutineDelegator _delegator;
        BakedEventGraph _bakedGraph;
        EventBlock _graphBlock;

        bool _useLock;
        bool _locked;
        int  _maxCallDepth;
        
        bool _aborted;
        string _abortReason;

        public EventGraphProcessor(BaseGraph graph, MonoBehaviour targetMono = null, bool useLock = false) : base(graph)
        {
            this.graph = graph;

            if (targetMono != null)
            {
                _delegator = new CoroutineDelegator(targetMono);
            }
            else if (graph?.baseObject is MonoBehaviour mb)
            {
                _delegator = new CoroutineDelegator(mb);
            }
            else
            {
                var go = new GameObject("EventGraphProcessor");
                var host = go.AddComponent<EventGraphProcessorMono>();
                host.Initialize(graph, this);
                _delegator = new CoroutineDelegator(host);
            }

            _graphBlock = new EventBlock(Run);
            _useLock    = useLock;
            _locked     = false;
        }

        /// <summary>그래프를 베이크합니다. (기본 파라미터 반영)</summary>
        public void Bake()
        {
            foreach (var node in graph.nodes)
                if (node is ParameterNode p) p.outputPorts.PushDatas();

            foreach (var node in graph.nodes)
                if (node is SequentialBaseNode s) s.SetupProcessing(_delegator);

            _bakedGraph = new BakedEventGraph(graph, _delegator);
        }

        /// <summary>오버라이드 파라미터를 반영하여 베이크합니다.</summary>
        public void BakeWithOverrides(OverrideParameterContainer overrides)
        {
            if (overrides == null) { Bake(); return; }

            foreach (var param in overrides.Parameters) param.Overwrite();

            foreach (var node in graph.nodes)
                if (node is ParameterNode p) p.outputPorts.PushDatas();

            foreach (var param in overrides.Parameters) param.Restore();

            foreach (var node in graph.nodes)
                if (node is SequentialBaseNode s) s.SetupProcessing(_delegator);

            _bakedGraph = new BakedEventGraph(graph, _delegator);
        }

        public override void Run()
        {
            if (_useLock && _locked)
            {
                Debug.LogWarning("You are trying to run a locked process concurrently.");
                return;
            }

            _locked       = true;
            _maxCallDepth = 0;
            _aborted      = false;
            _abortReason  = null;
            
            EventGraphRuntime.Push(this);

            if (_bakedGraph?.DoneNode is IDoneNode done)
                done.PlugOnProcessDone(OnProcessAllDone);

            _graphBlock?.OnStart();
            _bakedGraph?.StartNode?.Invoke(RunNextNodes);
        }
        
        public void Abort(string reason, UnityEngine.Object context = null)
        {
            if (_aborted) return;
            _aborted = true;
            _abortReason = reason ?? "Aborted";

            ConsoleLogger.LogError($"[액션 노드] {reason}", context);

            _locked = false;
            _graphBlock?.OnDone();

            EventGraphRuntime.Pop(this);
        }

        void RunNextNodes(BakedEventNode node)
        {
            if (_aborted) return;

            _maxCallDepth++;
            if (_maxCallDepth >= MaxCallDepth)
                Debug.LogWarningFormat("Call depth exceeded {0} (current {1}). Consider restructuring the graph.", MaxCallDepth, _maxCallDepth);

            var nextNodes = node.NextNodes;
            if (nextNodes != null)
            {
                for (int i = 0; i < nextNodes.Length; i++)
                    nextNodes[i].Invoke(RunNextNodes, node);
            }

            _maxCallDepth--;
        }

        void OnProcessAllDone()
        {
            if (_aborted) return;
            _locked = false;
            _graphBlock?.OnDone();
            EventGraphRuntime.Pop(this);
        }

        public override void UpdateComputeOrder()
        {
            // Not used in this pipeline.
        }
    }
}
