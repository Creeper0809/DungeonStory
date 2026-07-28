# Facility And Equipment Infinite Evolution

## Goal

Implement persistent, deterministic, work-driven infinite evolution for facility and combat
equipment instances while keeping rooms dynamic, LLM output presentation-only, and long-run
history bounded through deterministic compaction.

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit existing facility evolution, equipment, work, item, room, LLM, UI, and save contracts | Complete |
| 2 | Add shared usage ledger, hierarchical compaction, deterministic hashes, modules, catalysts, and attunement models | Complete |
| 3 | Extend facility instances with generations, three deterministic candidates, room activation, recalibration, relocation preservation, and save state | Complete |
| 4 | Extend equipment instances with usage-led evolution, catalyst-selected reforging, attunement, derived stats, and save state | Complete |
| 5 | Connect physical catalyst economy, work orders, hauling, LLM narrative requests, UI, and notifications | Complete |
| 6 | Add EditMode and PlayMode verification, long-run compaction tests, persistence checks, and clear Unity Console | Complete |

## Decisions

- Rooms own no persistent progression; they provide cached current environment and synergy.
- Facility evolution presents three deterministic candidates and requires player selection.
- Every equipment instance can evolve, but its direction is inferred from use history after the
  player selects reforge materials.
- Main effects are registered deterministic modules with explicit benefits and burdens.
- LLM output cannot define mechanics, hashes, evidence, budgets, or progression eligibility.
- LLM history remains hidden until a validated response succeeds; no player-visible fallback.
- Catalyst families drop randomly, while potency gates, salvage, refinement, and gold exchange
  prevent progression deadlocks and trivial low-tier grinding.
- Mutable instance progress stays in runtime/save data, never ScriptableObject assets.

## Completion Gate

- Existing facility and combat equipment authority remains singular.
- Facility movement preserves instance evolution; destruction and reconstruction do not.
- Room changes invalidate contextual module activation without mutating permanent nodes.
- Save/load cannot reroll candidates or reforge outcomes.
- Raw usage events remain capped at 128 and hierarchical compacted segments remain bounded.
- Invalid, late, or failed LLM responses never expose technical text or alter mechanics.
- Focused regressions pass and Unity Console reports Error 0 / Warning 0.

## Errors

| Error | Attempt | Resolution |
|---|---|---|
| Combat result struct compared with null | 1 | Removed the invalid null comparison and recompiled successfully. |
| Runtime evolution module registry resolved as empty | 1 | Marked the built-in constructor as the VContainer injection constructor and verified localized module effects in PlayMode. |
| Packed relocation restored on the authored layer | 1 | Added an explicit runtime layer and packed marker state to modular facility save payload v3. |
| Empty Unity-serialized orders became 0.1-work ghost orders | 1 | Treat orders without an order ID as absent during state cloning and verified save round-trip equality. |
