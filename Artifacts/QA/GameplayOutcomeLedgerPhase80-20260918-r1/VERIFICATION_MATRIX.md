# Phase80 verification matrix

Status: source edit active; no Unity result is accepted yet.

| Gate | Required proof | Current state |
|---|---|---|
| Registry closure | every descriptor has an adapter; duplicate receipt/type and unknown role/metric fail closed; content-ID branch count 0 | isolated core scenario PASS for missing/duplicate descriptor-adapter closure; final production registry/content branch audit pending |
| Page/lease safety | small/large reservation, capacity backpressure, cancel/commit lifecycle, generation rejects stale tokens, warm recorder path allocation measurement; retained Core/pin/anchor rows may exceed hot-page count without blocking later capture | isolated core scenario PASS for active-save rejection and owner revision. Source now reserves retained small/large slabs before owner mutation and uses a pre-sized sorted flat due index instead of per-result detached arrays or tree nodes. Configured known-result capacity fails before mutation, and an unexpected cold-promotion failure stays retryable as `RetentionPromotionDeferred`; target Player 0 B/result evidence remains pending. |
| Idempotent outbox | duplicate result key rejected; commit replay is idempotent; deliver fault retains payload; publish-before-ack restore retries as AlreadyPublished then acknowledges | isolated core scenario PASS for descriptor fault retention/retry, acknowledged identical replay and conflicting replay; publish-before-ack restore fault matrix pending |
| Save atomicity | staged candidate validates before pointer swap; tamper leaves live state unchanged; current domain/outbox/ledger generation agrees; in-flight consolidation scratch is not serialized | isolated core scenario PASS for active reservation guards, immutable-hash tamper rejection, staged exact restore and mid-consolidation exact-input restart; Unity save coordinator fault injection pending |
| Memory policy | direct subjects are never lost to witness limits; optional witness cap is deterministic; Core/anchor exact retention; compaction count/metrics/hash; Forgotten excluded | isolated core scenario PASS for optional-witness filtering, two-subject compaction, global dedupe, Forgotten terminal replay, transactional evidence retention/removal and 200-row Core retention |
| Time slicing | slice sizes 1 and 128 and differing elapsed budgets produce identical tiers, aggregates and hashes; restore resumes from exact next sequence; a day-2 job reserved while day-1 backlog exists does not re-evaluate rows before their persisted due day | isolated tiny-slice save/restart, explicit slice1-vs128 compact/tombstone hash parity, day2 queued during day1 backlog, and 128-record long-run drain PASS; Player time-budget measurement pending |
| Perspectives | global/attacker/victim/witness/multi-role projection use one outcome ID; neutral frame is explicit | core display-name snapshot/revision/pronunciation contract and exact/compacted projector gates present; real-domain role matrices and UI wiring pending |
| Korean particles | Hangul/NFC/NFD/markup/numbers/initials/unknown pronunciation/cache collision plus deterministic bounded-cache eviction | isolated C# PASS including capacity-2 deterministic FIFO eviction; Unity and all real projectors pending |
| Production | completed physical output remains retryable on prepare/commit failure; one committed result is delivered once without changing conservation | source edit active |
| Combat | capacity failure precedes mutation; exact clamped damage; all mutated owner state rolls back on prepare/commit failure; delivery failure never repeats attack | source edit active |
| UI/query | global, character, facility, equipment and expedition views page by the same outcome ID; no direct mutable ledger access | all five source surfaces are wired to the presentation query and the literal internal `neutral-frame` row diagnostic was removed from player output. Unity/container/runtime verification and same-ID cross-surface proof remain pending. |
| Narrative evidence | eligible exact/compacted rows map to source outcome IDs; Forgotten rows excluded; anchoring and influence consumption are atomic with accepted use | source now stores durable per-subject `InfluenceUseCount`/`InfluenceRevision` independently from anchor count and keeps the save guard through owner terminal completion. Focused compile and pure harness PASS for cancel/rollback non-consumption, 0→1→2, stale revision, save/restore and all-or-none two-token completion; four mechanic-owner save/reconciliation evidence and Unity remain pending. Compacted evidence is display/context-only by explicit policy. |
| Producer coverage | unresolved type/receiver/domain 0; Record planned0; authority-fix0; transaction-integration-pending0; final equal source digests | r1 was Receipt/Event-only. Provisional expanded r3 scans 373 `Receipt|Event|Result|Outcome|Resolution` declarations with unresolved Publish0, unknown receiver0 and unclassified domain0, but active writers changed the source digest and 269 rows still require final Record/exact-substitute/NonResult proof. Final-frozen classification and closure verifier remain pending. |
| Performance | Player backend, hardware/config/policy/receipt sizes recorded; 10/100/500 result loads; warm-path 0 B/result; no sustained capacity wait | pending final build |
| Balance | mechanics, effects, costs, rewards, probabilities, cooldowns and AI utility unchanged | pending source diff and representative path comparison |

No row may be marked PASS from stale assemblies, an editor-only assertion when a Player measurement is required, an observer-only capture, or a fallback that discards a recoverable receipt.

The missing environment, evolution, and social-life composition-root call sites
have been added in source. `verify_composition_source.py` now passes with three
domain extensions, 20 adapters and 20 descriptors with core outcome-type
literal/switch branches at zero, but this is only a static
supplement; exact-once container construction and runtime registry closure remain
blocked until the writers freeze and Unity verification runs.

## Isolated core scenario evidence

- Scenario: `Assets/Scripts/Services/Narrative/GameplayOutcomes/Editor/GameplayOutcomeLedgerDebugScenarios.cs`
- Standalone compiler: Unity 6000.3.8f1 bundled Roslyn, Unity NetStandard 2.1 references, `/warnaserror+`.
- Compile result: `PHASE80_SCENARIO_COMPILE_EXIT=0`.
- Execution result: `PASS GameplayOutcomeLedgerDebugScenarios`.
- Versioned non-Unity evidence: `Artifacts/QA/phase80-core-ledger-20260918-r3/verification.json` (SHA-256 `0ba88f39220c4f7e36937c9990869e645d0089a96c187d59302a100ab1ed6d3`) and `source-manifest.txt` (SHA-256 `ed030c415373bbbc615f7d1eb28e7b9ec33c695775953cd6b2347faf952d52ca`).
- Covered in this harness: registry fail-closed checks; active save guards; owner revision mismatch; delivery fault/retry/acknowledgement; exact query/projection; identical/conflicting replay; SHA-256 tamper rejection; staged restore and epoch advance; notification-fault isolation; optional-witness filtering; two-subject compaction/global dedupe; Forgotten exclusion/tombstone replay; transactional evidence anchor add/remove; independent influence cancel/rollback/0→1→2/stale/save-restore/atomic-batch semantics; slice1-vs128 and queued-day hash parity; mid-slice save/restart; and 200 retained Core rows through a one-page hot pool.
- This is isolated C# evidence only. It does not replace the required Unity compile, Player backend allocation/load measurement, save-coordinator fault injection or real-domain/UI integration gates.
