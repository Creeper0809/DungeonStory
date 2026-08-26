using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Hashes only fields that survive the current physical-item save format.
/// Runtime-only reservation counters are deliberately excluded; lease state is
/// proven separately by the actor authority-release receipt.
/// </summary>
public static class ProductionCapacityRoutingActorPhysicalFingerprint
{
    private sealed class DurableRow
    {
        internal string StackId;
        internal string ItemId;
        internal string ItemInstanceId;
        internal int Quantity;
        internal WorldItemStackState State;
        internal int GridX;
        internal int GridY;
        internal string DestinationId;
        internal bool HasDestinationPosition;
        internal int DestinationGridX;
        internal int DestinationGridY;
        internal string AggregationCohortId;
        internal string SourceStorageDestinationId;
        internal bool Forbidden;
        internal WasteOriginKind WasteOrigin;
        internal float Contamination;
        internal WorldItemDropDisposition DropDisposition;
        internal string RecoveryOwnerOperationId;
        internal string RecoverySourceStackId;
        internal string RecoveryCarrierPersistentId;
        internal WorldItemCarryInterruptionKind RecoveryInterruptionKind;
        internal double DroppedAtGameTime;
        internal double RecoveryDeadlineGameTime;
        internal IReadOnlyList<ItemInstanceComponentSaveData> Components;
    }

    internal static string Create(IEnumerable<WorldItemStackRecord> source) =>
        CreateCore((source ?? Array.Empty<WorldItemStackRecord>())
            .Select(record => record == null ? null : new DurableRow
            {
                StackId = record.stackId,
                ItemId = record.itemId,
                ItemInstanceId = record.itemInstanceId,
                Quantity = record.quantity,
                State = record.state,
                GridX = record.position.x,
                GridY = record.position.y,
                DestinationId = record.destinationId,
                HasDestinationPosition = record.hasDestinationPosition,
                DestinationGridX = record.destinationPosition.x,
                DestinationGridY = record.destinationPosition.y,
                AggregationCohortId = record.aggregationCohortId,
                SourceStorageDestinationId = record.sourceStorageDestinationId,
                Forbidden = record.forbidden,
                WasteOrigin = record.wasteOrigin,
                Contamination = record.contamination,
                DropDisposition = record.dropDisposition,
                RecoveryOwnerOperationId = record.recoveryOwnerOperationId,
                RecoverySourceStackId = record.recoverySourceStackId,
                RecoveryCarrierPersistentId =
                    record.recoveryCarrierPersistentId,
                RecoveryInterruptionKind = record.recoveryInterruptionKind,
                DroppedAtGameTime = record.droppedAtGameTime,
                RecoveryDeadlineGameTime = record.recoveryDeadlineGameTime,
                Components = record.components
            }));

    internal static string Create(IEnumerable<WorldItemStackSaveData> source) =>
        CreateCore((source ?? Array.Empty<WorldItemStackSaveData>())
            .Select(record => record == null ? null : new DurableRow
            {
                StackId = record.stackId,
                ItemId = record.itemId,
                ItemInstanceId = record.itemInstanceId,
                Quantity = record.quantity,
                State = record.state,
                GridX = record.gridX,
                GridY = record.gridY,
                DestinationId = record.destinationId,
                HasDestinationPosition = record.hasDestinationPosition,
                DestinationGridX = record.destinationGridX,
                DestinationGridY = record.destinationGridY,
                AggregationCohortId = record.aggregationCohortId,
                SourceStorageDestinationId = record.sourceStorageDestinationId,
                Forbidden = record.forbidden,
                WasteOrigin = record.wasteOrigin,
                Contamination = record.contamination,
                DropDisposition = record.dropDisposition,
                RecoveryOwnerOperationId = record.recoveryOwnerOperationId,
                RecoverySourceStackId = record.recoverySourceStackId,
                RecoveryCarrierPersistentId =
                    record.recoveryCarrierPersistentId,
                RecoveryInterruptionKind = record.recoveryInterruptionKind,
                DroppedAtGameTime = record.droppedAtGameTime,
                RecoveryDeadlineGameTime = record.recoveryDeadlineGameTime,
                Components = record.components
            }));

#if UNITY_EDITOR
    [GameplayInternalOnly(
        "Computes the durable actor physical fingerprint for isolated Editor save fixtures.",
        "Capacity-routing save validation fixtures only")]
    public static string CreateEditorTest(
        IEnumerable<WorldItemStackSaveData> source) => Create(source);
#endif

    private static string CreateCore(IEnumerable<DurableRow> source)
    {
        StringBuilder canonical = new(2048);
        foreach (DurableRow row in (source ?? Array.Empty<DurableRow>())
                     .OrderBy(value => value?.StackId, StringComparer.Ordinal))
        {
            AppendToken(canonical, row?.StackId);
            AppendToken(canonical, row?.ItemId);
            AppendToken(canonical, row?.ItemInstanceId);
            canonical.Append(row?.Quantity ?? -1).Append('|')
                .Append(row == null ? -1 : (int)row.State).Append('|')
                .Append(row?.GridX ?? 0).Append('|')
                .Append(row?.GridY ?? 0).Append('|');
            AppendToken(canonical, row?.DestinationId);
            canonical.Append(row?.HasDestinationPosition == true ? 1 : 0)
                .Append('|')
                .Append(row?.DestinationGridX ?? 0).Append('|')
                .Append(row?.DestinationGridY ?? 0).Append('|');
            AppendToken(canonical, row?.AggregationCohortId);
            AppendToken(canonical, row?.SourceStorageDestinationId);
            canonical.Append(row?.Forbidden == true ? 1 : 0).Append('|')
                .Append(row == null ? -1 : (int)row.WasteOrigin).Append('|')
                .Append((row?.Contamination ?? 0f).ToString(
                    "R", CultureInfo.InvariantCulture)).Append('|')
                .Append(row == null ? -1 : (int)row.DropDisposition)
                .Append('|');
            AppendToken(canonical, row?.RecoveryOwnerOperationId);
            AppendToken(canonical, row?.RecoverySourceStackId);
            AppendToken(canonical, row?.RecoveryCarrierPersistentId);
            canonical.Append(row == null
                    ? -1
                    : (int)row.RecoveryInterruptionKind)
                .Append('|')
                .Append((row?.DroppedAtGameTime ?? 0d).ToString(
                    "R", CultureInfo.InvariantCulture)).Append('|')
                .Append((row?.RecoveryDeadlineGameTime ?? 0d).ToString(
                    "R", CultureInfo.InvariantCulture)).Append('|');
            foreach (ItemInstanceComponentSaveData component in
                     (row?.Components
                         ?? Array.Empty<ItemInstanceComponentSaveData>())
                     .Where(value => value != null)
                     .OrderBy(value => value.componentTypeId,
                         StringComparer.Ordinal)
                     .ThenBy(value => value.ToCanonicalString(),
                         StringComparer.Ordinal))
            {
                AppendToken(canonical, component.ToCanonicalString());
            }
            canonical.Append('|');
            if (row != null
                && FacilityOutputExactRouteCustodyCodec.TryRead(
                    row.Components,
                    out FacilityOutputExactRouteCustodyMetadata custody))
            {
                AppendToken(canonical, custody.BatchCommitId);
                AppendToken(canonical, custody.RouteOperationId);
                AppendToken(canonical, custody.OriginDestinationId);
                AppendToken(canonical, custody.CurrentSourceStackId);
                canonical.Append(custody.CurrentDeliveryRevision).Append('|');
                AppendToken(
                    canonical,
                    custody.CurrentDeliveryRevisionFingerprint);
                AppendToken(canonical, custody.CurrentTargetDestinationId);
                canonical.Append(custody.CurrentTargetPosition.x).Append('|')
                    .Append(custody.CurrentTargetPosition.y).Append('|');
                AppendToken(
                    canonical,
                    custody.CurrentTargetAuthorityFingerprint);
            }
            canonical.Append("||");
        }
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte value in digest)
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static void AppendToken(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append('|');
    }
}
