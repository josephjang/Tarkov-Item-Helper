# PRD: Map Transfer 기능

## 1. 개요

### 1.1 목적
Tarkov Market API의 맵 마커 데이터를 우리 DB(MapMarkers 테이블)로 가져오는 도구를 제공합니다. 기존에 직접 찍은 탈출구, 트랜짓, 퀘스트 마커의 좌표를 참조점으로 활용하여 API 마커의 SVG 좌표를 게임 좌표로 변환합니다.

### 1.2 배경
- Tarkov Market API는 퀘스트 목표, 열쇠 위치, 은닉처 등 다양한 마커 데이터를 제공
- 우리 DB에는 이미 탈출구, 트랜짓 등의 마커가 게임 좌표로 저장되어 있음
- 두 좌표계가 다르므로 변환 수식이 필요함

### 1.3 범위
- TarkovDBEditor 프로젝트에 "Map Transfer" 탭 추가
- Map Preview 윈도우를 베이스로 UI 구성
- Tarkov Market API 마커 로드, 비교, 변환, DB 저장 기능

---

## 2. 좌표 시스템 분석

### 2.1 우리 DB (MapMarkers 테이블)
```
좌표계: 게임 내 좌표 (Game Coordinates)
- X: 게임 X 좌표
- Y: 높이 (대부분 0)
- Z: 게임 Z 좌표
- FloorId: 층 식별자 (main, basement, level2 등)

화면 변환: CalibratedTransform [a, b, c, d, tx, ty]
- screenX = a * gameX + b * gameZ + tx
- screenY = c * gameX + d * gameZ + ty
```

### 2.2 Tarkov Market API
```
좌표계: SVG viewBox 좌표 (SVG Coordinates)
- geometry.x: SVG X 좌표
- geometry.y: SVG Y 좌표
- level: 층 번호 (1, 2, 3 등)

문서에 따르면 SvgBounds를 사용해 변환 가능:
- SvgBounds: [[maxLat, minLng], [minLat, maxLng]]
```

### 2.3 변환 전략

**목표**: Tarkov Market SVG 좌표 → 우리 게임 좌표

**방법 1: 참조점 기반 변환 (Affine Transform)**
1. 동일한 마커(탈출구, 트랜짓 등)를 양쪽에서 찾아 매칭
2. 최소 3개의 참조점으로 Affine 변환 행렬 계산
3. 계산된 행렬로 모든 API 마커 좌표 변환

```
[gameX]   [a  b] [svgX]   [tx]
[gameZ] = [c  d] [svgY] + [ty]
```

**방법 2: 수동 보정**
- 자동 변환 후 오차가 있는 마커를 수동으로 조정

---

## 3. 기능 요구사항

### 3.1 화면 구성 (Map Preview 기반)

```
┌─────────────────────────────────────────────────────────────────┐
│ [Map: ▼Customs] [Floor: ▼Main]  [Show: ☑DB ☑API ☑Matched]      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                      SVG Map Display                            │
│                                                                 │
│   ● DB 마커 (녹색)                                               │
│   ○ API 마커 (파란색)                                            │
│   ◆ 매칭된 마커 (노란색)                                          │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│ [Fetch API] [Auto Match] [Calculate Transform] [Import Selected]│
├─────────────────────────────────────────────────────────────────┤
│ Status: Ready | DB: 15 markers | API: 42 markers | Matched: 8   │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 주요 기능

#### F1. API 데이터 가져오기
- Tarkov Market markers/list API 호출
- 난독화 디코딩 (index 5~9 제거 → Base64 → URL decode → JSON parse)
- 맵별 마커 목록 로드 및 캐싱

#### F2. 마커 자동 매칭
- DB 마커와 API 마커를 이름/타입으로 매칭
- 매칭 기준:
  - Category: Extractions → PmcExtraction, ScavExtraction, SharedExtraction
  - Category: Spawns → PmcSpawn, ScavSpawn, BossSpawn
  - 이름 유사도 비교 (Levenshtein distance 또는 부분 매칭)

#### F3. 좌표 변환 행렬 계산
- 매칭된 참조점 3개 이상으로 Affine 변환 행렬 계산
- 최소제곱법(Least Squares)으로 오차 최소화
- 계산된 행렬을 맵별로 저장

#### F4. 마커 미리보기
- DB 마커: 녹색 원으로 표시
- API 마커: 파란색 원으로 표시 (변환된 좌표)
- 매칭된 마커: 노란색 선으로 연결

#### F5. 선택적 Import
- API 마커 목록에서 가져올 항목 선택
- 선택된 마커를 변환된 좌표로 DB에 저장
- 중복 체크 및 업데이트/신규 구분

### 3.3 데이터 모델

#### TarkovMarketMarker (API 응답)
```csharp
public class TarkovMarketMarker
{
    public string Uid { get; set; }
    public string Category { get; set; }      // Quests, Extractions, Spawns, Keys, Loot, Miscellaneous
    public string SubCategory { get; set; }   // Quest, PMC Extraction, Scav Spawn 등
    public string Name { get; set; }
    public string? Desc { get; set; }
    public string Map { get; set; }
    public int? Level { get; set; }           // 층 번호
    public TarkovMarketGeometry Geometry { get; set; }
    public string? QuestUid { get; set; }
    public List<string>? ItemsUid { get; set; }
    public List<TarkovMarketImage>? Imgs { get; set; }
    public DateTime Updated { get; set; }
}

public class TarkovMarketGeometry
{
    public double X { get; set; }
    public double Y { get; set; }
}
```

#### MarkerMatchResult (매칭 결과)
```csharp
public class MarkerMatchResult
{
    public MapMarker DbMarker { get; set; }           // 우리 DB 마커
    public TarkovMarketMarker ApiMarker { get; set; } // API 마커
    public double Distance { get; set; }              // 변환 후 거리 오차
    public bool IsReferencePoint { get; set; }        // 참조점으로 사용 여부
}
```

#### MapTransformConfig (변환 행렬 저장)
```csharp
public class MapTransformConfig
{
    public string MapKey { get; set; }
    public double[] SvgToGameTransform { get; set; }  // [a, b, c, d, tx, ty]
    public DateTime CalculatedAt { get; set; }
    public int ReferencePointCount { get; set; }
    public double AverageError { get; set; }
}
```

---

## 4. 카테고리 매핑

### 4.1 Tarkov Market → 우리 DB MarkerType 매핑

| API Category | API SubCategory | DB MarkerType |
|-------------|-----------------|---------------|
| Extractions | PMC Extraction | PmcExtraction |
| Extractions | Scav Extraction | ScavExtraction |
| Extractions | Co-op Extraction | SharedExtraction |
| Spawns | PMC Spawn | PmcSpawn |
| Spawns | Scav Spawn | ScavSpawn |
| Spawns | Boss Spawn | BossSpawn |
| Keys | Key | Keys |
| Miscellaneous | Lever | Lever |
| Miscellaneous | Switch | Lever |
| Quests | Quest | (별도 처리 - QuestObjectives 테이블) |
| Loot | Cache | (신규 타입 추가 필요?) |

### 4.2 층(Level) 매핑

| API level | DB FloorId (예: Factory) |
|-----------|-------------------------|
| 0 또는 null | main |
| -1 | basement |
| 1 | main |
| 2 | level2 |
| 3 | level3 |

※ 맵마다 다를 수 있으므로 설정 필요

---

## 5. UI 워크플로우

### 5.1 기본 흐름

```
1. 맵 선택
   └─ MapSelector에서 맵 선택
   └─ DB 마커 자동 로드 및 표시

2. API 데이터 가져오기
   └─ [Fetch API] 버튼 클릭
   └─ Tarkov Market API 호출
   └─ 디코딩 후 마커 목록 표시

3. 자동 매칭
   └─ [Auto Match] 버튼 클릭
   └─ 이름/타입 기반 매칭 수행
   └─ 매칭 결과 시각화 (선으로 연결)

4. 변환 행렬 계산
   └─ 매칭된 마커 중 참조점 선택 (최소 3개)
   └─ [Calculate Transform] 버튼 클릭
   └─ Affine 변환 행렬 계산
   └─ API 마커 좌표 변환 및 표시

5. Import
   └─ 가져올 마커 선택 (체크박스)
   └─ [Import Selected] 버튼 클릭
   └─ DB에 저장
```

### 5.2 수동 매칭/조정

- 마커 클릭으로 선택
- 드래그로 위치 조정 (API 마커만)
- 우클릭 메뉴: 매칭 해제, 참조점 지정/해제

---

## 6. 구현 계획

### Phase 1: 기본 UI 및 API 연동
1. MapTransferWindow.xaml/cs 생성 (Map Preview 복사)
2. TarkovMarketService.cs 생성 (API 호출, 디코딩)
3. 기본 마커 표시 (DB + API)
4. MainWindow 메뉴에 "Map Transfer" 추가

### Phase 2: 매칭 및 변환
1. 자동 매칭 알고리즘 구현
2. Affine 변환 행렬 계산 로직
3. 변환된 좌표로 API 마커 표시
4. 오차 시각화

### Phase 3: Import 및 저장
1. 마커 선택 UI (체크박스, 전체선택 등)
2. 변환된 좌표로 DB 저장
3. 중복 처리 로직
4. 변환 행렬 저장/로드

### Phase 4: 개선
1. 수동 매칭/조정 기능
2. 층(Level) 매핑 설정
3. 퀘스트 마커 → QuestObjectives 연동
4. 캐시 및 성능 최적화

---

## 7. 파일 구조

```
TarkovDBEditor/
├── Views/
│   └── MapTransferWindow.xaml       # 신규
│   └── MapTransferWindow.xaml.cs    # 신규
├── Services/
│   └── TarkovMarketService.cs       # 신규 - API 호출 및 디코딩
│   └── MarkerMatchingService.cs     # 신규 - 매칭 알고리즘
│   └── CoordinateTransformService.cs # 신규 - 좌표 변환
├── Models/
│   └── TarkovMarketModels.cs        # 신규 - API 응답 모델
│   └── MarkerMatchResult.cs         # 신규 - 매칭 결과 모델
└── Resources/Data/
    └── map_svg_transforms.json      # 신규 - 맵별 SVG→Game 변환 행렬
```

---

## 8. 기술적 고려사항

### 8.1 Affine 변환 계산
- 최소 3개 점 필요 (6개 미지수: a, b, c, d, tx, ty)
- 과결정 시스템(3개 이상)에서 최소제곱법 적용
- Math.NET Numerics 라이브러리 또는 직접 구현

### 8.2 오차 처리
- 변환 후 평균 오차 표시
- 오차가 큰 마커 강조 표시
- 참조점 선택 가이드 (분산된 위치 권장)

### 8.3 API 제한
- Rate limiting 대응 (요청 간 딜레이)
- 캐시 활용 (map_market_cache/ 폴더)
- 오프라인 작업 지원

---

## 9. 제외 범위

- Tarkov Market quests/list API 연동 (추후 확장)
- 자동 주기적 동기화
- 다국어 이름 처리 (name_l10n)
- 이미지 다운로드 및 표시 (imgs 필드)

---

## 10. 성공 기준

1. Tarkov Market API에서 마커 데이터를 성공적으로 가져올 수 있음
2. 기존 DB 마커와 API 마커를 자동 매칭할 수 있음
3. 참조점 기반으로 좌표 변환 행렬을 계산할 수 있음
4. 변환된 좌표가 실제 게임 위치와 일치함 (오차 < 50 게임유닛)
5. 선택한 마커를 DB에 저장할 수 있음
