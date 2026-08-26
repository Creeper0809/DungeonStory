using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionBillStatus
{
    WaitingForMaterials = 0,
    Ready = 1,
    InProgress = 2,
    Suspended = 3,
    Completed = 4,
    Cancelled = 5,
    WaitingForSupports = 6,
    WaitingForUtilities = 7,
    Processing = 8,
    WaitingForFinishing = 9,
    WaitingForOutputSpace = 10,
    WaitingForStockSensor = 11,
    WaitingForDistributionRoute = 12,
    WaitingForEligibleWorker = 13
}

public enum ProductionBatchStage
{
    None = 0,
    Preparing = 1,
    Processing = 2,
    Finishing = 3
}

public enum ProductionPreparedOutputPhase
{
    Unresolved = 0,
    ResolvedWaitingForOutputSpace = 1,
    PublicationPrepared = 2,
    PhysicalBatchCommittedPublicationPending = 3,
    Completed = 4
}

public enum ProductionPreparedPhysicalCandidateState
{
    FacilityOutputBuffer = 0
}

public readonly struct ProductionBillId : IPersistentEntityId, IEquatable<ProductionBillId>
{
    private readonly string value;

    public ProductionBillId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "production-bill");
    public bool Equals(ProductionBillId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is ProductionBillId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static bool operator ==(ProductionBillId left, ProductionBillId right) =>
        left.Equals(right);
    public static bool operator !=(ProductionBillId left, ProductionBillId right) =>
        !left.Equals(right);
    public static explicit operator ProductionBillId(string value) => new(value);
}

public enum ProductionBillOutcomeCode
{
    None = 0,
    BillAdded,
    BillRemoved,
    BillUpdated,
    StockSensorDeliveryRequested,
    StockSensorInstalled,
    StockSensorRemoved,
    StockSensorAcknowledged,
    WorkProgressed,
    ProcessingStarted,
    CycleCompleted,
    OrderModeTransitionCompleted,
    MaterialPrefetchAdjusted
}

[Serializable]
public sealed class ProductionStatusSaveData
{
    public FailureCode code;
    public ProductionBillOutcomeCode outcome;
    public List<string> parameters = new();
}

public readonly struct ProductionLogisticsStatus
{
    public ProductionLogisticsStatus(
        ProductionBillOutcomeCode code,
        params string[] parameters)
    {
        Code = code;
        Parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
    }

    public ProductionBillOutcomeCode Code { get; }
    public IReadOnlyList<string> Parameters { get; }
    public bool HasStatus => Code != ProductionBillOutcomeCode.None;
    public static ProductionLogisticsStatus None =>
        new(ProductionBillOutcomeCode.None);
}

[Serializable]
public sealed class ProductionBillSaveData
{
    public string billId = string.Empty;
    public string recipeId = string.Empty;
    public string buildingInstanceId = string.Empty;
    public ProductionOrderMode mode;
    public int remainingCycles = 1;
    public int targetStock = 10;
    public int minimumReserve;
    public bool suspended;
    public bool materialsConsumed;
    public int cycleSequence = 1;
    public string wipInputCommitId = string.Empty;
    public int wipInputQuantity;
    public long wipInputMassGrams;
    public bool outputOutcomeResolved;
    public List<ProductionResolvedOutputSaveData> resolvedOutputs = new();
    public ProductionPreparedOutputBatchSaveData preparedOutput = new();
    public bool processFluidConsumed;
    public long processCleanWaterMassGrams;
    public long processWastewaterMassGrams;
    public List<ProductionWastewaterComponentSaveData>
        processWastewaterComponents = new();
    public List<ProductionManualWaterTransferSaveData> processManualWaterTransfers =
        new();
    public float completedWork;
    public ProductionBatchStage batchStage;
    public float remainingProcessingHours;
    public float batchIntegrity = 100f;
    public float utilityOutageHours;
    public float temperatureOutageHours;
    public string occupiedSupportNodeId = string.Empty;
    public ProductionStatusSaveData blocked = new();
    public string reservedWorkerId = string.Empty;
    public string materialDestinationId = string.Empty;
    public int prefetchBatchCount = 1;
    public float estimatedDeliverySeconds = 12f;
    public float estimatedProductionCycleSeconds;
    public ProductionStatusSaveData logistics = new();
    public List<string> allowedMaterialIds = new List<string>();
    public List<string> allowedWorkerIds = new List<string>();
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone(WorkerCandidateSortMode.Fastest);
    public string emergencyWorkerId = string.Empty;
    public List<CraftContributionSaveData> workerContributions = new();
    public bool hasPendingModeTransition;
    public ProductionOrderMode pendingMode;
    public string outputDestinationId = string.Empty;
    public List<ProductionOutputReservationSaveData> outputReservations = new();
    public ProductionDistributionMode distributionMode =
        ProductionDistributionMode.DemandWeighted;
    public List<ProductionConsumerRoutePolicy> routePolicies = new();
    public List<ProductionSelectedSupplySaveData> selectedSupplies = new();
}

[Serializable]
public sealed class ProductionResolvedOutputSaveData
{
    public string itemId = string.Empty;
    public int amount;
    public int committedAmount;
    public long committedMassGrams;
    public string pendingCommitId = string.Empty;
    public bool pendingCommitApplied;
    public float qualityModifier;
    public float workerQuality;

    public ProductionResolvedOutputSaveData Clone() => new()
    {
        itemId = itemId,
        amount = amount,
        committedAmount = committedAmount,
        committedMassGrams = committedMassGrams,
        pendingCommitId = pendingCommitId,
        pendingCommitApplied = pendingCommitApplied,
        qualityModifier = qualityModifier,
        workerQuality = workerQuality
    };
}

[Serializable]
public sealed class ProductionPreparedOutputLineSaveData
{
    public string outputLineId = string.Empty;
    public ProductionOutputRole role;
    public string itemId = string.Empty;
    public int quantity;
    public string componentPayload = string.Empty;
    public string componentFingerprint = string.Empty;
    public int qualityPermille = 1000;
    public string rollKind = string.Empty;
    public long rollValue;
    public long rollUpperExclusive = 1L;
    public bool rollSucceeded = true;
    public long exactMassGrams;
    public string lineCommitId = string.Empty;

    public ProductionPreparedOutputLineSaveData Clone() => new()
    {
        outputLineId = outputLineId,
        role = role,
        itemId = itemId,
        quantity = quantity,
        componentPayload = componentPayload,
        componentFingerprint = componentFingerprint,
        qualityPermille = qualityPermille,
        rollKind = rollKind,
        rollValue = rollValue,
        rollUpperExclusive = rollUpperExclusive,
        rollSucceeded = rollSucceeded,
        exactMassGrams = exactMassGrams,
        lineCommitId = lineCommitId
    };
}

[Serializable]
public sealed class ProductionPreparedOutputPhysicalCandidateSaveData
{
    public string stackId = string.Empty;
    public string batchCommitId = string.Empty;
    public string outputLineId = string.Empty;
    public string lineCommitId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public long massGrams;
    public string destinationId = string.Empty;
    public ProductionPreparedPhysicalCandidateState state =
        ProductionPreparedPhysicalCandidateState.FacilityOutputBuffer;

    public ProductionPreparedOutputPhysicalCandidateSaveData Clone() => new()
    {
        stackId = stackId,
        batchCommitId = batchCommitId,
        outputLineId = outputLineId,
        lineCommitId = lineCommitId,
        itemId = itemId,
        quantity = quantity,
        massGrams = massGrams,
        destinationId = destinationId,
        state = state
    };
}

[Serializable]
public sealed class ProductionPreparedOutputBatchSaveData
{
    public const int CurrentSchemaVersion = 3;

    public int schemaVersion = CurrentSchemaVersion;
    public ProductionPreparedOutputPhase phase =
        ProductionPreparedOutputPhase.Unresolved;
    public string billId = string.Empty;
    public int cycleSequence;
    public string recipeId = string.Empty;
    public string destinationId = string.Empty;
    public string recipeDefinitionDigest = string.Empty;
    public string migrationProfileDigest = string.Empty;
    public string capacitySourceDigest = string.Empty;
    public int outputBufferCycleCapacity;
    public long projectedPortfolioCapacityGrams;
    public long requiredMinimumCapacityGrams;
    public string outcomeFingerprint = string.Empty;
    public string admissionFingerprint = string.Empty;
    public string batchCommitId = string.Empty;
    public long totalPhysicalMassGrams;
    public long totalDeclaredLossMassGrams;
    public List<ProductionPreparedOutputLineSaveData> lines = new();
    public List<ProductionPreparedOutputPhysicalCandidateSaveData>
        physicalCandidates = new();

    public ProductionPreparedOutputBatchSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        phase = phase,
        billId = billId,
        cycleSequence = cycleSequence,
        recipeId = recipeId,
        destinationId = destinationId,
        recipeDefinitionDigest = recipeDefinitionDigest,
        migrationProfileDigest = migrationProfileDigest,
        capacitySourceDigest = capacitySourceDigest,
        outputBufferCycleCapacity = outputBufferCycleCapacity,
        projectedPortfolioCapacityGrams = projectedPortfolioCapacityGrams,
        requiredMinimumCapacityGrams = requiredMinimumCapacityGrams,
        outcomeFingerprint = outcomeFingerprint,
        admissionFingerprint = admissionFingerprint,
        batchCommitId = batchCommitId,
        totalPhysicalMassGrams = totalPhysicalMassGrams,
        totalDeclaredLossMassGrams = totalDeclaredLossMassGrams,
        lines = (lines ?? new List<ProductionPreparedOutputLineSaveData>())
            .Select(value => value?.Clone())
            .ToList(),
        physicalCandidates = (physicalCandidates
                ?? new List<ProductionPreparedOutputPhysicalCandidateSaveData>())
            .Select(value => value?.Clone())
            .ToList()
    };

    public static ProductionPreparedOutputBatchSaveData Unresolved() => new();
}

public static class ProductionPreparedOutputIdentity
{
    public static string BuildBatchCommitId(
        ProductionBillId billId,
        int cycleSequence,
        string outcomeFingerprint) =>
        $"production-output-batch:{billId.Value}:{cycleSequence:D8}:{outcomeFingerprint}";

    public static string BuildLineCommitId(
        string batchCommitId,
        string outputLineId) =>
        $"{batchCommitId}:line:{outputLineId}";
}

public static class ProductionPreparedOutputContract
{
    public static void ValidateForBill(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionBillId expectedBillId,
        string expectedRecipeId,
        int expectedCycleSequence,
        string expectedDestinationId)
    {
        if (batch == null)
        {
            throw new InvalidOperationException(
                "Prepared production output batch is missing.");
        }
        if (!Enum.IsDefined(typeof(ProductionPreparedOutputPhase), batch.phase)
            || batch.schemaVersion !=
                ProductionPreparedOutputBatchSaveData.CurrentSchemaVersion
            || batch.lines == null
            || batch.physicalCandidates == null)
        {
            throw new InvalidOperationException(
                "Prepared production output batch has an invalid schema or phase.");
        }

        if (batch.phase == ProductionPreparedOutputPhase.Unresolved)
        {
            ValidateUnresolved(batch);
            return;
        }

        if (!expectedBillId.IsValid
            || !IsCanonical(expectedRecipeId)
            || expectedCycleSequence < 1
            || !IsCanonical(expectedDestinationId)
            || !string.Equals(batch.billId, expectedBillId.Value, StringComparison.Ordinal)
            || batch.cycleSequence != expectedCycleSequence
            || !string.Equals(batch.recipeId, expectedRecipeId, StringComparison.Ordinal)
            || !string.Equals(
                batch.destinationId,
                expectedDestinationId,
                StringComparison.Ordinal)
            || !IsDigest(batch.recipeDefinitionDigest)
            || !IsDigest(batch.migrationProfileDigest)
            || !IsDigest(batch.capacitySourceDigest)
            || batch.outputBufferCycleCapacity is < 2 or > 4
            || batch.projectedPortfolioCapacityGrams <= 0L
            || batch.requiredMinimumCapacityGrams
                != Math.Max(
                    batch.projectedPortfolioCapacityGrams,
                    checked(
                        batch.totalPhysicalMassGrams
                        * batch.outputBufferCycleCapacity))
            || !IsDigest(batch.outcomeFingerprint)
            || !string.Equals(
                batch.batchCommitId,
                ProductionPreparedOutputIdentity.BuildBatchCommitId(
                    expectedBillId,
                    expectedCycleSequence,
                    batch.outcomeFingerprint),
                StringComparison.Ordinal)
            || batch.totalPhysicalMassGrams <= 0L
            || batch.totalDeclaredLossMassGrams < 0L
            || batch.lines.Count == 0)
        {
            throw new InvalidOperationException(
                "Prepared production output batch identity or totals conflict with its bill.");
        }

        bool admissionRequired = batch.phase is
            ProductionPreparedOutputPhase.PublicationPrepared
            or ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
            or ProductionPreparedOutputPhase.Completed;
        bool canonicalAdmission = admissionRequired
            ? IsDigest(batch.admissionFingerprint)
            : batch.admissionFingerprint != null
                && batch.admissionFingerprint.Length == 0;
        if (!canonicalAdmission)
        {
            throw new InvalidOperationException(
                "Prepared production output batch has an inconsistent admission fingerprint.");
        }

        ValidateLines(batch);
        ValidateCandidates(batch);
    }

    private static void ValidateUnresolved(
        ProductionPreparedOutputBatchSaveData batch)
    {
        if (batch.billId == null
            || batch.billId.Length != 0
            || batch.cycleSequence != 0
            || batch.recipeId == null
            || batch.recipeId.Length != 0
            || batch.destinationId == null
            || batch.destinationId.Length != 0
            || batch.recipeDefinitionDigest == null
            || batch.recipeDefinitionDigest.Length != 0
            || batch.migrationProfileDigest == null
            || batch.migrationProfileDigest.Length != 0
            || batch.capacitySourceDigest == null
            || batch.capacitySourceDigest.Length != 0
            || batch.outputBufferCycleCapacity != 0
            || batch.projectedPortfolioCapacityGrams != 0L
            || batch.requiredMinimumCapacityGrams != 0L
            || batch.outcomeFingerprint == null
            || batch.outcomeFingerprint.Length != 0
            || batch.admissionFingerprint == null
            || batch.admissionFingerprint.Length != 0
            || batch.batchCommitId == null
            || batch.batchCommitId.Length != 0
            || batch.totalPhysicalMassGrams != 0L
            || batch.totalDeclaredLossMassGrams != 0L
            || batch.lines.Count != 0
            || batch.physicalCandidates.Count != 0)
        {
            throw new InvalidOperationException(
                "Unresolved production output contains prepared authority.");
        }
    }

    private static void ValidateLines(ProductionPreparedOutputBatchSaveData batch)
    {
        string previousLineId = string.Empty;
        HashSet<string> lineCommitIds = new(StringComparer.Ordinal);
        int mainCount = 0;
        long physicalMass = 0L;
        long declaredLossMass = 0L;
        foreach (ProductionPreparedOutputLineSaveData line in batch.lines)
        {
            bool roleDefined = line != null
                && Enum.IsDefined(typeof(ProductionOutputRole), line.role);
            bool declaredLoss = roleDefined
                && line.role == ProductionOutputRole.DeclaredLoss;
            bool canonicalPhysical = line != null
                && IsCanonical(line.itemId)
                && (line.rollSucceeded
                    ? line.quantity > 0 && line.exactMassGrams > 0L
                    : line.quantity == 0 && line.exactMassGrams == 0L);
            bool canonicalLoss = line != null
                && line.itemId != null
                && line.itemId.Length == 0
                && line.quantity == 0
                && line.rollSucceeded
                && line.exactMassGrams > 0L;
            if (line == null
                || !roleDefined
                || !IsCanonicalOutputLineId(line.outputLineId)
                || previousLineId.Length > 0
                    && string.CompareOrdinal(previousLineId, line.outputLineId) >= 0
                || !IsDigest(line.componentFingerprint)
                || line.componentPayload == null
                || line.componentPayload.Length > 0
                    && !string.Equals(
                        line.componentPayload,
                        line.componentPayload.Trim(),
                        StringComparison.Ordinal)
                || line.qualityPermille < 0
                || line.qualityPermille > 2000
                || !IsCanonicalToken(line.rollKind)
                || line.rollUpperExclusive <= 0L
                || line.rollValue < 0L
                || line.rollValue >= line.rollUpperExclusive
                || (declaredLoss ? !canonicalLoss : !canonicalPhysical)
                || !string.Equals(
                    line.lineCommitId,
                    ProductionPreparedOutputIdentity.BuildLineCommitId(
                        batch.batchCommitId,
                        line.outputLineId),
                    StringComparison.Ordinal)
                || !lineCommitIds.Add(line.lineCommitId))
            {
                throw new InvalidOperationException(
                    "Prepared production output contains an invalid, duplicate, or unordered line.");
            }
            previousLineId = line.outputLineId;
            if (line.role == ProductionOutputRole.Main)
            {
                mainCount++;
            }
            if (declaredLoss)
            {
                declaredLossMass = checked(
                    declaredLossMass + line.exactMassGrams);
            }
            else
            {
                physicalMass = checked(physicalMass + line.exactMassGrams);
            }
        }
        if (mainCount != 1
            || physicalMass != batch.totalPhysicalMassGrams
            || declaredLossMass != batch.totalDeclaredLossMassGrams)
        {
            throw new InvalidOperationException(
                "Prepared production output line totals or Main authority are inconsistent.");
        }
    }

    private static void ValidateCandidates(
        ProductionPreparedOutputBatchSaveData batch)
    {
        bool physicalRequired = batch.phase is
            ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
            or ProductionPreparedOutputPhase.Completed;
        if (!physicalRequired)
        {
            if (batch.physicalCandidates.Count != 0)
            {
                throw new InvalidOperationException(
                    "Prepared production output has physical candidates before publication.");
            }
            return;
        }

        Dictionary<string, ProductionPreparedOutputLineSaveData> physicalLines =
            batch.lines
                .Where(line => line.role != ProductionOutputRole.DeclaredLoss
                    && line.quantity > 0)
                .ToDictionary(line => line.outputLineId, StringComparer.Ordinal);
        Dictionary<string, (int Quantity, long Mass)> published =
            new(StringComparer.Ordinal);
        HashSet<string> stackIds = new(StringComparer.Ordinal);
        string previousStackId = string.Empty;
        foreach (ProductionPreparedOutputPhysicalCandidateSaveData candidate in
                 batch.physicalCandidates)
        {
            if (candidate == null
                || !IsCanonical(candidate.stackId)
                || !stackIds.Add(candidate.stackId)
                || previousStackId.Length > 0
                    && string.CompareOrdinal(previousStackId, candidate.stackId) >= 0
                || !physicalLines.TryGetValue(
                    candidate.outputLineId,
                    out ProductionPreparedOutputLineSaveData line)
                || !string.Equals(
                    candidate.batchCommitId,
                    batch.batchCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    candidate.lineCommitId,
                    line.lineCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(candidate.itemId, line.itemId, StringComparison.Ordinal)
                || candidate.quantity <= 0
                || candidate.massGrams <= 0L
                || !string.Equals(
                    candidate.destinationId,
                    batch.destinationId,
                    StringComparison.Ordinal)
                || candidate.state !=
                    ProductionPreparedPhysicalCandidateState.FacilityOutputBuffer)
            {
                throw new InvalidOperationException(
                    "Prepared production output has an invalid or duplicate physical candidate.");
            }
            previousStackId = candidate.stackId;
            published.TryGetValue(
                candidate.outputLineId,
                out (int Quantity, long Mass) aggregate);
            published[candidate.outputLineId] = (
                checked(aggregate.Quantity + candidate.quantity),
                checked(aggregate.Mass + candidate.massGrams));
        }
        if (physicalLines.Count != published.Count
            || physicalLines.Any(pair =>
                !published.TryGetValue(pair.Key, out var value)
                || value.Quantity != pair.Value.quantity
                || value.Mass != pair.Value.exactMassGrams))
        {
            throw new InvalidOperationException(
                "Prepared production output physical candidates are partial, missing, or extra.");
        }
    }

    private static bool IsCanonicalOutputLineId(string value) =>
        ProductionOutputDefinition.IsCanonicalOutputLineId(value);

    private static bool IsCanonicalToken(string value)
    {
        if (!IsCanonical(value))
        {
            return false;
        }
        foreach (char c in value)
        {
            if (!(c is >= 'a' and <= 'z')
                && !(c is >= '0' and <= '9')
                && c != ':'
                && c != '-'
                && c != '_'
                && c != '.')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsDigest(string value)
    {
        if (value == null || value.Length != 64)
        {
            return false;
        }
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            if (!(c is >= '0' and <= '9')
                && !(c is >= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}

public enum ProductionWipTerminalReason
{
    Cancelled = 0,
    FacilityDestroyed = 1
}

public enum ProductionWipTerminalLossKind
{
    ExplicitIrrecoverableProcessLoss = 0
}

public static class ProductionFluidMassRules
{
    public const long GramsPerAuthoredUnit = 500L;

    public static long ToMassGrams(float authoredUnits)
    {
        if (float.IsNaN(authoredUnits)
            || float.IsInfinity(authoredUnits)
            || authoredUnits < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(authoredUnits));
        }

        double grams = authoredUnits * GramsPerAuthoredUnit;
        if (grams > long.MaxValue)
        {
            throw new OverflowException(
                "Production process-fluid mass exceeds Int64 authority.");
        }
        return checked((long)Math.Round(
            grams,
            MidpointRounding.AwayFromZero));
    }
}

public enum ProcessWastewaterComposition
{
    None = 0,
    SanitaryWashwater = 1,
    FoodProcessWashwater = 2,
    Whey = 3,
    Brine = 4,
    FermentationEffluent = 5,
    MedicalEffluent = 6,
    IndustrialEffluent = 7,
    AgriculturalRunoff = 8
}

public enum ProcessWastewaterSourceKind
{
    Recipe = 0,
    Facility = 1,
    Support = 2
}

public readonly struct ProcessWastewaterComponent
{
    public ProcessWastewaterComponent(
        ProcessWastewaterComposition composition,
        ProcessWastewaterSourceKind sourceKind,
        string sourceStableId,
        float authoredUnits)
    {
        if (composition == ProcessWastewaterComposition.None
            || !Enum.IsDefined(typeof(ProcessWastewaterComposition), composition))
        {
            throw new ArgumentOutOfRangeException(nameof(composition));
        }
        if (!Enum.IsDefined(typeof(ProcessWastewaterSourceKind), sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind));
        }
        if (string.IsNullOrEmpty(sourceStableId)
            || !string.Equals(
                sourceStableId,
                sourceStableId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Wastewater source stable ID must be canonical.",
                nameof(sourceStableId));
        }
        if (float.IsNaN(authoredUnits)
            || float.IsInfinity(authoredUnits)
            || authoredUnits <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(authoredUnits));
        }

        Composition = composition;
        SourceKind = sourceKind;
        SourceStableId = sourceStableId;
        AuthoredUnits = authoredUnits;
        MassGrams = ProductionFluidMassRules.ToMassGrams(authoredUnits);
    }

    public ProcessWastewaterComposition Composition { get; }
    public ProcessWastewaterSourceKind SourceKind { get; }
    public string SourceStableId { get; }
    public float AuthoredUnits { get; }
    public long MassGrams { get; }
}

[Serializable]
public sealed class ProductionWastewaterComponentSaveData
{
    public ProcessWastewaterComposition composition;
    public ProcessWastewaterSourceKind sourceKind;
    public string sourceStableId = string.Empty;
    public float authoredUnits;
    public long massGrams;

    public ProductionWastewaterComponentSaveData Clone() => new()
    {
        composition = composition,
        sourceKind = sourceKind,
        sourceStableId = sourceStableId,
        authoredUnits = authoredUnits,
        massGrams = massGrams
    };

    public ProcessWastewaterComponent ToRuntime() => new(
        composition,
        sourceKind,
        sourceStableId,
        authoredUnits);

    public static ProductionWastewaterComponentSaveData FromRuntime(
        ProcessWastewaterComponent value) => new()
    {
        composition = value.Composition,
        sourceKind = value.SourceKind,
        sourceStableId = value.SourceStableId,
        authoredUnits = value.AuthoredUnits,
        massGrams = value.MassGrams
    };
}

[Serializable]
public sealed class ProductionManualWaterTransferSaveData
{
    public string operationId = string.Empty;
    public string physicalCommitId = string.Empty;
    public string destinationId = string.Empty;
    public float requestedWaterUnits;
    public int transferredWaterUnits;
    public long inputMassGrams;
    public List<string> sourceStackIds = new();

    public ProductionManualWaterTransferSaveData Clone() => new()
    {
        operationId = operationId,
        physicalCommitId = physicalCommitId,
        destinationId = destinationId,
        requestedWaterUnits = requestedWaterUnits,
        transferredWaterUnits = transferredWaterUnits,
        inputMassGrams = inputMassGrams,
        sourceStackIds = new List<string>(sourceStackIds ?? new List<string>())
    };
}

public readonly struct ProductionProcessFluidReceipt
{
    public ProductionProcessFluidReceipt(
        long cleanWaterMassGrams,
        long wastewaterMassGrams,
        IReadOnlyList<ProductionManualWaterTransferSaveData> manualWaterTransfers = null,
        IReadOnlyList<ProcessWastewaterComponent> wastewaterComponents = null)
    {
        if (cleanWaterMassGrams < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(cleanWaterMassGrams));
        }
        if (wastewaterMassGrams < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(wastewaterMassGrams));
        }
        CleanWaterMassGrams = cleanWaterMassGrams;
        WastewaterMassGrams = wastewaterMassGrams;
        ManualWaterTransfers = (manualWaterTransfers
                ?? Array.Empty<ProductionManualWaterTransferSaveData>())
            .Select(value => value.Clone())
            .ToArray();
        WastewaterComponents = (wastewaterComponents
                ?? Array.Empty<ProcessWastewaterComponent>())
            .OrderBy(value => (int)value.Composition)
            .ThenBy(value => (int)value.SourceKind)
            .ThenBy(value => value.SourceStableId, StringComparer.Ordinal)
            .ToArray();

        long componentMass = WastewaterComponents.Aggregate(
            0L,
            (total, value) => checked(total + value.MassGrams));
        if (componentMass != wastewaterMassGrams)
        {
            throw new ArgumentException(
                "Wastewater component mass must equal the aggregate wastewater mass.",
                nameof(wastewaterComponents));
        }
    }

    public long CleanWaterMassGrams { get; }
    public long WastewaterMassGrams { get; }
    public IReadOnlyList<ProductionManualWaterTransferSaveData>
        ManualWaterTransfers { get; }
    public IReadOnlyList<ProcessWastewaterComponent> WastewaterComponents { get; }
}

[Serializable]
public sealed class ProductionWipTerminalReceiptSaveData
{
    public string commitId = string.Empty;
    public string billId = string.Empty;
    public string recipeId = string.Empty;
    public string buildingInstanceId = string.Empty;
    public int cycleSequence;
    public string inputCommitId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public long processCleanWaterMassGrams;
    public long processWastewaterMassGrams;
    public List<ProductionWastewaterComponentSaveData> wastewaterComponents = new();
    public long committedOutputMassGrams;
    public ProductionWipTerminalReason reason;
    public ProductionWipTerminalLossKind lossKind =
        ProductionWipTerminalLossKind.ExplicitIrrecoverableProcessLoss;
    public long declaredLossMassGrams;

    public ProductionWipTerminalReceiptSaveData Clone() => new()
    {
        commitId = commitId,
        billId = billId,
        recipeId = recipeId,
        buildingInstanceId = buildingInstanceId,
        cycleSequence = cycleSequence,
        inputCommitId = inputCommitId,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        processCleanWaterMassGrams = processCleanWaterMassGrams,
        processWastewaterMassGrams = processWastewaterMassGrams,
        wastewaterComponents = (wastewaterComponents
                ?? new List<ProductionWastewaterComponentSaveData>())
            .Select(value => value.Clone())
            .ToList(),
        committedOutputMassGrams = committedOutputMassGrams,
        reason = reason,
        lossKind = lossKind,
        declaredLossMassGrams = declaredLossMassGrams
    };
}

public readonly struct ProductionWipInputReceipt
{
    public ProductionWipInputReceipt(
        string commitId,
        int quantity,
        long inputMassGrams)
    {
        CommitId = commitId ?? string.Empty;
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
    }

    public string CommitId { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public bool IsCommitted => CommitId.Length > 0
        && Quantity > 0
        && InputMassGrams > 0L;
}

public enum ProductionStockSensorCommitPhase
{
    None = 0,
    InputCommitted = 1,
    OutcomePublished = 2
}

[Serializable]
public sealed class ProductionStockSensorPhysicalCommitSaveData
{
    public ProductionStockSensorCommitPhase phase;
    public string facilityId = string.Empty;
    public string itemId = string.Empty;
    public string destinationId = string.Empty;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string requestFingerprint = string.Empty;
    public string commitId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public List<string> sourceStackIds = new();

    public ProductionStockSensorPhysicalCommitSaveData Clone() => new()
    {
        phase = phase,
        facilityId = facilityId,
        itemId = itemId,
        destinationId = destinationId,
        operationId = operationId,
        reasonCode = reasonCode,
        requestFingerprint = requestFingerprint,
        commitId = commitId,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        sourceStackIds = new List<string>(sourceStackIds ?? new List<string>())
    };
}

[Serializable]
public sealed class ProductionInstalledStockSensorSaveData
{
    public string facilityId = string.Empty;
    public string itemId = string.Empty;
    public string inputOperationId = string.Empty;
    public string inputCommitId = string.Empty;
    public string inputSourceStackId = string.Empty;
    public long embeddedMassGrams;

    public ProductionInstalledStockSensorSaveData Clone() => new()
    {
        facilityId = facilityId,
        itemId = itemId,
        inputOperationId = inputOperationId,
        inputCommitId = inputCommitId,
        inputSourceStackId = inputSourceStackId,
        embeddedMassGrams = embeddedMassGrams
    };
}

public enum ProductionStockSensorRemovalPhase
{
    Prepared = 0,
    OutputPublished = 1
}

[Serializable]
public sealed class ProductionStockSensorRemovalSaveData
{
    public ProductionStockSensorRemovalPhase phase;
    public string facilityId = string.Empty;
    public string itemId = string.Empty;
    public int outputPositionX;
    public int outputPositionY;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string installationSourceStackId = string.Empty;
    public long expectedOutputMassGrams;
    public int outputQuantity;
    public long outputMassGrams;
    public List<string> outputCommitIds = new();

    public ProductionStockSensorRemovalSaveData Clone() => new()
    {
        phase = phase,
        facilityId = facilityId,
        itemId = itemId,
        outputPositionX = outputPositionX,
        outputPositionY = outputPositionY,
        operationId = operationId,
        reasonCode = reasonCode,
        installationSourceStackId = installationSourceStackId,
        expectedOutputMassGrams = expectedOutputMassGrams,
        outputQuantity = outputQuantity,
        outputMassGrams = outputMassGrams,
        outputCommitIds = new List<string>(
            outputCommitIds ?? new List<string>())
    };
}

public readonly struct ProductionStockSensorRemovalReceipt
{
    public ProductionStockSensorRemovalReceipt(
        string operationId,
        string reasonCode,
        IReadOnlyList<string> outputCommitIds,
        int outputQuantity,
        long outputMassGrams)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        OutputCommitIds = (outputCommitIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        OutputQuantity = outputQuantity;
        OutputMassGrams = outputMassGrams;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public IReadOnlyList<string> OutputCommitIds { get; }
    public int OutputQuantity { get; }
    public long OutputMassGrams { get; }
    public bool IsCommitted => OperationId.Length > 0
        && ReasonCode.Length > 0
        && OutputCommitIds.Count > 0
        && OutputCommitIds.All(value => !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal))
        && OutputCommitIds.Distinct(StringComparer.Ordinal).Count()
            == OutputCommitIds.Count
        && OutputQuantity == 1
        && OutputMassGrams > 0L;
}

public interface IProductionStockSensorRemovalOutputGateway
{
    bool TryEnsureRemovalOutput(
        string itemId,
        Vector2Int outputPosition,
        string operationId,
        string reasonCode,
        out ProductionStockSensorRemovalReceipt receipt,
        out string failureReason);
}

public readonly struct ProductionStockSensorPhysicalReceipt
{
    public ProductionStockSensorPhysicalReceipt(
        string operationId,
        string reasonCode,
        string requestFingerprint,
        string commitId,
        int inputQuantity,
        long inputMassGrams,
        IReadOnlyList<string> sourceStackIds)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        CommitId = commitId ?? string.Empty;
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
        SourceStackIds = sourceStackIds ?? Array.Empty<string>();
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public string CommitId { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public bool IsCommitted => OperationId.Length > 0
        && ReasonCode.Length > 0
        && RequestFingerprint.Length > 0
        && CommitId.Length > 0
        && InputQuantity == 1
        && InputMassGrams > 0L
        && SourceStackIds.Count > 0;
}

[Serializable]
public sealed class ProductionOutputReservationSaveData
{
    public string itemId = string.Empty;
    public int amount;
}

[Serializable]
public sealed class ProductionSelectedSupplySaveData
{
    public string supplyKey = string.Empty;
    public string itemId = string.Empty;
}

[Serializable]
public sealed class DungeonProductionBillSaveData
{
    public const int CurrentVersion = 17;

    public int version = CurrentVersion;
    public int nextBillSequence = 1;
    public List<ProductionBillSaveData> bills = new List<ProductionBillSaveData>();
    public List<string> installedStockSensorFacilityIds = new List<string>();
    public List<string> acknowledgedStockSensorFacilityIds = new List<string>();
    public List<ProductionStockSensorPhysicalCommitSaveData>
        pendingStockSensorInstalls = new();
    public List<ProductionInstalledStockSensorSaveData>
        installedStockSensors = new();
    public List<ProductionStockSensorRemovalSaveData>
        pendingStockSensorRemovals = new();
    public List<ProductionWipTerminalReceiptSaveData> wipTerminalReceipts = new();
}

public sealed class ProductionBillSnapshot
{
    public ProductionBillId BillId { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public BuildingInstanceId BuildingInstanceId { get; set; }
    public Vector2Int Position { get; set; }
    public WorkTypeId WorkTypeId { get; set; }
    public ProductionOrderMode Mode { get; set; }
    public ProductionBillStatus Status { get; set; }
    public int RemainingCycles { get; set; }
    public int TargetStock { get; set; }
    public int MinimumReserve { get; set; }
    public float RequiredWork { get; set; }
    public float CompletedWork { get; set; }
    public bool MaterialsConsumed { get; set; }
    public bool ProcessFluidConsumed { get; set; }
    public ProductionBatchStage BatchStage { get; set; }
    public float RemainingProcessingHours { get; set; }
    public float BatchIntegrity { get; set; } = 100f;
    public float UtilityOutageHours { get; set; }
    public float TemperatureOutageHours { get; set; }
    public string OccupiedSupportNodeId { get; set; } = string.Empty;
    public string ReservedWorkerId { get; set; } = string.Empty;
    public WorkerSelectionPolicySaveData WorkerPolicy { get; set; } =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.Fastest);
    public string EmergencyWorkerId { get; set; } = string.Empty;
    public string MaterialDestinationId { get; set; } = string.Empty;
    public DomainFailure BlockedFailure { get; set; } = DomainFailure.None;
    public int PrefetchBatchCount { get; set; } = 1;
    public float EstimatedDeliverySeconds { get; set; } = 12f;
    public float EstimatedProductionCycleSeconds { get; set; }
    public ProductionLogisticsStatus Logistics { get; set; } =
        ProductionLogisticsStatus.None;
    public IReadOnlyList<ItemAmountDefinition> Inputs { get; set; } =
        Array.Empty<ItemAmountDefinition>();
    public IReadOnlyList<ProductionOutputDefinition> Outputs { get; set; } =
        Array.Empty<ProductionOutputDefinition>();

    public float ProgressRatio => RequiredWork <= 0f
        ? 0f
        : Mathf.Clamp01(CompletedWork / RequiredWork);

    public float ProcessingProgressRatio { get; set; }
    public bool HasPendingModeTransition { get; set; }
    public ProductionOrderMode PendingMode { get; set; }
    public string OutputDestinationId { get; set; } = string.Empty;
    public int OutputBufferedQuantity { get; set; }
    public int ReservedOutputQuantity { get; set; }
    public int OutputCapacity { get; set; }
    public bool HasStockSensor { get; set; }
    public bool HasUnacknowledgedStockSensorUnlock { get; set; }
    public ProductionDistributionMode DistributionMode { get; set; }
    public IReadOnlyList<ProductionConsumerRoutePolicy> RoutePolicies { get; set; } =
        Array.Empty<ProductionConsumerRoutePolicy>();
    public IReadOnlyList<ProductionConsumerRouteState> RouteStates { get; set; } =
        Array.Empty<ProductionConsumerRouteState>();
}

public sealed class ProductionBillCommandResult
{
    private ProductionBillCommandResult(
        bool succeeded,
        ProductionBillId billId,
        ProductionBillOutcomeCode outcome,
        DomainFailure failure)
    {
        Succeeded = succeeded;
        BillId = billId;
        Outcome = outcome;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public ProductionBillId BillId { get; }
    public ProductionBillOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }

    public static ProductionBillCommandResult Success(
        ProductionBillId billId,
        ProductionBillOutcomeCode outcome = ProductionBillOutcomeCode.BillUpdated) =>
        new(true, billId, outcome, DomainFailure.None);

    public static ProductionBillCommandResult Failed(DomainFailure failure) =>
        new(false, default, ProductionBillOutcomeCode.None, failure);
}

public static class ProductionMaterialPrefetchPolicy
{
    public static int CalculateBatchCount(
        float estimatedDeliverySeconds,
        float safetySeconds,
        float effectiveProductionCycleSeconds,
        int maximumBatches = 3)
    {
        return Mathf.Clamp(
            Mathf.CeilToInt(
                (Mathf.Max(0f, estimatedDeliverySeconds)
                    + Mathf.Max(0f, safetySeconds))
                / Mathf.Max(0.1f, effectiveProductionCycleSeconds)),
            1,
            Mathf.Max(1, maximumBatches));
    }
}

public readonly struct ProductionWorkAvailabilityResult
{
    public ProductionWorkAvailabilityResult(
        bool available,
        DomainFailure failure,
        ProductionBillSnapshot bill = null)
    {
        Available = available;
        Failure = failure;
        Bill = bill;
    }

    public bool Available { get; }
    public DomainFailure Failure { get; }
    public ProductionBillSnapshot Bill { get; }
}

public readonly struct ProductionWorkBeginResult
{
    public ProductionWorkBeginResult(
        ProductionBillSnapshot bill,
        DomainFailure failure)
    {
        Bill = bill;
        Failure = failure;
    }

    public ProductionBillSnapshot Bill { get; }
    public DomainFailure Failure { get; }
    public bool Succeeded => Bill != null && !Failure.IsFailure;
}

public readonly struct ProductionWorkExecutionResult
{
    public ProductionWorkExecutionResult(
        bool succeeded,
        bool cycleCompleted,
        ProductionBillOutcomeCode outcome,
        DomainFailure failure,
        params string[] parameters)
    {
        Succeeded = succeeded;
        CycleCompleted = cycleCompleted;
        Outcome = outcome;
        Failure = failure;
        Parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
    }

    public bool Succeeded { get; }
    public bool CycleCompleted { get; }
    public ProductionBillOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }
    public IReadOnlyList<string> Parameters { get; }
}

public sealed class ProductionBillRestoreCandidate
{
    internal ProductionBillRestoreCandidate(ProductionAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Bills = State.Bills.AsReadOnly();
    }

    internal ProductionAggregateState State { get; }
    public IReadOnlyList<ProductionBillRecord> Bills { get; }

    public static ProductionBillRestoreCandidate Create(
        DungeonProductionBillSaveData snapshot,
        int billVersion,
        int stockSensorVersion)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }
        return new ProductionBillRestoreCandidate(
            ProductionAggregateStateSession.CreateRestoreState(
                snapshot,
                billVersion,
                stockSensorVersion));
    }
}

public interface IProductionBillPersistence
{
    DungeonProductionBillSaveData Capture();
    ProductionBillRestoreCandidate BuildRestore(
        DungeonProductionBillSaveData snapshot);
    void Restore(ProductionBillRestoreCandidate candidate);
}
