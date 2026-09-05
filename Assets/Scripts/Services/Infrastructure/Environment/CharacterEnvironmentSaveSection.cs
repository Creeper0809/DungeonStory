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
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    private readonly ICharacterEnvironmentPersistence persistence;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;
    private readonly ApparelRejectedDismantleRestoreGuard
        rejectedDismantleRestoreGuard;
    private readonly IApparelOutputDetachedCapacityRestoreGuard
        outputDetachedCapacityRestoreGuard;

    public CharacterEnvironmentSaveSection(
        ICharacterEnvironmentPersistence persistence,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates,
        ApparelRejectedDismantleRestoreGuard rejectedDismantleRestoreGuard,
        IApparelOutputDetachedCapacityRestoreGuard
            outputDetachedCapacityRestoreGuard)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
        this.rejectedDismantleRestoreGuard = rejectedDismantleRestoreGuard
            ?? throw new ArgumentNullException(
                nameof(rejectedDismantleRestoreGuard));
        this.outputDetachedCapacityRestoreGuard =
            outputDetachedCapacityRestoreGuard
            ?? throw new ArgumentNullException(
                nameof(outputDetachedCapacityRestoreGuard));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonCharacterEnvironmentSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCharacterEnvironmentSaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        DungeonCharacterEnvironmentSaveData payload,
        DungeonGameRestoreReport report) =>
        V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override CharacterEnvironmentRestoreCandidate
        BuildRestoreCandidate(DungeonCharacterEnvironmentSaveData payload)
    {
        rejectedDismantleRestoreGuard.Validate(payload.apparelWorkOrders);
        outputDetachedCapacityRestoreGuard.Validate(
            payload.apparelWorkOrders,
            payload.apparelWorkOrderTerminalStates);
        return persistence.BuildRestoreCandidate(payload);
    }

    /// <summary>
    /// Section preflight owns only the character-environment payload shape.
    /// Cross-aggregate apparel joins require the detached Physical Items and
    /// facility-world candidates, which are published by dependency-ordered
    /// staging and are deliberately unavailable during registry preflight.
    /// </summary>
    protected override void ValidateParsedPayload(
        DungeonCharacterEnvironmentSaveData payload)
    {
        DungeonGameRestoreReport report = new();
        CharacterEnvironmentSaveValidation.Validate(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(string.Join(" | ", report.Errors));
        }
    }

    protected override void PublishRestoreCandidate(
        CharacterEnvironmentRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);

    protected override void PublishRestoreCandidateProjection(
        DungeonCharacterEnvironmentSaveData payload,
        CharacterEnvironmentRestoreCandidate candidate) =>
        lifecycleRestoreCandidates.SetEnvironment(payload);
}

public static class CharacterEnvironmentSaveValidation
{
    public static void Validate(
        DungeonCharacterEnvironmentSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null
            || payload.exposures == null
            || payload.equippedWorkwear == null
            || payload.equippedApparel == null
            || payload.apparelWorkOrders == null
            || payload.apparelWorkOrderTerminalStates == null)
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

        HashSet<ItemInstanceId> apparelItems = new();
        string previousApparelKey = null;
        foreach (EquippedApparelSaveData equipped in payload.equippedApparel)
        {
            string rawCharacterId = equipped?.characterId ?? string.Empty;
            string rawItemId = equipped?.itemInstanceId ?? string.Empty;
            CharacterId characterId = new(rawCharacterId);
            ItemInstanceId itemId = (ItemInstanceId)rawItemId;
            string apparelId = equipped?.apparelDefinitionId?.Trim() ?? string.Empty;
            string key = equipped == null
                ? string.Empty
                : $"{rawCharacterId}\u001f{(int)equipped.layer:D2}\u001f{equipped.occupiedPoints:D10}\u001f{rawItemId}";
            if (equipped == null
                || !IsCanonical(characterId, rawCharacterId)
                || !itemId.IsValid
                || !string.Equals(itemId.Value, rawItemId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(apparelId)
                || !Enum.IsDefined(typeof(ApparelLayer), equipped.layer)
                || equipped.occupiedPoints == 0u
                || previousApparelKey != null
                    && string.CompareOrdinal(previousApparelKey, key) >= 0
                || !apparelItems.Add(itemId))
            {
                report.AddError(
                    "Character-environment apparel contains a null, non-canonical, duplicate, unordered, or invalid slot reference.");
                continue;
            }
            previousApparelKey = key;
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
