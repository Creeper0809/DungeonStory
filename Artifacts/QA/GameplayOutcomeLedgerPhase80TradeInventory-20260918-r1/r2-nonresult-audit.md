# Group 5 provisional r2 NonResult audit

This audit is source evidence for final-frozen classification. It does not by
itself claim the final `VERIFIED` status or replace the final inventory/hash
gate.

| Candidate | Category | Source proof |
|---|---|---|
| `AnimalPenCompatibilityResult` | `pure-query-or-projection` | `AnimalHusbandryRuntime.EvaluatePen` only computes current pen risk/issues; its consumers are query ports and UI panels. It neither owns nor commits an admission/move. |
| `BalanceAttributionResult` | `debug-diagnostic` | Runtime source only exposes deterministic `BalanceAttribution.Attribute`; all consumers are `Editor/V27BalanceLedgerDebugScenarios`. |
| `BalanceSccAuditResult` | `debug-diagnostic` | `BalanceSccAuditor.Audit` is consumed only by `Editor/V27BalanceAudit` and Editor debug scenarios. |
| `PairedRunWindowResult` | `debug-diagnostic` | Rows are authored/consumed by V27 paired-run Editor verifiers and comparison evaluators, not by a gameplay command owner. |
| `PreparedOutputPickupBoundaryResult` | `pure-query-or-projection` | Internal `WarehouseItemMutationUndoJournal.ValidatePreparedOutputPickupBoundary*` returns only validation failure code/detail before pickup mutation; it is not a committed pickup result. |
| `ShopFeatureCommandResult` | `presentation-ui-wrapper` | The type contains only `Succeeded` plus localized `Message`; the command service delegates to shop/treasury/stock owners and the presenter consumes the wrapper for feedback. |
| `WarehouseFeatureCommandResult` | `presentation-ui-wrapper` | The type contains only `Succeeded` plus localized `Message`; it wraps delegated warehouse/contract/project commands for the presenter. |
| `WasteFeedResult` | `contract-or-interface` | The type has no production constructor/caller outside its declaration in current source. Actual direct feed commands return `WasteFeedRequestResult`. |

Finalization requirements:

- repeat the reference scan after source freeze;
- use the exact final inventory definition paths;
- attach lowercase SHA-256 evidence for every referenced file;
- keep player-observable failure results (`WasteFeedRequestResult`,
  `WastePolicyCommandResult`) as Records unless an exact same-operation
  substitute is integrated.
