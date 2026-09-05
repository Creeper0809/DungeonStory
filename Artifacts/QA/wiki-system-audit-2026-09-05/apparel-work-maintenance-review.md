# 의복 세탁·건조·수선·개구부 작업 대조

상태: 부분 대조 완료. 전체 시스템·위키 전수 감사는 진행 중이다. 스크립트·자산·공개 위키는 수정하지 않았다. 밸런스 영향 없음.

## 조사 범위

세탁·건조·수선·기존 구멍 여닫기의 주문 생성, 완료 효과, 실제 UI 호출과 작업 완료 호출을 대조했다. command 41/42/43/45/46을 Resources 전체에서 찾아 시설 5개, 공개 시설 도감 5개, 수선 재료 도감 2개를 확인했다. 제작·품질 반복·불합격품 해체·오염/마모 생성자와 일반 착용 관리 전체는 아직 검토 중이다.

이번에 GAP-100~103을 추가했다. 기존 GAP-098(착용 적합성), GAP-099(원단 선택·성능)을 다시 세지 않는다. 전체 의미 중복 제거는 pending이다.

## 문서에서 빠진 규칙

| 작업 | 실제 UI의 대상 선택 | 주문 정의 WU | 완료 시 결과 |
| --- | --- | --- | --- |
| 손세탁 | 미착용·사용 가능·오염>0, 최대12벌 | 배치당12 | 오염0, 젖음100 |
| 동력 세탁·건조 | 위와 같음 | 배치당4 | 오염0, 젖음0 |
| 실내 건조 | 미착용·사용 가능·오염<=0·젖음>=20, 최대12벌 | 배치당24 | 젖음0, 오염은 유지 |
| 가벼운 수선 | 20<=내구도<100 중 가장 손상된 한 벌을 고른 뒤, 내구도60 이상이면 적용 | 8 | 재료 없이 min(100, 기존+25) |
| 큰 수선 | 위 선택에서20 이상60 미만 | 18 | 재봉실1+범용 수선 조각1, 내구도70 |
| 기존 구멍 여닫기 | 꼬리/날개/뿔 개조가 있는 미착용 의복 중 첫 대상 | 3 | 기존 구멍을 열거나 닫음. 크기와 개조 자체는 유지 |

수치는 주문 정의값이다. 실제 시설 작업 완료 호출이 남은 WU 전부를 한 번에 전달하므로 WU 차이를 곧바로 소요 시간이나 노동 절감률로 설명할 수 없다. 물·세제·전력 소비 또한 시설 이름으로 추정하지 않는다.

세탁·건조 명령은 유효한 고유 인스턴스1~12개를 예약한다. UI의 오염·젖음 필터와 명령 자체의 검증은 동일하지 않다. 예를 들어 건조 완료 함수는 오염을 없애지 않는다. 이미 착용한 의복은 UI에서 제외된다.

수선 내구도20 미만은 거부한다. 60 경계에서 재료와 결과가 달라지고,100 이상은 UI 대상에서 제외한다. 수선 재료 처리 후 상태 적용·확정에 실패한 경우 동일한 결과를 재사용하도록 저장하지만 실제 실패 복구의 성공 여부는 아래 확인 항목에 남긴다.

일반 개조 명령은14 WU로 크기와 지원 개조를 바꿀 수 있으나 현재 비 Editor UI 호출은 확인되지 않았다. 탈의 칸막이의3 WU 작업과 구분해야 한다. 새 구멍을 이 메뉴에서 만들 수 있다고 안내하면 안 된다.

## 시설·도감 전수표

| ID | 시설 | 명령 | 시설 Operate WU | 공개 설명 |
| --- | --- | --- | --- | --- |
| 9303 | [building-9303.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9303.json>) (손세탁 수조) | 41 | 10 | 생산 작업대·건설 비용·분류·크기, 관계0 |
| 9304 | [building-9304.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9304.json>) (실내 건조대) | 42 | 10 | 생산 작업대·건설 비용·분류·크기, 관계0 |
| 9305 | [building-9305.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9305.json>) (동력 세탁·건조기) | 43 | 10 | 생산 작업대·건설 비용·분류·크기, 관계0 |
| 9307 | [building-9307.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9307.json>) (탈의 칸막이) | 45 | 10 | 생산 작업대·건설 비용·분류·크기, 관계0 |
| 9308 | [building-9308.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9308.json>) (수선 접수대) | 46 | 10 | 생산 작업대·건설 비용·분류·크기, 관계0 |

5개 모두 부품 코드, 태그, 생산 작업대, 버퍼, 작업량 모듈만 작성되어 있다. 도메인 카탈로그에 각각1회 있고 루트 카탈로그는 해당 도메인 카탈로그를1회 참조한다. 루트에 직접 GUID가 없다는 이유로 미등록으로 세지 않았다. 모듈 종류 대조는 전체 시설의 건설 비용·수치 의미 감사와 별개다.

재봉실과 범용 수선 조각의 공개 설명은 공통 용도 문장이고 관계는0개다. 수선의 정확한1+1 소비 조건과 규칙 문서 연결이 필요하다. 연구 해금·건설 비용의 최신성은 이번 유지관리 작업 대조에서 완료로 세지 않는다.

## 실패·대기·저장

- 생성 시 시설이나 대상·재료 예약에 실패하면 주문을 만들지 않는다. 생성 중 추가한 임시 주문도 예약 실패 시 제거한다.
- 진행 중 시설/예약 문제로 ReturnToWaiting에 들어가면 예약을 해제하고 진척을0으로 초기화한다. 재시도는0.25,0.5,1 게임시간 순서로 늘고 이후1시간을 유지한다.
- 출력 공간 대기와 처리 확정 대기는 일반 재료 대기와 다르다. 모든 실패가 같은 진척 초기화로 이어진다고 일반화하지 않는다.
- 일반 미완료 주문은 저장된 진척을 유지하면서 복원 후 예약과 조건을 재검증하고, 재시도 시각은0으로 초기화한다. 수선 처리 확정 중인 주문은 해당 대기로 복원한다.
- 수선 재료 처리 확정이나 불합격품 회수 중에는 취소를 거부한다. 취소 API가 있다는 사실은 사용자에게 취소 UI가 제공된다는 증거가 아니다.
- 예약 서비스는 금지·가용 수량을 확인한다. 예약 존재는 시설로 실물이 이동했다는 증거가 아니다.

## 연결 경로와 검증 한계

UIBuildingInfo -> ApparelBuildingPanelPresenter -> IApparelWorkOrderCommand; Operate completion -> BuildingAbilityRuntimeDispatcher -> ResearchFacilityOperationFallbackHandler -> ApplyWork -> ResolveBatchState/ResolveRepair/ResolveAlteration; CharacterEnvironmentRuntime Capture/BuildRestoreCandidate/PublishRestoreCandidate -> work-order persistence

함수 호출과 상태 변경을 정적으로 확인했다. 정의5시설/명령 처리5변형/UI 처리5변형을 찾았지만 실제 클릭·게임 실행·저장 왕복은0건이다. 전체 의복 기능의 고아0 또는 연결 완료를 선언하지 않는다.

## 구현 확인 항목

### APPAREL-WORK-U01 주문 WU와 실제 시설 노동 회계

ResearchFacilityOperationFallbackHandler.ApplyApparelOrder는 Operate 한 사이클 완료 때 requiredWork-completedWork 전부를 ApplyWork에 전달한다. 다섯 시설의 operateWorkRequired는 모두 10이다.

주문 4/8/12/18/24 WU를 실제 노동·시간으로 직접 안내하면 오해가 생긴다. 시설 작업 후보와 한 사이클 경과시간, 기여·숙련 회계를 실제 경로에서 추가 검증해야 한다.

근거: [ResearchFacilityOperationFallbackHandler.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Research/ResearchFacilityOperationFallbackHandler.cs>), [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [V22_9303_손세탁_수조.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9303_손세탁_수조.asset>), [building-9303.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9303.json>), [V22_9304_실내_건조대.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9304_실내_건조대.asset>), [building-9304.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9304.json>), [V22_9305_동력_세탁_건조기.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9305_동력_세탁_건조기.asset>), [building-9305.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9305.json>), [V22_9307_탈의_칸막이.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9307_탈의_칸막이.asset>), [building-9307.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9307.json>), [V22_9308_수선_접수대.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9308_수선_접수대.asset>), [building-9308.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9308.json>)

### APPAREL-WORK-U02 완료 경계 재시도에 전달되는 0 WU

ApplyApparelOrder는 남은 WU가 0이면 0을 전달한다. ApplyWork는 amount<=0을 처리 확정 대기 검사보다 먼저 거부한다. 따라서 수선 재료 처리나 출력 확정 중 실패한 주문의 다음 작업 완료 호출이 재시도를 막을 수 있다.

저장된 영수증과 재시도 함수의 존재만으로 실제 복구 성공을 선언하지 않는다. 직접 UI/저장 실패 재현은 미실행이다.

근거: [ResearchFacilityOperationFallbackHandler.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Research/ResearchFacilityOperationFallbackHandler.cs>), [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>)

### APPAREL-WORK-U03 일반 개조와 취소의 실제 UI 호출

전체 비 Editor CreateAlteration 호출은 이 패널의 shortWardrobeOperation=true 한 곳이다. 14 WU 일반 개조는 API 및 완료 처리만 확인했다. 읽은 의복 패널 전체에는 주문 Cancel 호출이 없다.

새 구멍·사이즈를 변경하거나 의복 주문을 취소할 수 있는 실제 플레이 경로가 확인될 때까지 현재 UI 기능으로 쓰지 않는다. 해체/품질 반복의 나머지 경로는 후속 조사다.

근거: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [ApparelBuildingPanelPresenter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs>)

### APPAREL-WORK-U04 선택한 시설·의복과 실제 명령 대상의 차이

세탁·건조·수선·옷장 UI는 시설 ID를 명령에 전달하지 않는다. 명령은 같은 기능의 가동 시설을 안정 ID 순 첫 번째로 선택한다. 대상도 전체 재고 순서의 첫 배치 또는 첫 개조 의복이다.

여러 시설을 설치했을 때 다른 시설의 주문 목록에 들어가거나 엉뚱한 의복을 고칠 수 있는지 확인해야 한다. 수동 대상 선택 UI는 이 패널에 없다.

근거: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [ApparelBuildingPanelPresenter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs>), [V20ContentResolutionService.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20ContentResolutionService.cs>)

### APPAREL-WORK-U05 동력·물·세제 공급의 미확인 연결

5개 작성 시설에는 전력·급배수 모듈이 없고, CreateItemBatchOrder/ResolveBatchState는 의복만 예약·변경한다. FacilityCapabilityQuery의 가동 판정도 방과 파괴 상태만 본다.

시설 이름만으로 전력·물·세제 소모, 전력 단절 정지를 사실로 설명하지 않는다. 별도 기본 제공 모듈/실행 경로 여부는 추가 확인 대상이다.

근거: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [V20ContentResolutionService.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20ContentResolutionService.cs>), [V22_9303_손세탁_수조.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9303_손세탁_수조.asset>), [building-9303.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9303.json>), [V22_9304_실내_건조대.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9304_실내_건조대.asset>), [building-9304.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9304.json>), [V22_9305_동력_세탁_건조기.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9305_동력_세탁_건조기.asset>), [building-9305.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9305.json>), [V22_9307_탈의_칸막이.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9307_탈의_칸막이.asset>), [building-9307.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9307.json>), [V22_9308_수선_접수대.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9308_수선_접수대.asset>), [building-9308.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9308.json>)

### APPAREL-WORK-U06 수선·세탁의 실물 이동과 정책 검증

LeasedItemReservationService는 수량 예약을 한다. Reserve의 대상은 원래 스택 위치이고 목적지 ID는 비어 있다. ResolveBatchState/ResolveRepair/ResolveAlteration은 원래 스택을 직접 변경하며 시설 도착 여부를 검사하지 않는다. UI는 착용 의복을 제외하지만 명령의 TryFindApparel은 같은 제한을 직접 검사하지 않는다.

실제 운반/착용 잠금·거리 정책을 별도 경로가 보장하는지 확인해야 한다. '운반 대기'를 운반 실행 증거로 세지 않는다.

근거: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [ApparelBuildingPanelPresenter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs>), [LeasedItemReservationService.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/LeasedItemReservationService.cs>)

### APPAREL-WORK-U07 제작 고급 설정의 민첩 라벨

UI의 '작업자: 민첩 7 이상'은 DexterityPolicy에서 crafting 최소 경험치 400의 RuleSet을 생성한다.

이를 실제 민첩 제한으로 문서화하지 않는다. 제작 주문·작업자 정책 전체 대조에서 원인과 의도값을 확인한다.

근거: [ApparelBuildingPanelPresenter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs>)


## 수치·근거 검증

독립 산술 28건은 배치 범위5건, 완료 상태3건, 수선 경계7건, 건조 UI 경계4건, 재시도5건, 개구부4건이다. 오류0건. 이는 직접 읽은 조건의 전사·산술 확인이며 C# 실행이나 Unity 회귀 테스트가 아니다.

KB query=`ApparelWorkOrder Textile Laundry Repair`, area=`code, content, authority`, limit=8. 이전 실행 session30998을 같은 핸들로 확인했고 exit1/stale4건/생성 행0개였다. content digest=`139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6`, KB digest=`ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd`. 생성물은 최신 근거로 쓰거나 재생성하지 않았다.

읽은 구간, 시설 원문/공개값, 독립 산술 입력·기대값과 원본35개 해시는 [JSON 대조표](apparel-work-maintenance-review.json)에 보존한다. 이 중 기존 추적18개는 이전 해시와 같았고17개를 새로 추적한다. 해시 추적 수와 파일 완독 수는 다르다.

