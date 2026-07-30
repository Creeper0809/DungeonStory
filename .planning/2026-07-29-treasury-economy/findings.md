# Findings

- The worktree already contains broad surgery, offense V17, research, and content changes. Treat them as user-owned and preserve them.
- The repository root planning files describe prior V16/V17 work; this economy task uses an isolated plan directory.
# Audit Findings

- `GameMoneyRuntime` is only a thin wrapper over `GameData.holdingMoney`, while many legacy systems still mutate `holdingMoney` directly. A transaction ledger must observe `Data<int>.OnValueChange` so legacy mutations are recorded during migration.
- `OperatingDaySettlementRuntime` currently settles aggregate payroll and can issue emergency funding. It is the stable place to enforce the daily order `wages -> mercenary renewal -> paid contracts -> procurement`; emergency funding must be disabled.
- `DailyFacilityShopRuntime` refreshes once per operating day and exposes deterministic offers. Auto procurement should run from the refresh event after employment costs have been settled.
- `StockSupplyService` already creates physical drop-zone deliveries. Auto procurement can reuse it for stock categories instead of depositing directly into warehouse totals.
- Building placement still treats `BuildingEconomyAbility.constructionCost` as a gold cost, material fallback basis, refund basis, and invasion value. Placement spending/refunds must be removed while a separate construction-value accessor preserves threat/shop valuation and legacy asset data.
- `WorldItemStackRuntime` already owns physical stack state and drop-zone placement, but lacks a general item-at-dropoff API for blueprints, equipment, and facility kits.
- Starting staff have deterministic IDs (`owner`, `staff:{runSeed}:01`, `staff:{runSeed}:02`), allowing wage exemption without relying on display names.
- Save data is already sectioned. Treasury economy state should live in its own section so the global save version can remain unchanged.

# Migration Constraints

- The worktree contains substantial unrelated and in-progress surgery, offense, research, and content changes. All patches must be narrowly additive and must not revert or normalize unrelated files.
- Existing callers of `IGameMoneyRuntime` need to remain source-compatible. Context-aware transaction APIs should be additive while legacy calls receive a generic ledger classification.
- New runtime state must remain scene-scoped through VContainer and must not introduce another mutable global `Active` singleton.

# Implementation Findings

- Daily shop purchases now need an explicit transaction context because the same purchase API serves both manual buying and auto procurement. The runtime overload preserves old callers while recording `ShopPurchase` or `AutoProcurement` precisely.
- Building offers previously unlocked construction immediately without producing the promised physical installation kit. Facility purchases now create `facility-kit:{buildingId}` at the drop zone.
- Construction work orders previously supported only `StockCategory` materials. Item-ID requirements were added so a purchased installation kit is reserved, hauled, consumed, and saved like any other physical material.
- If no installation kit is available, construction still uses the building's ordinary physical material requirements. This preserves the rule that gold accelerates external procurement but does not gate normal construction.
- Offense money rewards were direct treasury deposits. They now create `offense:unappraised-loot`; G06 appraisal work converts it to sellable `offense:appraised-valuables`.
- Unity editor refresh is currently stale while the MCP connection awaits approval. Unity's own Roslyn compiler with the generated Bee response files reports runtime and editor compilation success.
- The GameplayScene QA fallback `OwnerSelectionPanel` covers the top-right resource HUD when the scene is opened directly. It is absent from the prepared product flow and was hidden only for visual QA.
- A direct synchronous GameView resolution change can block the editor command runner. Scheduling `GameViewResolutionController.Select` through `EditorApplication.delayCall` avoids the stall.
- Exact 1600x900 and 900x1600 captures show the physical-resource list, subordinate gold entry, finance detail window, and bottom navigation without overlap.
- The final live save round trip preserved all sections and included `economy.treasury`; physical item stack and sequence counts remained stable.
