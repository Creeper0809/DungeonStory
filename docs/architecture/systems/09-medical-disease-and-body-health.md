# 의료, 질병과 신체 상태

## 구현 개요

의료는 단일 체력 수치보다 넓은 상태를 다룬다. 캐릭터와 야생동물의 해부학 노드, 상처와 질병, 치료 주문, 수술 절차, 환자 이송, 적출 부위와 보존 상태가 각각 명시적 상태로 존재한다. 수술 정의는 절차와 효과를 데이터로 구성하고, 효과 handler가 실제 신체 변경을 수행한다.

```mermaid
flowchart LR
    H[신체와 질병 상태] --> P[치료 또는 수술 계획]
    P --> T[환자 이송과 시설 예약]
    T --> M[재료와 도구 예약]
    M --> W[의료 작업]
    W --> E[절차 효과 handler]
    E --> H
    E --> O[적출물과 의료 아이템 outbox]
```

## 수술과 치료 상태

수술 aggregate는 수술 주문, 신체 대상, 단계, 재료와 적출 ledger를 가진다. 환자 이송 runtime은 활성 이송을 ID로 추적하고 실패한 반환을 별도 목록에서 재시도한다. 절차가 완료되면 치료, 노드 제거, 인공 부위 설치, 부담 증가 같은 typed effect handler가 호출된다.

적출 부위와 인공 장기는 월드 아이템과 연결된다. 설치나 폐기 과정에서 물리 스택을 먼저 없애고 신체만 바꾸지 않도록 pending disposition과 receipt를 사용한다.

## 적용된 최적화와 비용 통제

| 구현 | 실제 효과 | 한계 |
|---|---|---|
| 절차 ID catalog | 절차 직접 조회와 중복 검증 | catalog 구축 비용은 초기화 시 지불한다 |
| typed effect handler | 새 효과가 기존 절차 실행문을 모두 바꾸지 않는다 | 새 의미에는 handler 등록과 저장 검토가 필요하다 |
| 환자 이송 ID 사전 | 활성 이송 직접 조회 | 반환 재시도 목록 정리가 필요하다 |
| 연료 및 보존 상태의 느린 refresh | 매 프레임 아이템 검색을 피한다 | 주기 사이에 표시가 늦을 수 있다 |
| 해부학 상태 ID 인덱스 | 개체별 신체 직접 조회 | 노드 내부 연산은 신체 크기에 비례한다 |
| item disposition outbox | 신체 효과와 물리 재료의 중복 및 유실 방지 | 영수증과 복원 검증이 복잡하다 |

## 적용 사례

감염된 팔 절단 수술을 추가한다고 가정한다. 절차 정의는 환자 조건, 수술실, 도구, 작업량과 노드 제거 효과를 조합한다. 실행 중 환자가 이동되거나 도구 예약이 사라지면 주문은 완료되지 않는다. 노드 제거가 성공하면 적출물 생성 workflow가 commit ID로 물리 아이템을 발행한다. 저장 직후 재실행해도 같은 팔이 두 번 생성되지 않는다.

## 비용과 한계

의료 시스템은 신체, 작업, 아이템, 생산과 저장을 넓게 연결한다. 안전성은 높지만 cross-aggregate 검증 코드가 많다. 일부 보존 및 도구 검색은 전체 아이템 스냅샷을 사전으로 다시 만든다. 환자와 부위 수가 늘 때 이 비용을 실제로 측정해야 한다.

## 구현 위치

- `Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs`
- `Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs`
- `Assets/Scripts/Models/Medical/Core/SurgeryAggregateState.cs`
- `Assets/Scripts/Services/Medical/SurgeryRuntime.cs`
- `Assets/Scripts/Services/Medical/SurgicalProcedureEffectHandlers.cs`
- `Assets/Scripts/Services/Medical/SurgicalPatientTransportRuntime.cs`
- `Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs`

