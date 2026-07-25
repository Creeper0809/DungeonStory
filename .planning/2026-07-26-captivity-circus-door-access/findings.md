# Findings

## Baseline

- `Door` and `InteriorDoor` currently affect visual sorting only; both are
  globally walkable and have no actor-aware policy state.
- `Grid.IsWalkable` deliberately treats doors as non-blocking. The new policy
  must therefore be layered onto path expansion and the final movement step,
  not implemented by globally toggling a collider.
- The V15 save system already supports independently owned section envelopes.
- The work registry has stable string IDs and two unused legacy bits at 18 and
  19, suitable for `work:warden` and `work:perform`.
- Building abilities use `[SerializeReference]`; new captive-housing and circus
  modules can remain static definitions while runtime state is kept elsewhere.
- The character summary health tab is generated at runtime and can host exact
  capture/labor controls without introducing another modal window.
- The operations surface supports a domain-owned section presenter; captivity
  controls can be appended without adding captivity decisions to the existing
  settlement command service.
- The work registry already owns candidate validation, so captive labor
  permission should be injected as a cross-cutting candidate policy rather
  than duplicated inside every work handler.
- The generated captivity building assets now load with their expected
  `[SerializeReference]` abilities, and the project compiles with Unity Console
  `Error 0 / Warning 0` after the wildlife-transport and circus-combat pass.
- `AbilityMove.StartExitDungeon()` deliberately rejects any actor that still
  has `AbilityWork`; released or ransomed labor captives therefore need a
  system-owned exit path instead of the visitor-only exit helper.
- Wildlife capture has a physical carrier/pen runtime but the wildlife info
  panel currently exposes only hunt commands, so the feature is not reachable
  through normal player UI yet.
- `RegisterAt()` unregisters the current wildlife grid occupant before
  registering the new cell, so transport recovery does not duplicate wildlife
  occupancy.
- Captive policies are currently immutable presets plus per-captive assignment;
  policy creation, cloning, renaming, deletion, and editable policy fields are
  not yet exposed by the public command API.
- Released captives should use a distinct system-owned exit operation. Reusing
  the ordinary visitor exit rule would continue to reject actors that retain
  `AbilityWork`, while deleting the work ability would corrupt later
  recruitment and minion conversion.
- The wildlife info panel can discover pens through the scoped
  `ICharacterAiWorldRegistry.Buildings` list; no scene scan or global manager
  is needed for the capture command.
- Recruitment and minion conversion currently check only captive thresholds;
  they must also honor the assigned policy's `allowRecruitment` and
  `allowCorruption` fields, just as ransom and performance already honor their
  policy flags.
- Managed captive-wildlife path creation was passing `EscortPass` directly in
  the traversal context. Because direct override kinds bypass policy checks,
  path planning could ignore a real pass registration. Managed movement must
  use the subject's active temporary pass instead.
- The circus `CleanupAndTreatment` phase completed immediately. The existing
  filth work target and medical-order runtimes can own the real aftermath:
  shows create world filth, then remain in post-processing until cleaning and
  active treatment orders finish.
- The original circus pass covered only stage, seating, and beast pen.
  Optional venue parts needed individual ability modules and snapshot fields
  so destroying or editing a venue during a show would not reroll settlement.
- Escape and ransom already emitted domain events; subscribing a small
  retaliation bridge to the existing invasion threat provider preserves the
  real invasion director instead of inventing a parallel rescue-raid manager.
- Door individual exceptions were persisted correctly, including absent IDs,
  but the panel lacked search and explicit removal. Both can remain
  presentation-only state without adding search data to the door save module.
- Normal building selection originally stopped at the compact summary panel,
  so door and circus actions were not reachable through ordinary world
  pointer input. The summary now exposes a `상세` command through scoped scene
  references and opens the existing building information surface.
- Appending door actions directly to the legacy building-info grid pushed
  controls outside the viewport. Context actions now use their own scrollable
  vertical panel, and the whole permission row is an exact clickable target
  rather than only the small checkbox graphic.
- The final pointer verifier passed at both 1600x900 and 900x1600. It confirmed
  exact door selection, captive-group toggling, UI-to-world click blocking,
  lock-indicator presence, and staff/all-access presets.
- The same PlayMode verifier now resolves the live gameplay container and
  confirms the captivity, circus, wildlife-capture, and door-access services.
  It also loads all 10 generated facility assets and verifies that every asset
  retains at least one serialized ability module.
- The final implemented-scenario run passed all 32 suites, including the
  captivity/circus/door contracts, with Unity Console Error 0 / Warning 0.
