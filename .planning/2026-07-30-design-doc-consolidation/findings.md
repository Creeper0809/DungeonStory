# Findings

## Initial
- The document is already a substantial 1,089-line implementation inventory with coherent sections for the core loop, 18 implemented feature domains, current development, content scale, technical architecture, validation, and conclusions.
- It already covers surgery, V17 offense, physical items, BT+Utility AI, captivity/circus, wildlife, production economy, combat, research tree, and developer tools.
- The most important missing implemented domain is the new electricity, clean-water/wastewater, conveyor, overflow, and automation system.
- The economy section still describes the older broad settlement ledger but does not document the newer treasury role, protected funds, wages/mercenaries, daily auto-procurement, precision reforging, 24-hour overclocking, or treasury-powered defenses.
- Research counts and content scale are stale: the document says 78 projects, while the current industrial build contains 118 projects.
- Player-experience guidance is fragmented across one sentence in the daily loop, world readability notes, and individual feature UI details. There is no authoritative UX chapter covering information hierarchy, interaction modes, feedback, failure recovery, responsive layouts, accessibility/readability, or first-run teaching.
- Status and implementation facts are intermingled with product intent. The revision should preserve both but clearly distinguish gameplay contract, UX contract, and current verification evidence.

## Code cross-check
- Treasury economy is implemented as separated runtime/services: `AutoProcurementRuntime`, `EmploymentContractRuntime`, `EquipmentOverclockRuntime`, `TreasuryDefenseRuntime`, and `EconomyTransactionLedgerRuntime`.
- Economy transactions cover wages, mercenary renewal, paid facility contracts, auto procurement, precision reforging, equipment/facility overclock, treasury defense shots, bribes, and expedition field funds.
- Industrial infrastructure is implemented under `Assets/Scripts/Services/Infrastructure/Industrial` with separate power, fluid, conveyor, automation, overlay, save, stress, and PlayMode verification code.
- Asset counts are 118 research projects and 32 industrial building assets.
- Industrial contracts preserve power/clean-water/wastewater channels, manual/powered-assist/automatic modes, stall/deadlock detection, and physical overflow routing without deleting item metadata.
- The active design document already contains extensive uncommitted additions for surgery, V17 offense, multi-haul, exact click priority, construction safety, and AI naturalness. These edits must be preserved and extended.
