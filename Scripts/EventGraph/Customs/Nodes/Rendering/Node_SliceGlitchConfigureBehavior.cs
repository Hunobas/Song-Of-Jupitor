using System;
using GraphProcessor;
using UnityEngine;

namespace EventGraph
{
    /// <summary>
    /// SliceGlitch의 글리치 동작(확률, 주기, 강도)을 런타임에 재설정하는 노드
    /// </summary>
    [Serializable, NodeMenuItem(EventCategories.Rendering + "슬라이스 글리치/빈도, 세기 재설정")]
    public sealed class Node_SliceGlitchConfigureBehavior : ActionNodeBase
    {
        [Input("Target Glitch"), SerializeField, ShowAsDrawer]
        public SliceGlitch target;

        [Input, SerializeField, ShowAsDrawer, Range(0f,1f)]
        public float glitchProbability = 0.7f;

        [Input, SerializeField, ShowAsDrawer, Min(0.01f)]
        public float glitchInterval = 0.2f;

        [Input, SerializeField, ShowAsDrawer, Range(0f,1f)]
        public float fullScreenIntensity = 0.5f;

        [Input, SerializeField, ShowAsDrawer, Range(0f,1f)]
        public float uiIntensity = 0.5f;

        protected override string DisplayName => "빈도, 세기 재설정";

        protected override IActionNode CreateAction()
        {
            if (target == null)
            {
                EventGraphRuntime.Abort($"{nameof(Node_SliceGlitchConfigureBehavior)}: Target SliceGlitch is null.", (UnityEngine.Object)null);
                return null;
            }

            return new ConfigureBehaviorAction(target, glitchProbability, glitchInterval, fullScreenIntensity, uiIntensity);
        }

        sealed class ConfigureBehaviorAction : IActionNode
        {
            readonly SliceGlitch _target;
            readonly float _prob, _interval, _full, _ui;

            public bool IsFinished => true;

            public ConfigureBehaviorAction(SliceGlitch t, float p, float interval, float full, float ui)
            {
                _target = t;
                _prob = p;
                _interval = interval;
                _full = full;
                _ui = ui;
            }

            public void OnStart(CoroutineDelegator delegator)
            {
                _target.ConfigureGlitchBehavior(_prob, _interval, _full, _ui);
            }
        }
    }
}
