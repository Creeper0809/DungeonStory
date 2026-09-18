# Service room runtime review

## Authored population

- Service hubs: 8 assets.
  - Dining: D04 배식카운터, base1, all three modes, direct price6/satisfaction42, managed requires reception+queue, automated additionally heated-serving+auto-order.
  - Bathing: H03 세면대 and H04 목욕통, each base1, all modes, direct price5/satisfaction44, managed requires bath-reception+bath-hygiene, automated additionally auto-water-control.
  - Lodging: R01 간이침대, R02 정식침대, R03 이층침대, each base1, all modes, direct price8/satisfaction46, managed requires lodging-reception+room-cleanup, automated additionally auto-room-assignment.
  - Retail: S01 판매카운터, base1, all modes, direct price4/satisfaction40, managed requires staffed-checkout+display, automated additionally auto-checkout.
  - Medical: M01 응급처치대, base1, all modes, direct price10/satisfaction48, managed requires medical-triage+medical-call, automated additionally queue.
- Service supports: 20 assets: D07/D08/D09, S02, and SR01–SR16.
- Their public facility IDs are D07=1006, D08=1007, D09=1008, S02=1013, and SR01–SR16=1700–1715.
- Service processes: dining meal, bathing wash, lodging rest, retail sale, and medical treat.

## Confirmed link and mode behavior

- A valid support links to one compatible hub on the same Grid and the same resolved room. If multiple hubs qualify, it selects the nearest Manhattan distance and then stable building key.
- Direct mode uses the hub base values and does not require support features.
- Managed mode requires all managed feature tags. Automated mode requires both managed and automated tags.
- A required powered support suspends the mode when unpowered.
- In non-direct modes every linked support contributes: capacities add, speed multipliers multiply, revenue modifiers add, and satisfaction modifiers add.
- A mode switch also requires the authored research for category/mode. Direct has no research gate.
- Managed research gates are Dining/Retail=`research:service-flow`, Lodging=`research:hospitality-operations`, Bathing=`research:bath-business`, and Medical=`research:medical-reception`. Automated mode for all five categories requires `research:service-automation`.
- Runtime “operational” currently means a non-destroyed building with data; hub direct operation does not itself require a usable enclosed room.

## Session and persistence behavior

- Dining, retail, facility-use, and survival medical entry points create service sessions through the same runtime.
- Session creation validates supported process/category/hub tag and the selected mode contract, freezes capacity/timing/price/satisfaction/payment/support IDs, and enforces the active-session capacity.
- Completion posts guest-service income with an idempotent economic command. Hub destruction cancels its sessions.
- Hub modes, advertised categories, active sessions, frozen contracts, stages, and timing participate in save/restore with detached building/character validation.
- `ServiceRoomBuildingPanelPresenter` shows support links, mode controls, capacity/active/wait, revenue/satisfaction, required features, blockers, and active sessions.
- Payment policy is not display-only: session creation computes `paymentRequired` from the process policy and exempts internal staff when the policy is `InternalStaffFree` (as authored on all five processes).
- The panel labels the three modes as 간이 운영/관리형/자동화, shows the next upgrade's missing support features, and applies a successful mode change only to new guests. A support facility panel identifies its same-room linked hub or reports that no compatible hub exists.

## Process contracts

- All five processes define Direct/Managed/Automated contracts. Direct has Service+Payment stages, zero added stage delays, and the hub's base price/satisfaction. Managed has all five stages with reception0.5s, wait0.5s, payment0.25s, cleanup0.5s; price is direct+2 and satisfaction direct+10. Automated has Waiting+Service+Payment+Cleanup with wait0.25s, payment0.1s, cleanup0.35s; price is direct+3 and satisfaction direct+8.
- Process values by category are dining 6/8/9 and 42/52/50, retail 4/6/7 and 40/50/48, lodging 8/10/11 and 46/56/54, bathing 5/7/8 and 44/54/52, medical 10/12/13 and 48/58/56.
- Mode `serviceSeconds` is authored as zero, so runtime falls back to the hub facility use duration and divides it by the multiplied support speed.
- Every process uses `InternalStaffFree`. Dining and retail service-contract price is forced to zero because their existing item-sale path owns payment; lodging/bathing/medical post the service price on completion.
- Bathing alone authors clean water0.45, wastewater0.45, manual fallback allowed. The initial consumer search found these process fields only in the definition/asset, not in the frozen service contract.

## Support modifier labels

- Modifier enum values are Stage0, Capacity1, WorkSpeed2, Satisfaction3, Revenue4, Security5, Cleanup6.
- Runtime aggregation currently does not branch on modifier type: every linked support contributes every nonzero authored capacity/speed/revenue/satisfaction field.

## Focused consumer boundary

- The first broad exact-reference command produced truncated output and is not evidence for this review. Two smaller production-only searches replaced it.
- `BuildingServiceSupportAbility.modifierType`, `cleanWaterPerUse`, `wastewaterPerUse`, and `allowsManualWaterFallback` have no non-Editor consumer outside their declarations. Same-named fields in production recipes and industrial water-fixture abilities do have consumers, but those are different ability types and do not activate service-support metadata.
- `ServiceProcessSO.requiredFeatureTags` is traversed by asset validation, while live mode gating uses the hub contract's managed/automated feature arrays. The five authored process assets currently have empty process-level feature arrays.
- `ServiceProcessSO` exposes bathing water/fallback values, but the focused reference search found no service-session runtime consumer. Industrial `WaterFixtureUseRuntime` and `ProcessFluidUseRuntime` consume different industrial ability records, so they are not evidence that the service-process values are committed.
- `requiresCleanup` has no production member-access consumer in the focused search; the active-stage mask and its Cleanup stage already control the observed service-session cleanup timing.

## Evidence quality notes

- The combined wiki-path/research search was truncated and is excluded; final evidence comes from exact guide, entity, presenter, rules, and caller files.
- The public guest/service guide describes the broad reception→waiting→use→payment→completion/cancel and save/physical-item flow, but does not document Direct/Managed/Automated modes, required support combinations, exact capacity/speed/revenue/satisfaction values, or research gates.
- A serialized-type asset census returned no rows because the expected managed-reference type string is not present in those YAML files; that empty output is not used as population evidence.
- All 28 public facility entities exist. Every page has exactly two generic facts (`분류`, `크기`) and zero relations; summaries expose only broad roles/build costs (including `서비스 보조` and power consumption where applicable), not the hub/support compatibility, feature tags, capacity, speed, revenue, satisfaction, power gate, or same-room linking behavior.
- The public IDs were checked directly: hubs 1003/1059/1060/1020/1021/1022/1012/9501, legacy supports 1006/1007/1008/1013, and SR01–SR16 1700–1715. No entity file was missing.
- Initial register keyword matches only exposed SR05/SR06/SR15/SR16 as source links inside an existing industrial-infrastructure entry, not a service-mode/support owner. Because source-link matches dominate that output, owner/title fields must be checked structurally before declaring no duplicate.
- Structured search across every existing GAP title/owner/category/required-documentation found zero service-hub, service-support, service-room, or three-mode owners. GAP-166/168 cover need/expedition effects of some overlapping hub buildings, but not service sessions or support linking.
- A WIM/DIFF search passed Windows wildcard paths directly to `rg` and failed with OS error 123. It is discarded and will be replaced with explicit files/directories.
- The corrected explicit WIM/DIFF search found WIM-031/DIFF-031 already owning the general mismatch where money is deducted immediately instead of waiting for physical vault access/transport. Do not duplicate that payment mismatch in the service GAP.
- `economy-and-trade.md` gives only high-level service margin targets (premium service net margin 25%, target band 20–35%) and the already-owned vault/payment claim; it does not document service-room modes, contracts, support links, or authored values.
- The guest/service guide's separate statement that dining/retail/medical/performance items physically reach the service location did not match an explicit WIM entry in this narrow search. Its runtime truth needs a category-specific consumer check before treating it as a new difference.
- Existing WIM-033 is faction-contract delivery logistics and WIM-037 is social-event movement/transport/work; neither owns the guest-service item-delivery claim.
- Exact service-session callers are `SurvivalFoodRuntime` for dining, `ShopCustomerInteractionService` for retail, and generic `Facility` for other service hubs. Performance is not a `ServiceCategory` in this runtime. The caller bodies must be read to determine whether the guide's item-delivery statement is true per category.
- Caller-body read: medical treatment begins a service session and then calls `stockRuntime.TryConsumeTreatmentMaterial`; whether that stock operation proves delivery to the medical hub remains unverified. Shop dining explicitly branches to `RunPhysicalMealService`. Generic facility use begins an industrial water-fixture ticket when the building has that separate ability.
- The medical stock method directly commits a physical `Sink` disposition against one medicine stack (or biological substitute) selected by the survival stock authority. It does not receive the medical hub/session as a destination argument. This proves physical item consumption but not the guide's promised delivery to the service location; inspect its source filter once more before registering a difference.
- The source filter is now confirmed: treatment chooses a `Stored` stack whose source/destination is any registered warehouse on the current grid, orders deterministically by item/stack ID, and sinks it in place. There is no reservation, carrying/delivery order, medical-facility buffer, or destination-arrival check. This is a direct mismatch with the public guide's claim that medical supplies are moved to the service location before consumption.
- Existing WIM/DIFF entries about medical paths do not own this logistics mismatch: WIM-027 concerns rescue-vs-living-treatment documentation, WIM-053 expedition result detail, and WIM-054 captivity health integration. Register a separate partial WIM/DIFF because the combined public claim is implemented for at least some other categories but false for medical treatment.
- Dining's `RunPhysicalMealService` moves the actor to the facility and delegates physical meal consumption to the consumables runtime. Retail selects from the shop's own purchasable stock and moves the actor to checkout; the remaining commit portion has not yet been read.
- The first performance/item search returned 61k-token broad output and was truncated. It is excluded; circus performance supply has dedicated named runtime files and must be checked directly if this guide sentence remains in scope.

## Ownership decisions

- GAP-172 owns the eight hub facilities and five service-process contracts: category/mode availability, mode research, stage timing, capacity, price/satisfaction, payment policy, session lifecycle, save/restore, and panel controls. Overlapping need/expedition recovery remains with GAP-166/168.
- GAP-173 owns the twenty support facilities: same-grid/same-room deterministic link selection, category/feature compatibility, Direct-vs-Managed-vs-Automated requirements, additive/multiplicative modifiers, power suspension, exact authored support matrix, and support-link UI.
- Authored support `modifierType` and support/process water metadata without service-runtime consumers are implementation-orphan boundaries, not user-facing effects to document. The industrial water-fixture path remains a separate ability/runtime.
- DIFF-047/WIM-062 owns the public medical-supply logistics mismatch: treatment consumes a stored warehouse stack in place instead of delivering it to the medical service location. It is classified `partial` because the guide's combined statement covers several service categories and at least dining has a physical service path.
- No game code, ScriptableObject, or public wiki content was changed; this is a static source audit and no Play Mode scenario was run.

## 2026-09-10 사용자 승인 및 원격 재고 소비 재조사

현재 사용자 결정: WIM-062는 의료시설로 약품을 실제 배송하고 해당 시설의 도착분만 소비한다. WIM-063은 현행 참조를 전환한 뒤 사용 중단 시설을 제거한다. 구현 체크는 `docs/game-design/wim-implementation-plan.md` 7.1/7.2절이 소유하며, 아래는 **현재 소스 조사**이지 실행 PASS나 수정 완료가 아니다.

### 발견 원인과 증거 기준

`TryCommitPhysicalDisposition(Sink)`는 물리 수량과 소비 사유를 기록하지만, 소비자가 그 물품 위치에 도착했거나 시설로 배송됐음을 보장하지 않는다. `SurvivalFoodStockRuntime.WithdrawStock`은 같은 Grid의 등록 창고 `Stored` 스택을 골라 즉시 차감한다. 호출 인자에 소비 시설/도착 주문이 없다. 코드에 물리 영수증이 있다는 사실을 물류 완료 증거로 사용할 수 없다.

이 경계가 일반 치료 외에도 재사용된다. 심지어 이 helper는 연료/음식에도 `survival-treatment-material-consumed`라는 같은 사유를 기록한다. 사유 문자열만 검색해 의료에만 국한된 API로 분류해서는 안 된다. 정확한 도입 의도/누락 시점은 이번 조사에서 Git 이력으로 확정하지 않았다. 기존 원장에서는062가 미승인 별도 후보였지만, 그것만으로 다른 경로의 물류 완성을 주장할 근거가 되지 않는다.

### 메인 조사: 생존·의료

| 경로 | 현재 확인한 소스·호출 | 판정 |
|---|---|---|
| 일반 의료 생활서비스 | `Services/Survival/SurvivalFoodRuntime.cs:1181` → `SurvivalFoodStockRuntime.TryConsumeTreatmentMaterial:118` → `WithdrawStock:74`. 창고 Medicine1, 없으면 기존 Biological1 대체; 소비 후 질환/HP 변경 | **원격 차감 확정**.062 배송 전환 대상. 시설 service session 생성은 약품 도착 증거가 아님 |
| 해독 치료 | `Models/Survival/Core/CharacterConsumablesRuntime.cs:1325,2699`에서 전역 Stored 약품 선택 → `Services/Survival/CharacterConsumablesApplicationAdapters.cs:989`에서 Stored 확인 후 pending Sink → `TryFinalizeDetoxTreatmentPlan:1914` 효과/회수 ack | **원격 차감 확정**.062에 함께 포함. `DeliveryPending` 이름과 달리 물류 주문/운반이 아니라 물리 소비·효과·회수 영수증 처리 대기 |
| 생존 시설 수동 연료 보충 | `Services/Survival/SurvivalFacilityWorkRules.cs:42` → `WithdrawStock(Fuel)`. `SurvivalWorkExecutionHandler` Refuel → 완료 dispatcher → `SurvivalBuildingAbilityHandler:29`의 생산 호출 경로 존재 | **원격 차감 확정**. 시설별 연료 버퍼가 아니라 전역 당일 연료 서비스 상태를 갱신. 다른 도메인 수정 승인/연료 서비스 의미는 별도 범위로 유지 |
| 일일 생존 연료 정산 | `SurvivalFoodRuntime.ProcessDailySurvival:224` → `ConsumeDailyFuel:770` → 창고 Fuel 차감. `OperatingDayStartedEvent` 구독/호출 존재 | **전역 일일 비용 경로 확정**. 시설 보급과 동일시하지 않음. 시설 버퍼 연료로 통일할 때 중복 청구 여부와 기존 난방/야간 위험 소비자를 함께 정리해야 함 |
| 구형 survival 조리 | `SurvivalFoodRuntime.TryApplyCook:912`는 Food/Fuel 창고 차감 후 결과 생성. `SurvivalWorkExecutionHandler:204`는 production bill 우선, 특정 lane 실패는 수동 fallback 차단; 나머지는 survival 완료 handler로 이어짐 | **원격 차감 코드 및 조건부 생산 연결 확인**. 정상 bill 생산 전부가 이렇다는 뜻은 아님. D01/D02에는 Cooking/Fuel ability, D03에는 workstation도 있으므로 시설별 선택 조건을 구분해 후속 처리 |
| 일일 물 집계 | `SurvivalFoodRuntime.ConsumeDailyWater:754`는 재고 전망/대시보드 상태 계산만 수행; 직접 Withdraw 없음 | **원격 물 소비로 세지 않음**. 메서드 이름/`consumed` 지역변수만으로 물리 차감 판정 금지 |
| 구조 의료 공급 | `Services/Combat/CharacterMedicalRuntime.cs:824` → `CharacterMedicalSupplyCoordinator.EnsureTreatmentSupplyReady`; coordinator:219/258 배송 요청,350 `FacilityBuffer`+일치 destination+exact item 검사 | 배송/도착 경계 존재.062 때문에 전체 구조 의료를 다시 설계하지 않음. 이번 실행 재검증 없음 |
| 수술 공급 | `Services/Medical/SurgeryLogisticsRuntime.cs:371`의 `AreRequiredMaterialsReady`는 필수 재료/선택 부품을 주문 destination의 `FacilityBuffer`에서 확인 | 배송/도착 경계 존재. 수술과 생활서비스를 한 경로라고 보고했던 가정은 사용하지 않음. 이번 실행 재검증 없음 |

`WorkTaskExecutor.cs:895`는 완료 효과가 선행 처리되지 않은 작업에만 `ModularFacilityRuntimeEffects.ApplyWorkCompleted`를 호출한다. 따라서 정상 bill 경로의 효과 적용과 구형 survival fallback 소비를 중복해 실행된다고 단정하지 않는다. 일반 치료·해독은 실제 운영 handler의 호출을 확인했으며 단순 private 메서드 존재만 센 것이 아니다.

### 독립 조사: 다른 도메인의 확정 경로와 후보

Terra/xhigh 읽기 전용 조사1개를 병렬 배정했다. 생존/의료는 메인, 아래 도메인은 작업자가 소유했으며 코드/문서는 작업자가 수정하지 않았다. READY 반환 후 메인은 핵심 source selector와 실제 소비/변환의 수정 필요 경계를1회 검토했다. 전체 로그·전체 원장 재조사나 Unity 실행은 하지 않았다.

경로는 모두 `Assets/Scripts/` 기준이다. 아래의 **최소 수정면은 조사 제안**이며062 하나를 다른 모든 기능 수정으로 확장한 승인이 아니다. 기존 승인 WIM과 겹치면 그 항목의 준비/배송 체크에 연결하고 새 기능으로 중복 계수하지 않는다.

| 경로 | 원본·라이브 연결 | 판정 / 최소 수정면 |
|---|---|---|
| 야생동물 도축 | `Services/Wildlife/WildlifeCarcassService.cs:351,401,542`의 `TryButcherNextCarcass`→`TryTransformWholeStack`; selector는 Stored/Loose/FacilityBuffer 허용하며 Stored 우선. `ButcherWorkExecutionHandler`→`ButcherBuildingAbilityHandler:25` 완료 연결 | **원격 변환 확정**. 창고 사체를 삭제하고 도축 시설 중심에 산출. 목적지 버퍼 또는 의도된 실제 현장 사체의 위치 도달을 증명한 뒤 transform해야 함. 소스에 Stored 금지만 붙여 다른 시설의 버퍼를 허용하지 않음 |
| 포로 재사회화 식량 | `Services/Captivity/CaptivityRuntime.cs:1230,1271`의 `AdvanceRehabilitation`→`TryConsumeStoredStock(Food)`; `CaptivityWorkExecutionUnityAdapters`의 Warden 작업 연결 | **원격 소비 확정**. 주거시설/교도 담당자 상태와 음식 도착은 별개. 기존 대상 시설의 준비 배송/버퍼 소비로 연결할 수정면 |
| 장례·축제·상담 | `Services/Character/FuneralFestivalRuntime.cs:348,464,561,610` atomic consume; `TryReserve:652`는 전역 스택을 상태/목적지 제한 없이 선택. `Services/Run/V20ContentResolutionService.cs:670` 및 `Views/UI/Core/EventAlertRuntime.cs:177` 알림 명령 연결 | **원격 소비 가능 경로 확정**. `ItemReservationPurpose.FacilityBuffer` 표기는 물리 상태/도착이 아님. 기존037/040/041의 준비·장소·배송 승인과 중복 여부를 묶어 처리 |
| 번식 서비스 재료 | `Services/Character/ReproductionCommandRuntime.cs:312,358,461`의 시작→전체 stock 예약→atomic consume; `reproduction-start` alert dispatcher 연결 | **원격 소비 가능 경로 확정**. 지원 시설 존재만 확인하는 것과 재료 도착은 별개. 기존 번식 서비스의 해당 시설 input buffer 준비/도착 경계가 수정면 |
| 골렘 충전 재료 | `Services/Character/SpeciesRuntime.cs:436,460,494,526`의 `TryBeginRecharge` 전체 stock 예약→실제 충전 작업 후 atomic consume; `DungeonCharacterRegistration.cs:226` 서비스 등록 및 `SurvivalWorkExecutionHandler` recharge 실행 | **원격 소비 가능 경로 확정**. 시설 ID가 있어도 stack 상태/목적지 필터 없음. 충전 시설 buffer-bound 예약/소비와 기존 배송 재사용 |
| 범용 사회/캠페인 ItemConsume | `Services/Run/V20ContentResolutionService.cs:411,2465,2602`의 전체 스택 비용 계획→예약→atomic consume; 일일 캠페인 및 알림 선택 연결 | **위치 제한 없는 물리 차감 API 확인, 개별 콘텐츠 결함 판정은 보류**. 실제 현장 투입을 의도한 선택과 즉시 사건 결과를 구분. 모든 추상 선택에 시설/이동을 강제하거나 기존 world item을 임의 gold로 바꾸지 않음 |

공통 소비기 `Services/Items/ItemTransferService.cs:402`의 atomic consumption은 예약 수량을 원자적으로 차감하되 소비 시설/actor 도착을 검증하는 API가 아니다. 따라서 모든 호출에 일괄 이동을 강제하기보다 **각 소비 작업의 입력 선택·배송 준비·도착 gate**를 기존 물류 계약으로 닫아야 한다.

정상 대조: 작업자는 농업 seed lot의 exact-stack delivery→시설 buffer 소비, `Models/Economy/Content/WasteProcessingRuntime.cs:164`의 목적지 배송→buffer 후보, 포로 노동 도구·서커스 소모품·원정 준비/동맹 신호 지원의 destination-bound FacilityBuffer 소비를 확인했다. 이 결과는 이들 시스템의 모든 경로가 완벽하다는 선언이나 새로운 실행 PASS가 아니다.

별도 후보/제한:

- `Services/Character/SpeciesRuntime.cs:1048`의 코볼트 부품 수집은 Stored 부품을 actor 인접 Loose로 직접 relocate하는 **운반 우회 후보**다. Sink/Transform이 아니므로 소비 결함 개수에 섞지 않는다. 특성의 의도된 행동/실제 이동 연결을 후속 확인한다.
- `Services/Evolution/EvolutionCatalystEconomyRuntime.cs:250`에도 Stored 우선 transform API가 있으나 이번 조사에서 비Editor 실제 호출자는 미확인이다. 등록만으로 현재 플레이 결함으로 확정하지 않는다.
- 시설 진화의 `WarehouseFacilityEvolutionResourceProvider`는 별도 warehouse category aggregate 차감 계열이다. 현재 물리 스택과 같은 권위인지/실제 운영 호출이 남았는지 추가 연결 확인 전에는 위 Stored sink 사례와 합산하지 않는다.

### 2026-09-10 후속 사용자 기획 판정 — 구현 전

위 조사 때의 결정 필요 표시는 현재 기획 대기를 뜻하지 않는다. 사용자가 전역 유지비 전면 제거(연료에 한정하지 않음), 마르는 수원은 실제 수원 공급 감소/저장수 보존, 연료 쟁탈은 수락한 물리 납품, 코볼트 숨기기는 설정만 유지, 손님/축제 장소9군은 제안한 기존 시설 재사용으로 확정했다. 현재 계약은 `docs/game-design/wim-implementation-plan.md` 7.4절에 집약했다. 코드/에셋은 아직 변경하지 않았다.

진화 촉매의 실제 호출 여부와 시설 진화 재고 권위는 사용자 선택이 아니라 기술 조사 잔여로 유지한다. 수치 산정·호출 확인·구현 검증을 ‘미결정 기획’으로 재분류하지 않는다. 이 후속 승인은 위 원본 조사 결과를 이미 고친 상태로 바꾸는 근거가 아니다.

### 조사 신선도·제한

- KB query `TryConsumeTreatmentMaterial`, area `code/authority`: fresh,0행. 타입 재검색 `SurvivalFoodRuntime`, 동일 area: fresh,2/12행. 생성 후보 `code/systems/services-survival.csv:29`에서 실제 원본으로 역추적. 다른 반환행 `content-db/code-consumers/events-campaign/weather-front.csv:4`는 의료 증거로 쓰지 않음.
- content digest `59a291bfb242e59597785fe26aaba0c59c70d36dafc1914f6c6d0153d5f84341`, system digest `949c4b98e59059d8c0a1528eb0dccf072be814a93b24584768cde5856d63b552`.
- 원본 SHA256: SurvivalFoodRuntime `77034AEF5F27B311CA913916EFB1F295A2DBC7369F1DDBA5563ACD6E881CB68B`; SurvivalFoodStockRuntime `467C4DDF9CFE307AD52E6F6F5A0EC05237CC488415BFCDBD3F04D220346C401D`; CharacterConsumablesApplicationAdapters `46F81963F9FFB666B43051E5CB662825F593EEB3616CCE97D55E1008F6C72A9D`.
- 신규 KB 재생성·코드/에셋 편집·Unity 실행·삭제0. 정상 대조 행은 현재 source contract 확인이며 모든 저장/AI 조합 PASS가 아니다. 다른 시스템에서 이 문제가 전혀 없다는 전수 인증은 하지 않는다.
- 처음 함께 실행한 전체 git status 출력은 과다해 잘렸으므로 조사 증거에서 제외하고 이후 파일/심볼 제한 검색으로 확인했다. `DeliveryPending`을 배송이라고 설명한 중간 추정도 위 실제 Sink/ack 구현 확인으로 정정했다.
