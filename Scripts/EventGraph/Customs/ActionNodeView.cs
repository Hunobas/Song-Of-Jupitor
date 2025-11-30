// ActionNodeView.cs
#if UNITY_EDITOR
using System.Reflection;
using GraphProcessor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace EventGraph
{
    [NodeCustomEditor(typeof(ActionNodeBase))]
    public class ActionNodeView : BaseNodeView
    {
        SerializedProperty _pDelay, _pWait, _pUnscaled;

        protected override bool hasSettings { get; set; } = true;

        public override void Enable(bool fromInspector = false)
        {
            // 기본 필드(입력 드로어) + 설정 컨테이너 생성
            base.Enable(fromInspector);

            SetTitle();
            
            _pDelay    = FindSerializedProperty("delay");
            _pWait     = FindSerializedProperty("waitUntilFinished");
            _pUnscaled = FindSerializedProperty("unscaledTime");

            // 본문 컨트롤
            var fWait     = new PropertyField(_pWait, "Wait Until Finished");
            var fUnscaled = new PropertyField(_pUnscaled, "Unscaled Time");
            fWait.Bind(owner.serializedGraph);
            fUnscaled.Bind(owner.serializedGraph);
            controlsContainer.Add(fWait);
            controlsContainer.Add(fUnscaled);
            
            if (IsForcedPolicy(out var forced))
                fWait.SetEnabled(false);

            schedule.Execute(() =>
            {
                var sWait     = this.Q<PropertyField>("waitUntilFinished");

                if (IsForcedPolicy(out var forcedPolicy))
                {
                    if (forcedPolicy == WaitPolicy.ForceNoWait)
                    {
                        if (_pWait != null) { _pWait.boolValue = false; owner.serializedGraph.ApplyModifiedPropertiesWithoutUndo(); }
                        sWait?.SetEnabled(false);
                    }
                    else if (forcedPolicy == WaitPolicy.ForceWait)
                    {
                        if (_pWait != null) { _pWait.boolValue = true; owner.serializedGraph.ApplyModifiedPropertiesWithoutUndo(); }
                        sWait?.SetEnabled(false);
                    }
                }
            }).ExecuteLater(0);
        }

        void SetTitle()
        {
            Label name = new Label(nodeTarget?.GetType().Name ?? "Node");
            if (nodeTarget is ActionNodeBase ab)
            {
                var p = ab.GetType().GetProperty("DisplayName",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null && p.GetValue(ab) is string s && !string.IsNullOrEmpty(s))
                    name.text = s;
            }
            titleContainer.Add(name);
        }
        
        bool IsForcedPolicy(out WaitPolicy policy)
        {
            policy = WaitPolicy.Inherit;
            if (nodeTarget is ActionNodeBase ab)
            {
                var m = ab.GetType().GetMethod("get_WaitBehavior",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null && m.Invoke(ab, null) is WaitPolicy p && p != WaitPolicy.Inherit)
                {
                    policy = p;
                    return true;
                }
            }
            return false;
        }
    }
}
#endif
