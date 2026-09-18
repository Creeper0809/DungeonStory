# Phase80 거래·재고·재정·서비스·손님 원자성 증거

## 현재 판정

초기 Receipt/Event 22개와 provisional r2가 추가한 `Result/Outcome/Resolution` 17개, 총 39개 타입을 추적한다. `PhysicalItemBatchDispositionReceipt`와 `PhysicalItemRelocationReceipt`는 생산 코드 연결까지 구현됐고, `PhysicalItemDispositionReceipt`는 같은 operation의 batch receipt가 모든 사실을 보존하는 exact substitute로 전환됐으며 세 건 모두 Unity 검증을 기다린다. 8개는 명시적 NonResult category와 producer/consumer source proof를 확보했지만 final-frozen hash를 기다린다. 나머지 28개는 실제 owner 연결이 없어 차단 상태다. observer 캡처나 손실 해시는 완료로 인정하지 않았다.

## PhysicalItemDispositionReceipt exact substitute

구형 단일-stack extension은 더 이상 `TryConsumeStackQuantity`로 물리 상태를 먼저 변경하지 않는다. 같은 disposition kind, operation ID, reason code, stack ID와 quantity를 `TryCommitBatchPhysicalDisposition`에 전달한다. batch owner는 mutation 전에 exact definition/instance ref, quantity/mass, source position, immutable display/pronunciation snapshot과 owner revision을 준비하고, 저장형 pending row와 canonical gameplay-outcome attachment를 함께 관리한다. 호환 receipt의 `Consumed` snapshot은 mutation 전 detached source snapshot을 요청 수량으로 고정한 값이며 권위나 별도 outbox가 아니다.

## PhysicalItemBatchDispositionReceipt

### mutation 전 준비

서비스는 정확한 source stack을 재검증하고 source별 definition/instance ID, quantity, gram mass, position, immutable Korean display/pronunciation snapshot을 동결한다. 예상 `WorldItemRepository.ItemStackVersion`을 계산한 뒤 typed participant의 `TryPrepare`를 호출한다. prepare가 실패하면 physical mutation은 시작하지 않는다.

### 공동 pending과 커밋 지점

physical pending row에는 request fingerprint, expected owner revision, expected `GameplayResultKey`, exact source facts를 mutation 전에 저장한다. source debit 후 실제 owner revision이 예상 revision과 일치할 때만 prepared outcome을 commit한다.

ledger commit 전 실패는 source/lease/pending을 rollback한다. ledger commit 이후에는 delivery, acknowledgement, diagnostics 또는 attachment 저장이 실패해도 physical state를 rollback하지 않는다. expected key가 남은 pending row가 재조정 책임을 가진다. 이 phase 구분은 결과 종류별 switch 없이 generic participant contract로 구현됐다.

### replay와 저장

동일 operation과 동일 fingerprint replay는 저장된 exact receipt와 result key를 사용한다. 다른 payload는 conflict다. attachment가 빠진 commit→save 구간은 복원 후 diagnostics의 exact `ResultKey`, `OutcomeId`, canonical lowercase SHA-256으로 attachment를 재구성한다.

pending row 제거는 identity가 `PublishedAcknowledged`, `Compacted`, `Forgotten` 중 하나이고 key/outcome/hash가 정확히 같을 때만 가능하다. terminal tombstone은 acknowledgement 이후에만 생기므로 ack 직후 attachment 저장 전에 consolidation된 경우도 복구한다. `Committed`, `DeliveryFaultPending`, `PublishedAwaitingAcknowledgement`에서는 제거하지 않는다.

### 사실 계약

| 범주 | 보존 값 |
|---|---|
| identity | kind, operation, reason, request fingerprint, commit ID |
| owner | expected repository revision |
| item | source stack ID, definition ID, optional instance ID |
| amount | per-source/total quantity, per-source/total gram mass |
| location | source x/y |
| presentation | immutable display text, snapshot revision, pronunciation mode/value/final consonant/revision, locale |
| outcome join | producer, operation, commit revision, local index, outcome run/sequence, lifecycle, lowercase SHA-256 |

## 공유 물리 seam

`IOutcomeAwarePhysicalItemBatchDispositionService`와 `IOutcomeAwareReservedPhysicalItemBatchDispositionService`는 environment 등 다른 domain이 일반 또는 lease-backed source에 자체 typed participant를 공급할 수 있다. item core는 result type을 알지 않는다. `IPreparedPhysicalItemRelocationService`는 exact destination identity를 mutation 전에 preview하고 성공 뒤 동기 rollback handle을 반환한다.

`PhysicalItemRelocationService`는 preview 뒤 typed outcome을 준비하고 reversible mutation을 적용한 다음, bounded physical relocation journal과 canonical ledger 결과를 공동 커밋한다. journal은 operation/reason, source/destination stack, definition/instance, quantity/mass, source/destination cell, destination state/owner, immutable display/pronunciation snapshot, owner revision, expected result key와 canonical attachment를 보존한다. ledger 전 실패는 physical mutation과 journal을 함께 rollback하고 ledger 후 실패는 절대 physical mutation을 되돌리지 않는다. 동일 operation replay는 모든 요청 인자의 lowercase SHA-256 fingerprint를 비교하고 원 영수증을 반환하며 다른 payload는 충돌한다. journal은 16,384행에서 자동 삭제 대신 fail-closed한다.

`IPreparedFacilityBufferOutputPublicationService`는 repository mutation 전에 결정적 stack/instance ID, definition ref, quantity/mass와 immutable Korean display/pronunciation snapshot을 확정한다. physical publication과 mass admission은 각각 reversible handle을 제공한다. 상위 `IOutcomeAwareProductionDomainOutputPublicationService`는 expected result key/revision/snapshots/attachment를 owner save row에 보존하고 canonical ledger commit 전 실패만 두 physical authority에서 rollback한다. ledger commit 뒤에는 pending을 유지하며, exact identity가 `PublishedAcknowledged` 또는 동일 key/outcome/hash의 `Compacted`/`Forgotten` tombstone이 된 뒤에만 physical output을 acknowledge한다.

## 선언형 결과 계약

초기 22개 결과 vocabulary는 `TradeInventoryOutcomeDefinition` row가 required roles/metrics/facts, narrative subject, role별 perspective frame을 소유한다. 중앙 kind switch는 없다. adapter는 enum/range/unit/ref/tag/subject/provenance를 fail-closed 검증한다. memory signature는 종류·상태·subject를 분리한다. provisional r2의 추가 17개 타입은 이름의 suffix나 UI/감사 성격 추정만으로 제외하지 않고 각각 별도 후보로 추적하며, owner 공동커밋 또는 최종 스키마가 요구하는 정확한 NonResult 증거 전에는 blocked다.

## 미실행 검증

Unity 6000.3.8f1 focused batch를 실행했으나, 시나리오 진입 전에 소유 범위 밖 Evolution assembly의 `GameplayOutcomeEvidenceBindingSnapshot` 누락 CS0246 두 건으로 컴파일이 중단됐다. 정확한 오류와 Unity log SHA-256은 `physical-relocation-runtime-test.json`에 보존했다. `TradeInventoryOutcomeDebugScenarios`에는 lowercase hash, declarative 22 rows, post-ledger exception, save/restore 후 compacted tombstone reconciliation, prepared relocation rollback, relocation retry-before-ack/restore/different-payload/terminal-tombstone 시나리오가 들어 있으나 실행되지 않았다. 따라서 verified implemented count는 0이다.
