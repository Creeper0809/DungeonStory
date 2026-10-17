# Phase80 Social/Life ledger slice review

판정은 `BLOCKED_FROZEN`이다. 23개 인벤토리 후보 중 3개는 두 개의 새 상위 결과로 정확히 통합됐고, 나머지 20개는 사후 observer 구독을 원자 기록으로 위장하지 않기 위해 미해결로 남겼다.

구현된 경로는 다음 두 개다.

- `SocialConflictEvent`와 조건부 `SocialConflictCommittedReceiptEvent`는 `SocialConflictOutcomeReceipt` 하나로 통합된다. 관계 기억과 기분 변경 전에 정확한 원장 공간을 예약하며, 실패 시 identity/mood snapshot을 복원한다.
- `ApologyEvent`는 `ApologyOutcomeReceipt`로 통합된다. 용서 및 기분 변경 전 예약, 실패 롤백, 성공 후 outbox 전달/ack 흐름을 사용한다.

중복 result key는 예약 단계에서 도메인 효과를 다시 적용하지 않는다. 저장된 최종 receipt를 가진 복구 경로만 canonical payload reconciliation을 수행할 수 있고, `PublishedAcknowledged`까지 도달해야 성공이다. 최초 commit 뒤 delivery/ack가 실패하면 원장 committed outbox가 retry owner이므로 도메인 상태를 되돌리지 않는다.

두 descriptor는 direct subject만 유지하고 optional witness를 만들지 않는다. importance는 descriptor-owned policy에서 novelty, magnitude/relationship, causation, repetition, age를 계산한다. provenance는 retention anchor로 사용하지 않는다. 구체 compacted projector/reference 계약이 생기기 전까지 compaction은 꺼져 있다.

남은 20개는 `closure.json`에 각 blocker를 기록했다. 특히 이미 상태를 바꾼 뒤 발행되는 이벤트를 observer로 받아 적는 방식은 채택하지 않았다. Combat 경로는 기존 `attackOperationId`를 `SocialConflictEvent`에 전달하는 다른 slice 수정이 필요하다.

Unity 실행, Unity CLI, MCP, 플레이모드 테스트는 이 slice에서 수행하지 않았다. 최종 검증은 main agent의 Unity 단계가 담당한다.
