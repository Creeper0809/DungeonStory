# V27 물리 중량·운반량 전수 재조정 구현 계획

## 0. 문서 상태와 목적

사양 상태: `Revision v6 / 미래 콘텐츠 확장 폐쇄·다형적 capability registry·수직 배치·계열 자동 authoring / 과거 save migration 제외`

구현 상태 스냅샷: `Foundation/Items gram 권위·공용 mass query·25kg nominal carry·L01 gram warehouse·warehouse-local admission·restore/lifecycle·equipment/apparel/carcass mass·conveyor ingress·packaged anesthetic/vial·typed disposition/WIP 기반의 다수 도메인 경계까지 structural evidence 확보 / current source inventory는 414 ledger items·355 recipes·356 active facilities·21 deprecated compatibility facilities·377 generated facility kits·1,074 serialized physical weight sites / semantic current-source expectation 363/414, unresolved 51 / exact recipe mass contract 42/355 / 실제 표준 생산→FacilityBuffer→AIHaul→warehouse 증거는 P17 hay-feed 한 경로 / 전수 authoring·비창고 경계·live fault matrix·EWU/가격·6인 폐쇄 루프·최종 다중 seed는 미완료`

2026-08-21 구현 갱신: exact-lot retail/current-format restore/실제 Shopping·Restock은 완료했다. 물리 제거는 production 직접 raw consume 0으로 줄였고 typed single/batch Transfer·Sink 및 atomic multi-input Transform으로 이관했다. Production V8은 cycle별 WIP input commit ID·수량·gram과 작업 완료 시 한 번 결정한 resolved output vector를 저장하며, output block 뒤 다른 RNG seed로 복원해도 결과를 재굴림하지 않는 focused 회귀를 통과했다. Physical V10 pending batch receipt는 WIP input과 manual clean-water lot의 save/retry/ack exact-once를 닫았고, Production V9 pending output commit은 표준 FacilityBuffer 출력과 의복·수술 부품 custom handler의 physical commit→aggregate advancement crash window를 닫았다. Production V11은 WIP input, cycle clean-water/wastewater gram, 이미 커밋된 output gram과 남은 명시적 손실을 하나의 terminal 보존식으로 저장해 부분 출력 후 취소·시설 소실도 exact하게 종결한다. current-format 왕복과 1g 변조 rollback, 실제 유체 서비스 합산을 검증했다. linked-support piped fluid는 모든 demand를 mutation 전에 수집해 하나의 deterministic network batch로 preflight/commit하며, 합산 부족은 water·wastewater·revision을 전혀 바꾸지 않고 성공은 한 revision으로 전량 반영한다. 수동 clean-water fallback은 exact `resource:clean-water` source slice를 pending Transfer로 넘기고 Fluid V5·Production V12에 operation/commit/source stack/gram을 저장하며, 같은 카테고리 decoy·충돌 retry·중복 reserve credit을 거절한다. Production V13은 43개 authored 폐수 source를 `SanitaryWashwater/FoodProcessWashwater/Whey/Brine/FermentationEffluent/MedicalEffluent/IndustrialEffluent/AgriculturalRunoff`로 분류하고 recipe·facility·support stable owner와 exact gram을 active WIP 및 terminal receipt에 저장한다. 실제 유체망은 capacity용 hydraulic volume을 합산하되 production 경제 provenance는 조성별로 보존하며, 합계 불일치·중복 key·1g 변조는 mutation 전에 거절한다. 남은 single-stack/terminal disposition outbox 이관과 조성별 실제 처리 시설 선택·슬러지/off-gas downstream disposition은 아직 `structural-partial`이다.

이 문서는 **사양 완전성**과 **구현 완료**를 분리한다. 아래 계약이 결정됐다는 이유로 runtime migration이나 밸런스 적용이 끝났다고 보고하지 않는다. 현재 worktree에 존재하는 구조 변경도 각 slice의 모든 exit gate를 통과하기 전에는 `structural-partial`이다.

### 0.0 Ship-first 범위 교정

V27의 현재 목표는 게임 출시를 위한 **플레이 가능한 물리 중량·운반·경제 밸런스**를 먼저 완성하는 것이다. 기존 문서에 포함된 모든 전수 증명과 미래-content certification을 한 번에 완료하려 하지 않는다.

이 절은 현재 실행 우선순위와 Ship P0 완료 범위에서 §0.1 및 Batch A~H의 exhaustive exit gate보다 우선한다. 기존 gate는 삭제하지 않고 P1/P2 backlog의 권위로 유지한다.

`Ship P0` 완료선:

1. 모든 현재 gameplay item이 positive canonical gram을 가지며 carry·warehouse·FacilityBuffer가 같은 질량 권위를 사용한다.
2. 일반 운반이 목표 묶음 범위에서 동작하고, cancel·Downed·대표 restore에서 삭제·복제·순간이동이 없다.
3. Trade/Supply 외부 유입의 무료 현금화 악용을 막고 unresolved market Critical을 0으로 만든다.
4. 6인 음식·물·저장·성장 노동 폐쇄 루프와 대표 공간 배치가 통상 플레이에서 성립한다.
5. current-source compile, 핵심 focused PlayMode, 최종 3-seed와 Console `0/0`을 통과한다.

다음은 `Hardening P1/P2`로 이동하며 Ship P0를 막지 않는다.

- 모든 261 open Transform의 현실 질량 설명을 개별적으로 완성하는 작업.
- 모든 92 producer에 대한 `32 seed` 자연 clearance 전수 계측.
- 가능한 모든 fault 조합의 완전 교차행렬.
- 64 paired clutter와 256 layout seed의 반복 인증.
- 모든 미래 콘텐츠 family의 synthetic canary와 형식적 확장 폐쇄 증명.
- p95 2ms·0B 같은 비출시 hot path의 미세 성능 증명.

P0 구현에서도 콘텐츠 ID 분기, 이중 권위, silent fallback과 상태 손상은 허용하지 않는다. 단, 현재 출시 경로와 관계없는 전수 인증을 끝낼 때까지 기능 완료를 보류하지 않는다. 남은 P1/P2는 별도 backlog와 증거 상태로 기록한다.

### 0.1 Revision v6 실행·확장 권위

이 절과 `16.1~16.8`이 앞으로의 작업 순서·병렬화·확장 설계·완료 보고에 대한 최상위 실행 권위다. Revision v6는 Revision v5의 Batch A~H critical path를 유지하면서, 각 배치가 현재 콘텐츠만 통과하는 전용 구현으로 끝나는 것을 금지한다. 뒤의 Phase 0~9와 구현 체크포인트 22~98은 세부 계약과 당시 증거를 보존하는 append-only 역사이며, 과거 배치 순서를 다시 직렬화하거나 콘텐츠별 분기를 정당화하는 근거로 사용하지 않는다. 서로 충돌하면 다음 우선순위를 적용한다.

```text
현재 source 재캡처
> Revision v6 capability 확장 계약
> Revision v5에서 승계한 Batch A~H와 exit gate
> 불변식·원자성·save/restore 계약
> 과거 Phase 순서와 당시 분모
```

#### 미래 콘텐츠 확장 폐쇄의 완료 정의

V27의 목표는 현재 414개 item과 355개 recipe를 한 번 통과시키는 데 그치지 않는다. **미래 콘텐츠가 이미 정의된 capability 범위에 속하면 코어 코드의 추가 설계·분기·저장 DTO 변경 없이 authoring data와 선언적 등록만으로 실제 생산·물류·저장·경제·UI/AI 경로에 참여해야 한다.** 완전히 새로운 행위도 기존 공용 인터페이스의 새 구현과 descriptor 추가로 연결하며, 기존 코어의 콘텐츠 ID 분기나 도메인별 재설계는 허용하지 않는다.

확장 등급:

| 등급 | 예 | 허용 변경 | 완료 조건 |
|---|---|---|---|
| `ParameterContent` | 같은 물리·생산 의미의 새 원료, 식사, 시설, 레시피 | SO/builder authoring과 stable ID | runtime/save/UI/AI/core diff 0 |
| `ComposedContent` | 기존 Source/Transform/Sink, 저장, 운반, output capability의 새 조합 | authoring + capability descriptor | 기존 registry와 projector가 자동 수집 |
| `NewCapabilityImplementation` | 기존 인터페이스로 표현되는 새 output policy, mass projector, sink disposition | 새 Strategy/Policy/Handler + 선언적 등록 + 공용 contract fixture | 기존 Aggregate/codec/composition의 콘텐츠별 분기 0 |
| `InvariantChange` | 새로운 소유권 차원, 기존 gram 보존식으로 표현되지 않는 물리 법칙 | 명시적 Revision 개정 | 사용자 설계 결정과 영향 그래프 승인 전 구현 금지 |

다형성·추상화 불변식:

1. item/recipe/facility stable ID, 이름 prefix, 에셋 경로, catalog index를 검사하는 `if`/`switch`를 생산·운반·저장·EWU 코어에 추가하지 않는다.
2. 변화 축은 typed capability interface로 표현한다. content definition은 capability ID와 canonical parameters를 선언하고, runtime은 registry에서 Strategy/Policy/Handler를 해석한다.
3. registry는 build/Editor 단계에서 결정론적으로 생성하거나 명시적 descriptor를 읽는다. gameplay runtime reflection, 이름 추론, optional null handler와 유사 콘텐츠 fallback은 금지한다.
4. 저장은 상위 DTO에 콘텐츠별 필드를 추가하지 않는다. 가변 capability는 stable implementation ID, schema version, canonical state envelope와 codec을 사용하며 누락·중복·unknown 구현은 publish 전에 거부한다.
5. 새 콘텐츠는 mass query, warehouse admission, FacilityBuffer capacity, haul planner, recipe conservation, EWU/price dependency graph, UI와 AI 목록에 catalog enumeration으로 자동 진입한다.
6. 새 capability 구현은 동일한 공용 contract suite를 자동 실행한다. 구현마다 새로운 테스트 구조를 설계하거나 core fixture에 ID allowlist를 추가하면 extension-closed가 아니다.
7. 추상화는 실제 도메인 불변식과 변화 축을 소유해야 한다. 미래를 추측한 빈 interface, one-method forwarding wrapper와 manager 증식은 확장성으로 세지 않는다.
8. capability 계약이 바뀌면 source digest와 dependency graph가 영향받는 정의만 자동 재검증한다. 무관한 시스템 전체 재설계 또는 수동 목록 갱신은 금지한다.

필수 확장 증거:

```text
synthetic canary authoring
→ generated registry/manifest 포함
→ 실제 producer/consumer
→ gram capacity와 custody
→ current-format save/restore
→ UI/AI와 EWU projection
→ 기존 공용 fault fixture
```

- 각 capability family마다 최소 하나의 synthetic canary를 기존 계약만으로 추가한다.
- canary 추가에서 production core source diff는 `0`이어야 한다. 허용 diff는 authoring, 선언적 descriptor, generated registry/manifest와 canary evidence뿐이다.
- canary를 제거한 두 번째 생성도 고아 registry/state/asset을 남기지 않아야 한다.
- semantic analyzer는 콘텐츠 ID별 신규 분기, unregistered capability, manual allowlist, orphan producer/consumer와 codec 누락을 fail-loud한다.
- 신규 콘텐츠가 기존 분모에 조용히 제외되면 현재 콘텐츠의 테스트가 통과해도 해당 Batch exit gate는 실패다.

V27 물리 경제 capability 매트릭스:

| 변화 축 | 현재 공용 경계 | Revision v6 폐쇄 조건 |
|---|---|---|
| item/instance 질량 | `IPhysicalItemMassProjector`, `IPhysicalItemDefinitionMassProjector`, `IPhysicalItemMassQuery` | 새 definition/state component가 projector registry에서 자동 발견되고 warehouse/carry/equipped 질량이 같은 subject를 사용 |
| 물리 입력·제거 | `IPhysicalItemBatchDispositionService`, `IReservedPhysicalItemBatchDispositionService` | Source/Transfer/Transform/Sink descriptor만으로 exact lot·gram·receipt 경로 선택, 도메인별 raw consume 분기 0 |
| 포장 tare | `IPackagedLotTareDispositionService` | reusable return/waste/declared destruction/transfer policy 구현을 descriptor로 선택, packaged item ID 분기 0 |
| 생산 output | `IProductionOutputHandler`, `IProductionOutputPlanningService`, `IProductionPreparedOutputComponentCodec` | 표준·stateful·packaged·domain output이 capability 조합으로 준비되고 custom handler ID allowlist 0 |
| output publication·routing | `IProductionPreparedOutputExecutionPort`, `IFacilityBufferPlannedOutputPublicationService`, `IFacilityOutputExactRoutePort` | 새 output line이 operation/commit/custody를 자동 획득하고 direct spawn 0 |
| buffer·warehouse capacity | `IFacilityBufferMassCapacityAuthorityQuery`, `IFacilityBufferMassAdmissionService`, `IWarehouseMassAdmissionService` | 새 recipe/facility가 maximum reachable branch와 2~4 cycle profile에 자동 포함되고 count admission 0 |
| 운반 | 기존 exact-lot haul lease/intent/carry inventory 경계 | 새 item은 mass subject만으로 mixed tour·partial pickup·Downed recovery에 참여하고 item별 planner 분기 0 |
| living entity transport | `ISurgicalPatientTransportRuntime`, `IWildlifeCaptureTransportRuntime` 계열 | item kg와 분리된 공용 entity custody capability로 통합하고 species/medical-case ID 분기 0 |
| EWU·가격 | `IRecipeBalanceWorkCalculator`와 V27 dependency ledger | 새 item/recipe/source/sink가 graph contributor로 자동 수집되고 수동 원장 행·가격 allowlist 0 |
| 저장 상태 | current-format section/receipt/outbox 계약 | capability state codec registry와 canonical envelope를 사용하고 상위 DTO의 콘텐츠별 필드 증식 0 |
| UI·AI | 공용 catalog/query/read model | 새 정의가 목록·capacity·운반 후보·작업 후보에 자동 등장하며 개별 ID presenter/action 분기 0 |

이 표의 “현재 공용 경계”가 존재한다는 사실만으로 폐쇄된 것은 아니다. Batch A~F는 각 경계의 production callsite를 registry 기반으로 이관하고, Batch G는 canary의 실제 AI/fault 경로를, Batch H는 canary가 EWU·가격·6인망 artifact 분모에 자동 포함되는 것을 증명해야 한다.

이 정의는 미래의 모든 새로운 게임 규칙이 코드 없이 구현된다는 뜻이 아니다. **기존 capability를 사용하는 콘텐츠에는 새 코드 설계가 없어야 하고, 새로운 capability에는 격리된 구현만 추가하며 코어 구조를 다시 설계하지 않아야 한다**는 계약이다. 물리·소유권 불변식 자체가 달라지는 기능만 명시적 Revision 개정을 요구한다.

### 0.2 물리 질량의 최상위 판정: 불변 강제가 아니라 설명 가능한 폐쇄

V27은 모든 레시피에서 `입력 gram == 출력 gram`을 현실 물리 법칙처럼 강제하지 않는다. 게임의 단위는 현실 물체를 축약하며, 수분 증발·절삭 분진·연소 배기·비가시 폐기·일회용 포장·환경에서의 유입을 물리 아이템으로 모두 생성하면 물류와 저장만 불필요하게 복잡해진다. 질량 감사의 목적은 현실 시뮬레이션이 아니라 **삭제·복제·숨은 무료 유입·취소 재시도 악용을 구분할 수 있게 만드는 것**이다.

여기서 강제하는 것은 열역학적 `엔트로피 불변`도, 모든 화면상 단위의 현실 질량 보존도 아니다. 엔트로피는 애초에 불변량이 아니며 V27의 게임 규칙으로 모델링하지 않는다. 게임이 반드시 지켜야 하는 것은 이미 존재하는 물리 lot의 **소유권·수량·재시도 원자성**, 반복 가능한 경제 순환의 **비차익성**, 그리고 공정에서 생기거나 사라진 값의 **설명 가능한 귀속**이다. 따라서 재미를 위해 축약된 수율은 허용하지만, 그 축약을 취소 복제나 무료 판매 가치 생성의 핑계로 사용할 수 없다.

```text
physicalInputMass
+ declaredExternalInputMass
= physicalOutputMass
+ physicalByproductMass
+ terminalSinkMass
+ declaredAbstractLossMass
```

판정 계층을 다음처럼 분리한다.

| 계층 | 엄격도 | 허용 범위 |
|---|---|---|
| 수량·소유권·WIP | exact, tolerance 0 | 취소·Downed·저장·복원에서 삭제/복제/순간이동 금지 |
| EWU·가격 순환 | SCC tolerance 0 | 반복 가능한 순환의 양의 차익 금지 |
| 레시피 질량 | 설명 가능성 필수 | 명시적 외부 유입, terminal Sink, 물리 부산물, 추상 손실 허용 |
| 현실 표현 | 디자인 선택 | 플레이에 의미 없는 증기·분진·배기·일회용 포장을 별도 item으로 만들 필요 없음 |

규칙:

1. 질량 차이 자체는 Critical이 아니다. 차이를 소유하는 typed source/sink/loss disposition이 없을 때만 `MASS_BALANCE_EXPLANATION_MISSING`이다.
2. 출력이 입력보다 커도 clean/process water, world source, air, biological growth, magic 등 선언된 외부 입력이 있으면 합법이다. 외부 입력 ID·gram·operation은 재시도 가능한 receipt에 결속한다.
3. 출력이 입력보다 작아도 증발·절삭·발효·연소·오염 폐기·추상 일회용 포장 등 선언된 손실이면 합법이다. 범용 퍼센트 tolerance로 덮지 않고 recipe/family policy가 reason과 계산식을 제공한다.
4. 부산물은 경제·운반·저장·재활용에 의미가 있을 때만 물리 item으로 만든다. 의미 없는 미세 부산물을 강제로 생성하지 않는다.
5. `packageTareGrams`는 항상 반환되는 자본이라는 뜻이 아니다. `ReusableContainerReturn`, `DisposableWasteByproduct`, `DestroyedDuringUse`, `TransferredWithOutput`, `BulkInfrastructureNotInUnit` 중 하나로 닫는다.
6. 동일 입력·동일 operation의 결과와 선언 손실은 저장·복원 후 재결정하지 않는다. 질량 추상화를 허용해도 exact-once와 idempotency는 완화하지 않는다.
7. 이 표시/설명 계약은 EWU SCC의 비대칭 Ceil/Floor와 별개다. 질량 손실을 이유로 EWU 양의 순환을 승인하지 않는다.

#### 게임적 비보존 허용 계약

게임플레이를 위해 질량을 단순화하는 것은 정상적인 authoring 선택이다. 다만 구현자가 현실 물리와 경제 무결성을 혼동하지 않도록 다음 세 등급으로 판정한다.

| 판정 | 예 | 요구 증거 |
|---|---|---|
| `GameplayAbstractionAllowed` | 증기·분진·연소 배기·발효 손실·소모되는 일회용 포장 | typed reason, deterministic gram 계산식, 같은 operation 재시도 시 동일 결과 |
| `ExternalSourceAllowed` | 작물 생장, 채굴 노드, 공기·물·마력 유입 | source owner, 생산 속도·WU·토지/시설/자원 비용, 재생·고갈 규칙 |
| `PhysicalByproductRequired` | 회수 가능한 빈 병, 재활용 금속 스크랩, 실제 운반·저장 가치가 있는 폐기물 | exact physical output lot, destination capacity, 가격·재투입 순환 감사 |

- 손실률 자체에 전역 상한을 두지 않는다. 5%든 80%든 해당 item의 `1개` 의미, BOM과 공정 설명이 맞고 경제·처리량 검증을 통과하면 허용한다.
- 반대로 1g 차이라도 typed 귀속이 없거나 취소·복원·재시도로 달라지면 버그다.
- 재미를 위한 간소화 때문에 보이지 않는 부산물을 전부 item으로 만들지 않는다. 물리화 여부는 회수·운반·저장·오염·재사용 중 실제 플레이 선택을 만드는지로 결정한다.
- “불변”이라는 용어는 이후 체크리스트에서 `lot 수량·소유권·commit exactness` 또는 `EWU SCC 무차익`에만 사용한다. 레시피 입력 gram과 출력 gram의 동일성에는 사용하지 않는다.
- 따라서 Batch C의 목표는 모든 공정을 닫힌계로 개조하는 것이 아니라, 각 공정을 `external source / physical byproduct / terminal sink / abstract loss` 중 하나로 완전 분류하고 미분류 경로를 0으로 만드는 것이다.

#### 외부 유입의 경제 폐쇄

물리 `Source`가 합법이라는 사실은 무료 경제 가치도 합법이라는 뜻이 아니다. 채집·농업·세력 배송·계약 보상·원정 보상처럼 외부에서 들어오는 물자는 질량 회계와 별도로 노동·시간·토지·관계·통화·위험·쿨다운 가운데 하나 이상의 실제 비용을 가져야 한다.

- `Trade`는 route 생성 전에 canonical quote와 동일한 금액을 한 번만 선결제한다. 출발 뒤 매복으로 화물이 줄어도 기본 정책은 환불 없음이며, 이 위험을 견적 UI에 표시한다.
- `Supply`는 무료 판매 금지 provenance로 아이템을 오염시키지 않는다. 동일 아이템은 동일한 시장 규칙을 유지하고, 대신 지급 EWU를 세력별 쿨다운과 모든 세력 합산 `alliance-benefit` 예산에서 exact debit한다.
- Trade 구매액은 동일 화물의 즉시 판매 회수액보다 커야 한다. Supply의 일일 기대 가치는 authored 관계 보상 예산 이하이어야 한다.
- 화물은 `Ready → Publishing → Delivered` exact-source publication으로 처리하고, 다중 line 일부 spawn·드롭존 부재·재시도에서 부분 완료나 복제를 허용하지 않는다.
- 가격·지급 정책은 item/faction ID 분기가 아니라 versioned economic-policy capability registry로 선택한다. 신규 팩션과 일반 아이템은 authoring만으로 quote·debit·delivery·save·audit에 들어와야 한다.
- route policy가 없는 외부 유입과 연결된 가격 후보는 수학적으로 SCC가 안전해도 `rework`로 유지한다. 낮은 가격으로 무료 유입 결함을 숨기지 않는다.

권장 초기 정책은 `Trade = V27 authored unit price × 수량의 1.00배 선결제`, `출발 후 손실 환불 없음`, `Supply = 통화 debit 0 + 전역 alliance-benefit EWU 예산 debit`이다. 구체적인 전역 예산 수치는 현재 faction 수·cargo·cooldown의 일일 기대값을 전수 산출한 뒤 승인하며, 계산 전 임의 상수를 넣지 않는다.

구현 순서는 결제·경로·배송을 한 번에 섞지 않고 다음 수직 슬라이스로 고정한다.

1. `IIdempotentGameMoneyAccount.TrySpendOnce`가 positive cost, stable source, target과 exact receipt를 소유한다. 동일 source·동일 값 재시도는 같은 receipt를 반환하고 추가 차감하지 않으며, 다른 값·target 재사용은 실패한다.
2. Faction economic-policy registry가 authored cargo의 canonical quote와 digest를 만든다. item/faction ID switch, UI 독자 계산, float 합산은 금지한다.
3. Trade 요청은 settlement operation sequence를 할당하고 exact debit receipt를 route V4 DTO에 동결한 뒤에만 route를 게시한다. debit과 route 게시 사이에는 save capture를 차단한다.
4. Supply 요청은 동일 quote 구조를 사용하되 currency receipt 대신 global alliance-benefit budget reservation을 동결한다.
5. 도착 화물은 frozen output vector를 exact-source publication으로 원자 게시하고, 모든 line receipt가 확인된 뒤에만 Delivered가 된다.

2026-08-31 첫 두 슬라이스 증거: 공용 exact-once debit과 `FactionTradePurchase/FactionTradePurchaseRefund` transaction kind를 추가했다. focused fixture는 최초 `9 gold` 차감 `100→91`, 동일 source replay 추가 차감 `0`, 다른 금액 충돌 거부, 잔액 `8<9` 실패 시 balance 불변과 failure receipt, ledger 예외 시 `100` 복원을 통과했다. 이어 6개 실제 세력의 Trade/Supply policy descriptor를 authored asset에 기록하고, capability registry가 cargo를 item ID ordinal로 정렬해 deterministic quote/source digest를 만들도록 연결했다. 실제 quote는 Trade `62~228 gold`, Supply authored cargo value `304~2,087 gold` 범위이며, 등록 순서 역전 동일성·중복 policy·누락 policy fail-loud를 통과했다. `QUALITY_REJECTED_SALE_OUTBOX_PASS`, `FACTION_ROUTE_ECONOMIC_POLICY_PASS`, Unity compile PASS, Console Warning/Error `0/0`이다. 이 증거는 경제-policy descriptor·canonical quote·공용 debit의 `3/7`만 닫으며 route request 결제 transaction, V4 settlement receipt, Supply budget, physical cargo publication과 남은 99 Critical은 OPEN이다.

이 절은 뒤의 과거 체크포인트에서 사용한 “질량 보존” 표현보다 우선한다. 과거 `mass-creation-critical`은 자동 실패가 아니라 `외부 유입 권위 누락 여부`를 재판정하고, `disposition-contract-missing`은 입력·출력의 동일화를 요구하는 대신 위 폐쇄식의 누락된 항을 작성하게 한다.

#### 현재 작업 분모

아래 수는 영구 상수가 아니라 2026-08-26 current-source snapshot이다. 모든 배치 시작 시 재캡처하고 stable ID 집합 또는 source digest가 달라지면 분모와 영향 그래프를 먼저 갱신한다.

| 대상 | 현재 분모 | 현재 증거 | 남은 핵심 |
|---|---:|---|---|
| V27 ledger item | 414 | current-source semantic projection 363 | unresolved semantic 51, 전수 coupling 검토 |
| serialized physical weight site | 1,074 | 전수 위치·writer/source digest 캡처 | builder 재생성 후 byte identity |
| ledger 밖 physical definition | 660 | 위치 식별 | authority 분류·중복/compatibility 제거 |
| production recipe | 355 | inventory 생성, exact contract 42 | 313개 contract 귀속과 mass anomaly 검토 |
| active facility | 356 | active/deprecated 분리 | construction·buffer·capacity 전수 연결 |
| deprecated compatibility facility | 21 | 별도 집합 | production 소비 0 또는 명시적 compatibility disposition |
| generated facility kit | 377 | 동일 8,000g 계열 식별 | packed-unit family policy·BOM coupling 검토 |
| FacilityBuffer input owner | 39 | exact migrated 3 | remaining 36 및 bypass 5·orphan 1 폐쇄 |
| FacilityOutputBuffer/direct output owner | 6 | exact migrated 6 | synthetic full-path canary·Batch A normal/fault PlayMode 검증 |

단일 `% 완료`는 사용하지 않는다. raw Markdown checkbox는 작업 색인일 뿐이며 작은 정적 항목과 전수 PlayMode gate의 무게가 같지 않다. 2026-08-27 fresh owner manifest와 current-source compile 기준 정직한 가중 잔여량은 약 `32~42%`이고, 다음 다섯 축을 별도로 보고한다.

| 축 | 현재 판정 | 완료 판정 |
|---|---|---|
| 구조 권위 | advanced structural-partial | legacy/bypass/orphan 0, 모든 mutation family가 typed authority 사용 |
| 콘텐츠 이관 | partial | current 분모 전체 semantic·mass·capacity·disposition closure |
| live 실행 증거 | early partial | 대표 정상·중단·복원·용량 실패 경로의 actual AI/PlayMode 증거 |
| 최종 밸런스 | open | EWU·가격·6인 생존망·공간·다중 seed·no-op artifact 모두 green |
| 미래 콘텐츠 확장 폐쇄 | open | 기존 capability canary의 core diff 0, 신규 Strategy 공용 contract 자동 통과, content-ID branch·manual allowlist·unregistered capability 0 |

#### 완료 가능한 작업 단위

앞으로 “메서드 하나”, “체크박스 하나”, “아이템 하나”를 완료 단위로 잡지 않는다. 하나의 배치는 아래 경로를 끝까지 닫아야 한다.

```text
definition/authoring
→ capability descriptor와 다형적 registry
→ runtime authority
→ 실제 producer/consumer
→ destination capacity와 custody
→ current-format save/restore
→ cancel/Downed/destroyed/output-space fault
→ synthetic future-content canary의 core diff 0
→ deterministic artifact와 baseline evidence
```

정적 감사만 끝난 행은 `inventory-complete`, 코드만 연결된 행은 `structural-partial`, 실제 생산 경로만 정상 통과한 행은 `live-normal-partial`이다. 같은 계열의 fault matrix와 전수 artifact까지 통과해야 해당 배치를 `complete`로 닫는다.

#### 병렬 안전 레인

| 레인 | 병렬 가능 작업 | 직렬화 조건 |
|---|---|---|
| `U: Unity integration` | 없음. compile, domain reload, PlayMode, SO apply | Unity Editor/MCP writer는 항상 한 작업만 소유 |
| `S: source audit` | semantic symbol manifest, callsite·save join·capacity reader 조사 | read-only 또는 서로 다른 산출물 파일만 허용 |
| `A: authoring proposal` | item family projection, recipe mass 계산, anomaly 후보 생성 | SO 직접 수정 금지; immutable proposal artifact만 출력 |
| `T: tests` | 서로 다른 assembly의 순수 단위 fixture 작성·검토 | 같은 source 파일·asmdef·generated file 동시 편집 금지 |
| `D: documentation` | evidence 수집·hash 계산 | 계획서·baseline·progress의 최종 writer는 root 하나만 소유 |

병렬 작업은 속도를 위해 검증을 생략하는 수단이 아니다. 각 작업은 입력 digest, 수정 가능 파일 집합, 산출물, exit gate를 먼저 선언한다. root integrator가 겹치는 diff를 합친 뒤 Unity 레인을 한 번만 실행한다. 동일 generated asset, shared contract, save schema, composition root를 둘 이상의 작업자가 동시에 수정하지 않는다.

#### 자동 authoring 원칙

414개 item과 355개 recipe를 사람이 한 행씩 수정하지 않는다. 같은 파이프라인은 미래에 추가되는 item/recipe/facility를 코드 수정 없이 자동 수집해야 한다.

```text
current authority capture
→ deterministic proposal generator
→ family policy projection
→ producer/consumer/capacity coupling 계산
→ anomaly-only review
→ approved projection을 builder/SO에 적용
→ second-run byte diff 0
```

기본 family는 `원초 자원·기초 식품`, `ResearchOverhaul 생성 중간재`, `V22 의복·장비`, `포장·용기·유체`, `facility-kit`, `사체·폐기물·특수 물품`으로 나눈다. 동일 정책으로 설명되는 정상 행은 묶어서 승인하고, 다음 항목만 사람에게 올린다.

- unit semantic을 current source에서 확정하지 못한 51개
- 가족 중앙값 또는 물리 BOM 범위를 벗어난 질량
- producer와 consumer가 서로 다른 unit 의미를 전제한 항목
- recipe mass creation, undeclared loss, tare 증발, WIP owner 누락
- ordinary batch `6~11kg`, mixed tour `8~14kg`, heavy `15~20kg` 범위를 의도 없이 벗어난 항목
- storage·FacilityBuffer·construction BOM·equipment duplicate writer에 연쇄 Critical을 만드는 root

사용자 질문은 게임 디자인 선택이 실제로 필요한 semantic anomaly만 묶어서 한다. 코드에서 확인 가능한 권위, 수량, producer/consumer, save owner를 사용자에게 되묻지 않는다.

#### 변경 예산과 중단 조건

- 한 integration batch는 공통 권위 한 계열과 그 수직 소비자만 바꾼다.
- 새 kg를 적용하는 batch는 대응 효능·stack·storage·recipe batch·haul trip·EWU/price 영향표를 같은 proposal에 포함한다.
- fresh compile, focused fixture, relevant full suite, Console `0/0`, scoped diff, rollback 경로가 없으면 다음 batch로 진행하지 않는다.
- source digest drift, unresolved P0/P1, partial restore publication, mass deletion/duplication, Unity YAML unexpected churn이 나오면 적용을 중단하고 proposal artifact는 보존한다.
- Unity를 장시간 독점하는 전수 seed는 구조·authoring·anomaly gate가 끝난 뒤 한 번에 실행한다. 작은 수정마다 256-seed를 반복하지 않는다.

이 문서는 다음 작업을 하나의 수직 슬라이스로 묶어 구현하기 위한 권위 계획서다.

- 캐릭터 기본 운반 목표를 건강한 평범한 성인 기준 약 `19kg 무감속 / 29kg 최대`로 조정
- 운반 멜빵 사용 시 약 `24kg 무감속 / 36kg 최대` 유지
- 모든 canonical 물리 아이템의 `1개` 의미와 kg 전수 확정
- 원료·식품·중간재·장비별 기준 중량표 작성
- BOM·재료 밀도·포장 질량을 이용한 파생 중량 계산
- 명시적 외부 유입·물리 부산물·terminal Sink·추상 공정 손실로 질량 차이를 설명하고, 입력=출력 자체는 강제하지 않음
- 전투 장비의 전투 중량과 물리 아이템 운반 중량을 단일 규칙으로 연결
- 일반 레시피 1회 투입 묶음 `6~11kg`, 실제 혼합 운반 계획 `8~14kg` 검증
- 물류 EWU·가격 재생성
- 6인 음식·물·N+1 생존망 회귀 검증

이 문서만 작성된 상태는 중량 적용이나 밸런스 완료가 아니다. 실제 에셋 적용, 전수 감사, PlayMode, 다중 seed와 실전 보정 전에는 `밸런스 기준 배정`보다 높은 상태로 보고하지 않는다.

이 계획의 “완전” 판정은 미래 결함이 절대 없다는 뜻이 아니다. 현재 compilation에서 발견 가능한 production symbol·save edge·reader·writer가 semantic manifest에 100% 포함되고, 기존 capability 범위의 신규 콘텐츠가 코어 재설계 없이 자동 연결되며, 계약 밖 콘텐츠와 신규 호출부 drift가 CI에서 fail-loud하고, 각 fault matrix 행이 실행 증거를 가진다는 뜻으로만 사용한다. manifest와 synthetic canary 증거가 없으면 계획이 decision-complete여도 시스템 연계 또는 확장 폐쇄 완료라고 부르지 않는다.

현재 focused 질량 회계 과정에서 실제 `unitWeight`가 변경된 12개 항목은 `물리 질량 잠정 적용값`이다. 이 명칭은 입력과 출력의 gram 동일성을 뜻하지 않는다. 단위 효능, 반복 WU, EWU, maxStack, 저장 밀도, live 운반과 typed source/sink/loss 귀속을 함께 검증하기 전에는 최종 밸런스 값으로 승인하지 않는다. 이 결합 감사가 끝날 때까지 신규 kg의 연속 적용을 중단한다.

---

## 1. 범위와 비범위

### 1.1 포함 범위

- canonical 물리 아이템 카탈로그 전체
- current-source V27 원장에 해석되는 414개 canonical item identity
- 현재 물리 에셋에서 발견되는 모든 `unitWeight` 작성 위치
- 전체 생산 레시피 355개
- active facility 356개, deprecated compatibility facility 21개, generated facility kit 377개
- 원료, 광석, 목재, 석재, 액체, 식품, 종자, 사료
- 중간재, 부품, 약품, 탄약, 폐기물
- 의복, 공구, 무기, 방어구, 방패, 장비 모듈
- 시설 설치 키트, 설계도, 촉매, 유물, 장기와 사체 등 특수 물품
- 캐릭터 운반 한도, 최대 과적, 이동 감속, 멜빵, 운반 특성
- 단일 스택 운반과 여러 스택 혼합 운반 계획
- 레시피 입력 버퍼, 시설 출력 버퍼, 창고, in-transit commitment
- 중량 변화에 영향을 받는 물류 EWU, AcquisitionCost, RecoverableValue, 구매·판매 가격
- 6인 생존 생산·비축·운반·공간 회귀

### 1.2 제외 범위

- 과거 세이브의 kg 마이그레이션
- 구버전 저장 데이터의 자동 변환
- 차량, 수레, 동물 운반대 같은 신규 운송 콘텐츠 추가
- 팀 리프트나 두 명 동시 운반 같은 신규 행동 추가
- 운반 한도를 설정 UI에서 없애는 작업
- 중량을 맞추기 위한 무료 생산, 순간이동, 추상 재고, 암묵적 fallback
- 실제 콘텐츠가 없는 모닥불·카트·짐꾼 장비를 존재한다고 가정하는 것

현재 저장은 물리 아이템 정의의 `unitWeight` 자체를 저장 권위로 소유하지 않는다. 신규 빌드에서는 정의와 인스턴스 구성요소에서 다시 계산한다. 과거 세이브 호환을 위해 이중 중량 권위를 만들지 않는다.

---

## 2. 현재 코드 권위와 문제 정의

### 2.1 현재 물리 중량 권위

현재 일반 물리 스택의 kg 권위는 다음 필드다.

```text
ItemDefinitionSO.unitWeight
```

이 값은 실제로 다음 경로에서 소비된다.

```text
ItemDefinitionSO.unitWeight
-> DungeonItemDefinition.UnitWeight
-> WorldItemStackSnapshot.UnitWeight
-> CharacterCarryInventory.GetCurrentWeight
-> WorldItemHaulPlanningService 후보 수량
-> ItemPileInfoPanel kg 표시
-> V23/V27 물류·취급 EWU
```

따라서 kg 변경은 장식용 수치가 아니라 다음을 동시에 바꾼다.

- 한 번에 픽업하는 수량
- 여러 스택을 한 운반 계획으로 묶을 수 있는 수량
- 과적 여부와 이동 속도
- 시설 투입 완료 시간
- 창고 정리량과 floor clutter 복구 시간
- 생산·건설·수술·수리의 물류 지연
- V27 handling/logistics EWU
- 내부 단가, 구매가, 판매가와 계약 가치

### 2.2 현재 운반 공식

현재 공식은 다음과 같다.

```text
baseCarryKg
= 25kg nominal authority
× performance:survival:haul-capacity
× haulingHarness(착용 시 1.25)

maxCarryKg
= baseCarryKg
× ItemHaulingSettings.maxCarryMultiplier
```

기본 `maxCarryMultiplier`는 `1.5`다. 무감속 한도를 넘으면 최대 한도에서 이동 속도 `0.45`가 되도록 선형 감속한다.

`25kg` nominal 권위는 `CharacterCarryTuning.NominalBaseCapacityKilograms`에 이미 적용되어 있다. `19/29kg`는 기준 상수가 아니라 평범한 actor의 실제 performance 투영 결과다. 둘을 같은 값으로 기록하지 않는다.

| 상태 | 계산 | 무감속 한도 | 최대 한도 |
|---|---|---:|---:|
| 일반 actor | `25kg × 약 0.764` | 약 19.10kg | 약 28.65kg |
| 일반 actor + 멜빵 | `일반 × 1.25` | 약 23.88kg | 약 35.81kg |
| 육중한 체격 | `일반 × 1.12` | 약 21.39kg | 약 32.09kg |
| 육중한 체격 + 멜빵 | `일반 × 1.12 × 1.25` | 약 26.74kg | 약 40.11kg |

`25kg` nominal, 멜빵 `1.25`, 기본 최대 배율 `1.5`, 육중한 체격 `1.12`, 최대 과적 속도 `0.45`를 현재 단일 권위로 유지한다. 이후 전수 item kg와 실전 물류 검증은 이 값을 후보로 다시 바꾸는 절차가 아니라 실제 `19/29kg`, `24/36kg` 경험 밴드가 유지되는지 검증하는 절차다.

### 2.3 현재 item kg의 구조적 문제

2026-08-26 current-source V27 원장에는 1,074개의 serialized `unitWeight` 위치가 있으며 값 범위는 약 `0.02~28kg`다. 이 수는 중복 에셋·생성 산출물까지 포함한 작성 위치 수이고 canonical item identity 수는 414개다. 구현 시 에셋 행 수를 아이템 수로 잘못 세지 않는다.

현재 확인된 문제는 다음과 같다.

- ResearchOverhaul 생성기는 실제 BOM이 아니라 item kind와 tag 휴리스틱으로 `0.05~8kg`를 생성한다.
- 377개 정도의 작성 위치가 정확히 `8kg`이며 대부분 `facility-kit:*` 설치 키트다.
- 시설 키트 8kg는 전체 건설 BOM 질량이 아니라 “포장된 부품 묶음”이라는 추상이다.
- 전투 장비는 physical item `unitWeight`, combat definition `weight`, 재료 `WeightMultiplier`가 함께 존재한다.
- 의복은 physical item `unitWeight`, apparel `baseWeight`, textile material `WeightMultiplier`가 함께 존재한다.
- 물리 스택과 운반 계획은 unique 장비 인스턴스의 재료·진화 중량을 읽지 않고 generic item `unitWeight`만 읽는다.
- 운반 멜빵은 현재 physical `1.15kg`, apparel base `0.9kg`로 서로 다른 의미가 명문화되어 있지 않다.

따라서 단순히 414개 ledger item 숫자만 바꾸면 660개 비원장 physical definition, builder 재생성, 장비 재료 변형, 운반과 전투 표시 사이에 다시 드리프트가 생긴다.

### 2.4 kg 단독 교정의 구조적 위험

설명 가능한 질량 회계도 필요조건일 뿐 밸런스 완료조건은 아니다. `unitWeight`를 바꾸고 효능·출력·WU·stack·storage를 그대로 두면 다음 비율이 동시에 변한다.

```text
nutritionPerKg
feedValuePerKg
medicalPotencyPerKg
damageOrProtectionPerKg
directWuPerOutputKg
ewuPerKg
pricePerKg
maxStackMassKg
warehouseReserveDaysAndUtilization
physicalPileCellOccupancy
recipeBatchMassKg
haulTripsPerBatch
```

예를 들어 건초를 `450g -> 196g`으로 바꾸면서 단위당 사료가치 8을 유지하면 사료가치/kg가 약 2.30배가 된다. 사일리지 `700g -> 230g`은 단위당 공급가치를 유지할 경우 kg당 효율이 약 3.04배가 된다. 두 값은 질량식만 보면 정확하지만 축산·저장·물류 밸런스는 틀어질 수 있다.

따라서 다음 원칙을 강제한다.

1. 물리적으로 자연스러운 kg를 경제 목표에 맞춰 다시 왜곡하지 않는다.
2. kg를 확정한 뒤 효능, 출력 수량, 소비 수량, WU, maxStack, 저장 용량, 가격을 순서대로 결합 조정한다.
3. 특정 레시피 하나가 아니라 동일 item의 모든 producer, consumer와 비레시피 sink를 감사한다.
4. 중간재는 전역 표준 단위질량 하나만 가지며 소비처별 다른 kg를 금지한다.
5. 공정 중 일시적인 수분·가열·숙성 상태는 recipe 내부 fluid/loss/byproduct로 처리하며 아이템을 과도하게 분할하지 않는다.
6. 별도 아이템 분리는 서로 독립적으로 저장·운반·판매되거나 부패·호환·게임플레이 결정이 실제로 다를 때만 허용한다.

현재 잠정 적용된 12개 kg 항목:

| item | Before | 잠정 After | 결합 감사 상태 |
|---|---:|---:|---|
| `food:fresh-curd` | 450g | 225g | 영양/kg·하수비용·저장 미검증 |
| `food:cheese-mushroom` | 700g | 450g | 영양/kg·숙성 downstream 미검증 |
| `material:grape-juice` | 450g | 375g | 음료 downstream·가격 미검증 |
| `food:grape-syrup` | 350g | 175g | 영양·기분/kg 미검증 |
| `material:young-wine` | 500g | 350g | 주류 downstream·가격 미검증 |
| `food:twilight-beer` | 500g | 475g | 효과/kg·저장 미검증 |
| `drug:night-wine` | 500g | 325g | substance 효과/kg·가격 미검증 |
| `food:vegetable-pie` | 700g | 475g | 영양/kg·식량망 미검증 |
| `food:stuffed-mushroom` | 650g | 575g | 영양/kg·식량망 미검증 |
| `feed:hay` | 450g | 196g | 사료가치/kg·축산 처리량 Critical 재검토 |
| `feed:silage` | 700g | 230g | 사료가치/kg·축산 처리량 Critical 재검토 |
| `food:meat-pie` | 800g | 575g | 영양/kg·식량망 미검증 |

이 표의 상태가 모두 `coupled-pass` 또는 명시적 rollback/revision으로 닫히기 전에는 위 값을 최종 기준으로 인용하지 않는다.

### 2.5 추가 교차 도메인 누락 감사

kg·효능 결합만으로도 충분하지 않다. 현재 런타임 권위와 이 계획을 다시 대조한 결과, 다음 경계는 별도 폐쇄 계약 없이는 질량 수치가 맞아도 게임플레이가 틀어질 수 있다.

| 우선순위 | 경계 | 실제 위험 | 필수 폐쇄 계약 |
|---|---|---|---|
| P0 | 저장 `개수`와 질량 capacity | 현재 `WarehouseInventory.MaxCapacity/RemainingCapacity`는 kg가 아니라 물리 item 수량 합을 센다. 작은 단위 item을 부당하게 많이 차감하고 무거운 item을 지나치게 싸게 저장한다. | 별도 L/부피 stat을 만들지 않고 창고 capacity를 canonical `unitMassGrams` 합계로 전환한다. admission·부분 입고·UI·저장·복원·시설 수치·Floor Clutter를 모두 `capacityGrams` 권위에 연결한다. |
| P0 | 공정 중 재공품(WIP) | 생산은 입력을 작업 시작에 물리 소비하고 출력을 완료 때 생성한다. 그 사이 주문 취소·시설 파괴·저장·복원이 일어나면 입력 질량이 단순 삭제되거나 재생성될 수 있다. | 입력 commitment, consumed mass, incorporated fluid, realized loss, pending output mass를 `InProcessMassLedger`로 감사한다. 취소 시점별 반환·폐기·회수 정책과 저장 왕복을 명시한다. |
| P0 | 입력 역할 | BOM의 모든 항목이 소모품인 것은 아니다. 촉매·도구·용기·치구·시설 요구와 공정 연료를 모두 제품 input으로 합산하면 질량과 EWU가 이중계상된다. | 각 입력을 `ConsumedIntoProduct`, `ConsumedProcessFuel`, `ReturnedCatalyst`, `ToolWearOnly`, `ReusableContainer`, `Infrastructure`, `Packaging`으로 분류한다. 반환 항목은 수량·상태·내구가 exact해야 한다. |
| P0 | 수량 기반 주문·계약 | output count나 소비량을 조정하면 생산 주문, 건설, 의료, 수술, 사료, 계약, 원정 보급처럼 `개수`를 권위로 쓰는 시스템이 동시에 변한다. | 모든 producer/consumer뿐 아니라 unit-count threshold, order, contract, expedition loadout, facility supply를 `quantitySemanticConsumers`로 전수 수집한다. output count 변경은 이 목록이 닫히기 전 금지한다. |
| P0 | 출력 공간 원자성 | 입력을 먼저 소비한 뒤 output spawn/capacity가 실패하면 WIP가 소실되거나 반복 재시도로 복제될 수 있다. | cycle 시작 전 또는 commit 직전 exact output capacity를 예약한다. output 실패 시 WIP를 유지하고 재시도하며 입력을 다시 소비하지 않는다. 시설 파괴 시 typed salvage/loss로만 종료한다. |
| P0 | 유체의 질량과 처리량 | clean water·wastewater unit이 현재 경제상 추상 수량이면 `1 unit = 500g` 같은 질량 가정이 배관 L/day·하수 처리량과 일치하지 않을 수 있다. 유청·염수·세척수도 같은 wastewater 수량으로 합쳐질 수 있다. | fluid별 `gramsPerUnit`, `millilitersPerUnit`, 조성/disposition, clean-water 수요, wastewater·sludge·off-gas 출력을 하나의 fluid authority에서 읽는다. 질량과 hydraulic volume을 별도 검증한다. |
| P1 | freshness·quality stack 병합 | maxStack 변경이나 생산량 변경은 서로 다른 신선도·품질 item의 병합 빈도를 바꾼다. 잘못 병합하면 식품 수명이 갱신되거나 품질이 사라진다. | 병합 signature에 모든 비신선도 상태를 보존하고 freshness는 보수적 최소값을 사용한다. incompatible state는 병합하지 않는다. split/merge 전후 mass·quality·freshness를 exact 검증한다. |
| P1 | Source 경제 폐쇄 | 농업·채집·축산은 합법적인 Source라서 질량 귀속만 보면 통과하지만, 토지·시간·물·사료 없이 고효율 kg가 생길 수 있다. | source마다 node/plot/animal/day yield, land, water, feed, labor, season/failure를 기록한다. Transform 질량식과 별도로 경제·처리량 폐쇄를 요구한다. |
| P1 | AI 운반 공정성 | 가벼운 item은 같은 kg 안에 더 많은 stack/leg를 넣을 수 있어 planner가 작은 일반 주문으로 tour를 채우고 무거운 긴급 주문을 굶길 수 있다. | kg·거리 효율 외에 urgency, order age, destination priority, last-deficit를 기록한다. 긴급 소량의 preemption, heavy-order bounded wait, item-count-per-tour 상한을 live 검증한다. |
| P1 | 시장·계약·원정의 kg당 가치 | unit price와 unit reward가 고정된 상태에서 kg가 줄면 같은 계약·원정 보상을 훨씬 적은 물류비로 운반한다. | buy/sell/reward per kg, contract WU/kg, expedition supply-days/kg를 ledger에 포함하고 unit-count 수요와 함께 재산정한다. |
| P1 | 동적 상태 질량 | 젖음·오염·충전·탄약·모듈·내구·품질이 실제 질량을 바꾸는지 정의하지 않으면 world/carry/equipped 표시가 갈라진다. | 질량을 바꾸는 component만 공용 instance mass projector에 포함한다. freshness·quality·내구는 기본적으로 질량 불변이며 예외는 exact component grams를 가져야 한다. |
| P1 | 현재 형식 저장의 과중 상태 | 과거 세이브 마이그레이션은 범위 밖이지만, 같은 V27 빌드에서 저장한 carried/WIP가 복원될 때 정의·장비 상태와 capacity를 다시 계산해야 한다. | current-format save/restore에서 item 수량·instance component·WIP·carry가 동일하고, 복원 직후 over-capacity여도 삭제·순간이동하지 않는다. actor가 활동 가능하면 유지 후 재계획, Downed/Dead면 현 위치 드롭 계약을 따른다. |
| P0 | 정확한 item identity | 상점 보충·보충 취소·일부 시설 생산은 item ID가 아니라 `StockCategory`만 넘기고 카테고리의 첫 정의를 물리 생성한다. 같은 수량이어도 다른 item·kg·가격·효능으로 바뀔 수 있다. | 모든 live 경제/생산/반환 API는 exact item ID와 unique instance/component lot을 보존한다. category-only spawn은 Editor fixture로 제한하고 production callsite를 0으로 만든다. |
| P0 | 창고 demolition·relocation | 현재 철거는 구조 지지만 검사하고 건물을 삭제하며, 이전은 건물 state module만 새 위치에 복원한다. 물리 Stored stack과 inbound intent는 비우거나 이동하지 않는다. | 1차 구현은 Stored mass, inbound capacity lease, carried intent, conveyor transit가 모두 0일 때만 철거·이전을 허용한다. 적재 창고 이전은 별도 원자적 physical relocation 기능 전에는 금지한다. |
| P0 | 입고 원자성·동시성 | carried item을 먼저 inventory에서 제거하고 lease를 완료한 뒤 capacity를 검사하며, 초과분을 actor 위치 Loose로 버린다. 여러 hauler/conveyor가 같은 남은 capacity를 동시에 선택할 수도 있다. | 계획 시점부터 destination gram lease를 잡고 commit에서 exact revalidate한다. 거부분은 carried 상태와 owner intent를 유지해 재계획하며, 창고 포화만으로 floor drop·operation 완료를 만들지 않는다. |
| P0 | virtual stock·equipment sink | 상점은 물리 창고 item을 category stock count로 바꾸고, 일부 non-combat equipment 입고는 Stored stack 없이 event만 발행한다. kg capacity 밖의 숨은 저장/삭제가 된다. | 모든 virtual inventory를 전수 분류한다. 경제적 물체이면 exact physical lot+mass로 전환하고, 진짜 추상 서비스 credit이면 명시적 Transform/Sink와 질량 disposition을 작성한다. |
| P0 | stateful mass 이중 권위 | combat material/evolution, apparel textile, wildlife species carcass weight가 generic physical `unitWeight`와 별도다. | 공용 instance mass projector에서 exact gram을 계산하고 builder·validator로 default physical item과 동기화한다. carcass/apparel/equipment mismatch 0건을 요구한다. |
| P1 | machine·facility buffer | production output buffer와 FacilityBuffer는 count/배치 슬롯이거나 무제한에 가깝다. 창고만 kg로 바꾸면 무거운 물품을 버퍼에 장기 적치해 우회할 수 있다. | 각 buffer를 `TransientOperationalBuffer`, `PhysicalMassLimitedBuffer`, `VirtualLedger`로 분류하고 최대 체류시간·최대 batch kg·overflow 정책을 검증한다. 창고 capacity를 암묵 재사용하지 않는다. |
| P1 | 질량 조회 성능·revision | warehouse mass를 매 planner 후보마다 전체 repository LINQ 합산하면 item 수×warehouse 수×actor 수로 커진다. component/evolution 변경이 cache revision을 올리지 않으면 오래된 kg를 쓴다. | warehouse별 derived gram index와 authority revision을 둔다. stack state·destination·quantity·component·material/evolution 변경 시 정확히 invalidate하고, no-op 변경은 revision을 올리지 않는다. |
| P0 | room storage와 shop count 차원 충돌 | room profile은 `BuildingStorageAbility.capacity`를 category별 합산하고 shop은 이를 abstract item-count 최대치에 더한다. storage를 kg로 바꾸면 `item count + kg`가 된다. | room profile에 warehouse mass와 retail display slot을 분리한다. shop은 exact physical retail lot+mass 또는 별도 authored display-slot count만 사용하며 두 차원을 더하지 않는다. |
| P1 | 초경량 item record 폭증 | kg만으로는 1g item 수만 개도 합법이다. `MaxStack` 단위로 물리 record가 늘면 save·query·marker·planner 성능이 무너질 수 있다. | count gameplay capacity를 부활시키지 않고 compatible-state compaction, record-count diagnostic, p95 query/save budget과 fail-loud technical ceiling을 둔다. |
| P0 | low-level Stored 우회 | public spawn/route/transit API가 `warehouse-storage:` destination으로 직접 Stored record를 만들 수 있다. 반대로 apparel recovery locker·sample ration도 Stored지만 warehouse가 아니다. | destination kind를 typed 분류하고 warehouse transition에는 capacity commit token을 필수화한다. non-warehouse Stored destination은 별도 owner/capacity/dwell contract를 가진다. |
| P0 | untyped 물리 소비·삭제 | 범용 `TryConsumeStackQuantity`는 exact stack을 제거하지만 소비 이유, output/byproduct, 합법 loss를 요구하지 않는다. 호출자가 출력을 빼먹어도 성공해 합법 Sink와 버그성 질량 삭제를 구분할 수 없다. | 모든 production consume/remove를 `PhysicalItemDispositionCommand/Receipt` 뒤로 모은다. exact input grams, closed disposition kind, owner operation, expected output/byproduct/loss, commit ID를 기록하고 untyped production caller를 0으로 만든다. |
| P1 | 원정 abstract burden | 원정 loot는 `StockCategory → count`로 저장되고 귀환 시 합계를 `UnappraisedLoot` 물리 item으로 만든다. 원정 중에는 kg가 없어 무제한·무중량 보상이 될 수 있다. | 원정 reward Source에 exact burden/mass-per-unit와 carry cap을 두고, 귀환 physical grams로 exact 변환한다. supply packing도 input lot·tare·소비·반환 receipt를 보존한다. |
| P0 | 저장 중 spoilage·in-place transform | 음식·사체 부패는 기존 stack을 먼저 삭제하고 waste/rot를 Loose로 spawn하며 성공을 확인하지 않는 경로가 있다. Stored item도 warehouse destination을 잃어 용량이 조기 해제된다. | atomic transform receipt로 input과 output을 한 번에 전환한다. warehouse owner/destination을 유지하고 새 grams를 즉시 반영한다. category 불일치는 보존+evacuation, 질량 증가는 valid over-capacity+신규 admission 차단으로 처리한다. |
| P1 | 운반 한도 동적 감소 | 부상·욕구·성능 저하나 멜빵 해제/파손은 pickup 뒤 hard limit를 낮출 수 있다. 현재 과중 cargo를 보존하지만 45% 속도로 계속 움직일 수 있고 bounded unload 계약이 없다. | cargo 보존+신규 pickup 차단+우선 unload/replan을 적용한다. SLA 실패 시 actor 위치 typed recovery drop. Downed/Dead는 기존 recovery drop을 사용하고 teleport/delete를 금지한다. |
| P1 | cargo와 equipped mass | haul cargo만 세면 중갑 장비의 이동 부담이 사라지고, 둘을 무작정 합치면 멜빵의 payload 보너스를 자기 무게로 이중 차감할 수 있다. | `cargoMassGrams`, `equippedMassGrams`, `totalBorneMassGrams`를 분리한다. cargo는 pickup capacity, equipped/total은 승인된 locomotion·stamina·combat encumbrance에 사용한다. |
| P0 | in-transit 질량 변화 | destination gram lease 뒤 module/ammunition/package 상태가 바뀌면 실제 cargo gram과 예약 gram이 달라져 창고 overcommit 또는 용량 누수가 생긴다. | mass-affecting mutation은 lease delta를 같은 transaction에서 reserve/release한다. 양의 delta 확보 실패는 cargo 유지+replan. 1차 구현에서 mutation을 금지한다면 haul-owned 상태에서 typed fail-loud한다. |
| P1 | living-entity transport | 구조 환자와 생포 동물은 carrier transform 자식으로 붙고 item cargo kg를 쓰지 않는다. generic 29kg hard cap이나 이동속도 페널티를 다시 연결하면 응급 구조가 막히고 승인된 전용 운송 계약과 충돌한다. | item kg·hard cap·속도 효과는 0으로 고정한다. 별도 `EntityTransportProfile`은 subject identity, carrier/equipment requirement, path, ownership, interruption, save/restore와 동시 운반 금지만 소유한다. entity 운송 중 신규 item haul과 두 번째 entity ownership을 금지하고 진단용 상태만 기록한다. |
| P2 | capacity 실패 설명 | count 기반 메시지를 그대로 두면 사용자는 kg 부족, category 거절, inbound 예약, technical fragmentation을 구분할 수 없다. 잘못된 버그 신고와 밸런스 오판이 반복된다. | 실패 결과에 `storedGrams`, `reservedInboundGrams`, `incomingGrams`, `maxGrams`, category, owner destination과 typed reason을 노출한다. UI에는 kg와 수량을 서로 다른 라벨로 표시한다. |
| P2 | authoring·inspection ergonomics | 기존 Inspector의 `capacity`/`보관량`과 float `unitWeight`만 보면 디자이너가 count·kg·gram·retail slot을 혼동하기 쉽다. | dimension이 들어간 필드명/툴팁, per-stack kg·7일 reserve·70/90% utilization 미리보기, exact-item identity/transform 검사를 Editor audit에 제공한다. runtime fallback은 만들지 않는다. |
| P2 | 계획 문서 Git 거버넌스 | 저장소 `.gitignore`가 일반 `docs/game-design/*`를 제외하므로 이 계획 파일은 명시적 추적이 없으면 구현 PR에서 사라진다. | 구현 브랜치에서 이 단일 파일을 `git add -f`로 추적하고 PR manifest와 append-only baseline v2 record에 exact SHA-256을 남긴다. 저장소 전체 docs ignore 정책은 바꾸지 않는다. |

이 표는 “새 시스템을 전부 추가한다”는 뜻은 아니지만 창고는 사용자의 확정 결정에 따라 예외다. 창고 수용량은 별도 L/부피 필드 없이 kg 기반 mass capacity로 전환한다. 이 값은 현실 부피 시뮬레이션이 아니라 “무거울수록 더 많은 저장 능력을 소비한다”는 게임플레이 저장 추상이다. 과거 세이브 변환은 계속 제외한다.

### 2.6 2026-08-20 엄격 감사 결론

현재 코드 역참조 감사에서 분류된 미폐쇄 경계는 `P0 16개 / P1 13개 / P2 3개`다. 이는 후보 수치 12개가 틀렸다는 뜻이 아니라, 그 수치를 창고·운반·생산·상점·원정·부패·구조 전역에 안전하게 적용할 기반이 아직 완성되지 않았다는 뜻이다.

- P0는 질량/정확한 item identity 삭제·복제, warehouse overcommit/orphan, untyped Sink, save 교차 권위처럼 구현 전에 반드시 닫는다.
- P1은 처리량·공정성·성능·동적 한도·entity transport ownership처럼 수치 승인과 PlayMode 전에 닫는다.
- P2는 실패 설명과 authoring/UI 차원 표기이며 correctness 게이트를 대체하지 않지만 최종 적용 전에 완료한다.
- 이번 감사에서 gameplay code, ScriptableObject kg, WU, EWU, 가격, 창고 수치는 추가 변경하지 않았다.
- 다음 구현 순서는 `공용 gram mass query/typed disposition → warehouse gram index+lease+atomic admission → lifecycle/save → virtual/buffer/expedition/in-place transform → 수치 재산정`이다.

추가 적용 중단 조건:

- P0 행의 권위·생산자·소비자·저장·실패 정책 중 하나라도 `unknown`이면 새 `unitWeight` 적용 금지
- P1 행은 대표 샘플이 아니라 변경 item의 실제 연결 경로 전수 목록이 있어야 `coupled-pass`
- kg를 고치기 위해 output count, maxStack, capacity, 효능을 바꾸는 순간 해당 값의 원래 밸런스 도메인 테스트를 다시 실행
- 질량 귀속·소유권 PASS와 게임 밸런스 PASS를 같은 marker로 합치지 않음

### 2.7 Phase 23 최종 문서 감사에서 닫은 설계 결함

- density×volume×packing 식의 permille 분모를 `1,000,000`으로 교정하고 bulk-density 이중 할인을 금지했다.
- 일반 item authoring origin과 runtime `unitWeight` projection을 분리해 이중 writer를 금지했다.
- `ItemDefinitionId`/`PhysicalMassGrams`의 assembly 이동, namespace/type identity와 `MovedFrom` 계약을 명시했다.
- warehouse query만 있던 계획에 exact-lot admission token, receipt, idempotent commit, rollback·lock order·game-clock expiry를 추가했다.
- item 행 하나에 warehouse/recipe/contract/haul 값을 억지로 축약하지 않고 normalized relation 원장으로 분리했다.
- process fluid, product-bound input, process fuel과 packaging이 서로 중복 계상되지 않게 WIP/Transform 식을 교정했다.
- Source/Transfer/Transform/Sink를 closed disposition command/receipt와 terminal-sink gram으로 구분했다.
- 독립 확률 output line 전체를 key-addressed realized outcome으로 먼저 저장하고 physical/special output을 all-or-nothing commit하도록 고쳤다.
- FacilityBuffer 산정에서 main output뿐 아니라 부산물·반환 포장과 이미 예약된 resolved output을 포함했다.
- current-format root/physical/production/warehouse version matrix와 AI wake 전 restore publication 순서를 고정했다.
- living entity transport에 item kg/hard cap/속도 페널티가 다시 들어가는 모순을 제거했다.
- 다중 destination Pick-and-Haul은 correctness migration과 분리된 후속 최적화로 내렸다.
- fixed item/recipe/equipment 수는 snapshot 기대값으로만 두고 매 실행 source inventory drift gate를 추가했다.
- 성능 `2ms/0B`의 timed region, 환경, 10,000-operation batch와 비권위 머신 비교 규칙을 고정했다.

이 감사에서 gameplay code, ScriptableObject, save schema, kg, WU, EWU, 가격과 창고 수치는 변경하지 않았다.

### 2.8 교차 시스템 연계 완전성 매트릭스

현재 non-Editor 물리 item·storage 표면은 Items 폴더에만 있지 않다. 현재 source inventory에서는 관련 표면이 수백 개 파일과 수십 개 상위 도메인에 걸쳐 있으며, `TryConsumeStackQuantity`, raw spawn, delivery request, destination release/remove, count-capacity reader가 서비스 경계를 통과한다. 따라서 다음 매트릭스의 모든 행을 구현 매니페스트와 증거에 연결하기 전에는 “다른 시스템 연계 완료”로 판정하지 않는다.

| 도메인/경계 | 현재 물리 역할과 대표 seam | V27 목표 권위 | 저장·롤백 결합 | 필수 증거 |
|---|---|---|---|---|
| Foundation·item catalog | string item ID, float unit kg, category 선택 | canonical `ItemDefinitionId`, positive gram projection, exact definition revision | catalog digest와 current-format source digest가 restore/apply 전 일치 | ID 이동 compile, projection 1g exact, catalog reload |
| Items repository·transfer | raw spawn/remove/split/merge/state/destination mutation | typed Source/Transfer/Transform/Sink와 exact lot transaction | physical candidate와 disposition receipt를 같은 commit ID로 검증 | mutation manifest, residual 0g, idempotent retry |
| Warehouse·building lifecycle | count capacity, raw Stored destination, demolition/relocation | positive gram capacity, destination admission token, empty-only lifecycle | building owner·physical Stored lot·inbound token cross-section join | partial admission, over-capacity restore, invalid-owner rollback |
| Character carry·AIHaul·presentation | generic unit weight, carried inventory, reservation, interruption | common instance mass query, quantity lease+gram token, `RecoveryPending` | character carry intent와 physical lot/token을 AI wake 전에 rebind | carry band, downed drop, drop-failure ownership retention |
| Work·construction·repair | delivered count와 category cost, incorporated material | exact delivered lot, WIP/incorporated-mass Transfer/Transform | work order terminal/cancel과 physical receipt atomic join | no duplicate request, cancel conservation, restore exact |
| Production·crop·husbandry·waste | count-delivered/pending, sequential consume/spawn, Source yield | exact input role, keyed outcome, prepare-all/commit-all output | domain operation owns WIP/result/commit; physical output owns lots | probability no-reroll, output-space wait, Source budget |
| Survival·needs·food·water | item Sink, reusable package, primitive fallback | typed need Sink, tare return/waste, exact nutrition/fluid disposition | action epoch, consumed lot and returned package exact-once | cancel 0 mutation, partial consume, package conservation |
| Medical·surgery·rescue | medicine delivery/Sink, surgery WIP, living transport | exact medical lot/claim, typed treatment Sink, entity transport outside item kg | order/subject/facility/claim/physical receipt candidate join | delivery, cancel, repeated restore, entity ownership 0 leak |
| Combat·equipment·apparel | unique instance, module/ammo state, abstract equipment-stored event | one instance mass projector and physical ownership throughout | combat aggregate/component mirror/loadout/storage exact join | module/ammo revision, harness once, virtual sink 0 |
| Research·blueprint·knowledge | physical blueprint delivery, knowledge inputs, archive destination | exact claim/lot, physical-to-nonphysical Transform where intended | research task/archive owner and haul intent restore order | auto-dispatch, archive claim, consume receipt, rollback |
| Economy·shop·contracts·factions | virtual stock, sequential payment, price/reward per unit | exact retail/payment lot or explicit ledger Transform/Sink | treasury/domain operation and physical payment commit atomicity | partial payment 0, return exact, kg-price/reward audit |
| Captivity·circus | category care/feed, durable labor tool assignment, interaction output | exact item selection; durable tool is Transfer, not Sink; output transaction | captive/order/tool instance/physical lot save join | release/death/cancel return, feed identity, output residual 0g |
| Defense·invasion | maintenance/ammo/category supply, signal horn/alliance kit | exact persisted pending commitment and unique/durable Transfer | facility/event terminal state owns request and returned/consumed lot | ignored-result 0, duplicate request 0, terminal cleanup |
| Offense·expedition | packed supplies, abstract burden/loot, urgent mitigation | exact packed input/tare, burden grams, keyed reward Source | package/event/claim/physical lot round-trip and exact-once return | cancel/return no mint, packed burden, consume/revoke |
| Wildlife·environment | carcass Source, capture feed, spoilage/rot | species projector, exact Source, atomic in-place Transform | wildlife/carcass/spoilage physical identity restore join | carcass mapping, Stored spoilage destination, residual 0g |
| Facility evolution·relocation | sequential request and broad destination release | operation-owned all-or-nothing reserve/release receipt | evolution order, facility identity and exact returned lots join | partial request 0, completion/cancel exact release |
| Industrial·conveyor·utility | count `CanStore`, transit state, fuel/fluid/waste | transit destination gram token, exact item, separate hydraulic authority | conveyor payload/token and utility WIP current-format join | preferred/fallback admission, transit fault rollback |
| Run·start-party·events·rewards | direct source grants/spawns and incident rewards | typed Source with stable event/commit ID and exact grams | run/event terminal receipt prevents repeated grant | save/reload no duplicate source, unknown reward 0 |
| UI·summary·diagnostics | count capacity, generic carry kg, localized strings | read-only common mass/capacity queries and typed failure projection | UI/diagnostics are non-authoritative and publish after commit | every reader manifest row, kg/count dimension labels |
| Save·restore transaction | independent sections/participants and derived caches | explicit partial order, aggregate preflight, reversible publication | all active operation IDs join exactly once before AI activation | failure at every phase leaves prior world fingerprint exact |
| EWU·price·balance ledger | unit kg affects logistics EWU/price/source digest | canonical gram snapshot feeds V27 ledger once | approved source digest and asset projection must match | recalculation, SCC/arbitrage, approval invalidation |
| Editor builders·validators·fixtures | generated SO projection and direct debug mutation | one authoring origin, deterministic projection, fixture-only bypass | no runtime fallback; YAML/meta/GUID/FileID stability | builder rerun byte identity, fixture API segregation |

매트릭스 규칙:

- 한 행의 representative test만 통과해도 도메인 전체가 완료된 것으로 보지 않는다. 해당 행에 속한 production callsite가 아래 manifest에 전부 있어야 한다.
- 하나의 callsite가 여러 역할을 수행하면 역할별 행을 분리한다. 예를 들어 포로 도구의 물리 stack 제거와 captive state assignment는 `Sink` 한 행이 아니라 `Transfer prepare/commit/rollback` 행이다.
- category requirement 자체는 허용할 수 있지만, 실제 예약 전에 exact item ID·instance/component lot과 gram을 한 번 고정한다. 실패·취소·복원 때 다른 category item으로 대체하지 않는다.
- 읽기 경로도 migration 대상이다. write 경로가 gram을 써도 AI·UI·작업 선택기가 count 또는 generic kg를 읽으면 완료가 아니다.
- 도메인 save payload에 새 active-operation 필드가 실제 필요하면 그 도메인 current version을 같은 change set에서 올린다. 과거 version 변환은 추가하지 않는다.

### 2.9 호출부 단위 migration manifest 계약

aggregate 숫자 `remaining=0`만으로는 indirect interface call, adapter forwarding, event-based virtual storage와 새 호출부 추가를 잡을 수 없다. 구현 시 Roslyn semantic symbol scan과 명시적 domain registration을 합쳐 다음 normalized artifact를 만든다.

`Artifacts/QA/v27-mass-cross-system-callsite-manifest.csv` 최소 열:

```text
sourceAssembly
sourceFile
sourceSymbol
sourceLineIdentity
domain
currentApiSymbol
semanticFlowRole
exactItemIdentityRule
lotOrInstanceRule
currentCapacityDimension
targetCommandSymbol
sourceQuantityLeaseOwner
destinationCapacityOwner
domainOperationOwner
commitOwner
rollbackOwner
saveSectionId
saveVersion
restoreParticipantId
restorePhase
uiDiagnosticConsumers
typedFailureCodes
focusedEvidenceId
migrationDisposition
sourceDigest
```

`migrationDisposition`은 다음 closed set만 허용한다.

```text
typed-source
typed-transfer
typed-transform
typed-sink
nonphysical-ledger
editor-fixture
migrated-reader
removed-legacy
```

금지 상태:

- `unknown`, `temporary`, `compatibility`, `legacy-allowed`, 빈 값
- file path/문자열 검색만으로 분류한 행
- exact symbol binding 없이 같은 이름 메서드를 승인한 행
- Editor fixture 승인을 production assembly callsite에 재사용한 행

수집 범위:

- `IWorldItemStackRuntime`, `IItemTransferService`, `IProductionItemGateway`와 모든 구현/adapter/extension 호출
- repository add/remove/split/merge/state/destination/component mutation
- `EquipmentStoredEvent`와 physical lot을 추상 ledger로 바꾸는 모든 event
- count `MaxCapacity`, `RemainingCapacity`, `CanStore`, generic `UnitWeight`, carry weight reader
- delivery pending/delivered count, destination release/remove, low-level Stored transition
- builder/validator/debug fixture의 우회 API
- reflection, delegate, event subscription과 interface alias를 통한 간접 호출

생성기는 이전 approved manifest의 semantic key와 현재 compilation을 대조한다. 새 production callsite가 분류 없이 생기면 `MASS_CALLSITE_MANIFEST_DRIFT`로 compile/CI를 실패시킨다. 단순 `rg` 결과는 inventory 보조 증거일 뿐 완전성 권위가 아니다.

### 2.10 교차 도메인 transaction·publication 불변식

모든 물리 질량 변경 작업은 다음 상태 기계를 공유한다.

```text
Requested
-> Prepared
-> SourceReserved
-> DestinationReserved
-> DomainPrepared
-> PhysicalCommitted
-> DomainCommitted
-> Published

failure before PhysicalCommitted
-> RolledBackWithoutMutation

failure during/after commit publication
-> CommittedPublicationPending
-> Published
```

필수 계약:

1. `operationId`는 gameplay 의도를, `commitId`는 exact-once 물리 commit을 식별한다. actor ID·destination ID를 commit ID로 재사용하지 않는다.
2. prepare는 physical/domain state를 바꾸지 않는다. 모든 input lot, output lot, gram, destination, capacity revision과 domain candidate를 immutable fingerprint로 고정한다.
3. source quantity lease와 destination gram token은 서로 다른 권위지만 한 coordinator가 획득·해제 순서를 소유한다.
4. physical repository commit과 domain aggregate commit 사이에 외부 subscriber가 partial state를 보지 못한다.
5. event/UI/diagnostic publication은 commit 이후 durable outbox/재시도 경계에서 수행한다. subscriber exception이 committed physical state를 되돌리거나 같은 Source/Transform을 재실행하게 하지 않는다.
6. 같은 commit ID+fingerprint 재시도는 같은 receipt를 반환한다. 같은 ID+다른 fingerprint는 즉시 실패한다.
7. destination release/remove는 문자열 prefix sweep이 아니라 operation이 소유한 exact lot/token 목록을 prepare하고 release receipt를 남긴다. carried/transit lot은 물리적으로 현재 위치/상태를 보존한다.
8. partial admission은 accepted lot/gram만 commit하고 remainder ownership을 원래 source/carry에 유지한다. remainder를 일반 Loose로 만들어 성공 처리하지 않는다.
9. lock 순서는 `domain stable ID -> source stack ID ordinal -> destination stable ID ordinal -> token ID ordinal`로 고정하며 reentrant callback은 commit guard로 차단한다.
10. terminal operation prune은 domain aggregate와 physical receipt가 모두 재시도 불필요 상태임을 증명한 뒤에만 한다.

도메인별로 별도 transaction을 새로 발명하지 않는다. production, faction payment, captivity tool assignment, facility evolution, expedition packing, surgery consumption과 spoilage가 같은 coordinator protocol 또는 동일 불변식을 구현한 검증된 adapter를 사용해야 한다.

### 2.11 save·restore 교차 권위와 AI wake fence

`Artifacts/QA/v27-mass-save-restore-order.txt`는 현재 compilation에서 모든 관련 section/participant를 수집해 다음 partial order를 증명한다.

```text
definition/facility candidates valid
-> physical item/instance candidates valid
-> character carry and domain active-operation candidates valid
-> WIP/retail/expedition/captivity/defense/evolution joins valid
-> quantity leases and destination gram tokens rebuilt
-> physical/domain candidates published reversibly
-> UI/event projections published
-> character and wildlife AI activation
```

필수 규칙:

- ordinal participant ID만 우연히 맞아서 통과시키지 않는다. manifest에 각 participant의 required predecessors와 published authority를 기록하고 cycle/누락을 실패시킨다.
- capture 중 `Prepared` 또는 `SourceReserved`만 있고 physical commit이 없는 작업은 저장하지 않거나 deterministic pending-replan으로 정리한다.
- `PhysicalCommitted` 이후 publication만 남은 작업은 commit receipt와 outbox를 저장해 restore 후 물리 변이 없이 publication만 재개한다.
- active WIP, carried haul, conveyor transit, captivity tool, expedition package, defense/invasion supply, facility evolution payment은 domain operation ID와 exact physical lot/commit ID가 1:1 또는 명시적 1:N으로 join되어야 한다.
- orphan physical commitment, orphan domain operation, duplicate commit ID, mismatched lot fingerprint, missing destination owner는 전체 current-format restore를 rollback한다.
- valid warehouse over-capacity만 예외다. stock을 보존하고 신규 admission을 막으며 evacuation을 게시한다.
- 모든 participant failure injection에서 restore 전 world semantic fingerprint, physical inventory, leases/tokens, domain aggregates, outbox와 AI registration이 exact하게 복구된다.
- AI wake는 모든 cross-section join과 reversible publication이 끝난 뒤 한 번만 허용한다. restore candidate가 scheduler/Brain에 조기 등록되는 경로는 gate 실패다.

과거 save 마이그레이션을 제외하는 결정은 이 엄격함을 낮추지 않는다. 지원하는 current-format payload의 필수 필드·version·digest 누락은 typed incompatibility이고, 부분 복원 fallback은 없다.

### 2.12 read side·UI·진단·현지화 폐쇄

write authority를 gram으로 바꾼 뒤 다음 reader가 구 count/generic kg를 읽으면 게임 로직과 표시가 갈라진다. 구현 manifest에 최소 다음 concrete reader를 포함한다.

- `CharacterCarryInventory`, `CharacterCarryPresentation`, character summary/status/combat presenter, AI utility/debug formatter
- `WorldItemHaulPlanningService`, work target selector/executor, conveyor gateway, shop inventory adapter, facility evolution resource provider, offense preparation
- warehouse feature surface, building summary/management query, operating-day settlement, gameplay flow diagnostics
- item pile/info UI, storage policy UI, production/FaultRecovery diagnostics와 balance capture
- `MaterialEconomicProfileSO`, V23 Before calculator, V27 EWU/price/logistics calculator

표시 계약:

- gameplay admission은 integer gram, UI는 locale-aware kg projection이다. UI 문자열을 gameplay가 역파싱하지 않는다.
- `현재/최대 kg`, `reserved inbound kg`, `incoming kg`, item/stack count, category policy를 서로 다른 라벨로 표시한다.
- count-era `무제한`, `남은 칸`, `capacity N개` 문구를 production warehouse에서 제거한다.
- 거부 reason은 `CapacityMassInsufficient`, `CategoryRejected`, `DestinationInvalid`, `InboundReservationConflict`, `TechnicalRecordLimit`, `StatefulMassChanged`, `RecoveryPending`처럼 closed typed code로 전달한다.
- localization key는 typed failure code에 매핑하고 source reason string을 authority로 사용하지 않는다.
- diagnostics/telemetry는 query snapshot만 읽고 revision이나 gameplay state를 올리지 않는다.
- commit 이전에는 UI event를 발행하지 않는다. publication retry가 중복 toast, audio, quest progress 또는 통계 증가를 만들지 않는다.

### 2.13 authoring·builder·validation·asset pipeline 연계

질량 원장과 실제 Unity content가 갈라지지 않게 다음 단방향을 강제한다.

```text
approved authoring origin
-> deterministic builder projection
-> ScriptableObject runtime field
-> runtime mass query
-> ledger/read-side projection
```

- item마다 `massDerivationKind`가 authoring origin을 정확히 하나 고른다. origin과 projected field를 같은 apply에서 독립 patch하지 않는다.
- combat equipment, apparel, carcass, packaged lot은 domain builder가 common physical definition으로 1g exact projection한다.
- builder가 생성하는 asset, hand-authored asset, sub-asset, catalog entry를 source manifest에서 구분하고 누락/중복 stable ID를 실패시킨다.
- domain catalog/build order가 바뀌어도 동일 input이면 동일 YAML/GUID/FileID와 projection을 만든다.
- direct Inspector edit, builder regeneration, domain reload, clean checkout generation의 네 경로가 같은 gram과 source digest를 만들어야 한다.
- Editor fixture는 production catalog를 오염시키지 않는 fixture-only authority를 사용한다. fixture unlimited warehouse/category spawn은 production interface로 노출하지 않는다.
- ApplyApproved 이전에 dirty asset, stale Before, user modification, missing baseline record, source digest drift를 검사한다.
- ApplyApproved 이후 builder 재실행, catalog reload, audit regeneration을 수행하고 두 번째 실행 byte diff 0을 요구한다.

### 2.14 fault matrix와 current-format cutover

각 migrated operation은 다음 fault를 최소 한 번 주입한다.

| 시점 | 주입 fault | 요구 결과 |
|---|---|---|
| prepare 전/중 | invalid item/component/category/owner, overflow | state·lease·token·event 변화 0 |
| source reserve 후 | source shrink/despawn/signature change | source lease release, destination token 0, physical 변화 0 |
| destination reserve 후 | warehouse full/policy change/destroy/relocate | cargo/source 보존, token exact release, typed retry/fail |
| domain prepare 후 | order cancel/subject death/facility disable | domain candidate rollback, physical 변화 0 |
| physical commit 중 | split/merge/spawn/component write failure | repository/lease/token/domain 모두 prior fingerprint |
| domain commit 중 | aggregate validation/publication failure | physical+domain atomic rollback 또는 durable committed-pending; ambiguous 상태 금지 |
| event/UI publish 중 | subscriber exception, scene unload | committed state 유지, outbox retry, gameplay mutation 중복 0 |
| save capture 중 | each transaction phase | unsupported intermediate phase 저장 0; supported phase exact resume |
| restore prepare/publish/complete 중 | section/participant late failure | restore 이전 world·AI registration exact rollback |
| interruption | actor Downed/Dead/Disabled, no-path, capacity shrink | current-cell recovery 또는 retained cargo; teleport/delete 0 |
| terminal retry | cancel/complete/return/reward 두 번 | 두 번째 physical/domain mutation 0 |

cutover 규칙:

- shadow 비교는 AuditOnly에서만 허용한다. production gameplay가 count capacity와 gram capacity 중 하나를 runtime flag로 선택하는 이중 권위는 만들지 않는다.
- compile-green 수직 슬라이스 동안 compatibility adapter를 둘 수 있지만, adapter는 새 typed command를 호출하는 단방향 bridge여야 한다. raw legacy mutation을 내부에 숨기지 않는다.
- 최종 수치 ApplyApproved 전 production `unknown/compatibility/legacy` manifest 행, count admission writer, category physical spawn, untyped removal, duplicate mass reader/writer는 모두 0이어야 한다.
- 과거 save converter는 만들지 않는다. 지원하지 않는 기존 payload는 명시적 안내와 typed restore failure를 내며 일부 state를 불러오지 않는다.
- 현재 scene/bootstrap/DI registration, Editor test factory, debug scenario, build validation, save fixture와 final acceptance를 같은 cutover change set에서 갱신한다.
- 한 도메인이라도 새 interface compile만 맞고 live producer/consumer/save/UI 증거가 없으면 그 슬라이스는 `structural-partial`이며 다음 numeric apply를 막는다.

### 2.15 구현 소유권·assembly·파일 경계

계약 타입의 위치를 구현 중에 다시 결정하지 않는다. 현재 assembly graph를 기준으로 다음 소유권을 고정한다.

| 역할 | assembly/계층 | 권위 파일 또는 신규 파일 | 금지 의존 |
|---|---|---|---|
| canonical item ID | `DungeonStory.Foundation` | `Assets/Scripts/Services/Foundation/ItemDefinitionId.cs` | Economy·Items·Unity object 역참조 |
| gram·mass subject/lot/query 계약 | `DungeonStory.Items` | `Assets/Scripts/Models/Items/Core/PhysicalMassContracts.cs` | Combat·Environment·Economy concrete type |
| warehouse authored gram·read model | `DungeonStory.Buildings` | `BuildingCoreAbilityDefinitions.cs`, `WarehouseInventory.cs` | Items의 concrete runtime/service 역참조. cross-assembly 값은 `long` gram과 Foundation ID만 사용 |
| mass projector composition | `Assembly-CSharp` integration | `Assets/Scripts/Services/Items/PhysicalItemMassQuery.cs`와 domain projector 파일 | save DTO를 gameplay query 입력으로 사용 |
| warehouse index·admission registry | `Assembly-CSharp` Items service | `PhysicalStockQuery.cs`, `WarehouseMassAdmissionService.cs` | UI/Editor 권위, 전역 repository revision만으로 unrelated warehouse token 무효화 |
| staged physical transaction | `Assembly-CSharp` Items service | 신규 `PhysicalItemTransactionCoordinator.cs`; repository 내부 mutation batch는 `WorldItemRepository`가 소유 | external event를 physical/domain commit 전 발행, raw caller의 직접 rollback |
| haul/carry/deposit | `Assembly-CSharp` Items service | `ItemTransferService.cs`, `WorldItemHaulPlanningService.cs`, `AbilityHaul.cs`, `CharacterCarryInventory.cs` | count capacity, generic unit mass, drop-on-full 성공 처리 |
| production WIP core authority | `DungeonStory.Economy` | `Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs`, production bill/save DTO projector | `DungeonStory.Production` core asmdef에 Items concrete type을 넣는 새 edge |
| WIP/output transaction orchestration | `Assembly-CSharp` Economy service | `Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs`, 신규 `Assets/Scripts/Services/Economy/ProductionMassTransactionRuntime.cs` | physical mutation을 우회하는 별도 output writer |
| stateful combat/apparel/carcass projector | 각 domain concrete를 볼 수 있는 `Assembly-CSharp` integration | Combat·Environment·Wildlife integration projector | Items core에서 domain codec 호출 |
| save validation·restore projection | 각 existing save section + Infrastructure restore coordinator | `PhysicalItemsSaveSection.cs`, `PhysicalItemSaveValidation.cs`, `ProductionBillsSaveSection.cs`, 관련 domain save section | save publish 이후 처음 cross-authority 검증, AI wake 선행 |
| read side/UI | Presentation·Building adapters | `WarehouseFeatureSurfacePresenter.cs`, `BuildingSummaryFormatter.cs`, `BuildingManagementWorldQueryAdapter.cs`, `OperatingDaySettlementApplicationAdapter.cs`, diagnostics | UI 문자열 역파싱, count를 kg capacity로 표시 |
| Editor inventory·CI | Editor assembly | 신규 semantic manifest/fault/evidence generator | `rg` 문자열 결과만으로 production coverage 승인 |

`DungeonStory.Items`는 이미 `DungeonStory.Buildings`와 `DungeonStory.Foundation`을 참조하고, `DungeonStory.Economy`는 Items를 참조한다. 이 방향을 뒤집거나 Buildings/`DungeonStory.Production` core가 Items service를 참조하는 edge를 추가하지 않는다. Economy의 production bill aggregate는 Items의 immutable lot/receipt 계약을 참조할 수 있지만 concrete repository/service를 참조하지 않는다. live repository, output handler와 domain aggregate를 함께 조정하는 coordinator는 `Assembly-CSharp` integration이 소유한다.

### 2.16 staged mutation과 publication의 실제 구현 계약

현재 repository에는 외부 도메인까지 포함한 범용 transaction snapshot이 없다. 따라서 “repository transaction”을 추상 문구로 남기지 않고 다음 방식으로 구현한다.

1. `PhysicalItemTransactionCoordinator`가 source stack revision, destination owner, exact affected stack ID, component fingerprint, quantity lease, gram token, domain operation fingerprint를 immutable prepare record로 캡처한다.
2. `WorldItemRepository`는 affected record만 복제한 `PhysicalItemMutationBatch`를 만든다. 전체 world 복제나 broad rollback은 하지 않는다.
3. split/merge/state/destination/component mutation은 live dictionary에 바로 쓰지 않고 batch candidate와 candidate-derived index에서 검증한다.
4. domain adapter도 live aggregate를 바꾸지 않고 publish 가능한 candidate 또는 reversible delta를 prepare한다.
5. coordinator는 physical candidate, domain candidate, receipt/outbox candidate를 모두 검증한 뒤 한 main-thread commit guard 안에서 순서대로 publish한다. observer event는 guard 해제 후 outbox에서 발행한다.
6. publish 전 실패는 candidate 폐기다. publish 중 예외는 touched physical record·index revision·lease/token·domain delta의 undo journal을 역순 적용하고 prior fingerprint를 대조한다.
7. 이미 외부에 관측된 irreversible domain effect는 rollback 대상으로 허용하지 않는다. 그런 effect는 physical/domain commit 뒤 durable outbox consumer가 exact-once로 수행하며 `CommittedPublicationPending`에서 재개한다.
8. batch는 repository item-stack global revision을 마지막에 정확히 한 번 올리고, affected warehouse-local revision만 정확히 한 번 올린다. no-op은 둘 다 올리지 않는다.

undo journal은 save authority가 아니다. save capture가 가능한 live phase는 `Published` 또는 명시적으로 지원하는 `CommittedPublicationPending`뿐이다. 그 외 candidate/undo 상태에서 save 요청이 오면 현재 frame commit 종료까지 capture를 fence하고, timeout이면 typed save failure를 낸다.

### 2.17 warehouse-local revision과 token lifecycle

전역 `WorldItemRepository.ItemStackVersion`은 cache rebuild trigger로는 사용할 수 있지만 admission 충돌 권위로 사용하지 않는다. 무관한 창고·Loose stack 변화가 active token을 무효화하지 않도록 warehouse마다 다음 revision을 둔다.

```text
WarehouseCapacityRevision
+= 1 exactly once when any of these changes:
   authored capacity projection
   Stored mass/index for this warehouse
   reservation ledger for this warehouse
   category policy
   warehouse lifecycle/owner validity
```

요소 revision을 산술 합산하면 서로 다른 변화가 같은 합을 만들 수 있으므로 금지한다. 구현은 warehouse별 checked monotonic `long` 하나를 권위로 사용하고, 한 transaction이 여러 요소를 바꿔도 publication 시 정확히 한 번만 증가시킨다. 변경 원인은 별도 flags/diagnostic field로 기록한다.

- request는 `ExpectedWarehouseCapacityRevision`, exact source stack/component revisions와 `ExpectedCatalogRevision`을 받는다. 전역 repository revision 필드는 admission request에서 제거한다.
- token reserve는 warehouse-local lock 안에서 remaining gram을 계산하고 reservation ledger를 변경한 뒤 **post-reserve warehouse revision**을 token에 고정한다.
- 다른 operation의 합법 reserve/commit은 token을 자동 무효화하지 않는다. registry가 이미 확보한 `ReservedMassGrams`를 소유하므로 commit은 token 상태와 exact lot/source revision을 검증한다.
- capacity·category·owner·lifecycle 변경은 active token을 `Invalidated`로 전환하고 owner operation에 typed retry를 게시한다. 조용히 dictionary에서 제거하지 않는다.
- lifecycle closed set은 `Reserved -> Committed | Released | Expired | Invalidated`다. terminal state를 다시 reserve/commit/release하면 같은 operation/fingerprint에는 idempotent receipt, 다른 payload에는 conflict를 반환한다.
- expiry tombstone과 commit receipt는 owner operation이 재시도할 수 있는 기간 동안 유지한다. terminal domain operation과 save/outbox 참조가 모두 사라진 뒤 deterministic prune한다.
- pre-pickup `Reserved` token은 save하지 않고 pending-replan으로 정리한다. pickup-committed carry/conveyor/WIP token은 owner operation payload의 exact lot/gram에서 restore candidate로 재구축한다. `CommittedPublicationPending` receipt/outbox는 current-format save에 남긴다.
- `StoredMassGrams` cache는 global stack revision과 mass-authority revision으로 무효화할 수 있으나, token correctness는 위 local ledger가 소유한다.
- 현재 worktree의 `WarehouseMassAdmissionService` 초안이 global `ExpectedRepositoryRevision`/`ItemStackVersion`을 token correctness로 쓰거나 expired state를 삭제해 typed tombstone을 잃는다면 이 v4 계약에 부합하지 않는다. compile 성공 여부와 무관하게 slice 2 accepted implementation으로 승격하지 않고 local revision·terminal state·staged commit으로 교체한 뒤 evidence를 다시 만든다.

---

## 3. 목표 운반 경험

### 3.1 운반 밴드

서로 다른 두 지표를 분리한다.

1. `RecipeInputBatchKg`: 한 레시피 사이클이 요구하는 입력 재료 총 kg
2. `ActualHaulPlanKg`: AI가 여러 compatible stack을 묶은 한 번의 실제 운반 총 kg

목표는 다음과 같다.

| 구분 | 목표 kg | 의미 |
|---|---:|---|
| 소형 긴급 운반 | 0.1~6 | 약품, 설계도, 탄약, 한 끼 등. 최소 적재를 강제하지 않음 |
| 일반 레시피 입력 묶음 | 6~11 | 평범한 생산 1회 투입 BOM |
| 일반 실제 혼합 운반 | 8~14 | 인접·동일 목적지 스택을 함께 실은 실전 운반 |
| 무거운 묶음 | 15~20 | 광석·석재·대형 부품·집중 건설 투입 |
| 일반 actor 무감속 목표 | 약 19 | 일상 물류가 감속 없이 끝나는 상한 |
| 일반 actor 최대 | 약 29 | 예외적 과적. 상시 물류 목표가 아님 |
| 멜빵 무감속 목표 | 약 24 | 무거운 전문 물류를 안정적으로 처리 |
| 멜빵 최대 | 약 36 | 정말 무거운 단일 장비의 예외 수송 |

`6~11kg`와 `8~14kg`는 모순이 아니다. 레시피 하나의 입력은 6~11kg가 기본이고, AI가 같은 목적지 또는 compatible leg의 다른 스택을 추가로 묶어 실제 왕복은 8~14kg가 되도록 한다.

### 3.2 과적 구간 사용 규칙

- 반복 원료·식품·중간재 레시피는 일반 actor의 과적 구간을 정상 처리량으로 사용하면 실패다.
- 일반 생산의 p50/p75 운반은 19kg 무감속 안에 들어와야 한다.
- 15~20kg 묶음은 중량물로 분류하고 멜빵·육중한 체격 actor에게 우선 배정할 수 있다.
- 20kg를 넘는 일반 원료·식품·중간재 단일 단위는 `MASS_UNHAULABLE_ORDINARY_ITEM` Critical이다.
- 일반 generic haul에서 20kg 초과 과적을 합법적으로 쓰는 항목은 원칙적으로 정말 무거운 unique 전투 장비뿐이다.
- 사체·포로·환자처럼 별도 구조·호송·동물 운반 행동을 사용하는 대상은 generic item kg 밴드와 분리한다.
- 사체가 generic item stack으로 운반된다면 20kg 초과를 자동 승인하지 않는다. 전용 운반 경로가 실제로 연결됐는지 확인하고, 없으면 단위 분할 또는 20kg 이하 packed carcass 단위로 교정한다.
- 시설 키트는 포장 단위이므로 일반적으로 `8~14kg`, 대형 키트도 `19kg 이하`를 목표로 한다. 건설 BOM 전체 질량을 한 키트에 그대로 넣어 운반 불가능하게 만들지 않는다.
- 설정 UI의 최대 배율 `1.0~2.5`는 접근성·스트레스 범위다. 밸런스 목표는 기본 `1.5`에서 맞추며 `2.5`를 전제로 콘텐츠를 설계하지 않는다.

### 3.3 작은 운반의 정당성

AI가 항상 8kg 이상을 채울 때까지 기다리면 긴급 약품·한 끼·탄약·설계도가 교착된다. 다음은 8kg 미만이어도 즉시 운반한다.

- 응급 치료·수술 재료
- hunger/thirst emergency 공급
- 전투 중 탄약·구조 물자
- exact 시설 주문의 마지막 부족 수량
- unique item, 장비, 설계도
- 부패 임박 식품
- 복구·취소·저장 정합성을 위한 잔여 스택

따라서 `8~14kg`는 실전 분포 목표이지 admission 최소값이 아니다.

### 3.4 RimWorld `Pick Up And Haul` 실제 구현에서 참고할 부분

이 계획의 직접 참고 대상은 RimWorld vanilla 운반이 아니라 Mehni의 `Pick Up And Haul` 모드다. 이름이나 플레이 인상만 참고하지 않고 공개 소스의 작업 분리, 예약, 적재, 하역 계약을 기준으로 삼는다.

확인한 권위 소스:

- [Pick Up And Haul 저장소와 기능 설명](https://github.com/Mehni/PickUpAndHaul)
- [`WorkGiver_HaulToInventory`](https://github.com/Mehni/PickUpAndHaul/blob/master/Source/PickUpAndHaul/WorkGiver_HaulToInventory.cs)
- [`JobDriver_HaulToInventory`](https://github.com/Mehni/PickUpAndHaul/blob/master/Source/PickUpAndHaul/JobDriver_HaulToInventory.cs)
- [`JobDriver_UnloadYourHauledInventory`](https://github.com/Mehni/PickUpAndHaul/blob/master/Source/PickUpAndHaul/JobDriver_UnloadYourHauledInventory.cs)
- [`CompHauledToInventory`](https://github.com/Mehni/PickUpAndHaul/blob/master/Source/PickUpAndHaul/CompHauledToInventory.cs)
- [`PawnUnloadChecker`](https://github.com/Mehni/PickUpAndHaul/blob/master/Source/PickUpAndHaul/PawnUnloadChecker.cs)

실제 모드의 핵심 흐름은 다음과 같다.

```text
가장 가까운 유효 haul seed 선택
-> seed의 더 나은 저장 위치 확인
-> 주변의 추가 haulable을 거리순으로 탐색
-> 각 source와 예상 저장 cell/container 용량을 함께 할당
-> source queue와 storage queue를 다중 예약
-> 질량 한도까지 여러 stack·여러 item type을 pawn inventory에 연속 적재
-> 운반 작업으로 들어온 inventory item만 별도 표식
-> 적재 완료·다른 haul 시작·idle·질량 임계에서 unload job 예약
-> 표식된 item만 category/definition 순으로 꺼냄
-> 현재 유효한 최선의 storage를 다시 찾고 예약한 뒤 반복 하역
```

소스에서 확인되는 세부 규칙:

- `WorkGiver_HaulToInventory`는 potential haulable을 현재 위치와의 거리로 정렬한다. 동일 거리 tie-break는 별도 stable ID로 고정하지 않으므로 DungeonStory에서는 이 부분을 그대로 복제하지 않는다.
- 추가 수집 범위는 seed에서 목적지까지 거리의 절반을 바탕으로 하되 최소 12칸을 사용한다.
- forbidden, reservation, reachability, 자동 운반 가능 여부, 더 나은 storage 존재 여부를 후보마다 다시 확인한다.
- urgent designation으로 시작한 묶음에는 urgent item만 추가한다.
- `targetQueueA`에는 source, `targetQueueB`에는 storage cell/container, `countQueue`에는 정확한 수량을 넣고 가능한 만큼 한 번에 예약한다.
- 같은 item type만이 아니라 서로 다른 item type도 inventory에 함께 넣을 수 있다.
- 목적지가 하나로 고정되지 않는다. item별 수용 필터와 stack 가능 여부에 따라 여러 storage cell/container를 미리 할당할 수 있다.
- 적재 예상 질량이 용량을 넘으면 마지막 stack 수량을 잘라 한도에 맞춘다.
- inventory에 들어온 일반 개인 소지품과 운반 화물을 구분하기 위해 `CompHauledToInventory`가 운반 화물 `Thing` 참조만 기록한다.
- 하역 때는 처음 계산한 cell을 맹신하지 않고 현재 유효한 최선의 storage를 재탐색한다.
- 하역은 한 item을 carry tracker로 옮겨 저장한 뒤 다음 표식 item으로 반복한다.
- 모드 설명과 구현 모두 일반 운반보다 효율이 크게 올라 게임을 다소 쉽게 만든다는 밸런스 비용을 인정한다.

이 구조에서 그대로 가져올 원칙:

1. `수집 단계`와 `하역 단계`를 분리한다.
2. 여러 source stack과 수량을 한 작업 단위로 예약한다.
3. item type이 달라도 compatible route면 한 inventory tour에 넣을 수 있다.
4. 개인 소지품·장착물과 물류 화물을 명시적으로 구분한다.
5. pickup 전에 목적지 수용량을 잡아 과잉 적재를 막는다.
6. 실제 하역 직전 목적지 권위를 다시 검증한다.
7. 적재 중 새 haulable이 생겨도 bounded 조건에서만 tour를 연장한다.
8. 중단·idle·용량 임계에서는 운반 화물을 잊지 않고 하역 또는 typed replan한다.

그대로 복제하지 않을 부분:

- 원본 모드는 저장 목적지 예약이 job 전환에서 풀릴 수 있음을 소스 TODO로 인정한다. DungeonStory는 operation별 quantity lease와 durable intent를 유지한다.
- 원본은 merge로 `Thing` ID가 바뀌면 같은 definition의 다른 item을 찾아 복구한다. DungeonStory는 exact stack slice·owner operation·quantity authority를 유지하고 definition-only 대체를 금지한다.
- 원본은 목적지를 찾지 못하거나 예약에 실패하면 pawn 주변에 drop할 수 있다. DungeonStory는 통로·접근칸 Floor Clutter를 막기 위해 carried slice를 보존한 typed blocked/replan 상태로 전환한다.
- 원본의 unload trigger와 vanilla haul job 결합을 위한 우회는 복제하지 않는다. DungeonStory의 Brain→AIHaul→plan→intent 경계를 사용한다.
- 원본의 inventory full 지향 적재를 그대로 쓰지 않는다. V27의 6~11kg 레시피 묶음, 8~14kg 일상 tour, 15~20kg heavy tour 목표를 우선한다.
- 원본의 최소 12칸 추가 탐색 범위를 그대로 복제하지 않는다. DungeonStory의 작은 던전에서는 detour와 shared-corridor 혼잡을 함께 측정해 상한을 정한다.

DungeonStory는 다음 점에서 `Pick Up And Haul`보다 이미 강한 권위 구조를 가진다.

```text
priority/score seed stack 선택
-> exact destination authority와 path 확인
-> 같은 목적지의 nearby compatible stack 탐색
-> 최대 6 pickup leg
-> detour 상한 min(4 cells, direct route의 약 15%)
-> 여러 item type 허용
-> operation별 quantity lease 일괄 예약
-> durable haul delivery intent
-> save/load delivery-only resume
```

현재 차이는 DungeonStory가 추가 pickup을 `같은 exact destination`으로 제한하고, 하나의 primary delivery leg만 만든다는 점이다. `Pick Up And Haul`처럼 여러 storage destination을 한 inventory tour에 묶으려면 기존 exact ownership을 약화시키지 않고 destination별 delivery leg와 commitment를 확장해야 한다.

### 3.5 `Pick Up And Haul` 참고 후 확정하는 적재·경로 정책

현재 `WorldItemHaulPlanningService.SelectOpportunisticCandidates`는 같은 목적지의 후보를 추가한 뒤 `maxAllowed`의 약 98%가 찰 때까지 적재한다. 운반 최대치를 29/36kg로 올리면서 이 규칙을 유지하면 평범한 반복 운반이 최대 과적 구간을 상시 사용하고 이동 속도 저하를 일으킨다. `Pick Up And Haul`의 inventory 활용은 도입하되 `항상 hard cap까지 채우는 것`을 목표로 삼지는 않는다.

이를 다음 두 한도로 분리한다.

```text
softTargetKg
= 일상적으로 채우고 출발할 목표

hardMaximumKg
= 단일 중량물·긴급 수송·oversize equipment만 허용하는 절대 한도
```

권장 정책 타입:

```csharp
public enum HaulLoadClass
{
    MicroUrgent,
    Ordinary,
    Heavy,
    OversizeEquipment,
    DedicatedTransport
}

public readonly struct HaulLoadTarget
{
    public PhysicalMassGrams PreferredMinimum;
    public PhysicalMassGrams PreferredMaximum;
    public PhysicalMassGrams SoftCapacity;
    public PhysicalMassGrams HardCapacity;
    public bool MayEnterOverload;
}
```

| class | 추가 적재 목표 | soft cap | hard cap | 과적 허용 |
|---|---:|---:|---:|---|
| MicroUrgent | 즉시 필요한 exact 수량 | 필요량 | 무감속 한도 | 아니오 |
| Ordinary | 실제 plan 8~14kg | actor 무감속 한도 약 19kg | 무감속 한도 | 아니오 |
| Ordinary+Harness | 실제 plan 8~14kg | 멜빵 무감속 한도 약 24kg | 무감속 한도 | 아니오 |
| Heavy | 15~20kg | min(20kg, 무감속 한도) | actor 상태별 최대 | 명시적 heavy leg만 |
| OversizeEquipment | 단일 장비 exact kg | 해당 단일 item kg | 29/36kg | 예 |
| DedicatedTransport | generic planner 대상 아님 | 전용 계약 | 전용 계약 | 전용 계약 |

선택 규칙:

1. urgent·exact order seed는 full load를 기다리지 않는다.
2. 첫 구현은 현재의 같은 exact destination 수집을 유지해 회귀 기준으로 삼는다.
3. 서로 다른 destination을 묶는 `PickAndHaulCompatibleTour`는 warehouse gram admission·destination lease·save/restore 전환이 green인 뒤의 **별도 최적화 슬라이스**다. V27 질량·창고 migration 완료 조건이나 current-source 414개 kg 산정의 선행 조건으로 삼지 않는다.
4. 다중 destination은 각 slice가 exact destination·drop position·owner operation·quantity commitment를 독립적으로 보유할 때만 허용한다.
5. 추가 후보는 detour, 목적지, reservation, spoilage, urgency와 load class가 compatible해야 한다.
6. multi-destination tour의 총 예상 이동 거리가 개별 운반 합계보다 작고 primary urgent delivery를 늦추지 않아야 한다.
7. pickup 순서는 현재 위치에서 가까운 후보만 보는 greedy 단독 규칙이 아니라 `pickup 증가 거리 + delivery group 증가 거리`를 함께 평가한다.
8. delivery 순서는 destination priority, spoilage/urgency, 현재 위치에서의 증가 거리, stable destination ID 순으로 결정론적으로 정한다.
9. 처음 계획한 destination이 invalid가 되면 다른 목적지에 묵시 입고하지 않는다. 해당 slice만 typed blocked/replan하고 나머지 합법 leg는 계속할 수 있다.
10. Ordinary는 preferred maximum 14kg를 넘겼으면 추가 경유 효율이 매우 높더라도 기본적으로 출발한다.
11. 단, 이미 선택한 한 stack의 합법 수량이 14~19kg이면 다시 쪼개는 것보다 한 번에 무감속 운반할 수 있다.
12. Heavy는 heavy-tagged recipe/order 또는 unit semantic만 선택할 수 있다.
13. OversizeEquipment는 다른 일반 stack을 opportunistic하게 덧싣지 않는다.
14. hard maximum은 `GetMaxAllowedWeight`를 사용하되 Ordinary 목표 계산에는 사용하지 않는다.
15. 목적지 용량보다 많은 수량을 pickup하지 않는다. 주변 초과 drop을 정상 성공 경로로 도입하지 않는다.
16. 목적지 입고가 일부만 가능해졌다면 남은 carried slice를 typed blocked 상태로 보존하고 재계획·취소 계약을 따른다.

이 정책으로 `capacity를 올릴수록 항상 더 무겁게 싣고 느려지는 역설`을 방지한다.

### 3.6 DungeonStory용 `PickAndHaulCompatibleTour` 계약

이 절은 필수 migration이 아니라 후속 최적화 계약이다. 첫 production 구현은 현재의 같은 exact destination tour를 유지한다. 그 회귀와 새 gram admission이 통과한 뒤에만 `Pick Up And Haul`의 효율을 가져오되 DungeonStory의 저장·복원·FacilityBuffer 권위를 보존하도록 plan을 다음처럼 확장한다.

```csharp
public sealed class WorldItemHaulTour
{
    public string OperationId;
    public IReadOnlyList<WorldItemPickupLeg> PickupLegs;
    public IReadOnlyList<WorldItemDeliveryGroup> DeliveryGroups;
    public PhysicalMassGrams PlannedMass;
    public HaulLoadClass LoadClass;
}

public sealed class WorldItemDeliveryGroup
{
    public WorldItemHaulDestinationKind DestinationKind;
    public string DestinationId;
    public Vector2Int DropPosition;
    public IReadOnlyList<WorldItemReservedStackQuantity> Slices;
}
```

필수 불변식:

- pickup slice 하나는 delivery group 정확히 하나에 속한다.
- 모든 slice의 합계 질량은 actor hard capacity 이하이고 Ordinary tour는 soft target을 따른다.
- reservation은 source quantity와 destination capacity를 같은 operation에서 원자적으로 잡는다.
- carry inventory에는 `ownerOperationId`, source stack, item ID, quantity, destination group이 유지된다.
- 개인 소지품·장착 장비·사용 예약 물품은 haul cargo로 계산하거나 unload하지 않는다.
- save에는 pickup 완료 여부, 현재 leg, 남은 delivery group, exact carried slice를 저장한다.
- restore는 destination claim·lease·physical carried record·inventory를 모두 대조한 뒤에만 resume한다.
- cancel은 미픽업 lease만 source reservation authority에서 해제한다. 이미 pickup된 carried slice를 source storage로 순간이동시키지 않는다.
- actor가 계속 활동 가능한 plan 취소·destination invalid·capacity shrink·no-path에서는 carried slice를 inventory에 유지한 채 합법 destination 재계획 또는 물리적 복귀 운반을 수행한다.
- actor downed·dead·world-present disabled처럼 운반 능력을 물리적으로 잃은 경우에는 carried slice를 마지막 authoritative actor cell에 exact quantity로 drop하고 transient recovery metadata를 붙인다.
- destination invalid, capacity shrink, actor downed, no-path가 발생해도 수량 삭제·복제·원격 source 복귀·무권위 영구 floor drop은 0이다.

도입 순서:

1. 기존 same-destination planner를 `PickAndHaulSameDestination` 기준선으로 명명하고 동작을 고정한다.
2. haul cargo와 개인 inventory 분리 marker를 추가한다.
3. destination별 capacity reservation과 다중 delivery-group DTO를 추가한다.
4. 최대 2 destination group으로 focused PlayMode를 통과시킨다.
5. 256-seed 물류 측정에서 개선이 확인될 때만 최대 3 group으로 확장한다.
6. `6~11kg recipe input`, `8~14kg ordinary tour`, `15~20kg heavy tour` 분포와 Wait WU를 재측정한다.
7. 같은 수량을 기존 planner와 새 tour로 운반해 거리, 작업시간, 예약 충돌, Floor Clutter, 저장 보존성을 paired 비교한다.

승격 조건:

- 운반 완료당 평균 이동 거리가 기준선보다 감소한다.
- 일상 tour p50은 8~14kg 안에 있고 p95가 hard capacity를 상시 사용하지 않는다.
- 긴급 약품·식사·수술 재료의 p95 배송 시간이 악화되지 않는다.
- destination capacity overcommit, duplicate pickup, personal inventory unload, orphan carried slice가 0이다.
- mid-tour save/load 반복 후 source+carried+delivered 수량이 exact하다.
- actor downed 직후의 typed transient drop은 허용하되 emergency/general recovery SLA 이후 통로 Floor Clutter가 0이다.

### 3.7 운반 중단의 논리적 lease와 물리적 화물 분리

`reservation rollback`과 `physical cargo disposition`은 같은 연산이 아니다. 다음 상태표를 권위로 사용한다.

| 중단 경계 | 미픽업 source lease | 이미 pickup된 carried slice | 허용 결과 |
|---|---|---|---|
| 계획 교체·낮은 우선순위 취소 | 즉시 해제 | inventory 유지 | 새 합법 delivery 또는 실제 source까지 복귀 운반 |
| 목적지 파괴·용량 축소 | 미픽업분 해제 | inventory 유지 | destination 재계획; 불가능하면 bounded blocked 상태 |
| 일시 no-path | heartbeat 또는 bounded 연장 | inventory 유지 | retry/replan; timeout 후에도 teleport 금지 |
| actor Downed | 즉시 해제 | 마지막 actor cell에 물리 drop | `TransientCarryRecoveryDrop` |
| actor Dead/Destroyed | 즉시 해제 | 제거 전 마지막 actor cell에 물리 drop | drop 성공 전 actor physical removal 완료 금지 |
| component/GameObject disable while world-present | drop commit 뒤 해제 | 마지막 actor cell에 물리 drop | drop 실패 시 `RecoveryPending`과 carried ownership 유지 |
| Despawn/원정·다른 world로 이전 | 현 world lease 해제 | 공식 transfer/save authority로 인계 | 현 world 바닥 drop·source teleport 모두 금지 |
| 테스트 teardown | production 규칙과 분리 | canonical checkpoint restore | gameplay 증거로 승격 금지 |

물리 drop metadata:

```text
dropDisposition=TransientCarryRecoveryDrop
ownerOperationId
sourceStackId
itemId
quantity
dropCell
carrierPersistentId
interruptionKind
droppedAtGameTime
recoveryDeadlineGameTime
```

규칙:

- `AuthorizedLooseSource`는 원래부터 허용된 채집·생산 source cell을 뜻하므로, 런타임 사고 드롭을 영구 `AuthorizedLooseSource`로 위장하지 않는다.
- 사고 드롭은 별도 `TransientCarryRecoveryDrop`으로 분류하며 grace 안에서만 허용된다.
- 일반 cell에서는 기존 `clutterGraceSeconds` 안에 실제 AIHaul이 회수해야 한다.
- EmergencyEgress·계단 착지·유일 접근칸이면 unavoidable transient obstruction으로 기록하고 일반 grace보다 짧은 emergency recovery SLA를 적용한다. 무기한 예외는 없다.
- drop cell은 actor의 마지막 authoritative grid cell이다. 멀리 있는 source·warehouse·목적지로 이동시키지 않는다.
- exact cell에 물리 spawn할 수 없으면 인접 임의 cell이나 source로 조용히 대체하지 않는다. carried record와 operation ownership을 `RecoveryPending`으로 보존하고 retryable typed failure를 발행한다.
- drop command는 typed commit result를 반환한다. 성공 전에는 `AbilityHaul`이 reservation·intent·active plan을 해제하거나 lifecycle physical owner를 제거할 수 없다.
- world-present disable은 coroutine 생존에 의존하지 않는다. world item recovery coordinator가 pending operation을 소유하고, exact drop 또는 retained-replan publication 성공 뒤에만 ownership을 종료한다.
- drop 성공 뒤 physical world stack 생성, carry inventory 제거, operation lease/intent 해제가 하나의 보존 경계로 완료되어야 한다.
- 회수 전 수량은 `source + stored + carried + transientDrop + delivered` 합계에 포함한다.

---

## 4. 구조 권위 계약

### 4.1 콘텐츠 정의

| 데이터 | 단일 권위 |
|---|---|
| 일반 물리 아이템의 runtime 포장 단위 kg | `ItemDefinitionSO.unitWeight`; 아래 authoring origin에서 생성·승인된 runtime projection이며 별도 수동 writer가 아님 |
| 아이템 1개의 의미·부피·포장·explicit/volume/recipe/packed 질량 origin | 기존 item definition stable ID에 연결되는 immutable `ItemMassProfileCatalogSO` 한 개 |
| 재료 밀도·가공 수율 | immutable `MaterialMassProfileCatalogSO` 한 개; material별 SO를 대량 생성하지 않음 |
| 레시피 질량 손실/부산물 | `ProductionRecipeSO` stable ID에 연결되는 immutable `RecipeMassBalanceProfileCatalogSO` 한 개 |
| 전투 장비 기본 형상 kg | `CombatEquipmentDefinitionSO.weight` |
| 전투 장비 실제 인스턴스 kg | 기본 형상 kg × material multiplier × 허용된 물리 질량 modifier |
| 의복 기본 형상 kg | `ApparelDefinitionSO.baseWeight` |
| 의복 실제 인스턴스 kg | 기본 형상 kg × textile material multiplier |
| 시설 키트 포장 kg | 설치 키트 mass profile의 packed transport kg |
| 캐릭터 운반 기준 | `CharacterCarryInventory`가 읽는 공용 carry tuning authority |
| 창고 authored kg capacity | `BuildingStorageAbility.maxStoredMassGrams` positive `long` 한 개; kg는 Inspector/UI projection 전용 |
| 창고 runtime 사용량 | physical Stored stack gram index + active destination gram lease의 derived query |
| 야생동물 사체 kg | species `CarcassWeight`와 species-specific physical carcass item의 exact builder projection |

`CSV`, 감사 리포트, 캐시, UI text, save DTO는 새로운 쓰기 권위가 아니다.

`massDerivationKind`는 item마다 authoring origin을 정확히 하나 선택한다.

- explicit primitive, volume-density, recipe-balanced, packed-facility item은 mass profile catalog가 authoring origin이고 builder가 `ItemDefinitionSO.unitWeight`를 projection한다.
- combat equipment는 equipment shape/material authority, apparel은 apparel/textile authority, carcass는 species authority가 origin이다. 각 builder가 default physical item `unitWeight`를 projection한다.
- `ApplyApproved`는 origin property 하나만 수정한다. origin과 projected `unitWeight`를 같은 적용에서 독립적으로 두 번 patch하지 않는다.
- runtime은 항상 `ItemDefinitionSO.unitWeight` 또는 exact instance projector를 읽는다. catalog의 계산 후보를 두 번째 gameplay mass로 직접 읽지 않는다.
- builder 재생성 뒤 origin과 projection이 1g까지 exact하지 않으면 `MASS_AUTHORING_PROJECTION_DRIFT`로 실패한다.
- 현재 `PhysicalMassAuthoringContracts`의 코드-local audit 배열은 inventory/후보 증거일 뿐 live content authoring 권위가 아니다. Gate S0에서 위 세 catalog로 이관하고, 해당 배열을 runtime fallback이나 두 번째 writer로 남기지 않는다.

### 4.2 런타임 물리 질량 조회

generic definition만 읽는 현재 경로를 다음 단일 Query로 모은다.

```csharp
public interface IPhysicalItemMassQuery
{
    long AuthorityRevision { get; }

    PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId);

    PhysicalMassGrams GetStackUnitMass(
        ItemDefinitionId itemId,
        PhysicalItemMassSubject subject);

    PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot);

    PhysicalMassGrams GetQuantityMass(
        ItemDefinitionId itemId,
        PhysicalItemMassSubject subject,
        int quantity);
}
```

runtime immutable subject는 다음 closed discriminator를 사용한다.

```csharp
public enum PhysicalItemMassSubjectKind
{
    GenericDefinition,
    CombatEquipment,
    Apparel,
    WildlifeCarcass,
    PackagedLot
}

public readonly struct PhysicalItemComponentSnapshot
{
    public string ComponentTypeId { get; }
    public int SchemaVersion { get; }
    public string CanonicalPayload { get; }
    public string Fingerprint { get; }
}
```

- `PhysicalItemMassSubject`는 `ItemDefinitionId`, optional instance ID, subject kind, immutable component snapshots와 전체 component fingerprint만 가진다.
- `PhysicalItemLotSnapshot`은 subject, positive quantity와 diagnostic stack/lot ID만 가진다. world state·destination은 질량 입력이 아니다.
- mutable `ItemInstanceComponentSaveData`를 Query에 직접 전달하지 않는다. capture/restore adapter가 strict decode와 canonical clone 뒤 runtime snapshot을 만든다.
- projector registry는 subject kind/component type마다 정확히 하나의 integration projector를 요구한다. 0개 또는 2개 이상이면 fallback 없이 실패한다.
- Items assembly의 subject는 Combat/Environment concrete type을 참조하지 않는다.

`PhysicalItemMassSubject`와 `PhysicalItemLotSnapshot`은 runtime immutable value다. save DTO는 gameplay Query 입력이 아니며, restore adapter가 strict validation 뒤 DTO를 이 runtime 값으로 변환한다.

소비자는 다음 규칙을 따른다.

- 일반 stackable item: definition unit mass
- unique combat equipment: equipment component의 definition/material/evolution에서 파생한 instance mass
- unique apparel: apparel component의 definition/material에서 파생한 instance mass
- wildlife carcass: species ID에서 species `CarcassWeight`를 resolve하며 physical carcass definition과 exact 일치 검증
- reusable package/content lot: package tare + 현재 content mass; 소비 뒤 empty-container byproduct와 exact 연결
- component가 있는데 decode 실패: generic fallback 금지, typed failure
- component가 없는 일반 item: definition mass
- quantity가 0 이하, 질량이 0 이하, overflow: fail-loud
- combat equipment runtime aggregate와 physical component mirror가 동시에 존재하면 exact gram 일치를 요구하며 불일치 시 한쪽을 조용히 선택하지 않음
- 질량을 바꾸는 상태는 material·evolution·attached module·loaded ammunition으로 한정한다. durability·charge·freshness·quality·오염·젖음은 V27에서 질량 불변이다.

다음 경로가 모두 같은 Query를 사용해야 한다.

- `WorldItemQueryService`
- `PhysicalStockQuery`
- `WorldItemHaulPlanningService`
- `CharacterCarryInventory`
- `CharacterCarryPresentation`
- `ItemPileInfoPanel`
- warehouse mass admission·reservation·utilization·UI
- conveyor·restock·production output의 exact-item commit
- reservation·pickup·deposit 전후 보존 감사
- V27 mass/handling EWU capture

### 4.3 고정소수점 질량

Authoring asset의 기존 item `unitWeight` float 필드는 Unity YAML 호환을 위해 유지하되 Capture 경계에서 즉시 정수 gram으로 변환한다. 신규 warehouse capacity와 runtime 질량 상태에는 float kg writer를 만들지 않는다.

```csharp
public readonly struct PhysicalMassGrams
{
    public long Value { get; }
}
```

규칙:

- 내부 질량 계산 단위: `1g`
- 일반 authored 표시 단위: `0.01kg`
- 10g 미만 item도 내부에서는 1g까지 보존
- float 누적 합산 금지
- canonical capture 시 `kg × 1000`이 exact gram이 아니면 `NON_CANONICAL_ITEM_MASS`
- UI만 kg 소수 둘째 자리로 반올림
- 질량 회계·소유권 감사에는 UI 반올림을 사용하지 않음
- EWU의 input Ceil/output Floor 규칙을 kg 자체에 오용하지 않음
- 질량 residual을 허용 오차로 버리지 않고 explicit loss 또는 byproduct로 귀속
- unit/lot/capacity는 positive grams만 생성한다. remaining/reserved 합계의 0은 별도 `long` query 결과 또는 명시적 `Zero` capable nonnegative aggregate type으로 표현한다.
- signed lease delta를 `PhysicalMassGrams`로 위장하지 않는다. 별도 checked signed delta/result를 사용한다.
- 수량 곱셈, 합산, 밀도 계산과 capacity 차감은 모두 `checked`이며 overflow는 typed failure다.

### 4.4 런타임 상태와 저장

- 일반 item mass는 immutable definition에서 재계산하며 save에 복제하지 않는다.
- unique 장비의 material/evolution/module identity는 기존 item component/save authority를 사용한다.
- apparel material/modification과 carcass species identity도 기존 component/item ID authority를 사용한다.
- save restore 후 `IPhysicalItemMassQuery` 결과가 저장 전과 exact gram 일치해야 한다.
- 캐시에는 authority revision을 붙이고 저장하지 않는다.
- 질량 영향 mutation은 runtime aggregate, physical component mirror, derived warehouse/carry index와 `AuthorityRevision`을 하나의 publication 경계에서 갱신한다. 중간 실패 시 일부만 보이지 않아야 한다.
- mass-affecting mutation은 revision exact 1회, mass-invariant/no-op mutation은 0회를 요구한다.
- save capture는 runtime aggregate와 physical component의 gram 불일치를 복원용 fallback으로 덮지 않고 fail-loud한다.
- 과거 save 전용 mass migration 필드나 legacy fallback을 만들지 않는다.
- 신규 save가 알 수 없는 material/equipment/apparel ID를 가지면 복원 전체를 fail-loud한다.

current-format schema와 복원 순서는 구현 전에 다음처럼 고정한다. 번호는 현재 source 기준의 다음 버전이며 실제 구현 직전 source inventory가 달라지면 `SOURCE_INVENTORY_CHANGED`로 중단하고 이 표부터 갱신한다.

| 저장 권위 | 현재 | 다음 current-format | V27 소유 필드 |
|---|---:|---:|---|
| root `DungeonGameSaveData` | V24 | V25 | section version/digest와 원자 restore transaction 경계 |
| physical items | V9 | V9 유지 | 단순 gram cache는 저장하지 않는다. disposition/commit 필드를 실제 추가할 때만 V10으로 올린다. |
| production bills/WIP | V7 | V8 | consumed slices, input dispositions, `cycleSequence`, complete realized output vector, output commit state |
| warehouse inventory state | V3 | V4 | mutable category/policy state만 저장한다. immutable authored `maxStoredMassGrams`를 snapshot에 복제하지 않는다. |

- 과거 버전은 변환하지 않고 typed incompatible-version failure로 전체 restore를 거절한다.
- 복원 순서는 `facility/definition candidate -> physical items -> production/WIP candidate -> warehouse destination gram lease rebuild -> participant publication -> AI wake`다.
- WIP와 destination lease를 검증·재구축하는 participant는 AI activation보다 먼저 publish되고, 이후 participant 실패 시 physical/WIP/lease가 함께 rollback되어야 한다.
- root/section version bump는 필드 추가가 실제로 일어난 같은 change set에서만 수행한다. 문서 단계에서 코드를 선반영하지 않는다.

### 4.5 의존 방향

```text
DungeonStory.Foundation: ItemDefinitionId
-> DungeonStory.Items: PhysicalMassGrams, mass subject/lot, IPhysicalItemMassQuery
-> DungeonStory.Economy/Combat/Environment integration projectors
-> Items/Haul/Carry/Warehouse/EWU/UI consumers

Editor audit/apply
-> immutable definitions
```

`ItemDefinitionId`는 `DungeonStory.Foundation`으로 이동하고 `PhysicalMassGrams`는 `DungeonStory.Items`으로 이동한다. 별도 `MassGrams`를 만들지 않는다. Economy는 기존처럼 Items를 참조하고 Items는 Economy를 참조하지 않는다. 타입 이동 슬라이스는 기존 생성 의미를 먼저 보존하고, 다음 canonical-ID 슬라이스에서 whitespace 자동 Trim을 제거한 strict factory와 validator로 전환한다.

- `ItemDefinitionId`의 namespace와 public type identity는 첫 assembly 이동에서 유지하고 `MovedFrom(sourceAssembly: "DungeonStory.Economy")`/API 호환을 검증한다.
- `PhysicalMassGrams`도 namespace/public identity를 유지하고 Economy 원본 파일에서 Items 전용 파일로 이동하며 `MovedFrom(sourceAssembly: "DungeonStory.Economy")`와 serialized test fixture를 검증한다.
- assembly 이동과 canonical 생성 의미 변경을 같은 commit에서 수행하지 않는다.
- 현재 `ItemDefinitionSO.cs`에 함께 있는 ID 타입은 Foundation 전용 파일로 먼저 분리한다. 원본 Economy 파일에 forwarding duplicate type을 남기지 않으며 compile-time type ambiguity 0과 Unity serialized-reference round-trip을 확인한다.

Items 코어가 Combat 또는 Environment 구체 runtime을 역참조해 assembly cycle을 만들지 않는다. instance component를 mass projection하는 adapter는 양쪽 definition catalog를 볼 수 있는 통합 계층에 두고 Items에는 `IPhysicalItemMassQuery`만 주입한다.

---

## 5. 아이템 1개 단위 의미 계약

모든 canonical item은 다음 필드를 가진다.

```text
itemId
unitSemanticKind
unitLabel
unitDescription
nominalVolumeMilliLiters
packageTareGrams
packageTareDisposition
packageContainerItemId(optional)
primaryMaterialId
massDerivationKind
canonicalUnitMassGrams
haulClass
massBalanceSourceId
```

`1개` 의미가 없으면 kg를 계산하지 않고 `MISSING_ITEM_UNIT_SEMANTIC`으로 실패한다.

### 5.1 단위 의미 분류

| 분류 | 1개의 의미 | 기준 kg 밴드 | 산정 권위 |
|---|---|---:|---|
| 물·액체 | 밀폐 용기 1회분 또는 1L 단위 | 0.25~1.25 | 부피 × 밀도 + 용기 |
| 종자 | 파종 가능한 1포 또는 정해진 종자 묶음 | 0.02~0.25 | 종자 수·포장 |
| 곡물·채소·버섯 | 저장 가능한 1바구니/다발 | 0.25~1.50 | 명시적 부피 × bulk density |
| 고기·동물 생산물 | 1절단·1포장·1병 | 0.25~2.00 | 절단 수율·부피·용기 |
| 완성 식사 | 1인 1회 섭취 portion | 0.35~1.20 | 음식 재료 + 물 - 조리 손실 |
| 약품 | 1회 치료 dose/kit | 0.02~0.50 | 유효 성분 + 용기 |
| 탄약 | 1발, 1탄창, 1묶음 중 하나를 item별 명시 | 0.01~0.50 | 탄체·화약·포장 BOM |
| 섬유·가죽 | 제작에 쓰는 1 roll/sheet | 0.15~2.50 | 면적·두께·밀도 |
| 목재 원료 | 통나무 1토막 또는 운반 가능한 원목 1개 | 3.00~8.00 | 규격 부피 × 목재 밀도 |
| 처리 목재 | 판재 bundle 1개 | 1.50~6.00 | 원목 입력 - 톱밥/수분 |
| 광석 | 광석 chunk/basket 1개 | 2.00~6.00 | 부피 × bulk density |
| 석재·벽돌 | block 1개 | 2.00~8.00 | 규격 부피 × 밀도 |
| 잉곳 | 규격 ingot 1개 | 0.50~3.00 | 금속 BOM + 회수 가능한 슬래그 + typed 제련 손실 |
| 소형 부품 | fastener, gear, fitting의 1봉/1개 | 0.05~1.50 | BOM - 절삭 손실 |
| 대형 부품 | frame, mechanism, power unit | 2.00~10.00 | BOM - 공정 손실 |
| 폐기물 | bag, slag chunk, scrap bundle | 0.10~8.00 | 실제 부산물 질량 |
| 의복 | 착용 가능한 완제품 1벌/1부위 | 0.05~8.00 | base shape × textile material |
| 무기 | 완제품 1개 | 0.30~12.00 | base shape × craft material + components |
| 방패 | 완제품 1개 | 1.50~15.00 | base shape × craft material |
| 방어구 | 한 장비 슬롯 완제품 1개 | 0.20~20.00 | base shape × craft material |
| 정말 무거운 장비 | unique 장비 1개 | 20.00~36.00 | 명시적 OversizeEquipment |
| 시설 설치 키트 | 설치에 필요한 compact packed subassembly 1상자 | 8.00~14.00, 최대 19 | packed transport profile |
| 설계도·기록 | 실제 책·두루마리·도면 묶음 1개 | 0.05~0.50 | 매체 + 포장 |
| 촉매·유물 | 봉인 용기/유물 1개 | 0.05~5.00 | 명시적 물체 질량 |

이 표의 밴드는 자동 대입값이 아니다. item별 unit meaning을 정한 뒤 실제 BOM/부피/재료에서 exact gram을 계산하고, 밴드 밖이면 이유를 기록한다.

### 5.2 단위 의미 작성 규칙

- `water:clean=1`이 1L인지 물병 1개인지 명시한다.
- `food:meal=1`은 “한 끼”이며 nutrition 값과 mass가 함께 검증되어야 한다.
- `material:lumber=1`은 판자 한 장인지 판재 묶음인지 명시한다.
- `ammo:* =1`은 1발인지 1묶음인지 item별로 통일한다.
- `medicine:* =1`은 1회 dose인지 treatment kit인지 명시한다.
- `facility-kit:* =1`은 시설 전체 질량이 아니라 설치용 packed subassembly라는 점과 packing ratio를 명시한다.
- 동일 item이 레시피마다 다른 단위로 해석되면 실패한다.
- display name만 보고 kg를 추측하지 않는다.
- maxStack은 unit semantic과 물리적으로 양립해야 한다.

### 5.3 포장 용기 질량의 lifecycle

`packageTareGrams > 0`은 내용물과 별개의 물리 자본이 존재한다는 뜻이다. 내용물을 소비했다고 tare까지 이유 없이 Sink 처리하지 않는다.

```csharp
public enum PackageTareDisposition
{
    None,
    ReusableContainerReturn,
    DisposableWasteByproduct,
    DestroyedDuringUse,
    TransferredWithOutput,
    BulkInfrastructureNotInUnit
}
```

| disposition | 계약 |
|---|---|
| `None` | `packageTareGrams == 0`일 때만 합법 |
| `ReusableContainerReturn` | 소비 1회당 exact empty-container item을 물리 inventory/world에 반환 |
| `DisposableWasteByproduct` | wrapper·vial·used kit 같은 물리 waste item을 exact 수량 반환 |
| `DestroyedDuringUse` | 소각·파손·오염폐기 같은 허용 loss kind와 exact grams 필수 |
| `TransferredWithOutput` | 다른 output item의 tare로 계속 보존되며 이중계상 금지 |
| `BulkInfrastructureNotInUnit` | unit mass에 tare를 더하지 않고 탱크·배관·시설 질량 권위가 별도 소유 |

예시:

- `water:bottle-full = water 1000g + bottle 200g`이면 음용 후 `container:bottle-empty 200g ×1`을 반환한다.
- 재사용 병을 게임 아이템으로 추적하지 않을 설계라면 `water:clean`의 단위 의미를 “시설/탱크가 소유한 1L bulk water”로 정의하고 `packageTareGrams=0`으로 둔다.
- 1회용 약품 vial은 `used-vial` 물리 waste를 만들거나 `ContaminationDisposal`로 exact  tare loss를 선언한다.
- 치료 kit의 천·금속 도구가 재사용된다면 전부 Sink하지 않고 빈 kit/used tool 부산물로 반환한다.
- 반환된 빈 용기는 재충전 recipe, 세척 WU, 저장 공간, 운반 kg, EWU를 가진다. 무료로 자동 재충전하지 않는다.

### 5.4 maxStack와 pile 질량

각 item에 대해 다음을 계산한다.

```text
maxStackMassKg = unitMassKg × maxStack
```

검증:

- maxStack은 UI 묶음 크기이지 한 캐릭터가 전부 들어야 한다는 뜻이 아니다.
- quantity lease와 partial pickup이 실제로 한도에 맞게 분할해야 한다.
- 한 stack이 매우 무거워도 pickup 가능한 partial quantity가 최소 1이어야 한다.
- `unitMass > harnessMax`인 generic item은 별도 운송 경로가 없으면 Critical이다.
- stack marker, warehouse, buffer는 stack mass가 커져도 int/float overflow가 없어야 한다.

### 5.5 창고 kg capacity 단일 권위

창고는 별도 부피(L) 데이터를 만들지 않고 canonical item mass를 저장 capacity 권위로 사용한다. 기존 item-count `maxCapacity`를 같은 이름으로 재해석하지 않고 명시적 gram 필드로 교체한다.

```csharp
public interface IWarehouseMassCapacityQuery
{
    long StoredMassGrams { get; }
    long ReservedInboundMassGrams { get; }
    long MaxMassGrams { get; }
    long RemainingMassGrams { get; }
}
```

production admission은 read query가 아니라 다음 opaque registry-backed token command를 통과한다.

```csharp
public readonly struct WarehouseMassAdmissionRequest
{
    public BuildingInstanceId WarehouseId { get; }
    public string OwnerOperationId { get; }
    public ItemDefinitionId ItemId { get; }
    public string ItemInstanceId { get; }
    public string LotFingerprint { get; }
    public int RequestedQuantity { get; }
    public long ExpectedWarehouseCapacityRevision { get; }
    public long ExpectedCatalogRevision { get; }
    public IReadOnlyList<PhysicalItemLotRevision> ExpectedSourceLotRevisions { get; }
}

public readonly struct WarehouseMassAdmissionToken
{
    public string TokenId { get; }
    public BuildingInstanceId WarehouseId { get; }
    public string OwnerOperationId { get; }
    public string LotFingerprint { get; }
    public int AcceptedQuantity { get; }
    public long ReservedMassGrams { get; }
    public long CatalogRevision { get; }
    public long WarehouseRevision { get; }
    public double ExpiresAtGameSeconds { get; }
}

public readonly struct WarehouseMassAdmissionReceipt
{
    public string TokenId { get; }
    public string CommitId { get; }
    public BuildingInstanceId WarehouseId { get; }
    public string OwnerOperationId { get; }
    public string LotFingerprint { get; }
    public int CommittedQuantity { get; }
    public long CommittedMassGrams { get; }
    public long ResultWarehouseRevision { get; }
}

public enum WarehouseMassAdmissionReleaseReason
{
    CancelledBeforePickup,
    PickupFailed,
    DestinationInvalidated,
    LeaseExpired,
    RestoreRollback,
    TransactionRollback
}

public interface IWarehouseMassAdmissionService
{
    bool TryReserve(
        WarehouseMassAdmissionRequest request,
        out WarehouseMassAdmissionToken token,
        out DomainFailure failure);

    bool TryRenew(
        string tokenId,
        long expectedWarehouseRevision,
        out WarehouseMassAdmissionToken renewed,
        out DomainFailure failure);

    bool TryCommit(
        string tokenId,
        string commitId,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure);

    bool TryRelease(
        string tokenId,
        WarehouseMassAdmissionReleaseReason reason,
        out DomainFailure failure);
}
```

- token struct는 진단 snapshot일 뿐 권위가 아니다. commit/release는 singleton registry의 `TokenId`와 exact immutable request를 다시 대조한다.
- token은 exact item/instance/component lot fingerprint, accepted quantity와 grams를 고정하며 다른 lot에 재사용할 수 없다.
- 만료 시간은 기존 item quantity lease와 동일한 deterministic game-clock seconds 권위를 사용한다. wall clock, frame count, 임의 tick 단위를 새로 만들지 않는다.
- production warehouse는 모두 positive limit이므로 production query에 unlimited boolean/sentinel을 두지 않는다. unlimited fixture는 별도 Editor-only implementation/API다.
- 동일 `commitId` 재호출은 같은 receipt를 반환하고 두 번째 physical mutation은 0이다.
- unrelated world/warehouse mutation은 active token을 무효화하지 않는다. token correctness는 2.17의 warehouse-local revision, reserved grams와 exact source lot revisions가 소유한다.
- `TryRenew`의 `expectedWarehouseRevision`은 registry가 발급한 token의 warehouse-local revision이다. global repository version을 전달하지 않는다.

원자 순서:

```text
1. source quantity lease/픽업 완료 ownership revalidate
2. destination gram token reserve 또는 기존 token revalidate
3. exact merge/spawn slot과 category/owner/position preflight
4. physical repository transaction에서 source/carried/transit -> Stored 전환
5. quantity lease와 gram token을 동일 commit ID로 완료
6. repository/warehouse revision과 event/UI publication
```

- 단계 4 이전 실패는 physical mutation 0이고 새 gram token을 해제한다.
- 단계 4~5 fault injection은 repository transaction과 두 lease를 역순 rollback한다. 부분 publication은 금지한다.
- Unity main thread라도 callback/reentrant admission을 막는 transaction guard가 필요하다.
- 다중 destination lock은 `(warehousePersistentId ordinal, ownerOperationId ordinal)` 순으로만 획득한다.

```text
stackMassGrams
= generic quantity × canonical unitMassGrams
or unique instance mass projector result

warehouseStoredMassGrams
= Σ all physical Stored stacks whose storageDestinationId is this warehouse

remainingMassGrams
= max(0, maxMassGrams - storedMassGrams - reservedInboundMassGrams)
```

작성 권위:

- `BuildingStorageAbility`는 positive serialized `long maxStoredMassGrams`를 단일 권위로 가진다. 기존 `capacity` 숫자를 kg나 gram으로 재해석하지 않는다.
- kg는 Inspector와 UI에서만 `grams / 1000m`으로 표시한다. runtime admission과 save 검증에 float kg를 쓰지 않는다.
- production warehouse의 `maxStoredMassGrams`는 반드시 양수다. `0=무제한` sentinel을 금지하고, 무제한 저장은 별도 debug-only fixture type으로만 허용한다.
- 기존 `maxCapacity` item-count 필드는 신규 작성 금지 후 제거한다. 같은 숫자를 kg로 재해석하지 않는다.
- `BuildingInternalStockAbility.capacity`는 shop/service 내부 unit-count 권위이므로 warehouse kg로 재해석하지 않는다.
- 현재 L01처럼 logistics role이라는 이유만으로 internal-stock capacity를 warehouse capacity로 승격하는 fallback은 제거한다. 실제 warehouse에는 명시적 reviewed mass ability가 필요하다.
- `RoomFacilityPolicyAdapter.GetStorageCapacity(category)`가 shop count에 더해지는 경로도 제거한다. room warehouse mass는 gram query로, retail 진열 슬롯은 별도 count authority로 노출한다.
- 과거 세이브 변환은 하지 않는다. 현재 V27 asset의 창고별 kg capacity는 population-stage 비축·처리량·공간 검증으로 새로 배정한다.
- `TotalStock`과 category별 quantity query는 UI·주문·재고 조회용으로 유지하지만 admission capacity 권위가 아니다.

입고 계약:

```text
acceptedQuantity
= min(
    requestedQuantity,
    floor(RemainingMassGrams / unitMassGrams))
```

- generic stack은 위 공식으로 partial deposit한다.
- unique equipment/apparel은 실제 instance mass를 사용하며 전량이 들어갈 때만 입고한다.
- wildlife carcass와 package-bearing unique lot도 실제 projected mass 전량이 들어갈 때만 입고한다.
- `unitMassGrams > RemainingMassGrams`이면 수량·lease·destination을 변경하지 않고 `WarehouseMassCapacityUnavailable`로 fail-loud한다.
- 같은 operation 재시도는 이미 입고된 quantity를 다시 더하지 않는다.
- source split, carried slice, warehouse Stored 전환과 mass capacity 차감은 같은 transaction에서 exact다.
- carry를 제거하거나 source quantity lease를 완료하기 전에 destination gram lease와 physical spawn 가능성을 preflight한다.
- 부분 거부분은 carry/owner intent에 남겨 다른 창고 또는 합법 overflow로 재계획한다. 창고 포화 자체는 Loose floor drop 사유가 아니다.
- AI haul과 conveyor는 같은 destination gram ledger에서 경쟁하며 commit 직전 capacity revision을 다시 검증한다.
- `SpawnItemAt`, unique spawn, route, transit complete 같은 low-level mutation이 warehouse destination을 받으면 valid capacity commit token 없이는 거부한다. save restore와 Editor fixture는 별도 명시 API다.
- FacilityBuffer와 공정 output buffer는 각자 별도 capacity 계약이 없으면 창고 kg capacity를 빌려 쓰지 않는다.

UI·공간 계약:

- UI는 `현재 저장 kg / 최대 kg`, 남은 kg와 가장 무거운 거부 item을 표시한다.
- item 개수와 stack 수는 보조 진단으로 함께 표시하되 capacity처럼 표현하지 않는다.
- kg capacity는 현실 부피가 아니라 저장 추상이다. 바닥 점유와 통로 혼잡은 실제 physical stack/pile 수와 SpatialCellRole로 계속 계산한다.
- 창고 footprint를 키우면 kg capacity가 늘어나는 authored scaling은 둘 수 있지만, `1 cell = N kg` 값은 population-stage·headroom 검증으로 승인한다.
- `7일 비축 kg`, 실제 stack/pile 수, storage utilization, overflow containment를 모두 출력한다.

저장·복원:

- 창고 capacity는 불변 definition에서 재계산하고 save DTO에 중복 권위로 저장하지 않는다.
- 물리 stack/instance 복원 후 owner·category·좌표와 계산된 `StoredMassGrams`를 검증한다. `StoredMassGrams <= MaxMassGrams`는 restore precondition이 아니다.
- 현재 형식 save가 valid owner의 over-capacity 상태를 포함하면 아이템을 삭제하거나 순간이동하지 않는다. 기존 stock은 보존하고 신규 입고·inbound reservation을 차단하며 정리 haul/overflow 진단을 발행한다.
- no-op restore에서 quantity·mass·stack identity가 동일해야 한다.
- pre-pickup gram lease는 저장하지 않고 복원 후 재계획한다. pickup-committed carry intent와 conveyor transit는 physical save authority에서 destination gram lease를 AI wake 전에 결정론적으로 재구축한다.
- 동일 operation의 persisted commitment와 derived lease를 동시에 합산하지 않는다.
- physical stack의 `warehouse-storage:` ID는 modular-facility restore candidate의 exact warehouse persistent ID·현재 center·category policy와 cross-section 검증한다. canonical text만으로 owner를 인정하지 않는다.
- owner missing/duplicate, warehouse kind mismatch, tampered destination position은 전체 restore를 원자적으로 거부한다.
- valid owner의 over-capacity는 stock 보존+신규 admission 차단이고, owner 없는 Stored destination은 복원 실패다. 두 경우를 같은 fallback으로 처리하지 않는다.
- pickup-committed haul intent/conveyor transit와 rebuilt inbound gram lease는 facility·physical item과 같은 restore transaction에서 rollback된다.

창고 lifecycle:

- demolition precondition은 `StoredMassGrams == 0`, `ReservedInboundMassGrams == 0`, 해당 destination carried intent 0, conveyor transit 0이다.
- relocation도 같은 empty/inbound-zero precondition을 우선 적용한다. 현재 physical Stored stack을 건물 state module만으로 옮기는 것은 금지한다.
- category filter 또는 capacity가 runtime에 변경되는 기능을 도입하면 기존 stock을 삭제하지 않고 신규 admission만 막으며 명시적 evacuation order를 만든다.
- authored base capacity와 category policy는 definition 권위다. save snapshot에는 immutable base를 복제하지 않고, 실제 mutable upgrade modifier가 존재할 때만 그 modifier와 revision을 저장한다.
- warehouse별 mass index는 physical repository에서 재구축 가능한 derived state이며 별도 quantity authority가 아니다.
- actor가 source에서 pickup한 뒤 source가 다시 차면 interrupted cargo를 source로 순간이동시키지 않는다. 활동 가능하면 cargo 유지+재계획, Downed/Dead면 현재 위치 recovery drop을 따른다.

밸런스 게이트:

- 작은 단위 item은 질량만큼만 capacity를 소모하므로 raw count 때문에 불리해지지 않는다.
- 고밀도 금속도 동일 kg만큼 capacity를 쓰므로 현실 부피 차이는 의도적으로 모델링하지 않는다.
- 창고 capacity를 늘려 Floor Clutter를 숨기지 않는다. 정상 70%, 장애 90%, containment 밖 clutter 0을 유지한다.
- kg capacity 전환 후 모든 warehouse definition, admission caller, UI, save validator, physical logistics verifier가 새 query를 사용해야 하며 구 count admission 호출자는 0이어야 한다.

### 5.6 비창고 저장·정확한 identity 폐쇄

창고만 kg로 바꾸고 다른 저장소가 physical item을 count/이벤트로 흡수하면 플레이어는 창고 제한을 우회할 수 있다. 다음을 전수 분류한다.

| 저장 경계 | 목표 계약 |
|---|---|
| shop internal stock | exact `RetailStockLot(itemId, instanceId, components, quantity, derivedMassGrams, sourceOperationId)` 또는 명시적 retail Transform. `derivedMassGrams`는 공용 query 결과를 검증·표시하는 파생 snapshot이며 독립 작성 권위가 아니다. |
| production output buffer | 최대 batch kg·체류시간·output reservation을 가진 operational buffer |
| FacilityBuffer | owner operation·destination·최대 routed kg·terminal release를 가진 transient buffer |
| character carry | 공용 instance mass query + soft/hard kg limit |
| equipped loadout | world/carry와 동일 instance kg, 단일 ownership |
| construction/work-order delivered ledger | WIP/incorporated mass ledger와 exact physical consume 기록 |
| corpse/organ transport | physical item이면 species/instance mass projector를 사용한다. 살아 있는 대상 운송과 공유하지 않는다. |
| living rescue/wildlife/captive transport | item stack이 아닌 `EntityTransportProfile`; subject identity·required carrier/equipment·path·single ownership·interruption·save/restore만 소유한다. item kg admission, hard cap, 속도 페널티는 적용하지 않는다. |
| apparel recovery locker | actor 위치의 숨은 global Stored ID가 아니라 owner·위치·kg limit·retrieval SLA가 있는 typed recovery buffer |
| expedition supplies | exact packed input lot·package tare·remaining amount·consumed Sink receipt·return lot을 가진 expedition transform |
| expedition loot | route-event Source ID·burden grams·carry cap·return `UnappraisedLoot` conversion receipt |
| money/research/reputation | 물리 item이 아닌 명시적 non-physical ledger. warehouse/carry kg를 사용하지 않으며 physical input을 흡수하면 Transform receipt 필수 |

필수 규칙:

- production API의 `SpawnStockInWarehouse(StockCategory, amount)`는 exact physical item 생산에 사용하지 않는다. live caller는 item ID를 전달한다.
- restock lease는 출발 item ID·instance/components를 보존한다. 취소·실패 반환은 같은 lot만 복원한다.
- `EquipmentStoredEvent`만 발행하고 physical stack을 없애는 경로는 허용된 abstract Sink가 아닌 한 제거한다.
- category는 필터/표시/정책 key이지 물리 item identity가 아니다.
- transient buffer가 terminal owner 없이 남거나 허용 체류시간을 넘으면 orphan mass Critical이다.
- `Stored` enum 값만으로 warehouse라고 판정하지 않는다. destination kind/owner가 exact해야 하며 모든 non-warehouse Stored ID를 manifest에 기록한다.
- kg capacity를 쓰지 않는 buffer는 count capacity가 아니라는 이유만으로 무제한이 되지 않는다. `maxBatchMassGrams`와 실제 peak mass를 원장에 기록한다.
- room warehouse mass와 shop display slot을 더하지 않는다. UI에도 `보관 kg`, `진열 lot/slot`, `판매 가능 수량`을 별도 차원으로 표시한다.
- kg는 gameplay admission 권위지만 technical physical-record 수는 별도 진단한다. compatible signature만 deterministic compact하고 freshness·contamination·provenance가 다른 record는 합치지 않는다.
- category requirement가 단지 교환 가능한 physical input을 선택하는 경우에는 category를 유지할 수 있다. 그러나 취소·반환·판매·저장·보상으로 concrete item을 다시 만들어야 하는 순간에는 exact lot 또는 명시적 Transform이 필수다.
- 모든 physical consume/remove는 closed disposition kind와 gram receipt를 남긴다. repository `Remove`는 typed transaction/transfer 내부 구현으로만 사용하고 production domain이 직접 질량을 삭제하지 않는다.
- spoilage·건조·부패·모듈/탄약 변화처럼 저장 중 질량/분류가 바뀌는 변환은 기존 destination ownership을 보존한다. 새 category가 policy와 맞지 않으면 기존 stock을 제거하지 않고 evacuation-required로 표시한다.
- in-place transform으로 StoredMass가 capacity를 넘으면 변환 결과를 보존하고 신규 admission을 막는다. capacity 확보를 이유로 output을 Loose로 방출하거나 input을 되돌리지 않는다.
- actor burden은 cargo/equipped/total 세 차원으로 보고한다. 멜빵은 payload capacity modifier이면서 자체 equipped mass를 가지며, 같은 질량을 cargo와 equipped 양쪽에 중복 계상하지 않는다.
- 부상·performance·장비 상태로 carry capacity가 내려가도 기존 cargo는 보존한다. 신규 pickup을 막고 실제 unload를 수행하며, 자동 source 반환이나 inventory 삭제를 하지 않는다.
- haul-owned cargo의 canonical gram이 바뀌면 destination lease도 exact delta만큼 원자 갱신한다. stale lease로 계속 deposit하지 않는다.
- living entity 운송은 generic item 29kg cap이나 total borne grams에 넣지 않는다. 전용 transport 상태로만 표시하고, 운송 중 opportunistic item pickup과 동시 entity carry를 금지한다.
- carcass mass와 living-body transport mass는 별도 의미다. 하나를 다른 하나의 암묵 fallback으로 사용하지 않는다.

---

## 6. BOM·밀도 기반 파생 질량

### 6.1 재료 질량 프로필

```csharp
public readonly struct MaterialMassProfile
{
    public string MaterialId { get; }
    public int DensityGramsPerLiter { get; }
    public int DefaultMoisturePermille { get; }
    public int PackingEfficiencyPermille { get; }
    public int DefaultProcessYieldPermille { get; }
}

public sealed class MaterialMassProfileCatalogSO : ScriptableObject
{
    // stable MaterialId로 정렬된 immutable authoring entries
    // 한 material ID당 정확히 한 entry
}
```

실제 SI density를 그대로 사용하는 것이 아니라 게임의 `1개` 부피와 포장 상태를 함께 사용한다.

```text
materialMassGrams
= CeilDiv(
    checked(nominalVolumeMl
        × densityGramsPerLiter
        × packingEfficiencyPermille),
    1,000,000)

unitMassGrams
= checked(materialMassGrams + packageTareGrams)
```

`nominalVolumeMl × densityGramsPerLiter / 1000`은 gram이고, `packingEfficiencyPermille / 1000`을 추가 적용하므로 전체 정수 분모는 `1,000,000`이다. 모든 곱셈은 `checked long`으로 수행한다. bulk ore, 곡물, 섬유처럼 빈 공간이 있는 재료는 다음 중 하나만 사용한다.

- 이미 공극을 반영한 bulk density를 쓰면 `PackingEfficiencyPermille=1000`.
- solid density를 쓰면 authored packing efficiency를 적용.

bulk density와 packing efficiency로 같은 공극을 두 번 할인하지 않는다.

### 6.2 파생 item 공식

```text
productBoundInputMass
= Σ(inputQuantity × canonicalInputUnitMass)
+ incorporatedFluidMass
+ packageMaterialMass

consumedProcessFluidMass
= suppliedCleanOrProcessFluidMass - incorporatedFluidMass

consumedProcessFuelMass
= Σ(fuelQuantity × canonicalFuelUnitMass)

totalPhysicalInputMass
= productBoundInputMass + consumedProcessFluidMass + consumedProcessFuelMass

declaredExternalInputMass
= worldSourceMass + ambientOrBiologicalMass + magicIntroducedMass

declaredOutputMass
= Σ(outputQuantity × canonicalOutputUnitMass)
+ physicalByproductMass
+ recoverableWasteMass

declaredLossMass
= moistureVapor
+ trimmingLoss
+ unrecoveredDust
+ offGas
+ consumedFuelExhaust
+ biologicalDiscard

terminalSinkMass
= consumedNeedMass + soldOffMapMass + hazardDestroyedMass

transformInvariant
= totalPhysicalInputMass
+ declaredExternalInputMass
- declaredOutputMass
- declaredLossMass
- terminalSinkMass
```

`incorporatedFluidMass`는 완성품에 실제 남은 유체만 뜻한다. 세척·냉각·공정용으로 공급됐지만 제품에 남지 않은 유체는 `consumedProcessFluidMass`로 분리해 wastewater/byproduct/loss로 닫는다. 같은 clean water gram을 두 항에 중복 계상하지 않는다.
`0 <= incorporatedFluidMass <= suppliedCleanOrProcessFluidMass`를 요구하고 뺄셈은 checked nonnegative 연산으로 수행한다.

연료는 제품 질량에 편입하지 않는다. 연료의 물리 질량은 ash·slag 같은 physical byproduct 또는 `CombustionExhaust` 등 명시적 loss로 전부 닫는다. WIP에는 아직 연소되지 않은 committed fuel과 이미 실현된 byproduct/loss만 단계별로 기록한다.

Transform 레시피는 `transformInvariant == 0g`를 요구한다. 이는 입력과 출력이 같다는 뜻이 아니라 모든 차이가 외부 유입·물리 출력·terminal Sink·추상 손실 중 정확히 한 항에 귀속된다는 뜻이다.

### 6.3 다중 출력·손실 귀속

- 제품·부산물·terminal Sink·abstract loss마다 `massAllocationPermille` 또는 exact grams를 둔다.
- 동일 회계식의 귀속 allocation 합은 1000이어야 한다. 이는 모든 항을 물리 item으로 만들거나 제품 output 합만 1000으로 만들라는 뜻이 아니다.
- waste/slag/sawdust는 회수·저장·운반·재사용의 gameplay 가치가 있을 때 물리 output으로 만든다.
- gameplay상 추적 가치가 없는 vapor/off-gas/미세 분진은 typed terminal Sink 또는 abstract loss로 둘 수 있다.
- 주산물 하나에 모든 질량을 몰아넣거나, 가치 있는 부산물을 0으로 숨기거나, BOM 오류를 큰 추상 손실로 덮지 않는다.

### 6.4 확률 출력

경제 기대값과 branch별 질량 귀속을 분리한다.

- EWU/가격은 확률 가중 expected output을 사용할 수 있다.
- 질량 회계는 실제로 발생 가능한 각 output branch를 검사한다.
- bonus output 성공 시 출력이 입력보다 커지면 declared external input 또는 Source 권위가 없을 때 실패한다.
- bonus가 나오지 않은 branch에서 제품으로 가지 않는 질량은 물리 부산물, terminal Sink 또는 typed abstract loss로 귀속한다.
- 독립 확률 output 조합이 여러 개면 reachable branch의 min/max mass를 모두 검사한다.
- 각 authored output line은 recipe 안에서 영구적인 `outputLineId`를 가진다. list index나 표시명은 identity가 아니다.
- completion roll key는 `(rootSeed, billId, cycleSequence, recipeId, outputLineId, rollKind)`이며 output list 순서·재시도·복원에 독립적이다.
- 독립 확률 line과 fractional quantity roll을 모두 해결한 완전한 `ResolvedOutputOutcome`을 먼저 권위 상태에 publish한다. 하나의 `resolvedOutputBranch` 문자열로 여러 line의 결과를 축약하지 않는다.
- `ResolvedOutputOutcome`은 모든 physical output, special handler output, byproduct, package return, declared loss의 exact slice/gram을 가진다.
- `outputRollState`는 `Unresolved/Resolved/Committed` phase와 outcome fingerprint만 뜻하며 순차 RNG 내부 상태를 WIP에 복제하지 않는다.

### 6.5 여러 레시피가 같은 item을 생산하는 경우

- item mass는 recipe별로 달라지지 않는 물리 불변값이다.
- primary recipe는 후보 kg를 제안할 뿐 최종 권위가 아니다.
- alternative recipe도 같은 output unit mass를 만족해야 한다.
- 레시피별 파생 후보가 다르면 평균내지 않고 `MULTI_PRODUCER_MASS_CONFLICT`로 보고한다.
- recipe cost처럼 최소 경로 relaxation으로 item kg를 선택하지 않는다.

### 6.6 Source·Transform·Sink

| Flow role | 질량 규칙 |
|---|---|
| Source | 채집·채굴·성장·번식 등 세계에서 질량을 도입. source amount와 unit meaning 필수 |
| Transform | 입력 + 명시적 외부 유입 = 출력 + 부산물 + terminal Sink + 명시적 추상 손실, exact gram |
| Sink | 섭취·연소·폐기처럼 내용물을 world physical inventory에서 제거. 제거 사유와 양, tare disposition 필수 |

Source 레시피를 질량 생성 버그로 오판하지 않고, Transform을 Source로 표기해 무료 질량 생성을 숨기지도 않는다. Sink는 내용물의 종착을 뜻할 뿐 재사용 용기까지 자동으로 소멸시킬 권한이 아니다.

production의 모든 물리 제거는 다음 closed command/receipt 경계 뒤에서만 실행한다. 타입과 namespace는 여기서 고정한다. `PhysicalItemDispositionKind`, command/receipt, lot/output/loss 계약과 service interface는 신규 `Assets/Scripts/Services/Items/PhysicalItemDispositionService.cs`의 `DungeonStory.Items` namespace, `Assembly-CSharp`가 소유한다. domain-specific adapter와 WIP join은 각 `Assembly-CSharp` service가 소유하며 Items core asmdef나 Foundation에 domain concrete type을 넣지 않는다.

```csharp
public enum PhysicalItemDispositionKind
{
    SourceIntroduced,
    Transferred,
    Transformed,
    ConsumedByNeed,
    FuelCombusted,
    WasteProcessed,
    SoldOffMap,
    DestroyedByHazard
}

public readonly struct PhysicalItemDispositionCommand
{
    public string OperationId { get; }
    public string CommitId { get; }
    public PhysicalItemDispositionKind Kind { get; }
    public IReadOnlyList<PhysicalItemLotSlice> ExactInputSlices { get; }
    public IReadOnlyList<ExpectedPhysicalOutput> ExpectedOutputs { get; }
    public IReadOnlyList<ExpectedMassLoss> ExpectedLosses { get; }
}

public readonly struct PhysicalItemDispositionReceipt
{
    public string OperationId { get; }
    public string CommitId { get; }
    public long InputMassGrams { get; }
    public long OutputAndByproductMassGrams { get; }
    public long TerminalSinkMassGrams { get; }
    public long DeclaredLossMassGrams { get; }
    public string ResultFingerprint { get; }
}
```

- `Transferred`는 world 총질량을 줄이지 않으며 input/output lot ownership만 바꾼다.
- `SourceIntroduced`는 input 0을 허용하지만 source authority와 exact created grams를 요구한다. Transform 내부의 물·공기·생물 성장·마력 유입도 별도 declared external-input line으로 같은 규칙을 따른다.
- Transform/Sink 계열은 command의 exact input slice와 예상 output/loss를 모두 prepare한 뒤 commit한다.
- receipt는 `input+declaredExternalInput = output+byproduct+terminalSink+declaredProcessLoss` 또는 Source의 명시적 introduction equation을 증명한다. 섭취처럼 의도적으로 physical inventory를 떠나는 질량을 공정 손실로 위장하지 않는다.
- 같은 commit ID는 idempotent하고 다른 payload fingerprint로 재사용하면 fail-loud한다.
- low-level repository remove/quantity mutation은 이 transaction 내부 구현으로만 남기며 production direct caller를 manifest 0으로 만든다.
- commit receipt의 save owner는 command를 시작한 domain aggregate다. active/terminal operation의 commit ID와 result fingerprint를 domain state와 physical mutation의 같은 publication 경계에 기록한다.
- 전역 무한 receipt log는 만들지 않는다. operation이 terminal이고 재시도·복원 참조가 없다는 증거 뒤 deterministic하게 prune하며, prune 전 current-format save는 idempotency 정보를 보존한다.

### 6.7 Sink의 포장 회수 불변식

포장된 item을 소비하는 모든 action/recipe branch에 대해 다음을 계산한다. 다만 `packageTareGrams > 0`이 곧 빈 용기 item 생성 의무를 뜻하지는 않는다. authoring에서 reusable/physical-waste/destroyed/transfer/bulk-infrastructure 중 하나를 먼저 선택한다.

```text
consumedPackedMass
= contentMass
+ packageTareMass

sinkInvariant
= consumedPackedMass
- contentSinkMass
- returnedContainerMass
- physicalPackagingWasteMass
- declaredPackagingLossMass
```

`sinkInvariant == 0g`가 아니면 실패한다.

추가 계약:

- `ReusableContainerReturn`을 선택한 경우에만 반환 용기 수량을 소비된 packed unit 수량과 exact하게 연결한다.
- `DisposableWasteByproduct`는 물리 waste가 물류·처리 gameplay에 의미가 있을 때만 사용한다.
- `DestroyedDuringUse`는 일회용 포장·소각·파손을 parent Sink에 결속된 declared packaging loss로 닫으며 빈 용기 item을 생성하지 않는다.
- `BulkInfrastructureNotInUnit`는 병·탱크·그릇이 시설 인프라에 속하므로 unit tare에 포함하지 않는다.
- partial stack 소비는 실제 소비 수량만큼만 빈 용기를 만든다.
- cancel-before-commit은 내용물·용기·waste 모두 변화 0이다.
- commit 재호출은 empty container 또는 waste를 중복 생성하지 않는다.
- 내용물 Sink와 용기 반환은 같은 transaction/operation ID 아래 exact-once로 처리한다.
- output 공간이 없으면 내용물만 먼저 소비하지 않고 `OutputCapacityUnavailable`로 fail-loud한다.
- 물리 반환으로 authoring된 용기만 우선 소비자 inventory로 들어가고, 불가능하면 현재 cell의 typed physical byproduct로 생성한다.
- emergency egress·유일 접근칸에 byproduct를 조용히 drop하지 않는다. 합법 output destination 또는 capacity가 없으면 action을 시작하지 않는다.

### 6.8 재공품·촉매·출력 원자성

현재 생산 경로는 cycle 시작 시 delivered input을 소비하고, 작업/숙성 후 output을 생성한다. 따라서 `materialsConsumed=true`인 구간은 물리 입력이 사라진 공백이 아니라 시설이 소유한 재공품(WIP)이다.

```text
WipMass
= productBoundConsumedSolidMass
+ incorporatedFluidMass
+ incorporatedPackagingMass
+ unburnedCommittedFuelMass
- realizedLossMass
- alreadyEmittedByproductMass
```

`productBoundConsumedSolidMass`는 fluid·packaging·process fuel·returned catalyst를 제외한 exact consumed slices의 질량이다. 각 항은 서로 배타적이어야 하며 같은 input gram을 두 항에 중복 계상하지 않는다.

`InProcessMassLedger`의 최소 필드:

```text
operationId
recipeId
facilityId
cycleSequence
consumedInputSlices
returnedCatalystSlices
toolWearEvents
incorporatedFluidGrams
unburnedCommittedFuelGrams
realizedLossByKind
emittedByproductSlices
pendingOutputMassByBranch
resolvedOutputOutcome
resolvedOutputSlices
outputRollState
outputCommitId
commitPhase
saveAuthority
```

입력 역할:

```csharp
public enum RecipeInputMassDisposition
{
    ConsumedIntoProduct,
    ConsumedProcessFuel,
    ReturnedCatalyst,
    ToolWearOnly,
    ReusableContainer,
    InfrastructureRequirement,
    PackagingTransferredToOutput
}
```

규칙:

- `ConsumedIntoProduct`만 product-bound WIP 질량에 편입한다.
- `ConsumedProcessFuel`은 연소 전까지 `unburnedCommittedFuelMass`로 WIP가 소유하고, 연소 뒤 ash/byproduct와 `CombustionExhaust` loss로 전부 닫는다. 완성품 질량에는 편입하지 않는다.
- `ReturnedCatalyst`와 `ReusableContainer`는 cycle 후 exact identity·quantity·state로 반환한다. 내구가 닳으면 별도 wear 또는 물리 손실을 기록한다.
- `ToolWearOnly`와 `InfrastructureRequirement`는 제품 질량에 합산하지 않는다.
- deterministic output은 기존 cycle admission에서 exact output grams를 예약할 수 있다. 확률 output은 작업 시작 시 최대치를 과예약하지 않는다.
- 확률·다중 line output은 작업 완료 시 key-addressed RNG로 각 roll을 정확히 한 번 해결한다. 완전한 `resolvedOutputOutcome`을 authoritative WIP state로 먼저 commit한 뒤 실제 output·byproduct·package-return grams의 destination capacity를 예약한다.
- output 공간이 없으면 완성품을 일반 Loose 바닥에 생성하지 않고 동일 WIP와 resolved outcome을 `WaitingForOutputSpace`로 유지한다. 재시도·저장 복원·worker 교체가 결과를 재굴림하지 않는다.
- physical repository output과 domain-specific `TryHandleOutput` 결과는 모두 prepare한 뒤 하나의 `outputCommitId`로 all-or-nothing commit한다. 중간 line만 먼저 생성·소비·이벤트 발행하지 않는다.
- commit fault는 준비한 모든 output/capacity lease/domain effect를 역순 rollback한다. 같은 `outputCommitId` 재호출은 기존 receipt를 반환하고 두 번째 mutation은 0이다.
- output 생성 실패는 이미 소비한 input을 다시 소비하지 않으며 WIP를 유지한 채 typed blocked state가 된다.
- cancel-before-consume은 destination release만 하고 물리 수량 변화 0이다.
- cancel-after-consume은 무료 원재료 복원을 금지한다. authored salvage/byproduct 반환 또는 typed irrecoverable loss 중 하나로 닫는다.
- 시설 Destroyed/Disabled, worker Downed, no-path, save/restore 각각에서 WIP·loss·output reservation이 exact-once다.
- passive batch의 준비·처리·완료 stage 전환마다 WIP mass가 저장되고 복원 후 같은 cycle sequence로 재개된다.
- old save migration은 범위 밖이지만 현재 형식 save의 동일 빌드 round-trip은 필수다.

시설 output은 생산자가 직접 창고로 운반하지 않는다. 별도 AIHaul이 시설의 물리 `FacilityBuffer`에서 회수한다.

```text
facilityOutputCapacityGrams
= clamp(
    max(2 × maximumCycleCompletionFootprintGrams,
        ceil(p95HaulClearanceHours × peakOutputMassPerHour)),
    2 × maximumCycleCompletionFootprintGrams,
    4 × maximumCycleCompletionFootprintGrams)
```

```text
maximumCycleCompletionFootprintGrams
= max reachable outcome의
  (main outputs + physical byproducts + returned packaging + recoverable waste)

remainingOutputCapacityGrams
= facilityOutputCapacityGrams
 - physicalFacilityBufferMassGrams
 - reservedResolvedOutputMassGrams
```

- 산정 요구량이 4회분을 넘으면 버퍼를 더 키우지 않고 물류·시설 처리량 Critical로 판정한다.
- buffer가 차기 전까지 생산자는 다음 cycle을 계속할 수 있다.
- FacilityBuffer는 warehouse capacity를 빌려 쓰지 않으며 exact owner·operation·mass reservation을 가진다.
- Sink의 empty container/waste도 실제 생성 전 같은 output-capacity token을 reserve한다. 단순 query preflight 후 내용물부터 소비하지 않는다.

---

## 7. 허용 질량 손실과 금지 규칙

### 7.1 허용 손실 종류

```csharp
public enum RecipeMassLossKind
{
    None,
    MoistureEvaporation,
    CuttingAndTrimming,
    GrindingDust,
    SmeltingSlag,
    SmeltingOffGas,
    CookingDripAndSteam,
    FermentationGas,
    CombustionExhaust,
    BiologicalDiscard,
    ContaminationDisposal
}
```

각 손실은 다음을 가진다.

```text
lossKind
lossGrams
lossPermilleOfInput
reason
physicalByproductItemId(optional)
evidenceSource
```

`physicalByproductItemId`는 선택 사항이다. 해당 부산물이 재활용·오염·운반·저장·가격에 실제 선택을 만들 때만 물리 item을 요구한다. 그렇지 않은 증기·분진·배기·미량 폐기는 exact gram과 reason이 receipt에 남는 추상 손실로 끝낼 수 있다. 전역 `5%` 같은 tolerance는 두지 않지만, family policy가 계산한 큰 손실도 설명과 재현성이 있으면 합법이다.

### 7.2 공정별 정책

- 수분 증발: 입력 수분과 조리/건조 조건에서 계산
- 절삭·제재: sawdust/scrap을 물리 부산물로 우선 생성, 추적하지 않는 미세 분진만 sink
- 제련: ingot + slag + off-gas로 분리
- 연료: 시설 에너지 입력으로 소비된 연료 질량은 제품에 편입하지 않고 exhaust/ash로 귀속
- 세척: 제품에 남은 물과 wastewater를 분리
- 도축: edible meat + hide/bone/blood/waste 합이 carcass input과 일치
- 음식 조리: 재료 + 포함된 물 - steam/drip = meal + waste
- 의약: 용매 증발, reusable/used 포장 용기, 오염 폐기물을 분리
- 음용·섭취: 내용물만 Sink하고 reusable tare는 empty container로, disposable tare는 물리 waste 또는 exact declared loss로 귀속
- 해체: 회수 material + scrap/waste <= 원 장비 instance mass

### 7.3 금지

- `입력과 출력이 안 맞지만 5% tolerance` 같은 포괄 허용
- EWU 반올림 epsilon을 질량에 재사용
- output mass를 맞추기 위한 임의 hidden water 추가
- reason·gram·operation 결속 없이 waste를 값 없는 추상 숫자로만 차감
- 레시피가 없다는 이유로 기본 1kg 적용
- unknown item을 0.01kg로 clamp해 통과
- builder에서 asset 값과 다른 runtime mass 생성
- 에셋 재생성 후 kg가 되돌아가는 이중 authoring
- `packageTareGrams > 0`인데 reusable/waste/destroyed/transfer/bulk-infrastructure disposition이 없음
- 재사용 용기를 내용물과 함께 Sink 처리
- 내용물 소비 commit과 용기 반환 commit이 분리되어 복제·누락 가능

---

## 8. 전투 장비·의복 중량 일치

### 8.1 전투 장비

기본 계약:

```text
defaultMaterialPhysicalMass
= CombatEquipmentDefinitionSO.weight
× defaultMaterial.WeightMultiplier
+ requiredComponentMassAdjustment
```

물리 item의 default `unitWeight`는 이 값과 exact gram 일치해야 한다.

unique instance:

```text
instanceMass
= CombatEquipmentDefinitionSO.weight
× actualMaterial.WeightMultiplier
× physicalEvolutionMassMultiplier
+ installedModuleMass
+ loadedAmmunitionMass
```

규칙:

- 전투 피해·방어용 weight와 물리 운반 kg를 서로 다른 수치로 따로 작성하지 않는다.
- combat projector와 physical mass query가 공용 `EquipmentMassProjector`를 사용한다.
- 장착 전 world stack, 운반 중 carried item, 장착 후 combat loadout의 instance kg가 같아야 한다.
- world state(Loose/Stored/Carried/Equipped) 변경은 kg를 바꾸지 않는다.
- durability 감소는 부품이 실제 분리되지 않는 한 kg를 바꾸지 않는다.
- module 장착/해제는 module physical mass만 exact 더하고 뺀다.
- 장전/배출은 exact ammunition item grams × loaded count만 더하고 뺀다. 탄약 종류·수량 mutation과 physical stack/warehouse/haul revision은 같은 transaction이다.
- haul-owned cargo의 material/evolution/module/ammunition mutation은 첫 구현에서 typed reject한다. destination gram lease delta 원자 갱신은 별도 승인된 후속 계약 없이는 허용하지 않는다.
- material 변경·reforge가 있다면 실제 input/output 질량과 instance mass를 원자적으로 갱신한다.
- evolution effect가 `combat.weight`를 바꾸는 경우 물리적 질량 변화인지 전투 체감 중량인지 분류한다. 물리 질량이 아니면 별도 stat ID로 분리한다.

### 8.2 의복

```text
apparelInstanceMass
= ApparelDefinitionSO.baseWeight
× TextileMaterialDefinitionSO.WeightMultiplier
+ modificationMass
```

규칙:

- physical `unitWeight`는 default textile의 apparel instance mass와 일치
- 다른 원단으로 제작한 unique 의복은 component 기반 instance mass 사용
- 젖음·오염·내구·품질·신선도·충전량은 V27에서 질량 불변이다. 향후 물리 성분을 추가하려면 별도 component grams와 전수 승인 계약이 필요하다.
- 멜빵은 physical item `1,150g`을 단일 권위로 사용한다. apparel `baseWeight 0.9kg` 복제값은 제거하거나 physical item 권위에서 exact `1,150g`으로 투영한다.
- 멜빵 자체 `1,150g`은 equipped mass에 정확히 한 번 포함하고 payload capacity 보너스와 중복 차감하지 않는다.

### 8.3 이중 계산 방지

- world stack에 있을 때 physical mass query만 계산
- carried inventory에 있을 때 같은 instance component로 한 번만 계산
- equipped combat/apparel loadout으로 이전되면 world/carry stack 소유권을 제거하고 loadout projection만 계산
- cargo 이동 감속과 전투 장비 기동성 패널티는 서로 다른 consumer일 수 있으나 같은 kg source를 읽음
- 한 instance가 world, carry, equipped 중 두 곳에 동시에 있으면 mass보다 먼저 ownership invariant 실패

### 8.4 장비 검증 수

- canonical equipment feature 61개 전수
- combat definition 61개와 physical item exact mapping
- 모든 allowed craft material 조합
- 모든 apparel definition과 allowed textile material 조합
- module 장착/해제
- repair, dismantle, sale, confiscation, death drop, save/restore

---

## 9. 시설 키트와 특수 대형 물품

### 9.1 시설 설치 키트

현재 시설 키트는 전부 `8kg`, maxStack 1로 생성된다. 이를 전체 시설 BOM의 질량과 동일시하지 않는다.

unit semantic:

```text
시설 설치 키트 1개
= 현장에서 조립할 수 있도록 포장된 핵심 subassembly·도면·fastener 묶음
```

계획:

- 건설 BOM 전체는 별도 재료 운반 경로가 비용 권위
- 설치 키트는 구매/청사진/설치 권한과 핵심 부품의 물리 토큰
- kit kg는 building footprint, construction BOM kg, 복잡도에 따른 packed formula로 `8~14kg` 산정
- 대형 시설도 `19kg`를 넘기지 않음
- kit 하나와 건설 BOM이 같은 재료를 이중 debit하지 않는지 검사
- kit를 해체해 전체 BOM을 얻을 수 없도록 회수 규칙 대조
- builder의 고정 `8f`를 mass profile projection으로 교체

### 9.2 설계도·기록·촉매

- 설계도는 책/두루마리 한 묶음으로 `0.05~0.50kg`
- 촉매는 용기 포함 exact mass
- progression potency가 높다는 이유만으로 질량을 자동 증가시키지 않음
- 값비싼 item이 반드시 무거워야 한다는 규칙 금지

### 9.3 생물·사체·장기

- 살아 있는 actor/환자/포로/생포 동물은 item mass가 아니라 구조·호송 authority를 사용한다. item kg admission, hard cap과 이동속도 페널티를 적용하지 않는다.
- 전용 구조·생포 행동의 실제 path, carrier ownership, subject parent/occupancy, interruption과 save/restore는 그대로 검증한다.
- 생물 운반 중 신규 opportunistic item haul과 두 번째 생물 운반 ownership은 금지한다.
- `OffenseFieldMobilityRules`의 기존 `최소 40kg / BaseCarryLimit ×3`는 생물 운반 gameplay admission에서 제거한다. 생물 질량은 정보 표시용으로도 이번 V27 item kg 원장에 합산하지 않는다.
- 장기·절단 부위는 item unit semantic과 species source mass를 연결
- carcass를 통째 1개로 운반하면 generic 한도와 충돌할 수 있으므로 실제 운반 행동을 감사
- 전용 drag/animal transport가 없다면 carcass output을 현실적인 butcherable package로 분할하거나 20kg 이하 unit으로 구성
- 사체를 28kg로 두고 일반 AI가 최대 29kg 과적을 상시 사용하게 만드는 것은 본 계획의 목표에 맞지 않음

---

## 10. 레시피별 운반 묶음 감사

### 10.1 계산 지표

각 레시피에 다음을 기록한다.

```text
recipeId
flowRole
processClass
inputMassKg
outputMassKg
byproductMassKg
declaredLossKg
massResidualGrams
recipeInputBatchKg
largestInputLineKg
expectedOutputBatchKg
ordinaryCarryTrips
harnessCarryTrips
haulClass
targetBand
bandDisposition
```

### 10.2 목표 분류

| haul class | 레시피 입력 목표 | 사용 예 |
|---|---:|---|
| MicroUrgent | 0.1~6kg | 약품, 탄약, 한 끼, 설계도 |
| OrdinaryRecurring | 6~11kg | 조리, 세척, 단순 제작, 일상 생산 |
| OrdinaryMixedCandidate | 입력 6~11kg / 실제 plan 8~14kg | compatible stack 추가 적재 |
| HeavyRecurring | 11~15kg | 대량 제분, 목공, 소규모 제련 |
| HeavyBatch | 15~20kg | 광석, 석재, 대형 부품, 집중 투입 |
| OversizeEquipment | 20~36kg | 정말 무거운 unique 장비 |
| DedicatedTransport | generic kg 대상 아님 | 환자, 포로, 살아 있는 동물 등 |

### 10.3 판정 규칙

- current-source 355개 레시피를 전수 검사한다.
- 반복 공정은 p50이 OrdinaryRecurring 안에 들도록 우선 조정한다.
- 레시피 kg를 맞추기 위해 입력 수량을 임의 변경하지 않는다.
- 먼저 `1개 의미`와 unit kg가 잘못됐는지 확인한다.
- 그다음 레시피 batch size가 실제 한 cycle인지 확인한다.
- batch size가 의도적으로 크면 multi-trip 또는 시설 local storage가 적법한지 확인한다.
- 한 input line이 전체 kg를 독점해 다른 재료를 항상 별도 왕복시키는지 검사한다.
- exact destination의 마지막 소량은 band 미달이어도 허용한다.
- actual mixed haul은 compatible destination/leg만 묶고 목적지가 다른 스택을 kg 목표 때문에 합치지 않는다.
- full load를 기다리느라 긴급 주문이 지연되면 실패한다.

### 10.4 운반 분포 목표

대표 정착지 PlayMode에서 다음 분포를 기록한다.

- 일반 반복 haul p50: `8~11kg`
- 일반 반복 haul p75: `8~14kg`
- 일반 반복 haul p95: `19kg 이하`
- 6kg 미만 haul: 긴급·마감·unique 사유가 100% 기록됨
- 19kg 초과 haul: OversizeEquipment 또는 명시적 HeavyBatch 사유만 존재
- 일반 반복 haul에서 최대 과적 구간 사용률 목표 `0%`, 허용 상한 `5%`
- 멜빵 없는 actor가 20kg 초과 일반 원료를 집는 횟수 `0`
- 멜빵이 있을 때 HeavyBatch 처리량 개선은 존재하되 멜빵 없이는 생존망이 멈추지 않음

### 10.5 kg·효능·노동·경제 결합 감사 게이트

각 item의 kg를 ApplyApproved로 승격하기 전에 다음 Before/After 행을 생성한다.

```text
itemId
beforeUnitMassGrams
afterUnitMassGrams
massDeltaPercent
payloadKind
beforePayloadPerUnit
afterPayloadPerUnit
beforePayloadPerKg
afterPayloadPerKg
payloadPerKgDeltaPercent
producerRecipeIds
consumerRecipeIds
nonRecipeConsumerIds
quantitySemanticConsumerIds
orderContractExpeditionConsumerIds
inputMassDisposition
wipAuthorityId
fluidAuthorityIds
beforeDirectWuPerOutputKg
afterDirectWuPerOutputKg
beforeEwuPerKg
afterEwuPerKg
beforeBuyPricePerKg
afterBuyPricePerKg
beforeSellPricePerKg
afterSellPricePerKg
beforeMaxStackMassKg
afterMaxStackMassKg
warehouseStageRelationIds
recipeThroughputRelationIds
consumerEconomicsRelationIds
haulScenarioRelationIds
couplingDisposition
couplingReason
```

warehouse capacity, reserve utilization, recipe batch, contract reward, planner latency는 item 하나에 값 하나가 아니다. 인구 단계·시설·recipe·consumer·actor class별 normalized relation 원장으로 기록하며, item 행은 그 stable relation ID만 참조한다. `storageDaysPerCell`은 kg admission과 실제 pile/cell 점유를 섞으므로 사용하지 않는다.

`payload`는 도메인별 실제 효능을 뜻한다.

- 식품: nutrition, mood, freshness, spoilage
- 사료: feed value, 대상 종, 소비 주기
- 약품: treatment potency, dose count, package return
- 연료: fuel value, burn duration, 부산물
- 건설·중간재: downstream BOM coverage, facility throughput
- 무기·방어구: 피해·방어·사거리·내구·기동성
- 일반 재료: producer yield, downstream coverage, storage density

결합 조정 우선순위:

1. `1개` 의미와 물리 kg를 확정한다.
2. 모든 producer의 질량식을 닫는다.
3. 모든 consumer와 sink가 같은 전역 kg를 읽는지 확인한다.
4. payload/kg가 category 기준에서 벗어나면 단위 효능 또는 소비량을 조정한다.
5. recipe batch 효능과 일일 수요가 깨지면 출력 수량·소비 수량 후보를 만들되, quantity 기반 주문·계약·원정·의료·건설 소비자 전수가 닫힌 경우에만 적용한다.
6. 반복 처리량이 깨지면 Direct WU와 facility throughput을 조정하고 Source의 land/water/feed/time budget도 함께 확인한다.
7. maxStack은 pile/partial pickup을 위해 조정하고, 창고 `maxCapacityGrams`는 7일 비축·정상 70%·장애 90% 목표로 별도 배정한다. item count를 admission capacity로 사용하지 않는다.
8. WIP, catalyst/tool/container 반환, output reservation, fluid mass/volume을 검증한다.
9. 실제 haul trip, overload, order aging과 heavy/urgent fairness를 검증한다.
10. 시장·계약·원정의 unit reward와 kg당 물류비를 검증한다.
11. 마지막에 EWU와 가격을 재생성한다.

자동 판정:

- `abs(massDeltaPercent) < 25%`: 표준 결합 감사
- `25% 이상`: producer/consumer 전수 결합 감사 없이는 apply 금지
- `50% 이상`: `COUPLED_BALANCE_REVIEW_REQUIRED`; 효능·출력·WU·stack·storage 중 적어도 하나를 검토하고 근거를 기록
- payload/kg 변화 `25% 이상`: Warning
- payload/kg 변화 `50% 이상`: Critical
- WU/kg, EWU/kg, price/kg 변화 `50% 이상`: root-cause attribution 없는 경우 Critical
- maxStack 총질량이 일반 actor 최대 29kg를 반복적으로 초과: stack/partial-pickup 감사 필요
- storage days/cell 변화 `50% 이상`: 6인 또는 해당 도메인 live 회귀 필수
- output count·maxStack·warehouse capacity 변화: quantity consumer manifest와 current-format save round-trip 없이는 Critical
- 구 item-count capacity admission을 계속 사용하는 행: `WAREHOUSE_COUNT_CAPACITY_LEGACY_PATH` Critical
- consumed input이 있는데 WIP/save/cancel/output-failure authority가 없는 recipe: `IN_PROCESS_MASS_AUTHORITY_MISSING` Critical
- catalyst/tool/container를 consumed mass로 처리하거나 반환 증거가 없는 recipe: `INPUT_DISPOSITION_UNRESOLVED` Critical
- fluid grams와 hydraulic volume 중 하나가 정의되지 않은 changed recipe: `FLUID_UNIT_AUTHORITY_MISSING` Critical
- causal cone 밖 order가 kg 변경 후 starvation되는 경우: `HAUL_PRIORITY_FAIRNESS_REGRESSION` Critical

kg 변화가 큰데도 모든 다른 값을 유지하는 선택은 자동 승인하지 않는다. `물리적으로 자연스러운 질량 + 의도된 고효율`이라는 명시적 설계 근거와 live 증거가 있을 때만 유지할 수 있다.

---

## 11. 6인 생존망 질량 회귀

### 11.1 기존 수요 권위

6명 음식 기준:

```text
평균 수요 = 300 nutrition/일
gross 목표 = 375 nutrition/일
net 하한 = 330 nutrition/일
7일 비축 = 2,100 nutrition
nutrition 35 식사 = 8.572개/일 소비
gross 식사 목표 = 10.715개/일
net 식사 하한 = 9.429개/일
```

kg 적용 후 다음을 새로 계산한다.

- 1식의 exact kg
- 하루 음식 소비 kg
- 하루 생산 입력 kg
- 7일 비축 kg와 필요한 storage slots/cells
- 수확 burst kg
- 창고→조리대 input kg
- 조리대→저장/식사 output kg
- 농업·조리·청소·운반 WU
- 운반자 1명 장애 시 p95 처리 지연

### 11.2 통과 조건

- gross nutrition 125% 이상
- net nutrition 110% 이상
- 물리 7일 비축 유지
- 6명의 effective `270 WU/일` 안에서 생존 반복 노동 25~35%
- 물류 12~20%
- 성장 노동 35% 이상
- 비상 예비 10% 이상
- 정상 storage 70% 이하
- 장애 storage+containment 90% 이하
- persistent floor clutter 0
- food/water access·egress clutter 0
- 일반 식품 운반 p75 14kg 이하
- 음식·물 emergency fallback은 small haul이어도 지연 없이 시작
- 멜빵·육중한 체격이 없어도 정상 생존망 통과
- 멜빵은 여유와 복구력을 높이지만 생존 필수 장비가 아님
- 주 시설 장애 하루 동안 N+1 primitive 경로의 실물 소비 exact
- 복구 후 primitive 지속 선점 0

### 11.3 공간 재검증

kg 변경으로 stack count와 storage occupancy가 바뀔 수 있으므로 다음을 다시 검사한다.

- population 1/3/6/12/18/24 stage
- 7일 비축 stack 수
- max batch와 overflow containment
- 30% runtime headroom
- shared access/corridor 혼잡
- floor clutter grace 초과 0
- 256 layout order seed 성공률 95% 이상

---

## 12. EWU·가격 재생성

### 12.1 재계산 순서

```text
item unit semantics
-> canonical grams
-> recipe mass balance
-> haul batch/trip counts
-> handling/logistics EWU
-> AcquisitionCost/RecoverableValue
-> buy/sell/contract prices
-> SCC/arbitrage audit
-> six-adult runtime regression
```

kg만 바꾸고 가격을 유지하지 않는다. 현재 V27 계산기는 item weight를 handling work에 사용하므로 kg 변경은 경제 source digest를 변경한다.

### 12.2 불변식

- 입력 비용 Ceil
- 산출·회수·판매 가치 Floor
- 반복 transform 최소 `-1 mEWU`
- SCC tolerance 0
- 구매→제작→판매 차익 0
- 제작→해체→재제작 차익 0
- 시설 키트→건설→해체 차익 0
- 장비 material variant→해체 material 차익 0
- 질량 손실을 EWU 가치 생성으로 보상하지 않음
- 무거운 item이 무조건 비싸지는 단순 선형 가격 규칙 금지
- 가격은 재료·노동·물류의 합이며 희귀도만으로 kg를 바꾸지 않음

### 12.3 승인 영향

- kg 변경으로 source digest와 dependency fingerprint가 바뀐 행의 기존 approval은 자동 만료
- inherited-only 가격 변화는 root-cause tree 아래 접을 수 있음
- item 자체 kg가 바뀐 행은 local change로 남김
- 하나의 approval 행은 하나의 판단만 소유한다. 반복 레시피의 `direct-wu` 승인과 `labor-density-ratio` 파생 경고를 같은 행에 합치지 않는다.
- `direct-wu`의 source digest는 대상 레시피 ID·공정 종류·실제 런타임 작업 계산기 버전만 결속한다. 질량 설명, 출력 원가 배분, 표시 문자열처럼 직접 WU를 바꾸지 않는 메타데이터 변경은 노동 승인 자체를 무효화하지 않는다.
- BOM EWU가 바뀌어 노동 밀도가 변하면 별도 `labor-density-ratio` 행을 만든다. 이 행의 로컬 delta는 `0`이고, 변경된 입력 아이템의 선택 생산 경로를 재귀 추적해 Source·Crop·Recipe·External item 근본 원인 아래로 접는다.
- 이전 approval을 새 digest로 재검증할 때는 `exact Before/After`, dependency fingerprint, reasonCode, baseline record ID가 모두 같고 현재 에셋이 exact After를 이미 보유한 경우만 허용한다. 신규 After나 미적용 후보는 이 경로로 승인할 수 없다.
- 재검증 중 현재 원장에 더 이상 대응하지 않는 키는 “승인”이 아니라 만료 이력이다. 이미 적용된 값이 다음 재계산 후보와 다르면 이를 임의 drift로 실패시키지 말고 `previous-applied authority → recalibration candidate` 두 상태로 분리해 명시적 rebase review를 생성한다.
- `previous-applied authority`는 기존 metric을 그대로 유지하고 `historical Before → exact current` 행으로 캡처한다. 현재 에셋이 exact After와 같은 경우에만 `assetApplied=true`로 기존 approval을 소비한다.
- `recalibration candidate`는 별도 metric에 `exact current → newly calculated candidate`를 기록하고 `assetApplied=false`, `root-critical`, `approvalKey=""`로 남긴다. semantic revalidation, labor/facility 일괄 승인, approved-patch 생성기가 이 행을 자동 소비하면 안 된다.
- WU 후보 metric은 `construction-recalibration-candidate-wu`, BOM 수량 후보는 `construction-recalibration-candidate-material:<itemId>`로 고정한다. 기존 적용 행과 후보 행은 같은 SerializedProperty를 가리킬 수 있지만, 후보 행은 승인키가 없으므로 동시 patch 충돌에 참여하지 않는다.
- exact current가 기존 approval After와도 다르고 새 candidate와도 다르면 여전히 미승인 authority drift로 fail-loud한다. 새 후보를 적용하는 명시적 review/rebase 명령은 기존 approval을 만료하고 기존 metric의 새 exact Before/After approval로 교체한 뒤에만 에셋을 수정해야 한다.
- SCC에는 2 mEWU presentation epsilon을 적용하지 않는다. 질량 회계도 gram 차이를 epsilon으로 숨기지 않되, typed external input/byproduct/Sink/loss 항으로 비보존 공정을 합법적으로 닫는다.
- 승인 없는 After를 asset에 적용하지 않음

---

## 13. 전수 원장과 산출물

과거 `413 item / 354 recipe / 61 combat mapping / 1,060 serialized 위치`는 2026-08-20 capture의 역사적 기대값이다. 2026-08-26 current-source 기대값은 `414 item / 355 recipe / 61 combat mapping / 1,074 serialized 위치 / 356 active facility / 21 deprecated compatibility facility`다. 어떤 수치도 영구 상수가 아니다. 매 AuditOnly 시작 시 current catalogs/builders/assets를 다시 열거해 `BalanceMassSourceInventory`와 digest를 먼저 만든다. 기대값·stable ID 집합·source digest 중 하나라도 다르면 `SOURCE_INVENTORY_CHANGED`로 중단하고 원장 schema/이 문서의 기대값/승인 fingerprint를 갱신한 뒤 재실행한다. 누락된 새 definition을 기존 분모에서 조용히 제외하지 않는다.

### 13.1 item kg 원장

`Artifacts/QA/v27-item-mass-before-after.csv`

필수 열:

```text
schemaVersion
itemId
displayName
definitionKind
resourceKind
stockCategory
unitSemanticKind
unitLabel
unitDescription
beforeUnitMassGrams
afterUnitMassGrams
afterUnitMassKg
massDerivationKind
primaryMaterialId
nominalVolumeMl
densityGramsPerLiter
packingEfficiencyPermille
packageTareGrams
packageTareDisposition
packageContainerItemId
packageWasteItemId
packageLossKind
primaryRecipeId
inputMassGrams
outputMassGrams
declaredLossGrams
maxStack
maxStackMassGrams
payloadKind
beforePayloadPerUnit
afterPayloadPerUnit
beforePayloadPerKg
afterPayloadPerKg
payloadPerKgDeltaPercent
beforeDirectWuPerOutputKg
afterDirectWuPerOutputKg
beforeEwuPerKg
afterEwuPerKg
beforeBuyPricePerKg
afterBuyPricePerKg
beforeSellPricePerKg
afterSellPricePerKg
producerRecipeIds
consumerRecipeIds
nonRecipeConsumerIds
couplingRelationArtifactIds
couplingDisposition
couplingReason
haulClass
ordinaryQuantityAt19Kg
maxQuantityAt29Kg
harnessQuantityAt24Kg
harnessMaxQuantityAt36Kg
equipmentDefinitionId
apparelDefinitionId
massFingerprint
sourceAuthority
sourcePropertyPath
baselineRecordId
reviewStatus
assetApplied
```

`assetApplied=true`는 `couplingDisposition=coupled-pass`를 의미하지 않는다. 이미 focused 적용된 값은 결합 감사 전까지 `provisional-applied`로 기록한다.

### 13.2 recipe 질량 원장

`Artifacts/QA/v27-recipe-mass-balance.csv`

current-source 355개 레시피 모두 포함하고 branch별 mass invariant와 loss reason을 기록한다.

### 13.3 운반 원장

`Artifacts/QA/v27-haul-batch-capacity.csv`

- recipe input kg
- planned quantity
- actual pickup kg
- mixed stack count
- actor base/max/harness limits
- speed multiplier
- trip count
- reason for underfill/overload

### 13.4 장비 일치 리포트

`Artifacts/QA/v27-equipment-mass-coherence.txt`

- combat equipment 61개 mapping
- default material exact match
- all material variants
- apparel/textile variants
- module add/remove
- world/carry/equipped/save-restore equality

### 13.5 kg 결합 밸런스 리포트

`Artifacts/QA/v27-mass-payload-throughput-coupling.csv`

- current-source canonical item 414개 전부 포함
- kg 변경 12개는 우선 재검토 대상으로 고정
- producer/consumer/non-recipe sink 역참조 목록
- payload/kg, Direct WU/kg, EWU/kg, 가격/kg Before/After
- maxStack 총질량과 아래 normalized relation artifact의 stable key
- `coupled-pass`, `revise-mass`, `revise-payload`, `revise-output`, `revise-wu`, `revise-stack-storage`, `rollback` 중 하나의 disposition
- 25%/50% threshold와 category anomaly
- 각 disposition의 baseline record ID와 live evidence

item 한 행에 warehouse·recipe·contract·인구 단계가 여러 개인 값을 임의 단일 scalar로 축약하지 않는다. 다음 관계 원장을 별도 생성한다.

| 산출물 | 유일 키 | 내용 |
|---|---|---|
| `v27-mass-warehouse-stage-capacity.csv` | `(warehouseId, populationStage, scenarioId)` | max/stored/reserved grams, 7일 비축, 정상/장애 utilization. `storageDaysPerCell` 대신 실제 warehouse capacity와 별도 floor/pile occupancy를 기록한다. |
| `v27-mass-recipe-throughput.csv` | `(recipeId, outputLineId, itemId)` | batch grams, direct WU/kg, output frequency, FacilityBuffer footprint, trip count |
| `v27-mass-consumer-economics.csv` | `(itemId, consumerKind, consumerId)` | payload/kg, price/reward/kg, contract/medical/expedition quantity semantic |
| `v27-mass-haul-scenarios.csv` | `(scenarioId, actorClassId, itemId, destinationKind)` | planned/actual kg, trips, latency, overload, starvation |

창고의 gram capacity는 추상 admission 용량이고 실제 던전 셀 점유는 stack/pile record와 배치 footprint의 별도 공간 지표다. kg를 낮췄다는 이유로 저장 셀 수가 자동 증가하거나 감소했다고 해석하지 않는다.

### 13.6 종합 감사

`Artifacts/QA/v27-mass-recalibration-audit.txt`

최상단에 다음을 기록한다.

```text
RESULT
canonicalItems
unitSemanticsResolved
recipesAudited
massBalancedTransforms
declaredLossTransforms
sourceTransforms
sinkTransforms
packagedSinkTransforms
reusableContainerReturns
packagingWasteByproducts
packagingLossTransforms
missingTareDisposition
coupledBalanceItems
provisionalAppliedItems
payloadDensityWarnings
payloadDensityCriticals
throughputDensityCriticals
equipmentMappings
ordinaryBatchPass
heavyBatchPass
oversizeEquipmentCount
unhaulableItems
massCritical
ewuCritical
minimumSccMargin
sixAdultResult
consoleWarnings
consoleErrors
```

### 13.7 결정론

- 기존 V27 canonical capture/normalization 사용
- 정렬 키: domain, definitionKind, stableId, metric 또는 itemId, recipeId, branchId
- CSV RFC 4180 CRLF
- JSON/Markdown LF
- timestamp·절대 경로·Dictionary 열거 순서 금지
- 동일 입력 두 번 생성 byte-identical
- asset apply 두 번째 실행 Git diff 0

---

## 14. Critical·Warning 판정

### 14.1 Critical

- unit semantic 누락
- canonical item ID 중복 또는 누락
- 0 이하·NaN·Infinity·overflow 질량
- 1g 단위로 canonicalize할 수 없는 authority
- generic item `unitMass > 20kg`인데 dedicated transport/exception 없음
- item `unitMass > 36kg`인데 실제 운송 경로 없음
- Transform recipe residual `!=0g`
- undeclared mass loss
- packaged Sink의 tare disposition 누락
- reusable tare 반환 누락·중복 또는 반환 item 질량 불일치
- disposable packaging의 physical waste/declared loss 누락
- mass creation branch
- alternative recipe mass conflict
- combat/apparel physical mass mismatch
- world/carry/equipped/save restore instance mass mismatch
- builder 재생성 후 kg drift
- ordinary recurring input batch `>20kg`
- 일반 haul의 과적 사용률 `>5%`
- six-adult survival/logistics/storage/headroom failure
- kg 변경 후 SCC margin `>=0`
- kg 25% 이상 변경 항목에 producer/consumer/non-recipe sink 결합 감사 누락
- kg 50% 이상 변경 항목이 `provisional-applied` 상태로 최종 원장에 남음
- payload/kg, WU/kg, EWU/kg 또는 price/kg 변화 50% 이상인데 원인 귀속·승인·live 증거 없음
- 같은 중간재를 producer/consumer별 서로 다른 unit mass로 해석
- kg를 맞추기 위해 공정 상태마다 저장·물류 의미가 없는 중간 아이템을 무분별하게 분할
- 창고 입고·부분 입고·복원·UI 중 하나라도 구 item-count capacity를 사용
- 창고 StoredMass 계산이 generic unit mass 또는 unique instance mass와 불일치
- destination gram lease 없이 복수 hauler/conveyor가 같은 남은 창고 kg를 중복 약속
- 창고 포화 때문에 carried cargo를 Loose로 자동 드롭하거나 owner operation을 완료 처리
- stock/inbound가 남은 창고 demolition·relocation 허용
- 철거·이전 뒤 storage destination owner가 없는 Stored stack 존재
- live production/restock/rollback에서 category-only spawn으로 concrete item identity·mass 변경
- shop/virtual inventory 또는 equipment event가 explicit Transform/Sink 없이 physical mass를 흡수
- combat/apparel/carcass authored mass와 physical item/instance mass 불일치
- warehouse mass index가 stack/component/destination revision과 불일치
- capacity commit token 없이 warehouse destination에 Stored record 생성·route·transit 완료
- owner/capacity/dwell 계약이 없는 non-warehouse Stored destination
- production untyped physical consume/remove caller 존재
- disposition receipt의 input grams와 output+byproduct+terminal-sink+declared-process-loss grams 불일치
- expedition abstract loot/supply burden이 physical return grams 또는 carry cap과 연결되지 않음
- spoilage/butchery/in-place transform이 input 삭제 후 output spawn 실패 가능 상태를 노출
- Stored transform이 destination ownership을 잃거나 capacity를 조기 해제
- capacity shrink가 cargo delete/teleport 또는 무기한 hard-overload를 만듦
- cargo/equipped mass 이중계상·누락으로 locomotion/haul capacity가 서로 다른 물리량을 표시
- in-transit cargo grams와 destination reserved grams 불일치
- rescue/wildlife/captive entity transport에 item kg·hard cap·속도 페널티가 적용되거나, 전용 transport path/ownership/interruption 누락 때문에 emergency liveness를 잃음
- 한 actor가 entity transport와 신규 opportunistic haul을 동시에 소유
- consumed input에 WIP authority가 없거나 cancel/destroy/output failure에서 질량이 사라짐·복제됨
- catalyst/tool/container/infrastructure input disposition 누락 또는 이중계상
- output count·maxStack·capacity 변경 후 quantity consumer orphan 존재
- fluid grams/unit 또는 milliliters/unit 권위 누락
- stack merge/split으로 freshness가 증가하거나 quality/instance state 유실
- urgent 소량 또는 heavy order가 kg 변경 때문에 bounded latency를 넘겨 starvation
- current-format save/restore에서 carried/WIP/stack 질량·수량·상태 drift
- restore에서 pickup-committed inbound mass를 0회 또는 2회 예약
- current-format restore가 owner 없는/tampered warehouse Stored destination을 승인
- 승인 없는 asset 변경
- 예상 외 YAML churn
- production warehouse가 0/음수/implicit-unlimited mass capacity를 가짐
- source inventory의 stable ID 집합·개수·digest가 승인된 capture와 다르지만 원장을 계속 생성함
- production physical mutation/read callsite가 semantic manifest에 없거나 `unknown/compatibility/legacy` 상태임
- domain operation의 target command, source/destination capacity, commit, rollback 또는 save owner 중 하나가 비어 있음
- faction/captivity/defense/invasion/facility-evolution operation이 partial physical/domain commit을 노출
- commit 전에 gameplay event/UI를 발행하거나 subscriber 실패가 committed 물리 mutation을 반복시킴
- save participant predecessor가 누락·순환하거나 cross-section join 전에 AI가 활성화됨
- gameplay reader가 canonical mass query 대신 count capacity 또는 generic definition kg로 stateful lot을 판단함
- typed capacity/ownership failure가 localization 과정에서 generic success/실패 문자열로 소실됨
- authoring origin과 builder projection이 독립 writer로 동시에 수정되거나 clean build에서 1g 이상 drift
- fault matrix의 어느 phase에서든 prior-world fingerprint 또는 exact-once receipt가 깨짐

### 14.2 Warning

- ordinary recipe input `11~15kg`
- HeavyBatch `15~20kg`
- repeated haul p75 `14~19kg`
- unit mass가 분류 기준 밴드 밖이지만 명시적 사유 있음
- maxStackMass가 일반 actor 최대의 10배 이상
- 6kg 미만 반복 haul 비율이 높음
- item kg 변화율 100% 이상
- item kg 변화율 25~50%이며 결합 조정 후보가 아직 미확정
- payload/kg, WU/kg, EWU/kg, price/kg 변화 25~50%
- warehouse-stage reserve days·utilization, 실제 pile/cell occupancy 또는 maxStack 총질량 변화 25~50%
- haul trip count 50% 이상 증가
- 6인 물류 WU가 18~20% 경계
- storage utilization이 65~70% 경계
- 창고 kg capacity가 정상 70%·장애 90%·7일 비축 중 하나를 만족하지 못함
- operational buffer peak mass 또는 dwell time이 authored bound의 80% 이상
- warehouse mass query/index 갱신이 planner budget을 초과하거나 전체 repository scan을 actor 후보마다 반복
- compatible compaction 후 freshness·contamination·provenance·mass drift
- contract reward/kg 또는 expedition supply-days/kg 변화 25~50%
- Source land/water/feed/time 당 yield 변화 25~50%
- planner tour item count 또는 heavy-order age가 Before 대비 25~50% 증가

### 14.3 예외 승인 키

```text
itemId
unitSemanticFingerprint
exactAfterMassGrams
massDerivationFingerprint
haulClass
reasonCode
sourceDigest
balanceBaselineRecordId
```

wildcard 승인은 금지한다. kg, unit meaning, BOM, recipe 또는 source가 바뀌면 승인은 만료된다.

---

## 15. 에셋 적용과 builder 권위

### 15.1 AuditOnly 우선

첫 실행은 asset을 바꾸지 않는다.

1. item unit semantic 후보 생성
2. mass profile 후보 생성
3. recipe balance 계산
4. kg Before/After 원장 생성
5. Critical 리뷰
6. 승인된 행만 ApplyApproved

### 15.2 직접 asset만 바꾸지 않음

다음 builder도 같은 mass authority를 사용하도록 바꾼다.

- `ResearchOverhaulContentAssetBuilder.ResolveGeneratedUnitWeight`
- `UnifiedItemDefinitionAssetBuilder.RebuildEquipmentItemsOnly`
- `GameContentCatalogAssetBuilder.EnsurePhysicalOfferDefinitions`
- `V22ApparelContentAssetBuilder`
- combat equipment/apparel/material builder
- 그 밖에 `ConfigureCore(... weight ...)`를 호출하는 모든 builder

builder 산출물과 수동 asset을 별도로 패치하면 다음 rebuild에서 값이 되돌아가므로 금지한다.

### 15.3 YAML 노이즈 방지

- 값이 실제로 다른 SerializedProperty만 수정
- 기존 dirty asset은 자동 적용 중단
- changed asset만 SetDirty
- changed path ordinal 정렬
- changed path만 ForceReserialize
- meta/GUID/local FileID/m_Script 불변
- 두 번째 apply·reserialize diff 0
- 예기치 않은 churn은 `UNITY_YAML_UNEXPECTED_CHURN`

---

## 16. 구현 단계

### 16.1 Revision v6 실행 배치 개요

아래 Batch A~H가 current-source 구현의 실제 critical path다. 기존 Phase 0~9는 계약 색인으로 유지하되, 더 이상 Phase 2의 414개 semantic을 모두 수동 완료한 뒤 Phase 3으로 이동하는 식으로 해석하지 않는다. 자동 proposal과 독립 구조 batch는 병렬로 준비하고, Unity integration과 SO 적용만 의존 순서대로 직렬화한다.

Revision v6는 이 Batch 순서를 바꾸지 않고 모든 exit gate에 **확장 폐쇄 공통 조건**을 추가한다. 한 batch가 현재 정의만 대상으로 한 ID 분기·수동 allowlist·전용 save 필드로 통과하면 실패다. 해당 batch의 대표 synthetic canary가 기존 capability 조합만으로 자동 수집·실행·저장·감사되고 production core source diff가 `0`이어야 한다. 완전히 새 capability가 필요한 경우에도 새 구현·descriptor·공용 contract fixture만 추가하고 기존 코어 분기를 수정하지 않아야 한다.

```text
Batch A exact output closure ─→ Batch B output capacity ─────────────┐
                                                                    ├→ Batch G live fault matrix ─→ Batch H final balance
Batch C non-warehouse/entity P0 ─→ Batch F domain clusters ─────────┤
                                                                    │
Batch D authoring proposal ─────→ Batch E anomaly review/apply ─────┘
```

| batch | 목적 | 현재 시작점 | 주요 산출물 | exit gate |
|---|---|---|---|---|
| A | 표준·custom 생산 output exact closure | P17 hay-feed 정상 live 1경로, custom output 4경로 gram reservation 미완료 | 모든 output의 prepared vector, exact commit, acknowledgement, legacy bypass manifest | output producer remaining/bypass/orphan 0, normal·partial·retry exact |
| B | 공통 FacilityBuffer gram capacity | 일부 시설이 active bill 또는 count batch에 의존 | max reachable recipe branch 기반 2~4 cycle projector, all-capable restore, revoke lifecycle | 모든 생산 가능 시설 capacity 존재, P17 4,200g, 4,199g block/1g clearance, no reconsume/reroll |
| C | 비창고 경계·생물 운반 P0 | input owner 3/39, shop/expedition 일부 count, entity transport 권위 분산 | FacilityBuffer admission, shop/expedition custody, rescue/captive common lifecycle | input remaining 0, bypass/orphan 0, disable/downed/restore ownership leak 0 |
| D | 전수 mass proposal 자동 생성 | 414 items, 1,074 sites, 355 recipes inventory | family policy, before/after proposal, coupling graph, anomaly report | proposal row coverage 100%, deterministic second run, SO mutation 0 |
| E | anomaly-only 검토·승인 적용 | unresolved semantic 51, exact recipe contract 42/355 | semantic decisions, approved builder projection, changed-only asset patch | unresolved semantic 0, duplicate writer 0, second build/apply byte diff 0 |
| F | 도메인 cluster 이관 | 다수 structural checkpoints와 남은 live/terminal gap | 의료·원정·계약·시설·전투·농업·건설 cluster별 typed closure | 각 cluster producer/consumer/save/fault/receipt closure, legacy callsite 0 |
| G | 실제 AI·fault matrix | hay-feed normal live만 확보 | partial pickup, cancel, Downed, Dead, destroyed, output full, mid-haul restore, Floor Clutter evidence | 수량·gram·lease·WIP 보존, teleport/duplication/orphan 0, Console 0/0 |
| H | 최종 밸런스·결정론 | 아직 open | EWU·가격·6인 생존·공간·32/64 paired·256 layout·3 final seeds·artifacts | unresolved Critical 0, no-op diff 0, 최종 완료조건 전부 green |

Batch 공통 확장 지표:

```text
coreContentSpecificBranchCount = 0
manualCapabilityAllowlistCount = 0
unregisteredCapabilityCount = 0
syntheticCanaryCoreSourceDiff = 0
syntheticCanaryExecutionOrphanCount = 0
```

이 지표는 새 콘텐츠가 생길 때 분모를 수동으로 유지하라는 뜻이 아니다. catalog/semantic symbol/generator가 새 정의를 자동 발견하고, capability registry·dependency graph·원장·테스트 fixture의 입력 집합이 함께 증가해야 한다.

### 16.2 Batch A — exact prepared-output closure

목적은 “생산 결과가 생겼다”와 “물리 output이 destination custody에 exact하게 들어갔다” 사이의 모든 우회를 제거하는 것이다.

1. 표준 output producer와 의복·수술 부품 등 custom handler를 semantic symbol로 전수 열거한다.
2. completion-time resolved output vector를 operation/commit과 함께 WIP에 저장한다.
3. 각 output은 exact item/instance component, quantity, unit grams, total grams, destination, buffer reservation을 가진다.
4. physical publication, domain outcome, acknowledgement를 분리하고 어느 단계의 재시도도 두 번째 output을 만들지 않는다.
5. legacy `SpawnOutput`, category-only publication, count-only capacity, receipt 없는 aggregate advancement를 production에서 0으로 만든다.
6. P17 hay-feed 외에 probabilistic, multi-output, packaged/tare, equipment custom output을 대표 fixture로 추가한다.

병렬 가능: producer callsite manifest, custom handler audit, save join audit, focused fixture 작성. 직렬 필수: 공통 output coordinator·save schema·composition root 변경과 Unity 실행.

현재 구현 체크리스트 (2026-08-26 current source):

- [x] semantic handler registration까지 포함한 output owner census를 고쳐 standard 1 + custom 5 = `6`을 current-source 분모로 확정했다.
- [x] `355 recipes / 357 physical output lines / canonical 4 / missing 353`을 AuditOnly로 캡처하고 byte-identical 두 번째 capture와 SO mutation 0을 증명했다.
- [x] reviewed proposal의 `outputLineId 353개`와 source recipe의 probabilistic secondary output `Main→Byproduct 6개`만 적용했다. `347`개 changed asset, `SaveAssets 1`, 두 번째 Apply 변경/SaveAssets `0/0`, empty line ID `0`이다.
- [x] 출력 ID 적용 뒤 `ProductionEconomyDebugScenarios`와 `SurgeryDebugScenarios`가 PASS하고 Unity full compile 및 Console Warning/Error `0/0`을 확인했다.
- [x] 355 recipes / 357 physical output lines를 component 초기화 권위로 전수 분류했다. generic codec은 item ID allowlist가 아니라 `ProductionItemFeature / MarketItemFeature / ResearchGateItemFeature / FacilitySupplyItemFeature`로만 구성된 definition-only capability를 판정한다. freshness·packaged tare·apparel·surgical·durable state feature가 하나라도 있으면 별도 codec/handler 없이는 fail-loud하며, ID prefix만으로 state를 추측하지 않는 후속 교정은 계속 OPEN이다.
- [x] current migrated recipe 11개(feedbench 4개 + sawmill 1개 + charcoal/mill/steelworks/treated-lumber whole-workstation 6개)에 positive exact profile gate를 추가했다. recipe ID만이 아니라 process kind, facility tag, spoilage item과 모든 output의 line ID·role·item ID·quantity·probability가 exact 일치해야 standard prepared-output 실행과 최대 buffer projection에 들어간다. 같은 definition-only capability의 다른 item으로 바꾼 drift fixture도 fail-loud한다.
- [x] `production-recipe-semantic@2` 공용 digest를 추가했다. process/flow/class, facility/workstation/support/research, WU·숙련, passive timing·온도, 물·폐수·manual fallback, spoilage, exact input/output/probability를 명시적 순서와 UTF-8 byte-length prefix·IEEE754 bit token으로 SHA-256에 묶는다. 표시명·설명·asset path·Unity object ID는 제외하고 collection insertion 순서는 ordinal 정렬한다. current 11개의 digest ratchet, display-only 불변, input shuffle 불변, WU drift 변경, duplicate input fail-loud를 focused 회귀로 증명했다.
- [x] resolved prepared-output 복원 시 저장된 `recipeDefinitionDigest`를 current `production-recipe-semantic@2`와 publication 전에 exact 비교한다. mismatch는 재계산·재굴림·legacy fallback 없이 `prepared-output-source-revision-stale`로 거부하며 정상·digest 변조·동일 ID WU drift fixture가 PASS했다.
- [x] `resource-item-semantic@1`과 `production-prepared-output-component-profile@1`을 추가했다. current reviewed definition-only item 11개는 item ID·stock category·canonical grams·maxStack·unit price와 production/market/research/facility-supply feature를 명시적 토큰으로 묶고, component profile은 exact item digest와 canonical empty-component payload를 합성한다. restore는 live item kg·stack·price·feature drift를 `prepared-output-item-revision-stale`로 participant publication 전에 거부한다.
- [x] `production-prepared-output-migration-profile@2`/`registry@1`을 사용해 current profile의 recipe semantic topology를 SHA-256으로 고정한다. 최초 `@1` allowlist 기반 profile은 폐기됐고, `@2`는 current `ProductionRecipeSemanticDigest`를 직접 포함해 신규 capable recipe도 같은 source contract에 들어간다. prepared-output save는 `migrationProfileDigest`, `capacitySourceDigest`, output-buffer cycle 수, projected portfolio grams, required minimum grams를 필수 저장하며 profile/code/source drift는 typed stale failure로 거부한다. 과거 schema fallback 또는 migration은 추가하지 않는다.
- [x] `production-output-buffer-capacity-source@1` digest로 authored support maximum catalog·Grand Project exact factor·mass authority·recipe/item/component/migration digest·facility definition/instance/position·destination·cycle 수·projected/exact minimum grams를 batch/restore/reserve/publication에 결속했다. production owner request는 lowercase SHA-256과 positive minimum을 강제하고 profile이 minimum보다 작으면 거부한다. pure stale guard를 실제 adapter restore/resume 경로가 공유하며 시설 identity drift를 `prepared-output-capacity-source-stale`로 batch mutation 없이 거부한다. P17은 `4,200g`, P03 sawmill은 `14,400g`, charcoal kiln은 `3,600g`, mill은 reachable malt를 포함해 `2,800g`, steelworks는 `3,400g`, treated-lumber는 `9,200g`으로 결정론적 검증했고 schema-v3 tamper, admission fingerprint, isolated publication, restore join, Production Economy와 Console `0/0`을 fresh Unity compile에서 통과했다.
- [x] standard migration scope를 current positive 11개에서 component codec이 실제 지원 가능한 나머지 whole-workstation family와 custom handler family로 확장했다. current 355 recipes는 `standard prepared 267 + ammunition prepared 21 + workwear exact 60 + surgical exact 3 + no-physical-output sink 4`로 전수 분류되며 unsupported/mixed family는 `0/0`이다. ammunition은 `AmmunitionItemFeature` 기반 자동 capability와 전용 materializer를 사용하고 standard definition-only codec은 이를 거부한다. food freshness, packaged tare, apparel, surgical unique 등 stateful output을 generic payload로 가장하지 않는다.
- [x] `recipe:sawmill-lumber`의 real `ProductionPreparedOutputExecutionAdapter` focused 종단 fixture를 추가했다. 실제 P03/recipe/item/catalog/bridge/Items repository/admission/publication/routing을 조립해 `3 × 1,200g` resolve, schema-v3 batch JSON round-trip, `14,400g` current-source authority restore, physical FacilityOutputBuffer stack, non-stacking durable marker, routing/ack exact-once를 검증했다. canonical하지만 stale한 digest와 산술 일관된 `cycle=3 / 10,800g` payload는 `prepared-output-capacity-source-stale`로 거부되고 claim/profile/reservation/occupancy/physical stack revision이 모두 불변이었다. fresh Unity compile과 전체 Production Economy, Console `0/0`을 통과했다.
- [x] charcoal-kiln·mill·steelworks·treated-lumber의 reachable recipe family를 원자적으로 승격했다. mill은 처음 후보에서 빠졌던 `recipe:malt`와 `support:fine-sieve` reachable branch를 감사로 찾아 포함해 legacy bypass를 `0`으로 만들었고, 최대 batch를 flour `600g`가 아니라 malt `700g`으로 계산한다. six recipe의 canonical output/profile/component/recipe/item digest와 family membership을 current-source ratchet으로 고정했다.
- [x] P01/P03/P04/P08/RF16의 `physicalOutputBufferCycleCapacity: 4`를 C# initializer/fallback이 아닌 BuildingSO YAML에 명시했다. 다섯 asset의 두 번째 Unity import 전후 SHA-256 변화가 `0`이며 GUID/FileID/meta와 gameplay 수치는 바꾸지 않았다.
- [x] sawmill의 completed-unrouted current-format Production/Physical/Routing save graph를 public section registry로 새 aggregate에 재조립했다. Physical→Production→Routing dependency-ordered detached stage에서 stack/component/commit ID·source digest·`3,600g` route·`14,400g` capacity와 section JSON recapture identity를 증명했고 Completed replay의 추가 physical stack은 0이다.
- [x] special output handler 선택을 capability ID ordinal registry로 감쌌다. DI 열거 순서와 locale에 무관한 fingerprint를 생성하고 null·비canonical ID·중복 capability ID·비멱등 handler·동일 item 다중 claim을 fail-loud한다. `material:qa-definition-only-canary`는 기존 definition-only feature 조합만으로 codec create/decode를 통과하며 과거 11-item allowlist를 요구하지 않는다. Roslyn Economy/Main/Editor compile과 Unity registry/codec focused command, Console Warning/Error `0/0`을 확인했다.
- [x] legacy resolved-output owner를 Production current-format `V17→V18`로 올렸다. `outputLineId`를 item ID와 별도 영구 key로 보존하고 capability ID/version, component codec ID/version, per-output descriptor fingerprint를 resolve 시점에 동결한다. 일반 출력도 `production-output:standard-definition@1` 실제 handler로 등록했으며 produce→mass→ack와 restore validation은 저장된 exact descriptor만 조회한다. capability version 변조는 live aggregate publish 전에 원자 거부되고, post-commit crash 복원은 재생성 없이 ack만 재개한다. Production/Economy/Main/Editor compile, registry/표준 실제 생산/수술 handler focused bundle, Console `0/0`을 통과했다.
- [x] prepared output의 물리 line에도 capability ID/version, component codec ID/version, descriptor fingerprint를 동결했다. prepared schema `v4`가 resolve/ruin 시 descriptor를 캡처하고 restore 전에 exact registry validation을 수행하며, routing current-format `v5`와 Physical current-format `v18` capacity-routing destructive drain까지 같은 다섯 필드를 clone·fingerprint·교차 검증한다. production/routing/items 각 저장 경계는 공용 Foundation fingerprint 권위로 독립 재계산하고 version/codec/fingerprint drift를 live publication 전에 거부한다.
- [x] generic recipe의 item 기반 자동 handler 선택과 domain owner의 명시 capability 선택을 분리했다. `IProductionOutputCapability`가 stable capability/codec metadata와 item 계약을 소유하고 `IProductionOutputHandler`는 generic bill exact-once 실행 specialization으로 한정된다. Apparel Work Order, Combat Equipment, Combat Ammunition, Certified Seed를 명시 capability로 등록했으며 동일 apparel item claim은 자동 선택 overlap을 만들지 않는다. 명시 capability를 generic bill handler로 실행하려는 시도는 fail-loud한다.
- [x] Apparel Work Order owner를 공용 frozen descriptor envelope에 연결했다. `output:apparel-crafted-item`의 line/item/capability/version/codec/fingerprint는 component·unit mass·capacity source와 같은 physical adoption에서 동결되고, CharacterEnvironment V10·terminal V2·drain V2 복원과 replay에서 exact 검증된다.
- [x] CertifiedSeed owner를 current-format V4 공용 envelope와 common gram publication에 연결했다. exact input Transfer와 certified seed-lot state가 확정되는 전이에서 `output:certified-seed` descriptor를 동결하고, exact component/gram·capacity reservation·FacilityOutputBuffer planned publication·input/output acknowledgement를 공용 service로 실행한다. restore는 pending physical marker를 section stage에서 exact 채택해 `OutputRestoredAwaitingInputAcknowledgement`로 정규화한 뒤 marker를 한 번만 acknowledgement하며, 다음 tick은 소실된 transient admission token을 다시 요구하지 않고 input receipt만 idempotent하게 끝낸다. 시설 파괴에서는 Planned 배송을 실제 destination 위치에서 해제하고 InputCommitted WIP를 `DestroyedWithFacilityLoss` quantity/gram receipt로 종결하며, OutputPublished는 시설 가동 여부와 무관하게 acknowledgement 후 제거되어 destructive preflight가 무기한 Deferred되지 않는다.
- [x] Combat Equipment current format V9가 장비·탄약 outcome의 exact line/item/capability/version/codec/fingerprint를 `attemptOutcomeResolved` 전에 동결한다. finalize와 restore는 exact registry 검증을 요구한다. 기본 화살이 `GenericItemDefinitionSO`인 실제 권위에 맞춰 탄약 capability는 통합 `IItemDefinitionCatalog`와 전용 ammunition-state codec을 사용한다.
- [x] prepared batch의 component 생성·복원을 등록형 `IProductionPreparedOutputMaterializer`로 분리했다. capability가 `IProductionPreparedOutputParticipantCapability`로 참여를 선언하면 registry가 capability/version/codec과 materializer를 1:1 exact join하고, normal·ruined·restore가 같은 dispatch를 사용한다. 비표준 synthetic capability가 코어 ID 분기 없이 `PreparedBatch` 선택과 create/decode round-trip을 통과했다. 참여자 누락·중복·metadata drift·비참여 materializer·prepared/per-line 이중 실행 권위는 구성 시 fail-loud한다. handler/maximum registry schema는 각각 `@3/@2`, migration profile은 `@2`이며, participation 의미를 바꾸는 capability는 반드시 contract version도 올린다.
- [x] 실제 생산→FacilityBuffer→AIHaul→warehouse→save/restore→UI/AI→audit synthetic full-path canary를 통과한다. definition-only synthetic item/recipe가 기존 capability만으로 발견되어 `1 × 80g` 입력 WIP, `20 × 1,000g` 출력, exact `80,000g` FacilityOutputBuffer, `60,001g` blocker와 `1g` 부족 대기, 동일 prepared outcome 재개, AI pre-pickup cancel/replan, active cancel carried custody, mid-carry save/restore, exact `20,000g` warehouse admission, Brain 재개 배송, UI `20kg/25kg`, terminal bill retirement, 두 번째 restore 무복제를 모두 실제 Title→Preparation→임시 sanitized Gameplay 경로에서 통과했다. 변조된 carried/admission join은 whole-root mutation 없이 원자 거부됐고, transient visitor lease의 문서화된 restore-release만 semantic fingerprint에서 정규화했다. 보고서 `RESULT=PASS; failures=0`, 런타임 캡처 Warning/Error `0/0`, 공식 Gameplay scene SHA-256 유지, synthetic asset/scene cleanup 0을 확인했다. 이 행만 닫으며 normal sawmill fault, 실제 M06 surgical PlayMode, manifest/fault-matrix zero gate는 계속 OPEN이다. Batch A는 `28/31`이다.

#### Batch A frozen output capability 구조 계약

| 항목 | 단일 권위와 전환 계약 |
|---|---|
| 콘텐츠 정의 | `ProductionRecipeSO.Outputs`와 item catalog가 출력 item/수량의 불변 원본이고, 특수 출력 구현은 canonical capability ID와 positive contract version을 선언한다. |
| 런타임 상태 | `ProductionResolvedOutputSaveData`가 `outputLineId`를 영구 key로 보존하고 결과 resolve 시점의 capability ID/version, component codec ID/version, descriptor fingerprint를 소유한다. 같은 item을 내는 서로 다른 line을 item ID로 합치지 않는다. 일반 출력도 암묵적 handler 부재가 아니라 `production-output:standard-definition@1`과 `production-output-codec:definition-only@1`로 동결한다. |
| 명령 | `ProductionOutputExecutionService.ResolveAll`만 output line→capability 선택을 수행한다. produce/mass/ack는 저장된 exact descriptor만 bridge에 전달하며 item 전체 handler scan을 다시 수행하지 않는다. commit ID도 item이 아니라 canonical output line을 포함한다. |
| 조회 | `ProductionOutputHandlerRegistry`는 output line의 최초 선택과 exact `(capabilityId, version, codecId, codecVersion)` 조회를 분리한다. prepared 참여 여부는 contract snapshot에 포함되고, `ProductionPreparedOutputMaterializerRegistry`가 참여 capability와 materializer를 1:1 결합한다. exact 조회는 누락·version drift·codec drift·item claim drift·descriptor fingerprint drift를 typed fail-loud한다. |
| 식별자 | capability ID는 lowercase canonical stable ID, version은 positive integer다. 콘텐츠 item ID와 capability ID를 서로 대체하거나 prefix로 추론하지 않는다. |
| 저장 | Production current-format `V18` legacy resolved row와 prepared-output schema `v4`, routing current-format `v5`, Physical current-format `v18` destructive-drain source line이 같은 frozen dispatch 다섯 필드와 영구 `outputLineId`를 필수 저장·clone·검증한다. 과거 schema/필드 누락 fallback과 migration은 추가하지 않는다. |
| 의존성 | Production aggregate는 문자열 ID/version만 저장하고 handler/materializer 인스턴스를 저장하지 않는다. Economy composition root가 deterministic capability/materializer/maximum-mass registry와 표준 FacilityBuffer gateway를 조합한다. 신규 동일 의미 output은 capability·materializer·maximum-mass 구현과 등록만 추가하며 coordinator 분기를 수정하지 않는다. |
| 실패 정책 | 중복/비canonical capability, non-idempotent handler, prepared/per-line 이중 실행 권위, materializer 누락·중복·metadata drift, overlap, unknown ID, version mismatch, 저장 item claim drift는 물리 publication 전에 실패하며 다른 handler나 표준 출력으로 fallback하지 않는다. participation 의미 변경은 capability contract version 변경 없이 허용하지 않는다. |
| 전환 범위 | legacy 및 prepared standard/surgical/workwear 경로의 item-only 재탐색을 frozen descriptor exact lookup으로 교체했고 routing·destructive drain까지 provenance를 전파했다. Apparel Work Order·CertifiedSeed·Combat owner의 공용 publication과 저장·복원 연결은 완료됐다. full-path canary와 전수 remaining/bypass/orphan 0은 별도 OPEN 행으로 유지한다. |
| 검증 | save JSON round-trip, missing/version/item drift 원자 거부, registry insertion order/locale 결정론, standard/special exact dispatch와 crash replay를 focused fixture로 증명한다. |

2026-08-27 prepared/routing provenance fresh 증거: Unity current-source compile 뒤 `ProductionPreparedOutputContractDebugScenarios`, `ProductionPreparedOutputRoutingAuthorityDebugScenarios`, `ProductionCapacityRoutingDrainOutboxDebugScenarios`, `ProductionPreparedOutputRoutingRestoreJoinDebugScenarios`, `ProductionPreparedOutputFullPersistenceDebugScenarios`, `ProductionEconomyDebugScenarios.RunFrozenStandardOutputFocused`를 같은 clean-console 구간에서 실행해 모두 PASS했고 최종 Console Warning/Error는 `0/0`이다. exact descriptor fingerprint·capability version·codec version 변조, declared-loss provenance 오염, routing restore drift, destructive-drain request drift를 모두 원자 거부한다. 검증 강화 중 기존 capacity-drain fixture의 `sourceStackId`와 carried stack signature가 실제 exact-route custody component를 반영하지 않던 두 잠복 오류가 드러났으며, runtime validator를 완화하지 않고 실제 physical row와 동일한 source/signature로 fixture를 교정했다. world-removed full-persistence fixture도 acknowledged generic bill owner의 terminal producer·0/0 Items child·exact WIP terminal receipt를 추가해 upper owner와 하위 receipt를 양방향 증명한다. 전수 `RuntimeAuthorityV18Validator.ValidateOrThrow`는 생산 V7·save-section 68 등 이미 폐기된 과거 ratchet을 계속 강제해 별도 stale-validator 부채로 실패했으며, 이 결과를 Batch A 실패나 PASS로 재해석하지 않는다.

2026-08-27 declared domain capability fresh 증거: Unity clean-cache compile 뒤 `ProductionOutputHandlerRegistryDebugScenarios.RunAll`과 `ProductionEconomyDebugScenarios.RunFrozenStandardOutputFocused`가 PASS했고 clean-console Warning/Error는 `0/0`이다. registry는 자동/명시 capability overlap 격리, declared descriptor exact validation, generic bill 실행 거부, 순서·locale 결정론을 검증한다. 정상 scene boot는 새 capability 타입을 포함하지 않는 기존 `EquipmentMaintenancePolicyRuntime → Defense → BuildingDestructiveLossRuntime → ProductionOutputDestinationLifecycleQuery → CombatEquipmentCraftLifecycleContributor → EquipmentMaintenancePolicyRuntime` VContainer 순환에서 실패했으며 이 P0에 편입하지 않는다. Apparel owner는 이후 공용 envelope 저장·복원까지 닫혔다. Combat/CertifiedSeed의 frozen descriptor와 공용 gram admission/publication/acknowledgement는 계속 OPEN이다.

2026-08-27 Apparel frozen owner fresh 증거: `ApparelPhysicalTransactionDebugScenarios.RunAll`, `ApparelTerminalAuthorityDebugScenarios.RunFocused`, `ProductionOutputHandlerRegistryDebugScenarios.RunAll`을 clean-cache compile 뒤 같은 clean-console 구간에서 실행해 모두 PASS했고 Warning/Error는 `0/0`이다. capability version drift는 input/output mutation 전에 conflict로 거부되며 valid V10 JSON terminal authority는 exact round-trip한다. Unity `JsonUtility`가 선택적 null 영수증을 빈 객체로 복원하는 특성은 DTO clone의 all-default canonical-null 처리로 격리했다. 이 slice는 kg·BOM·WU·EWU·가격·SO·prefab·scene 값을 바꾸지 않았고 Combat/CertifiedSeed와 full-path canary를 대신하지 않는다.

2026-08-27 CertifiedSeed common publication fresh 증거: CertifiedSeed current format V4와 domain-output schema V2가 `output:certified-seed`의 frozen descriptor, exact seed component, output grams, capacity source, admission/publication identity, physical stack receipt와 durable acknowledgement 상태를 저장한다. `ProductionDomainOutputPublicationDebugScenarios`, `ProductionDomainOutputRestoreGuardDebugScenarios`, `ProductionFacilityDestructiveDrainStartPreflightDebugScenarios`, `CropPhysicalTransactionFixture.Run`을 같은 post-compile clean-console 구간에서 실행해 모두 PASS했다. exact restore adoption→marker acknowledgement→시설 없는 다음 tick input acknowledgement, duplicate acknowledgement, capacity wait, publication rollback, owner/batch 양방향 drift, InputCommitted 시설 파괴 typed gram loss를 검증했고 최종 Unity Console Warning/Error는 `0/0`이다. 이 증거는 CertifiedSeed family만 닫으며 Combat publication과 실제 AIHaul full-path canary를 대신하지 않는다.

- [x] 정상 부트 주문→작업 완료→FacilityOutputBuffer→AIHaul→kg warehouse와 cancel/Downed/mid-haul restore까지 통과한다. routed exact-outbox lifecycle과 live fault가 끝나기 전에는 sawmill live closure라고 보고하지 않는다.
- [x] Combat/Apparel/CertifiedSeed owner를 common prepared gram reservation/publication/acknowledgement와 exact restore join으로 이관한다.
- [x] Environmental workwear의 direct `SpawnItemAtWithComponents` 출력을 제거하고 공용 gram admission·planned publication·durable acknowledgement 경로로 이관한다. 실제 apparel component를 publication 전에 생성하고, 출력 단위마다 unique instance/slice를 동결하며, 같은 commit replay는 exact provenance를 재검증해 중복 생성 없이 성공한다.
- [x] surgical-part crafted output의 direct Loose 경로를 제거했다. exact unique component·gram reservation·planned publication·physical join·rollback/replay/ack를 전용 focused scenario로 검증했고 Unity full compile과 Console Warning/Error `0/0`을 통과했다.
- [x] surgical-part crafted output의 실제 주문→작업 완료→FacilityOutputBuffer→AIHaul→kg warehouse PlayMode 경로를 닫는다. focused 검증만으로 live 증거를 대신하지 않는다.
- [x] Batch A output owner manifest의 output remaining/bypass/orphan을 모두 0으로 유지하고 partial/cancel/Downed/restore output fault matrix를 통과한다. 2026-08-29 WorldResource exact-source owner를 포함한 fresh 분모는 output `10/10 migrated`, remaining/bypass/orphan/unclassified `0/0/0/0`이다. 공용 partial/split/rollback/replay와 기존 synthetic/sawmill/M06 live 증거에 WorldResource source debit 1회, released acknowledgement→authority retirement forward retry, topology rebuild 실패 원자성, frozen root-seed restore cross-join, transient save-block, same-outcome retry와 0-output exact cycle을 결합했다. closure/manifest/fault artifact를 두 번 실행해 hash·length·mtime 변화 0과 Console Warning/Error `0/0`을 확인했다. input `8/39`, remaining `31`은 Batch C 분모다. 이 행으로 strict Batch A는 다시 `31/31`이다.

2026-08-27 Environmental workwear fresh 증거: `EnvironmentalWorkwearPlannedOutputDebugScenarios`와 공용 `FacilityBufferPlannedOutputPublicationDebugScenarios`를 source보다 최신인 `Assembly-CSharp-Editor.dll`에서 실행해 PASS했다. `2 × 1,150g` exact mass, 두 unique apparel instance의 component-at-creation, gram capacity reservation, batch atomic publication, publication 직후 durable acknowledgement, 9일 뒤 동일 commit replay 무복제, outcome fingerprint drift 거부, destination mismatch 무변경, 1,000g capacity 부족 무publication을 검증했고 Console Warning/Error는 `0/0`이다. owner manifest는 output `5/6 migrated`, remaining `1`로 재생성됐으며 CSV `33F10F9D2E65BCA9979E9DC96252C4E515144C251A2A969CA1054B0C548FB0B5`, report `D6EA3359C2B1FBFFFB286274AA5C90D182F422740BFB6BC1F48CFC9051F52FEF`가 두 번째 실행에서 byte hash와 mtime 모두 변하지 않았다. 이 증거는 작업복 output family만 닫으며 generic recipe family, 실제 AIHaul full-path, input/bypass/orphan과 Batch A 전체를 대신하지 않는다.

2026-08-28 whole output family closure fresh 증거: current catalog의 355 recipe를 매 실행 전수 분류하는 durable ratchet을 추가했다. 결과는 `standard prepared 267 / ammunition prepared 21 / workwear exact 60 / surgical exact 3 / no-physical-output sink 4 / unsupported 0 / mixed 0`이며 합계는 exact `355`다. 21개 탄약 recipe와 7개 workstation은 `CombatAmmunitionCraftOutputCapability@2`와 `CombatAmmunitionPreparedOutputMaterializer`를 통해 frozen descriptor, `ammunitionKindId` semantic digest, component payload와 definition mass를 동일한 prepared route로 round-trip한다. standard codec의 탄약 수용, 중복 family claim, 미지원 family, prepared/exact mixed recipe는 fail-loud한다. current-source Unity refresh 뒤 `ProductionAmmunitionPreparedOutputDebugScenarios.RunAll`과 전체 `ProductionEconomyDebugScenarios.RunAll`이 PASS했고 Console Warning/Error는 `0/0`이다. owner manifest를 두 번 재생성한 CSV/TXT의 SHA-256은 각각 `6A30E70615BAA771F75726DBDE52AC6D2C3892BA89509CAC65D51358D8431C07` / `DDCFC78245E630C8DAFE4C7E866717FFF5F234B51365B5CDAB2ECDCB0B02DC25`이며 byte length와 mtime도 두 번째 실행에서 변하지 않았다. 이 증거로 구조 범위 행만 닫고 실제 Gameplay AIHaul canary, normal/fault PlayMode와 manifest input remaining/bypass gate는 OPEN으로 유지한다. Batch A 현재 체크리스트는 `27 closed / 4 open / 31 total`이다.

2026-08-28 input/transport manifest accounting fresh 증거: `research.arcane-index`는 `ResearchWorkExecutionAdapter`의 legacy delivery 호출 0, durable slot reconcile/ensure/use, 정책 등록과 current-format lifecycle 검증이 이미 연결된 current source에 맞춰 `migrated`로 승격했다. conveyor는 `ConveyorItemGateway`가 직접 FacilityBuffer를 쓰는 owner가 아니라 `ItemTransferService.TryCompleteTransitToFacilityBuffer`의 arrival-time exact claim/profile/admission을 위임하고 실패 시 InTransit custody를 보존하므로 `transport-delegated-exact`로 별도 분류했다. fresh Unity compile 뒤 `ResearchArcaneIndexEquipmentDebugScenarios`, `ResearchDurableEquipmentLifecycleDebugScenarios`, `PhysicalStockQueryV18DebugScenarios`, `IndustrialInfrastructureDebugScenarios`, manifest capture가 모두 PASS했고 Console Warning/Error는 `0/0`이다. 새 CSV/TXT SHA-256은 각각 `7385D7C509E6F26ACAA6253B947A88B479662E43AF4E518D4EF4911581A9063C` / `B6CD04B8B7F78827CB25CBFA8AFB5D550FA2605D75500B9CF8FE215D583BBFE5`다. 이 교정은 remaining을 `32→31`, bypass를 `4→3`으로 줄였지만 checklist 2745의 zero gate를 닫지는 않는다.

### 16.3 Batch B — max-branch FacilityBuffer capacity

물리 output buffer capacity는 현재 선택된 bill 또는 legacy `defaultBatchCapacity`가 아니라 해당 시설이 도달 가능한 레시피의 최악 branch를 기준으로 한다.

```text
maxCycleOutputGrams(facility)
= max(
    Σ branch output quantity × exact projected unit grams
    for every reachable recipe and branch)

physicalOutputBufferCapacityGrams
= max(
    2 × maxCycleOutputGrams,
    p95 haul-clearance 동안의 생산량)

physicalOutputBufferCapacityGrams
<= 4 × maxCycleOutputGrams
```

- `physicalOutputBufferCycleCapacity`를 legacy count batch와 별도 필드로 둔다.
- current active bill이 없는 복원 상태에서도 모든 capable facility에 projection한다.
- recipe/facility/source digest가 바뀌면 capacity revision을 갱신하고 stale reservation을 typed reject한다.
- 시설 철거·변환·기능 상실은 실제 production `TryRevoke`를 호출하며 reserved/committed output이 있으면 종료하지 않는다.
- P17은 hay-feed current bill의 `588g × 4 = 2,352g`이 아니라 reachable dog-feed branch `1,050g × 4 = 4,200g`을 요구한다.
- 4회분을 넘겨야만 통과하는 시설은 capacity를 계속 늘리지 않고 물류·처리량 Critical로 올린다.

현재 구현 체크리스트 (2026-08-26 current source):

- [x] legacy count와 별도인 `physicalOutputBufferCycleCapacity [2,4]` 권위를 추가하고 P17, V22 14개와 P01/P03/P04/P08/RF16 asset, 4개 builder에 `4`를 명시했다.
- [x] production workstation의 live capture와 current-format restore가 같은 명시적 buffer ability를 요구한다. buffer authoring이 빠진 workstation을 live에서 묵시적으로 `4`회분 취급하던 fallback을 제거했고, non-workstation support handle만 production facility 계약의 최소 표현값을 유지한다. 기존 Economy fixture의 세 누락 workstation은 명시적 `4`회분 권위로 교정했으며 전체 `ProductionEconomyDebugScenarios`와 focused output-authority bundle, Console Warning/Error `0/0`을 통과했다.
- [x] P17 migrated recipe 4개의 base branch를 전수 투영해 `1,050/1,050/588/690g`, ruin `600g`, exact `4,200g`을 산출했다.
- [x] restore가 occupancy/current bill을 이유로 4회분보다 capacity를 키우지 않으며, non-capable physical output과 `occupancy > exact projection`을 fail-loud한다.
- [x] focused admission에서 `3,151+1,050=4,201g`과 `4,199+2=4,201g`을 차단하고 각각 정확한 1g clearance 후 성공을 증명했다.
- [x] active bill을 만들기 전 신규 P17 placement/topology에서 `4,200g` profile이 즉시 게시되고, 이후 actual hay cycle에서도 exact `588g` publication, Loose custody, AIHaul, kg warehouse `3/3`, inbound `0g`, Console `0/0`을 증명했다. 당시 fresh live report SHA-256은 `B32C1ABF3FF0D6C9BD6D6A96A863F25AD2668131ADA875F0EDDB57AB08C8413B`였다. 이후 직접 GameplayScene을 연 재실행이 부트스트랩 DI 누락으로 같은 보고서를 FAIL로 덮어썼으므로, 이 해시는 과거 통과 증거로만 보존하고 current-revision live 증거로 재사용하지 않는다.
- [x] 현재 설치 상태와 무관한 authored support/Grand Project maximum multiplier 권위를 만든다. required support는 canonical provider/tag bitset DP로 계산해 한 provider가 여러 tag를 덮어도 multiplier를 한 번만 적용하고, 서로 다른 provider 조합의 exact rational 최대를 선택한다. batch support는 종류·도달성만 검증하고 output multiplier에는 넣지 않는다. 63 tag/65,536 state 상한을 넘으면 fail-loud한다.
- [x] 과거 4-recipe migration allowlist를 제거하고 current generic physical-output `329 recipe / 88 workstation facility`를 workstation tag와 capability-owned maximum projection으로 전수 preprojection한다. `recipe:surgery:*` 3개는 Surgical capability, `source:*` 19개는 facility가 아닌 Source authority이므로 generic 분모와 분리한다.
- [x] 실행 handler와 분리된 정렬형 `IProductionFacilityOutputCapacityContributor` registry를 추가하고 CertifiedSeed를 첫 소비자로 연결했다. numeric `building:8893` 복제 대신 `workstation:v19:cultivar-breeding + valid [2,4] cycle buffer` predicate를 command/presentation/contributor가 공유한다. actual RF93의 current 12 crop branch 최대는 `50g/cycle`, contributor 4-cycle `200g`; recipe와 contributor 중 큰 상한을 선택한다.
- [x] Apparel/Combat/Surgical/Crop domain의 all-capable no-bill facility preprojection을 같은 contributor registry로 연결하고, 현재 producer-capable 시설 union `92`와 capability-shaped nonproducer `54`를 전수 분류해 unclassified/orphan `0`을 만든다. current-source census는 `146/92/54`, unclassified/orphan `0/0`, 명시적 content gap `6`을 분리해 PASS했다. 이전 `146/90/56`은 crop contributor 연결 전의 superseded 증거다.
  - [x] ApparelTailoring command·exact workstation tag·`[2,4]` cycle buffer 적격성을 UI/command/restore/contributor가 공유한다. current apparel `56`, textile material `12`, tag-compatible pair `598` 중 실제 질량보존을 만족하는 recovery `493`과 craft `56`, 총 `549` alternative branch를 사전 투영한다. maximum은 `1,380g/cycle`, authored 4-cycle `5,520g`이다.
  - [x] Surgical M06은 별도 no-bill producer가 아니라 일반 recipe-backed preprojection임을 전수 확인했다. 실제 reachable recipe는 arm/leg/eye 3개뿐이며 generic projector가 active bill 없이 `1,800g/cycle × 3 = 5,400g`을 이미 투영한다. capability가 해석할 수 있지만 recipe가 없는 14개 보철을 허위 contributor branch로 추가하지 않는다.
  - [x] Combat primary craft, rejected recovery와 facility allowlist를 공용 catalog/eligibility로 연결한다.
    - [x] S08 canonical nonempty exact allowlist, 61 equipment + 2 ammunition craft definition, arrow/bolt output·BOM과 primary contributor를 하나의 immutable catalog/eligibility로 연결했다. primary maximum은 powered harness `18,000g/cycle`, 4-cycle `72,000g`이다. queue는 facility allowlist 밖 definition을 input reservation 전에 거부하고 worker/UI는 malformed/empty wildcard를 허용하지 않는다.
    - [x] quality-rejected auto-dismantle의 다중 recovery output을 shared pure maximum projector로 연결하고 ammo 2종을 UI에서 실제 queue 가능하게 만든다.
      - 구현 전 구조 계약: 콘텐츠 정의 권위는 `ICombatEquipmentCatalog + IResourceEconomyContentCatalog.Materials + IMaterialEconomicProfileCatalog + GameplayEffectResultBoundsCatalog + IPhysicalItemMassQuery`이고, 가변 상태 권위는 기존 `CombatEquipmentCraftOrderSaveData`의 frozen rejected instance/stack와 recovery vector다. 상태 변경 명령은 기존 `TryResolveRejectedEquipmentDismantle`만 유지하며 contributor는 definition-only 조회만 수행한다.
      - 실제 projection은 rejected unique stack의 exact item/component subject와 weighted skill·실제 salvage multiplier를 사용하고, definition maximum은 fresh equipment definition mass·skill 100·authored finite effect maximum을 사용한다. 두 경로는 동일한 정수 vector clamp를 공유하며 `Σ recovery grams <= consumed equipment grams`를 강제한다.
      - recovery output line ID는 canonical item ID에서 결정론적으로 만들고 output은 item ID ordinal로 동결한다. unknown/zero mass, noncanonical ID, duplicate input/output, overflow, source fingerprint drift와 disposition receipt mass 불일치는 fallback 없이 publication 전에 실패한다.
      - 저장 DTO를 질량 Query 입력으로 사용하지 않는다. 처음 생성된 exact vector와 source digest를 current order에 저장하고 restore/retry는 재계산·재굴림하지 않는다. 기존 순차 exact outbox는 각 line의 idempotent commit과 `spawnedRecoveryAmounts`를 유지하며, 중간 실패 뒤 동일 vector를 resume한다.
      - dependency 방향은 Combat runtime/contributor → pure recovery projector → existing Economy/Items read authorities다. ScriptableObject, scene, prefab, item kg, BOM, WU, EWU와 가격은 이 구조 교정에서 변경하지 않는다.
      - 전환 완료 증거는 actual `61 equipment / 252 recovery policy branch`, synthetic canary, current effect maximum `10`, powered-harness raw/clamped vector, shuffle/digest identity, runtime frozen-vector restore/retry, primary/recovery alternative parity, `18,000g` primary winner와 `72,000g` S08 profile, Unity compile·focused/full Production Economy·Console `0/0`이다.
  - [x] `146 facility / 92 producer / 54 nonproducer`, unclassified/orphan `0/0` 전수 집계를 fresh source에서 닫았다. crop contributor가 recipe 없이 실제 harvest를 수행하는 기본 밭과 수경재배 시설 2개를 producer로 연결했기 때문에 이전 `90/56`에서 정확히 `+2/-2`가 됐다. 결정론적 report/CSV SHA-256은 각각 `1914B1ACBB7947F319384D601130C04B055D6B1E49901313C5D1F7A5A1034666` / `B5CBFA90F6F1A270C223BC60F362E05E33EE1BC3A1F861259381E87B88264DB3`이고, 연속 두 실행의 hash·length·mtime 변화는 `0`이다. content gap `6`은 `unclassified`가 아니라 typed `DeclaredNoOutput`으로 별도 보존한다.
- [x] `production-output-buffer-capacity-source@4`로 recipe/facility/mass/component/migration/support/factor/destination/cycle/contributor registry와 branch winner/projected minimum을 exact digest에 결속하고 schema-v3 restore/resume에서 stale profile/token을 publication 전 거부한다.
- [x] active bill 없는 신규 placement/topology change에서도 즉시 profile을 게시·재조정한다. `BuildingVersion` 변화가 no-bill capable facility 전체의 exact projected set replacement를 유발하며 stale pair는 원자적으로 retire한다.
- [x] demolition·structural loss·cover loss에 reserved·committed·physical-aware 6-participant durable terminal drain, journal, mutation admission fence와 save registration을 연결한다.
- [ ] evolution·relocation·synthesis에 persistent facility ID 보존과 reserved·committed·physical-aware reversible retarget transaction을 연결한다.
- [x] generic no-bill restore와 recipe/provider 입력 역순 shuffle이 같은 source digest·capacity를 만드는 focused 증거를 닫고, current P17 live에서 no-bill `4,200g`과 save/second restore를 확인했다.
- [ ] support attach/detach의 원자적 재투영, p95 haul-clearance 입력, required cycle `4.000` 통과 / `4.001` 이상 typed `PRODUCTION_OUTPUT_CLEARANCE_EXCEEDS_FOUR_CYCLES` Critical을 focused/PlayMode로 닫는다. 4회를 넘는 경우 capacity를 확대하지 않는다.

2026-08-29 clearance 순수 경계 구현 증거: content-ID와 Unity 오브젝트에 의존하지 않는 `IProductionOutputClearanceProfileSource`, fixed-point `ProductionOutputClearanceProfileSnapshot`과 `ProductionOutputClearanceRequirementProjector`를 추가했다. 계산은 `ceil(p95 milli-hours × peak grams/hour ÷ 1000)`, 최소 `2.000`회분, 절대 상한 `4.000`회분이며, 요구량 `4.001`회분은 published capacity를 `4.000`에 유지한 채 exact typed Critical `PRODUCTION_OUTPUT_CLEARANCE_EXCEEDS_FOUR_CYCLES`을 반환한다. p95 또는 peak rate `0`, 비canonical SHA-256은 fail-loud한다. Runtime/Editor direct compile과 current `Assembly-CSharp.dll`을 직접 호출한 focused harness는 `4.000 accepted / 4.001 critical / ceil / zero measurement reject / lowercase digest`를 PASS했다. 결정론적 evidence는 `Artifacts/QA/v27-production-output-clearance-requirement-focused.txt`, SHA-256 `F4CF2976CFBE5E3AB46A8F21B18B2E7643A5E7B726BA8ECC5F711922522B8528`이다. 실제 profile source 구현·DI, `ProductionOutputBufferCapacityProjector` 결합, Critical consumer, support link/profile 동시 transaction과 PlayMode는 계속 OPEN이므로 이 행은 닫지 않는다.

2026-08-29 clearance raw 계측 경계 구현 증거: 실제 `FacilityBufferPlannedOutputPublicationService.TryPublishFullBatch`의 신규 물리 publication과 `WorldItemStackRuntime.TryCommitHaulPickup`의 성공한 committed pickup을 `batchCommitId`와 exact gram으로 연결하는 diagnostics-only telemetry를 추가했다. 부분 pickup은 원래 publication gram 전량이 인수된 시점에만 한 표본으로 닫히며 physical publication rollback은 candidate를 제거한다. raw 시각은 `GameCalendarRules.SecondsPerDay/HoursPerDay`에서 파생한 micro-game-hour로 보존하고, 두 절대 시각을 각각 milli-hour로 자르지 않으며 elapsed에 정확히 한 번 conservative Ceil을 적용한다. player runtime은 기본 no-op이고 명시적 audit session에서만 bounded `active 4,096 / completed 16,384` 표본을 수집한다. 측정 중 save capture는 fail-loud하며 restore 개입, conflicting publication, orphan pickup, over-pickup, 미완료 active batch는 session을 unclean으로 만든다. `DungeonStory.Items`/Runtime/Editor focused direct compile과 harness가 `fixed-clock / disabled-no-op / partial exact / rollback / conflict / orphan / over-pickup / save-restore invalidation`을 PASS했고 evidence는 `Artifacts/QA/v27-production-output-clearance-telemetry-focused.txt`다. 하지만 자연 `AIBrain→AIHaul` clean 다중-seed 측정, 결정론적 CSV와 nearest-rank p95, support work-speed를 포함한 throughput envelope, immutable profile catalog/DI, whole-cycle capacity·restore digest, support transaction과 full PlayMode는 아직 OPEN이므로 상위 체크박스는 닫지 않는다.

2026-08-29 clearance 불변 profile 기반 구현 증거: raw micro-hour 표본을 `(facilityDefinitionId, workstationTag)`별로 ordinal 정렬하고 `rank = ceil(95 × N ÷ 100)` nearest-rank p95를 선택한 뒤 선택된 단일 표본에만 milli-hour Ceil을 적용하는 `ProductionOutputClearanceProfileAggregator`와 fail-loud `ProductionOutputClearanceProfileCatalog`를 추가했다. 기본 자격은 서로 다른 deterministic seed `32`개이며 duplicate sample/profile/throughput, missing/orphan throughput, seed 부족은 모두 거부한다. profile key에는 instance ID·좌표·현재 연결 support를 넣지 않고 authored-reachable support/work-speed throughput digest를 별도 provenance로 결속하여 미래 시설 인스턴스나 배치 추가에 코드 분기 없이 데이터 행만 추가하도록 했다. 또한 기존 순수 경계의 부분 회차 결함을 교정해 `2.001회분`은 2,001g이 아니라 `3회분` 전체를 게시하고, `4.001회분`은 required whole cycles `5`, published whole cycles `4`인 typed Critical로 유지한다. 순수 authored gate는 실제 권위가 2회인데 측정 요구가 3회면 `PRODUCTION_OUTPUT_CLEARANCE_AUTHORED_CAPACITY_UNDERSIZED` Critical을 반환하고, 실제 권위가 4회인데 요구가 2회인 경우 4회를 축소하지 않는다. Runtime/Editor direct compile과 독립 실행이 `nearest-rank p95 / input shuffle / missing·duplicate·orphan / 32-seed gate / digest drift / 2.001→3 / 4.001→Critical+4 / authored undersize / preserve larger`를 PASS했고 evidence는 `Artifacts/QA/v27-production-output-clearance-profile-catalog-focused.txt`다. 다만 이는 합성 표본으로 순수 집계·lookup·산술을 증명한 것이며 실제 p95 수치 권위가 아니다. 자연 `AIBrain→AIHaul` 32-seed raw/profile/report, capture-start topology attribution, 모든 producer의 authored-reachable peak envelope, 완전한 row provider/DI, capacity source `@5`, authored undersize·restore drift 및 원자적 support attach/detach가 통과하기 전에는 상위 체크박스를 닫지 않는다.

2026-08-30 자연 `AIBrain→AIHaul` 32-seed 하위 경계 완료 증거: 정상 부트의 임시 sanitized GameplayScene에서 P17 hay-feed의 실제 `588g` FacilityBuffer publication부터 scheduler-owned AIHaul committed pickup까지 seed `157181..157212`를 공통 체크포인트 복원→reseed로 측정했다. 32개 행은 distinct seed, exact owner/facility/batch, stable authored topology, clean telemetry, checkpoint whole-root recapture를 모두 통과했고 body-health provisional key 생성은 `none`이다. CSV를 메모리에서 두 번 직렬화한 결과 `15,922` bytes가 byte-identical했으며 captured Console Warning/Error는 `0/0`이다. report SHA-256은 `DA1A37AD321CC2CC852CC17FB1F884036174730728FFA03352E23951C11B64A6`, CSV SHA-256은 `484A22F63B68262CC87B2055A923DF87731B868F2F1F57EA0DF95B987E72DC26`이고 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다. 복원 중 발견된 야생동물 respawn float ULP 단차는 내부 deadline/restore candidate를 `double`, 저장 DTO를 기존 `float`로 유지해 손실 없이 닫았으며 종별 save 행을 ordinal 정렬했다. 이로써 자연 raw/report 하위 경계만 닫는다. 모든 producer의 authored-reachable throughput envelope, 실제 profile aggregation/row provider/DI, capacity source `@5`, support attach/detach 원자 재투영과 `4.000/4.001` live gate가 남아 있으므로 2808의 상위 체크박스는 계속 OPEN이다.

2026-08-30 finite lane/support 작성 권위 하위 경계 완료 증거: 모든 raw workstation definition `146`개에 명시적 lane policy를 기록했다. 현재 authored topology는 `manual-only + detached BatchProcessor 123`, `AutomationMode` 상호 배타 manual/automatic `23`, 수동 lane `1` 전수, 자동 lane `0/1 = 123/23`이다. support `28`개는 동일 support definition 기준 workstation당 최대 연결 인스턴스 `1`, BatchProcessor `5`개는 각 batch capacity `1`을 가진다. `ProductionWorkshopRuntime`은 support instance를 stable ID 순서로 처리하고 거리·stable ID 순으로 capacity가 남은 workstation에만 연결하며 초과 인스턴스는 unlinked로 둔다. maximum factor catalog를 `@4`, recipe source를 `@4`, support profile/assignment를 `@2`로 올리고 kind·batch capacity·maximum linked instances·work-speed/output factor를 digest에 동결했다. 첫 deterministic asset patch는 정확히 `174`개 SO만 변경했고 두 번째 실행은 `changed=0`; changed path의 두 번째 ForceReserialize byte 변화도 `0`이다. Unity compile, Production Workshop/Maximum Factor focused suites, Console Warning/Error `0/0`, 공식 GameplayScene SHA-256 불변을 확인했다. 증거는 `Artifacts/QA/v27-production-finite-lane-authority-focused.txt`, SHA-256 `A716E4D741D2F35D72A9E4B68FEC81B8959B65FD24AD8A06E3D05230D79F04BE`다. kg·BOM·WU·EWU·가격 변경은 `0`이다. 다만 producer `92`개 전체 throughput envelope, profile row provider/DI, capacity source `@5`, capacity claim/profile 원자 재투영 및 live `4.000/4.001` gate는 계속 OPEN이므로 2808의 상위 체크박스는 닫지 않는다.

2026-08-30 lane provenance census 하위 경계 완료 증거: `ProductionFacilityWorkstationLaneCapacityProfile@1`을 live `ProductionFacilityHandle`, save-side `ProductionFacilityCapacitySubject`, `ProductionOutputBufferCapacityProjector`와 census `@2`까지 전달하고, Capture 이후에는 raw `BuildingSO` lane 필드를 다시 읽지 않도록 immutable provenance를 동결했다. 현재 시설 `146`개는 `manual-only 123 / mode-exclusive manual-or-automatic 23`, unclassified `0`, execution orphan `0`이며 census source digest는 `1f3cf6e5d8153a5fd43186071cd4839ba16788020d39bf3ddd6a87fe86c7c0a6`이다. Unity compile, census/contributor/maximum-factor/clearance-profile focused suites와 Console Warning/Error `0/0`을 통과했고, report/CSV 두 번째 생성의 길이·mtime·byte가 모두 불변이었다. report SHA-256은 `B8D720F222919D0094A644A23E8E20CA465E9BC3D87D5FE7853D04E37417ED34`, CSV SHA-256은 `1F66C0C36A5DD6D961806AFFF7A9B67F947CFD53286FEF93F4AE8373D20F1E50`, 통합 증거는 `Artifacts/QA/v27-production-lane-provenance-census-focused.txt`다. 공식 GameplayScene SHA-256은 불변이며 kg·BOM·WU·EWU·가격 변경은 `0`이다. 이 하위 경계는 작성값 전달만 증명하고 수동/자동 실행 배타성이나 peak gram/hour를 증명하지 않으므로 parent support 행과 Batch B는 `36/40`으로 유지한다.

finite lane/support 구조 계약:

| 항목 | 권위·계약 |
|---|---|
| 콘텐츠 정의 | `BuildingProductionWorkstationAbility`가 lane policy와 manual/automatic count를, `BuildingProductionSupportAbility`가 batch capacity와 workstation당 동일 support definition 연결 상한을 소유한다. |
| 런타임 상태 | `ProductionWorkshopRuntime`의 link projection만 가변 파생 상태다. authored 수치를 복제하거나 수정하지 않는다. |
| 명령 | 건물 배치·철거·이전·방/grid revision이 기존 world mutation 명령을 통해 link 재계산을 유발한다. lane 전용 gameplay setter는 만들지 않는다. |
| 조회 | 생산 실행기와 capacity projector는 유효 ability 및 immutable maximum-factor/throughput snapshot만 읽는다. Editor·UI가 raw 필드를 별도 권위로 캐시하지 않는다. |
| 식별자 | workstation tag, support ID, feature tag와 building persistent ID는 canonical ordinal 비교를 사용한다. content numeric ID·경로·이름 분기는 금지한다. |
| 저장 | lane/link cap은 SO 원본이고 runtime link는 저장하지 않는다. 복원 뒤 building/room/grid authority에서 동일 순서로 재계산한다. |
| 의존성 | Building authoring → Workshop runtime → Production cycle/output consumers → maximum catalog → throughput/capacity projection 단방향이다. Items·save DTO가 authoring을 역참조하지 않는다. |
| 실패 정책 | unspecified policy, invalid lane/link/batch 수, nonfinite multiplier, duplicate support ID와 missing provider는 fallback 없이 typed/exception fail-loud한다. 초과 support instance는 unlinked 상태로 남고 처리량에 포함되지 않는다. |
| 전환 범위 | 모든 current workstation/support asset과 재생성 builder를 같은 변경에서 갱신한다. `FacilityData.capacity`, `requiredWorkers`, raw hash enumeration을 lane 대체 권위로 사용하지 않는다. |
| 검증 | 전수 raw/valid census, duplicate support spill, asset second-run no-op, catalog digest drift, runtime compile/focused suite를 요구한다. parent row는 throughput/profile/DI/live gate까지 통과해야 닫힌다. |

runtime manual/automatic mode-exclusion 구조 계약:

| 항목 | 권위·계약 |
|---|---|
| 콘텐츠 정의 | `ProductionFacilityWorkstationLaneCapacityProfile`의 policy와 manual/automatic lane count가 실행 가능 lane의 불변 정의다. 신규 SO 필드나 content-ID 분기를 추가하지 않는다. |
| 런타임 상태 | `DungeonRuntimeAggregateRootStore` 안의 기존 `AutomationAggregateState`와 `AutomationStateSession`이 facility별 현재 `AutomationMode`의 유일한 쓰기 권위다. |
| 명령 | 기존 `IAutomationInfrastructureCommand.SetMode`만 mode를 변경한다. 생산 작업 시작/진행은 mode를 변경하지 않고 현재 mode와 authored lane을 검증한다. |
| 조회 | `IAutomationExecutionModeQuery`가 `BuildingInstanceId`로 현재 mode를 읽으며, 기존 `AutomationPowerDemandRegistry`가 같은 root state에서 구현한다. 별도 cache나 복제 dictionary를 만들지 않는다. |
| 실행 배타성 | `Manual`·`PoweredAssist`에서는 actor가 있는 manual 실행만 허용하고 automatic executor를 거부한다. `Automatic`에서는 authored automatic lane이 있는 mode-exclusive 시설의 null-worker automatic 실행만 허용하고 manual actor를 거부한다. manual-only profile은 모든 automatic 실행을 거부한다. |
| 식별자 | runtime object·이름·좌표가 아니라 canonical `BuildingInstanceId`와 frozen lane profile을 사용한다. |
| 저장 | 신규 저장 필드는 없다. 기존 automation save/restore가 aggregate root를 교체하면 query가 즉시 같은 mode를 읽는다. 파생 admission 결과는 저장하지 않는다. |
| 의존성 | Automation state/model → root-backed mode query → Economy scene facade → existing production core 순서다. `AutomationRuntime`은 facade를 소비하지만 facade는 runtime이 아니라 root-only query 구현을 소비하므로 DI cycle이 없다. |
| 실패 정책 | missing/unspecified lane, 실행 주체와 mode 불일치, authored automatic lane 부재는 `ProductionBillUnavailable`과 stable reason으로 fail-loud한다. 다른 bill·manual/automatic 경로로 fallback하지 않는다. |
| 전환 범위 | live production work의 public facade `CheckWorkAvailability`, `BeginWork`, `ExecuteWork` 세 경로를 같은 gate로 묶는다. core 내부의 passive-processing completion은 worker lane 실행이 아니므로 기존 private/internal progression으로 유지한다. |
| 검증 | pure policy matrix, root state mode query, facade compile/DI, Manual·PoweredAssist manual 허용, Automatic manual 거부, Automatic null-worker 허용, manual-only null-worker 거부, restore 후 동일 판정을 focused 및 가능한 live 경로로 증명한다. |

2026-08-30 P15 live 실행 경계 구현 증거: 실제 `Brain -> AIWork -> AbilityWork -> WorkTaskExecutor`를 사용하는 Manual·PoweredAssist, 실제 Automation executor, allocated-worker 전환 거부를 하나의 집중 PlayMode matrix로 연결했다. 성공 fixture는 authored I02 전력과 I09/U04 폐수 수용력을 실제 배치하며 P15의 유틸리티 요구를 완화하지 않는다. `CheckWorkAvailability`가 cycle utility를 비변이 preflight하고, 선택된 production bill 또는 authored production workstation의 `BeginWork` 실패는 legacy Cook로 fallback하지 않는다. full-I09 실패 arm은 typed utility rejection 뒤 bill·physical stack·clean water·sludge가 모두 불변이고 delegated work가 `0`임을 증명한다. 최종 결과는 `5/5 PASS`, Console Warning/Error `0/0`이며 `Artifacts/QA/v27-p15-production-execution-modes-playmode.txt` SHA-256은 `69180B3CCEE72B23D8BAA9478721E63279746C007753DB9A243D7C8FF4B42A94`다. 동일 조건 두 번째 실행은 hash·length `1379`·UTC write ticks `639236281536916771`이 모두 불변이고 공식 GameplayScene SHA-256도 유지됐다. 이 증거는 실행 배타성과 utility 원자 경계만 닫으며 producer 전수 throughput/profile, capacity source `@5`, atomic support publication과 live `4.000/4.001`이 끝나기 전에는 Batch B parent 행을 닫지 않는다.

#### I17/I18 장비 진행 시설 output 귀속·지속 작업 구조 계약

`building:9826` 룬 조율실과 `building:9827` 계보 기록실은 recipe/capacity producer가 아니라 장비 상태를 변경하는 domain operation 시설이다. I17은 UI 명령이 즉시 물리 장비 component를 변경하지만 census 귀속이 없고, I18은 주문·저장·완료 API는 있으나 production Craft 작업자가 완료 API를 호출하지 않는 P0 고아다. 이를 `DeclaredNoOutput`으로 숨기거나 건물 ID 분기로 연결하지 않는다.

| 항목 | 확정 계약 |
|---|---|
| 콘텐츠 정의 | 기존 `BuildingProductionWorkstationAbility.WorkstationTag`가 immutable typed capability key다. I17은 `workstation:v3:rune-tuning`, I18은 `workstation:v3:lineage-archive`를 사용하며 신규 mutable SO 필드나 건물 ID allowlist를 추가하지 않는다. |
| 런타임 상태 | I17 장비/module component와 I18 `CombatEquipmentRuntimeState.HistoryTransferOrders`가 기존 단일 쓰기 권위다. Craft adapter·census·UI는 별도 상태를 저장하지 않는다. |
| 명령 | I17은 기존 tune command, I18은 기존 `TryQueueHistoryTransfer`와 `EquipmentHistoryTransferRuntime.ApplyWork`만 상태를 변경한다. 공용 Craft 코어는 등록된 contributor에 진행량을 위임할 뿐 장비·계보 의미를 알지 않는다. |
| 조회 | canonical capability ID의 ordinal registry가 즉시 명령, 지속 Craft 작업, output disposition을 해석한다. I18 contributor는 같은 facility ID의 미완료 주문을 order ID ordinal로 선택한다. 누락·중복·비canonical contributor는 composition에서 fail-loud한다. |
| 식별자 | capability/contributor/order/facility ID를 서로 대체하지 않는다. `CraftWorkExecutionPlan`은 generic registered-operation kind와 exact contributor ID·operation ID를 함께 동결한다. |
| 저장 | 신규 DTO/schema는 없다. I17은 기존 component codec, I18은 기존 history-transfer order와 physical equipment state를 저장한다. Craft plan과 registry instance는 저장하지 않고 복원된 주문에서 재계산한다. |
| 의존성 | Models의 owner-neutral Craft contributor 계약 ← Infrastructure의 deterministic registry/adapter ← Combat의 lineage strategy 순서다. Craft 코어가 Combat 타입·workstation tag·건물 ID를 분기하지 않는다. |
| 실패 | 잘못된 facility capability, unknown contributor, operation mismatch, 완료 주문, missing physical source/target/seal, progress 실패는 다른 production bill이나 legacy craft로 fallback하지 않고 typed failure로 끝낸다. partial progress는 기존 order authority에만 남는다. |
| 전환 | I18의 실제 시설에 Craft work type을 추가하되 Repair는 보존한다. Editor/PlayMode가 직접 완료 API를 호출해 worker 경로를 우회하는 증거는 제거하고 실제 candidate→Craft adapter→contributor→history runtime 경로로 교체한다. |
| 검증 | I17/I18 census 귀속, registry 순서·중복·synthetic canary, I18 partial/completion/exact-once/save-resume, 잘못된 capability 거부, 실제 UI queue→AI Craft completion, source+seal exact Transfer와 target history 보존을 current-source Unity에서 증명한다. |

이 연결은 item kg·BOM·WU·EWU·가격을 바꾸지 않는다. I18의 기존 authored `requiredWork=120`도 유지한다. 변경의 밸런스 영향은 새 수치 배정이 아니라 이미 존재하던 주문을 실제 작업 AI가 수행하게 만드는 실행 경로 복구이며, 전수 census·실제 작업 시간·저장 복원 증거 전에는 `밸런스 완료`로 세지 않는다.

현재 증거(2026-08-29): I17/I18은 `equipment-progression:facility-output-disposition`의 typed state-mutation 행으로 census에 연결됐고, I18은 등록형 `equipment-progression:lineage-transfer-work` Craft contributor를 통해 `120 WU`를 `60+60`으로 진행하여 source와 seal을 정확히 한 번 소비하고 target history를 보존했다. focused TSV SHA-256은 `48DBF064A51D82B23DD11B477214860F98133964648ACB3846771CABC282FB30`이며 두 번째 실행의 byte/hash/mtime 변화는 `0`이다. 전체 Physical Item, V22 Apparel, 180 ResearchOverhaul, Production Economy 회귀와 Unity Console Warning/Error `0/0`은 PASS했다. 실제 UI queue→자율 AI Craft 완료 PlayMode는 아직 OPEN이므로 위 구조 계약 전체를 완료로 확대하지 않는다.

Grand Project maximum 전수 증거(2026-08-29): current 355개 recipe 중 exact affected ID 집합 `21`개를 고정하고, 각 행의 `factor numerator/denominator`, 모든 `probability>0` physical output의 authored/max quantity와 unit grams, base/scaled batch grams, 실제 시설의 4-cycle capacity를 `Artifacts/QA/v27-grand-project-affected-recipe-envelope.csv`에 기록했다. P14/P18/P19/P22 winner는 각각 `1,500/750/800/15,300g per cycle`, `6,000/3,000/3,200/61,200g per four cycles`다. CSV는 header 포함 `22`행, `6,210 bytes`, SHA-256 `382BFFF9133BEE49CACA9F671636AE252814CB30069F14C454770BB41859159A`이며 두 번째 실행의 byte/hash/length/mtime 변화는 `0`이다. Unity compile은 `Tundra build success (7.61 seconds)`, focused 실행 PASS, Console Warning/Error `0/0`이다. 이 증거는 standard recipe/Grand Project branch만 닫으며 별도 Loose world-resource/crop output authority를 대신하지 않는다.

Crop maximum 전수 증거(2026-08-29): 실제 `BuildingCropPlotAbility`가 있는 4개 시설과 current catalog 12개 작물의 exact Cartesian set `48`행을 `Artifacts/QA/v27-crop-harvest-maximum-envelope.csv`에 기록했다. 각 행은 facility/workstation/indoor, base yield, definition-only worker capacity·proficiency·effect maximum, seed effect maximum, Grand Project·ecology·soil 유리수, harvest/seed quantity와 exact unit gram, maximum batch, authored 2~4 cycle portfolio, contributor/source digest를 결속한다. CSV는 header 포함 `49`행, `18,225 bytes`, SHA-256 `E95D931717C75E828A79572F9A9A0034E37CC2BCAEE1BB23494C73B1ACDD3963`이며 두 번째 실행의 byte/hash/length/mtime 변화는 `0`이다. 같은 fresh 실행에서 `V27CharacterPerformanceDebugScenarios.RunDefinitionMaximumAudit`, contributor registry, `146/92/54` facility census가 PASS했고 Console Warning/Error는 `0/0`이다. 이 증거는 pure maximum projection을 닫지만 실제 `CropPlotRuntime`의 frozen two-line atomic publication과 save/restore를 대신하지 않는다.

2026-08-30 reachable 권위 교정: 위 `2026-08-29` definition-only 값은 서로 동시에 도달할 수 없는 효과 상한을 조합한 미래 안전 진단 envelope이므로 실행·저장 capacity 권위에서 제외했다. 새 `ICropHarvestReachableMaximumWitnessContributor`는 실제 content로 만들 수 있는 하나의 actor/context를 immutable witness로 캡처하고, Crop capacity와 integrated-cycle contributor가 같은 witness ID와 SHA-256 source digest를 사용한다. 현재 natural witness는 healthy Beastkin + trait `304` + FoodProduction/Fieldwork 각 Master cap `3060 XP` + Golden Harvest jackpot이다. 실제 GameplayScene의 `ICharacterPerformanceQuery`와 offline contributor가 capacity `1.1125`, proficiency `1.7241379`, harvest effect `2.5`, worker multiplier `4.79525852`, outdoor `28`, indoor `33`, returned seeds `7`로 exact 일치했다. `4 facilities × 12 crops = 48` rows를 schema `v27-crop-harvest-reachable-envelope@2`로 재생성했으며 header 포함 `49`행, `20,756 bytes`, SHA-256 `240DA8AEF36F638A201D480B500FF7761121759E4CDC6040A15E6FCEF92CECF3`이다. Ember Root maximum single completion은 outdoor `12,950g`, indoor `15,200g`이고, focused reachable witness·special throughput·capacity contributor 검증이 모두 PASS했다. Console Warning/Error는 `0/0`, GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다. 이 교정은 reachable capacity/cycle 하위 경계를 닫지만 Crop executable descriptor/receipt와 publication→natural `AIHaul` handler를 대신하지 않는다.

2026-08-30 Crop executable descriptor 증거: capability-keyed contributor가 current source에서 선택된 Crop branch를 shared cycle/input/mass 권위에 재결합하며, `CropHarvestCycleMaximumSnapshot.CropId`를 직접 전달해 composite ID 역파싱을 금지한다. 네 Crop 시설 payload는 exact sow/harvest/growth 조건, 입력 선택 digest와 physical inputs, harvest+returned-seed 두 output capability를 동결한다. 실제 GameplayScene product boot는 `92/92 descriptors`, typed gap `0`, payload split `85 recipe / 4 crop / 1 combat / 1 apparel / 1 certified-seed`, 전체 `23/23 PASS`다. report SHA-256 `852F4BBED72EB11C617A7B34DFB6AE88EA95B6148DD56374EEF7855254EB3FB9`, plan SHA-256 `9D82A23CBCF970748198BB44EF735EE428F10E9EF1A97E95C275123B344CA6F8`, portfolio SHA-256 `D1EF74504D3A82C8ADBC91C02D55B9DD8EBD973846358EA052C39C7A82620B0E`이고, 두 번째 실제 실행에서 hash·length·mtime 변화가 `0`이다. Console Warning/Error `0/0`, GameplayScene byte 불변이다. 이 경계는 실행 descriptor만 닫으며 실제 Crop operation/output commit을 묶는 execution receipt와 publication→natural `AIHaul` handler는 계속 OPEN이다.

#### WorldResource exact-source output 구조 계약

현재 source recipe asset은 `20 recipes / 26 output lines`지만 실제 WorldResource topology가 생산하는 것은 `source:grass`, `source:logging`, `source:saltstone` 3개뿐이다. `source:quarry`는 P22 generic ProductionBill 경로이고, animal 12개와 spoilage 4개는 별도 husbandry/waste runtime 소유자이므로 `source:` prefix만으로 WorldResource에 편입하지 않는다.

| 항목 | 확정 계약 |
|---|---|
| 콘텐츠 정의 | `IResourceEconomyContentCatalog`의 canonical `ProductionRecipeSO`와 등록형 `IWorldResourceSourceBindingCatalog`가 source definition·topology capability의 단일 불변 권위다. Tree/Rock/renewable patch 분류와 recipe/work type 결합은 registry row로 옮기며 runtime core에 recipe ID switch를 추가하지 않는다. |
| 런타임 상태 | `WorldResourceSourceState`가 progress, finite/renewable source reference, completed cycle sequence와 한 frozen pending outcome의 단일 권위다. Wildlife patch는 renewable quantity, Physical Items는 published Loose stack의 기존 권위를 유지한다. |
| 명령 | `ApplyWork` 완료 시 canonical key-addressed resolver로 outcome을 정확히 한 번 동결한다. `PhysicalItemExactSourcePublicationService.TryPrepare → exact source debit → TryCommitReleased`만 물리 output을 생성한다. line별 `SpawnOutput`은 제거한다. |
| 조회 | work availability는 current resource/research와 frozen pending 상태를 읽는다. output capacity는 authored 첫 line이 아니라 frozen whole-vector exact gram prepare 결과를 사용한다. |
| 식별자 | operation identity는 `{persistent nodeId, workTypeId, completedCycleSequence+1}`이다. synthetic bill ID, output line ID, factor numerator/denominator와 recipe semantic digest를 frozen fingerprint에 포함한다. frame·GameObject instance ID·shared sequential RNG state는 사용하지 않는다. |
| 저장 | WorldResource current format을 V3로 올리고 completed sequence와 frozen pending root seed/recipe digest/factor/output vector/fingerprint만 저장한다. transient admission token·stack ID는 저장하지 않는다. save capture는 exact-source prepare 이후 commit/rollback 전의 transient transaction을 fail-loud한다. |
| 의존성 | Economy model은 canonical resolver와 owner-neutral exact-source port만 의존하고, Items publication 구현과 Wildlife exact debit adapter는 composition에서 주입한다. `AuthorizedLooseSource` 공간 role을 소유권으로 재사용하지 않는다. |
| 실패 | renewable debit은 all-or-none이다. prepare 실패는 source·WU outcome을 보존해 같은 frozen 결과로 retry한다. debit 뒤 commit 실패는 source debit과 prepared publication을 모두 exact rollback하며, 어느 rollback도 실패하면 save를 허용하지 않고 typed fatal로 중단한다. zero-output 확률 branch는 fake item 없이 source debit·cycle sequence만 정확히 한 번 commit한다. |
| 전환 | `WorldResourceRuntime → IWorldResourceOutputPort.SpawnOutput → ProductionItemGateway.SpawnOutput(Loose)` production 호출을 0으로 만들고 owner manifest에 `economy.world-resource-output`을 추가한다. 기존 manifest의 output remaining `0`은 이 누락을 반영할 때까지 false-negative로 취급한다. |
| 검증 | live binding 3행 maximum artifact(`320/9,200/5,300g`), source 20행 typed classification, multi-line second-fault rollback, renewable insufficient no-debit, key-addressed shuffle/retry, frozen save/restore no-reroll, zero-output cycle, exact-source provenance, static direct-Loose 0, owner manifest fresh zero gate를 통과한다. |

이 구조 교정은 item kg·BOM·WU·EWU·가격과 scene/asset 수치를 바꾸지 않는다. crop의 별도 atomic publication과 WorldResource 3행이 모두 proof-bound output 경로에 연결되기 전에는 full maximum envelope 체크를 닫지 않는다.

추가 current-source P0 발견 사항과 선행 조건:

- [x] 355개 레시피와 363개 `ResourceItemDefinitionSO`의 연구 ID를 current 180개 `ResearchProjectSO` 그래프와 exact join했다. V21 흡수 ID를 쓰던 recipe/item 각 11개를 canonical ID로 함께 교정해 orphan `0/0`을 증명했으며 runtime fallback은 추가하지 않았다.
- [x] 28개 production support의 unlock reachability를 증명했다. WS08 hearth는 `research:cuisine:crops`, WS10 electric oven은 `research:industry:assisted-processing`의 building unlock으로 귀속했고, BuildingSO `unlocked=0`을 유지하면서 unreachable support를 `0`으로 만들었다.
- [x] 실제 standard production output과 FacilityBuffer Grand Project 최대 projection이 같은 `ProductionOutputFactor` 유리수 권위를 사용한다. authored `1.25/1.20/1.15`는 각각 exact `5/4`, `6/5`, `23/20`으로 canonicalize되고, 실제 출력은 exact decimal scale, 최대 용량은 exact Ceil quantity를 사용한다. prepared outcome fingerprint도 float 표시값이 아니라 exact numerator/denominator를 기록한다. cross-GCD 곱셈·overflow·비canonical multiplier·경계 Ceil·fingerprint focused 회귀와 Unity Console Warning/Error `0/0`을 통과했다.
- [x] authored production support 28개를 immutable maximum catalog로 캡처하고, required/batch support tag의 실제 provider 존재를 current installed 상태와 무관하게 검증한다. 현행 28개 support는 exact `1/1`이지만 future non-unit 조합도 stable bitset DP로 계산한다. synthetic `1.4/1.25/1.25` overlapping-provider 조합은 exact `7/4`, 입력 순서 shuffle은 동일 digest/value, batch-provider multiplier 제외와 잘못된 provider 종류 fail-loud를 증명했다. current prepared-output family에는 P17 `4,200g`, sawmill `14,400g`, charcoal `3,600g`, mill `2,800g`, steelworks `3,400g`, treated-lumber `9,200g`가 포함된다. 실제 P22 quarry는 모든 확률 부산물과 Grand Project `5/4`를 포함해 exact `15,300g/cycle`, `61,200g/4 cycles`를 current asset에서 검증했다. Unity focused 회귀와 Console Warning/Error `0/0`을 통과했다.
- [x] 실행 handler와 분리된 `IProductionOutputMaximumMassCapability`/registry를 추가했다. registry는 capability ID/version, component codec ID/version, automatic-selection 정책과 mass-authority revision을 frozen SHA-256 projection에 결속하고, insertion-order 결정론·중복/누락/모호성·descriptor drift·overflow를 fail-loud한다. runtime projector 생성 시 execution registry와 projection registry의 정렬된 계약이 exact parity가 아니면 시작하지 않는다.
- [x] generic recipe capacity 열거에서 standard-only 필터를 제거하고 capability-owned projection으로 모든 matching recipe output line을 해석한다. 현재 재단대 58개 레시피는 stateful Apparel을 누락하지 않으며, 방수 작업복 `1,380g/cycle`, authored 4회 `5,520g`와 멜빵 actual minimum `4,600g`을 current asset에서 검증했다. source digest는 `production-output-buffer-capacity-source@2`로 올리고 각 line의 descriptor/projection digest를 포함한다.
- [x] 전체 maximum envelope를 닫는다. generic Apparel maximum, Apparel command/rejected-recovery no-bill contributor, ruined output의 frozen WIP/input/fluid claim, M06 surgical actual asset 및 recipe-backed no-bill 회귀, active Apparel/Combat/CertifiedSeed frozen maximum proof, CertifiedSeed no-bill contributor, current `146 facility / 92 producer / 54 nonproducer` typed 분류, Grand Project 영향 레시피 exact `21`행과 WorldResource 4-binding/3-recipe maximum proof·exact-source publication 결속은 닫혔다. WorldResource는 `320/9,200/5,300g` line별 capability proof와 aggregate maximum을 frozen outcome/save restore에 결속한다. 마지막 우회였던 crop harvest도 harvest/returned-seed의 frozen two-line outcome, proof-bound aggregate maximum, actual≤maximum, whole-vector atomic publication, capacity wait, save/restore no-reroll, acknowledgement 재시도 exact-once와 GoldenHarvest·ecology 결속을 공용 publication 경계에서 닫았다.
- [x] capability-owned maximum projection은 executable handler 집합을 projector에 직접 주입하지 않는다. `Standard/Workwear/Surgical/Apparel/Combat craft/CertifiedSeed`가 frozen descriptor에 결속된 definition-only upper-bound proof를 제공하고, pure projection registry와 execution registry의 capability ID/version parity를 composition audit에서 강제한다. active domain batch는 proof 없이는 시작할 수 없다. CertifiedSeed·Apparel의 no-bill contributor, Surgical generic preprojection, Combat facility eligibility·craft catalog·rejected-recovery contributor가 모두 공용 pure registry에 연결됐고 runtime projector 생성 시 execution/projection capability ID·version·codec parity를 exact 강제한다. raw exact batch caller와 content-ID별 core branch는 0이며 insertion-order/locale/canary focused 증거가 이미 current-source PASS다.
- [x] CertifiedSeed 시설 eligibility를 단일 권위로 만들고 numeric `building:8893` 복제를 production source에서 제거했다. 다른 numeric ID라도 exact workstation tag와 valid buffer가 있으면 동일 계약으로 편입되고, legacy ID라도 capability가 다르면 거부된다.
- [x] Combat은 `BuildingEquipmentCraftingAbility`의 canonical nonempty exact allowlist를 command/UI/worker/contributor가 공유하고, 빈 목록 wildcard와 silent Trim/Distinct를 제거한다. primary command/worker/contributor, UI의 malformed-list 거부와 ammo 2종 실제 queue, output/BOM catalog, quality-rejected multi-output recovery maximum을 동일 catalog/eligibility/projector에 결속했다.
- [x] current-revision P17 live 회귀를 정상 부트 경로에서 다시 생성했다. contributor schema-v4 적용 뒤에도 no-bill `4,200g`, 실제 hay `588g`, AIHaul, kg warehouse `3/3`, save recapture identity와 second restore no-duplicate를 모두 증명했다. fresh report SHA-256은 `90AF89B6538A45992160C9C6CA00537BBB9D44F62BF5F7A2CCFEF60E73DCEE14`, Console Warning/Error `0/0`이다.
- [x] Physical logistics verifier의 부트 경로를 direct Gameplay + debug fast commit에서 `Title → production navigator → StartPreparation owner UI → PreparedNewRun → Gameplay`로 교정했다. runner는 scene 전환 중 유지되고, 단계별 bounded readiness와 typed report를 남기며, 실행 전 영속 데이터 snapshot/restore와 미저장 scene fail-loud guard를 적용한다. Unity compile과 focused production 회귀는 Console `0/0`으로 통과했다.
- [x] 교정된 정상 부트 verifier로 current-revision P17 live report를 재생성했다. 임시 scene lease는 종료 시 제거됐고 request/backup 잔여는 `0`, 공식 `Assets/Scenes/GameplayScene.unity` SHA-256은 실행 전후 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 동일하다. 이 행은 P17 live 증거만 닫으며 full maximum-envelope·no-bill contributor·destructive lifecycle 완료로 확대하지 않는다.
- [x] 공통 `IProductionOutputDestinationLifecycleQuery`로 bill/WIP/reserved/origin physical/routing/outbox/custody/recovery를 한 snapshot에 집계한다. generic bill뿐 아니라 combat equipment craft와 apparel work order도 독립 contributor로 포함하고, contributor stable ID 순서·중복 거부·결합 SHA-256 fingerprint를 focused contract로 증명했다. gameplay query는 save DTO를 읽지 않으며 local buffer가 비었다는 사실만으로 revoke를 허용하지 않는다.
- [x] output destination lifecycle이 capacity profile뿐 아니라 exact claim도 같은 destination별 1:1 권위로 결합한다. claim-only/partial/conflicting authority는 lifecycle capture와 `TryRevoke` preflight에서 mutation 없이 fail-loud하며, owner/domain/operation/facility/drop/schema join과 claim revision을 semantic fingerprint에 포함한다. 정상 revoke 뒤 claim/profile 모두 0이라는 postcondition을 focused Unity에서 증명했다.
- [x] 합성 full-path canary의 정적 gate를 `batch >= 1회`에서 exact `20,000g/cycle × 4 = 80,000g`으로 강화하고 P17 buffer cycle authority가 정확히 `4`인지 요구한다. 생산 전·후 profile은 동일한 exact 80,000g이고 reservation 0이어야 한다. 이는 verifier 계약 compile 증거이며, F: volume health와 dirty GameplayScene 때문에 asset-backed PlayMode 실행 자체는 여전히 OPEN이다.
- [ ] 철거·structural/cover loss·evolution·relocation·synthesis가 공통 mutation fence와 reversible candidate를 통과하게 한다. relocation/evolution은 가능한 경우 persistent facility ID와 `production-output:{sameId}`를 유지하며, preflight 뒤 새 reservation이 생기지 않게 cycle start/reserve도 같은 fence를 확인한다.
- [x] 직접 demolition에 한해 `prepare → mutation epoch → exact lifecycle fingerprint 재검증 → empty-only authority revoke → grid removal → complete`를 연결했다. grid removal 실패 주입에서는 기존 positive capacity/profile과 grid occupancy를 복구하고 epoch를 종료하며, 성공 경로는 revoke 뒤 authority 부재 postcondition을 확인한다.
- [x] relocation·synthesis·evolution은 retarget/동일 persistent ID transaction이 생기기 전까지 production-output authority가 하나라도 있으면 첫 world mutation 전에 fail-closed한다. 이는 해당 기능의 최종 지원 완료가 아니며 위 복합 행은 계속 OPEN이다.
- [x] mutation epoch가 활성인 동안 generic production의 bill 추가·작업 시작·output 실행·passive progress를 차단한다. epoch owner/current 검증과 prepare 뒤 lifecycle fingerprint stale 주입 거부를 focused scenario로 증명했다.
- [x] structural integrity와 combat cover의 lethal 제거를 공통 `BuildingDestructiveLossRuntime`으로 전환했다. strict-empty mutation candidate가 준비된 뒤에만 output authority revoke·실제 registered grid layer 제거·visual 제거·epoch complete·`DestroySelf()`를 실행하며, 차단 또는 grid removal 실패에서는 HP·grid·authority를 변경하지 않는다. 전투 철거에는 건설비 환급·salvage를 호출하지 않고, HP 0 cover restore는 registry에 재등록하지 않는다.
- [x] current-format world aggregate 교체는 구 object를 gameplay destruction event가 없는 `RetireForWorldReplacement()`로 retire한다. 동일 restore fixture에서 old object 제거, destruction subscriber 호출 `0`, 새 candidate publication과 두 번째 report SHA-256 identity `EA0C49DFEC716CC1CE19817ADEC366714465B9D5ED40AF355C6D14294281DE40`을 증명했다.
- [x] active WIP/equipment craft/apparel/routing/outbox/carried/recovery의 6-participant durable terminal/transfer plan, journal-last checkpoint GC와 production DI/save registration을 실제 P03 구조 파괴 PlayMode로 닫았다. active carried cargo가 있는 sawmill 제거에서 6-participant drain, throwing notification subscriber의 `CommittedWithNotificationFailure`, authority pair `Absent/Absent`, lifecycle `0/0`, world removal, checkpoint GC, terminal restore 무복제와 두 번째 checkpoint no-op를 같은 current-source run에서 증명했다.
- [ ] relocation·synthesis·evolution의 active production retarget, persistent facility ID 보존, reversible multi-facility commit을 구현한다. 현재 증거는 no-authority fail-closed뿐이다.

검증 증거(2026-08-26): `ProductionOutputDestinationLifecycleDebugScenarios`, `ProductionOutputDestinationAuthorityDebugScenarios`, 전체 `ProductionEconomyDebugScenarios`, `FacilitySynthesisDebugScenarios`, `FacilityEvolutionDebugScenarios`를 같은 post-compile focused run에서 실행해 PASS했다. Unity Console Warning/Error는 `0/0`이다. 마지막 관련 runtime/editor DLL은 각각 `8,414,208 bytes @ 2026-08-26 07:04:06 +09:00`, `8,525,824 bytes @ 2026-08-26 07:04:08 +09:00`이며 이후 import/compile 구간도 `Tundra build success`, current Editor.log compile error `0`이다. 이 증거는 source/focused contract 수준이며 실제 repository PlayMode나 destructive-loss 완료 증거로 확대 해석하지 않는다.

2026-08-29 Apparel/Surgical no-bill fresh 증거: `ProductionFacilityOutputCapacityContributorRegistryDebugScenarios.RunAll`, `ApparelPhysicalTransactionDebugScenarios.RunAll`, `ApparelRejectedDismantlePhysicalTransactionDebugScenarios.RunAll`, 전체 `ProductionEconomyDebugScenarios.RunAll`이 current Unity assembly에서 PASS했고 Console Warning/Error는 `0/0`이다. Apparel contributor는 effect definition의 finite maximum과 exact item gram으로 recovery 상한을 계산하며, `output:apparel-rejected-recovery` 단일 line 권위와 549 executable alternatives를 고정한다. M06은 3개 실제 recipe와 5,400g generic preprojection parity를 고정한다. 이 증거는 Combat과 전수 `146/90/56` 집계를 대신하지 않으므로 상위 결합 행은 계속 OPEN이다.

2026-08-29 Combat primary no-bill fresh 증거: actual S08의 63-entry allowlist가 61 equipment + arrow/bolt 2개와 exact join되고, powered harness `18,000g/cycle`, 4-cycle `72,000g` primary profile을 투영한다. `ProductionFacilityOutputCapacityContributorRegistryDebugScenarios`, `CombatEquipmentMaterialDebugScenarios`, `CombatEquipmentCraftTerminalAuthorityDebugScenarios`, 전체 `ProductionEconomyDebugScenarios`가 PASS했고 마지막 clean Console Warning/Error는 `0/0`이다. 더 넓은 `CombatSystemDebugScenarios`는 이번 변경과 무관한 기존 `건설형 엄폐물` 행에서 실패했으므로 green 증거에 포함하지 않는다.

2026-08-29 Combat rejected-recovery fresh 증거: 동일 pure projector가 current `61 equipment × material policy = 252` definition projection을 캡처하고, 합법적 zero-output `20`개를 별도 census로 보존한 채 실제 물리 branch `232`개만 contributor에 게시한다. primary `63`과 recovery `232`는 합산하지 않는 alternative `295`개이며 S08 winner/profile은 계속 `18,000g/72,000g`이다. 실제 S08 `weapon:greatsword`의 품질 거절→별도 해체 작업자 skill `73`→exact input Transfer `4,600g`→회수 commit 1개→ack 실패→Physical/Combat detached restore join→동일 commit 재사용→attempt `+1`과 recovery state clear를 통과했고, 물리 commit을 삭제한 후보는 publication 전에 거부됐다. focused TSV SHA-256 `474AA3C192C51B8B86B5CE73ADE6C33418E8216AE8EED2F197BB35513EFB141B`; contributor/transaction, Combat material/terminal, 전체 Production Economy가 fresh PASS했으며 Console Warning/Error는 `0/0`이다. ammo 2종 UI queue도 current source/focused evidence로 닫혔다. Combat 결합 하위 행은 완료지만 전수 `146/90/56` census가 남아 상위 domain 행과 Batch B 전체는 OPEN이다.

2026-08-29 crop whole-vector maximum fresh 증거: crop completion은 harvest와 returned seed를 하나의 frozen two-line outcome으로 확정하고, 공용 `ProductionDomainOutputPublicationService`가 aggregate maximum proof와 actual vector를 publication 전에 대조한다. 출력 공간 부족은 `WaitingForOutputSpace`로 보존되고 current-format save/restore 뒤에도 동일 outcome을 재사용하며, acknowledgement 재시도에서 물리 출력 증분은 `0`이다. 실제 `crop:twilight-grain` PlayMode는 harvest `0→6`, exact SeedLot semantic component와 production provenance, capacity wait, frozen restore, replay delta `0`을 통과했다. sow receipt가 이미 소비한 cycle input 외의 over-delivery seed는 `RemoveDestination` sink로 삭제하지 않고 material destination release로 반환하며, Unity JSON이 빈 output을 all-default object로 물질화하는 경우에는 exact empty sentinel만 canonical null로 허용한다. `CropPhysicalTransactionFixture`, `V21CropGenomeDebugScenarios`, `V26FounderTraitAuditScenario`, `DungeonSaveSectionDebugScenarios`와 isolated paused PlayMode crop verifier가 PASS했고 최종 report SHA-256은 `E0F6787F7CDE53492BB1AFF7D62AADADAAE536832CAD82125865B8140B6A2785`, Console Warning/Error는 `0/0`이다. current assembly는 `Assembly-CSharp.dll 9,918,976 bytes @ 2026-08-29T06:51:15.5013996Z`, `Assembly-CSharp-Editor.dll 9,886,720 bytes @ 2026-08-29T06:42:24.6684648Z`; 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 유지됐다. 이 증거로 전체 maximum-envelope 한 행만 닫고, retarget transaction, support/p95/>4-cycle gate, unified mutation fence, destructive live integration, active multi-facility retarget의 Batch B 5개 행은 계속 OPEN이다. 전체 Batch B aggregate는 별도 wildlife save-version/player-fairness 실패가 남아 있으므로 green으로 보고하지 않는다.

2026-08-29 P03 destructive live fresh 증거: 실제 `recipe:sawmill-lumber` output을 AIHaul이 pickup한 상태에서 P03 구조 파괴를 요청해 capacity-routing durable stage와 중간 checkpoint를 먼저 만들고, 6-participant terminal drain을 bounded `6/32` 전이로 완료했다. 알림 subscriber의 의도적 예외는 world mutation rollback이나 raw exception이 아니라 `CommittedWithNotificationFailure`로 귀속됐고, 제거 뒤 output/sensor authority pair는 `Absent/Absent`, lifecycle은 authority/record/mass `0/0/0`, bill과 world facility는 제거 상태였다. 물리 전체는 quantity `18→18`, mass `21,600g→21,600g`, carried `0`, lease/intent/admission released, inbound `0`을 유지했다. Unity JSON all-default haul intent는 exact sentinel일 때만 absent로 정규화하고, 제거된 power node aggregate는 pending fuel이 없을 때만 retire한다. authority revoke와 world removal은 각각 모든 owner acknowledged·exact contributor set·empty lifecycle을 요구한 뒤 동일 snapshot의 aggregate와 participant current fingerprint vector를 한 revision으로 재기준화하며 prepared/plan/receipt provenance는 바꾸지 않는다. terminal checkpoint GC 뒤 open journal `0`, 저장 복원은 동일 `WorldRemovedAwaitingCheckpointGc`와 quantity/mass를 무복제로 재구성했고 두 번째 checkpoint도 open `0`/quantity `18`/mass `21,600g` no-op였다. 보고서 `Artifacts/QA/prepared-output-destructive-drain-live-playmode-report.txt` SHA-256은 `71614C1CC65F30F0187438F25770F503914CC2A325006821AEB33DE2A26370C9`, `RESULT=PASS; failures=0`, 런타임 캡처 Warning/Error `0/0`, 공식 GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 불변이다. 이 증거로 destructive live integration 한 행만 닫으며 Batch B는 `36/40`이다. 남은 네 행은 retarget transaction, support/p95/>4-cycle gate, unified mutation fence, active multi-facility retarget이고 Batch C–H 및 전체 밸런스 완료는 계속 OPEN이다.

### 16.4 Batch C — 비창고·entity transport P0

다음 경계는 mass query가 있다는 이유만으로 닫힌 것으로 간주하지 않는다.

- FacilityBuffer input owner 39개: current-source 기준 migrated `8`, remaining `31`, bypass/orphan `0/0`, transport-delegated-exact `1`이다. `31` remaining을 공통 capability·durable slot·entity care·external dropoff·fluid/waste·construction BOM 경계로 이관하고 `fullStoredDestinationCoverage=true`까지 폐쇄한다. Batch A output gate가 이 input 분모를 대신하거나 중복 소유하지 않는다.
- shop: exact retail lot과 내부 capacity 차원을 맞추고 count-only display/internal stock을 physical mass와 혼합하지 않는다.
- expedition: staging supply custody, abstract transition, loot materialization, cancel/return을 Source/Transfer/Transform/Sink로 닫는다.
- rescue/captive wildlife: item kg와 hard cap·속도 페널티에서는 제외하되 actor custody, path, disable, Downed, Dead, save/restore를 공통 entity transport authority에 둔다.
- 생물 운반 중 opportunistic item haul과 두 번째 entity transport를 금지하고, surgical transport `OnDisable/Cancel` 및 mid-carry restore teleport를 P0로 고친다.

### 16.5 Batch D — family proposal generator

이 배치는 gameplay asset을 쓰지 않는 `AuditOnly`다. 다음 immutable 행을 current authority에서 생성한다.

```text
stableId
definitionKind
unitSemantic
authoringFamily
beforeUnitGrams
proposedUnitGrams
massFormula
producerIds
consumerIds
recipeInputAndOutputGrams
packageTareDisposition
maxStackMass
warehouseAndBufferImpact
ordinaryHaulBatch
ewuAndPriceImpact
sourceDigest
proposalReason
reviewDisposition
```

가족별 projection은 공통 anchor와 공식으로 계산하며 display name 추론을 권위로 사용하지 않는다. builder가 생성하는 660개 비원장 definition과 377개 facility kit도 원장 item과 같은 source origin에 조인한다. 한 family의 정상 행은 한 번에 검토하고, root anomaly만 펼친다.

필수 자동 검사:

- proposal coverage와 source inventory의 exact bijection
- 모든 producer/consumer/non-recipe sink 연결
- 동일 stable ID의 competing writer 0
- BOM/밀도/포장/명시적 loss로 설명되지 않는 gram 0
- recipe input/output branch의 undeclared creation 0
- maxStack·warehouse·buffer·carry band impact
- 변경된 원초 재료가 downstream Critical을 만들 때 root-cause collapse
- 입력 순서·locale·domain reload·두 번째 실행 byte identity

### 16.6 Batch E — anomaly review와 changed-only apply

사용자 또는 기획 검토가 필요한 것은 414개 전체가 아니라 다음 anomaly 묶음이다.

1. unresolved semantic 51개를 producer/consumer 묶음과 함께 제시한다.
2. 같은 item을 서로 다른 단위로 해석하는 recipe·shop·contract를 묶는다.
3. family 공식에서 벗어난 facility kit, packaged lot, carcass, equipment, artifact를 묶는다.
4. kg 교정으로 nutrition/feed/potency/damage/WU/EWU/price/storage ratio가 Critical이 되는 root만 제시한다.
5. 승인 결과는 exact value, formula, dependency fingerprint, source digest를 포함하며 wildcard를 금지한다.

ApplyApproved는 proposal과 current source digest가 exact 일치할 때만 builder/SO를 바꾼다. generated asset을 손으로 고치지 않고 source builder를 먼저 고친다. changed asset만 Dirty/ForceReserialize하고, 두 번째 build/apply의 byte diff가 0이어야 한다.

### 16.7 Batch F — 도메인 cluster 이관

구조가 같은 도메인을 한 파일씩이 아니라 terminal behavior 단위로 묶는다.

| cluster | 포함 경계 | 대표 fault |
|---|---|---|
| 의료 | 약품·수술·포장 tare·폐수·병상 부품 | cancel, patient move, facility destroy, restore |
| 농업·축산 | sow/harvest/feed/carcass/butchery/spoilage | plot destroy, burst output, missing storage |
| 건설·수리·철거 | BOM WIP·restitution·kit·회수 | placement failure, cancel, partial output |
| 연구·계약·보상 | blueprint·archive·reward·virtual boundary | outcome ack fault, duplicate reward |
| 전투·원정 | ammunition·equipment·loot·supply | encounter cancel, return, entity transport |
| 상점·경제 | exact lot purchase/sale, internal capacity | stale price/stock, external handoff retry |

각 cluster는 정적 callsite 0만으로 닫지 않는다. 최소 한 actual producer→consumer 정상 경로와 한 terminal fault, current-format restore를 같은 source revision에서 통과한다.

### 16.8 Batch G/H — live fault matrix와 최종 밸런스

Batch G는 구조 이관을 검증하고 Batch H는 수치가 플레이 가능한지 검증한다. 둘을 섞어 구조 버그를 kg 수치로 보정하지 않는다.

Batch G representative matrix:

- whole/partial pickup, multi-stack carry, destination revision drift
- active replan은 cargo 유지
- Downed/Dead/disable은 current-cell `TransientCarryRecoveryDrop`
- drop publication 실패는 `RecoveryPending`과 cargo/lease/intent 보존
- output full, 1g clearance, facility destroyed, cancellation, mid-haul/mid-WIP restore
- carried quantity·grams, source+destination reservations, receipt, Floor Clutter provenance exact
- clean A/B repeatability와 causal-cone 밖 RNG cross-talk 0

Batch H 순서:

1. kg 적용 뒤 recipe output·efficacy·consumption·WU·maxStack·capacity를 결합 재산정한다.
2. 물류 EWU·AcquisitionCost·RecoverableValue·구매/판매 가격을 재생성한다.
3. 6인 음식·물·N+1·7일 비축·성장 노동·공간 headroom을 검증한다.
4. 32/64 paired clutter seed, 256 layout seed를 실행한다.
5. 마지막으로 5일 seed `157181/157182/157183`과 Console `0/0`을 확인한다.
6. CSV/JSON/Markdown/YAML을 두 번 생성·적용해 Git diff 0을 증명한다.

전수 seed는 구조 또는 authoring source가 바뀔 때마다 반복하지 않는다. Batch A~G의 source digest가 동결되고 unresolved P0/P1과 Critical이 0인 후보에서만 최종 실행한다.

### 16.9 과거 Phase 색인

아래 Phase 0~9는 기존 요구사항을 찾기 위한 색인이다. 완료 표시는 당시 하위 계약의 증거이며 Batch A~H의 완료를 자동 의미하지 않는다.

### Phase 0. 권위 동결과 전수 목록

상태: **현재 source revision 재캡처 완료**. 마취제 packaged-lot과 의료 바이알 추가 뒤 canonical item `414`, recipe `355`, serialized weight site `1,074`를 Unity 전수 AuditOnly로 다시 캡처했다. source 또는 asset digest가 바뀌면 이 단계는 자동 재개방한다.

이 완료 표시는 2026-08-20 snapshot에만 해당한다. 이후 각 AuditOnly/ApplyApproved 시작 시 current source inventory를 재캡처하며, 아래 기대 수나 stable ID 집합이 달라지면 Phase 0은 자동으로 다시 열린다.

- 414 canonical item identity 캡처
- 1,074 serialized weight 위치를 canonical item에 역매핑
- 355 recipe 캡처
- combat equipment 61개, apparel, textile, material 캡처
- 모든 weight builder 호출자 전수 manifest
- current source digest와 baseline record 캡처
- asset 변경 없음

완료 조건:

- item/recipe/equipment/apparel orphan 0
- 중복 item kg write authority 0
- unknown builder weight producer 0

### Phase 1. carry target 수직 슬라이스

- 적용된 carry nominal `25kg`를 공용 tuning authority의 단일 현재 권위로 검증
- ordinary/harness/heavy-build 조합 표 자동 검증
- default max multiplier 1.5 유지
- 과적 속도 곡선 유지
- 설정 1.0/1.5/2.5 stress
- 멜빵 착용/내구/해제 회귀

이 단계에서는 item kg를 아직 전수 적용하지 않는다. carry formula만 focused fixture로 검증한다.

상태: **완료**. nominal `25kg`, 실제 일반 `19/29kg`, 멜빵 `24/36kg` 증거를 유지한다.

### Phase 2. unit semantic과 mass profile

상태: **부분 완료**. current-source compiled semantic expectation은 `363/414`, unresolved `51`이다. 과거 Unity artifact의 explicit 행 수는 recapture 시점에 따라 뒤처질 수 있으므로 current source와 artifact를 혼합 보고하지 않는다. 전투 장비·의복·비포장 물자·원초 자원·공구·보철·침구·제조 부품·비포장 가공 소재·고형 공예품·전리품·포장 없는 식사/공정 단위와 후속 integral-solid 의료/농업 semantic family projection은 Batch D/E에서 deterministic artifact로 재생성한다.

- 414 item에 1개 의미 작성
- MaterialMassProfile 구축
- 기존 weight를 Before로 캡처
- raw/source item anchor kg 확정
- facility kit packed abstraction 확정
- missing semantic fail-loud

Phase 2 다음에는 아래 `구조 기반 Gate S0`를 먼저 완료한다. Phase 3~5의 계산·fixture 작성은 병행할 수 있지만, Gate S0 green 전에는 신규 kg/효능/WU/EWU/가격을 더 적용하거나 창고 수치를 바꾸지 않는다.

### Phase 3. 첫 mass 수직 슬라이스

상태: **부분 적용·검증 보류**. 12개 변경 unit weight는 `provisional-applied`이며 구조 기반 Gate S0와 Phase 5.5 전에는 추가 적용하지 않는다.

다음 세 경로를 먼저 끝까지 구현한다.

```text
통나무 -> 처리 목재 -> D03/조리 -> 해체/회수
철광석 -> 철괴 -> 단검/방패 -> 수리/해체
곡물/채소/물 -> 식사 -> 섭취/부패/폐기물
```

검증:

- 원료·중간재·시설·식품 unit semantic
- BOM mass derivation
- 절삭·제련·조리 loss
- 레시피 6~11kg
- actual haul 8~14kg
- 장비 instance mass
- dismantle mass/EWU 차익
- 6인 하루 food transport

이 수직 슬라이스가 green이 되기 전에는 전수 asset apply를 시작하지 않는다.

### Phase 4. 전투 장비·의복 통합

상태: **완료**. current Unity assembly에서 equipment `61`, apparel `56`, 멜빵 `1,150g` 단일 계상, module·loaded ammunition 동적 질량, instance/warehouse/carry/equipped projection과 mass-invariant 상태를 focused/full 계약으로 재검증했다.

- 공용 EquipmentMassProjector
- 공용 ApparelMassProjector
- IPhysicalItemMassQuery instance component 경로
- world/carry/equipped 동일성
- material variants
- module add/remove
- loaded ammunition add/remove
- wetness·contamination·durability·quality·freshness·charge mass-invariant 검증
- 멜빵 physical `1,150g` 단일 계상
- 61 combat definitions 전수

### Phase 5. 355 recipe 전수 질량 감사

상태: **전수 inventory 생성기 구현·Roslyn 컴파일 완료 / Unity capture와 개별 disposition 계약은 pending**. 355개를 전부 열거해 current input/output/fluid gram, probabilistic minimum/maximum/expected output, flow-role shape, mass-creation 여부와 current-source reviewed exact contract `42`개를 한 원장으로 분리한다. 이 inventory가 생성됐다는 사실만으로 나머지 recipe가 감사 완료된 것은 아니다.

- Source/Transform/Sink 분류
- branch별 mass balance
- explicit loss/byproduct
- multi-producer conflict
- batch haul class
- ordinary/heavy/oversize 분포

### 구조 기반 Gate S0. 교차 도메인 blind-spot 폐쇄 감사

> Revision v5 주의: 아래 실행 순서와 413-item 문구는 당시 Gate S0 증거의 역사 기록이다. 앞으로의 실행 순서는 16.1의 Batch A~H가 supersede하며 current-source 분모는 414 item이다.

실행 순서: **Phase 2 직후, Phase 3의 신규 수치 적용보다 먼저**. 상태: **진행 중**. slice 1은 fresh compile/focused/full Physical evidence까지 완료했다. slice 2는 L01 positive 25,000g authority, Stored gram index, partial-quantity 계산, warehouse-local revision, generic admission token/terminal tombstone/idempotent receipt, affected-record undo journal, 첫 production `SpawnStockInWarehouse` commit, `12kg/25kg` canonical building UI, management/tab/card mass/count dimension separation, detached facility candidate와 physical current-format exact join, 공식 `RestoreAll` 왕복, valid 39,300g/25,000g over-capacity 보존+신규 입고 차단, root-swap 뒤 exact evacuation request publication/cleanup, destination gram admission, 실제 AIHaul exact lot 15,000g 대피와 token/reservation/pending cleanup, non-empty 철거/이전 production command 무변경 거절과 empty lifecycle gate, orphan destination/좌표 변조 atomic rejection과 fresh full Physical evidence까지 증명했다. slice 3a combat-equipment은 base item+attached module+loaded ammunition의 exact gram을 validated immutable subject에 한 번 준비하고, durability·quality·worldState·powerCharge·module grade/condition은 질량 불변으로 유지하며, warehouse reserve/Stored index/commit receipt/restore rebind와 실제 crafted equipment AI 입고를 같은 subject 권위로 연결했다. slice 3b apparel은 exact Apparel component와 item-instance를 검증한 prepared subject, material projection의 physical definition 단일 권위, equipped read model과 carry의 stateful cargo mass를 연결했다. 멜빵은 authored physical `1,150g`이 equipped burden에 한 번만 포함되고, 25% carry bonus와 cargo mass는 분리 계산된다. slice 3c carcass는 18개 live species의 `CarcassWeight`와 physical item gram을 startup projector에서 exact join하고, 누락 13개를 builder가 stable ID로 생성하며, 사냥 사체 exactly-once와 누락 정의 fail-loud를 증명했다. 이어 부패·도축을 `PhysicalItemTransformService`에 연결해 모든 output definition과 gram을 source 제거 전에 검증하고, 출력 질량이 입력보다 큰 변환을 거절하며, output failure 시 source와 기존 output stack 수량을 복원하고, commit receipt에 input/output/loss gram을 기록한다. 수율이 비어 있는 13종은 사체를 삭제하지 않고 fail-loud한다. authored 413-item inventory와 writer/source digest도 새 source에서 deterministic recapture PASS했다. conveyor overflow는 `CanStore(category,count)` 우회를 제거하고 exact item/instance/component lot과 warehouse-local revision으로 full quantity gram token을 확보한 뒤에만 `InTransit→Stored`를 커밋한다. 부분 수용은 token을 release하고 전량 InTransit을 유지하며, 물리 전환 후 commit 실패도 이전 위치·상태·destination owner로 rollback한다. focused gram 계약, full PhysicalItemLogistics, 기존 씬 노드를 포함한 live Industrial 순환 교착 28/28·오버플로 물리 배출 회귀가 모두 PASS했다. 외부 상점의 unique 장비는 아직 exact-instance retail handoff가 없으므로 invalid generic carry를 생성하지 않고 외부 terminal 경계로 유지하며, typed disposition receipt는 후속 slice 7에 남는다. packaged lot·나머지 ingress·전체 count cutover가 남아 있으므로 Gate S0 전체는 계속 `structural-partial`이다. 아래 경계를 한 번에 바꾸지 않고 compile-green 수직 슬라이스 순서로 구현한다. 각 슬라이스는 rollback·focused evidence·Unity compile을 통과해야 다음 슬라이스로 넘어간다.

1. `Foundation/Items` canonical item ID, `PhysicalMassGrams`, runtime mass subject/query
2. generic warehouse 한 곳의 positive gram capacity, partial admission, UI와 current-format restore
3. unique equipment/apparel/module/ammunition mass와 멜빵 단일 계상
4. production/shop/reward/conveyor/warehouse-transfer 전체 ingress와 admission commit token
5. interruption `RecoveryPending`, valid over-capacity restore, warehouse lifecycle
6. typed physical disposition, recipe input role, WIP와 completion-time probabilistic output
7. facility/operational buffer, expedition/virtual inventory와 no-penalty entity transport
8. legacy count admission, category-only spawn, untyped remove, save-DTO query와 duplicate writer 완전 제거

현재 slice exit 표:

| slice | 상태 | 이미 증명된 것 | 남은 exit gate |
|---:|---|---|---|
| 1 | complete | Foundation/Items 소유권, canonical gram, 공용 query, carry/haul/readers, 2ms/0B, full Physical PASS | source drift 시 재실행 |
| 2 | structural-partial | L01 authored 25,000g, Stored/remaining derived index, warehouse-local revision, generic partial token, terminal tombstone, idempotent receipt, first production ingress, affected-record rollback journal, canonical kg building/tab/card UI, mass/count summary separation, detached facility→physical exact restore join, official RestoreAll round-trip, valid over-capacity preservation/new-ingress block, post-root-swap evacuation request publication/cleanup, destination gram admission, actual AI evacuation 15,000g exact conservation, non-empty demolition/relocation rejection, empty lifecycle gate, orphan/position atomic rejection, focused + full PlayMode 0/0 | common cross-domain staged coordinator, unique/stateful lot, conveyor and remaining ingress |
| 3 | structural-partial | combat equipment base+attached module+실제 loaded ammunition exact gram, validated component→immutable prepared subject, durability/quality/worldState/powerCharge/module grade·condition 질량 불변, warehouse reservation/Stored index/receipt/restore rebind, crafted equipment 실제 AI 입고, prepared query 10,000회 p95≤2ms·0B, fresh full Physical 0/0 | apparel projector, 멜빵 1,150g once, carcass·packaged-lot projector, haul-owned mass mutation reject 회귀 |
| 4 | pending | 없음 | 모든 production ingress와 exact identity/admission cutover |
| 5 | partial-existing | typed recovery drop의 기존 focused evidence | drop-failure `RecoveryPending`, warehouse capacity shrink/lifecycle/save 결합 |
| 6 | pending | 일부 authored transform audit뿐 | typed disposition, input role, WIP, probabilistic output runtime/save |
| 7 | pending | 일부 destination claim/expedition 기존 증거 | 모든 buffer/virtual boundary와 entity-transport 상호배제 |
| 8 | pending | 없음 | semantic manifest 기준 legacy/compatibility/bypass 0 |

#### Gate S0 slice 3a 구현 증거: combat equipment dynamic mass

- 저장/월드 component는 `PhysicalItemMassSubjectAdapter`에서 정확히 한 번 decode·identity/module-slot 검증한다. mass query는 save DTO를 직접 받지 않는다.
- adapter는 base equipment definition, 부착된 module 수량, 실제 loaded ammunition item ID·remaining 수량만 immutable mass contribution으로 캡처하고 authoritative definition gram으로 prepared unit mass를 확정한다.
- hot query는 prepared gram을 직접 읽으므로 JSON 재파싱·LINQ·카탈로그 재조회가 없고, focused 10,000-operation benchmark의 p95 `≤2ms`, steady-state allocation `0B`를 통과한다.
- warehouse request는 exact item ID, instance ID, lot fingerprint와 prepared subject fingerprint를 대조한다. restore rebind도 restored physical stack component에서 같은 subject를 재구성하고 saved reserved grams와 exact 비교한다.
- 현재 실제 콘텐츠 회귀는 crafted dagger `700g`을 AIHaul이 warehouse `Stored`로 입고하고 `UnitWeight=0.7kg`, reserved inbound `0g`으로 정리되는 것을 증명한다. module/ammunition이 있는 focused fixture는 `base + module + ammo×remaining`을 exact 검증한다.
- `Artifacts/QA/physical-item-logistics-playmode-report.txt` UTC `2026-08-20T12:22:56.1216752Z`는 `[PASS] COMBAT_EQUIPMENT_STATEFUL_WAREHOUSE_MASS_EXACT`와 `RESULT=PASS; failures=0`을 포함한다. Coverage의 Physical verifier는 `LiveExecuted`; 전체 manifest `uncovered=80`은 다른 stale evidence이므로 Gate S0 전체 완료로 오인하지 않는다.
- 이 하위 슬라이스는 authored kg, WU, EWU, 가격, BOM을 변경하지 않았다. apparel·멜빵·carcass·packaged lot 및 모든 ingress가 닫히기 전에는 “unique/stateful mass 완료” 또는 “물리 중량 밸런스 완료”라고 보고하지 않는다.

Gate S0에서 다음을 전수 감사한다. Phase 3~5가 발견하는 recipe/instance 특수 경로는 해당 Gate S0 slice로 되돌려 폐쇄한 뒤 계속한다.

1. 모든 warehouse definition과 admission caller를 item-count capacity에서 canonical gram capacity로 전환하고 구 count admission 경로를 0으로 만든다.
2. warehouse-only authored mass field를 추가하고 `BuildingInternalStockAbility` logistics fallback, snapshot base-capacity duplication, `int.MaxValue` sentinel을 제거한다.
3. room storage gram과 shop internal/display item-count를 분리하고 차원 혼합 호출을 0으로 만든다.
4. exact destination gram lease를 haul/conveyor 공용으로 구현하고 deposit preflight/commit/release/rebuild를 원자화한다.
5. 창고 철거·이전을 empty stock/inbound 조건으로 fail-loud하고 orphan storage destination을 전수 검증한다.
6. category-only spawn/return과 virtual shop/equipment stock을 exact physical lot 또는 explicit Transform/Sink로 폐쇄한다.
7. 모든 recipe input을 product-bound consumed/process-fuel/catalyst/tool/container/infrastructure/packaging으로 분류한다.
8. cycle 시작부터 output commit까지 WIP 질량, 취소, 시설 파괴, output-capacity 실패와 current-format save/restore를 연결한다.
9. output count·maxStack·소비량 후보의 quantity 기반 order/contract/expedition/medical/construction consumer를 역참조한다.
10. fluid unit의 grams와 milliliters, clean/waste composition과 처리 facility capacity를 연결한다.
11. freshness·quality·equipment/apparel/carcass component가 있는 stack의 split/merge가 상태와 질량을 보존하는지 검증한다.
12. 농업·축산·채집 Source에 land/water/feed/time yield budget을 연결한다.
13. kg 변경 전후 AI planner의 urgent order latency, heavy order aging, tour item count와 starvation을 측정한다.
14. 시장·계약·원정의 unit reward와 kg당 물류비를 비교한다.
15. 현재 형식 save에서 carried/WIP/stack state를 round-trip하고 복원 직후 과중 상태가 삭제·순간이동하지 않는지 검증한다.
16. warehouse mass index의 revision·재구축·성능과 non-warehouse buffer peak kg/dwell time을 검증한다.
17. 초경량/maxStack 조합의 record fragmentation, save size, query p95와 deterministic compaction을 검증한다.
18. 모든 `Stored` destination을 warehouse/operational-buffer/recovery/debug로 분류하고 low-level warehouse-prefix mutation 우회를 0으로 만든다.
19. 모든 stack add/remove/split/merge/quantity/state/destination/component mutation을 manifest로 만들고 canonical mass index revision이 exact 1회 갱신되는지 검증한다.
20. combat equipment runtime aggregate와 physical component mirror, apparel component, carcass species/item 정의의 mass가 save·world·carry·equipped·warehouse에서 단일 gram으로 수렴하는지 검증한다.
21. 모든 production physical consume/remove caller를 closed disposition으로 분류하고 input/output/byproduct/loss receipt가 exact인지 검증한다.
22. expedition supply/loot의 physical-to-abstract-to-physical 전환, tare, carry burden, cancel/consume/return을 검증한다.
23. food/carcass spoilage, butchery와 상태 질량 변환을 atomic transform으로 전환하고 Stored destination·category evacuation·valid over-capacity를 검증한다.
24. injury/performance/harness capacity shrink의 cargo retention, pickup block, unload/recovery SLA와 cargo/equipped/total burden을 검증한다.
25. haul-owned cargo의 mass-affecting mutation을 금지하거나 destination gram lease delta와 원자 동기화한다.
26. medical rescue·wildlife/captive transport가 item kg/hard cap/속도 페널티를 사용하지 않으면서 path·ownership·interruption과 item-cargo 상호배제를 보존하는지 검증한다.
27. semantic symbol 기반 callsite manifest를 생성해 indirect interface/adapter/event/read-side를 포함한 모든 행에 closed migration disposition을 부여한다.
28. Captivity·Defense·Factions·Invasion·FacilityEvolution의 exact item 선택, operation-owned reserve, typed Transfer/Sink, terminal release와 current-format save join을 각각 폐쇄한다.
29. work/construction/repair, research/archive, survival/need, run/start-party/reward/event의 Source/Transfer/Transform/Sink를 같은 transaction receipt로 연결한다.
30. `EquipmentStoredEvent`, shop stock, expedition ledger처럼 physical lot이 사라지는 event/virtual 경계를 exact lot Transform 또는 명시적 terminal Sink로 분류한다.
31. 모든 domain operation의 transaction phase, commit/outbox publication, retry/prune와 subscriber failure를 공통 불변식으로 검증한다.
32. save section/participant의 required-predecessor graph를 생성해 physical/domain/WIP/token join과 AI wake fence를 증명한다.
33. 모든 count-capacity/generic-unit-weight reader를 common mass query로 전환하고 UI·summary·AI·settlement·diagnostics의 차원/typed failure 표시를 검증한다.
34. authoring origin→builder→SO projection→runtime query→ledger의 단방향을 manifest로 만들고 clean checkout/domain reload/second build의 gram·YAML identity를 검증한다.
35. prepare/source reserve/destination reserve/domain prepare/physical commit/domain commit/publication/save/restore/terminal retry fault matrix를 모든 migrated operation family에 적용한다.
36. compatibility adapter와 shadow comparison을 AuditOnly로 제한하고 numeric ApplyApproved 전에 production legacy/unknown/parallel-authority 행을 0으로 만든다.

산출물:

- `Artifacts/QA/v27-mass-storage-authority.csv`
- `Artifacts/QA/v27-mass-input-disposition.csv`
- `Artifacts/QA/v27-mass-wip-lifecycle.txt`
- `Artifacts/QA/v27-mass-quantity-consumers.csv`
- `Artifacts/QA/v27-mass-fluid-authority.csv`
- `Artifacts/QA/v27-mass-stack-state-roundtrip.txt`
- `Artifacts/QA/v27-mass-haul-fairness.txt`
- `Artifacts/QA/v27-mass-warehouse-lifecycle.txt`
- `Artifacts/QA/v27-mass-destination-capacity-leases.txt`
- `Artifacts/QA/v27-mass-exact-item-identity.csv`
- `Artifacts/QA/v27-mass-virtual-inventory-disposition.csv`
- `Artifacts/QA/v27-mass-buffer-peak-capacity.csv`
- `Artifacts/QA/v27-mass-stored-destination-manifest.csv`
- `Artifacts/QA/v27-mass-mutation-revision.csv`
- `Artifacts/QA/v27-mass-instance-authority-consistency.txt`
- `Artifacts/QA/v27-mass-physical-disposition.csv`
- `Artifacts/QA/v27-mass-expedition-burden-roundtrip.txt`
- `Artifacts/QA/v27-mass-in-place-transform.txt`
- `Artifacts/QA/v27-mass-dynamic-carry-capacity.txt`
- `Artifacts/QA/v27-mass-borne-load-dimensions.csv`
- `Artifacts/QA/v27-mass-entity-transport.csv`
- `Artifacts/QA/v27-mass-cross-system-callsite-manifest.csv`
- `Artifacts/QA/v27-mass-domain-authority-matrix.csv`
- `Artifacts/QA/v27-mass-transaction-publication-faults.txt`
- `Artifacts/QA/v27-mass-save-restore-order.txt`
- `Artifacts/QA/v27-mass-ui-presentation-matrix.csv`
- `Artifacts/QA/v27-mass-builder-projection-manifest.csv`

완료 조건:

- P0 unknown 0
- warehouse count-capacity legacy path 0
- generic/unique StoredMass mismatch 0g
- WIP orphan mass 0
- unresolved input disposition 0
- quantity consumer orphan 0
- fluid grams/volume authority 누락 0
- merge/split freshness·quality drift 0
- urgent/heavy order starvation 0
- current-format save mass/count/state drift 0
- warehouse orphan destination 0
- destination gram overcommit 0g
- live category-only physical spawn 0
- unresolved virtual inventory mass 0
- combat/apparel/carcass mass mapping mismatch 0g
- operational buffer bound unknown 0
- room storage kg + shop count dimensional addition 0
- implicit unlimited production warehouse 0
- mass-affecting mutation revision 누락·중복 0
- runtime aggregate/component mirror gram mismatch 0
- production untyped physical consume/remove 0
- disposition receipt unexplained residual 0g
- expedition burden/physical return mismatch 0g
- delete-before-uncommitted-output transform 0
- Stored transform destination loss 0
- capacity-shrink cargo delete/teleport 0
- stale in-transit destination reserved grams 0
- cargo/equipped/total dimension mismatch 0g
- unbounded entity transport 또는 emergency rescue hard-cap deadlock 0
- physical record performance budget failure 0
- warehouse low-level admission bypass 0
- unowned/unbounded non-warehouse Stored destination 0
- unclassified production callsite 0
- indirect adapter/event/read-side manifest orphan 0
- cross-domain partial payment/assignment/output/release commit 0
- pre-commit event/UI publication 0
- committed operation publication loss·duplication 0
- save participant predecessor cycle·missing join 0
- AI wake-before-cross-section-publication 0
- count-capacity/generic-mass gameplay reader 0
- typed failure localization/display dimension mismatch 0
- authoring origin/projected field duplicate writer 0
- clean checkout/builder/domain reload gram drift 0g
- production compatibility/shadow dual authority 0
- fault-matrix prior-world fingerprint mismatch 0

### Phase 5.5. kg·효능·WU·EWU 결합 감사

신규 kg 적용보다 먼저 현재 잠정 적용 12개 항목을 감사한다.

1. 12개 item의 모든 producer·consumer·non-recipe sink를 역참조한다.
2. Before/After payload/kg, WU/kg, EWU/kg, 가격/kg를 계산한다.
3. maxStack 총질량, recipe batch kg, 실제 운반 횟수와 storage days/cell을 계산한다.
4. 식품은 6인 food loop, 사료는 실제 축산 소비·생산·저장 loop에 대입한다.
5. 25% 이상 변화는 결합 리뷰, 50% 이상 변화는 Critical로 분류한다.
6. 각 항목을 `coupled-pass`, revise 또는 rollback 중 하나로 닫는다.
7. 변경 결과로 질량식이 달라지면 해당 producer/consumer transform을 다시 전수 감사한다.
8. 결합 리포트와 append-only baseline 근거를 생성한다.

우선순위는 건초·사일리지, 신선 응유·포도 시럽식, 야채/고기 파이, 주류, 나머지 식사 순이다. 건초·사일리지는 mass delta와 feed value/kg 변화가 50%를 넘으므로 첫 Critical 수직 슬라이스다.

완료 조건:

- 잠정 적용 12개 `provisional-applied=0`
- producer/consumer/non-recipe sink 누락 0
- unresolved payload/WU/EWU/price density Critical 0
- 모든 변경 item에 coupled disposition과 baseline record 존재
- focused mass artifacts와 결합 리포트가 동일 source digest를 사용

### Phase 6. approved kg 적용

- AuditOnly artifact 리뷰
- Phase 5.5 coupled-pass 또는 명시적 revise/rollback 완료
- exact approval
- builder authority 변경
- changed-only SO apply
- YAML second-run zero diff

구조 기반 Gate S0와 Phase 5.5가 끝나기 전에는 추가 item `unitWeight`를 ApplyApproved로 반영하지 않는다. recipe fluid/loss 계약도 downstream utility·throughput 영향이 50% 이상이면 같은 결합 게이트를 적용한다.

### Phase 7. 물류·EWU·가격 재생성

- haul trip/handling 재계산
- V27 ledger 재생성
- pricing 재생성
- SCC/arbitrage
- approval expiry/root-cause tree

### Phase 8. 생산-live 회귀

- PhysicalItemLogistics full
- mid-action save/load
- facility input/output/construction/repair/surgery
- equipment world/carry/equip
- six-adult food/water N+1
- floor clutter paired run
- population stage capacity
- DailyRoutine 3 seed
- Console 0/0

### Phase 9. 실전 보정

- 5일 × seed 157181/157182/157183
- haul kg 분포
- trip count
- logistics WU share
- food/storage backlog
- primitive fallback
- 멜빵 사용률과 고장 비용
- ordinary overload frequency

목표를 벗어나면 carry 한도를 먼저 계속 올리지 않는다. unit meaning, batch size, mixed haul bundling, storage 위치와 레시피 물류를 원인별로 조정한다.

---

## 17. 테스트 계획

### 17.1 단위 테스트

- kg↔gram canonical conversion
- 1g 경계
- 0/음수/overflow
- density×volume
- density×volume×packing permille의 `1,000,000` 분모와 checked overflow
- bulk density와 packing 공극 이중 할인 거절
- packing tare
- reusable container exact return
- disposable packaging waste/loss
- partial packed-stack consumption
- cancel-before-consume tare mutation 0
- repeated commit container duplication 0
- BOM 합산
- 다중 output allocation
- explicit loss exact
- probability branch min/max
- output line 순서 shuffle 뒤 key-addressed realized outcome 동일
- 다중 독립 probability/fractional line의 complete outcome 저장·복원·재시도 재굴림 0
- physical output와 special handler output의 all-or-nothing commit 및 같은 commit ID 재호출 mutation 0
- Source/Transform/Sink
- multi-producer conflict
- maxStackMass
- generic stack quantity × unit grams 창고 mass 합산
- unique equipment/apparel instance mass 창고 합산
- wildlife carcass species/item exact mass 합산
- remaining grams 기반 partial deposit floor
- stored + inbound-reserved grams 기반 admission
- concurrent haul/conveyor capacity lease exact-one commit
- destination capacity lease timeout/heartbeat/cancel exact release
- admission receipt exact lot/quantity/grams/revision, commit ID idempotency, transaction fault rollback
- 구 item-count capacity admission 호출 0
- ordinary/harness quantity floor
- material/equipment/apparel projection
- input disposition별 mass 포함/제외
- WIP stage별 exact lot ownership과 설명 가능한 gram 귀속
- output reservation 실패 후 input 재소비 0
- Sink package return capacity token 실패 시 내용물 소비 0
- output buffer footprint에 main output·byproduct·returned package를 포함하고, 동시 completed WIP의 resolved-output reservation을 remaining capacity에서 별도 차감
- fluid grams/unit와 milliliters/unit 독립 계산
- disposition command exact input slice/gram capture
- `ConsumedByNeed`, `Transformed`, `WasteProcessed`, `SoldOffMap`, `DestroyedByHazard`별 output/byproduct/loss closure
- production untyped consume/remove caller manifest 0
- expedition loot burden cap과 physical return gram exact 변환
- expedition supply package tare·소비·취소·귀환 receipt
- Stored food spoilage keeps warehouse destination and changes grams atomically
- spoiled output category mismatch preserves stock and raises evacuation-required
- heavier in-place output creates valid over-capacity and blocks only new admission
- carcass rot/butchery multi-output failure leaves one WIP obligation, input/output duplication 0
- injury/performance drop after pickup preserves cargo, blocks pickup and schedules bounded unload
- hauling harness removal/breakage after pickup does not teleport cargo and does not leave stale capacity
- cargo/equipped/total borne grams exact, harness mass counted once
- haul-owned module/ammunition/package mass delta resizes destination lease exactly or rejects mutation
- rescue patient/wildlife transport applies no item-kg admission or speed penalty and preserves exact path/ownership/interruption
- entity transport blocks new opportunistic item haul and second entity ownership
- entity transport interruption restores subject occupancy/parent and preserves unrelated item cargo
- current-format V25 root/V8 production/V4 warehouse restore 순서와 old-version typed rejection
- faction multi-stack payment late-failure leaves all input lots and domain balance unchanged
- captivity durable-tool assignment is an exact Transfer; captive release/death/cancel returns or disposes the same instance once
- defense/invasion repeated supply polling creates one exact pending commitment and terminal cleanup removes it once
- facility-evolution multi-line request failure returns every exact lot/token without destination-wide collateral release
- event subscriber exception after commit creates one pending outbox publication and no repeated physical mutation
- every save/restore participant predecessor graph is acyclic, complete and AI-wake fenced
- every typed failure code has one localization/presentation mapping and no gameplay parser consumes display text
- clean builder/catalog reload reproduces every projected gram and source digest exactly

### 17.2 Property·Metamorphic

- BOM 행 순서 shuffle
- item catalog 순서 shuffle
- recipe output 순서 shuffle
- 같은 BOM을 여러 행으로 분할해도 총질량 동일
- stack을 여러 개로 분할해도 총질량 동일
- packed consumable stack을 분할 소비해도 `내용물 Sink + empty container/waste + declared loss` 총질량 동일
- Loose→Stored→Carried→FacilityBuffer 전이에서 질량 동일
- active plan cancel은 carried item 위치를 source/destination으로 순간이동시키지 않음
- Downed/Dead transition은 last actor cell에 exact `TransientCarryRecoveryDrop` 생성
- transient recovery drop 회수 뒤 총질량·수량·owner cleanup exact
- save→restore→resave에서 질량 동일
- current-format WIP save→restore 후 cycle sequence·consumed slices·pending output 동일
- output count 또는 maxStack 변경 시 quantity consumer manifest가 순서와 무관하게 동일
- freshness bucket 경계 split/merge에서 보수적 freshness와 quality exact
- catalyst/tool/container 반환 후 identity·quantity·내구 exact
- module attach/detach 질량 차 exact
- material variant 변경 외 unrelated state가 kg를 바꾸지 않음
- worldState만 바뀌어도 kg 동일
- category catalog·insertion order shuffle 뒤 exact item 생산/반환 identity 동일
- expedition route reward 순서 shuffle 뒤 source receipt와 return physical gram 동일
- packed supply consume/cancel 순서에서 tare·remaining lot 복제·삭제 0
- 동일 disposition commit ID 재실행 뒤 second mutation 0
- spoilage output spawn fault injection 전후 total input/WIP/output grams exact
- warehouse policy/catalog 순서 변화가 existing transformed stock을 삭제·eject하지 않음
- capacity multiplier/harness state oscillation does not duplicate recovery drops or repeatedly consume cargo
- unrelated durability/charge mutation leaves mass and destination lease revision unchanged
- living-body profile change does not mutate carcass item mass authority
- warehouse demolition/relocation은 stock 또는 inbound가 하나라도 있으면 world hash mutation 0
- empty warehouse relocation 뒤 destination authority·position exact, orphan Stored 0
- stored/carry/equipped apparel 및 carcass mass query 동일
- component mutation 뒤 warehouse gram index revision exact 1회, no-op mutation 0회
- 동일 kg를 다양한 maxStack/record 분할로 표현해도 admission·mass·save semantic hash 동일
- compatible compaction 전후 exact mass/quantity/state, incompatible state merge 0
- warehouse prefix direct spawn/route without token is rejected with world hash mutation 0
- every non-warehouse Stored destination has exact owner, capacity and terminal/retrieval test
- unique combat equipment, apparel and other component-backed item share one admission result for equivalent grams/category
- semantic callsite manifest is invariant to file enumeration order, adapter layering and interface alias spelling
- adding an unregistered production caller causes `MASS_CALLSITE_MANIFEST_DRIFT` regardless of method-name similarity
- event subscription order shuffle changes neither commit receipt nor outbox publication count
- save participant registration order shuffle that still satisfies declared predecessors yields the same restored semantic hash
- invalid participant reorder that violates a predecessor fails before candidate publication
- UI/diagnostic query order and panel visibility do not change mass/warehouse revisions
- direct Inspector edit, deterministic builder rerun and clean-checkout generation converge to the same projected grams

### 17.3 경제 무결성

- kg 변경 전후 payload/kg exact 계산
- producer recipe 추가·삭제 시 consumer closure 갱신
- 같은 중간재의 모든 producer/consumer unit mass 동일
- 25%·50% 결합 감사 threshold 경계
- feed value/kg·nutrition/kg·potency/kg category anomaly
- WU/kg·EWU/kg·buy/sell price/kg root-cause attribution
- maxStack 총질량과 partial pickup 가능성
- storage days/cell Before/After
- storage kg capacity와 physical stack/pile count를 별도 검증
- contract reward/kg·expedition supply-days/kg·order WU/kg
- Source yield의 land/water/feed/time budget
- urgent 소량과 heavy order의 bounded wait/fairness
- `provisional-applied` 항목의 최종 gate 진입 금지
- revise/rollback 후 모든 transform residual 0g

- 제작→해체→재제작
- 구매→제작→판매
- facility kit→건설→해체
- 장비 제작→수리→해체
- food cook→spoil→waste
- bottled water consume→empty bottle→wash/refill
- medicine consume→used vial/kit waste 또는 declared contamination loss
- SCC margin 0 미만
- mass audit tolerance 0g

### 17.4 EditMode 전수 감사

- canonical items `current/current` (2026-08-26 snapshot `414/414`)
- recipes `current/current` (2026-08-26 snapshot `355/355`)
- combat mappings 61/61
- unit semantics missing 0
- builder producers connected
- mass Critical 0
- duplicate authority 0
- warehouse count-capacity legacy caller 0
- warehouse stored-mass mismatch 0g
- warehouse destination-capacity lease overcommit 0g
- non-empty warehouse demolition/relocation accepted 0
- orphan warehouse storage destination 0
- live category-only item spawn caller 0
- virtual physical stock without mass disposition 0
- combat/apparel/carcass double-authority mismatch 0g
- machine/FacilityBuffer peak-mass bound unknown 0
- production warehouse implicit unlimited 0
- warehouse record fragmentation performance failure 0
- low-level warehouse Stored bypass 0
- Stored destination manifest orphan 0
- recipe input disposition unknown 0
- WIP lifecycle authority missing 0
- quantity semantic consumer orphan 0
- fluid gram/volume authority missing 0
- unclassified cross-system callsite 0
- production compatibility/legacy disposition 0
- indirect event/adapter physical ownership orphan 0
- transaction operation/commit/rollback owner missing 0
- save section/participant predecessor missing or cyclic 0
- pre-commit gameplay event/UI publication 0
- count-capacity or generic-mass gameplay reader 0
- typed failure localization mapping missing 0
- authoring origin/projected-field duplicate writer 0
- builder/catalog reload projection drift 0g

### 17.5 PlayMode

- 일반 actor 약 19/29kg
- 멜빵 actor 약 24/36kg
- 육중한 체격 조합
- 6~11kg recipe input
- 8~14kg mixed haul
- 15~20kg heavy batch
- 20kg 초과는 oversize equipment만
- urgent underfill 즉시 운반
- carry speed curve
- partial quantity pickup
- reservation/in-transit/deposit 보존
- two haulers + conveyor simultaneous last-capacity contention exact-one winner, loser cargo retained
- full warehouse deposit rejection leaves carry/lease/intent exact and floor drop 0
- single item heavier than empty warehouse typed rejection before pickup
- non-empty warehouse dismantle/relocation typed rejection and stock position unchanged
- empty warehouse relocation followed by deposit at new exact stand cell
- shop restock cancel returns same item ID/instance/components/mass
- production output commits exact authored item ID, category-first substitution 0
- pre-pickup cancel lease-only release
- active carrier replan retains physical cargo
- Downed/Dead carrier physical drop at exact actor cell
- transient recovery drop grace/SLA와 no teleport
- Despawn/world transfer uses official transfer authority and creates no world duplicate
- construction/repair/surgery/research archive
- bottled water/medicine packaged Sink exact tare recovery
- equipment physical world state transitions
- installed module·loaded ammunition mass revision exact; durability·quality·freshness·contamination·wetness·charge mutation mass revision 0
- hauling harness physical/equipped mass exact `1,150g`, duplicate count 0
- rescue/captive/wildlife transport item-kg admission 0, movement penalty 0, path·ownership·interruption leak 0
- six-adult food/water loop
- floor clutter and recovery
- production output-capacity blocked state에서 input 재소비·output 복제 0
- probabilistic output completion roll exactly once, persisted branch restore, reroll 0
- FacilityBuffer capacity 2~4 batches, producer direct-haul ownership 0, separate AIHaul delivery
- passive batch 중 cancel/facility destroy/save-restore WIP exact
- freshness·quality stack split/merge state exact
- urgent haul latency와 heavy-order aging bounded
- current-format restore over-capacity cargo 유지/재계획, 삭제·순간이동 0
- current-format restore reconstructs pickup-committed/conveyor inbound gram leases exactly once before AI wake
- tampered/missing warehouse owner·position·category restore rejection과 whole-transaction rollback fingerprint exact
- 1g-class high-count stock의 deterministic compaction과 query/save p95 budget
- public spawn/route/transit의 warehouse token enforcement와 apparel recovery-buffer bounded cleanup
- faction payment, captivity tool, defense/invasion supply and facility-evolution release focused live transactions
- commit 이후 event subscriber fault에서 physical/domain state 유지, outbox exact-one 재발행
- scene unload/domain disable 중 publication retry가 quest/stat/audio/UI side effect를 중복 생성하지 않음
- restore prepare/publish/complete 각 phase fault에서 prior world·lease/token·domain·AI registration fingerprint exact
- character/work/warehouse/production/shop/offense UI가 같은 gram query와 typed capacity reason을 표시
- count-era `무제한/남은 칸` warehouse 문구와 gameplay count admission 0
- clean checkout builder→catalog reload→PlayMode가 Audit artifact와 같은 gram/source digest 사용

### 17.6 실전 지표

최소 다음을 raw seed별로 보존한다.

```text
haulCount
haulKgP50
haulKgP75
haulKgP95
under6KgCountByReason
over19KgCountByReason
overloadSeconds
harnessHaulCount
ordinaryActorOverloadCount
tripCountByRecipe
logisticsWuShare
facilityInputWait
foodReserveDays
storageUtilization
storageStoredMassGrams
storageMaxMassGrams
storageStackCount
floorClutterCellSeconds
urgentOrderLatencyP95
heavyOrderAgeP95
wipBlockedSeconds
outputCapacityFailureCount
```

### 17.7 성능·전수 연결 게이트

- 공식 성능 환경은 PR manifest에 Unity Editor version, scripting runtime/backend, CPU, build configuration과 OS를 고정한다. Unity Editor Mono에서 Editor instrumentation을 끈 dedicated runner를 권위 표본으로 사용한다.
- 공통 fixture: cache/registry 사전 구성, warm-up 10회, 측정 100회, **한 측정당 canonical 10,000 operation batch**. fixture 생성, source capture, 파일 I/O, hashing, logging, GC collect는 timed region 밖이다.
- mass query와 warehouse admission: 각각 10,000-operation batch p95 `<=2ms`, steady-state thread allocation `0B`. allocation은 현재 thread의 timed-region delta로 측정한다.
- haul planner: 동일 fixture의 변경 전 baseline 대비 p95 회귀 `<=10%`; mass cache rebuild는 별도 표본으로 분리
- save capture/restore: wall-time·allocation·serialized bytes를 별도 기록하고 gameplay hot-loop `2ms` 예산에 섞지 않음
- 비권위 개발 머신에서는 동일 fixture의 Before 대비 `<=10%` 회귀를 보조 신호로 기록할 수 있으나 공식 `2ms/0B` 게이트를 완화하거나 대체하지 않는다.
- manifest는 migrated/remaining callsite, exact-lot producer/consumer, admission-token bypass, restore join, WIP owner, buffer classification과 evidence ID를 기록
- 최종 `remaining=0`, `orphan=0`, `bypass=0`, `duplicateWriter=0`을 요구

---

## 18. 완료 게이트

### 밸런스 기준 배정

- 이 문서와 목표 밴드 확정
- 구현 전 구조 권위 확정

### 밸런스 공식 검증

- item semantic `current/current` (2026-08-26 snapshot `414/414`)
- recipe mass audit `current/current` (2026-08-26 snapshot `355/355`)
- equipment mapping 61/61
- Transform residual 0g
- undeclared loss 0
- item `current/current` producer/consumer/non-recipe sink closure (2026-08-26 snapshot `414/414`)
- kg 변경 항목 payload/kg·WU/kg·EWU/kg·price/kg 결합 감사 100%
- 잠정 적용 12개 `provisional-applied=0`
- 구조 기반 Gate S0 P0 unknown 0
- warehouse count-capacity legacy path 0
- warehouse stored-mass mismatch 0g
- WIP/input-disposition/fluid/quantity-consumer orphan 0
- unresolved coupling Critical 0
- EWU/SCC/arbitrage 통과
- cross-system callsite manifest production row 100% classified, unknown/compatibility/legacy 0
- every domain row has target command, capacity/save/rollback owner and fresh evidence ID
- transaction publication fault matrix unresolved phase 0
- save/restore predecessor graph cycle·orphan·AI-wake violation 0
- gameplay read-side count-capacity/generic-mass legacy reader 0
- authoring origin→builder→runtime projection drift 0g

### 밸런스 시뮬레이션 검증

- 256-seed 물류/공간
- six-adult food/water loop
- clutter paired run
- normal/harness carry bands
- ordinary/heavy/oversize 분포
- maxStack 총질량·storage days/cell 결합 목표 통과
- 모든 제한 창고가 canonical kg capacity admission과 UI를 사용함
- 정상 storage 70%, 장애 storage 90%, 7일 비축 kg 통과
- output-capacity·WIP·stack-state current-format save 회귀 통과
- urgent/heavy haul starvation 0
- 음식 nutrition/kg와 사료 feed value/kg가 승인된 category band 내에 있음
- Captivity·Defense·Factions·Invasion·FacilityEvolution live transaction conservation PASS
- event subscriber/scene unload publication retry duplication 0
- restore phase fault injection prior-world fingerprint mismatch 0
- UI/summary/AI/settlement/diagnostics mass and capacity dimension mismatch 0
- clean checkout builder/catalog reload/source digest identity PASS
- Console 0/0

### 밸런스 실전 보정

- 5일 × 3 seed
- 물류 12~20%
- 생존 반복 노동 25~35%
- 성장 35% 이상
- 비상 10% 이상
- food gross/net 125/110%
- 7일 비축
- ordinary haul p75 14kg 이하
- 일반 반복 과적 사용률 5% 이하, 목표 0%
- kg 변경 후 payload·처리량·저장·가격 지배 전략 0
- 멜빵 없이도 6인 생존망 정상
- unresolved Critical 0
- 두 번째 artifact/apply Git diff 0

위 단계가 모두 끝난 뒤에만 `밸런스 실전 보정 완료`라고 보고한다.

---

## 19. 기준서 append-only 기록 계획

기존 `architecture:v27-physical-mass-runtime-authority-plan-v1`~`v4`와 `architecture:v27-physical-mass-canonical-gram-authority-v1`은 append-only 역사로 유지한다. 이 Revision v5 문서가 동결되면 새 `architecture:v27-physical-mass-execution-model-plan-v5`를 append한다. v5에는 current-source 분모, Phase 순서를 supersede하는 Batch A~H, 병렬 안전 레인, family proposal→anomaly-only review→changed-only apply, 가중 진행 보고와 이번 **계획서 전용 교정에서 gameplay/asset을 변경하지 않았음**을 기록한다. 과거 record의 당시 상태 문장을 수정하지 않는다.

계획 digest는 checkout의 CRLF 정책과 무관하도록 **UTF-8 without BOM + LF로 canonicalize한 전체 문서 byte**의 SHA-256으로 정의한다. PR manifest와 baseline은 같은 canonicalization을 사용하며 working-tree native newline hash를 권위로 쓰지 않는다.

실제 수치를 적용할 때 `docs/game-design/whole-game-balance-baseline.md`에 다음 17필드 기록을 append한다.

권장 record ID:

```text
balance:v27:physical-mass-and-hauling-capacity
```

기록 내용:

1. 전 시대 모든 물리 아이템·운반·생산에 적용
2. 일반 19/29kg, 멜빵 24/36kg 목표
3. current-source item unit semantics 414개
4. 물리 BOM·density·loss
5. 레시피 6~11kg, mixed haul 8~14kg
6. heavy 15~20kg, oversize equipment 예외
7. direct WU 불변 여부와 물류 WU 변화
8. Embedded EWU/가격 Before→After
9. 생산·운반 시간
10. storage·buffer·공간
11. 전력·물·연료·폐기물
12. 과적·clutter·no-path 위험
13. 멜빵·육중한 체격·자동화 대안
14. 지배 전략 방지
15. 질량·경제 순환 차익 방지
16. 실제 AI/물류 실행 경로
17. 저장 권위·전수 감사·PlayMode·3-seed 증거

수치가 적용되기 전에는 기준서에 “완료” 기록을 미리 쓰지 않는다.

---

## 20. 최종 불변식

- 건강한 평범한 actor의 목표는 약 `19kg 무감속 / 29kg 최대`다.
- 멜빵 actor의 목표는 약 `24kg 무감속 / 36kg 최대`다.
- 멜빵은 25% 보너스를 유지하고 생존 필수 장비가 아니다.
- 모든 canonical item은 `1개` 의미를 가진다.
- kg는 물리 제약이며 단독 밸런스 목표로 사용하지 않는다.
- 같은 중간재는 모든 producer와 consumer에서 전역 단위질량 하나만 사용한다.
- 공정 중 일시 상태는 recipe 내부 fluid/loss/byproduct로 처리하고 독립 물류 의미가 없으면 별도 아이템으로 분할하지 않는다.
- 일반 물리 kg의 definition authority는 하나다.
- unique 장비·의복은 component 기반 실제 인스턴스 kg를 사용한다.
- 장착 모듈과 loaded ammunition만 dynamic mass를 바꾸며 durability·quality·freshness·contamination·wetness·charge는 질량 불변이다.
- 멜빵 physical/equipped 질량은 `1,150g` 단일 권위에서 정확히 한 번 계산한다.
- world, carry, equipped, save/restore kg는 동일 source에서 계산한다.
- 일반 레시피 입력 묶음은 기본 `6~11kg`다.
- 실제 compatible mixed haul은 기본 `8~14kg`다.
- 무거운 묶음은 `15~20kg`다.
- 일반 반복 공정은 최대 과적을 정상 처리량으로 쓰지 않는다.
- 20kg 초과 일반 generic item은 fail-loud한다.
- 과적 구간은 정말 무거운 unique 장비와 명시적 예외에만 사용한다.
- 시설 키트는 전체 BOM이 아니라 bounded packed subassembly다.
- Transform 질량 residual은 `0g`다.
- 수분·절삭·제련·연료·폐기물 손실은 반드시 typed·명시적이다.
- 질량 감사에는 tolerance를 적용하지 않는다.
- EWU 입력 Ceil·출력 Floor와 SCC tolerance 0을 유지한다.
- kg 적용 후 물류 EWU와 가격을 반드시 재생성한다.
- kg 적용 후 payload/kg, WU/kg, EWU/kg, price/kg, maxStack 총질량과 storage days/cell을 반드시 재검증한다.
- 창고 admission capacity는 canonical gram 합계 하나를 권위로 사용하며 별도 packed-volume stat을 만들지 않는다.
- generic stack은 `quantity × unitMassGrams`, unique item은 instance mass projector 결과만큼 창고 capacity를 사용한다.
- item count와 maxStack은 pile/부분 pickup 진단이며 창고 admission capacity가 아니다.
- 현재 V27 창고 kg capacity는 7일 비축, 정상 70%, 장애 90%와 footprint/headroom을 함께 만족하도록 새로 배정한다.
- warehouse admission은 physical Stored mass와 active inbound destination gram lease를 함께 차감한다.
- 창고 포화는 carried 화물을 바닥에 버리거나 operation을 완료할 권한이 아니다.
- stock·inbound commitment가 남은 창고는 철거·이전할 수 없다.
- category는 정책 key일 뿐 physical item identity가 아니며 live 생산·반환은 exact item/instance lot을 보존한다.
- shop·buffer·event inventory가 경제적 physical asset을 흡수하면 exact mass disposition 없이는 완료할 수 없다.
- combat equipment, apparel, wildlife carcass의 authored/projected/physical gram 값은 공용 projector 계열에서 exact 일치한다.
- warehouse mass index는 derived cache이며 physical stack과 component revision에서 재구축 가능해야 한다.
- production warehouse는 명시적 양수 kg capacity를 가지며 implicit unlimited sentinel을 쓰지 않는다.
- 초경량 item의 많은 record는 compatible-state compaction과 성능 gate로 다루며 item을 count-capacity로 다시 제한하지 않는다.
- `Stored`는 warehouse 동의어가 아니며 모든 destination은 typed owner/capacity/terminal contract를 가진다.
- warehouse destination으로의 모든 상태 전이는 exact gram capacity commit token을 요구한다.
- current-format restore는 warehouse Stored destination을 exact facility candidate와 대조하며 valid over-capacity만 보존한다.
- 모든 recipe input은 product-bound consumed/process-fuel/catalyst/tool/container/infrastructure/packaging 중 하나의 mass disposition을 가진다.
- consume 시점부터 output commit까지의 재공품은 명시적 WIP 질량 권위를 가지며 cancel·destroy·output failure·current-format save에서 삭제·복제되지 않는다.
- output 공간이 확보되지 않으면 이미 소비한 input을 재소비하지 않고 같은 WIP가 blocked 상태로 남는다.
- 확률 output은 완료 시 정확히 한 번 결정·저장하고 실제 branch grams만 FacilityBuffer에 예약하며 재시도·복원에서 재굴림하지 않는다.
- 확률·다중 line output은 stable output line ID와 key-addressed roll로 완전한 realized outcome을 먼저 저장하고 physical/special output을 하나의 commit ID로 원자 생성한다.
- FacilityBuffer는 별도 AIHaul이 비우는 2~4회분 bounded physical output authority이며 생산자가 완성품을 직접 운반하지 않는다.
- output count·maxStack·소비량 변경은 모든 quantity 기반 주문·계약·원정·의료·건설 소비자 전수 감사 없이는 적용하지 않는다.
- fluid는 grams와 hydraulic volume을 모두 가진 단일 권위에서 읽으며 wastewater라는 이름으로 조성·처리비를 숨기지 않는다.
- stack merge/split은 mass뿐 아니라 freshness·quality·instance state를 보존한다.
- 살아 있는 환자·포로·생포 동물 운반은 item kg/hard cap/속도 페널티를 사용하지 않으며 전용 path·ownership·interruption 계약만 적용한다.
- 합법적인 Source도 land·water·feed·time·WU 경제 폐쇄를 가져야 한다.
- kg 변경으로 urgent 소량이나 heavy order가 starvation되지 않는다.
- 과거 세이브 마이그레이션은 범위 밖이지만 현재 형식 save의 carry·WIP·stack round-trip은 필수다.
- current-format schema version·restore participant 순서는 4.4 표와 일치하며 old version은 fallback 없이 typed rejection한다.
- 모든 AuditOnly/ApplyApproved는 current source inventory를 다시 캡처하고 기대 stable ID 집합·개수·digest drift 시 중단한다.
- kg 25% 이상 변화는 producer/consumer 전수 결합 감사, 50% 이상 변화는 Critical 리뷰 없이는 확정하지 않는다.
- `assetApplied`는 `coupled-pass`와 동일하지 않으며 provisional 적용값은 완료 게이트를 통과할 수 없다.
- 6인 음식·물·비축·저장·공간·N+1을 다시 검증한다.
- builder와 asset이 같은 권위를 사용한다.
- 승인된 changed asset만 수정한다.
- 두 번째 생성·적용은 Git diff 0이다.
- 공식·컴파일만 통과한 상태는 밸런스 완료가 아니다.
- 구현 브랜치는 이 계획 파일을 명시적으로 추적하고 수정 후 SHA-256을 PR manifest와 baseline record에 결합한다.

---

## 21. Revision v6 결정 폐쇄·잔여 위험·GO/NO-GO

### 21.1 구조 결정 폐쇄표

| 질문 | 확정 결정 | 다시 열리는 조건 |
|---|---|---|
| 일반 운반 권위 | nominal 25kg, 대표 성능 19/29kg, 멜빵 24/36kg | 3-seed 실전 분포가 목표를 벗어나고 item/batch/layout 원인으로 설명되지 않을 때 |
| 저장 capacity 차원 | positive `long` gram 단일 admission 권위. count는 재고/주문 표시용 | 신규 packed-volume gameplay를 별도 승인할 때만 |
| 일반·unique 질량 | generic=`quantity×definition grams`, unique=component projector | 새 mass-affecting component를 추가할 때 |
| 상태 질량 | module·loaded ammunition만 가변. durability/quality/freshness/contamination/wetness/charge 불변 | 실제 물리 component와 전수 migration 계약이 생길 때 |
| exact identity | category는 선택 정책, commit은 exact item/instance/lot | 없음. production category-only physical spawn은 금지 |
| 입고 동시성 | source quantity lease + warehouse-local gram token + staged physical/domain commit | 없음. global repository revision 기반 correctness는 금지 |
| 창고 over-capacity | valid owner는 보존·신규 입고 차단·evacuation; invalid owner는 restore rollback | 없음 |
| 운반 중단 | active cargo 유지/replan, Downed/Dead exact current-cell drop, 실패 시 `RecoveryPending` | formal off-world transfer 경계만 별도 |
| 포장 tare | reusable return, physical waste, declared destruction, transferred tare 중 정확히 하나 | current-source 414 item semantic 중 packaged family에서 disposition 선택 필요 |
| 생산 입력 | consumed/product fuel/catalyst/tool/container/infrastructure/packaging closed set | 새 input role이 실제 콘텐츠에 생기면 schema+전수 감사 |
| 확률 출력 | completion 시 key-addressed 1회 결정·WIP 저장·동일 outcome exact commit | 없음 |
| 생산 출력 | 2~4회분 bounded physical FacilityBuffer, 별도 AIHaul | 실제 clearance p95가 4회분을 초과하면 buffer 확대가 아니라 Critical 리뷰 |
| 물리 제거 | typed Source/Transfer/Transform/Sink command+receipt | production raw remove 호출은 허용하지 않음 |
| save | current-format strict join, pre-pick token 재계획, committed receipt/outbox 보존, old migration 없음 | source version drift 시 표부터 갱신 |
| living entity transport | item kg/hard cap/speed penalty 밖, 전용 단일 ownership·path·interruption | 신규 entity burden gameplay를 별도 승인할 때 |
| read side | gameplay gram, UI kg projection, count/slot과 라벨 분리 | 없음 |
| 적용 | AuditOnly→approval→changed-only apply→second-run zero diff | source/asset digest drift 시 승인 만료 |

위 표에서 구조적으로 사용자 결정을 기다리는 항목은 없다. current-source unresolved semantic 51개, package disposition, oversize 예외와 실제 balance After 중 family policy로 결정할 수 없는 anomaly만 AuditOnly 후보와 dependency evidence를 묶어 사용자에게 묻는다. 나머지 363개 semantic projection과 정상 family 행은 deterministic generator와 자동 검증으로 처리하며, 증거 없이 display name만 보고 추측하지 않는다.

### 21.2 잔여 위험과 방어

| 잔여 위험 | 계획상 방어 | 완료 증거 |
|---|---|---|
| interface/adapter/reflection을 통한 새 우회 호출 | Roslyn semantic symbol manifest + explicit domain registration + source digest drift | production row 100%, unknown/legacy/bypass 0 |
| Unity callback/subscriber가 commit 중 예외 | staged candidate + main-thread commit guard + durable post-commit outbox | fault matrix prior-world fingerprint 또는 exact committed-pending resume |
| unrelated stack mutation이 admission을 무효화 | warehouse-local monotonic revision + exact source lot revisions | two-warehouse interference property test |
| token expiry/scene unload/DI 재생성 | terminal tombstone, owner-operation receipt, derived restore rebuild | expiry·scene unload·save/load idempotency |
| content-specific 질량 의미 오류 | current/current explicit semantic, producer/consumer/sink closure, 25/50% review gates | missing semantic 0, coupling Critical 0 |
| WIP와 special output 부분 commit | realized outcome persistence + prepare-all/commit-all coordinator | line별 fault injection, reroll/duplicate 0 |
| 저장 복원 시 조기 AI 실행 | predecessor graph + reversible participant publication + AI wake fence | every-phase restore fault prior-world exact |
| transaction/index 성능 악화 | warehouse-local revision, derived cache, fixed 10,000-op benchmark | query/admission p95<=2ms 0B, planner<=10% regression |
| 수치상 보존되지만 플레이 경험 악화 | 6인 loop, 256-seed layout/clutter, urgent/heavy fairness, 3-seed live | throughput/storage/Wait WU/carry bands 모두 통과 |
| 문서와 구현의 재드리프트 | canonical plan hash + baseline v4 + plan-conformance CI | source symbol/contract/hash match |
| 미래 콘텐츠 추가 시 콘텐츠별 분기·저장 필드 증식 | typed capability interface + deterministic generated registry + canonical state envelope + synthetic canary | core-content-specific branch/manual allowlist/unregistered capability 0, canary core diff 0 |

이 표는 위험이 “없다”는 선언이 아니라 각 위험을 자동 실패로 바꾸는 계약이다. 새로운 시스템이 생기면 semantic manifest drift가 계획을 다시 열어야 하며, 기존 분모에 조용히 제외해서는 안 된다.

### 21.3 현재 GO/NO-GO

- **GO:** Revision v6 Batch A~D를 dependency graph대로 진행한다. Batch A exact output closure와 Batch C의 read-only manifest, Batch D AuditOnly proposal은 병렬 준비할 수 있고, 공통 contract·save schema·Unity integration은 root가 직렬 통합한다. 각 batch는 capability registry와 synthetic canary를 같은 exit gate에 포함한다.
- **NO-GO:** 현재 global repository revision 기반 admission 초안을 그대로 production ingress에 연결하는 것.
- **NO-GO:** slice 2 전체 exit gate 전에 slice 3~8을 병렬로 production cutover하는 것.
- **NO-GO:** Gate S0 전체, Phase 5.5 결합 감사 전에 신규 item kg·효능·WU·EWU·가격을 추가 ApplyApproved하는 것.
- **NO-GO:** compile 또는 단일 focused PASS를 다른 시스템 연계 완료·밸런스 완료로 보고하는 것.
- **NO-GO:** 새 item/recipe/facility/effect를 지원하기 위해 production core에 stable ID별 `if`/`switch`, prefix 추론, 수동 allowlist, 콘텐츠별 save 필드를 추가하는 것.
- **NO-GO:** 실제 변화 축이 없는 speculative interface/wrapper를 늘린 뒤 이를 미래 확장성 증거로 보고하는 것.

따라서 Revision v6는 구현 순서·병렬 소유권·자동 authoring·capability 확장 계약·실패 정책까지 결정된 **extension-oriented implementation specification**으로 판정할 수 있다. 그러나 unresolved semantic 51, 전수 disposition/capacity closure, synthetic canary, content-ID branch ratchet, fault evidence와 최종 밸런스 증거가 남아 있으므로 **extension-closed verified system** 또는 **밸런스 완료**로 판정할 수는 없다.

---

## 22. 구현 체크포인트: 수술 부품 설치 pending disposition outbox

상태: **도메인 수직 슬라이스 완료 / 전체 terminal outbox 이관 진행 중**.

- 수술 부품 설치는 `orderId + partInstanceId`로 결정론적이고 재사용되지 않는 operation ID를 만든다.
- 물리 부품 lot은 설치 전에 pending `Transfer` receipt로 commit하며, 부품 aggregate는 operation ID, physical commit ID, source stack ID, order ID, subject ID를 current-format Surgery V9에 저장한다.
- domain terminalization이 먼저 durable해지고 동일 physical commit만 acknowledge한다. acknowledge 전 중단은 installed aggregate+pending receipt 상태로 남아 같은 helper가 재실행되며, 재소비 없이 exact acknowledge만 완료한다.
- restore publication은 physical pending authority가 게시된 뒤 Surgery candidate를 공개하기 전에 exact receipt를 join·검증·terminalize·acknowledge한다.
- commit/source/order/subject가 하나라도 다르면 domain mutation과 physical receipt 소실 없이 fail-loud한다.
- installed terminal replay는 idempotent하고, 다른 order 또는 subject의 재사용은 거절한다.
- 과거 save migration은 없으며 V9 필수 provenance 누락은 typed current-format restore failure다.
- focused `surgical_part_installation_pending_outbox`, 전체 Surgery Editor 회귀, Dungeon save-section 회귀와 fresh Surgery PlayMode가 PASS했다. fresh report는 `Artifacts/QA/surgery-playmode-report.txt`, UTC `2026-08-20T20:09:15.1331318Z`, `RESULT=PASS; failures=0`, captured Warning/Error `0/0`이다.
- 이 체크포인트는 설치 부품 수량·BOM·WU·수술 시간·위험·회복·kg·EWU·가격을 변경하지 않는다.
- 남은 single-stack/terminal domain은 각자 genuinely unique action/epoch ID와 current-format aggregate provenance를 확보한 뒤 같은 계약으로 개별 이관한다. 이 행만으로 global outbox, full SaveLoad, EWU·가격 또는 물리 중량 밸런스 완료를 선언하지 않는다.

---

## 23. 구현 체크포인트: 포로 노동 도구 배정 pending disposition outbox

상태: **도메인 수직 슬라이스 완료 / 전체 terminal outbox 이관 진행 중**.

- 포로 노동 도구는 Sink가 아니라 포로 aggregate로 custody가 이동하는 durable `Transfer`다. operation ID는 `captiveId + itemInstanceId`로 결정론적이고 해당 배정 구간에서 재사용되지 않는다.
- exact physical lot은 배정 전에 pending batch receipt로 commit하며, captive state는 operation ID, physical commit ID, source stack ID, assigned instance ID와 assignment-completed 상태를 current-format Captivity V3에 저장한다.
- domain assignment publication이 먼저 durable해지고 동일 physical commit만 acknowledge한다. acknowledge 전 중단은 completed assignment+pending receipt 상태로 남아 재실행 시 추가 소비 없이 cleanup과 exact acknowledgement만 수행한다.
- restore stage는 physical pending authority를 조회할 수 있는 시점에 candidate-owned `ReconcileCaptives` 경계를 사용해 receipt kind, operation, reason, commit, source stack, quantity와 assigned instance를 exact join한 뒤 captive candidate를 공개한다.
- malformed commit syntax, valid-looking mismatched commit, wrong source/operation/item, 누락된 pending labor permission 또는 destination은 captive mutation과 receipt 소실 없이 fail-loud한다.
- labor cancel, captive release/death, tool return과 breakage는 unresolved receipt를 먼저 finalize하거나 custody를 유지하며, 성공한 종료만 assignment provenance를 전부 지운다. pending receipt가 남은 상태에서 destination을 먼저 release하지 않는다.
- 과거 save migration은 없으며 V3 필수 provenance 누락이나 completed 상태와 pending permission의 모순은 typed current-format restore failure다.
- focused `captive_labor_tool_assignment_pending_outbox`, Physical Stock V18, Captivity Circus, Dungeon save-section, runtime composition 회귀가 PASS했다. fresh `Artifacts/QA/captivity-ai-playmode.txt` UTC `2026-08-20T20:29:20.4909781Z`는 `RESULT=PASS; failures=0`, final Console Warning/Error `0/0`이다.
- 이 체크포인트는 도구 수량·내구 소모율·포로 노동 효율·시설·WU·kg·EWU·가격을 변경하지 않는다.
- broad Captivity PlayMode는 capture/warden/escape/recapture 회귀 증거이고, 노동 도구 pending outbox의 직접 증거는 focused Editor row다. 이 행만으로 남은 terminal domain, global outbox manifest, full mid-action SaveLoad 또는 물리 중량 밸런스 완료를 선언하지 않는다.

---

## 24. 구현 체크포인트: raw removal zero·current-format full restore·입고 operation history

상태: **정적 우회 차단 및 통합 복원 증거 완료 / 전체 terminal outbox 이관 진행 중**.

- production non-Editor C#의 `.TryConsumeStackQuantity(` 호출은 typed Items 경계인 `ItemTransferService`, `PhysicalItemDisposition`, `WorldItemModels`, `WorldItemStackRuntime` 네 파일만 허용한다. 다른 production caller가 하나라도 생기면 `V27_PRODUCTION_RAW_CONSUME_CALLS_ZERO`가 fail-loud한다.
- warehouse gram admission의 terminal token은 exact-once tombstone이며 같은 owner operation ID를 다른 lot 또는 source revision으로 재사용하지 않는다.
- physical current-format candidate나 rollback이 haul sequence를 과거 값으로 되돌린 뒤 admission terminal history가 더 앞서 있는 상태에서도 planning은 admission operation history와 충돌하는 haul ID를 건너뛴다. fingerprint/source validation을 완화하거나 tombstone을 삭제하지 않는다.
- admission history 조회는 collision avoidance용 read boundary다. terminal receipt의 payload, warehouse-local revision, exact lot fingerprint와 source 검증은 기존과 동일하며 count/category fallback은 없다.
- restore participant reverse rollback에서 wildlife candidate가 이미 replacement grid에 exact 등록된 상태라면 두 번째 facility-grid rebind는 idempotent no-op이다. 다른 grid, 중복 occupant 또는 registration 불일치는 계속 fail-loud한다.
- focused Physical Stock V18은 terminal release/commit 뒤 operation history 보존과 production raw-consume caller zero를 검사한다. Wildlife focused row는 wildlife rollback 뒤 facility rollback의 두 번째 grid rebind가 identity와 occupant를 보존함을 검사한다.
- fresh full `Artifacts/QA/physical-item-logistics-playmode-report.txt` UTC `2026-08-20T20:54:48.1823414Z`는 첫 loose ration gram warehouse haul, FacilityBuffer, construction, craft/equipment, repair, expedition, over-capacity evacuation을 포함해 `RESULT=PASS; failures=0`, captured Warning/Error `0/0`이다.
- fresh `Artifacts/QA/ai-mid-action-save-load-playmode.txt` UTC `2026-08-20T20:55:07.2614796Z`는 다섯 destination tamper의 atomic rollback, AI wake 전 bound intent, 두 번의 정상 restore, exact-once AIHaul과 conservation을 포함해 `result=PASS`, `failures=0`, no unexpected Error/Exception/Assert다.
- 이 체크포인트는 item kg, warehouse 25,000g authored capacity, carry limit, BOM, WU, EWU, 가격 또는 생산량을 변경하지 않는다.
- 남은 작업은 genuinely unique action/epoch ID를 확보한 terminal domain별 outbox 이관, packaged lot·나머지 ingress, 전수 kg After 적용, EWU·가격 재생성, 6인 생존망과 최종 3-seed다. 따라서 이 증거만으로 물리 중량 또는 밸런스 완료를 선언하지 않는다.

---

## 25. 구현 체크포인트: 세력 배상 pending disposition outbox

상태: **도메인 수직 슬라이스 완료 / 전체 terminal outbox 이관 진행 중**.

- 세력 배상은 `factionId + betrayalScar`로 결정론적인 재사용 불가 operation ID를 만든다. 같은 세력의 다음 배신은 scar를 증가시켜 이전 배상 receipt와 충돌하지 않는다.
- exact physical goods는 배상 aggregate 갱신 전에 pending `Transfer` receipt로 commit한다. Faction V2는 operation ID, physical commit ID, canonical source stack IDs, exact quantity·grams·physical value, campaign grievance의 절대 target과 completion 상태를 저장한다.
- 최종화 순서는 exact pending receipt 검증 → faction `restitutionPaid` terminal publication → campaign grievance absolute target 적용 → 동일 physical commit acknowledge다. crash/retry는 `-30`을 다시 적용하지 않고 같은 target으로 수렴한다.
- completed provenance는 이후 합법적인 campaign grievance 변화를 과거 target으로 되감지 않는다. terminal replay는 남아 있는 exact pending receipt의 acknowledgement만 정리한다.
- current-format restore는 Faction section이 이미 의존하는 PhysicalItems와 OffenseAggregate candidate를 사용해 incomplete provenance만 reconcile한 뒤 faction candidate를 공개한다. commit/source/quantity/grams/value/operation이 하나라도 다르면 faction·campaign mutation과 receipt loss 없이 fail-loud한다.
- recurring goodwill transfer는 의도적으로 이 operation identity를 재사용하지 않는다. 현재 `faction + day` 의미 ID는 합법적인 반복 지급과 충돌하므로 genuinely unique payment epoch를 aggregate가 소유하기 전까지 synchronous 경계를 유지한다.
- focused `FactionRestitutionOutboxDebugScenarios.RunAll()`과 `SpeciesFactionDefenseExpansionDebugScenarios.ValidateOnly()`가 PASS하며, 마지막 source refresh 뒤 Unity compilation success와 Console Error 0을 재확인했다.
- 이 체크포인트는 배상 요구 수량·관계 수치·campaign grievance 감소량·kg·BOM·WU·EWU·가격을 변경하지 않는다.
- 남은 terminal domain은 unique epoch와 current-format aggregate ownership을 먼저 확보해야 한다. 시설 진화는 다중 요구량·건물 교체까지 하나의 durable transaction이 필요하고, 의복 수리는 restore-owned aggregate가 아니므로 단순 receipt 추가로 완료 처리하지 않는다.

---

## 26. 구현 체크포인트: 시설 진화 다중 재료 pending batch

상태: **부분 완료 / restore 자동 reconciliation 미완료**.

- 한 진화 레시피의 모든 material requirement를 category별로 먼저 합산하고 exact physical stack vector 하나로 준비한다. 요구량 N번째 실패 전에 앞선 요구량만 소비하는 반복 `ConsumeMaterial` 경로는 production engine에서 제거했다.
- operation ID는 `facility persistent ID + next evolution-history sequence`다. recipe ID는 reason fingerprint에 포함하므로 같은 진화 slot에서 다른 recipe를 재사용하면 conflict로 fail-loud한다.
- 물리 재료는 `TryCommitPending`으로 한 번만 debit된다. 건물 교체가 실패하면 pending receipt를 유지하고, 같은 recipe retry는 현재 stock이 이미 없어도 exact pending receipt를 재생해 두 번째 debit 없이 계속한다.
- `GridFacilityEvolutionBuildingReplacer`는 원본 점유와 visual을 성공 전까지 보존한다. 결과 생성 또는 grid 등록 실패 시 결과 candidate를 정리하고 원본 occupant를 exact 재등록하며, 재등록 자체가 실패하면 즉시 예외로 중단한다.
- 성공 순서는 pending material commit → result facility replacement → state/record/evolution publication → exact material acknowledgement다. record-token clone은 성공 전 live source record를 변경하지 않는다.
- focused 회귀는 첫 replacement failure 뒤 원본 시설이 살아 있고 재료가 이미 debit된 상태, 두 번째 retry 성공, `TryReplaceCalls=2`를 검사한다. 전체 `FacilityEvolutionDebugScenarios.RunAll()`과 `OffenseStrategicDebugScenarios.RunAll()` 11행이 PASS하고 최종 Unity Console Warning/Error는 `0/0`이다.
- 아직 pending recipe ID, commit ID, resolved mutation input/record snapshot을 Facility state V4에 저장하지 않는다. 따라서 restore 후 자동 재개/acknowledge를 증명하지 않았고 이 terminal domain을 완료 처리하지 않는다.
- 다음 하위 단계는 source/result 어느 쪽을 저장해도 같은 phase를 판별하는 pending evolution DTO, physical receipt exact join, applied/unapplied phase별 restore reconciliation, tamper rollback이다.
- 이 체크포인트는 시설 진화 BOM·별 등급·요구 token·WU·kg·EWU·가격을 변경하지 않는다.

---

## 27. 구현 체크포인트: Facility V4 진화 material outbox·restore reconciliation

상태: **시설 진화 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- `FacilityEvolutionStateComponent`의 current-format module version을 V4로 올렸다. 과거 V3 세이브 마이그레이션은 만들지 않으며 V4 필수 provenance가 없으면 typed restore failure다.
- source facility는 physical debit 직후 다음 권위를 한 묶음으로 저장한다.
  - canonical material operation ID와 reason code
  - Physical pending commit ID
  - ordinal exact source stack ID vector
  - exact quantity와 input grams
  - recipe ID, source persistent/definition ID, result definition ID
  - next evolution-history sequence
  - `MaterialCommitted` 또는 `DomainApplied` phase
  - canonical resolved mutation tag vector
  - token consumption, identity pressure, lineage, mutation, history와 record aggregate가 모두 확정된 resolved result payload
- resolved result는 재귀적인 snapshot 객체로 중첩하지 않고 V4 pending DTO 안의 단일 canonical JSON payload로 저장한다. 이 구조는 Unity serializer의 depth cycle을 피하면서, 복원 때 proposal·mutation resolver·record token·확률·material selection을 다시 실행하지 않게 한다.
- 신규 진화는 proposal·mutation·record token 결과를 먼저 detached snapshot에서 확정하고 physical batch를 commit한다. commit 성공 직후 source state에 pending provenance를 publish한 뒤에만 건물을 교체한다.
- `MaterialCommitted` resume은 authored recipe/source/result와 Physical receipt를 exact 대조하고 저장된 result snapshot으로 한 번만 교체한다. 결과 state에 같은 commit을 `DomainApplied`로 기록한 뒤 physical acknowledgement를 시도한다.
- `DomainApplied` resume은 결과 건물과 resolved snapshot이 byte-equivalent인지 검증하고 건물을 다시 교체하지 않는다. 동일 physical commit acknowledgement와 pending marker 제거만 수행한다.
- `224.world.facility-evolution-materials` restore participant는 시설 candidate와 Physical Items가 publish된 뒤, haul-intent participant 225보다 먼저 다음 join을 read-only 검증한다.
  - operation/reason/commit/source vector/quantity/grams exact
  - recipe stable ID 유일성
  - authored source/result definition과 result star grade exact
  - resolved mutation tag가 authored allowed set 안에 있음
  - duplicate facility operation 0
- participant 224는 검증 중 gameplay mutation을 하지 않는다. 따라서 이후 participant 실패 시 전체 restore rollback을 방해하지 않으며, 불일치가 있으면 post-restore fallback으로 진행하지 않고 restore 자체를 fail-loud한다.
- restore가 성공한 뒤 `FacilityEvolutionPendingMaterialProjection`이 pending building을 스캔해 exact stored phase를 재개한다. 정상 world에서 실패하면 operation ID와 원인을 Console Error로 한 번 노출하며 다른 recipe·building·stack을 대체 선택하지 않는다.
- focused 회귀는 다음을 고정한다.
  - replacement first-failure 뒤 source occupant 생존·재료 exact 1회 debit·V4 JSON round trip
  - commit/quantity/grams cross-authority tamper의 restore guard rejection과 Physical receipt 보존
  - recipe/source/result payload structural tamper의 component restore 원자 거절
  - acknowledgement first-failure 뒤 `DomainApplied` 상태에서 building replacement 호출 수 1 유지
  - 자동 projection이 exact stored result를 재개하고 pending/receipt를 정리
  - Dungeon save-section participant 및 composition 등록 통과
- 최종 증거는 clean Unity compile, `FacilityEvolutionDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)`, `OffenseStrategicDebugScenarios.RunAll()` 11행 PASS, Console Warning/Error `0/0`이다.
- 이 체크포인트는 authored BOM·별 등급·required token·WU·kg·EWU·가격·후보 확률을 변경하지 않는다. 구조적 손실·중복·재굴림 경계만 닫았으므로 상태는 `밸런스 영향 없음`이다.
- 다음 terminal 하위 단계는 restore-owned aggregate가 아직 없는 의복 수리다. package tare, 남은 item semantics, 전수 kg After, EWU·가격, 6인 생존망과 3-seed 실측은 계속 미완료다.

---

## 28. 구현 체크포인트: 의복 수선 material outbox·Character Environment V6 복원 재개

상태: **의복 수선 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- `DungeonCharacterEnvironmentSaveData` current-format version을 V6로 올렸다. 과거 세이브 마이그레이션은 만들지 않으며 V6 repair provenance 누락·부분 기록·구조 불일치는 restore failure다.
- 내구도 60 미만 수선은 작업 중에는 기존 quantity lease가 대상 의복과 thread/scrap 선택을 보호한다. 모든 작업·시설·재료 preflight가 끝난 terminal 인계에서 해당 주문의 lease를 먼저 해제하고, 이후 Physical pending receipt가 유일한 exact custody authority가 된다.
- 수선 주문은 다음 current-format 권위를 한 묶음으로 저장한다.
  - `apparel-repair:{orderId}` operation ID와 고정 reason code
  - Physical pending commit ID와 ordinal exact source stack ID vector
  - exact input quantity와 grams
  - 대상 physical stack ID
  - canonical original `ApparelInstanceState` payload
  - canonical resolved `ApparelInstanceState` payload
  - `MaterialCommitted` 또는 `RepairApplied` phase
- 신규 수선은 thread/scrap을 pending `Transfer`로 exact 1회 debit한다. `MaterialCommitted`에서는 저장된 resolved apparel payload를 대상 component에 한 번 적용하고 `RepairApplied`로 전이한다. `RepairApplied`에서는 현재 component가 저장 payload와 byte-equivalent인지 확인한 뒤 같은 commit만 acknowledge한다.
- acknowledge 실패나 실행 중단은 주문을 `WaitingForDispositionFinalization`에 유지한다. 재시도는 시설·재료를 다시 선택하거나 lease를 재취득하지 않고 stored phase만 재개하므로 두 번째 재료 debit 또는 durability 적용이 없다.
- 수선 재료가 필요 없는 내구도 60 이상 경로는 기존 단일 component 갱신을 유지한다. 재료 outbox가 없는 주문에 빈 provenance를 합성하지 않는다.
- `ApparelWorkOrderRuntime`은 `226.world.apparel-work-orders` restore participant다. Character Environment section commit은 준비된 order candidate를 stage하고, participant publication은 Physical pending receipt와 다음 항목을 exact join한다.
  - repair kind·phase·state 조합
  - canonical operation/reason/commit
  - source stack vector·quantity·grams
  - target stack과 item instance identity
  - original 또는 resolved component payload와 현재 physical component
- malformed 또는 valid-looking mismatched commit, operation, source, mass, target, payload는 live order·target component·Physical pending receipt를 바꾸기 전에 fail-loud한다. post-restore material reselection이나 durability fallback은 없다.
- participant publication은 reconcile된 주문만 completed로 바꾸고 exact completed lease만 정리한다. 전체 주문 목록을 교체하면서 unrelated current-format reservation을 일괄 해제하지 않는다. 이후 restore participant가 실패하면 aggregate-root/Physical candidate rollback과 함께 이전 live order snapshot을 복구한다.
- focused `ApparelRepairOutboxDebugScenarios`는 실제 수선 시설 에셋, 실제 world item repository, quantity reservation service, leased selection, Physical batch disposition을 사용해 다음을 고정한다.
  - thread 1 + mending scrap 1 exact debit
  - 내구도 `40→70` exact 1회 적용
  - 첫 acknowledgement 실패 후 pending receipt·`RepairApplied` phase 보존
  - retry 완료 시 두 번째 재료 debit·durability 적용 0
  - valid-looking commit tamper restore rejection 후 live order·item·receipt 무변경
  - 정상 candidate publication에서 acknowledgement-only completion과 participant ID exact
- Unity clean compile, focused apparel repair outbox, `DungeonSaveSectionDebugScenarios.RunAll(false)`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)`가 PASS했고 focused/조립 검증 직후 Console Warning/Error는 `0/0`이다.
- broader `V22ApparelDebugScenarios`는 현재 별도 중량 재산정 뒤에도 `material:dreamweave MaxStack=100`을 고정한 기존 fixture mismatch로 중단된다. 전체 `PhysicalItemDebugScenarios`도 `warehouse_stored_stack_consumption`의 기존 water mirror fixture mismatch 한 행이 남는다. 두 실패는 이번 수선 receipt/phase 경계에서 발생하지 않았고, 이번 terminal slice 성공으로 상쇄하거나 통합 green으로 보고하지 않는다.
- 이 체크포인트는 의복 BOM·thread/scrap 수량·수선 WU·내구도 증가량·kg·EWU·가격을 변경하지 않는다. package tare, 남은 terminal domains, packaged lot·ingress, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-seed는 계속 미완료다.

---

## 29. 구현 체크포인트: 야생동물 식량 약탈 pending Sink outbox·Wildlife V5

상태: **야생동물 식량 약탈 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- 기존 식량 약탈은 `TryCommitPhysicalDisposition`으로 식량을 즉시 Sink한 뒤 order를 `Stolen`으로 바꾸었다. 물리 제거와 domain publication 사이에 durable phase가 없었고, 같은 raid의 여러 늑대가 `wildlife-food-raid:{raidId}`를 공유했다.
- operation ID를 `wildlife-food-raid:{raidId}:{wildlifeId}`로 고정했다. 같은 습격의 각 늑대가 독립된 exact-once slot을 소유하며 다른 늑대의 receipt를 재생할 수 없다.
- `DungeonWildlifeSaveData` current-format version을 V5로 올렸다. 과거 save migration은 만들지 않으며 V5 raid provenance 누락·부분 기록·비정규 operation은 restore failure다.
- raid order는 다음 권위를 저장한다.
  - `ItemCommitted` 또는 `RaidPublished` phase
  - canonical operation/reason/Physical commit ID
  - exact source stack ID vector
  - exact quantity와 input grams
  - consumed physical item definition ID
- `ItemCommitted`에서는 저장 quantity를 `stolenQuantity`와 outcome에 한 번 publish하고 `RaidPublished`로 전이한다. `RaidPublished`에서는 같은 Physical commit만 acknowledge한 뒤 pending provenance를 지우고 `Leaving`으로 전이한다.
- acknowledgement 실패 중에는 `WaitingForDispositionFinalization` 상태를 유지한다. actor는 receipt가 정리되기 전에 출발하지 않으며 retry는 target 검색이나 두 번째 Sink를 수행하지 않는다.
- actor가 commit 이후 죽거나 제거되는 경우 단순 `Cancelled`로 덮어쓰지 않는다. pending receipt를 먼저 exact reconcile하고 성공한 도난을 `Stolen` terminal로 보존한다. reconcile failure는 receipt를 버리거나 대체 음식으로 진행하지 않고 fail-loud한다.
- Wildlife restore participant `250.world.wildlife`는 candidate population을 publish하고 behavior runtime을 재구성한 직후, detached actor publication 전에 모든 pending raid receipt를 exact join·reconcile한다. operation/reason/commit/source/quantity/grams mismatch는 whole restore rollback 대상으로 처리된다.
- 구조 validator는 phase/state, unique wildlife order, source vector, quantity 1, positive grams, per-actor operation ID, item ID와 phase별 stolen quantity를 current-format 필수 계약으로 검사한다.
- focused real-physical fixture는 다음을 고정한다.
  - 같은 raid의 두 wildlife ID가 서로 다른 operation ID를 생성
  - ration 한 개 Sink 뒤 첫 acknowledgement 실패 시 `RaidPublished`+pending receipt 보존
  - retry에서 ration second Sink 0, receipt exact cleanup, stolen quantity 1
  - valid-looking commit tamper가 receipt를 잃지 않고 mismatch로 거절
  - 두 번째 wildlife도 독립적으로 exact 1회 완료
- clean Unity compile, `WildlifeFoodRaidOutboxDebugScenarios.RunFocused()`, `WildlifeDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)`가 PASS했고 Console Warning/Error는 `0/0`이다.
- 이 체크포인트는 약탈 수량 1, 야생동물 행동 빈도·경로·확률, 음식 kg·영양·가격, 시설·BOM·WU·EWU를 변경하지 않는다. 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, 6인 생존망과 최종 3-seed는 계속 미완료다.

---

## 30. 구현 체크포인트: 반복 팩션 호의 물자 pending Transfer outbox·Faction V3

상태: **반복 호의 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- 기존 호의 operation ID `faction-goodwill:{factionId}:{currentDay}`는 같은 날 같은 세력에 두 번 호의를 보내는 합법적인 실행을 하나로 충돌시켰다. Faction aggregate가 저장하는 monotonic `goodwillOperationSequence`를 추가하고 operation ID를 `faction-goodwill:{factionId}:{sequence:D8}`로 고정했다.
- `DungeonFactionSaveData` current-format version을 V3로 올렸다. 과거 save migration은 만들지 않으며 V3는 global sequence와 pending faction provenance의 sequence 범위를 함께 검증한다.
- pending 호의 상태는 sequence, operation/reason/Physical commit ID, ordinal exact source stack ID vector, exact quantity·input grams·physical value, campaign rapport absolute target, terminal phase를 저장한다.
- 신규 호의는 exact physical stack vector를 pending `Transfer`로 한 번 debit한 뒤에만 discovered 상태와 campaign rapport target을 적용한다. rapport 증가는 authored `min(10, physicalValue/10)`과 기존 betrayal scar `0.85^scar` 감쇠를 그대로 사용한다.
- crash/retry 또는 restore에서 campaign rapport가 이미 target 이상이면 relative delta를 재적용하지 않는다. pending receipt만 exact acknowledge하고 provenance를 지운다. target 미달일 때만 현재 rapport와 target의 차이를 한 번 적용한다.
- same-day 두 번째 호의는 다음 sequence를 사용하므로 첫 terminal tombstone과 충돌하지 않는다. 다른 source lot, commit, quantity, grams, reason, sequence를 기존 operation으로 재사용하면 fail-loud한다.
- V3 구조 validator는 빈 provenance의 partial field, global sequence보다 큰 local sequence, 비정규 operation/commit, 비정렬·중복 source IDs, 50 미만 physical value, 범위 밖 rapport target, discovered 없는 completed phase를 거절한다.
- Faction restore publication은 Physical/Offense candidate가 query 가능한 기존 dependency 순서에서 pending goodwill을 exact reconcile한다. receipt mismatch나 missing receipt는 다른 물자를 선택하거나 rapport를 추정하지 않고 whole restore failure로 전파한다.
- focused `FactionRestitutionOutboxDebugScenarios.RunAll()`은 두 same-day operation의 ID 분리, 첫 transfer exact debit, V3 JSON provenance, valid-looking commit tamper no-mutation/no-loss, campaign-already-applied recovery, second debit 0을 검사한다.
- `SpeciesFactionDefenseExpansionDebugScenarios.ValidateOnly()`와 `DungeonSaveSectionDebugScenarios.RunAll(false)`가 함께 PASS했고 clean Unity compile 및 Console Warning/Error `0/0`을 확인했다.
- 이 작업과 함께 기존 fixture drift 두 건을 별도로 교정했다. V22 woven stack 검증은 현재 authored `cloth=75`, `dreamweave=40`, `기타 woven=100`을 읽는 계약으로 맞췄고, warehouse stored-consumption row는 category spawn이 선택한 exact Water lot을 소비한다. V22 전체, stored-water focused, PhysicalItem 전체가 PASS했다.
- 이 체크포인트는 호의 최소 물자가치 50, trust/rapport 증가량, betrayal scar 감쇠, kg, BOM, WU, EWU, 가격, item MaxStack authored 값 자체를 변경하지 않는다. 나머지 terminal domain, packaged lot·나머지 ingress, package tare, 전수 kg After, 6인 생존망과 최종 3-seed는 계속 미완료다.

---

## 31. 구현 체크포인트: 포획 야생동물 급식 pending Sink outbox·Circus V3·Wildlife V6

상태: **포획 야생동물 일반/폐기물 급식 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- 기존 포획 동물 급식은 시설 버퍼의 category/item 수량을 동기 소비한 뒤 허기·질병 상태를 별도로 갱신했다. 물리 Sink와 동물 상태 publication 사이에 durable phase가 없었고, 재시도·저장 복원에서 같은 먹이를 다시 소비했는지 판별할 exact action identity가 없었다.
- `CapturedWildlifeState.nextFeedOperationSequence`를 per-animal monotonic authority로 두고 operation ID를 `captivity-wildlife-feed:{wildlifeId}:{sequence:D8}`로 고정했다. 정상 사료와 폐기물 직접 급식은 같은 ID·receipt·phase 계약을 사용한다.
- `CircusSaveData` current-format version을 V3, `DungeonWildlifeSaveData`를 V6로 올렸다. 과거 save migration은 만들지 않으며 sequence/phase/operation/reason/commit/source/quantity/grams/item/outcome의 부분 누락·비정규 값은 typed restore failure다.
- pending feed는 다음 권위를 저장한다.
  - `ItemCommitted` 또는 `CarePublished` phase
  - exact Physical Sink operation/reason/commit ID
  - ordinal 정렬된 exact source stack ID vector, quantity `1`, positive input grams
  - feed item ID, nutrition, 한 번만 결정한 disease chance/result
  - actor의 absolute hunger·health target과 captured-state sickness target
- 물리 receipt가 먼저 commit된 뒤 actor/captured-state의 absolute target을 한 번 publish한다. actor는 마지막 적용 commit ID를 Wildlife V6에 저장하여 replay를 idempotent하게 만들고, domain publication이 확인된 뒤에만 Physical receipt를 acknowledge하고 pending provenance를 지운다.
- acknowledgement 실패 시 `CarePublished`와 exact receipt가 남는다. retry·restore는 음식 후보나 질병 RNG를 다시 선택하지 않고 acknowledgement만 재시도한다. 이미 actor에 적용된 commit은 허기·피해를 두 번째 적용하지 않는다.
- 정상 사료는 해당 pen destination의 unreserved·unforbidden `FacilityBuffer` exact stack을 ordinal stack ID 순으로 선택한다. 폐기물 급식도 read-only candidate query로 exact source를 고른 뒤 같은 Sink path를 사용한다. category-only 동기 소비는 이 food 경로에서 제거했다.
- Circus participant `500.world.circus`의 reversible captured projection은 pending feed를 actor publication 전에 exact receipt와 join·reconcile한다. receipt mismatch, missing actor, operation/reason/commit/source/quantity/grams drift는 대체 먹이나 추정값 없이 restore를 실패시키며 새 projection을 즉시 rollback한다.
- `CapturedWildlifeState.Clone()`은 pending source list를 deep-copy한다. candidate와 prior-live state가 같은 mutable list를 공유해 tamper나 rollback 결과가 오염되는 경로를 금지한다.
- focused real-repository fixture는 다음을 고정한다.
  - 두 feed epoch의 sequence·operation ID uniqueness
  - quantity `1`과 exact input grams Sink
  - 첫 acknowledgement 실패 뒤 pending receipt와 JSON provenance 보존
  - clone source-list isolation과 valid-looking commit tamper no-mutation/no-loss
  - actor-already-applied crash recovery에서 두 번째 허기·피해 적용 0
  - 폐기물 disease outcome을 한 번 결정하고 health/sickness target exact 1회 적용
- production-live `CaptivityWildlifeLifecyclePlayModeVerifier`는 authored herbivore와 `feed:hay`를 실제 pen buffer에 연결해 다음 marker를 고정한다.
  - `ANIMAL_CARE_FEED_SOURCE_PHYSICAL`: `196g`, quantity `1`, exact pen destination
  - `ANIMAL_CARE_FEED_SINK_EXACT`: quantity `1→0`, hunger `0.9→0.1889`, exact Sink commit, pending receipt 0
  - `ANIMAL_CARE_FEED_OUTBOX_CLEAN`: sequence `1`, empty pending provenance, disease chance `0`
  - `ANIMAL_CARE_FEED_SAVE_EXACT`: Circus sequence/phase와 Wildlife actor commit current-format 저장 일치
- clean Unity compile, focused outbox, `CaptivityCircusDebugScenarios.RunAll(true)`, fresh `CaptivityWildlifeLifecyclePlayModeVerifier.RequestRun()`가 PASS했다. artifact UTC `2026-08-20T23:46:08.3171612Z`, `RESULT=PASS; failures=0`, Console Warning/Error `0/0`이다. coverage manifest의 `wildlife:animal-care`는 위 네 marker를 포함한 fresh `LiveExecuted`다.
- 이 체크포인트는 하루 먹이량, nutrition `0.72`, 폐기물 nutrition 보정·질병 확률, 동물 허기 진행률, 먹이 kg·가격·BOM·WU·EWU를 변경하지 않는다. 나머지 terminal domain, packaged lot·나머지 ingress, package tare, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-seed는 계속 미완료다.

---

## 32. 구현 체크포인트: 길잡이 부적 정보 해금 pending Sink outbox·External Influence V4

상태: **길잡이 부적 정보 해금 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- 기존 `TryUnlockIntel(..., TrailCharm)`은 exact 길잡이 부적 한 개를 동기 `Sink`한 뒤 별도 명령으로 사이트 정보를 해금했다. 두 권위 사이에 저장 가능한 phase가 없어 acknowledgement 또는 저장 경계에서 이미 사라진 부적을 다시 찾거나, 부적만 사라지고 정보가 남지 않는 상태를 판별할 수 없었다.
- 정보 사이트 ID는 한 번만 해금되는 canonical terminal identity다. operation ID는 `external-influence-trail-charm:{siteId}`로 고정하고, 같은 사이트 재시도는 동일 receipt만 정리하며 이미 해금된 사이트는 새 부적을 소비하지 않는다.
- `DungeonExternalInfluenceSaveData` current-format version을 V4로 올렸다. 과거 세이브 마이그레이션은 만들지 않으며 `None|ItemCommitted|IntelPublished` phase와 operation/reason/commit/source/quantity/grams/item provenance의 부분 누락·비정규 값은 restore 전에 실패한다.
- 저장 형식의 순수 계약은 `DungeonStory.CoreSession`의 `ExternalInfluenceTrailCharmSaveContract`가 소유한다. Infrastructure save validator가 Assembly-CSharp runtime helper를 역참조하지 않으며, runtime outbox가 core contract를 재사용하는 단방향 의존을 유지한다.
- pending provenance는 canonical site ID, exact Sink operation/reason/commit ID, ordinal exact source stack ID vector, quantity `1`, positive input grams, exact item ID `resource:trail-charm`을 저장한다. commit syntax도 `physical-batch-disposition:3:{operation}:1:{grams}`와 정확히 일치해야 한다.
- 실행 순서는 exact trail-charm stack의 deterministic ordinal 선택 → Physical pending Sink commit → V4 `ItemCommitted` 기록 → 사이트 정보 absolute membership publication → `IntelPublished` 기록 → exact receipt acknowledgement → provenance clear다.
- acknowledgement 실패 시 사이트 정보와 `IntelPublished` envelope를 보존한다. 다음 명령은 다른 결제나 다른 사이트를 처리하기 전에 이 receipt를 먼저 reconcile하며, 두 번째 Sink나 relative 정보 효과를 실행하지 않는다.
- current-format restore는 External Influence section이 이미 의존하는 Physical Items candidate를 사용한다. `ItemCommitted`는 exact pending receipt가 반드시 존재해야 하며 정보 membership을 한 번 publish한 뒤 acknowledge한다. `IntelPublished`에서 receipt가 이미 사라진 경우는 crash-after-ack-before-clear 경계로 허용하되 saved unlocked membership이 exact site를 포함해야만 provenance를 지운다.
- receipt가 존재하면 kind/operation/reason/commit/source/quantity/grams를 모두 exact join한다. source나 commit을 대체하거나 현재 inventory에서 다른 부적을 다시 고르지 않는다. malformed envelope는 candidate build 전에 live aggregate를 변경하지 않고 실패한다.
- focused real-repository fixture는 acknowledgement first-failure 후 quantity `1` exact debit과 `IntelPublished` 보존, retry second Sink `0`, same-site repeat debit `0`, `ItemCommitted` save/restore 자동 finalization, crash-after-ack restore idempotence, commit tamper no-mutation을 검사한다.
- clean Unity compile, `ExternalInfluenceTrailCharmOutboxDebugScenarios.RunAll()`, `BatchACoreSessionSaveDebugScenarios.RunAll(false)`가 PASS했고 최종 Console Warning/Error는 `0/0`이다.
- 이 체크포인트는 길잡이 부적 recipe(`resource:rune-dust` 1 + `resource:fang` 1), Direct WU `16`, authored kg, 정보 해금 비용, EWU, 가격, 원정 보상 또는 확률을 변경하지 않는다. 나머지 terminal domain, packaged lot·나머지 ingress, package tare, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-se드는 계속 미완료다.

---

## 33. 구현 체크포인트: 예약 수량 pending Sink 원자 경계

상태: **Items 선행 기반 완료 / 캐릭터 식사 outbox 연결은 다음 수직 슬라이스**.

- 시설 식사처럼 quantity lease가 먼저 잡힌 소비는 기존 generic pending batch에 바로 넣을 수 없었다. generic batch는 다른 소유자의 예약을 훔치지 않기 위해 reserved source를 의도적으로 거절하므로, lease를 먼저 풀고 pending Sink를 실행하면 그 사이 다른 actor가 같은 stack을 가져가는 새 저장·경합 경계가 생긴다.
- `IReservedPhysicalItemBatchDispositionService.TryCommitReservedSinkPending`을 별도 Items capability로 추가했다. 일반 unreserved batch API의 안전 규칙은 완화하지 않으며, 이 API는 exact lease ID·quantity·lease owner와 같은 canonical operation ID·reason을 요구한다.
- 최초 실행은 lease revalidate와 exact source slice·signature·mass를 모두 확인하고 pending Physical Sink receipt를 먼저 등록한 뒤, 동일 transaction 안에서 lease slice와 world stack quantity를 debit한다. 성공 후 lease는 소진되지만 pending receipt는 domain acknowledgement까지 Physical current-format save authority에 남는다.
- 같은 operation 재시도는 이미 소진된 lease를 다시 요구하지 않는다. 저장된 pending receipt의 kind·operation·reason·quantity가 일치할 때 동일 commit을 반환하며 두 번째 physical debit은 0이다. 다른 reason·quantity로 operation을 재사용하면 conflict로 실패한다.
- item marker publication 같은 후행 예외가 발생하면 원래 source identity·quantity를 복원하고, transaction 직전 `ItemQuantityLease` snapshot을 같은 lease/owner/purpose/cohort/slice/signature로 재등록한 뒤 pending receipt를 제거한다. 실패를 반환하면서 cargo만 복원하고 lease를 잃는 반쪽 rollback을 금지한다.
- DI는 `ItemQuantityReservationService`의 동일 singleton을 batch 경계에 주입한다. 3-인자 생성자는 예약 기능이 필요 없는 isolated fixture 호환용으로 남기고, production VContainer 생성자는 명시적 `[Inject]` 4-인자 경로를 사용한다.
- focused real-repository 회귀는 quantity 2 stack에서 quantity 1 meal lease를 예약해 exact Sink, source `2→1`, lease 소진, pending 1을 확인한다. 동일 operation replay는 source `1`을 유지하고, acknowledgement는 pending을 0으로 만든다.
- 강제 marker 예외 회귀는 source `2`, original lease remaining `1`, reserved quantity `1`, pending `0`을 모두 복원한다. clean Unity compile과 `PhysicalStockQueryV18DebugScenarios.RunAll()`이 PASS했고 최종 Console Warning/Error는 `0/0`이다.
- 이 체크포인트는 kg·BOM·수량·영양·WU·EWU·가격·레시피·시설 용량을 변경하지 않는다. 다음 슬라이스에서 existing `ConsumableOperationId`를 이 primitive에 연결하고 Character Consumables current-format plan이 receipt phase를 저장·복원하도록 해야 한다.

---

## 34. 구현 체크포인트: 캐릭터 식사 pending Sink outbox·Character Consumables V7

상태: **캐릭터 시설 식사 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- 기존 시설 식사는 `ItemQuantityLease`를 동기 소비한 직후 active plan을 삭제하고 허기·기분·이벤트를 적용했다. 물리 수량 제거와 survival aggregate publication 사이에 저장 가능한 receipt/phase가 없어 중단·복원 시 이미 사라진 식사를 다시 소비했는지 판별할 권위가 없었다.
- 기존 `ConsumableOperationId`가 meal lease owner이자 exact physical operation identity를 소유한다. 새 의미상 ID를 만들거나 character/facility 같은 반복 가능한 semantic key로 deduplicate하지 않는다.
- `DungeonCharacterConsumablesSaveData` current-format version을 V6에서 V7로 올렸다. 과거 save migration은 만들지 않으며 V7의 phase/provenance 누락·부분 기록·비정규 source vector는 restore failure다.
- active meal plan은 `Eating`, `ItemCommitted`, `EffectsPublished` 세 저장 가능 phase를 가진다.
  - `Eating`: physical provenance가 비어 있고 기존 meal quantity lease를 복원한다.
  - `ItemCommitted`: exact reserved Sink가 완료됐고 Physical pending receipt가 반드시 존재해야 한다.
  - `EffectsPublished`: completed-operation ledger와 meal effects가 publication됐으며 receipt acknowledgement만 남는다. ack가 save 직전에 이미 끝난 경우에는 ledger와 exact provenance가 있을 때 cleanup을 허용한다.
- V7 plan은 operation/reason/commit ID, ordinal exact source stack ID vector, quantity `1`, positive input grams, commit 당시 policy violation·contamination outcome을 저장한다. receipt kind는 adapter에서 Sink로 제한하며 reason은 `character-meal-consumed` 단일 권위다.
- 실행 순서는 exact lease revalidation → reserved pending Sink 원자 commit → `ItemCommitted` publication → 저장된 meal/policy/contamination 결과의 domain effect와 completed-operation ledger publication → `EffectsPublished` → exact receipt acknowledgement → active plan·facility slot cleanup이다.
- acknowledgement 실패는 이미 제거된 식사를 복구하거나 다른 stack을 고르지 않는다. `EffectsPublished` plan과 completed ledger, exact pending receipt를 보존하고 다음 tick/restore에서는 domain effect를 다시 적용하지 않은 채 acknowledgement만 재시도한다.
- restore preflight에서는 V7 구조만 검증하고, world/Physical candidate가 query 가능한 candidate build에서 exact pending receipt join을 수행한다. `ItemCommitted` receipt missing, operation/reason/commit/source/quantity/grams mismatch, completed-ledger phase mismatch는 live aggregate를 교체하기 전에 실패한다.
- Survival core는 Items receipt 타입을 참조하지 않는다. 순수 `CharacterMealPhysicalCommitSnapshot`을 port 계약으로 사용하고 Assembly-CSharp adapter가 Items의 receipt를 변환해 `DungeonStory.Survival`의 역방향 assembly 참조를 만들지 않는다.
- focused fixture는 authored NPC·meal facility·실제 physical ration·real quantity lease를 사용한다. 첫 acknowledgement를 강제로 실패시켜 quantity `1→0`, `EffectsPublished`, completed ledger 1, pending receipt 1을 확인하고, 저장·복원 뒤 허기 효과 재적용 0·second Sink 0·receipt 0·active plan 0을 검증한다.
- 같은 fixture는 pending receipt input grams를 1g 변조한 payload가 receipt를 잃지 않고 restore를 거절하는지, acknowledgement 뒤 receipt 없는 `ItemCommitted` payload가 restore를 거절하는지, 4초 전 consume 0과 spoil-before-commit lease release가 유지되는지도 검사한다.
- Unity clean compile, `SurvivalDebugScenarios.RunMealV7PendingOutboxFocused()`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)`가 통과했다. marker는 `V27_CHARACTER_MEAL_V7_PENDING_OUTBOX=PASS`, `V27_CHARACTER_MEAL_V7_COMPOSITION_SAVE=PASS`이고 최종 Console Warning/Error는 `0/0`이다.
- 이 체크포인트는 식사 quantity·nutrition·기분·오염 확률·action time, kg·BOM·WU·EWU·가격·시설 capacity를 변경하지 않는다. 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-seed는 계속 미완료다.

---

## 35. 구현 체크포인트: 원시 야전 식사 V7 pending Sink·복원 exact-once

상태: **원시 야전 식사 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- 기존 `TryConsumeFieldMeal`은 aggregate가 발급한 genuinely unique `ConsumableOperationId`를 이미 보유했지만, reserved quantity를 동기 소비한 뒤 즉시 허기·기분 효과를 적용했다. 물리 Sink와 Survival publication 사이에 저장 가능한 receipt/phase가 없었다.
- 야전 식사는 시설 식사와 동일한 reserved pending Sink primitive를 사용한다. exact quantity lease를 revalidate하고 동일 operation ID로 physical receipt를 commit한 뒤 `ItemCommitted → EffectsPublished → acknowledge` 순서로 완료한다.
- 가상 시설 식별자는 typed 저장 계약을 우회하지 않는다. `building:primitive-field-meal`이라는 canonical `BuildingInstanceId`를 사용하고, restore validator는 이 ID에 대해서만 실제 world building membership을 요구하지 않는다.
- V7 active plan은 exact operation/reason/commit/source stack vector/quantity/positive grams와 commit 당시 policy violation·contamination을 보존한다. acknowledgement 실패 시 completed ledger와 `EffectsPublished` plan, pending Physical receipt가 함께 남는다.
- restore는 exact pending receipt를 join한 뒤 hunger·mood·narrative를 다시 적용하지 않고 acknowledgement와 plan cleanup만 수행한다. 다른 meal stack 선택, 두 번째 physical Sink, 결과 재평가는 없다.
- 야전 식사는 실제 facility slot을 예약하지 않지만 완료의 slot release 명령은 stable operation/facility pair에 대한 멱등 no-op으로 실행한다. 이 원칙은 복원 전 world adapter에 남은 시설 식사 slot owner도 정확히 정리한다.
- 이번 회귀가 복원된 시설 식사의 transient `facilitySlotReserved=false` 때문에 pre-restore slot owner가 남는 기존 누수를 발견했다. 완료 시 조건부 release를 제거하고 멱등 release를 항상 호출해 다음 식사가 거짓 `DeliveryPending`으로 막히지 않게 했다.
- focused fixture는 real Loose preserved-ration, real quantity lease, real Physical pending batch와 first-ack failure를 사용한다. quantity exact 1 debit, completed ledger 1, pending receipt 1을 확인하고 restore 뒤 active plan 0, receipt 0, hunger second application 0을 검증한다.
- 동일 fixture에서 시설 식사의 4초 reservation, spoil-before-commit typed abort와 lease release를 계속 검증한다. 따라서 야전 식사 추가가 기존 시설 식사 시간·신선도·slot 계약을 약화하지 않는다.
- Unity clean compile, `SurvivalDebugScenarios.RunMealV7PendingOutboxFocused()`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)`가 PASS했다. marker는 `V27_CHARACTER_MEAL_V7_PENDING_OUTBOX=PASS facility=pending/restore-exact; field=pending/restore-exact; spoiled=abort; leaseReleased=True`, `V27_CHARACTER_MEAL_V7_COMPOSITION_SAVE=PASS`이고 최종 Console Warning/Error는 `0/0`이다.
- 이 체크포인트는 meal quantity·nutrition·mood·action time, kg·BOM·WU·EWU·가격·시설 capacity를 변경하지 않는다. 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-seed는 계속 미완료다.

---

## 36. 구현 체크포인트: 캐릭터 물질 사용 V8 pending Sink·Loose/FacilityBuffer/Carried exact-once

상태: **캐릭터 물질 사용 terminal domain 완료 / 전역 terminal outbox 이관은 계속 진행 중**.

- 기존 물질 사용은 physical item을 동기 제거한 뒤 tolerance·addiction·overdose RNG와 캐릭터 효과를 즉시 적용했다. Physical debit과 Survival publication 사이에 저장 가능한 receipt/phase가 없어 acknowledgement 실패·저장·복원 시 같은 dose와 결과를 구분할 권위가 없었다.
- 기존 `ConsumableOperationId`를 physical Sink와 completed ledger의 단일 genuinely unique action identity로 사용한다. character/item/facility 같은 반복 semantic key를 deduplication ID로 만들지 않는다.
- `DungeonCharacterConsumablesSaveData` current-format version을 V7에서 V8로 올렸다. 과거 save migration은 만들지 않으며 V8 active substance plan의 phase/provenance/once-resolved target 누락은 restore failure다.
- active substance plan은 `ItemCommitted`와 `EffectsPublished`를 저장한다. plan은 operation/character/item/source, automatic flag, exact Physical operation/reason/commit/source vector/quantity/positive grams, absolute tolerance/addiction/withdrawal/active/cooldown target, effect tolerance ratio와 addiction/overdose 결과를 소유한다.
- 실행 순서는 policy·source 검증 → overdose/addiction과 absolute target 한 번 결정 → pending physical Sink commit → `ItemCommitted` publication → absolute domain state·이벤트·completed ledger publication → `EffectsPublished` → exact receipt acknowledgement → active plan cleanup이다.
- acknowledgement 실패는 dose를 복구하거나 다른 stack을 고르지 않는다. `EffectsPublished` plan, completed ledger와 pending receipt를 보존하고 다음 tick/restore는 domain effect나 RNG를 다시 실행하지 않은 채 acknowledgement만 수행한다.
- Carried dose는 carry inventory와 world `Carried` stack을 같은 adapter transaction에서 제거한다. generic batch service의 Carried/InTransit 거절은 유지하고, 별도 `ICarriedPhysicalItemBatchDispositionService` capability만 exact Carried Sink를 허용한다. physical commit 실패·예외 시 carry snapshot을 복원한다.
- restore는 operation/reason/commit/source/quantity/grams와 pending receipt를 exact join한다. grams 1 증가 변조는 live receipt를 보존한 채 거절하고, acknowledgement 뒤 receipt 없는 `ItemCommitted` payload도 aggregate publication 전에 거절한다.
- 선술집 FacilityBuffer beverage도 같은 V8 physical Sink를 사용한다. focused 회귀가 발견한 inactive fixture를 authored NPC·Active·published composition으로 교정하고 real batch service를 주입해 production contract를 그대로 검증한다.
- 창고 질량 admission의 새 typed failures 9개에 English/Korean authored template과 정확한 parameter arity를 등록했다. `OwnerUnavailable` 호출부의 1/2 인자 흔들림을 2개 인자로 단일화하고 Localization shared/ko/en tables를 동기화했다.
- Unity clean compile, `SurvivalDebugScenarios.RunSubstanceV8PendingOutboxFocused()`, `SurvivalDebugScenarios.RunAll()`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)`가 PASS했다. marker는 `V27_CHARACTER_SUBSTANCE_V8_PENDING_OUTBOX=PASS ... carried=exact`, `V27_SURVIVAL_FULL=PASS scenarios=all`, `V27_CHARACTER_SUBSTANCE_V8_COMPOSITION_SAVE=PASS`이고 최종 Console Warning/Error는 `0/0`이다.
- 이 체크포인트는 dose quantity·물질 효과·중독/과다복용 확률·kg·BOM·WU·EWU·가격·시설 capacity를 변경하지 않는다. subscriber 예외 전구간 원자성, 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-seed는 계속 미완료다.

## 37. 구현 체크포인트: packaged-lot immutable 질량·수술 tare outbox 기반

상태: **런타임/수술 실행 기반과 첫 실제 포장 콘텐츠 production-live 증거 완료 / 나머지 packaged domain 확장 대기**.

- `PackagedLotItemFeature`가 packaged definition의 positive tare gram, disposition, physical output item ID를 authored authority로 소유한다. `DungeonItemDefinition`은 이 값을 immutable runtime snapshot으로만 투영하며 저장 DTO를 gameplay 질량 query에 넘기지 않는다.
- `GenericDefinitionPhysicalItemMassProjector`는 카탈로그 capture 시 packaged total mass와 tare를 한 번 검증한다. 반환·폐기·출력 승계 disposition은 canonical physical output definition을 요구하고, 그 정의의 unit gram이 tare gram과 1g 단위로 다르면 `PACKAGED_LOT_TARE_MASS_MISMATCH`로 시작 전에 실패한다.
- `PhysicalItemMassSubjectAdapter`는 packaged definition을 `PhysicalItemMassSubjectKind.PackagedLot`으로 만들고 total/content/tare 권위를 immutable prepared subject에 고정한다. generic definition total mass와 packaged total mass는 동일하며 tare를 두 번 더하지 않는다.
- 수술 재료 소비는 deterministic exact stack vector를 골라 `surgery-material-sink:{orderId}` pending Sink receipt를 먼저 커밋한다. receipt quantity·total input grams는 saved `SurgeryOrder.materials`에서 재계산한 값과 exact 일치해야 한다.
- reusable/disposable tare는 `receipt.CommitId:tare:{outputItemId}` commit marker를 가진 Loose physical output으로 exact surgery destination claim의 drop cell에 생성된다. 이미 같은 marker의 stack이 있으면 quantity/state/position을 대조하고 재사용하며, 충돌하거나 output spawn이 실패하면 `materialsConsumed`를 게시하지 않는다.
- 모든 tare output이 존재한 뒤에만 surgery domain이 `materialsConsumed=true`를 게시한다. Physical receipt acknowledgement는 별도 단계이며 실패·restore 뒤에는 두 번째 material Sink나 tare output을 만들지 않고 동일 receipt만 정리한다.
- focused fixture는 packaged medicine `160g = content 130g + reusable vial 30g`을 검증하고, 30g authored tare가 40g container definition을 가리키는 경우 fail-loud함을 고정한다.
- Unity 6000.3.8 MCP에서 common focused contract와 실제 authored 마취 수술을 실행했다. 수술은 마취약 2개를 terminal Sink하고 의료 바이알 2개/60g을 한 commit stack으로 exact 반환했으며, pending acknowledgement 뒤 whole-save restore에서도 `2→2`로 복제되지 않았다. fresh Surgery PlayMode `RESULT=PASS; failures=0`, captured Console `0/0`이다.
- 실제 아이템은 아직 reclassify하지 않았다. `medicine:standard`가 production recipe와 surgery consumer를 모두 가진 우선 후보지만, reusable vial은 신규 vial 제작·회수 BOM을 요구하고 disposable packaging은 신규 physical waste·처리 경로를 요구한다. 이 선택 전에는 SO mass/BOM/WU/EWU/price를 수정하지 않는다.
- 이 체크포인트로 packaged-lot 런타임 projection과 수술 tare outbox child만 닫는다. 실제 packaged content, container/waste producer-consumer closure, production-live save/retry, 나머지 ingress, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-se드는 계속 미완료다.

---

## 38. 구현 체크포인트: 공통 packaged tare outbox·식사/물질 Sink 연결

상태: **공통 Items 경계·production 소비 callsite·Unity focused 실행 완료 / 나머지 packaged item 적용 대기**.

- 수술에 있던 tare 생성 코드를 `PackagedLotTareDispositionService`로 분리했다. 서비스는 immutable `IPackagedLotDefinitionQuery`와 `IPackagedLotTareOutputGateway`만 의존하며 수술 주문·캐릭터 상태·저장 DTO를 알지 않는다.
- output gateway는 `GetAllStacks`와 exact Loose output spawn만 노출한다. 광범위한 `IEquipmentPhysicalItemGateway`는 adapter 뒤에 숨겨 서비스와 focused fixture가 저수준 소비·예약·운반 API를 호출할 수 없게 했다.
- terminal Sink의 parent commit ID를 `parent:tare:{outputItemId}`로 확장해 reusable container와 disposable waste를 item ID별로 합산한다. 같은 marker가 있으면 quantity/state/position을 exact 대조해 재사용하고, 중복 marker 또는 좌표·수량 충돌은 새 stack을 만들지 않고 실패한다.
- 이 체크포인트 당시 `DestroyedDuringUse`는 별도의 물리 손실 receipt가 없어 fail-loud했다. 아래 54항에서 parent Sink commit에 결합된 exact destroyed-tare loss receipt를 구현해 이 제한을 대체했다. `TransferredWithOutput`은 terminal Sink에서 계속 거절한다.
- 수술은 공통 서비스 성공 후에만 `materialsConsumed`를 게시하는 기존 순서를 유지한다. 수술 리소스 묶음에서 직접 package query 의존성을 제거해 package 정책의 단일 실행 권위를 Items 경계로 모았다.
- Character Consumables의 식사와 물질 사용은 `EffectsPublished` 뒤 Physical receipt acknowledgement 전에 같은 tare 서비스를 호출한다. saved plan의 exact item ID·committed quantity·commit ID와 현재 actor cell을 사용하고, actor/quantity/item context가 유효하지 않으면 receipt를 보존한 채 재시도한다.
- Survival core는 Items 타입을 새로 참조하지 않는다. port overload는 `CharacterId`, `ConsumableItemDefinitionId`, quantity, commit ID만 전달하며 Assembly-CSharp adapter가 공통 Items 서비스를 호출한다. 기존 isolated fake는 default overload로 소스 호환을 유지한다.
- focused contract source는 packaged medicine 두 개 소비에서 vial `2개/60g` exact 출력, 같은 commit replay 시 spawn call `1회` 유지, 기존 marker stack 위치 변조 거절, explicit loss receipt 없는 `DestroyedDuringUse` 거절을 검사한다.
- Unity 6000.3.8 MCP에서 `PhysicalStockQueryV18DebugScenarios.RunAll()`을 실행해 2개/60g 반환, replay spawn 1회, marker conflict, explicit-loss gate를 current assembly로 PASS했다. 실제 수술 consumer도 같은 공통 경계를 거쳐 마취제 2개→바이알 2개를 반환했으며 Console Warning/Error는 `0/0`이다.
- 이번 체크포인트도 authored item·unit kg·BOM·WU·EWU·가격·nutrition·의료 효과를 변경하지 않는다. 실제 packaged item과 container/waste producer-consumer closure, production-live save/retry, 나머지 ingress, 전수 kg After, EWU·가격, 6인 생존망과 최종 3-se드는 계속 미완료다.

---

## 39. 구현 체크포인트: 마취제·재사용 의료 바이알 authoring 폐쇄 루프

상태: **실제 SO·카탈로그 적용, 전수 원장, production-live 수술, no-op rebuild 결정론 완료 / EWU·가격·6인 생존망 재보정 대기**.

- 첫 실제 packaged item은 `medicine:standard`가 아니라 `medicine:anesthetic`로 제한한다. 표준 약품은 다섯 production Transform의 입력이므로 terminal Sink 전용 tare 처리만 적용하면 중간 공정에서 용기가 누락된다. 반면 마취제는 현재 다른 recipe의 입력이 아니어서 수술 terminal Sink와 정확히 닫힌다.
- 신규 reusable container 권위는 `container:medical-vial`, unit mass `30g`, stack limit `300`이다. 완전 스택은 `9kg`, 일반 운반 하한 `6kg`은 200개이므로 6–11kg 물류 band를 만족한다. `recipe:medical-vial`은 `material:iron-ingot` 1개(`900g`)를 바이알 30개(`30g × 30 = 900g`)로 변환해 물리 질량을 exact 보존한다.
- `recipe:anesthetic`는 기존 몽엽 2·알코올 1에 바이알 1개를 추가하고 마취제 1개를 출력한다. 마취제 total mass는 `120g`, package tare는 `30g`, content mass는 `90g`이며 disposition은 `ReusableContainerReturn`이다.
- 마취제 maxStack은 기존 30개(`3.6kg`)에서 75개(`9kg`)로 조정한다. 응급 주문은 필요한 소량을 계속 우선 운반할 수 있지만, 정상 재고 정리는 50개/6kg부터 75개/9kg까지 일반 운반 band를 구성한다.
- 수술에서 마취제를 terminal Sink하면 공통 tare outbox가 같은 parent receipt에서 바이알 1개/30g을 수술 시설의 owned drop cell에 exact-once Loose output으로 반환한다. 이후 일반 haul·warehouse gram admission을 그대로 사용하므로 즉시 저장 순간이동은 없다.
- builder는 item/recipe 전체 생성 뒤 `ValidateMedicalVialTopology`를 실행한다. exact package feature, 120=90+30g, 바이알 definition 30g, iron input 900g, vial output 900g, anesthetic recipe의 vial 1 input·anesthetic 1 output을 검증한다.
- packaged Transform 지원 전에는 어떤 recipe도 `medicine:anesthetic`를 입력으로 사용할 수 없다. 새 downstream consumer가 추가되면 builder가 fail-loud하여 용기 전달·반환 disposition을 먼저 구현하게 한다.
- Unity 6000.3.8 Bee/Roslyn의 `DungeonStory.Economy`, `Assembly-CSharp`, `Assembly-CSharp-Editor` 세 response file이 모두 exit `0`이다.
- Unity에서 `ResourceEconomyAssetBuilder.Rebuild()`를 실행해 `container_medical_vial.asset`, `recipe_medical_vial.asset`, 마취제 packaged feature/BOM과 item/domain catalog를 실제 생성·갱신했다. 전수 감사는 canonical item `414`, recipe `355`, serialized weight site `1,074`, explicit semantic `51`, package tare contract `1`로 PASS했다.
- foreign-body 수술의 실제 planning 결과는 시설 보정을 합쳐 마취제 2·소독약 2를 요구한다. PlayMode는 이 exact authored 요구량을 읽어 AI haul·수술을 완료하고 마취제 `0+2→0`, 바이알 `0→2`, committed stack `1`, restore `2→2`를 증명했다.
- builder가 의미상 동일한 `SerializeReference` feature를 매번 새 객체로 교체해 RID/YAML diff를 만들던 결함을 발견했다. 모든 core/feature 값을 비교하는 semantic no-op gate를 추가해 동일 입력이면 Configure·SetDirty를 생략한다. Economy item/recipe/crop/material/catalog 494개 파일의 합성 SHA-256은 연속 rebuild에서 `D79A6DD98AFF500DBBE2A67CABD083C4768C5846A9B0689F18FF163D1F731997`로 동일했다.
- current-format save를 함께 돌리며 만료된 식사 delivery 교체가 old pending row를 남겨 동일 route 중복을 만드는 결함도 발견했다. 새 delivery request가 성공한 뒤 old row와 route index를 함께 swap하도록 수정했고, 확장 Surgery whole-save restore, Survival full, runtime composition, save-section, full PhysicalItemLogistics, AI mid-action repeated restore가 모두 current source에서 PASS했다.

## 40. Unity MCP 재개 뒤 실제 적용·결정론·교차 저장 검증

상태: **현재 packaged-lot 수직 슬라이스 검증 완료 / 전체 V27 질량 재조정은 계속 진행 중**.

- 실제 Unity asset authority:
  - `container:medical-vial`: `30g`, `MaxStack=300`, 연구 `research:pharmacology:anesthesia`
  - `recipe:medical-vial`: 철괴 `1×900g → 30×30g`; builder seed work `12`, 현재 gameplay authored `RequiredWork=160`
  - `medicine:anesthetic`: total `120g`, reusable tare `30g`, content `90g`, `MaxStack=75`
  - `recipe:anesthetic`: 몽엽 2 + 알코올 1 + 바이알 1 → 마취제 1; builder seed work `16`, 현재 승인된 recurring gameplay `RequiredWork=28`
- 전수 권위: canonical item `414`, recipe `355`, serialized weight site `1,074`, explicit unit semantic `51/414`, remaining semantic `363`, package tare contract `1`, unknown writer `0`.
- 실제 실행 증거:
  - `Artifacts/QA/surgery-playmode-report.txt`: `RESULT=PASS; failures=0`, actual anesthetic `2` Sink, vial `2/60g` exact return, pending cleared, current-format restore no duplicate, captured Console `0/0`
  - `Artifacts/QA/physical-item-logistics-playmode-report.txt`: `RESULT=PASS; failures=0`, warehouse gram admission/UI, `19.1/28.7kg` carry UI, over-capacity restore/AI evacuation exact conservation, captured Console `0/0`
  - `Artifacts/QA/ai-mid-action-save-load-playmode.txt`: `result=PASS`, `failures=0`, committed pickup과 restore 2회 뒤 물리 수량 `23` exact 보존
  - `SurvivalDebugScenarios.RunAll()`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)` current assembly PASS
- 결정론 증거:
  - Economy asset 494개 전체 합성 SHA-256이 연속 no-op rebuild에서 `D79A6DD98AFF500DBBE2A67CABD083C4768C5846A9B0689F18FF163D1F731997`로 동일
  - V27 inventory/semantic/transform/profile/writer 산출물 8개의 개별 SHA-256이 연속 AuditOnly 실행에서 모두 동일
  - item builder는 semantic equality가 성립할 때 `SerializeReference` feature graph 재생성과 `SetDirty`를 생략해 managed-reference RID churn을 차단
- 이 체크포인트가 닫은 범위는 첫 reusable packaged Sink의 실제 자산·질량·회수·저장·결정론이다. 나머지 `363`개 item semantic, packaged Transform, 나머지 container/disposable-waste item, 전체 warehouse ingress cutover, EWU·가격 재생성, 6인 생존망, Floor Clutter/paired run, 최종 3-seed 실측은 아직 완료되지 않았다.
- 이 변경은 첫 실제 질량/BOM 후보를 authoring source에 추가했지만 EWU·가격·6인 생존망은 아직 재생성하지 않았다. 따라서 경제 균형 영향은 **검증 대기**다.

---

## 41. 구현 체크포인트: 반환 의료 바이알의 AI 창고 회수·마취제 재생산 production-live 폐쇄

상태: **첫 reusable packaged-lot의 수술→회수→창고→재투입→재생산→저장 복원 순환 완료 / 전체 V27 질량 재조정은 계속 진행 중**.

- 실제 foreign-body 수술이 `medicine:anesthetic` 2개를 terminal Sink한 뒤 `container:medical-vial` 2개/60g을 수술 시설의 owned drop cell에 하나의 commit-provenance Loose stack으로 반환한다. 검증기는 이 반환 stack의 exact output commit component를 식별하며 임의의 같은-ID stack을 대체 증거로 사용하지 않는다.
- 반환 바이알은 저장소로 순간이동시키지 않는다. 실제 `AIHaul`이 일반 warehouse 후보·경로·예약·픽업·입고를 실행하며, 검증기는 미리 특정 창고를 강제하지 않고 AI가 선택한 등록 warehouse의 exact destination과 수량을 추적한다. 실제 결과는 바이알 `2/2`, 시도 `1회`, world total 보존이다.
- 실제 authored `P18_약제대`를 `IGridBuildingObjectFactory`와 현재 grid placement 계약으로 배치하고 `research:pharmacology:anesthesia`를 해금한다. 테스트 전용 가상 생산자나 direct output spawn으로 생산 단계를 대체하지 않는다.
- 같은 실제 warehouse에 몽엽 2, 알코올 1, 회수 바이알 2가 존재하는 상태에서 `recipe:anesthetic`의 real `ProductionBill`을 생성한다. 실제 `AIHaul`이 몽엽 2+알코올 1+바이알 1을 약제대 입력 `FacilityBuffer`로 운반한다.
- `IProductionBillWorkExecution`은 실제 actor와 bill을 사용해 한 cycle만 실행한다. 결과는 마취제 `0→1`, 바이알 `2→1`이고, 출력은 canonical `production-output:{buildingInstanceId}` 목적지의 `FacilityOutputBuffer`에 정확히 1개 생성된다. `RepeatCount=1` 완료 뒤 bill이 제거되는 것은 정상 terminal 상태다.
- 생산 완료 frame에서 출력을 관찰해 다른 AI가 즉시 후속 운반한 결과와 생산 commit을 혼동하지 않는다. output state를 일반 입력 `FacilityBuffer`로 가정하지 않으며, 현재 `ProductionItemGateway`의 canonical `FacilityOutputBuffer` 권위를 검증한다.
- 생산 직후 whole-game current-format save/restore를 수행했다. 복원 뒤 마취제 1, 바이알 1이 유지되고 생산 출력·바이알 소비가 다시 실행되지 않아 exact-once가 성립한다.
- 장시간 수술 PlayMode에서 이미 퇴역한 character 또는 사라진 facility를 가리키는 `CharacterConsumables` delivery row와 route index가 save 직전까지 남을 수 있는 교차-domain 결함을 발견했다. `PruneExpiredDeliveries`는 elapsed time뿐 아니라 live character/facility membership을 검사하고, stale/mismatched route index도 함께 제거한다. reconciliation은 tick의 delta-time early return보다 먼저, 그리고 Capture 직전에도 실행한다.
- 이 정리는 저장 DTO를 임의 보정하는 fallback이 아니다. 존재하지 않는 actor/facility에 전달할 수 없는 transient request만 current runtime authority에서 제거하며, 유효한 pending physical receipt나 active delivery를 삭제하지 않는다. 이후 `SurvivalDebugScenarios.RunAll()`이 current assembly에서 PASS했다.
- 최종 `Artifacts/QA/surgery-playmode-report.txt` 증거:
  - `ANESTHETIC_RECYCLE_RETURNED_VIAL_LOOSE_SOURCE`: expected/actual `2/2`
  - `ANESTHETIC_RECYCLE_VIAL_AI_WAREHOUSE_INTAKE`: stored/total `2/2`, attempts `1`
  - `ANESTHETIC_RECYCLE_INPUTS_AI_DELIVERED`: vial `1`, dreamleaf `2`, alcohol `1` in real input buffer
  - `ANESTHETIC_RECYCLE_PRODUCTION_LIVE_EXACT_ONCE`: anesthetic `0→1`, vial `2→1`, `FacilityOutputBuffer:1`
  - `ANESTHETIC_RECYCLE_PRODUCTION_RESTORE_NO_DUPLICATE`: anesthetic `1`, vial `1`
  - `RESULT=PASS; failures=0`, captured Warning/Error `0/0`; Unity Editor final Console Warning/Error `0/0`
- 이 체크포인트가 닫은 것은 **마취제 한 종류의 reusable-container production-live 순환**이다. 나머지 item semantic `363`, packaged item의 Transform 중 용기 승계·반환, 다른 container/disposable-waste 품목, 나머지 warehouse ingress, 전수 kg After, EWU·가격 재생성, 6인 생존망, Floor Clutter/paired run, 최종 3-se드는 계속 미완료다.

---

## 42. 구현 체크포인트: Unity MCP 물리 질량·장비·의복 current-revision 재인증

상태: **Phase 4 완료 / Phase 2 semantic 코드 예상 `363/414`·Unity artifact `51/414`, Phase 5 전수 질량 감사 계속 진행 중**.

- Unity 6000.3.8 current assembly에서 `PhysicalStockQueryV18DebugScenarios.RunAll()`을 다시 실행해 generic·unique·packaged lot 질량, warehouse gram admission, over-capacity restore, typed disposition, pending receipt와 성능 marker를 전부 PASS했다.
- `V27PhysicalMassAuthorityInventoryDebugScenarios`는 canonical item `414`, recipe `355`, serialized weight site `1,074`, combat equipment `61`, apparel `56`, unknown writer `0`, explicit semantic `51`, remaining semantic `363`을 current asset에서 재캡처했다.
- `V27PhysicalMassExplicitSemanticDebugScenarios`를 다시 실행해 현재 51개 명시 semantic과 recipe transform/profile/writer 산출물을 exact current source로 검증했다.
- `PhysicalItemDebugScenarios`, `EquipmentItemStateV18DebugScenarios`, `CombatEquipmentMaterialDebugScenarios`, `V22ApparelDebugScenarios`를 같은 Unity session에서 실행했다. base item+module+loaded ammunition, equipment instance revision, apparel material projection, 멜빵 physical `1,150g` 단일 계상과 질량 불변 상태가 모두 PASS했다.
- 전투 장비·의복 경로는 runtime mass query, world/carry/warehouse/equipped read model이 같은 prepared subject를 사용한다. 품질·내구·신선도·오염·젖음·충전량은 별도 물리 성분이 없는 V27 범위에서 질량 불변이다.
- 위 focused/full 실행 직후 Unity Editor Console Warning/Error는 `0/0`이었다.
- 이 재인증은 Phase 4를 닫지만 363개 unit semantic 작성, 355 recipe 전수 질량 귀속 회계, 다른 packaged Transform/container/waste, 전수 kg After와 파생 EWU·가격을 완료한 것으로 간주하지 않는다.

---

## 43. 구현 체크포인트: Unity MCP 전수 재검증·32-seed 물류와 경제 소스 리비전 게이트

상태: **물리/공간/N+1/32-seed paired clutter current-revision PASS / output-capacity PlayMode 증거 fresh / 후속 방어시설 BOM 변경으로 노동·시설 재승인 재기준화 필요 / 전체 kg 재조정은 계속 진행 중**.

### 43.1 이번 Unity MCP 세션에서 닫힌 체크리스트

- [x] Unity `6000.3.8f1` current source 컴파일 완료, 최종 Console Warning/Error `0/0`.
- [x] 경제 builder 5종 no-clobber 전수 실행: 검사 파일 `7,219`, byte 변경 `0`, `RESULT=PASS`.
- [x] `PhysicalItemDebugScenarios.RunAll()` current assembly 전수 실행: `44/44 PASS`, Console `0/0`.
- [x] V27 비대칭 mEWU·SCC zero tolerance·RFC 4180·결정론·성능 원장 계약 `13/13 PASS`.
- [x] 장비·의복·멜빵 current-revision 질량 계약 재실행: physical stock/equipment/material/apparel 전부 PASS.
- [x] 6인 음식·물 폐쇄 루프 재실행: 수요 `300 nutrition/일`, gross 목표 `375`, 실제 gross `420`, net 하한 `330`, 반복 노동 `78.538/270 WU = 29.1%`, 7일 비축과 N+1 경로 `10`개 PASS.
- [x] 인구 `1/3/6/12/18/24` 공간 solver: `1,536/1,536 PASS`, 최소 headroom `47.7%`, 정상/장애 이용률 `44%/61.6%`.
- [x] 실제 asset 공간 검증: `1,536/1,536 PASS`, 최소 headroom `30.7%`, 정상/장애 이용률 `60%/84%`, 폭 `27/49/65/81`.
- [x] 서비스 연속성: 24시간 장애, 실제 primitive fallback `10`개, 중복 조리대/펌프 강제 `0`, redundancy capital `0` PASS.
- [x] 출력 버퍼 saturation/typed recovery `2/2 PASS`.
- [x] Floor Clutter 4-arm PlayMode를 32 seed로 fresh 실행: `512` window, floor row `640`, failure `0`, clean A/B exact, RNG cross-talk `0`, 외생 사건 divergence `0`, persistent/access/egress clutter `0`, runtime headroom 최저 `30.6%`, Wait WU median/p95/max `0%`, Console `0/0`.

### 43.2 current authority 전수 수집 결과

- canonical item `414`, recipe `355`, serialized weight site `1,074`, unknown writer `0`.
- explicit unit semantic `51/414`; 아직 정의할 semantic `363`.
- material profile `51`, transform contract `39`.
- 이 수치는 시스템 기반과 첫 packaged-lot 수직 슬라이스의 작동을 증명한다. `363`개 의미 정의와 `355` recipe 질량 감사를 대신하지 않으며, 전수 kg After·EWU·가격 적용 완료를 의미하지 않는다.

### 43.3 새 BOM으로 만료된 경제 승인과 안전한 재기준화

- 256-seed 경제 감사가 `building:8882`(`RF82_방_배정대`)에서 current WU `234`, historical Before `128`, 새 optimizer After `240`을 발견해 fail-loud했다.
- current asset에는 기존 승인 뒤 추가된 `component:room-partition-kit=1` BOM이 있다. 기존 approval은 WU `128→234`, iron `2→3`, paper `4→6`과 이전 source digest를 소유하므로 새 BOM source revision에서 자동으로 만료되는 것이 정상이다.
- 일반 audit의 stale-drift 거절은 유지한다. 별도 approval-refresh 모드에서만 current authored 값이 **canonical 이전 approval의 exactAfter와 정확히 같고**, 이전 exactBefore·reason·baseline identity가 일치할 때 새 source의 target을 계산할 수 있다.
- `RebaseAndApplyPreviouslyApprovedLaborFacilityDriftFromMenu`를 추가했다. 이 명령은 preview 무결성 검사 → 이전 승인/current scalar exact join → 대상 asset·approval byte snapshot → 승인된 property만 적용 → 새 source digest로 approval 재생성 → 표준 audit `requireApplied=true` → 두 번째 dry-run diff `0`을 한 원자 경계에서 수행한다.
- apply/재승인/검증 어느 단계든 실패하면 approval JSON과 이번 대상 asset byte를 원본으로 복구하고 reimport한다. 수동 YAML 편집, wildcard 승인, 이전 source digest의 새 target 승인 재사용은 금지한다.
- 새 source digest mismatch는 재승인 사유이므로 refresh 모드에서만 허용한다. 대신 old After=current equality가 custody gate이고, 새 record source digest는 canonical lowercase SHA-256인지 검증한 뒤 새 approval에 들어간다. 이후 표준 audit은 다시 digest exact 일치를 요구한다.

### 43.4 남은 작업

- [x] Unity MCP 재연결 뒤 world-resource/crop-plot output-capacity PlayMode 선행 보고서와 `v27-balance-output-capacity-playmode.txt`를 current source digest로 재생성하고 Console Warning/Error `0/0`을 확인했다.
- [x] 원자적 bounded rebase/apply를 current authored authority에서 재실행했다. dependency 변화로 목표가 재계산되는 행은 iteration별 exact custody approval 아래에서 고정점까지 다시 캡처하며, 새로 드러난 에셋도 최초 바이트를 한 번만 보관해 승인 파일과 함께 외부 rollback 단위로 묶는다. fresh Unity 실행은 `iterations=1`, `rebasePatches=4`, `directRefreshPatches=370`, `changedAssets=232`, `rollbackAssets=230`, `approvals=1936`, `noOpDiff=0`으로 PASS했다. 이 과정에서 승인 predicate에는 포함됐지만 직접 적용 helper에서 빠져 있던 item-market `203`행을 발견해 같은 exact direct-refresh 범위로 연결했으며, 임의 신규 승인을 만들거나 기존 authored 콘텐츠를 stale After로 덮어쓰지 않았다.
- [ ] 변경 후 표준 256-seed economy simulation PASS 및 unresolved Critical `0` 확인.
- [ ] whole-game coverage·market/labor/facility audit fresh 재실행, 두 번째 artifact/asset diff `0` 확인.
- [x] 명시 semantic 추가 작성분을 Unity에서 재수집해 `363/414`, packaging review 대기 `51`을 current artifact로 확정했다. duplicate/out-of-ledger/haul-class 실패와 asset mutation은 `0`이다.
- [ ] recipe `355`개의 input/output/byproduct/loss gram 전수 감사.
- [ ] 전수 item kg After 적용, warehouse/haul batch와 6인 생존망 회귀.
- [ ] 파생 EWU·구매가·판매가 재생성 및 SCC/순환 차익 재감사.
- [ ] 최종 3-seed 실제/유효 WU와 전체 Console `0/0` 증거.

이 체크포인트에서 **밸런스 완료**라고 보고하지 않는다. 현재 완료된 것은 기반 시스템과 current-revision 물리·공간·서비스·clutter 증거이며, 전수 kg semantic·recipe mass·파생 경제 수치는 계속 미완료다.

---

## 44. 구현 체크포인트: 장비·의복 authority-backed unit semantic 편입

상태: **코드·Roslyn 컴파일·Unity 누적 deterministic recapture PASS / 최종 kg·recipe 보존 대기**.

- [x] 전투 장비 `61`개는 `CombatEquipmentDefinitionSO.ItemId`, `Kind`, `Weight`와 physical item `unitWeight`의 기존 Phase 4 exact join을 semantic 생성 권위로 사용한다.
- [x] 장비 한 단위는 `1 complete combat equipment item`으로 고정한다. 기본 장비 질량만 canonical unit mass에 넣고, 부착 모듈과 장전 탄약은 기존 runtime dynamic component 질량으로 별도 합산한다.
- [x] Apparel definition `56`개와 distinct physical item `56`개를 exact join한다. 이 중 `52`개는 `apparel:` ID이고, 냉기복·룬 냉기복·슬라임 보온패드·멜빵 `4`개는 다른 namespace의 physical item이다. namespace가 다르다는 이유로 중복 semantic을 만들지 않는다.
- [x] 의복 한 단위는 `1 complete apparel item`으로 고정한다. canonical unit mass는 physical item 권위이고 textile/material variant는 기존 apparel projector가 처리한다.
- [x] equipment/apparel semantic은 현재 asset kg를 그대로 Before/After로 캡처한다. 이번 단계에서 SO kg, BOM, WU, EWU, 가격을 수정하지 않는다.
- [x] `PhysicalHaulMassClass.IndividualEquipment`를 추가해 장비 한 점을 원자재의 6–11kg 적재 묶음으로 거짓 판정하지 않는다.
- [x] 운반 클래스 검증을 분리했다: `MicroUrgent ≤11kg`, `IndividualEquipment ≤11kg/한 점`, `Heavy >11–20kg`, `Oversize >20kg`, `Ordinary`만 maxStack 안에서 6–11kg 묶음을 요구한다.
- [x] `DungeonStory.Economy`와 `Assembly-CSharp-Editor`를 current Bee response file로 Roslyn 컴파일해 exit `0`을 확인했다.
- [x] 이 단독 예상치 `168/414`는 후속 누적 구현으로 대체됐으며 Unity에서 최종 semantic `363/414`, missing `51`을 한 번에 recapture했다.
- [ ] 실행 뒤 authority inventory와 whole-game coverage의 source digest를 갱신하고 Console Warning/Error `0/0`을 확인한다.

이 단계는 이미 의미가 확정된 장비·의복만 편입한다. 탄약·약품·폐기물·기록·중간재를 stable-ID 휴리스틱만으로 자동 승인하지 않으며, packaging·recipe transform·sink 계약이 필요한 항목은 계속 review 대상으로 남긴다.

---

## 45. 구현 체크포인트: 비포장 물자 54개 exact unit semantic 편입

상태: **코드·Roslyn 컴파일·Unity deterministic recapture PASS / 최종 gram·recipe 보존 대기**.

- [x] 탄약 `21`개를 exact allowlist로 편입했다. 한 단위는 화살·볼트·탄약통 묶음이 아니라 실제 전투 소비량과 일치하는 발사체/카트리지/장약/신호탄/다트/캐니스터 `1개`다.
- [x] 기록물 `4`개를 책·색인·장부 한 권/한 부로 정의했다. maxStack `1`인 indivisible physical record이므로 6kg commodity minimum을 요구하지 않는다.
- [x] 부패 폐기물 `4`개를 처리 가능한 표준 waste bundle 한 단위로 정의했다. 생성 질량과 처리/폐기 질량은 이후 byproduct/Transform/Sink 원장에서 별도 감사한다.
- [x] 철괴를 제외한 금속괴 `4`개를 표준 ingot 한 개로 정의했다. 기존 철괴 semantic과 중복하지 않는다.
- [x] raw fiber `7`, yarn `10`, processed textile `3`을 각각 fiber bundle, yarn skein, textile roll/sheet 한 단위로 정의했다.
- [x] `feed:dog-food`를 동물 급식 한 회에 사용하는 feed ration 한 단위로 정의했다. 이미 명시된 hay/silage와 중복하지 않는다.
- [x] 총 추가 수는 `21+4+4+4+7+10+3+1=54`로 fail-loud 고정했다. 누락·추가 stable ID는 Phase 0 source revision 재개방 사유다.
- [x] 모든 항목은 package tare `0`, container ID 없음으로 명시했다. 용기 판단이 필요한 medicine/drug/sample은 이 묶음에서 제외했다.
- [x] current item gram을 semantic의 Before와 provisional After로 동일하게 캡처한다. recipe-derived/byproduct derivation 표시는 Phase 5 감사 라우팅용이며 질량 귀속 회계 통과를 선취하지 않는다.
- [x] Ordinary 품목은 current maxStack 안에서 실제 6–11kg 묶음을 구성할 수 있어야 한다. 기록물은 MicroUrgent 단품 규칙을 사용한다.
- [x] `DungeonStory.Economy`와 `Assembly-CSharp-Editor` current Bee/Roslyn 컴파일 exit `0`.
- [x] Unity MCP에서 두 번 recapture하여 전체 semantic `363/414`, missing `51`, asset mutation `0`, 12개 artifact byte-identical을 확인했다.
- [x] recapture 뒤 ordinary haul-band 실패 `0`, duplicate semantic `0`, out-of-ledger semantic `0`, Console Warning/Error `0/0`을 확인했다.

이 체크포인트는 단위 의미만 닫는다. 탄약·금속괴·섬유·폐기물의 최종 After gram, BOM과 typed 외부 유입·부산물·Sink·손실의 귀속, WU/kg, EWU/kg, 가격/kg은 Phase 5와 5.5를 통과하기 전에는 승인하지 않는다.

---

## 46. 구현 체크포인트: 355 recipe 전수 질량 inventory 생성기

상태: **코드·Roslyn 컴파일·Unity AuditOnly deterministic capture PASS / 개별 계약 검토 미완료**.

- [x] current domain catalog의 `ProductionRecipeSO`를 ordinal recipe ID로 전수 열거하며 기대 수 `355`와 중복 ID `0`을 fail-loud한다.
- [x] 모든 input/output item ID를 current physical item authority에 exact join한다. 명시 semantic이 있으면 proposed canonical gram, 아직 없으면 current asset gram을 사용해 `quantity × unit grams`를 checked `long`으로 계산한다.
- [x] current gram fallback이 하나라도 있는 recipe는 `missingSemanticIds`를 기록하고 Transform을 `unit-semantic-missing`으로 분리한다. provisional current gram으로 계산한 mass-creation 후보는 별도 bool로 보존하되 final Critical로 확정하지 않는다.
- [x] clean water와 wastewater는 `resource:clean-water`의 current `500g/unit`을 사용하고 authored float가 정확한 정수 gram으로 환산되지 않으면 거절한다.
- [x] 확률 출력은 1개의 평균값으로 축약하지 않는다.
  - `guaranteedOutputGrams`: probability `1`인 출력만 포함
  - `maximumOutputGrams`: probability `>0`인 모든 출력이 발생한 분기
  - `expectedOutputGrams`: decimal probability 기대값
  - 최대 분기의 disposition이 input+declared external input을 넘으면 당시 schema의 `mass-creation-critical`, 현행 schema의 `external-input-authority-missing`
- [x] `Source`, `Transform`, `Sink`의 authored flow role과 input/output shape를 대조한다. 불일치는 `flow-role-shape-critical`이며 source/sink 외부 질량을 transform conservation에 섞지 않는다.
- [x] 기존 reviewed transform `39`개를 recipe ID로 exact join한다. physical input, infrastructure water, physical output, wastewater 이하 byproduct 관계가 drift하면 capture 자체를 실패시킨다.
- [x] reviewed contract가 없는 Transform은 질량 생성이 없더라도 `disposition-contract-missing`으로 남긴다. residual gram을 임의 손실로 자동 승인하지 않는다.
- [x] 각 recipe 행에 input/output vector, flow role, 최소/최대/기대 output, wastewater, residual 범위, probabilistic output 수, missing semantic ID, provisional mass-creation 후보, reviewed contract, status, source path/digest를 기록한다.
- [x] 산출물 경로를 계획의 canonical `Artifacts/QA/v27-recipe-mass-balance.csv`로 고정하고 별도 audit 요약 `Artifacts/QA/v27-recipe-mass-balance-audit.txt`를 추가했다.
- [x] 두 번 Capture 결과의 CSV/report byte identity와 inspected item/recipe asset digest 불변을 요구한다.
- [x] `DungeonStory.Economy`와 `Assembly-CSharp-Editor` current Bee/Roslyn 컴파일 exit `0`.
- [x] Unity MCP에서 전수 capture를 실행해 source `23`, transform `328`, sink `4`, mass-creation Critical `83`, missing-disposition `159`, missing-semantic recipe `47`을 current artifact로 확정했다.
- [x] `material:granulated-powder`의 단위를 `2,150g→850g`으로 교정하고 builder no-clobber 권위를 추가했다. `black-powder×2 + paper×1 = 5,300g` 입력은 `850g×6 = 5,100g` 출력과 명시적 milling/screening dust `200g`으로 닫혀 `mass-creation-critical` 한 건을 제거했다. ordinary 8~12개 묶음은 `6.8~10.2kg`이며 transform/recipe/authority/packaging artifact 두 번째 실행 byte diff는 `0`이다.
- [ ] 현행 `external-input-authority-missing`을 우선 0으로 만들고, 나머지 Transform마다 physical byproduct, terminal Sink, abstract loss 또는 exact equality 계약을 작성한다.
- [ ] probabilistic recipe는 저장된 WIP 결과의 실제 분기마다 output+byproduct+loss가 input과 exact 일치하는지 PlayMode로 증명한다.

이 원장은 Phase 5의 **누락을 전수 가시화하는 기반**이다. `RESULT=IN_PROGRESS`로 출력하며 reviewed exact 수가 355가 되거나 Source/Sink의 별도 계약까지 모두 닫히기 전에는 Phase 5 PASS를 선언하지 않는다.

---

## 47. 구현 체크포인트: 원초 자원 22개 exact unit semantic 편입

상태: **코드·Roslyn 컴파일·Unity 누적 deterministic recapture PASS / 최종 gram 대기**.

- [x] 포장 결정을 요구하지 않는 current 원초 자원 `22`개를 exact stable ID로 편입했다.
- [x] `resource:blood`는 `0.5L` physical liquid portion으로 고정하고 current `500g`을 provisional gram으로 사용한다.
- [x] bloodleaf·moonflower는 medicinal-herb harvest bundle로 정의했다.
- [x] bone·fang·fat·hide·horn·wool과 feather는 animal-material portion/bundle로 정의했다.
- [x] coal·gold ore·iron ore·lead ore·sulfur는 mined mineral lot, stone은 quarried stone block으로 분리했다.
- [x] dark resin, mana crystal, rune dust, shade fiber, trail charm, manure의 단위 의미를 각각 resin bundle, crystal, dust sachet, fiber bundle, charm, manure bundle로 명시했다.
- [x] feather·rune dust·trail charm은 current maxStack으로 6kg commodity batch를 만들 수 없으므로 MicroUrgent로 분리했다. 나머지 Ordinary 자원은 current maxStack 안에서 6–11kg 묶음을 구성한다.
- [x] 모든 항목의 package tare는 `0`이다. Source recipe/world source의 외부 질량 유입은 Transform 보존식에서 제외하되 실제 출력 gram은 원장에 남긴다.
- [x] current gram은 `unit-reviewed-mass-provisional`이며 kg asset을 바꾸지 않는다.
- [x] 이 단계까지의 combined semantic 예상치는 `244/414`, remaining `170`; Unity recapture 전에는 공식 artifact 수치를 `51/414`로 유지한다.
- [x] current Economy/Editor Bee/Roslyn 컴파일 exit `0`.
- [x] Unity에서 semantic double-capture·recipe inventory를 실행해 duplicate/out-of-ledger/haul-class 실패 `0`을 확인했다.

원초 자원 semantic 확정은 “한 개의 의미”와 운반 묶음만 닫는다. 채굴·채집 노드 수율, 동물 source, 도축 byproduct, recipe After gram과 EWU/가격은 후속 전수 감사에서 별도로 닫는다.

---

## 48. 구현 체크포인트: 공구 17·보철 3·축사 침구 1 unit semantic

상태: **코드·Roslyn 컴파일·Unity 누적 deterministic recapture PASS / 최종 gram 대기**.

- [x] `tool:hauling-harness`는 이미 apparel physical item semantic에 포함되므로 공구 목록에서 제외해 중복 semantic을 방지했다.
- [x] maxStack `1`인 reusable tool `9`개를 indivisible `1 complete reusable tool`로 정의하고 `IndividualEquipment` 운반 규칙을 적용했다.
- [x] 재고 묶음으로 운반되는 crucible/hoist/repair/maintenance/probe/tool-head/gauge/prospecting kit `8`개를 ordinary tool/kit 단위로 정의했다.
- [x] 좌측 팔·눈·다리 보철 `3`개를 수술에 설치하는 indivisible prosthetic part 한 개로 정의했다.
- [x] `husbandry:bedding`은 축사에 투입하는 bedding bundle 한 묶음으로 정의했다.
- [x] ordinary tool/kit과 bedding은 current maxStack으로 6–11kg 묶음을 구성하며, 단품 tool/prosthetic은 한 점 단위 검증을 사용한다.
- [x] current gram은 provisional이며 recipe/tool-shape derivation의 최종 After mass를 승인하지 않는다.
- [x] 이 단계까지의 combined semantic 예상치는 `265/414`, remaining `149`; current Unity artifact는 recapture 전 `51/414`다.
- [x] current Economy/Editor Bee/Roslyn compile exit `0`.
- [x] Unity semantic/recipe double-capture에서 duplicate `0`, haul-class failure `0`, asset mutation `0`을 확인했다.

이 단계는 공구의 사용 횟수·내구·수리 소모량이나 보철의 의료 효과를 바꾸지 않는다. kg/효능/WU/EWU 결합은 Phase 5.5에서 별도 검토한다.

---

## 49. 구현 체크포인트: 제조 부품 36개 exact unit semantic

상태: **코드·Roslyn 컴파일·Unity 누적 deterministic recapture PASS / 최종 gram 대기**.

- [x] current ledger의 `component:` 품목 `36`개를 exact allowlist로 편입했다.
- [x] engineering drawing과 factory installation plan `2`개는 한 부/한 장의 physical engineering document로 분리했다.
- [x] 나머지 `34`개는 BOM 한 줄에서 수량으로 소비되는 manufactured component/subassembly 한 개로 정의했다.
- [x] current provisional gram `≤2,000g`은 SmallComponent, `>2,000g`은 LargeComponent로 분류한다. final After가 경계를 넘으면 semantic review를 다시 열어야 한다.
- [x] 모든 component는 maxStack `50`에서 current gram 기준 6–11kg ordinary batch를 구성한다.
- [x] `sealed-seasonal-container` 등 빈 component 자체는 packaging tare가 아니다. 내용물이 든 packaged item이 이 container를 참조할 때만 별도의 tare disposition을 요구한다.
- [x] current gram은 provisional이며 BOM/recipe mass balance를 아직 통과한 것으로 취급하지 않는다.
- [x] 이 단계까지의 combined semantic 예상치는 `301/414`, remaining `113`; current artifact는 Unity recapture 전 `51/414`다.
- [x] current Economy/Editor Bee/Roslyn compile exit `0`.
- [x] Unity 최종 double-capture에서 component duplicate/missing/haul failure `0`, 전체 semantic `363/414`, missing-semantic recipe `47`을 확인했다.

이 단계는 부품 수량 단위만 닫는다. facility kit의 packed abstraction, 설치 후 해체 회수, 부품 recipe의 부산물·손실과 노동/가격 밀도는 후속 Phase 5/5.5 대상이다.

---

## 50. 구현 체크포인트: 창고 후보 판정의 count → exact item gram 전환

상태: **코드·Roslyn 컴파일·Unity focused mass/haul PASS / 전체 Physical Logistics PlayMode 대기**.

- [x] 생산 출력의 창고 가능성 API를 `HasCompatibleWarehouse(StockCategory)`에서 `HasCompatibleWarehouse(itemId, StockCategory)`로 바꿨다. category만으로는 실제 출력 한 단위의 gram을 알 수 없으므로 kg admission을 증명할 수 없다.
- [x] 생산 분배는 출력 `itemId`를 bridge까지 전달한다. bridge는 canonical item ID, category acceptance, `WarehouseInventory.CanStoreItem(itemId, 1)`을 모두 만족하는 창고만 후보로 인정한다.
- [x] 일반 운반 계획의 빠른 destination 탐색은 `CanStore(category, 1)`을 제거했다. stock item은 category acceptance를 별도로 확인하고, stock·equipment 모두 exact item ID 기반 `CanStoreItem` 질량 판정을 통과해야 한다.
- [x] 운반 가능 수량·후보 총중량·opportunistic 누적중량이 definition kg가 아니라 exact `WorldItemStackRecord`의 instance/component subject를 `PhysicalItemMassSubjectAdapter → IPhysicalItemMassQuery.GetStackUnitMass`로 계산한다. 동일 subject가 warehouse admission과 실제 carry에도 사용되며 equipment focused fixture가 `plan.TotalWeight == exact stack mass`를 검증한다. Roslyn Main/Editor compile, Haul Plan 전체 focused suite와 equipment exact assertion, Console Warning/Error `0/0`을 통과했다.
- [x] Gameplay Flow 진단의 `CanAcceptLooseStack`도 loose stack의 exact `ItemId`로 판단한다. UI/진단이 실제 물류보다 낙관적으로 표시되는 count/kg 괴리를 제거했다.
- [x] `CanStoreItem`은 production mass authority가 있는 창고에서는 `RemainingMassGrams / definitionUnitMassGrams`, legacy/editor fixture에서는 기존 count capacity를 사용한다. 이번 변경은 창고 capacity asset이나 item kg를 바꾸지 않는다.
- [x] production `CanStore(category, 1)` callsite를 다시 검색해 `0`임을 확인했다.
- [x] `PhysicalStockQueryV18DebugScenarios`에 production `.CanStore(...)` callsite `0` manifest gate를 추가했다. 신규 count-only 후보 판정은 exact item identity를 `CanStoreItem`까지 전달하지 않으면 focused CI에서 실패한다.
- [x] 25,000g fixture에서 lumber `1,200g` 기준 remaining `13,000g`은 정확히 `10개`만 허용하고 `11개`는 거절하는 read-side 경계 테스트를 추가했다.
- [x] `DungeonStory.Economy`와 `Assembly-CSharp` current Bee/Roslyn 컴파일 exit `0`.
- [x] 신규 recipe inventory 파일을 명시 포함한 `Assembly-CSharp-Editor` Roslyn 컴파일 exit `0`.
- [x] Unity에서 mass-focused scenario를 재실행해 25kg 창고의 exact gram admission, dynamic equipment mass와 count-only 후보 우회 `0`을 확인했다. stale module fixture ID·attached ownership·빈 fake catalog를 strict production 계약에 맞게 교정했고 Physical Stock V18 및 Haul Plan/Construction Safety가 PASS했다.
- [ ] PhysicalItemLogistics PlayMode와 생산 출력 버퍼 회귀에서 count 기반 false-positive `0`, reservation leak `0`, Console Warning/Error `0/0`을 확인한다.

이 변경은 **후보 판정**을 실제 commit 경계와 일치시키는 작업이다. 최종 입고는 기존 `WarehouseMassAdmissionToken`의 revision·exact lot·commit receipt 계약을 계속 사용하므로, `CanStoreItem`의 true만으로 물리 소유권이나 용량을 선점하지 않는다.

---

## 51. 구현 체크포인트: 비포장 가공 소재·고형 공예품·전리품 48개 semantic

상태: **코드·정적 catalog 교차검증·Roslyn 컴파일·Unity deterministic recapture PASS / remaining package 계약 대기**.

- [x] catalog Apparel definition `56`개와 distinct physical ID `56`개를 asset GUID까지 교차검증했다. `apparel:` namespace는 `52`개일 뿐이며, 냉기복 3개와 멜빵은 기존 apparel semantic 권위에 이미 포함된다.
- [x] 기존 `ExpectedApparelItemSemantics=52`를 `56`으로 수정하고 definition count도 별도 fail-loud한다. 냉기복 3개를 새 묶음에 중복 추가하지 않는다.
- [x] 가공 소재 `40`개를 exact allowlist로 편입했다.
  - metal/powder/paper/mesh/shot/alloy/blank/composite lot `11`
  - ordinary textile/leather/rope/thread stock `14`
  - small bowstring/dreamweave component `2`
  - organic/fuel/process lot `8`
  - processed lumber `2`, cut stone `1`, paper stock `1`, mending scrap `1`
- [x] powder·textile·organic process lot은 `BulkInfrastructureNotInUnit`로 명시해 개별 포장 tare가 unit mass에 포함되지 않음을 고정했다. 별도 용기를 생성·소비한다고 가정하지 않는다.
- [x] 고형 craft item `6`개만 편입했다: bone charm, candle, ritual banner, gold ornament, soap, stone ornament. poison·balm·ritual reagent·trap coating은 포장/도포 Sink 계약 전까지 제외했다.
- [x] appraised/unappraised loot `2`개를 small valuables lot으로 편입했다. current max stack이 6kg 미만이므로 `MicroUrgent`이며 ordinary 묶음 규칙을 우회하지 않는다.
- [x] bowstring·dreamweave도 current full stack이 6kg 미만이라 `MicroUrgent`; 나머지 ordinary `44`개는 current maxStack 안에서 6–11kg 묶음을 구성한다.
- [x] 새 method 구간과 414-item candidate inventory를 교차해 exact item ID `48`개를 확인했다.
- [x] current grams를 provisional Before/After로만 사용하며 kg/BOM/WU/EWU/가격 asset은 변경하지 않았다.
- [x] 누적 코드 예상치는 `349/414`, remaining `65`; current Unity artifact는 recapture 전 `51/414`다.
- [x] 신규 recipe inventory 파일을 포함한 `Assembly-CSharp-Editor` Roslyn compile exit `0`.
- [x] Unity double-capture에서 누적 explicit `363`, missing `51`, duplicate/out-of-ledger/haul failure `0`, asset mutation `0`, byte-identical을 확인했다.
- [ ] remaining `51`의 package tare·container return/waste·Transform/Sink 계약을 먼저 닫은 뒤 semantic을 추가한다.

이 단계는 **포장 결정을 피할 수 있는 단위 의미**만 닫는다. 후속 integral-solid 승격 이후 remaining `51`에는 약품·항원·의료 키트·액상 도포재·포장 식량·보급 상자가 포함되며, 이름만 보고 tare `0`으로 자동 승인하지 않는다.

---

## 52. 구현 체크포인트: remaining package disposition 전수 review 원장

상태: **AuditOnly 생성기·Roslyn 컴파일·Unity deterministic capture PASS / 개별 계약 적용 대기**.

- [x] `V27PhysicalMassPackagingReviewDebugScenarios`를 추가해 current item `414`, recipe `355`, compiled semantic `355`, missing `59`를 exact count로 fail-loud한다.
- [x] missing count만 고정하지 않고 59개 stable ID allowlist를 ordinal exact 비교한다. 현재 집합은 craft `4`, drug `6`, packaged food `2`, medical `15`, medicine `13`(여기에 `medicine:mycelial-culture-pack` 포함), antigen sample `7`, supply `12`다. 한 항목이 빠지고 다른 항목이 들어와도 count 59만으로 통과할 수 없다.
- [x] 여기서 `414`는 전체 `ItemDefinitionCatalog` 개수가 아니라 V23/V27 EWU 양쪽에 연결된 canonical balance ledger scope다. 현재 카탈로그에는 facility kit·evolution catalyst·runtime mirror 등 파생 정의가 추가로 존재하므로, packaging review는 `CaptureCanonicalLedgerItemIds()`의 exact 414개를 먼저 고정한 뒤 item authority와 join한다.
- [x] 전체 카탈로그를 414개라고 가정하는 검증을 제거했다. ledger 외 정의는 이 원장에서 누락된 것이 아니라 별도의 파생 질량 projector/권위 감사 대상이며, ledger semantic이 scope 밖으로 새면 즉시 실패한다.
- [x] canonical 414 ID vector의 SHA-256 `ledgerScopeDigest`를 report에 기록하고, live `ItemDefinitionCatalog.asset` 자체도 no-mutation/source digest에 포함한다. 동일 개수라도 구성원이 바뀌면 review 기준선이 조용히 재사용되지 않는다.
- [x] missing item마다 current unit grams, maxStack, stock category, `PackagedLotItemFeature`, tare grams/disposition/container ID를 기록한다.
- [x] producer recipe와 consumer recipe를 exact item ID로 역색인해 각각 count와 stable recipe ID vector를 기록한다.
- [x] 각 항목을 `source-input`, `transform-intermediate`, `terminal-output`, `recipe-orphan`으로 분리한다. producer/consumer가 모두 없는 항목은 정상으로 접지 않고 live source/consumer 실행 경로를 별도로 요구한다.
- [x] review route를 명시적으로 분리한다.
  - meal serving 또는 ration package
  - bulk infrastructure 후보
  - specimen container
  - supply package/content role
  - dose container return/waste
  - medical kit의 integral/returned package
  - coating/reagent의 container 또는 target transfer
- [x] 현재 feature가 있어도 자동 승인하지 않는다. `authored-package-requires-runtime-route-proof`로 남겨 실제 Sink/Transform 반환·폐기·이전 경로를 요구한다.
- [x] review route별 `requiredDispositionProof`를 기록한다. 반환·폐기·target transfer·bulk infrastructure·명시 손실 중 어느 증거가 필요한지 행 단위로 고정한다.
- [x] 현재 terminal tare gateway가 연결된 Survival/Surgery source를 source digest에 포함한다. gateway 클래스가 존재하는 것만으로 개별 item route가 증명됐다고 간주하지 않고 `runtimeGatewayEvidence`를 `item-join-unproven` 상태로 남긴다.
- [x] feature가 없으면 `package-contract-required`; item 이름이나 stock category로 tare gram과 container ID를 자동 생성하지 않는다.
- [x] CSV는 stable item ID 순서, RFC 4180 CRLF, UTF-8 no BOM writer를 사용한다. 같은 capture 두 번의 CSV/report byte identity를 요구한다.
- [x] ledger inventory/item/recipe/source/serialization/tare runtime 전체 digest를 capture 전후 비교해 asset mutation `0`을 요구한다.
- [x] canonical artifact를 추가했다.
  - `Artifacts/QA/v27-physical-mass-packaging-review.csv`
  - `Artifacts/QA/v27-physical-mass-packaging-review.txt`
- [x] 신규 packaging review와 recipe inventory source를 명시 포함한 `Assembly-CSharp-Editor` Roslyn compile exit `0`. lifecycle·required proof·runtime gateway 열 추가 후 재컴파일도 exit `0`이다.
- [x] Unity에서 packaging review double-capture를 실행해 current missing `51`의 producer/consumer·runtime consumer를 확정했다. existing packaged feature는 `0`, runtime consumer rows/links는 `28/31`, execution orphan은 `0`이다.
- [ ] 각 행에 `ReusableContainerReturn`, `DisposableWasteByproduct`, `DestroyedDuringUse`, `TransferredWithOutput`, `BulkInfrastructureNotInUnit` 중 하나 또는 package 없음의 근거를 작성한다.
- [ ] physical output이 필요한 tare는 실제 container/waste item 정의, 생산 BOM, terminal disposition receipt, save/restore exact-once PlayMode가 모두 green일 때 semantic으로 승격한다.

이 원장은 남은 항목을 **보이는 검토 대기열**로 만드는 단계다. report는 의도적으로 `RESULT=IN_PROGRESS`이며, producer/consumer가 없거나 runtime terminal route가 증명되지 않은 행을 자동으로 정상 처리하지 않는다.

---

## 53. 구현 체크포인트: 포장 없는 식사·공정 단위 6개 semantic

상태: **코드·Roslyn 컴파일·Unity 누적 deterministic recapture PASS / final package 계약 대기**.

- [x] `component:temporal-stasis-seal`을 separable tare가 없는 solid LargeComponent 한 개로 정의했다.
- [x] `material:alchemical-solvent`를 reusable production/warehouse vessel을 사용하는 bulk process measure로 정의하고 `BulkInfrastructureNotInUnit`를 적용했다.
- [x] boar stew, garden meal, salted-meat stew `3`개는 reusable bowl/serving ware를 facility infrastructure로 분리한 served meal portion이다. 식기 질량은 소비 item unit에 포함되지 않으며 빈 식기 byproduct를 생성하지 않는다.
- [x] jerky는 별도 wrapper/container가 없는 solid ration portion으로 정의했다.
- [x] expedition ration pack과 preserved ration은 이름과 소비 경로상 물리 포장 가능성이 있어 이 묶음에서 제외했다.
- [x] 6개 모두 current gram을 provisional로 유지하고 kg/BOM/WU/EWU/가격 asset을 변경하지 않았다.
- [x] 모든 Ordinary 항목이 current maxStack 안에서 6–11kg 묶음을 구성하는지 계산했다.
- [x] 누적 코드 예상치는 `355/414`, remaining `59`; packaging review 생성기도 같은 exact count를 요구한다.
- [x] semantic·recipe inventory·packaging review source를 명시 포함한 `Assembly-CSharp-Editor` Roslyn compile exit `0`.
- [x] Unity semantic/package double-capture에서 explicit `363`, missing `51`, duplicate `0`, asset mutation `0`, byte-identical을 확인했다.

이 단계에서 `BulkInfrastructureNotInUnit`는 숨은 포장 삭제가 아니다. item mass는 내용물/식사 자체이고, bowl·tank·vessel은 반복 사용되는 시설 측 인프라이므로 item Sink에 동반되지 않는다는 계약이다.

---

## 54. 구현 체크포인트: `DestroyedDuringUse` 포장재의 parent-bound 손실 영수증

상태: **런타임·focused contract·Roslyn 컴파일·Unity focused PASS / 실제 item별 lifecycle 승인 대기**.

- [x] `PackagedLotTareOutputReceipt`에 `DestroyedTareMassGrams`와 `AccountedTareMassGrams`를 추가했다. 반환/폐기 물리 출력 질량과 사용 중 파손·소각되는 명시 손실 질량을 같은 parent Sink commit 아래 분리한다.
- [x] `DestroyedDuringUse`를 더 이상 무조건 실패시키지 않는다. 소비된 exact item quantity × immutable packaged tare gram을 checked 정수 연산으로 합산하고, 물리 output을 생성하지 않는 명시 손실 영수증으로 반환한다.
- [x] `ReusableContainerReturn`과 `DisposableWasteByproduct`는 기존처럼 physical output commit marker를 생성한다. `TransferredWithOutput`은 terminal Sink에서 여전히 typed failure이며 Transform output 계약 없이는 소비할 수 없다.
- [x] focused fixture는 destroyed package `20g × 3 = 60g`, physical output `0`, accounted tare `60g`, spawn 증가 `0`을 검사한다.
- [x] 같은 parent commit을 재실행해 destroyed loss `60g`이 동일하고 물리 output이 추가되지 않음을 검사한다.
- [x] runtime `Assembly-CSharp` Roslyn compile exit `0`; 새 runtime source를 명시 포함한 Editor focused compile도 exit `0`이다. Unity/PlayMode 결과는 MCP 승인 복구 전까지 주장하지 않는다.
- [x] Unity에서 `PhysicalStockQueryV18DebugScenarios`를 fresh 실행해 reusable return·conflict·destroyed loss·replay를 함께 통과시켰다.
- [ ] 실제 `DestroyedDuringUse` item을 승인할 때에는 item별 loss reason, 소비 gameplay route, parent physical disposition receipt, save/restore replay 증거를 packaging review 행과 join한다.

이 영수증은 `DestroyedDuringUse`를 자유로운 질량 삭제로 바꾸지 않는다. loss gram은 authored tare와 exact 수량에서만 계산되고 parent Sink commit ID에 결합되며, content mass나 Transform output 부족분을 이 필드로 흡수할 수 없다.

---

## 55. 구현 체크포인트: Character Consumables의 packaged Sink 무서비스 우회 차단

상태: **런타임 구현·Unity missing-service fault/recovery·기존 production-live DI PASS**.

- [x] 물리 meal/substance Sink acknowledgement 직전에 exact item ID를 `IPackagedLotDefinitionQuery`와 join한다.
- [x] 해당 item이 packaged lot인데 `IPackagedLotTareDispositionService`가 없으면 `*-packaged-tare-service-missing` typed failure로 receipt를 보존하고 acknowledgement를 거부한다.
- [x] 비포장 item 또는 legacy non-physical fixture까지 constructor 단계에서 일괄 거부하지 않는다. 판정은 실제 소비 item 단위라서 비포장 소비 경로의 기존 테스트 계약을 유지한다.
- [x] tare 서비스가 있으면 기존 parent commit·exact quantity·actor cell을 사용한 반환/폐기/손실 처리를 거친 뒤에만 physical Sink receipt를 acknowledge한다.
- [x] runtime `Assembly-CSharp` Roslyn compile exit `0`, diff check exit `0`.
- [x] Unity `SurvivalDebugScenarios.RunPackagedConsumableTareRecoveryFocused()`에서 packaged substance 1개를 Sink한 뒤 tare 서비스를 의도적으로 누락했다. 효과 ledger는 정확히 1개, active plan은 `EffectsPublished`, physical pending receipt는 유지되고 두 번의 retry 후에도 tolerance/addiction과 완료 ledger가 중복되지 않음을 확인했다. acknowledgement는 exact `substance-packaged-tare-service-missing`으로 fail-closed했다.
- [x] 같은 current-format payload에 production `PackagedLotTareDispositionService`와 `PackagedLotTareOutputGateway`를 복구해 빈 바이알 1개를 Loose로 exact-once 게시하고 pending receipt를 acknowledge했다. 후속 Tick에서 container 수량과 completed ledger가 증가하지 않았고, 전체 `SurvivalDebugScenarios.RunAll()` 및 기존 마취제 production-live DI 수술→바이알 회수→재생산 경로가 PASS이며 직후 Console Warning/Error `0/0`이다.

이 게이트는 optional constructor parameter 자체를 신뢰하지 않는다. 실제 immutable mass authority가 item을 packaged lot으로 판정한 경우에만 fail-closed하며, 서비스 누락을 포장재 `0g` 또는 암묵 Sink로 강등하지 않는다.

---

## 56. 구현 체크포인트: 비분리 고형 의료품 5개 semantic 승격

상태: **코드·current-source Roslyn 컴파일·후속 누적 Unity recapture PASS / 최종 질량 계약 대기**.

- [x] 포장 용기가 별도 물리 자본으로 분리되지 않는 고형 의료품 `5`개를 exact stable ID로만 승격했다.
  - `medical:sterile-bandage`: textile·antiseptic dressing 한 개
  - `medical:sterile-mycelium-graft`: 수술에 설치·소비되는 graft 한 개
  - `medical:organ-regeneration-scaffold`: 수술용 solid scaffold 한 개
  - `medical:slime-coagulation-frame`: 수술용 solid frame 한 개
  - `medical:mana-core-case`: 수술에 설치되는 solid case 한 개
- [x] 이 다섯 항목에는 reusable/disposable container tare를 가정하지 않는다. item 본체가 수술의 물리 입력이며, 소비·설치 시 별도 빈 용기를 반환하지 않는다.
- [x] `medical:organ-preservation-canister`는 이름이 유사해도 포함하지 않았다. 장기 저장·연료·반환/폐기 lifecycle이 분리되어야 하므로 packaging review에 유지한다.
- [x] `sterile-bandage`와 `sterile-mycelium-graft`는 `MedicineDoseOrKit`, case/scaffold/frame은 `LargeComponent` 단위 semantic을 사용한다.
- [x] current authored gram과 max stack만 provisional Before/After로 캡처하며 kg, BOM, WU, EWU, 가격, 수술 효과를 변경하지 않았다.
- [x] Ordinary 운반 묶음이 각 max stack 안에서 `6–11kg`을 구성하는지 semantic validator가 검사한다.
- [x] packaging review exact allowlist에서 위 다섯 ID를 제거하고 기대치를 compiled semantic `360/414`, remaining `54`로 갱신했다. count만 맞고 identity가 바뀌는 경우는 계속 fail-loud한다.
- [x] current `Assembly-CSharp` Roslyn compile exit `0`; stale Bee runtime reference를 최신 tare source로 덮어 검증한 `Assembly-CSharp-Editor` current-source compile도 exit `0`이다.
- [x] 중간 기준 `360/414`, missing `54`는 후속 semantic 3개 추가로 대체됐고, 최종 Unity double-capture `363/414`, missing `51`, duplicate/out-of-ledger/haul-class failure `0`, asset mutation `0`, byte-identical이 이를 포함해 검증했다.
- [x] 중간 remaining `54` review는 후속 allowlist 축소를 거쳐 최종 exact `51` producer/consumer/lifecycle 대기열로 fresh artifact에 고정됐다.

이 체크포인트는 위 52·53항의 `355/414`, remaining `59` 예상치를 대체한다. Unity에서 아직 fresh recapture하지 않았으므로 기존 공식 artifact의 `51/414`를 새 수치로 사칭하지 않으며, `360/414`는 current-source compiled expectation으로만 기록한다.

---

## 57. 구현 체크포인트: packaging review의 레시피 외 런타임 소비자 조인

상태: **AuditOnly 원장 코드·current-source Roslyn 컴파일·Unity deterministic recapture PASS / 개별 disposition proof 대기**.

- [x] 기존 packaging review가 producer/consumer recipe만 역색인해 레시피 밖 typed runtime Sink를 `consumer 없음`으로 오분류할 수 있음을 확인했다.
- [x] `PhysicalItemRuntimeConsumerCatalog`를 review의 별도 authority로 연결하고, 모든 link에 canonical item ID·`runtime:` owner ID·live item catalog 존재·중복 pair `0`을 fail-loud 검증한다.
- [x] remaining `54` 중 runtime consumer가 있는 exact `11`개 행과 link `11`개를 고정했다.
  - 의료: cross-lineage medium, fertility treatment, isolation care kit, organ-preservation canister, trait-analysis kit, trauma-care kit
  - 보급: alliance signal kit, certified seed kit, defense mixed-ammo box, funeral preparation kit, performance prop box
- [x] CSV schema를 `v27.mass.packaging-review.2`로 올리고 `recipeConsumerCount`, `consumerRecipeIds`, `runtimeConsumerCount`, `runtimeConsumerOwnerIds`, `totalConsumerCount`를 분리했다.
- [x] lifecycle 판정은 recipe consumer와 runtime consumer의 합을 사용한다. producer와 모든 consumer가 모두 없을 때만 `execution-orphan`이며, runtime Sink가 있는 항목을 `terminal-output` 또는 `recipe-orphan`으로 접지 않는다.
- [x] source/no-mutation digest에 runtime consumer catalog source를 포함했다. owner link가 바뀌면 기존 packaging approval fingerprint를 조용히 재사용하지 않는다.
- [x] generic runtime owner link는 item별 tare 처리 증거가 아니다. 행에는 live owner를 기록하되 반환·폐기·이전·명시 손실 receipt가 확인될 때까지 disposition 상태를 승인하지 않는다.
- [x] current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`.
- [x] Unity packaging review double-capture에서 current runtime consumer rows/links `28/31`, execution orphan `0`, exact 51 identity, byte identity, asset mutation `0`을 fresh artifact로 확인했다.
- [ ] 위 11개 실제 runtime 명령마다 `Source/Transfer/Transform/Sink` disposition receipt와 packaged tare gateway/item join을 역추적한다.

이 변경은 kg·BOM·WU·EWU·가격·효과를 바꾸지 않는 감사 정확성 보강이다. live runtime owner가 있다는 이유만으로 포장 질량을 자동 생성하거나 소비 완료로 인정하지 않는다.

---

## 58. 구현 체크포인트: 룬 동면 촉매의 비분리 고형 수술 재료 semantic

상태: **코드·authority 교차검증·current-source Roslyn 컴파일·후속 누적 Unity recapture PASS / 최종 질량 계약 대기**.

- [x] `medical:rune-hibernation-catalyst`의 실제 recipe가 `component:rune-conductor 1 + material:sterile-composite 1 → catalyst 1`이고 용수·액체·별도 container 입력이 없음을 확인했다.
- [x] 실제 `procedure:rune-hibernation`이 catalyst 본체 한 개를 수술 재료로 소비하며, 반환 용기나 포장 byproduct를 요구하지 않음을 확인했다.
- [x] unit semantic을 `1 solid surgical catalyst`로 고정하고 `MedicineDoseOrKit`, `RecipeMassBalance`, `Ordinary`, package tare `None`으로 편입했다.
- [x] current authored `550g`, max stack `50`은 11개 `6.05kg`부터 20개 `11kg`까지 ordinary 운반 묶음을 구성한다. 이 단계에서는 gram을 수정하거나 최종 질량으로 승인하지 않는다.
- [x] exact packaging review allowlist에서 이 ID만 제거해 current-source 기대치를 `361/414`, remaining `53`으로 갱신했다. array count `53`, ordinal sort exact, duplicate `0`을 정적으로 확인했다.
- [x] current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`.
- [x] 중간 기준 `361/414`, remaining `53`은 후속 semantic 2개 추가로 대체됐고 최종 Unity recapture `363/414`, remaining `51`, duplicate/out-of-ledger/haul-class failure `0`, asset mutation `0`, byte identity가 이를 포함해 검증했다.

이 체크포인트는 56항의 `360/414`, remaining `54` 예상치를 대체한다. 촉매를 고형 재료로 분류한 근거는 이름 추측이 아니라 실제 고형 BOM과 수술 소비 계약이며, 다른 배지·혈청·치료제·키트에는 확장 적용하지 않는다.

---

## 59. 구현 체크포인트: 약초 찜질약·접종 원목의 integral solid semantic

상태: **코드·authority 교차검증·current-source Roslyn 컴파일·Unity deterministic recapture PASS / remaining package 계약 대기**.

- [x] `medicine:herbal-poultice`는 moonflower 1+shade-fiber 1로 2개를 만드는 고형 찜질 dressing이며, 섬유를 빈 포장으로 반환하는 품목이 아니라 치료 재료 본체임을 확인했다.
- [x] 약초 찜질약 한 단위를 `1 complete herbal poultice dressing`, `MedicineDoseOrKit`, `RecipeMassBalance`, package tare `None`으로 정의했다.
- [x] current authored `200g`, max stack `50`은 30개 `6kg`부터 50개 `10kg`까지 ordinary 운반 묶음을 구성한다.
- [x] `supply:inoculated-log`는 treated lumber 1+cave mushroom 1로 2개를 만들고 균사 재배 선반 cycle input으로 본체가 투입되는 접종 원목임을 확인했다.
- [x] 접종 원목 한 단위를 `1 inoculated cultivation log`, `LogSection`, `RecipeMassBalance`, package tare `None`으로 정의했다.
- [x] current authored `1,800g`, max stack `50`은 4개 `7.2kg`부터 6개 `10.8kg`까지 ordinary 운반 묶음을 구성한다.
- [x] exact packaging review allowlist에서 위 두 ID만 제거해 current-source 기대치를 `363/414`, remaining `51`로 갱신했다. array count `51`, ordinal sort exact, duplicate `0`을 확인했다.
- [x] current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`.
- [x] Unity semantic/package double-capture에서 `363/414`, remaining `51`, duplicate/out-of-ledger/haul-class failure `0`, asset mutation `0`, byte identity를 확인했다.

이 체크포인트는 58항의 `361/414`, remaining `53` 예상치를 대체한다. 섬유 dressing과 접종 원목의 물질 자체를 포장 tare로 오인하지 않으며, 병·상자·표본 용기가 필요한 다른 약품·보급품에는 이 결정을 확장하지 않는다.

---

## 60. 구현 체크포인트: 질병 대응·원정 보급 runtime consumer 전수 등록

상태: **runtime audit authority·focused exact-set·Roslyn 컴파일·Unity focused PASS / item별 typed disposition·최종 경제 대기**.

- [x] `DiseaseFieldResponseRuntime`의 실제 16개 response rule을 조사해 distinct physical item `8`개를 확정했다.
  - reclaimed-water filter, dreamleaf analgesic, isolation care kit, antidote, blood pack, clean water, fungicide, pest lure
- [x] 기존 runtime consumer catalog에 빠져 있던 7개 link를 `runtime:disease-field-response`로 추가했다. 격리 키트 하나만 등록되어 있던 불완전 상태를 제거했다.
- [x] `V23CraftingDebugScenarios`가 질병 대응 owner의 exact 8-item ordinal set을 검사하도록 추가했다. 다른 item으로 count만 맞추거나 rule item이 누락되면 실패한다.
- [x] `OffenseSupplyCatalog`의 실제 physical mapping `11`개를 `runtime:offense-supply-package` owner로 전수 등록했다.
  - preserved ration, standard medicine, field emergency kit, five lineage medical kits, mana crystal, field repair kit
- [x] V23 focused gate가 원정 보급 owner의 exact 11-item ordinal set을 검사하도록 추가했다.
- [x] runtime consumer catalog 기대 링크를 `24→42`로 갱신하고 item/owner pair 중복 `0`, canonical `runtime:` owner를 요구한다.
- [x] packaging review source digest에 disease response runtime, offense supply catalog, offense preparation runtime을 포함했다.
- [x] current remaining `51` 중 runtime owner가 있는 packaging review 행/link 기대치를 `18/18`로 갱신했다. recipe consumer와 runtime owner를 별도 열로 보존한다.
- [x] current `Assembly-CSharp`와 current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`.
- [x] Unity에서 V23의 독립 typed runtime-consumer gate와 packaging review를 fresh 실행해 current catalog links `61`, review runtime rows/links `28/31`, Console Warning/Error `0/0`을 확인했다. 전체 `V23CraftingDebugScenarios.RunAll()`은 후단의 오래된 V23 WU·계약·시장 감사가 현재 권위와 불일치해 계속 fail-loud하며 최종 EWU·가격 재생성 전까지 PASS로 간주하지 않는다.
- [x] 질병 대응 `TryConsumeFacilityItemBuffer`를 exact facility-buffer lot 선택, typed pending Sink receipt, package tare 반환·폐기, health outcome outbox, receipt acknowledgement 순서로 전환했다. 원정 보급 `ConsumeDelivered`의 typed Transfer+package ownership은 계속 대기한다.
- [ ] 저장/복원·취소·재시도에서 질병 효과 또는 원정 package가 item debit 없이 게시되거나 debit 뒤 사라지지 않는지 PlayMode로 검증한다.

이번 단계는 누락을 숨기지 않도록 소비자 권위를 전수 등록한 것이다. `runtime:` link는 현재 untyped removal을 정당화하지 않으며, 질병 대응은 `Sink`, 원정 적재는 반환 가능한 `Transfer`로 서로 다른 disposition이 필요하다.

---

## 61. 구현 체크포인트: 질병 현장 대응 typed Sink·건강 결과 outbox

상태: **runtime/current-format/focused fixture·Roslyn 컴파일·Unity focused PASS / 실제 live 질병 대응·최종 질량 경제 대기**.

- [x] `DiseaseFieldResponseRuntime`에서 범용 `TryConsumeFacilityItemBuffer` 호출을 제거했다. `DiseaseFieldResponsePhysicalGateway`가 실제 `FacilityBuffer`, exact destination, exact item ID, reservation 0인 stable stack ID를 결정론적으로 골라 `PhysicalItemDispositionKind.Sink` pending receipt로만 제거한다.
- [x] 소비 작업 ID는 저장 권위의 단조 증가 `nextFieldResponseOperationSequence`와 canonical character/disease/response ID로 `disease-field-response:{character}:{disease}:{response}:{sequence:D8}`를 만든다. 프레임·시간·임의 GUID를 사용하지 않는다.
- [x] `PopulationHealthWorldSaveData` current schema를 `2`로 올리고 `IntentRecorded`/`OutcomePublished` 두 단계, exact source stack IDs, quantity, input gram, parent commit ID, facility cell을 저장한다. 과거 버전 마이그레이션은 추가하지 않았다.
- [x] 실행 순서를 `intent publish → physical Sink pending → packaged tare exact-once output/loss → detached health outcome publish → physical receipt acknowledge → pending clear/sequence advance`로 고정했다.
- [x] item commit 뒤 health publish 전에 중단되면 저장된 intent와 Physical pending receipt로 동일 치료를 복구한다. health publish 뒤 acknowledge 전에 중단되면 `OutcomePublished`가 두 번째 severity 감소를 막고 acknowledgement만 재시도한다.
- [x] item commit이 없는 `IntentRecorded`는 복원 시 치료·sequence 변경 없이 제거한다. pending operation과 다른 요청은 기존 pending을 먼저 회복한 후에만 새 sequence를 사용한다.
- [x] 포장된 치료품은 parent Sink commit과 facility center cell을 `IPackagedLotTareDispositionService`에 전달하고, 반환·폐기·명시 손실이 보장되기 전에는 health outcome을 게시하지 않는다. 따라서 물·키트·약품에 package feature가 추가되어도 tare 전체가 암묵 Sink되지 않는다.
- [x] `PopulationHealthAggregateState.Restore`가 current schema, sequence, phase, authored disease-response join, active intent target, sorted unique source IDs, exact commit ID를 fail-loud 검증한다.
- [x] aggregate reference preflight가 pending character, facility instance, disease definition, item definition을 실제 다른 save section/catalog와 교차 검증한다.
- [x] startup `DiseaseFieldResponseRecoveryAdapter`가 command 입력을 기다리지 않고 current pending operation을 회복한다.
- [x] focused fixture를 추가했다. `resource:clean-water` 2개×750g을 exact Sink하고 severity `50→36`, tare gateway parent commit/cell 1회, source 잔량 `3→1`, pending receipt `1→0`, sequence `1→2`, replay second debit/effect 0을 검사한다. receipt 없는 intent는 severity·sequence를 변경하지 않고 지운다.
- [x] `DungeonStory.Species`, `Assembly-CSharp`, 신규 focused Editor source를 포함한 `Assembly-CSharp-Editor` Roslyn compile exit `0`; `git diff --check` 오류 `0`.
- [x] Unity MCP에서 asset refresh/fresh compile 후 `DiseaseFieldResponseOutboxDebugScenarios.RunFromMenu()`를 실행하고 Console Warning/Error `0/0`을 확인했다.
- [ ] PlayMode에서 실제 의료 시설 buffer 배송→현장 대응→package tare 회수/폐기→whole-save restore를 실행해 live AI·시설 좌표·물류·UI 결과를 확인한다.

이 체크포인트는 질병 response 8종의 **물리 제거와 건강 결과 원자성**을 닫는다. 각 item의 최종 kg·개별 package BOM·효과 수치·EWU·가격을 승인한 것이 아니며, 남은 원정 보급품은 반환 가능한 추상 package 소유권 때문에 별도의 typed Transfer 수직 슬라이스로 처리한다.

---

## 62. 구현 체크포인트: 원정 보급품 typed Transfer custody·반환 Source

상태: **runtime/current-format/focused fixture·Roslyn 컴파일·Unity focused PASS / 실제 원정 live·최종 질량 경제 대기**.

- [x] 출정 시 집결지 `FacilityBuffer` 물자를 범용 `ConsumeDelivered`로 삭제하지 않는다. exact destination·item·reservation 0인 physical stack을 stable ID 순으로 선택해 `PhysicalItemDispositionKind.Transfer` pending receipt로 원정 package custody에 넘긴다.
- [x] custody 저장에는 operation/reason/commit, sorted unique source stack IDs, quantity, input gram, acknowledgement와 phase를 기록한다. commit ID는 `physical-batch-disposition:Transfer:operation:quantity:mass`와 exact 일치해야 한다.
- [x] domain package가 custody receipt를 영속한 뒤에만 physical pending receipt를 acknowledge하고 집결지 claim을 해제한다. acknowledgement 실패 시 package와 pending receipt를 보존해 같은 commit을 재시도한다.
- [x] 출정 후 남은 물자는 caller가 제공한 임의 loadout이 아니라 persisted custody의 owned costs 이하에서만 반환할 수 있다. unknown package와 package 없는 loadout은 stock을 생성하지 않는다.
- [x] 반환은 package가 `ReturnPublishing` intent·exact outputs·drop position을 먼저 저장한 뒤, deterministic output commit marker가 붙은 실제 `Loose` Source stack을 게시한다. replay는 기존 marker/quantity/mass를 검증하며 second output을 만들지 않는다.
- [x] 반환 질량과 소비·손실 질량의 합이 custody input mass와 exact 일치한다. 무반환 종료는 모든 custody mass를 consumed/lost로, 생존자 반환은 actual returned mass와 residual로 분리한다.
- [x] `Staging/CustodyOwned/ReturnPublishing/Returned/Lost`별 current-format provenance를 fail-loud 검증한다. terminal/returning phase는 custody acknowledgement가 필수이며, staging·owned·lost에 허용되지 않은 return 위치/commit/수량이 남으면 거부한다.
- [x] `OffenseWorldSaveData` schema는 custody v6 이후 mitigation WIP outbox까지 포함한 current `7`이다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] aggregate authored reference와 whole-save preflight가 원래 costs뿐 아니라 `returnedCosts` item definition도 교차 검증한다.
- [x] focused strategic fixture가 2개/2,000g Transfer, acknowledgement 1회, 3개 over-return 거절, 1개/1,000g Source 반환, residual 1,000g, replay second output 0, terminal restore 후 second output 0을 검사한다.
- [x] current `Assembly-CSharp`와 `Assembly-CSharp-Editor` Roslyn compile exit `0`; scoped `git diff --check` 오류 `0`.
- [x] Unity fresh import 후 `Tools/DungeonStory/Validation/Run Offense Strategic Scenarios`를 실행해 11개 focused 계약의 custody/return/save proof와 Console Warning/Error `0/0`을 확인했다.
- [ ] 실제 출정 PlayMode에서 집결 배송→custody Transfer→저장/복원→일부 소비→생존자 반환·전멸 loss를 실행해 physical quantity/mass·Floor Clutter·haul ownership을 확인한다.

이 체크포인트는 원정 보급품의 **world stack→package custody→반환 또는 명시 소비/손실** 소유권을 닫는다. 보급품 11종의 최종 kg·package BOM·가격·원정 ROI와 전투 소비율을 승인한 것이 아니며, Unity fresh execution 전에는 이 수직 슬라이스도 최종 PASS로 보고하지 않는다.

---

## 63. 구현 체크포인트: 긴급 거점 완화 재료 Transfer-to-WIP·결과 outbox

상태: **runtime/current-format/focused fault fixture·Roslyn 컴파일·Unity focused PASS / 실제 원정 live·최종 질량 경제 대기**.

- [x] 완화 완료 시 `IProductionItemGateway.ConsumeDelivered`와 `RemoveDestination`으로 재료를 범용 삭제하던 경로를 제거했다.
- [x] exact FacilityBuffer stack을 `ConsumeDeliveredToWip`의 pending `PhysicalItemDispositionKind.Transfer`로 완화 주문 WIP에 귀속한다. partially reserved stack은 고르지 않는다.
- [x] 주문 current-format에 `None/MaterialsCommitted/OutcomePublished` phase, deterministic operation/commit, input quantity/grams, acknowledgement, mitigation before/after를 저장한다.
- [x] 결과 게시 전에 authored urgent-site state를 다시 확인하고 `before`와 exact 일치할 때만 `after-before` delta를 적용한다. 이미 `after`이면 restore/retry로 보고 두 번째 완화를 적용하지 않는다. 두 값과 모두 다르면 conflict로 fail-loud한다.
- [x] 실행 순서를 `physical Transfer pending → domain receipt 저장 → mitigation outcome 게시/검증 → OutcomePublished 저장 → physical acknowledge → residual delivery 물리 Release → order 제거`로 고정했다.
- [x] acknowledgement 실패 시 결과가 게시된 주문과 pending receipt를 보존한다. current-format restore 후 acknowledgement와 cleanup만 재시도하며 재료를 다시 Transfer하거나 완화 수치를 다시 더하지 않는다.
- [x] physical commit 이후에는 플레이어 취소를 거부한다. 취소·시설 변경이 abstract WIP를 반환 없이 삭제하거나 다른 시설 destination으로 순간이동시키지 못한다.
- [x] aggregate validation이 phase별 빈 provenance, exact operation/commit, positive quantity/mass, completed work, before/after 범위와 acknowledgement 규칙을 검사한다.
- [x] authored validation이 input quantity와 urgent definition의 `mitigationItemAmount`를 조인하고, world urgent-site mitigation이 pending `before/after` 중 허용 상태인지 검사한다.
- [x] whole-save preflight가 unacknowledged order를 Physical pending batch operation/commit/quantity/mass와 exact 조인하고, acknowledged order에 stale pending receipt가 있으면 실패한다.
- [x] runtime consumer catalog에 실제 완화 재료 4종과 owner `runtime:offense-urgent-mitigation`을 추가했다. catalog exact link는 `46`, remaining packaging review는 같은 18행에 `medicine:standard`의 두 번째 owner가 추가되어 links `19`다.
- [x] focused strategic fixture가 acknowledgement fault를 주입해 WIP Transfer 1회, 결과 게시, `OutcomePublished` 저장, current-format restore, ack 1회, second Transfer/outcome 0을 검사한다.
- [x] `OffenseWorldSaveData` current schema는 mitigation outbox를 포함한 `7`이며 과거 save migration은 없다.
- [x] current `Assembly-CSharp`와 `Assembly-CSharp-Editor` Roslyn compile exit `0`.
- [x] Unity fresh import 후 Offense strategic scenarios, V23 typed runtime-owner exact set, packaging review double-capture와 Console Warning/Error `0/0`을 확인했다. 전체 V23 경제 감사의 현재 수치 드리프트는 별도 open gate로 유지한다.
- [ ] 실제 시설 배송·작업·whole-save restore·거점 시간 경과가 섞인 PlayMode에서 pending WIP가 고아가 되지 않는지 확인한다.

이 체크포인트는 긴급 완화의 **물리 재료와 월드 효과 원자성**을 닫는다. 완화 item의 최종 kg·BOM·WU·효과량·거점 ROI를 바꾸거나 승인하지 않았으며, 전수 untyped removal 0과 Unity live 검증은 계속 남아 있다.

---

## 64. 구현 체크포인트: 예방접종 typed Sink·면역 결과 outbox

상태: **runtime/current-format/focused fault fixture·Roslyn 컴파일·Unity focused PASS / 백신 질량 계약 대기**.

- [x] `PhysicalVaccinationRuntime`에서 범용 `TryConsumeFacilityItemBuffer`를 제거했다. 백신 1회분은 exact medical `FacilityBuffer` stack에서 `PhysicalItemDispositionKind.Sink` pending receipt로만 제거한다.
- [x] exact facility Sink lot 선택을 질병 전용 gateway에서 Items 계층의 `IPhysicalFacilityItemSinkGateway`로 승격했다. 질병·예방접종과 이후 농업/의료 소비자가 같은 reservation 0·stable stack order·pending receipt 규칙을 공유하며 농업이 Character/Disease 계층에 의존하지 않는다.
- [x] `PopulationHealthWorldSaveData` current schema를 `3`으로 올리고 field response와 분리된 `nextVaccinationOperationSequence`, `pendingVaccination`, `IntentRecorded/OutcomePublished` phase를 추가했다. 건강한 대상 예방접종을 활성 질병 치료 intent로 위장하지 않으며 과거 schema 마이그레이션은 추가하지 않았다.
- [x] operation ID는 `vaccination:{character}:{disease}:{sequence:D8}`, reason은 `vaccination-dose-administered`로 고정했다. 임의 GUID·프레임·현재 시각을 사용하지 않는다.
- [x] 실행 순서를 `vaccination intent publish → exact physical Sink pending → package tare return/waste/loss → detached immunity outcome publish → physical acknowledge → pending clear/sequence advance`로 고정했다.
- [x] acknowledgement 실패 시 `OutcomePublished`와 exact source IDs/input grams/commit을 저장한다. current-format restore 후에는 acknowledgement와 terminal clear만 재시도하며 두 번째 백신 Sink·tare output·면역 게시를 하지 않는다.
- [x] 물리 commit이 없는 `IntentRecorded`는 면역과 sequence를 바꾸지 않고 제거한다. startup `PhysicalVaccinationRecoveryAdapter`가 다음 플레이어 명령을 기다리지 않고 pending vaccination을 회복한다.
- [x] aggregate restore가 canonical disease/item/facility/character, vaccine 허용 질병, exact quantity `1`, operation/reason, sorted unique source stack IDs, positive input grams, exact Sink commit ID를 fail-loud 검증한다.
- [x] whole-save preflight가 field response와 vaccination 모두를 Physical pending batch의 kind/operation/reason/quantity와 교차 검증하고, outcome phase에 pending receipt가 남아 있으면 commit/input grams도 exact 조인한다.
- [x] 7종 백신을 `runtime:physical-vaccination` owner로 runtime consumer catalog에 등록하고 V23 focused gate가 exact ordinal set을 검사한다. catalog 기대 link는 `46→53`, remaining packaging review runtime rows/links는 `18/19→25/26`이다.
- [x] focused fault fixture를 추가했다. 2개×400g 중 1개 exact Sink, immunity `0→70`, package tare cell 1회, acknowledgement 1회 실패, `OutcomePublished` 저장, 새 runtime 복원, ack 성공, 잔량 `2→1`, pending receipt `1→0`, sequence `1→2`, terminal replay second debit/tare `0`을 검사한다. receipt 없는 intent도 별도로 검사한다.
- [x] `DungeonStory.Species`, current `Assembly-CSharp`, 신규 focused source를 포함한 `Assembly-CSharp-Editor` Roslyn compile exit `0`; scoped `git diff --check` 오류 `0`이다.
- [x] Unity fresh import 후 `PhysicalVaccinationOutboxDebugScenarios.RunFromMenu()`, V23 typed runtime-consumer exact set, packaging review double-capture를 실행하고 Console Warning/Error `0/0`을 확인했다. 전체 V23 경제 감사는 최종 재조정 전까지 별도 실패로 보존한다.
- [ ] 7종 백신의 최종 단위 의미·kg·재사용 바이알 BOM·내용물/공정 손실을 recipe transform과 함께 승인한다. 현재 authored 한 cycle은 항원 `900g`+고급 약품 `140g`+물 `500g`=`1,540g`을 백신 `400g×4=1,600g`으로 만들어 `60g` 질량을 생성하므로 이 상태를 semantic PASS로 승격하지 않는다.
- [ ] 실제 의료 시설 배송→접종→빈 바이알 반환/운반→whole-save restore PlayMode에서 수량·질량·면역·Floor Clutter·UI와 Console `0/0`을 확인한다.

이 체크포인트는 예방접종의 **물리 제거와 면역 결과 원자성**만 닫는다. 현재 백신 `400g`을 자연스러운 최종값으로 승인하지 않았고, 입력 질량·재사용 바이알·명시 공정 손실을 함께 맞추기 전에는 7종 백신 semantic과 전수 kg After를 완료 처리하지 않는다.

추가 조사에서 `IPhysicalCropTreatmentService`는 DI 등록과 정의만 있고 production 호출자가 검색되지 않았다. 따라서 작물 처리제 outbox를 먼저 작성해 runtime consumer가 존재하는 것처럼 보고하지 않는다. 실제 Planner/Runner/UI 명령 경로, plot/facility destination, 작업·취소·저장 권위를 연결한 뒤 physical Sink와 CropEcology outcome outbox를 같은 수직 슬라이스로 구현한다.

---

## 65. 구현 체크포인트: 캐릭터 치료 재료 exact Sink·치료 주문 outbox

상태: **runtime/current-format/focused fault fixture·exact consumer 감사·Roslyn 컴파일·Unity focused PASS / 개별 kg/package 계약 대기**.

- [x] `CharacterMedicalSupplyCoordinator`의 치료 완료 경로에서 범용 count 소비를 제거했다. 치료 재료는 실제 의료 주문의 `FacilityBuffer` destination, exact item ID, reservation `0`인 물리 stack만 `PhysicalItemDispositionKind.Sink` pending receipt로 제거한다.
- [x] 실제 `ResourceEconomyContentCatalog.Items`와 `supportsInjuryTreatment`를 교차해 live 치료 후보 exact `7`종을 확정했다.
  - `medical:regenerative-medium`
  - `medical:sterile-bandage`
  - `medicine:advanced`
  - `medicine:antiseptic`
  - `medicine:herbal-poultice`
  - `medicine:mycelial-culture-pack`
  - `medicine:standard`
- [x] 기존 `StockCategory.Biological` 배송·소비를 제거했다. 생물학 카테고리 `56`종 중 씨앗·장기·독소·폐기물 등을 임의 치료제로 고르지 않고, 포로 혈액 추출 production route가 생성하는 exact generic item `captivity:extracted-blood`만 저효율 fallback으로 요청·소비한다.
- [x] 추출 혈액 배송은 category request가 아니라 exact `TryRequestItemDelivery`를 사용한다. 이미 같은 주문에 배송 요청이 있으면 두 번째 request를 만들지 않으며, 도착 stack도 exact ID가 아니면 Sink하지 않는다.
- [x] 치료 주문 current-format schema를 `4`로 올리고 `None/IntentRecorded/SupplyPublished`, monotonic operation sequence, operation/reason, physical item/quantity, facility output cell, sorted source stack IDs, input grams, commit ID를 저장한다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] operation ID는 `character-medical-supply:{orderId}:{sequence:D8}`, reason은 `character-medical-treatment-supply`, quantity는 `1`로 고정했다. 프레임·시간·임의 GUID를 사용하지 않는다.
- [x] 실행 순서를 `supply intent publish → exact physical Sink pending → package tare return/waste/loss → treatment supply consumed publication → physical acknowledge → pending clear/sequence advance`로 고정했다.
- [x] acknowledgement 실패 시 `SupplyPublished`와 exact source IDs/input grams/commit을 저장한다. current-format restore와 startup recovery는 acknowledgement·terminal clear만 재시도하며 두 번째 Sink·tare output·치료 재료 소비 게시를 하지 않는다.
- [x] 물리 receipt가 없는 `IntentRecorded`는 sequence와 consumed 상태를 바꾸지 않고 intent만 지운다. pending receipt와 주문 provenance가 다르면 어느 쪽도 삭제하지 않고 fail-loud한다.
- [x] 물리 item validation을 Economy 전용 `IResourceEconomyContentCatalog`에서 전체 immutable `IItemDefinitionCatalog`로 분리했다. 따라서 generic `captivity:extracted-blood` intent는 정상 복원되지만 unknown physical ID는 거부된다. medicine ranking과 potency validation은 계속 Resource catalog가 소유한다.
- [x] whole-save preflight가 patient, facility, physical item과 pending Sink의 kind/operation/reason/quantity를 교차 검증하고, `SupplyPublished`에서는 commit/input grams까지 exact join한다.
- [x] 취소·환자 사망·시설 해제 시 pending 치료 재료 복구가 실패하면 destination/intent를 먼저 지우지 않는다. receipt acknowledgement 또는 합법적인 retained recovery가 끝난 뒤에만 주문의 물리 소유권을 종료한다.
- [x] runtime consumer catalog에 live medicine `7`종과 추출 혈액 `1`종을 owner `runtime:character-medical-treatment`로 등록했다. V23 gate가 exact ordinal `8`종을 검사하며 전체 runtime link 기대치는 `53→61`이다.
- [x] remaining packaging review `51`행 중 새 owner의 영향은 runtime consumer rows `25→28`, links `26→31`이다. 동일 item의 Offense owner와 character-medical owner를 별도 link로 보존하고 count만 맞는 identity 교체를 허용하지 않는다.
- [x] focused fixture가 medicine `2개×140g`에서 exact `1개/140g` Sink, package tare 1회, acknowledgement fault, `SupplyPublished` 저장 검증, 새 coordinator의 acknowledgement-only recovery, 잔량 `2→1`, sequence `1→2`, terminal replay second debit/tare `0`을 검사한다.
- [x] focused fixture가 추출 혈액 fallback이 `captivity:extracted-blood`만 exact 요청하고 같은 주문의 두 번째 delivery request를 만들지 않으며, generic item이 current-format physical intent validation을 통과하는지 검사한다.
- [x] live 후보 `7`종+추출 혈액과 runtime owner exact set을 정적으로 교차해 missing/extra `0`, CharacterMedical 범위의 `TryConsumeFacilityItemBuffer`와 Biological category request `0`을 확인했다. current `Assembly-CSharp`와 `Assembly-CSharp-Editor` Roslyn compile exit `0`이다.
- [x] Unity fresh import 후 `Dungeon Story/QA/V27/Character Medical Supply Outbox`, V23 typed runtime-consumer exact set, packaging review double-capture를 실행하고 Console Warning/Error `0/0`을 확인했다. 전체 V23 경제 감사는 최종 재조정 전까지 별도 실패로 보존한다.
- [ ] 실제 환자·의료 시설·AI 배송·작업·취소/사망·whole-save restore PlayMode에서 medicine과 추출 혈액 각각의 수량·질량·package tare·치료 결과·destination cleanup·Floor Clutter를 검증한다.
- [ ] `regenerative-medium`, `advanced`, `antiseptic`, `mycelial-culture-pack`, `standard`의 최종 단위 kg·용기/폐기 lifecycle과 생산 recipe 질량을 승인한다. `sterile-bandage`와 `herbal-poultice`의 integral-solid semantic은 앞선 체크포인트를 유지하며, 추출 혈액 `500g`도 혈액 채취량·용기 표현·치료 소비량을 전수 recipe/생존/의료 감사와 함께 재검토한다.

이 체크포인트는 **치료 재료의 물리 제거와 치료 주문 상태 원자성**을 닫는다. 기존 치료 potency·감염/통증 감소·추출 혈액 페널티, authored kg·BOM·WU·EWU·가격은 변경하지 않았다. Unity live 실행과 남은 package/recipe/전수 kg 승인이 없으므로 캐릭터 의료 또는 전체 중량 밸런스 완료로 보고하지 않는다.

---

## 66. 구현 체크포인트: 연령 치료 단일 수술 권위·시간 고정 유지보수 2-input Sink outbox

상태: **중복 direct 치료 권위 제거·runtime/current-format·focused fault fixture·실제 수술 whole-save PlayMode 완료 / 시간 고정 시설 fault PlayMode 대기**.

- [x] `IPhysicalAgeTreatmentService`의 production caller를 전수 검색해 `0`임을 확인했다. 전신 재생과 시간 고정 활성화는 이미 `AgeTreatmentCommandRuntime → SurgeryOrder → SurgeryLogistics → ApplyAgeTreatmentEffectHandler`가 재료 배송·작업·효과·저장 복원을 소유한다.
- [x] caller가 없는 `TryApplyWholeBodyRegeneration`, `TryActivateTemporalStasis`와 DI 노출을 제거했다. 같은 치료를 수술과 direct command가 각각 소비·게시할 수 있던 중복 권위를 남기지 않는다.
- [x] `PhysicalAgeTreatmentRuntime`의 실사용 책임을 이미 활성화된 시간 고정의 계절 유지보수로 축소했다. `IPhysicalAgeTreatmentService` 참조 `0`, 이 파일의 `TryConsumeFacilityItemBuffer` 호출 `0`을 정적 gate로 확인했다.
- [x] 유지보수 입력을 `component:rune-conductor ×1`과 `resource:mana-crystal ×1`의 exact ordinal vector로 고정했다. 임의 component/resource category 대체를 허용하지 않는다.
- [x] Items 계층에 `IPhysicalFacilityItemBatchSinkGateway`를 추가했다. 여러 exact item의 reservation 0 FacilityBuffer stack을 item ID·stack ID 안정 순서로 모두 선택한 뒤 하나의 `PhysicalItemDispositionKind.Sink` pending receipt로 원자 commit한다.
- [x] 두 번째 입력이 없거나 잘못되면 어느 source stack도 변경하지 않는다. 입력 수집 완료 전에는 pending receipt나 world debit을 만들지 않아 룬 도체만 먼저 사라지는 partial consumption을 막는다.
- [x] `CharacterLifeWorldSaveData` current schema를 `3`으로 올리고 monotonic maintenance sequence, `None/IntentRecorded/OutcomePublished`, exact character/facility/item vector, before/after maintenance day, source stack IDs, total quantity/grams, commit ID를 저장한다. 과거 save migration은 추가하지 않았다.
- [x] operation ID는 `temporal-stasis-maintenance:{characterId}:{sequence:D8}`, reason은 `temporal-stasis-seasonal-maintenance`로 고정했다. 현재 시간·프레임·임의 GUID를 사용하지 않는다.
- [x] 실행 순서를 `maintenance intent publish → exact two-input Sink pending → detached life outcome publish → physical acknowledge → pending clear/sequence advance`로 고정했다.
- [x] outcome publication은 저장 DTO의 같은 character/facility와 exact before maintenance day를 재검증한 뒤 `effectiveAgingCareMode=TemporalStasis`, `nextMaintenanceAfterAbsoluteDay`를 한 aggregate publication으로 반영한다.
- [x] acknowledgement 실패 시 이미 게시된 다음 유지일과 exact receipt provenance를 보존한다. startup/current-day recovery는 acknowledgement와 terminal clear만 재시도하며 두 재료를 다시 Sink하거나 유지일을 두 번 연장하지 않는다.
- [x] physical receipt가 없는 `IntentRecorded`는 sequence와 유지일을 바꾸지 않고 제거한다. contract/receipt/life-state mismatch는 pending을 지우지 않고 `TemporalStasisMaintenanceUnavailable`로 fail-loud한다.
- [x] character-life restore validation이 phase·sequence·canonical IDs·item quantities·before/after day·sorted unique stack IDs·total quantity/grams·commit과 현재 stasis owner state를 검증한다.
- [x] whole-save preflight가 pending character/facility/item 정의와 Physical pending Sink의 operation/reason/quantity를 조인한다. `OutcomePublished`에서는 commit/input grams/source stack IDs도 exact 비교한다.
- [x] focused fixture가 서로 다른 unit mass의 두 stack을 한 receipt의 `quantity=2`, `inputMass=1,200g`으로 소비하고, acknowledgement fault 뒤 `OutcomePublished`, 새 runtime recovery, sequence `1→2`, 두 stack 잔량 `2→1`, second debit `0`을 검사한다.
- [x] focused fixture가 마나 수정 누락 시 룬 도체 잔량 유지·pending receipt `0`, receipt 없는 intent의 유지일/sequence 변화 `0`을 검사한다.
- [x] `DungeonStory.Species`, current `Assembly-CSharp`, 신규 focused source를 포함한 `Assembly-CSharp-Editor` Roslyn compile exit `0`; scoped `git diff --check` 오류 `0`이다.
- [x] Unity fresh import 후 `TemporalStasisMaintenanceOutboxDebugScenarios`, `SurgeryDebugScenarios.RunAll(false)`와 실제 `SurgeryPlayModeVerifier`를 실행했다. 수술 AI가 물리 약품·공정수를 배송/소비하고 Procedure 중간 current-format whole-save를 복원해 동일 work/state, transient doctor owner 해제, AI exact-once 재개, 완료·빈 바이알 반환/재생산까지 PASS했으며 보고서 `Artifacts/QA/surgery-playmode-report.txt`의 `RESULT=PASS`, Console Warning/Error `0/0`을 확인했다.
- [ ] 실제 시간 고정 시설에서 계절 경계·전력 상실/복구·한 재료 부족·저장/복원을 조합해 physical quantity/mass, 다음 유지일, 효과 상태, haul destination과 Floor Clutter를 PlayMode로 검증한다.

이 체크포인트는 연령 치료의 **단일 수술 실행 권위**와 시간 고정 유지보수의 **두 물리 입력·효과 결과 원자성**을 닫는다. 룬 도체·마나 수정의 authored kg·BOM·WU·EWU·가격·계절 유지비는 변경하거나 최종 승인하지 않았다. 전수 untyped facility consume은 현재 `36`개가 남아 있고, Unity live 증거와 전수 recipe/kg 재산정 전에는 연령 치료나 전체 중량 밸런스 완료로 보고하지 않는다.

---

## 67. 구현 체크포인트: 발전기 연료 exact Sink·가동 시간 outbox

상태: **runtime/current-format/whole-save preflight·Unity focused fault/restore fixture 완료 / 연료 전수 수치 승인 대기**.

- [x] 연료 필요 발전기가 `TryConsumeFacilityItemBuffer`의 count-only 삭제를 사용하지 않도록 제거했다. `IPhysicalFacilityItemSinkGateway`가 exact `power:{nodeId}` FacilityBuffer, authored fuel item ID, quantity 1을 결정론적으로 선택해 `PhysicalItemDispositionKind.Sink` pending receipt로 연소시킨다.
- [x] 전력 save schema를 current version `3`으로 올리고 power node마다 `nextFuelOperationSequence`와 `None/IntentRecorded/OutcomePublished` fuel commit을 저장한다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] operation ID를 `power-fuel:{nodeId}:{sequence:D8}`, reason을 `power-generator-fuel-combustion`으로 고정했다. 프레임·실시간·임의 GUID를 사용하지 않는다.
- [x] 실행 순서를 `intent publish → physical Sink pending → FuelSeconds outcome publish → physical receipt acknowledge → pending clear/sequence advance`로 고정했다. Sink 실패는 intent를 sequence advance 없이 제거한다.
- [x] acknowledgement 실패 뒤에도 저장된 `OutcomePublished`와 실제 잔여 `FuelSeconds`를 사용한다. 복구 시 연료를 다시 소비하지 않고 acknowledgement만 재시도하며, 실패 기간에도 가동 시간은 매 simulation delta만큼 계속 감소해 공짜 한 틱을 만들지 않는다.
- [x] 새 연료 commit은 이전 acknowledgement가 끝나기 전에는 시작하지 않는다. 이전 연료 시간이 소진돼도 pending receipt를 버리거나 같은 sequence에서 두 번째 fuel item을 debit하지 않는다.
- [x] current-format validation이 canonical node/destination/item/reason, exact sequence, positive quantity 1, before/after time envelope, ordinal-sorted unique source stack IDs, positive input grams와 phase별 빈 provenance를 fail-loud 검증한다.
- [x] whole-save reference preflight가 pending node의 building instance와 fuel item definition을 조인하고, Physical pending Sink의 operation/reason/quantity 및 outcome commit/input grams/source stack IDs를 exact 비교한다.
- [x] Industrial save JSON fixture가 `OutcomePublished`, source stack, `2,000g`, Sink kind `3` commit을 current schema로 왕복한다. 테스트 문자열의 disposition kind를 enum 권위에서 생성해 Transform/Sink 혼동을 막았다.
- [x] `ElectricalNetworkRuntime`의 untyped facility consume은 `0`이며 current `Assembly-CSharp`와 `Assembly-CSharp-Editor` Roslyn compile exit `0`이다.
- [x] Unity fresh import 후 production `ElectricalNetworkRuntime`과 실제 fuelled `BuildableObject` topology를 사용하는 focused fixture가 fuel item `1`회 debit, `FuelSeconds` `120→110초` 감소, acknowledgement fault/current-format restore의 acknowledgement-only recovery와 second debit `0`을 확인했다. `IndustrialInfrastructureDebugScenarios.RunFuelOutboxTransactionFocused()`와 전체 `RunAll()`이 PASS했고 Console Warning/Error는 `0/0`이다.
- [ ] 저급 연료·석탄·기타 실제 발전기 fuel item 전수를 찾아 단위 gram, 묶음 운반량, 연소 부산물/재·명시 질량손실, `secondsPerFuel`, BOM·WU·EWU·가격을 승인하고 6인 전력·물류 폐쇄 루프를 재생성한다.

이 체크포인트는 발전기에서 사라지는 **한 연료 lot과 가동 시간 결과의 저장 원자성**만 닫는다. 연소는 terminal Sink이므로 package tare 반환 대상이 아니지만, 재·연기·폐열을 gameplay 물리 부산물로 채택할 경우 해당 output/loss mass 계약을 별도 Transform으로 추가해야 한다. authored kg·발전량·가동 시간·연료 경제와 전체 중량 밸런스는 아직 완료가 아니다.

---

## 68. 구현 체크포인트: 장비 모듈 감정 쿠폰 Sink·모듈/공구 결과 outbox

상태: **runtime/current-format/physical-save join·Unity focused fault/restore 검증 완료 / 감정 경제 수치 승인 대기**.

- [x] 모듈 감정에서 `TryConsumeFacilityItemBuffer`의 count-only 쿠폰 삭제를 제거했다. `component:material-test-coupon`은 exact appraisal `FacilityBuffer`, reservation `0`, stable stack ID 순서로 한 개만 선택되어 `PhysicalItemDispositionKind.Sink` pending receipt로 제거된다.
- [x] 독립 모듈 item-state schema를 current `2`로 올리고 monotonic `nextAppraisalOperationSequence`, `None/IntentRecorded/OutcomePublished`, operation/reason/destination, exact coupon stack/item/quantity, 모듈 before/after, 두 공구 stack/item/durability before/after, source IDs/input grams/commit을 저장한다. 과거 schema migration은 추가하지 않았다.
- [x] operation ID는 `equipment-module-appraisal:{moduleInstanceId}:{sequence:D8}`, reason은 `equipment-module-material-test`로 고정했다. 프레임·시간·임의 GUID를 사용하지 않는다.
- [x] 실행 순서를 `module intent persist → exact coupon Sink pending → module identified/state outcome → inspection gauge wear → rune lens wear → OutcomePublished provenance persist → physical acknowledge → pending clear/sequence advance`로 고정했다.
- [x] 모듈과 공구 결과는 각각 exact before/after envelope만 허용한다. current 값이 before면 after를 한 번 게시하고, 이미 after면 replay로 인정하며, 어느 쪽도 아니면 conflict로 fail-loud한다.
- [x] acknowledgement 실패 시 identified module, 두 공구 wear와 pending receipt를 보존한다. current-format restore/재명령은 acknowledgement와 clear만 완료하며 두 번째 쿠폰 Sink·두 번째 식별·두 번째 공구 마모를 만들지 않는다.
- [x] physical receipt가 없는 `IntentRecorded`는 결과·sequence 변경 없이 제거한다. acknowledgement 뒤 module clear 전에 캡처된 receipt 없는 `OutcomePublished`는 exact after envelope를 확인한 뒤 clear/sequence advance만 수행한다.
- [x] 미완료 appraisal outbox가 장착 장비 payload 안으로 이동해 독립 physical owner를 잃지 않도록 설치 명령과 equipment codec 양쪽에서 차단했다. attached module은 `Installed`, source stack 없음, exact host ownership, 유효하고 비어 있는 appraisal state만 허용한다.
- [x] physical save validation이 독립 module owner와 pending Sink의 kind/reason/quantity/coupon source를 조인한다. `OutcomePublished`는 input grams/commit/source IDs까지 exact 비교하고, `equipment-module-appraisal:` receipt의 owner가 없으면 restore를 거부한다.
- [x] runtime consumer catalog의 쿠폰·inspection gauge·rune lens 3개 exact item/owner pair를 V23 focused gate가 고정한다. 감정 범위의 untyped facility consume과 폐기된 direct tool-wear helper는 `0`이다.
- [x] focused fixture source가 정상 commit에서 쿠폰 1개, gauge `-1`, lens `-2`, sequence `1→2`, cleared outbox를 검사한다. fault fixture는 acknowledgement 1회 실패, `OutcomePublished` save, 변조된 owner/receipt restore 거부, 정상 restore ack `1회`, second debit/wear `0`, terminal replay 거부를 검사한다.
- [x] `DungeonStory.Combat`, current `Assembly-CSharp`, current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`; scoped `git diff --check` 오류 `0`이다.
- [x] Unity fresh import 후 `PhysicalItemDebugScenarios.RunAll()`과 `V23CraftingDebugScenarios.RunRuntimeConsumerContractsFromMenu()`를 실행했다. 정상 감정과 acknowledgement fault/current-format restore, owner/receipt 변조 거부, second coupon debit/tool wear `0`, typed runtime consumer 3개 연결이 PASS했고 Console Warning/Error는 `0/0`이다.
- [ ] material-test coupon의 최종 단위 gram·제작 BOM/WU, 파괴 검사 잔해 또는 명시 질량손실, 두 공구 durability 단위당 감정 횟수, 감정 결과 가치·EWU·가격을 승인하고 물류 묶음과 경제 순환을 재생성한다.

이 체크포인트는 감정에서 사라지는 **한 물리 쿠폰과 모듈/두 공구 결과의 저장 원자성**만 닫는다. 쿠폰을 terminal 파괴 검사로 보는 현재 Sink가 재료 시험 잔해를 gameplay item으로 만들지 않는다는 뜻은 아니다. 잔해를 채택하면 Sink가 아니라 explicit Transform/output-loss 계약으로 승격해야 하며, 최종 kg·BOM·WU·EWU·가격과 전체 중량 밸런스는 아직 완료가 아니다.

---

## 69. 구현 체크포인트: 지역 공급 계약 exact export Transfer·골드 결과 outbox

상태: **runtime/current-format/focused acknowledgement-fault fixture·Roslyn 컴파일 완료 / Unity fresh 실행·전수 계약 경제 재검증 대기**.

- [x] `RegionalSupplyContractApplicationAdapter`의 `TryConsumeFacilityItemBuffer` count 삭제를 제거했다. 계약 집결지의 exact `FacilityBuffer` lot은 외부 교역권으로 소유권이 이동하므로 `Sink`가 아니라 `PhysicalItemDispositionKind.Transfer`로 커밋한다.
- [x] 공용 facility lot selector를 Sink 전용으로 복제하지 않고 `IPhysicalFacilityItemBatchTransferGateway`까지 확장했다. exact destination·item ID·reservation 0·stable stack ID 순서와 batch all-or-nothing 규칙을 공유한다.
- [x] Economy assembly가 Assembly-CSharp 서비스 타입을 역참조하지 않도록 `RegionalSupplyDeliveryTransferReceipt` domain DTO를 두고 application adapter에서 physical receipt를 투영한다. Item/Economy 의존 방향을 뒤집지 않는다.
- [x] 계약 current-format schema를 `1→2`로 올리고 `None/PhysicalCommitted/RewardPublished`, operation/reason/commit, sorted unique source stack IDs, total quantity/input grams를 저장한다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] operation ID는 `regional-supply-transfer:{contractId}`, reason은 `regional-supply-export`로 고정했다. 현재 시각·프레임·임의 GUID를 사용하지 않는다.
- [x] 실행 순서를 `exact physical Transfer pending → contract provenance 저장 → gold outcome 게시·검증 → RewardPublished/Completed 저장 → physical acknowledge → pending clear`로 고정했다.
- [x] acknowledgement 실패 뒤 계약은 `Completed+RewardPublished`와 pending receipt를 보존한다. 다음 Tick/restore recovery는 acknowledgement만 재시도하고 두 번째 물품 Transfer나 골드 지급을 하지 않는다.
- [x] physical commit 뒤 골드 계정이 정확히 `rewardGold`만큼 증가하지 않으면 `PhysicalCommitted`를 유지하고 fail-loud한다. reward 상태가 게시되기 전에는 receipt를 acknowledge하지 않는다.
- [x] deadline·day-start 검사는 pending outbox를 계약 실패로 바꾸기 전에 먼저 회복한다. 이미 외부로 이전한 납품품을 기한 초과 처리하며 destination release하거나 계약 history trim으로 owner를 삭제하지 않는다.
- [x] save validation이 phase별 empty/pending provenance, exact operation ID, Transfer commit kind token, quantity/mass, ordinal unique source IDs와 허용 status를 검사한다. legacy DTO version과 변조 provenance를 거부한다.
- [x] runtime authority validator가 지역 계약 adapter의 `TryConsumeFacilityItemBuffer` 재도입을 금지하고 runtime/outbox 및 save-validator 조인을 요구한다.
- [x] focused fixture가 목재 3개 중 exact 2개를 Transfer하고 acknowledgement 1회 실패, 골드 publication 1회, `RewardPublished` JSON round-trip, input mass 변조 거부, acknowledgement-only recovery, source 잔량 1, pending receipt `1→0`, second income/Transfer `0`을 검사한다. 이 fixture는 Physical Item Contracts 집합에도 등록했다.
- [x] `DungeonStory.Economy`, current `Assembly-CSharp`, current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`; 지역 계약 production adapter의 untyped consume 호출은 `0`이다.
- [x] Unity current assembly에서 `PhysicalItemDebugScenarios.RunAll()`, `RegionalSupplyContractTransferOutboxDebugScenarios.Verify()`와 `ProductionEconomyDebugScenarios.RunAll()`을 연속 실행했다. exact 2-item Transfer, reward 1회, acknowledgement fault·JSON restore·acknowledgement-only recovery, source 잔량 1, pending `1→0`, second income/Transfer `0`이 PASS했고 직후 Console Warning/Error `0/0`이다.
- [ ] 인구·기술 단계별 계약 요구량, 실제 아이템 kg에 따른 운반 횟수, 45 effective WU 일정, 판매가·보상·7일 생존 비축 침해를 전수 재생성하고 일반 계약이 성장 노동 35%·물류 20% 상한을 깨지 않는지 다중 seed로 검증한다.

이 체크포인트는 지역 계약의 **물리 납품품 외부 이전과 골드 결과 원자성**만 닫는다. 기존 `rewardGold`, 계약 기간·수량·가격, 아이템 최종 kg를 변경하거나 승인하지 않았으며, 최종 질량·EWU·가격·6인 생존망과 Unity live evidence가 없으므로 계약 또는 전체 밸런스 완료로 보고하지 않는다.

---

## 70. 구현 체크포인트: 지역 공급 계약 incoming Physical restore 양방향 조인

상태: **detached candidate query·양방향 owner/receipt 조인·transaction 수명·pending regional delivery whole-save PlayMode PASS**.

- [x] 전체 저장 파이프라인을 재감사해 모든 section이 먼저 preflight되고 topological order로 staging된 뒤에만 commit된다는 사실을 확인했다. 지역 계약 staging에서 live physical gateway를 읽으면 복원 전 world를 보게 되므로 incoming save 교차 검증 권위로 사용할 수 없음을 명시했다.
- [x] `PhysicalItemRestoreCandidateDispositionSnapshot`과 `IPhysicalItemRestoreCandidateQuery`를 dependency-light 독립 source로 추가했다. incoming physical pending receipt의 kind/operation/reason/fingerprint/source IDs/quantity/input grams/commit을 immutable clone으로만 노출하며 save DTO나 live repository를 쓰기 권위로 노출하지 않는다.
- [x] `WorldItemStackRuntime.StageTransactionalRestore`가 완전히 검증된 detached `WorldItemRepositoryState.PendingBatchDispositions`를 candidate query로 게시하도록 연결했다. 같은 runtime singleton을 query로 등록해 별도 저장 권위나 live-state fallback을 만들지 않았다.
- [x] Physical stage를 `IDungeonDiscardableSaveRestoreStage`로 승격하고 `WorldItemStackRuntime`을 마지막 restore transaction participant로 등록했다. 뒤 section staging 실패 시 stage discard에서 candidate view를 지우지만, commit 뒤에는 교차-section participant publication이 모두 끝날 때까지 유지하고 transaction complete/rollback/discard에서만 지운다. 이로써 commit과 participant publish 사이의 candidate 조기 소멸을 막으면서 transaction 밖 누수는 `0`으로 유지한다.
- [x] RegionalSupply ordinary preflight는 DTO-local validation만 수행하고, ordered `BuildRestoreCandidate`에서 incoming physical candidate를 요구하도록 분리했다. 따라서 단독 payload validation과 cross-section staging의 책임이 섞이지 않는다.
- [x] 양방향 조인을 적용했다. pending contract는 exact incoming `Transfer` kind, `regional-supply-export` reason, operation/commit/source ID vector/quantity/grams가 일치해야 하며, 모든 `regional-supply-transfer:*` incoming receipt도 exact contract owner가 있어야 한다. missing·mismatch·orphan은 어느 live aggregate도 publish하기 전에 fail-loud한다.
- [x] focused source가 valid join, missing receipt, orphan receipt, mass provenance mismatch를 검사한다. 실제 `WorldItemStackRuntime`의 stage→query visible, stage discard→unavailable, second stage→commit 후 still-visible, participant publish/complete→unavailable+exact live pending receipt 수명을 함께 검사한다.
- [x] Runtime authority validator가 query/transaction participant DI 등록, discardable stage, complete/rollback/discard clear, 양방향 join과 세 실패 fixture를 고정한다. production bills·grand project·Circus·Surgery·RunMilestones의 DTO-local preflight와 candidate-dependent stage를 분리했고, crop/defense/combat material guard는 rollback 뒤 active flag를 남기지 않는다. current Unity compile과 관련 focused suites PASS, scoped `git diff --check` error `0`이다.
- [x] Unity fresh import 뒤 Physical Item Contracts·지역 공급 focused fixture·Production Economy·Dungeon Save Section 회귀가 PASS했다. 실제 `DungeonSaveSectionRegistry`가 Physical Items와 Regional Supply에 동일 aggregate-root store를 사용한 상태로 pending regional owner/receipt를 캡처·복원했고, valid 복원은 pending 소유권과 source 잔량 `1`을 보존했다. missing receipt·owner mass mismatch·orphan incoming receipt는 live aggregate publication 전에 원자 거부됐으며 각 실패와 정상 완료 뒤 candidate index 누수는 `0`이다. 동일 행렬을 `EditorApplication.isPlaying=true`인 PlayMode에서 재실행해 `PLAYMODE=1` PASS, 직후 Console Warning/Error `0/0`을 확인했다.

이 체크포인트는 **들어오는 저장 후보에서 계약 outbox와 물리 Transfer가 함께 존재한다는 복원 원자성**을 닫는다. authored kg·계약 요구량·골드 보상·운반 횟수·WU·EWU·가격은 변경하지 않았으며, 앞 절의 수치 재생성 및 Unity live gate는 그대로 미완료다.

---

## 71. 구현 체크포인트: 자원 재고 정책 일반 판매 exact Transfer·골드 outbox

상태: **runtime/current-format/incoming physical 양방향 조인/focused acknowledgement-fault fixture·Roslyn 컴파일 완료 / Unity fresh 실행·판매 경제 재검증 대기**.

- [x] `ResourceStockPolicyRuntime`의 일반 자원 판매 경로를 전수 확인했다. 기존 경로는 `stock-policy:sell:{itemId}` FacilityBuffer 수량을 먼저 `TryConsumeFacilityItemBuffer`로 삭제한 뒤 골드를 지급해, 두 단계 사이 실패에서 물품 손실·재시도 중복 지급을 증명할 수 없었다.
- [x] 정책 설정과 복구 상태를 분리했다. `SetPolicy`가 교체하는 `ResourceStockPolicyData`와 별개로 aggregate가 item ID별 pending sale 한 건과 global monotonic `NextSaleSequence`를 소유하므로 UI 정책 변경이 이미 반출된 물품의 outbox를 지우지 않는다.
- [x] stock-policy current-format schema를 `1→2`로 올리고 `nextSaleSequence`, canonical item-order `pendingSales`, `PhysicalCommitted/IncomePublished`, exact destination/operation/reason/commit/source IDs/quantity/input grams/proceeds를 저장한다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] 일반 판매품을 terminal Sink로 삭제하지 않고 외부 시장 소유권으로 `PhysicalItemDispositionKind.Transfer`한다. 공용 gateway가 exact destination·item ID·reservation 0·stable stack ID 순서로 lot을 선택하며 partial source mutation 없이 하나의 pending receipt를 만든다.
- [x] operation ID는 `stock-policy-sale:{sequence:D8}:{itemId}`, reason은 `stock-policy-market-export`, destination은 `stock-policy:sell:{itemId}`로 고정했다. 프레임·시간·임의 GUID를 사용하지 않으며 sequence 소진은 physical commit 전에 fail-loud한다.
- [x] 실행 순서를 `exact physical Transfer pending → aggregate pending sale 저장/sequence advance → 골드 잔액 exact 증가 검증 → IncomePublished → physical acknowledge → pending owner 제거`로 고정했다. acknowledgement 실패·restore에서는 acknowledgement만 재시도하고 두 번째 Transfer·골드 지급을 하지 않는다.
- [x] save preflight는 DTO-local 정책·outbox canonical validation만 수행하고 ordered staging에서 incoming physical candidate를 요구한다. 모든 pending sale→receipt와 모든 `stock-policy-sale:*` receipt→sale owner를 kind/reason/operation/commit/source IDs/quantity/grams까지 양방향 exact 조인하며 missing·mismatch·orphan을 publish 전 거부한다.
- [x] focused fixture가 목재 3개 중 2개 exact Transfer, pending sale 생성, valid/missing/orphan/mass-mismatch incoming join, acknowledgement 1회 실패, 골드 publication 1회, `IncomePublished` JSON round-trip, acknowledgement-only recovery, source 잔량 1, pending receipt `1→0`, second income/Transfer `0`을 검사한다. Physical Item Contracts 집합에도 등록했다.
- [x] runtime authority validator가 outbox recovery, schema provenance, incoming 양방향 조인, missing/orphan fixture와 `ResourceStockPolicyRuntime`의 count-only consume 재도입을 금지한다. `DungeonStory.Economy`, current-source `Assembly-CSharp`, current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`; scoped diff error `0`, 신규 meta GUID 중복 `0`, production stock-policy untyped consume `0`이다.
- [x] Unity current assembly에서 `PhysicalItemDebugScenarios.RunAll()`, `ResourceStockPolicySaleOutboxDebugScenarios.Verify()`와 `ProductionEconomyDebugScenarios.RunAll()`을 연속 실행했다. exact 2-item Transfer, valid/missing/orphan/mass-mismatch incoming join, acknowledgement fault·JSON restore·acknowledgement-only recovery, source 잔량 1, pending `1→0`, second income/Transfer `0`이 PASS했고 직후 Console Warning/Error `0/0`이다.
- [ ] 실제 AI 운반→판매 집결→정산 PlayMode에서 예약·부분 stack·정책 중도 변경·저장/복원·창고 kg admission·Floor Clutter를 검증하고, 최종 item kg/시장 회수율/운반 횟수/45 effective WU/6인 7일 비축을 반영해 stock threshold와 판매 ROI를 재생성한다. 품질 미달 unique 장비·의복 판매는 별도 physical ownership 수직 슬라이스로 남긴다.

이 체크포인트는 **일반 Resource item 자동 판매의 물리 반출과 골드 결과 원자성**을 닫는다. authored item kg·가격·sale rate·minimum/target/maximum stock을 변경하거나 승인하지 않았고, unique 장비·의복 판매, Unity live evidence와 최종 질량·EWU·가격·6인 생존망 검증은 아직 미완료다.

---

## 72. 구현 체크포인트: callerless 작물 처리제 직접 소비 권위 제거

상태: **죽은 production mutation·DI 제거, UI/AI planner·exact delivery·Treat WU·Sink/tare·생태 결과·V7 restore outbox·관찰 UI 수직 슬라이스와 Unity focused/PlayMode 검증 완료 / 최종 처리제 질량 경제·다중 seed 대기**.

- [x] `IPhysicalCropTreatmentService`와 `PhysicalCropTreatmentRuntime.TryApply`의 전수 역호출을 조사했다. 선언·구현과 DI 등록 외 production/UI/AI/work runner 호출자는 `0`이며, 실제 plot 작업이나 플레이어 명령에서 도달할 수 없는 죽은 API였다.
- [x] callerless 메서드의 `TryConsumeFacilityItemBuffer`를 typed Sink로 감싸 구현된 것처럼 남기지 않았다. 추적 중이던 runtime source/meta와 DI 등록만 제거했으며 authored `CropTreatmentItemFeature`, 세 처리제 정의, 생산 recipe·연구·표시 문자열·현재 수치는 변경하지 않았다.
- [x] runtime authority validator가 삭제된 source 재등장과 `IPhysicalCropTreatmentService` DI 등록을 금지한다. content catalog의 `ProductionConsumerKind.CropTreatment`는 authored 의도/수요 분류일 뿐 live 실행 경로 증거로 세지 않는다.
- [x] current-source `Assembly-CSharp`와 `Assembly-CSharp-Editor` Roslyn compile exit `0`, scoped diff error `0`, 삭제 GUID 외부 참조 `0`이다. repository `TryConsumeFacilityItemBuffer` text occurrence는 `33→32`이며 실제 crop-treatment production 호출은 `0`이다.
- [x] 작물 재배지 UI/AI planner → exact treatment delivery destination → 작업/WU runner → physical Sink+package tare → pest/disease ecology before/after outcome → acknowledgement/restore outbox → 관찰 UI까지 한 수직 슬라이스로 다시 구현했다. `CropPhysicalTransactionFixture`를 Unity에서 반복 실행해 exact Sink, tare replay, 생태 before/after, acknowledgement fault, missing/orphan/kind/fingerprint/mass 변조 거부와 파괴 terminal loss를 통과했다.
- [ ] pest lure·botanical pesticide·fungicide의 1개 단위 gram, 용기·잔류물·폐기 lifecycle, 처리 면적·효과량·재사용 간격, BOM/WU/EWU/가격, 6인 농업 노동·저장·운반과 오염 위험을 승인하고 다중 seed PlayMode를 수행한다.

처리 주문은 `crop-treatment:{plotId}:{sequence:D8}`와 plot별 `:treatment` destination을 사용한다. P23·P24·RF13·RF54만 기존 `Treat` 작업 bit를 노출하며, 동일 `WorkTypeId`의 단일 handler 권위를 유지하기 위해 `SurvivalWorkExecutionHandler`가 crop plot target만 `ICropPlotRuntime`으로 위임한다. provisional 구조값은 pest lure `1개/3 WU/-15 pest/1일`, botanical pesticide `1개/5 WU/-35 pest/2일`, fungicide `1개/5 WU/-25 disease/1일`이고 최종 밸런스 승인값은 아니다.

Unity PlayMode 저장 왕복에서 `JsonUtility`가 null `SeedLotState`를 빈 객체로 직렬화하는 결함이 발견됐다. Crop Plot current-format V7에 `hasSeedLot` discriminator를 추가해 완료된 empty owner와 실제 파종 WIP를 분리했다. 수정 뒤 `Verify Crop Physical Transactions`와 `Verify Crop Plot Runtime`이 통과했으며 보고서는 `valid=true`, outdoor harvest `0→6`, indoor phase `Growing`, output containment work/quantity conservation true, Console Warning/Error `0/0`이다.

## 73. 구현 체크포인트: 서커스 공연 소품 Sink·연회 수레 마모 outbox

- [x] `CircusRuntime.TryCommitShowSupplies`의 범용 `TryConsumeFacilityItemBuffer` 호출을 제거하고 공연 소품 상자 exact stack `1`개를 `PhysicalItemDispositionKind.Sink`로 커밋한다.
- [x] operation ID를 `circus-show-supplies:{orderId}:{sequence:D8}`, reason을 `circus-performance-prop-consumed`로 고정하고 source stack·quantity·input grams·physical commit ID를 주문 V4 저장 권위에 보존한다.
- [x] 연회 운반 수레의 내구도 before/after를 같은 pending 결과에 묶고, 현재값이 before이면 한 번 적용하고 after이면 replay로 인정하며 제3의 값은 conflict로 거부한다.
- [x] 물리 debit 뒤 acknowledgement가 실패해도 `OutcomesPublished`와 terminal commit을 보존하고, 재시도는 acknowledgement만 수행하도록 phase를 분리한다.
- [x] `preparationSuppliesCommitted`와 `preparationSupplyCommitId`를 영속하여 active 주문이 복원 시 Composition으로 재개되어도 소품·수레 결과를 두 번 적용하지 않는다.
- [x] Circus save schema를 V4로 올리고 empty/pending/terminal supply state의 sequence·receipt·mass·cart durability 일관성을 fail-loud 검증한다.
- [x] current-source `DungeonStory.Captivity`와 `Assembly-CSharp` 정적 Roslyn 컴파일을 통과한다.
- [x] 물리 disposition enum·immutable restore projection/query를 `DungeonStory.Items` 공용 권위로 이동하고 `DungeonStory.Infrastructure`가 기본 어셈블리를 역참조하지 않도록 의존 방향을 고정한다.
- [x] Circus save section의 incoming physical receipt 양방향 조인과 missing/mismatch/orphan 원자 거부 focused fixture를 추가하고 Items·Infrastructure·Runtime·Editor 정적 컴파일을 통과한다.
- [ ] Unity fresh import 후 실제 무대 FacilityBuffer 배송→준비 완료→ack fault/save restore→공연 진입을 실행해 두 번째 Sink/마모 `0`, Console Warning/Error `0/0`을 확인한다. 현재 MCP는 `Connection revoked`다.
- [ ] 공연 소품 상자의 최종 단위 gram·포장/잔해 또는 명시 손실·BOM/WU/EWU/가격과 연회 수레의 내구 수명·수리비·운반 묶음을 승인한다.

## 74. 구현 체크포인트: 동맹 신호 키트 날짜별 Sink·지원 결과 outbox

- [x] 전투 실행기의 범용 count 삭제를 제거하고 exact FacilityBuffer stack 1개의 typed Sink로 전환한다.
- [x] `accord-signal-support:{absoluteDay:D8}` operation과 physical commit/source/mass를 Run milestone 저장 권위에 보존한다.
- [x] Sink→지원 활성화→acknowledgement를 재시도 가능한 순서로 묶고 날짜별 지원 결과를 idempotent하게 유지한다.
- [x] pending provenance의 empty/commit/source/mass 일관성을 restore validation에서 fail-loud 검사한다.
- [x] current Runtime·Editor 정적 Roslyn 컴파일을 통과한다.
- [x] incoming physical receipt 양방향 whole-save 조인과 valid/missing/orphan/mass-mismatch focused fixture를 추가하고 Editor 정적 컴파일을 통과한다.
- [x] acknowledgement fault 뒤 JSON 저장·복원에서 지원 결과 재게시와 두 번째 Sink 없이 acknowledgement만 완료하는 real physical-repository fixture를 추가하고 Editor 정적 컴파일을 통과한다.
- [x] Unity에서 `AccordSignalRestoreJoinFixture.Run()`을 실행했다. valid/missing/orphan/mass mismatch와 acknowledgement fault→JSON restore→acknowledgement-only 완료가 PASS했고 두 번째 Sink·pending provenance 잔류는 `0`, Console Warning/Error `0/0`이다.
- [ ] Unity 전투 PlayMode에서 첫 guard 공격만 키트 1개를 소비하고 같은 날 후속 공격 소비 0, save/restore 후 지원 유지와 Console 0/0을 확인한다.
- [ ] 키트 단위 gram·포장/연소 잔해 또는 명시 손실·BOM/WU/EWU/가격을 승인한다.

## 75. 구현 체크포인트: 장기 보존 용기 exact Sink·보존 결과 outbox

- [x] 장기 보관소의 범용 count 삭제를 exact FacilityBuffer canister stack 1개 typed Sink로 전환한다.
- [x] part별 operation/commit/source/input grams와 outcome-published 상태를 Surgery V10 저장 권위에 보존한다.
- [x] Sink 뒤 보존 결과를 먼저 게시하고 acknowledgement 실패 시 다음 Tick에서 추가 Sink 없이 acknowledgement만 재시도한다.
- [x] empty/pending provenance, NaturalOrgan 종류, operation/commit/mass, outcome 일관성을 restore validation에서 fail-loud 검사한다.
- [x] Medical·Runtime·Editor 정적 Roslyn 컴파일을 통과한다.
- [x] Surgery whole-save incoming physical receipt 양방향 조인과 valid/missing/orphan/mass-mismatch focused fixture를 추가하고 Editor 정적 컴파일을 통과한다.
- [x] 실제 canister Sink의 acknowledgement fault/save-restore focused fixture를 공통 outbox 상태 전이로 구현하고 두 번째 Sink 0·최종 수량 보존·pending 정리를 검증하도록 컴파일한다.
- [x] Unity에서 `OrganPreservationRestoreJoinFixture.Run()`을 실행했다. valid/missing/orphan/mass mismatch가 fail-closed이고 실제 canister Sink acknowledgement fault→복원→acknowledgement-only 완료, 두 번째 Sink `0`, 최종 source 수량 `1`, pending `0`, Console Warning/Error `0/0`이다.
- [ ] Unity 장기 보관 PlayMode에서 canister 1개 소비, 보존 속도 적용, 복원 후 두 번째 소비 0과 Console 0/0을 확인한다.
- [ ] canister 단위 gram·용기 재사용/폐기·BOM/WU/EWU/가격과 장기 1개당 보존 기간을 승인한다.

## 76. 구현 체크포인트: 시설 이전 package exact Transfer-to-WIP outbox

- [x] 해체된 시설 package를 소모품 Sink가 아닌 재설치 WIP custody Transfer로 분류한다.
- [x] 범용 count 삭제를 제거하고 주문이 보유한 exact unique package stack 1개를 typed Transfer한다.
- [x] operation/commit/input grams/outcome phase를 relocation order에 보존하고 결과 게시 후 acknowledgement를 재시도한다.
- [x] Facility Evolution module schema를 V5로 올리고 empty/pending stack·mass·receipt·phase 일관성을 fail-loud 검증한다.
- [x] Evolution·Runtime·Editor 정적 Roslyn 컴파일을 통과한다.
- [x] `FacilityEvolutionPendingMaterialRestoreGuard`에 relocation owner↔incoming Transfer receipt 양방향 조인을 추가하고 Runtime·Editor 정적 컴파일을 통과한다.
- [x] current-format relocation order와 real physical repository를 사용하는 valid/missing/orphan/mass-mismatch 및 ack fault/save-restore fixture를 추가하고 Editor 정적 컴파일을 통과한다.
- [x] `packageConsumed` 결과 게시 뒤 acknowledgement가 실패해도 pending operation이 남아 있으면 다음 조회가 outbox를 다시 호출하도록 교정하고, terminal direct replay는 package를 다시 소비하지 않는 no-op으로 고정한다.
- [x] Unity에서 `FacilityRelocationPackageOutboxFixture.Run()`을 실제 실행했다. exact package Transfer, acknowledgement fault, restored acknowledgement-only completion, valid/missing/orphan/mass mismatch가 PASS했고 Console Warning/Error `0/0`이다.
- [ ] Unity 실제 해체→package 운반→저장/복원→재설치에서 package 수량·질량·ownership 보존, 두 번째 Transfer 0, Console 0/0을 확인한다.
- [ ] package 질량을 시설 BOM 질량 및 해체 손실과 연결하고 kg·운반 가능 여부·재설치 WU/EWU를 승인한다.

## 77. 구현 체크포인트: 시설 재보정 촉매 exact Transfer-to-WIP outbox

- [x] 재보정 촉매를 terminal Sink가 아닌 재보정 WIP custody Transfer로 분류한다.
- [x] 범용 count 삭제를 제거하고 destination의 exact catalyst stack 1개를 stable stack ID 순으로 선택해 typed Transfer한다.
- [x] operation/commit/source stack/input grams/outcome phase를 recalibration order에 보존하고 결과 게시 후 acknowledgement를 재시도한다.
- [x] empty/pending receipt·source·mass·state 일관성을 Facility Evolution V5 restore validation에서 fail-loud 검사한다.
- [x] Evolution·Runtime·Editor 정적 Roslyn 컴파일을 통과한다.
- [x] incoming Transfer receipt 양방향 조인과 ack fault/save-restore fixture를 추가한다. valid/missing/orphan/mass/source mismatch를 거부하고, 실제 physical repository의 acknowledgement 실패 뒤 JSON 복원·재호출에서 두 번째 Transfer가 0인 fixture가 Runtime·Editor 정적 컴파일을 통과했다.
- [x] acknowledgement가 끝난 terminal 재보정 주문의 outbox 직접 재호출을 no-op 처리하고, pending receipt의 kind/reason/commit/source/quantity/input grams를 결과 게시 전에 exact 대조한다.
- [x] 결과 게시 뒤 acknowledgement 실패 시 `materialsConsumed`만 보고 outbox를 건너뛰지 않도록 pending operation을 우선 재개하고, 실패 분기가 `Ready`를 `WaitingForMaterials`로 되돌리지 않게 교정한다.
- [ ] Unity 실제 재보정 배송→작업→복원에서 두 번째 Transfer 0, catalyst custody·질량·Console 0/0을 확인한다. `FacilityRecalibrationMaterialOutboxFixture`와 전체 Facility Evolution scenario suite의 transaction/restore 검증은 Unity에서 PASS했지만 실제 live 배송·작업 경로 증거는 아직 없다.
- [ ] 촉매 단위 gram·potency·재보정 횟수·WU/EWU/가격과 시설 투자 ROI를 승인한다.

이 체크포인트는 시설 재보정 촉매의 **물리 소유권·원자성·복원 구조**를 닫는다. 실제 Unity 실행과 최종 kg·WU·EWU·ROI가 열려 있으므로 시설 재보정 밸런스 완료를 주장하지 않는다.

## 78. 구현 체크포인트: 시설 개조 다중 재료 atomic Transfer-to-WIP outbox

- [x] `FacilityModificationOrder`의 binding/catalyst 다중 재료 범용 count 삭제를 제거하고, destination의 exact FacilityBuffer lot을 item ID·stable stack ID 순으로 선택해 하나의 atomic batch `Transfer`로 커밋한다.
- [x] operation/commit/request fingerprint/input grams/outcome phase와 source별 item ID·stack ID·quantity를 current-format Facility Evolution V6 주문 권위에 보존한다.
- [x] persisted input이 authored order requirement와 exact 일치하는지, source ID가 ordinal stable·unique인지, physical request fingerprint와 receipt kind/reason/source/quantity/mass가 일치하는지 fail-loud 검증한다.
- [x] pending modification owner↔incoming Physical Transfer를 양방향 조인하고 duplicate/missing/orphan/fingerprint/source/mass mismatch를 live publication 전에 거부한다.
- [x] dark resin 두 physical stack과 catalyst 한 stack의 3-input batch에서 acknowledgement 실패→JSON 복원→acknowledgement-only 완료→terminal replay second debit 0을 다루는 real repository fixture를 추가한다. 촉매 누락 시 dark resin이 하나도 차감되지 않는 atomic failure도 포함한다.
- [x] source ratchet으로 FacilityEvolution의 untyped FacilityBuffer consume을 0으로 고정하고 Evolution·Runtime·Editor Roslyn compile 및 scoped diff check를 통과한다.
- [x] production `FacilityInstanceEvolutionRuntime`과 restore guard는 `[Inject]` 필수 physical disposition/candidate query 생성자를 사용하고, 선택적 null 기본 매개변수는 제거한다. 기존 focused fixture용 구형 생성자는 production injection과 분리한다.
- [ ] Unity에서 focused fixture와 실제 후보 선택→두 재료 배송→개조 작업→whole-save 복원을 실행해 partial debit·두 번째 Transfer·orphan WIP 0, Console Warning/Error 0/0을 확인한다. focused atomic fixture와 Facility Evolution scenario suite는 Unity에서 PASS했지만 실제 live delivery/whole-save 경계는 별도 PlayMode 증거가 필요하다.
- [ ] dark resin·촉매 단위 gram, FacilityBuffer capacity, 운반 횟수, 개조 WU/EWU·가격·세대별 시설 ROI를 전수 질량 원장과 함께 승인한다.

이 체크포인트는 시설 개조 재료의 **물리 소유권·원자성·복원 구조**를 닫는다. 실제 Unity 실행과 최종 kg·WU·EWU·ROI가 열려 있으므로 시설 진화 밸런스 완료를 주장하지 않는다.

## 79. 구현 체크포인트: 장비 재단조·재귀속 exact Transfer-to-WIP outbox

상태: **runtime/current-format/incoming physical 양방향 조인/focused acknowledgement-fault fixture 구현·Roslyn 컴파일 완료 / Unity fresh 실행·장비 진화 경제 재검증 대기**.

- [x] `EquipmentEvolutionRuntime`의 재단조·재귀속 `TryConsumeFacilityItemBuffer` count 삭제를 모두 제거했다. 재단조의 주재료·촉매·결합재·선택 안정제와 재귀속 촉매는 terminal Sink가 아니라 각 주문의 WIP custody로 `PhysicalItemDispositionKind.Transfer`한다.
- [x] 재료 선택은 exact destination, exact authored item ID, `FacilityBuffer`, reservation `0`, stable stack ID 순서를 요구한다. 모든 요구량을 먼저 수집한 뒤 하나의 batch로 commit하므로 촉매나 안정제가 없을 때 주재료만 먼저 차감되지 않는다.
- [x] 주문 장비의 exact source stack ID를 material selector에서 제외한다. 장비 정의 item과 재단조 재료 item이 우연히 같아도 장비 본체를 input으로 소비하지 않으며, 재단조 queue는 source stack 없는 장비를 명시적으로 거부한다.
- [x] Equipment Evolution save section을 current-format `V4`로 올리고 두 주문에 operation/commit/request fingerprint/input grams/outcome phase와 source별 item ID·stack ID·quantity를 저장한다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] operation은 `equipment-reforge-material:{orderId}`와 `equipment-reattunement-material:{orderId}`, reason은 각각 `equipment-reforge-materials-to-wip`, `equipment-reattunement-catalyst-to-wip`로 분리했다. 같은 source vector라도 두 도메인 receipt를 바꿔 끼울 수 없다.
- [x] 실행 순서를 `exact batch Transfer pending → order owner provenance publish → receipt contract 검증 → materialsConsumed/equipmentDelivered/Ready outcome publish → acknowledgement → pending clear`로 고정했다. acknowledgement 실패 뒤에는 `Ready`를 `WaitingForMaterials`로 되돌리지 않고 다음 호출이 acknowledgement만 재시도한다.
- [x] 재료가 WIP로 이전된 뒤에는 기존 cancel이 장비 destination만 해제해 WIP owner를 잃지 않도록 재단조·재귀속 취소를 typed reject한다. 완료 시에는 기존 장비 evolution 결과를 게시한 뒤 장비 physical stack만 destination에서 해제한다.
- [x] restore builder가 empty/pending provenance, exact requirement vector, canonical operation/fingerprint, positive grams, outcome 상태를 검증한다. 별도 restore participant가 모든 saved order owner↔incoming Physical Transfer를 kind/reason/operation/commit/fingerprint/source IDs/quantity/grams까지 양방향 조인하고 missing/orphan/mismatch를 live publication 전에 거부한다.
- [x] real repository fixture가 재단조 split material 2-stack+catalyst, 재귀속 catalyst, 장비 source 제외, acknowledgement fault, JSON restore, acknowledgement-only completion, terminal replay second debit `0`, missing catalyst atomic failure와 missing/orphan/mass/source/fingerprint mismatch를 검사하도록 Strict Progression Combat Save suite에 등록됐다.
- [x] runtime authority ratchet이 `EquipmentEvolutionRuntime`의 untyped FacilityBuffer consume `0`, outbox 호출, restore owner-set validator와 missing-input fixture를 요구한다. `DungeonStory.Evolution`, current `Assembly-CSharp`, current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`, 신규 meta GUID 중복 `0`이다.
- [x] Unity fresh import 후 `StrictProgressionCombatSaveDebugScenarios.RunAll(false)`를 실행했다. 재단조·재귀속 outbox, 장비 수리, 전투 장비 제작 transaction, strict detached restore와 acknowledgement fault/replay가 PASS했고 Console Warning/Error `0/0`이다.
- [ ] 실제 장비 배송→다중 재료 배송→재단조/재귀속 작업→save/restore→완료 PlayMode에서 장비 stack custody, source 수량·질량, WIP orphan·두 번째 Transfer `0`, destination cleanup과 Floor Clutter를 검증한다.
- [ ] 주재료·dark resin·진화 촉매·안정제의 최종 단위 gram, 장비별 1회 진화 운반 묶음, FacilityBuffer gram capacity, 재단조/재귀속 WU·세대별 가치·EWU·가격·장비 수명 ROI를 전수 원장과 함께 승인한다.

이 체크포인트는 장비 재단조·재귀속의 **재료 물리 소유권·다중 입력 원자성·복원 구조**를 닫는다. authored kg·재료 수량·작업량·효과·가격을 변경하거나 승인하지 않았고 Unity live evidence와 전수 질량·경제 재생성이 열려 있으므로 장비 진화 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 80. 구현 체크포인트: 장비 수리 재료 Transfer·내구도 결과·출력 해제 outbox

상태: **runtime/current-format/incoming physical join/focused material fault fixture 구현·Roslyn 컴파일 완료 / Unity fresh 실행·수리 질량 경제 재검증 대기**.

- [x] `EquipmentMaintenancePolicyRuntime.CompleteOrder`의 `TryConsumeFacilityItemBuffer` count 삭제를 제거했다. 수리 재료는 사라지는 terminal Sink가 아니라 수리 공정 WIP custody로 `PhysicalItemDispositionKind.Transfer`한다.
- [x] exact 수리 material item, repair destination, `FacilityBuffer`, reservation `0`, stable stack ID 순서로 split lot을 선택하고 전체 요구량을 하나의 batch로 commit한다. 재료 하나가 부족하면 어떤 source도 차감하지 않는다.
- [x] 수리 중인 장비 source stack ID를 material selector에서 exact 제외한다. 장비와 수리 재료의 item ID가 같아도 장비 본체를 재료로 소비하지 않는다.
- [x] Equipment Maintenance save section을 current-format `V3`로 올렸다. 주문에 operation/commit/request fingerprint/input grams/source별 item·stack·quantity, 장비 source stack, durability before/after, outcome, acknowledgement, output-release phase를 저장하며 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] operation은 `equipment-repair-material:{orderId}`, reason은 `equipment-repair-materials-to-wip`로 고정했다. 시간·프레임·임의 GUID를 사용하지 않는다.
- [x] 실행 순서를 `exact material Transfer pending → WIP owner와 durability envelope 저장 → durability 결과 게시/검증 → physical acknowledgement → exact 장비 output state 전환·destination release → destination claim revoke → Completed`로 고정했다.
- [x] durability publication은 현재값이 saved before이면 after를 한 번 적용하고, 이미 after이면 crash/replay로 인정하며, 제3의 값이면 conflict로 fail-loud한다. acknowledgement 이후 재시도도 exact after 외 값을 허용하지 않는다.
- [x] acknowledgement 성공 뒤 output release가 실패해도 `materialTransferAcknowledged`와 `repairOutcomePublished`를 유지한다. 다음 호출은 재료나 내구도를 다시 적용하지 않고 exact 장비 stack의 출력 해제만 재시도하며, 해제 완료 여부를 `repairOutputReleased`로 별도 저장한다.
- [x] 재료가 WIP로 들어간 주문을 시설 소실·장비 누락 경로에서 기존 취소 처리해 destination만 해제하지 않도록 fail-loud 취소 금지 계약을 추가했다. WIP 이전 전 주문만 기존 보존적 destination release·cancel을 사용한다.
- [x] save validation이 empty/pending/acknowledged/output-released provenance와 repair material requirement, source stack, request fingerprint, positive grams, durability envelope 및 실제 equipment source/durability를 검사한다.
- [x] restore participant가 acknowledgement 전 owner와 incoming Physical Transfer를 kind/reason/operation/commit/fingerprint/source IDs/quantity/grams까지 양방향 조인한다. missing/orphan/mismatch와 acknowledged owner에 receipt가 다시 나타나는 경우를 live publication 전에 거부한다.
- [x] real repository fixture가 split repair material 2-stack, 같은 item ID의 장비 source 제외, missing-material atomic failure, acknowledgement fault, JSON restore, acknowledgement-only terminal replay와 valid/missing/orphan/mass/source/fingerprint mismatch를 검사하도록 Strict Progression Combat Save suite에 등록됐다.
- [x] runtime authority ratchet이 EquipmentMaintenance의 untyped FacilityBuffer consume `0`, Transfer outbox, durability outcome helper, restore guard와 missing-input fixture를 요구한다. current `Assembly-CSharp`와 current-source `Assembly-CSharp-Editor` Roslyn compile exit `0`, 신규 meta GUID 중복 `0`이다.
- [x] Unity fresh import 후 Strict Progression Combat Save와 `EquipmentRepairMaterialOutboxFixture`를 실행했다. acknowledgement fault/restore, second Transfer·second durability outcome `0`, incoming join이 PASS했고 Console Warning/Error `0/0`이다.
- [ ] 실제 장비 배송→split 재료 배송→작업 완료→ack fault/save restore→장비 해제 PlayMode에서 source 수량·질량, durability exact-once, destination claim, output custody, Floor Clutter와 두 번째 Transfer/수리 `0`을 검증한다.
- [ ] 갑옷·방패별 수리 재료 단위 gram, 내구도 25%당 소모량, 수리 재료가 장비에 잔존하는 질량/절삭·오염 손실, 수리 FacilityBuffer capacity, 운반 횟수, WU·EWU·가격·교체 대비 수리 ROI를 전수 원장과 함께 승인한다.

이 체크포인트는 장비 수리의 **재료 WIP 소유권·내구도 결과·출력 해제 재시도 구조**를 닫는다. 수리 재료를 장비 질량 증가로 볼지 명시 손실·폐기물로 볼지는 아직 authored mass transform으로 확정하지 않았으며, Unity live evidence와 최종 kg·WU·EWU·가격·ROI가 열려 있으므로 장비 정비 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 81. 구현 체크포인트: 전투 장비 제작 다중 입력 WIP·결과 고정·완성품 exact-once 출력

상태: **다중 재료 exact Transfer·품질 결과 저장·탄약/장비 출력 재시도·품질 거절 해체 WIP/recovery Source·incoming receipt 양방향 조인·Runtime/Editor Roslyn 컴파일 완료 / Unity live 실행·최종 질량/경제 승인 대기**.

- [x] `CombatEquipmentCraftingRuntime`과 건물 완료 핸들러를 전수 조사했다. 기존 경로는 다중 재료를 `TryConsumeFacilityItemBuffer`로 먼저 삭제하고 주문을 제거한 뒤 건물 핸들러가 완성품을 생성하여, 출력 공간 부족·unique spawn 실패·중간 저장에서 재료만 사라지거나 완성품을 재생성할 수 있었다.
- [x] 한 제작 시도의 모든 authored input을 item ID·stable source stack ID 순으로 선택해 하나의 pending `PhysicalItemDispositionKind.Transfer`로 WIP에 이전한다. 한 재료라도 부족하면 어떤 source도 차감하지 않으며 split stack 전체를 source별 item/quantity로 저장한다.
- [x] operation을 `combat-craft-material:{orderId}:{attempt:D4}`, reason을 `combat-equipment-craft-materials-to-wip`로 고정하고 commit/fingerprint/source vector/quantity/input grams를 주문에 보존한다. 반복 품질 제작은 attempt별 독립 operation을 사용한다.
- [x] Combat Equipment current-format schema를 V7로 올리고 `facilityPersistentId`, material receipt provenance, 확정 품질·Mythic provenance·maker, output item/quantity/operation/commit/instance/stack와 publication phase를 저장한다. 과거 save migration은 추가하지 않았다.
- [x] 작업량이 완료되면 품질 RNG·신화 판정·출력 종류와 수량을 정확히 한 번 확정해 주문에 먼저 기록한다. 출력 공간 또는 acknowledgement가 실패한 재시도는 저장된 결과를 사용하고 품질을 다시 굴리지 않는다.
- [x] 탄약 묶음 출력은 deterministic `ProductionOutputCommit` component로 FacilityOutputBuffer에 게시한다. 같은 operation 재호출은 exact item/quantity/position/destination을 검증하고 기존 출력만 재사용하므로 두 번째 stack을 만들지 않는다.
- [x] unique 장비 출력은 저장된 equipment instance ID를 단일 identity로 사용한다. 기존 stack을 instance ID로 재발견하거나 한 번만 생성하고 output stack·equipment state component·commit marker를 대조한 뒤에만 published로 기록한다.
- [x] 건물 완료 핸들러의 후행 `SpawnCraftedOutput` 경로를 제거했다. 제작 runtime이 물리 출력 게시와 WIP acknowledgement를 완료한 경우에만 `ApplyCraftWork`가 완료 `1`을 반환하므로 주문 제거와 물리 출력 사이의 공백이 없다.
- [x] 결과·품질 side effect와 physical output이 게시된 뒤에만 material receipt를 acknowledge한다. acknowledgement failure/restore에서는 pending order가 output commit/instance를 재검증한 뒤 acknowledgement만 재시도하며 재료 debit·품질 roll·출력 생성을 반복하지 않는다.
- [x] `CombatEquipmentCraftMaterialRestoreGuard`가 모든 pending craft owner→incoming receipt와 모든 `combat-craft-material:*` receipt→owner를 kind/reason/commit/fingerprint/source/quantity/grams까지 양방향 조인한다. missing/orphan/mismatch와 acknowledged receipt 재등장을 publish 전 fail-loud한다.
- [x] real repository focused fixture가 lumber split stack 2개+iron 1개 atomic Transfer, missing input rollback, 탄약 20개 output double-call identity, acknowledgement 1회 실패, JSON restore, second debit/output `0`, valid/missing/orphan/mass/fingerprint join을 검사하도록 Strict Progression Combat Save suite에 등록됐다.
- [x] Runtime authority gate가 combat crafting count consume 재도입, outbox/terminal finalizer/restore guard/second-output fixture 누락과 Combat Equipment V7 drift를 실패시킨다. `DungeonStory.Combat`, current-source Runtime, current-source Editor Roslyn compile exit `0`, scoped diff error `0`, production combat crafting `TryConsumeFacilityItemBuffer` 호출 `0`이다.
- [ ] Unity Strict Progression Combat Save suite와 실제 작업대 배송→작업→출력 공간 부족→save/restore→출력 회복을 실행해 second Transfer/roll/output `0`, destination cleanup, Floor Clutter와 Console Warning/Error `0/0`을 확인한다. strict suite와 제작 transaction fixture는 PASS했다. 이 과정에서 deterministic `outputOperationId`만 있는 pre-publication owner를 outbox가 conflict로 거부하던 production 결함을 수정했지만 실제 작업대 PlayMode는 아직 열려 있다.
- [x] 품질 미달 자동 해체의 기존 `DeleteStack`과 recovery `SpawnItemAt`을 제거했다. rejected unique stack은 attempt-scoped pending `Transfer`로 dismantle WIP에 들어가고 고정 recovery vector는 output별 deterministic Source commit으로 게시된다. recovery 전체 게시 뒤에만 input receipt를 acknowledge하며 restore guard가 dismantle owner/receipt도 양방향 조인한다. 이는 저장 가능한 두 단계 typed Transform 계약이며 unit gram 확정 뒤 receipt input mass−recovery output mass를 명시 손실로 감사한다.
- [ ] 무기·방어구·방패·화살·볼트 input/output 단위 gram, 절삭·제련·조립 손실, FacilityBuffer 2~4회분, 25kg 운반 묶음, 제작 WU·EWU·가격·품질 반복/해체 차익을 전수 원장과 6인 생존망에서 승인한다.

이 체크포인트는 전투 장비 제작의 **입력 WIP와 accepted/quality-rejected 물리 출력 및 자동 해체 recovery publication 경계**를 닫는다. Unity 실행과 최종 kg·BOM 손실·버퍼·운반·WU·EWU·가격·품질 ROI가 열려 있으므로 전투 장비 제작 또는 전체 밸런스 완료로 보고하지 않는다.

## 82. 구현 체크포인트: 방어 시설 정비 Sink·보급품 내부 custody Transfer outbox

상태: **정비/보급 exact-lot physical transaction·Defense Facility V2·incoming receipt 양방향 조인·acknowledgement-fault fixture·정적 컴파일 완료 / Unity live 실행·탄약/연료 질량 lifecycle 승인 대기**.

- [x] `DefenseFacilityRuntime`의 잼 해제 1곳과 보급 충전 2곳에 있던 `TryConsumeFacilityItemBuffer`, category-only `TryConsumeFacilityBuffer`를 감사했다. 기존 코드는 물리 스택을 먼저 삭제한 뒤 `operationalState` 또는 `supply`를 별도 변경하여 저장·실패 경계에서 재료 손실·중복 충전·source gram 불명확성이 있었다.
- [x] Defense Facility current-format schema를 V2로 올리고 정비/보급별 operation sequence, phase, operation/reason/commit/request fingerprint, destination, item, exact source별 quantity, input grams와 supply before/after/granted를 저장한다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] 잼 해제 철괴 1개는 `defense-maintenance-consume:{facilityId}:{sequence:D8}` operation과 `defense-maintenance-part-sink` reason의 exact pending `Sink`로 전환했다. 상태를 `Preparing`으로 게시한 뒤에만 receipt를 acknowledge하며 실패/복원 시 상태 결과를 반복하지 않고 acknowledgement만 재시도한다.
- [x] 특정 탄약·연료·독성 코팅은 `defense-supply-load:{facilityId}:{sequence:D8}` operation과 `defense-supply-to-internal-custody` reason의 exact pending `Transfer`로 시설 내부 supply custody에 넣는다. source는 exact destination·item·FacilityBuffer·reservation 0·stable stack ID 순이며 split stack 전체를 원자적으로 선택한다.
- [x] 혼합 탄약 상자는 1상자=`8` supply units로 변환하되 남은 내부 용량이 8 미만이면 상자를 부분 소비하지 않는다. 기존 `Ceil(wanted/8)` 뒤 capacity clamp로 1–7발이 사라질 수 있던 경로를 제거했다. authored 전용 탄약이 있으면 이를 먼저 사용하고 혼합 상자는 exact 8발 단위 fallback으로만 사용한다.
- [x] 모든 실제 physical-supply 방어 에셋이 canonical `supplyItemId`를 가진다는 authority 감사를 근거로 category-only 물리 삭제와 category delivery fallback을 제거했다. 자동 보급 요청은 exact authored item을 요구하며 item ID가 없는 잘못된 physical-supply 정의는 물자를 임의 대체하지 않고 실패한다.
- [x] `DefenseFacilityPhysicalRestoreGuard`가 pending maintenance/supply owner→incoming receipt와 두 operation prefix의 incoming receipt→owner를 kind/reason/operation/commit/fingerprint/source/quantity/grams까지 양방향 조인한다. missing/orphan/mismatch와 중복 operation을 live publish 전에 거부한다.
- [x] real repository focused fixture가 보급품 split 2-stack atomic Transfer, missing input partial-debit 0, 정비 철괴 Sink, acknowledgement 1회 실패, V2 JSON 복원, acknowledgement-only 완료, second debit 0과 valid/missing/orphan/mass mismatch를 검사하도록 P1 Defense Facility suite에 등록됐다.
- [x] Runtime authority ratchet이 Defense runtime의 item/category count consume 0, physical outbox, V2 schema, incoming reverse join과 acknowledgement-fault fixture를 고정한다. `DungeonStory.Defense`, current-source Runtime, current-source Editor Roslyn compile exit `0`, scoped diff error `0`, 신규 meta GUID 중복 `0`이다.
- [ ] Unity P1 Defense Facility suite와 실제 보급 배송→split 충전→발동→잼→정비→ack fault/save restore를 실행해 second debit/supply outcome `0`, destination cleanup, Floor Clutter와 Console Warning/Error `0/0`을 확인한다. P1 suite와 physical transaction fixture는 PASS했고 Console `0/0`이지만 AI 배송·Floor Clutter를 포함한 live PlayMode는 아직 열려 있다.
- [ ] 철괴·석탄·함정 캔·폭약·볼트·독성 코팅·혼합 탄약 상자의 최종 unit grams, 포장 tare/빈 용기 또는 발사·연소 잔해, 내부 magazine mass, initial supply의 건설 BOM 귀속, FacilityBuffer gram capacity, 25kg 운반 묶음, 발동당 비용·WU/EWU·가격·방어 ROI를 전수 원장과 6인 생존망에서 승인한다.

이 체크포인트는 방어 시설의 **world FacilityBuffer debit과 저장 가능한 정비/내부 보급 결과 사이 원자성**을 닫는다. 내부 supply 단위의 발동별 질량 감소·포장 반환/잔해와 authored initial supply의 물리 Source는 최종 unit gram·package lifecycle 감사에 남아 있으므로 방어 시설 중량 또는 전체 밸런스 완료로 보고하지 않는다.

## 83. 구현 체크포인트: 파종·인증 종자 입력 WIP와 생태/출력 exact-once 경계

상태: **파종/인증 입력 exact Transfer·Crop Plot V5·Certified Seed V2 frozen output capability·생태 envelope·인증 출력 commit·시설 파괴 WIP terminal loss·incoming receipt/output preflight·focused fixture·정적 컴파일 완료 / common gram publication·Unity live·최종 종자/농업 질량 경제 대기**.

- [x] `PhysicalSeedLotGateway.TryConsumeSowingInputs`의 두 live caller를 감사했다. 작물 파종은 count debit 뒤 비멱등 `ecology.Sow/ApplyCompost`를 호출했고, 인증 종자는 destination stack 자체를 주문으로 추론해 input debit과 output spawn 사이 저장에서 주문·종자 결과를 잃을 수 있었다.
- [x] `CropPhysicalTransactionOutbox`를 추가해 seed lot·물·퇴비·연료·cycle supply와 인증 키트를 exact destination·item ID·`FacilityBuffer`·reservation 0·stable stack ID 순으로 선택하고 한 batch `PhysicalItemDispositionKind.Transfer`로 WIP custody에 이전한다. 요구 재료 하나라도 부족하면 어떤 source도 차감하지 않는다.
- [x] operation/reason을 파종 `crop-sow-input:{plotId}:{sequence:D8}` / `crop-sow-input-to-wip`, 인증 `certified-seed-input:{orderId}` / `certified-seed-input-to-wip`로 분리하고 exact source별 item/stack/quantity, input grams, commit/fingerprint와 seed-lot component snapshot을 domain owner에 저장한다.
- [x] Crop Plot current-format schema를 V5로 올려 monotonic sow sequence, pending input owner, 마지막 실제 grid 좌표와 시설 파괴 WIP terminal loss를 저장한다. 과거 V2/V3/V4 migration은 추가하지 않았고 현재 형식 외 payload를 fail-loud 거부한다.
- [x] 파종 input receipt를 커밋하기 직전 생태 plot fingerprint를 저장하고 `Sow`와 선택적 compost 적용 뒤 after fingerprint를 저장한다. `InputCommitted` 복원은 before, `OutcomePublished` 복원/재시도는 after와 exact 일치해야 하며 제3의 생태 상태는 진행하지 않는다.
- [x] 파종 실행 순서를 `exact input Transfer pending → ecology before 검증 → Sow/compost 결과 게시 → crop phase/materialsConsumed 게시 → receipt acknowledgement → pending clear/sequence 증가`로 고정했다. acknowledgement 실패는 ReadyToSow와 after envelope를 유지하고 다음 Tick에서 acknowledgement만 재시도한다.
- [x] Certified Seed는 더 이상 transient destination 존재를 주문 권위로 사용하지 않는다. `CertifiedSeedWorldSaveData V1`이 order ID/sequence/action/facility/crop/destination/input owner, 확정 certified seed state와 output operation/commit을 소유하고 strict rollback-free save section으로 등록된다.
- [x] 인증 결과는 input seed의 pathogen load를 정확히 한 번 30 낮춰 저장한 뒤 seed-state component와 deterministic `ProductionOutputCommit` component를 함께 `FacilityOutputBuffer`에 게시한다. 같은 operation 재호출은 item/quantity/position/destination/seed state를 검증해 기존 stack만 재사용하며 두 번째 종자를 생성하지 않는다.
- [x] 전역 save preflight가 인증 주문의 pending input receipt와 output commit-tagged physical stack을 대조한다. `OutputPublished` owner는 exact seed item 1개와 동일 cultivar/generation/pathogen state가 없으면 live publication 전에 실패한다.
- [x] `CropPhysicalRestoreGuard`가 crop sow와 certified input owner→incoming receipt 및 두 operation prefix receipt→owner를 kind/reason/operation/commit/item-layer fingerprint/source/quantity/grams까지 양방향 조인한다. Crop Ecology restore 뒤 sow owner의 before/after fingerprint도 실제 복원 aggregate와 대조한다.
- [x] real repository focused fixture가 split water 2-stack+component seed lot atomic Transfer, missing water partial debit 0, acknowledgement fault, V5 JSON owner round-trip, acknowledgement-only recovery, destroyed-plot exact loss replay, certified-seed V1 owner round-trip와 missing/orphan/mass mismatch를 검사하도록 작성됐다.
- [x] runtime authority ratchet이 crop count consume/legacy `TryConsumeSowingInputs` 재도입, V5/persistent certified owner/ecology envelope/exact output/destroyed-plot WIP loss/restore reverse join/fault fixture 누락을 실패시킨다. current-source Economy·Runtime·Editor Roslyn compile exit `0`, scoped diff error `0`, production `TryConsumeFacilityItemBuffer` live caller는 `4→3`으로 감소했다.
- [x] Unity MCP fresh import 뒤 `Tools/DungeonStory/Economy/Verify Crop Physical Transactions`와 Crop Plot Runtime verifier를 실행했다. physical fixture 반복 PASS, runtime report `valid=true`, output containment work/quantity conservation true, outdoor harvest `0→6`, indoor `Growing`, Console Warning/Error `0/0`이다.
- [ ] 실제 P23/P24 배송→파종→ack fault/save restore→성장 및 greenhouse 인증 배송→output buffer→별도 haul을 PlayMode에서 실행해 partial/second Transfer, second Sow/compost, duplicate/missing seed, component loss, orphan receipt/WIP와 Floor Clutter가 모두 0인지 검증한다.
- [x] 시설이 input commit 직후 파괴되면 이미 WIP custody로 넘어간 종자·물·퇴비를 원래 창고로 순간이동시키지 않고 `DestroyedWithPlotLoss`로 exact 수량·그램 귀속한다. input receipt acknowledgement가 실패하면 V5 owner와 마지막 실제 좌표를 보존하고 재시도하며, 성공한 뒤에만 plot state와 crop ecology owner를 제거한다.
- [ ] 종자·깨끗한 물·퇴비·연료·온실 영양제·접종 통나무·인증 키트의 최종 unit grams와 포장/오염/손실, 파종·인증 `FacilityBuffer` 2~4회분 gram capacity, 25kg 운반 묶음, 농업 WU/EWU·가격·토지/용수·6인 gross 125%/net 110%를 전수 원장과 다중 seed에서 승인한다.

이 체크포인트는 농업의 **world input debit과 파종 생태 결과, 시설 파괴 WIP 손실 및 인증 종자 물리 출력 사이 원자성**을 current source에서 닫는다. 실제 Unity 실행과 최종 kg·버퍼·운반·생산/소비·WU/EWU·가격이 열려 있으므로 농업 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 84. 구현 체크포인트: 대형 사업 exact 자재 Sink·완공 결과 outbox

상태: **current-format V2 owner/incoming physical 양방향 조인/acknowledgement-fault fixture 구현·Economy/Runtime/Editor 컴파일·Unity focused PASS / 실제 PlayMode·authored 질량 경제 재검증 대기**.

- [x] `GrandProjectRuntime.ApplyWork`의 `ConsumeDelivered` count 삭제를 제거하고, 사업별 모든 납품 자재를 exact FacilityBuffer lot 하나의 atomic pending `Sink`로 커밋한다.
- [x] 물리 operation을 `grand-project-materials:{projectId}`, reason을 `grand-project.infrastructure-embedded`로 고정하고 시간·프레임·임의 GUID를 사용하지 않는다.
- [x] `GrandProjectPhysicalCommitSaveData`에 phase/project/operation/reason/request fingerprint/commit/source stack/input quantity/input grams와 before/after state fingerprint를 저장한다.
- [x] Grand Project current-format save schema를 V2로 올리고 과거 세이브 마이그레이션 없이 필수 owner envelope 누락·legacy version을 fail-loud 거부한다.
- [x] 실행 순서를 `exact Sink pending → owner publish → 완공 상태 publish → acknowledgement → owner clear`로 고정한다. acknowledgement 실패 시 completed project와 pending owner를 유지하고 다음 Tick은 두 번째 Sink 없이 acknowledgement만 재시도한다.
- [x] pending owner가 있는 동안 사용자 취소를 typed reject하고, 이미 완공 결과가 게시된 owner를 inactive state 정규화가 지우지 않게 한다.
- [x] restore validation이 empty/pending provenance, canonical project/operation/reason/commit, positive grams, stable unique source IDs와 state before/after envelope를 검증한다.
- [x] `GrandProjectSaveSection`이 owner→incoming receipt와 incoming prefix receipt→owner를 kind/reason/operation/commit/fingerprint/source/quantity/grams까지 양방향 조인해 missing/orphan/mismatch를 live publication 전에 거부한다.
- [x] 물리 batch receipt가 runtime owner에게도 item-layer request fingerprint를 노출하도록 공용 receipt 계약을 강화하고, physical save validation이 동일 fingerprint로 receipt identity를 재구성한다.
- [x] focused fixture가 acknowledgement fault→V2 JSON capture→valid/missing/orphan restore join→acknowledgement-only replay를 검사하고 input commit count `1`, 복원 후 두 번째 commit `0`, completed outcome exact-once를 요구한다.
- [x] Runtime authority ratchet이 GrandProject count 소비 금지, exact physical Sink gateway, V2 owner, reverse join과 focused fixture를 컴파일 타임 source contract로 고정한다.
- [x] current-source `DungeonStory.Economy`, `Assembly-CSharp`, `Assembly-CSharp-Editor` Roslyn compile exit `0`을 확인한다.
- [x] Unity fresh import 후 `DungeonStory/Debug/Economy/Run Production Economy Contracts`를 실행해 acknowledgement-fault fixture와 Console Warning/Error `0/0`을 확인했다. default empty receipt의 null-safe 판정 결함을 수정한 뒤 전체 suite가 PASS했다.
- [ ] 실제 영주 집무실에서 자재 배송→완공→ack fault/save restore→효과 적용 경로를 실행해 partial debit·second Sink·중복 benefit·orphan receipt `0`을 확인한다.
- [ ] 사업 중 집무실 파괴/교체 시 이미 WIP에 들어간 자재를 infrastructure embedded Sink로 확정할지, 복구 가능한 rubble/byproduct Transform으로 반환할지 authored 계약을 별도로 승인한다.
- [ ] 각 대형 사업 BOM의 최종 unit gram, 집무실 FacilityBuffer capacity, 25kg 운반 횟수, 건설 WU/EWU·가격·효과 ROI와 던전 공간/6인 성장 노동을 전수 원장으로 재검증한다.

이 체크포인트는 대형 사업 완료 경계의 **exact 물리 자재 debit·완공 결과·복원 원자성**을 닫는다. 실제 Unity 실행과 최종 kg·버퍼·운반·WU/EWU·ROI가 열려 있으므로 대형 사업 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 85. 구현 체크포인트: 생산 재고 센서 설치 exact Sink·V14 owner

상태: **설치 exact Sink/current-format Production V15/incoming receipt 양방향 조인/acknowledgement-fault fixture·제거 physical Source output·Unity focused PASS / 실제 PlayMode·authored 질량 경제 대기**.

- [x] `ProductionStockSensorRuntime`이 `IProductionAssemblyBridge.ConsumeDelivered`를 통해 센서 1개를 count 삭제하던 설치 경계를 감사했다. input debit과 installed facility ID publication 사이에 exact lot·gram owner가 없었다.
- [x] 설치 센서 1개를 exact destination/item/FacilityBuffer/reservation 0/stable stack 순으로 선택해 `production-stock-sensor-install:{facilityId}` / `production-stock-sensor.infrastructure-embedded` pending `Sink`로 커밋한다.
- [x] Production current-format schema를 V14로 올리고 `ProductionStockSensorPhysicalCommitSaveData`에 phase/facility/item/destination/operation/reason/fingerprint/commit/source IDs/quantity/input grams를 저장한다. 과거 세이브 마이그레이션은 추가하지 않았다.
- [x] 실행 순서를 `exact Sink pending → owner 저장 → installed outcome 저장 → acknowledgement → owner clear`로 고정한다. acknowledgement 실패 뒤에는 installed 결과와 owner를 유지하며 다음 finalize 호출은 두 번째 Sink 없이 acknowledgement만 재시도한다.
- [x] pending installation이 있는 시설의 제거를 typed reject하고, live 시설이 사라진 owner는 조용히 acknowledge·삭제하지 않는다.
- [x] 설치 물리 계층을 `IProductionStockSensorPhysicalGateway`로 분리하고 production composition에 필수 등록한다. adapter는 모델 receipt만 보며 item-layer receipt를 그대로 owner provenance로 투영한다.
- [x] `ProductionBillStateCodec`가 owner 목록의 canonical facility order, operation/destination/reason/commit, exact quantity 1, positive grams, stable unique source IDs와 phase↔installed outcome 일관성을 fail-loud 검증한다.
- [x] `ProductionBillsSaveSection`이 saved owner→incoming Sink와 모든 `production-stock-sensor-install:*` receipt→owner를 kind/reason/operation/commit/fingerprint/source/quantity/grams까지 양방향 조인한다.
- [x] `IProductionItemGateway`와 `IProductionAssemblyBridge`의 사용되지 않는 legacy `ConsumeDelivered` API 및 `ProductionItemGateway.TryConsumeFacilityItemBuffer` 구현을 제거했다. production semantic count-debit caller는 GrandProject/StockSensor 제거 후 `WorkAmountSystem`, `FluidNetworkRuntime` 두 곳만 남는다.
- [x] focused production fixture가 acknowledgement 1회 실패, installed outcome 유지, pending V14 owner, valid/missing/orphan incoming receipt, acknowledgement-only 재시도와 second debit `0`을 검사한다.
- [x] Runtime authority ratchet이 ProductionItemGateway count debit 0, stock-sensor legacy consume 0, exact pending Sink, V14 owner, reverse join, gateway registration과 acknowledgement-fault fixture를 고정한다.
- [x] current-source `DungeonStory.Production`, `DungeonStory.Economy`, `Assembly-CSharp`, `Assembly-CSharp-Editor` Roslyn compile exit `0`과 scoped diff error `0`을 확인한다.
- [x] Unity fresh import 후 `DungeonStory/Debug/Economy/Run Production Economy Contracts`를 실행해 센서 설치 fault fixture와 Console Warning/Error `0/0`을 확인했다.
- [ ] 실제 센서 배송→설치→ack fault/save restore→재고 모드 해금 경로에서 second Sink·중복 설치·orphan owner `0`을 PlayMode로 확인한다.
- [x] 센서 제거의 `installed state 제거 → loose SpawnOutput` 경계를 Production V15 pending removal owner와 deterministic physical Source output으로 닫았다. output-space 실패는 installed/acknowledged/embedded-mass 상태를 보존하며 exact output이 incoming candidate에 존재한 뒤에만 설치 상태를 제거한다.
- [ ] 센서 1개의 최종 gram, 포장/장착 손실, 시설 입력 버퍼 capacity, 25kg 운반 묶음, 설치/제거 WU·EWU·가격과 자동 재고 정책 ROI를 전수 원장으로 재검증한다.

이 체크포인트는 생산 센서 **설치 input debit과 installed outcome 사이 원자성**을 닫고, 제거 물리 output은 체크포인트 88에서 닫았다. Unity live와 최종 kg·버퍼·운반·WU/EWU·가격이 열려 있으므로 생산 센서 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 86. 구현 체크포인트: 수동 급수·물병 급수망 exact Transfer outbox

상태: **수동 급수와 자동 물병 급수의 count/category debit 제거·Fluid V6·incoming receipt 양방향 조인·acknowledgement-fault fixture·Infrastructure/Runtime/Editor Roslyn 컴파일·Unity focused PASS / 실제 AI PlayMode·빈 용기 lifecycle·최종 물 질량 경제 대기**.

- [x] `FluidNetworkRuntime`의 수동 물 공급 `TryConsumeFacilityItemBuffer`와 자동 물병 급수 `TryConsumeFacilityBuffer`를 함께 전수 감사했다. 두 경로 모두 exact item/stack/gram owner 없이 물리 수량을 먼저 삭제한 뒤 유체 reserve 또는 network water를 별도로 증가시키고 있었다.
- [x] 수동 즉시 급수를 exact destination, `resource:clean-water`, `FacilityBuffer`, reservation `0`, stable stack ID 순으로 선택한 `PhysicalItemDispositionKind.Transfer`로 전환했다. 같은 Water category의 다른 아이템은 대체 투입하지 않는다.
- [x] 수동 즉시 operation을 `manual-water-immediate:{nodeId}:{sequence:D8}`로 고정하고 node별 양의 monotonic sequence를 저장한다. acknowledgement가 끝난 뒤에만 sequence를 증가시켜 재시도에서 새 operation으로 두 번째 물리 debit을 만들지 않는다.
- [x] 수동 staged/즉시 owner에 request fingerprint, operation sequence, immediate 여부, commit/source/quantity/input grams를 저장하고 physical receipt와 exact 대조한다. 외부 staged process operation과 runtime immediate operation을 서로 바꿔 끼울 수 없다.
- [x] 수동 급수 실행 순서를 `exact Transfer pending → reserve outcome publish → acknowledgement → owner clear`로 고정했다. acknowledgement 1회 실패 fixture에서 reserve는 한 번만 증가하고 다음 호출은 두 번째 Transfer/outcome 없이 acknowledgement만 재시도한다.
- [x] Fluid current-format schema를 V6로 올려 node별 immediate sequence와 pending container-feed owner를 저장한다. 과거 세이브 마이그레이션이나 누락 필드 fallback은 추가하지 않았다.
- [x] 자동 물병 급수도 category count 삭제 대신 exact `resource:clean-water` FacilityBuffer lot을 stable stack ID 순으로 선택하는 pending Transfer로 전환했다. 입력 부족 시 exact item delivery만 요청한다.
- [x] 자동 급수 실행 순서를 `exact Transfer pending → network water outcome publish → acknowledgement → owner clear/sequence 증가`로 고정했다. 결과 게시 후 acknowledgement 실패·복원 시 pending outcome을 먼저 회복해 물을 두 번 더하지 않는다.
- [x] 자동 급수 owner에 phase, sequence, operation/reason/fingerprint/commit, node/network/destination/item, source별 quantity, water units와 input grams를 저장하고 empty/pending 상태를 fail-loud 검증한다.
- [x] `FluidInfrastructureSaveSection`이 수동/자동 pending owner→incoming Physical Transfer와 두 operation 계열 receipt→owner를 kind/reason/operation/commit/fingerprint/source/quantity/grams까지 양방향 조인한다. missing/orphan/mismatch 및 operation 중복을 live publication 전에 거부한다.
- [x] Fluid aggregate 자체 검증을 incoming physical 검증보다 먼저 detached candidate에서 수행한다. 잘못된 Fluid payload 때문에 Physical candidate index를 만들거나 live state를 부분 변경하지 않는다.
- [x] Runtime authority ratchet이 Fluid runtime의 두 legacy count/category debit 재도입, V6 drift, immediate sequence/feed owner, restore reverse join과 acknowledgement-fault fixture 누락을 실패시킨다.
- [x] current-source `DungeonStory.Infrastructure`, `Assembly-CSharp`, `Assembly-CSharp-Editor` Roslyn compile exit `0`과 scoped diff error `0`을 확인했다.
- [x] repository-wide semantic count debit을 다시 조사해 Fluid 계통은 `0`이며, 남은 production caller는 `WorkAmountSystem.EnsureMaterialsReady`의 한 경로뿐임을 확인했다.
- [x] Unity fresh import 후 Physical Item Contracts와 Industrial Infrastructure focused fixture를 실행해 수동/자동 급수 acknowledgement fault, V6 whole-save join과 Console Warning/Error `0/0`을 확인했다. 의료 시설 빌더가 산업 공정 유체와 M01 서비스 허브를 지우던 교차 빌더 결함을 수정하고, M01–M13 의료 에셋 연속 2회 재생성 SHA-256 byte diff `0`, Industrial Infrastructure·Service Room 회귀 PASS를 함께 확인했다.
- [ ] 실제 AI 물 운반→수동 reserve 및 자동 물병 급수→network 소비→save/restore→복구 PlayMode에서 second Transfer/outcome, orphan owner, 잘못된 category 대체와 Floor Clutter가 모두 `0`인지 확인한다.
- [ ] 물 소비 뒤 포장 tare를 빈 용기로 반환할지 일회용 포장 폐기 Transform으로 처리할지 authored container lifecycle을 확정하고 exact output/byproduct 질량을 연결한다.
- [ ] 깨끗한 물 1개 단위 gram, 물병/용기 tare, Fluid/FacilityBuffer 2~4회분 gram capacity, 25kg 운반 묶음, 급수 WU·EWU·가격과 6인 7일 식수망을 전수 원장과 다중 seed에서 승인한다.

이 체크포인트는 유체 급수의 **물리 물병 debit과 reserve/network outcome 사이 원자성·복원 구조**를 닫는다. 빈 용기·포장 질량, 실제 Unity 실행, 최종 kg·버퍼·운반·WU·EWU·가격·6인 식수 폐쇄 루프가 열려 있으므로 급수 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 87. 구현 체크포인트: 건설 재료 atomic Transfer-to-WIP·취소 restitution

상태: **건설 재료 count 삭제 제거·exact 다중 BOM Transfer·Work Order V6 WIP 소유권·취소 restitution·부분 committed-output 복원 preflight·incoming receipt 양방향 조인·Runtime/Editor 컴파일·Unity focused PASS / 실제 PlayMode·최종 건설 질량 경제 대기**.

- [x] `WorkOrderRuntime.EnsureMaterialsReady`가 delivered count 부족분을 `TryConsumeFacilityItemBuffer`로 삭제한 뒤 논리 수량만 올리던 경계를 전수 감사했다. 이미 논리 delivery로 바뀐 재료가 취소 때 복구되지 않고, 건설 배치 실패 전에 주문이 제거될 수 있던 수명주기 결함도 함께 확인했다.
- [x] 건설 BOM 전체를 exact destination, exact item ID, `FacilityBuffer`, reservation `0`, stable stack ID 순으로 먼저 선택한 뒤 하나의 atomic batch `PhysicalItemDispositionKind.Transfer`로 WIP custody에 커밋한다. 일부 재료가 부족하면 어떤 source도 차감하지 않는다.
- [x] operation을 `work-order-materials:{workOrderId}`, reason을 `work-order-materials-to-construction-wip`로 고정하고 source별 item ID·stack ID·quantity, request fingerprint, commit ID, input quantity와 input grams를 Work Order V6에 저장한다.
- [x] 실행 순서를 `exact Transfer pending → authored BOM delivered outcome publish → acknowledgement → Acknowledged custody`로 고정했다. acknowledgement 실패 뒤 `CustodyPublished`를 보존하고 재시도는 두 번째 debit 없이 acknowledgement만 수행한다.
- [x] delivered outcome은 owner source의 item별 합이 authored construction BOM과 exact 일치할 때만 게시한다. partial delivered count, 중복 source stack, 비정렬 source, quantity·mass·fingerprint mismatch는 fail-loud한다.
- [x] `WorkOrdersSaveSection`이 acknowledgement 전 owner→incoming Physical Transfer와 `work-order-materials:*` receipt→owner를 kind/reason/operation/commit/fingerprint/source/quantity/grams까지 양방향 조인한다. missing/orphan/mismatch를 live publication 전에 거부한다.
- [x] cancellation의 `RemoveStacksByStateAndDestination` 무타입 일괄 삭제를 제거했다. 아직 commit되지 않은 destination 재료는 lease/destination을 보존적으로 해제하고, acknowledged WIP는 deterministic `work-order-material-restitution:{workOrderId}` Source output으로 exact BOM·input grams를 반환한다.
- [x] restitution output 공간이 없으면 주문을 삭제하지 않고 `RestitutionPending`·`WaitingForOutputSpace`로 유지한다. Tick 재시도는 동일 operation ID의 committed Source output을 재사용하며 두 번째 반환을 만들지 않는다.
- [x] 재료가 있는 주문에서 `refundDeliveredMaterials:false`를 production 경로가 사용해 WIP를 버리는 것을 typed reject한다. debug instant construction과 debug completion도 재료 delivered count를 합성하지 않고 실제 physical readiness를 거친다.
- [x] `ConstructionSite.CancelConstruction`은 주문 cancellation/restitution이 성공한 경우에만 site를 제거한다. 실패한 반환이나 pending owner가 있는데 건설 표지만 먼저 사라지는 orphan 경로를 막는다.
- [x] 건설 완료는 acknowledged custody를 요구하며 `site.CompleteConstruction()`이 실제로 성공한 뒤에만 주문·site mapping을 Completed로 제거한다. placement failure는 주문을 `Blocked`로 보존한다.
- [x] focused fixture가 split construction material의 single commit, injected acknowledgement failure, V6 `CustodyPublished` JSON, valid/missing/orphan incoming receipt, acknowledgement-only 회복과 terminal `Acknowledged`를 검사하도록 작성했다.
- [x] cancellation fixture가 actual physical material commit/acknowledgement 뒤 exact lumber `2`개·`2,000g` restitution output, single commit/acknowledgement와 order 제거를 검사하도록 작성했다.
- [x] Runtime authority ratchet이 WorkAmount count consume·untyped bulk removal 재도입, exact pending Transfer/restitution/V6/reverse join과 acknowledgement-fault·single-commit fixture 누락을 실패시킨다. current-source Runtime·Editor Roslyn compile exit `0`, production WorkAmount semantic count-debit caller는 `0`이다.
- [x] Unity fresh import 후 Work Amount/Physical Item focused scenarios를 실행해 acknowledgement fault, V6 whole-save join, exact cancellation restitution과 Console Warning/Error `0/0`을 확인했다. fixture가 authored item catalog와 strict unique-stack authority를 실제로 합성하도록 교정한 뒤 두 suite가 모두 PASS했다.
- [ ] 실제 AI 재료 운반→construction FacilityBuffer→재료 commit→작업→배치와 취소·Downed·save/restore PlayMode에서 second Transfer, partial debit, orphan WIP, teleport와 Floor Clutter가 모두 `0`인지 확인한다.
- [x] incoming Physical restore candidate에 committed output stack query를 추가하고, 여러 restitution output 중 0개·일부·전부가 게시된 current-format save를 item/quantity/commit/mass/state/position/destination까지 preflight한다. 부분 집합은 exact subset만 허용하고 전부 게시됐을 때 총 output grams가 input custody grams와 같아야 한다.
- [ ] current-source active facility 356개와 deprecated compatibility facility 21개를 분리해 construction BOM item grams, site FacilityBuffer capacity, 25kg 운반 묶음, 건설 WU·EWU·가격·철거 회수·공간과 6인 성장 노동을 전수 원장으로 재검증한다.

이 체크포인트는 건설 재료의 **physical delivery→construction WIP 소유권→완료/취소 경계 원자성**을 current source에서 닫고, partial restitution restore preflight는 체크포인트 88에서 닫았다. Unity live와 최종 시설 kg·버퍼·운반·WU/EWU·가격·공간·6인 성장 루프가 열려 있으므로 건설 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 88. 구현 체크포인트: 재고 센서 제거 physical Source output·committed-output restore query

상태: **Production V15 설치 질량 보존·제거 pending owner·deterministic Source output·incoming committed-output preflight·건설 restitution 부분 출력 preflight·Items/Production/Economy/Runtime/Editor 컴파일·Unity focused PASS / 실제 PlayMode·최종 센서 질량 경제 대기**.

- [x] `ProductionStockSensorRuntime.Remove`가 installed/acknowledged 상태를 먼저 제거한 뒤 반환값을 확인하지 않는 `SpawnOutput`을 호출하던 경계를 감사했다. spawn 실패 시 센서 질량과 자본이 사라지고, 설치가 끝난 뒤 input grams provenance도 폐기되어 제거 질량을 증명할 수 없었다.
- [x] Production current-format schema를 V15로 올리고 installed sensor별 facility/item/input operation/input commit/input source stack/embedded grams 기록을 추가했다. installed ID 집합과 embedded-mass record 집합은 exact bijection이어야 한다.
- [x] 설치 input outcome 게시 시 pending Sink owner의 exact source stack과 input grams를 installed record로 승격한다. acknowledgement 실패·복원 상태에서도 installed outcome과 embedded-mass record가 pending owner와 exact 일치해야 한다.
- [x] 제거를 `Prepared → OutputPublished` pending owner로 분리하고 facility/item/position/operation/reason/installation source stack/expected grams/output commit·quantity·grams를 저장한다.
- [x] output publication이 실패하면 installed sensor, acknowledged 상태, embedded mass와 pending owner를 그대로 유지한다. 재시도는 같은 owner를 재개하며 상태를 먼저 지우거나 item을 source warehouse로 순간이동시키지 않는다.
- [x] `IPhysicalItemSourcePublicationService.TryEnsureLooseOutputs`를 사용하는 전용 gateway가 센서 1개를 deterministic commit-tagged Loose Source로 게시한다. receipt output grams는 설치 시 보존한 embedded input grams와 exact 일치해야 한다.
- [x] removal operation에 installation source stack ID를 포함해 같은 시설을 여러 차례 설치→제거해도 과거 Source commit과 충돌하지 않게 했다. 같은 제거의 재시도만 동일 operation을 재사용한다.
- [x] `IPhysicalItemRestoreCandidateOutputQuery`를 Items 공용 계약으로 추가했다. Physical Items detached candidate가 commit-tagged stack의 commit/stack/item/quantity/grams/state/position/destination을 live publish 전에 immutable하게 제공한다.
- [x] `WorldItemStackRuntime`이 incoming detached repository의 output commit component를 stable commit·stack 순서로 인덱싱하고 commit별 exact stack 집합을 제공하며 commit/discard에서 candidate index를 함께 해제한다.
- [x] `ProductionBillsSaveSection`이 Prepared removal에는 incoming output 0개를, OutputPublished removal에는 exact commit·item 1개·expected grams·Loose·saved position·empty destination을 요구한다. missing/tampered output은 live Production state publication 전에 거부한다.
- [x] 설치 pending과 제거 pending의 동시 소유, installed record 누락, input receipt/source/mass 불일치, removal operation/commit/mass 불일치를 Production V15 local validation에서 fail-loud한다.
- [x] Work Orders save preflight도 committed-output query를 사용하도록 연결해 construction restitution의 0개·부분 exact subset·전부 exact mass와 tampered output을 검증한다. 체크포인트 87의 부분 restitution restore 공백을 닫았다.
- [x] production composition이 removal output gateway와 committed-output candidate query를 필수 등록한다. runtime은 null/legacy fallback 없이 두 권위를 생성자 주입받는다.
- [x] focused Production fixture가 acknowledgement fault 뒤 embedded 1,000g 보존, removal output-space 1회 실패 시 installed owner 유지, single Source publication, terminal second output `0`, prepared/published/missing output candidate를 검사하도록 작성됐다.
- [x] Runtime authority ratchet이 Production V15, stock-sensor direct SpawnOutput 금지, installed gram→removal gram 연결, Source gateway, output candidate registration/save preflight와 focused fault fixture를 강제한다. Items·Production·Economy·Runtime·Editor Roslyn compile exit `0`, 신규 meta GUID 중복 `0`, scoped diff error `0`이다.
- [x] Unity fresh import 후 Production Economy·Physical Item·Work Amount focused scenarios를 실행해 설치 acknowledgement fault, 제거 output-space fault, committed-output restore preflight와 Console Warning/Error `0/0`을 확인했다. 세 전체 focused suite와 Unity Console `0/0`이 같은 fresh import 세션에서 통과했다.
- [ ] 실제 센서 배송→설치→ack fault/save restore→제거 output-space fault→Loose output→AI haul→재설치→재제거 PlayMode에서 input/output grams, unique operation, second Source, orphan owner와 Floor Clutter가 모두 기대값인지 확인한다.
- [ ] stock-sensor panel 1개의 최종 grams, 시설 input/output buffer capacity, 25kg 운반 묶음, 설치/제거 WU·EWU·가격·수리·자동화 ROI와 6인 성장 노동을 전수 원장으로 승인한다.

이 체크포인트는 재고 센서의 **설치 embedded mass→제거 physical Source output 원자성**과 공통 committed-output restore 관측 권위를 current source에서 닫는다. Unity live와 최종 authored kg·버퍼·운반·WU/EWU·가격·ROI가 열려 있으므로 재고 센서 또는 전체 중량 밸런스 완료로 보고하지 않는다.

## 89. 구현 체크포인트: 파종 시설 파괴 WIP terminal loss·ecology owner 정리

상태: **Crop Plot V5·typed destroyed-WIP loss·last-known position·acknowledgement replay·ecology cleanup·focused fixture·Economy/Runtime/Editor Roslyn 컴파일 완료 / Unity live·최종 농업 질량 경제 대기**.

- [x] 기존 `SynchronizePlots`가 파괴된 경작지의 pending sow owner를 `Building=null` 상태로 무기한 보존하면서 입력 receipt를 acknowledge하거나 WIP를 회수·손실 귀속하지 못하던 경계를 감사했다.
- [x] 이미 FacilityBuffer에서 exact `Transfer`로 WIP에 들어간 종자·물·퇴비·연료를 파괴 순간 원래 창고로 순간이동시키지 않는 정책을 확정했다.
- [x] `CropWipTerminalDisposition.DestroyedWithPlotLoss`와 `PlotDestroyedLossPending` phase를 추가해 terminal operation/reason, exact input quantity와 exact input grams를 같은 sow owner에 저장한다.
- [x] terminal operation을 `crop-sow-wip-loss:{inputOperationId}`, reason을 `crop-sow-wip-destroyed-with-plot`로 결정론화하고 loss quantity/mass가 원 input과 exact 일치하지 않으면 fail-loud한다.
- [x] 파괴 감지 시 기존 pending Transfer receipt를 exact operation/commit/source/quantity/grams로 재검증한 뒤에만 loss phase를 게시한다. 별도의 두 번째 item debit이나 임의 output은 생성하지 않는다.
- [x] 실행 순서를 `plot destruction 감지 → destination release → terminal loss owner 게시 → input receipt acknowledgement → owner clear → ecology owner 제거 → plot state 제거`로 고정한다.
- [x] acknowledgement 실패 시 파괴된 plot state, terminal loss provenance와 pending item receipt를 보존한다. 다음 Tick 또는 current-format restore는 같은 commit acknowledgement만 재시도한다.
- [x] `OutcomePublished` 직후 파괴된 경우에는 이미 게시된 ecology after를 먼저 검증·acknowledge한 뒤 ecology owner를 폐기한다. 게시된 생태 결과를 loss branch로 다시 분류하거나 두 번째 Sow/compost를 실행하지 않는다.
- [x] Crop Plot current-format을 V5로 올리고 각 plot의 `lastKnownGridX/Y`를 저장한다. 파괴·복원 뒤 destination release가 `(0,0)`으로 순간이동하지 않고 실제 마지막 셀에서 수행된다.
- [x] `ICropEcologyService.AbandonPlot`을 추가해 plot state 제거와 crop ecology record 제거를 같은 terminal 경계에서 수행한다. 이미 없는 ecology owner는 idempotent no-op이다.
- [x] local restore validation이 empty/input/outcome/destroyed-loss phase별 materialsConsumed, ecology before/after, terminal fields를 분리해 검증한다. loss mass·quantity·operation·reason 변조를 거부한다.
- [x] incoming restore guard는 loss pending owner도 원 pending Transfer receipt와 양방향 조인한다. acknowledgement 전 owner 또는 receipt 한쪽만 있는 current-format save는 live publication 전에 실패한다.
- [x] focused real-repository fixture가 seed 1+water 2 exact commit, injected acknowledgement failure, serialized loss owner, valid incoming join, tampered loss grams 거부, acknowledgement-only replay와 pending receipt 제거를 검사한다.
- [x] Runtime authority ratchet이 Crop Plot V5, destroyed loss API, ecology cleanup과 focused fixture 누락을 실패시키도록 강화됐다.
- [x] current-source `DungeonStory.Economy`, `Assembly-CSharp`, `Assembly-CSharp-Editor` Roslyn compile exit `0`과 scoped diff error `0`을 확인했다.
- [x] Unity fresh import 후 Crop Physical Transactions와 Crop Plot Runtime focused verifier를 실행했다. destroyed sow/treatment loss replay를 포함한 fixture와 V7 save round-trip이 PASS했고 Console Warning/Error `0/0`이다.
- [ ] 실제 P23/P24에서 배송 완료 직후 시설 파괴·ack fault·save/restore를 실행해 second Transfer/Sow/loss `0`, pending owner/receipt orphan `0`, destination 순간이동 `0`, ecology orphan `0`과 Floor Clutter를 확인한다.
- [ ] 종자·물·퇴비·연료의 destroyed-WIP 손실이 건설 철거 악용이나 무상 재료 회수 경로를 만들지 않는지 최종 kg·WU/EWU·가격·6인 농업 폐쇄 루프에서 승인한다.

이 체크포인트는 파종 시설 파괴 시 **physical input WIP→명시적 질량 손실→receipt/ecology 수명 종료**를 current source에서 닫는다. Unity live와 최종 authored 농업 kg·버퍼·운반·생산/소비·WU/EWU·가격은 아직 열려 있다.

## 90. 구현 증거: 작물 처리제 live consumer·V7 저장 왕복

상태: **작물 처리제 구조 수직 슬라이스와 focused Unity/PlayMode 검증 완료 / authored kg·용기·BOM·EWU·6인 농업·다중 seed 미완료**.

- `CropTreatmentItemFeature`가 처리 종류, 적용 수량, required WU, 압력 감소량과 cooldown을 한 authored policy로 제공한다. 처리제 수치가 없는 아이템은 planner 후보가 될 수 없다.
- `CropPlotRuntime`이 UI 예약, exact `:treatment` destination 배송, `Treat` 작업 진행, physical Sink pending, package tare, ecology before/after, cooldown과 acknowledgement를 순서대로 소유한다. free `SoilDiagnostics` fungicide mutation은 제거했다.
- `CropTreatmentPhysicalOutbox`가 exact source stack·quantity·input grams·request fingerprint·commit과 tare output을 저장한다. acknowledgement fault/save replay는 두 번째 Sink나 두 번째 ecology outcome을 만들지 않는다.
- `CropPhysicalRestoreGuard`가 treatment owner와 incoming Sink receipt를 양방향 조인하고 missing/orphan/kind/reason/commit/fingerprint/source/quantity/grams mismatch를 live publication 전에 거부한다.
- 작물 시설 파괴 시 precommit 주문은 destination을 해제하고, input commit 뒤 주문은 `DestroyedWithPlotLoss` quantity/grams를 보존한 채 acknowledgement를 재시도한다. source warehouse restitution이나 순간이동은 없다.
- Unity `JsonUtility`의 null nested seed-lot 빈 객체화 때문에 발생한 자기 저장 복원 실패를 Crop Plot V7 `hasSeedLot` discriminator로 수정했다. flag가 없는 empty owner는 빈 객체를 provenance로 오인하지 않고, 실제 pending sow는 flag와 유효 seed lot을 모두 요구한다.
- 정적 `DungeonStory.Economy`, Runtime, Editor Roslyn compile exit `0`; Unity `CropPhysicalTransactionFixture.Run()` 반복 PASS; `crop-plot-runtime-latest.txt`는 `valid=true`, output containment work/quantity conservation true, outdoor harvest `0→6`, indoor phase `Growing`; Console Warning/Error `0/0`이다.
- 전역 `RuntimeAuthorityV18Validator`는 새 crop-treatment ratchet을 통과했지만, repository의 별도 save-count/version·architecture-report stale ratchet 때문에 전체 PASS가 아니다. 해당 전역 부채를 crop focused PASS로 숨기지 않고 후속 공통 검증 작업으로 남긴다.

이 증거는 작물 처리제 definition을 이제 **실제 live consumer가 있는 콘텐츠**로 분류할 수 있게 한다. 세 처리제의 최종 gram·용기/잔류물 lifecycle·BOM/WU/EWU/가격·6인 저장/운반·오염 위험과 다중 seed가 열려 있으므로 작물 처리제 밸런스 완료로 보고하지 않는다.

## 91. 구현 체크포인트: 개 사료 2개 레시피 exact 질량 보존

상태: **개 사료 단위 질량·두 생산 경로 보존 계약·builder 권위·결정론적 감사·포로 동물 급식 Sink focused 회귀 완료 / 전수 kg 창고·운반·EWU·가격·6인 생존망 재검증 대기**.

- [x] `feed:dog-food` 1개의 authored 의미를 포장 tare가 없는 혼합 사료 1회분으로 유지하고 단위 질량을 `550g → 525g`으로 교정했다.
- [x] `animal-rot 1×700g + grain 1×350g → dog-food 2×525g`이 입력·출력 각각 `1,050g`이 되도록 exact transform을 등록했다.
- [x] `meat 1×700g + grain 1×350g → dog-food 2×525g`도 입력·출력 각각 `1,050g`이 되도록 exact transform을 등록했다.
- [x] 두 recipe는 물·연료·포장·부산물·확률 출력이 없는 혼합 공정으로 확인했으며 loss를 `0g`, `PhysicalMassLossKind.None`으로 명시했다.
- [x] `ResourceEconomyAssetBuilder`가 기존 `550g`을 보존해 교정을 되돌리지 않도록 exact `525g` 권위 override와 두 recipe의 item·수량·gram topology fail-loud 검증을 추가했다.
- [x] 실제 `feed_dog_food.asset`을 `525g`으로 적용하고 Unity 재import·컴파일에서 오류를 확인하지 않았다.
- [x] recipe inventory의 reviewed-exact 계약 수를 `39 → 41`, mass-creation Critical을 `83 → 81`로 갱신했다. `dispositionMissing=159`는 두 recipe가 기존에 mass-creation 분류였으므로 이 슬라이스에서 변하지 않는 것이 정상이다.
- [x] semantic·primitive·transform·recipe CSV/리포트 6종을 연속 두 번 생성해 SHA-256이 모두 byte-identical임을 확인했다.
- [x] `CaptivityCircusDebugScenarios.RunAll(false)`를 실행해 포로 동물 급식의 exact 물리 Sink·outbox 회귀가 새 `525g` 권위에서 PASS함을 확인했다.
- [x] Unity fresh compile과 focused 감사 뒤 Console Warning/Error `0/0`을 확인했다.
- [ ] 일반 창고·시설 버퍼를 kg admission으로 전환한 뒤 개 사료의 `2~4회 생산분` buffer와 25kg actor의 운반 묶음을 실제 planner에서 검증한다.
- [ ] 음식·물 폐쇄 루프에서 개 사료가 사람 음식 비축·농업 원료·축산 사료와 경쟁하는 WU·EWU·가격·공간 효과를 다시 산정한다.
- [ ] 전수 mass-creation Critical `81`, missing disposition `159`, missing unit semantic item `51`을 후속 수직 슬라이스로 계속 줄인다.

이 체크포인트는 개 사료 두 생산 경로의 **단위 질량과 recipe 질량 보존**만 닫는다. kg 기반 창고 admission, 실제 AI haul, FacilityBuffer, EWU·가격과 6인 생존망이 열려 있으므로 전체 물리 중량 또는 밸런스 완료로 보고하지 않는다.

## 92. 구현 체크포인트: 접종 원목 exact 질량·RF13 실제 소비 경로

상태: **접종 원목 단위 질량·recipe 보존·builder 권위·결정론적 감사·RF13 FacilityBuffer 소비 PlayMode 완료 / kg 창고·AI haul·FacilityBuffer gram admission·EWU·6인 농업은 기존 전역 행에서 대기**.

- [x] `supply:inoculated-log`의 단위 의미를 별도 아이템 분리 없이 `1 inoculated cultivation log section`으로 교정했다. 한 treated-lumber bundle이 두 재배 구간을 만드는 현재 출력 수량과 일치한다.
- [x] 단위 질량을 `1,800g → 700g`으로 적용해 `treated-lumber 1×1,150g + cave-mushroom 1×250g → inoculated-log 2×700g`을 exact `1,400g → 1,400g` transform으로 닫았다.
- [x] clean water·wastewater·연료·확률 출력·포장 tare·공정 손실이 없는 `PhysicalMassLossKind.None` 계약을 등록했다.
- [x] `ResearchOverhaulContentAssetBuilder`에 exact `0.7kg` no-clobber override와 item/recipe gram topology fail-loud 검증을 추가했다.
- [x] reviewed-exact 계약 수를 `41 → 42`, mass-creation Critical을 `81 → 80`, mass-creation candidate를 `123 → 122`로 갱신했다. disposition missing `159`는 이 행의 이전 분류가 mass-creation이었으므로 변하지 않는다.
- [x] semantic·primitive·transform·recipe CSV/리포트 6종을 연속 두 번 생성해 모든 SHA-256이 byte-identical임을 확인했다.
- [x] `CropPhysicalTransactionFixture.Run()`에서 crop input WIP의 exact debit·acknowledgement fault·restore join·destroyed loss 공통 계약이 PASS함을 확인했다.
- [x] 실제 PlayMode `CropPlotDebugScenarios`가 RF13의 authored `cycleSupplyInputs`에서 접종 원목 1개를 요청하고, `700g` physical FacilityBuffer stack을 소비한 뒤 `Growing`으로 진입했다.
- [x] RF03·RF93의 기존 crop-treatment `Treat` source/asset drift는 두 scalar만 맞춰 managed-reference RID 변경 없이 정리했고, 5개 builder·7,219파일 재실행에서 `changes=0`을 확인했다.
- [x] Unity fresh compile, RF13 PlayMode와 no-clobber 실행 뒤 Console Warning/Error `0/0`, scoped diff-check error `0`을 확인했다.

이 체크포인트는 접종 원목의 **단위 질량·생산 보존·실제 RF13 소비 경로**를 닫는다. §83의 농업 FacilityBuffer `2~4회분` gram capacity, §91의 kg 창고·25kg AI haul, 최종 WU/EWU·가격·공간·6인 gross/net·다중 seed는 여전히 열려 있으므로 전체 농업·물류·밸런스 완료로 보고하지 않는다.

## 93. 구현 체크포인트: L02 상자더미 positive gram 권위·실제 입고·복원

상태: **L02 12,500g authored 권위·warehouse admission·current-format 복원·pickup 전 overfill 거절·focused PlayMode·builder no-clobber 완료 / 나머지 19개 저장시설의 positive gram 권위는 체크포인트 94에서 후속 완료 / FacilityBuffer·FacilityOutputBuffer·전수 kg 경제는 대기**.

- [x] 기존 L01 대형보관선반의 `25,000g / 2셀`을 기준으로 일반 저장 밀도 `12,500g/cell`을 도출하고, 1×1 L02 상자더미를 `12,500g`으로 확정했다. 과거 count `16`을 kg로 재해석하지 않았다.
- [x] `ModularFacilityAssetBuilder.GetStorageMassCapacityGrams("L02")`와 실제 L02 `BuildingStorageAbility.maxStoredMassGrams=12,500`을 하나의 authored 권위로 맞췄다. category는 `General`, `allCategories=false`를 유지했다.
- [x] positive gram 권위에서는 `WarehouseInventory.GetAcceptableQuantity(itemId, quantity)`가 legacy count 경로를 사용하지 않는지 고정했다. 빈 L02가 count metadata `16`보다 많은 접종 원목 `17개`를 질량상 허용하는 anti-clamp 회귀를 추가했다.
- [x] 대표 exact lot으로 `supply:inoculated-log 700g`을 사용해 18개 요청을 `17개=11,900g`으로 부분 승인하고, `WarehouseMassAdmissionService` token·reserved grams·physical Stored stack·commit receipt를 exact 일치시켰다.
- [x] 입고 뒤 남은 `600g`에는 다음 `700g` 한 개를 예약할 수 없으며 `WarehouseMassCapacityUnavailable`로 실패하고 기존 수량·질량·reservation을 변경하지 않는지 검증했다.
- [x] current-format Physical Items capture/restore 뒤 exact stack ID, destination, 수량 `17`, stored `11,900g`, remaining `600g`과 다음 admissible quantity `0`이 동일함을 검증했다.
- [x] `ModularFacilityDebugScenarios`가 실제 `Facility.Initialization`으로 L02를 생성해 `HasMassCapacityAuthority=true`, `MaxMassGrams=12,500`, legacy metadata `MaxCapacity=16`, 초기 physical stock `0`을 투영하는지 검증했다.
- [x] 실제 PlayMode에서 production `WorldItemWarehouseService.SpawnItemStock`을 통해 L02에 접종 원목 `17×700g`을 reserve→publish→commit하고, receipt `11,900g`과 runtime inventory를 대조했다.
- [x] 같은 PlayMode checkpoint를 복원한 뒤 추가 접종 원목 1개를 Loose로 생성하고 L02 하나만 노출한 실제 `WorldItemHaulPlanningService` preview/reserve를 실행했다. 둘 다 pickup 전에 실패했으며 Loose 수량 `1`, reserved quantity `0`, carry `0→0`, inbound grams `0`, stored grams `11,900`을 유지했다.
- [x] focused artifact `Artifacts/QA/l02-mass-admission-playmode-report.txt`가 `RESULT=PASS; failures=0`, captured Console Error/Warning `0/0`을 기록했다.
- [x] `PhysicalStockQueryV18DebugScenarios`와 `ModularFacilityDebugScenarios`를 Unity current assembly에서 재실행해 각각 PASS했고 Console Error/Warning `0/0`을 확인했다.
- [x] 5개 content builder·7,219파일 no-clobber를 재실행해 `changes=0`을 확인했다. L02 mass authority는 builder 재생성 뒤에도 유지된다.
- [x] 전역 census에서 L01/L02를 제외한 positive-count `BuildingStorageAbility` 19개를 체크포인트 94의 exact authority 표대로 positive gram으로 전환했다. M08 장기보관함과 P1 방어 창고 두 개를 포함하며, Q03 category 의미는 mass와 분리해 변경하지 않았다.
- [ ] `FacilityBuffer`와 `FacilityOutputBuffer`는 warehouse token을 재사용하지 않고 각각 처리량 기반 `2~4회분` gram capacity, exact reservation·save/restore·output-space 대기를 구현한다.
- [ ] 기존 broad `physical-item-logistics-playmode-report.txt`의 제작 출력·원정·construction-order 공식 restore 실패 18건을 별도 회귀 결함으로 닫는다. focused L02 PASS로 전역 물류 PASS를 대신하지 않는다.

이 체크포인트는 일반 저장시설 하나의 **authored gram 용량→실제 production 입고→물리 복원→planner pickup 전 거절**을 닫는다. 나머지 19개 저장시설의 authored gram 배치는 체크포인트 94에서 닫았지만, 대형 사체 전용 운반·시설 입력/출력 버퍼·전체 item kg·물류·EWU·가격·6인 폐쇄 루프와 broad 물류 실패가 열려 있으므로 전체 창고·운반·밸런스 완료로 보고하지 않는다.

## 94. 구현 체크포인트: positive-count 저장시설 21개 gram 권위 전수 배치

상태: **21/21 authored gram 권위·writer 연결·Modular runtime 투영·M08/P1 특수 정책·결정론적 manifest·fresh compile/no-clobber 완료 / 대형 사체 전용 운반·Q03 archive category·FacilityBuffer는 별도 대기**.

- [x] positive-count `BuildingStorageAbility` 전수 집합을 21개로 고정했다. L01/L02 외 19개는 `0 또는 필드 누락 → positive long grams`로 전환했고, 각 에셋에는 질량 필드가 정확히 하나만 존재한다.
- [x] 1셀 일반·식품·의복·표본 storage는 `12,500g`, 1셀 무기 storage는 `25,000g`, L01 2셀 General은 `25,000g`을 사용한다. footprint와 authored count를 kg로 재해석하지 않았다.
- [x] Mana 특화 storage는 현재 최대 authored Mana 단위 `750g`과 기존 count metadata를 결합해 M01 `13,500g`, L07 `15,000g`, M02 `27,000g`으로 두었다. Q04는 1셀 일반 밀도 `12,500g`을 사용한다.
- [x] M08 장기보관함은 `Biological/count 8/12,500g/restricted`, P1 보급창고 1802는 `Ammunition/count 24/25,000g/all-category`, 정비대 1803은 `General/count 12/25,000g/restricted`로 고정했다.
- [x] `ModularFacilityAssetBuilder.GetStorageMassCapacityGrams`, `SurgeryContentAssetBuilder.OrganStorageMassCapacityGrams`, `P1DefenseFacilityAssetBuilder.OperationalStorageMassCapacityGrams`가 실제 writer와 focused validator가 공유하는 source authority다. 에셋-only 수동 값으로 남기지 않았다.
- [x] `PhysicalStockQueryV18DebugScenarios`가 exact 21-row path/category/count/gram/all-category/writer 표와 전체 positive-count census를 양방향 검증한다. 누락·중복·0g·writer divergence는 fail-loud한다.
- [x] `ModularFacilityDebugScenarios`가 모든 실제 runtime warehouse 인스턴스에서 `HasMassCapacityAuthority=true`와 positive `MaxMassGrams`를 검증하고 operational artifact에 `storageMassGrams` 열을 기록한다.
- [x] `Artifacts/QA/v27-storage-mass-authority.txt`를 두 번 생성해 SHA-256 `1D77C4EA3D8011561EDE0010EBB77E80432A87ED6E256CC5141CDA4A7461E214` byte identity를 확인했다. 헤더는 `authorities=21; positiveCount=21; positiveGram=21`이다.
- [x] current-source `Assembly-CSharp-Editor.dll`을 fresh compile해 `2026-08-25T04:25:15Z / 8,096,256 bytes`로 갱신한 뒤 Physical Stock·Modular·Surgery·P1 Defense를 재실행했다. 모두 PASS, Console Warning/Error `0/0`이다.
- [x] M08을 fresh Surgery writer로 한 번 정렬한 뒤 5개 builder·7,219파일 no-clobber를 재실행해 `changes=0`을 확인했다. stale project assembly로 나온 첫 실패 결과는 증거에서 제외한다.
- [ ] `wild:carcass:rune_deer 22kg`, `wild:carcass:moss_boar 28kg`, `dark:humanoid_corpse 28kg`는 live dedicated transport가 없고 28kg all-category warehouse도 없다. 일반 창고에 암묵 예외를 넣지 말고 single-leg drag/dedicated 분류와 D03 exact local input buffer를 구현한다.
- [ ] Q03 연구용책장의 General storage와 별도 archive ability가 Knowledge/Blueprint 중 무엇을 물리 보관하는지는 mass 필드와 분리해 결정한다. 이번 슬라이스에서 category를 몰래 변경하지 않았다.
- [ ] `FacilityBuffer`·`FacilityOutputBuffer`의 처리량 기반 `2~4회분` gram capacity, exact reservation·save/restore·`WaitingForOutputSpace`는 다음 수직 슬라이스에서 구현한다.

이 체크포인트는 기존 count-only 공간 의미를 제거하지 않으면서 **모든 현재 창고 정의에 positive gram admission 권위를 배치하고 실제 runtime까지 투영한 것**만 닫는다. 18~28kg 사체의 운반·보관·도축, FacilityBuffer, broad 물류 18개 실패, kg-aware EWU·가격·공간·6인 생존망이 열려 있으므로 전체 중량·운반·밸런스 완료가 아니다.

## 95. 구현 체크포인트: Production 입력 FacilityBuffer gram admission 수직 슬라이스

상태: **production input의 공용 positive-gram profile/token·exact claim·pickup/cancel·WIP consume·save-restore와 전수 owner 분류 manifest 완료 / 나머지 FacilityBuffer·FacilityOutputBuffer 이관 진행 중**.

### 95.1 구현 전 구조 계약

| 항목 | 확정 계약 |
|---|---|
| 콘텐츠 정의 | `ProductionRecipeSO.Inputs`의 exact item·quantity와 `IPhysicalItemMassQuery`의 item/instance/component gram이 유일한 질량 원본이다. |
| 런타임 상태 | `ProductionInputDestinationClaimRuntime`이 활성 bill 전체의 claim/profile 쌍을 원자적으로 소유한다. `ProductionBillRecord.prefetchBatchCount`, destination에 묶인 실제 world stack, pickup-commit된 `HaulDeliveryIntent`가 입력 버퍼 점유의 유일한 원본이며 별도 count mirror를 만들지 않는다. |
| 명령 | `ProductionInputLogisticsService`는 일반 `items.RequestDelivery`를 호출하되, 목적지의 공용 `FacilityBufferCapacityProfile`과 exact-lot admission token이 split/retarget 전에 반드시 승인되어야 한다. 구형 호출자 작성 상한 API `RequestDeliveryWithinMassCapacity`의 production live caller는 0이다. |
| 조회 | 공용 `FacilityBufferPhysicalOccupancyQuery`가 destination-bound world lot과 pickup-commit carried lot을 final destination/physical carrier record로 조인해 exact gram을 합산한다. presentation count나 저장 DTO는 입력으로 받지 않는다. |
| 식별자 | destination은 저장된 canonical `production:{billId}`를 그대로 사용한다. 이름·좌표·instance ID fallback을 만들지 않는다. |
| 저장 | capacity는 recipe input gram과 persisted prefetch count에서 재계산해 claim과 함께 restore candidate로 원자 게시한다. 물리 stack과 committed haul intent는 기존 current-format authority가 저장하며 새 occupancy/capacity mirror DTO는 만들지 않는다. |
| 의존성 | Economy는 public Items mass/query port만 사용한다. Production aggregate의 output RNG·WIP receipt·exact-once owner는 변경하지 않는다. |
| 실패 정책 | non-canonical ID, 0 이하 capacity, overflow, unknown item, missing carried physical lot, arithmetic overflow는 요청 전 fail-loud한다. 일부 source만 destination으로 retarget하지 않는다. |
| 전환 범위 | production input과 power-fuel 두 owner만 공용 admission으로 이관했다. 수술·건설·농업·연구 등 다른 FacilityBuffer는 manifest에 `remaining`으로 남기며 무제한이 안전하다고 간주하지 않는다. |
| 검증 | 1~3 batch, 2회분 최소 capacity, 최대 3회 prefetch, destination의 다른 item 점유, pickup 전·후, consume/cancel/release, current-format restore, duplicate request, exact 1g boundary를 검증한다. |

### 95.2 용량 공식과 불변식

```text
cycleInputMassGrams
= Σ GetExactUnitMass(input item/lot) × cycleInputQuantity

capacityBatchCount
= clamp(max(2, persistedPrefetchBatchCount), 2, 3)

maxInputBufferMassGrams
= cycleInputMassGrams × capacityBatchCount

pendingMassGrams
= destination-bound physical world lot grams
 + pickup-committed carried lot grams
```

- 현재 production prefetch 권위가 최대 3회이므로 이 슬라이스는 `2~3회분`을 사용한다. 계획 전체의 `2~4회분` 상한을 이유 없이 4회로 부풀리지 않는다. 실제 p95 clearance가 3회를 요구하면 3회, 4회를 초과하면 capacity 확대가 아니라 물류 Critical이다.
- 접종 원목 생산의 대표 입력은 treated lumber `1,150g` + cave mushroom `250g` = `1,400g/cycle`이다. prefetch 3회에서 capacity는 exact `4,200g`이다.
- 이미 목적지에 있거나 배송 예약된 모든 input item을 합산한다. item별 count 상한을 따로 두어 다른 재료가 같은 gram 공간을 초과 점유하게 하지 않는다.
- pickup 전 source destination commitment와 pickup 후 carried commitment는 같은 질량을 정확히 한 번만 센다.
- capacity 초과 요청은 source quantity·destination·lease·carry·intent를 바꾸기 전에 실패한다.
- source retarget은 전체 loose/stored slice를 먼저 preflight하고 split ID까지 준비한 뒤 기존 physical record를 제자리 전환한다. 원본을 먼저 차감하고 spawner 결과에 의존하던 `RequestLoose`/`RequestStored` 경로는 제거하며 partial request를 정상 결과로 허용하지 않는다.
- WIP input commit으로 FacilityBuffer 물리가 제거되면 pending mass도 같은 gram만큼 감소한다.
- bill cancel/release와 valid restore 뒤 동일 물리 상태는 동일 pending/capacity gram을 재생성한다.
- claim을 폐기하는 모든 production bill 종료 경로는 먼저 destination의 active haul intent를 전수 preflight한다. pickup 전 lease는 해제하고 pickup-commit cargo는 해당 actor의 현재 cell에 물리 반환한 뒤에만 intent·claim·bill ownership을 종료한다. 반환 실패 시 claim과 bill을 유지하고 typed failure로 중단한다.

### 95.3 구현·증거 체크리스트

- [x] `IWorldItemStackRuntime`에 committed haul destination exact gram query를 추가하고 item/instance/component lot을 검증한다.
- [x] `ProductionInputDestinationClaimRuntime`이 활성 bill 전체의 exact claim과 positive-gram profile을 한 candidate로 구성하고 lifecycle service를 통해 원자 교체·복원한다.
- [x] `ProductionBillRuntime`이 cycle input mass와 `2~3` capacity batches를 계산해 공용 profile을 게시하고, `ProductionInputLogisticsService`는 공용 admission이 강제되는 일반 exact delivery만 요청한다.
- [x] 구형 `RequestDeliveryWithinMassCapacity` production live caller를 0으로 만들고, 공용 profile/token·schema revision·일반 delivery caller를 정적 ratchet으로 고정한다.
- [x] exact `4,200g` 접종 원목 input buffer에서 허용 경계와 `+1 unit` pickup 전 거절을 검증한다.
- [x] pickup 전 world commitment와 pickup 후 carried commitment가 같은 gram을 한 번만 점유하는지 검증한다.
- [x] input consume, bill cancel/release, save/restore 뒤 reservation·mass leak 0을 검증한다.
- [x] current-source Unity compile, FacilityBuffer Mass Admission·Physical Stock·Production Economy·Industrial Infrastructure static suite와 실제 AIHaul PlayMode를 통과했다. `Artifacts/QA/production-input-buffer-mass-playmode-report.txt` SHA-256은 `A3902852796480CA6F6F253CF64E415E0904915913D3AFE22148B450B993466A`, Console Warning/Error는 `0/0`이다.
- [x] 다른 FacilityBuffer owner 전수 manifest를 생성해 migrated/remaining/orphan/bypass를 기록한다. Combat/Apparel 공용 output과 current handler registry를 반영한 fresh 분류는 input owner `39`(`migrated=3`, `remaining=36`), output owner `6`(`migrated=4`, `remaining=2`), direct bypass `5`, orphan API `1`, generic delivery invocation `59/39 files`, unclassified `0`이다. 두 번째 생성의 byte·mtime 변화는 `0`; CSV SHA-256은 `92BCD44BE2EE9278BFA1241A9164D644A54A6F53F0E41EE3AD2D3CCEDBF3EE43`, TXT SHA-256은 `7ADF8A780757D3F70B4CB8F1623391F90AA85B8BEBE7228139D66C317095D9FA`다. fresh Unity compile, classification gate와 Console Warning/Error `0/0`을 통과했다.

이 체크포인트는 production 입력 한 경로의 capacity admission만 닫는다. FacilityOutputBuffer, 비-production FacilityBuffer, 대형 사체 dedicated transport, 전체 item kg·EWU·가격·6인 생존망을 완료한 것으로 간주하지 않는다.

## 96. 구현 체크포인트: 공용 FacilityBuffer admission·발전기 연료 live 수직 슬라이스

상태: **공용 positive-gram profile/token·exact-stack managed route·발전기 claim/profile 복원·운반 중 저장복원·terminal close·실제 AIHaul/소비/발전·production 공용 profile cutover·결정론적 owner manifest 완료 / 나머지 owner/output/bypass/orphan 이관 대기**.

### 96.1 구현 전 구조 계약

| 항목 | 확정 계약 |
|---|---|
| 콘텐츠 정의 | 발전기 `BuildingPowerAbility.fuelItemId`와 `IPhysicalItemMassQuery`의 exact item/instance gram이 질량 원본이다. 이 슬라이스는 unit gram·연료 소비량·발전량을 변경하지 않는다. |
| 런타임 상태 | destination-bound physical world lot과 pickup-commit carried lot이 점유 질량의 원본이다. `FacilityBufferMassAdmissionService`는 reservation/receipt lifecycle만 소유하며 별도 저장 occupancy counter를 만들지 않는다. |
| 명령 | managed `power:{nodeId}` 배송은 exact-stack `TryRequestStackDelivery`가 공용 admission을 먼저 예약한 뒤 하나의 split/retarget transaction으로 게시한다. raw `TryRouteStackToDestination`은 거부한다. |
| 조회 | `FacilityBufferPhysicalOccupancyQuery`가 world destination과 haul intent의 final destination, carrier-owned carried physical record를 조인해 exact gram을 계산한다. |
| 식별자 | destination은 `power:{persistentNodeId}`, owner domain은 `infrastructure.electrical`, capacity revision은 authored schema `1`이다. topology epoch·GameObject ID·시간을 ID로 사용하지 않는다. |
| 저장 | Physical Items, Haul Delivery Intent, Electrical Network와 claim/profile restore candidate를 기존 전체 save-section transaction으로 함께 복원한다. profile revision은 topology `SourceVersion`과 무관하게 동일하다. |
| 의존성 | Infrastructure는 public Items claim/profile/admission/release port를 사용하고 Items는 Infrastructure를 참조하지 않는다. Economy production release도 같은 terminal service를 사용한다. |
| 실패 정책 | profile 누락, overflow, stale revision, source mutation, downstream retarget 실패와 physical recovery drop 실패는 publication 전 또는 owner 보존 상태로 fail-loud한다. |
| 전환 범위 | 이 체크포인트 자체는 `power:`와 production input 두 owner를 닫았다. 체크포인트 98의 equipment repair까지 합친 current source는 migrated 3, 다른 input 36개이며 output 5개, bypass 5개와 orphan 1개는 자동 승인하지 않는다. |
| 검증 | 공용 token/restore 단위 suite, topology revision 회귀, raw route 차단, 실제 AIHaul pickup, carried checkpoint restore, 입고·소비·발전, terminal close, manifest 2회 byte identity를 요구한다. |

### 96.2 구현·증거 체크리스트

- [x] 공용 `FacilityBufferCapacityProfile`, exact-lot admission token, repository-derived world/carried occupancy와 bounded commit/rollback/retire lifecycle을 추가했다.
- [x] owner-scoped claim/profile restore는 Begin에서 publish·rollback revision과 candidate를 모두 준비하고 Publish에서는 실패 가능 계산 없이 field/map swap만 수행한다.
- [x] `power:{nodeId}`가 exact LiveBuilding claim과 `fuel unit gram × 4회분` positive capacity를 게시하며 profile 없는 managed power destination은 fail-loud한다.
- [x] raw low-level `power:` retarget을 차단하고 exact-stack request가 admission 예약 뒤 하나의 deterministic split/retarget transaction을 사용하며 downstream 실패 시 routed token과 stack mutation을 함께 rollback한다.
- [x] 공용 terminal release가 미픽업 lease, committed carried cargo, 이미 deposited buffer를 구분한다. drop/publication 실패 시 claim·intent·cargo ownership을 보존하고 성공 후에만 종료한다.
- [x] 발전기 topology 교체·철거 전에 retired destination을 terminal close하고, restore staging에서는 live world release를 실행하지 않는다.
- [x] capacity revision을 transient topology epoch가 아닌 authored schema revision `1`로 고정하고 서로 다른 topology source version의 live/restore profile ID·max gram 일치를 검증했다.
- [x] 실제 AIHaul이 마나 수정 `350g`을 pickup한 상태에서 전체 save-section checkpoint를 복원해 동일 operation·carried gram을 보존하고, 이후 exact 입고·연료 소비·발전과 destination intent `0`을 확인했다.
- [x] current-source Unity compile 뒤 FacilityBuffer Mass Admission·Physical Stock·Industrial Infrastructure·Production Economy suite와 focused PlayMode가 PASS했고 Console Warning/Error `0/0`이다. 정규화 보고서는 `Artifacts/QA/industrial-power-fuel-buffer-playmode-report.txt`, SHA-256 `41CD376E62EA60D556DEF18382F38E1482FDF65B3FFF9F639593F997479CE0CC`이며 연속 두 실행의 byte·mtime 변화가 없다.
- [x] latest owner manifest는 production input·power·equipment repair를 `migrated`로 분류해 input `migrated=3/remaining=36`, output `remaining=5`, bypass `5`, orphan `1`, unclassified `0`을 유지한다. CSV SHA-256 `4578FAA4E4D1310484E2CB966E4FCD7BCECC17A99E1BB322E765DA74421B55EE`, TXT SHA-256 `CAAC0A58031E9C50926161A0A6F5858BAFBB018C63D14374E26006B7CBB56A31`이며 2회 생성 byte·mtime 변화가 없다.

이 체크포인트는 공용 admission 구조와 발전기 연료 **한 owner의 요청→실제 운반→운반 중 저장복원→입고→소비→발전→철거 종료**를 닫는다. §93·§94의 전역 FacilityBuffer/OutputBuffer 행, 나머지 owner·직접 우회·orphan, 최종 kg·EWU·가격·6인 생존망은 열려 있으므로 시설 버퍼나 전체 중량·밸런스 완료로 보고하지 않는다.

## 97. 구현 체크포인트: 표준 생산 출력 prepared-batch·planned gram admission

상태: **current-source 출력 경로 감사 완료 / planned gram token과 canonical line source schema green / 351 asset backfill·key-addressed 결과·prepared WIP·원자 publication·복원 양방향 조인 구현 대기**.

### 97.1 구현 전 구조 계약

| 항목 | 확정 계약 |
|---|---|
| 수직 슬라이스 | custom output handler가 없는 표준 generic physical recipe 한 family만 먼저 이관한다. combat/apparel/workwear/certified-seed 출력은 `remaining`으로 유지한다. |
| 결과 권위 | 작업 완료 때 `PreparedProductionOutputBatch`를 정확히 한 번 결정·저장한다. item별 group으로 line identity를 지우지 않고 `outputLineId`, role, item, quantity, component/quality, resolved roll, exact grams를 보존한다. |
| 식별자 | authored recipe 안의 canonical `outputLineId`가 필수다. runtime 배열 index, display name, GameObject ID와 시간 fallback을 금지한다. |
| 확률 | `rootSeed + billId + cycleSequence + recipeId + outputLineId + rollKind` key-addressed sequence를 사용한다. 공유 순차 `economy:production` stream 소비량에 결과가 의존하지 않는다. |
| 역할 | `Main`, `Byproduct`, `ReturnedPackaging`, `RecoverableWaste`, `DeclaredLoss`를 분리한다. 앞의 네 역할만 물리 output gram reservation에 포함하고 loss는 receipt에만 남긴다. |
| 용량 권위 | facility output destination 하나가 positive `FacilityBufferCapacityProfile`을 소유한다. reachable standard recipe의 최대 physical branch를 기준으로 최소 2회분, p95 clearance 필요량, 최대 4회분 사이에서 authored revision을 확정한다. active bill 변화로 profile을 축소하지 않는다. |
| 예약 | 존재하지 않는 출력을 existing-stack exact-lot token에 가장하지 않는다. 별도 planned-output request/token을 사용하되 source-lot token과 같은 destination reserved-gram ledger에서 경쟁한다. trusted planner의 immutable output subject를 공용 mass query가 재계산한다. |
| 대기 | 용량 부족은 authoritative `ResolvedWaitingForOutputSpace`다. 물리 output은 0, input 재소비·RNG 재굴림·일반 바닥 drop은 0이며 공간 신호 또는 tick에서 fresh transient token만 다시 요청한다. |
| publication | full batch를 preflight하고 한 repository transaction으로 0개 또는 exact full stack 집합만 게시한다. stack limit에 따른 여러 stack은 허용하되 일부 line/unit만 게시된 save 상태는 금지한다. |
| 저장·복원 | WIP owner와 physical output commit 후보를 operation/commit/outcome fingerprint/line/stack/item/quantity/grams/state/destination까지 양방향 조인한다. planned token은 저장하지 않는다. |
| terminal | cancel·facility destroy는 waiting token을 해제한다. 이미 full physical publication된 output은 삭제하지 않는다. facility destroy는 stock을 기존 시설 cell의 Loose/recovery owner로 원자 전환한 뒤 profile을 retire한다. |
| 후속 물류 | 첫 슬라이스의 최종 경계는 `FacilityOutputBuffer → distribution → Loose → 실제 AIHaul → kg warehouse admission`이다. downstream FacilityBuffer로의 수량 기반 이동은 별도 remaining으로 둔다. |

### 97.2 준비 batch와 fingerprint

```text
outcomeFingerprint
= schemaVersion
 + billId/cycleSequence/recipeId
 + recipeDefinitionDigest
 + outputLineId ordinal 순서의 role/item/quantity/components/quality/mass/roll

admissionFingerprint
= outcomeFingerprint
 + destinationId
 + profileOwnerId
 + capacityRevision
 + totalReservedMassGrams

batchCommitId
= production-output-batch:{billId}:{cycleSequence:D8}:{outcomeFingerprint}
```

허용 위상:

```text
Unresolved
→ ResolvedWaitingForOutputSpace
→ PublicationPrepared
→ PhysicalBatchCommittedPublicationPending
→ Completed
```

- `PublicationPrepared` restore에는 physical batch가 `0 또는 exact full`이어야 한다. 0이면 waiting으로, full이면 acknowledgement-only phase로 정규화한다.
- `PhysicalBatchCommittedPublicationPending`은 fresh reservation·재publication을 금지하고 aggregate acknowledgement와 cycle advance만 수행한다.
- 동일 full batch replay는 exact 검증 후 재사용하고 conflicting stack은 삭제하지 않고 fail-loud한다.

### 97.3 구현·증거 체크리스트

- [x] 현재 출력 경로를 감사해 cycle-start count reservation, 공유 순차 RNG, item grouping에 의한 line identity 손실, `FacilityOutputBuffer` 직접 spawn, durable waiting phase 부재와 WIP↔physical output restore join 부재를 source 위치별로 확정했다.
- [ ] 모든 표준 physical output definition에 canonical `outputLineId`와 role을 authored backfill하고 누락·중복·비정규 ID를 content capture에서 거부한다.
- [x] 공유 순차 output RNG를 key-addressed 결과 결정으로 교체하고 output list 순서 shuffle·무관 RNG draw 후 동일 outcome/fingerprint를 증명한다.
- [x] `PreparedProductionOutputBatch`와 위상·outcome/admission fingerprint·batch/line commit ID를 current-format Production save 권위에 추가한다.
- [x] source-lot token과 같은 reserved-gram ledger를 사용하는 별도 planned-output request/token/receipt를 공용 FacilityBuffer admission에 추가한다.
- [ ] facility-level output claim/profile을 reachable standard recipe의 최대 physical branch와 `2~4회분` 공식으로 게시·복원·retire한다.
- [ ] output 공간이 1g 부족하면 input 재소비·결과 재굴림·물리 spawn 없이 `ResolvedWaitingForOutputSpace`로 유지하고 공간 확보 후 같은 결과를 재개한다.
- [ ] main/byproduct/returned packaging/recoverable waste를 한 transaction으로 exact full batch publication하고 line별 실패 주입에서 전체 rollback한다.
- [x] partial/missing/extra/wrong item·mass·destination·fingerprint/orphan output을 live publish 전에 거부하는 WIP↔physical candidate 양방향 restore join을 구현한다.
- [ ] cancel 전·waiting·physical commit 후와 facility destroy 전·후의 token·output·profile ownership을 보존적으로 종료하고 순간이동·삭제·일반 통로 drop을 0으로 만든다.
- [ ] 실제 `FacilityOutputBuffer → distribution → Loose → AIHaul → kg warehouse`와 mid-haul save/restore에서 수량·질량·commit 보존을 PlayMode로 증명한다.
- [ ] migrated generic 경로의 count reservation·direct output spawn production caller를 0으로 ratchet하고 다른 4 output owner와 downstream FacilityBuffer는 `remaining`으로 유지한다.
- [ ] focused static/PlayMode, 동일 입력 artifact 2회 byte·mtime identity, Console Warning/Error `0/0`을 확보한 뒤에만 manifest의 표준 production output 행을 `migrated`로 바꾼다.

이 체크포인트는 표준 generic 생산 출력 한 family의 **결과 확정→gram 공간 대기→원자 physical batch 게시→저장복원→창고 운반**만 닫는다. custom output 4개, downstream FacilityBuffer, 전수 확률·부산물·포장 authored data, EWU·가격·6인 생존망과 다중 seed는 별도 열린 범위다.

2026-08-25 기반 증거: planned output은 repository stack ID를 가장하지 않고 immutable mass subject를 `IPhysicalItemMassQuery`로 재계산하며, 기존 source-lot과 동일한 `reservedByDestination` 장부를 사용한다. focused static scenario는 source `4,000g` + planned `4,000g`, 정확한 `1g` overflow, release, profile/mass revision drift, `1g` receipt 변조, exact replay와 conflicting replay 거부를 통과했다. canonical output source schema는 7개 생성 지점을 새 생성자로 전환했고 lowercase `output:` grammar와 duplicate line 거부 회귀를 통과했다. 기존 351개 asset의 714 scalar backfill과 live producer 연결은 아직 열려 있으므로 첫 checklist 행과 전체 출력 이관은 완료로 세지 않는다. Fresh DLL: `Assembly-CSharp.dll 7,938,048 bytes @ 2026-08-25T09:37:47Z`, `Assembly-CSharp-Editor.dll 8,189,440 bytes @ 2026-08-25T09:41:09Z`; `FacilityBufferMassAdmissionDebugScenarios`와 `ProductionEconomyDebugScenarios` PASS, Console Warning/Error `0/0`.

### 97.4 prepared output exact provenance-lot 라우팅·양방향 outbox

`FacilityOutputBuffer`의 완료 output을 기존 `destinationId + itemId + quantity` API로 Loose에 다시 spawn하지 않는다. 그 API는 서로 다른 batch·line·quality·component를 구분하지 못하고 partial spawn에서 provenance와 unique identity를 잃는다. prepared output은 다음 하나의 durable 상태열을 사용한다.

```text
RoutingSelectionPrepared
→ PhysicalRouteCommittedAckPending
→ EconomyRouteApplied
→ RoutableLoose
→ AIHaulReserved/Carried/Stored
```

- Economy routing owner가 `batchCommitId + lineCommitId + routedOffset + target + quantity + grams`로 route operation을 먼저 영속한다. 재시도는 새 target을 선택하지 않고 같은 request fingerprint만 재사용한다.
- RepeatCount 마지막 cycle도 outstanding routing batch·physical pending·ack pending이 하나라도 있으면 bill과 route policy owner를 제거하지 않는다. 완료 bill은 작업 대상에서 제외된 terminal routing owner로 남고, 양쪽 pending 0과 buffer drain을 증명한 뒤에만 input destination release·claim revoke·bill retire를 원자 순서로 수행한다.
- Items route port는 schema-2 publication provenance의 batch·line·origin stack ordinal과 business component fingerprint를 검증한다. whole stack은 identity를 유지하고, partial generic/stateful stack은 source와 child의 unit range를 겹침·누락 없이 결정론적으로 분할한다. unique item partial은 거부한다.
- post-publication split에는 `item-state:prepared-output-route-slice` custody component를 사용한다. 이 component는 `affectsStacking=true`이며 batch·line·origin stack·unit range·exact grams·route operation/commit·custody phase를 가진다. 물류 metadata는 gameplay mass에서 제외하지만 haul/merge signature에는 포함한다.
- physical mutation은 source before-image, source remainder, routed child, custody component와 Items pending outbox를 하나의 transaction으로 commit/rollback한다. `IWorldItemSpawner`의 item-only merge나 사후 stack 추측을 사용하지 않는다.
- 순서는 `Items PhysicalPending commit → Economy receipt 적용 → Items acknowledgement/Routable 공개`다. Economy 적용 전 Loose slice는 AI·conveyor·warehouse가 볼 수 없으며, acknowledgement 실패는 같은 receipt만 재시도한다.
- routing save와 staged PhysicalItems는 batch·line·range·item·quantity·grams·component fingerprint·source/target·phase를 양방향 join한다. orphan, missing, extra, overlap, gap, duplicate, wrong 1g와 applied/pending 조합 불일치는 publish 전에 전체 restore를 실패시킨다.
- generic `ReleaseDestination`, conveyor begin-transit, direct warehouse request와 legacy distribution은 prepared publication/custody marker를 만나면 typed fail-closed한다. 새 exact port 밖에서 provenance stack을 이동·병합·분할할 수 없다.
- AI pickup과 partial deposit도 같은 range-partition primitive를 사용한다. cancel-before-pickup은 lease만 해제하고, Downed/Dead는 carried slice와 custody를 현재 위치에 보존하며, restore는 exact stack/signature/range와 join한다.
- drained replay proof는 무한 tombstone으로 저장하지 않는다. 양쪽 pending 0과 completed save checkpoint가 증명된 receipt만 deterministic watermark/GC 계약으로 제거한다. 단순 max-N eviction은 금지한다.

#### 97.4 구현·증거 체크리스트

- [x] split-aware route-slice custody component와 canonical codec을 추가하고 서로 다른 batch·line·range가 merge되지 않음을 증명한다.
- [x] whole·partial generic/stateful exact split/retarget transaction에서 quantity·grams·business components·unit range 합을 보존하고 unique partial을 거부한다.
- [x] Items pending route outbox의 same-operation replay·conflict·rollback과 acknowledgement-only 재개를 구현한다.
- [x] Economy routing owner에 durable selection·physical receipt 적용·applied watermark·terminal bill-retire gate·checkpoint-safe GC를 구현한다.
- [x] routing save와 staged PhysicalItems의 orphan/missing/extra/overlap/gap/duplicate/1g/phase 양방향 restore join을 구현한다.
- [ ] legacy release·conveyor·direct warehouse·item-only distribution 우회를 typed fail-closed하고 production bypass caller를 0으로 만든다.
- [ ] exact route 후 Loose slice를 기존 AIHaul lease에 연결하고 partial pickup·deposit·cancel·Downed·mid-haul restore에서 range와 grams를 보존한다.
- [x] durable save byte replace 성공 후 Economy whole-batch tombstone·Items route outbox·모든 physical descendant custody를 하나의 checkpoint coordinator로만 GC하고, 운반·예약·recovery 중인 batch는 전체 defer하며 부분 publish·rollback·replay에서 세 권위가 분리되지 않음을 증명한다.
- [ ] 최초 physical receipt는 불변으로 보존하고 monotonic delivery revision으로 consumer 취소·목적지 파괴·claim closing·Downed를 explicit exact-warehouse reroute한다. raw `ReleaseDestination`/`ReleaseStacksByDestination`은 모든 custody phase를 fail-closed하고 ProductionInput→Surgery→Construction→Equipment 순서로 owner tombstone·haul intent·lease·gram admission을 원자 전이한다.
- [ ] 실제 feedbench 네 recipe와 사일리지 실패 `690g → plant-rot 600g + DeclaredLoss 90g`를 output buffer→Loose→kg warehouse까지 검증한다.
- [ ] focused fault injection, current-format save/restore, fresh compile, PlayMode, artifact 2회 identity와 Console Warning/Error `0/0` 뒤에만 97.3의 distribution·manifest 행을 닫는다.

2026-08-25 prepared-output fresh 증거: synchronous AssetDatabase refresh와 clean build 뒤 current log segment의 compile error는 `0`이고 `Assembly-CSharp.dll 8,041,472 bytes @ 2026-08-25T11:10:36Z`, `Assembly-CSharp-Editor.dll 8,258,048 bytes @ 2026-08-25T11:10:38Z`, `DungeonStory.Economy.dll 425,984 bytes @ 2026-08-25T11:10:28Z`다. `FacilityBufferMassAdmission`, atomic planned publication, canonical keyed output resolver, prepared save contract/component codec, destination/resume authority, partial·missing·extra·wrong restore join, silage ruin contract, durable routing authority와 전체 `ProductionEconomy` 11개 focused suite가 같은 source에서 PASS했고 Console Warning/Error는 `0/0`이다. 이 증거는 위의 RNG/save/restore-join 세 행만 닫으며 exact provenance route·실제 AIHaul·PlayMode·manifest migration은 증명하지 않는다.

2026-08-25 exact provenance/checkpoint fresh 증거: Physical Items current-format은 V13이며 원본 physical receipt/target과 별도의 current-delivery overlay를 outbox와 모든 descendant에 저장하고 restore 전에 exact join한다. durable save는 sibling 임시 파일에 `WriteThrough + Flush(true)` 후 atomic replace하고, 그 성공 뒤 Economy/Items 두 participant가 동일 batch·route 집합과 digest/sequence를 publish하며 stable하지 않은 carried/in-transit/reserved/recovery batch는 whole-batch defer한다. fresh synchronous compile의 `Assembly-CSharp.dll 8,239,104 bytes @ 2026-08-25T13:40:59Z`, `Assembly-CSharp-Editor.dll 8,302,592 bytes @ 2026-08-25T13:43:02Z`, `DungeonStory.Economy.dll 434,176 bytes @ 2026-08-25T13:21:23Z`, `DungeonStory.Items.dll 62,464 bytes @ 2026-08-25T13:21:22Z`, `DungeonStory.Production.dll 90,624 bytes @ 2026-08-25T13:21:22Z`를 확인했고 해당 Editor.log 구간의 `error CS/Tundra build failed/Script Compilation Error/Unhandled Exception=0/0/0/0`이다. `PreparedOutputCheckpointGc`, `FacilityOutputExactRoute`, custody mutation/carry/buffer guards, Economy routing authority·routing restore join·prepared contract/component/restore/resume와 전체 `ProductionEconomy`가 PASS했으며 Console Warning/Error `0/0`이다. 따라서 위 6행만 닫고 legacy production bypass 전수 0, 실제 AIHaul PlayMode, live delivery reroute, feedbench 전구간과 artifact 2회 identity는 계속 OPEN으로 둔다.

2026-08-25 actual AIHaul 부분 증거: 실제 P17 사료배합대의 `recipe:hay-feed` bill이 물리 입력 `grass-straw×3 + twilight-grain×1`을 소비해 exact `feed:hay×3 = 588g` route custody를 만들고, current delivery revision의 kg warehouse를 선택한 뒤 기존 `AIHaul`이 admission `588g`을 pickup 전에 예약해 창고에 `3/3`을 적재하고 최종 reserved inbound `0g`으로 종료했다. 이 과정에서 immutable 최초 receipt의 빈 target과 current delivery target을 혼동하던 extraction 검사를 고쳤고, warehouse center와 walkable delivery cell을 같은 좌표로 강제하던 pickup preflight를 live delivery-cell 권위로 교정했다. fresh compile 뒤 `PreparedOutputHaulPlannerGateDebugScenarios`와 `Artifacts/QA/prepared-output-warehouse-live-playmode-report.txt`가 PASS했고 보고서 SHA-256은 `DB95B67A452D56DC89A6BF049DF7C81307AB110D20D0E3AA9C83967B2F93CCA8`, Console Warning/Error `0/0`이다. 이는 5078의 정상 whole-stack AIHaul와 5081의 hay-feed 한 recipe에 대한 부분 증거일 뿐, partial/cancel/Downed/mid-haul restore, 다른 세 feed recipe, 사일리지 실패, fault injection·2회 identity를 아직 증명하지 않았으므로 해당 행은 닫지 않는다.

2026-08-26 capacity-source 및 sawmill focused 증거: prepared-output save를 schema `v3`로 올리고 `capacitySourceDigest`, cycle, projected portfolio, required minimum을 저장·clone·contract에 결속했다. `ProductionOutputBufferCapacitySourceGuard`를 실제 execution adapter의 restore/resume validation이 사용하며, current source와 다른 canonical 64-hex digest·facility identity는 `prepared-output-capacity-source-stale`로 fail-loud하고 durable batch를 변형하지 않는다. P17은 reachable maximum `1,050g × 4 = 4,200g`, P03 sawmill은 `material:lumber 3 × 1,200g × 4 = 14,400g`으로 계산된다. sawmill recipe profile SHA-256은 `aff2ab2651af8d28bc86764c0edd151e22b1b7b91e6cc2bf20feea19aeb128fb`, 5-profile registry SHA-256은 `1c8fd366e5a5c8761c3cf8119afde3d758344025c952f48a29e7c58a2ecce218`다. fresh Unity compile 뒤 capacity projector/profile/contract/restore/admission suite, isolated publication, recipe/item/component digest, resume/routing/destination 및 Production Economy가 PASS했고 Console Warning/Error `0/0`이다. 다만 sawmill의 real adapter 종단 fixture와 정상 부트 PlayMode가 없으므로 sawmill live closure, Batch A/B, 전체 kg/EWU·가격·6인망은 계속 OPEN이다.

## 98. 구현 체크포인트: 장비 수리 exact dynamic-mass FacilityBuffer 이관

상태: **`equipment-repair:{equipmentInstanceId}` owner-wide claim/profile·동적 장비+재료 1회분 capacity·공용 exact token·복원/WIP·실제 AIHaul·terminal zero·manifest 결정론 완료 / 나머지 input 36개와 output/bypass/orphan 대기**.

### 98.1 구조 계약

| 항목 | 확정 계약 |
|---|---|
| owner | owner domain `combat.equipment-maintenance`, operation ID는 repair order ID, destination은 canonical `equipment-repair:{equipmentInstanceId}`, lifetime은 `LiveFacility`다. |
| capacity | 정확히 한 repair job의 `unique equipment dynamic mass + required material exact mass`다. 반복 처리량 시설이 아니므로 2~4회분을 적용하지 않는다. |
| 장비 질량 | 장비 본체 authored gram에 설치 모듈과 실제 장전 탄약을 포함한다. 내구도·품질·오염 등 V27 질량 불변 성분은 추가하지 않는다. |
| 재료 질량 | `requiredMaterialAmount × authoritative material unit mass`이며 category·count fallback을 금지한다. |
| publication | 활성 repair order 전체를 stable order/destination 순서로 다시 계산해 claim/profile 한 candidate로 원자 교체한다. 한 주문 완료·취소가 다른 활성 주문 authority를 삭제하지 않는다. |
| delivery | exact claim/profile을 배송 직전에 다시 검증한다. managed destination의 profile 누락·잘못된 owner/facility/revision/capacity는 source mutation 전에 fail-loud한다. |
| 저장·복원 | maintenance restore candidate의 전체 활성 order 집합에서 authority를 준비·게시한다. material WIP receipt와 order provenance는 기존 exact Transfer 양방향 조인을 유지한다. |
| terminal | 완료·취소 후 남은 활성 주문 전체를 재게시한다. 마지막 주문이면 destination claim/profile 모두 0이며 장비·재료를 삭제하거나 원격 반환하지 않는다. |

### 98.2 구현·증거 체크리스트

- [x] equipment repair claim·배송·WIP·완료·취소·restore 경로를 전수 감사하고 direct FacilityBuffer spawn bypass가 없음을 확인했다.
- [x] maintenance runtime에 공용 lifecycle/capacity authority를 주입하고 생성·복원·완료·취소를 활성 owner 전체의 원자 claim/profile 교체로 전환했다.
- [x] unique equipment 본체와 설치 모듈·장전 탄약 canonical component 질량, exact 수리 재료 질량을 합산한 positive 1회분 profile을 구현했다.
- [x] 배송 전 `RequireRepairBufferAuthority`가 exact claim/profile/revision/capacity를 재검증하고 legacy 개별 claim/revoke 경로를 0으로 ratchet한다.
- [x] focused fixture에서 common exact-lot token reserve/commit, restore profile 재생성, material WIP receipt join과 terminal profile 0을 검증했다.
- [x] clean Unity compile에서 발견한 short-circuit definite-assignment 오류를 권위 완화 없이 두 단계 fail-loud 검증으로 수정했다. 최종 `Assembly-CSharp.dll`/Editor DLL은 `2026-08-25T09:04:44Z`/`09:10:59Z` current source다.
- [x] 전용 PlayMode가 실제 LiveFacility와 actual AIHaul로 장비+재료 두 pickup을 수행해 exact profile `6,500g`, revision `1`, 중복 요청 0, 수리 완료, claim/profile 0, salvage 보존을 PASS했다. 보고서 SHA-256 `867BC180F3C532F549A1584250B7C5D95C274EE8834352D12D188838526DFDDE`, captured/Console Warning/Error `0/0`이다.
- [x] Strict Progression Combat Save·FacilityBuffer Mass Admission·Physical Stock·Production Economy·Industrial Infrastructure static suite를 current source에서 재실행해 모두 PASS, Console Warning/Error `0/0`을 확인했다.
- [x] owner manifest를 input `39`, migrated `3`, remaining `36`, output `5`, bypass `5`, orphan `1`, unclassified `0`으로 갱신했다. CSV SHA-256 `4578FAA4E4D1310484E2CB966E4FCD7BCECC17A99E1BB322E765DA74421B55EE`, TXT SHA-256 `CAAC0A58031E9C50926161A0A6F5858BAFBB018C63D14374E26006B7CBB56A31`이며 2회 생성 byte·mtime 변화가 없다.

전체 Physical Item Logistics suite는 이 focused repair 구간을 PASS했지만 별도 craft-output fixture와 stale construction restore fixture에서 기존 11개 실패가 남아 있다. 이 실패를 repair 이관 실패로 잘못 귀속하지 않으며, 동시에 focused PASS로 전체 물류 완료를 주장하지 않는다.

이 체크포인트는 장비 수리 한 owner의 **동적 질량 권위→공용 admission→실제 운반→WIP→완료/취소/복원**을 닫는다. 다른 input `36`, output `5`, bypass `5`, orphan `1`, 장비 전수 kg·EWU·가격·6인 성장/전투 경제와 broad 물류 실패는 열려 있다.

## 99. 구현 체크포인트: production-capable 시설 파괴의 durable lifecycle 기반

상태: **공통 파괴 operation journal·current-format 자체 검증·live/save-stable fingerprint 분리·generic/combat/apparel/full routing/outbox/physical/capacity save projector·필수 5-contributor aggregate·present/absent whole-save·registry 교차 preflight·정규화된 7-section transaction candidate index·exact 5-participant registry/DAG/fingerprint·journal immutable-plan/monotonic-step guard·물리 destination 단일 owner·Physical V14 producer outbox/영수증 join·운반 중단 실패 원자성·의복 시도별 재료 operation ID 완료 / 나머지 owner/receipt 역조인·실제 participant drain·등록·침입 PlayMode 대기**.

### 99.1 이번 체크포인트에서 닫은 범위

- [x] `ProductionFacilityDestructiveDrainOperationId`와 cause/phase/participant/owner step을 current-format DTO로 정의하고, 동일 요청 replay 무변경·cause conflict·stale revision·phase 순서·terminal checkpoint GC를 fail-loud하는 `ProductionFacilityDestructiveDrainJournal`을 aggregate root store에 구현했다.
- [x] journal payload의 canonical ID, stable participant/owner 순서, request/plan/lifecycle/registry fingerprint, step operation/commit/receipt 위상을 검증하는 rollback-free save-section 골격과 byte-stable save/restore focused fixture를 추가했다.
- [x] live lifecycle fingerprint에서 저장되지 않는 `BillVersion`, apparel `Version`, capacity `Revision`, repository `ItemStackVersion`, stack `reservationRevision`을 별도 durable fingerprint에서 제외했다. transient capacity reserved gram도 restore 때 재발행되지 않으므로 durable fingerprint에서 제외했다.
- [x] generic production은 runtime record를 canonical `ProductionBillSaveData` 전체로 투영한 뒤 durable hash에 넣고, combat craft와 apparel은 public serialized order DTO 전체를 durable hash에 결속했다. 저장 collection 입력 순서를 바꿔도 stable ID 정렬 후 같은 hash이며 WIP/material/repair provenance 한 필드 변경은 hash를 바꾼다.
- [x] detached save-only projector가 generic bills, combat craft, apparel과 production-origin physical stack의 durable contribution을 live publication 없이 계산하도록 골격을 추가했다. equipment/apparel live contributor와 detached DTO projector equality, generic/physical shuffle invariance와 provenance mutation을 focused regression으로 증명했다.
- [x] prepared routing은 facility-owned batch/line/route operation/physical slice/delivery revision 전체 DTO를, Items exact-route outbox는 원본 receipt와 current delivery overlay·slice 전체 DTO를 저장 순서와 무관하게 canonical clone한 뒤 durable hash에 결속했다. 전역 checkpoint watermark는 facility fingerprint에서 제외했다.
- [x] physical durable projection은 저장과 같은 stack DTO를 공용 `CaptureDurableStack`에서 만들고 item/instance/components, 위치·destination, contamination, full route-custody component, recovery source/carrier/interruption/time을 모두 결속한다. committed haul intent는 exact carried inventory와 join하고 warehouse admission은 stable provenance를 포함하되 restore 때 재발급되는 `tokenId`만 비운다.
- [x] 공용 semantic digest에 finite 검사와 `+0/-0` 정규화를 적용한 `AppendDouble`을 추가했다. 최종 lifecycle aggregate는 `production-output-durable-lifecycle@1` typed-builder framing과 필수 5개 contributor ID 집합을 강제하며 누락·치환·중복을 fail-loud한다. contributor 내부 current-format DTO canonical JSON framing의 typed-field 전환은 semantic schema 변경이 필요할 때의 후속 최적화이며 현재 등록 전 무결성 blocker가 아니다.
- [x] 운반 중단은 carried physical recovery/drop 성공 전에 reservation, active plan, delivery intent와 destination claim을 해제하지 않는다. drop 실패에서는 cargo와 owner를 `RecoveryPending`으로 유지하고 terminal release 전체를 중단한다.
- [x] production input claim/profile revoke는 exact pair가 이미 없으면 revision을 바꾸지 않는 idempotent replay이며, claim-only/profile-only partial state는 보존한 채 fail-loud한다.
- [x] 의복 품질 재시도의 material disposition operation ID를 `apparel-craft-material:{orderId}:{attemptIndex:D4}`로 분리했다. attempt `0000/0001`이 다른 authority가 되는 focused 회귀를 추가했으며 laundry/drying/repair/alteration의 기존 operation identity는 변경하지 않았다.
- [x] destructive-drain envelope가 실제 존재할 때만 동작하는 순수 raw-save 교차 검증기를 whole-game preflight와 `DungeonSaveSectionRegistry` pre-stage 양쪽에 연결했다. 제거 전 phase는 exact facility 1개와 현재 5-contributor fingerprint를 요구하고, `WorldRemovedAwaitingCheckpointGc`는 facility·bill·combat/apparel order·routing/outbox·physical custody·carried intent가 모두 0인 canonical absent aggregate만 허용한다. envelope 미등록 save는 no-op이며 fingerprint 1-bit drift, 시설 누락/중복과 absent owner는 fail-loud한다.
- [x] world/characters/physical/production/routing/combat/environment의 정규화 payload를 일반 preflight나 stage build가 아니라 실제 detached transaction `Commit`에서만 exactly-once deep clone하는 공용 candidate index를 연결했다. 7개 고정 슬롯과 insertion-order-independent manifest를 요구하고 duplicate·missing·transaction 밖 게시를 fail-loud하며, success/rollback/discard 모두 참조를 0으로 정리한다. drain section commit은 완전한 bundle을 같은 순수 validator로 재검증하도록 연결했지만 save registry 등록은 계속 금지한다.
- [x] destructive drain participant registry를 필수 5개 ID와 contract version `1`로 고정하고, `apparel/combat/generic → capacity-routing → physical-custody` DAG를 Kahn ready-set ordinal 순서로 실행한다. 저장 participant 행은 ID ordinal을 유지하며 DI 입력·dependency 입력 순서와 culture가 fingerprint를 바꾸지 않는다. journal 요청 replay는 immutable participant/owner plan drift를 conflict로 거부하고 advance는 owner 삭제·phase regression·`Planned → OwnerAcknowledged` 점프·commit/receipt 변조를 거부한다.
- [x] atomic save replace 뒤 후처리를 ordered `IDungeonDurableSaveCommitParticipant` 파이프라인으로 분리했다. 기존 prepared-output checkpoint GC는 order `100` adapter로 유지하고, `Applied/AlreadyApplied`만 다음 참가자로 진행하며 `Deferred/Corruption`은 suffix를 중단한다. 기존 checkpoint coordinator를 받는 3/4-argument save-slot 생성자는 같은 adapter로 보존했고, production DI는 새 coordinator를 사용한다. destructive-drain resume/GC order `200` 실제 adapter는 아직 등록하지 않는다.
- [x] raw-envelope와 normalized candidate cross-validator가 drain header의 exact registry fingerprint, 필수 5개 participant ID/version, 각 `expectedCurrentContributionFingerprint`와 실제 save-only contributor projection을 직접 결속한다. `Prepared`에서는 prepared/current contribution이 모두 source projection과 일치해야 하며, world-removed에서는 canonical absent contributor 집합과 일치해야 한다. missing participant와 1-bit contribution drift는 live publication 전에 실패한다. 이는 participant↔contributor mapping만 닫으며 owner↔source/receipt 양방향 join을 대신하지 않는다.
- [x] `Prepared` journal owner를 typed stable key(`bill:`, `craft-order:`, `apparel-order:`, `routing-batch:`, `physical-destination:`)로 save-only source DTO에 투영하고 5 participant 각각 exact forward/reverse bijection을 요구한다. physical은 실제 bulk release 원자 단위와 맞게 같은 origin destination의 stack/carry/recovery 전체를 destination owner 하나로 접는다. 중복 source ID, source-only owner, journal-only owner와 non-Planned owner는 publication 전에 실패한다. world-removed는 모든 journal owner가 `OwnerAcknowledged`여야 한다. 이는 initial owner/source join만 닫으며 effect receipt와 terminal transfer 증명을 대신하지 않는다.
- [x] Physical current-format schema를 V14로 올리고 Items aggregate에 producer-side custody-drain outbox를 추가했다. immutable source stack/actor/haul-intent 벡터, owner cell, source/request fingerprint, input quantity/gram과 `Prepared → ReleasingActors → ReleasingIntents → ReleasingDestination → EffectCommittedAwaitingOwnerAck → OwnerAcknowledgedAwaitingCheckpointGc` 단계를 stable 순서로 저장한다. 동일 prepare/progress/effect/ack replay는 무변경이고 순서 위반·payload drift·1g 변조·checkpoint 전 GC는 fail-loud한다.
- [x] physical producer outbox와 destructive journal destination owner를 step operation/request/owner/destination/phase/commit/receipt로 양방향 조인했다. journal Planned는 producer가 없거나 최대 effect-committed까지, journal EffectCommitted는 producer effect/ack, journal OwnerAcknowledged는 producer ack만 허용한다. producer-only orphan과 commit/receipt drift는 normalized publication 전에 거부한다. 이 tombstone은 active lifecycle 소유량에 합산하지 않고 별도 producer 증거로만 검증한다.

### 99.2 계속 열린 P0와 등록 금지 조건

- [x] detached projector를 5개 contributor 전체 aggregate로 완성했다. routing batch/line/operation/slice/delivery revision, Items exact-route outbox, physical custody component의 전체 provenance, recovery source/carrier/interruption/time, haul commitment와 stable warehouse admission을 포함하고 restore 때 재발급되는 warehouse token ID와 runtime-only revision은 제외한다. 필수 contributor 집합은 `apparel-work-orders`, `capacity-routing-outbox`, `combat-equipment-crafting`, `generic-production-bills`, `physical-custody-carry-recovery`로 고정한다.
- [x] capacity는 저장되지 않는 live profile을 읽지 않고 `ModularFacilityBuildingSaveData + BuildingSO`에서 live와 동일한 immutable `ProductionFacilityCapacitySubject`를 재구성한다. authored recipe/mass authority로 portfolio와 prepared-output required minimum을 재계산하고 non-carried/carried physical occupancy를 exact 조인하며 transient reserved gram은 제외한다. missing physical carried stack, carry inventory/operation/item/quantity/signature mismatch, duplicate commitment ownership은 조용히 누락하지 않고 fail-loud한다.
- [ ] `DungeonAggregateReferencePreflight`와 section-stage restore join 양쪽에 world/physical/production bills/routing/combat/environment/journal forward·reverse 검증을 추가한다. journal owner가 source DTO에 없거나 destructive receipt가 journal 없이 존재하면 live publication 전에 전체 restore를 실패시킨다.
- [ ] apparel craft는 시도별 material pending receipt, frozen outcome, deterministic unique output operation/instance/commit, completion effect receipt를 저장하는 재개 가능 outbox로 전환한다. 이번 ID 분리만으로 crash-safe craft completion이 완료된 것은 아니다.
- [ ] combat craft의 `completionEffectsPublished` bool을 operation/request/commit/receipt/phase로 교체하고, 영감·기분·quality event를 character narrative aggregate의 idempotent effect ledger와 detached join한다.
- [ ] generic WIP, combat craft, apparel, routing/outbox, physical custody/carried/recovery 각각에 `Prepare → 한 Tick 한 step → effect commit → owner ack → absent verify` participant를 구현한다.
- [ ] `IProductionPhysicalCustodyDrainPort`를 outbox 위에 구현해 actor별 중단, intent별 해제, destination release를 같은 capture barrier/mutation 구간에서 재개한다. 한 actor의 active Pick-and-Haul plan에 다른 destination operation이 섞이면 기존 전체 `TryStopHauling`을 호출하지 말고 `mixed-destination-active-plan`으로 defer하거나 operation-scoped 중단 API로 대상 operation만 처리해야 한다.
- [ ] 위 양방향 join, actual participant의 receipt commit/owner acknowledgement/다음 Tick resume, restore 실패 시 candidate 전부 폐기가 모두 green이 되기 전에는 `ProductionFacilityDestructiveDrainSaveSection`을 DI/save registry에 등록하지 않는다. 지금 등록하면 current-format envelope를 바꾸면서 orphan open operation을 rollback-free publish할 수 있다.
- [ ] dirty `GameplayScene`을 저장·폐기하지 않는 조건 때문에 실제 침입 destructive-loss PlayMode와 정상 부트 P17 current-revision live report는 계속 OPEN이다.

### 99.3 current-source 증거와 해석 제한

- 강제 `AssetDatabase.Refresh(ForceSynchronousImport|ForceUpdate)`와 clean script compilation 뒤 `Assembly-CSharp.dll 8,448,512 bytes @ 2026-08-26 09:07:35 +09:00`, `Assembly-CSharp-Editor.dll 8,546,816 bytes @ 09:07:37 +09:00`을 확인했다. 마지막 Editor.log compile 구간의 `error CS`, `Tundra build failed`, `Script Compilation Error`, `Unhandled Exception`은 `0`이다.
- fresh DLL 기준 `ProductionOutputDestinationLifecycleDebugScenarios`, `ProductionFacilityDestructiveDrainJournalDebugScenarios`, `ProductionEconomyDebugScenarios`, `PhysicalItemDebugScenarios`, `ApparelRepairOutboxDebugScenarios.RunFocused`가 같은 focused set에서 PASS했고 Unity Console Warning/Error는 `0/0`이다.
- 2026-08-26 routing/physical projector 확장 뒤 fresh `Assembly-CSharp.dll 8,460,288 bytes @ 09:28:09 +09:00`, `Assembly-CSharp-Editor.dll 8,546,816 bytes @ 09:24:47 +09:00`을 확인했다. 마지막 Editor.log 500행의 `error CS`, `Tundra build failed`, `Script Compilation Error`, `Unhandled Exception`은 각각 0이며 위 focused 5개 suite 재실행과 Console Warning/Error `0/0`을 통과했다.
- 2026-08-26 capacity/save aggregate 확장 뒤 fresh `Assembly-CSharp.dll 8,471,552 bytes @ 09:57:14 +09:00`, `Assembly-CSharp-Editor.dll 8,553,472 bytes @ 10:01:08 +09:00`을 확인했다. `ProductionPreparedOutputFullPersistenceDebugScenarios`가 sawmill source live/source save-only/restored live/restored save-only의 필수 5-contributor durable aggregate exact equality를 증명했다. capacity fixture는 live/save subject equality, 4,200g profile, non-carried `588g` + carried `196g`, deposit-before-intent-retirement `784g` 단일 계상, intent retirement, wrong destination, missing physical stack, carry-operation mismatch, catalog 역순 aggregate equality와 필수 contributor schema fail-loud를 증명했다. focused 6개 suite PASS, Console Warning/Error `0/0`, 최근 Editor.log compiler failure pattern `0`이다.
- 2026-08-26 conditional cross-aggregate preflight와 absent-lifecycle 추가 뒤 fresh `Assembly-CSharp.dll 8,477,696 bytes @ 10:23:59 +09:00`, `Assembly-CSharp-Editor.dll 8,558,592 bytes @ 10:24:02 +09:00`을 확인했다. sawmill current-format source로 whole-save·registry exact preflight, drain-envelope absent no-op, lifecycle fingerprint drift, facility missing/duplicate, world-removed exact absent aggregate와 orphan production owner 거부를 실행했다. Full Persistence, capacity, lifecycle, journal, Production Economy, Physical Items, Dungeon Save Section suite가 PASS했고 Console Warning/Error `0/0`, 최근 Editor.log compiler failure pattern `0`이다.
- 2026-08-26 normalized section-stage index 추가 뒤 fresh `Assembly-CSharp.dll 8,486,400 bytes @ 10:43:10 +09:00`, `Assembly-CSharp-Editor.dll 8,568,832 bytes @ 10:50:01 +09:00`을 확인했다. commit-only hook, 7-slot 완비, reverse insertion 동일 manifest, deep-clone 격리, duplicate/missing/out-of-transaction 거부, success/rollback/discard cleanup과 두 연속 restore를 통과했다. sawmill full current-format registry는 실제 World/Physical/Production/Routing commit과 Character/Combat/Environment projection을 한 transaction index에 결속하고 완료 후 slot `0`을 증명했다. 위 focused 8개 suite PASS, Console Warning/Error `0/0`, 최근 Editor.log compiler failure pattern `0`이다.
- 2026-08-26 participant registry 교정 뒤 fresh `Assembly-CSharp.dll 8,495,104 bytes @ 11:07:18 +09:00`, `Assembly-CSharp-Editor.dll 8,577,024 bytes @ 11:07:21 +09:00`을 확인했다. 필수 5개 exact set/version, DAG execution, reverse DI order, culture invariant와 고정 registry fingerprint `316767864bd434d682d84c57b3b4de82fa8309304672eafd4ecea9645ec218a2`를 고정했다. missing/extra/duplicate/version/edge/unknown/self/duplicate-dependency/cycle을 fail-loud하고, journal replay plan drift·advance owner 삭제·phase jump를 거부한다. Registry/Journal/Index/Full Persistence/Lifecycle/Production Economy/Physical Items/Dungeon Save focused suite PASS, 최근 Editor.log compiler failure pattern `0`, Console Warning/Error `0/0`이다.
- 2026-08-26 durable-save pipeline 추가 뒤 fresh `DungeonStory.Foundation.dll 163,840 bytes @ 11:17:57 +09:00`, `Assembly-CSharp.dll 8,499,200 bytes @ 11:18:09 +09:00`, `Assembly-CSharp-Editor.dll 8,581,632 bytes @ 11:18:11 +09:00`을 확인했다. reverse registration order에서도 `100 → 200` 실행, AlreadyApplied continuation, Deferred/Corruption suffix stop, thrown/conflicting result typed corruption, duplicate ID/order 거부와 prepared-output status mapping을 통과했다. Durable Save Commit/Prepared Output Checkpoint GC/Dungeon Save Section focused suite PASS, 최근 Editor.log compiler failure pattern `0`, Console Warning/Error `0/0`이다.
- 2026-08-26 participant↔contributor mapping 강화 뒤 fresh `Assembly-CSharp.dll 8,500,736 bytes @ 11:26:10 +09:00`, `Assembly-CSharp-Editor.dll 8,583,680 bytes @ 11:26:12 +09:00`을 확인했다. exact current-format registry header, 5 participant/version, prepared/current contributor projection, missing participant와 contribution drift fail-loud, world-removed absent projection을 통과했다. Registry/Journal/Index/Full Persistence/Lifecycle/Production Economy/Physical Items/Dungeon Save focused suite PASS, 최근 Editor.log compiler failure pattern `0`, Console Warning/Error `0/0`이다.
- 2026-08-26 Prepared owner/source bijection 추가 뒤 fresh `Assembly-CSharp.dll 8,505,856 bytes @ 11:33:07 +09:00`, `Assembly-CSharp-Editor.dll 8,586,752 bytes @ 11:33:09 +09:00`을 확인했다. generic bill, combat craft, noncompleted apparel, routing batch와 origin/custody physical stack을 typed key로 projection하고 source-only/journal-only owner를 모두 거부했다. world-removed fixture는 모든 실제 owner를 acknowledged로 유지한다. focused 8개 suite PASS, 최근 Editor.log compiler failure pattern `0`, Console Warning/Error `0/0`이다.
- 2026-08-26 physical producer outbox 추가 뒤 current-format Physical V14, destination 단일 physical owner, stable actor/intent progress, effect receipt, ack/GC와 journal phase matrix를 연결했다. `ProductionPhysicalCustodyDrainOutboxDebugScenarios`, full persistence, Physical Items, Dungeon Save Section, Production Economy 회귀가 PASS했고 1g/순서/request/receipt/orphan 변조를 거부했다. fresh `Assembly-CSharp.dll 8,521,216 bytes @ 12:08:03 +09:00`, `Assembly-CSharp-Editor.dll 8,595,968 bytes @ 12:17:38 +09:00`; 최근 Editor.log compiler failure pattern `0`, Console Warning/Error `0/0`이다.
- 이 증거는 journal 자체, 전체 5-contributor save-stable fingerprint, capacity/occupancy 복원, 첫 raw-envelope 교차 preflight, normalized 7-section candidate 수명주기와 participant registry/DAG/fingerprint를 닫는다. journal owner↔source/receipt 양방향 조인, destructive drain participant, 실제 침입 PlayMode는 아직 없고 journal도 gameplay/save registry에 등록되지 않았으므로 §16.3의 active-authority destructive-loss 행과 전체 Batch B를 완료로 세지 않는다.

## 101. 구현 체크포인트: generic recipe output의 capability 기반 선택 전환

상태: **구조·legacy fallback ratchet·focused evidence 완료 / asset-mutating synthetic full-path canary 미완료 / Batch A 진행 중**.

### 101.1 닫은 범위

- [x] 표준 prepared-output 선택에서 recipe ID·prefix·11개 migration allowlist를 제거했다. 각 물리 output line의 frozen descriptor가 `standard-definition@1 + definition-only-codec@1`인지 확인해 공통 prepared batch를 선택한다.
- [x] Production bill 생성·분배·수요·capacity projection·save/restore가 같은 capability 판정을 사용한다. 새 definition-only item/recipe는 별도 코어 분기 없이 이 경로에 진입한다.
- [x] item feature가 production output instance state를 요구하는지 fail-closed 선언한다. Food·Medicine·PackagedLot·Vaccine·PathogenSample·MedicalProcedureSupply·CropTreatment·Substance·FacilitySupply·EvolutionCatalyst 등 definition-only feature는 공통 codec에서 허용하고 Equipment·Ammunition·Installation·Blueprint 등 stateful 계열은 전용 capability 없이는 거부한다.
- [x] profile digest를 고정 recipe registry가 아니라 current recipe semantic digest에서 결정론적으로 생성하고, resolved batch restore에서 live recipe와 exact 비교한다.
- [x] 전수 profile 감사가 발견한 유일한 authored 결함 `recipe:medical-vial`의 빈 primary proficiency를 같은 `forge/work:craft` 계열 권위인 `proficiency:crafting`으로 교정했다. kg·BOM·수량·WU·EWU·가격은 변경하지 않았다.
- [x] Unity current-source에서 component codec, handler registry, prepared-output contract, 351개 physical-output recipe dynamic profile, 실제 sawmill prepared adapter focused bundle이 PASS했고 최종 Console Warning/Error는 `0/0`이다.

### 101.2 아직 열린 exit gate

- [ ] temporary synthetic definition-only item/recipe가 공통 catalog에 들어와 실제 bill→FacilityOutputBuffer→route→AIHaul→gram warehouse→current-format save/restore→UI/audit까지 통과하고 제거 뒤 고아 권위 0인지 증명한다.
- [x] `standard-definition`을 descriptor-only capability로 분리하고 `ProductionOutputExecutionService`의 exact legacy 실행을 거부한다. 전체 output vector를 RNG·publication 전에 분류해 all-standard는 prepared, all-special은 exact capability, mixed standard/special은 원자 composite capability가 없으면 fail-loud한다.
- [x] silage ruined-output의 recipe-ID 전용 분기를 제거하고 typed spoilage/disposition capability로 교체했다. capability가 없는 custom passive recipe는 WIP와 입력을 보존한 채 `ruined-output-capability-unsupported`로 실패한다.
- [x] owner manifest의 마지막 generic output owner를 `migrated`로 바꾸고 두 번 생성해 byte/hash/mtime diff 0을 증명했다. output owner는 `6/6 migrated`, remaining `0`이다.
- [ ] Batch A의 actual normal·capacity wait·cancel·destroyed·restore fault matrix와 synthetic canary까지 모두 green이 되기 전에는 Batch A 체크를 닫지 않는다.

### 101.3 작업량 해석

- 이 전환으로 기존 capability 범위의 일반 item/recipe 추가는 handler·save DTO·capacity 분기 코드를 매번 작성하는 작업에서 벗어나 authoring data, anomaly review와 공용 회귀 실행 중심으로 축소된다.
- 이번 checkpoint 자체는 큰 Batch A~H를 닫지 않으므로 `0/8`을 유지한다. legacy fallback ratchet은 닫혔지만 focused 기반만으로 가중 잔여량을 낮추지 않고 약 `31~41%`를 유지하며, synthetic full-path canary와 normal/fault PlayMode 통과 뒤 다시 산정한다.

### 101.4 6/6 owner current-source 증거와 남은 blocker — 2026-08-27

- `StandardDefinitionProductionOutputCapability`는 metadata-only이며 `IProductionOutputHandler`가 아니다. registry exact validation은 descriptor를 검증하지만 executable resolution은 이를 거부한다.
- capability vector는 물리 output 전부를 먼저 캡처한다. `StandardPrepared`와 `ExactCapability`만 허용하며 혼합 vector는 `mixed-standard-output-capability-route-unsupported`로 RNG draw와 물리 mutation 전에 실패한다.
- static diagnostic은 generic ruined batch의 직접 spawn과 `recipe:silage` 분기, 표준 direct execution, non-Editor orphan buffered-output writer를 모두 ratchet한다.
- current-source Unity에서 `ProductionEconomyDebugScenarios.RunAll`, output registry, ruined batch, migration profile, full current-format persistence, static bypass, owner manifest가 PASS했고 Console Warning/Error는 `0/0`이다.
- 추가 non-PlayMode fault aggregate가 Combat/Apparel Editor fixture의 오래된 executable standard fake를 검출했다. fixture도 metadata-only 표준 capability로 교정한 뒤 exact-route lifecycle, routing authority rollback, restore join, multi-stack/priority/partial-heavy/transit/raw-food haul safety, Apparel physical output·rejected dismantle가 current-source에서 PASS했고 Console Warning/Error는 `0/0`이다.
- owner manifest 두 번째 생성은 byte/hash/mtime 변화가 0이다. output owner는 `6/6 migrated`, output remaining `0`이다.
- definition-only synthetic canary의 isolated contract는 request round-trip, re-entry ownership, scene/asset mutation `0`으로 PASS했다. 그러나 이는 bill→FacilityOutputBuffer→AIHaul→gram warehouse→RestoreAll의 asset-mutating full-path 증거가 아니다.
- full-path canary는 F: 볼륨의 Win32 I/O error 55와 Windows `HealthStatus=Warning / OperationalStatus=Full Repair Needed` 때문에 중단했다. 사용자 소유의 dirty `GameplayScene.unity`도 live PlayMode verifier 진입을 차단한다. 복구·명시적 위험 승인 또는 깨끗한 별도 검증 환경 없이 이 gate를 우회하지 않는다.
- current-source 비 PlayMode fault bundle은 추가로 닫혔다. `ProductionDomainOutputPublicationDebugScenarios`, `ProductionDomainOutputRestoreGuardDebugScenarios`, destructive-drain preflight/participant/outbox, `ProductionCapacityRoutingActorTransitionDebugScenarios`, `ProductionPreparedOutputFullPersistenceDebugScenarios`가 한 clean-console 구간에서 PASS했고 Warning/Error는 `0/0`이다. 두 actor current-cell quiescence, actor-B authority release 직전 fault, partial phase save 거부, replay completion, stable capture와 no-op replay를 검증한다. 이를 위해 오래된 Editor fixture의 Routable custody에 delivery revision `0`과 current target을 추가하고, custody 포함 carried signature와 custody 제외 business component fingerprint를 분리했다. production validator와 authored 밸런스 수치는 변경하지 않았다.
- 따라서 Batch A~H는 `0/8`, weighted remaining은 약 `31~41%`로 유지한다. owner closure를 Batch A 완료나 밸런스 완료로 과대 보고하지 않는다.

### 101.5 capability-owned domain 최대 질량 증명 — 2026-08-27

- [x] `ProductionOutputBatchMaximumMassProof`를 추가했다. 출력 line별 frozen capability descriptor를 실행 없는 `IProductionOutputMaximumMassRegistry`로 투영하고, stable line 순서·중복 line 거부·checked gram 합계·SHA-256 proof digest를 하나의 immutable 증명으로 만든다.
- [x] 공용 capacity projector에 proof 입력 경계를 추가했다. 시설의 authored recipe portfolio 최대치와 domain capability proof의 최대치를 같은 cycle 권위로 비교해 더 큰 값을 profile에 반영하므로, 실제 batch가 뒤늦게 profile을 자기 확장하는 경로를 Combat·CertifiedSeed에서 제거했다.
- [x] `ProductionDomainOutputLine`이 capability descriptor를 필수로 가지며, 실제 prepared component 질량이 선언된 capability 최대 질량을 1g이라도 넘으면 `domain-output-line-mass-exceeds-capability-maximum`으로 batch 전체를 mutation 전에 거부한다.
- [x] domain publication current format을 schema V5로 올리고 `maximumMassProofDigest`와 `maximumBatchMassGrams`를 저장했다. committed/frozen owner는 두 필드를 필수로 검증하며 adoption·retry에서 exact 일치를 요구한다.
- [x] Combat equipment/ammunition과 CertifiedSeed의 실제 두 domain publication caller가 frozen descriptor를 전달한다. 외부 저장 형식은 과거 세이브 마이그레이션 제외 정책에 따라 Combat V11, CertifiedSeed V5로 올렸다.
- [x] restore owner가 capability descriptor와 maximum quantity claim을 저장 권위에서 재구성하고 registry로 proof를 다시 계산한다. saved proof digest, maximum gram, unit-mass authority 또는 capability fingerprint drift는 live owner publication 전에 실패한다.
- [x] proof 입력 순서 shuffle은 같은 digest를 만들고, duplicate line·underreported maximum·uppercase/noncanonical digest·1g mass-authority drift를 모두 원자 거부하는 focused fixture를 추가했다.
- [x] fresh Unity DLL 기준 domain publication, domain restore guard, Combat craft, Crop physical transaction focused bundle과 maximum-mass registry/factor/handler/full-persistence/full Production Economy 비-PlayMode 회귀가 PASS했다. 최종 Console Warning/Error는 `0/0`이다.

이 체크포인트가 줄이는 미래 작업은 domain 출력마다 별도 최대 질량 계산기·저장 필드·복원 분기를 새로 설계하는 부분이다. 새 출력은 capability descriptor와 maximum claim을 제공하면 공용 proof/publication/restore 경로를 재사용한다. 다만 다음 항목은 아직 열려 있으므로 전체 raw escape 제거 또는 Batch B 완료로 세지 않는다.

- [x] Apparel craft와 rejected dismantle의 raw exact capacity 호출 2곳을 capability proof로 전환한다. craft는 frozen declared descriptor를, rejected recovery는 첫 automatic 선택 descriptor를 동결하고 이후 declared 재투영을 사용한다.
- [x] Environmental workwear의 raw exact capacity 호출 1곳을 공용 capability proof/projector로 전환하고, proof·capacity source를 outcome fingerprint에 결속해 physical marker/retry에서 재검증한다.
- [x] Surgical part의 별도 profile-max digest를 공용 capability proof/projector로 통합하고, replay의 persisted outcome을 현재 proof와 대조한다.
- [ ] 위 경로가 모두 green인 뒤 generic raw exact-batch overload의 production 호출을 0으로 ratchet한다.
- [ ] domain restore의 capability proof뿐 아니라 catalog·workstation·cycle·현재 facility subject를 포함한 전체 detached capacity source도 live와 exact 재투영한다.

authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값은 변경하지 않았다. Batch A~H는 계속 `0/8`, output owner는 구조상 `6/6`, input owner는 `3/39`(remaining `36`, bypass `5`, orphan `1`)이며 weighted remaining은 약 `31~41%`다.

### 101.6 Apparel craft·rejected recovery 최대 질량 증명 — 2026-08-27

- [x] craft output은 기존 `craftOutputCapability`에서 maximum projection을 만들고, 실제 dynamic component 질량이 proof maximum을 넘기면 재료 debit·admission 전에 실패한다.
- [x] rejected recovery는 첫 시도의 automatic capability descriptor를 `rejectedRecoveryOutputCapability`에 동결한다. 재시도·복원은 automatic selection을 다시 하지 않고 frozen descriptor를 declared 방식으로 재투영한다.
- [x] craft/rejected 각각 proof digest와 maximum batch grams를 current-format owner에 저장하고 capacity source digest·required minimum·exact output mass와 frozen tuple로 검증한다.
- [x] 새 proof 필드는 clone에서 Trim이나 음수 보정을 하지 않고 exact 복제한다. noncanonical digest·음수·maximum 초과는 validator가 fail-loud한다.
- [x] Character Environment V11, Apparel terminal state schema V3, terminal-drain row schema V3, terminal-drain outer V3, source-order fingerprint schema `@3`으로 current-format 경계를 명시했다. 과거 세이브 마이그레이션은 하지 않는다.
- [x] `PrepareRestoreOrders`와 terminal-state restore validation이 current maximum registry에서 proof를 다시 계산해 descriptor·quantity·unit mass·authority revision·digest/max drift를 live publication 전에 거부한다.
- [x] focused fixture는 실제 craft output `2,000g`, declared maximum `4,000g`, four-cycle capacity `16,000g`을 분리해 raw exact self-sizing 회귀를 검출한다. rejected recovery는 `1,500g` maximum과 `6,000g` four-cycle capacity를 검증한다.
- [x] proof digest tamper는 craft/rejected 모두 물리 mutation 0으로 실패하고, rejected JSON round-trip은 frozen descriptor/proof를 보존한다.
- [x] fresh Unity compile 후 Apparel craft/rejected/terminal/repair focused bundle과 maximum registry/factor/handler/lifecycle/full persistence/full Production Economy aggregate가 PASS했다. 최종 Console Warning/Error는 `0/0`이다.

이 전환으로 의복 종류와 회수 재료가 추가돼도 별도 capacity 분기나 저장 필드를 다시 설계하지 않는다. 새 capability의 선언 최대치와 descriptor만 공용 registry에 제공한다. Workwear와 Surgical의 공용 proof 전환은 아래 후속 체크포인트에서 닫혔고, 전체 detached facility-source 재투영과 generic raw overload 제거는 계속 열려 있다.

### 101.7 Environmental workwear 최대 질량 증명 — 2026-08-27

- [x] 자동 선택된 Workwear capability descriptor와 definition-only 최대 질량을 실행 전에 캡처하고 handler capability/version/codec parity를 강제한다.
- [x] proof-aware capacity projector 결과를 destination minimum과 planned-output request에 사용하며 raw exact-batch self-sizing 호출을 제거했다.
- [x] proof digest/max와 capacity source digest/required minimum을 outcome fingerprint에 결속해 pending·acknowledged replay가 같은 의미 권위를 검증한다.
- [x] 실제 prepared unique apparel component 질량이 선언 maximum을 넘으면 reservation·publication 전에 실패한다.
- [x] fresh Unity compile, Workwear focused, maximum registry, handler registry, full persistence와 full Production Economy 비-PlayMode 회귀가 PASS했다.

### 101.8 Surgical part 최대 질량 증명 — 2026-08-27

- [x] 기존 `SurgicalPartOutputCapacitySource` profile-max 전용 digest를 제거하고 automatic Surgical capability → `ProductionOutputBatchMaximumMassProof` → 공용 capacity projector 경로로 교체했다.
- [x] authored `context.OutputLineId`를 proof descriptor, physical slice, outcome fingerprint와 replay join의 단일 line 권위로 사용한다. 별도 item 기반 line 재합성을 제거했다.
- [x] proof digest/max와 capacity digest/minimum을 persisted physical outcome fingerprint에 결속한다. replay는 facility와 현재 maximum/capacity를 다시 계산하고 pending·acknowledged batch의 commit/line/item/quantity/mass/outcome을 exact 검증한다.
- [x] proof projection은 Surgical handler capability/version/codec과 정확히 일치해야 하며, actual reserved mass가 maximum을 1g이라도 넘으면 publication 전에 reservation을 해제하고 실패한다. 해제 자체가 실패하면 별도 typed detail로 fail-loud한다.
- [x] direct fixture도 one-line proof, maximum quantity, proof×cycle batch minimum, capacity/profile minimum pairing을 검사해 무관한 proof와 capacity source 혼합을 mutation 전에 거부한다.
- [x] maximum 초과·release 실패·proof/capacity mismatch focused 회귀와 maximum registry, handler registry, maximum factor, full persistence, full Production Economy aggregate가 fresh Unity compile에서 PASS했다. 최종 Console Warning/Error는 `0/0`이다.

이 두 전환은 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값을 바꾸지 않는다. 미래의 Workwear/Surgical 콘텐츠는 기존 capability/codec/maximum projection 계약으로 자동 연결되며 코어 item-ID 분기를 추가하지 않는다. 다만 전체 detached facility-source restore 재투영, production raw overload ratchet, ruined output, fault/performance와 asset-backed full-path canary가 남아 Batch A~H는 계속 `0/8`, weighted remaining은 `31~41%`다.

### 101.9 raw exact-capacity API 1차 ratchet — 2026-08-27

- [x] non-Editor raw 호출을 전수 분류했다. 1차 ratchet 당시 6곳이었으나 ruined-output bypass, portfolio-only 호출, generic prepared actual, normal detached restore를 모두 proof/claim 또는 명시적 portfolio API로 전환했다. 현재 public raw overload `0`, production·Editor raw caller `0`이며 Domain/Apparel/Workwear/Surgical raw 호출도 `0`이다.
- [x] `IProductionOutputBufferCapacityProjector`에서 `(facility, long exactBatchMassGrams)` overload를 제거했다. capability handler는 proof overload밖에 호출할 수 없어 새 특수 콘텐츠가 exact result로 자기 profile을 확장하면 컴파일되지 않는다.
- [x] Apparel transaction도 concrete projector 대신 proof-only interface에 의존하도록 좁혔다.
- [x] 정적 진단은 concrete projector의 non-Editor 소유자를 projector 본체, generic prepared execution, detached/destructive save validation과 composition root로 한정하고 신규 owner를 실패시킨다.
- [x] fresh Unity compile, static bypass ratchet, Surgical/Workwear focused와 full Production Economy 회귀가 PASS했고 Console Warning/Error는 `0/0`이다.
- [x] concrete raw overload와 호출자를 완전히 제거했다. `CaptureSourceCore(subject, long)`은 projector 내부 private 계산 경계로만 남고, 정적 진단이 public raw overload `0`, raw caller `0`, 허용된 concrete owner 집합을 ratchet한다. 이 기계적 제거는 완료됐지만 저장값 위조를 authored 상한과 대조하는 공통 restore guard 완료를 의미하지 않는다.

따라서 raw 우회 차단과 generic/restore caller 제거 자체는 완료됐다. Batch B가 계속 열려 있는 이유는 raw API가 아니라 normal authored-upper-bound restore guard, domain 전체 detached source 재투영, no-bill contributor, fault/performance와 full-path 증거가 남았기 때문이다.

### 101.10 ruined generic output의 최대 질량·종료 라우팅 권위 — 2026-08-27

- [x] ruined batch의 WIP 입력, clean-water 입력, wastewater·manual-water provenance, frozen 폐기물 capability descriptor와 disposition을 하나의 `ProductionRuinedOutputCapacityClaim` SHA-256에 결속했다.
- [x] 실제 폐기물 수량을 먼저 만들어 raw 질량으로 profile을 자기 확장하던 경로를 제거했다. frozen descriptor의 1개 최대 질량으로 공용 disposition을 계산하고, 그 최대 폐기물 수량을 다시 `CaptureDeclared`하여 `ProductionOutputBatchMaximumMassProof`를 만든다.
- [x] prepared-output current format을 V6, Production root를 V20으로 올리고 `maximumMassProofDigest`, `maximumBatchMassGrams`, `capacityClaimDigest`를 저장한다. 세 필드는 all-or-none이며 모든 resolved normal/ruined prepared batch에 필수다.
- [x] live retry와 detached active-bill restore가 current recipe·WIP·fluid provenance·frozen descriptor로 claim을 재계산하고 proof/claim/capacity source를 exact 비교한다. 1g·digest·WIP drift는 live publication 전에 실패한다.
- [x] terminal routing batch에도 capacity source, cycle, portfolio, required minimum과 normal/ruined proof/claim을 복제했다. Routing current format은 V7이고 routing fingerprint token도 `prepared-output-routing-v7`이다.
- [x] 생산 bill이 종료된 뒤 routing batch만 남아도 save-only capacity projector가 저장된 proof-sized minimum을 유지하며, 현행 silage terminal fixture는 proof/claim 축소와 capacity authority drift를 거부한다. 다중 폐기물 QA recipe의 bill-retirement 후 `2,400g → 9,600g` terminal 증거는 별도 체크로 열어 둔다.
- [x] `2,400g WIP + 600g clean water - 300g wastewater` QA-authored fixture가 `600g × 4 = 2,400g recoverable waste + 300g declared loss`를 만들고, live claim/source digest 일치, active restore, proof/claim tamper 거부를 통과했다.
- [ ] 같은 다중 폐기물 QA recipe를 detached catalog와 terminal routing까지 전달해 bill retirement 뒤에도 exact `9,600g` minimum과 claim/source가 재현되는지 별도 회귀로 증명한다.
- [x] stale Editor DLL에서 나온 최초 PASS는 증거에서 폐기했다. AssetDatabase refresh/domain reload로 현재 Main·Editor assembly를 다시 만든 뒤 recipe semantic digest, prepared contract, ruined execution, maximum-factor, routing authority/restore join, destination lifecycle, full persistence, destructive-drain participant/preflight와 full Production Economy를 한 묶음으로 재실행해 PASS했다. 최종 Console Warning/Error는 `0/0`이다.

이번 체크포인트는 폐기 출력의 capacity self-sizing과 bill-retire shrink를 닫았을 뿐, 미래 ruined capability 전부가 자동 편입된 상태는 아니다. 다음 항목은 완료로 세지 않는다.

- [x] `ProductionRecipeSO`의 암묵 `waste:mixed-rot` 기본값·getter fallback을 제거했다. `PassiveBatch`는 raw authored spoilage ID가 non-empty canonical이고 현재 item catalog에 존재해야 하며, 누락·공백 보정 필요·orphan은 catalog capture에서 fail-loud한다. `WorkOnly`은 empty를 허용하되 값이 있으면 canonical이어야 한다.
  - 현재 355 recipes 전수 census에서 `PassiveBatch 8/8`은 명시적 canonical spoilage를 가지며 orphan `0`이다. `WorkOnly 347`의 기존 직렬화된 무의미 `waste:mixed-rot` 값은 런타임 fallback 권위가 아니며, 이번 체크포인트에서는 대규모 YAML noise를 피하려고 재직렬화하지 않았다.
  - recipe semantic digest schema는 `production-recipe-semantic@3`으로 올렸고, WorkOnly-empty 허용과 Passive empty/noncanonical/orphan 거부를 current-source focused test로 고정했다.
- [x] `StandardDefinition + DefinitionOnlyCodec` 값 비교를 제거하고 등록형 `IProductionPreparedOutputMaterializer`와 participation flag로 normal/ruined component 생성·복원을 dispatch한다. `ProductionBillStateCodec`은 DTO 구조·authority만 검사하고, capability별 payload 의미 검증은 restore transaction이 live materializer로 publication 전에 수행한다. adapter에서 Standard/DefinitionOnly 상수 분기를 금지하는 static ratchet도 통과했다.
- [x] facility portfolio의 ruined 상한은 고정 수량 1이 아니라 recipe inputs, facility process fluid, feasible support assignment의 fluid/fuel을 합친 authored WIP maximum envelope로 동일 `ProductionRuinedBatchDispositionPlan.Create` 공식을 호출한다. synthetic `3,000g` envelope와 `3,600g` oversized active claim 거부, QA `2,400+600-300 → 4×600+300`을 focused 회귀로 증명했다.
- [ ] temporary passive ruined recipe와 등록형 non-standard materializer가 코어 수정 없이 bill→ruin→FacilityBuffer→route→AIHaul→gram warehouse→save/restore를 통과하는 synthetic canary를 추가한다.
- [x] generic normal prepared batch의 live actual-mass 경로와 normal detached 재투영을 `ProductionPreparedOutputCapacityClaim`으로 전환하고 concrete raw overload·caller를 0으로 제거했다.
- [x] generic normal Main/Byproduct restore claim을 저장된 실제 line 수량으로만 자기 재생성하지 않는다. current recipe의 authored output line ID·role·item과 maximum output factor를 대조해 line별 수량이 authored maximum을 넘으면 claim 생성 전에 거부한다. quantity·grams·proof·claim·capacity를 산술적으로 함께 고친 forged fixture도 `exceeds its authored output maximum`으로 실패했고 current-source maximum-factor/full Production Economy/static ratchet와 Console Warning/Error `0/0`을 통과했다. ReturnedPackaging은 recipe output이 아니라 container 회수 계약이므로 이 상한에 섞지 않고 별도 tare 보존 불변식으로 검증한다.
- [ ] Domain/Apparel/Workwear/Surgical의 detached capacity source가 proof뿐 아니라 facility definition·position·workstation·cycle·catalog portfolio까지 live와 exact 재투영되도록 공용 restore claim에 연결한다.

이 미완료 범위를 capability 등록·materializer·maximum claim·restore claim이라는 네 공용 계약으로 닫으면, 이후 같은 범주의 신규 콘텐츠는 코어 분기·새 저장 DTO·별도 capacity 계산기를 추가하지 않고 데이터와 capability 구현만으로 편입된다. 이것이 미래 작업량을 줄이는 기준이며, 아직 지원하지 않는 새로운 의미까지 선구현하는 기준은 아니다.

2026-08-27 registered materializer fresh 증거: AssetDatabase refresh/domain reload 뒤 현재 Main·Editor assembly가 오류 없이 컴파일됐다. `ProductionPreparedOutputComponentCodecDebugScenarios`는 비표준 prepared capability의 등록 순서·locale 독립 fingerprint, create/decode exact round-trip과 누락·중복·version drift·비참여 materializer 거부를 통과했다. `ProductionOutputHandlerRegistryDebugScenarios`는 prepared participant가 per-line handler까지 동시에 소유하는 이중 실행 권위를 거부했고, `PreparedOutputLegacyBypassStaticDiagnostics`는 adapter의 Standard/DefinitionOnly 상수 분기 0을 확인했다. 이어 recipe digest, prepared contract, ruined execution, maximum-factor, routing/restore join, destination lifecycle, full persistence, destructive drain participant/preflight와 full Production Economy 11개 회귀가 한 번에 PASS했으며 최종 Console Warning/Error는 `0/0`이다. 이후 authored ruined WIP envelope와 generic/restore raw 제거도 별도 current-source 회귀로 닫혔다. 이 증거로 아직 닫지 않는 범위는 authored-upper-bound restore guard, 전체 detached source 재투영과 asset-backed AIHaul full-path canary다.

### 101.11 authored normal ceiling·공용 detached facility capacity guard — 2026-08-27

- [x] generic normal Main/Byproduct line은 current recipe의 canonical line ID·role·item과 maximum output factor를 대조한다. saved quantity가 authored maximum을 넘으면 exact grams·proof·claim·capacity digest를 함께 다시 만든 경우에도 claim 생성 전에 실패한다.
- [x] ReturnedPackaging은 recipe output ceiling으로 오분류하지 않고 input container의 exact tare-return 계약에 남겼다.
- [x] `IProductionOutputDetachedFacilityCapacityRestoreGuard`를 추가했다. restore-world candidate 외의 live fallback 없이 persistent facility ID가 정확히 하나인지 확인하고 current proof로 facility definition·position·workstation·cycle·process-fluid·catalog source를 재투영한다.
- [x] saved capacity source digest와 required minimum은 exact 비교하며 epsilon·Trim·자동 교정은 없다. candidate 없음, facility 0/duplicate, digest drift와 1g minimum drift focused 회귀가 mutation 없이 실패한다.
- [x] CertifiedSeed V5는 `BuildRestoreCandidate`에서 common proof와 owner facility를 재구성하고 physical `AdoptPending` 전에 공용 detached guard를 호출한다. 새 DTO나 schema bump는 없다.
- [x] Combat V11도 `BuildRestoreCandidate`에서 descriptor×output quantity proof, `ownerFacilityId == facilityPersistentId`, saved outcome fingerprint를 검증한 뒤 공용 detached guard와 physical adoption을 순서대로 실행한다. 새 DTO나 schema bump는 없다.
- [x] 공용 가드를 composition root에 singleton interface로 등록했다. 콘텐츠 ID 분기와 owner별 capacity calculator를 추가하지 않았다.
- [x] current Main/Editor assembly를 강제 refresh해 재컴파일하고 generic maximum-factor/full Production Economy/static raw ratchet, detached guard, domain restore adoption과 Combat craft 회귀를 통과했다. 최종 Console Warning/Error는 `0/0`이다.
- [x] Apparel craft/rejected와 open terminal source order를 CharacterEnvironment detached build에 연결하고 ModularFacility dependency를 명시한다. `ApparelOutputDetachedCapacityRestoreGuard`가 live order와 아직 source-terminal receipt가 없는 terminal source만 검사하고, 현재 capability proof와 detached facility source를 exact 재투영한다. 이미 닫힌 역사적 receipt는 현재 capacity owner로 다시 잡지 않는다.
- [ ] Workwear/Surgical의 exact pending owner와 terminal drain을 같은 guard에 연결한다. 현행 unregistered Apparel terminal-drain save section은 production 등록 여부를 먼저 결정·검증한다.
- [x] Combat save payload의 facility ID·outcome·proof digest/max·capacity digest/minimum tamper가 runtime mutation과 physical acknowledgement 전에 실패하는 전용 회귀를 추가했다. production constructor는 `[Inject]` 5-arg로 고정하고 필수 의존성의 optional default를 제거했다. 모든 owner를 먼저 전수 preflight한 뒤에만 2차 pass에서 `AdoptPending/RequireNoPending`을 호출하므로 뒤쪽 row 변조도 앞쪽 partial adoption을 만들지 않는다. missing/duplicate/destroyed detached facility는 같은 실제 common guard matrix가 보완한다.
- [ ] multi-unit ruined QA recipe의 bill-retirement terminal `9,600g` detached 회귀와 asset-backed AIHaul full-path canary를 추가한다.

이 체크포인트로 common validator와 CertifiedSeed/Combat hook은 닫혔다. 상위 `Domain/Apparel/Workwear/Surgical detached source` 체크는 나머지 owner adapter와 전용 fault matrix가 끝날 때까지 OPEN이다. Batch A~H는 `0/8`, weighted remaining은 약 `29~39%`다.

authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값은 변경하지 않았다. Batch A~H는 `0/8`, output owner는 구조상 `6/6`, input owner는 다음 fresh manifest 전까지 `3/39`(remaining `36`, orphan `1`)이며 raw capacity bypass는 `0`이다. weighted remaining은 `29~39%`다.

2026-08-27 Apparel detached-source 추가 증거: `CharacterEnvironmentSaveSection`은 `world.facilities`를 명시적으로 선행하고, persistence candidate 생성 전 Apparel output-capacity guard를 실행한다. 공용 guard focused matrix와 Apparel physical/rejected/terminal/repair 4개 회귀가 현재 Unity assembly에서 PASS했다. 더 넓은 EnvironmentalField aggregate는 이번 변경과 무관한 구형 `wildlife.population` V5 fixture 때문에 별도 FAIL로 남겼으며 이를 Apparel 통과 증거에 포함하지 않았다. clean focused 재실행 후 Console Warning/Error는 `0/0`이다. Workwear/Surgical detached-source adapter는 계속 OPEN이다. Combat 전용 tamper matrix는 후속 current-source 체크포인트에서 닫혔다.

### 101.12 generic ExactCapability pending-output durable envelope — semantic registry·Workwear/Surgical·downstream lifecycle 구현

Workwear와 Surgical은 domain owner가 아니라 generic bill의 `resolvedOutputs`/`ExactCapability` 경로다. 현재 bill은 frozen capability·quality·`pendingCommitId/pendingCommitApplied`와 누적 mass만 저장하며, pending 한 단위가 publication 당시 사용한 proof·capacity·exact stack 집합을 독립적으로 소유하지 않는다. incoming physical batch도 outcome/planned fingerprint와 stack shape는 제공하지만 proof와 capacity source의 구성 필드를 제공하지 않는다. 따라서 handler replay가 나중에 우연히 실패하는 것만으로 detached restore 원자성을 증명했다고 보지 않는다.

- [x] `ProductionResolvedOutputSaveData`에 공용 `pendingOutputPublication` envelope를 추가하고 Production current format을 V20→V21로 올린다. 도메인별 Workwear/Surgical 필드는 추가하지 않는다.
- [x] envelope 선행 조건으로 Workwear의 publication과 acknowledgement를 분리했다. handler는 physical batch/admission commit까지만 성공시키고 generic bill이 owner를 freeze한 뒤 호출하는 `TryAcknowledge`만 marker를 닫는다. Surgical은 이미 같은 2단계 순서를 사용한다.
- [x] envelope는 pending unit 하나의 owner stable ID, facility ID, frozen capability, maximum proof digest/max grams, capacity source digest/required minimum, exact output mass, outcome/planned fingerprint, destination/position/capacity revision, publication/admission/ack phase와 exact stack IDs를 가진다.
- [x] `IIdempotentProductionOutputHandler`의 mass-only 조회를 registered committed-output snapshot 조회로 확장했다. handler가 공용 snapshot을 반환하고 generic bill이 그대로 freeze하며, coordinator에 capability/content ID 분기를 추가하지 않는다.
- [x] 실행 순서를 `BeginResolvedOutputUnit → handler publication → pending envelope freeze → MarkResolvedOutputUnitCommitted → handler acknowledgement → ClearResolvedOutputPendingCommit`으로 고정했다. clear는 envelope와 pending unit을 한 transaction으로 함께 비운다.
- [x] crash state는 정확히 둘만 허용한다. A: unacknowledged planned batch가 존재, B: planned marker는 ack됐고 commit-tagged physical output이 존재하지만 bill clear 전이다. 둘 다 존재하거나 둘 다 없으면 실패한다.
- [x] `ProductionExactCapabilityOutputRestoreJoin`은 `ProductionBillsSaveSection.BuildCandidate`에서 physical·facility dependency가 staged된 뒤 persistence/live publication 전에 실행한다. 모든 owner의 proof·detached facility capacity·physical batch/output을 먼저 전수 preflight하고 전체 성공 뒤 acknowledgement plan을 만든다.
- [x] pending `production-output:*` marker는 reverse scan하여 정확히 하나의 active Production owner가 없으면 거부한다. prepared-output owner와 domain-output owner는 각 기존 join의 단일 권위를 유지한다.
- [x] acknowledged marker는 active producer lease가 아니라 durable provenance receipt이므로 `Production owner 없음`만으로 orphan 처리하지 않는다. 등록된 save/registry preflight는 generic acknowledged ownerless terminal receipt와 unknown domain delegation을 허용하고, prepared `production-output-batch:*`에는 정확히 하나의 routing batch owner를 요구하며 provenance와 exact-route outbox의 dual custody, routing orphan, malformed/partial marker를 mutation 없이 거부한다. `FacilityOutputBuffer/Loose/Stored/FacilityBuffer/Carried`의 세부 상태 불변식은 기존 Physical/Character/routing join의 단일 권위를 재사용한다.
- [x] stable capability ID로 dispatch하는 `IProductionResolvedOutputRestoreCapabilityValidator` registry를 두었다. production composition에서 모든 `IIdempotentProductionOutputHandler`와 validator의 capability/version/codec이 정확히 1:1인지 강제하므로 신규 같은 의미 capability는 validator/descriptor 등록만으로 연결되고 generic coordinator 수정은 0이다.
- [x] Workwear validator는 current recipe/material/quality/facility와 frozen envelope로 outcome, proof, capacity, unique instance/component signature, qty/mass/destination을 exact 재계산한다. 신규 의복 ID 분기는 없으며, raw business component와 prepared fingerprint를 restore snapshot에 보존하고 동일 admission projector의 순수 projection을 재사용한다.
- [x] Surgical cross-aggregate preflight validator는 saved prepared-part commit과 physical surgical component의 partId/node/kind/quality/worldStack을 exact join한다. `SurgicalPartInstance.storedFacilityId`는 생산 facility owner로 재사용하지 않는다. 공용 capability registry 편입 여부와 무관하게 restore publish 전 원자 preflight로 등록되어 있다.
- [x] focused 실패 행렬은 proof digest/max, capacity digest/min ±1g, missing/duplicate/destroyed facility, outcome/planned fingerprint의 개별·paired 변조, item/qty/mass/destination/stack/component, owner-without-physical, pending physical-without-owner, A/B crash phase와 late-row read-only preflight를 포함한다.
- [x] acknowledged downstream lifecycle preflight의 save/registry 양 경로는 generic ownerless terminal PASS, pending generic delegation PASS, prepared routing/provenance와 prepared exact-route PASS, malformed/partial, prepared owner 누락, provenance+exact-route dual custody와 routing orphan FAIL, 입력 JSON mutation `0`을 통과했다. preflight는 non-failing participant publish에 넣지 않고 restore publish 전 registry preflight로 등록했다. 기존 Physical item report에서도 `reservation_carried_grandfather_restore`, `reservation_expired_committed_carry_restore`, `equipment_identity_across_carry_and_storage`는 PASS했다.
- [ ] acknowledged generic marker를 실제 Carried stack·haul intent와 한 whole-root fixture에 동시에 둔 통합 canary는 asset-backed full-path canary와 함께 남긴다. 현재 broader `PhysicalItemDebugScenarios`의 유일 실패는 이 변경과 무관한 `equipment_unique_retail_transfer_commit_and_rollback`의 stale lease 충돌 1건이며, 이를 lifecycle PASS에 포함하거나 숨기지 않는다.

이 envelope는 미래 ExactCapability handler를 위한 공용 다형성 경계다. 새 콘텐츠가 기존 capability의 파라미터 추가라면 데이터만 추가하고, 새 상태 의미라면 handler+validator+contract suite만 등록한다. 새 capability마다 `ProductionBillRuntime`·save section·DTO에 분기를 추가하는 방식은 실패다.

2026-08-27 current-source 증거: V21 envelope/full-current-format round trip, common exact-output restore join, terminal destructive-drain lifecycle과 Surgical cross-aggregate preflight를 Unity에서 실행했다. 이어 registered semantic-validator registry, Workwear와 Surgical semantic validator를 production composition에 연결했다. Workwear는 실제 recipe/material/apparel catalog와 공용 maximum/capacity/admission projection을 사용해 Crash A/B, paired outcome/planned tamper, recipe/material/quality/component/day/hash/mass/facility/destination/capacity 변조와 late-row DTO·handler 무변경을 통과했다. Surgical은 pending/acknowledged, paired fingerprint, node/kind/quality/commit/component/prepared fingerprint/mass/facility/destination/capacity 변조와 projection/input snapshot 무변경을 통과했다. Surgical handler와 maximum capability의 3개 item ID 분기를 공용 prosthetic semantic parser로 교체해 `item:prosthetic:arm:right` 같은 같은 의미의 미래 정의가 코어 수정 없이 같은 등록 경로를 사용하는 회귀도 통과했다. acknowledged lifecycle save/registry preflight와 routing/full-current-format persistence를 추가한 current-source 10-suite 묶음이 PASS했고 최종 Console Warning/Error는 `0/0`이다. asset-backed full-path canary와 explicit acknowledged-Carried 통합 canary는 계속 OPEN이므로 Batch A~H는 `0/8`이다. authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값은 이 체크포인트에서 변경하지 않았다.

2026-08-27 fresh static manifest: FacilityBuffer input owner는 `3/39` migrated, remaining `36`; output owner는 `6/6`; legacy bypass `5`, orphan `1`, unclassified/stale `0/0`; 실제 non-Editor delivery invocation은 `59회/39파일`로 generator 기대값과 일치한다. public/concrete raw-capacity API/caller는 `0/0`이고 unexpected concrete projector caller도 `0`이다. 이번 semantic/lifecycle 체크포인트는 구조적 안전성과 미래 콘텐츠 작업량을 줄였지만 input owner migration 수를 바꾸지 않았으므로 큰 Batch A~H는 계속 `0/8`이다.

2026-08-27 multi-unit ruined terminal blocker: 기존 `2,400g WIP + 600g clean water - 300g wastewater → 4×600g waste`, `9,600g` capacity fixture는 private이며, bill retirement 뒤 frozen `sourceBill`은 terminal payload에 남지만 durable capacity projector가 이를 입력받지 않는다. 현재 routing fallback은 1g minimum 축소는 막아도 다른 canonical SHA-256으로 proof/claim/capacity digest를 함께 바꾼 drift를 current recipe·WIP/fluid에서 재계산하지 못한다. 따라서 가짜 독립 fixture로 체크하지 않는다. 다음 구현은 terminal frozen sourceBill↔routing ownerBillId exact join, `CaptureRuinedClaim`/`CaptureSource` 재투영과 공용 immutable QA fixture 추출을 먼저 수행해야 한다.

2026-08-27 multi-unit ruined terminal 해결 증거: generic terminal payload를 durable capacity projector의 current-format 필수 입력으로 승격하고, live bill이 없는 routing owner를 terminal의 frozen `sourceBill`과 bill/recipe/facility/cycle/destination/batch/outcome/physical-line 단위로 exact join한다. join 뒤 현재 recipe와 frozen WIP/fluid에서 `CaptureRuinedClaim`과 `CaptureSource`를 다시 실행하고 maximum proof·claim·capacity source·cycle/portfolio/minimum을 exact 비교한다. 공용 fresh-object fixture가 `2,400g WIP + 600g clean water - 300g wastewater → 4×600g waste`, `9,600g`을 증명하며 WIP drift, canonical digest 동시 교체와 `+1g` minimum 변조를 입력 mutation 없이 거부한다. current-source Unity compile과 ruined-terminal/extended exact-output·restore 묶음이 PASS했고 최종 Console Warning/Error는 `0/0`이다. 이로써 terminal capacity blocker만 닫으며 asset-backed AIHaul full-path canary와 Batch A 전체는 계속 OPEN이다.

## 100. 구현 체크포인트: 품질 미달 장비 판매의 durable settlement

상태: **하위 P0 완료 / Batch A 전체는 진행 중**.

### 100.1 닫은 범위

- [x] 품질 미달 판매를 `Prepared → PhysicalCommitted → IncomePublished → UniqueAuthorityReleased`의 단조 증가 current-format outbox로 전환했다.
- [x] source stack ID, item/instance ID, component fingerprint, 시장 destination, proceeds, Transfer commit ID, quantity와 input grams를 하나의 immutable 요청으로 결속했다.
- [x] 물리 제거는 exact `Transfer` 영수증이 생긴 뒤에만 다음 단계로 진행한다. `Prepared`는 저장 불가 transient이며, 영수증 없는 owner와 owner 없는 영수증을 모두 거부한다.
- [x] Combat unique aggregate는 `MarketSalePending` external custody를 사용한다. 판매가 끝나기 전 진화·장전·탄약 소비·내구·수리·정비·materialize/drop·lost·loadout과 하위 repository 변조를 거부한다.
- [x] treasury credit은 transaction kind와 operation ID로 idempotent하다. ledger 기록 실패 시 balance를 원복하고, exact replay는 무변경이며 amount conflict는 실패한다.
- [x] stock-policy·Physical section restore join과 whole-save preflight가 physical receipt, treasury income, Combat pending/terminal authority를 phase별로 exact 검증한다.
- [x] 실제 `DungeonSaveSectionRegistry.RestoreAll`에서 정상 payload가 원자 복원된다. 같은 payload에서 physical receipt를 제거하면 전체 복원이 실패하고 임시 physical candidate는 남지 않는다.
- [x] Unity current-source compile, durable outbox, 공용 domain publication/restore guard, exact unique physical sale 회귀가 통과했고 최종 Console Warning/Error는 `0/0`이다.

### 100.2 해석 제한과 다음 gate

- 이 체크포인트는 authored kg·quantity·capacity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값을 변경하지 않는다.
- 이 거래 경계는 미래의 품질 판정 콘텐츠가 같은 capability/command 계약을 선언적으로 사용하도록 닫았지만, 새로운 경제 불변식이나 전혀 다른 결제 capability까지 미리 구현하지 않는다.
- Combat/Apparel의 inspiration·mood completion-effect exact-once 계약은 별도 P0다. 판매 settlement 완료를 그 효과 계약의 완료로 세지 않는다.
- Batch A~H는 큰 체크포인트이므로 계속 `0/8`이다. weighted remaining은 `32~42%`로 갱신하며, 다음은 generic 미이관 output family·environmental workwear remaining 2 제거와 synthetic full-path canary다.

## 102. 구현 체크포인트: schema-4 synthetic full-path canary identity·mid-carry 원자성

상태: **생성 에셋·요청 identity와 중간 운반 복원 코드는 current-source compile/focused PASS / 실제 PlayMode full-path는 dirty scene 때문에 OPEN / Batch A 진행 중**.

### 102.1 닫은 범위

- [x] canary request를 transaction nonce, 임시 item/recipe asset GUID, 증강된 item/domain catalog SHA-256, transaction/verifier source SHA-256에 exact 결속했다. request·marker·현재 project 중 하나라도 다르면 bill 생성 전에 fail-loud한다.
- [x] schema-4 contract round trip과 catalog-focused transaction이 exact identity를 승인하고 stale nonce를 거부한다. cleanup은 임시 에셋과 marker/request를 제거하고 양 catalog byte를 원본과 exact하게 복구한다.
- [x] AIHaul pickup 뒤 checkpoint 대상은 destination의 첫 intent가 아니라 persistent actor, destination, item, quantity가 일치하는 단 하나의 committed intent로 제한한다.
- [x] checkpoint에서 commitment↔Carried를 carried stack ID, source stack ID, item ID, stack signature, quantity로 exact join하고, warehouse admission을 warehouse/source/item/quantity/grams로 결속한다.
- [x] `RestoreAll` 실패 원자성은 특정 다섯 section만이 아니라 캡처된 모든 non-null envelope를 `sectionId` ordinal 정렬하고 version·restore phase·optional·payload까지 결속한 whole-root fingerprint로 비교한다. 별도의 다섯 section fingerprint는 정상 mid-carry semantic round-trip 비교에만 사용한다.
- [x] 저장된 warehouse admission을 `+1g` 변조한 negative arm은 `RestoreAll=false`여야 하며, 실패 뒤 live fingerprint·Carried cargo·haul intent·warehouse reserved inbound grams가 모두 prior-world와 exact 동일해야 한다.
- [x] 정상 arm은 같은 untampered checkpoint를 복원하고 restored actor를 AI-paused 상태로 고정하되 movement는 취소하지 않은 채 Brain·AbilityHaul·AbilityMove가 inert인지 먼저 증명한다. 이후 restored actor가 이미 가진 authored `AIHaul` action만 격리하고 `PreferActionOnNextDecision`·unpause·`RequestImmediateDecision`으로 production Brain 경로를 깨워 동일 deposit을 재개한다. `new AIHaul().Execute`와 generic quiescence의 `StopHauling`은 이 경로에서 사용하지 않는다.
- [x] current Unity project assembly가 재빌드됐고 schema-4 contract/catalog, acknowledged-output lifecycle, prepared routing restore focused suite가 PASS했다. 최종 Console Warning/Error는 `0/0`이다.

### 102.2 아직 열린 exit gate

- [ ] active `GameplayScene`이 사용자 소유 dirty 상태가 아니게 된 뒤 full transaction을 실행한다. 검증기는 dirty scene을 저장하거나 unload하지 않고 사전 거부해야 한다.
- [ ] 실제 report에서 bill→FacilityOutputBuffer→exact route→committed Carried checkpoint→tamper atomic reject→valid RestoreAll→resumed AIHaul→gram warehouse→post-deposit save/restore→UI/audit를 전부 current transaction/source digest로 PASS한다.
- [ ] transaction 전후 GameplayScene asset/meta, 두 catalog, persistent save root가 exact 복구되고 임시 asset/meta/marker/request가 0인지 확인한다.
- [ ] normal·capacity wait·cancel·destroyed와 Batch G의 partial/Downed/Dead/Floor Clutter 행은 각각 실제 PlayMode 증거가 생길 때까지 OPEN으로 둔다. 이번 mid-carry restore 코드 compile을 해당 fault matrix 전체 PASS로 세지 않는다.

이번 checkpoint는 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값을 변경하지 않았으므로 `밸런스 영향 없음`이다. Batch A~H는 `0/8`, 전체 진행은 약 `65~73%`, 잔여는 약 `27~35%`를 유지한다. 새 콘텐츠가 기존 definition-only capability를 사용하면 canary 코어 분기 추가 없이 catalog 자동 수집과 동일 identity/restore 계약을 거쳐야 하며, 새로운 상태 의미가 필요한 경우에만 새 capability·codec·validator fixture를 등록한다.

2026-08-27 current-source 교정 증거: 모든 envelope whole-root fingerprint와 실제 Brain wake 경로가 컴파일됐고, inert gate 전에 movement를 취소하던 증거 오염도 제거했다. `Assembly-CSharp-Editor.dll`은 `2026-08-27T13:02:15Z`에 재빌드됐으며 그 copy 경계 뒤 compiler error는 `0`이다. schema-4 contract/catalog, acknowledged lifecycle, prepared routing restore focused bundle가 PASS했고 Console Warning/Error는 `0/0`이다. 실제 coroutine PlayMode report는 dirty GameplayScene 보호 때문에 여전히 OPEN이다.

## 103. 구현 체크포인트: synthetic output-space exact retry verifier

상태: **실제 권위 기반 verifier 구현·current-source compile/focused PASS / PlayMode 실행 증거 OPEN / Batch A 진행 중**.

### 103.1 구현·컴파일로 닫은 범위

- [x] synthetic `20,000g` batch가 사용하는 `80,000g` FacilityOutputBuffer에 필요한 blocker 질량을 `live capacity - batch mass + 1`로 계산한다. 특정 item ID·현행 kg를 고정하지 않고 current catalog의 canonical·generic-definition·선형 질량 후보를 stable ID 순으로 수집해 최소 수량 정수 DP로 exact `60,001g` 조합을 결정한다. 후보 digest와 선택 plan을 기록하고 exact 조합이 없으면 `EXACT_MASS_UNREPRESENTABLE` 의미로 fail-loud한다.
- [x] solver가 선택한 plan을 실제 `SpawnItemAt` 경로로 생성한 뒤 새 stack의 component-aware 질량을 다시 측정한다. plan mass, runtime stack mass, occupancy, capacity와 reserved mass가 모두 일치해야 하므로 향후 item kg 조정은 verifier 코어 수정 없이 다른 exact plan으로 자동 흡수되고 runtime mass drift는 별도로 실패한다.
- [x] blocker는 `BeginWork`가 input을 WIP로 소비한 뒤 첫 `ExecuteWork` 전에 동기적으로 생성한다. 따라서 cycle-start capacity preflight를 통과한 뒤 completion 시점에만 공간이 부족한 실제 경계를 검사한다.
- [x] 첫 실패는 `ProductionOutputSpaceUnavailable`, bill `WaitingForOutputSpace`, prepared phase `ResolvedWaitingForOutputSpace`, `20,000g` frozen batch, non-empty batch commit/outcome fingerprint를 요구한다. material buffer는 이미 0이고 WIP commit/quantity/grams/cycle은 BeginWork 직후와 exact 동일해야 한다.
- [x] blocker가 남은 상태에서 `ExecuteWork(..., 0f)`를 재호출하고 prepared-output JSON, WIP, item-stack version, occupancy, input buffer와 custody stack 수가 모두 무변경인지 검사한다. 이 검사는 입력 재소비·출력 spawn·결과 재굴림을 저장 권위와 물리 repository 양쪽에서 동시에 막는다.
- [x] blocker 제거는 repository 직접 삭제가 아니라 solver가 선택한 exact stack vector를 입력으로 하는 typed `PhysicalItemDispositionKind.Sink` commit을 사용한다. receipt 수량은 선택 plan 총수량과 같고 질량은 `60,001g`이어야 하며, 제거 뒤 occupancy `0g`, capacity `80,000g`, reserved `0g`이어야 한다.
- [x] 공간 확보 뒤 같은 bill을 `0 WU`로 재개하고 정확히 한 `20개/20,000g` publication stack이 frozen batch commit ID·outcome fingerprint를 그대로 가지는지 공개 component payload에서 확인한다. Editor verifier가 runtime `internal` codec에 의존하지 않도록 component schema를 read-only로 검증한다.
- [x] 첫 exact Waiting 상태에서 실제 `IDungeonSaveSectionRegistry.CaptureAll → RestoreAll`을 수행한다. 복원 전 whole-root fingerprint와 frozen bill/prepared/WIP/blocker를 캡처하고, 복원 후 작업자·생산 시설·창고·bill·blocker stack을 persistent ID로 다시 조회해 stale Unity 참조를 재사용하지 않는다. 복원된 상태가 byte-identical이고 여전히 정확히 1g 부족한 경우에만 zero-WU retry·typed clear·same-batch resume를 진행한다.
- [x] pickup 전 cancel은 exact quantity lease·warehouse admission·intent를 캡처한 뒤 lease/intent만 해제하고 admission tombstone `Released/CancelledBeforePickup`과 routable physical stack을 보존하는지 검사한다. committed pickup 뒤 active actor cancel은 coroutine·movement를 종료하되 carried stack·동일 lease·Reserved admission·delivery-only intent를 유지하는지 독립 검사한다.
- [x] `AbilityHaul.DebugBeforeHaulRoutineStart`가 동기 cancel이나 예외를 일으킨 뒤 coroutine을 되살리지 않도록 active-plan guard와 typed cleanup을 추가했다. 예약 snapshot은 nullable value type으로 정확히 처리해 source-only false-green과 compile blocker를 제거했다.
- [x] current-source Unity compile이 완료되어 `Assembly-CSharp-Editor.dll`이 `2026-08-27T13:56:27.1877873Z`, `9,221,120 bytes`, SHA-256 `6ADC53EB...DC94C`로 재생성됐다. schema-4 contract/catalog, acknowledged lifecycle와 routing restore focused bundle가 다시 PASS했고 Console Warning/Error는 `0/0`이다.

### 103.2 아직 열린 exit gate

- [ ] dirty `GameplayScene`을 저장·unload·덮어쓰지 않고 실행할 수 있는 시점에 synthetic PlayMode transaction을 수행해 위 여섯 capacity-wait check ID의 실제 PASS report를 얻는다.
- [ ] 실제 report가 생성되기 전에는 상위 `output 공간 1g 부족 → 같은 결과 재개` 체크와 Batch A를 닫지 않는다.
- [x] pickup 전 cancel과 committed active-actor cancel의 독립 source 행, exact lease/admission/intent assertion과 coroutine resurrection guard를 구현하고 current-source compile을 통과했다.
- [ ] 실제 PlayMode report에서 `PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_RELEASES_ONLY_LEASE`, `PREPARED_OUTPUT_CANARY_ACTIVE_CANCEL_RETAINS_CARRIED_AUTHORITY`, `PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_RESTORE_EXACT`을 포함한 cancel·restore 행이 PASS해야 한다. 파괴 시도 차단은 `DESTRUCTIVE_ATTEMPT_BLOCKED_BY_LIVE_AUTHORITY`로만 보고하며 실제 `destroyed=PASS`로 오인하지 않는다.
- [ ] 실제 active-output 시설의 durable drain 뒤 성공 제거는 Batch G/destructive-loss P0에서 exact five participant·journal·coordinator·replay/restore 증거로 별도 구현한다.

이번 checkpoint는 verifier code만 바꿨고 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값은 변경하지 않았다. Batch A~H는 `0/8`, 전체 진행은 약 `65~73%`, 잔여는 약 `27~35%`다.

## 104. 구현 체크포인트: ReservedTarget exact kg admission과 원정 보급 소유권

상태: **공통 계약·원정 보급 구조·current-source 집중 검증 완료 / 실제 PlayMode 원정 운반 증거 OPEN / Batch C 진행 중**.

### 104.1 닫은 범위

- [x] `ReservedTarget`의 exact claim/profile만 `ownerFacilityId=null`을 허용한다. `LiveFacility`, `LiveBuilding`, planned-output과 빈 문자열·비정규 ID는 계속 fail-loud한다.
- [x] `expedition:` 목적지는 더 이상 위치·prefix만으로 FacilityBuffer 입고를 허용하지 않는다. 정확한 질량 admission profile과 token이 없으면 `WorldItemWarehouseService`가 commit 전에 거부한다.
- [x] `offense.expedition-supply`는 package cost vector를 current immutable item mass catalog로 재투영해 exact maximum grams를 만든다. 새 gram save field나 DTO version은 추가하지 않고 기존 semantic cost authority를 사용한다.
- [x] package 생성·restore·return·consume가 `IFacilityBufferDestinationLifecycleCommand`의 claim+capacity pair를 원자적으로 publish/retire한다. claim-only publication과 terminal profile 누수를 제거했다.
- [x] 비용 합산은 checked arithmetic을 사용하고 exact item/quantity mass를 계산한다. 부분 delivery는 물리 staging owner를 유지한 retryable package로 남으며 결과를 재굴림하거나 기존 요청을 찢지 않는다.
- [x] `FacilityBufferMassAdmissionDebugScenarios`에 ReservedTarget null-owner 정상, LiveFacility/LiveBuilding null 거부, 빈 owner 거부, profile/request owner mismatch 무변경을 추가했다.
- [x] `OffenseStrategicDebugScenarios`의 11개 시나리오가 exact 2,000g profile, restore prepare→claims→capacity publish와 역순 rollback, cancel·consume terminal absence를 검증한다.
- [x] 호출자 없는 Blueprint 직접 materialization API와 orphan Building stack-port FacilityBuffer API 두 개를 제거했다. 실제 P0 우회 경로를 지우지 않고 dead surface만 삭제했다.
- [x] current-source Unity compile 후 공통 admission, Offense 11-scenario, owner manifest 묶음이 PASS했고 Console Warning/Error는 `0/0`이다.
- [x] owner manifest를 두 번 생성해 byte/hash/mtime 변화 0을 확인했다. input은 `4/39 migrated`, remaining `35`; output은 `6/6`; bypass `4`, orphan `0`, unclassified `0`이다.

### 104.2 결정론적 증거

- CSV SHA-256: `7A5928C23A2403D0C2795BA5E836103948E8F0C240A340562CB2C93BAE1988FB`
- report SHA-256: `989A8253C00F1F4E76921C9A225CA10350ACD74BE84D5942FF1BD69F53357788`
- source digest: `8e1a77fc718afc783fb506977eae8d95cc557f1fa8a150ab3b5f6a19ae577d80`
- 실제 non-Editor delivery invocation: `59회 / 39파일`; classification gate `PASS`, full migration gate `OPEN`.

### 104.3 열린 exit gate

- [ ] dirty `GameplayScene`을 저장·unload하지 않는 별도 안전 환경 또는 깨끗한 live scene에서 원정 주문→warehouse lease→AIHaul→ReservedTarget FacilityBuffer→consume/return의 실제 PlayMode 경로를 통과한다.
- [ ] 남은 input owner `35`와 활성 bypass `4`를 공통 capability/lifecycle 계약으로 이관한다. 신규 콘텐츠 ID 분기나 owner별 gram 저장 필드를 추가하지 않는다.
- [ ] Batch A full-path canary, Batch B 잔여 capacity, Batch D~H의 전수 kg 적용·EWU/가격·6인 생존망은 계속 OPEN이다.

이 체크포인트는 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값을 변경하지 않았다. 따라서 `밸런스 영향 없음`이다. 큰 Batch A~H는 `0/8`; 전체 진행은 약 `66~74%`, 잔여는 약 `26~34%`다.

## 105. 구현 체크포인트: unique output identity와 컨베이어 도착 exact gram admission

상태: **unique planned-output identity와 컨베이어 도착 원자성은 current-source compile/focused PASS / 컨베이어 durable route-target·두 PlayMode 경로는 OPEN / Batch C 진행 중**.

### 105.1 닫은 범위

- [x] planned-output publication은 item definition·quantity·component뿐 아니라 frozen `ItemInstanceId`까지 exact 비교한다. unique output의 instance ID를 바꾼 receipt는 `TokenMismatch`로 거부되며 routed token과 물리 출력을 변경하지 않는다.
- [x] Apparel restore receipt와 Surgical prepared-output fixture가 실제 instance ID를 공용 receipt에 전달한다. 전투·의복·수술의 unique physical identity가 같은 admission 계약을 사용한다.
- [x] `IItemTransferService.TryCompleteTransitToFacilityBuffer`를 추가하고 `ConveyorItemGateway.TryCompleteToFacility`를 이 전용 경로로 전환했다. 범용 `TryCompleteTransit`은 이제 `Loose`만 허용하며 Stored·FacilityBuffer·FacilityOutputBuffer 직접 전환을 fail-loud한다.
- [x] 컨베이어 도착은 현재 exact destination claim/profile을 조회하고 InTransit whole lot의 current reservation revision·component-aware gram을 예약한다. 물리 상태를 FacilityBuffer로 바꾼 뒤 같은 fingerprint/mass를 commit하며, capacity 부족은 `ConveyorPortFull`, profile/owner 불일치는 `ConveyorDestinationUnavailable`이다.
- [x] commit 이전 실패는 physical record를 stack ID·instance·item·quantity·components·position·state·destination·source storage·reservation fields까지 복구하고 transient reserved grams를 0으로 만든다. 화물은 원격 반환하지 않고 같은 InTransit payload에 남는다.
- [x] focused real-repository 회귀는 profile 누락, `4,800g > 3,600g` 과적, 범용 API 우회, physical mutation 뒤 commit 직전 fault rollback, `2,400g` 정상 arrival, exact-once replay 거부와 destination occupancy `2,400g`을 검증한다.
- [x] current Unity assemblies에서 Physical Stock, domain output publication, full Production Economy와 owner manifest 묶음이 PASS했고 최종 Console Warning/Error는 `0/0`이다.
- [x] owner manifest 두 번째 생성은 byte/hash/mtime 변화 0이다. 현재 정직한 집계는 input `4/39 migrated`, remaining `35`, output `6/6`, bypass `4`, orphan `0`, unclassified `0`이다.

### 105.2 과대 계상 방지와 열린 exit gate

- [ ] 컨베이어 payload V3는 아직 raw `destinationId`만 저장한다. route 선택 시의 drop position, owner domain/operation/facility, anchor kind, max grams, capacity revision과 route-target fingerprint를 동결·복원 exact join하지 않으므로 `infrastructure.conveyor`는 아직 `bypass`로 유지한다.
- [ ] `IndustrialInfrastructurePlayModeVerifier`의 synthetic `qa:industrial-output`은 자체 exact claim/profile을 publish하고 실패 시 payload·InTransit을 유지한 뒤 capacity 확보 후 동일 payload가 exactly-once 도착하는 실제 runtime 증거가 필요하다.
- [ ] `ProductionBuildingPlayModeVerifier`의 `production-sensor:{facilityId}`는 production stock-sensor owner가 아직 capacity profile을 소유하지 않는다. 임시 무제한 QA profile로 숨기지 않고, 해당 owner의 실제 maximum gram·lifecycle·restore 계약을 이관한 뒤 PlayMode를 다시 통과한다.
- [ ] active user-owned dirty `GameplayScene`을 저장·unload하지 않는 안전한 시점에 위 PlayMode 경로를 실행한다. focused service 호출을 live ConveyorRuntime·production sensor 실행 증거로 세지 않는다.
- [ ] 남은 input owner `35`, bypass `4`, 전수 kg 적용, EWU·가격 재생성, 6인 생존망과 Batch A~H 최종 회귀는 계속 OPEN이다.

### 105.3 결정론적 증거

- owner manifest CSV SHA-256: `F27DB211652676C7945F9AF3F96912E5ED9121B6438BD761687BB0684FD3BD10`
- owner manifest report SHA-256: `180782275D298D176741F382D4F83069586B1318584F659399E014CF46854D97`
- owner manifest source digest: `835fdcca1a8c3f0f6b5c17125c6133e570956ca3ee6f474634b78eaca683f07d`
- current `Assembly-CSharp.dll`: `9,300,992 bytes @ 2026-08-27T15:12:51Z`
- current `Assembly-CSharp-Editor.dll`: `9,231,360 bytes @ 2026-08-27T15:16:39Z`

이 체크포인트는 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값을 변경하지 않았다. 따라서 `밸런스 영향 없음`이다. 미래 Parameter/Composed content는 같은 claim/profile/admission 계약으로 자동 참여하지만, 새로운 route ownership 불변식은 명시적 capability 개정 없이는 fail-loud한다. 큰 Batch A~H는 `0/8`; 전체 진행은 약 `67~75%`, 잔여는 약 `25~33%`다.

## 106. 2026-08-28 컨베이어 동적 intent·양방향 custody 교정

미래 콘텐츠를 모두 선구현하지 않는 새 완성 기준에 따라, 컨베이어 목적지의 owner/profile tuple을 저장 DTO에 새로 동결하지 않는다. 현재 V3 계약은 `destinationId`를 durable intent로 보존하고 복원 뒤 현재 topology와 FacilityBuffer 권위에서 경로 및 입고 승인을 다시 계산한다. 따라서 저장 버전 변경 대신 현재 capability의 실제 불변식만 닫는다.

- [x] V3 payload ID, stack ID, 현재 segment ID와 선택 destination의 non-canonical whitespace를 거부한다.
- [x] 두 payload가 같은 물리 stack을 소유하는 restore를 fail-loud한다.
- [x] `ConveyorPayloadSaveData ↔ WorldItemStackSaveData(InTransit)`를 양방향 exact cardinality로 검증한다.
- [x] 현재 segment가 복원 시설 집합에 존재하는지 검증한다. 합법적으로 제거될 수 있는 previous segment에는 존재 join을 강제하지 않는다.
- [x] top-level restore와 direct registry restore 모두 같은 custody preflight를 거치게 등록한다.
- [x] Industrial Editor 회귀에서 non-canonical ID, duplicate stack owner, missing stack, orphan InTransit, owner mismatch, missing current segment를 거부한다.
- [x] 실제 Industrial PlayMode 검증 소스에 출력 시설별 canonical QA destination, exact 2-unit block capacity, payload 보존, 3-unit capacity 재게시 후 exact-once retry, gram occupancy, scoped payload 판정과 whole-registry byte-equivalent cleanup을 연결한다.
- [x] 기존 QA owner rows를 보존한 owner-scoped 게시와 전체 save baseline 복원으로 terminal admission receipt 및 사용자 world state 잔존을 방지한다.
- [x] Unity 현재 소스 컴파일과 Industrial/Physical Stock/Domain Output/Production Economy/manifest 묶음이 `V27_CONVEYOR_DYNAMIC_INTENT_BUNDLE_PASS`; Console Warning/Error `0/0`.
- [ ] 실제 Industrial PlayMode 코루틴 실행 증거. 현재 사용자 소유의 dirty `GameplayScene`을 저장·언로드·덮어쓰지 않기 위해 실행하지 않는다.
- [ ] 위 live 증거 전까지 conveyor owner row는 `bypass`로 유지한다. 컴파일된 검증기만으로 migrated 처리하지 않는다.

이 교정은 새 save schema와 frozen route abstraction을 제거해 예상 작업량을 줄인다. kg, capacity, quantity, BOM, WU, EWU, 가격, ScriptableObject, prefab, scene authored 값은 바꾸지 않았다(`밸런스 영향 없음`). 현재 고정 체크포인트는 input `4/39`, remaining `35`, output `6/6`, bypass `4`, orphan/unclassified `0/0`, Batch A~H `0/8`, 전체 약 `68~76%` 완료 / `24~32%` 남음이다. 다음 P0는 `economy.production-sensor`의 1-panel exact gram socket 권위와 철거 rollback이다.

## 107. 2026-08-28 생산 재고 감지반 1패널 exact kg 권위

상태: **공용 1패널 claim/profile·exact admission·복원 재투영·시설 변이 rollback 집중 증거 완료 / 설치 센서의 실제 파괴 손실·live UI PlayMode OPEN / Batch C 진행 중**.

### 107.1 닫은 범위

- [x] `production-sensor:{facilityId}`를 `economy.production-sensor` 소유의 `LiveFacility` exact claim/profile로 투영한다. 용량은 시설이 작성한 `StockSensorInstallationItemId` 한 개의 current immutable grams이며 별도 gram 저장 필드나 save schema를 추가하지 않는다.
- [x] 센서 설치 가능 시설은 설치 여부와 무관하게 영구 빈 socket을 가진다. 설치 요청 전, 건물 revision 변경과 Production restore 때 현재 시설 집합에서 결정론적으로 재투영하며 누락·부분 claim/profile·중복·비정규 시설은 fail-loud한다.
- [x] `production-sensor:`를 공용 exact-claim·gram admission 대상에 포함한다. 일반 delivery와 per-stack transit completion은 같은 token 경계를 사용하고 저수준 `WorldItemStackRuntime` 직접 route는 거부한다.
- [x] 첫 패널이 exact one-panel socket을 점유하면 두 번째 패널은 `ConveyorPortFull`로 실패한다. 실패한 동일 InTransit stack의 ID·수량·destination·reserved grams를 보존하고, 첫 패널 설치 Sink 뒤 같은 lot 재시도가 성공한다.
- [x] 같은 destination ID의 anchor/profile 변경은 physical 또는 reserved mass가 1g이라도 있으면 `production-sensor-authority-update-not-empty`로 거부한다.
- [x] 시설 mutation fence가 sensor socket과 output authority를 함께 prepare/revoke/abort한다. sensor revoke 뒤 output revoke 실패 시 sensor exact capacity를 즉시 복구하고, prepare 이후 센서 점유 race는 어느 권위도 부분 철회하지 않은 채 실패한다.
- [x] 설치·pending install·pending removal을 가진 시설의 수동 철거·이전·합성·진화를 차단해 기존 facility ID에 aggregate 상태나 embedded mass가 고아로 남지 않게 한다.
- [x] current Unity compile, `ProductionEconomyDebugScenarios`, `ProductionOutputDestinationLifecycleDebugScenarios`, `ProductionPreparedOutputFullPersistenceDebugScenarios`가 PASS했다. exact authority/full-retry/restore-projection/demolition-rollback 증거가 현재 source에 있다.
- [x] owner manifest를 연속 두 번 생성해 byte hash와 mtime 변화가 0임을 확인했다. 현재 집계는 input `5/39 migrated`, remaining `34`; output `6/6`; bypass `4`; orphan/unclassified `0/0`; delivery callsite `59/39`이다.

### 107.2 결정론적 증거

- owner manifest CSV SHA-256: `8CA968EDA6AD09E6A3C31C819BCE29CE6859591442AA55DB6D90F37C2BCF12DA`
- owner manifest report SHA-256: `1575E2D272B6AFC6CDD2B5350F707E1D9534B29C6C6BCE4E985B68C92A9E80E5`
- owner manifest source digest: `d7bf53651c1e0a8b0bb1379376b5d73f2577209790b9347e9393c5b9b41bc27f`
- classification gate `PASS`, full migration gate `OPEN`; second-run byte/hash/mtime 변화 `0`.

### 107.3 열린 P0·exit gate

- [ ] 설치된 센서를 가진 시설의 `DestructiveLoss`는 현재 질량 삭제를 막기 위해 fail-closed한다. 건물을 사실상 무적으로 남기지 않도록 기존 durable destructive-drain participant/journal 경계에 센서 embedded mass의 salvage 또는 typed declared loss, acknowledgement와 save/replay를 연결해야 한다. 단순 차단을 파괴 경로 완료로 세지 않는다.
- [x] relocation은 새 건물 생성과 첫 world mutation 직전에 `TryRequireNoAuthority(..., Relocation)`를 다시 호출한다. 포장 이후 센서 배송·예약이 생긴 fixture는 생성 객체 0, packed source·Grid·좌표 보존과 typed 실패를 증명한다.
- [ ] 실제 `ProductionBuildingPlayModeVerifier`의 UI 요청→물리 패널 운반→socket 도착→설치 Sink→목표 재고 UI 해금 경로는 사용자 소유 dirty `GameplayScene`을 저장·unload하지 않는 안전한 환경에서 실행할 때까지 OPEN이다.
- [ ] input owner `34`, bypass `4`, Batch A full-path canary, 전수 kg 적용, EWU·가격 재생성과 6인 생존망은 계속 OPEN이다.

이 checkpoint는 기존 한 패널의 current 질량을 admission 권위에 연결했을 뿐 authored kg·수량·BOM·WU·EWU·가격·시설 면적·ScriptableObject·prefab·scene 값을 변경하지 않았다. 따라서 `밸런스 영향 없음`이다. 큰 Batch A~H는 `0/8`; 현재 전체 진행은 약 `69~77%`, 잔여는 약 `23~31%`다.

## 108. 2026-08-28 생산 재고 감지반 파괴 드레인 구조 계약

상태: **구조 계약 확정·producer 상태기와 coordinator freeze/GC 경계 집중 증거 완료 / 여섯 번째 participant·save 양방향 join·실제 파괴 호출 경로 OPEN**.

### 108.1 구현 전 구조 계약

| 항목 | 권위와 불변식 |
|---|---|
| 콘텐츠 정의 | 시설의 `StockSensorInstallationItemId`와 immutable item mass catalog가 설치 패널의 정의·gram 권위다. 별도 파괴 전용 질량 필드나 센서 콘텐츠 ID 분기를 추가하지 않는다. |
| 런타임 상태 | 센서 소유권은 하나의 upper owner `stock-sensor:{facilityId}`가 조정하되 상태를 복제하지 않는다. 미설치 소켓 화물·배송 lease·carried slice는 Items의 `production-sensor:{facilityId}` 물리 권위, pending install·installed mass·pending removal은 `ProductionAggregateState` 권위다. upper journal은 두 하위 권위의 실행 순서와 합성 receipt만 소유한다. |
| 명령 | 공개 설치·제거·ack는 open-drain gate를 통과한다. 파괴 중에는 여섯 번째 `stock-sensor-embedded-salvage` participant만 deterministic child `:input-destination-custody`와 `IProductionStockSensorDestructiveDrainPort`를 조정한다. child는 upper receipt가 durable해지기 전에 acknowledge하지 않으며, upper acknowledge가 physical child와 embedded removal을 순서대로 exact acknowledge한다. |
| 조회 | lifecycle contributor와 current save projector는 같은 composite canonical을 사용해 소켓 물리 화물·배송 intent·pending install·installed·removal을 함께 읽는다. `OutputPublished`부터 gameplay sensor 기능은 비활성이며 terminal tombstone은 active 기능으로 세지 않되 upper checkpoint 전 durable provenance로 보존한다. |
| 식별자 | 재설치마다 persisted stock-sensor revision에서 positive installation sequence를 발급해 `production-stock-sensor-install:{facilityId}:{sequence}`를 사용한다. 같은 시설의 여러 설치 세대가 physical Sink/Source 영수증을 재사용하지 못한다. |
| 저장 | 새 sensor-parent DTO를 추가하지 않는다. 기존 Items child outbox, installed/removal DTO와 범용 upper journal participant/owner row를 재사용한다. deterministic child step ID와 upper composite receipt로 양방향 join하고 terminal tombstone은 upper+all lower owners의 원자 checkpoint 계약 전까지 삭제하지 않는다. 과거 save migration은 범위 밖이다. |
| 의존성 | participant DAG는 `apparel/combat/generic → capacity-routing → physical-output-custody → stock-sensor-embedded-salvage`다. 센서 participant는 runtime Query/Command만 사용하며 Items 저장 DTO를 gameplay query로 받지 않는다. 신규 Parameter/Composed 센서 콘텐츠는 기존 socket capability와 item mass catalog 등록만으로 참여하고 코어의 item ID 분기를 추가하지 않는다. |
| 실패 정책 | claim/profile 불일치, pending install 안정화 drift, 수동 removal 경쟁, source/install provenance 불일치, child/embedded receipt 불일치, late ingress, journal/lower phase 불일치와 상·하위 orphan은 restore publication 전에 fail-loud한다. 합법 상태를 유사 상태로 치환하거나 소켓 화물을 원격 source로 반환하지 않는다. |
| 전환 범위 | 기존 mutation fence의 영구 차단은 실제 coordinator/world-mutation gate 연결 뒤 제거한다. `production-sensor:`의 미설치·배송·carried 화물과 embedded 센서는 같은 upper owner의 서로 다른 하위 component이며, physical child는 현재 위치에 보존적으로 release하고 embedded component는 실제 Loose salvage를 exact-once publish한다. |
| 검증 | stale plan 저널 0, 재설치 세대 영수증 충돌 0, Prepare/Publish/Ack replay, wrong ack 거부, commit-before-ack 저장복원, upper↔sensor forward/reverse phase matrix, socket cargo 회수, world removal forward retry, 두 번째 출력 0을 집중 검증한다. |

### 108.2 현재 닫은 하위 경계

- [x] coordinator가 participant 계획 생성 뒤 lifecycle을 다시 캡처하고 destination·durable fingerprint가 바뀌면 journal 생성 전에 `production-facility-destructive-drain-plan-freeze-drift`로 거부한다.
- [x] lower participant tombstone과 원자 checkpoint transaction이 없는 동안 upper `TryCollectCheckpointed`를 fail-closed하고 terminal journal을 보존한다.
- [x] 센서 producer를 `Prepared → OutputPublished → OwnerAcknowledgedAwaitingCheckpointGc`로 분리했다. publish는 exact physical Source를 먼저 확보하고 installed provenance를 유지하며, upper ack 뒤에만 active installed 상태를 제거한다.
- [x] 파괴용 port에는 checkpoint GC 권한을 노출하지 않는다. 수동 제거만 같은 명령 안에서 private local cleanup을 수행하며 destructive terminal tombstone은 upper checkpoint 증거 전까지 보존한다.
- [x] 같은 시설의 재설치가 이전 설치·제거 receipt를 재사용하던 operation ID 충돌을 positive persisted sequence로 분리했다.
- [x] current Unity compile과 Production Economy 집중 회귀에서 prepare/publish/ack replay, wrong commit 거부, physical output exact-once와 terminal tombstone 보존을 확인했다.

### 108.3 열린 P0·exit gate

- [ ] `stock-sensor-embedded-salvage` lifecycle contributor/projector/participant를 필수 여섯 번째 registry ID로 연결하고 registry schema/fingerprint와 current save version을 갱신한다.
- [ ] upper owner와 pending removal을 phase별 forward/reverse exact join한다. `Prepared + exact physical output` crash-cut은 동일 deterministic receipt를 adopt하고, terminal 이후 합법적으로 운반·저장·재사용된 salvage의 현재 위치를 고정하지 않는다.
- [ ] `production-sensor:{facilityId}`의 배송 중·도착·carried 패널과 embedded 센서를 하나의 upper owner와 deterministic Items child로 연결한다. physical-only, embedded-only, combined, pending-install 상태를 모두 같은 owner로 계획하고 source storage 순간이동이나 count 삭제를 금지한다.
- [ ] Tick·restore의 socket projection을 drain phase-aware로 만들어 `AwaitingWorldRemoval` 이후 revoke된 socket을 재생성하지 않는다. Prepared/Draining의 복구 가능한 socket과 relocation re-anchor는 유지한다.
- [ ] coordinator·participant·journal·save sections·restore recovery runner를 live DI에 등록하고 explicit demolition, structural loss, combat cover 세 경로를 단일 world-mutation gate로 직렬화한다.
- [ ] commit 이후 grid removal 실패는 센서/출력을 되살리지 않고 `AwaitingWorldRemoval`에서 forward retry한다. commit 전 실패만 world/fence rollback을 허용한다.
- [ ] 실제 dirty `GameplayScene`을 건드리지 않는 안전한 환경에서 센서 배송→설치→파괴→Loose salvage→AI haul→저장/재설치와 save/restore PlayMode를 통과한다.

이 체크포인트는 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값을 바꾸지 않았다(`밸런스 영향 없음`). 큰 Batch A~H는 계속 `0/8`; 전체 진행률은 compile·save·live 경로 증거가 추가되기 전까지 약 `69~77%`, 잔여 `23~31%`로 유지한다.

## 109. 2026-08-28 파괴 드레인 live 권위 수렴 체크포인트

상태: **세 live 파괴 호출자·typed 복구·양 destination 권위 수렴·결정론적 manifest 집중 증거 완료 / checkpoint GC·비동기 철거 후속 효과·dirty-scene PlayMode OPEN**.

### 109.1 이번 체크포인트에서 닫은 범위

- [x] explicit demolition, structural integrity loss, combat cover destruction 세 production 호출자가 공용 `IBuildingDestructiveLossRuntime`의 typed destructive-drain 경계를 사용한다.
- [x] 개발자 `building:destroy` 명령의 직접 `DestroySelf()` 우회를 제거하고 같은 typed 경계를 사용한다.
- [x] output과 `production-sensor:{facilityId}` claim/profile을 하나의 shared authority-state query로 캡처하며 0/0·1/1 이외 cardinality, owner, anchor, facility, operation, position, schema drift를 fail-loud한다.
- [x] sensor revoke와 output revoke가 서로 다른 lower transaction이어도 이미 commit된 도메인을 되살리지 않고 forward retry한다.
- [x] lower revoke가 실제 commit 뒤 `false`를 반환하는 경우 target 0/0을 재확인하고, 조기 `Applied` 반환 없이 같은 호출에서 다음 도메인과 final lifecycle postcondition까지 닫는다.
- [x] lower revoke가 commit 없이 `false`를 반환하면 `Deferred`, partial/invalid pair이면 `Conflict`로 구분한다. 영구 충돌은 매 Tick 무한 재시도하지 않는다.
- [x] coordinator는 `RecordAuthorityRevoked`와 `RecordWorldRemoved` 직전에 shared authority와 lifecycle을 다시 캡처하고, sensor/output 잔존·partial pair·empty-block을 모두 거부한다.
- [x] world visual 제거 실패 뒤 exact Grid occupant 복구 실패를 숨기지 않고 typed conflict와 save capture guard로 노출한다.
- [x] Unity current-source 컴파일 후 revoker/coordinator/recovery 집중 묶음이 `DESTRUCTIVE_DRAIN_ADVERSARIAL_AUTHORITY_SUITE_PASS`; Console Error `0`이다.
- [x] owner manifest가 live destructive callers `3`, legacy transaction callers `0`, debug bypass `0`, allowlisted direct destroy `8`을 검증한다.
- [x] owner manifest를 연속 두 번 생성해 CSV/report의 byte hash·길이·mtime 변화가 모두 `0`임을 확인했다.

### 109.2 결정론적 증거와 현재 전수 집계

- manifest CSV SHA-256: `7AEED0423C1A67FB3BCBE4B3A7C6D6DA08B1CEF287F3C4BEC86B2358B60A8C8B`
- manifest report SHA-256: `D6A0BCD9BA7E69B662D5758FD767FE71D52C6A89F13E0FE81E65EA2BEA7F86C4`
- source digest: `446201a5dba62757dd48bff20230049479f98d39290fa163b45266333904a907`
- input owners `5/39` migrated, `34` remaining; output owners `6/6`; bypass `4`; orphan/unclassified `0/0`; delivery invocations/files `59/39`.

### 109.3 아직 열려 있는 P0와 Batch exit gate

- [ ] 여섯 destructive participant의 checkpoint GC를 durable save order `200`의 prepare/publish/rollback/complete transaction으로 구현하고 upper journal을 항상 마지막에 제거한다.
- [ ] Apparel과 stock-sensor lower terminal/tombstone에 exact checkpoint-GC candidate API를 연결하고 gameplay의 raw `TryCollectCheckpointed` 경로를 제거한다.
- [x] explicit demolition의 deferred 완료 뒤 placement success, player refund/grid notification과 `WorkAmount`의 `facilityRemovedForRetry`를 `OnBuildingDestroyed + destructiveDrainOperationId`로 exactly-once 연결했다. world 제거 전에는 성공 효과를 선반영하지 않으며, V7 WorkOrder 저장과 upper journal의 교차 사전검증까지 §110에서 닫았다.
- [ ] dirty user scene을 저장·unload·overwrite하지 않는 환경에서 sensor delivery→install→destructive drain→Loose salvage→AI haul→store/reinstall의 PlayMode와 save/restore를 통과한다.
- [ ] 남은 input owner `34`, 전수 kg 적용, EWU·가격 재생성, 6인 생존망과 Batch A~H 최종 회귀를 계속 수행한다.

이번 변경은 destructive ownership과 복구 원자성만 교정했다. authored kg, capacity, quantity, BOM, WU, EWU, 가격, ScriptableObject, prefab, scene 값은 바꾸지 않았으므로 `밸런스 영향 없음 / 구조 교정 검증 진행 중`이다. 큰 Batch A~H는 `0/8`; 전체 진행률은 약 `69~77%`, 잔여는 `23~31%`로 유지한다.

## 110. 2026-08-28 WorkOrder 비동기 철거 완료·교차 저장 원자성

상태: **deferred 철거 완료·취소 salvage-only·live/terminal restore 재결합·whole/registry 사전검증 집중 증거 완료 / checkpoint GC·dirty-scene PlayMode OPEN**.

### 110.1 닫은 범위

- [x] Grid 파괴 요청은 `Removed`, `DeferredAccepted`, `Conflict` typed 결과와 canonical destructive-drain operation ID를 보존한다. 기존 bool API는 즉시 제거 성공만 true로 투영한다.
- [x] UI와 WorkOrder는 world 제거 이벤트를 요청 전에 구독하고 operation ID별 observation을 유지한다. deferred 요청을 실패로 표시하거나 성공 효과를 선반영하지 않으며 실제 `OnBuildingDestroyed` 뒤 grid/UI/WorkOrder 후속 효과를 한 번만 수행한다.
- [x] WorkOrder V7은 `destructiveDrainOperationId`, `facilityRemovedForRetry`, `cancelRebuildAfterDestructiveDrain`을 저장한다. accepted drain 취소는 주문을 삭제하지 않고 물리 salvage의 durable owner로 유지하며, 회수품 exact publish 뒤 재건 없이 종료한다.
- [x] restore 때 live 시설은 exact persistent ID·definition·center로 observation을 재부착한다. world가 없으면 exact `ExplicitDemolition` terminal journal만 forward acknowledgement로 인정하며 중복·불일치·누락은 fail-loud한다.
- [x] whole-save와 registry preflight가 `work.orders → destructive journal`을 publication 전에 검증한다. operation cardinality, `Dismantle`, `ExplicitDemolition`, operation↔facility, nonterminal world definition/center, quality pipeline definition/footprint, cancel stage/status, removed↔terminal phase를 exact 비교한다.
- [x] 한 operation의 WorkOrder 소유자가 둘이면 거부한다. WorkOrder owner가 있는데 journal이 없으면 whole/registry 모두 거부한다.
- [x] direct player demolition도 같은 `ExplicitDemolition` journal을 사용하므로 journal→WorkOrder 역방향 의무는 두지 않는다. WorkOrder가 없는 legitimate direct demolition은 계속 허용한다.
- [x] cross-save failure preflight는 입력 JSON을 변경하지 않는다. operation drift fixture에서 검증 전후 byte-equivalent JSON을 확인했다.

### 110.2 현재 증거

- Unity current-source compile: Warning/Error `0/0`.
- `WORKORDER_DRAIN_CROSSSAVE_FULL_PERSISTENCE_PASS_V7`.
- `WORK_AMOUNT_DEFERRED_DEMOLITION_SUITE_PASS_V7`.
- `DESTRUCTIVE_DRAIN_ADVERSARIAL_AUTHORITY_SUITE_PASS`.
- 정상 whole-save/registry, world target mismatch, pipeline 누락·footprint drift, operation mismatch, cause mismatch, premature removal acknowledgement, cancel-state mismatch, duplicate owner, journal 누락을 같은 current-format fixture에서 검증했다.
- 첫 실패 `Sequence contains no matching element`는 source fixture에 없던 `work.orders`를 `ReplacePayload`로 교체하려 한 테스트 조립 오류였다. 최초 section을 `Add`, 이후 변조를 `Replace`로 분리해 해결했다.
- 두 번째 실패는 world-target 테스트가 building definition mismatch에서 먼저 fail-loud해 목표 오류 코드를 관찰하지 못한 fixture 문제였다. WorkOrder와 pipeline 좌표를 함께 이동시켜 pipeline은 통과하고 world row만 불일치하도록 교정했다.

### 110.3 확장 폐쇄·잔여 범위

- 확장 유형: 기존 destructive ownership capability의 `InvariantChange` 교정.
- core content-specific branch count: `0`.
- unregistered capability count: `0`.
- synthetic canary: `N/A — 콘텐츠 추가 축이 아니라 current-format 교차 Aggregate 소유권 불변식`; 대신 기존 synthetic full-persistence fixture가 정상·변조 save 경로를 사용한다.
- [x] 여섯 lower participant와 upper journal을 한 durable checkpoint transaction으로 제거하는 journal-last atomic GC를 current-source focused gate에서 닫았다. 실제 dirty scene PlayMode 증거는 아래 별도 항목으로 계속 OPEN이다.
- [ ] dirty 사용자 `GameplayScene`을 저장·unload·overwrite하지 않는 안전한 환경의 실제 deferred demolition PlayMode는 계속 OPEN이다.
- [ ] 다음 input owner `medical.surgery`, 남은 input owner `34`, 전수 kg 적용, EWU·가격 재생성, 6인 생존망과 Batch A~H 최종 회귀는 계속 OPEN이다.

이번 체크포인트는 저장 결합과 후속 효과의 원자성만 교정했다. authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값은 변경하지 않았으므로 `밸런스 영향 없음 / 구조 교정 검증 완료`다. 큰 Batch A~H는 `0/8`; 전체 진행률은 약 `69~77%`, 잔여는 `23~31%`로 유지한다.

## 111. 2026-08-28 journal-last 원자 checkpoint GC 구현 계약

상태: **journal-last 원자 checkpoint GC current-source compile·focused·cross-save gate 완료 / dirty scene 실제 PlayMode OPEN**.

### 111.1 구현 전 재감사에서 확정한 위험

- destructive GC는 기존 prepared-output GC와 대상·topology가 다르므로 같은 participant 계약이나 candidate ID 벡터를 재사용하지 않는다. prepared-output은 durable order `100`, destructive GC는 그 결과를 관찰한 뒤 실행되는 별도 order `200`이다.
- 현재 하위 `TryGarbageCollect`는 generic과 combat에서 child를 먼저 삭제하고 producer를 나중에 삭제한다. apparel은 lower receipt 삭제 API가 없고 stock sensor는 destructive tombstone GC API가 없다. 이 메서드를 상위에서 단순 반복하면 중간 실패 때 부분 GC가 남는다.
- Items의 physical/capacity/input-child가 같은 `WorldItemRepositoryState`, Production의 sensor/generic WIP가 같은 `ProductionAggregateState`를 공유한다. participant별 전체 aggregate clone rollback은 먼저 성공한 다른 participant의 변경을 덮어쓰므로 금지한다.
- candidate는 exact operation/step/commit/receipt 행만 캡처한다. prepare는 무변경, publish는 exact compare-remove, rollback은 해당 exact 행만 compare-restore하고 unrelated revision·행을 되감지 않는다.
- 내부 publish 순서는 registry DAG 역순인 `sensor → physical → capacity → generic → combat → apparel → journal`이다. publish 호출 전에 현재 participant를 rollback 목록에 넣어 호출 중 일부 변경 후 예외도 자기 자신부터 복구한다.
- capacity candidate는 durable order `100` 뒤 publish 직전에 routing batch와 exact-route 권위가 모두 부재인지 다시 증명한다.
- generic candidate는 Items child, WIP terminal receipt, producer를 함께 수거한다. combat은 optional Items child, craft/repair source receipt, producer를 함께 수거한다. apparel은 effect/source receipt가 든 같은 terminal-state row와 producer를 함께 수거한다. sensor는 Items child와 pending removal을 함께 수거한다.
- upper journal은 항상 마지막에 제거한다. journal publish 실패 시 journal snapshot과 여섯 lower exact row를 역순으로 복구한 뒤 failure를 반환한다.

### 111.2 WorkOrder 수명과 저장 replay 계약

- WorkOrder를 GC mutation participant로 만들지 않는다. `IWorkOrderDestructiveDrainRetentionQuery`가 내부 전체 order authority에서 operation별 owner absence proof를 제공한다.
- terminal journal이라도 `facilityRemovedForRetry=false`, salvage/rebuild 진행 중, cancel salvage-only 진행 중이면 WorkOrder가 남아 있으므로 해당 operation을 수거하지 않는다. exact salvage publication 뒤 order가 실제 삭제된 다음 durable checkpoint에서만 수거한다.
- `ExplicitDemolition`에 WorkOrder 하나가 있는 것은 정상 retention이다. `StructuralIntegrity`/`CombatCover` operation에 WorkOrder가 있거나 한 operation에 owner가 둘 이상이면 corruption이다.
- candidate prepare 때 owner `0`의 version/fingerprint를 저장하고 publish 직전에 다시 읽는다. 중간에 WorkOrder가 생기거나 authority version이 바뀌면 mutation 없이 Deferred한다.
- journal save authority는 V3에서 `lastConfirmedCheckpointSequence`와 lowercase SHA-256 digest를 저장한다. sequence `0`은 빈 digest, positive sequence는 exact digest를 요구한다.
- 같은 live slot/digest callback은 `AlreadyApplied`, 다른 digest는 contiguous next sequence다. callback 후 다음 save 전 crash는 이전 durable bytes의 terminal rows와 이전 marker를 복원하므로 안전하게 다시 실행한다.
- legacy save-slot fixture가 durable order `100`만 조립하는 경로는 production에서 사용하지 않는다. production DI는 전체 `IDungeonDurableSaveCommitCoordinator`만 주입하고 order `100→200`을 보장한다.

### 111.3 현재 구현 체크리스트

- [x] 별도 destructive checkpoint context/result/candidate/participant/coordinator 계약을 추가했다.
- [x] WorkOrder runtime에 operation별 read-only retention query와 zero/duplicate owner fail-loud 검증을 연결했다.
- [x] journal 저장 계약을 V3 marker 구조로 올리고 exact candidate prepare/publish/rollback/complete 골격을 추가했다.
- [x] durable-save participant ID `200.production-facility-destructive-drain-checkpoint-gc`, order `200`과 DI 골격을 추가했다.
- [x] Items input-child/physical/capacity exact-row candidate와 revision-safe rollback을 완료했다. 다중 행 복원은 전체 충돌 preflight 뒤에만 시작하며, publish 중간 실패와 explicit rollback 모두 부분 복원을 남기지 않는다.
- [x] generic/sensor의 Production exact-row, apparel terminal-state row, combat craft/repair receipt row candidate를 완료했다. 실제 generic outbox는 child→WIP→producer publish, producer→WIP→child rollback, WIP 실패, producer drift, multi-producer preflight를 직접 통과했다.
- [x] 여섯 participant가 공통 GC capability를 구현하고 registry participant set과 exact 일치하도록 등록했다. apparel은 실제 `ApparelWorkOrderRuntime → terminal outbox → participant`, combat은 실제 terminal outbox/source authority 경계를 통과했다.
- [x] coordinator 정상·prepare 실패·여섯 publish 경계·journal publish 실패·rollback 실패·capacity authority 잔존·WorkOrder retention·same-digest replay fixture를 통과했다. rollback 실패 candidate는 `Complete`로 증거를 지우지 않고 active 상태를 유지한다.
- [x] journal V3 capture/restore와 full-persistence forward/reverse join을 통과했다. positive marker의 sequence/digest, row 제거+marker 동시 왕복, 불법 조합, 필수 scalar 누락과 V2 payload 거부를 포함한다.
- [x] current-source Unity compile, focused Editor suite, Console Warning/Error `0/0` 증거를 확보하고 §109/§110의 journal-last checkpoint GC OPEN 항목을 닫았다.

2026-08-28 fresh evidence: source보다 최신인 `Assembly-CSharp.dll`과 `Assembly-CSharp-Editor.dll`에서 `ProductionInputDestinationCustodyDrainOutboxDebugScenarios`, `ProductionPhysicalCustodyDrainOutboxDebugScenarios`, `ProductionCapacityRoutingDrainOutboxDebugScenarios`, `CombatEquipmentTerminalDrainOutboxDebugScenarios`, `ProductionApparelOrderTerminalDrainOutboxDebugScenarios`, `ProductionGenericBillTerminalDrainOutboxDebugScenarios`, journal과 여섯 participant, coordinator, durable-save adapter, recovery/authority revoker, `ProductionPreparedOutputFullPersistenceDebugScenarios`, `WorkAmountDebugScenarios`를 한 clean-console 구간에서 실행해 `V27_JOURNAL_LAST_CHECKPOINT_GC_FINAL_FOCUSED_PASS`를 확인했다. 최종 Console은 Warning/Error `0/0`이다. 검증 과정에서 (1) lower 다중 행 rollback의 restore-before-later-conflict 부분 복원, (2) rollback 실패 candidate를 coordinator가 `Complete`해 복구 증거를 해제하는 문제, (3) V3 checkpoint marker scalar 누락이 기본값으로 복원되는 문제를 발견했고, 런타임 계약을 완화하지 않고 atomic preflight·failed-candidate retention·strict raw payload 검증으로 교정했다. dirty 사용자 `GameplayScene`은 저장·unload·overwrite하지 않았으므로 실제 scene PlayMode 증거는 여전히 OPEN이다.

이번 P0는 저장 tombstone 수명과 원자성만 다뤘으며 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 수치를 변경하지 않았다. 따라서 분류는 `밸런스 영향 없음 / 구조 교정 focused 검증 완료 / 실제 scene PlayMode OPEN`이다. 큰 Batch A~H는 `0/8`, 전체 진행률은 약 `69~77%`, 잔여 `23~31%`를 고정한다.

## 112. 2026-08-28 `medical.surgery` 입력 owner exact gram·종결 수명주기 구현 계약

상태: **구조 owner migration 완료 / 실제 scene terminal fault PlayMode·신규 procedure 다형성 canary OPEN**.

### 112.1 현재 권위와 실제 결함

- 수술 주문은 `surgery-materials:{orderId}`의 exact `LiveFacility` claim을 발행하고 일반 재료, 수술 대상 시체, 선택 이식 부품을 같은 목적지로 운반한다. 소비는 exact physical stack을 `PhysicalItemBatchDispositionKind.Sink`로 commit하고 포장 tare를 별도 물리 부산물로 반환한다.
- manifest상 `medical.surgery` input owner는 `migrated`다. owner-neutral exact claim/profile과 destination-custody drain child를 Surgery V12 terminal join으로 묶고, raw/captured/registry restore preflight와 durable checkpoint GC까지 연결했다.
- `SurgeryRuntime`의 생성·복원·취소·실패·시설 소실은 같은 owner-neutral terminal 경로를 사용한다. claim/profile partial pair, pickup 전 lease, committed intent, carried cargo, FacilityBuffer stock과 pending sink receipt는 child authority의 exact phase/receipt로 결합되며, durable save 성공 뒤에만 upper/lower terminal join이 원자적으로 수거된다.
- 같은 목적지의 합법 payload가 BOM 재료에 한정되지 않으므로 capacity를 `materials` 수량 합만으로 계산하면 안 된다. 비선택 optional 재료는 제외하고, 필수 재료와 현재 주문이 실제 요청하는 exact corpse/selected-part stack을 모두 포함한다.
- 의사 Downed는 재료 주문의 terminal 원인이 아니다. doctor 실행권만 해제하고 destination, route, physical cargo와 수술 주문은 유지해 다른 의사가 이어받게 한다.

### 112.2 확장 폐쇄 구조

| 항목 | 계약 |
|---|---|
| authority | `SurgeryAggregateStateStore`는 수술 단계와 외부 transaction join key만 소유한다. claim registry, capacity registry, physical stack/haul, custody drain, batch disposition은 각 기존 단일 권위를 유지한다. |
| 공용 capability | Items의 owner-neutral `FacilityBufferDestinationLifecycleService`, exact-lot admission, custody drain, batch disposition을 사용한다. `ProductionInputDestinationClaimRuntime`은 production 전용이므로 Medical이 참조하지 않는다. |
| adapter | Surgery adapter가 active order와 exact payload에서 claim/profile/join snapshot을 만든다. procedure ID, item ID 또는 특정 BOM에 따른 코어 분기는 0이어야 한다. 신규 procedure/BOM은 기존 material collection과 optional/selected-payload 의미만으로 연결된다. |
| command | create/restore는 claim+profile을 하나의 reversible owner-scoped transaction으로 게시한다. cancel/failure/facility loss는 terminal drain을 준비하고 physical custody가 닫힌 뒤에만 pair와 order를 종결한다. |
| query | order별 destination, owner operation/facility, exact max grams, mass authority revision, authority fingerprint와 lifecycle phase를 immutable snapshot으로 읽는다. 저장 DTO를 gameplay 질량 query에 전달하지 않는다. |
| save | current-format Surgery order는 외부 권위의 복제 state가 아니라 exact join key/fingerprint/revision/phase와 pending sink·drain receipt identity만 저장한다. 필수 필드 누락은 migration 없이 typed restore failure다. |
| dependency | Foundation ← Items owner-neutral capability ← Medical adapter. Items가 Medical을 참조하거나 Medical이 Economy production adapter를 참조하지 않는다. |
| failure | missing/duplicate/partial claim-profile, 0 이하 capacity, mass revision drift, route/admission/sink/drain join 불일치는 world mutation 전에 fail-loud한다. profile 누락을 legacy count 입고로 fallback하지 않는다. |
| balance | 이번 slice는 authored kg·BOM·WU·위험·성공률·회복·공간·가격을 바꾸지 않는다. capacity는 기존 물리 질량에서 파생되는 안전 한도이며 새 비용이 아니다. |

### 112.3 수명주기와 원자성

```text
ActiveAuthority
→ Consuming
→ ConsumedPendingAck
→ TerminalDrainPrepared
→ TerminalDrainCommitted
→ Closed
```

- 생성은 selected-part 예약 성공 뒤 exact payload gram을 계산하고 claim/profile pair를 게시한다. pair 게시 실패 시 selected-part 예약과 order identity publication을 모두 되돌린다.
- 배송 요청은 pair가 exact한 경우에만 source lot을 retarget하고 gram admission을 commit한다. profile capacity는 order가 합법적으로 요청할 수 있는 필수 재료+corpse+selected part 전체 질량 이상이어야 한다.
- 소비는 exact source stack, quantity, input grams, operation/commit ID를 pending receipt로 보존한다. tare/byproduct publication과 acknowledgement 전에는 terminal로 취급하지 않는다.
- 소비 전 취소·시설 소실은 공용 custody drain으로 미픽업 lease, routed stack, carried cargo, buffered stock을 물리적으로 회수한다. carried 화물은 순간이동시키지 않고 기존 recovery-drop 계약을 따른다.
- 소비 후 취소·실패는 이미 소비된 재료를 환불하지 않는다. sink/tare를 exactly-once finalize한 뒤 잔여 destination custody만 drain한다.
- claim/profile revoke는 destination occupancy, reserved grams, active route/admission, carried commitment와 drain이 모두 0/terminal인 뒤 수행한다. 하나라도 남으면 order는 terminal 대기 상태를 유지한다.
- 시설 파괴는 drain acknowledgement 전 world removal을 완료하지 않는다. 의사 Downed는 이 terminal 수명주기를 시작하지 않는다.

### 112.4 current-format restore 계약

- whole-save와 registry preflight는 active/terminal-waiting order마다 claim 1개, capacity profile 1개, exact owner/facility/drop position, mass authority revision/fingerprint를 검증한다.
- 목적지로 향하는 physical stack, haul intent, quantity lease, carried cargo, admission token, pending custody drain과 material Sink receipt를 order의 lifecycle phase와 양방향으로 join한다. owner 없는 외부 행과 외부 행 없는 owner를 모두 거부한다.
- restore candidate는 claim과 capacity를 `FacilityBufferDestinationLifecycleService`로 함께 stage한다. publish 순서는 기존 registry DAG의 claim→capacity→surgery이고 rollback은 surgery→capacity→claim이다.
- `materialsConsumed=false`인데 sink receipt가 있거나, `materialsConsumed=true`인데 required pending/acknowledged receipt identity가 없거나, terminal drain phase가 실제 physical custody와 맞지 않으면 publication 전 전체 restore를 실패시킨다.
- 동일 facility의 복수 수술 주문은 destination와 owner operation이 order ID로 완전히 분리되어야 한다. profile·route·receipt를 facility ID만으로 합치지 않는다.

### 112.5 구현 체크리스트

- [x] Surgery owner-neutral adapter와 immutable authority snapshot/fingerprint를 추가한다.
- [x] active order의 필수 재료·corpse·selected part 전체 exact gram을 산출하고 positive capacity profile을 만든다.
- [x] order 생성의 direct claim mutation을 atomic claim+profile lifecycle publication으로 교체한다.
- [x] restore의 claim-only replacement를 claim+profile pair staging과 exact cross-aggregate preflight로 교체한다.
- [x] reserved FacilityBuffer destination의 mass-required capability를 prefix 분기 없이 선언하고 profile 없는 배송을 fail-loud한다.
- [x] cancel/failure/facility-loss를 공용 custody drain과 sink/tare acknowledgement 뒤 terminal close로 전환한다.
- [x] current-format 필수 join key/fingerprint/revision/phase를 저장하고 구버전·누락 payload를 거부한다.
- [x] 생성 rollback, exact gram 경계, corpse+part+materials, source-reserved/committed-intent/carried/FacilityBuffer custody, sink pending/ack와 raw/captured/registry save 변조 focused fixture를 통과한다.
- [ ] 실제 scene PlayMode에서 doctor Downed 비종결, facility loss 종결 재시도, tare output-space wait와 복수 주문 격리를 통과한다.
- [ ] 가상 신규 procedure와 신규 BOM을 data fixture로 추가해 core content-specific branch `0`, unregistered capability `0`을 증명한다.
- [x] manifest를 두 번 재생성해 `medical.surgery=migrated`, input remaining `33`, bypass `4` 유지 사유, byte·mtime 동일을 기록한다.
- [x] Unity current-source compile과 focused Surgery static/runtime/save/GC suite를 통과하고 Console Warning/Error `0/0`을 기록한다.
- [ ] 사용자 소유 dirty `GameplayScene`을 저장·unload·overwrite하지 않는 실제 Surgery PlayMode를 통과한다.

구조 owner migration은 닫혔지만 실제 scene fault PlayMode와 확장 폐쇄 canary가 닫히기 전에는 Surgery의 최종 runtime acceptance 또는 미래 콘텐츠 확장 폐쇄 완료로 보고하지 않는다. 또한 이 owner 하나의 구조 폐쇄만으로 Batch A~H 또는 전체 진행률을 올리지 않는다.

### 112.6 2026-08-28 현재 구현·검증 증거

- `SurgeryMaterialDestinationRuntime`이 필수 material 행을 stable item ID로 합산하고 optional을 제외하며, 주문이 실제 요청하는 corpse/selected-part exact stack을 더해 positive gram profile과 `ExactGramRequired` claim을 한 owner-scoped lifecycle transaction으로 게시한다.
- claim은 콘텐츠 prefix 검사 대신 공용 `FacilityBufferDestinationAdmissionPolicy.ExactGramRequired`를 선언한다. `WorldItemWarehouseService`는 live claim의 정책으로 mass profile 필수 여부를 판단하며 `SurgeryMaterialsPrefix` 분기를 포함하지 않는다.
- `SurgeryMaterialCapacityFingerprint`는 Models의 단일 계산 권위다. runtime create/restore와 `SurgerySaveValidation`이 같은 canonical digest를 사용해 capacity, mass authority revision, subject, selected part와 필수 BOM 변조를 publication 전에 거부한다.
- current-format은 material sink operation/commit ID, exact input grams와 acknowledgement를 저장한다. unconsumed owner의 sink join, terminal owner의 unacknowledged sink와 receipt identity/mass 불일치를 fail-loud한다. terminal destination 해제 전에 pending sink를 acknowledge한다.
- 집중 fixture `SurgeryMaterialDestinationRuntimeDebugScenarios`는 generic 중복 재료 합산, optional 제외, corpse+selected part 포함 `8,800g`, lifecycle 실패 rollback, claim/profile pair, restore candidate 비누출, publish/revoke, capacity/revision/fingerprint 변조, profile 누락 배송의 count fallback 차단과 source 보존을 검증했다.
- owner-neutral `FacilityBufferDestinationCustodyDrainProjection`과 `SurgeryMaterialTerminalRuntime`이 source-reserved, committed intent, carried, buffered custody를 같은 공용 phase/receipt로 동결한다. doctor loss는 주문을 유지하고 cancel/failure/facility loss만 terminal drain을 시작한다.
- `SurgeryMaterialTerminalCrossAggregateSaveValidation`은 raw whole-save, direct registry와 `CaptureAll` 이후 outgoing canonical envelope를 같은 순수 join으로 검증한다. upper-only/lower-only, phase·identity·digest·mass drift는 live publication 또는 durable write 전에 실패한다.
- durable order `300`의 `SurgeryMaterialTerminalCheckpointGcDurableSaveParticipant`는 save commit 성공 뒤 child를 먼저, Surgery upper join을 마지막에 수거한다. upper publish 실패 시 child를 exact rollback하며 완료·실패·취소 주문과 history는 유지한다.
- current Unity source에서 `V27_SURGERY_TERMINAL_CUSTODY_FOCUSED_PASS`, `V27_SURGERY_TERMINAL_AND_GC_FOCUSED_PASS`, `V27_SURGERY_TERMINAL_SAVE_AND_GC_PASS`, `V27_SURGERY_SYSTEM_CONTRACTS_PASS`, `V27_INPUT_DESTINATION_DRAIN_RESERVED_CARRIED_BUFFER_PASS`, `V27_SURGERY_OWNER_FOCUSED_BUNDLE_PASS`가 PASS했고 Console Warning/Error는 `0/0`이다.
- manifest는 두 번 생성해 CSV `5CA585F05B9E1FEA99C224350B6A0C2B1A26D1345A31D3EFE6D215639C19156D`, report `B2441967642619D4BE82878C46BDD2E93AF262CA7C14AC2C5463FF7C38CDAECD`이며 hash·length·mtime 변화가 0이다. `medical.surgery=migrated`, input `6/39`, remaining `33`, output `6/6`, bypass `4`, orphan/unclassified `0/0`이다.
- 실제 dirty-scene terminal fault PlayMode, tare output-space와 synthetic procedure/BOM canary는 계속 OPEN이다. 이 열린 증거는 구조 owner disposition을 되돌리지 않지만 최종 runtime acceptance와 확장 폐쇄 완료를 막는다.
- 이 slice의 authored kg·수량·BOM·WU·EWU·가격·시설 공간·SO·prefab·scene 변경은 0이다. Batch A~H는 `0/8`, 전체 진행률은 `69–77%`, 잔여는 `23–31%`로 유지한다.

## 113. 2026-08-28 `medical.character-supply` 입력 owner exact gram·terminal custody 구현 계약

상태: **구조 owner migration·focused evidence 완료 / actual autonomous medical PlayMode OPEN**.

### 113.1 현재 결함과 범위

- `facility-input:medical:{orderId}`는 치료약 또는 추출 혈액 정확히 1개를 치료 시설로 배송하고 typed Sink/tare outbox로 소비한다. physical Sink receipt의 current-format join은 이미 존재한다.
- destination에는 positive gram claim/profile이 없다. 배송은 same-cell 추론과 legacy count admission에 의존하며 profile 없는 physical mutation을 허용할 수 있다.
- 완료·취소는 `CharacterMedicalRuntime.ReleaseTreatmentMaterials`가 `ReleaseStacksByDestination`를 직접 호출한 뒤 destination ID와 supply state를 즉시 지운다. pickup 전 lease, routed stack, carried cargo와 FacilityBuffer stock을 구분하지 않으므로 순간이동·고아·save join 손실 위험이 있다.
- 이 slice는 캐릭터 치료용 공급 destination만 다룬다. 환자 entity 운반, 수술 주문, 장기 보관소, 보존 용기, 의료 시설 WU·성공률·수치는 변경하지 않는다.

### 113.2 구조 계약

| 항목 | 계약 |
|---|---|
| 콘텐츠 정의 | `IResourceEconomyContentCatalog`의 `SupportsInjuryTreatment` medicine과 `CaptivityItemDefinitions.ExtractedBloodItemId`가 현재 합법 supply 집합이다. item unit gram은 `IPhysicalItemMassQuery`가 유일한 권위다. |
| 런타임 상태 | `CharacterMedicalOrder`는 다음 destination sequence, 현재 active lifetime, 주문당 최대 1개의 미종결 drain join과 durable GC 전까지의 closed join bounded collection만 소유한다. claim/profile, world stack·lease·haul intent·carry·buffer와 custody drain child는 Items 기존 단일 권위를 유지한다. |
| 명령 | supply를 선택·요청하기 전에 owner-scoped exact claim/profile pair를 게시한다. 시설 소실·배치 실패·수동 재배정은 현재 lifetime을 먼저 drain해 non-terminal target으로 닫고 주문을 계속한다. 완료·취소·환자 사망은 material Sink를 먼저 exactly-once 회복하고 동일 active join의 target을 terminal로 승격한 뒤 닫는다. |
| 조회 | order ID, current destination sequence, facility ID/좌표, destination, current positive max grams, mass revision/fingerprint, active join과 closed join을 immutable projection으로 읽는다. save DTO를 gameplay 질량 query에 전달하지 않는다. |
| 식별자 | 범용 exact prefix `facility-input:exact:`을 사용한다. destination `facility-input:exact:medical.character-supply:{orderId}:{sequence:D8}`, owner stable `character-medical-order:{orderId}`, owner operation `character-medical-supply-destination:{orderId}:{sequence:D8}`, parent `character-medical-supply-drain:{orderId}:{sequence:D8}`, step `{parent}:custody`를 canonical formatter 한 곳에서 만든다. 콘텐츠별 exact-claim 코어 분기는 추가하지 않는다. |
| 저장 | 미배포 current-format schema에 `next/current destination sequence`, capacity/revision/fingerprint와 정렬된 bounded drain join list를 저장한다. join은 target state/status, parent/step, old facility/destination/fingerprint, request/commit/receipt, exact quantity/grams와 frozen owner coordinates를 가진다. claim/profile/child 상태는 복제하지 않는다. 과거 schema migration은 추가하지 않는다. |
| 의존성 | Combat runtime은 Items owner-neutral lifecycle/custody capability를 참조한다. Items는 Character Medical 또는 Combat을 참조하지 않는다. production-named child DTO를 Combat이 직접 해석하지 않는다. |
| 실패 | missing/partial claim-profile, 비양수 capacity, facility/destination/revision/fingerprint drift, upper-only/lower-only child, sink/custody phase 불일치는 physical mutation·restore publication·durable write 전에 fail-loud한다. |
| 전환 | `ReleaseStacksByDestination` 직접 호출을 제거한다. owner가 migrated된 뒤 character-medical destination에 legacy count admission 또는 direct release를 허용하지 않는다. |
| 검증 | exact gram 경계, medicine/혈액 후보, source-reserved/routed/carried/buffered, sink pending/ack, 완료·취소·환자 사망, raw/captured/registry restore, GC rollback/idempotence와 actual AI medical PlayMode를 분리 검증한다. |

### 113.3 capacity와 확장 폐쇄

- 하나의 active destination lifetime은 치료 supply 1개만 동시에 소유한다. profile max grams는 현재 카탈로그의 합법 치료 medicine과 extracted blood 중 가장 무거운 unit gram으로 계산한다. 따라서 어떤 후보를 선택해도 pair를 갈아끼우거나 candidate item ID별 코어 분기를 추가하지 않는다.
- 합법 supply 집합은 capability query가 열거하고 stable item ID별 ordinal 순서와 canonical mass revision/fingerprint를 만든다. 새 medicine이 기존 `SupportsInjuryTreatment` capability와 물리 질량을 선언하면 코어 코드 변경 없이 profile·감사에 자동 포함된다.
- 선택된 supply는 exact item/quantity `1`과 실제 unit gram으로 admission한다. profile은 상한 권위이며 실제 점유 gram을 대신하지 않는다.
- synthetic medicine canary는 data fixture 등록만으로 후보 집합·profile digest·delivery/admission/Sink/tare/save join에 포함되어야 하고 production core diff는 0이어야 한다.

### 113.4 destination lifetime·terminal·저장·GC

```text
ActiveAuthority(sequence N)
→ DestinationDrainPrepared(target may be active or terminal)
→ EffectCommittedAwaitingAck
→ OwnerAcknowledgedAwaitingClosure
→ ClosedAwaitingCheckpointGc(sequence N)
→ optional ActiveAuthority(sequence N+1)
→ durable save
→ child GC
→ matching closed upper join clear
```

- doctor/rescuer 해제만으로는 material destination을 종결하지 않는다. order가 active이면 다른 구조자가 이어받는다.
- 시설 도착 실패, 치료 중 시설 소실, 수동 재배정은 주문을 terminal로 만들지 않지만 현재 destination lifetime을 drain한다. drain을 닫기 전 새 facility ID를 기존 destination에 덮어쓰거나 다음 lifetime을 열지 않는다.
- 치료 완료, 명시 취소, 환자 사망은 active join이 있으면 새 join을 만들지 않고 그 target을 terminal로 승격한다. 이미 Sink된 supply는 환불하지 않고 잔여 destination custody만 보존적으로 회수한다.
- carried cargo는 현재 위치 recovery-drop 계약을 사용한다. 미픽업 lease는 논리 해제하고 physical carried slice를 source로 순간이동하지 않는다.
- sequence는 실패해도 재사용하지 않는다. current sequence는 0 또는 active join sequence와 같고, next sequence는 current와 모든 closed join보다 커야 한다. 미종결 join은 주문당 최대 1개, closed join은 bounded collection에 sequence ordinal로 보존하며 새 current와 공존할 수 있다. 상한 초과는 overwrite가 아니라 fail-loud다.
- owner 상태 `MaterialDestinationDraining`은 enum 끝에 append하며 AI 예약·구조·치료가 선택하거나 덮어쓰지 못한다. drain closure 때 frozen target state/status/parameters를 적용한 뒤에만 current destination·capacity·revision·fingerprint·supply state를 비운다.
- owner/child phase는 `Prepared ↔ Prepared/Releasing*`, `EffectCommittedAwaitingAck ↔ EffectCommittedAwaitingOwnerAck`, `OwnerAcknowledgedAwaitingClosure/ClosedAwaitingCheckpointGc ↔ OwnerAcknowledgedAwaitingCheckpointGc`만 허용한다. current open join 없음 상태는 exact claim/profile pair가 반드시 존재해야 한다.
- raw whole-save/direct registry/outgoing captured preflight는 upper order의 모든 join과 Items child를 양방향 exact join한다. restore candidate도 live publication 전에 같은 phase matrix를 재검증한다. normal current는 live facility 위치, drain current는 삭제된 old facility 대신 frozen join 좌표로 pair를 재구축한다.
- restore는 local validation → 기존 Sink join → drain upper/lower join → claim/profile candidate 재구축 → aggregate publication → Sink recovery → immediate drain recovery → gameplay tick 순서다. Character Medical save section은 facility world section에 의존한다.
- durable participant `310.character-medical-supply-destination-drain-checkpoint-gc`는 order `310`에서 closed child-first/upper-last로 수거한다. upper clear 실패 시 child exact rollback, child 실패 시 upper 보존, empty replay idempotence, 현재 destination과 주문/history retention을 요구한다.

### 113.5 구현 체크리스트

- [x] canonical lifetime identity, supply-capacity fingerprint와 owner-neutral destination adapter를 추가했다. exact prefix·current/next sequence와 sequence-scoped identity를 current-format 권위로 사용한다.
- [x] 치료 facility와 합법 supply catalog에서 positive max gram profile을 만들고 claim/profile pair를 원자 게시·복원한다. medicine/extracted-blood 최대값, replay/revoke/restore/tamper focused fixture를 통과했다.
- [x] 선택·배송·FacilityBuffer 입고가 `ExactGramRequired`를 사용하고 profile 없는 count fallback을 fail-loud한다.
- [x] `ReleaseStacksByDestination`을 제거하고 시설 도착 실패·시설 소실·수동 재배정·완료·취소·환자 사망을 common custody drain begin-or-resume으로 전환한다.
- [x] Character Medical current-format schema, clone/validation/restore와 sink/custody upper join을 확장한다.
- [x] raw whole-save, direct registry, outgoing captured save와 detached restore candidate의 양방향 preflight를 등록한다.
- [x] durable checkpoint GC와 exact rollback/idempotence를 등록한다.
- [x] focused medicine/혈액/gram/claim-profile/reserved-routed/carried/buffer/sink/terminal/save/GC fixture를 통과했다. 한 주문의 sequence 1·2 closed join, active-1/closed-N, terminal target의 re-Downed active target 재개방을 포함한다.
- [x] synthetic medicine data canary에서 capability만 선언한 QA medicine이 profile·delivery/Sink/tare/save 검증에 포함되고 production core의 canary/content-ID branch `0`임을 증명했다.
- [x] manifest를 두 번 재생성해 `medical.character-supply=migrated`, input remaining `32`, bypass `4`, orphan/unclassified `0/0`, byte/hash/mtime 동일을 기록했다.
- [x] Unity current-source compile과 focused suites를 Console Warning/Error `0/0`으로 통과했다.
- [ ] 사용자 소유 dirty scene을 저장·unload·overwrite하지 않는 actual autonomous medical PlayMode를 통과한다.

이번 구조 slice는 authored kg·BOM·WU·치료 potency·성공률·시간·시설 공간·가격·EWU를 변경하지 않는다. 구조 owner migration이 완료되더라도 Batch A~H와 전체 진행률 범위는 final balance gate 전까지 보수적으로 유지한다.

### 113.6 current evidence

- current Unity source에서 `V27_CHARACTER_MEDICAL_CARRY_FOCUSED_BUNDLE_PASS`, `V27_CHARACTER_MEDICAL_LIFECYCLE_PERSISTENCE_PASS`, `V27_CHARACTER_MEDICAL_OWNER_FINAL_FOCUSED_PASS`가 PASS했고 최종 Console Warning/Error는 `0/0`이다.
- 공용 custody outbox/service는 source-reserved·routed·carried·FacilityBuffer를 검증했고, detached restored carry는 Downed 위치의 `TransientCarryRecoveryDrop`으로 수량·gram·provenance를 보존했다. mixed/unowned/foreign operation은 cargo·lease·intent 무변경으로 fail-loud했다.
- order `310` GC fixture는 empty replay, child-first/upper-last, child publish failure, upper drift rollback, active join 보존과 durable status mapping을 모두 통과했다.
- manifest CSV SHA-256은 `F8FBC6644CF6CFBBBA816AA2E54E97C4FCE65A525750E956CC5DD112AE72A75A`, report는 `843D74AC3BB5F68E96AC2B38FF4CABD50E856F7EAE3B06D4A58C8C1940CC2B8D`다. 두 번째 생성에서 hash·length·mtime 변화가 0이며 input `7/39`, remaining `32`, output `6/6`, bypass `4`, orphan/unclassified `0/0`이다.
- broad Combat suite의 기존 `건설형 엄폐물` 항목은 이 slice와 무관하게 실패했으므로 성공 증거에서 제외했다. 동일 실행에서 이미 통과한 save section/durable coordinator를 focused medical strict-save와 다시 실행해 현재 의료 저장 회귀를 독립 PASS로 확인했다.
- actual autonomous medical PlayMode는 사용자 소유 dirty `GameplayScene.unity`를 건드리지 않는 실행 경로가 확보될 때까지 OPEN이다. 이 구조 migration은 authored kg·WU·EWU·가격 또는 Batch A~H 완료를 의미하지 않는다.

## 114. 2026-08-28 등록형 내구 시설 장비 슬롯과 `research.arcane-index` 입력 owner 계약

상태: **공용 policy/capacity/slot·current-format upper save·3단계 cross-preflight·동기 restore recovery는 current-source focused PASS / research live adapter·wear/effect·checkpoint-GC fixture·manifest·PlayMode OPEN**.

### 114.1 전수 감사 결론과 범위

- `research.arcane-index`, `infrastructure.climate`, `invasion.signal-horn`은 모두 “살아 있는 시설 한 곳이 내구성 물리 장비를 FacilityBuffer로 요청하고 사용 중 durability를 감소시키는” 동일 계열이다. 세 owner를 콘텐츠 ID 분기와 전용 claim/profile 코드로 각각 구현하지 않는다.
- 첫 수직 슬라이스는 `research.arcane-index`다. 현재 raw 시설 persistent ID를 destination으로 쓰고, `record:arcane-index` 한 개를 count 배송하며, same-cell 추론으로 FacilityBuffer에 넣는다. exact claim/profile, positive gram admission, 시설 소실 terminal drain과 physical child 저장 join이 없다.
- usable 도구가 있으면 현재 수치 `research WU ×1.1`, `approved WU당 durability 0.01`을 적용한다. `TrySetInstanceComponent` 실패를 무시하면 무료 보너스가 가능하므로 도구 wear commit 성공 전에는 보너스가 반영된 연구 진행을 publish하지 않는다.
- durability 0 stack은 usable 조회에서는 제외되지만 기존 중복 방지 조회에는 남아 신규 도구 요청을 영구 차단한다. 이를 삭제하거나 Sink하지 않고 현재 위치·물리 소유권을 보존하는 terminal custody drain으로 시설 슬롯에서 퇴역시킨 뒤 다음 sequence를 연다.
- 이번 slice는 기존 `record:arcane-index`의 `1,300g`, 연구 배율, wear, BOM, WU, 연구 진행량, 가격, 시설 면적과 저장 콘텐츠 수치를 바꾸지 않는다. `Climate`와 `Invasion`의 실제 등록·저장 어댑터는 research 수직 슬라이스가 green인 뒤 별도 owner 체크리스트로 진행한다.

### 114.2 공용 확장 폐쇄 구조 계약

| 항목 | 계약 |
|---|---|
| 콘텐츠 정의 | 도메인이 제공하는 immutable `DurableFacilityEquipmentSlotPolicy`가 owner domain, capability ID, destination namespace, 합법 장비 profile, 최대 동시 수량, 사용 효과와 wear 정책을 정의한다. 공용 런타임은 item ID를 비교하지 않는다. 같은 종류의 신규 장비는 policy/catalog 등록만으로 참여한다. |
| 런타임 상태 | Items의 공용 slot aggregate가 `(policyId, ownerSubjectId)`별 positive sequence와 active-1/closed-N slot lifetime join만 소유한다. claim/profile, stack·lease·intent·carry·FacilityBuffer와 drain child도 Items의 각 기존 단일 권위를 유지하되 slot aggregate가 그 상태를 복제하지 않는다. durability component는 물리 item instance가 유일한 권위다. 도메인은 slot state를 저장하지 않는다. |
| 명령 | `EnsureSlot`, `RequestEquipment`, `TryApplyWearAndEffect`, `BeginOrResumeDrain`, `AcknowledgeAndClose`만 상태를 바꾼다. 하드코딩된 item ID별 `if/switch`와 raw `ReleaseStacksByDestination` 호출은 금지한다. |
| 조회 | 살아 있는 facility snapshot, capability가 선택한 equipment profile, current unit mass, active slot lifetime와 exact claim/profile을 immutable query로 읽는다. save DTO는 gameplay query 입력이 아니다. |
| 식별자 | destination은 `facility-input:exact:{ownerDomain}:{facilityId}:{sequence:D8}`, owner operation은 `{ownerDomain}:slot:{facilityId}:{sequence:D8}`, drain parent/step은 canonical formatter 한 곳에서 만든다. raw 시설 ID를 destination으로 재사용하지 않는다. |
| 저장 | 별도 공용 `DurableFacilityEquipmentSlotSaveSection`의 upper DTO가 policy ID, owner subject/facility ID·좌표, sequence, destination, capacity/revision/fingerprint, active/closed join과 frozen requirement subjects를 저장한다. Research/Climate/Invasion DTO에 같은 구조를 반복하지 않는다. claim/profile·physical child를 복제하지 않으며 current-format 필수 필드 누락·알 수 없는 policy는 migration 없이 fail-loud한다. |
| 의존성 | Foundation의 capability/ID 계약 ← Items의 owner-neutral claim/profile/admission/custody drain ← 도메인 registration/upper save adapter 순서다. Items가 Research/Climate/Invasion을 참조하지 않는다. |
| 실패 | unknown/duplicate policy, 비양수 mass/capacity, partial claim/profile, wrong item/capability, sequence reuse, upper/lower orphan, facility/좌표/revision/fingerprint drift, wear mutation 실패와 terminal physical custody 잔존을 world publication 전에 거부한다. |
| 전환 | research raw destination 신규 작성과 direct release를 제거한다. 이미 존재하는 raw destination은 과거 save migration으로 흡수하지 않으며 current-format restore에서 typed failure다. |
| 검증 | synthetic policy/item이 core diff 없이 discovery→profile→delivery→wear→drain→save/restore에 참여해야 한다. content ID branch manifest는 0, unsupported capability는 fail-loud여야 한다. |

### 114.3 `research.arcane-index` 수명주기와 원자성

```text
NoSlot
→ ActiveAuthority(sequence N, exact 1,300g socket)
→ Requested / Routed / Carried / Buffered
→ UsableTool
→ WearCommitted → ResearchEffectPublished
→ DepletedOrFacilityLostDrainPrepared
→ ChildEffectCommittedAwaitingOwnerAck
→ ClosedAwaitingCheckpointGc
→ optional ActiveAuthority(sequence N+1)
```

- 연구 시설이 live/capable일 때 policy에서 한 개 장비의 exact current gram을 계산하고 claim/profile pair를 원자 게시한다. profile은 positive `long` gram이며 현재 item mass authority revision/fingerprint를 포함한다.
- 배송 요청은 active pair와 동일 policy item만 허용한다. source-reserved/routed/carried/buffered 수량 합이 slot 수량 `1`을 넘으면 추가 요청하지 않는다. profile 없는 count fallback은 fail-loud한다.
- 도구 사용은 선택한 exact stack ID와 component revision을 동결한다. wear mutation이 성공한 경우에만 1.1배 연구량을 publish한다. mutation 실패·stack replacement·revision drift에서는 보너스 연구 진행을 0으로 하고 typed failure를 반환한다.
- 내구도가 0에 도달한 stack은 질량을 삭제하지 않는다. active slot을 terminal drain하고 buffered 물품은 합법 Loose/warehouse haul 경로, carried 물품은 현재 위치 recovery-drop 계약으로 보존한다. drain closure 전 다음 sequence나 신규 배송을 열지 않는다.
- 프로젝트 취소만으로 reusable 도구를 퇴역시키지 않는다. 시설 파괴·시설 ID 소실·policy 제거·도구 고갈만 terminal 원인이다. 연구 프로젝트 전환은 같은 시설 slot을 재사용한다.
- drain은 source lease, route intent, carried cargo, FacilityBuffer occupancy와 reserved grams가 모두 terminal인 뒤 claim/profile을 revoke한다. 실패하면 active upper owner와 child receipt를 유지하고 다음 Tick/restore에서 같은 operation을 resume한다.

### 114.4 current-format 저장·복원·checkpoint 계약

- 공용 slot save section은 Modular Facility World와 Physical Items에 의존하고 도메인 save section에는 의존하지 않는다. 도메인은 restore된 live facility와 등록 policy를 제공하며 별도의 slot DTO를 쓰지 않는다.
- raw whole-save, direct registry, outgoing captured envelope와 detached restore candidate에서 모든 slot upper lifetime과 Items custody child를 양방향 exact join한다. upper-only, lower-only, duplicate destination/sequence, wrong phase/quantity/gram/fingerprint를 publication 전에 거부한다.
- normal active lifetime은 live facility 좌표에서 claim/profile을 재투영한다. draining lifetime은 삭제될 수 있는 old facility 대신 frozen upper 좌표와 fingerprint를 사용해 child를 복원하되 신규 gameplay admission은 열지 않는다.
- restore 순서는 local policy/upper 검증 → physical child preflight → claim/profile candidate staging → research aggregate publication → drain resume → ordinary research Tick이다.
- durable checkpoint participant는 child-first/slot-upper-last로 closed join만 수거한다. child publish 실패 시 upper 유지, upper clear 실패 시 child exact rollback, active join과 각 도메인 진행/history는 유지한다. 현재 사용 중인 `100/200/300/310`과 충돌하지 않는 `320.durable-facility-equipment-slot-checkpoint-gc`를 사용한다.

### 114.5 구현 체크리스트

- [x] 공용 durable facility equipment slot policy/catalog/query와 canonical identity/fingerprint를 추가한다.
- [ ] synthetic policy/item 등록만으로 profile·admission·wear·drain에 참여하고 unknown capability가 fail-loud하는 canary를 추가한다.
- [ ] `research.arcane-index`를 raw facility destination과 item-ID 분기에서 등록형 policy와 sequence-scoped exact destination으로 전환한다.
- [ ] current `1,300g × 1` claim/profile, delivery limit와 component-aware exact admission을 연결한다.
- [ ] wear mutation 성공과 연구 보너스 publication을 원자 경계로 묶고 mutation failure/revision drift에서 연구 진행이 증가하지 않음을 검증한다.
- [ ] 내구도 0·시설 소실을 common custody drain으로 처리하고 물리 삭제·순간이동·replacement deadlock을 0으로 만든다.
- [x] 공용 slot current-format upper payload/save section, clone/validation/restore와 raw/captured/registry/detached cross-save preflight를 추가한다. Research DTO에는 slot 필드를 추가하지 않는다.
- [x] durable child-first/upper-last checkpoint GC와 rollback/idempotence를 추가한다.
- [ ] source-reserved/routed/carried/buffered, facility loss, depleted replacement, save tamper/missing/duplicate/lower-only focused fixture를 통과한다.
- [ ] Unity current-source compile, focused Research/Items/save suites와 Console Warning/Error `0/0`을 통과한다.
- [ ] owner manifest를 두 번 생성해 byte/hash/length/mtime 동일을 확인한 뒤에만 `research.arcane-index=migrated`, input `8/39`, remaining `31`로 변경한다.
- [ ] 사용자 소유 dirty scene을 건드리지 않는 실제 AI research delivery→use→deplete→replacement PlayMode를 통과한다.

구조 owner migration과 manifest disposition은 focused/compile/save/결정론 증거 뒤에만 닫는다. 실제 AI PlayMode가 열려 있으면 최종 runtime acceptance는 OPEN이다. 큰 Batch A~H, 전수 kg·EWU·가격·6인 생존망 진행률은 이 구조 slice 하나로 올리지 않는다.

### 114.6 2026-08-28 공용 기반·저장 focused evidence

- registration 기반 policy/capacity/usability canary와 owner-neutral slot runtime이 exact `1,300g`, sequence-scoped destination, delivery deduplication, admission fence, child custody drain을 통과했다. 콘텐츠 item ID 분기는 추가하지 않았다.
- 공용 upper section `items.durable-facility-equipment`은 active-1/closed-N, 전역 단조 sequence, policy/assignment/capacity source fingerprint를 current-format으로 저장한다. Research/Climate/Invasion DTO에는 공통 slot 구조를 복제하지 않았다.
- upper가 child의 parent/step/owner/facility/destination/source authority/request/phase/count/quantity/gram/commit/receipt를 독립 동결하고 exact 비교한다. owner/parent/step/destination 중 하나라도 durable namespace를 주장한 lower-only child는 fail-loud한다.
- raw whole-save, direct registry envelope, outgoing captured save가 같은 `DurableFacilityEquipmentCrossAggregateJoin`을 사용한다. active facility missing, upper-only/lower-only, duplicate active key/sequence와 parent/step/source fingerprint/input mass/phase tamper fixture가 PASS했다.
- restore participant는 기존 `219.world.offense-supply-packages`와 충돌하지 않는 `222.world.durable-facility-equipment-slots`를 사용한다. claim `220`·capacity `221` 이후 upper `222`가 publish되고 rollback은 역순이다.
- fence replacement와 slot restore map은 완성된 replacement map의 참조 교체로 publish하며, revision/sequence overflow를 외부 mutation 전에 검사한다. 복원 완료 hook은 고정 반복 횟수 대신 sequence별 phase·remaining actor/operation의 단조 감소를 강제하고 ordinary gameplay tick 전에 pending drain을 끝낸다.
- current Unity compile 뒤 `Durable Facility Equipment Slot Checkpoint GC`, `Durable Facility Equipment Save Contracts`, `Durable Facility Equipment Slot Runtime Contracts`, `Production Economy Contracts`가 PASS했고 Console Warning/Error는 `0/0`이다. GC fixture는 empty `AlreadyApplied`, child-first/upper-last publish·complete, second-call idempotence, upper publish 실패의 child rollback, active/draining 보존과 durable participant status mapping을 검증했다.
- 이 증거는 authored kg·WU·EWU·가격을 바꾸지 않았고 `research.arcane-index`의 실제 delivery→wear→effect owner migration도 아직 수행하지 않았다. 따라서 manifest는 계속 input `7/39`, remaining `32`, bypass `4`, Batch A~H `0/8`로 유지한다.

## 115. 2026-08-28 M06 보철조립대 공용 생산 권위 연결

상태: **공용 workstation/output-buffer 구조·current-source compile·전수 생산 감사·실제 M06 주문→보철 출력→AIHaul→kg 창고→저장 왕복 PASS**.

### 115.1 구현·감사로 닫은 범위

- [x] `BuildingProstheticAssemblyAbility.outputCapacity`의 런타임 소비자가 0임을 확인하고 죽은 중복 권위를 제거했다. 속도·품질 보정과 내부 재고 권위는 유지했다.
- [x] M06에 공용 `BuildingProductionWorkstationAbility`를 `workstationTag=m06`, `stockSensorInstallationItemId=component:stock-sensor-panel`로 연결했다.
- [x] M06에 공용 `BuildingProductionBufferAbility`를 `defaultBatchCapacity=3`, `physicalOutputBufferCycleCapacity=3`, `allowOverflowDump=false`로 연결했다.
- [x] 세 보철 레시피가 기존 `ProductionRecipeSO.WorkstationTag` fallback을 통해 모두 `m06`을 사용함을 검증했다. 레시피 에셋과 kg·BOM·WU·가격은 변경하지 않았다.
- [x] 세 출력 정의의 현재 단위 질량 `1,800g`과 3-cycle 권위로 최대 `5,400g`을 공용 buffer 투영이 사용할 수 있게 했다.
- [x] targeted builder는 기존 managed-reference object/ID를 보존하고 두 공용 ability만 삽입한다. exact authority가 이미 있으면 두 번째 실행은 byte no-op이다.
- [x] 전수 생산 감사기를 355개 레시피에 적용했다. 출력 없는 소각 4개는 `ProductionFlowRole.Sink`와 semantic facility로 검증하고, 출력 레시피는 실제 workstation·`[2,4]` buffer를 요구한다.
- [x] 출력 정의는 하드코딩된 item-ID 예외가 아니라 standard/ammunition/apparel/surgical capability family로 검증한다. 특수 capability끼리의 중복은 거부하되 definition+special 중첩은 등록된 special 우선 규칙을 따른다.

### 115.2 current-source 증거와 열린 exit gate

- [x] 최신 `Assembly-CSharp-Editor.dll`에서 `ProductionWorkshopDebugScenarios`, `ProductionAmmunitionPreparedOutputDebugScenarios`, `SurgeryDebugScenarios`, `ProductionEconomyDebugScenarios`를 한 clean-console 구간으로 실행해 aggregate PASS를 확인했다. Console Warning/Error는 `0/0`이다.
- [x] M06 에셋은 `17 additions / 1 deletion`, SHA-256 `DD134761E335C83CC02B8C3089291904488C4CE140A3E711EE426B7957BF1DED`이다. 두 번째 targeted apply 뒤 byte가 같고 `.meta`와 GameplayScene은 변경되지 않았다.
- [x] 실제 M06을 배치하고 `recipe:surgery:prosthetic-arm` bill을 정상 API로 추가했다.
- [x] steel 2·lumber 1·cloth 1의 물리 BOM, 작업 완료, unique 보철 1회 `FacilityOutputBuffer` 생성, 공용 AIHaul, `1,800g` kg 창고 입고와 buffer drain을 PlayMode에서 증명했다.
- [x] 사용자 소유 dirty GameplayScene을 저장·unload·overwrite하지 않는 sanitized 임시 Gameplay 검증 환경에서 위 경로와 저장/복원 무복제를 실행했다.

2026-08-29 fresh live 증거: 일반 정의인 수술 부품을 resource-only 카탈로그로 조회하던 창고 호환 경계를 공용 `IDungeonItemCatalogProvider`의 `StockCategory` 권위로 교정했다. 공용 생산 회귀와 prepared-output planner gate를 통과한 뒤 실제 Title→Preparation→sanitized Gameplay에서 `recipe:surgery:prosthetic-arm`을 실행했다. 결과는 `surgery:prosthetic:arm:left ×1`, unique item instance와 `medical:surgical-part-output` component, exact `1,800g`이며 acknowledged planned-output provenance가 더 이상 in-flight publication lock으로 오인되지 않는다. AIHaul은 무감속 `19.13kg` actor로 exact 1개를 25kg warehouse에 입고했고, 완료 뒤 haul intent·admission·reserved inbound가 모두 0으로 은퇴했다. terminal bill은 즉시 제거됐고 whole-save restore, warehouse/UI `1.8kg/25kg`, recapture identity SHA-256 `56F51F8DD85BDEE2ECA6B6A763080DA61D501FFE5908DFFE4B0C6EFFFD356304`, 두 번째 restore 수량/질량 `1/1,800g` 무복제를 통과했다. 비유기 수술 부품 freshness는 current-format finite contract에 맞는 canonical `0` sentinel로 교정했다. report `RESULT=PASS; failures=0`, 런타임 및 최종 Unity Console Warning/Error `0/0`, 임시 asset/scene cleanup 0, 공식 GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 유지다.

이 checkpoint로 실제 M06 surgical production live 행만 닫는다. Batch A는 `29/31`; normal sawmill cancel/Downed/mid-haul restore와 output manifest/fault-matrix 상위 행은 계속 OPEN이다. 전체 V27은 보수적으로 약 `72–79%` 완료 / `21–28%` 남음이며 authored kg·quantity·BOM·WU·EWU·가격·시설 공간 수치는 변경하지 않았다.

### 115.3 equipment-module exact Source 우회 제거 증거 (2026-08-28)

- [x] `EquipmentModuleRuntime`의 생성·교체 반환·분리 반환에서 production `SpawnExistingUniqueItemAt` 직접 호출을 `0`으로 만들고 `PhysicalItemExactSourcePublicationService`의 prepare/targeted release/rollback 경계로 이관했다.
- [x] stack ID 할당 전 detached module 상태를 동결하는 `combat.equipment-module-stack-binding@1` capability와 `EquipmentModulePreparedOutputBinder`를 등록했다. 공용 publication 코어는 module item ID를 분기하지 않고 capability registry를 통해 stack ID를 결합한다.
- [x] binder 검증 경계는 mutable `WorldItemStackRecord`를 공개하지 않고 immutable `FacilityBufferPublishedUniqueOutputSnapshot`만 전달한다. prepared/committed module aggregate와 physical component/source stack ID를 exact 비교한다.
- [x] current-source Unity runtime/editor compile 뒤 공용 planned-output focused suite와 module 생성→감정→복원→장착→분리→current-format 저장 왕복 focused lifecycle이 PASS했다.
- [x] owner manifest는 `bypass=0`, `orphan=0`, output owner `9/9 migrated`, transport-delegated exact `1`을 증명했다. input owner는 `8/39 migrated`, `31 remaining`이다. 두 번째 artifact 생성은 byte hash·length·mtime 변화가 `0`이다.
- manifest CSV SHA-256: `43354306D869FBC60BE95DC8D3A253CC6FBF037508A114296B86DD3247BFF3FB`
- manifest report SHA-256: `5CB6739BD038BEE79D019543C31AF01DBB6C98DDADAB8288FD592A60BAA63ECE`

이 체크포인트는 manifest zero gate의 `bypass/orphan` 하위 조건만 닫는다. `inputRemaining=31`은 Batch C 분모이고 output 전체 fault matrix가 남았으므로 상위 체크리스트 행은 아직 OPEN이다. 이후 2026-08-29 M06 live 행이 닫혀 Batch A는 `29/31`이다. authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값은 변경하지 않았다.

## 116. 2026-08-29 제재소 실제 운반 fault·저장 폐쇄

상태: **`recipe:sawmill-lumber` 실제 생산→prepared exact route→공용 AIHaul→kg 창고와 pre-pickup cancel·active cancel·Downed current-cell recovery·mid-haul restore PASS**.

- [x] 실제 `resource:log ×2 → material:lumber ×3` 주문을 완료하고 exact `3 × 1,200g = 3,600g` physical output과 `14,400g` 4-cycle FacilityOutputBuffer 권위를 유지했다.
- [x] pickup 전 취소는 lease·intent·warehouse admission만 해제하고 같은 routed stack·custody·warehouse target을 보존했다.
- [x] pickup 뒤 active cancel은 carried slice, source stack ID, operation ID, exact lease와 `3,600g` admission을 유지한 delivery-only replan 상태로 종료했다.
- [x] 운반자를 source `(10,0)`·warehouse `(13,0)`과 다른 reachable cell `(23,0)`에서 Downed 처리했다. 같은 carried stack ID·수량 3·질량 3,600g이 그 현재 셀에 `TransientCarryRecoveryDrop`으로 정확히 한 번 나타났고, source/warehouse 순간이동과 수량 복제는 0이었다.
- [x] recovery provenance의 owner operation·source stack·carrier·Downed kind·deadline, warehouse destination ID, actor 접근칸 `(12,0)`, exact-route custody의 warehouse 본체 `(13,0)`과 receipt/revision fingerprint를 각각의 의미에 맞게 검증했다.
- [x] Downed 뒤 actor carry·haul intent·lease·admission·reserved inbound은 0으로 해제됐고, recovery stack의 route custody만 물리 권위로 남았다. V18 physical save의 recovery metadata validator도 오류 0으로 통과했다.
- [x] fault 직전 whole-save checkpoint 복원 뒤 같은 actor·warehouse·carried/source stack·operation·admission `3,600g`·route를 재바인딩했다. documented transient visitor restore 차이 때문에 raw whole-root byte는 비교 권위로 쓰지 않고, cargo/production/route/save section semantic fingerprint와 exact join을 요구했다.
- [x] 기존 mid-carry admission `+1g` 변조는 whole-root 무변경으로 원자 거부됐고, 정상 restore 뒤 실제 Brain이 AIHaul을 재개해 warehouse에 수량 3·질량 3,600g을 적재했다. 완료 후 active intent와 inbound reservation은 0이었다.
- [x] terminal bill retirement, UI `3.6kg/25kg`, whole-save restore, recapture identity SHA-256 `EAB7F0D019A8B1355E426C5FFEA446D75E8AB695F8EE2757D2827B3AE40F61A1`, 두 번째 restore 무복제를 통과했다.
- [x] 최종 report `Artifacts/QA/prepared-output-sawmill-live-playmode-report.txt`는 `62 PASS`, `RESULT=PASS; failures=0`, SHA-256 `E148D4D610235EEE9101FF8460B68F2834B0C43787A4E96CE300908718D3530E`다. 런타임 캡처와 Unity 외부 Console Warning/Error는 `0/0`; request·synthetic temp는 0; 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 유지됐다.

이 체크포인트로 제재소 live/fault 행만 닫는다. Batch A는 `30/31`; 남은 한 행은 **output owner aggregate manifest zero 유지 + 전체 output partial/cancel/Downed/restore fault matrix**다. Batch C input remaining `31`은 별도 분모다. 전체 V27 진행률은 기존 보수 범위 `72–79%`를 유지하며, 이 검증에서 authored kg·capacity·quantity·BOM·WU·EWU·가격·ScriptableObject·prefab·scene 값은 변경하지 않았다.

## 117. 2026-08-29 Batch A output aggregate 폐쇄

상태: **Batch A `31/31` 완료 / Batch C input remaining `31` 및 B–H 수치·회귀 작업은 OPEN**.

- [x] current-source manifest가 output owner `9/9 migrated`, output remaining `0`, bypass/orphan/unclassified `0/0/0`, transport-delegated-exact `1`을 유지한다.
- [x] 9개 output owner를 exact-source 3개, standard prepared-output 1개, production-domain 2개, planned unique-output 3개의 공용 계약으로 명시적으로 매핑했다. 동일한 fault를 owner별로 복제하지 않고 manifest의 공용 인터페이스 위임과 공용 fixture를 함께 요구한다.
- [x] 공용 exact-route suite가 partial split, multi-step partial restore, unique partial reject, rollback/replay와 carried/stored/recovery descendant 보존을 통과했다.
- [x] synthetic live report가 `1g` 부족 output-space wait와 동일 outcome 재개, pre-pickup cancel, mid-carry admission tamper 원자 거부와 restore, 최종 `20,000g` 입고를 증명한다.
- [x] sawmill live report가 실제 `3,600g` prepared output의 active cancel, Downed current-cell recovery, transient authority release, checkpoint 복원, Brain 재개 배송을 증명한다.
- [x] M06 live report가 unique prosthetic `1,800g` exact capability, kg warehouse 입고, post-delivery save/restore와 두 번째 restore 무복제를 증명한다.
- [x] `V27BatchAOutputClosureDebugScenarios`는 이전 artifact를 신뢰하지 않고 같은 실행에서 fresh manifest를 캡처·저장한 뒤 source digest와 output zero ratchet을 검증한다.
- [x] 최종 `Artifacts/QA/v27-batch-a-output-closure.txt`는 CertifiedSeed eligibility/contributor source까지 포함해 재생성했고 `result=PASS`, SHA-256 `3AB49384785188D05FD26872E69A8B26FF043A061D294AD9E3FA109ED73E20AF`다.
- [x] manifest TXT/CSV SHA-256은 `12100B34655AFC95360838830E814B8D776AC41B527099975A88E71F44765E9A` / `8A282F4965A5F4BDBCD94122E8C2A556A1588452568E11C5E37ACECBB2C5578E`다. 두 번째 실행에서 세 artifact 모두 length/hash/mtime 변화가 `0`이었다.
- [x] 최종 Unity Console Warning/Error는 `0/0`; request·synthetic backup은 없고 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 유지됐다.

Batch A 완료는 출력 생산·운반의 공용 구조와 fault 경계를 닫았다는 뜻이다. authored item kg 전수 적용, FacilityBuffer input owner `31`, 상태 기반 질량, WIP/Sink, EWU·가격 재생성, 6인 생존망과 최종 다중 seed는 B–H에 남아 있으므로 전체 진행률은 보수 범위 `72–79%`를 유지한다.

## 118. 2026-08-29 WorldResource 포함 10-owner Batch A 재폐쇄

상태: **Batch A `31/31` current-source PASS / Batch B `34/40` / Batch C input `8/39`, remaining `31` / D–H OPEN**.

- [x] `economy.world-resource-output`을 current owner manifest에 추가해 output `10/10 migrated`, remaining/bypass/orphan/unclassified `0/0/0/0`을 fresh source에서 재생성했다.
- [x] WorldResource runtime은 등록형 topology binding과 frozen output을 사용하고, detached candidate rebuild 실패가 기존 live node identity·source sequence·pending authority를 변경하지 않는다.
- [x] finite/renewable source debit은 physical exact-source prepare와 결합되며, release acknowledgement 뒤 authority retirement만 실패한 경우 pending이 save를 차단하고 동일 mode/target 재시도만 허용한다. 4-stack acknowledgement mutation count는 최초 4회 이후 mismatch 및 exact retry에서도 4회로 유지된다.
- [x] frozen pending output은 restored random root seed와 exact cross-join하고 authored Grand Project maximum을 넘는 factor를 거부한다. 저장된 자기 seed로만 결과를 다시 만드는 self-consistency 우회는 허용하지 않는다.
- [x] 확률상 물리 output 0개인 cycle은 fake item·publication 없이 source debit과 completed sequence를 정확히 한 번 commit한다.
- [x] `Artifacts/QA/v27-world-resource-transaction-fault-matrix.txt`는 `RESULT=PASS`, SHA-256 `722FEDC805CF3ED449166ED73ACF21F691DE020D6559A9A2FBDFAB749384B15D`다.
- [x] `Artifacts/QA/v27-batch-a-output-closure.txt`는 `result=PASS`, SHA-256 `8A18E2B5F9F5EA1FCE26E29A8010DF574DDC034C0295858E0ED1ABDDCAC25200`다.
- [x] manifest TXT/CSV SHA-256은 각각 `50C6475F89C8220384576359DC7101FA2CE694423DCA036816AC54827AD8C155` / `AC4229A0D514DCD4EF2EED7A3A60B256C034B6233F343EB8C570F849E9D22AC2`다.
- [x] 네 artifact의 두 번째 실행은 byte hash·length·mtime 변화가 모두 0이다. Unity current-source compile과 focused aggregate는 Console Warning/Error `0/0`, 공식 GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`을 유지했다.

이번 checkpoint는 출력 소유권·복원 원자성과 결정론만 교정했다. authored kg·capacity·quantity·BOM·WU·EWU·가격·시설 공간·ScriptableObject·prefab·scene 수치는 변경하지 않았으므로 `밸런스 영향 없음 / 구조 교정 검증 완료`다. Batch A 완료는 B–H 또는 전수 밸런스 완료를 의미하지 않는다.

## 119. 2026-08-29 WorldResource maximum proof-bound 폐쇄

상태: **WorldResource maximum 하위 경계 PASS / Batch A `31/31` 재회귀 PASS / Batch B 상위 `34/40` 유지 / crop actual publication OPEN**.

- [x] `WorldResourceSourceBindingCatalog`를 단일 생성자 하드코딩 열거에서 `IWorldResourceSourceBindingContributor` 집합으로 전환했다. contributor·binding은 ordinal stable 정렬되고 duplicate contributor ID, binding ID, visual/renewable key를 fail-loud한다. built-in 4행은 한 contributor이며 신규 contributor가 들어와도 runtime/maximum projector 코드는 변경하지 않는다.
- [x] pure `WorldResourceOutputMaximumEnvelopeAuthority`가 binding `4`개를 고유 recipe `3`개로 귀속하고, source recipe의 모든 `probability>0` non-loss line을 공용 `IProductionOutputMaximumMassRegistry`로 투영한다. `probability=0`과 `DeclaredLoss`는 물리 용량에서 제외하고 factor 적용 수량은 exact Ceil한다.
- [x] current source maximum은 `source:grass=4×80=320g`, `source:logging=5×1,800+1×200=9,200g`, `source:saltstone=3×1,600+1×500=5,300g`이다. grass/brush 두 binding은 `source:grass` 한 envelope로 접힌다.
- [x] frozen pending output은 recipe digest·line vector뿐 아니라 maximum aggregate gram과 maximum source digest를 함께 저장한다. actual line quantity/mass와 aggregate mass가 proof를 넘거나 item/role/probability/unit gram이 drift하면 source debit과 publication 전에 fail-loud한다.
- [x] current-format WorldResource schema는 `V4`이며 복원은 root seed, recipe/factor, maximum mass/source digest를 모두 재구성해 exact 비교한다. maximum mass `+1g` 및 proof digest 변조가 restore publication 전에 거부됨을 focused fixture로 확인했다.
- [x] `Artifacts/QA/v27-world-resource-maximum-envelope.csv`는 header+3행, `1,891 bytes`, SHA-256 `FCDDF691D279D74ABDB78A8A1C2B4855797BD7B39C499A00788F7FEFE476E374`다. 두 번째 실행의 hash·length·mtime 변화는 `0`이다.
- [x] transaction fault report는 maximum tamper 두 경계를 추가해 `412 bytes`, SHA-256 `88EEB4E202650408D2640ECE19ADE51C97FF58FEE2510E5E02A179392AA3D806`이며 두 번째 실행 변화 `0`이다.
- [x] contributor registry 전체와 `ProductionEconomyDebugScenarios.RunAll()`이 current assembly에서 PASS했다. Batch A aggregate도 fresh manifest source digest로 재생성해 closure SHA-256 `841CD9EA0E4AF78A0F31EED4B41E4A5D91C943639FEB70D82372399F7C7C0A9F`, manifest TXT/CSV `65D0EF0B572E912ECA1C9EF7E585A0006F6BE766139AA85DC29319420EBE689C` / `0DD8CFF1ECC0345D453BDC5AEA8E46604A641A6D9E2232201958218D486866B5`를 얻었다. 연속 재실행 hash·length·mtime 변화는 `0`, Console Warning/Error `0/0`, 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 유지됐다.

이 checkpoint는 WorldResource output의 정의 상한과 actual exact-source publication 결속을 닫는다. crop은 여전히 GoldenHarvest/ecology state를 먼저 변경한 뒤 harvest와 seed lot을 두 독립 spawn으로 생성하므로 Batch B의 `전체 maximum envelope` 체크는 계속 OPEN이다. authored item kg·BOM·WU·EWU·가격·시설 수치·asset·scene 변경은 `0`이며 판정은 `밸런스 영향 없음 / 구조 교정 검증 완료`다.

## 120. 2026-08-29 Crop whole-vector maximum 및 외부 저장 원자성 폐쇄

상태: **crop maximum/actual publication PASS / Batch A `31/31` / Batch B `35/40` / Batch C input `8/39`, remaining `31` / 전체 보수 진행 `73–80%`**.

- [x] harvest와 returned seed는 completion 시점에 한 번 결정된 frozen two-line outcome을 공유하며, GoldenHarvest·ecology 결과도 같은 completion owner의 publication 성공 뒤에만 live authority로 승격된다.
- [x] `ProductionDomainOutputPublicationService`가 line별 item·quantity·unit gram과 aggregate mass를 current proof-bound maximum에 대조하고, FacilityOutputBuffer 공간 부족이면 같은 outcome으로 `WaitingForOutputSpace`를 유지한다.
- [x] current-format save/restore는 frozen outcome을 재굴림하지 않으며, publication acknowledgement 재시도는 기존 commit을 재사용해 물리 수량 증분 `0`을 보장한다.
- [x] sow receipt가 이미 exact cycle input을 소비한 뒤 남은 over-delivery seed는 `RemoveDestination`으로 삭제하지 않고 material destination을 release해 회수 가능한 물리 재고로 보존한다.
- [x] Unity JSON이 null nested output을 all-default object로 복원하는 경우 exact empty sentinel만 phase None의 canonical empty로 수용하며, 필드 하나라도 채워진 stale output은 거부한다.
- [x] `CharacterNarrativeRuntime`은 identity와 work-completion delivery cursor를 restore transaction에 stage/publish/rollback한다. 뒤 participant 실패가 외부 저장소만 앞서 적용하는 partial restore를 만들 수 없다.
- [x] `CropPhysicalTransactionFixture`, `V21CropGenomeDebugScenarios`, `V26FounderTraitAuditScenario`, `DungeonSaveSectionDebugScenarios`와 isolated paused PlayMode `CropPlotDebugScenarios`가 PASS했다. PlayMode report는 harvest `0→6`, `capacityWait=true`, `frozenRestore=true`, `replayDelta=0`, `valid=true`; SHA-256은 `E0F6787F7CDE53492BB1AFF7D62AADADAAE536832CAD82125865B8140B6A2785`다.
- [x] current assembly는 `Assembly-CSharp.dll 9,918,976 bytes @ 2026-08-29T06:51:15.5013996Z`, `Assembly-CSharp-Editor.dll 9,886,720 bytes @ 2026-08-29T06:42:24.6684648Z`; isolated Console Warning/Error `0/0`, 공식 GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 유지됐다.

이 checkpoint가 119의 crop OPEN 상태를 supersede한다. 닫힌 것은 Batch B의 `전체 maximum envelope` 한 행뿐이다. retarget transaction, support attach/detach 및 p95/>4-cycle gate, unified mutation fence, destructive live integration, active multi-facility retarget의 5개 행은 계속 OPEN이다. broad Batch B wrapper의 별도 wildlife save-version/player-fairness 실패도 green으로 숨기지 않는다. authored item kg·BOM·WU·EWU·가격·시설·asset·scene 수치 변경은 `0`이며 판정은 `밸런스 영향 없음 / 구조 교정 검증 완료`다.

## 121. 2026-08-30 생산 수동·자동 실행 권위 배타성 하위 경계

상태: **current-source compile·P15 실제 자연 AI·자동 executor·utility atomic PlayMode PASS / producer 전수 throughput/profile/capacity `@5` OPEN / Batch B `36/40` 유지**.

- [x] `AutomationAggregateState`를 다시 저장하거나 복제하지 않는 root-backed `IAutomationExecutionModeQuery`를 추가하고 composition root에서 같은 session 권위를 조회한다.
- [x] `ProductionWorkstationExecutionModeRules`를 preview·Begin·Execute가 공용으로 사용한다. Manual/PoweredAssist는 manual actor만, Automatic은 authored automatic lane의 명시적 executor만 허용하며 manual+automatic lane을 합산하지 않는다.
- [x] 자동 실행과 passive finishing은 null worker 우회가 아니라 `AutomaticExecutor`와 `PassiveProcessor`라는 서로 다른 immutable system authority를 사용한다. 기존 actor worker policy는 완화하지 않는다.
- [x] `SetMode(Automatic)`은 facility reservation, 실제 allocated worker ownership, production bill reservation 중 하나라도 있으면 `automatic-mode-manual-worker-active`로 거부한다. `IAllocatedWorkerOccupancyQuery`는 Facility·Shop·ConstructionSite의 실제 owner를 다형적으로 노출한다.
- [x] 일반 생산 작업대가 아닌 D01/D02 계열 legacy Cook은 Manual에서 기존 경로를 유지한다. 같은 시설이 Automatic이면 lane 누락보다 먼저 `production-manual-disabled-by-automatic-mode`를 반환하여 Cook/Craft legacy fallback이 수동 작업을 재개하지 못한다.
- [x] current Unity compile, isolated execution-mode gate, `ProductionWorkshopDebugScenarios`, `ProductionMaximumOutputFactorCatalogDebugScenarios`가 PASS했고 Console Warning/Error는 `0/0`이다. 공식 GameplayScene SHA-256은 불변이다.
- [x] P15 실제 fixture에서 Manual natural Cook progress, PoweredAssist natural progress와 배율, Automatic natural candidate/Begin/Execute 거부, automatic WU progress, allocate-before-Begin 전환 거부, utility-failure atomicity와 두 번째 실행 hash·length·mtime 무변경을 증명했다. 최종 matrix는 `5/5 PASS`, report SHA-256은 `69180B3CCEE72B23D8BAA9478721E63279746C007753DB9A243D7C8FF4B42A94`다.
- [ ] producer 전수 authored-reachable throughput envelope, complete frozen profile/DI, capacity source `@5`, atomic support attach/detach 재투영과 live `4.000/4.001` gate를 닫는다.

2026-08-30 producer-wide 처리량 순수 코어 하위 경계 증거: `ProductionAuthoredThroughputEnvelopeAuthority`를 추가해 `(recipe branch, exact feasible support assignment, execution mode)`를 하나의 후보로 유지한 상태에서만 gram/hour를 계산한다. 서로 다른 recipe/assignment의 최대 질량과 최대 속도를 교차 결합하지 않으며 Manual/Automatic lane은 합산하지 않고 `max`, PassiveBatch는 workstation과 해당 assignment 내부 processor capacity의 `min`을 사용한다. 리뷰 중 전체 compatible processor를 합산하던 초기 결함을 발견해 exact assignment로 교정했고, focused suite가 cross-product 금지, mode-exclusive max-not-sum, passive bottleneck, mutually-exclusive processor 비합산, special typed-gap 시 envelope 미발행, shuffle digest 동일, invalid provenance·overflow fail-loud를 PASS했다. 또한 실제 작업 실행 세 곳의 기존 `0.05–8 WU/s` clamp를 `WorkRateBoundsAuthority` 단일 권위와 source digest로 통합했으며 `WorkAmountDebugScenarios`와 Unity compile, Console Warning/Error `0/0`을 확인했다. 수치·SO·저장 형식 변경은 `0`이다. 이는 순수 계산 코어만 닫으며 actual branch/work-rate provider, 현재 census `85 complete / 7 typed gap`, 특수 contributor, 92개 producer의 32-seed profile, capacity source `@5`와 atomic publication이 남아 있으므로 위 parent 체크박스는 계속 OPEN이다.

2026-08-30 처리량 provider 기반 하위 경계 증거: fail-loud `ProductionRecipeWorkRateMaximumAuthority`는 required contributor manifest, source digest, nano fixed-point upper Ceil, `BigInteger` exact product, 최종 mWU/s 단일 Ceil과 공용 `WorkRateBoundsAuthority`를 결속한다. WorkOnly branch provider는 exact assignment의 output factor를 한 번만 적용하고 canonical physical output line별 기존 maximum-mass projection을 합산하며, PassiveBatch는 공용 ruined-branch 권위가 추출되기 전까지 typed missing으로 남긴다. 특수 producer registry는 stable contributor ID/version, capacity-owner/branch 완전성, immutable candidate/gap aggregate와 existing facility-subject injection을 제공하고 CertifiedSeed를 `AuthoredCycleAuthorityMissing`으로 보류한다. 세 focused suite는 rounding/clamp, missing contributor, lane mismatch, assignment 분리, Passive typed gap, CertifiedSeed gap, shuffle digest, duplicate/orphan/collision/applicability fail-loud를 모두 PASS했고 Unity compile·Console Warning/Error `0/0`이다. 수치·SO·save·DI 변경은 `0`; 실제 stat/room/evolution/automation contributors, Passive normal/ruined 공용 branch, Crop/Apparel/Combat contributors와 current census 전수 proof가 남아 있어 parent는 OPEN이다.

2026-08-30 live work-rate contributor 하위 경계 증거: 실제 `WorkAmountCalculator`의 첫 두 항을 execution-free 정의 권위로 연결했다. `WorkStatPolicyRegistry`는 현재 등록 정책의 definition maximum과 source digest를 다형적으로 조회하며 Gather `1.10`, Logging `1.08²`, AnimalCare `1.04⁴`, 나머지 명시적 neutral 경로를 제공한다. 등록된 신규 정책이 maximum source를 구현하지 않거나 unknown work type이면 fail-loud한다. `ProductionWorkStatPolicyMaximumContributor`는 recipe의 exact `WorkTypeId`와 이 권위를 결속하고 nano fixed-point로 보수적 상향한다. `ProductionCharacterPerformanceMaximumContributor`는 `CharacterPerformanceFormulaCatalog.RequireWork(workType, Speed)`와 기존 `ICharacterPerformanceDefinitionMaximumQuery`를 재사용해 base·functional capacity·proficiency·gameplay-effect maximum을 같은 formula provenance로 발행한다. formula mismatch·미작성 mapping은 typed gap이며 actor `ContextFactor`를 임의로 흡수하지 않는다. focused tests는 neutral/non-neutral 경계, 등록 순서 결정론, wrong/missing source, formula drift를 통과했고 Unity compile 및 기존 work-rate authority 회귀도 PASS, Console Warning/Error `0/0`이다. 수치·SO·save·DI 변경은 `0`이다. environment/context, evolution, powered assist, craftsmanship, automatic rate, PassiveBatch와 특수 producer가 남아 있어 상위 체크박스는 계속 OPEN이다.

2026-08-30 room/craftsmanship/automation work-rate 하위 경계 증거: live 방 환경 공식의 score weight `0.6/0.4`, score clamp `0..100`, speed `0.85 + score×0.003`, clamp `0.85..1.15`와 적용 work-type 집합을 `RoomWorkEnvironmentRateAuthority`로 단일화하고 live query와 definition maximum이 같이 사용한다. Restore-valid `CraftsmanshipQualityTier` 전수를 열거하는 query는 Mythic `1.60`을 보수 상한으로 선택하며 undefined enum의 과거 neutral fallback을 fail-loud로 교정했다. `AutomationWorkRateAuthority`는 live condition/assist 공식을 공유하고, PoweredAssist contributor는 facility ability의 authored mode/multiplier를, 실제 automatic query는 mode-exclusive lane과 `automaticWorkPerSecond × conditionMaximum`을 결속한다. Manual factor와 Automatic rate를 섞지 않으며 manual-only lane은 상위 authority에서 automatic `0`이다. 공용 `ProductionFacilityDefinitionCatalog`가 canonical facility identity를 한 번만 동결한다. focused suite는 affected/non-affected room work, Mythic/invalid craftsmanship, assisted/manual ability, automatic lane mismatch와 `1.25 WU/s → 1250 mWU/s` 통합을 PASS했고 최종 Console Warning/Error `0/0`이다. Broad Industrial suite는 기존 별도 fuel-outbox ratchet에서 중단되어 증거에서 제외했다. authored 수치·SO·save·DI 변경은 `0`; actor context, facility evolution, full manifest/DI, PassiveBatch·special producer·전수 profile가 남아 상위 체크박스는 OPEN이다.

2026-08-30 facility-evolution work-rate 하위 경계 증거: `FacilityEvolutionWorkSpeedDefinitionMaximumQuery`가 exact facility role/work type, canonical `IEvolutionModuleRegistry.All`, restore-valid active node 상한 `256`, 동일 module 반복 가능성과 benefit/burden 조합을 통해 `service.speed` 상한을 계산하고 live 최종 clamp `0.1..8`을 보존한다. `ProductionFacilityEvolutionWorkRateMaximumContributor`는 exact facility definition과 recipe work type을 이 snapshot에 결속하고 facility-catalog/evolution source digest를 함께 발행한다. 현재 service+Operate는 `8`, nonservice 또는 다른 work type은 `1`; missing facility, unknown work type, duplicate/null/non-finite module은 typed gap 또는 fail-loud다. Unity current-source compile 및 query/contributor focused suites PASS, Console Warning/Error `0/0`이다. 이 변경은 authored kg·BOM·WU·EWU·가격·SO·save 값을 바꾸지 않으며, actor context provenance와 complete manifest/DI, PassiveBatch, special producer, `92/92` profile, capacity `@5` 및 atomic support publication이 남아 parent Batch B는 계속 OPEN이다.

증거는 `Artifacts/QA/v27-production-execution-mode-exclusion-focused.txt`다. broad `ProductionEconomyDebugScenarios`는 별도 destructive-drain topology ratchet에서 중단됐으므로 PASS로 기록하지 않는다. 이번 하위 경계는 authored kg·BOM·WU·EWU·가격·시설·asset·save schema를 변경하지 않았다. 상위 체크포인트는 Batch A `31/31`, Batch B `36/40`, Batch C `8/39`, parent remaining `35`로 유지한다.

## 122. 2026-08-30 캐릭터 작업 문맥·DI 구성 및 특수 producer 소유권 census

상태: **character-context factor provenance PASS / production DI actual GameplayScene container PASS / recipe-only actual projector `85/85` PASS / special owner coverage `7 facilities · 904/904 branches` PASS / Apparel `549/549`·Combat `295/295`·Crop `48/48`·CertifiedSeed `12/12` actual cycles PASS / frozen profile·atomic publication OPEN / Batch B `36/40` 유지**.

- [x] 캐릭터 작업속도 문맥의 9개 factor(research shared, fatigue, discontent, transient skill, deprivation, substance, character environment, equipment burden, content delay)를 live 공식과 같은 실행 없는 definition-maximum 권위로 연결했다. 모든 authored/runtime aggregate는 finite 검증, stable 순서와 source digest를 가지며 transient skill은 `0.1..2.5`, substance는 `0.45..1.75`, 최종 work rate는 공용 `0.05..8` clamp를 유지한다.
- [x] 장비 부담 공식의 오래된 `20kg` literal은 이미 확정된 `CharacterCarryTuning.NominalBaseCapacityKilograms = 25kg` 단일 권위를 조회하도록 교정했다. 이는 새 authored kg 변경이 아니라 동일 nominal 권위의 중복 상수 제거다.
- [x] 실제 WorkStat와 RoomEnvironment singleton이 maximum-query interface도 함께 노출되고, facility catalog·craftsmanship·assist·automatic·character context·facility evolution 및 7개 work-rate contributor와 canonical manifest가 production composition root에 등록됐다. 실제 GameplayScene `DungeonRuntimeLifetimeScope.Container`에서 exact 7개 contributor, 두 singleton identity와 4개 special owner를 resolve해 live graph까지 증명했다.
- [x] 첫 live container 실행은 `ProductionFacilityDefinitionCatalog`가 negative-ID runtime archetype을 authored facility로 해석해 fail-loud했다. current asset의 `377` authored definition과 `42` runtime-only exterior/world archetype을 전수 확인하고 catalog schema를 `@2`로 올렸다. non-workstation runtime archetype만 제외하며, 유효 workstation ability를 가진 identityless 정의는 계속 예외로 거부한다.
- [x] focused catalog 경계 test와 actual PlayMode report가 PASS했다. 확장된 `Artifacts/QA/v27-production-work-rate-composition-playmode.txt`는 `2,240 bytes`, SHA-256 `683111B94D583B6649DDA71FC0EAB1E82FB26DA926518DAE266BBBEB96DB8C8C`; 2회차 hash·length·mtime 변화 `0`, Console Warning/Error `0/0`, GameplayScene SHA-256 불변이다.
- [x] `ProductionSpecialThroughputContributorRegistry`는 applicable capacity의 owner가 없으면 branch별 `SpecialThroughputProviderUnregistered`를 발행하고, 반환 직전에 `applicable authored branch key 집합 == candidate ∪ gap key 집합`을 fail-loud 검증한다.
- [x] capacity ID당 정확히 한 owner인 `CertifiedSeedSpecialThroughputGapContributor`, `CropHarvestSpecialThroughputGapContributor`, `ApparelSpecialThroughputGapContributor`, `CombatCraftSpecialThroughputGapContributor`를 등록했다. crop 4시설은 같은 capacity ID를 공유하므로 owner는 시설별 4개가 아니라 capacity별 1개다.
- [x] current-source census는 `146` workstation, `92` producer를 `85 recipe-only + 7 special`로 분류한다. special 7개 중 recipe도 가진 overlap은 `4`이며, 이를 별도 수치로 보존해 중복 집계를 방지한다.
- [x] special branch는 총 `904`: `AuthoredCycleAuthorityMissing 60`(crop `48` + certified seed `12`), `ExecutionAuthorityUnsupported 844`(combat `295` + apparel `549`), `SpecialThroughputProviderUnregistered 0`, candidate `0`이다. 따라서 소유권 누락은 닫혔지만 actual cycle 처리량은 아직 완성되지 않았다.
- [x] Unity current-source compile, `ProductionSpecialThroughputContributorRegistryDebugScenarios.Validate()`와 `ProductionFacilityOutputCensusDebugScenarios.RunAll()`이 PASS했고 최종 Console Warning/Error는 `0/0`이다. 첫 overlap 기대치 `0`은 current source가 `4`임을 fail-loud로 드러냈고, 이 실패 실행은 증거에 포함하지 않았다.
- [x] report는 `Artifacts/QA/v27-production-facility-output-census.txt`, SHA-256 `12CAB06AE8DA9C5FE50C249028F52EB2DC7AD4E1DD7E9C3C8F0595C49E995133`, `670 bytes`; CSV는 SHA-256 `272652EC1F158856F93DFEC4F176232EEB58B1F2B2DA92CFD63E571AE55C080F`, `175,917 bytes`다. 두 번째 생성에서 두 파일의 hash·length·UTC write ticks가 모두 동일했다.
- [x] 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다. ScriptableObject, BOM, WU, EWU, 가격, save schema 변경은 `0`이다.
- [x] 모든 `85` recipe-only key의 실제 branch/work-rate projector를 실행했다. production content와 actual GameplayScene container의 work-rate `7/7`, maximum-mass capability `8/8`, 공용 `7,500,000µs/game-hour` 권위로 `271` recipes와 `278` exact support assignments를 투영했다. 최초 결과의 WorkOnly `266` candidate와 PassiveBatch `12` typed gap은 공용 normal/ruined 포트폴리오 연결 후 최종 `278` candidate, gap `0`, complete envelope `85`, withheld facility key `0`으로 폐쇄됐다. 후보 source digest 중복은 `0`이다.
- [x] 첫 actual 전수 실행이 numeric building ID를 거부하던 facility-evolution 결함을 발견했다. 공용 `BuildingDefinitionIdentity`가 명시 stable ID 또는 정상 numeric authority를 동일 canonical key로 투영하도록 production/facility-evolution을 통합했고 focused 및 actual PlayMode 재실행이 PASS했다. production·crop·automation의 중복 `7.5초` 상수도 assembly-low `GameSimulationTimeRules`로 통합했다.
- [x] `ProductionPassiveBatchOutputPortfolioAuthority`가 exact assignment별 normal productive branch와 ruined terminal-fault branch를 한 번만 계산한다. throughput은 deterministic normal만 발행하고, capacity source `@5`는 normal/ruined 중 큰 물리 질량을 사용한다. ruined 보존식과 mass revision 일치가 fail-loud다.
- [x] 최종 deterministic projection CSV `Artifacts/QA/v27-production-recipe-throughput-playmode.csv`는 `278`행, `52,489 bytes`, SHA-256 `0DD6C9F49A3FB2B44BF27F4198F724E21B0B8D07211D83D87B6CD799E1629225`; report는 `2,044 bytes`, SHA-256 `E9955684EA9588EC930736D06F61EBD90106741A2556402F4E968775B508092E`다. 두 번째 실행은 report와 CSV 모두 hash·length·UTC write time 변화 `0`; Console Warning/Error `0/0`, GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다.
- [x] certified seed·crop·apparel·combat의 실제 cycle 권위를 구현하여 `904` typed gap을 candidate로 전환한다. actual GameplayScene은 `904 candidates / 0 gaps`다.
  - [x] Apparel `549`개 branch는 실제 material/size/modification work 계산, shared branch mass, 공용 7-factor work-rate, mode-exclusive lane과 game-hour 권위로 candidate 전환했다. primary `56`, 실제 nonempty rejected-backlog recovery `493`, Apparel-local gap `0`이다. recovery는 이미 존재하는 rejected backlog의 독립 peak phase이므로 `execution:backlog-recovery`와 recovery-only `max(0.1 WU, primary WU * 0.20)`를 사용하며, 원료부터의 지속 처리량과 혼동하지 않는다.
  - [x] Combat `295`개 branch는 primary `63`(equipment `61` + ammunition `2`)과 실제 nonempty rejected-backlog recovery `232`를 모두 candidate로 전환했다. ammunition `4 WU`와 recovery `max(0.1 WU, primary WU * 0.25)`는 live runtime과 공용 권위이며, 실제 legacy craft가 actor lane만 사용하므로 mode-exclusive forge의 automatic lane은 검증만 하고 처리량 후보로 선택하지 않는다.
  - [x] Crop `48`개 branch는 4개 시설과 12개 crop의 실제 sow → calendar-integrated growth → harvest 직렬 cycle로 candidate 전환했다. 야외는 현재 180초 하루 중 night 65초에 `0.55`, 나머지 115초에 비야간 배율을 적용하고 실내는 야간 감속을 적용하지 않는다.
  - [x] CertifiedSeed `12`개 branch는 current save V6의 persisted monotonic `lastProcessedOperatingDay` gate와 같은 권위를 사용한다. strictly newer positive day만 처리 기회를 열고 duplicate/stale day는 `0`; 각 branch throughput은 24 operating hours당 physical output 1개다. 과거 세이브 migration/fallback은 추가하지 않았다.
- [ ] complete `92/92` frozen profile, capacity source `@5`, atomic support publication과 live `4.000/4.001`을 통과한다.

2026-08-30 Apparel actual-cycle 추가 증거: generic operation work-rate subject/query, full immutable special facility subject, shared branch-mass query와 lane-aware `ProductionWorkCycleThroughputAuthority`를 추출해 recipe와 special producer가 동일 권위를 사용한다. actual GameplayScene census는 `904 branches / 549 candidates / 355 gaps`, 남은 gap은 authored-cycle `60`과 execution-unsupported `295`, unregistered `0`이다. PlayMode는 `17/17 PASS`; report는 `2,167 bytes`, SHA-256 `C9F9BAFDF2D675E1DF9CAB95FCC72B123E4D46FDB95C7DC20248D03E43AEBFEB`, CSV는 `52,489 bytes`, SHA-256 `1AF2DD226628285893389AC9C4A3B6449B101B4D652D0664B677CD5470CD5A08`이다. 두 번째 실행에서 두 파일의 hash·length·UTC write time 변화는 `0`, Console Warning/Error `0/0`, GameplayScene SHA-256은 불변이다.

2026-08-30 Combat actual-cycle 추가 증거: branch identity, ammunition `4 WU`, rejected-backlog `25%` 규칙을 live runtime과 감사가 공유하고, equipment primary는 합법 material 전수의 실제 계산 WU를 평가한다. `CaptureManualOnly`은 S08의 authored automatic lane을 실행 가능 Combat 경로로 오인하지 않는다. actual GameplayScene census는 `904 branches / 844 candidates / 60 gaps`, remaining은 Crop+CertifiedSeed authored-cycle뿐이다. PlayMode `17/17 PASS`; report는 `2,171 bytes`, SHA-256 `59124B4C325ACAF6466C41E2C8236661A5FF0505C81F1FA96FA85BA7148C86DA`, CSV는 `52,489 bytes`, SHA-256 `DE79E9ECC5DD4B0A5988B69CE02CFACF5CA1D94370BD3D2AB33F64C832DF678F`이다. 2회차 hash·length·mtime 변화 `0`, Console Warning/Error `0/0`, GameplayScene SHA-256 불변이다.

2026-08-30 Crop actual-cycle 추가 증거: shared growth multiplier와 branch identity를 runtime/capacity/throughput이 함께 사용하고, sow·calendar-integrated growth·harvest를 하나의 직렬 cycle로 계산한다. focused `48 candidates / 0 gaps`, actual GameplayScene `904 branches / 892 candidates / 12 gaps`이며 남은 gap은 CertifiedSeed뿐이다. PlayMode `17/17 PASS`; report는 `2,176 bytes`, SHA-256 `02DB67909E75552EBF47C09360CDA88612DA33586FCA936756C914AF569CBBB9`, recipe CSV는 SHA-256 `DE79E9ECC5DD4B0A5988B69CE02CFACF5CA1D94370BD3D2AB33F64C832DF678F`로 불변이다. 2회차 hash·length·mtime 변화 `0`, Console Warning/Error `0/0`, GameplayScene SHA-256 불변이다.

2026-08-30 CertifiedSeed actual-cycle 추가 증거: command와 adapter는 operating-day 번호를 전달하고 runtime은 persisted monotonic gate를 주문 처리 전에 커밋한다. save V6 round-trip, duplicate/stale/new day focused 검증과 physical transaction suite가 PASS했다. actual GameplayScene special census는 `904 candidates / 0 gaps`, PlayMode `17/17 PASS`; report는 `2,157 bytes`, SHA-256 `EE46049236A606B83B1CF8CE4CDE703A26113E3D18ACB13EACA3F1F518F9A01D`, recipe CSV는 `52,489 bytes`, SHA-256 `DE79E9ECC5DD4B0A5988B69CE02CFACF5CA1D94370BD3D2AB33F64C832DF678F`다. 2회차 hash·length·mtime 변화 `0`, Console Warning/Error `0/0`, GameplayScene SHA-256 불변이다.

이 checkpoint는 Apparel·Combat·Crop·CertifiedSeed actual-cycle의 실제 source/compile/PlayMode 경계를 닫았지만 생산량 밸런스나 Batch B parent를 완료하지 않는다. complete `92/92` frozen 32-seed profile과 capacity `@5` atomic publication이 남아 있다. 상위 체크포인트는 Batch A `31/31`, Batch B `36/40`, Batch C `8/39`, parent remaining `35`다.

2026-08-30 P17 natural-clearance `@2` 재생성 증거: sanitized PlayMode lease로 seed `157181..157212`를 공통 checkpoint에서 실제 `AIBrain -> AIHaul` 경로로 재실행했다. CSV는 `building:1089/workstation:feedbench`의 PASS `32`행, distinct seed `32`, schema `v27-production-output-clearance-natural-seed@2`, 빈 `batchCommitId` `0`이다. report SHA-256은 `AEE55B4E189E23E6478472091274F41DF796508F8E346E040AE43FF57DEC1783`, CSV SHA-256은 `B22187E7AD86B8F438094EAD7ABA119C587AD791D8DAD1D4698D15A9E03096E7`, Console Warning/Error `0/0`, GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다. 이는 실패한 구 `@1` 산출물 교체와 P17 하위 경계만 닫는다. complete frozen profile의 엄격 분모는 `92 × 32 = 2,944`이며 현재 인증 coverage는 `32/2,944`; 따라서 `92/92` provider, capacity `@5`, atomic publication 및 Batch B parent는 계속 OPEN이다.

2026-08-30 producer-wide authored envelope 증거: actual GameplayScene container에서 `85` generic recipe key와 `7` special key를 공용 envelope 권위에 결합했다. generic 경로는 `271 recipes / 278 exact support candidates`, special 경로는 실제 `904 candidates / 0 gaps`를 사용하며, 4개 recipe/special overlap의 tag-matched recipe를 generic 실행 가능 경로로 중복 투영하지 않는다. 결과는 `92/92 complete envelopes / 0 gaps`, catalog digest `2d7f7cb577e6e76e03495d232a9f547c4466cfc1fa8cce91cb3e5436e1a6dd6f`이다. 재사용 가능한 current-source scope query도 같은 정상 product-boot verifier에서 별도 검증했다. current report SHA-256은 `902ADF3229683300DCCB2CCA65885F6F2FEE982B4DBC51CE69C51690F88F8053`, 92행 envelope CSV SHA-256은 `93B58E869B745542A8CB9FBE45A454B8D3BA79AB96710714C1A299BC38EC446E`; 2회차 report/envelope/recipe artifact의 hash·length·mtime 변화 `0`, Console Warning/Error `0/0`, GameplayScene SHA 불변이다. 이로써 throughput envelope 선행조건만 닫는다. natural clearance는 여전히 `1/92 keys`, `32/2,944 observations`이므로 frozen provider와 Batch B parent는 OPEN이다.

2026-08-30 frozen profile cardinality 방어 증거: `ProductionOutputClearanceProfileAggregator.BuildFrozen`은 명시적 unique seed cohort, exact profile count, exact `profileCount × seedCount` 총행과 각 `(definitionId, workstationTag, seed)`당 정확히 한 batch를 요구한다. 같은 key/seed의 다른 `batchCommitId`, 승인되지 않은 추가 seed 행, 누락 profile/envelope는 fail-loud다. Unity compile과 focused catalog suite PASS, Console Warning/Error `0/0`이다. 이는 불완전·중복 산출물의 freeze를 막는 경계이며 누락된 `2,912` natural observation을 대신하지 않는다.

2026-08-30 capability 기반 measurement plan 증거: `ProductionOutputClearanceMeasurementPlanRegistry`는 facility ID별 분기 없이 recipe/Crop/Apparel/Combat/CertifiedSeed source capability를 측정 실행 capability에 결속한다. 공용 branch-mass 권위로 maximum single-completion physical footprint를 선택하고 mass-descending + ordinal tie-break로 결정론을 유지한다. unique output capability는 immutable plan에 보존되며 unregistered/unsupported payload는 typed gap으로 실패하고 generic item fallback은 금지된다. Unity compile과 focused registry suite PASS, Console Warning/Error `0/0`이다. 이 단계는 measurement plan foundation만 닫으며 runtime execution과 `2,912` remaining observation은 OPEN이다.

2026-08-30 current-source `92/92` clearance measurement plan 증거: recipe branch snapshot이 maximum-mass projector가 실제 선택한 output capability ID를 immutable하게 보존하도록 확장했다. WorkOnly은 exact line projection, PassiveBatch는 productive normal portfolio projection을 사용하며 item/recipe 이름 추측과 generic-item fallback은 없다. `ProductionOutputClearanceMeasurementScopeAuthority`는 공용 92-producer scope, 278 feasible recipe assignments와 special facility의 current capacity contribution을 결합하고 special contributor ID/source digest를 census와 exact 대조한다. actual GameplayScene product boot 결과는 `92 contexts / 92 plans / 0 gaps`, recipe branch `278`, special branch `904`, candidate `1,182`이며 focused 회귀와 Console Warning/Error `0/0`을 통과했다. 92행/92키 manifest SHA-256은 `7D2F060E0C42C955A97A9E25FD18F3A4969D6608C07D75F575210F698C9CFEF2`, 20/20 통합 report SHA-256은 `C10F418D4642F7E02670FE35F328BB8D36C25E3935F4FE33C44FD6A57A9D5451`이다. 두 번째 PlayMode 실행에서 report/manifest/envelope/recipe CSV의 hash·length·UTC write ticks가 모두 불변이고 공식 GameplayScene SHA-256도 유지됐다. 이는 92-key 측정 계획과 manifest 하위 경계만 닫는다. 자연 observation은 여전히 `1/92` keys, `32/2,944` rows이므로 남은 `2,912`개 계측, frozen provider, capacity `@5`, support/profile atomic publication 전에는 상위 체크박스와 Batch B parent를 닫지 않는다.

2026-08-30 exact `92×32` 실행 포트폴리오 및 natural-observation acceptance gate 증거: `ProductionOutputClearanceMeasurementPortfolio`가 current-source `92` measurement plans와 고정 seed `157181..157212`를 정확히 교차하여 unique fixture/observation ID `2,944`개를 발행한다. `ProductionOutputClearanceNaturalObservationPortfolio` schema `@3`는 각 행을 frozen fixture, runtime facility, resolved output vector, positive committed batch mass, unique batch commit, topology/owner/action 증거, exact micro→milli-hour timing, clean telemetry, scheduler/delivery completion, RNG provenance와 전체 record digest에 결속한다. 누락 행과 중복 commit은 fail-loud다. actual GameplayScene product boot는 `22/22 PASS`, portfolio `2,944`, structural gate accepted `2,944`, missing/duplicate rejection PASS, Console Warning/Error `0/0`이다. portfolio CSV는 `1,653,743 bytes`, SHA-256 `7470240809D8CFB2E308BB0AD0206DE66D946EBF8E896130D497721640347565`; integrated report는 `3,048 bytes`, SHA-256 `DA90540A8C93BC4FC4AA21F61855ABC4B3B29B791974F5468445E34BDBD9E953`이며 반복 실행에서 hash와 artifact write 상태가 유지됐다. 공식 GameplayScene SHA-256도 불변이다. 단, structural acceptance fixture는 자연 계측으로 세지 않는다. live certified coverage는 여전히 `32/2,944`, remaining `2,912`이며 generated frozen provider, capacity projection, atomic publication과 Batch B parent는 OPEN이다. 부모 체크포인트는 `A 31/31, B 36/40, C 8/39`, parent remaining `35`로 유지한다.

2026-08-30 Crop/Apparel accepted-output natural-haul routing 증거: Crop harvest의 physical reserve/publication/admission은 기존 FacilityOutputBuffer atomic batch를 유지하되 acknowledgement disposition을 `ReleaseLooseOrDestination`로 교정했다. harvest와 returned SeedLot 두 line은 동일 provenance를 유지한 unassigned Loose stack으로 원자 해제되어 일반 AIHaul 후보가 된다. active/frozen/committed owner validation도 동일 disposition을 요구한다. 실제 Crop PlayMode는 capacity-full wait, frozen capture/restore, exact retry, 두 line Loose release와 replay delta `0`을 통과했고 report SHA-256은 `73767D9223B5E177D215116EB92334345A2C74A178FF5EDFAC10BAAB849D6535`, Console Warning/Error `0/0`이다. accepted Apparel은 fresh batch에 `TryAcknowledgeAndReleasePublishedBatch`, pending/restore candidate에 `TryAcknowledgeAndReleaseRestoreCandidate`를 사용한다. one-shot acknowledgement mutation failure 회귀에서 첫 실행은 committed FacilityOutputBuffer와 input receipt를 보존하고, retry는 같은 unique stack/instance/component/mass를 Loose로 원자 해제하며 재소비·복제 `0`을 증명했다. mark-for-sale 경로는 별도 market route로 유지했다. Apparel focused suite와 Unity compile은 PASS, Console Warning/Error `0/0`; integrated producer report SHA-256은 기존 `DA90540A8C93BC4FC4AA21F61855ABC4B3B29B791974F5468445E34BDBD9E953`로 byte-identical이고 GameplayScene SHA-256도 불변이다. 이 증거는 Crop 및 accepted-Apparel의 publication→natural-haul P0 하위 경계만 닫는다. executable descriptor/handler, Crop maximum-batch storage topology, live `2,912` observation과 frozen provider는 OPEN이며 부모 체크포인트는 `A 31/31, B 36/40, C 8/39`, remaining `35`로 유지한다.

2026-08-30 exact winner-mass gate 및 recipe executable descriptor 증거: 자연 관측 schema를 `production-output-clearance-natural-observation@4`로 올리고 실제 batch mass가 선택된 winner의 `MaximumSingleCompletionMassGrams`와 정확히 같을 때만 생성되도록 교정했다. `winner-1g` synthetic observation은 portfolio 진입 전에 fail-loud하므로 작은 정상 배치가 최대 branch 관측으로 위장할 수 없다. `ProductionOutputClearanceRecipeExecutableDescriptorContributor`는 recipe-backed plan `85`개 전부를 exact current-source facility/recipe, feasible support assignment, selected throughput branch, process/work/research/fluid/temperature 권위, physical input, maximum output line/capability/mass에 재결속한다. quarry 같은 합법적인 zero-input Source recipe는 허용하지만 outputless/duplicate physical line과 branch drift는 거부한다. actual GameplayScene product boot 결과는 `23/23 PASS`, descriptor `85`, special typed gap `7`, reduced-winner-mass rejection `True`, Console Warning/Error `0/0`이다. descriptor digest는 `74b4b906f1753d6b269d1b8ecaa6ba952ef9cc6ddd9412a6100377a39f969666`, observation gate digest는 `31cb7601dc889218094825ba90fccbfe5b3216d51f41fe622ef64bbf8bc5bc74`, report SHA-256은 `108602D86ECB2549CA7B744B62B2D9505E8DAF78EB57A560040C37DD5B0BBAE1`이다. 두 번째 실행에서 report와 92-row plan, 2,944-row portfolio의 hash·length·mtime 변화는 모두 `0`; GameplayScene SHA-256도 불변이다. 이로써 exact-mass acceptance와 generic recipe descriptor 하위 경계만 닫는다. 4개 typed special contributor가 담당할 7시설, executable handler, Crop maximum-batch storage topology, live `2,912` observation, frozen provider 및 capacity/atomic publication은 계속 OPEN이고 부모 체크포인트는 `A 31/31, B 36/40, C 8/39`, remaining `35`로 유지한다.

2026-08-30 Combat/Apparel/Certified Seed executable descriptor 증거: Combat은 실제 선택 material과 전체 physical input vector, Apparel은 apparel ID·선택 material item·size·modification·exact quantity를 current branch에 동결했다. Certified Seed는 공용 `CertifiedSeedPhysicalTransformAuthority`를 런타임과 descriptor가 함께 사용하며, authored base-genome/generation 0/pathogen 0의 exact input `SeedLotState`와 변환 후 output component를 canonical fingerprint로 동결한다. seed 1개·certification kit 1개·출력 1개와 capability/codec, winner `50g`을 모두 current capacity branch에 결속했다. 또한 `CertifiedSeedPlanExecutionReceipt`/`ICertifiedSeedExecutionReceiptQuery`를 추가해 runner가 private ID 포맷을 재구현하지 않고 action→order→destination→input operation→output owner/batch를 추적할 수 있게 했다. focused receipt 테스트와 Unity compile PASS. actual GameplayScene product boot는 `23/23 PASS`, descriptor `88/92`, Crop typed gap `4`, recipe `85`, Combat/Apparel/Certified Seed 각각 `1`, Console Warning/Error `0/0`; report SHA-256 `3D25FA67BBA45431343489F69BCAEF17190495C8137BA59977EABC72A40C99F2`, plan `5D5AB93120FF92080C69C7A7AD8169742A39C3DAA8D002B90B02DD433AF93099`, portfolio `911EA0D00DEC3410DB8E031B0AD6935281B849C5AEB23623D4C0A2A0D97D40F4`이며 반복 실행 hash·length·mtime 변화 `0`, GameplayScene SHA 불변이다. Certified descriptor 하위 경계만 닫았고 실제 single-boot handler는 OPEN이다. Crop 4개는 현재 `3.43–4.12t` execution-free upper bound가 단일 runtime state에서 도달 가능하다는 witness가 없고 현재 `12.5–27kg` 저장 topology로 전량 clearance할 수 없어 단순 contributor 등록을 금지한다. live natural coverage는 `32/2,944`, parent checkpoint는 `A 31/31, B 36/40, C 8/39`, remaining `35`다.

## 123. 2026-08-30 Crop 실행 영수증·저장 수명주기 폐쇄

상태: **Crop exact execution receipt·terminal save/restore·physical batch join PASS / natural `AIBrain → AIHaul` measurement handler OPEN / Batch B `36/40` 유지**.

- [x] Crop Plot current-format 저장을 V10으로 올리고 `crop-cycle-execution-receipt@2` 권위를 추가했다. 전역 유일 explicit action ID가 exact sow operation, ordered input lot·quantity·gram·seed state·request digest와 terminal harvest operation/batch를 한 owner에 결속한다.
- [x] 성공 terminal은 harvest와 returned seed의 exact two-line output, item/quantity/unit gram/instance ID, capability descriptor·fingerprint, outcome/planned fingerprint와 aggregate mass를 보존한다. restore에서는 capability fingerprint와 receipt digest를 재계산하며 duplicate line, mass drift, scalar garbage와 비정규 ID를 fail-loud한다.
- [x] explicit terminal receipt는 measurement consumer가 동일 action을 acknowledgement하기 전까지 다음 자동 crop cycle이 덮어쓰지 못한다. save/restore 뒤에도 query 가능하고 premature/duplicate acknowledgement와 plot 간 correlation 중복은 거부한다.
- [x] crop death, plot destruction과 destruction-before-sow는 action을 조용히 삭제하지 않고 각각 typed terminal failure를 발행한다. Unity `JsonUtility`가 null `SeedLotState`를 all-default 객체로 복원하는 경우에는 정확히 모든 필드가 기본값인 semantic-empty만 absent로 인정한다.
- [x] `CropPhysicalTransactionFixture.Run()`은 Begin/Complete/Fail/FailBeforeSow, immutable witness, JSON digest round-trip, capability·mass tamper와 strict-empty rejection을 PASS했다.
- [x] 실제 GameplayScene `CropPlotDebugScenarios`는 `valid=true`, `executionReceipt=true`, `physicalBatchJoin=true`, `terminalRestore=true`, `ackRetention=true`다. acknowledgement 전 실제 two-line committed batch와 receipt를 exact join했고 acknowledgement 뒤 owner가 제거되며 duplicate ack가 거부됐다. report는 `975 bytes`, SHA-256 `C165D46CE2458BBEF13D39E8C648606942CF5A0D1D7B7369B78F180D6E74FD5E`다.
- [x] 전체 product boot는 `23/23 PASS`, descriptor `92/92`, typed gap `0`, portfolio `2,944`를 유지한다. report/plan/portfolio SHA-256은 각각 `852F4BBED72EB11C617A7B34DFB6AE88EA95B6148DD56374EEF7855254EB3FB9`, `9D82A23CBCF970748198BB44EF735EE428F10E9EF1A97E95C275123B344CA6F8`, `D1EF74504D3A82C8ADBC91C02D55B9DD8EBD973846358EA052C39C7A82620B0E`이며 두 번째 실행에서 hash·length·mtime이 모두 동일했다.
- [x] 최종 Console Warning/Error `0/0`, GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 불변을 확인했다.
- [ ] released two-line output과 exact receipt를 자연 `AIBrain → AIHaul` measurement handler에 연결하고, observation acceptance 성공 뒤에만 receipt acknowledgement를 수행한다.

이번 체크포인트는 실제 코드·저장 계약·focused/PlayMode 회귀가 추가된 구현 진척이다. 다만 natural measurement handler와 `92 × 32 = 2,944` 실측 생성, frozen provider가 남아 있으므로 certified live coverage는 `32/2,944`이며 상위 체크포인트는 `A 31/31, B 36/40, C 8/39`, parent remaining `35`로 유지한다. 상위 숫자 유지가 코드 진척 없음이라는 뜻은 아니다.

## 124. 2026-08-30 Unity 재시작 복구·Batch D 별도 무변경 실행·relocation 저장 토폴로지

상태: **Batch D schema-v2 별도 2회차 byte/no-write PASS / relocation completion·save-topology focused PASS / 상위 Batch B·D OPEN**.

- [x] 재시작된 Unity Editor가 `IsPlaying=false`, `IsCompiling=false`, `IsUpdating=false`인 상태로 MCP에 다시 연결됨을 확인했다.
- [x] `V27PhysicalMassFamilyProposalDebugScenarios.RunFromMenu()`를 이전 실행과 분리된 새 Unity command로 다시 실행했다.
- [x] CSV는 `421,303 bytes`, SHA-256 `BDB3E502DAA9EA9C4B110141B7EA44B82220B29E089CB83C4889602D08744066`; report는 `932 bytes`, SHA-256 `466475480671ED443C404F65A79F2D397E81772BDA7FBB9FFED20A20511EF788`을 유지했다. 두 파일의 UTC last-write time도 바뀌지 않아 기존 byte와 같을 때 writer가 파일을 건드리지 않는 별도 실행 no-op을 증명했다.
- [x] fresh CSV는 `v27.mass.family-proposal.2`, `414`행, `19`열이다. report는 `ready=7`, `unchanged=353`, `critical=54`, `warning=0`, `assetMutations=0`, `deterministicRecapture=PASS`다.
- [x] `FacilityRelocationCompletionFenceFixture.Run()`을 current loaded assemblies에서 실행하여 relocation completion fence와 canonical save-topology positive/negative matrix를 통과했다.
- [x] 최종 Unity Console Warning/Error는 `0/0`; 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다.
- [ ] 이 focused validator는 live active authority retarget이 아니다. relocation/evolution/synthesis의 persistent survivor ID, 6 participant 가역 retarget, set-level synthesis rollback, 공통 start/reserve freeze, journal save/restore와 실제 PlayMode가 통과하기 전에는 Batch B의 세 parent 행을 닫지 않는다.
- [ ] Batch D의 54 Critical은 `missing-unit-semantic 51`과 singleton heavy-equipment haul-band 경계 `3`이다. anomaly review, warehouse/buffer gram authority, EWU/price 재생성 및 승인된 값의 ScriptableObject 적용 전에는 Batch D를 닫지 않는다.

현재 고정 진행률은 Batch A `31/31`, Batch B `36/40`, Batch C `8/39`, 열거된 A-C 부분집합 `75/110`; 전체 A-H 완전 종료는 Batch A만인 `1/8`이다.

## 125. 2026-08-30 질량 설명 가능성 계약과 proposal dependency gate

상태: **입력=출력 강제 폐기 / typed external-input·sink·loss 설명 계약 반영 / recipe audit v2·family proposal v3 PASS / authored kg 적용 0**.

- [x] 최상위 질량 판정을 `physical input + declared external input = physical output + byproduct + terminal sink + declared abstract loss`로 교정했다. 현실 질량 불변을 강제하지 않고, 소유권·수량 exactness와 EWU SCC 무차익은 별도 strict invariant로 유지한다.
- [x] 증기·분진·배기·미량 폐기·일회용 포장은 gameplay 의미가 없으면 물리 item을 강제하지 않는다. exact gram·reason·operation에 결속된 추상 손실로 허용한다.
- [x] 포장 tare를 `ReusableContainerReturn / DisposableWasteByproduct / DestroyedDuringUse / TransferredWithOutput / BulkInfrastructureNotInUnit`로 분리하고 reusable로 작성된 경우에만 빈 용기 반환을 강제한다.
- [x] recipe inventory schema를 `v27.mass.recipe-inventory.2`로 올렸다. 기존 `mass-creation-critical`은 `external-input-authority-missing`, `disposition-contract-missing`은 `mass-balance-explanation-missing`으로 교정했다. 현재 `355` recipe는 Source `23`, Sink `4`, reviewed exact `42`, unit semantic missing `47`, external-input authority missing `80`, explanation missing `159`다.
- [x] family proposal schema를 `v27.mass.family-proposal.3`으로 올리고 current recipe authority를 직접 재캡처한다. 변경되는 item의 producer/consumer 중 설명 계약이 열려 있으면 `proposal-blocked:dependency-mass-contract-open`으로 차단한다.
- [x] singleton maxStack `1`인 12~13kg 중장비 세 개를 `intentional-single-heavy`로 분류하여 거짓 Critical을 제거했다. Critical은 unresolved semantic `51`만 남았다.
- [x] 기존 변경 후보 `7`개는 즉시 후보 `5`, dependency-blocked `2`로 분리됐다. `material:flour`는 `recipe:ration-mixture`, `material:lumber`는 current open producer/consumer mass explanation 계약이 닫힐 때까지 적용하지 않는다.
- [x] 두 audit를 별도 2회 실행한 뒤 CSV/TXT 4개의 hash·length·UTC write ticks가 모두 불변이었다. recipe CSV/report SHA-256은 `9BE6A576549CEEF7EDF34BE488EE5CAACE01E20AD0C4784C54E6B10BC4241D30` / `A2229624A1C5B9E81FE70300746472308E846DCC370365F9283025B9368783F3`, family CSV/report는 `E375B40A4A19E35088A053104253451A18BE96C75BA640A3D5D47AFB30041EDF` / `C0C0A6238A2BEEA45CAE0C4BC9B763DD3CB0A693F3D97071ADD99248E2890497`다.
- [x] Unity compile/command 실행과 최종 Console Warning/Error `0/0`; asset mutation `0`, GameplayScene 변경 `0`이다.
- [x] `PhysicalMassTransformContract`에 기존 constructor 호환을 유지하면서 `DeclaredExternalInputGrams + PhysicalMassExternalInputKind`와 `TerminalSinkGrams + PhysicalMassTerminalSinkKind`를 추가했다. 새 full constructor는 입력+외부 유입과 출력+부산물+sink+loss의 exact equality, positive gram의 typed kind, zero gram의 `None`을 검증한다.
- [x] focused self-test는 process-water 외부 입력 `800+200→1000g`, 물리 부산물 없는 수분 손실 `1000→700+300g`, positive untyped external input 거부를 current Unity assembly에서 통과했다.
- [x] open recipe `239`개를 proposal-only family로 분류했다: water incorporation `6`, evaporation/drying `6`, cutting dust/solid offcuts `106`, smelting/off-gas `6`, combustion `1`, biological process `4`, 안전 확정 packaging/sink `0/0`, unexplained `110`. ID 패턴은 감사 후보 생성에만 사용하고 runtime/core 분기로 사용하지 않는다.
- [ ] 첫 구현 family는 `cutting dust/solid offcuts 106`이다.
  - [x] synthetic canary와 `recipe:bowstring-fiber` 대표 레시피를 opaque `process-loss@1` capability로 연결했다. descriptor는 고정 gram을 복제하지 않고 `mode=residual`, `lossKind=CuttingWaste`, canonical reason, `physicalByproduct=false`만 작성한다. 완료 시 실제 `WIP input + process water - physical output - wastewater`에서 손실을 계산한다.
  - [x] 대표 경로는 `240g input → 80g physical bowstring + 160g nonphysical CuttingWaste receipt`를 증명했다. 80g만 FacilityBuffer·capacity·routing에 들어가고, 160g은 prepared-output `DeclaredLoss` line과 outcome/recipe fingerprint에 포함된다. JSON round-trip과 frozen replay에서 재계산·물리 생성·중복 stack이 발생하지 않았다.
  - [x] 전수 audit가 같은 recipe authority를 읽어 `recipe:bowstring-fiber`를 `process-loss@1 / reviewed-exact`로 재분류했다. audit 결과는 reviewed `42→43`, explanation missing `159→158`; 2회 실행의 byte hash·length·mtime 변화는 0이다.
  - [x] 기존 106행 ID-pattern clustering을 입력·출력·시설·손실률 기준으로 행별 재검토했다. 즉시 안전 17, 공정은 맞지만 고손실률 22, 절삭+다른 소비 혼합 2, 섬유 카딩 22, 제분·코팅·마법 12, 조립/BOM/회수 선행 31로 분리했으며 runtime/core의 recipe-ID 분기는 추가하지 않았다.
  - [x] 손실률 40% 이하이고 절삭·재단·고형 offcut 의미가 명확한 17행만 editor content manifest로 작성했다. 첫 실행 변경 17, 두 번째 실행 변경 0이며 audit `reviewedExact 43→60`, `explanationMissing 158→141`이다.
  - [ ] 고손실률 22행은 descriptor 구조가 가능해도 kg/BOM 단위 오류를 손실로 덮지 않도록 수치 승인을 보류한다. 기존 `bowstring-fiber` 66.7% canary도 구조 검증은 PASS지만 밸런스 승인으로 간주하지 않는다.
  - [x] 카딩·방적 22행 중 원섬유 240g→원사 160g으로 수동/동력 쌍이 일치하는 16행은 별도 `FiberProcessingWaste` reason으로 작성했다. 첫 실행 16, 두 번째 0이며 audit `reviewedExact 60→76`, `explanationMissing 141→125`다.
  - [ ] 양모·그늘섬유·힘줄 계열 카딩 6행과 혼합·제분·코팅·마법 14행은 kg/BOM 또는 합성/별도 capability가 필요하고, 조립/BOM/회수 31행은 출력 질량·회수 가능한 physical scrap 정책이 선행돼야 한다.
  - [x] routing V8 batch에 physical line과 분리된 `nonPhysicalDispositions`를 추가했다. exact loss gram·canonical payload·fingerprint·line/batch identity를 routing save/restore와 routing fingerprint에 결속하며 변조는 restore 전에 거부한다. 이 영수증은 물리 route·capacity를 차지하지 않고 batch의 durable checkpoint GC가 성공할 때까지 보존된다.

이 변경은 감사 의미와 적용 게이트를 교정한 것이며 item kg·BOM·WU·EWU·가격을 바꾸지 않았다. Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`은 유지한다.

## 126. 2026-08-30 residual process-loss capability와 실제 생산 canary

상태: **공용 capability·실제 `bowstring-fiber` resolve/save/frozen-retry/audit·routing checkpoint tombstone receipt PASS / 106행 전수 authoring OPEN / 상위 체크포인트 유지**.

- [x] `ProductionRecipeSO`에 `{capabilityId, contractVersion, canonicalPayload}`만 가진 opaque mass-explanation envelope를 추가하고 `ProductionRecipeSemanticDigest@4`에 포함했다. 신규 공정 유형은 생산 코어의 recipe-ID switch가 아니라 등록 capability를 추가한다.
- [x] `ProductionMassExplanationCapabilityRegistry`는 `(ID, version)` stable key, duplicate·unknown·invalid payload fail-loud, registry fingerprint를 제공한다. 첫 capability `process-loss@1`은 exact runtime subject에서 positive residual을 계산하며 `CuttingWaste`와 canonical reason을 immutable disposition으로 만든다.
- [x] 고정 `grams`를 RecipeSO에 중복 저장하지 않는다. kg가 후속 조정되면 실제 WIP·output gram에서 residual이 다시 계산되고 recipe digest·equation fingerprint·outcome fingerprint가 함께 바뀐다.
- [x] 정상 prepared-output resolver는 physical output capacity claim을 먼저 계산한 뒤 nonphysical `DeclaredLoss` line을 추가한다. capacity·FacilityBuffer·publication·routing은 physical 80g만 계산하고 loss 160g은 물리 item이나 바닥 clutter로 생성하지 않는다.
- [x] 실제 `recipe:bowstring-fiber`의 current runtime/audit 권위가 모두 `resource:shade-fiber 2 = 240g`, `material:bowstring 1 = 80g`, residual `160g`으로 일치한다. 처음 검토한 `treated-lumber`는 runtime 300g/audit 100g 권위 불일치를 발견해 canary에서 제외했고 OPEN 상태로 되돌렸다.
- [x] pure capability fixture는 다른 recipe ID에서도 같은 descriptor 작동, input보다 output이 큰 음수 residual, unknown/duplicate capability, 비정규 payload를 검증했다.
- [x] 실제 adapter fixture는 loom facility, WIP 240g, physical output 80g, CuttingWaste receipt 160g, 4-cycle/portfolio capacity 4,000g, JSON byte round-trip, frozen completed replay의 추가 physical stack 0을 통과했다.
- [x] prepared-output contract와 P03 full current-format persistence 회귀가 함께 PASS했다. 최종 Unity Console Warning/Error `0/0`, GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 불변이다.
- [x] audit CSV/report SHA-256은 `E404AEC60D42FC55C213DB28141C0A0008559C4081CA202C5AA5ADAB803CAD68` / `830119199A8781B0CF83D233EDF862841BED08730F11BA225CB4955C52B0AF94`; 길이 `124,466 / 745 bytes`; 두 번째 실행에서 hash·length·UTC write ticks가 모두 불변이다.
- [x] routing save schema를 V8로 올리고 nonphysical disposition tombstone을 물리 route line과 분리했다. prepared batch가 정상 clear된 뒤에도 loss `outputLineId/lineCommitId/payload/fingerprint/exactMassGrams`는 routing batch에 남고, 저장·복원·route replay·drain 완료를 거쳐 durable checkpoint GC 성공 시 물리 route tombstone과 함께 제거된다. 1g 변조는 routing fingerprint/total 검증에서 fail-loud한다.
- [x] 절삭/offcut 후보 106행의 의미 분류는 완료했다. 안전 17행만 authoring·전수 runtime/audit·2회 무변경을 통과했으며 상세 분류는 §126.2다.
- [x] 카딩·방적 22행 중 33.3%로 일치하는 수동/동력 16행을 `FiberProcessingWaste`로 추가 작성하고 별도 2회 무변경을 통과했다.
- [ ] 나머지 72행은 고손실률 21(별도 bowstring canary 1 제외), 혼합 2, 카딩 고손실 6, 다른 공정 12, BOM/회수 선행 31이다. kg/BOM 승인·합성/별도 capability·scrap 정책이 남아 family parent는 계속 OPEN이다.

### 126.1 routing V8 nonphysical disposition tombstone — 2026-08-30

- [x] `ProductionPreparedOutputRoutingBatchSaveData`는 `totalDeclaredLossMassGrams`와 stable-ordered `nonPhysicalDispositions`를 소유한다. disposition은 batch/line/output identity, role, canonical payload, SHA-256 fingerprint와 exact gram만 저장하고 item·quantity·destination을 만들지 않는다.
- [x] `PublishCommittedBatch`는 completed prepared-output의 physical line만 routable line으로 만들고 DeclaredLoss는 별도 tombstone으로 투영한다. `CaptureAll/CaptureDestination/PrepareRoute`에는 nonphysical line이 나타나지 않으므로 haul·FacilityBuffer·창고 admission이 오염되지 않는다.
- [x] routing fingerprint `prepared-output-routing-v8`은 declared-loss total과 모든 disposition 필드를 포함한다. null/unsorted/duplicate identity, noncanonical payload, nonpositive gram, wrong role, total drift와 1g 변조는 current-format restore 전에 실패한다. 과거 save migration은 범위 밖이다.
- [x] detached terminal save 검증은 prepared DeclaredLoss line과 routing disposition의 payload/fingerprint/gram을 1:1 대조한다. durable projector도 disposition을 outputLineId/lineCommitId ordinal 순으로 canonicalize한다.
- [x] focused routing fixture는 90g loss의 publish→save→restore→partial route→full drain→checkpoint tombstone을 검증하고 checkpoint GC 전까지 동일 영수증을 확인했다. 변조 restore 거부와 GC 후 whole-batch 제거도 통과했다.
- [x] current Unity에서 routing authority, pure mass capability, real bowstring adapter, full current-format persistence 묶음이 `V27_PROCESS_LOSS_ROUTING_RECEIPT_FOCUSED_PASS`; 최종 Console Warning/Error `0/0`, GameplayScene hash 불변이다.

이 영수증은 감사 이력을 영구 보관하는 별도 ledger가 아니다. durable save checkpoint가 해당 물리 route를 확정하기 전까지 재시작·재시도에 필요한 exact tombstone이며, 성공한 checkpoint GC 뒤 장기 분석은 결정론적 V27 ledger artifact가 담당한다.

### 126.2 절삭/offcut 106행 행별 분류와 안전 17행 적용 — 2026-08-30

상태: **106/106 의미 분류 완료 / 안전 17행 authoring·audit·2회 no-op PASS / 나머지 수치·별도 capability OPEN**.

보수적 `40%` 손실률 선은 새 gameplay 규칙이 아니라 kg/BOM 오류를 대량 descriptor로 은폐하지 않기 위한 이번 적용의 review gate다.

#### 즉시 작성 완료 17

```text
recipe:bedding-animal
recipe:bolt-bone
recipe:cold-work-suit
recipe:component:brigandine-padding
recipe:component:precision-parts
recipe:component:price-board
recipe:component:room-partition-kit
recipe:tool:hauling-harness
recipe:v22:apparel:blouse
recipe:v22:apparel:gloves
recipe:v22:apparel:long-underpants
recipe:v22:apparel:shorts
recipe:v22:apparel:sleep-bottom
recipe:v22:apparel:sleep-top
recipe:v22:apparel:smoke-protection-hood
recipe:v22:apparel:tail-guard
recipe:v22:weave:cave-silk
```

#### 절삭 의미는 맞지만 kg/BOM 검토 선행 22

```text
recipe:arrow-bone
recipe:bone-charm
recipe:bowstring-fiber
recipe:component:blast-coat-shell
recipe:stone-block
recipe:stone-ornament
recipe:v22:apparel:belt
recipe:v22:apparel:chest-wrap
recipe:v22:apparel:contract-sash
recipe:v22:apparel:footwraps
recipe:v22:apparel:hat
recipe:v22:apparel:horn-ring
recipe:v22:apparel:loincloth-underwear
recipe:v22:apparel:lower-underwear
recipe:v22:apparel:scarf
recipe:v22:apparel:sky-chorus-shawl
recipe:v22:apparel:slime-warming-pad
recipe:v22:apparel:socks
recipe:v22:apparel:spore-protection-hood
recipe:v22:apparel:tail-ribbon
recipe:v22:apparel:undershirt
recipe:v22:mending-scrap
```

이 22행의 현재 손실률은 약 `41~86%`다. `recipe:bowstring-fiber`는 240g→80g+160g 영수증 구조를 검증하는 canary지만 66.7% 손실 자체는 kg/BOM 승인 전이다.

#### 합성 설명 필요 2

```text
recipe:book:seasonal-almanac
recipe:component:engineering-drawing
```

종이 절삭과 목탄·표식 소비가 함께 있으므로 `PaperTrim + MarkingConsumable` 합성 설명 없이는 단일 CuttingWaste로 닫지 않는다.

#### FiberProcessingWaste 계열 22 — 안전 16 완료 / 고손실 6 OPEN

```text
recipe:bowstring-sinew
recipe:wool-cloth
recipe:v22:spin-powered:cave-silk
recipe:v22:spin-powered:common-wool
recipe:v22:spin-powered:deep-goat-wool
recipe:v22:spin-powered:dreamweave
recipe:v22:spin-powered:ember-cotton
recipe:v22:spin-powered:frost-linen
recipe:v22:spin-powered:frost-wool
recipe:v22:spin-powered:mire-canvas
recipe:v22:spin-powered:shade-cloth
recipe:v22:spin-powered:spore-hemp
recipe:v22:spin:cave-silk
recipe:v22:spin:common-wool
recipe:v22:spin:deep-goat-wool
recipe:v22:spin:dreamweave
recipe:v22:spin:ember-cotton
recipe:v22:spin:frost-linen
recipe:v22:spin:frost-wool
recipe:v22:spin:mire-canvas
recipe:v22:spin:shade-cloth
recipe:v22:spin:spore-hemp
```

완료 16행은 `cave-silk/deep-goat-wool/dreamweave/ember-cotton/frost-linen/frost-wool/mire-canvas/spore-hemp`의 수동·동력 쌍이다. 각 행은 240g→160g+80g으로 손실률 33.3%가 동일하다. `bowstring-sinew` 94.1%, `wool-cloth` 61.9%, common-wool 수동/동력 84.8%, shade-cloth 수동/동력 55.6%는 kg/BOM 검토 전 OPEN이다.

#### 제분·코팅·마법 등 별도 설명 12

```text
recipe:starch
recipe:ammo:mana-disruptor-bolt
recipe:component:dreamweave-rune-lining
recipe:component:rune-leather-strap
recipe:component:rune-tuning-shield
recipe:gold-leaf
recipe:gold-ornament
recipe:medical:mana-core-case
recipe:rune-cold-suit
recipe:slime-warming-pad
recipe:trail-charm
recipe:treated-lumber
```

`starch`는 제분 잔여물, `slime-warming-pad/treated-lumber`는 수지 함침·경화, 나머지는 룬·마나 소비·코팅·추출·제련이 혼합된다. 특히 treated-lumber의 runtime/audit gram 불일치는 descriptor보다 권위 교정이 먼저다.

#### 조립·BOM·회수 정책 선행 31

탄약 단위/BOM 의심 3:

```text
recipe:ammo:armor-piercing-cartridge
recipe:ammo:paper-cartridge
recipe:ammo:scatter-cartridge
```

금속 가공·복합 조립 16:

```text
recipe:arrow-iron
recipe:arrow-steel
recipe:bolt-iron
recipe:bolt-steel
recipe:component:lead-counterweight
recipe:component:powered-armor-joint
recipe:component:siege-counterweight
recipe:component:siege-reinforcement-kit
recipe:component:waterwheel-drive-shaft
recipe:surgery:prosthetic-arm
recipe:surgery:prosthetic-leg
recipe:tool:alloy-crucible
recipe:tool:deep-shaft-hoist
recipe:tool:inspection-gauge
recipe:tool:precision-gauge
recipe:tool:reinforced-restraint
```

완제품 조립 9:

```text
recipe:component:factory-installation-plan
recipe:component:prototype-package
recipe:component:sealed-seasonal-container
recipe:component:stock-sensor-panel
recipe:tool:banquet-cart
recipe:tool:mana-probe
recipe:tool:rune-identification-lens
recipe:tool:weather-observation-kit
recipe:v22:sewing-kit
```

기록·제본 혼합 3:

```text
recipe:record:arcane-index
recipe:record:breeding-ledger
recipe:record:career-ledger
```

이 31행은 회수 가능한 금속·부품 scrap을 abstract loss로 지우지 않는다. 특히 탄약 3행 손실률 약 91~94%, sewing-kit 2,100g→50g은 unit/BOM 오류 가능성을 먼저 해결한다.

#### 적용·검증 증거

- `V27ReviewedProductionMassExplanationCatalog`는 17개 stable ID를 ordinal 정렬·중복 0으로 보유하는 editor-only content manifest다. runtime resolver는 recipe ID를 알지 못하며 모든 행은 동일 opaque `process-loss@1` capability를 사용한다.
- Resource, ResearchOverhaul, V22Apparel builder가 같은 manifest를 호출하므로 에셋 재생성 뒤에도 descriptor가 유실되지 않는다.
- 첫 적용 `changed=17`, 두 번째 적용 `changed=0`; audit는 recipe `355`, reviewed exact `60`, explanation missing `141`, external input missing `80`, role mismatch `0`이다.
- cutting 17 적용 시점 audit CSV/report SHA-256은 `9CE9BF472A1B3EAB27EF60A9BB568EE7177E8B46FBE3CE54CE4BC51AD24B513A` / `FDDC2ADB2ABA7F40C84A2579C9680479378D5B90D4C558013893A09D40283C45`다.
- fiber-processing 16 추가 뒤 최종 audit는 reviewed exact `76`, explanation missing `125`; CSV/report SHA-256 `C2312568D2C568D95E1644AF6B75CB7387C882D77C7F6D66EDB0D57C36CBF333` / `4E523920BC6C169C09CA49002E29AB2238E907C37FE66684480C393BC5FF1254`; 별도 두 번째 실행에서 hash·length·UTC ticks 변화 `0`이다.
- focused capability·real adapter·routing V8·full persistence PASS, Console Warning/Error `0/0`, GameplayScene SHA-256 불변이다.

이 체크포인트는 17행의 **질량 차이 설명 authoring**을 닫는다. kg·BOM·WU·EWU·가격을 바꾸거나 106행 family 전체, 전수 질량 밸런스, 6인 생존망을 완료 처리하지 않는다.

### 126.3 안전한 섬유 카딩·방적 16행 적용 — 2026-08-30

- [x] `PhysicalMassLossKind.FiberProcessingWaste`를 기존 enum 끝에 추가했다. `process-loss@1` capability는 새 recipe 분기나 새 resolver 없이 동일 residual 계약으로 이를 처리한다.
- [x] V22 수동/동력 방적 쌍 가운데 input 240g, output 160g, residual 80g인 8재료×2경로만 editor reviewed manifest에 추가했다.
- [x] common-wool, shade-cloth, bowstring-sinew, wool-cloth 6행은 각각 55.6~94.1% 손실이라 descriptor로 kg/BOM 오류를 덮지 않고 OPEN으로 유지했다.
- [x] V22 builder가 같은 manifest를 호출하므로 재생성 후에도 authoring이 보존된다. 첫 apply `changed=16`, 두 번째 `changed=0`이다.
- [x] 전수 audit는 recipe 355, reviewed exact 76, explanation missing 125, external-input missing 80, role mismatch 0이다. CSV/report SHA-256은 `C2312568D2C568D95E1644AF6B75CB7387C882D77C7F6D66EDB0D57C36CBF333` / `4E523920BC6C169C09CA49002E29AB2238E907C37FE66684480C393BC5FF1254`; 별도 두 번째 실행은 hash·length·mtime 변화 0이다.
- [x] capability·routing V8·full current-format persistence focused suite가 `V27_REVIEWED_FIBER_PROCESSING_FOCUSED_PASS`, Console Warning/Error `0/0`, GameplayScene SHA-256 불변이다.

authored unit kg·BOM·WU·EWU·가격은 변경하지 않았다. 이 체크포인트도 물리 차이의 설명만 추가하며 고손실 6행과 전체 family parent는 닫지 않는다.

authored kg·BOM·WU·EWU·가격·소비량은 변경하지 않았다. 바뀐 콘텐츠 데이터는 `bowstring-fiber`의 기존 160g 차이를 설명하는 capability 한 행뿐이다. 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.4 게임 공정 질량의 설명 가능성 경계와 runtime/proposed 이중 감사 — 2026-08-30

상태: **정책 교정·audit schema v3·Unity compile·결정론적 2회 생성 PASS / runtime-proposed 불일치 23 recipe OPEN / authored kg·BOM 적용 없음**.

게임 공정에 현실의 닫힌계 질량 보존이나 열역학적 엔트로피 불변을 그대로 강제하지 않는다. 엔트로피는 애초에 질량과 같은 보존량이 아니며, 이 계획이 증명해야 하는 것은 현실 물리학이 아니라 플레이어가 이해할 수 있는 게임 회계다. 재미와 콘텐츠 밀도를 해치면서 증기·먼지·찌꺼기 item을 무조건 생성하는 것은 목표가 아니다. 대신 서로 다른 세 불변식을 분리한다.

1. **소유권·수량·WIP 불변식은 exact다.** 이미 생성된 item lot, carried slice, reservation, WIP input과 frozen output은 취소·파괴·저장복원·재시도에서 삭제·복제·순간이동하면 안 된다.
2. **경제 불변식은 exact다.** 입력 Ceil, 산출·회수 Floor, repeatable transform 최소 `-1 mEWU`, SCC tolerance `0`을 유지한다. 추상 손실은 회수·판매·재투입 credit을 만들지 않는다.
3. **공정 질량은 문자 그대로 동일할 필요가 없지만 차이는 전부 설명되어야 한다.** 수분 증발, 절삭 손실, 연소, 마법적 생성·소멸처럼 게임에 필요한 비보존 변환을 허용한다. 다만 그 차이를 kg/BOM 오류나 복제 버그와 구분할 수 있도록 다음 회계식이 runtime과 proposed 양쪽에서 닫혀야 한다.

```text
physical input + typed external input
= physical product + physical byproduct
 + typed terminal sink + typed abstract process loss
```

이 식은 `input gram == output gram`을 강제하는 물리 보존식이 아니다. `typed external input`, `terminal sink`, `abstract process loss`가 의도된 비보존량을 명시적으로 설명하는 감사식이다. 이 값들은 필요할 때 gameplay상 생성·소모될 수 있지만, 동일 operation에서 exact-once로 기록되고 EWU 회수 credit을 만들지 않아야 한다.

허용되는 예:

- 조리·건조·발효의 수분 증발
- 절삭·연마의 gameplay 가치 없는 미세 분진
- 소성·제련의 가스·슬래그 중 별도 운반 gameplay가 없는 부분
- 마법적 변환에서 명시적으로 소비되는 typed reagent/energy-mass abstraction

허용되지 않는 예:

- kg 또는 BOM 오류를 큰 `process-loss`로 가리는 것
- 회수·수리·오염·폐기 gameplay 가치가 있는 50g 이상 고형 잔여물을 추상 삭제하는 것
- 포장 용기 tare를 Sink와 함께 사라지게 하는 것
- 시설 파괴·취소·Downed를 공정 손실로 기록하는 것
- 설명이 없다는 이유로 의미 없는 waste item을 대량 추가하는 것

`40%` 손실률은 절대 물리 법칙이 아니라 대량 자동 승인 차단선이다. 이를 넘는 행은 unit/BOM/output batch, 물리 부산물, 외부 입력과 downstream 경제를 사람이 검토한다. 40% 이하라도 고형 잔여물에 재사용 가치가 있으면 physical byproduct가 우선이다.

#### runtime/proposed 권위 분리

기존 audit는 `CanonicalItemUnitSemantic.CanonicalUnitMass`가 있으면 아직 적용되지 않은 proposed gram을 우선 사용했다. 따라서 실제 `ItemDefinitionSO.UnitWeight`와 제안값이 다른 레시피를 마치 현재 gameplay 질량인 것처럼 계산할 수 있었다. schema v3는 각 레시피에 두 식을 동시에 기록한다.

- `physicalInput/Output/ResidualGrams`: 제안된 canonical mass 기준
- `runtimePhysicalInput/Output/ResidualGrams`: 현재 live `ItemDefinitionSO.UnitWeight` 기준
- `runtimeProposedMassMismatchIds`: 두 권위가 다른 exact item ID
- `massCreationCandidate`와 `runtimeMassCreationCandidate`: 양쪽을 별도로 판정

descriptor가 작성된 recipe는 runtime과 proposed 양쪽에서 같은 capability가 exact positive residual을 설명해야 `reviewed-exact`가 된다. 둘 중 하나라도 음수·확률 branch 불일치·설명량 drift가 있으면 audit 자체가 fail-loud한다. 기존 수동 transform contract는 제안 kg 권위의 검토 기록이며, runtime/proposed mismatch를 해소하거나 명시적으로 승인하기 전에는 asset apply 증거로 사용하지 않는다.

현재 전수 결과:

- recipe `355`, Source `23`, Transform `328`, Sink `4`, role mismatch `0`
- reviewed exact `67`, proposed-only/runtime-mismatch reviewed `9`, explanation missing `125`, external-input authority missing `80`
- proposed mass-creation candidate `122`, runtime mass-creation candidate `127`
- runtime/proposed mass mismatch recipe `23`, missing semantic recipe `47`, probabilistic recipe `3`
- `recipe:arrow-bone`: proposed `1,550→800g`, residual `750g`; runtime `1,650→800g`, residual `850g`; mismatch `material:lumber`

따라서 `arrow-bone`, `treated-lumber` 및 같은 변경 item을 쓰는 하류 recipe를 proposed 식 하나로 승인하지 않는다. 먼저 item kg의 실제 적용 묶음과 모든 producer/consumer를 결속하고, 그 직후 audit 양쪽이 같아지는지 확인한다.

#### 검증 증거와 다음 경계

- [x] audit CSV schema `v27.mass.recipe-inventory.3`에 runtime six-column equation, mismatch IDs와 runtime mass-creation 판정을 추가했다.
- [x] Unity current-source compile과 audit 실행이 PASS했고 Console Warning/Error `0/0`이다.
- [x] legacy reviewed contract가 proposed 식에는 맞지만 runtime item gram이 다르면 `reviewed-proposed-runtime-mismatch`로 강등한다. 현재 9행이며, 이 중 dough·grain-porridge·mushroom-soup·roasted-meat·root-stew 5행은 runtime 식에서 mass creation candidate다.
- [x] 이 상태는 family proposal dependency gate로 전파된다. 현재 proposed change `7`은 ready `0`, blocked `7`이며 선행 kg 적용·재감사 전에는 SO apply 대상이 아니다.
- [x] CSV `136,888 bytes`, SHA-256 `51C7CFB340FC0B2DA2AAF536F1FF034258EF15B4F24B4847A49471DB7C267A3A`; report `854 bytes`, SHA-256 `F7EB7BDFAD4025018D5D4439562BFF864EB086611C1B7488DDFE9F5CA6DF4211`이다.
- [x] family CSV/report SHA-256은 `23C2AEF5007CDEC8BF288B0CFD2C567938188FFE2C4AE71B4BF503CCB74967E0` / `0FD1353CFFBDB0BE09C6A4BFF246BA4BC74904621137F20C87634800DB47BEAA`다.
- [x] 별도 두 번째 Unity 실행에서 네 파일의 hash·length·UTC write ticks가 모두 불변이다.
- [x] 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 유지됐다.
- [ ] mismatch 23 recipe의 item 적용 순서와 dependency closure를 확정한다.
- [ ] 고손실 22행은 의복 coefficient 기반 BOM 분리, mending-scrap 회수 순환, 중복 slime-warming-pad, stone/bone/lumber 물리 부산물을 먼저 닫는다.
- [ ] 그 뒤에도 남는 gameplay 무의미 잔여만 typed abstract loss로 작성하고 EWU/SCC·6인 생존·운반 회귀를 재실행한다.

이 체크포인트는 audit가 현재 gameplay와 제안 상태를 혼동하지 않게 만든 구조 교정이다. authored kg·BOM·output quantity·WU·EWU·가격·SO 수치 변경은 `0`이며, 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.5 mending-scrap 수율과 의복 offcut 선행 구조 — 2026-08-30

상태: **전수 호출·경제·물류 감사 완료 / 단독 수치 적용 금지 / 수선 비용 정책 사용자 결정 OPEN / 구현 전 co-product 권위 필요**.

#### 현재 사실

- `recipe:v22:mending-scrap`은 live asset과 builder 모두 cloth `1×200g → scrap 2×50g=100g`, current authored work `16 WU`다.
- 물리적으로 자연스러운 후보는 scrap `4×50g=200g`이다. 한 batch는 200g이고 4-cycle output buffer 요구는 800g, max stack 200 안에서 한 stack이므로 물류 모양 자체는 안전하다.
- 실제 severe repair는 sewing thread 1 + mending scrap 1을 소비한다. alteration은 현재 scrap을 소비하지 않지만 dependency catalog에는 `apparel-alteration:patch`가 소비자로 기록돼 있어 가짜 수요를 만든다.
- rejected apparel 해체는 mending scrap이 아니라 원래 소재를 50% 회수한다. Editor fixture의 `RecoveryItemId=mending-scrap`은 live 권위가 아니다.

#### output 2→4 단독 적용 차익

현재 원장:

```text
batch total cost = 40,941~40,942 mEWU
current output 2 acquisition = 20,471 mEWU/개
candidate output 4 acquisition = ceil(batch/4) = 10,236 mEWU/개
current market sale credit = 12,281 mEWU/개
candidate batch sale = 4 × 12,281 = 49,124 mEWU
minimum arbitrage = 49,124 - 40,942 = +8,182 mEWU
```

따라서 output quantity만 바꾸지 않는다. 같은 atomic apply 묶음에서 최소 다음을 재생성한다.

- acquisition `10,236 mEWU/개`
- authored unit price 후보 `ceil(10,236/3,000)=4 gold`
- market sale credit 상한 `floor(10,236×0.60)=6,141 mEWU/개`
- 4 gold와 이 상한을 넘지 않는 saleRate
- 계약 요구량·보상, 시작 scrap 6개 가치, repair 1회의 재료 EWU
- craft→sell, repair→dismantle→sell, purchase→repair/alter→sell SCC
- 800g FacilityBuffer admission, WaitingForOutputSpace, overflow 일반 바닥 drop 0

#### 수선 비용 결정 — 구현 전 사용자 선택 1건

- **권장: severe repair scrap `1→2`.** cloth 한 batch가 제공하는 repair 횟수는 Before `2/1=2회`, After `4/2=2회`로 같고, 실제 패치 질량도 50g→100g이 되어 기존 수선 경제와 물리량을 함께 보존한다.
- 대안: scrap `1` 유지. repair 한 회의 scrap EWU가 약 절반이 되고 cloth 한 장이 4회를 수선해 의도적인 수선 완화 패치가 된다.

이 선택 전에는 output4·가격을 적용하지 않는다. 선택과 무관하게 가짜 `apparel-alteration:patch` consumer는 실제 소비를 구현하지 않는 한 제거한다.

#### 의복 offcut을 즉시 붙일 수 없는 구조적 이유

1. builder, live CreateCraft, restore/reselection, throughput, capacity가 모두 `ceil(2×apparel.Coefficient)`를 각각 계산한다. 성능 계수와 물리 패턴/BOM이 결합돼 있다.
2. domain `ApparelWorkOrderRuntime`은 unique garment 하나만 exact-once 게시하며 offcut sidecar output/receipt/save 권위가 없다.
3. 같은 재단대에서 generic production panel과 apparel domain panel이 모두 live다. RecipeSO에만 byproduct를 넣으면 두 제작 경로의 BOM·출력이 달라진다.
4. 이 감사 시점의 `V27EmbeddedWorkValueCalculator`는 Main과 Byproduct의 총 수량으로 batch cost를 나눈 뒤 모든 output item에 같은 per-unit acquisition을 줬다. 이 결함은 아래 126.6의 출력 라인별 allocation으로 교정했으며, 의복 offcut 적용은 그 계약의 전수 경제 재감사 뒤에만 진행한다.

#### 다중 산출물 원가 배분 계약

`V27` After 계산에서는 두 개 이상의 positive physical output을 가진 Transform에 명시적 allocation policy를 요구한다. 기존 V23 Before 재현 경로는 동결하고 조용히 의미를 바꾸지 않는다.

```text
batchInputDebit = Ceil(inputs + directWU + logistics + utility + loss)

allocatedDebit[i] >= 0
sum(allocatedDebit[i]) = batchInputDebit
perUnitAcquisition[i] = Ceil(allocatedDebit[i] / expectedUnits[i])

sum(Floor(expectedUnits[i] × recoverableValue[i]))
<= batchInputDebit - 1mEWU
```

정책은 recipe-ID switch가 아니라 등록형 `IProductionOutputCostAllocationPolicy`와 opaque policy descriptor로 확장한다.

- `single-main`: 물리 output 하나인 기존 경로.
- `main-with-recoverable-byproduct-credit`: independently bounded byproduct recoverable credit을 먼저 배정하고 main이 exact remainder를 부담.
- `homogeneous-mass-share`: 같은 재질의 main/offcut처럼 gram share가 경제 의미와 일치하는 경우만 사용.
- 시장 가격 비율을 allocation input으로 쓰는 정책은 가격↔EWU 순환을 만들므로 금지.
- policy가 없는 multi-output Transform, 음수 remainder, batch credit 비음수 margin, 역할/weight drift는 fail-loud.

분할 Ceil 때문에 output별 per-unit acquisition 합이 batch debit보다 커질 수는 있지만 작아질 수는 없다. 판매·회수 credit은 별도 Floor이며 batch 전체에서 반드시 최소 1mEWU 손실을 남긴다.

#### 구현 순서

1. current multi-output Source/Transform 전수 census와 legacy equal-unit allocation 영향 목록을 만든다.
2. `apparel-alteration:patch` catalog-only 가짜 consumer를 제거하거나 실제 소비 경로를 구현한다.
3. 사용자 선택에 따라 mending-scrap output/repair quantity를 builder·asset·runtime·restore·throughput·capacity에 원자 반영한다.
4. 같은 apply에서 EWU·unit price·saleRate·계약·시작 자본을 재생성하고 SCC 및 800g buffer 실제 생산을 통과한다.
5. 공용 `ApparelMaterialYieldAuthority`가 input quantity, garment grams, recoverable scrap, abstract trim을 한 번 계산하게 한다.
6. generic apparel recipe와 domain apparel command의 실행 권위를 하나로 통합하거나 generic bill이 domain command에 명시적으로 위임한다.
7. domain craft에 garment + offcut sidecar의 exact outcome freeze, capacity reservation, publication, save/restore/retry를 추가한다.
8. 다중 산출물 원가 allocation을 적용하고 garment·scrap·판매·수리·해체 SCC를 재생성한다.
9. 이후에만 의복별 50g 단위 recoverable scrap과 50g 미만 abstract trim을 작성한다.

현재 단계에서는 설계·읽기 전용 감사만 완료했다. item kg·BOM·output quantity·repair 소비·가격·saleRate·계약·SO 변경은 `0`이다.

### 126.6 다중 산출 EWU 배분 권위 구현 — 2026-08-30

상태: **공통 capability·3개 live Source authoring·계산기 연결·집중 결정론 PASS / world-resource·crop output-capacity 증거 갱신 PASS / dependency review 86,933행·SCC 297·integrity failure 0 / 건설 노동밀도 2건과 stale 노동 승인 328건 OPEN / mending-scrap·의복 수치 적용 없음**.

#### 전수 범위와 기존 결함

- 전체 recipe `355`, positive physical output line `357`이다.
- positive physical output이 2개 이상인 recipe는 정확히 `3`개이며 모두 Source다. Transform multi-output은 `0`개다.
- role은 Main `351`, Byproduct `6`, ReturnedPackaging `0`, RecoverableWaste `0`이다.
- 해당 Source는 `source:logging`, `source:quarry`, `source:saltstone`이다.
- 기존 equal-unit 계산은 모든 `amount×probability`를 하나의 분모로 합치고 같은 unit acquisition을 모든 output에 배정했다. 이 때문에 quarry의 `coal @20%`와 `mana-crystal @1%`가 발생 1개당 같은 `9,335 mEWU`를 받는 역할·희소성 왜곡이 있었다.

#### 구현된 확장 계약

- `ProductionRecipeSO`가 opaque `ProductionOutputCostAllocationAuthoring`을 소유한다. 필드는 `capabilityId`, `contractVersion`, `canonicalPayload`이며 recipe semantic digest를 `production-recipe-semantic@5`로 올려 경제 의미 변경을 source digest에 포함했다.
- runtime calculator는 recipe ID나 item ID를 분기하지 않는다. `ProductionOutputCostAllocationCapabilityRegistry`가 capability/version으로 정책을 선택한다.
- 첫 등록 정책은 `weighted-output-share@1`이다. payload는 ordinal 정렬된 `outputLineId=integerWeight` 목록이며 market price를 입력으로 받지 않는다.
- 단일 physical output은 기존처럼 batch debit 전부를 부담한다. 둘 이상이면 명시적 descriptor가 없거나 line 누락·중복·미정렬·음수·overflow weight·Main 복수/누락이면 fail-loud한다.
- 현재 Source builder의 기본 정책은 `weight=authored amount`다. 이는 Source에만 허용한다. 향후 multi-output Transform은 builder가 자동 추론하지 않고 콘텐츠가 명시적인 allocation capability를 작성해야 한다.
- 각 non-Main line은 integer mEWU 비율의 Floor를 받고, 유일 Main이 exact remainder를 받는다. 따라서 `sum(allocatedDebit)==batchDebit`가 정확히 성립한다.
- 각 line의 per-unit acquisition은 `Ceil(allocatedDebit/expectedUnits)`다. 같은 item을 여러 line에서 내면 line debit과 expected units를 item별로 먼저 합친 뒤 한 번 Ceil한다.
- ReturnedPackaging은 weight `0`만 허용하며 `IsAcquisitionCandidate=false`를 끝까지 보존한다. 반환 용기를 0원 생산품으로 fixed-point에 넣어 기존 용기 acquisition을 지우는 것을 금지한다.
- recoverable value와 판매 credit은 기존 Output Floor 경로를 유지하며 allocation input으로 사용하지 않는다. SCC tolerance도 계속 `0`이다.

#### 실제 live authoring과 결과

| recipe | output별 After acquisition mEWU/개 |
|---|---|
| `source:logging` | log `4,159`, dark-resin `23,100` |
| `source:quarry` | stone `5,135`, coal `25,665`, iron-ore `32,082`, gold-ore `171,100`, mana-crystal `513,300` |
| `source:saltstone` | stone `4,528`, saltstone `18,112` |

이 값은 line amount weight와 발생 확률로 계산한 **감사 계산값**이다. authored item price·saleRate·계약·보상에 아직 적용한 승인값이 아니다. 특히 mana-crystal은 downstream consumer `18`개라 anomaly root tree, 시장 가격, 계약 ROI와 SCC를 함께 재생성한 뒤 적용 여부를 판단한다.

#### 검증 증거

- [x] 세 recipe 모두 output allocation debit 합계가 exact batch debit과 같다.
- [x] quarry의 per-unit acquisition은 `stone < coal < iron < gold < mana`로 희소성 순서를 반영한다.
- [x] synthetic Main+ReturnedPackaging에서 반환 용기의 allocated debit은 `0`, acquisition candidate는 `false`다.
- [x] recipe·crop·item·equipment·material 입력 열거 순서를 역순으로 바꿔도 semantic snapshot hash가 같다.
- [x] 집중 artifact `Artifacts/QA/v27-output-cost-allocation-focused.txt`는 `390 bytes`, SHA-256 `F08A706DF53C86FC73BD0755213446FC6473F272DCA80D23448CE53C1E81F17B`이며 두 번째 생성에서 hash·length·mtime 변화가 `0`이다.
- [x] Unity current-source runtime/editor compile, 집중 계약, Console Warning/Error `0/0`을 통과했다.
- [x] 256-seed의 allocation·strict-loss·snapshot determinism 구간은 통과해 통합 audit 진입까지 도달했다.
- [x] 기존 `v27-balance-output-capacity-playmode.txt`의 오래된 source digest를 현재 world-resource exact-source marker와 crop FacilityBuffer wait/restore/retry marker로 갱신했다.
- [x] 갱신 뒤 처음 드러난 `building:9502`의 불가능 조합을 전수 감사 중단이 아니라 `CriticalDensityUnresolved` root로 수집하도록 교정했다. 후보는 WU `1.5~2.25배`, 기존 BOM 종류, 수량 최대 `150%`, 투자오차 `±2%` 안에서 밀도 drift가 가장 작은 값을 선택하지만 자동 승인·적용하지 않는다.
- [x] synthetic focused fixture는 `797 WU`, lumber `9`, density ratio `1.8242974074`, investment error `853mEWU`, `autoApproved=false`로 PASS했다. artifact SHA-256은 `BA67D6F863FE15ECF393DB037A97C8C6E2E1E8B1903B8132AC22BC5E21E93977`이며 2회 hash·length·mtime 변화가 `0`이다.
- [x] dependency review는 `86,933`행 전체를 끝까지 생성했다. unresolved root `332`, collapsed descendant `39`, SCC `297`, minimum margin `-23,142,719mEWU`, integrity failure `0`이다. 건설 root는 `building:9303`, `building:9502` 두 건이며 실제 `construction-authored-wu:redistributed` approvalKey와 연결된다.
- [x] 안정화 뒤 연속 두 실행에서 CSV·audit·anomaly graph·source inventory·manifest의 hash·length·mtime 변화가 `0`이었다. 생성기는 `v27.13.2`이며 이 절의 baseline record ID를 manifest에 포함한다.
- [ ] 기존 source digest가 바뀌어 stale해진 recipe `direct-wu` exact approval `328`건은 현재 After·dependency·실행 경로를 검토해 재승인해야 한다. 이미 적용됐다는 사실만으로 자동 승계하지 않는다.
- [ ] 새 부산물 가치 폭포에 대해 root-cause anomaly collapse, market/contract regeneration, 전체 SCC를 fresh artifact로 남긴다.

이 체크포인트는 미래 multi-output 콘텐츠가 새 recipe/item switch 없이 descriptor와 capability 등록으로 확장되도록 **계산 구조**를 닫는다. mending-scrap output `2→4`, severe repair 소비량, 의복 offcut, kg·BOM·WU·가격·saleRate·계약 수치는 변경하지 않았다. 부모 진행률은 실제 parent row가 닫히지 않았으므로 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.7 비보존 공정 회계와 시장 live/candidate 권위 분리 — 2026-08-30

상태: **질량·엔트로피 정책 명료화 / 시장 원장 integrity failure 0 / provenance 후보 337·파생 후보 175 검토 OPEN / exact legacy baseline 58 복구 / authored 수치 적용 없음**.

#### 질량·엔트로피 정책

게임은 현실의 닫힌계가 아니므로 모든 레시피에 `input gram == output gram`을 강제하지 않는다. 엔트로피 또한 질량처럼 보존되는 값이 아니며 V27의 검증 대상이 아니다. V27이 strict하게 보존하는 것은 다음 두 경계다.

1. 이미 존재하는 physical lot, WIP, reservation, carried slice와 frozen outcome의 **소유권·수량·exact-once**.
2. 구매·판매·해체·재제작을 반복해 양의 EWU를 만들 수 없다는 **경제 무차익**.

공정 질량은 비보존일 수 있다. 수분 증발, 연소, 절삭 분진, 발효, 마법적 생성·소멸은 gameplay상 허용한다. 단, 차이는 `typed external input`, physical byproduct, `typed terminal sink`, `typed abstract process loss` 중 하나에 operation ID와 exact gram으로 귀속한다. 이 설명 회계는 현실 물리학을 흉내 내기 위한 것이 아니라 kg/BOM 오류, 재시도 복제, 취소 삭제와 의도된 수율을 구분하기 위한 것이다. 의미 없는 폐기물 item 생성을 강제하지 않으며, 추상 손실은 회수·판매·재투입 credit을 만들 수 없다.

#### 시장 권위 분리

건설과 같은 원칙으로 live authored 값과 새 재계산 후보를 한 행에 섞지 않는다.

- canonical market row의 `After`는 현재 live authored 값을 그대로 나타낸다.
- exact approval custody가 남아 있으면 `applied`; 없으면 `provenance-missing`이다. 후자는 현재 에셋을 관측했다는 뜻일 뿐 검토·승인됐다는 뜻이 아니다.
- 새 가격·판매율·재고 구매가·소매가·보상 후보는 `market-recalibration-candidate:*` 행으로 분리한다.
- 후보는 `assetApplied=false`, 빈 approval key, `pending-explicit-review`, `local-critical`이다.
- generic ApplyApproved와 approval validator는 review-only metric을 거부한다.
- 현재 가격·판매율로 계산되는 `market-sale-credit`은 live derived row다. 새 가격·판매율에서 계산되는 회수액은 `market-derived-recalibration-candidate:market-sale-credit`로 분리하고 `collapsed-inherited`, 빈 approval key, `assetApplied=false`로 고정한다. 파생 회수액을 독립 승인하거나 live로 표시하지 않는다.

현재 `market-authority-provenance-missing` 직접 후보는 `337`개다. 별도로 현재 authored 값이 V23 재구성값과 정확히 같음을 증명한 `58`개(unit price 55, guest reward 3)는 `LegacyReconstructedBaseline`으로 복구했다. 이 58개는 승인 완료가 아니라 현재값을 합법적인 Before로 사용하는 일반 pending 후보이며, exact approval key가 있어야만 적용할 수 있다.

| metric | 행 수 |
|---|---:|
| authored unit price | 151 |
| authored market sale rate | 175 |
| stock daily unit cost | 6 |
| retail cost | 2 |
| guest money reward | 3 |

이 337개는 모두 `market-authority-provenance-missing`이다. 기존 승인 이력에서 정확한 현재 판매율을 증명할 수 없는 175건을 자동 승인하지 않았고, 가격·소비자 값도 현재 approval source digest가 증명하지 못하는 상태에서는 같은 실패 계약을 사용한다. 여기에 live/candidate 가격·판매율을 사용하는 파생 sale-credit 후보 `175`개가 추가되지만 독립 Critical annotation을 만들지 않고 상위 원인 아래 접힌다.

민감도상 일괄 적용은 금지한다. unit-price 후보 206개 전체 기준 절대 변화율 중앙값은 `50.34%`, `100%` 초과 62개, `300%` 초과 22개다. 대표적으로 `resource:mana-crystal 4→172 gold`, `stock:mana 4.20075→230.98495 gold/item`이며, 이는 quarry의 희귀 부산물 EWU 배분이 시장까지 전파된 결과다. 현재 live sale credit은 acquisition cost를 초과하는 행이 `0`이라 즉시 양의 차익은 없지만, 60% 회수 목표를 초과하는 행은 `34`개다. 따라서 다음 검토는 희귀 Source allocation, 시장 유동성/접근성, stock procurement와 downstream 계약을 함께 보고 가격 후보를 승인·완화·재배분해야 한다.

#### 검증 증거

- [x] Unity current-source compile PASS.
- [x] 남아 있던 stock V23 reconstruction 전용 precheck 6건을 공통 provenance 계약으로 통합했다.
- [x] typed classifier는 `Implemented / LegacyReconstructedBaseline / PreviouslyApprovedApplied / MissingProvenance / UnauthorizedDrift`를 분리한다. float 1 ULP는 V23 재구성 비교에만 허용하고 approval·candidate equality는 exact다.
- [x] focused gate는 직접 provenance 후보와 파생 후보 합계 `512`행의 exact source/property join, `candidate.Before == live.After`, approval key 부재, asset mutation 금지와 상위 귀속을 통과했다. 별도로 exact legacy baseline 58행의 metric 분포와 mutation eligibility를 검증한다.
- [x] full AuditOnly 결과는 `87,823` rows, integrity failure `0`, Critical `378`, collapsed `189`, SCC `297`, minimum margin `-23,142,719mEWU`다. 이는 `PASS`가 아니라 `REVIEW_REQUIRED`다.
- [x] 별도 두 번째 실행에서 CSV, audit, manifest의 SHA-256·byte length·UTC write ticks가 모두 동일했다.
  - CSV: `17ECA843A9D724DDDDC36902B76BCFEBA76F38529E4A0DFACA7C5AA159541A9C`, `68,372,513 bytes`
  - audit: `734CAE0F3B768606DCCBA7F4C257C40F7F4BB957478FEA63722B7CE113BD506B`, `77,705 bytes`
  - manifest: `62FA03B4FB693DCB5169F8C499CD8B54E140FAFE67520C24B90F4CA40BCB5F94`, `5,583 bytes`
- [x] classifier/test source 변경으로 stale해진 기존 approval key 8개는 의미가 같은 적용값만 재검증했다. 결과는 unchanged `1,363`, revalidated `8`, expired `0`, total `1,371`, 신규 값 승인 `0`이며 approval SHA-256은 `A09A1B282A568F5B2D46F3878342C3F646D608CC8720B04CAC6A57F1362E665A`다.
- [x] 최종 Unity Console Warning/Error `0/0`.
- [x] GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 불변.
- [ ] 337개 현재값의 exact provenance를 복구하거나 명시적 baseline review를 수행한다.
- [ ] 378개 unresolved Critical을 닫은 뒤에만 ApplyApproved와 256-seed 통합 감사를 진행한다.

이번 체크포인트에서 item kg·BOM·WU·EWU·가격·saleRate·계약·보상·ScriptableObject 값 변경은 `0`이다. 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.8 시장 기존 적용 권위 복구와 의존성 root metric 교정 — 2026-08-30

상태: **기존 적용 custody 162 exact 복구 / 판매율 provenance 175 OPEN / 잘못 승격된 sale-rate Critical 12 제거 / strict AuditOnly 결정론 PASS / authored 수치 적용 없음**.

126.7의 `337개 모두 provenance-missing`, `Critical 378`, `collapsed 189`는 이 체크포인트 이전 상태다. Git 이력을 런타임 의존성으로 만들지 않고 일회성 증거 원천으로만 사용해, 현재 후보의 exact Before와 과거 approval After가 같고 stable ID·metric·dependency fingerprint가 일치하는 기존 적용 권위만 복구했다.

| 복구 metric | exact 적용 custody |
|---|---:|
| `authored-unit-price-gold` | 151 |
| `authored-daily-unit-cost-gold` | 6 |
| `authored-retail-cost-gold` | 2 |
| `authored-money-reward-gold` | 3 |
| 합계 | 162 |

복구 계약:

- 현재 asset token이 과거 approval의 exact After와 같아야 한다.
- dependency fingerprint, reasonCode, baseline record ID가 같아야 한다.
- source digest만 바뀐 16건은 현재 asset이 exact After를 보유한 경우에만 기존 semantic revalidation 경로로 새 canonical key를 발급한다.
- review-only `market-recalibration-candidate:*`에는 approval key를 만들지 않는다.
- approval 파일·artifact 외 ScriptableObject, price, saleRate, reward, stock cost는 수정하지 않는다.
- 중간 merge 또는 재검증이 실패하면 원본 approval byte로 전부 복구한다.

복구 뒤 직접 후보 `337`개는 사라지지 않는다. 그 의미가 다음처럼 정확히 갈린다.

| 후보 상태 | 행 수 | 의미 |
|---|---:|---|
| `previous-applied-market-recalibration-review-required` | 162 | 현재값의 적용 권위는 증명됐지만 새 후보로 바꿀지는 별도 판단 |
| `market-authority-provenance-missing` | 175 | 현재 saleRate의 exact 승인 이력이 없고 신규 후보도 미승인 |

#### dependency root metric 교정

collapsed 노동밀도 Critical의 root ID만 보고 같은 stable ID의 첫 approval metric을 선택하던 로직은 `authored-market-sale-rate` 12개를 실제 원인처럼 승격했다. 이는 밸런스 이상이 아니라 귀속 버그다.

- root 승격은 `authored-unit-price-gold → acquisition/cultivated acquisition → direct WU → authored WU → construction material`의 causal allowlist만 사용한다.
- 기존 승인 key를 가진 causal row를 먼저 선택한다.
- saleRate, retail, reward 같은 비원인 시장 표현 행은 fallback root로 사용할 수 없다.
- 현재 12개 item의 이미 승인된 unit-price row가 정확한 review root로 재사용되고, 잘못된 sale-rate CI annotation은 `0`이 됐다.

이 교정으로 unresolved Critical은 `378→366`, collapsed descendant는 `189→201`, approved root는 `35→47`이 됐다. 남은 `366`은 시장 review-only 후보 `337`과 건설 authored 판단 `29`의 합이다. 질량/runtime mismatch 또는 pipeline readiness Critical은 현재 `0`이다.

#### 시장 검토 분할

- 가격·시장 소비자 162건 중 formula-clean/downstream-zero item price `74`건은 한 검토 cohort로 묶을 수 있지만 exact property approval은 행별로 발급한다.
- 나머지 `88`건은 downstream 연결, acquisition warning, quarry 폭포, stock/retail/reward를 dependency bundle로 검토한다.
- saleRate 175건 중 `92`건은 price+rate 원자 묶음, `83`건은 rate 단독 검토 대상이다.
- live sale credit이 acquisition cost 이상인 행과 직접 buy→sell 차익은 각각 `0`이다.
- live 60% 회수 목표를 넘는 행은 `34`개이며 `resource:stone`은 `73.8074%`로 유일하게 70%도 넘는다.
- 후보 sale credit은 `175/175`가 60% 이하이며 파생 행은 계속 독립 승인 불가다.
- quarry/mana family, `resource:mana-crystal 4→172 gold`, `stock:mana 4.200749874→230.984954834`는 후손을 따로 승인하지 않고 하나의 upstream family로 먼저 검토한다.

#### 검증 증거

- [x] Unity compile과 causal-root focused 검증 PASS.
- [x] 기존 적용 custody `162`, 신규 후보 승인 `0`, approval 총 `1,533`, SHA-256 `C896179A221C13902AD118F3C00E94B89AD7707F15D18DFF68B2619CA0D4A891`.
- [x] strict focused 결과: previous-applied 후보 `162`, provenance-missing saleRate `175`, candidate approval/asset violation `0`.
- [x] strict AuditOnly 2회 결과: rows `87,823`, Critical `366`, collapsed `201`, approved `47`, SCC `297`, minimum margin `-23,142,719mEWU`, integrity failure `0`.
- [x] 두 번째 실행에서 CSV·audit·manifest hash·length·mtime 변화 `0`.
  - CSV: `D702D1616318254714B38C783367EDCBD78822E773591EA36650F0C0FB8D5A59`, `68,373,346 bytes`
  - audit: `89F46407A72017A8D97A72096F0495C4CDC168EF38CCFBEC709C23F87C6EF9F`, `78,793 bytes`
  - manifest: `3CB8A93718B5DB6F6938A0863175BF513AF7C4FBA8C5CF8BCB69EB7F1E790817`, `5,583 bytes`
- [x] 최종 Unity Console Warning/Error `0/0`.
- [x] GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 불변.
- [ ] 가격/소비자 162, saleRate 175와 건설 29를 명시적 bundle review로 승인·유지·재설계한다.
- [ ] unresolved Critical `0` 이후에만 ApplyApproved와 256-seed 통합 감사를 실행한다.

이번 체크포인트의 authored kg·BOM·WU·EWU·가격·saleRate·계약·보상·ScriptableObject mutation은 `0`이다. 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.9 시장 검토 묶음의 원자성·계열 귀속 — 2026-08-30

상태: **직접 후보 337행을 품목 anchor 234개로 결정론적 분할 / family wildcard 승인 금지 / 두 번째 생성 무변경 PASS / authored 수치 적용 없음**.

`source:quarry`나 `resource:mana-crystal` 의존성은 검토 순서를 정하는 경제 계열이지 승인 단위가 아니다. 같은 계열의 수백 행을 하나의 `bundleId`로 합치면 원초 자원 하나의 승인으로 무관한 품목·판매율·상점가까지 해제될 수 있으므로 다음 두 식별자를 분리한다.

- `bundleId = market-atomic:{anchorItemId}`: exact property를 함께 검토할 최소 원자 단위.
- `rootFamilyIds`: `source:quarry`, `resource:mana-crystal` 같은 인과 탐색 태그. 승인 키나 wildcard patch로 변환할 수 없다.

item price와 sale rate는 자기 item ID를 anchor로 사용한다. stock·retail·guest reward 후보는 exact dependency item 하나를 anchor로 사용하며, 11행 모두 기존 item bundle에 연결된다. 고아 consumer, 중복 membership, 같은 `(stableId, authorityMetric)`의 이중 귀속은 모두 `0`이어야 한다. derived sale credit은 supporting evidence일 뿐 bundle의 approval member가 아니다.

| 구분 | 수 |
|---|---:|
| 직접 후보 행 | 337 |
| 품목별 원자 bundle | 234 |
| price+rate | 92 |
| rate-only | 83 |
| price-only | 59 |
| 시장 consumer 후보 | 11 |
| 기존 적용 custody | 162 |
| provenance 없는 sale rate | 175 |

member shape는 `G/P/R/S/T` 순서의 canonical token을 사용한다. `P=price`, `R=sale rate`, `S=stock`, `T=retail`, `G=guest reward`다.

| shape | bundle 수 |
|---|---:|
| `PR` | 87 |
| `R` | 80 |
| `P` | 58 |
| `RS` | 3 |
| `PRS` | 2 |
| `PT` | 1 |
| `GPRS` | 1 |
| `GPR` | 1 |
| `GPRT` | 1 |

`formula-clean/downstream-zero` price 74행은 검토 cohort일 뿐 일괄 승인 집합이 아니다. 이 중 sale rate 또는 stock·retail·reward가 같은 anchor에 붙은 경우에는 동일 bundle 안에서 함께 판단하되 exact property 승인은 각각 발급한다. 나머지 price 77행은 acquisition warning, inherited 변화 또는 production downstream 연결을 포함한다.

전용 경제 인과 그래프는 item의 `acquisition-cost`·`cultivated-acquisition-cost`와 recipe `direct-wu`의 물리 입력 edge만 합친다. presentation dependency나 market self-edge는 사용하지 않는다. 이 그래프에서 quarry 계열은 직접 후보 180행, 그중 mana 계열은 84행이다. family는 리뷰 정렬·영향 범위 설명에만 사용하고 승인 전파에는 사용하지 않는다.

검증 증거:

- [x] Unity current-source compile PASS.
- [x] 행 분할 `74/77/92/83/11`, 총 337, bundle 234, shape 분포 exact PASS.
- [x] market consumer orphan `0`, 후보 membership 중복 `0`.
- [x] quarry candidate 180, mana candidate 84.
- [x] live 회수율 60% 초과 품목은 anchor 중복 제거 뒤 34개.
- [x] 모든 decision은 `pending-explicit-review`; asset mutation과 신규 candidate approval은 `0`.
- [x] 모든 행의 dependency fingerprint·source digest·semantic hash와 234개 bundle digest가 비어 있지 않고, 같은 bundle의 digest 불일치가 `0`이다.
- [x] 두 번째 생성에서 CSV·report SHA-256·byte length·UTC write ticks 변화 `0`.
  - CSV: `83BE11C00EDF60B1398C75A2284A525F496FFA1363E7A12F8EF214343FCE5F46`, `293,481 bytes`
  - report: `D26D7CDCB4A4A7334B5C50BF9BB1AF022F9B248BDCB61F2EC1AEFD522361339A`, `650 bytes`
- [x] Unity Console Warning/Error `0/0`.
- [x] GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 불변.
- [ ] 각 bundle을 `retain-current / promote-candidate / rework` 중 하나로 명시 결정하고 exact approval transaction을 생성한다.
- [ ] 시장 337과 건설 29의 unresolved Critical을 0으로 만든 뒤에만 ApplyApproved와 256-seed를 진행한다.

권위 있는 결정 파일은 생성 artifact와 분리한 `docs/game-design/v27-balance-market-review-decisions.json`으로 둔다. generator는 이 파일을 만들거나 덮어쓰지 않는다. 각 결정은 `schemaVersion`, `bundleId`, `bundleDigest`, `anchorItemId`, `decisionReason`, `reviewedBaselineRecordId`와 모든 member의 `stableId`, `authorityMetric`, `sourcePropertyPath`, `beforeExactToken`, `candidateExactToken`, `dependencyFingerprint`, `sourceDigest`, `semanticHash`, `decision`을 포함한다.

- `retain-current`: 현재 asset token을 새 canonical baseline receipt로 고정한다. 후보값을 승인한 것으로 취급하지 않는다.
- `promote-candidate`: candidate exact token을 같은 transaction에서 asset patch와 canonical approval로 승격한다.
- `rework`: candidate를 적용하지 않고 reviewer가 명시한 replacement exact token과 reason을 새 proposal로 만든다. 이 proposal도 재감사 전에는 승인·적용하지 않는다.
- 한 bundle 안에서 member별 결정은 다를 수 있지만 검증과 적용은 bundle transaction 하나로 원자적으로 수행한다.
- 현재 ledger의 bundle digest 또는 member identity 중 하나라도 다르면 `MARKET_REVIEW_DECISION_STALE`로 전체 bundle을 거부한다.
- 누락 member, 중복 member, extra member, family wildcard, derived sale-credit member는 fail-loud다.

현재 전수 감사 한 번은 약 3~4분이 걸린다. 검토 bundle serializer의 결정론 반복 시험은 향후 이미 생성된 `FrozenBalanceLedger`를 입력으로 받는 전용 경로를 추가해 EWU 전수 재계산과 분리한다. 이 최적화는 결과 의미나 승인 권위를 바꾸지 않는다.

이번 체크포인트의 authored kg·BOM·WU·EWU·가격·saleRate·계약·보상·ScriptableObject mutation은 `0`이다. 126.10 교정 뒤 unresolved Critical은 시장 337개만 남고 integrity failure는 `0`이며 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.10 건설 재조정의 현재 승인 권위 우선과 밀도 경계 열거 — 2026-08-30

상태: **건설 Critical 29→0 / current approved BOM·WU 보존 / 진짜 불가능 fixture 차단 유지 / authored mutation 없음**.

기존 optimizer는 historical Before BOM만 기준으로 `changedRows`를 최소화했고, WU 후보도 minimum·period·target-nearest±1만 평가했다. 그 결과 이미 승인된 재료를 다시 빼면서 WU를 올리는 27개 rebase 후보와, 실제로는 허용 밀도 경계 안에 있는 2개 거짓 Critical이 발생했다.

교정 계약:

- historical BOM은 50% 수량 상한과 Before 투자 권위로 계속 사용한다.
- current approved BOM·WU는 별도 preferred authority로 입력한다.
- 후보 변경 수는 historical Before가 아니라 current approved property와 비교한다.
- Critical과 non-Critical이 경쟁하면 non-Critical을 우선하지만, Normal/Warning 안에서는 current property 변경 수를 먼저 최소화한다.
- WU 후보에 current approved WU와 density ratio `0.67/0.80/1.00/1.25/1.50`의 floor·ceil 인접 정수를 포함한다.
- 재료 지배 시설도 period WU로 조기 반환하지 않고 동일 후보 비교를 거친다.
- current 값이 투자 `±2%`, WU `1.5~2.25`, 밀도 Warning 상한 안에 있으면 새 patch를 만들지 않는다.

결과:

- 기존 건설 review-only 후보 27행과 density-unresolved 2행이 모두 제거됐다.
- 이전 승인 BOM 종류·수량과 WU는 변경하지 않았다.
- `building:9303`, `building:9502`는 경계 후보 누락이 해소되어 live audit Critical이 아니다.
- 진짜로 불가능한 합성 fixture는 `CriticalDensityUnresolved`, WU 797, lumber 9, density ratio `1.8242974074`, 투자 오차 853mEWU로 계속 차단된다.

검증 증거:

- [x] Unity current-source compile PASS.
- [x] focused impossible-density guard PASS.
- [x] strict AuditOnly rows `87,796`, Critical `337`, integrity failure `0`; 건설 후보 `0`.
- [x] 두 번째 실행에서 full CSV·audit·market CSV·market report hash·length·mtime 변화 `0`.
  - full CSV: `25507F2CCABC8F163A1D35462BAAD61481C7C0C710A57706FA1DB2CC00BEF45B`, `68,336,356 bytes`
  - audit: `1AA486FF6D072F927E9CF2226CCA7CC994F7BC8620F363D15A01159286D661A0`, `72,767 bytes`
  - market CSV: `83BE11C00EDF60B1398C75A2284A525F496FFA1363E7A12F8EF214343FCE5F46`, `293,481 bytes`
  - market report: `D26D7CDCB4A4A7334B5C50BF9BB1AF022F9B248BDCB61F2EC1AEFD522361339A`, `650 bytes`
- [x] GameplayScene SHA-256 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8` 불변.

건설 29개를 승인해서 경고를 숨긴 것이 아니라, 이미 승인된 현재값을 후보 생성기가 다시 존중하도록 계산 구조를 고쳤다. 남은 unresolved Critical 337개는 모두 시장 review-only 후보이며, 전체 밸런스 완료는 아니다. 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.11 시장 bundle 기계 추천과 무상 외부 유입 차단 — 2026-08-30

상태: **추천 178 promote / mana 무상유입 46 rework / 비-mana 2배 충격 10 rework / 실제 decision 234개 전부 pending / asset mutation 없음**.

추천은 승인 결정이 아니다. 각 bundle의 exact identity를 유지한 채 다음 기계 규칙으로 검토 순서를 제안한다.

1. 경제 인과 경로가 `resource:mana-crystal`을 포함하면 `rework-mana-unpriced-inflow`.
2. 그 외 품목에서 candidate sale credit이 live sale credit의 2배 이상이면 `rework-sale-credit-double`.
3. 나머지는 `promote-candidate` 추천.

결과는 bundle 기준 `178 / 46 / 10`이다. 추천 이유는 모든 행에 기록하지만 `decision`은 계속 `pending-explicit-review`이고, 추천만으로 approval key·asset patch를 만들 수 없다.

mana 계열 보류 원인은 EWU quarry 배분 오류가 아니다. quarry의 expected output과 weighted share는 batch debit exact 보존과 `stone < coal < iron < gold < mana` 희소성 순서를 통과한다. 실제 위험은 세력 경로가 Trade/Supply cargo를 결제 없이 물리 지급하는 데 있다.

- demon trade: mana 6개 / 23일
- golem trade: mana 4개 / 27일
- golem supply: mana 8개 / 99일
- demon supply: mana-awakener 6개 / 84일

후보 판매가 적용 시 이 무상 화물을 현금화하면 약 `60.20 gold/일`이며 현재 약 `2.69 gold/일`의 22.3배다. 따라서 mana 가격을 인위적으로 낮춰 숨기지 않고, faction Trade의 실제 결제 또는 Supply의 obligation/resale/value-cap 중 하나를 generic route economic policy로 먼저 연결한 뒤 mana 46 bundle을 재생성한다. content ID별 예외 분기는 금지한다.

추가로 `tool:rune-identification-lens`, `medical:cross-lineage-medium`, `drug:mana-awakener`는 producer 외 명시 물리 소비처가 없거나 무상 supply만 있으므로 동일 rework 경계 안에 남긴다. gold ore의 수학적 후보는 타당하지만 gold ingot/leaf 전이 가격과 exact multi-bundle transaction으로 적용해야 한다.

검증 증거:

- [x] 추천 누락·빈 이유 `0`, actual decision 변경 `0`.
- [x] recommendation bundle count `178/46/10` exact.
- [x] 추천 규칙은 인과 family와 credit ratio 기반이며 신규 품목 ID switch가 없다.
- [x] v4 artifact 2회 hash·length·mtime 변화 `0`.
  - CSV: `83BE11C00EDF60B1398C75A2284A525F496FFA1363E7A12F8EF214343FCE5F46`, `293,481 bytes`
  - report: `D26D7CDCB4A4A7334B5C50BF9BB1AF022F9B248BDCB61F2EC1AEFD522361339A`, `650 bytes`
- [x] Unity compile, Console Warning/Error `0/0`, GameplayScene hash 불변.
- [ ] 추천 178개를 실제 `promote-candidate` 결정으로 승격할지 정책 승인한다.
- [ ] mana 46개는 faction route economic policy를 구현·검증한 뒤 재분류한다.
- [ ] 비-mana 2배 충격 10개는 availability·sink·transitive price를 검토해 promote 또는 rework replacement를 확정한다.

남은 Critical은 시장 337개이며 전체 밸런스 완료가 아니다. 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### 126.12 시장 안전 후보 적용과 세력 경제 폐쇄 세부 체크포인트 — 2026-08-31

상태: **시장 bundle `178/234` 적용 / 직접 property `238`개 적용 / unresolved Critical `337→99` / 세력 경제 폐쇄 `5/7` / 전체 배치 완료 수 `1/8`**.

`A–H 1/8`은 배치가 exit gate까지 완전히 닫힌 개수만 세는 원자 지표다. 따라서 Batch H의 일부 작업이 크게 진행돼도 H 전체의 6인 생존·공간·paired run·layout·최종 seed가 남아 있으면 `2/8`로 올리지 않는다. 하지만 이 숫자만 진행률로 보고하면 실제 감소량을 숨기므로, 이후 모든 보고는 아래 세부 checkpoint를 함께 제시한다.

#### 시장 검토·적용

- [x] 234개 atomic bundle의 exact identity와 recommendation을 현재 ledger에서 재검증했다.
- [x] formula·source·dependency 검토를 통과한 `178`개 bundle을 `promote-candidate` 결정으로 승격했다.
- [x] `183`개 ScriptableObject의 exact property `238`개만 변경하고 두 번째 Apply에서 변경 `0`, SaveAssets `0`을 확인했다.
- [x] 적용 뒤 strict AuditOnly는 `87,438`행, integrity failure `0`, SCC `297`, minimum margin `-23,142,719mEWU`를 유지했다.
- [x] unresolved Critical은 `337→99`, collapsed descendant는 `201`, approved root는 `47`이다.
- [ ] 남은 `56/234` bundle을 재설계한다. 구성은 `mana-unpriced-inflow 46`, `sale-credit-double 10`이며 이들이 직접·파생 Critical `99`개를 소유한다.
- [ ] 세력 Trade/Supply 경제 폐쇄 뒤 56개 bundle을 재생성·검토하고 unresolved Critical `0`을 만든다.

시장 checkpoint:

```text
bundleDecisionApplied = 178 / 234
bundleReworkRemaining = 56 / 234
authoredPropertyApplied = 238
unresolvedCritical = 99
```

#### 세력 경로 경제 폐쇄

다음 7개 gate를 독립 분모로 사용한다. 부분 구현을 `Batch H 완료`로 세지 않되, 실제 완료 행은 다시 0으로 되돌리지 않는다.

- [x] `1/7` Trade/Supply economic-policy descriptor를 6개 실제 Faction authority에 명시하고 capability registry로 등록한다.
- [x] `2/7` 실제 cargo·authored price에서 canonical deterministic quote와 source digest를 생성한다.
- [x] `3/7` stable source 기반 exact-once debit과 충돌·부족·ledger 예외 rollback을 증명한다.
- [x] `4/7` Trade route request가 validation 뒤 exact debit을 commit하고 route publication 실패 시 exact refund/pending recovery를 수행한다. 미해결 환불은 Tick에서 forward retry하고 direct/central capture·reset·restore publication을 모두 차단하며, route list publication 실패는 route sequence를 증가시키지 않는다.
- [x] `5/7` route V4 settlement receipt와 sequence를 current-format save/restore에서 exact 검증한다. receipt는 item·quantity·과거 unit price의 frozen line을 저장하고 strict restore가 authored total/source digest/quote digest를 자체 재계산하므로 현재 SO 가격이나 bounded Treasury ledger에 영구 join하지 않는다.
- [ ] `6/7` 도착 cargo whole-vector를 `Ready → Publishing → Delivered` exact-source publication으로 게시한다.
- [ ] `7/7` Supply 전역 alliance-benefit EWU budget을 승인된 source digest·refill/capacity·reservation과 결속한다.

현재 6개 Supply의 exact 전수 후보는 refill `1079184126313/1843380 mEWU/day` (`585.437688546583 EWU/day`)와 같은 날 6개 route 한 bundle을 모두 수용하는 capacity `39,142,546mEWU`다. 이 값은 route 목록을 런타임에서 재합산해 자동 확대하지 않는다. 현재 6-route source digest에 결속된 review authority로 두고 faction/cargo/cooldown 변화 시 stale로 실패시켜 전역 혜택 예산이 콘텐츠 수에 따라 무한 선형 증가하지 않게 한다.

Supply 예산 source authority의 canonical byte 계약은 다음으로 고정한다. 첫 행은 `schema|faction-alliance-benefit-budget-source|1`, route 행은 faction ID ordinal 순서의 `route|factionId|cooldownDays|debitMilliEwu`, item 행은 각 route 내부 item ID ordinal 순서의 `item|factionId|itemId|quantity|acquisitionMilliEwu|reviewStatus|anomalyDisposition|sourceDigest|semanticHash`, 마지막 두 행은 `capacity-mewu|39142546`와 `daily-refill-mewu-rational|1079184126313|1843380`이다. 인코딩은 UTF-8 without BOM, 줄바꿈은 LF, 마지막 trailing LF는 금지한다. 현재 18개 cargo occurrence/15개 unique acquisition row를 이 규칙으로 직렬화한 승인 대기 source digest는 `c539c892bb0b8355801c923c3a86da8f2a331ed459414684aa2dc60d0767fe15`다. 전체 CSV byte hash는 unrelated row 변경까지 예산을 stale시키므로 source authority에는 넣지 않고 증거로만 보존한다. `drug:mana-awakener`, `resource:mana-crystal`의 pending inherited review와 `material:blacksteel-ingot` warning이 해소되기 전에는 이 digest를 최종 밸런스 승인으로 해석하지 않으며, 구현·악용 방지 검증용 frozen review authority로만 사용한다.

`4/7~5/7` focused evidence: `FactionTradeSettlementRecovery`가 두 번의 injected credit 실패를 pending으로 보존하고 capture를 차단한 뒤 세 번째 forward retry에서 exact `17 gold`를 한 번만 복구했으며, successful publication 경로는 refund `0`을 유지했다. Faction V4 fixture는 frozen unit price와 64-char digest의 valid-looking tamper를 모두 live mutation 없이 거부했다. `FACTION_ROUTE_ECONOMIC_POLICY_PASS`, `FACTION_TRADE_V4_RECOVERY_PASS`, Unity compile PASS, Console Warning/Error `0/0`이다.

#### A–H 전체 진행 대시보드와 보고 규칙

이전 보고는 분모가 이미 있던 A–C만 반복하고 D–H의 실제 작업을 서술형으로 숨겼다. 이후에는 A–H 전체를 아래처럼 함께 보고한다. 서로 다른 의미의 분모를 억지로 더해 단일 퍼센트로 만들지 않는다.

| Batch | 현재 checkpoint | 남은 핵심 |
|---|---|---|
| A exact output | `31/31`, 완료 | current-source 회귀 유지 |
| B output capacity | `36/40` | retarget transaction, support/p95 `>4 cycle`, unified mutation fence, active multi-facility retarget |
| C input/entity P0 | `8/39` input owners | remaining `31`, full stored-destination coverage |
| D mass proposal | item coverage `414/414`, canonical semantic `363/414`, recipe role/contract closed `94/355` (`Source 23 + Sink 4 + reviewed exact Transform 67`), proposed `7/7 blocked` | semantic `51`, open Transform contract `261`, warehouse/buffer·EWU/price impact capture |
| E anomaly/apply | market bundle `178/234` 적용, changed-only/no-op apply PASS | market rework `56`, mass semantic decision `51`, 최종 duplicate writer/second-build gate |
| F domain closure | output owner `10/10`, input owner `8/39`; cluster별 부분 증거 다수 | 6개 cluster의 producer→consumer+terminal fault+restore 통합 분모를 fresh manifest로 생성하고 0 gap까지 폐쇄 |
| G live fault | sawmill/M06/crop 및 공용 output fault 증거 PASS | input/entity까지 포함한 whole/partial/cancel/Dead/drop-failure/Floor-Clutter/RNG 행렬의 fresh 통합 분모와 남은 행 |
| H final balance | integrity failure `0`, SCC `297`, market Critical `337→99`, faction economy `5/7` | exact cargo publication, Supply budget, Critical `99→0`, kg·생존·공간·paired/layout·3 final seeds와 최종 no-op 재실행 |

오늘 실제로 닫은 세부 checkpoint는 다섯 개다.

1. 시장 안전 후보 `178/234` bundle의 exact 결정·적용.
2. strict audit Critical `337→99`, integrity failure `0` 유지.
3. 6개 실제 Faction의 versioned economic-policy descriptor authoring.
4. 실제 cargo의 canonical deterministic quote와 source digest.
5. exact-once debit의 replay/conflict/insufficient/ledger-exception rollback.

`A–H complete 1/8`은 여덟 batch 중 exit gate 전체가 닫힌 수만 나타내는 release 지표다. 하루 진행률로 사용하지 않는다. 이후 checkpoint 보고는 최소한 다음 여덟 줄을 포함한다.

```text
A 31/31
B 36/40
C 8/39
D items 414/414; semantics 363/414; recipe contracts 94/355
E market 178/234 applied; 56 remaining
F output 10/10; input 8/39; integrated cluster denominator pending
G output fault evidence green; consolidated input/entity matrix pending
H Critical 99; faction economy 5/7; exact cargo publication, Supply budget and final simulations pending
```

F와 G의 통합 분모가 아직 없다는 사실 자체를 OPEN 결함으로 기록한다. 다음 구조 작업 전에 source-derived F cluster manifest와 G fault-row manifest를 생성해 이후에는 이 두 Batch도 정량적으로 증감하게 한다. 실제 세부 진척이 있는 턴을 `진척 없음`으로 보고하지 않는다.
