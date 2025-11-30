using System;
using GraphProcessor;
using UnityEngine;
using UnityEngine.Video;
using ValueObjects;

namespace EventGraph
{
    public enum DialogMode
    {
        Plain,      // Dialog only
        Audio,      // Dialog + Audio
        Image,      // Dialog + Image
        Video,      // Dialog + Video
        Intertitle, // Dialog + Intertitle text
    }

    /// <summary>
    /// 통합 다이얼로그 노드
    /// 다양한 모드(Plain, Audio, Image, Video, Intertitle)를 지원합니다.
    /// </summary>
    [Serializable, NodeMenuItem(EventCategories.Display + "다이얼로그")]
    public sealed class Node_Dialog : ActionNodeBase
    {
        [SerializeField, Input("Dialog Object"), ShowAsDrawer]
        DialogObject _dialog;

        [SerializeField, Input("Dialog Block Provider"), 
         OMR.MethodSignature(typeof(EventBlock))]
        OMR.ObjectMethodReference _binder;

        [SerializeField] DialogMode _mode = DialogMode.Plain;

        // Mode-specific inputs
        [Input("Audio"), SerializeField] AudioClip _audio;
        [Input("Image"), SerializeField] Sprite _sprite;
        [Input("Video"), SerializeField] VideoClip _video;
        [Input("Intertitle Text"), SerializeField] StringValue _stringValue;
        [SerializeField] bool _useWrite = true;

        protected override string DisplayName => "다이얼로그";

        protected override IActionNode CreateAction()
        {
            return new DialogAction(_binder, _dialog, _mode, _audio, _sprite, _video, _stringValue, _useWrite);
        }

        sealed class DialogAction : IActionNode
        {
            readonly OMR.ObjectMethodReference _binder;
            readonly DialogObject _dialog;
            readonly DialogMode _mode;

            readonly AudioClip _audio;
            readonly Sprite _sprite;
            readonly VideoClip _video;
            readonly StringValue _stringValue;
            readonly bool _useWrite;

            EventBlock _block;
            bool _finished;

            public bool IsFinished => _finished;

            public DialogAction(
                OMR.ObjectMethodReference binder, 
                DialogObject dialog,
                DialogMode mode,
                AudioClip audio,
                Sprite sprite,
                VideoClip video,
                StringValue strVal,
                bool useWrite)
            {
                _binder     = binder;
                _dialog     = dialog;
                _mode       = mode;
                _audio      = audio;
                _sprite     = sprite;
                _video      = video;
                _stringValue= strVal;
                _useWrite   = useWrite;
            }

            public void OnStart(CoroutineDelegator delegator)
            {
                if (_binder == null)
                {
                    EventGraphRuntime.Abort("Dialog binder is null");
                    _finished = true;
                    return;
                }

                object[] args = _mode switch
                {
                    DialogMode.Plain      => new object[] { _dialog },
                    DialogMode.Audio      => new object[] { _dialog, _audio },
                    DialogMode.Image      => new object[] { _dialog, _sprite },
                    DialogMode.Video      => new object[] { _dialog, _video },
                    DialogMode.Intertitle => new object[] { _dialog, _stringValue, _useWrite },
                    _                     => new object[] { _dialog }
                };

                _block = _binder.Invoke(args) as EventBlock;
                if (_block == null)
                {
                    EventGraphRuntime.Abort($"DialogNode: EventBlock 생성 실패 (mode={_mode})");
                    _finished = true;
                    return;
                }

                _block.PlugCallbacks(null, () =>
                {
                    _block.UnplugCallbacks();
                    _finished = true;
                });

                _block.Invoke();
            }
        }
    }
}
