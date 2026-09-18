using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct ApparelFitAssessment
{
    public ApparelFitAssessment(
        ApparelBodyForm bodyForm,
        ApparelSizeClass wearerSize,
        AnatomyAttachmentPoint availablePoints,
        ApparelModificationKind unusedOpenings,
        bool adjacentSize)
    {
        BodyForm = bodyForm;
        WearerSize = wearerSize;
        AvailablePoints = availablePoints;
        UnusedOpenings = unusedOpenings;
        AdjacentSize = adjacentSize;
    }

    public ApparelBodyForm BodyForm { get; }
    public ApparelSizeClass WearerSize { get; }
    public AnatomyAttachmentPoint AvailablePoints { get; }
    public ApparelModificationKind UnusedOpenings { get; }
    public bool AdjacentSize { get; }
}

public interface IAnatomyAttachmentQuery
{
    bool CanEquip(
        CharacterId characterId,
        ApparelDefinitionSO definition,
        ApparelInstanceState instance,
        out ApparelFitAssessment assessment,
        out DomainFailure failure);
    AnatomyAttachmentPoint GetAvailablePoints(CharacterId characterId);
    ApparelBodyForm GetBodyForm(CharacterId characterId);
    ApparelSizeClass GetSize(CharacterId characterId);
}

public sealed class AnatomyAttachmentQuery : IAnatomyAttachmentQuery
{
    private const AnatomyAttachmentPoint StandardHumanoidPoints =
        AnatomyAttachmentPoint.Head
        | AnatomyAttachmentPoint.Face
        | AnatomyAttachmentPoint.Neck
        | AnatomyAttachmentPoint.Torso
        | AnatomyAttachmentPoint.Pelvis
        | AnatomyAttachmentPoint.Arms
        | AnatomyAttachmentPoint.Hands
        | AnatomyAttachmentPoint.Legs
        | AnatomyAttachmentPoint.Feet
        | AnatomyAttachmentPoint.Back;

    private readonly ICharacterWorldQuery characters;
    private readonly IAnatomyHealthRuntime anatomy;

    public AnatomyAttachmentQuery(
        ICharacterWorldQuery characters,
        IAnatomyHealthRuntime anatomy)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
    }

    public bool CanEquip(
        CharacterId characterId,
        ApparelDefinitionSO definition,
        ApparelInstanceState instance,
        out ApparelFitAssessment assessment,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        assessment = default;
        if (!characterId.IsValid || FindActor(characterId) == null)
        {
            failure = new DomainFailure(FailureCode.ApparelCharacterMissing, characterId.Value);
            return false;
        }
        if (definition == null || instance == null)
        {
            failure = new DomainFailure(FailureCode.ApparelDefinitionMissing);
            return false;
        }

        ApparelBodyForm bodyForm = GetBodyForm(characterId);
        if (definition.BodyForm != ApparelBodyForm.Any
            && definition.BodyForm != bodyForm)
        {
            failure = new DomainFailure(
                FailureCode.ApparelBodyFormIncompatible,
                definition.ApparelId,
                bodyForm.ToString());
            return false;
        }

        AnatomyAttachmentPoint points = GetAvailablePoints(characterId);
        if ((points & definition.RequiredPoints) != definition.RequiredPoints)
        {
            failure = new DomainFailure(
                FailureCode.ApparelAttachmentMissing,
                definition.ApparelId,
                (definition.RequiredPoints & ~points).ToString());
            return false;
        }

        ApparelSizeClass wearerSize = GetSize(characterId);
        int distance = Math.Abs((int)wearerSize - (int)instance.size);
        bool adjacent = distance == 1;
        if (definition.FitMode == ApparelFitMode.Sized && distance != 0
            || definition.FitMode == ApparelFitMode.Adjustable && distance > 1)
        {
            failure = new DomainFailure(
                FailureCode.ApparelSizeIncompatible,
                definition.ApparelId,
                instance.size.ToString(),
                wearerSize.ToString());
            return false;
        }

        ApparelModificationKind requiredModification = RequiredOpenings(
            definition.SealedOptionalPoints & points);
        ApparelModificationKind openModifications =
            instance.modifications & ~instance.closedOpenings;
        if ((openModifications & requiredModification) != requiredModification)
        {
            failure = new DomainFailure(
                FailureCode.ApparelModificationRequired,
                definition.ApparelId,
                (requiredModification & ~openModifications).ToString());
            return false;
        }

        ApparelModificationKind unused = openModifications
            & RequiredOpenings(~points & AnatomyAttachmentPoint.OptionalAppendages);
        assessment = new ApparelFitAssessment(
            bodyForm,
            wearerSize,
            points,
            unused,
            definition.FitMode == ApparelFitMode.Adjustable && adjacent);
        return true;
    }

    public AnatomyAttachmentPoint GetAvailablePoints(CharacterId characterId)
    {
        CharacterActor actor = FindActor(characterId);
        if (actor == null)
        {
            return AnatomyAttachmentPoint.None;
        }

        // Every biological resident, including slime, uses the humanoid apparel
        // surface. Existing anatomy nodes only remove a standard point when that
        // exact surface node exists and is missing.
        AnatomyAttachmentPoint result = StandardHumanoidPoints;
        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(characterId.Value);
        result = ApplyStandardNode(result, snapshot, "head", AnatomyAttachmentPoint.Head | AnatomyAttachmentPoint.Face);
        result = ApplyStandardNode(result, snapshot, "torso", AnatomyAttachmentPoint.Torso | AnatomyAttachmentPoint.Pelvis | AnatomyAttachmentPoint.Back);
        result = ApplyStandardNode(result, snapshot, "arm:left", AnatomyAttachmentPoint.ArmLeft | AnatomyAttachmentPoint.HandLeft);
        result = ApplyStandardNode(result, snapshot, "arm:right", AnatomyAttachmentPoint.ArmRight | AnatomyAttachmentPoint.HandRight);
        result = ApplyStandardNode(result, snapshot, "leg:left", AnatomyAttachmentPoint.LegLeft | AnatomyAttachmentPoint.FootLeft);
        result = ApplyStandardNode(result, snapshot, "leg:right", AnatomyAttachmentPoint.LegRight | AnatomyAttachmentPoint.FootRight);

        if (HasFunctionalNode(snapshot, "tail")
            || HasFunctionalNode(snapshot, "balance-tail"))
        {
            result |= AnatomyAttachmentPoint.Tail;
        }
        if (HasFunctionalNode(snapshot, "wing:left"))
        {
            result |= AnatomyAttachmentPoint.WingLeft;
        }
        if (HasFunctionalNode(snapshot, "wing:right"))
        {
            result |= AnatomyAttachmentPoint.WingRight;
        }
        if (HasFunctionalNode(snapshot, "horn:left")
            || HasFunctionalNode(snapshot, "horn:right")
            || HasFunctionalNode(snapshot, "horn-set"))
        {
            result |= AnatomyAttachmentPoint.HornSet;
        }

        return result;
    }

    public ApparelBodyForm GetBodyForm(CharacterId characterId)
    {
        CharacterActor actor = FindActor(characterId);
        return string.Equals(actor?.SpeciesTag, "Golem", StringComparison.OrdinalIgnoreCase)
            ? ApparelBodyForm.Construct
            : ApparelBodyForm.Humanoid;
    }

    public ApparelSizeClass GetSize(CharacterId characterId)
    {
        string species = FindActor(characterId)?.SpeciesTag?.Trim() ?? string.Empty;
        if (string.Equals(species, "Kobold", StringComparison.OrdinalIgnoreCase))
        {
            return ApparelSizeClass.Small;
        }
        if (string.Equals(species, "Orc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(species, "Golem", StringComparison.OrdinalIgnoreCase))
        {
            return ApparelSizeClass.Large;
        }
        return ApparelSizeClass.Medium;
    }

    private CharacterActor FindActor(CharacterId id) => characters.Characters
        .FirstOrDefault(actor => CharacterPersistentIdentity.TryGet(actor, out CharacterId found)
            && found.Equals(id));

    private static AnatomyAttachmentPoint ApplyStandardNode(
        AnatomyAttachmentPoint current,
        AnatomyHealthSnapshot snapshot,
        string nodeId,
        AnatomyAttachmentPoint points)
    {
        AnatomyNodeHealthState node = snapshot.Nodes.FirstOrDefault(value =>
            value != null && string.Equals(value.nodeId, nodeId, StringComparison.Ordinal));
        return node != null && node.missing
            ? current & ~points
            : current;
    }

    private static bool HasFunctionalNode(AnatomyHealthSnapshot snapshot, string nodeId) =>
        snapshot.Nodes.Any(value => value != null
            && !value.missing
            && string.Equals(value.nodeId, nodeId, StringComparison.Ordinal));

    private static ApparelModificationKind RequiredOpenings(AnatomyAttachmentPoint points)
    {
        ApparelModificationKind result = ApparelModificationKind.None;
        if ((points & AnatomyAttachmentPoint.Tail) != 0)
        {
            result |= ApparelModificationKind.TailOpening;
        }
        if ((points & AnatomyAttachmentPoint.Wings) != 0)
        {
            result |= ApparelModificationKind.WingSlits;
        }
        if ((points & AnatomyAttachmentPoint.HornSet) != 0)
        {
            result |= ApparelModificationKind.HornClearance;
        }
        return result;
    }
}

[Serializable]
public sealed class EquippedApparelSaveData
{
    public string characterId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string apparelDefinitionId = string.Empty;
    public ApparelLayer layer;
    public uint occupiedPoints;
}

[Serializable]
public sealed class ApparelDirectPreferenceSaveData
{
    public ApparelSelectionPurpose purpose;
    public ApparelLayer layer;
    public uint occupiedPoints;
    public string itemInstanceId = string.Empty;
}

[Serializable]
public sealed class ApparelTemporaryOverrideSaveData
{
    public string itemInstanceId = string.Empty;
    public string source = string.Empty;
    public EquippedApparelSaveData[] displaced;
}

[Serializable]
public sealed class CharacterApparelPolicySaveData
{
    public string characterId = string.Empty;
    public ApparelSelectionPurpose purpose;
    public ApparelDirectPreferenceSaveData[] directPreferences;
    public bool hasTemporaryOverride;
    public ApparelTemporaryOverrideSaveData temporaryOverride;
}

public readonly struct EquippedApparelSnapshot
{
    public EquippedApparelSnapshot(
        CharacterId characterId,
        ItemInstanceId itemInstanceId,
        string apparelDefinitionId,
        ApparelLayer layer,
        AnatomyAttachmentPoint occupiedPoints)
    {
        CharacterId = characterId;
        ItemInstanceId = itemInstanceId;
        ApparelDefinitionId = apparelDefinitionId ?? string.Empty;
        Layer = layer;
        OccupiedPoints = occupiedPoints;
    }

    public CharacterId CharacterId { get; }
    public ItemInstanceId ItemInstanceId { get; }
    public string ApparelDefinitionId { get; }
    public ApparelLayer Layer { get; }
    public AnatomyAttachmentPoint OccupiedPoints { get; }
}

public enum ApparelSelectionPurpose
{
    Daily = 0,
    Work = 1
}

public readonly struct ApparelDirectPreferenceSnapshot
{
    public ApparelDirectPreferenceSnapshot(
        ApparelSelectionPurpose purpose,
        ApparelLayer layer,
        AnatomyAttachmentPoint occupiedPoints,
        ItemInstanceId itemInstanceId)
    {
        Purpose = purpose;
        Layer = layer;
        OccupiedPoints = occupiedPoints;
        ItemInstanceId = itemInstanceId;
    }

    public ApparelSelectionPurpose Purpose { get; }
    public ApparelLayer Layer { get; }
    public AnatomyAttachmentPoint OccupiedPoints { get; }
    public ItemInstanceId ItemInstanceId { get; }
}

public readonly struct CharacterApparelPolicySnapshot
{
    public CharacterApparelPolicySnapshot(
        CharacterId characterId,
        ApparelSelectionPurpose purpose,
        IReadOnlyList<ApparelDirectPreferenceSnapshot> directPreferences,
        DomainFailure lastFailure,
        bool hasTemporaryOverride)
    {
        CharacterId = characterId;
        Purpose = purpose;
        DirectPreferences = directPreferences
            ?? Array.Empty<ApparelDirectPreferenceSnapshot>();
        LastFailure = lastFailure;
        HasTemporaryOverride = hasTemporaryOverride;
    }

    public CharacterId CharacterId { get; }
    public ApparelSelectionPurpose Purpose { get; }
    public IReadOnlyList<ApparelDirectPreferenceSnapshot> DirectPreferences { get; }
    public DomainFailure LastFailure { get; }
    public bool HasTemporaryOverride { get; }
}

public readonly struct ApparelPolicyChoiceSnapshot
{
    public ApparelPolicyChoiceSnapshot(
        ItemInstanceId itemInstanceId,
        string displayName,
        ApparelLayer layer,
        AnatomyAttachmentPoint occupiedPoints,
        bool isAvailable,
        bool isEquipped,
        bool isDirectPreference,
        bool isConditionAvailable)
    {
        ItemInstanceId = itemInstanceId;
        DisplayName = displayName ?? string.Empty;
        Layer = layer;
        OccupiedPoints = occupiedPoints;
        IsAvailable = isAvailable;
        IsEquipped = isEquipped;
        IsDirectPreference = isDirectPreference;
        IsConditionAvailable = isConditionAvailable;
    }

    public ItemInstanceId ItemInstanceId { get; }
    public string DisplayName { get; }
    public ApparelLayer Layer { get; }
    public AnatomyAttachmentPoint OccupiedPoints { get; }
    public bool IsAvailable { get; }
    public bool IsEquipped { get; }
    public bool IsDirectPreference { get; }
    public bool IsConditionAvailable { get; }
}

internal sealed class CharacterApparelDirectPreference
{
    internal ApparelSelectionPurpose Purpose { get; set; }
    internal ApparelLayer Layer { get; set; }
    internal AnatomyAttachmentPoint OccupiedPoints { get; set; }
    internal ItemInstanceId ItemInstanceId { get; set; }

    internal CharacterApparelDirectPreference Copy() => new()
    {
        Purpose = Purpose,
        Layer = Layer,
        OccupiedPoints = OccupiedPoints,
        ItemInstanceId = ItemInstanceId
    };
}

internal sealed class CharacterApparelTemporaryOverride
{
    internal ItemInstanceId OverrideItemInstanceId { get; set; }
    internal string Source { get; set; } = string.Empty;
    internal List<EquippedApparelSaveData> Displaced { get; } = new();

    internal CharacterApparelTemporaryOverride Copy()
    {
        CharacterApparelTemporaryOverride copy = new()
        {
            OverrideItemInstanceId = OverrideItemInstanceId,
            Source = Source
        };
        copy.Displaced.AddRange(Displaced.Select(CharacterApparelRecord.Clone));
        return copy;
    }
}

internal sealed class CharacterApparelRecord
{
    internal List<EquippedApparelSaveData> Equipped { get; } = new();
    internal ApparelSelectionPurpose Purpose { get; set; } =
        ApparelSelectionPurpose.Daily;
    internal List<CharacterApparelDirectPreference> DirectPreferences { get; } =
        new();
    internal CharacterApparelTemporaryOverride TemporaryOverride { get; set; }

    internal CharacterApparelRecord Copy()
    {
        CharacterApparelRecord copy = new()
        {
            Purpose = Purpose,
            TemporaryOverride = TemporaryOverride?.Copy()
        };
        copy.Equipped.AddRange(Equipped.Select(Clone));
        copy.DirectPreferences.AddRange(DirectPreferences.Select(value => value.Copy()));
        return copy;
    }

    internal static EquippedApparelSaveData Clone(EquippedApparelSaveData value) => new()
    {
        characterId = value?.characterId?.Trim() ?? string.Empty,
        itemInstanceId = value?.itemInstanceId?.Trim() ?? string.Empty,
        apparelDefinitionId = value?.apparelDefinitionId?.Trim() ?? string.Empty,
        layer = value?.layer ?? ApparelLayer.Inner,
        occupiedPoints = value?.occupiedPoints ?? 0u
    };
}

internal sealed class CharacterApparelAggregateState
{
    internal Dictionary<CharacterId, CharacterApparelRecord> Characters { get; } = new();
    internal int Version { get; set; }

    internal CharacterApparelAggregateState Copy()
    {
        CharacterApparelAggregateState copy = new() { Version = Version + 1 };
        foreach (KeyValuePair<CharacterId, CharacterApparelRecord> pair in Characters)
        {
            copy.Characters.Add(pair.Key, pair.Value.Copy());
        }
        return copy;
    }
}

public sealed class CharacterApparelAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public CharacterApparelAggregateStateStore(DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
    }

    internal CharacterApparelAggregateState Current =>
        rootStore.GetOrCreate(() => new CharacterApparelAggregateState());

    internal void Replace(CharacterApparelAggregateState state) =>
        rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
}

public interface ICharacterApparelQuery
{
    int Version { get; }
    IReadOnlyList<EquippedApparelSnapshot> GetAllEquipped();
    IReadOnlyList<EquippedApparelSnapshot> GetEquipped(CharacterId characterId);
    CharacterApparelPolicySnapshot GetPolicy(CharacterId characterId);
    IReadOnlyList<ApparelPolicyChoiceSnapshot> GetPolicyChoices(
        CharacterId characterId,
        ApparelSelectionPurpose purpose);
    bool TryGetByItemInstance(
        ItemInstanceId itemInstanceId,
        out EquippedApparelSnapshot equipped);
}

public sealed class ApparelChangePlan
{
    internal ApparelChangePlan(
        CharacterId characterId,
        string stackId,
        ItemInstanceId itemInstanceId,
        ApparelDefinitionSO definition,
        ApparelInstanceState instance,
        ApparelFitAssessment fit,
        int apparelVersion,
        int itemVersion,
        IReadOnlyList<EquippedApparelSaveData> displaced)
    {
        CharacterId = characterId;
        StackId = stackId ?? string.Empty;
        ItemInstanceId = itemInstanceId;
        Definition = definition;
        Instance = instance;
        Fit = fit;
        ApparelVersion = apparelVersion;
        ItemVersion = itemVersion;
        Displaced = displaced ?? Array.Empty<EquippedApparelSaveData>();
    }

    public CharacterId CharacterId { get; }
    public string StackId { get; }
    public ItemInstanceId ItemInstanceId { get; }
    public ApparelDefinitionSO Definition { get; }
    public ApparelInstanceState Instance { get; }
    public ApparelFitAssessment Fit { get; }
    internal int ApparelVersion { get; }
    internal int ItemVersion { get; }
    internal IReadOnlyList<EquippedApparelSaveData> Displaced { get; }
}

public interface ICharacterApparelCommand
{
    bool TryPlanChange(
        CharacterId characterId,
        ItemInstanceId itemInstanceId,
        out ApparelChangePlan plan,
        out DomainFailure failure);
    bool TryCommitChange(ApparelChangePlan plan, out DomainFailure failure);
    bool TryUnequip(
        CharacterId characterId,
        ItemInstanceId itemInstanceId,
        out DomainFailure failure);
    bool TrySetSelectionPurpose(
        CharacterId characterId,
        ApparelSelectionPurpose purpose,
        out DomainFailure failure);
    bool TrySetDirectPreference(
        CharacterId characterId,
        ApparelSelectionPurpose purpose,
        ItemInstanceId itemInstanceId,
        out DomainFailure failure);
    bool TryClearDirectPreferences(
        CharacterId characterId,
        ApparelSelectionPurpose purpose,
        out DomainFailure failure);
    void RefreshAutomaticSelections();
    bool TryBeginTemporaryOverride(
        CharacterId characterId,
        ItemInstanceId itemInstanceId,
        string source,
        out DomainFailure failure);
    bool TryRestoreTemporaryOverride(
        CharacterId characterId,
        out DomainFailure failure);
}

public sealed class CharacterApparelRestoreCandidate
{
    internal CharacterApparelRestoreCandidate(CharacterApparelAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CharacterApparelAggregateState State { get; }
}

public interface ICharacterApparelPersistence
{
    IReadOnlyList<EquippedApparelSaveData> CaptureApparel();
    IReadOnlyList<CharacterApparelPolicySaveData> CaptureApparelPolicies();
    CharacterApparelRestoreCandidate PrepareRestoreApparel(
        IEnumerable<EquippedApparelSaveData> values,
        IEnumerable<CharacterApparelPolicySaveData> policies,
        DungeonGameRestoreReport report);
    void PublishRestoreApparel(CharacterApparelRestoreCandidate candidate);
    void ResetApparel();
}

public sealed class CharacterApparelAggregate :
    ICharacterApparelQuery,
    ICharacterApparelCommand,
    ICharacterApparelPersistence
{
    public const string EquippedDestinationPrefix = "apparel-equipped:";
    public const string RecoveryLockerDestination = "apparel-recovery-locker";
    public const string LegacyShadeClothMaterialId = "textile:shade-cloth";
    // ApparelWorkOrderRuntime already treats values below this boundary as
    // non-repairable. WIM-001 consumes that existing state; it adds no new
    // contamination, moisture, or wear threshold.
    private const float ExistingRepairableDurabilityFloor = 20f;

    private readonly struct ApparelOutcomeChange
    {
        public ApparelOutcomeChange(string apparelId, bool equipped)
        {
            ApparelId = apparelId ?? string.Empty;
            Equipped = equipped;
        }

        public string ApparelId { get; }
        public bool Equipped { get; }
    }

    private readonly CharacterApparelAggregateStateStore stateStore;
    private readonly IApparelDefinitionCatalog catalog;
    private readonly IAnatomyAttachmentQuery anatomy;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemRestoreCandidateStackQuery restoreCandidateItems;
    private readonly ICharacterWorldQuery characters;
    private readonly IApparelAvailabilityIndex availability;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly IGameClock gameClock;
    private readonly IApparelChangeOutcomeCommitter outcomeCommitter;
    private readonly Dictionary<CharacterId, string> observedPolicyFingerprints =
        new();
    private readonly Dictionary<CharacterId, DomainFailure> observedPolicyFailures =
        new();
    private int observedItemVersion = int.MinValue;
    private int observedApparelVersion = int.MinValue;
    private int observedCharacterVersion = int.MinValue;

    public CharacterApparelAggregate(
        CharacterApparelAggregateStateStore stateStore,
        IApparelDefinitionCatalog catalog,
        IAnatomyAttachmentQuery anatomy,
        IWorldItemStackRuntime items,
        IPhysicalItemRestoreCandidateStackQuery restoreCandidateItems,
        ICharacterWorldQuery characters,
        IApparelAvailabilityIndex availability,
        CharacterIdentityEventPublisher identityEvents,
        IGameClock gameClock,
        IApparelChangeOutcomeCommitter outcomeCommitter)
    {
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.restoreCandidateItems = restoreCandidateItems
            ?? throw new ArgumentNullException(nameof(restoreCandidateItems));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.availability = availability
            ?? throw new ArgumentNullException(nameof(availability));
        this.identityEvents = identityEvents
            ?? throw new ArgumentNullException(nameof(identityEvents));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.outcomeCommitter = outcomeCommitter
            ?? throw new ArgumentNullException(nameof(outcomeCommitter));
    }

    public int Version => stateStore.Current.Version;

    public IReadOnlyList<EquippedApparelSnapshot> GetAllEquipped() =>
        stateStore.Current.Characters
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .SelectMany(pair => pair.Value.Equipped
                .OrderBy(value => value.layer)
                .ThenBy(value => value.occupiedPoints)
                .Select(value => ToSnapshot(pair.Key, value)))
            .ToArray();

    public IReadOnlyList<EquippedApparelSnapshot> GetEquipped(CharacterId characterId)
    {
        if (!stateStore.Current.Characters.TryGetValue(characterId, out CharacterApparelRecord record))
        {
            return Array.Empty<EquippedApparelSnapshot>();
        }

        return record.Equipped
            .OrderBy(value => value.layer)
            .ThenBy(value => value.occupiedPoints)
            .Select(value => ToSnapshot(characterId, value))
            .ToArray();
    }

    public CharacterApparelPolicySnapshot GetPolicy(CharacterId characterId)
    {
        CharacterApparelRecord record = stateStore.Current.Characters.TryGetValue(
            characterId,
            out CharacterApparelRecord found)
                ? found
                : null;
        IReadOnlyList<ApparelDirectPreferenceSnapshot> preferences = record == null
            ? Array.Empty<ApparelDirectPreferenceSnapshot>()
            : record.DirectPreferences
                .OrderBy(value => value.Purpose)
                .ThenBy(value => value.Layer)
                .ThenBy(value => value.OccupiedPoints)
                .ThenBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal)
                .Select(value => new ApparelDirectPreferenceSnapshot(
                    value.Purpose,
                    value.Layer,
                    value.OccupiedPoints,
                    value.ItemInstanceId))
                .ToArray();
        return new CharacterApparelPolicySnapshot(
            characterId,
            record?.Purpose ?? ApparelSelectionPurpose.Daily,
            preferences,
            observedPolicyFailures.TryGetValue(characterId, out DomainFailure failure)
                ? failure
                : DomainFailure.None,
            record?.TemporaryOverride != null);
    }

    public IReadOnlyList<ApparelPolicyChoiceSnapshot> GetPolicyChoices(
        CharacterId characterId,
        ApparelSelectionPurpose purpose)
    {
        if (!characterId.IsValid)
        {
            return Array.Empty<ApparelPolicyChoiceSnapshot>();
        }

        CharacterApparelRecord record = stateStore.Current.Characters.TryGetValue(
            characterId,
            out CharacterApparelRecord found)
                ? found
                : null;
        HashSet<string> equipped = new(
            (record?.Equipped ?? new List<EquippedApparelSaveData>())
                .Select(value => value.itemInstanceId),
            StringComparer.Ordinal);
        HashSet<string> preferred = new(
            (record?.DirectPreferences
                ?? new List<CharacterApparelDirectPreference>())
                .Where(value => value.Purpose == purpose)
                .Select(value => value.ItemInstanceId.Value),
            StringComparer.Ordinal);
        ApparelUseTag purposeTag = ToUseTag(purpose);
        HashSet<ItemInstanceId> availableCandidates =
            GetAvailablePolicyCandidateIds(characterId, purposeTag);
        return items.GetAllStacks()
            .Where(stack => TryReadPolicyItem(
                stack,
                purposeTag,
                out _,
                out _))
            .Select(stack =>
            {
                catalog.TryGetByItemId(stack.ItemId, out ApparelDefinitionSO definition);
                TryReadApparelState(stack, definition, out ApparelInstanceState instance);
                ItemInstanceId instanceId = (ItemInstanceId)stack.ItemInstanceId;
                return new ApparelPolicyChoiceSnapshot(
                    instanceId,
                    string.IsNullOrWhiteSpace(stack.DisplayName)
                        ? definition.DisplayName
                        : stack.DisplayName,
                    definition.Layer,
                    definition.OccupiedPoints,
                    availableCandidates.Contains(instanceId)
                        && IsPolicyStockAvailable(stack)
                        && IsReplacementEligible(instance),
                    equipped.Contains(instanceId.Value),
                    preferred.Contains(instanceId.Value),
                    IsReplacementEligible(instance));
            })
            .OrderBy(value => value.Layer)
            .ThenBy(value => value.OccupiedPoints)
            .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private HashSet<ItemInstanceId> GetAvailablePolicyCandidateIds(
        CharacterId characterId,
        ApparelUseTag purposeTag)
    {
        HashSet<ItemInstanceId> result = new();
        ApparelCandidate[] buffer = new ApparelCandidate[8];
        AnatomyAttachmentPoint availablePoints =
            anatomy.GetAvailablePoints(characterId);
        ApparelSizeClass size = anatomy.GetSize(characterId);
        foreach (ApparelLayer layer in Enum.GetValues(typeof(ApparelLayer)))
        {
            int count = availability.FindCandidates(
                new ApparelSelectionQuery(
                    availablePoints,
                    size,
                    layer,
                    purposeTag,
                    TextileMaterialTag.None,
                    CraftsmanshipQualityTier.Awful),
                buffer);
            for (int index = 0; index < count; index++)
            {
                if (buffer[index].Durability
                    >= ExistingRepairableDurabilityFloor)
                {
                    result.Add(buffer[index].ItemInstanceId);
                }
            }
        }
        return result;
    }

    public bool TryGetByItemInstance(
        ItemInstanceId itemInstanceId,
        out EquippedApparelSnapshot equipped)
    {
        foreach (KeyValuePair<CharacterId, CharacterApparelRecord> pair in stateStore.Current.Characters)
        {
            EquippedApparelSaveData found = pair.Value.Equipped.FirstOrDefault(value =>
                string.Equals(value.itemInstanceId, itemInstanceId.Value, StringComparison.Ordinal));
            if (found != null)
            {
                equipped = ToSnapshot(pair.Key, found);
                return true;
            }
        }

        equipped = default;
        return false;
    }

    public bool TryPlanChange(
        CharacterId characterId,
        ItemInstanceId itemInstanceId,
        out ApparelChangePlan plan,
        out DomainFailure failure)
    {
        plan = null;
        failure = DomainFailure.None;
        CharacterActor actor = FindActor(characterId);
        if (actor == null)
        {
            failure = new DomainFailure(FailureCode.ApparelCharacterMissing, characterId.Value);
            return false;
        }

        WorldItemStackSnapshot stack = FindStack(itemInstanceId);
        if (stack == null || stack.Quantity != 1)
        {
            failure = new DomainFailure(FailureCode.ApparelPhysicalItemMissing, itemInstanceId.Value);
            return false;
        }
        if (stack.AvailableQuantity <= 0)
        {
            failure = new DomainFailure(FailureCode.ApparelItemReserved, stack.StackId);
            return false;
        }
        if (!catalog.TryGetByItemId(stack.ItemId, out ApparelDefinitionSO definition))
        {
            failure = new DomainFailure(FailureCode.ApparelDefinitionMissing, stack.ItemId);
            return false;
        }

        bool authoredState = ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState instance);
        if (!authoredState)
        {
            instance = CreateLegacyState(definition, anatomy.GetSize(characterId));
        }
        if (!anatomy.CanEquip(
                characterId,
                definition,
                instance,
                out ApparelFitAssessment fit,
                out failure))
        {
            return false;
        }

        CharacterApparelRecord current = stateStore.Current.Characters.TryGetValue(
            characterId,
            out CharacterApparelRecord found)
                ? found
                : new CharacterApparelRecord();
        EquippedApparelSaveData[] displaced = current.Equipped.Where(value =>
                value.layer == definition.Layer
                && (((AnatomyAttachmentPoint)value.occupiedPoints & definition.OccupiedPoints)
                    != AnatomyAttachmentPoint.None))
            .Select(CharacterApparelRecord.Clone)
            .ToArray();
        plan = new ApparelChangePlan(
            characterId,
            stack.StackId,
            itemInstanceId,
            definition,
            instance,
            fit,
            Version,
            items.ItemStackVersion,
            displaced);
        return true;
    }

    public bool TryCommitChange(ApparelChangePlan plan, out DomainFailure failure)
    {
        return TryCommitChange(
            plan,
            CharacterCommandOrigin.DirectPlayerOrder,
            clearTemporaryOverride: true,
            out failure);
    }

    private bool TryCommitChange(
        ApparelChangePlan plan,
        CharacterCommandOrigin origin,
        bool clearTemporaryOverride,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (plan == null
            || plan.ApparelVersion != Version
            || plan.ItemVersion != items.ItemStackVersion)
        {
            failure = new DomainFailure(FailureCode.ApparelPlanStale);
            return false;
        }

        CharacterActor actor = FindActor(plan.CharacterId);
        WorldItemStackSnapshot candidate = FindStack(plan.ItemInstanceId);
        if (actor == null || candidate == null || candidate.AvailableQuantity <= 0)
        {
            failure = new DomainFailure(FailureCode.ApparelPlanStale, plan.ItemInstanceId.Value);
            return false;
        }
        if (!anatomy.CanEquip(
                plan.CharacterId,
                plan.Definition,
                plan.Instance,
                out _,
                out failure))
        {
            return false;
        }

        List<WorldItemStackSnapshot> displacedStacks = new();
        foreach (EquippedApparelSaveData displaced in plan.Displaced)
        {
            WorldItemStackSnapshot stack = FindStack((ItemInstanceId)displaced.itemInstanceId);
            if (stack == null)
            {
                failure = new DomainFailure(
                    FailureCode.ApparelPhysicalItemMissing,
                    displaced.itemInstanceId);
                return false;
            }
            displacedStacks.Add(stack);
        }

        ApparelOutcomeChange[] outcomeChanges = plan.Displaced
            .Select(value => new ApparelOutcomeChange(
                value.apparelDefinitionId,
                equipped: false))
            .Append(new ApparelOutcomeChange(
                plan.Definition.ApparelId,
                equipped: true))
            .ToArray();
        long outcomeRevision = checked((long)Version + 1L);
        if (!TryPrepareApparelOutcomes(
                plan.CharacterId,
                outcomeRevision,
                origin,
                outcomeChanges,
                out List<PreparedEvolutionOutcome> preparedOutcomes,
                out string outcomePrepareFailure))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                outcomePrepareFailure);
            return false;
        }

        Vector2Int position = actor.GetNowXY();
        if (!items.TryRouteStackToDestination(
                candidate.StackId,
                WorldItemStackState.Carried,
                EquippedDestinationPrefix + plan.CharacterId.Value,
                position,
                out _))
        {
            CancelApparelOutcomes(preparedOutcomes);
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, candidate.ItemId);
            return false;
        }

        int moved = 0;
        for (; moved < displacedStacks.Count; moved++)
        {
            WorldItemStackSnapshot displaced = displacedStacks[moved];
            if (items.TryRouteStackToDestination(
                    displaced.StackId,
                    WorldItemStackState.Stored,
                    RecoveryLockerDestination,
                    position,
                    out _))
            {
                continue;
            }

            for (int rollback = 0; rollback < moved; rollback++)
            {
                RestoreRoute(displacedStacks[rollback]);
            }
            RestoreRoute(candidate);
            CancelApparelOutcomes(preparedOutcomes);
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, displaced.ItemId);
            return false;
        }

        if (!ApparelItemStateCodec.TryRead(candidate.Components, out _))
        {
            if (!items.TrySetInstanceComponent(
                    candidate.StackId,
                    ApparelItemStateCodec.Create(plan.Instance)))
            {
                foreach (WorldItemStackSnapshot displaced in displacedStacks)
                {
                    RestoreRoute(displaced);
                }
                RestoreRoute(candidate);
                CancelApparelOutcomes(preparedOutcomes);
                failure = new DomainFailure(
                    FailureCode.ApparelTransferFailed,
                    candidate.StackId);
                return false;
            }
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        if (!next.Characters.TryGetValue(plan.CharacterId, out CharacterApparelRecord record))
        {
            record = new CharacterApparelRecord();
            next.Characters.Add(plan.CharacterId, record);
        }
        HashSet<string> displacedIds = plan.Displaced
            .Select(value => value.itemInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        record.Equipped.RemoveAll(value => displacedIds.Contains(value.itemInstanceId));
        record.Equipped.Add(new EquippedApparelSaveData
        {
            characterId = plan.CharacterId.Value,
            itemInstanceId = plan.ItemInstanceId.Value,
            apparelDefinitionId = plan.Definition.ApparelId,
            layer = plan.Definition.Layer,
            occupiedPoints = (uint)plan.Definition.OccupiedPoints
        });
        if (clearTemporaryOverride)
        {
            record.TemporaryOverride = null;
        }
        stateStore.Replace(next);
        availability.Invalidate();
        CommitApparelOutcomesOrThrow(preparedOutcomes, outcomeRevision);
        foreach (EquippedApparelSaveData displaced in plan.Displaced)
        {
            PublishApparelChanged(
                plan.CharacterId,
                displaced.apparelDefinitionId,
                equipped: false,
                origin);
        }
        PublishApparelChanged(
            plan.CharacterId,
            plan.Definition.ApparelId,
            equipped: true,
            origin);
        return true;
    }

    public bool TryUnequip(
        CharacterId characterId,
        ItemInstanceId itemInstanceId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGetByItemInstance(itemInstanceId, out EquippedApparelSnapshot equipped)
            || !equipped.CharacterId.Equals(characterId))
        {
            failure = new DomainFailure(FailureCode.EnvironmentWorkwearNotEquipped, characterId.Value);
            return false;
        }

        WorldItemStackSnapshot stack = FindStack(itemInstanceId);
        CharacterActor actor = FindActor(characterId);
        if (stack == null || actor == null)
        {
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, itemInstanceId.Value);
            return false;
        }
        long outcomeRevision = checked((long)Version + 1L);
        if (!TryPrepareApparelOutcomes(
                characterId,
                outcomeRevision,
                CharacterCommandOrigin.DirectPlayerOrder,
                new[] { new ApparelOutcomeChange(equipped.ApparelDefinitionId, false) },
                out List<PreparedEvolutionOutcome> preparedOutcomes,
                out string outcomePrepareFailure))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                outcomePrepareFailure);
            return false;
        }
        if (!items.TryRouteStackToDestination(
                stack.StackId,
                WorldItemStackState.Stored,
                RecoveryLockerDestination,
                actor.GetNowXY(),
                out _))
        {
            CancelApparelOutcomes(preparedOutcomes);
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, itemInstanceId.Value);
            return false;
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        next.Characters[characterId].Equipped.RemoveAll(value =>
            string.Equals(value.itemInstanceId, itemInstanceId.Value, StringComparison.Ordinal));
        next.Characters[characterId].TemporaryOverride = null;
        stateStore.Replace(next);
        availability.Invalidate();
        CommitApparelOutcomesOrThrow(preparedOutcomes, outcomeRevision);
        PublishApparelChanged(
            characterId,
            equipped.ApparelDefinitionId,
            equipped: false,
            CharacterCommandOrigin.DirectPlayerOrder);
        return true;
    }

    [GameplayEntryPoint("Character apparel purpose controls; CharacterEnvironmentUnityAdapter work context")]
    public bool TrySetSelectionPurpose(
        CharacterId characterId,
        ApparelSelectionPurpose purpose,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!characterId.IsValid
            || FindActor(characterId) == null
            || !Enum.IsDefined(typeof(ApparelSelectionPurpose), purpose))
        {
            failure = new DomainFailure(
                FailureCode.ApparelCharacterMissing,
                characterId.Value);
            return false;
        }

        if (stateStore.Current.Characters.TryGetValue(
                characterId,
                out CharacterApparelRecord current)
            && current.Purpose == purpose)
        {
            return TryRefreshAutomaticSelection(
                characterId,
                force: false,
                out failure);
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        if (!next.Characters.TryGetValue(characterId, out CharacterApparelRecord record))
        {
            record = new CharacterApparelRecord();
            next.Characters.Add(characterId, record);
        }
        record.Purpose = purpose;
        stateStore.Replace(next);
        observedPolicyFingerprints.Remove(characterId);
        return TryRefreshAutomaticSelection(characterId, force: true, out failure);
    }

    [GameplayEntryPoint("Character apparel direct-designation control")]
    public bool TrySetDirectPreference(
        CharacterId characterId,
        ApparelSelectionPurpose purpose,
        ItemInstanceId itemInstanceId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        WorldItemStackSnapshot stack = FindStack(itemInstanceId);
        if (!characterId.IsValid
            || FindActor(characterId) == null
            || !Enum.IsDefined(typeof(ApparelSelectionPurpose), purpose))
        {
            failure = new DomainFailure(
                FailureCode.ApparelCharacterMissing,
                characterId.Value);
            return false;
        }
        if (stack == null
            || !TryReadPolicyItem(
                stack,
                ToUseTag(purpose),
                out ApparelDefinitionSO definition,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.ApparelPhysicalItemMissing,
                itemInstanceId.Value);
            return false;
        }

        if (stateStore.Current.Characters.TryGetValue(
                characterId,
                out CharacterApparelRecord current)
            && current.DirectPreferences.Any(value =>
                value.Purpose == purpose
                && value.Layer == definition.Layer
                && value.OccupiedPoints == definition.OccupiedPoints
                && value.ItemInstanceId.Equals(itemInstanceId))
            && !current.DirectPreferences.Any(value =>
                value.Purpose == purpose
                && value.Layer == definition.Layer
                && (value.OccupiedPoints & definition.OccupiedPoints)
                    != AnatomyAttachmentPoint.None
                && !value.ItemInstanceId.Equals(itemInstanceId)))
        {
            return TryRefreshAutomaticSelection(
                characterId,
                force: false,
                out failure);
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        if (!next.Characters.TryGetValue(characterId, out CharacterApparelRecord record))
        {
            record = new CharacterApparelRecord { Purpose = purpose };
            next.Characters.Add(characterId, record);
        }
        record.DirectPreferences.RemoveAll(value =>
            value.Purpose == purpose
            && value.Layer == definition.Layer
            && (value.OccupiedPoints & definition.OccupiedPoints)
                != AnatomyAttachmentPoint.None);
        record.DirectPreferences.Add(new CharacterApparelDirectPreference
        {
            Purpose = purpose,
            Layer = definition.Layer,
            OccupiedPoints = definition.OccupiedPoints,
            ItemInstanceId = itemInstanceId
        });
        stateStore.Replace(next);
        observedPolicyFingerprints.Remove(characterId);
        return TryRefreshAutomaticSelection(characterId, force: true, out failure);
    }

    [GameplayEntryPoint("Character apparel direct-designation clear control")]
    public bool TryClearDirectPreferences(
        CharacterId characterId,
        ApparelSelectionPurpose purpose,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!characterId.IsValid
            || FindActor(characterId) == null
            || !Enum.IsDefined(typeof(ApparelSelectionPurpose), purpose))
        {
            failure = new DomainFailure(
                FailureCode.ApparelCharacterMissing,
                characterId.Value);
            return false;
        }
        if (!stateStore.Current.Characters.TryGetValue(
                characterId,
                out CharacterApparelRecord current))
        {
            return true;
        }
        if (!current.DirectPreferences.Any(value => value.Purpose == purpose))
        {
            return TryRefreshAutomaticSelection(
                characterId,
                force: false,
                out failure);
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        next.Characters[characterId].DirectPreferences.RemoveAll(value =>
            value.Purpose == purpose);
        stateStore.Replace(next);
        observedPolicyFingerprints.Remove(characterId);
        return TryRefreshAutomaticSelection(characterId, force: true, out failure);
    }

    public void RefreshAutomaticSelections()
    {
        if (observedItemVersion == items.ItemStackVersion
            && observedApparelVersion == Version
            && observedCharacterVersion == characters.CharacterVersion)
        {
            return;
        }

        if (observedItemVersion != items.ItemStackVersion)
        {
            availability.Invalidate();
        }
        EnsureActiveCharacterPolicies();
        CharacterId[] charactersWithPolicies = stateStore.Current.Characters.Keys
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < charactersWithPolicies.Length; index++)
        {
            CharacterId characterId = charactersWithPolicies[index];
            string fingerprint = BuildPolicyFingerprint(characterId);
            if (observedPolicyFingerprints.TryGetValue(
                    characterId,
                    out string previous)
                && string.Equals(previous, fingerprint, StringComparison.Ordinal))
            {
                continue;
            }
            TryRefreshAutomaticSelection(characterId, force: false, out _);
        }
        observedItemVersion = items.ItemStackVersion;
        observedApparelVersion = Version;
        observedCharacterVersion = characters.CharacterVersion;
    }

    private void EnsureActiveCharacterPolicies()
    {
        CharacterId[] missing = (characters.Characters
                ?? Array.Empty<CharacterActor>())
            .Select(actor => new CharacterId(actor?.Identity?.PersistentId))
            .Where(characterId => characterId.IsValid
                && !stateStore.Current.Characters.ContainsKey(characterId))
            .Distinct()
            .OrderBy(characterId => characterId.Value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        for (int index = 0; index < missing.Length; index++)
        {
            next.Characters.Add(missing[index], new CharacterApparelRecord());
        }
        stateStore.Replace(next);
    }

    public bool TryBeginTemporaryOverride(
        CharacterId characterId,
        ItemInstanceId itemInstanceId,
        string source,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (stateStore.Current.Characters.TryGetValue(
                characterId,
                out CharacterApparelRecord current)
            && current.TemporaryOverride != null)
        {
            if (current.TemporaryOverride.OverrideItemInstanceId.Equals(itemInstanceId)
                && current.Equipped.Any(value => string.Equals(
                    value.itemInstanceId,
                    itemInstanceId.Value,
                    StringComparison.Ordinal)))
            {
                return true;
            }
            failure = new DomainFailure(FailureCode.ApparelPlanStale, characterId.Value);
            return false;
        }

        if (!TryPlanChange(characterId, itemInstanceId, out ApparelChangePlan plan, out failure))
        {
            return false;
        }
        EquippedApparelSaveData[] displaced = plan.Displaced
            .Select(CharacterApparelRecord.Clone)
            .ToArray();
        if (!TryCommitChange(
                plan,
                CharacterCommandOrigin.Autonomous,
                clearTemporaryOverride: false,
                out failure))
        {
            return false;
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        CharacterApparelRecord record = next.Characters[characterId];
        CharacterApparelTemporaryOverride temporary = new()
        {
            OverrideItemInstanceId = itemInstanceId,
            Source = source?.Trim() ?? string.Empty
        };
        temporary.Displaced.AddRange(displaced);
        record.TemporaryOverride = temporary;
        stateStore.Replace(next);
        ObservePolicy(characterId, DomainFailure.None);
        return true;
    }

    public bool TryRestoreTemporaryOverride(
        CharacterId characterId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!stateStore.Current.Characters.TryGetValue(
                characterId,
                out CharacterApparelRecord current)
            || current.TemporaryOverride == null)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearNotEquipped,
                characterId.Value);
            return false;
        }

        CharacterApparelTemporaryOverride temporary = current.TemporaryOverride;
        WorldItemStackSnapshot overrideStack = FindStack(
            temporary.OverrideItemInstanceId);
        CharacterActor actor = FindActor(characterId);
        if (actor == null
            || overrideStack == null
            || !current.Equipped.Any(value => string.Equals(
                value.itemInstanceId,
                temporary.OverrideItemInstanceId.Value,
                StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.ApparelPhysicalItemMissing,
                temporary.OverrideItemInstanceId.Value);
            return false;
        }

        List<WorldItemStackSnapshot> displacedStacks = new();
        foreach (EquippedApparelSaveData displaced in temporary.Displaced)
        {
            WorldItemStackSnapshot stack = FindStack(
                (ItemInstanceId)displaced.itemInstanceId);
            if (stack == null
                || stack.AvailableQuantity <= 0
                || stack.Forbidden
                || !catalog.TryGet(
                    displaced.apparelDefinitionId,
                    out ApparelDefinitionSO definition)
                || !TryReadApparelState(stack, definition, out ApparelInstanceState instance)
                || !anatomy.CanEquip(
                    characterId,
                    definition,
                    instance,
                    out _,
                    out failure))
            {
                if (!failure.IsFailure)
                {
                    failure = new DomainFailure(
                        FailureCode.ApparelItemReserved,
                        displaced.itemInstanceId);
                }
                return false;
            }
            displacedStacks.Add(stack);
        }

        List<ApparelOutcomeChange> outcomeChanges = new();
        bool hasOverrideDefinition = catalog.TryGetByItemId(
            overrideStack.ItemId,
            out ApparelDefinitionSO overrideDefinition);
        if (hasOverrideDefinition)
        {
            outcomeChanges.Add(new ApparelOutcomeChange(
                overrideDefinition.ApparelId,
                equipped: false));
        }
        outcomeChanges.AddRange(temporary.Displaced.Select(value =>
            new ApparelOutcomeChange(value.apparelDefinitionId, equipped: true)));
        long outcomeRevision = checked((long)Version + 1L);
        if (!TryPrepareApparelOutcomes(
                characterId,
                outcomeRevision,
                CharacterCommandOrigin.Autonomous,
                outcomeChanges,
                out List<PreparedEvolutionOutcome> preparedOutcomes,
                out string outcomePrepareFailure))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                outcomePrepareFailure);
            return false;
        }

        Vector2Int position = actor.GetNowXY();
        int moved = 0;
        for (; moved < displacedStacks.Count; moved++)
        {
            WorldItemStackSnapshot displaced = displacedStacks[moved];
            if (items.TryRouteStackToDestination(
                    displaced.StackId,
                    WorldItemStackState.Carried,
                    EquippedDestinationPrefix + characterId.Value,
                    position,
                    out _))
            {
                continue;
            }
            for (int rollback = 0; rollback < moved; rollback++)
            {
                RestoreRoute(displacedStacks[rollback]);
            }
            CancelApparelOutcomes(preparedOutcomes);
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                displaced.ItemId);
            return false;
        }

        if (!items.TryRouteStackToDestination(
                overrideStack.StackId,
                WorldItemStackState.Stored,
                RecoveryLockerDestination,
                position,
                out _))
        {
            foreach (WorldItemStackSnapshot displaced in displacedStacks)
            {
                RestoreRoute(displaced);
            }
            CancelApparelOutcomes(preparedOutcomes);
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                overrideStack.ItemId);
            return false;
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        CharacterApparelRecord record = next.Characters[characterId];
        record.Equipped.RemoveAll(value => string.Equals(
            value.itemInstanceId,
            temporary.OverrideItemInstanceId.Value,
            StringComparison.Ordinal));
        foreach (EquippedApparelSaveData displaced in temporary.Displaced)
        {
            if (!record.Equipped.Any(value => string.Equals(
                    value.itemInstanceId,
                    displaced.itemInstanceId,
                    StringComparison.Ordinal)))
            {
                record.Equipped.Add(CharacterApparelRecord.Clone(displaced));
            }
        }
        record.TemporaryOverride = null;
        stateStore.Replace(next);
        availability.Invalidate();
        CommitApparelOutcomesOrThrow(preparedOutcomes, outcomeRevision);
        if (hasOverrideDefinition)
        {
            PublishApparelChanged(
                characterId,
                overrideDefinition.ApparelId,
                equipped: false,
                CharacterCommandOrigin.Autonomous);
        }
        foreach (EquippedApparelSaveData displaced in temporary.Displaced)
        {
            PublishApparelChanged(
                characterId,
                displaced.apparelDefinitionId,
                equipped: true,
                CharacterCommandOrigin.Autonomous);
        }

        observedPolicyFingerprints.Remove(characterId);
        TryRefreshAutomaticSelection(characterId, force: true, out _);
        return true;
    }

    private bool TryRefreshAutomaticSelection(
        CharacterId characterId,
        bool force,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!stateStore.Current.Characters.TryGetValue(
                characterId,
                out CharacterApparelRecord record)
            || FindActor(characterId) == null)
        {
            failure = new DomainFailure(
                FailureCode.ApparelCharacterMissing,
                characterId.Value);
            ObservePolicy(characterId, failure);
            return false;
        }
        if (record.TemporaryOverride != null)
        {
            ObservePolicy(characterId, DomainFailure.None);
            return true;
        }

        string beforeFingerprint = BuildPolicyFingerprint(characterId);
        if (!force
            && observedPolicyFingerprints.TryGetValue(
                characterId,
                out string previous)
            && string.Equals(previous, beforeFingerprint, StringComparison.Ordinal))
        {
            return true;
        }

        ApparelSelectionPurpose purpose = record.Purpose;
        ApparelUseTag purposeTag = ToUseTag(purpose);
        List<ApparelPolicyChoiceSnapshot> choices = GetPolicyChoices(
            characterId,
            purpose).ToList();
        List<EquippedApparelSnapshot> equipped = GetEquipped(characterId).ToList();
        bool foundPurposeCandidate = false;
        DomainFailure observation = DomainFailure.None;

        foreach (ApparelLayer layer in Enum.GetValues(typeof(ApparelLayer)))
        {
            List<ApparelPolicyChoiceSnapshot> desired = new();
            AnatomyAttachmentPoint occupied = AnatomyAttachmentPoint.None;
            CharacterApparelDirectPreference[] preferences = record.DirectPreferences
                .Where(value => value.Purpose == purpose && value.Layer == layer)
                .OrderBy(value => value.OccupiedPoints)
                .ThenBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal)
                .ToArray();
            foreach (CharacterApparelDirectPreference preference in preferences)
            {
                ApparelPolicyChoiceSnapshot preferred = choices.FirstOrDefault(value =>
                    value.ItemInstanceId.Equals(preference.ItemInstanceId));
                if (!preferred.ItemInstanceId.IsValid
                    || !preferred.IsConditionAvailable
                    || (!preferred.IsAvailable && !preferred.IsEquipped))
                {
                    observation = ResolvePreferenceFailure(preference.ItemInstanceId);
                    continue;
                }
                if ((occupied & preferred.OccupiedPoints)
                    != AnatomyAttachmentPoint.None)
                {
                    continue;
                }
                desired.Add(preferred);
                occupied |= preferred.OccupiedPoints;
                foundPurposeCandidate = true;
            }

            foreach (EquippedApparelSnapshot current in equipped
                         .Where(value => value.Layer == layer)
                         .OrderBy(value => value.OccupiedPoints)
                         .ThenBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal))
            {
                WorldItemStackSnapshot stack = FindStack(current.ItemInstanceId);
                if (!TryReadPolicyItem(
                        stack,
                        purposeTag,
                        out ApparelDefinitionSO definition,
                        out ApparelInstanceState instance)
                    || !ShouldRetainCurrent(instance)
                    || (occupied & definition.OccupiedPoints)
                        != AnatomyAttachmentPoint.None)
                {
                    continue;
                }
                ApparelPolicyChoiceSnapshot existing = choices.FirstOrDefault(value =>
                    value.ItemInstanceId.Equals(current.ItemInstanceId));
                if (!existing.ItemInstanceId.IsValid)
                {
                    existing = new ApparelPolicyChoiceSnapshot(
                        current.ItemInstanceId,
                        definition.DisplayName,
                        definition.Layer,
                        definition.OccupiedPoints,
                        isAvailable: false,
                        isEquipped: true,
                        isDirectPreference: false,
                        isConditionAvailable: true);
                }
                desired.Add(existing);
                occupied |= definition.OccupiedPoints;
                foundPurposeCandidate = true;
            }

            foreach (ApparelPolicyChoiceSnapshot candidate in choices
                         .Where(value => value.Layer == layer && value.IsAvailable))
            {
                if ((occupied & candidate.OccupiedPoints)
                    != AnatomyAttachmentPoint.None)
                {
                    continue;
                }
                desired.Add(candidate);
                occupied |= candidate.OccupiedPoints;
                foundPurposeCandidate = true;
            }

            foreach (ApparelPolicyChoiceSnapshot candidate in desired)
            {
                if (equipped.Any(value => value.ItemInstanceId.Equals(
                        candidate.ItemInstanceId)))
                {
                    continue;
                }
                if (!TryPlanChange(
                        characterId,
                        candidate.ItemInstanceId,
                        out ApparelChangePlan plan,
                        out failure)
                    || !TryCommitChange(
                        plan,
                        CharacterCommandOrigin.Autonomous,
                        clearTemporaryOverride: true,
                        out failure))
                {
                    ObservePolicy(characterId, failure);
                    return false;
                }
                equipped = GetEquipped(characterId).ToList();
            }

            foreach (EquippedApparelSnapshot current in equipped
                         .Where(value => value.Layer == layer))
            {
                WorldItemStackSnapshot stack = FindStack(current.ItemInstanceId);
                bool controlled = stack != null
                    && catalog.TryGetByItemId(
                        stack.ItemId,
                        out ApparelDefinitionSO currentDefinition)
                    && (currentDefinition.UseTags
                        & (ApparelUseTag.Daily | ApparelUseTag.Work)) != 0;
                bool validForPurpose = controlled
                    && TryReadPolicyItem(stack, purposeTag, out _, out ApparelInstanceState state)
                    && ShouldRetainCurrent(state);
                bool hasReplacement = desired.Any(value =>
                    value.Layer == current.Layer
                    && (value.OccupiedPoints & current.OccupiedPoints)
                        != AnatomyAttachmentPoint.None);
                if (controlled && !validForPurpose && !hasReplacement)
                {
                    observation = new DomainFailure(
                        FailureCode.ApparelWorkOrderInvalid,
                        current.ItemInstanceId.Value,
                        purpose.ToString());
                }
            }
        }

        if (!foundPurposeCandidate
            && catalog.Definitions.Any(value => value != null
                && (value.UseTags & purposeTag) != 0))
        {
            observation = new DomainFailure(
                FailureCode.ApparelWorkOrderInvalid,
                purpose.ToString());
        }
        ObservePolicy(characterId, observation);
        return true;
    }

    private void ObservePolicy(CharacterId characterId, DomainFailure failure)
    {
        observedPolicyFailures[characterId] = failure;
        observedPolicyFingerprints[characterId] = BuildPolicyFingerprint(characterId);
        observedItemVersion = items.ItemStackVersion;
        observedApparelVersion = Version;
    }

    private string BuildPolicyFingerprint(CharacterId characterId)
    {
        if (!stateStore.Current.Characters.TryGetValue(
                characterId,
                out CharacterApparelRecord record))
        {
            return string.Empty;
        }
        ApparelUseTag purposeTag = ToUseTag(record.Purpose);
        HashSet<string> equippedIds = new(
            record.Equipped.Select(value => value.itemInstanceId),
            StringComparer.Ordinal);
        HashSet<string> preferredIds = new(
            record.DirectPreferences.Select(value => value.ItemInstanceId.Value),
            StringComparer.Ordinal);
        StringBuilder builder = new StringBuilder()
            .Append((int)record.Purpose).Append('|');
        foreach (CharacterApparelDirectPreference preference in record.DirectPreferences
                     .OrderBy(value => value.Purpose)
                     .ThenBy(value => value.Layer)
                     .ThenBy(value => value.OccupiedPoints)
                     .ThenBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal))
        {
            builder.Append((int)preference.Purpose).Append(':')
                .Append((int)preference.Layer).Append(':')
                .Append((uint)preference.OccupiedPoints).Append(':')
                .Append(preference.ItemInstanceId.Value).Append('|');
        }
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(value => value != null
                         && (equippedIds.Contains(value.ItemInstanceId ?? string.Empty)
                             || preferredIds.Contains(value.ItemInstanceId ?? string.Empty)
                             || TryReadPolicyItem(value, purposeTag, out _, out _)))
                     .OrderBy(value => value.ItemInstanceId, StringComparer.Ordinal))
        {
            builder.Append(stack.ItemInstanceId).Append(':')
                .Append(stack.ContentRevision).Append(':')
                .Append(stack.ReservationRevision).Append(':')
                .Append((int)stack.State).Append(':')
                .Append(stack.AvailableQuantity).Append(':')
                .Append(stack.Forbidden ? 1 : 0).Append(':')
                .Append(stack.DestinationId).Append('|');
        }
        return builder.ToString();
    }

    private DomainFailure ResolvePreferenceFailure(ItemInstanceId itemInstanceId)
    {
        WorldItemStackSnapshot stack = FindStack(itemInstanceId);
        if (stack == null)
        {
            return new DomainFailure(
                FailureCode.ApparelPhysicalItemMissing,
                itemInstanceId.Value);
        }
        if (catalog.TryGetByItemId(
                stack.ItemId,
                out ApparelDefinitionSO definition)
            && TryReadApparelState(
                stack,
                definition,
                out ApparelInstanceState state)
            && !IsReplacementEligible(state))
        {
            return new DomainFailure(
                FailureCode.ApparelWorkOrderInvalid,
                itemInstanceId.Value);
        }
        if (!IsPolicyStockAvailable(stack))
        {
            return new DomainFailure(
                FailureCode.ApparelItemReserved,
                itemInstanceId.Value);
        }
        return new DomainFailure(
            FailureCode.ApparelWorkOrderInvalid,
            itemInstanceId.Value);
    }

    private static ApparelUseTag ToUseTag(ApparelSelectionPurpose purpose) =>
        purpose == ApparelSelectionPurpose.Work
            ? ApparelUseTag.Work
            : ApparelUseTag.Daily;

    private bool TryReadPolicyItem(
        WorldItemStackSnapshot stack,
        ApparelUseTag purposeTag,
        out ApparelDefinitionSO definition,
        out ApparelInstanceState instance)
    {
        definition = null;
        instance = null;
        return stack != null
            && ((ItemInstanceId)stack.ItemInstanceId).IsValid
            && catalog.TryGetByItemId(stack.ItemId, out definition)
            && (definition.UseTags & purposeTag) != 0
            && TryReadApparelState(stack, definition, out instance);
    }

    private bool TryReadApparelState(
        WorldItemStackSnapshot stack,
        ApparelDefinitionSO definition,
        out ApparelInstanceState instance)
    {
        if (ApparelItemStateCodec.TryRead(stack.Components, out instance))
        {
            return true;
        }
        instance = definition != null
            ? CreateLegacyState(definition, ApparelSizeClass.Medium)
            : null;
        return instance != null;
    }

    internal static bool IsConditionAvailableForSelection(
        ApparelInstanceState state) =>
        state != null
        && state.durability >= ExistingRepairableDurabilityFloor
        && TextileConditionRules.ResolveCondition(
            state.moisture,
            state.contamination) == TextileConditionBand.Ready;

    private static bool ShouldRetainCurrent(ApparelInstanceState state) =>
        state != null
        && ApparelConditionRules.ShouldRetainCurrent(
            state.durability,
            state.moisture,
            state.contamination);

    private static bool IsReplacementEligible(ApparelInstanceState state) =>
        state != null
        && ApparelConditionRules.IsReplacementEligible(
            state.durability,
            state.moisture,
            state.contamination);

    private static bool IsPolicyStockAvailable(WorldItemStackSnapshot stack) =>
        stack != null
        && stack.Quantity == 1
        && stack.AvailableQuantity > 0
        && !stack.Forbidden
        && stack.State is WorldItemStackState.Loose
            or WorldItemStackState.Stored
            or WorldItemStackState.FacilityOutputBuffer
        && !(stack.DestinationId ?? string.Empty).StartsWith(
            EquippedDestinationPrefix,
            StringComparison.Ordinal);

    private bool TryPrepareApparelOutcomes(
        CharacterId characterId,
        long ownerRevision,
        CharacterCommandOrigin origin,
        IReadOnlyList<ApparelOutcomeChange> changes,
        out List<PreparedEvolutionOutcome> prepared,
        out string failureReason)
    {
        prepared = new List<PreparedEvolutionOutcome>();
        failureReason = string.Empty;
        if (changes == null || changes.Count == 0)
            return true;
        string operationId = "apparel-change:"
            + characterId.Value
            + ":revision:"
            + ownerRevision.ToString("D8");
        int absoluteDay = Mathf.Max(
            0,
            Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));
        for (int index = 0; index < changes.Count; index++)
        {
            ApparelOutcomeChange change = changes[index];
            ApparelChangeOutcomeReceipt receipt;
            try
            {
                receipt = new ApparelChangeOutcomeReceipt(
                    operationId,
                    ownerRevision,
                    index,
                    characterId,
                    change.ApparelId,
                    change.Equipped,
                    origin,
                    absoluteDay);
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                CancelApparelOutcomes(prepared);
                failureReason = "apparel-outcome-receipt-invalid:" + exception.Message;
                return false;
            }
            if (!outcomeCommitter.TryPrepare(
                    receipt,
                    out PreparedEvolutionOutcome next,
                    out failureReason))
            {
                CancelApparelOutcomes(prepared);
                return false;
            }
            prepared.Add(next);
        }
        return true;
    }

    private void CancelApparelOutcomes(
        IReadOnlyList<PreparedEvolutionOutcome> prepared)
    {
        if (prepared == null)
            return;
        for (int index = 0; index < prepared.Count; index++)
            outcomeCommitter.Cancel(prepared[index]);
    }

    private void CommitApparelOutcomesOrThrow(
        IReadOnlyList<PreparedEvolutionOutcome> prepared,
        long ownerRevision)
    {
        if (prepared == null)
            return;
        for (int index = 0; index < prepared.Count; index++)
        {
            if (!outcomeCommitter.TryCommit(
                    prepared[index],
                    ownerRevision,
                    out string failureReason))
            {
                for (int cancelIndex = index + 1;
                     cancelIndex < prepared.Count;
                     cancelIndex++)
                {
                    outcomeCommitter.Cancel(prepared[cancelIndex]);
                }
                throw new InvalidOperationException(
                    "Apparel domain committed without its mandatory outcome: "
                    + failureReason);
            }
        }
    }

    private void PublishApparelChanged(
        CharacterId characterId,
        string apparelId,
        bool equipped,
        CharacterCommandOrigin origin)
    {
        if (!characterId.IsValid || string.IsNullOrWhiteSpace(apparelId))
            return;
        identityEvents.Publish(new ApparelChangedEvent(
            characterId,
            apparelId,
            equipped,
            origin,
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    gameClock.Time / GameCalendarRules.SecondsPerDay))));
    }

    public IReadOnlyList<EquippedApparelSaveData> CaptureApparel()
    {
        return stateStore.Current.Characters
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .SelectMany(pair => pair.Value.Equipped
                .OrderBy(value => value.layer)
                .ThenBy(value => value.occupiedPoints)
                .Select(value => CharacterApparelRecord.Clone(value)))
            .ToArray();
    }

    public IReadOnlyList<CharacterApparelPolicySaveData> CaptureApparelPolicies()
    {
        return stateStore.Current.Characters
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CharacterApparelPolicySaveData
            {
                characterId = pair.Key.Value,
                purpose = pair.Value.Purpose,
                directPreferences = pair.Value.DirectPreferences
                    .OrderBy(value => value.Purpose)
                    .ThenBy(value => value.Layer)
                    .ThenBy(value => value.OccupiedPoints)
                    .ThenBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal)
                    .Select(value => new ApparelDirectPreferenceSaveData
                    {
                        purpose = value.Purpose,
                        layer = value.Layer,
                        occupiedPoints = (uint)value.OccupiedPoints,
                        itemInstanceId = value.ItemInstanceId.Value
                    })
                    .ToArray(),
                hasTemporaryOverride = pair.Value.TemporaryOverride != null,
                temporaryOverride = CaptureTemporaryOverride(pair.Value)
            })
            .ToArray();
    }

    public CharacterApparelRestoreCandidate PrepareRestoreApparel(
        IEnumerable<EquippedApparelSaveData> values,
        IEnumerable<CharacterApparelPolicySaveData> policies,
        DungeonGameRestoreReport report)
    {
        CharacterApparelAggregateState restored = new()
        {
            Version = stateStore.Current.Version + 1
        };
        foreach (EquippedApparelSaveData value in values ?? Array.Empty<EquippedApparelSaveData>())
        {
            CharacterId characterId = new(value?.characterId);
            ItemInstanceId itemId = (ItemInstanceId)(value?.itemInstanceId ?? string.Empty);
            PhysicalItemRestoreCandidateStackSnapshot stack =
                FindRestoreStack(itemId);
            if (!characterId.IsValid
                || !itemId.IsValid
                || value == null
                || !catalog.TryGet(value.apparelDefinitionId, out ApparelDefinitionSO definition)
                || definition.Layer != value.layer
                || (uint)definition.OccupiedPoints != value.occupiedPoints
                || FindActor(characterId) == null
                || stack == null
                || stack.Quantity != 1
                || !string.Equals(
                    stack.ItemId,
                    definition.PhysicalItemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    stack.DestinationId,
                    EquippedDestinationPrefix + characterId.Value,
                    StringComparison.Ordinal))
            {
                report?.AddError("Current apparel restore contains an invalid actor/physical join, definition, layer, or attachment signature.");
                continue;
            }
            if (!restored.Characters.TryGetValue(characterId, out CharacterApparelRecord record))
            {
                record = new CharacterApparelRecord();
                restored.Characters.Add(characterId, record);
            }
            record.Equipped.Add(CharacterApparelRecord.Clone(value));
        }

        foreach (CharacterApparelPolicySaveData policy in policies
                     ?? Array.Empty<CharacterApparelPolicySaveData>())
        {
            CharacterId characterId = new(policy?.characterId);
            if (policy == null
                || !characterId.IsValid
                || FindActor(characterId) == null
                || !Enum.IsDefined(
                    typeof(ApparelSelectionPurpose),
                    policy.purpose))
            {
                report?.AddError(
                    "Current apparel policy restore contains an invalid character or purpose.");
                continue;
            }
            if (!restored.Characters.TryGetValue(characterId, out CharacterApparelRecord record))
            {
                record = new CharacterApparelRecord();
                restored.Characters.Add(characterId, record);
            }
            record.Purpose = policy.purpose;
            foreach (ApparelDirectPreferenceSaveData preference in
                         policy.directPreferences
                         ?? Array.Empty<ApparelDirectPreferenceSaveData>())
            {
                ItemInstanceId preferenceId =
                    (ItemInstanceId)preference?.itemInstanceId;
                PhysicalItemRestoreCandidateStackSnapshot preferenceStack =
                    FindRestoreStack(preferenceId);
                if (preference == null
                    || !Enum.IsDefined(
                        typeof(ApparelSelectionPurpose),
                        preference.purpose)
                    || !Enum.IsDefined(typeof(ApparelLayer), preference.layer)
                    || preference.occupiedPoints == 0u
                    || !preferenceId.IsValid)
                {
                    report?.AddError(
                        $"Current apparel policy for '{characterId.Value}' contains an invalid direct preference.");
                    continue;
                }
                if (preferenceStack != null
                    && (!catalog.TryGetByItemId(
                            preferenceStack.ItemId,
                            out ApparelDefinitionSO preferenceDefinition)
                        || preferenceDefinition.Layer != preference.layer
                        || (uint)preferenceDefinition.OccupiedPoints
                            != preference.occupiedPoints
                        || (preferenceDefinition.UseTags
                            & ToUseTag(preference.purpose)) == 0))
                {
                    report?.AddError(
                        $"Current apparel policy for '{characterId.Value}' contains a direct preference whose live physical definition no longer matches its saved purpose or slot.");
                    continue;
                }
                record.DirectPreferences.Add(new CharacterApparelDirectPreference
                {
                    Purpose = preference.purpose,
                    Layer = preference.layer,
                    OccupiedPoints = (AnatomyAttachmentPoint)preference.occupiedPoints,
                    ItemInstanceId = preferenceId
                });
            }

            if (!policy.hasTemporaryOverride)
            {
                if (!IsEmptyTemporaryOverrideCarrier(policy.temporaryOverride))
                {
                    report?.AddError(
                        $"Current apparel policy for '{characterId.Value}' contains temporary override data without presence authority.");
                }
                continue;
            }
            if (policy.temporaryOverride == null)
            {
                report?.AddError(
                    $"Current apparel temporary override for '{characterId.Value}' is missing its payload.");
                continue;
            }
            ItemInstanceId overrideId =
                (ItemInstanceId)policy.temporaryOverride.itemInstanceId;
            bool overrideEquipped = record.Equipped.Any(value => string.Equals(
                value.itemInstanceId,
                overrideId.Value,
                StringComparison.Ordinal));
            CharacterApparelTemporaryOverride temporary = new()
            {
                OverrideItemInstanceId = overrideId,
                Source = policy.temporaryOverride.source?.Trim() ?? string.Empty
            };
            bool temporaryValid = overrideId.IsValid
                && overrideEquipped
                && !string.IsNullOrWhiteSpace(temporary.Source);
            HashSet<ItemInstanceId> displacedIds = new();
            foreach (EquippedApparelSaveData displaced in
                         policy.temporaryOverride.displaced
                         ?? Array.Empty<EquippedApparelSaveData>())
            {
                ItemInstanceId displacedId = (ItemInstanceId)displaced?.itemInstanceId;
                PhysicalItemRestoreCandidateStackSnapshot displacedStack =
                    FindRestoreStack(displacedId);
                temporaryValid &= displaced != null
                    && displacedId.IsValid
                    && displacedIds.Add(displacedId)
                    && catalog.TryGet(
                        displaced.apparelDefinitionId,
                        out ApparelDefinitionSO definition)
                    && definition.Layer == displaced.layer
                    && (uint)definition.OccupiedPoints == displaced.occupiedPoints
                    && displacedStack != null
                    && displacedStack.Quantity == 1
                    && string.Equals(
                        displacedStack.ItemId,
                        definition.PhysicalItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        displacedStack.DestinationId,
                        RecoveryLockerDestination,
                        StringComparison.Ordinal);
                temporary.Displaced.Add(CharacterApparelRecord.Clone(displaced));
            }
            if (!temporaryValid)
            {
                report?.AddError(
                    $"Current apparel temporary override for '{characterId.Value}' has an invalid exact physical join.");
                continue;
            }
            record.TemporaryOverride = temporary;
        }
        return new CharacterApparelRestoreCandidate(restored);
    }

    public void PublishRestoreApparel(CharacterApparelRestoreCandidate candidate)
    {
        stateStore.Replace((candidate
            ?? throw new ArgumentNullException(nameof(candidate))).State);
        availability.Invalidate();
        observedPolicyFingerprints.Clear();
        observedPolicyFailures.Clear();
        observedItemVersion = int.MinValue;
        observedApparelVersion = int.MinValue;
        observedCharacterVersion = int.MinValue;
    }

    public void ResetApparel()
    {
        stateStore.Replace(new CharacterApparelAggregateState
        {
            Version = stateStore.Current.Version + 1
        });
        availability.Invalidate();
        observedPolicyFingerprints.Clear();
        observedPolicyFailures.Clear();
        observedItemVersion = int.MinValue;
        observedApparelVersion = int.MinValue;
        observedCharacterVersion = int.MinValue;
    }

    private static ApparelTemporaryOverrideSaveData CaptureTemporaryOverride(
        CharacterApparelRecord record)
    {
        CharacterApparelTemporaryOverride source = record?.TemporaryOverride;
        return source == null
            ? null
            : new ApparelTemporaryOverrideSaveData
            {
                itemInstanceId = source.OverrideItemInstanceId.Value,
                source = source.Source,
                displaced = source.Displaced
                    .OrderBy(value => value.layer)
                    .ThenBy(value => value.occupiedPoints)
                    .ThenBy(value => value.itemInstanceId, StringComparer.Ordinal)
                    .Select(CharacterApparelRecord.Clone)
                    .ToArray()
            };
    }

    private static bool IsEmptyTemporaryOverrideCarrier(
        ApparelTemporaryOverrideSaveData value) =>
        value == null
        || string.IsNullOrEmpty(value.itemInstanceId)
        && string.IsNullOrEmpty(value.source)
        && (value.displaced == null || value.displaced.Length == 0);

    private CharacterActor FindActor(CharacterId id) => characters.Characters
        .FirstOrDefault(actor => CharacterPersistentIdentity.TryGet(actor, out CharacterId found)
            && found.Equals(id));

    private WorldItemStackSnapshot FindStack(ItemInstanceId id) => items.GetAllStacks()
        .FirstOrDefault(stack => string.Equals(
            stack.ItemInstanceId,
            id.Value,
            StringComparison.Ordinal));

    private PhysicalItemRestoreCandidateStackSnapshot FindRestoreStack(
        ItemInstanceId id)
    {
        if (restoreCandidateItems.IsCandidateAvailable)
        {
            restoreCandidateItems.TryGetStack(
                id,
                out PhysicalItemRestoreCandidateStackSnapshot candidate);
            return candidate;
        }

        WorldItemStackSnapshot live = FindStack(id);
        return live == null
            ? null
            : new PhysicalItemRestoreCandidateStackSnapshot(
                live.StackId,
                id,
                live.ItemId,
                live.Quantity,
                live.State,
                live.Position,
                live.DestinationId,
                live.Forbidden);
    }

    private static EquippedApparelSnapshot ToSnapshot(
        CharacterId characterId,
        EquippedApparelSaveData value) => new(
            characterId,
            (ItemInstanceId)value.itemInstanceId,
            value.apparelDefinitionId,
            value.layer,
            (AnatomyAttachmentPoint)value.occupiedPoints);

    private static ApparelInstanceState CreateLegacyState(
        ApparelDefinitionSO definition,
        ApparelSizeClass size) => new()
    {
        apparelDefinitionId = definition.ApparelId,
        primaryMaterialId = LegacyShadeClothMaterialId,
        size = definition.FitMode == ApparelFitMode.Accessory
            ? ApparelSizeClass.Medium
            : size,
        durability = 100f
    };

    private void RestoreRoute(WorldItemStackSnapshot stack)
    {
        string destination = string.IsNullOrWhiteSpace(stack.DestinationId)
            ? "apparel-rollback:" + stack.StackId
            : stack.DestinationId;
        items.TryRouteStackToDestination(
            stack.StackId,
            stack.State,
            destination,
            stack.Position,
            out _);
    }
}
