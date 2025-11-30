# 🪐 목성의 노래

내러티브 1인칭 3D 퍼즐 게임
- ▶️ [**Play Demo**](https://www.youtube.com/watch?v=UEz0kvJCfAg)

<br />

# 📝 프로젝트 정보

### 1. 제작기간

> 2025.07.01 ~ 2025.12.08

### 2. 참여 인원

> |                    Name                    |  Position   |
> | :----------------------------------------: | :---------: |
> | 김재환 | 3D 아트 및 UI 디자이너 |
> | 박태훈 | Unity 클라이언트 프로그래머 |
> | 이상민 | Unity 클라이언트 프로그래머 |
> | 정영호 | 기획 |
> | 박채연 | 2D 아트 |

### 3. 역할 분담

> - 박태훈 : 게임 흐름 FSM 구축 + 사인파 퍼즐 로직 구현 + 패널 가상 커서 드래그 & 드랍 시스템 + 로컬라이징 + `SliceGlitch/ASCIIImage/MotionBlur` 셰이더 구현

<br />

---

## 📌 주요 작업 내용

### 1️⃣ FSM 기반 플레이 모드 아키텍처 설계

#### 🚨 문제 상황

**패널 여는 카메라 블렌딩 중 → 시네마 재생 → 시네마 종료 → 아무 것도 할 수 없음**

플레이어가 인게임 패널을 여는 도중 컷씬이 재생되면, 컷씬이 끝나도 **조작 불가 상태**가 되는 치명적 버그가 발생했습니다.

- 5가지 플레이 모드(Normal/Panel/Cinema/Dialog/Pause)가 **상호배타적**이어야 하는데 **중첩 발생**
- 각 모드 전환 시 정리해야 할 상태가 **여러 파일에 분산**
- 디버깅 시 어디서 상태가 꼬였는지 추적하는 데에 **평균 1시간 소요**

![GameState 버그 영상](https://github.com/user-attachments/assets/fa973d2f-df58-483d-ae3b-05d5104e9bc6)
<br /> *↑ 패널 모드 진입 중 시네마 모드가 끼어들면 발생하는 문제*

---

#### 🎯 해결 방법

<img width="1020" height="458" alt="그림1" src="https://github.com/user-attachments/assets/d0b930d5-8c1a-4120-8fbd-e9b4ee1dfc44" />

**중앙 집중식 FSM으로 모든 플레이 모드를 단일 책임 관리**

<img width="1322" height="456" alt="image" src="https://github.com/user-attachments/assets/73d29e7e-e055-409d-a52c-ab6ecd0f5ad0" />

**핵심 구현 포인트**

1. [**상태 중첩 방지** - `GameState.ChangePlayMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L60)

2. [**자동 정리 훅** - `PanelMode.OnExit`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/PanelMode.cs#L35)

[📂 전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L15)

---

#### 📊 성과

| 개선 항목 | Before | After | 효과 |
|---------|--------|-------|-----|
| 상태 충돌 버그 | 주 2-3건 발생 | **0건** | 100% 해결 |
| 디버깅 소요 시간 | 평균 60분 | 평균 30분 | **50% 감소** |
| 신규 모드 추가 시간 | - | 20분 이내 | `IPlayMode`만 구현 |

---

<details>
<summary><b>🔍 엣지 케이스 해결 과정</b></summary>

<br />

**문제 ①: 일시정지 해제 시 노말 모드로만 돌아감**

- **증상**: 패널 모드에서 일시정지 → 재개 시 패널이 닫혀버림
- **원인**: 모든 모드 종료 시 기본값(`NormalMode`)으로 설정
- **해결**: `PauseMode`가 `prevMode` 저장 후 자체 `Resume()` 메서드로 복구
<br /> [세부 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/PauseMode.cs#L34)

**문제 ②: 시네마 모드 중 패널 모드 전환 시도**

- **증상**: 타임라인 재생 중 다이얼로그 모드 전환 → 시네마 중단
- **원인**: 모든 모드 전환 요청의 우선순위를 동등하게 처리
- **해결**: `ChangePlayMode`에서 시네마 모드 진입 시 다른 모드 요청 무시
<br /> [세부 코드 보기 - `GameState.ChangePlayMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L66)
<br /> [세부 코드 보기 - `CinemaMode.ExitCinemaMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/CinemaMode.cs#L27)

- 시네마 모드는 `TimelineController._timeline.stopped` 훅에서 [**자체적으로 종료**](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/TimelineController.cs#L102)

</details>

---

### 2️⃣ 패널 UGUI Interaction System

#### 🚨 문제 상황

**기존 패널 시스템에 클릭/호버링 기능만 존재**

시그널 퍼즐에 **드래그 & 드랍** 기능이 요구되었습니다.

- 기존 패널 시스템에서는 크로스헤어(월드 조작용)와 패널 UI(캔버스 조작용)가 **동시에 반응**
- 드래그 중 캔버스 밖으로 벗어나면 **입력 유실**

![시그널 퍼즐](https://github.com/user-attachments/assets/a160c3f4-1c15-4820-ba24-a88395dc58cf)
<br /> *↑ 드래그 & 드랍 기능이 필요한 시그널 퍼즐*

---

#### 🎯 해결 방법

**Unity의 EventSystem 파이프라인을 완전히 재구현**
```plaintext
PointerDown
↓
initializePotentialDrag (드래그 준비)
↓
(일정 거리 이동) → BeginDragHandler
↓
DragHandler
↓
PointerUp
↓
EndDragHandler
```

**핵심 구현**

1. **1단계: 기본 클릭만 구현**
```csharp
  // 드래그가 없어서 Slider 조작 불가
  private void PointerDown()
  {
      var results = RaycastAtCursor();
      _pressedObject = results.FirstOrDefault().gameObject;
      
      var ped = BuildPointerData();
      ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.pointerDownHandler);
  }
  
  private void PointerUp()
  {
      var ped = BuildPointerData();
      ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.pointerUpHandler);
      ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.pointerClickHandler);
  }
```

2. **2단계: 드래그 기능 추가 → 새로운 버그 발견**
```csharp
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
  }
```
- 극소량 움직여도 드래그로 인식 → 클릭이 안 됨
- 이미 눌린 상태에서 다시 Down → 이전 입력이 정리 안 됨
- 마우스 왼쪽 Down → 오른쪽 Up → 이벤트 짝이 안 맞음
- Slider Handle 클릭 → Slider 본체가 이벤트를 받아야 함

3. **3단계: Unity EventSystem과 동일한 수준으로 엣지 케이스 처리 (최종)**

<details>
<summary><b>🔧 해결 과정 1: 드래그 임계값 적용</b></summary>

<br />

**문제**: [1픽셀만 움직여도 드래그로 인식되어 클릭이 불가능](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L330)

[Unity의 기본 임계값 캐싱](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L93)
[임계값 이상 이동해야만 드래그 시작](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L269)

</details>

<details>
<summary><b>🔧 해결 과정 2: 중복 입력 방지</b></summary>

<br />

**문제**: [Down 상태인데 다시 Down이 들어오면 이전 입력이 정리되지 않음](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L307)

[이미 뭔가 눌려있다면 먼저 정리하고 새로 시작](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L225)

</details>

<details>
<summary><b>🔧 해결 과정 3: 버튼 짝 검증</b></summary>

<br />

**문제**: [왼쪽 버튼으로 Down → 오른쪽 버튼으로 Up 시 잘못된 이벤트 발생](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L340)

[Down 시점에 어떤 버튼인지 저장](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L227)
[Up 시점에 다른 버튼의 Up이면 무시](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L291)
[실제 입력 바인딩 (왼쪽/오른쪽 구분)](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L464)

</details>

<details>
<summary><b>🔧 해결 과정 4: 실제 이벤트 핸들러 찾기</b></summary>

<br />

**문제**: [Slider의 Handle을 클릭하면 Handle이 이벤트를 받지만, 실제로는 Slider 본체가 받아야 함](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L314)

[상위에서 실제 핸들러를 찾음](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L233)
</details>

<details>
<summary><b>🔧 해결 과정 5: PointerEventData 완전 재현</b></summary>

<br />

**문제**: [Unity EventSystem은 Down/Drag/Up 시점의 위치를 모두 기억하는데, 초기 구현은 현재 위치만 전달](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L296)

[현재/Down 시점 레이캐스트 결과에 대해 Unity와 동일한 정보 제공](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L177)

</details>

<details>
<summary><b>🔧 해결 과정 6: Update문에서 예외 상황 감지</b></summary>

<br />

**문제**: [Down 후 Update가 멈추거나, 입력이 유실되면 영원히 `_pressedObject`가 남아있음](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L72)

[Down 상태인데 마우스가 안 눌려있으면 강제 정리](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L112)

</details>

---

#### 📊 최종 성과

1. 클릭과 드래그가 명확히 구분됨
2. 연속 클릭 시 이전 상태가 간섭하지 않음
3. 마우스 양쪽 버튼을 동시에 사용해도 이벤트 충돌 없음
4. Slider 본체가 반응
5. 예상치 못한 입력 유실에도 상태가 자동 복구됨

[📂 초기 버전 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L307)
[📂 최종 버전 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L219)

---

### 3️⃣ Custom NodeGraph / UnityEvent Graph 확장

#### 🚨 문제 상황

**기존 이벤트 그래프 시스템은 2개 이상 파라미터 메서드의 호출이 불가능**

<img width="781" height="366" alt="image" src="https://github.com/user-attachments/assets/69ea3e47-2097-444d-8cc9-b94cc31b73b1" />
<br /> *↑ 최대 1개 파리미터 메서드만 호출할 수 있는 기존 이벤트그래프 Invoke() 노드*
