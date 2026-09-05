# 런타임 도달성 감사

정적 코드 소비, Resources 로더, 카탈로그 등록, 안정 ID 리터럴을 교차해 작성 자산의 도달 근거를 분류한다. 실제 플레이 경로와 저장 왕복은 별도의 실행 검증 대상이다.

| 상태 | 항목 | 의미 |
|---|---:|---|
| `catalog-registered-static-consumer` | 3,210 | 카탈로그 등록과 비-Editor 코드 소비가 함께 확인됨 |
| `deprecated-compatibility` | 21 | 호환성 보존 자산 |
| `resources-authored-consumer-unverified` | 184 | Resources 아래 작성 자산이나 구체 소비 경로는 미확인 |
| `stable-id-literal-consumer` | 43 | 비-Editor 코드가 안정 ID를 직접 조회함 |
| `type-consumer-registration-unverified` | 18 | 유형 소비 코드는 있으나 이 자산의 등록은 확인되지 않음 |

세부 근거는 각 유형 CSV의 `catalog_memberships`, `runtime_evidence`, `save_evidence` 열에 기록한다.
