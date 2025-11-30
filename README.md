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

### 1️⃣ FSM 기반 GameState 아키텍처 설계

#### 🚨 문제 상황

**"패널 여는 카메라 블렌딩 중 → 시네마 재생 → 시네마 종료 → 아무 것도 할 수 없음"**

플레이어가 인게임 패널을 여는 도중 컷씬이 재생되면, 컷씬이 끝나도 **조작 불가 상태**가 되는 치명적 버그가 발생했습니다.

- 5가지 플레이 모드(Normal/Panel/Cinema/Dialog/Pause)가 **상호배타적**이어야 하는데 **중첩 발생**
- 각 모드 전환 시 정리해야 할 상태가 **여러 파일에 분산**
- 디버깅 시 어디서 상태가 꼬였는지 추적하는 데에 **평균 1시간 소요**

![GameState 버그 영상](https://github.com/user-attachments/assets/fa973d2f-df58-483d-ae3b-05d5104e9bc6)
*↑ 패널 모드 진입 중 시네마 모드가 끼어들면 발생하는 문제*

---

#### 🎯 해결 방법

<img width="1020" height="458" alt="그림1" src="https://github.com/user-attachments/assets/d0b930d5-8c1a-4120-8fbd-e9b4ee1dfc44" />

**중앙 집중식 FSM으로 모든 플레이 모드를 단일 책임 관리**

<img width="1322" height="456" alt="image" src="https://github.com/user-attachments/assets/73d29e7e-e055-409d-a52c-ab6ecd0f5ad0" />

**핵심 구현 포인트**

1. [**상태 중첩 방지**](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L60)

2. [**자동 정리 훅**](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/PanelMode.cs#L35)

[📂 전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L15)

---

#### 📊 성과

| 개선 항목 | Before | After | 효과 |
|---------|--------|-------|-----|
| 상태 충돌 버그 | 주 2-3건 발생 | **0건** | 100% 해결 |
| 디버깅 소요 시간 | 평균 60분 | 평균 30분 | **50% 감소** |
| 신규 모드 추가 시간 | - | 20분 이내 | IPlayMode만 구현 |

---

<details>
<summary><b>🔍 엣지 케이스 해결 과정</b></summary>

**문제 ①: 일시정지 해제 시 노말 모드로만 돌아감**

- **증상**: 패널 모드에서 일시정지 → 재개 시 패널이 닫혀버림
- **원인**: 모든 모드 종료 시 기본값(NormalMode)으로 설정
- **해결**: `PauseMode`가 `prevMode` 저장 후 자체 `Resume()` 메서드로 복구
[세부 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/PauseMode.cs#L34)

**문제 ②: 시네마 모드 중 패널 모드 전환 시도**

- **증상**: 타임라인 재생 중 다이얼로그 모드 전환 → 시네마 중단
- **원인**: 모든 모드 전환 요청의 우선순위를 동등하게 처리
- **해결**: `ChangePlayMode`에서 시네마 모드 진입 시 다른 모드 요청 무시
[세부 코드 보기 - GameState.ChangePlayMode](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L66)
[세부 코드 보기 - CinemaMode.ExitCinemaMode](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/CinemaMode.cs#L27)

- 시네마 모드는 `TimelineController._timeline.stopped` 훅에서 (**자체적으로 종료**)[https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/TimelineController.cs#L102]

</details>

---

