# DungeonStory 전역 밸런스 기준서

## Captured wildlife indoor pen physical authority correction (2026-08-15)

```text
Definition ID: captivity:wildlife-transport:indoor-pen-physical-authority-v26
Content type: existing captured-wildlife transport and pen-placement authority correction
Definition/producer/consumer locations: WildlifeCaptureRuntime, AbilityWildlifeCaptureTransport, WildlifeActor managed carry, WildlifeRuntime position validation
Growth stage and player decision: unchanged; the existing capture order, carrier selection, reachable pen choice, pickup, delivery, and care flow remain the only route to a penned animal
Physical BOM/input/output, WU, EWU, work rates, time, space, facility capacity, prices, rewards, risks, and authored species movement values: unchanged
Execution authority: while a wildlife actor is Captured, its physical position is owned by the captivity transport/pen aggregate rather than by the free-wildlife outdoor relocation rule. A carry terminal may commit Penned only after the exact carrier-owned actor is detached, registered once at the reserved reachable pen cell, and observed there
Failure and recovery: an occupied or invalid delivery cell returns a typed physical-placement failure without committing Penned or clearing the carrier reservation. The existing transport failure path then releases the animal at the carrier position and removes the incomplete capture aggregate. Ordinary uncaptured wildlife in an invalid dungeon position still uses the existing nearest lawful outdoor recovery
Exploit prevention: the correction grants no teleport, free tame, free pen capacity, duplicate wildlife, alternate destination, path bypass, or indoor access to uncaptured species. The exact reserved carrier, Transporting state, destination cell, parent ownership, and grid registration must agree before completion
Save authority: CapturedWildlifeState remains the aggregate/save authority and WildlifeActor plus the Wildlife grid layer remain the physical projection. Managed-carry ownership is transient and must converge before the aggregate terminal is committed
Automatic audits: CaptivityWildlifeLifecyclePlayModeVerifier must prove production pickup approach, exact external action ownership, one physical registration at penPosition after at least one Wildlife runtime tick, source/parent release, Penned state, and typed failure cleanup; ordinary uncaptured indoor wildlife relocation remains covered by wildlife runtime scenarios
Balance state: numeric balance unchanged; production authority correction implemented, fresh Unity compile and CaptivityWildlifeLifecycle PlayMode rerun pending
```

## 원정 보급 ReservedTarget 목적지 권위 교정 기록 (2026-08-16)

```text
정의 ID: architecture:offense-expedition-supply-reserved-target-v27
콘텐츠 종류: 전략 원정 보급의 물리 운반 목적지 소유권·취소·소비·저장 복원
정의·카탈로그·실행기 위치: DungeonOffensePreparationService, FacilityBufferDestinationClaimRegistry, WorldItemHaulDestinationAuthority, WorldItemHaulPlanningService, ItemTransferService, HaulDeliveryIntentRestoreCoordinator
등장 시대와 연구: 기존 원정 준비가 열리는 시점과 연구 선행 조건을 그대로 유지하며 신규 연구·무료 해금·시작 재고를 추가하지 않음
플레이어에게 주는 새 결정: 새 선택지는 없으며, 선택한 원정 보급이 정확한 패키지 소유 집결지로 실제 운반된 뒤에만 출정에 소비되는 기존 결정을 복구
물리 BOM·입력·출력: 원정 식량·탄약·약품의 기존 수량, 제작 BOM, 창고 재고, 적재 소비량과 귀환 반환량을 변경하지 않음. claim은 물품을 생성하지 않으며 실제 물리 스택의 Stored→Carried→FacilityBuffer→Consumed 전이를 허가하는 소유권 증거만 제공
직접 작업량과 계산 근거: 운반 속도·경로·적재 시간·정착지 WU를 변경하지 않음. 같은 셀의 시설 수나 marker 수를 목적지 권위로 추측하지 않고 패키지 ID와 exact staging 좌표를 한 번 검증함
EWU와 목표 회수 기간: 신규 생산 보너스·작업량 감면·보상 증가가 없으므로 원정 준비 EWU와 회수 기간 목표를 변경하지 않음. 기존 교착으로 무한 대기하던 비정상 경로만 제거
공간·전력·물·연료·정비: 기존 exterior ExpeditionStaging 또는 Entrance 좌표와 실제 보관·운반 경로를 요구함. ReservedTarget claim은 집결지에 가짜 시설을 요구하거나 생성하지 않음
위험·실패·회복 방식: 패키지 ID·좌표·소유 도메인·작업 ID가 누락되거나 어긋나면 요청·계획·복원을 typed failure로 거부하고 임의 같은 셀 시설로 fallback하지 않음. 부분 요청 실패와 포기는 destination-bound Stored/Carried/FacilityBuffer를 release한 뒤 exact claim을 폐기하며, 소비는 물리 수량 커밋 뒤 claim을 정확히 한 번 폐기
사회·비가역 비용: 변경 없음. 원정 중 부재·부상·사망과 보급 소비의 기존 비가역 비용을 유지
기존 대안과의 장단점: 일반 FacilityBuffer는 live facility persistent ID가 권위이고, 원정 집결지는 package-owned ReservedTarget claim이 권위다. 두 경로는 같은 물리 운반기를 재사용하지만 좌표 일치나 문자열 prefix만으로 서로를 대체하지 않음
지배 전략 방지 조건: 동일 패키지 중복 claim 0, 부분 요청 뒤 잔존 목적지 0, 포기·반환 뒤 outbound Stored 잔존 0, 소비 뒤 destination 전 상태 스택 0, 저장 복원 뒤 중복 배송·재소비 0, 같은 셀 다중 marker/시설로 목적지 오선택 0
저장 권위와 실행 명령: OffenseSupplyPackingStateData가 package ID·destination ID·staging·cost·consumed 원본을 저장하고 claim은 복원 시 그 원본과 현재 exterior authority에서 재구축하는 파생 권위다. restore transaction은 offense section commit에서 owner-domain claim candidate를 채우고 220 claim publish 뒤 225 haul-intent participant가 exact destination을 재검증함
자동 감사 ID와 전수 목록 포함 여부: OffenseStrategicDebugScenarios의 physical supply packing/restore/cancel/consume 행, PhysicalItemLogisticsPlayModeVerifier의 EXPEDITION_RESERVED_TARGET_CLAIM_EXACT 및 destination 전 상태 잔존 0 행, mid-action haul save/load exact destination 회귀
검증 매트릭스와 보고서 위치: `DungeonStory/Debug/Offense/Run Strategic Scenarios`, `PhysicalItemLogisticsPlayModeVerifier.RequestRunFromMenu()`, `DungeonAiActionSaveLoadPlayModeVerifier.RequestRun()`, `Artifacts/QA/physical-item-logistics-playmode-report.txt`, `Artifacts/QA/ai-mid-action-save-load-playmode.txt`, Unity Console Error/Warning 0/0
현재 밸런스 상태: `밸런스 영향 없음 / 구조 교정 검증 대기`. 수치·BOM·WU·보상·위험량은 변경하지 않았으나 물리 운반과 저장 복원 실행 경로가 바뀌므로 fresh 집중 회귀, full Physical Logistics, mid-action save/load와 Console 0/0을 모두 통과하기 전에는 연결 완료 또는 원정 밸런스 완료로 보고하지 않음
```

## 장비 수리 LiveFacility 목적지 권위 교정 기록 (2026-08-16)

```text
정의 ID: architecture:equipment-repair-live-facility-destination-authority-v27
콘텐츠 종류: 기존 장비 수리 주문의 물리 장비·재료 운반 목적지 소유권과 저장 복원
정의·카탈로그·실행기 위치: EquipmentMaintenancePolicyRuntime, EquipmentMaintenanceItemServices, FacilityBufferDestinationClaimRegistry, WorldItemHaulDestinationAuthority, PhysicalItemLogisticsPlayModeVerifier
등장 시대와 연구: 기존 대장작업대와 장비 수리 해금 시점을 그대로 유지하며 신규 시설·연구·시작 장비를 추가하지 않음
플레이어에게 주는 새 결정: 없음. 기존 수리 명령이 선택한 exact 수리 시설로 장비와 원재료가 실제 운반되는 계약만 복구
물리 BOM·입력·출력: 장비 원재료 3개, 내구도 회복량, 수리 완료 장비 인스턴스와 해체 회수량을 변경하지 않음. claim은 재료나 장비를 생성하지 않고 기존 물리 스택의 목적지 소유권만 증명
직접 작업량과 계산 근거: 기존 수리 requiredWork, 이동 속도, 픽업·입고 시간과 작업자 수를 변경하지 않음. 목적지는 주문 ID·장비 인스턴스 ID·시설 persistent ID·시설 중심 좌표를 exact 비교
EWU와 목표 회수 기간: 기존 수리 EWU·장비 수명·대체 장비 정책을 변경하지 않으며, 같은 셀 시설 모호성 때문에 발생하던 무한 대기만 제거
공간·전력·물·연료·정비: 기존 대장작업대 footprint·정비 능력·유틸리티를 그대로 요구하고 같은 좌표의 창고나 다른 시설을 수리 목적지로 추측하지 않음
위험·실패·회복 방식: 주문 생성 시 exact LiveFacility claim을 운반 요청보다 먼저 만들고, 시설 소실·주문 취소·완료 시 destination-bound 물리 스택을 보존적으로 release한 뒤 exact claim을 폐기. claim 충돌·시설 ID·좌표 불일치는 typed failure로 거부
사회·비가역 비용: 변경 없음. 수리 중 장비 부재와 작업자 기회비용, 재료 소비는 기존 규칙을 유지
기존 대안과의 장단점: 창고·다른 작업대가 같은 셀에 있어도 대체 권위가 되지 않으며, 대체 장비 지급은 기존 policy와 실제 저장 장비만 사용
지배 전략 방지 조건: 무료 수리·재료 생성·장비 복제 0, 같은 셀 fallback 0, 중복 재료 요청 0, 완료·취소 뒤 orphan claim 0, 저장 복원 뒤 중복 claim·운반 0
저장 권위와 실행 명령: CombatEquipmentMaintenanceSaveData의 active order와 현재 modular facility persistent ID가 원본 권위이고 claim은 저장하지 않는 파생 권위다. restore section commit이 claim candidate를 재구축하고 220 claim publish 뒤 225 haul-intent rebind가 exact destination을 검증
자동 감사 ID와 전수 목록 포함 여부: PhysicalItemLogisticsPlayModeVerifier의 MATERIAL_REPAIR_DESTINATION_CLAIM_EXACT, MATERIAL_REPAIR_INPUTS_DELIVERED, MATERIAL_REPAIR_NO_DUPLICATE_REQUEST, MATERIAL_REPAIR_PRESERVES_INSTANCE_AND_MATERIAL, MATERIAL_SALVAGE_RETURNS_ORIGINAL_MATERIAL 및 mid-action save/load 목적지 검증
검증 매트릭스와 보고서 위치: `PhysicalItemLogisticsPlayModeVerifier.RequestRunFromMenu()`, `DungeonAiActionSaveLoadPlayModeVerifier.RequestRun()`, `Artifacts/QA/physical-item-logistics-playmode-report.txt`, `Artifacts/QA/ai-mid-action-save-load-playmode.txt`, Unity Console Warning/Error 0/0
현재 밸런스 상태: `밸런스 영향 없음 / 구조 교정 검증 대기`. BOM·재료 수량·WU·내구도·회수량은 불변이며 fresh Unity compile, full Physical Logistics, mid-action save/load와 Console 0/0 전에는 연결 완료 또는 장비 수리 밸런스 완료로 보고하지 않음
```

## 수술 재료 LiveFacility 목적지 권위 교정 기록 (2026-08-16)

```text
정의 ID: architecture:surgery-materials-live-facility-destination-authority-v27
콘텐츠 종류: 기존 수술 주문의 약품·재료 물리 운반 목적지 소유권, 취소·실패·완료와 저장 복원
정의·카탈로그·실행기 위치: SurgeryRuntime, SurgeryLogisticsRuntime, SurgeryRestoreCoordinator, SurgerySaveValidation, FacilityBufferDestinationClaimRegistry, WorldItemHaulDestinationAuthority
등장 시대와 연구: 기존 수술 절차·수술대·연구 선행 조건과 해금 시점을 그대로 유지하며 신규 수술·시설·연구·시작 약품을 추가하지 않음
플레이어에게 주는 새 결정: 없음. 플레이어가 선택한 기존 수술 주문의 재료가 exact 수술 시설로 실제 운반된 뒤에만 수술이 진행되는 기존 결정을 복구
물리 BOM·입력·출력: 절차별 기존 약품·물·부품 수량과 환자 결과를 변경하지 않음. claim은 물품을 생성·소비하지 않고 기존 물리 스택의 목적지 소유권만 증명
직접 작업량과 계산 근거: 기존 준비·수술·회복 WU, 이동 속도, 픽업·입고 시간과 작업자 수를 변경하지 않음. 목적지는 수술 주문 ID·시설 persistent ID·시설 중심 좌표를 ordinal exact 비교
EWU와 목표 회수 기간: 기존 수술 시설 EWU, 의료 작업 손실과 회복 시간을 변경하지 않으며 같은 셀 시설 추론이나 중복 배송으로 생기던 비정상 대기·과소비만 차단
공간·전력·물·연료·정비: 기존 수술대 footprint, 방·전력·물·정비·수용량 조건을 그대로 요구함. 같은 좌표의 창고나 다른 시설은 수술 주문의 목적지 권위가 아님
위험·실패·회복 방식: 주문 생성은 exact LiveFacility claim을 물리 요청과 주문 공개보다 먼저 획득함. 시설 소실·취소·실패·완료는 destination-bound 잔여 물리 스택을 보존적으로 release한 뒤 exact claim을 폐기하며, ID·좌표·소유권 불일치는 typed failure로 거부하고 fallback하지 않음
사회·비가역 비용: 기존 환자 위험, 실패 결과, 의사·운반자 기회비용과 회복 대기를 유지하며 구조 교정으로 성공률·부상·기분·관계를 변경하지 않음
기존 대안과의 장단점: 일반 치료와 수술은 기존 절차·시설·위험 차이를 유지함. live 수술대의 exact claim은 좌표 추론보다 추적 가능하지만 주문 생성·terminal·restore가 같은 소유권 생명주기를 지켜야 함
지배 전략 방지 조건: 무료 약품·재료 생성 0, 동일 주문 중복 요청·중복 소비 0, 같은 셀 fallback 0, 취소·실패·완료 뒤 orphan claim 0, 저장 복원 뒤 중복 claim·운반·수술 재개 0
저장 권위와 실행 명령: SurgeryAggregateState의 active order와 현재 modular facility persistent ID가 원본 권위이고 claim은 저장하지 않는 파생 권위임. restore stage가 owner-domain claim candidate를 재구축하고 220 claim publish 뒤 225 haul-intent rebind가 exact 목적지를 검증하며 525 surgery projection이 환자·시설 상태를 공개
자동 감사 ID와 전수 목록 포함 여부: SurgeryPlayModeVerifier의 exact claim·AIHaul delivery·중복 요청 0·완료/취소 revoke·mid-action restore 행, SurgeryDebugScenarios의 late-failure rollback, mid-action SaveLoad와 coverage manifest의 surgery 필수 marker에 포함
검증 매트릭스와 보고서 위치: `SurgeryPlayModeVerifier.RequestRunFromMenu()`, `SurgeryDebugScenarios.RunAll()`, `DungeonAiActionSaveLoadPlayModeVerifier.RequestRun()`, `Artifacts/QA/surgery-playmode-report.txt`, `Artifacts/QA/ai-mid-action-save-load-playmode.txt`, Unity Console Warning/Error 0/0
현재 밸런스 상태: `밸런스 영향 없음 / 구조 교정 검증 대기`. BOM·재료 수량·WU·회복·위험·성공률은 불변이며 fresh Unity compile, 수술 집중/PlayMode, mid-action save/load와 Console 0/0 전에는 연결 완료 또는 수술 밸런스 완료로 보고하지 않음
```

## 건설 자재 운반 중 커밋 수량 중복 요청 교정 기록 (2026-08-16)

```text
정의 ID: architecture:construction-material-in-transit-commitment-v26
콘텐츠 종류: 기존 건설 주문 자재의 물리 운반·수량 예약·목적지 커밋 권위 교정
정의·카탈로그·실행기 위치: WorkOrderRuntime.RequestMissingMaterials/CountPendingDestinationItem, HaulDeliveryIntentRuntime, AbilityHaul, WorldItem quantity lease, CharacterCarryInventory, HaulDeliveryIntentRestoreCoordinator
등장 시대와 연구: Day 1부터 모든 건설 주문. 연구·해금·시설 목록은 변경하지 않음
플레이어에게 주는 새 결정: 없음. 이미 픽업되어 목적지로 이동 중인 자재를 같은 주문이 다시 요청하지 않도록 기존 결정을 보존
물리 BOM·입력·출력: 건설 BOM과 출력은 변경하지 않음. 주문의 delivered + destination world stack + exact owner-operation carried 수량만 기존 required 수량에 대해 한 번 계산
직접 작업량과 계산 근거: 건설 WU와 운반 이동·픽업·입고 시간은 변경하지 않음. 중복 요청으로 생기던 불필요한 두 번째 운반만 제거
EWU와 목표 회수 기간: authored 시설 EWU·회수 기간 변화 없음. 잘못된 중복 운반과 잉여 버퍼 손실만 제거
공간·전력·물·연료·정비: 변경 없음
위험·실패·회복 방식: 계획마다 결정론적 sequence로 발급한 고유 operation ID와 exact destination·item·carried stack·quantity commitment가 모두 일치할 때만 pending으로 인정. unrelated carry는 계산하지 않으며 운반 실패·취소는 기존 물리 회수 경로를 사용. 저장·복원에서 actor, destination, carried stack, signature, quantity, Hauling purpose, cohort 또는 grandfather lease가 불일치하면 대체 스택이나 신규 계획 없이 fail-loud
사회·비가역 비용: 변경 없음. 주민 운반 기회비용은 실제 required 자재 운반에만 발생
기존 대안과의 장단점: world stack만 세는 기존 방식보다 운반 중 전환을 정확히 보존하며, 추상 재고나 free delivery fallback은 추가하지 않음
지배 전략 방지 조건: 자재 생성·복제·BOM 감면 0, actor-wide owner 재사용 0, 같은 destination의 unrelated carried item 과계상 0, pickup/deposit 전환 이중계상 0, 저장 반복 operation/lease/수량 복제 0, 취소 후 물리 총량 보존
저장 권위와 실행 명령: 불변 콘텐츠에는 저장 필드를 추가하지 않는다. 런타임·저장 권위는 per-plan unique HaulDeliveryIntent와 물리 carried stack 및 quantity lease이며, pickup commit·restore rebind·deposit만 이를 변경한다. Physical Items V8은 nextHaulOperationSequence와 grandfather lease hint를, Character World V3은 pickup-committed HaulDeliveryIntent를 저장한다. runtime lease ID는 저장하지 않고 225.world.haul-delivery-intents participant가 character publication 뒤 AI 활성화 전에 새 grandfather lease와 exact rebind한다. participant는 destination kind·stable destination ID·현재 warehouse 또는 construction/input-buffer owner·현재 delivery/drop cell을 공용 resolver로 대조하되 actor를 깨우거나 coroutine을 시작하지 않는다. 전체 RestoreAll 완료 뒤 정상 Brain→AIHaul만 pending delivery-only intent를 정확히 한 번 실행한다. pre-pick 계획은 저장하지 않고 복원 뒤 재계획하며 pickup-committed 계획만 delivery-only로 재개한다
자동 감사 ID와 전수 목록 포함 여부: PhysicalItemLogisticsPlayModeVerifier의 construction physical delivery 행, Work/Haul coverage, DungeonAiActionSaveLoadPlayModeVerifier의 mid-construction-haul delivery-only restore 행에 포함
검증 매트릭스와 보고서 위치: PhysicalItemLogisticsPlayModeVerifier.RequestConstructionRunFromMenu()/RequestRunFromMenu(), DungeonAiActionSaveLoadPlayModeVerifier.RequestRun(), Artifacts/QA/construction-project-playmode-report.txt, Artifacts/QA/physical-item-logistics-playmode-report.txt, Artifacts/QA/dungeon-ai-action-save-load-playmode.txt
현재 밸런스 상태: 수치·BOM·WU·ROI 변화 없음. production 저장 권위 교정과 결정론적 per-plan identity 구현 완료. 기존 건설 전용 및 PhysicalItemLogistics 증거는 fresh PASS였으나 V8/V3 mid-haul 저장 왕복, duplicate request 0, exact delivery completion, 2회 물리 conservation, Console Warning/Error 0/0은 새 코드 기준 Unity 재검증 대기
```

## Emergency work command suspension ownership correction (2026-08-15)

```text
Definition ID: character:alarm-suspended-priority-ownership-v26
Content type: existing Red-alert work suspension ownership correction
Definition/producer/consumer locations: WorkTaskExecutor.TrySuspendAtSafeCheckpoint, CharacterAlarmResponseRuntime, SettlementAlertRuntime suspended-work journal, AbilityWork priority command
Growth stage and player decision: unchanged; the player's original explicit work command is suspended during the existing Red/Amber emergency window and restored from the existing journal only after Green
Physical BOM/input/output, WU, EWU, work rates, progress, hysteresis time, space, utilities, prices, rewards, risks, and emergency thresholds: unchanged
Execution authority: the safe-checkpoint transaction creates the suspension receipt and clears the live priority command before ending the action. The receipt/journal becomes the sole restoration authority, preventing the scheduler from reacquiring the ordinary target before the alarm runtime consumes the receipt. When Amber escalates back to Red, the committed alert epoch and every suspended-work journal entry advance together; the same responder gate may advance only from its exact prior positive epoch to that newer epoch. On Green, an active emergency work executor retains the gate until its coroutine has released movement, reservations and accounting; only a selected live AIWork action with no executor routine is cancelled through its typed terminal. The gate release and original-priority restore then commit in the same scheduler tick
Failure and recovery: a cancelled request before a checkpoint leaves the priority command untouched; a completed suspension preserves externally persisted or inline progress; Green lets an active emergency executor finish its cleanup, cancels only a selected pre-execution/orphan emergency route exactly once, then restores the exact original work type/target and clears the journal; a missing target abandons the journal with the existing typed reason. Same, stale, reversed, or mismatched epoch replacement still fails loudly, while the authoritative monotonic escalation no longer collides with its own retained Amber gate
Exploit prevention: no free WU, priority escalation, emergency bypass, early Amber return, duplicate resume, target substitution, or timeout relaxation is introduced
Save authority: SettlementThreatAlertSaveData and its suspended-work entry remain authoritative; the live priority pointer is transient and is reconstructed only through the existing Green return command
Automatic audits: CharacterAlarmResponsePlayModeVerifier must prove safe-checkpoint stop, persistent-progress conservation, Red->Amber->Green 2+2-hour hysteresis, no Amber reacquisition, exact original-work return, journal removal, and path/reservation/invariant conservation
Balance state: numeric balance unchanged; production ownership correction implemented. The original current-source alarm-response PlayMode gate passed safe-checkpoint suspension, Red/Amber hold, exact Green return, journal cleanup and target-destroy abandonment. A fresh captivity/invasion run exposed the missing Amber-to-new-Red gate epoch handoff; focused alarm and captivity/invasion PlayMode reruns plus Console Warning/Error 0/0 are pending after this monotonic ownership correction
```

## Facility evolution activation reconciliation snapshot correction (2026-08-15)

```text
Definition ID: architecture:facility-evolution-activation-reconcile-snapshot-v26
Content type: existing facility-evolution activation projection liveness and restore-authority correction
Definition/producer/consumer locations: CharacterAiWorldRegistry.Buildings, FacilityEvolutionActivationProjection, FacilityInstanceEvolutionRuntime.RefreshRoomActivation, FacilityEvolutionStateComponent
Growth stage and player decision: unchanged; no facility, evolution option, research, unlock, module, or player command is added or removed
Physical BOM/input/output, WU, EWU, time, space, utilities, maintenance, prices, rewards, risks, and authored activation thresholds: unchanged
Execution authority: one reconciliation pass snapshots the building authority at pass start, refreshes each eligible facility from that snapshot, and commits the observed building/facility-state versions only after the whole pass succeeds
Failure and recovery: registry mutation or nested reconciliation during a pass schedules one next-tick replay against the new authority. An exception remains fail-loud and leaves the previous observed versions intact so the next tick retries instead of treating a partial pass as complete
Exploit prevention: no fixed-point same-frame loop, free evolution progress, duplicate node activation, skipped replacement facility, fallback facility, or silent exception suppression is introduced
Save authority: the building registry version and FacilityEvolutionStateComponent remain authoritative. The pass snapshot, observed versions, reentrancy flag, and pending replay are transient and are not saved
Performance: allocation occurs only when a building or facility dynamic-state version requires reconciliation; stable ticks return before snapshot creation
Automatic audits: FacilityEvolutionActivationProjectionDebugScenarios requires A/B snapshot processing while A is removed and C registered, a B/C next-tick replay, a stable zero-work tick, and fail-loud retry after an injected refresh exception
Deterministic live verification: run FacilityEvolutionDebugScenarios.RunAll and PrimitiveStartSurvivalPlayModeVerifier.RunFocusedFromMenu; both must pass from current source with Console Warning/Error/Exception 0/0 and the Primitive full-save restore must preserve exact facility IDs, definitions, and positions
Balance state: numeric balance unchanged; production liveness correction implemented. Current-source focused and aggregate FacilityEvolution audits pass, and the original Primitive full-save teardown/restore reproduction now passes with exact Rest facility identity/definition/position restoration and post-terminal/post-EditMode Console Warning/Error/Exception 0/0
```

## Safe-drink environmental route admission correction (2026-08-15)

```text
Definition ID: survival:safe-drink-environment-route-authority-v26
Content type: existing emergency drinking route-admission correction
Definition/producer/consumer locations: CharacterSafeDrinkPlanner, IEnvironmentWorkPolicy, CharacterDeprivationRuntime, AI emergency survival branch
Growth stage and player decision: unchanged; this affects only an already-authored emergency drink attempt after physical water selection
Physical BOM/input/output: unchanged. One successful drink still consumes the same reserved physical clean-water quantity, while rejected routes consume nothing
WU, time, need recovery, thresholds, item values, space, and movement speed: unchanged
Risk/failure/recovery: candidate selection now evaluates the exact resolved route, destination, and estimated traversal time through the existing environmental work policy before reserving and moving. A lethal exposure route is rejected with typed diagnostic detail and another lawful physical candidate may be considered
Alternatives: authored drink facilities, stored water, and other physically reachable loose water retain their existing scoring and reservation rules; no missing path or unsafe route is replaced by teleportation or free recovery
Exploit prevention: the correction cannot create water, bypass item leases, ignore a hostile environment, or grant recovery before final physical consumption
Save authority: unchanged world-item stacks, leases, character need state, environment state, and movement state remain authoritative; route assessment is recomputed and not persisted
Automatic audits: CharacterSafeDrinkPlanner direct and exact-path candidates must both pass IEnvironmentWorkPolicy; rejected routes retain the policy failure detail
Deterministic live verification: PrimitiveStartSurvivalPlayModeVerifier five-day run must keep all three founders at positive health, report no survival damage or active breakdown, and conserve physical meals/water
Balance state: numeric balance unchanged; production route-authority correction has a fresh five-day PASS, while final clean-console and manifest refresh remain pending
```

## Social rumor target-authority mood-cap correction (2026-08-15)

```text
Definition ID: character:social-rumor-target-mood-authority-v26
Content type: existing social-memory stacking authority correction
Definition/producer/consumer locations: CharacterSocialMemory.HearRumor, CharacterMoodStateService, visitor rumor events, CharacterDeprivationRuntime, StaffDiscontentRuntime
Growth stage and player decision: unchanged; no new rumor, visitor, facility, relationship, departure, or reward is added
Physical BOM/input/output, WU, time, facility capacity, prices, service recovery, and rewards: unchanged
Mood calculation: the authored rumor impulse and existing maxStacks=2 remain unchanged. The mood-factor identity is keyed by rumor target type plus authoritative target ID rather than by speaker, so many visitors repeating the same facility warning share the existing two-stack cap
Risk/failure/recovery: distinct target facilities or characters still produce distinct memories and mood factors; repeated warnings about one target cannot scale without bound with crowd size
Alternatives: direct interactions, distinct rumors, ordinary mood recovery, staff discontent, and permanent-departure policy keep their existing rules
Exploit prevention: rotating speaker IDs cannot multiply one target warning beyond its authored cap; merging occurs only for the same typed target and never merges unrelated facilities or characters
Save authority: CharacterSocialMemory entries and CharacterMoodState interaction factors remain authoritative; no new persisted field is introduced and the canonical key is deterministically reconstructed from the typed target
Automatic audits: CharacterAiPlanDebugScenarios applies the same facility warning through multiple speakers and requires one target-authoritative mood factor capped at two stacks
Deterministic live verification: the fresh Primitive five-day run must keep the three founders live with no rumor-amplified departure and preserve all physical survival invariants
Balance state: numeric balance unchanged; production mood-authority correction has fresh five-day evidence, while final clean-console and manifest refresh remain pending
```

## Customer visitor work-role authority correction (2026-08-15)

```text
Definition ID: character:customer-visitor-work-role-authority-v26
Content type: existing customer AI catalog and visitor lifecycle authority correction
Definition/producer/consumer locations: CharacterIdentity.CharacterType, CharacterPopulationService.ApplyStaffRuntimeState, CharacterWorkRoleUtility, CharacterAiDecisionPipeline, CharacterDeprivationRuntime, CharacterSpawner exit handoff
Growth stage: existing Day-1 visitor flow; no new visitor, facility, action, unlock, reward, or spawn rate
Player decision: unchanged; Customers use their existing Shopping/LookAround/Exit catalog while promoted visitor staff are authoritatively projected to NPC and keep Work
Physical BOM/input/output, WU, time, space, utility, prices, service recovery, and rewards: unchanged
Risk/failure/recovery: the shared prefab's AbilityWork component is no longer sufficient worker authority for CharacterType.Customer; transient Customers are excluded from the staff deprivation/breakdown aggregate and remain governed by visitor satisfaction, patience, complaint, vandalism, and exit; visitors can reach their authored exit instead of accumulating as false workers until violent breakdown
Alternatives: NPC/Owner workers retain existing work priorities and off-duty leisure; verifier-only visitor projection remains available for focused tests
Exploit prevention: Customer identity cannot perform staff work merely because the shared prefab carries AbilityWork; promotion must pass through the existing population authority before work becomes available
Save authority: CharacterIdentity.CharacterType and the population profile's isStaff projection remain authoritative; transient Customer deprivation state is recomputed as absent and no new save field or cached role is added
Automatic audits: Customer/visitor AI scenarios must prove a Customer is non-worker despite AbilityWork, while a promoted NPC staff actor remains a worker
Deterministic live verification: Primitive 5-day must keep the three starting actors free of visitor-breakdown damage and prove natural physical meal/water conservation; VisitorControl must still prove entry, service terminal, and pool-release exit
Balance state: numeric balance unchanged; production role-authority correction pending fresh Unity compile and PlayMode verification
```

## Offense strategic battle liveness correction (2026-08-15)

```text
Definition ID: combat:offense-strategic-planned-turn-liveness-v26
Content type: existing strategic battle formation and command-resolution contract correction
Definition/catalog/executor locations: OffenseBattleSession, OffenseBattleRuntime, OffenseBattleDirector, OffenseCommandResolutionAdapter
Growth stage: existing strategic expedition battle; no new unlock, facility, project, unit, card, enemy, or reward
Player decision: existing card-to-enemy-intent pointer flow is unchanged; an invalid allied execution no longer deletes the enemy intent for free
Physical BOM/input/output: unchanged; no item, ammunition, equipment, supply, loot, or currency quantity changes
Direct work/time calculation: unchanged; no WU, action cost, damage, health, armor, accuracy, speed, stage, or reward multiplier changes
EWU/target payback: unchanged because this restores execution/liveness only
Space/power/water/fuel/maintenance: unchanged
Risk/failure/recovery: a downed non-acting combatant is removed from formation occupancy, surviving combatants compact into existing slots, and unavailable allied commands preserve the enemy intent; the planned command batch advances exactly one battle round; the enemy intent remains the clash target while the applied ability effect resolves its authored Self/Ally/Enemy target rule deterministically
Social/mood/relationship cost: unchanged
Alternatives: ordinary battle commands keep their existing Advance/Reload/Ability choices; strategic cards keep authored tags, effects, target rules, formation restrictions, and cooldowns
Exploit prevention: an out-of-range or otherwise unavailable allied card cannot nullify an enemy action; Self/Ally effects cannot be redirected to an enemy and Enemy effects cannot be redirected to the party; deterministic ally selection grants no extra activation; compaction grants no free attack, damage, turn, item, or reward
Save authority: existing battle round, formation, cooldown, status, director deck, intent, and command queue fields remain authoritative; no new persisted field
Automatic audits: OffenseStrategicDebugScenarios requires an unavailable interception to execute the preserved enemy intent exactly once
Deterministic live verification: OffenseJourneyPlayModeFacade.RequestRun must traverse the real strategic pointer UI, record typed command outcomes, reject three consecutive no-effect turns, observe enemy damage and battle terminal, return, reward exactly once, and prove ownership cleanup
Balance state: numeric balance unchanged; production liveness correction pending fresh Unity compile and PlayMode verification
```

## Incapacitated injury mood side-effect isolation (2026-08-15)

```text
Definition ID: character:injury-mood-side-effect-capacity-v26
Content type: existing damage side-effect transaction correction
Execution path: CharacterBodyHealthRuntime -> CharacterVitalsSideEffectAdapter -> CharacterMoodPolicyService
Physical BOM/input/output, WU, time, space, utilities, equipment, rewards, and progression: unchanged
Health and damage values: unchanged; authoritative injury damage is committed exactly once through the existing body-health runtime
Mood values: the existing health:injury impulse (-clamp(damage*0.25, 2, 10), 180 seconds, max 2 stacks) is unchanged when negative-mood-duration performance is applicable
Failure/recovery: when MentalMaintenance is below the authored applicability floor, the optional injury mood side effect is not created; damage activity, death/lifecycle publication, expedition result, and later medical recovery continue instead of being aborted by an exception
Exploit prevention: this grants no health, resistance, immunity, reward, action, or free recovery; it only prevents an unavailable post-damage mood projection from breaking the already committed damage transaction
Save authority: body health remains authoritative for damage and CharacterMoodState remains authoritative only for mood factors that were actually applicable; no new save field
Deterministic verification: the production strategic Offense journey must complete a real battle whose incapacitated member damage is projected without Console exceptions, then publish result/reward and clean all expedition ownership
Balance state: numeric balance unchanged; transactional side-effect isolation pending fresh Unity compile and PlayMode verification
```

## V26-156 GC 수용 기준 교정 (2026-08-12)

```text
교정 사유: 기존 release soak의 Editor 전체 평균 2,048 KB/frame 상한은 Editor·검증기·게임 할당을 혼합하므로 물류 회귀의 인과 기준이 될 수 없음
Editor 회귀 게이트: 동일 월드 정지 상태 30프레임 워밍업 + 120프레임 baseline을 수집한 뒤 활성 평균과 p95에서 각각 baseline을 차감
Editor 증분 예산: 평균 512 KB/frame 이하, p95 2 MB/frame 이하
Editor 폭주 방지: 활성 절대 평균 16 MB/frame 이하, 단일 최대 256 MB 이하. 합격 권위가 아니라 측정 파손·폭주 차단용
Player 합격 권위: 정상 운용 평균 32 KB/frame 이하, p95 128 KB/frame 이하, 단일 최대 2 MB 이하
장기 안정성: 측정 전후 강제 수집 기준 잔류 Mono heap 증가 64 MB 이하. 물리 스택·Lease·MealPlan 수는 공급·활성 작업 수와 비례하고 역사적 운반 횟수와 비례하면 실패
비주기 작업 분리: 저장·로드·명시적 대량 집약의 일시 할당은 Player 정상 운용 평균/p95/max 표본에서 제외한다. 대신 작업 완료 후 잔류 Mono 64 MB, 동일 상태 왕복, Lease·Intent·물리 스택 누적 및 수량 보존을 별도 검사한다
실행 권위: `GameplayGcAcceptancePolicy`를 release soak와 공용 `GameplayPerformanceReportAssembler`가 함께 사용한다. Editor는 baseline 표본 120개가 없으면 실패하고, Player는 절대 평균·p95·최대값을 모두 통과해야 한다
판정 원칙: 측정 후 합격시키기 위해 예산을 이동하지 않는다. 먼저 위 수치를 고정하고 동일 시나리오를 반복 측정한다
현재 상태: Unity MCP 컴파일·라이브 소비자 감사·baseline/active release soak는 통과했다. 공식 Editor 증분은 평균 280.0 KB/frame, p95 281.2 KB이며 폭주 평균/최대는 1,011.6/70,669.6 KB, 잔류 Mono 증가는 19.40 MB다. Player 절대 평균/p95/max 측정 전에는 최종 성능 완료 아님
```

## Existing equipment world-drop authority record (2026-08-15)

```text
Definition ID: architecture:equipment-existing-instance-world-drop-v26
Content type: physical equipment identity and save-authority correction
Execution paths: wildlife recoverable weapon, settlement defense recoverable weapon, captive equipment confiscation, and equipment-maintenance delivery/output
Physical BOM/input/output: unchanged. The operation moves the already authoritative equipment instance into one physical stack and never creates a second equipment instance.
Time, WU, space, power, water, fuel, maintenance, quality, risk, and alternatives: unchanged. This is an identity/transaction correction only.
Failure and rollback: physical creation uses SpawnExistingUniqueItemAt with the authoritative ItemInstanceId. Link success is mandatory; link failure deletes the newly created unlinked stack, and rollback failure is a hard invariant error. Generic SpawnUnique rejects equipment and equipment-module item IDs.
Save authority: DungeonPhysicalItemSaveData V7 keeps a one-to-one mapping between each equipment-item stack and authoritative uniqueItems entry. Existing linked unique stacks retain stack and item-instance IDs when released from a facility buffer.
Exploit prevention: repeated drop calls reuse the existing linked stack; they cannot mint a second item, duplicate modules, orphan a stack, or replace the authoritative item-instance ID.
Deterministic verification: PhysicalItemDebugScenarios case equipment_existing_instance_atomic_drop_capture_24 creates and atomically drops 24 authoritative equipment instances, then performs the canonical physical Capture and requires exact one-to-one stack/unique-item identity.
Balance status: no balance values changed. Static implementation is complete; Unity compile and the focused physical-item contract run are still required before reporting verification complete.
```

### V26-157 implementation continuation (2026-08-12)

- Actual approved character work now enters a fixed-point daily labor ledger. Process-output conversion, domain automation, losses, essential maintenance and facility maintenance remain separate physical-commit channels.
- Emergency reserve target authority is `max(12 WU, productive non-downed adults * 3 WU, highest authored P90 risk WU)` multiplied by committed Green/Amber/Red state. Missing disaster forecasts are not fabricated.
- The saved 30-day per-capita net-WU median replaces the sovereign 20-resident gate. Temporal qualification replaces the 60-resident gate with per-capita index 2.00 and emergency coverage 1.20 sustained for 120 days.
- This is still `balance baseline assigned / implementation in progress`. Domain automation/loss producers, culture/service authorities, suspended-work Lease preservation, shadow disasters, UI and Unity MCP regressions remain mandatory.

## V26-156 공용 수량 예약·배식 물류 완료 증거 보강 (2026-08-12)

```text
정의 ID: architecture:item-quantity-lease-v26 / balance:meal-logistics-v26
변경 범위: 모든 물리 아이템 수량 예약·부분 픽업·Meal/ProductionInput 버퍼 집약·음식 배식·부패·Task Intent 소유권 복원·진단 UI
물리 BOM·입력·출력: 예약은 아이템을 생성하지 않는다. 픽업 시 점유 수량만 분할하고, 소비 커밋 시 동일 수량만 제거한다. 100개 동시 처리에서 원본+운반+버퍼 합계와 예약 합계가 전 과정 보존되며 완료 후 잔량 0이다
직접 작업량·시간: 식사 행동 4초, 하루 포만 감소 50, 일반/긴급 식사선 50/20, 포만 상한 115, 간식 쿨다운 15초. 배식 경로는 Region 하한 뒤 최대 8개 정밀 경로, 프레임당 2,048 노드 예산을 사용한다
공간·동선·대기: 좌석 방문 슬롯과 음식 수량 Lease를 하나의 MealPlan으로 확보한다. ETA는 캐릭터·음식의 시설 도착과 2초 물류 여유, 4초 식사 시간을 포함한다. 8초 이상 열세 후보는 정밀 경로 전에 제거한다
분할·집약: 예약 단계 자식 0. 픽업 중에만 물리 자식이 존재한다. 동일 시설·cohort·품질·오염·보존·2초 신선도 버킷만 집약하며 64건/tick 처리 후 나머지를 다음 tick으로 넘긴다. 100개/MaxStack 75 결과는 2개 canonical stack이다
부패·실패: 운반·버퍼에서도 신선도가 감소한다. 식사 시작과 소비 직전 두 번 검증하며 부패·오염·정책 변화 시 해당 Slice만 무효화하고 재탐색한다. 다른 작업의 Lease와 물량은 유지한다
저장 권위·악용 방지: runtime Lease/TTL/인덱스는 저장하지 않는다. Intent에 origin stack, preferred physical stack, signature, quantity, purpose, cohort, ordinal을 저장한다. 복원은 모든 기존 claim을 우선 일괄 등록하고 나서 신규 AI를 연다. 누락·서명 불일치·수량 초과는 대체 없이 복원 실패다
실행 권위: ItemQuantityReservationService + ItemTransferService + BufferStackAggregationService가 수량 변이를 소유한다. 직접 소비도 임시 DirectPlayerOrder Lease를 사용하고 시설 출력은 AvailableQuantity만 이동한다
자동 감사: 물리 아이템 계약 33/33, 100-owner 64+36 집약·먼지 0, 실제 아이템 상세 UI PlayMode PASS, 동기 최종 수용 33/33, 공식 PlayMode 7/7·캡처 32·저장 68/68/68·Console 0/0/0/0
성능 증거: 최종 Unity MCP 릴리스 soak에서 무효 예약 0, 시설 초과 0, 저장 증가 제한, 저장 재로드, frame p95 42.81 ms, 잔류 Mono +19.40 MB, Editor 증분 평균/p95 280.0/281.2 KB, 폭주 평균/최대 1,011.6/70,669.6 KB, Console 0/0과 RESULT=PASS를 확인했다
성능 인과: full-mana idle actor의 매 프레임 mana-recovery 전체 성능 투영과, 선행 온보딩 단계에서 0.25초마다 발생하던 offense campaign snapshot을 제거했다. GC 예산은 측정 후 이동하지 않았고 512 KB 평균/2 MB p95를 그대로 통과했다
현재 밸런스 상태: Phase 156 기능·구조 연결과 Editor 성능 수용 완료. Player 절대 GC 측정과 Phase 155 기술 단계별 순 WU 재계산은 별도 미완료이므로 전체 밸런스 완료 아님
```

> 상태: 이론 기준 권위
>
> 기준 세대: V26 전역 밸런스 체계
>
> 최종 갱신: 2026-08-09
>
> 적용 대상: 시설, 아이템, 재료, 조합식, 장비, 의복, 연구, 종족, 특성, 농업, 축산, 의료, 사건, 축제, 손님, 세력, 계약, 전투 조우, 이정표, 엔드리스와 이들의 수치 변경

---

## 1. 문서 권위와 사용 원칙

이 문서는 DungeonStory의 수치와 시스템 간 교환 관계를 판단하는 단일 이론 밸런스 권위다. 플레이어에게 새 추상 재화를 추가하지 않고, 실제 물리 아이템·노동·공간·위험을 개발용 공통 기준으로 비교한다.

문서 권위는 다음처럼 분리한다.

| 권위 | 소유 범위 |
|---|---|
| `DungeonStory_Game_Design_and_Implementation.md` | 게임 정체성, 콘텐츠 범위, 시스템 규칙, 저장·구현 계약 |
| 이 문서 | 공통 밸런스 단위, 목표 밴드, 분포, 교환율, 지배 전략 방지와 검증 절차 |
| ScriptableObject와 루트 콘텐츠 카탈로그 | 현재 빌드에서 실행되는 개별 콘텐츠 수치 |
| 생성된 QA·감사 보고서 | 현재 에셋이 권위 계약을 만족했다는 실행 증거 |

충돌이 생기면 다음 순서로 해결한다.

1. 게임 정체성과 시스템 규칙은 종합 문서를 따른다.
2. 목표 수치·분포·검증 방식은 이 문서를 따른다.
3. 현재 에셋 수치가 기준을 벗어나면 에셋을 조정하거나 예외 근거를 이 문서에 기록한다.
4. 생성 보고서는 권위를 바꾸지 않는다. 보고서는 통과·실패 증거다.

카탈로그 등록, 공식 존재, 컴파일 성공만으로 `밸런스 완료`라고 기록하지 않는다. 밸런스 상태는 다음 네 단계로 구분한다.

| 상태 | 의미 |
|---|---|
| 기준 배정 | 역할, 비용, 목표 밴드가 이 문서에 따라 지정됨 |
| 공식 검증 | BOM·작업량·순환·도달 가능성과 정적 분포가 통과함 |
| 시뮬레이션 검증 | 결정론적 다중 시드에서 목표 분포가 통과함 |
| 실전 보정 | 실제 플레이·장시간 성능·UX 자료로 최종 조정됨 |

## 2. 공통 밸런스 단위

### 2.1 작업과 시간

- `1 WU(Work Unit)`는 중립 주민의 실제 작업 1초다.
- 현실 180초는 게임 1일이다.
- `100초 × 전환 효율 0.99 = 99`는 과거 시간표에서 계산한 **이론적 작업 가능 상한**일 뿐 실제 WU 권위가 아니다.
- 무연구 건강 성인의 밸런스 기준은 5일 라이브 표본의 안정 관측치 `19.882 WU/인·일`을 반올림한 `20 WU/인·일`이다.
- `1 WD(Worker Day)`는 고정 99 WU가 아니다. 계산 대상 기술 단계와 집단의 승인된 라이브 `WU/인·일`을 반드시 함께 기록한다.
- 시작 주민 3명의 기준 실제 노동은 `60 WU/일`이며 숙련·종족·특성·시설·욕구·동선 분포를 적용하기 전의 중앙 기준이다.
- `99 WU/인·일`을 사용한 기존 연구 일수·계약 용량·생산 처리량·장비 준비 계산은 레거시 이론 계산으로 분류하며 신규 밸런스 판단에 사용하지 않는다.

### 2.2 밸런스 비용 벡터

모든 콘텐츠는 하나의 가격이 아니라 다음 비용 벡터로 평가한다.

```text
BalanceCost = [
  직접 작업량,
  내재 작업량,
  달력 지연,
  공간·동선,
  전력·상하수·연료·정비,
  가역 위험,
  사회 부담,
  비가역 위험,
  플레이어 주의력
]
```

### V26 창립자 특성 런타임 연결 폐쇄 기록 (Phase 149, 진행 중)

```text
정의 ID: balance:founder-trait-runtime-connectivity-v26
콘텐츠 종류: 기존 창립자 특성 100종의 수치 조건, 정체성 사건, AI 선호, 극한 명령 연결 보수
정의·카탈로그·실행기 위치: CharacterStatsProjectionService, AbilityWork/WorkTaskExecutor, CharacterIdentityDomainAdapters, ExtremeTraitRuntime, 각 도메인 UI
성장·세대·연구: 신규 연구 비용·숙련 XP·세대 수치는 추가하지 않음. 기존 traitId와 연구 권위를 그대로 사용
플레이어 결정: 금단의 도약은 연구 트리에서 실제 특성 보유자를 확인한 뒤 직접 명령으로 실행. 나머지 미연결 극한 명령은 연결 완료 전까지 밸런스 완료로 간주하지 않음
물리 BOM·입력·출력: 이번 수직 슬라이스는 BOM·산출량을 변경하지 않음. 사고는 승인 작업량을 기준으로 기존 작업을 중단하고 체력 2 피해를 적용하며 재료를 복제하지 않음
직접 작업량 근거: 일반 작업 사고 hazard는 p = 1 - exp(-0.001 × 승인 WU × 최종 사고 배율). 장기 교대 조건은 게임 내 4시간(하루의 1/6) 연속 작업부터 활성
EWU·목표 회수 기간: 작업속도·사고율·회복·소비는 기존 WU/욕구 공식의 배율로만 연결. WU 재계산은 모든 고아 연결과 회귀 검증 뒤 수행
공간·전력·물·연료·정비: 기존 시설 요구를 변경하지 않음. 방 소음은 동일 방의 실제 생산·제작·훈련·마나 시설 존재로 판정
위험·실패·회복: 작업 사고, 오염 조리, 비쾌적 지형, 수술 관찰, 경보 반응, 사선 각성 탈진을 기존 저장/상태 권위에 연결
사회·비가시 비용: 직접 명령 후 기분·스트레스 지속시간은 authored durationDays를 사용. 사과·단식·비상 비축은 아직 명령/상태가 없어 미완결로 기록
기존 대안과의 장단점: 숫자만 SO에 저장하는 방식보다 실제 도메인 결과를 바꾸고 contribution trace를 남김. 즉시 one-target 투영 반복은 단일 revision snapshot 캐시보다 비용이 크므로 후속 통합 필요
지배 전략·악용 방지: status 직접 곱과 공용 투영의 이중 적용 제거. 결정론적 극한 판정은 저장된 상태/고정 hash를 유지하며 UI 재개방으로 재굴림 불가
저장 권위와 실행 명령: 선택은 CharacterGrowthState.traitIds, 정체성 연속 상태는 기존 narrative identity state, 물리 장비는 아이템 인스턴스. 파생 배율은 저장하지 않고 재계산
자동 감사 ID와 전수 목록: Artifacts/QA/v26-founder-trait-connectivity-audit.md. 현재 잔여 2 partial target, 6 condition, 42 event ID, 19 AI tag, 3 typed endpoint, 3 persistent need, 3 extreme
현재 밸런스 상태: 연결 폐쇄 진행 중. Unity MCP 컴파일/Console Error 0은 통과했지만 focused/full 회귀와 라이브 WU 재계산 전이므로 밸런스 완료 아님
```

- 직접 작업량: 해당 주문에서 실제로 누적하는 작업량
- 내재 작업량: 모든 입력을 처음부터 다시 생산하는 총 노동
- 달력 지연: 성장, 건조, 치료, 연구, 이동처럼 노동과 별도로 기다리는 시간
- 공간·동선: 점유 셀과 혼잡·운반 거리 증가
- 기반 비용: 전력, 물, 하수, 연료, 정비와 예비 설비
- 가역 위험: 부패, 손상, 실패, 재시도처럼 다시 복구할 수 있는 기대 손실
- 사회 부담: 기분, 관계, 문화 충돌, 원한과 의무
- 비가역 위험: 사망, 고유 유물 소실, 세대 단절과 영구 외교 결렬
- 주의력: 주문, 경보, 팝업, 정책 변경과 예외 처리 횟수

사망, 관계 파탄과 고유 유물은 철괴나 작업량으로 환산하지 않는다. 이들은 발생 확률, 회복 가능 여부와 후속 세대 영향을 별도 지표로 유지한다.

### 2.3 내재 작업량

개발용 그림자 원가 `EWU(Embedded Work Unit)`는 다음처럼 계산한다.

```text
EWU(output) =
(
    Σ(입력 수량 × 입력 EWU)
  + 직접 제작 작업량
  + 예상 운반 작업량
  + 배부된 전력·정비 작업량
  + 부패·실패·오염의 평균 손실
)
÷ 기대 유효 출력량
```

- EWU는 플레이어 재화가 아니다.
- 금화는 EWU의 복사본이 아니라 외부 조달·계약용 회계 자원으로 유지한다.
- 물리 BOM은 질량과 기능적 개연성을 먼저 만족해야 한다. EWU를 맞추기 위해 무관한 소비처를 추가하지 않는다.
- 희귀 비제작 유물과 세력 인장은 EWU만으로 가격을 정하지 않고 비가역 자산으로 별도 표시한다.

## 3. 런 단계별 거시 기준

### 3.1 정상 노동 배분

정상 공급 상태에서 전체 계획 가능 노동은 다음 밴드를 목표로 한다.

| 용도 | 목표 비중 |
|---|---:|
| 생존·청소·수리·생활 유지 | 25~35% |
| 운반·재고 정리 | 12~20% |
| 건설·생산·연구·교육 | 35~50% |
| 비상 대응 여유 | 10~20% |

- 필수 유지비가 장기간 40%를 넘으면 성장 불능 위험으로 본다.
- 필수 유지비가 장기간 20%보다 낮으면 생존 시스템이 장식화됐는지 검사한다.
- 정상 상태의 작업 차단 비율은 5% 미만이어야 한다.
- 자동화는 운반 노동을 35~55% 줄이되 전력·정비가 총노동의 5~10%를 소비해야 한다.
- 자동화의 순노동 회수 목표는 20~35%다.

### 3.2 단계별 비축과 회복

| 시기 | 필수 유지비 | 목표 비축 | 보통 위기 회복 |
|---|---:|---:|---:|
| 1~10일 | 35~45% | 5~7일분 | 2~3일 |
| 10~30일 | 30~40% | 7~12일분 | 5일 이내 |
| 30~120일 | 25~35% | 15~30일분 | 10일 이내 |
| 120~400일 | 25~40% | 한 계절분 | 15일 이내 |
| 400~960일 | 30~45% | 핵심 물자 30~60일분 | 30일 이내 |
| 960일 이후 | 문명 구조에 따라 변동 | 복합 위기 1회분 | 다음 보스 주기 전 |

시작 주민 3명의 기본 거처는 3일 총노동 `891 WU`의 55~65% 안에서 완성 가능해야 한다. 나머지 노동은 식량, 물, 운반, 수리와 응급 대응에 남아야 한다.

## 4. 영역별 기준

### 4.1 생존과 욕구

정상 난도·충분 공급 기준:

- 생존 행동 비중 18~28%
- 평균 작업 비중 55% 이상
- 결핍 피해와 붕괴 0건
- 재료·경로 외 작업 차단 5% 미만
- 종족 결과 편차 15%p 이하
- 하루 식사와 음수 각각 1~1.5회
- 하루 수면 0.7~1.2회
- 하루 배변과 위생 각각 0.6~1.0회. 정상 시설·중립 건강 성인의
  5일 다중 시드 실측을 사용하며, 하루 한 번을 중심으로 하되
  관측 창 경계와 욕구 회복량에 따른 편차를 허용한다.

표준 공급에서는 안정적이어야 하지만 물 부족, 동선 단절, 질병과 기후 위기 중 둘 이상이 겹치면 준비 수준에 따라 붕괴 가능성이 생겨야 한다.

### 4.2 농업·축산·식량

- 정상 생산은 평균 소비의 125%를 목표로 한다.
- 종자 보존, 사료와 평균 부패를 제외한 뒤에도 110% 이상이어야 한다.
- 겨울 직전에는 최소 30일분 식량 확보가 가능해야 한다.
- 고속·고수확 작물은 보통급 대량생산을 담당한다.
- 느린 전문 품종은 고급 섬유, 환경 저항과 종자 안정성을 담당한다.
- 모든 기후와 목적에서 동시에 우월한 작물·품종·가축은 허용하지 않는다.
- 축산의 순수 식량 효율은 재배보다 낮게 두고 양모, 비단, 운반, 비료와 문화 가치를 통해 경쟁시킨다.
- 품종 우위는 수확량, 성장, 물, 비옥도, 병저항, 종자 회수와 품질 생산량의 파레토 전선으로 검사한다.

### 4.3 시설 건설

| 시설 | 목표 회수 기간 |
|---|---:|
| 초반 생활·저장 시설 | 3~10일 |
| 중세 작업대·서비스 | 10~30일 |
| 환경·의료 시설 | 15~45일 |
| 산업·자동화 시설 | 30~90일 |
| 룬·비전 시설 | 60~180일 |
| 랜드마크 | 경제 회수보다 문명 효과와 새 압력으로 평가 |

모든 플레이어 건설 시설은 다음을 가져야 한다.

- 구체 물리 BOM
- 건설 작업량
- 점유 셀과 경로 영향
- 필요 연구
- 실제 실행 역할
- 처리량 또는 효과 용량
- 전력·물·연료·정비·인력 요구
- 같은 시대 대안과의 교환 관계
- 목표 회수 기간
- 해체 작업량과 회수 규칙

연구 순번이나 에셋 인덱스만으로 비용을 증가시키지 않는다.

### 4.4 생산·재료·아이템

- 원료 이후 연속 제작 깊이는 최대 4단계다.
- 공용 중간재는 실제 소비처 2개 이상, 전략 재료는 3개 이상을 목표로 한다.
- 일반 가역 순환의 회수 가치는 투입 EWU의 95% 미만이어야 한다.
- 해체·재건·품질 재굴림 순환은 투입 EWU의 85% 미만이어야 한다.
- 소비성 약품, 접착제, 연료와 촉매는 회수하지 않는다.
- 부산물은 실제 하류 수요가 있을 때만 경제 가치로 인정한다.
- 입력 부족, 출력 포화, 경로 단절과 하류 재고 충족은 서로 다른 상태로 표시한다.
- 일반 상태에서 출력 포화로 전체 생산망이 멈추는 시간은 5% 미만이어야 한다.

### 4.5 금화·상점·손님

- 내부 비교 기준은 `1 gold = 3 EWU`다. 이 환산은 가격 감사용이며 금화를 물리 재료로 바꾸지 않는다.
- 외부 구매가는 EWU 환산 원가보다 25~50% 높게 둔다.
- 외부 판매가는 EWU 환산 원가보다 30~50% 낮게 둔다.
- 구매→제작→판매, 구매→해체와 제작→해체 순환에서 무한 차익을 허용하지 않는다.
- 일반 영업의 순이익률 목표는 10~20%다.
- 고급 서비스는 20~35%를 허용하되 사고, 재고, 숙련과 공간 부담이 증가해야 한다.
- 금화는 물리 생산망을 대체하지 않고 결핍을 외부 비용으로 메우는 선택이어야 한다.
- 기본 중앙값은 외부 구매 `0.45 gold/EWU`, 자동 판매 `0.20 gold/EWU`, 일반 소매 내부가치의 1.20배, 프리미엄 서비스 순마진 25%다.
- 외부 납품은 금화를 먼저 차감한 뒤 물리 아이템을 생성한다. 전량을 만들지 못하면 실제 납품 비용을 `ceil(총비용 × 실제수량 ÷ 요청수량)`으로 정산하고 나머지는 `ShopPurchaseRefund`로 즉시 환불한다. 생성 0개는 전액 환불한다.
- 자동 판매는 실제 판매 버퍼의 물리 수량을 먼저 소비하고 성공한 경우에만 `SaleIncome`을 지급한다. 품질 미달품의 `MarkForSale`은 정확한 StackId를 `sale:quality-rejected` 시장 버퍼까지 운반한 뒤 물리 스택과 의복·장비 인스턴스 권위를 함께 제거해야 정산된다. 지정만으로 금화를 만들지 않는다.
- 품질 미달 완제품 판매가는 `floor(기본 단가 × 판매율 × 품질 투영 계수)`다. 품질 투영은 형편없음 0.70, 저급 0.82, 보통 1.00, 양호 1.08, 우수 1.16, 명품 1.26, 전설 1.40을 사용한다. 일반 전투 장비의 기본 판매율은 0.60, 콘텐츠가 정의한 합법 범위는 0.50~0.70이며, 최대 조합도 기본 단가의 98%를 넘지 않는다.
- 품질 미달 시장 정산은 평가 1회당 최대 4개로 제한한다. 판매 금지 물품, 장착·휴대·원정·정비 중인 장비, 부품이 장착된 장비와 목적지·인스턴스 권위가 불일치하는 물품은 소비하거나 지급하지 않고 원인을 표시한다.
- 비제작 유물, 계보 인장, 미감정 전리품과 장착 부품은 일반 자동 구매·판매 대상에서 제외한다. 별도 감정·계약·원정 경로가 실제 권위다.

### 4.6 물류·전력·자동화

- 초중반 운반 노동은 총노동의 12~20%다.
- 자동화 이후 운반 노동은 5~10%를 목표로 한다.
- 자동화 정비·전력 노동은 5~10%를 목표로 한다.
- 정상 발전 여유는 평균 수요의 120%다.
- 의료·방어용 비상 전력은 최소 하루분을 목표로 한다.
- 자동화가 정지해도 기존 수동 시설과 비상 운반으로 핵심 생존 기능을 유지할 수 있어야 한다.
- 반복 운반은 줄어들지만 필터, 버퍼, 우선순위, 오버플로와 정비라는 상위 운영 문제가 남아야 한다.
- 현재 자동화 콘텐츠 기준선은 전동 보조 작업 1.35배, 자동화 정비 소모 시간당 1, 자동 품질 상한 0.50~0.90이다. 자동 모드는 보조 모드보다 적은 전력을 소비할 수 없고 수동 모드는 자동화 전력 0을 유지한다.
- 기반망 알고리즘 기준선은 100×100 유틸리티 셀과 2,000개 화물 경로를 정상 해석하고 워밍업 후 해당 측정 구간 할당 0바이트를 유지하는 것이다.

`InfrastructureBalanceCalibrationScenario`가 자동화·발전·소비·축전 콘텐츠와 기반망 스트레스를 검사한다. 운반 5~10%, 정비·전력 노동 5~10%라는 실제 사회 비중은 대표 식민지 PlayMode 부하에서 별도로 측정한다.

### 4.7 의복·섬유

- 생물 주민당 속옷 완전 세트 3벌이 기준이다.
- 세탁·건조 처리량은 하루 평균 오염 발생량의 125% 이상이어야 한다.
- 실내 건조는 최악 환경에서도 24시간 안에 완료된다.
- 일상 세탁·수선 노동은 총노동의 5%를 넘지 않아야 한다.
- 방한복 비용은 해당 환경에서 예상되는 질병·작업 손실 10~30일분보다 낮아야 한다.
- 특수 개조복은 범용복보다 비싸지만 해당 신체·환경에서 15~25%의 명확한 이점을 제공해야 한다.
- 원단 종류는 물성과 가공 난도를 제공하지만 완성 품질을 보장하지 않는다.

### 4.8 제작 품질과 작업자

현재 품질식에서 시설·도구 보정과 복잡도 페널티를 0으로 둔 기준 분포는 다음과 같다.

| 숙련 | 자연 목표 | 성공률 | 평균 시도 |
|---:|---|---:|---:|
| 25 | 보통 이상 | 41.12% | 2.43회 |
| 50 | 양호 이상 | 34.24% | 2.92회 |
| 75 | 우수 이상 | 41.12% | 2.43회 |
| 100 | 명품 이상 | 58.88% | 1.70회 |
| 100 | 전설 | 19.12% | 5.23회 |

- 기본 반복 주문은 평균 2~3회 안에 도달 가능한 품질을 목표로 한다.
- 기대 시도가 20회를 넘으면 강한 자원 경고를 표시한다.
- 이론상 확률이 0이면 재료를 소비하지 않고 `TargetCurrentlyUnreachable`로 대기한다.
- 작업자 정책을 바꿔도 이미 누적된 기여량과 확정된 시도 난수는 변하지 않는다.
- 좋은 재료는 성능·작업 난도·손실 비용을 바꾸지만 품질을 보장하지 않는다.

### 4.9 연구·기술 진행

기준 연구원 1명의 하루 연구 작업은 무연구 라이브 기준 `20 WU`에서 연구 수행 성능과 실제 연구 가능 시간을 적용해 계산한다. `99 WU` 고정 나눗셈은 금지한다.

| 기술권 | 중앙 목표 | 허용 범위 |
|---|---:|---:|
| 중세 기반 | 30일 | 25~40일 |
| 초기 산업 | 90일 | 70~120일 |
| 성숙 산업 | 220일 | 180~280일 |
| 후기 룬 산업 | 360일 | 300~480일 |
| 시간 고정 선행 | 964일 | 850~1,100일 |

V26 통합 체크포인트의 누적 플레이타임은 Day 1 시작을 기준으로 계산한다. 순수 진행시간은 `(절대일 - 1) × 180초 ÷ 배속`이며 일시정지, 건설·생산 명령, UI 검토와 전투 의사결정 시간은 제외한다. `임시 체감 누적 플탐`은 아직 플레이 로그가 없으므로 정지와 배속 혼합을 합친 유효 진행 배율 x1.5~x2.5를 가정한 설계 범위다. 밸런스 확정값이 아니며 실제 플레이 측정으로 교체해야 한다.

| 절대일 | 예상 산업 수준 | x1 순수 누적 | x3 순수 누적 | x5 순수 누적 | 임시 체감 누적 플탐 |
|---:|---|---:|---:|---:|---:|
| 1 | 생존 기반·시작 재고 | 0분 | 0분 | 0분 | 0분 |
| 30 | 중세 기반 | 1시간 27분 | 29분 | 17분 24초 | 35~58분 |
| 120 | 초기 산업 정착 | 5시간 57분 | 1시간 59분 | 1시간 11분 24초 | 2시간 23분~3시간 58분 |
| 240 | 성숙 산업 | 11시간 57분 | 3시간 59분 | 2시간 23분 24초 | 4시간 47분~7시간 58분 |
| 400 | 후기 룬 산업 | 19시간 57분 | 6시간 39분 | 3시간 59분 24초 | 7시간 59분~13시간 18분 |
| 960 | 시간 고정 선행·최종 산업 직전 | 47시간 57분 | 15시간 59분 | 9시간 35분 24초 | 19시간 11분~31시간 58분 |

- 날짜가 연구를 직접 잠그지 않는다.
- 집중 연구는 한 계통을 앞당길 수 있지만 식량, 의료, 방어 중 최소 두 영역에서 실제 기회비용을 만들어야 한다.
- 연구 보상은 실제 시설, 아이템, 조합식 또는 장비를 열어야 한다.
- 연구 완료 후 해금 생산 기반을 실제로 가동할 수 있는 시점도 함께 측정한다.
- 초반 연구 보상은 5~20일, 중반은 20~60일, 후반은 60~180일 안에 투자 효과를 체감하는 것을 기준으로 한다. 문명 캡스톤은 예외다.

### 4.10 의료·질병·노화

- 기본 의료 용량은 인구 10명당 병상 1개다.
- 유행병 대응은 인구 5명당 격리·회복 자리 1개를 목표로 한다.
- 현재 작성된 15개 감염병의 완전 노출 1일 감염 확률 기준선은 7~25%다. 동일 질병에서 8시간 노출·환경 계수 0.50은 완전 노출 위험의 1/6이어야 한다.
- 백신은 면역 70에서 시작하고 하루 0.05씩 감소한다. 30일째 면역 68.5, 완전 노출 감염 위험은 무접종의 31.5% 이하를 현재 기준선으로 둔다.
- 같은 질병 확진 3명이 10일 안에 발생하면 유행을 선언하고 마지막 신규 확진 뒤 14일째 종료한다. 선언·종료 날짜와 면역은 저장 복원으로 변하지 않아야 한다.
- 무대응 감염은 확산되지만 기본 격리와 환기로 유효 재생산율이 1 미만이 되어야 한다.
- 백신과 고급 공중보건은 유효 재생산율 0.6 이하를 목표로 한다.
- 일반 부상은 1~3일, 중상은 5~20일 노동 공백을 기준으로 한다.
- 장기 재생 비용은 같은 역할의 대가급 주민을 새로 양성하는 비용의 40~70%를 기준으로 한다.
- 만성 관리는 완치보다 싸지만 영구 유지비를 요구한다.
- 시간 고정망은 후기 사회 총생산의 10~20%를 지속 소비해야 한다.

현재 수치 계약은 `PopulationHealthBalanceCalibrationScenario`가 16개 질병 에셋과 감염병별 100,000개 결정론 표본으로 검사한다. 단, 이 검사는 1회 노출과 유행 상태 계약의 기준이며, 방 구조·접촉망을 포함한 유효 재생산율은 별도 장시간 인구 시뮬레이션으로 검증해야 한다.

### 4.11 전투·방어·원정

동시대 장비와 합리적 진형을 갖춘 파티 기준:

| 조우 | 승률 목표 | 중증·영구 부상 |
|---|---:|---:|
| 일상 | 85~95% | 5% 미만 |
| 표준 | 65~80% | 10~20% |
| 위험 | 45~65% | 20~35% |
| 보스 초견 | 25~45% | 30~50% |
| 보스 정보·대응 준비 후 | 55~70% | 20~40% |

승률과 함께 다음을 기록한다.

- 라운드 또는 교전 시간
- 탄약, 약품과 식량 소비
- 장비 내구도 손실
- 치료·회복 작업량과 달력 지연
- 사망, 포획, 탈출과 목표 실패
- 획득 전리품 EWU와 비제작 고유 보상

단일 장비 조합이 전체 조우의 절반 이상에서 최적 조합과 5%p 이내 성능을 내면 지배 전략 후보로 판정한다. 대응 장비는 해당 조우에서 15%p 이상 유리할 수 있지만 다른 전장에서 10%p 이상의 기회비용을 가져야 한다.

캠페인별 적 능력치는 실제 원정 목표의 `requiredPower`를 권위로 삼는다. 기준 파워는 10이며 다음 계수를 적용한다.

```text
전투 능력치 계수 = clamp(sqrt(requiredPower / 10), 0.75, 3.00)
행동·기동 계수 = clamp(sqrt(전투 능력치 계수), 0.90, 1.55)
예상 위협 계수 = 전투 능력치 계수²
```

- 체력, 공격, 힘, 강인함과 사격은 전투 능력치 계수를 사용한다.
- 민첩과 이동은 행동·기동 계수를 사용해 후기 적이 선제권과 이동까지 과도하게 독점하지 않게 한다.
- 현재 6개 캠페인의 투영 위협 중앙값은 `502.2 / 720.0 / 1,148.0 / 1,423.8 / 2,849.4 / 4,197.6`이며 모든 다음 캠페인은 이전 캠페인보다 최소 5% 높아야 한다.
- 조우 에셋의 표시용 사이트 강도는 실제 적 스케일 권위가 아니다. 캠페인 순서, 목표 위험도와 `requiredPower`가 실제 권위다.
- 투영 위협은 콘텐츠 규모·증가 순서를 감사하는 정적 지표이지 승률이 아니다. 위 표의 승률 목표는 여섯 목표를 실제로 수행하는 동시대 파티의 결정론적 다중 시드 전투로 별도 통과해야 한다.

정적 전투 콘텐츠 감사 결과는 `Artifacts/QA/combat-content-balance.txt`에 기록한다.

초반 전투·원정 진입 계약은 다음과 같다.

- 1~9일의 자연 외부 위협은 비적대 사건만 허용하고, 강제 침입은 발생시키지 않는다.
- 10·20·30일에는 각각 25%·50%·75% 규모의 예행 침입을 사용하며 첫 일반 보스는 40일 이후다.
- 플레이어의 훈련, 지도 열람, 정찰과 목표 비교는 첫날부터 허용한다.
- 실제 원정 편성과 출발은 `research:survival:field-rations` 완료를 요구한다. 날짜 잠금이 아니므로 집중 연구로 앞당길 수 있지만 식량 보존과 물류 연구의 기회비용을 지불한다.
- UI 명령과 `OffenseExpeditionRuntime` 직접 실행 입구가 같은 연구 상태를 검사하며, 저장·화면 전환·직접 호출로 우회할 수 없어야 한다.

#### V26 초반 정착·원정 진입 변경 기록

```text
정의 ID: balance:early-settlement-combat-cadence-v26
콘텐츠 종류: 전투 일정·원정 시스템 진입
정의·카탈로그·실행기 위치: ExperiencePacingRuntime, InvasionThreatRuntime, OffenseExpeditionAccessRules, OffenseApplication, OffenseExpeditionRuntime
등장 시대와 연구: 1~30일 정착기, research:survival:field-rations
플레이어에게 주는 새 결정: 원정 정보를 미리 보고 생활·보급·훈련·연구 중 언제 출발 준비를 완료할지 선택
물리 BOM·입력·출력: 일정 변경 자체는 없음; 출발 이후 기존 원정 배급·장비·탄약·의약품 물리 비용 유지
직접 작업량과 계산 근거: 야전 식량학 132 연구 WU와 두 인과 선행 연구; 별도 날짜 대기 작업량 없음
EWU와 목표 회수 기간: 기존 원정 보상·회복 비용 계약 유지; 출발 잠금 자체는 자원이나 보상을 생성하지 않음
공간·전력·물·연료·정비: 기존 연구·식량 보존·창고·원정 준비 시설 비용 유지
위험·실패·회복 방식: 연구 전 출발 명령은 이유를 표시하고 무변경 실패; 지도·정찰·훈련은 계속 가능
사회·비가역 비용: 없음; 연구 기회비용과 출발 후 기존 사망·부상·관계 비용 유지
기존 대안과의 장단점: 날짜 잠금보다 집중 준비를 보상하지만 식량·물류 연구를 생략한 첫날 원정은 금지
지배 전략 방지 조건: 보호 기간이 자원·숙련·위협을 삭제하지 않으며 경비 대기로 전투 숙련을 얻지 못함
저장 권위와 실행 명령: 기존 절대 달력, BlueprintResearchState, OffenseCampaign/Expedition Aggregate; 별도 타이머 없음
자동 감사 ID와 전수 목록 포함 여부: ExperiencePacingDebugScenarios, OffenseExpeditionAccessDebugScenarios
검증 매트릭스와 보고서 위치: task_plan.md Phase 147, progress.md 2026-08-10 기록
현재 밸런스 상태: 공식 규칙·집중 결정론 검증 통과, 전체 저장 68/68/68 재인증과 실제 승률 보정은 보류
```

원정 준비 화면과 실제 출발 기록의 전투력은 다음 단일 투영을 사용한다.

```text
캐릭터 전투력 = 숙련에서 투영된 공격·힘·강인함·지구력·이동 × 현재 건강·부상·약물 상태
장비 전투력 = min(무기 기여 + 방어구 기여 + 방패 기여, 캐릭터 전투력 × 0.60)
원정 전투력 = Σ(캐릭터 전투력 + 장비 전투력), 최대 5명
```

- 장비는 실제 착용 인스턴스만 계산하며 재고에만 있는 장비는 기여하지 않는다.
- 무기는 피해·관통·공격 주기·사거리별 명중과 피해·품질·재질·내구·탄약 준비를 계산한다. 무기 기여 상한은 해당 캐릭터 전투력의 35%다.
- 방어구는 신체 부위별 피격 비중과 베기·관통·둔기 방어의 평균을 계산한다. 방어구 기여 상한은 30%다.
- 방패는 정면 막기 확률과 세 피해 유형 방어를 계산한다. 방패 기여 상한은 15%다.
- 총 장비 상한 60%는 최고 장비만 반복 제작해 숙련·건강·인원 준비를 대체하는 전략을 막는다. 장비 품질과 역사 진화는 상한 안에서 효율과 역할을 바꾼다.
- 탄약을 요구하는 무기가 비어 있으면 무기 기여는 절반만 인정한다. 실제 전투의 탄약 소비와 원정 보급 검사는 별도 권위로 계속 적용한다.
- 이 수치는 출발 가능성의 설명 지표다. 실제 승률은 목표·진형·탄약·상태 효과·전장 변형을 포함한 다중 시드 전투로 검증한다.

#### V26 원정 장착 전투력 변경 기록

```text
정의 ID: balance:expedition-loadout-power-v26
콘텐츠 종류: 원정 준비·전투력 투영
정의·카탈로그·실행기 위치: OffenseExpeditionService, OffenseEquipmentPowerRules, ICombatEquipmentRuntime, OffenseExpeditionRuntime, OffenseExpeditionLaunchService, OffenseExpeditionPanel
등장 시대와 연구: 야전 식량학 이후 모든 시대; 장비 자체 연구·제작·획득 조건 유지
플레이어에게 주는 새 결정: 숙련된 인물과 좋은 장비를 누구에게 집중할지, 탄약과 내구를 출발 전에 정비할지 선택
물리 BOM·입력·출력: 기존 장비 BOM·품질·내구·탄약을 읽기만 하며 새 자원이나 복제 출력 없음
직접 작업량과 계산 근거: 새 작업 없음; 기존 제작·수선·장전 WU와 숙련 성장 비용이 기회비용
EWU와 목표 회수 기간: 기존 장비 EWU·전리품 계약 유지; 표시 전투력이 금화·자원 보상을 만들지 않음
공간·전력·물·연료·정비: 기존 작업대·무기고·탄약·수선 시설 계약 유지
위험·실패·회복 방식: 저품질·파손·미장전 장비는 낮게 투영되고 실제 전투에서도 기존 규칙대로 불리함
사회·비가역 비용: 숙련 주민의 부상·사망·원정 공백과 고유 장비 유실 위험 유지
기존 대안과의 장단점: 숙련은 모든 장비의 기반 효율, 장비는 역할·상성·준비 보정; 어느 한쪽도 다른 쪽을 완전히 대체하지 않음
지배 전략 방지 조건: 총 장비 60%, 무기 35%, 방어구 30%, 방패 15% 상한; 파티 최대 5명 유지
저장 권위와 실행 명령: 캐릭터 숙련·건강 Aggregate, CombatEquipment 인스턴스·로드아웃, Expedition snapshot; 별도 전투력 저장 권위 없음
자동 감사 ID와 전수 목록 포함 여부: OffenseEquipmentPowerDebugScenarios; 실제 장비 에셋 전수 분포 감사는 후속 체크포인트 프로브에 포함
검증 매트릭스와 보고서 위치: task_plan.md Phase 147, progress.md 2026-08-10 기록
현재 밸런스 상태: 공식 규칙·집중 결정론 검증 통과; 장기 인구·장비 획득·실제 승률 통합 보정은 보류
```

### 4.11.1 인구·숙련·장비 통합 체크포인트

원정 전투력과 정착지 노동력은 총인구 하나로 비교하지 않는다. 다음 네 수치를 분리한다.

- 총인구: 모든 생존 주민
- 성인 생산인구: 미성년·완전 활동 불가 상태를 제외하고 일반 작업에 참여 가능한 주민
- 부양인구: 미성년과 장기 치료 등으로 일상 생산에 참여하지 못하는 주민
- 전투 준비 인구: 원정 참가 조건, 유효 전투 숙련, 건강, 무기와 최소 보호 장비를 모두 갖춘 성인

출생자는 종족별 성년일까지 부양인구다. 미성년 생물학적 나이는 게임 하루에 4일 증가하므로 균사체 180일, 슬라임 240일, 코볼트 300일, 오크·수인·하피 420일, 악마·뱀파이어·인간 540일 뒤에야 출생 세대가 성인 노동력으로 전환된다. 골렘은 조립 과정 완료 뒤 즉시 성인으로 분류한다. 따라서 240일 이전 성인 생산인구 증가는 주로 성인 영입·포로 영입·골렘 조립에서 나와야 하며, 생물 출생을 즉시 노동력으로 계산하지 않는다.

| 절대일 | 총인구 목표 | 성인 생산인구 목표 | 부양인구 목표 | 전투 준비 인구 목표 | 기준 원정 |
|---:|---:|---:|---:|---:|---|
| 1 | 3 | 3 | 0 | 2 | 외부 원정 잠금, 내부 훈련만 |
| 30 | 3~6 | 3~6 | 0~2 | 2~4 | 식재료 농장 / 상단 교역로 |
| 120 | 6~14 | 5~12 | 1~4 | 3~7 | 낡은 무기고 |
| 240 | 12~28 | 8~20 | 4~12 | 5~12 | 마력 유적 |
| 400 | 25~60 | 15~40 | 10~25 | 10~24 | 경쟁 던전 전초기지 |
| 960 | 80~220 | 55~160 | 25~70 | 25~70 | 봉인된 진실의 심장부 |

이 표는 실측 결과가 아니라 장시간 시뮬레이션이 맞춰야 할 이론 목표 밴드다. 종족·정책에 따라 범위 안 구성이 달라질 수 있다. 기준 원정 비교에는 실제 카탈로그 장비 스냅샷과 숙련 투영을 사용하고, 캠페인 `requiredPower`로 캐릭터 능력치를 역산하지 않는다. 파티 밖 전투 준비 인구는 부상 교대·방어 잔류·다중 원정의 회복 탄력성으로만 평가한다.

#### V26 인구·숙련·장비 체크포인트 변경 기록

```text
정의 ID: balance:population-power-checkpoints-v26
콘텐츠 종류: 인구 성장·숙련 성장·장비 획득·원정 준비 통합 기준
정의·카탈로그·실행기 위치: SpeciesLifeHistory, Reproduction, Recruitment/Captivity, CharacterProficiency, CombatEquipment, OffenseCampaignCatalog
성장 시대와 연구: 1/30/120/240/400/960일; 기존 연구 도달 시점과 장비 requiredResearchId 사용
플레이어에게 주는 새 결정: 출생 부양, 성인 영입, 골렘 조립, 훈련, 장비 생산과 원정 투입 사이의 인력·자원 배분
물리 BOM·입력·출력: 기존 생식·영입·골렘·훈련·장비 BOM만 사용; 체크포인트가 자원이나 인구를 생성하지 않음
직접 작업량과 계산 근거: 기존 99 WU/성인·일, 0.08 XP/WU, 안전 전투 훈련 2 XP/일, 실제 장비 제작·수리 WU
EWU와 목표 회수 기간: 성인 생산인구 목표 밴드로 정착지 EWU를 산정하며 전투 배치 시간은 생산 기회비용으로 차감
공간·전력·물·연료·정비: 실제 인구의 주거·급식·의료·훈련 공간과 장비 생산 기반 비용 유지
위험·실패·회복 방식: 미성년 부양 기간, 질병·부상, 장비 파손·탄약 부족, 영입 지연과 원정 실패
사회·비가시 비용: 보호자·교사·의료·경비 잔류와 세대 숙련 손실
기존 대안과의 장단점: 출생은 느리지만 계보를 만들고, 성인 영입은 빠르지만 서비스·외교 조건을 요구하며, 골렘은 즉시 성인이지만 제작 기반을 요구함
지배 전략·악용 방지 조건: 파티 5명 상한, 출생 즉시 노동 금지, 목표 전투력 역산 금지, 장비 기여 60% 상한
저장 권위와 실행 명령: 기존 인구·생애·숙련·장비·원정 Aggregate; 보고서와 파생 체크포인트는 저장하지 않음
자동 감사 ID와 필수 목록 포함 여부: SettlementPopulationPowerCheckpointDebugScenarios
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-population-power-checkpoints.md, task_plan.md Phase 147
현재 밸런스 상태: 이론 목표 밴드·비순환 결정론적 체크포인트 6개 통과; 장시간 실제 인구 시뮬레이션과 다중 시드 전투 승률 보정은 보류
```

#### V26 일반 영입 처리량·후발 숙련 변경 기록

```text
정의 ID: balance:regular-recruitment-throughput-v26
콘텐츠 종류: 손님 영입, 인구 성장, 후발 주민 숙련 보정
정의·카탈로그·실행기 위치: RegularCustomerRules, RegularCustomerState, RegularCustomerRuntime, RecruitedCharacterActivationService
성장 단계와 연구: 첫 일반 영입은 방문·만족 조건만으로 가능하고, 이후 일반·용병 영입은 마지막 성공 영입일로부터 10일 간격을 요구한다.
플레이어에게 주는 새 결정: 빠른 성인 노동력 한 명을 지금 영입할지, 더 적합한 후보를 기다릴지 선택한다. 후발 성인은 캠페인 경험에 맞는 두 전문 숙련만 보정되고 나머지 숙련은 실제 작업으로 길러야 한다.
물리 BOM·입력·출력: 새 물품을 생성하지 않는다. 영입자는 기존 주거·음식·임금·장비 수요를 그대로 추가하며, 용병은 기존 선급 비용을 지불한다.
직접 작업량·효과 계산 근거: 10일 전역 간격은 30일까지 최대 3명, 120일까지 최대 12명, 240일까지 최대 24명의 외부 성인 유입 상한을 만든다. 캠페인 완료 목표 수 0/1/2/3/4+에 따른 상위 두 숙련 하한은 0/100/250/400/600 XP다.
EWU와 목표 회수 기간: 숙련 보정은 전문가 1,200 XP와 대가 3,000 XP에 도달하지 않으며, 기술자 이후 성장은 0.08 XP/WU의 실제 작업 권위를 따른다.
공간·전력·물·연료·정비: 영입 자체가 이를 면제하지 않는다. 늘어난 주민은 기존 시설 수용량과 운영 자원을 소비한다.
위험·실패·회복 방식: 쿨다운 중 후보는 소모·활성화되지 않고 다음 가능 일자를 표시한다. 저장 복원 후에도 성공 영입일을 기준으로 같은 결과를 낸다.
사회·비금전 비용: 후보 대기, 방문 만족 유지, 임금과 장비 배정, 기존 주민과의 관계 형성 비용이 남는다.
기존 대안과의 장단점: 출생은 느리지만 세대·유전 서사를 만들고, 포로 영입은 위험과 관계 비용을 지며, 골렘은 제작망을 요구한다. 일반 손님 영입은 빠르지만 전역 10일 간격과 전문 분야 두 개 제한을 가진다.
지배·악용·입력 방지 조건: 저장 재시작, 후보 교체, 일반/용병 경로 전환으로 쿨다운을 초기화할 수 없다. 후발 숙련 보정은 모든 9종 숙련이나 전문가·대가 등급을 무료로 지급하지 않는다.
저장 권위와 실행 명령: 기존 regular-customer 저장 레코드의 recruitedAbsoluteDay와 캐릭터 서사 Aggregate의 현재·평생 숙련 XP가 권위다. 별도 인구 수치나 숙련 캐시를 저장하지 않는다.
자동 감사 ID와 필수 목록 포함 여부: RegularCustomerDebugScenarios, RecruitmentRulesDebugScenarios
검증 매트릭스와 보고서 위치: task_plan.md Phase 147, progress.md, findings.md
현재 밸런스 상태: 10일 경계와 숙련 하한 순수 규칙·저장 집중 검증 통과. 일반 영입·동일 계보 출생·자연사·숙련 작업량을 묶은 1~960일 256시드 정책 범위 검증 통과. 장비 생산 처리량 체크포인트도 통과했으며 포로·세력·골렘 유입과 실제 수용력은 후속 통합 게이트다.
```

#### V26 번식 성공 권위·인구 노동 다중 시드 변경 기록

```text
정의 ID: balance:population-labor-multiseed-v26
콘텐츠 종류: 번식 성공 판정, 종족별 성년·노화, 일반 영입, 숙련 성장과 장기 노동력
정의·카탈로그·실행기 위치: ReproductionProfileSO, ReproductionProcess, SpeciesLifeHistorySO, RegularCustomerRules, ProficiencyProgressionRules
성장 단계와 연구: 1/30/120/240/400/960일; 별도 날짜 해금은 추가하지 않고 실제 영입·번식·성년·사망 규칙만 투영한다.
플레이어에게 주는 새 결정: 성인 영입 속도, 번식 승인 빈도, 현재 노동력과 장기 부양·계보 투자 사이의 선택
물리 BOM·입력·출력: 기존 번식 시설·치료제·주거·급식·임금·보육 비용을 유지한다. 시뮬레이터는 물건이나 주민을 라이브 월드에 생성하지 않는다.
직접 작업량·효과 계산 근거: 모든 비골렘 번식 프로필은 1일 Attempt 단계에서 baseSuccessChance × 건강·영양 × 연령 가임성 판정을 먼저 수행한다. 생략 시 카탈로그 오류다. 성인 1명은 중립 99 WU/일, 실제 0.08 XP/WU와 등급별 속도 0.85~1.25배를 사용한다.
EWU와 목표 회수 기간: 균형 정책은 일반 성인 15일 간격, 번식 평가 40일 간격이다. 중앙 총인구는 1/30/120/240/400/960일에 3/5/11/20/30/64명이다.
공간·전력·물·연료·정비: 안전 환경·건강·영양을 가정한 상한 범위이며 실제 수용력·자원 부족은 결과를 낮춘다.
위험·실패·회복 방식: 수태 실패, 노년 질환의 첫 발생과 4년 중증 진행, 성년 전 부양 기간, 노년 25% 안전 작업만 계산한다. 생식 치료와 응급 적출은 사용하지 않는다.
사회·비금전 비용: 같은 계보 성인 후보만 공급하는 격리 시나리오이므로 실제 다문화 후보 희소성과 관계 비용은 후속 PlayMode 압력으로 남긴다.
기존 대안과의 장단점: 보수 정책 960일 중앙 33명, 균형 64명, 확장 100명이다. 균형 정책의 목표 하한 80명 부족분 16명은 포로·세력 합류·골렘 조립에서 평균 60일마다 성인 약 1명을 확보하면 메울 수 있다.
지배·악용·입력 방지 조건: baseSuccessChance를 우회하는 에셋 금지, 숨은 인구 성장률 금지, 출생 즉시 성인 처리 금지, 일반 영입 10일 전역 하한 유지
저장 권위와 실행 명령: 기존 생애·번식·손님·숙련 Aggregate만 사용한다. 시뮬레이션 결과는 QA 파생 보고서이며 저장하지 않는다.
자동 감사 ID와 필수 목록 포함 여부: SettlementPopulationLaborSimulationDebugScenarios; 생물 번식 프로필 전수 Attempt 선행 검사 포함
검증 매트릭스와 보고서 위치: 정책 3종 × 시작 종족 3종 × 256시드 × 960일, Artifacts/QA/v26-population-labor-multiseed.md
현재 밸런스 상태: 일반 영입+동일 계보 번식+자연사+숙련 노동 범위와 장비 생산·전투 준비 처리량 검증 통과. 기타 성인 유입, 주거·식량·의료 수용력과 실제 PlayMode 장시간 검증은 보류
```

#### V26 장비 생산·전투 준비 처리량 변경 기록

```text
정의 ID: balance:equipment-readiness-throughput-v26
콘텐츠 종류: 전투 준비 인구 최소 장비, 기준 원정 파티 장비, 물리 생산 처리량
정의·카탈로그·실행기 위치: CombatEquipmentDefinitionSO, ProductionRecipeSO, V23BalanceWorkCalculator, V23EmbeddedWorkValueCalculator, CombatEquipmentCraftingRuntime, SettlementEquipmentReadinessThroughputDebugScenarios
성장 단계와 연구: 1/30/120/240/400/960일; 완제품뿐 아니라 기본 재료와 최저 EWU 상류 조합식의 통합 연구 선행 폐쇄를 사용한다.
플레이어에게 주는 새 결정: 신규 전투 준비 인구에는 최소 장비를 우선 지급하고, 최신 고급 장비는 필요한 원정 파티에 집중하거나, 생존·연구·건설 노동을 보존한다.
물리 BOM·입력·출력: 실제 기본 재질 수량과 필수 부품을 사용한다. Day 1 창·천 후드는 시작 재고이며 사전 제작으로 간주하지 않는다. 이후 최소 준비 세트는 보통 창+천 후드, 기준 원정 세트는 실제 장착 가능한 시대별 무기·방어구·방패다.
직접 작업량·효과 계산 근거: 성인 1명 99 WU/일, 이전 체크포인트 최소 생산인구, 성장·생산 하한 배분 35%, 등급별 작업 속도와 실제 장비 직접 WU를 사용한다. 전담 제작자 1명의 창구 압력과 정착지 전체 생산 압력을 분리한다.
EWU와 목표 회수 기간: 상류 물리 조합식의 최저 EWU 경로와 품질 재시도 기대비용을 포함한다. Day 30/120/240/400/960 기준 원정 세트는 각 기간 성장·생산 하한의 32.5%/24.2%/75.6%/90.9%/27.1%이며, 신규 준비 인구 최소 세트는 0%/2.3%/2.0%/2.3%/1.1%다.
공간·전력·물·연료·정비: EWU의 상류 시설·물류·기반 비용을 포함하되 실제 시설 동시 점유와 정비 정지는 후속 라이브 생산 시뮬레이션에서 측정한다.
위험·실패·회복 방식: 품질 미달 시 최대 10회 정책과 결정론적 품질 분포를 계산한다. 총투입 회수는 인정하지 않고 거부품 해체가 줄이는 순재료비만 후속 라이브 생산에서 별도 측정한다.
사회·비가시 비용: 원정대 고급 장비 집중은 예비 방어대의 장비 질과 제작자·연구자 가용 노동을 줄인다. 전투 준비 인구 전원을 최신 장비로 자동 갱신하지 않는다.
기존 대안과의 장단점: 값싼 최소 세트는 예비 인원을 빠르게 늘리지만 후기 원정 성능을 대체하지 못한다. 성장형·동력·룬 장비는 원정 파티를 강화하지만 성장 골격과 정밀·룬 생산망의 높은 기회비용을 요구한다.
지배·악용·입력 방지 조건: 전투 준비 인구 증가와 최신 원정 장비 수요를 분리하고, 기존 장비를 삭제·무료 업그레이드·무료 회수하지 않는다. 양손 무기와 방패처럼 실제 장착 불가능한 조합은 로드아웃 권위가 거부한다. 흡수된 연구 ID를 별도 연구로 부활시키지 않고 V21 단일 통합 ID로 정규화한다.
저장 권위와 실행 명령: 기존 장비 인스턴스·재고·조합식·연구·인구 Aggregate가 권위다. 체크포인트 비용과 보고서는 저장하지 않는다.
자동 감사 ID와 필수 목록 포함 여부: SettlementEquipmentReadinessThroughputDebugScenarios, SettlementPopulationPowerCheckpointDebugScenarios, ResearchEquipmentOverhaulDebugScenarios, requiredResearchId 흡수-ID 전수 검사
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-equipment-readiness-throughput.md, Artifacts/QA/v26-population-power-checkpoints.md, task_plan.md Phase 147
현재 밸런스 상태: 실제 BOM·직접 WU·EWU·품질·연구를 사용한 6개 처리량 체크포인트와 비순환 전투력 체크포인트 통과. 180 연구/장비 감사 통과, 흡수 연구 ID 잔존 0건. 실제 시설 경합·재고·해체 순회수·수리·손실을 포함한 장시간 PlayMode 생산 및 다중 시드 전투 승률은 보류한다.
```

### 4.12 장비와 역사 진화

- 품질 한 단계의 평균 성능 증가는 8~12%를 목표로 한다.
- 재료 특화 이점은 10~25%다.
- 성장형 장비의 기본 성능 -12%를 유지한다.
- 부품과 역사 노드를 완성하면 동시대 일반 장비보다 10~20% 강해질 수 있다.
- 역사 노드 하나는 범용 5~10% 또는 조건부 15~25% 효과를 목표로 한다.
- 전체 공명 능력의 범용 성능 증가는 25% 이내, 특정 서사 조건의 순간 이점은 40% 이내다.
- 전설 품질, 희귀 부품과 역사 진화를 모두 갖춰야만 표준 조우를 해결할 수 있게 만들지 않는다.

### 4.13 포로·영입·흥행

- 포로 노동의 순노동 효율은 일반 주민의 35~60%다.
- 간수, 음식, 도구, 치료, 탈출과 외교 비용을 모두 포함한다.
- 영입은 10~30일 관리·설득 비용과 실패 가능성을 요구한다.
- 가혹한 처우는 단기 공포·수익을 주지만 관계, 주민 기분, 세력 원한과 보복 비용을 남긴다.
- 처형, 석방, 교환, 노동, 흥행과 영입 중 하나가 모든 상태에서 우월하면 안 된다.

### 4.14 사회·가족·문화

- 일반 특성 하나의 평시 생산 영향은 대체로 ±3~8%다.
- 강한 특성도 단독으로 ±12%를 넘지 않는 것을 기준으로 한다.
- 가까운 가족 한 명의 사망은 운영 가능해야 한다.
- 10일 안에 3명 이상 사망하면 장례·상담 없이 집단 붕괴 가능성이 생겨야 한다.
- 아동 교육의 성인 노동 투자비는 성년 경력의 1/3 안에 회수하는 것을 목표로 한다.
- 멘토링은 현재 생산을 줄이지만 다음 세대 숙련 손실을 30~50% 완화해야 한다.
- 문화 적응은 시설, 음식, 행사와 생활 참여 비용을 요구하고 장기 통합 효율과 교환한다.
- 특정 문화가 서비스, 기후, 전투와 세대 운영을 모두 지배할 수 없다.

### 4.15 숙련·경력·멘토링

- 숙련 권위는 현장 작업, 건설·공학, 제작, 식량 생산, 학술, 의료, 사교, 근접 전투, 원거리 전투의 9종이다.
- 대등급은 견습·숙련자·기술자·전문가·대가를 유지하고 각 대등급 내부를 `IV → III → II → I` 네 구간으로 나눈다. 다음 진행은 `견습 I → 숙련자 IV` 순서다. 요구 조건은 기존 대등급을 계속 사용하고 UI·초기 생성·미세 조정은 소등급을 사용한다.
- 캐릭터 생성은 아홉 숙련의 `15~45 XP` 바닥값에 출신과 과거 이력 보너스를 더한다. 과거 이력은 주전문과 부전문을 반드시 하나씩 지정하며, 출신은 더 작은 보조 숙련 보너스를 제공한다. 별도의 초기 능력치 주사위는 없다.
- 시작 생물학적 나이는 종족별 성년·노년 나이에 상대적으로 생성한다. 젊은 성인·경력 성인·베테랑·노년 구간의 개별 숙련 상한은 각각 `99/174/249/399 XP`이며, 어떤 출신·이력 조합도 이 상한이나 기술자 등급을 넘지 못한다.
- 나이가 많을수록 주전문·부전문의 경력 보너스가 커지지만, 노년 시작자는 기존 종족별 노화성 질환 확률과 노화성 질환 Aggregate를 그대로 사용한다. 시작 숙련을 위해 별도의 건강 페널티 수치를 만들지 않는다.
- 저장하고 성장·쇠퇴시키는 직무 능력은 아홉 숙련 XP뿐이다. 공용 캐릭터 레벨은 서사 기술과 선택지를 열지만 숙련이나 별도 능력치를 올리지 않는다.
- 중립 주민의 실효 99 WU/일과 `0.08 XP/WU`를 기준으로 이론 도달일은 숙련 13일, 기술자 51일, 전문가 152일, 대가 379일이다.
- 실제 운영 목표는 휴식·욕구·병목을 포함해 각각 15~20일, 60~80일, 180~240일, 450~600일이다.
- 등급별 속도 배율은 `0.85/0.95/1.05/1.15/1.25`, 품질 점수는 `25/40/58/78/95`, 사고 배율은 `1.25/1.10/1.00/0.80/0.65`다.
- 속도·사고 배율과 품질 점수는 대등급 기준점 사이를 현재 XP로 연속 보간한다. 소등급은 그 연속 구간을 4등분해 표시·초기 상한·미세 조정에 사용하며, 별도의 관련 능력치 25% 항은 사용하지 않는다.
- 근접·원거리 전투, 운반 한계와 의료 위험처럼 기존 코드가 숫자형 성능값을 요구하는 곳은 현재 숙련을 읽는 호환 투영값을 사용한다. 이 값은 독립 생성·성장·쇠퇴·저장하거나 플레이어에게 별도 성장축으로 표시하지 않는다.
- 숙련은 BOM이나 물리 재료를 감소시키지 않고 속도·완성 품질·사고 위험 및 해당 전투 행동의 성능만 바꾼다.
- 난도 계수는 상위 1.25, 적정 1.0, 한 단계 쉬움 0.55, 두 단계 이상 쉬움 0.20이다. 결과 계수는 성공 1.0, 부분 0.6, 안전 실패 0.3, 사고·강제 중단 0.1이다.
- 자동 품질 반복은 1~3회 100%, 4~10회 50%, 이후 15% XP만 주며 해체 XP는 원 제작의 20%가 상한이다.
- 전문가는 15일 유예 뒤 시간당 0.25 XP, 대가는 5일 유예 뒤 0.10 XP 쇠퇴한다. 기술자 이하는 쇠퇴하지 않는다.
- 전투 숙련은 종류별 하루 8 XP, 훈련은 하루 2 XP가 상한이다. 경비 대기·무효 피해·쓰러진 적 반복 공격·아군 공격은 0 XP다.
- 멘토 수업은 참가자마다 30 WU를 소비하며 학생 보너스는 `min(10, 2 + 당일 실습 XP × 0.35) × 관계 계수`다.
- 멘토링의 생산 기회비용을 포함한 세대 숙련 손실 완화 목표는 30~50%다.
- 숙련 추가·변경 시 31개 작업 전수 분류, 취소·대기 중복 방지, 960일 도달·쇠퇴, 2,000명 지연 정산과 저장 복원 결정을 함께 검증한다.
- V25의 100,000회 품질 표본, 960일 쇠퇴, 2,000명 정산, 콘텐츠 전수 연결, UI와 저장 증거는 이전 기준선이다. V26 단일 숙련 권위 변경 뒤에는 같은 묶음을 다시 통과하기 전까지 `밸런스 완료`로 표시하지 않는다.

#### V26 단일 숙련 권위 변경 기록

```text
정의 ID: balance:character-proficiency-single-authority-v26
콘텐츠 종류: 캐릭터 성장·작업·전투 공통 규칙
정의·카탈로그·실행기 위치: CharacterProficiencyDomain, CharacterNarrativeAggregate, WorkTaskExecutor, CharacterStatsProjectionService
등장 시대와 연구: 전 시대, 연구 잠금 없음
플레이어에게 주는 새 결정: 시작 숙련 분포와 실제 작업·훈련·멘토 배정을 보고 주민을 육성
물리 BOM·입력·출력: 작업별 기존 BOM 유지, 승인 WU를 숙련 XP로 변환
직접 작업량과 계산 근거: 0.08 XP/WU, 기존 작업 WU를 이중 계산하지 않음
EWU와 목표 회수 기간: 숙련 15~20일, 기술자 60~80일, 전문가 180~240일, 대가 450~600일
공간·전력·물·연료·정비: 작업과 멘토 학원의 기존 물리 조건 유지
위험·실패·회복 방식: 등급별 사고 배율, 전문가·대가 쇠퇴, 훈련 일일 상한
사회·비가역 비용: 멘토·학생 각각 30 WU, 장기 비활동 시 전문가 이상 강등
기존 대안과의 장단점: 12종 독립 능력치 성장 제거, 9종 숙련으로 이해 가능성 향상
지배 전략 방지 조건: 반복 XP 감쇠, 난도 감쇠, 전투·훈련 일일 상한
저장 권위와 실행 명령: 현재·평생 숙련 XP는 인물 서사 Aggregate, 시작 숙련은 캐릭터 성장 상태
자동 감사 ID와 전수 목록 포함 여부: v26-proficiency-single-authority, 31개 작업과 전체 숙련 프로필

#### V26 창립자 나이·출신·이력 숙련 변경 기록

변경 ID: `balance:founder-age-background-proficiency-v26`

콘텐츠 종류: 새 게임 창립자 생성, 숙련 소등급, 출신, 과거 이력, 생물학적 나이, 초기 노화성 질환

성장 단계: 게임 시작 전 준비 화면과 day 1 초기 상태

플레이어에게 주는 새 결정: 젊고 건강하지만 성장 여지가 큰 인물과, 시작 숙련이 높지만 노화 위험을 가진 인물 사이에서 주전문·부전문 조합을 보고 선택

물리 BOM·입력·출력: 물리 입력·완제품 출력 없음. 시작 XP는 도착 이전 경력의 기록이며 재료·아이템·연구·생산 주문을 생성하지 않음

직접 작업량과 내재 작업량: 런 이전 서사 경력이라 현재 정착지 WU를 소모하지 않는다. 런 시작 뒤 기본 `0.08 XP/WU`에 주전문 x1.50, 부전문 x1.20, 기타 x1.00 학습 배수를 적용하며 작업·전투·훈련·멘토링 획득 XP에 동일하게 작동한다. 기존 전투·훈련 일일 상한과 멘토링 기회비용은 유지하고 시작 패킷·캠페인 영입 보정은 학습으로 보지 않아 배수를 적용하지 않는다

시간·공간·위험: 나이는 종족별 성년·노년 기준에 상대적이며 젊은 성인/경력 성인/베테랑/노년의 개별 숙련 상한은 99/174/249/399 XP. 시작 노년층의 65~80%는 경증 노화성 질환 1개 이상, 25~45%는 복수 질환을 목표로 하며 비노년층은 시작 질환이 없다. 질환은 기존 신체 5% 손상·진행·치료 경로를 사용

대안과 악용 가능성: 출신과 이력을 따로 뽑아 조합 다양성을 주되, 모든 보너스는 나이 상한을 마지막에 적용한다. 리롤 그룹 사이에 주전문 XP·나이·건강을 따로 보존할 수 없고 기술자 이상 시작은 금지한다. 무한 수동 리롤은 허용된 플레이어 최적화이며 기준 기대값은 무리롤 자연 분포로만 산출한다

실행 경로: 시작 준비 생성 → 성장 상태 스냅샷 → 게임 적용 → 생애 등록 및 기존 노화성 질환 이벤트 → 현재 숙련은 서사 Aggregate에 단일 등록

저장 권위: 준비 프로필과 시작 숙련은 기존 캐릭터 성장 상태, 실제 생물학적 나이·노화성 질환은 기존 CharacterLife Aggregate. 별도 현재 숙련·건강 저장 권위 없음

자동 감사: `v26-founder-profile`에서 소등급 경계, 나이 상한 단조성, 주전문/부전문 우선순위, 9종 이력 커버리지, 결정론, 생애 등록, 저장 복원과 주인공 고정·후보 6명 중 2명 선택의 3인 역할 커버리지를 검증

현재 상태: 소등급·출신/이력·나이 상한·주전문 x1.50/부전문 x1.20 학습·생애/건강 투영 구현과 집중 검증을 통과했다. 노년 875명 중 건강 문제 1개 이상은 74.9%, 복수는 35.8%이며 비노년은 0%다. 특정 주전문을 가진 건강한 노년은 특성 필터 전 자연 후보 7명 로스터의 약 1%다. 무리롤 20,000개 로스터에서 균형 선택은 핵심 4분야 전문 커버리지를 10.8%에서 48.7%로 높이고, 선택 노년 비중은 5.0%에서 4.7%, 건강 문제 수는 3,461건에서 2,560건으로 낮춘다. 실제 준비 화면 PlayMode 시각 검증, 전체 월드 저장 왕복과 성장 배수를 포함한 실제 초기 생산 시간 재계산 전에는 전체 밸런스 완료로 보고하지 않음. 증거: `Artifacts/QA/v26-founder-starting-profile.md`
검증 매트릭스와 보고서 위치: CharacterProficiencyDebugHarness 및 V26 후속 QA 보고서
현재 밸런스 상태: 수식·권위 구현, 전체 콘텐츠·UI·68섹션 회귀 재검증 전

#### V26 창립자 가중 특성 변경 기록

변경 ID: `balance:founder-weighted-traits-v26`

콘텐츠 종류: 새 게임 일반 특성 56종, 특성 생성 수, 희귀도, 계열 충돌, 종족 적합성, 경험치 성장 효과

성장 단계: 게임 시작 전 주인공과 직원 후보 생성부터 저장된 인물의 이후 작업·학습·전투·생활까지

플레이어에게 주는 새 결정: 특성이 적지만 단점이 작은 인물, 여러 혼합 특성을 가진 인물, 낮은 확률의 강한 특성을 가진 인물 사이에서 주전문·나이·건강과 함께 선택

물리 BOM·입력·출력: 특성 자체는 물리 자원과 완료 작업을 만들지 않는다. 작업·연구·학습·전투·소비·사고 배수는 기존 실제 작업량, 식량 소비, 전투와 사고 경로에만 적용한다

직접 작업량과 내재 작업량: 생성 비용은 없지만 작업·학습 이득은 실제 승인 WU와 획득 XP가 있을 때만 발생한다. 빠른 학습자는 시작 XP를 만들지 않고 이후 획득 XP만 증폭한다

시간·공간·위험: 자연 생성 특성 수 가중치는 1/2/3/4개에 15/40/35/10%이며 기대값은 2.40개다. 강한 순이득 특성은 일반 특성보다 낮은 희귀도 가중치를 갖고, 모든 특성은 하나의 기능 계열에 속한다

대안과 악용 가능성: 같은 계열은 한 인물에게 하나만 허용하고 명시적 상극과 종족 적합성을 추가로 검사한다. 네 번째 슬롯이 같은 종류의 생산 배수를 중첩시키지 못하며, 무한 수동 리롤은 허용하되 보고값은 무리롤 자연 분포로 산출한다

실행 경로: 콘텐츠 카탈로그 일반 특성 → 개수 가중 추첨 → 희귀도 가중 무복원 선택 → 계열/상극/종족 검증 → 기존 성장 상태 traitIds → UI/작업/학습/전투/생활 조회

저장 권위: 기존 `CharacterGrowthState.traitIds`만 사용한다. 희귀도·계열·종족 적합성은 정의 에셋 권위이며 저장 복사본을 만들지 않는다

자동 감사: `v26-founder-traits`에서 100,000명 분포, 100종 도달, 개수 밴드, 희귀도·극한형 감쇠, 계열·상극·종족 중복 금지, 시작 숙련 XP, 네 특성 저장·UI 투영과 결정론을 검증한다

현재 상태: 56개 일반 특성 에셋에 희귀도·계열·종족 조건을 작성했고 1~4개 가중 선택, 네 특성 저장/UI, 빠른 학습자 승인 작업 XP x1.30과 주요 레거시 수치 재조정을 구현했다. Unity MCP 100,000명 감사 결과 1/2/3/4개가 15.203/40.029/34.664/10.104%, 평균 2.397개였고 일반/고급/희귀/특별 개별 출현율은 6.12/3.43/1.57/0.66%였다. 56/56 도달, 계열 충돌 0, 비슬라임 종족 누출 0, 네 특성 저장 왕복, V20 회귀와 Console Error 0 / Warning 0이 통과했다. 이 특성 분포를 반영한 최초 3인방 생산량 재계산은 다음 전게임 산업 밸런스 단계에서 진행한다
```

### 4.16 손님·세력·계약·사건

- 정기 세력 계약 비용은 해당 기간 총생산의 1~3%다.
- 위기 계약은 3~8%다.
- 전략 계약은 희귀 자산 또는 총생산의 5~15%다.
- 정적 계약 감사의 기준 정착지는 성인 생산인구 12명, 성인당 하루 99 WU, 생산·성장 가동률 42.5%다. 기준 기간 생산량은 `12 × 99 × 계약 기한 × 0.425` EWU로 계산한다.
- 이 12명 기준은 콘텐츠 에셋의 요구량을 비교하기 위한 이론 기준점이다. 실제 플레이 UI에서는 현재 성인 생산인구와 최근 10일 실효 생산량으로 예상 부담률을 다시 표시하며, 연구·시설·재고가 없어 이행 불가능한 계약은 그 원인을 수락 전에 알려야 한다.
- 비제작 유물·계보 인장처럼 비가역 자산을 요구하는 계약은 EWU 비율에서 제외하고 자산 종류, 수량, 재획득 경로와 기회비용을 별도 감사한다. 한 계약의 기본 요구는 고유 자산 1개를 넘지 않는다.
- 한 런에서 획득량이 유한한 고유 자산은 모든 필수 소비처와 선택 소비처의 최대 요구량을 합산한다. 필수 진행에 필요한 총량이 획득 가능한 총량을 넘으면 실패이며, 서로 다른 장기 목표가 같은 자산을 요구할 때는 모든 목표를 한 저장에서 달성할 수 있는 대체재·반환·재획득 경로가 있어야 한다.
- 세력 무상 화물의 장기 유입률은 기준 정착지 하루 생산량 `504.9 EWU` 대비 교역 카라반 5%, 보급 카라반 10%를 넘지 않는다. 쿨다운은 `ceil(화물 EWU ÷ (504.9 × 허용 비율))`로 정하고 교역 최소 7일, 보급 최소 20일을 강제한다.
- 지원군 요청은 의무 토큰 1개를 성공적인 경로 생성 시 소비하고 같은 세력의 지원군 요청 사이에 최소 10일을 둔다. 경로·자격 검증 실패는 토큰이나 쿨다운을 소비하지 않는다.
- 계약 보상은 평균 2~4회 이행 후 장기 손익분기에 도달한다.
- 소형 사건 비용은 0.25~0.75 WD다.
- 일반 사건 비용은 1~3 WD다.
- 대형 사건 비용은 5~15 WD와 실제 위험을 요구한다.
- 균형 상태에서 선택지 기대가치 차이는 15% 이내를 목표로 한다.
- 특정 준비 상태에서는 한 선택이 25% 이상 유리할 수 있어야 한다.
- 중요 사건은 정상 시기 2~5일마다 1회, 위기 시기 1~2일마다 1회까지 허용한다.
- 세력 장 지원안의 물리 비용을 100%로 보았을 때 협상안은 30~75% EWU를 목표로 한다. 더 싼 협상은 의무 토큰과 후속 위험을 남겨 단순 상위 선택지가 되지 않아야 한다.
- 계절 세계 사건은 계절별 정확히 7개, 지속 1~6일, 실제 영향 도메인 2개 이상을 요구한다. 정규화된 총 강도는 12 이하이며 순수 기회 사건은 0을 허용한다.
- 축제 준비 비용은 최소 참가자 기준 1인당 5~80 EWU다. 성공·부분 성공·실패의 기분 일수는 반드시 내림차순이어야 하며, 고비용 축제는 슬픔 완화·외교·문화 동화처럼 추가 회수 경로가 있어야 한다.
- 서비스 사고는 정확히 3개의 기계적으로 다른 대응을 제공하고 최대 정규화 강도는 0.5~12다. 텍스트만 다른 동일 효과 선택지는 허용하지 않는다.

계약 18개, 세력 장 36개, 이정표 9개, 계절 사건 28개, 축제 16개와 서비스 사고 8개의 정적 감사 결과는 `Artifacts/QA/strategic-content-balance.txt`에 기록한다.

### 4.16 플레이어 주의력과 UI

- 동시에 처리할 중요 사건은 최대 2개다.
- 동시에 보이는 중요 경보는 최대 5개, 치명 경보는 최대 2개다.
- 자동 소사건은 하루 6개 상한 뒤 요약한다.
- 같은 원인의 반복 경보는 하나로 묶는다.
- 자동화는 클릭 횟수를 줄여야 하며 정상 생산 주문 하나가 지속적으로 개별 아이템 선택을 요구하지 않아야 한다.
- 실패 원인은 재료, 작업자, 경로, 위험, 출력 공간과 정책으로 구분한다.

## 5. 종족·전략·난도 기준

### 5.1 종족

- 각 종족은 최소 두 영역에서 명확한 강점을 가진다.
- 각 종족은 최소 한 가지 실제 운영 비용을 가진다.
- 종족에 맞는 합리적 전략을 사용했을 때 표준 난도 장기 생존율 차이는 10%p 이내를 목표로 한다.
- 어느 종족도 농업, 산업, 전투, 서비스와 세대 운영을 모두 지배하지 않는다.
- 시작 종족에 따라 다른 해답이 나오지만 필수 연구나 이정표가 영구 봉쇄되지는 않는다.

### 5.2 난도

보통 난도가 모든 수치의 기준 권위다. 쉬움과 어려움은 핵심 물리 규칙을 바꾸기보다 준비 여유와 외부 압력을 조절한다.

- 쉬움: 더 긴 경고, 낮은 외부 위협, 넓은 회복 창과 추가 시작 비축
- 보통: 이 문서의 중앙 목표
- 어려움: 더 높은 외부 압력, 짧은 회복 창과 복합 사건 확률 증가

배고픔, 물리 생산 레시피와 저장 규칙처럼 세계의 기본 인과는 난도에 따라 바꾸지 않는다. 현재 전투 배율은 후보값이며 실제 승률 분포로 재검증한다.

## 6. 이정표와 엔드리스

| 목표 | p10 | 중앙값 | p90 |
|---|---:|---:|---:|
| 첫 일반 이정표 | 120일 이후 | 180~220일 | 300일 이내 |
| 후기 룬 산업 | 280일 이후 | 360~400일 | 500일 이내 |
| 첫 대이정표 | 900일 이후 | 1,020~1,100일 | 1,250일 이내 |
| 9개 전체 달성 | 1,050일 이후 | 1,250~1,400일 | 1,650일 이내 |

- 이정표는 날짜로 잠그지 않고 실제 콘텐츠 조건으로 달성한다.
- 모든 이정표는 영구 보상, 물리 랜드마크와 새로운 세계 압력을 함께 제공한다.
- 엔드리스는 체력만 무한 증가시키지 않는다.
- 10일 주기마다 기후, 세력, 질병, 물류, 전투 중 한두 축을 강화한다.
- 일반 복합 위기 뒤에는 최소 5일의 회복 창을 목표로 한다.

## 7. 결정론적 검증 매트릭스

모든 큰 밸런스 패치는 다음 기준점을 사용한다.

| 축 | 기준점 |
|---|---|
| 날짜 | 1, 3, 10, 30, 60, 120, 240, 400, 960, 1,200 |
| 인구 | 3, 10, 30, 100, 500, 2,000 |
| 기후 | 온대 동굴, 서리 균열, 잿불 황무지, 균사 심층, 마나 폭풍지 |
| 종족 | 던전 종족 9개와 인간 |
| 정책 | 균형, 연구집중, 군사집중, 서비스집중, 자동화집중, 계보집중 |
| 난도 | 쉬움, 보통, 어려움 |
| 시드 | 경제·진행 256개 이상, 전투 조합당 1,000회 이상 |

자동 보고서는 다음을 포함한다.

- 노동 배분과 작업 차단 원인
- 재고 일수와 EWU 흐름
- 생산 병목, 출력 포화와 운반 비중
- 연구·시설 가동·이정표 도달 분포
- 전투 승률, 소모품, 부상과 회복 비용
- 사망, 붕괴, 이탈과 계약 실패 원인
- 사용률이 낮은 시설·장비·연구
- 최적 전략과 차선 전략의 격차
- 종족·기후·난도별 결과 편차
- 플레이어 경보와 예상 입력량

결정론적 규칙과 저장 복원은 동일 시드·동일 입력에서 같은 결과를 내야 한다. 성능 프레임률이 밸런스 결과를 바꾸면 실패다.

## 8. 콘텐츠 추가·수정 필수 기록

새 콘텐츠 또는 수치 변경은 구현 전에 다음 기록을 만든다.

```text
정의 ID:
콘텐츠 종류:
정의·카탈로그·실행기 위치:
등장 시대와 연구:
플레이어에게 주는 새 결정:
물리 BOM·입력·출력:
직접 작업량과 계산 근거:
EWU와 목표 회수 기간:
공간·전력·물·연료·정비:
위험·실패·회복 방식:
사회·비가역 비용:
기존 대안과의 장단점:
지배 전략 방지 조건:
저장 권위와 실행 명령:
자동 감사 ID와 전수 목록 포함 여부:
검증 매트릭스와 보고서 위치:
현재 밸런스 상태:
```

콘텐츠 종류별 추가 확인:

| 콘텐츠 | 반드시 비교할 항목 |
|---|---|
| 시설 | BOM, 건설 WU, 면적, 처리량, 유지비, 회수 기간, 해체 |
| 재료·아이템·조합식 | 입력·출력, 제작 깊이, EWU, 소비 분기, 순환 수익 |
| 장비·의복 | 전투·환경 역할, 재료, 작업량, 내구도, 품질 분포, 대응·약점 |
| 연구 | 작업량, 인과 선행, 실제 해금, 가동 가능 시점, 시대 도달 분포 |
| 종족·특성 | 강점, 운영 비용, 환경·직무별 결과 편차, 배타 조합 |
| 농업·축산 | 노동, 물, 비옥도, 계절, 사료, 순생산, 품종 지배 여부 |
| 의료·질병 | 전파, 작업 손실, 시설·약품, 회복 시간, 예방 효과 |
| 사건·축제·손님 | 실제 비용, 기한, 선택 기대가치, 후속 상태, 주의력 |
| 세력·계약 | 기간 생산비, 의무·원한, 장기 손익분기, 교차 세력 영향 |
| 전투 조우 | 파티 기준, 승률, 라운드, 소모·부상·회복, 보상, 카운터 |
| 이정표·엔드리스 | 조건, p10/중앙/p90, 랜드마크, 영구 보상, 새 압력 |

기준 기록이 없거나 자동 감사가 해당 정의를 발견하지 못하면 콘텐츠를 `완료`로 표시할 수 없다. 변경이 기존 기준 밴드 안에 있더라도 기록을 생략하지 않으며, 기준을 벗어나는 값은 예외 사유·대가·악용 방지·재검증 날짜를 이 문서에 남긴다.

## 9. 밸런스 변경 순서

수치는 상류에서 하류 순서로 조정한다.

1. 공통 시간, 노동과 EWU 계산
2. 원료 수확량과 물리 BOM
3. 제작·건설 작업량과 처리량
4. 운반·저장·전력·정비
5. 식량·농업·의복·기후·의료
6. 연구 작업량과 시대 도달 시점
7. 장비·전투·원정 보상
8. 손님·금화·포로·세력 계약
9. 사건·가족·문화·노화
10. 이정표·엔드리스
11. 난도 보정

하류 날짜나 보상만 먼저 바꾸어 상류 생산망의 문제를 숨기지 않는다.

## 10. 완료와 예외

다음 조건을 모두 만족해야 해당 범위를 `이론 밸런스 통과`로 표시한다.

- 대상 콘텐츠 100%에 기준 기록이 있음
- BOM과 작업량 계산이 재현 가능함
- 가역 순환과 해체 순환이 상한을 넘지 않음
- 목표 시점에 물리적으로 도달 가능함
- 지배 전략 방지 검사가 통과함
- 다중 시드 결과가 목표 밴드 안에 있음
- 저장 복원과 배속이 결과를 바꾸지 않음
- 실패 원인과 회복 수단을 UI가 설명함

기준을 의도적으로 벗어나는 콘텐츠는 다음을 문서에 기록해야 한다.

- 예외 정의 ID
- 벗어나는 목표 밴드
- 필요한 게임 경험과 이유
- 다른 비용축에서 지불하는 대가
- 악용 방지 조건
- 사용자 또는 설계 권위의 승인
- 전용 검증 결과

실제 플레이 검증 전에는 `최종 밸런스 완료`라고 표시하지 않는다. 이론 기준은 첫 권위이며, 텔레메트리와 사람 플레이테스트는 이 기준을 보정하는 다음 단계다.

## 11. V26 창립자 100특성·신화 품질 필수 기록

```text
정의 ID: balance:founder-traits-shared-effects-v26 / trait:101~109,200~230,235,239,245,247~259,300~306,400~417,500~518
콘텐츠 종류: 창립자 특성, 공용 수치 효과, 정체성 규칙, 신화 완제품 품질
정의·카탈로그·실행기 위치: CharacterTraitSO, GameplayEffectDefinitionSO/Binding, CharacterIdentityRuleRouter/StateStore, V26FounderTraitContentBuilder
등장 시대와 연구: 새 게임 창립자 생성부터 적용. 신화 승격은 해당 무기·방어구·방패·의복의 기존 연구·시설 해금을 그대로 요구
플레이어에게 주는 새 결정: 1~4개 특성의 장단점과 가족 배타 조합을 수용하거나 무제한 수동 리롤. 극한형은 직접 위험 명령과 후유증을 선택
물리 BOM·입력·출력: 특성은 물리 입력·출력을 생성하지 않음. 신화도 대상 완제품의 원래 BOM을 100% 소비하며 추가 복제·감면 없음
직접 작업량과 계산 근거: 기존 레시피·장비·의복 승인 작업량을 그대로 사용. 신화 자격은 최종 제작자이자 승인 작업량 60% 이상 기여자
EWU와 목표 회수 기간: 공용 효과는 속도·품질·사고·소비만 투영하며 EWU 자체를 삭제하지 않음. 장비 준비 보고서와 바닥부터 계산 보고서에서 별도 검증
공간·전력·물·연료·정비: 기존 대상 시설과 공정 요구를 그대로 사용. 신화품도 일반 물리 아이템 인스턴스로 저장·운반·수리
위험·실패·회복 방식: 극한형 301~306은 명시된 발동 임계치, 단일 사용 키, 실패 확률, 피해·내구·피로·후유증을 가짐
사회·비가역 비용: 직접 명령은 실행하되 UI에서 예상 기분·스트레스·관계 비용을 먼저 표시하고 성공 뒤 적용한다. 청소 강박의 청소 우선순위 하향은 기분 -2·스트레스 +3이며, 원한·욕구·반복 제작·극한 사용 상태는 저장됨
기존 대안과의 장단점: 단순 긍정/부정, 수치 절충, 기벽, 정체성형, 극한형을 함께 제공. 동일 선택 가족은 동시 보유 불가
지배 전략 방지 조건: 부정 슬롯 목표 28%, 극한 슬롯 1%, 두 번째 극한 가중치 5%, 정상 품질 계산 신화 0%, 자동 판매·분해 재굴림 차단
저장 권위와 실행 명령: 선택은 CharacterGrowthState.traitIds, 규칙 상태는 기존 character narrative 저장 섹션, 신화 계보는 물리 아이템. 파생 스냅샷은 미저장 재계산
자동 감사 ID와 전수 목록 포함 여부: V26FounderTraitAuditScenario 100종 전수, 100,000 리롤, 1,000,000 정상/신화 판정
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-founder-trait-mythic-audit.txt, Artifacts/QA/v26-founder-industry-bottom-up.md, 장비 준비 처리량 보고서
현재 밸런스 상태: `밸런스 시뮬레이션 검증`. Unity MCP 최신 재컴파일과 카탈로그 재빌드 후 100종/100,000 리롤/1,000,000 신화·정상 판정, 공용 효과 이중 적용 차단, 정체성 상태 왕복, 직접 명령 비용 미리보기·기분·스트레스 적용, 10,000개 3인 파티 바닥부터 산업 계산, 6개 장비 준비 체크포인트와 실제 68/68/68 전체 월드 저장 왕복이 통과했다. 무연구 생산 0은 Day 1 시작 재고 전용 계약이며, 최초 생산 해금은 최소 36 연구 WU로 자연 창립자 기준 0.389일이다. 실제 플레이 입력·정지 시간을 포함한 실전 보정은 별도 단계다.
```

### V26-149 비상 비축 연결 기록

```text
정의 ID: stock-policy:emergency-reserve / trait:220
콘텐츠 종류: 품목별 재고 정책, 창립자 특성 조건·지속 욕구
정의·카탈로그·실행기 위치: ResourceStockPolicyData, ResourceStockPolicyQuery, WarehouseFeatureCommandService/SurfacePresenter, CharacterStatsProjectionService, CharacterPersistentNeedClock
등장 시대와 연구: 창립 직후 창고 자원 전망 UI에서 사용. 별도 연구 해금 없음
플레이어에게 주는 새 결정: 활성 품목 정책을 비상 비축으로 지정하고 기존 최소 재고를 안전 기준으로 사용할지 선택
물리 BOM·입력·출력: 새 물품이나 무료 산출 없음. 실제 비출고 월드 스택 수량만 계산
직접 작업량과 계산 근거: 기존 재고 정책·생산·운반 작업량을 그대로 사용하며 비상 지정 자체의 WU는 0
EWU와 목표 회수 기간: 비상 기준을 충족한 특성 220 보유자의 비상 작업 사고율만 ×0.85. 생산량이나 BOM은 변경하지 않음
공간·전력·물·연료·정비: 기존 품목의 저장 공간과 시설 비용을 그대로 부담
위험·실패·회복 방식: 지정된 모든 비상 품목의 실제 보유량이 각 최소 재고 이상이어야 준비 완료. 미지정 또는 한 품목이라도 부족하면 부족 판정
사회·비가역 비용: 특성 220은 일일 판정으로 준비 완료 +2 또는 부족 -4를 정체성 기분 정책을 통해 적용
기존 대안과의 장단점: 일반 재고 정책은 그대로 유지되고, 비상 지정은 안전 보너스와 더 높은 유지 재고·공간 기회비용을 교환
지배 전략 방지 조건: 최소 재고 0 정책도 지정할 수 있으나 모든 지정 품목을 실제 보유량으로 판정하며 비상 미지정은 준비 완료가 아님
저장 권위와 실행 명령: ResourceStockPolicyData.isEmergencyReserve가 기존 stock-policy 저장 섹션에 저장됨. UI ToggleEmergencyReserve 명령은 정책을 자동 활성화하고, 비활성화 정규화는 비상 지정을 제거
자동 감사 ID와 전수 목록 포함 여부: Phase 149 source-to-consumer manifest에 condition state:emergency-stocked와 사건 stockpile:emergency-ready/shortage를 포함
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-founder-trait-connectivity-audit.md
현재 밸런스 상태: 연결 수직 슬라이스 구현·Unity MCP 컴파일 통과. 전체 특성 회귀와 WU 재계산 전이므로 최종 밸런스 완료 아님
```

### V26-149 정체성 조건·식사·환경 연결 기록

```text
정의 ID: trait:103,209,213,224 / identity:luxury,insult,ritual-fast,environment
콘텐츠 종류: 창립자 정체성 규칙, 조건부 공용 효과, 실제 식사·환경 사건
정의·카탈로그·실행기 위치: CharacterRitualFastingRuntime, CharacterConsumablesRuntime/ApplicationPorts, SurvivalFoodRuntime, CharacterIdentityDomainAdapters, CharacterPersistentNeedClock, EquipmentCraftingBuildingAbilityHandler, CharacterAiMacroDecisionRunner
등장 시대와 연구: 창립 직후부터 적용. 장비 제작은 대상 장비·시설의 기존 연구를 그대로 요구
플레이어에게 주는 새 결정: 대체 재료 장비 주문, 의식 단식 시작·완수·파기, 직접 식사로 단식 파기, Lavish 식사 확보 여부
물리 BOM·입력·출력: 대체 재료는 실제 주문 재료를 소비하고 식사는 기존 물리 스택 1개를 소비. 단식 중 자동 식사는 차감 전에 거절되며 무료 음식·재료를 만들지 않음
직접 작업량과 계산 근거: 대체 재료 조건은 실제 다음 장비 주문에만 적용되고 작업 실행기의 단일 속도 투영을 사용. 완료 뒤 추가 속도 곱 없음
EWU와 목표 회수 기간: 새 EWU 감면 없음. 식사 소비 배율은 권위 있는 허기·배설 감소 주기에만 적용되며 단식 후 1.15배는 다음 실제 식사까지 유지
공간·전력·물·연료·정비: 기존 장비 시설, 식당, 저장 공간과 환경 설비 비용을 그대로 부담
위험·실패·회복 방식: 직접 식사는 단식을 파기하고 -3 기분 규칙을 적용. 하루 이상 단식만 완수 가능하며 축제·장례도 성공 후에만 완수 시도. 장기 환경 사건은 저장된 노출 15 이상에서 일일 판정
사회·비가역 비용: 실제 모욕은 대상 기분과 관계 반응을 거치며 기본 손실과 특성 손실을 이중 적용하지 않음. 대응 선호 보유자는 자율 반응을 수행
기존 대안과의 장단점: Lavish 식사는 부자의 욕구를 만족하지만 절약가는 회피. 기본 식사는 비용이 낮지만 장기 기본 생활 결핍을 누적
지배 전략 방지 조건: UI 재개방·실패 주문은 대체 재료 성공 사건을 만들지 않음. 단식 상태는 기분 문자열이 아니라 저장 권위이며 자동 식사로 아이템만 사라지는 경로를 차단
저장 권위와 실행 명령: 단식은 CharacterIdentityStateStore의 traitDefinitionId+ruleId 상태, 장비 재료는 실제 생산 주문, 환경은 CharacterEnvironmentExposure 저장 상태. 파생 조건은 저장하지 않고 재투영
자동 감사 ID와 전수 목록 포함 여부: Phase 149 source-to-consumer 감사에 4개 condition, 8개 identity event/tag와 실제 호출자를 포함
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-founder-trait-connectivity-audit.md
현재 밸런스 상태: 연결 수직 슬라이스 구현·Unity MCP 컴파일 통과. 집중 행동 회귀·전체 저장 왕복·WU 재계산 전이므로 최종 밸런스 완료 아님
```
## V26 극한 특성 실행 권위 연결 기록 (2026-08-11)

대상: `황금 수확`, `한계 돌파`, `마력 과충전`의 정의만 존재하거나 호출자 입력을 신뢰하던 경로를 실제 경작지·생산 주문·전투 자원 권위에 연결한다.

- 등장 시대·역할: 황금 수확은 농업 극한 선택, 한계 돌파는 생산 긴급 선택, 마력 과충전은 후기 룬 전투의 저마나 역전 선택이다. 일반 생산·수확·비전 장비를 대체하지 않고 동일 물리 입력을 위험하게 변환한다.
- 플레이어 결정: 경작지에서 특성 304 작업자를 지정해 24시간 지연을 수락한다. 생산 주문에서 특성 305 작업자를 지정한다. 직원 관리에서 특성 306 보유자와 실제 착용 룬 장비를 선택해 과충전한다.
- 물리 BOM: 황금 수확과 한계 돌파는 기존 종자·재료·연료·작업량을 줄이지 않는다. 비전 공격은 실제 장비·탄약 비용에 더해 저장되는 개인 마나를 소비한다. 과충전은 최대 체력 15%와 착용 마력 장비 최대 내구도 25%를 실제 차감한다.
- 직접 작업량·시간: 황금 수확은 기존 수확 WU에 24시간 달력 지연을 추가한다. 한계 돌파는 실제 지정 주문 작업 중에만 속도 ×1.50, 사고 ×1.50, 피로 ×2.00이며 종료·이탈 뒤 1일간 작업 ×0.65다.
- 마나 기준 배정: 최대 100, 룬 검 8/타, 룬 활 10/발, 마나 랜스 12/발, 기본 회복 8/게임시간으로 시작한다. 마나 차단 중 회복은 0이다. 이 수치는 전투 회귀와 장비 시대별 전투 준비 시뮬레이션 전까지 `밸런스 기준 배정`이다.
- 과충전 조건·효과: 실제 현재 마나가 최대의 30% 미만일 때만 직접 명령 가능하다. 20초간 비전 위력 ×1.60, 이후 1일간 마나 회복 ×0.50이다. UI가 전달한 비율을 신뢰하지 않고 마나 Query가 판정한다.
- 저장 권위: 황금 수확 시도 순번과 지정 작업자는 경작지 저장, 한계 돌파 지정 작업자는 생산 주문 저장, 개인 현재/최대 마나와 마나 차단은 신체 전투 상태 저장, 극한 활성·후유 판정은 CharacterIdentityStateStore가 소유한다. 파생 배율은 저장하지 않는다.
- 악용 방지: UI 재개방·취소·작업자 교체로 시도 순번을 되돌리지 않는다. 지정하지 않은 작업자는 위험 수확을 수행할 수 없다. 한계 돌파 효과는 실제 지정 주문 실행 lease가 갱신되는 동안만 활성화된다. 마나 부족 공격은 탄약·내구도·명령 revision을 소비하기 전에 거절한다.
- 실행·감사: 실제 UI Button → command → 저장 권위 → 작업/전투 결과 경로와 저장 왕복을 PlayMode에서 검증한다. Editor가 극한 서비스를 직접 호출한 결과만으로 연결 완료 처리하지 않는다.

현재 판정: **밸런스 기준 배정 / 실행 연결 및 시뮬레이션 검증 진행 중**.

## V26 창립자 특성 벡터·장비 준비 재계산 기록 (2026-08-11)

```text
정의 ID: balance:founder-trait-vector-v26 / balance:equipment-readiness-founder-input-v26
콘텐츠 종류: 창립자 특성 수치 투영, 초기 산업·연구, 장비 생산과 전투 준비 처리량
정의·카탈로그·실행기 위치: V26FounderIndustryBalanceDebugScenarios, SettlementEquipmentReadinessThroughputDebugScenarios, CharacterNeedStateService, WorkTaskExecutor, CharacterProficiencyLearningRules
등장 시대와 연구: 창립자 생성 직후부터. 무연구 Day 1은 시작 재고 전용이고 최초 생산 해금은 실제 연구 선행을 요구
플레이어에게 주는 새 결정: 무리롤, 현실적 3파티 타협 리롤, 20파티 상위 리롤, 이론 극단을 비교하고 산업 3종 배치·제작·연구 담당을 선택
물리 BOM·입력·출력: 특성은 BOM을 줄이지 않는다. 장비 세트는 live BOM·직접 WU·gross EWU를 그대로 소비하고 신화도 동일 입력을 소비
직접 작업량과 계산 근거: 1인 기준 99 승인 WU/일. 작업 속도만 WU에 직접 곱하며 식량·사고·XP는 별도 단위로 계산
EWU와 목표 회수 기간: 13개 기능과 나이별 기관 손상을 반영한 무리롤 자연 3인 필수산업 평균 273.030 WU/일, p10/중앙/p90 266.508/272.746/279.774, 최고 제작 평균 91.467 WU/일, 최고 연구 평균 92.448 WU/일. 장비 준비 6개 체크포인트 모두 기간 용량 안
공간·전력·물·연료·정비: 기존 시설·운반·가동 비용은 유지. 처리량 보고서는 이를 동시 최적화하지 않는 gross 상한이며 산업 배정은 35% 상한
위험·실패·회복 방식: 자연 3교대의 하루 사고 기대 0.323건, 기대 해부 노드·총 생명력 피해 0.647. 사고는 실제 해부 노드를 손상하고 작업을 중단하며 관련 기능·성능 Query가 다시 계산된다. 정확한 손실 WU는 어떤 노드가 손상되고 언제 치료하는지 포함한 후속 일과표 실행 시뮬레이션에서 산출
사회·비가역 비용: 정체성 사건과 기분·관계 비용은 WU로 환산하지 않고 별도 typed event·저장 상태로 유지
기존 대안과의 장단점: 같은 창립자 시드에서 특성만 제거한 대조군과 비교. 자연 특성은 산업 WU +0.13%, 최고 제작 +0.46%, 식량 +0.72%, 사고 +0.23%, XP +0.02%
지배 전략 방지 조건: 선택 가족 충돌 0, 극한 중복 감쇠, 정상 신화 0, 물리 BOM 감면 0. 리롤 평균 이득이 작아 무한 리롤은 극단 조합 탐색의 시간 비용으로 남음
저장 권위와 실행 명령: traitIds·숙련 XP·욕구·사고·아이템은 각 기존 저장 권위. 감사 파생값과 캐시는 저장하지 않고 재계산
자동 감사 ID와 전수 목록 포함 여부: V26_FOUNDER_INDUSTRY 10,000파티, V26_TRAIT_CONNECTIVITY_MANIFEST 541행(target 45, condition 45, identity 63, behavior 38, need 9, extreme 7, public API 104, helper method 77, serialized field 126), 장비 준비 6체크포인트
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-founder-industry-bottom-up.md, Artifacts/QA/v26-equipment-readiness-throughput.md, Artifacts/QA/v26-founder-trait-connectivity-audit.md
현재 밸런스 상태: 연결성·13기능 기반 p10/중앙/p90 이론 처리량 통과. 실제 이동·식사·수면, 사고 시점별 치료 정책과 질병 노출 정책을 포함한 손실 WU 및 플레이 입력 시간은 후속 검증이므로 최종 밸런스 완료 아님
```

## V26 창립자 특성 단일 수치 권위·동적 연결 감사 기록 (2026-08-11)

```text
정의 ID: architecture:founder-trait-effect-authority-v26 / audit:founder-trait-dynamic-endpoints-v26
콘텐츠 종류: 창립자 특성 공용 효과, 극한형 정체성 규칙, 신화 품질 판정, 연결성 자동 감사
정의·카탈로그·실행기 위치: GameplayEffectBinding, CharacterIdentityRule, CharacterGameplayEffectProjector, CombatRuntimeStatFactory, CombatEquipmentCraftingRuntime, ApparelWorkOrderRuntime, V26FounderTraitConnectivityManifestScenario
등장 시대와 연구: 창립자 생성 직후부터; 연구 해금 수치와 무관한 런타임 구조 교정
플레이어에게 주는 새 결정: 없음. 기존 특성 선택과 극한형 명령의 authored 결과가 실제 계산에 한 번만 반영되도록 교정
물리 BOM·입력·출력: BOM·직접 작업량·재료 소비 변화 없음. 신화도 동일 BOM과 작업량을 소비
직접 작업량과 계산 근거: 작업·전투·이동·사고·피로·회복·품질 배율은 GameplayEffectBinding 하나만 수치 권위. 정체성 규칙은 발동·확률·선택 비용·상태만 소유
EWU와 목표 회수 기간: 변경 없음. 중복 적용 제거는 저술된 배율을 정확히 1회 적용하는 구조 교정이며 새 보너스를 추가하지 않음
공간·전력·물·연료·정비: 변경 없음
위험·실패·회복 방식: 신들린 영감은 실제 rule.mythicChance와 minimumContributionShare를 읽는다. 사선 각성은 임계 상태 페널티 무효화와 발동 상태를 규칙이, 전투력·이동·후유 작업 배율을 공용 effect가 소유
사회·비가역 비용: 변경 없음. 정체성 사건·기분·관계 권위는 typed event와 CharacterMoodPolicyService 유지
기존 대안과의 장단점: 중복 필드·하드코딩은 개별 기능 작성은 빠르지만 값 불일치와 이중 적용을 만든다. 단일 binding 권위는 추적·중첩·장비 공유가 가능하고 authored 값 변경이 실제 도메인 판정에 즉시 반영됨
지배 전략 방지 조건: 일반 품질 신화 0, 신화 재굴림 불가, 공용 수치 이중 투영 0, 0%/100% authored 확률 경계 검증
저장 권위와 실행 명령: traitIds와 identity runtime state 및 물리 아이템 계보는 기존 권위 유지. 파생 수치와 감사 캐시는 저장하지 않고 복원 후 재계산
자동 감사 ID와 전수 목록 포함 여부: V26_TRAIT_CONNECTIVITY_MANIFEST 541/541, public API 104/104, private/internal/protected helper 77/77, serialized field 126/126, orphan 0
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-founder-trait-source-consumer-manifest.md, Artifacts/QA/v26-founder-trait-connectivity-audit.md, Artifacts/QA/v26-founder-trait-mythic-audit.txt, Artifacts/QA/full-world-round-trip-playmode-report.txt
현재 밸런스 상태: 연결 구조 공식 검증 완료. 100,000회 리롤과 1,000,000회 신화 감사, 전체 월드 68/68/68 통과. 장기 일과·성장·실전 밸런스 보정은 별도 후속 단계
```

## V26 적 방패 장비 호환 교정 기록 (2026-08-11)

```text
정의 ID: enemy:settler-barricadier / enemy:neutral-clockwork
콘텐츠 종류: 전투 적 원형 장비 조합
정의·카탈로그·실행기 위치: V20CombatContentAssetBuilder, EnemyArchetypeDefinitionSO, EnemyIndividualRuntime, CombatEquipmentLoadoutRuntime
등장 시대와 연구: 자유개척·중립 침입/조우 원형. 플레이어 연구와 무관
플레이어에게 주는 새 결정: 방패벽 방어자를 상대할 때 방패 파괴·측후면·정밀 대응을 선택
물리 BOM·입력·출력: 외부 적 장비 인스턴스 생성 규칙 유지. 무료 플레이어 산출이나 BOM 변경 없음
직접 작업량과 계산 근거: 적 생성 장비이므로 제작 WU 없음. 양손 무기 2손+방패 1손의 불가능한 3손 조합만 제거
EWU와 목표 회수 기간: 적 장비 보상 EWU와 회수 규칙 변경 없음
공간·전력·물·연료·정비: 해당 없음. 장비 내구·실물 인스턴스 규칙은 유지
위험·실패·회복 방식: 바리케이드병은 창→팔시온, 태엽구성체는 전쟁망치→철퇴로 변경하고 각 방패는 유지. 역할은 방어자로 유지하되 무기 사거리·타격 성향이 낮아짐
사회·비가역 비용: 없음
기존 대안과의 장단점: 방패를 제거하는 대신 1손 무기로 교체해 shield-wall 정체성과 카운터 태그를 보존
지배 전략 방지 조건: 전체 적 원형의 무기+방패 손 점유 합계 ≤2, 누락 장비 정의 0을 카탈로그 전수 감사
저장 권위와 실행 명령: 적 원형 SO가 정의 권위, 실제 적 장비 인스턴스가 런타임/저장 권위
자동 감사 ID와 전수 목록 포함 여부: ENEMY_HAND_COMPATIBILITY=PASS, 전체 EnemyArchetypeDefinitionSO 포함
검증 매트릭스와 보고서 위치: Artifacts/QA/full-world-round-trip-playmode-report.txt, Artifacts/QA/v26-founder-trait-connectivity-audit.md
현재 밸런스 상태: 장비 생성 연결 오류 수정·공식 68/68/68 전체 월드 왕복 통과. 변경된 두 적의 실전 승률 보정은 후속 전투 플레이테스트 대상
```

## V26-150 질병 세부 능력치 분리 기록 (2026-08-11)

```text
정의 ID: effect:character:disease-resistance:multiply / effect:character:disease-recovery-speed:multiply / effect:character:immunity-gain:multiply / effect:character:immunity-retention:multiply
콘텐츠 종류: 캐릭터 세부 능력치, 질병 감염·회복·면역 계산, 창립자 특성 207
정의·카탈로그·실행기 위치: GameplayEffectDefinitionSO, CharacterPopulationDiseaseModifierQuery, PopulationHealthAggregateState, V26FounderTraitContentBuilder
성장 단계와 연구: 창립자 생성 직후부터 적용. 별도 연구·시설을 요구하지 않으며 향후 종족·장비·상태·연구도 같은 정의를 참조할 수 있음
플레이어 결정: 빠른 회복 특성 보유자, 방역 장비·상태·연구 조합을 통해 감염 위험·질병 기간·면역 획득·면역 유지 중 서로 다른 축을 선택
물리 BOM·입력·출력: 신규 물리 BOM과 아이템 생성 없음. 백신과 현장 치료의 기존 물리 투입량은 그대로 소비됨
직접 작업량·EWU·시간: 신규 작업량 0 WU. 질병 회복 속도는 감염 순간 비만성 전염 기간을 baseDays/speed로 계산해 일 단위 올림하고 recoveryDay에 확정 저장
공간·전력·물·연료·정비: 신규 요구 없음. 기존 의료시설·백신·치료 공급망을 변경하지 않음
위험·실패·회복: 질병 저항은 감수성을 나누고, 질병 회복은 만성 질환을 치료하지 않음. 면역은 0~100으로 제한하고 면역 유지는 감소량만 나누며 면역을 생성하지 않음
사회·기분·관계 비용: 이번 분리는 수치 소비 경로만 변경하며 기존 질병 기분·신체 부담·관계 규칙을 우회하지 않음
기존 대안과 교환: 신체 회복 속도는 HP·상처 회복 전용으로 유지. 유전 all/toxin은 감염 저항, recovery는 질병 회복, memory는 면역 유지에만 합성되어 한 수치가 모든 의료 결과를 동시에 지배하지 않음
지배적 전략 방지: 빠른 회복 207은 신체 회복 ×1.15와 네 질병 축 ×1.10의 희귀 이점이지만 BOM·치료 성공·만성 제거를 우회하지 않음. 같은 공용 효과는 중앙 투영 한 번만 소비
저장 권위와 실행 명령: 활성 감염의 recoveryDay와 면역 값/기본 감소율은 PopulationHealthWorldSaveData, 효과 투영은 저장하지 않고 캐릭터 특성·종족·장비·상태·연구에서 재계산
자동 감사 ID와 필수 목록 포함 여부: V26FounderTraitAuditScenario, V26FounderTraitConnectivityManifestScenario, PopulationHealthBalanceCalibrationScenario, DungeonFullWorldRoundTripPlayModeFacade에 포함
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-founder-trait-mythic-audit.txt, Artifacts/QA/v26-founder-trait-connectivity-audit.md, Artifacts/QA/population-health-balance.txt, Artifacts/QA/full-world-round-trip-playmode-report.txt
현재 밸런스 상태: 해당 세부 능력치 분리와 실행 연결 완료. 질병 16종/전염성 15종 결정론 감사, 연결 manifest 545/545 orphan 0, 창립자 100종 감사, 공식 전체 저장 68/68/68, Console Warning/Error 0/0 통과
```

## V26-151 캐릭터 기능·숙련·세부 성능 단일 체계 기록 (2026-08-11)

```text
정의 ID: architecture:character-functional-capacity-performance-v26
콘텐츠 종류: 캐릭터 신체 기능, 숙련, 작업·전투·의료·생존 세부 성능
정의·카탈로그·실행기 위치: CharacterFunctionalCapacityDefinitionSO, CharacterPerformanceFormulaDefinitionSO, ICharacterPerformanceQuery, CharacterBodyHealthRuntime, WorkTaskExecutor
등장 시대와 연구: 창립자 생성 직후부터 전 시대. 별도 연구 해금은 추가하지 않으며 기존 종족·장비·상태·연구 효과가 동일 투영에 참여
플레이어에게 주는 새 결정: 신체 손상·숙련·장비·특성의 실제 기여와 병목을 확인하고 작업자·치료·장비·원정 배치를 선택
물리 BOM·입력·출력: 신규 물리 BOM과 무료 출력 없음. 기존 작업·치료·장비·식량·약품·마나 입력을 그대로 소비
직접 작업량과 계산 근거: 기존 99 WU/성인·일과 0.85~1.25 숙련 속도 기준을 유지하되 13개 기능과 작업별 병목을 실제 처리량에 한 번만 적용
EWU와 목표 회수 기간: 구조 전환 자체의 EWU 감면 없음. 13기능·나이별 기관 손상 기반 10,000파티 재계산 결과 자연 3인 필수산업 p10/중앙/p90은 266.508/272.746/279.774 WU/일. 최소 전투 준비 장비의 공급/신규 수요는 비수요 구간을 제외하고 40.216x~93.481x이며 첫 충족일은 Day 32.238/122.478/243.799/405.991
공간·전력·물·연료·정비: 기존 시설·장비·환경 계수를 공식의 별도 문맥 입력으로 유지하고 신체·숙련 계수에 숨겨 합치지 않음
위험·실패·회복 방식: 적용 가능한 필수 기능 10% 미만은 명시적 실행 불가. 사고·품질·수율·회복·질병은 서로 다른 결과 채널로 계산. 작업 사고는 실제 해부 노드 2 피해와 작업 중단을 발생시키며, 질병 증상은 표적 기관·중증도별 작업 배율로 실제 작업 문맥에 소비
사회·비가역 비용: 기분·관계·정체성 상태는 기존 typed identity 규칙 권위를 유지하며 WU나 신체 기능으로 환산하지 않음
기존 대안과의 장단점: 구형 12능력치 호환 투영보다 원인과 결과를 추적할 수 있으나 전 도메인 공식·UI·저장 경계를 함께 전환해야 함
지배 전략 방지 조건: 숙련 보조 비중 20% 상한, 효과 단일 투영, 물리 BOM 감면 금지, 전역 기능 상한 없음, 결과별 도메인 안전 범위만 유지
저장 권위와 실행 명령: 해부학 건강·9종 숙련·traitIds·장비·상태·연구가 원본 권위. 기능·수행·최종 성능과 기여 추적은 저장하지 않고 재계산. 구형 12능력치 저장은 명시적 새 게임 경계
자동 감사 ID와 전수 목록 포함 여부: V27_CHARACTER_PERFORMANCE_STRUCTURAL_AUDIT, V27_CHARACTER_PERFORMANCE_LIVE_AUDIT, V27_CHARACTER_PERFORMANCE_CONSUMER_AUDIT, V27_CHARACTER_PERFORMANCE_SAVE_AUDIT, V26_FOUNDER_INDUSTRY, V25 proficiency links, 13기능·5지표·31작업·전 도메인 결과 전수 포함
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-founder-industry-bottom-up.md, Artifacts/QA/v26-equipment-readiness-throughput.md, Artifacts/QA/v25-proficiency-authored-mapping.md
현재 밸런스 상태: 밸런스 기준 배정 / 구조·연결 검증 통과. 13기능·107공식·구형 기호 0·저장 왕복·주요 실제 소비자·10,000파티·6개 장비 체크포인트·UI 1600x900/900x1600·전체 월드 68/68/68과 Console 0/0은 통과했으나 실제 이동·식사·수면과 치료·질병 노출 정책을 포함한 장기 다중시드 일정은 미완료이므로 밸런스 완료 아님
```

## V26-152 14개 신체 기능 교정 기록 (2026-08-11)

```text
정의 ID: architecture:character-functional-capacity-14-v26
콘텐츠 종류: 캐릭터 신체 기능, 작업·전투·의료·생존 세부 성능 공식
정의·카탈로그·실행기 위치: CharacterFunctionalCapacityDefinitionSO, AnatomyProfileSO, CharacterPerformanceFormulaDefinitionSO, ICharacterPerformanceQuery 및 기존 도메인 소비자
등장 시대와 연구: 창립자 생성 직후부터 전 시대. 연구 해금·시대·콘텐츠 수는 변경하지 않음
플레이어에게 주는 새 결정: 근력 손상과 면역 손상을 구별해 작업자·전투원·환자·장비·치료를 배치하고, 자원 효율은 섭취·정화·순환·활력의 파생 결과로 확인
물리 BOM·입력·출력: 신규 아이템·시설·무료 산출·BOM 변경 없음. 기존 음식·약품·장비·치료 입력을 그대로 소비
직접 작업량과 계산 근거: 기존 99 WU/성인·일과 0.85~1.25 숙련 계수 유지. 자원 효율 기능을 제거하고 근력 출력·면역 방어를 추가해 총 14개이며, 공식 가중치 합은 적용 가능한 입력끼리 재정규화
EWU와 목표 회수 기간: BOM·승인 WU를 바꾸지 않음. 운반·벌목·채석·구조·건설·해체·근접 위력은 근력 출력, 감염·질병 회복·면역 획득·유지는 면역 방어를 독립 입력으로 사용. 재계산 전에는 종족별 처리량 수치를 확정하지 않음
공간·전력·물·연료·정비: 변경 없음. 기존 시설·장비·환경 문맥 계수를 공식 마지막 단계에서 계속 적용
위험·실패·회복 방식: 근력 출력 또는 면역 방어 생산 기관 손상이 해당 결과를 실제로 낮춤. 필수 기능 10% 미만 거부, N/A 재정규화, 사고·소비·노출 역방향 채널 규칙 유지
사회·비가역 비용: 변경 없음. 정체성·기분·관계 상태는 기능 수치와 별도 권위
기존 대안과의 장단점: 13개 구조는 UI가 짧지만 완력과 기동, 면역과 회복을 섞었다. 14개 구조는 자원 효율을 파생값으로 내려 UI 증가를 1행으로 제한하면서 두 핵심 원인을 분리
지배 전략 방지 조건: 종족 최종 배율로 완력·면역을 중복 부여하지 않으며 Query에서 기능·숙련·공용 효과를 각각 한 번만 계산. 종족 간 역할 상위 호환 여부는 후속 전 종족 정량 감사에서 별도 실패 조건으로 둠
저장 권위와 실행 명령: 해부 노드 건강·질병/면역 상태·숙련·traitIds·장비·상태·연구만 저장. 14기능과 모든 결과·기여 추적은 복원 후 재계산하며 신규 저장 필드 없음
자동 감사 ID와 전수 목록 포함 여부: V27 structural/live/consumer/save audit에 14정의·10종족 생산자·자원효율 고아 0·근력/면역 실제 결과 변화·UI 행을 추가
검증 매트릭스와 보고서 위치: Artifacts/QA/v25-proficiency-authored-mapping.md, Artifacts/QA/v26-founder-industry-bottom-up.md, Artifacts/QA/full-world-round-trip-playmode-report.txt 및 V27 Unity Console 감사 로그
현재 밸런스 상태: `밸런스 기준 배정 / 구조·연결 검증 통과`. 14정의·107공식·10/10 종족 생산자, 근력/면역 인과 소비, 1600x900·900x1600 UI, 전체 월드 68/68/68 저장 왕복과 Console Error/Warning 0/0을 통과함. 전 종족 역할/부상/질병 다중시드 비교 전에는 밸런스 완료로 보고하지 않음
```

## V26-153 던전 종족 9종 기능 배분·유지비 기록 (2026-08-11)

```text
정의 ID: balance:dungeon-species-capacity-allocation-v26
콘텐츠 종류: 종족 신체 기능, 작업 성장 적성, 생존 유지비, 골렘 충전·정비
등장 시대와 연구: 시작 종족 3종은 Day 1, 나머지 6종은 영입·포로·동맹·후속 세대. 골렘 충전은 명시적 충전 capability와 마나 결정 접근을 요구
역할과 목표 밴드: 인간 100% 기준, 기능 80~125%, 종족 평균 최대 +5%, 대표 역할 +5~+12.5%, 대표 약점 -5~-15%, 일반 유효 WU 95~105%
물리 BOM·직접 작업량: 골렘 충전 1회당 마나 결정 1개와 100 WU로 충전 50 회복. 동력핵 정비는 기존 목재 1개·26 WU·마모 30 복구를 유지
내재 작업량·시간: 충전 35 이하에서 실행하며 기본 방전율 6.3/일 기준 약 7.94일마다 1회. 작업 100 WU마다 마모 부담 2.5, 건전도 50 이하에서 정비 제안
공간·기반 비용: 마력저장조 또는 명시적 골렘 충전 capability 시설, 실제 접근 경로와 물리 재고 예약 필요
위험·실패·회복: 재료·시설·경로·예약 누락은 실행 실패. 취소 전 소비 금지, 완료 후 중복 보상 금지. 마모는 해부 부담으로만 성능을 낮추고 정비 절차로 회복
대안과 장단점: 생물 종족은 음식·물·수면을 반복 소비하고 골렘은 긴 주기의 마나 결정·충전 정지·정비를 소비. 유지비만으로 5%를 넘는 상시 성능 우위를 허용하지 않음
지배 전략 방지: 종족 광역 작업/전투 배율 제거, 기능과 최종 효과 이중 적용 금지, 동일 역할 단독 1위 3개 초과 금지, Pareto 상위호환 0건
저장 권위: 종족 SO는 불변 정의, 종족 런타임은 충전 주문, 해부 런타임은 마모 부담, 숙련 aggregate는 XP. 기능·성능·AI 효용은 저장하지 않고 복원 후 재계산
자동 감사: 9종×14 바인딩, 범위·평균·대표 역할, 강약 작업 XP/AI 소비, 충전 재고·WU·취소·저장, 마모·정비 인과, UI 및 전체 월드 회귀
현재 밸런스 상태: `밸런스 기준 배정 / 종족 정량 감사·전체 구현 회귀 통과 / 의료 일정 절대량 미완`. 9종×14 기능, 107공식, 강약 작업 XP·AI, 골렘 충전·마모·정비·V3 저장, 100,000 자연 인물과 종족별 10,000 손상·질병 조건 감사가 통과함. 중립 배치는 0.963~1.018, 대표 역할은 1.075~1.115, 성장 적성 포함 Pareto 상위호환 0건, 골렘 30일 순 WU는 0.965. 공식 68 strict save sections, 동기식 최종 검증 33/33, PlayMode 7/7·최신 캡처 32개·저장 복원·Console Warning/Error/Exception/Assert 0/0을 통과함. 다만 실제 의료 스케줄을 포함한 치료 WU·회복 기간·작업 불가율·사망률을 아직 산출하지 않았으므로 종족 세부 능력치 밸런스 최종 완료로 보고하지 않음
```

## V26-154 술음료장 물리 유흥 소비 연결 기록 (2026-08-11)

```text
정의 ID: facility:recreational-substance-service-v26
콘텐츠 종류: D12 술음료장, 유흥 음료, 캐릭터 재미·기분·중독·과다복용
정의·카탈로그·실행기 위치: BuildingRecreationalSubstanceServiceAbility, D12_술음료장.asset, Facility.Interact, CharacterConsumablesRuntime, AbilityUseSubstance
등장 시대와 연구: 기존 D12 건설·해금 조건과 각 음료 생산 조건을 유지한다. 새 무료 해금이나 시작 재고를 추가하지 않음
플레이어에게 주는 새 결정: 술음료장 전용 재고를 배송하고 물질 정책을 허용해 재미와 음료별 기분 보상을 얻되, 내성·중독·과다복용·작업/전투 저하 위험을 감수
물리 BOM·입력·출력: 성공한 시설 이용 1회당 허용된 유흥 음료 물리 스택 정확히 1개를 소비. 음료의 기존 생산 BOM·연료·재료·제작 WU는 변경하지 않으며 무료 음식·영양·음료를 만들지 않음
직접 작업량과 계산 근거: 신규 생산 WU 없음. 시설 이용 시간은 기존 시설 상호작용을 따르고, 성공 뒤 시설은 재미 +8만 제공. 음료의 기분·지속시간·작업·전투 배율은 SubstanceItemFeature와 기존 물질 런타임이 한 번만 적용
EWU와 목표 회수 기간: D12 자체의 기존 건설 EWU·공간·운영비는 변경하지 않음. 유흥 효과를 생산 WU로 환산하지 않으며 음료 수요·중독 손실의 장기 균형은 후속 일정 시뮬레이션 대상
공간·전력·물·연료·정비: `facility-input:recreation-substance:{facilityId}` 전용 물리 버퍼가 필요. 외부 보관·낙하 재고는 배송 요청만 만들고 원격 소비할 수 없음. 기존 D12 공간·전력·물·정비 조건 유지
위험·실패·회복 방식: 물질 정책 거부, 적격 음료 부재, 시설 미가동은 소비 전에 실패. 성공 시 음료 정의의 내성·중독·과다복용과 작업/전투 효과를 그대로 적용하고 시설은 두 번째 기분 효과를 추가하지 않음
사회·기분·관계 비용: 성공한 이용만 사회 활동과 시설 경험을 기록하고 재미 +8을 적용. 좋은 음식의 식사 기분, 음료 자체의 기분, 시설 재미는 서로 다른 채널이며 중복 기분 보너스 없음
기존 대안과의 장단점: 적격 술음료장이 있으면 자동 유흥 음주는 시설을 우선한다. 시설이 전혀 없을 때는 기존 직접 픽업·소비를 유지해 음료 자체를 봉쇄하지 않지만 시설 재미·사회 경험은 얻지 못함
지배 전략·악용 방지: D12의 구형 무료 배고픔·기분 회복 제거, 정확한 시설 목적지 재고만 차감, 정책/재고 실패 시 무소비, 한 번의 상호작용당 한 스택, 외부 재고 원격 소비 금지, 음료 효과 이중 적용 금지, 취소·반복 명령으로 재고나 효과 복제 금지
저장 권위와 실행 명령: 물리 아이템 스택은 기존 재고 저장, 내성·중독·활성 효과는 기존 CharacterConsumables 상태 저장이 권위. 신규 건물 능력은 불변 SO이며 별도 런타임 저장 필드를 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: SurvivalDebugScenarios `tavern_recreational_substance_service`, D12 역할/능력, 물리 스택 1→0, 정책 거부 보존, 활성 물질·작업 배율·재미 적용 포함
검증 매트릭스와 보고서 위치: Artifacts/QA/final-playmode-acceptance-report.txt, SurvivalDebugScenarios Unity Console 로그, 동기식 최종 수용 검증 33/33
현재 밸런스 상태: 술음료장 물리 소비·효과·정책·자동 행동 연결 완료. PlayMode 7/7, 최신 캡처 32개, 저장 복원, Console Warning/Error/Exception/Assert 0/0 통과. 음료별 장기 소비량·중독률·유흥 배치가 전체 WU에 미치는 다중 시드 균형은 후속 일정 시뮬레이션 대상이며 전체 게임 밸런스 완료로 보고하지 않음
```

## V26-156 공용 수량 예약·버퍼 집약·배식 물류 기록 (2026-08-11)

```text
정의 ID: architecture:item-quantity-lease-v26 / balance:meal-logistics-v26
콘텐츠 종류: 모든 물리 아이템 수량 예약, 운반 분할, 시설 버퍼 집약, 음식 영양·배식·부패·좌석 사용, 저장 소유권 복원
정의·카탈로그·실행기 위치: WorldItemStackRuntime/Repository, ItemQuantityReservationService, ReservedItemTransferService, BufferStackAggregationService, CharacterConsumablesRuntime, AIEat, 저장 캡처·복원 조정기
등장 시대와 연구: Day 1부터 전 시대의 물류·식사·제작·건설·의료·교역. 연구는 기존 시설·레시피·자동화 해금만 사용
플레이어에게 주는 새 결정: 부분 재고를 여러 작업에 동시에 배정하고, 음식 품질 정책·시설 위치·좌석 수·보존과 자동화를 통해 소비 품질·부패·동선·대기를 교환
물리 BOM·입력·출력: 예약은 물건을 생성하지 않는다. 픽업 때만 예약 수량을 보존적으로 분할하고, 소비 커밋 때만 정확한 수량을 제거한다. 음식 기존 BOM·제작 WU·출력량·가격은 유지
직접 작업량과 계산 근거: 1 WU=실제 작업 1초. 식사 행동 4초, 하루 허기 감소 50, 일반선 50, 긴급선 20, 포만 상한 115, 간식 쿨다운 15초. 운반·경로·대기 시간은 Phase 155 순 WU에서 실제 일과 비용으로 계산
EWU와 목표 회수 기간: 예약/집약 자체는 EWU를 감면하지 않는다. 버퍼 집약은 물리 엔티티·저장·검색 비용을 줄일 뿐 재료·신선도·수량·소유권을 복제하지 않음
공간·전력·물·연료·정비: 시설별 버퍼와 좌석, 실제 경로와 운반을 요구한다. Meal/ProductionInput 동일 cohort만 시설 버퍼에서 최소 스택 수로 집약하며 다른 목적·시설은 분리
위험·실패·회복 방식: 수량 경합은 명시적 부족으로 실패한다. 운반/버퍼 부패는 해당 Slice만 무효화하고 재탐색한다. 식사 시작과 소비 직전 이중 검증으로 상한 음식·좌석 대기 데드락을 fail-soft 처리
사회·비가역 비용: 식사 기분은 실제 소비 성공 뒤 최근 180초 최고 하나만 적용한다. 자동 품질 선택은 식사 버프를 뺀 기저 기분과 개인·문화 정책을 사용
기존 대안과의 장단점: 전체 스택 잠금은 단순하지만 거짓 품절과 원거리 탐색을 만든다. 수량 Lease는 동시성을 높이는 대신 Slice 원장·복원 힌트·원자적 집약 검증이 필요
지배 전략 방지 조건: operation 재시도 예약 복제 0, 분할·병합 수량 보존, 2초 미만 보수적 신선도 손실, 저장 재시작 소유권 탈취/재굴림 0, 부패 음식 섭취 0, 전 세계 핫패스 순회 0
저장 권위와 실행 명령: 물리 스택·운반 자식과 Task Intent 점유 힌트만 저장. runtime Lease/TTL/예약 합계/경로·집약 캐시는 미저장 재생성. 신형 저장은 기존 점유권을 우선 일괄 복원한 뒤 AI를 시작
자동 감사 ID와 전수 목록 포함 여부: V26_ITEM_QUANTITY_LEASE, V26_BUFFER_AGGREGATION, V26_MEAL_LOGISTICS, V26_RESERVATION_GRANDFATHER, 모든 물리 아이템 소비자 전수 검색
검증 매트릭스와 보고서 위치: task_plan.md Phase 156, Artifacts/QA/v26-item-quantity-lease.md, Artifacts/QA/v26-meal-logistics.md, Artifacts/QA/full-world-round-trip-playmode-report.txt
현재 밸런스 상태: 구현 중. 픽업 시 active Lease의 Slice가 물리 운반 자식으로 이동하고 버퍼 집약 후 canonical stack으로 재지정되는 경로, 100개 동시 Lease의 MaxStack 75 기준 2개 물리 스택 집약·개별 소비, 운반 중 owner/stack/quantity Grandfather 저장 복원은 Unity MCP 계약으로 통과했다. 64/tick 지연 큐·전역 저장 변이 장벽/AI 복원 게이트·실제 region/A*·진단 UI·Profiler/PlayMode/68-68-68 및 Phase 155 기술 단계별 순 WU 재계산 전에는 밸런스 완료 아님
```

## V26-157 기술 단계별 WU·비상 노동·경보 히스테리시스 기록 (2026-08-12)

```text
정의 ID: balance:settlement-labor-technology-v26 / architecture:emergency-work-accounting-v26 / architecture:settlement-alert-hysteresis-v26
콘텐츠 종류: 정착지 노동 WU, 기술 투자 회수, 연구·대형 프로젝트 동시 작업, 비상 예비 노동, 위협 경보, 인구 유입
정의·카탈로그·실행기 위치: SettlementLaborBalanceRules, WorkTypeCatalog.EmergencyFlags, EmergencyWorkAccountingRuntime, SettlementAlertRuntime, WorkTaskExecutor, EventAlertSaveSection
등장 시대와 연구: Day 1 무연구부터 Day 960 이후. 기술 효과는 실제 연구·BOM·건설·설치·물류 비용을 지불한 뒤 연결된 소비처에서만 적용
플레이어에게 주는 새 결정: 성장 작업과 즉시 회수 가능한 예비 작업의 배분, 프로젝트별 효율 인원/긴급 추가 인원, 기술 투자 회수기간, Amber 준비와 Red 대응, 정착 후보 수용 기준을 선택
물리 BOM·입력·출력: 신규 무료 자원 없음. 자동화는 지정 영역의 물리 입력·연료·운반·정비·고장·부패를 차감한 순산출만 인정하고 다른 영역 노동으로 전용하지 않음
직접 작업량과 계산 근거: 하루 180초 시간표의 작업 가능 구간 100초와 전환 효율 0.99가 만드는 99는 이론 상한이다. 실제 AI·욕구·동선·대기·작업 선택을 포함한 5일 안정 표본 `19.882 WU/인·일`을 반올림한 `20 WU/인·일`을 무연구 기준으로 사용한다. 욕구 붕괴가 발생한 14.343 및 8.444 표본은 실패 표본으로 제외한다.
EWU와 목표 회수 기간: 실제 노동 WU와 산출 등가 WU를 분리한다. 초기 4~12일, 초기-중기 12~30일, 중기 30~60일, 산업기 60~120일, 후기 120~240일 회수 목표. Day 1/30/120/240/400/960 1인 산출 지수 목표는 1.00/1.09/1.25/1.49/1.70/2.00
공간·전력·물·연료·정비: 시설·연구 슬롯과 프로젝트 단계별 최대 인원을 실제 요구한다. 랜드마크 최대 8명, 기본 자동 배치 5명, 8명 누적 기여 5.00명. 연구는 단일 활성 프로젝트와 1/2/4명, 1.00/0.70/0.45/0.25 기여 곡선
위험·실패·회복 방식: 31개 작업은 예비 즉시/체크포인트, 중단 불가, 비상 대응, 보호 회복 중 하나의 유효 비트 조합을 가진다. 실제 승인 WU만 milli-WU 원장에 반영하며 day-end/save/restore/조건부 Red 감사에서 Ground Truth와 재조정
사회·비가역 비용: 위협 경보와 노동 취약성 표시는 별도 권위다. 예비 부족만으로 무장하지 않는다. 정착 유입은 실제 방문·관계·체류 경험·수용 능력·정책을 사용하며 목표 인구와의 차이를 확률에 사용하지 않음
기존 대안과의 장단점: 단일 광역 생산 배율은 간단하지만 생활시간·시설 처리량·자동화·인구를 중복 계산한다. 분리 채널은 추적 가능하나 모든 실제 소비처와 유지비를 연결해야 함
지배 전략 방지 조건: 프로젝트 인원 캡과 한계효율, 연구 슬롯/시료/전력, 자동화 영역 제한, 동적 예비력, 재난 Shadow Simulation, 인구 수 진행 게이트 금지. 30명 고효율 정착지도 64명 저효율 정착지와 같은 기술 조건을 달성 가능해야 함
저장 권위와 실행 명령: 증분 회계 캐시는 저장하지 않고 활성 Intent에서 재구축한다. 확정/희망 경보, Epoch, 활성 사건, 하향 안정화, 중단 작업·복귀 큐는 저장한다. 현재 첫 수직 슬라이스는 기존 event-alert 섹션을 V3으로 확장해 경보·사건·커버리지 시간을 저장
자동 감사 ID와 전수 목록 포함 여부: PHASE157_EMERGENCY_LABOR, 31개 WorkType 전수, 180초/99초 이론 작업구간과 20 WU 라이브 기준의 분리, 랜드마크 8=5.00, 연구 4=2.40, milli-WU 멱등성, Red→Amber→Green 2+2시간, 커버리지 Schmitt threshold, 경보 저장 왕복
검증 매트릭스와 보고서 위치: task_plan.md Phase 157, findings.md Phase 157, progress.md Phase 157. Unity MCP focused/PlayMode/Profiler/68-68-68 보고서는 검증 뒤 추가

Phase 157 live-consumer record (2026-08-12): character-executed WU is credited only after a domain accepts progress through the central approved-work gate. Automatic production is credited to its own domain channel from the production bill's accepted delta and is not transferable growth labor. Work-order and domain-persistent operations may suspend only at an accepted-progress checkpoint; local unsaved loops finish before reassignment. Repeated alert reads reuse a mutation-invalidated snapshot so incident/suspended-work collection rebuilding is not a normal-frame cost. Physical spoilage/fire/breakdown loss values are not inferred where no authoritative producer exists.

Phase 157 facility-project record (2026-08-12): facility construction scale is authored on `BuildingWorkAmountAbility`, not inferred from footprint at runtime. Current modular content maps authored progression phase 1/2/3 to Small/Medium/Industrial construction and therefore hard caps 2/3/4 simultaneous workers. Each accepted worker keeps actual labor WU separate from diminished project contribution, while the construction UI exposes active/max/effective workers and the next worker's expected time saving. Construction BOM, required work, facility output, research unlock, maintenance and quality rules do not change. This is a concurrency/snowball-control connection, not free EWU; the live multi-worker PlayMode timing and full ROI/multi-stage WU simulation remain required before balance completion.

## True-start primitive survival content record (2026-08-12)

```text
Definition IDs: survival:field-meal / survival:floor-rest / survival:primitive-latrine / survival:bucket-wash
Content type: founder opening survival fallback and AI priority transition
Physical BOM and output: field meal consumes one authored edible ration; bucket wash consumes one clean water; floor rest consumes no item; latrine creates Waste 8 and Stain 2 at the selected cell. No action creates food, water or construction material.
Time and WU: action times are 4 / 60 / 6 / 6 game seconds. They are self-care time and therefore subtract from available labor rather than producing transferable WU.
Risk and cost: floor rest applies Hygiene -4 and Mood -3; latrine applies Hygiene -8 and Mood -2 plus filth; field meal and wash require a physically available item and commit it only at the final action frame.
Alternatives: authored meal, rest, toilet and hygiene facilities remain the efficient routine alternatives. Primitive utility is 0.65 during routine need and 1.00 only at the emergency threshold, where deterministic tie ordering prevents a stale or unresolved facility action from causing deprivation collapse.
Exploit prevention: starter totals are explicit (ration 24, water 30); carried-to-warehouse transfer preserves the existing physical identity; reservation, movement or cancellation cannot mint an item; action-local completion events record exact physical cost. In-progress primitive effects are committed only at completion, so interruption before the final frame grants no recovery and consumes no item.
Execution authority: CharacterAiDecisionPipeline survival interrupt -> branch job giver -> AIPrimitiveSurvivalAction -> CharacterPrimitiveSurvivalRunner -> physical item reservation/consumption and CharacterPrimitiveSurvivalCompletedEvent.
Save authority: physical stacks and needs use their existing world/character save authorities. The short in-progress primitive coroutine is not a material authority; after restore AI replans, and no uncommitted item or recovery is partially restored.
Deterministic verification: Artifacts/QA/primitive-start-survival-5day-report.txt and Artifacts/QA/primitive-survival-focused-report.txt. Natural five-day result is three survivors at 100 health, zero active breakdown, ration 24->15, water 30->22, no quantity increase. Focused result proves all four AI actions, exact action-event item costs and positive need recovery. Final natural-run Console Warning/Error is 0/0.
Balance status: true-start survival transition verified. Phase 157 technology-stage net-WU and whole-game balance remain in progress.
```
현재 밸런스 상태: 구현 중. 기준 공식·31개 작업 분류·실제 작업 승인량 증분 회계·일일/저장 전 재조정·침입/중상 사건 경보·단계별 히스테리시스·경보 저장의 첫 수직 슬라이스를 작성했다. 작업 중단 Lease 보존/복귀 큐, 프로젝트 라이브 동시성, 기술별 실제 소비처, Shadow Simulation, 인구 유입, UI와 Unity MCP 전체 검증 전에는 WU·비상 대응·인구 밸런스 완료 아님
```

## D03 도축·R07 대형 사업 시설 연결 교정 기록 (2026-08-14)

```text
정의 ID: facility:D03:butcher-work / facility:R07:grand-project-work
콘텐츠 종류: 조리손질대 도축 작업 시설, 영주집무책상 대형 사업 작업 시설
정의·카탈로그·실행기 위치: ModularFacilityAssetBuilder, D03_조리손질대.asset, R07_영주집무책상.asset, BuildingButcherAbility, ButcherWorkExecutionHandler, GrandProjectRuntime
등장 시대와 연구: 기존 D03·R07의 등장 시대와 해금 조건을 그대로 유지하며 신규 연구·무료 해금을 추가하지 않음
플레이어에게 주는 새 결정: 기존에 저술되어 있었지만 직렬화 연결이 빠진 D03 도축 작업과 R07 대형 사업 작업자를 실제 시설에 배치할 수 있게 함
물리 BOM·입력·출력: 건설 BOM, 도축 사체 입력·고기 출력, 대형 사업 BOM과 산출을 변경하지 않음. 각 실행기는 기존 물리 입력과 커밋 규칙을 그대로 사용
직접 작업량과 계산 근거: D03 BuildingButcherAbility.workSeconds=1은 기존 ButcherWorkExecutionHandler의 기본값 1과 동일함. R07 건설·수리·운영·대형 사업 요구 WU 및 프로젝트 기여 곡선은 변경하지 않음
EWU와 목표 회수 기간: 신규 생산 보너스나 WU 감면이 없으며 기존 authored 작업의 도달 가능성만 복구하므로 시설 EWU·회수기간 변경 없음
공간·전력·물·연료·정비: D03 폭 2, R07 폭 2와 기존 공간·유틸리티·정비 조건을 그대로 유지
위험·실패·회복 방식: 사체·대형 사업·재료·시설 상태가 부적격하면 기존 typed 실패를 유지. 능력 또는 지원 작업 타입이 다시 누락되면 targeted builder 검증과 modular facility 자동 감사가 실패
사회·비가역 비용: 변경 없음
기존 대안과의 장단점: D03은 조리와 도축을 함께 지원하지만 도축 사체·작업량을 우회하지 않음. R07은 운영·수리와 대형 사업을 함께 지원하지만 프로젝트 인원 상한·BOM·단계 제한을 우회하지 않음
지배 전략 방지 조건: 무료 사체 처리·무료 프로젝트 진행 0, 동일 작업 이중 완료 0, D03 지원 작업은 Cook+Butcher 정확히 2종, R07 지원 작업은 Operate+Repair+GrandProject 정확히 3종
저장 권위와 실행 명령: BuildingSO는 불변 시설 정의, 실제 사체·프로젝트·작업 Intent는 기존 런타임/저장 권위를 유지. 신규 저장 필드 없음
자동 감사 ID와 전수 목록 포함 여부: ModularFacilityAssetBuilder.ValidateCriticalWorkTypeWiringAssets, ModularFacilityDebugScenarios.RunAll; D03 능력 1건과 정확한 2개 작업 ID, R07 정확한 3개 작업 ID를 대상으로 함
검증 매트릭스와 보고서 위치: DungeonStory/Content/Patch D03/R07 Work Wiring, DungeonStory/Debug/Facilities/Run Modular Facility Checks; Unity MCP 컴파일·감사 결과는 실행 후 기존 modular facility report에 기록
현재 밸런스 상태: 정의·직렬화 연결 교정 및 정적 검토 완료. 수치·BOM·WU 변화 없음. Unity MCP 컴파일과 targeted modular facility 감사를 아직 실행하지 않았으므로 연결 검증 완료 또는 전체 밸런스 완료로 보고하지 않음
```

## Dry-fallback sanitation water-delivery authority record (2026-08-15)

```text
Definition IDs: facility:H01:dry-fallback-water-authority / survival:clean-water:priority
Content type: sanitation fallback and physical clean-water delivery arbitration
Physical BOM and output: unchanged. A dry-capable fixture consumes no clean-water item when it commits the authored dry fallback; non-dry fixtures still require one physical clean-water container or piped supply.
Time and WU: unchanged. Facility service duration, recovery, construction WU, maintenance WU and worker opportunity cost are not modified.
Risk and cost: dry use retains its authored filth, mood and recovery tradeoffs. It no longer earmarks unrelated loose drinking water for an optional manual-water upgrade after choosing dry completion.
Alternatives: piped supply and already-buffered manual water remain preferred. A fixture that cannot run dry continues to publish a physical FacilityBuffer delivery request and remains unavailable until it is supplied.
Exploit prevention: the correction does not create water, convert abstract stock, relax reservation validation, or allow destination-bound FacilityBuffer stock to be consumed as loose drinking water.
Execution authority: WaterFixtureUseRuntime.TryBeginUse selects piped -> existing manual buffer -> dry fallback. A delivery intent is emitted only when the fixture cannot complete through dry fallback.
Save authority: unchanged physical world-item stacks, destination IDs, fluid state, and facility state remain authoritative.
Deterministic verification: DailyRoutineWuPlayModeVerifier.RequestRun(157181) must retain loose/Stored drink candidates, record zero harmful thirst stalls, and preserve exact water depletion and reservation conservation. Multi-seed 157181..157183 remains required.
Balance status: runtime authority correction implemented; fresh multi-seed Unity evidence is pending.
```

## H03 hygiene recovery calibration record (2026-08-15)

```text
Definition ID: facility:H03:hygiene-recovery
Content type: authored hygiene facility recovery cadence
Physical BOM and output: unchanged. H03 construction materials, clean-water input, wastewater/manual-waste output, and physical buffer authority are unchanged.
Time and WU: service time and all construction, cleaning, repair, and maintenance WU are unchanged. Hygiene recovery per completed H03 use changes from 62 to 45, matching NeedBalanceCalibrationScenario's canonical hygiene recovery.
Space, power, water, and maintenance: unchanged one-cell footprint, facility capacity, manual/piped water rules, drainage risk, and maintenance requirements.
Risk and alternatives: lower per-use recovery raises neutral adult H03 use from the observed 0.467/day toward the required 0.6~1.0/day and increases self-care opportunity cost. H04 and primitive bucket wash retain their distinct recovery/cost profiles.
Exploit prevention: recovery is granted only by the existing completed facility-use authority after physical water/fallback commit; cancellation, queueing, or failed supply grants no recovery.
Execution authority: BuildingNeedRecoveryAbility -> BuildingVisitorPort -> CharacterStats.RecoverNeed(HYGIENE). No new runtime authority is added.
Save authority: unchanged character need state and authored BuildingSO asset.
Deterministic verification: ModularFacilityDebugScenarios.RunAll validates exact minimum recovery; DailyRoutineWuPlayModeVerifier.RequestRun(157181..157183) must produce 0.6~1.0 completed/right-censored H03 uses per actor-day with zero harmful stalls and exact water conservation.
Balance status: authored builder and asset calibrated; fresh three-seed Unity evidence is pending.
```

## U01~U04 유틸리티 작업 런타임 연결 교정 기록 (2026-08-15)

```text
정의 ID: facility:industrial:U01-U04:work-runtime-wiring
콘텐츠 종류: 전력선·상수관·하수관·통합 기반 덕트의 수리/배관 작업 런타임 연결
정의·카탈로그·실행기 위치: IndustrialInfrastructureAssetBuilder, U01~U04 BuildingSO, FluidNetworkRuntime, PlumbingWorkExecutionHandler, WorkTargetSelector
등장 시대와 연구: 기존 U01~U04의 연구 해금과 등장 단계는 변경하지 않음
플레이어에게 주는 새 결정: 기존 정의에 저술된 Repair/Plumbing 유지보수 수요가 실제 AI 작업 후보와 작업자 배치로 연결됨. 신규 작업이나 무료 해금은 추가하지 않음
물리 BOM·입력·출력: 네 시설의 건설 BOM, 수리 재료, 물·오수·전력 흐름, 배관 작업 출력은 변경하지 않음
직접 작업량과 계산 근거: 기존 BuildingWorkAmountAbility와 PlumbingWorkExecutionHandler의 `8 + blockage×0.25 + leak×0.30 WU`를 그대로 사용. runtime archetype만 Generic에서 Facility로 교정하므로 WU·속도·수율 수치 변화 없음
EWU와 목표 회수 기간: 신규 생산 보너스나 유지비 감면이 없고 기존 유지보수 작업의 도달 가능성만 복구하므로 시설 EWU와 기술 회수기간 목표는 변경 없음
공간·전력·물·연료·정비: 폭 1, Utility 레이어, 기존 채널·처리량·정비 수요를 유지. Facility 런타임은 역할 없는 작업자 슬롯만 제공하며 방문객 역할·좌석·생산 공간을 새로 만들지 않음
위험·실패·회복 방식: 실제 blockage/leak가 0이면 NoWork, 작업 중 대상 파괴·수요 해소·경로 실패는 typed terminal로 종료. blockage/leak 변화는 후보 캐시 revision을 갱신해 stale 후보와 영구 미발견을 방지
사회·비가역 비용: 변경 없음
기존 대안과의 장단점: Generic 유지 시 렌더링은 단순하지만 IWorkableFacility 권위에 들어오지 않아 저술된 유지보수가 실행 불가능함. Facility 연결은 기존 작업자 예약·경로·실패 계약을 재사용하며 방문객 admission 권위를 바꾸지 않음
지배 전략 방지 조건: 무료 수리·무료 배관 0, 동일 수요 이중 완료 0, blockage/leak 0인 시설의 후보 0, 실제 수요 변경당 candidate revision 갱신, 역할 없는 유틸리티가 방문객 시설로 노출되지 않음
저장 권위와 실행 명령: BuildingSO는 불변 정의, FluidNetworkRuntime state store의 blockage/leak가 런타임·저장 권위. 후보 캐시는 저장하지 않고 정의·유체 상태에서 재구축. 신규 저장 필드 없음
자동 감사 ID와 전수 목록 포함 여부: IndustrialInfrastructureAssetBuilder.BuildAll, CharacterAiWorkTypeLiveMatrixPlayModeVerifier `work:plumbing`; U01~U04 runtimeArchetype=Facility, U02~U04 Plumbing 지원, 실제 maintenance publication과 typed cancel/invalidation을 확인
검증 매트릭스와 보고서 위치: `DungeonStory/Content/Build Industrial Infrastructure` targeted rebuild 후 `CharacterAiWorkTypeLiveMatrixPlayModeVerifier.RequestRun()`; `Artifacts/QA/character-ai-worktype-live-matrix.txt`
현재 밸런스 상태: 정의 생성기·런타임 후보 publication·정적 계약 교정 완료. 수치·BOM·WU 변화 없음. Unity MCP targeted asset rebuild, 컴파일, full 20-row PlayMode와 Console 0/0을 통과하기 전에는 연결 검증 완료 또는 전체 밸런스 완료로 보고하지 않음
```

## Q03 연구 청사진 물리 보관 연결 교정 기록 (2026-08-15)

```text
정의 ID: building:1032 / facility:Q03:research-blueprint-archive
콘텐츠 종류: 연구용책장 청사진 물리 보관 능력의 정의·직렬화 연결 교정
정의·카탈로그·실행기 위치: Q03_연구용책장.asset, ResearchProjectAssetBuilder.AttachArchiveAbility, ResearchBlueprintArchiveAdapter, BlueprintResearchRuntime
등장 시대와 연구: 기존 Q03 등장 단계·해금 조건을 그대로 유지하며 신규 연구·무료 해금을 추가하지 않음
플레이어에게 주는 새 결정: 상점에서 산 실제 청사진을 AI가 연구실 책장으로 운반한 뒤 플레이어가 연구 큐에 넣는 기존 저술 흐름을 복구
물리 BOM·입력·출력: Q03 목재 4 BOM, 청사진의 고유 물리 아이템 1개와 구매 금화 비용을 그대로 유지. 무료 청사진·복제·원격 보관 없음
직접 작업량과 계산 근거: Q03 건설 48 WU, 수리 12 WU, 기존 운반·연구 WU를 변경하지 않음. 보관 능력은 작업량을 감면하거나 연구를 자동 완료하지 않음
EWU와 목표 회수 기간: 신규 산출·속도 보너스가 없고 누락된 authored 경로만 복구하므로 기존 시설 EWU와 연구 25~40/70~120/180~280일 목표 밴드를 변경하지 않음
공간·전력·물·연료·정비: Q03 1×1 면적, 내부 저장 10, 정비 1과 기존 Research room 요구를 유지. BuildingResearchArchiveAbility 용량은 기존 빌더·감사 권위와 같은 8
위험·실패·회복 방식: Research room 부적격, 경로 없음, 목적지 가득 참, 물리 아이템·예약 소실은 typed blocked/terminal로 남기며 재시도·복원 시 같은 청사진 ID를 사용
사회·비가역 비용: 변경 없음. 구매 금화와 연구자·운반자 기회비용은 기존 권위를 유지
기존 대안과의 장단점: 일반 창고는 임시 물류 대안일 뿐 연구 큐 권위가 아니며 Q03을 대체하지 못함. 여러 Q03은 기존 BOM·공간·정비를 지불한 만큼만 슬롯을 늘림
지배 전략 방지 조건: 현재 청사진 7종 대비 8칸, 고유/max-stack-1 물리 청사진만 계산, 구매·배송·저장·큐 등록 exactly-once, 취소·저장복원·재시도로 아이템·금화·큐 복제 0
저장 권위와 실행 명령: Q03 SO가 불변 용량을, FacilityShop acquired IDs·물리 WorldItemStack·BlueprintResearchState가 각 런타임 저장 권위를 소유. archive query/destination은 stable building persistent ID에서 재계산하며 신규 저장 필드 없음
자동 감사 ID와 전수 목록 포함 여부: ResearchTreeDebugScenarios 180 프로젝트·7 청사진·archiveConfigured, BlueprintResearchDebugScenarios, FirstRunObjectivePlayModeVerifier 실제 UI 구매/AI 운반/수동 큐
검증 매트릭스와 보고서 위치: Unity MCP targeted AttachArchiveAbility, ResearchTreeDebugScenarios, Temp/first-run-objective-report.txt, 청사진 loose/in-transit/archived 저장 왕복
현재 밸런스 상태: 정의·직렬화 연결 교정 기록 완료. Q03 기존 수치·BOM·WU·ROI 변화 없음. Unity MCP targeted 에셋 저장, 연구 감사, 실제 FirstRun, 저장 왕복과 Console 0/0을 모두 통과하기 전에는 연결 검증 완료 또는 전체 시설·연구 밸런스 완료로 보고하지 않음
```

## Q01~Q06 연구 시설 능력 직렬화 연결 교정 기록 (2026-08-15)

```text
정의 ID: facility:Q01:research-basic / Q02:research-basic+arcane / Q03:research-archive / Q04:research-reagent / Q05:research-specimen / Q06:research-design / P19:research-basic+reagent+arcane / P1_ResearchLab:research-basic+archive+advanced
콘텐츠 종류: 기존 연구 시설의 `BuildingResearchCapacityAbility` 정의·직렬화 연결 교정
정의·카탈로그·실행기 위치: Q01~Q06·P19·P1_ResearchLab BuildingSO, ResearchProjectAssetBuilder.AttachArchiveAbility, ResearchFacilityCapacityQuery, BlueprintResearchProjectCoordinator, ResearchWorkExecutionHandler
등장 시대와 연구: 각 시설의 기존 등장 단계·해금 조건을 유지하고 신규 연구·무료 해금·시설을 추가하지 않음. Q01의 기존 시작 해금 권위를 복구함
플레이어에게 주는 새 결정: 새 선택지를 추가하지 않고, 이미 문서화된 연구 시설 조합을 건설·유지하여 프로젝트의 Basic/Archive/Reagent/Specimen/Design/Arcane/Advanced 요구를 충족하는 기존 결정을 다시 작동시킴
물리 BOM·입력·출력: 모든 시설의 기존 BOM과 청사진·시약·표본·설계·비전 물리 경로를 유지. 능력 연결은 자원을 생성·삭제·복제하거나 원격 공급하지 않음
직접 작업량과 계산 근거: 각 시설 건설·수리·정비 WU와 프로젝트 requiredWork를 변경하지 않음. 연구 능력 수치는 기존 빌더 권위(Q01 Basic 1, Q02 Basic 1+Arcane 1, Q03 Archive 1, Q04 Reagent 1, Q05 Specimen 1, Q06 Design 1, P19 Basic 1+Reagent 1+Arcane 1, P1 연구소 Basic 2+Archive 1+Advanced 1)를 그대로 직렬화함
EWU와 목표 회수 기간: 신규 생산량·연구 속도·작업량 감면이 없으므로 기존 시설 및 연구 단계의 EWU·회수 기간 목표를 변경하지 않음
공간·전력·물·연료·정비: 각 시설의 기존 footprint, 방 요구, 전력·물·연료·정비 수치를 변경하지 않음. 능력 합산은 실제 operational building만 계산함
위험·실패·회복 방식: 시설 미건설·비가동·부족 수량은 프로젝트를 suspended 상태와 typed blocker로 유지하며 AI 정책은 연구 작업을 거부함. 에셋 연결 누락을 queued-but-inactive fallback으로 숨기지 않음
기회비용·비가역 비용: 변경 없음. 필요한 시설의 기존 BOM·공간·정비·해금 비용이 연구 진행의 기회비용으로 계속 남음
기존 대안과의 장단점: Q01/Q03 조합은 기록 연구의 최소 경로이고 고급 연구소는 더 큰 기존 비용으로 복수 능력을 제공함. 일반 작업대·창고는 연구 능력 대안으로 간주하지 않음
지배 전략 방지 조건: 여러 시설은 실제로 건설·가동된 수만 합산하며 한 시설 모듈을 중복 직렬화하지 않음. 저장·복원·빌더 재실행 후 동일 BuildingSO에 동일 능력 모듈 exactly-one을 유지함
저장 권위와 실행 명령: BuildingSO가 불변 연구 능력 기여를 소유하고 ResearchFacilityCapacityQuery가 live operational building에서 재계산함. 프로젝트 queue/active 상태의 기존 저장 권위를 유지하며 신규 저장 필드 없음
자동 감사 ID와 필수 목록 포함 여부: ResearchTreeDebugScenarios의 180 프로젝트·시설 요구 감사, ResearchProjectAssetBuilder의 exact capability mapping, FirstRunObjectivePlayModeVerifier의 active project·실제 AI 연구 작업을 필수 증거로 사용
매트릭스와 보고서 위치: Unity MCP `ResearchProjectAssetBuilder.PatchQ03ArchiveAbility`, `ResearchTreeDebugScenarios.RunAll`, `Temp/first-run-objective-report.txt`; Q01~Q06/P19/P1 연구소 ability module exact-one 및 Console 0/0을 함께 기록
현재 밸런스 상태: 기존 정의의 직렬화 연결 교정이며 수치·BOM·WU·ROI 변화 없음. targeted 에셋 저장과 fresh FirstRun·연구 감사가 통과하기 전에는 연결 검증 완료로 보고하지 않음
```

## 연구 승인 WU 진행량 커밋 일치 교정 기록 (2026-08-15)

```text
정의 ID: work:research:approved-wu-commit
콘텐츠 종류: 연구 작업 사이클의 승인 작업량과 프로젝트 진행량 연결 교정
정의·카탈로그·실행기 위치: BuildingWorkAmountAbility.researchWorkRequired, ResearchWorkExecutionAdapter, WorkTaskExecutor.ExecuteWorkAmount, BlueprintResearchRuntime.ApplyResearchWork
등장 시대와 연구: 기존 연구 단계·해금·프로젝트 목록을 유지하고 신규 연구나 무료 해금을 추가하지 않음
플레이어에게 주는 새 결정: 새 선택지는 없으며, 실제 연구자가 완료한 승인 WU가 프로젝트 진행 권위에 같은 양으로 반영되는 기존 계약을 복구
물리 BOM·입력·출력: 시설 건설 BOM, 청사진·시약·표본·색인 입력, 연구 해금 출력은 변경하지 않음
직접 작업량과 계산 근거: 연구 시설의 한 사이클은 authored `researchWorkRequired` 전량을 WorkTaskExecutor에서 승인받은 뒤에만 커밋함. 프로젝트에는 `승인 사이클 WU × 프로젝트 인력 기여 배수`를 exactly-once 적용함. 기존 구현은 Q01의 6 WU 승인 뒤 고정 1 WU만 커밋하여 5 WU를 소실했음
EWU와 목표 회수 기간: 프로젝트 requiredWork·시설 작업량·연구 속도 수치를 새로 조정하지 않고, 이미 지불한 WU의 소실만 제거하여 기존 연구 목표 밴드와 일치시킴
공간·전력·물·연료·정비: 변경 없음
위험·실패·회복 방식: 작업 중단·취소·시설 소실로 승인 사이클이 끝나지 않으면 프로젝트 커밋은 0. 성공한 사이클만 한 번 반영하고 재시도는 새 작업 사이클 권위를 사용
기회비용·비가역 비용: 연구자 시간·시설 점유·욕구 소모·물리 입력의 기존 비용을 유지하며 승인되지 않은 작업을 보상하지 않음
기존 대안과의 장단점: Editor 직접 진행도 주입이나 instant-work 우회 없이 Brain→AIWork→AbilityWork→WorkTaskExecutor의 실제 승인 경로만 사용
지배 전략 방지 조건: 인력 기여 배수는 한 번만 적용한다. 캐릭터·시설 작업 속도는 WorkAmountCalculator가 승인 WU를 만드는 동안 이미 한 번 반영하므로 프로젝트 커밋에서 재적용하지 않는다. 메타 연구 배수·비전 색인·명시적 연구 output 보너스만 각자의 권위에서 한 번 적용하며, 취소·저장복원·재계획으로 같은 사이클을 중복 커밋하지 않는다
저장 권위와 실행 명령: BlueprintResearchState가 프로젝트 진행 저장 권위를 유지하며 신규 저장 필드 없음. 작업 사이클은 저장하지 않고 중단 시 기존 규칙대로 재시작
자동 감사 ID와 필수 목록 포함 여부: FirstRunObjectivePlayModeVerifier `PROJECT_COMPLETED_BY_WORK_ROUTINE`, CharacterAiWorkTypeLiveMatrixPlayModeVerifier `work:research`, BlueprintResearchDebugScenarios의 진행·속도 감사
매트릭스와 보고서 위치: `Temp/first-run-objective-report.txt`, `Artifacts/QA/character-ai-worktype-live-matrix.txt`, Unity Console 0 Error/Warning
현재 밸런스 상태: 승인 WU 커밋 일치 교정 구현 완료. 수치·BOM·프로젝트 requiredWork 변화 없음. fresh FirstRun과 연구 작업 매트릭스를 통과하기 전에는 연결 검증 완료 또는 연구 밸런스 완료로 보고하지 않음
```

## 전략 원정 명령열 전투 교착 교정 기록 (2026-08-15)

```text
정의 ID: offense:strategic-command-battle:planned-turn-resolution
콘텐츠 종류: 전략 원정 명령열 전투의 충돌·적 의도 소유권·동시 턴 진행 연결 교정
정의·카탈로그·실행기 위치: OffenseBattleDirector.ResolveTurn, OffenseCommandResolutionAdapter, OffenseBattleRuntime.FinalizePlannedTurn, OffenseBattleSession.FinalizePlannedRound
등장 시대와 연구: 기존 전략 원정 전투 전체에 적용하며 신규 연구·카드·적·보상을 추가하지 않음
플레이어에게 주는 새 결정: 새 선택지는 없으며, 실행 불가능한 카드가 적 의도를 무료로 지우지 않고 실제 유효 명령만 충돌 결과를 소유하는 기존 전투 결정을 복구
물리 BOM·입력·출력: 원정 보급·탄약·약품·장비 내구·보상과 전리품 수치를 변경하지 않음
직접 작업량과 계산 근거: 정착지 WU·원정 준비 WU 변화 없음. 한 명령열 resolve는 적 의도를 최대 한 번 실행 시도하고 planned round/BeginTurn을 정확히 한 번 진행함
EWU와 목표 회수 기간: 신규 산출·보상·비용 감면 없음. 교착으로 전투가 끝나지 않던 결함만 제거하므로 기존 원정 EWU와 회수기간 목표를 유지
공간·전력·물·연료·정비: 변경 없음
위험·실패·회복 방식: 아군 명령이 Unavailable/IllegalTarget/Cancelled이면 충돌로 감소시킨 단계를 인정하지 않고 적 의도의 원래 full execution stages를 실행함. 실제 적 실행도 불가능하면 typed outcome과 reason을 남기며 임의 피해·이동·대상 fallback을 만들지 않음. terminal BattleCompleted가 director를 동기 제거해도 finalization 시작 시 캡처한 turn 소유 상태에 카드·queue·fence cleanup을 exactly-once 적용하고 완료 trace는 유지함. callback 중 다른 non-null battle state가 들어오면 이전 finalizer는 fail-fast하며 새 state의 pending·trace·카드 소유권을 전혀 변경하지 않음
사회·비가역 비용: 전투 부상·사망·포획·원정 부재의 기존 비용을 유지하며 무효 카드로 위험을 무료 상쇄할 수 없음
기존 대안과의 장단점: 유효한 Intercept/Break 명령은 기존 충돌 단계 감소를 유지함. 실행 불가 카드는 안전한 방어 대안이 아니며 적 행동을 받거나 다음 합법 명령을 선택해야 함
지배 전략 방지 조건: invalid 카드의 적 의도 무료 소비 0, 감소된 적 단계 무료 적용 0, 같은 director turn 중복 resolve·중복 BeginTurn 0, fallback 피해 0, 적 의도 실행 결과와 실패 이유의 durable trace 유지
저장 권위와 실행 명령: OffenseBattleDirectorStateData의 resolutionAppliedTurn/finalizedTurn과 OffenseBattleSession persistence의 preparedPlannedTurn/finalizedPlannedTurn이 명령 실행 적용과 라운드 마감의 멱등 토큰을 저장함. resolved trace는 런타임 관찰값이며 저장하지 않음. 구형 저장은 drawn candidates/command queue와 현재 라운드 상태에서 기준 토큰을 한 번 결합하고 BeginTurn을 재생하지 않음
자동 감사 ID와 필수 목록 포함 여부: OffenseStrategicDebugScenarios.VerifyCommandBattle, OffenseBattleDebugScenarios.VerifyPlannedRoundFinalization, OffenseJourneyPlayModeFacade strategic full journey
검증 매트릭스와 보고서 위치: `DungeonStory/Debug/Offense/Run Strategic Scenarios`, `DungeonStory/Debug/Offense/Run Turn Battle Scenarios`, `OffenseJourneyPlayModeFacade.RequestRun()`, `Artifacts/QA/offense-journey-playmode.txt`, Unity Console 0/0
현재 밸런스 상태: 생산 계약과 집중 회귀 구현 완료. 수치·카드·적·보상 변화는 없지만 실제 위험 처리 경로가 바뀌므로 fresh 정적 회귀와 전략 원정 PlayMode가 통과하기 전에는 연결 검증 완료 또는 전투 밸런스 완료로 보고하지 않음
```

## 전략 원정 명령 카드 행동 권위 후속 교정 기록 (2026-08-16)

```text
정의 ID: offense:strategic-command-battle:typed-action-authority-v27
콘텐츠 종류: 기존 전략 원정 명령 카드·적 의도의 행동 종류와 후열 교전 liveness 연결 교정
정의·카탈로그·실행기 위치: OffenseStrategicBattleSetupFactory, OffenseCommandCardStateData, OffenseEnemyIntentStateData, OffenseCommandBattleDirector, OffenseCommandResolutionAdapter, OffenseBattleRuntime, OffenseBattleSession
등장 시대와 연구: 기존 전략 원정 전투 전체에 적용하며 신규 연구·해금·적·보상·카드 수를 추가하지 않음
플레이어에게 주는 새 결정: 기존 8장 덱에서 중복 기본 공격 한 장이 기존 전투 행동인 전진 명령을 명시적으로 제공함. 후열 적도 도달 불가 공격 대신 현재 진형에서 합법적인 전진 의도를 표시함
물리 BOM·입력·출력: 원정 보급, 탄약, 장비, 약품, 전리품, 통화의 입력·출력과 물리 수량 변화 없음
직접 작업량과 계산 근거: 정착지 WU와 전투 카드 수 8, 후보 수 2, execution stage·speed·power·damage 수치는 변경 없음. 전진은 기존 한 행동과 한 planned command ID를 소비하며 피해·무료 추가 턴을 만들지 않음
EWU와 목표 회수 기간: 신규 산출·보상·비용 감면 없음. 불법 공격만 반복해 전투가 영구 정지하던 상태를 기존 전진 행동으로 해소하므로 원정 EWU 목표 자체는 유지
공간·전력·물·연료·정비: 변경 없음
위험·실패·회복 방식: 카드와 적 의도가 저장한 typed action만 실행함. Ability인데 skill ID가 없거나 현재 진형·대상이 불법이면 typed Unavailable/IllegalTarget로 남기고 다른 공격이나 이동으로 대체하지 않음. Advance와 Guard는 명시적 self target이며 후열 melee는 한 행동씩 전진함
사회·비가역 비용: 기존 전투 부상·사망·포획·원정 부재 비용 유지. 전진을 선택한 행동은 공격하지 않으므로 접근 중 적 행동과 턴 기회비용을 그대로 부담
기존 대안과의 장단점: 기본 공격·능력·방어는 즉시 효과 가능하지만 거리·진형 제한을 받음. 전진은 피해가 없고 한 턴을 소비하는 대신 이후 근접 행동의 합법 거리를 확보함
지배 전략 방지 조건: 전진 피해 0, 추가 카드·추가 행동·무료 충돌 단계 0, 한 planned turn당 command ID·BeginTurn·Finalize 정확히 한 번, invalid 명령의 적 의도 무료 제거 0
저장 권위와 실행 명령: OffenseCommandCardStateData.actionType과 OffenseEnemyIntentStateData.actionType이 행동 종류의 저장 원본이고 battle session이 진형·체력·상태의 유일 쓰기 권위임. sourceSkillId/actionId는 Ability일 때만 기술 ID로 소비함. 기존 V5 누락 필드는 종전 BasicAttack 의미로 읽고 신규 저장부터 typed action을 기록함
자동 감사 ID와 전수 목록 포함 여부: OffenseStrategicDebugScenarios.VerifyCommandBattle, OffenseBattleDebugScenarios의 planned-round/liveness 행, OffenseJourneyPlayModeFacade pointer-driven strategic journey, CharacterAiCoverageManifest offense journey/strategic markers
검증 매트릭스와 보고서 위치: Front unarmed allies 대 Rear melee enemy focused regression, strategic save clone/restore action-type 보존, unavailable interception exact-once, `Artifacts/QA/offense-journey-playmode.txt`, `Temp/OffenseStrategicValidation/offense-strategic-visual-report.txt`, Unity Console Warning/Error 0/0
현재 밸런스 상태: 수치·BOM·보상 영향 없음 / 행동 연결 검증 대기. focused 회귀와 실제 pointer-driven Journey·Strategic UI가 current source에서 통과하기 전에는 연결 완료 또는 전투 밸런스 완료로 보고하지 않음
```

## D03 합성 작업 권위 후속 교정 기록 (2026-08-16)

```text
정의 ID: facility:D03:composed-work-authority-v27
콘텐츠 종류: 조리손질대의 기본 조리·도축 작업과 산업 공정 유체 배관 작업의 합성 정의 검증
정의·카탈로그·실행기 위치: ModularFacilityAssetBuilder.ValidateCriticalWorkTypeWiringAssets, D03_조리손질대.asset, IndustrialInfrastructureAssetBuilder.PatchProcessFluidConsumers, IndustrialInfrastructureDebugScenarios.VerifySanitationAndProcessFluids, PlumbingWorkExecutionHandler
등장 시대와 연구: 기존 D03 해금 시점과 산업 기반·배관 연구 조건을 그대로 유지하며 신규 연구·무료 해금·시설을 추가하지 않음
플레이어에게 주는 새 결정: 없음. D03의 기본 Cook+Butcher 작업과 산업 공정 유체 오버레이가 저술된 경우의 Plumbing 유지보수 작업을 서로 덮어쓰지 않고 함께 검증함
물리 BOM·입력·출력: D03 건설 BOM, 조리·도축 입력과 출력, 공정당 깨끗한 물 0.25와 오수 0.25, 수동 급수 대안 및 물리 저장·운반 계약을 변경하지 않음
직접 작업량과 계산 근거: 조리·도축 WU와 BuildingButcherAbility.workSeconds=1을 변경하지 않음. Plumbing은 기존 `8 + blockage×0.25 + leak×0.30 WU`를 실제 막힘·누수 상태가 있을 때만 요구함
EWU와 목표 회수 기간: 신규 생산 보너스·작업량 감면·처리량 변경이 없고 검증기가 기존 합성 정의를 정확히 인식하도록 교정하므로 D03 EWU와 회수 기간 목표를 변경하지 않음
공간·전력·물·연료·정비: D03 2×1 면적과 기존 전력·자동화·컨베이어·상수·하수 연결을 유지함. 공정 유체 능력과 상수·하수 채널이 모두 저술된 경우에만 Plumbing을 필수·허용함
위험·실패·회복 방식: 공정 유체 능력과 상수·하수 채널이 부분적으로만 존재하면 targeted validator가 fail-loud함. 완전한 유체 오버레이에서는 Plumbing 누락을 실패시키고, 유체 오버레이가 없으면 기본 Cook+Butcher만 허용하며 그 밖의 작업 ID는 모두 거부함
사회·비가역 비용: 변경 없음. 조리·도축 작업자와 배관 작업자의 기존 숙련·기회비용·시설 정지 비용을 유지함
기존 대안과의 장단점: 기본 D03은 수동 조리·도축 경로를 유지하고 산업 유체 오버레이는 물 운반을 줄이는 대신 배관 장애·정비 노동을 요구함. Plumbing을 제거해 감사를 맞추면 유체 장애 회복 경로가 끊기고, 모든 추가 작업을 허용하면 무관한 작업 권위 drift를 숨김
지배 전략 방지 조건: 무료 조리·도축·배관 진행 0, 동일 작업 이중 완료 0, 유체 오버레이 없는 D03 작업은 Cook+Butcher 정확히 2종, 완전한 유체 오버레이가 있는 D03 작업은 Cook+Butcher+Plumbing 정확히 3종, 부분 유체 오버레이와 그 밖의 extra 작업 0
저장 권위와 실행 명령: BuildingSO FacilityData가 불변 작업 지원 정의를 소유하고 FluidNetworkRuntime state store의 blockage/leak가 배관 수요와 저장 권위를 소유함. PlumbingWorkExecutionHandler만 실제 수요가 있을 때 기존 AI 작업 명령으로 진행하며 신규 저장 필드는 없음
자동 감사 ID와 전수 목록 포함 여부: ModularFacilityAssetBuilder.ValidateCriticalWorkTypeWiringAssets와 ModularFacilityDebugScenarios.RunAll이 D03 core/overlay exact set을 검사하고, IndustrialInfrastructureDebugScenarios.VerifySanitationAndProcessFluids가 모든 Cook/Surgery 유체 소비자의 ProcessFluid·상수·하수·Plumbing 연결을 전수 검사함
검증 매트릭스와 보고서 위치: 정적 focused 검증은 D03 현재 bitmask가 Cook+Butcher+Plumbing과 일치하고 validator가 완전한 유체 오버레이에서 같은 exact set을 요구하는지 확인함. `DungeonStory/Debug/Facilities/Run Modular Facility Checks`, `DungeonStory/Debug/Industrial/Run Infrastructure Checks`, Unity Console Error/Warning 0/0은 fresh Unity 실행에서 후속 확인
현재 밸런스 상태: `밸런스 영향 없음 / 합성 권위 정적 검증`. 콘텐츠 수치·BOM·WU·처리량·위험량은 변경하지 않았으나 Unity를 실행하지 않았으므로 focused modular/industrial 감사와 Console 0/0 전에는 연결 완료 또는 시설 밸런스 완료로 보고하지 않음
```

## 연구 청사진 보관 LiveFacility 목적지 권위 교정 기록 (2026-08-16)

```text
정의 ID: architecture:research-blueprint-archive-live-facility-destination-authority-v27
콘텐츠 종류: Q03 연구용책장 청사진 운반 목적지의 exact 시설 소유권 연결 교정
정의·카탈로그·실행기 위치: BuildingResearchArchiveAbility, ResearchBlueprintArchiveQuery, BlueprintResearchRuntime, BlueprintResearchSaveSection, FacilityBufferDestinationClaimRegistry, WorldItemHaulDestinationAuthority
등장 시대와 연구: 기존 Q03 등장 시대·해금 조건·연구 프로젝트 순서를 그대로 유지하며 신규 연구·무료 해금을 추가하지 않음
플레이어에게 주는 새 결정: 새 선택은 없으며 상점에서 구매한 물리 청사진을 적법한 Q03까지 실제 AI가 운반한 뒤 연구 큐에 넣는 기존 결정을 복구
물리 BOM·입력·출력: Q03 목재 4, 청사진 고유 물리 아이템 1개, 구매 금화와 연구 해금 출력을 변경하지 않음. claim은 아이템을 생성·복제·소비하지 않음
직접 작업량과 계산 근거: Q03 건설 48 WU·수리 12 WU와 기존 운반·연구 승인 WU를 변경하지 않음. 목적지 권위 확인은 작업량·속도 배수를 추가하지 않음
EWU와 목표 회수 기간: 신규 생산·속도·자동 운반 보너스가 없고 누락된 실제 운반 admission만 복구하므로 기존 연구 25~40/70~120/180~280일 목표 밴드와 Q03 EWU를 변경하지 않음
공간·전력·물·연료·정비: Q03 1×1, 내부 저장 10, 보관 슬롯 8, 정비 1과 기존 Research room 조건을 유지하며 추가 전력·물·연료 비용 없음
위험·실패·회복 방식: exact persistent facility ID, 목적지 ID 또는 drop 좌표가 누락·불일치하면 계획·입고·복원을 fail-loud함. 적법한 archive 시설이 사라지면 좌표·prefix fallback 없이 기존 haul 실패/회수 경로로 복구
사회·비가역 비용: 변경 없음. 구매 금화, 운반자·연구자 시간과 시설 공간의 기존 기회비용을 유지
기존 대안과의 장단점: 일반 창고는 물리 임시 보관은 가능하지만 연구 archive 권위가 아니며 Q03을 대체하지 못함. 여러 Q03은 기존 BOM·공간·정비를 지불하고 각 persistent destination을 소유함
지배 전략 방지 조건: 무료 운반·teleport·same-cell 시설 추론 0, 청사진·금화·연구 큐 복제 0, 목적지 claim exactly-one, 저장복원·재계획·반복 poll의 중복 배송·중복 커밋 0
저장 권위와 실행 명령: BuildingSO의 BuildingResearchArchiveAbility와 live BuildableObject persistent ID가 불변/시설 권위를, WorldItemStack·haul intent가 물리 운반 권위를 소유. claim은 저장하지 않고 restore candidate buildings에서 participant 220 publish 전에 결정론적으로 재구축
자동 감사 ID와 전수 목록 포함 여부: ResearchTreeDebugScenarios archive claim restore/rollback, FirstRunObjectivePlayModeVerifier BLUEPRINT_ARCHIVE_DESTINATION_CLAIM_EXACT 및 Brain→AIHaul→FacilityBuffer 경로, CharacterAiCoverageManifestDebugScenarios 필수 marker/freshness에 포함
검증 매트릭스와 보고서 위치: ResearchTreeDebugScenarios.RunAll, Temp/first-run-objective-report.txt, DungeonAiActionSaveLoadPlayModeVerifier, Unity Console Warning/Error 0/0; missing/wrong destination·drop·facility restore는 전체 transaction 무변경 실패
현재 밸런스 상태: 밸런스 영향 없음 / 구조·연결 검증 대기. 수치·BOM·WU·속도·보상은 불변이며 fresh focused restore, FirstRun 실제 AI 운반, save/load 회귀와 Console 0/0을 모두 통과하기 전에는 연결 완료 또는 연구 밸런스 완료로 보고하지 않음
```

## 연구 청사진 보관 LiveBuilding anchor 후속 교정 기록 (2026-08-16)

```text
정의 ID: architecture:research-blueprint-archive-live-building-anchor-v27
콘텐츠 종류: Q03 연구용책장 청사진 목적지의 비방문형 건물 anchor 분리
정의·카탈로그·실행기 위치: Q03 BuildingSO, BuildingResearchArchiveAbility, ResearchBlueprintArchiveDestinationAuthority, FacilityBufferDestinationClaimRegistry, WorldItemHaulDestinationAuthority
등장 시대와 연구: 기존 Q03 등장 시대·해금·연구 순서를 그대로 유지하며 새 콘텐츠나 해금을 추가하지 않음
플레이어에게 주는 새 결정: 없음. 기존 Q03을 청사진 보관 건물로 사용하는 선택을 실제 물류 경로와 다시 연결
물리 BOM·입력·출력: Q03 목재 4와 물리 청사진 1개, 구매 비용, 연구 출력 모두 불변이며 anchor는 물리 수량을 쓰지 않음
직접 작업량과 계산 근거: 건설 48 WU·수리 12 WU·기존 운반 및 연구 WU 불변. exact 건물 조회는 작업량이나 이동 속도를 바꾸지 않음
EWU와 목표 회수 기간: 보너스·무료 운반·새 생산이 없고 admission 권위만 교정하므로 기존 Q03 EWU와 연구 목표 기간 불변
공간·전력·물·연료·정비: Q03 1×1, 저장 10, 보관 8, 정비 1과 Research room 조건을 유지. BuildingFacilityAbility를 억지로 추가하지 않음
위험·실패·회복 방식: LiveBuilding은 exact persistent building ID·drop·살아 있는 BuildingData를 요구하고, LiveFacility는 계속 FacilityData까지 요구. 누락·불일치는 fail-loud
사회·비가역 비용: 변경 없음. 금화·공간·운반자·연구자 시간의 기존 기회비용 유지
기존 대안과의 장단점: 방문형 시설은 LiveFacility, Q03 같은 비방문형 물류 건물은 LiveBuilding을 사용하며 일반 창고·동일 좌표 건물은 대체 권위가 아님
지배 전략 방지 조건: anchor 종류 간 fallback 0, 동일 좌표 추론 0, 청사진 생성·복제 0, 목적지 claim exactly-one, 저장·재계획 중복 배송 0
저장 권위와 실행 명령: Q03 ability와 persistent BuildableObject가 건물 권위, WorldItemStack·haul intent가 물리 운반 권위. LiveBuilding claim은 저장하지 않고 restore candidate에서 결정론적으로 재구축
자동 감사 ID와 전수 목록 포함 여부: FirstRun BLUEPRINT_ARCHIVE_DESTINATION_CLAIM_EXACT, ResearchTree save/rollback, coverage FirstRun transitive source와 필수 marker에 포함
검증 매트릭스와 보고서 위치: ResearchTreeDebugScenarios.RunAll, Temp/first-run-objective-report.txt, DungeonAiActionSaveLoadPlayModeVerifier, Console Warning/Error 0/0. LiveFacility 수술·정비 회귀도 함께 유지
현재 밸런스 상태: 밸런스 영향 없음 / 구조·연결 검증 대기. 수치·콘텐츠는 불변이고 fresh FirstRun·save/load·물류 회귀와 Console 0/0 전에는 연결 완료로 보고하지 않음
```

## V27 아이템·시장 가격 비대칭 환산 기록 (2026-08-17)

```text
정의 ID: balance:v27:item-market-asymmetric-price-authority
콘텐츠 종류: 전 아이템 내부 단가·자동 판매율·외부 구매·소매·계약 보상의 V27 mEWU 가격 환산
정의·카탈로그·실행기 위치: ItemDefinitionSO, ResourceItemDefinitionSO, V27EmbeddedWorkValueCalculator, GoldEconomyBalanceRules, V27BalanceAudit, V27BalanceAssetApplication, ResourceStockPolicyRuntime, FacilityShopRuntime
등장 시대와 연구: 기존 아이템·상점·계약의 시대와 연구 해금을 유지하며 신규 아이템·상점·화폐·해금을 추가하지 않음
플레이어에게 주는 새 결정: 노동 생산성 20→45 WU/성인·일에 맞춰 상승한 물리 생산 원가가 내부 단가와 외부 구매·판매·소매·계약에 일관되게 반영됨. 직접 생산, 외부 조달, 판매, 계약 수행 중 어느 한 경로만 구가격으로 남는 차익을 제거함
물리 BOM·입력·출력: 354개 레시피와 413개 해석 가능 아이템의 입력·출력 수량, 품질, 스택 크기, 무게, 저장 상태를 변경하지 않음. 가격 환산은 물리 아이템을 생성·소비하지 않으며 구매·판매는 기존 물리 수량 선차감·후정산 경계를 유지함
직접 작업량과 계산 근거: 아이템 AcquisitionCost는 이미 승인된 actual 50·effective 45 노동 기준과 각 레시피 Direct WU를 입력 Ceil로 포함함. 가격 단계에서 WU를 다시 배율하지 않으며 내부 단가는 `ceil(AcquisitionCost / 3000 mEWU-per-gold)`, 자동 판매 credit은 `floor(RecoverableValue / 3000)` 경계를 사용함
EWU와 목표 회수 기간: `1 gold=3 EWU`, 외부 구매 중앙값 `0.45 gold/EWU`, 자동 판매 중앙값 `0.20 gold/EWU`, 일반 소매 내부가치 1.20배, 프리미엄 서비스 순마진 25%를 유지함. 구매 debit은 Ceil, 판매·회수·보상 credit은 Floor하고 AcquisitionCost와 RecoverableValue를 혼용하지 않음
공간·전력·물·연료·정비: 변경 없음. 가격 조정은 시설 면적·전력·상수·하수·연료·정비·처리량·저장량을 바꾸지 않으며 해당 물리 비용은 각 아이템 AcquisitionCost의 기존 의존 그래프를 통해서만 반영됨
위험·실패·회복 방식: 미해석 아이템, 0/음수 원가, overflow, 가격 property 누락, 현재 Authority가 V23 Before와 V27 After 어느 쪽과도 불일치, 판매 금지 아이템의 양수 판매율, 소비자 가격 stale 상태는 fallback 없이 실패함. 부분 적용은 허용하지 않고 에셋 트랜잭션 실패 시 byte snapshot으로 원자 복구함
사회·비가역 비용: 금화 지출·판매 물량·계약 납품·방문객 서비스의 기존 기회비용을 유지함. 가격 상승은 정상 AI가 만든 노동 가치 상승을 통화에 반영하는 것이며 무료 금화, 부채 탕감, 재고 생성, 과거 세이브 변환을 제공하지 않음
기존 대안과의 장단점: V23 `Round(EWU/3)`은 현재 Before를 재현하지만 새 원가의 소수 debit을 아래로 반올림할 수 있음. V27 Ceil/Floor는 플레이어에게 보수적이고 미세 차익을 차단하는 대신 저가 아이템에서 1 gold 양자화 영향이 커질 수 있어 percent·rounding warning을 별도 표시함
지배 전략 방지 조건: 외부 구매→제작→자동 판매 비음수 순환 0, 제작→분해→판매 비음수 순환 0, 판매 credit>RecoverableValue 0, 소매가<내부 단가 0, 계약 보상 승인 상한 초과 0, 판매 금지 아이템 수익 0, 한 물리 수량의 중복 정산 0
저장 권위와 실행 명령: ItemDefinitionSO unitPrice와 ResourceItemDefinitionSO MarketItemFeature.saleRate가 아이템 가격 권위이고 SaleItem·stock category·guest request·regional contract의 저술 값은 파생 소비자 권위임. CSV는 감사 산출물이며 ApplyApproved만 exact approval patch를 적용함. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: V27_ITEM_UNIT_PRICE_INPUT_CEIL, V27_ITEM_SALE_OUTPUT_FLOOR, V27_MARKET_ALL_RESOLVED_ITEMS_COVERED, V27_MARKET_CONSUMER_PRICE_COHERENCE, V27_MARKET_BUY_CRAFT_SELL_NEGATIVE, V27_MARKET_ASSET_APPLY_ATOMIC, V27_SCC_ZERO_TOLERANCE를 필수 목록에 포함함
검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-balance-before-after.csv`, `v27-balance-recalibration-audit.txt`, `v27-balance-economy-256-seed.txt`, 시장 focused debug scenarios, 물리 구매·판매 PlayMode, 계약·방문객·상점 회귀, YAML second-run zero diff, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정 / 원장 후보 구현 중. 전 아이템과 모든 가격 소비자 후보가 전수 생성되고 Critical·순환·물리 보존·PlayMode·3-seed 실전 검증을 통과하기 전에는 가격 적용 완료 또는 전역 밸런스 완료로 보고하지 않음
```

## V27 생존 조리 출력 권위 후속 교정 기록 (2026-08-17)

```text
정의 ID: architecture:v27-survival-cook-output-authority
콘텐츠 종류: D03 조리손질대의 생존 조리·급수 물리 출력 정의 선택 권위 교정
정의·카탈로그·실행기 위치: SurvivalFoodRuntime, IItemDefinitionCatalog, survival_cooked_meal.asset, survival_preserved_food.asset, V3R01_깨끗한_물.asset, V27BalanceVerticalSlicePlayModeVerifier
등장 시대와 연구: 기존 D03·보존 시설·깨끗한 물의 등장 시대와 연구 조건을 그대로 유지하고 신규 해금이나 콘텐츠를 추가하지 않음
플레이어에게 주는 새 결정: 없음. 기존 Cook·DrawWater 명령이 역할 조건만 같은 임의의 사전순 아이템이 아니라 저술된 정식 생존 출력으로 연결됨
물리 BOM·입력·출력: 일반 조리 입력 Food 1→`survival:cooked_meal` 1, 보존 조리 입력 Food 1→`survival:preserved_food`의 기존 저술 수량, 급수→`resource:clean-water`의 기존 저술 수량을 유지함. 금기의 고기·사체·다른 음식이 조리 출력으로 새로 생성되는 경로는 0
직접 작업량과 계산 근거: D03 건설 468 WU·해체 117 WU 및 Cook·DrawWater의 기존 직접 작업량을 변경하지 않음. 이번 변경은 출력 definition ID 선택만 exact authority로 고정함
EWU와 목표 회수 기간: 입력·출력 수량과 작업량은 불변이며 각 정식 출력의 기존 Acquisition/Recoverable EWU를 사용함. 임의 저가 Food definition을 출력으로 선택해 가치가 흔들리는 비결정 경로를 제거함
공간·전력·물·연료·정비: D03 2×1, 기존 전력·상수·하수·연료 요구와 저장·처리량·정비 수치를 변경하지 않음
위험·실패·회복 방식: exact item ID가 누락되거나 Food/Water 역할·보존 속성과 불일치하면 대체품 fallback 없이 fail-loud함. 출력 공간 실패 시 기존 물리 출력 fallback과 typed 작업 결과를 유지함
사회·비가역 비용: 변경 없음. 조리·급수 작업자 시간, 입력 식량·연료와 시설 점유 기회비용을 그대로 지불함
기존 대안과의 장단점: 역할 predicate의 사전순 첫 항목 선택은 새 콘텐츠 추가에 따라 출력이 조용히 바뀌지만 exact ID는 콘텐츠 확장과 무관하게 결정론적임. 별도 신규 recipe나 아이템 추가 없이 기존 정식 생존 아이템을 재사용함
지배 전략 방지 조건: Food 1 투입당 일반 조리 물리 출력 1, 임의 고가·금기·사체 출력 0, 동일 명령 이중 출력 0, 출력 누락 시 임의 definition fallback 0, 저장·복원 후 definition drift 0
저장 권위와 실행 명령: ItemDefinitionSO stable ID가 출력 정의 권위이고 SurvivalFoodRuntime만 생존 Cook·DrawWater 결과를 생성함. WorldItemStack save가 생성된 물리 수량·definition ID를 저장하며 과거 세이브 변환은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: V27_SLICE_D03_FOOD_PHYSICAL_CONSERVATION은 출력 ID=`survival:cooked_meal`, 입력·출력 수량 보존과 실제 Loose stack 생성을 함께 검사하고 생존 focused 감사에 exact Water/Preserved 출력 회귀를 추가함
검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-balance-vertical-slice-full-loop-playmode.txt`, SurvivalDebugScenarios, V27 전수 원장 재생성, Unity Console Warning/Error 0/0. D03 실제 건설→조리→해체→회수→재건과 baseline restore byte-equivalence를 같은 세션에서 요구함
현재 밸런스 상태: 밸런스 영향 없음 / 실행 권위 교정 검증 진행 중. fresh full-loop PlayMode와 생존 focused 감사, 전수 원장 source digest 갱신, Console 0/0 전에는 연결 완료 또는 밸런스 완료로 보고하지 않음
```

## V27 전수 밸런스 화이트박스 원장 파이프라인 기록 (2026-08-16)

```text
정의 ID: architecture:v27-whitebox-ledger-pipeline
콘텐츠 종류: 전역 밸런스 Before/After·mEWU·의존성·승인·에셋 적용의 결정론적 감사 권위
정의·카탈로그·실행기 위치: V23EmbeddedWorkValueCalculator, V27EmbeddedWorkValueCalculator, V27BalanceLedgerCore, V27BalanceAttribution, V27BalanceSerialization, V27BalanceAudit, GameContentCatalogSO, GameDomainContentCatalogSO
등장 시대와 연구: 모든 시대·연구·콘텐츠를 감사하지만 이 파이프라인 자체는 새 해금·콘텐츠·플레이 규칙을 추가하지 않음
플레이어에게 주는 새 결정: AuditOnly 단계에서는 없음. 이후 승인된 After만 ApplyApproved로 반영하며 승인되지 않은 후보·Critical은 플레이 수치에 영향을 주지 않음
물리 BOM·입력·출력: 최초 구현은 모든 현재 ScriptableObject 숫자 권위와 354개 레시피·아이템·시설 BOM을 읽기 전용 캡처함. 기간 유지 후보는 입력 종류를 유지하고 WU 1.5~2.25배, BOM 증가는 최대 50% 범위에서만 비교하며 아직 물리 수량을 변경하지 않음
직접 작업량과 계산 근거: 정상 AI 5일 실측 44.418/48.882/53.126 WU의 평균 48.809와 유효 평균 44.971을 근거로 actual 50, effective 45 WU/성인·일을 V27 목표로 배정함. 기존 20 기준의 기간 유지 1차 후보는 45/20=2.25이며 기술 단계 actual 50/54.5/62.5/74.5/85/100, effective 45/49.05/56.25/67.05/76.5/90을 사용함
EWU와 목표 회수 기간: V23 float 결과는 Before 재현용으로 동결하고 V27은 long mEWU를 사용함. 입력·직접 WU·물류·유틸리티·손실은 구성요소별 Ceil, 산출·회수·판매 credit은 Floor하며 AcquisitionCost와 RecoverableValue를 분리함. 모든 반복 transform은 최소 -1 mEWU, SCC tolerance는 0임
공간·전력·물·연료·정비: footprint, 전력, 상수, 하수, 연료, 정비, 처리량, 저장량을 전수 serialized authority 행으로 캡처함. AuditOnly는 값을 변경하지 않고 시설별 노동 밀도와 대체 후보만 표시함
위험·실패·회복 방식: 누락 권위·중복 키·비정규 stable ID·NaN/Infinity·overflow·0 출력·미수렴·비음수 SCC margin·stale 승인·예상 밖 YAML churn은 fallback 없이 실패함. 산출물은 sibling 임시 파일 후 byte 비교·atomic replace하며 실패 시 기존 파일을 유지함
사회·비가역 비용: AuditOnly에는 없음. ApplyApproved는 사용자의 기존 dirty asset을 거부하고 원장 Before가 현재 SerializedProperty와 정확히 같을 때만 변경하며 실패 시 이번 대상 byte snapshot으로 복구함
기존 대안과의 장단점: V23 감사는 현재 콘텐츠와 float Before를 잘 재현하지만 비대칭 양자화·SCC·원인 귀속·결정론 직렬화·승인 적용 권위가 없음. V27은 더 엄격하고 리뷰 비용이 들지만 미세 순환 차익, 경고 폭포, CSV/YAML diff noise를 fail-loud하게 통제함
지배 전략 방지 조건: 입력 Ceil/산출 Floor, 배치 분할 비용 감소 0, 출력 분할 가치 증가 0, 제작→해체→재제작 및 구매→제작→판매의 비음수 순환 0, RecoverableValue>AcquisitionCost 0, 승인 없는 After 적용 0. Warning 트리 접기 epsilon은 fingerprint 동일·상위 변화 전용 최대 2 mEWU이며 SCC·Authority·Apply에는 사용하지 않음
저장 권위와 실행 명령: ScriptableObject·카탈로그·런타임 공식이 유일 원본이며 CSV/Markdown/DTO는 저장 권위가 아님. 기본 명령은 `DungeonStory/V27/Generate Audit-Only Whole-Game Ledger`; 명시적 ApplyApproved 전에는 SO를 수정하지 않음. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: V27_MEWU_ASYMMETRIC_QUANTIZATION, V27_MEWU_BATCH_PARTITION_MONOTONICITY, V27_ATTRIBUTION_COLLAPSE_EPSILON_ISOLATED, V27_SCC_ZERO_TOLERANCE, V27_CAPTURE_NORMALIZATION_AND_STABLE_SORT, V27_CSV_RFC4180_ESCAPE, V27_CSV_BYTE_DETERMINISM, V27_APPROVAL_EXACT_KEY_EXPIRY; 전수 CSV 각 행은 이 baseline ID 또는 후속 도메인별 record ID를 가짐
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-before-after.csv, v27-balance-recalibration-audit.txt, v27-balance-anomaly-graph.json, v27-balance-artifact-manifest.json, docs/generated/V27_Balance_Before_After.md, docs/game-design/v27-balance-critical-approvals.json. 단위·property·metamorphic·RFC parser·Analyzer·YAML no-op·256 economy seed·조우별 1000 combat seed·인구/기술 매트릭스·DailyRoutineWu 157181~157183·Console 0/0을 순차 요구함
현재 밸런스 상태: 밸런스 기준 배정 및 원장 기반 구현 진행 중. mEWU/접기/SCC/정규화/정렬/RFC 4180/승인키 focused 8종과 Unity compile·Console 0/0은 통과했으나 Analyzer 배포, 전수 감사, Critical 승인, SO 적용, YAML second-run zero diff, 경제·전투 시뮬레이션과 실전 재보정 전에는 밸런스 공식 검증·실전 보정·완료로 보고하지 않음
```

## V27 전수 원장 1차 감사·성능 게이트 후속 기록 (2026-08-16)

```text
정의 ID: architecture:v27-whitebox-ledger-pipeline-evidence-gate-v1
콘텐츠 종류: V27 전수 원장의 실제 Authority 캡처·Critical 귀속·승인·직렬화·에셋 적용 게이트 후속 증거
정의·카탈로그·실행기 위치: V27BalanceAudit, V27EmbeddedWorkValueCalculator, V27BalanceAttribution, V27BalanceSerialization, V27BalanceAssetApplication, DungeonStoryBalanceAnalyzer
등장 시대와 연구: 모든 시대와 연구를 감사하지만 이 후속 기록 자체는 시대·연구·해금·콘텐츠를 추가하거나 변경하지 않음
플레이어에게 주는 새 결정: 아직 없음. 승인 파일이 비어 있어 후보 After는 리뷰 정보로만 존재하고 실제 ScriptableObject 값은 한 건도 변경되지 않음
물리 BOM·입력·출력: 354개 레시피와 전 ScriptableObject의 숫자·bool·enum 권위 및 BOM을 81,792행으로 캡처함. D03은 기존 처리목재 6·철 2·석재 2를 유지한 기간 보존 후보와 동일 재료 종류 내 BOM 재분배 후보를 분리하며 아직 적용하지 않음
직접 작업량과 계산 근거: actual 50·effective 45 WU/성인·일과 2.25 기간 보존 배율을 유지함. D03의 파생 경제 작업량 208→468은 비적용 행이고 실제 patchable authored constructionWorkRequired 40→90 또는 BOM 재분배 40→60을 별도 행으로 둠
EWU와 목표 회수 기간: long mEWU, 입력 Ceil·산출 Floor, SCC tolerance 0을 유지함. 현재 352 SCC의 최저 margin은 -2,311,986 mEWU이고 integrity failure는 0이나 최종 ROI·회수기간은 승인·적용·실전 검증 전 미확정
공간·전력·물·연료·정비: 현재 Authority 값을 Before=After 명시 행으로 전수 보존하고 승인된 도메인 패치 전에는 footprint·전력·용수·폐기물·연료·정비·처리량을 바꾸지 않음
위험·실패·회복 방식: 4개 상위 Critical을 승인 키와 함께 fail-loud하고 상속·반올림 전용 6개 파생 경고만 접음. 승인 키는 exact After·dependency fingerprint·source digest·reason·baseline ID가 바뀌면 만료하며 wildcard를 허용하지 않음
사회·비가역 비용: 현재 적용 0건이라 없음. 향후 ApplyApproved는 기존 dirty asset 거부, Before 재검증, changed-only Dirty, 안정 정렬 ForceReserialize, identity 보존, 예외 시 대상 byte rollback을 요구함
기존 대안과의 장단점: 사람이 CSV 수만 행을 직접 읽는 방식보다 root 4개와 collapsed 6개로 원인을 격리하고 no-op Git diff를 보장하지만, 승인과 도메인별 실제 플레이 검증 비용은 의도적으로 남음
지배 전략 방지 조건: SCC margin >=0 0건, duplicate ledger key 0건, missing baseline ID 0건, 승인 없는 SO 변경 0건, 접기 epsilon의 SCC·Authority·Apply 사용 0건. 네 상위 root가 해결되기 전 하위 접힌 행을 독립 승인하지 않음
저장 권위와 실행 명령: ScriptableObject·카탈로그·런타임 공식이 권위이며 생성 CSV·Markdown·JSON은 감사 산출물임. AuditOnly·RegenerateArtifacts는 무변경, ApplyApproved만 승인 파일을 소비함. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: DSB001~DSB008 analyzer source/DLL hash gate, V27 stable sort p95 2ms/0B, RFC 4180 byte·parser·긴 필드·Unicode 회귀, 81,792행 unique key·baseline coverage, 352 SCC 감사가 포함됨
검증 매트릭스와 보고서 위치: v27-balance-before-after.csv, V27_Balance_Before_After.md, v27-balance-recalibration-audit.txt, v27-balance-anomaly-graph.json, v27-balance-artifact-manifest.json, v27-balance-ledger-contracts.txt. 다섯 결정론 산출물은 연속 재생성에서 length·SHA-256·mtime 모두 무변경
현재 밸런스 상태: 밸런스 기준 배정·전수 AuditOnly 구현 완료 / 공식 검증 보류. unresolved root/local Critical 4, approved 0, asset patch 0이며 CSV escape 10,000필드·약 1MiB p95가 요구 2ms 대비 현재 4.271ms로 실패함. 비어 있지 않은 승인 적용·YAML rollback, 전수 경제 256 seed, 전투 조우별 1,000 seed, 인구·기술 매트릭스와 5일 실전 재보정 전에는 밸런스 완료로 보고하지 않음
```

## V27 통나무·조리 수직 슬라이스 노동 권위 배정 기록 (2026-08-16)

```text
정의 ID: balance:v27:logging-cooking-dismantle-vertical-slice
콘텐츠 종류: 통나무→제재목→처리목재→D03 조리손질대→곡물죽→해체→재건의 첫 V27 수직 슬라이스
정의·카탈로그·실행기 위치: source_logging.asset, source_quarry.asset, V3R01_깨끗한_물.asset, recipe_sawmill_lumber.asset, recipe_treated_lumber.asset, crop_twilight_grain.asset, recipe_grain_porridge.asset, D03_조리손질대.asset, V27BalanceWorkCalculator, V23MaterialSalvageCalculator, ProductionBillRuntime, WorkAmountSystem
등장 시대와 연구: 각 기존 원천 채집·제재·목재 처리·황혼곡물·곡물죽·D03의 시대와 연구 해금을 그대로 유지하며 신규 해금·무료 기술·콘텐츠를 추가하지 않음
플레이어에게 주는 새 결정: 기존 생산·건설·해체 선택은 유지하되 정상 AI의 유효 생산성 20→45 WU/성인·일에 맞춰 같은 달력 기간을 지불함. 재료 종류·공정 순서·대체품은 바뀌지 않음
물리 BOM·입력·출력: 벌목·채석·깨끗한 물의 무입력 원천 출력, 제재목 `resource:log=2`, 처리목재 `material:lumber=2|resource:dark-resin=1`, 황혼곡물 `seed-lot:twilight-grain=1(수확 시 최소 2 반환)|resource:clean-water=1`, 곡물죽 `resource:twilight-grain=2`, D03 `material:iron-ingot=2|material:stone-block=2|material:treated-lumber=6`을 Before와 After에서 동일하게 유지함. D03 해체 회수는 숙련 100 기준 철괴 1·석재 블록 1·처리목재 5로 불변
직접 작업량과 계산 근거: 런타임 직접 WU는 벌목 18→40.5, 채석 32→72, 깨끗한 물 10→22.5, 제재목 22→49.5, 처리목재 22→49.5, 황혼곡물 파종 3→7·수확 6→14, 곡물죽 28→63, D03 건설 208→468, D03 해체 52→117임. 소수 WU를 지원하는 런타임 계산은 정확히 ×2.25하고, 정수 ScriptableObject 표시는 각각 41·72·23·50·50·7·14·63·90처럼 Ceil한 authored 후보를 별도 기록함. 기간 증명은 예를 들어 D03 `208/20=10.4` 성인·일과 `468/45=10.4` 성인·일로 동일함
EWU와 목표 회수 기간: 통나무 Acquisition 4.817→10.838 EWU, 처리목재 31.792→71.533 EWU, 황혼곡물 재배 입력 5.098→11.469 EWU/개, 곡물죽 28.032→53.694 EWU, D03 BOM 337.216391→758.760 EWU임. D03 건설 노동밀도는 208/337.216391=0.6168146198에서 468/758.760=0.6167958248로 사실상 유지됨. 해체·재건 순환 margin은 -365.029→-821.314 EWU로 더 손실적이며 SCC에는 표시용 epsilon을 적용하지 않음
공간·전력·물·연료·정비: D03 2×1 면적, 전력·자동화·컨베이어·상수·하수 연결, 공정당 깨끗한 물 0.25·오수 0.25, 저장·처리량·정비 수치는 변경하지 않음. 황혼곡물 재배 면적·성장시간·일일 용수·수확량도 변경하지 않음
위험·실패·회복 방식: 자원 부족·물류 no-path·예약 취소·생산 중단·작물 실패·시설 파괴의 기존 typed 실패를 유지함. 입력 Ceil·산출 Floor와 exact in-transit commitment를 사용하며 취소·저장·복원으로 자원이나 WU를 복제하지 않음. 과거 세이브 마이그레이션은 범위 밖이고 신규 주문·신규 공정이 V27 권위를 사용함
사회·비가역 비용: 동일 달력 기간과 재료 손실을 유지하므로 작업자 기회비용·시설 점유·배관 장애·작물 재배지 점유·해체 손실을 낮추지 않음. D03 해체 후 재건은 최소 821.314 EWU를 소실해 반복 이득이 없음
기존 대안과의 장단점: `WU×2.25, BOM 동일`은 노동밀도와 기간을 보존함. 검토한 `WU×1.5 + BOM 증가`는 D03에서 노동밀도를 0.6168146→0.4111972로 더 붕괴시키며 재료 추가는 분모를 키워 악화하므로 기각함. BOM 종류·수량을 유지한 ×2.25 후보를 최소 변화 승인안으로 선택함
지배 전략 방지 조건: 배치 분할로 입력 비용 감소 0, 출력 분할로 가치 증가 0, 원천→중간재→음식·시설 우회 무료 산출 0, D03 철거→재건 비음수 순환 0, 같은 시대 대안 대비 BOM·WU·시간을 동시에 모두 이기는 경로 0, 승인되지 않은 ScriptableObject 변경 0
저장 권위와 실행 명령: ProductionRecipeSO·BuildingSO·CropDefinitionSO가 BOM·공정·정수 authored 표시를 소유하고 V27BalanceWorkCalculator가 신규 생산·건설의 기간 보존 runtime WU를 소유함. 해체는 같은 V27 건설 WU를 V23MaterialSalvageCalculator의 불변 0.20~0.35 비율에 한 번 넣으므로 별도 재배율 없이 52→117이 됨. WorkOrder save는 생성 시 확정 requiredWork를 저장하고 ProductionBill은 현재 계산 권위를 조회함. 과거 저장 변환은 구현하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_VERTICAL_SLICE_RUNTIME_WORK_SCALE, V27_VERTICAL_SLICE_AUTHORITY_ALIGNMENT, V27_VERTICAL_SLICE_DISMANTLE_REBUILD_NEGATIVE, V27_MEWU_ASYMMETRIC_QUANTIZATION, V27_SCC_ZERO_TOLERANCE, V27_APPROVAL_EXACT_KEY_EXPIRY를 필수로 하며 해당 CSV 행의 balanceBaselineRecordId는 이 정의 ID를 사용함
검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-balance-before-after.csv`, `v27-balance-recalibration-audit.txt`, `v27-balance-ledger-contracts.txt`, focused Production/WorkAmount PlayMode, D03 실제 건설·조리·해체·회수·재건, YAML second-run zero diff, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정 / 구현·공식·실전 검증 진행 중. 수치와 기각 대안은 확정했지만 runtime calculator 등록, exact 승인·ApplyApproved, D03 full loop PlayMode, 256-seed 경제 감사와 5일 실전 재측정을 모두 통과하기 전에는 밸런스 공식 검증·실전 보정·완료로 보고하지 않음
```
