# 연구, 해금과 런 진행

## 구현 개요

연구는 정의 자산, 대기열, 진행 상태, 시설 요구와 보상 적용을 분리한다. blueprint와 project 정의가 요구 사항과 해금 묶음을 제공하고, runtime은 작업 기여와 완료 상태를 보존한다. 메타 진행은 런 내부 진행과 계정 또는 프로필 해금을 별도 권위로 관리한다.

```mermaid
flowchart LR
    D[연구 정의와 선행 조건] --> Q[연구 queue]
    Q --> F[시설 요구 검사]
    F --> W[연구 작업 기여]
    W --> C[완료 상태]
    C --> U[건물, 제작식, 효과 해금]
    U --> R[런 진행 기록]
    R --> M[런 종료 결과와 메타 보상]
```

## 연구 상태

저장 상태에는 기존 blueprint 작업, 완료 blueprint, 해금 건물과 제작식, 연구 project 진행, 완료 project와 project queue가 들어간다. queue 항목은 프로젝트 ID와 순서를 가진다. 시설 용량 adapter가 필요한 역할과 능력을 검사한다.

보상은 `ResearchUnlockBundleDefinitionSO`의 그룹으로 구성된다. 완료 workflow가 보상 종류에 맞는 handler 또는 application port를 호출하고, 진행 및 결과 event를 발행한다.

## 런과 메타 진행

`MetaProgressionRuntime`은 현재 런의 시작, 종료와 최신 결과를 관리한다. 런 종료 시 환경과 진행 상태를 snapshot으로 캡처하고 result builder가 하나의 결과를 만든다. 프로필 해금은 별도 persistence를 사용해 새 런의 후보 집합에 반영된다.

## 적용된 최적화와 비용 통제

| 구현 | 실제 효과 | 한계 |
|---|---|---|
| 정의 ID 인덱스 | 선행 조건과 복원 참조 직접 조회 | 초기화와 복원 때 인덱스를 만든다 |
| queue와 진행 사전 | 진행 중 프로젝트 직접 갱신 | 순서 변경 시 index 정리가 필요하다 |
| 시설 요구 snapshot | 연구 실행 중 일관된 조건 판단 | 시설 변경 후 다시 검사해야 한다 |
| typed 진행 event | UI와 서사가 연구 runtime을 polling하지 않는다 | 구독 수명주기를 관리해야 한다 |
| 해금 bundle 검증 | 중복 보상 종류와 잘못된 정의를 조기 발견 | 새 보상 의미에는 코드 확장이 필요하다 |
| 런 결과 snapshot | 종료 후 라이브 상태 변화와 결과 분리 | 캡처 시 여러 시스템을 읽는다 |

## 적용 사례

고온 제련 연구를 추가한다고 가정한다. project 정의는 선행 연구와 연구 시설 능력을 요구하고, 보상 묶음은 새 제련소와 제작식을 해금한다. 연구 작업자는 일반 작업 시스템을 통해 기여한다. 완료 후 해금 catalog가 바뀌고 시설 후보 cache가 무효화된다. 메타 보상으로 설정하지 않는 한 다음 런에는 자동으로 남지 않는다.

## 비용과 한계

연구 작업과 해금은 조립식이지만 일부 연구 시설 command는 중앙 fallback switch를 통과한다. 새로운 보상 의미가 계속 늘면 보상 handler registry를 더 엄격히 통일할 필요가 있다. 연구 180개 규모의 대기열과 선행 그래프 UI 성능은 코드 구조만으로 보장할 수 없다.

## 구현 위치

- `Assets/Scripts/Models/Research/Core/ResearchQueueSystem.cs`
- `Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs`
- `Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs`
- `Assets/Scripts/Services/Infrastructure/BlueprintResearchProjectCoordinator.cs`
- `Assets/Scripts/Services/Infrastructure/BlueprintResearchSaveSection.cs`
- `Assets/Scripts/Services/Infrastructure/Core/MetaProgressionRuntime.cs`
- `Assets/Scripts/Services/Meta/MetaRuntimeApplicationAdapter.cs`

