using System;
using System.Collections.Generic;

public sealed class SurvivalResourcesSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonSurvivalSaveData,
        SurvivalFoodRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "survival.resources";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        WildlifeSaveSection.Id
    };
    private readonly ISurvivalFoodPersistence runtime;

    public SurvivalResourcesSaveSection(ISurvivalFoodPersistence runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonSurvivalSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonSurvivalSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report) =>
        V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override SurvivalFoodRestoreCandidate BuildRestoreCandidate(
        DungeonSurvivalSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        SurvivalFoodRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}

public sealed class DarkSurvivalSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonDarkSurvivalSaveData,
        DarkSurvivalRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "survival.deprivation";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        SurvivalResourcesSaveSection.Id
    };
    private readonly ICharacterDeprivationPersistence runtime;

    public DarkSurvivalSaveSection(ICharacterDeprivationPersistence runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonDarkSurvivalSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonDarkSurvivalSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        DungeonDarkSurvivalSaveData payload,
        DungeonGameRestoreReport report) =>
        V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        DungeonDarkSurvivalSaveData payload) =>
        CharacterDeprivationPersistenceCoordinator.ValidatePayloadShape(payload);

    protected override DarkSurvivalRestoreCandidate BuildRestoreCandidate(
        DungeonDarkSurvivalSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        DarkSurvivalRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
