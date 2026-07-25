using System;
using System.Collections.Generic;

public sealed class ModularFacilityWorldSaveSection :
    DungeonJsonSaveSection<ModularFacilityWorldSaveData>
{
    public const string Id = "world.facilities";

    private readonly IModularFacilityWorldSaveService worldSaveService;
    private readonly ICharacterWorldSaveService characterWorldSaveService;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IGameDataProvider gameDataProvider;

    public ModularFacilityWorldSaveSection(
        IModularFacilityWorldSaveService worldSaveService,
        ICharacterWorldSaveService characterWorldSaveService,
        IGridSystemProvider gridSystemProvider,
        IGameDataProvider gameDataProvider)
    {
        this.worldSaveService = worldSaveService
            ?? throw new ArgumentNullException(nameof(worldSaveService));
        this.characterWorldSaveService = characterWorldSaveService
            ?? throw new ArgumentNullException(nameof(characterWorldSaveService));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.World;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        RunVariableSaveSection.Id,
        MetaProgressionSaveSection.Id
    };

    protected override ModularFacilityWorldSaveData CapturePayload()
    {
        ResolveWorld(out Grid grid, out GameData gameData);
        return worldSaveService.CreateSnapshot(grid, gameData);
    }

    protected override void RestorePayload(
        ModularFacilityWorldSaveData source,
        DungeonGameRestoreReport report)
    {
        ResolveWorld(out Grid grid, out GameData gameData);
        characterWorldSaveService.PrepareForWorldRestore();
        if (!worldSaveService.TryRestoreSnapshot(
                grid,
                gameData,
                source,
                out ModularFacilityWorldRestoreReport worldReport))
        {
            foreach (string error in worldReport.errors)
            {
                report.AddError(error);
            }
        }

        report.RecordRestoredBuildings(worldReport.restoredCount);
        foreach (string warning in worldReport.warnings)
        {
            report.AddWarning(warning);
        }
    }

    private void ResolveWorld(out Grid grid, out GameData gameData)
    {
        if (!gridSystemProvider.TryGetGrid(out grid))
        {
            throw new InvalidOperationException(
                "Cannot save or restore before the dungeon grid is initialized.");
        }

        if (!gameDataProvider.TryGetGameData(out gameData))
        {
            throw new InvalidOperationException(
                "Cannot save or restore without active GameData.");
        }
    }
}

public sealed class CharacterWorldSaveSection :
    DungeonJsonSaveSection<DungeonCharacterWorldSaveData>
{
    public const string Id = "characters.world";

    private readonly ICharacterWorldSaveService saveService;
    private readonly IGridSystemProvider gridSystemProvider;

    public CharacterWorldSaveSection(
        ICharacterWorldSaveService saveService,
        IGridSystemProvider gridSystemProvider)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Characters;
    public override IReadOnlyList<string> DependsOn =>
        new[] { ModularFacilityWorldSaveSection.Id };

    protected override DungeonCharacterWorldSaveData CapturePayload()
    {
        return saveService.Capture(ResolveGrid());
    }

    protected override void RestorePayload(
        DungeonCharacterWorldSaveData source,
        DungeonGameRestoreReport report)
    {
        report.RecordRestoredCharacters(
            saveService.Restore(ResolveGrid(), source, report));
    }

    private Grid ResolveGrid()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            throw new InvalidOperationException(
                "Cannot save or restore characters before the grid is initialized.");
        }

        return grid;
    }
}
