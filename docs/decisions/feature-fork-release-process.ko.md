# 독립 포크 릴리즈 프로세스 PRD

## 개요

- **상태**: 완료 (PR #10 main 머지; 첫 릴리즈 v2026.7.0 발행 2026-07-25)
- **작성일**: 2026-07-24
- **수정일**: 2026-07-25
- **담당자**: josephjang
- **번역**: 영어 원본 `feature-fork-release-process.md` (1:1 동기화 유지)

## 문제 정의

이 포크(josephjang/Tarkov-Item-Helper)는 2026-07-02에 fork-first로 전환한 이후
upstream(Zeliper/Tarkov-Item-Helper)과 독립적으로 개발되어 왔다. 다른 사용자가
포크를 사용하려면 GitHub에 자체 바이너리 릴리즈가 필요하지만, v4.3.1 기준으로 포크는
릴리즈가 아예 불가능한 상태다:

- 모든 업데이트 채널이 upstream 저장소를 하드코딩: 앱 자체 업데이트 피드
  (`UpdateService.cs`), DB 자동 업데이트 URL(`DatabaseUpdateService.cs`),
  `update.xml`의 다운로드/체인지로그 URL. 이대로 배포한 포크 빌드는
  **자기 자신을 upstream 빌드로 교체**하겠다고 제안하게 된다.
- ZIP 패키징 단계(`CreateRelease.bat`)는 커밋된 적이 없고 어디에도 남아 있지 않다 —
  릴리즈 재현 불가.
- LICENSE 파일이 없고(README에 MIT 문구만 존재), CI가 없으며, 배포본에
  `Assets/db_version.txt`가 빠져 있어 신규 설치마다 DB 전체를 한 번 재다운로드한다.

## 목표

- [x] 목표 1: 포크 빌드는 **오직** 포크에 대해서만 업데이트: 앱 피드, DB 피드,
      다운로드 URL 모두 josephjang/Tarkov-Item-Helper를 가리킨다.
- [x] 목표 2: 반복 가능하고 대부분 자동화된 릴리즈 프로세스: `v*` 태그 push가
      빌드→테스트→패키징→`TarkovHelper.zip`이 첨부된 GitHub Release 발행까지 수행.
- [x] 목표 3: 클라이언트는 404가 나는 다운로드 URL을 절대 보지 않는다
      (update.xml은 릴리즈 자산이 존재한 뒤에만 범프).
- [x] 목표 4: 법적으로 배포 가능한 포크: 이중 저작권(Zeliper + Jeongho Jang)의
      실제 MIT `LICENSE` 파일과, 프로젝트를 유지보수 포크로 소개하는 README.
- [x] 목표 5: 첫 릴리즈 **v2026.7.0** 발행 및 end-to-end 검증.

## 범위 제외 (Non-Goals)

- 앱 이름 변경이나 실행 파일/zip 이름 변경(`TarkovHelper.exe`, `TarkovHelper.zip` —
  AutoUpdater.NET이 기존 레이아웃을 기대).
- 인스톨러(MSI/MSIX/Inno) — 배포는 upstream과 동일하게 포터블 zip 유지.
- Self-contained 빌드 — framework-dependent 유지(.NET 8 Desktop Runtime 필요).
- 기존 upstream 설치본의 마이그레이션(upstream 피드를 폴링하므로 통제 밖).
- v4.3.1 이전 upstream 태그 26개의 push(릴리즈 노트 기준선인 `v4.3.1`만 push).

## 기술 결정

| 결정 | 근거 | 날짜 |
|------|------|------|
| **CalVer `YYYY.M.N`** (N = 월내 릴리즈 일련번호, 0부터; 기능/픽스 의미 없음), **2026.7.0**부터 시작 | upstream의 4.x SemVer 라인과 명확히 구분; 항상 수치적으로 더 큼(2026 > 4) → `System.Version` 비교가 계속 동작; 데이터·게임 패치 주기 중심의 릴리즈 사이클에 부합 | 2026-07-24 |
| **`v*` 태그 push 트리거 GitHub Actions** 릴리즈 자동화 (`.github/workflows/release.yml`) | 로컬 환경에 의존하지 않는 재현 가능한 빌드; 사라진 `CreateRelease.bat` 단계를 대체 | 2026-07-24 |
| 패키지는 **framework-dependent zip** 유지 (`build/Create-ReleasePackage.ps1`, zip 루트 = 앱 루트) | AutoUpdater.NET의 설치 폴더 인플레이스 압축 해제 모델 및 기존 사용자 기대(.NET 8 Desktop Runtime 요구)와 일치; 다운로드 용량 최소화 | 2026-07-24 |
| **2단계 update.xml 플로우**: 태그/릴리즈 먼저, update.xml 범프는 자산 확인 후 | 피드는 `main`에서 3분마다 raw로 폴링됨; 마지막에 범프하면 아직 존재하지 않는 URL이 클라이언트에 제시될 수 없음 | 2026-07-24 |
| update.xml 버전은 릴리즈 사이에 csproj 버전보다 의도적으로 **뒤처짐**; 릴리즈 워크플로는 태그 == csproj만 검사 | 2단계 플로우의 직접 결과; xml == csproj를 단언하면 모든 릴리즈 실행이 깨짐 | 2026-07-24 |
| 가드 테스트가 세 피드 상수와 update.xml URL을 `/josephjang/`에 고정 | upstream URL을 되살리는 잘못된 머지는 포크 빌드가 자신을 upstream 앱으로 교체하게 만듦 — 이 저장소 최악의 조용한 실패 | 2026-07-24 |
| 레거시 태그는 `v4.3.1` 하나만 origin에 push | `--generate-notes`와 체인지로그에 `v4.3.1...v2026.7.0` 기준선 제공; 옛 커밋에는 워크플로 파일이 없어 태그 push로 아무것도 트리거되지 않음 | 2026-07-24 |

## 구현 계획

### Phase 1: 포크 빌드 자립화 (repoint + 패키징 공백 수정)

- [x] Task 1.1: 앱 자체 업데이트 피드 repoint 및 파서 테스트 가능화
  - 파일: `TarkovHelper/Services/UpdateService.cs` (`UpdateXmlUrl` → 포크, `internal`;
    `ParseUpdateXml` → `internal static`)
- [x] Task 1.2: DB 자동 업데이트 URL repoint
  - 파일: `TarkovHelper/Services/DatabaseUpdateService.cs` (`VERSION_URL`/`DATABASE_URL`
    → 포크, `internal`)
- [x] Task 1.3: 죽은 upstream URL 상수 제거
  - 파일: `TarkovHelper/App.xaml.cs` (미사용 `UpdateXmlUrl` + 고아
    `using AutoUpdaterDotNET;`)
- [x] Task 1.4: `update.xml` 호스트 repoint (버전은 첫 릴리즈까지 4.3.1 유지 —
      포크 클라이언트는 모두 2026.7.0 이상이므로 v4.3.1 URL은 절대 따라가지 않음)
  - 파일: `update.xml`
- [x] Task 1.5: `Assets/db_version.txt` 배포 포함; 앱 아이덴티티 메타데이터 및
      테스트 가시성 추가
  - 파일: `TarkovHelper/TarkovHelper.csproj` (복사 항목, `Product`/`Authors`/`Copyright`/
    `RepositoryUrl`, `InternalsVisibleTo`), `TarkovHelper/app.manifest` (아이덴티티 이름)

### Phase 2: 패키징, CI, 릴리즈 자동화

- [x] Task 2.1: 커밋되는 패키징 스크립트 (사라진 `CreateRelease.bat` 대체)
  - 파일: `build/Create-ReleasePackage.ps1`, `.gitignore` (`artifacts/`)
- [x] Task 2.2: 태그 트리거 릴리즈 워크플로 (버전 가드 → 테스트 → 패키징 →
      `--generate-notes`와 함께 `gh release create`)
  - 파일: `.github/workflows/release.yml`
- [x] Task 2.3: PR 및 main push 대상 최소 CI
  - 파일: `.github/workflows/ci.yml`

### Phase 3: 법적 요건 + 문서

- [x] Task 3.1: 이중 저작권 MIT `LICENSE`
  - 파일: `LICENSE`
- [x] Task 3.2: README를 포크로 소개 (안내문, clone URL, Desktop Runtime, 라이선스
      링크, 크레딧; upstream 후원 배지 제거)
  - 파일: `README.md`, `README_KR.md`, `README_JA.md`
- [x] Task 3.3: 새 플로우 중심으로 `/release` 커맨드 재작성; 루트 가이드에 짧은
      Releases 섹션 추가; 낡은 참조 수정
  - 파일: `.claude/commands/release.md`, `CLAUDE.md`,
    `docs/DatabaseUpdateMechanism.md`,
    `docs/PRDs/feature-hideout-localized-sort.md` + `.ko.md`

### Phase 4: 가드 테스트

- [x] Task 4.1: 실제 파서를 통한 update.xml 계약 테스트 (파싱 가능, 포크 호스팅,
      URL이 자체 버전과 일치; csproj 버전 일치는 의도적으로 단언하지 않음)
  - 파일: `TarkovHelper.Tests/UpdateXmlTests.cs`
- [x] Task 4.2: `ParseUpdateXml` 엣지 케이스 + 포크 URL 상수 가드
  - 파일: `TarkovHelper.Tests/UpdateServiceTests.cs`

### Phase 5: 첫 릴리즈 (이 PR이 main에 머지된 후)

- [x] Task 5.1: 기준선 태그 push: `git push origin refs/tags/v4.3.1`
- [x] Task 5.2: `/release 2026.7.0` 실행 (아래 "릴리즈 플로우" 참조)
- [x] Task 5.3: 완료 기준 체크리스트에 따라 검증 (CI + 자동 + 자산/피드 검증 완료;
      발행된 zip의 수동 런타임 스모크 체크는 미완 — 완료 기준 참조)

## 릴리즈 플로우 (정의되는 프로세스)

정식 실행 절차는 `.claude/commands/release.md`에 있으며, 요약:

1. **Preflight**: 버전이 `^\d{4}\.\d{1,2}\.\d+$`에 부합; 태그 `v<ver>` 미사용;
   클린하고 pull된 `main` 위; `gh auth status` 정상 (remote가 두 개이므로 모든
   `gh` 호출에 `-R josephjang/Tarkov-Item-Helper`).
2. `TarkovHelper.csproj`의 `<Version>` 범프 (AssemblyVersion/FileVersion은 이 값에서
   파생; **update.xml은 아직 아님**), `chore(release): bump version to <ver>` 커밋.
3. `git tag v<ver>` → `git push --atomic origin main v<ver>` — atomic이라 main push가
   거부되면 태그도 push되지 않음(미반영 커밋에서 CI가 도는 사고 방지); 이 태그 하나만
   push, `git push --tags` 절대 금지 (레거시 upstream 태그 26개 v0.9.0–v4.3.0은 로컬
   전용으로 유지).
4. CI(`release.yml`): 태그/csproj 가드 → 테스트 → 패키징 → `TarkovHelper.zip` +
   자동 생성 노트로 GitHub Release. `gh run watch --exit-status`로 대기.
5. `gh release edit --notes-file`로 이중 언어(EN/KO) 노트 큐레이션,
   `compare/<이전>...v<ver>` 링크 포함.
6. `TarkovHelper.zip` 자산 존재 확인 (`gh release view --json assets`).
7. **이제서야** main의 `update.xml` 범프 (버전 + 다운로드 URL) — 클라이언트
   (raw main 3분 폴링)는 자산이 확실히 존재한 뒤에만 새 버전을 보게 된다.

**실패 복구**: update.xml을 건드리지 않았으므로 클라이언트는 아무것도 못 봤다 —
릴리즈·태그 삭제, main에서 수정, **같은** 버전으로 재태깅.

## 진행 로그

| 날짜 | 업데이트 | 작성자 |
|------|----------|--------|
| 2026-07-24 | PRD 작성. 소유자와 결정 확정: 2026.7.0부터 CalVer `YYYY.M.N`, 태그 트리거 Actions 릴리즈, framework-dependent zip, upstream 후원 배지 제거, 레거시 태그는 `v4.3.1`만 push. Phase 1–4를 같은 세션에서 구현 (feature/fork-release-process). | josephjang |
| 2026-07-25 | 브랜치 딥 리뷰: 16개 수정 적용 (ref_name 인젝션 → env; 단일 `<Version>` 소스로 AssemblyVersion/FileVersion drift 차단; 비-Windows 런타임 제거 −9 MB; publish sanity check; 앵커 XPath + CalVer 가드; `--atomic` push; full-URL 피드 pin + Ordinal + 마이그레이션/경계 가드 테스트; preflight 로컬 빌드 게이트). PR #10 main 머지 (CI green). | josephjang |
| 2026-07-25 | **첫 릴리즈 발행.** `v4.3.1` 기준선 태그 push; csproj → 2026.7.0 범프; `git push --atomic origin main v2026.7.0`로 `release.yml` 트리거 (green, 5m3s: 버전 가드 → 빌드 → 테스트 → 패키징 → 릴리즈). GitHub Release `v2026.7.0` 라이브, `TarkovHelper.zip`(36.2 MB, draft/prerelease 아님) 첨부; 이중언어 노트 적용; 자산 확인 후 main의 `update.xml`을 2026.7.0으로 범프. | josephjang |

## 완료 기준

- [x] 모든 목표 달성
- [x] PR CI(`ci.yml`) green; 브랜치 main 머지
- [x] `v4.3.1` 기준선 태그 origin push
- [x] 첫 릴리즈 발행: `v2026.7.0`에 대해 `release.yml` green;
      `TarkovHelper.zip` 첨부 (public 저장소, 비인증 다운로드)
- [x] Zip 레이아웃 검증 (CI와 동일한 패키징 스크립트로 로컬 확인): zip 루트에
      `TarkovHelper.exe` + `Assets/` (`db_version.txt` 포함); `*.pdb` 없음; `Config/Data/Cache/Logs` 없음
- [x] 릴리즈 후 범프 뒤 main의 `update.xml`이 2026.7.0을 광고
- [x] 단위 가드 green: `UpdateXmlTests`, `UpdateServiceTests`
- [ ] **수동 스모크 체크 미완** (이번 세션에서 발행된 zip 대상 미실행): 추출한 앱을
      `dotnet TarkovHelper.dll`로 실행해 `v2026.7.0` 표시 확인; 인앱 업데이트 확인 최신 판정;
      DB 업데이트 확인 최신 판정. (로컬 2026.6.0 빌드로 AutoUpdater 자체 교체 리허설도 미완.)

## 리스크 및 완화

| 리스크 | 영향 | 완화 |
|--------|------|------|
| raw.githubusercontent CDN이 update.xml을 ~5분 캐시 | 낮음 — 가시성 지연만, 404 아님(범프 전에 자산 존재) | 수용 |
| 첫 릴리즈에서 `--generate-notes`가 부정확할 수 있음 | 낮음 | 5단계에서 큐레이션 노트로 덮어씀 |
| 향후 upstream 머지가 Zeliper URL을 되살림 | 높음 — 포크 빌드가 자신을 upstream 앱으로 교체 | `Update_feed_constants_point_at_fork` + `Update_xml_urls_point_at_fork` 가드 테스트가 빌드를 실패시킴 |
| 관리자 권한 앱 + AutoUpdater 인플레이스 교체 오동작 | 중간 | upstream과 동일 동작; 선택 리허설로 end-to-end 확인 |
| 릴리즈 워크플로 중간 실패 | 낮음 | 2단계 플로우로 클라이언트는 아무것도 못 봄; 문서화된 복구 절차로 같은 버전 재태깅 |

---

**참고 (2026-07-30)** — 릴리스된 `TarkovHelper.zip`의 수동 스모크 체크(패키징된
빌드 설치·실행)는 기록된 결과가 없습니다. 통과했다고 가정하는 대신 미검증으로
남깁니다.
