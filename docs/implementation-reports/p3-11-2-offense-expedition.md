# P3 11.2 오펜스 원정 편성 구현 보고

## 구현 범위

이번 단계는 월드맵에서 발견한 대상에 직원을 보내고, 원정이 자동으로 끝나며 결과가 돌아오는 흐름을 구현한다.

구현한 것:

- 원정 가능 인력 필터
- 원정대 편성 API
- 원정 중 캐릭터의 던전 운영 제외
- 자동 원정 결과 처리
- 피로, 기분 저하, 피해, 사망 처리
- 보상 요약 결과 생성
- 원정 편성 패널
- 디버그 시나리오

아직 구현하지 않은 것:

- 재고 실제 입고
- 희귀 시설 실제 지급
- 설계도 실제 지급
- 인간 세력 약화 수치 반영
- 새 손님/직원/포로 실제 추가

이 항목들은 11.3 오펜스 보상에서 연결한다.

## 추가 파일

```text
Assets/Scripts/Offense/OffenseExpeditionSystem.cs
Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs
```

## 원정 가능 인력

원정 가능 여부는 `OffenseExpeditionService.CanJoinExpedition`에서 판단한다.

조건:

- 캐릭터가 존재해야 한다.
- 사장은 보낼 수 없다.
- 사망 상태가 아니어야 한다.
- 이미 원정 중이면 안 된다.
- 현재 생명주기 상태가 `Active`여야 한다.
- `CharacterType.NPC`여야 한다.
- `AbilityWork`를 가지고 있어야 한다.

즉 현재는 직원/방어 몬스터 역할을 할 수 있는 내부 인력만 원정 대상이다.

## 원정 중 상태

`Character.LifecycleState`에 `OnExpedition`을 추가했다.

원정 출발 흐름:

```text
OffenseExpeditionRuntime.TryStartExpedition
-> OffenseExpeditionService.CanJoinExpedition
-> Character.BeginExpedition
-> AbilityWork.PrepareForExpedition
-> LifecycleState.OnExpedition
-> 캐릭터 스프라이트 숨김
```

중요한 판단:

```text
GameObject를 비활성화하지 않는다.
```

이유:

- `SetActive(false)`를 쓰면 `OnDisable`이 호출된다.
- `AbilityWork`의 스케줄 구독 같은 런타임 연결이 끊길 수 있다.
- 그래서 GameObject는 유지하고, `CanRunAi == false`가 되도록 생명주기만 바꾼다.
- 화면에서는 스프라이트만 숨겨 던전에서 빠진 것처럼 보이게 한다.

이렇게 하면 원정 중 캐릭터는 AI 판단, 작업, 방어 대응에서 제외되지만 객체 참조는 안전하게 유지된다.

## 원정 전투력

원정 전투력은 `OffenseExpeditionService.CalculateMemberPower`에서 계산한다.

반영 능력:

```text
공격
힘
맷집
지구력
이동속도
전투력 배율
```

대략적인 식:

```text
원정 전투력 =
공격 * 1.4
+ 힘 * 0.8
+ 맷집 * 0.6
+ 지구력 * 0.4
+ 이동속도 * 0.25
* 캐릭터 전투력 배율
```

원정대 전투력은 멤버 전투력 합산이다.

## 자동 결과 처리

자동 원정은 `OffenseExpeditionRun.RemainingSeconds`가 0이 되면 완료된다.

현재 성공 판정:

```text
원정대 총 전투력 >= 대상 권장 전투력
```

성공 시:

- 원정대 복귀
- 피로 소모
- 약한 피해
- 보상 요약 생성
- `MetaProgressionRuntime.RecordOffenseSuccess` 호출
- `OffenseExpeditionCompletedEvent` 발행
- 이벤트 알림 발행

실패 시:

- 원정대 복귀 또는 사망
- 더 큰 피로/기분 저하
- 더 큰 피해
- 보상 없음
- `OffenseExpeditionCompletedEvent` 발행
- 이벤트 알림 발행

피해는 대상 위험도, 원정대 인원 수, 성공 여부에 따라 계산한다.

## 원정 편성 패널

`OffenseExpeditionPanel`은 런타임 생성 UI다.

구성:

- 상단: 선택된 원정 대상, 필요 인원, 권장 전투력
- 왼쪽: 원정 가능 캐릭터 목록
- 오른쪽: 대상 상세, 선택 인원, 선택 전투력
- 버튼: 원정 출발

이 UI는 최종 UX가 아니라 11.2 시스템 검증용이다.

## 이벤트

추가 이벤트:

- `OffenseExpeditionStartedEvent`
- `OffenseExpeditionCompletedEvent`

이 이벤트는 이후 다음 시스템과 연결된다.

- 운영일 정산의 원정 결과 기록
- 11.3 실제 보상 지급
- 메타 진행의 오펜스 성과
- 침입 이벤트의 인간 세력 약화 보정

## 디버그 시나리오

Unity 메뉴:

```text
DungeonStory/Debug/Offense/Run P3 Expedition Scenarios
```

검증 항목:

- 원정 가능 인력 필터가 사장/손님을 제외하는가
- 원정 출발 시 캐릭터가 `OnExpedition` 상태가 되고 AI에서 빠지는가
- 필요 인력이 부족하면 출발 실패하는가
- 자동 원정 성공 시 보상 요약과 복귀가 발생하는가
- 실패 원정에서 부상/사망이 발생할 수 있는가
- 원정 편성 패널이 생성되는가

## 다음 단계

11.3에서는 `OffenseExpeditionResult.rewardSummaries`를 실제 게임 상태 변화로 연결해야 한다.

우선 연결 대상:

- 창고 재고 입고
- 설계도 해금/획득
- 희귀 시설 획득
- 인간 세력 약화 값 누적
- 직원/손님 후보 추가
- 포로/특수 몬스터 후보 추가
