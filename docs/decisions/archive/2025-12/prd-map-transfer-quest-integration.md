# PRD: Map Transfer Quest Integration (v2)

## 개요

Map Transfer 기능을 통해 Tarkov Market API에서 마커를 가져와 **참조용 테이블(ApiMarkers)**에 저장합니다.
이 참조 마커는 Map Preview, Quest Requirement Validator에서 BSG ID/퀘스트명/Objective명으로 연관 관계를 찾아 표시되며,
사용자가 위치를 확인 후 실제 데이터(QuestObjectives, MapMarkers)에 반영할지 결정합니다.

## 핵심 개념

### 테이블 역할 분리

```
┌──────────────────────────────────────────────────────────────────┐
│  ApiMarkers (신규)                                               │
│  - Tarkov Market API에서 가져온 참조용 마커                       │
│  - 직접 사용되지 않음, 참조/비교용                                │
│  - BSG ID, 퀘스트명, Objective명 저장                            │
└──────────────────────────────────────────────────────────────────┘
                    ↓ 연관 관계 (런타임 매칭)
┌──────────────────────────────────────────────────────────────────┐
│  QuestObjectives (기존)          │  MapMarkers (기존)            │
│  - 퀘스트 목표의 실제 위치        │  - 스폰/탈출구 등 실제 마커    │
│  - LocationPoints 필드           │  - X, Y, Z 좌표               │
│  - 사용자가 승인한 데이터         │  - 사용자가 승인한 데이터      │
└──────────────────────────────────────────────────────────────────┘
```

### 워크플로우

```
1. Map Transfer
   └→ API 마커 로드 → ApiMarkers 테이블에 저장

2. Quest Requirement Validator / Map Preview
   └→ 퀘스트 목표 선택
   └→ BSG ID / 퀘스트명 / Objective명으로 ApiMarkers 검색
   └→ 매칭되는 API 마커를 참조용으로 표시 (다른 색상)

3. 사용자 판단
   ├→ "위치 맞음" → API 마커 좌표를 QuestObjectives.LocationPoints에 복사
   ├→ "위치 틀림" → 수동으로 마커 수정
   └→ "추가 필요" → 수동으로 마커 추가 (폴리곤/영역 표시)
```

---

## 요구사항 재정의

### 1. Map Transfer에서 마커 구분
- **현재**: DB 마커(녹색), API 마커(파란색)로 시각적 구분 완료
- **변경 없음**

### 2. Map Transfer에서 API 마커의 퀘스트/Objective 정보 표시
- API 마커에 연결된 퀘스트명, Objective 정보 표시
- ToolTip 또는 선택 시 상세 패널에 표시

### 3. ApiMarkers 테이블에 저장
- **기존 MapMarkers 테이블 수정 없음**
- 새로운 `ApiMarkers` 테이블 생성
- BSG ID, 퀘스트명(EN), Objective명 저장하여 연관 관계 검색 가능

### 4. Map Preview / Quest Requirement Validator에서 참조 마커 표시
- 퀘스트 목표 선택 시 연관된 API 마커를 참조용으로 표시
- 실제 마커와 다른 스타일 (점선 테두리, 반투명 등)

### 5. 참조 마커 위치 적용 기능
- 사용자가 "위치 적용" 클릭 시 API 마커 좌표를 실제 데이터에 복사
- QuestObjectives.LocationPoints 또는 MapMarkers에 반영

### 6. 퀘스트 매칭 로직
- **tarkov.dev API 사용 안 함**
- DB `Quests` 테이블과 매칭
- BSG ID 우선: Tarkov Market `bsgId` ↔ DB `Quests.BsgId`
- Fallback: Tarkov Market `name_l10n.en` ↔ DB `Quests.NameEN`
- Objective는 Description 텍스트로 유사도 매칭

---

## 기술 설계

### 신규 테이블: ApiMarkers

```sql
CREATE TABLE ApiMarkers (
    Id TEXT PRIMARY KEY,              -- GUID
    TarkovMarketUid TEXT NOT NULL,    -- API marker uid (중복 방지)

    -- 마커 기본 정보
    Name TEXT NOT NULL,               -- 마커명 (EN)
    NameKo TEXT,                      -- 마커명 (KO)
    Category TEXT NOT NULL,           -- Extractions, Spawns, Quests, etc.
    SubCategory TEXT,                 -- PMC Extraction, Quest Objective, etc.

    -- 위치 정보
    MapKey TEXT NOT NULL,             -- Customs, Woods, etc.
    X REAL NOT NULL,                  -- 게임 X 좌표 (변환 후)
    Y REAL,                           -- 게임 Y 좌표 (높이)
    Z REAL NOT NULL,                  -- 게임 Z 좌표 (변환 후)
    FloorId TEXT,                     -- 층 ID

    -- 퀘스트 연관 정보 (DB Quests 테이블과 매칭용)
    QuestBsgId TEXT,                  -- BSG ID (DB Quests.BsgId와 매칭)
    QuestNameEn TEXT,                 -- 퀘스트명 EN (DB Quests.NameEN과 fallback 매칭)
    ObjectiveDescription TEXT,        -- Objective 설명 (DB QuestObjectives.Description과 매칭)

    -- 메타 정보
    ImportedAt TEXT NOT NULL,         -- import 시점

    UNIQUE(TarkovMarketUid)           -- 중복 import 방지
);

-- 인덱스
CREATE INDEX idx_apimarkers_mapkey ON ApiMarkers(MapKey);
CREATE INDEX idx_apimarkers_bsgid ON ApiMarkers(QuestBsgId);
CREATE INDEX idx_apimarkers_questname ON ApiMarkers(QuestNameEn);
```

### 기존 테이블 변경: 없음

- `MapMarkers` 테이블: 변경 없음
- `QuestObjectives` 테이블: 변경 없음

### 연관 관계 매칭 로직

**DB Quests 테이블과 매칭 (tarkov.dev 사용 안 함)**

```csharp
public class ApiMarkerMatcher
{
    private readonly ApiMarkerService _apiMarkerService;

    /// <summary>
    /// DB 퀘스트 목표에 매칭되는 API 참조 마커 검색
    /// </summary>
    public List<ApiMarker> FindMatchingMarkers(QuestObjective objective, DbQuest quest)
    {
        var markers = new List<ApiMarker>();

        // 1. BSG ID로 매칭 (가장 정확)
        // Tarkov Market bsgId ↔ DB Quests.BsgId
        if (!string.IsNullOrEmpty(quest.BsgId))
        {
            markers = _apiMarkerService.GetByQuestBsgId(quest.BsgId);
        }

        // 2. BSG ID 없거나 매칭 실패 시 퀘스트명으로 매칭
        // Tarkov Market name_l10n.en ↔ DB Quests.NameEN
        if (markers.Count == 0 && !string.IsNullOrEmpty(quest.NameEN))
        {
            markers = _apiMarkerService.GetByQuestName(quest.NameEN);
        }

        // 3. Objective Description으로 필터링
        // Tarkov Market marker.name ↔ DB QuestObjectives.Description
        if (!string.IsNullOrEmpty(objective.Description) && markers.Count > 0)
        {
            markers = markers.Where(m =>
                !string.IsNullOrEmpty(m.ObjectiveDescription) &&
                CalculateSimilarity(m.ObjectiveDescription, objective.Description) > 0.7
            ).ToList();
        }

        return markers;
    }

    private double CalculateSimilarity(string a, string b)
    {
        // Levenshtein distance 또는 Contains 기반 유사도 계산
        if (a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
            b.Contains(a, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // TODO: 더 정교한 유사도 알고리즘 필요 시 구현
        return 0;
    }
}
```

---

## 구현 계획

### Phase 1: ApiMarkers 테이블 및 서비스

#### 1.1 ApiMarker 모델 생성
**파일**: `TarkovDBEditor/Models/ApiMarker.cs`

```csharp
public class ApiMarker
{
    public string Id { get; set; } = "";
    public string TarkovMarketUid { get; set; } = "";

    // 마커 기본 정보
    public string Name { get; set; } = "";
    public string? NameKo { get; set; }
    public string Category { get; set; } = "";
    public string? SubCategory { get; set; }

    // 위치 정보
    public string MapKey { get; set; } = "";
    public double X { get; set; }
    public double? Y { get; set; }
    public double Z { get; set; }
    public string? FloorId { get; set; }

    // 퀘스트 연관 정보
    public string? QuestBsgId { get; set; }
    public string? QuestNameEn { get; set; }
    public string? ObjectiveDescription { get; set; }

    // 메타 정보
    public DateTime ImportedAt { get; set; }
}
```

#### 1.2 ApiMarkerService 생성
**파일**: `TarkovDBEditor/Services/ApiMarkerService.cs`

- `EnsureTableExistsAsync()` - 테이블 생성
- `SaveMarkersAsync(List<ApiMarker>)` - 마커 일괄 저장 (UPSERT)
- `GetByMapKeyAsync(string mapKey)` - 맵별 마커 조회
- `GetByQuestBsgIdAsync(string bsgId)` - BSG ID로 조회
- `GetByQuestNameAsync(string questName)` - 퀘스트명으로 조회
- `DeleteByMapKeyAsync(string mapKey)` - 맵별 마커 삭제 (재import용)

### Phase 2: Tarkov Market Quest API 연동

#### 2.1 TarkovMarketQuest 모델 추가
**파일**: `TarkovDBEditor/Models/TarkovMarketModels.cs`

```csharp
public class TarkovMarketQuest
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("bsgId")]
    public string? BsgId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enObjectives")]
    public List<string>? EnObjectives { get; set; }

    [JsonPropertyName("name_l10n")]
    public Dictionary<string, string>? NameL10n { get; set; }
}
```

#### 2.2 Map Transfer에서 퀘스트 정보 연결
- API에서 markers/list + quests/list 호출
- marker.questUid → quest.uid 매칭 → quest.bsgId 획득
- ApiMarkers 저장 시 QuestBsgId, QuestNameEn, ObjectiveDescription 포함

### Phase 3: Map Transfer UI 개선

#### 3.1 퀘스트 정보 표시
- 마커 선택 시 하단 패널에 퀘스트/Objective 정보 표시
- ToolTip에 퀘스트명 추가

#### 3.2 DB 저장 로직 변경
- 기존: MapMarkers 테이블에 저장
- 변경: ApiMarkers 테이블에 저장

```csharp
private async Task ImportSelectedMarkersAsync()
{
    var apiMarkers = _selectedApiMarkers.Select(m => new ApiMarker
    {
        Id = Guid.NewGuid().ToString(),
        TarkovMarketUid = m.Uid,
        Name = m.Name,
        NameKo = m.NameL10n?.GetValueOrDefault("ko"),
        Category = m.Category,
        SubCategory = m.SubCategory,
        MapKey = _currentMapKey,
        X = m.GameX ?? 0,
        Z = m.GameZ ?? 0,
        FloorId = m.FloorId,
        QuestBsgId = GetQuestBsgId(m.QuestUid),      // 퀘스트 매칭
        QuestNameEn = GetQuestNameEn(m.QuestUid),
        ObjectiveDescription = m.Name,               // 마커명을 Objective로 사용
        ImportedAt = DateTime.UtcNow
    }).ToList();

    await _apiMarkerService.SaveMarkersAsync(apiMarkers);
}
```

### Phase 4: Quest Requirement Validator 개선

#### 4.1 참조 마커 표시
- 퀘스트 목표 선택 시 ApiMarkers에서 매칭 검색
- 매칭된 마커를 Map Preview에 참조용으로 표시 (반투명/점선)

#### 4.2 위치 적용 기능
```csharp
private async Task ApplyApiMarkerLocation(ApiMarker apiMarker, QuestObjective objective)
{
    // API 마커 좌표를 QuestObjectives.LocationPoints에 추가
    var point = new LocationPoint
    {
        X = apiMarker.X,
        Y = apiMarker.Y ?? 0,
        Z = apiMarker.Z,
        FloorId = apiMarker.FloorId
    };

    objective.LocationPoints ??= new List<LocationPoint>();
    objective.LocationPoints.Add(point);

    await _questService.UpdateObjectiveAsync(objective);
}
```

#### 4.3 UI 변경
```xml
<!-- 참조 마커 패널 -->
<GroupBox Header="API 참조 마커" Margin="0,10,0,0">
    <StackPanel>
        <TextBlock Text="매칭된 API 마커:" Foreground="#888"/>
        <ListBox x:Name="ApiMarkerList" MaxHeight="150">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding Name}" Foreground="#2196F3"/>
                        <TextBlock Text=" - " Foreground="#888"/>
                        <TextBlock Text="{Binding MapKey}" Foreground="#888"/>
                        <Button Content="위치 적용" Margin="10,0,0,0"
                                Click="ApplyApiMarkerLocation_Click"/>
                    </StackPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </StackPanel>
</GroupBox>
```

### Phase 5: Map Preview 개선

#### 5.1 참조 마커 레이어 추가
- 기존 마커와 별도 레이어로 API 참조 마커 표시
- 반투명 + 점선 테두리로 구분

---

## 구현 순서

### Step 1: 기반 작업
1. [ ] ApiMarker 모델 생성
2. [ ] ApiMarkerService 생성 (테이블 생성, CRUD)
3. [ ] TarkovMarketQuest 모델 추가

### Step 2: Map Transfer 수정
4. [ ] quests/list API 호출 추가
5. [ ] 마커-퀘스트 연결 로직 (questUid → bsgId)
6. [ ] DB 저장 대상을 ApiMarkers로 변경
7. [ ] 퀘스트 정보 UI 표시

### Step 3: Quest Requirement Validator 개선
8. [ ] ApiMarkerMatcher 클래스 구현
9. [ ] 퀘스트 목표 선택 시 매칭 마커 검색
10. [ ] 참조 마커 UI 표시
11. [ ] "위치 적용" 기능 구현

### Step 4: Map Preview 개선
12. [ ] 참조 마커 레이어 추가
13. [ ] 참조 마커 스타일 (반투명/점선)

---

## 유의사항

### API JSON 응답 null/빈 문자열 처리

Tarkov Market API 응답의 여러 필드가 null, 빈 문자열, 또는 예상과 다른 타입일 수 있습니다.

#### 기존 구현된 컨버터 (`TarkovMarketModels.cs`)
```csharp
// int? 필드: null, 숫자, 문자열 모두 처리
public class FlexibleIntConverter : JsonConverter<int?>

// double 필드: null, 숫자, 문자열 모두 처리
public class FlexibleDoubleConverter : JsonConverter<double>
```

#### 구현 시 필수 체크 포인트
```csharp
// 1. Geometry null 체크
if (marker.Geometry == null) continue;

// 2. questUid 빈 문자열 체크
var questUid = marker.QuestUid;
if (string.IsNullOrEmpty(questUid)) questUid = null;

// 3. bsgId null/빈 문자열 체크
var bsgId = marketQuest?.BsgId;
if (string.IsNullOrWhiteSpace(bsgId)) bsgId = null;

// 4. Dictionary 접근 시 null 체크
var enName = marketQuest?.NameL10n?.GetValueOrDefault("en")
          ?? marketQuest?.Name;

// 5. List 접근 시 null 체크
var objectives = marketQuest?.EnObjectives ?? new List<string>();
```

### 데이터 분리 원칙

- **ApiMarkers**: 참조용 데이터, 직접 사용 안 함
- **QuestObjectives**: 실제 사용 데이터, 사용자 승인 필요
- **MapMarkers**: 실제 사용 데이터, 사용자 승인 필요
- 두 테이블 간 직접 FK 관계 없음, 런타임 매칭만 사용

### 매칭 우선순위 (DB Quests 테이블과 매칭)

1. **BSG ID 매칭** (가장 정확): Tarkov Market `bsgId` ↔ DB `Quests.BsgId`
2. **EN 퀘스트명 매칭** (fallback): Tarkov Market `name_l10n.en` ↔ DB `Quests.NameEN`
3. **Objective 필터링**: Tarkov Market `marker.name` ↔ DB `QuestObjectives.Description`

---

## 테스트 계획

1. **ApiMarkers 테이블 테스트**
   - 테이블 생성 확인
   - UPSERT 동작 (중복 import 방지)

2. **퀘스트 매칭 테스트**
   - BSG ID 매칭 정확도
   - EN 이름 fallback 동작
   - Objective 유사도 매칭

3. **UI 테스트**
   - Map Transfer: 퀘스트 정보 표시, ApiMarkers 저장
   - Quest Validator: 참조 마커 표시, 위치 적용
   - Map Preview: 참조 마커 레이어

---

## 참고 자료

- [Tarkov Market API 문서](./tarkov-market-markers-api.md)
- DB Quests 테이블: `TarkovDBEditor/Services/RefreshDataService.cs`
- QuestObjective 모델: `TarkovDBEditor/Models/QuestObjectiveItem.cs`
- MapMarker 모델: `TarkovDBEditor/Models/MapMarker.cs`
