# Findings

## Baseline

- Product code is currently compiled into `Assembly-CSharp`.
- Runtime singleton-style `*.Active` access is widespread and bypasses
  VContainer scene lifetime.
- `IDungeonSceneComponentQuery` repeatedly searches loaded scene roots.
- `GridPathSearchBroker` and `CharacterAiWorldRegistry` are mutable statics.
- Work execution and candidate selection dispatch on `FacilityWorkType`.
- `BuildingAbility` serialized settings call runtime services directly.
- Item, wildlife, defense, UI, and save services each own multiple unrelated
  responsibilities.
- Unity Test Framework is installed, but product contracts are primarily
  verified by large editor/debug scenarios instead of NUnit fixtures.
- Unity MCP console reads can show the last successfully loaded assembly while a
  project compile failed. The migration now also checks the latest Editor log
  compilation block before declaring a checkpoint green.
- The AI naturalness observer was QA-only code compiled into the product
  assembly. Moving it under `Editor` removed five product scene searches and
  keeps artifact-writing automation out of player builds.
- `DefenseEngagementRuntime` mixed orchestration with combat resolution and
  equipment mutation. `IDefenseCombatExecutor` now owns the latter, leaving
  movement/placement and engagement storage as the next defense split.
- V15 root-field compatibility would have recreated the save God object.
  Editor contracts now write/read section payloads instead, so removed root
  fields cannot silently return.
- `DungeonRuntimeLifetimeScope` now delegates all registration to domain
  modules and contains only 13 direct `builder.Register*` calls, including
  scene-scoped instances and final build callbacks.
- The shop feature repeated the warehouse UI coupling: its presenter only
  called back into `P0FeatureSurfacePanel`, while query, purchase mutation,
  idempotency, and retail detail selection lived in the panel partials.
  The shop now owns those responsibilities through query/command/presenter
  services, leaving the panel as an `IFeatureSurfaceView`.
- `WorldItemStackRuntime` also owned route selection and multi-haul grouping.
  Moving those algorithms to `IWorldItemHaulPlanningService` keeps stack
  mutation in the runtime while making route policy independently replaceable
  and testable.
- Editor AI fixtures had silently bypassed the registered research candidate
  policy. Sharing a real `WorkExecutionHandlerRegistry` with the fixture fixed
  the regression and prevents editor-only behavior from drifting from the
  product composition.
- Defense engagement IDs were sequence-generated inside the coordinator.
  Restoring an existing `defense-engagement:N` did not advance that sequence,
  allowing a later invasion to reuse an ID. The scene-scoped engagement store
  now owns IDs, active records, and retreat history and observes restored IDs.
- VContainer selected `RandomStreamProvider(int rootSeed)` when the type was
  registered directly and attempted to resolve `System.Int32`. Registering an
  explicit seeded factory prevents primitive constructor selection from
  aborting the entire gameplay composition root.
- The P1/P2 pointer verifier loaded `GameplayScene` additively when another
  scene was already open. The resulting duplicate EventSystem and global
  runtime warnings were verifier isolation defects, not product-scene defects.
- `WorldItemStackRuntime` still owned inventory pickup, warehouse deposit, and
  facility-buffer transfer after haul planning was extracted.
  `IItemTransferService` now owns those mutations; the runtime remains a
  compatibility facade while more repository responsibilities are removed.
- Several hot-path services still used `IDungeonSceneComponentQuery` even
  though characters and buildings were already explicitly registered.
  Read-only character, building, wildlife, warehouse, and retail query
  interfaces now expose the scene-scoped registry without giving consumers
  mutation access.
- The Editor work-priority fixture only registered the research candidate
  policy. Repair therefore fell back to the structural facility check and
  accepted an undamaged target. Registering an Editor repair candidate policy
  restored parity with the product handler and clarified that
  `BuildableObject.CanAssignWork` checks structural support, not dynamic need.
- Scene-search fallback in item logistics hid missing registry registration
  in editor fixtures. Removing it initially exposed facility-delivery and
  multi-haul failures; registering fixture warehouses explicitly fixed the
  tests without reintroducing a production scan.
- The raw `.Active` search count after the migration was misleading: all 150
  remaining matches are enum values (`CharacterLifecycleState.Active` and
  `CharacterSkillKind.Active`), not static runtime accessors. The architecture
  test still reports zero static `Active` service locators.
- The feature presenter registry previously hid a second copy of every tab
  inside `P0FeatureSurfacePanel` partial methods. Once all eight presenters
  owned their query and command paths, those methods and 18 domain
  dependencies were provably unreachable. Removing them leaves the panel as a
  reusable view shell instead of a central gameplay controller.
- Warehouse, shop, research, and codex presenters were visually separated but
  still performed scene searches internally. Scoped warehouse/building/
  retail/character queries and cached runtime providers now make the
  presentation boundary real rather than cosmetic.
- `DungeonGameSaveService` carried domain DTO declarations even after section
  orchestration was introduced. Moving those declarations to their domains
  makes the section boundary visible in source ownership and prevents the
  root save file from becoming the next integration God object.
- Summary/query services are easy places for scene searches to return because
  they look read-only and harmless. Codex, research, offense, invasion, and
  reward-context queries now consume scoped providers, and a ratchet explicitly
  protects those five consumers.
- Several editor scenario builders woke `CharacterActor` before injecting
  `IGameClock`. Product prefabs were safe because VContainer injects inactive
  scene objects before activation, but edit-mode fixtures no longer matched
  that lifecycle. Their creation order now mirrors product composition.
- GameplayScene emits five transient Unity `Unknown Behaviour` warnings only
  during play-entry deserialization with domain reload disabled. Loaded scene
  objects, all prefab assets, script GUIDs, and supported managed references
  report zero missing scripts afterward. This appears to be transient editor
  serialization state, not a surviving runtime component; it remains an open
  diagnostics item before the final zero-warning gate.
- `SocialReputationRuntime` combined three hidden globals: scene traversal,
  `Time.time`, and `UnityEngine.Random`, then exposed itself again through a
  static `Current` used by save capture. Moving all four edges to scoped
  providers makes rumor spreading deterministic and removes a save-order
  dependency on whichever scene instance assigned the static field last.
- Meta progression initialized its tracker in a field initializer and then
  read Unity time directly. Deferring tracker construction to VContainer
  injection preserves scene lifetime while allowing deterministic elapsed-time
  and restore tests.
- The live MCP transport can fail independently of the Unity editor and its
  compiler. An isolated batchmode project is a reliable fallback for compiler
  and NUnit evidence without saving or rewriting the user's dirty live scene.
- `WildlifeEcosystemRuntime.cs` still contained a complete HUD toggle class.
  The first scoped-marker ratchet exposed that hidden presentation dependency;
  moving the toggle to `UI` made the habitat simulation source independent of
  scene lookup and TMP/UI types.
- `InvasionThreatSettings` and `CharacterSO` looked like static data assets but
  executed global random rolls. Passing an `IRandomStream` from the owning
  runtime keeps those assets declarative and makes candidate delays, customer
  budgets, visit counts, and respawn cadence reproducible.
- Direct product `Object.Find*` count is now zero. Remaining
  `IDungeonSceneComponentQuery` usage is concentrated in cached providers,
  composition/bootstrap, and presentation hosts rather than hot-path world
  decisions; those provider facades remain a later removal target.
- Clearing `RandomStreamProvider` on a new run invalidated the provider's map
  but not the `IRandomStream` references already held by long-lived services.
  Reseeding existing stream objects in place keeps every injected consumer on
  the same run seed. During load, `run.variables` replays its legacy draw
  history first and `foundation.random-streams` then restores exact per-stream
  states, preserving the next draw without reordering domain restore work.
- Isolated Unity test artifacts are emitted relative to the temporary project
  path, not the source repository. A missing root-level result file is
  therefore not evidence that the batch process failed; verification tooling
  must inspect the mirror path before diagnosing startup or lock failures.
- `WorkDutyController` is an actor-owned helper created by `AbilityWork`, which
  already owns the scoped `IGameClock`. Passing that clock into the helper is
  preferable to making the helper independently resolve runtime services.
  Scene services such as combat commands and the AI director can receive the
  same clock directly through VContainer.
- The Items folder is not yet a legal assembly boundary. Even the catalog layer
  directly depends on building-owned `StockCategory`, combat equipment assets,
  user settings, and `Resources.Load`. The next Items boundary must first
  extract stable item IDs/DTOs and invert catalog/equipment/settings lookups
  behind interfaces; adding an asmdef around the current folder would only
  encode the existing dependency cycle.
- V15 save contracts form a viable low-level boundary: the envelope, restore
  phase, section interface, and duplicate-validating/topological registry are
  consumed by every domain while orchestration and file I/O stay in
  Infrastructure. Moving that contract source into Foundation establishes the
  intended dependency direction without making gameplay assemblies depend on
  save I/O.
- `IDungeonSaveSection.Restore` still referenced
  `DungeonGameRestoreReport` declared inside `DungeonGameSaveService.cs`.
  The report is a domain-neutral warning/error collector, so it belongs with
  the save contracts in Foundation; the root save DTO and disk service remain
  Infrastructure-owned.
- The save-root architecture test was coupled to source declaration order by
  requiring `DungeonGameRestoreReport` to be the next class after
  `DungeonGameSaveData`. That assertion blocks legitimate file ownership
  changes. The test should delimit the root DTO independently and assert its
  fields, not enforce an unrelated neighboring declaration.
- `OperatingDaySettlementRuntime` still injects the generic scene query and
  exposes two `Construct` overloads with different dependency sets. This lets
  editor fixtures and product composition exercise different object graphs.
  It should consume narrow settlement inputs/providers and have one canonical
  constructor before the scene-query facade is retired.
- The settlement's five scene-query call sites only enumerate
  `BuildableObject` and `CharacterActor`. Existing `IBuildingWorldQuery` and
  `ICharacterWorldQuery` cover the complete read surface, so this migration
  does not need another abstraction or any hierarchy traversal fallback.
- The settlement editor fixture mirrored the production smell with a generic
  `FixedSceneComponentQuery` that traversed supplied GameObject hierarchies.
  A fixed world query exposing explicit actor/building lists will exercise the
  same narrow contracts as product composition and remove hierarchy-dependent
  fixture behavior.
- Local LLM request timeout and queue-age timing must continue while gameplay
  is paused, so it belongs on `IUiClock` rather than `IGameClock`. Product scene
  components can receive the scoped UI clock through VContainer; the two QA
  probes that dynamically create a queue must inject the same clock explicitly
  instead of relying on a hidden Unity-time fallback.
- `LocalLlmQueuedRequest` does not need to know any clock implementation.
  Passing the queue's sampled start time into `Attach` keeps the request record
  a passive state object while the scene-scoped queue owns timing policy.
- Rumor save models had hidden runtime behavior through `Time.time` properties
  and capture/restore helpers. Passing one sampled `now` value through a whole
  prune or save operation avoids boundary-time inconsistencies and keeps the
  serialized model independent of Unity.
- Main camera, grid texture/controller, and `GameManager` providers were
  caching the result of the same generic scene query independently. Capturing
  these references once in the composition root and explicitly re-registering
  replacements removes ten generic query edges without moving gameplay
  decisions into Infrastructure.
- `BuildableObject` is created through both VContainer injection and explicit
  runtime factories. Adding the clock to its existing injection method keeps
  normal building creation automatic, while `WorldFilthRuntime` must pass its
  own clock explicitly for dynamically added cleanup targets.
- Read-only clocks are insufficient for modal UI that intentionally pauses and
  resumes simulation. A narrow `IGameTimeScaleController` keeps that mutation
  centralized without teaching `IGameClock` command responsibilities or
  leaving UI coupled to Unity globals.
- Items could not become an independent assembly while `StockCategory` lived
  inside the `SaleItem` implementation file. Moving it with the other stable
  building enums into a no-dependency Buildings contract assembly removes that
  specific cycle without pretending the rest of Items is ready to isolate.
- The remaining generic scene lookups are concentrated in singleton runtime
  providers, not gameplay decisions. Offense, invasion, and feature-panel
  providers independently cache references obtained from the same hierarchy
  query. Domain-scoped composition-time reference groups can remove those
  edges without expanding the existing core reference bag or introducing a
  mutable service locator.
- Offense panels are an exception to immutable scene references because panel
  factories create them at runtime. Their domain reference group therefore
  needs explicit `RegisterWorldMapPanel` / `RegisterExpeditionPanel` methods;
  the factory remains the only creator and the panel service remains the only
  consumer.
- Exterior zones and wildlife habitat markers only require a one-time seed
  from the loaded scene. Capturing both collections in
  `WorldSimulationSceneReferences` preserves dynamic register/unregister
  behavior while removing repeated hierarchy traversal from simulation
  initialization.
- Debug commands that operate on all staff or locate the owner do not need a
  debug-specific scene lookup. `ICharacterWorldQuery` already represents the
  complete scoped read model and keeps debug execution consistent with normal
  gameplay registration.
- Build/player automation can discover dynamically created controls without a
  scene locator by traversing only the registered gameplay Canvas. The same
  bridge can read camera state from the captured user-settings targets.
- The generic scene-query interface existed primarily to support old Editor
  fixtures after product consumers were migrated. Replacing those fixtures
  with the concrete composition utility or scoped world query allowed the
  interface and its VContainer registration to be deleted entirely.
- The complete Rooms runtime still depends on buildings, grid, and
  presentation, but `FacilityRoleCatalog` is a stable leaf over the Buildings
  role contract. Moving only that catalog establishes a real Rooms assembly
  without encoding the current runtime cycles.
- Copy-based isolated-project synchronization preserves files that were moved
  or deleted in the source tree. Assembly-move verification must remove the
  exact stale mirror path or use a validated mirror sync before interpreting
  duplicate-type compiler errors as source defects.
- Survival ownership was split across `WildlifeModels.cs` and
  `DarkSurvivalModels.cs`. The stable save DTOs, enums, snapshots, and
  filth/water query contracts depend only on Unity primitives and the World
  grid contract, so they form a legal `DungeonStory.Survival` assembly.
  Actor-driven deprivation execution remains in the gameplay assembly until
  `CharacterActor` itself is separated from presentation/runtime bridges.
- Combat resolution inputs, outputs, equipment state, and attack verbs are a
  dependency-free domain kernel. The `Resources` catalog and equipment SO
  definitions are not: they remain in the gameplay assembly while the kernel
  moves to `DungeonStory.Combat`. The polymorphic attack verbs require
  `MovedFrom` because `CombatWeaponSO` stores them through
  `[SerializeReference]`.
- Invasion policy/save DTOs and threat tuning depend only on Foundation random
  contracts and Unity value types, while active engagements depend on
  `CharacterActor`, intruder MonoBehaviours, coroutines, and Grid movement.
  Extracting only the passive half creates a legal Invasion boundary without
  hiding those runtime dependencies inside the asmdef.
- Offense journey files combine pure route/supply/preparation models with
  runtime members, building stock categories, and target content generation.
  The pure graph and value-model subset can stand alone; route generation,
  supply-to-stock mapping, member state, reward eligibility, and battle
  sessions must remain outside until their dependencies are inverted.
- AI source has the same mixed-boundary shape: stable decision identifiers and
  serializable macro state are dependency-free, while decision contexts and
  runners currently depend on actors, work, buildings, and runtime services.
  Moving only the stable identifiers creates a real leaf assembly and avoids
  papering over the remaining cycles with broad references.
- Presentation can establish a real boundary without moving the whole UI:
  top-level tab IDs/catalog data and presenter registration contracts are
  domain-independent. View components and factories remain outside until
  their domain query/command dependencies are represented by assembly ports.
- Save API and implementation were coupled in one source file. Moving the V15
  root envelope and service contracts to Infrastructure Core makes the
  Foundation dependency explicit; making slot metadata immutable also removes
  cross-assembly mutation from the file-system implementation.
- Wildlife can own its serialized state independently of its current runtime
  coupling. State/intent/habitat IDs and save DTOs are a dependency-free leaf;
  species catalog loading, item definitions, actor behavior, and ecosystem
  execution still expose the real integration work instead of creating a
  broad Wildlife assembly that points back into Assembly-CSharp.
- Root namespaces in asmdef metadata do not migrate existing global types by
  themselves. The assembly graph is now explicit and cycle-checked, while
  namespace moves must stay part of the compatibility-facade phase to avoid
  breaking serialized Unity types in one unsafe batch.
- Runtime-created MonoBehaviours execute `Awake` before an explicit
  post-`Instantiate` injection pass. Injected services therefore cannot be
  required from `Awake`; local component binding belongs in `Awake`, while
  service-dependent initialization belongs in the injection method or a
  factory-owned activation step.
- A registered dispatcher is not a real extension boundary while serialized
  ability objects still implement an executable fallback interface. Removing
  that interface and failing fast for an unregistered marker ability prevents
  new modules from silently bypassing composition.
- The remaining compatibility risk is concentrated in the static
  `EventObserver` network: 28 publisher files and 41 subscriber files still
  use it. Replacing it safely requires lifecycle-aware scoped subscriptions,
  including runtime-created characters and panels; a blind textual conversion
  would lose enable/disable semantics. This remains part of the final
  compatibility checkpoint.
- A complete event-family migration is small enough to validate atomically:
  move its only publisher and every subscriber together, retain direct handler
  methods only as private callbacks, and let services own `IDisposable`
  subscriptions. Injected MonoBehaviours must subscribe both from injection
  and `OnEnable`, guarded by `isActiveAndEnabled`, because Unity can enable
  scene objects before VContainer performs method injection.
- The God-object line ratchet remains useful during cleanup: even structurally
  better lifecycle fields can expose that a monolith has no room left. The
  migration must preserve or lower the cap rather than normalize growth by
  increasing it.
- Not every global event requires a replacement bus message. Selection and
  refresh notifications whose publisher and consumer share one runtime
  lifetime are safer as instance C# events; diagnostics with no product
  consumer can be deleted. Reserve `IGameEventBus` for genuine cross-domain
  communication.
- Several legacy event types were write-only diagnostics: staff-discontent
  changes, research queue/progress, and economy refreshes had no consumers.
  Treating every old event as a compatibility contract would preserve global
  coupling without preserving any behavior.
- UI bound to one runtime should prefer that runtime's instance event even
  when the same result is also meaningful across domains. A scoped bus remains
  appropriate for codex/save/progression observers, but requiring it for a
  directly bound panel makes manually composed views unnecessarily fragile.
- A UI alert that must both select an actor and open a specific tab has two
  distinct responsibilities. Keeping the established actor-selection command
  and publishing only the tab request preserves click behavior while removing
  the global tab notification.
- Defense facilities are created through VContainer in product scenes but
  several characterization fixtures construct them through `GridBuildingFactory`
  and call `ConstructBuildableObject` manually. A new required runtime port
  must be supplied in every such explicit composition path; otherwise a green
  compile can still hide a scenario-time dependency failure.
- Long-running editor automation that creates its runner before scene
  injection should subscribe only after the gameplay `LifetimeScope` is ready.
  Resolving the scoped bus once from that verified scope is preferable to
  retaining a process-wide event solely for test instrumentation.
- UTF-8 BOM-prefixed C# files can reject `apply_patch` hunks anchored to the
  first `using` line even when terminal output looks identical. Stable
  interior declarations or fully qualified type names avoid rewriting file
  encoding and make the patch deterministic.
- Threat-warning consumers belong to different lifetimes: loadout preparation
  is an `IInitializable` service, meta progression is an enabled
  `MonoBehaviour`, and editor scenarios are manually composed worlds. A
  scoped event contract is safe only when each consumer owns disposal in its
  native lifecycle and fixtures inject the same bus into publisher and
  listener.
- An all-files `apply_patch` remains atomic when one later hunk misses its
  context; the rejected warning-event patch left every earlier file untouched.
  Large refactors should keep using file-sized patches so a minor context
  mismatch cannot stall unrelated verified edits.
- A verification clone must carry the same `Packages/manifest.json` and
  `packages-lock.json` as the source project. Copying only
  `Library/PackageCache` is temporary when the clone manifest omits those
  packages: UPM will correctly prune the restored directories on its next
  resolve and produce misleading missing NUnit, TMP, and UGUI compiler errors.
- Cross-domain event families should be migrated as a unit. Moving the final
  invasion result required all reset/report/progression consumers to use the
  same scoped bus; leaving even one static subscriber would split one logical
  world transition across two delivery domains and make ordering dependent on
  process history.
- Facility activity messages are not yet a safe event-only edit. Their
  publishers live on `BuildableObject` and `Shop`, which are constructed both
  by VContainer and by many explicit editor factories. The scoped bus port and
  every manual composition path must move in the same checkpoint.
- A required publication dependency hidden behind a protected helper is still
  exercised by manually composed fixtures. Running the implemented scenarios
  immediately after an event-family migration is valuable: compile-only
  verification cannot detect a factory that skipped method injection.

## Preserve

- Existing VContainer composition.
- `[SerializeReference]` building ability data and serialized IDs.
- BT for flow, Utility AI for scoring.
- Shared combat resolution contracts.
- Current dirty worktree and gameplay behavior as the migration baseline.
