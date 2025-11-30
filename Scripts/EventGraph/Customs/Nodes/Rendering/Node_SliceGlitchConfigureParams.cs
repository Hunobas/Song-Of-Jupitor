using System;
using GraphProcessor;
using UnityEngine;

namespace EventGraph
{
    /// <summary>
    /// SliceGlitch의 슬라이스 파라미터를 런타임에 재설정하는 노드
    /// </summary>
    [Serializable, NodeMenuItem(EventCategories.Rendering + "슬라이스 글리치/단일 글리치 크기, 수량 재설정")]
    public sealed class Node_SliceGlitchConfigureParams : ActionNodeBase
    {
        [Input("Target Glitch"), SerializeField, ShowAsDrawer]
        public SliceGlitch target;

        [Input, SerializeField, ShowAsDrawer, Range(0f,1f)]
        public float rectAmount = 0.25f;

        [Input, SerializeField, ShowAsDrawer, Min(1f)]
        public float rectMinSizePx = 12f;

        [Input, SerializeField, ShowAsDrawer, Min(1f)]
        public float rectMaxSizePx = 96f;

        [Input, SerializeField, ShowAsDrawer, Min(0f)]
        public float rectMaxShiftPx = 32f;

        [Input, SerializeField, ShowAsDrawer, Range(1,4)]
        public int iterations = 3;

        [Input, SerializeField, ShowAsDrawer, Range(0f,1f)]
        public float aniso = 1f;

        protected override string DisplayName => "단일 글리치 크기, 수량 재설정";

        protected override IActionNode CreateAction()
        {
            if (target == null)
            {
                EventGraphRuntime.Abort($"{nameof(Node_SliceGlitchConfigureParams)}: Target SliceGlitch is null.", (UnityEngine.Object)null);
                return null;
            }

            return new ConfigureParamsAction(target, rectAmount, rectMinSizePx, rectMaxSizePx, rectMaxShiftPx, iterations, aniso);
        }

        sealed class ConfigureParamsAction : IActionNode
        {
            readonly SliceGlitch _target;
            readonly float _rectAmount, _rectMin, _rectMax, _rectShift, _aniso;
            readonly int _iters;

            public bool IsFinished => true;

            public ConfigureParamsAction(SliceGlitch t, float a, float min, float max, float shift, int iters, float an)
            {
                _target = t;
                _rectAmount = a;
                _rectMin = min;
                _rectMax = max;
                _rectShift = shift;
                _iters = iters;
                _aniso = an;
            }

            public void OnStart(CoroutineDelegator delegator)
            {
                _target.ConfigureSliceParams(_rectAmount, _rectMin, _rectMax, _rectShift, _iters, _aniso);
            }
        }
    }
}
