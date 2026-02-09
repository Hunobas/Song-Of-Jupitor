using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Sirenix.OdinInspector;

public class TimelineController : MonoBehaviour
{
    [Header("Timeline Setting")]
    [SerializeField] protected PlayableDirector _timeline;
    [SerializeField] private SequentialDialogFeeder _dialogFeeder;
    [SerializeField] private DialogueType _dialogType;
    [SerializeField] private int _etcID;
    [SerializeField] private bool _isOneShot;
    [ShowIf("_isOneShot")]
    [SerializeField, ReadOnly] private bool _hasPlayed;

    public EventBlock TimelinePlay => _timeBlock;
    private EventBlock _timeBlock;

    protected virtual void Awake()
    {
        _timeline ??= GetComponent<PlayableDirector>();
        _timeBlock = new EventBlock(Play);
    }

    public void Pause() => _timeline.Pause();
    public void PauseTime(float duration) => StartCoroutine(PauseTimeCoroutine(duration)); 
    public void Stop() => _timeline.Stop();
    public void Resume() => _timeline.Resume();

    public void BindTimeline(TimelineAsset timelineAsset)
    {
        _timeline.playableAsset = timelineAsset;
    }
    
    public void Play(TimelineAsset timelineAsset)
    {
        _timeline.playableAsset = timelineAsset;
        Play();
    }
    
    public void Play()
    {
        if (_isOneShot && _hasPlayed)
            return;
        
        _timeBlock.OnStart();
        GameState.Instance.ChangePlayMode(GameState.Instance.CinemaMode);

        _timeline.stopped -= OnTimelineStopped;
        _timeline.stopped += OnTimelineStopped;
        
        _timeline.Play();
        
        if (_isOneShot)
            _hasPlayed = true;
    }

    public void FeedNext() => _dialogFeeder.FeedNext();

    public void StartTalk()
    {
        if (_dialogType is DialogueType.ETC)
        {
            _dialogFeeder.StartETCDialog(_etcID);
            return;
        }
        
        _dialogFeeder.StartNextDialog();
    }
    
    public void StartTalkAndPause()
    {
        if (_dialogType is DialogueType.ETC)
        {
            _dialogFeeder.StartETCDialog(_etcID);
            _dialogFeeder.AddTalkEndEvent(Resume);
            Pause();
            return;
        }
        
        _dialogFeeder.StartNextDialog();
        _dialogFeeder.AddTalkEndEvent(Resume);
        Pause();
    }

    public PlayableDirector GetTimeline() => _timeline;

    /// <summary>
    /// 일정 시간만큼 대기 했다가 타임라인 동작 
    /// </summary>
    private IEnumerator PauseTimeCoroutine(float duration)
    {
        Pause();
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            yield return null;
        }
        Resume();
    }

    private void OnTimelineStopped(PlayableDirector _)
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.CinemaMode.ExitCinemaMode();
        }

        _timeBlock.OnDone();
        _timeline.stopped -= OnTimelineStopped;
    }
}
