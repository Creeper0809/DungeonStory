# Assembly Migration Planner

This independent Roslyn CLI reads the newest Unity Bee `Assembly-CSharp.rsp`, binds the same source files and metadata references as Unity, and emits a deterministic file dependency graph for first-party `Assets/Scripts` sources still compiled into `Assembly-CSharp`.

Run the semantic self-test:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/AssemblyMigrationPlanner/Run-AssemblyMigrationPlanner.ps1 -SelfTest
```

Generate `Library/AssemblyMigrationPlanner/assembly-migration-plan.json`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/AssemblyMigrationPlanner/Run-AssemblyMigrationPlanner.ps1
```

Use `-ResponseFile` or `-ReportPath` to override either path. If no Bee response file exists, the analyzer falls back to project sources selected by nearest `.asmdef`/`.asmref` ownership and metadata from Unity/`Library/ScriptAssemblies`.

`files` contains declared types, semantic incoming/outgoing file references, boundary references, SCC membership, and leaf status. `migrationBatches` orders sink SCCs first so dependencies are moved before their dependents; a cyclic batch must move as a unit or be split deliberately. Hash fields are calculated from canonical ordinal sequences, and the report contains no timestamp, so identical inputs produce byte-identical JSON.
