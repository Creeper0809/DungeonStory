# Captivity, Circus, And Door Access

## Goal

Implement the V15 captivity, forced-labor, circus, wildlife-capture, and
per-door access-control feature set without undoing the current architecture
refactor or unrelated dirty work.

## Phases

- [x] Establish a clean local compile baseline and map current door, movement,
      work, room, UI, and save extension points.
- [x] Implement per-door access policy, group/individual precedence, presets,
      direct-command and escort overrides, path filtering, movement recheck,
      persistence, and tests.
- [x] Add stable warden/perform work IDs and the captivity domain runtime,
      capture/escort/cell security, policies, registered interactions, labor
      eligibility, recruitment/corruption outcomes, persistence, and tests.
- [x] Add entertainment room role, modular circus abilities, show state
      machine, audience movement, lethality policies, performer progression,
      wildlife capture, persistence, and tests.
- [x] Connect building/operations UI surfaces and exact pointer commands.
- [x] Run local compilation, Unity EditMode/PlayMode regressions, camera/screen
      captures, and a final Console error/warning audit.

## Constraints

- Shared ScriptableObjects contain static authoring data only.
- Runtime reservations, progress, captive state, show state, and door policy
  state live in scoped services or per-instance save data.
- New open-ended behavior uses registered handlers, not central switch growth.
- Existing V15 save sections remain independently versioned.
- Existing dirty worktree changes are preserved.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
| Unity MCP returned `Connection revoked` for editor state and Console | 1 | Continue with local compilation; retry editor verification after the connection is restored. |
| PowerShell `Join-Path` was called with three positional path segments | 1 | Compose the plan directory first, then append each file name. |
