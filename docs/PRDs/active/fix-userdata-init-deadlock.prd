# UserDataDbService 초기화 데드락 방지 PRD

## Overview

- **Status**: Planning
- **Created**: 2026-06-01
- **Updated**: 2026-06-01
- **Owner**: josephjang
- **Origin**: PR #1 `gemini-code-assist` Critical 리뷰 (`UserDataDbService.cs:1057`)

## Problem Statement

`UserDataDbService`의 동기 접근자(`GetSetting`, `SetSetting`, `GetProfileSetting`,
`SetProfileSetting`)는 미초기화 시 `InitializeAsync().GetAwaiter().GetResult()`로 비동기 init을
UI 스레드에서 블로킹 호출한다. WPF UI 스레드에는 `SynchronizationContext`가 있어, `await`가
continuation을 UI 컨텍스트로 post하려는데 UI 스레드는 `.GetResult()`로 블로킹되어 있으면
교착(deadlock)이 발생할 수 있다.

### 호출 경로 (근거)

`Program.Main` → `new MainWindow()`의 필드 초기화에서 `LocalizationService.Instance` /
`SettingsService.Instance` 생성자가 동기 `GetSetting`을 호출 → 최초 init(스키마 마이그레이션
포함)이 **메시지 펌프(`app.Run`) 이전, STA 스레드에서 블로킹으로** 실행된다.

### 현재 미발현 이유 (잠재 위험)

`Microsoft.Data.Sqlite`의 `OpenAsync`/`ExecuteAsync`는 사실상 동기로 완료되어 `InitializeAsync`가
실제로 suspend되지 않는다. 그래서 오늘은 교착이 드러나지 않지만, 향후 (a) 진짜 비동기 I/O 도입,
(b) 다른 DB 프로바이더 교체, (c) 호출 경로 변경 시 표면화될 수 있는 **잠재 결함**이다.

### 함께 고려된 인접 문제 (같은 root cause)

- **동시 init 경쟁:** `if(!_isInitialized)` check-then-act에 락이 없어, UI 경로와 백그라운드
  fire-and-forget(`ProfileService.SetActiveGameMode`의 `_ = SetSettingAsync(...)`)이 동시 진입
  가능. 이 경우 PR이 추가한 파괴적 마이그레이션(`RENAME→DROP`)이 중복 실행되어 데이터 손실 위험.
- **UI 블로킹:** init+마이그레이션을 창 표시 전 UI 스레드에서 블로킹 → 첫 실행 시 시작 지연.

## Goals

- [ ] Goal 1: 동기 접근자에서 UI 스레드 데드락 가능성 제거
- [ ] Goal 2: `InitializeAsync`가 어떤 호출 순서/스레드에서도 정확히 1회만 실행되도록 보장
- [ ] Goal 3: 첫 실행 시 마이그레이션으로 인한 UI 블로킹 최소화

## Non-Goals

- 동기 접근자를 전부 async로 바꾸는 호출부 전반 리팩터링 (블래스트 큼, 별도 과제)
- 설정 인메모리 캐시(Layer 2) 도입 — 별도 성능 과제
- 교차 프로세스/외부 DB 변경 안전성 (단일 인스턴스 Mutex 등) — 아래 Risks에 기록, 별도 과제

## Technical Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| init 앞당기기: `Program.Main`에서 `new App()` 이전 1회 `InitializeAsync().GetResult()` | Dispatcher SyncContext·타 스레드 생성 이전이라 데드락·동시성 구조적으로 불가. 마이그레이션을 pre-UI 단계로 모음 | 2026-06-01 |
| 동기 접근자: lazy re-init 제거 → 미초기화 시 fail-fast 예외 | init 앞당기기 전제 하에 정상 흐름에선 미발현. 순서 버그를 데드락 대신 명시적 예외로 표면화. Gemini가 지적한 `.GetResult()`도 구조적으로 제거 | 2026-06-01 |
| init 라인은 모든 설정 접근보다 앞 | `Program.Main:12 RunMigrationIfNeeded()`가 `SettingsService`를 건드릴 수 있어 진입 순서 점검 필요 | 2026-06-01 |

### 검토된 대안: Task.Run 폴백

동기 접근자의 `.GetResult()`를 `Task.Run(() => InitializeAsync()).GetResult()`로 감싸 UI
컨텍스트를 회피하는 방식(Gemini 제안). **장점**: 변경 최소, 코멘트 즉시 해소. **단점**: 근본
해결이 아님(블로킹·동시성 잔존), 시작 시 cold 스레드풀 대기로 체감 지연·풀 기아 위험. init
앞당기기를 채택하면 이 분기는 죽은 코드가 되므로 기각.

## Risks / Open Questions

- **교차 프로세스 경쟁(별도 과제):** 단일 인스턴스 Mutex가 없어 앱 중복 실행 시 두 프로세스가
  같은 `user_data.db`의 파괴적 마이그레이션에 동시 진입 → TOCTOU 손상 가능. in-process 해법
  (init 앞당기기/fail-fast)으로는 못 막음. 해결책 후보: 단일 인스턴스 Mutex, 또는 마이그레이션을
  `BEGIN IMMEDIATE` 트랜잭션 안에서 `hasProfileId` 재검사.
- `RunMigrationIfNeeded`가 실제로 어떤 설정/서비스를 건드리는지 확인 필요(fail-fast 도입 전제).

## Implementation Plan

### Phase 1: init 앞당기기
- [ ] `Program.Main`에서 `RunMigrationIfNeeded()`/`new App()` 대비 안전한 위치에 init 1회 추가
- [ ] 진입 순서 검증 (모든 동기 설정 접근이 init 이후인지)

### Phase 2: 동기 접근자 fail-fast 전환
- [ ] 4개 동기 접근자의 lazy re-init 제거 → 미초기화 시 `InvalidOperationException`

### Phase 3: 검증
- [ ] cold start / 기존 DB 마이그레이션 / 첫 실행 경험 회귀 테스트

## Test Plan

- [ ] 신규 설치(빈 DB) 정상 시작 및 설정 읽기/쓰기
- [ ] 기존 DB(마이그레이션 필요) 시작 시 1회 마이그레이션, 데이터 보존
- [ ] PvP/PvE 전환·로그 자동 감지 등 백그라운드 경로에서 예외 없이 동작

## Progress Log

- 2026-06-01: PR #1 Gemini Critical 리뷰 기반 PRD 작성 (Planning)
