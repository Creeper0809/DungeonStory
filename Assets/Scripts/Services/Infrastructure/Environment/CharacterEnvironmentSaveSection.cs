using System;
using System.Collections.Generic;

public sealed class CharacterEnvironmentSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCharacterEnvironmentSaveData,
        CharacterEnvironmentRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "environment.exposure";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        EnvironmentalFieldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly ICharacterEnvironmentPersistence persistence;

    public CharacterEnvironmentSaveSection(
        ICharacterEnvironmentPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonCharacterEnvironmentSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCharacterEnvironmentSaveData CapturePayload() =>
        persistence.Capture();

    protected override CharacterEnvironmentRestoreCandidate
        BuildRestoreCandidate(DungeonCharacterEnvironmentSaveData payload) =>
        persistence.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        CharacterEnvironmentRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}

public static class CharacterEnvironmentSaveValidation
{
    public static void Validate(
        DungeonCharacterEnvironmentSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null
            || payload.exposures == null
            || payload.equippedWorkwear == null)
        {
            report.AddError(
                "Character-environment payload or required collection is null.");
            return;
        }
        if (payload.version != DungeonCharacterEnvironmentSaveData.CurrentVersion)
        {
            report.AddError(
                $"Character-environment payload version {payload.version} is unsupported.");
        }

        HashSet<CharacterId> exposureCharacters = new();
        string previousCharacterId = null;
        foreach (CharacterEnvironmentExposure exposure in payload.exposures)
        {
            string rawCharacterId = exposure?.characterId ?? string.Empty;
            CharacterId characterId = new(rawCharacterId);
            if (exposure == null
                || !IsCanonical(characterId, rawCharacterId)
                || previousCharacterId != null
                    && string.CompareOrdinal(previousCharacterId, rawCharacterId) >= 0
                || !exposureCharacters.Add(characterId))
            {
                report.AddError(
                    "Character-environment exposures contain a null, non-canonical, duplicate, or unordered character ID.");
                continue;
            }
            previousCharacterId = rawCharacterId;

            if (!InExposureRange(exposure.coldExposure)
                || !InExposureRange(exposure.heatExposure)
                || !InExposureRange(exposure.airborneExposure)
                || !InExposureRange(exposure.visualStrain)
                || !IsFiniteNonNegative(exposure.criticalDamageTimer)
                || !Enum.IsDefined(
                    typeof(EnvironmentalExposureBand),
                    exposure.physiologicalBand)
                || !Enum.IsDefined(
                    typeof(EnvironmentalExposureBand),
                    exposure.visualBand))
            {
                report.AddError(
                    $"Character-environment exposure '{rawCharacterId}' contains invalid numeric or band state.");
            }
        }

        HashSet<CharacterId> equippedCharacters = new();
        HashSet<ItemInstanceId> equippedItems = new();
        previousCharacterId = null;
        foreach (EnvironmentalWorkwearSaveData equipped in payload.equippedWorkwear)
        {
            string rawCharacterId = equipped?.characterId ?? string.Empty;
            string rawItemId = equipped?.itemInstanceId ?? string.Empty;
            CharacterId characterId = new(rawCharacterId);
            ItemInstanceId itemId = (ItemInstanceId)rawItemId;
            if (equipped == null
                || !IsCanonical(characterId, rawCharacterId)
                || !itemId.IsValid
                || !string.Equals(itemId.Value, rawItemId, StringComparison.Ordinal)
                || previousCharacterId != null
                    && string.CompareOrdinal(previousCharacterId, rawCharacterId) >= 0
                || !equippedCharacters.Add(characterId)
                || !equippedItems.Add(itemId))
            {
                report.AddError(
                    "Character-environment workwear contains a null, non-canonical, duplicate, or unordered reference.");
                continue;
            }
            previousCharacterId = rawCharacterId;
        }
    }

    private static bool IsCanonical(CharacterId id, string raw) =>
        id.IsValid
        && string.Equals(id.Value, raw, StringComparison.Ordinal);

    private static bool InExposureRange(float value) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= 0f
        && value <= 100f;

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= 0f;
}
