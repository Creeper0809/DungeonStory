# DungeonStory 전역 밸런스 기준서

## 신선 개밥 결과물 분리 기록 (2026-09-04)

```text
정의 ID: balance:v28:fresh-dog-food-output-identity
콘텐츠 종류: 기존 recipe:dog-food-fresh의 결과물을 일반 개밥과 구분하는 사료 아이템 정의
등장 시대와 연구: 기존 research:husbandry:feed와 feedbench 생산 시점을 유지한다
플레이어에게 주는 결정: 동물성 부패물을 쓰는 일반 개밥과 생고기를 쓰는 신선 개밥을 재고에서 구분한다
물리 BOM·입력·출력: 입력 resource:meat×1 + resource:twilight-grain×1, 출력 feed:dog-food-fresh×2로 결과물 ID만 바로잡는다
직접 작업량과 계산 근거: 기존 18 WU를 유지한다
EWU와 목표 회수 기간: 입력·출력 수량, 무게 0.525kg, 가격 6, 영양 14를 일반 개밥과 동일하게 유지해 기존 생산 가치 총량을 바꾸지 않는다
공간·전력·물·연료·정비: 시설, 작업대, 적재량 75, 기반 시설 비용을 유지한다. 서로 다른 사료는 별도 재고 묶음을 사용한다
위험·실패·회복 방식: 새 안정 ID가 카탈로그, 생산 출력, 인게임 설명, 포획 동물 급이 선택, 경제 예측과 위키 관계에 모두 연결되지 않으면 콘텐츠 감사를 실패시킨다
사회·비가역 비용: 변경 없음
기존 대안과의 장단점: 두 조합식의 결과 이름과 실제 재고가 일치한다. 사료 종류가 하나 늘어 창고 정리와 배급 선택이 늘어난다
지배 전략 방지 조건: 영양, 가격, 출력량과 작업량을 유지해 구매→판매·제작→해체 차익을 추가하지 않는다
저장 권위와 실행 명령: ResourceItemDefinitionSO와 ProductionRecipeSO가 불변 정의를 소유하고 기존 물리 재고 저장 형식이 새 안정 ID를 저장한다. 급이 소비와 수요 예측은 개별 item ID 하드코딩 대신 feedEligible·식성 태그와 실제 재고·예정 생산량을 사용한다
자동 감사 ID와 전수 목록 포함 여부: 위키 모델 검증, 콘텐츠 DB 재생성, 새 아이템·조합식 관계 검사, WildlifeFeedSelectionDebugScenarios의 식성·우선순위·재고 기반 선택 회귀에 포함한다
현재 밸런스 상태: 구현·집중 검증 완료. 기존 수치는 유지했고 카탈로그·생산·물리 질량·도달성·콘텐츠 DB·위키·데이터 기반 급이 선택이 통과했다. 전수 current-source 시장·공간·H 인증 전에는 전체 밸런스 완료로 표시하지 않는다
```

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

## V27 서비스 연속성 N+1 권위 기록 (2026-08-17)

```text
정의 ID: balance:v27:service-continuity-nplusone
콘텐츠 종류: 음식·식수·수면·위생·배설 주 서비스와 실제 primitive 대체 경로의 1게임일 단일 장애 연속성 계약
정의·카탈로그·실행기 위치: ISurvivalContinuityCatalogQuery, SurvivalContinuityCatalogQuery, CharacterPrimitiveSurvivalRunner, CharacterSafeDrinkPlanner, V27PopulationCapacityDebugScenarios
등장 시대와 연구: Tier 0부터 적용하며 연구 시설이 없는 초기에도 기존 physical meal·clean-water·floor-rest·latrine 경로만 사용함
플레이어에게 주는 새 결정: 같은 시설을 두 개 강제하지 않고, 기분·위생·시간 손해를 감수하는 실제 저자본 primitive 경로를 N+1로 선택할 수 있음
물리 BOM·입력·출력: field meal은 Meal 1, bucket wash는 clean-water 1을 정확히 소비하며 floor rest·latrine은 아이템을 생성하지 않음. 식수 fallback은 기존 실제 수원 또는 clean-water 권위만 사용함
직접 작업량과 계산 근거: field meal 4초, floor rest 60초, bucket wash 6초, latrine 6초의 기존 실행 시간을 기회비용 WU로 환산하며 숨은 무료 노동을 추가하지 않음
EWU와 목표 회수 기간: primitive는 물리 입력의 acquisition EWU와 행동 시간 비용을 모두 부담하고 신규 회수 가치를 만들지 않음. 입력 Ceil·산출 Floor·SCC tolerance 0 유지
공간·전력·물·연료·정비: 대체 경로는 실패한 주 시설·전용 버퍼·유일 접근칸에 의존하지 않아야 하며 별도 전력·가상 저장을 생성하지 않음
위험·실패·회복 방식: 24시간 장애 동안 생존 수요 100%, 사망·기절·breakdown·복제 0, 복구 후 primitive 지속 우선 0을 요구하고 불가능하면 typed failure
사회·비가역 비용: floor rest mood -3·hygiene -4, latrine mood -2·hygiene -8·Waste 8·Stain 2를 그대로 부담하여 primitive 지배 전략을 막음
기존 대안과의 장단점: 동일 시설 2개보다 초기 BOM이 적지만 서비스 질·시간·청소 비용이 나쁨. 존재가 확인되지 않은 모닥불이나 생식 경로는 인정하지 않음
지배 전략 방지 조건: 정상 시설 사용 가능 시 primitive start 목표 0·상한 5%, 다단 fallback 0, 입력 없는 식사·식수 0, 취소 후 회복·소비 0
저장 권위와 실행 명령: 욕구·아이템·오염은 기존 runtime/save 권위이며 continuity catalog는 파생 조회라 저장하지 않음. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: V27_SERVICE_NPLUSONE_EXACT, V27_PRIMITIVE_COSTS_EXACT, V27_PRIMITIVE_RECOVERY_ZERO_DOMINANCE를 음식·식수·수면·위생·배설 5서비스 전수 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-service-continuity.csv, primitive-survival-focused-report.txt, v27-balance-six-adult-food-water-loop.txt, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정. 실제 1일 장애 PlayMode와 복구·물리 보존·노동 비율이 통과하기 전 시뮬레이션 검증으로 승격하지 않음
```

## V27 primitive fallback 초기 자본 완화 기록 (2026-08-17)

```text
정의 ID: balance:v27:primitive-fallback-capital-relief
콘텐츠 종류: 6인 Tier 0의 조리·식수 중복 시설 강제를 primitive 경로로 대체하는 초기 자본 보호 계약
정의·카탈로그·실행기 위치: PopulationStagePortfolio, ServiceContinuityRequirement, SurvivalContinuityCatalogQuery, V27PopulationCapacityDebugScenarios
등장 시대와 연구: 시작 단계 전용이며 연구가 해금되어 처리량 또는 장애 이용률 상한을 넘으면 영구 중복 시설 후보로 전환함
플레이어에게 주는 새 결정: 성장 시설 BOM을 고갈시키지 않고 1일 비상 경로를 선택하되 낮은 편의와 기분·오염 비용을 수용함
물리 BOM·입력·출력: 중복 시설을 짓지 않은 뒤에도 음식·식수 7일분, 다음 연구 필수 시설 BOM 100%, 주요 수리 재료 10%를 물리 재고로 남겨야 함
직접 작업량과 계산 근거: 생존·청소·수리 25~35%, 성장 35~50%, 비상 예비 10% 이상, 총 반복 노동 90% 이하를 동시에 요구함
EWU와 목표 회수 기간: redundancy BOM+WU가 단계 가용 자본의 15% 초과면 Warning, 25% 초과면 Critical이며 primitive는 물리 입력 비용을 면제하지 않음
공간·전력·물·연료·정비: fallback은 별도 영구 footprint를 요구하지 않지만 안전한 접근·오염 containment·실물 저장 공간을 요구함
위험·실패·회복 방식: fallback 24시간 미충족, 성장 노동 35% 미달, 저장 90% 초과, 복구 후 fallback 고착이면 중복 시설을 승인 후보로 올림
사회·비가역 비용: 초기 연구·건설 지연과 기분 저하를 모두 기록하고 주민 고통을 무료 자본으로 환산하지 않음
기존 대안과의 장단점: 두 번째 조리대·펌프보다 싸지만 처리량과 생활 질이 낮고 수동 복구 부담이 큼
지배 전략 방지 조건: primitive가 정상 시설보다 빠르거나 싸거나 기분상 유리한 경우 0, 비상 비축 없이 N+1 통과 0
저장 권위와 실행 명령: 시설·재고는 기존 권위이며 자본 비율은 V27 audit 파생값으로 저장하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_REDUNDANCY_CAPITAL_RATIO, V27_TIER0_NO_UNNECESSARY_DUPLICATE, V27_FALLBACK_PHYSICAL_RESERVE_EXACT
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-stage-portfolios.csv, v27-balance-service-continuity.csv, v27-balance-layout-256-seed.txt
현재 밸런스 상태: 밸런스 기준 배정. Tier 0 production-live 24시간 장애와 성장 BOM 잔존 검증 전 완료 아님
```

## V27 공유 접근칸 공간 합집합 기록 (2026-08-17)

```text
정의 ID: balance:v27:shared-access-spatial-union
콘텐츠 종류: 시설 footprint·작업 접근칸·대기칸·공용 통로를 셀 역할 합집합으로 계산하는 공간 권위
정의·카탈로그·실행기 위치: BuildingWorkAccessRules, SpatialCellRole, DeterministicDungeonSpaceCapacityQuery, BuildingPlacementValidator
등장 시대와 연구: 모든 단계에 적용하며 연구 Tier가 오를 때 기존 배치를 철거하지 않는 오른쪽 확장과 함께 사용함
플레이어에게 주는 새 결정: 실제 충돌이 없는 접근칸과 통로를 공유해 조밀 배치할 수 있지만 유일 접근·egress는 공유할 수 없음
물리 BOM·입력·출력: 시설 BOM과 생산량은 변경하지 않으며 배치 셀만 권위 있게 합집합 계산함
직접 작업량과 계산 근거: shared cell 방문횟수×점유초/180초로 정상 70%, 단일 장애 90% 이용률 상한을 적용함
EWU와 목표 회수 기간: 공유 셀은 공간 비용만 줄이며 WU·BOM·EWU 비용을 삭제하지 않음
공간·전력·물·연료·정비: effectiveUsedCells는 exclusive∪access∪corridor∪storage∪overflow∪fixed이고 usable 대비 headroom 30% 이상을 요구함
위험·실패·회복 방식: 본체 overlap, 계단 착지, emergency egress, 동시에 필요한 두 시설의 유일 접근, 반복 StepAside·replan 배치를 실패시킴
사회·비가역 비용: 좁은 배치의 응급 접근 지연과 작업 대기를 Wait WU로 부담함
기존 대안과의 장단점: 단순 면적 합산보다 false-negative가 적지만 혼잡과 유일 접근 검증이 추가됨
지배 전략 방지 조건: utility overlay로 본체 면적 삭제 0, shared-cell 이용률 초과 0, 접근 불가능 시설 배치 0
저장 권위와 실행 명령: Grid·Building placement가 권위이고 Solver 결과는 저장하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_SHARED_ACCESS_UNION_EXACT, V27_SHARED_CELL_70_90, V27_LAYOUT_ORACLE_NO_FALSE_NEGATIVE
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-spatial-capacity.csv, v27-balance-shared-cell-congestion.txt, v27-balance-layout-256-seed.txt
현재 밸런스 상태: 밸런스 공식 검증. 256-seed 정적 PASS이며 실제 다중 시설 PlayMode 혼잡 증거 전 시뮬레이션 완료 아님
```

## V27 Floor Clutter 런타임 용량 기록 (2026-08-17)

```text
정의 ID: balance:v27:floor-clutter-runtime-capacity
콘텐츠 종류: StorageBuffer 밖 persistent Loose item과 접근·egress 즉시 실패를 관찰하는 비변이 진단 권위
정의·카탈로그·실행기 위치: IFloorClutterDiagnosticsQuery, FloorClutterDiagnosticsQuery, PhysicalStockQuery, WorldItemStackRuntime
등장 시대와 연구: 모든 단계·모든 물리 아이템에 적용하며 이동 비용을 새로 발명하지 않고 실제 예약·운반·서비스 지연을 측정함
플레이어에게 주는 새 결정: 창고를 과포화하면 생산 대기 또는 containment를 확보해야 하며 통로 바닥을 무료 창고로 쓸 수 없음
물리 BOM·입력·출력: Loose 수량을 삭제·순간이동·가상 보관하지 않고 원래 item/quantity/destination 보존성을 요구함
직접 작업량과 계산 근거: grace=min(0.25일,max(15초,clean p95 haul×2)); 이후 clutter cell-seconds와 Wait WU를 기록함
EWU와 목표 회수 기간: clutter 자체가 이동 페널티 EWU를 생성하지 않으며 실제 지연 노동만 귀속함
공간·전력·물·연료·정비: 정상 storage 70%, 장애 storage+containment 90%, runtime headroom 30%, containment 밖 clutter 0
위험·실패·회복 방식: egress·계단·수술/구조/침상·주/fallback 유일 접근의 Loose는 grace 없이 실패함
사회·비가역 비용: 운반자 장애와 서비스 지연을 성장 노동에서 차감하며 소실 재고는 허용하지 않음
기존 대안과의 장단점: 정적 buffer 계산보다 실제 병목을 잡지만 숨은 path-cost 가정은 하지 않음
지배 전략 방지 조건: 통로 fallback drop 0, capacity 초과 출력 삭제 0, orphan Loose 0
저장 권위와 실행 명령: item repository와 physical stack save가 권위이고 clutter 평가는 파생 진단이라 저장하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_FLOOR_CLUTTER_OUTSIDE_CONTAINMENT_ZERO, V27_ACCESS_EGRESS_CLUTTER_ZERO, V27_CLUTTER_QUANTITY_CONSERVED
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-floor-clutter.csv, v27-balance-paired-run-rng.csv, PhysicalItemLogistics PlayMode
현재 밸런스 상태: 밸런스 기준 배정. production burst·창고 90%·운반자 Downed PlayMode 전 완료 아님
```

## V27 저장·overflow containment 기록 (2026-08-17)

```text
정의 ID: balance:v27:storage-overflow-containment
콘텐츠 종류: 7일 생존 비축·정상 cycle·최대 batch·운반 복구 유입을 합산한 물리 저장/overflow 용량 계약
정의·카탈로그·실행기 위치: StockSpaceRequirement, OverflowRequirement, PopulationStagePortfolio, DeterministicDungeonSpaceCapacityQuery
등장 시대와 연구: 인구 1/3/6/12/18/24 및 Tier 0~3 전 단계
플레이어에게 주는 새 결정: 창고와 안전한 overflow를 확장하거나 생산을 WaitingForOutputSpace로 멈춰야 함
물리 BOM·입력·출력: requiredStorage=7일 비축+cycle+max batch+p95 복구 유입, overflow=max(수확·채굴·시설 batch·carry 취소·hauler 장애 순출력)
직접 작업량과 계산 근거: 저장 포화에서 발생하는 haul/정리 노동은 물류 12~20% 밴드와 Wait WU에 포함함
EWU와 목표 회수 기간: 저장 셀·시설 BOM·정리 노동을 비용으로 유지하며 가상 용량 크레딧 0
공간·전력·물·연료·정비: overflow는 corridor·access·egress와 공유하지 않고 30% headroom 계산에 포함함
위험·실패·회복 방식: 출력 공간 없으면 WaitingForOutputSpace, 불가피한 burst는 containment, 둘 다 차면 typed capacity failure
사회·비가역 비용: 재고 부패·작업 중지·복구 시간을 생산 손실로 기록함
기존 대안과의 장단점: 과잉 창고는 자본·공간 비용이 크고 부족 창고는 장애 시 생산 중지 위험이 큼
지배 전략 방지 조건: 삭제·teleport·통로 저장·headroom 이중사용 0
저장 권위와 실행 명령: physical item stacks와 warehouse authority가 유일 쓰기 권위, capacity assessment는 읽기 전용
자동 감사 ID와 전수 목록 포함 여부: V27_STORAGE_70_90, V27_OVERFLOW_EXCLUSIVE, V27_OUTPUT_SPACE_TYPED_FAILURE
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-stage-portfolios.csv, v27-balance-spatial-capacity.csv, v27-balance-floor-clutter.csv
현재 밸런스 상태: 밸런스 기준 배정. 전 인구 production-live capacity 검증 전 완료 아님
```

## V27 counterfactual RNG 격리 기록 (2026-08-17)

```text
정의 ID: balance:v27:counterfactual-rng-isolation
콘텐츠 종류: character decision/movement actor별 stream과 key-addressed 외생 사건의 결정론 권위
정의·카탈로그·실행기 위치: RandomStreamScopeIds, RandomStreamProvider, IRandomStreamDiagnosticsQuery, CounterfactualRandomKey, AIBrain, AbilityMove
등장 시대와 연구: 모든 런타임·모든 인구·기술 단계에 적용, 콘텐츠 수치 변화 없음
플레이어에게 주는 새 결정: 없음. 동일 seed 진단에서 무관 actor와 외생 사건의 나비효과를 제거함
물리 BOM·입력·출력: 변화 없음
직접 작업량과 계산 근거: decision=`character-ai:{persistentId}`, movement=`character-movement:{persistentId}`로 분리하고 draw count를 진단함
EWU와 목표 회수 기간: 변화 없음. RNG 격리는 경제 결과 비교의 인과 정확성만 보장함
공간·전력·물·연료·정비: 변화 없음
위험·실패·회복 방식: duplicate persistent ID, global character stream, duplicate event key, save/restore sequence drift를 fail-loud 처리함
사회·비가역 비용: 무관 actor 결과가 장애 actor의 추가 tick에 의해 변하지 않도록 보장함
기존 대안과의 장단점: root seed만 공유하는 paired run보다 정확하지만 stream manifest·save 진단이 추가됨
지배 전략 방지 조건: 프레임·instance ID·이름 기반 seed 0, UnityEngine.Random 직접 사용 0, actor 간 cross-talk 0
저장 권위와 실행 명령: 기존 random stream save가 state 권위, draw count와 파생 handle은 저장하지 않음. 과거 global-stream save 마이그레이션 제외
자동 감사 ID와 전수 목록 포함 여부: RNG_ACTOR_DECISION_ISOLATED, RNG_DECISION_MOVEMENT_ISOLATED, RNG_SAVE_RESTORE_EXACT, RNG_EVENT_KEY_ORDER_INDEPENDENT
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-random-stream-manifest.txt, v27-balance-paired-run-rng.csv, RandomStreamIsolationDebugScenarios
현재 밸런스 상태: 밸런스 공식 검증. focused 격리 회귀 PASS, 전체 4-arm PlayMode 전 시뮬레이션 완료 아님
```

## V27 paired-run window 귀속 기록 (2026-08-17)

```text
정의 ID: balance:v27:paired-run-window-attribution
콘텐츠 종류: cleanRepeatA/B·faultControl·clutterStress 4-arm의 6시간 window Wait WU 인과 귀속
정의·카탈로그·실행기 위치: PairedRunWindowResult, PairedRunAttributionEvaluator, CounterfactualRandomKey
등장 시대와 연구: 모든 인구 단계의 clutter stress 감사
플레이어에게 주는 새 결정: 없음. 내부 밸런스 진단의 false-negative를 억제함
물리 BOM·입력·출력: C/D의 동일 장애·burst 입력과 exact physical 결과를 요구함
직접 작업량과 계산 근거: warm-up 0.5일, intervention 1일, recovery 0.5일; pureFault=C-mean(A,B), clutter=D-C
EWU와 목표 회수 기간: Wait WU는 window별 mWU로만 귀속하고 frame count를 비용으로 사용하지 않음
공간·전력·물·연료·정비: D만 실제 physical stock command로 storage 90%와 clutter 압력을 구성함
위험·실패·회복 방식: A/B hash·RNG·mWU 불일치면 PAIRED_RUN_NONDETERMINISTIC_BASELINE, 64 seed도 불명확하면 POWER_INSUFFICIENT
사회·비가역 비용: Downed 자체 영향과 clutter 영향을 분리해 과잉 공간·노동 보정을 막음
기존 대안과의 장단점: 1:1 frame 비교보다 강건하지만 네 배 실행 비용이 듦
지배 전략 방지 조건: 평균만 보고 극단 seed 숨김 0, causal cone 밖 RNG divergence 0, 외생 event divergence 0
저장 권위와 실행 명령: canonical checkpoint는 기존 save 권위에서 복원하며 arm 차이는 production command만 사용
자동 감사 ID와 전수 목록 포함 여부: V27_PAIRED_AB_EXACT, V27_PAIRED_EVENTS_EXACT, V27_CLUTTER_DELTA_MEDIAN_P95, RNG_CROSS_TALK_ZERO
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-paired-run-rng.csv; 32 seed 기본·경계 시 64 seed
현재 밸런스 상태: 밸런스 공식 검증. synthetic 32-seed 귀속 PASS, production checkpoint 4-arm 실행 전 완료 아님
```

## V27 인구 단계 공간·노동 용량 기록 (2026-08-17)

```text
정의 ID: balance:v27:population-stage-capacity
콘텐츠 종류: 인구 1/3/6/12/18/24의 생존·성장·비상·시설·저장·공간 폐쇄 루프
정의·카탈로그·실행기 위치: PopulationStagePortfolio, IDungeonSpaceCapacityQuery, V27PopulationCapacityDebugScenarios
등장 시대와 연구: 인구 1/3/6/12/18/24의 수용력 요구를 비교하되 특정 연구나 공간 해금과 연결하지 않음
플레이어에게 주는 새 결정: 없음. 이 기록은 현재 정상 게임플레이가 제공하는 공간 안에 시설·비축·통로·여유가 들어가는지 진단함
물리 BOM·입력·출력: 각 단계 7일 음식·식수, 정상 cycle, max batch, overflow, 의료·수리 재고를 실물로 요구함
직접 작업량과 계산 근거: actual 50·effective45, 반복 90% 이하, 생존 25~35%, 물류12~20%, 성장35~50%, 비상10%
EWU와 목표 회수 기간: 단계별 물리 자본과 반복 노동을 V27 원장에 별도 metric으로 기록함
공간·전력·물·연료·정비: usable 셀 기준 headroom30%, normal/fault utilization70/90%, storage70/90%
위험·실패·회복 방식: 256 순서 seed 중 243 이상, exact oracle false-negative 0, 기존 시설 철거 0
사회·비가역 비용: 의료·경비·침입 인력 이탈과 복구 노동을 성장 여력에서 차감함
기존 대안과의 장단점: 인구만으로 시설 수를 배수하는 방식보다 정확하지만 단계별 실제 포트폴리오 authoring이 필요함
지배 전략 방지 조건: 개발자 E를 정식 진행으로 계상 0, 가정한 무료 확장 0, headroom 숨은 소비 0, growth/emergency 이중 계상 0
저장 권위와 실행 명령: 시설·재고·연구는 기존 권위, portfolio/assessment는 파생 원장
자동 감사 ID와 전수 목록 포함 여부: V27_STAGE_1_3_6_12_18_24, V27_LAYOUT_243_OF_256, V27_GROWTH_35, V27_EMERGENCY_10
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-stage-portfolios.csv, v27-balance-layout-256-seed.txt, v27-balance-spatial-capacity.csv
현재 밸런스 상태: 밸런스 기준 배정. 6인 정적 portfolio PASS, 나머지 실제 portfolio와 PlayMode 전 완료 아님
```

## V27 6인 음식·물 폐쇄 루프와 반복 작업 교정 기록 (2026-08-17)

```text
정의 ID: balance:v27:six-adult-food-water-closed-loop
콘텐츠 종류: 6인 황혼곡→곡죽·깨끗한 물의 생산·소비·비축·노동·저장 폐쇄 루프 및 recurring WU 교정
정의·카탈로그·실행기 위치: ResourceEconomyContentCatalog, SurvivalBalanceSettingsSO, V27BalanceWorkCalculator, V27SixAdultSurvivalLoopAudit, ProductionBillRuntime, CropPlotRuntime
등장 시대와 연구: Tier 0 음식 fallback을 우선 검증하고 grain porridge는 agriculture:field+cuisine:crops, clean water는 agriculture:irrigation 해금 상태를 명시함
플레이어에게 주는 새 결정: 프로젝트 WU 증가와 반복 처리량을 분리하고, 7일 원곡/물 비축과 조리 1일 정지에도 버티는 12개 즉시 식사를 준비함
물리 BOM·입력·출력: 6인 수요 food300 nutrition/일, gross375, net330, 7일2100. porridge35 nutrition으로 gross10.715개/일·net9.429이며 6개 batch 단위 즉시12개. clean-water는 thirst60/인·일, 65 회복/단위의 실제 수요5.539단위/일과 gross125% 6.924단위/일을 구분함. 작물1.05와 조리0.447을 합친 총 깨끗한 물 생산 목표는 8.421단위/일, 7일 비축은59단위. 황혼곡은 종자1/plot/cycle와 물0.35/plot/일을 별도 계상함
직접 작업량과 계산 근거: Before current는 crop sow7+harvest14, porridge63/2개, water23/4개로 gross 생존 생산만 약419.3WU/일이라 270WU를 초과함. After는 crop3+6, porridge28의 batch를 grain6→meal6, water10의 batch를 clean-water8로 하여 crop18+cook50.008+water10.53=78.538WU/일(29.09%)로 맞춤
EWU와 목표 회수 기간: 반복 작업은 ×2.25를 적용하지 않고 V23 공정 WU로 복귀하며 batch는 input/output 1:1을 유지함. 입력 Ceil·산출 Floor·SCC 최소 -1mEWU를 재계산함
공간·전력·물·연료·정비: twilight grain 3 plot 기준 gross12 grain/일, 곡죽 12개/2 batch capacity, 7일 reserve는 부패 2일인 완성식 대신 원곡60+즉시 meal12+water59 물리 stack으로 저장하고 30% headroom을 유지함
위험·실패·회복 방식: 종자·crop water·0.25 clean water/cook cycle·0.1 wastewater·부패·haul·청소를 포함하고 주 조리/식수 경로 1일 장애에는 primitive N+1을 사용함
사회·비가역 비용: 식사 mood+2, primitive 기분/위생 손실과 운반·전환 시간을 유효 노동에서 차감함
기존 대안과의 장단점: WU63 유지안은 물량 전부터 노동 불가능, 단순 WU6 복귀안은 공정 복잡도를 과소평가, 28WU 6개 batch는 공정 비용과 실제 throughput을 동시에 보존함
지배 전략 방지 조건: grain:meal 비율1:1, batch 분할 비용 감소0, 7일 prepared-food 부패 무시0, seed/water/haul 누락0, primitive 정상 지배0
저장 권위와 실행 명령: recipe/crop SO와 physical item runtime이 권위, loop audit는 파생값. ApplyApproved 전 exact Before를 재확인하고 changed asset만 Dirty/Reserialize
자동 감사 ID와 전수 목록 포함 여부: V27_SIX_ADULT_FOOD_GROSS_125, V27_SIX_ADULT_FOOD_NET_110, V27_SIX_ADULT_WATER_GROSS_125, V27_SIX_ADULT_RECURRING_WU_35, V27_SEVEN_DAY_PHYSICAL_RESERVE
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-six-adult-food-water-loop.txt, v27-balance-service-continuity.csv, v27-balance-stage-portfolios.csv, DailyRoutineWu 3 seeds
현재 밸런스 상태: 밸런스 기준 배정. exact asset 적용·EWU/SCC·정적 storage·production-live 5일 3-seed 전 공식/시뮬레이션/실전 완료 아님
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
Editor 폭주 방지: 활성 절대 평균 16 MB/frame 이하, 단일 최대 256 MB 이하. 측정 파손·폭주 차단용 한계
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
- 무연구 건강 성인의 V27 기준은 현재 정상 AI 5일×3 seed 실측을 보수적으로 반올림한 `actual 50 WU/인·일`, 일정·계약·생산량용 `effective 45 WU/인·일`이다. 기존 `20 WU/인·일`은 V26 역사적 Before로만 보존한다.
- `1 WD(Worker Day)`는 고정 99 WU가 아니다. 계산 대상 기술 단계와 집단의 승인된 라이브 `actual WU/인·일` 또는 `effective WU/인·일` 중 어느 권위를 사용했는지 반드시 함께 기록한다.
- 시작 주민 3명의 V27 중앙 기준은 실제 수행 `150 WU/일`, 일정·산출 `135 WU/일`이며 숙련·종족·특성·시설·욕구·동선 분포를 적용하기 전의 값이다.
- `99 WU/인·일`을 사용한 기존 연구 일수·계약 용량·생산 처리량·장비 준비 계산은 레거시 이론 계산으로 분류하며 신규 밸런스 판단에 사용하지 않는다.

### 2.2 밸런스 비용 벡터

모든 콘텐츠는 다음 비용 벡터로 평가한다.

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
- 금화는 외부 조달·계약용 회계 자원으로 유지한다.
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

기준 연구원 1명의 하루 연구 일정은 무연구 V27 effective 기준 `45 WU`에서 연구 수행 성능과 실제 연구 가능 시간을 적용해 계산한다. 실제 수행량을 기록할 때만 actual `50 WU`를 사용하며 `99 WU` 고정 나눗셈은 금지한다.

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

이 표는 장시간 시뮬레이션이 맞춰야 할 이론 목표 밴드다. 종족·정책에 따라 범위 안 구성이 달라질 수 있다. 기준 원정 비교에는 실제 카탈로그 장비 스냅샷과 숙련 투영을 사용하고, 캠페인 `requiredPower`로 캐릭터 능력치를 역산하지 않는다. 파티 밖 전투 준비 인구는 부상 교대·방어 잔류·다중 원정의 회복 탄력성으로 평가한다.

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
- 하수인과 정식 주민의 세부 조건, 사회 비용, 재사회화와 지배 전략 기준은 `captivity:v28:minion-resident-standing-and-rehabilitation` 기록이 소유한다.

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
- 정적 계약 감사의 V27 기준 정착지는 성인 생산인구 12명, 성인당 effective `45 WU/일`, 생산·성장 가동률 42.5%다. 기준 기간 생산량은 `12 × 45 × 계약 기한 × 0.425` EWU로 계산한다. `12 × 20`은 V23/V26 Before 재현에만, `12 × 99`는 레거시 이론 진단에만 사용한다.
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

사건·축제·손님·세력·계약·연구 분기·원정·세대·이정표 후속 압력의 결합 등급, 실제 도메인 과정, 흔적과 역반응 기록 방법은 [`시스템 콘텐츠 통합 설계`](systemic-content-integration-design.md)를 따른다.

```text
정의 ID:
콘텐츠 종류:
정의·카탈로그·실행기 위치:
등장 시대와 연구:
플레이어에게 주는 새 결정:
목표 시스템 결합 등급과 근거:
발생 생산자와 실제 원인 증거:
실제 도메인 명령·진행·해결 영수증:
영속 흔적과 역반응 소비자:
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
| 사건·축제·손님 | 실제 원인, 결합 등급, 도메인 과정, 실제 비용, 기한, 선택 기대가치, 해결 영수증, 영속 흔적, 역반응, 주의력 |
| 세력·계약 | 실제 원인, 결합 등급, 물리 이전·호위·작업·전투 영수증, 기간 생산비, 의무·원한, 장기 손익분기, 교차 세력·공급망 영향 |
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
지배 전략 방지 조건: UI 재개방·실패 주문은 대체 재료 성공 사건을 만들지 않음. 단식 상태는 저장 권위이며 자동 식사로 아이템만 사라지는 경로를 차단
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
플레이어에게 주는 새 결정: 없음. 기존 Cook·DrawWater 명령이 저술된 정식 생존 출력으로 연결됨
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

## V27 전역 노동·시설 기간 보존 권위 배정 기록 (2026-08-17)

```text
정의 ID: balance:v27:global-labor-facility-period-preserving
콘텐츠 종류: 전 생산 레시피 350개, 작물 12종의 파종·수확 24개 작업량, 물리 BOM이 있는 시설 356개의 authored WU 전수 재배정
정의·카탈로그·실행기 위치: ProductionRecipeSO.requiredWork, CropDefinitionSO.sowWork/harvestWork, BuildingWorkAmountAbility.constructionWorkRequired, V27BalanceWorkCalculator, V27EmbeddedWorkValueCalculator, ProductionBillRuntime, CropPlotRuntime, ConstructionSite, WorkAmountSystem
등장 시대와 연구: 기존 각 레시피·작물·시설의 시대, 연구, 해금과 선행 조건을 그대로 유지하고 신규 콘텐츠·해금·대체 경로를 추가하지 않음
플레이어에게 주는 새 결정: 기존 생산·재배·건설 선택은 유지하되 정상 AI의 유효 산출 기준이 20→45 WU/성인·일로 상승한 만큼 같은 달력 기간을 지불함. BOM·시설 기능·출력량은 바뀌지 않음
물리 BOM·입력·출력: 전 대상의 입력 item ID·수량, 출력 item ID·수량·확률, 작물 수확량·종자 반환, 시설 건설 재료 종류·수량을 Before와 After에서 byte-equivalent 의미로 유지함. BOM 재분배 후보는 비교 행으로만 남기고 적용하지 않음
직접 작업량과 계산 근거: 각 authored Before에 `Ceil(Before×45/20)`을 적용함. 350 recipe + 24 crop work + 356 building, 총 730개 작업량 행이 대상이며 런타임 파생 작업은 정확한 2.25 scale을 사용함. 기존 첫 수직 슬라이스 9개는 같은 공식으로 이미 적용됨
EWU와 목표 회수 기간: BOM Acquisition EWU도 동일 V27 원가 사슬에서 2.25배로 이동하므로 356개 시설 authored 노동밀도 비율은 0.9999331467~1.0258584894로 정상 범위 0.80~1.25 안에 유지됨. WU×1.5+BOM 증가는 분모를 더 키워 노동밀도를 악화하므로 기각함
공간·전력·물·연료·정비: 시설 footprint, 접근 셀, 작업자 슬롯, 전력, 상수, 하수, 연료, 정비, 저장량과 처리량은 변경하지 않음. 작물 성장시간·용수·면적과 레시피 유틸리티도 변경하지 않음
위험·실패·회복 방식: 정확한 historical Before가 승인 원장에 없거나 현재 Authority가 Before/After 어느 쪽과도 다르면 fail-loud함. 작업 취소·시설 파괴·재료 부족·작물 실패의 기존 typed terminal과 보존성은 유지하며 과거 세이브 마이그레이션은 범위 밖임
사회·비가역 비용: 같은 달력 기간과 기존 재료·시설 점유를 유지하므로 작업자 기회비용, 생존·의료·경비 이탈, 생산 대기열 비용을 낮추지 않음. 기존 진행 중 주문의 과거 저장값 변환은 하지 않고 신규 주문이 현재 V27 권위를 사용함
기존 대안과의 장단점: WU×2.25+BOM 동일은 기간과 노동밀도를 함께 보존하고 diff가 최소임. WU×1.5+BOM 보강은 노동밀도를 하락시키며 새 재료 수요와 물류 병목을 만들기 때문에 전수 기본안에서 제외함. 개별 실전 ROI 이상치는 후속 도메인 보정에서 별도 승인함
지배 전략 방지 조건: 동일 BOM에서 더 짧은 달력 생산 0, 작업량 분할로 Ceil 비용 감소 0, 철거→재건 비음수 순환 0, BOM·WU·시간을 동시에 모두 이기는 같은 시대 대안 0, 승인 없는 authored WU 변경 0. 356개 해체·재건 최대 margin은 -87,366 mEWU 이하로 엄격히 손실적임
저장 권위와 실행 명령: ScriptableObject authored 필드가 표시·신규 작업의 권위이고 V27BalanceWorkCalculator가 공식 파생 WU 권위임. 기계 승인은 exact Before/After·dependency fingerprint·source digest를 저장하며 사용자의 별도 수동 승인을 요구하지 않음. 실행은 Generate Exact Labor and Facility Approvals→ApplyApproved→VerifyApplied 순서임
자동 감사 ID와 전수 목록 포함 여부: V27_LABOR_AUTHORED_WU_SCALE_EXACT, V27_LABOR_BOM_UNCHANGED, V27_FACILITY_LABOR_DENSITY_NORMAL, V27_FACILITY_DISMANTLE_REBUILD_STRICT_LOSS, V27_LABOR_EXACT_APPROVAL_KEYS, V27_LABOR_ASSET_APPLIED_EXACT를 전수 목록에 포함함
검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-balance-labor-facility-authority.txt`, `v27-balance-before-after.csv`, `v27-balance-recalibration-audit.txt`, `v27-balance-economy-256-seed.txt`, YAML second-run zero diff, Production/Crop/Construction focused PlayMode, DailyRoutineWu 3 seed, Unity Console Warning/Error 0/0을 요구함
현재 밸런스 상태: 밸런스 기준 배정 / 전수 적용·공식·실전 검증 진행 중. exact approval·SO 적용·no-op 재적용·256 seed·focused PlayMode·5일 실전 재보정이 모두 통과하기 전에는 밸런스 공식 검증·실전 보정·완료로 보고하지 않음
```

## V27 전투 조우 실제 장비·숙련 결과 보정 기록 (2026-08-17)

```text
정의 ID: balance:v27:combat-outcome-checkpoint-calibration
콘텐츠 종류: 기존 36개 전투 조우의 실제 장비·숙련·진형·위험도 기반 1,000-seed 결과 보정
정의·카탈로그·실행기 위치: CombatBalanceCheckpointAuthority, CombatOutcomeBalanceCalibrationScenario, SettlementPopulationPowerCheckpointDebugScenarios, EnemyCombatContentCatalog, EnemyEncounterDefinitionSO, EnemyArchetypeDefinitionSO, OffenseBattleModel
등장 시대와 연구: 캠페인 1/30/120/240/400/960일의 기존 연구·장비 해금·적 조우 순서를 유지하고 신규 적·기술·보상·무료 장비를 추가하지 않음
플레이어에게 주는 새 결정: 기존 무기·방어구·방패·품질과 전투 명령을 그대로 사용하며 일상·표준·위험·보스 조우의 준비 수준과 감수할 부상 위험을 수치로 비교할 수 있게 함
물리 BOM·입력·출력: 전투 장비, 탄약, 원정 보급, 약품, 전리품과 보상 물리 수량은 보정 전수 감사에서 Before와 After를 각각 기록함. 결과 밴드만 맞추기 위해 아이템을 생성하거나 requiredPower에서 가짜 장비 능력치를 역산하지 않음
직접 작업량과 계산 근거: 정착지 생산 WU는 변경하지 않음. 전투 표본은 조우마다 1,000 deterministic seed를 사용하고 checkpoint 전투 준비 최소 인원을 실제 원정 상한 5명으로 제한한 2/2/3/5/5/5명, checkpoint 장비 정의의 피해·방어·방패·품질, 실제 숙련 성장 규칙과 authored 적 구성·라운드·위험 배율을 입력함. 캠페인 5·6에서 파티 상한을 넘는 준비 인구는 승률에 합산하지 않고 교대·방어 여력으로만 남김. 조우별 보정 축은 enemyHealthMultiplier·enemyDamageMultiplier 0.10~4.00과 기존 objectiveRoundLimit이며 적 archetype 원본과 정착지 WU를 역산하지 않음
EWU와 목표 회수 기간: 전투 보급·장비 내구·부상 치료·사망·전리품·보상을 AcquisitionCost debit Ceil과 RecoverableValue credit Floor로 환산함. 각 조우의 기대 순가치와 캠페인 구간 회수 기간은 결과 밴드 통과 후 확정하며 SCC·판매·해체 tolerance는 0을 유지함
공간·전력·물·연료·정비: 기존 전투 시설·병상·장비 보관 공간과 전력·물·연료·정비·수리 비용을 유지하고 변경 후보가 생기면 해당 SerializedProperty를 별도 원장 행과 exact approval로만 적용함
위험·실패·회복 방식: 일상 조우 승률 85~95%·성공 원정의 노출 인원당 Dead/Downed 5% 미만, 표준 65~80%·성공 노출 인원당 Dead/Downed 상한 20%, 위험 45~65%·성공 노출 인원당 Dead/Downed 상한 35%, 보스 첫 시도 25~45%·성공 노출 인원당 Dead/Downed 상한 50%를 목표로 함. 원정 하나에 1명이라도 쓰러졌는지로 세어 대형 파티를 과대 처벌하지 않고 `Dead/Downed 인원 ÷ 성공한 원정의 전체 파티 인원`을 권위로 사용함. HP 25% 미만 생존은 같은 인원 분모의 low-health attrition으로 별도 기록해 의료적 중상과 혼합하지 않으며, 표의 중상 하한은 관찰 기준선으로 보고하되 일부러 부상을 늘리는 실패 조건으로 쓰지 않음. 패배·목표 실패의 Dead/Downed도 실패 노출 인원당 failure-casualty로 별도 기록해 성공 중상 분모에 중복 산입하지 않음. 정지·무효 명령·무한 전투·NaN·표본 누락은 수치 보정으로 숨기지 않고 구조 실패로 분리함
사회·비가역 비용: 사망·중상·수술·회복 병상·원정 부재·장비 파손의 기존 비가역 비용을 실제 결과에 포함하며 패배 직전 세이브 반복이나 보상 복제를 허용하지 않음
기존 대안과의 장단점: 전역 공격력 배율 하나는 일부 조우를 고치면서 다른 조우를 확정 승리로 만들어 기각함. 조우별 적 수·능력·위험·라운드·보상 후보를 최소 변경으로 비교하되 실제 장비·숙련 투영과 authored 전투 실행기를 우회하지 않음
지배 전략 방지 조건: 승률만 맞추고 중상·소모·순가치를 악화하는 후보 0, requiredPower 역산 능력치 0, 무료 회복·무료 장비·무료 행동 0, 승리 보상 EWU가 기대 debit을 무제한 초과하는 조우 0, 동일 조우 반복 비음수 자원 순환 0
저장 권위와 실행 명령: EnemyEncounterDefinitionSO·EnemyArchetypeDefinitionSO·실제 장비 SO와 숙련 성장 규칙이 입력 권위이고 CombatBalanceCheckpointAuthority는 감사 표본의 명시적 checkpoint 권위임. `CombatOutcomeBalanceCalibrationScenario.RunAll()`은 읽기 전용 결과 증거를 쓰며 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: V27_COMBAT_CHECKPOINT_ACTUAL_LOADOUT_AUTHORITY, V27_COMBAT_REQUIRED_POWER_REVERSE_ENGINEERING_ZERO, V27_COMBAT_1000_SEEDS_PER_ENCOUNTER, V27_COMBAT_WIN_SEVERE_BANDS, V27_COMBAT_STALL_ZERO, V27_COMBAT_EWU_NET_VALUE, V27_COMBAT_ARTIFACT_FRESH를 전수 원장 manifest와 CI 필수 목록에 포함함
검증 매트릭스와 보고서 위치: `Artifacts/QA/combat-outcome-balance.txt`, `combat-power-sweep.txt`, `combat-content-balance.txt`, `v26-population-power-checkpoints.md`, `v27-balance-before-after.csv`, 조우별 1,000 seed, 캠페인 checkpoint 매트릭스, 전투 PlayMode, 5일 3-seed 후속 의료·생산 손실, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정 / 실제 Before 재측정 진행 중. 실제 장비·숙련 기반 36개 조우 1,000-seed 결과, 밴드 밖 조우의 exact approval·ApplyApproved, 재실행, EWU 순가치, 전투 PlayMode와 Console 0/0 전에는 전투 밸런스 공식 검증·실전 보정·완료로 보고하지 않음
```

## V27 전투 조우 36종 적용·재검증 후속 기록 (2026-08-17)

```text
정의 ID: balance:v27:combat-outcome-checkpoint-calibration-applied-v1
콘텐츠 종류: 기존 공격 원정 전투 조우 36종의 production tactics 결과 배율·목표 내구·제어 저항·라운드 제한 보정
정의·카탈로그·실행기 위치: CombatBalanceCheckpointAuthority.AllEncounters, OffenseEncounterSO, V20CombatContentAssetBuilder, EnemyEncounterFactory, EnemyTacticalDecisionService, OffenseBattleModel, CombatOutcomeBalanceCalibrationScenario, V27BalanceAudit, V27BalanceAssetApplication
등장 시대와 연구: 캠페인 1~6과 기존 1/30/120/240/400/960일 장비·숙련 checkpoint를 유지함. 연구 해금, 적 archetype, 전투 보상, 조우 순서, 과거 세이브 마이그레이션은 변경하지 않음
플레이어에게 주는 새 결정: 근접·원거리 혼합 2/2/3/5/5/5 원정대, 실제 무기·방어구·방패·탄약·숙련으로 조우별 목표 성공률과 중상 위험을 구분함. ProtectTarget 적은 법적으로 공격 가능한 보호대상을 전술적으로 우선하며, hook-pull은 실제 진형 이동과 지연으로 실행됨
물리 BOM·입력·출력: 장비·탄약·약품·원정 보급·전리품 BOM과 수량은 Before와 동일함. 무료 장비·무료 회복·무료 행동·보상 증액은 0이고 additionalEnemyCount는 36행 모두 0을 유지함
직접 작업량과 계산 근거: 생산·건설·치료 WU는 변경하지 않음. 36종 각각 production EnemyTacticalDecisionService로 1,000 deterministic seeds를 실행함. 변경은 18개 에셋 35 scalar이며 exact Before→After는 03:o 1→2.6; 04:h 1→0.122,o 1→0.8,r 7→9; 06:h 1→2.53,c 1→1.5; 10:h 1→0.503,o 1→0.25; 11:d 1→0.632; 12:h 1→0.224,d 1→0.2,c 1→0.25,r 7→8; 14:d 1→0.632; 16:h 1→0.411,o 1→0.95; 18:c 1→1.2; 21:h 1→2.53,d 1→2; 22:h 1→1.55,d 1→0.8; 25:h 1→2.53,d 1→4; 27:h 1→2,d 1→2; 28:h 1→3.2,d 1→1.25; 30:h 1→2.53,c 1→2; 33:h 1→1.8,d 1→7,a 1→8; 34:h 1→3.789; 36:h 1→2.53,c 1→0.5임. h=enemyHealthMultiplier, d=enemyDamageMultiplier, a=enemyAccuracyMultiplier, o=objectiveHealthMultiplier, c=objectiveControlResistanceMultiplier, r=objectiveRoundLimit임
EWU와 목표 회수 기간: 이번 수직 적용은 전투 결과 권위만 교정하고 보급 debit·내구 손실·치료·사망·전리품·보상 EWU 값은 바꾸지 않음. 전투 순가치와 캠페인 회수 기간은 전수 경제 단계에서 Acquisition debit Ceil/Recoverable credit Floor로 별도 확정하며 SCC tolerance 0을 유지함
공간·전력·물·연료·정비: 기존 원정 준비 공간, 병상, 보관, 전력, 물, 연료, 장비 정비와 수리 비용은 모두 Before와 동일함
위험·실패·회복 방식: Routine Defeat 비엘리트·비보스 85~100%, Survive 85~100%, Escape 표준/엘리트/보스 80/65/55% 하한, 일반 표준 65~80%, 일반 엘리트 45~65%, 일반 보스 25~45%, ProtectTarget 표준 65~80%, ProtectTarget 엘리트 55~90%를 사용함. 성공 전투 Dead/Downed 상한은 표준 20%, 엘리트 35%, 보스 50%이며 stalled·rejected command·NaN은 0이어야 함
사회·비가역 비용: 사망·Downed·저체력·수술·회복 병상·원정 부재·장비 내구 손실의 기존 비가역 비용을 실제 결과에 포함함. 패배 직전 반복 저장이나 보상 exact-once 위반을 허용하지 않음
기존 대안과의 장단점: 전역 공격력 한 값, requiredPower 역산, 적을 전부 Front로 강제, 검증기 전용 명령은 조우별 의미와 진형을 파괴해 기각함. 조우 로컬의 최소 배율·내구·저항·라운드 변경만 선택했고 적 수·BOM·보상은 유지함
지배 전략 방지 조건: 36개 중 한 조우라도 목표 밴드 이탈·중상 상한 초과·무한 전투·무효 적 명령이 있으면 실패함. 보호대상 대신 저체력 미끼만 공격하는 전술, route의 authored archetype 누락, hook-pull 표시만 있고 실제 진형이 안 바뀌는 false-green을 별도 회귀로 금지함
저장 권위와 실행 명령: OffenseEncounterSO가 실제 배율 저장 권위이고 CombatBalanceCheckpointAuthority.AllEncounters가 빌더·원장·검증기의 단일 승인 표임. GenerateCombatEncounterApprovalsFromMenu→ApplyApproved→FinalizeAppliedCombatCheckpointEvidence 순서이며 exact Before/After·dependency fingerprint·source digest·baseline record ID가 일치해야 함
자동 감사 ID와 전수 목록 포함 여부: COMBAT_BALANCE_ALL_FINAL_CHECKPOINTS_V1, COMBAT_BALANCE_APPLIED_FINAL, PRODUCTION_PROTECT_OBJECTIVE_TARGET_PRIORITY, PRODUCTION_HOOK_PULL_PROJECTED_AND_EXECUTED, ROUTE_ENCOUNTER_AUTHORED_DIVERSITY_PRESERVED, V27 combat 252 ledger rows, exact combat approval 35개를 전수 목록에 포함함
검증 매트릭스와 보고서 위치: `Artifacts/QA/combat-balance-final.txt`, `Artifacts/QA/combat-balance-final/encounter-01..36.txt`, `Artifacts/QA/v27-balance-before-after.csv`, `Artifacts/QA/v27-balance-recalibration-audit.txt`; 적용 결과 36×1,000 PASS, failures=0, stalled=0, 18 assets/35 properties, no-op differing=0, Unity Console Warning/Error=0/0
현재 밸런스 상태: 전투 조우 scalar 공식·적용·결정론 검증 완료. 게임 전역 밸런스 완료는 아님. 전투 보급·내구·의료·보상 EWU 순가치, 전체 256-seed 경제, 전투 PlayMode, 5일 3-seed 후속 비용과 최종 manifest가 통과하기 전에는 전투 실전 보정·전체 밸런스 완료로 보고하지 않음
```
## V27 정상 AI 5일 실전 노동 생산성 최종 보정 기록 (2026-08-17)

```text
정의 ID: balance:v27:daily-routine-actual-effective-wu-final
콘텐츠 종류: 정상 AI 현재 소스 기준 5일 3-seed 실제 노동량·유효 산출량 최종 실전 보정 증거
정의·카탈로그·실행기 위치: DailyRoutineWuPlayModeVerifier, CharacterAiDecisionPipeline, AbilityWork, WorkTaskExecutor, V27BalanceWorkCalculator, phase157-daily-routine-wu-seed-157181/157182/157183 reports
등장 시대와 연구: 기존 5일 일상 루틴 fixture의 시대·연구·인구·작업 카탈로그를 유지하며 새 해금이나 콘텐츠를 추가하지 않음
플레이어에게 주는 새 결정: 없음. 이 기록은 이미 적용된 actual 50·effective 45 WU/성인·일 권위가 정상 AI 실전에서 보수적으로 유지되는지 재측정함
물리 BOM·입력·출력: 음식·물·위생·휴식·작업의 기존 물리 입력과 출력 수량을 변경하지 않음. 세 seed 모두 5일 동안 실제 생산·소비·물류 경로를 사용하고 감사용 무료 지급이나 런타임 억제를 사용하지 않음
직접 작업량과 계산 근거: seed 157181 actual=50.340, effective=46.165; 157182 actual=51.117, effective=46.871; 157183 actual=51.927, effective=47.847 WU/성인·일. 평균 actual=51.128, 표본 표준편차=0.794, CV=1.55%; 평균 effective=46.961, 표본 표준편차=0.845, CV=1.80%. authored actual 50과 일정 권위 effective 45는 각각 실측 평균보다 2.21%·4.18% 낮아 보수적 여유를 유지함
EWU와 목표 회수 기간: 기존 20 WU 기준 대비 authored actual 배율 2.50, effective 기간 보존 배율 2.25를 유지함. 새 실측값을 다시 수치에 곱해 재팽창시키지 않으며 모든 원가·SCC·ROI는 승인된 50/45 권위로 계산함
공간·전력·물·연료·정비: 5일 fixture의 기존 시설 footprint·접근·전력·상수·하수·저장·정비 조건을 유지함. 측정 편의를 위한 공간·유틸리티 면제는 없음
위험·실패·회복 방식: 세 seed 중 하나라도 5일 미달, runtimeDiagnosticsGate 불일치, Console issue, terminal 실패, actual/effective 범위 이탈이면 실전 보정을 실패 처리함. 현재 세 보고서 모두 RESULT=PASS, failures=0, capturedIssues=0임
사회·비가역 비용: 식사·수면·위생·이동·예약·작업 전환·긴급 self-care로 빠지는 실제 시간이 effective 산출에 포함됨. 이 손실을 제거하거나 생산 작업으로 재분류하지 않음
기존 대안과의 장단점: 초안의 48.809 actual·44.971 effective는 AI 안정화 이전/중간 표본이라 방향성 근거로만 보존함. 현재 소스의 51.128·46.961은 변동이 더 작지만 그대로 authored 기준으로 올리면 일정과 원가를 다시 팽창시키므로 50/45를 안정적인 보수 기준으로 유지함
지배 전략 방지 조건: 측정용 시간·재료 생성 0, 작업자 강제 고정 0, 생존 행동 억제 0, 실패 seed 제외 0, 유리한 seed 선택 0, 동일 생산을 actual과 effective에 이중 계상 0
저장 권위와 실행 명령: ScriptableObject·작업 런타임·실제 Character 상태가 권위이며 각 seed는 DailyRoutineWuPlayModeVerifier.RequestRun(157181/157182/157183)의 독립 PlayMode 세션과 durable artifact를 사용함. 과거 세이브 변환은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: observedDays=5, exact runSeed, runtimeDiagnosticsGate=ai-runtime-gate-v3, RESULT=PASS, failures=0, capturedIssues=0을 세 seed 모두 요구하며 V27 artifact manifest와 이 baseline ID가 최종 실전 보정 증거를 참조함
검증 매트릭스와 보고서 위치: Artifacts/QA/phase157-daily-routine-wu-seed-157181.txt, phase157-daily-routine-wu-seed-157182.txt, phase157-daily-routine-wu-seed-157183.txt, v27-balance-artifact-manifest.json, final-acceptance-report.txt, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 실전 보정 PASS. 정상 AI 현재 소스에서 5일×3 seed가 actual 50·effective 45 목표를 안정적으로 상회하며 CV 2% 미만임. 전수 원장·SCC·256-seed·수직 슬라이스·YAML/no-op·FinalAcceptance의 별도 게이트 결과와 함께 판단하며 AI 전체 커버리지 매니페스트의 stale 증거 71축은 별도 재실행 대상으로 남김
```

## V27 actual/effective 노동 권위 하류 연결 교정 기록 (2026-08-17)

```text
정의 ID: balance:v27:actual-effective-labor-authority-downstream-v1
콘텐츠 종류: actual 50·effective 45·역사적 이론 상한 99 WU/성인·일의 단일 권위와 계약·연구·종족·장비 준비·비상 노동 하류 연결
정의·카탈로그·실행기 위치: SettlementLaborAuthority, SettlementLaborBalanceRules, AuthoredFactionContractBalanceRules, SettlementLaborAccountingRuntime, V27SpeciesBalanceSimulationDebugScenarios, ResearchEquipmentOverhaulDebugScenarios, SettlementEquipmentReadinessThroughputDebugScenarios, V27BalanceAudit
등장 시대와 연구: 무연구부터 엔드리스까지 공통 노동 단위 권위를 교정함. 기술 단계 actual 50/54.5/62.5/74.5/85/100, effective 45/49.05/56.25/67.05/76.5/90을 사용하며 새 연구나 콘텐츠를 추가하지 않음
플레이어에게 주는 새 결정: 실제 수행·숙련·종족 유지비에는 actual을, 달력 일정·계약·연구·성장 처리량·정착지 산출 지수에는 effective를 사용함. 99를 현재 생산량으로 오인해 기한과 처리량이 과대평가되는 불일치를 제거함
물리 BOM·입력·출력: 물리 아이템 BOM·출력 수량·소비량은 변경하지 않음. 계약·연구·장비 준비 계산은 이미 적용된 V27 WU와 effective 45를 함께 사용하며 같은 작업을 actual/effective 양쪽에 중복 계상하지 않음
직접 작업량과 계산 근거: 현재 5일 3-seed actual 평균 51.128·effective 평균 46.961의 보수 authored 권위 50/45를 사용함. 99는 100초 작업창×0.99 전환 효율의 역사적 이론 상한으로만 남기며 V26 Before 20은 재현용 값임
EWU와 목표 회수 기간: V27 생산·건설·재배 authored WU는 V26 20 기준 대비 2.25배 적용되어 있으므로 일정·ROI·계약은 effective 45로 나눠 기존 달력 기간을 보존함. actual 50은 수행·경험·마모에만 사용해 비용과 기간을 50/45로 이중 축소하지 않음
공간·전력·물·연료·정비: 기존 공간·유틸리티·연료·정비 비용은 불변이며 생존·정비·이동·예약으로 손실되는 10%를 actual 50에서 effective 45로 명시함
위험·실패·회복 방식: 모호한 Baseline 이름, 하류 20/99 상수, 기술 단계 누락, 계약 mirror drift, 인구·기술·생존·비상 행렬 누락을 fallback 없이 실패함. past-save migration은 범위 밖이며 새 계산과 새 작업만 현재 권위를 사용함
사회·비가역 비용: 의료·침입·경비·생존 부족으로 이탈하는 인원을 effective capacity에서 제외하고 필수 노동을 먼저 차감함. 사망·부상·경비 인력을 생산 가능 인원으로 계속 세지 않음
기존 대안과의 장단점: 단일 50만 사용하면 일정이 의도보다 10% 빨라지고 단일 45만 사용하면 실제 경험·마모가 과소 계상됨. 99를 유지하면 계약·연구·준비 기간이 절반 이하로 축소됨. 명시적 이중 권위는 호출부 의미를 드러내는 대신 모든 하류 계산을 재검증해야 함
지배 전략 방지 조건: actual과 effective 중 유리한 값을 호출자가 임의 선택하지 못하게 이름과 테스트를 고정함. 계약 요구량·연구 기간·장비 준비량은 effective만, 경험·종족 유지비·실제 수행은 actual만 사용하며 99 현재 권위 사용 수를 0으로 요구함
저장 권위와 실행 명령: 고정 노동 권위는 DungeonStory.Work의 SettlementLaborAuthority가 유일 원본이고 저장하지 않음. ScriptableObject 수치와 진행 중 주문의 저장값은 그대로 유지하며 과거 세이브 변환은 구현하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_LABOR_AUTHORITY_SINGLE_SOURCE, V27_LABOR_ACTUAL_50, V27_LABOR_EFFECTIVE_45, V27_LABOR_HISTORICAL_99_DIAGNOSTIC_ONLY, V27_CONTRACT_REFERENCE_12X45X0425, V27_POPULATION_TECH_SURVIVAL_EMERGENCY_MATRIX를 필수 목록에 추가함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-labor-authority-matrix.txt, phase157-technology-founder-wu.md, v27-species-capacity-balance.md, research/equipment-readiness reports, V27 전수 원장·256-seed 경제·DailyRoutineWu 3 seed·FinalAcceptance·Unity Console 0/0을 현재 소스에서 재실행함
현재 밸런스 상태: 밸런스 기준 배정. 단일 권위 구현, 하류 재계산, 인구 1/3/6/12/24×기술×생존×의료·침입·경비 행렬, 전수 원장 재생성, fresh 실전 증거와 CI가 모두 통과하기 전에는 공식 검증·시뮬레이션 검증·실전 보정 완료로 승격하지 않음
```

## V27 연구 일정 effective 45 권위 기간 보존 재배정 기록 (2026-08-17)

```text
정의 ID: balance:v27:research-effective-output-period-preserving-v1
콘텐츠 종류: 기존 연구 프로젝트 180개의 requiredWork를 현재 effective 45 WU/성인·일 일정 권위에 맞추는 기간 보존 재배정
정의·카탈로그·실행기 위치: ResearchProjectSO.requiredWork, ResourceResearchProjectCatalog, ResearchWorkExecutionHandler, ResearchEquipmentOverhaulDebugScenarios, ResearchTreeDebugScenarios, V27BalanceAudit, V27BalanceAssetApplication
등장 시대와 연구: 중세·초기 산업·성숙 산업·후기 산업과 시간 고정까지 기존 180개 연구, 선행 그래프, 해금, 설계도, 시설 요구를 그대로 유지하며 연구를 추가·삭제하지 않음
플레이어에게 주는 새 결정: 해금 순서와 선택지는 불변이다. 과거 99 WU/일 이론 상한으로 작성된 연구 WU를 현재 일정 권위 45 WU/일로 환산해 의도했던 달력 진행 속도만 복구함
물리 BOM·입력·출력: 연구 설계도, 지식 잔여물, 연구 시설, 전력, 물리 입력과 모든 해금 출력은 Before와 After에서 동일함. 180개 에셋의 requiredWork scalar만 변경 후보이며 과거 세이브 마이그레이션은 범위 밖임
직접 작업량과 계산 근거: 각 프로젝트에 `After=Ceil(Before×45/99)`를 적용함. 180개 총합은 138,824→63,173 WU다. 중세 폐쇄합 3,184→1,465, 초기 산업 7,964→3,640, 성숙 산업 23,192→10,569, 후기 산업 36,828→16,777, 시간 고정 폐쇄합 95,448→43,423 WU임
EWU와 목표 회수 기간: 연구 WU는 연구원 기회비용 debit으로 입력 Ceil을 유지한다. 새 effective 45 기준 누적 기간은 중세 32.555556일, 초기 산업 80.888889일, 성숙 산업 234.866667일, 후기 산업 372.822222일, 시간 고정 964.955556일이며 기존 의도 밴드 27–34/80–100/200–240/320–400일을 복구함
공간·전력·물·연료·정비: 연구 시설 면적·동시 연구자 1/2/4명·전력·용수·보관·정비·시설 capability는 변경하지 않음. 해당 비용은 별도 EWU 입력으로 유지함
위험·실패·회복 방식: 현재 값이 승인 원장의 exact Before 또는 계산된 After가 아니면 fail-loud함. 누락 프로젝트·중복 stable ID·선행 그래프 오류·기간 밴드 이탈·승인 source digest drift·YAML identity 변화는 fallback 없이 실패함
사회·비가역 비용: 연구 중 식사·수면·의료·경비·침입 대응으로 빠지는 시간은 effective 45에 이미 포함됨. actual 50을 추가로 나누어 연구 기간을 이중 단축하지 않으며 진행 중 과거 주문을 변환하지 않음
기존 대안과의 장단점: WU를 그대로 두고 45로 나누면 중세 70.8일, 초기 산업 177.0일, 성숙 산업 515.4일, 후기 산업 818.4일로 의도보다 약 2.2배 느려져 기각함. 전역 2.25배를 다시 곱하는 안도 연구가 이미 99 기준으로 작성되어 있어 기각함. 프로젝트별 Ceil 45/99는 최소 scalar 변경으로 기존 달력 밴드를 보존함
지배 전략 방지 조건: 연구 WU 0·음수 0, 선행 우회 0, 무료 설계도·시설 0, actual/effective 이중 할인 0, 배치 분할로 Ceil 비용 감소 0, 승인되지 않은 연구 에셋 변경 0
저장 권위와 실행 명령: ResearchProjectSO.requiredWork가 신규 연구 작업의 권위이고 활성 연구 save는 completedWork를 보존함. V27 audit→exact labor/facility approval→ApplyApproved→VerifyApplied 순서를 사용하며 구버전 세이브 자동 변환은 구현하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_RESEARCH_WU_EFFECTIVE_AUTHORITY_EXACT, V27_RESEARCH_WU_ASSET_APPLIED_EXACT, RESEARCH_180_PROJECT_CATALOG_EXACT, RESEARCH_PACING_M/E/A/L_IN_BAND, RESEARCH_TEMPORAL_CLOSURE_EXACT를 180개 원장 행과 함께 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-before-after.csv, v27-balance-labor-facility-authority.txt, V27_Balance_Before_After.md, ResearchEquipmentOverhaulDebugScenarios, ResearchTreeDebugScenarios, V27 YAML second-run zero diff, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정. exact approval·180개 SO 적용·두 연구 감사·전수 원장·YAML no-op·인구/기술/생존/비상 행렬·fresh 5일 3-seed가 모두 통과하기 전에는 공식 검증·시뮬레이션 검증·실전 보정 완료로 승격하지 않음
```

## V27 인구·기술·생존·비상 노동 권위 행렬 기록 (2026-08-17)

```text
정의 ID: balance:v27:population-technology-survival-emergency-matrix-v1
콘텐츠 종류: actual 50·effective 45 노동 권위의 인구 1/3/6/12/24명×기술 6단계×생존 3상태×비상 4상태 결정론적 360셀 감사
정의·카탈로그·실행기 위치: SettlementLaborAuthority, SettlementLaborBalanceRules.TechnologyCheckpoints, SettlementLaborBalanceRules.EvaluateDisasterShadow, V27LaborAuthorityMatrixDebugScenarios
등장 시대와 연구: 무연구·초기·중기·산업·후기·엔드리스 6단계를 모두 포함하며 기존 기술 해금·효율·자동화·콘텐츠를 변경하지 않음
플레이어에게 주는 새 결정: 없음. 이 행렬은 인구·기술·생존 비축·의료·침입·경비 이탈 조합에서 실제 생산 가능 인원, 필수 노동 충족률, 성장 WU와 7일 회복 가능성을 공개함
물리 BOM·입력·출력: 음식·물·약품·장비·시설의 물리 BOM과 생산량을 바꾸지 않음. shortage/normal/surplus는 성인당 필수 노동을 effective 45의 90%/50%/30%로 두고 식량·물 비축을 각각 2/7/14일로 입력함
직접 작업량과 계산 근거: 단계별 actual은 50/54.5/62.5/74.5/85/100, effective 산출은 45/49.05/56.25/67.05/76.5/90 WU/성인·일임. 의료는 ceil(인구×20%) unavailable+가능하면 1명 responder, 침입은 ceil(인구×10%) unavailable+ceil(인구×25%) responder, 경비는 ceil(인구×20%) responder이며 합계는 인구를 넘지 않게 제한함
EWU와 목표 회수 기간: 이 행렬은 새 EWU 값을 만들지 않고 가용 effective WU에서 필수 노동을 먼저 차감한 성장 여력을 계산함. 모든 비용·SCC·판매·회수 계산은 기존 입력 Ceil·산출 Floor·SCC tolerance 0 권위를 유지함
공간·전력·물·연료·정비: 시설 footprint·전력·상수·하수·연료·정비 수치를 변경하지 않음. 이 조건이 야기하는 노동은 survival essential burden에만 귀속하고 자동화 WU를 자유 전용 노동으로 이중 계상하지 않음
위험·실패·회복 방식: 비상은 3일 지속, shortage 비축은 2일이라 모든 비상 shortage 셀이 반드시 위기 생존 실패를 드러냄. essential deficit가 있으면 growth는 먼저 0이 되어야 하며 음수·NaN·가용 인원 초과·기술 역전·비축량 무시를 fallback 없이 실패함
사회·비가역 비용: 의료 unavailable, 침입 부상 unavailable, 침입·경비 responder는 생산 가능 인원에서 제외함. 의료 unavailable만 7일까지 회복되고 비상 responder는 계속 비상 배치로 남아 회복률을 과대평가하지 않음
기존 대안과의 장단점: 모든 셀을 PASS로 강제하는 감사는 소인구 취약성을 숨겨 기각함. 고정 인원 차감만 쓰는 방식도 인구 1명과 24명에 같은 충격을 주므로 기각하고 결정론적 비율+Ceil을 사용함. 행렬은 취약 셀을 결과로 기록하되 공식 불변식 위반만 테스트 실패로 처리함
지배 전략 방지 조건: 기술 상승 시 인당 effective 산출·필수 충족률·성장량 감소 0, survival 부담 완화 시 성장 감소 0, 비상 인력이 무사건보다 많아지는 경우 0, 필수 deficit인데 성장 WU가 남는 경우 0, actual과 effective 이중 계상 0
저장 권위와 실행 명령: SettlementLaborAuthority와 TechnologyCheckpoints는 코드 권위이며 저장하지 않음. 실행 명령은 DungeonStory/V27/Verify Population Technology Survival Emergency Matrix이고 산출물은 파생 감사 증거임. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: V27_LABOR_MATRIX_360_CELLS, V27_LABOR_MATRIX_ACTUAL_EFFECTIVE_RATIO, V27_LABOR_MATRIX_TECH_MONOTONIC, V27_LABOR_MATRIX_SURVIVAL_MONOTONIC, V27_LABOR_MATRIX_GROWTH_CUT_FIRST, V27_LABOR_MATRIX_SHORTAGE_CRISIS_EXPOSED를 전수 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-labor-authority-matrix.txt, phase157-technology-founder-wu.md, v27-balance-artifact-manifest.json, V27 whole-game coverage, DailyRoutineWu 157181/157182/157183, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정. 360셀 결정론 실행·manifest hash·whole-game coverage·fresh 3-seed와 Console 0/0 전에는 시뮬레이션 검증 또는 실전 보정 완료로 승격하지 않음
```

## V27 장비 품질 일정·생산 배분 교정 기록 (2026-08-17)

```text
정의 ID: balance:v27:equipment-readiness-quality-schedule-v1
콘텐츠 종류: 캠페인 240/400/960일 기준 원정 장비 품질 일정과 장비용 성장·생산 배분의 actual 50/effective 45 재보정
정의·카탈로그·실행기 위치: CombatBalanceCheckpointAuthority, SettlementEquipmentReadinessThroughputDebugScenarios, CombatEquipmentCraftingRuntime, DeterministicCraftQualityResolver, V23MaterialSalvageCalculator, CombatOutcomeBalanceCalibrationScenario
등장 시대와 연구: 캠페인 1~6의 1/30/120/240/400/960일, 기존 장비 정의·재료·부품·연구·원정 대상 순서를 유지함. 품질 기대는 Normal/Normal/Normal/Good/Good/Excellent에서 Normal/Normal/Normal/Normal/Normal/Good으로 변경함
플레이어에게 주는 새 결정: 중·후기 원정대 전원에게 낮은 확률의 Good/Excellent 장비를 반복 제작하기보다 시대 최신 장비의 Normal 품질을 먼저 갖추고, Good 품질은 960일 룬 장비 시점의 집중 투자로 늦춤. 개별 고품질 제작 선택과 품질 재시도 기능은 유지함
물리 BOM·입력·출력: estoc·articulated-plate·iron shield, powered gauntlet·harness·shield, rune blade·mail·shield의 물리 BOM·출력 수량·부품·재질을 변경하지 않음. 불량품은 production과 동일하게 자동 해체하며 회수량은 V23MaterialSalvageCalculator의 Floor 결과를 사용함
직접 작업량과 계산 근거: 품질 후보별 기대 직접 WU는 craft×1/p + rejected dismantle×(1-p)/p임. Day240 세트는 Good 3,228.103→Normal 611.303, Day400은 Good 2,894.975→Normal 1,088.999, Day960은 Excellent 2,882.325→Good 1,105.851 WU임. 성장·생산 배분은 전역 정상대 35~50% 안에서 35%→37%로 최소 2%p 조정함
EWU와 목표 회수 기간: production exact 순 기대 EWU는 gross item EWU×1/p + dismantle WU×rejects - recovered input acquisition EWU×rejects임. Day240 세트 23,412.840→5,480.962, Day400 21,307.690→10,234.990, Day960 24,016.380→10,566.500 EWU. 파티 수량 적용 후 37% 성장 예산 점유율은 Day240 92.2%, Day400 99.7%, Day960 13.5%로 100% 이하임
공간·전력·물·연료·정비: 제작 시설 footprint·전력·용수·연료·정비·버퍼·작업자 상한은 변경하지 않음. 37%는 성장·생산 정상대 35~50% 안의 장비 준비 감사 배분이며 런타임에서 다른 도메인 WU를 강제로 빼앗는 숨은 보너스가 아님
위험·실패·회복 방식: 품질 성공 확률은 실제 21^3 결정론적 roll 분포, 현재 specialist rank, 장비 복잡도를 사용함. 거부품 해체 WU와 회수 입력을 모두 계상하고 10회 내 수용률 50% 미만·전담 제작자 초과·순 EWU 예산 초과·연구 지연을 fallback 없이 실패함
사회·비가역 비용: 장비 생산 중 제작자·재료·시설 점유, 불량품 해체, 원정 준비 지연은 순비용에 남음. 기존 장비를 무료 업그레이드·삭제·판매·완전 회수하지 않으며 최소 준비 인구의 창+천 후드는 최신 원정 장비로 자동 승격하지 않음
기존 대안과의 장단점: BOM 대폭 삭감은 최신 장비의 물리적 의미와 상류 수요를 훼손해 기각함. 성장 배분만 올리면 Day240 Good 세트가 100%를 훨씬 넘어 기각함. facility bonus를 20점 이상 가정하는 안은 실제 별 등급 1~2 권위와 불일치해 기각함. 품질 한 단계 지연+2%p 배분은 BOM·기능을 보존하는 최소 변경임
지배 전략 방지 조건: 품질 재굴림 회수 크레딧이 원투입을 초과하는 경우 0, 불량 해체 노동 누락 0, Good/Excellent 무료 지급 0, 100% 초과 성장 예산 0, old 99 현재 생산 권위 사용 0, 장비 품질 변경 후 전투 조우 재보정 누락 0
저장 권위와 실행 명령: 실제 장비 인스턴스 품질·제작 주문·회수 재료는 기존 runtime/save 권위가 소유함. CombatBalanceCheckpointAuthority는 편집기 시뮬레이션의 단일 일정 권위이며 과거 세이브·기존 인스턴스를 변환하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_EQUIPMENT_NET_RETRY_COST_EXACT, V27_EQUIPMENT_QUALITY_SCHEDULE_EXACT, V27_EQUIPMENT_GROWTH_SHARE_37, V27_EQUIPMENT_PARTY_ENVELOPE_WITHIN_CAPACITY, V27_EQUIPMENT_REJECTED_RECOVERY_CONSERVATIVE, COMBAT_BALANCE_ALL_FINAL_CHECKPOINTS_V1을 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v26-equipment-readiness-throughput.md, combat-balance-final.txt, combat-balance-final/encounter-01..36.txt, v27-balance-before-after.csv, 36×1,000 combat seeds, 장비 production PlayMode, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정. 처리량 감사·공유 checkpoint 일치·36×1,000 전투 재보정·전수 원장·exact approval·PlayMode·Console 0/0 통과 전에는 장비 공식 검증 또는 전투 실전 보정 완료로 보고하지 않음
```

## V27 장비 품질 일정 변경 후 전투 경계 최소 재보정 기록 (2026-08-17)

```text
정의 ID: balance:v27:combat-after-equipment-quality-minimal-recalibration-v1
콘텐츠 종류: 240/400/960일 원정 장비 품질 일정 하향 뒤 목표 밴드를 이탈한 기존 전투 조우 22·28·33·34의 최소 단일 scalar 재보정
정의·카탈로그·실행기 위치: CombatBalanceCheckpointAuthority.AllEncounters, OffenseEncounterSO, EnemyEncounterFactory, EnemyTacticalDecisionService, OffenseBattleModel, CombatOutcomeBalanceCalibrationScenario, V27BalanceAudit, V27BalanceAssetApplication
등장 시대와 연구: 캠페인 4~6의 기존 조우·연구·적 archetype·목표·보상·원정 순서를 유지함. 원정 장비 품질은 별도 승인 기록의 Day240 Normal, Day400 Normal, Day960 Good 권위를 사용하며 새 콘텐츠나 과거 세이브 변환은 추가하지 않음
플레이어에게 주는 새 결정: 장비 품질 재시도 비용을 감당 가능한 일정으로 낮춘 상태에서도 기존 전투 목표 승률·중상률을 유지함. 적 수·행동·진형·보상은 바꾸지 않고 해당 조우의 체력 또는 피해 scalar 한 축만 조정함
물리 BOM·입력·출력: 장비·탄약·약품·원정 보급·전리품의 BOM·수량·품질 공식·내구·회수·보상은 Before와 After에서 동일함. 변경되는 물리 생산·소비 행은 0이며 전투 조우 SO scalar 4개만 적용 후보임
직접 작업량과 계산 근거: 1,000 deterministic seed 근접 후보 비교로 22 health 1.55→1.45(승률 67.8%, 중상 0.0%), 28 damage 1.25→1.0(67.1%, 20.9%), 33 health 1.8→1.1(67.3%, 49.6%), 34 health 3.789→3.5(65.0%, 21.1%)를 선택함. 다른 scalar와 round는 유지함
EWU와 목표 회수 기간: 이번 변경은 전투 결과 scalar만 교정하고 생산 WU·장비 순 EWU·보급·치료·전리품·보상 EWU를 변경하지 않음. 입력 Ceil·산출 Floor·SCC tolerance 0을 유지하며 전투 순가치는 전체 경제 감사에서 별도 검증함
공간·전력·물·연료·정비: 원정 준비 공간·병상·창고·전력·용수·연료·수리·정비·FacilityBuffer 수치는 모두 Before와 동일함
위험·실패·회복 방식: 일반 표준 조우 승률 65~80%·중상 상한 20%, ProtectTarget 엘리트 승률 55~90%·중상 상한 50%를 사용함. 28번의 20.9%는 해당 조우의 elite 상한 40% 이내이며 33번은 49.6%로 보스/보호 목표 상한 50% 이내임. stalled·NaN·무효 적 명령은 0이어야 함
사회·비가역 비용: 사망·Downed·저체력·수술·회복 병상·원정 부재·장비 내구 손실을 production 결과에 그대로 포함함. 무료 회복·무료 장비·무료 행동·실패 seed 제외는 허용하지 않음
기존 대안과의 장단점: 22번 round 7→8은 승률 81.7%로 상한을 넘고 damage 0.8→0.7은 63.4%로 실패해 health 1.45를 선택함. 28번 damage 1.0 단독이 통과해 round 변경을 배제함. 33번 damage 7→1은 중상률을 낮추지 못했고 accuracy 8→3.75는 통과하지만 변화율이 더 커서 health 1.1을 선택함. 34번 damage 1→0.8도 통과하지만 health 3.789→3.5가 더 작은 상대 변화라 선택함
지배 전략 방지 조건: 목표 밴드 완화 0, 검증기 전용 버프 0, 적 수 감소 0, 보상 증가 0, requiredPower 역산 0, 전역 전투 배율 변경 0, 한 조우에서 두 scalar 이상 변경 0, 36개 전체 재검증 누락 0
저장 권위와 실행 명령: OffenseEncounterSO가 신규 전투 scalar 저장 권위이고 CombatBalanceCheckpointAuthority가 빌더·원장·검증기의 승인 표임. GenerateCombatEncounterApprovalsFromMenu→ApplyApproved→VerifyApplied→36×1,000 final checkpoint 순서를 사용하며 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: COMBAT_BALANCE_ALL_FINAL_CHECKPOINTS_V1, V27_COMBAT_POST_QUALITY_MINIMAL_FOUR, V27_COMBAT_22_HEALTH_1450, V27_COMBAT_28_DAMAGE_1000, V27_COMBAT_33_HEALTH_1100, V27_COMBAT_34_HEALTH_3500, COMBAT_BALANCE_APPLIED_FINAL을 전수 원장·manifest 필수 증거에 포함함
검증 매트릭스와 보고서 위치: Artifacts/QA/combat-balance-focused-search.txt, combat-balance-final.txt, combat-balance-final/encounter-01..36.txt, v26-equipment-readiness-throughput.md, v27-balance-before-after.csv, 장비·전투 PlayMode, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정. 네 focused 1,000-seed 후보는 통과했으나 exact approval·SO 적용·VerifyApplied·36×1,000 전체·전투 PlayMode·전수 경제·Console 0/0 전에는 전투 공식 검증·시뮬레이션 검증·실전 보정 완료로 승격하지 않음
```

## V27 전체 재조정 소스 기준 5일 노동 실측 후속 기록 (2026-08-17)

```text
정의 ID: balance:v27:daily-routine-post-recalibration-wu-evidence-v1
콘텐츠 종류: 연구·장비 준비·전투 재조정을 모두 반영한 현재 소스의 5일 3-seed actual/effective 노동 생산성 최종 재측정
정의·카탈로그·실행기 위치: DailyRoutineWuPlayModeVerifier, CharacterAiDecisionPipeline, AbilityWork, WorkTaskExecutor, SettlementLaborAuthority, V27BalanceWorkCalculator, phase157-daily-routine-wu-seed-157181/157182/157183 reports
등장 시대와 연구: 기존 daily-routine 공식 fixture의 인구 3명·5일·시설·연구 상태를 유지함. 이번 측정은 새 해금·콘텐츠·무료 자동화를 추가하지 않고 현재 전체 재조정 소스를 그대로 사용함
플레이어에게 주는 새 결정: 없음. actual 50은 실제 수행·숙련·마모, effective 45는 일정·계약·생산량 권위라는 기존 분리를 최신 AI 실전에서 재확인함
물리 BOM·입력·출력: 음식·물·위생·휴식·산업 건설 주문의 실제 물리 입력·출력·재고를 사용함. 측정용 무료 생산, 생존 행동 억제, 작업 강제 고정, 실패 seed 제외는 0임
직접 작업량과 계산 근거: seed157181 actual=55.546067/effective=50.860667, seed157182 actual=55.189533/effective=50.766867, seed157183 actual=52.656400/effective=48.781267 WU/성인·일임. 평균 actual=54.464000, 표본 표준편차=1.575545, CV=2.8928%; 평균 effective=50.136267, 표본 표준편차=1.174401, CV=2.3424%임
EWU와 목표 회수 기간: authored actual 50은 실측 평균보다 8.20% 낮고 effective 45는 10.25% 낮아 보수적 여유를 유지함. 이 여유를 다시 원가·기간에 곱해 재팽창시키지 않고 승인된 50/45를 유지함. 입력 Ceil·산출 Floor·SCC tolerance 0은 불변임
공간·전력·물·연료·정비: fixture의 공간·조명·상수·하수·식사 버퍼·산업 건설 주문·작업자 상한을 그대로 사용함. 공간·유틸리티·정비 면제는 없음
위험·실패·회복 방식: 세 seed 모두 observedDays=5, runtimeDiagnosticsGate=ai-runtime-gate-v3, activeActorsAtEnd=3, failures=0, capturedIssues=0을 요구함. 평균이 50/45 아래이거나 CV가 기존 안정 범위를 크게 악화하면 실전 보정 실패로 처리함
사회·비가역 비용: 식사·수면·위생·이동·예약·작업 전환·긴급 self-care와 실제 loss WU가 effective 산출에 포함됨. 이 손실을 actual과 effective에 이중 계상하거나 제거하지 않음
기존 대안과의 장단점: 최신 평균 actual 54.464/effective 50.136을 새 authored 권위로 올리면 향후 작은 AI·동선 변화마다 전역 일정과 원가가 재팽창하므로 기각함. 50/45는 세 seed 모두가 상회하면서 10% 안팎의 일정 안전 여유를 보존함
지배 전략 방지 조건: 유리한 seed 선택 0, 실패 seed 제외 0, 테스트용 time-scale 외 생산 보너스 0, 동일 WU 이중 계상 0, 생존·휴식 억제 0, actual/effective 중 유리한 값의 호출부 임의 선택 0
저장 권위와 실행 명령: 작업·욕구·재고·행동 상태는 기존 runtime/save 권위이고 SettlementLaborAuthority는 비저장 공식 권위임. 각 seed는 DailyRoutineWuPlayModeVerifier.RequestRun(157181/157182/157183)의 독립 PlayMode 세션과 fresh durable artifact를 사용함. 과거 세이브 변환은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: observedDays=5, exact runSeed, runtimeDiagnosticsGate=ai-runtime-gate-v3, RESULT=PASS, failures=0, capturedIssues=0을 세 artifact 모두 요구하며 V27 artifact manifest와 최종 CI가 이 record ID를 참조함
검증 매트릭스와 보고서 위치: Artifacts/QA/phase157-daily-routine-wu-seed-157181.txt, phase157-daily-routine-wu-seed-157182.txt, phase157-daily-routine-wu-seed-157183.txt, v27-labor-authority-matrix.txt, v27-balance-artifact-manifest.json, final-acceptance-report.txt, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 실전 보정 PASS. 현재 전체 재조정 소스에서 5일×3 seed가 actual 50·effective 45를 모두 안정적으로 상회하고 CV 3% 미만임. 전수 원장·SCC·256-seed 경제·수직 슬라이스·YAML no-op·FinalAcceptance·최종 manifest의 별도 게이트와 함께 완료 여부를 판정함
```
## V27 시설 WU·BOM 제한 재분배 교정 기록 (2026-08-17)

```text
정의 ID: balance:v27:facility-bounded-wu-bom-redistribution-v2
콘텐츠 종류: 물리 BOM이 있는 기존 시설 356개의 건설 WU·재료 수량 단일 권위 재조정
정의·카탈로그·실행기 위치: BuildingSO, BuildingWorkAmountAbility, V23BalanceWorkCalculator, V27ConstructionRedistributionPolicy, V27BalanceWorkCalculator, WorkAmountSystem
등장 시대와 연구: 각 시설의 기존 시대·연구·해금·기능을 유지하며 신규 시설·재료 종류·해금은 추가하지 않음
플레이어에게 주는 새 결정: 같은 시설을 기존 재료 종류 안에서 노동 집약형 또는 재료 집약형으로 임의 전환하지 않고, 정상 AI의 기간과 초기 자본을 함께 만족하는 승인값을 지불함
물리 BOM·입력·출력: 기존 item ID만 사용하고 각 정수 수량은 Before 이상, Ceil(Before×1.5) 이하로 제한함. 신규 희귀·전략 자원, 가상 비용, 무료 산출은 0이며 실제 ConstructionSite→AIHaul→소비 경로를 사용함
직접 작업량과 계산 근거: frozen V23 건설 WU의 Ceil(×1.5)~Ceil(×2.25) 범위에서 선택하고, Ceil(V23×2.25)+현재 V27 BOM EWU의 총투자 목표를 ±2% 안에서 맞춤. 정상 밀도 0.80~1.25를 우선하고 경고 범위 0.67~1.50, 재료 비중 60% 이상, 1셀·BOM 2개 이하 초기 인프라만 명시적 경고 예외로 둠
EWU와 목표 회수 기간: 입력 재료와 WU는 mEWU Ceil, 해체 회수는 RecoverableValue Floor를 사용함. 총투자 목표와 건설 기간을 동시에 보존하되 모든 해체→재건 transform은 최소 -1mEWU를 요구하고 SCC tolerance는 0임
공간·전력·물·연료·정비: footprint·접근칸·작업자 슬롯·전력·상수·하수·연료·처리량·저장량은 변경하지 않음. 증가한 실물 BOM의 운반·버퍼·저장 면적은 물류 및 인구 공간 감사에 그대로 포함함
위험·실패·회복 방식: optimizer 해 없음, 총투자 ±2% 초과, WU/BOM cap 초과, 원장 Before와 SO 불일치, 해체 순환 margin 비음수는 fail-loud함. 취소·시설 파괴·no-path·저장복원 시 기존 물리 수량 보존 계약을 유지함
사회·비가역 비용: 초반 1셀 원시 인프라의 BOM을 정수 1개 더 늘려 시작 자본을 고갈시키지 않으며, 재료 비중이 이미 60% 이상인 시설에 의미 없는 재료를 추가하지 않음
기존 대안과의 장단점: 전 시설 WU×2.25·BOM 동일은 재계산된 V27 재료 EWU와 노동밀도 괴리를 만들므로 폐기함. 전 시설 BOM×1.5는 초기 자본과 물류를 과도하게 소모하므로 폐기하고 결정론적 최소 변경 후보를 선택함
지배 전략 방지 조건: 기존 BOM 종류 교체 0, 재료 수량 50% 초과 0, 총투자 오차 2% 초과 0, 같은 시대 대안 대비 비용·시간·공간 동시 우위 0, 철거→재건 차익 0, 승인 없는 SO 변경 0
저장 권위와 실행 명령: BuildingWorkAmountAbility.constructionWorkRequired와 constructionMaterials가 신규 작업의 단일 authored 권위이며 V27BalanceWorkCalculator는 해당 WU를 검증해 그대로 반환함. WorkOrder는 생성 시 requiredWork와 실제 요청 재료를 저장하며 과거 세이브 변환은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: V27_FACILITY_BOUNDED_WU_BOM_NO_CRITICAL, V27_CONSTRUCTION_WU_SINGLE_AUTHORITY, V27_CONSTRUCTION_BOM_150_CAP, V27_TOTAL_INVESTMENT_2_PERCENT, V27_FACILITY_DISMANTLE_REBUILD_STRICT_LOSS를 356개 시설과 모든 material row에 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-labor-facility-authority.txt, v27-balance-before-after.csv, v27-balance-recalibration-audit.txt, D03 건설·운반·해체·재건 PlayMode, PhysicalItemLogistics, 256-seed 경제, YAML second-run diff 0, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정. exact approval·ApplyApproved·VerifyApplied·해체 SCC·물리 건설 PlayMode·256-seed·3-seed 실전 재검증 전에는 공식 검증·시뮬레이션 검증·실전 보정·완료로 보고하지 않음
```

## V27 연구 기반 던전 공간 확장 교정 기록 (2026-08-17)

```text
정의 ID: balance:v27:research-gated-dungeon-expansion
콘텐츠 종류: 기존 27열 던전을 35/49/63열로 단계 확장하는 신규 연구 프로젝트 3개와 오른쪽 방향 공간 출판·저장 권위
정의·카탈로그·실행기 위치: ResearchProjectSO research:dungeon-expansion:basic-sector/supported-sector/deep-sector, ResearchProjectAssetBuilder, DungeonSpaceExpansionCatalog, DungeonSpaceExpansionRuntime, GridSystemProvider, ModularFacilityWorldSaveService V5, DungeonAggregateReferencePreflight
등장 시대와 연구: 기초 구역 굴착(채석장+기본 숙소, Basic 연구시설), 지보 구역 공학(기초 확장+석재 가공+철제 가공, Basic+Design), 심층 구역 확장(지보 확장+심부 채굴+공장 공학/동력 공구, Basic+Design+Advanced)의 순차 선행을 요구함. 인구 6→12/12→18/18→24 구간의 권장 시점이며 인구 도달만으로 자동 해금하지 않음
플레이어에게 주는 새 결정: 연구 노동과 연구시설·선행 기술을 지불해 생활·생존·창고·산업 포트폴리오의 30% 공간 여유를 확보할지, 현재 27/35/49열 안에서 더 밀집 운영할지 선택함. 개발자 E키 GridExpand는 연구 보상·정식 명령·저장 권위로 사용하지 않음
물리 BOM·입력·출력: 확장 자체의 별도 물리 BOM·무료 자원 생성·가상 재고는 0이며 출력은 오른쪽 DungeonInterior 셀뿐임. 시작 27열×3=81셀, 1단계 35열(+8열=+24셀), 2단계 49열(+14열=+42셀), 3단계 63열(+14열=+42셀)이고 입구·기존 좌표·시설·아이템 점유자는 보존함
직접 작업량과 계산 근거: V23 기간 권위 252/560/960 WU에 After=Ceil(Before×45/99)을 적용해 115/255/437 WU로 배정함. 신규 3개 합계는 Before 1,772→After 807 WU이며 전체 연구 카탈로그는 180→183개, 총 requiredWork는 138,824→140,596 Before 및 63,173→63,980 After로 확장됨
EWU와 목표 회수 기간: 확장 연구 WU는 연구원 기회비용 입력으로 Ceil하며 공간 셀 자체를 판매·철거·회수 가능한 EWU 자산으로 만들지 않음. 115/255/437 WU는 성인 1명 effective 45 기준 약 2.56/5.67/9.71일이고 선행 연구·연구시설 구축·생존 노동은 별도 실비로 남음
공간·전력·물·연료·정비: 인구 1/3/6은 27열, 12명은 35열, 18명은 49열, 24명은 63열 포트폴리오를 사용함. 현재 결정론적 수용력 결과는 사용 셀 25/27/42/69/97/127, headroom 69.1/66.6/48.1/34.2/34.0/32.8%, 정상/단일 장애 공유 접근칸 최고 이용률 44.0/61.6%로 30%·70%·90% 상한 안임. 확장은 전력·용수·연료·정비를 무료 제공하지 않음
위험·실패·회복 방식: 선행 확장 없이 후기 연구 완료, 목표 셀 점유, 비연속 interior, 입구 복수·소실, 96 전체 폭 초과, 높이 3 불일치, 연구 완료 상태와 저장 폭 불일치, 그리드 publication 경쟁은 typed failure로 중단함. 실패 후보는 독립 GridCell 복사본에서 폐기해 기존 그리드·점유자를 부분 변이하지 않음
사회·비가역 비용: 확장 연구 동안 연구자가 생존·의료·경비·생산에서 이탈하며 확장 뒤에도 새 시설 BOM·건설 WU·유틸리티·청소·운반 노동은 모두 지불함. 인구 6/12/18 도달이 무료 공간을 지급하지 않고 연구 우선순위의 기회비용을 유지함
기존 대안과의 장단점: 고정 +2열은 12/18/24명 시설·저장·통로·overflow의 30% 여유를 충족하지 못해 기각함. 27→33→47→61 최소폭은 seed 경계 여유가 작아 각각 35/49/63을 선택함. 시작부터 63열 제공은 공간 연구와 밀집 운영 결정을 제거해 기각하고, E키 개발자 확장을 정식 기능으로 재사용하는 안도 기각함
지배 전략 방지 조건: 연구 우회 0, 단계 건너뛰기 0, 동일 연구 반복 확장 0, 왼쪽 좌표·입구 이동 0, 기존 시설·재고 삭제 0, 확장 셀 무료 시설·유틸리티·자원 생성 0, E키 production 호출 0, 30% headroom 미달 0, 현재 폭보다 작은 저장 복원 0
저장 권위와 실행 명령: ResearchProjectSO와 기존 Research save completedProjectIds가 완료 권위이고 live Grid가 현재 공간 권위임. BlueprintResearchRuntime의 BlueprintResearchCompletedEvent를 DungeonSpaceExpansionRuntime만 소비해 IGridSystemPublisher로 출판함. ModularFacilityWorldSaveData V5가 exact width/height/area/terrain 셀을 저장하며 별도 mutable expansionTier를 중복 저장하지 않음. 과거 V4 이하 세이브 마이그레이션은 명시적으로 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: EXPANSION_RESEARCH_ASSETS_EXACT, EXPANSION_EVENT_27_35_49_63_EXACT, EXPANSION_GRID_COPY_ATOMIC_AND_OCCUPANTS_PRESERVED, EXPANSION_SAVE_V5_LAYOUT_ROUNDTRIP_EXACT, EXPANSION_E_KEY_DEVELOPER_ONLY, RESEARCH_183_PROJECT_CATALOG_EXACT, V27_LAYOUT_ALL_1536_CASES_PASS를 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-expansion-editmode.txt, v27-balance-layout-256-seed.txt, v27-balance-stage-portfolios.csv, ResearchTreeDebugScenarios, ResearchTreePlayModeVerifier, ModularFacility save/load scenarios, GameplayScene production research completion PlayMode, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정. 3개 SO·카탈로그·정적 확장·V5 JSON 레이아웃·1536 배치 사례는 구현 대상으로 고정했으나 production research completion PlayMode, 전체 저장 왕복, 전수 V27 원장·manifest 재생성, YAML no-op, 최종 Console 0/0이 모두 통과하기 전에는 공식 검증·시뮬레이션 검증·실전 보정 완료로 승격하지 않음. 이 기록은 앞선 180개 연구 불변 기록을 183개 카탈로그로 명시적으로 후속 교정함
```

## V27 연구 기반 던전 공간 확장 검증 완료 후속 기록 (2026-08-17)

```text
정의 ID: balance:v27:research-gated-dungeon-expansion-validated
콘텐츠 종류: balance:v27:research-gated-dungeon-expansion의 수치 변경 없는 production·저장·공간·실전 증거 종결 기록
정의·카탈로그·실행기 위치: ResearchProjectSO 3개, ResearchProjectAssetBuilder, DungeonSpaceExpansionCatalog, DungeonSpaceExpansionRuntime, ModularFacilityWorldSaveService V5, DungeonAggregateReferencePreflight, V27PopulationCapacityDebugScenarios
등장 시대와 연구: 기초 구역 굴착 115 WU(채석장+기본 숙소), 지보 구역 공학 255 WU(기초 확장+석재 가공+철제 가공), 심층 구역 확장 437 WU(지보 확장+심부 채굴+공장 공학/동력 공구)의 순차 구조를 최종 유지함. 인구 12/18/24는 수용력 검증 시점일 뿐 자동 해금 조건이 아님
플레이어에게 주는 새 결정: 연구 기회비용을 지불해 35/49/63열을 해금하거나 현재 폭에서 밀집 운영하는 선택을 유지함. 개발자 E키는 production 연구·보상·저장 명령에서 계속 제외됨
물리 BOM·입력·출력: 신규 연구 또는 최종 검증 과정에서 물리 BOM·무료 자원·시설·유틸리티 추가 0. 오른쪽 interior 출력은 +8/+14/+14열, 3행 기준 +24/+42/+42셀로 확정함
직접 작업량과 계산 근거: 승인된 115/255/437 WU와 50 actual/45 effective 권위를 변경하지 않음. 최종 3-seed actual/effective 평균은 51.608844/47.587889 WU·성인^-1·일^-1로 목표를 충족함
EWU와 목표 회수 기간: 확장 셀의 판매·회수 EWU는 계속 0이며 연구 노동만 Ceil 입력 비용으로 유지함. 전수 원장 84,065행의 SCC 313개가 모두 음수이고 최저 margin은 -14,364,087 mEWU임
공간·전력·물·연료·정비: 1/3/6/12/18/24명 × 256 seed의 1,536배치를 모두 통과함. 최소 headroom 32.8%, 정상/단일 장애 공유 접근칸 최대 이용률 44.0%/61.6%, heuristic false-negative 0이며 전력·물·연료·정비 무료 공급은 0임
위험·실패·회복 방식: 연구 단계 건너뛰기·중복 완료·점유 셀 충돌·입구 손실·비연속 interior·잘못된 저장 폭을 fail-loud로 거부하고 후보 그리드를 원자 폐기함. EditMode와 production PlayMode에서 연구 3회·publication 3회 exact를 확인함
사회·비가역 비용: 연구자 이탈과 확장 후 시설·청소·운반·유틸리티 비용을 그대로 부담함. 확장 연구가 6명 초기 생존 자본이나 N+1 시설을 자동 소비하지 않음
기존 대안과의 장단점: +2열 고정·27→33→47→61 최소폭·시작 63열·E키 정식 편입은 각각 여유 부족·seed 경계 취약·진행 삭제·개발 경로 혼입 때문에 최종 기각 상태를 유지함
지배 전략 방지 조건: 연구 우회·반복 무료 확장·자원 생성·좌표 이동·점유자 삭제·30% headroom 미달·저장 축소·E키 production 호출이 모두 0임. producer/consumer orphan과 approved-unapplied도 모두 0임
저장 권위와 실행 명령: completedProjectIds가 연구 완료, live Grid가 현재 공간, ModularFacilityWorldSaveData V5가 exact 레이아웃의 단일 저장 권위임. 파생 expansion tier는 별도 저장하지 않으며 과거 V4 이하 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: 확장 EditMode 5 marker, PlayMode 5 marker, 1,536-layout exact marker, Research 183 프로젝트, V27 whole-game coverage, portable artifact verifier와 Final Acceptance 33/33에 포함됨
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-expansion-editmode.txt, v27-balance-expansion-playmode.txt, v27-balance-layout-256-seed.txt, v27-balance-whole-game-coverage.txt, v27-balance-recalibration-audit.txt, phase157 daily seed 3개, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 실전 보정. 연구 에셋·production event·V5 저장·1,536 공간 배치·전수 원장·3-seed 실측·결정론적 2회차 무변경·portable 검증·Console 0/0이 동일 current-source digest에서 모두 통과함
```

## V27 서비스 연속성·실제 공간·Floor Clutter·RNG 통합 검증 종결 기록 (2026-08-18)

```text
정의 ID: balance:v27:service-spatial-clutter-rng-validation-v1
콘텐츠 종류: 인구별 음식·물 폐쇄 루프, 저자본 N+1, 실제 BuildingSO 공간 수용력, 공유 접근칸, 동적 바닥 재고, actor별 RNG 격리와 4-arm counterfactual의 통합 검증
정의·카탈로그·실행기 위치: PopulationStagePortfolioCatalog, SurvivalContinuityCatalogQuery, V27SixAdultSurvivalLoopDebugScenarios, V27ServiceContinuityEvidenceDebugScenarios, V27AssetBackedSpatialCapacityDebugScenarios, V27PairedClutterPlayModeVerifier, WorldItemHaulPlanningService, RandomStreamProvider, DungeonSpaceExpansionRuntime
등장 시대와 연구: 시작 인구 1/3/6명은 27열을 유지하고, 인구 12/18/24 포트폴리오의 실제 최소 필요 폭 35/45/57열을 검증함. 정식 확장은 기존 기초 구역 굴착 35열, 지보 구역 공학 49열, 심층 구역 확장 63열 연구 완료로 발생하며 개발자 E키는 production 경로에서 제외함
플레이어에게 주는 새 결정: 6명 초기에 동일 조리대·펌프를 두 개 강제하지 않고 실제 물리 식사·물과 원시 행동으로 하루 장애를 버티거나, 이후 자본 여유에 따라 영구 중복 시설을 선택함. 연구 확장도 검증된 최소 폭보다 0/4/6열 여유를 가진 35/49/63열을 확보할지 현재 폭에서 밀집 운영할지 선택함
물리 BOM·입력·출력: 6명 음식은 300 nutrition/일 소비, gross 375(125%), net 330(110%), 7일 비축 2,100 nutrition을 권위로 함. grain 60·즉시 식사 12·물 59·저장 4의 실제 물리 비축을 요구하고 field meal·safe drink·bucket wash는 각각 물리 입력 1개를 소비함. 무료 생성·가상 비축·통로 순간이동은 0임
직접 작업량과 계산 근거: 6명 effective 270 WU/일 중 반복 생존·생산 노동은 78.538 WU/일(29.1%)로 25–35% 범위이며 성장·비상 여유를 보존함. 전수 원장 84,065행은 actual 50/effective 45, 입력 Ceil·출력 Floor, SCC 313개 tolerance 0을 유지하고 최소 순환 margin은 -14,364,087 mEWU임
EWU와 목표 회수 기간: N+1 원시 경로의 물리 입력·시간·기분·위생·오염 비용을 모두 debit으로 포함하며 동일 시설 중복 BOM을 무료 안전으로 간주하지 않음. 시설 356종 철거→재건 최대 margin은 -69,746 mEWU이고 시장 구매는 Ceil, 판매는 RecoverableValue Floor로 유지함
공간·전력·물·연료·정비: 실제 BuildingSO와 BuildingPlacementValidator로 1/3/6/12/18/24명×256 seed=1,536배치를 모두 통과함. 실제 최소 폭은 27/27/27/35/45/57, authored 폭은 27/27/27/35/49/63, 최소 headroom은 30.3%, 정상/단일 장애 공유셀 최대 이용률은 60.0%/84.0%임. 저장·overflow·고정 셀·공유 접근 합집합을 면적에 포함하고 전력·상수·하수·연료·정비를 면제하지 않음
위험·실패·회복 방식: 주 시설 하루 장애, 예약·입력·접근·no-path·창고 포화·운반자 장애를 typed failure로 추적함. 32-seed 4-arm의 512개 window에서 access/egress clutter 0, recovery 후 persistent clutter 0, 물리 burst 수량 보존, 외생 사건 일치, RNG causal-cone 밖 cross-talk 0을 요구함. clutter wait delta는 median 0%, p95 0%로 10% 상한을 통과했지만 단일 최악 seed는 79.1%였으며 raw seed 행을 접거나 삭제하지 않고 보존함
사회·비가역 비용: primitive fallback은 field meal 시간, floor rest의 위생 -4·기분 -3, bucket wash의 물 1, latrine의 위생 -8·Waste 8·Stain 2·기분 -2를 그대로 부담함. 운반 장애와 clutter의 대기·재계획·StepAside 비용은 growth 노동으로 전환하지 않고 window별 mWU에 귀속함
기존 대안과의 장단점: Tier 0에서 조리대·펌프 2개를 고정하면 초기 BOM·건설 WU를 고갈시키므로 실제 primitive 경로를 N+1로 인정함. 접근칸을 단순 합산하면 공간 거짓 실패가 생겨 walkable 공유 합집합을 사용하되 동시 유일 접근칸은 공유 금지함. clean/fault 2-arm 프레임 비교는 RNG 나비효과를 분리하지 못해 cleanRepeatA/B·faultControl·clutterStress 4-arm과 game-time window를 사용함
지배 전략 방지 조건: 정상 시설 사용 가능 시 primitive 우선 사용 0, 물리 입력 없는 fallback 0, 중복 시설 무료 보상 0, 본체 overlap 0, egress·계단·유일 의료 접근 clutter 0, containment 밖 persistent Loose 0, actor 간 decision RNG cross-talk 0, decision→movement RNG cross-talk 0, 인구만으로 무료 확장 0, E키 production 확장 0임
저장 권위와 실행 명령: 아이템·예약·주문·캐릭터·random stream 전체 상태는 기존 current save authority를 사용하고 actor별 stream은 persistentCharacterId로 파생함. 연구 completedProjectIds와 live Grid/ModularFacilityWorldSaveData V5가 확장 권위이며 별도 population expansion flag를 저장하지 않음. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: balance:v27:service-continuity-nplusone, primitive-fallback-capital-relief, shared-access-spatial-union, floor-clutter-runtime-capacity, storage-overflow-containment, counterfactual-rng-isolation, paired-run-window-attribution, population-stage-capacity, research-gated-dungeon-expansion, six-adult-food-water-closed-loop을 V27 manifest에 포함함. 공간 보고서는 검증기·공간 규칙·모든 BuildingSO sourceDigest와 1,536행 CSV SHA-256에 결합함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-service-continuity-playmode.txt, v27-balance-six-adult-food-water-loop.txt, v27-balance-spatial-capacity.csv, v27-balance-shared-cell-congestion.txt, v27-balance-expansion-tiers.txt, v27-balance-paired-run-rng.txt/.csv, v27-balance-floor-clutter.csv, v27-balance-random-stream-manifest.txt, v27-balance-whole-game-coverage.txt, v27-balance-artifact-manifest.json, physical-item-logistics-playmode-report.txt
현재 밸런스 상태: 밸런스 실전 보정 PASS. 10개 서비스 경로의 production actor 실행, 6명 폐쇄 루프, 실제 에셋 1,536배치, 32-seed 4-arm clutter/RNG, 연구 27→35→49→63 production PlayMode, 전수 원장·SCC·가격·적용 권위, 5일 3-seed actual 51.608844/effective 47.587889, 핵심 artifact 두 번째 생성 SHA-256 무변경을 현재 소스에서 통과함. 단일 clutter 최악 seed 79.1%는 p95 기준 밖의 관찰값으로 원장에 계속 공개하며 향후 회귀에서 p95 10%를 넘으면 즉시 실패함
```

## V27 전 시설 포트폴리오 반영 던전 확장 폭 후속 교정 기록 (2026-08-18)

```text
정의 ID: balance:v27:full-portfolio-dungeon-expansion-widths-v2
콘텐츠 종류: 농업·축산·발전·물 저장·폐수 처리까지 포함한 실제 BuildingSO 포트폴리오의 연구 기반 던전 목표 폭 교정
정의·카탈로그·실행기 위치: V27AssetBackedSpatialCapacityDebugScenarios, V27SixAdultSurvivalLoopDebugScenarios, BuildingSO, BuildingPlacementValidator, DungeonSpaceExpansionCatalog, DungeonSpaceExpansionRuntime, ModularFacilityWorldSaveService V5
등장 시대와 연구: 시작 1/3/6명은 27열을 유지함. 12명은 기초 구역 굴착 115 WU 완료 시 47열, 18명은 지보 구역 공학 255 WU 완료 시 61열, 24명은 심층 구역 확장 437 WU 완료 시 75열을 목표로 함. 인구는 검증 포트폴리오 시점일 뿐 자동 확장 트리거가 아니며 각 전용 확장 연구 완료 이벤트만 production 트리거임
플레이어에게 주는 새 결정: 기존 35/49/63열의 표면상 여유 대신 작물 플롯·동물 우리·발전·물 저장·폐수 처리·창고·overflow·공용 접근칸까지 실제 설치한 뒤에도 30% 공간 여유를 확보함. 연구 우선순위와 밀집 운영 선택은 유지하고 개발자 E키는 정식 진행에서 계속 제외함
물리 BOM·입력·출력: 연구·시설 BOM·레시피·소비량은 변경하지 않음. 확장 출력만 27→47(+20열, 3행 기준 +60셀)→61(+14열, +42셀)→75(+14열, +42셀)로 교정하며 시설·아이템·입구·기존 좌표를 보존함
직접 작업량과 계산 근거: 115/255/437 연구 WU와 50 actual/45 effective 노동 권위는 불변임. 실제 생존 폐쇄 루프가 요구하는 crop plot 1/2/3/6/9/11개와 인구 단계별 8/15/21/38/52/64개 시설 요구를 256개 결정론적 건설 순서마다 배치해 최소 폭 27/27/27/47/61/75를 산출함
EWU와 목표 회수 기간: 공간 셀은 판매·해체·회수 EWU가 없고 연구 WU만 Ceil 입력으로 남음. 시설 BOM·건설 WU·철거 회수·시장 가격·SCC 잠재값은 이번 폭 교정에서 불변이며 확장으로 무료 자원·무료 시설·가상 저장을 생성하지 않음
공간·전력·물·연료·정비: 1/3/6/12/18/24명×256 seed=1,536건 모두 통과함. 단계별 최소 headroom은 74.0/54.3/34.5/32.6/31.1/31.5%, 정상·단일 장애 공유셀 최대 이용률은 최종 60.0/84.0%임. crop plot, animal-care, power-generation, water-storage, wastewater-treatment, storage/overflow와 고정 셀을 모두 면적에 포함함
위험·실패·회복 방식: 필요 폭보다 작은 authored 목표, headroom 30% 미달, 정상 70%·단일 장애 90% 이용률 초과, overflow 누락, 불법 본체 중첩, 접근칸 차단, 96 전체 폭 초과, 단계 건너뛰기는 fail-loud함. 목표 전체 grid 폭은 시작 X=17 기준 64/78/92로 96 상한 안이며 실패 후보는 live Grid를 부분 변이하지 않음
사회·비가역 비용: 넓어진 공간도 시설 BOM·건설 WU·유틸리티·청소·운반·수리 노동을 대신 지불하지 않음. 6명 초기에는 추가 연구나 중복 조리대·펌프를 강제하지 않고 27열과 primitive N+1을 유지함
기존 대안과의 장단점: 35/49/63은 축산·발전·물 저장·폐수 처리와 실제 작물 플롯을 누락한 대표 시설 subset에서만 통과해 폐기함. 고정 +2열은 용량 근거가 없고, 시작부터 75열은 연구·밀집 운영 결정을 삭제하므로 기각함. 47/61/75는 현재 전수 포트폴리오의 최소 256-seed 통과 폭이며 추가 시설 권위가 생기면 Solver가 더 큰 폭을 요구할 수 있음
지배 전략 방지 조건: 인구 자동 확장 0, E키 production 호출 0, 무료 BOM·유틸리티·저장 0, 시설 subset 누락 0, 본체 overlap 0, 30% headroom 미달 0, 연구 반복 무료 확장 0, 저장 축소 0, 기존 좌표·점유자 삭제 0
저장 권위와 실행 명령: Research completedProjectIds가 연구 완료, live Grid가 현재 공간, ModularFacilityWorldSaveData V5가 exact 셀 레이아웃의 단일 저장 권위임. 별도 expansion-tier 저장 DTO를 추가하지 않고 BlueprintResearchCompletedEvent→DungeonSpaceExpansionRuntime→IGridSystemPublisher 경로만 사용함. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: EXPANSION_EVENT_27_47_61_75_EXACT, EXPANSION_LIVE_RESEARCH_BASIC_27_TO_47, EXPANSION_LIVE_RESEARCH_SUPPORTED_47_TO_61, EXPANSION_LIVE_RESEARCH_DEEP_61_TO_75, V27_ASSET_BACKED_SPATIAL_ALL_1536, V27_STAGE_PORTFOLIO_ALL_PASS를 manifest 필수 증거로 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-expansion-editmode.txt, v27-balance-expansion-playmode.txt, v27-balance-spatial-capacity.csv, v27-balance-expansion-tiers.txt, v27-balance-stage-portfolios.csv, v27-balance-shared-cell-congestion.txt, v27-balance-whole-game-coverage.txt, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 진행 중. 전 시설 1,536배치와 확장 EditMode는 현재 소스에서 PASS했으나 27→47→61→75 production PlayMode, V5 저장 왕복, 실제 6인 동시 서비스 장애, 최종 manifest·전수 감사·3-seed·결정론적 두 번째 실행까지 통과하기 전에는 시뮬레이션 검증·실전 보정·완료로 승격하지 않음
```

## V27 물리 비축·장애 저장 포함 던전 확장 폭 후속 교정 기록 (2026-08-18)

```text
정의 ID: balance:v27:storage-bounded-dungeon-expansion-widths-v3
콘텐츠 종류: 7일 생존 비축·정상 저장 70%·단일 장애 저장+overflow 90%를 실제 시설 포트폴리오 공간에 포함한 연구 기반 던전 목표 폭 후속 교정
정의·카탈로그·실행기 위치: V27AssetBackedSpatialCapacityDebugScenarios, V27PopulationStageSpatialBaseline, V27PairedClutterPlayModeVerifier, BuildingSO, BuildingPlacementValidator, DungeonSpaceExpansionCatalog, DungeonSpaceExpansionRuntime, ModularFacilityWorldSaveService V5
등장 시대와 연구: 시작 1/3/6명은 27열을 유지함. 12명은 기초 구역 굴착 115 WU 완료 시 49열, 18명은 지보 구역 공학 255 WU 완료 시 65열, 24명은 심층 구역 확장 437 WU 완료 시 81열을 해금함. 인구는 자동 해금 조건이 아니며 각 전용 확장 연구의 production 완료 사건만 공간 출판을 요청함
플레이어에게 주는 새 결정: 농업·축산·유틸리티 시설뿐 아니라 7일 비축과 운반자 장애 중 overflow까지 실제 셀과 물리 저장 용량으로 지불한 뒤에도 30% 자유 공간을 남길지, 연구를 미루고 현재 폭에서 밀집 운영할지 선택함. 개발자 E키의 2×2 확장은 developerMode에서만 허용하며 진행·연구·저장 권위가 아님
물리 BOM·입력·출력: 연구·시설·아이템 BOM, 음식·물 소비, 시설 처리량은 변경하지 않음. 공간 출력만 27→49(+22열, 3행 기준 +66셀)→65(+16열, +48셀)→81(+16열, +48셀)로 교정함. 실제 전체 Grid 폭은 기존 60→66→82→98이고 왼쪽 고정 구역·입구·기존 좌표·시설·아이템을 보존함
직접 작업량과 계산 근거: 연구 WU는 승인된 115/255/437과 actual 50/effective 45를 유지함. 인구 1/3/6/12/18/24의 256개 건설 순서마다 7일 grain·즉시 식사·식수, 정상 cycle 재고, 최대 batch, full-carry 취소와 장애 중 유입량을 포함해 27/27/27/49/65/81을 산출함
EWU와 목표 회수 기간: 추가 공간 셀은 판매·철거·회수 가능한 EWU가 없고 연구 노동만 입력 Ceil로 남음. 시설 BOM·건설 WU·시장 가격·회수 Floor·SCC potential은 이번 폭 교정으로 변하지 않으며 무료 자원·가상 저장·통로 순간이동은 0임
공간·전력·물·연료·정비: 1,536/1,536 실제 BuildingSO 배치가 통과함. 단계별 worst used/headroom은 21/74.0%, 37/54.3%, 56/30.8%, 101/31.2%, 135/30.7%, 166/31.6%임. 정상 저장 최고는 46.7/55.9/54.6/61.0/64.5/65.4%, 장애 저장+overflow 최고는 76.3/72.9/52.9/46.0/47.4/47.6%, 공유 접근 정상/장애 최고는 60.0/84.0%임. 전력·상수·하수·연료·정비 면제는 없음
위험·실패·회복 방식: headroom 30% 미달, 정상 저장 70% 초과, 장애 저장+overflow 90% 초과, overflow 누락, 불법 본체 중첩, 유일 접근·egress 침범, 연구 단계 건너뛰기, 목표 전체 Grid 폭 104 초과를 fail-loud함. 실패한 후보 Grid는 출판하지 않고 기존 world와 점유자를 보존함
사회·비가역 비용: 확장 연구자 이탈, 신규 시설 BOM·건설·청소·운반·유틸리티·수리 노동을 모두 그대로 부담함. 6명 초기에는 확장 연구와 동일 조리대·펌프 중복을 강제하지 않고 27열·물리 비축·primitive N+1을 유지함
기존 대안과의 장단점: 47/61/75는 시설 본체와 접근칸은 수용했지만 7일 비축을 정상 70% 이하로 보관하는 실제 창고 수와 장애 overflow를 모두 포함한 현재 전수 조건에는 부족해 폐기함. 49/65/81은 각 단계의 2열 단위 첫 통과 폭이며, 추가 콘텐츠·저장 권위가 생기면 Solver가 더 큰 폭을 요구하도록 고정 상수보다 검증 결과를 우선함
지배 전략 방지 조건: 인구 자동 확장 0, E키 production 호출 0, 무료 BOM·시설·유틸리티·가상 저장 0, 통로 overflow 0, 저장 점유율 은폐 0, 본체 overlap 0, 30% headroom 미달 0, 연구 반복 무료 확장 0, 기존 좌표·점유자 삭제 0임
저장 권위와 실행 명령: Research completedProjectIds가 완료 권위, live Grid가 공간 권위, ModularFacilityWorldSaveData V5가 exact width/height/area/terrain 권위임. BlueprintResearchCompletedEvent→DungeonSpaceExpansionRuntime→IGridSystemPublisher 단일 경로만 사용하고 별도 population expansion flag나 과거 세이브 마이그레이션을 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: EXPANSION_EVENT_27_49_65_81_EXACT, EXPANSION_LIVE_RESEARCH_BASIC_27_TO_49, EXPANSION_LIVE_RESEARCH_SUPPORTED_49_TO_65, EXPANSION_LIVE_RESEARCH_DEEP_65_TO_81, V27_ASSET_BACKED_SPATIAL_ALL_1536, V27_STORAGE_NORMAL_FAULT_ALL_STAGES, PAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT를 manifest 필수 증거로 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-expansion-editmode.txt, v27-balance-expansion-playmode.txt, v27-balance-spatial-capacity.csv, v27-balance-expansion-tiers.txt, v27-balance-shared-cell-congestion.txt, Temp/v27-balance-paired-clutter-focused.txt, v27-balance-whole-game-coverage.txt, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 시뮬레이션 검증 진행 중. 현재 소스에서 1,536 asset placement, 27→49→65→81 EditMode·production PlayMode, focused production 4-arm의 runtime headroom 30.7%가 통과했으나 full 32-seed, exact/beam oracle, 전수 manifest·원장·3-seed·결정론적 두 번째 실행을 모두 재완료하기 전에는 실전 보정 또는 전체 완료로 승격하지 않음
```

## V27 기존 채굴 연구 던전 확장 권위 후속 교정 기록 (2026-08-18)

```text
정의 ID: balance:v27:existing-mining-research-dungeon-expansion-authority-v4
콘텐츠 종류: 별도 확장 연구 3개를 제거하고 기존 채석장·석재 가공·심부 채굴 완료 사건에 27→49→65→81열 공간 출판을 결합하는 권위 교정
정의·카탈로그·실행기 위치: ResearchProjectSO research:mining:quarry/stonecutting/deep, ResearchProjectAssetBuilder, DungeonSpaceExpansionCatalog, DungeonSpaceExpansionRuntime, ModularFacilityWorldSaveService V5, DungeonAggregateReferencePreflight
등장 시대와 연구: 인구 12명 수용 폭 49열은 research:mining:quarry, 18명 수용 폭 65열은 research:mining:stonecutting, 24명 수용 폭 81열은 research:mining:deep 완료로 해금함. 인구는 검증 시점일 뿐 자동 확장 조건이 아니고 개발자 E키도 production 진행 권위가 아님
플레이어에게 주는 새 결정: 기존 채굴 기술에 공간 확장 보상을 함께 부여하므로 같은 목적의 별도 연구를 다시 지불하지 않음. 채굴 연구를 미루고 현재 공간을 밀집 운영할지, 연구해 저장·농업·축산·유틸리티를 포함한 30% 여유 공간을 확보할지 선택함
물리 BOM·입력·출력: 확장 전용 BOM과 신규 연구 에셋은 0. 기존 채굴 연구의 물리 입력·해금은 유지하며 출력은 오른쪽 DungeonInterior 27→49(+22열)→65(+16열)→81(+16열)뿐임. 입구·기존 좌표·시설·아이템 점유자를 보존함
직접 작업량과 계산 근거: 별도 확장 연구의 Before 252/560/960 WU 및 After 115/255/437 WU를 폐기함. 기존 채석장·석재 가공·심부 채굴의 authored After 28/42/60 WU만 유지하며 연구 카탈로그는 183→180개, 총 FacilityThresholdWork는 140,596→138,824, 총 requiredWork는 63,980→63,173으로 복귀함
EWU와 목표 회수 기간: 공간 셀의 판매·철거·회수 EWU는 0이고 기존 채굴 연구 노동만 입력 Ceil 비용으로 남음. 삭제한 별도 연구의 807 WU를 다른 가격·보상·BOM으로 이전하지 않으며 전수 SCC는 tolerance 0으로 재검증함
공간·전력·물·연료·정비: 1/3/6명은 27열, 12/18/24명은 49/65/81열을 사용함. 실제 BuildingSO·7일 비축·overflow·공유 접근칸을 포함한 1,536배치의 30% headroom 요구를 폭 권위로 유지하고 전력·상수·하수·연료·정비는 무료 제공하지 않음
위험·실패·회복 방식: 연구 완료 이벤트 순서가 저장·복원 또는 도구 실행 때문에 바뀌어도 가장 높은 완료 연구가 요구하는 폭까지 한 번에 확장하고, 뒤늦은 낮은 단계 완료는 no-op으로 처리함. 목표 셀 점유·입구 손실·비연속 interior·104 전체 폭 초과·publication 경쟁은 fail-loud함
사회·비가역 비용: 확장 전용 중복 연구 노동을 제거하되 기존 채굴 연구자 이탈, 확장 후 시설 BOM·건설·청소·운반·유틸리티·수리 노동은 그대로 부담함. 6명 초기 자본과 N+1 비축을 확장 연구가 소비하지 않음
기존 대안과의 장단점: 별도 research:dungeon-expansion:* 3개는 기존 채굴 연구와 동일한 진행을 이중 과금해 폐기함. 고정 +2열, 시작부터 81열, 인구 자동 확장, E키 정식 편입도 각각 수용력 근거 부족·진행 삭제·개발 경로 혼입 때문에 기각함
지배 전략 방지 조건: 별도 연구 WU 이중 과금 0, 인구 자동 확장 0, E키 production 호출 0, 동일 연구 반복 무료 확장 0, 무료 BOM·시설·유틸리티·가상 저장 0, 기존 좌표·점유자 삭제 0, 30% headroom 미달 0
저장 권위와 실행 명령: 기존 Research completedProjectIds가 완료 권위, live Grid가 현재 공간 권위, ModularFacilityWorldSaveData V5가 exact width/height/area/terrain 권위임. 별도 expansionTier DTO와 과거 세이브 마이그레이션은 추가하지 않으며 가장 높은 완료 채굴 연구와 저장 폭의 일치만 검증함
자동 감사 ID와 전수 목록 포함 여부: EXPANSION_RESEARCH_ASSETS_EXACT, EXPANSION_EVENT_27_49_65_81_EXACT, EXPANSION_OUT_OF_ORDER_DEEP_IDEMPOTENT, EXPANSION_SAVE_RESEARCH_LAYOUT_AUTHORITY_EXACT, EXPANSION_LIVE_RESEARCH_QUARRY_27_TO_49, EXPANSION_LIVE_RESEARCH_STONECUTTING_49_TO_65, EXPANSION_LIVE_RESEARCH_DEEP_MINING_65_TO_81, V27_ASSET_BACKED_SPATIAL_ALL_1536을 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-balance-expansion-editmode.txt, v27-balance-expansion-playmode.txt, v27-balance-layout-256-seed.txt, v27-balance-expansion-tiers.txt, v27-balance-before-after.csv, v27-balance-recalibration-audit.txt, 3-seed 실측, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 진행 중. 소스·에셋 정합화 후 Unity compile, EditMode·production PlayMode, 저장 preflight, 1,536배치, 전수 원장·SCC, 32-seed 4-arm, 3-seed 실전, 결정론적 두 번째 실행이 모두 fresh PASS하기 전에는 시뮬레이션 검증·실전 보정·완료로 승격하지 않음
```

## V27 채집·수확 출력 containment 포화 계약 후속 기록 (2026-08-19)

```text
정의 ID: balance:v27:resource-output-containment-saturation-v1
콘텐츠 종류: 기존 채굴·벌목·채집·작물 수확의 물리 출력 공간 admission과 포화 회복 계약
정의·카탈로그·실행기 위치: ProductionItemGateway.CanSpawnOutput, WorldResourceOutputPortAdapter, WorldResourceRuntime.TryGetWork/ApplyWork, CropPlotRuntime.TryGetWork/ApplyWork, WorldResourceDebugScenarios
등장 시대와 연구: 기존 자원·작물·레시피의 시대·연구·해금을 그대로 유지하며 신규 아이템·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 채집·수확 위치의 한 물리 출력 묶음을 먼저 운반하거나 소비한 뒤 다음 주기를 진행해야 하며, 창고·물류를 무시한 무한 바닥 적치를 선택할 수 없음
물리 BOM·입력·출력: 기존 output item ID와 authored amount를 그대로 사용함. source position별·item별 unassigned Loose 묶음 하나를 overflow containment로 허용하고, 점유 중 두 번째 묶음 생성·삭제·복제·가상 보관은 0임
직접 작업량과 계산 근거: 레시피 RequiredWork, crop Sow/Harvest WU, 생산량 배율은 변경하지 않음. 포화 중 작업 후보를 ProductionOutputSpaceUnavailable로 닫아 완료 WU·남은 cycle·renewable patch를 소비하지 않으며, 묶음 제거 후 같은 작업이 다시 열림
EWU와 목표 회수 기간: BOM·직접 WU·AcquisitionCost·RecoverableValue·시장 가격·SCC potential은 불변임. 출력이 물리화되지 않은 실패 주기는 EWU credit을 만들지 않고 입력·자원도 debit하지 않으므로 차익과 손실 은폐가 없음
공간·전력·물·연료·정비: source containment 한 묶음은 기존 OverflowContainment/AuthorizedLooseSource 공간 계약 안에서만 허용함. 전력·용수·연료·정비·창고 용량을 추가 제공하지 않으며 통로·접근칸·egress를 overflow로 승격하지 않음
위험·실패·회복 방식: 잘못된 item/0 이하 수량은 ProductionOutputUnavailable, 이미 점유된 source containment는 ProductionOutputSpaceUnavailable로 fail-loud함. 실제 spawn이 admission 뒤 실패하면 조용히 cycle을 reset하지 않고 예외로 중단하며, 포화 해제 뒤 bounded replan으로 회복함
사회·비가역 비용: 운반자 장애·창고 포화 시 채집자가 새 산출을 계속 쌓지 못해 성장 노동이 대기할 수 있으며, 이 비용은 paired-run Wait WU·dispatch latency·replan 지표에 귀속함. 캐릭터·시설·재고를 자동 삭제하지 않음
기존 대안과의 장단점: Loose가 Grid traversal cost를 직접 바꾸지 않는다는 이유로 무제한 source 적치를 허용하는 안은 저장·예약·물류 livelock을 숨겨 기각함. 창고가 없으면 첫 채집조차 금지하는 안도 초기 생존을 막으므로, 물리 한 묶음의 bounded containment 후 포화 차단을 선택함
지배 전략 방지 조건: 출력 실패 후 resource cycle 감소 0, completedWork reset 0, RNG 재굴림 0, 물리 아이템 삭제·복제 0, 두 번째 source batch 0, 통로 fallback drop 0, capacity 해제 전 작업 재개 0
저장 권위와 실행 명령: WorldResource/Crop aggregate가 진행·cycle 권위이고 IWorldItemStackRuntime이 물리 출력 권위임. capacity는 저장하지 않는 파생 query이며 복원 뒤 실제 Loose stack에서 재계산함. 과거 세이브 변환은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY, ProductionOutputSpaceUnavailable, remainingCycles/completedWork unchanged, physical quantity exact, recovery available marker를 WorldResource production-live report와 V27 artifact manifest에 요구함
검증 매트릭스와 보고서 위치: docs/implementation-reports/world-resource-runtime-latest.txt, Artifacts/QA/v27-balance-floor-clutter.csv, v27-balance-paired-run-rng.csv, v27-balance-before-after.csv, v27-balance-recalibration-audit.txt, WorldResource/Crop focused scenarios, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증. Unity compile과 84,387행 전수 원장 Critical 0/SCC 313/무결성 0은 통과했으나 production PlayMode의 typed block·무변이·회복과 최종 current-source no-op/3-seed/Console 0/0 전에는 시뮬레이션 검증·실전 보정·전체 완료로 승격하지 않음
```

## V27 운반 중단 물리 회수 권위 교정 기록 (2026-08-20)

```text
정의 ID: architecture:v27:haul-interruption-physical-recovery-v1
콘텐츠 종류: 기존 AIHaul의 미픽업 예약·픽업 완료 화물 중단 처리를 분리하고 Downed/Dead 화물을 현재 위치의 추적 가능한 물리 Loose로 전환하는 구조 교정
정의·카탈로그·실행기 위치: AIHaul, AbilityHaul, CharacterCarryInventory, ItemTransferService, WorldItemStackRuntime, WorldItemPersistenceService, PhysicalStockQuery, AbilityHunt, CharacterAiCrossActionFaultPlayModeVerifier
등장 시대와 연구: 기존 모든 시대·연구·직업의 운반에 공통 적용하며 신규 연구·아이템·시설·행동 해금은 추가하지 않음
플레이어에게 주는 새 결정: 활동 가능한 운반자의 일시적 행동 전환은 이미 든 화물을 유지한 채 배송 재계획으로 이어지고, 운반자가 Downed 또는 Dead가 되면 화물이 쓰러진 정확한 셀에 남아 구조·전투·물류 복구의 실제 공간 비용을 발생시킴
물리 BOM·입력·출력: 기존 itemId·수량·BOM·stack signature를 변경하지 않음. 미픽업 quantity lease만 해제하고 pickup-committed stack은 인벤토리와 동일한 기존 carriedStackId를 Loose로 재배치하므로 신규 스택 생성·삭제·복제·원래 창고 순간이동은 0임
직접 작업량과 계산 근거: 운반 WU·이동 속도·운반 한도·회수 행동 WU는 변경하지 않음. transient recovery deadline은 drop 시각부터 15 game seconds이며 그 전에는 장애 회수 유예, 이후에는 Floor Clutter persistent 진단 대상으로 계산함
EWU와 목표 회수 기간: AcquisitionCost·RecoverableValue·시장 가격·SCC potential은 불변임. 중단은 물리 수량과 item identity를 그대로 보존하고 free credit/debit을 만들지 않으며 deadline은 진단·회수 SLA로 작동하고 가치 할인이나 소유권 삭제를 수행하지 않음
공간·전력·물·연료·정비: 드롭 셀은 actor.GetNowXY의 exact current cell이고 source storage·destination·인접 fallback을 사용하지 않음. 전력·용수·연료·정비를 추가 제공하지 않으며 critical access/egress에 남은 recovery drop은 deadline 이후 Floor Clutter 실패로 승격함
위험·실패·회복 방식: 활동 가능한 actor는 unpicked lease를 해제하고 carried operation·delivery intent·picked lease를 유지해 Brain 재계획을 요청함. Downed/Dead는 owner operation/source stack/carrier/interruption/drop time/deadline이 모두 유효할 때만 동일 physical record를 Loose로 전환하고 exact 검증 실패 시 인벤토리를 복원한 뒤 Error로 fail-loud함
사회·비가역 비용: 전투·부상으로 운반자가 쓰러지면 물자가 현장에 남아 다른 운반자의 회수 시간과 통로 clutter 위험을 실제로 부담함. 활동 가능한 재계획은 무의미한 source 왕복을 제거하지만 목적지까지의 남은 이동·예약·입고 비용은 면제하지 않음
기존 대안과의 장단점: 모든 중단을 source storage로 논리 반환하는 방식은 물리 순간이동을 만들고, 모든 중단을 바닥 드롭하는 방식은 단순 AI 전환마다 불필요한 clutter를 만드므로 active retain/replan과 Downed/Dead physical drop을 typed disposition으로 분리함
지배 전략 방지 조건: source 순간이동 0, destination 순간이동 0, 새 stack spawn 0, carried quantity 삭제·복제 0, unpicked lease 잔존 0, completed intent 조기 해제 0, recovery metadata 없는 Downed/Dead drop 0, deadline 경과 clutter 은폐 0
저장 권위와 실행 명령: CharacterCarryInventory가 들고 있는 수량, WorldItemStackRecord가 물리 위치·상태, quantity lease와 HaulDeliveryIntent가 운반 소유권의 단일 권위임. Physical Items V9이 recovery provenance와 deadline을 저장·검증하며 과거 V8 이하 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: HAUL_ACTIVE_REPLAN_RETAINS_CARRIED, HAUL_DOWNED_CURRENT_CELL_TRANSIENT_DROP, HAUL_DOWNED_QUANTITY_NO_TELEPORT, HAUL_DEAD_CURRENT_CELL_TRANSIENT_DROP, HAUL_DEAD_QUANTITY_NO_TELEPORT와 기존 haul-source-despawn/shrink/destination-destroy 행을 focused 증거로 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/character-ai-cross-action-fault-playmode.txt, DungeonPhysicalItemSaveData V9 capture/validation, PhysicalStockQuery FloorClutter 진단, Unity Items·Runtime·Editor compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 영향 없음 / 구조·연결 검증 PASS. BOM·WU·EWU·가격·처리량·운반 한도 수치는 불변이고 Unity 정식 Roslyn Items·Runtime·Editor 컴파일, active retain, Downed/Dead current-cell drop, V9 provenance/deadline, 수량 보존·순간이동 방지가 모두 fresh production PlayMode PASS함. 사냥감 파괴 시 전체 경로를 끝까지 걷던 기존 liveness 결함도 한 셀 경계 재검증으로 교정해 Cross-Action 전체 result=PASS, lateCommit=0, Console Warning/Error 0/0을 확인함
```

## V27 물리 중량 재조정 1차 운반 한도 권위 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-carry-capacity-slice-v1
콘텐츠 종류: 전 물리 아이템 운반에 공통 적용되는 캐릭터 nominal 운반 한도·멜빵 배율·접근성 최대 배율의 단일 권위
정의·카탈로그·실행기 위치: CharacterCarryTuning, CharacterCarryInventory, ItemHaulingSettingsSO, ItemHaulingSettingsSnapshot, WorldItemHaulPlanningService, OffenseFieldMobilityService, PhysicalItemDebugScenarios
등장 시대와 연구: nominal 기준은 전 시대 공통이며 운반 멜빵 1.25배는 기존 research:commerce:logistics 해금과 실제 workwear 장착·내구·해제 경로를 그대로 사용함. 신규 연구·아이템·시설은 추가하지 않음
플레이어에게 주는 새 결정: 기본 설정 1.5에서 평범한 주민은 대표 성능 기준 약 19kg까지 무감속, 약 29kg까지 감속 과적을 선택할 수 있고, 멜빵 사용 시 약 24/36kg 밴드를 얻음. 설정의 1.0~2.5 접근성 범위는 유지하되 일반 반복 물류가 과적 구간을 정상 처리량으로 전제해서는 안 됨
물리 BOM·입력·출력: item unitWeight, 레시피 BOM·출력·손실·포장·스택 수량은 이번 slice에서 변경하지 않음. 멜빵 BOM과 물리 중량·내구 소모도 유지하며 nominal 계산 상수만 Before 20kg→After 25kg로 변경함
직접 작업량과 계산 근거: 운반 WU·픽업/입고 시간·이동 경로는 불변임. soft limit=`25kg × performance:survival:haul-capacity × (멜빵이면 1.25)`, hard limit=`soft × 사용자 maxCarryMultiplier`이며 기본 1.5, 범위 1.0~2.5, 과적 이동 곡선 100%→45%를 유지함
EWU와 목표 회수 기간: kg별 handling EWU와 시장 가격은 item kg 전수 적용 전이라 이번 slice에서 재생성하지 않음. 무료 물자·생산량·판매 credit은 생기지 않으며 carry 상향에 따른 trip/handling 변화는 후속 413 item/354 recipe 원장에서 다시 계산해야 함
공간·전력·물·연료·정비: 저장·FacilityBuffer·통로·overflow·전력·용수·연료는 불변임. 한 번에 더 운반해도 창고 용량과 시설 입력 버퍼를 늘리거나 Floor Clutter를 합법화하지 않으며 멜빵 내구는 기존 성공 운반 시 1만 소모함
위험·실패·회복 방식: NaN·Infinity·0 이하 performance는 fail-loud함. 사용자 배율은 기존 1.0~2.5로 canonical clamp하고 Downed/Dead 화물은 current-cell TransientCarryRecoveryDrop 계약을 유지함. 최대 한도를 넘는 수량은 partial pickup으로 제한하고 원본 수량·lease·intent를 보존함
사회·비가역 비용: 동일 화물을 더 적은 왕복으로 옮길 수 있어 물류 노동이 감소할 수 있으나 멜빵 연구·제작·내구와 과적 속도 저하를 유지함. 이 변화가 물류 목표 12~20%를 과도하게 낮추거나 성장 노동을 무료화하는지는 전수 kg 적용 후 3-seed에서 판정함
기존 대안과의 장단점: Before nominal 20kg는 실제 평범한 주민의 soft limit를 약 15kg대로 낮춰 6~11kg recipe와 8~14kg mixed-haul 목표에 여유가 작았음. After 25kg는 실제 Roma에서 19.13/28.69kg를 만들고 멜빵 대표 밴드 23.75/35.625kg를 제공하지만, 설정 2.5에서는 대표 hard 47.5kg까지 허용되므로 접근성 stress로만 취급함
지배 전략 방지 조건: 멜빵 없는 6인 생존 가능, 일반 반복 haul 과적 사용률 5% 이하·목표 0, 20kg 초과 일반 단위 금지, item 생성·복제 0, 멜빵 내구 면제 0, 사용자 2.5 설정을 authored 처리량 기준으로 사용 0, carry 상향으로 storage/overflow/headroom 비용 면제 0을 후속 전수 감사에서 요구함
저장 권위와 실행 명령: CharacterCarryInventory가 live performance와 장착 workwear로 soft limit를 계산하고 IItemHaulingSettingsProvider가 기존 save의 maxCarryMultiplier만 소유함. nominal 25kg·멜빵 1.25·범위 1.0/2.5는 CharacterCarryTuning 코드 권위이며 save DTO·CSV·UI에 별도 쓰기 권위를 만들지 않음. 과거 세이브 마이그레이션은 범위 밖임
자동 감사 ID와 전수 목록 포함 여부: carry_target_band_authority, carry_weight_penalty, CARRY_UI_ITEM_SEEDED, CARRY_UI_WEIGHT_VISIBLE와 full Physical Logistics RESULT=PASS를 현재 slice 증거로 사용함. 후속 mass 원장은 413/413 item semantic, 354/354 recipe mass balance, 61/61 equipment mapping을 별도로 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-carry-capacity.txt, Temp/physical-item-contracts.tsv, Artifacts/QA/physical-item-logistics-playmode-report.txt, Unity exact Roslyn DungeonStory.Items·Assembly-CSharp·Assembly-CSharp-Editor, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 시뮬레이션 검증(운반 한도 slice) PASS. 대표 performance 0.76에서 ordinary 19/28.5kg, harness 23.75/35.625kg, stress 19/28.5/47.5kg가 exact이고 live 기본 performance actor는 25/37.5kg, production Physical Logistics의 실제 Roma는 19.13/28.69kg로 CARRY UI와 AI 물류 전체 RESULT=PASS, Console 0/0을 통과함. item kg·BOM 질량·장비 instance mass·EWU·가격·6인 3-seed 전수 보정은 아직 미완료이므로 전체 물리 중량 또는 밸런스 완료로 보고하지 않음
```

## V27 물리 중량 명시 단위·질량 보존 1차 수직 슬라이스 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-explicit-semantic-slice-v1
콘텐츠 종류: 깨끗한 물·황혼곡물·곡물죽·통나무·처리목재의 명시적 1개 단위, 재료 밀도와 조리·제재 질량 보존 AuditOnly 기준
정의·카탈로그·실행기 위치: PhysicalMassAuthoringContracts, V27PhysicalMassAuthorityInventoryDebugScenarios, V27PhysicalMassExplicitSemanticDebugScenarios, ItemDefinitionSO, ProductionRecipeSO
등장 시대와 연구: 기존 농업·조리·벌목·제재 연구와 해금을 그대로 사용하며 신규 아이템·레시피·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 아직 플레이 수치는 바뀌지 않음. 향후 적용 후보는 물 1개=0.5L, 곡물 1개=0.5L 건곡 계량, 곡물죽 1개=1인분, 통나무 1개=표준 절단 구간, 처리목재 1개=표준 가공 묶음으로 고정해 ‘1개’ 의미가 kg 계산마다 달라지는 것을 금지함
물리 BOM·입력·출력: 곡물죽은 곡물 6×350g+상수 0.25물 단위 125g=2,225g 입력을 곡물죽 6×362g=2,172g+폐수 0.1물 단위 50g+증발 3g로 exact 보존함. 제재는 통나무 2×1,800g=3,600g을 처리목재 3×1,100g=3,300g+절삭 손실 300g로 exact 보존함
직접 작업량과 계산 근거: 기존 곡물죽 28 WU, 제재 22 WU와 생산 수량은 불변임. proposed kg는 물 500→500g, 곡물 350→350g, 곡물죽 600→362g, 통나무 1,800→1,800g, 처리목재 1,200→1,100g이며 현재 ScriptableObject에는 적용하지 않음
EWU와 목표 회수 기간: 이번 slice는 kg·handling 후보만 배정하고 EWU·시장 가격·회수 Floor를 재생성하지 않음. ApplyApproved 전에는 기존 원장 값이 권위이며 proposed gram을 가격 credit이나 비용 debit으로 사용하지 않음
공간·전력·물·연료·정비: 곡물죽의 상수 0.25와 폐수 0.1을 물 1개=500g 기준으로 질량에 포함함. 저장·FacilityBuffer·전력·연료·공간·정비·부패 수치는 불변이며 tableware는 재사용 시설로 보고 식사 item tare에 넣지 않음
위험·실패·회복 방식: 413 canonical item 중 명시 semantic이 없는 408개, duplicate/noncanonical ID, live BOM·수량·fluid drift, 질량식 1g 불일치, 6–11kg haul batch 불가능, asset byte mutation은 fail-loud함. 약품·용기는 empty-container/waste 회수 계약 전까지 의도적으로 미배정함
사회·비가역 비용: 처리목재 감소 100g와 곡물죽 감소 238g는 향후 운반 왕복과 handling EWU를 낮출 수 있으므로 전수 kg 적용 후 6인 생존망·물류 12–20%·3-seed를 다시 검증해야 함. AuditOnly 상태에서는 실제 플레이·세이브·가격에 영향 0임
기존 대안과의 장단점: 현재 float kg를 그대로 이름 추측으로 승인하면 곡물죽이 입력보다 1,375g 무거워지고 제재 손실이 0g인 문제를 숨김. 반대로 모든 포장 무게를 즉시 추가하면 빈 용기 회수 경로 없이 tare가 증발하므로 첫 slice는 bulk/무포장 항목만 닫고 약품을 보류함
지배 전략 방지 조건: 무입력 질량 생성 0, 미기록 증발·절삭 0, 포장 tare 증발 0, displayName 기반 fallback 0, candidate-only 자동 승인 0, kg 적용 전 EWU·가격 선반영 0, 일반 품목 6–11kg 묶음 불가능 0을 요구함
저장 권위와 실행 명령: live kg Before는 ItemDefinitionSO.unitWeight, BOM·fluid는 ProductionRecipeSO가 단일 권위임. explicit semantic/profile/transform은 Editor AuditOnly 코드 권위이며 저장 DTO를 추가하지 않고 과거 세이브 마이그레이션도 수행하지 않음
자동 감사 ID와 전수 목록 포함 여부: GRAIN_PORRIDGE_MASS_CONSERVATION, SAWMILL_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture, assetMutations=0을 요구함. 전체 gate는 413/413 semantic, 354/354 transform/tare, 61/61 equipment mapping이 닫힐 때까지 미완료임
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정(AuditOnly) PASS. 5/413 item semantics, 5 material profiles, 2 exact transform contracts와 no-op byte/mtime identity는 통과했지만 assetApplication=0이며 408 item semantics, package tare, 나머지 recipe 질량, 실제 kg 적용, EWU·가격 재생성, 6인 생존망·Physical Logistics·3-seed 전에는 공식·시뮬레이션·실전 완료로 승격하지 않음
```

## V27 물리 중량 버섯·육류 조리 질량 보존 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-food-transform-slice-v2
콘텐츠 종류: 동굴버섯·버섯수프·생고기·구운고기의 명시 단위와 조리 질량 보존 AuditOnly 확장
정의·카탈로그·실행기 위치: PhysicalMassAuthoringContracts, V27PhysicalMassExplicitSemanticDebugScenarios, ResourceItemDefinitionSO, ProductionRecipeSO recipe:mushroom-soup/recipe:roasted-meat
등장 시대와 연구: 기존 버섯 채집·축산/도축·조리 연구와 해금을 유지하며 신규 콘텐츠를 추가하지 않음
플레이어에게 주는 새 결정: 아직 live 수치는 변하지 않음. 향후 버섯 1개는 250g 수확 바구니, 생고기 1개는 700g 절단육, 수프와 구운고기는 각 1인분으로 고정해 생산·소비·운반의 단위 의미를 일치시킴
물리 BOM·입력·출력: 버섯수프는 버섯 2×250g+상수 125g=625g을 수프 2×285g+폐수 50g+증발 5g로 exact 보존함. 구운고기는 생고기 2×700g=1,400g을 구운고기 2×630g+수분 증발 140g로 exact 보존함
직접 작업량과 계산 근거: 기존 버섯수프 28 WU, 구운고기 20 WU와 BOM·출력량은 불변임. proposed kg는 버섯 250→250g, 수프 650→285g, 생고기 700→700g, 구운고기 750→630g이며 ScriptableObject에는 적용하지 않음
EWU와 목표 회수 기간: proposed kg에 따른 handling EWU·가격은 아직 생성하지 않음. 현재 원장과 live asset이 Before 권위이며 asset apply 전 값은 경제 credit/debit으로 사용하지 않음
공간·전력·물·연료·정비: 버섯수프 상수 0.25물 단위와 폐수 0.1물 단위를 각각 125g/50g으로 포함함. 조리 시설·저장·부패·전력·연료·청소·정비 수치는 유지함
위험·실패·회복 방식: recipe input/output/fluid drift, gram equation 불일치, 미기록 수분 생성·소실, 6–11kg batch 불가능, asset mutation은 fail-loud함. 조리 실패·부패는 별도 spoilage transform에서 후속 감사함
사회·비가역 비용: 수프 -365g, 구운고기 -120g 후보는 운반량과 저장 밀도를 낮추므로 적용 후 물류·저장·6인 식량망을 반드시 재측정함. AuditOnly에서는 플레이·세이브 영향 0임
기존 대안과의 장단점: Before를 유지하면 수프 2개가 입력+상수보다 675g, 구운고기 2개가 생고기보다 100g 무거워지는 무입력 질량 생성이 남음. 출력량을 줄이지 않고 1인분 중량과 명시적 증발만 조정해 nutrition 생산량은 보존함
지배 전략 방지 조건: 조리 질량 생성 0, 미기록 수분 손실 0, nutrition 무료 증가 0, BOM·출력량 몰래 변경 0, displayName fallback 0, asset apply 전 EWU 반영 0을 요구함
저장 권위와 실행 명령: ItemDefinitionSO.unitWeight와 ProductionRecipeSO가 live Before 권위이고 explicit semantic/transform은 Editor AuditOnly 기준임. 저장 DTO·과거 세이브 마이그레이션은 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: MUSHROOM_SOUP_MASS_CONSERVATION, ROASTED_MEAT_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture, assetMutations=0을 필수화함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, Unity compile, Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정(AuditOnly) PASS. 누적 9/413 semantics, 9 profiles, 4 exact transforms가 통과했고 두 번째 실행의 byte와 mtime도 동일함. 404개 semantic·나머지 레시피·tare·실제 kg·EWU·가격·실전 회귀가 남아 전체 완료가 아님
```

## V27 물리 중량 잿불뿌리 스튜 질량 보존 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-root-stew-transform-slice-v3
콘텐츠 종류: 잿불뿌리·잿불뿌리 스튜의 명시 단위와 조리 질량 보존 AuditOnly 확장
정의·카탈로그·실행기 위치: PhysicalMassAuthoringContracts, V27PhysicalMassExplicitSemanticDebugScenarios, ResourceItemDefinitionSO resource:ember-root/food:root-stew, ProductionRecipeSO recipe:root-stew
등장 시대와 연구: 기존 research:agriculture:field와 research:cuisine:crops 해금, crop:ember-root 생산 및 cookbench 실행 경로를 유지하며 신규 콘텐츠를 추가하지 않음
플레이어에게 주는 새 결정: 아직 live 수치는 변하지 않음. 향후 잿불뿌리 1개는 450g 표준 수확 묶음, 스튜 1개는 480g 1인분으로 고정해 농업 수확·조리·식사의 단위 의미를 일치시킴
물리 BOM·입력·출력: 잿불뿌리 2×450g+깨끗한 물 0.25단위 125g=1,025g 입력을 스튜 2×480g=960g+폐수 0.1단위 50g+수분 증발 15g로 exact 보존함
직접 작업량과 계산 근거: 기존 recipe:root-stew의 28 WU, 뿌리 2개 입력, 스튜 2개 출력, nutrition 38은 불변임. proposed kg는 뿌리 450→450g, 스튜 700→480g이며 ScriptableObject에는 적용하지 않음
EWU와 목표 회수 기간: proposed kg에 따른 handling EWU·가격·농업 ROI는 아직 재생성하지 않음. ApplyApproved 전에는 현재 live unitWeight와 기존 전수 원장이 경제 권위이며 proposed mass는 credit/debit에 사용하지 않음
공간·전력·물·연료·정비: 조리 상수 물 125g과 폐수 50g을 질량식에 포함함. 작물 면적·성장 42시간·수확량·cookbench 공간·용수·하수·전력·정비는 변경하지 않음
위험·실패·회복 방식: live item Before mass, recipe BOM·출력·확률·fluid drift, 1g 질량 불일치, 6–11kg 일반 haul batch 부재, asset byte mutation은 fail-loud함. 부패·조리 실패·용수 중단은 별도 transform/PlayMode에서 후속 감사함
사회·비가역 비용: 스튜 1개 -220g 후보는 저장 밀도와 운반량을 낮추지만 nutrition·출력 수량은 유지함. 실제 적용 후 6인 식량 폐쇄 루프, 물류 12–20%, 저장 70/90%, 3-seed에서 과도한 편익 여부를 재검증해야 함
기존 대안과의 장단점: Before 700g를 유지하면 2개 출력이 총 입력보다 375g 무거워져 무입력 질량 생성이 발생함. 출력 수량·nutrition을 줄이지 않고 1인분 질량과 명시적 조리 손실만 교정해 생산량 밸런스와 물리 보존을 분리함
지배 전략 방지 조건: 조리 질량 생성 0, 미기록 수분 손실 0, 무료 nutrition 증가 0, BOM·출력·WU 몰래 변경 0, displayName fallback 0, asset 적용 전 EWU·가격 선반영 0을 요구함
저장 권위와 실행 명령: ItemDefinitionSO.unitWeight와 ProductionRecipeSO가 live Before 권위이고 explicit semantic/profile/transform은 Editor AuditOnly 기준임. 저장 DTO와 과거 세이브 마이그레이션은 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: ROOT_STEW_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture, assetMutations=0을 필수로 기록하며 전체 413 item·354 recipe gate에는 누적 포함함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, Unity compile, Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정(AuditOnly) PASS. 누적 11/413 semantics, 11 profiles, 5 exact transforms와 두 번째 실행 byte·mtime identity가 통과했음. 402개 semantic, package tare, 나머지 recipe 질량, 실제 kg 적용, EWU·가격 재생성, 6인 생존망·Physical Logistics·3-seed가 남아 전체 완료가 아님
```

## V27 물리 중량 우유·달걀·제분·달걀전 질량 보존 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-animal-product-pancake-slice-v4
콘텐츠 종류: 우유·달걀의 명시적 동물 생산 단위와 황혼곡 제분·달걀전 조리 질량 보존 AuditOnly 확장
정의·카탈로그·실행기 위치: PhysicalMassAuthoringContracts, V27PhysicalMassExplicitSemanticDebugScenarios, source:animal-milk/source:animal-egg, recipe:milling-flour/recipe:egg-pancake, 관련 ResourceItemDefinitionSO
등장 시대와 연구: 기존 research:husbandry:selective, research:cuisine:milling, research:cuisine:livestock와 animal-pen·mill·cookbench 경로를 유지하며 신규 아이템·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 아직 live 수치는 변하지 않음. 향후 우유 1개는 재사용 착유통으로 옮기는 0.8L 내용물, 달걀 1개는 껍질 포함 4알 묶음, 밀가루 1개는 500g 제분 계량, 달걀전 1개는 800g 식사 1인분으로 고정함
물리 BOM·입력·출력: 제분은 곡물 3×350g=1,050g을 밀가루 2×500g=1,000g+미수거 밀기울·분진 50g으로 보존함. 달걀전은 달걀 2×250g+밀가루 500g+우유 824g+물 100g=1,924g을 식사 2×800g+폐수 75g+증발 249g으로 exact 보존함
직접 작업량과 계산 근거: 기존 제분 10 WU, 달걀전 44 WU, 입력·출력 수량·nutrition 50·mood 4는 불변임. proposed kg는 우유 800→824g, 달걀 250→250g, 밀가루 300→500g, 달걀전 650→800g이며 SO에는 적용하지 않음
EWU와 목표 회수 기간: 우유·달걀 생산의 사료·축산 WU와 proposed handling kg에 따른 EWU·가격은 아직 재생성하지 않음. ApplyApproved 전에는 현재 live unitWeight와 기존 전수 원장이 경제 권위임
공간·전력·물·연료·정비: 우유의 재사용 착유통·달걀 수거 바구니·조리기구·식기는 시설 인프라이며 item tare에 포함하지 않음. animal-pen·mill·cookbench 공간, 물 0.2단위, 폐수 0.15단위, 전력·청소·정비는 유지함
위험·실패·회복 방식: live Before mass, source batch 3, recipe BOM·출력·fluid drift, 1g 질량 불일치, 일반 6–11kg haul batch 부재, asset mutation은 fail-loud함. 사료에서 동물 생체량으로 이어지는 외부 질량은 source transform 밖의 축산 권위로 명시함
사회·비가역 비용: 우유 +24g, 밀가루 +200g, 달걀전 +150g 후보는 물류·저장 부담을 늘리지만 기존 무입력 질량을 제거함. 적용 후 축산 처리량, 6인 식량망, 저장 70/90%, 물류 12–20%, 3-seed를 재검증해야 함
기존 대안과의 장단점: 우유·달걀을 이름만 보고 단일 낱개로 간주하면 source batch와 운반 단위가 모호하고, 달걀전 2개 Before 1,300g는 입력·용수 1,924g 중 549g 처분을 숨김. 명시 단위와 폐수·증발을 분리해 영양·출력량은 유지함
지배 전략 방지 조건: 제분·조리 질량 생성 0, 미기록 밀기울·수분 손실 0, 착유통 tare 증발 0, 무료 nutrition 증가 0, BOM·WU·출력 몰래 변경 0, kg 적용 전 EWU·가격 선반영 0을 요구함
저장 권위와 실행 명령: ItemDefinitionSO.unitWeight, ProductionRecipeSO, animal source recipe가 live Before 권위이고 explicit semantic/profile/transform은 Editor AuditOnly 기준임. 런타임 상태·저장 DTO·과거 세이브 마이그레이션은 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: MILLING_FLOUR_MASS_CONSERVATION, EGG_PANCAKE_MASS_CONSERVATION, ANIMAL_PRODUCT_SOURCE_CONTRACTS, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture, assetMutations=0을 누적 전수 gate에 포함함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, Unity compile, Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 기준 배정(AuditOnly) PASS. 누적 15/413 semantics, 15 profiles, 7 exact transforms와 두 번째 실행 byte·mtime identity가 통과했음. 응유는 물리 유청 처분 계약 전까지 보류하며 398개 semantic, package tare, 실제 kg 적용, EWU·가격, 6인 생존망·Physical Logistics·3-seed가 남아 전체 완료가 아님
```

## V27 응유 유청 폐수·신선 응유 중량 적용 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-curd-whey-wastewater-applied-v5
콘텐츠 종류: 응유 생산에서 발생하는 유청을 기존 하수망의 물리 폐수로 귀속하고 신선 응유 1인분 중량을 실제 ScriptableObject에 적용한 focused 질량 보존 교정
정의·카탈로그·실행기 위치: ProductionWorkshopContentAssetBuilder, recipe:curd, recipe:fresh-curd, material:curd, food:fresh-curd, ProductionCycleUtilityService, ProcessFluidUseRuntime, IFluidWastewaterTransaction, FluidNetworkRuntime, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 축산·응유 제조·조리·상수·하수 처리 연구와 시설 해금을 유지하며 신규 유청 아이템·레시피·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 응유를 만들면 유청이 사라지지 않고 기존 폐수망의 용량 4.2단위를 점유하므로 배수·저장·폐수 처리 능력을 함께 확보해야 함. 하수 용량이 부족하면 기존 typed utility failure로 해당 생산 주기가 시작되지 않으며 바닥 폐기나 무료 증발 경로는 없음
물리 BOM·입력·출력: recipe:curd는 우유 3×800g+소금돌 1×500g=2,900g과 공정수 0.2단위=100g을 받아 응유 2×450g=900g과 유청 폐수 4.2단위=2,100g을 배출해 3,000g을 exact 보존함. recipe:fresh-curd는 응유 1×450g을 신선 응유 2×225g으로 변환해 손실 0g임
직접 작업량과 계산 근거: 응유 34 WU와 신선 응유 18 WU, 입력·출력 수량·nutrition·mood는 불변임. 실제 변경은 recipe:curd wastewaterPerCycle 0.2→4.2와 food:fresh-curd unitWeight 0.45kg→0.225kg 두 값이며 builder 권위도 동일하게 변경함. 이전 v4의 우유 824g 후보는 shared animal-product 권위 800g으로 후속 교정하고 달걀전은 물리 입력 1,824→1,800g, 증발 249→225g으로 exact 재계산함
EWU와 목표 회수 기간: 이번 적용은 질량과 폐수 용량만 교정하며 Direct WU·시장 가격·구매·판매·회수 EWU는 아직 재생성하지 않음. 유청 2.1kg은 처리 비용을 가진 wastewater debit이고, 후속 전수 원장에서 배수·처리 handling EWU를 Ceil 입력 비용으로 반영해야 함
공간·전력·물·연료·정비: recipe 자체 공정수 0.1kg와 유청 폐수 2.1kg 외에 조리 시설의 공통 세척 유틸리티는 기존 BuildingProcessFluidAbility 계약을 별도로 유지함. 폐수는 FluidNetworkRuntime 저장량과 처리 시설 I11/I12/I13의 기존 input 10단위 처리량을 실제로 사용하며 용량·전력·슬러지·정비를 면제하지 않음
위험·실패·회복 방식: 배수망 수용량 부족 시 물과 재료를 소비하기 전에 생산이 실패해야 하며, 성공 시 폐수 4.2단위가 정확히 추가되어야 함. recipe·item·builder 값 drift, 1g 질량 불일치, 유청 무료 증발, 폐수망 우회, 두 번째 artifact 생성 diff는 fail-loud함. 생산 취소·시설 파괴 시 이미 발생하지 않은 유청을 생성하거나 이미 배출한 폐수를 되돌리지 않음
사회·비가역 비용: 신선 응유 1개가 450g에서 225g으로 줄어 동일 영양의 운반·저장 부담은 감소하지만 한 응유 batch의 하수 부담은 0.2에서 4.2로 증가함. 이 상쇄가 조리·축산 선택을 지나치게 불리하거나 유리하게 만드는지는 폐수 시설 포트폴리오, 6인 식량망과 3-seed에서 후속 판정함
기존 대안과의 장단점: 유청 전용 아이템을 새로 만들면 저장·부패·소비처·레시피·AI 물류를 모두 추가해야 하고, 유청을 단순 증발시키면 2.1kg 질량이 사라짐. 기존 폐수망을 사용하면 추가 콘텐츠 없이 물리 용량과 처리 비용이 남지만 하수 처리 병목을 명시적으로 부담함
지배 전략 방지 조건: 유청 무료 소멸 0, 폐수 용량 우회 0, 폐수 처리 무료화 0, 신선 응유 질량 생성 0, nutrition·출력량 무료 증가 0, BOM·WU 몰래 변경 0, 동일 레시피 반복으로 질량·EWU 차익 생성 0을 요구함
저장 권위와 실행 명령: recipe:curd ProductionRecipeSO의 cleanWaterPerCycle/wastewaterPerCycle과 food:fresh-curd ResourceItemDefinitionSO.unitWeight가 live 권위이며 builder가 재생성 권위임. 폐수량은 FluidNetworkRuntime과 기존 fluid save authority가 소유하고 별도 유청 DTO·과거 세이브 마이그레이션을 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: CURD_WHEY_WASTEWATER_MASS_CONSERVATION, FRESH_CURD_MASS_CONSERVATION, EGG_PANCAKE_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture, CURD_PIPELINE_CONTRACTS를 focused 증거로 사용함. 전체 gate는 413/413 item semantic, 전 recipe transform/tare, 61/61 equipment mapping과 전수 EWU·가격 재생성을 계속 요구함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, IndustrialInfrastructureDebugScenarios.RunCurrentBalanceContracts, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 실제 asset 2개와 builder 권위가 적용되었고 누적 18/413 semantics, 18 profiles, 9 transforms, 응유·신선 응유 질량 보존, 생산·하수 계약, 두 번째 artifact byte·mtime identity를 통과함. 395개 semantic, package tare, 나머지 recipe 질량, EWU·가격 재생성, 폐수 용량을 포함한 6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 숙성 치즈·치즈버섯찜 중량 적용 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-cheese-aging-meal-applied-v6
콘텐츠 종류: 응유 downstream의 숙성 수분 손실과 치즈버섯찜 1인분 중량을 명시하고 실제 치즈버섯찜 ScriptableObject를 교정한 focused 질량 보존 확장
정의·카탈로그·실행기 위치: ResourceEconomyAssetBuilder, ProductionWorkshopContentAssetBuilder, recipe:cheese, recipe:cheese-mushroom, material:cheese, food:cheese-mushroom, support:cheese-rack, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 research:cuisine:livestock와 cookbench·cheese-rack 해금을 유지하며 신규 아이템·레시피·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 숙성 치즈는 신선 응유보다 가볍고 보존성이 좋은 중간재로 남으며, 치즈버섯찜은 치즈와 버섯의 실제 총질량을 두 인분으로 나눈 450g 식사가 됨. nutrition 47·mood 7·freshness 390과 생산 수량은 변경하지 않음
물리 BOM·입력·출력: recipe:cheese는 응유 2×450g=900g을 치즈 2×400g=800g과 숙성 수분손실 100g으로 exact 보존함. recipe:cheese-mushroom은 치즈 1×400g+동굴버섯 2×250g=900g을 식사 2×450g=900g으로 변환해 손실 0g임
직접 작업량과 계산 근거: 치즈 숙성은 preparation 4 WU+finishing 2 WU와 16 game-hour passive processing, 치즈버섯찜은 기존 18 WU를 유지함. 실제 변경값은 food:cheese-mushroom unitWeight 0.7→0.45kg 하나이며 material:cheese 0.4kg는 이미 exact이므로 변경하지 않음. ResourceEconomyAssetBuilder도 0.45kg로 동기화함
EWU와 목표 회수 기간: 이번 slice는 질량과 handling 후보만 적용하며 치즈·식사의 가격, 생산 노동 EWU, 숙성 선반 점유 비용은 아직 재생성하지 않음. 숙성 손실 100g은 회수 가능한 output credit이 아니고 MoistureEvaporation으로만 기록함
공간·전력·물·연료·정비: cheese-rack의 16시간 점유와 cookbench 접근·청소·정비·저장은 유지함. 두 recipe는 별도 공정수·폐수가 없으며 치즈버섯찜 조리 시설의 공통 세척 유틸리티는 기존 BuildingProcessFluidAbility 계약을 별도로 부담함
위험·실패·회복 방식: passive batch 온도·지원시설 중단, spoilage, 예약·물류 실패는 기존 생산 상태와 typed failure를 유지함. recipe BOM·출력·fluid drift, 숙성 수분 미기록, 식사 질량 생성, builder/asset 불일치와 두 번째 artifact diff는 fail-loud함
사회·비가역 비용: 치즈버섯찜 1개가 700g에서 450g으로 줄어 저장·운반 부담은 감소하지만 nutrition·출력량·신선도·노동은 유지됨. 치즈의 장기 보존 편익과 rack 점유 비용이 다른 식사보다 지배적인지는 EWU·6인 식량망·3-seed에서 후속 판정함
기존 대안과의 장단점: 기존 700g를 유지하면 900g 입력에서 1,400g 출력이 생겨 batch마다 500g이 생성됨. 출력을 1개로 줄이면 영양 생산량이 바뀌므로, 기존 2인분을 유지하면서 단위 중량만 450g로 맞춰 생산량 밸런스와 물리 보존을 분리함
지배 전략 방지 조건: 숙성·조리 질량 생성 0, 미기록 숙성 손실 0, nutrition·mood 무료 증가 0, passive processing 우회 0, cheese-rack 점유 면제 0, BOM·WU·출력 몰래 변경 0, kg 적용 전 가격 선반영 0을 요구함
저장 권위와 실행 명령: material:cheese와 food:cheese-mushroom ItemDefinitionSO.unitWeight, 두 ProductionRecipeSO, ProductionWorkshopContentAssetBuilder와 ResourceEconomyAssetBuilder가 live/rebuild 권위임. passive batch는 기존 ProductionBillSaveData 권위를 사용하고 새 DTO·과거 세이브 마이그레이션을 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: CHEESE_AGING_MASS_CONSERVATION, CHEESE_MUSHROOM_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture, CHEESE_PIPELINE_CONTRACTS를 focused 증거로 사용함. 전체 413 item·전 recipe·61 equipment gate에는 누적 포함함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 누적 20/413 semantics, 20 profiles, 11 transforms와 실제 mass asset 3개 적용 상태를 검증했고 치즈·치즈버섯찜 보존, 생산 계약, 두 번째 artifact SHA-256·mtime identity, Console 0/0을 통과함. 393개 semantic, package tare, 나머지 recipe, EWU·가격, 6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 밤포도·핏빛 호화식 중량 보존 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-night-grape-lavish-meat-slice-v7
콘텐츠 종류: 밤포도 수확 단위와 치즈 downstream인 핏빛 호화식의 공정수·폐수·수분손실을 포함한 focused 질량 보존 AuditOnly 확장
정의·카탈로그·실행기 위치: ResourceEconomyAssetBuilder, recipe:lavish-meat, resource:night-grape, resource:meat, material:cheese, food:lavish-meat, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 research:agriculture:field/irrigation, research:cuisine:livestock/lavish와 cookbench·prep-sink·cold-prep·spice-rack·hearth 해금을 유지하며 신규 콘텐츠를 추가하지 않음
플레이어에게 주는 새 결정: live 수치는 바뀌지 않음. 밤포도 1개를 250g 표준 수확 송이, 핏빛 호화식 1개를 1kg의 큰 호화식 1인분으로 명시해 농업·운반·조리 단위가 레시피마다 달라지는 것을 금지함
물리 BOM·입력·출력: recipe:lavish-meat는 생고기 2×700g+치즈 1×400g+밤포도 2×250g=2,300g과 공정수 0.3단위=150g을 받아 호화식 2×1,000g=2,000g+폐수 0.25단위=125g+조리 수분손실 325g으로 2,450g을 exact 보존함
직접 작업량과 계산 근거: 기존 54 WU, 출력 2, nutrition 60, mood 14, freshness 480을 유지함. 밤포도 250g과 호화식 1,000g은 현재 ItemDefinitionSO와 일치하므로 actual asset 변경·Dirty는 0이며 explicit semantic/profile/transform만 추가함
EWU와 목표 회수 기간: 이번 slice는 kg 단위 권위만 고정하고 농업 WU·치즈 숙성·지원시설 점유·handling을 포함한 EWU와 가격은 재생성하지 않음. 증발 325g과 폐수 125g은 판매·회수 output credit이 아님
공간·전력·물·연료·정비: crop plot, cookbench와 네 support 시설의 공간·접근·전력·상수·하수·청소·정비를 유지함. recipe 공정수 150g과 폐수 125g 외에 시설 공통 세척 유틸리티는 기존 별도 계약을 부담함
위험·실패·회복 방식: 상수·하수·지원시설·입력 버퍼 부족은 기존 typed production failure를 유지하고 재료 소비 전 preflight해야 함. BOM·출력·fluid drift, 미기록 수분, 무입력 질량 생성, asset mutation, 두 번째 artifact diff는 fail-loud함
사회·비가역 비용: 호화식은 1kg로 일반 식사보다 무거워 운반·저장 부담을 유지하지만 nutrition·mood 편익도 큼. 이 선택이 일반 식사를 지배하는지는 농업·치즈·지원시설 노동과 폐수 처리까지 포함한 EWU·6인 폐쇄 루프에서 후속 판정함
기존 대안과의 장단점: 출력 중량을 줄이면 현재도 닫히는 질량식을 불필요하게 바꾸고 호화식의 큰 1인분 의미가 사라짐. 밤포도를 1알로 해석하면 농업 수확 batch와 레시피 물량이 비현실적이므로 250g 송이 단위를 유지함
지배 전략 방지 조건: 호화식 질량 생성 0, 미기록 수분·폐수 0, nutrition·mood 무료 증가 0, support 시설 우회 0, 물·폐수 처리 면제 0, kg 변경 없는 에셋 Dirty 0, kg 적용 전 EWU·가격 선반영 0을 요구함
저장 권위와 실행 명령: resource:night-grape와 food:lavish-meat ItemDefinitionSO.unitWeight, recipe:lavish-meat ProductionRecipeSO, ResourceEconomyAssetBuilder가 live 권위임. 기존 item/production/fluid save authority만 사용하고 새 DTO·과거 세이브 마이그레이션은 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: LAVISH_MEAT_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture, LAVISH_MEAT_PIPELINE_CONTRACTS를 focused 증거로 사용하며 전체 413 item·전 recipe gate에 누적 포함함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 누적 22/413 semantics, 22 profiles, 12 transforms와 실제 mass asset 적용 3개를 검증했고 호화식 질량 보존, 생산 계약, 두 번째 artifact SHA-256·mtime identity, Console 0/0을 통과함. 391개 semantic, package tare, 나머지 recipe, EWU·가격, 6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 맥아·농축 시럽·포도즙·포도 시럽 중량 적용 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-grape-malt-syrup-batch-applied-v8
콘텐츠 종류: 황혼곡 맥아화, 밤포도 농축 시럽, 포도 착즙, 포도 시럽 식사의 명시 단위와 실제 포도즙·포도 시럽 ScriptableObject 중량 교정
정의·카탈로그·실행기 위치: ProductionWorkshopContentAssetBuilder, recipe:malt, recipe:syrup, recipe:grape-juice, recipe:grape-syrup, material:malt, material:syrup, material:grape-juice, food:grape-syrup, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 research:cuisine:milling/crops와 brewery·cookbench·prep-sink·fermentation-vat 해금 및 실행 경로를 유지하며 신규 아이템·레시피·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 맥아 1개는 700g 발아·건조 곡물 묶음, 농축 시럽 1개는 350g 농축액, 포도즙 1개는 375g 착즙액, 포도 시럽 1개는 175g 소형 고당도 식사로 고정함. nutrition·mood·출력 수량은 유지하므로 선택 변화는 운반·저장 질량에 한정됨
물리 BOM·입력·출력: recipe:malt는 곡물 2×350g=700g을 맥아 1×700g으로 손실 없이 보존함. recipe:syrup은 밤포도 3×250g=750g을 농축 시럽 2×350g=700g+수분손실 50g으로 보존함. recipe:grape-juice는 포도 3×250g+공정수 0.1단위 50g=800g을 포도즙 2×375g=750g+폐수 0.05단위 25g+증발 25g으로 보존함. recipe:grape-syrup은 포도즙 375g을 포도 시럽 2×175g=350g+농축손실 25g으로 보존함
직접 작업량과 계산 근거: 기존 recipe WU, BOM, 출력량, nutrition, mood는 불변임. 실제 변경은 material:grape-juice unitWeight 0.45→0.375kg와 food:grape-syrup 0.35→0.175kg이며 material:malt 0.7kg와 material:syrup 0.35kg는 이미 exact라 Dirty하지 않음. ProductionWorkshopContentAssetBuilder도 두 변경값과 동기화함
EWU와 목표 회수 기간: 이번 적용은 물리 kg와 handling 후보만 교정하며 농업·발아·착즙·농축 Direct WU, 시장 가격, 구매·판매·회수 EWU는 아직 재생성하지 않음. 증발·농축 손실과 폐수는 판매 가능한 output credit이 아니며 후속 원장에서 입력 handling과 폐수 처리 비용을 Ceil debit으로 반영해야 함
공간·전력·물·연료·정비: brewery·cookbench·prep-sink·fermentation-vat의 공간·접근·전력·청소·정비를 유지함. 포도즙 공정수 50g과 폐수 25g은 기존 fluid transaction을 사용하며 재사용 생산 용기는 item tare에 넣지 않음
위험·실패·회복 방식: 상수·하수·입력·지원시설 부족은 재료 소비 전 기존 typed preflight로 실패해야 함. recipe BOM·출력·fluid drift, 1g 질량 불일치, 미기록 농축 손실, builder/asset 불일치, 변경 없는 malt/syrup Dirty, 두 번째 artifact diff는 fail-loud함
사회·비가역 비용: 포도즙은 개당 -75g, 포도 시럽은 -175g으로 운반·저장 부담이 감소하지만 nutrition·출력량은 유지됨. 포도 시럽의 높은 영양 밀도와 맥아·시럽 downstream이 다른 식량·음료 경로를 지배하는지는 EWU·가격·6인 식량망·3-seed에서 후속 판정함
기존 대안과의 장단점: 기존 포도즙 2개 900g은 포도·공정수 800g보다 100g을 생성하고, 기존 포도 시럽 2개 700g은 포도즙 입력 375g보다 325g을 생성함. BOM·영양·출력량을 변경하지 않고 단위 질량과 명시 손실만 교정해 생산량 밸런스와 물리 보존을 분리함
지배 전략 방지 조건: 착즙·농축 질량 생성 0, 미기록 수분·폐수 0, nutrition·mood 무료 증가 0, 지원시설·용수·하수 우회 0, BOM·WU·출력 몰래 변경 0, kg 적용 전 EWU·가격 선반영 0, 변경 없는 에셋 Dirty 0을 요구함
저장 권위와 실행 명령: 네 ItemDefinitionSO.unitWeight와 네 ProductionRecipeSO, ProductionWorkshopContentAssetBuilder가 live/rebuild 권위임. 기존 item·production·fluid save authority만 사용하며 새 DTO와 과거 세이브 마이그레이션은 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: MALT_MASS_CONSERVATION, CONCENTRATED_SYRUP_MASS_CONSERVATION, GRAPE_JUICE_MASS_CONSERVATION, GRAPE_SYRUP_MEAL_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture와 ProductionWorkshopDebugScenarios를 focused 증거로 사용함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 누적 26/413 semantics, 26 profiles, 16 transforms와 실제 mass asset 적용 5개를 검증했고 네 변환의 질량 보존, 생산 계약, 두 번째 artifact SHA-256·mtime identity, Console 0/0을 통과함. 387개 semantic, package tare, 나머지 recipe, 발효 계열 물·하수 계약, EWU·가격, 6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 발효액·맥아죽·포도주·맥주·증류 계열 중량 적용 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-fermentation-chain-applied-v9
콘텐츠 종류: 발효가스 대기 방출 손실의 명시적 질량 계약과 맥아죽 공정수, 발효액·어린 포도주·황혼 맥주·밤포도주·밤 증류주·알코올 downstream의 실제 중량 교정
정의·카탈로그·실행기 위치: PhysicalMassAuthoringContracts.PhysicalMassLossKind.FermentationGasLoss, ProductionWorkshopContentAssetBuilder, ResourceEconomyAssetBuilder, recipe:fermented-liquor/malt-porridge/young-wine/twilight-beer/night-wine/night-spirit/alcohol, 관련 ItemDefinitionSO, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 research:cuisine:crops/fermentation/distilling-aging와 cookbench·brewery·distillery·hearth·fermenter·aging-barrel·fractional-still 경로를 유지하며 신규 아이템·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 맥아죽과 발효액은 실제 상수 공급을 요구하므로 물 없는 생산이 불가능해지고, 발효 술은 배치당 대기 방출 손실로 산출 단위가 가벼워짐. nutrition·mood·효과·출력 개수·작업량·숙성시간은 유지됨
물리 BOM·입력·출력: 발효액은 맥아 700g+공정수 350g=발효액 1,000g+발효가스 50g, 맥아죽은 맥아 350g+공정수 750g=죽 1,100g, 어린 포도주는 포도즙 750g=어린 포도주 700g+발효가스 50g, 황혼 맥주는 발효액 1,000g=맥주 950g+발효가스 50g, 밤포도주는 어린 포도주 700g=숙성주 650g+수분증발 50g, 밤 증류주는 어린 포도주 700g+농축 시럽 350g=증류주 900g+공정증발 150g, 알코올은 발효액 1,000g=알코올 1,000g으로 exact 보존함
직접 작업량과 계산 근거: 모든 기존 Direct WU·준비/마감 WU·passive 시간·BOM 개수·출력 개수는 불변임. 실제 변경은 fermented-liquor cleanWater 0→0.7, malt-porridge cleanWater 0→1.5, young-wine 500→350g, twilight-beer 500→475g, night-wine 500→325g이며 builder 권위와 SO를 함께 동기화함
EWU와 목표 회수 기간: 발효가스와 숙성 증발은 회수·판매 가능한 output credit이 아니며 EWU 0의 명시 손실임. 추가 공정수는 후속 원장에서 Ceil 입력 비용과 상수 처리량에 반영해야 하며 시장 가격·구매·판매·회수 EWU는 아직 재생성하지 않음
공간·전력·물·연료·정비: 발효액 1 batch가 깨끗한 물 0.7단위, 맥아죽 1 batch가 1.5단위를 실제 ProductionRecipeSO utility preflight로 요구함. 별도 폐수는 생성하지 않고 발효가스는 하수·저장·물류를 사용하지 않는 대기 방출이며 기존 시설 공간·전력·연료·정비·support 점유는 유지함
위험·실패·회복 방식: 상수 부족 시 재료 소비 전에 typed utility failure가 나야 하고, 성공한 발효가스 손실은 되돌리거나 물리 아이템으로 회수하지 않음. 무입력 수분 생성, 발효손실의 판매·회수, 발효가스의 폐수 이중계상, recipe/builder/asset drift, 1g 질량 불일치와 두 번째 artifact diff는 fail-loud함
사회·비가역 비용: 어린 포도주 -150g, 황혼 맥주 -25g, 밤포도주 -175g은 운반·저장 부담을 줄이나 발효액·맥아죽의 물 수요가 생산 기반시설 부담을 늘림. 이 상쇄가 술·맥아죽을 과도하게 유리하거나 불리하게 만드는지는 상수 용량·6인 식량망·기분 수요·3-seed에서 후속 판정함
기존 대안과의 장단점: 발효손실을 폐수나 물리 CO2 아이템으로 만들면 존재하지 않는 처리·저장·소비 시스템을 추가하게 되고, 손실을 기록하지 않으면 기존 산출 중량이 입력보다 커짐. 대기 방출 손실은 경제 credit 없이 보존식을 닫지만 플레이어가 회수할 수 없는 손실이므로 레시피별 g을 명시적으로 고정해야 함
지배 전략 방지 조건: 발효·숙성 질량 생성 0, 발효가스 판매·재활용 0, 물·시설 preflight 우회 0, nutrition·mood 무료 증가 0, BOM·WU·출력 개수 몰래 변경 0, 공정수 비용 누락 0, kg 적용 전 가격 선반영 0을 요구함
저장 권위와 실행 명령: ItemDefinitionSO.unitWeight, ProductionRecipeSO.cleanWaterPerCycle, ProductionWorkshopContentAssetBuilder와 ResourceEconomyAssetBuilder가 live/rebuild 권위임. FermentationGasLoss는 immutable 감사 enum이며 런타임 재고·save DTO를 소유하지 않고 과거 세이브 마이그레이션도 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: FERMENTED_LIQUOR_MASS_CONSERVATION, MALT_PORRIDGE_MASS_CONSERVATION, YOUNG_WINE_MASS_CONSERVATION, TWILIGHT_BEER_MASS_CONSERVATION, NIGHT_WINE_MASS_CONSERVATION, NIGHT_SPIRIT_MASS_CONSERVATION, ALCOHOL_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture를 focused 증거로 사용함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 누적 33/413 semantics, 33 profiles, 23 transforms와 누적 assetApplication 10을 검증했고 일곱 발효·숙성·증류 변환, 생산 계약, 두 번째 artifact SHA-256·mtime identity, Console 0/0을 통과함. 380개 semantic, package tare, 나머지 recipe, 추가 물 사용을 포함한 EWU·가격·6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 채소 세척·염지·식초·발효 절임 중량 적용 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-vegetable-preservation-chain-applied-v10
콘텐츠 종류: 씻은 채소·염지 채소·발효 식초·발효 절임·보존 채식의 물·소금물 폐수·발효가스·조리 증발을 포함한 연쇄 질량 보존 교정
정의·카탈로그·실행기 위치: ProductionWorkshopContentAssetBuilder, recipe:washed-vegetable/brined-vegetable/fermented-vinegar/fermented-pickle/preserved-vegetable, 관련 ItemDefinitionSO, ProductionCycleUtilityService, FluidNetworkRuntime, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 research:cuisine:kitchen-hygiene/fermentation/vegan과 research:survival:preservation, cookbench·brewery·prep-sink·pickling-vat·fermenter·hearth 경로를 유지하며 신규 아이템·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 채소 보존은 원재료뿐 아니라 실제 상수와 하수 처리 용량을 요구함. 특히 염지·발효 절임의 버린 소금물은 폐수망을 점유하고, 발효 식초는 공정수와 회수 불가능한 발효가스 손실을 가짐. nutrition·mood·출력 개수·작업량은 유지됨
물리 BOM·입력·출력: 세척은 잿불뿌리 900g+물 125g=씻은 채소 900g+폐수 125g, 염지는 씻은 채소 900g+소금돌 500g+물 100g=염지 채소 1,000g+소금물 폐수 500g, 식초는 발효액 500g+물 350g=식초 800g+발효가스 50g, 발효 절임은 염지 채소 1,000g+식초 400g=절임 900g+소금물 폐수 500g, 보존 채식은 염지 채소 500g+씻은 채소 450g+식초 400g=식사 1,100g+조리 증발 250g으로 exact 보존함
직접 작업량과 계산 근거: 기존 Direct WU·passive 시간·BOM 개수·출력 개수는 불변임. 아이템 unitWeight는 다섯 항목 모두 기존 값이 exact라 Dirty하지 않았고, 실제 변경은 brined-vegetable wastewater 0.2→1.0, fermented-pickle wastewater 0→1.0, fermented-vinegar cleanWater 0→0.7 세 recipe utility 값이며 builder 권위도 동기화함
EWU와 목표 회수 기간: 버린 소금물과 발효가스는 판매·회수 output credit이 아니고, 물 투입·폐수 처리는 후속 원장에서 Ceil 비용으로 반영해야 함. 시장 가격·구매·판매·회수 EWU와 채소 보존 ROI는 아직 재생성하지 않음
공간·전력·물·연료·정비: prep-sink·pickling-vat·fermenter·hearth의 기존 공간·접근·전력·청소·정비를 유지함. 염지와 발효 절임은 각각 폐수 1.0단위를 실제 하수망에 배출하고, 식초는 물 0.7단위를 소비하며 발효가스는 하수·저장 공간을 점유하지 않음
위험·실패·회복 방식: 상수·하수 용량 부족은 입력 소비 전에 typed utility failure로 중단해야 함. 버린 소금물을 일반 증발로 숨기기, 발효가스·폐수 이중계상, 폐수 처리 우회, recipe/builder drift, 1g 질량 불일치, 변경 없는 item Dirty와 두 번째 artifact diff는 fail-loud함
사회·비가역 비용: 아이템 중량·영양은 유지하지만 보존식 생산의 상수·하수 부담이 증가함. 장기 보존 편익이 일반 채소 식사를 지배하거나 기반시설 부담 때문에 무용해지는지는 부패 회피 가치, 폐수 처리량, 6인 식량망과 3-seed에서 후속 판정함
기존 대안과의 장단점: 염지·발효 절임의 400~500g 차이를 단순 증발시키면 소금과 수분의 처분이 불명확하고 하수 비용이 사라짐. 기존 폐수망에 소금물을 귀속하면 추가 아이템 없이 물리 처리 비용을 남길 수 있으나 하수 시설 병목을 명시적으로 부담함
지배 전략 방지 조건: 채소 보존 질량 생성 0, 소금물 무료 소멸 0, 폐수 처리 면제 0, 발효가스 회수·판매 0, nutrition·보존기간 무료 증가 0, BOM·WU·출력 몰래 변경 0, utility 비용 누락 0을 요구함
저장 권위와 실행 명령: 다섯 ItemDefinitionSO.unitWeight, 다섯 ProductionRecipeSO fluid/BOM, ProductionWorkshopContentAssetBuilder가 live/rebuild 권위임. 폐수는 기존 FluidNetworkRuntime/save authority가 소유하고 발효가스는 immutable 감사 손실로만 존재하며 새 DTO·과거 세이브 마이그레이션을 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: WASHED_VEGETABLE_MASS_CONSERVATION, BRINED_VEGETABLE_MASS_CONSERVATION, FERMENTED_VINEGAR_MASS_CONSERVATION, FERMENTED_PICKLE_MASS_CONSERVATION, PRESERVED_VEGETABLE_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture를 focused 증거로 사용함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 누적 38/413 semantics, 38 profiles, 28 transforms와 누적 assetApplication 13을 검증했고 다섯 보존 변환, 생산·유틸리티 계약, 두 번째 artifact SHA-256·mtime identity, Console 0/0을 통과함. 375개 semantic, package tare, 나머지 recipe, utility 변경을 포함한 EWU·가격·6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 반죽·속재료·파이·속 채운 버섯 중량 적용 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-baking-filling-chain-applied-v11
콘텐츠 종류: 반죽과 양념 속재료의 명시적 손실 및 야채 파이·속 채운 버섯의 무손실 조립 질량을 연결한 focused 중량 보존 교정
정의·카탈로그·실행기 위치: ProductionWorkshopContentAssetBuilder, recipe:dough/seasoned-filling/vegetable-pie/stuffed-mushroom, material:dough/material:seasoned-filling/food:vegetable-pie/food:stuffed-mushroom ItemDefinitionSO, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 cookbench, prep-sink, cold-prep, spice-rack, oven, hearth와 관련 조리 연구 해금 경로를 유지하며 신규 아이템·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 파이와 속 채운 버섯은 기존 두 인분 생산량과 영양·기분을 유지하면서 실제 투입 질량을 정확히 나눈 475g·575g 식사가 됨. 반죽과 속재료의 기존 운반 단위는 유지되어 조리 단계 선택과 물류 묶음 의미가 바뀌지 않음
물리 BOM·입력·출력: 반죽은 밀가루 600g+달걀 250g+물 75g=반죽 1,000g+폐수 50g+준비 수분손실 275g, 속재료는 고기 1,400g+씻은 채소 450g=속재료 1,300g+손질 폐기 550g, 파이는 반죽 500g+씻은 채소 450g=2×475g, 속 채운 버섯은 속재료 650g+버섯 500g=2×575g으로 exact 보존함
직접 작업량과 계산 근거: 네 recipe의 기존 Direct WU·BOM 개수·출력 개수·nutrition·mood·지원시설은 불변임. 실제 item 변경은 food:vegetable-pie 0.7→0.475kg와 food:stuffed-mushroom 0.65→0.575kg 두 값이며 material:dough 0.5kg와 material:seasoned-filling 0.65kg는 이미 계약과 일치해 Dirty하지 않음
EWU와 목표 회수 기간: 이번 slice는 물리 질량과 운반 후보만 적용하며 조리 노동 EWU, 재료 가격, 구매·판매·회수 가치와 시설 ROI는 아직 재생성하지 않음. 손질 폐기와 수분 손실은 output credit이 아니며 후속 전수 원장에서 입력 비용만 남김
공간·전력·물·연료·정비: prep-sink·cold-prep·spice-rack·oven·hearth의 기존 공간·접근·전력·연료·청소·정비 계약을 유지함. 반죽 물 0.15단위와 폐수 0.1단위는 기존 상하수망을 사용하고 나머지 세 recipe의 별도 유체량은 0임
위험·실패·회복 방식: 지원시설·상수·하수·입력 저장·출력 공간 부족은 기존 typed 생산 실패를 유지함. recipe BOM·유체·출력 drift, 1g 질량 불일치, 미기록 손실, 파이·버섯 요리 질량 생성, builder/asset 불일치와 두 번째 artifact diff는 fail-loud함
사회·비가역 비용: 파이 1개 -225g, 속 채운 버섯 1개 -75g으로 저장·운반 부담이 감소하지만 영양·출력·노동은 유지됨. 이 편익이 다른 식사를 지배하는지는 EWU·부패·6인 식량 폐쇄 루프·저장 70/90%·3-seed에서 후속 판정함
기존 대안과의 장단점: 기존 중량은 파이 batch에서 450g, 속 채운 버섯 batch에서 150g을 무입력 생성함. 출력 수나 영양을 낮추지 않고 실제 BOM 총질량을 두 인분으로 나눠 생산량 밸런스와 물리 보존을 분리함
지배 전략 방지 조건: 조리 질량 생성 0, 미기록 손질·수분 손실 0, nutrition·mood 무료 증가 0, BOM·WU·출력 몰래 변경 0, 상하수 비용 우회 0, kg 적용 전 EWU·가격 선반영 0을 요구함
저장 권위와 실행 명령: 네 ItemDefinitionSO.unitWeight, 네 ProductionRecipeSO, ProductionWorkshopContentAssetBuilder가 live/rebuild 권위임. 폐수는 기존 FluidNetworkRuntime/save authority가 소유하고 손질·수분 손실은 감사 계약이며 새 DTO·과거 세이브 마이그레이션을 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: DOUGH_MASS_CONSERVATION, SEASONED_FILLING_MASS_CONSERVATION, VEGETABLE_PIE_MASS_CONSERVATION, STUFFED_MUSHROOM_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture를 focused 증거로 사용함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 누적 42/413 semantics, 42 profiles, 32 transforms와 누적 assetApplication 15를 검증했고 네 변환, 생산 계약, 두 번째 artifact SHA-256·mtime identity, Console 0/0을 통과함. 371개 semantic, package tare, 나머지 recipe, EWU·가격·6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 건초·사일리지·고기 파이·비건 만찬 중량 적용 후속 기록 (2026-08-20)

```text
정의 ID: balance:v27:physical-mass-feed-and-baking-downstream-applied-v12
콘텐츠 종류: 풀·짚에서 건초·사일리지로 이어지는 사료와 반죽·속재료 downstream 고기 파이, 식물 재료 downstream 비건 만찬의 focused 질량 보존 교정
정의·카탈로그·실행기 위치: ResourceEconomyAssetBuilder, ProductionWorkshopContentAssetBuilder, recipe:hay-feed/silage/meat-pie/lavish-vegan, 관련 ItemDefinitionSO, V27PhysicalMassExplicitSemanticDebugScenarios
등장 시대와 연구: 기존 agriculture gathering, husbandry feed, cuisine livestock/lavish와 feedbench·fermenter·cookbench·hearth 경로를 유지하며 신규 아이템·시설·연구를 추가하지 않음
플레이어에게 주는 새 결정: 건초와 사일리지는 동일한 풀·짚·곡물 투입에서 건조형과 수분 보강형 사료로 구분되며, 고기 파이는 반죽과 속재료의 실제 총질량을 두 인분으로 나눈 575g 식사가 됨. 비건 만찬의 900g·영양·기분은 현행 유지됨
물리 BOM·입력·출력: 풀·짚 3×80g+곡물 350g=건초 3×196g+건조손실 2g, 같은 물리 입력 590g+물 100g=사일리지 3×230g, 반죽 500g+속재료 650g=고기 파이 2×575g, 밀가루 600g+시럽 350g+버섯 500g+뿌리 450g+물 150g=비건 만찬 1,800g+폐수 125g+증발 125g으로 exact 보존함
직접 작업량과 계산 근거: 네 recipe의 기존 Direct WU·BOM 개수·출력 개수·nutrition·mood·feed value·지원시설은 불변임. 실제 item 변경은 feed:hay 0.45→0.196kg, feed:silage 0.7→0.23kg, food:meat-pie 0.8→0.575kg이며 resource:grass-straw 0.08kg와 food:lavish-vegan 0.9kg는 exact라 Dirty하지 않음
EWU와 목표 회수 기간: 이번 slice는 물리 질량과 handling 후보만 적용하며 축산 사료 가치, 조리 노동, 가격, 구매·판매·회수 EWU는 아직 재생성하지 않음. 건조·조리 손실은 output credit이 아니고 물·폐수 처리는 후속 Ceil 입력 비용으로 반영해야 함
공간·전력·물·연료·정비: feedbench·fermenter·cookbench와 연결 지원시설의 공간·접근·전력·연료·정비를 유지함. 사일리지는 물 0.2단위, 비건 만찬은 물 0.3·폐수 0.25단위를 실제 상하수망에 요구하며 건초·고기 파이는 별도 유체 0임
위험·실패·회복 방식: 물·하수·지원시설·저장·출력 공간 부족은 기존 typed failure를 유지함. recipe·builder·asset drift, 사료나 식사의 질량 생성, 건조·조리 손실 누락, feed value 몰래 변경과 두 번째 artifact diff는 fail-loud함
사회·비가역 비용: 건초와 사일리지 단위가 가벼워져 동일 개수 사료의 저장·운반 부담이 크게 감소하고 고기 파이는 225g 감소함. 이 변화가 축산 사료 공급이나 식사 선택을 지배하는지는 실제 동물 소비량, 물류 묶음, 저장 70/90%, 6인 식량망과 3-seed에서 후속 판정함
기존 대안과의 장단점: 기존 건초·사일리지 중량은 각각 batch당 760g·1,410g, 고기 파이는 450g의 무입력 질량을 생성함. 출력 개수·사료값·영양을 바꾸지 않고 BOM 총질량에 맞춰 unitWeight만 교정해 생산량 밸런스와 물리 보존을 분리함
지배 전략 방지 조건: 사료·식사 질량 생성 0, 미기록 수분 손실 0, feed value·nutrition·mood 무료 증가 0, BOM·WU·출력 몰래 변경 0, 상하수 비용 우회 0, kg 적용 전 EWU·가격 선반영 0을 요구함
저장 권위와 실행 명령: 다섯 ItemDefinitionSO.unitWeight, 네 ProductionRecipeSO, ResourceEconomyAssetBuilder와 ProductionWorkshopContentAssetBuilder가 live/rebuild 권위임. 폐수는 기존 FluidNetworkRuntime/save authority가 소유하고 새 DTO·과거 세이브 마이그레이션을 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: HAY_FEED_MASS_CONSERVATION, SILAGE_MASS_CONSERVATION, MEAT_PIE_MASS_CONSERVATION, LAVISH_VEGAN_MASS_CONSERVATION, HAUL_BATCH_BAND_6_TO_11_KG, deterministicRecapture를 focused 증거로 사용함
검증 매트릭스와 보고서 위치: Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt, v27-physical-mass-explicit-unit-semantics.csv, v27-physical-mass-primitive-profiles.csv, v27-physical-mass-transform-contracts.csv, ProductionWorkshopDebugScenarios, Unity compile, Unity Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 공식 검증 focused slice PASS. 누적 47/413 semantics, 47 profiles, 36 transforms와 누적 assetApplication 18을 검증했고 네 신규 변환, 생산 계약, 두 번째 artifact SHA-256·mtime identity, Console 0/0을 통과함. 366개 semantic, package tare, 나머지 recipe, EWU·가격·축산·6인 생존망·Physical Logistics·3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```
## V27 물리 질량·운반 런타임 권위 전환 계획 기록 (2026-08-20)

```text
정의 ID: architecture:v27-physical-mass-runtime-authority-plan-v1
콘텐츠 종류: 기존 item kg, 운반, 창고, 생산 WIP와 물리 disposition의 단일 gram 권위 전환 계획
정의·카탈로그·실행기 위치: v27-physical-mass-and-hauling-recalibration-plan.md, ItemDefinitionSO, BuildingStorageAbility, IPhysicalItemMassQuery, AbilityHaul, ProductionBillRuntime
등장 시대와 연구: 모든 시대·모든 물리 아이템과 저장 시설에 적용하며 연구 해금 순서와 콘텐츠 목록은 변경하지 않음
플레이어에게 주는 새 결정: 창고는 실제 kg 한도로 운영되고, 생산 출력은 bounded FacilityBuffer에서 별도 운반 AI가 처리함. 부상자·포로·생포 동물 운반은 item kg와 속도 페널티에서 제외함
물리 BOM·입력·출력: 이번 계획 교정 자체는 BOM·수량·출력을 변경하지 않음. 구현은 generic quantity×unit grams, unique instance grams, typed Source/Transfer/Transform/Sink와 exact WIP output/byproduct/loss를 요구함
직접 작업량과 계산 근거: nominal carry 25kg, 대표 일반 19.10/28.65kg, 멜빵 23.88/35.81kg을 유지함. 생산 FacilityBuffer는 max(2회분, p95 haul clearance 유입량), 최대 4회분으로 제한함
EWU와 목표 회수 기간: 이번 문서 교정은 EWU·가격을 변경하지 않음. item kg와 warehouse admission 적용 뒤 handling EWU, 가격, SCC와 6인 생존망을 재생성하기 전 밸런스 완료로 승격하지 않음
공간·전력·물·연료·정비: warehouse capacity는 positive long grams 한 개를 권위로 사용하고 kg는 UI projection으로만 표시함. 7일 비축, 정상 70%, 장애 90%, overflow와 30% 공간 headroom을 함께 검증함
위험·실패·회복 방식: over-capacity stock은 보존하고 신규 admission을 차단함. invalid owner/category/position은 restore를 원자 거부함. drop 실패는 RecoveryPending과 cargo ownership을 유지하며 확률 output은 완료 시 한 번 결정·저장하고 공간이 없으면 재굴림 없이 대기함
사회·비가역 비용: 무제한 바닥 출력·창고 count 우회·환자 구조 hard-cap을 추가하지 않음. kg 창고 전환이 초기 자본과 물류 노동을 과도하게 늘리면 capacity 또는 throughput을 재조정하고 수치 적용을 보류함
기존 대안과의 장단점: count 창고보다 물리적으로 일관되고 작은 단위 item을 부당하게 차감하지 않지만, exact instance mass·reservation·restore와 전수 callsite 전환 비용이 큼. 일반 바닥 출력보다 clutter가 적지만 buffer 포화 시 생산이 대기함
지배 전략 방지 조건: category-first physical spawn 0, untyped remove 0, count admission bypass 0, output reroll 0, cargo teleport/delete 0, duplicate mass writer 0, warehouse kg 우회 buffer 0
저장 권위와 실행 명령: 불변 item/building 정의와 physical stack/WIP/haul intent가 원본 권위이며 query·cache·UI·save DTO는 gameplay 쓰기 권위가 아님. 과거 세이브 마이그레이션은 범위 밖이고 신규 current-format 필수 필드 누락은 typed failure
자동 감사 ID와 전수 목록 포함 여부: v27-mass-storage-authority, v27-mass-mutation-revision, v27-mass-physical-disposition, v27-mass-wip-lifecycle, v27-mass-destination-capacity-leases와 production-callsite manifest 전수 포함
검증 매트릭스와 보고서 위치: generic/unique warehouse, module/ammunition, harness 1150g, over-capacity restore, RecoveryPending, probabilistic output, FacilityBuffer, entity transport, PhysicalItemLogistics, mid-action save/load, six-adult, clutter, EWU/price, Console 0/0
현재 밸런스 상태: 밸런스 기준 배정. 계획 교정 SHA-256 2F912C808C5E0F00CE6A1462214D58C57732FAEDE3FDB5FC218B0AD56D74E47A; runtime·asset 수치 전환과 전수 검증 전에는 공식/시뮬레이션/실전 완료가 아님
```
## V27 물리 질량·운반 런타임 권위 전환 계획 최종 감사 기록 (2026-08-20)

```text
정의 ID: architecture:v27-physical-mass-runtime-authority-plan-v2
콘텐츠 종류: architecture:v27-physical-mass-runtime-authority-plan-v1을 대체하는 문서 전용 최종 설계 감사; gameplay·asset·save schema·kg·WU·EWU·가격 수치 변경 없음
정의·카탈로그·실행기 위치: docs/game-design/v27-physical-mass-and-hauling-recalibration-plan.md, ItemMassProfileCatalogSO, MaterialMassProfileCatalogSO, RecipeMassBalanceProfileCatalogSO, IPhysicalItemMassQuery, IWarehouseMassAdmissionService, PhysicalItemDispositionCommand, ProductionBillRuntime
등장 시대와 연구: 모든 시대·모든 물리 아이템·생산·창고에 적용할 계획이며 연구 해금·콘텐츠 목록·과거 세이브 호환 정책은 변경하지 않음
플레이어에게 주는 새 결정: 최종 구현 뒤 창고는 positive gram capacity, 일반 운반은 25kg nominal의 19/29kg 실효 밴드, living transport는 item kg·hard cap·속도 페널티 없는 전용 소유권 경로를 사용함. 다중 destination Pick-and-Haul은 필수 migration 뒤 별도 최적화임
물리 BOM·입력·출력: 문서 단계 BOM·수량 변경 0. density×volume×packing 분모 1,000,000, product-bound/process-fluid/process-fuel 분리, Source/Transfer/Transform/Sink receipt, complete realized output vector와 output/byproduct/package-return gram 보존을 구현 계약으로 확정함
직접 작업량과 계산 근거: 직접 WU는 변경하지 않음. nominal 25kg, 대표 일반 19.10/28.65kg, 멜빵 23.88/35.81kg을 유지하고 신규 수치는 구조 기반 Gate S0와 kg·효능·WU·EWU 결합 감사 전 적용 금지함
EWU와 목표 회수 기간: EWU·가격 변경 0. kg 적용 뒤 handling EWU·AcquisitionCost·RecoverableValue·가격·SCC를 재생성하고 입력 Ceil·산출 Floor·SCC tolerance 0을 유지함
공간·전력·물·연료·정비: warehouse gram capacity와 actual pile/cell occupancy를 분리하고, process fluid와 process fuel은 제품 질량에서 제외해 wastewater/byproduct/loss로 닫음. FacilityBuffer는 main output·부산물·반환 포장과 resolved-output reservation을 포함한 2~4 cycle bound를 사용함
위험·실패·회복 방식: admission token/receipt와 stable lock order, output all-or-nothing commit, valid over-capacity 보존, invalid destination atomic restore rejection, WIP blocked-state 무재굴림, current-cell recovery drop·RecoveryPending을 계획 불변식으로 고정함
사회·비가역 비용: 초기 자본·공간·생존망의 수치를 이번 문서에서 바꾸지 않음. living transport를 kg로 제한하지 않되 opportunistic item haul·두 번째 entity ownership을 금지하고 전용 path/interruption을 검증함
기존 대안과의 장단점: count warehouse·category spawn·untyped remove보다 질량·identity 보존이 강하지만 8개 compile-green Gate S0 slice가 필요함. runtime catalog를 이중으로 읽거나 per-material SO를 대량 생성하지 않고 단일 catalog와 runtime projection을 사용함
지배 전략 방지 조건: count admission bypass 0, duplicate mass writer 0, category-only physical spawn 0, untyped remove 0, output reroll/partial commit 0, tare 증발 0, warehouse overcommit 0, cargo teleport/delete 0, implicit unlimited production warehouse 0
저장 권위와 실행 명령: current source 기준 root V25, production/WIP V8, warehouse state V4를 다음 형식으로 계획하고 physical V9는 새 필드가 없으면 유지함. restore는 facility/definition→physical→WIP→gram lease→publication→AI wake이며 과거 버전 migration은 하지 않음
자동 감사 ID와 전수 목록 포함 여부: BalanceMassSourceInventory drift gate, v27-mass-storage-authority, v27-mass-physical-disposition, v27-mass-wip-lifecycle, v27-mass-destination-capacity-leases, normalized warehouse/recipe/consumer/haul relation ledgers와 production callsite manifest 전수 포함
검증 매트릭스와 보고서 위치: density/packing, admission idempotency/rollback, probabilistic multi-line output, package-return capacity, current-format restore order, living transport, source inventory drift, 10,000-op p95/0B, PhysicalItemLogistics, mid-action save/load, six-adult, EWU/price, Console 0/0
현재 밸런스 상태: 밸런스 기준 배정. v1은 이 기록으로 superseded. UTF-8 without BOM+LF canonical 계획 SHA-256 6EBE4A7DF670B357995103F63DC8F985CBC04CE0EB4F38825E08FEFAEC98B726; 구조 기반 Gate S0·전수 수치 산정·적용·Unity/PlayMode/3-seed 증거 전에는 공식/시뮬레이션/실전 완료가 아님
```

## V27 물리 질량·운반 교차 시스템 완전성 계획 감사 기록 (2026-08-20)

```text
정의 ID: architecture:v27-physical-mass-runtime-authority-plan-v3
콘텐츠 종류: architecture:v27-physical-mass-runtime-authority-plan-v2를 대체하는 문서 전용 교차 시스템 완전성 감사; gameplay·asset·save schema·kg·WU·EWU·가격·capacity 변경 없음
정의·카탈로그·실행기 위치: docs/game-design/v27-physical-mass-and-hauling-recalibration-plan.md, IWorldItemStackRuntime, IItemTransferService, IProductionItemGateway, IPhysicalItemMassQuery, IWarehouseMassAdmissionService, PhysicalItemDispositionCommand, 모든 domain producer/consumer/read-side/save participant
등장 시대와 연구: 모든 시대·물리 아이템·창고·운반·생산·건설·생존·의료·연구·전투·경제·포로·방어·침입·원정·사건에 적용할 구조 계획이며 연구 해금과 콘텐츠 목록은 변경하지 않음
플레이어에게 주는 새 결정: 이번 문서 교정은 새 플레이 선택을 추가하지 않음. 최종 구현 뒤에는 창고 kg, exact lot 운반, 생산 WIP, 포장 반환, 도메인 소비·보상이 같은 물리 질량 권위를 사용하고 오류 시 부분 성공·순간이동·삭제·복제를 허용하지 않음
물리 BOM·입력·출력: BOM·수량·출력 변경 0. 모든 Source/Transfer/Transform/Sink 호출부가 exact item/instance lot, input/output/byproduct/terminal sink/loss gram, operation/commit ID와 receipt를 가지도록 semantic callsite manifest 계약을 추가함
직접 작업량과 계산 근거: WU 변경 0. nominal 25kg와 대표 일반 19.10/28.65kg·멜빵 23.88/35.81kg 밴드는 유지함. 이번 감사는 현재 production의 consume·spawn·delivery·destination release·count/generic-mass reader를 도메인별로 역참조해 누락된 연계 계약을 보강함
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. mass projection 변경은 MaterialEconomicProfile과 V23 Before/V27 After source digest를 무효화하므로 구조 Gate S0 뒤 handling EWU·가격·SCC를 재생성하고 승인 전 수치를 적용하지 않음
공간·전력·물·연료·정비: warehouse/FacilityBuffer/conveyor/WIP/retail/expedition capacity 차원을 분리하고 기존 7일 비축·70/90% utilization·Floor Clutter·30% headroom 계약을 유지함. fluid grams와 hydraulic volume은 별도 권위로 검증함
위험·실패·회복 방식: prepare, source reserve, destination reserve, domain prepare, physical/domain commit, publication, save/restore와 terminal retry 각 phase fault를 주입함. partial commit, pre-commit UI/event, subscriber 재실행, participant predecessor 누락, AI 조기 wake는 fail-loud함
사회·비가역 비용: 문서 단계에서 초기 자본·생존 생산량·시설 면적·인력 부담을 바꾸지 않음. kg 전환 때문에 Captivity·Defense·Factions·Invasion·FacilityEvolution·shop·events 등 다른 도메인이 깨지지 않도록 각 callsite의 save/rollback/UI/evidence owner를 명시함
기존 대안과의 장단점: aggregate remaining=0이나 문자열 검색보다 Roslyn semantic symbol+명시 domain registration manifest가 indirect interface·adapter·event·reader를 잡을 수 있으나 구현·검증 비용이 큼. runtime dual authority나 compatibility 예외 대신 compile-green 수직 슬라이스와 AuditOnly shadow 비교만 허용함
지배 전략 방지 조건: unclassified/unknown/compatibility production callsite 0, count-capacity/generic-mass gameplay reader 0, partial payment/assignment/output/release 0, pre-commit publication 0, save join orphan 0, duplicate writer 0, builder projection drift 0g, fault rollback mismatch 0
저장 권위와 실행 명령: current-format domain aggregate와 physical item/character carry/WIP/lease/token/outbox가 operation/commit ID로 cross-section join됨. participant required-predecessor graph와 AI wake fence를 생성·감사하며 과거 save converter나 partial fallback은 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: v27-mass-cross-system-callsite-manifest, v27-mass-domain-authority-matrix, v27-mass-transaction-publication-faults, v27-mass-save-restore-order, v27-mass-ui-presentation-matrix, v27-mass-builder-projection-manifest와 Gate S0 전 도메인 행 포함
검증 매트릭스와 보고서 위치: Foundation/Items/Warehouse/Carry/Work/Production/Survival/Medical/Combat/Research/Economy/Captivity/Defense/Invasion/Offense/Wildlife/FacilityEvolution/Industrial/Run/UI/Save/EWU/Editor 매트릭스, unit/property/EditMode/PlayMode fault tests, PhysicalItemLogistics, mid-action restore, six-adult, paired clutter, 3-seed, Console 0/0
현재 밸런스 상태: 밸런스 기준 배정. v2는 이 기록으로 superseded. UTF-8 without BOM+LF canonical 계획 SHA-256 2FFFE69F172A40B1F8C8568472BCF63CD81339AAF19F5F3D99C195421BB3891F; semantic callsite manifest·Gate S0 구현, 전수 수치 적용, Unity/PlayMode/3-seed 증거 전에는 공식/시뮬레이션/실전 완료가 아님
```

## V27 1g canonical 질량 런타임 권위 적용 기록 (2026-08-20)

```text
정의 ID: architecture:v27-physical-mass-canonical-gram-authority-v1
콘텐츠 종류: ItemDefinitionId·PhysicalMassGrams 어셈블리 소유권 분리, immutable runtime 질량 Query와 1g exact authored projection 교정
정의·카탈로그·실행기 위치: ItemDefinitionId.cs, PhysicalMassContracts.cs, PhysicalItemMassQuery.cs, PhysicalStockQuery.cs, V22ApparelContentAssetBuilder.cs, material:cave-silk/common-wool ItemDefinitionSO
등장 시대와 연구: 모든 물리 아이템과 모든 시대에 공통 적용하며 기존 textile 연구·레시피·해금 순서·콘텐츠 목록은 변경하지 않음
플레이어에게 주는 새 결정: 표시 중량과 실제 운반 의도는 동굴 비단 110g·일반 모직 240g으로 유지됨. float 인접 비트값을 1g canonical 표현으로 고쳐 표시·수량·효능 선택은 바뀌지 않음
물리 BOM·입력·출력: BOM·출력 수량·maxStack·재료 계보 변경 0. generic stack은 positive long gram 단위 질량×수량의 checked 곱만 사용함
직접 작업량과 계산 근거: WU 변경 0. 기존 YAML 0.11000001kg/0.24000001kg는 110g/240g 의도와 다른 float bit pattern이어서 각각 exact 0.11kg/0.24kg projection으로 canonicalize함
EWU와 목표 회수 기간: 가격·Direct WU·회수율은 이번 slice에서 변경하지 않음. float bit 교정과 새 mass source digest 때문에 handling EWU·가격 원장은 후속 전수 재생성 전 stale로 취급함
공간·전력·물·연료·정비: 창고 capacity·공간·전력·유체·정비 수치는 변경하지 않음. gram warehouse admission은 별도 수직 슬라이스 전까지 적용하지 않음
위험·실패·회복 방식: 알 수 없는 item, 1g 격자 밖 float, 0/음수/NaN/Infinity, 곱셈 overflow, save DTO query 입력과 duplicate projector는 fail-loud함. runtime 반올림 fallback은 금지함
사회·비가역 비용: 동일 표시 gram으로의 canonical bit 교정만 수행하므로 자본·노동·운반 횟수 변화는 없음. 이후 carry·haul·창고가 같은 Query로 이관되기 전 전체 중량 완료로 보지 않음
기존 대안과의 장단점: ±0.001g 허용은 기존 두 값을 통과시키지만 future drift를 숨김. exact bit projection은 엄격하나 Editor builder가 origin 계산을 1g으로 canonicalize하므로 재생성 안정성을 유지함
지배 전략 방지 조건: runtime silent rounding 0, Economy↔Items assembly cycle 0, duplicate mass writer 0, unknown definition fallback 0, save DTO gameplay query 0, 변경 없는 kg·WU·가격 적용 0
저장 권위와 실행 명령: immutable ItemDefinitionSO와 instance component가 원본 권위이고 Query/cache/snapshot은 파생값임. generic gram cache를 save에 복제하지 않으며 과거 save migration과 schema 변경은 없음
자동 감사 ID와 전수 목록 포함 여부: V27_MASS_AUTHORITY_ASSEMBLY_EXACT, V27_GENERIC_MASS_QUERY_EXACT_GRAMS, V27_MASS_QUERY_FAILS_LOUD, V27_MASS_QUERY_SAVE_DTO_FREE, 1,060 serialized unitWeight exact-gram inventory를 요구함
검증 매트릭스와 보고서 위치: PhysicalStockQueryV18DebugScenarios, V27PhysicalMassAuthorityInventoryDebugScenarios, Unity full compile, 10,000-op warm-up10/measured100 p95<=2ms, steady allocation 0B, Console Warning/Error 0/0
현재 밸런스 상태: 구조 기반 Gate S0 slice 1 PASS, slice 2 진행 중. carry·haul·UI·stateful mass·warehouse gram admission·WIP·disposition·EWU/가격·6인 생존망·3-seed 전수 검증 전에는 물리 중량 또는 밸런스 완료가 아님
```

## V27 L01 창고 로컬 gram admission 트랜잭션 적용 기록 (2026-08-20)

```text
정의 ID: architecture:v27-l01-warehouse-local-mass-admission-v1
콘텐츠 종류: 일반 stackable 아이템의 창고 입고를 warehouse-local gram 예약·물리 생성·exact 커밋 영수증으로 닫는 첫 production 수직 슬라이스
정의·카탈로그·실행기 위치: WarehouseInventory.cs, PhysicalStockQuery.cs, WarehouseMassAdmissionService.cs, WorldItemWarehouseService.cs, PhysicalStockQueryV18DebugScenarios.cs, PhysicalItemLogisticsPlayModeVerifier.cs
등장 시대와 연구: 모든 시대의 일반 물리 창고 입고에 공통 적용되는 기반 구조이며 연구 해금·콘텐츠 목록·창고 배치 조건은 변경하지 않음
플레이어에게 주는 새 결정: 이번 슬라이스는 새 선택이나 수치 변화를 추가하지 않음. 최종 전환 뒤에는 창고가 남은 kg 한도를 넘는 입고를 부분 승인하거나 거절하고, 다른 창고의 변화는 현재 입고를 불필요하게 무효화하지 않음
물리 BOM·입력·출력: BOM·수량·출력·unitWeight 변경 0. generic stackable lot만 exact item ID·quantity·unit grams로 예약하며 MaxStack=1 unique/stateful 항목은 이 슬라이스에서 typed reject함
직접 작업량과 계산 근거: WU·운반 속도·25kg nominal·19.10/28.65kg 일반 성능 밴드 변경 0. accepted quantity는 remaining grams를 unit grams로 정수 나눗셈한 값과 요청량의 최솟값이며 음수·overflow·0g fallback을 허용하지 않음
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 창고 admission 구조가 실제 물류 경계에 연결됐으므로 stateful lot·전체 유입 전환 뒤 handling EWU와 가격 원장을 재생성하기 전까지 경제 수치는 stale로 취급함
공간·전력·물·연료·정비: 기존 authored positive maxStoredMassGrams와 25,000g L01 권위를 유지함. 공간·전력·상하수·정비·저장 면적 수치는 변경하지 않았고 kg UI·current-format save round-trip은 후속 Gate S1 항목임
위험·실패·회복 방식: token은 Reserved/Committed/Released/Expired/Invalidated terminal 상태를 typed tombstone으로 보존함. 같은 commit ID는 동일 receipt를 반환하고 외부 same-warehouse mutation은 무효화하며 unrelated warehouse mutation은 무효화하지 않음. commit/publication 실패 시 이번 operation이 건드린 warehouse/item record만 복원하며 persistent ID sequence의 완전한 rollback은 후속 공통 트랜잭션 과제로 남김
사회·비가역 비용: 자본·재료·생존 생산량·시설 면적·반복 노동 변화 0. gram admission이 추가되었으나 첫 경로는 synchronous source ingress이고 haul/conveyor/상점/보상 등 전체 유입과 unique item은 아직 기존 경로가 남아 있어 플레이 체감 완료로 보지 않음
기존 대안과의 장단점: repository 전역 revision은 무관한 창고 변화에도 token을 무효화하므로 warehouse-local revision으로 교체함. count-only 입고보다 질량 보존이 강하지만 모든 producer를 한 번에 교체하지 않고 compile-green 수직 슬라이스로 진행해야 회귀 원인을 격리할 수 있음
지배 전략 방지 조건: mass overcommit 0, same-warehouse 외부 변이의 silent commit 0, unrelated-warehouse false invalidation 0, duplicate commit 0, terminal token 부활 0, MaxStack=1 generic 위장 0, publication 실패 후 surviving stock 0을 요구함
저장 권위와 실행 명령: immutable ItemDefinitionSO 질량, warehouse definition capacity, physical repository stock이 원본 권위이고 admission token·receipt·stored/reserved/remaining grams는 파생 트랜잭션 권위임. 현재 token은 synchronous 입고 수명에 한정하며 저장 DTO와 과거 세이브 마이그레이션은 변경하지 않음
자동 감사 ID와 전수 목록 포함 여부: V27_WAREHOUSE_LOCAL_REVISION_ISOLATED, V27_WAREHOUSE_ADMISSION_PARTIAL_RESERVE_EXACT, V27_WAREHOUSE_ADMISSION_COMMIT_RECEIPT_IDEMPOTENT, V27_WAREHOUSE_ADMISSION_RELEASE_TOMBSTONE, V27_WAREHOUSE_ADMISSION_EXPIRED_TOMBSTONE, V27_WAREHOUSE_ADMISSION_EXTERNAL_MUTATION_INVALIDATED, WAREHOUSE_MASS_ADMISSION_PRODUCTION_INGRESS_COMMITTED를 요구하며 Physical coverage transitive source에 admission/query/service/definition 경계를 포함함
검증 매트릭스와 보고서 위치: PhysicalStockQueryV18DebugScenarios focused 전 marker PASS, Artifacts/QA/physical-item-logistics-playmode-report.txt의 RESULT=PASS; failures=0 및 production ingress marker, Unity full compile, Console Warning/Error 0/0. 계획 canonical SHA-256 B2D517FF397BACC8E56B8F12F7C341732526B14744C1E98EB7CDF1623FABD6D3
현재 밸런스 상태: 구조 기반 Gate S1 부분 PASS. generic source ingress의 warehouse-local gram admission과 fresh production PlayMode 증거만 닫혔음. kg UI/current-format save·invalid destination atomic restore·unique/stateful mass·전체 producer/conveyor/haul destination·WIP/disposition·EWU/가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 L01 current-format 창고 복원 권위 적용 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-l01-warehouse-current-format-restore-authority-v2
콘텐츠 종류: architecture:v27-l01-warehouse-local-mass-admission-v1의 후속 physical→detached facility candidate exact restore join과 valid over-capacity 보존 계약
정의·카탈로그·실행기 위치: PhysicalItemsSaveSection.cs, WorldItemStackRuntime.cs, WorldItemPersistenceService.cs, WarehousePhysicalRestoreValidation.cs, WorldItemModels.cs, PhysicalItemLogisticsPlayModeVerifier.cs
등장 시대와 연구: 모든 시대의 물리 Stored warehouse route에 공통 적용하며 연구·해금·창고 종류·과거 세이브 정책은 변경하지 않음
플레이어에게 주는 새 결정: 새 선택이나 수치 변화 0. 지원하는 current-format 저장에서 정상 창고의 초과 적재는 사라지거나 순간이동하지 않고 남으며, 공간이 확보될 때까지 새 입고가 차단됨
물리 BOM·입력·출력: BOM·수량·unitWeight·출력 변경 0. Stored owner는 sourceStorageDestinationId 우선, 없으면 destinationId로 exact 결정하고 quantity×canonical unit grams를 checked 합산함
직접 작업량과 계산 근거: WU·25kg nominal·일반/멜빵 운반 밴드 변경 0. fresh fixture는 restraints 2,500g 단위와 같은 창고의 기존 stock을 합산해 39,300g/25,000g over-capacity를 구성함
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. restore join은 물리 권위만 닫았으며 전체 ingress·handling EWU·가격·SCC 재생성 전 경제 완료로 승격하지 않음
공간·전력·물·연료·정비: L01 authored 25,000g와 기존 공간·전력·유체·정비 수치를 유지함. 초과 14,300g을 일반 바닥이나 다른 창고로 순간 이동시키지 않고 RemainingMassGrams=0으로 투영함
위험·실패·회복 방식: detached facility candidate가 없거나 warehouse owner ID가 orphan/duplicate이거나 category/현재 위치/목적지 위치가 다르면 repository publish 전에 stable typed reason code로 실패함. valid over-capacity는 WarehousePhysicalRestoreAssessment로 구분하고 stock을 보존함
사회·비가역 비용: 자본·재료·생존망 변화 0. over-capacity evacuation publication은 아직 미구현이다. 이번 후속의 완료 범위는 보존+입고 차단이며 수동/AI 정리 행동은 OPEN이다.
기존 대안과의 장단점: JSON preflight 시점에는 detached 시설 후보가 없어 exact owner join이 불가능함. items.physical stage에서 world.facilities 후보를 명시적으로 요구하면 전체 저장 트랜잭션 rollback에 참여할 수 있지만, fixture용 direct Restore와 공식 transactional restore API를 분리해야 함
지배 전략 방지 조건: orphan 창고 승인 0, 좌표 변조 승인 0, category 우회 0, over-capacity 아이템 삭제·순간이동 0, 초과 상태 신규 입고 0, validation 전 live repository mutation 0, direct fixture Restore의 production fallback 사용 0
저장 권위와 실행 명령: current-format physical DTO와 detached modular facility candidate가 exact warehouse ID·position·category로 join하며 physical repository publication은 검증 성공 뒤에만 수행함. 과거 버전 migration·silent fallback·save DTO gameplay query는 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: WAREHOUSE_RESTORE_TRANSACTIONAL_STAGING_AVAILABLE, WAREHOUSE_RESTORE_INVALID_DESTINATION_ATOMIC, WAREHOUSE_RESTORE_POSITION_MISMATCH_ATOMIC, WAREHOUSE_RESTORE_OVER_CAPACITY_PRESERVED, WAREHOUSE_RESTORE_OVER_CAPACITY_ADMISSION_BLOCKED를 Physical coverage required marker로 승격하고 persistence/validator/runtime/save-section source를 freshness 목록에 포함함
검증 매트릭스와 보고서 위치: Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T09:47:35Z, RESULT=PASS; failures=0, over-capacity 39,300/25,000g exact, 신규 spawned=0, Unity compile, Console Warning/Error 0/0. 계획 canonical SHA-256 E29FEF54849ECCCF9D720FDC878ED36C481E082FCAF549F61DFD3329A9875600
현재 밸런스 상태: 구조 기반 Gate S1 restore sub-slice PASS. kg UI·official whole-save V4 round-trip·evacuation publication·unique/stateful mass·warehouse lifecycle·haul/conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 L01 canonical kg UI 적용 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-l01-warehouse-canonical-kg-ui-v3
콘텐츠 종류: architecture:v27-l01-warehouse-current-format-restore-authority-v2의 후속 canonical kg 표시와 count/mass 요약 차원 분리
정의·카탈로그·실행기 위치: WarehouseMassUiFormatter.cs, BuildingSummaryFormatter.cs, BuildingManagementSummaryQuery.cs, BuildingManagementWorldQueryAdapter.cs, WarehouseFeatureSurfacePresenter.cs, UITabContentTextProvider.cs, PhysicalStockQueryV18DebugScenarios.cs, PhysicalItemLogisticsPlayModeVerifier.cs
등장 시대와 연구: 모든 시대의 positive gram capacity 창고 UI에 공통 적용하며 연구·해금·시설 목록·창고 배치 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 선택 0. 창고 화면은 물리 재고를 개수로, 용량을 kg로 구분하고 L01의 실제 authority를 `12kg/25kg`처럼 표시함
물리 BOM·입력·출력: BOM·수량·unitWeight·생산 출력 변경 0. UI는 저장된 물리 stack quantity를 재고 개수로 유지하되 그 값을 kg capacity의 분모로 사용하지 않음
직접 작업량과 계산 근거: WU·25kg nominal·19.10/28.65kg 일반 밴드·23.88/35.81kg 멜빵 밴드 변경 0. positive long grams를 invariant `0.###kg`로 한 번 투영함
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 표시 권위만 연결했으며 전체 ingress·운반·WIP 전환 뒤 handling EWU와 가격을 재생성하기 전 경제 완료로 승격하지 않음
공간·전력·물·연료·정비: authored 25,000g와 기존 공간·전력·유체·정비 수치 변경 0. legacy count-only 창고가 있으면 별도 개수형 용량으로 표시하고 mass 합계와 섞지 않음
위험·실패·회복 방식: mass authority가 없거나 음수 gram이면 formatter가 fail-loud함. presentation adapter가 mass warehouse를 count capacity에 다시 합산하면 focused dimension-separation 회귀가 실패함
사회·비가역 비용: 자본·재료·생존망·저장 실용량 변화 0. UI 교정은 초과 적재 대피나 자동 정리를 구현하지 않는다. 해당 기능은 OPEN이다.
기존 대안과의 장단점: 기존 `/60` count 표시는 단순하지만 gram admission과 단위가 충돌함. 기존 localization template에 canonical kg token을 넣어 번역 asset churn 없이 실제 용량을 보여주고 재고 개수는 별도 현황으로 유지함
지배 전략 방지 조건: count를 kg capacity로 오인하는 표시 0, mass/count 합산 0, locale-dependent decimal 0, negative/unbounded production capacity 표시 0, legacy `/60` 재출현 0
저장 권위와 실행 명령: UI snapshot은 runtime warehouse definition과 physical stock query의 파생값이며 save 권위가 아님. 저장 DTO를 gameplay 질량 Query에 입력하지 않고 current-format restore 계약을 그대로 유지함
자동 감사 ID와 전수 목록 포함 여부: V27_WAREHOUSE_MASS_UI_EXACT_KG, V27_WAREHOUSE_MASS_SUMMARY_DIMENSIONS_SEPARATED, WAREHOUSE_MASS_UI_PRODUCTION_EXACT_KG를 요구하고 관련 formatter/query/adapter/presenter source를 Physical coverage freshness 목록에 포함함
검증 매트릭스와 보고서 위치: focused 두 marker PASS, Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T09:54:35Z의 RESULT=PASS; failures=0과 runtime `12kg/25kg`, legacy `/60` 부재, Unity full compile, Console Warning/Error 0/0. 계획 canonical SHA-256 007582708E58DC0CF7F1D3BE965B058B9C080E923D0403AFD608CD4741DC0EDA
현재 밸런스 상태: 구조 기반 Gate S1 UI sub-slice PASS. official whole-save 왕복·over-capacity evacuation publication·lifecycle·unique/stateful mass·haul/conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 L01 공식 저장·초과 적재 요청 publication 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-l01-warehouse-official-restore-publication-v4
콘텐츠 종류: architecture:v27-l01-warehouse-canonical-kg-ui-v3의 후속 official RestoreAll 질량 왕복, detached over-capacity assessment와 post-root-swap evacuation request publication
정의·카탈로그·실행기 위치: WarehousePhysicalRestoreValidation.cs, WorldItemRepository.cs, WorldItemPersistenceService.cs, WorldItemStackRuntime.cs, WorldItemHaulPlanningService.cs, ItemTransferService.cs, PhysicalItemLogisticsPlayModeVerifier.cs, DungeonSaveSections.cs
등장 시대와 연구: 모든 시대의 current-format 물리 창고 저장·복원과 운반 목적지 선택에 공통 적용하며 연구·해금·시설 목록·과거 세이브 정책은 변경하지 않음
플레이어에게 주는 새 결정: 새 선택 0. 정상 소유 창고의 초과 적재는 전체 저장 복원 뒤 보존되고 신규 입고가 막히며, 정리 대상이라는 요청이 복원 성공 뒤에만 공개됨
물리 BOM·입력·출력: BOM·수량·unitWeight·생산 출력 변경 0. 검증 fixture는 기존 stackable lot을 39,300g까지 늘려 25,000g 창고의 current-format over-capacity를 구성함
직접 작업량과 계산 근거: WU·25kg nominal·일반/멜빵 운반 밴드 변경 0. 창고 목적지 후보는 category exact와 최소 1개분 RemainingMassGrams를 만족해야 하고 이용률은 stored+reserved grams/max grams로 계산함
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 목적지 선택이 mass-aware해졌으나 실제 evacuation haul과 전체 ingress cutover 전 handling EWU·가격 재생성은 계속 보류함
공간·전력·물·연료·정비: L01 authored 25,000g와 기존 시설 면적·전력·유체·정비 수치 변경 0. restored 39,300g의 remaining은 0이며 다른 공간으로 순간이동시키지 않음
위험·실패·회복 방식: assessment는 detached repository candidate에 저장되므로 stage/publish 실패 시 live request 0. root swap 성공 뒤 pending exact 1, 원본 RestoreAll 뒤 pending 0을 요구함. orphan/category/position mismatch는 계속 전체 restore를 거절함
사회·비가역 비용: 자본·재료·생존망 변화 0. pending publication은 실제 물리 이동이 아니며 destination admission token과 정리 haul이 완료되어야 한다. 해당 기능은 OPEN이다.
기존 대안과의 장단점: runtime 일반 필드 publication은 rollback 누수 위험이 있으나 aggregate candidate 소유는 저장 원자성을 보존함. category 복원 검사를 완화하는 대신 오래된 planner/deposit 우회를 제거해 live와 restore 권위를 일치시킴
지배 전략 방지 조건: 실패 restore의 evacuation request 누수 0, root swap 전 publication 0, duplicate pending ID 0, baseline restore 뒤 orphan pending 0, category 우회 저장 0, count-only target saturation 선택 0, over-capacity 삭제·순간이동 0
저장 권위와 실행 명령: current-format physical DTO와 detached facility candidate가 source 권위이고 pending evacuation ID는 repository aggregate의 파생 복원 권위임. 저장 DTO를 gameplay 질량 query에 입력하지 않고 과거 save migration을 추가하지 않음
자동 감사 ID와 전수 목록 포함 여부: WAREHOUSE_RESTORE_OFFICIAL_FULL_ROUNDTRIP, WAREHOUSE_RESTORE_OFFICIAL_OVER_CAPACITY_PRESERVED, WAREHOUSE_RESTORE_EVACUATION_PUBLISHED_AFTER_ROOT_SWAP, WAREHOUSE_RESTORE_EVACUATION_CLEANUP_EXACT를 Physical manifest 필수 marker로 승격함
검증 매트릭스와 보고서 위치: Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T10:17:52Z, RESULT=PASS; failures=0, official 39,300/25,000g exact, pending 1→0, Unity compile, Console Warning/Error 0/0. 계획 canonical SHA-256 D8FE166767208033AD39439201BC620E445999CF9CE3130DE9128CBB3D43CA17
현재 밸런스 상태: 구조 기반 Gate S1 official restore/publication sub-slice PASS. 실제 evacuation haul 완료·lifecycle·unique/stateful mass·haul/conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 L01 초과 적재 실제 AI 대피 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-l01-warehouse-overcapacity-ai-evacuation-v5
콘텐츠 종류: architecture:v27-l01-warehouse-official-restore-publication-v4의 후속 over-capacity pending→outbound physical route→destination gram admission→AIHaul→exact commit 수직 슬라이스
정의·카탈로그·실행기 위치: WorldItemStackRuntime.cs, WorldItemWarehouseService.cs, WorldItemHaulPlanningService.cs, HaulDeliveryIntentRuntime.cs, WarehouseMassAdmissionService.cs, AbilityHaul.cs, ItemTransferService.cs, HaulDeliveryIntentRestoreCoordinator.cs, PhysicalItemLogisticsPlayModeVerifier.cs
등장 시대와 연구: 모든 시대의 positive gram authority 창고와 current-format 초과 적재 정리에 공통 적용하며 연구·해금·시설 종류·과거 세이브 정책은 변경하지 않음
플레이어에게 주는 새 결정: 새 직접 명령 0. 정상 소유 창고가 초과 적재로 복원되면 기존 stock은 보존되고, 다른 합법 창고 여유가 있을 때 일반 AI 운반이 exact lot을 실제로 옮겨 신규 입고 가능 상태를 회복함
물리 BOM·입력·출력: BOM·수량·unitWeight·생산 출력 변경 0. fresh live 증거는 captivity:restraints 6개를 source 39,300g/25,000g에서 target으로 exact 15,000g 이동하고 source를 24,300g으로 낮춤
직접 작업량과 계산 근거: Direct WU·25kg nominal·일반 19.10/28.65kg·멜빵 23.88/35.81kg 밴드 변경 0. 대피 대상은 초과 gram, 대상 창고 RemainingMassGrams, 실제 carry capacity와 path authority로 결정하며 물리 이동 시간을 우회하지 않음
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 실제 haul 이동이 추가된 구조 교정이므로 전체 ingress·conveyor·unique lot 전환 뒤 handling EWU/가격/SCC를 재생성하기 전 경제 완료로 승격하지 않음
공간·전력·물·연료·정비: L01 authored 25,000g와 시설 footprint·전력·유체·정비 수치 변경 0. nearest legal target을 사용하지만 category·gram capacity·delivery authority를 모두 통과해야 하며 바닥 순간이동이나 hidden overflow를 만들지 않음
위험·실패·회복 방식: planner의 fast availability와 full plan이 동일 `warehouse:*` kind authority를 사용함. destination token reserve/renew/commit 실패는 carried physical lot과 owner intent를 보존하거나 transaction rollback하며, 완료 뒤 source/target reserved grams=0·pending=0을 요구함
사회·비가역 비용: 자본·생존 생산량·시설 수 변경 0. 과적 정리는 실제 운반자의 시간과 경로를 사용하므로 물류 노동을 무료화하지 않으며, 다른 일반 입고가 같은 검증 시간에 일어나도 exact evacuation lot 보존으로 판정함
기존 대안과의 장단점: 과적 stock 삭제·Loose 자동 drop·즉시 다른 창고 이동은 단순하지만 질량·시간·소유권을 위반함. exact outbound Stored route와 AIHaul은 비용과 실패가 관찰 가능하나 target 부재 시 pending이 유지됨
지배 전략 방지 조건: source stock 순간이동 0, destination gram overcommit 0, route와 token의 다른 lot 결합 0, duplicate pending 0, 완료 뒤 token/quantity lease 누수 0, unrelated item 입고를 대피 보존으로 오판 0
저장 권위와 실행 명령: current-format physical repository의 pending warehouse ID와 exact Stored lot이 source 권위이고 scheduler가 outbound route를 게시함. haul intent가 destination admission DTO를 소유하며 215 admission participant→225 haul intent coordinator 순서로 복원·rollback하고 AIHaul이 실제 commit함
자동 감사 ID와 전수 목록 포함 여부: WAREHOUSE_EVACUATION_TARGET_READY, WAREHOUSE_EVACUATION_LIVE_FIXTURE_READY, WAREHOUSE_EVACUATION_AI_HAUL_COMPLETED, WAREHOUSE_EVACUATION_GRAM_TOKEN_CONSERVATION_EXACT를 Physical coverage 필수 marker로 승격함
검증 매트릭스와 보고서 위치: Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T11:27:57Z, RESULT=PASS; failures=0, exact 6→6/15,000g→15,000g, source 24,300/25,000g, destination reservations 0, pending 0, Unity compile. 계획 SHA-256 1C4BF82917DECBAC8FC81A6A72A606DB54D4071A0BD89A714DAC3712C2DDD005
현재 밸런스 상태: 구조 기반 Gate S1 actual evacuation haul sub-slice PASS. warehouse demolition/relocation lifecycle·unique/stateful mass·conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망 재검증·3-seed 재검증이 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 L01 창고 empty-only lifecycle 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-l01-warehouse-empty-lifecycle-v6
콘텐츠 종류: architecture:v27-l01-warehouse-overcapacity-ai-evacuation-v5의 후속 positive gram warehouse 철거·이전 empty-only 수명주기 권위
정의·카탈로그·실행기 위치: WarehouseInventory.cs, WarehouseLifecycleOccupancyQuery.cs, GridBuildingRuntime.cs, DungeonStoryGridBuildingController.cs, FacilityRelocationWorldService.cs, DungeonWorldSimulationRegistration.cs, PhysicalItemLogisticsPlayModeVerifier.cs
등장 시대와 연구: 모든 시대의 positive gram capacity 창고 철거·시설 이전에 공통 적용하며 연구·해금·건물 목록·철거 환급·과거 세이브 정책은 변경하지 않음
플레이어에게 주는 새 결정: 새 선택 0. 창고를 철거하거나 이전하려면 물리 재고뿐 아니라 목적지 예약과 운반 중 소유권까지 비워야 하며, 실패 시 어떤 재고·건물·좌표도 변경되지 않음
물리 BOM·입력·출력: BOM·수량·unitWeight·생산 출력·철거 회수량 변경 0. 12,000g live stock 창고는 철거/이전 모두 거절되고 0g/0 token/0 stack/0 intent 창고만 lifecycle gate를 통과함
직접 작업량과 계산 근거: Direct WU·철거 WU·이전 WU·25kg nominal과 운반 밴드 변경 0. lifecycle occupancy는 StoredMassGrams, ReservedInboundMassGrams, destination/source physical reference 수, active haul intent 수를 exact 합성함
EWU와 목표 회수 기간: EWU·가격·환급·회수 기간 변경 0. loaded warehouse lifecycle exploit만 차단했으며 전체 ingress·unique lot·WIP/disposition 뒤 경제 원장을 재생성하기 전 완료로 승격하지 않음
공간·전력·물·연료·정비: footprint·전력·유체·정비·25,000g capacity 변경 0. loaded warehouse를 건물 state module만으로 옮겨 physical stack 좌표와 owner를 분리하거나 orphan destination을 만들 수 없음
위험·실패·회복 방식: occupancy query가 없거나 warehouse ID가 invalid하면 positive gram warehouse mutation을 fail-loud함. not-empty 상세는 stored/reserved/stack/intent 수를 기록하고 철거·이전의 grid/world mutation 전에 평가됨
사회·비가역 비용: 자본·생존 생산량·시설 수치 변화 0. 플레이어는 먼저 실제 운반으로 창고를 비워야 하므로 기존 물류 비용이 보존되며, 숨은 삭제·순간이동으로 이전 비용을 우회하지 못함
기존 대안과의 장단점: StoredMass만 보는 검사는 단순하지만 pre-pick route·carried intent·conveyor/in-transit reference를 놓침. repository+admission+intent join은 엄격하지만 save DTO나 UI snapshot을 새 gameplay 권위로 만들지 않음
지배 전략 방지 조건: loaded demolition 0, loaded relocation 0, orphan warehouse destination 0, inbound token 손실 0, carried commitment 손실 0, empty warehouse false rejection 0, 실패 명령 뒤 stock/building mutation 0
저장 권위와 실행 명령: physical repository와 warehouse admission/haul intent runtime이 가변 권위이고 lifecycle snapshot은 저장하지 않는 read-only projection임. production 명령은 DungeonStoryGridBuildingController.TryDestroyBuilding과 FacilityRelocationWorldService.CanRelocate/TryPackAtDestination임
자동 감사 ID와 전수 목록 포함 여부: WAREHOUSE_NONEMPTY_DEMOLITION_REJECTED, WAREHOUSE_NONEMPTY_RELOCATION_REJECTED, WAREHOUSE_EMPTY_LIFECYCLE_GATE_OPEN을 Physical coverage 필수 marker로 승격하고 lifecycle query/controller/grid/relocation source를 freshness 목록에 포함함
검증 매트릭스와 보고서 위치: Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T11:39:39Z, RESULT=PASS; failures=0, non-empty stored=12,000g accepted=0, empty occupancy 0/0/0/0, actual evacuation 15,000g PASS, Unity compile/Console Error 0. 계획 SHA-256 6EBA862F914FBB7822A915086EECC4CBACE70E23EEEA31EAA7C2A94146FEBF5E
현재 밸런스 상태: 구조 기반 Gate S1 warehouse lifecycle sub-slice PASS. unique/stateful mass·conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망 재검증·3-seed 재검증이 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 전투 장비 동적 질량·창고 입고 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-combat-equipment-dynamic-mass-v7
콘텐츠 종류: generic definition 질량 이후 첫 unique/stateful combat equipment의 base·부착 module·실제 loaded ammunition 질량과 창고 gram admission 수직 슬라이스
정의·카탈로그·실행기 위치: PhysicalMassContracts.cs, PhysicalItemMassQuery.cs, CombatEquipmentPhysicalMassProjector.cs, PhysicalStockQuery.cs, WarehouseMassAdmissionService.cs, WorldItemWarehouseService.cs, WorldItemHaulPlanningService.cs, PhysicalItemLogisticsPlayModeVerifier.cs
등장 시대와 연구: 모든 시대의 물리 전투 장비와 장비 창고 입고에 공통 적용하며 연구·해금·장비 정의·제작법·과거 세이브 정책은 변경하지 않음
플레이어에게 주는 새 결정: 새 직접 선택 0. 장비는 base physical item뿐 아니라 실제 장착 module과 장전된 탄약을 운반·창고 용량에 포함하며, 내구·품질·월드 상태·전하·module 등급/상태 변화는 질량을 바꾸지 않음
물리 BOM·입력·출력: BOM·수량·authored unitWeight·제작 출력 변경 0. focused fixture 질량은 exact `base + item:equipment-module×attachedCount + ammunitionItemId×remaining`; fresh production dagger는 기존 authored 700g 그대로 Stored됨
직접 작업량과 계산 근거: Direct WU·25kg nominal·19.10/28.65kg 일반 밴드·23.88/35.81kg 멜빵 밴드 변경 0. component payload는 adapter 경계에서 1회 decode/identity 검증하고 immutable prepared gram을 생성하며 hot query는 JSON/LINQ/카탈로그 재조회를 하지 않음
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 실제 장비 handling mass 권위만 연결했으며 apparel·멜빵·carcass·packaged lot·전체 ingress 이후 물류 EWU/가격/SCC를 재생성하기 전 경제 완료로 승격하지 않음
공간·전력·물·연료·정비: 창고 25,000g authority와 시설 footprint·전력·유체·정비 수치 변경 0. 실제 attached module/ammunition gram이 StoredMass와 RemainingMass에 포함되므로 장비 내부 질량이 창고 공간에서 무료가 되지 않음
위험·실패·회복 방식: equipment item의 component 누락/중복, item-instance identity 불일치, module slot/attached payload 불일치, 비canonical ammunition, restore reserved gram 불일치는 mutation 전에 fail-loud함. haul-owned cargo의 mass-affecting mutation은 후속 typed reject 회귀 전까지 허용 완료로 간주하지 않음
사회·비가역 비용: 자본·재료·생존 생산량 변화 0. 실제 module/탄약 질량을 정확히 계상해 내부 적재를 무료 운반·무료 저장으로 이용하는 우회를 막되 장비 수치 자체를 재조정하지 않음
기존 대안과의 장단점: 매 query마다 component JSON을 재파싱하면 GC와 catalog lookup이 hot loop를 오염시킴. 검증된 immutable prepared subject는 capture 비용을 경계에 한 번 지불하고 read-side를 O(1) 정수 조회로 만들지만 component revision 때 subject/index를 반드시 재구축해야 함
지배 전략 방지 조건: 장착 module 무료 질량 0, loaded ammunition 무료 질량 0, durability/quality reroll 질량 변화 0, warehouse request와 physical lot fingerprint 불일치 승인 0, restore gram drift 0, query steady allocation 0B
저장 권위와 실행 명령: physical equipment component와 exact item instance가 mutable source이고 prepared subject는 저장하지 않는 immutable runtime projection임. warehouse token은 item/instance/lot fingerprint/grams를 소유하고 restore 시 physical component에서 subject를 재구축해 saved grams와 대조함
자동 감사 ID와 전수 목록 포함 여부: V27_COMBAT_EQUIPMENT_DYNAMIC_MASS_EXACT, V27_COMBAT_EQUIPMENT_NON_MASS_STATE_INVARIANT, V27_COMBAT_EQUIPMENT_WAREHOUSE_ADMISSION_EXACT, V27_COMBAT_EQUIPMENT_MASS_QUERY_10000_OP_P95, V27_COMBAT_EQUIPMENT_MASS_QUERY_STEADY_ALLOC_0B, COMBAT_EQUIPMENT_STATEFUL_WAREHOUSE_MASS_EXACT를 요구하고 projector/query/warehouse/haul source를 Physical freshness에 포함함
검증 매트릭스와 보고서 위치: focused current-source PASS(10,000 operation p95≤2ms, allocation 0B), Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T12:22:56.1216752Z, RESULT=PASS; failures=0, dagger projected/base 700g, UnitWeight 0.7kg, reserved 0g, Coverage Physical LiveExecuted, Unity Console Warning/Error 0/0. 계획 canonical SHA-256 E9DA89E64FFEAFD3B095C9A021046AC850DFBA1FA5BB9BF511BB3907A7811DB4
현재 밸런스 상태: 구조 기반 Gate S0 slice 3a combat-equipment dynamic mass PASS. apparel·멜빵 1,150g once·carcass·packaged lot·conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망·3-seed와 전체 coverage fresh sweep가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 의복·멜빵 착용 질량 단일 권위 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-apparel-equipped-mass-single-authority-v8
콘텐츠 종류: combat-equipment 동적 질량 이후 apparel component·material projection·equipped burden과 hauling harness 1,150g 단일 계상 수직 슬라이스
정의·카탈로그·실행기 위치: PhysicalMassContracts.cs, CombatEquipmentPhysicalMassProjector.cs, ApparelPhysicalMassProjector.cs, EquippedApparelPhysicalMassQuery.cs, ApparelItemStateCodec.cs, CharacterCarryInventory.cs, CharacterCarryPresentation.cs, PhysicalStockQueryV18DebugScenarios.cs, PhysicalItemDebugScenarios.cs
등장 시대와 연구: 모든 시대의 물리 의복과 착용 상태에 공통 적용하며 연구·해금·의복 정의·제작법·멜빵 25% capacity bonus는 변경하지 않음
플레이어에게 주는 새 결정: 새 선택 0. 착용한 의복은 실제 물리 질량을 운반 부담에 포함하고, 멜빵은 capacity를 25% 늘리지만 자기 질량 1,150g도 정확히 한 번 부담함
물리 BOM·입력·출력: BOM·수량·authored unitWeight·제작 출력 변경 0. 멜빵 physical definition 1,150g이 world/carry/warehouse/equipped의 단일 질량이고 legacy apparel baseWeight·material multiplier는 runtime 질량을 더 쓰지 않음
직접 작업량과 계산 근거: Direct WU·25kg nominal·일반 19.10/28.65kg·멜빵 23.88/35.81kg 목표 변경 0. focused fixture의 cargo 11,250g+equipped harness 1,150g=12,400g이며 harness gram의 이중 계상은 0
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. apparel handling burden만 단일화했으며 carcass·packaged lot·전체 ingress 이후 물류 EWU/가격/SCC를 재생성하기 전 경제 완료로 승격하지 않음
공간·전력·물·연료·정비: 창고 25,000g·시설 footprint·전력·유체·정비 수치 변경 0. 착용 의복은 물리 instance가 Carried/equipped owner에 있으므로 창고 Stored mass와 동시에 이중 계상되지 않음
위험·실패·회복 방식: Apparel component 누락/중복, schema·instance identity 불일치, equipped owner destination 불일치, physical stack 누락은 fail-loud함. material·quality·내구·오염·수분·fit 변화는 gram을 바꾸지 않음
사회·비가역 비용: 자본·재료·생존 생산량 변화 0. 멜빵의 편의성은 capacity bonus와 자기 질량을 함께 지불하며 필수 생존 장비나 무료 운반 강화로 만들지 않음
기존 대안과의 장단점: apparel baseWeight×material multiplier는 재질 표현이 쉽지만 physical UnitWeight와 이중 권위가 됨. exact physical definition projection은 저장·운반·창고와 일치하지만 의복별 실제 질량 변경은 physical item authority에서만 authoring해야 함
지배 전략 방지 조건: 멜빵 자기 질량 무료 0, 멜빵 이중 계상 0, material quality reroll 질량 변화 0, equipped aggregate만 있고 physical instance 없는 유령 질량 승인 0, component 없는 unique apparel generic carry 0
저장 권위와 실행 명령: physical Apparel component와 exact item instance가 mutable source이고 equipped apparel aggregate는 owner/layer projection임. EquippedApparelPhysicalMassQuery는 저장하지 않는 revision-index read model이며 current-format restore 뒤 exact physical join으로 재구축됨
자동 감사 ID와 전수 목록 포함 여부: V27_APPAREL_PHYSICAL_MASS_EXACT, V27_APPAREL_NON_MASS_STATE_INVARIANT, V27_APPAREL_MATERIAL_WEIGHT_PROJECTS_PHYSICAL_AUTHORITY, V27_HAULING_HARNESS_1150G_COUNTED_ONCE, V27_EQUIPPED_APPAREL_AND_CARGO_BURDEN_EXACT를 focused evidence로 요구함
검증 매트릭스와 보고서 위치: focused apparel/harness 5 markers PASS, Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T12:49:29.8397855Z, RESULT=PASS; failures=0, carry UI 2.4/19.13/28.69kg, full logistics/repair/expedition/restore/evacuation PASS, Unity Console Warning/Error 0/0
현재 밸런스 상태: 구조 기반 Gate S0 slice 3b apparel·멜빵 mass PASS. carcass·packaged lot·exact-instance retail receipt·conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망·3-seed와 전체 coverage fresh sweep가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 야생동물 사체 질량·물리 정의 권위 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-wildlife-carcass-mass-authority-v9
콘텐츠 종류: 18개 live wildlife species의 CarcassWeight와 physical carcass item gram exact join, 누락 definition deterministic authoring, 사체 생성 fail-loud 수직 슬라이스
정의·카탈로그·실행기 위치: WildlifeSpeciesSO.cs, WildlifeModels.cs, WildlifeCarcassPhysicalMassProjector.cs, CombatEquipmentPhysicalMassProjector.cs, UnifiedItemDefinitionAssetBuilder.cs, WildlifeCarcassService.cs, PhysicalStockQueryV18DebugScenarios.cs, WildlifeAiHuntPlayModeVerifier.cs
등장 시대와 연구: authored wildlife가 출현하는 모든 시대에 공통 적용하며 종 spawn·연구·사냥 작업·도축 시설 해금은 변경하지 않음
플레이어에게 주는 새 결정: live species 사망 시 실제 무게의 사체가 반드시 남아 운반·저장·도축 대상으로 보임. 새 명령이나 선택은 추가하지 않음
물리 BOM·입력·출력: 18개 live species 중 누락된 physical carcass definition 13개를 추가함. 각 unitWeight는 exact species.CarcassWeight이며 수량 1·maxStack 1이다. 기존 종 체력·도축 산출 수량·사체 질량 값은 변경하지 않음
직접 작업량과 계산 근거: 사냥·운반·도축 Direct WU와 25kg nominal/운반 밴드 변경 0. 사체 운반량은 exact authored gram과 actor capacity로 계산하며 18~28kg 사체는 일반 actor의 heavy/overload 구간을 실제 사용함
EWU와 목표 회수 기간: EWU·가격·회수 기간은 아직 재생성하지 않음. 신규 13개 definition이 item graph 분모를 바꿨으므로 old 413-item ledger/source digest/approval은 stale이며 새 AuditOnly 전 경제 완료로 승격하지 않음
공간·전력·물·연료·정비: 시설 footprint·전력·유체·정비 수치 변경 0. 사체는 physical Loose/Stored mass와 창고 gram capacity를 사용하고 hidden biological slot이나 virtual storage를 추가하지 않음
위험·실패·회복 방식: live species에 physical definition이 없거나 species/item gram이 다르면 startup/projector가 fail-loud함. 사냥 spawn도 warning 후 소멸하지 않고 invariant failure로 중단함. 부패·도축 atomic output failure는 후속 Transform slice pending
사회·비가역 비용: 기존 missing species가 공짜로 소멸하던 물질·도축 obligation을 복구함. live species의 사체가 늘어나는 만큼 물류·저장 부담이 현실화되며 이를 무료 삭제 fallback으로 완화하지 않음
기존 대안과의 장단점: death path fail-soft는 AI 예외 전파를 줄이지만 물질과 생산 기회를 삭제함. content startup exact join은 오류를 조기에 막지만 모든 species definition을 물리 item catalog와 함께 authoring해야 함
지배 전략 방지 조건: missing carcass silent despawn 0, species/item mass mismatch 0g, freshness·living-state 질량 변동 0, stackable carcass 0, virtual carcass storage 0, legacy-only 종을 live 분모로 오인 0
저장 권위와 실행 명령: WildlifeSpeciesSO.CarcassWeight와 physical ItemDefinitionSO.UnitWeight는 builder가 동기화하고 projector가 exact equality를 강제함. physical stack이 world/save source이고 freshness save는 시간 projection만 소유함
자동 감사 ID와 전수 목록 포함 여부: V27_WILDLIFE_CARCASS_SPECIES_ITEM_MASS_EXACT, V27_WILDLIFE_CARCASS_PREPARED_SUBJECT_EXACT, HUNT_CARCASS_EXACTLY_ONCE를 요구함. 18 live/20 total carcass definition, live missing 0, legacy-only 2를 별도 보고함
검증 매트릭스와 보고서 위치: focused exact markers PASS, Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T13:04:28.5940696Z RESULT=PASS; failures=0, Artifacts/QA/wildlife-ai-hunt-playmode.txt UTC 2026-08-20T13:07:54.5406862Z HUNT_CARCASS_EXACTLY_ONCE/RESULT PASS, Console Warning/Error 0/0. 계획 SHA-256 C42A4BF922C366DD49DDD06942A38D5A91708411BE478E6D6E1D6374B08F29DC
현재 밸런스 상태: 구조 기반 Gate S0 slice 3c carcass mass/spawn PASS. Phase 0 item inventory 재캡처, 부패·도축 atomic Transform, packaged lot·retail exact-instance·conveyor/all ingress·WIP/disposition·EWU/가격·6인 생존망·3-seed와 전체 coverage fresh sweep가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 야생동물 사체 원자 변환 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-wildlife-carcass-atomic-transform-v10
콘텐츠 종류: wildlife carcass mass authority 이후 부패·일반 도축·비상 인체 도축의 source-first 삭제를 제거한 exact physical Transform 수직 슬라이스
정의·카탈로그·실행기 위치: PhysicalItemTransformService.cs, WildlifeCarcassService.cs, PhysicalItemMassQuery.cs, WorldItemSpawner.cs, WorldItemRepository.cs, PhysicalStockQueryV18DebugScenarios.cs
등장 시대와 연구: 기존 wildlife 출현·도축 및 비상 도축 해금에 공통 적용하며 종·연구·시설·행동 가용성은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 도축 산출 권위가 없거나 출력 질량이 입력보다 크거나 output definition이 누락된 사체는 조용히 사라지지 않고 물리 source로 남음
물리 BOM·입력·출력: authored carcass·yield 수량·unitWeight 변경 0. positive yield 5종은 exact source 1개를 기존 authored output으로 변환하고, yield가 비어 있는 13종은 output 0이므로 mutation 전 거절함
직접 작업량과 계산 근거: 도축 WU·사냥 WU·25kg nominal·19.1/28.65kg 및 23.88/35.81kg 운반 band 변경 0. input/output mass는 공용 immutable mass subject/query에서 integer gram으로 합산함
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. receipt가 input/output/loss gram을 노출하지만 전체 disposition ledger와 경제/SCC 재생성 전에는 밸런스 완료로 승격하지 않음
시간·신선도·확률: 기존 360초 freshness와 확정 butcher yield를 유지함. expiry/도축은 동기 exact-once transaction이며 재시도 때 source가 이미 없으면 재생성하지 않음
공간·전력·물·연료·정비: output position과 Loose state는 기존 계약 유지, 시설 footprint·전력·유체·정비 변경 0. FacilityOutputBuffer·2~4 batch capacity와 WIP는 후속 production slice임
위험·실패·회복 방식: source 부재·예약·Carried/InTransit, output definition/instance authority 누락, output mass 초과는 mutation 전 typed failure. 예상 밖 partial spawn은 새 stack 제거와 기존 merge 수량 복원 뒤 source를 보존함
사회·비가역 비용: 기존 생태·기분·금기·인체 도축 결과는 유지함. 사체 삭제나 부분 yield로 물류/사회 비용을 회피할 수 없으며 biological trimming은 non-negative loss gram으로 명시됨
기존 대안과의 장단점: DeleteStack 후 Spawn은 단순하지만 output failure 때 질량을 삭제함. preflight+output-first+source-last는 엄격하고 receipt가 남지만 아직 global WIP/save disposition authority는 아님
지배 전략 방지 조건: input-first deletion 0, output mass creation 0g, partial yield 0, failed transform source loss 0, empty-yield carcass deletion 0, duplicate retry output 0
저장 권위와 실행 명령: current physical repository stack과 species/item definitions가 source이고 receipt는 commit 결과 projection임. freshness save는 남은 시간만 소유하며 transform 완료 후에만 freshness entry를 제거함
자동 감사 ID와 전수 목록 포함 여부: V27_WILDLIFE_CARCASS_TRANSFORM_ATOMIC, V27_WILDLIFE_CARCASS_TRANSFORM_MASS_RECEIPT_EXACT, V27_WILDLIFE_CARCASS_TRANSFORM_FAILURE_PRESERVES_SOURCE를 focused evidence에 추가함. authority inventory는 ledger 413, catalog/serialized 1,060, writers 18, unknown 0으로 재캡처함
검증 매트릭스와 보고서 위치: focused contracts PASS, Artifacts/QA/wildlife-ai-hunt-playmode.txt UTC 2026-08-20T13:21:29.7885162Z RESULT=PASS, Artifacts/QA/physical-item-logistics-playmode-report.txt UTC 2026-08-20T13:23:33.4290366Z RESULT=PASS; failures=0, Console Warning/Error 0/0. 계획 SHA-256 85626681A82BA581DA64D3289563C00FB9E0B3E90E878E2DF1AE4DEF3BCCB267
현재 밸런스 상태: Gate S0 slice 3c carcass mass/spawn/atomic transform PASS. packaged lot·retail exact-instance·conveyor/all ingress·global WIP/disposition·EWU/가격·6인 생존망·3-seed와 전체 coverage fresh sweep가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 컨베이어 창고 gram admission 후속 기록 (2026-08-20)

```text
정의 ID: architecture:v27-conveyor-warehouse-gram-admission-v11
콘텐츠 종류: 컨베이어 overflow의 count-only 창고 입고를 exact physical lot·full quantity gram transaction으로 교정한 수직 슬라이스
정의·카탈로그·실행기 위치: ConveyorItemGateway.cs, ConveyorRuntime.cs, ItemTransferService.cs, WarehouseMassAdmissionService.cs, PhysicalStockQuery.cs, PhysicalStockQueryV18DebugScenarios.cs
등장 시대와 연구: 컨베이어와 창고를 사용할 수 있는 기존 산업 연구 단계에 공통 적용하며 연구·시설 해금·벨트 속도·창고 authored 25,000g은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 예약 창고가 full payload gram을 수용하지 못하면 compatible warehouse 또는 기존 overflow 정책을 사용하며, 수량 capacity만 남았다는 이유로 과적 입고하지 않음
물리 BOM·입력·출력: item unit gram·BOM·stack quantity·output 변경 0. 기존 InTransit physical stack 하나를 그대로 Stored로 전환하며 새 stack 생성·분할·삭제 0
직접 작업량과 계산 근거: conveyor throughput·전력·정비 WU·25kg actor nominal 변경 0. focused fixture는 lumber 1,200g 기준 2개=2,400g full commit과 21개=25,200g/25,000g partial rejection을 exact integer gram으로 검증함
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. count 우회로 무료 저장 공간을 얻던 경로만 차단하며 전체 ingress·물류 EWU 재생성 전 경제 완료로 승격하지 않음
시간·처리량·대기: conveyor route/tick/stall/overflow 시간 변경 0. destination admission token은 동기 transit completion 경계에서 reserve·commit/release되고 잔여 token은 0이어야 함
공간·전력·물·연료·정비: warehouse authored gram capacity와 category filter를 사용하며 count capacity는 admission 권위가 아님. 컨베이어 시설 footprint·전력·유체·정비 수치는 그대로 유지
위험·실패·회복 방식: full quantity보다 작은 partial token은 즉시 release하고 payload 전량을 InTransit으로 보존함. 물리 Stored 전환 후 commit failure는 이전 position/state/destination/source/reservation metadata로 rollback하며 silent count fallback은 없음
사회·비가역 비용: 기존 산업 자동화 선택과 수동 overflow 승인 비용을 유지함. 과적 화물의 순간 삭제·부분 증발·무권위 loose 전환을 허용하지 않음
기존 대안과의 장단점: count CanStore는 단순하지만 unit mass와 unique component를 무시함. exact lot gram transaction은 정합성을 보장하지만 창고 포화 시 기존 overflow/정리 동선 비용이 실제로 드러남
지배 전략 방지 조건: count-only Stored ingress 0, partial payload 입고 0, failed commit unaccounted Stored 0, residual reserved grams 0, stateful component mass 우회 0, retry operation 충돌 0
저장 권위와 실행 명령: conveyor payload ID와 physical InTransit stack이 source 권위이고 warehouse admission token/receipt가 destination gram commitment를 소유함. token은 저장 권위가 아니며 current physical/conveyor restore 뒤 재계획함
자동 감사 ID와 전수 목록 포함 여부: V27_CONVEYOR_WAREHOUSE_MASS_ADMISSION_EXACT, V27_CONVEYOR_PARTIAL_MASS_REJECT_PRESERVES_TRANSIT를 focused evidence에 포함함. live caller는 ConveyorRuntime.TryDischargeOverflow→ConveyorItemGateway→IItemTransferService exact 1경로임
검증 매트릭스와 보고서 위치: focused contracts PASS, Artifacts/QA/physical-item-logistics-playmode-report.txt fresh RESULT=PASS; failures=0, captured Warning/Error 0/0. Temp/IndustrialInfrastructure/playmode-live-report.txt도 기존 씬 노드 2개를 포함한 28/28 cyclic deadlock·exact Loose overflow release·screenshot 뒤 result=PASS. 계획 SHA-256 4C13E9103385E1108E88BE5495743069D09CE9AC037AA32A5756999E3E94841F
현재 밸런스 상태: 구조 기반 conveyor warehouse ingress slice PASS, 밸런스 영향 없음. packaged lot·exact-instance retail·나머지 warehouse ingress·WIP/disposition·EWU/가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 생산 WIP 부분 출력·공정 유체 질량 보존 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-production-wip-fluid-terminal-mass-v11
콘텐츠 종류: 생산 cycle의 exact WIP input·공정 유체·부분 출력·취소/시설 소실 terminal 손실을 하나의 current-format 질량 보존식으로 닫는 구조 수직 슬라이스
정의·카탈로그·실행기 위치: ProductionBillModels.cs, ProductionAggregateState.cs, ProductionBillStateCodec.cs, ProductionBillRuntime.cs, ProductionCycleUtilityService.cs, ProductionAssemblyBridge.cs, ProductionAssemblyBridgeAdapter.cs, ProductionEconomyDebugScenarios.cs
등장 시대와 연구: 기존 ProductionRecipeSO가 실행되는 모든 시대와 연구 단계에 공통 적용하며 연구·시설·레시피 해금은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 작업 중 주문 취소 또는 시설 소실 시 이미 나온 물리 출력과 배출된 폐수는 보존하고, 아직 제품으로 나오지 않은 잔여 질량만 명시적 비가역 공정 손실로 기록함
물리 BOM·입력·출력: authored BOM·출력 수량·item unitWeight 변경 0. baseline의 공정 유체 `1 authored unit=500g`을 사용하며 focused 식은 `3000g 고체+100g 상수=1000g 출력+50g 폐수+2050g 손실`임
직접 작업량과 계산 근거: Direct WU·cycle 시간·확률·25kg nominal·운반 band 변경 0. output gram은 실제 standard FacilityBuffer/apparel/surgical-part commit authority에서 읽고 quantity와 함께 exact 증가함
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. terminal loss가 명시돼 hidden free return 또는 silent deletion을 막지만 linked-support/manual-container/byproduct와 전체 SCC 재생성 전 경제 완료로 승격하지 않음
시간·확률·재시도: 확률 출력은 기존처럼 cycle당 한 번 resolve·저장하고 재시도/복원에서 재굴림하지 않음. partial output commit ID와 gram은 저장되며 terminal receipt retry는 동일 payload만 idempotent함
공간·전력·물·연료·정비: 시설 footprint·출력 버퍼·전력·연료·배관 처리량 수치 변경 0. 시설 ability와 recipe의 동일 시설 clean-water/wastewater는 소비 전에 합산하며 필수 linked support는 첫 유체 mutation 전에 전수 resolve함
위험·실패·회복 방식: 음수 remainder, long overflow, 1g receipt drift, 비정규 ID, 저장 필드 불일치는 bill 제거 전 fail-loud함. current-format 누락은 호환 fallback 없이 restore를 원자 거절함
사회·비가역 비용: 기존 기분·사회·의료·생존 효과 변경 0. 취소로 이미 소비된 재료를 무료 반환하지 않고, 이미 물리 발행된 출력도 삭제하지 않아 플레이어 주의력 비용과 비가역 손실이 명시적으로 보임
기존 대안과의 장단점: partial output을 무기한 차단하면 bill 교착과 시설 잔해를 남기고, input 전량 손실 처리하면 이미 나온 출력 질량을 이중 계상함. 보존식 terminalization은 정확하지만 network-wide fluid transaction과 wastewater 조성 권위가 추가로 필요함
지배 전략 방지 조건: 취소 원재료 무료 반환 0, partial output 삭제 0, output+loss 이중 계상 0g, 저장 재굴림 0, 1g 질량 생성 0, 지원시설 누락 뒤 주 시설 유체 선소비 0
저장 권위와 실행 명령: ProductionBillRecord가 active cycle input/fluid/resolved-output gram의 유일한 가변 권위이고 DungeonProductionBillSaveData V11은 직렬화 경계임. 제거 후 bounded terminal receipt가 bill/recipe/facility/cycle/input/output/fluid/loss를 보존함
자동 감사 ID와 전수 목록 포함 여부: focused production 계약이 output-free cancel, partial-output cancel, partial-output missing facility, V11 byte-exact restore, 1g tamper rejection, real ProductionCycleUtilityService aggregate call을 검사함
검증 매트릭스와 보고서 위치: fresh Unity clean compile, ProductionEconomyDebugScenarios.RunAll, ProductionWorkshopDebugScenarios.Run, IndustrialInfrastructureDebugScenarios.RunAll, EnvironmentalFieldDebugScenarios.RunAll, SurgeryDebugScenarios.RunAll(true) PASS; Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 영향 없음 / 구조 기반 Production V11 WIP terminal mass slice PASS. linked-support network-wide atomic mutation, manual-container exact lot provenance, typed wastewater/byproduct composition, 전체 물리 PlayMode·SaveLoad·EWU/가격·6인 생존망은 pending이므로 물리 중량 또는 밸런스 완료가 아님
```

## V27 연결 지원시설 배관 유체 원자 처리 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-linked-support-piped-fluid-atomic-batch-v11
콘텐츠 종류: 생산 시설·레시피·연결 지원시설의 상수 소비와 폐수 배출을 하나의 배관망 transaction으로 묶는 구조 교정
정의·카탈로그·실행기 위치: IndustrialInfrastructureModels.cs, FluidNetworkRuntime.cs, ProcessFluidUseRuntime.cs, ProductionCycleUtilityService.cs, DungeonWorldSimulationRegistration.cs, FluidNetworkBatchDebugContract.cs, IndustrialInfrastructureDebugScenarios.cs
등장 시대와 연구: 기존 배관·생산 지원시설이 해금되는 모든 시대에 적용하며 연구, 시설, 레시피 해금은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 한 cycle에 필요한 연결 지원시설까지 모두 공급 가능한 경우에만 배관 자원이 함께 소비되며 일부 시설만 먼저 물을 빼는 숨은 실패가 사라짐
물리 BOM·입력·출력: authored BOM, item 수량, 출력, unitWeight 변경 0. 기존 clean-water/wastewater authored unit과 Production V11의 500g 환산을 그대로 사용함
직접 작업량과 계산 근거: Direct WU, cycle 시간, 생산량, 25kg nominal, 운반 band 변경 0. demand 정렬은 수질 엄격도, persistent node ID, 입력 ordinal의 결정론적 순서임
EWU와 목표 회수 기간: EWU, 가격, 회수 기간 변경 0. 부분 debit을 제거해 실패 cycle의 무료/유실 자원 왜곡만 막으며 manual-container와 wastewater 조성이 닫히기 전 경제 완료로 승격하지 않음
시간·확률·재시도: 확률 출력과 WIP cycle sequence는 변경하지 않음. 배관 batch 실패는 revision을 올리지 않고 동일 cycle이 나중에 재시도할 수 있음
공간·전력·물·연료·정비: 시설 footprint, 배관 용량, 저장량, 폐수 용량, 전력, 정비 수치 변경 0. 모든 network water debit과 wastewater credit을 가상 원장에서 먼저 검증함
위험·실패·회복 방식: consumer 누락, 잘못된 수질, NaN/Infinity/음수, network 부재, aggregate water 부족, aggregate wastewater 초과는 mutation 전에 typed failure. 성공은 모든 debit/credit 뒤 revision을 정확히 한 번 증가시킴
사회·비가역 비용: 생존, 기분, 의료, 관계 수치 변경 0. 실패한 생산 cycle이 일부 지원시설의 물만 영구 소비하는 비가역 비용을 제거함
기존 대안과의 장단점: 지원시설별 순차 호출은 단순하지만 뒤쪽 실패 시 앞쪽 debit을 되돌릴 권위가 없음. network batch는 exact하지만 수동 물통의 physical lot와 용기·부산물은 별도 transaction이 필요함
지배 전략 방지 조건: aggregate shortage partial debit 0, wastewater partial credit 0, 성공 revision 다중 증가 0, support 순서별 결과 차이 0, missing support 선소비 0
저장 권위와 실행 명령: FluidNetworkAggregateState가 piped water/wastewater의 유일한 mutable authority이고 ProductionBillRecord V11은 cycle별 gram receipt를 소유함. batch reservation은 별도 저장하지 않고 동기 commit 결과만 WIP에 기록함
자동 감사 ID와 전수 목록 포함 여부: FluidNetworkBatchDebugContract를 IndustrialInfrastructureDebugScenarios.RunAll 및 current balance contracts에 포함하고 production receipt aggregation과 missing-support zero-call 회귀를 유지함
검증 매트릭스와 보고서 위치: 같은 망의 3+3 demand/5 supply는 water·wastewater·revision 무변경 실패, 6 supply는 water 0·wastewater 2·revision +1 성공. fresh clean Unity compile과 Industrial, Production Economy, Production Workshop, Environmental Field, Surgery suites PASS; Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 영향 없음 / linked-support piped-fluid 구조 검증 PASS. manual-container exact-lot provenance, typed wastewater/byproduct composition, 전체 물리 PlayMode·SaveLoad·EWU/가격·6인 생존망은 pending이므로 물리 중량 또는 밸런스 완료가 아님
```

## V27 수동 식수 exact-lot·공정 reserve 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-manual-clean-water-exact-lot-v12
콘텐츠 종류: 생산 공정의 수동 식수 fallback을 Water 카테고리 개수 소비에서 exact physical lot·pending Transfer·fluid reserve·production receipt로 교정한 구조 수직 슬라이스
정의·카탈로그·실행기 위치: FluidNodeState.cs, IndustrialInfrastructureModels.cs, FluidNetworkRuntime.cs, ProcessFluidUseRuntime.cs, ProductionBillModels.cs, ProductionAggregateState.cs, ProductionBillStateCodec.cs, ProductionBillRuntime.cs, ProductionCycleUtilityService.cs, PhysicalItemDebugScenarios.cs
등장 시대와 연구: 기존 수동 식수 fallback이 허용되는 생산·의료 시설에 공통 적용하며 연구, 시설, 레시피, fallback 허용 여부는 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 목적지에 같은 Water 카테고리 물품이 있어도 exact clean-water lot이 없으면 공정을 시작하지 않으며, 부족한 exact 물만 기존 배송 경로로 요청함
물리 BOM·입력·출력: authored item/BOM/출력 변경 0. `resource:clean-water` 1개는 기존 0.5L·500g bulk unit이며 포장 tare 0. 0.2 authored unit 사용 시 한 physical unit을 reserve로 Transfer하고 0.8 unit/400g-equivalent를 보존함
직접 작업량과 계산 근거: Direct WU, cycle 시간, 25kg nominal, 운반 band 변경 0. exact source quantity와 input gram은 Physical V10 pending batch receipt에서 읽고 process consumed gram은 기존 500g/authored-unit 규칙으로 계산함
EWU와 목표 회수 기간: EWU, 가격, 회수 기간 변경 0. 잘못된 Water-category 소비와 중복 재시도만 막으며 typed wastewater/byproduct 및 전체 원장 재생성 전 경제 완료로 승격하지 않음
시간·확률·재시도: 동일 operation retry는 같은 physical commit/source lot을 재생하고 reserve를 두 번 적립하지 않음. operation payload가 다르면 mutation 없이 conflict로 거절하며 생산 aggregate 기록 뒤 명시적으로 acknowledge함
공간·전력·물·연료·정비: 배관·저장·FacilityBuffer 용량과 수동 물 배송량 변경 0. bulk 물의 남은 질량은 FluidNodeState.ManualWaterReserve가 소유함
위험·실패·회복 방식: exact item 부족, 예약된 source, destination 불일치, 비정규 operation, retry conflict, current-format 필드 누락은 fail-loud. Fluid V5는 pending operation/commit/source stack/input gram/applied 상태를 저장하고 restore 후 동일 acknowledgement를 허용함
사회·비가역 비용: 생존, 기분, 질병, 오염 수치 변경 0. 같은 카테고리 미끼 물품의 무단 소비와 save/retry 물 복제를 제거함
기존 대안과의 장단점: category consume은 단순하지만 clean/unsafe/기타 물 identity를 잃음. exact pending Transfer는 provenance와 replay를 보장하지만 wastewater 조성별 downstream 처리는 후속 권위가 필요함
지배 전략 방지 조건: same-category decoy 소비 0, physical lot 중복 소비 0, reserve 중복 credit 0, 500g terminal deletion 0, 빈 용기 무료 생성 0, conflicting replay mutation 0
저장 권위와 실행 명령: physical pending receipt가 source custody, Fluid V5가 reserve/pending state, Production V12가 cycle별 manual source provenance를 소유함. 과거 세이브 마이그레이션은 없고 비현재 버전은 typed restore failure임
자동 감사 ID와 전수 목록 포함 여부: `manual_water_exact_lot_pending_transfer` focused row가 exact item/500g/source stack/conflict/replay/V5 restore/ack/process batch를 검사함. Production V12 focused save는 manual operation/physical commit/source stack/gram을 검증함
검증 매트릭스와 보고서 위치: fresh Unity compile, focused actual-runtime PASS `cleanWaterInput=500g; processUse=100g; reserve=400g; decoyPreserved=1; replayDelta=0; pendingAfterAck=0; processBatchExact=1`; ProductionEconomyDebugScenarios.RunAll 및 IndustrialInfrastructureDebugScenarios.RunAll PASS; Console Warning/Error 0/0. 계획 SHA-256 C3B52C83A15B5C3C4C1DB4EB80283D145B1585DC328B2A03E1EFE13BC7966A5E
현재 밸런스 상태: 밸런스 영향 없음 / manual clean-water exact-lot 구조 검증 PASS. typed wastewater/byproduct composition, 남은 disposition outbox, 전체 물리 PlayMode·SaveLoad·EWU/가격 재생성이 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 공정 폐수 조성·질량 provenance 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-typed-process-wastewater-composition-v13
콘텐츠 종류: 생산 공정 폐수의 hydraulic aggregate와 recipe·facility·support별 조성/질량 provenance를 분리한 구조 수직 슬라이스
정의·카탈로그·실행기 위치: ProductionBillModels.cs, ProductionAggregateState.cs, ProductionBillStateCodec.cs, ProductionRecipeSO.cs, IndustrialInfrastructureBuildingAbilities.cs, ProductionWorkshopAbilities.cs, ProductionCycleUtilityService.cs, ProcessFluidUseRuntime.cs, ProductionEconomyDebugScenarios.cs, PhysicalItemDebugScenarios.cs
등장 시대와 연구: 기존 생산·식품·발효·의료·산업·농업 공정 전 시대에 적용하며 연구·해금·시설 가용성은 변경하지 않음
플레이어에게 주는 새 결정: 새 직접 명령 0. 폐수량이 같아도 위생 세척수·식품 세척수·유청·염수·발효 폐수·의료 폐수·산업 폐수·농업 유출수의 source provenance를 보존해 후속 처리 정책이 정확한 조성을 선택할 수 있게 함
물리 BOM·입력·출력: authored BOM·입출력 수량·clean-water·wastewater authored unit 변경 0. 비영점 authored source 43개를 explicit composition으로 분류했고 missing 0이며 `1 authored fluid unit = 500g` 기존 권위로 component gram을 산출함
직접 작업량과 계산 근거: Direct WU·cycle 시간·25kg nominal·운반 band 변경 0. component별 authored units를 integer gram으로 환산해 합계가 aggregate wastewater gram과 exact 일치해야 함
EWU와 목표 회수 기간: EWU·가격·처리비·회수 기간 변경 0. 조성별 처리 recipe·sludge·off-gas·판매/폐기 값을 아직 적용하지 않았으므로 경제 완료가 아니며 후속 AuditOnly 재생성이 필요함
공간·전력·물·연료·정비: 유체망 용량·처리량·시설 footprint·전력·용수·정비 수치 변경 0. hydraulic network는 aggregate volume을 유지하고 production receipt만 typed mass provenance를 소유함
위험·실패·회복 방식: 미지정/비정상 enum, 비canonical source ID, 0/비유한 units, duplicate component key, aggregate mismatch, save gram tamper는 mutation 전에 fail-loud함. active WIP와 terminal receipt가 같은 component vector를 current-format V13에 저장함
사회·비가역 비용: 기존 음식·의료·농업 생산량과 오염·기분·질병 수치 변경 0. 서로 다른 폐수를 generic free disposal로 합쳐 처리비를 회피하는 미래 우회를 막는 provenance 기반만 추가함
기존 대안과의 장단점: 유체망 자체를 조성별 tank로 즉시 분해하면 topology/save/UI 파급이 크다. aggregate hydraulic capacity+typed production receipt는 현재 흐름을 보존하면서 경제 귀속을 닫지만 실제 혼합·분리·처리시설 선택은 후속 구현이 필요함
지배 전략 방지 조건: 미분류 authored source 0, component 없는 wastewater commit 0, component 합계와 aggregate 차이 0g, duplicate source key 0, restore 재분류 0, retry 재작성 0
저장 권위와 실행 명령: Production V13 active bill/terminal receipt가 sorted component provenance를 소유하고 Fluid V5 node state는 aggregate hydraulic volume을 소유함. 과거 save migration은 없으며 current-format 누락/변조는 atomic typed restore failure임
자동 감사 ID와 전수 목록 포함 여부: authored nonzero source 43/missing 0 정적 감사, ProductionEconomy V13 active/terminal/tamper 회귀, manual-water focused `wastewaterTyped=150g; wastewaterInvalidDelta=0`, ProductionWorkshop 전수 authoring 검사를 요구함
검증 매트릭스와 보고서 위치: Unity dynamic compile PASS; ProductionEconomyDebugScenarios.RunAll, ProductionWorkshopDebugScenarios.Run, IndustrialInfrastructureDebugScenarios.RunAll, EnvironmentalFieldDebugScenarios.RunAll, SurgeryDebugScenarios.RunAll(true), PhysicalItemDebugScenarios.RunManualWaterExactLotFocused PASS; Console Warning/Error 0/0. 계획 SHA-256 677093692DDD287DABCE94BED529CB02C4FA02B634348A14DEB4AE0DF79EC683
현재 밸런스 상태: 밸런스 영향 없음 / typed wastewater provenance·V13 저장·실패 원자성 구조 검증 PASS. 조성별 처리시설, sludge/off-gas physical disposition, 전체 disposition outbox, EWU·가격·6인 생존망·전체 PlayMode/SaveLoad fresh evidence가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 수술 부품 설치 pending disposition outbox 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-surgical-part-installation-pending-outbox-v9
콘텐츠 종류: 수술 부품의 물리 lot Transfer와 설치 aggregate terminalization 사이 crash seam을 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: SurgeryModels.cs, SurgeryAggregateState.cs, SurgerySaveValidation.cs, PhysicalItemBatchDispositionService.cs, SurgicalPartRuntime.cs, SurgeryRuntimeServices.cs, SurgeryRestoreCoordinator.cs, SurgeryDebugScenarios.cs, SurgeryPlayModeVerifier.cs
등장 시대와 연구: 기존 자연 장기·의수·임플란트·비전 이식 수술 전 시대에 공통 적용하며 연구·해금·시설·부품 가용성은 변경하지 않음
플레이어에게 주는 새 결정: 새 직접 명령 0. 기존 수술 설치 명령이 물리 부품을 먼저 잃거나 재시도에서 중복 소비하지 않고, 같은 order/part/subject 설치만 exact replay함
물리 BOM·입력·출력: 수술 부품 수량·재료 BOM·약품·물·부산물 변경 0. exact source stack 1개를 pending Transfer로 commit하고 동일 commit만 installed aggregate가 소유·acknowledge함
직접 작업량과 계산 근거: Direct WU·수술 단계 시간·회복 시간·25kg nominal·운반 band 변경 0. 수술 order ID와 part instance ID를 결합한 operation identity로 physical/domain join을 결정함
EWU와 목표 회수 기간: EWU·가격·회수율·수술 ROI 변경 0. 이 기록은 물리 보존·exact-once 구조만 닫으며 전수 EWU·가격 재생성은 후속임
공간·전력·물·연료·정비: 시설 footprint·병상·전력·용수·폐수·정비 수치 변경 0. 기존 수술 FacilityBuffer와 물류 경로를 유지함
위험·실패·회복 방식: 물리 commit 뒤 domain publication 전 중단은 pending receipt와 V9 installation provenance로 복구함. commit/source/order/subject 변조는 domain mutation·receipt loss 없이 fail-loud하며 terminal replay는 무재소비 idempotent함
사회·비가역 비용: 의사·환자·간호 인력, 부상 위험, 장기 기증·임플란트 희소성 변경 0. crash/retry로 부품이 증발하거나 복제되는 비가역 비용만 제거함
기존 대안과의 장단점: synchronous consume 후 설치 갱신은 단순하지만 crash seam이 있고, ack-first는 같은 손실 창을 재개방함. domain terminalization 후 exact ack는 일시 pending receipt를 허용하지만 restore/retry로 안전하게 정리 가능함
지배 전략 방지 조건: 물리 중복 debit 0, receipt 없는 installed part 0, conflicting order/subject replay 0, tamper mutation 0, acknowledgement 전 receipt 소실 0, terminal retry 추가 부품 생성 0
저장 권위와 실행 명령: current-format Surgery V9 part aggregate가 order/operation/commit/source/subject provenance를 소유하고 physical pending receipt가 미acknowledged custody를 소유함. 과거 save migration은 없으며 V9 누락/변조는 typed restore failure임
자동 감사 ID와 전수 목록 포함 여부: `surgical_part_installation_pending_outbox` focused row가 pending commit, V9 save validation, tampered commit no-mutation/no-loss, restore finalization, acknowledgement와 terminal replay를 검사함
검증 매트릭스와 보고서 위치: Unity clean compile; PhysicalStockQueryV18DebugScenarios, ProductionEconomyDebugScenarios, DungeonSaveSectionDebugScenarios, SurgeryDebugScenarios PASS. fresh `Artifacts/QA/surgery-playmode-report.txt` UTC 2026-08-20T20:09:15.1331318Z `RESULT=PASS; failures=0`; captured/final Console Warning/Error 0/0. 계획 SHA-256 2A3F157180B77C81BA337E0D2A4729486DE4C26185548DE3C698D8DA54508B5C
현재 밸런스 상태: 밸런스 영향 없음 / 수술 부품 설치 pending outbox·V9 current-format 구조 검증 PASS. 나머지 single-stack terminal domain, global outbox manifest, full mid-action SaveLoad, EWU·가격·6인 생존망이 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 포로 노동 도구 배정 pending disposition outbox 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-captive-labor-tool-assignment-pending-outbox-v3
콘텐츠 종류: 포로 노동 도구의 exact physical Transfer와 captive aggregate 배정 사이 crash seam을 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: CaptivityStateModels.cs, CaptivityRestoreContracts.cs, CaptivitySaveValidation.cs, CaptivityRuntimeContexts.cs, CaptivityRuntime.cs, CaptivityEscortRuntime.cs, PhysicalItemBatchDispositionService.cs, CaptivityRestoreCoordinator.cs, CaptivityCircusDebugScenarios.cs
등장 시대와 연구: 기존 포로 노동과 durable work-kit 사용 전 시대에 공통 적용하며 연구·해금·수감 시설·노동 정책은 변경하지 않음
플레이어에게 주는 새 결정: 새 직접 명령 0. 기존 노동 지정이 exact 도구 instance를 잃거나 재시도에서 중복 소비하지 않고, 같은 captive/tool assignment만 exact replay함
물리 BOM·입력·출력: 노동 도구 수량·재료 BOM·내구 소모·회수량 변경 0. exact source stack 1개를 pending Transfer로 commit하고 동일 commit만 captive aggregate가 소유·acknowledge함
직접 작업량과 계산 근거: Direct WU·포로 노동 효율·작업 시간·25kg nominal·운반 band 변경 0. captive persistent ID와 item instance ID를 결합한 operation identity로 physical/domain join을 결정함
EWU와 목표 회수 기간: EWU·가격·도구 ROI·포로 유지비 변경 0. 이 기록은 물리 보존·exact-once 구조만 닫으며 전수 EWU·가격 재생성은 후속임
시간·확률·재시도: 노동 명령의 확률·주기 변경 0. 동일 operation retry는 pending receipt를 재생하고 도구를 두 번 제거하지 않으며 terminal replay는 cleanup/ack만 반복함
공간·전력·물·연료·정비: 감방·작업장 footprint, 입력 버퍼, 저장, 전력·용수·정비 수치 변경 0. 기존 labor-tool FacilityBuffer와 AIHaul 경로를 유지함
위험·실패·회복 방식: physical commit 뒤 assignment publication 전 중단은 pending receipt와 V3 provenance로 복구함. commit/source/item/operation 변조는 captive mutation·receipt loss 없이 fail-loud하고, cancel/release/death/breakage는 unresolved custody를 먼저 finalize하거나 유지함
사회·비가역 비용: 포로 관계·순응도·탈출 위험·경비 노동·노동 효율 변경 0. crash/retry로 unique work kit이 증발하거나 복제되는 비가역 비용만 제거함
기존 대안과의 장단점: synchronous consume 후 assignment 갱신은 단순하지만 crash seam이 있고 category/count consume은 instance를 잃음. exact pending Transfer는 provenance와 replay를 보장하지만 current-format V3 필수 join과 restore reconciliation 비용이 있음
지배 전략 방지 조건: 물리 중복 debit 0, receipt 없는 assigned tool 0, 다른 captive/tool replay 0, tamper mutation 0, acknowledgement 전 receipt 소실 0, cancel-before-finalize destination release 0
저장 권위와 실행 명령: current-format Captivity V3 captive state가 operation/commit/source/instance/completed provenance를 소유하고 physical pending receipt가 미acknowledged custody를 소유함. 과거 save migration은 없으며 V3 누락/변조는 typed restore failure임
자동 감사 ID와 전수 목록 포함 여부: `captive_labor_tool_assignment_pending_outbox` focused row가 pending commit, V3 validation, malformed/mismatched provenance no-mutation/no-loss, restore finalization, acknowledgement와 terminal replay를 검사함
검증 매트릭스와 보고서 위치: Unity clean compile; PhysicalStockQueryV18DebugScenarios, CaptivityCircusDebugScenarios, DungeonSaveSectionDebugScenarios, DungeonRuntimeCompositionDebugScenarios PASS. fresh `Artifacts/QA/captivity-ai-playmode.txt` UTC 2026-08-20T20:29:20.4909781Z `RESULT=PASS; failures=0`; final Console Warning/Error 0/0. 계획 SHA-256 B33B4578E04E7FC7D90938619A3DDAFE7C4154F7036761C033297FC2CF276213
현재 밸런스 상태: 밸런스 영향 없음 / 포로 노동 도구 pending outbox·Captivity V3 current-format 구조 검증 PASS. 나머지 unique-ID terminal domain, production raw-consume manifest, full mid-action SaveLoad, EWU·가격·6인 생존망이 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 raw removal zero·창고 입고 operation history·current-format 복원 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-raw-removal-zero-admission-history-full-restore-v13
콘텐츠 종류: production raw physical removal 우회 차단, warehouse admission exact-once operation 충돌 회피, reverse restore rollback idempotency를 닫는 구조 수직 슬라이스
정의·카탈로그·실행기 위치: PhysicalStockQueryV18DebugScenarios.cs, WarehouseMassAdmissionService.cs, WorldItemHaulPlanningService.cs, WildlifeActor.cs, WildlifeDebugScenarios.cs, ModularFacilityWorldSaveService.cs, PhysicalItemLogisticsPlayModeVerifier.cs, DungeonAiActionSaveLoadPlayModeVerifier.cs
등장 시대와 연구: 물리 아이템·창고·운반·야생동물 restore를 사용하는 전 시대에 공통 적용하며 연구·해금·콘텐츠 가용성은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 취소되거나 복원된 과거 운반 ID가 새 화물을 가리키지 않고, 정상 물류가 충돌 ID를 건너뛰어 계속 진행함
물리 BOM·입력·출력: item 수량·unitWeight·BOM·생산량 변경 0. exact physical lot, token, receipt와 conservation 검증만 강화함
직접 작업량과 계산 근거: Direct WU·운반 시간·25kg nominal·19.1/28.65kg 및 23.88/35.81kg band 변경 0. operation history 조회는 ordinal exact ID membership이며 질량 계산을 바꾸지 않음
EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. raw removal 우회와 replay 가능성을 차단하지만 전수 kg After·EWU·가격 재생성 전 경제 완료로 승격하지 않음
시간·확률·재시도: terminal admission tombstone과 request equality를 유지하고 충돌 operation ID만 deterministic sequence에서 건너뜀. 확률·lease 시간·AI 작업 선택 수치 변경 0
공간·전력·물·연료·정비: 창고 authored 25,000g, 시설 footprint·버퍼·전력·용수·정비 수치 변경 0. over-capacity evacuation의 기존 physical path를 회귀 검증함
위험·실패·회복 방식: fingerprint/source mismatch는 계속 fail-loud하고 tombstone을 삭제하지 않음. exact already-bound wildlife grid rollback만 idempotent success이며 다른 grid/occupant mismatch는 전체 restore atomic failure임
사회·비가역 비용: 캐릭터·야생동물·포로·관계·기분·생존 수치 변경 0. restore 실패가 ghost grid owner나 새 화물 replay로 이어지는 비가역 상태만 제거함
기존 대안과의 장단점: source revision 비교 완화나 tombstone 삭제는 작은 수정이지만 exact-once를 깨뜨림. admission history-aware allocation은 sequence gap을 허용하는 대신 physical identity와 replay 방어를 보존함
지배 전략 방지 조건: terminal operation 재사용 0, 다른 lot admission replay 0, production raw consume caller 0, restore rollback 중복 occupant 0, physical 수량 생성·삭제 0
저장 권위와 실행 명령: physical repository가 haul sequence/stack, warehouse admission ledger가 token history, wildlife/facility restore participants가 grid publication을 소유함. 과거 save migration은 없고 current-format strict join만 검증함
자동 감사 ID와 전수 목록 포함 여부: `V27_PRODUCTION_RAW_CONSUME_CALLS_ZERO`, terminal release/commit operation-history assertions, `restore_grid_rebind_is_idempotent_after_wildlife_rollback`, full Physical·SaveLoad exact markers를 요구함
검증 매트릭스와 보고서 위치: Unity clean compile 및 focused Physical Stock, Wildlife, Modular Facility save/load, Dungeon save-section PASS. `Artifacts/QA/physical-item-logistics-playmode-report.txt` UTC 2026-08-20T20:54:48.1823414Z `RESULT=PASS; failures=0`, captured Warning/Error 0/0. `Artifacts/QA/ai-mid-action-save-load-playmode.txt` UTC 2026-08-20T20:55:07.2614796Z `result=PASS`, `failures=0`, repeated restore conservation exact, no unexpected Error/Exception/Assert. 계획 SHA-256 54E2A53A96AD28875798947F51123C4F7B203D4EAD328D0789D2F6628979743B
현재 밸런스 상태: 밸런스 영향 없음 / raw removal zero·admission history·current-format full restore 구조 검증 PASS. 남은 terminal domain outbox, packaged lot·나머지 ingress, 전수 kg After, EWU·가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 세력 배상 pending disposition outbox 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-faction-restitution-pending-outbox-v2
콘텐츠 종류: 세력 배상 physical Transfer와 faction/campaign terminal publication 사이 crash seam을 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: FactionModels.cs, FactionDomainRuntime.cs, FactionPayloadValidation.cs, FactionRestitutionOutbox.cs, FactionRuntime.cs, FactionRestitutionOutboxDebugScenarios.cs, SpeciesFactionDefenseExpansionDebugScenarios.cs
등장 시대와 연구: 기존 세력 배신·배상 시스템 전 시대에 공통 적용하며 연구·해금·세력 등장 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 기존 배상 명령이 physical goods를 먼저 잃거나 retry에서 관계·campaign grievance를 두 번 변경하지 않고 같은 betrayal scar만 exact replay함
물리 BOM·입력·출력: authored 배상 물품 수량·가치·종류 변경 0. exact source stack vector를 pending Transfer로 commit하고 동일 commit만 faction aggregate가 소유·acknowledge함
직접 작업량과 계산 근거: Direct WU·운반 시간·25kg nominal·성능 band 변경 0. faction persistent ID와 persisted betrayal scar를 결합한 operation identity로 physical/domain join을 결정함
EWU와 목표 회수 기간: EWU·가격·배상 ROI 변경 0. 이 기록은 custody 보존과 exact-once 구조만 닫으며 전수 kg After·EWU·가격 재생성은 후속임
시간·확률·재시도: 배상 확률·쿨다운 변경 0. 동일 scar retry는 pending receipt를 재생하고 물품·grievance를 두 번 debit하지 않으며 completed replay는 lingering receipt ack만 수행함
공간·전력·물·연료·정비: 창고·시설 footprint, FacilityBuffer, 전력·용수·연료·정비 수치 변경 0. 기존 물리 stock selection과 faction command 경로를 유지함
위험·실패·회복 방식: physical commit 뒤 faction/campaign publication 전 중단은 Faction V2 provenance로 복구함. operation/commit/source/quantity/grams/value 변조는 faction·campaign mutation과 receipt loss 없이 fail-loud함
사회·비가역 비용: authored goodwill·trust·betrayal scar·grievance 감소량 변경 0. crash/retry가 배상 물품을 삭제하거나 관계 효과를 중복 적용하는 비가역 상태만 제거함
기존 대안과의 장단점: synchronous consume 후 관계 갱신은 단순하지만 crash seam이 있음. scar-unique pending outbox는 current-format provenance 비용이 있으나 exact replay를 보장함. recurring goodwill은 unique epoch가 없어 이 slice에서 억지 dedup하지 않음
지배 전략 방지 조건: 물리 중복 debit 0, receipt 없는 restitution terminal 0, 같은 scar grievance 중복 감소 0, 다른 scar replay 0, tamper mutation 0, acknowledgement 전 receipt 소실 0
저장 권위와 실행 명령: current-format Faction V2가 operation/commit/source/quantity/grams/value/absolute grievance target/completed provenance를 소유하고 physical pending receipt가 미acknowledged custody를 소유함. 과거 save migration은 없으며 V2 누락·부분 provenance는 typed restore failure임
자동 감사 ID와 전수 목록 포함 여부: `FACTION_RESTITUTION_OUTBOX_CONTRACTS` focused row가 scar identity, V2 JSON provenance, tampered commit no-mutation/no-loss, quantity conservation, absolute campaign target, acknowledgement와 terminal replay를 검사함
검증 매트릭스와 보고서 위치: Unity clean compile; `FactionRestitutionOutboxDebugScenarios.RunAll()` PASS, `SpeciesFactionDefenseExpansionDebugScenarios.ValidateOnly()` PASS, Console Error 0. 계획 SHA-256 C197C9CDF83E1387B6C5A14880625D58FC6E0FC2BD81F9EA2FFCCB04157FE93E
현재 밸런스 상태: 밸런스 영향 없음 / 세력 배상 pending outbox·Faction V2 current-format 구조 검증 PASS. 시설 진화·의복 수리 등 남은 terminal domain, packaged lot·나머지 ingress, 전수 kg After, EWU·가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 시설 진화 다중 재료 pending batch 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-facility-evolution-multi-material-pending-batch-partial
콘텐츠 종류: 시설 진화의 다중 재료 부분 소비와 결과 건물 교체 실패 손실을 줄이는 pending physical Transfer 구조 하위 슬라이스
정의·카탈로그·실행기 위치: FacilityEvolutionService.cs, FacilityEvolutionRuntime.cs, WarehouseFacilityEvolutionResourceProvider.cs, FacilityEvolutionDebugScenarios.cs, OffenseStrategicDebugScenarios.cs
등장 시대와 연구: 기존 시설 진화 레시피 전 시대에 공통 적용하며 연구·별 등급·레시피 공개 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 한 진화 시도에서 뒤 재료가 부족해 앞 재료만 사라지지 않고, 교체 실패 뒤 같은 recipe를 재시도하면 기존 pending material commit을 이어감
물리 BOM·입력·출력: authored 요구 category·수량·결과 시설 변경 0. 모든 요구량을 category별 합산하고 exact physical source stack vector 한 번으로 pending Transfer함
직접 작업량과 계산 근거: Direct WU·건설/진화 시간·운반 kg 변경 0. persistent facility ID와 next history sequence가 operation slot이며 recipe ID는 request fingerprint를 소유함
EWU와 목표 회수 기간: EWU·가격·시설 ROI 변경 0. 부분 debit·중복 debit 방지 구조만 변경하며 kg After와 경제 재생성은 후속임
시간·확률·재시도: proposal 확률·후보 순서 변경 0. exact pending retry만 material availability를 대체하고 다른 recipe 또는 다른 request는 operation conflict로 거절함
공간·전력·물·연료·정비: footprint·전력·용수·연료·정비 요구 변경 0. 결과 occupant 등록 성공 전 source occupant와 visual을 유지함
위험·실패·회복 방식: 결과 create/register 실패는 failed result를 정리하고 exact source occupant를 복구함. source 재등록 실패는 fail-loud. pending material receipt는 같은 recipe retry까지 유지됨
사회·비가역 비용: 세력·포로·기분·생존·관계 수치 변경 0. 진화 실패가 재료나 원본 시설을 일방적으로 삭제하는 비가역 상태만 차단함
기존 대안과의 장단점: 요구량별 immediate consume은 단순하지만 부분 손실이 있고 source-first destruction은 교체 실패를 복구하지 못함. pending aggregate와 source-last destruction은 replay/rollback 경계를 제공하지만 restore provenance가 추가로 필요함
지배 전략 방지 조건: 다중 요구량 partial debit 0, replacement retry second debit 0, 다른 recipe pending 재사용 0, failed result와 source 동시 occupant 0, 성공 전 source destruction 0
저장 권위와 실행 명령: physical pending receipt는 current-format Items authority에 저장되지만 Facility state는 아직 pending recipe/commit/resolved mutation phase를 저장하지 않음. 따라서 restore 자동 재개는 미완료이며 이 기록은 partial임
자동 감사 ID와 전수 목록 포함 여부: `Failed replacement retries one pending material batch without a second debit`, warehouse aggregate/reject fixture, full Facility Evolution Editor suite와 Offense Strategic 11행을 실행함
검증 매트릭스와 보고서 위치: Unity clean compile; `FacilityEvolutionDebugScenarios.RunAll()` PASS; `OffenseStrategicDebugScenarios.RunAll()` PASS(11); final Console Warning/Error 0/0. 계획 SHA-256 D5798CB7C835322D9A93367A7C91D4BB3EF72D403E9979DF7FFA6AD7A1797C21
현재 밸런스 상태: 밸런스 영향 없음 / 시설 진화 multi-material pending retry와 source-preserving replacement PASS. Facility V4 provenance·restore reconciliation, 나머지 terminal domain, packaged lot·나머지 ingress, 전수 kg After, EWU·가격·6인 생존망·3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 시설 진화 material outbox·Facility V4 복원 재개 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-facility-evolution-material-outbox-v4
콘텐츠 종류: 시설 진화의 exact physical material debit과 결과 건물 publication 사이 crash seam을 닫는 current-format durable outbox·restore reconciliation 구조 수직 슬라이스
정의·카탈로그·실행기 위치: FacilityEvolutionStateComponent.cs, FacilityEvolutionAggregateAdapter.cs, FacilityEvolutionService.cs, FacilityEvolutionRuntime.cs, WarehouseFacilityEvolutionResourceProvider.cs, FacilityEvolutionPendingMaterialProjection.cs, DungeonFacilityRegistration.cs, FacilityEvolutionDebugScenarios.cs
등장 시대와 연구: 기존 시설 진화 레시피 전 시대에 공통 적용하며 연구·별 등급·레시피 공개 조건·후보 확률은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 교체 또는 acknowledgement 실패 뒤 같은 진화를 다시 선택하지 않아도 current-format restore 이후 저장된 exact result가 자동 재개되며, 다른 recipe·building·stack으로 대체하지 않음
물리 BOM·입력·출력: authored 요구 category·수량·결과 시설·token 변경 0. exact source stack vector, quantity와 input grams를 한 Physical pending Transfer로 debit하고 같은 commit만 acknowledge함
직접 작업량과 계산 근거: Direct WU·건설/진화 시간·25kg nominal·운반 성능 band 변경 0. operation ID는 facility persistent ID와 next history sequence, recipe는 request fingerprint와 V4 provenance에 고정함
EWU와 목표 회수 기간: EWU·가격·시설 ROI·철거 회수율 변경 0. 손실·중복·재굴림 경계만 닫았으며 전수 kg After와 EWU·가격 재생성 전 경제 완료로 승격하지 않음
시간·확률·재시도: proposal·mutation·record-token·확률 결과를 physical debit 전에 detached snapshot으로 한 번 확정하고 canonical result payload로 저장함. retry/restore는 resolver를 다시 호출하지 않고 MaterialCommitted 또는 DomainApplied phase만 재개함
공간·전력·물·연료·정비: footprint·접근칸·전력·용수·연료·정비·출력 버퍼 수치 변경 0. 결과 occupant 등록 성공 전 source occupant와 visual을 보존하며 failed result만 정리함
위험·실패·회복 방식: V4 구조 불일치, authored recipe/source/result 불일치, mutation tag 이탈, Physical operation/reason/commit/source/quantity/grams drift와 duplicate operation은 participant 224에서 restore 완료 전에 fail-loud함. post-restore projection은 정상 join만 exact 재개함
사회·비가역 비용: 세력·포로·기분·생존·관계·사건 수치 변경 0. crash/retry가 재료·원본 시설을 삭제하거나 mutation/token 결과를 재굴림하는 비가역 상태만 제거함
기존 대안과의 장단점: 동기 consume→replace는 단순하지만 중단 창을 복구할 권위가 없음. V4 outbox는 current-format 필수 provenance와 participant 비용이 있으나 debit·domain·ack phase를 정확히 재개하고 rollback을 방해하지 않음
지배 전략 방지 조건: material second debit 0, replacement second application 0, acknowledgement 전 receipt 소실 0, restore mutation reroll 0, 다른 recipe pending 재사용 0, tamper 부분 publication 0, source/result 동시 occupant 0
저장 권위와 실행 명령: source/result의 FacilityEvolutionStateComponent V4가 operation/commit/source/quantity/grams/recipe/history/phase/mutation/result payload를 소유하고 Physical pending receipt가 미acknowledged custody를 소유함. 과거 save migration은 없으며 V4 누락·변조는 typed current-format restore failure임
자동 감사 ID와 전수 목록 포함 여부: `224.world.facility-evolution-materials`가 225 haul-intent보다 먼저 exact join을 read-only 검증하고, `FacilityEvolutionPendingMaterialProjection`이 publication 이후 revision 기반으로 pending을 재개함. focused suite가 replacement/ack first-failure, V4 JSON, 여섯 tamper, automatic projection과 participant composition을 검사함
검증 매트릭스와 보고서 위치: clean Unity compile; `FacilityEvolutionDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)`, `OffenseStrategicDebugScenarios.RunAll()` 11행 PASS; final Console Warning/Error 0/0. 계획 SHA-256 BB1CD797882EAA5D0BCBF8ED4C290922ED4E31E8B730D88DA3710EE33BDDBC09
현재 밸런스 상태: 밸런스 영향 없음 / 시설 진화 Facility V4 material outbox·restore reconciliation 구조 검증 PASS. 의복 수리 등 남은 terminal domain, packaged lot·나머지 ingress, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 의복 수선 material outbox·Character Environment V6 복원 재개 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-apparel-repair-material-outbox-v6
콘텐츠 종류: 의복 수선의 thread/scrap physical debit과 durability publication 사이 crash seam을 닫는 current-format durable outbox·restore reconciliation 구조 수직 슬라이스
정의·카탈로그·실행기 위치: ApparelWorkOrderRuntime.cs, CharacterEnvironmentModels.cs, ApparelItemStateCodec.cs, PhysicalItemBatchDispositionService.cs, LeasedItemReservationService.cs, DungeonWorldSimulationRegistration.cs, ApparelRepairOutboxDebugScenarios.cs
등장 시대와 연구: 기존 V22 수선 접수대와 의복 수선을 사용하는 시대에 동일하게 적용하며 연구·해금·시설 요구는 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 중단·저장·복원 후에도 같은 수선 재료와 결과만 이어지며 대체 재료를 다시 고르지 않음
물리 BOM·입력·출력: 기존 내구도 60 미만 수선의 `material:sewing-thread` 1개 + `material:mending-scrap` 1개를 그대로 사용. exact Physical pending Transfer 한 번만 debit하고 수량·grams·source stack vector를 저장함
직접 작업량과 계산 근거: 수선 requiredWork 18, 내구도 40→70, 25kg nominal·19.1/28.65kg 및 23.88/35.81kg 성능 band 변경 0. 작업 완료 후 lease→pending receipt 소유권 인계만 추가함
EWU와 목표 회수 기간: EWU·가격·수선 ROI·내구도 회수 수치 변경 0. 중복 debit·중복 durability 적용만 차단하며 전수 kg After·EWU·가격 재생성은 후속임
시간·확률·재시도: 수선 시간·확률 변경 0. acknowledgement 실패 시 `RepairApplied` phase와 receipt를 보존하고 재시도는 재료 선택·작업·내구도 적용을 반복하지 않고 acknowledgement만 수행함
공간·전력·물·연료·정비: 수선 접수대 footprint·접근칸·전력·용수·연료·정비·출력 버퍼 수치 변경 0
위험·실패·회복 방식: commit/source/quantity/grams/target/payload/phase가 하나라도 다르면 participant 226 publication 전에 fail-loud함. retry는 대체 stack이나 material을 선택하지 않고 current-format strict join만 허용함
사회·비가역 비용: 캐릭터·세력·포로·기분·생존·의복 효과 수치 변경 0. crash/retry로 수선 재료가 사라지거나 내구도가 두 번 적용되는 비가역 상태만 제거함
기존 대안과의 장단점: synchronous material consume→component update는 단순하지만 둘 사이 crash seam을 복구할 권위가 없음. V6 outbox는 DTO·participant 비용이 있지만 exact phase replay와 tamper rollback을 보장함
지배 전략 방지 조건: thread/scrap second debit 0, durability second application 0, acknowledgement 전 receipt 소실 0, different material substitution 0, tampered restore partial publication 0, pending 주문의 lease reacquisition 0
저장 권위와 실행 명령: Character Environment V6 apparel order가 operation/reason/commit/source/quantity/grams/target/original+resolved payload/phase를 소유하고 Physical pending receipt가 미acknowledged custody를 소유함. `226.world.apparel-work-orders` participant가 exact join·reconcile하며 과거 save migration은 없음
자동 감사 ID와 전수 목록 포함 여부: `apparel_repair_pending_outbox_restore_exact` 행이 one-debit, phase retry, payload application, tampered commit no-mutation/no-loss, normal restore acknowledgement를 검사하며 Physical item contract list에 포함됨
검증 매트릭스와 보고서 위치: Unity clean compile; `ApparelRepairOutboxDebugScenarios.RunFocused()`, `DungeonSaveSectionDebugScenarios.RunAll(false)`, `DungeonRuntimeCompositionDebugScenarios.RunAll(false)` PASS; focused/restore-contract Console Warning/Error 0/0. broader V22는 `dreamweave MaxStack=100`, PhysicalItem aggregate는 `stored water mirror missing` 기존 fixture mismatch로 비-green이며 면제하지 않음. 계획 SHA-256 BC49F511D3E5EC086E9D13E296D7EDEFE212999BC01BB519A50EA9364BE19BEF
현재 밸런스 상태: 밸런스 영향 없음 / 의복 수선 material outbox·Character Environment V6 current-format 구조 검증 PASS. 남은 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 야생동물 식량 약탈 pending Sink outbox·Wildlife V5 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-wildlife-food-raid-pending-sink-v5
콘텐츠 종류: 야생동물 식량 약탈의 physical Sink와 raid outcome publication 사이 crash seam 및 다중 늑대 operation 충돌을 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: WildlifePrimitives.cs, WildlifeRuntimeServices.cs, WildlifeBehaviorRuntime.cs, WildlifeFoodRaidDispositionOutbox.cs, WildlifeRestoreRuntime.cs, WildlifeSaveValidation.cs, WildlifeFoodRaidOutboxDebugScenarios.cs
등장 시대와 연구: 기존 야생동물 식량 약탈 사건이 가능한 전 시대에 공통 적용하며 연구·해금·습격 발생 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 늑대가 식량에 도달한 뒤 중단·저장·복원이 발생해도 같은 늑대의 같은 한 개 도난만 완료되고 다른 음식이나 다른 늑대 receipt로 대체하지 않음
물리 BOM·입력·출력: 기존 늑대 한 마리당 식량 1개 Sink를 그대로 유지. exact source stack 1개, quantity 1, positive input grams와 item definition을 pending receipt와 Wildlife V5 order에 저장함
직접 작업량과 계산 근거: 캐릭터 Direct WU·운반 WU·25kg nominal·성능 band 변경 0. raid ID와 wildlife persistent ID 결합이 exact-once operation authority임
EWU와 목표 회수 기간: 음식 EWU·가격·영양·부패·약탈 기대손실 변경 0. 중복 Sink와 receipt orphan만 차단하며 전수 kg After·EWU·가격 재생성은 후속임
시간·확률·재시도: 약탈 빈도·이동·target 선택·확률 변경 0. acknowledgement 실패는 `RaidPublished`에서 같은 receipt만 재시도하며 target 재탐색·두 번째 Sink를 금지함
공간·전력·물·연료·정비: 시설 footprint·창고·통로·전력·용수·연료·정비 수치 변경 0. 기존 loose food 도달 경로를 유지함
위험·실패·회복 방식: actor 사망·제거 전 pending theft를 먼저 reconcile해 성공한 도난을 Stolen으로 보존함. operation/reason/commit/source/quantity/grams/item/phase mismatch는 대체 물품 없이 fail-loud하고 whole restore rollback 대상임
사회·비가역 비용: 기분·세력·포로·야생동물 능력·침입 관계 수치 변경 0. crash/retry가 음식 하나를 여러 번 삭제하거나 물리 Sink를 Cancelled outcome으로 잃는 비가역 상태만 제거함
기존 대안과의 장단점: synchronous Sink 후 state update는 단순하지만 중단 경계와 같은 raid 다중 actor ID 충돌이 있음. per-actor V5 outbox는 저장 provenance 비용이 있으나 exact replay와 restore join을 보장함
지배 전략 방지 조건: actor별 second Sink 0, 같은 raid operation collision 0, acknowledgement 전 receipt 소실 0, actor-loss cancellation overwrite 0, 다른 target substitution 0, tamper partial publication 0
저장 권위와 실행 명령: Wildlife V5 raid order가 operation/reason/commit/source/quantity/grams/item/phase와 stolen outcome을 소유하고 Physical pending receipt가 미acknowledged Sink custody를 소유함. participant `250.world.wildlife`가 candidate runtime 재구축 후 exact reconcile하며 과거 save migration은 없음
자동 감사 ID와 전수 목록 포함 여부: `food_raid_pending_disposition_outbox`와 `V27_WILDLIFE_FOOD_RAID_PENDING_OUTBOX`가 두 actor operation uniqueness, one-Sink retry, tamper no-loss와 acknowledgement cleanup을 검사하고 Wildlife Editor contract list에 포함됨
검증 매트릭스와 보고서 위치: Unity clean compile; `WildlifeFoodRaidOutboxDebugScenarios.RunFocused()`, `WildlifeDebugScenarios.RunAll(false)`, `DungeonSaveSectionDebugScenarios.RunAll(false)` PASS; final Console Warning/Error 0/0. 계획 SHA-256 3673FFC0A3157908913BA97FD86C61AB8EE35D02D63D33DA7D5C738292418A5F
현재 밸런스 상태: 밸런스 영향 없음 / 야생동물 식량 약탈 pending Sink·Wildlife V5 current-format 구조 검증 PASS. 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 반복 팩션 호의 pending Transfer outbox·Faction V3 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-faction-goodwill-pending-transfer-v3
콘텐츠 종류: 반복 가능한 팩션 호의 물자 physical Transfer와 관계 publication 사이 crash seam 및 same-day operation 충돌을 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: FactionModels.cs, FactionAggregateState.cs, FactionDomainRuntime.cs, FactionPayloadValidation.cs, FactionGoodwillOutbox.cs, FactionRuntime.cs, FactionRestitutionOutboxDebugScenarios.cs
등장 시대와 연구: 기존 팩션 호의가 가능한 전 시대에 공통 적용하며 연구·세력 발견·협상 봉쇄·동맹 해금 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 같은 날 같은 세력에 합법적으로 여러 번 호의를 보내도 각 지급이 독립 operation이 되며, 중단·저장·복원 뒤 이미 적용된 지급은 두 번째 물자나 rapport를 만들지 않음
물리 BOM·입력·출력: 기존 최소 물리가치 50 이상 exact goods Transfer를 유지. source stack vector, quantity, input grams와 평가 physical value를 Physical pending receipt와 Faction V3 provenance에 저장함
직접 작업량과 계산 근거: Direct WU·운반 WU·25kg nominal·성능 band 변경 0. aggregate monotonic goodwill sequence가 반복 지급별 exact-once slot을 소유함
EWU와 목표 회수 기간: 물자 EWU·가격·세력 ROI·관계 보상 변경 0. operation collision, 중복 debit, relative rapport 재적용만 차단하며 전수 kg After·EWU·가격 재생성은 후속임
시간·확률·재시도: 같은 날 제한·확률·AI cadence 변경 0. pending retry는 저장된 absolute rapport target과 exact commit만 사용하고 current inventory에서 다른 물자를 다시 선택하지 않음
공간·전력·물·연료·정비: 시설 footprint·창고 용량·통로·전력·용수·연료·정비 수치 변경 0
위험·실패·회복 방식: operation/reason/commit/source/quantity/grams/value/sequence/target mismatch와 missing receipt는 rapport 또는 faction publication 전에 fail-loud함. 이미 target 이상이면 delta를 다시 적용하지 않고 acknowledgement만 수행함
사회·비가역 비용: authored 최소가치 50, rapport gain 상한 10, betrayal scar 감쇠 0.85^scar 변경 0. crash/retry로 물자가 여러 번 사라지거나 관계 효과가 중복되는 비가역 상태만 제거함
기존 대안과의 장단점: `faction+day` synchronous Transfer는 단순하지만 합법적 same-day 반복과 충돌하고 저장 경계를 복구할 provenance가 없음. V3 monotonic outbox는 저장 필드 비용이 있으나 독립 반복과 exact replay를 함께 보장함
지배 전략 방지 조건: same-day operation collision 0, second physical debit 0, second rapport application 0, acknowledgement 전 receipt 소실 0, 다른 source substitution 0, tamper partial publication 0
저장 권위와 실행 명령: Faction V3 aggregate가 global goodwill sequence와 per-faction operation/reason/commit/source/quantity/grams/value/absolute target/phase를 소유하고 Physical pending receipt가 미acknowledged custody를 소유함. 과거 save migration은 없으며 V3 누락·부분 provenance는 typed current-format restore failure임
자동 감사 ID와 전수 목록 포함 여부: `FactionRestitutionOutboxDebugScenarios.RunAll()`의 recurring goodwill row가 두 operation ID, V3 JSON, tampered receipt no-mutation/no-loss, campaign-already-applied recovery, exact physical conservation을 검사함
검증 매트릭스와 보고서 위치: clean Unity compile; `FactionRestitutionOutboxDebugScenarios.RunAll()`, `SpeciesFactionDefenseExpansionDebugScenarios.ValidateOnly()`, `DungeonSaveSectionDebugScenarios.RunAll(false)`, V22 apparel full, stored-water focused, PhysicalItem full PASS; final Console Warning/Error 0/0. 계획 SHA-256 99EB25D8A4A10D2B60A4C3B3E3AFE878F2EE0276A3F05C1DC00CECE4E712F8AC
현재 밸런스 상태: 밸런스 영향 없음 / 반복 팩션 호의 pending Transfer·Faction V3 current-format 구조 검증 PASS. 나머지 terminal domain, packaged lot·나머지 ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 포획 야생동물 급식 pending Sink outbox·Circus V3·Wildlife V6 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-captured-wildlife-feed-pending-sink-v3-v6
콘텐츠 종류: 포획 야생동물의 일반 사료·폐기물 직접 급식 physical Sink와 허기·질병 publication 사이 crash seam을 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: CircusModels.cs, CircusSaveValidation.cs, CapturedWildlifeFeedOutbox.cs, WildlifeCaptureRuntime.cs, WildlifeActor.cs, WildlifePrimitives.cs, WildlifeSaveValidation.cs, CapturedWildlifeFeedOutboxDebugScenarios.cs, CaptivityWildlifeLifecyclePlayModeVerifier.cs
등장 시대와 연구: 기존 우리·축산·포획 동물 급식이 가능한 전 시대에 공통 적용하며 연구·우리 능력·species diet·사료 해금 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 중단·저장·복원 후에도 같은 동물의 같은 급식 결과만 완료되며 다른 먹이로 대체하거나 질병 결과를 다시 굴리지 않음
물리 BOM·입력·출력: 기존 급식 1회당 exact food stack quantity 1 Sink를 유지. source stack vector, item definition, quantity 1, positive input grams와 terminal commit을 Circus V3 provenance에 저장함
직접 작업량과 계산 근거: AnimalCare WU·시설 작업 시간·운반 WU·25kg nominal·19.1/28.65kg 및 23.88/35.81kg 성능 band 변경 0. per-animal monotonic feed sequence가 exact action slot을 소유함
EWU와 목표 회수 기간: 사료 EWU·가격·영양·사육 ROI·폐기물 가치 변경 0. 중복 Sink·중복 허기/피해·receipt orphan만 차단하며 전수 kg After와 EWU·가격 재생성은 후속임
시간·확률·재시도: 기존 normal nutrition 0.72와 waste nutrition 보정·disease chance를 유지. 폐기물 질병 outcome은 physical commit 전에 한 번 결정해 저장하고 retry/restore에서 재추첨하지 않음
공간·전력·물·연료·정비: 우리 footprint·접근칸·FacilityBuffer·물 공급·전력·연료·정비 수치 변경 0. 해당 pen destination의 exact unreserved FacilityBuffer stack만 정상 급식 source로 허용함
위험·실패·회복 방식: operation/reason/commit/source/quantity/grams/item/phase/outcome/absolute target mismatch와 missing actor는 restore publication 전에 fail-loud함. acknowledgement 실패는 CarePublished phase와 receipt를 보존하고 재시도는 ack만 수행함
사회·비가역 비용: 길들이기·탈출 위험·기분·세력·포로·질병 authored 수치 변경 0. crash/retry로 먹이가 여러 번 사라지거나 허기·질병 피해가 중복되는 비가역 상태만 제거함
기존 대안과의 장단점: 동기 buffer consume→needs update는 단순하지만 두 authority 사이 중단을 복구할 provenance가 없음. V3/V6 outbox는 저장 필드와 restore join 비용이 있으나 exact source·결과·phase replay를 보장함
지배 전략 방지 조건: feed second Sink 0, hunger/health second application 0, disease reroll 0, acknowledgement 전 receipt 소실 0, 다른 feed substitution 0, tamper partial publication 0, clone rollback alias 0
저장 권위와 실행 명령: Circus V3 captured state가 sequence/operation/reason/commit/source/quantity/grams/item/nutrition/disease/targets/phase를 소유하고 Wildlife V6 actor가 last applied feed commit을 소유함. Physical pending receipt가 미acknowledged Sink custody를 소유하며 participant `500.world.circus`가 exact reconcile함. 과거 save migration은 없음
자동 감사 ID와 전수 목록 포함 여부: `captured_wildlife_feed_pending_outbox` focused row와 production-live `ANIMAL_CARE_FEED_SOURCE_PHYSICAL`, `ANIMAL_CARE_FEED_SINK_EXACT`, `ANIMAL_CARE_FEED_OUTBOX_CLEAN`, `ANIMAL_CARE_FEED_SAVE_EXACT`를 coverage manifest의 wildlife animal-care 필수 marker로 포함함
검증 매트릭스와 보고서 위치: Unity clean compile; `CapturedWildlifeFeedOutboxDebugScenarios.Run()`, `CaptivityCircusDebugScenarios.RunAll(true)` PASS; `Artifacts/QA/captivity-wildlife-lifecycle-playmode.txt` UTC 2026-08-20T23:46:08.3171612Z `RESULT=PASS; failures=0`, feed:hay 196g quantity 1→0, hunger 0.9→0.1889, final Console Warning/Error 0/0. 계획 SHA-256 345B6DE952950DA1246B2DCC1B3DF113D7BA7A809339F2D8E17D4D79D4B5C273
현재 밸런스 상태: 밸런스 영향 없음 / 포획 야생동물 급식 pending Sink·Circus V3·Wildlife V6 current-format 구조 및 production-live 연결 검증 PASS. 나머지 terminal domain, packaged lot·나머지 ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 길잡이 부적 정보 해금 pending Sink outbox·External Influence V4 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-external-intel-trail-charm-pending-sink-v4
콘텐츠 종류: 길잡이 부적 physical Sink와 원정 사이트 정보 해금 publication 사이 중단 경계를 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: ExternalInfluenceContracts.cs, ExternalInfluenceSaveSection.cs, ExternalInfluenceRuntime.cs, ExternalInfluenceTrailCharmOutbox.cs, PhysicalItemBatchDispositionService.cs, ExternalInfluenceTrailCharmOutboxDebugScenarios.cs
등장 시대와 연구: 기존 `recipe:trail-charm`과 `research:husbandry:capture` 해금 조건을 그대로 사용하며 시대·연구·사이트 생성 조건을 변경하지 않음
플레이어에게 주는 새 결정: 새 명령 0. 기존 TrailCharm 결제 선택은 유지하고 중단·저장·복원 뒤 같은 사이트의 같은 한 개 결제만 정확히 완료함
물리 BOM·입력·출력: 기존 `resource:trail-charm` 1개 terminal Sink를 유지. 제작 BOM `resource:rune-dust` 1 + `resource:fang` 1, 출력 1을 변경하지 않으며 exact source stack·quantity 1·positive grams를 V4 provenance와 Physical pending receipt에 저장함
직접 작업량과 계산 근거: 기존 trail-charm Direct WU 16, 25kg nominal과 19.1/28.65kg·23.88/35.81kg 성능 band 변경 0. canonical one-time site ID가 exact action identity임
EWU와 목표 회수 기간: 부적 EWU·제작 ROI·정보 가치·원정 보상·가격 변경 0. 중복 Sink와 정보 미공개 receipt orphan만 차단하며 전수 kg After·EWU·가격 재생성은 후속임
시간·확률·재시도: 제작 시간·사이트 만료·원정 확률 변경 0. acknowledgement 실패는 `IntelPublished`와 exact receipt를 보존하고 retry는 부적 선택·Sink·정보 효과를 반복하지 않음
공간·전력·물·연료·정비: 시설 footprint·접근칸·전력·용수·연료·정비·창고 용량 변경 0
위험·실패·회복 방식: operation/reason/commit/source/quantity/grams/item/phase 또는 saved unlocked membership이 다르면 대체 부적·추정값 없이 fail-loud함. `ItemCommitted` receipt missing은 거절하고, `IntelPublished`+exact membership의 already-acknowledged 경계만 cleanup 허용함
사회·비가역 비용: 명성·공포·소문·정찰 노동·세력·기분 수치 변경 0. crash/retry로 부적이 여러 번 사라지거나 부적만 사라지고 정보가 누락되는 비가역 상태만 제거함
기존 대안과의 장단점: synchronous Sink→unlock은 단순하지만 두 authority 사이 저장 가능한 phase가 없음. External Influence V4 outbox는 provenance 필드 비용이 있으나 domain-first publication과 acknowledgement-only replay를 보장함
지배 전략 방지 조건: same-site second Sink 0, acknowledgement 전 receipt 소실 0, 다른 charm/source substitution 0, second unlock effect 0, malformed current-format partial publication 0
저장 권위와 실행 명령: External Influence V4 aggregate가 site/operation/reason/commit/source/quantity/grams/item/phase와 unlocked membership을 소유하고 Physical pending receipt가 미acknowledged Sink custody를 소유함. 과거 save migration은 없으며 V4 누락·변조는 restore failure임
자동 감사 ID와 전수 목록 포함 여부: `ExternalInfluenceTrailCharmOutboxDebugScenarios.RunAll()`이 one-debit, retry, same-site repeat, item-committed restore, crash-after-ack restore와 tamper no-mutation을 검사하고 Batch A core/session save aggregate에 current V4 strict section을 포함함
검증 매트릭스와 보고서 위치: clean Unity compile; `ExternalInfluenceTrailCharmOutboxDebugScenarios.RunAll()`, `BatchACoreSessionSaveDebugScenarios.RunAll(false)` PASS; final Console Warning/Error 0/0. 계획 SHA-256 `F750EE24373CEAA0E32C8621E9CD04D709018C80E81F81B5790B09309402F34D`
현재 밸런스 상태: 밸런스 영향 없음 / 길잡이 부적 pending Sink·External Influence V4 current-format 구조 검증 PASS. 나머지 terminal domain, packaged lot·나머지 ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 예약 수량 pending Sink 원자 경계 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-reserved-quantity-pending-sink-atomic-boundary
콘텐츠 종류: quantity lease로 보호된 물리 아이템을 durable pending Sink로 전환하는 Items 공용 원자성 기반
정의·카탈로그·실행기 위치: ItemQuantityReservationService.cs, PhysicalItemBatchDispositionService.cs, DungeonWorldSimulationRegistration.cs, PhysicalStockQueryV18DebugScenarios.cs
등장 시대와 연구: 모든 시대의 예약 기반 terminal 소비에 공통 적용하며 연구·해금 조건 변경 0
플레이어에게 주는 새 결정: 새 명령·선택 0. 기존 예약 소비가 중단·저장 경계에서 다른 actor에게 탈취되거나 lease만 사라지지 않도록 함
물리 BOM·입력·출력: authored BOM·수량 변경 0. exact reserved source slice와 quantity를 terminal Sink하고 pending receipt에 exact source vector·quantity·positive grams를 기록함
직접 작업량과 계산 근거: Direct WU·운반 WU·25kg nominal·성능 band 변경 0. 기존 quantity lease owner operation을 physical operation identity로 재사용함
EWU와 목표 회수 기간: EWU·가격·ROI·회수율 변경 0. 중복 debit과 반쪽 rollback만 차단함
시간·확률·재시도: action 시간·확률 변경 0. 첫 commit 뒤 lease가 없어도 동일 pending receipt만 replay하고 RNG·source 선택을 반복하지 않음
공간·전력·물·연료·정비: 시설 footprint·창고 gram capacity·FacilityBuffer·전력·용수·연료·정비 변경 0
위험·실패·회복 방식: lease owner/operation/reason/quantity/source/signature mismatch는 fail-loud. 후행 publication 예외는 source quantity와 동일 lease ownership을 함께 복원하고 pending receipt를 제거함
사회·비가역 비용: 기분·관계·건강·영양 효과 변경 0. 물리 debit 성공 뒤 domain effect 누락 또는 실패 뒤 lease 소실을 막는 기반만 추가함
기존 대안과의 장단점: lease release 후 generic pending Sink는 단순하지만 경쟁 actor가 source를 가져갈 seam이 있음. 전용 atomic boundary는 snapshot·rollback 비용이 있으나 reservation과 physical custody를 함께 보존함
지배 전략 방지 조건: second Sink 0, operation 재사용 conflict 허용 0, 실패 시 free item·orphan lease 0, generic reserved-source bypass 0
저장 권위와 실행 명령: Physical current-format pending receipt가 미acknowledged Sink custody를 소유하고 ItemQuantityReservationService가 commit 직전 lease를 소유함. domain current-format outbox는 후속 수직 슬라이스에서 receipt를 join해야 함
자동 감사 ID와 전수 목록 포함 여부: `PhysicalStockQueryV18DebugScenarios.RunAll()`의 reserved pending Sink row가 commit/replay/ack/forced rollback을 실제 repository와 reservation service로 검사함
검증 매트릭스와 보고서 위치: clean Unity compile; `V27_RESERVED_PENDING_SINK_FOCUSED=PASS`; V18 physical stock aggregate PASS; final Console Warning/Error 0/0; 계획 SHA-256 `5748712A5AB4D91F6B037CFF16C4683F7180DFA5DCB7DDCDE1647CF8301C1B40`
현재 밸런스 상태: 밸런스 영향 없음 / 예약 수량 pending Sink Items 기반 PASS. Character Consumables current-format receipt publication, 나머지 terminal domains, packaged lot·ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 캐릭터 시설 식사 pending Sink outbox·Character Consumables V7 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-character-meal-pending-sink-outbox-v7
콘텐츠 종류: 시설 식사의 reserved physical Sink와 허기·기분·이벤트 publication 사이 중단 경계를 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: CharacterConsumablesRuntimeContracts.cs, CharacterConsumablesPersistenceContracts.cs, CharacterConsumablesStateRules.cs, CharacterConsumablesRuntime.cs, CharacterConsumablesApplicationAdapters.cs, CharacterConsumablesSaveSection.cs, PhysicalItemBatchDispositionService.cs, SurvivalDebugScenarios.cs
등장 시대와 연구: 기존 식사 시설과 physical meal이 가능한 전 시대에 공통 적용하며 연구·식사 정의·시설 역할·diet policy 해금 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령·선택 0. 기존 식사 행동은 유지하고 중단·저장·복원 뒤 같은 serving과 같은 효과만 정확히 한 번 완료함
물리 BOM·입력·출력: 기존 meal serving quantity 1 Sink를 유지. exact lease source vector, quantity 1, positive input grams를 Physical pending receipt와 Character Consumables V7 active plan에 저장함
직접 작업량과 계산 근거: 식사 action 4초, Direct WU·운반 WU·25kg nominal·19.1/28.65kg 및 23.88/35.81kg 성능 band 변경 0. 기존 ConsumableOperationId가 lease와 Sink의 단일 action identity임
EWU와 목표 회수 기간: 식사 EWU·가격·nutrition·mood·생존 ROI 변경 0. 중복 serving debit과 효과 재적용, receipt orphan만 차단하며 전수 kg After와 EWU·가격 재생성은 후속임
시간·확률·재시도: 식사 시간·오염/중독 확률 변경 0. commit 당시 policy violation·contamination을 저장하고 acknowledgement retry/restore에서 source·결과·효과를 다시 선택하지 않음
공간·전력·물·연료·정비: 식사 시설 footprint·FacilityBuffer·창고 gram capacity·전력·용수·연료·정비 변경 0. 기존 exact facility buffer와 slot reservation을 유지함
위험·실패·회복 방식: operation/reason/commit/source/quantity/grams/phase/completed-ledger mismatch와 ItemCommitted receipt missing은 aggregate publication 전에 fail-loud함. acknowledgement 실패는 EffectsPublished plan과 receipt를 보존하고 ack만 재시도함
사회·비가역 비용: 허기·기분·diet violation·ritual fasting·narrative authored 효과 변경 0. crash/retry로 식사가 여러 번 사라지거나 허기·기분이 중복 적용되는 비가역 상태만 제거함
기존 대안과의 장단점: lease 동기 consume→effects는 단순하지만 두 authority 사이 저장 가능한 custody가 없음. V7 pending outbox는 provenance 필드와 restore join 비용이 있으나 exact debit·domain-first publication·acknowledgement-only replay를 보장함
지배 전략 방지 조건: second Sink 0, second hunger/mood effect 0, 다른 meal/source substitution 0, acknowledgement 전 receipt 소실 0, tampered restore partial publication 0, spoil-before-commit free serving 0
저장 권위와 실행 명령: Character Consumables V7 aggregate가 active plan phase와 operation/reason/commit/source/quantity/grams/outcome 및 completed-operation ledger를 소유하고 Physical pending receipt가 미acknowledged Sink custody를 소유함. 과거 save migration은 없으며 V7 누락·부분 provenance는 current-format restore failure임
자동 감사 ID와 전수 목록 포함 여부: `SurvivalDebugScenarios.RunMealV7PendingOutboxFocused()`가 4초 reservation, first-ack failure, V7 JSON, exact receipt join, tampered grams no-mutation, missing receipt rejection, restore no-second-effect/no-second-debit와 spoil lease release를 검사함
검증 매트릭스와 보고서 위치: Unity clean compile; `V27_CHARACTER_MEAL_V7_PENDING_OUTBOX=PASS`; `V27_CHARACTER_MEAL_V7_COMPOSITION_SAVE=PASS`; focused detail `pending=3.9s; committed=4.1s; spoiled=abort; leaseReleased=True`; final Console Warning/Error 0/0. 계획 SHA-256 `CFC7707C2FE707899ACA7D18451FBB1F81A6F064E5A8862328B17DC6C0B0BD20`
현재 밸런스 상태: 밸런스 영향 없음 / 캐릭터 시설 식사 pending Sink·Character Consumables V7 current-format 구조 검증 PASS. 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 원시 야전 식사 pending Sink·Character Consumables V7 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-primitive-field-meal-pending-sink-v7
콘텐츠 종류: 원시 야전 식사의 reserved physical Sink와 허기·기분·이벤트 publication 사이 중단 경계를 닫고 복원된 시설 식사 slot ownership을 멱등 정리하는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: CharacterConsumablesRuntime.cs, CharacterConsumablesStateRules.cs, CharacterConsumablesRuntimeContracts.cs, CharacterConsumablesApplicationAdapters.cs, PhysicalItemBatchDispositionService.cs, SurvivalDebugScenarios.cs
등장 시대와 연구: 기존 `AIPrimitiveFieldMeal`과 physical meal을 사용할 수 있는 전 시대에 공통 적용하며 연구·식사 정의·diet policy·primitive fallback 조건은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령·선택 0. 기존 야전 식사는 유지하고 acknowledgement 실패·저장·복원 뒤 동일 serving과 동일 효과만 정확히 한 번 완료함
물리 BOM·입력·출력: 기존 meal serving quantity 1 terminal Sink를 유지. exact lease source vector, quantity 1, positive input grams를 Physical pending receipt와 Character Consumables V7 active plan에 저장함
직접 작업량과 계산 근거: 기존 field-meal action cadence, Direct WU·운반 WU·25kg nominal·19.1/28.65kg 및 23.88/35.81kg 성능 band 변경 0. aggregate-generated ConsumableOperationId가 exact action identity임
EWU와 목표 회수 기간: meal EWU·가격·nutrition·mood·생존 ROI 변경 0. 중복 serving debit·effect replay·facility slot orphan만 차단하며 전수 kg After와 EWU·가격 재생성은 후속임
시간·확률·재시도: 식사 시간·신선도·오염·policy 확률 변경 0. commit 당시 outcome을 저장하고 retry/restore에서 source·결과·효과를 다시 선택하지 않음
공간·전력·물·연료·정비: 시설 footprint·창고 gram capacity·FacilityBuffer·전력·용수·연료·정비 변경 0. 가상 시설은 canonical `building:primitive-field-meal` ID만 사용하며 실제 facility slot을 점유하지 않음
위험·실패·회복 방식: operation/reason/commit/source/quantity/grams/phase/completed-ledger mismatch와 missing receipt는 fail-loud함. first-ack failure는 `EffectsPublished`와 exact receipt를 보존하고 restore는 acknowledgement만 수행함
사회·비가역 비용: 허기·기분·diet violation·ritual fasting·narrative authored 효과 변경 0. crash/retry로 식사가 여러 번 사라지거나 효과가 중복되는 비가역 상태와 복원 후 시설 slot 교착만 제거함
기존 대안과의 장단점: synchronous reserved consume는 단순하지만 Physical/Survival 사이 저장 가능한 custody가 없음. V7 outbox는 provenance 필드와 receipt join 비용이 있으나 exact debit·domain-first publication·acknowledgement-only replay를 보장함
지배 전략 방지 조건: second Sink 0, second hunger/mood effect 0, 다른 meal substitution 0, receipt orphan 0, virtual facility typed-ID bypass 0, restore facility-slot leak 0, spoil-before-commit free serving 0
저장 권위와 실행 명령: Character Consumables V7 aggregate가 field plan phase와 exact physical provenance·completed ledger를 소유하고 Physical pending receipt가 미acknowledged Sink custody를 소유함. 과거 save migration은 없으며 canonical virtual BuildingInstanceId와 V7 필수 provenance 누락은 current-format restore failure임
자동 감사 ID와 전수 목록 포함 여부: `SurvivalDebugScenarios.RunMealV7PendingOutboxFocused()`가 시설/야전 first-ack failure, exact quantity debit, V7 restore no-replay, spoil typed abort와 lease release를 실제 repository·reservation service로 검사함
검증 매트릭스와 보고서 위치: Unity clean compile; `V27_CHARACTER_MEAL_V7_PENDING_OUTBOX=PASS facility=pending/restore-exact; field=pending/restore-exact; spoiled=abort; leaseReleased=True`; `V27_CHARACTER_MEAL_V7_COMPOSITION_SAVE=PASS composition=True;saveSections=True`; final Console Warning/Error 0/0. 계획 SHA-256 `40F0C64E20F2B8DEBFF634F47AA23E9F71E2ABFDB80AD99DC1934A3AC2A4AE5F`
현재 밸런스 상태: 밸런스 영향 없음 / 원시 야전 식사 pending Sink·Character Consumables V7 구조 및 복원 검증 PASS. 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 캐릭터 물질 사용 pending Sink outbox·Character Consumables V8 후속 기록 (2026-08-21)

```text
정의 ID: architecture:v27-character-substance-pending-sink-outbox-v8
콘텐츠 종류: Loose·FacilityBuffer·Carried 물질 dose의 physical Sink와 tolerance·addiction·overdose·캐릭터 효과 publication 사이 중단 경계를 닫는 current-format exact-once 구조 수직 슬라이스
정의·카탈로그·실행기 위치: CharacterConsumablesRuntimeContracts.cs, CharacterConsumablesPersistenceContracts.cs, CharacterConsumablesStateRules.cs, CharacterConsumablesRuntime.cs, CharacterConsumablesApplicationAdapters.cs, CharacterConsumablesSaveSection.cs, PhysicalItemBatchDispositionService.cs, DungeonWorldSimulationRegistration.cs, SurvivalDebugScenarios.cs
등장 시대와 연구: 기존 substance item과 정책·선술집 서비스가 가능한 전 시대에 공통 적용하며 연구·아이템 정의·시설 역할·물질 해금은 변경하지 않음
플레이어에게 주는 새 결정: 새 명령·선택 0. 기존 복용·음용 행동은 유지하고 중단·저장·복원 뒤 동일 dose와 이미 결정된 결과만 정확히 한 번 완료함
물리 BOM·입력·출력: 기존 substance quantity 1 terminal Sink를 유지. Loose/FacilityBuffer는 generic pending batch, Carried는 narrow carried-Sink capability로 exact stack 1개와 positive input grams를 receipt와 V8 plan에 기록함
직접 작업량과 계산 근거: 기존 물질 사용 action cadence, Direct WU·운반 WU·25kg nominal·19.1/28.65kg 및 23.88/35.81kg 성능 band 변경 0. 기존 ConsumableOperationId가 physical Sink와 domain publication의 단일 action identity임
EWU와 목표 회수 기간: 물질 EWU·가격·효과·소비량·선술집 ROI 변경 0. 중복 dose debit·RNG reroll·effect replay만 차단하며 전수 kg After와 EWU·가격 재생성은 후속임
시간·확률·재시도: duration·tolerance/addiction/overdose 확률과 공식 변경 0. 최초 commit 전 결과와 absolute target을 한 번 결정해 저장하고 acknowledgement retry/restore에서는 RNG·source·결과를 다시 선택하지 않음
공간·전력·물·연료·정비: 창고·FacilityBuffer·선술집 footprint·gram capacity·전력·용수·연료·정비 변경 0. FacilityBuffer beverage도 동일 pending Sink custody를 사용함
위험·실패·회복 방식: operation/reason/commit/source/quantity/grams/phase/completed-ledger mismatch와 missing receipt는 aggregate publication 전에 fail-loud함. acknowledgement 실패는 EffectsPublished plan·ledger·receipt를 보존하고 ack만 재시도함. carried commit 실패는 carry snapshot을 복원함
사회·비가역 비용: mood·work/combat modifier·withdrawal·중독·과다복용 authored 효과 변경 0. crash/retry로 dose가 여러 번 사라지거나 효과/RNG가 중복되는 비가역 상태만 제거함
기존 대안과의 장단점: synchronous consume→effect는 단순하지만 Physical/Survival 사이 저장 가능한 custody가 없음. V8 outbox는 provenance·absolute target 필드와 restore join 비용이 있으나 exact debit·domain-first publication·acknowledgement-only replay를 보장함
지배 전략 방지 조건: second Sink 0, second tolerance/addiction/overdose roll 0, 다른 dose/source substitution 0, carry/world 이중 잔존 0, acknowledgement 전 receipt 소실 0, tampered restore partial publication 0
저장 권위와 실행 명령: Character Consumables V8 aggregate가 substance plan phase·exact receipt provenance·absolute outcome·completed ledger를 소유하고 Physical pending receipt가 미acknowledged Sink custody를 소유함. 과거 save migration은 없으며 V8 필수 provenance 누락은 current-format restore failure임
자동 감사 ID와 전수 목록 포함 여부: `SurvivalDebugScenarios.RunSubstanceV8PendingOutboxFocused()`가 Loose first-ack replay, different-RNG restore, grams tamper, missing receipt, duplicate command, policy/missing stack, Carried carry/world exact debit를 검사하고 `SurvivalDebugScenarios.RunAll()`이 선술집 FacilityBuffer 경로와 typed localization parity를 포함함
검증 매트릭스와 보고서 위치: Unity clean compile; focused `stack=...; quantity=2->1->1; ledger=1; V8=ack-replay; carried=exact`; `V27_SURVIVAL_FULL=PASS scenarios=all`; `V27_CHARACTER_SUBSTANCE_V8_COMPOSITION_SAVE=PASS composition=True;saveSections=True`; final Console Warning/Error 0/0. 계획 SHA-256 `33838B228D634898EC09915CE6424C227101ED9BF753CE8AF088E816D0AE1B30`
현재 밸런스 상태: 밸런스 영향 없음 / 캐릭터 물질 사용 pending Sink·Character Consumables V8 current-format 구조 검증 PASS. arbitrary subscriber-exception atomicity, 나머지 terminal domain, packaged lot·ingress, package tare, 전수 kg After, EWU·가격·6인 생존망·최종 3-seed가 남아 물리 중량 또는 밸런스 완료가 아님
```

## V27 packaged-lot 질량 권위·수술 tare outbox 기반 기록 (2026-08-23)

```text
정의 ID: architecture:v27-packaged-lot-surgery-tare-outbox-foundation
콘텐츠 종류: 포장된 물리 lot의 content/tare 질량 권위와 수술 terminal Sink 뒤 물리 tare 부산물 exact-once publication 기반
정의·카탈로그·실행기 위치: ItemDefinitionSO.cs, DungeonItemDefinition.cs, PackagedLotPhysicalMassProjector.cs, PhysicalItemMassQuery.cs, SurgeryLogisticsRuntime.cs, SurgeryRuntime.cs, PhysicalStockQueryV18DebugScenarios.cs
등장 시대와 연구: 전 시대 packaged item에 공통 적용 가능한 기반이며 실제 아이템·연구·해금 조건 변경 0
플레이어에게 주는 새 결정: 현재 새 선택 0. 실제 reusable-container 또는 disposable-waste 콘텐츠 적용 전까지 authored item 동작은 기존과 동일함
물리 BOM·입력·출력: authored BOM·아이템 수·unit kg 변경 0. 향후 packaged definition은 total=content+tare를 exact gram으로 소유하고 수술 Sink는 tare output을 물리 Loose stack으로 반환해야 함
직접 작업량과 계산 근거: Direct WU·운반 WU·25kg nominal·성능 band 변경 0. packaged total mass를 기존 generic total 위에 중복 가산하지 않음
EWU와 목표 회수 기간: EWU·가격·ROI·회수율 변경 0. 실제 container/waste producer-consumer closure 후 재생성 예정
시간·확률·재시도: 수술 시간·성공률·위험 공식 변경 0. material Sink receipt와 tare output commit marker를 재사용해 retry/restore에서 두 번째 debit·출력을 금지함
공간·전력·물·연료·정비: 시설 footprint·창고 gram capacity·FacilityBuffer·전력·용수·연료·정비 변경 0. tare output은 exact owned surgery drop cell의 Loose 물리 stack으로만 출현함
위험·실패·회복 방식: invalid tare/disposition, missing output definition, tare/output mass mismatch, receipt quantity/gram mismatch, output marker conflict는 domain publication 전에 fail-loud함
사회·비가역 비용: 건강·기분·관계·수술 효과 변경 0. crash/ack failure에서 포장 질량이 사라지거나 두 번 생성되는 비가역 상태를 막는 기반만 추가함
기존 대안과의 장단점: synchronous consume는 단순하지만 package tare가 증발하고 저장 경계가 없음. pending Sink+output marker는 provenance 비용이 있으나 exact physical custody와 idempotent replay를 제공함
지배 전략 방지 조건: second material Sink 0, second tare output 0, total/content/tare double count 0, unrelated container substitution 0, receipt mismatch silent acceptance 0
저장 권위와 실행 명령: Physical current-format pending batch receipt가 미acknowledged Sink를 소유하고 SurgeryOrder.materials/materialsConsumed가 domain 상태를 소유함. tare output stack의 production-output commit component가 output exact-once identity임
자동 감사 ID와 전수 목록 포함 여부: `PhysicalStockQueryV18DebugScenarios.RunAll()`에 160=130+30g projection과 30/40g mismatch fail-loud fixture를 추가함. 실제 packaged asset과 production-live 수술 행은 아직 미포함
검증 매트릭스와 보고서 위치: Unity 6000.3.8 Bee/Roslyn Assembly-CSharp 및 Assembly-CSharp-Editor compile PASS; Unity MCP connection approval revoke로 focused 실행·Console 0/0은 대기; 계획 SHA-256 `C61FDFDF2E12870A43D806FD535AA496C38B566B4CC7408520B2B57D99E0C548`
현재 밸런스 상태: 밸런스 영향 없음 / packaged-lot runtime·수술 tare outbox 기반 compile PASS. 실제 packaging BOM/disposition·producer/consumer·production-live/save evidence와 전수 kg After·EWU·가격·6인 생존망·최종 3-se드는 미완료
```

## V27 packaged-lot 공통 tare outbox·캐릭터 소비 연결 기록 (2026-08-23)

```text
정의 ID: architecture:v27-packaged-lot-common-tare-outbox-v1
콘텐츠 종류: 포장된 terminal Sink의 reusable container·disposable waste 물리 반환을 수술·식사·물질 사용이 공유하는 Items exact-once 경계
정의·카탈로그·실행기 위치: PackagedLotTareDispositionService.cs, DungeonWorldSimulationRegistration.cs, SurgeryLogisticsRuntime.cs, SurgeryRuntimeServices.cs, CharacterConsumablesRuntimeContracts.cs, CharacterConsumablesRuntime.cs, CharacterConsumablesApplicationAdapters.cs, PhysicalStockQueryV18DebugScenarios.cs
등장 시대와 연구: 기존 전 시대 수술·식사·물질 사용 소비 경로에 적용 가능한 구조 기반이며 실제 포장 아이템·연구·해금 변경 0
플레이어에게 주는 새 결정: 현재 새 선택 0. 실제 container/waste 정의가 승인되기 전 기존 authored item의 출력과 사용 경험은 변경하지 않음
물리 BOM·입력·출력: authored BOM·아이템 수·unit kg 변경 0. 향후 packaged Sink는 exact consumed quantity에 비례한 tare output을 actor 또는 owned facility cell에 물리 Loose stack으로 반환함
직접 작업량과 계산 근거: Direct WU·운반 WU·25kg nominal·19/29 및 24/36 성능 band 변경 0. tare output은 일반 haul 대상이 되며 별도 순간이동 저장을 사용하지 않음
EWU와 목표 회수 기간: EWU·가격·ROI·회수율 변경 0. 실제 reusable/disposable topology와 producer-consumer closure 뒤 물류·폐기 처리 비용을 포함해 재생성함
시간·확률·재시도: 소비 시간·수술 시간·효과 확률 변경 0. parent physical commit ID에서 output commit ID를 결정해 acknowledgement 실패·복원 재시도에서도 두 번째 tare 출력을 금지함
공간·전력·물·연료·정비: 시설 footprint·창고 gram capacity·FacilityBuffer·전력·용수·연료·정비 변경 0. 반환된 Loose tare는 향후 저장·폐기 물류와 Floor Clutter 검증 대상임
위험·실패·회복 방식: duplicate marker, wrong quantity/state/position, invalid context, implicit destroyed tare, transferred-output misuse는 receipt acknowledgement 전에 fail-loud하며 pending receipt를 보존함
사회·비가역 비용: 건강·기분·중독·관계·수술 결과 변경 0. 포장 물리 자본의 묵시적 소멸 또는 acknowledgement replay에 의한 복제만 차단함
기존 대안과의 장단점: 소비 도메인별 출력 코드는 단순하지만 정책이 드리프트함. 공통 Items outbox는 adapter 비용이 있으나 동일 질량·marker·replay 계약을 수술·식사·물질에 강제함
지배 전략 방지 조건: second Sink 0, second tare output 0, actor/facility 간 출력 teleport 0, marker 충돌 silent substitution 0, destroyed tare without loss receipt 0
저장 권위와 실행 명령: Physical current-format pending batch receipt가 미acknowledged Sink를 소유하고 output stack의 production-output commit component가 tare publication identity를 소유함. 각 도메인은 output 보장 뒤에만 receipt를 acknowledge함
자동 감사 ID와 전수 목록 포함 여부: `PhysicalStockQueryV18DebugScenarios.RunAll()`이 2개/60g exact output, replay spawn 1회, marker-position conflict, explicit-loss gate를 검사함. `SurvivalDebugScenarios.RunPackagedConsumableTareRecoveryFocused()`가 missing-service pending 보존과 service 복구 후 1회 반환을 검사하며, 마취제 production-live 행은 별도 v2/v3 기록으로 연결됨
검증 매트릭스와 보고서 위치: Unity 6000.3.8 current assembly에서 packaged consumable focused fixture PASS(`missing-service=pending; effects=1; restored-tare=1; replay=0`), 전체 `SurvivalDebugScenarios.RunAll()` PASS, 기존 마취제 production-live DI 수술→바이알 회수→재생산 PASS, 직후 Console Warning/Error 0/0
현재 밸런스 상태: 밸런스 영향 없음 / 공통 tare outbox와 수술·식사·물질 callsite, missing-service fail-closed, 복원 후 tare exact-once 및 첫 실제 마취제 production-live 경로 PASS. 나머지 item별 packaging BOM/disposition·producer/consumer, 전수 kg After·EWU·가격·6인 생존망·최종 3-se드는 미완료
```

## V27 마취제·재사용 의료 바이알 authoring 폐쇄 루프 기록 (2026-08-23)

```text
정의 ID: balance:v27:anesthetic-reusable-medical-vial-v1
콘텐츠 종류: terminal 수술 Sink에서 회수되는 재사용 의료 바이알과 마취제 packaged-lot 첫 실제 authoring 후보
정의·카탈로그·실행기 위치: ResourceEconomyAssetBuilder.cs, ItemDefinitionSO.cs, PackagedLotPhysicalMassProjector.cs, PackagedLotTareDispositionService.cs, SurgeryLogisticsRuntime.cs
등장 시대와 연구: `research:pharmacology:anesthesia`; 기존 마취제 해금 시대는 유지하고 같은 연구에서 바이알 제작을 해금함
플레이어에게 주는 새 결정: 마취제 제작 전에 재사용 바이알을 확보하고, 수술 뒤 반환된 바이알을 회수·보관·재사용해야 함
물리 BOM·입력·출력: 바이알은 철괴 1개 900g→30개×30g exact; 마취제는 기존 몽엽 2+알코올 1에 바이알 1개를 추가하고 120g 완제품 1개를 출력하며 수술 Sink 뒤 바이알 1개/30g을 반환함
직접 작업량과 계산 근거: 바이알 recipe authored direct WU 후보 12; 마취제 authored direct WU 16 유지. builder balance projector 적용 뒤 실제 값 재검증 전 확정값으로 승격하지 않음
EWU와 목표 회수 기간: 아직 재생성하지 않음. reusable 자본은 제작·회수·운반·분실 위험을 포함한 순환 자본으로 계산해야 하며 positive SCC 차익 0을 후속 감사함
시간·확률·재시도: 마취제 결과·수술 시간·효과 확률 변경 0. tare output은 parent physical Sink commit에서 exact-once이며 ack/restore 재시도 때 재생성하지 않음
공간·전력·물·연료·정비: 바이알은 일반 warehouse gram capacity와 Loose floor clutter를 사용함. 별도 순간이동 회수·무한 전용 저장은 없음
위험·실패·회복 방식: 바이알 부족 시 마취제 recipe 입력 부족으로 대기; 출력 공간·tare publication 실패 시 수술 receipt를 보존하고 효과 acknowledgement 전에 재시도함
사회·비가역 비용: 수술 건강 효과 변경 0. 빈 용기 자본의 묵시적 삭제·복제와 downstream transform에서의 미기록 tare 손실을 금지함
기존 대안과의 장단점: disposable medical waste보다 신규 폐기 처리 부담이 작고 폐쇄 루프가 명확함. 대신 회수·운반 비용과 바이알 초기 자본이 생김
지배 전략 방지 조건: 철괴→바이알 질량 생성 0, 수술당 바이알 중복 반환 0, acknowledgement replay 복제 0, packaged Transform 미지원 상태에서 마취제를 중간재로 사용하는 recipe 0
저장 권위와 실행 명령: economy item/recipe SO가 authored 권위, immutable packaged runtime snapshot이 gameplay query 권위, physical pending Sink receipt와 output commit marker가 미ack·반환 exact-once 권위
자동 감사 ID와 전수 목록 포함 여부: `ResourceEconomyAssetBuilder.ValidateMedicalVialTopology`가 120=90+30g, 900g input/output, vial input/output, terminal-only topology를 검사함. 실제 SO·카탈로그 전수 목록은 재생성 전이라 미포함
검증 매트릭스와 보고서 위치: Unity 6000.3.8 Bee/Roslyn Economy/runtime/Editor compile exit 0; Unity builder·focused contract·수술 PlayMode·Console 0/0은 대기; 계획 SHA-256 `23399AB43011763FD609FC075D6DE32AF49A2325374BB6ECC94AD6BDF21B7A3E`
현재 밸런스 상태: 첫 packaged authoring 후보 구조 검증 PASS / 실제 SO 적용·WU/EWU·가격 재생성·생산 및 수술 live loop·6인 생존망 전에는 밸런스 완료가 아님
```

## V27 마취제·재사용 의료 바이알 실제 적용·Unity MCP 검증 후속 기록 (2026-08-24)

```text
정의 ID: balance:v27:anesthetic-reusable-medical-vial-applied-v2
콘텐츠 종류: 첫 reusable packaged-lot의 실제 ScriptableObject·카탈로그·수술 terminal Sink·current-format 복원·결정론 수직 슬라이스
정의·카탈로그·실행기 위치: ResourceEconomyAssetBuilder.cs, GameContentCatalogAssetBuilder.cs, ItemDefinitionSO.cs, PackagedLotTareDispositionService.cs, SurgeryLogisticsRuntime.cs, SurgeryPlayModeVerifier.cs, CharacterConsumablesRuntime.cs
등장 시대와 연구: `research:pharmacology:anesthesia`; 마취제의 기존 연구 단계는 유지하고 같은 연구에서 의료 바이알 제작을 해금함
플레이어에게 주는 새 결정: 마취제 생산 전 바이알 자본을 확보하고 수술 뒤 Loose로 반환된 바이알을 운반·저장·재사용해야 함. 별도 순간이동 회수나 무한 전용 저장은 없음
물리 BOM·입력·출력: `container:medical-vial` 30g, MaxStack 300; `recipe:medical-vial` 철괴 1×900g→바이알 30×30g; `medicine:anesthetic` 120g=내용물 90g+바이알 30g, MaxStack 75; 마취제 recipe는 몽엽 2+알코올 1+바이알 1→마취제 1; 실제 foreign-body 수술은 authored facility 보정을 합쳐 마취제 2·소독약 2를 소비하고 바이알 2×30g을 반환함
직접 작업량과 계산 근거: builder seed work는 바이알 12·마취제 16이지만 gameplay 권위가 아니다. 현재 authored RequiredWork는 바이알 batch 160, 마취제 recurring 28이다. 일반 운반 band에서 바이알 200개=6kg·300개=9kg, 마취제 50개=6kg·75개=9kg로 6–11kg ordinary batch 범위를 만족함
EWU와 목표 회수 기간: 이번 단계에서는 EWU·가격·회수 기간을 확정하지 않음. reusable 바이알은 초기 제작·회수 운반·분실 위험을 가진 순환 자본으로 후속 SCC/가격 재생성에 포함해야 함
시간·확률·재시도: 수술 시간·성공률·마취 효과 변경 0. material Sink parent commit에서 바이알 output commit을 파생해 acknowledgement·save/restore retry에서 두 번째 debit·출력을 금지함
공간·전력·물·연료·정비: 바이알은 일반 Loose/warehouse gram 경로를 사용함. 시설 footprint·전력·용수·연료 수치 변경 0이며 반환 stack은 실제 수술대 owned drop cell에 생성됨
위험·실패·회복 방식: missing/mismatched tare output, wrong marker/position/quantity, receipt mismatch, packaged anesthetic downstream Transform 추가, topology mass drift는 domain publication 전에 fail-loud함. 만료된 식사 delivery 교체는 새 request 성공 뒤 old row와 route index를 원자 swap해 unrelated current-format restore 중복을 차단함
사회·비가역 비용: 건강·기분·관계·수술 효과 변경 0. 포장 자본의 묵시적 증발·복제와 save/retry 중복만 제거함
기존 대안과의 장단점: disposable waste보다 폐기 처리 부담이 작고 900g 폐쇄 질량 루프가 명확함. 대신 초기 철 자본과 실제 회수 물류가 필요하며 바이알 손실 시 마취 생산이 정지할 수 있음
지배 전략 방지 조건: 철괴→바이알 질량 생성 0, 수술당 second vial return 0, restore/retry duplication 0, packaged Transform 미지원 상태의 anesthetic consumer recipe 0, no-op builder YAML RID churn 0
저장 권위와 실행 명령: Economy SO가 authored BOM/질량 권위, immutable packaged snapshot이 gameplay 질량 query 권위, Physical pending Sink receipt와 output commit component가 미ack/반환 exact-once 권위. 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: canonical item 414, recipe 355, serialized weight site 1,074, explicit semantic 51/414, remaining 363, package contract 1, unknown writer 0. `ValidateMedicalVialTopology`, authority inventory, explicit semantic, common tare focused contract에 포함됨
검증 매트릭스와 보고서 위치: fresh `surgery-playmode-report.txt` PASS(마취제 2 Sink, 바이알 2/60g return, restore 2→2, captured 0/0); `physical-item-logistics-playmode-report.txt` PASS; `ai-mid-action-save-load-playmode.txt` PASS; Survival/runtime composition/save sections PASS; Economy 494-file 연속 rebuild digest `D79A6DD98AFF500DBBE2A67CABD083C4768C5846A9B0689F18FF163D1F731997`; V27 artifact 8종 연속 hash identity; 계획 SHA-256 `C4F9B86BC6D17B5985DC706E1ED256E897DF4FCE2D92216B0DA69199D1B20B30`
현재 밸런스 상태: 첫 packaged-lot 실제 적용·수술 회수·복원·결정론 수직 슬라이스 PASS. 반환 바이알의 production-live warehouse 재입고→재생산, 나머지 363개 unit semantic, packaged Transform, 전수 EWU·가격, 6인 생존망, Floor Clutter/paired run, 최종 3-seed가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 마취제·재사용 의료 바이알 production-live 재활용 순환 기록 (2026-08-24)

```text
정의 ID: balance:v27:anesthetic-reusable-medical-vial-production-recycle-v3
콘텐츠 종류: 실제 수술 반환 바이알의 AI 창고 회수·약제대 입력 운반·마취제 재생산·current-format 복원 폐쇄 루프
정의·카탈로그·실행기 위치: SurgeryPlayModeVerifier.cs, SurgeryLogisticsRuntime.cs, PackagedLotTareDispositionService.cs, AIHaul.cs, WorldItemHaulPlanningService.cs, ProductionItemGateway.cs, ProductionWorkshopAbilities.cs, CharacterConsumablesRuntime.cs
등장 시대와 연구: `research:pharmacology:anesthesia`; 기존 연구 단계와 해금 관계를 유지하며 실제 약제대 `P18_약제대`에서 검증함
플레이어에게 주는 새 결정: 수술 뒤 바닥에 반환된 바이알을 일반 물류로 회수해 창고에 저장하고 다음 마취제 생산에 재투입해야 함. 반환 바이알을 방치하면 초기 철 자본과 생산 연속성을 잃음
물리 BOM·입력·출력: 수술 마취제 2개 Sink→바이알 2개/60g Loose 반환; 후속 recipe는 몽엽 2+알코올 1+회수 바이알 1→마취제 1, 결과 바이알 재고 2→1·마취제 0→1. 신규 수치 변경 없이 v2 authored BOM을 실제 실행으로 검증함
직접 작업량과 계산 근거: builder seed work 12/16과 gameplay RequiredWork를 구분한다. 현재 authored RequiredWork는 바이알 batch 160, 마취제 recurring 28이며, 25kg nominal과 실제 AI haul cadence를 유지함. 검증에는 실제 AI path/reservation/pickup/warehouse admission/input delivery와 actual production work를 사용함
EWU와 목표 회수 기간: EWU·가격·회수 기간은 아직 재생성하지 않음. 이번 행은 reusable 자본의 물리 폐쇄와 exact 수량 보존만 증명하며 경제 균형 승인은 후속 SCC/가격/6인 생존망 감사에 남김
시간·확률·재시도: 수술·생산 시간과 확률 변경 0. 생산 one-cycle 결과를 완료 frame의 canonical `FacilityOutputBuffer`에서 관찰하고 whole-save restore 뒤 마취제 1·바이알 1을 유지해 second output/debit 0을 증명함
공간·전력·물·연료·정비: 실제 grid에 배치된 약제대와 실제 warehouse/FacilityBuffer/FacilityOutputBuffer를 사용함. footprint·전력·용수·연료·정비 수치 변경 0이며 별도 순간이동 저장이나 무한 fixture buffer를 사용하지 않음
위험·실패·회복 방식: AI가 합법적인 다른 warehouse를 선택할 수 있으므로 특정 창고를 강제하지 않고 실제 destination을 추적함. missing input/output, wrong buffer state, duplicate output, stale receipt 또는 save restore mismatch는 fail-loud함. 퇴역 character·사라진 facility의 stale consumables delivery는 Capture 전에 live membership으로 제거함
사회·비가역 비용: 건강·기분·관계·수술 효과 변경 0. 수술 반환 자본의 분실·복제·저장 순간이동과 생산 retry의 중복 소비/출력만 차단함
기존 대안과의 장단점: verifier가 source를 창고로 직접 옮기거나 output을 직접 spawn하면 빠르지만 실제 AI/예약/입고/생산 권위를 검증하지 못함. production-live 경로는 실행 시간이 길지만 플레이어가 겪는 동일한 물류·시설·저장 경계를 통과함
지배 전략 방지 조건: 수술당 second vial return 0, AI intake 중 수량 손실·복제 0, 재생산의 second vial debit/output 0, save/restore rerun 0, 특정 warehouse fixture 의존 0, stale delivery 때문에 unrelated whole-save 실패 0
저장 권위와 실행 명령: Economy SO가 item/recipe 질량·BOM 권위, Physical world/warehouse stack이 수량·위치 권위, ProductionBill/WIP와 output commit component가 생산 exact-once 권위, Character Consumables current-format aggregate가 유효 delivery 권위를 소유함. 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: `ANESTHETIC_RECYCLE_RETURNED_VIAL_LOOSE_SOURCE`, `ANESTHETIC_RECYCLE_VIAL_AI_WAREHOUSE_INTAKE`, `ANESTHETIC_RECYCLE_INPUTS_AI_DELIVERED`, `ANESTHETIC_RECYCLE_PRODUCTION_LIVE_EXACT_ONCE`, `ANESTHETIC_RECYCLE_PRODUCTION_RESTORE_NO_DUPLICATE`. 전수 목록은 item 414/recipe 355 중 이 폐쇄 루프를 포함하지만 remaining semantic 363은 미완료
검증 매트릭스와 보고서 위치: `Artifacts/QA/surgery-playmode-report.txt` fresh `RESULT=PASS; failures=0`; 바이알 returned/stored `2/2`, input vial `1`, anesthetic `0→1`, vial `2→1`, restore `1/1`; `SurvivalDebugScenarios.RunAll()` PASS; captured 및 Unity Editor Console Warning/Error `0/0`; 계획 SHA-256 `80A83DDD0157DC44101387F7A7B7DB86CE285203E91C2D177A1AE6F0F8290C9E`
현재 밸런스 상태: 첫 reusable packaged-lot의 production-live 재활용 순환 PASS. 나머지 363개 unit semantic, packaged Transform/다른 용기·폐기물, 나머지 ingress, 전수 kg After, EWU·가격, 6인 생존망, Floor Clutter/paired run, 최종 3-se드가 남아 전체 물리 중량 또는 밸런스 완료가 아님
```

## V27 물리 질량·장비·의복 current-revision Unity 재인증 기록 (2026-08-24)

```text
정의 ID: balance:v27:physical-mass-equipment-apparel-current-recert-v1
콘텐츠 종류: generic/unique/packaged 물리 질량 권위와 전투 장비·의복 동적 질량의 current Unity assembly 재인증
정의·카탈로그·실행기 위치: PhysicalStockQueryV18DebugScenarios.cs, V27PhysicalMassAuthorityInventoryDebugScenarios.cs, V27PhysicalMassExplicitSemanticDebugScenarios.cs, PhysicalItemDebugScenarios.cs, EquipmentItemStateV18DebugScenarios.cs, CombatEquipmentMaterialDebugScenarios.cs, V22ApparelDebugScenarios.cs
등장 시대와 연구: 전 시대 공통 기반. 기존 장비·의복 연구 해금과 시대 배치는 변경하지 않음
플레이어에게 주는 새 결정: 신규 결정 없음. 현재 장비·탄약·모듈·의복·멜빵의 운반 부담이 동일한 물리 질량 권위를 사용하는지 재인증함
물리 BOM·입력·출력: 신규 BOM 변경 0. base item+attached module+loaded ammunition을 합산하고 멜빵 physical 1,150g을 정확히 한 번 포함함
직접 작업량과 계산 근거: 신규 WU 변경 0. 장비/의복 질량 projector와 warehouse/carry/equipped read model의 current-revision 동일성만 검증함
EWU와 목표 회수 기간: 이 기록에서 EWU·가격·회수 기간을 재생성하지 않음. 전수 kg After 이후 handling EWU와 시장 가격을 별도 재생성해야 함
시간·확률·재시도: module/ammunition state revision, warehouse admission, restore/retry가 같은 immutable prepared subject를 사용하며 중복 질량 계상 0을 요구함
공간·전력·물·연료·정비: 창고는 positive gram capacity를 사용하며 신규 footprint·전력·용수·연료·정비 변경 0
위험·실패·회복 방식: unknown mass writer, missing definition, duplicate component, mismatched instance projection은 fail-loud함. 저장 DTO를 gameplay 질량 query로 사용하지 않음
사회·비가역 비용: 품질·내구·신선도·오염·젖음·충전량은 별도 물리 성분이 없는 V27 범위에서 질량 불변이며 사회·건강 효과 변경 0
기존 대안과의 장단점: 각 UI·창고·전투 시스템이 독자 weight를 보유하는 방식보다 단일 prepared subject가 일관적이지만, remaining semantic 363개를 작성하기 전에는 전수 kg 자연스러움을 보장하지 않음
지배 전략 방지 조건: module/ammunition 누락으로 장비 부담 축소 0, 멜빵 자체 질량 이중/누락 계상 0, 상태 변화로 질량 리롤 0, unknown writer 0
저장 권위와 실행 명령: Economy/Items authored definition과 validated runtime component snapshot이 질량 권위이며 저장 adapter는 검증된 DTO를 runtime subject로 변환함. 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: item 414, recipe 355, weight site 1,074, equipment 61, apparel 56, explicit semantic 51, remaining 363, unknown writer 0; PhysicalStockQuery V18/full equipment/apparel marker 전부 PASS
검증 매트릭스와 보고서 위치: Unity MCP current assembly에서 PhysicalStockQueryV18, PhysicalItem, EquipmentItemStateV18, CombatEquipmentMaterial, V22Apparel, authority inventory, explicit semantic PASS; 실행 직후 Unity Editor Console Warning/Error 0/0
현재 밸런스 상태: Phase 4 전투 장비·의복 통합 재인증 PASS. 363개 semantic, 355 recipe 전수 질량, 다른 packaged Transform/container/waste, 전수 kg After·EWU·가격·6인 생존망은 미완료
```

## V27 질병 현장 대응 물리 Sink·건강 결과 원자성 기록 (2026-08-24)

```text
정의 ID: balance:v27:disease-field-response-physical-sink-outbox-v1
콘텐츠 종류: 질병 현장 대응 8종 물리 재료의 exact facility-buffer Sink, 포장 tare 처리, 건강 결과 durable outbox
정의·카탈로그·실행기 위치: PopulationHealthDomain.cs, DiseaseFieldResponseRuntime.cs, PhysicalItemBatchDispositionService.cs, PackagedLotTareDispositionService.cs, DungeonAggregateReferencePreflight.cs
등장 시대와 연구: 기존 질병·의료 시설·response 해금 관계 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 밸런스 선택 없음. 실제 의료 시설 버퍼에 정확한 치료 재료가 있어야 효과가 게시되고, 포장품은 빈 용기 반환·폐기·명시 손실이 끝나야 치료가 완료됨
물리 BOM·입력·출력: response rule의 exact item/quantity 8종을 typed Sink. item kg·BOM·효과 수치 변경 0; packaged feature가 있는 경우 authored tare만 같은 parent commit에서 반환/폐기/손실 처리
직접 작업량과 계산 근거: WU·치료 시간 변경 0. 범용 count removal을 exact stack IDs·quantity·input grams receipt로 교체함
EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 최종 item semantic/package BOM 승인 뒤 물류·용기 회수 비용을 포함해 재생성함
시간·확률·재시도: 저장 sequence 기반 operation ID 사용. intent→Sink→tare→health→ack 순서이며 restore/retry에서 second item debit·second severity reduction·second tare output 0
공간·전력·물·연료·정비: 시설 footprint·utility 변경 0. package output은 처리 시설 center cell의 실제 Loose item이며 창고·Floor Clutter 대상임
위험·실패·회복 방식: item 부족, 예약 충돌, receipt mismatch, package disposition 실패, stale character/facility/disease/item reference는 health publication 전에 fail-loud. OutcomePublished 복원은 acknowledgement만 재시도함
사회·비가역 비용: 질병 severity reduction 값 변경 0. 아이템 없이 치료 효과 생성, 약품 소실 후 효과 누락, 복원 중 중복 치료만 차단함
기존 대안과의 장단점: 기존 단일 함수 count consumption은 단순하지만 저장 경계 원자성이 없었음. durable outbox는 schema/receipt 비용이 있으나 질량·효과를 exact-once로 결합함
지배 전략 방지 조건: free treatment 0, second Sink 0, second health outcome 0, packaged tare evaporation 0, pending receipt orphan 0
저장 권위와 실행 명령: field-response outbox는 PopulationHealth v2에서 도입됐고 aggregate current-format은 vaccination outbox를 포함한 v3임. intent/outcome phase와 monotonic sequence를 PopulationHealth가, item debit receipt를 Physical pending batch disposition이 소유함. startup recovery가 둘을 조인하며 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: runtime consumer owner `runtime:disease-field-response` exact 8 items; `DiseaseFieldResponseOutboxDebugScenarios.RunFromMenu()`이 2개/1,500g Sink, severity 50→36, ack, replay와 empty-intent recovery를 검사함
검증 매트릭스와 보고서 위치: DungeonStory.Species·Assembly-CSharp·focused Assembly-CSharp-Editor Roslyn exit 0, git diff check 0. Unity MCP에서 `DiseaseFieldResponseOutboxDebugScenarios.RunFromMenu()` focused fixture PASS 및 직후 Console Warning/Error 0/0을 확인함. 실제 환자·시설·AI 배송·whole-save PlayMode는 아직 미실행
현재 밸런스 상태: 질병 response의 물리 제거·건강 결과 원자성과 Unity focused fixture PASS. 실제 환자·시설·AI 배송 live 경로, item별 최종 kg/package BOM, Offense custody Transfer의 live 경로, 전수 EWU·가격·6인 생존망은 미완료
```

## V27 원정 보급품 물리 custody·반환 원자성 기록 (2026-08-24)

```text
정의 ID: balance:v27:offense-supply-custody-transfer-return-v1
콘텐츠 종류: 원정 보급품 11종의 집결지 physical stack→원정 package custody Transfer→잔여 물자 Source 반환 또는 명시 소비·손실
정의·카탈로그·실행기 위치: OffensePreparationService.cs, PhysicalItemBatchDispositionService.cs, PhysicalItemSourcePublicationService.cs, OffenseAggregateSaveValidation.cs, DungeonAggregateReferencePreflight.cs
등장 시대와 연구: 기존 원정·보급품 해금 관계 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 수치 선택 없음. 집결지에 실제 보급품이 도착해야 출정 custody가 성립하고, 귀환 시 package가 실제로 소유한 잔량만 바닥 Source로 돌아옴
물리 BOM·입력·출력: authored 보급품 item/quantity 변경 0. 출정 시 exact FacilityBuffer stacks를 Transfer하고, 귀환 시 owned subset만 Loose output으로 게시하며 input mass=returned mass+consumed/lost mass를 요구함
직접 작업량과 계산 근거: WU·출정 준비 시간 변경 0. 기존 count deletion/re-spawn을 source stack IDs·quantity·input gram·deterministic output commit이 있는 custody transaction으로 교체함
EWU와 목표 회수 기간: EWU·가격·원정 ROI 변경 0. 보급품 11종의 최종 kg·package BOM·전투 소비율 확정 뒤 운반·손실·반환 비용을 포함해 재생성함
시간·확률·재시도: package stable ID 기반 custody/return operation을 사용함. domain receipt 저장 전 acknowledge/claim revoke 금지, return intent 저장 전 output 금지, retry·restore second debit/output 0
공간·전력·물·연료·정비: 집결지 FacilityBuffer와 귀환 staging position의 실제 physical stack을 사용함. facility footprint·utility 변경 0; 반환 Loose는 정상 haul/Floor Clutter/저장 admission 대상임
위험·실패·회복 방식: destination claim 유실, reserved source, receipt conflict, acknowledgement 실패, over-return, unknown package, stale item reference, mass closure mismatch를 fail-loud함. pending Transfer와 ReturnPublishing intent는 current-format restore에서 재시도함
사회·비가역 비용: 전투·건강·관계 효과 변경 0. 전멸·포기 시 반환 0과 custody 전량 loss를 명시하고, 호출자가 package 없이 물자를 생성하거나 이미 반환한 물자를 재생성하지 못하게 함
기존 대안과의 장단점: count-only Consume/Spawn은 단순하지만 어떤 물리 lot이 사라졌는지, 귀환 stock이 원래 소유된 것인지 증명하지 못함. custody 영수증은 저장량이 늘지만 exact 소유권·질량·재시도 증거를 제공함
지배 전략 방지 조건: free expedition supply 0, unowned return 0, second Transfer 0, second return output 0, terminal restore mint 0, input/return/loss mass gap 0
저장 권위와 실행 명령: OffenseWorld current-format v7 package가 custody/return phase와 provenance를, Physical pending batch disposition이 world debit receipt를, output commit component가 반환 Source exact-once를 소유함. 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: runtime consumer owner `runtime:offense-supply-package` exact 11 items; `OffenseStrategicDebugScenarios`가 2개/2,000g Transfer, over-return 거절, 1개/1,000g 반환, residual 1,000g, replay/restore second output 0을 검사함
검증 매트릭스와 보고서 위치: Assembly-CSharp·Assembly-CSharp-Editor Roslyn exit 0, scoped git diff check 0. Unity MCP에서 `OffenseStrategicDebugScenarios.RunAll()` 11개 focused scenario PASS 및 직후 Console Warning/Error 0/0을 확인함. 실제 집결·출정·귀환 PlayMode는 아직 미실행
현재 밸런스 상태: 원정 package physical custody의 current-source 원자성과 Unity focused fixture PASS. 실제 집결·출정·귀환 live 실행, 보급품 최종 kg/package BOM, 전수 EWU·가격·6인 생존망·원정 ROI는 미완료
```

## V27 긴급 거점 완화 재료 WIP·결과 원자성 기록 (2026-08-24)

```text
정의 ID: balance:v27:offense-urgent-mitigation-physical-wip-outbox-v1
콘텐츠 종류: 긴급 거점 완화 시설의 물리 재료 Transfer-to-WIP, 월드 완화 결과 outbox, acknowledgement/restore 폐쇄
정의·카탈로그·실행기 위치: OffenseUrgentMitigationRuntime.cs, ProductionItemGateway.cs, PhysicalItemBatchDispositionService.cs, OffenseAggregateSaveValidation.cs, DungeonAggregateReferencePreflight.cs
등장 시대와 연구: 기존 urgent-site 정의와 mitigation work/facility 연구 관계 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 수치 선택 없음. 재료가 실제 시설 buffer에 도착하고 작업량을 채운 뒤에만 완화 WIP에 귀속되며, 귀속 뒤에는 취소로 재료를 되돌리거나 복제할 수 없음
물리 BOM·입력·출력: authored mitigation item/amount 변경 0. low fuel, lumber, standard medicine, mana crystal 네 물리 재료 경로를 exact FacilityBuffer stacks→abstract mitigation WIP Transfer로 기록하고 예상 외 잔여 납품은 Loose로 보존함
직접 작업량과 계산 근거: mitigationWork 변경 0. completedWork가 requiredWork에 도달한 시점에만 Transfer하고 source stack IDs/quantity/input grams가 있는 pending receipt를 사용함
EWU와 목표 회수 기간: EWU·가격·거점 ROI 변경 0. 최종 kg와 urgent-event 기대 손실을 확정한 뒤 운반·재료·시간 비용을 포함해 재생성함
시간·확률·재시도: deterministic operation ID를 사용하고 before/after mitigation을 저장함. current가 before면 delta 1회 게시, after면 restore replay로 인정, 그 외 값은 conflict. acknowledgement fault/restore에서 second Transfer·second outcome 0
공간·전력·물·연료·정비: 기존 mitigation facility와 destination cell 사용. footprint·utility 변경 0; reserved stack은 WIP input으로 훔치지 않음
위험·실패·회복 방식: missing material, reserved source, inactive site, authored cap 도달, receipt conflict, outcome state conflict, acknowledgement 실패, stale pending receipt를 fail-loud함. physical commit 이후 취소는 거부함
사회·비가역 비용: 관계·건강·전투 수치 변경 0. 재료만 사라지고 완화가 누락되거나 restore에서 완화가 중복되는 비가역 결함을 차단함
기존 대안과의 장단점: 기존 ConsumeDelivered→TryMitigate→RemoveDestination은 단순하지만 두 mutation 사이 저장 원자성이 없고 잔여 stock을 삭제할 수 있음. WIP/outbox는 provenance가 늘지만 exact 복구를 제공함
지배 전략 방지 조건: free mitigation 0, second material debit 0, second mitigation delta 0, post-commit cancel refund 0, residual delivery deletion 0, pending receipt orphan 0
저장 권위와 실행 명령: OffenseWorld current-format v7 order가 phase/receipt/before-after를, Physical pending batch disposition이 재료 Transfer를, urgent-site world state가 결과를 소유함. 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: runtime owner `runtime:offense-urgent-mitigation` exact 4 items; runtime consumer catalog 46 links, remaining packaging review 18 rows/19 links. focused Offense strategic scenario가 injected ack fault와 restore를 검사함
검증 매트릭스와 보고서 위치: Assembly-CSharp·Assembly-CSharp-Editor Roslyn exit 0. Unity MCP에서 `OffenseStrategicDebugScenarios.RunAll()` 11개 focused scenario와 독립 runtime-consumer contract gate PASS, 직후 Console Warning/Error 0/0을 확인함. 실제 배송·작업·시간 경과 PlayMode는 아직 미실행
현재 밸런스 상태: 긴급 완화 재료·월드 효과 current-source 원자성과 Unity focused fixture PASS. 실제 배송·작업·시간 경과 live 증거, 최종 kg/BOM/WU/EWU/ROI와 전수 untyped removal 0은 미완료
```

## V27 예방접종 물리 Sink·면역 결과 원자성 기록 (2026-08-24)

```text
정의 ID: balance:v27:physical-vaccination-sink-immunity-outbox-v1
콘텐츠 종류: 7종 예방접종의 exact facility-buffer Sink, 포장 tare 처리, 면역 결과 durable outbox
정의·카탈로그·실행기 위치: PopulationHealthDomain.cs, PhysicalVaccinationRuntime.cs, DiseaseFieldResponseRuntime.cs, PhysicalItemBatchDispositionService.cs, PackagedLotTareDispositionService.cs, DungeonAggregateReferencePreflight.cs
등장 시대와 연구: 기존 research:health:vaccination과 질병별 백신 해금 관계 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 밸런스 선택 없음. 실제 의료 시설 버퍼의 질병 일치 백신 1회분이 Sink되고 package terminal 처리가 끝난 뒤에만 면역이 게시됨
물리 BOM·입력·출력: runtime은 exact 백신 1개를 Sink하고 package feature가 승인되면 같은 parent commit으로 빈 용기 반환·폐기·명시 손실을 처리함. 현재 recipe 1 cycle은 1,540g input→1,600g output이라 최종 kg/BOM 승인은 보류함
직접 작업량과 계산 근거: 백신 제작·접종 WU 변경 0. 기존 count removal을 source stack IDs·quantity 1·input grams 영수증으로 교체함
EWU와 목표 회수 기간: EWU·가격·접종 ROI 변경 0. 7종 공통 단위 의미, 재사용 의료 바이알 BOM, 내용물과 공정 손실을 승인한 뒤 재생성함
시간·확률·재시도: monotonic vaccination sequence를 사용함. intent→Sink→tare→immunity→ack 순서이며 acknowledgement fault/restore에서 second debit·tare·immunity publication 0
공간·전력·물·연료·정비: 기존 의료 시설 destination과 실제 output cell 사용. footprint·utility 변경 0; 반환 바이알은 실제 physical output이므로 창고·haul·Floor Clutter 대상임
위험·실패·회복 방식: missing/mismatched vaccine, vaccine-disallowed disease, missing character/facility destination, receipt/package conflict, outcome publication/acknowledgement 실패를 fail-loud함. receipt 없는 intent는 면역·sequence 변경 없이 제거함
사회·비가역 비용: 면역 공식 70과 decay 변경 0. 백신 없이 면역 생성, 백신 소실 후 면역 누락, 복원 중 중복 접종을 차단함
기존 대안과의 장단점: 기존 consume-then-mutate는 단순하지만 두 권위 사이 저장 원자성이 없었음. 별도 vaccination outbox는 schema가 늘지만 건강한 대상과 활성 질병 치료의 검증 조건을 섞지 않고 exact recovery를 제공함
지배 전략 방지 조건: free immunity 0, second Sink 0, second tare output 0, second outcome 0, receipt orphan 0, 다회분 item을 1회분으로 암묵 소비 0
저장 권위와 실행 명령: PopulationHealth current-format v3가 vaccination intent/outcome/sequence를, Physical pending batch가 item debit을 소유함. startup recovery가 둘을 조인하며 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: runtime owner `runtime:physical-vaccination` exact 7 items; total runtime consumer links 53, remaining packaging review runtime rows/links 25/26. focused fixture가 1개/400g Sink와 ack fault/restore/replay를 검사함
검증 매트릭스와 보고서 위치: DungeonStory.Species·Assembly-CSharp·focused Assembly-CSharp-Editor Roslyn exit 0, scoped git diff check 0. Unity MCP에서 `PhysicalVaccinationOutboxDebugScenarios.RunFromMenu()` focused fixture PASS 및 직후 Console Warning/Error 0/0을 확인함. 실제 의료 시설 배송·접종·빈 바이알 회수 PlayMode는 아직 미실행
현재 밸런스 상태: 예방접종 물리 제거·면역 결과 원자성과 Unity focused fixture PASS. 백신 recipe의 현재 60g 질량 생성, 최종 unit kg/바이알 BOM/package feature, 실제 배송·접종 live 증거, EWU·가격·6인 생존망은 미완료
```

## V27 캐릭터 치료 재료 물리 Sink·주문 결과 원자성 기록 (2026-08-25)

```text
정의 ID: balance:v27:character-medical-treatment-supply-sink-outbox-v1
콘텐츠 종류: 부상자 치료용 live medicine 7종과 추출 혈액 fallback의 exact facility-buffer Sink, 포장 tare 처리, 치료 주문 durable outbox
정의·카탈로그·실행기 위치: CharacterMedicalSupplyPolicy.cs, CharacterMedicalSupplyCoordinator.cs, CharacterMedicalRuntime.cs, CharacterMedicalRestoreRuntime.cs, CharacterMedicalSaveValidation.cs, PhysicalFacilityItemSinkGateway.cs, PhysicalItemRuntimeConsumerCatalog.cs, DungeonAggregateReferencePreflight.cs
등장 시대와 연구: 각 기존 medicine·의료 시설·포로 혈액 추출의 연구/해금 관계 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 밸런스 선택 없음. 가능한 치료약을 potency/가격 정책으로 고르고 없으면 exact 추출 혈액을 저효율·감염/불안정 비용과 함께 사용하던 기존 선택을 실제 물리 재고와 원자적으로 연결함
물리 BOM·입력·출력: 치료당 exact item 1개를 Sink함. package feature가 있는 item은 같은 parent commit으로 빈 용기 반환·폐기·명시 손실을 먼저 처리함. sterile bandage·herbal poultice는 integral solid이며 추출 혈액은 current 500g generic item이나 최종 kg/용기 계약은 전수 감사 전 승인하지 않음
직접 작업량과 계산 근거: 안정화·운반·치료 WU와 치료 시간 변경 0. 기존 count removal을 source stack IDs·quantity 1·input grams 영수증으로 교체함
EWU와 목표 회수 기간: EWU·가격·치료 ROI 변경 0. 남은 liquid/kit package semantic과 recipe 질량을 승인한 뒤 전수 재생성함
시간·확률·재시도: 치료약 ranking·효과·추출 혈액 penalty 변경 0. monotonic order supply sequence를 사용하고 intent→Sink→tare→consumed publication→ack 순서로 acknowledgement fault/restore의 second debit/tare/publication 0을 요구함
공간·전력·물·연료·정비: 기존 의료 시설 destination과 실제 facility center output cell 사용. footprint·utility 변경 0; 반환 용기/폐기물은 실제 physical output이라 창고·haul·Floor Clutter 대상임
위험·실패·회복 방식: unknown item, wrong destination, reserved stack, receipt mismatch, package conflict, stale save, ack failure를 fail-loud함. receipt 없는 intent는 sequence·consumed 상태를 바꾸지 않고 제거하며 pending recovery 실패 중 destination을 삭제하지 않음
사회·비가역 비용: medicine 효과와 추출 혈액의 기존 infection 4·instability 6·치료 효율/회복 차이를 유지함. 무료 치료, 잘못된 Biological 소비, item 소실 뒤 치료 완료, 복원 중 중복 치료 재료 소비를 차단함
기존 대안과의 장단점: category Biological fallback은 구현이 단순하지만 56종의 장기·독소·폐기물까지 오소비할 수 있었음. exact captivity:extracted-blood는 생산·저장·운반 비용을 보존하지만 해당 재고가 없으면 명확히 배송 대기/공급 불가가 됨
지배 전략 방지 조건: free treatment 0, arbitrary Biological consumption 0, duplicate delivery request 0, second Sink 0, second tare output 0, receipt/destination orphan 0, unknown physical item restore 0
저장 권위와 실행 명령: CharacterMedical current-format v4가 supply intent/outcome/sequence를, Physical pending batch가 item debit을, immutable item catalog가 generic/resource physical ID를 소유함. startup/restore recovery가 이들을 exact join하며 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: runtime owner runtime:character-medical-treatment exact 8 items; total runtime consumer links 61, remaining packaging review runtime rows/links 28/31. live Resource medicine 7종과 exact extracted-blood를 정적 exact-set으로 교차함
검증 매트릭스와 보고서 위치: focused fixture는 1개/140g Sink·tare 1회·ack fault·current-format restore·ack-only replay와 exact extracted-blood request/generic item validation을 검사함. Assembly-CSharp·Assembly-CSharp-Editor Roslyn exit 0. Unity MCP의 `Dungeon Story/QA/V27/Character Medical Supply Outbox` focused fixture PASS 및 직후 Console Warning/Error 0/0을 확인함. 실제 환자·AI 배송·취소/사망 PlayMode는 아직 미실행
현재 밸런스 상태: 캐릭터 치료 재료 물리 제거·주문 상태 원자성과 Unity focused fixture PASS. 실제 환자·AI 배송 live 증거, 5개 unresolved packaged medicine의 unit kg/BOM/lifecycle, 추출 혈액 최종 단위, EWU·가격·6인 생존망은 미완료
```

## V27 연령 치료 단일 수술 권위·시간 고정 유지보수 원자성 기록 (2026-08-25)

```text
정의 ID: balance:v27:temporal-stasis-maintenance-two-input-sink-outbox-v1
콘텐츠 종류: 중복 direct 연령 치료 권위 제거, 수술 실행 권위 단일화, 시간 고정 계절 유지보수의 exact 2-input Sink와 life outcome outbox
정의·카탈로그·실행기 위치: AgeTreatmentCommandRuntime.cs, SurgeryLogistics, SurgicalProcedureEffectHandlers.cs, PhysicalAgeTreatmentRuntime.cs, CharacterLifeDomain.cs, PhysicalFacilityItemSinkGateway.cs, DungeonAggregateReferencePreflight.cs
등장 시대와 연구: 기존 연령 치료 수술 procedure·시설·연구 관계 유지; 신규 연구·해금·시대 이동 없음
플레이어에게 주는 새 결정: 신규 밸런스 선택 없음. 전신 재생·시간 고정 활성화는 기존 수술로만 실행되고, 활성 시간 고정은 계절마다 두 exact 촉매가 모두 있어야 유지됨
물리 BOM·입력·출력: 유지보수당 component:rune-conductor 1개와 resource:mana-crystal 1개를 하나의 physical Sink receipt로 소비함. 출력·부산물 없음; 두 입력 중 하나라도 없으면 둘 다 소비 0
직접 작업량과 계산 근거: 기존 수술 WU·시간과 유지보수 cadence 변경 0. count dictionary 삭제를 source stack IDs·total quantity·input grams 영수증으로 교체함
EWU와 목표 회수 기간: EWU·가격·계절 유지비 변경 0. 두 item의 최종 kg/BOM과 시간 고정 편익을 전수 recipe·의료 ROI와 함께 승인한 뒤 재생성함
시간·확률·재시도: deterministic sequence와 operation ID를 사용함. intent→two-input Sink→next-maintenance outcome→ack 순서이며 acknowledgement fault/restore의 second debit·second day extension 0
공간·전력·물·연료·정비: 기존 시간 고정 시설 destination과 power requirement 10을 유지함. 시설·전력 불가 시 효과를 operational false로 게시하고 재료를 소비하지 않음
위험·실패·회복 방식: missing input, partial selection, stale character/facility/day, contract/receipt mismatch, outcome publication 및 acknowledgement 실패를 fail-loud함. receipt 없는 intent는 sequence/day 변화 없이 제거함
사회·비가역 비용: 연령 상태·수술 효과 수치 변경 0. direct command와 수술의 이중 재료 소비/이중 효과, 룬 도체만 부분 소실, 저장 복원 중 두 번째 계절 연장을 차단함
기존 대안과의 장단점: 기존 범용 dictionary consume은 짧지만 두 물리 item의 exact lot·grams와 life outcome 사이 저장 원자성이 없었음. batch Sink outbox는 provenance가 늘지만 all-or-nothing 소비와 exact recovery를 제공함
지배 전략 방지 조건: 무료 유지 0, 한 재료 partial debit 0, second Sink 0, second maintenance extension 0, 중복 direct 치료 경로 0, pending receipt orphan 0
저장 권위와 실행 명령: CharacterLife current-format v3가 maintenance intent/outcome/sequence를, Physical pending batch가 두 world debit을, SurgeryOrder가 연령 치료 활성화 재료·작업·효과를 소유함. 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: IPhysicalAgeTreatmentService refs 0, PhysicalAgeTreatmentRuntime untyped consume 0, focused fixture가 2 inputs/1,200g, acknowledgement fault/recovery/replay와 missing-second-input no-partial-debit를 검사함
검증 매트릭스와 보고서 위치: Unity fresh TemporalStasisMaintenanceOutboxDebugScenarios와 SurgeryDebugScenarios PASS. Artifacts/QA/surgery-playmode-report.txt에서 실제 수술 물리 배송·Procedure 중간 whole-save·동일 state/work 복원·AI exact-once 재개·완료·빈 바이알 반환/재생산이 RESULT=PASS, Console Warning/Error 0/0, scoped git diff check 0
현재 밸런스 상태: 연령 치료 단일 수술 권위, 시간 고정 유지보수 focused 원자성과 실제 수술 whole-save PlayMode 완료. 시간 고정 시설 계절/전력 fault PlayMode, 두 촉매 최종 kg/BOM/WU/EWU/가격, 전수 untyped removal 0과 6인 생존망은 미완료
```

## V27 발전기 연료 물리 Sink·가동 시간 원자성 기록 (2026-08-25)

```text
정의 ID: balance:v27:power-generator-fuel-sink-outbox-v1
콘텐츠 종류: 연료 발전기의 exact FacilityBuffer Sink, FuelSeconds 결과 outbox, acknowledgement/restore 폐쇄
정의·카탈로그·실행기 위치: ElectricalNetworkRuntime.cs, IndustrialInfrastructureModels.cs, IndustrialInfrastructureAggregateStates.cs, IndustrialInfrastructureSaveValidation.cs, PhysicalFacilityItemSinkGateway.cs, DungeonAggregateReferencePreflight.cs
등장 시대와 연구: 기존 발전 시설·연료·연구 관계 유지; 신규 연구·해금·시대 이동 없음
플레이어에게 주는 새 결정: 신규 밸런스 선택 없음. 발전기 전용 buffer에 authored exact fuel item 한 개가 실제 도착해야 기존 secondsPerFuel만큼 가동함
물리 BOM·입력·출력: exact fuel item 1개를 combustion Sink함. 현재 출력·부산물 0이며 최종 연료 gram과 재·연기·폐열의 gameplay 물리화 여부는 전수 질량 감사에서 별도 승인함
직접 작업량과 계산 근거: 발전·운반 WU와 secondsPerFuel 변경 0. count deletion을 source stack IDs·quantity 1·input grams가 있는 pending receipt로 교체함
EWU와 목표 회수 기간: EWU·가격·발전 ROI 변경 0. 최종 fuel kg, 발전량, 가동 시간, 부산물/loss를 승인한 뒤 연료당 전력 EWU와 6인 기지 수요를 재생성함
시간·확률·재시도: node별 monotonic sequence 사용. intent→Sink→FuelSeconds→ack 순서이며 acknowledgement fault 동안에도 FuelSeconds가 simulation delta만큼 감소하고 second debit은 0
공간·전력·물·연료·정비: 기존 power:{nodeId} destination과 generator footprint/production을 유지함. 전용 buffer item 없이는 delivery를 요청하고 발전하지 않음
위험·실패·회복 방식: missing/mismatched fuel, receipt conflict, stale sequence, source provenance drift, acknowledgement 실패를 fail-loud함. receipt 없는 intent는 sequence/time 변경 없이 제거함
사회·비가역 비용: 전투·건강·사회 효과 변경 0. 연료 없이 발전, 연료 소실 뒤 시간 미게시, restore 중 두 번째 연료 소비, acknowledgement 지연 중 공짜 가동 시간을 차단함
기존 대안과의 장단점: 기존 count-only consume은 간단하지만 어떤 lot/grams가 사라졌고 시간이 게시됐는지 증명하지 못함. outbox는 node save provenance가 늘지만 exact recovery를 제공함
지배 전략 방지 조건: free fuel time 0, second Sink 0, same-sequence refill 0, acknowledgement-stall free tick 0, pending receipt orphan 0
저장 권위와 실행 명령: PowerInfrastructure current-format v3 node가 fuel intent/outcome/sequence를, Physical pending batch가 item debit을, Electrical runtime state가 감소 중 FuelSeconds를 소유함. 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: ElectricalNetworkRuntime untyped facility consume 0. Industrial JSON fixture가 OutcomePublished 1개/2,000g provenance를 왕복하고 whole-save preflight가 pending physical receipt를 exact join함. production runtime probe가 실제 world stack debit과 acknowledgement-only restore를 검사함
검증 매트릭스와 보고서 위치: Unity fresh import compile PASS. IndustrialInfrastructureDebugScenarios.RunFuelOutboxTransactionFocused()가 fuel debit 1/outcome 1/ack recovery 1/second debit 0과 FuelSeconds 120→110초를, 전체 RunAll()이 산업 인프라 회귀를 통과했으며 Console Warning/Error 0/0. scoped git diff check 오류 0
현재 밸런스 상태: 발전기 fuel debit·가동 시간 current-format 원자성과 Unity focused live 증거 완료. 전수 fuel kg/부산물/BOM/WU/EWU/가격, 6인 전력·물류 폐쇄 루프와 전수 untyped removal 0은 미완료
```

## V27 장비 모듈 감정 쿠폰 Sink·결과 원자성 기록 (2026-08-25)

```text
정의 ID: balance:v27:equipment-module-appraisal-coupon-sink-outbox-v1
콘텐츠 종류: 장비 모듈 감정의 exact material-test coupon Sink, 모듈 식별·두 내구 공구 wear 결과 outbox
정의·카탈로그·실행기 위치: EquipmentModuleRuntime.cs, EquipmentProgressionModels.cs, EquipmentItemStateCodec.cs, PhysicalItemBatchDispositionService.cs, PhysicalItemSaveValidation.cs, PhysicalItemRuntimeConsumerCatalog.cs
등장 시대와 연구: 기존 relic-appraisal 연구와 appraisal workstation 관계 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 밸런스 선택 없음. 감정 시설 buffer에 쿠폰·inspection gauge·rune-identification lens가 실제로 있어야 미확인 모듈을 식별함
물리 BOM·입력·출력: exact component:material-test-coupon 1개를 terminal test Sink로 소비하고 gauge durability 1, lens durability 2를 소모함. 현재 물리 부산물 0이며 검사 잔해를 채택하면 explicit Transform/output-loss로 재분류함
직접 작업량과 계산 근거: 기존 감정 작업·시설 요구와 공구 wear 값 변경 0. count deletion과 분리된 즉시 mutation을 source stack ID/quantity/input grams 및 세 결과 before/after envelope로 교체함
EWU와 목표 회수 기간: EWU·가격·감정 ROI 변경 0. 쿠폰 최종 kg/BOM/WU, 두 공구 수명, 모듈 가치 상승과 검사 잔해를 승인한 뒤 재생성함
시간·확률·재시도: monotonic module sequence를 사용함. intent→coupon Sink→module/gauge/lens outcome→ack 순서이며 acknowledgement fault/restore에서 second debit·identification·wear 0
공간·전력·물·연료·정비: 기존 appraisal FacilityBuffer와 workstation tag 사용. footprint·utility 변경 0; 공구는 소비되지 않고 동일 exact physical stack의 durability component만 변경됨
위험·실패·회복 방식: wrong facility/research, missing or reserved coupon/tool, stale module state, before/after drift, receipt mismatch, acknowledgement failure, pending module attachment를 fail-loud함. receipt 없는 intent는 결과 없이 제거함
사회·비가역 비용: 전투·사회·품질 수치 변경 0. 쿠폰 소실 뒤 모듈 미식별, 모듈 식별 뒤 공구 wear 누락, restore 중 중복 감정과 pending owner 유실을 차단함
기존 대안과의 장단점: 기존 count consume 뒤 세 개의 독립 mutation은 짧지만 저장 경계에서 부분 결과가 가능했음. module-owned outbox는 provenance가 늘지만 독립 physical module과 exact 도구 stack을 복구함
지배 전략 방지 조건: free appraisal 0, second coupon Sink 0, second gauge/lens wear 0, acknowledged operation replay 0, pending outbox attachment 0, orphan appraisal receipt 0
저장 권위와 실행 명령: 독립 module item-state current v2가 operation/result envelope를, Physical pending batch가 coupon debit을, exact tool component가 durability를 소유함. 장착 equipment payload는 pending appraisal을 거부하며 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: runtime consumer exact pairs는 coupon/runtime:equipment-module-testing, inspection gauge/runtime:equipment-module-inspection, rune lens/runtime:rune-module-identification. EquipmentModuleRuntime untyped consume 0, dead direct wear helper 0
검증 매트릭스와 보고서 위치: Unity fresh import 뒤 PhysicalItemDebugScenarios.RunAll()이 정상 commit, acknowledgement fault/current-format save-restore, owner/receipt 변조 거부와 terminal replay를 통과했고 V23CraftingDebugScenarios.RunRuntimeConsumerContractsFromMenu()가 typed consumer 연결을 통과함. Console Warning/Error 0/0, scoped diff check 0
현재 밸런스 상태: 감정 쿠폰·모듈/공구 결과 current-format 원자성과 Unity focused live 증거 완료. 쿠폰/공구 최종 kg·BOM·WU·EWU·가격·잔해 질량과 전수 untyped removal 0은 미완료
```

## V27 지역 공급 계약 물리 export Transfer·골드 결과 원자성 기록 (2026-08-25)

```text
정의 ID: balance:v27:regional-supply-contract-export-transfer-outbox-v1
콘텐츠 종류: 지역 공급 계약 집결품의 exact external-custody Transfer, 계약 골드 outcome, acknowledgement/restore outbox
정의·카탈로그·실행기 위치: RegionalSupplyContractRuntime.cs, RegionalSupplyContractDeliveryOutbox.cs, ResourceEconomyPlanningModels.cs, RegionalSupplyContractSaveValidation.cs, RegionalSupplyContractApplicationAdapter.cs, PhysicalFacilityItemSinkGateway.cs
등장 시대와 연구: 기존 research:commerce:integration 해금, 3일 계약 기간과 지역 후보 규칙 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 선택 없음. 계약 destination에 실제 요구 item lot이 모두 도착한 경우에만 외부 교역권으로 이전되고 보상을 받음
물리 BOM·입력·출력: 계약 requirements의 exact item/quantity를 stable stack order로 Transfer함. world item 출력 없음; 결과는 기존 rewardGold의 금고 입금이며 납품품은 외부 소유권으로 이동함
직접 작업량과 계산 근거: 기존 운반·계약 기한·요구량 변경 0. count deletion을 exact source stack IDs·total quantity·input grams가 있는 pending Transfer receipt로 교체함
EWU와 목표 회수 기간: 기존 가격·rewardGold·계약 ROI 변경 0. 최종 item kg와 45 effective WU 일정, 운반 횟수, 생존 비축 침해를 전수 재생성한 뒤 승인함
시간·확률·재시도: operation은 regional-supply-transfer:{contractId}로 결정론적임. Transfer→provenance→gold→Completed→ack 순서이며 acknowledgement fault/restore의 second transfer·second gold 0
공간·전력·물·연료·정비: 기존 delivery dropoff와 FacilityBuffer destination 사용. 시설 footprint·utility 변경 0; 납품 대기 질량은 실제 바닥/버퍼·운반·Floor Clutter 대상임
위험·실패·회복 방식: missing/partially reserved item, wrong destination, receipt/provenance mismatch, balance publication failure, stale schema, acknowledgement failure를 fail-loud함. physical commit 뒤 deadline failure/history trim보다 outbox recovery를 우선함
사회·비가역 비용: 평판·관계 수치 변경 0. 물품 소실 뒤 보상 누락, 보상 뒤 중복 지급, 기한 경과로 이미 이전한 물품 release, restore orphan receipt를 차단함
기존 대안과의 장단점: count-only consume은 짧지만 외부 이전 lot/grams와 보상 사이 저장 원자성이 없었음. Transfer outbox는 계약 provenance가 늘지만 exact custody와 acknowledgement-only recovery를 제공함
지배 전략 방지 조건: free contract income 0, second reward 0, second item Transfer 0, Sink 오분류 0, pending history trim 0, receipt owner orphan 0
저장 권위와 실행 명령: RegionalSupplyContract current-format v2가 outbox phase/provenance와 terminal status를, Physical pending batch가 world debit을, GameMoneyAccount가 reward balance를 소유함. application adapter는 receipt projection만 하며 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: RegionalSupplyContractApplicationAdapter untyped facility consume 0. RuntimeAuthorityV18Validator가 outbox recovery/save join과 count-delete 재도입 금지를 검사함
검증 매트릭스와 보고서 위치: Unity current assembly에서 `PhysicalItemDebugScenarios.RunAll()`, `RegionalSupplyContractTransferOutboxDebugScenarios.Verify()`와 `ProductionEconomyDebugScenarios.RunAll()`을 연속 실행함. 목재 exact 2-lot Transfer, ack fault, gold 1회, JSON restore, mass tamper rejection, ack-only recovery와 source 잔량이 PASS했고 직후 Console Warning/Error 0/0
현재 밸런스 상태: 지역 계약 납품·보상 current-source 원자성과 Unity focused fixture PASS. 실제 AI 집결·기한·whole-save PlayMode, 최종 item kg/운반량, 요구량·보상·45 WU 일정, EWU·가격·6인 생존망 재검증은 미완료
```

## V27 지역 공급 계약 incoming physical restore 조인 기록 (2026-08-25)

```text
정의 ID: balance:v27:regional-supply-incoming-physical-restore-join-v1
콘텐츠 종류: 지역 공급 계약 delivery outbox와 incoming PhysicalItems pending Transfer의 양방향 복원 교차 검증
정의·카탈로그·실행기 위치: PhysicalItemRestoreCandidateQuery.cs, WorldItemStackRuntime.cs, RegionalSupplyContractSaveSection.cs, RegionalSupplyContractTransferOutboxDebugScenarios.cs
등장 시대와 연구: 기존 지역 공급 계약 해금·기간·후보·연구 조건 변경 0
플레이어에게 주는 새 결정: 신규 선택 없음. 저장 복원 중 계약 소유권과 이미 반출된 물리 receipt가 불일치하면 부분 복원 없이 명시적으로 거부됨
물리 BOM·입력·출력: item/quantity/gram 변경 0. 기존 exact external Transfer receipt의 incoming candidate 존재성과 owner 대응만 검증함
직접 작업량과 계산 근거: 운반·생산·계약 작업 WU 변경 0. topological staging 중 detached physical candidate를 읽도록 교정함
EWU와 목표 회수 기간: EWU·가격·rewardGold·회수 기간 변경 0. 최종 kg 기반 계약 수치 재생성 gate 유지
시간·확률·재시도: RNG·deadline·offer cadence 변경 0. candidate query는 physical stage 생성부터 모든 cross-section participant publication 완료/rollback/discard까지만 존재하고 재시도마다 새 incoming candidate에서 재구축됨
공간·전력·물·연료·정비: 시설·버퍼·공간·utility 변경 0. receipt orphan을 허용해 이미 이동한 물품을 무소유 상태로 복원하는 경로만 차단함
위험·실패·회복 방식: missing receipt, kind/reason/operation/commit/source IDs/quantity/grams mismatch, orphan regional receipt, candidate unavailable를 live publication 전 fail-loud함
사회·비가역 비용: 평판·관계 변경 0. 물품만 사라지거나 보상 owner만 남는 비가역 교차 저장 손상을 차단함
기존 대안과의 장단점: live gateway 조회는 구현이 짧지만 복원 전 aggregate를 읽어 incoming payload를 증명하지 못함. detached query는 stage 수명 관리가 필요하지만 동일 save의 두 후보를 정확히 비교함
지배 전략 방지 조건: missing-owner item loss 0, owner-without-debit reward recovery 0, second Transfer/gold 0, live-state false join 0, candidate view leak 0
저장 권위와 실행 명령: PhysicalItems pending batch와 RegionalSupply v2가 각자의 기존 권위를 유지함. candidate query는 저장되지 않는 immutable 검증 투영이며 stage discard 또는 전체 transaction complete/rollback/discard에서 폐기됨. commit과 cross-section participant publication 사이에는 유지됨
자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 query/transaction participant DI, discardable stage, complete/rollback/discard clear, 양방향 owner/receipt join과 missing/orphan/mismatch fixture를 검사함. DTO-only preflight와 candidate-dependent stage 분리를 production bills·grand project·Circus·Surgery·RunMilestones까지 감사함
검증 매트릭스와 보고서 위치: Unity 지역 공급 focused fixture에서 valid/missing/orphan/mass mismatch와 실제 candidate stage→discard, stage→commit-visible→participant-complete-clear 수명이 PASS. 실제 Surgery whole-save가 모든 cross-section participant publication과 candidate 누수 0을 통과했고 Artifacts/QA/surgery-playmode-report.txt RESULT=PASS. 추가 registry fixture는 Physical Items와 Regional Supply에 동일 aggregate-root store를 주입해 pending owner/receipt valid 복원, source 잔량 1, missing receipt·owner mass mismatch·orphan receipt의 publication 전 원자 거부, 모든 경로의 candidate 누수 0을 검증했다. 같은 행렬을 `EditorApplication.isPlaying=true` PlayMode에서 재실행해 `PLAYMODE=1` PASS했고 전체 focused/full 회귀 직후 Console Warning/Error 0/0이다
현재 밸런스 상태: 밸런스 영향 없음(저장 원자성 교정) / detached candidate transaction 수명, 실제 whole-save publication, pending regional delivery PlayMode 복원·tamper 행렬 PASS. 지역 계약 authored kg·요구량·보상·운반 WU·EWU·가격·6인 생존망은 미완료
```

## V27 자원 재고 정책 일반 판매 물리 Transfer·골드 결과 원자성 기록 (2026-08-25)

```text
정의 ID: balance:v27:resource-stock-policy-market-transfer-outbox-v1
콘텐츠 종류: 일반 Resource 초과 재고의 exact external-market Transfer, 골드 outcome, acknowledgement/restore outbox
정의·카탈로그·실행기 위치: ResourceStockPolicyRuntime.cs, ResourceStockPolicySaleOutbox.cs, ResourceEconomyPlanningModels.cs, ResourceStockPolicySaveValidation.cs, ResourceStockPolicySaveSection.cs, PhysicalFacilityItemSinkGateway.cs
등장 시대와 연구: 기존 resource item 연구·시장 판매 가능 조건·stock policy 해금 관계 유지; 신규 연구·시대 이동 없음
플레이어에게 주는 새 결정: 신규 선택 없음. Sell 정책이 활성화된 item은 기존 maximumStock 초과분을 실제 시장 destination으로 운반한 뒤에만 골드로 정산함
물리 BOM·입력·출력: exact generic resource lot/quantity/grams를 외부 시장 소유권으로 Transfer함. world item 출력은 없고 결과는 기존 unitPrice×marketSaleRate Floor 골드임
직접 작업량과 계산 근거: 기존 운반·정책 평가 간격·threshold 변경 0. count deletion을 source stack IDs·quantity·input grams가 있는 pending Transfer와 단계별 골드 outbox로 교체함
EWU와 목표 회수 기간: authored unitPrice·saleRate·EWU·threshold 변경 0. 최종 item kg·haul batch·45 effective WU·6인 비축을 반영한 뒤 판매 ROI와 minimum sale batch를 재생성함
시간·확률·재시도: global monotonic sequence와 item ID로 operation을 결정함. Transfer→pending owner→income→IncomePublished→ack 순서이며 acknowledgement fault/restore의 second Transfer·income은 0
공간·전력·물·연료·정비: 기존 stock-policy:sell:{itemId} FacilityBuffer와 delivery dropoff 사용. footprint·utility 변경 0; 판매 대기 stock은 실제 저장·운반·Floor Clutter와 kg admission 대상임
위험·실패·회복 방식: reserved/missing lot, sequence exhaustion, receipt/provenance mismatch, balance overflow/publication failure, acknowledgement failure, incoming owner/receipt orphan을 fail-loud함. pending owner가 있으면 같은 item 새 판매를 시작하지 않음
사회·비가역 비용: 평판·관계·전투 수치 변경 0. 물품 소실 뒤 골드 누락, 골드 뒤 중복 지급, 정책 변경으로 pending owner 삭제, restore 중 물리 receipt 고아화를 차단함
기존 대안과의 장단점: 기존 count consume은 짧지만 어느 lot/grams가 반출됐고 보상이 게시됐는지 복구할 수 없었음. item별 outbox는 저장 provenance가 늘지만 설정 교체와 독립된 exact recovery를 제공함
지배 전략 방지 조건: free sale income 0, second income 0, second item Transfer 0, Sink 오분류 0, pending owner overwrite 0, receipt owner orphan 0
저장 권위와 실행 명령: ResourceStockPolicy current-format v2 aggregate가 pending sale/sequence를, Physical pending batch가 world debit을, GameMoneyAccount가 gold를 소유함. save candidate query는 저장되지 않는 staging 전용 검증 투영이며 과거 save migration은 제외함
자동 감사 ID와 전수 목록 포함 여부: ResourceStockPolicyRuntime generic sale untyped facility consume 0. RuntimeAuthorityV18Validator가 outbox recovery, schema validation, incoming two-way join과 count-delete 재도입 금지를 검사함
검증 매트릭스와 보고서 위치: Unity current assembly에서 `PhysicalItemDebugScenarios.RunAll()`, `ResourceStockPolicySaleOutboxDebugScenarios.Verify()`와 `ProductionEconomyDebugScenarios.RunAll()`을 연속 실행함. 목재 2-lot Transfer, ack fault, income 1회, JSON restore, missing/orphan/mass mismatch 거부, ack-only recovery와 잔량이 PASS했고 직후 Console Warning/Error 0/0
현재 밸런스 상태: 일반 자원 판매 current-source 원자성과 Unity focused fixture PASS. 실제 AI 운반·정산·whole-save PlayMode, unique 품질 미달 장비·의복 판매, 최종 item kg/운반량/stock threshold/sale ROI/WU/EWU/가격·6인 생존망 재검증은 미완료
```

## V27 callerless 작물 처리제 직접 소비 권위 제거 기록 (2026-08-25)

```text
정의 ID: balance:v27:crop-treatment-dead-mutation-removal-v1
콘텐츠 종류: production caller 0인 작물 처리제 direct facility-buffer 소비 API와 DI 등록 제거
정의·카탈로그·실행기 위치: ResourceItemDefinitionSO.cs, ResourceEconomyContentCatalog.cs, DungeonWorldSimulationRegistration.cs, RuntimeAuthorityV18Validator.cs
등장 시대와 연구: 기존 세 처리제 definition·recipe·연구 관계 변경 0; 실제 live 실행 해금은 아직 없음
플레이어에게 주는 새 결정: reachable gameplay 변경 없음. 기존 API는 UI·AI·작업 runner 호출자가 없어 플레이어가 실행할 수 없었음
물리 BOM·입력·출력: authored item/BOM/kg 변경 0. 도달 불가능했던 count deletion만 제거했으며 새 Sink·출력·손실을 만들지 않음
직접 작업량과 계산 근거: authored WU·효과량 변경 0. 실제 구현 시 plot 작업 WU와 delivery/cleanup을 별도 권위로 작성해야 함
EWU와 목표 회수 기간: EWU·가격·농업 ROI 변경 0. live 명령·물리 lifecycle이 생긴 뒤에만 재생성함
시간·확률·재시도: 기존 처리제 실행 경로가 없으므로 runtime cadence 변경 0. 향후 operation sequence와 ecology before/after envelope 필요
공간·전력·물·연료·정비: footprint·utility 변경 0. 향후 plot destination, package tare/폐기, storage/Floor Clutter 공간 필요
위험·실패·회복 방식: 죽은 API를 exact Sink처럼 포장해 live consumer로 오인하는 위험을 제거함. 향후 missing delivery, cancel, receipt mismatch, ecology conflict를 fail-loud해야 함
사회·비가역 비용: 현재 reachable 사회·농업 상태 변화 0. 향후 처리제 오염·잔류 위험과 생태 결과를 물리 소비와 원자 결합해야 함
기존 대안과의 장단점: dead API 유지·DI 등록은 구현된 것처럼 보이지만 실제 호출과 저장 증거가 없음. 제거는 기능 공백을 명시적으로 드러내고 거짓 완료를 막음
지배 전략 방지 조건: phantom live consumer 0, callerless item deletion 0, hidden fallback application 0. 실제 기능 전에는 처리제 효과를 자동 적용하지 않음
저장 권위와 실행 명령: 현재 처리제 live command/save owner 없음. authored metadata는 불변 정의만 유지하며 향후 crop aggregate+Physical pending receipt outbox가 필요함
자동 감사 ID와 전수 목록 포함 여부: source validator가 삭제 파일과 DI interface 재등장을 금지함. content usage row는 authored intent이며 live producer/consumer 실행 수에 포함하지 않음
검증 매트릭스와 보고서 위치: production caller 검색 0, deleted GUID 외부 참조 0, runtime/Editor Roslyn exit 0, scoped diff 0, repository untyped occurrence 32. Unity live 변화는 없지만 fresh compile/Console은 MCP approval 복구 대기
현재 밸런스 상태: 밸런스 영향 없음(죽은 mutation 제거). 실제 처리제 Planner/Runner/Sink+package/ecology outbox/UI·AI와 최종 kg·BOM·WU·EWU·가격·농업 폐쇄 루프는 미완료
```

### balance:v27:circus-show-supply-physical-outbox-v1

- 시대/역할: 서커스 공연 준비의 소품 소비와 재사용 연회 수레 마모 원자 경계.
- Before: 공연 소품 상자는 범용 count mutation으로 삭제되고 수레 내구도는 직후 별도 변경되어, 두 동작 사이 실패 및 active 주문 restore의 Composition 재개가 중복 소비·마모를 만들 수 있었다.
- After: exact prop stack Sink receipt와 cart durability before/after를 Circus V4 order outbox 하나에 보존한다. terminal commit은 restore 뒤에도 남고 acknowledgement replay는 domain 결과를 다시 게시하지 않는다.
- 물리 BOM/질량: 현재 authored 공연 소품 상자 1개를 exact Sink한다. 최종 package/잔해·단위 gram 승인은 후속 전수 질량 감사 전까지 열어 둔다.
- 작업량/시간/공간: 기존 공연 준비 WU·무대 FacilityBuffer·배송 경로를 유지한다. 수치 재조정은 하지 않았다.
- 위험/악용 방지: receipt missing/mismatch, cart third-value conflict, non-positive mass, sequence reuse를 fail-loud 처리한다. 저장 재개로 무료 공연 또는 이중 debit이 발생하지 않게 terminal commit을 분리했다.
- 실행 경로: `CircusRuntime.AdvancePreparation → TryCommitShowSupplies → CircusShowSupplyOutbox → IPhysicalItemBatchDispositionService`.
- 저장 권위: `CircusShowOrder` V4 pending phase/provenance와 permanent preparation supply commit.
- 자동 감사 증거: `DungeonStory.Captivity` 및 current-source `Assembly-CSharp` Roslyn compile PASS. Unity focused/PlayMode와 incoming whole-save cross-join은 미완료 / OPEN이다.
- 교차 저장 보강: restore projection/query와 disposition enum을 `DungeonStory.Items`로 이동하고 Infrastructure→Items 단방향 참조로 Circus pending owner↔incoming Sink receipt 양방향 조인을 추가했다. valid/missing/orphan/mass-mismatch fixture가 컴파일됐으며 실제 Unity 실행은 아직 미인증이다.

### balance:v27:accord-signal-physical-outbox-v1

- 역할/Before: 동맹 지원일의 첫 경비 공격이 신호 키트를 count 삭제한 뒤 milestone 결과를 별도 게시하여 중간 실패 시 물자만 사라질 수 있었다.
- After: 날짜별 exact Sink receipt와 source/mass/commit을 Run milestone 저장 권위에 보존하고 지원 날짜 게시 후 acknowledgement를 재시도한다.
- 실행/저장: `DefenseCombatExecutor → IRunMilestoneCommand → IPhysicalItemBatchDispositionService`; `RunMilestoneWorldSaveData` pending provenance와 `lastAccordSignalSupportAbsoluteDay`.
- 감사: incoming owner↔receipt 양방향 조인과 valid/missing/orphan/mass-mismatch fixture, Runtime·Editor Roslyn compile PASS. Unity ack-fault/PlayMode와 최종 kg·BOM·EWU는 미인증이다.

### balance:v27:facility-recalibration-catalyst-transfer-outbox-v1

- 시대/역할: 시설 인스턴스 진화가 해금된 뒤 활성화 규칙을 재보정할 때 촉매 1개를 시설 입력 버퍼에서 재보정 WIP custody로 이전하는 원자 경계다.
- Before: `FacilityInstanceEvolutionRuntime`이 destination의 촉매를 범용 count mutation으로 삭제하고 곧바로 `materialsConsumed/Ready`를 게시하여, 중간 실패·저장 복원에서 exact source·질량·결과 귀속을 증명할 수 없었다.
- After: stable source stack ID로 촉매 1개를 exact `Transfer`하고 operation/commit/source/input grams/outcome phase를 `FacilityRecalibrationOrder`에 보존한다. 결과를 먼저 게시하고 acknowledgement만 재시도하며 terminal 직접 재호출은 no-op이다.
- 물리 BOM/질량: 기존 authored catalyst item 1개와 potency를 유지한다. 이번 구조 변경은 kg·BOM·수량을 바꾸지 않으며 최종 촉매 단위 gram과 질량 변환은 전수 recipe 감사 전까지 미승인이다.
- 작업량/시간/공간: 기존 재보정 requiredWork, FacilityBuffer destination, 작업 접근과 시설 footprint를 유지한다. 운반 묶음·버퍼 gram capacity·ROI는 최종 kg 적용 뒤 재생성한다.
- 위험/악용 방지: missing/duplicate/orphan receipt, kind/reason/commit/source/quantity/mass mismatch, 결과 phase 불일치, terminal second debit을 fail-loud 또는 no-op 계약으로 차단한다.
- 실행 경로: `FacilityInstanceEvolutionRuntime.EnsureMaterialsReady(FacilityRecalibrationOrder) → FacilityRecalibrationMaterialOutbox → IPhysicalItemBatchDispositionService`.
- 저장 권위: `FacilityEvolutionStateComponent` current-format instance state의 `FacilityRecalibrationOrder`와 Physical Items pending batch가 각자의 유일 권위를 유지하며 restore guard가 incoming 두 후보를 publish 전에 양방향 조인한다.
- 자동 감사 증거: real repository acknowledgement-fault + JSON restore fixture가 second Transfer 0과 terminal replay 0을 작성했고 valid/missing/orphan/mass/source mismatch를 검사한다. Evolution·Runtime·Editor Roslyn compile 및 scoped diff check PASS다.
- 미완료 증거: Unity MCP 서버가 현재 도구 목록에 노출되지 않아 focused fixture와 실제 배송→재보정→whole-save PlayMode, Console 0/0은 실행하지 못했다. 촉매 kg·potency·횟수·WU/EWU·가격·시설 ROI도 미승인 상태다.

### balance:v27:facility-modification-material-batch-transfer-outbox-v1

- 시대/역할: 시설 숙련으로 세대 개조 후보가 열린 뒤 binding 재료와 선택적 고위험 촉매를 시설 개조 WIP custody로 이전하는 다중 재료 원자 경계다.
- Before: `FacilityInstanceEvolutionRuntime`이 item별 count 요구량을 한 번에 삭제했지만 exact source lot·source별 quantity·input grams·pending receipt·domain outcome을 저장하지 않아 실패/복원 귀속과 kg 보존을 증명할 수 없었다.
- After: order requirement를 item ID 순으로 해석하고 destination의 unreserved FacilityBuffer stack을 stable stack ID 순으로 선택해 모든 source를 하나의 exact batch `Transfer`로 커밋한다. 한 입력이라도 부족하면 어떤 source도 차감하지 않는다.
- 물리 BOM/질량: 현재 binding `resource:dark-resin` 1개와 고위험 후보의 exact catalyst 0/1개라는 authored BOM을 유지한다. 구조 fixture는 split-stack 원자성을 증명하기 위해 resin 2개를 사용하지만 gameplay 수치를 변경하지 않는다.
- 작업량/시간/공간: 기존 modification requiredWork, 시설 destination, 배송·접근·footprint를 유지한다. source별 gram 합계는 receipt로 저장하며 FacilityBuffer kg capacity와 실제 haul 횟수는 최종 mass ledger 뒤 재계산한다.
- 위험/악용 방지: partial debit, request fingerprint 재작성, source/quantity swap, missing/orphan/duplicate receipt, acknowledgement 후 second debit, outcome phase 역행을 fail-loud 또는 terminal no-op으로 차단한다.
- 실행 경로: `FacilityInstanceEvolutionRuntime.ApplyPendingWork → EnsureMaterialsReady(FacilityModificationOrder) → FacilityModificationMaterialOutbox → IPhysicalItemBatchDispositionService`.
- 저장 권위: Facility Evolution V6 `FacilityModificationOrder`가 operation/commit/fingerprint/source별 item·stack·quantity/input grams/outcome을, Physical Items pending batch가 실제 debit을 소유한다. restore guard는 두 incoming 후보 집합을 양방향 비교한다.
- 자동 감사 증거: dark resin 2-stack+catalyst 1-stack acknowledgement-fault/JSON replay와 missing-catalyst atomic failure fixture를 추가했다. RuntimeAuthority ratchet은 FacilityEvolution의 untyped FacilityBuffer consume 0과 outbox/restore fixture 존재를 요구한다. production runtime/restore guard의 physical service·candidate query는 `[Inject]` 필수 생성자로 고정했고 선택적 null injection은 제거했다. Evolution·Runtime·Editor Roslyn compile 및 scoped diff PASS다.
- 미완료 증거: Unity focused/live PlayMode, whole-save, Console 0/0과 최종 resin/catalyst kg·버퍼 용량·운반·WU/EWU·가격·세대별 ROI는 미인증이다.

### balance:v27:equipment-evolution-material-transfer-outbox-v1

- 시대/역할: 장비 숙련 세대가 열린 뒤 재단조 주재료·촉매·결합재·선택 안정제와 역사 노드 재귀속 촉매를 대장작업대 버퍼에서 주문 WIP custody로 이전하는 물리 원자 경계다.
- Before: 재단조·재귀속은 장비 본체가 작업대에 도착했는지만 확인한 뒤 item별 count 요구량을 삭제했다. exact source lot·source별 quantity·input grams·pending receipt와 domain outcome이 없어 저장/실패 경계에서 어느 재료가 어느 주문으로 갔는지 증명할 수 없었다.
- After: authored requirement 전체를 exact destination·unreserved FacilityBuffer·stable source ID 순서로 먼저 수집하고 하나의 `Transfer` batch로 커밋한다. 장비 본체 source stack은 material 후보에서 exact 제외하며 별도 physical custody로 유지한다.
- 물리 BOM/질량: 기존 재단조 BOM과 재귀속 촉매 1개를 유지한다. 이번 구조 변경은 item 수량·unit grams를 바꾸지 않고 실제 receipt의 input grams와 source vector만 WIP owner에 보존한다.
- 직접 작업량과 계산 근거: 기존 `GetReforgeWork`, `GetReattunementWork`와 세대·정밀 서비스 배율을 유지한다. 반복 WU·효과 가치·운반 횟수는 최종 kg 원장 뒤 재산정한다.
- EWU와 목표 회수 기간: 기존 장비 진화 효과·재료 scarcity·가격을 변경하지 않았다. 주재료/촉매/결합재/안정제 질량과 장비 수명·수리·전투 가치를 연결한 세대별 ROI 감사가 후속 gate다.
- 시간·확률·재시도: material selection에는 RNG가 없다. exact batch pending 뒤 owner와 Ready outcome을 게시하고 acknowledgement만 재시도하며, terminal replay는 두 번째 Transfer를 만들지 않는다.
- 공간·전력·물·연료·정비: 기존 대장작업대 destination·접근·전력·footprint를 유지한다. 모든 재료와 장비는 실제 FacilityBuffer gram capacity·운반·Floor Clutter 대상이며 최종 용량 승인은 미완료다.
- 위험·실패·회복 방식: 입력 하나 누락, 장비 source 혼입, receipt missing/orphan/duplicate, kind/reason/commit/fingerprint/source/quantity/grams mismatch, pending cancel을 fail-loud한다. acknowledgement fault/restore는 이미 이전된 재료와 장비 결과를 다시 적용하지 않는다.
- 사회·비가역 비용: 신규 사회·평판 비용 없음. 재료만 사라지거나 다른 장비 주문에 귀속되는 비가역 손상을 양방향 restore join으로 차단한다.
- 기존 대안과의 장단점: 기존 count deletion은 짧지만 WIP 소유권과 kg 보존을 증명하지 못했다. exact outbox는 저장 provenance가 늘지만 split-stack 원자성, 복구 가능성, 장비 본체 비소비를 제공한다.
- 지배 전략 방지 조건: partial material debit 0, 장비 본체 소비 0, second Transfer 0, cross-domain receipt swap 0, orphan WIP owner 0, 재료 이전 뒤 무료 cancel 0.
- 실행 경로: `EquipmentEvolutionRuntime.ApplyReforgeWork/ApplyReattunementWork → EquipmentEvolutionMaterialOutbox → IPhysicalItemBatchDispositionService`.
- 저장 권위와 실행 명령: Equipment Evolution V4의 두 order가 operation/commit/fingerprint/source별 inputs/input grams/outcome을, Physical Items pending batch가 실제 world debit을 소유한다. restore participant가 두 incoming 후보를 publish 전에 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 EquipmentEvolution의 untyped FacilityBuffer consume 0, outbox/restore guard/focused atomic fixture를 고정한다.
- 검증 매트릭스와 보고서 위치: real repository fixture가 재단조 3-input split batch, 재귀속 catalyst, acknowledgement fault·JSON replay, source 장비 보존, missing input atomic failure와 valid/missing/orphan/mass/source/fingerprint join을 작성했다. Evolution·Runtime·Editor Roslyn exit 0이며 Unity 실행은 MCP approval 복구 대기다.
- 현재 밸런스 상태: 장비 진화 material lifecycle current-source 구현 완료. Unity live evidence와 최종 kg·FacilityBuffer·운반·WU·EWU·가격·전투 ROI·6인 생존망 재검증은 미완료다.

### balance:v27:equipment-repair-material-durability-output-outbox-v1

- 시대/역할: 갑옷·방패 내구도가 정책 임계치 아래로 내려간 뒤 수리 재료를 정비 작업대 WIP로 이전하고 내구도를 회복해 장비를 다시 세계로 내보내는 원자 경계다.
- Before: exact 장비 배송과 수리 재료 배송을 확인한 뒤 재료 count를 먼저 삭제하고, 내구도 회복·destination 전체 release·장비 world-state 전환·claim revoke를 차례로 실행했다. 단계 사이 실패에서 재료 손실, 중복 수리, 출력 custody 불일치를 증명할 수 없었다.
- After: split material lot 전체를 exact Transfer pending으로 묶고 order에 source/grams/commit과 durability before/after를 보존한다. 내구도 결과 게시 후에만 acknowledgement하며 장비 output release를 별도 영속 단계로 재시도한다.
- 물리 BOM·입력·출력: 기존 material item과 `ceil(lostDurability/0.25) × RepairSupplyPerQuarterDurability` 수량을 유지한다. 장비 본체는 input에서 제외되고 exact source stack으로 유지된다. 재료의 장비 잔존/폐기 질량 계약은 후속 승인 대상이다.
- 직접 작업량과 계산 근거: 기존 `12 + lost × 28 WU`, 시설 work-speed multiplier와 정책 return durability를 유지한다. 최종 운반·수리 시간 재조정은 item gram 적용 뒤 수행한다.
- EWU와 목표 회수 기간: 수리재 가격·장비 교체비·내구 수명을 변경하지 않았다. 수리 1회 총 material EWU+labor WU가 동일 장비 교체와 지배 관계를 만들지 않는지 후속 ROI 감사가 필요하다.
- 시간·확률·재시도: RNG 없음. before→after 또는 after replay만 허용하며 acknowledgement/output release 재시도는 두 번째 재료 Transfer와 durability 증가를 만들지 않는다.
- 공간·전력·물·연료·정비: 기존 repair destination·정비 시설 footprint/utility를 유지한다. 수리 재료와 장비 본체가 동시에 차지하는 실제 gram capacity 및 대기열은 최종 질량 원장 뒤 재검증한다.
- 위험·실패·회복 방식: missing material, partial debit, 장비 source 혼입, durability third value, receipt missing/orphan/mismatch, acknowledged receipt 재등장, output release 불일치를 fail-loud한다. WIP 이전 뒤 취소로 owner를 잃는 경로를 금지한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 전투 준비 중 장비가 수리 WIP에 묶이는 기존 기회비용을 유지하며 재료만 사라지거나 장비가 복제되는 비가역 손상을 차단한다.
- 기존 대안과의 장단점: count 삭제와 즉시 release는 구현이 짧지만 kg·lot·결과 원자성을 증명할 수 없다. 단계형 outbox는 저장 필드가 늘지만 acknowledgement fault·내구도 replay·출력 해제를 독립 회복한다.
- 지배 전략 방지 조건: partial material debit 0, 장비 본체 소비 0, second Transfer 0, second durability gain 0, acknowledged receipt replay 0, output duplicate/loss 0, free cancel 0.
- 실행 경로: `RepairWorkExecutionHandler → EquipmentMaintenancePolicyRuntime.TryApplyRepairWork/CompleteOrder → EquipmentRepairMaterialOutbox → IPhysicalItemBatchDispositionService`.
- 저장 권위와 실행 명령: Equipment Maintenance V3 order가 WIP material·durability·ack/output phase를, Physical Items pending batch가 acknowledgement 전 world debit을, CombatEquipment instance가 실제 durability와 source stack을 소유한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 EquipmentMaintenance count consume 0, Transfer outbox, outcome helper, restore guard와 atomic missing-input fixture를 고정한다.
- 검증 매트릭스와 보고서 위치: real repository fixture가 split material·장비 source exclusion·ack fault/JSON replay와 restore join 변조를 작성했다. Runtime·Editor Roslyn exit 0이며 실제 durability/output Unity execution은 MCP approval 복구 대기다.
- 현재 밸런스 상태: 장비 수리 lifecycle current-source 구현 완료. 재료 mass transform, 갑옷/방패 수리량·WU·버퍼·운반·EWU·가격·교체 ROI와 Unity live evidence는 미완료다.

### balance:v27:combat-equipment-craft-material-output-outbox-v1

- 시대/역할: 대장 작업대에서 화살·볼트 묶음 또는 무기·방어구·방패를 제작할 때 다중 재료를 WIP로 이전하고 품질이 고정된 완성품을 FacilityOutputBuffer에 게시하는 원자 경계다.
- Before: destination에 모인 재료를 item count로 삭제하고 order를 제거한 뒤 건물 완료 핸들러가 완성품을 생성했다. 재료 debit과 generic/unique output 사이의 실패·저장 경계에 durable owner가 없어 재료 손실, 품질 재굴림 또는 중복 출력 가능성을 증명할 수 없었다.
- After: attempt별 exact source lot 전체를 pending Transfer로 묶고 order V7에 commit/fingerprint/source/grams를 저장한다. 품질·Mythic provenance·실제 output을 먼저 고정하고 deterministic output commit 또는 unique instance identity로 FacilityOutputBuffer를 idempotent하게 게시한 뒤 receipt를 acknowledge한다.
- 물리 BOM·질량: 기존 화살 `lumber 1 + feather 1 → arrow 20`, 볼트 `lumber 1 + iron ingot 1 → bolt 12`, 장비 primary material+component 수량은 유지한다. 이번 구조 변경은 unit gram과 손실을 승인하지 않으며 input grams는 실제 receipt로만 보존한다.
- 직접 작업량과 계산 근거: 탄약 4 WU와 기존 장비 `IBalanceWorkCalculator.CalculateEquipment` 결과를 유지한다. 반복 품질 attempt는 매번 별도 material Transfer와 기존 craft WU를 요구한다.
- EWU와 목표 회수 기간: 가격·품질 가치·해체 회수율을 바꾸지 않았다. 최종 item grams와 typed 해체 Transform이 확정된 뒤 제작→거절→해체→재제작 SCC를 tolerance 0으로 재감사한다.
- 시간·확률·재시도: quality roll과 Mythic fixed hash는 work 완료 시 한 번 order에 저장한다. output/ack failure 및 save/restore는 저장된 quality/output identity를 검증하며 RNG를 다시 소비하지 않는다.
- 공간·전력·물·연료·정비: 기존 작업대 좌표·facility identity·output destination을 유지한다. 물리 출력은 FacilityOutputBuffer에 생성되고 별도 운반 AI가 처리하며 최종 2~4 batch gram capacity는 전수 질량 원장 뒤 승인한다.
- 위험·실패·회복 방식: missing input은 atomic no-debit, receipt missing/orphan/mismatch는 restore fail-loud, output conflict는 재생성 없이 fail-loud한다. acknowledgement fault는 second Transfer/quality roll/output 없이 재개한다.
- 사회·비가역 비용: 신규 사회 비용 없음. maker/품질 event는 고정 결과와 함께 한 번만 게시하며 재료만 소실되는 비가역 실패를 차단한다.
- 기존 대안과의 장단점: count 삭제+후행 spawn은 짧지만 WIP·kg·복원 귀속이 없다. attempt outbox는 저장 provenance가 늘지만 split input 원자성, 품질 고정, generic commit/unique instance 기반 output replay를 제공한다.
- 지배 전략 방지 조건: partial debit 0, second Transfer 0, second quality roll 0, duplicate accepted/rejected output 0, cross-attempt receipt swap 0. 품질 거절 자동 해체도 rejected stack Transfer-to-WIP와 commit-tagged recovery Source로 닫았지만 최종 unit grams·loss가 미승인이다. 차익 PASS는 OPEN이다.
- 실행 경로: `EquipmentCraftingBuildingAbilityHandler → CombatEquipmentRuntime.ApplyCraftWork → CombatEquipmentCraftingRuntime → CombatEquipmentCraftMaterialOutbox/CombatEquipmentCraftOutputOutbox → physical item authority`.
- 저장 권위와 실행 명령: Combat Equipment V7 craft order가 attempt WIP·resolved outcome·output publication을, Physical Items pending batch가 acknowledgement 전 world debit을, equipment instance repository+physical stack이 unique output을 소유한다. restore participant가 incoming owner/receipt를 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 count consume 0, material/output outbox, terminal finalizer, restore guard, second-output fixture와 V7 schema를 고정한다.
- 검증 매트릭스와 보고서 위치: real repository fixture가 3-source atomic input, missing input rollback, tagged ammo output identity, ack fault/JSON replay, missing/orphan/mass/fingerprint restore join을 작성했다. Combat model·Runtime·Editor Roslyn exit 0이다. Unity 실행은 MCP connection approval 복구 대기다.
- 현재 밸런스 상태: 전투 장비 제작 input/output/quality-reject dismantle lifecycle current-source 공식 검증. Unity live evidence와 최종 kg·loss·FacilityBuffer·haul·WU/EWU·가격·품질 ROI·6인 생존망은 미완료다.

### balance:v27:defense-facility-physical-transaction-outbox-v1

- 시대/역할: 물리 보급을 사용하는 방어 시설의 exact 탄약·연료·코팅 충전과 기계적 잼 정비 원자 경계다.
- Before: 잼 해제 철괴와 보급품을 destination의 count로 먼저 삭제한 뒤 시설 상태를 별도로 바꿨다. exact source lot·input grams·pending receipt와 outcome phase가 없어 중간 실패/복원에서 재료 손실·중복 충전·혼합 탄약 capacity clamp 손실을 증명할 수 없었다.
- After: 정비 부품은 exact pending `Sink`, 방어 보급품은 시설 내부 supply custody로 exact pending `Transfer`한다. 상태 before/after와 source vector를 V2 save owner에 먼저 귀속하고 결과 게시 뒤 acknowledgement만 재시도한다.
- 물리 BOM·입력·출력: 잼 해제는 철괴 1개, 보급은 authored `supplyItemId × free capacity` 또는 남은 용량 8 이상일 때 혼합 탄약 상자 `1개→8 supply units`다. unit grams·상자 tare·발사/연소 잔해·초기 내장 보급의 건설 BOM 귀속은 후속 전수 질량 승인 대상이다.
- 직접 작업량과 계산 근거: 기존 정비 work route, activation cadence와 supplyPerActivation을 유지한다. 이번 체크포인트에서 WU·cooldown·damage를 변경하지 않았다.
- EWU와 목표 회수 기간: 기존 탄약·연료·시설 가격과 방어 효과를 변경하지 않았다. 발동 1회 물리 비용과 피해/지연 기대가, 시설 건설·정비·재장전 물류를 포함해 지배 전략을 만들지 않는지 최종 전투 ROI에서 재계산한다.
- 시간·확률·재시도: jam/misfire RNG 공식은 유지한다. physical operation은 facility persistent ID+monotonic sequence로 결정되며 pending debit→domain outcome→ack 순서다. acknowledgement fault/restore의 second debit·supply increment는 0이다.
- 공간·전력·물·연료·정비: 기존 defense/defense-maintenance FacilityBuffer destination과 접근 셀을 유지한다. 내부 magazine과 delivery buffer의 gram capacity, 25kg 운반 묶음, Floor Clutter는 최종 kg 적용 뒤 승인한다.
- 위험·실패·회복 방식: missing/partial source, category-only fallback, receipt missing/orphan/duplicate, kind/reason/commit/fingerprint/source/quantity/grams mismatch와 outcome third-state를 fail-loud한다. 혼합 상자는 free capacity가 8 미만이면 소비하지 않아 clamp 질량 손실을 막는다.
- 사회·비가역 비용: 신규 사회·평판 비용 없음. 전투 중 보급품만 사라지거나 같은 보급이 두 번 magazine에 반영되는 비가역 손상을 차단한다.
- 기존 대안과의 장단점: count mutation은 짧지만 어떤 lot과 질량이 시설에 들어갔는지 복구할 수 없었다. exact outbox는 저장 provenance가 늘지만 split-stack 원자성, acknowledgement-only 회복과 incoming receipt 귀속을 제공한다.
- 지배 전략 방지 조건: partial debit 0, second maintenance/supply debit 0, second supply increment 0, category substitute 0, mixed-box partial disappearance 0, orphan receipt/owner 0. initial supply free rebuild 가능성은 최종 construction-BOM 감사에서 반드시 닫는다.
- 실행 경로: `DefenseFacilityRuntime.TryClearJam/EnsureSupply → DefenseFacilityPhysicalTransactionOutbox → IPhysicalItemBatchDispositionService`.
- 저장 권위와 실행 명령: Defense Facility V2 aggregate가 pending transaction과 internal supply outcome을, Physical Items pending batch가 acknowledgement 전 world debit을 소유한다. restore participant가 incoming 두 후보 집합을 live publish 전에 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 Defense runtime item/category count consume 0, V2 schema, outbox, incoming reverse join과 acknowledgement-fault fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: real repository fixture가 split ammo 2-stack Transfer, missing input atomic failure, iron maintenance Sink, acknowledgement fault·JSON replay와 valid/missing/orphan/mass mismatch를 작성했다. Defense model·Runtime·Editor Roslyn exit 0, scoped diff error 0이다. Unity 실행은 MCP connection approval 복구 대기다.
- 현재 밸런스 상태: world debit→defense state 원자성 current-source 구현 완료. Unity live evidence와 최종 unit grams·package/잔해·internal magazine mass·initial supply Source·buffer/haul·WU/EWU·가격·방어 ROI·6인 생존망은 미완료다.

### balance:v27:crop-sow-certified-seed-physical-transaction-v1

- 시대/역할: 전 기술 단계의 밭·실내 재배지에서 물리 종자와 cycle supply를 파종 WIP로 이전하고, 육종 온실에서 종자+인증 키트를 인증 WIP로 이전해 component-bearing 종자를 배출하는 농업 원자 경계다.
- Before: `TryConsumeSowingInputs`가 destination count를 먼저 삭제한 뒤 비멱등 생태 Sow/compost 또는 loose certified seed spawn을 실행했다. 파종 생태 중복, input 손실, transient destination 기반 주문 소실과 인증 결과 중복/누락을 복원할 owner가 없었다.
- After: 모든 input을 exact pending Transfer로 WIP에 묶고 Crop Plot V5 또는 Certified Seed V1 owner에 lot/grams/seed state를 저장한다. 파종은 ecology before/after envelope, 인증은 deterministic component output commit을 게시한 뒤에만 input receipt를 acknowledge한다. 파종 시설 파괴는 exact WIP loss owner를 먼저 게시한 뒤 receipt와 ecology owner를 종료한다.
- 물리 BOM·입력·출력: 기존 crop별 seed lot 1개와 ability가 요구하는 clean water·compost·fuel·cycle supplies, 인증 seed lot 1개+`supply:certified-seed-kit` 1개를 유지한다. 인증 출력은 동일 cultivar/generation의 seed 1개이며 pathogen load만 30 감소한다. unit grams·포장/손실은 이번 구조 체크포인트에서 변경하지 않았다.
- 직접 작업량과 계산 근거: 기존 crop SowWork, 성장 시간, HarvestWork와 인증 일일 command cadence를 유지한다. input/output 원자성만 바꾸며 반복 WU와 6인 농업 노동 예산은 최종 질량 적용 뒤 재측정한다.
- EWU와 목표 회수 기간: seed/물/퇴비/연료/kit 가격과 작물 yield를 변경하지 않았다. 인증으로 줄어드는 질병 기대 손실이 kit+운반+시설 투자보다 과대/과소인지, crop gross 125%·net 110%와 함께 후속 ROI 감사가 필요하다.
- 시간·확률·재시도: 파종은 RNG가 없고 생태 before→after 또는 after replay만 허용한다. 인증 output도 저장된 source seed로 고정된다. acknowledgement/output 재시도는 두 번째 input Transfer, pathogen 감소, Sow/compost 또는 seed spawn을 만들지 않는다.
- 공간·전력·물·연료·정비: 기존 P23/P24/육종 온실 footprint와 utility를 유지한다. input/output buffer의 정확한 gram capacity·2~4 batch headroom·별도 haul·shared access·Floor Clutter는 최종 kg 뒤 검증한다.
- 위험·실패·회복 방식: missing input은 atomic no-debit, ecology third state와 receipt/output missing/orphan/mismatch는 fail-loud한다. 시설 파괴 뒤 이미 Transfer된 WIP는 `DestroyedWithPlotLoss` exact quantity/grams로 귀속하며 acknowledgement 실패 시 V5 owner와 마지막 실제 좌표를 보존한다. 원래 source로 순간이동시키지 않는다.
- 사회·비가역 비용: 신규 사회 비용 없음. 병원체·작물 질병·토양 생태가 물리 종자 debit과 분리되어 두 번 적용되는 비가역 손상을 차단한다.
- 기존 대안과의 장단점: destination count 삭제는 짧지만 lot/kg/WIP/복원 귀속이 없다. exact owner/outbox는 저장 provenance가 늘지만 split input 원자성, 생태 결과 replay와 component-bearing output identity를 제공한다.
- 지배 전략 방지 조건: partial debit 0, second input Transfer 0, second Sow/compost 0, duplicate certified seed 0, pathogen 재감소 0, receipt/owner swap 0, output component loss 0. 최종 kg·yield·price 차익은 아직 승인 전이다.
- 실행 경로: `CropPlotRuntime.EnsureSowingMaterials → CropPhysicalTransactionOutbox → CropEcologyRuntime` 및 `CertifiedSeedRuntime.CompleteDeliveredPlans → CropPhysicalTransactionOutbox/PhysicalSeedLotGateway.TryEnsureSeedLotOutput`.
- 저장 권위와 실행 명령: Crop Plot V5가 sow sequence/input owner/ecology envelope, destroyed-WIP loss와 last-known grid position을, Certified Seed V1이 persistent order/input owner/fixed output을, Physical Items pending batch와 commit-tagged stack이 실제 world debit/output을 소유한다. restore participant와 aggregate preflight가 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 legacy crop consume 0, V5/persistent certified owner, ecology envelope, destroyed loss/cleanup, deterministic output, reverse restore join과 fault fixture를 고정한다.
- 검증 매트릭스와 보고서 위치: real repository fixture가 split input, missing-input atomicity, acknowledgement fault, V5/V1 JSON provenance, destroyed-loss replay와 missing/orphan/mass mismatch를 작성했다. Economy·Runtime·Editor Roslyn exit 0이며 Unity 실행은 MCP approval 복구 대기다.
- 현재 밸런스 상태: 파종/인증 input→domain/output 및 시설 파괴 WIP terminal loss 원자성 current-source 구현 완료. Unity live, 최종 unit grams·buffer/haul·농업 생산/소비·WU/EWU·가격·6인 생존망은 미완료다.

### balance:v27:grand-project-material-sink-outcome-outbox-v1

- 시대/역할: 연구 해금 뒤 영주 집무실에 대량 납품된 건설 자재를 심부 채굴망·방어 구역·실내 농장망·연금 배관망·교역소·원정 보급 기지의 영구 기반시설로 편입하는 완료 원자 경계다.
- Before: 작업량이 끝난 프레임에 destination별 item count를 삭제하고 completed project ID를 별도로 추가했다. exact source lot·input grams·pending receipt가 없어 count debit과 benefit publication 사이의 저장/실패에서 자재 손실 또는 중복 효과를 증명할 수 없었다.
- After: 사업 전체 BOM을 exact FacilityBuffer lot의 하나의 pending Sink로 원자 커밋하고 V2 owner에 물리 provenance와 state before/after fingerprint를 저장한다. 완공 결과를 먼저 게시한 뒤 receipt를 acknowledge하며 실패 복원은 acknowledgement만 재시도한다.
- 물리 BOM·입력·출력: 기존 6개 사업의 authored BOM 수량은 변경하지 않았다. 물리 item 출력은 없고 자재는 `grand-project.infrastructure-embedded` Sink로 기반시설 상태에 귀속한다. 파괴 시 rubble/byproduct 회수 여부와 질량 손실은 별도 authored Transform 계약이 필요하다.
- 직접 작업량과 계산 근거: 기존 required work `520/760/680/820/900/1100 WU`를 유지한다. 이번 체크포인트는 물리 원자성만 바꾸며 45 effective WU와 성장 노동 35% 기준의 기간 재조정은 최종 질량·운반 적용 뒤 수행한다.
- EWU와 목표 회수 기간: BOM·노동·benefit 배율을 변경하지 않았다. 각 사업의 자재 EWU+건설 WU+운반비가 생산/방어/계약/원정 효과의 회수 기간과 지배 관계를 만들지 않는지 후속 ROI 감사가 필요하다.
- 시간·확률·재시도: RNG 없음. operation은 project ID로 결정되며 input commit, outcome publication, acknowledgement를 단계화한다. acknowledgement fault/save restore의 second Sink와 second benefit publication은 0이다.
- 공간·전력·물·연료·정비: 기존 집무실 destination과 사업 효과를 유지한다. 대량 BOM의 실제 gram FacilityBuffer capacity, 운반 대기열, Floor Clutter, 사업별 공간/utility 증설은 최종 물류·공간 폐쇄 루프에서 검증한다.
- 위험·실패·회복 방식: missing material은 atomic no-debit, owner/receipt missing·orphan·kind/reason/commit/fingerprint/source/quantity/grams mismatch와 state envelope third value는 fail-loud한다. pending owner가 있는 취소로 자재 소유권을 잃는 경로를 금지한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 장기간 건설 중 성장 노동과 대량 재고가 묶이는 기존 기회비용은 유지하며 자재만 삭제되거나 benefit만 복제되는 비가역 손상을 차단한다.
- 기존 대안과의 장단점: count 삭제는 구현이 짧지만 lot/kg/복원 귀속이 없다. exact Sink owner는 저장 provenance가 늘지만 다중 BOM 원자성, acknowledgement-only 회복과 incoming receipt 양방향 감사를 제공한다.
- 지배 전략 방지 조건: partial BOM debit 0, second Sink 0, second benefit 0, free cancel 0, cross-project receipt swap 0, orphan receipt/owner 0. 파괴 회수 차익은 rubble/byproduct 계약 확정 전까지 open이다.
- 실행 경로: `GrandProjectRuntime.ApplyWork/ResumePendingPhysicalCommit → GrandProjectApplicationAdapter → IPhysicalFacilityItemBatchSinkGateway → IPhysicalItemBatchDispositionService`.
- 저장 권위와 실행 명령: Grand Project V2 state가 physical owner와 completed outcome을, Physical Items pending batch가 acknowledgement 전 world debit을 소유한다. save section이 incoming physical candidate를 publish 전에 exact 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 GrandProject count consume 0, exact Sink gateway, state owner, reverse join과 acknowledgement-fault fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: focused fixture가 acknowledgement fault, V2 capture, valid/missing/orphan candidate와 acknowledgement-only restore replay를 작성했다. default empty receipt의 null-safe 판정을 교정한 뒤 `ProductionEconomyDebugScenarios.RunAll()`이 Unity에서 PASS했고 Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: 대형 사업 material debit→completed outcome 원자성 current source와 focused Unity 검증 완료. 실제 영주 집무실 PlayMode, 파괴 회수 Transform, 최종 kg·buffer/haul·WU/EWU·가격·ROI·공간/6인 성장 노동은 미완료다.

### balance:v27:production-stock-sensor-install-physical-outbox-v1

- 시대/역할: 생산 작업대에 물리 재고 센서 패널을 설치해 목표 재고·최소 비축 기반 주문 모드를 해금하는 설비 부품 장착 원자 경계다.
- Before: destination에 패널 1개가 도착하면 production gateway의 count 소비를 먼저 실행하고 facility ID를 installed set에 추가했다. exact source lot·input grams·pending receipt가 없어 중간 저장/실패에서 패널 손실 또는 설치 결과 중복을 증명할 수 없었다.
- After: 패널 1개를 exact FacilityBuffer pending Sink로 커밋하고 Production V14 owner에 물리 provenance를 저장한다. installed outcome을 먼저 게시한 뒤 receipt를 acknowledge하며 실패 시 acknowledgement만 재시도한다.
- 물리 BOM·입력·출력: 기존 facility별 `StockSensorInstallationItemId ×1`을 유지한다. 설치 시 센서는 시설에 embedded Sink로 귀속되고 제거 시 동일 item 1개를 반환한다. 제거 output은 Production V15 pending owner와 deterministic physical Source publication으로 소유한다.
- 직접 작업량과 계산 근거: 기존 즉시 설치/제거 command와 별도 건설 WU 없음 상태를 유지한다. 최종 센서 설치 노동 또는 전기/정비 비용은 item gram과 자동화 ROI를 함께 본 뒤 승인한다.
- EWU와 목표 회수 기간: 센서 가격·자동 재고 이점은 변경하지 않았다. 패널 BOM/EWU와 목표 재고가 줄이는 과잉생산·물류 WU의 회수 기간을 후속 경제 감사에서 계산한다.
- 시간·확률·재시도: RNG 없음. operation은 facility persistent ID로 결정되며 pending input→installed outcome→acknowledgement 순서다. acknowledgement fault의 second Sink/installed add는 0이다.
- 공간·전력·물·연료·정비: 기존 생산 시설 footprint와 utility를 유지한다. 패널 배송이 차지하는 FacilityBuffer gram, 다른 recipe input과의 경쟁, 제거 output 공간과 별도 haul은 최종 버퍼 감사 대상이다.
- 위험·실패·회복 방식: missing input은 no-debit, receipt missing/orphan/kind/reason/commit/fingerprint/source/quantity/grams mismatch와 phase↔installed 불일치는 fail-loud한다. pending 설치가 있는 제거와 사라진 facility owner의 조용한 삭제를 금지한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 자동화 부품만 사라지거나 비용 없이 installed flag만 얻는 비가역 상태를 차단한다.
- 기존 대안과의 장단점: count deletion은 짧지만 lot/kg/restore owner가 없다. exact Sink owner는 V14 provenance가 늘지만 설치 결과를 저장 가능하고 incoming receipt를 양방향 감사한다.
- 지배 전략 방지 조건: second Sink 0, duplicate installed add 0, free remove during pending 0, cross-facility receipt swap 0, orphan receipt/owner 0. 제거 spawn 실패·설치/제거 반복 차익은 output outbox 전환 전까지 open이다.
- 실행 경로: `ProductionStockSensorRuntime.RequestInstallation/FinalizeDeliveredSensors → ProductionAssemblyBridgeAdapter → ProductionStockSensorPhysicalGateway → IPhysicalFacilityItemSinkGateway`.
- 저장 권위와 실행 명령: Production V14 aggregate가 pending installation과 installed/acknowledged set을, Physical Items pending batch가 acknowledgement 전 world debit을 소유한다. Production save section이 incoming candidate를 publish 전에 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 gateway count debit 0, stock-sensor pending Sink/V14 owner/reverse join/composition/fault fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: Production economy focused fixture가 acknowledgement fault, installed outcome, pending owner, valid/missing/orphan receipt와 second debit 0을 작성했다. `ProductionEconomyDebugScenarios.RunAll()`이 Unity에서 PASS했고 Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: 센서 설치 input→installed outcome과 embedded mass→제거 physical Source output 원자성 current source와 focused Unity 검증 완료. 실제 센서 delivery/install/remove PlayMode와 최종 gram·buffer/haul·WU/EWU·가격·자동화 ROI는 미완료다.

### balance:v27:fluid-manual-water-container-feed-physical-outbox-v1

- 시대/역할: 초기 수동 급수와 산업 유체망의 물병 보충이 world clean-water lot을 유체 reserve/network custody로 이전하는 공통 물리 경계다.
- Before: 수동 급수는 exact item이 아닌 destination count debit 뒤 reserve를 증가시켰고 자동 급수는 Water category count debit 뒤 network water를 증가시켰다. source lot·input grams·저장 가능한 outcome owner가 없어 중간 실패에서 물 손실·중복 급수·잘못된 물 아이템 대체를 증명할 수 없었다.
- After: 두 경로 모두 exact `resource:clean-water` FacilityBuffer lot을 pending `Transfer`로 커밋한다. Fluid V6 node owner가 operation sequence, fingerprint, commit/source/quantity/input grams와 reserve/network outcome phase를 저장하며 outcome 게시 후 receipt를 acknowledge한다.
- 물리 BOM·입력·출력: 기존 깨끗한 물 1개 단위와 기존 water-unit 환산을 변경하지 않았다. world 물 아이템은 fluid custody로 이전되며 포장 tare의 빈 용기 반환 또는 폐기 Transform은 최종 authored mass lifecycle 전까지 open이다.
- 직접 작업량과 계산 근거: 기존 수동 운반/production process WU와 자동 feed cadence를 유지했다. 최종 1개 gram·운반 횟수·buffer 정리 지연이 확정된 뒤 45 effective WU와 6인 급수 반복 노동을 재계산한다.
- EWU와 목표 회수 기간: clean-water BOM, 정수/용수 인프라와 가격을 변경하지 않았다. input 물 EWU와 회수 가능한 용기 EWU, 수동 대비 자동망 투자 회수 기간은 전수 원장에서 후속 승인한다.
- 시간·확률·재시도: RNG 없음. node별 monotonic sequence와 deterministic operation ID를 사용하며 `Transfer → reserve/network outcome → acknowledgement`를 exact-once로 재개한다. acknowledgement fault의 second Transfer와 second water outcome은 0이다.
- 공간·전력·물·연료·정비: 기존 시설 footprint·전력·유체 capacity를 유지한다. clean-water FacilityBuffer 2~4회분 gram, 25kg 운반 묶음, 빈 용기 output 공간과 별도 haul은 후속 공간·물류 감사 대상이다.
- 위험·실패·회복 방식: missing exact clean-water는 no-debit와 exact delivery request로 끝난다. missing/orphan/kind/reason/operation/commit/fingerprint/source/quantity/grams mismatch 및 제3의 reserve/network outcome은 fail-loud하며 pending outcome을 먼저 복구한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 물 아이템만 사라지거나 물 없이 reserve/network가 증가하는 비가역 상태와 category 대체로 오염수가 투입되는 경로를 차단한다.
- 기존 대안과의 장단점: count/category debit은 구현이 짧지만 질량·lot·복원 provenance가 없다. exact Transfer owner는 V6 상태가 늘지만 실제 물병 custody, idempotent 재시도와 whole-save 양방향 감사를 제공한다.
- 지배 전략 방지 조건: second Transfer 0, second reserve/network outcome 0, same-category substitution 0, cross-node receipt swap 0, orphan owner/receipt 0. 빈 용기 미반환으로 인한 질량 소실과 물병 반복 경제는 authored package lifecycle 전까지 open이다.
- 실행 경로: 수동 `FluidNetworkRuntime.TryConsumeManualContainer/StageManualContainerWater/ApplyStagedManualWaterTransfer`; 자동 `TransferContainerWater/TryFeedWaterNetwork/TryRecoverContainerFeed`; 물리 경계는 `IPhysicalItemBatchDispositionService`다.
- 저장 권위와 실행 명령: Fluid V6 aggregate가 수동/자동 pending outcome owner와 operation sequence를, Physical Items pending batch가 acknowledgement 전 world debit을 소유한다. Fluid save section은 Physical candidate와 owner를 publish 전에 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: `RuntimeAuthorityV18Validator`가 Fluid legacy count/category consume 0, V6, immediate/feed owner, restore reverse join과 acknowledgement-fault fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: Physical Item focused source fixture가 wrong-category decoy, exact 500g source, acknowledgement fault, reserve exact-once, V6 JSON, valid/missing/orphan candidate와 second debit/outcome 0을 작성했다. Unity fresh import에서 Physical Item과 Industrial Infrastructure focused suite가 PASS했고 Console Warning/Error는 `0/0`이다. M01–M13 의료 에셋은 연속 두 번 Surgery builder를 실행한 뒤 13/13 SHA-256 byte-identical이며 Service Room 계약도 PASS했다.
- 현재 밸런스 상태: 급수 input→fluid outcome 원자성 current source와 focused Unity 검증 완료. 실제 AI 물 운반 PlayMode, 빈 용기/포장 lifecycle과 최종 water gram·FacilityBuffer/haul·WU/EWU·가격·6인 7일 식수 폐쇄 루프는 미완료다.

### balance:v27:medical-industrial-service-overlay-authority-v1

- 시대/역할: M01–M13 의료 시설의 기본 의료 능력, M01 직접 의료 서비스, 수술 공정의 상수·폐수·배관 계약을 빌더 실행 순서와 무관하게 합성하는 authoring 경계다.
- Before: Surgery builder가 `ReplaceAbilities`로 전체 collection을 재작성해 Industrial builder의 `BuildingUtilityConnectionAbility`·`BuildingProcessFluidAbility`와 M01의 `BuildingServiceHubAbility`를 제거하고 `unlocked`를 다시 false로 만들었다. 그 결과 수술이 물 없이 진행되는 free-fluid 경로와 의료 Direct 서비스 소실이 빌더 실행 순서에 따라 재발했다.
- After: Surgery builder는 자신이 소유한 의료 타입만 교체하고 다른 도메인의 ability 인스턴스와 managed-reference identity를 보존한다. Industrial/Service Room은 각각 재사용 가능한 overlay를 제공하며 Surgery builder가 최종 합성 시 재적용한다. 기존 ability는 remove/add하지 않고 in-place 갱신한다.
- 물리 BOM·입력·출력: 시설 건설 BOM·수술 약품·결과 수량 변경 0. 수술 전용 공정은 기존 계약대로 cycle당 CleanWater `0.2`, MedicalEffluent Wastewater `0.2`를 요구한다.
- 직접 작업량과 계산 근거: 의료·수술·Plumbing WU 값 변경 0. Surgery builder가 기존 승인된 `BuildingWorkAmountAbility`를 보존하고 유체 채널이 있는 의료 시설에 Plumbing work type을 합성한다.
- EWU와 목표 회수 기간: 시설·수술 EWU·가격·ROI 변경 0. 최종 물 gram·처리비·운반 WU가 승인된 뒤 수술당 utility EWU를 전수 원장에서 재계산한다.
- 시간·확률·재시도: 수술 시간·성공률·RNG 변경 0. authoring 재실행은 동일 입력에서 동일 의미와 동일 asset bytes를 산출해야 하며 두 번째 실행 변화는 0이다.
- 공간·전력·물·연료·정비: M01–M13 footprint와 전력 설정 변경 0. CleanWater|Wastewater channel `6`, 기본 throughput `20`, normally-open true, manual-water fallback true를 유지한다. M01은 Direct 의료 service hub와 unlocked 상태를 동시에 유지한다.
- 위험·실패·회복 방식: 의료 ability·service hub·utility/process-fluid 중 하나라도 빌더 순서 때문에 사라지면 focused verifier가 fail-loud한다. 부분 유체 authoring, Plumbing 누락, 두 번째 builder byte churn도 실패다.
- 사회·비가역 비용: 건강·수술 효과·서비스 만족도·가격 변경 0. 물 없는 무료 수술과 응급 처치대 Direct 서비스 소실이라는 비의도적 상태만 제거한다.
- 기존 대안과의 장단점: 전체 ability replacement는 단순하지만 다른 도메인 overlay를 파괴한다. 타입별 소유권 보존과 in-place overlay는 코드가 조금 늘지만 cross-builder 합성·managed-reference 안정성·no-op 재생성을 보장한다.
- 지배 전략 방지 조건: 공정수 무소비 수술 0, 폐수 미발생 수술 0, M01 service hub 소실 0, 빌더 순서별 상태 차이 0, 두 번째 asset byte diff 0.
- 실행 경로: `SurgeryContentAssetBuilder.BuildFacilities/CreateFacilityAbilities`, `IndustrialInfrastructureAssetBuilder.ApplyProcessFluidConsumerOverlay`, `ServiceRoomContentAssetBuilder.ApplyDirectMedicalHubOverlay`.
- 저장 권위와 실행 명령: ScriptableObject ability collection이 authored authority이며 런타임 저장 schema는 변경하지 않는다. 변경된 SO만 Unity serialization을 거치고 GUID·FileID·prefab 참조는 변경하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: Industrial Infrastructure가 모든 Cook/Surgery의 utility/process-fluid/Plumbing 합성을 검사하고 Service Room이 M01 Direct hub·unlocked를 검사한다. M01–M13 13개 asset hash를 1차/2차 재생성 사이 exact 비교한다.
- 검증 매트릭스와 보고서 위치: Unity 6000.3.8f1 fresh compile, `IndustrialInfrastructureDebugScenarios.RunAll()` PASS, `ServiceRoomDebugScenarios.Run()` PASS, M01–M13 second-build SHA-256 byte-identical `13/13`, Console Warning/Error `0/0`.
- 현재 밸런스 상태: 교차 빌더 authoring 권위와 focused Unity 결정론 검증 PASS. 실제 수술 PlayMode의 물 운반·폐수 처리, 최종 물 gram·WU/EWU·가격과 6인 의료/식수 폐쇄 루프는 미완료다.

### balance:v27:physical-mass-authority-semantic-recipe-recapture-v1

- 시대/역할: 전 시대 canonical balance item·recipe의 unit semantic, serialized kg writer, recipe 질량 disposition, 남은 packaging 검토 대기열을 current Unity authority에서 다시 고정하는 AuditOnly 전수 경계다.
- Before: 코드에는 누적 semantic `363/414`와 recipe inventory 생성기가 있었지만 Unity artifact는 오래된 `51/414` 상태였고, 최근 예방접종·의료 보급 focused fixture 두 파일이 writer manifest에 없어 fresh capture가 unknown writer로 fail-loud했다.
- After: 두 fixture를 asset authoring 권위가 아닌 `editor-test-writer`로 명시 분류했다. authority inventory·explicit semantic·recipe mass·packaging review를 각각 내부 double-capture하고 전체 묶음을 다시 두 번 실행해 current artifact를 확정했다.
- 물리 BOM·입력·출력: item kg·recipe BOM·출력 수량 변경 0. canonical ledger `414`, recipe `355`, explicit semantic `363`, material profile `51`, reviewed transform `38`, packaging review remaining `51`을 수집했다.
- 직접 작업량과 계산 근거: WU 수치 변경 0. ordinary haul은 maxStack 안에서 `6–11kg`, individual equipment는 단품, heavy `11–20kg`, oversize `>20kg` 분류 계약만 재검증했다.
- EWU와 목표 회수 기간: EWU·구매가·판매가 변경 0. recipe audit는 source `23`, transform `328`, sink `4`, reviewed exact `38`, missing disposition `159`, mass-creation Critical `84`, provisional candidate `126`, missing-semantic recipe `47`을 후속 수정 대기열로 공개한다.
- 시간·확률·재시도: probabilistic recipe `3`개는 guaranteed/maximum/decimal expected branch를 분리해 기록했다. 실제 WIP outcome 저장·복원 exact branch 증명은 아직 open이다.
- 공간·전력·물·연료·정비: 공간·utility·buffer 수치 변경 0. recipe clean-water/wastewater는 current `500g/unit` 정수 환산으로만 포함하고 포장·FacilityBuffer·창고 용량은 후속 적용 전까지 기존 권위를 유지한다.
- 위험·실패·회복 방식: unknown writer, duplicate/out-of-ledger semantic, noncanonical ID, haul-class 실패, recipe role-shape mismatch, inspected asset mutation, capture 간 byte 차이는 fail-loud한다. 이번 run의 unknown writer·role mismatch·asset mutation은 모두 `0`이다.
- 사회·비가역 비용: 플레이 효과 변경 0. 전수 적용 전에 숨어 있던 질량 생성·포장 소실·잘못된 writer가 정상으로 접히지 않고 row 단위 검토 대상으로 남도록 한다.
- 기존 대안과의 장단점: 코드 예상치만 신뢰하면 stale artifact와 신규 writer를 놓친다. Unity current-authority recapture는 실행 비용이 있지만 실제 카탈로그·SO·source digest·artifact bytes를 한 번에 검증한다.
- 지배 전략 방지 조건: unknown writer `0`, duplicate/out-of-ledger semantic `0`, role-shape mismatch `0`, asset mutation `0`, second-run artifact diff `0`. mass-creation Critical `84`와 missing disposition `159`는 해소 전 승인 금지다.
- 실행 경로: `V27PhysicalMassAuthorityInventoryDebugScenarios.RunFromMenu`, `V27PhysicalMassExplicitSemanticDebugScenarios.RunFromMenu`, `V27PhysicalMassRecipeInventoryDebugScenarios.RunFromMenu`, `V27PhysicalMassPackagingReviewDebugScenarios.RunFromMenu`.
- 저장 권위와 실행 명령: AuditOnly artifact만 갱신하며 gameplay save schema와 ScriptableObject 값을 수정하지 않는다. item kg는 `ItemDefinitionSO.unitWeight`, recipe는 `ProductionRecipeSO`, writer 역할은 exact source-path manifest가 소유한다.
- 자동 감사 ID와 전수 목록 포함 여부: authority `414/1074/355/61`, semantic `363/51`, packaging runtime consumer rows/links `28/31`, execution orphan `0`, recipe `23/328/4`, reviewed/missing/critical `38/159/84`를 source digest와 함께 기록한다.
- 검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-physical-mass-authority-inventory.*`, `v27-physical-mass-weight-writer-manifest.txt`, `v27-physical-mass-explicit-*`, `v27-recipe-mass-balance.*`, `v27-physical-mass-packaging-review.*`; 12개 artifact second-run SHA-256 byte-identical, Console Warning/Error `0/0`.
- 현재 밸런스 상태: current authority inventory와 deterministic review queue 검증 PASS. remaining package `51`, recipe Critical `84`, missing disposition `159`, 전수 After kg 적용, EWU·가격·6인 생존망·최종 3-seed가 미완료이므로 물리 중량 또는 전체 밸런스 완료가 아니다.

### balance:v27:construction-work-order-material-transfer-wip-restitution-v1

- 시대/역할: 모든 시대의 시설 건설이 운반 완료된 물리 BOM을 건설 WIP custody로 이전하고, 완료 또는 취소에서 그 소유권을 정확히 끝내는 공통 경계다.
- Before: `EnsureMaterialsReady`가 FacilityBuffer 재료를 count-only로 삭제하고 delivered 숫자만 올렸다. acknowledgement·save owner가 없었고 취소는 이미 삭제된 재료를 되돌리지 못했으며, 배치 실패 전에 주문을 제거할 수 있었다.
- After: authored BOM 전체의 exact physical lots을 하나의 pending `Transfer`로 커밋하고 Work Order V6 owner가 source·fingerprint·commit·quantity·grams와 delivered outcome phase를 보유한다. 성공 완료는 실제 placement 뒤 owner를 끝내고 취소는 deterministic physical restitution을 게시한다.
- 물리 BOM·입력·출력: 기존 시설별 authored construction BOM과 수량은 변경하지 않았다. 입력은 exact FacilityBuffer lot이며 completed construction에서는 시설 embedded mass, cancellation에서는 동일 BOM·동일 input grams의 Loose Source output이 된다.
- 직접 작업량과 계산 근거: 기존 건설 required WU·작업자 기여·품질 계산을 유지했다. 25kg 운반 횟수, site buffer 정리 시간과 45 effective WU를 반영한 최종 공사 기간은 전수 시설 원장에서 후속 승인한다.
- EWU와 목표 회수 기간: 시설 BOM·설치 WU·가격·철거 회수율을 변경하지 않았다. 건설 Transfer는 새 가치 생성이 아니며 취소 restitution은 input 가치 이하 exact 반환이다. 철거→재건 SCC margin과 시설 ROI는 최종 경제 재생성 대상이다.
- 시간·확률·재시도: 재료 commit과 취소 반환에 RNG 없음. stable work-order ID와 source stack ordering을 사용하며 acknowledgement fault와 output-space failure는 같은 operation을 재개한다. 건설 품질 RNG는 기존 저장된 quality pipeline 권위를 유지한다.
- 공간·전력·물·연료·정비: 기존 construction site footprint와 destination을 유지한다. site FacilityBuffer gram capacity, 접근칸 공유, 통로 혼잡, 취소 반환 output 공간과 별도 haul은 후속 공간·물류 감사 대상이다.
- 위험·실패·회복 방식: missing BOM은 atomic no-debit다. receipt missing/orphan/kind/reason/operation/commit/fingerprint/source/quantity/grams mismatch, partial delivered outcome와 placement conflict는 fail-loud하고 주문·WIP를 보존한다. restitution 공간 부족은 `RestitutionPending`으로 재시도한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 공사 취소로 자재가 증발하거나 placement 실패로 주문·재료가 함께 사라지는 비가역 손실을 차단한다.
- 기존 대안과의 장단점: count deletion은 단순하지만 lot/kg/복원 provenance와 취소 보존성이 없다. exact Transfer owner는 Work Order V6 상태와 source 기록이 늘지만 다중 BOM 원자성·replay·whole-save join을 제공한다.
- 지배 전략 방지 조건: partial material debit 0, second Transfer 0, second restitution 0, synthetic debug delivery 0, refund=false material deletion 0, failed placement order loss 0, cross-order receipt swap 0, orphan owner/receipt 0.
- 실행 경로: `WorkOrderRuntime.EnsureMaterialsReady → WorkOrderMaterialOutbox.TryCommitOrResume`; 취소 `WorkOrderRuntime.CancelOrder/ContinuePendingMaterialRestitutions → WorkOrderMaterialOutbox.TryPublishRestitution`; 완료 `CompleteOrder → ConstructionSite.CompleteConstruction`.
- 저장 권위와 실행 명령: Work Order V6 aggregate가 material WIP owner·delivered outcome·restitution phase를, Physical Items pending batch가 acknowledgement 전 input debit을, physical Source publication commit tag가 cancellation output을 소유한다. WorkOrders save section이 incoming pending receipt를 publish 전에 양방향 조인한다.
- 자동 감사 ID와 전수 목록 포함 여부: `RuntimeAuthorityV18Validator`가 WorkAmount legacy consume/bulk removal 0, exact Transfer/restitution, V6, reverse join, acknowledgement-fault와 single-commit fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: Work Amount focused fixture가 split material, ack fault, V6 JSON, valid/missing/orphan join, acknowledgement-only replay와 exact 2,000g cancellation return을 작성했다. authored catalog와 strict unique-stack fixture composition을 교정한 뒤 `WorkAmountDebugScenarios.RunAll(true)`와 `PhysicalItemDebugScenarios.RunAll()`이 Unity에서 PASS했고 Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: 건설 input→WIP→완료/취소 원자성과 partial committed-output restore preflight current source와 focused Unity 검증 완료. 실제 AI delivery/construction/cancel PlayMode와 최종 facility gram·buffer/haul·WU/EWU·가격·철거·공간·6인 성장 루프는 미완료다.

### balance:v27:production-stock-sensor-removal-physical-source-v1

- 시대/역할: 산업 자동화 시설의 재고 감지반을 설치·제거·재사용할 때 embedded 물리 질량을 world item으로 보존하는 경계다.
- Before: 설치 Sink가 끝나면 facility ID만 남아 input grams가 사라졌고, 제거는 installed 상태를 먼저 지운 뒤 성공 여부를 확인하지 않는 loose spawn을 호출했다. 출력 공간 실패·저장 경계에서 센서 자본이 삭제될 수 있었다.
- After: Production V15 installed record가 input operation/commit/source stack/embedded grams를 보존한다. 제거는 pending owner를 먼저 게시하고 deterministic commit-tagged Loose Source가 exact embedded grams로 존재한 뒤에만 installed 상태를 제거한다.
- 물리 BOM·입력·출력: 기존 `StockSensorInstallationItemId ×1`을 유지한다. 설치 input 1개의 exact grams가 embedded mass가 되고 제거 output도 같은 item 1개·같은 grams다. 별도 손실·포장 부산물은 추가하지 않았다.
- 직접 작업량과 계산 근거: 현재 즉시 install/remove command와 기존 배송 노동을 유지했다. 최종 센서 gram·FacilityBuffer와 반복 설치 악용을 반영한 별도 설치/제거 WU는 전수 물류 원장에서 후속 승인한다.
- EWU와 목표 회수 기간: 패널 BOM/EWU·가격과 자동화 효과를 변경하지 않았다. 제거 회수 가치는 설치 acquisition value 이하이며 반복 설치→제거는 수량·질량 이득 0이어야 한다. 자동 재고가 줄이는 과잉 생산·물류 WU로 ROI를 계산한다.
- 시간·확률·재시도: RNG 없음. removal operation은 facility ID와 실제 installation source stack ID로 결정돼 한 제거 재시도에서는 동일하고 다음 설치 cycle에서는 달라진다. output-space fault는 같은 operation을 재개한다.
- 공간·전력·물·연료·정비: 기존 facility footprint와 전력은 유지한다. 설치 FacilityBuffer와 제거 Loose output/AI haul/warehouse gram capacity, 접근칸 clutter는 후속 공간·물류 감사 대상이다.
- 위험·실패·회복 방식: output publication 실패는 installed/acknowledged/embedded owner를 보존한다. Prepared owner의 선행 output, OutputPublished owner의 missing/tampered commit/item/quantity/grams/state/position/destination, installed/record 비대칭과 동시 install/remove는 fail-loud한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 시설에서 센서를 떼는 순간 자본이 사라지는 비가역 손실을 제거한다.
- 기존 대안과의 장단점: 직접 SpawnOutput은 짧지만 결과·질량·재시도 owner가 없다. V15 pending Source owner는 저장 상태와 preflight가 늘지만 exact mass, output-space recovery와 반복 cycle identity를 제공한다.
- 지배 전략 방지 조건: failed output state loss 0, second Source 0, input/output gram delta 0, past-cycle commit collision 0, install/remove simultaneous owner 0, missing installed mass 0, tampered incoming output 0.
- 실행 경로: 설치 `ProductionStockSensorRuntime.TryBeginInstallation/ResumePendingInstallation`; 제거 `Remove/ResumePendingRemoval → IProductionStockSensorRemovalOutputGateway → IPhysicalItemSourcePublicationService`.
- 저장 권위와 실행 명령: Production V15 aggregate가 installed embedded mass와 pending removal phase를, Physical Items stack의 deterministic output commit component가 published world output을 소유한다. Production save section은 incoming committed-output candidate를 live publication 전에 검사한다.
- 자동 감사 ID와 전수 목록 포함 여부: `RuntimeAuthorityV18Validator`가 Production V15, direct SpawnOutput 금지, expected grams 연결, Source gateway, candidate query/save preflight, DI와 removal fault fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: Production focused fixture가 ack fault, 1,000g installed record, output-space fault ownership, single output, terminal replay와 prepared/published/missing candidate를 작성했다. Work Amount fixture도 부분 restitution output을 같은 query로 검증한다. `ProductionEconomyDebugScenarios.RunAll()`, `PhysicalItemDebugScenarios.RunAll()`, `WorkAmountDebugScenarios.RunAll(true)`이 같은 fresh Unity 세션에서 PASS했고 Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: 센서 설치 embedded mass→제거 Source output 원자성 current source와 focused Unity 검증 완료. 실제 delivery/output-space/AI haul/reinstall PlayMode와 최종 panel grams·buffer/haul·WU/EWU·가격·수리·자동화 ROI·6인 성장 루프는 미완료다.

### balance:v27:crop-plot-destruction-wip-terminal-loss-v1

- 시대/역할: 모든 시대의 밭·실내 재배 시설이 파종 input을 WIP custody로 받은 직후 파괴될 때 종자·물·퇴비·연료의 질량과 생태 소유권을 끝내는 공통 예외 경계다.
- Before: 파괴된 plot은 pending sow owner를 `Building=null`로 보존했지만 input receipt를 acknowledge하지도, 물리 회수나 명시 손실로 종결하지도 못했다. 저장 뒤에는 destination을 `(0,0)`에서 해제할 위험과 crop ecology orphan도 남았다.
- After: Crop Plot V5가 마지막 실제 grid 위치와 `DestroyedWithPlotLoss` terminal owner를 먼저 저장한다. loss quantity/grams가 original Transfer와 exact 일치한 상태에서 receipt를 acknowledge한 뒤에만 ecology owner와 plot state를 제거한다.
- 물리 BOM·입력·출력: 기존 crop cycle의 seed lot 1개와 clean water·compost·fuel·cycle supply 수량을 유지한다. 이미 WIP로 들어간 물질은 파괴된 토양·시설에 소실되는 명시 loss이며 world item output이나 source warehouse 반환은 없다.
- 직접 작업량과 계산 근거: 기존 배송·파종 WU를 변경하지 않았다. 파괴 이전에 투입된 운반·작업은 환급하지 않으며 재건·재파종 비용은 최종 농업 실패율과 ROI 감사에서 계산한다.
- EWU와 목표 회수 기간: item EWU·시설 가격·작물 yield를 변경하지 않았다. 파괴 loss는 회수 가치 0이며 재료를 무상 반환해 철거→재건 차익을 만들지 않는다. 최종 기대 손실은 시설 생존 시간과 침입·화재 빈도로 보정한다.
- 시간·확률·재시도: RNG 없음. terminal operation은 original sow operation에서 결정론적으로 파생된다. acknowledgement fault/save restore는 같은 owner와 commit만 재개하며 second Transfer, second loss 또는 second Sow/compost를 만들지 않는다.
- 공간·전력·물·연료·정비: 마지막 실제 plot cell을 V5에 저장해 destination release와 잔여 delivery가 원점으로 순간이동하지 않게 한다. 별도 output 공간은 요구하지 않으며 Floor Clutter는 실제 파괴 PlayMode에서 검증한다.
- 위험·실패·회복 방식: missing/mismatched pending receipt, loss operation/reason/quantity/grams 변조, ecology-after가 있는 input-loss branch와 owner/receipt 한쪽 누락을 fail-loud한다. 이미 OutcomePublished인 branch는 after 상태를 acknowledge한 뒤 ecology를 제거한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 시설 파괴로 인한 투입 재료 손실은 명시적 비가역 비용이며 저장 가능한 terminal provenance로 남는다.
- 기존 대안과의 장단점: source 창고 restitution은 플레이어 친화적이지만 물·퇴비·파종 종자가 순간이동·재사용되는 악용을 만든다. explicit WIP loss는 파괴 비용이 생기지만 질량과 소유권을 물리적으로 일관되게 끝낸다.
- 지배 전략 방지 조건: source teleport 0, free material return 0, second debit/loss/Sow 0, acknowledgement 전 owner removal 0, destination `(0,0)` release 0, ecology orphan 0, pending receipt orphan 0.
- 실행 경로: `CropPlotRuntime.SynchronizePlots/Tick → TryFinalizeDestroyedPlot → CropPhysicalTransactionOutbox.TryAcknowledgeDestroyedPlotLoss → ICropEcologyService.AbandonPlot`.
- 저장 권위와 실행 명령: Crop Plot V5 owner가 terminal disposition/operation/reason/quantity/grams와 last-known position을, Physical Items pending Transfer가 acknowledgement 전 debit을, Crop Ecology aggregate가 plot 생태 record를 소유한다.
- 자동 감사 ID와 전수 목록 포함 여부: `RuntimeAuthorityV18Validator`가 Crop Plot V5, destroyed-loss API, ecology cleanup과 `VerifyDestroyedPlotLoss` fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: real repository fixture가 seed 1+water 2 commit, acknowledgement fault, loss owner JSON replay, incoming receipt join, tampered loss grams 거부와 terminal acknowledgement를 작성했다. Economy·Runtime·Editor Roslyn exit 0, scoped diff error 0이다.
- 현재 밸런스 상태: 파종 시설 파괴 WIP loss current-source 원자성 완료. Unity live와 최종 seed/water/compost/fuel grams·buffer/haul·농업 생산/소비·WU/EWU·가격·6인 폐쇄 루프는 미완료다.

### balance:v27:crop-treatment-live-consumer-outbox-v1

- 시대/역할: 농업 연구 이후 pest lure·botanical pesticide·fungicide를 밭과 실내 재배지에 물리적으로 배송하고 작업자가 적용해 해충·병압을 낮추는 선택적 유지관리 경계다.
- Before: 처리제 definition과 생산 recipe는 있었지만 gameplay caller가 없는 서비스가 FacilityBuffer count를 직접 삭제하고 ecology를 변경하는 죽은 API였다. UI/AI 작업·delivery owner·저장 가능한 물리 receipt가 없었고 SoilDiagnostics가 매일 무료 fungicide 효과를 적용했다.
- After: crop plot UI/AI가 authored treatment policy를 예약하고 exact `:treatment` destination으로 물리 item을 배송한다. 기존 `Treat` WU runner가 작업을 완료하면 durable outbox가 exact Sink와 tare를 게시하고, ecology before/after와 cooldown을 저장한 뒤 acknowledgement한다.
- 물리 BOM·입력·출력: provisional 입력은 선택 처리제 1개다. content mass는 현재 각 1,150g을 유지했다. package feature가 아직 없으므로 empty container/waste/residue output은 최종 lifecycle 승인 전까지 open이며, outbox는 공통 tare service를 반드시 호출한다.
- 직접 작업량과 계산 근거: provisional pest lure 3 WU, botanical pesticide 5 WU, fungicide 5 WU다. 기존 crop sow 3~5 WU·harvest 5~9 WU와 비교해 구조 검증용으로 배정했으며 처리 면적·6인 농업 반복 노동을 반영한 최종값은 OPEN이다.
- EWU와 목표 회수 기간: EWU·구매/판매 가격은 변경하지 않았다. 처리 1회의 crop loss 회피 EWU가 input acquisition+labor+haul+폐기 비용보다 커야 하지만 무처리 작물·윤작·온실 대안을 지배해서는 안 된다. 최종 SCC·ROI는 전수 농업 원장에서 재생성한다.
- 시간·확률·재시도: 처리 효과 자체 RNG는 없다. provisional cooldown은 pest lure 1일, botanical pesticide 2일, fungicide 1일이다. operation은 plot ID와 monotonic sequence로 결정되고 acknowledgement fault/save replay는 second Sink·tare·ecology outcome 0을 요구한다.
- 공간·전력·물·연료·정비: 기존 plot footprint와 전력/용수를 유지한다. exact treatment FacilityBuffer, 2~4회분 gram capacity, 빈 용기/폐기물 output 공간, 25kg 운반 묶음과 접근칸 혼잡은 후속 authored 질량·공간 감사 대상이다.
- 위험·실패·회복 방식: missing item은 no-debit와 exact delivery wait다. receipt missing/orphan/kind/reason/operation/commit/fingerprint/source/quantity/grams mismatch, ecology before conflict와 tare replay conflict를 fail-loud한다. plot 파괴 postcommit은 exact quantity/grams terminal loss로 끝낸다.
- 사회·비가역 비용: 신규 사회 비용은 아직 없다. free daily fungicide를 제거해 처리제 재료·노동·폐기물 없이 병압이 사라지는 숨은 혜택을 차단했다. 향후 독성·오염·기분 위험은 botanical/chemical 구분과 함께 승인한다.
- 기존 대안과의 장단점: 윤작·저항성 종자·온실·무처리는 자재가 적지만 압력 회복이 느리다. 처리제는 빠르지만 item, haul, WU, cooldown과 package disposal을 지불한다. primitive fallback이나 무료 연구 효과로 N+1을 위조하지 않는다.
- 지배 전략 방지 조건: 정상 시설에서 무료 treatment 0, second Sink/outcome 0, cross-plot receipt swap 0, orphan owner/receipt 0, package tare deletion 0, plot 파괴 source teleport 0. 최종 값은 처리제 연속 사용이 모든 생태 관리 대안을 지배하지 않아야 한다.
- 실행 경로: `CropPlotBuildingPanelPresenter.TryScheduleTreatment → CropPlotRuntime.TickTreatmentDelivery → SurvivalWorkExecutionHandler(Treat) → CropPlotRuntime.ApplyWork/TryFinalizeTreatment → CropTreatmentPhysicalOutbox`.
- 저장 권위와 실행 명령: Crop Plot V7 owner가 treatment intent/work/cooldown/physical receipt/tare/ecology/terminal loss를, Physical Items pending Sink가 acknowledgement 전 input debit을, Crop Ecology aggregate가 pressure outcome을 소유한다. `hasSeedLot`가 null nested object와 실제 sow provenance를 구분한다.
- 자동 감사 ID와 전수 목록 포함 여부: RuntimeAuthorityV18Validator가 V7, `hasSeedLot`, live treatment planner/outbox/tare, exact Sink receipt, restore reverse join과 focused treatment/destroyed-loss fixture를 요구한다.
- 검증 매트릭스와 보고서 위치: `CropPhysicalTransactionFixture`가 exact Sink, repeated tare, acknowledgement fault, JSON replay, missing/orphan/kind/fingerprint/mass mismatch와 destroyed loss를 Unity에서 반복 PASS했다. `docs/implementation-reports/crop-plot-runtime-latest.txt`는 PlayMode `valid=true`, outdoor harvest `0→6`, indoor `Growing`, output containment 보존을 기록하고 Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: 작물 처리제 live consumer와 물리/생태/복원 원자성은 current source와 focused Unity에서 완료했다. 최종 3개 item gram·package/residue lifecycle·처리 면적/효과/cooldown·BOM/WU/EWU/가격·6인 농업·저장/운반·오염·다중 seed는 미완료다.

### balance:v27:combat-craft-prepublication-output-owner-v1

- 시대/역할: 모든 시대의 화살·볼트 및 전투 장비 제작이 확정 결과를 물리 output buffer에 exact-once 게시하는 저장 경계다.
- Before: 제작 결과 확정 단계가 deterministic `outputOperationId`를 먼저 저장했지만 generic output outbox는 operation이 있으면 commit도 이미 있어야 한다고 판정했다. 합법적인 prepared owner가 conflict로 막혀 탄약 출력이 생성되지 않았다.
- After: prepared 상태는 empty 또는 exact operation-only를 허용한다. commit-without-operation은 거부하고, `outputPublished` 상태는 exact operation과 deterministic commit을 모두 요구한다. 기존 output stack 재호출은 같은 commit만 재사용한다.
- 물리 BOM·입력·출력: authored recipe input과 output 수량을 변경하지 않았다. focused 화살 bundle은 lumber 2+iron ingot 1을 WIP Transfer하고 화살 20개를 FacilityOutputBuffer에 게시한다.
- 직접 작업량과 계산 근거: 기존 required/craft WU를 변경하지 않았다. pre-publication owner 판정만 수정했다.
- EWU와 목표 회수 기간: EWU·가격·품질 확률을 변경하지 않았다. second output 0과 rejected dismantle recovery single output을 유지해 가치 복제를 막는다.
- 시간·확률·재시도: 품질 결과는 기존 fixed attempt 결과를 사용한다. acknowledgement fault/JSON restore는 동일 operation·commit·quality·output을 재사용한다.
- 공간·전력·물·연료·정비: 기존 작업대·output destination을 유지한다. 실제 output-space fault/Floor Clutter PlayMode는 열려 있다.
- 위험·실패·회복 방식: operation mismatch, commit mismatch, commit-without-operation, published-without-operation/commit과 tampered incoming material receipt는 fail-loud한다. prepared exact operation은 정상 출력으로 진행한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 재료는 소비됐지만 잘못된 owner conflict로 결과가 영구 대기하는 비가역 손실을 제거한다.
- 기존 대안과의 장단점: output operation을 결과 게시 시점까지 저장하지 않으면 restore에서 RNG/result identity를 잃는다. operation-first는 상태가 하나 늘지만 확정 결과와 물리 게시를 안전하게 분리한다.
- 지배 전략 방지 조건: second material Transfer 0, second quality roll 0, second output 0, commit-only owner 0, published incomplete owner 0, rejected dismantle second recovery 0.
- 실행 경로: `CombatEquipmentCraftingRuntime.ResolveAttempt → TryFinalizeResolvedAttempt → CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput → IEquipmentPhysicalItemGateway`.
- 저장 권위와 실행 명령: Combat Equipment craft order가 prepared operation과 published commit을, physical output stack의 `ProductionOutputCommit` component가 실제 게시를 소유한다.
- 자동 감사 ID와 전수 목록 포함 여부: Strict Progression Combat Save suite가 material/evolution/repair/craft fixture를 함께 실행하고 Runtime authority ratchet이 exact output/outbox 경로를 감시한다.
- 검증 매트릭스와 보고서 위치: `CombatEquipmentCraftTransactionFixture`가 missing input atomicity, rejected dismantle replay, split material Transfer, output replay, acknowledgement fault, JSON restore와 missing/orphan/mass/fingerprint 거부를 PASS했다. Strict suite와 P1 Defense suite Unity 실행 후 Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: generic craft output prepared→published 원자성 focused Unity 완료. 실제 작업대 delivery/output-space/save/Floor Clutter와 최종 장비·탄약 gram/WU/EWU/가격은 미완료다.

### balance:v27:granulated-powder-mass-conservation-v1

- 시대/역할: 표준 탄약 연구 이후 흑색화약과 종이를 균일한 장약으로 체질·과립화하는 중간재다. 신호 키트, 복도식 기폭 장치와 8종 이상의 탄약·방어 보급품이 소비한다.
- Before: `material:granulated-powder` 한 단위가 `2,150g`이어서 `black-powder×2 + paper×1 = 5,300g` 입력으로 `2,150g×6 = 12,900g`을 만들어 한 cycle마다 `7,600g` 질량을 생성했다.
- After: 한 단위를 실제 downstream 장약 1회분인 `850g`으로 고정한다. 6개 출력은 `5,100g`이며 입력과의 차이 `200g`은 회수하지 않는 종이 절삭분·체질 분진인 `MillingByproduct`로 명시한다.
- 물리 BOM·입력·출력: 입력 `material:black-powder×2`, `material:paper×1`; 출력 `material:granulated-powder×6`; byproduct/loss `200g`. 포장 tare와 별도 용기는 없고 생산 bin은 `BulkInfrastructureNotInUnit`다.
- 직접 작업량과 계산 근거: authored recurring batch `98 WU`를 유지한다. 이 교정은 작업 속도나 출력 수량을 바꾸지 않고 잘못된 단위 질량과 보존 계약만 바로잡는다.
- EWU와 목표 회수 기간: current audit acquisition `37,506mEWU`, recoverable `25,645mEWU`, authored price `13 gold`는 현재 recipe graph 결과다. 최종 kg-aware haul·가격 전수 재생성 전까지 가격 완료값으로 승인하지 않는다.
- 시간·확률·재시도: 출력 확률은 1이며 RNG가 없다. 확률 WIP 재굴림 대상은 아니지만 실제 FacilityBuffer output publication은 기존 exact-once 생산 계약을 사용해야 한다.
- 공간·전력·물·연료·정비: 별도 utility 입력은 없다. ordinary haul `8~12개 = 6.8~10.2kg`; max stack `50 = 42.5kg`이므로 planner가 actor 한도 안에서 split해야 한다. downstream buffer는 새 gram을 사용해 재계산한다.
- 위험·실패·회복 방식: builder가 generic mineral 기본값 `2.15kg`을 다시 쓰지 않도록 exact unit-weight override를 둔다. 입력 commit 뒤 취소·시설 파괴·출력 공간 부족은 WIP/FacilityBuffer 계약으로 보존하며 `200g` 외 미분류 손실·생성을 허용하지 않는다.
- 사회·비가역 비용: 신규 사회 비용 없음. 분진은 현재 별도 물리 waste item을 만들지 않는 명시 공정 손실이며 환경·건강 효과를 추가할 경우 별도 콘텐츠 승인과 물리 부산물 계약이 필요하다.
- 기존 대안과의 장단점: 출력 수량을 줄이면 downstream recipe의 authored 단위와 전투 소비량을 광범위하게 바꾸므로 기각했다. `850g` 단위는 기존 6개 batch를 유지하면서 입력 질량과 ordinary haul band를 동시에 만족한다.
- 지배 전략 방지 조건: transform residual은 정확히 `200g>0`, 출력이 입력을 넘지 않으며 반복 과립화·분해 경로에서 질량 차익이 없어야 한다. downstream EWU/SCC는 최종 kg-aware 재생성에서 다시 검증한다.
- 실행 경로: `ResearchOverhaulContentAssetBuilder.ResolveAuthoredUnitWeight → ResourceItemDefinitionSO.unitWeight → PhysicalItemMassQuery`; 생산은 `ProductionBillRuntime → FacilityBuffer output → 별도 AI haul`을 사용한다.
- 저장 권위와 실행 명령: `ResourceItemDefinitionSO.unitWeight=0.85kg`이 단위 질량 권위이고 recipe/WIP가 input/output quantity와 exact 결과를 소유한다. audit semantic·transform CSV는 파생 증거이며 저장 권위가 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: `material:granulated-powder` semantic, `recipe:material:granulated-powder` reviewed-exact transform, recipe inventory shape와 builder no-clobber 검증을 전수 414 item/355 recipe 원장에 포함한다.
- 검증 매트릭스와 보고서 위치: `v27-physical-mass-explicit-unit-semantics.csv`, `v27-physical-mass-transform-contracts.csv`, `v27-recipe-mass-balance.csv`, `v27-recipe-mass-balance-audit.txt`. 두 번 recapture의 8개 artifact SHA-256이 byte-identical이고 focused mass/stock/campaign 회귀와 Console Warning/Error `0/0`을 확인했다.
- 현재 밸런스 상태: 이 item과 transform의 단위 질량·명시 손실·builder 권위는 검증 완료다. 전수 mass-creation Critical은 아직 `83`, missing disposition `159`, missing semantic recipe `47`이며 downstream kg·창고·haul·EWU·가격·SCC·6인 실전 회귀 전에는 전체 질량/밸런스 완료가 아니다.

### balance:v27:dog-food-ration-mass-conservation-v1

- 시대/역할: 초기 축산·포로 동물 관리에서 부패한 동물성 재료 또는 신선육과 곡물을 혼합해 만드는 포장 없는 1회분 개 사료다. 현재 확인된 live consumer는 포로 동물 급식 exact Sink/outbox다.
- Before: `feed:dog-food` 1개가 `550g`이어서 `animal-rot 1×700g + grain 1×350g` 및 `meat 1×700g + grain 1×350g`의 `1,050g` 입력이 `dog-food 2×550g = 1,100g`이 되어 cycle마다 `50g`을 생성했다.
- After: 개 사료 1개를 `525g`으로 고정한다. 두 recipe 모두 `1,050g → 2×525g = 1,050g`이고 별도 부산물·손실·포장 tare는 없다.
- 물리 BOM·입력·출력: 부패 경로는 `animal-rot×1 + grain×1`, 신선 경로는 `meat×1 + grain×1`, 공통 출력은 `dog-food×2`; clean water·wastewater·연료·확률 출력은 없다.
- 직접 작업량과 계산 근거: 현재 authored recipe WU와 출력 수량은 변경하지 않았다. 이 교정은 반복 생산량이나 작업 속도를 바꾸지 않고 단위 질량의 `+50g` 생성만 제거한다.
- EWU와 목표 회수 기간: EWU·구매/판매 가격은 이 수직 슬라이스에서 변경하지 않았다. 최종 kg-aware 물류비 재생성에서 부패 재료 처리 가치, 신선육 기회비용, 포로 유지비와 사료 대안을 함께 비교한다.
- 시간·확률·재시도: 두 출력 확률은 1이며 RNG가 없다. 생산 WIP·FacilityBuffer output은 기존 exact-once publication 계약을 사용하고 급식은 exact physical Sink receipt를 사용한다.
- 공간·전력·물·연료·정비: 신규 시설 footprint와 utility를 추가하지 않는다. ordinary haul `12~20개 = 6.3~10.5kg`; max stack `75 = 39.375kg`이므로 planner는 actor 한도에 맞춰 분할해야 한다. 출력 버퍼는 최종 생산 cycle과 p95 haul clearance로 재산정한다.
- 위험·실패·회복 방식: builder의 기존값 보존 로직이 `550g`을 되살리지 못하도록 exact override와 recipe topology 검증을 둔다. 입력 commit 뒤 취소·시설 파괴·출력 공간 부족은 WIP/FacilityBuffer owner가 보존하며 일반 바닥 드롭이나 결과 재굴림을 허용하지 않는다.
- 사회·비가역 비용: 신규 사회 비용 없음. 포로 동물에게 급식된 질량은 명시 Sink이며, 포장 용기가 없으므로 tare 반환 의무도 없다.
- 기존 대안과의 장단점: output 수량을 1개로 줄이면 사료 소비·stack·생산 throughput 의미가 바뀌므로 기각했다. `525g`은 기존 수량·소비 계약을 유지하면서 두 원료 경로를 동일하게 보존한다.
- 지배 전략 방지 조건: transform residual `0g`, 무료 질량 생성 0, 급식 second Sink 0, 부패 재료 경로가 신선육 경로를 비용 없이 지배하지 않음, 사람 음식 공급망의 곡물·육류를 과도하게 잠식하지 않음.
- 실행 경로: `ResourceEconomyAssetBuilder.ResolveAuthoredUnitWeight/ValidateDogFoodTopology → ResourceItemDefinitionSO.unitWeight → PhysicalItemMassQuery`; 생산은 production WIP/FacilityBuffer, 소비는 `CapturedWildlifeFeedOutbox` exact Sink를 사용한다.
- 저장 권위와 실행 명령: `ResourceItemDefinitionSO.unitWeight=0.525kg`이 단위 질량 권위이고 recipe가 input/output quantity를, physical receipt와 captive feed owner가 소비 operation·commit·grams를 소유한다.
- 자동 감사 ID와 전수 목록 포함 여부: `feed:dog-food` semantic, `recipe:resource:dog-food`, `recipe:resource:fresh-dog-food` reviewed-exact transform과 builder topology guard를 전수 `414 item/355 recipe` 원장에 포함한다.
- 검증 매트릭스와 보고서 위치: `v27-physical-mass-explicit-semantic-slice.txt`, `v27-physical-mass-transform-contracts.csv`, `v27-recipe-mass-balance.csv`, `v27-recipe-mass-balance-audit.txt`; 6개 artifact 연속 두 번 SHA-256 byte-identical, `CaptivityCircusDebugScenarios.RunAll(false)` PASS, Unity Console Warning/Error `0/0`.
- 현재 밸런스 상태: 두 개 사료 recipe의 단위 질량·질량 보존·builder 권위·focused live consumer 회귀 완료. 전수 mass-creation Critical `81`, missing disposition `159`, missing semantic recipe `47`이며 kg 창고·AI haul·FacilityBuffer·EWU·가격·6인 폐쇄 루프 전에는 전체 질량/밸런스 완료가 아니다.

### balance:v27:inoculated-log-section-mass-conservation-v1

- 시대/역할: `research:forestry:fungal` 이후 균사 재배 선반 RF13의 동굴버섯 cycle에 물리 투입되는 접종 원목 구간이다. 하나의 treated-lumber bundle을 두 재배 구간으로 나누는 현재 recipe 출력 단위다.
- Before: `supply:inoculated-log` 1개가 `1,800g`이어서 `treated-lumber 1×1,150g + cave-mushroom 1×250g = 1,400g` 입력이 `2×1,800g = 3,600g`을 출력해 cycle마다 `2,200g`을 생성했다.
- After: 단위를 `700g` cultivation-log section으로 고정한다. recipe는 `1,400g → 2×700g = 1,400g`이며 포장 tare·부산물·손실은 없다.
- 물리 BOM·입력·출력: `material:treated-lumber×1 + resource:cave-mushroom×1 → supply:inoculated-log×2`; clean water·wastewater·연료·확률 출력은 0이다. RF13은 crop cycle마다 완성 구간 1개를 소비한다.
- 직접 작업량과 계산 근거: authored recurring craft `22 WU`와 RF13 crop WU를 변경하지 않았다. 현재 output 2개·실제 cycle input 1개를 유지하면서 input 질량을 균등 분할한 유일한 count-preserving exact 값이 `700g`이다.
- EWU와 목표 회수 기간: item price `12 gold`, recipe WU, crop yield와 EWU는 이 수직 슬라이스에서 변경하지 않았다. kg-aware 물류비와 균사 재배 ROI는 treated lumber·버섯 기회비용, RF13 면적·물·퇴비·성장시간과 함께 최종 재생성한다.
- 시간·확률·재시도: recipe 출력 확률은 1이며 RNG가 없다. production output과 crop sow input은 기존 exact-once owner/receipt를 사용하고 acknowledgement fault·save restore에서 두 번째 output/debit을 허용하지 않는다.
- 공간·전력·물·연료·정비: item 자체는 `9~15개 = 6.3~10.5kg`, max stack `50 = 35kg`이다. RF13의 compost·water와 시설 footprint는 유지한다. 실제 FacilityBuffer gram capacity와 AI haul 접근칸·혼잡은 후속 검증 대상이다.
- 위험·실패·회복 방식: Research builder가 기존 `1.8kg`을 보존해 되살리지 못하도록 exact override와 topology 검증을 둔다. crop input commit 뒤 시설 파괴는 기존 `DestroyedWithPlotLoss` owner가 exact input grams를 소실로 기록하며 source 창고 순간이동을 허용하지 않는다.
- 사회·비가역 비용: 신규 사회 비용 없음. 재배 cycle에 투입된 원목과 접종 물질은 작물 기반에 흡수되는 비가역 WIP이며 별도 회수 부산물이 아니다.
- 기존 대안과의 장단점: 일반 P24 실내 재배는 석탄·버섯 substrate를 요구하고, RF13은 연구·전용 시설·treated lumber와 접종 원목을 요구한다. 접종 원목이 목재·버섯을 무상 복제하거나 RF13이 모든 실내 재배를 지배하지 않아야 한다.
- 지배 전략 방지 조건: transform residual `0g`, cycle input 복제 0, second output/debit 0, destroyed-WIP source teleport 0, 700g 변경으로 구매→제작→판매 또는 crop→접종→crop 양의 EWU 순환 0.
- 실행 경로: `ResearchOverhaulContentAssetBuilder.ResolveAuthoredUnitWeight/ValidateInoculatedLogTopology → ResourceItemDefinitionSO.unitWeight → ProductionBillRuntime/FacilityOutputBuffer → CropPlotRuntime.EnsureSowingMaterials → CropPhysicalTransactionOutbox`.
- 저장 권위와 실행 명령: `ResourceItemDefinitionSO.unitWeight=0.7kg`이 단위 질량 권위이고 production recipe가 output count를, RF13 `BuildingCropPlotAbility.CycleSupplyInputs`가 cycle 요구량을, crop V7 owner와 Physical Items receipt가 exact input commit·grams를 소유한다.
- 자동 감사 ID와 전수 목록 포함 여부: `supply:inoculated-log` semantic, `recipe:supply:inoculated-log` reviewed-exact transform, RF13 consumer topology와 builder no-clobber를 전수 `414 item/355 recipe` 원장에 포함한다.
- 검증 매트릭스와 보고서 위치: `v27-physical-mass-explicit-semantic-slice.txt`, `v27-physical-mass-transform-contracts.csv`, `v27-recipe-mass-balance.csv`, `v27-recipe-mass-balance-audit.txt`, `v27-balance-builder-no-clobber.txt`, `docs/implementation-reports/crop-plot-runtime-latest.txt`; 6개 mass artifact 두 번 byte-identical, builder 5/5·7,219파일 changes 0, RF13 `700g×1` PlayMode 소비·Growing PASS, Console Warning/Error `0/0`.
- 현재 밸런스 상태: 접종 원목 단위 질량·recipe 보존·builder 권위·실제 RF13 소비 경로는 공식·PlayMode 검증 완료. 전수 mass-creation Critical `80`, missing disposition `159`, missing semantic recipe `47`; kg warehouse/AI haul·FacilityBuffer gram capacity·EWU/가격·6인 농업·다중 seed 전에는 전체 질량/농업/밸런스 완료가 아니다.

### balance:v27:l02-mass-authoritative-general-warehouse-v1

- 시대/역할: 초기부터 사용하는 1×1 일반 물자 상자더미다. L01 대형보관선반보다 적은 공간·자본으로 한 번의 평범한 물류 묶음을 보관하는 초기 General 창고 역할이다.
- Before: `BuildingStorageAbility.capacity=16`만 있고 `maxStoredMassGrams=0`이어서 수용 여부가 아이템 질량과 무관한 legacy count fallback으로 결정됐다. 20g 소품 16개와 8kg 원료 16개가 같은 공간을 차지했고, kg 재조정이 창고·면적·입고 경제에 반영되지 않았다.
- After: immutable authored capacity를 positive `12,500g`으로 둔다. legacy count `16`은 호환·진단 metadata로 남지만 gram 권위가 활성화된 production admission에는 사용하지 않는다. category `General`, restricted storage 정책은 유지한다.
- 물리 BOM·입력·출력: 시설 BOM `material:lumber×6`을 변경하지 않았다. 저장은 exact item/instance lot만 받고 물리 stack 수량·component에서 gram을 파생한다. 검증 lot은 `supply:inoculated-log 700g×17=11,900g`이며 추가 1개는 12,600g이 되어 거절된다.
- 직접 작업량과 계산 근거: 건설 `116 WU`, 수리 `12 WU`, 청소 `4 WU`, 운용 `10 WU`를 유지한다. 용량은 L01 `25,000g / 2셀 = 12,500g/cell`의 일반 저장 밀도를 그대로 투영했다.
- EWU와 목표 회수 기간: BOM·WU·가격·시설 회수 기간은 이 슬라이스에서 변경하지 않았다. L02의 kg-aware 물류비와 L01 대비 ROI는 나머지 저장시설·아이템 질량·운반 거리와 함께 최종 EWU/가격 재생성에서 감사한다.
- 시간·확률·재시도: 입고는 warehouse-local revision과 catalog revision을 검증한 exact admission token으로 reserve→physical publication→commit한다. commit receipt는 수량 `17`, 질량 `11,900g`을 소유하며 재시도는 같은 operation/commit만 허용한다.
- 공간·전력·물·연료·정비: footprint 1×1, payload 밀도 12.5kg/cell이다. 전력·물·연료 입력은 없다. 대표 묶음 11.9kg은 일반 actor `19.1kg 무감속 / 28.65kg 최대` 안에 들어간다.
- 위험·실패·회복 방식: remaining 600g에서 700g 추가 unit은 pickup 전에 typed capacity failure다. valid over-capacity restore는 stock을 보존하고 신규 ingress를 막으며, owner/좌표 불일치는 기존 transactional restore gate가 원자 거절한다. count-only `CanStore(int)` production caller 재도입은 정적 ratchet이 실패시킨다.
- 사회·비가역 비용: 신규 사회 비용 없음. 낮은 자본의 1셀 저장소를 제공해 6인 초기에 L01 중복 투자를 강제하지 않지만, 12.5kg을 넘는 대형 lot은 더 큰 창고·분할 운반을 요구한다.
- 기존 대안과의 장단점: L01은 2셀·25kg·all-category라 총량과 유연성이 높다. L02는 1셀·12.5kg·General 전용이라 저렴하고 조밀하지만 무기·식품·마력 특화 저장을 대체하지 않는다.
- 지배 전략 방지 조건: 동일 셀 밀도에서 L02가 L01보다 kg/공간·BOM·WU 모든 면에서 우월하지 않아야 한다. legacy count 16이 positive gram path를 clamp하거나, 12.5kg 초과 lot이 부분 publication 뒤 유실되는 경로는 0이어야 한다.
- 실행 경로: `ModularFacilityAssetBuilder → BuildingSO/BuildingStorageAbility → Facility.Initialization → WarehouseInventory → WorldItemWarehouseService/WorldItemHaulPlanningService → WarehouseMassAdmissionService → physical Stored stack`.
- 저장 권위와 실행 명령: `BuildingStorageAbility.maxStoredMassGrams=12,500`이 시설 definition 권위다. 저장 DTO는 capacity를 복제하지 않고 physical stack과 policy만 저장하며, restore 뒤 runtime inventory가 authored capacity와 stack gram을 다시 결합한다.
- 자동 감사 ID와 전수 목록 포함 여부: `V27_L02_MASS_CAPACITY_12500G`, `V27_L02_INOCULATED_LOG_COUNT_FALLBACK_BYPASSED`, `V27_L02_INOCULATED_LOG_ADMISSION_17X700G_EXACT`, `V27_L02_INOCULATED_LOG_OVERFILL_REJECTED`, `V27_L02_CURRENT_FORMAT_RESTORE_EXACT`를 static ratchet에 포함한다.
- 검증 매트릭스와 보고서 위치: `PhysicalStockQueryV18DebugScenarios`, `ModularFacilityDebugScenarios`, `Artifacts/QA/l02-mass-admission-playmode-report.txt`, `Artifacts/QA/v27-balance-builder-no-clobber.txt`. focused PlayMode `RESULT=PASS; failures=0`, Console Error/Warning `0/0`, builder 5/5·7,219파일 `changes=0`이다.
- 현재 밸런스 상태: L02 하나의 gram authority·production ingress·restore·pickup 전 rejection은 current source와 PlayMode에서 완료했다. 나머지 19개 저장시설, FacilityBuffer/FacilityOutputBuffer, 전체 kg·물류·EWU·가격·공간·6인 생존망과 broad 물류 18개 실패가 남아 있어 전체 창고·운반·밸런스 완료가 아니다.

### balance:v27:positive-gram-storage-census-v1

- 시대/역할: 초기 식품·일반 물류부터 의료 장기, 연구, 마력, 무기와 P1 방어 보급까지 현재 positive-count 창고 21개 전부의 물리 보관량 권위다.
- Before: L01 `25,000g`, L02 `12,500g`만 positive gram이었고 나머지 19개는 필드 누락 13개·명시적 0 여섯 개였다. 동일 count가 수십 g 소품과 수십 kg 단일품을 같은 공간으로 취급했다.
- After: 일반·식품·의복·표본 1셀은 `12,500g`, 무기 1셀과 P1 2셀은 `25,000g`, Mana는 M01 `13,500g`·L07 `15,000g`·M02 `27,000g`, L01은 기존 `25,000g`, M08은 `12,500g`으로 고정했다. exact 표 21개 모두 positive다.
- 물리 BOM·입력·출력: 시설 건설 BOM과 count metadata를 변경하지 않았다. 입고는 exact item/instance lot의 unit grams×quantity만 사용하며 category/count/all-category 정책은 기존 값을 보존했다.
- 직접 작업량과 계산 근거: 건설·청소·수리·운용 WU를 변경하지 않았다. 기본 공간 밀도는 L01의 `25kg/2셀=12.5kg/cell`; Weapon은 현재 18kg powered harness 단일 수용을 위해 25kg; Mana는 최대 단위 750g×기존 count를 사용했다.
- EWU와 목표 회수 기간: 이번 배치는 admission 단위를 count→gram으로 전환했으며 BOM·WU·판매가·회수 기간은 아직 재생성하지 않았다. item kg, 운반 횟수와 공간 수요를 확정한 뒤 kg-aware EWU·가격을 다시 계산한다.
- 시간·확률·재시도: 창고 용량은 immutable SO authority이며 저장 DTO에 복제하지 않는다. 예약은 warehouse-local revision과 exact grams를 검증하고 commit/rollback/restore에서 같은 lot와 질량을 유지한다.
- 공간·전력·물·연료·정비: 시설 footprint·전력·유체·연료·정비는 변경하지 않았다. 1셀 기본 payload 12.5kg, 2셀 L01/P1 25kg이다. P1의 25kg은 기존 all-category/category 정책과 별개다.
- 위험·실패·회복 방식: positive gram이면 legacy count admission을 우회한다. valid over-capacity restore는 stock 보존·신규 입고 차단이며 owner/category/좌표 불일치는 원자 거절한다. 22/28kg 사체를 12.5kg Food storage에 암묵 예외로 넣지 않는다.
- 사회·비가역 비용: 초기 6인이 L01 중복 건설을 강제받지 않도록 1셀 storage를 유지한다. 반대로 대형 화물을 억지로 작은 창고에 넣어 공간·운반 비용을 삭제하지 않는다.
- 기존 대안과의 장단점: L01과 P1 25kg은 더 유연하지만 셀·BOM·연구 비용이 크다. 12.5kg 특화 storage는 좁고 저렴하지만 대형 사체를 받지 않는다. Weapon storage는 powered harness 18kg을 수용한다.
- 지배 전략 방지 조건: count metadata가 gram path를 clamp하지 않고, 낮은 셀 밀도 storage가 더 큰 창고보다 kg/셀·BOM·WU 모두 우월하지 않아야 한다. all-category도 질량 상한을 우회하지 않는다.
- 실행 경로: 세 writer(`ModularFacilityAssetBuilder`, `SurgeryContentAssetBuilder`, `P1DefenseFacilityAssetBuilder`) → `BuildingStorageAbility.maxStoredMassGrams` → `Facility.Initialization` → `WarehouseInventory` → exact admission token → physical Stored stack.
- 저장 권위와 실행 명령: 각 SO의 positive `long maxStoredMassGrams`가 단일 작성 권위다. runtime stored/reserved/remaining grams는 physical lot과 admission ledger에서 재계산하며 저장하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `V27_STORAGE_MASS_AUTHORITY_21_OF_21`, `V27_STORAGE_POSITIVE_COUNT_CENSUS_EXACT`, `V27_MODULAR_STORAGE_WRITER_MATCH`, `V27_M08_STORAGE_12500G`, `V27_P1_STORAGE_25000G`, `V27_STORAGE_NO_CLOBBER_SECOND_RUN`을 전수 manifest에 포함한다.
- 검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-storage-mass-authority.txt`, `PhysicalStockQueryV18DebugScenarios`, `ModularFacilityDebugScenarios`, `SurgeryDebugScenarios`, `SpeciesFactionDefenseExpansionDebugScenarios`, `Artifacts/QA/v27-balance-builder-no-clobber.txt`. manifest SHA-256 `1D77C4EA3D8011561EDE0010EBB77E80432A87ED6E256CC5141CDA4A7461E214`, builder 5/5·7,219파일 changes 0, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: 21개 창고의 authored gram 권위와 runtime 투영은 완료했다. rune-deer 22kg, moss-boar/humanoid corpse 28kg의 dedicated transport·D03 local buffer, Q03 archive category, FacilityBuffer/OutputBuffer, broad 물류 회귀, EWU·가격·6인 생존망 전에는 전체 창고·운반·밸런스 완료가 아니다.

### balance:v27:production-input-buffer-bounded-mass-v1

- 시대/역할: 모든 생산 주문이 재료를 작업 시설로 미리 운반할 때 사용하는 per-bill 물리 입력 버퍼의 첫 gram admission 수직 슬라이스다.
- Before: `ProductionInputLogisticsService`는 품목별 count와 prefetch batch만 계산한 뒤 무제한 delivery를 만들었다. destination에 이미 묶인 world lot과 pickup-commit carried lot의 총 gram을 검사하지 않아 서로 다른 재료의 합산 질량이 시설 입력 공간을 초과할 수 있었다.
- After: recipe cycle의 exact item grams를 합산하고 persisted prefetch를 `2~3회분`으로 제한해 destination의 단일 최대 gram을 계산한다. 모든 production input request는 bounded gateway를 통하며 overflow는 source retarget·lease·carry·intent 전에 실패한다. source는 전체 loose/stored slice preflight 후 기존 physical record를 exact split/retarget하므로 spawner partial failure가 수량을 삭제할 수 없다.
- 물리 BOM·입력·출력: 대표 `recipe:supply:inoculated-log` 입력은 treated lumber `1×1,150g`과 cave mushroom `1×250g`, 합계 `1,400g/cycle`이다. prefetch 3회에서 exact capacity는 `4,200g`; 출력·recipe BOM·item gram은 변경하지 않았다.
- 직접 작업량과 계산 근거: WU와 작업 속도는 변경하지 않았다. 이 변경은 기존 prefetch window의 물리 공간 비용을 드러내며 반복 노동량을 임의로 증가시키지 않는다.
- EWU와 목표 회수 기간: EWU·가격·회수 기간을 변경하지 않았다. 입력 버퍼 공간과 haul 횟수는 최종 kg-aware 물류비 재생성에서 production ROI에 반영한다.
- 시간·확률·재시도: capacity batch count는 저장된 prefetch count에서 결정론적으로 재계산한다. RNG와 output result는 건드리지 않으며 요청 재시도는 현재 pending world+carried gram을 다시 읽는다.
- 공간·전력·물·연료·정비: 별도 창고 capacity나 facility count mirror를 만들지 않는다. 입력 공간은 이 recipe에서 4.2kg이며 전력·유체·연료·시설 footprint는 유지한다.
- 위험·실패·회복 방식: non-canonical destination, unknown/non-positive mass, arithmetic overflow, dynamic source mass mismatch와 capacity overflow는 fail-loud한다. current-format restore는 기존 physical lot/haul intent에서 동일 pending gram을 재생성한다. bill claim 종료 전에 active delivery를 전수 preflight하고 pickup-commit cargo를 actor 현재 cell에 물리 반환한다. drop/intent 정리가 실패하면 claim과 bill을 유지한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 조리·의료·연구 등 다른 FacilityBuffer owner는 아직 이 bounded 경로로 이관되지 않았으므로 안전하다고 간주하지 않는다.
- 기존 대안과의 장단점: count-only prefetch는 단순하지만 가벼운 재료와 무거운 재료를 같은 공간으로 취급한다. exact gram은 실제 물류를 반영하지만 instance-dependent source는 정의 질량과 일치하지 않으면 별도 exact-lot admission이 필요하다.
- 지배 전략 방지 조건: item별 요청을 분리해 같은 destination capacity를 중복 사용하는 경로 0, overflow partial retarget 0, pickup 전후 이중 계상 0, cancel/consume/restore gram leak 0을 요구한다.
- 실행 경로: `ProductionBillRuntime → ProductionInputLogisticsService.RequestMissingInputs → IProductionItemGateway.RequestDeliveryWithinMassCapacity → WorldItemWarehouseService`.
- 저장 권위와 실행 명령: recipe input과 item mass가 불변 정의 권위, `ProductionBillRecord.prefetchBatchCount`와 physical world lot/committed haul intent가 가변 권위다. capacity와 pending grams는 파생값이며 별도 저장하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: Production Economy source ratchet은 `ProductionInputLogisticsService` bounded caller 정확히 1개/unbounded caller 0개, `WorldItemWarehouseService` atomic retarget 1개/구형 partial helper 0개, production claim-revocation atomic release caller 4개를 요구한다. 전체 FacilityBuffer owner migrated/remaining/bypass manifest는 아직 미완료다.
- 검증 매트릭스와 보고서 위치: `ProductionEconomyDebugScenarios.ValidateProductionInputBufferMassAdmission`, `Artifacts/QA/production-input-buffer-mass-playmode-report.txt`. focused PlayMode는 4,200g admit, +250g 무변경 reject, restore 4,200g, 실제 AIHaul pickup 뒤 동일 4,200g 단일 계상, pickup 중 cancel의 actor-cell 물리 회수, intent/lease/destination 0, 즉시 save/restore orphan 0을 PASS했다. 이어 exact `1,400g/2 units`를 WIP Transfer receipt로 제거하고 pending 0g, pending receipt restore 뒤 acknowledge, 전체 fixture 수량 8/8을 증명했으며 fresh Unity Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: 대표 production input의 공식·exact production claim·실제 AIHaul pickup/cancel·WIP consume·save-restore 경계까지 검증했다. non-production owner manifest, FacilityOutputBuffer, final kg/EWU/가격/6인망은 미완료이므로 체크포인트와 전체 밸런스 완료가 아니다.

### balance:v27:facility-buffer-owner-classification-manifest-v1

- 시대/역할: 모든 시대의 `FacilityBuffer` 입력·직접 게시와 `FacilityOutputBuffer` 출력 owner를 공용 gram-capacity 이관 전에 누락 없이 분류하는 current-source 감사 권위다.
- Before: production 입력 한 경로만 `4,200g` bounded admission이 있었고, 다른 owner·직접 spawn·conveyor·출력 publication은 여러 도메인에 흩어져 있었다. 새 배송 호출이 추가되어도 전수 owner 목록에서 빠졌는지 강제하는 산출물과 CI gate가 없었다.
- After: input owner `39`, output owner `5`, direct bypass `5`, orphan API `1`을 결정론적 registry로 고정한다. production input과 `power:{nodeId}`가 `migrated`, 나머지 input `37`과 output `5`는 `remaining`, 직접 admission 우회는 `bypass`, caller 없는 building buffer port는 `orphan`으로 명시한다.
- 물리 BOM·입력·출력: 이번 체크포인트는 item·recipe·facility BOM, 수량, unit grams와 물리 stack을 변경하지 않는다. 각 행은 기존 delivery/publication 경로와 destination 규칙만 분류한다.
- 직접 작업량과 계산 근거: WU·처리시간·haul 속도 변경 0. current production source의 generic delivery dot-invocation `59개/39파일`을 exact source census로 대조한다.
- EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. remaining/bypass owner가 gram-capacity로 이관된 뒤 kg-aware 물류비를 재생성한다.
- 시간·확률·재시도: manifest capture는 RNG와 timestamp를 사용하지 않는다. source path·내용을 canonical LF로 digest하고 동일 입력을 두 번 capture해 byte identity를 확인한다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·현재 count buffer 변경 0. 이 manifest는 용량을 부여하지 않으며 `fullStoredDestinationCoverage=false`를 명시해 warehouse까지 끝냈다는 오인을 막는다.
- 위험·실패·회복 방식: 미분류 delivery callsite, 사라진 classification, 중복 stable row, exact-claim prefix 수 변화, production bounded caller 변화, orphan port 신규 caller와 bypass marker 소실을 fail-loud한다.
- 사회·비가역 비용: 플레이 수치 변화 없음. 후속 이관 순서를 드러내어 무제한 버퍼가 무료 공간·초기 자본 우회로 남는 것을 방지한다.
- 기존 대안과의 장단점: 문서 수동 목록은 단순하지만 source drift를 잡지 못한다. 명시 registry+동적 discovery ratchet은 semantic Roslyn 분석보다 제한적이지만 현재 Unity Editor 어셈블리 경계를 바꾸지 않고 새 호출 누락을 즉시 차단한다.
- 지배 전략 방지 조건: manifest 생성만으로 remaining/bypass를 안전하다고 승인하지 않는다. 최종 `RequireFullyMigrated`는 `remaining=0`, `bypass=0`, `orphan=0` 전에는 항상 실패한다.
- 실행 경로: `ProductionEconomyDebugScenarios.RunAll → V27FacilityBufferOwnerManifestDebugScenarios.RequireClassificationCoverage → current production source census → deterministic owner registry`다.
- 저장 권위와 실행 명령: runtime 저장 권위는 각 domain save와 Physical Items/HaulIntent에 그대로 있다. CSV/TXT와 source digest는 읽기 전용 감사 증거이며 gameplay state를 저장하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `V27_FACILITY_BUFFER_OWNER_39`, `V27_FACILITY_OUTPUT_OWNER_5`, `V27_FACILITY_BUFFER_BYPASS_5`, `V27_FACILITY_BUFFER_ORPHAN_1`, `V27_FACILITY_DELIVERY_CALLS_59_IN_39_FILES`, `V27_FACILITY_BUFFER_UNCLASSIFIED_ZERO`를 current-source gate로 유지한다.
- 검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-facility-buffer-owner-manifest.csv`, `.txt`, `ProductionEconomyDebugScenarios.RunAll`. power-fuel 이관 뒤 두 번째 실행의 CSV SHA-256 `C0FF287B1B97021137368976C993EE92476E6C9DDD37E4CACCA7B246E3624A73`, TXT SHA-256 `CC4FAAF56166BD02818392B59287A453B79806750C66032B18898E05B626FCCB`, byte·mtime 변경 `0`, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: 전수 owner 분류와 drift gate, production input 및 power-fuel 두 수직 슬라이스는 완료했다. 실제 gram-capacity 이관은 input `37`, output `5`, bypass `5`, orphan `1`이 남아 있으므로 FacilityBuffer·물류·전체 밸런스 완료가 아니다.

### balance:v27:power-fuel-common-buffer-admission-v1

- 시대/역할: 산업 전력망의 연료 발전기가 `power:{persistentNodeId}` 입력 버퍼로 물리 연료를 요청·운반·소비하는 공용 FacilityBuffer admission의 첫 비-production live 수직 슬라이스다.
- Before: 발전기는 item count 기반 generic delivery를 요청했고 destination 전체 gram, pickup-commit carried gram과 topology 교체 시 owner 종료를 하나의 admission/terminal 계약으로 묶지 않았다. raw low-level retarget으로 공용 용량 검사를 우회할 수도 있었다.
- After: fuel item exact unit gram의 `4회분`을 positive capacity로 게시한다. 현재 마나 수정은 `350g`이므로 대표 buffer 상한은 `1,400g`; exact-stack request가 공용 admission token을 먼저 예약하고 actual AIHaul·입고·연소를 거친다. authored unit gram·연료 소모량·발전량은 변경하지 않았다.
- 물리 BOM·입력·출력: 대표 입력 `resource:mana-crystal×1=350g`; 출력은 전력 `6.4/10` 네트워크 상태다. 연료는 `power-generator-fuel-combustion` typed Sink로 exact-once 소비하며 별도 물리 부산물은 현재 authored 정의에 없다.
- 직접 작업량과 계산 근거: 운반 WU·이동속도·연소 tick·발전량은 유지했다. capacity는 현재 발전기 연료 품목 질량×기존 batch capacity 4로 계산한다.
- EWU와 목표 회수 기간: EWU·가격·발전기 ROI를 변경하지 않았다. 이 슬라이스는 무료 무제한 입력 공간과 이중 점유를 제거하는 구조 공식 검증이며 전체 전력 경제는 item kg·운반 횟수 확정 뒤 재생성한다.
- 시간·확률·재시도: capacity schema revision `1`은 topology epoch와 독립이다. admission/retarget/terminal release와 연료 Sink receipt는 replay-safe하며 acknowledgement 재시도에서 두 번째 debit을 허용하지 않는다.
- 공간·전력·물·연료·정비: 별도 footprint를 만들지 않고 시설 입력 버퍼가 1.4kg을 수용한다. world destination lot과 carried intent는 같은 화물을 한 번만 센다. raw route와 profile 없는 managed destination은 fail-loud한다.
- 위험·실패·회복 방식: retarget 실패는 token과 split을 rollback한다. 미픽업 화물은 lease를 해제하고, carried 화물은 actor 현재 cell에 물리 회수하며, deposited 화물은 former owner cell에 release한다. drop/publication 실패 시 claim·intent·cargo를 유지한다.
- 사회·비가역 비용: 신규 사회·기분 비용 없음. 고장·철거 시 연료가 원격 창고로 순간이동하거나 사라지지 않고 실제 위치의 물리 화물로 남는다.
- 기존 대안과의 장단점: legacy generic delivery는 단순하지만 용량·carried 점유·철거 종료를 보장하지 않았다. 공용 exact admission은 강한 원자성과 복원을 제공하는 대신 owner별 positive profile·claim·terminal lifecycle을 명시해야 한다.
- 지배 전략 방지 조건: raw route 우회 0, capacity 초과 partial retarget 0, pickup 전후 이중 gram 0, save/restore 재소비 0, topology 교체 owner leak 0, 철거 시 연료 삭제·순간이동 0을 요구한다.
- 실행 경로: `ElectricalNetworkRuntime.PublishFuelBufferAuthorities → WorldItemWarehouseService.TryRequestStackDelivery → FacilityBufferMassAdmissionService → AIHaul/AbilityHaul → FacilityBufferPhysicalOccupancyQuery → ElectricalNetworkRuntime fuel Sink`.
- 저장 권위와 실행 명령: 연료 SO/item mass가 definition 권위, Physical Items와 Haul Delivery Intent가 lot/carry 권위, Electrical Network가 소비 결과 권위다. capacity/claim restore candidate는 전체 save transaction에서 precomputed swap으로 게시하며 occupancy를 별도 저장하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `power:{nodeId}` owner 행을 `migrated`로 기록하고 input owners `39`, migrated `2`, remaining `37`, output `5`, bypass `5`, orphan `1`, unclassified `0`을 ratchet한다. full-migration gate는 계속 OPEN이다.
- 검증 매트릭스와 보고서 위치: `FacilityBufferMassAdmissionDebugScenarios`, `PhysicalStockQueryV18DebugScenarios`, `IndustrialInfrastructureDebugScenarios`, `ProductionEconomyDebugScenarios`, `Artifacts/QA/industrial-power-fuel-buffer-playmode-report.txt`, owner manifest CSV/TXT. actual AIHaul carried `350g` full save/restore, exact consumption, power `6.4/10`, intent 0, terminal close 1, capacity revision stable, PlayMode/static PASS, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: power-fuel 한 owner의 공용 buffer 공식과 live 실행은 `밸런스 공식 검증` 단계다. 다른 input 37, output 5, bypass 5, orphan 1, kg-aware EWU·가격·6인 생존망·다중 seed 전에는 FacilityBuffer·전력·전체 밸런스 완료가 아니다.

### balance:v27:production-input-common-buffer-admission-v2

- 시대/역할: 모든 기술 시대의 생산 주문이 exact 재료를 작업 시설의 `production:{billId}` 입력 버퍼로 선행 운반하는 공용 gram admission 권위다.
- Before: v1은 production caller가 직접 계산한 최대 gram을 `RequestDeliveryWithinMassCapacity`에 전달했다. 같은 공식을 여러 호출자가 복제할 수 있었고 claim과 capacity profile의 생성·복원·종료가 하나의 원자 lifecycle로 강제되지 않았다.
- After: `ProductionInputDestinationClaimRuntime`이 활성 bill 전체에서 exact claim과 `FacilityBufferCapacityProfile`을 함께 계산해 원자 교체한다. 일반 exact delivery는 공용 `FacilityBufferMassAdmissionService`가 repository-derived lot mass와 physical/carried occupancy를 검증한 token 없이는 split·retarget할 수 없다.
- 물리 BOM·입력·출력: 대표 `recipe:supply:inoculated-log`는 treated lumber `1×1,150g`과 cave mushroom `1×250g`, 합계 `1,400g/cycle`이다. prefetch 3회 profile은 `4,200g`; item·recipe BOM·output grams는 변경하지 않았다.
- 직접 작업량과 계산 근거: WU·작업 속도·prefetch authored count는 변경하지 않았다. profile은 기존 `2~3회분` 정책과 exact item mass에서만 파생된다.
- EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 공용 admission으로 드러난 actual haul 횟수와 buffer pressure는 후속 kg-aware EWU 재생성 입력으로만 사용한다.
- 시간·확률·재시도: claim/profile schema revision은 양의 canonical 값 `1`이며 restore 때 동일 활성 bill 집합에서 재계산한다. output RNG와 생산 결과는 이 슬라이스에서 변경하지 않는다.
- 공간·전력·물·연료·정비: 대표 입력 버퍼 4.2kg 외 footprint·전력·유체·정비 수치 변경 0. destination의 서로 다른 품목과 carried commitment를 같은 gram 상한에서 한 번만 센다.
- 위험·실패·회복 방식: profile/claim 누락, non-positive capacity, stale revision, unknown mass, overflow, source revision drift와 downstream retarget 실패는 물리 mutation 전 또는 완전 rollback으로 fail-loud한다. 취소 시 carried cargo는 actor 현재 cell에 물리 반환하고 실패하면 owner를 유지한다.
- 사회·비가역 비용: 신규 사회 비용 없음. production buffer를 무제한 임시 창고로 쓰거나 취소로 화물을 원격 반환하는 이득을 제거한다.
- 기존 대안과의 장단점: caller-authored bounded request는 작은 변경이지만 수치 권위가 분산된다. 공용 profile/token은 owner lifecycle 구현이 필요하지만 모든 FacilityBuffer가 같은 admission·restore·terminal 계약을 재사용할 수 있다.
- 지배 전략 방지 조건: legacy bounded caller `0`, profile 없는 managed request `0`, overflow partial retarget `0`, pickup 전후 이중 gram `0`, cancel/consume/restore leak `0`을 요구한다.
- 실행 경로: `ProductionBillRuntime → ProductionInputDestinationClaimRuntime → FacilityBufferDestinationLifecycleService → ProductionInputLogisticsService → WorldItemWarehouseService → FacilityBufferMassAdmissionService → AIHaul`.
- 저장 권위와 실행 명령: recipe/prefetch/item mass가 definition 권위, Production bill과 Physical Items/Haul Intent가 mutable 권위다. claim/profile restore candidate는 활성 bill 전체에서 준비해 map swap으로 게시하며 occupancy와 transient token은 저장하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: production live `RequestDeliveryWithinMassCapacity` caller `0`, 일반 exact delivery caller `1`, claim/profile schema revision `1`, input owners `39`, migrated `2`, remaining `37`을 current-source ratchet으로 고정한다.
- 검증 매트릭스와 보고서 위치: `FacilityBufferMassAdmissionDebugScenarios`, `PhysicalStockQueryV18DebugScenarios`, `ProductionEconomyDebugScenarios`, `IndustrialInfrastructureDebugScenarios`, `Artifacts/QA/production-input-buffer-mass-playmode-report.txt`. 실제 LiveFacility·AIHaul에서 4,200g admit, overflow 무변경 거절, carried pickup/cancel actor-cell 회수, current-format restore, 1,400g WIP Transfer와 수량 8/8이 PASS했다. 보고서 SHA-256은 `A3902852796480CA6F6F253CF64E415E0904915913D3AFE22148B450B993466A`, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: production input 한 owner family의 공용 profile/token 이관은 공식·live 검증까지 완료했다. 다른 input `37`, output `5`, bypass `5`, orphan `1`, kg-aware EWU·가격·6인 생존망과 다중 seed 전에는 FacilityBuffer·생산·전체 밸런스 완료가 아니다.

### balance:v27:facility-buffer-owner-classification-manifest-v2

- 시대/역할: production common-profile cutover 뒤 current source의 모든 FacilityBuffer/FacilityOutputBuffer owner와 우회 경로를 다시 고정한 결정론적 감사 증거다.
- Before: v1 보고서 해시는 power-fuel code 상태를 반영했지만 production common lifecycle·실제 LiveFacility verifier source digest 이전 값이었다.
- After: 동일 50행 분류와 `59개/39파일` delivery census를 최신 source digest `f73709bec9fcb41478ee882a38b56a270eaa1c7f7f49ae9a4acea4b4e28d2daf`로 재캡처했다.
- 물리 BOM·입력·출력: item·recipe·facility BOM, 수량, grams와 물리 stack 변경 0. registry 분류와 source digest만 갱신했다.
- 직접 작업량과 계산 근거: WU·처리시간·운반 속도 변경 0. source census와 stable registry row 수만 검증한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. remaining/bypass 해소 전 kg-aware 경제 재생성을 완료로 보지 않는다.
- 시간·확률·재시도: timestamp·RNG·machine path를 artifact에 포함하지 않으며 두 번의 capture가 같은 byte를 생성한다.
- 공간·전력·물·연료·정비: 시설 공간·utility·capacity 변경 0. `fullStoredDestinationCoverage=false`를 유지한다.
- 위험·실패·회복 방식: delivery invocation drift, 누락·중복·stale classification, source digest 비결정성, unclassified callsite를 fail-loud한다.
- 사회·비가역 비용: 플레이 상태 변화 0. remaining owner를 완료로 오인하는 리뷰 위험을 줄인다.
- 기존 대안과의 장단점: 수동 표는 source drift를 놓치지만 current-source registry는 즉시 drift를 잡는다. semantic analyzer보다 좁으므로 full migration gate와 production tests를 함께 요구한다.
- 지배 전략 방지 조건: `remaining=0`, `bypass=0`, `orphan=0` 전에는 `RequireFullyMigrated`가 항상 실패해야 한다.
- 실행 경로: `ProductionEconomyDebugScenarios → V27FacilityBufferOwnerManifestDebugScenarios → current-source census → deterministic CSV/TXT writer`.
- 저장 권위와 실행 명령: CSV/TXT는 gameplay 저장 권위가 아닌 읽기 전용 증거다. 실제 mutable 권위는 각 domain save와 Physical Items/Haul Intent에 남는다.
- 자동 감사 ID와 전수 목록 포함 여부: input `39`, migrated `2`, remaining `37`, output `5`, bypass `5`, orphan `1`, delivery `59/39`, unclassified `0`, classification gate `PASS`, full migration gate `OPEN`이다.
- 검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-facility-buffer-owner-manifest.csv` SHA-256 `DF455D3CA1BD9D7C07939FA0758C210743CDCEC03BF4F0753B3271E52DC6E5A4`, `.txt` SHA-256 `377388A7B6187E83031EA0DB9F1B8DEA7A170FB94F49967C510B069667A5FADC`. 연속 두 생성의 byte·mtime 변화 `0`, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: classification coverage와 두 migrated input owner는 검증됐지만 remaining `37`, output `5`, bypass `5`, orphan `1`이 남아 full migration gate는 의도적으로 OPEN이다.

### balance:v27:equipment-repair-common-buffer-admission-v1

- 시대/역할: 모든 시대의 장비 수리 주문이 손상된 고유 장비와 exact 원재료를 `equipment-repair:{equipmentInstanceId}` 작업 버퍼로 운반하는 공용 gram admission 수직 슬라이스다.
- Before: exact LiveFacility claim과 실제 AIHaul/WIP Transfer는 있었지만 destination의 positive gram profile이 없었다. 장비와 재료 요청이 시설 공간을 공유한다는 admission 권위와 restore/terminal profile 수명주기가 강제되지 않았다.
- After: 활성 repair order 전체가 claim과 profile을 deterministic하게 계산해 공용 lifecycle로 원자 교체된다. managed exact-stack 배송은 profile/token을 통과하고 완료·취소·복원은 남은 활성 주문 전체를 재게시한다.
- 물리 BOM·입력·출력: 대표 나무 방패 수리는 unique shield의 현재 동적 질량과 `material:blacksteel-ingot×3`을 합쳐 exact `6,500g` input capacity를 사용한다. 수리 뒤 장비 인스턴스와 원재료 성질을 보존하고 salvage는 기존 original-material contract를 유지한다.
- 직접 작업량과 계산 근거: repair WU·작업시간·내구 회복량은 변경하지 않았다. capacity는 정확히 한 repair job만 수용하며 반복 처리량 2~4회분을 적용하지 않는다.
- EWU와 목표 회수 기간: 장비·재료 EWU·가격·수리 ROI 변경 0. 실제 haul 횟수와 buffer pressure는 전수 kg-aware EWU 재생성 때 반영한다.
- 시간·확률·재시도: capacity schema revision은 `1`, RNG 없음. 요청·token·WIP acknowledgement와 restore는 같은 order ID/provenance를 재사용해 두 번째 debit을 만들지 않는다.
- 공간·전력·물·연료·정비: 대표 profile은 6.5kg이고 시설 footprint·전력·유체는 변경하지 않는다. 장비와 재료가 한 destination capacity를 공유한다.
- 위험·실패·회복 방식: 장비 instance 누락, module/ammo component decode 실패, unknown material mass, overflow, claim/profile owner/facility/revision/capacity mismatch는 source mutation 전에 fail-loud한다. terminal republish 실패 시 기존 authority를 rollback한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 수리 버퍼를 무제한 임시 창고로 사용하거나 한 주문 취소로 다른 주문 destination을 지우는 악용을 막는다.
- 기존 대안과의 장단점: 재료만 세는 단순 profile은 unique 장비 본체와 모듈·탄약을 무료 공간으로 만든다. full dynamic mass profile은 component codec 검증 비용이 있지만 실제 운반 부담과 일치한다.
- 지배 전략 방지 조건: 장비 본체·모듈·장전탄 이중/누락 계상 0, profile 없는 managed delivery 0, 한 주문 terminal이 다른 주문 authority를 지우는 경우 0, WIP/restore 질량 leak 0을 요구한다.
- 실행 경로: `EquipmentMaintenanceRuntime.TryRequestManualRepair → TryPublishRepairBufferAuthorities → FacilityBufferDestinationLifecycleService → WorldItemWarehouseService/FacilityBufferMassAdmissionService → AIHaul → EquipmentRepairMaterialOutbox → repair completion`.
- 저장 권위와 실행 명령: equipment instance/component와 material definition이 mass 권위, maintenance order와 Physical Items/Haul Intent/WIP receipt가 mutable 권위다. transient admission token은 저장하지 않고 restore candidate가 claim/profile을 다시 준비한다.
- 자동 감사 ID와 전수 목록 포함 여부: equipment legacy individual claim/revoke `0`, owner-wide lifecycle publication `2`, delivery authority assertion `1`, input owners `39`, migrated `3`, remaining `36`, unclassified `0`을 source ratchet한다.
- 검증 매트릭스와 보고서 위치: `EquipmentRepairMaterialOutboxFixture`, `StrictProgressionCombatSaveDebugScenarios`, `FacilityBufferMassAdmissionDebugScenarios`, `PhysicalStockQueryV18DebugScenarios`, `Artifacts/QA/equipment-repair-buffer-mass-playmode-report.txt`. 실제 LiveFacility/AIHaul 두 pickup, profile `6,500g/revision 1`, 중복 요청 0, 수리·salvage 보존, terminal claim/profile 0이 PASS했다. focused report SHA-256은 `867BC180F3C532F549A1584250B7C5D95C274EE8834352D12D188838526DFDDE`, captured/Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: equipment repair owner의 공식·restore·live 경계는 닫았다. 최신 manifest CSV/TXT SHA-256은 `4578FAA4E4D1310484E2CB966E4FCD7BCECC17A99E1BB322E765DA74421B55EE` / `CAAC0A58031E9C50926161A0A6F5858BAFBB018C63D14374E26006B7CBB56A31`, 두 번째 생성 변화 0이다. 다른 input `36`, output `5`, bypass `5`, orphan `1`, 전수 kg/EWU/가격·6인망·broad 물류 전에는 전체 완료가 아니다.

### balance:v27:prepared-output-exact-provenance-checkpoint-v1

- 시대/역할: 모든 시대의 표준 생산 완료품이 `FacilityOutputBuffer`에서 provenance를 잃지 않고 Loose·AIHaul·창고로 이동하기 위한 exact route와 durable checkpoint 권위다.
- Before: prepared output은 batch/line/range가 다른 물리 stack을 legacy item-only 이동·병합·직접 소비 경계에서 구분하지 못했고, 원본 receipt와 현재 delivery target, Economy tombstone과 Items custody GC가 하나의 durable checkpoint로 결합되지 않았다.
- After: split-aware custody schema3와 Physical Items V13이 batch/line/origin/range/grams, 원본 physical receipt/target, current-delivery revision overlay를 분리해 저장한다. Economy routing owner와 Items exact outbox/descendants는 동일 save digest·checkpoint sequence·candidate ID 집합으로만 GC된다.
- 물리 BOM·입력·출력: item quantity, recipe BOM, output grams와 authored mass 변경 0. exact physical partition은 source/remainder/routed child의 quantity·grams·business components·unit range 합을 보존하고 unique partial을 거부한다.
- 직접 작업량과 계산 근거: WU·생산시간·운반속도 변경 0. 이번 체크포인트는 provenance와 저장 원자성을 닫으며 실제 haul 횟수와 Wait WU는 후속 PlayMode/질량 재생성에서 측정한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. routing/GC가 재료 복제·삭제·원격 반환을 만들지 않는 구조 공식만 검증했다.
- 시간·확률·재시도: 동일 operation/request/receipt replay는 exact 일치만 허용하고 conflict를 fail-loud한다. same serialized digest checkpoint replay는 sequence와 participant authority를 바꾸지 않는다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·buffer capacity 변경 0. current target overlay는 destination intent일 뿐 physical stack을 즉시 이동시키지 않는다.
- 위험·실패·회복 방식: carry/in-transit/reservation/recovery가 남은 batch는 부분 GC하지 않고 whole-batch defer한다. mutation/retail/theft/relocation/compaction/generic spawn/FacilityBuffer aggregation 우회는 custody를 만나면 physical mutation 전에 typed fail-close한다.
- 사회·비가역 비용: 신규 사회·기분 비용 없음. 저장 성공 뒤 provenance만 제거하며 save byte가 durable하지 않거나 participant가 불일치하면 기존 authority를 유지·rollback한다.
- 기존 대안과의 장단점: item ID/수량 기반 generic 이동은 단순하지만 batch·quality·range를 잃는다. exact outbox/custody는 저장·복원 검증 비용이 있으나 원료 삭제·복제와 route tombstone 누수를 fail-loud한다.
- 지배 전략 방지 조건: quantity/gram gap·overlap·1g mismatch, orphan/extra route, same-operation conflict, checkpoint partial publish, custody generic mutation, terminal bill 조기 retire를 모두 0으로 요구한다.
- 실행 경로: `ProductionPreparedOutputRoutingAuthority → FacilityOutputExactRouteService → Physical Items V13/AIHaul intent → PreparedOutputCheckpointGcCoordinator → DungeonGameSaveSlotService durable replace`다.
- 저장 권위와 실행 명령: Economy routing V4와 Physical Items V13이 mutable authority다. checkpoint coordinator는 정확히 Economy 1개와 Items 1개 participant만 허용하며 CSV/문서는 gameplay 권위가 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: `PREPARED_OUTPUT_ROUTE_SPLIT_EXACT`, `PREPARED_OUTPUT_RESTORE_BIDIRECTIONAL`, `PREPARED_OUTPUT_CHECKPOINT_GC_TWO_PARTICIPANTS`, `PREPARED_OUTPUT_CUSTODY_MUTATION_GUARDS`를 focused gate로 유지한다. legacy production bypass 전수 0은 아직 OPEN이다.
- 검증 매트릭스와 보고서 위치: `PreparedOutputCheckpointGcDebugScenarios`, `FacilityOutputExactRouteDebugScenarios`, `PreparedOutputCustodyMutationGuardDebugScenarios`, `PreparedOutputBufferAggregationGuardDebugScenarios`, `PreparedOutputCustodyCarryBoundaryDebugScenarios`, Economy routing/restore/prepared suites와 `ProductionEconomyDebugScenarios`. fresh compile과 전 시나리오 PASS, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: plan 97.4의 exact split/outbox/Economy owner/restore join/checkpoint GC 6행은 공식·저장 focused 검증 단계까지 닫았다. live delivery reroute, actual AIHaul PlayMode, feedbench 전구간, artifact 2회 identity, 전수 kg/EWU/가격과 6인망 전에는 생산·물류·전체 밸런스 완료가 아니다.

### balance:v27:prepared-output-live-aihaul-hay-feed-v1

- 시대/역할: P17 사료배합대의 표준 생산 완료품이 실제 `FacilityOutputBuffer → Loose exact custody → AIHaul → kg warehouse` 경로를 통과하는 첫 live 수직 슬라이스다.
- Before: exact route·delivery overlay·gram admission은 focused fixture에서 통과했지만 실제 warehouse 접근칸과 중심칸이 다른 배치에서 pickup preflight가 의도를 stale로 오판했고, extraction은 최초 receipt의 빈 target을 current target 대신 비교했다.
- After: pickup 직전 검증은 live warehouse에서 다시 계산한 walkable delivery cell과 intent의 delivery/drop cell을 비교하며, custody extraction은 monotonic `CurrentTargetDestinationId`를 사용한다. immutable receipt는 감사 권위로 그대로 보존한다.
- 물리 BOM·입력·출력: `recipe:hay-feed` 입력은 `resource:grass-straw×3 + resource:twilight-grain×1`, 출력은 `feed:hay×3 = 588g`이다. authored BOM·수량·unit grams 변경은 없다.
- 직접 작업량과 계산 근거: WU·생산시간·운반속도 변경 0. 일반 actor의 live 성능 밴드 `약 19.1kg 무감속 / 28.7kg 최대`에서 588g lot을 기존 AIHaul로 운반했다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 실제 pickup·이동·적재 성공만 증명하며 전수 kg-aware EWU 재생성은 아직 OPEN이다.
- 시간·확률·재시도: hay-feed는 이 행에서 확률 출력이 아니다. 최초 receipt와 current delivery revision을 분리해 retry가 최초 감사값을 덮어쓰지 않는다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·buffer capacity 변경 0. warehouse center와 walkable delivery cell이 다른 실제 배치를 통과했다.
- 위험·실패·회복 방식: pickup 직전 source/lease/intent/admission/live destination을 다시 join한다. stale authority는 source mutation 전에 typed 실패하고 정상 current overlay만 extraction한다.
- 사회·비가역 비용: 신규 사회 비용 없음. 실패한 pickup이 물건을 삭제·복제하거나 최초 receipt를 현재 목적지로 변조하는 경우를 제거한다.
- 기존 대안과의 장단점: center 좌표를 interaction 좌표로 간주하면 단순하지만 실제 multi-cell 시설에서 거짓 실패한다. live cell 재해석은 grid 조회가 필요하지만 정상 배치와 저장 authority가 일치한다.
- 지배 전략 방지 조건: source quantity `3`, route grams `588`, warehouse stored `3`, terminal reserved inbound `0`, Console Warning/Error `0/0`을 요구한다.
- 실행 경로: `ProductionBillRuntime → ProductionWorkExecutionRuntime → ProductionPreparedOutputRoutingAuthority → FacilityOutputExactRouteService → WorldItemHaulPlanningService → AbilityHaul → WorldItemWarehouseService`.
- 저장 권위와 실행 명령: Economy routing owner, Physical Items V13 custody, Haul Delivery Intent와 warehouse admission이 mutable 권위다. receipt 원본과 current delivery overlay를 별도 저장한다.
- 자동 감사 ID와 전수 목록 포함 여부: `PREPARED_OUTPUT_LIVE_BATCH_COMPLETED`, `PREPARED_OUTPUT_LIVE_EXACT_WAREHOUSE_TARGET`, `PREPARED_OUTPUT_LIVE_AIHAUL_CAN_START`, `PREPARED_OUTPUT_LIVE_STORED_WITH_DURABLE_ADMISSION`을 첫 live marker로 추가했다. 다른 feed recipe와 fault/restore는 포함하지 않는다.
- 검증 매트릭스와 보고서 위치: `PreparedOutputHaulPlannerGateDebugScenarios` PASS, `Artifacts/QA/prepared-output-warehouse-live-playmode-report.txt` SHA-256 `DB95B67A452D56DC89A6BF049DF7C81307AB110D20D0E3AA9C83967B2F93CCA8`, `RESULT=PASS; failures=0`, Console Warning/Error `0/0`.
- 현재 밸런스 상태: 정상 whole-stack hay-feed live route는 닫혔다. partial/cancel/Downed/mid-haul restore, 다른 세 feed recipe, 사일리지 실패, 전수 owner migration·kg/EWU/가격·6인 생존망 전에는 생산·물류·전체 밸런스 완료가 아니다.

### balance:v27:output-line-census-and-no-bill-buffer-capacity-v1

- 시대/역할: 모든 시대의 표준 생산 output line 전수 식별과, P17 사료배합대가 production bill 없이도 최악 reachable branch 4회분의 물리 출력 공간을 보유하는 current-source 검증이다.
- Before: 정적 추정치는 `353 physical lines / 349 missing`이었고 P17 profile은 active bill 복원 시점에만 관찰되어 신규 placement의 no-bill 권위가 증명되지 않았다.
- After: Unity current domain에서 `355 recipes / 357 physical output lines / canonical 4 / missing proposal 353`을 결정론적으로 캡처했다. 신규 P17 placement는 bill 생성 전 `4,200g` profile을 게시하고, 이후 hay output `588g`을 같은 상한 아래 exact publication한다.
- 물리 BOM·입력·출력: P17 최대 정상 branch는 dog-food `2×525g=1,050g`, hay 실제 branch는 `3×196g=588g`, 4 cycle physical capacity는 `4,200g`이다. BOM·output quantity·unit grams 자체는 변경하지 않았다.
- 직접 작업량과 계산 근거: WU·작업시간·운반속도 변경 0. capacity는 현재 이관 범위의 모든 reachable recipe physical branch 최대값과 authored cycle count 4에서 계산한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 실제 kg buffer와 haul leg는 후속 kg-aware EWU 재생성의 입력이며 아직 경제 완료 증거가 아니다.
- 시간·확률·재시도: probability가 양수인 독립 physical line을 최대 branch에 포함하고 DeclaredLoss는 물리 capacity에서 제외한다. 이번 P17 live branch는 확률 출력이 아니며 재굴림 계약을 변경하지 않는다.
- 공간·전력·물·연료·정비: footprint·utility·연료·정비 수치 변경 0. output buffer는 legacy count batch와 분리된 positive gram profile을 사용한다.
- 위험·실패·회복 방식: 신규 capable facility는 bill 없이 profile이 있어야 하며 topology 변화 때 exact set replacement한다. occupancy가 projection을 초과하거나 non-capable destination에 physical output이 남으면 권위를 키우거나 폐기하지 않고 fail-loud한다.
- 사회·비가역 비용: 신규 사회 비용 없음. active bill 선택으로 작은 profile을 만들거나 no-bill 시설을 무제한 임시 창고로 쓰는 여지를 제거한다.
- 기존 대안과의 장단점: current bill 기준은 단순하지만 더 무거운 recipe로 전환할 때 overflow한다. reachable 최대 branch 기준은 공간을 선예약하지만 안정적인 2~4 cycle 처리와 typed admission을 보장한다.
- 지배 전략 방지 조건: active bill 유무에 따른 capacity 차이 0, 4,200g 초과 silent growth 0, 1g overflow 허용 0, legacy count/gram 권위 혼합 0을 요구한다.
- 실행 경로: `BuildingVersion topology change → ProductionBillRuntime → ProductionPreparedOutputExecutionAdapter.RestoreDestinationAuthorities → ProductionOutputBufferCapacityProjector → ProductionOutputDestinationAuthorityRuntime.TryReplaceProjected → FacilityBufferDestinationLifecycleService`; 실제 생산은 기존 exact route와 AIHaul 경로를 따른다.
- 저장 권위와 실행 명령: recipe/facility/item mass가 immutable projection 권위이고 claim/profile과 Physical Items가 mutable 권위다. AuditOnly proposal CSV/TXT는 gameplay 저장 권위가 아니며 SO를 수정하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `PREPARED_OUTPUT_LIVE_FEEDBENCH_NO_BILL_CAPACITY_4200G`, `PREPARED_OUTPUT_LIVE_FEEDBENCH_MAX_BRANCH_CAPACITY_4200G`, output-line `357/4/353`, deterministic recapture PASS를 기록한다.
- 검증 매트릭스와 보고서 위치: output proposal CSV SHA-256 `74827AADFAE968FD4E706AA1BE180010D67C691996C6A645DC4240D5F3DB81AE`, TXT SHA-256 `C5E4CD3B6866ECE6BE07A003AC8AC699A3D268B4170FDA4C86B838D4C2BDBF19`; live report SHA-256 `B32C1ABF3FF0D6C9BD6D6A96A863F25AD2668131ADA875F0EDDB57AB08C8413B`, `RESULT=PASS`, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: 신규 placement no-bill profile과 current output-line denominator만 닫혔다. 353 line 적용, 다섯 custom owner, all-capable maximum envelope, digest revision, destructive lifecycle, fault/restore, EWU·가격·6인망 전에는 생산·물류·전체 밸런스 완료가 아니다.

### balance:v27:canonical-production-output-line-apply-v1

- 시대/역할: 모든 시대의 355개 production recipe가 물리 output provenance를 authored ordinal이나 item-only fallback이 아닌 canonical line ID와 역할로 식별하도록 하는 전수 authoring 적용이다.
- Before: 357개 physical output line 중 canonical ID는 4개뿐이고 353개가 비어 있었다. logging/quarry/saltstone의 확률 secondary output 6개도 Main으로 직렬화되어 있었다.
- After: reviewed proposal exact join 뒤 ID 353개와 역할 6개만 변경했다. canonical ID `357/357`, empty `0`, Byproduct asset은 승인된 source 3개뿐이며 두 번째 Apply는 변경/SaveAssets `0/0`이다.
- 물리 BOM·입력·출력: item ID, input/output quantity, probability, order, unit grams와 BOM 변경 0. secondary line의 물리 역할만 실제 의미에 맞게 Byproduct로 교정했다.
- 직접 작업량과 계산 근거: WU·cycle time·운반속도 변경 0. ID는 `output:{recipeId}/{ordinal:D3}/{role}/{itemId}`로 결정론적으로 생성된다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 이 적용은 향후 exact mass/EWU 귀속을 위한 식별 권위이며 경제 재생성 자체는 아직 OPEN이다.
- 시간·확률·재시도: 확률값을 변경하지 않았다. canonical line ID는 key-addressed outcome과 retry/save replay에서 같은 physical line을 재식별한다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·buffer capacity 변경 0. capacity 연결은 각 recipe output component가 지원된 뒤 별도 migration한다.
- 위험·실패·회복 방식: 357행 전체가 approved Before 또는 After와 exact 일치하지 않으면 mutation 전에 실패한다. source/asset digest stale, dirty SO, duplicate/noncanonical ID, 허용되지 않은 role 변경도 preflight에서 거부한다.
- 사회·비가역 비용: 플레이 사회 비용 없음. 안정 ID가 없는 output retry로 발생할 수 있는 복제·오귀속 위험을 줄인다.
- 기존 대안과의 장단점: 배열 ordinal fallback은 간단하지만 reorder에서 identity가 바뀐다. authored stable ID는 asset diff가 생기지만 save/retry/provenance를 명시적으로 결합한다.
- 지배 전략 방지 조건: item/amount/probability/order 변경 0, 승인 외 role 변경 0, partial apply 0, duplicate ID 0, second-run diff 0을 요구한다.
- 실행 경로: `ProductionRecipeSO.outputs → CaptureCanonicalOutputs → CanonicalProductionOutputResolver → PreparedProductionOutputBatch → exact publication/routing`이며 stateful family는 component codec gate를 통과하기 전 migration scope에 넣지 않는다.
- 저장 권위와 실행 명령: Recipe SO가 line ID/role immutable 권위다. proposal artifact는 reviewed Before, apply manifest는 change evidence이며 mutable gameplay save 권위가 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: `357/353/6`, before/after semantic hash, inspected/source digest, changed asset GUID 347개와 second Apply no-op을 manifest에 기록한다.
- 검증 매트릭스와 보고서 위치: apply manifest SHA-256 `4C5F68B5109FE5D26E78AE333BD5030DF635FA17F13B60FED81BACC02EAE0724`; proposal CSV/TXT SHA-256 `9050205DED259D2DC93C8BB83C170BD8B93C31C01E21D00C01A903A2AF0B8BC7` / `E45C778C6FFD71A344BA5D7D03F1464F62A7727C364F33F84532B7FCA7AA5442`; Production Economy/Surgery contracts PASS, full compile, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: canonical output authoring은 닫혔다. generic/stateful component migration, custom handler 5개, lifecycle/fault, kg-aware EWU·가격·6인망 전에는 production 또는 전체 밸런스 완료가 아니다.

### balance:v27:production-reachability-audit-v1

- 시대/역할: output capacity maximum envelope가 현재 잠금 결함을 조용히 제외하지 않도록 recipe research와 production support unlock graph를 전수 감사한 current-source 증거다.
- Before: current-state multiplier만 조회하면 누락 연구와 설치 불가능 support를 단순히 0으로 보아 buffer capacity를 과소 계산할 수 있었다.
- After: 355 recipe, 180 research, 28 support, support-tagged recipe 40개/50 links를 결정론적으로 캡처했다. orphan research reference 11개와 unreachable support WS08/WS10 2개를 exact expected Critical로 노출했다.
- 물리 BOM·입력·출력: BOM·output·grams 변경 0. 누락 경로를 capacity 후보에서 제거하지 않고 content Critical로 유지한다.
- 직접 작업량과 계산 근거: WU 변경 0. support consumer는 hearth 15, oven 2를 포함하며 authored support 배율은 현 시점 전부 1.0이다.
- EWU와 목표 회수 기간: EWU·가격 변경 0. 연구 잠금으로 producer가 영구 비활성인 상태에서는 ROI를 완료로 계산하지 않는다.
- 시간·확률·재시도: RNG 변경 0. AuditOnly 두 capture의 CSV/report/source digest가 동일해야 한다.
- 공간·전력·물·연료·정비: support footprint/utility 변경 0. WS08은 hearth 유일 공급자이므로 해금 소유자 부재가 음식 생산 15 recipe의 공간·서비스 경로를 막는다.
- 위험·실패·회복 방식: 누락 연구/support는 maximum envelope에서 omission하지 않고 fail-visible Critical로 보고한다. runtime alias fallback은 만들지 않는다.
- 사회·비가역 비용: 플레이 mutation 0, SO JSON/Dirty 변화 0. 영구 잠금 콘텐츠를 정상 밸런스로 오인하지 않게 한다.
- 기존 대안과의 장단점: current installed graph는 실제 순간 상태를 반영하지만 미래 reachable maximum을 보장하지 않는다. authored graph 감사가 추가 비용을 들이되 capacity 안전 상한을 제공한다.
- 지배 전략 방지 조건: missing research/support를 제외한 축소 capacity 0, editor-only alias 의존 0, unreachable sole-provider 방치 0을 요구한다.
- 실행 경로: `ProductionRecipeSO.RequiredResearchId → ResearchProjectSO catalog`, `RequiredSupportTags → BuildingProductionSupportAbility → BlueprintBuildingUnlock` exact join이다.
- 저장 권위와 실행 명령: research/building/recipe SO가 권위이고 CSV/TXT는 AuditOnly evidence다. gameplay save와 에셋을 수정하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: recipes `355`, research `180`, orphans `11`, supports `28`, unreachable `2`, links `50`, hearth `15`, oven `2`, source files `918`을 고정한다.
- 검증 매트릭스와 보고서 위치: CSV SHA-256 `FDDF26519B2560DF2CE1E53D733050DE0626493E709C0C107D6B3A5EBE47E962`, TXT SHA-256 `1AACE43CE73EE9C4FBA73DFBD6AF406D67D316910F4D696DB6D829B172EDCD81`, deterministic recapture PASS, SO mutation 0이다.
- 현재 밸런스 상태: 감사 도구와 분모만 닫혔다. 11 recipe ID canonicalization, WS08/WS10 unlock ownership, exact multiplier envelope 적용 전에는 reachability·capacity·전체 밸런스 완료가 아니다.

### balance:v27:surgical-part-prepared-output-focused-v1

- 시대/역할: 수술 부품 제작 결과를 작업자 셀의 직접 Loose 생성에서 시설별 exact prepared-output custody로 이관하는 의료 출력 경계다.
- Before: `SurgicalPartProductionOutputHandler`가 제작된 unique 부품을 작업자 현재 셀에 직접 게시했고, 출력 gram 예약·physical instance join·재시도 acknowledgement가 하나의 계약으로 닫히지 않았다.
- After: crafted output은 exact unique component와 total grams를 준비하고 `production-output:{facilityId}` 용량을 예약한 뒤 planned publication, physical instance join, domain commit, provenance acknowledgement 순서로 진행한다. 구형 `TryCreateCraftedPart` direct 경로는 typed 실패한다.
- 물리 BOM·입력·출력: 수술 부품 recipe BOM·수량·unit grams 변경 0. 준비된 unique component와 실제 게시 instance의 ID·component·grams를 exact join한다.
- 직접 작업량과 계산 근거: 제작 WU·작업시간·운반속도 변경 0. 이번 slice는 출력 소유권과 원자성만 변경한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. prepared route의 실제 haul leg는 후속 kg-aware EWU 재생성 입력이며 아직 계산하지 않았다.
- 시간·확률·재시도: capacity 거절은 게시 0, publish 실패는 reservation 해제, runtime join 실패는 physical publication 역연산, domain commit 실패는 모든 owner rollback, 성공 replay는 두 번째 게시 0을 요구한다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·의료 fluid·buffer authored capacity 변경 0. live 시설 capacity projection은 후속 PlayMode에서 별도 검증한다.
- 위험·실패·회복 방식: acknowledgement의 operation/commit/component/instance/grams가 다르면 거절하며 exact acknowledgement는 idempotent하다. 실패 단계에서 unique item 복제·삭제·orphan reservation을 허용하지 않는다.
- 사회·비가역 비용: 신규 사회 비용 없음. 제작 부품이 작업자 위치에 임의 생성되어 시설·운반 권위를 우회하던 경로를 제거한다.
- 기존 대안과의 장단점: 직접 Loose 생성은 단순하지만 용량·저장·재시도 원자성을 보장하지 않는다. prepared 경로는 단계별 계약이 늘지만 save/retry와 kg custody를 증명할 수 있다.
- 지배 전략 방지 조건: capacity 부족 시 output 0, 실패 retry 중 이중 게시 0, acknowledgement 변조 승인 0, 성공 replay 게시 0을 요구한다.
- 실행 경로: `SurgicalPartProductionOutputHandler → SurgicalPartRuntime prepared output contract → FacilityOutputBuffer planned publication → physical unique instance join → domain commit → acknowledgement`.
- 저장 권위와 실행 명령: surgical-part domain state, prepared-output operation/commit, Physical Items unique instance와 output destination admission이 mutable 권위다. focused fixture는 저장 권위가 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: owner manifest의 `medical.surgical-part-output`을 `outputMigrated=1`로 분류하며 input/output remaining을 별도로 집계한다.
- 검증 매트릭스와 보고서 위치: `SurgicalPartPreparedOutputDebugScenarios.RunAll()` PASS, Unity full compile PASS, owner manifest schema 2 TXT/CSV SHA-256 `0A4A2A88D4595340C0AE8D60EFF463B03BCD71C86ED73DD53C59100E6FFDEFC9` / `31CFCF0F503C512FB207849B8C6E1269F3FE171D8EB1FB39CBAEE52649BA88BA`, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: 수술 부품 crafted output의 focused 원자성 경계만 닫혔다. 실제 주문·작업·AIHaul·kg warehouse PlayMode, 나머지 output owner 5개, input owner·bypass·orphan, EWU·가격·6인망 전에는 의료 출력 또는 전체 밸런스 완료가 아니다.

### balance:v27:production-reachability-canonical-closure-v2

- 시대/역할: 모든 시대의 생산 레시피·물리 아이템 연구 게이트와 생산 지원 시설의 실제 해금 경로를 canonical 연구 그래프에 결합하는 P0 연결성 교정이다.
- Before: recipe 11개와 대응 item 11개가 V21에서 흡수된 연구 ID를 계속 참조했고, WS08 hearth와 WS10 electric oven은 `unlocked=0`이면서 어느 연구에도 귀속되지 않았다.
- After: recipe/item 각 11개를 V21 survivor ID로 exact 교정했다. WS08은 `research:cuisine:crops`, WS10은 `research:industry:assisted-processing`에 귀속해 recipe orphan `0`, item orphan `0`, unreachable support `0`을 달성했다.
- 물리 BOM·입력·출력: BOM·output quantity·unit grams 변경 0. 연구 게이트 22필드와 연구 building unlock 2개만 변경했다.
- 직접 작업량과 계산 근거: WU·cycle time 변경 0. WS08은 15개 hearth recipe의 유일 provider이고, WS10은 power와 machine-parts를 요구하므로 assisted-processing 시점에 맞췄다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 영구 잠금 경로를 제거해 후속 maximum-envelope와 경제 감사를 실제 reachable graph 위에서 수행할 수 있게 했다.
- 시간·확률·재시도: RNG·확률·재시도 계약 변경 0. AuditOnly 두 capture가 byte/source digest 동일하고 SO mutation 0이어야 한다.
- 공간·전력·물·연료·정비: footprint·utility 수치 변경 0. WS08/WS10 BuildingSO의 `unlocked=0`은 유지하며 연구를 통한 정상 건설 경로만 추가한다.
- 위험·실패·회복 방식: 구형 ID runtime alias를 추가하지 않고 Authority에서 제거한다. 향후 orphan 또는 unreachable support가 하나라도 생기면 감사기가 fail-loud한다.
- 사회·비가역 비용: 기존 연구 완료 상태의 런타임 migration은 범위 밖이다. current-format 신규 진행에서만 canonical gate가 적용된다.
- 기존 대안과의 장단점: WS08을 시작 해금하면 연구 이전 시설 노출이 생기고, WS10을 cuisine baking에 넣으면 machine-parts 이전의 건설 불가 UI가 생긴다. 선택한 연구 귀속은 실제 전제 자원과 맞는다.
- 지배 전략 방지 조건: fuel WS09는 baking 대안으로 남고 powered WS10은 더 늦은 산업 선택지다. hearth 연구 우회, orphan reward, 잠겼지만 capacity에서 누락되는 support를 0으로 요구한다.
- 실행 경로: `ResearchProjectSO completion → BlueprintBuildingUnlock → Building unlock state → recipe RequiredResearchId/item ResearchGateItemFeature → production/support reachability`.
- 저장 권위와 실행 명령: ResearchProjectSO, ProductionRecipeSO, ResourceItemDefinitionSO, ResearchOverhaul/ResearchProject builder가 authored 권위다. AuditOnly CSV/TXT는 증거이며 gameplay save 권위가 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: recipes `355`, resource items `363`, research `180`, supports `28`, recipe/item orphans `0/0`, unreachable `0`, support-tagged `40`, links `50`, source files `1283`을 기록한다.
- 검증 매트릭스와 보고서 위치: `V27ProductionReachabilityAuditOnly`, `ResearchTreeDebugScenarios`, `ResearchEquipmentOverhaulDebugScenarios`, `BlueprintResearchDebugScenarios`, `ProductionEconomyDebugScenarios` PASS. TXT/CSV SHA-256 `63588BBDB2EB60B07AC5FF273FDF4DCA434CA29CF7188D3B211969B695BEF23E` / `3116475645AC02B1B83997517287960CF8BC37E9F198FEDED22382F37A520524`, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: 생산 연구/support reachability P0만 닫혔다. exact rational maximum multiplier, all-capable output profile, lifecycle/fault, EWU·가격·6인 생존망 전에는 FacilityBuffer 또는 전체 밸런스 완료가 아니다.

### balance:v27:exact-production-output-factor-grand-project-envelope-v1

- 시대/역할: 모든 standard production output과 물리 FacilityBuffer 최대치 산정에 쓰는 Grand Project 출력 배율의 공통 정수 유리수 권위다.
- Before: 실제 output은 `float` multiplier를 decimal로 변환해 계산하고 최대 buffer projector도 별도 `float` 곱셈과 `Ceiling`을 사용해, 같은 authored 배율이 실행·용량 경계에서 다르게 양자화될 여지가 있었다.
- After: `ProductionOutputFactor`가 양의 기약분수와 overflow-safe cross-GCD 곱셈을 소유한다. quarry `1.25=5/4`, crop-indoor `1.20=6/5`, alchemy/apothecary/distillery `1.15=23/20`, 그 외 `1/1`을 exact authority로 사용한다.
- 물리 BOM·입력·출력: 이번 변경은 BOM·기본 output quantity·unit grams를 바꾸지 않는다. 실제 standard output은 exact factor로 산출하고 FacilityBuffer 최대치는 같은 factor의 Ceil quantity로 예약한다.
- 직접 작업량과 계산 근거: Direct WU·cycle time 변경 0. authored float는 1 mPermille canonical 값으로만 수용하며 실행 중 float 누적을 허용하지 않는다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. output quantity가 정확해져야 후속 gram·EWU·가격 재생성의 입력이 일치한다.
- 시간·확률·재시도: 확률 roll 횟수와 저장 권위를 바꾸지 않는다. deterministic line quantity 계산만 공통 exact factor로 교체했다.
- 공간·전력·물·연료·정비: 시설 footprint·utility 변경 0. 최대 출력 질량만 도달 가능한 Grand Project 상한을 선반영해 output-space 부족에 의한 삭제·바닥 fallback을 막는다.
- 위험·실패·회복 방식: 비canonical multiplier와 산술 overflow를 fail-loud한다. 현재 authored support multiplier는 모두 `1.0`이며 향후 복수 support 조합은 stable bitset/DP와 전 branch 감사를 통과하기 전 승인하지 않는다.
- 사회·비가역 비용: 신규 사회 비용·영구 선택 변경 없음. 기존 저장에는 파생 factor를 추가하지 않는다.
- 기존 대안과의 장단점: float 계산은 단순하지만 실행과 capacity의 1단위 경계를 분리시킬 수 있다. exact rational은 검증 비용이 있으나 같은 권위와 경계 증명을 제공한다.
- 지배 전략 방지 조건: actual output은 exact scale, maximum capacity는 exact Ceil을 사용하고, underprojection·overflow·비canonical 배율을 조용히 보정하지 않는다.
- 실행 경로: `CanonicalProductionOutputResolver`, `ProductionOutputExecutionService`, `ProductionPreparedOutputExecutionAdapter`, `ProductionOutputBufferCapacityProjector`가 공통 factor를 사용한다.
- 저장 권위와 실행 명령: factor는 authored production tag와 Grand Project 상태에서 파생하며 저장하지 않는다. current standard output과 maximum projector가 같은 immutable value를 소비하고, prepared outcome fingerprint에는 float 표시값 대신 exact numerator/denominator를 기록한다.
- 자동 감사 ID와 전수 목록 포함 여부: current Grand Project 최대 태그 권위와 21개 영향 recipe 분모를 기록한다. support 조합·world resource·crop·ruined/probabilistic branch는 아직 전수 승인하지 않았다.
- 검증 매트릭스와 보고서 위치: Unity focused scenarios가 `5/4`, `6/5`, `23/20`, Ceil 경계, cross-reduction, overflow·noncanonical rejection, FacilityBuffer admission과 output destination authority를 통과했고 Console Warning/Error `0/0`이었다.
- 현재 밸런스 상태: 공통 exact 배율 산술과 standard 경로만 닫혔다. 21개 영향 recipe 전수, support 조합, world-resource/crop, ruined/probabilistic branch와 정상 부트 P17 live 재실행이 열려 있다. 직접 GameplayScene을 연 재실행은 DI 부트스트랩 누락으로 verifier 자체가 실패했으므로 밸런스 통과 증거가 아니다.

### balance:v27:prepared-output-positive-exact-profile-v1

- 시대/역할: current prepared-output migration에 들어간 feedbench 4개 레시피가 검토되지 않은 출력 topology로 조용히 바뀌는 것을 막는 positive profile ratchet이다.
- Before: migration membership은 recipe ID 4개만 확인했다. 같은 ID의 output item을 다른 definition-only generic item으로 바꾸면 component codec 자체는 유효해도 기존 검토 범위를 벗어난 output이 실행될 수 있었다.
- After: `recipe:dog-food`, `recipe:dog-food-fresh`, `recipe:hay-feed`, `recipe:silage` 각각에 process kind·facility tag·spoilage item·output line ID/role/item/quantity/probability exact profile을 둔다.
- 물리 BOM·입력·출력: BOM·unit grams·output quantity 변경 0. 기존 current topology를 positive allowlist로 고정했으며 임의 다른 generic output도 거부한다.
- 직접 작업량과 계산 근거: WU·cycle time 변경 0. 검토 경계의 source topology만 고정한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 승인되지 않은 output drift가 후속 질량·EWU 계산의 분자를 바꾸지 못하게 한다.
- 시간·확률·재시도: authored probability의 exact float bit가 profile과 같아야 한다. roll 횟수·WIP 저장·재시도 계약은 변경하지 않는다.
- 공간·전력·물·연료·정비: capacity 수치 변경 0. 최대 buffer projector도 같은 exact profile을 먼저 검증하므로 실행과 용량의 recipe set이 갈라지지 않는다.
- 위험·실패·회복 방식: recipe ID만 일치하는 drift, spoilage branch 변경, line role·quantity·probability 변경은 fail-loud한다. stateful family를 generic payload로 자동 승격하지 않는다.
- 사회·비가역 비용: 신규 gameplay 비용 없음. 저장 schema 변경 없음.
- 기존 대안과의 장단점: prefix·feature blacklist는 신규 feature를 놓칠 수 있다. positive exact profile은 명시적 갱신이 필요하지만 검토 범위를 좁고 감사 가능하게 유지한다.
- 지배 전략 방지 조건: 실행과 capacity projector 모두 동일 profile gate를 통과해야 하며 profile 밖 output은 legacy/custom handler에 남는다.
- 실행 경로: `ProductionPreparedOutputMigrationScope exact profile → ProductionPreparedOutputExecutionAdapter / ProductionOutputBufferCapacityProjector → component codec → physical mass/admission`.
- 저장 권위와 실행 명령: profile은 코드의 immutable reviewed authority이고 저장하지 않는다. 저장된 prepared batch는 기존 recipe digest와 component fingerprint를 계속 검증한다.
- 자동 감사 ID와 전수 목록 포함 여부: current numerator는 4 recipes, 4 normal output lines이며 silage ruined `waste:plant-rot` branch를 별도 spoilage field로 고정한다. 전수 357 physical line 승격은 아직 아니다.
- 검증 매트릭스와 보고서 위치: exact asset 4개 검증, compatible generic item drift 거부, component codec, exact output factor, FacilityBuffer admission, output destination, Production Economy scenarios가 Unity에서 PASS했고 Console Warning/Error `0/0`이었다.
- 현재 밸런스 상태: current 4개 profile의 silent source drift만 닫혔다. 나머지 definition-only 254 lines와 stateful 98 lines의 profile/handler 승인, live PlayMode, EWU·가격·6인망 전에는 Batch A 또는 전체 밸런스 완료가 아니다.

### balance:v27:authored-production-support-maximum-catalog-v1

- 시대/역할: 모든 시대의 production support와 current prepared-output 4개 레시피가 사용하는 물리 출력 버퍼의 authored maximum provider 권위다.
- Before: buffer projector가 현재 설치된 support modifier를 읽어 topology·설치 순서에 따라 더 작은 현재값을 최대값처럼 사용할 수 있었고, support 조합 상한의 안전한 실패 계약이 없었다.
- After: 28개 `BuildingProductionSupportAbility`를 immutable catalog로 캡처하고 support ID·feature tag·workstation tag를 exact 검증한다. recipe의 required/batch support tag는 호환 authored provider가 반드시 존재해야 하며 현재 설치 상태를 읽지 않는다.
- 물리 BOM·입력·출력: BOM·기본 output·unit grams 변경 0. 현재 support output factor 28개는 모두 exact `1/1`이고 current 4개 prepared profile은 별도 Grand Project exact factor와 결합한다. P17은 `1,050g/cycle × 4 = 4,200g`이다.
- 직접 작업량과 계산 근거: Direct WU·cycle time 변경 0. 최대 factor는 immutable authored support 집합과 facility tag의 exact Grand Project 상한에서 계산한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. output capacity의 underprojection을 막아 후속 kg-aware 물류/EWU 측정의 물리 경계를 안정화한다.
- 시간·확률·재시도: RNG·확률 roll·재시도 횟수 변경 0. probabilistic/ruined 모든 branch의 전수 maximum 증명은 아직 OPEN이다.
- 공간·전력·물·연료·정비: footprint·utility·support 설치비 변경 0. 현재 support factor가 모두 1/1이므로 support 유무가 P17 gram capacity를 축소하지 않는다.
- 위험·실패·회복 방식: missing provider, duplicate/noncanonical support ID·tag, invalid support와 non-unit support factor를 startup/capture에서 fail-loud한다. non-unit 발견 시 `NON_UNIT_SUPPORT_MAXIMUM_REQUIRES_DP`를 내고 조합값을 추측하지 않는다.
- 사회·비가역 비용: gameplay 선택·사회 비용·저장 schema 변경 없음. support 건설 순서에 따른 숨은 작은 버퍼만 제거한다.
- 기존 대안과의 장단점: live installed modifier는 현재 상태에는 정확하지만 미래 reachable maximum이 아니다. authored catalog는 보수적인 물리 공간을 확보하지만 미래 non-unit 복수 조합에는 별도 stable bitset/DP가 필요하다.
- 지배 전략 방지 조건: support 미설치로 capacity 축소 0, 누락 provider 조용한 제외 0, non-unit 곱/합 추측 0, actual/capacity factor 권위 분리 0을 요구한다.
- 실행 경로: `IGameContentCatalog BuildingSO capture → ProductionMaximumOutputFactorCatalog → ProductionOutputBufferCapacityProjector → ProductionOutputDestinationAuthorityRuntime → FacilityBuffer gram admission`.
- 저장 권위와 실행 명령: BuildingSO/ProductionRecipeSO와 exact Grand Project factor가 immutable 권위다. maximum catalog와 projected grams는 파생값이며 gameplay save DTO를 입력으로 받지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: authored supports `28`, recipes `355`, support-tagged recipes `40`, links `50`, Grand Project affected recipes `21`, current prepared profiles `4`, non-unit supports `0`을 기록한다.
- 검증 매트릭스와 보고서 위치: `ProductionMaximumOutputFactorCatalogDebugScenarios`, `ProductionOutputFactorDebugScenarios`, `ProductionPreparedOutputMigrationProfileDebugScenarios`, `FacilityBufferMassAdmissionDebugScenarios`, `ProductionOutputDestinationAuthorityDebugScenarios`, `ProductionEconomyDebugScenarios` PASS; Unity Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: current prepared-output 4개가 소비하는 authored support maximum만 닫혔다. 21개 영향 recipe 전수 migration, world-resource/crop, probabilistic/ruined branch, future non-unit support DP, 정상 boot live fault/restore, EWU·가격·6인망 전에는 Batch B 또는 전체 밸런스 완료가 아니다.

### balance:v27:prepared-output-recipe-semantic-source-revision-v1

- 시대/역할: 모든 시대의 prepared production WIP가 생성 당시 recipe 실행 의미와 current content를 exact 비교하는 복원 경계다.
- Before: 기존 recipe digest는 recipe/process/facility/input/output 일부만 포함했고 restore에서 current recipe와 재계산 비교하는 callsite가 없었다. 같은 ID의 WU·support·utility·spoilage drift가 resolved WIP에 섞일 수 있었다.
- After: `production-recipe-semantic@2`가 process/flow/class/authored flag, facility/workstation/work type/research, WU·숙련, passive 시간·온도, clean water·wastewater·manual fallback, support/batch support, spoilage, exact input/output/probability를 SHA-256으로 묶는다. resolved restore는 current digest가 다르면 `prepared-output-source-revision-stale`로 거부한다.
- 물리 BOM·입력·출력: BOM·quantity·unit grams 변경 0. recipe 의미를 식별하는 digest만 강화했으며 resolved output을 재계산하거나 재굴림하지 않는다.
- 직접 작업량과 계산 근거: WU 수치 변경 0. WU bit가 달라지면 digest가 달라져 이전 의미로 시작한 WIP가 새 WU 정의 아래 이어지지 않는다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. stale recipe 아래의 output grams가 경제 원장에 혼입되는 경로를 차단한다.
- 시간·확률·재시도: float는 finite 검증 후 `-0→+0` 정규화된 IEEE754 bits로 기록한다. display/locale/collection insertion order는 digest를 바꾸지 않으며 semantic field·확률 bit 변화는 바꾼다.
- 공간·전력·물·연료·정비: footprint·capacity 수치 변경 0. water/waste/support 의미는 recipe digest에 들어가지만 item/component/capacity source digest는 후속 OPEN이다.
- 위험·실패·회복 방식: noncanonical/duplicate input·support, invalid enum, NaN/Infinity를 capture에서 fail-loud한다. restore mismatch는 어떤 participant publication보다 앞선 Production candidate validation에서 거부한다.
- 사회·비가역 비용: gameplay 비용·사회 수치 변경 없음. stale WIP를 억지로 복구하지 않으며 과거 세이브 migration은 범위 밖이다.
- 기존 대안과의 장단점: raw YAML/file hash는 표시·직렬화 noise에 민감하다. 명시적 semantic digest는 schema 갱신 책임이 있지만 gameplay 의미만 안정적으로 식별한다.
- 지배 전략 방지 조건: stale WIP 재굴림 0, 같은 ID 의미 drift 승인 0, 표시명 변경에 의한 false stale 0, input 순서에 의한 false stale 0을 요구한다.
- 실행 경로: `ProductionRecipeSO → ProductionRecipeSemanticDigest → prepared batch recipeDefinitionDigest → ProductionBillStateCodec → ProductionPreparedOutputSourceRevisionGuard`.
- 저장 권위와 실행 명령: Recipe SO가 current immutable 의미 권위이고 prepared batch digest가 생성 당시 증거다. digest는 Authority를 대체하지 않으며 mismatch 시 current 값으로 덮어쓰지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: current prepared recipe 4개와 next candidate sawmill 1개의 exact digest ratchet을 기록한다. item/component/migration/capacity digest는 아직 분모에 포함하지 않는다.
- 검증 매트릭스와 보고서 위치: `ProductionRecipeSemanticDigestDebugScenarios`, prepared contract/restore/profile, output factor/support, FacilityBuffer admission/destination, `ProductionEconomyDebugScenarios` PASS; Unity Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: recipe semantic source binding과 resolved restore rejection만 닫혔다. item/component/migration/capacity source digest, save schema v2, sawmill live migration, EWU·가격·6인망 전에는 Batch A/B 또는 전체 밸런스 완료가 아니다.

### balance:v27:prepared-output-item-component-migration-source-v1

- 시대/역할: prepared production WIP가 생성 당시 output item·component profile·positive migration profile 의미를 current source와 exact 비교하는 current-format 저장 경계다.
- Before: recipe digest는 강화됐지만 같은 item ID의 kg·maxStack·가격·feature 변경과 코드의 positive migration profile 변경은 resolved batch의 기존 component payload만으로 검출되지 않았다.
- After: `resource-item-semantic@1`이 item ID·stock category·canonical grams·maxStack·unit price와 지원 feature 전체를 묶고, `production-prepared-output-component-profile@1`이 이 digest와 empty runtime-component payload를 합성한다. migration profile/registry도 exact digest를 가지며 batch schema v2의 `migrationProfileDigest`에 저장된다.
- 물리 BOM·입력·출력: BOM·output quantity·authored kg 변경 0. 기존 4개 feedbench item과 다음 후보 lumber의 현재 의미를 digest ratchet으로 고정했다.
- 직접 작업량과 계산 근거: WU 변경 0. recipe WU는 기존 recipe semantic digest, item kg·stack·가격·feature는 item digest, positive profile topology는 migration digest로 분리 귀속한다.
- EWU와 목표 회수 기간: EWU·가격 수치 변경 0. unit price는 의미 digest에 포함되어 stale prepared output이 새 가격 권위에 조용히 합류하지 못한다.
- 시간·확률·재시도: resolved output을 재굴림하지 않는다. profile mismatch는 restore candidate 검증에서 fail-loud하며 저장 payload를 current source 값으로 덮어쓰지 않는다.
- 공간·전력·물·연료·정비: capacity 수치 변경 0. authored support/Grand Project/facility buffer capacity source digest는 별도 OPEN이므로 sawmill 승격은 아직 금지한다.
- 위험·실패·회복 방식: item drift는 `prepared-output-item-revision-stale`, profile drift는 `prepared-output-migration-profile-stale`, recipe drift는 기존 `prepared-output-source-revision-stale`로 구분한다. schema v1 migration/fallback은 범위 밖이다.
- 사회·비가역 비용: gameplay 비용·사회 수치 변경 없음. 과거 세이브를 자동 변환하지 않고 current schema mismatch를 typed incompatibility로 취급한다.
- 기존 대안과의 장단점: raw YAML hash는 표시·직렬화 noise를 포함한다. 명시적 semantic digest는 필드 추가 시 schema 갱신이 필요하지만 gameplay 의미만 비교하고 collection 순서·표시명 false stale을 피한다.
- 지배 전략 방지 조건: 같은 ID kg/stack/price drift 승인 0, unsupported stateful feature의 generic 승격 0, stale profile 복원 0, mismatch 뒤 부분 publication 0을 요구한다.
- 실행 경로: `ResourceItemDefinitionSO → ResourceItemSemanticDigest → component profile → prepared line fingerprint`, `ProductionPreparedOutputMigrationScope → migrationProfileDigest → ProductionBillStateCodec restore gate`.
- 저장 권위와 실행 명령: ScriptableObject와 immutable positive profile이 current 권위이고 resolved batch의 schema v2 digest는 생성 당시 증거다. digest mismatch를 자동 수선하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: reviewed resource items `5`, live migration recipes `4`, registry profiles `4`, component runtime collection `0`인 definition-only family만 포함한다.
- 검증 매트릭스와 보고서 위치: `ResourceItemSemanticDigestDebugScenarios`, `ProductionRecipeSemanticDigestDebugScenarios`, component codec, migration profile, prepared contract/restore/resume/routing, `ProductionEconomyDebugScenarios` PASS; Unity Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: item/component/migration source binding과 schema v2만 닫혔다. capacity source digest, sawmill migration, lifecycle fault, 전수 kg/EWU·가격·6인망 전에는 Batch A/B 또는 전체 밸런스 완료가 아니다.

### balance:v27:prepared-output-capacity-source-and-sawmill-focused-v1

- 시대/역할: current prepared-output 5개 레시피의 물리 출력 버퍼를 도달 가능한 최대 branch와 exact source revision으로 결정하는 공통 저장·복원 경계다.
- Before: prepared batch schema v2에는 item·recipe·migration 의미만 있었고, support/facility/destination/cycle/projected grams가 바뀌어도 저장된 WIP가 이전 용량 근거로 reserve/publication될 수 있었다. sawmill은 recipe/item semantic ratchet만 있고 migration scope 밖이었다.
- After: schema v3가 `capacitySourceDigest`, output-buffer cycle, projected portfolio grams, required minimum grams를 저장한다. digest는 mass authority, recipe/item/component/migration digest, authored support/Grand Project factor, facility definition/instance/position, destination과 exact grams를 묶는다. `recipe:sawmill-lumber`와 `material:lumber`는 positive exact profile/definition-only codec에 승격했다.
- 물리 BOM·입력·출력: authored BOM과 output quantity 변경 0. sawmill은 기존 `resource:log ×2 → material:lumber ×3`, lumber `1,200g/unit`을 그대로 사용해 exact batch `3,600g`을 만든다.
- 직접 작업량과 계산 근거: sawmill Direct WU와 cycle time 변경 0. P03의 physical output buffer cycle `4`를 적용해 required minimum을 `3,600g × 4 = 14,400g`으로 산정한다. P17은 reachable dog-food branch `1,050g × 4 = 4,200g`을 유지한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 이번 단계는 후속 재생성의 물리 output/capacity 입력을 stale source와 underprojection에서 보호한다.
- 시간·확률·재시도: sawmill output은 probability `1`이며 completion-time resolved vector를 재굴림하지 않는다. retry/restore는 저장된 source와 current projector를 exact 비교하고 mismatch면 publication 전에 fail-loud한다.
- 공간·전력·물·연료·정비: 시설 footprint·전력·용수·정비 수치 변경 0. P03 output capacity는 reachable portfolio maximum으로 고정한다. 4-cycle를 넘는 전수 물류 Critical과 p95 clearance는 후속 OPEN이다.
- 위험·실패·회복 방식: production-owner request는 lowercase SHA-256 digest와 positive minimum을 요구한다. profile이 minimum보다 작거나 facility identity/source가 변하면 typed failure로 거부하며 durable batch를 current 값으로 덮어쓰지 않는다.
- 사회·비가역 비용: 신규 사회 비용·영구 선택 변경 없음. 과거 schema v1/v2 save migration은 범위 밖이며 current-format mismatch는 명시적 restore failure다.
- 기존 대안과의 장단점: current bill 기준 capacity는 작고 단순하지만 recipe 전환 시 output-space 실패를 만든다. 최대 branch source는 보수적 공간을 요구하지만 시설별 2~4 cycle 상한과 digest로 감사 가능하다.
- 지배 전략 방지 조건: 작은 bill로 capacity를 축소하거나 stale reservation을 재사용할 수 없어야 한다. source mismatch 뒤 partial profile/claim/publication mutation은 0이어야 한다.
- 실행 경로: `ProductionOutputBufferCapacityProjector → ProductionOutputBufferCapacitySourceGuard → ProductionPreparedOutputExecutionAdapter → FacilityBufferMassAdmissionService → planned publication/restore join/routing authority`.
- 저장 권위와 실행 명령: ScriptableObject recipe/item/facility/support와 mass authority가 current source다. prepared batch v3는 생성 당시 증거이며 Authority를 대체하지 않는다. digest mismatch는 재계산 승인이나 legacy fallback이 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: prepared recipes `5`, profile registry `5`, authored supports `28`, P17 `4,200g`, P03 `14,400g`, sawmill profile SHA `aff2ab2651af8d28bc86764c0edd151e22b1b7b91e6cc2bf20feea19aeb128fb`, registry SHA `1c8fd366e5a5c8761c3cf8119afde3d758344025c952f48a29e7c58a2ecce218`를 기록한다. 355-recipe 전수 migration은 OPEN이다.
- 검증 매트릭스와 보고서 위치: fresh Unity compile 뒤 maximum projector, migration profile, schema-v3 contract/tamper, restore join, admission minimum/fingerprint, isolated planned publication, recipe/item/component digest, resume/routing/destination, Production Economy가 PASS했고 Console Warning/Error `0/0`이었다.
- 현재 밸런스 상태: capacity-source focused closure와 sawmill source 승격만 닫혔다. sawmill real-adapter E2E/정상 부트 PlayMode, 전체 recipe/custom handler, lifecycle/fault, EWU·가격·6인 생존망 전에는 Batch A/B 또는 전체 밸런스 완료가 아니다.

### balance:v27:sawmill-real-adapter-focused-e2e-v1

- 시대/역할: 임업 제재 단계의 `recipe:sawmill-lumber`가 generic prepared-output 실제 어댑터를 통해 물리 FacilityOutputBuffer와 durable routing까지 exact하게 이어지는 focused 실행 증거다.
- Before: profile/codec/projector는 sawmill을 검증했지만 실제 `ProductionPreparedOutputExecutionAdapter` 생성·reserve·publication·ack 경로를 sawmill source로 관통한 fixture가 없었다.
- After: 실제 P03 BuildingSO, sawmill recipe/lumber definition, production bridge, Items repository, gram admission, planned publication, routing authority와 real adapter를 조립해 한 cycle을 Completed까지 실행한다.
- 물리 BOM·입력·출력: 기존 `resource:log ×2` WIP 입력 질량을 기록하고 `material:lumber ×3`, unit `1,200g`, total `3,600g`을 한 물리 stack으로 출판한다. BOM·quantity·kg 변경 0.
- 직접 작업량과 계산 근거: Direct WU 변경 0. 이 fixture는 completion boundary의 output transaction만 실행하며 P03 reachable maximum `3,600g × 4 = 14,400g`을 current source에서 재검증한다.
- EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 실제 물리 output grams와 buffer authority가 후속 원장 재생성의 입력으로 일치하는지만 증명한다.
- 시간·확률·재시도: output probability `1`, cycle sequence `1`의 canonical keyed outcome이다. Completed replay와 전체 save graph 재조립은 후속 OPEN이며 이번 증거로 대체하지 않는다.
- 공간·전력·물·연료·정비: 시설 footprint·utility 변경 0. exact profile `14,400g`, publication 후 reserved mass `0g`, physical occupancy `3,600g`을 검증한다.
- 위험·실패·회복 방식: canonical 64-hex digest만 바꾼 payload와 산술적으로 일관된 `cycle=3/projected=required=10,800g` payload 모두 restore authority publication 전에 `prepared-output-capacity-source-stale`로 거부한다.
- 사회·비가역 비용: gameplay 선택·영구 비용·content 변경 없음. fixture는 SO를 수정하거나 scene을 저장하지 않는다.
- 기존 대안과의 장단점: 수동 batch fixture는 저렴하지만 실제 recipe/component/capacity 연결 누락을 숨길 수 있다. real adapter fixture는 조립 비용이 크지만 production과 Items 경계를 함께 검증한다.
- 지배 전략 방지 조건: 작은 cycle 값으로 buffer를 축소하거나 stale source로 기존 physical stack/claim/profile을 바꾸지 못해야 한다. 실패 전후 authority snapshot은 byte-semantic하게 같아야 한다.
- 실행 경로: `P03 + recipe:sawmill-lumber → ProductionAssemblyBridgeAdapter → ProductionPreparedOutputExecutionAdapter → FacilityBufferMassAdmissionService → FacilityBufferPlannedOutputPublicationService → ProductionPreparedOutputRoutingAuthority`.
- 저장 권위와 실행 명령: prepared batch schema v3를 `JsonUtility`로 round-trip한 뒤 current source로 destination authority를 복원한다. 전체 Production/Physical/Routing persistence participant 재조립은 별도 OPEN이다.
- 자동 감사 ID와 전수 목록 포함 여부: migrated recipes `5` 중 sawmill `1`에 대한 real adapter focused 증거이며 355개 전수 또는 custom handler 완료가 아니다.
- 검증 매트릭스와 보고서 위치: `ProductionEconomyDebugScenarios.ValidateSawmillPreparedOutputRealAdapter`, full Production Economy suite, fresh runtime/editor compile, Unity Console Warning/Error `0/0`.
- 현재 밸런스 상태: sawmill focused adapter output closure만 닫혔다. full current-format save rehydration, 정상 부트 AIHaul, cancel/Downed/mid-haul restore, 전체 family/custom output, EWU·가격·6인망 전에는 sawmill live 또는 Batch A 완료가 아니다.

### balance:v27:prepared-output-workonly-family-expansion-v1

- 시대/역할: 초기·중기 생산의 숯가마, 제분소, 제강로, 방부 처리 목재대 whole-workstation definition-only 출력을 exact physical FacilityOutputBuffer 경계에 연결한다.
- Before: positive migration은 feedbench 4개와 sawmill 1개뿐이었다. charcoal·flour·starch·steel ingot·treated lumber는 legacy output executor에 남았고, 최초 후보는 같은 mill의 reachable `recipe:malt`를 빠뜨려 최대 용량을 `2,400g`으로 과소 계산할 위험이 있었다.
- After: `recipe:charcoal`, `recipe:malt`, `recipe:milling-flour`, `recipe:starch`, `recipe:steel-ingot`, `recipe:treated-lumber`를 exact profile과 definition-only component codec에 승격했다. 같은 네 workstation의 reachable legacy recipe는 0이다.
- 물리 BOM·입력·출력: authored BOM·수량·unit grams 변경 0. exact batch는 charcoal `2×450=900g`, malt `2×350=700g`, flour `2×300=600g`, starch `2×250=500g`, steel ingot `1×850=850g`, treated lumber `2×1,150=2,300g`이다.
- 직접 작업량과 계산 근거: Direct WU·cycle time 변경 0. 이번 slice는 existing authored output topology와 kg를 immutable digest/profile로 고정한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 물리 output과 buffer maximum을 닫아 후속 kg-aware 물류·EWU·가격 재생성의 입력을 안정화했다.
- 시간·확률·재시도: 여섯 레시피는 모두 WorkOnly, 단일 Main, probability 1이다. output roll·WIP retry 의미는 변경하지 않으며 current profile 밖 topology drift는 fail-loud한다.
- 공간·전력·물·연료·정비: footprint·utility 수치 변경 0. P01/P03/P04/P08/RF16 BuildingSO에 physical output cycle `4`를 명시하고 maximum branch로 charcoal `3,600g`, mill `2,800g`, steelworks `3,400g`, treated lumber `9,200g`, sawmill `14,400g`을 산정한다.
- 위험·실패·회복 방식: mill은 `support:fine-sieve`를 제공하는 WS01이 reachable하므로 malt를 제외할 수 없다. recipe family drift, legacy bypass, item/recipe/profile digest drift, underprojected grams를 focused suite에서 거부한다.
- 사회·비가역 비용: 신규 gameplay 비용·선택·영구 상태 변경 없음. 과거 세이브 migration은 범위 밖이다.
- 기존 대안과의 장단점: 일부 recipe만 이관하면 작은 diff지만 같은 destination의 legacy bypass와 maximum underprojection이 남는다. whole-workstation 이관은 한 시설의 실행·용량 분모를 원자적으로 닫는다.
- 지배 전략 방지 조건: support 유무나 현재 bill을 이유로 buffer를 축소하지 않으며, smaller flour batch를 mill maximum으로 선택하거나 malt를 legacy route로 우회할 수 없어야 한다.
- 실행 경로: `ProductionPreparedOutputMigrationScope → ProductionPreparedOutputComponentCodec → ProductionOutputBufferCapacityProjector → ProductionPreparedOutputExecutionAdapter → FacilityBuffer gram admission/publication/routing`.
- 저장 권위와 실행 명령: recipe/item/building ScriptableObject, immutable profile registry와 schema-v3 prepared batch가 권위다. C# initializer/fallback은 authored cycle 증거로 세지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: current prepared recipes `11`, new whole-workstation recipes `6`, reviewed definition-only items `11`, authored support `28`, 같은 네 workstation legacy recipes `0`을 기록한다.
- 검증 매트릭스와 보고서 위치: component codec, migration profile/registry, recipe/item semantic digest, maximum capacity source, prepared contract/restore join, FacilityBuffer admission/publication, full Production Economy가 fresh Unity assemblies에서 PASS했다. 다섯 BuildingSO의 두 번째 import byte diff는 `0`이다.
- 현재 밸런스 상태: 여섯 definition-only recipe family의 source/profile/capacity closure만 닫혔다. full aggregate restore, 정상 부트 AIHaul/fault, stateful/custom output, lifecycle, EWU·가격·6인망 전에는 Batch A 또는 전체 밸런스 완료가 아니다.

### balance:v27:sawmill-full-current-format-aggregate-restore-v1

- 시대/역할: sawmill completed output의 Physical Items, Production Bills, prepared-output Routing 세 저장 권위를 새 aggregate에 원자 복원하는 current-format 경계다.
- Before: focused adapter와 개별 restore join은 통과했지만 registry preflight가 Physical candidate publication 전에 Production/Routing cross-section join을 요구해 정상 full restore도 차단했다.
- After: preflight는 section 자체 current payload를 검증하고 cross-section join은 dependency-ordered detached staging에서 수행한다. public V17 source bill을 실행·capture한 뒤 새 aggregate에 `RestoreAll`한다.
- 물리 BOM·입력·출력: BOM·quantity·kg 변경 0. lumber `3×1,200g=3,600g`, output capacity `14,400g`과 한 physical stack을 exact 복원한다.
- 직접 작업량과 계산 근거: WU·cycle time 변경 0. completion 이후 저장/복원 경계만 검증한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 복원 뒤 physical/routing grams가 후속 원장 입력과 동일함을 보장한다.
- 시간·확률·재시도: probability 1의 저장된 Completed 결과를 재실행해 추가 stack 0을 증명한다. outcome 재굴림 없음.
- 공간·전력·물·연료·정비: 시설 수치 변경 0. 새 aggregate에도 P03 source-bound 4-cycle capacity가 동일하다.
- 위험·실패·회복 방식: Physical→Production→Routing 후보는 모두 live publication 전 detached stage에서 join한다. preflight에서 candidate 부재를 완화하지 않고 join 시점만 dependency 이후로 교정한다.
- 사회·비가역 비용: gameplay 선택·영구 비용 변경 없음. 과거 save migration은 범위 밖이다.
- 기존 대안과의 장단점: internal codec 직접 복원은 간단하지만 실제 registry lifecycle을 우회한다. public section restore는 조립 비용이 크지만 production save와 동일한 stage/participant 순서를 증명한다.
- 지배 전략 방지 조건: restore/replay에 의한 output 복제 0, source digest 재작성 0, routing mass 분리 0, 부분 aggregate publication 0을 요구한다.
- 실행 경로: `PhysicalItemsSaveSection → ProductionBillsSaveSection → ProductionPreparedOutputRoutingSaveSection → DungeonSaveSectionRegistry.RestoreAll`.
- 저장 권위와 실행 명령: V17 Production, current Physical Items, Routing save DTO와 새 aggregate root가 권위다. fixture JSON은 증거일 뿐 Authority가 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: sawmill completed-unrouted 1 cycle, physical stack 1, route line 1, grams `3,600`, capacity `14,400`을 포함한다.
- 검증 매트릭스와 보고서 위치: `ProductionPreparedOutputFullPersistenceDebugScenarios`, restore join, routing authority, full Production Economy, fresh compile, Console Warning/Error `0/0` PASS.
- 현재 밸런스 상태: completed-unrouted aggregate restore만 닫혔다. routed outbox, 정상 부트 AIHaul/cancel/Downed/mid-haul restore, lifecycle mutation fence, EWU·가격·6인망 전에는 sawmill live 또는 전체 밸런스 완료가 아니다.

### balance:v27:production-output-lifecycle-empty-mutation-fence-v1

- 시대/역할: 모든 시대의 production-capable 시설이 철거·이전·합성·진화될 때 출력 목적지의 논리·물리 소유권을 고아로 만들지 않는 수명주기 경계다.
- Before: generic bill 외 combat equipment/apparel 주문, reserved/origin physical, routing, exact-route outbox, carried/recovery가 서로 다른 권위에 있어 local buffer가 비었다는 이유만으로 시설을 제거할 수 있었다. 직접 demolition은 grid 제거 결과도 무시했다.
- After: `production-output:{facilityId}`를 canonical ID로 만들고 다섯 contributor의 immutable snapshot을 stable ID 순으로 결합한다. 직접 demolition은 mutation epoch와 exact fingerprint를 획득하고 완전 공백일 때만 output authority를 revoke한 뒤 grid를 제거한다. 실패하면 exact positive profile과 occupancy를 복구한다.
- 물리 BOM·입력·출력: authored BOM, item quantity, unit grams, recipe output을 변경하지 않았다. lifecycle snapshot은 이미 존재하는 물리 stack·reservation·routing·custody를 읽기만 한다.
- 직접 작업량과 계산 근거: Direct WU·cycle time 변경 0. epoch 중 bill 추가·작업 시작·output 실행·passive progress만 fail-closed해 제거 transaction과 새 생산의 race를 막는다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 고아 output이나 중복 회수로 경제 가치가 생성·삭제되는 경계를 차단하는 선행 구조다.
- 시간·확률·재시도: prepared output의 확률 결과와 WIP는 재굴림하지 않는다. candidate commit 직전 exact lifecycle fingerprint가 달라지면 revoke/world mutation 없이 stale로 거부한다.
- 공간·전력·물·연료·정비: footprint·utility·capacity authored 수치 변경 0. rollback은 철거 전 positive output capacity/profile과 grid occupancy를 복원한다.
- 위험·실패·회복 방식: active bill/WIP/waiting/publication/physical/routing/outbox/carry/recovery가 하나라도 있으면 direct demolition을 거부한다. grid removal failure는 authority를 재게시하고 epoch를 clean abort한다.
- 사회·비가역 비용: 신규 비용·사회 수치·보상 변경 없음. 과거 세이브 migration을 추가하지 않았다.
- 기존 대안과의 장단점: `DestroySelf()` 말단 guard나 generic bill-only 검사는 너무 늦거나 불완전하다. contributor query와 pre-mutation fence는 권위가 분산돼도 exact 집계하지만 새 owner가 생기면 contributor 등록과 duplicate 검증이 필요하다.
- 지배 전략 방지 조건: committed output 삭제 0, profile revoke 뒤 grid 잔존 0, 철거 실패 뒤 무상 capacity 소실 0, epoch 중 신규 cycle/reservation 0을 요구한다.
- 실행 경로: `GridBuildingPlacementService.TryDestroyBuilding → ProductionFacilityMutationFence.TryPrepare → lifecycle Capture → TryCommitAuthorityRevoke → grid.RemoveOccupant → TryComplete`; relocation/synthesis/evolution은 retarget 전까지 `TryRequireNoAuthority`로 첫 mutation 전에 거부한다.
- 저장 권위와 실행 명령: runtime bill/WIP, FacilityBuffer admission/occupancy, routing/outbox와 Physical Items repository가 gameplay 권위다. save DTO는 lifecycle query 입력으로 사용하지 않는다. epoch는 transient transaction 권위이며 저장하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:production-output-lifecycle-empty-mutation-fence-v1`; generic production, combat equipment, apparel, capacity/routing/outbox와 physical custody contributor를 포함한다.
- 검증 매트릭스와 보고서 위치: `ProductionOutputDestinationLifecycleDebugScenarios`, output destination authority, 전체 Production Economy, synthesis/evolution focused scenarios PASS. stale candidate·duplicate contributor·grid removal rollback·no-authority identity mutation을 포함하고 Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: 공통 read query와 direct empty-only demolition만 닫혔다. structural/cover destructive loss, active relocation/evolution/synthesis retarget, repository PlayMode, EWU·가격·6인망은 OPEN이므로 Batch B 또는 전체 밸런스 완료가 아니다.

### balance:v27:empty-structural-loss-and-world-replacement-retirement-v1

- 시대/역할: 모든 시대의 벽·문·방벽·엄폐물이 lethal damage로 사라지는 경로와 current-format world restore가 같은 facility ID의 출력 권위를 훼손하지 않게 하는 topology 수명주기다.
- Before: structural integrity와 cover durability가 HP 0에서 직접 `DestroySelf()`를 호출해 production mutation fence를 우회했다. world restore도 구 object에 gameplay destruction event를 발행해 새 same-ID aggregate의 subscriber authority를 제거할 위험이 있었다.
- After: structural/cover lethal damage는 `BuildingDestructiveLossRuntime`의 strict-empty candidate를 사용한다. revoke, registered-layer grid removal, visual removal, epoch complete 뒤에만 object를 제거한다. world replacement는 구 object를 `RetireForWorldReplacement()`로 제거해 destruction event를 발행하지 않는다.
- 물리 BOM·입력·출력: authored BOM, kg, output quantity 변경 0. live production output owner가 하나라도 있으면 전투 제거 전 physical mutation 0으로 차단한다.
- 직접 작업량과 계산 근거: 건설·수리·전투 WU 변경 0. 전투 제거는 player demolition의 `ApplyDestroySuccess`를 호출하지 않으므로 철거 작업량이나 salvage를 생성하지 않는다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 전투 파괴를 통한 output 삭제·철거 환급·same-ID restore revoke를 차단하는 경제 무결성 선행 경계다.
- 시간·확률·재시도: lethal 판정은 현재 HP와 exact damage를 사용한다. candidate 차단·stale·grid 실패에서는 HP를 적용하지 않으며 retry가 두 번째 revoke나 destruction event를 만들지 않는다.
- 공간·전력·물·연료·정비: footprint와 utility 수치 변경 0. 실제 registered layer가 Construction인지 authored placement layer인지 live grid에서 판별하고 같은 위치·movement 연결로 제거한다.
- 위험·실패·회복 방식: grid/visual/complete 실패는 grid·visual과 output authority를 복원하고 epoch를 종료한다. destruction subscriber 실패는 이미 commit된 world removal을 되살리지 않고 typed `CommittedWithNotificationFailure`로 분리한다.
- 사회·비가역 비용: 사망·기분·세력·보상 수치 변경 없음. combat destruction은 demolition refund/salvage를 발생시키지 않는다.
- 기존 대안과의 장단점: HP 0 뒤 말단 `DestroySelf()` guard는 rollback이 불가능하다. pre-damage strict-empty transaction은 현 authored wall/cover를 안전하게 제거하지만 active WIP를 가진 혼합 production 시설은 후속 durable destructive-release coordinator 전까지 fail-closed한다.
- 지배 전략 방지 조건: lethal damage 철거 환급 0, blocked damage HP 손실 0, grid 실패 뒤 authority 손실 0, HP 0 cover 재등록 0, world replacement destruction subscriber 호출 0을 요구한다.
- 실행 경로: `BuildingStructuralIntegrityRuntime/CombatCoverDurabilityRegistry → BuildingDestructiveLossRuntime.TryPrepare → ProductionFacilityMutationFence → TryCommit → grid/visual removal → complete → DestroySelf`; restore는 `ModularFacilityWorldSaveService → RetireForWorldReplacement`다.
- 저장 권위와 실행 명령: structural/cover component HP와 modular facility current-format candidate가 저장 권위다. mutation candidate/epoch는 transient이며 save DTO를 gameplay lifecycle query로 사용하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:empty-structural-loss-and-world-replacement-retirement-v1`; structural integrity, combat cover, same-ID modular world replacement를 포함한다. non-empty production destructive release는 제외하고 OPEN으로 기록한다.
- 검증 매트릭스와 보고서 위치: lifecycle focused scenario에서 structural·cover success, blocked HP/world no-mutation, grid failure abort, zero-HP restore를 검증했다. Modular Facility save/load report는 replacement destruction events `0`, 두 번 동일 SHA-256 `EA0C49DFEC716CC1CE19817ADEC366714465B9D5ED40AF355C6D14294281DE40`; Production/output/synthesis/evolution/invasion focused 통합 PASS, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: drained structural/cover destructive loss와 aggregate retirement만 닫혔다. active production contributor별 destructive release, 실제 침입 PlayMode, save/restore lifecycle fingerprint, EWU·가격·6인망은 OPEN이므로 Batch B 또는 전체 밸런스 완료가 아니다.

### balance:v27:stateful-output-capability-owner-envelope-v1

- 시대/역할: 모든 시대의 stateful custom output 중 Apparel Work Order와 CertifiedSeed가 생산 당시 구현·codec 의미를 current-format owner에 동결하는 공용 확장 경계다.
- Before: 두 domain owner는 item/component 결과만 저장하고 해당 결과를 만든 capability ID/version과 codec ID/version을 저장하지 않아 registry 진화 뒤 pending output을 다른 의미로 재해석할 수 있었다.
- After: 공용 `ProductionOutputCapabilitySaveData`가 canonical output line, exact item, capability/version, codec/version과 descriptor fingerprint를 한 envelope로 저장한다. Apparel은 physical adoption에서, CertifiedSeed는 exact input commit과 seed-lot 확정 뒤 동결한다.
- 물리 BOM·입력·출력: authored BOM·수량·item gram·output quantity 변경 0. Apparel의 기존 exact material Transfer와 unique output, CertifiedSeed의 seed+kit Transfer와 seed-lot output을 그대로 보존한다.
- 직접 작업량과 계산 근거: Direct WU·cycle time·quality roll·certification 효과 변경 0. 이번 slice는 output provenance와 restore 검증만 변경한다.
- EWU와 목표 회수 기간: EWU·가격·ROI·판매 회수율 변경 0. capability drift로 stateful output이 definition-only output으로 바뀌어 가치가 삭제되는 경계를 막는다.
- 시간·확률·재시도: Apparel quality attempt와 CertifiedSeed certified lot은 기존 결정 시점을 유지한다. replay는 저장 descriptor를 exact 검증하고 결과를 재굴림하지 않는다.
- 공간·전력·물·연료·정비: footprint·facility capacity·utility·maintenance 수치 변경 0. CertifiedSeed common gram output reservation은 아직 OPEN이다.
- 위험·실패·회복 방식: unknown capability, version/codec/item/line/fingerprint drift는 physical publication 또는 live restore candidate publication 전에 fail-loud한다. 다른 handler·standard output fallback은 없다.
- 사회·비가역 비용: 기분·품질·연구·보상·영구 선택 변경 0. 과거 save migration은 범위 밖이며 current-format 누락은 실패한다.
- 기존 대안과의 장단점: item ID만 재검색하면 필드는 적지만 unrelated capability 추가나 overlap에서 의미가 바뀐다. per-output envelope는 저장량이 늘지만 unrelated registry 변화에는 안정적이고 새 capability가 공용 schema를 재사용한다.
- 지배 전략 방지 조건: 다른 apparel/seed item의 유효 descriptor 치환, codec downgrade, 저장 fingerprint 재작성, stateful→definition-only fallback, restore 뒤 output 재생성을 모두 금지한다.
- 실행 경로: `ApparelWorkOrderRuntime → ApparelPhysicalTransaction → declared registry → gram admission/publication`; `CertifiedSeedRuntime → CropPhysicalTransactionOutbox → declared registry → IPhysicalSeedLotGateway`.
- 저장 권위와 실행 명령: CharacterEnvironment V10/Apparel terminal V2/drain V2와 CertifiedSeed V2가 owner envelope를 저장한다. registry 구현체나 save DTO를 gameplay 질량 조회 권위로 사용하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:stateful-output-capability-owner-envelope-v1`; domain metadata 4종 중 Apparel owner와 CertifiedSeed owner 2종을 포함한다. Combat equipment/ammunition owner와 synthetic full-path는 제외해 OPEN으로 기록한다.
- 검증 매트릭스와 보고서 위치: Unity clean-cache compile, Apparel physical/terminal/registry suites, Crop physical transaction/CertifiedSeed restore drift fixture가 PASS했고 각 clean-console 최종 Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: owner provenance 2종만 닫혔다. CertifiedSeed/Combat common gram publication, Combat owner, AIHaul→kg warehouse full-path, EWU·가격·6인 생존망과 final seeds 전에는 Batch A 또는 전체 밸런스 완료가 아니다.

### balance:v27:combat-output-capability-owner-envelope-v1

- 시대/역할: 모든 시대의 Combat Equipment 제작에서 unique 장비와 generic 탄약 outcome을 생성 당시 capability·codec 의미에 결속하는 current-format provenance 경계다.
- Before: Combat craft order는 output item/quantity와 unique instance 또는 generic stack 상태만 저장했다. pending outcome은 registry 진화 뒤 다른 capability나 codec으로 재해석될 수 있었고, 첫 탄약 capability 초안은 실제 기본 화살이 없는 resource-only catalog를 조회했다.
- After: Combat Equipment V9 order가 outcome resolve 시점에 canonical output line, exact item, capability/version, codec/version과 descriptor fingerprint를 동결한다. 장비는 combat-equipment-state codec, 탄약은 통합 item catalog의 `AmmunitionItemFeature`와 combat-ammunition-state codec으로 검증한다.
- 물리 BOM·입력·출력: authored BOM·quantity·unit grams·장비/탄약 output 수량 변경 0. 기존 장비 1개와 arrow/bolt bundle 수량을 그대로 보존한다.
- 직접 작업량과 계산 근거: Direct WU·quality roll·inspiration·cycle time 변경 0. descriptor는 output item과 수량이 결정된 동일 전이에서 `attemptOutcomeResolved`보다 먼저 저장된다.
- EWU와 목표 회수 기간: EWU·가격·ROI·판매 회수율 변경 0. capability drift로 pending 장비/탄약의 물리 의미가 바뀌는 경계만 차단한다.
- 시간·확률·재시도: 저장된 quality/mythic/ammunition 결과는 재굴림하지 않는다. finalize·restore는 저장된 exact descriptor가 현재 registry와 일치할 때만 진행한다.
- 공간·전력·물·연료·정비: footprint·FacilityBuffer capacity·utility·maintenance 변경 0. Combat common gram reservation/publication은 아직 OPEN이다.
- 위험·실패·회복 방식: missing envelope, 장비/탄약 line 교차, item/capability/version/codec/fingerprint drift는 output mutation 또는 live restore publication 전에 fail-loud한다. 다른 handler나 standard-definition fallback은 없다.
- 사회·비가역 비용: 기분·품질·연구·보상·영구 선택 변경 0. 과거 save migration은 범위 밖이며 V9 필수 필드 누락은 current-format failure다.
- 기존 대안과의 장단점: resource-only catalog는 일부 신형 탄약을 다루지만 legacy `ammo:arrow`를 누락한다. 통합 item catalog와 feature 기반 판정은 새 탄약도 코어 ID 분기 없이 참여하며 전용 codec으로 상태 의미를 명시한다.
- 지배 전략 방지 조건: equipment descriptor를 ammunition으로 치환, 다른 output item 치환, codec downgrade, fingerprint 재작성, restore 뒤 outcome 재굴림을 모두 금지한다.
- 실행 경로: `CombatEquipmentCraftingRuntime.ResolveAttemptOutcome → CombatEquipmentPhysicalStateWriter → declared capability registry → CombatEquipmentCraftOutputOutbox`; direct outbox의 common publication 이관은 OPEN이다.
- 저장 권위와 실행 명령: Combat Equipment save section V9와 공용 capability registry가 provenance 권위다. item kg와 장비 상태는 기존 physical mass subject/query 권위를 유지한다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:combat-output-capability-owner-envelope-v1`; Combat Equipment Craft와 Combat Ammunition Craft capability 2종을 포함한다. common gram publication과 synthetic full-path는 제외해 OPEN으로 기록한다.
- 검증 매트릭스와 보고서 위치: Unity clean-cache compile, Combat craft transaction, strict progression/combat save, craft terminal authority, material equipment integration, declared capability registry가 PASS했다. 장비·탄약 exact descriptor와 탄약 codec-version drift 거부를 포함하며 final Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: Apparel·CertifiedSeed·Combat frozen owner provenance는 닫혔다. CertifiedSeed/Combat common gram publication, AIHaul→kg warehouse full-path canary, EWU·가격·6인 생존망과 final seeds 전에는 Batch A 또는 전체 밸런스 완료가 아니다.

### balance:v27:generic-prepared-output-capability-selection-v1

- 시대/역할: 전 시대의 definition-only 일반 생산 output이 recipe별 migration 목록 없이 동일한 prepared FacilityOutputBuffer 경로를 사용하는 구조 권위다.
- Before: 표준 prepared-output 진입은 11개 recipe ID registry에 묶였고, registry 밖 일반 레시피는 legacy `ResolveAll/ProduceOne`과 direct buffered-output commit을 사용할 수 있었다. `recipe:medical-vial`은 forge crafting 레시피인데 primary proficiency가 비어 semantic source digest를 만들 수 없었다.
- After: 각 physical output line의 frozen capability/version/codec/fingerprint가 모두 `standard-definition@1 + definition-only-codec@1`일 때 공통 prepared batch를 자동 선택한다. medical-vial primary proficiency는 같은 forge/work:craft 계열의 `proficiency:crafting`으로 교정했다.
- 물리 BOM·입력·출력: 모든 recipe의 기존 input/output item과 수량, medical-vial 철괴 1×900g→바이알 30×30g을 포함한 질량 계약 변경 0.
- 직접 작업량과 계산 근거: authored RequiredWork와 WU 배율 변경 0. 숙련도 누락만 교정해 기존 crafting performance projector가 레시피에 정상 적용되도록 했다.
- EWU와 목표 회수 기간: EWU·가격·회수 기간 변경 0. 전수 kg After와 handling EWU/가격 재생성은 후속 Batch H로 남긴다.
- 시간·확률·재시도: output 확률은 완료 시 한 번 결정해 prepared batch에 저장한다. recipe semantic digest와 capability profile digest가 restore에서 drift하면 publication 전에 실패한다.
- 공간·전력·물·연료·정비: facility footprint·utility·maintenance·authored capacity 변경 0. standard output은 common gram capacity projector와 planned publication 권위를 사용한다.
- 위험·실패·회복 방식: missing/unregistered/stateful capability는 definition-only codec으로 추측하지 않고 fail-closed한다. synthetic canary와 legacy direct fallback 도달불가 ratchet은 아직 OPEN이다.
- 사회·비가역 비용: crafting 숙련 캐릭터가 의료 바이알 생산 성능에 정상 반영된다. 신규 기분·관계·건강·연구·보상 수치 변경은 없다.
- 기존 대안과의 장단점: recipe ID allowlist는 작은 범위에서 명확하지만 새 콘텐츠마다 코어 수정이 필요하다. capability 선택은 새 definition-only 콘텐츠를 자동 수용하지만 stateful output이 명시적 codec 없이 일반 경로로 섞이는 것을 허용하지 않는다.
- 지배 전략 방지 조건: stale recipe digest로 결과 재해석 0, stateful component 누락으로 질량 축소 0, output line 병합 0, restore 재굴림·중복 publication 0을 요구한다.
- 실행 경로: `ProductionBillRuntime → ProductionPreparedOutputCapabilitySelection → ProductionPreparedOutputExecutionAdapter → FacilityBufferMassAdmissionService → FacilityBufferPlannedOutputPublicationService → exact routing/AIHaul`이며 마지막 synthetic actual path는 아직 검증 대기다.
- 저장 권위와 실행 명령: Production current-format prepared batch가 resolved result와 recipe/capability digest를 소유한다. legacy output authority와 prepared authority가 동시에 존재하면 restore를 거부한다. 과거 save migration은 제외한다.
- 자동 감사 ID와 전수 목록 포함 여부: `ProductionPreparedOutputMigrationProfileDebugScenarios`, `ProductionPreparedOutputComponentCodecDebugScenarios`, `ProductionOutputHandlerRegistryDebugScenarios`, `ProductionPreparedOutputContractDebugScenarios`; physical-output recipe 351개 profile을 포함한다.
- 검증 매트릭스와 보고서 위치: Unity current-source compile 후 위 4개 suite와 실제 sawmill prepared adapter focused runner가 한 묶음으로 PASS, Console Warning/Error `0/0`. synthetic production→AIHaul→warehouse→RestoreAll canary와 owner-manifest 최종 행은 대기다.
- 현재 밸런스 상태: capability 기반 일반 생산 선택과 medical-vial authoring 결함 교정은 structural/focused PASS. Batch A, 전수 kg, EWU·가격, 6인 생존망, Floor Clutter/paired run과 final seeds 전에는 밸런스 완료가 아니다.

### balance:v27:certified-seed-common-output-lifecycle-v1

- 시대/역할: 재배 품종 인증 작업의 exact seed+kit WIP가 인증 종자 FacilityOutputBuffer stack으로 전환되고 저장·복원·시설 파괴에서도 질량과 소유권을 보존하는 공용 생산 경계다.
- Before: CertifiedSeed는 frozen capability를 저장했지만 output은 domain 전용 직접 생성 경로였고 common gram admission·planned publication을 거치지 않았다. committed output을 저장 후 복원하면 transient admission token이 사라져 재개가 멈출 수 있었고 모든 active phase가 철거 preflight를 무기한 차단할 수 있었다.
- After: common domain-output service가 exact component/quantity/gram, capacity source, token, batch/outcome, physical stack과 acknowledgement를 소유한다. pending restore marker는 CertifiedSeed section stage에서 exact 채택·acknowledge되고 durable restored phase는 input receipt만 끝낸다. 시설 파괴는 Planned 배송 해제, InputCommitted typed loss, OutputPublished facility-independent acknowledgement로 종결한다.
- 물리 BOM·입력·출력: seed lot 1개와 `supply:certified-seed-kit` 1개 입력, 동일 crop certified seed lot 1개 출력은 변경하지 않았다. input quantity/grams와 terminal facility-loss quantity/grams를 exact 일치시킨다.
- 직접 작업량과 계산 근거: 인증 작업 WU·cycle time·작업자 생산성 변경 0. 이번 변경은 publication·restore·terminal transaction 경계만 교정한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 재시도 output 복제, restore 후 WIP 삭제, 시설 파괴 receipt 유실을 차단해 기존 경제 값을 보존한다.
- 시간·확률·재시도: pathogen 감소와 certified lot 결정 시점은 유지한다. output은 outcome fingerprint당 한 번만 생성되고 restore는 결과·질량·component를 재계산하거나 재굴림하지 않는다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·authored buffer 수치 변경 0. output은 current capacity projector의 positive gram profile과 exact destination을 요구하며 부족하면 WIP를 유지한다.
- 위험·실패·회복 방식: publication fault는 reservation/stack을 rollback하고, pending physical marker 또는 domain owner가 한쪽만 존재하면 restore 전체를 fail-loud한다. 시설 파괴 후 committed input은 `DestroyedWithFacilityLoss` receipt가 acknowledgement될 때까지 저장 가능하다.
- 사회·비가역 비용: 기분·연구·품질·보상 변경 없음. 과거 save migration은 제외하며 CertifiedSeed V4/domain-output schema V2보다 오래되거나 필수 provenance가 누락된 payload는 current-format 복원 대상이 아니다.
- 기존 대안과의 장단점: transient token을 저장 뒤 재사용하면 admission service의 restore 권위와 충돌한다. detached candidate adoption은 section staging이 늘지만 physical/domain batch를 exact-once로 결합하고 새 domain producer도 같은 join을 재사용한다.
- 지배 전략 방지 조건: restore output 복제 0, orphan marker/owner 0, acknowledgement 재생성 0, facility destruction input 환급·output 중복 0, terminal quantity/gram drift 0을 요구한다.
- 실행 경로: `CertifiedSeedRuntime → CropPhysicalTransactionOutbox → ProductionDomainOutputPublicationService → FacilityBufferMassAdmissionService → FacilityBufferPlannedOutputPublicationService`; restore는 `CertifiedSeedSaveSection → ProductionDomainOutputRestoreJoin`, destruction은 `ProductionFacilityDestructiveDrainStartPreflight`와 phase별 runtime terminal path다.
- 저장 권위와 실행 명령: CertifiedSeed V4가 order/input/capability/publication/acknowledged phase를 저장하고 Physical Items의 pending input 및 planned-output marker와 bidirectional join한다. runtime mass query는 save DTO를 입력으로 사용하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:certified-seed-common-output-lifecycle-v1`; CertifiedSeed output family 1개와 common domain publication/restore/lifecycle contract를 포함한다. Combat output과 synthetic AI full-path canary는 제외해 OPEN으로 둔다.
- 검증 매트릭스와 보고서 위치: Unity compile 후 domain publication, domain restore guard/save-section adoption, destructive preflight, crop physical transaction focused suites가 PASS했다. capacity wait·publication rollback·duplicate ack·owner/batch drift·restore→next tick·facility loss를 포함하며 final Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: CertifiedSeed common output 하위 경계만 닫혔다. Batch A는 Combat common publication과 synthetic full-path canary, 이후 B~H와 EWU·가격·6인 생존망·final seeds가 남아 있으므로 전체 밸런스 완료가 아니다.

### balance:v27:ruined-output-maximum-capacity-and-terminal-routing-v1

- 시대/역할: passive production의 실패 WIP를 recoverable waste와 declared loss로 종결하면서, 동일 frozen capability가 허용하는 최대 batch 질량으로 FacilityBuffer를 유지하는 공용 경계다.
- Before: ruined output은 실제 recoverable waste grams를 raw capacity 입력으로 사용했다. bill 종료 뒤 routing batch만 남으면 save-only projector가 단일 실제 batch 질량만 보아 authored 2~4 cycle profile을 축소할 수 있었다.
- After: WIP·process fluid provenance·frozen waste descriptor·disposition·maximum proof를 `ProductionRuinedOutputCapacityClaim`으로 결속한다. active bill과 terminal routing은 같은 proof-sized required minimum을 저장하고 current-format 복원에서 exact 검증한다.
- 물리 BOM·입력·출력: authored BOM·수량·unit grams 변경 0. 검증 fixture는 WIP `2,400g`과 clean water `600g`에서 wastewater `300g`을 분리해 plant rot `600g × 4 = 2,400g`과 declared loss `300g`을 보존한다.
- 직접 작업량과 계산 근거: Direct WU·cycle time 변경 0. buffer 계산은 capability maximum batch `2,400g × cycle 4 = 9,600g`과 current recipe portfolio 중 큰 값을 사용한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 재굴림·삭제·작은 실제 결과를 이용한 capacity 축소만 차단하며 kg-aware EWU/가격 재생성은 후속 Batch H다.
- 시간·확률·재시도: 실패 결과는 한 번 결정하고 저장한다. retry/restore는 stored descriptor를 declared 방식으로 재투영하며 WIP·fluid·descriptor·proof·claim drift 시 결과를 재굴림하지 않고 publication 전에 실패한다.
- 공간·전력·물·연료·정비: facility footprint·utility·authored cycle 값 변경 0. active/terminal 양쪽에서 동일 `9,600g` 최소 capacity를 유지한다.
- 위험·실패·회복 방식: proof tuple 부분 누락, normal batch의 가짜 proof, ruined batch의 proof 누락, actual mass가 maximum 초과, claim/proof digest 또는 maximum 1g drift를 fail-loud한다. 과거 schema migration은 범위 밖이다.
- 사회·비가역 비용: 기분·연구·보상·영구 선택 변경 없음. 폐기물과 wastewater의 기존 물리 disposition 의미를 유지한다.
- 기존 대안과의 장단점: 실제 output mass로 profile을 잡으면 구현은 단순하지만 작은 결과·bill retirement로 capacity가 축소된다. capability maximum claim은 저장 필드와 검증 비용이 늘지만 신규 동일 의미 출력이 공용 proof를 재사용한다.
- 지배 전략 방지 조건: 작은 ruined result로 buffer 축소 0, bill retirement 후 profile shrink 0, proof보다 무거운 output publication 0, restore-time claim 재작성 0을 요구한다.
- 실행 경로: `ProductionBillRecord/WIP/fluid provenance → ProductionRuinedOutputCapacityClaim → ProductionOutputBatchMaximumMassProof → ProductionOutputBufferCapacityProjector → prepared batch → FacilityBuffer → ProductionPreparedOutputRoutingAuthority → detached capacity projector`.
- 저장 권위와 실행 명령: Prepared Output V5, Production V19, Routing V6가 생성 당시 proof/claim/capacity를 저장한다. ScriptableObject recipe/item/facility와 current capability maximum registry가 현재 source 권위이며 저장 digest가 이를 대체하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:ruined-output-maximum-capacity-and-terminal-routing-v1`; real silage ruined fixture와 generic ruined active/terminal 경계를 포함한다. non-standard materializer와 synthetic full-path canary는 제외해 OPEN으로 둔다.
- 검증 매트릭스와 보고서 위치: fresh Unity compile, prepared contract, ruined execution, maximum-factor, routing authority/restore join, full persistence, destructive-drain participant/preflight, full Production Economy 비-PlayMode suite가 PASS했고 Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: ruined maximum-capacity 하위 경계만 닫혔다. 암묵 mixed-rot fallback과 등록형 materializer는 각각 후속 baseline record에서 닫혔다. portfolio WIP 상한, generic normal raw 제거, 전체 detached source와 asset-backed AIHaul canary 전에는 Batch A/B 또는 전체 밸런스 완료가 아니다.

### balance:v27:passive-spoilage-authority-fail-loud-v1

- 시대/역할: 모든 시대의 passive production이 실패·부패할 때 생성할 물리 부산물의 item identity를 recipe 작성 데이터로 명시하고, 신규 recipe도 catalog capture 단계에서 같은 계약에 자동 편입하는 공용 authoring 경계다.
- Before: `ProductionRecipeSO` 필드·getter·builder 기본 인수가 `waste:mixed-rot`를 암묵 대입해 누락된 PassiveBatch가 정상처럼 보일 수 있었다. 잘못된 ID는 실제 ruin 또는 capacity 계산까지 늦게 실패했고 미래 recipe가 별도 allowlist 검사를 요구했다.
- After: 기본값과 getter fallback을 empty로 바꾸고 raw authored ID를 보정 없이 보존한다. PassiveBatch는 non-empty canonical ID와 현재 item-catalog 존재를 요구하며 empty·noncanonical·orphan을 catalog construction에서 거부한다. WorkOnly은 empty를 허용하고 값이 있으면 canonical만 허용한다.
- 물리 BOM·입력·출력: 기존 355 recipe의 BOM·정상 출력·실패 출력 수량·item kg 변경 0. 전수 census의 PassiveBatch `8/8`은 이미 명시적 spoilage를 가지며 `plant-rot 7`, `animal-rot 1`, 누락·noncanonical·orphan `0`이다.
- 직접 작업량과 계산 근거: Direct WU·cycle time·인력·작업 종류 변경 0. 이번 변경은 누락 authoring을 런타임 기본값으로 채우지 않고 catalog 경계에서 거부하는 구조 계약이다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. spoilage item·수량·질량이 달라질 때 semantic digest와 후속 EWU 재생성이 명시적으로 stale되어 암묵 mixed-waste 가치 치환을 막는다.
- 시간·확률·재시도: passive duration·temperature·확률 결정 시점·저장된 ruined outcome은 변경하지 않는다. restore/retry는 `production-recipe-semantic@3`과 현재 raw spoilage authority가 일치해야 한다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·buffer cycle·capacity 수치 변경 0. 명시된 spoilage item만 ruined maximum-capacity claim의 입력이 된다.
- 위험·실패·회복 방식: Passive empty, whitespace·대소문자 보정이 필요한 ID, catalog에 없는 canonical ID는 physical mutation 전에 fail-loud한다. WorkOnly empty는 합법이며 의미 없는 기존 직렬화 필드는 fallback 권위로 읽지 않는다.
- 사회·비가역 비용: 기분·연구·보상·콘텐츠 수 변경 0. 잘못 작성된 신규 passive recipe가 빌드·실행 중 조용히 mixed rot로 바뀌는 대신 개발 단계에서 명확히 실패한다.
- 기존 대안과의 장단점: universal mixed-rot fallback은 authoring 비용이 작지만 질량·EWU·저장 category와 생산 의미를 숨긴다. 명시적 item 계약은 recipe 작성 시 한 필드를 요구하지만 이후 코어 분기나 allowlist 없이 신규 콘텐츠를 자동 검증한다.
- 지배 전략 방지 조건: 누락 spoilage의 무료 삭제·임의 mixed-rot 치환 0, noncanonical ID 자동 보정 0, orphan output 지연 실패 0, WorkOnly의 무의미 값이 실제 ruined output으로 승격되는 경우 0을 요구한다.
- 실행 경로: `ProductionRecipeSO raw authority → ProductionRecipeSemanticDigest@3 → ResourceEconomyContentCatalog passive validation → ProductionRuinedOutputCapacityClaim → capability maximum proof → FacilityBuffer publication`.
- 저장 권위와 실행 명령: ScriptableObject recipe와 item catalog가 현재 source 권위다. prepared/routing save digest는 생성 당시 증거이며 누락 spoilage를 복구하거나 현재 source를 대체하지 않는다. 과거 save migration은 범위 밖이다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:passive-spoilage-authority-fail-loud-v1`; 355 recipes, PassiveBatch 8, WorkOnly 347 census를 포함한다. WorkOnly 347 asset의 무의미 serialized field 정리는 비-P0 YAML cleanup으로 분리한다.
- 검증 매트릭스와 보고서 위치: 현재-source Unity compile 후 recipe semantic digest, prepared contract, ruined execution, maximum-factor, routing authority/restore join, destination lifecycle, full persistence, destructive-drain participant/preflight와 full Production Economy 11개 묶음이 PASS했다. Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: silent spoilage fallback 하위 경계는 닫혔고 등록형 prepared-output materializer도 후속 record에서 닫혔다. ruined portfolio WIP maximum, generic normal raw 제거, complete detached restore claim과 asset-backed full-path canary가 남아 Batch A~H는 `0/8`, weighted remaining은 `31~41%`다.

### balance:v27:prepared-output-materializer-registry-v1

- 시대/역할: 전 시대의 일반·폐기 생산 output이 상태 의미별 component materializer를 등록만으로 공용 prepared batch에 연결하는 확장 경계다.
- Before: common prepared adapter가 `StandardDefinition + DefinitionOnlyCodec` 값을 직접 비교해 component를 생성·복원했다. 새 stateful output family는 capability를 추가해도 coordinator와 restore 코드를 다시 수정해야 했다.
- After: capability participation marker와 deterministic materializer registry가 capability/version/codec을 1:1 exact join한다. normal·ruined·publication slice·restore가 동일 dispatch를 사용하며 비표준 synthetic capability도 코어 분기 없이 `PreparedBatch`를 통과한다.
- 물리 BOM·입력·출력: authored BOM, item identity, output quantity, unit grams와 total grams 변경 0. materializer는 frozen descriptor가 가리키는 item과 exact component projection만 반환한다.
- 직접 작업량과 계산 근거: Direct WU·cycle time·작업자 생산성 변경 0. 이번 변경은 출력 상태 materialization의 소유권과 확장 방식만 교정한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. materializer drift가 저장 결과를 다른 질량·가치로 재해석하는 경로를 publication 전에 차단한다.
- 시간·확률·재시도: completion-time 결과 결정과 저장된 roll은 유지한다. restore/retry는 저장 payload를 재굴림하지 않고 frozen capability의 현재 materializer로 exact 검증한다.
- 공간·전력·물·연료·정비: 시설 footprint·utility·buffer cycle·capacity 수치 변경 0. maximum-mass registry와 handler registry는 같은 participation contract snapshot을 공유한다.
- 위험·실패·회복 방식: materializer 누락·중복·version/codec drift, 비참여 capability 등록, prepared/per-line 이중 실행 권위, adapter의 Standard/DefinitionOnly 직접 분기는 fail-loud한다. unique instance ID가 있는 projection은 한 saved line에서 quantity 1만 허용한다.
- 사회·비가역 비용: 기분·연구·보상·콘텐츠 수 변경 0. 과거 save migration은 범위 밖이며 current-format payload만 검증한다.
- 기존 대안과의 장단점: coordinator switch/ID 분기는 처음엔 단순하지만 콘텐츠마다 코어 변경과 회귀 범위를 늘린다. 등록형 dispatch는 capability별 구현이 필요하지만 같은 의미 범주의 신규 콘텐츠는 코어 설계 변경 없이 편입된다.
- 지배 전략 방지 조건: payload를 다른 codec으로 해석, stateful output을 definition-only로 축소, normal/ruined 경로 간 materializer 불일치, restore 뒤 component·질량 재작성, 두 실행 owner의 중복 publication을 모두 0으로 요구한다.
- 실행 경로: `ProductionRecipeSO frozen descriptor → ProductionPreparedOutputCapabilitySelection → ProductionPreparedOutputMaterializerRegistry → ProductionPreparedOutputExecutionAdapter → FacilityBuffer planned publication`; restore는 DTO 구조 검증 뒤 같은 registry decode를 publication 전에 수행한다.
- 저장 권위와 실행 명령: Production prepared-output current format이 capability/version/codec/fingerprint와 canonical payload/fingerprint를 저장한다. materializer 인스턴스는 저장하지 않으며 composition root의 current registry가 실행 권위다. participation 의미 변경 시 capability contract version을 함께 올린다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:prepared-output-materializer-registry-v1`; current Standard participant와 synthetic non-standard participant, normal/ruined/restore dispatch 및 negative registry matrix를 포함한다. asset-backed AIHaul canary는 제외한다.
- 검증 매트릭스와 보고서 위치: current-source Unity compile, component codec/materializer registry, output handler registry, static bypass ratchet과 recipe/prepared/ruined/maximum/routing/lifecycle/full-persistence/destructive-drain/full Production Economy 11개 묶음이 PASS했다. Console Warning/Error는 `0/0`이다.
- 현재 밸런스 상태: 등록형 materializer 하위 경계만 닫혔다. facility portfolio WIP maximum, generic/restore raw capacity 5곳, complete detached source claim, asset-backed production→AIHaul→gram warehouse canary와 이후 Batch B~H가 남아 전체 체크포인트는 `0/8`, weighted remaining은 `31~41%`다.

### balance:v27:authored-output-ceiling-and-detached-capacity-guard-v1

- 시대/역할: 전 시대의 generic Main/Byproduct와 domain-produced FacilityBuffer output이 저장 데이터로 자기 용량을 확장하지 못하도록 current authoring과 detached facility 후보에 재결속하는 공용 복원 경계다.
- Before: raw public capacity API는 제거됐지만 generic normal claim은 저장된 line quantity로 새 proof를 만들 수 있었고, Domain owner는 capability proof만 재검증해 facility definition·position·workstation·cycle·catalog source drift를 놓칠 수 있었다.
- After: generic Main/Byproduct는 current recipe의 canonical line ID·role·item과 maximum output factor를 넘는 수량을 claim 생성 전에 거부한다. 공용 detached guard는 restore-world candidate에서 exact facility 한 개를 찾고 current proof로 capacity source를 재투영해 saved digest와 required minimum을 tolerance 없이 비교한다. CertifiedSeed와 Combat은 physical `AdoptPending` 전에 이 검증을 호출한다.
- 물리 BOM·입력·출력: authored BOM·item·output quantity·unit grams 변경 0. 검증은 저장 수량을 늘리지 않고 current authored maximum을 초과하는 forged output만 거부한다.
- 직접 작업량과 계산 근거: Direct WU·cycle time·생산성 변경 0. upper bound는 기존 maximum output factor와 capability maximum-mass registry를 재사용한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 저장값 동시 위조로 더 큰 물리 출력·저장 용량을 합법화하는 경제 우회만 차단한다.
- 시간·확률·재시도: 기존 completion-time RNG와 frozen 결과는 유지한다. restore는 결과를 재굴림하지 않고 current authored ceiling과 detached source만 대조한다.
- 공간·전력·물·연료·정비: facility authored cycle·위치·workstation·process-fluid source를 digest에 그대로 포함하며 수치 변경 0. live-world fallback과 epsilon은 없다.
- 위험·실패·회복 방식: missing/duplicate/destroyed detached facility, facility ID·outcome drift, proof digest/max drift, capacity digest 또는 required minimum 1g drift는 ownership adoption과 live runtime publication 전에 fail-loud한다.
- 사회·비가역 비용: 기분·연구·품질·보상·영구 선택 변경 0. 과거 save migration은 제외한다.
- 기존 대안과의 장단점: owner별 facility snapshot DTO를 추가하면 중복 권위와 미래 저장 필드가 늘어난다. 공용 guard와 owner adapter 방식은 기존 저장 provenance를 재사용하지만 각 capability owner가 자신의 descriptor·maximum quantity·활성 phase를 정확히 제공해야 한다.
- 지배 전략 방지 조건: quantity·grams·proof·claim·capacity 동시 상향, 다른 valid facility로 owner 재지정, live facility fallback, duplicate candidate 선택, raw exact-mass self-sizing을 모두 금지한다. ReturnedPackaging은 별도 tare 회수 계약으로 검증한다.
- 실행 경로: generic은 `ProductionPreparedOutputCapacityClaim → current recipe maximum factor`; domain은 `CertifiedSeedSaveSection/CombatEquipmentSaveSection detached build → maximum registry → ProductionOutputDetachedFacilityCapacityRestoreGuard → restore-world candidate facility → capacity projector → physical AdoptPending`이다.
- 저장 권위와 실행 명령: 기존 Prepared Output V6/Production V20/Domain publication V5/CertifiedSeed V5/Combat V11 필드를 재사용하며 새 save DTO나 schema bump는 없다. current ScriptableObject catalog와 detached world candidate가 재투영 권위다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:authored-output-ceiling-and-detached-capacity-guard-v1`; generic Main/Byproduct와 CertifiedSeed·Combat owner를 포함한다. Apparel craft/rejected, Workwear, Surgical, terminal-drain 연결은 OPEN이다.
- 검증 매트릭스와 보고서 위치: current-source Unity Main/Editor compile, forged generic authored-maximum rejection, detached guard valid/missing/duplicate/digest/1g drift, domain restore adoption과 Combat craft 회귀가 PASS했다. 마지막 Console Warning/Error 확인은 아래 구현 체크포인트에서 별도 기록한다.
- 현재 밸런스 상태: raw public API/caller 0, generic authored ceiling, 공용 detached guard와 CertifiedSeed/Combat hook은 닫혔다. Apparel/Workwear/Surgical 및 terminal owner 연결, full-path canary와 Batch C~H가 남아 Batch A~H는 `0/8`이다.

### balance:v27:apparel-detached-capacity-restore-v1

- 시대/역할: 전 시대 Apparel 제작·불량품 회수의 살아 있는 FacilityBuffer 용량 소유자를 detached 시설 복원 후보와 exact 재결속한다.
- Before: Apparel은 capability 최대 질량을 복원 때 다시 검증했지만 저장된 capacity source/minimum을 detached facility definition·위치·workstation·cycle에서 재투영하지 않았다.
- After: live work order와 source-terminal receipt가 아직 없는 terminal source order만 공용 detached guard로 검증한다. 완료된 역사적 receipt는 현재 capacity owner에서 제외한다.
- 물리 BOM·입력·출력: BOM·수량·item gram 변경 0. craft는 frozen descriptor `×1`, rejected recovery는 descriptor `×rejectedMaterialAmount`로 현재 최대 proof를 재구성한다.
- 직접 작업량과 계산 근거: WU·cycle time·작업자 성능 변경 0. current maximum registry와 facility capacity projector만 재사용한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. forged proof/source/minimum으로 저장 용량과 물리 output을 확대하는 복원 우회만 차단한다.
- 시간·확률·재시도: 저장된 품질 결과나 rejected disposition을 재굴림하지 않는다. live publication 전 exact 검증에 실패하면 복원 전체가 중단된다.
- 공간·전력·물·연료·정비: `world.facilities`를 명시적 선행 section으로 추가하고 detached facility 하나만 사용한다. live-world fallback과 epsilon은 없다.
- 위험·실패·회복 방식: capability 누락, quantity 0, proof digest/max drift, facility missing/duplicate/destroyed, capacity digest 또는 minimum 1g drift를 fail-loud한다.
- 사회·비가역 비용: 기분·품질·연구·보상·과거 save migration 변경 0.
- 기존 대안과의 장단점: Apparel 전용 facility snapshot을 저장하지 않아 중복 권위를 피한다. 대신 owner adapter가 craft/rejected phase와 maximum quantity를 명시해야 한다.
- 지배 전략 방지 조건: closed receipt 재활성화, 다른 시설로 owner 전환, 저장 proof/capacity 동시 상향, live-world fallback, output 재발행을 모두 0으로 요구한다.
- 실행 경로: `CharacterEnvironmentSaveSection → ApparelOutputDetachedCapacityRestoreGuard → maximum registry → ProductionOutputDetachedFacilityCapacityRestoreGuard → detached world facility → capacity projector → persistence candidate`.
- 저장 권위와 실행 명령: 기존 Apparel current-format capability/proof/capacity/facility 필드를 재사용하며 schema bump와 신규 저장 권위는 없다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:apparel-detached-capacity-restore-v1`; craft, rejected recovery, live order와 open terminal source를 포함한다. Workwear·Surgical은 OPEN이다.
- 검증 매트릭스와 보고서 위치: common guard valid/missing/duplicate/digest/1g matrix, strict section, Apparel physical/rejected/terminal/repair focused suites가 current Unity assembly에서 PASS했다. clean focused Console Warning/Error `0/0`이다. wider environment suite의 구형 wildlife V5 fixture 실패는 별도 미해결로 기록했다.
- 현재 밸런스 상태: Apparel detached capacity 하위 경계만 닫혔다. Workwear/Surgical, Combat save tamper, ruined terminal/full-path canary와 Batch A~H가 남아 전체 밸런스 완료가 아니다.

### balance:v27:combat-output-all-owner-restore-preflight-v1

- 시대/역할: 전 시대 전투 장비·탄약 제작의 pending FacilityBuffer owner를 복원 때 원자적으로 전수 검증한다.
- Before: 주문 정렬 루프가 한 owner를 검증한 직후 즉시 `AdoptPending`해 뒤쪽 owner 변조가 앞쪽 candidate를 부분 adoption할 수 있었고 production DI의 필수 capacity 의존성이 optional이었다.
- After: `[Inject]` production constructor의 다섯 의존성을 필수화하고, 모든 owner의 facility/outcome/proof/capacity를 1차 pass에서 검증한 뒤 전체 성공 시에만 2차 adoption을 수행한다.
- 물리 BOM·입력·출력: 장비·탄약 BOM, 수량, item gram과 component 변경 0.
- 직접 작업량과 계산 근거: 제작 WU·시간·작업자 성능 변경 0. 저장 owner의 frozen descriptor×quantity와 current maximum registry를 사용한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 저장 output 확대·복제 복원만 차단한다.
- 시간·확률·재시도: 품질 roll과 제작 결과를 재굴림하지 않는다. preflight 실패는 adoption·acknowledgement·runtime publish 전에 발생한다.
- 공간·전력·물·연료·정비: detached facility candidate와 capacity source/minimum을 exact 비교하며 authored facility 수치는 변경하지 않는다.
- 위험·실패·회복 방식: facility ID, outcome, proof digest/max, capacity digest/min, missing/duplicate/destroyed facility와 late-row corruption을 fail-loud한다.
- 사회·비가역 비용: 기분·영감·품질·연구·보상 변경 0.
- 기존 대안과의 장단점: owner별 즉시 adoption은 코드가 짧지만 전수 원자성을 증명하지 못한다. 2-pass는 작은 메모리 비용으로 restore side effect 순서를 명확히 한다.
- 지배 전략 방지 조건: 저장 output 질량 상향, facility 갈아끼우기, partial restore adoption, 중복 acknowledgement와 physical publication을 0으로 요구한다.
- 실행 경로: `CombatEquipmentSaveSection → CombatEquipmentOutputRestorePreflight pass1 → common detached capacity guard → pass2 domain output restore join → candidate publish`.
- 저장 권위와 실행 명령: 기존 Combat V11과 domain publication V5 필드를 재사용하며 schema bump와 새 저장 권위는 없다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:combat-output-all-owner-restore-preflight-v1`; combat craft pending owner 전체를 포함한다.
- 검증 매트릭스와 보고서 위치: dedicated tamper/late-row atomicity fixture, common detached facility fault matrix와 existing Combat craft transaction fixture가 current Unity assembly에서 PASS했고 Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: Combat restore preflight 하위 경계만 닫혔다. generic ExactCapability Workwear/Surgical envelope와 나머지 Batch A~H가 남아 전체 밸런스 완료가 아니다.

### balance:v27:prepared-output-completion-capacity-retry-verifier-v1

- 시대/역할: 모든 시대의 prepared production이 cycle 시작 뒤 출력 공간을 잃었을 때 WIP와 이미 확정된 결과를 보존하고 같은 batch를 exact-once로 재개하는 공용 검증 경계다.
- Before: cycle-start capacity와 개별 admission의 1g 경계는 focused test가 있었지만, 실제 `BeginWork → input WIP 소비 → completion-time output full → zero-WU retry → 공간 확보 → 동일 publication`을 한 live canary에서 검증하지 않았다.
- After: schema-4 synthetic canary가 `80,000g` FacilityOutputBuffer에 `60,001g` 물리 blocker를 넣어 `20,000g` batch를 정확히 `1g` 차단하고, retry 무변경과 typed cleanup 뒤 동일 batch/outcome 재개를 검사한다.
- 물리 BOM·입력·출력: authored BOM·수량·unit grams 변경 0. fixture는 현행 `frost-wool 21×256g + meat-pie 95×575g = 60,001g`과 synthetic output `20×1,000g = 20,000g`만 읽는다.
- 직접 작업량과 계산 근거: authored WU 변경 0. 첫 completion에 `requiredWork+1`, blocked retry와 resume에는 `0 WU`를 사용해 이미 완료된 노동을 재요구하거나 중복 계상하지 않는지 검증한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. input 재소비, output 재굴림·복제와 공간 부족 중 손실을 차단해 후속 kg-aware EWU 재생성의 물리 전제를 보존한다.
- 시간·확률·재시도: synthetic output 확률은 1이며 completion outcome은 한 번 저장된다. blocker 상태의 반복 실행은 prepared JSON, WIP, repository version, occupancy와 물리 publication을 바꾸지 않아야 한다.
- 공간·전력·물·연료·정비: authored footprint·utility·capacity 변경 0. 검증 capacity는 기존 4 cycle `80,000g` 권위이고 blocker+batch 합계 `80,001g`으로 정확히 1g 초과한다.
- 위험·실패·회복 방식: 첫/반복 실패는 typed `ProductionOutputSpaceUnavailable`과 `ResolvedWaitingForOutputSpace`여야 한다. blocker는 exact typed Sink receipt로만 제거하며 공간 확보 뒤 저장된 batch commit/outcome을 그대로 publication한다.
- 사회·비가역 비용: 기분·연구·보상·콘텐츠 해금 변경 없음. fixture blocker는 QA transaction cleanup 대상이며 gameplay 콘텐츠로 남지 않는다.
- 기존 대안과의 장단점: blocker를 BeginWork 전에 두면 start preflight만 시험하고, repository 직접 삭제는 mass receipt를 우회한다. completion 직전 실제 stack과 typed disposition은 더 엄격하지만 production 경계를 그대로 검증한다.
- 지배 전략 방지 조건: blocked retry input 재소비 0, RNG 재굴림 0, WIP gram 변화 0, premature output 0, blocker 삭제 우회 0, resume duplicate 0을 요구한다.
- 실행 경로: `ProductionBillRuntime.BeginWork → WIP authority → completion ExecuteWork → PreparedOutputExecutionAdapter → FacilityBuffer occupancy/admission → typed blocker Sink → same-batch publication → distribution/AIHaul`이다.
- 저장 권위와 실행 명령: Production V21 bill/WIP/prepared output과 Physical Items repository/component payload가 권위다. verifier는 runtime internal codec을 호출하지 않고 공개 저장 성분을 read-only로 검사한다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:prepared-output-completion-capacity-retry-verifier-v1`; synthetic standard prepared capability 하나의 completion-time 1g boundary를 포함한다. 355 recipe 전수와 모든 input owner migration은 포함하지 않는다.
- 검증 매트릭스와 보고서 위치: current-source Unity compile 및 schema-4 contract/catalog, acknowledged lifecycle, routing restore focused bundle는 PASS, Console Warning/Error `0/0`이다. 실제 PlayMode report는 dirty user-owned GameplayScene 보호 때문에 OPEN이다.
- 현재 밸런스 상태: verifier 구현·compile만 완료됐고 live coroutine PASS가 없으므로 상위 capacity-wait 체크와 Batch A~H는 `0/8`로 유지한다. 전체 밸런스 완료가 아니다.

### balance:v27:prepared-output-capacity-restore-and-cancel-verifier-v2

- 시대/역할: 모든 시대의 prepared production이 completion-time 출력 공간 부족, 저장·복원, pickup 전 취소와 committed-carry 취소를 거쳐도 물리 질량과 단일 소유권을 보존하는 공용 검증 경계다. 이 기록은 v1의 고정 blocker fixture를 supersede하되 v1의 당시 증거는 역사로 유지한다.
- Before: 1g 부족 blocker가 `frost-wool 256g`과 `meat-pie 575g`에 고정돼 kg 재조정 때 verifier 코어 수정이 필요했고, Waiting 상태의 실제 whole-root restore와 cancel lease/admission 원자성은 source/compile 증거가 없었다.
- After: blocker 목표는 `live capacity - exact batch mass + 1`이며 current catalog의 canonical generic linear-mass 후보로 stable minimum-quantity DP를 수행한다. 첫 Waiting 직후 whole-root save/restore를 실행하고 stable ID로 worker/facility/warehouse/bill/stack을 재결속한다. 두 cancel 행은 exact lease, admission, intent와 coroutine/movement 상태를 독립 검증한다.
- 물리 BOM·입력·출력: authored BOM·item quantity·unit grams 변경 0. QA blocker 조합만 current mass authority에서 파생하며 output은 기존 synthetic `20×1,000g=20,000g`이다.
- 직접 작업량과 계산 근거: authored WU 변경 0. completion까지 기존 required WU를 사용하고 blocked retry와 restored resume는 `0 WU`다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. kg 전수 조정 후에도 exact shortage fixture가 자동 재구성되도록 해 후속 EWU/price 재생성의 물리 전제만 강화한다.
- 시간·확률·재시도: probability-1 결과의 batch commit/outcome을 한 번 freeze한다. save/restore와 zero-WU retry 뒤 complete bill/prepared JSON, WIP와 물리 input absence가 동일해야 한다.
- 공간·전력·물·연료·정비: authored footprint·utility·capacity 변경 0. 현행 synthetic capacity `80,000g`, output `20,000g`에서 blocker는 정확히 `60,001g`; 향후 수치가 바뀌면 같은 공식으로 목표를 다시 구한다.
- 위험·실패·회복 방식: exact mass 조합 부재, runtime stack mass drift, whole-root fingerprint drift, stale object 재사용, lease/admission/intent 누수와 coroutine resurrection을 fail-loud한다. cleanup은 typed Sink와 outer scene-baseline restore만 사용한다.
- 사회·비가역 비용: 기분·연구·보상·해금·scene authored value 변경 0. 사용자 소유 dirty scene은 저장·unload·overwrite하지 않는다.
- 기존 대안과의 장단점: 두 아이템 고정 fixture는 단순하지만 kg 변경 때 false-red가 난다. catalog DP는 Editor 검증 비용이 추가되지만 hash 순서와 특정 콘텐츠 ID에 무관하고 새 generic item도 코어 분기 없이 후보가 된다.
- 지배 전략 방지 조건: input 재소비, output 재굴림·premature publication·duplicate publication, blocker repository 직접 삭제, pre-pickup admission/lease 누수, committed cargo 순간이동·소유권 상실을 모두 0으로 요구한다.
- 실행 경로: `BeginWork → physical WIP → exact blocker spawn → ExecuteWork/Waiting → CaptureAll/RestoreAll → persistent-ID rebind → zero-WU retry → typed Sink → same-batch publication → route → AIHaul cancel/restore → gram warehouse`다.
- 저장 권위와 실행 명령: Production V21 bill/WIP/prepared output, Physical Items stack/component, Character haul intent, quantity lease와 warehouse admission이 단일 권위다. save DTO는 검증 입력일 뿐 gameplay query가 아니다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:prepared-output-capacity-restore-and-cancel-verifier-v2`; synthetic definition-only capability와 current generic mass candidate catalog를 포함한다. 355 recipe 전수와 36 remaining input owner는 아직 포함하지 않는다.
- 검증 매트릭스와 보고서 위치: current-source Editor DLL `6ADC53EB...DC94C`, schema-4 contract/catalog, acknowledged lifecycle, routing restore focused bundle PASS, Console Warning/Error `0/0`. active GameplayScene이 dirty라 coroutine PlayMode report는 OPEN이다.
- 현재 밸런스 상태: `밸런스 영향 없음`. source·compile/focused 검증만 닫혔고 actual save/restore/cancel PlayMode 행, Batch A exit, kg 적용, EWU·가격과 6인망은 미완료다. Batch A~H는 `0/8`이다.

### balance:v27:expedition-supply-exact-mass-admission-v1

- 시대/역할: 전 시대 원정 준비의 물리 보급품을 임시 `ReservedTarget`에 모으는 입력 소유권과 kg admission 경계다.
- Before: `offense.expedition-supply`는 destination claim만 발행했고 FacilityBuffer capacity profile·gram reservation과 원자적 terminal retire가 없었다. `expedition:` delivery는 exact mass profile 없이 generic commit에 진입할 수 있었다.
- After: package의 저장된 item/quantity cost vector를 current immutable mass catalog로 투영해 exact capacity profile을 만들고 claim+profile을 공용 lifecycle로 함께 publish/restore/retire한다. `ReservedTarget`만 null facility owner를 허용하며 LiveFacility·LiveBuilding·planned output은 기존 non-null 계약을 유지한다.
- 물리 BOM·입력·출력: authored 보급 비용, 수량, item gram 변경 0. 검증 fixture의 ration 2개는 current authority로 합계 `2,000g`이다.
- 직접 작업량과 계산 근거: 원정 준비 WU·haul 거리·속도·작업자 성능 변경 0. package cost vector의 각 item/quantity를 `GetDefinitionQuantityMassGrams`로 checked 합산한다.
- EWU와 목표 회수 기간: EWU·가격·보상 변경 0. 무용량 FacilityBuffer 입고와 terminal owner 누수만 차단한다.
- 시간·확률·재시도: 확률 결과를 추가하지 않는다. 부분 availability는 같은 package·claim·profile을 유지하는 retryable staging으로 남고 terminal consume/return에서만 pair가 해제된다.
- 공간·전력·물·연료·정비: authored 시설·공간 수치 변경 0. ReservedTarget은 exact destination/drop position과 cost-vector maximum grams를 사용한다.
- 위험·실패·회복 방식: claim/profile 불일치, null Live owner, 빈/noncanonical owner, operation mismatch, over-capacity와 admission 없는 expedition commit은 물리 mutation 전에 fail-loud한다.
- 사회·비가역 비용: 원정 보상·기분·연구·사망 규칙 변경 0. 과거 save migration과 신규 gram 저장 필드는 없다.
- 기존 대안과의 장단점: package마다 gram을 저장하면 현재 kg 권위와 중복돼 drift한다. semantic costs 재투영은 현재 질량 변경을 자동 반영하지만 restore 시 current item catalog가 반드시 존재해야 한다.
- 지배 전략 방지 조건: claim-only 입고, destination prefix 우회, 다른 owner operation의 profile/request, terminal profile 잔류와 overflow를 0으로 요구한다.
- 실행 경로: `OffensePreparationService.TryCommitLoadout → exact cost mass projection → lifecycle claim/profile publish → item delivery request → warehouse mass admission → FacilityBuffer staging → consume/return → lifecycle retire`다.
- 저장 권위와 실행 명령: 기존 Offense package costs가 semantic 저장 권위이며 capacity grams는 runtime projection이다. save DTO/schema bump는 없다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:expedition-supply-exact-mass-admission-v1`; owner manifest의 `offense.expedition-supply`를 migrated로 포함한다. input owner `35`, bypass `4`는 OPEN이다.
- 검증 매트릭스와 보고서 위치: 공통 admission negative matrix, Offense 11-scenario restore/cancel/consume, deterministic owner manifest가 current Unity assembly에서 PASS했다. CSV/report SHA-256은 `7A5928C2...9888FB` / `989A8253...57788`이며 Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: `밸런스 영향 없음`. 구조·focused evidence만 닫혔고 실제 원정 PlayMode, 나머지 input owner, kg 적용, EWU·가격과 6인 생존망은 미완료다.

### balance:v27:planned-output-unique-instance-binding-v1

- 시대/역할: 전 시대의 unique 전투 장비·의복·수술 부품이 planned FacilityBuffer output으로 생성될 때 정의 ID뿐 아니라 실제 instance identity를 보존하는 공용 물리 계약이다.
- Before: publication receipt가 item definition·quantity·component·mass를 비교했지만 frozen output slice의 `ItemInstanceId`를 비교하지 않아 같은 정의와 component를 가진 다른 instance ID가 admission token을 통과할 수 있었다.
- After: frozen slice와 publication receipt의 instance ID를 ordinal exact 비교한다. Apparel restore와 Surgical fixture도 같은 instance ID를 receipt에 전달하며 변조는 `TokenMismatch`로 물리 mutation 전에 실패한다.
- 물리 BOM·입력·출력: authored BOM·수량·unit gram 변경 0. 출력 identity 검증만 강화했다.
- 직접 작업량과 계산 근거: WU·작업 시간·작업자 능력 변경 0.
- EWU와 목표 회수 기간: EWU·가격·판매·회수율 변경 0. 다른 instance로 바꿔 unique state나 가격을 재굴리는 경로만 차단한다.
- 시간·확률·재시도: frozen output 결과와 instance ID는 retry/restore에서 동일해야 하며 재굴림하지 않는다.
- 공간·전력·물·연료·정비: 시설 capacity와 footprint 변경 0.
- 위험·실패·회복 방식: instance mismatch는 token을 commit하지 않고 physical output과 reserved gram을 변경하지 않는다.
- 사회·비가역 비용: 신규 사회·기분·연구 비용 없음.
- 기존 대안과의 장단점: definition-only 비교는 단순하지만 unique ownership을 증명하지 못한다. exact instance 비교는 receipt 작성자가 identity를 전달해야 하지만 전 도메인이 같은 공용 계약을 재사용한다.
- 지배 전략 방지 조건: instance 바꿔치기, duplicate unique publication과 restore replay를 0으로 요구한다.
- 실행 경로: `domain output owner → frozen planned slice(instance ID) → physical publication receipt → FacilityBufferMassAdmissionService exact commit`이다.
- 저장 권위와 실행 명령: 도메인 WIP/output owner와 physical instance component가 권위다. 새 save field는 없다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:planned-output-unique-instance-binding-v1`; 현재 Combat/Apparel/Surgical common publication 경로를 포함한다.
- 검증 매트릭스와 보고서 위치: `ProductionDomainOutputPublicationDebugScenarios`, Apparel, Surgical, FacilityBuffer admission과 Production Economy가 current Unity assembly에서 PASS했고 Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: `밸런스 영향 없음`. identity 구조만 닫았고 authored kg·EWU·가격·6인망은 미완료다.

### balance:v27:conveyor-arrival-exact-mass-admission-v1

- 시대/역할: 자동화 컨베이어의 실제 InTransit 화물이 시설 입력 버퍼에 도착할 때 현 시설 소유권과 gram capacity를 우회하지 않게 하는 공용 도착 경계다.
- Before: `ConveyorItemGateway`가 범용 `TryCompleteTransit`으로 `InTransit → FacilityBuffer`를 직접 변경해 claim/profile/token 없이 무제한 입고할 수 있었다.
- After: 전용 `TryCompleteTransitToFacilityBuffer`가 exact current claim/profile과 whole-lot gram을 예약하고 physical mutation과 admission commit을 한 transaction으로 묶는다. 범용 완료는 Loose만 허용한다.
- 물리 BOM·입력·출력: authored item 수량·unit gram 변경 0. focused lumber lot은 정상 `2×1,200g=2,400g`, 과적 `4×1,200g=4,800g`, profile `3,600g`을 사용한다.
- 직접 작업량과 계산 근거: 컨베이어 속도·전력·WU 변경 0. arrival admission은 component-aware current mass query를 사용한다.
- EWU와 목표 회수 기간: EWU·가격·ROI 변경 0. 무제한 버퍼로 공간·물류 비용을 삭제하는 우회만 차단한다.
- 시간·확률·재시도: capacity/profile/commit 실패 시 동일 stack과 payload가 InTransit으로 남아 runtime retry 대상이 된다. item/result를 재생성하지 않는다.
- 공간·전력·물·연료·정비: authored footprint·belt throughput·port capacity 변경 0. destination의 기존 positive gram profile만 사용한다.
- 위험·실패·회복 방식: missing profile, overmass, generic bypass와 commit 직전 fault는 stack ID·instance·quantity·component·position·state·destination·reservation·mass를 보존하고 reserved grams를 0으로 만든다.
- 사회·비가역 비용: 신규 사회·기분·연구 비용 없음.
- 기존 대안과의 장단점: 도착 current-profile 검증은 저장 schema 없이 우회를 즉시 차단하지만 route 선택 당시 owner/profile을 동결하지 않는다. durable route-target fingerprint와 restore join은 후속 OPEN이다.
- 지배 전략 방지 조건: 직접 FacilityBuffer 전환, capacity 초과, 실패 시 원격 반환·복제·삭제와 exact-once 재도착 중복을 0으로 요구한다.
- 실행 경로: `ConveyorRuntime.TryUnloadAtCurrentNode → ConveyorItemGateway → ItemTransferService exact-lot reserve → physical FacilityBuffer mutation → admission commit`이다.
- 저장 권위와 실행 명령: physical repository의 InTransit stack과 Conveyor payload가 권위다. arrival token은 synchronous transient이며 저장하지 않는다. Conveyor V3의 durable route-target proof 부재는 manifest `bypass`로 유지한다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:conveyor-arrival-exact-mass-admission-v1`; current arrival callsite·generic bypass closure·focused physical fixture를 포함한다. input owner `35`, bypass `4`는 OPEN이다.
- 검증 매트릭스와 보고서 위치: Physical Stock, domain output publication, Production Economy와 deterministic owner manifest가 PASS했다. manifest CSV/report SHA-256은 `F27DB211...3BD10` / `18078227...54D97`, 두 번째 생성 변화 0, Console Warning/Error `0/0`이다.
- 현재 밸런스 상태: `밸런스 영향 없음`. arrival 구조는 공식·focused 검증 단계지만 Industrial/production-sensor PlayMode, durable route-target restore, kg 적용, EWU·가격과 6인망은 미완료다.

### balance:v27:conveyor-dynamic-intent-custody-v1

- 시대/역할: 전 시대 산업 물류의 `InTransit → FacilityBuffer` 복원·도착 안전성.
- Before/After 수치: authored kg·수량·WU·EWU·가격·용량 변화 없음.
- 물리 BOM/Direct WU/Embedded EWU: 변화 없음.
- 시간/공간/전력·용수·폐기물: 변화 없음.
- 위험: payload만 남거나 InTransit stack만 남는 단방향 restore join, 두 payload의 동일 stack 공유, non-canonical ID, 현재 segment 고아, 입고 실패 후 cargo 또는 gram reservation 유실.
- 대안: owner/profile tuple을 V4 DTO에 동결하는 방안은 현재 동적 route intent 의미보다 강한 새 기능이므로 채택하지 않음.
- 악용·오류 방지: payload↔InTransit 양방향 exact cardinality, unique stack owner, current segment join, arrival current claim/profile exact gram admission, 실패 시 InTransit 보존.
- 실행 경로: `ConveyorPersistenceAdapter.Restore → ConveyorPhysicalCustodySaveValidation → route rebuild → ConveyorItemGateway.TryCompleteToFacility → ItemTransferService.TryCompleteTransitToFacilityBuffer`.
- 저장 권위: Conveyor V3의 payload intent와 Physical Items의 exact InTransit lot. 경로와 FacilityBuffer 승인은 복원 후 현재 권위에서 재계산.
- 자동 감사·증거: `IndustrialInfrastructureDebugScenarios`, `PhysicalStockQueryV18DebugScenarios`, `V27FacilityBufferOwnerManifestDebugScenarios`; Unity 묶음 `V27_CONVEYOR_DYNAMIC_INTENT_BUNDLE_PASS`, Console `0/0`.
- 실전 증거: dirty 사용자 scene 보호 때문에 actual Industrial PlayMode 실행은 OPEN. 따라서 manifest disposition도 `bypass` 유지.
- 판정: 구조 검증 PASS, authored 밸런스 영향 없음, live PlayMode gate OPEN.

### balance:v27:production-stock-sensor-one-panel-mass-authority-v1

- 시대/역할: 재고 목표 주문을 지원하는 모든 생산 시설의 감지반 설치 입력과 시설 수명주기에서 한 패널의 실물 질량을 보존하는 공용 FacilityBuffer 경계다.
- Before: `production-sensor:{facilityId}`는 같은 셀을 추론하는 destination만 있었고 positive gram profile이 없었다. 일반 운반·컨베이어 도착은 정확한 한 패널 한도를 증명하지 못했으며 시설 철거 중 socket 권위 rollback도 없었다.
- After: 시설의 `StockSensorInstallationItemId` 한 개를 current immutable mass query로 계산해 `economy.production-sensor`의 exact `LiveFacility` claim/profile로 투영한다. 설치 여부와 무관하게 빈 socket을 유지하고 restore·건물 revision에서 현재 시설 집합으로 재계산한다.
- 물리 BOM·입력·출력: 기존 센서 패널 한 개를 그대로 사용한다. item ID·수량·unit grams·시설 BOM·센서 제거 output 수량 변경은 0이다.
- 직접 작업량과 계산 근거: 설치·제거 WU, 생산 cycle과 작업자 성능 변경 0. 용량은 `GetDefinitionQuantityMassGrams(sensorItemId, 1)`의 exact positive long grams다.
- EWU와 목표 회수 기간: EWU·구매가·판매가·ROI 변경 0. 무제한 입력이나 중복 패널 설치로 기존 물리 비용을 우회하는 경로만 닫는다.
- 시간·확률·재시도: 확률·설치 시간 변경 0. full socket에서 실패한 같은 InTransit lot은 상태와 ownership을 보존하고 공간 확보 뒤 동일 lot으로 재시도한다.
- 공간·전력·물·연료·정비: 시설 footprint·접근칸·전력·용수·폐기물·출력 buffer 수치 변경 0. 센서 input socket만 한 패널 질량으로 제한한다.
- 위험·실패·회복 방식: partial claim/profile, capacity mismatch, occupied same-ID anchor mutation, 두 번째 패널, prepare 이후 occupancy race와 revoke 실패를 fail-loud한다. output revoke 실패 시 먼저 철회한 sensor authority를 exact 복구한다.
- 사회·비가역 비용: 기분·연구·해금·보상·품질 변경 0. 과거 save migration과 신규 gram save field는 없다.
- 기존 대안과의 장단점: 임시 무제한 QA profile은 간단하지만 실제 비용과 파괴 수명주기를 숨긴다. current facility에서 파생하는 영구 socket은 저장 중복이 없고 향후 같은 capability 시설도 자동 수집하지만 current item catalog가 반드시 존재해야 한다.
- 지배 전략 방지 조건: 한 socket에 두 패널 입고, direct route bypass, occupied authority retarget, restore 후 capacity 팽창, 철거 중 partial authority revoke와 설치 질량 고아를 금지한다.
- 실행 경로: `ProductionStockSensorRuntime.RequestInstallation → ProductionStockSensorDestinationAuthorityRuntime → FacilityBuffer exact admission → physical panel delivery → install Sink → ProductionBill stock-target consumer`; restore는 `ProductionBillRuntime → TryReconcileDestinationAuthorities`로 재투영한다.
- 저장 권위와 실행 명령: Production V14의 installed/pending install/pending removal owner와 Physical Items가 상태 권위다. claim/profile은 저장하지 않는 current-facility 파생값이며 save DTO를 gameplay 질량 query로 사용하지 않는다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:production-stock-sensor-one-panel-mass-authority-v1`; owner manifest의 `economy.production-sensor` 행은 `migrated`, input `5/39`, remaining `34`, bypass `4`, orphan/unclassified `0/0`이다.
- 검증 매트릭스와 보고서 위치: `ProductionEconomyDebugScenarios`, `ProductionPreparedOutputFullPersistenceDebugScenarios`, `ProductionOutputDestinationLifecycleDebugScenarios`, `Artifacts/QA/v27-facility-buffer-owner-manifest.{csv,txt}`. manifest second-run byte/hash/mtime 변화 0, current Unity compile과 집중 회귀 PASS다.
- 현재 밸런스 상태: `밸런스 영향 없음`. one-panel admission·restore·mutation rollback 구조는 닫혔지만 설치 센서의 durable destructive loss, relocation 완료 직전 재검증과 실제 UI PlayMode는 OPEN이며 전체 밸런스 완료가 아니다.

### balance:v27:medical-surgery-input-exact-gram-authority-v1

- 시대/역할: 모든 시대의 수술 주문에 필요한 일반 재료, 수술 대상 시체와 선택 이식 부품을 하나의 임시 FacilityBuffer에 모으는 의료 입력 소유권이다.
- Before: `surgery-materials:{orderId}`는 exact LiveFacility claim만 가졌고 positive gram capacity가 없었다. profile 누락 시 generic count 배송으로 내려갈 수 있었고 저장 fingerprint와 material Sink join도 불완전했다.
- After: 주문 payload에서 필수 material+exact corpse+selected part의 최대 gram을 계산해 claim/profile pair를 원자 게시한다. claim의 owner-neutral `ExactGramRequired` 정책이 profile 누락 배송을 물리 mutation 전에 거부한다.
- 물리 BOM·입력·출력: authored BOM·수량·unit grams 변경 0. QA fixture는 bandage `5×100g`, anesthetic `2×250g`, corpse `7,000g`, selected part `800g`을 합쳐 `8,800g`을 증명하고 optional `99×900g`은 제외한다.
- 직접 작업량과 계산 근거: 수술·운반 WU 변경 0. 이번 slice는 용량과 저장 join만 추가했으며 실제 운반 횟수·거리·대기 WU 재측정 전에는 authored 작업량을 변경하지 않는다.
- EWU와 목표 회수 기간: EWU·가격·회수율 변경 0. count fallback과 무용량 임시 버퍼를 차단해 후속 물류 EWU 재생성의 물리 전제만 강화한다.
- 시간·확률·재시도: 수술 성공률·작업 시간·회복 시간 변경 0. destination lifecycle 실패는 order의 capacity/revision/fingerprint를 rollback하며 restore candidate는 publish 전 live query에 노출되지 않는다.
- 공간·전력·물·연료·정비: 시설 footprint·전력·용수·처리량 수치 변경 0. profile은 해당 주문이 합법적으로 요청할 수 있는 전체 payload의 exact gram 상한이다.
- 위험·실패·회복 방식: capacity/revision/fingerprint 변조, claim/profile partial pair와 missing profile delivery는 fail-loud한다. terminal 해제 전 pending material Sink receipt를 operation/commit/mass로 확인하고 acknowledge한다.
- 사회·비가역 비용: 기분·연구·환자 결과·콘텐츠 해금 변경 없음. 이미 소비된 재료를 terminal cancel에서 환불하지 않는 기존 의미를 유지한다.
- 기존 대안과의 장단점: surgery prefix를 코어 배송 분기로 추가하면 새 owner마다 코어 수정이 필요하다. claim의 admission policy는 신규 owner가 같은 capability만 선언하면 공통 경로가 자동 적용되지만, 기존 미이관 owner는 전환 기간 동안 CountCompatible을 유지한다.
- 지배 전략 방지 조건: profile 없는 count 입고, optional 재료를 capacity에 부풀리는 오류, exact 시체/부품 질량 누락, fingerprint 변조, Sink receipt 없는 terminal close를 0으로 요구한다.
- 실행 경로: `SurgeryRuntime.TrySchedule → SurgeryMaterialDestinationRuntime claim/profile → SurgeryLogisticsRuntime delivery → exact gram admission → FacilityBuffer → typed material Sink/tare → carried-aware release → pair revoke`다.
- 저장 권위와 실행 명령: Surgery V11 order는 capacity grams, mass authority revision, canonical fingerprint와 material Sink operation/commit/input grams/ack join만 저장한다. claim/profile과 physical receipt는 각 Items 단일 권위가 유지한다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:medical-surgery-input-exact-gram-authority-v1`; owner manifest에 current implementation을 기록하되 durable terminal drain과 pre-consume restore가 남아 disposition은 `remaining`이다.
- 검증 매트릭스와 보고서 위치: `SurgeryMaterialDestinationRuntimeDebugScenarios`, `SurgeryDebugScenarios`, FacilityBuffer mass admission이 current Unity assembly에서 PASS했다. manifest CSV/report SHA-256은 `803A5A12...3763521` / `A14E2F49...D8E889`, 두 번째 생성 byte·mtime 변화 0이다.
- 현재 밸런스 상태: `밸런스 영향 없음 / 구조 검증 부분 완료`. input owner는 `5/39`, remaining `34`, bypass `4`, orphan/unclassified `0/0`이며 `medical.surgery`는 아직 migrated가 아니다. Batch A~H는 `0/8`이다.

### balance:v27:medical-surgery-terminal-custody-and-checkpoint-gc-v2

- 시대/역할: 기존 `balance:v27:medical-surgery-input-exact-gram-authority-v1`의 당시 부분 증거를 역사로 유지하면서 current disposition을 대체한다. 모든 시대의 수술 재료 목적지가 취소·실패·시설 소실 뒤에도 예약·운반·버퍼 재고를 보존적으로 종결하고, durable save 이후 terminal join을 수거하는 의료 입력 수명주기다.
- Before: exact gram claim/profile과 material Sink join은 있었지만 source-reserved, committed intent, carried cargo, FacilityBuffer stock을 하나의 durable terminal phase로 묶지 못했다. upper-only/lower-only restore와 save 직후 tombstone 수거 경계도 없었다.
- After: owner-neutral custody-drain child가 물리 custody를 exact receipt로 닫고 Surgery V12 upper join이 그 identity·phase를 저장한다. raw/captured/registry preflight가 양방향 join을 검증하며 durable order `300` GC가 child-first/upper-last로 원자 수거한다.
- 물리 BOM·입력·출력: authored BOM·수량·unit grams·tare/byproduct 수치 변경 0. 이미 소비된 Sink는 환불하지 않고 미소비·운반 중·버퍼 보유 수량만 기존 recovery/drop 및 custody drain 계약으로 보존한다.
- 직접 작업량과 계산 근거: 수술·운반·청소·복구 WU 변경 0. terminal drain은 새 무료 이동이나 순간이동을 만들지 않고 기존 물리 예약·운반·recovery-drop 단계만 재사용한다.
- EWU와 목표 회수 기간: EWU·가격·회수율 변경 0. 구조적 소유권 누수·복제·삭제를 차단했으며 전수 EWU/가격 재생성은 후속 Batch에서 별도로 수행한다.
- 시간·확률·재시도: 수술 성공률·시간·회복·RNG 변경 0. cancel/failure/facility loss는 begin-or-resume terminal command이며 committed child effect 이후 재시도는 acknowledge/revoke/close만 전진한다.
- 공간·전력·물·연료·정비: footprint·전력·용수·처리량·저장 capacity 변경 0. carried cargo는 현재 위치 recovery-drop 계약을 유지하고 FacilityBuffer stock은 exact destination custody로만 drain한다.
- 위험·실패·회복 방식: upper-only/lower-only, phase·request·commit·receipt·digest·mass drift와 duplicate row를 durable write 또는 restore publication 전에 fail-loud한다. child publish 뒤 upper clear 실패는 child를 exact rollback한다.
- 사회·비가역 비용: 환자 결과·의사 배정·기분·연구·해금 변경 0. doctor Downed는 terminal 원인이 아니며 다른 의사가 주문을 이어받을 수 있도록 destination와 physical custody를 유지한다.
- 기존 대안과의 장단점: Surgery 전용 물리 outbox는 구현이 짧지만 owner마다 같은 저장·drain 코드를 복제한다. owner-neutral capability는 한 번의 공용 검증/GC를 재사용하지만 실제 scene fault PlayMode와 신규 procedure canary를 별도로 증명해야 한다.
- 지배 전략 방지 조건: 취소를 이용한 재료 환불·복제, carried cargo 순간이동, unacknowledged sink close, terminal tombstone 조기 삭제와 doctor loss 재료 반환을 모두 금지한다.
- 실행 경로: `SurgeryRuntime terminal request → SurgeryMaterialTerminalRuntime → FacilityBufferDestinationCustodyDrainService → Items physical custody drain/receipt → claim/profile revoke → Surgery terminal state → durable save order 300 checkpoint GC`다.
- 저장 권위와 실행 명령: Surgery V12는 upper join identity/phase만, Items child authority는 exact physical drain 진행만 소유한다. `SurgeryMaterialTerminalCrossAggregateSaveValidation`이 raw whole-save, direct registry와 outgoing captured envelope를 같은 순수 join으로 검증한다.
- 자동 감사 ID와 전수 목록 포함 여부: `balance:v27:medical-surgery-terminal-custody-and-checkpoint-gc-v2`; owner manifest는 `medical.surgery=migrated`, input `6/39`, remaining `33`, output `6/6`, bypass `4`, orphan/unclassified `0/0`, source digest `fa12cc1ec1531077043c22adf34552f7d350f03e72f7baa5d887a98e43acf249`다.
- 검증 매트릭스와 보고서 위치: `SurgeryMaterialTerminalCustodyDebugScenarios`, `SurgeryMaterialTerminalCheckpointGcDebugScenarios`, `ProductionInputDestinationCustodyDrainServiceDebugScenarios`, `Artifacts/QA/v27-facility-buffer-owner-manifest.{csv,txt}`. focused labels 6종 PASS, Console Warning/Error `0/0`, CSV/report SHA-256 `5CA585F0...C19156D` / `B2441967...CDAECD`, second-run byte/hash/mtime 변화 0이다.
- 현재 밸런스 상태: `밸런스 영향 없음 / 구조 owner migration 완료 / 실제 scene PlayMode·확장 폐쇄 canary OPEN`. Batch A~H는 `0/8`, 전체 `69–77%`, 잔여 `23–31%`를 유지한다.

### balance:v27:medical-character-supply-sequence-custody-v1

- 시대/역할: 모든 시대에서 부상 캐릭터의 치료약 또는 추출 혈액 한 개를 현재 치료 시설로 배송·소비하고, 시설 변경·취소·회복·재-Downed에도 물리 공급 소유권을 보존하는 의료 입력 수명주기다.
- Before/After: Before는 same-cell inferred destination과 직접 `ReleaseStacksByDestination`에 의존했다. After는 sequence-scoped exact `LiveFacility` claim/profile, active-1/closed-N upper join과 Items custody child를 사용한다. authored kg·수량·potency·치료 WU·시간 변경은 0이다.
- 물리 BOM: 합법 입력은 capability-derived `SupportsInjuryTreatment` medicine 또는 extracted blood 정확히 1개다. 기존 item ID와 BOM을 유지하며 신규 물리 재료·환불·무료 fallback을 추가하지 않는다.
- Direct WU: 치료·구조·운반·청소·시설 재배정 WU 변경 0. carried cargo는 새 무료 이동 없이 현재 위치 recovery drop과 기존 haul 재계획을 사용한다.
- Embedded WU/EWU: 이번 구조 slice에서 EWU·가격·회수율 재계산은 수행하지 않았다. 삭제·복제·순간이동 경로만 닫았으며 전수 EWU/가격 재생성은 후속 Batch 권위다.
- 시간: 치료 시간·배송 속도·재시도 주기 변경 0. committed physical effect 이후에는 ack→authority revoke→close만 전진하고 재료를 재생성하지 않는다.
- 공간: 시설 footprint·접근칸·FacilityBuffer authored 용량 변경 0. profile은 현재 합법 공급품 중 최대 positive unit gram이며 실제 점유량은 선택된 exact item 1개의 gram이다.
- 전력·용수·폐기물: 변경 0. package tare는 기존 typed output 계약을 유지하고 Sink 회복이 destination drain보다 먼저 실행된다.
- 위험: partial claim/profile, 상·하위 missing/duplicate/tamper, 시설/좌표/revision/fingerprint drift, pending Sink 뒤 destination 제거, restored carried cargo orphan과 tombstone 누적을 fail-loud한다.
- 대안: 주문당 하나의 terminal join은 시설을 여러 번 바꾸는 실제 수명주기를 표현하지 못한다. sequence active-1/closed-N은 기존 Items capability를 재사용하면서 order history와 다음 facility를 동시에 보존한다.
- 지배 전략 방지: 취소·시설 변경을 이용한 공급품 환불·복제, carried cargo 원위치 순간이동, profile 없는 count admission, sequence 재사용과 active join 두 개를 금지한다.
- 순환 차익 방지: 이미 Sink된 공급은 환불하지 않고 미소비 lease/routed/carried/buffered 수량만 보존적으로 drain한다. authored EWU 수치가 없으므로 SCC 경제 검증은 OPEN이다.
- 실행 경로: `CharacterMedicalRuntime → CharacterMedicalSupplyDestinationRuntime exact claim/profile → CharacterMedicalSupplyCoordinator exact delivery/Sink/tare → CharacterMedicalSupplyDestinationDrainRuntime → FacilityBufferDestinationCustodyDrainService → order 310 checkpoint GC`다.
- 저장 권위: Character Medical V6가 current/next sequence, frozen upper join과 Sink provenance를 소유한다. Physical Items가 exact stack·lease·haul intent·carry·buffer와 child phase를 소유한다. raw/registry/captured/detached restore가 양방향 join을 publication 전에 검증한다.
- 자동 감사: `CharacterMedicalSupplyDestinationRuntimeDebugScenarios`, `CharacterMedicalSupplyOutboxDebugScenarios`, `CharacterMedicalSupplyDestinationDrainRuntimeDebugScenarios`, `CharacterMedicalSupplyDestinationDrainCheckpointGcDebugScenarios`, `RestoredCarrySubsetReleaseDebugScenarios`, 공용 custody suites와 strict medical save가 PASS했다.
- 전수 목록: manifest `medical.character-supply=migrated`, input `7/39`, remaining `32`, output `6/6`, bypass `4`, orphan/unclassified `0/0`. CSV/report SHA-256은 `F8FBC664...2A75A` / `843D74AC...C2B8D`, 두 번째 생성 hash·length·mtime 변화 0이다.
- 현재 판정: `밸런스 영향 없음 / 구조 owner migration 완료 / actual autonomous medical PlayMode OPEN`. Console Warning/Error `0/0`; Batch A~H `0/8`, 전체 `69–77%`, 잔여 `23–31%`를 보수적으로 유지한다.

### balance:v27:durable-facility-equipment-slot-research-index-v1

- 시대/역할: 비전 색인철은 연구 시설의 재사용 가능한 물리 보조 장비다. 첫 적용은 `research.arcane-index`이며 기후 장비와 침입 신호 장비는 같은 등록형 capability를 후속 재사용한다.
- Before/After: Before는 raw 시설 ID, same-cell inferred admission과 count 배송을 사용한다. After 목표는 policy 등록으로 생성되는 sequence-scoped exact claim/profile, component-aware wear와 carried-aware terminal custody다.
- 물리 BOM: 기존 `record:arcane-index` 1개와 기존 제작 BOM을 유지한다. 신규 재료, 무료 대체품, 추상 장비 또는 자동 삭제를 추가하지 않는다.
- Direct WU: 기존 연구 작업량과 도구 wear `approved WU당 0.01`을 유지한다. wear commit 실패 시 1.1배 보너스 연구 진행을 publish하지 않아 무료 효과를 차단한다.
- Embedded WU/EWU: 기존 EWU·가격·회수 기간 변경 0. 전수 EWU/가격 재생성은 후속 Batch 권위이며 공식 검증은 OPEN이다.
- 시간: 연구 Tick, 작업 시간, 배송 속도와 재시도 주기 변경 0. 고갈·시설 소실 drain만 exact same-operation으로 재개한다.
- 공간: 기존 시설 footprint와 접근칸 변경 0. 슬롯 용량은 기존 item mass `1,300g × 1`에서 파생하며 별도 authored 공간 값을 추가하지 않는다.
- 전력·용수·폐기물: 변경 0. 고갈된 도구는 Sink/폐기하지 않고 물리 stack으로 회수하여 향후 수리·폐기 시스템의 입력을 보존한다.
- 위험: claim/profile 누락·부분 권위, raw destination 충돌, wear mutation 실패, 내구도 0 replacement deadlock, 시설 소실 orphan, upper/lower save 불일치를 fail-loud한다.
- 대안: owner별 전용 구현은 초기 코드가 짧지만 같은 불변식을 반복하고 신규 콘텐츠마다 설계 변경이 필요하다. 등록형 slot policy는 공통 비용이 있으나 같은 종류의 장비를 데이터/등록만으로 추가한다.
- 지배 전략 방지: wear 실패 무료 보너스, 고장 도구가 새 요청을 영구 차단하는 상태, 시설 파괴를 이용한 stack 삭제·복제·순간이동을 금지한다.
- 순환 차익 방지: 고갈 도구를 무료 신품으로 교환하거나 삭제하지 않는다. 회수된 동일 물리 질량은 기존 수리/폐기 권위로만 이동한다.
- 실행 경로: `ResearchWorkExecutionHandler → registered equipment-slot policy → exact claim/profile → Items delivery/admission → exact wear commit → research effect → custody drain`이다.
- 저장 권위: Items의 공용 durable-facility-equipment-slot upper section이 policy/subject별 sequence/join만 소유하고 기존 Items 권위가 stack·lease·intent·carry·buffer/drain child를 소유한다. Research/Climate/Invasion DTO는 slot state를 복제하지 않으며 raw/captured/registry/detached restore에서 양방향 join한다.
- 자동 감사: synthetic policy canary, exact gram/admission, wear failure no-progress, depleted/facility-loss drain, cross-save tamper와 checkpoint rollback fixture를 요구한다.
- 전수 목록: 구현 전 owner manifest는 `research.arcane-index=remaining`, input `7/39`, remaining `32`, bypass `4`, orphan/unclassified `0/0`이다. compile/focused/two-pass artifact 전에는 disposition을 변경하지 않는다.
- 현재 판정: `밸런스 영향 없음 / 구조 계약 확정 / 구현·시뮬레이션 OPEN`. authored kg·BOM·WU·EWU·가격·SO·prefab·scene 변경은 없다.

### balance:v27:m06-surgical-output-live-logistics-v1

- 시대/역할: M06 보철조립대가 제작하는 unique prosthetic을 공용 생산·물류·kg 창고·current-format 저장 경로에 연결한다.
- Before/After: Before는 generic surgical item의 stock category를 resource-only catalog에서 찾지 못해 compatible warehouse가 항상 없었고, acknowledged publication provenance도 pending lock으로 오인되어 AIHaul이 막혔다. After는 unified item catalog의 category authority와 publication 상태 의미를 사용한다.
- 물리 BOM: `recipe:surgery:prosthetic-arm`의 기존 steel 2·lumber 1·cloth 1, 출력 1개를 유지한다. unit mass는 기존 `1,800g`; BOM·수량·kg authored 값 변경 0이다.
- Direct WU/Embedded EWU: 제작 WU·운반 WU·EWU·가격 변경 0. 이번 checkpoint는 기존 실제 작업과 질량을 삭제·복제 없이 운반하는 구조만 교정한다.
- 시간/공간/전력·용수·폐기물: 작업 시간, M06 footprint, 3-cycle `5,400g` output buffer, 전력·용수·폐기물 수치 변경 0이다.
- 위험: generic item의 resource-only lookup, acknowledged provenance 영구 잠금, 배송 완료 뒤 intent/admission 잔존, 비유기 부품의 무한대 freshness 저장, restore 재생성·중복을 fail-loud한다.
- 대안: surgical item ID를 planner/warehouse에 특례로 추가하는 방식은 미래 보철마다 코어 변경을 요구하므로 채택하지 않았다. unified catalog와 상태 기반 publication gate를 사용한다.
- 지배 전략·순환 차익 방지: unique instance와 component를 동일 physical record로 보존하고, admission은 exact 1개/1,800g만 허용한다. 배송 완료 뒤 inbound reservation은 0이며 두 번째 restore에서도 수량·질량이 증가하지 않는다.
- 실행 경로: `Production bill → physical BOM WIP → SurgicalPartProductionOutputHandler → FacilityOutputBuffer → acknowledged provenance → WorldItemHaulPlanningService → AIHaul → WarehouseInventory gram admission`이다.
- 저장 권위: Surgery aggregate가 part identity/finite numeric state, Physical Items가 unique stack/component, Warehouse가 stored stock/mass를 소유한다. 완료된 haul intent/admission은 저장 권위가 아니며 은퇴한다.
- 자동 감사·증거: `PreparedOutputHaulPlannerGateDebugScenarios`, `ProductionEconomyDebugScenarios`, `SurgicalPartPreparedOutputDebugScenarios`, surgical cross-aggregate/save suites와 actual M06 PlayMode가 PASS했다. report `RESULT=PASS; failures=0`, Console Warning/Error `0/0`, recapture identity SHA-256 `56F51F8DD85BDEE2ECA6B6A763080DA61D501FFE5908DFFE4B0C6EFFFD356304`다.
- 실전·결정론: Title→Preparation→sanitized Gameplay에서 1개/1,800g 창고 입고, UI `1.8kg/25kg`, whole-save restore, recapture identity, 두 번째 restore 무복제를 확인했다. 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 유지됐다.
- 현재 판정: `밸런스 영향 없음 / actual M06 production-live 구조 검증 완료`. Batch A는 `29/31`; sawmill fault와 output aggregate fault matrix, 전수 kg·EWU·가격·6인 생존망은 계속 OPEN이다.

### balance:v27:sawmill-live-downed-and-midhaul-restore-v1

- 시대/역할: 제재소의 실제 통나무→처리목재 생산물을 공용 물리 운반·kg 창고로 보내고 운반 중단·Downed·저장 복원에서도 수량·질량·소유권을 보존하는 live gate다.
- Before/After: Before는 정상 production/route focused 증거와 synthetic canary만 있었고 실제 sawmill의 pre-pickup cancel, committed active cancel, Downed current-cell drop와 mid-haul restore가 한 경로로 닫히지 않았다. After는 실제 P03/recipe를 동일 공용 pipeline으로 끝까지 검증한다.
- 물리 BOM·출력: 기존 `resource:log ×2 → material:lumber ×3`, lumber `1,200g/unit`, batch `3,600g`을 유지한다. FacilityOutputBuffer는 기존 4-cycle `14,400g`; authored BOM·수량·kg 변경 0이다.
- Direct WU/Embedded EWU: 기존 sawmill WU·EWU·가격 변경 0. 이번 기록은 물리 수량·질량·운반 권위 폐쇄 증거이며 전수 EWU 재생성을 대신하지 않는다.
- 시간·공간·유틸리티: 작업 시간, P03 footprint, 창고 접근칸, 전력·용수·폐기물 수치 변경 0. actor가 쓰러진 실제 셀 `(23,0)`, warehouse center `(13,0)`, reachable access/drop cell `(12,0)`의 역할을 분리해 검증했다.
- 위험: 사전 취소의 output 삭제, active cancel의 carried 권위 소실, Downed 화물 원위치 순간이동, stack 복제, provenance/route 분실, stale admission restore, 저장 재생성, 완료 intent/bill 잔존을 fail-loud한다.
- 대안: 제재소 전용 운반 코드를 추가하지 않는다. 기존 prepared exact-route, item reservation, warehouse gram admission, transient carry recovery와 Brain 재계획을 사용해 미래 일반 output도 같은 불변식으로 검증한다.
- 지배 전략·순환 차익 방지: 취소·Downed·restore를 반복해도 동일 stack ID·수량 3·3,600g만 존재한다. lease/admission은 fault에서 해제되고 정상 배송에서 exact commit된 뒤 retire되며 두 번째 restore stock 증가는 0이다.
- 실행 경로: `Production bill → physical log WIP → FacilityOutputBuffer → exact route → WorldItemHaulPlanningService → AIHaul → active cancel → Downed recovery drop → RestoreAll → Brain resume → WarehouseInventory`다.
- 저장 권위: Physical Items가 stack/custody/recovery provenance, Character world가 committed carry/haul intent, Warehouse admission이 transient reserved grams, Production/Routing이 output lifecycle을 소유한다. 배송 완료 후 warehouse stored stock/mass만 최종 권위다.
- 자동 감사·증거: `PreparedOutputHaulPlannerGateDebugScenarios`, `ProductionEconomyDebugScenarios`, actual sawmill PlayMode 62 PASS. report SHA-256 `E148D4D610235EEE9101FF8460B68F2834B0C43787A4E96CE300908718D3530E`, recapture identity `EAB7F0D019A8B1355E426C5FFEA446D75E8AB695F8EE2757D2827B3AE40F61A1`, Console Warning/Error `0/0`이다.
- 현재 판정: `밸런스 영향 없음 / actual sawmill live fault·restore 폐쇄`. Batch A는 `30/31`; output owner aggregate manifest/fault-matrix zero gate와 Batch C input `31`, 전수 kg·EWU·가격·6인 생존망은 계속 OPEN이다.

### balance:v27:batch-a-output-aggregate-closure-v1

- 시대/역할: 모든 시대의 물리 생산 output owner가 item family별 코어 분기 없이 공용 exact-source/prepared/domain/planned publication 계약을 사용하고, partial·cancel·Downed·restore에서도 질량과 소유권을 보존하는 Batch A 최종 gate다.
- Before/After: Before는 개별 owner 구조와 세 live 경로가 통과했지만 fresh source digest와 전체 owner zero ratchet을 하나의 결정론적 gate로 묶지 못했다. After는 9개 owner를 네 공용 계약에 매핑하고 같은 실행의 fresh manifest와 공용 fault 증거를 결합한다.
- 물리 BOM·입력·출력: authored BOM·수량·unit grams 변경 0. synthetic `20,000g`, sawmill `3,600g`, M06 prosthetic `1,800g`의 기존 실제 수량·질량을 증거로 사용했다.
- Direct WU/Embedded EWU: WU·EWU·가격·회수율 변경 0. 이 기록은 output 소유권과 fault 원자성만 닫으며 전수 kg/EWU/가격 재생성은 후속 Batch 권위다.
- 시간·공간·유틸리티: 시설 작업시간·footprint·전력·용수·폐기물·buffer authored capacity 변경 0. 기존 capacity-wait와 실제 AIHaul 경로를 재사용한다.
- 위험·실패·회복 방식: stale manifest digest, owner 누락, bypass/orphan/unclassified, partial split 손실, cancel 삭제, Downed 순간이동, restore 복제와 admission drift를 fail-loud한다.
- 대안과 확장성: owner별 동일 fault fixture 반복은 신규 콘텐츠마다 테스트 코드를 복제한다. manifest가 등록형 공용 계약 위임을 ratchet하고 계약별 fixture와 대표 live 경로를 결합해 같은 의미의 신규 콘텐츠는 등록만으로 같은 gate에 편입한다.
- 지배 전략·순환 차익 방지: cancel·Downed·restore 반복으로 물리 stack 수량·질량이 증가하지 않으며, transient lease/intent/admission은 정확히 해제·복원·은퇴한다. SCC/EWU 차익 검증은 후속 전수 경제 gate에서 별도로 수행한다.
- 실행 경로: `output owner adapter → registered shared publication contract → FacilityOutputBuffer/Loose source → common AIHaul → gram warehouse → current-format restore → aggregate manifest gate`다.
- 저장 권위: owner aggregate는 frozen descriptor/operation identity, Physical Items는 stack/custody/recovery, Warehouse는 exact stored/reserved grams를 소유한다. aggregate report는 진단 산출물이며 gameplay 저장 권위가 아니다.
- 자동 감사·증거: `V27BatchAOutputClosureDebugScenarios`, `V27FacilityBufferOwnerManifestDebugScenarios`, 공용 output suites와 synthetic/sawmill/M06 live reports. CertifiedSeed contributor source 반영 뒤 재생성한 aggregate SHA-256 `3AB49384785188D05FD26872E69A8B26FF043A061D294AD9E3FA109ED73E20AF`; manifest TXT/CSV `12100B34...65E9A` / `8A282F49...C5578E`; second-run byte/hash/length/mtime 변화 0; Console Warning/Error `0/0`.
- 현재 판정: `밸런스 영향 없음 / Batch A output 구조·fault 폐쇄 완료`. Batch A는 `31/31`; Batch C input remaining `31`, authored kg, WIP/Sink, EWU·가격, 6인 생존망과 final seeds는 OPEN이므로 전체 밸런스 완료가 아니다.

### balance:v27:p17-current-live-capacity-v1

- 시대/역할: P17 사료배합대가 active bill 없이도 reachable 최대 생산 branch의 물리 출력 공간을 게시하고, 실제 hay 생산물을 공용 AIHaul·kg 창고·current-format 저장으로 전달하는 current-revision live gate다.
- Before/After: 이전 정상 부트 보고서는 verifier가 직접 GameplayScene을 열어 DI 준비 전에 실패했으므로 current 증거가 아니었다. After는 전용 P17 request/report와 임시 sanitized Gameplay scene lease로 production 부트를 통과한다.
- 물리 BOM·입력·출력: dog-food 최대 branch `2×525g=1,050g`, cycle capacity `4`, no-bill minimum `4,200g`; 실제 hay는 기존 `grass-straw×3 + twilight-grain×1 → feed:hay×3=588g`이다. authored BOM·수량·kg 변경 0이다.
- Direct WU/Embedded EWU: 작업 WU·EWU·가격 변경 0. 이 기록은 capacity·생산·물류·저장 권위의 live 증거만 갱신한다.
- 시간·공간·유틸리티: 작업 시간·footprint·전력·용수·폐기물 변경 0. 임시 scene은 사용자 `GameplayScene`을 저장·폐기하지 않고 종료 시 제거된다.
- 위험·대안: active bill만 보고 `2,352g`으로 축소하는 오류, direct Gameplay 부트 DI 누락, synthetic 보고서 덮어쓰기, save restore 중 수량 복제를 fail-loud한다. 전용 report identity와 공통 normal-boot verifier를 사용하며 P17 전용 gameplay 분기는 추가하지 않는다.
- 순환·소유권: exact physical inputs를 한 번 소비하고 output `3/3=588g`만 생성한다. AIHaul 종료 후 inbound reservation은 0, bill은 terminal retire되며 두 번째 restore에서 중복은 0이다.
- 실행·저장 권위: `Title → Preparation → PreparedNewRun → sanitized Gameplay → no-bill capacity publication → bill/WIP → FacilityOutputBuffer → AIHaul → Warehouse → RestoreAll`이다. gameplay 저장 권위는 기존 Production/Physical Items/Warehouse aggregate가 유지한다.
- 자동 감사·증거: contributor schema-v4 적용 뒤 `Artifacts/QA/prepared-output-p17-live-playmode-report.txt`의 SHA-256은 `90AF89B6538A45992160C9C6CA00537BBB9D44F62BF5F7A2CCFEF60E73DCEE14`, `RESULT=PASS`, captured Warning/Error `0/0`, save recapture identity `6CEECB0F96CFA64F232C7A14F88FB266547C8B175CD716878CDA6BB30F8FAD87`이다. request/temp scene/backup 잔여 0, 공식 GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 보존됐다.
- 현재 판정: `밸런스 영향 없음 / P17 current live capacity 증거 완료`. 전체 maximum-envelope·domain no-bill contributor·p95/4-cycle Critical·destructive live·topology retarget과 이후 Batch C–H는 OPEN이다.

### balance:v27:certified-seed-no-bill-capacity-contributor-v1

- 시대/역할: RF93 육종 온실이 아직 인증 종자 주문을 갖지 않아도 모든 현재 작물의 definition-only 물리 출력 최대치를 FacilityBuffer 용량에 사전 투영한다.
- Before/After: Before는 `building:8893` numeric ID가 runtime 두 곳에 복제됐고, CertifiedSeed capacity proof는 주문 완료 시점에만 생겨 no-bill 상한을 사후 확대했다. After는 exact workstation tag·valid buffer eligibility와 등록형 pure contributor를 command/presentation/projector가 공유한다.
- 물리 BOM·출력: 기존 종자 로트 1개 출력, 인증 키트·입력 종자 BOM과 모든 authored unit kg는 변경 0이다. current crop `12`, 인증 종자 최대 `50g/cycle`, contributor-only 4-cycle envelope `200g`이다. 최종 RF93 capacity는 generic recipe envelope와 contributor envelope 중 큰 값이다.
- Direct WU/Embedded EWU·가격: 인증 작업 WU·EWU·구매·판매 가격 변경 0. 이 기록은 no-bill 물리 공간 권위와 확장 구조만 추가한다.
- 시간·공간·유틸리티: cycle 수 `[2,4]`, footprint·전력·물·폐수·저장 공간 authored 값 변경 0. 4회를 초과하는 p95 정책은 별도 OPEN이다.
- 위험·실패: 대체 crop branch 질량 합산, contributor 삽입 순서 의존, duplicate ID, capability/item 불일치, 숫자 ID 특례, active proof의 사후 capacity 확대와 stale source digest를 fail-loud한다.
- 대안·확장성: projector에 crop/seed ID switch를 넣지 않는다. contributor가 facility predicate와 정적 branch request만 제공하고 공통 selector/registry가 질량·정렬·digest·maximum을 소유하므로 미래 crop 추가는 코어 코드 변경 없이 카탈로그에 자동 편입된다.
- 순환·소유권: contributor는 reserve/publish/spawn/save를 호출하지 않는 read-only projection이다. 실제 CertifiedSeed owner와 물리 seed-lot output의 exact-once 계약은 기존 domain publication/restore가 계속 소유한다.
- 실행·저장 권위: `BuildingSO workstation/buffer + Crop catalog → CertifiedSeedFacilityOutputCapacityContributor → contributor registry → maximum-mass capability selector → ProductionOutputBufferCapacityProjector@4 → no-bill destination profile`이다. contributor state는 저장하지 않고 source digest로 재계산한다.
- 자동 감사·증거: maximum-mass registry, contributor order/branch/duplicate fixture, CertifiedSeed eligibility, actual RF93/12 crop, CropPhysicalTransaction, maximum-factor, workshop, full ProductionEconomy가 PASS했다. schema-v4 P17 normal boot도 `RESULT=PASS`, Console Warning/Error `0/0`, 공식 scene hash 보존을 확인했다.
- 현재 판정: `밸런스 수치 변경 없음 / CertifiedSeed no-bill contributor 완료`. Apparel·Combat·Surgical contributor, 전수 facility census, world/crop Source authority, p95/>4-cycle, destructive live와 Batch C–H는 OPEN이다.

### balance:v27:apparel-and-surgical-no-bill-capacity-v1

- 시대/역할: 재단·재봉 작업대의 직접 의복 주문과 M06 보철조립대의 일반 production recipe가 active order/bill 이전에도 실제 도달 가능한 최악 물리 출력 질량을 예약한다.
- Before/After: Before는 Apparel 직접 주문의 no-bill 상한이 시설 portfolio에 없었고 불량 회수 planned line 한 곳이 `output:` 접두사 없는 복제 literal을 사용했다. After는 UI·command·restore·contributor가 동일 ApparelTailoring 적격성을 공유하고, 회수 line을 단일 상수로 통일한다. M06은 별도 contributor를 만들지 않고 기존 recipe 도달성 권위를 유지한다.
- 물리 BOM·출력: authored BOM·출력량·unit kg 변경 0. Apparel `56`, textile material `12`, tag-compatible `598` 중 runtime 질량보존으로 실제 성공 가능한 recovery `493`과 craft `56`을 alternative branch로 둔다. 최대는 방수 작업복 `1,380g/cycle`, 4-cycle `5,520g`. M06은 실제 3 recipe, `1,800g/cycle`, 3-cycle `5,400g`이다.
- Direct WU/Embedded WU·EWU·가격: 재단·회수·보철 제작 WU, EWU, 구매·판매 가격 변경 0. 이번 기록은 출력 공간 상한과 실행/표시 적격성 drift만 닫으며 전수 경제 재생성을 대신하지 않는다.
- 시간·공간·유틸리티: 기존 cycle 수, 시설 footprint, 전력·용수·폐기물 변경 0. contributor는 물리 reserve/spawn/save를 수행하지 않는 definition-only 조회다.
- 위험·대안: 모든 tag-compatible 회수를 producer로 세어 runtime이 거부할 105개 phantom branch를 만드는 오류, effect 1.08 하드코딩, craft/recovery 동시 합산, Surgical semantic 17종을 recipe 없이 투영하는 오류를 금지한다. Apparel은 effect finite maximum과 exact gram conservation으로, Surgical은 실제 recipe 3개로만 도달성을 정한다.
- 지배 전략·순환 차익: 불량 의복 하나의 회수 질량은 소비한 garment 질량을 넘지 않으며 craft와 recovery는 순차 alternative다. 회수를 시설 용량 증설이나 물질 복제 경로로 사용하지 않는다. SCC/EWU tolerance 0 검증은 후속 전수 경제 gate에서 별도로 수행한다.
- 실행 경로: `BuildingSO command/workstation/buffer → ApparelTailoringFacilityEligibility → ApparelFacilityOutputCapacityContributor → shared maximum selector/projector`; Surgical은 `ProductionRecipeSO(m06) → Surgical maximum capability → generic projector`다.
- 저장 권위: contributor와 effect bounds catalog는 저장 상태를 만들지 않는다. capacity source digest가 catalog/effect/mass/contributor revision을 결속하며 실제 output 저장은 기존 Apparel/Production/Physical Items authority가 소유한다.
- 자동 감사·증거: contributor registry actual-content scenario, Apparel craft/rejected physical transaction, 전체 Production Economy가 fresh PASS했다. Apparel 549 branches, `1,380g/5,520g`, M06 3 recipes, `1,800g/5,400g`, deterministic repeat digest와 Unity Console Warning/Error `0/0`을 확인했다.
- 현재 판정: `밸런스 수치 변경 없음 / Apparel no-bill contributor 및 Surgical recipe-backed preprojection 완료`. Combat contributor, facility `146/90/56` census, p95/>4-cycle, topology retarget, Batch C–H는 OPEN이다.

### balance:v27:combat-primary-no-bill-capacity-v1

- 시대/역할: S08 대장작업대가 active craft order 이전에도 시설 allowlist로 도달 가능한 장비·탄약의 최악 primary 물리 출력 질량을 예약한다.
- Before/After: Before는 queue가 시설 ability/allowlist를 검사하지 않았고 worker는 빈 목록을 wildcard로, UI는 malformed ID를 silent filter했다. 화살·볼트 output/BOM/ammunition 판별도 세 switch였다. After는 63개 immutable craft catalog, canonical nonempty exact allowlist와 definition-indexed facility eligibility를 command/worker/UI/contributor가 사용한다.
- 물리 BOM·출력: authored BOM·수량·kg 변경 0. 장비 61종은 기존 equipment catalog, 탄약은 arrow `20 × 60g = 1,200g`과 bolt `12 × 90g = 1,080g`; 최대 powered harness `18,000g/cycle`, S08 4-cycle `72,000g`이다. 화살/볼트 fixed BOM은 같은 craft definition record가 소유한다.
- Direct WU/Embedded WU·EWU·가격: 제작 WU·EWU·가격 변경 0. primary output 공간과 order admission만 연결했으며 rejected recovery의 경제·질량 상한은 계속 OPEN이다.
- 시간·공간·유틸리티: S08 cycle 4, footprint·전력·용수·폐기물 변경 0. 72kg는 최악 18kg branch의 4-cycle envelope다.
- 위험·대안: 빈 wildcard, whitespace/duplicate/unknown allowlist, ammo output/BOM drift, contributor에 네 번째 ID switch, facility 밖 주문 admission을 fail-loud한다. repair/evolution은 기존 item mutation/release이므로 primary Source branch에서 제외한다.
- 지배 전략·순환 차익: primary 장비/탄약 branch는 서로 대체 경로이며 합산하지 않는다. quality-rejected recovery는 primary 뒤에 이어지는 순차 경로다. 향후 별도 simultaneous-material branch로 검증해야 하며 회수 차익 0은 OPEN이다.
- 실행 경로: `Combat craft catalog + BuildingEquipmentCraftingAbility → CombatCraftFacilityEligibility → queue/worker/UI validation + CombatCraftFacilityOutputCapacityContributor → shared maximum selector/projector`다.
- 저장 권위: catalog/contributor는 저장 상태를 만들지 않는다. 기존 Combat order가 frozen descriptor/outcome을 소유하고 Physical Items/FacilityBuffer가 actual stack·gram을 소유한다. 향후 definition fingerprint restore 결속은 recovery/UI 하위 행과 함께 닫는다.
- 자동 감사·증거: actual S08 61+2 join, 63 primary branches, arrow/bolt output·BOM, 18,000g winner, 72,000g profile, material/craft terminal/full Production Economy PASS, clean Console Warning/Error `0/0`. broad Combat suite의 unrelated cover failure는 제외해 별도 red로 보존한다.
- 현재 판정: `밸런스 수치 변경 없음 / Combat primary no-bill envelope 완료 / Combat 전체 OPEN`. rejected recovery, ammo UI, definition fingerprint restore, facility census와 Batch C–H가 남는다.

### balance:v27:combat-rejected-recovery-capacity-and-restore-v1

- 시대/역할: S08 대장작업대에서 목표 품질에 미달한 장비를 자동 해체할 때, 실제 회수물의 질량·출력 공간·물리 commit·저장 복원·재시도를 primary 제작과 같은 공용 FacilityBuffer 용량 권위에 결속한다.
- Before/After: Before는 rejected recovery 상한이 시설 profile에 없고 maker contribution을 해체 factor로 오인할 수 있었으며, 저장된 spawned prefix가 실제 physical commit 없이 복원될 수 있었다. After는 실제 해체 작업자 factor를 input commit 전에 한 번 동결하고, shared pure projector와 detached physical candidate가 frozen vector·질량·commit을 양방향 검증한다.
- 물리 BOM·입력·출력: authored 장비 BOM·출력량·unit kg 변경 0. current definition projection은 `61 equipment × material policy = 252`; 합법적 zero-output `20`, 실제 physical recovery branch `232`다. 모든 vector는 consumed unique equipment 질량 이하이며 actual greatsword 입력은 `4,600g`, focused 회수 commit은 1개다.
- Direct WU/Embedded WU·EWU·가격: 제작·해체 WU, salvage formula, item EWU, 구매·판매 가격 변경 0. actual runtime은 별도 해체 작업자 skill `73`과 live salvage multiplier `1`을 동결했다. 제작→거절→해체→재제작 SCC tolerance 0과 kg-aware 가격 재생성은 Batch H에서 계속 수행한다.
- 시간·공간·유틸리티: S08 cycle 4, footprint·전력·용수·폐기물 변경 0. primary `63`과 nonzero recovery `232`는 alternative branch로 비교하며 합산하지 않는다. S08 maximum은 powered harness primary `18,000g/cycle`, profile `72,000g`을 유지한다.
- 위험·실패·회복 방식: maker/해체 worker 혼동, projector 누락 뒤 consume, 잘못된 equipment item substitution, NaN/무한 factor, source digest drift, partial spawned prefix, physical commit 누락·변조·orphan, zero-output terminal 거부, restore 후 재굴림·중복 출력을 fail-loud한다.
- 대안·확장성: 장비 ID별 회수 switch나 저장 필드를 추가하지 않는다. equipment/material/mass/effect bounds와 공용 projector가 신규 정의를 자동 열거하며, 새 material policy는 같은 deterministic vector clamp와 contributor registry에 자동 편입된다.
- 지배 전략·순환 차익 방지: desired recovery가 source 질량을 초과해도 정수 whole-vector clamp로 `Σ output grams <= input receipt grams`를 강제한다. zero-output은 삭제를 숨기지 않고 명시 census로 남기며, 재시도는 기존 deterministic commit만 재사용해 second output을 만들지 않는다.
- 실행 경로: `S08 craft order → quality rejection → physical unique output → separate dismantle WU → pending Transfer receipt → shared recovery projection → FacilityOutputBuffer commits → acknowledgement → next attempt`이며 restore는 `Physical staged candidate → Combat candidate build → cross-aggregate owner/commit guard → publish → retry` 순서다.
- 저장 권위: Combat order가 factor bit·worker skill·salvage multiplier·input/output grams·source digest·output vector·spawned prefix를 소유하고, Physical Items가 pending input disposition과 committed output stack/component를 소유한다. contributor는 definition-only 조회이며 저장 상태를 만들지 않는다.
- 자동 감사·증거: `ProductionFacilityOutputCapacityContributorRegistryDebugScenarios`, `CombatEquipmentCraftTransactionFixture`, focused `PhysicalItemDebugScenarios.RunCombatRejectedRecoveryRuntimeRestore`, `CombatEquipmentMaterialDebugScenarios`, `CombatEquipmentCraftTerminalAuthorityDebugScenarios`, 전체 `ProductionEconomyDebugScenarios`가 current assembly에서 PASS했다. focused TSV SHA-256은 `474AA3C192C51B8B86B5CE73ADE6C33418E8216AE8EED2F197BB35513EFB141B`, Console Warning/Error `0/0`이다.
- 현재 판정: `밸런스 수치 변경 없음 / Combat primary·ammo UI·rejected recovery contributor 및 actual restore/retry 완료`. 전수 `146/90/56` typed facility census, p95/>4-cycle, topology retarget, destructive live와 Batch C–H가 남아 Batch B 및 전체 밸런스 완료가 아니다.

### balance:v27:facility-output-census-and-equipment-progression-craft-v1

- 시대: 전 시대 생산·서비스·장비 진행 시설의 물리 output 귀속과 지속 작업 경계다.
- 역할: 생산 capability 모양을 가진 시설 전부를 recipe/contributor producer 또는 typed nonproducer로 분류하고, I17 룬 조율실과 I18 계보 기록실의 상태 변경을 일반 생산물과 구별한다.
- Before: 전체 `146`개 시설 중 Apparel 5개와 P06/I17/I18의 귀속이 불완전했고 I18 계보 이전 주문은 저장·완료 API만 존재해 실제 Craft worker가 진행하지 못했다. 오래된 검증은 ResearchOverhaul을 `33 command + 5 deferred`로 고정했다.
- After: current source는 `90 producer / 56 nonproducer`, unclassified/orphan `0/0`, typed content gap `6`으로 결정론적으로 분류된다. ResearchOverhaul은 현재 권위인 `38 command / 0 deferred`이고 I18은 capability 등록형 Craft contributor로 기존 `120 WU` 주문을 진행한다.
- 물리 BOM: I18 source 장비와 lineage seal의 기존 exact Transfer를 그대로 사용한다. item ID·수량·unit grams·시설 BOM·kg 변경은 `0`이다.
- Direct WU: I18 authored `requiredWork=120`을 유지한다. focused 증거는 `60+60` partial/completion을 사용하며 신규 무료 완료나 WU 배율을 추가하지 않는다.
- Embedded WU/EWU: EWU·구매가·판매가·회수율 변경은 `0`이다. 이번 기록은 실행 고아와 시설 분류만 닫으며 전수 EWU·가격 재생성은 후속 Batch 권위다.
- 시간: I17은 기존 즉시 tune command, I18은 기존 지속 작업 시간을 유지한다. 완료 재시도는 이미 완료된 order를 다시 적용하지 않는다.
- 공간: footprint·작업 접근칸·FacilityBuffer 용량·저장 면적 변경은 `0`이다. I17/I18은 state-mutation nonproducer다.
- 전력·용수·폐기물: authored 전력·용수·폐수·폐기물·연료 수치 변경은 `0`이다.
- 위험: content gap을 unclassified로 숨김, state mutation을 물리 output으로 오분류, core의 building-ID switch, duplicate/noncanonical contributor, I18 partial progress 손실과 source/seal 이중 소비를 fail-loud한다.
- 대안: I18 전용 Work handler나 Building ID 분기는 신규 시설마다 코어 수정이 필요하므로 채택하지 않는다. 기존 workstation tag를 immutable capability key로 사용하고 등록형 contributor가 공용 Craft adapter에 연결된다.
- 지배 전략 방지: I18 완료 반복으로 history를 재적용하거나 source/seal을 복제하지 않는다. I17/I18을 producer로 세어 허위 buffer capacity를 얻지 않는다.
- 순환 차익 방지: 이번 slice는 수치를 변경하지 않으며 exact source+seal Transfer와 target history 보존만 검증한다. SCC tolerance `0` 전수 경제 증명은 후속 Batch에서 별도로 수행한다.
- 실행 경로: `BuildingProductionWorkstationAbility.WorkstationTag → deterministic persistent-operation registry → CraftWorkExecutionHandler → LineageTransferCraftOperationContributor → EquipmentHistoryTransferRuntime.ApplyWork`; census는 등록형 disposition contributor를 사용한다.
- 저장 권위: 신규 DTO/schema는 없다. I17은 기존 equipment/module component, I18은 기존 history-transfer order와 physical equipment state가 단일 쓰기 권위다. Craft plan과 contributor registry는 저장하지 않는다.
- 자동 감사·실전 증거: census TXT/CSV SHA-256은 `298EE0FA0B4C07F059BC5CE8F72982434D1DE9F3312AC45B1CFE5CF554235DE2` / `C898AC94D165D0DB472DA88DDAD5F1059D9E111D81866BB9C305AA529143D5CA`, lineage focused TSV는 `48DBF064A51D82B23DD11B477214860F98133964648ACB3846771CABC282FB30`이다. second-run hash·length·mtime 변화 `0`, 전체 Physical Item·V22 Apparel·180 ResearchOverhaul·Production Economy PASS, Unity Console Warning/Error `0/0`, 공식 GameplayScene SHA-256 보존을 확인했다. 실제 UI queue→자율 AI Craft completion PlayMode는 OPEN이다.
- 현재 판정: `밸런스 수치 변경 없음 / 전수 시설 census 완료 / I17·I18 구조 및 focused Craft 완료 / actual UI·AI PlayMode OPEN`. Batch B의 나머지 6개와 Batch C–H가 남으므로 전체 밸런스 완료가 아니다.
### balance:v27:grand-project-affected-recipe-maximum-envelope-v1

- 시대/역할: Grand Project 산출 증가의 영향을 받는 모든 standard production recipe가 실제 output line·unit gram·시설 버퍼와 동일한 최대치 권위를 사용하도록 고정하는 전수 증거다.
- Before/After: Before는 current affected recipe 수 `21`과 P22 quarry `15,300g`만 직접 고정했고 나머지 20개 recipe ID·scaled gram 및 P14/P18/P19 시설 최대치가 결합 증거로 남지 않았다. After는 exact 21개 ID와 각 행의 유리수 factor, base/max gram, 4-cycle capacity를 결정론적 CSV와 fail-loud fixture로 결속한다.
- 물리 BOM·출력: authored BOM·output quantity·unit kg 변경 `0`. maximum projection만 `23/20` 또는 `5/4`를 적용해 모든 `probability>0`, non-loss output을 동시에 가능한 최악 branch로 계산한다.
- Direct WU/Embedded WU·EWU·가격: WU·EWU·구매·판매 가격 변경 `0`. 이 기록은 출력 공간 상한 증거이며 전수 EWU·가격 재생성을 대신하지 않는다.
- 시간·공간·유틸리티: 모든 해당 시설의 authored output buffer `4 cycles`를 유지한다. P14/P18/P19/P22의 최악 단일 cycle은 `1,500/750/800/15,300g`, 4-cycle capacity는 `6,000/3,000/3,200/61,200g`이다.
- 위험·대안: affected 개수만 유지한 채 recipe 구성이나 kg가 drift하는 오류, float multiplier 반올림, 확률 부산물 누락, alternative recipe mass 합산을 fail-loud한다. 콘텐츠 ID switch를 runtime에 추가하지 않고 current recipe/facility/catalog를 전수 투영한 뒤 QA fixture에서 현재 기준 집합만 승인한다.
- 지배 전략·순환 차익 방지: projection은 read-only이며 물리 생성·소비·가격을 바꾸지 않는다. 입력 Ceil/출력 Floor 및 SCC 차익 검증은 Batch H 경제 gate에서 별도로 유지한다.
- 실행 경로: `ProductionRecipeSO → ProductionMaximumOutputFactorCatalog → physical output lines → PhysicalItemMassQuery → facility 4-cycle envelope → deterministic CSV`다.
- 저장 권위: 신규 gameplay 저장 상태 없음. RecipeSO·BuildingSO·item gram authority가 source이고 CSV는 review evidence다.
- 자동 감사·증거: `ProductionMaximumOutputFactorCatalogDebugScenarios.RunAll()` current Unity PASS. artifact는 header 포함 `22`행, `6,210 bytes`, SHA-256 `382BFFF9133BEE49CACA9F671636AE252814CB30069F14C454770BB41859159A`; second-run byte/hash/length/mtime 변화 `0`; Unity compile `7.61s`, Console Warning/Error `0/0`.
- 현재 판정: `밸런스 수치 변경 없음 / Grand Project 영향 standard recipe 21행 maximum envelope 완료`. world-resource/crop direct Loose output maximum·원자 publication은 OPEN이며 Batch B 전체는 `34/40`을 유지한다.

### balance:v27:world-resource-proof-bound-maximum-output-v1

- 시대·역할: 전 시대의 지상 자연자원 채집·벌목·채석 출력이 실제 source topology와 동일한 최대 질량 증명을 사용한다.
- Before/After: Before는 factor가 authored maximum 이내인지만 검사하고 actual frozen vector가 공용 maximum-mass registry와 결속되지 않았다. After는 등록형 binding contributor `4`행을 고유 source recipe `3`행으로 투영하고 line별 capability·quantity·unit gram·maximum gram과 aggregate proof를 frozen outcome/save restore에 결속한다.
- 물리 BOM·출력: authored item kg·output quantity·BOM 변경 `0`. current maximum은 grass `320g`, logging `9,200g`, saltstone `5,300g`이며 확률 양수 부산물은 포함하고 확률 0·DeclaredLoss는 물리 용량에서 제외한다.
- Direct WU·Embedded WU/EWU·가격: required work, WU, EWU, 구매·판매 가격 변경 `0`. 이번 기록은 정의 상한과 exact-source publication 원자성만 닫으며 전수 경제 재생성을 대신하지 않는다.
- 시간·공간·유틸리티: source cycle 시간, 노드 위치, 저장·운반 공간, 전력·용수·폐기물 변경 `0`. source output은 exact-source physical publication으로 게시된다.
- 위험·대안: 확률 부산물 누락, factor fractional ceil 누락, 신규 binding 미집계, stateful output을 component 없이 생성, actual line/aggregate maximum 초과, mass/registry/binding/recipe drift restore와 source debit 후 실패를 fail-loud한다. item-ID switch 대신 contributor catalog와 pure registry를 사용한다.
- 순환·악용 방지: maximum authority는 read-only이며 생성·소비·가격을 변경하지 않는다. source debit과 physical output commit은 기존 exact-source transaction으로 exact-once 처리하고 zero-output cycle도 fake item 없이 sequence만 완료한다. SCC tolerance `0` 검증은 Batch H에서 별도로 유지한다.
- 실행 경로: `binding contributors → WorldResourceSourceBindingCatalog → source recipe → ProductionOutputMaximumMassRegistry → WorldResourceOutputMaximumEnvelopeAuthority → frozen pending proof → PhysicalItemExactSourcePublicationService`다.
- 저장 권위: WorldResource V4 pending owner가 root seed, cycle identity, recipe/factor/vector, aggregate maximum gram과 maximum source digest를 소유한다. transient admission token/stack ID는 저장하지 않으며 restore는 current authority로 exact 재구성한다.
- 자동 감사·증거: maximum CSV SHA-256 `FCDDF691D279D74ABDB78A8A1C2B4855797BD7B39C499A00788F7FEFE476E374`, transaction fault SHA-256 `88EEB4E202650408D2640ECE19ADE51C97FF58FEE2510E5E02A179392AA3D806`; 두 번째 실행 byte/hash/length/mtime 변화 `0`; contributor·Production Economy·Batch A aggregate PASS; Console Warning/Error `0/0`; GameplayScene SHA-256 보존.
- 현재 판정: `밸런스 수치 변경 없음 / WorldResource maximum proof-bound output 완료`. crop frozen two-line atomic publication이 남아 Batch B 전체 maximum envelope와 전체 밸런스 완료는 OPEN이다.

### balance:v27:p03-destructive-live-conservation-and-restore-v1

- 시대/역할: 초기 목재 생산의 P03 sawmill이 FacilityBuffer output과 active AIHaul carried custody를 가진 상태에서 구조 파괴될 때 모든 생산·물리·저장 권위를 보존적으로 종결하는 공통 경계다.
- Before/After: Before는 6-participant durable drain과 checkpoint GC가 focused 계약으로만 존재했고 actual P03 복원은 stale power-node row, Unity JSON default haul intent와 capacity participant의 pre-removal fingerprint 때문에 실패했다. After는 제거된 power node를 pending fuel 사전검증 뒤 retire하고, exact empty haul sentinel만 absent로 정규화하며, authority revoke와 world removal마다 최종 contributor vector를 동일 lifecycle snapshot에 결속한다.
- 물리 BOM·입력·출력: authored sawmill BOM, log input, lumber output, unit kg와 stack 수량 변경 `0`. actual terminal run은 전체 quantity `18→18`, mass `21,600g→21,600g`, carried `0`, inbound `0`을 유지했다.
- Direct WU: sawmill 제작·건설·운반 WU 변경 `0`. destructive drain은 bounded `6/32` 상태 전이로 완료됐으며 무료 생산·작업 환급을 추가하지 않는다.
- Embedded WU/EWU: item EWU, 입력 Ceil, 출력 Floor, 구매·판매 가격과 회수율 변경 `0`. 이 slice는 삭제·복제·순간이동을 막는 구조 무결성 증거이며 전수 kg-aware EWU/가격 재생성을 대신하지 않는다.
- 시간: active cancel은 carried cargo를 유지하고, 파괴는 durable stage와 중간 checkpoint 뒤 terminal drain을 재개한다. 두 번째 checkpoint는 추가 mutation 없이 no-op다.
- 공간: P03 footprint, output buffer `14,400g`, 창고 위치와 통로 변경 `0`. terminal physical custody는 월드 제거 전 보존적으로 해제되고 일반 바닥 teleport나 orphan destination을 만들지 않는다.
- 전력·물·폐기물: authored 전력·용수·폐기물 수치 변경 `0`. 제거된 power topology node는 pending fuel이 없을 때만 aggregate/timer에서 retire하며 fuel이 남으면 fail-loud한다.
- 위험: 부분 haul intent를 empty로 오인, stale participant fingerprint 승인, prepared provenance 재작성, notification exception rollback, authority pair 잔존, terminal restore duplication, checkpoint GC 순서 역전을 fail-loud한다.
- 대안: restore validator tolerance나 occupancy 제거는 사용하지 않는다. coordinator가 소유한 두 shared-state 경계에서 exact participant set과 acknowledged owners를 확인한 뒤 current fingerprint만 원자 갱신한다.
- 지배 전략 방지: 파괴로 lumber를 복제·삭제·창고 순간이동하거나 WU/BOM을 환급하지 않는다. physical quantity와 gram conservation, lease/intent/admission release와 second-checkpoint no-op를 함께 요구한다.
- 순환 차익 방지: authored 경제값은 바뀌지 않았고 파괴 전후 물리 질량이 exact 동일하다. dismantle/rebuild SCC tolerance `0`과 최종 가격/회수 차익은 Batch H 전수 gate에 계속 남는다.
- 실행 경로: `P03 prepared output → AIHaul carried custody → ProductionFacilityDestructiveDrainRecoveryRuntime → six participant journal → authority revoke → world removal → checkpoint GC → whole-root restore → second checkpoint`다.
- 저장 권위: upper destructive-drain journal은 prepared provenance와 current lifecycle epoch를 분리해 저장한다. Physical Items가 stack/carry를, Production/Routing participant가 lower receipt를, ElectricalNetwork aggregate가 live power node를 각각 소유한다.
- 실패·회복: throwing destruction subscriber는 `CommittedWithNotificationFailure`로 귀속하되 제거를 롤백하지 않는다. contributor set 누락/중복, unacknowledged owner, pending fuel, nonempty intent, cross-aggregate mismatch는 journal revision을 진전시키지 않는다.
- 자동 감사·증거: direct Runtime/Editor compile, power/physical/coordinator focused regressions와 actual P03 PlayMode PASS. 보고서 `Artifacts/QA/prepared-output-destructive-drain-live-playmode-report.txt` SHA-256 `71614C1CC65F30F0187438F25770F503914CC2A325006821AEB33DE2A26370C9`, `RESULT=PASS`, captured Warning/Error `0/0`, 공식 GameplayScene SHA-256 불변이다.
- 현재 판정: `밸런스 수치 변경 없음 / Batch B destructive live integration 완료`. Batch B는 `36/40`; retarget transaction, support/p95/>4-cycle, unified mutation fence, active multi-facility retarget과 Batch C–H는 OPEN이므로 전체 밸런스 완료가 아니다.

### balance:v27:production-finite-lane-and-support-link-authority-v1

- 시대/역할: 모든 시대의 생산 작업대와 연결 지원 시설이 출력 버퍼 처리량 상한을 유한하게 증명하도록 하는 공용 작성 권위다.
- Before/After: Before는 workstation `146`개에 lane 수·동시성 정책이 없고 support instance가 한 workstation에 무제한 연결될 수 있었다. After는 수동 전용 `123`, AutomationMode 상호 배타 수동/자동 `23`, 수동 lane `1` 전수, 자동 lane `0/1=123/23`, support `28`개의 동일 정의 연결 상한 `1`을 명시한다.
- 물리 BOM·입력·출력: item kg, recipe input/output 수량, facility BOM과 물리 batch output 변경 `0`이다.
- Direct WU·Embedded WU/EWU: 작업 WU, 작업자 성능, EWU와 가격 변경 `0`. 이번 slice는 기존 singular worker/automation/batch 동작을 유한 작성 계약으로 승격하고 중복 support stacking만 차단한다.
- 시간·처리량: manual lane은 항상 `1`; automatic lane은 `AutomationMode.Automatic`일 때만 manual과 상호 배타로 `1`; PassiveBatch의 장시간 점유는 detached BatchProcessor의 `batchCapacity=1`이 소유한다. producer 전체 peak gram/hour 수치는 아직 재생성하지 않았다.
- 공간·전력·용수·폐기물: footprint·전력·용수·폐수·연료·정비 수치 변경 `0`. support 배치는 동일 방·호환 tag·거리·stable ID를 사용하되 동일 support definition 연결 상한을 초과하지 않는다.
- 위험·실패·회복: unspecified/zero/negative lane, zero link cap, invalid batch capacity와 nonfinite work-speed를 fail-loud한다. over-cap support는 임의 multiplier에 포함하지 않고 unlinked로 관찰된다.
- 대안: FacilityData.capacity나 requiredWorkers를 생산 lane으로 재해석하지 않는다. 이 값들은 방문/인력 권위이고 생산 실행 동시성과 의미가 다르다.
- 지배 전략·순환 차익 방지: 같은 support asset을 반복 건설해 한 workstation의 work/output/batch 처리량을 무제한 누적하는 경로를 닫는다. 경제 SCC·가격 차익은 Batch H에서 별도 전수 검증한다.
- 실행 경로: `BuildingSO lane/support authoring → ProductionWorkshopRuntime deterministic link → ProductionCycleUtilityService/ProductionOutputExecutionService → maximum factor catalog @4 → 후속 throughput envelope`다.
- 저장 권위: lane과 link cap은 immutable BuildingSO 권위다. runtime link는 building/room/grid revision에서 결정론적으로 재계산하며 저장 DTO에 복제하지 않는다.
- 자동 감사·전수: raw workstation/support `146/28`, invalid `0/0`, BatchProcessor `5`; production core의 content-ID별 신규 분기 `0`, unregistered capability `0`. 실제 canary는 focused duplicate-support spill fixture다.
- 검증: deterministic asset application `174→0`, second ForceReserialize byte-stable, `ProductionWorkshopDebugScenarios`, `ProductionMaximumOutputFactorCatalogDebugScenarios`, Unity compile과 Console `0/0` PASS. 증거 SHA-256 `A716E4D741D2F35D72A9E4B68FEC81B8959B65FD24AD8A06E3D05230D79F04BE`; GameplayScene SHA-256 불변.
- 현재 판정: `밸런스 영향 없음 / finite lane·support authoring 공식 검증 완료`. producer `92`개 전수 throughput, complete p95 profile/DI, capacity source `@5`, 원자적 attach/detach 재투영과 live `4.000/4.001` gate는 OPEN이며 Batch B는 `36/40`을 유지한다.

### balance:v27:production-lane-provenance-census-v2

- 시대/역할: 모든 시대의 생산 시설 lane 작성값을 live/save projection과 전수 census에 동일하게 귀속하는 구조 증거다.
- Before/After: Before는 lane policy/count가 `BuildingSO`에는 존재했지만 `ProductionFacilityHandle`, save-side capacity subject와 census에서 소실됐다. After는 immutable `ProductionFacilityWorkstationLaneCapacityProfile@1`을 Capture 경계에서 생성하고 이후 투영·복원 검증·census가 같은 profile과 digest를 사용한다.
- 물리 BOM·Direct WU·EWU·가격: item kg, BOM, recipe WU, EWU, 구매·판매 가격 변경 `0`이다.
- 처리량·공간: 시설 `146`개의 authored profile은 `manual-only 123 / mode-exclusive manual-or-automatic 23`이며 lane 수 자체는 변경하지 않았다. 이번 증거는 peak gram/hour나 출력 buffer 크기를 변경하거나 승인하지 않는다.
- 위험·실패: nonproduction handle만 explicit Empty profile을 허용하고 production capacity subject는 specified profile을 요구한다. raw SO를 downstream에서 재조회하거나 누락 lane을 추정하는 fallback을 두지 않는다.
- 실행 경로: `BuildingProductionWorkstationAbility -> ProductionOutputBufferCapacityProjector/ProductionAssemblyBridgeAdapter capture -> immutable handle/subject -> ProductionFacilityOutputCensus@2`다.
- 저장 권위: lane 수치는 SO 정의 권위이고 저장 DTO에 복제하지 않는다. save-side projector는 복원된 building definition에서 같은 immutable profile을 재캡처하며 digest drift를 검증 경계에서 관찰할 수 있다.
- 자동 감사·전수: 시설 `146`, manual-only `123`, mode-exclusive `23`, unclassified `0`, execution orphan `0`, 기존 별도 content gap `6`을 명시적으로 보존한다.
- 검증: Unity compile, `ProductionFacilityOutputCensusDebugScenarios`, contributor registry, maximum-factor catalog, clearance-profile catalog PASS; Console Warning/Error `0/0`; report/CSV second-run diff `0`. report SHA-256 `B8D720F222919D0094A644A23E8E20CA465E9BC3D87D5FE7853D04E37417ED34`, CSV SHA-256 `1F66C0C36A5DD6D961806AFFF7A9B67F947CFD53286FEF93F4AE8373D20F1E50`, 통합 증거 `Artifacts/QA/v27-production-lane-provenance-census-focused.txt`.
- 현재 판정: `밸런스 영향 없음 / lane provenance 구조 검증 완료`. runtime manual/automatic mode-exclusive admission, producer `92`개 throughput/profile, capacity source `@5`, support attach/detach 원자 재투영과 live `4.000/4.001` gate는 OPEN이므로 Batch B는 `36/40`을 유지한다.

### balance:v27:production-execution-mode-exclusion-v1

- 시대/역할: Automation을 지원하는 전 시대 생산 작업대의 수동 actor·자동 executor·detached passive processor 실행 권위를 서로 배타적인 authored lane으로 결속한다.
- Before/After: Before는 Automation tick이 Automatic에서만 실행돼도 수동 preview/Begin/Execute가 mode를 읽지 않았고, 자동 executor는 null worker로 worker policy에 거부되며 legacy Cook/Craft fallback이 gate를 우회할 수 있었다. After는 한 root-backed mode query와 공용 matrix가 세 실행 경계를 모두 통제하고 system executor를 명시적으로 구분한다.
- 물리 BOM·입력·출력: item kg, recipe BOM·출력량·FacilityBuffer gram 변경 `0`. 같은 bill/output owner가 실행 주체만 배타적으로 수용한다.
- Direct WU·Embedded WU/EWU·가격: authored WU, work-speed, EWU와 가격 변경 `0`. PoweredAssist는 기존 actor work multiplier를 유지하고 Automatic은 기존 automaticWorkPerSecond 경로만 사용한다.
- 시간·처리량: Manual/PoweredAssist actor lane과 Automatic executor lane은 대안이며 합산하지 않는다. PassiveProcessor는 장시간 batch processor의 detached lane으로 mode 전환과 무관하게 finishing-only 실행한다.
- 공간·전력·용수·폐기물: footprint·buffer·utility·연료·정비 수치 변경 `0`. mode query는 기존 Automation aggregate를 읽고 새 저장/캐시 권위를 만들지 않는다.
- 위험·실패·회복: automatic 상태의 manual preview/Begin/Execute, null system worker, legacy Cook/Craft fallback, allocated-worker 이동 구간의 mode switch, restore mode drift를 stable typed reason으로 fail-loud한다.
- 대안·확장성: 시설 ID switch나 handler별 mode 복제를 사용하지 않는다. 신규 생산 시설은 immutable lane profile과 기존 AutomationMode만 작성하면 공용 gate에 자동 편입되고, 신규 worker 종류는 명시적 authority enum을 추가하지 않는 한 거부된다.
- 지배 전략·순환 차익 방지: manual+automatic 동시 WU, mode switch로 reservation 탈취, legacy fallback으로 Automatic 우회, system worker의 actor skill/trait 위조를 금지한다. 경제 SCC 수치는 변경하지 않으며 Batch H tolerance `0` 증명은 계속 OPEN이다.
- 실행 경로: `BuildingSO lane profile + AutomationAggregateState → IAutomationExecutionModeQuery → ProductionBillSceneFacade preview/Begin/Execute → ProductionBillRuntime`; mode 변경은 `AutomationRuntime.SetMode → reservation/allocated-worker/bill owner preflight → aggregate write`다.
- 저장 권위: mode는 기존 Automation current-format save, bill/WIP는 기존 Production save, allocated worker는 transient world ownership이다. 신규 저장 필드와 과거 save migration은 없다.
- 자동 감사·증거: isolated mode matrix는 Manual/PoweredAssist/Automatic, manual-only automatic reject, legacy Manual allow/Automatic reject, allocated-owner transition reject와 restore exact를 검증했다. Production Workshop·Maximum Factor PASS, Unity Console `0/0`; 증거는 `Artifacts/QA/v27-production-execution-mode-exclusion-focused.txt`다.
- 현재 판정: `밸런스 수치 영향 없음 / compiled execution exclusion 하위 경계 완료 / live P15 evidence OPEN`. parent support row와 Batch B는 `36/40`을 유지한다.

### balance:v27:production-residual-process-loss-capability-v1

- 시대/역할: 전 시대의 deterministic Transform recipe가 gameplay 의미가 없는 절삭 분진·미세 offcut을 물리 아이템으로 강제하지 않으면서도 실제 WIP 질량 차이를 설명하는 공용 생산 capability다. 첫 current canary는 초기 섬유 활시위다.
- Before/After: Before는 `recipe:bowstring-fiber`의 `240g input → 80g output` 차이 160g이 정상 성공 영수증에 없었다. After는 RecipeSO의 opaque `process-loss@1` descriptor와 실제 WIP/output 기반 residual 계산이 160g CuttingWaste를 nonphysical DeclaredLoss line으로 동결한다.
- 물리 BOM·입력·출력: BOM `resource:shade-fiber ×2`, output `material:bowstring ×1`, unit grams `120/80`, quantity 변경 0. 물리 output은 80g이며 160g은 `physicalByproduct=false`인 추상 절삭 손실이라 item·FacilityBuffer·routing stack을 만들지 않는다.
- Direct WU: authored required WU, 작업자 속도, 생산 시간 변경 0. 기존 loom 실행 경로를 그대로 사용한다.
- Embedded WU/EWU·가격: item EWU, 입력 Ceil, 출력 Floor, 구매·판매 가격 변경 0. process loss는 무료 물질이나 회수 가치를 만들지 않으며 전수 SCC tolerance 0 재생성은 후속 Batch H gate다.
- 시간·공간: loom footprint·access·queue와 output buffer authored cycle 변경 0. actual physical batch 80g보다 facility portfolio 4,000g 권위가 커서 required capacity는 4,000g을 유지한다.
- 전력·용수·폐기물: 이 recipe의 clean water/wastewater는 0이며 utility 수치 변경 0. 160g을 wastewater나 바닥 폐기물로 위장하지 않는다.
- 위험: recipe ID switch, SO에 고정 residual gram 중복, output capacity에 nonphysical loss 포함, kg 변경 뒤 stale receipt, unknown/duplicate capability, 음수 residual, payload drift를 fail-loud한다.
- 대안: 모든 분진을 물리 waste item으로 생성하거나 무조건 input=output을 강제하지 않는다. gameplay 상 회수·운반·오염 의미가 있는 부산물만 physical byproduct로 두고, 의미 없는 손실은 typed abstract disposition으로 둔다.
- 지배 전략 방지: process loss는 회수·판매·재투입할 수 없고 물리 output·FacilityBuffer gram을 늘리지 않는다. frozen retry는 기존 batch/fingerprint를 재사용해 stack과 loss line을 중복 생성하지 않는다.
- 순환 차익 방지: 이번 slice는 recipe 질량 설명만 추가하고 BOM·output·EWU·가격을 바꾸지 않는다. 입력 Ceil/출력 Floor와 SCC margin tolerance 0은 별도 경제 전수 gate에서 유지한다.
- 실행 경로: `ProductionRecipeSO mass envelope → ProductionMassExplanationCapabilityRegistry → resolved physical output → exact WIP equation → prepared-output DeclaredLoss line → save/frozen retry → routing V8 nonphysical disposition tombstone → durable checkpoint GC`; physical line만 capacity/publication/haul route로 간다.
- 저장 권위: RecipeSO와 semantic digest가 descriptor 권위다. prepared-output schema 6이 frozen result를 소유하고, routing schema 8이 정상 cycle clear 이후 checkpoint 확정 전까지 exact loss gram·payload·fingerprint·line identity를 별도 tombstone으로 보존한다. 성공한 checkpoint GC 뒤 장기 감사 권위는 결정론적 V27 ledger artifact다.
- 자동 감사·실전 증거: pure registry, real loom adapter, prepared-output contract, routing V8 publish/save/restore/partial-route/drain/GC와 P03 full persistence가 PASS했다. 90g fixture의 1g 변조 restore는 거부됐고 `V27_PROCESS_LOSS_ROUTING_RECEIPT_FOCUSED_PASS`; recipe audit는 reviewed `43`, explanation missing `158`; CSV/report hash `E404AEC...AD68` / `830119...F94`, 2회 hash·length·mtime 불변, Console Warning/Error `0/0`, 공식 scene hash 불변이다.
- 현재 판정: `밸런스 수치 변경 없음 / representative process-loss resolve·save·frozen-retry·routing tombstone·audit 완료`. 절삭/offcut 106행 전수 검토·authoring과 kg/EWU/가격/6인 생존망은 OPEN이다.

### balance:v27:reviewed-cutting-loss-authoring-17-v1

- 시대/역할: 초기~후기의 침구·뼈 볼트·방한복·갑옷 안감·정밀 부품·목재 표지·칸막이·운반 멜빵·V22 의복/직조 가운데 절삭·재단·고형 offcut 의미가 명확한 deterministic Transform 17행이다.
- Before/After: Before는 17행의 양의 질량 차이가 `mass-balance-explanation-missing`이었다. After는 editor-only reviewed manifest가 각 RecipeSO에 동일한 opaque `process-loss@1 / CuttingWaste / cutting-dust-or-offcut` descriptor를 작성하고 runtime actual gram에서 residual을 계산한다.
- 물리 BOM·입력·출력: 17행의 기존 input/output item·quantity·unit gram 변경 `0`. residual은 회수·운반·오염 gameplay 의미가 없는 절삭 분진/미세 offcut이며 physical stack·FacilityBuffer·warehouse capacity를 만들지 않는다.
- Direct WU·Embedded WU/EWU: authored recipe WU, 작업자 성능, embedded EWU, 입력 Ceil·출력 Floor와 가격 변경 `0`. 이 기록은 질량 차이 귀속이며 경제 가치 재조정이 아니다.
- 시간·공간: 생산 시간·workstation footprint·access·queue·output buffer cycle 변경 `0`. nonphysical loss는 처리량·저장·Floor Clutter 면적에 포함하지 않는다.
- 전력·용수·폐기물: authored utility와 wastewater 변경 `0`. descriptor는 실제 clean-water/wastewater를 equation에 포함하되 절삭 손실을 폐수나 가상 item으로 위장하지 않는다.
- 위험·실패: ID-pattern 일괄 적용, 고손실률 kg/BOM 오류 은폐, recipe-ID runtime 분기, descriptor 충돌, 음수/0 residual, payload·fingerprint·routing total drift와 save 1g 변조를 fail-loud한다.
- 대안: 106행 전체를 동일 reason으로 닫지 않는다. 행별 감사로 안전 17, 고손실률 22, 혼합 2, 카딩 22, 다른 공정 12, BOM/회수 선행 31로 분리하고 후속 정책이 필요한 행은 OPEN으로 유지한다.
- 지배 전략·순환 차익 방지: abstract loss는 회수·판매·재투입할 수 없고 physical output을 늘리지 않는다. BOM·output·가격이 그대로이므로 신규 차익을 만들지 않으며 SCC tolerance 0 전수 증명은 Batch H에 남는다.
- 실행 경로: `editor reviewed-ID manifest → RecipeSO opaque descriptor → capability registry → actual WIP/output residual → prepared DeclaredLoss → routing V8 nonphysical tombstone → checkpoint GC`다. runtime/core에는 recipe ID 목록이 없다.
- 저장 권위: RecipeSO descriptor, prepared-output frozen line, routing V8 tombstone이 순서대로 권위를 소유한다. 성공한 durable checkpoint 뒤 장기 증거는 결정론적 recipe ledger artifact가 소유한다.
- 자동 감사·증거: 첫 asset apply `17`, 두 번째 `0`; recipe `355`, reviewed exact `60`, explanation missing `141`, external-input missing `80`, role mismatch `0`. CSV/report SHA-256 `9CE9BF472A1B3EAB27EF60A9BB568EE7177E8B46FBE3CE54CE4BC51AD24B513A` / `FDDC2ADB2ABA7F40C84A2579C9680479378D5B90D4C558013893A09D40283C45`, 별도 두 번째 실행 hash·length·mtime 변화 `0`, focused Unity PASS, Console Warning/Error `0/0`, GameplayScene hash 불변이다.
- 현재 판정: `밸런스 수치 변경 없음 / 안전 17행 설명 authoring 완료`. `bowstring-fiber`를 포함한 고손실률 22행의 kg/BOM 승인, 나머지 별도 capability/회수 정책, 전체 106 family와 전수 kg·EWU·6인 생존망은 OPEN이다.

### balance:v27:reviewed-fiber-processing-loss-authoring-16-v1

- 시대/역할: V22 원섬유를 원사로 만드는 수동·동력 방적 8재료×2경로의 카딩·세척·방적 찌꺼기 설명이다.
- Before/After: Before는 각 recipe의 240g input→160g yarn 차이 80g이 설명되지 않았다. After는 `process-loss@1 / FiberProcessingWaste / fiber-carding-and-spinning-waste` descriptor가 actual WIP/output residual 80g을 동결한다.
- 물리 BOM·입력·출력: cave silk, deep-goat wool, dreamweave, ember cotton, frost linen, frost wool, mire canvas, spore hemp의 기존 `raw ×3 → yarn ×2`, unit gram과 수량 변경 0. 80g은 gameplay 의미 없는 섬유 찌꺼기로 physical item을 만들지 않는다.
- Direct WU·Embedded WU/EWU·가격: 수동 18 WU·동력 9 WU를 포함한 authored 수치, EWU와 가격 변경 0. 수동/동력은 시간 대안이며 질량 수지는 동일하다.
- 시간·공간·utility: workstation, lane, footprint, buffer, 전력·용수·폐수 변경 0. nonphysical residual은 저장·haul·Floor Clutter를 소비하지 않는다.
- 위험·대안: 모든 섬유 행을 자동 승인하지 않는다. common wool 84.8%, shade cloth 55.6%, bowstring sinew 94.1%, wool cloth 61.9%는 kg/BOM 오류 가능성을 먼저 검토한다.
- 지배 전략·순환 차익: residual은 회수·판매 불가이며 physical output과 가격을 늘리지 않는다. 경제 SCC tolerance 0은 Batch H에서 별도 유지한다.
- 실행·저장 권위: `V22 builder → reviewed manifest → RecipeSO descriptor → generic process-loss capability → prepared receipt → routing V8 tombstone → checkpoint GC`; runtime recipe-ID 분기와 신규 save authority는 없다.
- 자동 감사·증거: 첫 apply 16, 두 번째 0; recipe 355, reviewed exact 76, explanation missing 125. CSV/report SHA-256 `C2312568D2C568D95E1644AF6B75CB7387C882D77C7F6D66EDB0D57C36CBF333` / `4E523920BC6C169C09CA49002E29AB2238E907C37FE66684480C393BC5FF1254`, 별도 2회 no-op, focused Unity PASS, Console Warning/Error 0/0, scene hash 불변이다.
- 현재 판정: `밸런스 수치 변경 없음 / 안전 fiber-processing 16행 설명 authoring 완료`. 고손실 섬유 6, 전체 106 family, kg/BOM/WU/EWU/가격과 6인 생존망은 OPEN이다.

### balance:v27:recipe-mass-explanatory-closure-runtime-proposed-split-v1

- 시대/역할: 전 시대의 물리 Transform recipe에서 게임적으로 허용한 공정 손실과 실제 물리 소유권·경제 무결성을 분리하고, 현재 runtime kg와 미적용 proposed kg를 동시에 감사하는 기준이다.
- Before/After: Before audit는 canonical proposed mass가 존재하면 live `ItemDefinitionSO.UnitWeight`보다 우선해, 미적용 kg를 현재 gameplay처럼 계산할 수 있었다. After schema v3는 proposed/runtime 입력·출력·기대값·residual·mass-creation을 별도 열로 기록하고 mismatch item ID를 보존한다.
- 물리 BOM·입력·출력: 이번 slice의 item kg·BOM·output quantity 변경은 `0`. 물리 질량은 input=output을 강제하지 않고 `physical input + typed external input = physical product + byproduct + terminal sink + typed abstract loss`로 설명 가능해야 한다.
- Direct WU·Embedded WU/EWU·가격: WU·EWU·가격 변경 `0`. ownership/WIP exactness, 입력 Ceil·산출 Floor, repeatable transform 최소 `-1mEWU`, SCC tolerance `0`은 완화하지 않는다. abstract loss는 회수·판매 credit을 만들지 않는다.
- 시간·공간·utility: 생산 시간, buffer, storage, haul, footprint, utility 수치 변경 `0`. nonphysical loss는 공간을 차지하지 않지만 gameplay 가치가 있는 고형 부산물은 물리 item으로 남겨야 한다.
- 위험·실패: 미적용 제안 kg로 현재 loss 승인, 고손실률로 BOM·단위 오류 은폐, tare 증발, 취소·파괴를 공정 손실로 기록, 의미 없는 폐기물 item 남발을 금지한다. descriptor recipe는 runtime/proposed 양쪽 exact equation이 닫히지 않으면 fail-loud한다.
- 대안·확장성: 닫힌계 질량 equality나 recipe-ID별 예외 switch를 사용하지 않는다. 신규 공정은 typed external input/Sink/loss capability를 등록하고 generic equation subject를 소비한다.
- 지배 전략·순환 차익 방지: 공정 손실 허용은 물리 output·회수량·판매가를 늘리지 않는다. 품질·해체·재제작·구매판매 SCC 검증은 tolerance `0`으로 별도 유지한다.
- 실행 경로: `ItemDefinitionSO runtime gram + canonical proposed semantic → recipe dual equation audit → mismatch dependency IDs → descriptor capability dual resolve → deterministic CSV/report`다.
- 저장 권위: 신규 gameplay save schema 없음. runtime kg는 ItemDefinitionSO, proposed kg는 canonical semantic proposal이 각각 source이며 CSV는 읽기 전용 review evidence다.
- 자동 감사·증거: 355 recipe 중 reviewed exact 67, proposed-only/runtime-mismatch reviewed 9, explanation missing 125, external-input missing 80, runtime/proposed mismatch recipe 23, proposed/runtime mass-creation 122/127이다. 9개 drift 중 5개는 runtime mass-creation candidate이며 family proposed change는 ready 0/blocked 7이다. Recipe CSV/report SHA-256 `51C7CFB340FC0B2DA2AAF536F1FF034258EF15B4F24B4847A49471DB7C267A3A` / `F7EB7BDFAD4025018D5D4439562BFF864EB086611C1B7488DDFE9F5CA6DF4211`; family CSV/report `23C2AEF5007CDEC8BF288B0CFD2C567938188FFE2C4AE71B4BF503CCB74967E0` / `0FD1353CFFBDB0BE09C6A4BFF246BA4BC74904621137F20C87634800DB47BEAA`; 별도 2회 hash·length·mtime 변화 0, Unity compile·Console Warning/Error `0/0`, 공식 scene hash 불변이다.
- 현재 판정: `밸런스 영향 없음 / runtime-proposed 감사 구조 교정 완료`. mismatch 23의 kg 적용, 고손실 22의 BOM·부산물 승인, 전체 EWU/SCC·운반·6인 생존 회귀는 OPEN이며 전체 밸런스 완료가 아니다.

### balance:systemic-content-event-catalog-design-v1

- 정의 ID·종류: 현존 생애 사건 32, 문화 관습 20, 축제 16, 계절 사건 28, 서비스 사고 8, 손님 요청 14, 세력 장 36, 세력 계약 18, 이정표 9, 전투 조우 36의 시스템 연결 계약, 야망이 참조하지만 `EventSpec`이 누락된 생애 사건 6종과 신규 이정표 후속 압력 9종이다. 현존 정의 전수 범위는 217개이며 누락·신규 정의를 합친 감사 목표는 232행이다.
- 정의·카탈로그·실행기 위치: 상세 설계 권위는 `docs/game-design/systemic-content-event-catalog.md`, 공용 결합 계약은 `docs/game-design/systemic-content-integration-design.md`다. gameplay code·ScriptableObject·asset·save schema 실행기는 이번 기록에서 변경하지 않았다.
- 등장 시대와 연구: 각 현존 정의의 기존 시대·연구·이정표 조건을 보존한다. 신규 압력 9종은 대응하는 이정표의 랜드마크 완공·운영 조건 영수증 뒤에만 해금된다.
- 플레이어에게 주는 새 결정: 실제 교육·수리·치료·조사·정책·운반·호위·전투·계약·상속 절차 중 무엇을 수행하고 어떤 물리·사회 책임을 남길지 선택한다.
- 목표 시스템 결합 등급과 근거: 자동 생애 기록 일부만 C1~C2, 반복 관습·소규모 사건은 C3~C4, 세계·세력·손님·계보·이정표 콘텐츠는 C4~C5다. C3 이상은 실제 도메인 과정과 해결 영수증, C4 이상은 영속 흔적과 역소비자, C5는 제도·경로·소유권·생태·세대 구조 변경을 요구한다.
- 발생 생산자와 실제 원인 증거: 인물 생애·관계·가구, 생산 실패 배치, 서비스 세션, 환자·오염, 기후·지도 셀, 야생 개체군, 세력 장·계약, 원정·전투, 랜드마크 운영 지표가 각각 후보를 생산한다. timer·random만으로 중요 사건을 확정하지 않는다.
- 실제 도메인 명령·진행·해결 영수증: 물자 예약·물리 이전, WU 작업 주문, 시설·방 정책, 서비스·의료 처리, 사회·계약 처리, 전투·원정 목표와 진행 세계 조건을 조합한다. 실제 도메인 commit 영수증이 생성된 뒤에만 generic mood·rapport·threat 요약 효과와 장기 trace를 확정한다.
- 영속 흔적과 역반응 소비자: 인물·가구·관계·고유 아이템 custody, 시설·방·경로·전력·수질, 환자·질병·야생 개체군, 세력 의무·원한·문서, 원정·적 적응, 이정표 압력을 각 기존 권위에 저장한다. 후속 생애·문화·계절·서비스·세력·계약·전투·이정표가 stable ID와 typed trace query로 읽는다.
- 물리 BOM·입력·출력: 현존 축제·관습·손님·세력 장·계약·랜드마크의 작성 수량은 변경하지 않고 카탈로그에 그대로 보존했다. 손님 물품은 플레이어 제공·요청자 반입 작업 대상·완료 보상·반환 담보로 분리한다. `guest-request:militia-arms`의 방패와 `guest-request:bodyguard-kit`의 방폭 외투 수량은 BOM·EWU 감사 전까지 OPEN이며 현재 설명과 단일 요구품의 불일치를 완료로 취급하지 않는다. 신규 압력 9종의 대응 물량은 실제 피해·대상 규모에 따른 기존 물리 작업으로 산출하고 고정 무료 비용을 두지 않는다.
- 직접 작업량과 계산 근거: 신규 WU 수치는 이번 문서 단계에서 배정하지 않았다. 모든 수리·교육·조사·조리·의식·건설·호송은 해당 기존 도메인의 작업량 계산기와 작업자·시설·경로를 사용해야 하며 즉시 완료 또는 별도 무노동 분기를 금지한다.
- EWU와 목표 회수 기간: 현존 EWU·가격·보상·계약 기한 변경은 0이다. 신규 압력과 물품 역할 교정의 기대비용·회복기간·장기 계약 손익분기는 구현 Wave별 Before/After 기록과 다중 시드 결과를 받은 뒤 승인한다.
- 공간·전력·물·연료·정비: 사건은 실제 방·병상·감방·작업대·저수조·경로·랜드마크와 기존 utility 용량을 예약한다. 별도 무공간·무전력 event capacity를 만들지 않으며 과부하·대기·축소·중단을 실제 결과로 남긴다.
- 위험·실패·회복 방식: context 대상 사망·이사·파괴·소멸, 예약 부족, 작업 중단, 기한 초과, notification 실패, save/restore, legacy context 부족과 후속 중복을 fail-loud 또는 명시적 실패·승계·취소 상태로 처리한다. 복구는 재예약·재수리·대체 경로·중재·재심·철수처럼 사건별 실제 작업이다.
- 사회·비가역 비용: 사망·부상·후유증·관계 기억·가구 이전·문화 모욕·후계 판정·세력 의무·경계·문서·고유 장비 custody·생태 개체군·공개 진실은 rollback용 숫자가 아니며 장기 trace로 보존한다.
- 기존 대안과의 장단점: 현재 generic scalar effect는 저비용이고 감사하기 쉽지만 원인·과정·물리 결과가 약하다. 새 계약은 구현·저장·QA 비용이 크지만 현존 물리 경제와 콘텐츠가 실제로 상호작용하고 서로의 결과를 다시 소비하게 한다. scalar effect는 도메인 해결 뒤 요약 결과로 계속 사용한다.
- 지배 전략 방지 조건: 선택 즉시 소비·보상, 창고 보유만으로 원격 납품, requester cargo의 플레이어 재고 선납, 취소·복원 중복 보상, 사건 반복으로 관계·평판 farm, 랜드마크 보상만 받고 고유 압력을 피하는 경로를 금지한다.
- 저장 권위와 실행 명령: 진행 episode는 선택·stable context reference·예약·단계·receipt reference만 소유한다. 물리 아이템, 인물, 시설, 의료, 야생, 세력, 전투와 랜드마크 상태는 기존 aggregate 권위에 남기며 content-ID switch 대신 등록된 typed operation capability를 사용한다.
- 자동 감사 ID와 전수 목록 포함 여부: 목표 감사 스키마는 `content_id, content_kind, coupling_grade, trigger_producer, bound_context_types, operation_types, completion_receipt_type, persistent_trace_type, reverse_consumer_count, physical_input_roles, save_authority, legacy_generic_effect_only, audit_status`다. 현존 217개, 누락 생애 정의 6개와 신규 압력 9개를 합친 232행 전수를 요구한다.
- 검증 매트릭스와 보고서 위치: 문서 단계에서는 상세 카탈로그의 현존 unique ID 정적 census가 `32/20/16/28/8/14/36/18/9`와 일치하고, 야망 완료 참조 6개가 실제 `EventSpec`에서 누락됐으며, 신규 압력 9개가 명시됐음을 확인했다. 첫 실행 검증은 `life-event:apprentice-mistake`, `service-incident:brawl`, `seasonal:winter-frozen-pipes`, `faction-contract:beastkin:supply` 네 canary의 예약 전·진행 중·commit 뒤·복원 뒤 결정론과 중복 0을 요구한다. 자동 보고서 위치는 구현 Wave 0에서 정한다.
- 현재 밸런스 상태: `문서 설계 완료 / 구현·수치 배정·자동 감사·실행 검증 OPEN / 밸런스 완료 아님`. 이번 작업에서는 컴파일, 테스트, Play Mode, Unity 명령을 실행하지 않았다.
### balance:v27:multi-output-economic-allocation-v1 — 2026-08-30

- 시대/역할: V27 전 시대 공통 다중 산출 경제 권위. Main·Byproduct·ReturnedPackaging·RecoverableWaste의 배치 비용 귀속을 recipe ID 분기 없이 해석한다.
- Before: 모든 positive output의 expected quantity를 합친 단일 분모와 동일 unit acquisition을 사용해 희귀 부산물과 Main의 역할을 지웠다.
- After: `weighted-output-share@1` descriptor를 semantic digest에 포함하고, line별 integer weight Floor와 유일 Main exact remainder로 batch debit을 보존한다. line/item per-unit acquisition은 Input Ceil이다.
- 물리 BOM·Direct WU·시간·공간·유틸리티: 이 기록에서 변경 `0`. 기존 batch debit을 출력 사이에만 재귀속한다.
- 내재 EWU: logging은 log `4,159`/dark-resin `23,100`; quarry는 stone `5,135`/coal `25,665`/iron `32,082`/gold `171,100`/mana `513,300`; saltstone은 stone `4,528`/saltstone `18,112 mEWU/개` 감사값이다.
- 위험/대안: market price 기반 allocation은 가격↔EWU 순환 때문에 금지한다. Source의 amount-weight 기본값 외 multi-output Transform은 명시적 capability authoring 없이는 실패한다.
- 악용 방지: `sum allocated debit == batch debit`, 반환 포장은 0가중치·비취득 후보, 판매·회수는 Output Floor, SCC tolerance `0`을 유지한다.
- 실행 경로: `ProductionRecipeSO.OutputCostAllocation → ProductionOutputCostAllocationCapabilityRegistry → V27EmbeddedWorkValueCalculator.OutputCosts/OutputItemValues → fixed-point item acquisition`.
- 저장 권위: ScriptableObject descriptor가 작성 권위다. 계산 breakdown과 focused artifact는 파생 증거이며 gameplay save 대상이 아니다.
- 자동 감사: focused PASS, reverse-order deterministic PASS, ReturnedPackaging 보존 PASS, artifact SHA-256 `F08A706DF53C86FC73BD0755213446FC6473F272DCA80D23448CE53C1E81F17B`. 통합 whole-game audit는 stale output-capacity PlayMode evidence 갱신 뒤 재실행한다.
- 후속 전수 증거: world-resource·crop output-capacity evidence를 현재 marker로 갱신했고 dependency review `86,933`행, SCC `297`, minimum margin `-23,142,719mEWU`, integrity failure `0`까지 생성했다. `CriticalDensityUnresolved` 건설 후보는 WU/BOM/투자 bounds 안의 최선값만 제시하고 자동 적용하지 않는다.
- 경고·승인 상태: unresolved root `332`, collapsed `39`. 이 중 건설 노동밀도 root는 `building:9303`, `building:9502` 두 건이며 실제 `construction-authored-wu:redistributed` approvalKey와 연결된다. 나머지 recipe 노동 root `328`건은 source digest 변경으로 기존 exact approval이 stale해져 OPEN이다.
- 결정론 증거: focused construction artifact SHA-256 `BA67D6F863FE15ECF393DB037A97C8C6E2E1E8B1903B8132AC22BC5E21E93977`; 안정화 후 전수 CSV·audit·anomaly graph·source inventory·manifest 연속 생성 hash·length·mtime 변화 `0`. manifest는 이 baseline ID를 포함한다.
- 현재 판정: `밸런스 공식 검증 진행 중 / 전수 dependency·SCC integrity PASS / unresolved exact review 332`. authored 건설 WU·BOM, item kg·가격·saleRate·계약·mending-scrap·offcut 수치는 이번 후속에서 변경하지 않았다.
- 2026-08-30 superseding attribution evidence: 반복 작업의 의도적 `2.25 project-scale 제거`와 파생 노동 밀도 변화를 한 `direct-wu` 행에 합치던 결함을 제거했다. same-route density Before는 frozen per-batch WU를 사용하고 별도 `labor-density-ratio` 행이 입력 acquisition 변화만 재귀 귀속한다.
- 새 전수 결과: `87,284`행, unresolved root `31`, collapsed descendant `172`, SCC `297`, integrity failure `0`. root 31은 파생 경제 root 29와 자동 미적용 `building:9303/9502` 건설 root 2다. 이는 이전 `332/39` 상태를 대체하지만 strict 재생성 완료를 의미하지 않는다.
- 승인 재검증 증거: exact Before/After·dependency·reason·baseline·이미 적용된 asset 값이 모두 같은 항목만 `310 unchanged + 1,061 revalidated`로 이관했고 `565` obsolete key를 만료했다. 신규 After 승인은 `0`이다.
- strict 재검증은 `building:1011 material:treated-lumber`의 previous-applied `6`과 새 optimizer candidate `5`(historical Before `4`)를 한 상태로 취급하는 모델 결함에서 fail-loud했다. 다음 단계는 previous-applied custody와 recalibration candidate를 분리하는 것이며, 이번 단계에서 건설 BOM/WU 또는 다른 authored balance 값은 변경하지 않았다.
- 2026-08-30 superseding construction-custody evidence: 기존 canonical metric은 historical `4→6` approval과 exact current asset을 그대로 소비하고, 새 `6→5` optimizer 결과는 `construction-recalibration-candidate-material:material:treated-lumber` review-only root로 분리했다. 후보는 `assetApplied=false`, 빈 approvalKey, `pending-explicit-review`, derived proposal authority를 가지며 generic ApplyApproved로 적용될 수 없다.
- focused 증거: current ledger의 previous-applied/recalibration candidate `27`건이 모두 `candidate.Before == applied.After`, exact property identity, historical 50% BOM bound, approval-key 분리를 통과했다. `building:1011` 행은 exact `4→6 applied / 6→5 candidate`이고 GameplayScene hash는 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다.
- 새 strict 감사는 건설 drift 예외를 통과해 `87,311` rows, Critical `41`, collapsed `189`, SCC `297`, minimum margin `-23,142,719mEWU`를 생성했지만 item-market/market-consumer의 기적용 권위와 신규 계산 후보를 아직 한 상태로 취급해 integrity failure `337`로 종료했다. 따라서 현재 판정은 `밸런스 공식 검증 진행 중`이며 strict PASS나 전수 밸런스 완료가 아니다.

### balance:strategic-build-and-progression-design-v1 — 2026-08-30

- 정의 ID·종류: 연구 180개와 런타임 시설 전수를 식량, 물·위생, 전력, 물류, 방어, 의료, 인구, 대외경제의 8개 문제 영역과 대체 해법 포트폴리오로 분류하는 문서 설계다. QA 기준 조합은 봉인 생태 공동체, 지하 균사 도시, 환대 교역 연방, 요새 산업국, 원정 병기국, 비전 계보 성역 6개이며 고정 클래스나 시작 잠금이 아니다.
- 정의·카탈로그·실행기 위치: 상세 설계 권위는 `docs/game-design/strategic-build-and-progression-design.md`, 현존 ID·작업량·직접 해금 권위는 `docs/DungeonStory_Game_Design_and_Implementation.md`, 수치 권위는 이 기준서다. gameplay code, ScriptableObject, asset, scene, save schema는 이번 기록에서 변경하지 않았다.
- 등장 시대와 역할: 1~30일 탐색, 30~120일 첫 약속, 120~240일 전문화와 첫 Legacy, 240~400일 혼합·전환, 400~960일 문명화, EndlessAge 완전 수렴을 관찰 창으로 사용한다. 날짜가 콘텐츠를 잠그지 않으며 실제 연구·시설·생산·인력·계약 영수증이 전략 상태를 투영한다.
- 플레이어에게 주는 새 결정: 같은 생존 문제를 어떤 생산망·공간·유틸리티·인력·외교 방식으로 해결할지, 어떤 공용 전략재와 연구 시간을 먼저 묶을지, 약점을 보완할지 주력을 바꿀지 선택한다. 한 계열을 고르면 다른 계열을 영구 잠그지 않지만 첫 Legacy 전의 의미 있는 기간 동안 지연시킨다.
- 목표 시스템 결합 등급과 근거: 연구 분기와 시대 변화는 C4, 이정표와 세대·세력·지역 공급망까지 구조를 바꾸는 포트폴리오는 C5를 목표로 한다. 연구 완료 플래그만 보지 않고 실제 생산·서비스·시설망·숙련·계약 영수증과 사건의 역반응을 사용한다.
- 물리 BOM·입력·출력: 현존 연구·시설·조합식·품목의 BOM, 수량, kg와 custody 변경은 `0`이다. 교역·계약·원정 조달은 기존 물리 이전·호위·전투·보상 영수증을 사용해야 한다. 포트폴리오별 전수 BOM 비교는 Wave 0 분류 뒤 OPEN이다.
- 직접 작업량과 계산 근거: 현존 연구 총 WU `138,824`와 개별 연구·건설·생산·서비스 WU 변경은 `0`이다. 계획된 대형 전환의 안정 처리량 도달을 최소 한 계절 `30일` 문제로 만든다는 값은 시뮬레이션 전 목표이며 승인 수치가 아니다. 별도 무료 전환 WU나 전략 통화를 추가하지 않는다.
- EWU·가격·회수 기간: 현존 item EWU, 구매·판매 가격, 계약 보상과 회수율 변경은 `0`이다. 계열별 투자 회수 기간과 기준 빌드 Before/After는 연구 WU, 건설 EWU, 비축, 인건비, utility, 사고·손실을 함께 계산한 뒤 승인한다.
- 시간·공간·전력·물·연료·정비: 기존 시설 footprint와 utility 수치 변경은 `0`이다. 선택 비용은 외부 농지와 내부 재배 공간, 중앙·분산 창고, 배관·전력·환기 토폴로지, 증기 연료, 수차 입지, 마나·냉각, 자동화 정비처럼 현존 물리 조건에서 나온다. 무공간·무전력·무정비 전략 용량을 만들지 않는다.
- 종족·문화와 사회 비용: 슬라임의 청결·물, 오크의 식량·공동 공간, 뱀파이어의 혈액·암실·개인 공간, 코볼트의 밀집 작업장, 균사체의 습윤·저조도, 하피의 고저차·환기, 골렘의 전력·정비 등 현존 생리·문화 요구를 운영 압력으로 사용한다. 직접 생산량 퍼센트 보너스를 새로 만들거나 종족별 빌드를 잠그지 않는다. 계약 의무, 동화 120일, 시설 진화, 숙련 계보와 고유 장비 역사는 즉시 전환으로 초기화하지 않는다.
- 위험·실패·회복 방식: 단일 지배 빌드, 같은 순서의 연구 체크리스트, 이름만 다른 대안, 자동화의 무비용 노동 삭제, 교역·원정 무료 조달, 전환 시 계약·공간·숙련·진화 초기화, 종족 강제 빌드를 금지한다. 회복은 배급, 수동 운반·수공업, 긴급 수입·세력 원조, 부품 회수·수리, 부하 차단, 원정 철수 같은 저효율 실제 경로를 사용한다.
- 기존 대안과의 장단점: 영구 연구 잠금은 선택을 선명하게 하지만 960일 이상 장기 문명과 전 연구·전 이정표 달성 원칙을 훼손한다. 아무 잠금도 비용도 없는 완전 개방은 체크리스트가 된다. 본 설계는 실제 매몰비용으로 초중반 분기를 만들고 후기 수렴을 허용하지만, 전수 태깅·UI·사건·시뮬레이션 비용이 크다.
- 지배 전략·순환 차익 방지: 주요 문제 영역마다 최소 3개 실질 해법, 해법 간 비용 차원 2개 이상, `Fallback`과 `Bridge`를 요구한다. 어떤 기준 빌드도 모든 환경에서 생산 안정성·생존·첫 이정표 시간이 동시에 1위여서는 안 된다. 외부 보상과 회수는 기존 물리·경제 보존 규칙을 우회하지 않는다.
- 체크리스트 방지 목표: 첫 Legacy에서 미완료 연구 `54개 이상(30%)`, 미건설 포트폴리오 시설 `분류 대상의 35% 이상`, 120일까지 주력 문제 영역 `2~4개`, 환경·시작 조건 간 핵심 건설 순서 Jaccard `0.75 이하`, 대형 전환 안정화 `30일 이상`을 제안한다. 이 값들은 모두 OPEN이며 다중 시드 Before/After 승인 전 밸런스 수치가 아니다.
- 실행·저장 권위: 초기 포트폴리오 상태는 완료 연구, 가동 시설·망, 최근 물리 생산·소비, 기여 WU·숙련, 계약·의무, 진화·계보 영수증에서 계산하는 read-only 투영이다. 기존 Aggregate가 원본 상태를 계속 소유하며 UI·추천·빌드 이름은 권위가 아니다. 별도 저장이 필요하면 원본 복제 대신 stable receipt reference와 결정론적 source digest만 보존한다.
- 자동 감사 ID와 전수 목록: 목표 스키마는 `content_id, content_kind, problem_domains, solution_family_ids, portfolio_roles, mandatory_kernel, physical_inputs, direct_wu, embedded_wu, space_profile, utility_profile, workforce_profile, external_dependency, failure_modes, counter_pressure_ids, bridge_targets, switch_out_path, fallback_path, milestone_ids, skippable_before_first_legacy, source_authority, save_authority, execution_path, audit_status`다. 연구 180개·시설 전수·이정표 9개·문화 10개·생산망 대체 계약 전수 분류는 OPEN이다.
- 검증 매트릭스와 canary: Wave 1은 식량의 외부 농업/지하 생태/교역 조달, 전력의 수공업/증기/수차·마나 전환, 방어의 복도 요새/기동 민병대/동맹 방위를 canary로 삼는다. 1/30/120일과 첫 Legacy에서 연구·시설·재고·인력·실패 원인, 저장 복원, 동일 seed hash를 비교한다. 이후 6개 기준 빌드와 6개 환경 프로필을 120/240/400/960일 다중 시드로 검증한다.
- 문서 정적 증거: 상세 문서는 630행이며 완전한 `research:*` 참조 16개가 모두 현존 종합 문서에 존재함을 정적으로 확인했다. 컴파일, 테스트, Play Mode, Unity 명령은 실행하지 않았다.
- 현재 밸런스 상태: `전략 포트폴리오 문서 구조와 구체 후보 설계 완료 / 연구·시설 전수 분류·수치 승인·구현·자동 감사·다중 시드 검증 OPEN / 밸런스 완료 아님`.
- 2026-08-30 `balance:v27:market-live-candidate-custody`: 시장 수치의 Authority는 현재 ScriptableObject와 downstream authored property이며, 새 계산값은 별도 review-only ledger row로 기록한다. 이번 변경은 원장 귀속 구조만 교정했다. 직접 후보는 unit price 206, sale rate 175, stock cost 6, retail cost 2, guest reward 6으로 총 395개이며 모두 exact approval provenance가 없어 `market-authority-provenance-missing` Critical이다. current row는 live observation을 표시하고, 승인 baseline은 approved asset row가 소유한다. 후보는 빈 approval key·`assetApplied=false`·`pending-explicit-review`다. 현재 unitPrice/saleRate 기반 live sale credit과 후보 기반 derived sale credit 175개도 분리했으며 derived row는 독립 승인·적용할 수 없다. full AuditOnly 두 번은 `87,881` rows, integrity failure `0`, Critical `436`, collapsed `189`, SCC `297`, minimum margin `-23,142,719mEWU`와 byte/hash/mtime identity를 증명했다. CSV/audit/manifest SHA-256은 각각 `A2B4EEB01EBE70D7E694576CE0E3675EF0FAD150874A4EB185107D6AF4FD1680`, `6466D4354D6AB6FC64DAE21700158C371A56CF2F513C231D8A39F24265EF94AA`, `2003B158823CA57D0492981BF2F4ECC624F41CFAE24A2B5CBE5EA52144FA5EE3`다. 질량 정책은 의도된 생성·소모·수율 차이를 typed external input/byproduct/terminal sink/abstract loss로 설명한다. 이미 생성된 lot/WIP/carry의 소유권·수량 exactness와 EWU SCC 무차익은 strict하게 유지한다. authored kg·BOM·WU·EWU·가격·saleRate·보상·SO mutation은 `0`, Console Warning/Error `0/0`, GameplayScene SHA-256은 `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`로 불변이다. 현재 판정은 `밸런스 공식 검증 진행 중 / REVIEW_REQUIRED`이며 436 Critical과 256-seed가 OPEN이다.
- 2026-08-30 `balance:v27:market-authority-classifier-v1` supersedes the row-count evidence in `balance:v27:market-live-candidate-custody` without changing its custody policy. A pure typed classifier now distinguishes `Implemented`, `LegacyReconstructedBaseline`, `PreviouslyApprovedApplied`, `MissingProvenance`, and `UnauthorizedDrift`. Exact V23 reconstruction proves 58 legitimate current baselines (unit price 55, guest reward 3); these are ordinary pending Before→After rows, not approvals. The unresolved provenance set is therefore the original 337 rows: unit price 151, sale rate 175, stock cost 6, retail cost 2, guest reward 3. Derived sale-credit candidates remain 175 and cannot be independently approved. The focused gate covers 512 direct+derived review rows plus the exact 58-row legacy classification. Two full AuditOnly runs are byte/hash/mtime-identical at `87,823` rows, integrity `0`, Critical `378`, collapsed `189`, SCC `297`, minimum margin `-23,142,719mEWU`. Final hashes are CSV `ED12116D5F024DC9E851990DA2FA587B5426F9044DFB46F2B8E07DF0E534C94F` (`68,372,513 bytes`), audit `734CAE0F3B768606DCCBA7F4C257C40F7F4BB957478FEA63722B7CE113BD506B` (`77,705 bytes`), manifest `77BEDD3EAC275149A3CDA469837149A2C4EB4F0866A5EE269F76D4C05129E6F9` (`5,583 bytes`). No authored kg/BOM/WU/EWU/price/saleRate/reward/SO value was changed. This remains `REVIEW_REQUIRED`, not balance completion.
- 2026-08-30 `balance:v27:market-authority-classifier-approval-custody-v1`: the classifier regression changed 8 approval keys for semantically unchanged, already-applied market-consumer rows. Atomic semantic revalidation retained `1,363`, revalidated `8`, expired `0`, kept total approvals `1,371`, and approved `0` new candidate values. Approval SHA-256 is `A09A1B282A568F5B2D46F3878342C3F646D608CC8720B04CAC6A57F1362E665A`. Post-revalidation strict AuditOnly passed the focused custody gate twice with unchanged row/anomaly/SCC counts (`87,823 / 378 / 297`) and second-run byte/length/mtime identity. Final CSV/audit/manifest SHA-256 is `17ECA843A9D724DDDDC36902B76BCFEBA76F38529E4A0DFACA7C5AA159541A9C` / `734CAE0F3B768606DCCBA7F4C257C40F7F4BB957478FEA63722B7CE113BD506B` / `62FA03B4FB693DCB5169F8C499CD8B54E140FAFE67520C24B90F4CA40BCB5F94`. Authored values and GameplayScene were not mutated.
- 2026-08-30 `balance:v27:market-candidate-sensitivity-v1`: no market candidate is bulk-approved. Across 206 unit-price candidates the median absolute delta is `50.34%`, 62 exceed `100%`, and 22 exceed `300%`; `resource:mana-crystal` is `4→172 gold` and `stock:mana` is `4.20075→230.98495 gold/item`. Current live sale credit exceeds acquisition cost in `0` rows but exceeds the 60% recovery target in `34` rows. These are review evidence, not authored changes. Rare Source allocation, market liquidity/accessibility, stock procurement and downstream contracts must be reviewed together before price promotion.

### balance:v27:market-custody-causal-root-v2

- 정의 ID·종류: market authority custody와 dependency anomaly attribution의 구조 교정이다. 콘텐츠 정의·아이템·레시피·시설을 추가하지 않았다.
- 등장 시대·연구: 모든 기존 시대·연구 조건을 유지하며 변경 `0`이다.
- 역할: 현재 authored market 값을 관측값, 과거 exact approval을 적용 custody, 새 계산값을 review-only candidate로 분리하고 collapsed 경제 변화가 정확한 causal review metric 아래 접히게 한다.
- Before: candidate `337`개 전부 `market-authority-provenance-missing`, Critical `378`, collapsed `189`, approved root `35`, approval `1,371`이었다. 같은 stable ID의 첫 approvable metric을 고르면서 무관한 saleRate `12`개가 root로 승격됐다.
- After: existing-applied custody `162`와 unproved saleRate `175`를 분리했다. Critical `366`, collapsed `201`, approved root `47`, approval `1,533`; review-only candidate `337`은 그대로 OPEN이다.
- 물리 BOM: item·recipe·facility BOM, output quantity, packaging, byproduct 변경 `0`이다.
- Direct WU: recipe·construction·research authored WU 변경 `0`이다.
- Embedded WU/EWU: acquisition/recoverable/SCC 계산식과 값 변경 `0`; 최소 SCC margin `-23,142,719mEWU`, integrity failure `0`이다.
- 시간: 생산 주기·계약 기한·연구 기간·게임 하루 길이 변경 `0`이다.
- 공간: footprint, FacilityBuffer, warehouse capacity, dungeon expansion 변경 `0`이다.
- 전력·용수·폐기물: utility authoring과 process disposition 변경 `0`이다.
- 위험: 과거 approval을 신규 후보 승인으로 오인하거나, source digest drift를 무조건 승계하거나, 비원인 saleRate를 승인 root로 쓰는 것을 금지한다. 175 saleRate는 exact provenance가 없으므로 자동 복구하지 않는다.
- 대안: 162건을 다시 사람에게 현재값 baseline부터 검토시키는 방법은 안전하지만 불필요한 중복 판단이다. exact prior After와 현재 token이 같은 경우만 custody를 복구하고 새 후보 판단은 별도로 남기는 방법을 선택했다.
- 지배 전략 방지: live sale credit/acquisition 위반 `0`, 직접 buy→sell 차익 `0`; 60% 회수 목표 초과 `34`는 OPEN이며 quarry/mana 후손을 개별 승인하지 않는다.
- 순환 차익 방지: 후보 sale credit `175/175`는 60% 이하이고 독립 approval key를 갖지 않는다. 구매·제작·판매·해체 SCC tolerance는 계속 `0`이다.
- 실행 경로: `historical exact approval evidence → current candidate Before join → semantic revalidation → canonical approval file → AuditOnly causal-root selection → anomaly collapse`다. Git은 일회성 evidence source일 뿐 production/runtime dependency가 아니다.
- 저장 권위: ScriptableObject market 값은 변경하지 않았다. 승인 권위는 `docs/game-design/v27-balance-critical-approvals.json`, review 후보는 결정론적 ledger이며 save schema 변경 `0`이다.
- 자동 감사·실전 증거: recovered `162`, new candidate approval `0`, provenance-missing `175`, candidate authority violation `0`. strict 2회는 rows `87,823`, Critical `366`, collapsed `201`, approved `47`, SCC `297`, integrity `0`과 CSV/audit/manifest hash·length·mtime identity를 통과했다. hash는 `D702D1616318254714B38C783367EDCBD78822E773591EA36650F0C0FB8D5A59` / `89F46407A72017A8D97A72096F0495C4CDC168EF38CCFBEC709C23F87C6EF9F` / `3CB8A93718B5DB6F6938A0863175BF513AF7C4FBA8C5CF8BCB69EB7F1E790817`; approval hash `C896179A221C13902AD118F3C00E94B89AD7707F15D18DFF68B2619CA0D4A891`, Console Warning/Error `0/0`, GameplayScene hash 불변이다.
- 현재 판정: `밸런스 공식 검증 진행 중 / REVIEW_REQUIRED`. 시장 337과 건설 29의 명시적 bundle review 및 256-seed가 OPEN이며 밸런스 완료가 아니다.

### balance:v27:market-review-atomic-bundles-v1

- 정의 ID·종류: 시장 직접 후보 337행을 exact item anchor 234개로 묶는 review-only 원장 구조다. `P/R/S/T/G`는 각각 price, sale rate, stock, retail, guest reward authority를 뜻한다.
- 등장 시대와 연구: 기존 item·stock·retail·guest request의 시대·연구 조건을 변경하지 않는다.
- 역할: 시장 수치를 자동 승인하지 않고 함께 판단해야 할 exact property와 upstream 경제 계열을 분리해 리뷰 누락·wildcard 승인을 방지한다.
- Before·After: 모든 candidate의 Before/After token은 기존 V27 ledger를 그대로 사용하며 이번 기록에서 authored 값은 변경하지 않는다.
- 물리 BOM: 변경 없음. bundle family는 acquisition/cultivated acquisition과 recipe direct-WU physical input graph만 읽는다.
- Direct WU·Embedded EWU: 변경 없음. quarry 직접 후보 180행, mana 하위 84행은 영향 범위 태그이며 approval 단위가 아니다.
- 시간: artifact 생성은 현 전수 감사 포함 약 3~4분이며 serializer 결정론 검증은 frozen-ledger 전용 경로로 분리할 예정이다.
- 공간·전력·용수·폐기물: 변경 없음.
- 위험: 337행 일괄 승인, family wildcard 승인, downstream consumer 고아, derived sale credit 독립 승인을 모두 금지한다.
- 대안: price/rate/consumer를 개별 행으로만 보여 주는 방식은 원자적 결정을 놓치므로 item anchor bundle과 family tag를 함께 사용한다.
- 지배 전략 방지: live 회수율 60% 초과 item 34개를 명시하고 buy/sell·SCC 무차익 검증은 tolerance 0으로 유지한다.
- 순환 차익 방지: 이번 slice는 수치를 적용하지 않으며 기존 SCC 297, minimum margin `-23,142,719mEWU`를 보존한다.
- 실행 경로: `AuditOnly FrozenBalanceLedger → exact candidate capture → item anchor join → economic causal family traversal → deterministic CSV/report`다.
- 저장 권위: gameplay save 변경 없음. CSV/report는 review evidence이며 approval authority가 아니다.
- 결정론·자동 감사: rows 337, bundles 234, shape `PR87/R80/P58/RS3/PRS2/PT1/GPRS1/GPR1/GPRT1`, orphan 0, duplicate 0을 fail-loud로 고정한다.
- 검증 증거: dependency/source/semantic/bundle digest 누락·bundle 내부 불일치 0. CSV SHA-256 `83BE11C00EDF60B1398C75A2284A525F496FFA1363E7A12F8EF214343FCE5F46`/293,481 bytes, report `D26D7CDCB4A4A7334B5C50BF9BB1AF022F9B248BDCB61F2EC1AEFD522361339A`/650 bytes, 2회 hash·length·mtime 변화 0, Unity compile PASS, Console Warning/Error 0/0, GameplayScene hash 불변이다.
- 현재 판정: 검토 입력 구조 완료, gameplay balance decision OPEN. 126.10 이후 남은 Critical은 시장 337개이며 진행률 A 31/31, B 36/40, C 8/39, A-C 75/110, whole A-H 1/8은 변하지 않는다.

### balance:v27:construction-current-authority-boundary-enumeration-v1

- 정의 ID·종류: 건설 optimizer의 historical bound와 current-approved preference를 분리하고 밀도 경계 WU를 열거하는 계산 계약이다.
- 역할: 이미 승인된 BOM/WU를 불필요하게 되돌리는 rebase 후보와 경계 누락 거짓 Critical을 제거한다.
- Before·After·BOM·WU: gameplay authored mutation 0. historical Before는 상한/투자 기준, current approved 값은 최소 변경 기준으로 유지한다.
- 시간·공간·utility: 시설 건설시간·footprint·처리량·전력·용수·폐기물 값은 변경하지 않는다.
- 위험·대안: current 값을 무조건 유지하지 않는다. 투자 ±2%, WU 1.5–2.25, density 0.67–1.50을 벗어나면 기존 Critical 경로를 유지한다.
- 지배 전략·순환 차익: BOM 감소·재건 회수 차익을 새로 만들지 않으며 current-approved 수량을 그대로 보존한다.
- 실행 경로: `historical bound + current approved authority → exhaustive BOM enumeration → target/current/density-boundary WU enumeration → minimal current-property change`다.
- 저장 권위: save schema 변경 없음. BuildingSO current scalar/BOM과 approval receipt가 권위다.
- 자동 감사·증거: 건설 Critical 29→0, full rows 87,796, remaining market Critical 337, integrity 0. impossible fixture는 Critical 유지. full CSV `25507F2CCABC8F163A1D35462BAAD61481C7C0C710A57706FA1DB2CC00BEF45B`, audit `1AA486FF6D072F927E9CF2226CCA7CC994F7BC8620F363D15A01159286D661A0`, 두 번째 실행 no-op, GameplayScene hash 불변이다.
- 현재 판정: 건설 후보 생성 결함 해소 완료 / 시장 337 OPEN / 전체 밸런스 완료 아님.

### balance:v27:market-review-recommendation-unpriced-inflow-v1

- 정의 ID·종류: market item bundle 234개의 review recommendation과 무상 외부 유입 위험 분류다.
- 역할: formula-safe 후보와 gameplay inflow exploit을 분리하며 추천을 승인으로 오인하지 않는다.
- Before·After·BOM·WU: authored 값 변경 0. 후보 exact token과 bundle digest만 읽는다.
- 시간·공간·utility: 변경 없음. faction cargo cooldown은 위험 산정 입력으로만 사용한다.
- 위험: mana 후보 적용 시 무상 Trade/Supply cargo 즉시 판매가 약 60.20 gold/day가 되어 지배 전략을 만든다.
- 대안: mana EWU를 낮추는 임시 cap 대신 generic route payment/obligation/resale/value-cap 정책을 먼저 연결한다.
- 지배 전략·순환 차익: mana 46 bundle을 rework로 격리하고, candidate credit 2배 이상인 비-mana 10 bundle도 availability/sink 검토 전 적용하지 않는다.
- 실행 경로: `market atomic bundle → economic causal family → live/candidate sale-credit ratio → recommendation only`다.
- 저장 권위: gameplay save 변경 없음. recommendation artifact는 approval authority가 아니며 actual decision은 전부 pending이다.
- 자동 감사·증거: promote 178, mana rework 46, non-mana double rework 10, 빈 추천/이유 0, decision mutation 0. CSV `83BE11C00EDF60B1398C75A2284A525F496FFA1363E7A12F8EF214343FCE5F46`, report `D26D7CDCB4A4A7334B5C50BF9BB1AF022F9B248BDCB61F2EC1AEFD522361339A`, 2회 no-op, Console 0/0, scene hash 불변이다.
- 현재 판정: recommendation 구조 완료 / 사용자 정책 결정과 faction economic policy OPEN / 전체 밸런스 완료 아님.

### balance:v27:market-reviewed-safe-promotion-v1

- 정의 ID·종류: 시장 review bundle `234`개 가운데 현재 공급 경로와 회수율에서 안전 판정된 `178`개 bundle의 exact authored market promotion이다. 신규 콘텐츠 정의는 추가하지 않았다.
- 등장 시대·연구: 기존 item·stock·retail·guest request의 시대·연구·해금 조건을 그대로 유지한다.
- 역할: 추천과 승인을 분리한 뒤 exact decision authority로 검토된 unit price, sale rate, stock cost, retail cost와 guest reward만 원자 적용한다.
- Before·After: 적용 대상은 `183`개 에셋의 `238`개 property이며 unit price `109`, sale rate `120`, stock daily unit cost `5`, retail cost `2`, guest reward `2`다. exact Before/After token은 `docs/game-design/v27-balance-market-review-decisions.json`의 member record가 권위다.
- 물리 BOM·입력·출력: recipe BOM, output quantity, package tare, physical lot과 FacilityBuffer 값 변경 `0`이다.
- Direct WU: recipe·construction·research·운반 WU 변경 `0`이다.
- Embedded WU/EWU: V27 acquisition/recoverable 계산과 SCC 알고리즘은 변경하지 않았다. SCC `297`, minimum margin `-23,142,719mEWU`, tolerance `0`을 유지한다.
- 시간: 생산 주기·계약 기한·route cooldown·게임 하루 길이 변경 `0`이다.
- 공간: footprint, warehouse/FacilityBuffer gram capacity, dungeon usable area 변경 `0`이다.
- 전력·용수·폐기물: utility와 process-disposition authoring 변경 `0`이다.
- 위험·실패: bundle/member/source/dependency/semantic digest drift, 부분 asset apply, 임시 approval stale, 두 번째 YAML churn을 fail-loud한다. 첫 적용 시 post-application authority recapture가 일반 strict audit를 사용해 임시 approval을 stale로 거절한 결함은 rollback `PASS` 후 `GenerateForApprovalRefresh` 중간 경계로 교정했고 최종 검사는 다시 일반 strict audit를 사용한다.
- 기존 대안: `337`개 전부 일괄 적용은 무료 faction Trade/Supply와 고가 후보를 현금화하므로 금지했다. 현재 가격을 전부 유지하면 EWU 재조정이 시장에 반영되지 않으므로, 안전 `178`만 적용하고 `56` bundle을 rework로 남겼다.
- 지배 전략 방지: mana 무상 유입 `46` bundle과 candidate sale credit 2배 이상 비-mana `10` bundle은 적용하지 않았다. 남은 `99` Critical은 이 `56` rework bundle의 exact member다.
- 순환 차익 방지: Trade 구매→판매, 제작→판매, 해체→재제작 SCC tolerance는 계속 `0`이다. 질량 비보존 허용은 시장 가치 생성 승인으로 사용하지 않는다.
- 실행 경로: `AuditOnly ledger → exact bundle decision JSON → stale-safe validation → temporary approval custody → atomic SerializedProperty patch → approval-refresh recapture → canonical approval replacement → strict AuditOnly → no-op reapply`다.
- 저장 권위: gameplay save schema 변경 `0`이다. authored market SO와 canonical approval JSON이 적용 권위이며 recommendation artifact는 권위가 아니다.
- 자동 감사·실전 증거: decision `234 bundles / 337 members`, promote `178 / 238`, rework `56 / 99`; 적용 후 rows `87,438`, Critical `99`, collapsed `201`, approved roots `47`, integrity failure `0`. no-op 재적용은 changed asset/property `0/0`; application report SHA-256 `C39C0F7840CA9CD13A9E9873E744DBE94BCBA37F2C49E259E08442F688D14C5A`, decision SHA-256 `C5550419386749841D1548BE1F70F4D02F6F59D30D6A595E362A561BD34F6E92`, approval SHA-256 `B39BF6C60F88ECB6B34CA446B0645F5D8463FFD636248170688D0D7C41C7EDA8`, Console Warning/Error `0/0`, GameplayScene SHA-256 불변이다.
- 현재 판정: `안전 시장 promotion 적용·원자성·no-op 검증 완료 / rework 56 bundle·99 Critical과 faction route economic policy OPEN / 전체 밸런스 완료 아님`. 부모 진행률은 Batch A `31/31`, B `36/40`, C `8/39`, A-C `75/110`, 전체 A-H `1/8`로 유지한다.

### balance:v27:faction-route-economic-source-closure-v1 — 2026-08-31

- 정의 ID·종류: 기존 Faction `TradeCaravan`과 `SupplyCaravan`의 외부 경제 Source를 일반화된 정책 capability와 exact settlement receipt로 닫는 구조 변경이다. 신규 팩션·아이템 콘텐츠는 추가하지 않는다.
- 등장 시대·연구: 기존 관계도·계약 해금·쿨다운·경로 조건을 그대로 유지한다. 연구와 시대 해금 변경 `0`이다.
- 역할: 외부에서 생성되는 물리 화물의 정당성과 무료 경제 가치 생성을 분리한다. Trade는 canonical quote를 출발 전에 한 번 지불하고, Supply는 전체 동맹 원조 예산을 소비한다.
- Before: Trade와 Supply 모두 계약·쿨다운·경로만 확인하고 sellable Loose cargo를 무료 생성한다. delivery line별 `Spawn` 결과도 검사하지 않아 부분 생성 뒤 전체 완료가 가능하다.
- After 목표: Trade 가격은 authored unit price 합계 `1.00×`, 출발 후 매복·손실은 환불 없음. Supply는 골드 차감 없이 global alliance-benefit EWU budget을 예약·소비한다. 정책은 capability registry로 선택하며 faction/item ID 분기는 금지한다.
- 물리 BOM·입력·출력: cargo item과 amount는 현재 authored 값을 유지한다. 최종 publication은 frozen exact output vector와 단일 commit receipt가 모두 일치한 경우에만 Delivered가 된다.
- Direct WU·Embedded EWU: 생산·운반 WU와 item EWU 변경 `0`. Supply budget의 최종 수치는 현존 모든 route의 일일 기대 EWU를 전수 계산한 뒤 별도 승인한다.
- 시간: 현재 travel time과 cooldown을 유지한다. quote는 요청 시점에 동결하며 경로 도중 재가격하지 않는다.
- 공간·전력·용수·폐기물: 변경 `0`. drop zone이나 output space가 없으면 cargo를 삭제·순간이동하지 않고 Ready 상태로 대기한다.
- 위험·실패: 잔액 부족은 balance·route·cooldown 변화 `0`; 동일 settlement source의 재시도는 추가 차감 `0`; debit 뒤 route 생성 실패는 exact 보상 환불을 끝낼 때까지 save capture를 차단한다.
- 대안: mana 품목만 판매 불가로 표시하거나 candidate 가격을 임시 cap하는 방법은 콘텐츠별 분기와 일관성 붕괴를 만들므로 사용하지 않는다. 배송품은 일반 시장 규칙을 유지한다.
- 지배 전략 방지: 모든 Trade bundle은 `purchase gold > immediate sale recovery gold`를 요구한다. Supply는 팩션 수 증가에 따라 무료 가치가 선형 증가하지 않도록 전역 budget을 사용한다.
- 순환 차익 방지: route Source의 비용·benefit debit을 EWU graph 외부 입력으로 감사하고, 기존 제작·판매·해체 SCC tolerance `0`을 유지한다. 질량/엔트로피 비보존 허용은 무료 경제 가치 승인 근거가 아니다.
- 실행 경로: `TryGetRouteQuote → exact-once debit/budget reservation → frozen route receipt → travel/ambush → Ready → exact physical publication → Delivered → ordinary stock/use/sale`다.
- 저장 권위: Faction save V4 route receipt가 과거 settlement 권위다. bounded Treasury ledger는 사후 진단이며 오래된 route 복원의 영구 join이 아니다. 과거 V3 마이그레이션은 범위 밖이라 fail-loud한다.
- 자동 감사·실전 증거: 이 기록 시점에는 공용 exact-once debit, route policy, V4 receipt, exact cargo publication, global Supply budget, UI quote와 통합 seed 검증이 모두 OPEN이다. 각 수직 슬라이스가 별도 compile/focused test를 통과하기 전에는 99 Critical을 해제하지 않는다.
- 현재 판정: `구현 전 계약 고정 / authored 화물·가격·WU·kg 변경 0 / 전체 밸런스 완료 아님`.

2026-08-31 Trade settlement/V4 증거 갱신:

- Trade 요청은 canonical quote의 exact gold를 선결제하고, receipt 생성부터 route list publication까지의 모든 실패를 canonical refund boundary로 보상한다. refund가 즉시 실패하면 transient pending authority를 유지해 Tick에서 forward retry하며 direct capture, 중앙 save capture, reset과 restore publication을 차단한다.
- route list가 실제로 받은 뒤에만 route sequence를 증가시킨다. 실패한 publication은 route `0`, sequence 증가 `0`이고 성공 publication은 환불 `0`이다.
- Faction V4 settlement receipt는 item ID·quantity·과거 unit price의 ordinal frozen quote lines와 payment/source/quote digest를 저장한다. strict restore는 이 historical snapshot에서 total과 두 digest를 재계산하며 현재 authored SO 가격이나 bounded Treasury ledger에 join하지 않는다.
- frozen unit price 변조와 valid-looking 64-char digest 변조는 live mutation 없이 거부한다. injected refund 실패 2회 뒤 세 번째 retry가 exact `17 gold`를 한 번만 복구하고, 완료 후 replay credit `0`을 증명했다.
- focused 결과는 `FACTION_ROUTE_ECONOMIC_POLICY_PASS`, `FACTION_TRADE_V4_RECOVERY_PASS`, Unity compile PASS, Console Warning/Error `0/0`이다. 세력 경제 폐쇄는 `5/7`; whole-vector cargo publication과 Supply 전역 budget은 OPEN이며 unresolved Critical은 `99`라 전체 밸런스 완료가 아니다.

### balance:v27:faction-route-exact-cargo-and-supply-budget-v1 — 2026-08-31

- 정의 ID·종류: 기존 6개 Faction의 Trade/Supply route cargo publication과 하나의 전역 `alliance-benefit` 예산 권위다. 신규 faction·item·recipe는 추가하지 않는다.
- 등장 시대·연구: 기존 관계 rapport, contract, research, route cooldown 조건을 그대로 사용한다. 해금 변경 `0`이다.
- 역할: Trade/Supply 화물을 부분 생성할 수 없는 하나의 물리 Source vector로 게시하고, Supply 무료 가치를 고정된 전역 EWU budget에서 차감한다.
- Before: 도착 cargo는 line별 raw `Spawn` 뒤 bool 완료였고 Supply는 current route request에서 예산 미연결로 차단됐다.
- After: cargo는 `Ready→Publishing→Delivered` V5 receipt를 사용한다. Supply는 current 6-route reviewed authority에서 whole debit을 먼저 예약한 뒤 route를 게시한다.
- 물리 BOM·입력·출력: 6개 faction의 authored cargo item/quantity 변경 `0`. strength 반영은 기존 positive `Mathf.RoundToInt`의 midpoint-to-even 의미를 integer projector로 보존한다. whole vector의 ordered stack·quantity·mass가 terminal receipt와 exact 일치해야 한다.
- Direct WU: 제작·운반·건설 WU 변경 `0`이다.
- Embedded WU/EWU: capacity `39,142,546mEWU`; route debit은 beastkin `924,102`, demon `14,016,103`, golem `16,675,895`, harpy `1,443,442`, kobold `3,683,242`, myconid `2,399,762mEWU`다. 합계가 capacity와 exact 같다.
- 시간: refill은 `1079184126313/1843380mEWU/day`의 정수 quotient/remainder carry를 사용한다. 기존 Supply cooldown `20/84/99/22/49/38일`은 변경하지 않는다.
- 공간: 기존 drop zone 좌표를 exact publication plan에 동결한다. drop zone 부재 시 `Ready`를 유지하고 일반 바닥 fallback spawn을 하지 않는다.
- 전력·용수·폐기물: 변경 `0`이다.
- 위험·실패: prepare 실패는 물리 prefix `0`; commit 실패는 같은 transaction을 forward retry한다. Publishing과 transient transaction은 save/reset/restore publication을 차단한다. route publication 실패의 Supply debit은 같은 aggregate에서 exact refund한다.
- 기존 대안: item별 판매 금지, mana 가격 cap, faction 수 기반 자동 budget 확대, line별 compensation spawn은 사용하지 않는다.
- 지배 전략 방지: current 6-route canonical source bytes SHA-256 `c539c892bb0b8355801c923c3a86da8f2a331ed459414684aa2dc60d0767fe15`에 capacity/refill/route debit을 결속한다. faction/cargo/cooldown/quote 변경은 자동 확대 대신 stale failure다.
- 순환 차익 방지: Supply budget debit은 외부 Source의 EWU 비용이다. item acquisition/recoverable 및 SCC tolerance `0`은 변경하지 않는다. unresolved market Critical `99`는 이 구조만으로 승인하지 않는다.
- 실행 경로: `current quote+cooldown join→exact budget reserve→Faction route publish→arrival→whole-vector TryPrepare→same transaction TryCommitReleased(Unassigned)→terminal V5 receipt`다.
- 저장 권위·자동 감사·실전 증거: immutable SO가 schema/source digest/capacity/refill/6 route debit을 소유하고 mutable balance/remainder/day/digest는 Faction aggregate/save V5만 소유한다. Trade의 과거 frozen price receipt는 current price에 영구 join하지 않으며 Supply V5는 current global budget authority와 exact join한다. `FACTION_ALLIANCE_BENEFIT_BUDGET_PASS`, `FACTION_EXACT_CARGO_PUBLICATION_PASS`, `PHYSICAL_ITEM_EXACT_PUBLICATION_REGRESSION_PASS`, `FACTION_ROUTE_ECONOMIC_POLICY_PASS`, `FACTION_V5_STRICT_RESTORE_PASS`, Unity compile PASS다.
- 현재 판정: 세력 경제 폐쇄 `7/7` 완료. 기존 dirty GameplayScene의 `LifecycleDemolitionFixture` 두 개가 clean PlayMode 기동을 막아 final PlayMode·Critical `99→0`·kg·6인 생존·공간·paired/layout·3-seed는 OPEN이고 전체 밸런스 완료가 아니다.

### balance:v27:ship-p0-physical-mass-survival-market-closure-v1 — 2026-08-31

- 정의 ID·종류: V27 물리 kg·운반·warehouse·FacilityBuffer·시장·6인 생존·공간의 Ship-first P0 최종 폐쇄다. 신규 콘텐츠 정의는 추가하지 않는다.
- 등장 시대·연구: 인구 `1/3/6/12/18/24`와 기존 연구 기반 공간 tier를 사용한다. developer `E` 확장은 정식 진행 권위가 아니다.
- 플레이어 결정: 일반 운반 nominal `25kg`, kg 저장량, 실제 비축·시설·공유 통로·연구 확장 범위 안에서 생산망과 물류 여유를 선택한다. 6명에게 동일 핵심 시설 두 개를 강제하지 않고 물리 primitive fallback을 허용한다.
- Before·After: count storage 검증을 canonical gram으로 교체했다. 6인 7일 비축은 grain `60`, meal `12`, water `59`, 합계 `57,700g`; 최대 관련 stack `30,000g`; 4×25kg warehouse 정상/장애 점유는 `577/549‰`다.
- 물리 BOM·입력·출력: 음식 수요/총생산/순생산은 `300/420/399 nutrition/day`이고 물리 비축·출력·운반 quantity와 gram을 exact 보존한다. 생산 출력은 FacilityBuffer에서 별도 haul되며 일반 바닥 순간이동을 허용하지 않는다.
- Direct WU: 6인 recurring 생존 노동 `78.538WU/day`, effective `270WU/day`의 `29.1%`다. final 3-seed actual/effective 평균은 `65.711/59.713 WU/성인·일`, CV `3.21%/2.98%`; 중립 authored 권위 `50/45`는 유지하고 시나리오 상회분을 원가에 재곱하지 않는다.
- Embedded WU/EWU·가격: 최종 ledger `87,286`행, item `414`, live recipe `351`, crop `12`, Critical/integrity `0/0`, SCC `297`, 최소 margin `-4,326,042mEWU`; market은 unit price `414/414`, sale rate `350/350`, retail `4/4`, daily cost `7/7`, reward `13/13` 적용이다.
- 시간·공간: 5일×3 seed와 6×256 layout을 사용했다. 공간 폭은 `27/27/27/49/65/81`, 최소 headroom `30.7%`, 6인 runtime headroom `30.8%`다.
- 전력·물·연료·정비: 기존 utility 값을 유지하고 정상/장애 shared-cell utilization `60%/84%`, storage normal/fault 상한 `70%/90%` 안에서 검증했다.
- 위험·실패·회복: active haul lease/admission heartbeat, actual pickup 뒤 Downed current-cell recovery, access/egress clutter `0`, persistent recovery clutter `0`, RNG cross-talk `0`, no-op apply를 요구한다.
- 기존 대안: count capacity는 kg를 줄여도 창고·공간이 자동 조정되지 않아 폐기했다. 모든 Batch A–H exhaustive 행을 출시 차단선으로 두는 방식은 게임 완성을 과도하게 지연시켜 P1/P2로 분리했다.
- 지배 전략·순환 차익: Trade는 exact 결제, Supply는 fixed-source EWU budget을 사용하며 sale output은 acquisition의 60% Floor 이하, SCC tolerance는 `0`이다.
- 실행 경로: `canonical item grams → warehouse/admission/FacilityBuffer → haul/recovery → six-adult static/live spatial → market apply/no-op → economy256 → AuditOnly manifest → portable verifier`다.
- 저장 권위: current-format warehouse·world item·production·Faction aggregate가 runtime authority다. 과거 세이브 마이그레이션은 범위 밖이고 GameplayScene은 수정하지 않았다.
- 자동 감사·증거: `v27-balance-six-adult-food-water-loop.txt`, `v27-balance-population-stage-playmode.txt`, `v27-balance-spatial-capacity.csv`, focused paired report, `v27-balance-economy-256-seed.txt`, market report, three daily seed reports와 manifest를 사용한다.
- 검증 결과: portable verifier `rows=87286; critical=0; scc=297; combat=36x1000; dailySeeds=3; finalAcceptance=33/33`, Unity Console Warning/Error `0/0`, official GameplayScene SHA-256 불변이다.
- 현재 판정: `V27 Ship P0 밸런스 완료`. exhaustive Batch A–H는 `1/8`이며 B/C/D/F/G/H 전수 행렬과 32/64 paired는 P1/P2 backlog다.

### balance:v27:production-reviewed-process-loss-family-closure-2 — 2026-08-31

- 정의 ID·종류: 기존 deterministic Transform recipe 16개(제련 6, 연소 1, 수분 3, 제분 1, 추출 4, 직조 1)의 residual mass 설명 계약이다. 신규 item/recipe 정의는 추가하지 않는다.
- 등장 시대·연구: 기존 recipe의 시대·연구·workstation 조건을 그대로 유지한다.
- 역할: 성공한 생산 cycle의 입력 gram과 물리 출력 gram 차이를 typed nonphysical disposition으로 기록해 삭제·복제 여부를 감사 가능하게 한다.
- Before·After: Before는 16개 recipe가 `mass-balance-explanation-missing`; After는 기존 `process-loss@1` descriptor를 갖는다. item kg, input/output quantity, BOM, WU, 가격 변경은 `0`이다.
- 물리 BOM·입력·출력: 현재 authored input/output vector를 그대로 사용하며 residual만 SmeltingByproduct, Combustion, MoistureEvaporation, MillingByproduct, ExtractionResidue, FiberProcessingWaste로 분류한다.
- Direct WU·Embedded WU/EWU: 변경 `0`. 기존 계산기와 SCC tolerance `0`을 유지한다.
- 시간·공간: 생산시간, 시설 footprint, FacilityBuffer/warehouse gram capacity 변경 `0`이다.
- 전력·용수·폐기물: utility authored 값 변경 `0`. 조리·손질/훈연·건조/염장과 tanning/brine/spent-feedstock/distillation/rendering 잔사를 서로 다른 reason으로 보존한다.
- 위험·실패: descriptor 누락·충돌, regeneration clobber, current/runtime residual drift, duplicate recipe ID는 fail-loud한다.
- 기존 대안: 80 external-input 행을 무료 외부 질량으로 일괄 승인하거나 87 residual을 하나의 generic loss로 접는 방식을 금지한다.
- 지배 전략·순환 차익: 이 계약은 비용·산출량을 변경하지 않으며 기존 EWU 입력 Ceil/산출 Floor와 SCC margin을 그대로 사용한다.
- 실행 경로: `builder balance authority → reviewed catalog → RecipeSO descriptor → process-loss capability → frozen WIP/output receipt → routing tombstone → audit`다.
- 저장 권위: RecipeSO descriptor와 prepared/routing current-format receipt가 runtime 권위다. 신규 save schema는 추가하지 않는다.
- 자동 감사·실전 증거: Unity first apply `changed=16 exact=68`, second apply `changed=0`; fresh inventory reviewed exact `102`, balanced exact `3`, closed recipes `132/355`; broad ProductionEconomy PASS, Console `0/0`.
- 결정론적 산출물: recipe CSV/TXT SHA-256 `60FC9AF0456E1538FF07894FB2E8B0EBFD619F3E2E1BDF203B25160BB7BDBD81` / `E9CA9799AC1E3E601FC91700163C3B09B6C266603CF001309F03A540D3253D72`.
- 현재 판정: 16개 Transform 계약 완료. D parent는 semantic `51`, Transform `223`이 남아 OPEN이며 전체 exhaustive A–H 완료가 아니다.

### balance:v27:production-reviewed-process-loss-family-closure-3 — 2026-08-31

- 정의 ID·종류: 기존 deterministic Transform 69개의 게임 추상화형 공정 손실 계약이다. 신규 item/recipe는 추가하지 않는다.
- 등장 시대·연구: 기존 recipe 해금·facility·workstation을 유지한다.
- 역할: 현재 게임에 물리 scrap/byproduct가 없는 projectile, component, compost/fuel, craft, record, medical, tool 공정의 exact residual을 typed disposition으로 기록한다.
- Before·After·BOM·WU: 69개가 `mass-balance-explanation-missing→reviewed-exact`; kg·BOM·quantity·WU·가격 변경 `0`이다.
- Embedded WU/EWU·시간·공간·utility: 변경 `0`; SCC tolerance `0`과 현재 facility/warehouse capacity를 유지한다.
- 위험·대안: authored/runtime residual이 다른 9개와 67–98% extreme residual 9개는 승인하지 않는다. generic loss로 덮는 대신 kg/BOM/output-unit 권위를 후속 교정한다.
- 지배 전략·순환 차익: 물리 산출·비용을 바꾸지 않으며 기존 입력 Ceil/산출 Floor를 유지한다.
- 실행·저장 권위: reviewed catalog→RecipeSO descriptor→generic process-loss capability→frozen production/routing receipt. 신규 save authority는 없다.
- 자동 감사·증거: reviewed exact `171`, balanced exact `3`, closed recipes `201/355`; corrected clear `9`, second clear/apply `0/0`, broad ProductionEconomy PASS, Console `0/0`, scene hash 불변.
- 결정론적 artifact: CSV/TXT SHA-256 `DAEF7F140257ACD95FA675767777CDC23A73247833B6F9839631829D34BBEFD6` / `4B784BA9596E373AC5E3387BE7B6781848DC5267607C9C88B41A340312704CBC`.
- 현재 판정: 69개 추가 계약 완료. D parent는 semantics `51`, Transform `154`가 남아 OPEN이다.

### balance:v27:facility-one-to-one-world-identity-handoff — 2026-08-31

- 정의 ID·종류: 기존 시설 relocation/evolution의 런타임 object 교체 경계다. 신규 시설·아이템·레시피는 추가하지 않는다.
- Before·After: Before는 새 object가 신규 persistent ID로 live 등록돼 원본 정체성과 transient/projection 권위가 끊겼다. After는 detached candidate가 원본 `BuildingInstanceId`를 승계하고 building/warehouse/retail/transient registration을 reversible transaction으로 교체한다.
- BOM·WU·EWU·가격·시간·공간·utility: authored 값 변경 `0`. 시설 위치나 definition이 바뀌어도 비용·처리량은 기존 권위를 유지하며 active 생산 권위 retarget은 아직 허용하지 않는다.
- 위험·실패: candidate init/grid/state/handoff/publication 각 실패는 원본 world/grid 권위를 보존하거나 exact rollback한다. duplicate ID 동시 노출, source 파괴가 survivor를 unregister하는 현상, candidate cleanup leak을 금지한다.
- 실행·저장 권위: `detached create→persistent ID restore→grid/state prepare→world projection swap→publish→old retire`. current-format 저장 권위와 과거 세이브 정책은 변경하지 않는다.
- 자동 증거: building/warehouse/retail forward·rollback·retry focused, warehouse relocation identity fixture, broad FacilityEvolution identity assertion, Unity compile, Console `0/0`, GameplayScene hash 불변.
- 현재 판정: one-to-one world identity 하위 경계 완료. active bill/WIP/output/custody retarget과 multi-facility synthesis는 Batch B P1/P2로 OPEN이며 전체 밸런스 완료가 아니다.

### balance:v27:character-durable-equipment-owner-migration — 2026-08-31

- 정의 ID·종류: 기존 `character.career`의 `record:arcane-index`와 `character.reproduction`의 `record:breeding-ledger` 장기 사용 도구 소유권 경계다. 신규 아이템·레시피·시설은 추가하지 않는다.
- Before·After: Before는 각 도메인이 world stack 전수 검색, raw delivery와 durability component 직접 변경을 중복 소유했다. After는 공통 durable-equipment policy/slot/reconcile/supply/use가 exact physical lot, claim/profile, wear와 terminal lifecycle을 소유한다.
- BOM·Direct WU·Embedded WU/EWU·가격·시간·공간·utility: authored 값 변경 `0`. 도구 quantity는 기존 exact `1`, use당 wear는 기존 `1`을 유지한다.
- 위험·실패: 누락 policy, 잘못된 owner/item/quantity, non-positive gram, raw delivery 재유입과 domain effect 실패 후 wear 누수를 fail-loud한다. domain aggregate effect가 reject/throw하면 같은 transaction에서 wear를 복원한다.
- 대안·지배 전략·순환 차익: 시설별 bespoke buffer를 새로 만들거나 무료 도구를 생성하지 않는다. 물리 수량·가격·EWU가 바뀌지 않아 신규 차익 경로는 없다.
- 실행·저장 권위: `policy→facility slot claim/profile→reconcile/supply→durable use→domain effect`. 공통 durable slot과 기존 domain persistence를 결합하며 신규 save schema는 추가하지 않는다.
- 자동 감사·증거: career/reproduction focused, common durable-equipment suite, broad ProductionEconomy, Unity compile, Console `0/0`; owner manifest input `10/39`, output `10/10`, bypass/orphan/unclassified `0/0/0`, second-run byte/hash/length/mtime 변화 `0`.
- 결정론적 artifact: CSV/TXT SHA-256 `79B5E35B5F32624EBAA748C4495F9842666739B50A06904777E43CBA28A78287` / `8BFF7243E661AF34A66DF90D7CBF2D111E4BF32B821C3E7A6160943745264D8D`; GameplayScene SHA-256 불변.
- 현재 판정: `character.career`와 `character.reproduction` owner 2개 완료. Batch C는 `10/39`, remaining `29`, full stored-destination coverage OPEN이며 전체 exhaustive A–H 완료가 아니다.

### balance:v27:climate-durable-equipment-owner-migration — 2026-08-31

- 정의 ID·종류: 기존 `book:seasonal-almanac` 1개와 `tool:weather-observation-kit` 1개를 사용하는 authored weather tower `8851`의 물리 입력 권위다. 신규 콘텐츠는 없다.
- Before·After: Before는 ClimateRuntime이 raw stack scan, delivery, durability mutation을 직접 소유했고 tower 상실 terminal이 없었다. After는 공통 durable slot이 exact claim/profile/save/drain을 소유하고 climate adapter는 assignment와 paired use만 요청한다.
- 물리 BOM·질량: 기존 1,100g + 2,350g, 합계 3,450g definition-mass profile을 유지한다. quantity와 item definition 변경 `0`.
- Direct WU·Embedded WU/EWU·가격·시간·공간·utility: 변경 `0`. 예보 horizon과 기상 효과도 변경하지 않는다.
- 위험·실패: kit wear 실패 시 almanac wear rollback, tower 소실/capability 변경 시 close, close conflict 시 save fail-loud를 요구한다. raw delivery/component mutation 재유입은 manifest ratchet이 거부한다.
- 대안·지배 전략·순환 차익: 무료 대체 장비나 신규 생성 경로가 없고 도구 소비 속도·효과가 동일하므로 신규 차익과 지배 전략 변화가 없다.
- 실행·저장 권위: `weather tower→durable assignment→exact supply→paired daily wear→climate projection`; common durable save section과 carried-aware terminal drain을 사용한다.
- 자동 감사·증거: focused climate/shared durable rollback, broad ProductionEconomy, Unity compile, Console `0/0`; manifest input `11/39`, output `10/10`, bypass/orphan/unclassified `0/0/0`, no-op writer diff `0`.
- 결정론적 artifact: CSV/TXT SHA-256 `18FD1DA6D6706524F2FE20F768EDAD2562D64A889C1DDD747B4152A157F8C8B6` / `DC309B3DD5F16D9BD64BA9D0093BBC02D0250F680DE2A2716710B5C4E379972D`; GameplayScene SHA 불변.
- 현재 판정: climate owner 완료. Batch C는 `11/39`, remaining `28`; 전체 exhaustive A–H는 OPEN이다.

### balance:v27:knowledge-residue-exact-task-owner-migration — 2026-08-31

- 정의 ID·종류: 기존 `captivity:memory-residue`를 사용하는 `research.knowledge-residue` task 입력의 exact 물리 소유권 경계다. 신규 item·recipe·facility는 추가하지 않는다.
- 등장 시대·연구: 기존 연구 해금·시설·task 조건을 유지한다.
- 역할: category-wide Knowledge 소비와 별도 자동 보너스를 하나의 explicit task/assignment exact claim/profile/Sink 계약으로 대체한다.
- Before·After: Before는 범주 기반 delivery/consume와 저장되지 않은 자동 `+12` 연구 보너스가 별개로 존재했다. After는 exact memory-residue 한 개가 task 결과와 exact-once로 결속되고 legacy 자동 보너스는 제거된다.
- 물리 BOM·질량: 기존 item definition, quantity `1`, current positive gram을 유지한다. item kg와 recipe BOM 변경 `0`이다.
- Direct WU·Embedded WU/EWU: task WU, item EWU와 가격 변경 `0`; 입력 Sink가 명시적이어서 중복 소비·무료 연구 증가를 금지한다.
- 시간·공간·utility: 연구 시간, facility footprint, buffer 위치, 전력·용수·폐기물 변경 `0`이다.
- 위험·실패: task/assignment fingerprint 불일치, pending Sink 재게시, facility 상실 후 authority 선삭제, Physical Items보다 이른 restore ack를 fail-loud한다.
- 대안·지배 전략·순환 차익: 범주 전체에서 임의 잔여물을 집거나 자동 보너스를 유지하지 않는다. exact physical Sink만 연구 효과를 커밋하므로 무료 반복 경로가 없다.
- 실행 경로: `task assignment→exact claim/profile→delivery→pending typed Sink→Physical Items restore join→Sink outcome→task commit→ack/revoke`다.
- 저장 권위: Research V6가 pending operation/fingerprint를, Physical Items가 exact lot custody를 소유한다. 과거 save migration은 범위 밖이다.
- 자동 감사·증거: `KnowledgeResiduePhysicalRestoreJoinDebugScenarios`, V16 integration, broad ProductionEconomy, source ratchet과 owner manifest를 통과했다. Console Warning/Error `0/0`, GameplayScene hash 불변이다.
- 현재 판정: `research.knowledge-residue` owner 완료. authored kg/BOM/WU/EWU/가격 변경 `0`; Batch C와 exhaustive A–H는 OPEN이다.

### balance:v27:blueprint-archive-exact-capacity-owner-migration — 2026-08-31

- 정의 ID·종류: 기존 research blueprint 물리 아이템을 보관하는 `research.blueprint-archive` destination authority다. 신규 blueprint·시설은 추가하지 않는다.
- 등장 시대·연구: 기존 blueprint 구매·연구 해금과 archive 대상 시설 조건을 유지한다.
- 역할: exact gram으로 archive 입고·carried commitment·restore·retirement를 통제한다.
- Before·After: Before는 exact claim만 있고 gram profile과 paired rollback 증거가 없었다. After는 `ExactGramRequired` claim과 revision 1 profile을 공용 lifecycle로 함께 게시한다.
- 물리 BOM·질량: 모든 current blueprint의 exact unit mass는 양수이고 현재 최대 `150g`; authored archive count `8`이므로 capacity `1,200g`이다. item kg·BOM 변경 `0`이다.
- Direct WU·Embedded WU/EWU: purchase/research WU, blueprint EWU·가격 변경 `0`이다.
- 시간·공간·utility: research 시간, 시설 footprint와 전력·용수·폐기물 변경 `0`; archive의 기존 center-cell drop authority를 유지한다.
- 위험·실패: 0g/중복 blueprint ID, claim/profile tear, committed-carried mass 누락, release 전 authority 삭제와 raw prefix release를 fail-loud한다.
- 대안·지배 전략·순환 차익: count capacity나 무제한 archive를 사용하지 않는다. 신규 생성·회수·가격 경로가 없어 경제 차익 변화는 없다.
- 실행 경로: `catalog mass projection→paired claim/profile→purchase delivery→committed carried/deposited occupancy→research archive→physical release→paired retirement`다.
- 저장 권위: Research V6가 archive projection을 복원하고 Physical Items가 lot/carry custody를 소유한다. 공용 lifecycle이 claim/profile을 원자 게시·rollback한다.
- 자동 감사·실전 증거: 실제 pair restore/late-failure rollback, BlueprintResearch/DungeonSave/ProductionEconomy focused+broad, FirstRun의 150g carried/deposited occupancy, manifest 두 번째 실행 diff `0`을 통과했다. full FirstRun은 이후 arcane-index 결함으로 OPEN이다.
- 결정론적 artifact: 후속 unified-mutation source ratchet까지 포함한 owner CSV/TXT SHA-256 `19AF62F4E23067E5BD4805B6D0662EF820B6E39EA1F9323F74BF6758D88C35A2` / `5249D69DB07E4C445632E979780CD81CF7AE192334FA26EE6B9AD04A624547F4`; GameplayScene SHA 불변이다.
- 현재 판정: `research.blueprint-archive` owner 완료. Batch C `13/39`, remaining `26`; F `0/6`, G `0/19`, H exhaustive OPEN이다.

### balance:v27:unified-production-mutation-admission-foundation — 2026-08-31

- 정의 ID·종류: 기존 production facility mutation의 transient topology epoch와 durable destructive-drain journal을 합성하는 런타임 admission 경계다. 신규 콘텐츠 정의는 없다.
- 등장 시대·연구: 모든 시대·연구·시설에 공통이며 해금 변경 `0`이다.
- 역할: mutation 중 exact-lot·planned-output 신규 FacilityBuffer custody가 유입되는 race를 하나의 operation snapshot으로 차단한다.
- Before·After: Before는 production DI가 destructive drain만 admission source로 보아 transient relocation/evolution epoch의 신규 예약을 통과시켰다. After는 exact transient/durable operation ID와 revision을 하나의 source가 투영한다.
- 물리 BOM·질량: item definition, quantity, kg, FacilityBuffer capacity와 warehouse capacity 변경 `0`이다.
- Direct WU·Embedded WU/EWU·가격: 변경 `0`; 생산 recipe, cycle time, 가격과 SCC 계산을 건드리지 않는다.
- 시간·공간·utility: footprint, 처리량, 전력·용수·폐기물 변경 `0`이다.
- 위험·실패: transient/durable overlap ambiguity, synthetic operation ID, rejected admission의 token sequence 소비, 기존 custody commit/release 차단으로 인한 deadlock을 금지한다.
- 대안: 두 independent source를 등록하거나 bool `IsFrozen`만 전달하는 방식 대신 durable-first exact snapshot을 선택했다.
- 지배 전략·순환 차익: 생산량·비용·회수량 변화가 없어 신규 경제 전략이나 순환 차익을 만들지 않는다.
- 실행 경로: `mutation epoch/journal→combined snapshot→production.facility-mutation source→owner-neutral admission composer→exact/planned reserve gate`다.
- 저장 권위: transient epoch는 저장하지 않고 durable journal은 기존 save authority를 유지한다. snapshot은 파생 read model이며 신규 save field가 없다.
- 자동 감사·증거: focused 4 suites, broad ProductionEconomy, manifest source ratchet, current-source Unity compile, Console `0/0`, artifact 두 번째 생성 hash·length·mtime 변화 `0`, GameplayScene SHA 불변을 통과했다.
- 현재 판정: admission foundation 완료. 특수 producer·active custody retarget·multi-facility transaction이 남아 Batch B는 `36/40`, exhaustive A–H는 OPEN이다.

### balance:v27:unified-production-mutation-parent-closure — 2026-08-31

- 정의 ID·종류: 기존 Combat/Apparel/Certified Seed/Crop 생산 owner의 공통 facility mutation 실행 경계다. 신규 item·recipe·facility 정의는 없다.
- 등장 시대·연구: 모든 시대의 해당 production facility에 공통 적용하며 연구 해금 변경 `0`이다.
- 역할: relocation/evolution/synthesis/destructive drain 중 신규 계획·설정·productive work가 frozen owner snapshot을 변경하지 못하게 하고, 이미 소유된 terminal receipt만 수렴시킨다.
- Before·After: Before는 신규 FacilityBuffer admission만 차단하고 특수 producer의 queue/progress/cancel이 계속 변이했다. After는 terminal convergence를 먼저 허용한 뒤 모든 신규 productive mutation을 exact operation fence로 차단한다.
- 물리 BOM·질량: item kg, input/output quantity, FacilityBuffer/warehouse gram capacity 변경 `0`이다.
- Direct WU·Embedded WU/EWU·가격: recipe WU·EWU·가격·SCC 계산 변경 `0`이다. fence 중 진행 WU는 수락하지 않는다.
- 시간·공간·utility: cycle time, footprint, 전력·용수·폐기물 변경 `0`이다.
- 위험·실패: terminal publication까지 막아 destructive drain과 교착, write-on-read policy mutation, AI의 frozen order 반복 선택, fence 종료 전 cancel로 exact receipt orphan을 만드는 경로를 fail-loud한다.
- 대안·지배 전략·순환 차익: producer별 bool 분기를 중복하지 않고 공통 exact snapshot policy를 사용한다. 생산량·비용 변화가 없어 신규 지배 전략·차익은 없다.
- 실행 경로: `terminal convergence→ProductionFacilityMutationWorkPolicy→plan/config/work mutation`이다.
- 저장 권위: transient epoch는 파생 runtime, durable journal은 기존 save authority다. producer save schema와 과거 세이브 정책은 변경하지 않는다.
- 자동 감사·증거: combined gate, Combat, Apparel, Crop/Certified focused와 broad ProductionEconomy, Unity compile, Console `0/0`, owner/F/G artifact 2회 동일성, GameplayScene SHA 불변을 통과했다.
- 현재 판정: unified mutation parent 완료. Batch B `37/40`; active retarget/support/multi-facility 3개는 OPEN이다.

### balance:v27:run-invasion-durable-owner-migration — 2026-08-31

- 정의 ID·종류: 기존 `tool:administrative-seal`과 `tool:watch-signal-horn`의 장기 facility equipment owner다. 신규 콘텐츠는 없다.
- 등장 시대·연구: 기존 행정/경비 연구·시설 조건을 유지한다.
- 역할: V20 행정 resolution과 invasion rally가 사용하는 exact 물리 도구의 supply, wear, save, facility-loss terminal을 공통 durable-equipment 경계가 소유한다.
- Before·After: Before는 domain runtime이 raw delivery와 durability component를 직접 변경했다. After는 `run.v20-administrative-seal`과 `invasion.signal-horn` exact policy/slot/use/lifecycle이 담당한다.
- 물리 BOM·질량: 두 item 모두 authored `2,350g`, max-stack `1`을 유지한다. BOM·quantity·definition 변경 `0`이다.
- Direct WU·Embedded WU/EWU·가격: 기존 action WU, wear, EWU와 가격 변경 `0`이다.
- 시간·공간·utility: facility footprint, action timing, buffer gram capacity, 전력·용수·폐기물 변경 `0`이다.
- 위험·실패: 잘못된 facility 선택, raw delivery/direct component writer 재유입, domain effect reject 뒤 wear 누수, lost facility의 slot orphan을 fail-loud한다.
- 대안·지배 전략·순환 차익: 도메인별 bespoke buffer/save를 추가하지 않는다. 실제 도구 수량·소비율·효과가 같아 신규 차익 경로는 없다.
- 실행 경로: `exact durable policy→facility slot claim/profile→supply→transactional wear+domain effect→lost-owner close→carried-aware drain`이다.
- 저장 권위: 공통 durable slot save와 기존 Run/Invasion domain state를 결합하며 신규 save schema는 추가하지 않는다.
- 자동 감사·증거: shared durable save/use, Run/Invasion focused, broad ProductionEconomy, manifest ratchet과 두 번째 생성 diff `0`, Unity compile/Console `0/0`을 통과했다.
- 결정론적 artifact: owner CSV/TXT SHA-256 `48EF09658349A178532801957AE92E2BD8DB0CA5169875B785DB0FBB16961E97` / `D521208CC0B5B4BD7B0B7DF1FD39A187B7B519BC8022F19F3D9ECDEB2D98CF0A`; scene SHA 불변이다.
- 현재 판정: 두 owner 완료. Batch C `15/39`, remaining `24`; F/G/H exhaustive는 OPEN이다.

### balance:v27:production-retarget-and-clearance-capacity-gate — 2026-08-31

- 정의 ID·종류: 기존 production facility의 relocation/evolution/synthesis mutation transaction과 FacilityBuffer output-clearance 검증 경계다. 신규 item·recipe·facility는 없다.
- 등장 시대·연구: 모든 시대의 해당 시설에 공통 적용하며 연구 해금 변경 `0`이다.
- 역할: source facility의 mutation epoch, world identity 교체와 production participant commit을 원자 결합하고, support/work-speed를 반영한 frozen p95가 authored 2~4 cycle capacity 안인지 판정한다.
- Before·After: Before는 세 caller가 empty-authority preflight와 world 교체를 각각 수행했고 공통 participant transaction이 없었다. After는 stable source-ID transaction을 공유하며 current empty path도 real participant/DI를 통과한다. clearance는 `4.000`/`4.001`을 typed하게 구분한다.
- 물리 BOM·입력·출력: item, BOM, quantity, output branch와 FacilityBuffer authored gram 변경 `0`이다.
- Direct WU·Embedded WU/EWU: recipe WU·EWU·SCC·가격 변경 `0`; reachable support/work-speed는 용량 검증 입력으로만 사용한다.
- 시간·공간·전력·용수·폐기물: cycle time, footprint와 utility 변경 `0`이다. p95 clearance가 4 cycle을 넘으면 자동 증설하지 않고 Critical로 보고한다.
- 위험·실패·복구: duplicate source, split-brain binding, participant/fingerprint drift, partial commit, world/grid publication 실패와 stale support/profile digest를 fail-loud한다. attempted participant까지 역순 rollback하고 epoch를 닫는다.
- 대안: caller별 bespoke fence나 active authority를 묵시 폐기하지 않는다. 현재 `empty-lifecycle-guard`는 active bill/WIP/custody를 안전하게 거부하며 마지막 participant 구현을 강제한다.
- 지배 전략·순환 차익: 생산량·용량·비용·회수량을 바꾸지 않아 신규 지배 전략이나 순환 차익 변화가 없다.
- 실행 경로: `stable source requests→all-source epochs→participant prepare→detached world/grid handoff→participant commit→publish→complete`; capacity는 `scope/census→support/work-speed envelope→32-seed frozen p95→2~4 cycle gate`다.
- 저장 권위: transient retarget transaction은 저장하지 않는다. 기존 facility/production/physical current-format 저장 권위를 유지하고 source digest/fingerprint가 복원 교차 검증을 담당한다.
- 자동 감사·실전 증거: retarget/empty-guard/clearance/synthesis focused와 ProductionEconomy/FacilityEvolution/Synthesis broad PASS, Unity compile, Console `0/0`, artifact 두 번째 실행 diff `0`, GameplayScene SHA 불변이다.
- 결정론적 artifact: owner CSV/TXT `435366C31A2C18149241C0C8C254702A9283DE689386F6ACD04C61C014D33E3D` / `585307398E01F8574EA678C6D78A8DFD5D1C68549997C93CCB50658632723827`; F CSV/TXT `4115402BF407C760B76DB808D6F3883860FDC6BA207C4EC75BE9EC6A48EE721C` / `1FD72C65EF1B2B8465F0194365CA71842ADB1D9588462AD077BE8410E8958DC4`; G CSV/TXT `78DA87887B1A11CDCDA8BF02BA6CA4D290C4FB3B0BF94EF105F52DF6426942A3` / `2CAAA4249D6A4C46BD945BB3F511052D6E4A9B54D50FF2F9982118C707620E1C`.
- 현재 판정: retarget transaction과 support/p95 capacity gate 완료. Batch B `39/40`; active bill/WIP/physical-custody multi-facility retarget과 exhaustive F/G/H는 OPEN이다.

### balance:v27:deterministic-process-loss-closure-2 — 2026-08-31

- 정의 ID·종류: 기존 arrow/bolt 5개, prosthetic 2개, treated-lumber 1개 recipe의 질량 설명 계약이다. 신규 item·recipe·facility 정의는 없다.
- 등장 시대·연구: 기존 recipe의 시대·연구 해금 조건을 유지한다.
- 역할: proposal/current runtime 양쪽에서 양의 deterministic residual로 증명된 절삭·가공 잔사를 명시해 숨은 질량 삭제를 감사 가능하게 만든다.
- Before·After: Before는 해당 8개 recipe가 positive residual인데 설명 계약이 없었다. After는 각각 projectile fitting, prosthetic machining, lumber planing/treatment reason code를 갖는다.
- 물리 BOM·질량: authored unit kg, BOM, input/output quantity 변경 `0`. 잔차 계산과 disposition descriptor만 추가했다.
- Direct WU·Embedded WU/EWU·가격: WU·EWU·가격·SCC 변경 `0`이다.
- 시간·공간·utility: cycle time, footprint, buffer capacity, 전력·용수·폐기물 수치 변경 `0`이다.
- 위험·실패: runtime equation이 zero residual인 `recipe:ration-mixture`에 proposal-only 손실을 잘못 적용하는 것을 금지한다. 극단 손실 9개도 item-unit/process 결정 전에는 자동 승격하지 않는다.
- 대안·지배 전략·순환 차익: 실제 kg나 산출량을 바꾸지 않아 신규 지배 전략·순환 차익을 만들지 않는다. 설명을 꾸미는 대신 증명되지 않은 행을 OPEN으로 유지한다.
- 실행 경로: `reviewed policy catalog→Unity SO descriptor apply→355-recipe inventory→broad ProductionEconomy regression`이다.
- 저장 권위: ProductionRecipeSO의 기존 mass-explanation authoring이 권위이며 신규 save schema는 없다.
- 자동 감사·증거: apply second-run `changed=0 exact=145`, recipe CSV/TXT second-run byte/mtime 동일, broad ProductionEconomy PASS, Console `0/0`, GameplayScene SHA 불변이다.
- 결정론적 artifact: recipe CSV/TXT SHA-256 `EECE2739CFCD009CC59E4CBB8DAC8FDE6638087381FB17426AC82872DBF8135D` / `C7CC59FFB133E91F08E2A9106C4C911FEED8BA35521830E289885444892BDED2`.
- 현재 판정: recipe contracts `209/355`; semantic `51`, remaining recipe contracts `146`, F/G/H exhaustive는 OPEN이다.

### balance:v27:circus-exact-supply-owner-and-runtime-balanced-recipe — 2026-08-31

- 정의 ID·종류: 기존 `captivity.circus` 공연 공급품과 `recipe:ration-mixture`의 물리 소유권/질량 감사 계약이다. 신규 콘텐츠는 없다.
- 등장 시대·연구: 기존 circus와 ration recipe의 연구·해금 조건을 유지한다.
- 역할: circus stage는 소모성 prop box와 내구성 banquet cart를 exact common slot으로 소유하며, ration mixture는 current runtime exact balance와 unapplied proposal drift를 분리한다.
- Before·After: Before Circus는 same-cell inferred destination과 bespoke delivery 2개를 사용했다. After는 `5,100g` exact claim/profile, V4 save join, typed Sink+wear rollback, lost-owner close를 사용한다. Ration은 false loss 후보에서 runtime-balanced mismatch 상태로 이동한다.
- 물리 BOM·질량: prop box `1,950g`, banquet cart `3,150g`, 합계 `5,100g`; ration current input/output gram은 변경 `0`. 신규 authored kg/BOM 변경은 없다.
- Direct WU·Embedded WU/EWU·가격: WU·EWU·가격·SCC 변경 `0`이다.
- 시간·공간·utility: show timing, facility footprint, utility, authored capacity 변경 `0`이다. durable slot profile은 기존 두 physical item의 합계를 투영한다.
- 위험·실패: prop Sink 실패 뒤 cart wear 누수, lost-stage orphan, restore candidate 불일치, current exact ration에 false process loss를 붙이는 것을 fail-loud한다.
- 대안·지배 전략·순환 차익: bespoke delivery/save를 추가하지 않고 공용 durable slot variation을 사용한다. 수량·가격·효과가 같아 신규 경제 지배 전략이나 차익은 없다.
- 실행 경로: `stage→common exact slot→prop Sink effect+cart wear→pending receipt finalize`; ration은 `runtime equation→proposal mismatch attribution→closed runtime contract`다.
- 저장 권위: Circus V4 + durable slot + Physical Items pending Sink가 교차 검증한다. ration은 ProductionRecipeSO/current ItemDefinitionSO와 semantic proposal 원장이 각각의 권위다.
- 자동 감사·증거: focused common use/save/circus/retarget, broad Captivity/ProductionEconomy, owner/recipe/F/G artifacts second-run identical, Unity compile, Console `0/0`, GameplayScene SHA 불변이다.
- 결정론적 artifact: owner CSV/TXT `39A5DD8434262142E9E6DE5ECC891B5F4967FB0226E97730D9A1D586C1289538` / `6F4A6F724BA2E108393E2C2241AFCA4622D7F0A83A75E26839732936AA4993B1`; recipe CSV/TXT `2E828D35FAAF6D99AF9653DE59D37B8971204912BC2FD6853C7432FCE9C3157C` / `8099D886A0EA4B1187880530A1E0ECDE72B99E2C2C668BCEEA723DC5778483AB`.
- 현재 판정: B `39/40`, C `16/39`, D recipe `210/355`; F/G/H exhaustive는 OPEN이다.

### balance:v27:defense-crafting-seed-urgent-exact-input-owners — 2026-08-31

- 정의 ID·종류: 기존 `combat.defense-facility`, `combat.equipment-crafting`, `economy.certified-seed`, `offense.urgent-mitigation` 입력 소유권이다. 신규 item·recipe·facility 정의는 없다.
- 등장 시대·연구: 기존 시설·recipe·연구 해금 조건을 유지한다.
- 역할: 네 도메인의 exact physical input lot, positive gram claim/profile, current-format restore join과 terminal custody drain을 공용 FacilityBuffer 권위에 연결한다.
- Before·After: Before는 inferred same-cell destination, runtime 직접 delivery/release 또는 저장되지 않은 owner pair가 남았다. After는 persistent order/facility ID 기반 exact destination, `LiveFacility + ExactGramRequired`, carried-aware release-before-revoke를 사용한다.
- 물리 BOM·질량: authored kg, BOM, quantity, facility capacity 변경 `0`. capacity는 현재 exact item/recipe lot 질량을 파생 투영한다.
- Direct WU·Embedded WU/EWU·가격: WU·EWU·가격·SCC 변경 `0`이다.
- 시간·공간·utility: action duration, footprint, 전력·용수·폐기물 수치 변경 `0`이다.
- 위험·실패: one-gram restore drift, torn claim/profile, facility loss, carried cargo가 남은 terminal revoke, input commit 재실행, stale Offense restore projection을 fail-loud한다.
- 대안·지배 전략·순환 차익: domain별 parallel storage나 임의 바닥 반환을 추가하지 않고 공용 exact owner variation을 사용한다. 생산량·소비량·가격이 같아 신규 지배 전략·차익은 없다.
- 실행 경로: `domain order→exact owner ensure→physical delivery→typed Transfer/WIP 또는 기존 domain consumption→carried-aware terminal release→paired revoke`다.
- 저장 권위: Defense state/outbox, Combat Craft V7/WIP, Certified Seed current-format order, Offense World V8와 Physical Items child authority가 각 owner pair를 교차 검증한다.
- 자동 감사·증거: 네 focused owner fixture, Offense strategic 11종, ProductionEconomy/Defense/Combat material broad, Unity current-source compile, owner manifest classification PASS와 second-run hash/length/mtime `0`을 통과했다. Combat editor factory는 production null 계약을 완화하지 않는 격리 exact-owner composition을 사용한다.
- 결정론적 artifact: owner CSV/TXT SHA-256 `B45A075896BD58C23D72BA4E9E77C36C1D6DF7592A323E182A6F7A34E6491B4E` / `FF34C3FBC81FDB3C430B1A24C596EFC49C5ECB4C5CD42E641A87A70B1CB725C2`; scene SHA `B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8`.
- 현재 판정: 네 owner 완료. Batch C/F input `22/39`, remaining `17`; F integrated `0/6`, G `0/19`, H exhaustive는 OPEN이다.

### balance:v27:research-overhaul-ammunition-unit-scale — 2026-08-31

- 정의 ID: `material:lead-shot`, `material:cartridge-paper`, `ammo:armor-piercing-cartridge`, `ammo:paper-cartridge`, `ammo:scatter-cartridge`, `ammo:smoke-cartridge`, `ammo:signal-flare`, `ammo:rune-cartridge`, `ammo:blasting-charge`, `ammo:trap-canister`, `supply:defense-mixed-ammo-box`와 각 생산 recipe다.
- 콘텐츠 종류: ResearchOverhaul 중간재·소모 탄약·방어 시설 보급 상자의 물리 단위, 스택과 조합식 질량 권위 보정이다.
- 정의·카탈로그·실행기 위치: `ResearchOverhaulContentAssetBuilder`의 named physical-mass policy → `ResourceItemDefinitionSO`/`ProductionRecipeSO` → `IPhysicalItemMassQuery`, FacilityBuffer, warehouse admission, haul planner, combat/defense individual-ammunition consumer다.
- 등장 시대와 연구: 기존 표준 탄약, 압력 총열, 룬 모듈, 심부 채굴, 동맹 신호 연구와 시설 해금 순서를 유지한다.
- 플레이어에게 주는 새 결정: 탄약 종류별 전투 소비량과 batch output은 유지한다. 가벼운 개별 탄약, 중량 발파 장약·함정 산탄통, 방어 보급 상자가 각각 실제 운반·시설 버퍼·창고 점유를 요구하므로, 급한 방어 준비와 지속 탄약 비축의 공간·노동 배분을 선택한다.
- 목표 시스템 결합 등급과 근거: `물리 BOM → 생산 WIP → FacilityBuffer → AI haul → warehouse → combat/defense consumption`의 동일 item gram을 사용한다. 중간재 lot와 개별 탄약 단위를 혼용하던 기존 generic mass는 이 경로의 결합을 왜곡했다.
- 발생 생산자와 실제 원인 증거: `lead-ingot 2,150g → lead-shot 12×2,100g`, `blasting input 6,200g → output 2×150g`, `armor-piercing input 10,150g → output 6×150g`가 current asset/recipe audit에서 확인됐다. `lead-shot`과 `cartridge-paper`, 고급 탄약은 generated default mass가 원인이다.
- 실제 도메인 명령·진행·해결 영수증: 기존 `ProductionRecipeSO` Transform과 frozen prepared-output receipt가 input vector, declared process loss, output vector를 exact-once로 보존한다. 전투·방어는 individual item count를 그대로 소비한다.
- 영속 흔적과 역반응 소비자: Item definition gram과 stack limit은 physical-item lot, carry, warehouse/FacilityBuffer admission, output buffer, defense supply와 current-format restore가 함께 읽는다. 전투 피해·명중·탄약 소비량에는 질량 수치를 직접 곱하지 않는다.
- 물리 BOM·입력·출력: lead shot `2,100g→150g`(lead ingot 1개→12개=1,800g, casting residue 350g); cartridge paper `900g→325g`(paper 2+starch 1=2,050g→6개=1,950g, trim 100g). Armor-piercing `275g×6`, paper cartridge `300g×12`, scatter `325g×6`, smoke `175g×6`, signal flare `275g×4`, rune cartridge `575g×6`, blasting charge `2,900g×2`, trap canister `1,900g×2`, mixed defense box `3,400g×1`으로 정한다. 각 input-output 차이는 recipe-specific deterministic process loss로 별도 귀속하며 output quantity는 변경하지 않는다.
- 직접 작업량과 계산 근거: existing recipe WU(`armor-piercing=106 WU` 포함), 생산 cycle과 research gating은 변경하지 않는다. 이번 변경은 단위 질량·stack만 보정하며 WU 재배분은 아래 검증 후 다음 상류-하류 단계에서만 검토한다.
- EWU와 목표 회수 기간: 당장 authored price/EWU를 변경하지 않는다. 변경 뒤 V27 ledger에서 acquisition, sale credit, SCC margin, defense readiness payback을 재생성해 현재 price가 kg와 무관한 경제 권위를 유지하는지 확인한다. 음의 최소 margin과 SCC 무차익을 유지해야 한다.
- 공간·전력·물·연료·정비: 신규 utility는 없다. cartridge paper와 ammunition max stack은 원칙적으로 약 `10.2~12.0kg` 범위가 되도록 cartridge paper `36`, armor/paper/scatter/smoke/signal/rune는 `40/40/36/64/40/20`, blasting `4`, trap `6`, mixed box `3`으로 조정한다. 시설 buffer와 warehouse는 count가 아니라 이 gram을 다시 투영한다.
- 위험·실패·회복 방식: 과적 stack, output-space block, partial haul, cancel/Downed, restore와 defense activation은 exact existing receipt를 사용한다. mass audit가 input/output/loss gram 또는 runtime/proposed value drift를 발견하면 apply를 fail-loud하고 해당 recipe를 OPEN으로 되돌린다.
- 사회·비가역 비용: 없음. 인명·관계·세력 보상 수치, 탄약 효과와 연구 해금은 변경하지 않는다.
- 기존 대안과의 장단점: 큰 잔차를 abstract loss로 등록하는 대안은 거부한다. 기존 output count와 individual consumer 의미를 유지하고, generic default weight만 family policy로 대체해 운반·저장 판단을 실제 defense load에 맞춘다.
- 지배 전략 방지 조건: 중량을 0 또는 generic 150g으로 두어 대형 폭약과 함정 보급을 공짜 운반하는 경로를 막는다. stack gram, buffer admission, SCC/market audit, combat readiness regression이 모두 통과해야 하며, 가격·보상을 통해 중량 오류를 상쇄하지 않는다.
- 저장 권위와 실행 명령: authored `ResourceItemDefinitionSO.UnitWeight/MaxStack`, recipe output vector와 mass-explanation descriptor가 권위다. Physical Items/Production prepared-output/FacilityBuffer/warehouse current-format save schema를 변경하지 않으며 restore는 same definition digest를 재검증한다.
- 자동 감사 ID와 전수 목록 포함 여부: `V27PhysicalMassExplicitSemanticDebugScenarios`, `V27PhysicalMassRecipeInventoryDebugScenarios`, `V27PhysicalMassFamilyProposalDebugScenarios`, `ProductionEconomyDebugScenarios`, output-capacity/physical-haul focused suites, V27 ledger and market/SCC audit에 전수 포함한다.
- 검증 매트릭스와 보고서 위치: `Artifacts/QA/v27-recipe-mass-balance-audit.txt`, `Artifacts/QA/v27-recipe-mass-balance.csv`, `Artifacts/QA/v27-physical-mass-family-proposals.*`, `Artifacts/QA/v27-balance-economy-256-seed.txt`, combat readiness and FacilityBuffer/haul reports. Apply 후 Unity compile, double-capture byte identity, current-format restore, individual combat/defense consume, heavy-output capacity failure/recovery를 실행한다.
- 현재 밸런스 상태: canonical source와 11개 item/recipe asset 적용 완료, recipe inventory는 `reviewed-exact` 11개와 closed `221/355`를 기록했다. price/EWU, all-build economy/combat/event tuning과 human play evidence 전에는 최종 밸런스 완료로 표시하지 않는다.

### balance:v27:combat-cover-authoring-parity — 2026-08-31

- 정의 ID: `building:9601`, `building:9602`, `building:9603` 목재 바리케이드·자루 방책·화살막이다.
- 콘텐츠 종류: 기존 방어 시설 BOM·건설 WU authoring source와 combat regression의 권위 일치다.
- 정의·카탈로그·실행기 위치: live `BuildingSO` assets → `CombatCoverAssetBuilder` → `CombatSystemDebugScenarios.VerifyCoverAssets`다.
- 등장 시대와 연구: 기존 즉시 배치 가능 조건과 security facility 역할을 유지한다.
- 플레이어에게 주는 새 결정: 없다. 현재 플레이 값은 유지하고 재생성으로 값이 낮아지는 숨은 경로만 제거한다.
- 목표 시스템 결합 등급과 근거: `construction BOM/WU → 건설 작업 → cover HP/block chance → 수리`의 live asset과 builder/test가 한 값을 사용한다.
- 발생 생산자와 실제 원인 증거: live asset과 final facility catalog는 `lumber×5/cloth×6/lumber×8`, `476/458/463 WU`, `8.4/11.9/14.7 repair WU`를 기록한다. legacy builder/test는 `3/4/5`, `24/34/42`를 기대해 combat cover regression을 fail시켰다.
- 실제 도메인 명령·진행·해결 영수증: 기존 construct work order, material consume, BuildingCoverAbility, repair command와 BuildingSO save를 사용한다.
- 영속 흔적과 역반응 소비자: building definition과 construction order/facility state가 기존 current-format 저장 권위다.
- 물리 BOM·입력·출력: current asset BOM을 유지한다. barricade `material:lumber×5`, bulwark `material:cloth×6`, arrow screen `material:lumber×8`; 신규 item/output 없음.
- 직접 작업량과 계산 근거: current asset construction `476/458/463 WU`, repair `8.4/11.9/14.7 WU`를 builder와 test가 그대로 재현한다.
- EWU와 목표 회수 기간: current construction cost `45/70/95`와 market/EWU는 변경하지 않는다.
- 공간·전력·물·연료·정비: footprint, cover HP `60/90/110`, block chance `35/55/70%`, utility와 maintenance `0`을 유지한다.
- 위험·실패·회복 방식: stale builder invocation과 stale test expectation을 fail-loud한다. construction/repair failure recovery는 기존 work-order custody 경로다.
- 사회·비가역 비용: 없음.
- 기존 대안과의 장단점: live asset 값을 낮은 legacy defaults로 되돌리지 않는다. 재생성 가능한 source parity가 미래 balance update의 안전한 기반이다.
- 지배 전략 방지 조건: BOM·WU·HP·block chance·refund rate 변화가 없으므로 신규 cheap-cover loop나 차익을 만들지 않는다.
- 저장 권위와 실행 명령: BuildingSO/construct order existing authority를 유지하며 save schema 변경 `0`이다.
- 자동 감사 ID와 전수 목록 포함 여부: `CombatSystemDebugScenarios.VerifyCoverAssets`는 세 asset의 role/layer/sprite/cover/BOM/construction·repair WU를 검사한다.
- 검증 매트릭스와 보고서 위치: V14 combat scenario, builder second-run no-diff, construction material/work-order and save/restore regression을 실행한다.
- 현재 밸런스 상태: builder와 regression source parity 수정 완료. 현재 Unity domain은 별도 consumables compile/import 상태로 source reload가 멈춰 있어 V14 재실행과 builder second-run evidence가 남아 있다.

### balance:v27:active-retarget-crop-wildlife-exact-owners — 2026-08-31

- 정의 ID·종류: 기존 generic production active bill/WIP, `economy.crop-plot`, `captivity.wildlife-care`의 물리 소유권 계약이다. 신규 콘텐츠 정의는 없다.
- 등장 시대·연구: 기존 production/crop/captivity 시설과 연구 해금 조건을 유지한다.
- 역할: 다중 시설 identity retarget을 all-or-none으로 만들고 crop sow/treatment 및 wildlife feed/water를 exact positive-gram FacilityBuffer owner에 연결한다.
- Before·After: Before active retarget은 same-ID generic adapter만 있어 N→1 active custody를 거부했고 crop/wildlife는 inferred destination이었다. After는 current-format authority snapshot rollback과 persistent facility 기반 exact owner pair를 사용한다.
- 물리 BOM·질량: authored kg·BOM·quantity 변경 `0`. crop capacity는 실제 sow/treatment lot, wildlife capacity는 active animals × authored daily units × catalog maximum unit grams에서 파생한다.
- Direct WU·Embedded WU/EWU·가격: WU·EWU·가격·SCC 변경 `0`이다.
- 시간·공간·utility: cycle time, footprint, utility, authored buffer capacity 변경 `0`이다.
- 위험·실패: partial N→1 publication, carried cargo 순간이동, WIP/claim/stack identity drift, terminal outbox 미수렴, release 실패 뒤 authority revoke, restore facility mismatch를 fail-loud한다.
- 대안·지배 전략·순환 차익: 신규 생산량·소비량·가격·fallback을 만들지 않는다. existing authority의 이동과 소유권 명시만 강화하므로 신규 지배 전략·차익은 없다.
- 실행 경로: `stable source capture→candidate world handoff→active authority publish→later participant commit→complete`와 `domain order→exact owner ensure→physical delivery/typed consume→carried-aware release→paired revoke`다.
- 저장 권위: Production Bill current format, Physical Items, Haul Intent, Crop Plot current format, Wildlife/Circus current format이 기존 validator/restore join을 통해 권위다. 신규 save field는 없다.
- 자동 감사·증거: N=3 active retarget focused, crop/wildlife focused, Captivity broad, ProductionEconomy broad, owner manifest current-source PASS, clean Console Warning/Error `0/0`, GameplayScene SHA 불변이다.
- 현재 판정: Batch B `40/40`; Batch C/F input `24/39`, remaining `15`; F integrated `0/6`, G `0/19`, H exhaustive는 OPEN이다.

### balance:v27:pharmacology-and-clinical-kit-unit-scale — 2026-08-31

- 정의 ID·종류: `craft:resin-balm`, `craft:ritual-reagent`, `craft:fang-poison`, `medicine:antiseptic`, `medicine:standard`, `medicine:advanced`, `medicine:antidote`, `drug:vitality-tonic`, `drug:dreamleaf-analgesic`, `drug:blood-stimulant`, `drug:mana-awakener`, `drug:hallucinogenic-distillate` 및 `medical:trait-analysis-kit`, `medical:cross-lineage-medium`, `medical:isolation-care-kit`, `medical:rejuvenation-serum`, `medical:organ-preservation-canister`, `medical:organ-regeneration-scaffold`, `medical:regenerative-medium`, `medical:fertility-treatment`, `medical:trauma-care-kit`, `medical:whole-body-regeneration-medium`의 물리 단위·stack·조합식 잔차 권위다.
- 등장 시대와 연구: 기존 약초학·증류·고급 약학·마취·자극제·외과·재생 배양·유전·전신 재생 연구와 시설 조건을 유지한다.
- 역할과 플레이 결과: 약품 1개는 용량·병·치료 꾸러미·수술 배지의 실제 준비 단위다. 의료실 비축은 가벼운 일회 투약분, 중량 수술 키트, 한 번에 한 건만 운반 가능한 전신 재생 배지로 갈린다. 치료 성능·투약 횟수·연구 해금은 유지하고 운반, 버퍼, 창고, 수술 준비의 노동·공간 비용만 물리 BOM과 일치시킨다.
- 정의·카탈로그·실행기 위치: core 약품은 `ResourceEconomyAssetBuilder`의 canonical item/recipe definition, 임상 키트는 `ResearchOverhaulContentAssetBuilder`의 named physical-unit policy, 각 recipe 잔차는 `V27ReviewedProductionMassExplanationCatalog` → `ProductionRecipeSO` descriptor가 소유한다. `IPhysicalItemMassQuery`, FacilityBuffer, warehouse admission, AI hauling, medical procedure supply, character medicine consumer가 같은 authored gram을 읽는다.
- 발생 생산자와 실제 원인 증거: core 약학 output은 `100–200g` generic 생성값이었지만 standard medicine batch는 `630g→320g`, advanced medicine은 `1,470g→140g`, stimulant·distillate는 `460–1,000g→100–400g`로 기록됐다. 임상 키트도 rune conductor `2,400g` 또는 sterile composite `1,200g` 입력을 `350–600g` output으로 축소했다. item 단위가 BOM 단위와 달라 발생한 오류다.
- 물리 BOM·입력·출력: core final unit은 resin balm `600g`, ritual reagent `1,100g`, fang poison `550g`, antiseptic `280g×2`, standard medicine `300g×2`, advanced medicine `1,500g`, antidote `600g`, vitality tonic `425g×2`, dreamleaf analgesic `425g`, blood stimulant `900g`, mana awakener `650g`, hallucinogenic distillate `900g`이다. 임상 final unit은 trait kit `1,950g`, cross-lineage medium `3,400g`, isolation kit `700g`, rejuvenation serum `2,000g`, organ canister `3,000g`, regeneration scaffold `2,600g`, regenerative medium `1,150g×2`, fertility treatment `1,400g`, trauma kit `2,200g`, whole-body medium `14,500g`이다. sterile bandage는 existing `500g×2`를 유지한다.
- typed residual: resin `30g`, ritual `100g`, poison `50g`, antiseptic `40g`, standard `30g`, advanced `110g`, antidote `50g`, tonic `50g`, analgesic `35g`, stimulant `100g`, awakener `80g`, distillate `100g`, sterile bandage `280g`, trait kit `150g`, cross-lineage `200g`, isolation `80g`, rejuvenation `150g`, canister `300g`, scaffold `300g`, regenerative medium `200g`, fertility `100g`, trauma kit `200g`, whole-body medium `1,200g`으로 deterministic processing/disinfection/sterile trimming residue를 기록한다. output quantity와 ingredient vector는 유지한다.
- 직접 작업량·EWU·가격: 기존 recipe WU, 작업대 throughput, treatment potency, substance effect, procedure effect, item price와 EWU는 변경하지 않는다. 변경 후 256-seed market/SCC와 medical readiness payback 자료로 price·EWU 조정 필요 여부를 따로 판정한다.
- 공간·전력·물·연료·정비: stack은 ordinary supply가 약 `10–12kg`이 되도록 balm `20`, ritual `10`, poison `20`, antiseptic `40`, standard `36`, advanced `8`, antidote `18`, tonic `24`, analgesic `24`, stimulant `12`, awakener `16`, distillate `12`, sterile bandage `20`, trait `6`, cross-lineage `3`, isolation `16`, rejuvenation `5`, canister `3`, scaffold `4`, regenerative medium `10`, fertility `8`, trauma `5`, whole-body `1`로 정한다. utility·footprint·maintenance는 기존 값을 유지한다.
- 위험·실패·회복: source reload 전 stale builder apply, output-space 부족, partial haul, treatment/procedure cancel, carrier downed, restore 중 definition digest 불일치를 existing prepared-output and exact-custody receipt가 처리한다. mass audit는 proposal/runtime kg drift, residual descriptor 누락, 0g unit, max-stack 오류를 fail-loud한다.
- 대안·지배 전략·순환 차익: 대형 input을 abstract loss로 처리하거나 모든 약품을 50-stack으로 남기지 않는다. 고급 수술 공급품의 공짜 대량 저장·운반을 막고, 치료 능력이나 가격을 중량 오류 보정 수단으로 사용하지 않는다.
- 저장 권위와 실행 명령: ItemSO `unitWeight/maxStack`, RecipeSO input/output vector와 process-loss descriptor가 권위다. physical lots, production receipt, FacilityBuffer, warehouse, medical procedure current-format save schema는 변경하지 않는다.
- 자동 감사·검증 매트릭스: `V27PhysicalMassExplicitSemanticDebugScenarios`, `V27PhysicalMassRecipeInventoryDebugScenarios`, `ProductionEconomyDebugScenarios`, medical item/procedure consumer, prepared output cancel/restore, output-capacity/haul, `V27BalanceEconomySimulationDebugScenarios`, market/SCC 256 seed에 포함한다. 보고서는 `Artifacts/QA/v27-recipe-mass-balance.*`, `Artifacts/QA/v27-balance-economy-256-seed.txt`와 medical/capacity focused report에 남긴다.
- 현재 밸런스 상태: source authority와 23개 canonical item/recipe asset 적용, YAML 기반 물리 BOM 전수 재계산 완료. current-source Unity compile, recipe/runtime closure, 256-seed 경제, 실제 의료 소비·복원, 단독/중량 공급 output-buffer 회귀가 남아 있다.

### balance:v27:vaccine-tool-and-typed-residual-closure — 2026-08-31

- 정의 ID·종류: seven `medicine:vaccine:*` doses, `tool:sewing-kit`, `material:granulated-powder`, `ammo:tranquilizer-dart`, funeral/alliance/performance supply, curd, anesthetic, artificial eye와 조합식 잔차의 물리 권위다.
- 등장 시대와 연구: 예방 접종, 재봉, 탄약, 장례·동맹·공연, 외과 연구와 기존 작업대·해금 조건을 유지한다.
- 역할과 플레이 결과: 백신은 4회분 batch를 의료실로 옮기는 실제 보급품, 재봉 도구는 내구성 있는 1개 장비, 과립 화약과 행사 공급품은 창고·운반 부담을 갖는 묶음으로 정한다. 공연 소품 상자의 `1,950g`은 circus exact supply contract와 함께 유지한다.
- 정의·카탈로그·실행기 위치: ResearchOverhaul/V22/Surgery builder의 item·recipe authoring, `V27ReviewedProductionMassExplanationCatalog`, ItemSO/RecipeSO, physical item custody, FacilityBuffer, warehouse, vaccine/medical, apparel maintenance, circus and defense consumers가 동일 값을 읽는다.
- 발생 생산자와 실제 원인 증거: six standard vaccine batches는 antigen `900g` + advanced medicine `1,500g` + water `500g`=`2,900g`인데 `400g×4`만 남겼고, mana-pox는 `3,100g→600g×4`였다. sewing kit은 `machine-parts 2,100g→50g`; granulated powder는 `5,300g→5,100g`; prop box는 `2,800g→1,950g`이다.
- 물리 BOM·입력·출력: six standard vaccines `700g×4` with `100g` residue, mana-pox `750g×4` with `100g` residue, sewing kit `2,000g` with `100g` residue로 정한다. granulated powder `850g×6`, tranquilizer dart `150g×6`, funeral kit `1,200g`, alliance signal kit `1,150g`, prop box `1,950g`, anesthetic `120g`, artificial eye `1,800g`, curd `450g×2`는 현재 단위를 유지한다.
- typed residual: granulated powder `200g` screening, tranquilizer dart `300g` needle/forming/fill, funeral `150g`, alliance signal `100g`, prop box `850g` frame/fabric trim, curd `2,000g` whey separation, anesthetic `570g` distillation vapour/herbal filter, artificial eye `900g` lens grinding/arcane calibration을 named descriptor로 기록한다. prop box와 anesthetic은 exact runtime/package contracts 때문에 kg를 변경하지 않는다.
- 직접 작업량·EWU·가격: existing recipe WU, vaccine efficacy, medical effect, sewing tool use/wear, circus result, defense effect, price와 EWU는 변경하지 않는다. batch price/EWU는 256-seed market/SCC와 medical/maintenance readiness payback 결과로 별도 조정한다.
- 공간·전력·물·연료·정비: standard vaccine stack `15`, mana-pox `14`, granulated powder `12`, tranquilizer dart `72`, funeral kit `8`, alliance kit `9`, prop box `5`; each ordinary stack is near `10–12kg`. sewing kit, artificial eye and anesthetic remain single-unit equipment or packaged-dose forms.
- 위험·실패·회복: vaccine/medical cancellation, partial haul, tool use, circus supply Sink, carrier downed, output-space failure and current-format restore use existing prepared-output/exact-custody receipts. descriptor conflict, source/asset kg drift, contract-mass mismatch and 0g item fail loud.
- 대안·지배 전략·순환 차익: vaccine dose를 generic `400g`, sewing tool을 `50g`, event supply를 50-stack으로 남기지 않는다. circus contract mass와 anesthetic reusable vial topology를 바꾸지 않아 save join·effect·wear 우회를 만들지 않는다.
- 저장 권위와 실행 명령: authored ItemSO unitWeight/maxStack and RecipeSO input/output/process-loss descriptor are authoritative. Physical Items, durable tool, circus V4, medical and production save schemas remain unchanged.
- 자동 감사·검증 매트릭스: V27 recipe inventory/family/semantic, vaccine and character-medicine use, apparel tool use/save, circus exact supply/use/restore, surgery output, output-capacity/haul, 256-seed market/SCC and current-source Unity compile are required. Reports remain under `Artifacts/QA`.
- 현재 밸런스 상태: source authority와 canonical asset 적용, YAML 물리 BOM 재계산 완료. current-source Unity recipe inventory, 백신·의료·재봉·circus·수술 consumer/restore, output-capacity/haul, 256-seed market/SCC 회귀가 남아 있다.

### balance:v27:food-and-fermentation-residual-closure — 2026-08-31

- 정의 ID·종류: `seasoned-filling`, fermented/brined/preserved vegetable, flour, cheese, fine meals, wine/beer/spirit/syrup, hay feed의 existing recipe physical residual disposition이다. 신규 item·recipe·시설은 없다.
- 등장 시대와 연구: 기존 조리, 보존, 제분, 낙농, 양조와 사료 연구·시설 해금을 유지한다.
- 역할과 플레이 결과: 음식 1개·음료 1병·사료 1단위의 kg, 영양, 만족, 보존시간, 가격, recipe output count는 유지한다. player는 이미 authored된 식재료·시간·연료·저장 선택을 그대로 사용하고, 조리·발효·제분 과정에서 빠지는 수분, 껍질, 유청, 가스가 ledger에 명시된다.
- 정의·카탈로그·실행기 위치: `ResourceEconomyAssetBuilder`와 Workshop recipe ItemSO/RecipeSO, `V27ReviewedProductionMassExplanationCatalog`, Production transform receipt, warehouse/FacilityBuffer/haul, food and feed consumer가 같은 material vector와 loss descriptor를 사용한다.
- 발생 생산자와 실제 원인 증거: current asset recapture에서 seasoned filling `1,850→1,300g`, fermented pickle `1,400→900g`, flour `1,050→600g`, cheese `900→800g`과 meal/beverage residual `2–400g`이 descriptor 없이 남았다. output item units는 downstream food/feed consumer와 기존 stack/use semantics에 맞는다.
- 물리 BOM·입력·출력: item kg, input/output quantity, WU, processing time, utility, output cost allocation 변경 `0`이다. 각 recipe의 measured positive residual만 typed process disposition으로 추가한다.
- typed residual: cooking/preparation `100–550g`, fermentation `50–500g`, spirit extraction `150g`, syrup evaporation `25–50g`, flour milling `450g`, cheese whey press `100g`, hay screening `2g`을 exact current input-output difference로 기록한다.
- 직접 작업량·EWU·가격·공간: recipe WU, fuel/water, preservation duration, item price/EWU, storage/stack/haul mass와 nutrition·mood effect를 유지한다.
- 위험·실패·회복: prepared output cancel, partial haul, food spoilage, animal feed, save/restore는 existing receipt and item custody authority를 유지한다. future item/BOM change가 residual gram과 descriptor reason을 어기면 inventory audit가 fail loud한다.
- 대안·지배 전략·순환 차익: 음식 output mass·효과·가격을 임의로 바꾸거나 residual을 generic tolerance로 처리하지 않는다. output count 변화와 food-to-feed/market loop를 만들지 않는다.
- 저장 권위와 실행 명령: RecipeSO process-loss descriptor와 existing ItemSO mass are authoritative; save schema, market credit and consumer state changes `0`이다.
- 자동 감사·검증 매트릭스: V27 recipe inventory/family, ProductionEconomy food/feed and spoilage, restaurant/animal feed consumer, prepared output restore, market/SCC 256 seed, source-current Unity compile and deterministic double capture are required.
- 현재 밸런스 상태: source catalog와 16개 canonical recipe asset descriptor 적용, YAML 물리 BOM 재계산 완료. residual 없는 네 `recipe:incinerate-*`는 `ProductionFlowRole.Sink`의 input-only/output-empty terminal disposal로 별도 검증됐다. current-source Unity inventory, food/feed·spoilage consumer/restore, 256-seed market/SCC 회귀가 남아 있다.

### balance:v27:transform-physical-input-closure — 2026-08-31

- 정의 ID·종류: 현재 `ProductionFlowRole.Transform`인 97개 input/output 조합식의 물리 BOM 권위다. 기존 `AbstractProcessAddition` 97개 중 실제 질량을 생성하는 89개와 `grain-porridge`, `mushroom-soup`, `roasted-meat`, `root-stew`, `brined-vegetable`, `curd`, `dough`, `fermented-pickle`를 대상으로 한다. 이미 단위를 바로잡은 백신 7종과 artificial eye는 이 batch에서 제외한다.
- 등장 시대와 연구: 기존 연구, workstation, facility, unlock, recipe ID와 생산 경로를 유지한다. 의복·직물, 화약·금속, 부품·도구, 농업 공급품, 조리의 해금 순서를 바꾸지 않는다.
- 역할과 플레이 결과: output 1개가 실제로 가질 질량을 유지한 채, 그 질량을 만들 재료·물·보조재를 production input으로 회수한다. 고급 의복, 방탄 소재, 부품, 농업 공급품과 조리식은 이전보다 실제 재료·창고·운반·작업대 선택을 요구한다.
- 정의·카탈로그·실행기 위치: 각 원본 content builder의 recipe definition, canonical RecipeSO input vector, `V27ReviewedProductionMassExplanationCatalog`, production receipt, FacilityBuffer, warehouse, haul, market/EWU calculator와 save restore가 같은 BOM을 읽는다.
- 발생 생산자와 실제 원인 증거: 정적 recapture는 missing item mass `0`, valid Source external mass `23`건 `52,220g`, valid Transform creation `97`건 `68,202g`을 기록했다. clean-water와 wastewater를 모두 포함한 값이다. 가장 큰 family는 V22 tailoring `32건/10,040g`, powder mill `2건/8,450g`, V3 armor tailoring `6건/5,150g`, material-test `4건/4,350g`이다.
- 금지되는 권위: `process-addition@1|kind=AbstractProcessAddition|reason=game-unit-abstraction-addition`은 Transform의 물리 질량 생성 근거로 사용하지 않는다. output보다 큰 input mass를 `process-loss`로 설명하지 않는다.
- 물리 BOM: 각 대상 recipe는 output maximum mass와 clean-water input을 합친 뒤, 최소 `20–200g`의 공정 손실 또는 명시된 수분/가스/부산물만 남도록 입력 수량을 확정한다. 재료 종류와 수량은 recipe별 BOM database가 권위다.
- 물 입력과 폐수: 수분을 제품에 보존하는 조리식은 `cleanWaterPerCycle`로 넣고, 증발·폐수는 기존 fluid ledger와 typed disposition으로 귀속한다. 금속·섬유·부품·화약에 보이지 않는 물을 추가해 질량을 메우지 않는다.
- 직접 작업량: existing WU, preparation/finishing work, processing hour와 workforce skill gate는 유지한다. 입력 증가 뒤 EWU와 throughput payback을 재계산하며, 추가 재료를 공짜 작업량으로 상쇄하지 않는다.
- 가격·EWU: ItemSO price와 market seed value는 첫 apply에서 고정한다. 새 BOM의 embedded work·material value가 기존 price/EWU와 충돌하면 256-seed market/SCC 결과로 별도 가격 조정 record를 만든다.
- 공간·전력·물·연료·정비: item kg, max stack, facility utility, power, footprint, maintenance는 유지한다. 증가한 input lot의 haul, buffer capacity와 output blocked 상태는 실제 비용으로 남는다.
- 위험·실패·회복: output-space 부족, partial haul, reserved input, craft cancel, worker downed, source reload, restore는 기존 exact-custody receipt와 prepared output 경로를 사용한다. BOM patch는 새 save field를 만들지 않으며, 예약된 구버전 input vector는 current-format restore 규칙으로 보존한다.
- 대안과 전략: 고급 산출물의 재료 비용은 source resource, 가공 재료, 운반량, 작업대 점유를 함께 요구한다. 단일 cheap-material 입력으로 모든 의복·기계·군수품을 생산하는 지배 경로를 허용하지 않는다.
- 순환 차익: transform output kg가 input kg를 초과하는 market loop를 제거한다. 가격이 높은 output에 저질량 input만 투입해 운반과 warehouse를 우회하는 경로도 제거하며, source의 world/animal/spoilage external mass는 별도 flow role로 유지한다.
- 변경 범위: ItemSO kg, output amount, item effect, research, facility, consumer effect, WU, item price, save schema는 이 batch에서 변경하지 않는다. canonical RecipeSO input vector, 필요한 clean-water amount, mass descriptor만 바꾼다.
- 콘텐츠 DB: `docs_final/content-db/fields/production-facilities/production-recipe.csv`와 `docs_final/game-design/content-plans/production/production-recipe-contract.csv`의 recipe row에 final input vector, water, maximum output grams, residue grams, loss kind, source builder와 apply status를 기록한다. recipe ID가 BOM row의 stable key다.
- 저장 권위: RecipeSO input/output vector와 mass descriptor가 생산 직전의 단일 권위다. PhysicalItem lot, order reservation, receipt, FacilityBuffer와 warehouse slot은 그 snapshot을 저장하며 old save를 retroactively rewrite하지 않는다.
- 실행 명령: content builder regenerate 뒤 V27 approved BOM apply를 한 번만 수행하고, asset digest를 capture한다. source와 canonical asset의 input vector가 다르면 이후 economic simulation을 중단하고 source/asset drift로 fail한다.
- 자동 감사: V27 inventory는 Transform의 `minimumResidual < 0`을 `external-input-authority-missing`으로 fail한다. source/sink shape, exact kg semantic, BOM database coverage, descriptor parity, recipe duplicate, current-source deserialization을 함께 검사한다.
- 검증 매트릭스: 256-seed market/SCC, apparel maintenance/use/restore, production cancel/resume, output capacity, haul, food/feed consumer, weapon/ammo consumption, medical prepared-output, deterministic double capture를 family별로 다시 통과해야 한다.
- 현재 밸런스 상태: V22 tailoring·weaving 41건, ResearchOverhaul 40건, core·Workshop 16건의 source/canonical asset에 final BOM을 적용했다. GameContentCatalog의 1,074 weighted item 기준 static recapture는 Transform `328`, unknown reference `0`, mass creation `0`을 확인했다. current-source Unity deserialization, 256-seed economy curve, consumer/restore 및 deterministic regenerate는 남아 있다.

### balance:v27:core-and-workshop-fluid-bom-closure — 2026-08-31

- 정의 ID·종류: `recipe:bedding-straw`, `cloth`, `garden-meal`, `grain-porridge`, `herbal-poultice`, `moonflower-tea`, `mushroom-soup`, `preserved-ration`, `roasted-meat`, `root-stew`, `brined-vegetable`, `curd`, `dough`, `expedition-ration-pack`, `fermented-pickle`, `salted-meat-stew`의 최종 Transform 물리 BOM과 물·폐수 값이다.
- 등장 시대와 연구: 기존 섬유, 축산, 조리, 약초학, 보존, 제빵, 원정 식량 연구·작업대·support 조건을 그대로 유지한다.
- 역할과 플레이 결과: 가벼운 섬유, 침구, 조리식, 약초 찜질약, 염지·발효·원정 식량은 표시된 산출물 질량만큼의 섬유·식재료·전분·물·유청을 실제로 통과시킨다. 창고, 운반, 물 공급, 조리대 점유의 비용이 생산물의 kg와 일치한다.
- 발생 생산자와 실제 원인 증거: GameContentCatalog가 참조한 ItemDefinitionCatalogSO의 weighted item `1,074`, missing asset `0`, unweighted item `0`, unknown recipe reference `0`을 기준으로 재계산했다. 잔여 생성은 core `10 / 4,480g`, workshop `6 / 3,800g`, 합계 `16 / 8,280g`이다.
- 최종 BOM: bedding은 grass-straw `10`+shade-fiber `2`→`1,000g×2`; cloth는 shade-fiber `4`→`200g×2`; preserved ration은 ration-mixture `2`+starch `2`→`500g×3`; expedition ration은 ration-mixture `1`+salted-meat `1`+starch `2`→`750g×2`로 고정한다. 나머지 12개는 기존 solid input vector와 output count를 유지한다.
- 물·폐수: garden `0.70/0.20`, porridge `1.70/0.10`, poultice `0.25/0`, tea `0.55/0.10`, mushroom soup `0.95/0.10`, roasted meat `0.15/0`, root stew `0.65/0.10`, brined vegetable `0.70/1.00`, curd `0.20/2.10`, dough `0.30/0.10`, fermented pickle `0.60/1.00`, salted-meat stew `0.60/0` kg per cycle로 고정한다. 물은 조리·침출·염지·발효에만 사용하며 금속·섬유 물량을 물로 보정하지 않는다.
- typed residual: bedding `40g`, cloth `80g`, garden `50g`, porridge `100g`, poultice `50g`, tea `30g`, soup `50g`, preserved ration `100g`, roast `50g`, root stew `50g`, brined vegetable `100g`, curd `100g`, dough `50g`, expedition ration `200g`, pickle `100g`, salted stew `100g`이다. 섬유 손실, 조리·훈연 증발, 약초 침출, 염지·발효 가스, 유청 분리, 반죽 가루 손실의 named descriptor를 남긴다.
- 직접 작업량·EWU·가격: required/preparation/finishing WU, processing hour, output quantity, item kg, max stack, nutrition, mood, preservation, effect, unit price와 existing direct EWU는 변경하지 않는다. 새 BOM의 embedded cost는 후속 market/SCC curve record에서 재판정한다.
- 시간·공간·utility: facility footprint, support, fuel, power, maintenance, batch duration은 유지한다. 정해진 물·폐수량만 production water ledger와 buffer capacity에 새로 반영한다.
- 위험·실패: 물 부족, output-space 부족, partial hauling, reserved input, prepared-output cancel/restore, downed carrier, source reload와 old receipt restore는 기존 production custody·receipt 경계를 사용한다. generated source와 asset의 input/water/waste drift는 감사에서 실패한다.
- 대안: output kg를 내려 수치를 맞추거나 process-loss로 질량 생성을 가리는 방식을 사용하지 않는다. 실제 수분을 보존하는 식품은 water ledger, 구조·식량 성분은 item BOM으로 기록한다.
- 지배 전략·순환 차익: 280g의 섬유로 2kg 깔짚을 만들거나 1.2kg 식재료로 1.5kg 원정 배급을 만드는 우회를 제거한다. 물이 필요한 대량 조리·염지·발효는 급수와 폐수 처리 능력에 따라 확장 속도가 갈린다.
- 실행 경로: core/workshop builder→canonical RecipeSO→mass explanation catalog→production reservation→water/facility buffer→output receipt→warehouse/haul→food·medicine·animal consumer다.
- 저장 권위: RecipeSO input/output vector, clean-water/wastewater fields, process-loss descriptor가 생산 snapshot의 권위다. item definition, physical lot, FacilityBuffer, warehouse, production receipt와 save schema는 변경하지 않는다.
- 자동 감사: ItemDefinitionCatalogSO material lookup, transform equation, source/asset vector parity, descriptor parity, 0g/unknown reference/duplicate recipe, deterministic double capture를 요구한다.
- 경제 검증: 256-seed market/SCC, starter·water-limited·textile·preservation build의 material flow, haul/buffer occupancy, food/medicine/feed consumer와 output-blocked recovery를 다시 측정한다.
- 전투·사건 검증: 약초 찜질약 소비, 원정 배급 준비, siege·weather로 인한 물·식량 shortage, partial delivery/cancel/restore가 기존 효과·보상 수치와 일치하는지 확인한다.
- 악용 검증: dry cookbench에서 물 없는 음식 mass 증가, wastewater만 늘려 whey/brine 생성, process descriptor를 생성 근거로 쓰는 경로, source rebuild가 asset BOM을 되돌리는 경로를 fail-loud로 막는다.
- 결정론·문서: source regenerate와 canonical asset apply를 각각 두 번 실행해 변화 `0`을 확인하고, production recipe CSV 계약의 final input/water/waste/residue/status를 함께 갱신한다.
- 현재 밸런스 상태: 16개 source와 canonical RecipeSO 적용을 마쳤다. 정적 검증은 16/16 process-loss descriptor, residue `30–200g`, nonpositive `0`, transform mass creation `0`, abstract-process-addition reference `0`을 확인했다. current-source Unity deserialization, 256-seed economic curve, consumer/restore와 deterministic regenerate는 남아 있다.

### balance:v27:fluid-authored-unit-correction-and-six-adult-capacity — 2026-08-31

- 정의 ID·종류: 기존 `garden-meal`, `grain-porridge`, `herbal-poultice`, `moonflower-tea`, `mushroom-soup`, `roasted-meat`, `root-stew`, `brined-vegetable`, `curd`, `dough`, `fermented-pickle`, `salted-meat-stew` 12개 Transform의 clean-water/wastewater authored-unit 교정이다. 신규 item·recipe·facility는 없다.
- 등장 시대·연구: 기존 조리·약초·보존·낙농·발효 연구, workstation과 support 조건을 유지한다.
- 역할: 기준서의 유체 값은 `kg/cycle`인데 Production 런타임은 `1 authored unit = 500g`으로 해석한다. kg 숫자를 그대로 직렬화해 절반만 소비·배출하던 단위 불일치를 제거한다.
- Before·After: serialized clean/waste는 garden `0.7/0.2→1.4/0.4`, porridge `1.7/0.1→3.4/0.2`, poultice `0.25/0→0.5/0`, tea `0.55/0.1→1.1/0.2`, soup `0.95/0.1→1.9/0.2`, roast `0.15/0→0.3/0`, root `0.65/0.1→1.3/0.2`, brined `0.7/1→1.4/2`, curd `0.2/2.1→0.4/4.2`, dough `0.3/0.1→0.6/0.2`, pickle `0.6/1→1.2/2`, salted stew `0.6/0→1.2/0`이다. 물리 kg/cycle 설계값 자체는 변경하지 않고 serialized unit만 바로잡았다.
- 물리 BOM·질량: solid input, output count와 item kg는 변경 `0`. 12개 식은 current ItemSO mass와 500g fluid unit로 다시 계산해 residual `30–100g`이며 mass creation `0`이다.
- Direct WU·Embedded WU/EWU: recipe direct WU는 변경 `0`. 다만 실제 물 생산 반복 노동은 6인 기준 `10.530→17.560 WU/day`, 총 반복 노동은 `78.538→85.568 WU/day`로 증가했다. embedded EWU·가격/SCC 전수 재생성은 D/H 후속 gate다.
- 시간: crop growth, cooking cycle, processing hour와 게임 하루 `180초`는 변경하지 않는다.
- 공간: 6인 7일 생존 비축은 `57,700→77,700g`; normal storage 70% gate를 포함한 asset-backed 256-seed 결과 최소 폭은 인구 `1/3/6/12/18/24 = 27/27/29/51/71/87`이며 minimum headroom `300 permille`다.
- 전력·용수·폐기물: clean water와 wastewater는 위 authored-unit대로 실제 Production water ledger에 반영된다. wastewater composition은 기존 FoodProcessWashwater/Whey/Brine를 유지한다.
- 위험·실패·복구: 단위 회귀, dry-cook mass creation, wastewater 누락, output-space 대기, partial haul, cancel/restore를 fail-loud한다. current-format receipt는 결정된 fluid vector를 재굴림하지 않는다.
- 기존 대안: output kg를 낮추거나 residual tolerance로 숨기는 대신, 단일 `ProductionFluidMassRules.GramsPerAuthoredUnit=500` 권위에 authoring 값을 맞춘다.
- 장점·단점: 질량 보존과 물류 비용이 실제 수분량과 일치한다. 대신 초기 물 생산·저장 부담이 증가하지만 6인 반복 노동 `31.7%`, growth available `43.3%`로 목표 범위를 유지한다.
- 지배 전략·순환 차익: 물을 절반만 내고 같은 식품 kg·영양·가격을 얻던 경로를 제거한다. output quantity·nutrition·price를 올리지 않아 신규 지배 생산이나 양의 순환을 만들지 않는다.
- 실행 경로: `ProductionWorkshopContentAssetBuilder→RecipeSO fluid fields→ProductionFluidMassRules→WIP/receipt→water/wastewater authority→FacilityBuffer/warehouse/haul→food/medicine/feed consumer`다.
- 저장 권위: current RecipeSO fluid vector와 production receipt가 권위다. 신규 save field나 과거 세이브 마이그레이션은 없다.
- 자동 감사·실전 증거: explicit semantics `414/414`, transform fixtures `42`, recipe inventory `355/355`, explanation/external-input/runtime mismatch `0/0/0`, second descriptor apply `changed=0 exact=294`, 6인 closed loop PASS, asset-backed `6×256=1,536/1,536` PASS다.
- 결정론·산출물: recipe inventory 내부 double capture byte-identical PASS; 공간 CSV source digest `71f03d9183d48c8322fda93baf06b372dddafbfbba23b9a2b856460e9523b6d4`, SHA-256 `d8aa14655c8e932e4f7691b0c8a90182c3004b97d1a97e2c6b67c4ddca9b6a99`; GameplayScene SHA 불변이다.
- 현재 판정: `밸런스 시뮬레이션 검증` 중 공식·정적 256-seed 공간 단계까지 통과했다. EWU·가격/SCC, 실제 consumer/restore, paired run과 3-seed 실전 보정 전에는 D/H parent와 전체 밸런스를 완료 처리하지 않는다.

## V27 generic recipe natural execution utility-authoring P0 (2026-09-01)

### `balance:v27:production-support-utility-authoring-p0`

- 시대: 기존 WS02/04/06/07/10/11/12/14/15/18/19/20/23/25/26/28 해금 시대를 유지한다.
- 역할: generic recipe의 실제 support node가 `ProductionCycleUtilityService`의 전력·상수·폐수 preflight와 소비 경로에 참여하게 한다.
- Before: 16개 support의 `BuildingProductionSupportAbility`에는 전력·유체 수요가 있었지만 `BuildingUtilityConnectionAbility`가 없었고, requiresPower 11개에는 `BuildingPowerConsumerAbility`도 없었다. 따라서 실제 topology에서는 요구량을 충족할 수 없었다.
- After: 16개 support에 authored 수요의 합집합 채널을 부여한다. WS04/06/07/28은 Power|CleanWater|Wastewater(7), WS10/12/18/20/23/25/26은 Power(1), WS02는 CleanWater(2), WS11/14/15/19는 CleanWater|Wastewater(6)이다. requiresPower 11개는 `1 power/s`, Production priority, minimum supply fraction `1.0`을 사용한다.
- 물리 BOM: 변경 없음. 기존 support 건설 BOM을 유지한다.
- Direct WU: 변경 없음. 기존 건설·수리·운영 WU를 유지한다.
- Embedded WU/EWU: 수치 변경 없음. 실제 전력·급배수 운영비는 기존 authored cycle demand를 누락 없이 실행하는 비용으로 귀속하며, 후속 EWU 재생성에서 검증한다.
- 시간: support의 기존 cycle 처리량을 유지한다. 전력 소비 `1/s`는 `BuildingPowerConsumerAbility` 기본 최소의 정직한 nonzero demand이며 무료 전력 연결을 허용하지 않는다.
- 공간: 시설 footprint 변경 없음. 실제 generator·상수 저장·폐수 저장과 연결 가능한 grid topology가 별도로 필요하다.
- 전력·용수·폐기물: connection `maxThroughput=100`, `normallyOpen=true`. clean/waste 수량은 기존 support의 `cleanWaterPerCycle`/`wastewaterPerCycle`와 조성값을 단일 수요 권위로 유지한다.
- 위험: 전력 또는 배관이 없으면 `ProductionUtilitiesUnavailable`로 fail-loud한다. fixture가 support를 mock하거나 descriptor 값으로 통과시키면 안 된다.
- 대안: WS02의 manual 표시만으로는 support manual destination owner가 생기지 않으므로, current V27 자연 실행은 실제 CleanWater 연결을 사용한다. 별도 manual-owner 확장 전에는 가상 물통 fallback을 만들지 않는다.
- 지배 전략 방지: 유틸리티가 필요한 고급 support를 무전력·무배관으로 무료 운용하던 암묵 우회를 제거한다.
- 순환 차익 방지: 아이템 BOM·출력·회수·가격은 바꾸지 않는다. utility debit 누락만 제거하며 SCC tolerance와 mEWU 양자화 규칙은 그대로다.
- 실행 경로: `BuildingSO → IndustrialInfrastructureTopology → ElectricalNetworkRuntime/FluidNetworkRuntime → ProductionCycleUtilityService → ProductionBillRuntime`.
- 저장 권위: 기존 BuildingSO authoring과 기존 infrastructure/save authority를 사용한다. save schema와 과거 세이브 migration은 변경하지 않는다.
- 자동 감사·실전 증거: `ProductionWorkshopContentAssetBuilder`가 같은 ability 조합을 재생성하도록 고정하고 현재 16개 YAML을 동기화했다. Roslyn/YAML 정적 검증 뒤에도 실제 GameplayScene compile·natural recipe PlayMode·두 번째 serialization diff 0이 남아 있으므로 이 기록만으로 H 완료를 주장하지 않는다.

### `balance:v27:recipe-facility-fluid-topology-p0`

- 시대: 기존 P02 양조장, P17 사료배합대, P18 약제대 해금 시대를 유지한다.
- 역할: 시설 자체 레시피 유체 요구량이 실제 상수·폐수 network에 연결되게 한다.
- Before: 세 시설은 기존 Power(1) 연결만 가졌다. P02의 발효주·식초·포도즙, P17의 사일리지, P18의 약초 찜질약은 clean-water 또는 wastewater 요구가 있으나 recipe-derived manual destination authority가 없어서 자연 실행이 불가능했다.
- After: 기존 recipe demand 합집합에서 도출한 최소 채널만 추가한다. P02는 Power|CleanWater|Wastewater(7), P17/P18은 Power|CleanWater(3)이다. 기존 facility 전력 소비 `5/s`, 처리량 `25`, minimum supply fraction `0.75`는 바꾸지 않는다.
- 물리 BOM: 변경 없음.
- Direct WU: 변경 없음.
- Embedded WU/EWU: 수치 변경 없음. 기존 recipe fluid debit의 실제 실행 가능성만 복구하며 후속 EWU 재생성에서 귀속을 재검증한다.
- 시간: recipe cycle 시간과 시설 처리량 변경 없음.
- 공간: 시설 footprint 변경 없음. 실제 I08/I09 또는 동등한 저장 topology의 공간은 던전 공간 검증에 포함한다.
- 전력·용수·폐기물: P02는 clean-water와 wastewater 모두, P17/P18은 clean-water만 실제 network를 사용한다. 폐수 요구가 없는 시설에 Wastewater 채널을 추가하지 않는다.
- 위험: manual fallback 표시는 물리 destination owner 증명이 아니므로 natural verifier가 이를 가정하지 않는다. 배관·저장 용량 부족은 typed utility failure다.
- 대안: recipe/support 수요에서 manual owner를 투영하는 공용 runtime 계약은 후속 확장 가능하나, 현재 P0는 실제 pipe 경로로 닫는다.
- 지배 전략 방지: 유체 수요 레시피가 infrastructure 없이 실행되는 무료 경로를 만들지 않는다.
- 순환 차익 방지: 생산 입출력·손실·판매가를 변경하지 않으며 utility 비용 누락만 방지한다.
- 실행 경로: `ProductionRecipeSO fluid demand → facility BuildingUtilityConnectionAbility → FluidNetworkRuntime → ProductionCycleUtilityService.TryConsumeCycleUtilities`.
- 저장 권위: 기존 facility BuildingSO와 fluid infrastructure current-format save를 사용하며 schema를 바꾸지 않는다.
- 자동 감사·실전 증거: `IndustrialInfrastructureAssetBuilder.PatchProductionFacilities`와 현재 P02/P17/P18 YAML을 동일 채널로 동기화했다. Roslyn/YAML 정적 검증 이후 실제 GameplayScene utility topology·cycle consume·restore·Console 0/0은 별도 open gate다.

### `architecture:v27:runtime-building-archetype-content-identity` — 2026-09-01

- 정의 ID·종류: `building:runtime:world-resource-node`, `building:runtime:world-filth-work-target`, `building:runtime:exterior-zone:{zone}:{layer}` 42개 runtime-only BuildingSO의 canonical content identity다.
- 등장 시대·연구: 기존 월드 자원·오염·외부 구역 생성 시점을 그대로 유지하며 신규 연구나 해금을 추가하지 않는다.
- 역할·플레이 결과: 음수 numeric ID는 기존 runtime archetype lookup 권위로 유지하고, V27 원장·감사·의존성 join에는 별도의 immutable `contentDefinitionId`를 사용한다. 플레이어 선택과 시설 동작은 바뀌지 않는다.
- Before·After: 42개 에셋의 `contentDefinitionId`가 빈 값에서 위 canonical ID로 바뀌고 source note만 추가됐다. numeric `id`, 이름, ability, managed-reference RID, footprint, category와 runtime archetype은 불변이다.
- 물리 BOM·입력·출력: 변경 `0`. 기존 자원·오염·외부 시설의 물리 생산·소비·회수 계약을 유지한다.
- Direct WU·Embedded WU/EWU: 건설·청소·수리·가동 WU와 embedded EWU 변경 `0`. 식별자 누락 때문에 중단되던 coupling join만 복구한다.
- 시간: 작업 시간, 장애 시간, 생산 주기와 게임 하루 길이 변경 `0`.
- 공간: width/height/layer/anchor/access/외부 배치 규칙 변경 `0`.
- 전력·용수·폐기물: utility ability와 수요·처리량 변경 `0`.
- 위험·실패·복구: content ID 누락·중복·numeric/runtime content ID 불일치는 fail-loud한다. 저장과 런타임 조회는 기존 numeric ID를 유지해 current-format save identity를 바꾸지 않는다.
- 대안·지배 전략: 음수 numeric ID를 경제 stable ID로 재해석하지 않고 두 권위의 역할을 분리한다. 비용·처리량·보상 변화가 없어 지배 전략이나 순환 차익 영향은 없다.
- 실행 경로: `RuntimeBuildingArchetypeAssetBuilder → BuildingSO contentDefinitionId → BuildingDefinitionIdentity → V27 physical-mass coupling ledger`; runtime lookup은 `RuntimeBuildingArchetypeCatalog`의 numeric ID를 계속 사용한다.
- 저장 권위: runtime archetype numeric ID가 기존 저장·조회 권위이고 canonical content ID는 감사·원장용 immutable authored identity다. 신규 save field와 과거 세이브 migration은 없다.
- 자동 감사: builder의 identity-only batch method는 기존 42개 에셋만 허용하며 누락 asset을 생성하지 않는다. runtime catalog는 기대 numeric/content ID 쌍을 exact 검증한다.
- 결정론: 격리 Unity worker 첫 실행 `changed=42`, 두 번째 실행 `changed=0`; 두 번째 실행의 42개 asset SHA-256·length·LastWriteTimeUtc 변화는 모두 `0`이다. 변경 line은 asset별 content ID와 source note뿐이다.
- 공식 scene·에셋 노이즈: GameplayScene, root catalog와 `.meta` 변경 `0`; managed-reference RID churn `0`; 공식 scene SHA 권위를 유지한다.
- 현재 판정: `밸런스 영향 없음 / 구조 교정 검증 진행 중`. worker compile·identity no-op은 PASS했지만 main current-source import와 Batch D coupling/EWU/price parent가 통과하기 전에는 D 또는 전체 밸런스를 완료 처리하지 않는다.

### `wiki:public-prose-positive-boundary` — 2026-09-02

- 정의 ID·종류: `ambition:guard-captain`, `species:adventurer`의 공개 위키 설명 문구 교정이다.
- 정의·카탈로그·실행기 위치: `Assets/Resources/SO/V20/Narrative/Ambitions/ambition_guard-captain.asset`, `Assets/Resources/SO/Character/Species/Species_Adventurer.asset`와 각각의 `V20NarrativeContentAssetBuilder`, `V19LifeContentAssetBuilder`다.
- 등장 시대와 연구: 기존 야망·외부 원정자 종족의 등장 시점과 연구 조건을 그대로 유지한다.
- 플레이어에게 주는 새 결정: 새 선택, 해금, 비용, 보상은 없다. 공개 설명이 두 정의가 다루는 목표와 역할을 직접 말한다.
- 목표 시스템 결합 등급과 근거: 문서 표현만 바꾸는 `0`등급이다. 런타임 명령, 콘텐츠 발생 조건, 도메인 과정과 영수증은 변경하지 않는다.
- 발생 생산자와 실제 원인 증거: 공개 위키 데이터 생성물에 방어적 대조 표현이 남아 있어, 원본 ScriptableObject와 재생성 builder의 동일 설명을 함께 수정했다.
- 실제 도메인 명령·진행·해결 영수증: 없음. 표시용 `description`만 wiki model의 `summary`로 투영된다.
- 영속 흔적과 역반응 소비자: 저장·관계·사건·AI 상태와 그 소비자는 변경하지 않는다.
- 물리 BOM·입력·출력: 변경 없음.
- 직접 작업량과 계산 근거: 변경 없음.
- EWU와 목표 회수 기간: 변경 없음.
- 공간·전력·물·연료·정비: 변경 없음.
- 위험·실패·회복 방식: 변경 없음.
- 사회·비가역 비용: 변경 없음.
- 기존 대안과의 장단점: 문구가 정의의 실제 역할을 직접 설명해, 독자가 존재하지 않는 반론을 먼저 읽지 않게 한다. 콘텐츠 효과와 비용의 장단점은 변경하지 않는다.
- 지배 전략 방지 조건: 변경 없음.
- 저장 권위와 실행 명령: 기존 야망·종족 ScriptableObject와 현재 저장 형식을 유지한다. 새 필드·마이그레이션·실행 명령은 없다.
- 자동 감사 ID와 전수 목록 포함 여부: `Tools/Wiki/validate_wiki_model.py`가 공개 entity, guide, work reference, Astro/TypeScript 템플릿의 `아니다`·`아니라`·`아닌` 표현을 fail-loud 검사한다.
- 검증 매트릭스와 보고서 위치: `npm run build`, `npm run audit`, `npm run check`, 생성된 현재·역사 버전 page의 금지 표현 전수 검색이다.
- 현재 밸런스 상태: `밸런스 영향 없음 / 문서 생성 검증 대기`. ID·수치·능력·연구·행동·저장·경제 수치를 바꾸지 않았다. Unity 재생성·컴파일과 wiki model 재생성 결과가 이 기록의 최종 확인 근거다.

### `balance:v27:m06-natural-clearance-capacity-correction` — 2026-09-03

- 정의 ID·종류: `building:9506`, workstation `m06`, M06 보철조립대의 공용 생산 출력 버퍼 용량 교정이다.
- 등장 시대·연구: 기존 M06 해금과 의료 연구 순서를 유지한다. 새 연구·시설·레시피는 추가하지 않는다.
- 역할·플레이 결과: 보철 arm/leg/eye의 unique 1개 출력이 자연 p95 운반 정리 시간 동안 삭제·바닥 overflow 없이 FacilityBuffer에서 대기할 수 있게 한다.
- Before·After: `defaultBatchCapacity 3→4`, `physicalOutputBufferCycleCapacity 3→4`, 최대 출력 적치 `5,400g→7,200g`이다.
- 물리 BOM·입력·출력: M06 건설 BOM, 보철 레시피 입력, 출력 종류·수량과 각 보철 `1,800g`은 변경하지 않는다. 네 번째 cycle은 새 생산물이 아니라 기존 생산 흐름의 일시 적치 한도다.
- Direct WU·Embedded WU/EWU: 건설·제작 Direct WU, 재료 내재 EWU, 가격과 판매·해체 회수율 변경 `0`이다.
- 계산 근거: p95 `1,814 milli-hours × 3,375g/hour = Ceil 6,123g`; `Ceil(6,123 / 1,800) = 4 cycles`. 3회분 `5,400g`은 `723g` 부족하고 4회분 `7,200g`은 요구를 만족한다.
- 시간: 작업 시간·수술 시간·생산 주기 변경 `0`. fixed scheduling-turn 자연 계측 결과만 사용한다.
- 공간: M06 footprint와 접근칸 변경 `0`. FacilityBuffer는 동일 시설 소유의 질량 admission 권위이므로 던전 셀을 추가 점유하지 않는다.
- 전력·용수·폐기물: 변경 `0`.
- 위험·실패·복구: 작성 용량이 실측 bounded 요구보다 작으면 `PRODUCTION_OUTPUT_CLEARANCE_AUTHORED_CAPACITY_UNDERSIZED`로 fail-loud한다. 4회 초과 raw 요구는 자동 확장하지 않고 `BackpressureExpected`로 유지한다.
- 기존 대안과 장단점: 시설 중복이나 보철 생산량 증가는 없다. 장점은 단일 M06의 정상 일시 backlog 수용이고, 대가는 내부 적치 한도 1,800g 증가뿐이다.
- 지배 전략·악용 방지: 생산 속도, 출력량, 입력 소비, 판매가를 바꾸지 않으므로 무한 생산·구매→판매·제작→해체 차익을 만들지 않는다. buffer에 든 물리는 exact ownership과 kg admission을 계속 사용한다.
- 실행 경로: `M06 BuildingSO → ProductionFacilityOutputCensus → recipe-backed maximum 1,800g → natural p95 profile → capacity gate → FacilityBuffer → AIHaul → kg warehouse`다.
- 저장 권위: capacity는 BuildingSO에서 재파생되며 신규 저장 필드는 없다. 저장된 물리 output lot과 WIP의 current-format identity·수량·질량은 유지한다.
- 자동 감사: M06 builder/validator, Surgery focused, contributor registry, actual M06 PlayMode, profile generation/promotion/strict 및 portable verifier가 `4 cycles / 7,200g`과 Critical 0을 확인해야 한다.
- 결정론: `accepted/backpressureExpected` 결과 개수는 하드코딩하지 않는다. 현재 92행, Critical 0, 두 disposition의 합 92, ordered pressure 행·공식·digest의 generation↔strict exact 동일성과 second-write diff 0을 요구한다.
- 현재 판정: `밸런스 기준 배정 / 메인 Unity 및 새 92×32 시뮬레이션 검증 대기`. 예상 분포는 `74/18/0`이나 fresh 실행 전에는 밸런스 시뮬레이션 검증 또는 H 완료로 보고하지 않는다.

### `narrative:v27:human-form-and-in-game-description-authority` — 2026-09-04

- 정의 ID·종류: 인간의 기원과 발견 순서를 정리하고, 기존 기술 설명과 별도로 플레이 화면 전용 문장을 제공하는 서사·표시 권위다. 신규 아이템·레시피·시설·카드·세력·이정표·효과는 추가하지 않는다.
- 등장 시대와 연구: 기존 콘텐츠의 등장 시점, 연구, 원정 단계, 세력 조건과 이정표 조건을 그대로 유지한다. 원정 카드의 새 문장은 기존 카드가 등장하는 단계 안에서만 보인다.
- 플레이어에게 주는 결정: 기존 카드 선택과 정착 운영 결정을 유지한다. 원정 문장은 식인 소문, 이중 이름, 생존자 단서, 첫 협약 원본, 오원회의 은폐 순서로 정보를 공개한다.
- 물리 BOM·입력·출력: 전 항목 변경 `0`. 아이템 수량, 레시피 입력·출력, 시설 건설 재료, 원정 보급과 카드 결과를 그대로 보존한다.
- 직접 작업량과 계산 근거: 전 항목 변경 `0 WU`. 제작, 건설, 운반, 원정, 전투와 카드 선택의 기존 작업량·시간을 그대로 사용한다.
- 내재 작업량·EWU와 목표 회수 기간: 변경 `0`. 새 문장과 조회 카탈로그는 가격, 가치, 보상, 회수율과 SCC 계산에 참여하지 않는다.
- 시간·공간·전력·물·연료·정비: 전 항목 변경 `0`. 문장 조회는 시설 용량과 기반망 상태를 만들거나 면제하지 않는다.
- 위험·실패·회복 방식: 안정 ID가 빠졌거나 문장이 비었거나 UI 소비처가 기존 `description`으로 되돌아가면 콘텐츠 감사가 fail-loud한다. 조회 실패는 빈 표시와 개발 오류로 남기며 기술 설명을 자동 대체 문장으로 사용하지 않는다.
- 사회·비가역 비용: 기존 관계, 포로, 계약, 원정 부상·사망과 이정표 결과를 그대로 유지한다. 첫 협약은 인간의 몸을 자발적으로 선택하고 원래 종족의 몸으로 돌아갈 권리를 보장했다. 오르데나는 귀환자를 사망자로 기록했고, 일부 하르나크 지휘자는 석방과 식량을 조건으로 귀환을 강요했다. 양쪽의 책임은 기존 포로·세력 사건에서 다룬다.
- 기존 대안과의 장단점: 기존 `description` 한 필드를 모든 화면에서 공유하면 위키의 기술 설명과 플레이 중 필요한 문장이 충돌한다. 별도 불변 카탈로그는 두 권위를 분리하고 모든 소비처·고아를 전수 감사하는 비용을 추가한다.
- 지배 전략·악용 방지: gameplay 수치·결과·확률·저장 상태를 문장 태그로 분기하지 않는다. 서사 갈래 태그는 검색·감사용 읽기 전용 메타데이터이며 보상이나 해금 권위가 아니다.
- 실행 경로: `stable content ID → InGameNarrativeTextCatalog → read-only query → item/food/production/facility/expedition UI`. 아이템 위키는 기술 요약과 별도로 이 카탈로그의 아이템 문장을 그대로 표시하며, 다른 유형의 위키 설명은 기존 일반 설명을 유지한다.
- 아이템 문장 권위: `docs/game-design/content/item-in-game-descriptions.ko.json` 스키마 2가 검수 가능한 한국어 원문과 항목별 `lore_anchor`, `lore_connection`, `story_layer`, `lore_sentence`을 함께 소유한다. `Tools/Documentation/sync_item_narratives.py --sync --check`가 런타임 카탈로그와의 완전 일치를 강제하며 Unity C# 생성기는 수정하지 않는다.
- 저장 권위와 실행 명령: 카탈로그는 불변 ScriptableObject 정의다. 플레이어 진행과 공개 기록 단계는 기존 원정·이정표·세력 저장 권위가 소유하며, 표시 문장을 save에 복제하지 않는다. 과거 세이브 마이그레이션은 없다.
- 자동 감사 ID와 전수 목록: `IN_GAME_NARRATIVE_TEXT_COVERAGE`가 대상 수, 고유 문장 수, 실제 UI 소비처 수와 고아 수를 출력한다. 아이템 1,075개는 누락·중복·빈 문장·고아, 금지된 상투 문구, 장문 대시, 조사 오류, 문장 첫머리 반복과 JSON·런타임 불일치를 실패시킨다. 항목명을 지우고 비교한 세계관 문장 틀은 250개 이상, 한 틀의 반복은 20개 이하를 요구한다. 음식, 생산식, 플레이어 건설 시설·청사진·설치 키트, 원정 카드·선택지와 공개 지도 거점의 기존 전수 검사도 유지한다.
- 검증 매트릭스와 보고서 위치: 대표 아이템·음식·시설·원정 카드의 UI 표시, 카드 선택 결과 불변, 저장 후 재개, 위키 접힘·목차·해시 접근성, Unity 컴파일, 위키 model/authority/build 감사를 요구한다. 결과는 `Artifacts/QA/in-game-narrative-text-coverage.txt`와 Phase 65 진행 기록에 남긴다.
- 현재 판정: `밸런스 영향 없음 / 아이템 세계관 연결 전수 작성·동기화·위키 검증 완료`. 1,075개 전부가 고유 문장과 검수된 세계관 연결을 가지며 일상 물품 1,030개와 서사 단서 45개로 분리된다. 세계관 문장 틀 284개, 최대 반복 19개, 누락·고아·중복·금지된 조기 반전 노출 0개다. 설명은 평균 99.4자, 최대 145자이며 보고서식 핵심 단어의 개별 사용은 13회 이하이다. 수치·BOM·효과·결과·stable ID·save schema가 바뀌면 이 판정을 폐기하고 해당 도메인의 신규 밸런스 검토를 수행한다.

### `narrative:v27:ordena-five-regions` — 2026-09-05

- 정의 ID·종류: 오르데나 왕국을 하나의 거대 국가와 다섯 지역으로 정리하는 세계관·원정 표시 계약이다. 다섯 지역은 구성국이나 자치국이 아니며, 칼리오르 왕관분지, 레메시아 일곱수로평원, 바르카젠 화로산맥, 밀로세아 국경회랑, 사르베니아 백등해안을 각각 기존 인간 전쟁 조직 하나의 본거지로 둔다.
- 등장 시대와 연구: 기존 인간 침입과 원정 해금 순서, 캠페인 단계와 이정표 조건을 바꾸지 않는다. 지역 이름은 세계관 문서와 원정 안내에서 처음부터 확인할 수 있다.
- 플레이 결과: 기존 왕실 원정군, 개척 보급국, 왕립 병기청, 첩보 사냥단, 성광 교단을 서로 다른 국가로 오해하지 않게 한다. 각 조직의 지휘부와 지원망이 원정 목표이며 지역 주민과 도시 전체는 파괴 목표가 아니다.
- 물리 BOM·입력·출력: 변경 없음. 원정 보급품, 전리품, 시설, 장비와 보상 수량을 유지한다.
- 직접 작업량·EWU·시간·공간·기반 비용: 변경 없음. 지명과 설명은 작업량, 이동 시간, 지역 압력, 전투력과 시설 용량 계산에 참여하지 않는다.
- 위험·실패·회복 방식: 기존 다섯 전쟁 계통의 붕괴, 지원 거점 회복과 다른 지역의 계속되는 전쟁 규칙을 유지한다.
- 기존 대안과의 장단점: 국가명만 표시하던 설명보다 각 전선의 목적과 생활권을 구분할 수 있다. 대신 지역명과 전쟁 조직명을 함께 관리해야 하므로 세계관 문서를 단일 설명 권위로 둔다.
- 지배 전략·악용 방지: 수치, 보상, 경로, 적 구성과 저장 상태를 바꾸지 않으므로 새 지배 전략이나 자원 순환은 생기지 않는다.
- 저장 권위와 실행 명령: 기존 인간 전쟁 계통 ID, 원정 지역 상태와 저장 형식을 유지한다. 이번 변경은 공개 문서만 다루며 C#과 ScriptableObject는 수정하지 않는다.
- 자동 감사와 검증: 위키 모델·링크·빌드 검사를 실행한다. 원정 문서는 다섯 지역의 세부 설명을 복제하지 않고 `세계와 정착`을 참조한다.
- 현재 판정: `밸런스 영향 없음 / 문서 검증 완료`. 이름과 세계관 설명만 추가했으며 gameplay 수치와 실행 경로는 변경하지 않았다. 위키 모델·권위·범위·마크다운 감사와 Astro 검사·서버 빌드를 통과했다.

### `captivity:v28:minion-resident-standing-and-rehabilitation` — 2026-09-04

- 정의 ID·종류: 포로를 빠른 통제 노동력인 `하수인` 또는 장기 성장하는 `정식 주민`으로 전환하는 정착 신분·업무·사회·재사회화 규칙이다. 신규 아이템·시설·업무 종류는 추가하지 않는다.
- 등장 시대·연구: 기존 포로 수용, 타락 의식, 설득과 영입 해금 순서를 유지한다. 하수인은 타락 80 이상 및 포획 3일, 정식 영입은 신뢰 70 이상·원한 30 이하·타락 60 미만 및 포획 10일이 필요하다.
- 플레이 결과: 하수인은 정식 영입보다 최소 7일 빨리 노동에 합류하지만 31개 업무 중 23개만 수행하고, 경비는 가능하나 원정·멘토링·연구·접객·의료·포로 관리·공연·대형 사업·위협 대응은 맡지 못한다. 정식 주민은 모든 사회·업무·원정 권한과 임금 계약을 가진다.
- 물리 BOM·입력·출력: 기존 타락 의식 1회 입력 `마나 1 + 생물 재료 1`을 유지하며 3회에 각각 3개를 쓴다. 재사회화 1회는 기존 음식 1개를 소비한다. 새 물리 항목이나 무료 대체 입력을 만들지 않는다.
- Direct WU: 타락 의식은 기존 `42 WU/회`, 3회 `126 WU`다. 재사회화는 `18 WU/일`, 15회 완료 기준 `270 WU`다. 정식 영입을 위한 기존 설득은 `14 WU/회`와 음식 1개를 그대로 쓴다.
- Embedded WU/EWU: 재사회화 음식의 실제 생산·운반·보관 EWU와 타락 의식 재료의 생산·조달 EWU를 기존 물리 항목 권위에서 그대로 읽는다. 하수인의 무임금은 음식·물·수면·침상·의복·치료 비용을 면제하지 않는다.
- 시간: 하수인 전환 최소 3일, 일반 정식 영입 최소 10일, 하수인 재사회화는 하루 1회 및 15회 완료가 필요하다. 저장·재개로 일일 횟수나 최소 날짜를 초기화하지 않는다.
- 공간·전력·용수·연료·정비: 하수인은 정착지 수용량과 일반 생활 공간을 정식 주민과 동일하게 점유한다. 음식·물·침상·의복·치료·시설 접근 요구를 100% 유지한다. 별도 감방·구속구·포로 노동 도구는 하수인 전환 순간 종결한다.
- 숙련·품질: 하수인은 현재 숙련의 작업 속도·품질·사고율을 그대로 적용하고 승인 업무 XP만 50%로 받는다. 멘토·학생이 될 수 없다. 별도 속도 보너스나 품질 감산은 없다.
- 사회 비용: 하수인 비율 `M/(R+M)`을 정식 주민에게 하루 한 번 적용한다. 10% 미만 `0`, 10~24% `-2`, 25~49% `-5`, 50% 이상 `-9`다. 전환 순간 정식 주민 기분 `-6`을 2일 적용하고 출신 세력 원한을 12 올린다.
- 충돌 위험: 하수인 1명당 하루 한 번 `clamp(5 + 20r + 0.1g + 0.3max(0, 50-m), 5, 35)%`를 판정하고, 하루 사건 수를 `ceil(M/4)`로 제한한다. 관계가 -0.2 이하이거나 어느 한쪽 기분이 25 이하면 40%, 그 밖에는 10%가 한 차례씩 주고받는 기존 비무장 몸싸움으로 번진다.
- 통제 이탈: 하수인은 직원 반란에 참가하지 않는다. `안정도=max(타락, 신뢰)-0.25×원한`이 30 미만이거나 기분이 20 미만일 때 `max(0,30-안정도)×0.25% + max(0,20-기분)×0.25%`, 최대 10%를 하루 한 번 판정한다.
- 재사회화: 하루 1회 `18 WU + 음식 1`, 신뢰 `+5`, 원한 `-3`, 타락 `-6`이다. 15회와 신뢰 70 이상·원한 30 이하·타락 30 이하를 모두 충족해야 정식 영입 선택을 연다.
- 기존 대안과 장단점: 일반 포로 노역은 감방·구속구·노동 도구·탈출 위험을 감수하고 제한적 일을 시킨다. 하수인은 그 포로 경로를 끝내고 생활 자원·정착 공간·사회 위험을 부담하는 대신 빠른 23종 노동과 경비를 얻는다. 정식 영입은 10일 이상 걸리고 임금을 내지만 모든 업무·원정·사회 성장과 XP 100%를 제공한다.
- 지배 전략·악용 방지: 하수인 무임금만 비교하지 않고 생활 EWU, XP 50%, 8개 업무 금지, 사회 기분·충돌·통제 이탈, 재사회화 비용과 세력 원한을 함께 계산한다. 120일 256시드에서 하수인 전략 순가치가 정식 영입 전략을 15% 넘게 지배하면 수치를 재검토한다.
- 실행 경로: `포획 상태와 포획일 → 상호작용 수치·정책 → CharacterSettlementStanding 원자 전환 → 업무 허용/임금/숙련/불만/원정 조회 → 일일 사회 판정 → 재사회화 → 정식 주민 전환`이다.
- 저장 권위: 인구 프로필의 enum 신분과 포로 상태의 포획일·재사회화 일수·마지막 실행일·사회 판정일을 저장한다. 기존 `isStaff/isVisiting`과 V3 `Minion`은 복원 후보에서 명시적으로 변환하며, 새 current-format의 누락·모순은 fail-loud한다. 전환은 포로 상태·인구 신분·캐릭터 유형·출입·임금 상태를 한 명령에서 적용하고 실패 시 이전 값으로 되돌린다.
- 자동 감사: `MINION_WORK_MATRIX_31`, `MINION_TRANSITION_BOUNDARIES`, `MINION_DAILY_SOCIAL_DETERMINISM`, `MINION_REHABILITATION_ROUND_TRIP`, `MINION_STRATEGY_256_SEEDS`가 각각 23/8 업무, 3·10·15일 경계, 10·25·50% 기분 경계와 사건 상한, 저장 전후 동일 결과, 30일 조기 합류와 120일 지배 방지를 검사한다.
- 검증 매트릭스와 보고서: 순수 규칙 EditMode, 전환 실패 주입 원자성, 포로 V3 이관 및 새 저장 왕복, 경비/원정 권한, 임금 0·생활 소비 100%·XP 50%·멘토링 차단, UI 표면, 256시드 운영 감사를 요구한다. 결과는 `Artifacts/QA/minion-resident-standing-audit.txt`에 남긴다.
- 현재 판정: `밸런스 영향 있음 / 구현·검증 완료`. `MINION_RESIDENT_STANDING_AUDIT=PASS`가 31개 업무의 23/8 분할, 3·10·15일 경계, 사회 수식, 저장 왕복과 malformed 거부, 전환 상태 snapshot exact rollback, 30일 7일 조기 합류와 120일 256시드 감사를 통과했다. 120일 순가치 비율은 평균 `0.843`, 최댓값 `0.855`(`seed 107`)로 상한 `1.150` 안이다. Unity 클린 컴파일은 2026-09-04 18:16 KST에 오류와 경고 없이 성공했고, 위키 감사와 server build도 성공했다. 보고서는 `Artifacts/QA/minion-resident-standing-audit.txt`에 있다.

### `balance:v27:packaging-output-id-gram-warehouse-closure` — 2026-09-05

- 정의 ID·종류: 415개 물리 아이템의 포장 검토 메타데이터, 355개 레시피의 357개 물리 출력선 identity, 21개 실창고의 gram admission 경계를 닫는 구조 교정이다.
- 등장 시대·연구: 기존 아이템·레시피·창고의 등장 시점과 연구 조건을 유지한다. 신규 콘텐츠나 해금은 없다.
- 역할·플레이 결과: 포장처럼 보이는 51개 단위를 `IntegralUnitNoDetachableTare`로 명시하고, 실제 분리형 포장은 `medicine:anesthetic`의 30g 재사용 의료 바이알 1건만 유지한다. 357개 출력선은 topology-exact ID를 사용하며 창고는 count 용량 우회 없이 gram admission만 허용한다.
- Before·After: 물리 중량, 수량, BOM, 출력량, 소비량, 가격과 WU 변경 `0`. 런타임 `WarehouseInventory`의 `MaxCapacity`, `RemainingCapacity`, `HasCapacityLimit`, `CanStore(count)` 및 count 생성자를 제거하고 표시·일일 보고를 gram으로 투영한다.
- 물리 BOM·입력·출력: 51개 통합 단위에 빈 용기나 포장 폐기물을 새로 만들지 않는다. 마취제의 기존 `120g = 내용물 90g + 바이알 30g`과 `ReusableContainerReturn`을 그대로 유지한다. 출력선 item·amount·probability·role은 현재 승인 의미와 exact 일치한다.
- Direct WU·Embedded WU/EWU: 전 항목 변경 `0`. 포장 검토 메타데이터와 stable ID는 EWU 비용·판매가·SCC potential을 바꾸지 않는다.
- 시간·공간·전력·용수·폐기물: 변경 `0`. 창고 셀·시설 면적·운반 속도·생산 주기·utility 처리량을 유지한다.
- 위험·실패·복구: 분리형 tare와 통합 단위의 모순, 미지정 포장 후보, 중복·비정규 출력선, gram 권위 없는 실창고, admission token 없는 직접 입고는 fail-loud한다. 철거·이전·복원은 모든 실창고의 물리 점유를 검사한다.
- 기존 대안과 장단점: 이름에 병·키트·상자가 있다는 이유만으로 빈 용기를 대량 추가하지 않아 BOM·물류 폭증을 피한다. 대신 향후 detachable package를 추가하면 실제 container item과 반환·폐기·출력 이전 disposition을 함께 작성해야 한다.
- 지배 전략·악용 방지: 질량·가격·입출력량을 바꾸지 않으므로 신규 순환 차익은 없다. count 입고 우회를 제거해 가벼운 스택이 gram 상한을 무시하거나 일부 시스템에서만 창고로 보이는 split-brain을 막는다.
- 실행 경로: `CanonicalItemUnitSemantic packaging review → PackagedLotItemFeature/tare sink`, `ProductionRecipeSO outputs → ProductionOutputLineAuthoring → canonical resolver/commit`, `BuildingStorageAbility maxStoredMassGrams → WarehouseInventory → admission token → physical stored lot`이다.
- 저장 권위: 과거 세이브 migration은 없다. 창고 정책 snapshot V4는 기존처럼 category 정책만 저장하고 질량 용량은 BuildingSO에서 재구성한다. 일일 보고서의 창고 용량 표시는 current-format gram 필드만 사용한다.
- 자동 감사: packaging review는 `52 reviewed / 51 integral / 1 detachable / unresolved 0 / execution orphan 0`, output-line current authority는 `357/357 / duplicate 0 / secondApplyChanges 0`, physical-stock gate는 production count-only member callsite `0`과 21개 positive gram authority를 요구한다.
- 결정론: packaging·output-line 산출물은 동일 입력 두 번 실행 시 byte와 mtime이 변하지 않아야 한다. current output semantic hash는 `35694582a7501d9b11e35f94ff976e5c29945e3b4ba5c050117b68b96fb5d6b6`이다.
- 현재 판정: `밸런스 영향 없음 / 구조 교정 검증 완료`. 메인 Unity 컴파일, explicit semantic, packaging review, canonical output current-authority, physical stock·warehouse admission focused gate와 Console Warning/Error `0/0`을 최종 근거로 사용한다.
