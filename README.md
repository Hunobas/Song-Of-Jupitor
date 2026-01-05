# 🪐 목성의 노래

내러티브 1인칭 3D 퍼즐 게임
- ▶️ [**Play Demo**](https://www.youtube.com/watch?v=UEz0kvJCfAg)
- 📘 [**전체 포트폴리오**](https://github.com/Hunobas/Portfolio)

<br />

# 📑 목차

- 📝 프로젝트 정보
  - [제작기간](#1-제작기간)
  - [참여 인원](#2-참여-인원)
  - [역할 분담](#3-역할-분담)
- 📌 주요 작업 내용
  - [1️⃣ PC 렌더링 파이프라인 최적화](#1️⃣-pc-렌더링-파이프라인-최적화)
  - [2️⃣ FSM 기반 플레이 모드 아키텍처 설계](#2️⃣-fsm-기반-플레이-모드-아키텍처-설계)
  - [3️⃣ ASCII 이미지 UGUI 렌더러 플러그인 구현](#3️⃣-ascii-이미지-ugui-렌더러-플러그인-구현)
  - [4️⃣ 패널 드래그 앤 드랍 시스템 확장](#4️⃣-패널-드래그-앤-드랍-시스템-확장)
  - [5️⃣ 유니티 이벤트그래프 → 언리얼 블루프린트처럼 확장](#5️⃣-유니티-이벤트그래프-확장)
  - [6️⃣ 모션벡터 없는 카메라 모션블러 셰이더 구현](#6️⃣-모션벡터-없는-카메라-모션블러-셰이더-구현)

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

### [1️⃣ PC 렌더링 파이프라인 최적화](#-목차)

#### 🚨 문제 상황

<img width="1370" height="814" alt="image" src="https://github.com/user-attachments/assets/b1cb833c-c6ae-4603-bdc4-5901bd7b340f" />

**400만 버텍스 + 300개 머터리얼 → 30~60 FPS로 불안정**

목성의 노래의 우주 정거장 씬은 3060ti 환경에서도 에디터 씬 전환에 2초 이상, 인게임 FPS도 들쭉날쭉했습니다.

- 배칭 수 **2,600개** 이상
- CPU Usage만 한 프레임에 **10ms** 이상

<img width="482" height="360" alt="image" src="https://github.com/user-attachments/assets/6e7cd80a-cb53-408a-adff-cc3937bbbceb" />
<br /> *↑ 최적화 전: 배칭 수 2,600개, CPU 프레임당 10ms 이상*

#### 🎯 핵심 구현 포인트

**MeshBaker로 텍스처 아틀라스 + 콤바인 메쉬 → 오클루전 컬링으로 GPU 부하 감소**

1. **MeshBaker 텍스처 아틀라스 + 콤바인 메쉬**
```plaintext
Before: 300개 머터리얼 × 수천 개 메쉬 → SRP Batcher로도 Draw Call 수천 번
After:  방 단위 1개 머터리얼 × 1개 메쉬 → Draw Call 1개
```

- `MB3_TextureBaker`로 머터리얼들을 텍스처 아틀라스로 통합
- `MB3_MeshBakerGrouper`로 방 단위 클러스터링 후 콤바인 메쉬 생성

2. **오클루전 컬링 설정**
```plaintext
Smallest Occluder: 1m
Smallest Hole: 0.5m
Backface Threshold: 15
```

- 실내 씬 특성상 벽/문이 자연스러운 Occluder 역할
- GPU가 보이지 않는 오브젝트를 렌더링하지 않음

<br />

[📂 MeshBaker 에디터 확장 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/main/Plugins/MeshBaker/Editor/MB3_MeshBakerGrouperEditor.cs)
<br /> [📜 개발일지 전문](https://velog.io/@po127992/목성의-노래-MeshBaker-최적화-삽질기-텍스처-아틀라스만-vs-콤바인-메쉬까지)

#### 📊 성과

<img width="1914" height="487" alt="image" src="https://github.com/user-attachments/assets/ae54d0cd-f24b-4357-8bf0-73c0634bd549" />
<br /> *↑ 최적화 전: CPU/GPU 총합 1프레임 평균 15ms 소요, 스파이크 다수*

<img width="1887" height="464" alt="image" src="https://github.com/user-attachments/assets/726fb8cd-71cc-4869-b42b-cff7c3be2427" />
<br /> *↑ 최적화 후: CPU/GPU 모두 안정화*

| 단계 | Batches | CPU | GPU | FPS |
|------|---------|-----|-----|-----|
| 기준선 | 2,650 | 12.5ms | 8.24ms | 30~60 |
| MeshBaker 적용 | 750 | 5ms | 8ms | 80~100 |
| 오클루전 컬링 적용 | 601 | 8ms | 6.26ms | **120+** |

*기준 - CPU 라이젠5 7600X | GPU NVDIA 3060Ti*

<br />

<details>
<summary><b>🔧 고찰 과정 1: 콤바인 메쉬 적용 후 **실시간 그림자로 Verts 수 폭증** (2.5M → 11.9M)</b></summary>

<br />

<img width="1271" height="481" alt="image" src="https://github.com/user-attachments/assets/869a229f-477d-4ff6-a61f-69fa2bf32e56" />
<br /> *↑ 실시간 그림자 ON 시 Verts 폭증 현상*

**가설:** 콤바인 메쉬 없이 텍스처 아틀라스만 적용하면 그림자 문제가 해결될까?

MeshBaker 에디터 스크립트를 확장하여 "Material Only" 방식을 직접 구현했습니다:
- 원본 메쉬의 UV를 Atlas 좌표로 재매핑
- 다중 머터리얼(서브메쉬) 케이스 처리
- 메쉬 에셋 자동 저장으로 프리팹 호환

**실험 결과:**

| 항목 | 콤바인 메쉬 | 텍스처 아틀라스 Only | 변화 |
|------|------------|-------------------|------|
| FPS | 75.3 | 53.6 | 📉 -29% |
| Batches | 5,808 | 10,089 | 📉 +74% |
| Verts | 64.3M | 18.3M | 📈 -72% |

Verts는 72% 감소했지만 **Draw Call 74% 증가로 전체 성능 하락**.
이 씬에서는 콤바인 메쉬 클러스터링 + 오클루전 컬링 및 LOD + 라이트맵 베이킹이 정답이었습니다.

</details>

<br />
<br />

---

## 📌 주요 작업 내용

### [2️⃣ FSM 기반 플레이 모드 아키텍처 설계](#-목차)

#### 🚨 문제 상황

**패널 여는 카메라 블렌딩 중 → 시네마 재생 → 시네마 종료 → 아무 것도 할 수 없음**

플레이어가 인게임 패널을 여는 도중 컷씬이 재생되면, 컷씬이 끝나도 **조작 불가 상태**가 되는 치명적 버그가 발생했습니다.

- 5가지 플레이 모드(Normal/Panel/Cinema/Dialog/Pause)가 **상호배타적**이어야 하는데 **중첩 발생**
- 각 모드 전환 시 정리해야 할 상태가 **여러 파일에 분산**
- 디버깅 시 어디서 상태가 꼬였는지 추적하는 데에 **평균 1시간 소요**

![GameState 버그 영상](https://github.com/user-attachments/assets/fa973d2f-df58-483d-ae3b-05d5104e9bc6)
<br /> *↑ 패널 모드 진입 중 시네마 모드가 끼어들면 발생하는 조작 불가 문제*

#### 🎯 핵심 구현 포인트

<img width="1020" height="458" alt="그림1" src="https://github.com/user-attachments/assets/d0b930d5-8c1a-4120-8fbd-e9b4ee1dfc44" />

**중앙 집중식 FSM으로 모든 플레이 모드를 단일 책임 관리**

1. **재활용될 플레이 모드 미리 정의**

```csharp
// GameState.cs
protected override void Awake()
{
    base.Awake();
    _pauseMode  ??= new PauseMode(this);
    _cinemaMode ??= new CinemaMode(this);
    _dialogMode ??= new DialogMode(this);
    _panelMode  ??= new PanelMode(this);
    _normalMode ??= new NormalMode();

    _activeMode = _normalMode;
}
```

2. **상태 중첩 방지**
   
```csharp
  public void ChangePlayMode(IPlayMode next)
  {
      if (next == null || ReferenceEquals(_activeMode, next))
          return;
      
      // 시네마 모드는 특별하게, 일시정지 모드 이외 다른 모드의 방해를 받지 않아야 하므로 자신이 시네마 모드를 끝내기 전까지 모드 변경 무시. 
      if (IsPlayingCinema && !ReferenceEquals(next, PauseMode))
          return;
      
      // 패널 모드 한번 더 체크 (디버깅 시 패널 모드 자동 종료)
      if (IsOperatingPanel && PanelMode.Controller != null)
      {
          PanelMode.Controller.EndPanelForcely();
      }

      var prev = _activeMode;
      prev?.OnExit(next);
      _activeMode = next;
      _activeMode.OnEnter(prev);

      InputManager.Instance?.UpdateCursorLock();
  }
```

3. **자동 정리 훅**

```csharp
// 패널 모드의 경우 PanelMode.OnExit에서 UI 상태 정리
public void OnExit(IPlayMode next)
{
    if (Controller == null)
        return;
    
    Controller = null;
    OnExitEvent?.Invoke();
    _owner.ScreenDirector.FadeInIcons();
    _owner.StaminaBarUI.CanRun = true;
    _owner.ScreenDirector.ShowCrosshair();
    _owner.InputManager.BindToWorld();
}
```

[📂 전체 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L15)

#### 📊 성과

| 개선 항목 | Before | After | 효과 |
|---------|--------|-------|-----|
| 상태 충돌 버그 | 주 2-3건 발생 | **0건** | 100% 해결 |
| 디버깅 소요 시간 | 평균 60분 | 평균 30분 | **50% 감소** |
| 신규 모드 추가 시간 | - | 20분 이내 | `IPlayMode`만 구현 |

<br />

<details>
<summary><b>🔧 해결 과정 1: 일시정지 해제 시 이전 플레이 모드를 기억하지 못함</b></summary>

<br />

**`PauseMode`가 `prevMode` 저장 후 자체 `Resume()` 메서드로 복구**
<br /> [세부 코드 보기](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/PauseMode.cs#L34)

</details>

<details>
<summary><b>🔧 해결 과정 2: 시네마 모드 중 다이얼로그 모드로 전환되면 시네마 중단</b></summary>

<br />

- **`ChangePlayMode`에서 시네마 모드 진입 시 다른 모드 요청 무시**
- 시네마 모드는 `TimelineController`에서 **자체적으로 종료**

<br /> [세부 코드 보기 - `GameState.ChangePlayMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/GameState.cs#L66)
<br /> [세부 코드 보기 - `CinemaMode.ExitCinemaMode`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/CinemaMode.cs#L27)
<br /> [세부 코드 보기 - `TimelineController.OnTimelineStopped`](https://github.com/Hunobas/Song-Of-Jupitor/blob/7386ab978fc3115a13a700758c7a618567bc168a/Scripts/System/TimelineController.cs#L102)

</details>

<br />
<br />

---

### [3️⃣ ASCII 이미지 UGUI 렌더러 플러그인 구현](#-목차)

#### 🚨 문제 상황

**아트 팀이 아스키 아트를 편집할 때마다 프로그래머에게 요청**

게임 내 터미널 UI에 아스키 아트가 필요했지만, 기존 방식은 아트 팀의 작업 흐름을 막았습니다.

- 포토샵에서 ASCII 변환 → 텍스트 파일 → Unity에 수동 복붙
- 색상/밝기 조정할 때마다 **전체 과정 반복**
- 애니메이션 프레임마다 **수작업 필요**
- 아트 팀원: "이거 좀 더 밝게 해주세요" → 프로그래머 호출

![image (2)](https://github.com/user-attachments/assets/389ec02c-9fdf-4cdd-aa57-0c9e79bbfa4b)
<br /> *↑ 목표: Unity 에디터에서 실시간 미리보기 가능한 아스키 렌더러*

#### 🎯 핵심 구현 포인트

[나이브했던 초기 구현: CPU에서 모든 픽셀 읽은 후 아스키 문자 변환](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L312)

**🐛 문제점:**
   - 160×90 그리드 × 4×4 슈퍼샘플 = **230,400회** 픽셀 접근
   - 각 픽셀마다 `<color>` 태그 생성 → **문자열 길이 76,800자**
   - `Update()` 호출 시 **CPU 점유 27.6ms, 프레임 비중 70.4%**

**비동기 Readback + 색이 바뀌는 구간에만 태그 + 색상 해상도 낮춤**

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

<br />

<details>
<summary><b>🔧 해결 과정 1: GPU → CPU 전송이 끝날 때까지 메인 스레드 블록킹</b></summary>

<br /> [문제 코드 보기 - `ReadPixels` 이후 즉시 `Apply`](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L235)

**① GPU 다운샘플 먼저 수행**

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

**② AsyncGPUReadback으로 비동기 전송**

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

</details>

<details>
<summary><b>🔧 해결 과정 2: 모든 픽셀마다 생성되는 컬러 태그로 문자열 오버헤드 증가</b></summary>

<br /> [문제 코드 보기 - 픽셀 당 Append(`<color=#RRGGBB>문자</color>`) 수행](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L421)

**색이 바뀌는 구간에만 태그 열고/닫기**

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

</details>

<details>
<summary><b>🔧 해결 과정 3: "잦은 색상 변경 구간 == 잦은 컬러 태그 추가"로 문자열 오버헤드 증가</b></summary>

<br /> [문제 코드 보기 - 지나치게 엄밀한 색상 계산 과정](https://github.com/Hunobas/Song-Of-Jupitor/blob/687a96614dea727599ce651bbc00cf15cac9f099/Scripts/Renders/ASCIIImage/AsciiImageUGUI.cs#L400)

**각 채널을 4bit로 양자화 → 4096가지 색만 사용**

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

</details>

<br />
<br />

---

### [4️⃣ 패널 드래그 앤 드랍 시스템 확장](#-목차)

#### 🚨 문제 상황

**기존 패널 시스템에 클릭/호버링 기능만 존재**

시그널 퍼즐에 **드래그 & 드랍** 기능이 요구되었습니다.

- 기존 패널 시스템에서는 크로스헤어(월드 조작용)와 패널 UI(캔버스 조작용)가 **동시에 반응**
- 드래그 중 캔버스 밖으로 벗어나면 **입력 유실**

![시그널 퍼즐](https://github.com/user-attachments/assets/a160c3f4-1c15-4820-ba24-a88395dc58cf)
<br /> *↑ 목표: 드래그 & 드랍 기능이 필요한 시그널 퍼즐*

#### 🎯 핵심 구현 포인트

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

1. **기본 클릭만 구현**
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

2. **드래그 기능 추가**
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

<br /> 3. **Unity EventSystem과 동일한 수준으로 엣지 케이스 처리**

[📂 초기 버전 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L307)
<br /> [📂 최종 버전 코드](https://github.com/Hunobas/Song-Of-Jupitor/blob/826a59ee72650fc6df054c2b0edb57e9080fef91/Scripts/System/PanelBase.cs#L219)

#### 📊 성과

1. **클릭과 드래그**가 명확히 구분됨
2. 연속 클릭 시 이전 상태가 간섭하지 않음
3. 마우스 양쪽 버튼을 동시에 사용해도 이벤트 충돌 없음
4. **Slider 본체**가 반응
5. 예상치 못한 입력 유실에도 상태가 자동 복구됨

<br />

<details>
<summary><b>🔧 해결 과정 1: 1픽셀만 움직여도 드래그로 인식되어 클릭이 불가능</b></summary>

<br /> [문제 코드 보기 - `PointerDragTick`](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L330)

**드래그 임계값 적용**

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
<summary><b>🔧 해결 과정 2: Down 상태인데 다시 Down이 들어오면 이전 입력이 정리되지 않음</b></summary>

<br /> [문제 코드 보기 - PointerDown](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L307)

**중복 입력 방지**

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
<summary><b>🔧 해결 과정 3: 왼쪽 버튼으로 Down → 오른쪽 버튼으로 Up 시 잘못된 이벤트 발생</b></summary>

<br /> [문제 코드 보기 - PointerUp](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L340)

**버튼 짝 검증**

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
<summary><b>🔧 해결 과정 4: 슬라이드 클릭 이벤트를 본체가 아닌 핸들이 받음</b></summary>
  
<br /> [문제 코드 보기 - PointerDown](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/System/PanelBase.cs#L314)

**핸들러 UI 버블링 기능 구현**

```csharp
// ❌ 기존: 레이캐스트 히트된 오브젝트를 그대로 사용
var results = RaycastAtCursor();
if (results.Count == 0) return;
_pressedObject = results.FirstOrDefault().gameObject;

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

<br />
<br />

---

### [5️⃣ 유니티 이벤트그래프 → 언리얼 블루프린트처럼 확장](#-목차)

#### 🚨 문제 상황

**기존 이벤트 그래프는 2개 이상 파라미터 메서드 호출이 불가능**

Unity 기본 `UnityEvent`에서 최대 1개 파라미터만 지원하는 `Invoke()` 노드의 한계가 명확했으며, 일련의 노드 실행 흐름을 제어할 수도 없었습니다.

- 컷씬 연출에 필요한 복잡한 메서드 호출 불가 (예: `SetCameraShake(amplitude, frequency, duration)`)
- 파라미터마다 노드를 쪼개면 **실행 순서 보장 안 됨**
- **Unreal 블루프린트**처럼 한 노드의 실행이 정확히 끝나는 순간 다음 노드로 실행이 불가능

<img width="781" height="366" alt="image" src="https://github.com/user-attachments/assets/69ea3e47-2097-444d-8cc9-b94cc31b73b1" />
<br /> *↑ 최대 1개 파라미터 메서드만 호출할 수 있는 기존 이벤트그래프 `Invoke` 노드*

#### 🎯 핵심 구현 포인트

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

<br /> **2개 이상의 파라미터를 받을 수 있고 실행 흐름을 커스텀할 수 있는 이벤트그래프 커스텀 노드**

<br />

<details>
<summary><b>🔧 구현 과정 1: 백그라운드 실행 vs 대기 실행</b></summary>

<br /> **`BakedActionNode`에서 `WaitPolicy` 분기 처리**

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
- **ForceNoWait**: [카메라 셰이크](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/EventGraph/Customs/Nodes/Camera/Node_Start6DShake.cs) (2.5초 페이드아웃 중에도 다음 노드 진행)
- **ForceWait**: [컷씬 이미지](https://github.com/Hunobas/Song-Of-Jupitor/blob/a2e7f56c02f078d6600144e669e1234659e749ad/Scripts/EventGraph/Customs/Nodes/Display/Node_CutsceneImage.cs) (Duration 끝날 때까지 대기)

</details>

<details>
<summary><b>🔧 구현 과정 2: 런타임 Abort 시스템</b></summary>

<br /> 노드 실행 중 필수 참조가 `null`이면 무한 대기하므로 **`EventGraphRuntime` 스택으로 현재 실행 컨텍스트 추적**

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

</details>

<details>
<summary><b>🔧 구현 과정 3: 에디터 UI 자동 생성</b></summary>

<br /> **`ActionNodeView`로 공통 UI 자동 생성 + Reflection으로 타이틀 변경**

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

</details>

<br />
<br />

---

### [6️⃣ 모션벡터 없는 카메라 모션블러 셰이더 구현](#-목차)

#### 🚨 문제 상황

**씬에 BaseLayer 카메라가 2개 이상 있으면 모션 블러 무시됨**

Unity URP에서 BaseLayer 카메라가 여러 개 있는 씬에서는 모션 벡터 렌더링이 충돌하여 모션 블러가 작동하지 않았습니다.

- Unity URP의 모션 블러는 **모션 벡터 텍스처**에 의존
- BaseLayer 카메라 2개 → 모션 벡터 렌더 타겟 충돌
- Volume Override의 Motion Blur가 **먼저 렌더링된 카메라**에만 적용
- 두 번째 카메라는 모션 벡터 없이 렌더링 → **모션 블러 효과 사라짐**

<img width="1839" height="916" alt="image" src="https://github.com/user-attachments/assets/52ed330a-0238-4c12-be0d-0bc4d6086860" />
<br /> *↑ Main Camera와 Player Camera가 모두 Base Layer → Motion Blur Volume 무시됨*

#### 🎯 핵심 구현 포인트

**모션 벡터가 필요 없는 정적 방향 블러 Scriptable Render Feature 구현**

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

![Jupitor-Prologue-WindowsMacLinux-Unity2022 3 62f2_DX11_2025-12-1307-55-40-ezgif com-video-to-gif-converter](https://github.com/user-attachments/assets/359d2e29-2e54-4c56-a11b-4fa64f54b5e4)
<br /> *↑ Timeline에서 편집 가능한 정적 모션 블러*

1. 모션 벡터 텍스처 의존성 제거된 연출
2. **각도/중심점 자유롭게 설정** 가능한 블러 방향
3. Timeline 애니메이션 지원
4. 카메라가 정지 상태에서도 **속도감 연출 가능**

<br />

<details>
<summary><b>🔧 구현 과정 1: Scriptable Render Feature 기반 구조</b></summary>

<br /> **Custom Render Pass로 완전히 독립적인 블러 구현**

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
<summary><b>🔧 구현 과정 3: 애니메이션 친화적 컨트롤러</b></summary>

<br /> Volume Override는 타임라인에서 키프레임 애니메이팅이 어려우므로 **MonoBehaviour 컴포넌트로 직접 필드 노출**

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
<summary><b>🔧 구현 과정 4: 다운샘플로 성능 최적화</b></summary>

<br />

**다운샘플 후 블러 → 업샘플**

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

</details>

---
