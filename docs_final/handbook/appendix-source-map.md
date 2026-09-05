# 부록. 코드와 문서의 근거 지도

이 부록은 핸드북의 주장과 원래 자료를 다시 찾기 위한 지도다. 파일 목록 자체가 설계 권위는 아니며, 현재 구현 판단은 항상 최신 소스와 작성 자산을 우선한다.

파일 위치뿐 아니라 의존 방향, 적용 패턴과 실제 확장 절차를 확인하려면 [코드 아키텍처 문서군](../architecture/code-architecture-guide.md)을 함께 본다.

현재 C# 파일, 콘텐츠 유형, 상태 권위와 구현 상태를 한 번에 검색하려면 [기술·콘텐츠 지식베이스](../knowledge-base/README.md)를 사용한다. 이 부록은 설명용 진입 경로를 유지하고, 전수 파일 목록과 변경 감지는 자동 생성 인덱스가 담당한다.

## 아키텍처 열람 경로

| 확인할 내용 | 설명 문서 |
|---|---|
| asmdef, Composition Root, 등록 모듈과 19개 시스템의 전체 배치 | [전체 런타임 구조](../architecture/08-whole-runtime-topology.md) |
| 상태 가족별 쓰기 권위, 명령, 읽기 투영과 save section | [상태 권위 원장](../architecture/09-state-authority-ledger.md) |
| 생산, 의료, 전투, 시설, 사건과 복원 사이의 인계 및 실패 처리 | [도메인 간 거래와 실패 경계](../architecture/10-cross-domain-transactions.md) |
| 게임·UI 시계, tick, budget, cadence, dirty, revision과 cache | [런타임 스케줄링과 읽기 투영](../architecture/11-runtime-scheduling-and-projections.md) |

## 런타임 구조

| 주제 | 주요 코드 영역·타입 |
|---|---|
| composition root | `Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs`, 런타임 registration module들 |
| 공통 기반 | `Assets/Scripts/Services/Foundation/`, aggregate store, clock, random stream, event bus, path broker |
| 콘텐츠 카탈로그 | `Assets/Scripts/Content/GameContentCatalogSO.cs`, `ResourceGameContentCatalog` |
| presentation | `Assets/Scripts/Views/`와 `Assets/Scripts/Views/UI/Core/`의 presenter·projection·factory |

## 건물·방·환경

| 주제 | 주요 코드 |
|---|---|
| 건물 정의 | `Assets/Scripts/Services/Buildings/SO/BuildingSO.cs` |
| 건물 ability | `BuildingAbilityCollection`과 건물별 ability handler |
| 방 | `Assets/Scripts/Models/Rooms/Core/`, `Assets/Scripts/Services/Infrastructure/Rooms/` |
| 환경 | `Assets/Scripts/Models/Environment/Core/`, `Assets/Scripts/Services/Infrastructure/Environment/` |
| 전기·유체·컨베이어 | `Assets/Scripts/Services/Infrastructure/Industrial/` |
| 시설 합성 | `Assets/Scripts/Models/Synthesis/`, `Assets/Scripts/Services/Synthesis/` |
| 시설 진화 | `Assets/Scripts/Models/FacilityEvolution/`, `Assets/Scripts/Services/FacilityEvolution/` |

## 아이템·생산·경제

| 주제 | 주요 코드 |
|---|---|
| 아이템 작성 | `Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs`, `ItemDefinitionCatalogSO.cs` |
| 공통 아이템 투영 | `Assets/Scripts/Models/Economy/Content/DungeonItemDefinition.cs` |
| 물리 아이템 | `WorldItemRepository`, `ItemTransferService`와 관련 persistence/validation |
| canonical 질량 | `Assets/Scripts/Models/Items/Core/PhysicalMassContracts.cs`, `Assets/Scripts/Services/Items/PhysicalItemMassQuery.cs`, 장비·의복 mass projector |
| 운반과 목적지 질량 입고 | `CharacterCarryInventory`, `WorldItemHaulPlanningService`, `WarehouseMassAdmissionService`, `FacilityBufferMassAdmissionService` |
| 생산 | `Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs`, 생산 aggregate·output capability 계열 |
| 생산 출력 질량과 buffer | `ProductionOutputBufferCapacityProjector`, `ProductionMassExplanationCapabilityRegistry` |
| 농업 | `Assets/Scripts/Services/Economy/CropPlotRuntime.cs`, 작물 ecology/transaction 계열 |
| 축산 | `Assets/Scripts/Services/Economy/Husbandry/` |
| treasury | 고용·임금·조달·계약·overclock·reforge와 `economy.treasury` save section |

## 캐릭터·AI·건강

| 주제 | 주요 코드 |
|---|---|
| 캐릭터 모델 | `Assets/Scripts/Models/Characters/` |
| 캐릭터 응용·AI | `Assets/Scripts/Services/Character/` |
| 식사·물질 | `Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs` |
| 신체 건강 | `Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs` |
| 의료 | `Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs` |
| 수술·질병·번식 | `Assets/Scripts/Models/Medical/`, `Assets/Scripts/Services/Medical/`와 인구 건강·번식 도메인 |
| 야생동물 | `Assets/Scripts/Models/Wildlife/`, `Assets/Scripts/Services/Wildlife/` |

## 연구·전투·캠페인

| 주제 | 주요 코드 |
|---|---|
| 연구 정의 | `Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs` |
| 연구 상태·조율 | `Assets/Scripts/Services/Infrastructure/BlueprintResearch*.cs` |
| 침입 | `Assets/Scripts/Models/Invasion/Core/`, `Assets/Scripts/Services/Invasion/` |
| 원정·공격 | `Assets/Scripts/Models/Offense/Core/`, `Assets/Scripts/Services/Offense/` |
| 세력 | `Assets/Scripts/Models/Factions/Core/`, `Assets/Scripts/Services/Factions/` |
| 캠페인 콘텐츠 | `V20CampaignRuntime`, `V20ContentResolutionService`, `Assets/Scripts/Content/` |
| 메타 진행 | `MetaProgressionRuntime`과 run lifecycle 계열 |

## 저장·검증

| 주제 | 주요 코드 |
|---|---|
| save orchestration | `Assets/Scripts/Services/Infrastructure/Core/Save/`와 도메인 save section |
| aggregate restore | aggregate root store와 restore staging/participant 계열 |
| 물리 검증 | custody, capacity, WIP, destination drain, outbox validation 계열 |
| 물리 아이템 질량 복원 | `PhysicalItemsSaveSection`, `PhysicalItemSaveValidation`, `PhysicalItemMassQuery` |
| 정적/Editor 검증 | 각 도메인의 `Editor/*DebugScenarios.cs`, asset builder, validation |
| PlayMode 검증 | 각 도메인의 `*PlayModeVerifier.cs`. 검증 코드의 존재와 최신 통과 여부는 구분 |

## 설계·밸런스 권위 문서

- [시스템 구현 권위 체크리스트](../system-implementation-checklist.md)
- [전체 게임 밸런스 기준서](../game-design/whole-game-balance-baseline.md)
- [V27 물리 질량·운반 재조정 계획](../game-design/v27-physical-mass-and-hauling-recalibration-plan.md), 읽기 전용
- [전략 빌드·연구·시설 포트폴리오 설계](../game-design/strategic-build-and-progression-design.md)
- [시스템 콘텐츠 통합 설계](../game-design/systemic-content-integration-design.md)
- [시스템 콘텐츠 상세 카탈로그](../game-design/systemic-content-event-catalog.md)

## 기존 문서의 용도 분류

| 경로 | 역할 |
|---|---|
| `docs/DungeonStory_Game_Design_and_Implementation.md` | 과거 종합 설계·구현 설명 |
| `docs/game-design/current-system-inventory.md` | 이전 시점의 시스템 전수 목록 |
| `docs/game-design/*.md` | 시스템별 상세 설계와 역사적 제안 |
| `docs/implementation-reports/` | 단계별 구현 보고와 당시 근거 |
| `docs/qa/` | 수동·구조·UI·확장성 감사 |
| `docs/code-audit/` | 함수 목록, 결합도·리팩터 감사 |
| `docs/generated/` | 자동 생성된 대형 수치·연결 보고서 |

과거 보고서의 `완료` 표시는 해당 시점과 검증 범위에만 적용한다. 현재 worktree의 전체 게임 품질은 최신 근거로 별도 평가한다.

## 근거의 시점과 조사 방법

- 기준일: 2026-08-30
- 방법: C# 소스, Unity YAML 자산, 기존 문서의 정적 교차 확인
- 실행하지 않은 것: Unity 컴파일, 테스트, Play Mode
- 보호 자료: `v27-physical-mass-and-hauling-recalibration-plan.md`는 읽기와 링크만 사용하고 수정하지 않음
