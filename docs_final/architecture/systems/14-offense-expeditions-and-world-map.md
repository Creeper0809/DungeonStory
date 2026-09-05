# 원정, 월드맵과 전략 전투

## 구현 개요

원정 시스템은 지역과 사이트, 원정대 편성, 보급, 이동, 현장 결정, 전략 전투, 귀환과 보상을 각각 상태로 보존한다. 월드맵 UI는 이 상태의 스냅샷을 표시하고 명령을 보낼 뿐, 이동 결과나 전투 상태의 권위가 아니다.

```mermaid
flowchart LR
    W[지역과 사이트 상태] --> P[원정 준비]
    P --> X[원정 aggregate]
    X --> T[이동과 결정]
    T --> B[전략 전투 director]
    B --> F[현장 의료와 손실]
    F --> R[귀환 안전과 도착]
    R --> G[보상과 월드 상태 변경]
```

## 전략 상태

원정은 참가자 ID, 보급품, 위치, 이동 진행과 현재 조우를 가진다. 전략 전투는 적 intent, 카드 후보, 명령 queue와 resolved command를 저장한다. 플레이어가 명령을 선택한 뒤 결과를 한꺼번에 계산해도 선택 내용과 실행 결과가 분리되어 복원 가능하다.

현장 의료는 부상 안정화와 소모품을 관리하고, 귀환 안전은 남은 안전 이동 예산을 관리한다. 귀환 완료는 scene 캐릭터와 보상 아이템을 되돌리는 별도 workflow다.

## 보급과 전리품의 물리 경계

기지에서 출발하는 보급품은 실제 물건의 ID, 수량, 개별 상태와 인계 operation을 보존해야 한다. 기지 Items 권위에서 빠진 뒤에는 원정 aggregate가 현재 책임을 가지며, 소비하거나 귀환 인계하기 전까지 기지 재고로 다시 사용할 수 없다. 포장 용기와 반환 장비가 있다면 내용물 소비와 별도로 귀환 receipt를 가진다.

현재 원정 상태와 귀환 workflow는 보급, 결과와 보상 발행 단계를 보존하지만, 전리품 일부는 원정 중에 범주별 추상 수량으로 유지되다가 귀환 시 물리 아이템으로 바뀐다. 이 구간에는 canonical kg가 없으므로 원정 중 적재 한도, 보상 kg당 가치와 회수 운반비가 완전히 닫혔다고 볼 수 없다.

목표 계약은 출발 보급과 현장 보상 모두 exact burden을 갖게 하는 것이다. 보상 source는 원정 중 질량과 적재 한도를 기록하고, 귀환 시 같은 총 gram을 물리 lot으로 발행해야 한다. 버리고 올 전리품을 고르는 결정, 운반 장비와 인원 배치, 부상자 구조와 보상 회수 사이의 기회비용이 이 경계에서 생긴다. 이 계약이 구현되기 전까지 원정 kg는 [시스템 구현 권위 체크리스트](../../system-implementation-checklist.md)의 부분 이행으로 유지한다.

원정 참가자는 아이템 화물로 환산하지 않는다. 부상자와 포로는 인물 및 신체 권위를 유지하고 귀환, 구조와 호송 절차가 이동을 관리한다. 들것, 의약품, 식량, 장비와 실제 전리품만 Items 질량 권위를 사용한다.

## 저장과 결정론

`OffenseWorldStateSaveCodec`는 지역, 이동, 결정, battle director, 긴급 완화와 보급 상태를 하나의 versioned payload로 캡처한다. 복원 시 각 목록과 ID를 검증해 후보를 만들고 runtime들이 후보를 발행한다. 명령 queue와 enemy intent가 저장되므로 로드가 플레이어의 선택을 다시 추첨하지 않는다.

## 적용된 최적화와 비용 통제

| 구현 | 실제 효과 | 한계 |
|---|---|---|
| 지역과 사이트 좌표 사전 | 월드 셀 직접 조회 | UI가 사전을 복사해 표시할 수 있다 |
| 명령 queue의 명시적 상태 | 선택과 해결의 재계산 방지 | 저장 payload가 커진다 |
| 이동과 battle director 분리 | 평시 이동에서 전투 규칙을 실행하지 않는다 | 조우 전환 계약이 필요하다 |
| 긴급 완화 0.5초 cadence | 모든 프레임의 시설 평가 방지 | 최대 반 초의 반응 지연 |
| bound facility 인덱스 | 완화 주문과 시설 직접 연결 | 시설 파괴 시 재구축이 필요하다 |
| versioned save codec | 오래된 payload의 모호한 수용 방지 | migration이 없으면 호환 실패가 명시적이다 |

## 적용 사례

독성 늪지 원정을 추가한다고 가정한다. 사이트 정의는 이동 소모와 질병 위험을 제공한다. 원정 준비는 보호 장비와 해독제의 exact 물리량을 고정하고, 이동 state는 매 step의 소모를 기록한다. 현장에서 무거운 유물과 약재를 함께 발견했다면 목표 계약에서는 남은 적재량과 귀환 안전을 비교해 일부를 포기할 수 있어야 한다. 조우가 시작되면 battle director가 적 intent와 명령 후보를 만들며, 현장 의료가 중독 안정화를 처리한다. 월드맵 패널은 이 스냅샷을 읽어 표시하되 아직 구현되지 않은 적재 판정을 독자적으로 계산하지 않는다.

## 비용과 한계

전략 UI 일부가 runtime 파일과 밀접하고, 표시용 목록 생성에 LINQ와 GameObject 생성이 많다. 시뮬레이션 상태는 분리되어 있지만 코드 파일 경계는 완전히 정돈되지 않았다. 대형 월드의 사이트 수와 동시 원정 수에 대한 성능 상한은 측정되지 않았다.

## 구현 위치

- `Assets/Scripts/Services/Offense/OffenseWorldMapRuntime.cs`
- `Assets/Scripts/Services/Offense/OffenseExpeditionRuntime.cs`
- `Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs`
- `Assets/Scripts/Services/Offense/Strategic/OffenseCommandBattleDirector.cs`
- `Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs`
- `Assets/Scripts/Services/Offense/Strategic/OffenseWorldStateSaveCodec.cs`
