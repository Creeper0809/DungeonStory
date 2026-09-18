using System;
using System.Collections.Generic;
using System.Linq;

public static class EvolutionOutcomeIds
{
    public const string ApparelProducerId = "equipment.apparel-change";
    public const string TraitReactionProducerId = "trait.acquired-reaction";
    public const string FacilityProducerId = "facility.evolution";
    public const string MemoryErasureProducerId = "trait.memory-erasure-seal";

    public static readonly GameplayOutcomeTypeId ApparelChanged =
        new GameplayOutcomeTypeId("equipment.apparel-changed");
    public static readonly GameplayOutcomeTypeId TraitReactionExecuted =
        new GameplayOutcomeTypeId("trait.acquired-reaction-executed");
    public static readonly GameplayOutcomeTypeId FacilityEvolutionCompleted =
        new GameplayOutcomeTypeId("facility.evolution-completed");
    public static readonly GameplayOutcomeTypeId MemoryErasureTerminal =
        new GameplayOutcomeTypeId("trait.memory-erasure-terminal");

    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId ApparelKind = new("apparel");
    public static readonly GameplayEntityKindId TraitReactionKind = new("trait-reaction");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId FacilityDefinitionKind =
        new("facility-definition");

    public static readonly GameplayRoleId WearerRole = new("wearer");
    public static readonly GameplayRoleId TraitOwnerRole = new("trait-owner");
    public static readonly GameplayRoleId EvolvedFacilityRole = new("evolved-facility");
    public static readonly GameplayRoleId ErasureTargetRole = new("erasure-target");

    public static readonly GameplayMetricId ApparelEquippedMetric =
        new("apparel.equipped-state");
    public static readonly GameplayMetricId ApparelOriginMetric =
        new("apparel.change-origin");
    public static readonly GameplayMetricId TraitExperienceMetric =
        new("trait.experience-granted");
    public static readonly GameplayMetricId TraitMoodMetric =
        new("trait.mood-impulse");
    public static readonly GameplayMetricId FacilityStarGradeMetric =
        new("facility.star-grade");
    public static readonly GameplayMetricId MaterialQuantityMetric =
        new("facility.material-count");
    public static readonly GameplayMetricId MaterialMassMetric =
        new("facility.material-mass");
    public static readonly GameplayMetricId MaterialSourceCountMetric =
        new("facility.material-source-count");
    public static readonly GameplayMetricId MemoryErasureStatusMetric =
        new("trait.erasure-status");
    public static readonly GameplayMetricId PreviousRevisionMetric =
        new("trait.previous-revision");
    public static readonly GameplayMetricId CommittedRevisionMetric =
        new("trait.committed-revision");
    public static readonly GameplayMetricId ConsumedSealMetric =
        new("item.consumed-count");

    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId EffectPointUnit = new("effect-point");
    public static readonly GameplayMetricUnitId GradeUnit = new("grade");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId GramUnit = new("gram");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");

    public static readonly GameplayOutcomeTagId EquipmentTag = new("equipment");
    public static readonly GameplayOutcomeTagId TraitTag = new("trait");
    public static readonly GameplayOutcomeTagId FacilityTag = new("facility-evolution");
    public static readonly GameplayOutcomeTagId MemoryErasureTag =
        new("memory-erasure");
}

public readonly struct ApparelChangeOutcomeReceipt
{
    public ApparelChangeOutcomeReceipt(
        string operationId,
        long ownerRevision,
        int localResultIndex,
        CharacterId characterId,
        string apparelId,
        bool equipped,
        CharacterCommandOrigin origin,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision < 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (localResultIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(localResultIndex));
        if (!characterId.IsValid)
            throw new ArgumentException("A valid character is required.", nameof(characterId));
        ApparelId = GameplayOutcomeStableIdSyntax.Require(apparelId, nameof(apparelId));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        OwnerRevision = ownerRevision;
        LocalResultIndex = localResultIndex;
        CharacterId = characterId;
        Equipped = equipped;
        Origin = origin;
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public int LocalResultIndex { get; }
    public CharacterId CharacterId { get; }
    public string ApparelId { get; }
    public bool Equipped { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.ApparelProducerId,
        OperationId,
        OwnerRevision,
        LocalResultIndex);
}

public readonly struct AcquiredTraitReactionOutcomeReceipt
{
    public AcquiredTraitReactionOutcomeReceipt(
        string operationId,
        long ownerRevision,
        CharacterId characterId,
        string traitInstanceId,
        string reactionId,
        CharacterAcquiredTraitReactionAction action,
        float appliedValue,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision < 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (!characterId.IsValid)
            throw new ArgumentException("A valid character is required.", nameof(characterId));
        if (string.IsNullOrWhiteSpace(traitInstanceId))
            throw new ArgumentException("A trait instance is required.", nameof(traitInstanceId));
        ReactionId = GameplayOutcomeStableIdSyntax.Require(reactionId, nameof(reactionId));
        if (!float.IsFinite(appliedValue))
            throw new ArgumentOutOfRangeException(nameof(appliedValue));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        OwnerRevision = ownerRevision;
        CharacterId = characterId;
        TraitInstanceId = traitInstanceId.Trim();
        Action = action;
        AppliedValue = appliedValue;
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public CharacterId CharacterId { get; }
    public string TraitInstanceId { get; }
    public string ReactionId { get; }
    public CharacterAcquiredTraitReactionAction Action { get; }
    public float AppliedValue { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.TraitReactionProducerId,
        OperationId,
        OwnerRevision,
        0);
}

public readonly struct FacilityEvolutionOutcomeReceipt
{
    public FacilityEvolutionOutcomeReceipt(
        string operationId,
        long ownerRevision,
        string facilityPersistentId,
        string recipeId,
        string resultFacilityDefinitionId,
        int resultStarGrade,
        string materialCommitId,
        IReadOnlyList<string> sourceStackIds,
        int materialQuantity,
        long materialInputMassGrams,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        FacilityPersistentId = GameplayOutcomeStableIdSyntax.Require(
            facilityPersistentId,
            nameof(facilityPersistentId));
        RecipeId = GameplayOutcomeStableIdSyntax.Require(recipeId, nameof(recipeId));
        ResultFacilityDefinitionId = GameplayOutcomeStableIdSyntax.Require(
            resultFacilityDefinitionId,
            nameof(resultFacilityDefinitionId));
        if (resultStarGrade <= 0)
            throw new ArgumentOutOfRangeException(nameof(resultStarGrade));
        string[] sources = (sourceStackIds ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool hasMaterial = materialQuantity > 0
            || materialInputMassGrams > 0L
            || !string.IsNullOrWhiteSpace(materialCommitId)
            || sources.Length > 0;
        if (hasMaterial
            && (materialQuantity <= 0
                || materialInputMassGrams <= 0L
                || string.IsNullOrWhiteSpace(materialCommitId)
                || sources.Length == 0
                || sources.Any(string.IsNullOrWhiteSpace)))
        {
            throw new ArgumentException("Material provenance must be complete.");
        }
        if (!hasMaterial && (materialQuantity != 0 || materialInputMassGrams != 0L))
            throw new ArgumentException("Material metrics must be zero without a commit.");
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        OwnerRevision = ownerRevision;
        ResultStarGrade = resultStarGrade;
        MaterialCommitId = materialCommitId?.Trim() ?? string.Empty;
        SourceStackIds = sources;
        MaterialQuantity = materialQuantity;
        MaterialInputMassGrams = materialInputMassGrams;
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public string FacilityPersistentId { get; }
    public string RecipeId { get; }
    public string ResultFacilityDefinitionId { get; }
    public int ResultStarGrade { get; }
    public string MaterialCommitId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int MaterialQuantity { get; }
    public long MaterialInputMassGrams { get; }
    public int AbsoluteDay { get; }
    public bool HasMaterialCommit => MaterialCommitId.Length > 0;
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.FacilityProducerId,
        OperationId,
        OwnerRevision,
        0);
}

public readonly struct MemoryErasureOutcomeReceipt
{
    public MemoryErasureOutcomeReceipt(
        MemoryErasureSealUseResult result,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(result.OperationId);
        if (!GameplayOutcomeStableIdSyntax.IsValid(result.TargetCharacterId))
            throw new ArgumentException("A canonical target character is required.", nameof(result));
        if (string.IsNullOrWhiteSpace(result.TraitInstanceId)
            || string.IsNullOrWhiteSpace(result.AuditId))
        {
            throw new ArgumentException("Trait and audit evidence are required.", nameof(result));
        }
        if (result.PreviousRevision < 0 || result.CommittedRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(result));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        Result = result;
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public MemoryErasureSealUseResult Result { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.MemoryErasureProducerId,
        OperationId,
        0L,
        0);
}

public readonly struct PreparedEvolutionOutcome
{
    internal PreparedEvolutionOutcome(
        PreparedOutcomeToken prepared,
        GameplayResultKey resultKey,
        bool alreadyCommitted)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        AlreadyCommitted = alreadyCommitted;
    }

    internal PreparedOutcomeToken Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public bool AlreadyCommitted { get; }
    public bool IsValid => AlreadyCommitted && ResultKey.IsValid || Prepared.IsValid;
}

public readonly struct ReservedMemoryErasureOutcome
{
    internal ReservedMemoryErasureOutcome(
        PreparedOutcomeReservation reservation,
        GameplayOperationId operationId)
    {
        Reservation = reservation;
        OperationId = operationId;
    }

    internal PreparedOutcomeReservation Reservation { get; }
    public GameplayOperationId OperationId { get; }
    public bool IsValid => Reservation.IsValid && OperationId.IsValid;
}
