# PRD: Map Auto-Calibration System

## Overview
구 버전(tarkov.dev) 지도와 신 버전(Tarkov Market) 지도를 비교 분석하여 마커 좌표를 자동으로 정밀 보정하는 시스템.

## Problem Statement
현재 수동으로 탈출구 마커를 드래그하여 보정점을 생성하고 있음. 이 방식은:
- 시간이 많이 소요됨
- 사용자마다 정확도가 다름
- 새 맵 추가 시 매번 수동 작업 필요
- 모든 영역을 커버하기 어려움

## Solution
기존 tarkov.dev 지도의 정확한 좌표 변환 로직을 "정답"으로 활용하여 신 지도의 좌표를 자동 보정.

## Architecture

### Core Concept
```
GameCoords(x, z)
  → OldMapTransform → OldScreenPos (정확한 위치)
  → Old→New Mapping → NewScreenPos (보정된 위치)
```

### Components

#### 1. OldMapReferenceData (Model)
기존 tarkov.dev 맵 설정 저장.

```csharp
public class OldMapReferenceData
{
    public string MapKey { get; set; }
    public double OldImageWidth { get; set; }
    public double OldImageHeight { get; set; }
    public double[] OldTransform { get; set; }  // [scaleX, marginX, scaleY, marginY]
    public double[][] OldSvgBounds { get; set; }
    public int OldCoordinateRotation { get; set; }
}
```

**Data Source:** tarkov.dev maps.json (archived)
| Map | ImageSize | Transform | Rotation |
|-----|-----------|-----------|----------|
| Woods | 8192x8192 | [0.635152, 387.254, 0.626805, 566.996] | 180 |
| Customs | 5765x4193 | [0.989938, 698.548, 1.42898, 815.237] | 180 |
| Shoreline | 7680x6400 | [0.7904, 410.96, 1.001, 694.92] | 180 |
| Interchange | 4000x3900 | [1.08491, 616.556, 1.05788, 537.323] | 180 |
| Reserve | 3200x3000 | [1.52747, 471.774, 1.5567, 542.479] | 180 |
| Lighthouse | 3100x3700 | [0.5852, 0, 0.4294, 0] | 180 |
| Streets | 3260x3500 | [2.04668, 0, 1.59904, 0] | 180 |
| Factory | 3600x3600 | [44.8285, 3299.53, 41.5216, 3550.62] | 90 |
| GroundZero | 2800x3100 | [4.2051, 1342.58, 3.32583, 413.19] | 180 |
| Labs | 5500x4200 | [4.39242, 2148.09, 4.12102, 1388.25] | 270 |

#### 2. OldMapTransformService (Service)
구 지도 좌표 변환 로직 (기존 tarkov.dev 방식 그대로 유지).

```csharp
public class OldMapTransformService
{
    private readonly Dictionary<string, OldMapReferenceData> _references;

    // 게임 좌표 → 구 지도 화면 좌표
    public (double x, double y) TransformToOldScreen(string mapKey, double gameX, double gameZ);

    // 구 지도 참조 데이터 반환
    public OldMapReferenceData? GetReferenceData(string mapKey);
}
```

#### 3. MapComparisonService (Service)
구/신 지도 비교 분석 및 매핑 계산.

```csharp
public class MapComparisonService
{
    // 구→신 좌표 매핑 계산 (affine transform)
    public double[] CalculateOldToNewMapping(
        string mapKey,
        List<(double oldX, double oldY, double newX, double newY)> correspondences);

    // 매핑 적용하여 보정된 좌표 반환
    public (double x, double y) ApplyMapping(double[] mapping, double oldX, double oldY);

    // 분석 결과 (오차 통계)
    public MappingAnalysis AnalyzeMapping(string mapKey, double[] mapping);
}

public class MappingAnalysis
{
    public double MeanError { get; set; }
    public double MaxError { get; set; }
    public double MinError { get; set; }
    public List<(double x, double y, double error)> ErrorDistribution { get; set; }
}
```

#### 4. AutoCalibrationService (Service)
자동 보정점 생성 및 최적 변환 계산.

```csharp
public class AutoCalibrationService
{
    private readonly OldMapTransformService _oldTransform;
    private readonly MapComparisonService _comparison;
    private readonly IMapCoordinateTransformer _newTransform;

    // tarkov.dev API에서 모든 위치 데이터 수집
    public async Task<List<ReferencePoint>> CollectReferencePointsAsync(string mapKey);

    // 자동 보정 실행
    public async Task<AutoCalibrationResult> CalibrateMapAsync(string mapKey);

    // 모든 맵 일괄 보정
    public async Task<Dictionary<string, AutoCalibrationResult>> CalibrateAllMapsAsync();
}

public class ReferencePoint
{
    public string Id { get; set; }
    public string Name { get; set; }
    public double GameX { get; set; }
    public double GameZ { get; set; }
    public double OldScreenX { get; set; }  // 구 지도에서의 정확한 위치
    public double OldScreenY { get; set; }
    public double CurrentNewScreenX { get; set; }  // 신 지도에서의 현재 위치
    public double CurrentNewScreenY { get; set; }
}

public class AutoCalibrationResult
{
    public string MapKey { get; set; }
    public int ReferencePointCount { get; set; }
    public double[] OldToNewMapping { get; set; }  // [a, b, c, d, tx, ty]
    public List<CalibrationPoint> GeneratedCalibrationPoints { get; set; }
    public MappingAnalysis Analysis { get; set; }
}
```

#### 5. TransformAnalyzer (Tool)
변환 정확도 분석 및 시각화.

```csharp
public class TransformAnalyzer
{
    // 현재 변환의 오차 분석
    public AnalysisReport AnalyzeCurrentTransform(string mapKey);

    // 보정 전/후 비교
    public ComparisonReport CompareBeforeAfter(string mapKey, AutoCalibrationResult result);

    // 오차 히트맵 생성 (맵 영역별 오차 크기)
    public double[,] GenerateErrorHeatmap(string mapKey, int gridSize = 10);
}
```

## Data Flow

### Phase 1: 참조 데이터 수집
```
tarkov.dev API
    ↓
Extract/Spawn/Quest locations with game coords
    ↓
ReferencePoint list
```

### Phase 2: 좌표 계산
```
For each ReferencePoint:
    GameCoords(x, z)
        ↓
    OldMapTransformService.TransformToOldScreen()
        ↓
    OldScreenPos (= ground truth)

    GameCoords(x, z)
        ↓
    MapCoordinateTransformer.TryTransform() (current new map)
        ↓
    CurrentNewScreenPos
```

### Phase 3: 매핑 계산
```
Correspondences: [(oldX, oldY) → (expectedNewX, expectedNewY)]
    ↓
MapComparisonService.CalculateOldToNewMapping()
    ↓
Mapping matrix [a, b, c, d, tx, ty]
```

### Phase 4: 적용
```
Option A: Update CalibratedTransform in MapConfig
Option B: Generate CalibrationPoints for IDW
Option C: Create new transform pipeline
```

## Implementation Plan

### Phase 1: Core Infrastructure (Priority: High)
1. [ ] Create `OldMapReferenceData` model
2. [ ] Create `OldMapTransformService` with hardcoded tarkov.dev data
3. [ ] Implement `TransformToOldScreen()` using original algorithm
4. [ ] Unit tests for old transform accuracy

### Phase 2: Comparison & Analysis (Priority: High)
1. [ ] Create `MapComparisonService`
2. [ ] Implement `CalculateOldToNewMapping()` using least squares
3. [ ] Implement `AnalyzeMapping()` for error statistics
4. [ ] Create `TransformAnalyzer` for debugging

### Phase 3: Auto-Calibration (Priority: Medium)
1. [ ] Create `AutoCalibrationService`
2. [ ] Implement API data collection (reuse existing TarkovDevApiService)
3. [ ] Implement `CalibrateMapAsync()`
4. [ ] Generate CalibrationPoints from analysis
5. [ ] Integration with existing calibration system

### Phase 4: UI & Tooling (Priority: Low)
1. [ ] Add "Auto-Calibrate" button to MapTrackerPage
2. [ ] Progress indicator during calibration
3. [ ] Error visualization overlay
4. [ ] Before/After comparison view

## File Structure
```
TarkovHelper/
├── Models/MapTracker/
│   ├── OldMapReferenceData.cs          # NEW
│   ├── ReferencePoint.cs               # NEW
│   └── AutoCalibrationResult.cs        # NEW
├── Services/MapTracker/
│   ├── OldMapTransformService.cs       # NEW
│   ├── MapComparisonService.cs         # NEW
│   ├── AutoCalibrationService.cs       # NEW
│   └── TransformAnalyzer.cs            # NEW
└── Pages/
    └── MapTrackerPage.xaml.cs          # MODIFY (add auto-calibrate button)
```

## Success Criteria
1. 수동 보정 없이 모든 맵에서 마커 오차 < 20px
2. 자동 보정 시간 < 5초 (모든 맵)
3. 새 맵 추가 시 코드 수정 없이 자동 지원
4. 기존 수동 보정 데이터와 호환

## Risks & Mitigations
| Risk | Mitigation |
|------|------------|
| tarkov.dev API 변경 | 로컬 캐시 사용, 폴백 로직 |
| 구/신 지도 영역 불일치 | 영역별 매핑 지원 |
| 비선형 왜곡 | 다항식/TPS 변환 옵션 추가 |

## Timeline
- Phase 1: 1-2 days
- Phase 2: 1-2 days
- Phase 3: 2-3 days
- Phase 4: 1-2 days

Total: ~1 week
