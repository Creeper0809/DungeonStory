# Phase80 External/Faction Outcome Atomicity Evidence

## Implemented record

`OffenseTruthRevealedEvent` is now derived from `OffenseTruthRevealOutcomeReceipt` at the real campaign commit boundary.

1. The producer validates the target and builds the exact receipt, including immutable target display metadata and SHA-256 provenance of the authored truth title/text.
2. `TryPrepare` runs before any campaign mutation. Capacity failure returns `false`; campaign state, alerts and events remain unchanged and this path consumes no RNG.
3. The producer builds independent rollback and next-state candidates from the save authority, mutates only the detached next candidate, then publishes it.
4. The prepared ledger row commits using the same post-change campaign revision. A ledger commit failure republishes the rollback candidate.
5. Delivery and acknowledgement occur after domain/outbox commit. Delivery failure remains in the ledger outbox and does not repeat the campaign mutation.
6. Only after commit does the producer invoke `Changed`, raise the alert and publish the legacy event.
7. Canonical replay accepts only `AlreadyCommitted`, `AlreadyPublished` (including acknowledged), or `AlreadyTerminal` returned after the recorder has rebuilt and hash-compared the exact payload. The returned identity must match the exact result key and be at `Committed` or later. A plain/unreconciled duplicate and every conflicting payload remain failures.

The outcome is intentionally non-compactable and pinned at Core tier, so no compacted projector or additive-metric reference is required. Provenance rows are audit identity only; retention is controlled by the target subject link and memory policy, not provenance.

## Festival zero-producer finding

`FestivalOutcomeEvent` still has zero producers and one identity subscriber. It has not been silently removed or labelled `NonResult`: the existing `FestivalCelebratedEvent` omits the identity event's explicit per-participant outcome mapping and is not yet atomically ledger-integrated. The row therefore remains `dependent-substitute-pending`.

## Deliberately blocked rows

The remaining twelve high-level records are post-state notifications over authorities that cannot presently roll back with a gameplay-outcome commit. The ten low-level receipts have no ledger-integrated higher same-operation result preserving all fields. No event-bus observer was counted as integration and no low-level receipt was reclassified as `NonResult` without an exact substitute.

## Verification scope

Only bounded static checks were run in this worker: path inventory, producer/subscriber search, `git diff --check`, registration/reference search and Unity meta pairing. Unity, Unity CLI and Unity MCP were not started.
