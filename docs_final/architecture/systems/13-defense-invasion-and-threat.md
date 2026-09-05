# 방어, 침입과 위협 대응

## 구현 개요

방어 시스템은 방어 시설 상태, 침입 캠페인, 침입자 계획, 교전, 경비 명령과 피해 보고를 분리한다. `InvasionDirectorRuntime`이 침입 단계와 후보를 관리하고, 침입자 실행기는 그리드 경로와 breach planner를 사용해 목표에 접근한다. 교전 runtime은 공격 간격에 따라 전투 계산을 호출한다.

```mermaid
flowchart LR
    T[위협과 캠페인 상태] --> D[InvasionDirector]
    D --> I[침입자 생성과 진입]
    F[방어 시설 snapshot] --> P[목표와 breach 계획]
    I --> P
    P --> G[위험 가중 경로]
    G --> E[교전]
    E --> R[피해와 전투 보고]
```

## 단계와 상태

침입 aggregate는 위협 단계, 후보, 활성 침입, 침입자와 결과를 저장한다. 방어 시설은 상태와 condition을 별도 runtime에서 제공한다. 경비 통제, 원거리 지원과 주인 대피는 director의 상태를 읽고 자신의 실행 책임만 가진다.

침입자 scene 객체는 저장 권위가 아니다. 복원 후보가 검증된 뒤 persistent ID를 기준으로 scene 표현을 다시 만든다. 시설 피해는 건물 구조 내구 권위에 적용된다.

## 경로와 교전

breach planner는 알려진 위험, 파괴 가능한 목표와 그리드 이동 비용을 결합한다. 일반 이동은 공통 path broker를 사용한다. 벽을 파괴해야 하는 경우 접근 경로와 가상 breach 구간을 구분한다. 교전은 각 참가자의 다음 공격 시각을 유지해 공격 가능성을 매 프레임 처음부터 계산하지 않는다.

## 적용된 최적화와 비용 통제

| 구현 | 실제 효과 | 한계 |
|---|---|---|
| director와 execution 분리 | 캠페인 판단과 개별 침입자 틱의 결합 감소 | 상태 동기화 계약이 필요하다 |
| persistent ID 복원 | scene 순서와 무관한 재연결 | 누락된 scene 대상은 명시적으로 실패해야 한다 |
| 공통 path broker 사용 | 캐시와 프레임 경로 예산 재사용 | 침입자 급증 시 일반 AI와 예산 경쟁 가능 |
| 위험 가중 breach plan | 위험과 파괴 비용을 한 경로 비용으로 비교 | 위험 지도 갱신 비용이 있다 |
| 공격 시각 scheduling | 불필요한 공격 계산 감소 | 활성 교전 목록 순회는 남는다 |
| 단계별 aggregate와 report | 중단 및 복원 가능한 침입 진행 | 저장 검증 항목이 많아진다 |

## 적용 사례

공성 골렘을 추가한다고 가정한다. 침입자 정의는 높은 벽 파괴 능력과 느린 이동 비용을 제공한다. planner는 열린 문으로 우회하는 비용과 벽을 파괴하는 비용을 비교한다. 벽을 고르면 접근 셀까지 공통 경로를 사용하고, 도착 뒤 구조 내구 runtime에 피해를 준다. 별도 골렘 전용 맵 그래프를 만들지 않는다.

## 비용과 한계

침입 구현은 director, planner, scene adapter와 전투 runtime에 넓게 퍼져 있다. 다수 침입자가 같은 목표를 평가하면 위험 지도와 후보 계산이 반복될 수 있다. 실제 공격 규모에서 path deferral, director tick과 engagement 비용을 함께 측정해야 한다. 현재 코드만으로 대규모 습격의 안정성을 단정할 수 없다.

## 구현 위치

- `Assets/Scripts/Models/Invasion/Core/InvasionAggregateState.cs`
- `Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs`
- `Assets/Scripts/Services/Invasion/InvasionIntruderExecutionCoordinator.cs`
- `Assets/Scripts/Services/Invasion/DefenseBreachRuntimeAdapter.cs`
- `Assets/Scripts/Services/Invasion/DefenseEngagementRuntime.cs`
- `Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs`
- `Assets/Scripts/Services/Invasion/InvasionSaveSection.cs`

