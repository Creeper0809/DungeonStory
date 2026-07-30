# Progress

## 2026-07-29

- Started the treasury-centered economy implementation in an isolated planning directory.
- Captured the approved product decisions and verification gate.
- Began auditing concrete runtime and save boundaries.
# Progress

- Created an isolated implementation plan for the treasury economy pass.
- Audited money storage, operating-day settlement, facility shop refresh, physical stock delivery, building construction costs, and sectioned save infrastructure.
- Confirmed a low-risk migration path: observe the authoritative money value, preserve old call sites, then migrate new features to explicit transaction contexts.

# Next

- Implement the transaction ledger and scene-scoped treasury save section.
- Add employment contracts and ordered daily settlement.
- Add target-stock and wishlist procurement using physical drop-zone delivery.

## 2026-07-29 Implementation

- Added contextual manual shop purchase transactions and kept auto procurement under its own ledger kind.
- Completed deterministic employee wages, mercenary advance/renewal/departure, and daily priority ordering.
- Added target-stock and wishlist procurement with protected funds, idempotent daily keys, and physical drop-zone deliveries.
- Removed ordinary construction gold spending while preserving construction value for threat and market calculations.
- Added precision reforge services, 24-hour overclock state, treasury-powered defense policies, bribes, and offense field funds.
- Added treasury/resource HUD, finance detail window, procurement controls, and paid facility summaries.
- Replaced direct offense gold rewards with physical unappraised loot, G06 appraisal work, and appraised valuables sale.
- Added D12 mercenary hiring content and validation.
- Added physical facility installation kits and item-specific construction material requirements with V1-to-V2 work-order restore support.
- Extended focused debug scenarios for installation-kit delivery, loot appraisal, tavern hiring, and economy content wiring.
- Unity runtime and editor assemblies compile successfully through Unity 6000.3.8f1 Roslyn response files.
- Added the authored `금고각인 쇠뇌대` defense facility, overclock support, treasury-shot policy, material construction requirements, and tactical-command research unlock.
- Re-ran focused work, production-economy, and settlement contracts successfully.
- Verified the live treasury service graph and V17 sectioned save round trip, including the `economy.treasury` section.
- Captured and inspected the treasury HUD and finance window at exact 1600x900 and 900x1600 GameView resolutions.
- Final Unity Console audit reports Error 0 / Warning 0.
- Final runtime and editor Roslyn compiles both exit successfully.
