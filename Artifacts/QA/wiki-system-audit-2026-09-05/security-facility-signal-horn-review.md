# 보안 시설·경계 신호 나팔 런타임 대조

조사일: 2026-09-06  
범위: active `BuildingSecurityAbility` 시설6개, Guard 완료 상태, 침입 신호 나팔 durable-equipment 경로, 공개 facility/item 엔트리와 방어 가이드

## 결론

- 보안 시설은 Guard 업무 후보이자 `FacilityRole.Security` capability다. 침입 시작 시 첫 operational 보안 시설이 신호소로 선택된다.
- 신호소에 물리 `tool:watch-signal-horn` 1개가 공급되어 있으면 침입 집결시간이6초 늘고, 침입 publish가 성공할 때 내구도1을 원자적으로 소모한다. 나팔 최대 내구도는120이다.
- 공개 보안 시설6개는 “치안과 보안” 역할과 건설비만, 나팔 페이지는 무게2.35kg·한칸1·가격65만 표시한다. 시설 조건·집결+6초·내구도1 소비를 설명하지 않아 GAP-169로 등록한다.
- 나팔의 공개 `in_game_description`은 침입과 화재를 알린다고 쓰지만 비Editor consumer는 `runtime:invasion-watch-signal` 하나뿐이고 화재 신호 소비자는 검색되지 않았다. 침입 기능은 존재하므로 부분 구현 차이 DIFF-046/WIM-061로 등록한다.
- 별도 `alarmCharges`는 Guard 완료마다1씩 최대3까지 증가하고 facility state로 저장·복원되지만 production 차감·효과 consumer가 없다. 위키가 이 충전을 방어 효과로 약속하지는 않으므로 DIFF로 중복 계수하지 않고 구현 고아 상태로 기록한다.

## active 보안 시설6개

| 자산 | building ID | 공개 제목 | max alarm charges | Guard 완료당 |
| --- | ---: | --- | ---: | ---: |
| `G01_경비초소책상.asset` | 1044 | 경비초소책상 | 3 | +1 |
| `G02_경보종.asset` | 1045 | 경보종 | 3 | +1 |
| `G03_순찰상황판.asset` | 1046 | 순찰상황판 | 3 | +1 |
| `G04_전술지도탁자.asset` | 1047 | 전술지도탁자 | 3 | +1 |
| `G05_전투깃발.asset` | 1048 | 전투깃발 | 3 | +1 |
| `G06_전리품거치대.asset` | 1049 | 전리품거치대 | 3 | +1 |

모두 `deprecatedCompatibilityAsset: 0`이다. public page는 facts2(분류·크기), relations0이고 summary의 고유 기능 표기는 역할명뿐이다.

## 침입 신호 나팔 실행 계약

1. `InvasionDirectorRuntime`이 `FindOperational(FacilityCapabilityKind.Security).FirstOrDefault()`로 신호소를 고른다.
2. `TryEnsureReady`가 시설 persistent instance/position에 durable-equipment assignment를 reconcile하고 `tool:watch-signal-horn` 1개의 공급 준비를 확인한다.
3. 준비 성공 시 침입의 `rallyDurationSeconds`에6초를 더한다.
4. 실제 spawn publish는 `TryCommitRally` 안에서 수행되고, 성공한 경우에만 나팔 내구도1과 효과가 함께 commit된다. 실패/예외 시 공통 durable-equipment 원자성 계약이 wear를 보존한다.
5. 나팔 정의는 무게2.35kg·최대 stack1이고 공통 durable tool 권위에서 최대 내구도120이다. 공개 기준 가격은65다.

## charge 상태와 실제 효능을 구분할 것

`BuildingSecurityAbility`는 `IBuildingWorkCompletionAbility`와 `IBuildingRuntimeStateAbility`다. Guard가 완료되면 `BuildingSecurityStateModule.AddAlarmCharges(1, 3)`가 실행되고 활동 기록에 현재 charge가 남는다. state module은 JSON capture/restore를 제공한다.

그러나 비Editor production 검색에서 `AlarmCharges`를 읽는 곳은 이 활동 기록과 진단 getter뿐이며, 차감하거나 침입·범죄·settlement alert에 전달하는 consumer는 없다. 신호 나팔 집결 보너스는 charge가 아니라 operational Security role과 물리 durable slot만 본다. 현재 문서화에서는 charge를 방어 보너스로 표현하면 안 된다.

## 공개 설명 차이

`tool-watch-signal-horn.json`의 `in_game_description`은 “침입과 화재 같은 위험”을 알린다고 설명한다. 현재 비Editor exact 참조는 다음뿐이다.

- `InvasionSignalHornDurableEquipmentRuntime`
- `DurableToolItemRules.WatchSignalHorn`의 내구도120
- `PhysicalItemRuntimeConsumerCatalog`의 `runtime:invasion-watch-signal`

fire↔signal/horn 교차 참조는 공개 설명 문장 외0건이었다. 따라서 침입 기능 전체 부재가 아니라 화재 경보 절반의 production 연결 부재로 한정한다.

## 근거

- `Assets/Resources/SO/Building/Modular/G01_경비초소책상.asset` ~ `G06_전리품거치대.asset`
- `Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs`
- `Assets/Scripts/Services/Buildings/BuildingStateModule.cs`
- `Assets/Scripts/Services/Buildings/ModularFacilityRuntimeEffects.cs`
- `Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs`
- `Assets/Scripts/Services/Invasion/InvasionSignalHornDurableEquipmentRuntime.cs`
- `Assets/Scripts/Models/Items/Core/ItemPrimitives.cs`
- `Assets/Scripts/Models/Items/Core/PhysicalItemRuntimeConsumerCatalog.cs`
- `Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I108_경계_신호_나팔.asset`
- `wiki/game-versions/0.0.1v/data/entities/facility/building-1044.json` ~ `building-1049.json`
- `wiki/game-versions/0.0.1v/data/entities/item/tool-watch-signal-horn.json`
- `wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md`

## 동적 증거 경계

이번 절편은 읽기 전용 정적 감사다. Unity Play Mode에서 나팔 공급→침입 commit→내구도 감소, 실패 rollback, 화재 발생 시 무반응을 재현하지 않았다. 구현 후에는 이 세 경로와 save→restore→resave를 focused regression으로 검증해야 한다.
