# PRDs (Product Requirements Documents)

이 폴더는 저장소 전체(TarkovHelper + TarkovDBEditor + 프로젝트 간 교차 작업)의 기능 개발
계획을 관리하는 단일 위치입니다. `TarkovHelper`만을 위한 폴더가 아니라서 저장소 루트의
`docs/`(TarkovHelper.sln, TarkovHelper/, TarkovDBEditor/와 형제 위치)에 있습니다 — 예를 들어
`fix-quest-name-localization.prd`는 TarkovDBEditor의 데이터 파이프라인과 TarkovHelper의
WPF 앱을 모두 다룹니다.

## PRD vs 참고 문서

- **PRD** (`docs/PRDs/`): 계획된 작업 — Goals, Implementation Plan, Completion Criteria가
  있는 문서. "앞으로 무엇을 할 것인가"를 기술합니다.
- **참고/분석 문서** (`docs/` 바로 아래, 또는 `TarkovDBEditor/docs/`): DB 스키마, 시스템
  분석, 로그 포맷 노트처럼 "현재 시스템이 어떻게 동작하는가"를 기술하는 문서. 완료 기준이나
  진행 상태를 추적하지 않습니다.

새 문서를 어디에 둘지 애매하면: Goals/Completion Criteria로 자연스럽게 쓸 수 있으면 PRD,
아니면 참고 문서입니다.

## Folder Structure

```
docs/
├── PRDs/
│   ├── README.md              # 이 파일
│   ├── active/                # 진행 중인 PRD
│   ├── archive/                # 완료(또는 폐기)된 PRD (월별 정리, YYYY-MM/)
│   └── templates/             # PRD 템플릿
└── (그 외 모든 파일)            # 참고/분석 문서 (예: DatabaseSchema.md, Map_System_Analysis.md)
```

## Workflow

### 1. 새 기능 계획
1. `templates/feature-template.prd`를 복사하여 `active/` 폴더에 생성
2. 파일명: `feature-[기능명].prd` (예: `feature-map-v2.prd`)
3. PRD 내용 작성

### 2. 작업 진행
1. Status를 "In Progress"로 변경
2. 각 Task 완료 시 체크박스 체크
3. Progress Log에 진행 상황 기록
4. 관련 에이전트의 Learning Log 업데이트 요청

### 3. 완료 및 아카이빙
1. 모든 Task 완료 확인
2. Status를 "Completed"로 변경
3. Archive Info 섹션 작성
4. `archive/YYYY-MM/` 폴더로 이동

### 4. 정체(Stale) PRD 처리

`active/`에 있는 PRD가 **~30일 이상** 업데이트되지 않았다면, 다음에 이 폴더를 건드리는
사람이 방치하지 말고 처리합니다:
- 실제로 진행 중이면 Progress Log를 갱신하고 계속 진행
- 아니면 완료 여부와 관계없이 **정직하게** Archive Info를 채우고 archive로 이동
  (예: "Superseded — 2025-12 이후 활동 없음, 다른 작업으로 우선순위 이동")

`active/`는 실제로 살아있는 작업만 남아있어야 합니다. (2026-07 정리 당시 3개의 PRD가
6개월 이상 방치되어 있었던 것이 이 규칙이 생긴 이유입니다.)

## 이중 언어(EN/KO) PRD

사용자 대상 동작을 다루는 PRD는 영문 원본(`name.prd`) + 한글 번역본(`name.ko.prd`)을
1:1로 유지할 수 있습니다 (예: `feature-hideout-localized-sort.*`,
`feature-quest-unlock-sort.*`, `fix-quest-name-localization.*`). 두 문서 내용이 충돌하면
**영문 원본이 기준**입니다. 코드 식별자(`AppLanguage.KO`, `NameKO` 등)는 번역하지 않고
그대로 둡니다.

## PRD Status

| Status | Description |
|--------|-------------|
| Planning | 계획 수립 중 |
| In Progress | 개발 진행 중 |
| Review | 검토/테스트 중 |
| Completed | 완료 |
| Archived | 아카이브됨 |

## Agent Integration

PRD는 다음 에이전트들과 연동됩니다:

| Agent | Role |
|-------|------|
| `prd-manager` | PRD 생성/관리/아카이빙 |
| `map-feature-specialist` | Map 기능 작업 |
| `db-schema-analyzer` | DB 스키마 작업 |
| `wpf-xaml-specialist` | UI/XAML 작업 |
| `service-architect` | 서비스 설계 작업 |

## Commands

```powershell
# 활성 PRD 목록 확인 (저장소 루트에서 실행)
ls docs/PRDs/active/

# PRD 아카이빙 (2026년 7월)
New-Item -ItemType Directory -Force docs/PRDs/archive/2026-07/
git mv docs/PRDs/active/feature-xxx.prd docs/PRDs/archive/2026-07/
```

## Best Practices

1. **작은 단위**: 각 PRD는 1-2주 내 완료 가능한 크기로
2. **명확한 기준**: 완료 기준을 구체적으로 명시
3. **진행 기록**: Progress Log를 꾸준히 업데이트
4. **에이전트 학습**: 작업 결과를 에이전트 파일에 기록
5. **정기 정리**: 완료된 PRD는 월별로 아카이빙, 정체된 PRD는 위 "정체 PRD 처리" 규칙을 따름
