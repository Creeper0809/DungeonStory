# Surgery System Progress

## 2026-07-29

- Started from a clean `main` worktree.
- Loaded Unity scripting, ScriptableObject, and persistent planning guidance.
- Confirmed the existing medical, body-health, research, stat, work, and building foundations.
- Created a dedicated implementation plan so the oversized historical root planning files remain untouched.
- Added 12-stat character growth with Medical, stable anatomy profiles, surgery orders, registered procedure effects, risk evaluation, and unique surgical parts.
- Authored 13 specialized medical facilities, 13 procedures, 3 anatomy profiles, prosthetic recipes, custom sprites, and the exact 78-node research branch.
- Connected surgery work, material hauling, pause-safe work progress, wildlife anatomy/effects, physical wildlife patient transport, policies, and V16 domain-section persistence.
- Added character health/surgery UI and a surgery facility panel with support facilities, sterility, progress, material delivery, organ capacity, power, and fuel state.
- Added shared surgery planning for captured wildlife and fresh humanoid/wildlife corpse stacks.
- Corrected captive consent detection and excluded captives from surgeon candidates.
- Added medical room names based on actual fixtures: infirmary, operating room, and transplant room.
- Enforced organ storage capacity and physical fuel consumption; organ preservation falls back to the normal spoilage rate when unpowered.
- Fixed wildlife installation subject IDs and failed-transport carry cleanup.
- Recompiled in Unity after each integration group; current compilation is successful.
- Verified the pointer-driven flow reaches patient admission and physical medicine delivery in PlayMode.
- Added current-speed movement and in-transit haul accounting fixes so the medicine is carried into the surgery facility buffer without duplicate haul replans.
- Invalidated incremental work-candidate scans on dynamic facility-state changes and made targeted workforce replans publish that dirty signal before selecting a worker.
- Completed the real AI surgery loop: admission, medicine request, physical hauling, surgeon reservation, cumulative work, outcome, and recovery.
- Recorded all crossed clinical stages so high work speed cannot erase incision, procedure, or suturing history.
- Fixed body capacity composition so severe legacy head, torso, arm, and leg injuries still cause the expected downed and work penalties.
- Fixed completed research restoration so current building and recipe unlocks are reapplied from the 78-node project catalog.
- Updated stale Combat, Research, modular facility, and full-save verification assumptions to the current registered architecture.
- Passed Surgery, Research Tree, V14 Combat, V16 save-section, and the broader feature regression batch with Console Error 0 / Warning 0.
- Passed full V16 save round trip: 559,263 JSON bytes, 138 buildings, 3 characters, 1 expedition, 1 intruder, and 0 warnings.
- Passed final pointer PlayMode verification with physical medicine consumption `6 -> 4`, work `12/12`, all surgery stages, patient recovery `0 -> 8`, and no captured errors or warnings.
- Visually inspected `Artifacts/QA/surgery-playmode.png`; the centered surgery window, Korean labels, risk details, and controls are nonblank and do not overlap.
- `git diff --check` passes.
