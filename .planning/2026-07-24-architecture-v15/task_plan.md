# DungeonStory Architecture V15

## Goal

Preserve current gameplay while replacing global runtime access, open-ended
dispatch, monolithic save orchestration, and domain/UI coupling with scoped,
registered services and domain-owned modules.

## Checkpoints

- [x] Capture source and performance baselines without mutating gameplay assets.
- [x] Add Foundation contracts for clocks, random streams, events, registries,
      and path search.
- [x] Register Foundation services in the gameplay composition root.
- [x] Convert the first runtime vertical slice away from static `Active` and
      scene hierarchy scans.
- [x] Replace work-type execution and availability conditionals with registered
      work handlers and policies.
- [x] Separate building ability configuration from runtime execution.
- [x] Split the first item, wildlife, and defense runtime responsibilities into
      repositories, planners, carcass/survival services, combat execution, and
      engagement state storage.
- [x] Replace forwarding UI presenters with domain-owned presenters.
- [x] Introduce V15 sectioned saves and reject pre-V15 saves.
- [x] Complete product assembly boundaries for all fifteen V15 domains and
      enforce the dependency graph with NUnit architecture ratchets.
- [x] Remove public compatibility facades and lock dependency direction.

## Working Rules

- Keep every checkpoint compiling.
- Do not revert unrelated or pre-existing worktree changes.
- Keep ScriptableObjects limited to static configuration.
- Allow `switch` only for closed state machines and exhaustive display mapping.
- Prefer registered handlers for work, ability, UI, and save extension points.
- Validate each vertical slice in Unity before moving to the next one.

## Completion Note

Required closed/serialized primitives such as `BuildingCategory`,
`FacilityRole`, `FacilityWorkType`, and `StockCategory` remain as enums where
they preserve existing Unity assets or model closed concepts. `FacilityWorkType`
is no longer a public extension API: new work extension goes through stable
`WorkTypeId` registration and registered handlers, while the legacy enum is
internal compatibility for existing serialized masks and in-assembly bridges.
