# Quest Sync Enhancement PRD (퀘스트 동기화 개선)

## Overview
퀘스트 로그 동기화 기능의 UX 개선. 계정 초기화(와이프) 시 로그 정리 안내와 동기화 결과에서 진행중 퀘스트 표시 기능 추가. (영어, 한국어, 일본어 언어 설정따라서 안내가 나와야함)

## Reference
- 연계 문서: `PRDs/quest-sync.md`
- 기존 서비스: `LogSyncService.cs`, `QuestProgressService.cs`

---

## Features

### 1. 계정 초기화 안내 팝업

#### 1.1 트리거 조건
설정 메뉴에서 "퀘스트 동기화" 버튼 클릭 시, 동기화 실행 전 안내 팝업을 먼저 표시한다.

#### 1.2 UI 디자인

```
+------------------------------------------------------------------+
| ⚠️ 퀘스트 동기화 전 확인                                          |
+------------------------------------------------------------------+
|                                                                   |
| 최근 계정 초기화(와이프)를 진행하셨나요?                           |
|                                                                   |
| 계정 초기화 후 동기화를 진행하면 이전 시즌의 로그가 섞여            |
| 퀘스트 진행 상태가 올바르지 않게 표시될 수 있습니다.                |
|                                                                   |
| 📁 로그 폴더 위치:                                                 |
| C:\Users\...\Battlestate Games\EFT\Logs                           |
|                                                                   |
| 💡 권장 조치:                                                      |
| 계정 초기화 이전 날짜의 로그 폴더를 삭제하거나                      |
| 다른 위치로 백업해 주세요.                                         |
|                                                                   |
|                      [폴더 열기]  [계속 진행]                       |
+------------------------------------------------------------------+
```

#### 1.3 버튼 동작

| 버튼 | 동작 |
|------|------|
| 폴더 열기 | 로그 폴더를 Windows 탐색기로 열기 (`Process.Start("explorer.exe", logPath)`) |
| 계속 진행 | 팝업 닫고 동기화 실행 |

#### 1.4 "다시 보지 않기" 옵션

```
+------------------------------------------------------------------+
| [✓] 이 안내를 다시 보지 않기                                       |
+------------------------------------------------------------------+
```

- 체크 시 `settings.json`에 `"hideWipeWarning": true` 저장
- 설정 페이지에서 다시 활성화 가능

#### 1.5 다국어 지원

**한국어:**
```
최근 계정 초기화(와이프)를 진행하셨나요?
```

**English:**
```
Have you recently reset your account (wipe)?
```

**日本語:**
```
最近アカウントをリセット（ワイプ）しましたか？
```

---

### 2. 진행중 퀘스트 표시 기능

#### 2.1 동기화 결과 화면 개선

기존 동기화 결과 다이얼로그를 좌우 2컬럼 레이아웃으로 변경한다.

```
+------------------------------------------------------------------+
| 퀘스트 동기화 완료                                                 |
+------------------------------------------------------------------+
|  완료된 퀘스트 (35)          |  진행중 퀘스트 (8)                  |
| ----------------------------|-------------------------------------|
| ✓ Debut                     | ● Delivery from the Past            |
| ✓ Checking                  | ● The Punisher - Part 4             |
| ✓ Shootout Picnic           | ● Colleagues - Part 3               |
| ✓ Acquaintance              | ● Gratitude                         |
| ✓ Supplier                  | ● Setup                             |
| ✓ The Extortionist          | ● Insomnia                          |
| ✓ Stirrup                   | ● Test Drive - Part 1               |
| ✓ What's on the Flash Drive | ● The Guide                         |
| ... (더보기)                 |                                     |
|                             |                                     |
| 자동 완료된 선행 퀘스트: 23  |                                     |
| 매칭 실패: 3                 |                                     |
+------------------------------------------------------------------+
|                        [확인]                                      |
+------------------------------------------------------------------+
```

#### 2.2 진행중 퀘스트 정의

**진행중(In Progress) 상태:**
- 로그에서 `message.type == 10` (Started) 이벤트가 발견됨
- 해당 퀘스트의 `message.type == 12` (Completed) 또는 `message.type == 11` (Failed) 이벤트가 없음

```csharp
public class SyncResult
{
    // 기존 필드
    public int TotalEventsFound { get; set; }
    public int QuestsCompleted { get; set; }
    public int QuestsFailed { get; set; }
    public int PrerequisitesAutoCompleted { get; set; }
    public List<string> UnmatchedQuestIds { get; set; }

    // 신규 필드
    public List<TarkovTask> InProgressQuests { get; set; }  // 진행중 퀘스트 목록
    public List<TarkovTask> CompletedQuests { get; set; }   // 완료된 퀘스트 목록
}
```

#### 2.3 진행중 퀘스트 처리 로직

```csharp
public void ProcessQuestEvents(List<QuestLogEvent> events)
{
    // 퀘스트별로 이벤트 그룹화
    var questEvents = events
        .GroupBy(e => e.QuestId)
        .ToDictionary(g => g.Key, g => g.ToList());

    var inProgressQuests = new List<TarkovTask>();
    var completedQuests = new List<TarkovTask>();

    foreach (var (questId, questEventList) in questEvents)
    {
        var task = FindTaskByQuestId(questId);
        if (task == null) continue;

        // 최신 이벤트 기준으로 상태 판단
        var latestEvent = questEventList.OrderByDescending(e => e.Timestamp).First();

        switch (latestEvent.EventType)
        {
            case QuestEventType.Started:
                // 시작만 됨, 완료/실패 이벤트 없음 = 진행중
                inProgressQuests.Add(task);
                _progressService.SetQuestStatus(task.NormalizedName, QuestStatus.Active);
                break;

            case QuestEventType.Completed:
                completedQuests.Add(task);
                _progressService.SetQuestStatus(task.NormalizedName, QuestStatus.Done);
                break;

            case QuestEventType.Failed:
                _progressService.SetQuestStatus(task.NormalizedName, QuestStatus.Failed);
                break;
        }
    }

    return new SyncResult
    {
        InProgressQuests = inProgressQuests,
        CompletedQuests = completedQuests,
        // ...
    };
}
```

#### 2.4 진행중 퀘스트 UI 동작

- 클릭 시 해당 퀘스트 상세 페이지로 이동
- 마우스 오버 시 트레이더, 필요 레벨 등 툴팁 표시
- 목록이 길면 스크롤 가능한 리스트로 표시

---

### 3. Settings 저장 구조

```json
{
  "logSyncSettings": {
    "logDirectory": "C:\\Users\\...\\Battlestate Games\\EFT\\Logs",
    "autoSyncOnStart": true,
    "hideWipeWarning": false,
    "lastSyncTimestamp": "2025-12-02T08:00:00Z"
  }
}
```

---

## Implementation Steps

### Phase 1: 계정 초기화 안내 팝업
1. 안내 다이얼로그 XAML 생성
2. "폴더 열기" 버튼 구현 (`Process.Start`)
3. "다시 보지 않기" 설정 저장/로드
4. 다국어 리소스 추가

### Phase 2: 진행중 퀘스트 표시
1. `SyncResult`에 `InProgressQuests`, `CompletedQuests` 필드 추가
2. `ProcessQuestEvents` 로직 수정
3. 동기화 결과 다이얼로그 2컬럼 레이아웃으로 변경
4. 퀘스트 목록 스크롤/클릭 동작 구현

### Phase 3: 통합 테스트
1. 와이프 시나리오 테스트 (이전 로그 + 새 로그)
2. 다양한 퀘스트 상태 조합 테스트
3. UI 레이아웃 검증

---

## UI Mockups

### 3.1 동기화 결과 다이얼로그 상세

```
+------------------------------------------------------------------+
| 퀘스트 동기화 완료                                    [X]          |
+------------------------------------------------------------------+
|                                                                   |
|  ┌─────────────────────────┐  ┌─────────────────────────────────┐ |
|  │ 완료된 퀘스트 (35)      │  │ 진행중 퀘스트 (8)               │ |
|  ├─────────────────────────┤  ├─────────────────────────────────┤ |
|  │ ✓ Debut                 │  │ ● Delivery from the Past        │ |
|  │   프라퍼                │  │   테라피스트                    │ |
|  │                         │  │                                 │ |
|  │ ✓ Checking              │  │ ● The Punisher - Part 4         │ |
|  │   프라퍼                │  │   프라퍼                        │ |
|  │                         │  │                                 │ |
|  │ ✓ Shootout Picnic       │  │ ● Colleagues - Part 3           │ |
|  │   프라퍼                │  │   테라피스트                    │ |
|  │                         │  │                                 │ |
|  │ [스크롤 가능...]        │  │                                 │ |
|  └─────────────────────────┘  └─────────────────────────────────┘ |
|                                                                   |
|  요약:                                                            |
|  ├─ 로그에서 발견된 이벤트: 86                                    |
|  ├─ 자동 완료된 선행 퀘스트: 23                                   |
|  └─ 매칭 실패한 퀘스트 ID: 3                                      |
|                                                                   |
+------------------------------------------------------------------+
|                           [확인]                                   |
+------------------------------------------------------------------+
```

### 3.2 진행중 퀘스트 툴팁

```
+----------------------------------+
| Delivery from the Past           |
| 과거로부터의 배달                 |
|----------------------------------|
| 트레이더: 테라피스트              |
| 필요 레벨: 17                     |
| Kappa 필수: Yes                   |
|----------------------------------|
| 목표: Factory와 Customs에서       |
| 서류가방 찾기                     |
+----------------------------------+
```

---

## Error Handling

### 로그 폴더 접근 오류
```csharp
try
{
    Process.Start("explorer.exe", logDirectory);
}
catch (Exception ex)
{
    // 폴더 열기 실패 시 경로를 클립보드에 복사
    Clipboard.SetText(logDirectory);
    ShowMessage("폴더를 열 수 없습니다. 경로가 클립보드에 복사되었습니다.");
}
```

### 빈 로그 폴더
- 로그 파일이 없으면 "로그 파일을 찾을 수 없습니다" 메시지 표시
- 게임을 한 번 이상 실행해야 로그가 생성됨을 안내

---

## Testing

### Test Cases
1. 계정 초기화 안내 팝업 표시/숨김
2. "다시 보지 않기" 설정 저장/로드
3. "폴더 열기" 버튼 동작
4. 진행중 퀘스트 정확히 분류되는지
5. 동일 퀘스트의 시작→완료 이벤트 순서 처리
6. 동일 퀘스트의 시작→실패→재시작 이벤트 처리
7. 2컬럼 레이아웃 반응형 동작
