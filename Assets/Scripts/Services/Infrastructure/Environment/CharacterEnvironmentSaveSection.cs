using System;
using System.Collections.Generic;
using System.Linq;

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
            || payload.apparelPolicies == null
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

        HashSet<CharacterId> apparelCharacters = new(
            payload.equippedApparel
                .Where(value => value != null)
                .Select(value => new CharacterId(value.characterId)));
        HashSet<CharacterId> policyCharacters = new();
        previousCharacterId = null;
        foreach (CharacterApparelPolicySaveData policy in payload.apparelPolicies)
        {
            string rawCharacterId = policy?.characterId ?? string.Empty;
            CharacterId characterId = new(rawCharacterId);
            if (policy == null
                || !IsCanonical(characterId, rawCharacterId)
                || previousCharacterId != null
                    && string.CompareOrdinal(previousCharacterId, rawCharacterId) >= 0
                || !policyCharacters.Add(characterId)
                || !Enum.IsDefined(
                    typeof(ApparelSelectionPurpose),
                    policy.purpose)
                || policy.directPreferences == null)
            {
                report.AddError(
                    "Character apparel policies contain a null, non-canonical, duplicate, unordered, or invalid row.");
                continue;
            }
            previousCharacterId = rawCharacterId;

            string previousPreferenceKey = null;
            foreach (ApparelDirectPreferenceSaveData preference in
                         policy.directPreferences)
            {
                string rawItemId = preference?.itemInstanceId ?? string.Empty;
                ItemInstanceId itemId = (ItemInstanceId)rawItemId;
                string key = preference == null
                    ? string.Empty
                    : $"{(int)preference.purpose:D2}\u001f{(int)preference.layer:D2}\u001f{preference.occupiedPoints:D10}\u001f{rawItemId}";
                if (preference == null
                    || !Enum.IsDefined(
                        typeof(ApparelSelectionPurpose),
                        preference.purpose)
                    || !Enum.IsDefined(typeof(ApparelLayer), preference.layer)
                    || preference.occupiedPoints == 0u
                    || !itemId.IsValid
                    || !string.Equals(
                        itemId.Value,
                        rawItemId,
                        StringComparison.Ordinal)
                    || previousPreferenceKey != null
                        && string.CompareOrdinal(previousPreferenceKey, key) >= 0)
                {
                    report.AddError(
                        $"Character apparel policy '{rawCharacterId}' contains an invalid or unordered direct preference.");
                    continue;
                }
                previousPreferenceKey = key;
            }

            ApparelTemporaryOverrideSaveData temporary = policy.temporaryOverride;
            if (!policy.hasTemporaryOverride)
            {
                if (!IsEmptyTemporaryOverrideCarrier(temporary))
                {
                    report.AddError(
                        $"Character apparel policy '{rawCharacterId}' contains temporary override data without presence authority.");
                }
                continue;
            }
            ItemInstanceId overrideId =
                (ItemInstanceId)temporary?.itemInstanceId;
            if (temporary == null
                || !overrideId.IsValid
                || !string.Equals(
                    overrideId.Value,
                    temporary.itemInstanceId,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(temporary.source)
                || temporary.displaced == null
                || !apparelItems.Contains(overrideId))
            {
                report.AddError(
                    $"Character apparel policy '{rawCharacterId}' contains an invalid temporary override.");
                continue;
            }
            string previousDisplacedKey = null;
            HashSet<ItemInstanceId> displacedIds = new();
            foreach (EquippedApparelSaveData displaced in temporary.displaced)
            {
                string rawItemId = displaced?.itemInstanceId ?? string.Empty;
                ItemInstanceId itemId = (ItemInstanceId)rawItemId;
                string key = displaced == null
                    ? string.Empty
                    : $"{(int)displaced.layer:D2}\u001f{displaced.occupiedPoints:D10}\u001f{rawItemId}";
                if (displaced == null
                    || !string.Equals(
                        displaced.characterId,
                        rawCharacterId,
                        StringComparison.Ordinal)
                    || !itemId.IsValid
                    || !string.Equals(itemId.Value, rawItemId, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(displaced.apparelDefinitionId)
                    || !Enum.IsDefined(typeof(ApparelLayer), displaced.layer)
                    || displaced.occupiedPoints == 0u
                    || previousDisplacedKey != null
                        && string.CompareOrdinal(previousDisplacedKey, key) >= 0
                    || !displacedIds.Add(itemId))
                {
                    report.AddError(
                        $"Character apparel policy '{rawCharacterId}' contains an invalid temporary displaced item.");
                    continue;
                }
                previousDisplacedKey = key;
            }
        }
        if (!apparelCharacters.IsSubsetOf(policyCharacters))
        {
            report.AddError(
                "Every equipped-apparel character requires one current-format apparel policy row.");
        }
    }

    private static bool IsEmptyTemporaryOverrideCarrier(
        ApparelTemporaryOverrideSaveData value) =>
        value == null
        || string.IsNullOrEmpty(value.itemInstanceId)
        && string.IsNullOrEmpty(value.source)
        && (value.displaced == null || value.displaced.Length == 0);

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
