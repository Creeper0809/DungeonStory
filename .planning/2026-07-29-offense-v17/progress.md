# Offense V17 Progress

## 2026-07-29

- Preserved all existing surgery and gameplay changes.
- Confirmed the Unity editor baseline at Console Error 0 / Warning 0.
- Audited the current hard-coded world map, route generator, three-member
  expedition model, battle integration, save section, and composition root.
- Started the deterministic world/travel/threat vertical slice.
- Added a deterministic radius-9 hex world, weighted A* routing, dynamic sites,
  urgent-site lifecycle, and shared world threat modifiers.
- Added 49 deterministic two-choice decision cards, 10 site archetypes,
  6 urgent-site definitions, and 6 encounter definitions as individual assets.
- Added safe-step return protection with free-direction movement, pity rules,
  forced-combat caps, and protection removal on a new site attack.
- Added five-member command decks, two-card draws, enemy intents, clashes,
  tactical chain degradation, turn refresh, and exactly-once enemy actions.
- Added a full-screen generated hex-map surface, preparation/travel/event/battle
  views, pooled card-clash presentation, map pan/zoom, and Korean labels.
- Connected urgent threats to temperature, fuel, sanitation, disease, mood,
  rest, automated defense, invasion warning, and ranged accuracy.
- Added V17 world/travel/decision/battle persistence and V17 expedition target
  snapshots so generated-site expeditions survive save/load.
- Unity compilation and console verification remain Error 0 / Warning 0.
- Connected physical departure packing, real return drops, fixed boss rewards,
  urgent-site mitigation orders, and V17 mid-state save/restore.
- Added a status-only skill regression and guaranteed real weapon basic cards
  so an expedition cannot deadlock with non-damaging opening commands.
- Verified the generated map and combat path with actual pointer events; basic
  and pressure attacks reduced enemy health through the shared combat runtime.
- Added a portrait-responsive offense layout with a scrollable command sheet.
  Map visual verification now passes at 1600x900 and 900x1600.
- Captured and validated the Reigns-style decision surface at both target
  resolutions through an actual pointer-driven expedition.
- Fixed legacy battle UI overlap for V17 expeditions and verified the active
  V17 command surface contains enemy intent plus two candidates per member.
- Reset the map viewport when changing between map, decision, and battle
  surfaces so prior panning cannot clip the battle title.
- Added battle focus dimming while a command card is pending.
- Replayed the complete product flow with actual pointer handlers: owner
  selection, party confirmation, map/site selection, two-member departure,
  decision choice, command card, enemy intent, and command execution.
- Verified that only the V17 battle surface is active, turn resolution advances
  to turn 2, and the tactical chain reports a full executed stage.
- Final V17 contract suite passed 10/10.
- Final 1600x900 and 900x1600 battle captures passed panel bounds, text
  overflow, and nonblank pixel checks.
- Final Unity Console result: Error 0 / Warning 0.
