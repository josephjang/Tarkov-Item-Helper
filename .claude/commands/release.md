# Release Command

버전 $ARGUMENTS 으로 릴리스를 수행합니다.

빌드/패키징/GitHub Release 생성은 `v*` 태그 push 시 `.github/workflows/release.yml`이
자동 수행합니다. 이 커맨드는 버전 범프, 태그 push, 릴리즈 노트 큐레이션, 그리고
릴리즈 완료 **후** `update.xml` 범프만 담당합니다. 설계 배경은
`docs/PRDs/active/feature-fork-release-process.md` 참조.

## Preflight (하나라도 실패하면 중단)

1. `$ARGUMENTS`가 CalVer 형식 `^\d{4}\.\d{1,2}\.\d+$`에 부합하는지 확인
   (예: `2026.7.0`; 마지막 자리는 그 달의 릴리즈 일련번호 — 기능/픽스 구분 없음)
2. 태그 `v$ARGUMENTS`가 로컬/origin 어디에도 없는지 확인:
   `git tag -l v$ARGUMENTS` 와 `git ls-remote --tags origin v$ARGUMENTS`
3. `main` 브랜치에서 클린 워킹 트리인지 확인 후 `git pull origin main`
4. `gh auth status` 정상 확인. **remote가 두 개(origin=josephjang, upstream=Zeliper)
   이므로 모든 `gh` 호출에 `-R josephjang/Tarkov-Item-Helper`를 붙일 것**

## 릴리즈 수행

1. **csproj 버전 범프**: `TarkovHelper/TarkovHelper.csproj`의 `<Version>`,
   `<AssemblyVersion>`, `<FileVersion>`을 모두 `$ARGUMENTS`로 변경.
   **update.xml은 아직 건드리지 않습니다** (마지막 단계에서 범프)
2. **커밋**: `git commit -am "chore(release): bump version to $ARGUMENTS"`
3. **태그 + push**: `git tag v$ARGUMENTS` 후 `git push origin main v$ARGUMENTS`
   - 반드시 이 태그 **하나만** push. **`git push --tags` 절대 금지** — 로컬에는
     upstream 시절 레거시 태그 26개(v0.9.0~v4.2.1)가 있으며 push하지 않기로 결정됨
4. **워크플로 대기**: 약 15초 후
   `gh run list -R josephjang/Tarkov-Item-Helper --workflow release.yml --limit 1 --json databaseId`
   로 run ID 확인 →
   `gh run watch <id> --exit-status -R josephjang/Tarkov-Item-Helper`
5. **릴리즈 노트 큐레이션** (워크플로 성공 시):
   - 이전 태그 확인: `git describe --tags --abbrev=0 "v$ARGUMENTS^"`
   - 커밋 로그 확인: `git log <이전태그>..v$ARGUMENTS --oneline`
   - 아래 형식으로 영어/한국어 노트를 scratchpad 임시 파일에 작성 후:
     `gh release edit v$ARGUMENTS -R josephjang/Tarkov-Item-Helper --notes-file <파일>`
6. **자산 확인**:
   `gh release view v$ARGUMENTS -R josephjang/Tarkov-Item-Helper --json assets`
   결과에 `TarkovHelper.zip`이 있어야 합니다
7. **update.xml 범프 (자산 확인 후에만!)**:
   - `<version>` → `$ARGUMENTS`
   - `<url>` → `https://github.com/josephjang/Tarkov-Item-Helper/releases/download/v$ARGUMENTS/TarkovHelper.zip`
   - `git commit -am "chore(release): point update.xml at v$ARGUMENTS"` 후
     `git push origin main`
   - 이 순서 덕분에 클라이언트(3분마다 raw main의 update.xml 폴링)는 다운로드
     자산이 실제로 존재한 뒤에만 새 버전을 보게 됩니다 (404 창 제로)

## Release Notes 형식

```markdown
## What's Changed / 변경 사항

### English
- [Feature/Fix/Update description]
- ...

### 한국어
- [기능/수정/업데이트 설명]
- ...

---
**Full Changelog**: https://github.com/josephjang/Tarkov-Item-Helper/compare/[이전태그]...v$ARGUMENTS
```

커밋 메시지 패턴에 따른 분류:
- `feat:` → New feature / 새로운 기능
- `fix:` → Bug fix / 버그 수정
- `DB Update` → Database update / 데이터베이스 업데이트
- `refactor:` → Code refactoring / 코드 리팩토링
- `chore:` → Maintenance / 유지보수 (일반적으로 Release Notes에서 생략)
- `Merge PR` → PR 제목에서 기능 추출

## 실패 복구 (워크플로 red)

update.xml을 아직 범프하지 않았으므로 어떤 클라이언트도 깨진 버전을 보지 못했습니다.
복구는 안전하며 외부에서 보이지 않습니다:

1. 부분 생성된 릴리즈가 있으면:
   `gh release delete v$ARGUMENTS -y -R josephjang/Tarkov-Item-Helper`
2. 태그 삭제: `git tag -d v$ARGUMENTS` 후
   `git push origin :refs/tags/v$ARGUMENTS`
3. main에서 원인 수정 (일반 커밋) 후 위 "릴리즈 수행" 3단계부터 **같은 버전으로**
   재시도

## 주의사항

- 로컬에서 패키지를 확인하고 싶으면 `./build/Create-ReleasePackage.ps1` 실행
  (CI와 동일한 스크립트; 결과물은 `artifacts/TarkovHelper.zip`)
- gh CLI 경로가 PATH에 없을 수 있음: `C:\Program Files\GitHub CLI\gh.exe`
- update.xml 수정 시 XML 태그가 누락되지 않도록 주의 (닫는 태그 필수).
  `UpdateXmlTests`가 형식·포크 URL·버전-URL 일치를 검증하므로 수정 후
  `dotnet test --filter UpdateXmlTests` 로 확인 가능

위 작업들을 순서대로 실행해주세요. 각 단계마다 결과를 확인하고 진행하세요.
