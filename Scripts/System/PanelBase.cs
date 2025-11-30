using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class PanelBase : MonoBehaviour
{
    [Title("BaseSettings")] [SerializeField]
    private GraphicRaycaster _graphicRaycaster;
    [SerializeField] private IARaycaster _iaRaycaster;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Image _cursorImg;
    [SerializeField] private Canvas _curCanvas;
    [SerializeField] private CinemachineVirtualCamera _viewCam;
    [SerializeField] private InputEventAsset _panelInput;
    [SerializeField] private InputEventAsset _hoverInput;
    [SerializeField] private Transform _endPos;
    [SerializeField] private GameObject _InteractCollider;
    [SerializeField] private float _aimSensitivity = 0.05f;
    [SerializeField] private float scrollSensitivity = 0.1f;
    [SerializeField] private Vector2 _canvasBoundaryOffset = new Vector2(1f, 1f);
    [SerializeField] private bool _canHover = false;

    [Title("BaseDebug")] [SerializeField, ReadOnly]
    protected bool isOperatingPanel;

    [SerializeField, ReadOnly] protected bool isHovered;

    public bool IsOperatingPanel
    {
        get => isOperatingPanel;
        set => isOperatingPanel = value;
    }

    [SerializeField, ReadOnly] private bool _isBlending;

    private RectTransform _cursorRect;
    private RectTransform _canvasRect;
    private Vector2 _cursorScreenPos;

    protected Action CashedLeft;
    protected Action CashedRight;
    private float _cachedAinSensitivity;

    private bool _panelLock;
    protected bool PanelKeyLock;

    private GameObject _lastHoveredObject;

    // ---- Pointer state ----
    private GameObject _pressedObject;
    private bool _pressedSupportsDrag;
    private bool _isDragging;
    private RaycastResult _lastRaycast;
    private RaycastResult _pressRaycast;
    private Vector2 _pressScreenPos;
    private Vector2 _lastScreenPos;
    private float _dragThresholdSqr;

    private PointerEventData.InputButton _pressedButton = PointerEventData.InputButton.Left;
    private bool _leftHeld, _rightHeld;

    // ==== Focus Cursor internals ====
    float _baseFov;
    float _baseOrtho;
    bool _isOrtho;
    Tweener _zoomTw;
    Tweener _cursorScaleTw;
    Tweener _cursorColorTw;
    bool _focusFxActive;

    public InputEventAsset PanelInput => _panelInput;

    public event Action OperatingPanelEvent;
    public event Action PanelOperationCompleted;

    private bool _isZoomOperating = false; //카메라 전환 후 패널 조작중인지 

    protected virtual void Awake()
    {
        _cursorRect = _cursorImg.GetComponent<RectTransform>();
        _canvasRect = _curCanvas.GetComponent<RectTransform>();
        _cachedAinSensitivity = _aimSensitivity;

        if (EventSystem.current != null)
        {
            float th = EventSystem.current.pixelDragThreshold;
            _dragThresholdSqr = th * th;
        }

        // 커서가 클릭을 먹지 않도록(혹시 켜져 있다면)
        if (_cursorImg) _cursorImg.raycastTarget = false;

        if (_viewCam != null)
        {
            _isOrtho = _viewCam.m_Lens.Orthographic;
            if (_isOrtho) _baseOrtho = _viewCam.m_Lens.OrthographicSize;
            else _baseFov = _viewCam.m_Lens.FieldOfView;
        }
    }

    protected virtual void Update()
    {
        if (!isOperatingPanel)
            return;

        if (!_leftHeld && !_rightHeld && _pressedObject != null)
        {
            ForceReleasePointer();
        }

        HandleScrollWheel();
        UpdatePointerHover();
    }

    #region 패널 컨트롤 구현부

    // ===== Cursor move =====
    private void SetRayDirection(float x, float y)
    {
        if (!isOperatingPanel || PanelKeyLock) return;

        _cursorScreenPos += new Vector2(x, y) * _aimSensitivity;

        Vector2 halfSize = _canvasRect.sizeDelta * 0.5f;
        Vector2 min = -halfSize + _canvasBoundaryOffset;
        Vector2 max = halfSize - _canvasBoundaryOffset;

        _cursorScreenPos = new Vector2(
            Mathf.Clamp(_cursorScreenPos.x, min.x, max.x),
            Mathf.Clamp(_cursorScreenPos.y, min.y, max.y)
        );

        _cursorRect.localPosition = _cursorScreenPos;

        if (_pressedObject != null && (_leftHeld || _rightHeld))
            PointerDragTick();
    }

    // ===== Raycast utils =====
    private List<RaycastResult> RaycastAtCursor()
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_curCanvas.worldCamera, _cursorRect.position);
        var pointerData = new PointerEventData(EventSystem.current) { position = screenPoint };

        List<RaycastResult> results = new();
        _graphicRaycaster.Raycast(pointerData, results);
        return results;
    }

    private GameObject HitTopMost()
    {
        var results = RaycastAtCursor();
        if (results.Count > 0)
        {
            _lastRaycast = results[0];
            return results[0].gameObject;
        }

        _lastRaycast = default;
        return null;
    }

    private static GameObject FindHandlerTarget<T>(GameObject start) where T : IEventSystemHandler
        => ExecuteEvents.GetEventHandler<T>(start);

    private PointerEventData BuildPointerData()
    {
        var cam = _curCanvas.worldCamera;
        var screenPos = RectTransformUtility.WorldToScreenPoint(cam, _cursorRect.position);

        return new PointerEventData(EventSystem.current)
        {
            pointerId = 0,
            button = _pressedButton, // ★ 중요: 실제 누른 버튼
            position = screenPos,
            pressPosition = _pressScreenPos,
            delta = screenPos - _lastScreenPos,
            pointerCurrentRaycast = new RaycastResult
            {
                gameObject = _lastRaycast.gameObject,
                module = _lastRaycast.module,
                worldPosition = _lastRaycast.worldPosition,
                screenPosition = screenPos
            },
            pointerPressRaycast = _pressRaycast
        };
    }

    // ===== Hover =====
    private void UpdatePointerHover()
    {
        var raycastResults = RaycastAtCursor();
        GameObject currentHovered = raycastResults.Count > 0 ? raycastResults[0].gameObject : null;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(_curCanvas.worldCamera, _cursorRect.position)
        };

        if (_lastHoveredObject != currentHovered)
        {
            if (_lastHoveredObject != null)
                ExecuteEvents.Execute(_lastHoveredObject, pointerData, ExecuteEvents.pointerExitHandler);

            if (currentHovered != null)
                ExecuteEvents.Execute(currentHovered, pointerData, ExecuteEvents.pointerEnterHandler);

            _lastHoveredObject = currentHovered;
        }
    }

    // ===== Pointer pipeline =====
    private void PointerDown(PointerEventData.InputButton btn)
    {
        if (PanelKeyLock) return;

        // ★ 이미 뭔가 눌려있다면 먼저 ‘정리’하고 새로 시작
        if (_pressedObject != null)
            ForceReleasePointer();

        _pressedButton = btn;

        var hit = HitTopMost();
        if (hit == null) return;

        _pressedObject =
            FindHandlerTarget<IBeginDragHandler>(hit) ??
            FindHandlerTarget<IDragHandler>(hit) ??
            FindHandlerTarget<IPointerClickHandler>(hit) ??
            FindHandlerTarget<IPointerDownHandler>(hit);
        if (_pressedObject == null) return;

        _pressRaycast = _lastRaycast;

        var cam = _curCanvas.worldCamera;
        _pressScreenPos = RectTransformUtility.WorldToScreenPoint(cam, _cursorRect.position);
        _lastScreenPos = _pressScreenPos;
        _isDragging = false;

        var ped = BuildPointerData();
        ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.pointerDownHandler);

        _pressedSupportsDrag =
            ExecuteEvents.CanHandleEvent<IBeginDragHandler>(_pressedObject) ||
            ExecuteEvents.CanHandleEvent<IDragHandler>(_pressedObject);
    }

    private void PointerDragTick()
    {
        if (_pressedObject == null) return;

        HitTopMost();
        var ped = BuildPointerData();

        if (_pressedSupportsDrag)
        {
            // 임계치 이상 이동해야 드래그 시작
            float distSqr = (ped.position - _pressScreenPos).sqrMagnitude;
            if (!_isDragging && distSqr >= _dragThresholdSqr)
            {
                var beginTarget = FindHandlerTarget<IBeginDragHandler>(_pressedObject);
                if (beginTarget != null)
                    ExecuteEvents.Execute(beginTarget, ped, ExecuteEvents.beginDragHandler);

                _isDragging = true;
            }

            if (_isDragging)
            {
                var dragTarget = FindHandlerTarget<IDragHandler>(_pressedObject);
                if (dragTarget != null)
                    ExecuteEvents.Execute(dragTarget, ped, ExecuteEvents.dragHandler);
            }
        }

        _lastScreenPos = ped.position;
    }

    private void PointerUp(PointerEventData.InputButton btn)
    {
        // ★ 다른 버튼의 Up이면 무시 (짝이 안 맞는 업)
        if (_pressedObject == null || btn != _pressedButton)
            return;

        HitTopMost(); // 최신 ray 정보를 써서 ped 갱신
        var ped = BuildPointerData();

        var upTarget = FindHandlerTarget<IPointerUpHandler>(_pressedObject) ?? _pressedObject;
        ExecuteEvents.Execute(upTarget, ped, ExecuteEvents.pointerUpHandler);

        if (_isDragging && _pressedSupportsDrag)
        {
            var endTarget = FindHandlerTarget<IEndDragHandler>(_pressedObject);
            if (endTarget != null)
                ExecuteEvents.Execute(endTarget, ped, ExecuteEvents.endDragHandler);
        }
        else
        {
            var releaseHit = HitTopMost();
            var upClickTarget = releaseHit ? FindHandlerTarget<IPointerClickHandler>(releaseHit) : null;
            var downClickTarget = FindHandlerTarget<IPointerClickHandler>(_pressedObject);

            if (upClickTarget != null && downClickTarget == upClickTarget)
                ExecuteEvents.Execute(downClickTarget, ped, ExecuteEvents.pointerClickHandler);
        }

        ForceReleasePointer();
    }

    private void ForceReleasePointer()
    {
        _pressedObject = null;
        _pressedSupportsDrag = false;
        _isDragging = false;
    }

    // ===== Scroll =====
    private void HandleScrollWheel()
    {
        if (_scrollRect == null) return;

        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        var results = RaycastAtCursor();
        foreach (RaycastResult result in results)
        {
            if (result.gameObject == _scrollRect.gameObject ||
                result.gameObject.transform.IsChildOf(_scrollRect.transform))
            {
                float newValue = _scrollRect.verticalNormalizedPosition + scrollDelta * scrollSensitivity;
                _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newValue);
                break;
            }
        }
    }

    // ==== Shift ====
    readonly float _zoomFactor = 0.98f;
    readonly float _zoomDuration = 0.15f;
    readonly float _cursorScale = 1.3f;
    readonly Color _cursorTint = new(1.3f, 1.3f, 1.3f, 1f);

    private void StartFocusCursor()
    {
        if (PanelKeyLock || _focusFxActive) return;
        _focusFxActive = true;

        // 조준 감도 하향
        _aimSensitivity *= 0.5f;

        // 카메라 줌 인 (FOV↓ or OrthoSize↓)
        if (_viewCam != null)
        {
            _zoomTw?.Kill();
            if (_isOrtho)
            {
                float target = Mathf.Max(0.001f, _baseOrtho * _zoomFactor);
                _zoomTw = DOTween.To(
                    () => _viewCam.m_Lens.OrthographicSize,
                    v =>
                    {
                        var lens = _viewCam.m_Lens;
                        lens.OrthographicSize = v;
                        _viewCam.m_Lens = lens;
                    },
                    target, _zoomDuration
                ).SetEase(Ease.OutCubic);
            }
            else
            {
                float target = Mathf.Clamp(_baseFov * _zoomFactor, 1f, 179f);
                _zoomTw = DOTween.To(
                    () => _viewCam.m_Lens.FieldOfView,
                    v =>
                    {
                        var lens = _viewCam.m_Lens;
                        lens.FieldOfView = v;
                        _viewCam.m_Lens = lens;
                    },
                    target, _zoomDuration
                ).SetEase(Ease.OutCubic);
            }
        }

        // 커서 시각 피드백(살짝 크게, 살짝 밝게)
        if (_cursorRect != null && _cursorImg != null)
        {
            _cursorScaleTw?.Kill();
            _cursorColorTw?.Kill();

            _cursorScaleTw = _cursorRect.DOScale(_cursorScale, _zoomDuration).SetEase(Ease.OutSine);
            Color from = _cursorImg.color;
            Color to = new Color(from.r * _cursorTint.r, from.g * _cursorTint.g, from.b * _cursorTint.b, from.a);
            _cursorColorTw = _cursorImg.DOColor(to, _zoomDuration).SetEase(Ease.OutSine);
        }
    }

    private void EndFocusCursor()
    {
        if (!_focusFxActive)
            return;
        _focusFxActive = false;

        // 조준 감도 원복
        _aimSensitivity = _cachedAinSensitivity;

        // 카메라 줌 원복
        if (_viewCam != null)
        {
            _zoomTw?.Kill();
            if (_isOrtho)
            {
                _zoomTw = DOTween.To(
                    () => _viewCam.m_Lens.OrthographicSize,
                    v =>
                    {
                        var lens = _viewCam.m_Lens;
                        lens.OrthographicSize = v;
                        _viewCam.m_Lens = lens;
                    },
                    _baseOrtho, _zoomDuration
                ).SetEase(Ease.OutCubic);
            }
            else
            {
                _zoomTw = DOTween.To(
                    () => _viewCam.m_Lens.FieldOfView,
                    v =>
                    {
                        var lens = _viewCam.m_Lens;
                        lens.FieldOfView = v;
                        _viewCam.m_Lens = lens;
                    },
                    _baseFov, _zoomDuration
                ).SetEase(Ease.OutCubic);
            }
        }

        // 커서 시각 원복
        if (_cursorRect != null && _cursorImg != null)
        {
            _cursorScaleTw?.Kill();
            _cursorColorTw?.Kill();

            _cursorScaleTw = _cursorRect.DOScale(1f, _zoomDuration).SetEase(Ease.OutSine);
            Color target = _cursorImg.color;
            target.r = 1f;
            target.g = 1f;
            target.b = 1f;
            _cursorColorTw = _cursorImg.DOColor(target, _zoomDuration).SetEase(Ease.OutSine);
        }
    }

    private void _LeftDownCache()
    {
        _leftHeld = true;
        PointerDown(PointerEventData.InputButton.Left);
    }

    private void _LeftUpCache()
    {
        _leftHeld = false;
        PointerUp(PointerEventData.InputButton.Left);
    }

    private void _RightDownCache()
    {
        _rightHeld = true;
        PointerDown(PointerEventData.InputButton.Right);
    }

    private void _RightUpCache()
    {
        _rightHeld = false;
        PointerUp(PointerEventData.InputButton.Right);
    }

    #endregion

    private void EnterPanelCore()
    {
        ConsoleLogger.Log("패널캠 전환완료");

        GameState.Instance.PanelMode.Controller = this;
        GameState.Instance.ChangePlayMode(GameState.Instance.PanelMode);
        GameState.Instance.CanPause = false;

        _InteractCollider.gameObject.SetActive(false);

        _cursorImg.gameObject.SetActive(true);
        GameState.Instance.PanelMode.OnEnterPanelRoutineComplete(_endPos);

        OperatingPanelEvent?.Invoke();

        _cursorScreenPos = Vector3.zero;
        _cursorRect.localPosition = _cursorScreenPos;

        _panelInput.OnClickLeftDown += _LeftDownCache;
        _panelInput.OnClickLeftUp += _LeftUpCache;

        _panelInput.OnClickRightDown += _RightDownCache;
        _panelInput.OnClickRightUp += _RightUpCache;

        _panelInput.OnMouseAxis += SetRayDirection;

        _panelInput.OnESCKeyDown += EndPanelControl;
        _panelInput.OnLeftKeyDown += CashedLeft;
        _panelInput.OnRightKeyDown += CashedRight;

        _panelInput.OnLShiftKeyDown += StartFocusCursor;
        _panelInput.OnLShiftKeyUp += EndFocusCursor;

        isOperatingPanel = true;
        _isZoomOperating = true;
    }

    private void ExitPanelCore()
    {
        if (_focusFxActive) EndFocusCursor();

        GameState.Instance.ChangePlayMode(GameState.Instance.NormalMode);
        _cursorImg.gameObject.SetActive(false);

        _panelInput.OnClickLeftDown -= _LeftDownCache;
        _panelInput.OnClickLeftUp -= _LeftUpCache;

        _panelInput.OnClickRightDown -= _RightDownCache;
        _panelInput.OnClickRightUp -= _RightUpCache;

        _panelInput.OnMouseAxis -= SetRayDirection;

        _panelInput.OnESCKeyDown -= EndPanelControl;
        _panelInput.OnLeftKeyDown -= CashedLeft;
        _panelInput.OnRightKeyDown -= CashedRight;

        _panelInput.OnLShiftKeyDown -= StartFocusCursor;
        _panelInput.OnLShiftKeyUp -= EndFocusCursor;

        PanelOperationCompleted?.Invoke();
    }

    // ===== Panel control =====
    public void StartPanelControl()
    {
        if (_panelLock || _isBlending || _isZoomOperating) return;

        if (_hoverInput != null)
            EndHoverControl();

        _viewCam.Priority = 100;
        SwitchToVCam(() => { EnterPanelCore(); });
    }

    private void SwitchToVCam(Action onComplete = null)
        => StartCoroutine(WaitForBlendEnd(onComplete));

    private IEnumerator WaitForBlendEnd(Action onComplete)
    {
        _isBlending = true;
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        yield return new WaitUntil(() => brain.IsBlending);
        yield return new WaitUntil(() => !brain.IsBlending);
        _isBlending = false;
        onComplete?.Invoke();
    }

    public void EndPanelControl()
    {
        if (!isOperatingPanel || _isBlending || PanelKeyLock)
            return;

        ExitPanelCore();

        _viewCam.Priority = 0;

        StartCoroutine(WaitForBlendEnd(() =>
        {
            GameState.Instance.PanelMode.OnExitPanelRoutineComplete();
            GameState.Instance.CanPause = true;
            _InteractCollider.gameObject.SetActive(true);
            isOperatingPanel = false;
            _isZoomOperating = false;

            //만약 Hover가 가능한 상태면, 재진입
            if (_canHover && isHovered)
                StartHoverControl();
        }));
    }

    public void EndPanelForcely()
    {
        if (!isOperatingPanel || _isBlending || PanelKeyLock)
            return;

        if (_focusFxActive) EndFocusCursor();

        _viewCam.Priority = 0;
        _cursorImg.gameObject.SetActive(false);

        _panelInput.OnClickLeftDown -= _LeftDownCache;
        _panelInput.OnClickLeftUp -= _LeftUpCache;

        _panelInput.OnClickRightDown -= _RightDownCache;
        _panelInput.OnClickRightUp -= _RightUpCache;

        _panelInput.OnMouseAxis -= SetRayDirection;

        _panelInput.OnESCKeyDown -= EndPanelControl;
        _panelInput.OnLeftKeyDown -= CashedLeft;
        _panelInput.OnRightKeyDown -= CashedRight;

        _panelInput.OnLShiftKeyDown -= StartFocusCursor;
        _panelInput.OnLShiftKeyUp -= EndFocusCursor;

        PanelOperationCompleted?.Invoke();

        GameState.Instance.PanelMode.OnExitPanelRoutineComplete();
        GameState.Instance.CanPause = true;
        _InteractCollider.gameObject.SetActive(true);
        isOperatingPanel = false;
    }

    public virtual void ChangeNextPage()
    {
    }

    public virtual void ChangePastPage()
    {
    }

    [EasyButtons.Button]
    public void PanelLock() => _panelLock = true;

    [EasyButtons.Button]
    public void PanelUnLock() => _panelLock = false;

    public virtual void SetPanelKeyLock() => PanelKeyLock = true;
    public virtual void ReleasePanelKeyLock() => PanelKeyLock = false;

    public void RemoveESCKey() => _panelInput.OnESCKeyDown -= EndPanelControl;
    public void AddESCKey() => _panelInput.OnESCKeyDown += EndPanelControl;

    #region HoverControl

    public void HoverEnter() => isHovered = true;
    public void HoverExit() => isHovered = false;

    public void StartHoverControl()
    {
        if (_isZoomOperating || !_canHover) return;

        ScreenDirector.Instance.HideCrosshair();

        _cursorImg.gameObject.SetActive(true);
        _cursorRect.localPosition = _cursorScreenPos;

        if (_iaRaycaster != null)
        {
            _iaRaycaster.onRaycasted += SyncCursorToRayHit;
        }

        _hoverInput.OnClickLeftDown += _LeftDownCache;
        _hoverInput.OnClickLeftUp += _LeftUpCache;

        _hoverInput.OnClickRightDown += _RightDownCache;
        _hoverInput.OnClickRightUp += _RightUpCache;

        _hoverInput.OnLeftKeyDown += CashedLeft;
        _hoverInput.OnRightKeyDown += CashedRight;

        isOperatingPanel = true;
    }


    public void EndHoverControl()
    {
        if (_isZoomOperating || !_canHover) return;

        if (_iaRaycaster != null)
        {
            _iaRaycaster.onRaycasted -= SyncCursorToRayHit;
        }

        ScreenDirector.Instance?.ShowCrosshair();

        _cursorImg.gameObject.SetActive(false);

        _hoverInput.OnClickLeftDown -= _LeftDownCache;
        _hoverInput.OnClickLeftUp -= _LeftUpCache;

        _hoverInput.OnClickRightDown -= _RightDownCache;
        _hoverInput.OnClickRightUp -= _RightUpCache;

        _hoverInput.OnMouseAxis -= SetRayDirection;

        _hoverInput.OnLeftKeyDown -= CashedLeft;
        _hoverInput.OnRightKeyDown -= CashedRight;

        isOperatingPanel = false;
    }


    private void SyncCursorToRayHit()
    {
        if (_iaRaycaster == null) return;
        if (!_iaRaycaster.hitted) return;

        Vector3 worldHitPos = _iaRaycaster.hit.point;


        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldHitPos);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPos,
            _curCanvas.worldCamera,
            out localPoint
        );

        Vector2 halfSize = _canvasRect.sizeDelta * 0.5f;
        Vector2 min = -halfSize + _canvasBoundaryOffset;
        Vector2 max = halfSize - _canvasBoundaryOffset;

        localPoint = new Vector2(
            Mathf.Clamp(localPoint.x, min.x, max.x),
            Mathf.Clamp(localPoint.y, min.y, max.y)
        );

        _cursorScreenPos = localPoint;
        _cursorRect.localPosition = _cursorScreenPos;
    }

    #endregion
}