# Progress

- Started MVC folder-structure pass after V15 architecture refactor completion.
- Chosen target layout: `Models` for primitives/data contracts, `Views` for UI
  and presentation, `Controllers` for input and entry-point controllers, and
  `Services` for runtime domain systems and infrastructure.
- Moved the previous domain-root script folders under the MVC layer while
  preserving `.meta` GUIDs.
- Moved root `GameManager.cs` to `Controllers`, and root `Data.cs`,
  `GameData.cs`, and `GameData.asset` into `Models`.
- Added missing folder `.meta` files for new MVC parent directories.
- Updated exact asmdef path checks in `GameplayArchitectureRatchetTests`.
- Added `ProductScriptsUseMvcTopLevelFolders` to prevent old domain roots or
  loose root scripts from returning.
- Verification completed in the temp Unity clone:
  - Batch compile: passed.
  - `DungeonStory.Architecture.Tests`: 77/77 passed.
  - `ImplementedScenarioDebugRunner`: 30/30 passed.
- Isolated third-party demo/example content out of Unity's active `Assets`
  import tree:
  - `DamageNumbersPro/Demo`, `Demo C#`, and `Demo_Popup.prefab`
  - `TextMesh Pro/Examples & Extras`
  - New location: repository-level `ThirdPartySamples/`, preserving `.meta`
    files for future restoration.
- Rechecked `Assets` script references after isolation:
  - Missing script GUIDs: 0.
  - Explicit `m_Script fileID: 0`: 0.
  - Unity batch compile after isolation: passed.
