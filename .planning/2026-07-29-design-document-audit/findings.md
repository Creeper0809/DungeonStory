# Findings

## Initial

- The document is about 38 KB and already has 15 top-level sections.
- Existing coverage includes core loops through V16-era evolution work.
- Recent surgery and Offense V17 work needs a code-backed coverage audit.

## Current document gaps found

- Sections 3.6, 5.1, 5.11, and 8 still describe the legacy three-member,
  linear-node offense route.
- Current Offense V17 uses up to five members, a persistent seeded hex world,
  dynamic and urgent sites, decision cards, command decks, clashes, tactical
  chains, safe-step return protection, and physical return.
- Section 5.6 covers stabilization/rescue/treatment but not surgery, anatomy,
  organ extraction/transplant, prosthetics, rejection, or specialized medical
  facilities.
- Research and content counts still say 72 projects and omit the six-project
  medical branch.
- The document mentions 112 debug commands but does not explain the developer
  mode toggle, palette, targeting, cheats, overlays, and debug-save marking.
- Content counts need to be recalculated from the current Resources assets.
- The document itself is valid UTF-8. Earlier mojibake came from PowerShell's
  default decoding, not from confirmed file corruption.
- Sections 10, 12, 13, and 15 still treat the full world map as future work and
  facility/equipment evolution as the sole current implementation priority.
  These statements need to be reconciled with Offense V17 and Surgery.
- The evidence table does not point to the medical domain, V17 offense content,
  developer tools, or their QA artifacts.

## Code-backed findings

- `OffenseV17ContentBuilder` declares 12 sites, 6 urgent sites, 49 decision
  cards, and 6 encounters.
- `OffenseExpeditionRuntime` enforces a five-member maximum for V17 travel.
- V17 has dedicated scenario, visual, save-roundtrip, safe-step, urgent-site,
  tactical-chain, command-deck, and truth-reveal verification code.
- The legacy offense panel/model still exists as compatibility code, so the
  document must identify V17 as the product path without pretending all legacy
  implementation files have been deleted.
- Surgery is not just planned: the project has anatomy models, procedure SOs,
  a registered `work:surgery` type and stat policy, surgical facility queries,
  risk/effect/policy/part/extraction services, a runtime and work handler,
  character and building UI, save section, content builder, scenario tests, and
  a PlayMode verifier.
- The developer mode has a user-facing settings toggle, palette controller,
  command registry/providers, world targeting, overlays, command history,
  debug-modified save metadata, and title/save-slot badges.
- The global `DungeonGameSaveData.CurrentVersion` is 17.
- Several older debug scenarios still assert V16 even though the authoritative
  save version is V17. The document should list this as stale validation debt,
  not claim the entire historical suite is currently green.
- The research project asset count is exactly 78.
- Medical content currently contains 16 medical assets and 13 medical-building
  assets; these counts include more than just procedures/buildings and must be
  broken down by type before documenting exact subtype counts.
- Wildlife still has 5 species assets in its current catalog.
- Current QA artifacts include surgery PlayMode evidence, research tree desktop
  and portrait captures, developer-mode desktop/portrait captures, V17 map,
  decision, command queue, and battle captures, plus a V17 visual report.
- Resource-production content is stored under `Assets/Resources/SO/Economy`
  (not the provisional `Production/Crops/Materials` paths). Exact recipe,
  substance, crop, and material counts need to use these actual catalog roots.
- Combat equipment assets visibly include 9 weapons, 8 armor pieces, and 2
  shields (19 total), matching the current combat catalog count.
- Medical facilities have 13 authored building assets (`M01` through `M13`).
- The economy catalog currently has 8 crops, 12 craft materials, 129
  production recipes, 7 substances, and 101 item-definition assets.
- Economy systems are connected through production bills, crop plots, animal
  husbandry, waste processing, resource usage indexing, forecasting, stock
  policies, contracts/grand projects, and player-facing building/operations/
  warehouse presenters.
- Medical content breaks down into 3 anatomy profiles and 13 procedures.
- Surgery PlayMode evidence passes actual pointer scheduling, physical medicine
  flow, accumulated work, all surgery stages, recovery, capture, and reports
  zero errors/warnings.
- Developer-mode PlayMode evidence confirms exactly 112 unique commands,
  exact-cell targeting, Shift repeat, right-click/Escape cancel, overlays,
  wildlife/character/invasion commands, responsive layouts, debug metadata,
  transient cheat reset, and zero errors/warnings.
- V17 visual evidence passes both 1600x900 and 900x1600 battle layouts with no
  text overflow.
- `BuiltInWorkTypeIds` now contains 29 work IDs. The document's 27-work table
  omits `work:surgery` and `work:threat-mitigation`.
- Current asset totals are 160 building assets and 257 economy assets.
- `Assets/Scripts` currently contains 875 project C# files, not the documented
  approximate 825.
- The stale work/content counts appear in sections 5.4 and 7 and need direct
  correction to 29 work IDs, 160 buildings, 257 economy assets, and 78 research
  projects.
- The day/run diagrams and offense subloop still encode the old linear route;
  they need hex travel, two-choice decisions, five-member command battles,
  urgent-site pressure, safe-step protection, and physical return.
- The existing research paragraph only mentions queue/progress. It omits the
  full-screen deterministic graph layout, search/filter/zoom, physical
  blueprint archive rules, dependency auto-queue, drag ordering, and responsive
  detail/queue sheet.
- The physical-item section omits pile list/detail UX, exact selection, partial
  reservations, multi-pickup haul plans, destination-grouped unloading, and
  construction-safety ordering.
- `SampleScene` is still enabled in build settings after `GameplayScene`; the
  document should describe it as a debug scene that is still present in the
  build list and therefore requires release-build cleanup.
- Research Tree is code-backed with deterministic graph layout, physical
  blueprint rules (`None/Required/Shortcut`), an 8-slot archive ability,
  full-screen pan/zoom (0.55-1.45), queue drag ordering, optional auto-pause,
  responsive UI, and dedicated scenario/PlayMode verification.
- Facility/equipment evolution has substantial model/runtime/save/UI-adjacent
  code, including ledgers, compaction, deterministic modules, recalibration,
  reforge orders, catalysts, LLM narrative proposals, and debug scenarios.
  However, unlike Surgery/Research/Debug/V17, there is no current consolidated
  zero-error PlayMode report in the QA artifact set. Keeping its status as
  implemented-in-parts/final-integration-required is accurate.
- All explicit evidence paths added to the document currently exist.
- World UX is also implemented but was under-documented:
  - Event alerts support right-click dismissal without deleting event history,
    with both scenario and pointer verification coverage.
  - `WorldCharacterNameplate` and `WildlifeActor` hide health bars at full
    health and reveal them for damage/combat; presentation updates are
    visibility-throttled.
  - `CameraManager` supports wheel/keyboard zoom using the UI clock, so game
    speed does not multiply camera movement or zoom.
  - `DungeonSceneBackdropFitter` reacts to orthographic zoom and refits the
    solid sky/background to cover the visible camera bounds.
- Current performance evidence includes real Unity Editor measurements on a
  `1024x1024` grid with normal new-run setup, real character prefabs, rendering,
  UI, physics, and dense facilities:
  - 100 actors + 8,192 dense facilities: valid, p95/p99 60 FPS criteria pass.
  - 500 actors + 8,192 dense facilities: valid and p95 criterion passes, but
    p99 fails and one-percent-low FPS is about 14; this is stress evidence, not
    a universal 60 FPS guarantee.
  - The latest 5x-speed 100-actor Editor profile uses a `1024x1024` grid with
    900 dense facilities, runs 30 seconds, passes p95/p99 criteria, and reports
    zero errors/warnings. It still records backlog/deferral and substantial
    cumulative allocation, so optimization remains an active risk rather than
    a completed guarantee.
