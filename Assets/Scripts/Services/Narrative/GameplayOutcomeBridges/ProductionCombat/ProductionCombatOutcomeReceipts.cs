using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public readonly struct ProductionCompletedOutcomeReceipt
{
    public ProductionCompletedOutcomeReceipt(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        KoreanNameSnapshot facilityDisplayName,
        string workerPersistentId,
        KoreanNameSnapshot workerDisplayName,
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string destinationId,
        string wipInputCommitId,
        int wipInputQuantity,
        long wipInputMassGrams,
        long cleanWaterMassGrams,
        long wastewaterMassGrams,
        long declaredLossMassGrams,
        long declaredExternalInputMassGrams,
        IReadOnlyList<ProductionOutcomeLineSnapshot> outputLines,
        IReadOnlyList<ProductionOutcomePhysicalStackSnapshot> physicalStacks,
        int absoluteDay)
    {
        if (!billId.IsValid)
            throw new ArgumentException("A valid production bill is required.", nameof(billId));
        if (cycleSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(cycleSequence));
        if (!GameplayOutcomeStableIdSyntax.IsValid(recipeId))
            throw new ArgumentException("A canonical recipe ID is required.", nameof(recipeId));
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid facility ID is required.", nameof(facilityId));
        if (!string.IsNullOrEmpty(workerPersistentId)
            && !GameplayOutcomeStableIdSyntax.IsValid(workerPersistentId))
        {
            throw new ArgumentException("The worker ID is not canonical.", nameof(workerPersistentId));
        }
        if (!GameplayOutcomeStableIdSyntax.IsValid(batchCommitId))
            throw new ArgumentException("A canonical batch commit ID is required.", nameof(batchCommitId));
        if (!IsDigest(outcomeFingerprint))
            throw new ArgumentException("A lowercase outcome SHA-256 is required.", nameof(outcomeFingerprint));
        if (!IsDigest(plannedOutputFingerprint))
            throw new ArgumentException("A lowercase planned-output SHA-256 is required.", nameof(plannedOutputFingerprint));
        if (!GameplayOutcomeStableIdSyntax.IsValid(destinationId))
            throw new ArgumentException("A canonical output destination is required.", nameof(destinationId));
        bool hasWip = wipInputQuantity != 0 || wipInputMassGrams != 0L
            || !string.IsNullOrEmpty(wipInputCommitId);
        if (hasWip && (!GameplayOutcomeStableIdSyntax.IsValid(wipInputCommitId)
                || wipInputQuantity <= 0 || wipInputMassGrams <= 0L))
        {
            throw new ArgumentException("The WIP input tuple is incomplete.", nameof(wipInputCommitId));
        }
        if (!hasWip && (wipInputQuantity != 0 || wipInputMassGrams != 0L))
            throw new ArgumentException("The zero-WIP tuple is inconsistent.", nameof(wipInputQuantity));
        if (cleanWaterMassGrams < 0L)
            throw new ArgumentOutOfRangeException(nameof(cleanWaterMassGrams));
        if (wastewaterMassGrams < 0L)
            throw new ArgumentOutOfRangeException(nameof(wastewaterMassGrams));
        if (declaredLossMassGrams < 0L)
            throw new ArgumentOutOfRangeException(nameof(declaredLossMassGrams));
        if (declaredExternalInputMassGrams < 0L)
            throw new ArgumentOutOfRangeException(nameof(declaredExternalInputMassGrams));
        if (absoluteDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        ProductionOutcomeLineSnapshot[] lines = (outputLines
                ?? throw new ArgumentNullException(nameof(outputLines)))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        ProductionOutcomePhysicalStackSnapshot[] stacks = (physicalStacks
                ?? throw new ArgumentNullException(nameof(physicalStacks)))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (lines.Length == 0 || stacks.Length == 0
            || lines.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != lines.Length
            || stacks.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != stacks.Length)
        {
            throw new ArgumentException(
                "Exact production lines and unique physical stacks are required.");
        }
        foreach (ProductionOutcomeLineSnapshot line in lines)
        {
            ProductionOutcomePhysicalStackSnapshot[] lineStacks = stacks
                .Where(value => string.Equals(
                    value.OutputLineId,
                    line.OutputLineId,
                    StringComparison.Ordinal))
                .ToArray();
            if (lineStacks.Length == 0
                || lineStacks.Any(value => !string.Equals(
                    value.ItemId,
                    line.ItemId,
                    StringComparison.Ordinal))
                || lineStacks.Sum(value => value.Quantity) != line.Quantity
                || lineStacks.Sum(value => value.MassGrams) != line.MassGrams)
            {
                throw new ArgumentException(
                    "Physical output stacks must exactly conserve each output line.");
            }
        }
        if (stacks.Any(value => !lines.Any(line => string.Equals(
                line.OutputLineId,
                value.OutputLineId,
                StringComparison.Ordinal))))
        {
            throw new ArgumentException("A physical stack has no output-line owner.");
        }

        BillId = billId;
        CycleSequence = cycleSequence;
        RecipeId = recipeId;
        FacilityId = facilityId;
        FacilityDisplayName = facilityDisplayName;
        WorkerPersistentId = workerPersistentId ?? string.Empty;
        WorkerDisplayName = workerDisplayName;
        BatchCommitId = batchCommitId;
        OutcomeFingerprint = outcomeFingerprint;
        PlannedOutputFingerprint = plannedOutputFingerprint;
        DestinationId = destinationId;
        WipInputCommitId = wipInputCommitId ?? string.Empty;
        WipInputQuantity = wipInputQuantity;
        WipInputMassGrams = wipInputMassGrams;
        CleanWaterMassGrams = cleanWaterMassGrams;
        WastewaterMassGrams = wastewaterMassGrams;
        DeclaredLossMassGrams = declaredLossMassGrams;
        DeclaredExternalInputMassGrams = declaredExternalInputMassGrams;
        OutputLines = Array.AsReadOnly(lines);
        PhysicalStacks = Array.AsReadOnly(stacks);
        AbsoluteDay = absoluteDay;
    }

    public ProductionBillId BillId { get; }
    public int CycleSequence { get; }
    public string RecipeId { get; }
    public BuildingInstanceId FacilityId { get; }
    public KoreanNameSnapshot FacilityDisplayName { get; }
    public string WorkerPersistentId { get; }
    public KoreanNameSnapshot WorkerDisplayName { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public string DestinationId { get; }
    public string WipInputCommitId { get; }
    public int WipInputQuantity { get; }
    public long WipInputMassGrams { get; }
    public long CleanWaterMassGrams { get; }
    public long WastewaterMassGrams { get; }
    public long DeclaredLossMassGrams { get; }
    public long DeclaredExternalInputMassGrams { get; }
    public IReadOnlyList<ProductionOutcomeLineSnapshot> OutputLines { get; }
    public IReadOnlyList<ProductionOutcomePhysicalStackSnapshot> PhysicalStacks { get; }
    public int AbsoluteDay { get; }
    public bool HasWorker => !string.IsNullOrEmpty(WorkerPersistentId);
    public bool HasWipInput => !string.IsNullOrEmpty(WipInputCommitId);
    public int ProducedQuantity => OutputLines.Sum(value => value.Quantity);
    public long ProducedMassGrams => OutputLines.Sum(value => value.MassGrams);

    public GameplayOperationId OperationId => new GameplayOperationId(
        $"production-cycle:{BillId.Value}:{CycleSequence}");

    public GameplayResultKey ResultKey => new GameplayResultKey(
        ProductionCombatOutcomeIds.ProductionProducerId,
        OperationId,
        CycleSequence,
        0);

    private static bool IsDigest(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

public readonly struct ProductionOutcomeLineSnapshot
{
    public ProductionOutcomeLineSnapshot(
        string outputLineId,
        string lineCommitId,
        string itemId,
        int quantity,
        long massGrams,
        string capabilityFingerprint) : this(
        outputLineId,
        new[] { lineCommitId },
        itemId,
        quantity,
        massGrams,
        capabilityFingerprint)
    {
    }

    public ProductionOutcomeLineSnapshot(
        string outputLineId,
        IReadOnlyList<string> commitIds,
        string itemId,
        int quantity,
        long massGrams,
        string capabilityFingerprint)
    {
        OutputLineId = GameplayOutcomeStableIdSyntax.Require(
            outputLineId,
            nameof(outputLineId));
        string[] orderedCommitIds = (commitIds
                ?? throw new ArgumentNullException(nameof(commitIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (orderedCommitIds.Length == 0
            || orderedCommitIds.Any(value =>
                !GameplayOutcomeStableIdSyntax.IsValid(value))
            || orderedCommitIds.Distinct(StringComparer.Ordinal).Count()
                != orderedCommitIds.Length)
        {
            throw new ArgumentException(
                "Unique canonical output commit IDs are required.",
                nameof(commitIds));
        }
        CommitIds = Array.AsReadOnly(orderedCommitIds);
        LineCommitId = orderedCommitIds[0];
        ItemId = GameplayOutcomeStableIdSyntax.Require(itemId, nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (massGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(massGrams));
        if (capabilityFingerprint == null
            || capabilityFingerprint.Length != 64
            || capabilityFingerprint.Any(character =>
                !((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f'))))
        {
            throw new ArgumentException(
                "A lowercase capability SHA-256 is required.",
                nameof(capabilityFingerprint));
        }
        Quantity = quantity;
        MassGrams = massGrams;
        CapabilityFingerprint = capabilityFingerprint;
    }

    public string OutputLineId { get; }
    public string LineCommitId { get; }
    public IReadOnlyList<string> CommitIds { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string CapabilityFingerprint { get; }
}

public readonly struct ProductionOutcomePhysicalStackSnapshot
{
    public ProductionOutcomePhysicalStackSnapshot(
        string stackId,
        string outputLineId,
        string itemId,
        int quantity,
        long massGrams,
        string itemInstanceId,
        string sourceCommitId = "",
        string componentSignature = "")
    {
        StackId = GameplayOutcomeStableIdSyntax.Require(stackId, nameof(stackId));
        OutputLineId = GameplayOutcomeStableIdSyntax.Require(
            outputLineId,
            nameof(outputLineId));
        ItemId = GameplayOutcomeStableIdSyntax.Require(itemId, nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (massGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(massGrams));
        if (!string.IsNullOrEmpty(itemInstanceId)
            && !GameplayOutcomeStableIdSyntax.IsValid(itemInstanceId))
        {
            throw new ArgumentException(
                "The optional item-instance ID is not canonical.",
                nameof(itemInstanceId));
        }
        if (!string.IsNullOrEmpty(sourceCommitId)
            && !GameplayOutcomeStableIdSyntax.IsValid(sourceCommitId))
        {
            throw new ArgumentException(
                "The optional source commit ID is not canonical.",
                nameof(sourceCommitId));
        }
        if (!string.IsNullOrEmpty(componentSignature)
            && (componentSignature.Length != 64
                || componentSignature.Any(character =>
                    !((character >= '0' && character <= '9')
                        || (character >= 'a' && character <= 'f')))))
        {
            throw new ArgumentException(
                "The optional component signature must be a lowercase SHA-256.",
                nameof(componentSignature));
        }
        Quantity = quantity;
        MassGrams = massGrams;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        SourceCommitId = sourceCommitId ?? string.Empty;
        ComponentSignature = componentSignature ?? string.Empty;
    }

    public string StackId { get; }
    public string OutputLineId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string ItemInstanceId { get; }
    public string SourceCommitId { get; }
    public string ComponentSignature { get; }
}

public readonly struct CombatDamageOutcomeReceipt
{
    public CombatDamageOutcomeReceipt(
        string attackOperationId,
        long attackRevision,
        CharacterId attackerId,
        KoreanNameSnapshot attackerDisplayName,
        CharacterId victimId,
        KoreanNameSnapshot victimDisplayName,
        CombatDamageType damageType,
        CombatBodyPart bodyPart,
        float actualDamage,
        int absoluteDay,
        Vector2Int victimCell)
    {
        if (!GameplayOutcomeStableIdSyntax.IsValid(attackOperationId))
            throw new ArgumentException("A canonical attack operation ID is required.", nameof(attackOperationId));
        if (attackRevision < 0L)
            throw new ArgumentOutOfRangeException(nameof(attackRevision));
        if (!attackerId.IsValid)
            throw new ArgumentException("A valid attacker ID is required.", nameof(attackerId));
        if (!victimId.IsValid)
            throw new ArgumentException("A valid victim ID is required.", nameof(victimId));
        if (!float.IsFinite(actualDamage) || actualDamage <= 0f)
            throw new ArgumentOutOfRangeException(nameof(actualDamage));
        if (absoluteDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        AttackOperationId = attackOperationId;
        AttackRevision = attackRevision;
        AttackerId = attackerId;
        AttackerDisplayName = attackerDisplayName;
        VictimId = victimId;
        VictimDisplayName = victimDisplayName;
        DamageType = damageType;
        BodyPart = bodyPart;
        ActualDamage = actualDamage;
        AbsoluteDay = absoluteDay;
        VictimCell = victimCell;
    }

    public string AttackOperationId { get; }
    public long AttackRevision { get; }
    public CharacterId AttackerId { get; }
    public KoreanNameSnapshot AttackerDisplayName { get; }
    public CharacterId VictimId { get; }
    public KoreanNameSnapshot VictimDisplayName { get; }
    public CombatDamageType DamageType { get; }
    public CombatBodyPart BodyPart { get; }
    public float ActualDamage { get; }
    public int AbsoluteDay { get; }
    public Vector2Int VictimCell { get; }
    public GameplayOperationId OperationId => new GameplayOperationId(AttackOperationId);
    public GameplayResultKey ResultKey => new GameplayResultKey(
        ProductionCombatOutcomeIds.CombatProducerId,
        OperationId,
        AttackRevision,
        0);
}

public readonly struct ReservedCombatDamageOutcome
{
    internal ReservedCombatDamageOutcome(
        PreparedOutcomeReservation reservation,
        string operationId,
        long attackRevision,
        PreparedMigratedProducerOutcome attackOutcome = default)
    {
        Reservation = reservation;
        OperationId = operationId ?? string.Empty;
        AttackRevision = attackRevision;
        AttackOutcome = attackOutcome;
    }

    internal PreparedOutcomeReservation Reservation { get; }
    internal PreparedMigratedProducerOutcome AttackOutcome { get; }
    public string OperationId { get; }
    public long AttackRevision { get; }
    public bool HasDamageReservation => Reservation.IsValid;
    public bool HasAttackOutcome => AttackOutcome.IsValid;
    public bool IsValid => (HasDamageReservation || HasAttackOutcome)
        && GameplayOutcomeStableIdSyntax.IsValid(OperationId)
        && AttackRevision >= 0L;

    internal ReservedCombatDamageOutcome WithAttackOutcome(
        in PreparedMigratedProducerOutcome attackOutcome) => new(
            Reservation,
            OperationId,
            AttackRevision,
            attackOutcome);
}

public readonly struct CombatOutcomeApplyResult
{
    private CombatOutcomeApplyResult(bool succeeded, string failureReason)
    {
        Succeeded = succeeded;
        FailureReason = failureReason ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string FailureReason { get; }
    public static CombatOutcomeApplyResult Success() =>
        new CombatOutcomeApplyResult(true, string.Empty);
    public static CombatOutcomeApplyResult Failed(string failureReason) =>
        new CombatOutcomeApplyResult(false, failureReason);
}

public readonly struct CombatOutcomeMechanicalMutation
{
    public CombatOutcomeMechanicalMutation(
        Func<string> apply,
        Action rollback,
        Action complete)
    {
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
        Rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        Complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    public Func<string> Apply { get; }
    public Action Rollback { get; }
    public Action Complete { get; }
    public bool IsValid => Apply != null && Rollback != null && Complete != null;
}

public static class ProductionCombatOutcomeIds
{
    public const string ProductionProducerId = "production.recipe-cycle";
    public const string CombatProducerId = "combat.character-damage";

    public static readonly GameplayOutcomeTypeId ProductionCompleted =
        new GameplayOutcomeTypeId("production.completed");
    public static readonly GameplayOutcomeTypeId CombatDamageResolved =
        new GameplayOutcomeTypeId("combat.damage-resolved");
    public static readonly GameplayEntityKindId CharacterKind =
        new GameplayEntityKindId("character");
    public static readonly GameplayEntityKindId FacilityKind =
        new GameplayEntityKindId("facility");
    public static readonly GameplayEntityKindId RecipeKind =
        new GameplayEntityKindId("recipe");
    public static readonly GameplayEntityKindId ItemKind =
        new GameplayEntityKindId("item-definition");
    public static readonly GameplayRoleId ProducerRole =
        new GameplayRoleId("producer");
    public static readonly GameplayRoleId WorkerRole =
        new GameplayRoleId("worker");
    public static readonly GameplayRoleId AttackerRole =
        new GameplayRoleId("attacker");
    public static readonly GameplayRoleId VictimRole =
        new GameplayRoleId("victim");
    public static readonly GameplayMetricId ProducedQuantityMetric =
        new GameplayMetricId("item.produced-count");
    public static readonly GameplayMetricId ProducedMassMetric =
        new GameplayMetricId("item.produced-mass");
    public static readonly GameplayMetricId DeclaredLossMetric =
        new GameplayMetricId("production.declared-loss-mass");
    public static readonly GameplayMetricId DeclaredExternalInputMetric =
        new GameplayMetricId("production.declared-external-input-mass");
    public static readonly GameplayMetricId WipInputQuantityMetric =
        new GameplayMetricId("production.wip-input-count");
    public static readonly GameplayMetricId WipInputMassMetric =
        new GameplayMetricId("production.wip-input-mass");
    public static readonly GameplayMetricId CleanWaterMetric =
        new GameplayMetricId("production.clean-water-input-mass");
    public static readonly GameplayMetricId WastewaterMetric =
        new GameplayMetricId("production.wastewater-output-mass");
    public static readonly GameplayMetricId DamageMetric =
        new GameplayMetricId("health.damage");
    public static readonly GameplayMetricUnitId CountUnit =
        new GameplayMetricUnitId("count");
    public static readonly GameplayMetricUnitId GramUnit =
        new GameplayMetricUnitId("gram");
    public static readonly GameplayMetricUnitId HealthPointUnit =
        new GameplayMetricUnitId("health-point");
    public static readonly GameplayOutcomeTagId ProductionTag =
        new GameplayOutcomeTagId("production");
    public static readonly GameplayOutcomeTagId CombatTag =
        new GameplayOutcomeTagId("combat");
    public static readonly GameplayOutcomeFactId DamageTypeFact =
        new GameplayOutcomeFactId("damage-type");
    public static readonly GameplayOutcomeFactId BodyPartFact =
        new GameplayOutcomeFactId("body-part");
}

public static class ProductionCombatOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(
        string displayText,
        string stableEntityId)
    {
        string display = displayText?.Trim() ?? string.Empty;
        string stableId = stableEntityId?.Trim() ?? string.Empty;
        if (display.Length == 0
            || !GameplayOutcomeStableIdSyntax.IsValid(stableId))
        {
            throw new ArgumentException(
                "An immutable display name and canonical entity ID are required.");
        }
        return new KoreanNameSnapshot(
            display,
            "production-combat-display-v1:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    stableId + "|" + display),
            KoreanPronunciationHint.AutoHangulDisplay(
                "production-combat-korean-v1"),
            "ko-KR");
    }
}
