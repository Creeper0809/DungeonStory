# 생산, 제작식과 출력 인계

## 구현 개요

생산은 제작법을 즉시 아이템으로 바꾸는 함수가 아니다. 생산 주문은 재료 선택, 예약, 작업 기여, 품질 판정, 준비된 출력, 목적지 경로와 물리 인계 상태를 보존한다. `ProductionBillRecord`의 단계 전이가 이 순서를 제한하고, 출력이 아이템 권위로 넘어간 뒤에도 영수증과 승인이 끝날 때까지 원본 상태를 유지한다.

```mermaid
flowchart LR
    B[생산 주문] --> S[재료 선택과 예약]
    S --> W[작업 기여]
    W --> Q[품질과 출력 확정]
    Q --> P[Prepared output]
    P --> R[목적지 경로 결정]
    R --> I[Items 물리 발행]
    I --> A[수신 승인]
    A --> C[주문 완료와 정리]
```

## 상태 기계

생산 주문은 대기, 재료 확보, 작업, 품질 해결, 출력 공간 대기, 완료 같은 명시적 단계를 가진다. 선택 재료, 허용 재료와 작업자, 출력 예약, 소비자 경로 정책, 작업자별 기여도와 폐수 성분도 주문 상태에 들어간다. 중간 상태를 저장하기 때문에 로드 후 처음부터 제작을 다시 계산하지 않는다.

준비된 출력은 제작 결과의 확정본이다. 결과를 다시 굴리거나 같은 재료를 두 번 소비하지 않도록 batch commit ID와 세부 출력 정보를 가진다. 물리 아이템 생성과 목적지 인계는 별도 workflow가 처리한다.

## 생산 경계의 질량 회계

생산은 아이템의 단위 질량을 독자적으로 계산하지 않는다. 선택된 입력과 준비된 출력의 ID, 수량, 인스턴스 구성요소를 Items의 공통 질량 query에 전달한다. 주문은 공정 중에 책임지는 입력 질량, 공정 유체와 폐수, 준비 출력의 총질량과 명시된 손실을 상태에 남긴다. 이는 제작 도중 입력 stack이 일반 재고에서 사라져 보이는 구간에도 어느 주문이 그 물질을 책임지는지 밝힌다.

`IProductionOutputMaximumMassRegistry` 계열은 조합식, 시설과 적용 가능한 보정을 기준으로 한 배치가 만들 수 있는 최대 물리 출력량을 투영한다. `ProductionOutputBufferCapacityProjector`는 그 결과와 시설의 실제 버퍼 계약을 연결한다. 최대 출력 투영은 공간 예약의 상한이고, 실제 발행 질량은 품질과 확정 출력으로 다시 계산해 receipt에 고정한다.

```mermaid
flowchart LR
    I[선택 입력과 공정 유체] --> W[WIP 질량 원장]
    W --> R[품질 및 수율 확정]
    R --> O[준비 출력과 명시 손실]
    O --> M[공통 물리 질량 query]
    M --> C[시설 버퍼 capacity 투영]
    C --> T[gram 입고 token]
    T --> P[Items 물리 발행]
    P --> A[생산 acknowledgement]
```

입출력 gram의 차이는 외부의 물과 공기, 생물 성장처럼 선언된 유입, 회수 가치가 있는 물리 부산물, 최종 폐기와 게임상 생략한 공정 손실로 설명한다. 일반 운반과 창고 인계에는 이런 변환이 없으므로 목적지에 맞추기 위해 수량이나 질량을 줄일 수 없다.

이 설계는 질량 감사와 게임 경제를 한 경계에서 다룬다. 레시피 수율을 단순화할 수 있으면서도 취소, 시설 파괴와 저장 재개에서 입력 삭제나 출력 복제를 판정할 수 있다. 단위 질량을 바꿀 때는 출력 buffer, 효능, 반복 작업량, 운반 횟수, EWU와 가격을 함께 검토해야 한다. 전체 조합식의 결합 검증 상태는 중앙 구현 체크리스트가 관리한다.

## 출력 인계

출력 경로는 시설 버퍼, 창고, 소비 시설과 비물리 처분을 구분한다. 각 경로 요청은 목적지 용량, 현재 revision과 capability version을 확인한다. 물리 발행에는 요청, 결과, receipt와 acknowledgement가 남는다. 크래시가 발생해도 재시도는 동일 commit ID와 fingerprint를 비교해 이미 적용된 효과를 다시 만들지 않는다.

## 적용된 최적화와 비용 통제

| 구현 | 실제 효과 | 한계 |
|---|---|---|
| 주문별 단계 상태 | 완료 조건의 반복 추론을 피하고 재개 지점을 보존 | 상태 전이 수가 많아진다 |
| 선택 공급과 출력 예약 사전 | 주문 ID로 직접 조회 | 정리 누락을 막는 검증이 필요하다 |
| prepared output | 품질과 결과의 중복 계산 방지 | 승인 전까지 상태를 오래 보존한다 |
| 목적지 revision | 오래된 용량 판단으로 발행하는 문제 방지 | 경쟁이 많으면 재계획이 늘어난다 |
| batch commit ID와 fingerprint | 중복 발행과 잘못된 재생 감지 | 해시와 영수증 관리 비용이 있다 |
| durable outbox 단계 | 저장과 물리 효과 사이의 크래시 창 복구 | 단일 함수보다 구현이 복잡하다 |
| 체크포인트 GC | 완료된 영수증의 무제한 축적 방지 | durable save 이후에만 안전하게 지울 수 있다 |

## 적용 사례

제련소에서 금속판과 슬래그를 함께 생산한다고 가정한다. 생산 주문은 제품에 들어가는 원광, 공정에서 소비되는 연료와 회수 가능한 용기를 서로 다른 역할로 기록하고 작업 기여를 누적한다. 품질 확정 후 금속판은 창고 경로, 슬래그는 폐기물 처리 경로를 가진 준비 출력이 된다. 각 경로는 자신이 받을 exact gram을 별도로 예약한다. 금속판 창고가 가득 차면 금속판 경로만 대기하거나 재지정할 수 있고, 이미 확정된 품질, 물질수지와 슬래그 결과는 다시 계산하지 않는다.

## 비용과 한계

정합성은 강하지만 생산의 교차 aggregate 프로토콜이 매우 크다. 새 출력 의미를 추가할 때 생산, 아이템, 목적지 용량, 저장 검증과 체크포인트 정리를 함께 검토해야 한다. 다수의 주문이 동시에 경로를 재평가할 때의 CPU와 할당량은 정적 코드로 확정할 수 없다.

## 구현 위치

- `Assets/Scripts/Models/Production/Core/ProductionAggregateState.cs`
- `Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs`
- `Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs`
- `Assets/Scripts/Services/Economy/ProductionOutputDestinationAuthorityRuntime.cs`
- `Assets/Scripts/Models/Economy/Core/ProductionRuntimeContracts.cs`
- `Assets/Scripts/Services/Economy/ProductionOutputBufferCapacityProjector.cs`
- `Assets/Scripts/Services/Economy/ProductionMassExplanationCapabilityRegistry.cs`
- `Assets/Scripts/Services/Items/FacilityBufferMassAdmissionService.cs`
- `Assets/Scripts/Services/Economy/ProductionPreparedOutputRoutingSaveSection.cs`
- `Assets/Scripts/Services/Items/ProductionPhysicalCustodyDrainOutbox.cs`
