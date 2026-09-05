# 구현 파일 맵

이 문서는 1.1 이후 구현을 실제 코드와 데이터 위치로 추적하기 위한 파일 맵이다.

세부 동작 설명은 [1.1 이후 구현 로직 요약](implementation-logic-summary.md), 검증 연결은 [구현 증거 매트릭스](implementation-evidence-matrix.md)를 기준으로 한다.

## 공통 기반

| 영역 | 주요 파일 |
|---|---|
| 그리드 모델 | `Assets/Scripts/Grid/Core/Grid.cs`, `Assets/Scripts/Grid/Core/GridCell.cs` |
| 그리드 런타임/배치 | `Assets/Scripts/Grid/System/GridSystemManager.cs`, `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`, `Assets/Scripts/Grid/Placement/GridPlacementValidator.cs` |
| 배치 비주얼/고스트 | `Assets/Scripts/Grid/Rendering/GridTexture.cs`, `Assets/Scripts/Grid/UI/GridGhostObject.cs`, `Assets/Scripts/Grid/UI/GridPlacementGhostPresenter.cs` |
| 배치 대상 | `Assets/Scripts/Buildings/BuildableObject.cs`, `Assets/Scripts/Buildings/SO/BuildingSO.cs` |
| 이동 건물 | `Assets/Scripts/Buildings/Hallway.cs`, `Assets/Scripts/Buildings/Stair.cs`, `Assets/Scripts/Buildings/Door.cs` |

## 1.1 초기 시설 세트

| 영역 | 주요 파일/에셋 |
|---|---|
| 시설 런타임 | `Assets/Scripts/Buildings/Facility.cs`, `Assets/Scripts/Buildings/Shop.cs`, `Assets/Scripts/Buildings/IInteractable.cs` |
| 시설 데이터 | `Assets/Scripts/Buildings/SO/BuildingSO.cs`, `Assets/Scripts/Buildings/SO/SaleItem.cs`, `Assets/Scripts/Buildings/SO/StockInfo.cs` |
| P1 시설 에셋 | `Assets/Resources/SO/Building/P1/*.asset` |
| P1 재고 에셋 | `Assets/Resources/SO/Stock/P1/*.asset` |
| 플레이스홀더 이미지 | `Assets/Images/Placeholders/Facilities`, `Assets/Images/Placeholders/Defense` |
| 검증 | `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs` |

## 1.2 손님 AI

| 영역 | 주요 파일/에셋 |
|---|---|
| AI 선택 | `Assets/Scripts/Character/AI/AIBrain.cs`, `FacilityCandidateScorer.cs` |
| AI 행동 | `Assets/Scripts/Character/AI/Action/AIShopping.cs`, `Assets/Scripts/Character/AI/Action/AIEat.cs`, `Assets/Scripts/Character/AI/Action/AIRest.cs`, `Assets/Scripts/Character/AI/Action/AILookAround.cs`, `Assets/Scripts/Character/AI/Action/AIWait.cs` |
| 고려 조건 | `Assets/Scripts/Character/AI/Consideration/ConsiderationFacilityNeed.cs`, `Assets/Scripts/Character/AI/Consideration/ConsiderationIsVisitable.cs` |
| 소비/방문 상태 | `Assets/Scripts/Character/Ability/AbilityShopping.cs` |
| AI 에셋 | `Assets/Resources/SO/AI/Action/*.asset`, `Assets/Resources/SO/AI/Consideration/*.asset` |
| 검증 | `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs` |

## 1.3 캐릭터 모델

| 영역 | 주요 파일/에셋 |
|---|---|
| 캐릭터 본체 | `Assets/Scripts/Character/Core/Character.cs`, `Assets/Scripts/Character/Core/Customer.cs`, `Assets/Scripts/Character/Core/Shopkeeper.cs` |
| 능력/종족/특성 데이터 | `Assets/Scripts/Character/SO/CharacterModelData.cs`, `Assets/Scripts/Character/SO/CharacterSpeciesSO.cs`, `Assets/Scripts/Character/SO/CharacterTraitSO.cs`, `Assets/Scripts/Character/SO/CharacterSO.cs` |
| 종족 에셋 | `Assets/Resources/SO/Character/Species/*.asset` |
| 특성 에셋 | `Assets/Resources/SO/Character/Traits/*.asset` |
| 검증 | `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs` |

## 1.4 종족 1차 구현

| 영역 | 주요 파일/에셋 |
|---|---|
| 종족 데이터 구조 | `Assets/Scripts/Character/SO/CharacterSpeciesSO.cs` |
| 종족 에셋 | `Assets/Resources/SO/Character/Species/Species_Slime.asset`, `Species_Orc.asset`, `Species_Vampire.asset` |
| 캐릭터 이미지 | `Assets/Images/Placeholders/Characters` |
| 검증 | `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs` |

## 1.5 사장 캐릭터

| 영역 | 주요 파일/에셋 |
|---|---|
| 사장 런 상태 | `Assets/Scripts/Character/Core/OwnerRunManager.cs` |
| 우선 명령 | `Assets/Scripts/Character/Input/OwnerCommandController.cs` |
| 사장 선택 UI | `Assets/Scripts/Character/UI/OwnerSelectionPanel.cs`, `Assets/Prefabs/UI/OwnerSelectionPanel.prefab` |
| 사장 에셋 | `Assets/Resources/SO/Character/Owners/*.asset` |
| 검증 | `Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs` |

## 2. 직원과 작업 시스템

| 영역 | 주요 파일/에셋 |
|---|---|
| 작업 수행 | `Assets/Scripts/Character/Ability/AbilityWork.cs`, `Assets/Scripts/Character/AI/Action/AIWork.cs` |
| 우선순위 데이터 | `Assets/Scripts/Character/Work/WorkPriorityProfile.cs` |
| 직원 불만 | `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs` |
| 우선순위 UI | `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`, `Assets/Prefabs/UI/StaffWorkPriorityPanel.prefab` |
| 검증 | `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`, `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`, `Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs` |

## 3. 재고와 물류

| 영역 | 주요 파일/에셋 |
|---|---|
| 시설 재고 | `Assets/Scripts/Buildings/Facility.cs`, `Assets/Scripts/Buildings/Shop.cs`, `Assets/Scripts/Buildings/SO/StockInfo.cs` |
| 보충/작업 | `Assets/Scripts/Character/Ability/AbilityWork.cs` |
| 초반 수급 | `Assets/Scripts/Operation/OperatingDaySettlement.cs` |
| 재고 에셋 | `Assets/Resources/SO/Stock/P1/*.asset`, `Assets/Resources/SO/Stock/Item/*.asset` |
| 검증 | `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`, `Assets/Scripts/Operation/Editor/OperatingDaySettlementDebugScenarios.cs` |

## 4. 운영일과 피드백

| 영역 | 주요 파일 |
|---|---|
| 운영일 정산 | `Assets/Scripts/Operation/OperatingDaySettlement.cs` |
| 이벤트 알림 | `Assets/Scripts/Operation/EventAlertSystem.cs` |
| 캐릭터 피드백 | `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs` |
| UI 요약 | `Assets/Scripts/UI/BuildingSummaryInfo.cs`, `CharacterSummeryInfo.cs` |
| 검증 | `OperatingDaySettlementDebugScenarios.cs`, `CharacterFeedbackDebugScenarios.cs`, `EventAlertDebugScenarios.cs` |

## 5. 침입과 방어

| 영역 | 주요 파일/에셋 |
|---|---|
| 위협도 | `Assets/Scripts/Invasion/InvasionThreatSystem.cs` |
| 침입자 | `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`, `Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset` |
| 방어 시설 | `Assets/Scripts/Defense/DefenseFacilitySystem.cs`, `DefenseEffectSO.cs` |
| 전투 결과 | `Assets/Scripts/Invasion/InvasionCombatReportSystem.cs` |
| 방어 에셋 생성 | `Assets/Scripts/Defense/Editor/P1DefenseFacilityAssetBuilder.cs` |
| 방어 시설 에셋 | `Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset`, `P1_PoisonPool.asset`, `P1_FireVent.asset`, `P1_LightningPillar.asset`, `P1_IceVent.asset`, `P1_GuardRoom.asset` |
| 검증 | `InvasionThreatDebugScenarios.cs`, `InvasionIntruderDebugScenarios.cs`, `DefenseFacilityDebugScenarios.cs`, `InvasionCombatReportDebugScenarios.cs` |

## 6. 시설 획득, 연구, 합성

| 영역 | 주요 파일/에셋 |
|---|---|
| 시설 상점 | `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`, `FacilityBlueprintSO.cs` |
| 연구 | `Assets/Scripts/Research/BlueprintResearchSystem.cs` |
| 합성 | `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`, `FacilitySynthesisRecipeSO.cs` |
| 합성 UI | `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs` |
| 시설 계보 진화 | `Assets/Scripts/FacilityEvolution/*.cs`, `FacilityEvolutionRecipeSO.cs`, `FacilityEvolutionRuntime.cs`, `FacilityEvolutionRecordRuntime.cs`, `FacilityEvolutionLlmProposalProvider.cs`, `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs` |
| 설계도 에셋 | `Assets/Resources/SO/Blueprint/P1/*.asset` |
| 합성식 에셋 | `Assets/Resources/SO/Synthesis/P1/*.asset` |
| 진화식 에셋 | `Assets/Resources/SO/FacilityEvolution/P1/*.asset` |
| 검증 | `FacilityShopDebugScenarios.cs`, `BlueprintResearchDebugScenarios.cs`, `FacilitySynthesisDebugScenarios.cs`, `FacilityEvolutionDebugScenarios.cs` |

## 7. 도감

| 영역 | 주요 파일 |
|---|---|
| 도감 런타임 | `Assets/Scripts/Codex/CodexSystem.cs` |
| 도감 UI | `Assets/Scripts/Codex/UI/CodexPanel.cs` |
| 검증 | `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs` |

## 8. 내부 위협과 영입

| 영역 | 주요 파일 |
|---|---|
| 단골/영입 | `Assets/Scripts/Recruitment/RegularCustomerSystem.cs` |
| 직원 불만/반란 | `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs` |
| 검증 | `RegularCustomerDebugScenarios.cs`, `StaffDiscontentDebugScenarios.cs`, `StaffRebellionResponseDebugScenarios.cs` |

## 9. 런 변수와 이벤트

| 영역 | 주요 파일 |
|---|---|
| 런 변수 | `Assets/Scripts/Run/RunVariableSystem.cs` |
| 이벤트 알림 | `Assets/Scripts/Operation/EventAlertSystem.cs` |
| 검증 | `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs` |

## 10. 런 종료와 계승

| 영역 | 주요 파일 |
|---|---|
| 사장 사망/런 상태 | `Assets/Scripts/Character/Core/OwnerRunManager.cs` |
| 메타 진행 | `Assets/Scripts/Meta/MetaProgressionSystem.cs` |
| 검증 | `Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs` |

## 11. 오펜스

| 영역 | 주요 파일 |
|---|---|
| 월드맵/정찰 | `Assets/Scripts/Offense/OffenseWorldMapSystem.cs` |
| 원정 편성/자동 결과 | `Assets/Scripts/Offense/OffenseExpeditionSystem.cs` |
| 보상 지급 | `Assets/Scripts/Offense/OffenseRewardSystem.cs` |
| 검증 | `OffenseWorldMapDebugScenarios.cs`, `OffenseExpeditionDebugScenarios.cs`, `OffenseRewardDebugScenarios.cs` |

## 통합 검증

| 영역 | 주요 파일 |
|---|---|
| 통합 러너 | `Assets/Scripts/Editor/ImplementedScenarioDebugRunner.cs` |
| 결과 파일 | `Temp/DungeonStoryImplementedScenarioReport.txt`, `Temp/DungeonStoryImplementedScenarioReport.json` |
| 감사 문서 | `docs/implementation-reports/current-verification-audit.md` |
| 증거 매트릭스 | `docs/implementation-reports/implementation-evidence-matrix.md` |
