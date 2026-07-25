# Progress

## 2026-07-26

- Created an isolated implementation plan for captivity, circus, wildlife
  capture, and door access.
- Confirmed Unity MCP currently rejects calls as `Connection revoked`.
- Began the baseline architecture and compile audit.
- Restored Unity MCP connectivity and verified a project refresh with Console
  `Error 0 / Warning 0`.
- Added per-door group and individual access policy state, presets,
  direct-command and escort overrides, actor-aware path filtering, final-step
  movement checks, lock indication, building-module persistence, and the door
  access information panel.
- Added the captivity domain model, registered interaction handlers, physical
  restraint reservation, stabilization, equipment confiscation, visible
  escort, secure-cell validation, policy thresholds, performer progression,
  and a V15 `captivity` save section.
- Added the entertainment room role, modular circus ability types, registered
  programs, show state machine, audience and performer movement, settlement,
  captured-wildlife state, a V15 `circus` save section, and a stage panel.
- Registered `work:warden` and `work:perform` through the work registry.
- Remaining vertical-slice work: character capture controls, operations
  controls, captive labor filtering, content assets, wildlife physical
  transport, and direct PlayMode verification.
- Generated and verified the captive restraint, circus stage, audience
  seating, and beast-pen BuildingSO assets.
- Added periodic captive security checks, physical escape attempts, false
  compliance betrayal, ransom settlement, retaliation pressure, and actual
  captured-wildlife transport.
- Connected captured wildlife to circus participant movement and expanded
  circus exchanges to use the shared combat resolver for human/wildlife
  pairings.
- Forced a Unity refresh after the transport/combat changes; project
  assemblies rebuilt successfully and the Console currently reports no
  errors or warnings.
- Added the dedicated captivity/circus/door-access contract suite and included
  it in the implemented-scenario runner. The suite currently passes.
- Added policy creation, duplication, editing, deletion, and assignment;
  physical interaction-material delivery; the wildlife capture command;
  captured-animal feeding, watering, escape pressure, and real escape paths.
- Added six optional circus facilities as separate ability modules and
  BuildingSO assets: ticket booth, gambling booth, announcer stand, hazard
  device, treatment area, and public-punishment device.
- Connected those facilities to preparation work, ticket and gambling revenue,
  audience satisfaction, accident risk and damage, filth, and staff witness
  mood. Cruel captivity outcomes now also increase the normal invasion threat
  runtime rather than existing only as UI text.
- Added door individual-subject search and an explicit exception-removal
  command while retaining absent IDs in the list.
- Added the ordinary world-click path from the compact building summary to the
  detailed building panel, making door permissions and circus controls
  reachable without debug-only entry points.
- Moved building context actions to a dedicated scrollable panel and expanded
  door permission row hit targets so labels and checkboxes behave as one
  control.
- Added and ran a real Input System pointer verifier for door selection and
  permissions at 1600x900 and 900x1600. The focused report passed with no
  Console errors or warnings.
- Extended the focused PlayMode verifier to resolve every new domain service
  from the actual gameplay LifetimeScope and validate all 10 generated
  captivity/circus BuildingSO assets and their ability modules.
- Reran the complete implemented-scenario regression after the UI fixes:
  32 passed, 0 failed. Final Unity Console audit: Error 0 / Warning 0.
