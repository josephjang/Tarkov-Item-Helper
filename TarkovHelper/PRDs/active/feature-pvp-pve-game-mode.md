# PvP/PvE Game Mode Progress Tracking PRD

## Overview

- **Status**: Planning
- **Created**: 2026-05-22
- **Updated**: 2026-05-22
- **Owner**: josephjang

## Problem Statement

EFT에는 PvP(표준)와 PvE(코옵/오프라인) 두 가지 게임 모드가 있으며, 각 모드는 캐릭터, 퀘스트 진행, 은신처, 인벤토리, 플레이어 스탯이 완전히 독립적입니다 (EFT 1.0, 2025년 11월 출시 기준).

현재 앱은 단일 전역 진행 상태만 관리하여, 두 모드를 모두 플레이하는 사용자가 모드 간 혼용된 상태를 보게 됩니다. PvE에서 완료한 퀘스트가 PvP 뷰에도 표시되고, 레벨/팩션 등 설정도 모드별로 다르게 설정할 수 없습니다.

### EFT PvP vs PvE 분리 현황 (검증됨)

| 데이터 | 분리 여부 |
|--------|-----------|
| 퀘스트/태스크 | 동일한 퀘스트 콘텐츠, 진행 상태는 완전 분리 |
| 은신처 (Hideout) | 모드별 독립 |
| 스태시/인벤토리 | 모드별 독립 |
| 캐릭터 레벨 & 스킬 | 모드별 독립 |
| 스캐브 카르마 (Fence rep) | 모드별 독립 |
| 트레이더 스탠딩 | 모드별 독립 |
| 프레스티지 | PvP: 시즌 와이프 / PvE: 자발적 프레스티지 (강제 와이프 없음) |

tarkov_data.db의 퀘스트 정의는 두 모드에서 동일하므로 변경 불필요.

## Goals

- [ ] Goal 1: PvP/PvE 별로 퀘스트/목표 진행 상태를 독립적으로 추적
- [ ] Goal 2: PvP/PvE 별로 은신처 건설 레벨을 독립적으로 추적
- [ ] Goal 3: PvP/PvE 별로 아이템 인벤토리(FIR/Non-FIR)를 독립적으로 추적
- [ ] Goal 4: PvP/PvE 별로 플레이어 설정(레벨, 스캐브 카르마, 팩션, 에디션, 프레스티지)을 독립적으로 저장
- [ ] Goal 5: 게임 로그에서 세션 모드 자동 감지 후 활성 모드 자동 전환
- [ ] Goal 6: 타이틀바에 PvP/PvE 토글 UI 제공 (수동 전환 지원)
- [ ] Goal 7: 기존 사용자 데이터를 PvP 프로필로 자동 마이그레이션

## Non-Goals (Scope Out)

- 2개 초과 모드 / 임의의 이름을 가진 프로필 시스템
- 모드 간 진행 상태 비교 또는 동기화 UI
- 트레이더 스탠딩 추적 (현재 앱에서 추적하지 않음)
- tarkov_data.db 변경 (읽기 전용, 두 모드에서 동일한 퀘스트 정의 공유)

## Technical Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| 내부 데이터 모델: ProfileId (문자열) | 향후 확장성 유지. 현재는 'pvp'/'pve' 두 값만 사용 | 2026-05-22 |
| UI: GameMode 토글 (PvP/PvE) | "프로필" 용어를 UI에서 숨겨 불필요한 복잡성 제거 | 2026-05-22 |
| DB 스키마: 단일 DB + 복합 기본 키 (ProfileId, Id) | 동일한 퀘스트 ID가 두 모드에 모두 존재 가능. 아래 대안 참고. | 2026-05-22 |

### 검토된 대안: 프로필별 DB 파일 분리

프로필마다 별도 SQLite 파일을 두는 방식 (`user_data_pvp.db`, `user_data_pve.db`).

**장점**: DB 스키마 변경·마이그레이션 불필요 (기존 `user_data.db`가 그대로 PvP), 모드 간 데이터 격리 완벽, 파일 단위 백업 가능.

**단점**: `logFolderPath`, `baseFontSize` 등 전역 설정을 어느 파일에 둘지 결정 필요 (세 번째 파일 또는 PvP DB에 혼재), `UserDataDbService`가 두 파일의 연결을 관리해야 함.

**기각 이유**: 전역 설정과 프로필 설정이 같은 `UserSettings` 테이블에 섞여 있어 분리 시 추가 복잡성 발생. 단일 DB + `ProfileId` 복합 키 방식이 서비스 레이어를 단순하게 유지하면서 다중 프로필로의 확장도 용이함.
| 기존 데이터 마이그레이션: PvP 프로필로 | 기존 사용자의 진행 상태를 보존하는 가장 안전한 기본값 | 2026-05-22 |
| ProfileSettings 별도 테이블 | 전역 설정(logPath 등)과 프로필별 설정(레벨 등)을 명확히 분리 | 2026-05-22 |

## Implementation Plan

### Phase 1: ProfileService & DB 마이그레이션

**목표**: 내부 프로필 인프라 구축

- [ ] Task 1.1: ProfileService 신규 작성
  - Files: `TarkovHelper/Services/ProfileService.cs` (new)
  - Notes: ActiveGameMode, ActiveProfileId, SetActiveGameMode, InitializeAsync, EftRaidEventService 구독

- [ ] Task 1.2: DB 스키마 마이그레이션
  - Files: `TarkovHelper/Services/UserDataDbService.cs`
  - Notes: QuestProgress/ObjectiveProgress/HideoutProgress/ItemInventory 테이블 복합 PK로 재생성, ProfileSettings 테이블 추가

- [ ] Task 1.3: UserDataDbService 메서드에 profileId 파라미터 추가
  - Files: `TarkovHelper/Services/UserDataDbService.cs`

- [ ] Task 1.4: TestMenu - DB 마이그레이션 검증 함수 추가
  - Files: `TarkovHelper/Debug/TestMenu.cs`

### Phase 2: 서비스 레이어 업데이트

**목표**: 진행 상태 서비스가 활성 프로필을 인식하도록 변경

- [ ] Task 2.1: SettingsService - 프로필별 설정을 ProfileSettings 테이블로 이동
  - Files: `TarkovHelper/Services/SettingsService.cs`
  - Notes: playerLevel, scavRep, faction 등 → ProfileSettings; 로그 경로 등 → UserSettings 유지

- [ ] Task 2.2: QuestProgressService - 프로필 인식 및 프로필 전환 시 리로드
  - Files: `TarkovHelper/Services/QuestProgressService.cs`

- [ ] Task 2.3: HideoutProgressService - 프로필 인식 및 리로드
  - Files: `TarkovHelper/Services/HideoutProgressService.cs`

- [ ] Task 2.4: ItemInventoryService - 프로필 인식 및 리로드
  - Files: `TarkovHelper/Services/ItemInventoryService.cs`
  - Notes: _pendingSaves를 Dictionary<string, string>으로 변경 (저장 시점의 profileId 캡처)

### Phase 3: UI 구현

**목표**: 타이틀바 게임 모드 토글 추가

- [ ] Task 3.1: MainWindow 타이틀바에 PvP/PvE ToggleButton 추가
  - Files: `TarkovHelper/MainWindow.xaml`, `TarkovHelper/MainWindow.xaml.cs`
  - Notes: ProfileService.ActiveProfileChanged 구독, Auto 뱃지 표시

- [ ] Task 3.2: Window_Loaded 초기화 순서 조정
  - Files: `TarkovHelper/MainWindow.xaml.cs`
  - Notes: UserDataDbService.InitializeAsync → ProfileService.InitializeAsync → 기존 순서 유지

## Dependencies

- [ ] EftRaidEventService.RaidEvent (SessionModeDetected) — 이미 존재
- [ ] GameMode enum in `TarkovHelper/Models/EftRaidEvent.cs` — 이미 존재

## Completion Criteria

- [ ] dotnet build 성공
- [ ] PvP/PvE 수동 전환 후 모든 탭(퀘스트/은신처/아이템) 즉시 갱신
- [ ] 기존 user_data.db 마이그레이션 후 PvP 진행 상태 보존
- [ ] 로그 모니터링 중 PvE 세션 감지 → 자동 전환 + Auto 뱃지
- [ ] 신규 설치 (DB 없음) → 새 스키마로 테이블 생성

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| DB 마이그레이션 중 데이터 손실 | 높음 | 트랜잭션으로 래핑, 실패 시 _old 테이블 보존 |
| 프로필 전환 시 디바운스 저장이 잘못된 프로필에 저장 | 중간 | _pendingSaves에 profileId 캡처 |
| SettingsService 초기화 시 ProfileService 미초기화 | 중간 | Window_Loaded에서 명시적 순서 보장 |

## Progress Log

| Date | Update | By |
|------|--------|-----|
| 2026-05-22 | PRD 생성 | user |

---

## Archive Info (완료 시 작성)

- **Completed**: -
- **Summary**: -
- **Actual vs Planned**: -
- **Lessons Learned**: -
- **Follow-up Items**: -
