# P0-P1 Gameplay Closure

## Goal

Complete and verify the product-facing P0 and P1 gameplay closure:

1. A debug-free first-three-day run can progress through construction, survival,
   hauling, hunting/butchering, defense, and the first expedition without a
   silent stall.
2. Physical resources move through real storage, carry, delivery, work, and
   output flows with player-visible forecasts and blocked reasons.
3. Recoverable failures always expose a reason and a valid recovery route.
4. Defense reads as a complete rally-to-treatment loop.
5. Human and wildlife AI do not remain in unexplained waits, oscillation, or
   reservation churn during a long run.
6. Current desktop and portrait UI surfaces remain readable and input-safe.

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Establish compile, test, runtime, and direct-play baseline | Complete |
| 2 | Close first-three-day resource/work/haul flow and surface blockers | Complete |
| 3 | Add recovery paths for progression-blocking world states | Complete |
| 4 | Complete defense feedback from rally through rescue/treatment | Complete |
| 5 | Detect and repair AI stationary/oscillation/reservation failures | Complete |
| 6 | Re-audit current UI with pointer input and two target resolutions | Complete |
| 7 | Run regressions, captures, and final Console audit | Complete |

## Completion Evidence

- Current assemblies compile in Unity with Console Error 0 / Warning 0.
- A fresh non-debug run records first-three-day milestones and every blocked
  interval with a player-readable reason.
- Resource totals remain conserved across storage, reservation, carry, facility
  buffers, work completion, and output.
- No tested recovery scenario requires state injection to resume play.
- Defense PlayMode evidence covers rally, interior breach, guard response,
  reciprocal combat, downing, rescue, treatment, and AI resumption.
- AI soak evidence contains no unexplained stationary interval over 10 seconds,
  repeated two-cell oscillation, or unbounded reservation churn.
- Pointer-driven desktop and portrait checks cover the latest survival, work,
  wildlife, defense/combat, event, and save surfaces.

## Constraints

- Preserve unrelated dirty worktree changes.
- Do not count scenario state injection as proof of product-loop completion.
- Existing focused debug scenarios are useful regressions but do not replace a
  fresh player-path PlayMode run.
- Shared ScriptableObjects contain static authoring data only.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
| Release soak reported zero path searches while actors moved | 1 | Treat as invalid instrumentation; wire broker counters into the soak before using it as P1 evidence. |
| Standalone automation bridge file was absent | 1 | Continue with Editor PlayMode and defer standalone evidence until a player build is running. |
| Nameplate refreshed after the scene camera was unregistered during PlayMode teardown | 1 | Treat the missing camera as an expected teardown state in `WorldCharacterNameplate`. |
| P0 UI verifier request could not enter PlayMode after diagnostics edit | 1 | Compilation later reported an invalid `BuildableObject.objectNameOrDefault` call; replace it with the authored building name fallback before rerunning. |
| P0 flow-text evidence used a pattern variable behind a derived boolean | 2 | Declare the flow section object separately so definite assignment is explicit before reading child text. |
| Stale P0 verification request entered PlayMode after compilation | 1 | Stop the unintended SampleScene run, clear the stale request, and use the product release soak for logistics diagnosis instead of mixing debug-scene evidence. |
| Manual stale-request cleanup found no request file | 1 | The verifier had already consumed and removed the request; no filesystem cleanup was needed. |
| Natural construction could not select a hauler | 1 | Candidate tracing showed every prepared worker was in Priority1 `Operate`; retain the current task but queue a Priority2 haul for the next shift. |
| Queued haul preference did not produce a delivery | 2 | The destination stack and haul candidates existed, but the worker recommitted generic work. Trace action availability/consumption and promote the concrete blocking haul plan rather than relying only on a score bonus. |
| Prioritized construction stock lost priority after an AI reservation was cancelled | 3 | Keep haul priority independent from reservation ownership and remove it only when the stack is actually consumed or removed. |
| Delivered construction materials left the order Ready with no worker | 4 | Request a targeted `Construct` replan when materials transition to Ready; generic replanning was selecting unrelated Priority1 work again. |
| A one-shot construction replan could miss every worker while path searches were deferred | 5 | Keep Ready construction orders alive as a low-frequency tick source until one worker actually reserves the site. |
| Restored partial construction retained an `InProgress` worker ID without a live AI action | 6 | Restore durable progress and materials, but reset Ready/InProgress orders to Ready with no runtime worker reservation. |
| Restored item stacks retained reservations for haul plans that are not persisted | 1 | Clear transient item reservations during restore and return combat-loadout reservations to their source storage. |
| Cancelling or orphaning a construction site deleted reserved and delivered materials | 1 | Release destination stacks back to source storage or loose site stacks, and auto-cancel orphaned construction orders with conservation checks. |
| Responsive UI audit stopped while scanning an uninitialized TMP label | 1 | Ignore active TMP components whose text value is temporarily null during panel rebuilds. |
| Unity run-command resolved `CompilationPipeline` through its generated `Unity.*` namespace | 1 | Use the fully qualified `global::UnityEditor.Compilation.CompilationPipeline` name for the compile request. |
| ProductShell batch began with title DI alive but without the fresh title UI | 1 | Reset the verifier session/request state and launch the ProductShell verifier from a clean TitleScene edit-mode entry before accepting the shell regression. |
| ProductShell recruit fixture still produced no spawned visitor through `TrySpawnCharacter` | 2 | Trace customer-state, population-pool, and entrance eligibility; keep the offense attack-confirm fix, which now passes. |
| StartParty final regression expected generated active/passive skills on the owner | 1 | Align the verifier with the current owner contract: four fixed owner skills, while selected staff retain one generated active and first passive. |
| StartParty reroll controls were checked immediately after a drag swap | 1 | Select the newly promoted roster card and its Identity tab through pointer input before checking the contextual dice controls. |
