using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal enum FacilityOutputExactRouteCustodyPhase
{
    OriginBuffered = 0,
    PhysicalPending = 1,
    Routable = 2
}

internal readonly struct FacilityOutputExactRouteCustodyMetadata
{
    internal FacilityOutputExactRouteCustodyMetadata(
        FacilityOutputExactRouteCustodyPhase phase,
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string outputLineId,
        string lineCommitId,
        int originalStackOrdinal,
        int originalBatchStackCount,
        int originalBatchQuantity,
        long originalBatchMassGrams,
        int originalLineStackCount,
        int originalLineQuantity,
        long originalLineMassGrams,
        string itemId,
        string componentSignature,
        string componentFingerprint,
        string originDestinationId,
        string targetDestinationId,
        string originStackId,
        string currentSourceStackId,
        Vector2Int originPosition,
        int sourceOffsetQuantity,
        int quantity,
        long massGrams,
        string routeOperationId,
        string requestFingerprint,
        string physicalReceiptFingerprint,
        long currentDeliveryRevision = -1L,
        string currentDeliveryRevisionFingerprint = "",
        string currentDeliveryRerouteOperationId = "",
        string currentTargetDestinationId = "",
        int currentTargetPositionX = 0,
        int currentTargetPositionY = 0,
        string currentTargetAuthorityFingerprint = "")
    {
        Phase = phase;
        BatchCommitId = batchCommitId;
        OutcomeFingerprint = outcomeFingerprint;
        PlannedOutputFingerprint = plannedOutputFingerprint;
        OutputLineId = outputLineId;
        LineCommitId = lineCommitId;
        OriginalStackOrdinal = originalStackOrdinal;
        OriginalBatchStackCount = originalBatchStackCount;
        OriginalBatchQuantity = originalBatchQuantity;
        OriginalBatchMassGrams = originalBatchMassGrams;
        OriginalLineStackCount = originalLineStackCount;
        OriginalLineQuantity = originalLineQuantity;
        OriginalLineMassGrams = originalLineMassGrams;
        ItemId = itemId;
        ComponentSignature = componentSignature;
        ComponentFingerprint = componentFingerprint;
        OriginDestinationId = originDestinationId;
        TargetDestinationId = targetDestinationId;
        OriginStackId = originStackId;
        CurrentSourceStackId = currentSourceStackId;
        OriginPosition = originPosition;
        SourceOffsetQuantity = sourceOffsetQuantity;
        Quantity = quantity;
        MassGrams = massGrams;
        RouteOperationId = routeOperationId;
        RequestFingerprint = requestFingerprint;
        PhysicalReceiptFingerprint = physicalReceiptFingerprint;
        CurrentDeliveryRevision = currentDeliveryRevision;
        CurrentDeliveryRevisionFingerprint =
            currentDeliveryRevisionFingerprint ?? string.Empty;
        CurrentDeliveryRerouteOperationId =
            currentDeliveryRerouteOperationId ?? string.Empty;
        CurrentTargetDestinationId = currentTargetDestinationId ?? string.Empty;
        CurrentTargetPosition = new Vector2Int(
            currentTargetPositionX,
            currentTargetPositionY);
        CurrentTargetAuthorityFingerprint =
            currentTargetAuthorityFingerprint ?? string.Empty;
    }

    internal FacilityOutputExactRouteCustodyPhase Phase { get; }
    internal string BatchCommitId { get; }
    internal string OutcomeFingerprint { get; }
    internal string PlannedOutputFingerprint { get; }
    internal string OutputLineId { get; }
    internal string LineCommitId { get; }
    internal int OriginalStackOrdinal { get; }
    internal int OriginalBatchStackCount { get; }
    internal int OriginalBatchQuantity { get; }
    internal long OriginalBatchMassGrams { get; }
    internal int OriginalLineStackCount { get; }
    internal int OriginalLineQuantity { get; }
    internal long OriginalLineMassGrams { get; }
    internal string ItemId { get; }
    internal string ComponentSignature { get; }
    internal string ComponentFingerprint { get; }
    internal string OriginDestinationId { get; }
    internal string TargetDestinationId { get; }
    internal string OriginStackId { get; }
    internal string CurrentSourceStackId { get; }
    internal Vector2Int OriginPosition { get; }
    internal int SourceOffsetQuantity { get; }
    internal int Quantity { get; }
    internal long MassGrams { get; }
    internal string RouteOperationId { get; }
    internal string RequestFingerprint { get; }
    internal string PhysicalReceiptFingerprint { get; }
    internal long CurrentDeliveryRevision { get; }
    internal string CurrentDeliveryRevisionFingerprint { get; }
    internal string CurrentDeliveryRerouteOperationId { get; }
    internal string CurrentTargetDestinationId { get; }
    internal Vector2Int CurrentTargetPosition { get; }
    internal string CurrentTargetAuthorityFingerprint { get; }

    internal FacilityOutputExactRouteCustodyMetadata WithSlice(
        FacilityOutputExactRouteCustodyPhase phase,
        string targetDestinationId,
        string currentSourceStackId,
        int sourceOffsetQuantity,
        int quantity,
        long massGrams,
        string routeOperationId,
        string requestFingerprint,
        string physicalReceiptFingerprint,
        FacilityOutputExactRouteDeliveryRevisionSnapshot deliveryRevision = null)
    {
        long revision = -1L;
        string revisionFingerprint = string.Empty;
        string rerouteOperationId = string.Empty;
        string currentTargetDestinationId = string.Empty;
        Vector2Int currentTargetPosition = default;
        string targetAuthorityFingerprint = string.Empty;
        if (phase != FacilityOutputExactRouteCustodyPhase.OriginBuffered)
        {
            if (deliveryRevision != null)
            {
                revision = deliveryRevision.Revision;
                revisionFingerprint = deliveryRevision.RevisionFingerprint;
                rerouteOperationId = deliveryRevision.RerouteOperationId;
                currentTargetDestinationId = deliveryRevision.TargetDestinationId;
                currentTargetPosition = new Vector2Int(
                    deliveryRevision.TargetPositionX,
                    deliveryRevision.TargetPositionY);
                targetAuthorityFingerprint =
                    deliveryRevision.TargetAuthorityFingerprint;
            }
            else
            {
                revision = CurrentDeliveryRevision;
                revisionFingerprint = CurrentDeliveryRevisionFingerprint;
                rerouteOperationId = CurrentDeliveryRerouteOperationId;
                currentTargetDestinationId = CurrentTargetDestinationId;
                currentTargetPosition = CurrentTargetPosition;
                targetAuthorityFingerprint = CurrentTargetAuthorityFingerprint;
            }
        }
        return new FacilityOutputExactRouteCustodyMetadata(
        phase,
        BatchCommitId,
        OutcomeFingerprint,
        PlannedOutputFingerprint,
        OutputLineId,
        LineCommitId,
        OriginalStackOrdinal,
        OriginalBatchStackCount,
        OriginalBatchQuantity,
        OriginalBatchMassGrams,
        OriginalLineStackCount,
        OriginalLineQuantity,
        OriginalLineMassGrams,
        ItemId,
        ComponentSignature,
        ComponentFingerprint,
        OriginDestinationId,
        targetDestinationId,
        OriginStackId,
        currentSourceStackId,
        OriginPosition,
        sourceOffsetQuantity,
        quantity,
        massGrams,
        routeOperationId,
        requestFingerprint,
        physicalReceiptFingerprint,
        revision,
        revisionFingerprint,
        rerouteOperationId,
        currentTargetDestinationId,
        currentTargetPosition.x,
        currentTargetPosition.y,
        targetAuthorityFingerprint);
    }

    internal bool TryPartitionRoutablePrefix(
        string currentPhysicalStackId,
        int prefixQuantity,
        long prefixMassGrams,
        long remainderMassGrams,
        out FacilityOutputExactRouteCustodyMetadata prefix,
        out FacilityOutputExactRouteCustodyMetadata remainder)
    {
        prefix = default;
        remainder = default;
        string physicalStackId = currentPhysicalStackId ?? string.Empty;
        if (Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || physicalStackId.Length == 0
            || prefixQuantity <= 0
            || prefixQuantity >= Quantity
            || prefixMassGrams <= 0L
            || remainderMassGrams <= 0L
            || prefixMassGrams >= MassGrams
            || remainderMassGrams != MassGrams - prefixMassGrams)
        {
            return false;
        }

        int remainderOffset = checked(SourceOffsetQuantity + prefixQuantity);
        int remainderQuantity = checked(Quantity - prefixQuantity);
        prefix = WithSlice(
            FacilityOutputExactRouteCustodyPhase.Routable,
            TargetDestinationId,
            physicalStackId,
            SourceOffsetQuantity,
            prefixQuantity,
            prefixMassGrams,
            RouteOperationId,
            RequestFingerprint,
            PhysicalReceiptFingerprint);
        remainder = WithSlice(
            FacilityOutputExactRouteCustodyPhase.Routable,
            TargetDestinationId,
            physicalStackId,
            remainderOffset,
            remainderQuantity,
            remainderMassGrams,
            RouteOperationId,
            RequestFingerprint,
            PhysicalReceiptFingerprint);
        return true;
    }

    internal FacilityOutputExactRouteCustodyMetadata WithDeliveryRevision(
        FacilityOutputExactRouteDeliveryRevisionSnapshot deliveryRevision)
    {
        if (Phase != FacilityOutputExactRouteCustodyPhase.Routable)
            throw new InvalidOperationException(
                "Only Routable custody can change its delivery overlay.");
        return WithSlice(
            Phase,
            TargetDestinationId,
            CurrentSourceStackId,
            SourceOffsetQuantity,
            Quantity,
            MassGrams,
            RouteOperationId,
            RequestFingerprint,
            PhysicalReceiptFingerprint,
            deliveryRevision ?? throw new ArgumentNullException(
                nameof(deliveryRevision)));
    }
}

internal static class FacilityOutputExactRouteCustodyCodec
{
    internal const string ComponentTypeId =
        "item-state:prepared-output-route-slice";
    internal const int SchemaVersion = 3;

    private const string PhaseKey = "phase";
    private const string BatchCommitIdKey = "batch-commit-id";
    private const string OutcomeFingerprintKey = "outcome-fingerprint";
    private const string PlannedOutputFingerprintKey = "planned-output-fingerprint";
    private const string OutputLineIdKey = "output-line-id";
    private const string LineCommitIdKey = "line-commit-id";
    private const string OriginalStackOrdinalKey = "original-stack-ordinal";
    private const string OriginalBatchStackCountKey = "original-batch-stack-count";
    private const string OriginalBatchQuantityKey = "original-batch-quantity";
    private const string OriginalBatchMassGramsKey = "original-batch-mass-grams";
    private const string OriginalLineStackCountKey = "original-line-stack-count";
    private const string OriginalLineQuantityKey = "original-line-quantity";
    private const string OriginalLineMassGramsKey = "original-line-mass-grams";
    private const string ItemIdKey = "item-id";
    private const string ComponentSignatureKey = "component-signature";
    private const string ComponentFingerprintKey = "component-fingerprint";
    private const string OriginDestinationIdKey = "origin-destination-id";
    private const string TargetDestinationIdKey = "target-destination-id";
    private const string OriginStackIdKey = "origin-stack-id";
    private const string CurrentSourceStackIdKey = "current-source-stack-id";
    private const string OriginPositionXKey = "origin-position-x";
    private const string OriginPositionYKey = "origin-position-y";
    private const string SourceOffsetQuantityKey = "source-offset-quantity";
    private const string QuantityKey = "quantity";
    private const string MassGramsKey = "mass-grams";
    private const string RouteOperationIdKey = "route-operation-id";
    private const string RequestFingerprintKey = "request-fingerprint";
    private const string PhysicalReceiptFingerprintKey =
        "physical-receipt-fingerprint";
    private const string CurrentDeliveryRevisionKey =
        "current-delivery-revision";
    private const string CurrentDeliveryRevisionFingerprintKey =
        "current-delivery-revision-fingerprint";
    private const string CurrentDeliveryRerouteOperationIdKey =
        "current-delivery-reroute-operation-id";
    private const string CurrentTargetDestinationIdKey =
        "current-target-destination-id";
    private const string CurrentTargetPositionXKey =
        "current-target-position-x";
    private const string CurrentTargetPositionYKey =
        "current-target-position-y";
    private const string CurrentTargetAuthorityFingerprintKey =
        "current-target-authority-fingerprint";

    internal static bool IsCustody(ItemInstanceComponentSaveData component) =>
        component != null
        && string.Equals(
            component.componentTypeId,
            ComponentTypeId,
            StringComparison.Ordinal);

    internal static bool HasAnyCustody(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>()).Any(IsCustody);

    internal static bool IsRouteBlocked(
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        ItemInstanceComponentSaveData[] publicationMarkers = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(PlannedOutputPublicationComponentCodec.IsAnyMarker)
            .ToArray();
        if (publicationMarkers.Length > 0
            && (!PlannedOutputPublicationComponentCodec.TryRead(
                    publicationMarkers,
                    out PlannedOutputPublicationMetadata publication)
                || !publication.Acknowledged))
        {
            // The publication marker fences the in-flight materialization.
            // Once it is atomically converted to validated provenance, the
            // physical lot is acknowledged and ordinary/exact hauling may
            // consume that immutable evidence without treating it as a lock.
            return true;
        }
        if (!HasAnyCustody(components))
            return false;
        return !TryRead(
                components,
                out FacilityOutputExactRouteCustodyMetadata metadata)
            || metadata.Phase != FacilityOutputExactRouteCustodyPhase.Routable;
    }

    internal static ItemInstanceComponentSaveData Create(
        FacilityOutputExactRouteCustodyMetadata metadata) => new()
    {
        componentTypeId = ComponentTypeId,
        schemaVersion = SchemaVersion,
        affectsStacking = true,
        values = new List<ItemStateValueSaveData>
        {
            Integer(PhaseKey, (int)metadata.Phase),
            String(BatchCommitIdKey, metadata.BatchCommitId),
            String(OutcomeFingerprintKey, metadata.OutcomeFingerprint),
            String(PlannedOutputFingerprintKey, metadata.PlannedOutputFingerprint),
            String(OutputLineIdKey, metadata.OutputLineId),
            String(LineCommitIdKey, metadata.LineCommitId),
            Integer(OriginalStackOrdinalKey, metadata.OriginalStackOrdinal),
            Integer(OriginalBatchStackCountKey, metadata.OriginalBatchStackCount),
            Integer(OriginalBatchQuantityKey, metadata.OriginalBatchQuantity),
            Integer(OriginalBatchMassGramsKey, metadata.OriginalBatchMassGrams),
            Integer(OriginalLineStackCountKey, metadata.OriginalLineStackCount),
            Integer(OriginalLineQuantityKey, metadata.OriginalLineQuantity),
            Integer(OriginalLineMassGramsKey, metadata.OriginalLineMassGrams),
            String(ItemIdKey, metadata.ItemId),
            String(ComponentSignatureKey, metadata.ComponentSignature),
            String(ComponentFingerprintKey, metadata.ComponentFingerprint),
            String(OriginDestinationIdKey, metadata.OriginDestinationId),
            String(TargetDestinationIdKey, metadata.TargetDestinationId),
            String(OriginStackIdKey, metadata.OriginStackId),
            String(CurrentSourceStackIdKey, metadata.CurrentSourceStackId),
            Integer(OriginPositionXKey, metadata.OriginPosition.x),
            Integer(OriginPositionYKey, metadata.OriginPosition.y),
            Integer(SourceOffsetQuantityKey, metadata.SourceOffsetQuantity),
            Integer(QuantityKey, metadata.Quantity),
            Integer(MassGramsKey, metadata.MassGrams),
            String(RouteOperationIdKey, metadata.RouteOperationId),
            String(RequestFingerprintKey, metadata.RequestFingerprint),
            String(PhysicalReceiptFingerprintKey, metadata.PhysicalReceiptFingerprint),
            Integer(CurrentDeliveryRevisionKey, metadata.CurrentDeliveryRevision),
            String(CurrentDeliveryRevisionFingerprintKey,
                metadata.CurrentDeliveryRevisionFingerprint),
            String(CurrentDeliveryRerouteOperationIdKey,
                metadata.CurrentDeliveryRerouteOperationId),
            String(CurrentTargetDestinationIdKey,
                metadata.CurrentTargetDestinationId),
            Integer(CurrentTargetPositionXKey, metadata.CurrentTargetPosition.x),
            Integer(CurrentTargetPositionYKey, metadata.CurrentTargetPosition.y),
            String(CurrentTargetAuthorityFingerprintKey,
                metadata.CurrentTargetAuthorityFingerprint)
        }
    };

    internal static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out FacilityOutputExactRouteCustodyMetadata metadata)
    {
        metadata = default;
        ItemInstanceComponentSaveData[] matches = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(IsCustody)
            .ToArray();
        if (matches.Length != 1
            || matches[0].schemaVersion != SchemaVersion
            || !matches[0].affectsStacking
            || matches[0].values == null
            || matches[0].values.Count != 35)
            return false;
        IReadOnlyList<ItemStateValueSaveData> values = matches[0].values;
        if (!TryInteger(values, PhaseKey, out long rawPhase)
            || rawPhase < 0L
            || rawPhase > 2L
            || !TryRequiredString(values, BatchCommitIdKey, out string batchCommitId)
            || !TryRequiredString(values, OutcomeFingerprintKey, out string outcomeFingerprint)
            || !TryRequiredString(values, PlannedOutputFingerprintKey, out string plannedFingerprint)
            || !TryRequiredString(values, OutputLineIdKey, out string outputLineId)
            || !TryRequiredString(values, LineCommitIdKey, out string lineCommitId)
            || !TryNonNegativeInt(values, OriginalStackOrdinalKey, out int originalOrdinal)
            || !TryPositiveInt(values, OriginalBatchStackCountKey, out int batchStackCount)
            || !TryPositiveInt(values, OriginalBatchQuantityKey, out int batchQuantity)
            || !TryPositiveLong(values, OriginalBatchMassGramsKey, out long batchMass)
            || !TryPositiveInt(values, OriginalLineStackCountKey, out int lineStackCount)
            || !TryPositiveInt(values, OriginalLineQuantityKey, out int lineQuantity)
            || !TryPositiveLong(values, OriginalLineMassGramsKey, out long lineMass)
            || !TryRequiredString(values, ItemIdKey, out string itemId)
            || !TryOptionalString(values, ComponentSignatureKey, out string componentSignature)
            || !TryRequiredString(values, ComponentFingerprintKey, out string componentFingerprint)
            || !TryRequiredString(values, OriginDestinationIdKey, out string originDestinationId)
            || !TryOptionalString(values, TargetDestinationIdKey, out string targetDestinationId)
            || !TryRequiredString(values, OriginStackIdKey, out string originStackId)
            || !TryRequiredString(
                values,
                CurrentSourceStackIdKey,
                out string currentSourceStackId)
            || !TryInt(values, OriginPositionXKey, out int originPositionX)
            || !TryInt(values, OriginPositionYKey, out int originPositionY)
            || !TryNonNegativeInt(values, SourceOffsetQuantityKey, out int sourceOffset)
            || !TryPositiveInt(values, QuantityKey, out int quantity)
            || !TryPositiveLong(values, MassGramsKey, out long massGrams)
            || !TryOptionalString(values, RouteOperationIdKey, out string routeOperationId)
            || !TryOptionalString(values, RequestFingerprintKey, out string requestFingerprint)
            || !TryOptionalString(
                values,
                PhysicalReceiptFingerprintKey,
                out string physicalFingerprint)
            || !TryInteger(values, CurrentDeliveryRevisionKey,
                out long currentDeliveryRevision)
            || !TryOptionalString(values, CurrentDeliveryRevisionFingerprintKey,
                out string currentDeliveryRevisionFingerprint)
            || !TryOptionalString(values, CurrentDeliveryRerouteOperationIdKey,
                out string currentDeliveryRerouteOperationId)
            || !TryOptionalString(values, CurrentTargetDestinationIdKey,
                out string currentTargetDestinationId)
            || !TryInt(values, CurrentTargetPositionXKey,
                out int currentTargetPositionX)
            || !TryInt(values, CurrentTargetPositionYKey,
                out int currentTargetPositionY)
            || !TryOptionalString(values, CurrentTargetAuthorityFingerprintKey,
                out string currentTargetAuthorityFingerprint))
            return false;
        FacilityOutputExactRouteCustodyPhase phase =
            (FacilityOutputExactRouteCustodyPhase)rawPhase;
        int rangeEnd;
        try
        {
            rangeEnd = checked(sourceOffset + quantity);
        }
        catch (OverflowException)
        {
            return false;
        }
        if (phase == FacilityOutputExactRouteCustodyPhase.OriginBuffered
                ? targetDestinationId.Length != 0
                    || routeOperationId.Length != 0
                    || requestFingerprint.Length != 0
                    || physicalFingerprint.Length != 0
                    || currentDeliveryRevision != -1L
                    || currentDeliveryRevisionFingerprint.Length != 0
                    || currentDeliveryRerouteOperationId.Length != 0
                    || currentTargetDestinationId.Length != 0
                    || currentTargetPositionX != 0
                    || currentTargetPositionY != 0
                    || currentTargetAuthorityFingerprint.Length != 0
                : routeOperationId.Length == 0
                    || requestFingerprint.Length == 0
                    || physicalFingerprint.Length == 0
                    || currentDeliveryRevision < 0L
                    || !IsLowerSha256(currentDeliveryRevisionFingerprint)
                    || (currentDeliveryRevision == 0L
                        ? currentDeliveryRerouteOperationId.Length != 0
                            || currentTargetAuthorityFingerprint.Length != 0
                            || !string.Equals(currentTargetDestinationId,
                                targetDestinationId, StringComparison.Ordinal)
                        : currentDeliveryRerouteOperationId.Length == 0
                            || currentTargetDestinationId.Length == 0
                            || !IsLowerSha256(
                                currentTargetAuthorityFingerprint))
            || originalOrdinal >= batchStackCount
            || lineStackCount > batchStackCount
            || lineQuantity > batchQuantity
            || lineMass > batchMass
            || rangeEnd > lineQuantity
            || quantity > lineQuantity
            || massGrams > lineMass)
            return false;
        metadata = new FacilityOutputExactRouteCustodyMetadata(
            phase,
            batchCommitId,
            outcomeFingerprint,
            plannedFingerprint,
            outputLineId,
            lineCommitId,
            originalOrdinal,
            batchStackCount,
            batchQuantity,
            batchMass,
            lineStackCount,
            lineQuantity,
            lineMass,
            itemId,
            componentSignature,
            componentFingerprint,
            originDestinationId,
            targetDestinationId,
            originStackId,
            currentSourceStackId,
            new Vector2Int(originPositionX, originPositionY),
            sourceOffset,
            quantity,
            massGrams,
            routeOperationId,
            requestFingerprint,
            physicalFingerprint,
            currentDeliveryRevision,
            currentDeliveryRevisionFingerprint,
            currentDeliveryRerouteOperationId,
            currentTargetDestinationId,
            currentTargetPositionX,
            currentTargetPositionY,
            currentTargetAuthorityFingerprint);
        return true;
    }

    internal static List<ItemInstanceComponentSaveData> ReplaceAuthority(
        IEnumerable<ItemInstanceComponentSaveData> components,
        FacilityOutputExactRouteCustodyMetadata metadata)
    {
        List<ItemInstanceComponentSaveData> result = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null
                && !PlannedOutputPublicationComponentCodec.IsAnyMarker(component)
                && !IsCustody(component))
            .Select(component => component.Clone())
            .ToList();
        result.Add(Create(metadata));
        return result;
    }

    private static ItemStateValueSaveData String(string key, string value) => new()
    {
        key = key,
        kind = ItemStateValueKind.String,
        stringValue = value ?? string.Empty
    };

    private static ItemStateValueSaveData Integer(string key, long value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Integer,
        integerValue = value
    };

    private static bool TryRequiredString(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result) => TryOptionalString(values, key, out result)
        && result.Length > 0;

    private static bool TryOptionalString(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result)
    {
        ItemStateValueSaveData[] matches = values.Where(value => value != null
                && value.kind == ItemStateValueKind.String
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = matches.Length == 1
            ? matches[0].stringValue ?? string.Empty
            : string.Empty;
        return matches.Length == 1
            && string.Equals(result, result.Trim(), StringComparison.Ordinal);
    }

    private static bool TryNonNegativeInt(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out int result)
    {
        result = 0;
        if (!TryInteger(values, key, out long raw)
            || raw < 0L
            || raw > int.MaxValue)
            return false;
        result = (int)raw;
        return true;
    }

    private static bool TryInt(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out int result)
    {
        result = 0;
        if (!TryInteger(values, key, out long raw)
            || raw < int.MinValue
            || raw > int.MaxValue)
            return false;
        result = (int)raw;
        return true;
    }

    private static bool TryPositiveInt(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out int result) => TryNonNegativeInt(values, key, out result)
        && result > 0;

    private static bool TryPositiveLong(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result) => TryInteger(values, key, out result)
        && result > 0L;

    private static bool TryInteger(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result)
    {
        ItemStateValueSaveData[] matches = values.Where(value => value != null
                && value.kind == ItemStateValueKind.Integer
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = matches.Length == 1 ? matches[0].integerValue : 0L;
        return matches.Length == 1;
    }

    private static bool IsLowerSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        return value.All(character =>
            character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
    }
}

#if UNITY_EDITOR
public readonly struct FacilityOutputExactRouteCustodyDiagnosticSnapshot
{
    internal FacilityOutputExactRouteCustodyDiagnosticSnapshot(
        FacilityOutputExactRouteCustodyMetadata value)
    {
        IsRoutable = value.Phase
            == FacilityOutputExactRouteCustodyPhase.Routable;
        RouteOperationId = value.RouteOperationId;
        ItemId = value.ItemId;
        Quantity = value.Quantity;
        MassGrams = value.MassGrams;
        CurrentTargetDestinationId = value.CurrentTargetDestinationId;
        CurrentTargetPosition = value.CurrentTargetPosition;
    }

    public bool IsRoutable { get; }
    public string RouteOperationId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string CurrentTargetDestinationId { get; }
    public Vector2Int CurrentTargetPosition { get; }
}

public static class FacilityOutputExactRouteCustodyDiagnostics
{
    public static bool TryCapture(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out FacilityOutputExactRouteCustodyDiagnosticSnapshot snapshot)
    {
        if (FacilityOutputExactRouteCustodyCodec.TryRead(
                components,
                out FacilityOutputExactRouteCustodyMetadata value))
        {
            snapshot = new FacilityOutputExactRouteCustodyDiagnosticSnapshot(value);
            return true;
        }

        snapshot = default;
        return false;
    }
}
#endif
