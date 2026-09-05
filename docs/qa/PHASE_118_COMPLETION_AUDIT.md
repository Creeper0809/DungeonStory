# Phase 118 completion audit

This checklist audits the reduced, feature-focused Phase 118 contract against
the original player-facing requirements. It does not restore the retired goal
of moving every gameplay source out of `Assembly-CSharp`, and it does not
authorize another save redesign without a current failing regression.

## Verdict legend

- `PASS-SOURCE`: current source, metadata, and a fresh offline compile support
  the requirement.
- `PASS-LOADED`: current Unity-loaded execution produced the required evidence.
- `PENDING-LOADED`: source evidence exists, but completion cannot be claimed
  until the current Unity-loaded gate passes.
- `FAIL`: current evidence contradicts the requirement.

## A. Scope and authority contract

| ID | Requirement | Required evidence | Current verdict |
|---|---|---|---|
| A1 | Completion is based on concrete authority defects, not a mechanical `Assembly-CSharp == 0` count. | Phase 118/117 authority validator and reviewed default-domain classification pass. | PENDING-LOADED |
| A2 | SO assets remain immutable content authority; runtime state and save DTOs remain separate. | Runtime-authority and content-authority steps pass on current Unity assemblies. | PENDING-LOADED |
| A3 | Physical items are the single stock/equipment/module instance authority. | Physical item, physical stock, equipment item-state, transfer, loss, and round-trip scenarios pass. | PENDING-LOADED |
| A4 | Typed persistent IDs remain stable; name, coordinate, and Unity instance IDs are not persistence keys. | Persistent-identity and V18 authority scenarios pass. | PENDING-LOADED |
| A5 | No new speculative save or assembly migration was introduced during final closure. | Scoped source review and diff audit show final work is regression, orchestration, and concrete smoke correction only. | PASS-SOURCE |

## B. Original gameplay requirements

| ID | Requirement | Required evidence | Current verdict |
|---|---|---|---|
| B1 | The research graph contains exactly 168 rewarded nodes with valid causal prerequisites. | `ResearchTreeDebugScenarios`, the 168 research/equipment overhaul step, and ResearchTree PlayMode target pass. | PENDING-LOADED |
| B2 | Production is a real branching dependency graph with concrete recipe inputs, buffers, distribution, fuel/feed supplies, and stock-sensor behavior. | Branched-production, production-economy, infrastructure scenarios, and Production pointer matrix pass. | PENDING-LOADED |
| B3 | Equipment research locks, tiers, growth slots, modules, ammunition, reload, smoke, and misfire cannot be bypassed. | Combat/equipment suites and the current runtime composition pass. | PENDING-LOADED |
| B4 | Lineage transfer uses the real queued work and physical-item authorities. | `PhysicalItemDebugScenarios` consumes source equipment and seal, preserves target manufacture properties/modules, and transfers valid history. | PENDING-LOADED |
| B5 | Expedition death loses the equipped item and its installed module together and removes the loadout reference. | `OffenseJourneyDebugScenarios` executes `OffenseExpeditionReturnPort.HandleMemberDeath`. | PENDING-LOADED |
| B6 | Firearm smoke is distinct from target suppression and is applied to the shooter exactly once for hit, miss, and misfire. | `CombatResolutionService.Record` is the sole mutation point; `CombatSystemDebugScenarios` asserts three exactly-once calls. | PENDING-LOADED |
| B7 | Bow, crossbow, and firearm roles remain non-dominating alternatives. | Combat preview, penetration, reload, cadence, ammunition, and misfire scenario passes. | PENDING-LOADED |
| B8 | Research, production, equipment, and service/medical surfaces remain usable at `1600x900` and `900x1600`. | Six-target PlayMode matrix uses Unity EventSystem/automation and produces fresh explicit PASS reports. | PENDING-LOADED |

## C. V18 save and run isolation

| ID | Requirement | Required evidence | Current verdict |
|---|---|---|---|
| C1 | The current root is V18 with exactly 54 registered and captured save sections. | Strict-save synchronous step and FullWorld PlayMode report both prove 54. | PENDING-LOADED |
| C2 | Restore is staged and atomic; rejected or late-failing data does not partially mutate the live world. | V18 save-section late-failure tests and full-world canonical baseline comparison pass. | PENDING-LOADED |
| C3 | Legacy run-variable payloads are rejected instead of inferred or silently upgraded. | Current-envelope/V2-payload fixture reports the explicit unsupported-version error and canonical live state remains unchanged. | PENDING-LOADED |
| C4 | The full-world scenario restores its exact pre-run state rather than merely returning without throwing. | Pre/post canonical 54-section captures match; `canonicalBaselineMatched=true` and `baselineRestored=true`. | PENDING-LOADED |
| C5 | Scene/run transitions do not leak static or transient state. | Runtime composition, transient-root, new-game/save/load/scene, and FullWorld gates pass. | PENDING-LOADED |

## D. Final acceptance gates

| ID | Requirement | Required evidence | Current verdict |
|---|---|---|---|
| D1 | Current runtime and Editor sources compile with zero diagnostics that block execution. | Fresh Bee response files compile `Assembly-CSharp` and `Assembly-CSharp-Editor` successfully. | PASS-SOURCE |
| D2 | The synchronous final runner passes all 33 named steps. | Fresh `Artifacts/QA/final-acceptance-report.txt` explicitly passes all steps. | PENDING-LOADED |
| D3 | The ordered PlayMode matrix passes Resolution, FullWorld, Research, Production, ServiceRoom, and CharacterSummaryMedical. | Fresh coordinator report starts with `FINAL_PLAYMODE_ACCEPTANCE RESULT=PASS`. | PENDING-LOADED |
| D4 | FullWorld captures warnings/errors from request creation through runner startup, including the domain-reload interval. | Early persistent buffer is drained by the runner; its report has zero warnings/errors. | PENDING-LOADED |
| D5 | Final Unity Console contains Error 0 and Warning 0. | Unity MCP reads the complete Console after the matrix without clearing it. | PENDING-LOADED |
| D6 | Final orchestration leaves no request, state, pending-finish, or early-console buffer marker. | All ten known marker paths are absent after completion. | PENDING-LOADED |

## Current source-integrity checkpoint

- Fresh offline current-source compile: runtime PASS, Editor PASS.
- Missing source paths in fresh Bee response files: 0.
- Audited C# files with missing `.meta`: 0.
- Duplicate GUIDs among audited C# files: 0.
- Scoped `git diff --check`: PASS.
- Known request/state/buffer markers before loaded execution: 0.
- Unity-loaded result: pending; no final completion claim is valid until all
  `PENDING-LOADED` rows are independently resolved.

