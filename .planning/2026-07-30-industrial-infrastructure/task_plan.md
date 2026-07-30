# Industrial Infrastructure Implementation

## Goal
Implement the V17 electricity, water/wastewater, conveyor/overflow, automation, research, UI, save, and verification plan on top of the current dirty worktree without reverting unrelated work.

## Phases
- [completed] 1. Establish compile baseline and map existing extension points.
- [completed] 2. Add shared utility topology, power, and fluid runtime contracts.
- [completed] 3. Add conveyor routing, stall/deadlock detection, and overflow ejection.
- [completed] 4. Integrate facilities, production automation, sanitation, work, and research content.
- [completed] 5. Add save sections, UI/debug surfaces, and editor validation scenarios.
- [completed] 6. Compile, run focused regressions, inspect Unity console, and document residual risk.
- [completed] 7. Run a populated live PlayMode scenario with real facilities, fluids, conveyor payloads, automation, and deadlock recovery.

## Decisions
- Preserve V17 global save version; increment only domain section versions.
- Keep physical water stacks and manual hauling as fallback paths.
- Never delete conveyor payloads during overflow recovery.
- Runtime state stays in scoped services; ScriptableObjects contain static tuning only.
- Existing user changes are authoritative and must not be reverted.

## Errors Encountered
| Error | Attempt | Resolution |
|---|---:|---|
| Initial multi-file patch did not match `GridCell.cs` context | 1 | Split the patch and targeted the actual area-rule file (`Grid.cs`). |
| Unity MCP command connection was revoked during refresh | 1 | Continue with static compilation checks; retry read/compile after Editor reconnects. |
| `dotnet build` unavailable because no .NET SDK is installed | 1 | Use Unity Editor compilation once the MCP connection returns; do not install unrelated tooling. |
| UTF-8 label cleanup patch reported a context mismatch after applying earlier hunks | 1 | Verify file bytes with UTF-8 reads and apply the remaining planning change separately. |
| Forced Unity refresh exposed public implementation constructors with internal topology dependencies | 1 | Kept public contracts and made scene-scoped implementation classes internal. |
| Industrial content builder used nonexistent power-priority names and an inaccessible FacilityData field | 1 | Mapped life support to `Critical` and configured work IDs through the public `AddSupportedWorkTypeIds` API. |
| Editor validation code could not reference internal topology descriptor types | 1 | Added the public primitive-only `IndustrialInfrastructureStressProbe` facade in the runtime assembly. |
| Stress-report `init` accessors failed because the Unity profile did not expose `IsExternalInit` | 1 | Replaced `init` with ordinary setters on the validation-only report object. |
| The first 10,000-cell stress run found that conveyor output ports emitted edges while input ports could not feed belts | 1 | Corrected port direction semantics so Input/Both feed the network and Output is a terminus. |
| Repeated auto-refresh diagnostics unbalanced Unity's refresh counter | 1 | Rebalanced the counter and verified subsequent PlayMode entry and asset refresh without assertions. |
| Live PlayMode verifier could not compile because an iterator yielded inside a try/catch block | 1 | Replace the coroutine body with a nested-iterator guard that catches exceptions outside yield statements. |
| The live automation facility lookup found no patched production assets | 1 | Patch production facilities by stable Craft/Cook/Butcher work IDs instead of a nonexistent `Production` semantic tag. |
| Pumped water remained invisible to point queries | 1 | Version the fluid snapshots and refresh them from both `Networks` and `TryGetNetwork`. |
| A full cyclic conveyor stayed `Stalled` after 30 seconds | 2 | Measure network-wide last progress and classify a fully blocked, powered, full SCC as `Deadlocked`. |
| The live deadlock fixture was partially unpowered | 1 | Use a fueled 32-output mana generator and correct its catalog fuel ID to `resource:mana-crystal`. |
