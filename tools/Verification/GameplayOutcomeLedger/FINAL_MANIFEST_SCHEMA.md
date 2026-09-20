# Phase80 final producer closure manifest

The final manifest is generated only after every source writer is frozen and the
inventory start/end digest is equal. It contains exactly one row for every
pre-existing `*Receipt`, `*Event`, `*Result`, `*Outcome`, or `*Resolution`
definition in the final inventory. Phase80 implementation contracts are excluded
from this denominator and are instead covered by registry/descriptor tests.

Top-level fields:

- `schema`: `gameplay-outcome-ledger-final-closure@1`
- `sourceCommit`, `sourceScanSha256`: copied from the final-frozen inventory
- `candidateCount`: exact number of pre-existing inventory candidates
- `humanApprovalClaimed`: always `false`
- `trainingEligible`: always `false`
- `rows`: one reviewed disposition per candidate

Every row has `candidate`, `definitionPath`, `status: VERIFIED`, `disposition`,
`reason`, `sourceOwner`, `authoritySymbol`, and at least one repository-relative
`verificationEvidence` entry containing `kind`, `path`, lowercase SHA-256, and a
concise proof description. Evidence paths must resolve inside the repository.
`Record` and `ExactSubstitute` require both `source-owner` evidence under
`Assets/Scripts/` and a separate `runtime-test` artifact under `Artifacts/QA/`.
`NonResult` requires `source-audit` evidence. A source hash alone cannot promote a
row to `VERIFIED`.

`Record` additionally identifies the outcome type, owner seam, adapter,
descriptor, projector, joint commit boundary, save-backed outbox owner, canonical
replay identity, and UI/query proof.

Different Records may intentionally share one registered outcome type when a
dynamic adapter uses the receipt shape and canonical replay identity to
distinguish operations. Candidate rows remain unique; an outcome type ID is a
schema family, not a per-producer primary key.

`ExactSubstitute` proves that a higher result from the same operation preserves
all committed facts and provides the same transaction/save/replay/UI fields.
Lower-level results cannot be dropped merely because their name looks internal.

`NonResult` supplies one explicit category accepted by the verifier. It is used
only for diagnostics, UI wrappers, pure queries/projections, previews, persistence
maintenance, lifecycle/progress signals, contracts, or duplicate derived
notifications. A failure result a player should observe is not a `NonResult`.

The verifier intentionally rejects `planned`, `blocked`, `pending`,
`source-complete`, and missing evidence. Unity compilation and runtime/performance
evidence are separate gates and must be referenced by their frozen artifact hash.
