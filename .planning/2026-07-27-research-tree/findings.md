# Findings

## Existing baseline
- Legacy research authority is `FacilityBlueprintSO` plus `BlueprintResearchTask`.
- Existing runtime auto-enqueues on `FacilityShopPurchasedEvent`.
- Existing UI is a generic vertical feature-card list.
- Existing save section serializes blueprint IDs and progress.
- Generic unique physical item stacks and facility buffers already exist.
- Q01/Q03 modular research buildings already exist; Q03 is the intended archive.
- Global save version is V16 and must remain unchanged.
- Unity 6000.3.8f1 is connected through MCP; baseline Editor state is idle with Console Error/Warning 0.
- `UITabSurfaceKind` currently has only Construction, Staff, Feature; Research needs a dedicated surface kind.
- `RoomInstance.IsUsable` and `SupportsFacilityRole(FacilityRole.Research)` provide the required archive-room validity checks.
- Q03 currently has Research/Storage traits but no archive ability module.
- Unity ScriptableObject classes must live in a matching filename for generated `.asset` files to retain their script reference.
- The existing physical item runtime can route loose and stored stock to facility buffers, but its public delivery request is category-based; unique blueprint delivery needs an item-ID-aware endpoint.
- The item runtime already exposes `TryRequestItemDelivery(itemId, ...)`; research archives can automatically convert a loose blueprint stack into an item-specific haul request without creating a parallel delivery system.
- Existing blueprint unlock collections use `[SerializeReference]`; moving the same collection instance between ScriptableObjects does not serialize reliably. Migration must clone each registered unlock type.
- Legacy PlayMode verifiers still contain old card IDs and purchase-autoqueue assertions; they must be converted to physical stack/archive/tree-node assertions before final regression.
- `ResearchCraftingSummaryService` was the remaining product-facing legacy task consumer; it now reports project queue, completed projects, and active project progress.
- Research Tree's graph mask requires an explicit `CanvasRenderer`; the graph content root must use top-left fixed anchors because applying `sizeDelta` to a stretched root doubles and offsets the calculated layout.
- Scheduled tree refreshes must be suppressed during queue drag, otherwise the dragged row can be destroyed before pointer-up.
- Q03 is a modular facility part without a role-bearing facility ability, so `RoomLayout.TryGetRoom(BuildableObject)` alone cannot validate its archive room. Archive validity must fall back to each occupied grid cell.
- A blueprint temporarily disappears from world stacks while carried. Archive status must include character carry inventories so research reports `운반 중` instead of `미보유`.
- The physical-flow verifier must not restart an already reserved haul action. Restarting a multi-haul actor after pickup can redirect existing carried items into the new plan's destination and creates a false loss that normal gameplay does not cause.
- Research tree nodes are periodically rebuilt. PlayMode tests must record a button's existence before yielding because the clicked Unity object can become a destroyed reference after a scheduled refresh.
- `GetComponentsInChildren(..., true)` counts deferred-destroy inactive research nodes during same-frame refreshes; visible node assertions must use `includeInactive: false`.
- Pooled world-space character UI cannot be reparented while its actor hierarchy is deactivating. Those transient views should be destroyed and recreated instead of returned to the shared pool in that lifecycle window.

## Locked design decisions
- Full-screen dedicated research surface under the existing Research tab.
- 24 visible nodes, deterministic automatic graph layout.
- Blueprint rules: None, Required, Shortcut.
- Physical blueprints only count in a valid archive inside a usable Research room.
- Queue automatically inserts prerequisites; progress persists after removal.
- Research screen auto-pause is optional and defaults to false.
