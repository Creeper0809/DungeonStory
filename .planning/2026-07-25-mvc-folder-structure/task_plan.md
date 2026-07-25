# DungeonStory MVC Folder Structure

## Goal

Reorganize `Assets/Scripts` into a conventional MVC-oriented layout while
preserving Unity `.meta` GUIDs, existing asmdef assembly names, and current
gameplay behavior.

## Checkpoints

- [x] Inspect current script structure and asmdef/path-sensitive tests.
- [x] Move scripts into `Models`, `Views`, `Controllers`, and `Services`
      without changing type names or assemblies.
- [x] Update architecture ratchets to recognize the MVC layout.
- [x] Verify Unity compile and architecture tests.

## Decisions

- Keep domain folder names under the MVC layer, e.g. `Models/Buildings/Core`
  and `Services/Buildings`, so suffix-based source checks and developer search
  remain readable.
- Keep asmdef names unchanged. Moving an asmdef folder is allowed as long as
  the assembly name remains stable.
- Keep `.meta` files paired with moved files/directories to preserve Unity
  asset GUIDs.
- Allow only `Controllers`, `Editor`, `Models`, `Services`, and `Views` as
  `Assets/Scripts` top-level directories. `Editor` remains top-level for Unity
  editor-only tooling.
