# P1 5.2 침입자 1종 구현 보고

## 목표

첫 침입 루프에 사용할 가장 단순한 침입자 1종을 구현했다.

이번 단계의 침입자는 `사장방 돌파형`이다. 다만 현재 설계에서 별도 사장방은 실제 룸 오브젝트가 아니라 설정상의 목표에 가깝기 때문에, 구현 목표는 `사장 캐릭터의 현재 위치`로 둔다.

즉 침입자가 던전에 들어와 사장 캐릭터와 같은 칸에 도달하면 최종 교전으로 넘어가고, 그 피해로 사장이 사망하면 런이 끝난다.

## 구현 파일

- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`
- `Assets/Scripts/Grid/Core/Grid.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset`
- `Assets/Images/Placeholders/Characters/character_intruder.png`

## 침입자 데이터

`Intruder_Breakthrough` 캐릭터 SO를 추가했다.

기본 값:

```text
타입: Intruder
역할: Regular
이름: 돌파형 침입자
스프라이트: character_intruder.png
```

P1에서는 침입자도 기존 `Character` 모델을 사용한다. 그래야 이후 침입자 종류를 늘릴 때 체력, 공격, 이동속도, 상태, 로그, 피해 처리 같은 공통 기능을 재사용할 수 있다.

## 이벤트 연결

5.1의 `InvasionThreatRuntime`은 위협도가 100%를 넘고 지연 시간이 끝나면 `InvasionCandidateEvent`를 발행한다.

5.2에서는 `InvasionDirectorRuntime`이 이 이벤트를 구독한다.

흐름:

```text
InvasionCandidateEvent
-> InvasionDirectorRuntime
-> 침입자 데이터 로드
-> 입구 위치 확인
-> 침입자 GameObject 생성
-> InvasionIntruderRuntime.Begin
-> InvasionStartedEvent
-> InvasionSpawnedEvent
```

`GameManager.EnsureOperatingDaySystems()`에서 `InvasionDirectorRuntime`을 자동으로 붙인다. 그래서 별도 씬 오브젝트를 수동으로 만들지 않아도 위협도 시스템과 침입자 생성이 이어진다.

## 입장 처리

침입자는 바로 던전 내부에 생성되지 않는다.

먼저 `CharacterSpawner`가 제공하는 외부 스폰 위치에서 시작한다. 이후 입구 문 위치까지 걸어오고, 마지막으로 입구 그리드 칸으로 들어간다.

`CharacterSpawner`가 없으면 그리드에서 가장 가까운 이동 가능 칸을 찾아 임시 입구로 사용한다.

이렇게 한 이유:

- 손님 입장 연출과 같은 감각을 침입자에도 재사용할 수 있다.
- 침입자가 갑자기 던전 안에 튀어나오는 느낌을 피한다.
- 나중에 침입자별 시작 지점, 매복, 특수 입구를 붙일 수 있다.

## 이동 로직

침입자는 기존 그리드 이동 체계를 사용한다.

사용 경로:

```text
Grid.SearchPath
GridPathSearchResult.GetReachablePositions
Grid.GetMovePath
AbilityMove.MoveByPath
GridMoveStep
```

따라서 복도, 계단, 특수 이동 시설은 기존 캐릭터 이동과 같은 기준으로 처리된다.

5.2에서 새로 추가한 것은 `GridPathSearchResult.GetReachablePositions()`다. 기존에는 방문 가능한 시설 목록 중심 API는 있었지만, 침입자 탐색에는 시설이 아니라 `도달 가능한 칸 전체`가 필요했다.

## 탐색성과 목표 보정

침입자가 처음부터 완벽하게 사장에게 직행하면 던전 구조를 시험하는 맛이 약해진다. 반대로 너무 오래 헤매면 침입 이벤트 템포가 죽는다.

그래서 침입자에는 `Focus` 값을 둔다.

```text
Focus = 침입 후 경과 시간 / secondsToFullFocus
```

동작:

```text
Focus 낮음:
도달 가능한 칸 중 탐색 목적지를 고른다.
사장 위치는 초반 후보에서 낮게 보거나 제외한다.

Focus 상승:
사장 위치에 가까운 칸일수록 선택 점수가 높아진다.

Focus 0.95 이상:
사장 캐릭터 위치로 직접 경로를 잡는다.
```

탐색 목적지는 랜덤성만 쓰지 않는다. 랜덤 탐색 점수와 사장 위치에 가까운 정도를 `Focus`로 섞는다.

이렇게 하면 초반에는 던전을 둘러보는 듯 움직이고, 시간이 지나면 점점 사장 위치를 찾아내는 느낌이 난다.

## 시설 파손 보조 목표

첫 버전 침입자에게는 보조 목표를 하나만 넣었다.

```text
주 목표: 사장 캐릭터 위치 도달
보조 목표: 근처 시설 파손
```

침입자는 일정 간격마다 현재 칸과 좌우 인접 칸을 확인한다. 파손 가능하고 아직 파손되지 않은 비이동 시설을 발견하면 `SetDamaged(true)`를 호출한다.

동시에 다음 이벤트를 발행한다.

```text
InvasionFacilityDamagedEvent
```

아직 방어 시설 발동 로그와 침입 결과 상세 패널은 5.3 이후 범위다. 5.2에서는 `시설이 실제로 파손될 수 있다`는 연결만 먼저 만든다.

## 최종 교전

침입자가 사장 캐릭터와 같은 그리드 칸에 도달하면 최종 교전에 들어간다.

흐름:

```text
침입자 위치 == 사장 위치
-> InvasionFinalCombatStartedEvent
-> 사장에게 finalCombatDamage 적용
-> 사장 사망 여부 확인
-> InvasionResolvedEvent
-> 침입자 종료
```

사장이 사망하면 기존 `Character.Die`와 `OwnerRunManager.HandleOwnerDeath` 흐름을 그대로 탄다. 즉 5.2 침입자 시스템이 런 종료를 별도로 중복 구현하지 않는다.

## 왜 이렇게 구현했나

### AI Brain 대신 전용 런타임을 쓴 이유

침입자는 손님이나 직원처럼 긴 일상 행동 후보를 평가하지 않는다.

P1 침입자에게 필요한 것은 다음뿐이다.

- 입구에서 들어온다.
- 사장 위치를 향해 점점 정확하게 이동한다.
- 이동 중 일부 시설을 파손한다.
- 사장에게 닿으면 최종 교전을 한다.

그래서 `AIBrain` 액션 트리로 억지로 끼우기보다, 침입 이벤트 전용 런타임으로 작게 만들었다.

다만 이동 자체는 기존 `GridMoveStep`과 `AbilityMove`를 사용한다. 즉 AI 판단은 전용이지만, 이동 시스템은 공유한다.

### 사장방 오브젝트를 만들지 않은 이유

현재 설계상 게임 오버 조건은 `사장방이 뚫림`이 아니라 `사장 캐릭터 사망`이다.

따라서 P1에서는 방 오브젝트를 별도로 만들지 않고 사장 캐릭터 위치를 목표로 삼는다. 나중에 사장방/왕좌/핵심 시설을 추가하더라도, 최종 판정은 사장 캐릭터 생존으로 유지할 수 있다.

### 시설 파손을 단순하게 둔 이유

침입자 유형, 약탈, 몬스터 처치, 시설 파괴 우선순위까지 한 번에 만들면 전투 피드백과 방어 시설보다 침입자 AI가 먼저 복잡해진다.

그래서 5.2에서는 `근처 시설 파손`만 넣었다. 이 정도면 침입자가 사장에게 닿지 못해도 던전에 피해를 남기는 기본 감각을 확인할 수 있다.

## 검증

Unity MCP에서 다음 시나리오를 실행했다.

```text
DungeonStory/Debug/Invasion/Run P1 Intruder Scenarios
```

검증 항목:

- 침입자 SO가 `Intruder` 타입이고 placeholder sprite를 가진다.
- 침입 초기에는 사장 위치로 바로 직행하지 않고 탐색 목적지를 고른다.
- 집중도가 충분히 오르면 사장 위치까지 직접 경로를 잡는다.
- 침입자가 주변 시설을 파손하고 `InvasionFacilityDamagedEvent`를 발행한다.
- 최종 교전 피해로 사장이 사망하면 `OwnerRunManager`의 런 종료 상태로 이어진다.

실행 결과:

```text
InvasionIntruder=True
```

