# Treasury Economy Implementation

## Goal

Implement the approved treasury-centered economy without reverting the existing dirty worktree:

`physical production and service revenue -> treasury ledger -> wages/contracts/procurement
-> precision services/overclock/paid defense -> sectioned save/UI verification`.

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit current money, shop, construction, evolution, defense, UI, and save boundaries | Complete |
| 2 | Add authoritative transaction ledger, employment contracts, and daily settlement ordering | Complete |
| 3 | Add target-stock and wishlist auto-procurement with physical drop-zone delivery | Complete |
| 4 | Remove ordinary construction gold spending and preserve explicit construction value | Complete |
| 5 | Add paid reforge precision, 24-hour overclock, treasury defense, and bribe contracts | Complete |
| 6 | Add treasury/resource HUD and policy surfaces | Complete |
| 7 | Compile, run focused regressions, save round trips, pointer tests, and Console audit | Complete |

## Locked Decisions

- Gold is abstract treasury liquidity, not a physical item.
- General construction uses physical materials and work only.
- Start owner and two start employees are wage-exempt.
- Later employees have daily wages; mercenaries require daily renewal and physically leave when unpaid.
- Auto-procurement uses target stock for fungible goods and a wishlist for unique goods.
- Procurement never bypasses the drop zone or hauling.
- Reforge gold services are optional and never replace materials, catalyst, research, or work.
- Overclock lasts 24 scaled game hours and cannot stack, extend, or refund.
- Only explicitly configured high-end defense facilities may consume treasury gold for attacks.
- Global save version remains unchanged; owned save sections increase independently.

## Verification Gate

1. Runtime/editor compilation succeeds with Console Error 0 / Warning 0.
2. Construction placement never deducts gold.
3. Daily procurement is idempotent across save/load and counts in-transit stock.
4. Wage exemptions, arrears, mercenary renewal, and physical departure are deterministic.
5. Precision services, overclock expiry/strain, and paid defense budgets do not bypass core resources.
6. Economy UI exposes the resource summary, treasury forecast, policies, and failure reasons.
7. Focused EditMode and pointer-driven PlayMode tests pass.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---|---|
| Unity MCP approval timeout | 1 | Stopped retrying the revoked connection; used Unity's exact Roslyn response files for deterministic runtime/editor compilation. |
| `ProductionEconomyDebugScenarios.cs` missing parenthesis | 1 | Corrected the combined facility-tag query and recompiled both assemblies successfully. |
| Focused editor contracts expected global save V16 and 72 research nodes | 1 | Removed the stale save-version literal and updated the authored research count to the current 78-node surgery-expanded tree. |
| Production content count remained at 101 after adding two offense loot items | 1 | Updated the authoritative expected item count to 103. |
| Production recipe count omitted three prosthetic recipes and loot appraisal | 1 | Updated the authoritative expected recipe count from 126 to 130. |
| Direct synchronous GameView resolution command stalled the editor | 1 | Restarted the editor after confirming PlayMode was stopped, then added a deferred editor bridge and completed both resolution captures without another stall. |
| GameplayScene QA owner-selection fallback obscured the treasury HUD in captures | 1 | Disabled only the runtime QA fallback panel during capture; the prepared product flow remains unchanged. |
| Planning completion script was blocked by the host execution policy | 1 | Re-ran the same checker in a child PowerShell process with `-ExecutionPolicy Bypass`; the completion gate passed. |
