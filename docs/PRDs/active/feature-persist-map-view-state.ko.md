# Map 탭 — 마지막 본 맵/줌/팬 복원 (페이지 뷰 상태 유지, Phase 1) PRD

## Overview

- **Status**: In Progress (Phase 1 구현 완료; 단위 + e2e 그린; PR 대기)
- **Created**: 2026-07-23
- **Updated**: 2026-07-24
- **Owner**: josephjang
- **Translations**: 영문 원본 `feature-persist-map-view-state.md` (1:1 동기화 유지; 내용 충돌 시 영문이 기준)

## Problem Statement

### 사용자가 겪는 증상

Map 탭에서 아무 맵(예: Customs)을 보다가 잠시 다른 탭(예: Quests)으로 갔다가 돌아오면
Map 탭이 **Woods**, 줌 100%, 중앙 정렬, 기본 층으로 리셋되고 이동 궤적도 지워진다.
앱을 재시작할 때마다 같은 리셋이 발생한다. 사용자는 탭을 전환할 때마다 맵을 다시
선택하고 화면을 다시 맞춰야 한다.

### 근본 원인 (코드 확인 완료)

`MainWindow`는 페이지 인스턴스를 캐시하고 `ContentControl`에 교체해 넣는다
(`MainWindow.xaml.cs`의 `Tab_Checked`). 즉 탭 전환 시 `MapPage`는 재생성되지 **않는다**
— 하지만 WPF는 콘텐츠 교체마다 `Loaded`/`Unloaded`를 발화하고, MapPage의 핸들러가
캐시된 상태를 스스로 파괴한다:

1. `MapTrackerPage_Loaded`(`TarkovHelper/Pages/Map/MapPage.xaml.cs`)가 **탭 진입마다
   전체 초기화를 재실행**한다: 궤적을 지우고, 모든 마커 매니저를 다시 만들고,
   `PopulateMapComboBox()`를 호출한다.
2. `PopulateMapComboBox()`는 무조건 `SelectedIndex = 0`으로 설정한다 —
   `Assets/DB/Data/map_configs.json`의 첫 번째 키, 즉 **Woods**다. 코드에 "Woods"
   리터럴 기본값이 있는 게 아니라 딕셔너리 0번 위치일 뿐이다.
3. 저장된 `SettingsService.MapLastSelectedMap`을 다시 적용해야 할 `RestoreMapState()`는
   2번 **이후에** 실행되며 `string.IsNullOrEmpty(_currentMapKey)` 가드로 보호되는데,
   그 시점에는 항상 false다. 복원 경로는 사실상 **죽은 코드**다. 설령 실행되더라도
   저장된 줌/팬을 복원하는 대신 줌을 100%로 강제하고 재중앙 정렬한다.
4. 저장 측은 정상 동작한다: `MapTrackerPage_Unloaded` → `SaveMapState()`가
   `MapLastSelectedMap`/`MapLastZoomLevel`/`MapLastTranslateX/Y`를 저장한다. 그래서
   리셋으로 Woods가 표시된 뒤 다음 탭 이탈 때 **"Woods"가 저장되어 사용자의 실제
   마지막 맵을 덮어쓴다**.

같은 구조에 잠복 버그 두 개가 얹혀 있다:

- `Unloaded`가 (생성자에서 한 번 구독한) `MapMarkerDbService.DataRefreshed`와
  `QuestObjectiveDbService.DataRefreshed` 구독을 해제하는데 `Loaded`는 재구독하지
  않는다 — 첫 탭 이탈 이후 앱이 살아있는 동안 DB 갱신이 맵 마커에 반영되지 않는다.
- `StartRaidEventMonitoring()`이 탭 진입마다 `EftRaidEventService.StartMonitoring(...)`을
  호출해, `MainWindow.AutoStartLogMonitoring`이 이미 시작해 둔 앱 전역
  `FileSystemWatcher`들을 매번 부수고 다시 만든다.

### 일반화된 문제

이것은 하나의 계열에 속하는 사례다: **캐시가 보존해 준 상태를 `Loaded` 핸들러가
스스로 파괴하는 캐시된 페이지들.** 5개 메인 페이지 조사 결과:

| 페이지 | 탭 전환 후에도 유지되길 기대하는 상태 | 현재 |
|--------|--------------------------------------|------|
| MapPage | 선택 맵, 줌, 팬, 층, 궤적, 드로어/패널 상태 | **손실** — 저장은 되나 복원 안 됨; 저장값도 덮어써짐 |
| QuestListPage | 검색어, 필터, 선택 퀘스트 | 유지 (`_isDataLoaded` 가드 — 따라야 할 모범) |
| ItemsPage | 검색, 필터, 정렬, 선택 아이템 | 유지 (가드 + 명시적 선택 복원) |
| HideoutPage | 검색어; 선택 모듈 | 검색어는 유지; **선택 손실** (ItemsSource 재구성 시 재선택 없음) |
| CollectorPage | 검색, 토글, 정렬; 선택 아이템 | 필터는 유지; 재구성 시 **목록 하이라이트 손실** |
| (전역) | 마지막 선택 메인 탭 | 미저장 — 매 실행마다 Quests로 시작 |

MapPage는 상태를 *저장하고도* 복원에 실패하는 유일한 페이지다. Hideout/Collector는
목록 선택을 잃고, 마지막 탭 유지는 존재하지 않는다. Quest/Items는 이 코드베이스에
이미 있는 동작하는 패턴을 보여준다.

## 개선 원칙 (일반화)

이 PRD가 확립하는 규칙이다. Phase 1은 MapPage에, 이후 Phase는 나머지에 적용한다.
앞으로 탭 캐시에 추가되는 페이지도 이 규칙을 따라야 한다.

1. **캐시된 페이지의 `Loaded` 핸들러는 멱등이어야 한다.** 1회성 초기화는
   `_isInitialized` 가드 뒤에서 한 번만 실행하고, 탭 진입마다 할 일은 이벤트 재구독,
   감시 시작/중지, 언로드 중 놓친 신호의 조정(reconcile)으로 한정한다. 탭 재진입은
   아무것도 복원하지 않는다 — 이미 있는 것을 파괴하지 않을 뿐이다.
2. **복원이 기본값보다 먼저다.** 하드코딩 기본값(콤보 index 0, `isDefault` 층)은
   *복원 결정의 fallback*으로만 존재해야 하며, 복원보다 먼저 실행되는 단계여선 안 된다.
3. **저장 상태는 라이브 게임 신호에 양보한다.** 우선순위: 진행 중 레이드의 맵 >
   저장된 맵 > 설정 기본값.
4. **결정 로직은 순수하고 단위 테스트된 코어에 둔다** (UI/DB/서비스 의존성 없음) —
   window-bounds 기능의 `WindowBoundsPersistence` 선례.
5. **영속화를 `Unloaded`에만 의존하지 않는다.** WPF는 앱 종료 시 `Unloaded`를 보장하지
   않는다 — 변경 시점에 저장하거나, 창 `Closing` 훅을 병행한다.

## Goals (Phase 1 — Map 탭)

- [x] Goal 1: Map 탭으로 돌아오면 떠날 때와 **같은 맵, 줌, 팬**이 보인다.
- [x] Goal 2: 앱을 재시작해도 **마지막 본 맵, 줌, 팬**이 복원된다 (저장 인프라는 이미
      존재; 복원이 빠진 반쪽이다).
- [x] Goal 3: **레이드 감지가 우선한다**: 레이드가 진행 중이면(`EftRaidEventService`로
      감지) 감지된 맵이 저장된 맵보다 우선한다 — 최초 로드 시에도, 다른 탭에 있는 동안
      레이드가 시작된 경우에도. *(단위 테스트 완료; 실제 게임으로의 수동 확인 보류)*
- [x] Goal 4: **첫 실행**(저장값 없음)은 현재 동작을 유지한다: 설정된 첫 맵, 줌 100%,
      중앙 정렬.
- [x] Goal 5: 멱등 `Loaded` 수정의 구조적 부수 효과: 층, 궤적, 드로어 상태, 마커 매니저
      정체성이 탭 전환에서 생존; `DataRefreshed` 죽은 구독 버그와 탭 진입마다의
      `FileSystemWatcher` 재생성이 수정된다.

## Non-Goals (Scope Out)

- 재시작 간 층 유지 — 새 맵 로드 시에는 자동 층 감지 + 맵별 기본 층이 올바른 동작이다.
- 재시작 간 궤적 유지 — 궤적은 레이드 단위 데이터; 레이드 이벤트에 의한 초기화는 유지.
- 탭 재진입 시 오버레이 미니맵 재표시 — 현재의 이탈 시 숨김 동작 유지 (별도 UX 결정).
- 레이드 자동 전환 외의 맵별 줌/팬 기억 — 지금처럼 전역 마지막 뷰 하나만 저장.
- 뷰어 기하 기반 팬 클램프 (Phase 1에서는 저장된 팬을 유한성만 검증; 맵/뷰어 기하에
  대한 클램프는 향후 강화 항목).

**이후 Phase (이 PRD의 구현 범위 밖, 명시적 나열):**

- **Phase 2**: `ItemsSource` 재구성 시 HideoutPage/CollectorPage 선택 복원 (메모리 내
  복원만; ItemsPage의 선택 저장 후 재선택 패턴이 템플릿).
- **Phase 3**: 마지막 선택 메인 탭 저장 (`app.lastSelectedTab`); 기동 비용 상호작용에
  주의 (Map 탭 복원은 실행 시점에 MapPage를 생성하게 됨).

## Requirements / Acceptance Criteria

- [x] R1 (탭 왕복): 맵 M 선택, 줌/팬 조작 → 탭 전환 → 복귀: 같은 줌/팬의 맵 M; 층,
      궤적, 드로어 상태 그대로. *(맵 선택은 e2e 검증; 줌/팬/층/궤적은 재진입 시
      재초기화가 없어져 구조적으로 생존)*
- [x] R2 (재시작 왕복): 맵 M 선택, 줌/팬 조작 → 앱 종료 → 재실행 → Map 탭: 저장된
      줌/팬으로 맵 M 복원. *(e2e: 시드된 맵+뷰가 복원되고 그대로 재저장됨)*
- [x] R3 (레이드 우선): 맵 R에서 레이드 진행 중이면 Map 탭 최초 로드는 (저장된 맵이
      아닌) R을 표시; 다른 탭에 있는 동안 시작된 레이드는 복귀 시 맵을 전환한다.
      *(`DecideInitialMap`/`GetActiveRaidMapKey` 단위 테스트; e2e로는 구동 불가)*
- [x] R4 (첫 실행): 저장값 없음 → 설정된 첫 맵, 줌 100%, 중앙 정렬. *(e2e)*
- [x] R5 (내성): `map_configs.json`에 더 이상 없는 저장 맵 키나 비유한(non-finite)
      줌/팬 값은 기본값으로 fallback — 크래시나 빈 맵은 절대 없음. *(fallback 규칙
      단위 테스트)*
- [x] R6 (덮어쓰기 금지): 탭 전환만으로는 저장된 맵이 리셋값으로 덮어써지지 않는다.
      *(e2e: 수정 전 앱에서는 이 테스트들이 실패 — Woods 표시, null/리셋값 저장)*

## Technical Decisions

| 결정 | 근거 | 날짜 |
|------|------|------|
| `PopulateMapComboBox`에 저장 키만 가르치거나 `RestoreMapState` 순서만 바꾸는 대신, `Loaded`를 멱등으로 수정 (`_isInitialized` 가드: 1회성 초기화 vs 탭 진입마다 재장전) | 맵+줌+팬+층+궤적+드로어+마커 매니저 정체성을 한 번에 해결; 탭 진입마다 SVG 재파싱과 목표/탈출구/마커 재로드 비용 제거; QuestListPage/ItemsPage에서 이미 검증된 `_isDataLoaded` 패턴과 일치; `DataRefreshed` 죽은 구독 버그를 구조적으로 해결 | 2026-07-23 |
| 순수 정적 코어 `TarkovHelper/Services/Map/MapViewStatePersistence.cs` 신설: `DecideInitialMap(savedKey, availableKeys, activeRaidKey)` → `(MapKey, Source)` (우선순위 레이드 > 저장 > 첫 맵); `ValidateView(zoom, tx, ty, minZoom, maxZoom)` → 검증된 뷰 또는 null; `GetActiveRaidMapKey(raid)` | `WindowBoundsPersistence` 선례를 따름 (순수 코어 + 얇은 UI 배선); 대소문자 무시 매칭으로 canonical 설정 키 반환; 무효 키 fallback 내장; 단위 테스트 용이 | 2026-07-23 |
| 맵 키는 `SelectionChanged`에서 즉시 저장; 줌/팬은 `Unloaded` **및** `MainWindow.OnWindowClosing`에서 저장 (`PersistViewState()` 백스톱) | 맵 키가 프로세스 강제 종료에도 생존; WPF는 종료 시 `Unloaded` 미보장; `MapSettings` setter에 변경 감지가 있어 중복 저장은 저렴한 no-op | 2026-07-23 |
| 레이드 "진행 중" 판정: `EftRaidEventService.CurrentRaid != null && State != Ended && MapKey` 비어있지 않음; 탭 재진입 시 `ReconcileActiveRaid()` 실행 (진행 중 레이드의 맵이 이미 일치하면 no-op — 궤적 보존) | `EftRaidEventService` 변경 불필요; 복원이 레이드 자동 감지와 싸우지 않음; 레이드 중 재진입 시 궤적 유지 | 2026-07-23 |
| `RestoreMapState()` 삭제; 그 의도는 결정 코어 + `SelectionChanged`에서 소비되는 `_pendingViewRestore`로 이전 (`LoadMapImage(key, centerView: false)` → `SetZoom(saved)` → translate 마지막) | 이 메서드는 현재 죽은 코드; `centerView: false`가 지연된 `CenterMapInView`의 복원된 팬 덮어쓰기를 방지 | 2026-07-23 |
| `StartRaidEventMonitoring`에 `EftRaidEventService.IsMonitoring` 가드 | 탭 진입마다 앱 전역 `FileSystemWatcher`를 부수고 재생성하는 것을 중단 | 2026-07-23 |

## Implementation Plan

### Phase 1: Map 뷰 상태 복원 (이 PRD)

- [x] Task 1.1: 순수 결정 코어 추가
  - Files: `TarkovHelper/Services/Map/MapViewStatePersistence.cs` (신규)
- [x] Task 1.2: `MapTrackerPage_Loaded` 멱등화 — 1회성 초기화(설정, 마커 매니저, 맵 결정
      + `PopulateMapComboBox(selectKey)`, 데이터 로드, 드로어, 오버레이)와 탭 진입마다의
      재장전(진행도/키보드/레이드 이벤트 재구독, `StartAutoTracking`,
      `ReconcileActiveRaid`)을 분리; `PopulateMapComboBox`를 파라미터화해 index 0은
      fallback으로만; `CmbMapSelect_SelectionChanged`에서 `_pendingViewRestore` 소비 및
      맵 키 저장; `RestoreMapState()` 삭제; `Unloaded`에서 `DataRefreshed -=` 2줄 제거;
      `StartMonitoring`에 `IsMonitoring` 가드; `HandleRaidStarted`에서
      `SwitchToRaidMap(...)` 추출 및 `ReconcileActiveRaid()` 추가
  - Files: `TarkovHelper/Pages/Map/MapPage.xaml.cs`
- [x] Task 1.3: 종료 백스톱 — 창 닫힘 시 맵 뷰 상태 저장
  - Files: `TarkovHelper/MainWindow.xaml.cs` (`OnWindowClosing`: `_mapTrackerPage?.PersistViewState();`)

### Phase 1 테스트

- [x] Task 1.4: 결정 코어 단위 테스트 (~10개): 저장 키 happy path; 대소문자 불일치 시
      canonical 키 반환; 설정에 없는 저장 키 → 첫 맵 fallback; 첫 실행 → 첫 맵; 진행 중
      레이드가 저장 키보다 우선; 알 수 없는 레이드 키는 무시; 빈 키 목록 → null;
      `GetActiveRaidMapKey` 레이드 상태별 (null/Ended/Matching/InRaid/빈 MapKey);
      `ValidateView` 왕복, 양끝 줌 클램프, NaN/Infinity 거부
  - Files: `TarkovHelper.Tests/MapViewStatePersistenceTests.cs` (신규)
- [x] Task 1.5: E2E 테스트 — `MainWindowBoundsE2ETests.cs`에서 공용 앱 드라이버
      (`App`/`Win32`/`E2EFact`)를 재사용 가능한 harness로 추출; UI Automation으로 탭
      클릭과 맵 콤보 읽기 (WPF는 `x:Name` — `TabMap`/`TabQuests`/`CmbMapSelect` — 을
      AutomationId로 노출). 케이스: 시드된 맵이 실행 시 복원되고 종료 시 **덮어써지지
      않음**; Map → Quests → Map 전환에서 맵 생존; 첫 실행은 Woods를 표시하고 저장.
      정직한 갭: 줌/팬 복원은 UIA로 검증 불가 (단위 테스트 + 수동 확인); 레이드
      우선순위는 단위 테스트만 (가짜 EFT 로그를 watcher에 흘리는 것은 CI에 너무 취약).
      DPI-unaware 테스트 호스트에서 UIA가 불안정하면 DB-only 검증으로 강등 (시드 → 탭
      왕복 → 종료 → 값 불변) — 이것만으로도 R6 덮어쓰기 증상은 고정된다.
  - Files: `TarkovHelper.Tests/MapStateE2ETests.cs` (신규), `TarkovHelper.Tests/MainWindowBoundsE2ETests.cs` (harness 추출)

## Progress Log

| Date | Update | By |
|------|--------|-----|
| 2026-07-23 | 근본 원인 분석으로부터 PRD 작성: 캐시된 MapPage의 `Loaded`가 탭 진입마다 전체 초기화를 재실행; `PopulateMapComboBox`가 `RestoreMapState`보다 먼저 index 0(Woods)을 강제하고, `IsNullOrEmpty(_currentMapKey)` 가드가 항상 실패 (죽은 코드); 이후 리셋값이 저장되어 실제 마지막 맵을 덮어씀. 5개 개선 원칙으로 일반화; 설계 확정 (멱등 `Loaded`, 순수 `MapViewStatePersistence` 코어, 레이드 > 저장 > 기본값 우선순위, 변경 시 저장 + `Closing` 백스톱). | josephjang |
| 2026-07-24 | Task 1.1–1.3대로 Phase 1 구현 (순수 코어; 탭 진입 재장전 + `ReconcileActiveRaid`를 갖춘 멱등 `Loaded`; `SwitchToRaidMap` 추출; `SelectionChanged`에서 변경 시 저장; `OnWindowClosing` 백스톱; `RestoreMapState` 삭제; `DataRefreshed` 구독 해제와 watcher 재생성 제거). Task 1.4–1.5 테스트: 단위 28개 + e2e 3개 (UIA 탭 구동 성공; 공용 harness를 bounds 테스트에서 추출). 수정 전 앱을 상대로 e2e 검증: 3개 모두 실패 — 특히 수정 전에는 Map 탭을 보다가 앱을 닫으면 **아무것도** 저장되지 않았음이 확인됨 (창 닫힘 시 `Unloaded` 미발화), 즉 `Closing` 백스톱은 이론이 아닌 실재하던 두 번째 유실을 고침. harness 보강: UI Automation을 로드하면 테스트 호스트가 실행 중 DPI-aware로 바뀌어 200% 디스플레이에서 bounds e2e의 좌표 전제가 깨짐 — 호스트 DPI 인지를 per-monitor-v2로 선고정하고 `GetWindowRect`가 물리 픽셀을 WPF 단위로 정규화하도록 수정. 전체 스위트 71 통과 / 1 건너뜀 (기존 스킵). `MainWindow.BtnClearAllData_Click`의 기존 CS1998도 수정. | josephjang |

## Completion Criteria

- [x] 모든 Goals와 Requirements (R1–R6) 충족 *(R3 실제 게임 수동 확인 보류)*
- [x] 빌드 그린 (`dotnet build`)
- [x] 단위 가드: `MapViewStatePersistenceTests`가 결정/검증 규칙을 보호 (28개)
- [x] E2E: `MapStateE2ETests`가 격리된 Config 디렉토리에서 실제 앱을 상대로 실행 시
      복원, 탭 전환 생존, 덮어쓰기 금지를 검증
      (빠른 실행에서는 `dotnet test --filter Category!=E2E`로 제외);
      수정 전 앱에서 실패하는 것까지 확인됨
- [ ] 수동 확인: 실제 게임으로 레이드 자동 전환 정상 동작 (앱을 켠 채 레이드 시작) —
      자동화로 커버되지 않는 유일한 항목; 탭/재시작 왕복, 첫 실행 기본값, 무효 값
      fallback은 위의 e2e/단위 테스트가 커버

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| 멱등 `Loaded`가 어떤 데이터가 의존하던 재초기화를 건너뜀 → 재진입 시 낡은 내용 | Medium | 이벤트 구독(진행도, DB 갱신, 언어)이 로드 중 갱신을 밀어주거나 진입 시 재장전됨; `ReconcileActiveRaid()`가 부재중 레이드 신호를 커버; 탭 전환 후 맵 마커/목표에 대한 수동 회귀 확인 |
| DPI-unaware 테스트 호스트에서 UIA 자동화가 불안정 | Low | Task 1.5에 강등 모드 문서화: DB-only 검증만으로도 R6 덮어쓰기 증상은 고정됨 |
| 저장된 팬이 다른 창 크기 기준으로 계산됨 → 복원 후 중심이 어긋난 뷰 | Low | 창 크기/위치도 저장됨(window-bounds 기능)이라 일반적으로는 기하가 정합; Reset View로 복구 가능; 기하 기반 클램프는 향후 강화 항목 |
| 프로세스 강제 종료 시 닫힘 시점의 줌/팬 저장 누락 | Low | 수용 — 맵 키는 변경 시 저장됨; 최악의 경우 한 세션의 줌/팬만 손실 (window bounds와 같은 트레이드오프) |

---

## Archive Info (fill on completion)

- **Completed**: YYYY-MM-DD
- **Summary**:
- **Actual vs Planned**:
- **Lessons Learned**:
- **Follow-up Items**:
