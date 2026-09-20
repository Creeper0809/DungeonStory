using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;

public static class EvolutionOutcomeIds
{
    public const string ApparelProducerId = "equipment.apparel-change";
    public const string ApparelPhysicalProducerId = "equipment.apparel-physical";
    public const string ProductQualityProducerId = "equipment.product-quality";
    public const string TraitInferenceProducerId = "trait.acquired-inference";
    public const string TraitReactionProducerId = "trait.acquired-reaction";
    public const string FacilityProducerId = "facility.evolution";
    public const string MemoryErasureProducerId = "trait.memory-erasure-seal";
    public const string MemoryErasureBossAwardProducerId =
        "item.memory-erasure-seal-boss-award";

    public static readonly GameplayOutcomeTypeId ApparelChanged =
        new GameplayOutcomeTypeId("equipment.apparel-changed");
    public static readonly GameplayOutcomeTypeId ApparelPhysicalCompleted =
        new GameplayOutcomeTypeId("equipment.apparel-physical-completed");
    public static readonly GameplayOutcomeTypeId ProductQualityResolved =
        new GameplayOutcomeTypeId("equipment.product-quality-resolved");
    public static readonly GameplayOutcomeTypeId TraitInferenceCompleted =
        new GameplayOutcomeTypeId("trait.acquired-inference-completed");
    public static readonly GameplayOutcomeTypeId TraitReactionExecuted =
        new GameplayOutcomeTypeId("trait.acquired-reaction-executed");
    public static readonly GameplayOutcomeTypeId FacilityEvolutionCompleted =
        new GameplayOutcomeTypeId("facility.evolution-completed");
    public static readonly GameplayOutcomeTypeId MemoryErasureTerminal =
        new GameplayOutcomeTypeId("trait.memory-erasure-terminal");
    public static readonly GameplayOutcomeTypeId MemoryErasureBossAwarded =
        new GameplayOutcomeTypeId("item.memory-erasure-seal-boss-awarded");

    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId ApparelKind = new("apparel");
    public static readonly GameplayEntityKindId EquipmentKind = new("equipment");
    public static readonly GameplayEntityKindId TraitReactionKind = new("trait-reaction");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId FacilityDefinitionKind =
        new("facility-definition");
    public static readonly GameplayEntityKindId OffenseRegionKind =
        new("offense-region");

    public static readonly GameplayRoleId WearerRole = new("wearer");
    public static readonly GameplayRoleId TraitOwnerRole = new("trait-owner");
    public static readonly GameplayRoleId EvolvedFacilityRole = new("evolved-facility");
    public static readonly GameplayRoleId ErasureTargetRole = new("erasure-target");
    public static readonly GameplayRoleId PhysicalTransactionOwnerRole =
        new("physical-transaction-owner");
    public static readonly GameplayRoleId ProductMakerRole =
        new("product-maker");
    public static readonly GameplayRoleId AcquiredTraitOwnerRole =
        new("acquired-trait-owner");
    public static readonly GameplayRoleId AwardedRegionRole = new("awarded-region");

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
    public static readonly GameplayMetricId PhysicalInputMassMetric =
        new("item.physical-input-mass");
    public static readonly GameplayMetricId PhysicalOutputMassMetric =
        new("item.physical-output-mass");
    public static readonly GameplayMetricId ProductQualityTierMetric =
        new("product.quality-tier");
    public static readonly GameplayMetricId ProductQualityAttemptMetric =
        new("product.quality-attempt");
    public static readonly GameplayMetricId ProductRejectedMetric =
        new("product.rejected-below-minimum");
    public static readonly GameplayMetricId TraitExpectedRevisionMetric =
        new("trait.expected-revision");
    public static readonly GameplayMetricId TraitResultingRevisionMetric =
        new("trait.resulting-revision");
    public static readonly GameplayMetricId AwardedItemCountMetric =
        new("item.awarded-count");

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
    public static readonly GameplayOutcomeTagId PhysicalTransactionTag =
        new("physical-transaction");
    public static readonly GameplayOutcomeTagId ProductQualityTag =
        new("product-quality");
    public static readonly GameplayOutcomeTagId TraitInferenceTag =
        new("acquired-trait-inference");
    public static readonly GameplayOutcomeTagId BossAwardTag =
        new("boss-award");
}

internal static class EvolutionOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(
        string stableId,
        string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableId,
            nameof(stableId));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0)
        {
            throw new ArgumentException(
                "An immutable participant display name is required; stable IDs are not display names.",
                nameof(displayText));
        }
        string revision = "evolution-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

public readonly struct ApparelPhysicalOutcomeReceipt
{
    public ApparelPhysicalOutcomeReceipt(
        string operationId,
        long ownerRevision,
        string facilityPersistentId,
        string facilityDisplayName,
        string orderId,
        string outputItemId,
        ApparelPhysicalTransactionResult result,
        int absoluteDay,
        int qualitySchemaVersion,
        string makerCharacterId,
        string makerDisplayName,
        CraftsmanshipQualityTier quality,
        int attemptIndex,
        bool rejectedBelowMinimum)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision < 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        FacilityPersistentId = GameplayOutcomeStableIdSyntax.Require(
            facilityPersistentId,
            nameof(facilityPersistentId));
        FacilityDisplayName = EvolutionOutcomeNames.Snapshot(
            FacilityPersistentId,
            facilityDisplayName);
        OrderId = GameplayOutcomeStableIdSyntax.Require(orderId, nameof(orderId));
        OutputItemId = GameplayOutcomeStableIdSyntax.Require(
            outputItemId,
            nameof(outputItemId));
        if (!result.IsCompleted)
            throw new ArgumentException("Only a completed physical result can be recorded.", nameof(result));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (qualitySchemaVersion is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(qualitySchemaVersion));
        if (qualitySchemaVersion == 1)
        {
            MakerCharacterId = GameplayOutcomeStableIdSyntax.Require(
                makerCharacterId,
                nameof(makerCharacterId));
            MakerDisplayName = EvolutionOutcomeNames.Snapshot(
                MakerCharacterId,
                makerDisplayName);
            if (!Enum.IsDefined(typeof(CraftsmanshipQualityTier), quality))
                throw new ArgumentOutOfRangeException(nameof(quality));
            if (attemptIndex < 0 || attemptIndex != ownerRevision)
                throw new ArgumentOutOfRangeException(nameof(attemptIndex));
        }
        else
        {
            MakerCharacterId = string.Empty;
            MakerDisplayName = default;
        }
        OwnerRevision = ownerRevision;
        Result = result;
        AbsoluteDay = absoluteDay;
        QualitySchemaVersion = qualitySchemaVersion;
        Quality = quality;
        AttemptIndex = attemptIndex;
        RejectedBelowMinimum = rejectedBelowMinimum;
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public string FacilityPersistentId { get; }
    public KoreanNameSnapshot FacilityDisplayName { get; }
    public string OrderId { get; }
    public string OutputItemId { get; }
    public ApparelPhysicalTransactionResult Result { get; }
    public int AbsoluteDay { get; }
    public int QualitySchemaVersion { get; }
    public bool HasProductQuality => QualitySchemaVersion == 1;
    public string MakerCharacterId { get; }
    public KoreanNameSnapshot MakerDisplayName { get; }
    public CraftsmanshipQualityTier Quality { get; }
    public int AttemptIndex { get; }
    public bool RejectedBelowMinimum { get; }
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.ApparelPhysicalProducerId,
        OperationId,
        OwnerRevision,
        0);
}

public readonly struct ProductQualityOutcomeReceipt
{
    public ProductQualityOutcomeReceipt(
        string operationId,
        long ownerRevision,
        string makerCharacterId,
        string makerDisplayName,
        string definitionId,
        CraftsmanshipQualityTier quality,
        int attemptIndex,
        int absoluteDay,
        bool rejectedBelowMinimum)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision < 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        MakerCharacterId = GameplayOutcomeStableIdSyntax.Require(
            makerCharacterId,
            nameof(makerCharacterId));
        MakerDisplayName = EvolutionOutcomeNames.Snapshot(
            MakerCharacterId,
            makerDisplayName);
        DefinitionId = GameplayOutcomeStableIdSyntax.Require(
            definitionId,
            nameof(definitionId));
        if (!Enum.IsDefined(typeof(CraftsmanshipQualityTier), quality))
            throw new ArgumentOutOfRangeException(nameof(quality));
        if (attemptIndex < 0 || attemptIndex != ownerRevision)
            throw new ArgumentOutOfRangeException(nameof(attemptIndex));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        OwnerRevision = ownerRevision;
        Quality = quality;
        AttemptIndex = attemptIndex;
        AbsoluteDay = absoluteDay;
        RejectedBelowMinimum = rejectedBelowMinimum;
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public string MakerCharacterId { get; }
    public KoreanNameSnapshot MakerDisplayName { get; }
    public string DefinitionId { get; }
    public CraftsmanshipQualityTier Quality { get; }
    public int AttemptIndex { get; }
    public int AbsoluteDay { get; }
    public bool RejectedBelowMinimum { get; }
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.ProductQualityProducerId,
        OperationId,
        OwnerRevision,
        0);
}

public readonly struct AcquiredTraitInferenceOutcomeReceipt
{
    public AcquiredTraitInferenceOutcomeReceipt(
        string operationId,
        CharacterAcquiredTraitInferenceCommandResult result,
        string characterDisplayName,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        CharacterAcquiredTraitInferenceAuditRecord audit = result.Audit;
        if (!result.Succeeded
            || audit.CommandKind != CharacterAcquiredTraitInferenceCommandKind.Completion
            || !GameplayOutcomeStableIdSyntax.IsValid(audit.TargetPersistentId)
            || string.IsNullOrWhiteSpace(audit.AuditId)
            || string.IsNullOrWhiteSpace(audit.CandidatePacketHash)
            || audit.ExpectedRevision < 0
            || audit.ResultingRevision <= audit.ExpectedRevision)
        {
            throw new ArgumentException(
                "A successful acquired-trait completion is required.",
                nameof(result));
        }
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        Result = result;
        CharacterDisplayName = EvolutionOutcomeNames.Snapshot(
            audit.TargetPersistentId,
            characterDisplayName);
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public CharacterAcquiredTraitInferenceCommandResult Result { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.TraitInferenceProducerId,
        OperationId,
        Result.Audit.ResultingRevision,
        0);
}

public readonly struct MemoryErasureBossAwardOutcomeReceipt
{
    public MemoryErasureBossAwardOutcomeReceipt(
        MemoryErasureSealBossAwardResult result,
        string regionDisplayName,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(result.OperationId);
        if (result.Status != MemoryErasureSealBossAwardStatus.Awarded
            || !GameplayOutcomeStableIdSyntax.IsValid(result.RegionId)
            || string.IsNullOrWhiteSpace(result.PhysicalCommitId))
        {
            throw new ArgumentException(
                "A committed memory-erasure boss award is required.",
                nameof(result));
        }
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        Result = result;
        RegionDisplayName = EvolutionOutcomeNames.Snapshot(
            result.RegionId,
            regionDisplayName);
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public MemoryErasureSealBossAwardResult Result { get; }
    public KoreanNameSnapshot RegionDisplayName { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        EvolutionOutcomeIds.MemoryErasureBossAwardProducerId,
        OperationId,
        0L,
        0);
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

public readonly struct ReservedEvolutionOutcome
{
    internal ReservedEvolutionOutcome(
        PreparedOutcomeReservation reservation,
        GameplayResultKey resultKey,
        bool alreadyCommitted)
    {
        Reservation = reservation;
        ResultKey = resultKey;
        AlreadyCommitted = alreadyCommitted;
    }

    internal PreparedOutcomeReservation Reservation { get; }
    public GameplayResultKey ResultKey { get; }
    public bool AlreadyCommitted { get; }
    public bool IsValid => AlreadyCommitted && ResultKey.IsValid
        || Reservation.IsValid && ResultKey.IsValid;
}
