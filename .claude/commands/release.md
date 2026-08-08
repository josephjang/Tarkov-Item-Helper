# Release Command

버전 $ARGUMENTS 으로 릴리스를 수행합니다.

빌드/패키징/GitHub Release 생성은 `v*` 태그 push 시 `.github/workflows/release.yml`이
자동 수행합니다. 이 커맨드는 버전 범프, 태그 push, 릴리즈 노트 큐레이션, 그리고
릴리즈 완료 **후** `update.xml` 범프만 담당합니다. 설계 배경은
`docs/decisions/feature-fork-release-process.md` 참조.

## Preflight (하나라도 실패하면 중단)

1. `$ARGUMENTS`가 CalVer 형식 `^\d{4}\.\d{1,2}\.\d+$`에 부합하는지 확인
   (예: `2026.7.0`; 마지막 자리는 그 달의 릴리즈 일련번호이며 기능/픽스 구분 없음)
2. 태그 `v$ARGUMENTS`가 로컬/origin 어디에도 없는지 확인:
   `git tag -l v$ARGUMENTS` 와 `git ls-remote --tags origin v$ARGUMENTS`
3. `main` 브랜치에서 클린 워킹 트리인지 확인 후 `git pull origin main`
4. `gh auth status` 정상 확인. **remote가 두 개(origin=josephjang, upstream=Zeliper)
   이므로 모든 `gh` 호출에 `-R josephjang/TarkovHelper`를 붙일 것**
5. **로컬 빌드 게이트**: `dotnet build TarkovHelper.sln -c Release` 성공 확인.
   실패하면 여기서 중단합니다. 태그를 push하기 전에 컴파일 오류를 잡아, CI 실패 후
   태그/릴리즈를 지우는 복구 절차를 애초에 피합니다 (CI는 여전히 최종 게이트)

## 릴리즈 수행

1. **csproj 버전 범프**: `TarkovHelper/TarkovHelper.csproj`의 `<Version>`만
   `$ARGUMENTS`로 변경 (`<AssemblyVersion>`/`<FileVersion>`은 SDK가 `<Version>`에서
   자동 파생하므로 별도 필드가 없음).
   **update.xml은 아직 건드리지 않습니다** (마지막 단계에서 범프)
2. **커밋**: `git commit -am "chore(release): bump version to $ARGUMENTS"`
3. **태그 + push (atomic)**: `git tag v$ARGUMENTS` 후
   `git push --atomic origin main v$ARGUMENTS`
   - `--atomic`: main과 태그를 한 트랜잭션으로 push. main이 non-fast-forward로
     거부되면 태그도 push되지 않음 → main에 없는 커밋에서 release.yml이 도는 사고 방지
   - 반드시 이 태그 **하나만** push. **`git push --tags` 절대 금지** — 로컬에는
     upstream 시절 레거시 태그 26개(v0.9.0~v4.3.0)가 있으며 (baseline v4.3.1 외에는)
     push하지 않기로 결정됨
4. **워크플로 대기**: 약 15초 후
   `gh run list -R josephjang/TarkovHelper --workflow release.yml --limit 1 --json databaseId`
   로 run ID 확인 →
   `gh run watch <id> --exit-status -R josephjang/TarkovHelper`
5. **릴리즈 노트 큐레이션** (워크플로 성공 시):
   - 이전 태그 확인: `git describe --tags --abbrev=0 "v$ARGUMENTS^"`
   - **전수 조사 (커버리지 규칙)**: `git log <이전태그>..v$ARGUMENTS --oneline` 의
     모든 커밋을 "노트 항목에 반영" 또는 "명시적 제외" 중 하나로 분류합니다.
     제외 기준: 문서/PRD, CI/테스트, TarkovDBEditor 전용 변경 (개발 도구라 배포본에
     미포함). 단, DB 콘텐츠 변경은 tarkov_data.db로 배포되므로 사용자 가시 항목입니다.
     어느 쪽에도 분류되지 않은 커밋이 남아 있으면 노트는 미완성입니다.
     (v2026.7.0에서 PvP/PvE 모드처럼 릴리즈 두 달 전에 머지된 대형 기능이 누락된
     원인이 이 규칙의 부재였습니다)
   - **PR/기여자 매핑**: squash/rebase 머지라 머지 커밋이 없으므로
     `gh api repos/josephjang/TarkovHelper/commits/{sha}/pulls` 로 커밋을 PR에
     연결합니다. PR이 없는 커밋(업스트림 직접 커밋 등)은
     `gh api repos/josephjang/TarkovHelper/commits/{sha} --jq '.author.login'` 으로
     작성자를 확인해 기여자만 표기합니다.
   - **사실 검증**: 각 항목의 문구를 해당 커밋/PRD와 대조합니다. 구현보다 좋게 쓰지
     않습니다 (예: 알파벳순 정렬을 "인게임 순서와 동일"이라고 쓰지 않기).
   - 아래 형식으로 3개 언어(EN/KO/JA) 노트를 scratchpad 임시 파일에 작성 후:
     `gh release edit v$ARGUMENTS -R josephjang/TarkovHelper --notes-file <파일>`
6. **자산 확인**:
   `gh release view v$ARGUMENTS -R josephjang/TarkovHelper --json assets`
   결과에 `TarkovHelper.zip`이 있어야 합니다
7. **update.xml 범프 (자산 확인 후에만!)**:
   - `<version>` → `$ARGUMENTS`
   - `<url>` → `https://github.com/josephjang/TarkovHelper/releases/download/v$ARGUMENTS/TarkovHelper.zip`
   - `git commit -am "chore(release): point update.xml at v$ARGUMENTS"` 후
     `git push origin main`
   - 이 순서 덕분에 클라이언트(3분마다 raw main의 update.xml 폴링)는 다운로드
     자산이 실제로 존재한 뒤에만 새 버전을 보게 됩니다 (404 창 제로)

## Release Notes 형식

제품 가치 관점으로 씁니다. 커밋 prefix를 분류해 나열하는 방식(feat/fix 표)은
커밋 어투를 그대로 옮기게 만들므로 쓰지 않습니다.

작성 규칙:

- 영향이 큰 순서로 배치합니다 (이번 릴리즈의 대표 기능이 맨 위)
- 기능은 굵은 제목으로 그룹화하고, 필요하면 한 줄 도입문과 불릿으로 설명합니다.
  수정 사항은 "Fixes / 수정 / 修正" 목록으로 모읍니다
- 커밋 메시지 어투 금지. 사용자가 체감하는 증상/효과 중심으로 씁니다:
  "culture-sensitive comparer resolution 수정" (X) →
  "일부 시스템에서 Windows 로캘에 따라 발생하던 시작 시 크래시를 수정" (O)
- 항목별 출처 표기: EN `(#N by @user)`, KO `(#N, @user)`, JA `(#N、@user)`.
  한 PR이 그룹 전체를 커버하면 굵은 제목 옆에, 불릿마다 PR이 다르면 불릿 끝에
  붙입니다. 업스트림 직접 커밋은 PR 없이 기여자만:
  `(upstream fix by @user)` / `(업스트림 수정, @user)` / `(アップストリーム修正、@user)`
- CLAUDE.md의 Writing Conventions를 따릅니다 (em dash, 가운뎃점, "…" 금지).
  일본어 용어는 EFT 커뮤니티 관례를 따릅니다 (ハイドアウト, Scavカルマ, 陣営)

```markdown
## What's Changed / 변경 사항 / 変更内容

### English

**[대표 기능 제목]** (#N by @user)

[한 줄 도입문 (선택)]

- [사용자 체감 효과 중심 설명]

**Fixes**

- [증상 중심 수정 설명] (#N by @user)

### 한국어

[같은 구조, 출처 표기는 `(#N, @user)`]

### 日本語

[같은 구조, 출처 표기는 `(#N、@user)`]

---
**Full Changelog**: https://github.com/josephjang/TarkovHelper/compare/[이전태그]...v$ARGUMENTS
```

참고 사례: v2026.7.0 노트가 이 형식으로 작성되어 있습니다.
https://github.com/josephjang/TarkovHelper/releases/tag/v2026.7.0

## 실패 복구 (워크플로 red)

update.xml을 아직 범프하지 않았으므로 어떤 클라이언트도 깨진 버전을 보지 못했습니다.
복구는 안전하며 외부에서 보이지 않습니다:

1. 부분 생성된 릴리즈가 있으면:
   `gh release delete v$ARGUMENTS -y -R josephjang/TarkovHelper`
2. 태그 삭제: `git tag -d v$ARGUMENTS` 후
   `git push origin :refs/tags/v$ARGUMENTS`
3. main에서 원인 수정 (일반 커밋) 후 위 "릴리즈 수행" 3단계부터 **같은 버전으로**
   재시도

## 주의사항

- 로컬에서 패키지를 확인하고 싶으면 `./build/Create-ReleasePackage.ps1` 실행
  (CI와 동일한 스크립트; 결과물은 `artifacts/TarkovHelper.zip`)
- gh CLI 경로가 PATH에 없을 수 있음: `C:\Program Files\GitHub CLI\gh.exe`
- update.xml 수정 시 XML 태그가 누락되지 않도록 주의 (닫는 태그 필수).
  `UpdateXmlTests`가 형식, 포크 URL, 버전-URL 일치를 검증하므로 수정 후
  `dotnet test --filter UpdateXmlTests` 로 확인 가능

위 작업들을 순서대로 실행해주세요. 각 단계마다 결과를 확인하고 진행하세요.
