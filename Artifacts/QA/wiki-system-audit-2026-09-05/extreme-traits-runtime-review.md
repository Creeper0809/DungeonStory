# 극단 특성 301·303·304·305·306 런타임 대조

조사일: 2026-09-06  
범위: 공개 특성 도감, 작성 특성/효과 자산, 실제 전투·수술·수확·생산·비전 명령 소비처, 상태 저장 경계  
판정 범위: 정적 원본 대조. Unity Play Mode와 실제 저장 왕복은 실행하지 않았다.

## 결론

- 공개 `trait-301/303/304/305/306` 페이지는 모두 존재하며 희귀도·성향·효과 수·정체성 규칙 수가 작성 자산과 맞는다.
- 그러나 다섯 페이지의 관계는 모두 비어 있고, 요약은 정성 문장 한 줄뿐이다. 임계·확률·지연·배율·물리 비용·실행 UI·결정론 키·상태 전이·저장 권위는 공개 위키에서 찾을 수 없다.
- `GAP-157`은 특성100종 공통 effect/rule payload 투영 공백을 소유한다. 이 보고서의 `GAP-158~162`는 별도 런타임 명령과 물리/상태 생명주기만 소유한다.
- 특성302 금단의 도약은 `GAP-048`, 특성300 극한 제작 영감은 `GAP-106`이 이미 소유하므로 다시 세지 않았다.

## 공개 위키 상태

| 특성 | 공개 요약 | facts | relations | 빠진 실행 정보 |
| --- | --- | ---: | ---: | --- |
| 301 사선 각성 | 죽음 직전 전투력과 긴 탈진 | 4 | 0 | 발동 문턱, 전투당 1회, 통증/위급 페널티 억제, 효과·후유증·저장 |
| 303 기적의 집도 | 치명 수술의 기적/중증 합병증 | 4 | 0 | 중대 수술 판정, 12/18/70%, 수술별 결정론, 결과 강제, 후유증 |
| 304 황금 수확 | 수확 지연 뒤 대수확/손실 | 4 | 0 | 24시간, 12/18/70%, 배율, 담당자 잠금, 물리 commit·복구·저장 |
| 305 한계 돌파 | 긴급 생산의 사고/탈진 | 4 | 0 | 주문 지정, 5초 lease, 작업/사고/피로 배율, 완료·이탈 후유증 |
| 306 마력 과충전 | 생명/장비를 태운 마력 폭주 | 4 | 0 | 마나 문턱, 장착 장비 검사, 체력/내구도 비용, 지속·후유증·저장 |

위키 전체 이름·stable ID 검색에서도 entity/search/navigation/graph 파생물과 `excluded-records.json`, `omitted-relations.json`만 확인됐다. 실행 규칙을 설명하는 공개 가이드는 없었다. 내부 `docs/content-db`는 조건 자산의 존재와 참조 수만 기록하며 비용·위험 수치를 확인할 수 없다고 명시한다.

## 301 사선 각성

- `LastStandRule`: 체력 문턱 `0.20`, 후유증 `2일`.
- `CombatRuntimeStatFactory`가 전투 스탯을 만들 때 현재 체력 비율과 신체 의식 수치를 넘긴다. 핵심 위급 입력은 현재 `body.Consciousness <= 0.20`이다.
- 새 encounter ID를 만나면 `used/active`를 초기화한다. 같은 encounter에서는 한 번만 발동하며, 핵심 위급이 아니면 체력 20% 미만일 때만 발동한다.
- 활성 중에는 통증/위급 체력 효율 페널티를 1로 억제한다. 조건부 자산은 전투력 `×1.5`, 이동 속도 `×1.2`를 적용한다.
- 전투 명령 완료·취소 시 같은 encounter ID의 활성 상태를 끝내고 2일 후유증을 기록한다. 후유증 중 작업 속도는 `×0.5`다.
- encounter ID, 사용 여부, 활성 여부, 후유증 종료 시각은 `CharacterIdentityStateStore`의 규칙 상태 payload로 저장된다.

## 303 기적의 집도

- 적용 대상은 응급 절차이거나 사망 확률 `>=0.15` 또는 성공 확률 `<=0.50`인 중대 수술이다.
- 수술 ID별 한 번만 판정하고 사용한 ID 목록을 정렬 저장한다. `runSeed + "surgery" + surgeryId + actorId + "303"`의 고정 해시로 roll을 만든다.
- roll 구간은 기적 `12%`, 중증 합병증 `18%`, 일반 `70%`다. 모든 대상 시도는 1일 후유증을 기록하고 그동안 작업 속도 `×0.6`이다.
- 기적은 기존 성공 확률보다 앞서 성공을 강제하되 정상 수술 효과와 회복 절차를 그대로 수행한다. 합병증은 일반 성공 난수를 건너뛰고 Major 실패를 강제한다. 일반 결과만 기존 성공 확률과 실패 심각도 난수를 따른다.
- 사용 수술 ID와 후유증 종료 시각은 `CharacterIdentityStateStore`로 저장된다.

## 304 황금 수확

- trait304 작업자를 경작지에 지정하면 24시간 뒤 해소 가능한 예약을 만든다. 밭 ID·시도 순번·행위자·run seed 기반 고정 해시로 대성공 `12%`, 손실 `18%`, 일반 `70%`를 결정한다.
- 대성공은 `state:golden-harvest-jackpot`을 통해 수확량 `×2.5`, 종자 수확량 `×1.5`를 적용한다. 손실은 주/부 산출을 `×0.5`로 만든다.
- UI는 ReadyToHarvest/Harvesting 경작지에서 살아 있는 trait304 후보를 stable ID 순으로 제시하고 “24시간 숙성 후 위험 수확”으로 예약한다. 예약 뒤 해당 persistent ID 작업자만 수확할 수 있다.
- 해소는 황금 수확 판정과 생태 수확을 준비하고 물리 산출 publication까지 만든 뒤 commit한다. 준비 실패 시 양쪽 prepared 상태를 보상 중단한다. 황금 수확 commit은 실제 산출 commit 뒤에 일어나며 acknowledge 전 재진입과 외부 소유 상태를 보존한다.
- 지정 작업자와 시도 순번은 `CropPlotSaveData v3`, 예약/준비/commit 상태는 `CharacterIdentityStateStore`에 저장된다. 작업자 사망 시 미완료 외부 소유 판정은 완료/중단될 때까지 유지된다.

## 305 한계 돌파

- 생산 주문 UI에서 살아 있는 trait305 작업자를 `EmergencyWorkerId`로 지정·해제한다. 일반 생산의 재료·연료·작업량은 줄이지 않는다.
- 정확한 실행 가능 bill을 먼저 검사한 다음 aggregate가 작업 시작을 받아들였을 때 상태를 활성화한다. 사전 검사와 활성화가 어긋나면 소비된 입력만 남는 분기를 막기 위해 예외를 던진다.
- 활성은 고정 장시간 타이머가 아니라 같은 batch ID의 `5초 lease`다. 성공한 작업 틱마다 5초 갱신하고, 사이클 완료·UI 해제 시 종료한다. 버려진 lease는 `ExtremeTraitLeaseClock`이 만료시킨다.
- 활성 중 작업 속도 `×1.5`, 사고 확률 `×1.5`, 피로 증가율 `×2`. 정상 종료·해제·lease 만료 뒤 1일간 작업 속도 `×0.65`다.
- 비상 작업자 지정은 `Production bill v7`, batch ID·활성·lease·후유증은 `CharacterIdentityStateStore`에 저장된다.

## 306 마력 과충전

- 직원 관리 UI가 trait306 보유자와 실제 활성 장착 프로필의 비전 장비 인스턴스를 찾은 뒤 명령을 노출한다.
- 현재 마나 비율이 `0.30` 미만이고 활성/후유증이 없을 때만 발동한다. 활성은 `20초`, 이후 후유증은 `1일`이다.
- 활성화가 성공한 뒤 즉시 최대 체력의 `15%` 피해와 선택한 장비 최대 내구도의 `25%` 손실을 실제 적용한다.
- 활성 중 비전 위력 `×1.6`; 후유증 중 마나 회복 `×0.5`다. UI 문구도 이 문턱·지속·비용·후유증을 정확히 안내한다.
- 활성 ID·활성/후유증 종료 시각은 `CharacterIdentityStateStore`, 체력은 body-health 저장, 장비 손상은 equipment 저장 경계가 소유한다.

## 저장과 검증 경계

- `CharacterIdentityStateStore`는 `(characterId, traitDefinitionId, ruleId)`의 escape된 키와 revision/payload를 저장하고 character별 안정 정렬로 capture한다. `CharacterNarrativeRuntime.identityStates`가 이를 capture/restore한다.
- 사망 정리는 기본적으로 인물의 규칙 상태를 제거하지만, 황금 수확은 외부 경작지 트랜잭션이 끝날 때까지 상태를 유지하는 retention policy가 있다.
- 작성 연결성 시나리오와 focused audit가 소비자·저장 경계를 검사하지만, 이번 위키 감사에서는 Unity 시나리오를 재실행하지 않았다.

## 직접 확인한 원본

- `Assets/Resources/SO/V26/Traits/Founder/Trait_301_last-stand.asset`
- `Assets/Resources/SO/V26/Traits/Founder/Trait_303_miracle-surgery.asset`
- `Assets/Resources/SO/V26/Traits/Founder/Trait_304_golden-harvest.asset`
- `Assets/Resources/SO/V26/Traits/Founder/Trait_305_production-limit-break.asset`
- `Assets/Resources/SO/V26/Traits/Founder/Trait_306_arcane-overcharge.asset`
- `Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs`
- `Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityRuntime.cs`
- `Assets/Scripts/Services/Character/Identity/Runtime/ArcaneOverchargeCommandRuntime.cs`
- `Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs`
- `Assets/Scripts/Services/Combat/CombatRuntimeStatFactory.cs`
- `Assets/Scripts/Services/Combat/CharacterCombatCommandRuntime.cs`
- `Assets/Scripts/Services/Medical/SurgeryRuntime.cs`
- `Assets/Scripts/Services/Economy/CropPlotRuntime.cs`
- `Assets/Scripts/Services/Economy/ProductionBillSceneFacade.cs`
- `Assets/Scripts/Views/Buildings/UI/CropPlotBuildingPanelPresenter.cs`
- `Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs`
- `Assets/Scripts/Views/Character/UI/StaffManagementSurfacePanel.cs`
- `wiki/game-versions/0.0.1v/data/entities/character/trait-301.json`
- `wiki/game-versions/0.0.1v/data/entities/character/trait-303.json`
- `wiki/game-versions/0.0.1v/data/entities/character/trait-304.json`
- `wiki/game-versions/0.0.1v/data/entities/character/trait-305.json`
- `wiki/game-versions/0.0.1v/data/entities/character/trait-306.json`

