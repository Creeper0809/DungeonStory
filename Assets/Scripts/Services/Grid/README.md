# Grid Module Boundary

이 폴더는 Grid 시스템을 Unity 에셋처럼 재사용할 수 있게 나누는 것을 목표로 한다.

## 재사용 가능한 에셋 영역

- `Core`: Unity 에셋으로 재사용할 수 있는 Grid 런타임 코어.
- `System`: Grid 인스턴스, Grid 모드, 드래그 선택, 변경 이벤트를 관리하는 재사용 가능한 Unity 매니저.
- `Building`: `BuildingSO`, `BuildableObject` 기반 설치/삭제/검증/경로 편의 로직. `BuildingSO`의 `GridBuildingPlacement` 뷰만 배치 판단에 사용한다.
- `Placement`: Grid 위에 무언가를 배치할 수 있는지 판단하는 범용 검증 로직.
- `Rendering`: Tilemap, 벽 계산, Grid 시각화처럼 다른 Unity 프로젝트에서도 재사용 가능한 렌더링 로직.
- `UI`: 고스트 뷰, 단순 탭처럼 특정 게임 데이터에 묶이지 않는 Grid UI 컴포넌트.

이 영역은 다른 Unity 프로젝트로 옮겨도 동작할 수 있어야 한다.

허용되는 개념:

- `UnityEngine`
- `Vector2Int`
- `Vector3`
- Grid position
- Cell
- Layer
- Occupant
- Building data
- Buildable object
- Path search
- Visitability
- Movement connection
- Placement validation
- Tilemap rendering helper
- Ghost object
- Grid mode
- Drag selection
- Grid changed event
- Injected building visual output

피해야 하는 의존:

- `Grid/System` 바깥의 재사용 컴포넌트에서 현재 게임 싱글톤에 직접 기대는 흐름
- `GameManager.Instance`
- `DataManager.Instance`
- `NoticeFeedEvent`
- `InfoFeedEvent`
- DungeonStory 전용 UI
- DungeonStory 전용 자원/시설/상점 규칙

## DungeonStory 통합 영역

- `DungeonStory`: 재사용 가능한 Grid 모듈을 현재 게임의 `BuildingSO`, `BuildableObject`, `DataManager`, `GameManager`, `NoticeFeed`, `UITab` 흐름에 연결한다.
- `DungeonStory/Building`: 현재 게임의 건물 데이터, 건물 GameObject, 건설/삭제 규칙을 Grid에 연결한다.
- `DungeonStory/UI`: 현재 게임의 선택 건물, 건설 패널, 건물 정보 UI를 재사용 가능한 Grid UI/Rendering 컴포넌트에 연결한다.

이 영역은 게임에 종속되어도 된다. 단, 가능하면 재사용 가능한 영역으로 역방향 의존을 만들지 않는다.

## 현재 경계 판정

- `Grid/System/GridSystemManager`: 재사용 영역이다. 현재 게임의 건물 선택, 건설, 삭제, 알림, 데이터 로딩을 알면 안 된다.
- `Grid/Building/GridBuildingRuntime`: `BuildingSO`, `BuildableObject`를 Grid에 설치/삭제/조회하는 재사용 가능한 건설 런타임이다. `DataManager`, `GameManager`, `NoticeFeed`, 입력 액션을 직접 알면 안 된다. 건물 타일 출력은 `IGridBuildingVisual`로 주입받는다.
- `Grid/Placement/GridPlacementValidator`: 좌표 범위, 점유 가능 여부, 하단 지지, 제거 시 상단 지지 유지처럼 `Grid`, `GridCell`, `IGridOccupant`만으로 판단 가능한 범용 검증을 담당한다.
- `Grid/DungeonStory/Building/DungeonStoryGridBuildingController`: 현재 게임의 건물 선택, 입력, 알림, 데이터 조회, `GridTexture` 비주얼 출력을 재사용 가능한 Grid Building 런타임에 연결하는 MonoBehaviour다.
- `Grid/DungeonStory/Building` 영역: `DataManager`, `GameManager`, `NoticeFeed`, 입력 액션을 직접 사용하므로 현재 게임 통합 영역이다.
- `Grid/DungeonStory/UI/GridConstructTab`, `GridUIManager`, `DungeonStoryGridGhostPresenter`: 건설 패널, 건물 정보 UI, 선택 건물 표시를 연결하므로 현재 게임 통합 영역이다.
- `Grid/UI/UIGridTab`: 특정 게임 데이터 없이 카테고리 탭만 토글하므로 재사용 영역이다. 필드 이름도 `categoryId`처럼 범용 이름을 사용한다.
- `Grid/Rendering/GridTexture`: Tilemap 출력과 벽 계산을 담당하는 재사용 렌더링 컴포넌트다. `IGridBuildingVisual`을 구현하므로 건설 런타임에 주입할 수 있다. `GridTexture.TilemapLayer`가 기존 건물 자산에 직렬화되어 있으므로 이름/enum 변경은 자산 마이그레이션이 필요하다.

## 0.1 완료 기준 매핑

- `Grid`: 점유, 이동 가능성, 연결성, 경로 탐색만 담당한다. `BuildingSO`, `DataManager`, `GameManager`, `NoticeFeedEvent`, DungeonStory UI를 모른다.
- `GridSystemManager`: Grid 인스턴스, 모드, 드래그 선택, 변경 이벤트만 담당한다. 건물 선택/알림/데이터 조회는 `DungeonStoryGridBuildingController`가 맡는다.
- `BuildingSO`: 기존 공개 필드는 유지하되, 배치 판단은 `GridBuildingPlacement` 뷰로 모았다. 게임 진행용 값은 `Game Data` 그룹으로 분리했다.
- `BuildableObject`: Grid 점유에 필요한 id, 위치, 점유 좌표, 이동 타입과 클릭 이벤트만 제공한다. 정보 UI 연결은 `DungeonStoryGridBuildingController`가 클릭 이벤트를 받아 처리한다.
- `GridBuildingRuntime`: 건설/삭제/검증/경로 편의 API를 제공한다. GameObject 생성과 Collider 구성은 factory가 맡고, Tilemap 출력은 `IGridBuildingVisual` 주입으로 처리한다.
- `DungeonStoryGridBuildingController`: 현재 게임의 최상위 조립 API다. `DataManager`, `GameManager`, `NoticeFeedEvent`, `GridTexture`, 입력 액션을 여기서 연결한다.
- 범용 배치/검증/고스트/매니저는 `Assets/Scripts/Grid` 아래에 있고, 현재 게임 통합 컴포넌트는 `Assets/Scripts/Grid/DungeonStory` 아래에 있다.

## 파일 수 관리 원칙

- Unity 컴포넌트로 씬/프리팹에 붙는 `MonoBehaviour`는 파일을 분리한다.
- 순수 C# 보조 클래스는 같은 경계 안의 대표 파일로 합칠 수 있다.
- 경계를 나누기 위해 파일을 무조건 늘리지 않는다. 의존성 방향은 폴더와 타입 책임으로 관리하고, 작은 helper는 합쳐도 된다.

## 에디터 호환성 우회

- `Grid/System/Editor/GridSystemManagerEditor`: Odin Inspector가 현재 Unity 버전의 내부 에디터 필드를 참조하다가 `gridOriginPos` 표시 중 예외를 내는 문제를 피하기 위한 우회다.
- 이 에디터는 런타임 로직을 바꾸지 않고 `GridSystemManager`만 Unity 기본 인스펙터로 그린다.
- Odin 자체 DLL은 직접 수정하지 않는다. Odin 호환성 문제는 패키지 업데이트 또는 문제 컴포넌트의 기본 인스펙터 우회로 처리한다.

좋은 방향:

```text
DungeonStory
-> Grid/Core
-> Grid/System
-> Grid/Placement
-> Grid/Rendering
-> Grid/UI
```

나쁜 방향:

```text
Grid/Core
Grid/System
Grid/Placement
Grid/Rendering
Grid/UI
-> DungeonStory
-> DataManager.Instance
```
