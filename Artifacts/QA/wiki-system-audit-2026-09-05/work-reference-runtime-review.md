# 작업 참조 42건과 실제 실행 경로 대조

상태: 42/42 정적 대조 완료. 이 문서는 `work-references.json`의 42개 정성 업무를 런타임 `WorkTypeCatalog`의 31개 stable work type과 억지로 1:1 대응시키지 않고, 실제 기능을 수행하는 단일 또는 합성 실행 경로에 연결한다.

## 판정 기준

- `direct`: 공개 업무가 하나의 stable work type 또는 전용 행동/전투 command로 직접 실행된다.
- `composite`: 공개 업무가 둘 이상의 stable work type·도메인 operation을 묶은 사용자 관점의 상위 개념이다. 전용 work ID가 없다는 이유만으로 구현 누락으로 세지 않는다.
- `gap-linked`: 기능 일부가 기존 WIM과 동일한 근본 원인 때문에 production에서 닫히지 않는다. 중복 WIM을 만들지 않는다.
- 모든 31개 runtime work type에는 emergency flag와 failure profile이 있다. 배정 UI는 `WorkPriorityProfile.Definitions -> WorkTypeCatalog.All`을 사용한다.

## 42건 전수표

| # | 공개 업무 | 실제 권위·실행 경로 | 판정 | WIM |
|---:|---|---|---|---|
| 1 | 운반 | `work:haul` → `AbilityHaul`; exact lease·pickup·delivery | direct | - |
| 2 | 보급 | `work:restock`의 destination lease/commit + 필요 시 `work:haul` | composite | - |
| 3 | 채집 | `work:gather` → `ResourceGatheringWorkExecutionHandler` → world resource output | direct | - |
| 4 | 벌목 | `work:logging` → resource persistent progress/output | direct | - |
| 5 | 채광·채석 | `work:quarry` → resource node 또는 production recipe | direct | - |
| 6 | 급수 | `work:draw-water` + 물리 물 출력/저장·운반; 배관 공급은 별도 network | composite | - |
| 7 | 연료 보급 | `work:refuel` → survival fuel handler + exact item consumption | direct | - |
| 8 | 건설 | `work:construct` → persistent work order/material readiness | direct | - |
| 9 | 수리 | `work:repair` → `RepairWorkExecutionHandler` | direct | - |
| 10 | 배관 | `work:plumbing` → `PlumbingWorkExecutionHandler` | direct | - |
| 11 | 해체 | `work:dismantle` → dismantle work order/material recovery | direct | - |
| 12 | 기반 시설과 대형 건설 | `work:grand-project`와 일반 `construct`의 project/work-order 조합 | composite | - |
| 13 | 일반 제작 | `work:craft` → production bill/WIP/prepared output | direct | - |
| 14 | 의복 제작 | `work:craft` + apparel order UI/runtime + 물리 출력 | composite | WIM-001(환복), WIM-006(자동 품질 상한) |
| 15 | 무기·방어구 제작 | `work:craft` + combat equipment crafting/evolution runtime | composite | WIM-006 |
| 16 | 목공·단조 | recipe/facility/research 조건을 가진 `work:craft` | composite | - |
| 17 | 수선과 개조 | `work:craft`의 equipment reforge + apparel repair/wash order | composite | - |
| 18 | 농업 | `work:sow` + `work:harvest` → crop persistent state/output | composite | WIM-016, WIM-043 |
| 19 | 사냥 | `work:hunt` → `AbilityHunt` → wildlife carcass | direct | - |
| 20 | 도축 | `work:butcher` → carcass service/output | direct | WIM-030(수정딱정벌레 갑각만) |
| 21 | 축산 | `work:animal-care` → husbandry work adapter/runtime | direct | WIM-018(계절·날씨 주기) |
| 22 | 조리 | `work:cook` → production input/WIP/prepared meal output | direct | - |
| 23 | 연구 | `work:research` → blueprint research service/project completion | direct | WIM-015(예상 기간 설명) |
| 24 | 비전 분석 | arcane research projects를 `work:research`로 수행하고 관련 기록/재료를 recipe로 생산 | composite | - |
| 25 | 기록과 검증 | research project + `record:*` 물리 recipe/시설 장비의 조합 | composite | - |
| 26 | 구조 | `work:rescue` → `AbilityRescue` → 안전 위치/치료 경로 | direct | WIM-026(위키 순서 의미) |
| 27 | 진단과 치료 | `work:treat` → survival/medical availability·약품 소비 | direct | WIM-021, WIM-022, WIM-028 |
| 28 | 수술 | `work:surgery` → non-interruptible surgery order/doctor lease | direct | WIM-023 |
| 29 | 접객 | `work:reception` + guest/service runtime | composite | - |
| 30 | 외교 | faction aggregate·계약·지원·통행 domain은 있으나 일반 gameplay 계약 진입/납품 작업이 닫히지 않음 | gap-linked | WIM-032, WIM-033 |
| 31 | 공연 | `work:perform` → circus preparation/performance/supply lifecycle | direct | - |
| 32 | 포로 설득과 관리 | `work:warden` + escort/cell/food/medical captivity runtime | composite | WIM-054, WIM-055 |
| 33 | 근접 공격 | guard/character combat command → melee attack execution | direct | - |
| 34 | 방패 방어 | equipped shield snapshot → block chance/power use/durability in combat | composite | - |
| 35 | 근접 제압 | melee lock·suppression/body health + invasion suppressed resolution | composite | - |
| 36 | 활 공격 | ranged combat command + bow verb/ammunition/reload | direct | - |
| 37 | 석궁 공격 | ranged combat command + crossbow verb/ammunition/reload | direct | - |
| 38 | 화기 공격 | ranged combat command + firearm fire mode/ammunition/reload/smoke rules | direct | - |
| 39 | 원거리 엄호 | `DefenseRangedSupportRuntime`의 position fill/movement/tick + LoS/cover | direct | - |
| 40 | 청소 | `work:clean` → `CleanWorkExecutionHandler`/building cleaning | direct | - |
| 41 | 오염물 처리 | clean, waste-processing production, contaminated item/water/medical policies의 조합 | composite | WIM-046(공통 대기 진단 범위) |
| 42 | 위험 상황 대응 | `work:guard`, `work:rescue`, `work:threat-mitigation`의 emergency-response 정책 | composite | WIM-048(일반 주민 대피) |

## 실행·저장·UI 근거

- stable ID와 31개 카탈로그: `Assets/Scripts/Models/Work/WorkTypeId.cs`, `Assets/Scripts/Models/Work/WorkTypeCatalog.cs`
- 31개 전부의 target invalidation·reservation·checkpoint 계약: `Assets/Scripts/Services/Character/Work/WorkExecutionFailureContracts.cs`
- production DI 등록: `Assets/Scripts/Services/Infrastructure/Registration/DungeonWorkRegistration.cs`
- 공통 선택·배정: `WorkTargetEvaluator`, `WorkPriorityProfile`, `StaffWorkPriorityPanel`
- 건설·해체·보급: `WorkTaskExecutor`, `WorkAmountSystem`
- 제작·연구·생존·자원·축산·수술: 각 `*WorkExecutionHandler` 및 adapter
- 전투 역할: `CharacterCombatCommandRuntime`, `DefenseCombatExecutor`, `DefenseRangedSupportRuntime`
- 포로·공연: `CaptivityWorkExecutionUnityAdapters`, `CircusRuntime`
- 작업 우선순위는 character save의 stable work ID로 복원되며, 장기 작업은 각 도메인 aggregate/save section이 소유한다.

## 결론

- 42개 모두를 실제 기능 경로에 연결했다.
- 31개와 42개의 개수 차이는 11개 구현 누락이 아니다. 사용자 관점에서 합친 업무(농업·보급·응급 대응 등)와 장비별 전투 역할이 별도 공개 항목이기 때문이다.
- 새 독립 WIM은 없다. 외교의 닫히지 않은 production 경로와 일부 도메인 효과 차이는 이미 WIM-001, 006, 015, 016, 018, 021~023, 026, 028, 030, 032~033, 043, 046, 048, 054~055가 근본 원인을 소유한다.
