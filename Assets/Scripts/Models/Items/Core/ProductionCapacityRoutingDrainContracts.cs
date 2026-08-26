using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum ProductionCapacityRoutingDrainPhase
{
    Prepared = 0,
    RoutingRemainder = 1,
    QuiescingActors = 2,
    ReleasingOperationAuthority = 3,
    AwaitingStablePhysicalState = 4,
    AwaitingDurableCheckpointGc = 5,
    EffectCommittedAwaitingOwnerAck = 6,
    OwnerAcknowledgedAwaitingCheckpointGc = 7
}

public enum ProductionCapacityRoutingDrainStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

[Serializable]
public sealed class ProductionCapacityRoutingDrainLineSaveData
{
    public string lineCommitId = string.Empty;
    public string outputLineId = string.Empty;
    public string itemId = string.Empty;
    public string componentFingerprint = string.Empty;
    public int originalQuantity;
    public long originalMassGrams;
    public int remainingQuantity;
    public long remainingMassGrams;
    public int routedQuantity;
    public long routedMassGrams;

    public ProductionCapacityRoutingDrainLineSaveData Clone() => new()
    {
        lineCommitId = lineCommitId,
        outputLineId = outputLineId,
        itemId = itemId,
        componentFingerprint = componentFingerprint,
        originalQuantity = originalQuantity,
        originalMassGrams = originalMassGrams,
        remainingQuantity = remainingQuantity,
        remainingMassGrams = remainingMassGrams,
        routedQuantity = routedQuantity,
        routedMassGrams = routedMassGrams
    };
}

[Serializable]
public sealed class ProductionCapacityRoutingDrainRouteSaveData
{
    public string routeOperationId = string.Empty;
    public string requestFingerprint = string.Empty;
    public string physicalReceiptFingerprint = string.Empty;
    public int phase;
    public long currentDeliveryRevision;
    public string currentDeliveryRevisionFingerprint = string.Empty;
    public string currentTargetDestinationId = string.Empty;
    public string currentTargetAuthorityFingerprint = string.Empty;

    public ProductionCapacityRoutingDrainRouteSaveData Clone() => new()
    {
        routeOperationId = routeOperationId,
        requestFingerprint = requestFingerprint,
        physicalReceiptFingerprint = physicalReceiptFingerprint,
        phase = phase,
        currentDeliveryRevision = currentDeliveryRevision,
        currentDeliveryRevisionFingerprint = currentDeliveryRevisionFingerprint,
        currentTargetDestinationId = currentTargetDestinationId,
        currentTargetAuthorityFingerprint = currentTargetAuthorityFingerprint
    };
}

[Serializable]
public sealed class ProductionCapacityRoutingDrainSliceSaveData
{
    public string routeOperationId = string.Empty;
    public string sourceStackId = string.Empty;
    public string routedStackId = string.Empty;
    public string outputLineId = string.Empty;
    public string lineCommitId = string.Empty;
    public string itemId = string.Empty;
    public int sourceOffsetQuantity;
    public int routedOffsetQuantity;
    public int routedQuantity;
    public long routedMassGrams;
    public string componentFingerprint = string.Empty;

    public ProductionCapacityRoutingDrainSliceSaveData Clone() => new()
    {
        routeOperationId = routeOperationId,
        sourceStackId = sourceStackId,
        routedStackId = routedStackId,
        outputLineId = outputLineId,
        lineCommitId = lineCommitId,
        itemId = itemId,
        sourceOffsetQuantity = sourceOffsetQuantity,
        routedOffsetQuantity = routedOffsetQuantity,
        routedQuantity = routedQuantity,
        routedMassGrams = routedMassGrams,
        componentFingerprint = componentFingerprint
    };
}

[Serializable]
public sealed class ProductionCapacityRoutingDrainActorCarrySaveData
{
    public string actorPersistentId = string.Empty;
    public string haulIntentOperationId = string.Empty;
    public string routeOperationId = string.Empty;
    public string carriedStackId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;
    public long massGrams;
    public string stackSignature = string.Empty;

    public ProductionCapacityRoutingDrainActorCarrySaveData Clone() => new()
    {
        actorPersistentId = actorPersistentId,
        haulIntentOperationId = haulIntentOperationId,
        routeOperationId = routeOperationId,
        carriedStackId = carriedStackId,
        sourceStackId = sourceStackId,
        quantity = quantity,
        massGrams = massGrams,
        stackSignature = stackSignature
    };
}

[Serializable]
public sealed class ProductionCapacityRoutingActorQuiesceReceiptSaveData
{
    public string actorPersistentId = string.Empty;
    public string batchCommitId = string.Empty;
    public int physicalCellX;
    public int physicalCellY;
    public List<string> carriedRowKeys = new();
    public List<string> quantityLeaseIds = new();
    public List<string> warehouseAdmissionTokenIds = new();
    public string activePlanFingerprint = string.Empty;
    public string prePhysicalFingerprint = string.Empty;
    public string postPhysicalFingerprint = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionCapacityRoutingActorQuiesceReceiptSaveData Clone() => new()
    {
        actorPersistentId = actorPersistentId,
        batchCommitId = batchCommitId,
        physicalCellX = physicalCellX,
        physicalCellY = physicalCellY,
        carriedRowKeys = carriedRowKeys?.ToList() ?? new List<string>(),
        quantityLeaseIds = quantityLeaseIds?.ToList() ?? new List<string>(),
        warehouseAdmissionTokenIds = warehouseAdmissionTokenIds?.ToList()
            ?? new List<string>(),
        activePlanFingerprint = activePlanFingerprint,
        prePhysicalFingerprint = prePhysicalFingerprint,
        postPhysicalFingerprint = postPhysicalFingerprint,
        receiptFingerprint = receiptFingerprint
    };
}

[Serializable]
public sealed class ProductionCapacityRoutingOperationAuthorityRowSaveData
{
    public string operationId = string.Empty;
    public List<string> quantityLeaseIds = new();
    public List<string> warehouseAdmissionTokenIds = new();
    public string haulIntentFingerprint = string.Empty;

    public ProductionCapacityRoutingOperationAuthorityRowSaveData Clone() => new()
    {
        operationId = operationId,
        quantityLeaseIds = quantityLeaseIds?.ToList() ?? new List<string>(),
        warehouseAdmissionTokenIds = warehouseAdmissionTokenIds?.ToList()
            ?? new List<string>(),
        haulIntentFingerprint = haulIntentFingerprint
    };
}

[Serializable]
public sealed class ProductionCapacityRoutingActorAuthorityReleaseSaveData
{
    public string actorPersistentId = string.Empty;
    public string actorQuiesceReceiptFingerprint = string.Empty;
    public List<string> operationIds = new();
    public List<ProductionCapacityRoutingOperationAuthorityRowSaveData>
        operations = new();
    public string activePlanFingerprint = string.Empty;
    public string planFingerprint = string.Empty;
    public bool effectsCommitted;
    public bool actorPlanFinalized;
    public string effectFingerprint = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionCapacityRoutingActorAuthorityReleaseSaveData Clone() => new()
    {
        actorPersistentId = actorPersistentId,
        actorQuiesceReceiptFingerprint = actorQuiesceReceiptFingerprint,
        operationIds = operationIds?.ToList() ?? new List<string>(),
        operations = (operations
                ?? new List<ProductionCapacityRoutingOperationAuthorityRowSaveData>())
            .Select(value => value?.Clone())
            .ToList(),
        activePlanFingerprint = activePlanFingerprint,
        planFingerprint = planFingerprint,
        effectsCommitted = effectsCommitted,
        actorPlanFinalized = actorPlanFinalized,
        effectFingerprint = effectFingerprint,
        receiptFingerprint = receiptFingerprint
    };
}

public sealed class ProductionCapacityRoutingDrainRequest
{
    public ProductionCapacityRoutingDrainRequest(
        string stepOperationId,
        string ownerStableId,
        string facilityId,
        string sourceDestinationId,
        string batchCommitId,
        string sourceOutcomeFingerprint,
        string sourceRoutingFingerprint,
        string sourceOwnershipFingerprint,
        IEnumerable<ProductionCapacityRoutingDrainLineSaveData> sourceLines,
        IEnumerable<ProductionCapacityRoutingDrainRouteSaveData> sourceRoutes,
        IEnumerable<ProductionCapacityRoutingDrainSliceSaveData> sourceSlices,
        IEnumerable<ProductionCapacityRoutingDrainActorCarrySaveData> sourceActorCarries,
        IEnumerable<string> sourceCustodyStackIds,
        int inputQuantity,
        long inputMassGrams,
        string requestFingerprint)
    {
        StepOperationId = stepOperationId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        FacilityId = facilityId ?? string.Empty;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        BatchCommitId = batchCommitId ?? string.Empty;
        SourceOutcomeFingerprint = sourceOutcomeFingerprint ?? string.Empty;
        SourceRoutingFingerprint = sourceRoutingFingerprint ?? string.Empty;
        SourceOwnershipFingerprint = sourceOwnershipFingerprint ?? string.Empty;
        SourceLines = Array.AsReadOnly((sourceLines
                ?? Array.Empty<ProductionCapacityRoutingDrainLineSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.lineCommitId, StringComparer.Ordinal)
            .ToArray());
        SourceRoutes = Array.AsReadOnly((sourceRoutes
                ?? Array.Empty<ProductionCapacityRoutingDrainRouteSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.routeOperationId, StringComparer.Ordinal)
            .ToArray());
        SourceSlices = Array.AsReadOnly((sourceSlices
                ?? Array.Empty<ProductionCapacityRoutingDrainSliceSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => ProductionCapacityRoutingDrainFingerprint
                .SliceKey(value), StringComparer.Ordinal)
            .ToArray());
        SourceActorCarries = Array.AsReadOnly((sourceActorCarries
                ?? Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => ProductionCapacityRoutingDrainFingerprint
                .ActorCarryKey(value), StringComparer.Ordinal)
            .ToArray());
        SourceCustodyStackIds = Array.AsReadOnly((sourceCustodyStackIds
                ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
        RequestFingerprint = requestFingerprint ?? string.Empty;
    }

    public string StepOperationId { get; }
    public string OwnerStableId { get; }
    public string FacilityId { get; }
    public string SourceDestinationId { get; }
    public string BatchCommitId { get; }
    public string SourceOutcomeFingerprint { get; }
    public string SourceRoutingFingerprint { get; }
    public string SourceOwnershipFingerprint { get; }
    public IReadOnlyList<ProductionCapacityRoutingDrainLineSaveData> SourceLines { get; }
    public IReadOnlyList<ProductionCapacityRoutingDrainRouteSaveData> SourceRoutes { get; }
    public IReadOnlyList<ProductionCapacityRoutingDrainSliceSaveData> SourceSlices { get; }
    public IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData> SourceActorCarries { get; }
    public IReadOnlyList<string> SourceCustodyStackIds { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
    public string RequestFingerprint { get; }
}

public static class ProductionCapacityRoutingDrainFingerprint
{
    private const string CommitPrefix =
        "production-capacity-routing-drain-commit:";

    public static string CreateRequest(
        string stepOperationId,
        string ownerStableId,
        string facilityId,
        string sourceDestinationId,
        string batchCommitId,
        string sourceOutcomeFingerprint,
        string sourceRoutingFingerprint,
        string sourceOwnershipFingerprint,
        IEnumerable<ProductionCapacityRoutingDrainLineSaveData> sourceLines,
        IEnumerable<ProductionCapacityRoutingDrainRouteSaveData> sourceRoutes,
        IEnumerable<ProductionCapacityRoutingDrainSliceSaveData> sourceSlices,
        IEnumerable<ProductionCapacityRoutingDrainActorCarrySaveData> sourceActorCarries,
        IEnumerable<string> sourceCustodyStackIds,
        int inputQuantity,
        long inputMassGrams)
    {
        StringBuilder canonical = new StringBuilder(1024)
            .Append("production-capacity-routing-drain-request@1|");
        AppendToken(canonical, stepOperationId);
        AppendToken(canonical, ownerStableId);
        AppendToken(canonical, facilityId);
        AppendToken(canonical, sourceDestinationId);
        AppendToken(canonical, batchCommitId);
        AppendToken(canonical, sourceOutcomeFingerprint);
        AppendToken(canonical, sourceRoutingFingerprint);
        AppendToken(canonical, sourceOwnershipFingerprint);
        canonical.Append(inputQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(inputMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|');
        AppendLines(canonical, sourceLines);
        AppendRoutes(canonical, sourceRoutes);
        AppendSlices(canonical, sourceSlices);
        AppendActorCarries(canonical, sourceActorCarries);
        AppendTokens(canonical, sourceCustodyStackIds);
        return Hash(canonical.ToString());
    }

    public static string CreateCommitId(
        string stepOperationId,
        string requestFingerprint) => CommitPrefix
        + Hash((stepOperationId ?? string.Empty) + "|"
            + (requestFingerprint ?? string.Empty)).Substring(0, 24);

    public static string CreateResultFingerprint(
        ProductionCapacityRoutingDrainSaveData value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        StringBuilder canonical = new StringBuilder(1024)
            .Append("production-capacity-routing-drain-result@1|")
            .Append(value.stepOperationId).Append('|')
            .Append(value.requestFingerprint).Append('|')
            .Append(value.batchCommitId).Append('|')
            .Append(value.inputQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(value.inputMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|');
        AppendTokens(canonical, value.completedLineCommitIds);
        AppendTokens(canonical, value.finalRouteOperationIds);
        AppendTokens(canonical, value.preservedStackIds);
        AppendActorQuiesceReceipts(canonical, value.actorQuiesceReceipts);
        AppendActorAuthorityReleases(canonical, value.actorAuthorityReleases);
        AppendTokens(canonical, value.releasedHaulIntentOperationIds);
        AppendTokens(canonical, value.stablePhysicalStackIds);
        return Hash(canonical.ToString());
    }

    public static string CreateReceipt(
        ProductionCapacityRoutingDrainSaveData value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        StringBuilder canonical = new StringBuilder(1024)
            .Append("production-capacity-routing-drain-receipt@1|")
            .Append(value.requestFingerprint).Append('|')
            .Append(value.observedRemovedBatchCommitId).Append('|')
            .Append(value.preservedQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(value.preservedMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|').Append(value.resultFingerprint).Append('|')
            .Append(value.commitId).Append('|');
        AppendTokens(canonical, value.completedLineCommitIds);
        AppendTokens(canonical, value.finalRouteOperationIds);
        AppendTokens(canonical, value.preservedStackIds);
        AppendActorQuiesceReceipts(canonical, value.actorQuiesceReceipts);
        AppendActorAuthorityReleases(canonical, value.actorAuthorityReleases);
        AppendTokens(canonical, value.releasedHaulIntentOperationIds);
        AppendTokens(canonical, value.stablePhysicalStackIds);
        return Hash(canonical.ToString());
    }

    public static string SliceKey(
        ProductionCapacityRoutingDrainSliceSaveData value) => value == null
        ? string.Empty
        : (value.routeOperationId ?? string.Empty) + "|"
            + value.sourceOffsetQuantity.ToString("D10", CultureInfo.InvariantCulture)
            + "|" + (value.sourceStackId ?? string.Empty) + "|"
            + value.routedOffsetQuantity.ToString("D10", CultureInfo.InvariantCulture)
            + "|" + (value.routedStackId ?? string.Empty);

    public static string ActorCarryKey(
        ProductionCapacityRoutingDrainActorCarrySaveData value) => value == null
        ? string.Empty
        : (value.actorPersistentId ?? string.Empty) + "|"
            + (value.haulIntentOperationId ?? string.Empty) + "|"
            + (value.routeOperationId ?? string.Empty) + "|"
            + (value.carriedStackId ?? string.Empty) + "|"
            + (value.sourceStackId ?? string.Empty);

    public static string CreateActorCarryStackSignature(
        string itemId,
        string itemInstanceId,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        string canonical = ItemStackSignature.Create(itemId, components)
            + "|instance=" + (itemInstanceId ?? string.Empty);
        return Hash(canonical);
    }

    public static string ActorQuiesceReceiptKey(
        ProductionCapacityRoutingActorQuiesceReceiptSaveData value) =>
        value?.actorPersistentId ?? string.Empty;

    public static string CreateActorQuiesceReceiptFingerprint(
        string stepOperationId,
        string requestFingerprint,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        StringBuilder canonical = new StringBuilder(512)
            .Append("production-capacity-routing-actor-quiesce@1|");
        AppendToken(canonical, stepOperationId);
        AppendToken(canonical, requestFingerprint);
        AppendToken(canonical, value.actorPersistentId);
        AppendToken(canonical, value.batchCommitId);
        canonical.Append(value.physicalCellX.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value.physicalCellY.ToString(CultureInfo.InvariantCulture))
            .Append('|');
        AppendTokens(canonical, value.carriedRowKeys);
        AppendTokens(canonical, value.quantityLeaseIds);
        AppendTokens(canonical, value.warehouseAdmissionTokenIds);
        AppendToken(canonical, value.activePlanFingerprint);
        AppendToken(canonical, value.prePhysicalFingerprint);
        AppendToken(canonical, value.postPhysicalFingerprint);
        return Hash(canonical.ToString());
    }

    public static string CreateActorAuthorityReleasePlanFingerprint(
        string stepOperationId,
        string requestFingerprint,
        ProductionCapacityRoutingActorAuthorityReleaseSaveData value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        StringBuilder canonical = new StringBuilder(512)
            .Append("production-capacity-routing-actor-authority-plan@1|");
        AppendToken(canonical, stepOperationId);
        AppendToken(canonical, requestFingerprint);
        AppendToken(canonical, value.actorPersistentId);
        AppendToken(canonical, value.actorQuiesceReceiptFingerprint);
        AppendTokens(canonical, value.operationIds);
        foreach (ProductionCapacityRoutingOperationAuthorityRowSaveData row in
                 (value.operations
                     ?? new List<ProductionCapacityRoutingOperationAuthorityRowSaveData>())
                 .OrderBy(row => row?.operationId, StringComparer.Ordinal))
        {
            AppendToken(canonical, row?.operationId);
            AppendTokens(canonical, row?.quantityLeaseIds);
            AppendTokens(canonical, row?.warehouseAdmissionTokenIds);
            AppendToken(canonical, row?.haulIntentFingerprint);
        }
        canonical.Append('|');
        AppendToken(canonical, value.activePlanFingerprint);
        return Hash(canonical.ToString());
    }

    public static string CreateActorAuthorityReleaseEffectFingerprint(
        string planFingerprint,
        bool actorPlanFinalized)
    {
        StringBuilder canonical = new StringBuilder(128)
            .Append("production-capacity-routing-actor-authority-effect@1|");
        AppendToken(canonical, planFingerprint);
        canonical.Append(actorPlanFinalized ? '1' : '0').Append('|');
        return Hash(canonical.ToString());
    }

    public static string CreateActorAuthorityReleaseReceiptFingerprint(
        string planFingerprint,
        string effectFingerprint)
    {
        StringBuilder canonical = new StringBuilder(160)
            .Append("production-capacity-routing-actor-authority-receipt@1|");
        AppendToken(canonical, planFingerprint);
        AppendToken(canonical, effectFingerprint);
        return Hash(canonical.ToString());
    }

    internal static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        StringBuilder result = new StringBuilder(digest.Length * 2);
        foreach (byte current in digest)
            result.Append(current.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    internal static void AppendTokens(
        StringBuilder target,
        IEnumerable<string> values)
    {
        foreach (string value in (values ?? Array.Empty<string>())
                     .OrderBy(value => value, StringComparer.Ordinal))
            AppendToken(target, value);
        target.Append('|');
    }

    private static void AppendLines(
        StringBuilder target,
        IEnumerable<ProductionCapacityRoutingDrainLineSaveData> values)
    {
        foreach (ProductionCapacityRoutingDrainLineSaveData value in
                 (values ?? Array.Empty<ProductionCapacityRoutingDrainLineSaveData>())
                 .OrderBy(value => value?.lineCommitId, StringComparer.Ordinal))
        {
            AppendToken(target, value?.lineCommitId);
            AppendToken(target, value?.outputLineId);
            AppendToken(target, value?.itemId);
            AppendToken(target, value?.componentFingerprint);
            target.Append(value?.originalQuantity ?? -1).Append(':')
                .Append(value?.originalMassGrams ?? -1L).Append(':')
                .Append(value?.remainingQuantity ?? -1).Append(':')
                .Append(value?.remainingMassGrams ?? -1L).Append(':')
                .Append(value?.routedQuantity ?? -1).Append(':')
                .Append(value?.routedMassGrams ?? -1L).Append(';');
        }
        target.Append('|');
    }

    private static void AppendRoutes(
        StringBuilder target,
        IEnumerable<ProductionCapacityRoutingDrainRouteSaveData> values)
    {
        foreach (ProductionCapacityRoutingDrainRouteSaveData value in
                 (values ?? Array.Empty<ProductionCapacityRoutingDrainRouteSaveData>())
                 .OrderBy(value => value?.routeOperationId, StringComparer.Ordinal))
        {
            AppendToken(target, value?.routeOperationId);
            AppendToken(target, value?.requestFingerprint);
            AppendToken(target, value?.physicalReceiptFingerprint);
            target.Append(value?.phase ?? -1).Append(':')
                .Append(value?.currentDeliveryRevision ?? -1L).Append(';');
            AppendToken(target, value?.currentDeliveryRevisionFingerprint);
            AppendToken(target, value?.currentTargetDestinationId);
            AppendToken(target, value?.currentTargetAuthorityFingerprint);
        }
        target.Append('|');
    }

    private static void AppendSlices(
        StringBuilder target,
        IEnumerable<ProductionCapacityRoutingDrainSliceSaveData> values)
    {
        foreach (ProductionCapacityRoutingDrainSliceSaveData value in
                 (values ?? Array.Empty<ProductionCapacityRoutingDrainSliceSaveData>())
                 .OrderBy(SliceKey, StringComparer.Ordinal))
        {
            AppendToken(target, value?.routeOperationId);
            AppendToken(target, value?.sourceStackId);
            AppendToken(target, value?.routedStackId);
            AppendToken(target, value?.outputLineId);
            AppendToken(target, value?.lineCommitId);
            AppendToken(target, value?.itemId);
            target.Append(value?.sourceOffsetQuantity ?? -1).Append(':')
                .Append(value?.routedOffsetQuantity ?? -1).Append(':')
                .Append(value?.routedQuantity ?? -1).Append(':')
                .Append(value?.routedMassGrams ?? -1L).Append(';');
            AppendToken(target, value?.componentFingerprint);
        }
        target.Append('|');
    }

    private static void AppendActorCarries(
        StringBuilder target,
        IEnumerable<ProductionCapacityRoutingDrainActorCarrySaveData> values)
    {
        foreach (ProductionCapacityRoutingDrainActorCarrySaveData value in
                 (values ?? Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>())
                 .OrderBy(ActorCarryKey, StringComparer.Ordinal))
        {
            AppendToken(target, value?.actorPersistentId);
            AppendToken(target, value?.haulIntentOperationId);
            AppendToken(target, value?.routeOperationId);
            AppendToken(target, value?.carriedStackId);
            AppendToken(target, value?.sourceStackId);
            target.Append(value?.quantity ?? -1).Append(':')
                .Append(value?.massGrams ?? -1L).Append(';');
            AppendToken(target, value?.stackSignature);
        }
        target.Append('|');
    }

    private static void AppendActorQuiesceReceipts(
        StringBuilder target,
        IEnumerable<ProductionCapacityRoutingActorQuiesceReceiptSaveData> values)
    {
        foreach (ProductionCapacityRoutingActorQuiesceReceiptSaveData value in
                 (values
                     ?? Array.Empty<ProductionCapacityRoutingActorQuiesceReceiptSaveData>())
                 .OrderBy(ActorQuiesceReceiptKey, StringComparer.Ordinal))
        {
            AppendToken(target, value?.actorPersistentId);
            AppendToken(target, value?.batchCommitId);
            target.Append(value?.physicalCellX ?? 0).Append(':')
                .Append(value?.physicalCellY ?? 0).Append(';');
            AppendTokens(target, value?.carriedRowKeys);
            AppendTokens(target, value?.quantityLeaseIds);
            AppendTokens(target, value?.warehouseAdmissionTokenIds);
            AppendToken(target, value?.activePlanFingerprint);
            AppendToken(target, value?.prePhysicalFingerprint);
            AppendToken(target, value?.postPhysicalFingerprint);
            AppendToken(target, value?.receiptFingerprint);
        }
        target.Append('|');
    }

    private static void AppendActorAuthorityReleases(
        StringBuilder target,
        IEnumerable<ProductionCapacityRoutingActorAuthorityReleaseSaveData> values)
    {
        foreach (ProductionCapacityRoutingActorAuthorityReleaseSaveData value in
                 (values
                     ?? Array.Empty<ProductionCapacityRoutingActorAuthorityReleaseSaveData>())
                 .OrderBy(value => value?.actorPersistentId, StringComparer.Ordinal))
        {
            AppendToken(target, value?.actorPersistentId);
            AppendToken(target, value?.actorQuiesceReceiptFingerprint);
            AppendTokens(target, value?.operationIds);
            foreach (ProductionCapacityRoutingOperationAuthorityRowSaveData row in
                     (value?.operations
                         ?? new List<ProductionCapacityRoutingOperationAuthorityRowSaveData>())
                     .OrderBy(row => row?.operationId, StringComparer.Ordinal))
            {
                AppendToken(target, row?.operationId);
                AppendTokens(target, row?.quantityLeaseIds);
                AppendTokens(target, row?.warehouseAdmissionTokenIds);
                AppendToken(target, row?.haulIntentFingerprint);
            }
            target.Append('|');
            AppendToken(target, value?.activePlanFingerprint);
            AppendToken(target, value?.planFingerprint);
            target.Append(value?.effectsCommitted == true ? '1' : '0')
                .Append(':')
                .Append(value?.actorPlanFinalized == true ? '1' : '0')
                .Append(';');
            AppendToken(target, value?.effectFingerprint);
            AppendToken(target, value?.receiptFingerprint);
        }
        target.Append('|');
    }

    private static void AppendToken(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append(';');
    }
}

public readonly struct ProductionCapacityRoutingDrainResult
{
    public ProductionCapacityRoutingDrainResult(
        ProductionCapacityRoutingDrainStatus status,
        string commitId,
        string receiptFingerprint,
        string failureReason)
    {
        Status = status;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionCapacityRoutingDrainStatus Status { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public string FailureReason { get; }
}

public interface IProductionCapacityRoutingDrainQuery
{
    bool IsBatchPending(string batchCommitId);

    bool TryCaptureByBatch(
        string batchCommitId,
        out ProductionCapacityRoutingDrainSaveData record);
}

public interface IProductionCapacityRoutingDrainOutbox
{
    [GameplayInternalOnly(
        "Persists one immutable routing-batch destructive-drain source vector after the journal owner exists.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryPrepare(
        ProductionCapacityRoutingDrainRequest request);

    [GameplayInternalOnly(
        "Advances one replay-safe capacity-routing drain phase or canonical progress item.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryBeginRouting(
        string stepOperationId,
        string requestFingerprint);
    [GameplayInternalOnly(
        "Records one canonical routing-line completion after the normal route/ack path commits it.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryRecordLineRouted(
        string stepOperationId,
        string lineCommitId);
    [GameplayInternalOnly(
        "Freezes the final normal-route and preserved physical stack vectors before actor quiesce.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryBeginQuiescingActors(
        string stepOperationId,
        IEnumerable<string> finalRouteOperationIds,
        IEnumerable<string> preservedStackIds);
    [GameplayInternalOnly(
        "Records one atomically completed actor current-cell quiesce.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryConfirmActorQuiesced(
        string stepOperationId,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt);
    [GameplayInternalOnly(
        "Begins lease, admission, intent, and active-plan authority release only after all actor relocation receipts exist.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryBeginReleasingOperationAuthority(
        string stepOperationId);
    [GameplayInternalOnly(
        "Persists the canonical next actor's exact lease, admission, intent and frozen-plan release vector before any authority mutation.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryPrepareActorAuthorityRelease(
        string stepOperationId,
        string requestFingerprint,
        ProductionCapacityRoutingActorAuthorityReleaseSaveData plan);
    [GameplayInternalOnly(
        "Commits one actor-wide exact authority release only after every planned effect and the frozen actor plan are terminal.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryCommitActorAuthorityRelease(
        string stepOperationId,
        string planFingerprint,
        string effectFingerprint,
        bool actorPlanFinalized);
    [GameplayInternalOnly(
        "Begins stable physical-state verification after all planned operation authorities are released.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryBeginAwaitingStablePhysicalState(
        string stepOperationId);
    [GameplayInternalOnly(
        "Records one preserved physical stack after route, recovery, reservation, and carry stability checks pass.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryRecordStablePhysicalStack(
        string stepOperationId,
        string stackId);
    [GameplayInternalOnly(
        "Enters the save-coupled wait for normal Economy/Items whole-batch checkpoint GC.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryBeginAwaitingDurableCheckpointGc(
        string stepOperationId);
    [GameplayInternalOnly(
        "Commits producer evidence only after both Economy and Items routing authority are absent and physical mass is preserved.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryCommitEffect(
        string stepOperationId,
        string observedRemovedBatchCommitId,
        int preservedQuantity,
        long preservedMassGrams,
        string resultFingerprint);
    [GameplayInternalOnly(
        "Acknowledges the producer receipt only after the upper destructive-drain journal records the same receipt.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);
    [GameplayInternalOnly(
        "Deletes terminal producer evidence only from the ordered durable-save checkpoint callback.",
        "Production capacity-routing destructive-drain checkpoint GC only")]
    ProductionCapacityRoutingDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);
    bool TryCapture(
        string stepOperationId,
        out ProductionCapacityRoutingDrainSaveData record);
}

[Serializable]
public sealed class ProductionCapacityRoutingDrainSaveData
{
    public string stepOperationId = string.Empty;
    public string ownerStableId = string.Empty;
    public string facilityId = string.Empty;
    public string sourceDestinationId = string.Empty;
    public string batchCommitId = string.Empty;
    public string sourceOutcomeFingerprint = string.Empty;
    public string sourceRoutingFingerprint = string.Empty;
    public string sourceOwnershipFingerprint = string.Empty;
    public string requestFingerprint = string.Empty;
    public ProductionCapacityRoutingDrainPhase phase;
    public List<ProductionCapacityRoutingDrainLineSaveData> sourceLines = new();
    public List<ProductionCapacityRoutingDrainRouteSaveData> sourceRoutes = new();
    public List<ProductionCapacityRoutingDrainSliceSaveData> sourceSlices = new();
    public List<ProductionCapacityRoutingDrainActorCarrySaveData> sourceActorCarries = new();
    public List<string> sourceCustodyStackIds = new();
    public List<string> completedLineCommitIds = new();
    public List<string> finalRouteOperationIds = new();
    public List<string> preservedStackIds = new();
    public List<ProductionCapacityRoutingActorQuiesceReceiptSaveData>
        actorQuiesceReceipts = new();
    public List<ProductionCapacityRoutingActorAuthorityReleaseSaveData>
        actorAuthorityReleases = new();
    public List<string> releasedHaulIntentOperationIds = new();
    public List<string> stablePhysicalStackIds = new();
    public int inputQuantity;
    public long inputMassGrams;
    public int preservedQuantity;
    public long preservedMassGrams;
    public string observedRemovedBatchCommitId = string.Empty;
    public string resultFingerprint = string.Empty;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionCapacityRoutingDrainSaveData Clone() => new()
    {
        stepOperationId = stepOperationId,
        ownerStableId = ownerStableId,
        facilityId = facilityId,
        sourceDestinationId = sourceDestinationId,
        batchCommitId = batchCommitId,
        sourceOutcomeFingerprint = sourceOutcomeFingerprint,
        sourceRoutingFingerprint = sourceRoutingFingerprint,
        sourceOwnershipFingerprint = sourceOwnershipFingerprint,
        requestFingerprint = requestFingerprint,
        phase = phase,
        sourceLines = Clone(sourceLines),
        sourceRoutes = Clone(sourceRoutes),
        sourceSlices = Clone(sourceSlices),
        sourceActorCarries = Clone(sourceActorCarries),
        sourceCustodyStackIds = Clone(sourceCustodyStackIds),
        completedLineCommitIds = Clone(completedLineCommitIds),
        finalRouteOperationIds = Clone(finalRouteOperationIds),
        preservedStackIds = Clone(preservedStackIds),
        actorQuiesceReceipts = Clone(actorQuiesceReceipts),
        actorAuthorityReleases = Clone(actorAuthorityReleases),
        releasedHaulIntentOperationIds = Clone(releasedHaulIntentOperationIds),
        stablePhysicalStackIds = Clone(stablePhysicalStackIds),
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        preservedQuantity = preservedQuantity,
        preservedMassGrams = preservedMassGrams,
        observedRemovedBatchCommitId = observedRemovedBatchCommitId,
        resultFingerprint = resultFingerprint,
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };

    private static List<T> Clone<T>(IEnumerable<T> source)
        where T : class => (source ?? Array.Empty<T>())
        .Select(value => value switch
        {
            ProductionCapacityRoutingDrainLineSaveData line => line.Clone() as T,
            ProductionCapacityRoutingDrainRouteSaveData route => route.Clone() as T,
            ProductionCapacityRoutingDrainSliceSaveData slice => slice.Clone() as T,
            ProductionCapacityRoutingDrainActorCarrySaveData carry => carry.Clone() as T,
            ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt =>
                receipt.Clone() as T,
            ProductionCapacityRoutingActorAuthorityReleaseSaveData release =>
                release.Clone() as T,
            _ => throw new InvalidOperationException(
                "Unsupported capacity-routing drain clone type.")
        })
        .ToList();

    private static List<string> Clone(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>()).ToList();
}
