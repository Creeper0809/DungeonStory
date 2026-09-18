# DungeonStory Current Task Plan

Updated: 2026-09-18

This file contains only the current cross-repository implementation state. The complete
pre-cleanup planning history is recoverable from Git commit
`6e77756f8909bcb23cc91f2698e5129e90eec123`.

## Active authority

- Implementation contract: `Tools/Documentation/gameplay-outcome-narrative-ledger-plan.md`
- Narrative-AI handoff: `../DungeonStoryNarrativeAI/HANDOFF.md`
- Documentation map: `DOCUMENTATION.md`
- Unity MCP and `dungeon-player` MCP remain disabled and must not be used.

## Current goal: Phase80 gameplay-outcome narrative ledger

Record every meaningful committed gameplay result under C# authority, retain or compact
it deterministically, and project the same fact by viewer perspective without a central
result-type switch.

## Completed evidence

- [x] Common ledger, save and memory core implemented with bounded retention and a flat due index.
- [x] Separate influence-use transaction/revision and atomic multi-evidence completion implemented.
- [x] Shared Korean particle adapter and focused static/runtime checks added.
- [x] Unity 6000.3.8f1 batch compile passes with zero C# errors:
  `Artifacts/QA/GameplayOutcomeLedgerPhase80Compile-20260918-r7/unity-compile.log`.
- [x] Narrative-AI gameplay-outcome verifier passes 17/17.

## Remaining completion gates

- [ ] Reclassify the focused runtime failures and fix composition plus facility, character and equipment paths.
- [ ] Freeze the source and reduce producer/transaction manifest unresolved or pending rows to zero.
- [ ] Connect all four mechanic-evidence consumers without adding model-owned mechanics or values.
- [ ] Register and verify global, entity and role-aware queries/UI for all five perspectives.
- [ ] Verify save/restore, fault injection, deterministic consolidation and no immediate duplicate delivery.
- [ ] Measure Player warm-path allocation, sustained load, capacity and lease behavior.
- [ ] Generate the final hash-bound manifest/export and rebuild the knowledge base once after source freeze.
- [ ] Only after Phase80 closes, resume Phase79 facility rows, C# replay and the complete 100-row editorial review.

## Invariants

- Existing QA, Review and Export evidence is immutable; new evidence uses a new versioned directory.
- Game rules, legal candidates, effects, numeric values and persistent state remain owned by C#.
- `humanApprovalClaimed=false`, `trainingEligible=false`, and `REJECT_BEFORE_DPO` remain in force.
- No reservoir/full generation, SFT, DPO, GGUF or release promotion occurs before the required gates.
- `SOURCE_EDIT` and `UNITY_VERIFY` never overlap.
