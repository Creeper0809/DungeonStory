# Phase80 final release evidence

The final release report uses schema
`gameplay-outcome-ledger-release-evidence@1`. It is created only after every
writer is frozen and the final source inventory has identical start/end hashes.

Top-level fields bind the report to `sourceScanSha256`, keep
`humanApprovalClaimed=false` and `trainingEligible=false`, and record that neither
Unity MCP nor `dungeon-player` MCP was used.

All 13 gates must have `status: PASS` and at least one repository-contained,
SHA-256-verified evidence artifact:

- source freeze and producer closure;
- Unity compilation and runtime registry closure;
- domain transaction, save/restore fault and memory determinism tests;
- five-surface/perspective UI, Korean Josa and narrative-evidence tests;
- target Player performance, balance impact and the one final knowledge-base rebuild.

Player performance records hardware, Unity version, backend, configuration and
receipt shape; measures loads 10, 100 and 500; proves zero warm allocation bytes
per result and no sustained capacity wait. It also measures the 100-day and
200-day long-run simulations, records positive serialized save sizes for both
checkpoints under one declared positive byte ceiling, and finishes with no
consolidation backlog. Balance requires zero gameplay-value changes. The final
knowledge-base rebuild count must be exactly one with zero failures.
