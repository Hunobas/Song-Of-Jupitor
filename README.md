# 🪐 목성의 노래

내러티브 1인칭 3D 퍼즐 게임
- ▶️ [**Play Demo**](https://www.youtube.com/watch?v=UEz0kvJCfAg)
- 📘 [**전체 포트폴리오**](https://github.com/Hunobas/Portfolio)

<br />

# 📑 목차

- [📝 프로젝트 정보](#-프로젝트-정보)
  - [제작기간](#1-제작기간)
  - [참여 인원](#2-참여-인원)
  - [역할 분담](#3-역할-분담)
- [📌 주요 작업 내용](#-주요-작업-내용)
  - [1️⃣ FSM 기반 플레이 모드 아키텍처 설계](#1️⃣-fsm-기반-플레이-모드-아키텍처-설계)
  - [2️⃣ Unity용 ASCII 이미지 UGUI 렌더러 플러그인](#2️⃣-unity용-ascii-이미지-ugui-렌더러-플러그인)
  - [3️⃣ 패널 UGUI Interaction System](#3️⃣-패널-ugui-interaction-system)
  - [4️⃣ Custom NodeGraph / UnityEvent Graph 확장](#4️⃣-custom-nodegraph--unityevent-graph-확장)
  - [5️⃣ 모션벡터 없는 Camera 모션블러 셰이더 구현](#5️⃣-모션벡터-없는-camera-모션블러-셰이더-구현)

<br />

---

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

#### 🎯 해결 방법

<img width="1020" height="458" alt="그림1" src="https://github.com/user-attachments/assets/d0b930d5-8c1a-4120-8fbd-e9b4ee1dfc44" />

**중앙 집중식 FSM으로 모든 플레이 모드를 단일 책임 관리**

<img width="1322" height="456" alt="image" src="https://github.com/user-attachments/assets/73d29e7e-e055-409d-a52c-ab6ecd0f5ad0" />

**핵심 구현 포인트**

1. [**상태 중첩 방지** - `GameState.ChangePlayMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L60)
2. [**자동 정리 훅** - `PanelMode.OnExit`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/PanelMode.cs#L35)

[📂 전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L15)

#### 📊 성과

| 개선 항목 | Before | After | 효과 |
|---------|--------|-------|-----|
| 상태 충돌 버그 | 주 2-3건 발생 | **0건** | 100% 해결 |
| 디버깅 소요 시간 | 평균 60분 | 평균 30분 | **50% 감소** |
| 신규 모드 추가 시간 | - | 20분 이내 | `IPlayMode`만 구현 |

<details>
<summary><b>🔧 해결 과정 1: 일시정지 해제 시 이전 플레이 모드로 돌아감</b></summary>

<br />

- **증상**: 패널 모드에서 일시정지 → 재개 시 패널이 닫혀버림
- **원인**: 모든 모드 종료 시 기본값(`NormalMode`)으로 설정
- **해결**: `PauseMode`가 `prevMode` 저장 후 자체 `Resume()` 메서드로 복구
<br /> [세부 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/PauseMode.cs#L34)

</details>

<details>
<summary><b>🔧 해결 과정 2: 시네마 모드 중 다이얼로그 모드 전환 무시</b></summary>

<br />

- **증상**: 타임라인 재생 중 다이얼로그 모드 전환 → 시네마 중단
- **원인**: 모든 모드 전환 요청의 우선순위를 동등하게 처리
- **해결**: `ChangePlayMode`에서 시네마 모드 진입 시 다른 모드 요청 무시
<br /> [세부 코드 보기 - `GameState.ChangePlayMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L66)
<br /> [세부 코드 보기 - `CinemaMode.ExitCinemaMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/CinemaMode.cs#L27)

- 시네마 모드는 `TimelineController._timeline.stopped` 훅에서 [**자체적으로 종료**](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/TimelineController.cs#L102)

</details>

---

### 2️⃣ Unity용 ASCII 이미지 UGUI 렌더러 플러그인

#### 🚨 문제 상황

**아트 팀이 아스키 아트를 편집할 때마다 프로그래머에게 요청**

게임 내 터미널 UI에 아스키 아트가 필요했지만, 기존 방식은 아트 팀의 작업 흐름을 막았습니다.

- 포토샵에서 ASCII 변환 → 텍스트 파일 → Unity에 수동 복붙
- 색상/밝기 조정할 때마다 **전체 과정 반복**
- 애니메이션 프레임마다 **수작업 필요**
- 아트 팀원: "이거 좀 더 밝게 해주세요" → 프로그래머 호출

![image (2)](https://github.com/user-attachments/assets/389ec02c-9fdf-4cdd-aa57-0c9e79bbfa4b)
<br /> *↑ 목표: Unity 에디터에서 실시간 미리보기 가능한 아스키 렌더러*

#### 🎯 해결 방법 (1단계 → 2단계 → 3단계)

**1단계: 기본 기능 구현 → 심각한 성능 문제 발견**

[초기 구현: CPU에서 모든 픽셀 읽기](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L312)

**🐛 문제점:**
   - 160×90 그리드 × 4×4 슈퍼샘플 = **230,400회** 픽셀 접근
   - 각 픽셀마다 `<color>` 태그 생성 → **문자열 길이 76,800자**
   - `Update()` 호출 시 **CPU 점유 27.6ms, 프레임 비중 70.4%**

**2단계: GPU에서 먼저 다운샘플 → 비동기 Readback → 색이 바뀌는 구간에만 태그**

[📂 전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/eb4c59e1717a806b9d3d89dc7e6dd77ab297f198/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L51)  
[📂 초기 버전 (최적화 전)](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L26)  
[📝 UPM 플러그인 GitHub](https://github.com/Hunobas/AsciiImageUGUI-UPM)  
[📜 개발일지 전문](https://velog.io/@po127992/목성의-노래-Unity-ASCII-렌더러-공유-및-개발일지)

#### 📊 성과

<img width="1181" height="250" alt="image" src="https://github.com/user-attachments/assets/7b108303-6448-4f9e-9d31-c916c3d97ea6" />

| 개선 항목 | Before | After | 개선률 |
|---------|--------|-------|--------|
| CPU 시간 (1프레임) | 27.6ms | **2.15ms** | **92% 감소** |
| 프레임 비중 | 70.4% | **3.5%** | **95% 감소** |
| 문자열 길이 | 76,800자 | ~20,000자 | **74% 감소** |
| 아트 팀 작업 시간 | 조정당 5분 | **실시간** | - |

<details>
<summary><b>🔧 해결 과정 1: GPU 다운샘플링 + AsyncGPUReadback</b></summary>

<br />

**문제**: [Texture2D.ReadPixels()는 GPU → CPU 전송이 끝날 때까지 메인 스레드 블록킹](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L235)

**해결 1: GPU 다운샘플 먼저 수행**

```csharp
// 커스텀 셰이더로 Sprite UV 영역만 잘라서 다운샘플
Shader "Hidden/Ascii/UVBlit"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            float4 _UVRect; // (x, y, width, height)
            float _FlipY;
            
            float2 vert(float4 pos : POSITION) : TEXCOORD0
            {
                float2 uv = pos.xy * 0.5 + 0.5;
                uv = _UVRect.xy + uv * _UVRect.zw;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                return uv;
            }
            
            fixed4 frag(float2 uv : TEXCOORD0) : SV_Target
            {
                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
```

```csharp
void DownsampleToRT()
{
    // 스프라이트 UV 영역만 잘라서 160×90×4 크기로 축소
    _blitMat.SetVector(_UVRectID, new Vector4(
        _spriteUv.x, _spriteUv.y, 
        _spriteUv.width, _spriteUv.height
    ));
    Graphics.Blit(_srcTex, _downRT, _blitMat);
}
```

[세부 코드 보기 - DownsampleToRT](https://github.com/Hunobas/Song-Of-Jupitor/blob/eb4c59e1717a806b9d3d89dc7e6dd77ab297f198/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L504)

**해결 2: AsyncGPUReadback으로 비동기 전송**

```csharp
AsyncGPUReadbackRequest _pendingReq;
NativeArray<Color32> _frame;
bool _frameValid;

void KickReadback()
{
    if (_downRT == null || _pendingReq.done == false)
        return;
    
    // 비동기 요청 (메인 스레드 블록 안 함)
    _pendingReq = AsyncGPUReadback.Request(_downRT, 0, OnReadbackComplete);
}

void OnReadbackComplete(AsyncGPUReadbackRequest req)
{
    if (req.hasError) return;
    
    // GPU → CPU 전송 완료 (백그라운드)
    _frame.CopyFrom(req.GetData<Color32>());
    _frameValid = true;
}

void Update()
{
    DownsampleToRT();      // GPU 작업 큐에 추가
    KickReadback();        // 비동기 요청
    TryConsumeReadback();  // 이전 프레임 데이터 소비
}
```

[세부 코드 보기 - AsyncGPUReadback](https://github.com/Hunobas/Song-Of-Jupitor/blob/eb4c59e1717a806b9d3d89dc7e6dd77ab297f198/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L536)

**성과**: 
- GPU → CPU 전송이 **백그라운드로 이동**
- 메인 스레드 블록킹 **완전 제거**
- 1프레임 지연 발생하지만 실시간 애니메이션에서는 **눈에 띄지 않음**

</details>

<details>
<summary><b>🔧 해결 과정 2: 색 구간 병합 (Running Color Tag)</b></summary>

<br />

**문제**: [모든 픽셀마다 `<color=#RRGGBB>문자</color>` 태그 생성 → 문자열 오버헤드 급증](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L421)

**해결**: 색이 바뀌는 구간에만 태그 열고/닫기

```csharp
// ❌ 기존: 픽셀마다 태그
for (int c = 0; c < cols; c++)
{
    Color avg = SamplePixel(c, r);
    _sb.Append($"<color=#{ToHex(avg)}>{ch}</color>");
}
// 결과: <color=#FF0000>A</color><color=#FF0000>B</color><color=#FE0000>C</color>
```

```csharp
// ✅ 개선: 색 구간 병합
int lastColorKey = -1;
bool colorOpen = false;

for (int c = 0; c < cols; c++)
{
    int key = Quantize12bit(avg);
    
    if (key != lastColorKey)
    {
        if (colorOpen) _sb.Append("</color>");
        _sb.Append(GetOrMakeColorTag(key));
        colorOpen = true;
        lastColorKey = key;
    }
    
    _sb.Append(ch);
}
// 결과: <color=#FF0000>AB</color><color=#FE0000>C</color>
```

[세부 코드 보기 - GenerateAsciiFromFrame](https://github.com/Hunobas/Song-Of-Jupitor/blob/eb4c59e1717a806b9d3d89dc7e6dd77ab297f198/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L690)

**Before/After 비교:**

| 케이스 | 기존 문자열 | 개선 문자열 |
|--------|------------|-----------|
| 동일 색 5개 | `<color>A</color><color>B</color>...` (95자) | `<color>ABCDE</color>` (28자) |
| 3색 전환 | `<color>A</color><color>B</color><color>C</color>` (57자) | `<color>A</color><color>B</color><color>C</color>` (57자) |

**실제 효과**: 
- 일반적인 이미지는 인접 픽셀끼리 색이 비슷함
- 평균적으로 태그 개수 **70-80% 감소**

</details>

<details>
<summary><b>🔧 해결 과정 3: 12bit 색 양자화 + 캐싱</b></summary>

<br />

**문제**: [24bit 색상(16M가지) → 태그 문자열 생성 비용 높음](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L422C21-L422C33)

**해결**: 각 채널을 4bit로 양자화 → 4096가지 색만 사용

```csharp
// 12bit 양자화 (R4G4B4)
int Quantize12bit(Color avg)
{
    return ((int)(avg.r * 15f) << 8) |  // R: 0-15
           ((int)(avg.g * 15f) << 4) |  // G: 0-15
           (int)(avg.b * 15f);          // B: 0-15
    // 총 16 × 16 × 16 = 4096가지
}

// 캐시에서 태그 가져오기
Dictionary<int, string> _colorTagCache = new(256);

string GetOrMakeColorTag(int key)
{
    if (_colorTagCache.TryGetValue(key, out var tag))
        return tag;  // ★ 캐시 히트
    
    // 4bit → 8bit 복원 (0-15 → 0-255)
    byte r4 = (byte)((key >> 8) & 0xF);
    byte r = (byte)((r4 << 4) | r4);  // 예: 15 → 255
    
    tag = $"<color=#{r:X2}{g:X2}{b:X2}>";
    _colorTagCache[key] = tag;
    return tag;
}
```

[세부 코드 보기 - GetOrMakeColorTag](https://github.com/Hunobas/Song-Of-Jupitor/blob/eb4c59e1717a806b9d3d89dc7e6dd77ab297f198/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L720)

**Before/After:**

| 항목 | 24bit | 12bit |
|------|-------|-------|
| 가능한 색 | 16,777,216 | **4,096** |
| 태그 생성 횟수 | 픽셀 수만큼 | **구간 수만큼** (~500회) |
| 캐시 적중률 | 낮음 | **높음** (>90%) |

**시각적 차이**: 
- ASCII 아트는 해상도가 낮아서 12bit로도 충분
- 육안으로 거의 구분 불가

</details>

---

### 3️⃣ 패널 UGUI Interaction System

#### 🚨 문제 상황

**기존 패널 시스템에 클릭/호버링 기능만 존재**

시그널 퍼즐에 **드래그 & 드랍** 기능이 요구되었습니다.

- 기존 패널 시스템에서는 크로스헤어(월드 조작용)와 패널 UI(캔버스 조작용)가 **동시에 반응**
- 드래그 중 캔버스 밖으로 벗어나면 **입력 유실**

![시그널 퍼즐](https://github.com/user-attachments/assets/a160c3f4-1c15-4820-ba24-a88395dc58cf)
<br /> *↑ 목표: 드래그 & 드랍 기능이 필요한 시그널 퍼즐*

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

2. **2단계: 드래그 기능 추가**
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

**🐛 문제점:**
   - 극소량 움직여도 드래그로 인식 → 클릭이 안 됨
   - 이미 눌린 상태에서 다시 Down → 이전 입력이 정리 안 됨
   - 마우스 왼쪽 Down → 오른쪽 Up → 이벤트 짝이 안 맞음
   - Slider Handle 클릭 → Slider 본체가 이벤트를 받아야 함

<br /> 3. **3단계: Unity EventSystem과 동일한 수준으로 엣지 케이스 처리**

[📂 초기 버전 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L307)
<br /> [📂 최종 버전 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L219)

#### 📊 성과

1. **클릭과 드래그**가 명확히 구분됨
2. 연속 클릭 시 이전 상태가 간섭하지 않음
3. 마우스 양쪽 버튼을 동시에 사용해도 이벤트 충돌 없음
4. **Slider 본체**가 반응
5. 예상치 못한 입력 유실에도 상태가 자동 복구됨

<details>
<summary><b>🔧 해결 과정 1: 드래그 임계값 적용</b></summary>

<br />

**문제**: [1픽셀만 움직여도 드래그로 인식되어 클릭이 불가능](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L330)

```csharp
// Unity의 기본 임계값 캐싱
protected virtual void Awake()
{
    if (EventSystem.current != null)
    {
        float th = EventSystem.current.pixelDragThreshold;
        _dragThresholdSqr = th * th; // 제곱해서 저장 (sqrMagnitude 비교용)
    }
}
```

```csharp
private void PointerDragTick()
{
    if (!_pressedSupportsDrag) return; // ★ 드래그 지원 여부 확인
    
    // ★ 임계값 이상 이동해야만 드래그 시작
    float distSqr = (ped.position - _pressScreenPos).sqrMagnitude;
    if (!_isDragging && distSqr >= _dragThresholdSqr)
    {
        ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.beginDragHandler);
        _isDragging = true;
    }

    if (_isDragging)
        ExecuteEvents.Execute(_pressedObject, ped, ExecuteEvents.dragHandler);
}
```

[세부 코드 보기 - Awake](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L93)
<br /> [세부 코드 보기 - PointerDragTick](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L269)

</details>

<details>
<summary><b>🔧 해결 과정 2: 중복 입력 방지</b></summary>

<br />

**문제**: [Down 상태인데 다시 Down이 들어오면 이전 입력이 정리되지 않음](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L307)

```csharp
private void PointerDown(PointerEventData.InputButton btn)
{
    // ★ 이미 뭔가 눌려있다면 먼저 정리하고 새로 시작
    if (_pressedObject != null)
        ForceReleasePointer();

    _pressedButton = btn;
    // ...
}

private void ForceReleasePointer()
{
    _pressedObject = null;
    _pressedSupportsDrag = false;
    _isDragging = false;
}
```

[세부 코드 보기 - PointerDown](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L225)

</details>

<details>
<summary><b>🔧 해결 과정 3: 버튼 짝 검증</b></summary>

<br />

**문제**: [왼쪽 버튼으로 Down → 오른쪽 버튼으로 Up 시 잘못된 이벤트 발생](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L340)

```csharp
// Down 시점에 어떤 버튼인지 저장
private PointerEventData.InputButton _pressedButton;

private void PointerDown(PointerEventData.InputButton btn)
{
    _pressedButton = btn; // ★ 저장
    // ...
}

private void PointerUp(PointerEventData.InputButton btn)
{
    // ★ 다른 버튼의 Up이면 무시
    if (_pressedObject == null || btn != _pressedButton)
        return;
    // ...
}
```

```csharp
// 실제 입력 바인딩 (왼쪽/오른쪽 구분)
_panelInput.OnClickLeftDown  += () => PointerDown(InputButton.Left);
_panelInput.OnClickLeftUp    += () => PointerUp(InputButton.Left);
_panelInput.OnClickRightDown += () => PointerDown(InputButton.Right);
_panelInput.OnClickRightUp   += () => PointerUp(InputButton.Right);
```

[세부 코드 보기 - PointerDown](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L227)
<br /> [세부 코드 보기 - PointerUp](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L291)
<br /> [세부 코드 보기 - 입력 바인딩](https://github.com/Hunobas/Song-Of-Jupitor/blob/28b16dda09ae410124e0763ff97627d8ad92b76d/Scripts/System/PanelBase.cs#L508)

</details>

<details>
<summary><b>🔧 해결 과정 4: 실제 이벤트 핸들러 찾기</b></summary>

<br />

**문제**: [Slider의 Handle을 클릭하면 Handle이 이벤트를 받지만, 실제로는 Slider 본체가 받아야 함](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L314)

```csharp
// ❌ 기존: 레이캐스트 히트된 오브젝트를 그대로 사용
_pressedObject = HitTopMost();

// ✅ 개선: 상위에서 실제 핸들러를 찾음
private void PointerDown(PointerEventData.InputButton btn)
{
    var hit = HitTopMost();
    if (hit == null) return;

    // ★ 우선순위대로 핸들러를 찾아 올라감
    _pressedObject =
        FindHandlerTarget<IBeginDragHandler>(hit) ??
        FindHandlerTarget<IDragHandler>(hit) ??
        FindHandlerTarget<IPointerClickHandler>(hit) ??
        FindHandlerTarget<IPointerDownHandler>(hit);
}

private static GameObject FindHandlerTarget<T>(GameObject start) 
    where T : IEventSystemHandler
    => ExecuteEvents.GetEventHandler<T>(start);
```

[세부 코드 보기 - PointerDown](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L233)

</details>

---

### 4️⃣ Custom NodeGraph / UnityEvent Graph 확장

#### 🚨 문제 상황

**기존 이벤트 그래프는 2개 이상 파라미터 메서드 호출이 불가능**

Unity 기본 `UnityEvent`는 최대 1개 파라미터만 지원하며, 기존 이벤트 그래프의 `Invoke()` 노드도 동일한 제약이 있었습니다.

- 컷씬 연출에 필요한 복잡한 메서드 호출 불가 (예: `SetCameraShake(amplitude, frequency, duration)`)
- 파라미터마다 노드를 쪼개면 **실행 순서 보장 안 됨**
- 기획팀이 직접 그래프 편집 시 **실수 확률 증가**

<img width="781" height="366" alt="image" src="https://github.com/user-attachments/assets/69ea3e47-2097-444d-8cc9-b94cc31b73b1" />
<br /> *↑ 최대 1개 파라미터 메서드만 호출할 수 있는 기존 이벤트그래프 `Invoke` 노드*

#### 🎯 해결 방법

**노드 생명주기 기반 커스텀 액션 시스템 구축**

```plaintext
[기존 Invoke 노드]
단순 메서드 호출 → 즉시 다음 노드

[구현한 ActionNode 시스템]
Init() → Delay 대기 → OnStart() → OnUpdate() (매 프레임) → OnComplete()
          ↓                                ↓
    WaitPolicy에 따라              IsFinished == true 감지
    다음 노드 진행 여부 결정          → 다음 노드 진행
```

**핵심 구현 포인트**

1. **IActionNode 인터페이스로 노드 생명주기 정의**
```csharp
public interface IActionNode
{
    void Init();                      // 런타임 초기화
    void OnStart(CoroutineDelegator); // 실행 시작
    void OnUpdate(float deltaTime);   // 매 프레임 갱신
    void OnComplete();                // 정리
    bool IsFinished { get; }          // 완료 여부
}
```

2. **WaitPolicy로 실행 흐름 제어**
```csharp
public enum WaitPolicy 
{ 
    Inherit,      // 노드 설정 따름
    ForceWait,    // 강제로 완료까지 대기
    ForceNoWait   // 즉시 다음 노드로 (백그라운드 실행)
}
```

3. **ActionNodeBase로 보일러플레이트 제거**
   - 자식 노드는 `CreateAction()` 메서드만 구현
   - Delay, Wait, UnscaledTime 옵션 자동 처리
   - 에디터 UI 자동 생성

[📂 EventGraph 전체 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/main/Scripts/EventGraph/EventGraphProcessor.cs)  
[📂 실전 노드 15개 모음](https://github.com/Hunobas/Song-Of-Jupitor/tree/main/Scripts/EventGraph/Customs/Nodes)

#### 📊 성과

<img width="699" height="425" alt="image" src="https://github.com/user-attachments/assets/3f5355f3-19d4-4490-a180-5c655b812547" />
<br /> *↑ 2개 이상의 파라미터를 받을 수 있고 실행 흐름을 커스텀할 수 있는 이벤트그래프 커스텀 노드*

<details>
<summary><b>🔧 구현 과정 1: 노드 생명주기 표준화</b></summary>

<br />

**문제**: 각 노드마다 실행 방식이 달라 코드 중복 발생

**해결**: `ActionNodeBase` 추상 클래스로 공통 로직 분리

```csharp
public abstract class ActionNodeBase : SequentialNode
{
    [Input("Delay")] public float delay = 0f;
    [Setting("Wait Until Finished")] public bool waitUntilFinished = true;
    [Setting("Unscaled Time")] public bool unscaledTime = false;

    // 자식 노드는 이것만 구현하면 됨
    protected abstract IActionNode CreateAction();

    public sealed override BakedEventNode GetBakedNode()
        => new BakedActionNode(CreateAction(), delay, waitUntilFinished, unscaledTime);
}
```

[세부 코드 보기 - ActionNodeBase](https://github.com/Hunobas/Song-Of-Jupitor/blob/ff8e930744aef5769f6bb1d1b53c50be8dc31b3b/Scripts/EventGraph/Customs/ActionNodeBase.cs#L32)

**성과**: 
- 신규 노드 작성 시간 **60% 감소**
- Delay/Wait/UnscaledTime 로직 중복 **완전 제거**

</details>

<details>
<summary><b>🔧 구현 과정 2: 백그라운드 실행 vs 대기 실행</b></summary>

<br />

**문제**: 카메라 셰이크는 백그라운드 실행, 컷씬 이미지는 대기 필요

**해결**: `BakedActionNode`에서 `WaitPolicy` 분기 처리

```csharp
public override void Invoke(Action<BakedEventNode> onDone, BakedEventNode prevNode)
{
    bool mustWait = _policy switch
    {
        WaitPolicy.ForceWait   => true,
        WaitPolicy.ForceNoWait => false,
        _                      => _wait  // Inherit
    };

    if (mustWait) 
        _delegator.InvokeOnMono(RunWaitThenComplete(onDone));
    else
    {
        _delegator.InvokeOnMono(RunImmediatelyAndForget());
        onDone?.Invoke(this);  // 즉시 다음 노드로
    }
}
```

[세부 코드 보기 - BakedActionNode](https://github.com/Hunobas/Song-Of-Jupitor/blob/ff8e930744aef5769f6bb1d1b53c50be8dc31b3b/Scripts/EventGraph/Customs/ActionNodeBase.cs#L52)

**사용 예시:**
- **ForceNoWait**: 카메라 셰이크 (2.5초 페이드아웃 중에도 다음 노드 진행)
- **ForceWait**: 컷씬 이미지 (Duration 끝날 때까지 대기)

</details>

<details>
<summary><b>🔧 구현 과정 3: 런타임 Abort 시스템</b></summary>

<br />

**문제**: 노드 실행 중 필수 참조가 `null`이면 무한 대기

**해결**: `EventGraphRuntime` 스택으로 현재 실행 컨텍스트 추적

```csharp
public static class EventGraphRuntime
{
    static readonly Stack<EventGraphProcessor> _stack = new();
    
    public static void Abort(string message, UnityEngine.Object context = null)
        => Current?.Abort(message, context);
}
```

```csharp
// 노드 내부에서 사용 예시
if (brain == null)
{
    EventGraphRuntime.Abort("CinemachineBrain이 비어있습니다.", null);
    return null;
}
```

[세부 코드 보기 - EventGraphRuntime](https://github.com/Hunobas/Song-Of-Jupitor/blob/ff8e930744aef5769f6bb1d1b53c50be8dc31b3b/Scripts/EventGraph/EventGraphProcessor.cs#L12)

**성과**: 
- 그래프 실행 중 에러 발생 시 **즉시 중단 + 로그 출력**
- 디버깅 시간 **70% 단축**

</details>

<details>
<summary><b>🔧 구현 과정 4: 에디터 UI 자동 생성</b></summary>

<br />

**문제**: 각 노드마다 커스텀 에디터 작성 필요

**해결**: `ActionNodeView`로 공통 UI 자동 생성 + Reflection으로 타이틀 변경

```csharp
[NodeCustomEditor(typeof(ActionNodeBase))]
public class ActionNodeView : BaseNodeView
{
    public override void Enable(bool fromInspector = false)
    {
        base.Enable(fromInspector);
        
        SetTitle();  // Reflection으로 DisplayName 추출
        
        // Wait/Unscaled 옵션 자동 추가
        var fWait = new PropertyField(_pWait, "Wait Until Finished");
        var fUnscaled = new PropertyField(_pUnscaled, "Unscaled Time");
        controlsContainer.Add(fWait);
        controlsContainer.Add(fUnscaled);
        
        // ForceWait/ForceNoWait면 UI 비활성화
        if (IsForcedPolicy(out var forced))
            fWait.SetEnabled(false);
    }
}
```

[세부 코드 보기 - ActionNodeView](https://github.com/Hunobas/Song-Of-Jupitor/blob/ff8e930744aef5769f6bb1d1b53c50be8dc31b3b/Scripts/EventGraph/Customs/ActionNodeView.cs#L12)

**Before/After:**

| | Before | After |
|---|--------|-------|
| 커스텀 에디터 코드 | 노드당 50-100줄 | **0줄** |
| UI 일관성 | 노드마다 다름 | **완전 통일** |

</details>

<details>
<summary><b>📝 실전 노드 예시 1: Node_Start6DShake</b></summary>

<br />

**요구사항**: 카메라 셰이크를 시작하고, **셰이크가 끝나기 전에 다음 노드로 진행**

```csharp
[NodeMenuItem(EventCategories.Camera + "카메라 6D 셰이크")]
public sealed class Node_Start6DShake : ActionNodeBase
{
    [Input] public CinemachineBrain brain;
    [Input] public NoiseSettings noiseProfile;
    [Input] public float amplitude = 3f;
    [Input] public float frequency = 3f;
    [Input] public float shakeDuration = 2.5f;

    protected override string DisplayName => "카메라 셰이크";
    protected override WaitPolicy WaitBehavior => WaitPolicy.ForceNoWait;  // ★

    protected override IActionNode CreateAction()
        => new Start6DShakeAction(brain, noiseProfile, amplitude, frequency, shakeDuration);
}
```

```csharp
sealed class Start6DShakeAction : IActionNode
{
    public void OnStart(CoroutineDelegator delegator)
    {
        // Perlin Noise 초기 설정
        _perlin.m_AmplitudeGain = _amp;
        _perlin.m_FrequencyGain = _freq;
        
        // 2.5초 페이드아웃 시작 (백그라운드)
        _delegator.InvokeOnMono(FadeOut());
    }
    
    IEnumerator FadeOut()
    {
        float t = 0f;
        while (t < _dur)
        {
            t += Time.deltaTime;
            _perlin.m_AmplitudeGain = Mathf.Lerp(_amp, 0f, t / _dur);
            yield return null;
        }
    }
}
```

[전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/ff8e930744aef5769f6bb1d1b53c50be8dc31b3b/Scripts/EventGraph/Customs/Nodes/Camera/Node_Start6DShake.cs#L15)

**결과**: 
- 다음 노드(대사 재생)가 **즉시 실행**
- 셰이크는 **백그라운드에서 2.5초간 페이드아웃**

</details>

<details>
<summary><b>📝 실전 노드 예시 2: Node_CutsceneImage</b></summary>

<br />

**요구사항**: 컷씬 이미지를 표시하고, **Duration이 끝날 때까지 대기**

```csharp
[NodeMenuItem(EventCategories.Display + "컷씬 이미지")]
public sealed class Node_CutsceneImage : ActionNodeBase
{
    [Input] CutscenePanelBase _panel;
    [Input] Sprite _sprite;
    [Input] float _duration = 1.0f;
    
    [ToggleLeft] bool _useVignette = false;
    [ShowIf(nameof(_useVignette))] bool _vignetteAnimated = false;

    protected override string DisplayName => "컷씬 이미지";
    // WaitPolicy 지정 안 함 → Inherit → 노드의 waitUntilFinished 따름

    protected override IActionNode CreateAction()
        => new CutsceneImageAction(_panel, _sprite, _useVignette, _vignetteAnimated, _duration);
}
```

```csharp
sealed class CutsceneImageAction : IActionNode
{
    public void OnStart(CoroutineDelegator delegator)
    {
        _panel.ShowSprite(_sprite);
        
        if (_useVignette)
        {
            _panel.ShowVignette();
            if (_vignetteAnimated)
                _panel.VignetteAnimator.Play(0, 0, 0f);
        }
    }

    public void OnUpdate(float deltaTime)
    {
        _elapsed += deltaTime;
        if (_elapsed >= _duration)
        {
            _panel.CloseSprite();
            _finished = true;  // ★ 여기서 다음 노드로 진행
        }
    }
}
```

[전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/ff8e930744aef5769f6bb1d1b53c50be8dc31b3b/Scripts/EventGraph/Customs/Nodes/Display/Node_CutsceneImage.cs#L9)

---

**결과**: 
- Duration(1초) 동안 **이미지 표시**
- 1초 후 자동으로 **다음 노드로 진행**

</details>

---

### 5️⃣ 모션벡터 없는 Camera 모션블러 셰이더 구현

#### 🚨 문제 상황

**씬에 BaseLayer 카메라가 2개 이상 있으면 모션 블러 무시됨**

Unity URP에서 BaseLayer 카메라가 여러 개 있는 씬에서는 모션 벡터 렌더링이 충돌하여 모션 블러가 작동하지 않았습니다.

- Unity URP의 모션 블러는 **모션 벡터 텍스처**에 의존
- BaseLayer 카메라 2개 → 모션 벡터 렌더 타겟 충돌
- Volume Override의 Motion Blur가 **먼저 렌더링된 카메라**에만 적용
- 두 번째 카메라는 모션 벡터 없이 렌더링 → **모션 블러 효과 사라짐**

<img width="1839" height="916" alt="image" src="https://github.com/user-attachments/assets/52ed330a-0238-4c12-be0d-0bc4d6086860" />
<br /> *↑ Main Camera와 Player Camera가 모두 Base Layer → Motion Blur Volume 무시됨*

---

#### 🎯 해결 방법

**모션 벡터가 필요 없는 정적 방향 블러 Scriptable Render Feature 구현**

```plaintext
[Unity 기본 Motion Blur]
모션 벡터 텍스처 필요 → BaseLayer 카메라 충돌

[커스텀 Camera Blur]
방향/중심점 기반 정적 블러 → 모션 벡터 불필요
```

**핵심 구현 포인트**

1. **2가지 블러 타입 지원**
```csharp
public enum BlurType 
{ 
    Linear,  // 각도 방향으로 블러 (카메라 이동 효과)
    Radial   // 중심점에서 방사형 블러 (속도감)
}
```

2. **3가지 샘플링 방법**
```csharp
public enum BlurMethod 
{ 
    Gaussian,      // 가우시안 가중치 (자연스러움)
    Fixed,         // 균일 가중치 (또렷함)
    Proportional   // 거리 비례 가중치 (중간)
}
```

3. **애니메이션 친화적 설계**
   - `CameraBlurController` 컴포넌트의 필드를 직접 애니메이션 가능
   - Timeline/Animator에서 `intensity`, `angleDeg` 등을 키프레임으로 제어
   - Downsample/Iterations로 품질-성능 트레이드오프

[📂 전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/10a1e7beee04279e75c236bbac08075c8c4097b4/Scripts/Renders/CameraBlur/CameraBlurController.cs#L24)  
[📂 Shader 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/main/Scripts/Renders/CameraBlur/CameraBlur.shader)  
[📂 Render Feature](https://github.com/Hunobas/Song-Of-Jupitor/blob/10a1e7beee04279e75c236bbac08075c8c4097b4/Scripts/Renders/CameraBlur/CameraBlurFeature.cs#L6)

#### 📊 성과

1. 모션 벡터 텍스처 의존성 제거된 연출
2. **각도/중심점 자유롭게 설정** 가능한 블러 방향
3. Timeline 애니메이션 지원
4. 카메라가 정지 상태에서도 **속도감 연출 가능**

<details>
<summary><b>🔧 구현 과정 1: Scriptable Render Feature 기반 구조</b></summary>

<br />

**문제**: Unity 기본 Volume Override는 모션 벡터에 의존

**해결**: Custom Render Pass로 완전히 독립적인 블러 구현

```csharp
// Scriptable Renderer Feature
public class CameraBlurFeature : ScriptableRendererFeature 
{
    CameraBlurPass _pass;

    public override void Create() 
    {
        _pass = new CameraBlurPass(Params);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data) 
    {
        // 카메라에 CameraBlurController가 있으면 활성화
        if (!_pass.Setup(renderer, ref data)) return;
        renderer.EnqueuePass(_pass);
    }
}
```

[세부 코드 보기 - CameraBlurFeature](https://github.com/Hunobas/Song-Of-Jupitor/blob/10a1e7beee04279e75c236bbac08075c8c4097b4/Scripts/Renders/CameraBlur/CameraBlurFeature.cs#L6)

</details>

<details>
<summary><b>🔧 구현 과정 2: Linear/Radial Blur 셰이더</b></summary>

<br />

**Linear Blur: 각도 방향으로 블러**

```hlsl
float3 BlurLinear(float2 uv) 
{
    // 각도를 방향 벡터로 변환
    float2 dir = float2(cos(_AngleRad), sin(_AngleRad));
    float2 stepUV = dir * _RadiusPx * _TexelSize.xy / max(_RadiusPx, 1.0);
    
    const int TAPS = 13;
    float3 acc = 0; 
    float wsum = 0;
    
    // 방향으로 13개 탭 샘플링
    [unroll] for (int i = -(TAPS/2); i <= (TAPS/2); ++i) 
    {
        float k = (float)i;
        float2 uvk = uv + stepUV * k;
        
        // 가우시안/균일/비례 가중치 선택
        #if defined(METHOD_GAUSS)
            float w = gaussianWeight(k, sigma);
        #elif defined(METHOD_FIXED)
            float w = 1.0;
        #else
            float w = abs(k) + 1.0;
        #endif
        
        acc += SAMPLE_TEXTURE2D(tex, uvk).rgb * w;
        wsum += w;
    }
    
    return acc / max(wsum, 1e-4);
}
```

**Radial Blur: 중심점에서 방사형**

```hlsl
float3 BlurRadial(float2 uv) 
{
    // 중심점에서 현재 픽셀로의 방향
    float2 dir = normalize(uv - _Center);
    float2 stepUV = dir * (_RadiusPx * _TexelSize.xy) / steps;
    
    // 중심에서 바깥으로 13개 탭 샘플링
    [unroll] for (int i=0; i<TAPS; ++i) 
    {
        float t = ((i/(TAPS-1.0)) - 0.5) * 2.0;
        float2 uvk = uv + stepUV * t * steps;
        // ... 가중치 계산 및 누적
    }
    
    return acc / max(wsum, 1e-4);
}
```

[세부 코드 보기 - Shader](https://github.com/Hunobas/Song-Of-Jupitor/blob/main/Scripts/Renders/CameraBlur/CameraBlur.shader)

</details>

<details>
<summary><b>🔧 구현 과정 3: 애니메이션 친화적 Controller</b></summary>

<br />

**문제**: Volume Override는 Timeline에서 키프레임 애니메이션 어려움

**해결**: MonoBehaviour 컴포넌트로 직접 필드 노출

```csharp
public class CameraBlurController : MonoBehaviour 
{
    // ★ Timeline/Animator에서 직접 키프레임 설정 가능
    [SerializeField] public bool enabledBlur = false;
    [SerializeField, Range(0f,1f)] public float intensity = 0f;
    [SerializeField, Min(0f)] public float clamp = 8f;
    [SerializeField] public float angleDeg = 0f;                    // Linear 전용
    [SerializeField] public Vector2 radialCenter01 = new(0.5f,0.5f); // Radial 전용
    
    [SerializeField, Range(1,4)] public int downsample = 1; // 성능 제어
    [SerializeField, Range(1,4)] public int iterations = 1; // 품질 제어
    
    public BlurType type = BlurType.Linear;
    public BlurMethod method = BlurMethod.Gaussian;
}
```

[세부 코드 보기 - CameraBlurController](https://github.com/Hunobas/Song-Of-Jupitor/blob/10a1e7beee04279e75c236bbac08075c8c4097b4/Scripts/Renders/CameraBlur/CameraBlurController.cs#L24)

</details>

<details>
<summary><b>🔧 구현 과정 4: Downsample + Iterations로 성능 최적화</b></summary>

<br />

**문제**: 풀 해상도에서 13-tap 샘플링 → 비용 높음

**해결**: Downsample 후 블러 → 업샘플

```csharp
public bool Setup(ScriptableRenderer renderer, ref RenderingData rd) 
{
    var desc = rd.cameraData.cameraTargetDescriptor;
    var ds = Mathf.Max(1, st.Downsample); // 1/2/4배 축소
    desc.width  /= ds; 
    desc.height /= ds;
    
    // 축소된 해상도에서 블러 수행
    RenderingUtils.ReAllocateIfNeeded(ref _tmpA, desc, name: "_BlurTmpA");
    RenderingUtils.ReAllocateIfNeeded(ref _tmpB, desc, name: "_BlurTmpB");
}

public override void Execute(ScriptableRenderContext ctx, ref RenderingData rd) 
{
    // 다운샘플
    Blitter.BlitCameraTexture(cmd, src, _tmpA);
    
    // Iterations만큼 반복 블러 (품질 향상)
    for (int i=0; i<_iterations; i++) {
        Blitter.BlitCameraTexture(cmd, _tmpA, _tmpB, _mat, 0);
        (_tmpA, _tmpB) = (_tmpB, _tmpA); // ping-pong
    }
    
    // 결과를 원본 해상도로 합성
    Blitter.BlitCameraTexture(cmd, _tmpA, src);
}
```

[세부 코드 보기 - CameraBlurPass](https://github.com/Hunobas/Song-Of-Jupitor/blob/10a1e7beee04279e75c236bbac08075c8c4097b4/Scripts/Renders/CameraBlur/CameraBlurPass.cs#L77)

**성능 트레이드오프:**

| 설정 | 품질 | 성능 |
|------|------|------|
| Downsample 1 + Iterations 1 | 최고 | 낮음 |
| Downsample 2 + Iterations 2 | 높음 | **중간** ← 권장 |
| Downsample 4 + Iterations 1 | 낮음 | 최고 |

</details>

---
