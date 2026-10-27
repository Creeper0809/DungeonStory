# main 코드 구조 재감사 R28

기준: main `c979d18e17f7bc821775079c612b49b1ae5dad43`, 2026-10-27. 작업 branch `codex/audit-structure-r28`.

## 결론과 수정 우선순위

기존 S01~S11의 수정은 실제 원본과 호출 관계에서 확인했다. 그러나 현재 main을 구조 정리가 끝난 코드라고 평가할 수는 없다. 추가로 고쳐야 할 원인은 아래 7묶음이다. R28-03은 같은 원인이 여러 경계에 반복되므로 확인한 대상 21개를 별도 표에 모두 적었다. 파일 길이나 순환 묶음의 크기로 결함 수를 만들지 않았다.

| 순서 | 항목 | 문제 | 판정 근거 |
|---|---|---|---|
| 1 | R28-01 | AI 경로의 상위 캐시가 아래 검색기의 정책 버전 검사를 우회 | 현재 생산 코드에서 오래된 도달 가능 결과를 재사용할 조건 확인 |
| 2 | R28-02 | 성장 상태를 외부 거래와 일반 조회가 직접 변경 | 실제 외부 writer와 조회 중 삭제 확인 |
| 3 | R28-03 | 조회 API가 변경 가능한 저장 상태 원본을 넘김 | 21경계에서 공개 필드와 원본 반환의 연결 확인 |
| 4 | R28-04 | 소비처 인덱스와 실제 소비/배분 지원이 불일치 | 현재 등록 에셋의 소비 누락 3계열과 실행 불가 경로의 조절 UI 확인 |
| 5 | R28-05 | Inspector가 해석한 고정 키 별칭을 그리기 단계에서 버림 | 정상 키를 오류로 분류하는 조건과 선택지 누락 확인 |
| 6 | R28-06 | 일부 로컬 선택기가 미적용 Inspector 값과 다른 원본을 읽음 | anatomy/boss 후보 경로와 최종 검증의 불일치 확인 |
| 7 | R28-07 | 방어 시설의 운영 조건을 특정 건물 번호에 결합 | 동일 정책이 두 서비스에 중복되고 에셋에 정책 표현이 없음 |

우선순위 1~3은 실행 판단과 상태 쓰기 권한을 먼저 바로잡는 대상이다. 4~7은 현재 작성/선택 결과가 잘못되거나 콘텐츠 추가가 공용 코드 수정으로 번지는 대상이다. 이번 조사에서 크래시나 저장 손상을 실제 플레이로 재현한 것은 아니다. 정적 근거로 확인한 구조 결함과 실제 실행 재현을 구별한다.

## R28-01. 경로 유효성의 중복 권위

- [AIBrainPathSearchSession.cs](../../Assets/Scripts/Models/AI/Core/AIBrainPathSearchSession.cs) 28~30행은 grid, 시작점, `TraversalVersion`만 비교한다. cache hit이면 broker를 호출하지 않는다.
- [GridPathSearchBroker.cs](../../Assets/Scripts/Models/Grid/Core/GridPathSearchBroker.cs) 450~452행과 757행 이후는 traversal context, 문 접근 버전, cost-policy 버전까지 키에 포함한다. 위쪽 캐시 때문에 이 검사가 생략될 수 있다.
- [ChildSafetyTraversalRuntime.cs](../../Assets/Scripts/Services/Character/AI/ChildSafetyTraversalRuntime.cs) 329/337행의 전투 위험 overlay 변경은 별도 버전을 올린다. 678행의 생산 cost policy가 이 버전을 사용하지만 grid 버전 변경이나 모든 brain cache 무효화를 함께 수행하지 않는다. [생산 등록](../../Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs) 302행에서 이 정책을 사용한다.
- [OwnerCommandController.cs](../../Assets/Scripts/Controllers/Character/Input/OwnerCommandController.cs) 750행은 같은 세션 결과로 우선 작업을 평가한다. 실패하면 763행에서 반환하며 cache clear는 성공 후 768행이다. 제압 명령도 같은 형태다. [AIPrimitiveSurvivalActions.cs](../../Assets/Scripts/Services/Character/AI/Action/AIPrimitiveSurvivalActions.cs) 94행은 같은 결과로 시설 접근 가능 여부를 판단한다.

현재 조건: 위치와 grid가 그대로인 상태에서 위험 구역이 생기거나 사라지면, 실패한 명령 재시도나 생존 행동 판단이 이전 검색 결과를 사용할 수 있다. 실제 이동의 별도 재검사까지 모두 우회한다고 확대하지 않는다. 문 변경은 기존 adapter가 각 brain cache를 비우므로 이 결함의 재현 조건에서 제외했다.

최소 수정: 결과 재사용 여부는 기존 broker 한 곳이 결정하도록 한다. 세션은 urgency/deferred 수명만 소유하면 된다. 버전 목록을 세션에 다시 복사하는 새 캐시 체계는 만들지 않는다. 위치 고정 상태의 위험 생성/제거, 명령 거절 후 재시도, 정상 cache hit와 deferred를 표적으로 확인한다.

## R28-02. 성장 상태의 외부 쓰기와 조회 중 삭제

- [CharacterProgression.cs](../../Assets/Scripts/Services/Character/Core/CharacterProgression.cs) 59~68행의 `GrowthState`는 원본이다. 82~87행의 스킬/드래프트 조회도 같은 객체를 노출한다. [CharacterSkillModels.cs](../../Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs) 583행 이후의 상태에는 public list, `useLimits`, revision 필드가 있다.
- [CharacterSkillActivationOutcomeTransaction.cs](../../Assets/Scripts/Services/Character/Core/CharacterSkillActivationOutcomeTransaction.cs) 275~278행과 374~378행은 이 원본에 cooldown/사용 횟수를 쓴다. 1002/1370행의 rollback은 `GrowthState.useLimits`를 외부에서 교체한다. 거래 서비스의 명시적 before-state/rollback과 정상 확정 통지는 존재한다. 그것을 제거할 대상은 아니다.
- [CharacterManualSkillRuntime.cs](../../Assets/Scripts/Services/Character/Core/CharacterManualSkillRuntime.cs) 220~233행의 `GetActiveBuffSkills`는 일반 조회인데 원본 목록을 초기화하고 만료 항목을 삭제한다. [CharacterSkillRuntimeEffects.cs](../../Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs) 540행의 효과 계산이 이 조회를 호출한다. 조회 순서가 저장되는 목록의 정리 시점도 결정한다.

변경 위험: 성장 규칙이나 rollback 표현을 바꾸면 Progression, 거래 서비스, 계산용 조회가 함께 수정되어야 한다. getter의 setter를 닫아도 하위 객체 쓰기는 계속 열린다.

최소 수정: 기존 Progression에 사용 제한 적용/복구 명령과 checkpoint를 두고 거래 서비스가 이를 호출한다. 조회는 유효 항목만 골라 반환한다. 시작 파티 preview의 draft 작성도 별도 준비 명령으로 명시한다. DTO 형식과 사실 발행 순서는 유지한다. cooldown 적용 전/후 저장, 실패 rollback, ACK 유실 재시도, 조회 전/후 상태 불변만 검증하면 된다.

## R28-03. 읽기 전용 목록 아래의 가변 저장 원본

`IReadOnlyList<T>`, `AsReadOnly()` 또는 `ToArray()`는 목록 형태만 제한한다. T가 public 필드를 가진 class이고 항목을 복사하지 않으면 조회자가 저장 권위의 원본을 직접 수정할 수 있다. 실제 원본 반환을 확인한 대상은 다음과 같다. 같은 API를 전달하는 facade는 별도 원인으로 중복 집계하지 않았다.

| 대상 경계 | 현재 원본과 위치 | 열려 있는 값의 예 |
|---|---|---|
| 01 문 정책 | [DoorAccessStateModule.cs](../../Assets/Scripts/Services/Buildings/Access/DoorAccessStateModule.cs) 18행, Door.AccessPolicy | AllowedGroups와 개별 허용 규칙. module의 revision 증가/changed 호출을 거치지 않고 변경 가능 |
| 02 건물 시설 상태 | [BuildableObject.cs](../../Assets/Scripts/Services/Buildings/BuildableObject.cs) 122행, FacilityRuntimeState | 연료, 사용 순번, pending/active use 목록. 외부 조회에 전체 쓰기 표현이 노출됨 |
| 03 운반 재고 | [CharacterCarryInventory.cs](../../Assets/Scripts/Services/Items/CharacterCarryInventory.cs) 295행 Items | 수량, 원래 스택과 ownerOperationId, 중첩 component |
| 04 응급 의료 주문 | [CharacterMedicalRuntime.cs](../../Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs) 209행 ActiveOrders, 577행 TryGetOrder | 환자/담당자, 상태와 안정화 결과 |
| 05 수술 주문 | [SurgeryRuntime.cs](../../Assets/Scripts/Services/Medical/SurgeryRuntime.cs) 140행 ActiveOrders, 533/546행 조회 | 단계, 대상 부위, 결과/재료 상태. SurgeryAggregateStateStore의 조회도 writable root를 얻음 |
| 06 작물 생태 | [CropEcologyDomain.cs](../../Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs) 364행, CropEcologyRuntime.Plots | pest/disease pressure, 생사와 중첩 seed/genome. ToArray만 수행 |
| 07 장비 제작 | [CombatEquipmentCraftingRuntime.cs](../../Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs) 121행 Queue, CombatEquipmentRuntime.CraftQueue | completedWork, 재료 영수증, 작업자 정책 |
| 08 의복 제작 | [ApparelWorkOrderRuntime.cs](../../Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs) 123행 Orders | 주문 단계, 물리 소비 결과와 영수증 |
| 09 유료 시설 계약 | [AutoProcurementRuntime.cs](../../Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs) 62행 Contracts, 90행 GetContract | active, dailyCost, lastSettledDay |
| 10 고용/급여 | [EmploymentContractRuntime.cs](../../Assets/Scripts/Services/Economy/Treasury/EmploymentContractRuntime.cs) 112/117행 | 미지급 급여, 정산일과 용병 계약 상태 |
| 11 대형 사업 | [GrandProjectRuntime.cs](../../Assets/Scripts/Models/Economy/Content/GrandProjectRuntime.cs) 216행 State | completedProjectIds, 진행량과 pendingPhysicalCommit |
| 12 외부 사건 | [ExteriorActivityRuntime.cs](../../Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs) 242행 IncidentStates | incidentAggregate의 같은 항목. stage/progress/receptionApplied와 참여 목록 |
| 13 침입 캠페인 | [InvasionCampaignRuntime.cs](../../Assets/Scripts/Services/Invasion/InvasionCampaignRuntime.cs) 92~96/113행 | 지원 거점 생사, 예약 공격일과 started publication 키 |
| 14 원정 지역 | [OffenseRegionRuntime.cs](../../Assets/Scripts/Services/Offense/OffenseRegionRuntime.cs) 182행 Regions | 각 압박 값과 보상 지급 상태 |
| 15 원정 귀환 | [OffenseReturnArrivalRuntime.cs](../../Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs) 237행 Arrivals | 귀환 단계와 materializedIds. private arrivals getter 자체가 writable root를 사용 |
| 16 원정 이동 | [OffenseTravelAndDecisionRuntime.cs](../../Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs) 350/700행 | 현재 좌표, 경로와 남은 이동 상태 |
| 17 긴급 완화 주문 | [OffenseUrgentMitigationRuntime.cs](../../Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationRuntime.cs) 174행 Orders | 진행량, physicalCommitPhase/영수증. ReadOnlyView는 항목 복사를 하지 않음 |
| 18 원정 지도 | [OffenseHexWorldSimulation.cs](../../Assets/Scripts/Services/Offense/Strategic/OffenseHexWorldSimulation.cs) 169~172/232행 | tile의 q/r/blocked, 사이트와 긴급 사이트 상태 |
| 19 세력 | [FactionRuntime.cs](../../Assets/Scripts/Services/Factions/FactionRuntime.cs) 192~201/425행 | 세력의 저장 필드와 경로 항목. trust는 비직렬화 projection이므로 두 번째 관계 저장 권위로 오분류하지 않음 |
| 20 사건/관계 캠페인 | [V20CampaignRuntime.cs](../../Assets/Scripts/Services/Run/V20CampaignRuntime.cs) 161/515행과 [V20SocietyEventRuntime.cs](../../Assets/Scripts/Services/Run/V20SocietyEventRuntime.cs) 29행 | 이벤트 resolved/selectedChoiceId/참여자, 세력 관계 저장 행 |
| 21 가족/묘비 | [PopulationSocialRuntime.cs](../../Assets/Scripts/Services/Character/PopulationSocialRuntime.cs) 116/276/278행, Household/Kinship aggregate 조회 | 방 배정, pending assignment와 묘비 SaveData. 목록 조회가 복사해도 단건 out 조회는 원본 |

이 표는 현재 호출자 모두가 실제로 값을 잘못 쓴다는 주장이 아니다. **공개 조회를 통해 저장 원본에 쓸 수 있는 구조가 확정**됐다는 뜻이다. 조회 결과가 소유자의 원본을 가리키며 그 필드를 수정할 수 있음을 대조했다. 예를 들어 `GetContract(...).active = false`, `Plots[0].pestPressure = ...`, `Queue[0].completedWork = ...`는 해당 소유자의 검증/변경 통지/물리 명령을 거치지 않는다. 실제 손상 저장을 만들어 실행한 결과는 아니다.

실제 소비 예: Production/장비/의복 panel의 주문 표시, TreasuryResourceHudController의 WageStates, PaidFacilityBuildingPanelPresenter의 GetContract, OperationsFeatureQueryService의 IncidentStates, DefenseFeatureQueryService의 침입 일정, 원정 이동/귀환 UI가 이 조회 경계를 사용한다. 모두 Editor 전용 미사용 API는 아니다. 외부 사건 aggregate는 스스로 single mutable owner라고 선언하지만 항목 반환 경계는 그 선언을 보장하지 못한다.

최소 수정: 외부 조회에는 기존 Clone/DeepClone/저장 복사 함수를 재사용하거나 읽기 전용 view를 반환한다. 실제 writer가 필요한 내부 실행은 별도 내부 명령/세션으로 유지한다. DTO 이름과 JSON 필드를 숨기거나 바꾸지 않는다. `AsReadOnly`만 추가하거나 전체 상태를 새 manager로 옮기는 방식은 해결이 아니다. 조회 결과의 scalar와 중첩 목록을 수정해도 canonical capture/revision이 바뀌지 않는지, 기존 명령과 pending 복원이 계속 같은 결과를 내는지를 경계별로 확인한다.

## R28-04. 실제 소비, 소비 정보와 배분 경로의 혼합

[ResourceEconomyContentCatalog.cs](../../Assets/Scripts/Services/Economy/ResourceEconomyContentCatalog.cs) 469~980행의 `ResourceUsageIndex.BuildStaticIndex`는 여러 소비 지식을 별도 목록으로 다시 적는다. 긴 함수라는 이유가 아니라 **현재 등록된 소비와 이미 어긋났기 때문에** 수정 대상으로 삼았다.

| 누락 계열 | 현재 등록 에셋 | 실제 소비 소유자 |
|---|---|---|
| 시설 연료 | D01 간이화덕, ID1000, resource:log. BuildingFuelConsumerAbility와 Temperature가 있고 generic supply는 없음 | SurvivalFoodRuntime.TryApplyRefuelWork -> SurvivalFoodStockRuntime.TryCommitFacilityFuel, 329행 physical sink |
| 발전기 연료 | I01 ID9810 material:low-fuel, I03 ID9812 resource:mana-crystal. requiresFuel=true이며 generic supply 없음 | ElectricalNetworkRuntime 1248/1306행의 배송/연료 sink |
| 골렘 충전 | M02 ID1037 resource:mana-crystal, BuildingGolemRechargeAbility. generic supply 없음 | SurvivalWorkExecutionHandler -> SpeciesRuntime 594행 예약, 720행 증빙 소비 |

모두 현재 GameDomainContentCatalog에 등록되어 있다. 일반 공급 분기가 대신 포착하는지 각 에셋의 능력 목록을 대조했다. I02는 연료 필드가 있어도 requiresFuel=false이므로 소비자로 세면 안 된다. D02/E01/E02/E03/E07의 같은 연료 누락은 별도 원인 수로 늘리지 않았다.

반대 방향의 불일치도 있다. 현재 WS08 ID1607의 Fuel profile은 `facility:1607:supply:fuel`로 등록된다. [생산 등록](../../Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs) 692행의 demand provider 5개는 Recipe/Construction/Equipment/Medical/MarketSale만 지원한다. [ProductionDistributionRuntime.cs](../../Assets/Scripts/Models/Economy/Content/ProductionDistributionRuntime.cs) 673~689행은 FacilitySupply를 `consumer-provider-unavailable`로 차단한다. 그런데 [ProductionRoutePanelPresenter.cs](../../Assets/Scripts/Views/UI/Core/ProductionRoutePanelPresenter.cs) 50~62/92행은 같은 소비 목록으로 선택지와 조절 버튼을 만든다. UI가 차단 이유를 표시하는 것은 확인했으며, 배송 성공으로 위장한다고 보고하지 않는다.

현재 실제 소비의 정보와 생산 배분이 지원하는 경로를 같은 `IsRealConsumer` 조건으로 취급하여 두 방향의 누락/과잉이 발생한다. 최소 수정은 기존 능력에서 소비 정보를 빠짐없이 읽고, 배분 후보/조절 가능 여부에는 기존 provider의 Supports를 반영하는 것이다. 새 범용 registry나 미지원 공급 방식의 전면 구현은 필요하지 않다. 정보 누락 보완만으로 골렘 충전에 새 배송 기능이 생겼다고 완료 처리해서도 안 된다.

## R28-05. 해석한 고정 키를 표시 단계가 버림

[ContentReferenceDrawer.cs](../../Assets/Editor/ContentReferenceDrawer.cs) 41행은 `resolved`를 만들지만 56행의 별칭은 원래 `reference.Aliases`에서 읽는다. 같은 파일 187행 이후는 trait와 규칙 타입에 맞는 정확한 키를 **새 attribute의 Aliases**에 넣는다. 공용 catalog의 FixedTraitRule 후보는 의도적으로 비어 있다.

따라서 trait301의 `extreme:last-stand` ruleId, trait213의 `need:ritual-fast` needId 같은 정상 키가 valid=false/끊어진 참조 표시로 내려간다. 빈 미게시 필드에는 선택해야 할 고정 키가 나타나지 않는다. 기존 게시 값의 변경 보호를 뚫는 문제는 아니다.

최소 수정: 후보, 별칭, 현재 값의 판정이 같은 resolved 계약을 사용하게 한다. 기존 테스트가 `resolved.Aliases`와 값 보존까지만 확인하고 실제 옵션/오류 표시를 확인하지 않는 경계도 보완한다. 정상 ruleId/needId와 빈 미게시 행만 표적으로 검사한다.

## R28-06. 미적용 값과 CLR 원본을 혼용하는 로컬 선택기

[ContentReferenceDrawer.cs](../../Assets/Editor/ContentReferenceDrawer.cs) 306~312행은 anatomy parent 후보를 `profile.Nodes`에서, boss phase의 능력 후보를 `enemy.abilityIds`에서 읽는다. 이 값은 현재 Inspector의 미적용 SerializedProperty와 다를 수 있다. 같은 파일의 encounter, age-condition, service-hub, owner-scoped 선택기는 이미 pending 값을 따로 읽는다.

현재 조건: 같은 편집 안에서 로컬 node ID/부모 또는 enemy ability 목록을 변경하고 Apply 전 하위 참조를 고르면, 삭제한 항목이 남거나 새 항목이 빠진 후보를 얻을 수 있다. anatomy parent에는 자기 node도 후보로 들어가지만 [최종 검증](../../Assets/Scripts/Services/Items/ContentReferenceCatalog.cs) 781행은 self-parent를 거절한다. 즉 선택기와 검사기가 서로 다른 유효 범위를 제시한다. 실제 Inspector 클릭 재현은 이번에 실행하지 않았다.

최소 수정: 이미 있는 pending owner 복사/판정 경계를 재사용하고 anatomy parent만 자기 node를 제외한다. 모든 AnatomyNode 참조에 자기 제외를 적용하면 부착점 등 다른 정상 참조를 막을 수 있으므로 사용 문맥을 구별한다. 새 행, rename, 삭제, Undo와 boss ability 목록 변경만 확인한다.

## R28-07. 특정 건물 ID에 묶인 방어 운영 정책

- [DefenseFacilityRuntime.cs](../../Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs) 194행은 building.id==1805일 때만 자동 제어 지원을 요구한다.
- [DefenseFacilityNetworkRuntime.cs](../../Assets/Scripts/Services/Defense/DefenseFacilityNetworkRuntime.cs) 125~129행도 1805에 같은 예외를 둔다.
- 현재 1805는 DefenseWallLauncher이고 `facilityFamilyId=defense:launcher`다. [DefenseFacilityData.cs](../../Assets/Scripts/Services/Defense/DefenseFacilityData.cs)는 power/supply/family 정책을 갖지만 자동 제어 요구는 표현하지 않는다.

현재 한 정의의 행동은 성립하지만, 같은 운영 조건의 새 시설을 다른 ID로 추가하면 두 공용 서비스를 함께 수정해야 한다. ID는 정의의 신원인데 운영 정책의 소유자 역할까지 맡는다. 이는 줄 수나 취향 문제가 아닌 현재의 변경 결합이다.

최소 수정: 기존 방어 정의에 이 요구를 표현하고 두 경로가 같은 판정을 사용하게 한다. 기존 1805와 다른 시설의 행동을 보존하고 제어 링크 없음/있음 및 이전 저장의 정의 결합을 확인한다. 새 제어 전략 클래스나 family 전체의 행동 변경은 필요하지 않다.

## 이전 수정과 비수정 판정

- S01의 일일 구매 권위/저장, S02의 Brain 상태 외부 setter 차단, S03 장착 준비 정책, S04 효과 대상 매핑, S05 저장 능력 marker, S06 scoped 이동 증빙, S07 시설 소비 실행, S08 배송/조립 소유자, S09 루트 참조, S10 서비스 소유 판정, S11 UI helper 단방향을 현재 원본에서 확인했다. 이번 새 항목은 이 완료 표의 문구를 재사용한 것이 아니다.
- 그래프에서 제작 presenter -> 개량 panel은 남고 반대 helper 의존은 없다. 배송 owner -> 사건 서비스의 역참조도 없다.
- CharacterActor/BuildableObject의 많은 참조는 공용 실행 객체라는 역할도 반영한다. 이 숫자만으로 거대한 인터페이스 이관 작업을 요구하지 않는다.
- V21ContentEffectExecutionRegistry와 handler 파일의 큰 상호 참조 중 다수는 같은 파일의 handler 계약 타입 참조다. 타입별 적용 등록이 이미 존재하므로 파일 그래프의 양방향만으로 공용 효과 분기가 되살아났다고 판정하지 않는다.
- ledger, physical receipt, ACK, 복원 preflight와 교차 저장 join의 분기는 실제 원자성/호환 계약이다. 검사나 영수증을 줄여 복잡도 수치를 낮추지 않는다.
- WorldItemStackRuntime, CharacterBodyHealthRuntime, CaptivityRuntime, SurgeryRuntime은 크기 자체로 분리하지 않는다. 수술 등의 공개 조회 문제는 R28-03에서 다루며 클래스 전체를 재분할할 근거로 확대하지 않는다.
- 포획 query, 동물 사육 query, 장비 수리/재단련 query, 과부하 상태와 물리 영수증 조회에는 이미 Clone 경계가 있다. household의 목록 조회도 복사하므로 원본을 내주는 단건 조회와 구별했다. RegionalSupplyContract의 ContractView도 원본 계약과 분리된 복사본이다.
- Codex/Meta/Blueprint의 상태 객체는 스스로 명령과 불변식을 소유하는 도메인 모델이다. State라는 이름이나 반환 타입만 보고 public 필드 DTO와 같은 결함으로 세지 않았다.
- 직원 관리 UI의 일부 고정 설명 문구는 SO 설정과 변경 결합이 있지만 현재 값은 일치한다. 필수 수정 목록에 넣지 않았다.

## 조사 범위와 증거

| 구분 | 실제 확인 범위 |
|---|---|
| 게임 C# 목록 | 4,079개: Runtime2,985, embedded Editor1,034, root Editor42, Tests18 |
| 정적 지표 | 기존 ArchitectureMetricsAnalyzer의 현재 소스 Verify PASS, baseline 무변경. 큰 타입14/큰 생성자11은 검토 단서만 사용 |
| 의미 의존 그래프 | default assembly 게임2,200파일, metadata427/누락0. edge18,748, 순환 SCC21, 최대 묶음1,004파일 |
| 명시적 asmdef | 게임45개, 선언 참조 순환0 |
| 클래스 탐색 | 기존 Roslyn inventory를 현재 원본에 실행. class 선언8,026개, 메서드 분기/필드/조회/생성자 후보 수집 |
| 직접 검토 | AI 검색/행동/이동, 성장 발동/복구, 의료/수술, 소유 상태 조회, 물리/운반/제작, 경제 소비/분배, 사건/세력, 저장 조립, UI와 SO 작성 경계 |
| 에셋 대조 | 위 소비 능력과 해당 domain/root catalog, 방어 발사구, 고정 trait key와 owner-local 정의 |
| KB | freshness 성공. 여러 심볼을 묶은 첫 검색0건은 부재 근거로 쓰지 않았고 영역별 조회와 원본으로 보완. 인덱스 재생성 없음 |
| 코드 보존 | 게임/Editor/Tests/Architecture/분석 도구 입력8,553개 SHA를 조사 전후 대조 |
| 실행 경계 | 외부 폴더에서 정적 분석 도구 실행. 이번 조사에서 Unity 게임 컴파일/Play/저장 복원은 실행하지 않음 |

첫 그래프는 과거 Bee 입력의 이동 전 파일 때문에 project-fallback으로 내려가 Editor를 포함했다. 그 수치는 최종 판단에서 제외했다. 최종 그래프는 기존 Bee의 옵션/참조를 그대로 보존하고 현재 default 소유 파일로 source 목록만 맞춘 외부 rsp를 사용했다. 추가3/삭제1은 R27 실제 이동과 새 실행 소유자에 일치한다. 과거/부분 그래프를 같은 입력의 결과로 취급하지 않는다.

외부 증거 폴더: `C:/Users/vulpo/Documents/Codex/2026-10-27/structure-audit-r28`. `coverage.json`, `metrics.json`, `current-main-graph.json`, `classes.json`, `input-files.json`, `verification.json`과 3개 독립 근거 메모를 보존한다. SDK 구현은 수정 대상으로 조사하지 않았으며 게임의 SDK 사용 경계만 포함했다. 원본 Documents checkout의 별도 변경은 섞지 않았다.

전체 입력의 정적 수집과 고위험 경계의 원본 검토를 수행했다. 모든 함수의 의미와 모든 실행 조합을 완전 증명했다는 뜻은 아니다. 이 보고서는 현재 확인한 필수 수정 대상을 제시하며, 코드/에셋 수정이나 해당 수정의 실행 PASS를 대신하지 않는다.

## R28 수정 구현

수정 branch는 codex/fix-structure-r28이다. 위 본문은 c979d18e1의 감사 결과이며 아래는 그 원인에 대한 구현 기록이다. 저장 DTO의 필드와 버전, 기존 ID/영수증 형식은 유지했다.

| 항목 | 반영한 동작 |
|---|---|
| R28-01 | 세션의 별도 경로 결과 캐시를 제거했다. 기존 broker가 위험 비용과 접근 정책 버전, 정상 재사용과 deferred 재시도를 결정한다. |
| R28-02 | 성장 조회는 복사본을 반환하고 발동과 복구는 성장 소유자의 명령을 사용한다. 버프 조회 중 삭제를 제거했다. 이름과 요청 버전은 불변 값만 읽어 전체 상태의 반복 복사를 피한다. |
| R28-03 | 표의 21개 조회 경계를 깊은 복사로 보호했다. 의료/수술 명령의 반환 객체도 복사한다. 변경 명령 후에는 필요한 곳에서 ID로 현재 상태를 다시 조회하며, 의료 사건 연결도 소유자의 비교 명령으로 변경한다. |
| R28-04 | 실제 시설 연료, 전력 연료, 골렘 충전 소비를 인덱스에 포함한다. 배분의 실제 provider 지원 여부와 UI 조작 가능 여부를 같은 조회로 판단한다. 기존 비지원 정책은 저장에서 보존하고 UI는 조작을 막는다. |
| R28-05 | Inspector가 해석한 고정 키 별칭을 실제 옵션과 유효성 표시에도 사용한다. |
| R28-06 | anatomy/boss 선택기가 미적용 SerializedObject 값을 같은 소유자 문맥에서 읽는다. 부모의 자기 참조 거절, 부착 관계, 혼합 선택과 Undo를 유지한다. |
| R28-07 | 자동 제어 요구를 방어 정의의 requiresAutomaticControl로 표현한다. 기존 1805 에셋과 생성기만 true로 설정해 현재 동작을 유지한다. |

21개 경계의 통제 검사는 R28AccessAndEcologySnapshotDebugScenarios의 3개, R28OwnedQuerySnapshotDebugScenarios의 8개, R28RemainingQuerySnapshotDebugScenarios의 10개로 나눴다. 조회 결과의 스칼라와 중첩 목록/배열 변경이 원본을 바꾸지 않는지, 기존 명령이 실제 소유 상태를 바꾸는지 확인한다. 성장 발동 원자성, JSON 복원과 명시 재시도는 기존 시나리오를 사용한다.

Editor 준비 코드의 직접 설정은 명시적 fixture 초기화 또는 기존 소유자 명령으로 옮겼다. 실제 UI/AI 동작을 흉내 내는 player 쓰기 API를 추가하지 않았다. 원장 rewind와 고의 손상 반례는 Editor의 정확한 private 상태 관측/주입을 사용하며 기존 실패 기대를 유지한다.

실행 증거는 각 native 실행의 실제 source label을 보존한다. 관련 코드가 바뀌지 않은 통과 결과와 최종 소스 실행 결과를 구분하며, 실패/부분 결과를 최신 PASS로 바꾸지 않는다. 최종 실행과 GitHub 통합 상태는 검증 마감 후 아래에 추가한다.


## R28 실행 검증 마감

다음 8개 native 실행은 모두 exit0, C# 오류0, console 오류0, 입력 SHA drift0이며 전용 Unity/display/compiler 종료까지 확인했다. 통제 조회 검사는 실제 플레이와 구별한다.

| 실행 | 실제 source | 시간 | 확인한 범위 |
|---|---|---:|---|
| query-contracts | r208r28 | 230초 | 조회 21경계, 경로 정책, 의료/수술/시설 상태 복원 |
| authoring-contracts | r208r28 | 70초 | 실제 Inspector, pending/별칭/Undo, 소비/UI 지원, 방어 정책 |
| progression-contracts | r208r28 | 150초 | 성장 발동 원자성, JSON 복원, 실패/관측 ACK/재시도 |
| first-play | r208r28 | 150초 | 실제 첫 진행, 상점, 보관/운반, 연구 |
| cold-shop | r208r28 | 100초 | 새 Unity의 Title LoadGame, 104섹션, 같은 slot 재로드 |
| skill-targeting | r208r28 | 80초 | 실제 UI/카메라/physics callback, invalid retry, 한 번 적용 |
| autonomous-medical | r218r28 | 260초 | 간이침대 실제 AI 운반/두 회차 치료/완전 회복 |
| medical-transfer | r218r28 | 140초 | 실제 식사/쓰러짐/Society/AIRescue, JSON 복원/영수증 |

실행별 원문과 출처는 [R28 검증 manifest](../../Artifacts/QA/StructureR28/verification.json)에 있다. 앞의 6개 실행은 r208의 실제 소스 증거로 유지했다. r208과 최종 r218 입력21746개의 차이는 CharacterAiAutonomousMedicalPlayModeVerifier.cs와 Wim040ObservedIncidentLiveRunner.cs 두 Editor fixture뿐이다. 게임 Runtime, 에셋, 설정 및 나머지 입력은 동일하다. manifest는 원래 label/digest와 정확한 차이를 보존하며 allRunsAtFinalSource=false로 표시한다. 마지막 의료2개와 필수 영향 CI는 최종 소스를 사용한다.

자동 의료는 기존 부상과 약품 총 예산7, 실제 owner-issued 공급 목적지, 공식 작업 우선순위로 준비한 구조 전담 조건이다. 기존 fixture의 AI 행동 목록 강제 축소를 제거하고 정상 행동 목록을 유지했다. 다른 직원 AI가 정지된 통제 조건의 운반 경쟁은 Haul Off/Rescue Priority1으로 명시하며 cleanup에서 원래 Haul 우선순위를 복원한다. 자연 운반과 작업 경쟁의 전수 증거로 확대하지 않는다.

간이침대는 같은 부상에 두 치료 회차가 필요했다. r217은 첫58.08 완료와 둘째34.10/51.48449까지 진행했고 시작1/실패0/불변식0/누수0이었지만 기존75초 예산에서 완전 회복하지 못해 FAIL로 남겼다. 이 실측을 근거로 시나리오 관측 상한을120초로 조정했다. 설정15초와 entry 전체180초, 원래 회복/typed completion/실패0/불변식0/console0 판정, 게임 치료 수치와 Runtime timeout은 유지했다. 최종 r218은 약품2회 실제 소비, 의식/이동0.39와 Active 복귀, 완료1/실패0/불변식0/누수0을 통과했다. 관측 예산 변경을 검사 수치 불변으로 표현하지 않는다.

의료 사건 검사는 실제 오염된 식사의 물품-1, body collapse, 기존 방문객/출발 영수증과 Society 소유권, AIRescue의 M11 도착 및 효과 한 번 발행을 검사했다. 현재 공식 in-memory 저장/JSON/복원과 잘못된 소유자 원자적 거절을 포함한다. 디스크 slot load는 별도 cold-shop 실행으로, 완전 치료는 별도 autonomous-medical 실행으로 확인했다.

성장과 21개 조회 경계는 저장 DTO 필드/버전/기존 ID와 영수증 형식을 바꾸지 않았다. 실제 명령/rollback의 쓰기 권위를 유지하며 외부 변조가 소유 상태를 바꾸지 않는지 확인했다. 구조 baseline/ownership overrides와 기존 analyzer, Python CI의 판정은 변경하지 않았다. Python 표적100개와 analyzer 재현/구조 Verify PASS를 유지하고, 최종 생성 인덱스의 내용/링크/freshness 실패는0이다.

최종 GitHub 통합은 현재 소스의 영향 CI5와 게시 ID/변경 범위 검사를 완료한 뒤 진행한다. GitHub PR과 Actions의 실제 결과를 최종 상태로 사용한다. 전체 게임 조합, 자연 밸런스, 모델 추론 또는 모든 UI의 전수 인증은 이 검증 범위가 아니다. 이전 실패와 수정 근거는 progress.md/findings.md 및 외부 structure-remediation-r28 실행 폴더에 보존했다.


## R28 최종 영향 CI

현재 공유 조회/저장/다중 도메인 변경에 필요한 기존 영향 CI5를 최종 소스 r218에서 실행했다. 모두 exit0, 컴파일 오류0, console 오류0, 전체 입력21746개 drift0, 전용 Unity/display/compiler 종료를 확인했다. 기존 r199 수용 manifest는 바이트 그대로 별도 보존하고 [현재 CI 증거](../../Artifacts/QA/ImpactCI/verification.json)를 새로 기록했다.

| 묶음 | 실제 source | 시간 | 결과 |
|---|---|---:|---|
| core | r218r28 | 250초 | PASS |
| character | r218r28 | 190초 | PASS |
| effects | r218r28 | 1170초 | PASS |
| crop | r218r28 | 200초 | PASS |
| world | r218r28 | 250초 | PASS |

[현재 전체 월드 복원 원문](../../Artifacts/QA/StructureR28/r218r28-ci-world-full-report.txt)은 등록/캡처/복원104섹션 일치, 건물138개/캐릭터3/원정1/침입1, canonical baseline 복구와 진행/방어/침입 rollback/retry를 확인한다. consoleWarnings=0, consoleErrors=0이다. starting party는 공식 명령으로 준비한 통제 조건이며 학습 데이터나 사람의 승인으로 표시하지 않는다.

현재 script digest는 `8c4bfa289549fb0d96792195436dbb6a348a2951b22318b34007988ba73b8d89`, Unity configuration digest는 `ef88068560f78103e54f07311493fe05cb6a4889ec9586c88af0362591990ebb`다. Git 게시 때 동일 코드/입력과 증거 바이트를 대조한다. source edit와 Unity verify는 모두 닫혔으며 이후는 GitHub 통합 단계다.
