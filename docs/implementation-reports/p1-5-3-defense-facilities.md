# P1 5.3 초기 방어 시설 구현 보고

## 목표

첫 침입 루프에서 침입자를 실제로 막거나 지연시키는 1성 방어 시설을 구현했다.

이번 단계의 핵심은 `방어 시설도 복도 부착물이 아니라 주 시설로 둔다`는 설계를 코드와 데이터로 확정하는 것이다.

## 구현 파일

- `Assets/Scripts/Defense/DefenseFacilitySystem.cs`
- `Assets/Scripts/Defense/Editor/P1DefenseFacilityAssetBuilder.cs`
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`
- `Assets/Scripts/Buildings/SO/BuildingSO.cs`
- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`
- `Assets/Scripts/Character/Ability/AbilityMove.cs`
- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`
- `Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset`
- `Assets/Resources/SO/Building/P1/P1_PoisonPool.asset`
- `Assets/Resources/SO/Building/P1/P1_FireVent.asset`
- `Assets/Resources/SO/Building/P1/P1_LightningPillar.asset`
- `Assets/Resources/SO/Building/P1/P1_IceVent.asset`
- `Assets/Resources/SO/Building/P1/P1_GuardRoom.asset`
- `Assets/Images/Placeholders/Defense/defense_guard_room.png`

## 초기 방어 시설

구현한 1성 방어 시설은 6개다.

```text
1성 가시 함정
1성 독 웅덩이
1성 화염 분사구
1성 번개 기둥
1성 냉기 분사구
1성 경비실
```

모두 `BuildingSO` 에셋이며, 배치 레이어는 `GridLayer.Building`이다.

즉 방어 시설은 복도 위에 별도 레이어로 붙는 장치가 아니라, 방/시설 슬롯을 차지하는 주 시설이다.

## 데이터 구조

`BuildingSO`에 `DefenseFacilityData`를 추가했다.

방어 시설 데이터는 다음 값을 가진다.

```text
enabled
concept
triggerTimings
targetRule
cooldownSeconds
periodicIntervalSeconds
range
star
combatLogText
effects
```

이번 구현에서는 별도 `DefenseFacilitySO`를 만들지 않고, 기존 `BuildingSO` 안에 방어 데이터를 붙였다.

이유:

- 방어 시설도 결국 배치 가능한 건물이다.
- 배치, 파손, 수리, 합성 대상이라는 점은 경영 시설과 같다.
- 별도 SO를 만들면 `BuildingSO + DefenseFacilitySO`의 동기화 문제가 생긴다.
- P1에서는 `BuildingSO` 하나를 보면 배치 정보와 방어 정보를 같이 읽을 수 있는 편이 단순하다.

5.3 최초 구현에서는 효과 자체를 `DefenseEffectData` 배열로 분리했다. 이후 5.4에서 이 효과 배열을 `DefenseEffectSO` 참조 구조로 승격했다.

## 발동 구조

방어 시설 GameObject는 `DefenseFacility` 컴포넌트로 생성된다.

발동 흐름:

```text
침입자 이동
-> GridMoveStep 1칸 완료
-> 도착 칸의 DefenseFacility 검색
-> 발동 조건 확인
-> 쿨타임 확인
-> 효과 적용
-> DefenseFacilityTriggeredEvent 발행
-> 침입자 로그에 발동 요약 기록
```

이 연결을 위해 `AbilityMove.MoveByStep`을 public으로 열었다. 침입자 런타임이 `MoveByPath` 전체를 한 번에 맡기는 대신, 한 스텝씩 이동하고 각 스텝마다 방어 시설을 검사한다.

이동 시스템 자체는 그대로 유지한다.

```text
GridMoveStep
AbilityMove.MoveByStep
IGridMovementHandler
계단/텔레포트/특수 이동 처리
```

방어 시설은 그 위에 후처리로 얹힌다.

## 효과 처리

`DefenseEffectResolver`가 효과를 적용한다.

초기 효과:

```text
Damage
Corrosion
Burn
Charge
Slow
GuardAttack
```

효과별 처리:

```text
Damage:
즉시 피해를 준다.

Corrosion:
부식 상태를 부여한다. 이후 받는 방어 시설 피해가 증가한다.

Burn:
연소 상태를 부여한다. 상태 틱에서 지속 피해를 준다.

Charge:
축전 스택을 쌓는다. 3스택이 되면 방전 피해를 주고 스택을 지운다.

Slow:
감속 상태와 즉시 이동 지연 시간을 준다.

GuardAttack:
경비실의 임시 교전 피해를 준다.
```

초기 상태이상 규칙은 단순하게 시작했다.

```text
부식: 중첩하지 않고 더 강한 값/긴 시간을 유지
연소: 중첩하지 않고 더 강한 값/긴 시간을 유지
감속: 중첩하지 않고 더 강한 지연만 사용
축전: 번개만 스택 누적
```

## 시설별 값

```text
가시 함정:
OnEnter, 피해 14

독 웅덩이:
OnEnter/Periodic 데이터, 피해 6, 부식 0.25, 8초

화염 분사구:
OnEnter/Cooldown 데이터, 피해 18, 연소 2 dps, 5초

번개 기둥:
OnEnter/Cooldown 데이터, 피해 8, 축전 1스택, 3스택 방전 10

냉기 분사구:
OnEnter/Periodic 데이터, 피해 5, 감속 지연 0.7초, 4초

경비실:
OnEnter/GuardResponse 데이터, 경비 교전 피해 10, Guard 작업 지원
```

P1에서는 `OnEnter`를 실제 발동 타이밍으로 사용한다. `Periodic`, `Cooldown`, `GuardResponse`는 데이터에 열어 두었고, 이후 전투 루프가 고도화되면 같은 데이터로 확장한다.

## 파손과 수리

방어 시설은 기존 `BuildableObject`의 파손 상태를 그대로 사용한다.

```text
정상: 발동 가능
파손: 발동 불가
```

`DefenseFacility.CanTrigger`는 파손된 시설을 발동시키지 않는다.

수리는 기존 직원 작업 우선순위에 연결했다.

변경:

- 방어 시설의 `FacilityData.supportedWorkTypes`에 `Repair`를 넣었다.
- 경비실은 `Repair | Guard`를 가진다.
- `AbilityWork`에 `ExecuteRepairWork`를 추가했다.
- 수리 작업이 완료되면 `SetDamaged(false)`로 복구된다.

즉 방어 시설 파손은 별도 예외 시스템이 아니라 기존 직원 작업 루프에 들어간다.

## 경비실 처리

경비실은 장기적으로 직원/몬스터 전투 합류의 기준점이 되어야 한다.

다만 P1에서는 아직 직원 전투 AI가 완성되지 않았으므로, 경비실은 다음 정도만 처리한다.

```text
Guard 작업 지원
requiredWorkers = 1
침입자 진입 시 GuardAttack 피해
```

이렇게 해두면 나중에 `GuardAttack`을 실제 직원 교전 호출 효과로 교체할 수 있다.

## 왜 이렇게 구현했나

### 방어 시설을 BuildingSO로 둔 이유

사용자 설계 방향은 방어 시설과 경영 시설을 완전히 겹치는 레이어 장치로 만들지 않는 쪽이었다.

그래서 방어 시설도 다음 성질을 공유한다.

```text
배치된다.
파손된다.
수리된다.
합성된다.
침입자 경로 위에서 의미를 가진다.
```

이 공통 성질은 `BuildingSO + BuildableObject`가 이미 처리하고 있으므로, 별도 함정 레이어를 만들지 않았다.

### Effect ScriptableObject를 다음 단계로 미룬 이유

5.4 최종 방향은 ScriptableObject Effect 조합이다.

하지만 5.3 단계에서는 초기 6개 시설의 효과 수가 작았다. 그래서 먼저 `DefenseEffectData` 배열로 효과 구조와 발동 흐름을 검증했다.

검증할 수 있는 것:

- 시설별 발동 조건이 데이터로 분리되는가?
- 피해/부식/연소/축전/감속이 서로 다르게 작동하는가?
- 침입자 이동 중 시설 발동을 끼워 넣을 수 있는가?
- 파손/수리와 자연스럽게 연결되는가?

이 흐름이 안정된 뒤 5.4에서 Effect를 ScriptableObject로 분리했다.

## 검증

Unity MCP에서 다음 시나리오를 실행했다.

```text
DungeonStory/Debug/Defense/Run P1 Defense Facility Scenarios
```

검증 항목:

- 방어 시설 에셋 6개가 존재하고 `DefenseFacility` 타입으로 생성된다.
- 모든 방어 시설은 `Building` 레이어의 주 시설이다.
- 방어 시설은 파손 시 발동하지 않는다.
- 방어 시설은 `Repair` 작업 대상으로 잡히고 수리 후 정상 상태가 된다.
- 가시 함정은 진입 시 피해와 발동 이벤트를 만든다.
- 독 웅덩이는 부식을 걸고 이후 피해를 증가시킨다.
- 화염 분사구는 연소 지속 피해를 준다.
- 번개 기둥은 축전 3스택에서 방전 피해를 준다.
- 냉기 분사구는 피해와 이동 지연을 만든다.
- 경비실은 Guard 작업을 지원하고 교전 피해를 준다.

실행 결과:

```text
DefenseFacility=True
```

기존 P1 회귀 검증도 함께 통과했다.

```text
Facility=True
Customer=True
Character=True
Owner=True
WorkPriority=True
Command=True
StaffDuty=True
Operation=True
Feedback=True
Alerts=True
InvasionThreat=True
InvasionIntruder=True
Defense=True
```
