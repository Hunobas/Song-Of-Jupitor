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

# 🔨 작업 내용

## **1. FSM 기반 GameState 아키텍처**

### **문제 상황**

- 플레이어가 조작 가능한 노멀 모드에서 UI 조작에만 의존하는 패널 모드로 이동하는 도중, 시네마 컷씬이 재생되는 연출이 겹치면 시네마가 끝나고 아무런 조작도 할 수 없는 문제가 발생했습니다.
  
    - 기존 게임 흐름 로직은 다양한 플레이 모드(노멀 모드/패널 모드/시네마 모드/다이얼로그 모드/일시정지 모드) 사이를 나이브하게 교체하는 방식으로 작동하고 있었습니다.
    - 플레이어의 조작에 따라 분명히 플레이 모드를 교체하는 사이 다른 플레이 모드가 중첩되는 경우가 비일비재했으나, 이와 달리 상호배타적으로 교체되어야만 했습니다.

<img width="1020" height="458" alt="그림1" src="https://github.com/user-attachments/assets/d0b930d5-8c1a-4120-8fbd-e9b4ee1dfc44" />

- 노말 ↔ 패널 ↔ 시네마 ↔ 일시정지 상태 흐름을 안정시키기 위해 GameState FSM 아키텍처를 도입하고 설계했습니다.

◆ 주요 기능

<img width="1322" height="456" alt="image" src="https://github.com/user-attachments/assets/73d29e7e-e055-409d-a52c-ab6ecd0f5ad0" />

- 퍼즐 ↔ 패널 ↔ 시네마 ↔ 일시정지의 모든 상태 흐름을 중앙 `GameState`에서 관리합니다.
- `OnEnter/OnExit` 훅을 표준화해 Panel/Camera/Input/UI 잔여 상태를 자동 정리&상태 중첩 방지합니다.
- 모드별로 흩어져 있던 타임 스케일, 입력 잠금, 카메라 상태, UI 표시 상태에 대해 **어디서 무엇을 정리해야 하는지**가 코드 구조상 드러나도록 합니다. (Ex. [`PanelBase`](https://github.com/Hunobas/Song-Of-Jupitor/blob/d4197df8b6149e4ec91b2d8d4058053f210aa484/Scripts/System/PanelBase.cs#L493) & [`PanelMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/d4197df8b6149e4ec91b2d8d4058053f210aa484/Scripts/System/PanelMode.cs#L20))
- 5개의 재사용될 모드(`Normal/Panel/Dialog/Cinema/Pause`)는 게임 시작 시 정적 인스턴싱합니다.
- 이후 추가될 일회용 모드(특정 챕터 전용 미니게임 등)는 `IPlayMode` 훅만 구현한 동적 생성으로 확장을 고려했습니다.

◆ 성과

- 퍼즐 ↔ 패널 ↔ 시네마 간 교차 상태에서 발생하는 버그의 디버깅 속도가 50% 증가했습니다.
- 플레이 전체 안정성 확보 / 상태 충돌 버그 제거.

[자세한 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/d4197df8b6149e4ec91b2d8d4058053f210aa484/Scripts/System/GameState.cs#L15)

---

**추가 문제 상황 ①.**

- 일시정지 모드 해제(`GameState.Resume`) 시 반드시 이전 플레이 모드로 돌아가야 했습니다.
    
    - 처음에는 모든 플레이 모드가 종료되면 자동으로 노말 모드로 돌아가도록 구현했습니다.
    - 일시정지 모드의 경우, 노말 모드 뿐만 아니라 다른 모드 상태에서도 호출할 수 있으므로 특별하게 노말 모드로 돌아가는게 아닌 이전(prev) 모드로 돌아가야 했습니다.
    
    **⇒ 해결 방법.** 
    
    [자세한 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/d4197df8b6149e4ec91b2d8d4058053f210aa484/Scripts/System/PauseMode.cs#L20)
    
    - `PauseMode` 클래스에서 `OnEnter(prev)` 훅으로부터 받아온 이전 플레이 모드를 내부에 저장합니다.
    - 다른 모드와 달리, 일시정지 모드 해제 시 자신이 주도해서 저장했던 이전 플레이 모드로 돌아갑니다.

---

**추가 문제 상황 ②.** 

- 시네마 모드(`CinemaMode`)는 특별히 타임라인이 끝나기 전까지 일시정지 모드를 제외한 나머지 모드로 전환되어서는 안됩니다.
    
    - 플레이 모드의 우선순위는 일시정지 모드 > 시네마 모드 > 다른 모드.. 이므로, 시네마 모드가 명확히 다른 모드와 겹치는 상황에서 ChangePlayMode 명령을 모두 무시해야 했습니다.
    
    **⇒ 해결 방법.** 
    
    [자세한 코드 보기 - `GameState.ChangePlayMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/d4197df8b6149e4ec91b2d8d4058053f210aa484/Scripts/System/GameState.cs#L66) <br />
    [자세한 코드 보기 - `CinemaMode.ExitCinemaMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/d4197df8b6149e4ec91b2d8d4058053f210aa484/Scripts/System/CinemaMode.cs#L27)
    
    - `GameState.ChangePlayMode` 메서드의 흐름을 비슷하게 따라가는 `CinemaMode.ExitCinemaMode` 메서드를 따로 구현합니다.
    - `PauseMode`와 비슷하게, `TimelineController`에 연결된 타임라인 에셋이 끝날 때 스스로 시네마 모드를 종료하도록 `stopped` 훅에 `CinemaMode.ExitCinemaMode` 메서드를 구독시켰습니다.
