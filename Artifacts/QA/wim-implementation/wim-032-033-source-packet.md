# WIM-032/033 source packet — 세력 계약 수락·물자 납품

기준일: 2026-09-07  
조사 성격: 읽기 전용 원본 조사. 이 패킷 외 코드·에셋·Unity·MCP·컴파일·KB 생성은 수행하지 않았다.

## 실행 권위와 범위

실행 권위는 `docs/game-design/wim-implementation-plan.md`의 P07이다.

- **WIM-032**: authored 계약 목록을 기존 세력 UI에서 보이고, 수락/거절/진행/기한/완료 결과와 불가 사유를 보이며, 같은 계약의 중복 수락·완료를 막는다.
- **WIM-033**: 물자 계약만 실제 목적지까지 예약·운반·도착·납품 수량을 구분한다. 계약 소유 목적지 도착 뒤에만 기존 완료 명령과 중앙 계정 보상을 실행한다. 정보/관계 조건에는 물자 운반을 만들지 않는다.
- 흐름은 `수락 → 실제 물자 예약 → 운반 → 계약 소유 목적지 도착 → 기존 완료 명령 → 중앙 계정 보상`이다. 만료·부분 납품은 기존 계약의 실패 효과와 기존 물리 회수 경로를 따른다. 정의되지 않은 몰수·가격 정책은 추가하지 않는다.

P04→P07→P10 순서와 P08의 P07 물류 재사용도 같은 계획에서 확인했다. 루트 `AGENT.md`, `AGENTS.md`, `docs/game-design/whole-game-balance-baseline.md`를 모두 읽었다. 이번 범위는 authored 수치·BOM·WU/EWU·기한·보상 수치를 변경하지 않는 실행 경로 보정이므로 새 밸런스 기록 대상은 없다. 단, 현재 18개 계약의 실제 부담/기한 검증은 아직 실행 증거가 없으므로 이를 `밸런스 완료`로 보고할 수 없다.

## KB 신선도와 원본 경로

`docs_final/knowledge-base/README.md`의 프로토콜대로 다음 fresh-gated 질의를 먼저 수행했다.

```text
python -X utf8 Tools/Documentation/query_knowledge_base.py --query "faction contract" --area code --area authority --area persistence --area observation --area implementation --limit 12 --format markdown
```

결과는 `stale`이었다. content DB digest는 `90915fcdf4afc3540278ced70f9e77d5e38f302bf02c7884fccdc6b290d7f0f2`, KB digest는 `5c269c1539e33a660649108a6713c7feab6227c2845d2bdc7159458d2cd13862`, 실패 항목은 138개다. 따라서 생성 KB 행은 근거로 쓰지 않고 아래 원본을 직접 대조했다.

시작점은 `Artifacts/QA/wiki-system-audit-2026-09-05/faction-contract-route-review.md`이다. 이 문서는 정적 조사이며 Unity UI/저장 플레이 검증은 하지 않았다는 한계를 그대로 유지한다. 추가로 확인한 원본은 다음과 같다.

- `Assets/Scripts/Content/FactionContractDefinitionSO.cs`
- `Assets/Scripts/Content/V20AuthoredContentContracts.cs`
- `Assets/Resources/SO/V20/Factions/Contracts/*.asset`
- `Assets/Scripts/Services/Run/V20CampaignRuntime.cs`
- `Assets/Scripts/Services/Run/V20ContentResolutionService.cs`
- `Assets/Scripts/Services/Run/V20CampaignApplicationAdapter.cs`
- `Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategic.cs`
- `Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicDetails.cs`
- `Assets/Scripts/Models/Factions/Core/FactionModels.cs`
- `Assets/Scripts/Models/Factions/FactionRuntime.cs`
- `Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs`
- `Assets/Scripts/Models/Economy/Content/RegionalSupplyContractDeliveryOutbox.cs`
- `Assets/Scripts/Services/Economy/Planning/RegionalSupplyContractApplicationAdapter.cs`
- `Assets/Scripts/Services/Items/PhysicalFacilityItemSinkGateway.cs`
- `Assets/Scripts/Models/Economy/Content/EconomyProjectInputOwnerAuthority.cs`
- `Assets/Scripts/Services/Economy/EconomyProjectInputOwnerRuntime.cs`
- `Assets/Scripts/Services/Items/ItemTransferService.cs`
- `Assets/Scripts/Services/Infrastructure/Save/V20ContentSaveSections.cs`
- `Assets/Scripts/Services/Economy/Planning/RegionalSupplyContractSaveSection.cs`
- `Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs`
- `docs/game-design/systemic-content-event-catalog.md`
- `wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md`

## 현재 구현 사실과 끊긴 지점

| P07 구간 | 구현된 사실 | 현재 상태 / 최소 결손 |
| --- | --- | --- |
| authored 목록 | `V20StoryContentCatalog`은 faction별 3개, 총 18개 `FactionContractDefinitionSO`를 보유한다. 모든 현재 asset은 `items` 수량과 `consume: 1`을 완료 요구로 가진다. `kind`, `deadlineDays`, 성공/실패 `V20ContentEffect`도 정의돼 있다. | 목록을 실제 플레이어가 여는 authored 계약 카드로 투영하는 비편집기 생산자는 0개다. asset 자체에는 납품 목적지·부분납품·취소 규칙·영수증 식별자가 없다. |
| 수락 | `V20CampaignRuntime.TryAcceptContract`는 faction 일치, 활성 계약 없음, 완료/실패 이력 없음만 허용하고 `activeContractId`, `activeContractDeadlineAbsoluteDay`를 저장한다. `V21ContentAlertChoiceActionDispatcher`는 `faction-contract-accept` action을 해석하고 관리 인장(administrative seal)을 요구한다. | authored action ID의 비편집기 UI caller는 0개다. 수락 시 실제 물자 예약, 계약 목적지 생성, 운반 요청이 없다. |
| 거절 | 현재 정의·상태·명령에 contract decline이 없다. | P07의 거절 버튼 의미가 정의되지 않았다. `failedContractIds`를 거절에 재사용하면 실패 효과와 재수락 금지를 몰래 부여하므로 사용하면 안 된다. |
| 기존 세력 UI | `OffenseWorldMapPanelStrategicDetails`에는 선택 세력의 실제 명령 UI가 있고 `TryOfferGoodwill`, `TryRequestTrade`, `TryRequestSupply`, `TryCompleteAllianceProject`, `TryRequestReinforcement`를 호출한다. | 이는 `IFactionRuntime`의 Trade/Recruitment/Supply/Reinforcement이며 authored `V20FactionContractKind`(Supply/CrisisResponse/Strategic)와 별개다. 이 패널에 authored 카드를 붙일 수 있으나, 기존 거래/동맹 정책을 authored 계약에 합치거나 재사용하면 안 된다. |
| 현재 완료 | `TryResolveContract`는 세계 스냅샷으로 `completionRequirements`를 만족했다고 판단한 뒤 성공 효과에 `WithConsumedRequirements`의 `ItemConsume`을 더한다. `V20ContentResolutionService`는 모든 재고에서 `DirectPlayerOrder` 예약 후 `atomicItems.TryConsumeReserved`로 소비하고 faction 상태를 publish한다. | 이것은 계약 목적지, 운반자, 도착, 납품 영수증 없이 전 세계 스택을 직접 소비하는 우회 경로다. WIM-033의 핵심 결손이며, UI만 붙여서는 해결되지 않는다. |
| 실제 운반 capability | `IWorldItemStackRuntime.TryRequestItemDelivery`, `ItemTransferService`, facility buffer destination, `IPhysicalFacilityItemBatchTransferGateway.TryCommitTransferPending`와 `Acknowledge`가 존재한다. Regional supply는 이것으로 `FacilityBuffer`의 실제 도착량을 세고 정확한 source stack/수량/그램/commit ID를 가진 outbox를 완료한다. | authored faction 계약은 이 capability를 호출하지 않고, canonical contract-owned destination도 없다. |
| 완료/보상 | 현재 18개 성공 보상은 돈이 아니라 `FactionCampaignStateSaveData`의 rapport/grievance/obligationTokens 등 세력 캠페인 중앙 상태에 적용된다. `GameMoneyAccount`의 regional supply income은 별 시스템이다. | P07의 “중앙 계정 보상”은 현 authored asset에서는 faction campaign 계정이어야 한다. 새 gold·가격·거래 정책을 도입하지 않는다. |
| 기한·부분 납품 | daily evaluation은 `deadline < AbsoluteDay`이면 기존 실패 효과를 적용하고 active 계약을 실패 이력으로 옮긴다. | 부분 도착량을 정확한 계약 목적지 버퍼에서 읽지 않으므로, 현재는 부분 납품/만료가 물리 세계와 연결되지 않는다. 만료 직전 physical transfer pending을 실패 처리하면 reward/penalty가 교차할 수 있다. |
| 취소·운반 중단·화물 | `ItemTransferService` 및 character carry recovery가 운반 화물을 권위로 가진다. 기준서는 Downed/Dead 때 현 위치에 정확한 carried record를 drop하고, 취소/재계획 때 운반물이 삭제·원창고 teleport되지 않아야 한다고 명시한다. `EconomyProjectInputOwnerRuntime.TryRetireDestination`은 `TryReleaseAtOwnerPosition`으로 목적지 버퍼 물품을 실제 위치에 방출한다. | authored 계약은 아직 target owner가 없으므로 이 기존 회수 경로를 사용할 수 없다. 새 화물 보관소·삭제·몰수·원창고 되돌림을 만들면 안 된다. |
| 저장/복구 | `factions.campaign`은 이미 `PhysicalItemsSaveSection` 뒤에 restore되며 `FactionCampaignWorldSaveData` v1을 `PrepareFactions`/`PublishFactions`한다. Regional supply 저장은 pending Transfer outbox와 incoming physical candidate를 source IDs, qty, mass, commit, reason으로 **양방향** join하고 input owner를 복원한다. | faction save section은 현재 physical candidate/input owner를 주입·검증하지 않는다. delivery target/outbox가 없는 상태다. |

정적 확인 수치: authored 계약 18개, 현재 material consume 계약 18/18, public authored action producer 0개, authored 물자 delivery consumer 0개, 이번 조사에서 수행한 실제 Unity UI/저장 round-trip 검증 0개다.

## 최소 구현 경로와 단일 권위

아래는 새 물류 프레임워크가 아니라 기존 contract/capability/haul API를 연결하는 최소 경로다. Regional supply의 **offer 생성, 연구 조건, money income, 가격 정책**은 재사용 대상이 아니다. 재사용 대상은 실제 목적지 buffer, `TryRequestItemDelivery`, exact transfer pending/ack, input-owner lifecycle, physical restore join뿐이다.

| 데이터/전이 | 단일 권위 | 저장/복구 | 소비자 및 금지 사항 |
| --- | --- | --- | --- |
| authored 요구·기한·성공/실패 효과 | `FactionContractDefinitionSO` / `V20AuthoredContentContracts` | asset | UI는 읽기 전용. 수량/보상/기한을 UI 또는 새 정책에서 다시 계산하지 않는다. |
| active/terminal 계약, deadline, contract target descriptor, delivery stage, exact pending Transfer outbox | `V20CampaignRuntime`의 faction campaign aggregate | `FactionCampaignWorldSaveData`를 버전 상승해 raw 필드를 capture/prepare/publish | UI cache는 저장하지 않는다. completion/expiry/retry 모두 이 상태 전이만 사용한다. 별도 “V20 delivery aggregate”를 만들지 않는다. |
| 원물자 reservation·hauler·carried cargo·도착 스택 | existing physical item / `ItemTransferService` | existing PhysicalItems save authority | 계약 상태가 원물자를 삭제·재생성하지 않는다. 이미 운반 중이면 기존 replan/drop recovery가 우선이다. |
| 계약 소유 목적지 및 버퍼 admission | existing `EconomyProjectInputOwnerRuntime` | faction payload descriptor를 `IEconomyProjectInputOwnerRestoreRuntime`로 복원 | `EconomyProjectInputOwnerAuthority`에 `FactionContractDomain`과 canonical destination/terminal reason만 추가한다. `RegionalContractDomain`을 authored faction ID에 빌려 쓰지 않는다. |
| “납품 완료”라는 소비/소유권 이동 | `IPhysicalFacilityItemBatchTransferGateway`의 exact pending transfer receipt | physical-items candidate와 faction outbox의 bidirectional join | 대상 버퍼에 모든 required 물량이 실제 존재할 때만 transfer. stock snapshot 또는 주문 성공은 납품이 아니다. |
| 성공 효과/중앙 faction account | `V20CampaignRuntime.TryResolveContract`의 성공 effect publish, exact receipt 뒤 한 번 | outbox phase가 `PhysicalCommitted → RewardPublished → Completed`를 보존 | 현재 18개에는 money effect가 없다. `GameMoneyAccount`·regional `ContractIncome`·가격 계산을 붙이지 않는다. |

### 수락부터 완료까지의 필요한 전이

1. UI는 선택 faction의 authored 3개 계약과 active/terminal 상태를 query로 읽고, 수락은 기존 admin-seal 권위가 보장된 command로 보낸다. 성공한 수락은 같은 contract ID로 한 번만 active가 된다.
2. 물자 요구(`items` 및 `consume`)가 있는 경우에만 active contract ID 기반의 canonical destination 및 input-owner descriptor를 만든다. 목적지/공간/유효 dropoff/물자/경로 불가 사유는 command result로 반환해 UI가 표시한다.
3. 기존 `TryRequestItemDelivery`가 **실제 source lot**을 예약하고 운반한다. 예약은 소비가 아니다. UI 진행은 contract destination의 `FacilityBuffer`에서 item별 delivered quantity와 outstanding quantity를 계산한다. source inventory 전량이나 carry cache를 progress truth로 저장하지 않는다.
4. 요구량 전부가 contract-owned destination에 실제 도착한 뒤에만 existing transfer gateway로 exact batch transfer pending을 만든다. outbox에는 operation ID, commit ID, reason, source stack IDs, item quantity, mass와 phase를 저장한다.
5. pending receipt가 있으면 daily expiry는 실패 효과를 실행하지 않는다. 같은 receipt는 재시도 시 재전송/재소비하지 않고, 성공 effect publish 후 receipt ack만 재시도한다. 성공 effect publish 전에는 `TryResolveContract(success)`를 호출하지 않는다.
6. physical transfer commit 뒤에는 `TryResolveContract(success)`를 source/receipt matched 상태에서 한 번만 실행하여 existing faction campaign reward를 publish하고 outbox phase를 전진시킨다. ACK까지 끝난 경우에만 terminal success다. duplicate completion command는 reward와 consumption 모두 0이다.
7. 기한 전 만료 또는 정의된 실패는 active→failed 및 **existing failure effects**를 한 번만 적용한다. target를 retire하면 existing `TryReleaseAtOwnerPosition`으로 이미 목적지에 있던 부분 물량은 실제 위치에 release한다. 운반 중 화물은 target intent가 남아 있어도 기존 hauler recovery/replan/drop 규칙이 정한다. 완결되지 않은 carried cargo를 contract failure로 삭제·몰수·원창고 teleport하지 않는다.

`V20ContentResolutionService`의 현 `FactionContractOutcome` global-snapshot + `DirectPlayerOrder` + `TryConsumeReserved` 경로는 물자 authored 계약에는 차단/대체해야 한다. non-material contract가 장래 asset에 생기면 그 요구가 `items.consume`이 아닌 경우에만 기존 requirement evaluation으로 가고 물류/transfer를 만들지 않는다. 현재 18개가 material이라는 사실은 이 분기를 생략할 근거가 아니다.

## 기한, 부분 납품, 취소, 재시도, 화물 회수의 권위

| 경우 | 권위 있는 처리 | P07에서 하지 않을 것 |
| --- | --- | --- |
| 기한 전 일부 도착 | contract destination `FacilityBuffer` actual lots를 item별로 계수하고 UI에 delivered/outstanding으로 표시 | world snapshot 재고량으로 완료 처리하거나 부분량을 별도 UI 숫자로 저장하지 않음 |
| 기한 만료, receipt 없음 | existing `EvaluateDaily` failure 효과를 정확히 한 번 적용하고 contract destination을 retire/release | 부분 납품 물품을 삭제·몰수·원창고로 teleport하거나 새로운 penalty/가격을 적용하지 않음 |
| physical Transfer pending | faction state의 outbox와 physical receipt가 terminal 권위 | 만료 penalty 또는 duplicate transfer를 병행하지 않음 |
| reward/ack 중 재시도 | stored receipt identity로 effect publish 여부를 판별하고, 이미 성공 effect가 publish됐으면 ack만 시도 | 동일 command가 item consume 또는 reward를 다시 실행하지 않음 |
| hauler 취소/경로 불능/Downed/Dead | `ItemTransferService`와 existing carried item recovery; active contract는 미완료 상태로 남아 이후 같은 target에 delivery를 다시 요청할 수 있음 | contract 전용 cargo recovery, 가짜 refund, 임의 보관/폐기를 만듦 |
| 사용자가 계약 자체를 포기 | 현재 정의에 없음. 아래 사용자 결정 전에는 public contract-cancel command를 만들지 않는다. | hauler order cancellation을 contract failure/몰수로 암묵 변환하지 않음 |

## 어셈블리·UI 경로

현재 composition root인 `DungeonCharacterRegistration`은 `V20CampaignRuntime`, `V20ContentResolutionService`, `V20CampaignApplicationAdapter`를 singleton/entrypoint로 만든다. exact transfer/destination capability는 world simulation registration의 existing `RegionalSupplyContractApplicationAdapter`가 이미 참조하는 physical services다.

최소 assembly 원칙은 다음과 같다.

- authored contract 전용으로 새 registry, global scheduler, economy pricing layer를 만들지 않는다.
- 기존 `V20CampaignApplicationAdapter`의 day-start lifecycle 또는 같은 existing V20 application composition에서 delivery request, buffer observation, terminal outbox retry를 구동한다. persistent state는 이 adapter가 아니라 campaign runtime에 남긴다.
- UI는 `OffenseWorldMapPanelStrategicDetails`의 selected-faction command area에 authored contract card/query를 추가한다. `IFactionRuntime` trade/supply commands는 그대로 보존하고 authored card가 그것들을 호출하지 않게 한다.
- action dispatcher path를 계속 쓸 경우, material accept/outcome이 direct stock consumption으로 내려가지 않게 `V20ContentResolutionService`에서 physical-delivery-aware command로 연결한다. UI가 internal dictionary나 raw save data를 mutate하면 안 된다.

## 정확한 구현 수정면 (이번 조사에서는 미수정)

| 파일 | 최소 수정 |
| --- | --- |
| `Assets/Scripts/Services/Run/V20CampaignRuntime.cs` | faction state/save DTO 및 typed query/command에 destination descriptor, delivery phase, exact transfer outbox를 추가한다. accept/expiry/complete의 상태 전이를 receipt-aware하게 만들고 material success가 global `RequirementsSatisfied`만으로 끝나지 않게 한다. `PrepareFactions` validation에 active/terminal/outbox identity와 phase invariants를 넣는다. |
| `Assets/Scripts/Services/Run/V20ContentResolutionService.cs` | `FactionContractAccept`/`FactionContractOutcome`이 material 계약에서 `DirectPlayerOrder`/`TryConsumeReserved`로 빠지는 bypass를 제거한다. administrative seal은 보존하되, delivery-aware receipt-gated command만 publish한다. |
| `Assets/Scripts/Services/Run/V20CampaignApplicationAdapter.cs` | existing lifecycle에서 accepted material contract의 delivery request, destination buffer observation, exact pending transfer→reward→ack retry를 연결한다. adapter에는 cache를 저장하지 않는다. |
| `Assets/Scripts/Models/Economy/Content/EconomyProjectInputOwnerAuthority.cs` | `FactionContractDomain`, `BuildFactionContractDestinationId`, faction-contract terminal release reason을 기존 allow-list/canonical helper에 추가한다. |
| `Assets/Scripts/Services/Infrastructure/Save/V20ContentSaveSections.cs` | `FactionCampaignSaveSection`에 `IPhysicalItemRestoreCandidateQuery`와 `IEconomyProjectInputOwnerRestoreRuntime`을 주입하고, physical pending receipt↔faction outbox 양방향 join 및 faction input-owner descriptor restore를 추가한다. `factions.campaign`의 `PhysicalItemsSaveSection` dependency는 유지한다. |
| `Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs` | 추가한 existing-capability dependencies와 V20 entry path를 등록한다. 새 shared framework나 regional supply offer/income binding은 등록하지 않는다. |
| `Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategic.cs` | selected faction details 구성에 authored campaign query/command를 전달한다. 기존 `IFactionRuntime` UI wiring은 건드리지 않는다. |
| `Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicDetails.cs` | authored 3개 card, accept/reject presentation, deadline, item별 reserved/hauling/delivered/outstanding, disabled reason, terminal result를 render하고 typed command만 호출한다. prefab/asset mutation은 필요 없다. |
| `Assets/Scripts/Services/Run/Editor/V20CampaignDebugScenarios.cs` | 아래 focused deterministic verifier를 existing V20 scenario suite에 추가한다. Regional supply test를 faction contract test로 바꾸지 않는다. |

새 파일이 꼭 필요해지는 경우에도 범위는 `Assets/Scripts/Services/Run/`의 한 개 authored faction delivery application adapter로 제한한다. 그 adapter는 state/store를 새로 소유하지 않고 위 campaign/physical capability만 조합해야 한다. 위 기존 `V20CampaignApplicationAdapter` 확장으로 충분하면 새 파일을 만들지 않는 편이 더 작다.

## 집중 검증 (구현 후, 이번 조사에서 미실행)

1. **사용자 UI 진입**: world map의 selected faction에서 authored 3개 card가 실제로 보이고, disabled reason/기한/현재 진행이 나온다. accept는 관리 인장을 거쳐 active 1회만 만들고 duplicate accept는 0 state mutation이다. 기존 trade/supply command 결과는 변하지 않는다.
2. **물리 납품과 보상**: current asset인 `faction-contract:beastkin:supply`을 canary로, reservation 전/운반 중/목적지 buffer 도착/transfer pending/restore 뒤를 고정 시드로 확인한다. 모든 item이 contract destination에 있을 때만 exact Transfer→receipt→faction success effects→ack 순서가 되고 reward/consumption duplicate는 각각 0이다.
3. **불가 사유**: 물자 부족, delivery dropoff 없음, 경로 단절을 각각 재현한다. active/물자/보상은 의도된 command 단계 밖에서 변하지 않고 UI는 구체 reason을 표시한다.
4. **부분·기한**: item 일부만 destination buffer에 둔 채 deadline을 넘긴다. 완료/보상은 0, failure effect는 1회, retired target buffer 물품은 existing release 위치에 실제로 남으며 운반 중 화물은 삭제/teleport되지 않아야 한다.
5. **운반 중 취소·저장/복구**: hauler cancel/replan 및 carried cargo interruption, 그리고 save/load를 transit와 partial buffer 양쪽에서 수행한다. owner target, physical lots, active phase는 정확히 복원되고 recovery는 existing route가 담당한다.
6. **outbox 재시도/저장**: physical commit 직후, reward publish 직후, ack 직후에 각각 restore한다. physical incoming receipt가 faction outbox owner와 source stack IDs/qty/mass/commit/reason 모두 양방향 일치하지 않으면 restore를 실패시킨다. 정상 receipt의 retry는 transfer/reward를 다시 만들지 않고 남은 phase만 전진시킨다.
7. **미래 non-material guard**: item consume requirement가 없는 fixture 계약은 delivery 주문/contract destination/transfer 0이며 기존 nonmaterial requirement 완료만 허용되는지 확인한다.

## 실제 사용자 결정과 미확인 항목

다음은 코드를 보고 추정하면 안 되는 제품 결정이다.

1. **수락 전 거절의 지속성**: P07은 reject UI를 요구하지만 authored definition에는 거절 효과·재노출·재수락 규칙이 없다. 최소 안전안은 이번 화면에서만 dismiss하고 state/history/effect를 바꾸지 않는 것이다. 영구 거절을 원하면 별도 authored semantics가 필요하며 `failedContractIds` 재사용은 불가하다.
2. **수락 후 계약 포기**: 현재 contract cancel 정의가 없다. P07의 화물 취소/recovery는 hauler/order의 중단을 말할 수 있으나 contract failure 효과로 변환할 권위는 없다. 첫 slice에서는 public contract-cancel을 만들지 않고, 필요할 때만 “포기가 existing failure effect를 적용하는가”를 명시 결정해야 한다.
3. **UI host 확정**: 원본상 가장 가까운 실제 faction UI는 world map `OffenseWorldMapPanelStrategicDetails`이지만, authored V20 contract action의 현 production caller는 0이다. 해당 panel을 P07 host로 승인할지, EventAlert를 host로 할지는 사용자/기획 확인이 필요하다. 어느 쪽이든 `IFactionRuntime`의 별도 거래 정책과 합치면 안 된다.

미확인: 이 조사에서는 Unity 실행, 실제 UI 클릭, haul path, save round-trip, deterministic scenario 실행을 금지되어 수행하지 않았다. KB는 stale이므로 원본 직접 조사로만 결론을 냈다.
