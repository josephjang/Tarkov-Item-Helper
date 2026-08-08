# PRD: Quest HTML Entity Encoding Duplicate Fix

## 문제 요약

Wiki HTML 페이지에서 퀘스트 이름을 파싱할 때 HTML 엔티티(`&#39;` 등)가 디코딩되지 않아 같은 퀘스트가 중복으로 표시되는 문제가 발생합니다.

## 중복 발생 사례

### 영향받는 퀘스트 목록 (9개)

| 퀘스트 이름 (Wiki) | 실제 이름 |
|------------------|----------|
| `Pets Won&#39;t Need It - Part 1` | Pets Won't Need It - Part 1 |
| `Pets Won&#39;t Need It - Part 2` | Pets Won't Need It - Part 2 |
| `You&#39;ve Got Mail` | You've Got Mail |
| `Gunsmith - Old Friend&#39;s Request` | Gunsmith - Old Friend's Request |
| `Developer&#39;s Secrets - Part 1` | Developer's Secrets - Part 1 |
| `Developer&#39;s Secrets - Part 2` | Developer's Secrets - Part 2 |
| `Forester&#39;s Duty` | Forester's Duty |
| `Keeper&#39;s Word` | Keeper's Word |
| `Hot Wheels - Let&#39;s Try Again` | Hot Wheels - Let's Try Again |

## 원인 분석

### 1. 데이터 소스별 표현 차이

```
[Wiki HTML 페이지]
  data-tpt-row-id="Pets Won&#39;t Need It - Part 1"  ← HTML 엔티티 인코딩됨
                        ↓
[WikiDataService.ParseQuestsFromHtml]
  FixMojibake()는 double-encoded UTF-8만 처리, HTML 엔티티는 처리 안함
                        ↓
[quests_by_trader.json]
  name: "Pets Won&#39;t Need It - Part 1"  ← 그대로 저장됨
  wikiPath: "/wiki/Pets_Won%26%2339%3Bt_Need_It_-_Part_1"  ← 이중 인코딩 발생!
```

```
[tarkov.dev API]
  name: "Pets Won't Need It - Part 1"  ← 정상 apostrophe
                        ↓
[TarkovDataService.FetchAndMergeTasksAsync]
  matchedWikiQuests.Add("Pets Won't Need It - Part 1")  ← 정상 이름으로 저장
                        ↓
[Wiki-only 퀘스트 추가 단계]
  quest.Name = "Pets Won&#39;t Need It - Part 1"
  matchedWikiQuests.Contains(quest.Name) → FALSE  ← 불일치로 중복 추가!
```

### 2. 핵심 문제 코드 위치

**파일**: `WikiDataService.cs:131-148`
```csharp
public WikiQuestsByTrader ParseQuestsFromHtml(string html)
{
    // ...
    var questPattern = @"data-tpt-row-id=""([^""]+)""";
    // ...
    var questName = FixMojibake(match.Groups[1].Value);
    // ❌ HTML 엔티티 디코딩 누락
    // ...
    quests.Add(new WikiQuest
    {
        Name = questName,  // ← HTML 엔티티가 그대로 저장됨
        WikiPath = $"/wiki/{Uri.EscapeDataString(questName.Replace(" ", "_"))}"
        // ← &#39;가 %26%2339%3B로 이중 인코딩됨
    });
}
```

**파일**: `TarkovDataService.cs:359-361`
```csharp
// Skip if already matched with API task
if (matchedWikiQuests.Contains(quest.Name))  // ← HTML 엔티티로 인해 불일치
    continue;
```

### 3. 영향 범위

1. **quests_by_trader.json**: 잘못된 이름과 잘못된 wikiPath 저장
2. **Wiki 페이지 다운로드**: 잘못된 파일명으로 저장 가능성
3. **tasks.json**: 같은 퀘스트가 API 버전과 Wiki-only 버전으로 중복 저장
4. **UI 표시**: 동일 퀘스트가 2번 표시됨

## 해결 방안

### 방안 1: 파싱 단계에서 HTML 엔티티 디코딩 (권장)

**수정 위치**: `WikiDataService.cs`

```csharp
using System.Net;

public WikiQuestsByTrader ParseQuestsFromHtml(string html)
{
    // ...
    foreach (Match match in matches)
    {
        var questName = FixMojibake(match.Groups[1].Value);

        // 🆕 HTML 엔티티 디코딩 추가
        questName = WebUtility.HtmlDecode(questName);

        if (!seen.Contains(questName))
        {
            seen.Add(questName);
            quests.Add(new WikiQuest
            {
                Name = questName,
                WikiPath = $"/wiki/{Uri.EscapeDataString(questName.Replace(" ", "_"))}"
            });
        }
    }
    // ...
}
```

**장점**:
- 근본 원인을 해결
- 한 곳만 수정하면 됨
- 모든 HTML 엔티티 처리 (`&#39;`, `&amp;`, `&quot;` 등)

**단점**:
- 기존 캐시 파일 재생성 필요

### 방안 2: 매칭 단계에서 정규화 비교

**수정 위치**: `TarkovDataService.cs`

```csharp
// matchedWikiQuests 저장 시
matchedWikiQuests.Add(WebUtility.HtmlDecode(wikiMatchName));

// Wiki-only 추가 시
if (matchedWikiQuests.Contains(WebUtility.HtmlDecode(quest.Name)))
    continue;
```

**장점**:
- 기존 캐시 호환

**단점**:
- 여러 곳 수정 필요
- 근본 원인 해결 안됨

## 권장 구현 계획

### Phase 1: HTML 엔티티 디코딩 추가

1. `WikiDataService.ParseQuestsFromHtml`에 `WebUtility.HtmlDecode` 추가
2. wikiPath 생성 로직도 디코딩된 이름 사용하도록 수정

### Phase 2: 캐시 갱신

1. 기존 `quests_by_trader.json` 삭제
2. `--fetch` 명령으로 Wiki 데이터 새로 다운로드
3. Quest 페이지 캐시는 파일명 기준이므로 별도 처리 필요

### Phase 3: 파일명 매칭 개선 (선택사항)

`TarkovDataService.GetWikiFilePath`에서 HTML 엔티티 인코딩된 파일명도 탐색하도록 개선
(현재 lines 165-169에 이미 존재하나, 역방향 디코딩도 추가)

## 테스트 계획

1. **단위 테스트**: `ParseQuestsFromHtml`에 HTML 엔티티 포함 퀘스트명 테스트
2. **통합 테스트**: `--fetch-tasks` 후 중복 퀘스트 없는지 확인
3. **회귀 테스트**: 기존 퀘스트들이 정상 동작하는지 확인

### 테스트 케이스

```csharp
[TestMethod]
public void ParseQuestsFromHtml_DecodesHtmlEntities()
{
    var html = @"<table id=""tpt-2""><input data-tpt-row-id=""Pets Won&#39;t Need It - Part 1"" /></table>";
    var result = service.ParseQuestsFromHtml(html);
    Assert.AreEqual("Pets Won't Need It - Part 1", result["Therapist"][0].Name);
}
```

## 영향도 분석

| 영역 | 영향도 | 설명 |
|-----|--------|-----|
| 퀘스트 목록 | 높음 | 9개 퀘스트 중복 해결 |
| 퀘스트 진행 추적 | 중간 | normalizedName은 정상 작동 |
| Wiki 페이지 링크 | 높음 | wikiPath 이중 인코딩 해결 |
| API 매칭 | 중간 | 불필요한 wiki-only 퀘스트 제거 |

## 추가 발견 사항

### wikiPath 이중 인코딩 문제

현재 `&#39;`가 URL 인코딩되어 `%26%2339%3B`로 변환됨:
```
잘못된 경로: /wiki/Pets_Won%26%2339%3Bt_Need_It_-_Part_1
올바른 경로: /wiki/Pets_Won%27t_Need_It_-_Part_1
```

이로 인해 Wiki 링크가 올바르게 작동하지 않을 수 있음.

---

**작성일**: 2025-12-03
**작성자**: Claude Code
**상태**: 분석 완료, 구현 대기
