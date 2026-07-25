# Findings

- `GameplayArchitectureRatchetTests` uses suffix-based source lookup for most
  files, so preserving domain subpaths under MVC layers keeps most checks
  stable.
- Several asmdef existence checks use exact `Assets/Scripts/<domain>/...`
  paths and must be updated after moving asmdef folders.
- Moving files without changing namespaces/type names should preserve compile
  behavior. Moving `.asmdef` folders preserves assembly identity as long as
  the asmdef `name` remains unchanged.
- Root `Assets/Scripts` had legacy loose files (`Data.cs`, `GameData.cs`,
  `GameData.asset`, `GameManager.cs`). These were moved into MVC folders so the
  root no longer contains runtime scripts.
- One editor debug scenario had a literal old `Assets/Scripts/Rooms/RoomRole.cs`
  path. It was updated to `Assets/Scripts/Models/Rooms/Core/RoomRole.cs`.
- Full-`Assets` script GUID scans were noisy because imported demo scenes from
  DamageNumbersPro and TextMesh Pro referenced optional HDRP/post-processing
  scripts. Product scenes/prefabs/resources were clean, and those demo/example
  folders are now isolated outside `Assets`.
