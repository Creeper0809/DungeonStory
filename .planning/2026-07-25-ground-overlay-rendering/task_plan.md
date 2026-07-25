# Ground Overlay Rendering Fix

## Goal

Find and fix the red world-space surface that is covering the exterior ground,
restore the intended ground texture, and verify that temporary placement/debug
overlays cleanly follow their activation state.

## Checkpoints

- [x] Identify the renderer/overlay responsible for the red surface.
- [x] Fix its lifecycle, bounds, or sorting without changing the real terrain.
- [x] Add a focused regression for the failure mode.
- [x] Verify Unity compilation, PlayMode rendering, and Console state.

## Decisions

- Preserve the authored terrain and scene hierarchy changes already in the
  worktree.
- Use runtime renderer inventory and the last clean scene revision to
  distinguish authored terrain from serialized transient tile data.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
| Broad material GUID and scene-array search timed out | 1 | Split the searches and narrow each command to one artifact. |
| Broad exact-color search across all assets timed out | 1 | Stopped scanning unrelated assets after `HEAD` comparison proved the scene-copy regression. |
