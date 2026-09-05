# 변경 원본의 누락 판정 재검토

GAP-039와 GAP-066의 현재 원본 재검토를 마쳤다. 두 누락 판정은 유지한다. 파일 전체의 모든 동작이나 실제 UI·저장 왕복을 검증한 것이 아니라, 해당 판정을 지탱하는 현재 호출부와 규칙을 다시 읽은 결과다. 스크립트·자산·공개 문서는 수정하지 않았다.

## GAP-039 단골·일반 영입 후보

- 현재 Facility719의 완료 호출은 BuildableObject.CompleteUse944 및 BuildingOccupancyAssignment.CompleteUse187을 거쳐 RecordFacilityUse1263에서 방문 이벤트를 발행한다. 사용을 시작한 횟수나 입장 일수가 아니다.
- 변경된 OperatingDaySettlement352~388은 방문자를 이벤트에 담아 전달한다. RegularCustomerRuntime155의 실제 구독자는 수용 심사 결과를 RecordVisit에 전달하고, RegularCustomerSystem은 당시 MOOD를0~100으로 제한해 기록한다.
- DungeonRegularCustomerSaveData의 진행 상태는 시설 방문 수와 만족도 합으로 산술평균을 계산하고, 한번 얻은 단골/후보 상태를 낮은 후속 만족도로 취소하지 않는다. GameplayScene7530~7533은 양쪽 조건을2회/65점으로 작성한다.
- 기존 GAP-039의 내용은 현재 실행 경로와 일치한다. 수용 조건·10일 재영입 간격 등 별도 항목은 중복 추가하지 않았다.

## GAP-066 급수 수질과 소비 단위

- 변경된 Facility420~438은 최소 수질을 직접 덮어쓰지 않고 WaterFixtureUseRuntime.TryBeginUse에 실제 시설과 이용자를 넘긴다.621에서는 사용 티켓을 완료한다.
- WaterFixtureUseRuntime33~65는 작성 minimumQuality와 개인 물 소비 보정을 FluidNetworkRuntime.TryConsume에 전달한다.
- FluidNetworkRuntime200~288,303~362의 소비/가용/배치 할당은 요청량 전체를 한 수질로 충족할 때만 선택한다. 수질 순서와0.0001 허용오차는 기존 판정과 같다.2022~2044의 빈 용량은 Clean/Unsafe/Foul 합을 뺀다.
- FluidNodeWaterRules.GetConsumptionOrder의 Clean, Unsafe→Clean, Foul→Unsafe→Clean 순서도 그대로다. 기존 GAP-066의 내용은 현재 경로와 일치한다. 재래식·수동 물통 허용은 GAP-071~073에서 별도로 다룬다.

## 현재 원본 해시

| 원본 | SHA-256 |
| --- | --- |
| [OperatingDaySettlement.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Operation/OperatingDaySettlement.cs>) | `ec27018cfd80773dc30d0396bcbe5b57f9e303f130840c45e846b8322cafca61` |
| [RegularCustomerRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/RegularCustomerRuntime.cs>) | `74c3fae42a295b90077b82ad89031458bbd6458d41c170e9840b644f703f4a43` |
| [RegularCustomerSystem.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/RegularCustomerSystem.cs>) | `5923ca452c4e690433f6c1ca68eb680d393ecb2c162c4950de3e85876232e3c6` |
| [DungeonRegularCustomerSaveData.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Recruitment/Core/DungeonRegularCustomerSaveData.cs>) | `eeeec68f8a812f87ca894437961812cc0d68ad6867a7456e2d068e6bf979e1b1` |
| [Facility.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs>) | `c280991bd7089fff337f3523f099201e1eb89cc2379af762d59121bb1f48fb31` |
| [BuildableObject.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildableObject.cs>) | `eb1d06186598120201ffc80ee2c416f6d48c724445bc962a37268dbbd407e691` |
| [BuildingOccupancyAssignment.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingOccupancyAssignment.cs>) | `2813db7ce480b86332ead3e0a29b80ae0fc022a1bf967e8379439421d15b0444` |
| [GameplayScene.unity](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scenes/GameplayScene.unity>) | `6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40` |
| [FluidNetworkRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs>) | `04d270b395f5075d5f8f9e69fcda0c3afe3d449735ac04ed752211e011d672bb` |
| [FluidNodeState.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Core/FluidNodeState.cs>) | `10f323eb6fcc1d8326e2625c57414910ddeed362768a72cf5630f59fd0388acc` |
| [WaterFixtureUseRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/WaterFixtureUseRuntime.cs>) | `8e9b42c16c31769e665706c3cccc68e77ad0841b18c7abba8a84d833fe6d5879` |

지식베이스 freshness는 같은 체크포인트의 Conveyor 조회(stale4, 반환0행)를 따르며 원본을 직접 대조했다. 두 변경 파일의 이전 해시와 현재 해시는 evidence-check.json에 계속 보존한다. 원본 재검토 대기는 해소했지만 전체 의미 중복 검토와 다른 시스템의 미검토 범위는 남아 있다. 밸런스 영향 없음.

