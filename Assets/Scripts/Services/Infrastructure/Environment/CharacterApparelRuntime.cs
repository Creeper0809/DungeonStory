using System;
using System.Collections.Generic;
using System.Linq;
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

internal sealed class CharacterApparelRecord
{
    internal List<EquippedApparelSaveData> Equipped { get; } = new();

    internal CharacterApparelRecord Copy()
    {
        CharacterApparelRecord copy = new();
        copy.Equipped.AddRange(Equipped.Select(Clone));
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
    CharacterApparelRestoreCandidate PrepareRestoreApparel(
        IEnumerable<EquippedApparelSaveData> values,
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

    private readonly CharacterApparelAggregateStateStore stateStore;
    private readonly IApparelDefinitionCatalog catalog;
    private readonly IAnatomyAttachmentQuery anatomy;
    private readonly IWorldItemStackRuntime items;
    private readonly ICharacterWorldQuery characters;
    private readonly IApparelAvailabilityIndex availability;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly IGameClock gameClock;

    public CharacterApparelAggregate(
        CharacterApparelAggregateStateStore stateStore,
        IApparelDefinitionCatalog catalog,
        IAnatomyAttachmentQuery anatomy,
        IWorldItemStackRuntime items,
        ICharacterWorldQuery characters,
        IApparelAvailabilityIndex availability,
        CharacterIdentityEventPublisher identityEvents,
        IGameClock gameClock)
    {
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.availability = availability
            ?? throw new ArgumentNullException(nameof(availability));
        this.identityEvents = identityEvents
            ?? throw new ArgumentNullException(nameof(identityEvents));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
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

        Vector2Int position = actor.GetNowXY();
        if (!items.TryRouteStackToDestination(
                candidate.StackId,
                WorldItemStackState.Carried,
                EquippedDestinationPrefix + plan.CharacterId.Value,
                position,
                out _))
        {
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
        stateStore.Replace(next);
        availability.Invalidate();
        foreach (EquippedApparelSaveData displaced in plan.Displaced)
        {
            PublishApparelChanged(
                plan.CharacterId,
                displaced.apparelDefinitionId,
                equipped: false);
        }
        PublishApparelChanged(
            plan.CharacterId,
            plan.Definition.ApparelId,
            equipped: true);
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
        if (stack == null || actor == null
            || !items.TryRouteStackToDestination(
                stack.StackId,
                WorldItemStackState.Stored,
                RecoveryLockerDestination,
                actor.GetNowXY(),
                out _))
        {
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, itemInstanceId.Value);
            return false;
        }

        CharacterApparelAggregateState next = stateStore.Current.Copy();
        next.Characters[characterId].Equipped.RemoveAll(value =>
            string.Equals(value.itemInstanceId, itemInstanceId.Value, StringComparison.Ordinal));
        stateStore.Replace(next);
        availability.Invalidate();
        PublishApparelChanged(
            characterId,
            equipped.ApparelDefinitionId,
            equipped: false);
        return true;
    }

    private void PublishApparelChanged(
        CharacterId characterId,
        string apparelId,
        bool equipped)
    {
        if (!characterId.IsValid || string.IsNullOrWhiteSpace(apparelId))
            return;
        identityEvents.Publish(new ApparelChangedEvent(
            characterId,
            apparelId,
            equipped,
            CharacterCommandOrigin.DirectPlayerOrder,
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

    public CharacterApparelRestoreCandidate PrepareRestoreApparel(
        IEnumerable<EquippedApparelSaveData> values,
        DungeonGameRestoreReport report)
    {
        CharacterApparelAggregateState restored = new();
        foreach (EquippedApparelSaveData value in values ?? Array.Empty<EquippedApparelSaveData>())
        {
            CharacterId characterId = new(value?.characterId);
            ItemInstanceId itemId = (ItemInstanceId)(value?.itemInstanceId ?? string.Empty);
            if (!characterId.IsValid
                || !itemId.IsValid
                || value == null
                || !catalog.TryGet(value.apparelDefinitionId, out ApparelDefinitionSO definition)
                || definition.Layer != value.layer
                || (uint)definition.OccupiedPoints != value.occupiedPoints)
            {
                report?.AddError("V22 apparel restore contains an invalid definition, character, item, layer, or attachment signature.");
                continue;
            }
            if (!restored.Characters.TryGetValue(characterId, out CharacterApparelRecord record))
            {
                record = new CharacterApparelRecord();
                restored.Characters.Add(characterId, record);
            }
            record.Equipped.Add(CharacterApparelRecord.Clone(value));
        }
        return new CharacterApparelRestoreCandidate(restored);
    }

    public void PublishRestoreApparel(CharacterApparelRestoreCandidate candidate)
    {
        stateStore.Replace((candidate
            ?? throw new ArgumentNullException(nameof(candidate))).State);
        availability.Invalidate();
    }

    public void ResetApparel()
    {
        stateStore.Replace(new CharacterApparelAggregateState
        {
            Version = stateStore.Current.Version + 1
        });
        availability.Invalidate();
    }

    private CharacterActor FindActor(CharacterId id) => characters.Characters
        .FirstOrDefault(actor => CharacterPersistentIdentity.TryGet(actor, out CharacterId found)
            && found.Equals(id));

    private WorldItemStackSnapshot FindStack(ItemInstanceId id) => items.GetAllStacks()
        .FirstOrDefault(stack => string.Equals(
            stack.ItemInstanceId,
            id.Value,
            StringComparison.Ordinal));

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
