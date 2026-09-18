# WIM-045 durable return save/resume evidence

## Scope and authority

- Scope: the current-format expedition return state already stored by `offense.aggregate`; no legacy migration.
- Runtime authority: `OffenseExpeditionRun` owns the pending result and the per-member return stage.
- Persisted source state: `returnPending`, `returnSuccess`, `returnMessage`, and stable-character-ID return progress only.
- Derived state not saved: coroutine ownership, in-flight flags, retry clocks, warning suppression, and finalization guards.
- Position authority remains `CharacterWorldSaveService` grid coordinates. No expedition-specific position field or fallback motion was added.

## Connectivity manifest

| symbol/state | definition | live producer | authority | live consumer | save/recompute | observation | deterministic test | status |
|---|---|---|---|---|---|---|---|---|
| `returnPending` and pending result | `OffenseExpeditionModel.cs` | `OffenseExpeditionRuntime.CompleteExpedition` | `OffenseExpeditionRun` | runtime update and return coordinator | `OffenseSaveService`; aggregate v4, offense/journey v3 | existing expedition panel | real retreat, capture, whole-registry restore, resumed settlement | connected |
| `ExpeditionReturnStage` per stable character ID | `OffenseExpeditionModel.cs` | `ExteriorActivityRuntime` transport outcomes and death handling | `OffenseExpeditionRun.ReturnProgress` | return coordinator barrier/finalizer | capture/strict validation/restore; in-flight state recomputed | pending stage/failure in existing panel and alerts | invalid stage rejection, ownership interruption, denied-door resume | connected |
| terminal return finalization | `OffenseExpeditionReturnCoordinator.cs` | all member stages terminal | result history plus existing reward/resource services | active-expedition removal | terminal guard is derived and not persisted; partial finalization is not saveable | result history | restored return reaches physical entry and produces one result | connected |

Audit count for this bounded state family: definitions 3; live producer connections 3; live consumer connections 4; focused execution path 1 with four named assertions; orphans 0. Core content-ID branches 0; unregistered capabilities 0. Synthetic content canary is not applicable to this current-format lifecycle/save correction; future-content closure is not claimed.

## Focused execution

- Main Unity compile: PASS; compilation errors 0.
- `IndustrialInfrastructurePlayModeVerifier.RunReturnRestoreOnly`: PASS.
- Assertions: active run retained until finalization; lifecycle-transfer start rejection retains state and resumes; current-format invalid stage rejected atomically; whole-registry checkpoint resumes from the canonical saved cell and finalizes exactly once.
- Cleanup: exact except the existing meta elapsed-time float reprojection below `0.00001s`; no physical, offense, actor, or reward difference.
- Final Unity Console: warnings 0, errors 0. Play Mode exited and no runner remained.
- Stable report: `Artifacts/QA/wim-implementation/wim-045-return-restore.txt` (SHA-256 `2A51E9B6E7ADD8C87D74B8C2EC2B71E679F0B355E7486DACC1CEC95E78DF3258`).

## Balance and investigation record

- Balance classification: existing WIM-045 balance baseline assignment only; no authored BOM, direct/embedded work, time, space, power, water, risk, reward quantity, or price changed. The correction prevents dropped/duplicated settlement and does not add a synthetic fallback, reward, or alternate strategy. Full-game balance completion is not claimed.
- Knowledge-base query: `OffenseExpeditionRun returnPending OffenseAggregateSaveValidation`, area `persistence`, status `stale` with 22 failures. Content digest `b3902a5c663e26ff1f7a9922645caa2ed533d54e7bbdd85761e6d91b97c28b6e`; system digest `0167d00e4e352023c25583435640e7b8213a803b25c92c0fd56f26d26335f257`.
- Direct originals inspected: the model, runtime, return coordinator, exterior transport, aggregate validator, save service, expedition panel, focused scenario, and PlayMode runner.
- Remaining scope: legacy saves are intentionally excluded. This evidence does not cover invasion, launch/supply consumption, replenishment pickup, or broad door regressions already tracked separately.
