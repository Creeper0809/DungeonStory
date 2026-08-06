using System;
using System.Collections.Generic;

public sealed class ModularFacilityWorldSaveSection :
    DungeonStrictJsonSaveSection<
        ModularFacilityWorldSaveData,
        ModularFacilityWorldRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.facilities";

    private readonly IModularFacilityWorldSaveService worldSaveService;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IGameSessionStateProvider gameDataProvider;

    public ModularFacilityWorldSaveSection(
        IModularFacilityWorldSaveService worldSaveService,
        IGridSystemProvider gridSystemProvider,
        IGameSessionStateProvider gameDataProvider)
    {
        this.worldSaveService = worldSaveService
            ?? throw new ArgumentNullException(nameof(worldSaveService));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 1;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.World;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        RunVariableSaveSection.Id,
        MetaProgressionSaveSection.Id
    };

    protected override ModularFacilityWorldSaveData CapturePayload()
    {
        ResolveWorld(out Grid grid, out GameSessionState gameData);
        return worldSaveService.CreateSnapshot(grid, gameData);
    }

    protected override void NormalizeRestorePayload(
        ModularFacilityWorldSaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        ModularFacilityWorldSaveData payload)
    {
        ResolveWorld(out Grid grid, out _);
        ModularFacilityWorldRestoreReport validation =
            worldSaveService.ValidateRestore(grid, payload);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                "Facility-world restore payload is invalid: "
                + string.Join(" | ", validation.errors));
        }
    }

    protected override ModularFacilityWorldRestoreCandidate
        BuildRestoreCandidate(ModularFacilityWorldSaveData source)
    {
        ResolveWorld(out Grid grid, out GameSessionState gameData);
        return worldSaveService.PrepareRestoreCandidate(
            grid,
            gameData,
            source);
    }

    protected override void PublishRestoreCandidate(
        ModularFacilityWorldRestoreCandidate candidate)
    {
        worldSaveService.StageRestoreCandidate(candidate);
    }

    private void ResolveWorld(out Grid grid, out GameSessionState gameData)
    {
        if (!gridSystemProvider.TryGetGrid(out grid))
        {
            throw new InvalidOperationException(
                "Cannot save or restore before the dungeon grid is initialized.");
        }

        if (!gameDataProvider.TryGetSessionState(out gameData))
        {
            throw new InvalidOperationException(
                "Cannot save or restore without active GameSessionState.");
        }
    }
}

public sealed class CharacterWorldSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCharacterWorldSaveData,
        CharacterWorldRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.world";

    private readonly ICharacterWorldSaveService saveService;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;

    public CharacterWorldSaveSection(
        ICharacterWorldSaveService saveService,
        IGridSystemProvider gridSystemProvider,
        IRestoreWorldCandidateQuery restoreWorldCandidates)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 1;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Characters;
    public override IReadOnlyList<string> DependsOn =>
        new[] { ModularFacilityWorldSaveSection.Id };

    protected override DungeonCharacterWorldSaveData CapturePayload()
    {
        return saveService.Capture(ResolveGrid());
    }

    protected override void NormalizeRestorePayload(
        DungeonCharacterWorldSaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        DungeonCharacterWorldSaveData payload)
    {
        saveService.ValidateRestorePayload(ResolveGrid(), payload);
    }

    protected override CharacterWorldRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterWorldSaveData source)
    {
        return saveService.PrepareRestoreCandidate(
            ResolveRestoreGrid(),
            source);
    }

    protected override void PublishRestoreCandidate(
        CharacterWorldRestoreCandidate candidate)
    {
        saveService.StageRestoreCandidate(candidate);
    }

    private Grid ResolveRestoreGrid()
    {
        if (!restoreWorldCandidates.TryGetGrid(out Grid candidateGrid))
        {
            throw new InvalidOperationException(
                "Character restore requires the detached facility-world candidate grid.");
        }

        return candidateGrid;
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
