using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using EasyButtons;
using KinematicCharacterController.Examples;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PanelBase : MonoBehaviour
{
    [Header("BaseSettings")]
    [SerializeField] private GraphicRaycaster _graphicRaycaster;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private ScreenDirector _screenDirector;
    [SerializeField] private StaminaBarUI _staminaBarUI;
    [SerializeField] private Image _cursorImg;
    [SerializeField] private Image _panelCrossHair;
    [SerializeField] private Image _crossHair;
    [SerializeField] private Canvas _curCanvas;
    [SerializeField] private CinemachineVirtualCamera _viewCam;
    [SerializeField] private InputEventAsset _worldInput;
    [SerializeField] protected PlayerControlSystem _playerControlSystem;
    [SerializeField] private Transform _endPos;
    [SerializeField] private GameObject _InteractCollider;
    [SerializeField] private float _aimSensitivity = 0.05f; //마우스 감도
    [SerializeField] private float scrollSensitivity = 0.1f; // 휠 감도
    [SerializeField] private Vector2 _canvasBoundaryOffset = new Vector2(1f, 1f); //패널 스크린 가장자리 Offset

    [Header("BaseDebug")] [SerializeField] protected bool isOperatingPanel; //패널을 조작중인지

    public bool IsOperatingPanel
    {
        get => isOperatingPanel;
        set => isOperatingPanel = value;
    }
    [SerializeField] private bool _isBlending;
    private RectTransform _cursorRect;
    private RectTransform _canvasRect;
    private Vector2 _cursorScreenPos;

    protected Action CashedLeft;
    protected Action CashedRight;

    private bool _panelLock;
    protected bool PanelKeyLock; //패널 키 잠금 
    
    private GameObject _lastHoveredObject; //호버된 오브젝트
    private GameObject _pressedObject;
    private bool _isDragging;
    private Vector2 _lastScreenPos;
    private Vector2 _pressScreenPos;

    //프로퍼티 -> 접근 편의용
    public InputEventAsset WorldInput => _worldInput;

    public event Action OperatingPanelEvent; //패널 조작시 실행되는 이벤트
    public event Action PanelOperationCompleted; // 패널 조작 종료시 실행되는 이벤트

    protected virtual void Awake()
    {
        _cursorRect = _cursorImg.GetComponent<RectTransform>();
        _canvasRect = _curCanvas.GetComponent<RectTransform>();
    }

    protected virtual void Update()
    {
        if (!isOperatingPanel) return;
        HandleScrollWheel(); //스크롤 View 조작을 위한 Custom Func
        UpdatePointerHover(); //UI 오브젝트 Hover
    }

    //x,y는 마우스 Hor,Ver
    private void SetRayDirection(float x, float y)
    {
        if (!isOperatingPanel || PanelKeyLock) return;

        // 커서 이동량 계산
        _cursorScreenPos += new Vector2(x, y) * _aimSensitivity;

        // 캔버스 절반 크기와 오프셋 적용한 제한 범위 계산
        Vector2 halfSize = _canvasRect.sizeDelta * 0.5f;
        Vector2 min = -halfSize + _canvasBoundaryOffset;
        Vector2 max = halfSize - _canvasBoundaryOffset;

        // 커서 위치 클램프
        _cursorScreenPos = new Vector2(
            Mathf.Clamp(_cursorScreenPos.x, min.x, max.x),
            Mathf.Clamp(_cursorScreenPos.y, min.y, max.y)
        );
        
        _cursorRect.localPosition = _cursorScreenPos;

        if (_pressedObject != null)
            PointerDragTick();
    }

    /// <summary>
    /// 커서 위치 기준으로 UI Raycast 결과를 반환하는 유틸 메서드
    /// </summary>
    private List<RaycastResult> RaycastAtCursor()
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_curCanvas.worldCamera, _cursorRect.position);

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };

        List<RaycastResult> results = new List<RaycastResult>();
        _graphicRaycaster.Raycast(pointerData, results);
        return results;
    }

    /// <summary>
    /// 커서 위치 오브젝트 Hover
    /// </summary>
    private void UpdatePointerHover()
    {
        var raycastResults = RaycastAtCursor();
        GameObject currentHovered = raycastResults.Count > 0 ? raycastResults.FirstOrDefault().gameObject : null;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(_curCanvas.worldCamera, _cursorRect.position)
        };

        if (_lastHoveredObject != currentHovered)
        {
            if (_lastHoveredObject != null)
                ExecuteEvents.Execute<IPointerExitHandler>(_lastHoveredObject, pointerData, ExecuteEvents.pointerExitHandler);

            if (currentHovered != null)
                ExecuteEvents.Execute<IPointerEnterHandler>(currentHovered, pointerData, ExecuteEvents.pointerEnterHandler);

            _lastHoveredObject = currentHovered;
        }
    }

    /// <summary>
    /// 커서 위치에 있는 버튼 클릭
    /// </summary>
    private void InteractButton()
    {
        if (PanelKeyLock) return;

        var results = RaycastAtCursor();
        foreach (var r in results)
        {
            if (r.gameObject.TryGetComponent(out Button _))
            {
                var ped = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(_curCanvas.worldCamera, _cursorRect.position)
                };
                ExecuteEvents.Execute(r.gameObject, ped, ExecuteEvents.pointerClickHandler);
                break;
            }
        }
    }

    /// <summary>
    /// 커서 위치의 ScrollView를 휠로 스크롤
    /// </summary>
    private void HandleScrollWheel()
    {
        if (_scrollRect == null) return;

        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        var results = RaycastAtCursor();
        foreach (var r in results)
        {
            if (r.gameObject == _scrollRect.gameObject || r.gameObject.transform.IsChildOf(_scrollRect.transform))
            {
                float newValue = _scrollRect.verticalNormalizedPosition + scrollDelta * scrollSensitivity;
                _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newValue);
                break;
            }
        }
    }

    /// <summary>
    /// 패널 조작 시작
    /// </summary>
    public void StartPanelControl()
    {
        if (_panelLock || _isBlending) return;
        
        // TODO: 나중에 제거
        if (Chapter01Manager.Instance != null && Chapter01Manager.Instance.EquipBattery) return;
        
        _viewCam.Priority = 100;
        _playerControlSystem.FreezePlayerControl();
        _InteractCollider.gameObject.SetActive(false);
        _screenDirector.FadeOutIcons();
        _staminaBarUI.CanRun = false;

        //카메라 전환 완료 후, 조작가능
        SwitchToVCam(() =>
        {
            _cursorImg.gameObject.SetActive(true);
            _crossHair.gameObject.SetActive(false);
            if (_panelCrossHair != null)
                _panelCrossHair.gameObject.SetActive(false);

            OperatingPanelEvent?.Invoke();

            // 커서 원점
            _cursorScreenPos = Vector3.zero;
            _cursorRect.localPosition = _cursorScreenPos;

            // 입력 바인딩: 드래그 시퀀스 합성
            _worldInput.OnClickLeftDown += PointerDown;
            _worldInput.OnClickLeftUp   += PointerUp;
            _worldInput.OnMouseAxis     += SetRayDirection;
            _worldInput.OnLeftKeyDown   += CashedLeft;
            _worldInput.OnRightKeyDown  += CashedRight;
            _worldInput.OnESCKeyDown    += EndPanelControl;
            
// #if UNITY_EDITOR
//             _worldInput.OnQKeyDown += EndPanelControl;
// #endif

            _playerControlSystem.SetPlayerPosition(_endPos);
            _playerControlSystem.SetPlayerCameraRotation(_endPos.rotation);
            _playerControlSystem.LockCameraMouseInput();

            isOperatingPanel = true;
        });
    }

    private void SwitchToVCam(Action onComplete = null)
        => StartCoroutine(WaitForBlendEnd(onComplete));

    private IEnumerator WaitForBlendEnd(Action onComplete)
    {
        _isBlending = true;
        var brain = Camera.main.GetComponent<CinemachineBrain>();

        // 블렌딩이 시작될 때까지 대기
        yield return new WaitUntil(() => brain.IsBlending);

        // 블렌딩이 끝날 때까지 대기
        yield return new WaitUntil(() => !brain.IsBlending);

        _isBlending = false;
        onComplete?.Invoke();
    }


    /// <summary>
    /// 패널 조작 종료
    /// </summary>
    public void EndPanelControl()
    {
        if (!isOperatingPanel || _isBlending || PanelKeyLock) return;

        _viewCam.Priority = 0;
        _screenDirector.FadeInIcons();
        _staminaBarUI.CanRun = true;
        _cursorImg.gameObject.SetActive(false);
        _crossHair.gameObject.SetActive(true);

        _worldInput.OnClickLeftDown -= PointerDown;
        _worldInput.OnClickLeftUp   -= PointerUp;
        _worldInput.OnMouseAxis     -= SetRayDirection;
        _worldInput.OnLeftKeyDown   -= CashedLeft;
        _worldInput.OnRightKeyDown  -= CashedRight;
        _worldInput.OnESCKeyDown    -= EndPanelControl;

        PanelOperationCompleted?.Invoke();

// #if UNITY_EDITOR
//         _worldInput.OnQKeyDown -= EndPanelControl;
// #endif

        StartCoroutine(WaitForBlendEnd(() =>
        {
            _playerControlSystem.UnFreezePlayerControl();
            _InteractCollider.gameObject.SetActive(true);
            isOperatingPanel = false;
        }));
    }

    /// <summary>
    /// Pointer 합성(Down/Drag/Up)
    /// </summary>
    private PointerEventData BuildPointerData()
    {
        var cam = _curCanvas.worldCamera;
        var screenPos = RectTransformUtility.WorldToScreenPoint(cam, _cursorRect.position);
        var ped = new PointerEventData(EventSystem.current)
        {
            pointerId = 0,
            button = PointerEventData.InputButton.Left,
            position = screenPos,
            pressPosition = _pressScreenPos,
            delta = screenPos - _lastScreenPos
        };
        return ped;
    }

    private void PointerDown()
    {
        if (PanelKeyLock) return;

        var results = RaycastAtCursor();
        if (results.Count == 0) return;

        _pressedObject = results.FirstOrDefault().gameObject; // 첫 번째 히트 대상 고정

        var cam = _curCanvas.worldCamera;
        _pressScreenPos = RectTransformUtility.WorldToScreenPoint(cam, _cursorRect.position);
        _lastScreenPos  = _pressScreenPos;

        var ped = BuildPointerData();
        ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.pointerDownHandler);
    }

    private void PointerDragTick()
    {
        if (_pressedObject == null) return;

        var ped = BuildPointerData();

        if (!_isDragging)
        {
            ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.beginDragHandler);
            _isDragging = true;
        }

        ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.dragHandler);
        _lastScreenPos = ped.position;
    }

    private void PointerUp()
    {
        if (_pressedObject == null) return;

        var ped = BuildPointerData();

        ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.pointerUpHandler);

        if (_isDragging)
        {
            ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.endDragHandler);
        }
        else
        {
            // 드래그가 아니면 클릭으로 간주
            ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.pointerClickHandler);
        }

        _pressedObject = null;
        _isDragging = false;
    }

    // ====== Page switching hooks ======
    public virtual void ChangeNextPage() { }
    public virtual void ChangePastPage() { }

    // ====== Panel lock controls ======
    [Button] public void PanelLock()   => _panelLock = true;
    [Button] public void PanelUnLock() => _panelLock = false;
    
    public virtual void SetPanelKeyLock()      => PanelKeyLock = true;
    public virtual void ReleasePanelKeyLock()  => PanelKeyLock = false;

    public void RemoveESCKey() => _worldInput.OnESCKeyDown -= EndPanelControl;
    public void AddESCKey()    => _worldInput.OnESCKeyDown += EndPanelControl;
}