using System;
using GraphProcessor;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EventGraph
{
    [Serializable, NodeMenuItem(EventCategories.Display + "컷씬 이미지")]
    public sealed class Node_CutsceneImage : ActionNodeBase
    {
        // ===== 공통 =====
        [Input("Cutscene Panel"), SerializeField]
        CutscenePanelBase _panel;

        // ===== 메인 이미지(정적/애니) - 전부 노드 바디에 배치 =====
        [ToggleLeft, SerializeField, LabelText("Animated")]
        bool _animated = false;

        [ShowIf("@_animated")] [Input("Anim Controller"), SerializeField]
        RuntimeAnimatorController _imageController;

        [HideIf(nameof(_animated))]  [Input("Sprite"), SerializeField]
        Sprite _sprite;

        [ToggleLeft, SerializeField, LabelText("Use Scanline")]
        bool _useScanline = true;
        
        [ToggleLeft, SerializeField, LabelText("Preserve Same")]
        bool _preserveAspect = true;
        
        // ===== 비네트 옵션 - 전부 노드 바디에 배치 =====
        [ToggleLeft, SerializeField, LabelText("Use Vignette")]
        bool _useVignette = false;

        [ShowIf(nameof(_useVignette))]  [ToggleLeft, SerializeField, LabelText("Vignette Animated")]
        bool _vignetteAnimated = false;

        [ShowIf("@_useVignette && _vignetteAnimated")]  [SerializeField, Min(0f), LabelText("Vignette Speed")]
        float _vignetteSpeed = 1f;

        [ShowIf("@_useVignette && _vignetteAnimated")]  [ToggleLeft, SerializeField, LabelText("Vignette Unscaled Time")]
        bool _vignetteUnscaledTime = false;

        // ===== 표시 수명 - 노드 바디에 배치 =====
        [ToggleLeft, SerializeField, LabelText("Until Cutscene End")]
        bool _untilCutsceneEnd = false;

        [HideIf(nameof(_untilCutsceneEnd))]
        [Input("Duration (sec)"), SerializeField, Min(0.01f)]
        float _duration = 1.0f;

        protected override string DisplayName => "컷씬 이미지";

        protected override IActionNode CreateAction()
            => new CutsceneImageAction(
                _panel,
                // main
                _animated, _imageController, _sprite, _useScanline, _preserveAspect,
                // vignette
                _useVignette, _vignetteAnimated, _vignetteSpeed, _vignetteUnscaledTime,
                // life
                _untilCutsceneEnd, Mathf.Max(0.01f, _duration)
            );

        // ── Action 구현 ────────────────────────────────────────────────────────
        sealed class CutsceneImageAction : IActionNode
        {
            readonly CutscenePanelBase _panel;

            // main
            readonly bool _animated;
            readonly RuntimeAnimatorController _imageController;
            readonly Sprite _sprite;
            readonly bool _useScanline;
            readonly bool _preserveAspect;

            // vignette
            readonly bool _useVignette;
            readonly bool _vignetteAnimated;
            readonly float _vignetteSpeed;
            readonly bool _vignetteUnscaledTime;

            // life
            readonly bool _untilCutsceneEnd;
            readonly float _duration;

            float _elapsed;
            bool _finished;

            public bool IsFinished => _finished;

            public CutsceneImageAction(
                CutscenePanelBase panel,
                // main
                bool animated, RuntimeAnimatorController imageController, Sprite sprite, bool useScanline, bool preserveAspect,
                // vignette
                bool useVignette, bool vignetteAnimated, float vignetteSpeed, bool vignetteUnscaledTime,
                // life
                bool untilCutsceneEnd, float duration)
            {
                _panel = panel;

                _animated = animated;
                _imageController = imageController;
                _sprite = sprite;
                _useScanline = useScanline;
                _preserveAspect = preserveAspect;

                _useVignette = useVignette;
                _vignetteAnimated = vignetteAnimated;
                _vignetteSpeed = vignetteSpeed;
                _vignetteUnscaledTime = vignetteUnscaledTime;

                _untilCutsceneEnd = untilCutsceneEnd;
                _duration = duration;
            }

            public void OnStart(CoroutineDelegator delegator)
            {
                if (_panel == null)
                {
                    EventGraphRuntime.Abort("[CutsceneImage] Panel이 비었습니다.");
                    _finished = true;
                    return;
                }
                
                _panel.ImagePanel.Image.preserveAspect = _preserveAspect;
                _panel.VignettePanel.Image.preserveAspect = _preserveAspect;
                _panel.gameObject.SetActive(true);

                if (_useVignette)
                {
                    _panel.ShowVignette();

                    if (_vignetteAnimated)
                    {
                        var v = _panel.VignetteAnimator;
                        if (v == null)
                        {
                            EventGraphRuntime.Abort("[CutsceneImage] 비네트 Animator가 패널에 연결되어 있지 않습니다.");
                        }
                        else
                        {
                            v.updateMode = _vignetteUnscaledTime ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
                            v.speed = Mathf.Max(0f, _vignetteSpeed);
                            v.enabled = true;
                            v.Rebind();
                            v.Update(0f);
                            v.Play(0, 0, 0f);
                        }
                    }
                }

                if (_animated)
                {
                    var anim = _panel.ImageAnimator;
                    if (anim == null)
                    {
                        EventGraphRuntime.Abort("[CutsceneImage] 메인 이미지 Animator가 패널에 연결되어 있지 않습니다.");
                        _finished = true;
                        return;
                    }

                    if (_imageController != null)
                    {
                        _panel.AssignImageController(_imageController);
                    }

                    _panel.ShowCutsceneImage(null, _useScanline);

                    anim.enabled = true;
                    anim.Rebind();
                    anim.Update(0f);
                    anim.Play(0, 0, 0f);
                }
                else
                {
                    if (_sprite == null)
                    {
                        EventGraphRuntime.Abort("[CutsceneImage] Sprite가 비었습니다.");
                        _finished = true;
                        return;
                    }
                    _panel.ShowCutsceneImage(_sprite, _useScanline);
                }

                _elapsed = 0f;

                if (_untilCutsceneEnd)
                    _finished = true;
            }

            public void OnUpdate(float deltaTime)
            {
                if (_finished || _untilCutsceneEnd)
                    return;

                _elapsed += deltaTime;
                if (_elapsed >= _duration)
                {
                    _panel.CloseSprite();
                    if (_useVignette)
                        _panel.CloseVignette();
                    _finished = true;
                }
            }
        }
    }
}
