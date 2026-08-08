# docs/

저장소 전체의 문서가 모이는 곳입니다. 두 계층으로 나뉩니다:

- **결정 문서** (`decisions/`): 할 작업(또는 한 작업)에 대한 결정 기록.
  형식과 규칙은 `decisions/README.md` 참고.
- **참고 문서** (이 폴더 바로 아래): 현재 시스템이 어떻게 동작하는가를 기술하는
  살아있는 문서. 시스템이 바뀌면 함께 고칩니다.

특정 시점의 일회성 조사 결과(스냅샷 분석)는 여기 두지 않습니다. 과거의
스냅샷들은 `decisions/archive/`에 동결되어 있고, 새로 필요한 시점 분석은 해당
작업 spec의 Current Behavior 섹션으로 남깁니다.

프로젝트 내부에만 해당하는 구현 노트는 각 프로젝트의 `docs/`에 둡니다
(예: `TarkovDBEditor/docs/`).

## 참고 문서 목록

- [database-schema.md](database-schema.md): tarkov_data.db 스키마
  (TarkovDBEditor가 생성, TarkovHelper가 소비)
- [database-update-mechanism.md](database-update-mechanism.md): 앱과 DB의
  자동 업데이트 메커니즘
- [eft-log-patterns.md](eft-log-patterns.md): EFT 게임 로그 폴더 구조와
  레이드 정보 추출 패턴
- [eft-raid-event-service.md](eft-raid-event-service.md): EftRaidEventService가
  제공하는 이벤트와 사용법
- [tarkov-market-markers-api.md](tarkov-market-markers-api.md): Tarkov Market
  마커 API 분석

## 관례

- **파일명은 kebab-case**로 짓습니다 (`database-schema.md`).
- **새 참고 문서는 영어로** 씁니다. 기존 한국어 문서는 그대로 유지합니다
  (결정 문서와 같은 규칙).
- 새 문서를 추가하면 위 목록에도 한 줄 추가합니다.
