using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public interface IHaulDeliveryIntentQuery
{
    bool TryCapture(string operationId, out HaulDeliveryIntentSaveData intent);
    IReadOnlyList<HaulDeliveryIntentSaveData> CaptureCommitted();
    bool MatchesCommittedReservation(
        string operationId,
        string stackId,
        string expectedSignature,
        int quantity);
    int GetCommittedQuantity(string destinationId, string itemId);
}

public interface IHaulDeliveryIntentCommand
{
    bool TryRegisterPlan(
        string operationId,
        string ownerCharacterId,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition,
        IReadOnlyList<WarehouseHaulAdmissionSaveData> warehouseAdmissions,
        out string failureReason);
    bool TryCommitPickup(
        string operationId,
        CharacterCarryInventory inventory,
        out string failureReason);
    bool TryRestoreCommitted(
        HaulDeliveryIntentSaveData intent,
        out string failureReason);
    bool Remove(string operationId);
    IReadOnlyList<HaulDeliveryIntentSaveData> CaptureRuntimeState();
    void ReplaceRuntimeState(IReadOnlyList<HaulDeliveryIntentSaveData> intents);
}

internal static class HaulDeliveryOperationIdentity
{
    public static string Format(string ownerCharacterId, long sequence)
    {
        string owner = ownerCharacterId?.Trim() ?? string.Empty;
        if (owner.Length == 0 || sequence <= 0)
            throw new ArgumentException("Haul operation identity is invalid.");
        return $"haul:{owner}:{sequence:D12}";
    }

    public static bool TryParse(
        string operationId,
        string ownerCharacterId,
        out long sequence)
    {
        sequence = 0;
        string owner = ownerCharacterId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        string prefix = $"haul:{owner}:";
        if (owner.Length == 0
            || !operation.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string suffix = operation.Substring(prefix.Length);
        return suffix.Length == 12
            && suffix.All(character => character >= '0' && character <= '9')
            && long.TryParse(suffix, out sequence)
            && sequence > 0
            && string.Equals(
                operation,
                Format(owner, sequence),
                StringComparison.Ordinal);
    }
}

/// <summary>
/// Transient haul-delivery authority. Definitions remain immutable. One
/// per-plan operation owns one exact destination and, after pickup commits,
/// the physical carried-stack quantities that may be queried, restored or
/// deposited. Missing or mismatched identity is never substituted.
/// </summary>
public sealed class HaulDeliveryIntentRuntime :
    IHaulDeliveryIntentQuery,
    IHaulDeliveryIntentCommand
{
    private readonly WorldItemRepository repository;
    private readonly Dictionary<string, HaulDeliveryIntentSaveData> byOperation =
        new(StringComparer.Ordinal);

    public HaulDeliveryIntentRuntime(WorldItemRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public bool TryRegisterPlan(
        string operationId,
        string ownerCharacterId,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition,
        IReadOnlyList<WarehouseHaulAdmissionSaveData> warehouseAdmissions,
        out string failureReason)
    {
        failureReason = string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        string owner = ownerCharacterId?.Trim() ?? string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (operation.Length == 0 || owner.Length == 0 || destination.Length == 0)
        {
            failureReason = "haul-delivery-intent-invalid";
            return false;
        }

        if (!HaulDeliveryOperationIdentity.TryParse(operation, owner, out _)
            || !Enum.IsDefined(typeof(WorldItemHaulDestinationKind), destinationKind))
        {
            failureReason = "haul-delivery-intent-identity-invalid:" + operation;
            return false;
        }

        if (byOperation.ContainsKey(operation))
        {
            failureReason = "haul-delivery-operation-duplicate:" + operation;
            return false;
        }

        byOperation.Add(operation, new HaulDeliveryIntentSaveData
        {
            operationId = operation,
            ownerCharacterId = owner,
            destinationKind = destinationKind,
            destinationId = destination,
            deliveryGridX = deliveryPosition.x,
            deliveryGridY = deliveryPosition.y,
            dropGridX = dropPosition.x,
            dropGridY = dropPosition.y,
            warehouseAdmissions = (warehouseAdmissions
                    ?? Array.Empty<WarehouseHaulAdmissionSaveData>())
                .Where(value => value != null)
                .Select(CloneAdmission)
                .ToList(),
            commitments = new List<HaulDeliveryItemCommitmentSaveData>()
        });
        return true;
    }

    public bool TryCommitPickup(
        string operationId,
        CharacterCarryInventory inventory,
        out string failureReason)
    {
        failureReason = string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        if (!byOperation.TryGetValue(operation, out HaulDeliveryIntentSaveData intent)
            || inventory == null)
        {
            failureReason = "haul-delivery-intent-missing:" + operation;
            return false;
        }

        HaulDeliveryItemCommitmentSaveData[] committed = inventory.Items
            .Where(item => item != null
                && string.Equals(
                    item.ownerOperationId?.Trim(),
                    operation,
                    StringComparison.Ordinal))
            .OrderBy(item => item.carriedStackId, StringComparer.Ordinal)
            .Select(item => new HaulDeliveryItemCommitmentSaveData
            {
                carriedStackId = item.carriedStackId?.Trim() ?? string.Empty,
                sourceStackId = item.sourceStackId?.Trim() ?? string.Empty,
                itemId = item.itemId?.Trim() ?? string.Empty,
                expectedStackSignature = ItemReservationSignature.Create(
                    item.itemId,
                    item.components),
                quantity = item.quantity
            })
            .ToArray();
        if (committed.Length == 0
            || committed.Any(value => value.carriedStackId.Length == 0
                || value.itemId.Length == 0
                || value.expectedStackSignature.Length == 0
                || value.quantity <= 0))
        {
            failureReason = "haul-pickup-physical-commitment-missing:" + operation;
            return false;
        }

        foreach (HaulDeliveryItemCommitmentSaveData commitment in committed)
        {
            if (!TryValidatePhysicalCommitment(
                    intent,
                    commitment,
                    out failureReason))
            {
                return false;
            }
        }

        intent.commitments = committed.ToList();
        return true;
    }

    public bool TryRestoreCommitted(
        HaulDeliveryIntentSaveData intent,
        out string failureReason)
    {
        failureReason = Validate(intent);
        if (failureReason.Length > 0)
            return false;
        if (!intent.HasCommittedPickup)
        {
            failureReason = "haul-restore-has-no-committed-pickup";
            return false;
        }
        foreach (HaulDeliveryItemCommitmentSaveData commitment in intent.commitments)
        {
            if (!TryValidatePhysicalCommitment(
                    intent,
                    commitment,
                    out failureReason))
            {
                return false;
            }
        }
        if (byOperation.ContainsKey(intent.operationId.Trim()))
        {
            failureReason = "haul-delivery-operation-duplicate:" + intent.operationId.Trim();
            return false;
        }
        byOperation.Add(intent.operationId.Trim(), Clone(intent));
        return true;
    }

    public bool TryCapture(
        string operationId,
        out HaulDeliveryIntentSaveData intent)
    {
        if (byOperation.TryGetValue(
                operationId?.Trim() ?? string.Empty,
                out HaulDeliveryIntentSaveData state))
        {
            intent = Clone(state);
            return true;
        }
        intent = null;
        return false;
    }

    public IReadOnlyList<HaulDeliveryIntentSaveData> CaptureCommitted()
    {
        List<HaulDeliveryIntentSaveData> result = new();
        foreach (HaulDeliveryIntentSaveData intent in byOperation.Values
                     .Where(value => value != null && value.HasCommittedPickup)
                     .OrderBy(value => value.operationId, StringComparer.Ordinal))
        {
            foreach (HaulDeliveryItemCommitmentSaveData commitment in intent.commitments)
            {
                if (!TryValidatePhysicalCommitment(
                        intent,
                        commitment,
                        out string failureReason))
                {
                    throw new InvalidOperationException(failureReason);
                }
            }
            HaulDeliveryIntentSaveData committedProjection = Clone(intent);
            ExactWarehouseHaulAdmissionJoin.RetainCommittedAdmissions(
                committedProjection);
            result.Add(committedProjection);
        }
        return result;
    }

    public bool MatchesCommittedReservation(
        string operationId,
        string stackId,
        string expectedSignature,
        int quantity)
    {
        return byOperation.TryGetValue(
                operationId?.Trim() ?? string.Empty,
                out HaulDeliveryIntentSaveData intent)
            && intent.commitments.Any(commitment => commitment != null
                && commitment.quantity == quantity
                && string.Equals(
                    commitment.carriedStackId,
                    stackId?.Trim(),
                    StringComparison.Ordinal)
                && string.Equals(
                    commitment.expectedStackSignature,
                    expectedSignature?.Trim(),
                    StringComparison.Ordinal));
    }

    public int GetCommittedQuantity(string destinationId, string itemId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        string item = itemId?.Trim() ?? string.Empty;
        if (destination.Length == 0 || item.Length == 0)
            return 0;
        int total = 0;
        foreach (HaulDeliveryIntentSaveData intent in byOperation.Values
                     .Where(intent => intent != null
                         && string.Equals(
                             intent.destinationId,
                             destination,
                             StringComparison.Ordinal)))
        {
            foreach (HaulDeliveryItemCommitmentSaveData commitment in
                     intent.commitments ?? new List<HaulDeliveryItemCommitmentSaveData>())
            {
                if (commitment == null
                    || !string.Equals(commitment.itemId, item, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!TryValidatePhysicalCommitment(
                        intent,
                        commitment,
                        out string failureReason))
                {
                    throw new InvalidOperationException(failureReason);
                }
                total = checked(total + commitment.quantity);
            }
        }
        return total;
    }

    public bool Remove(string operationId) =>
        RemoveCore(operationId, string.Empty);

    private bool RemoveCore(
        string operationId,
        string authorityReleasePlanFingerprint)
    {
        string operation = operationId?.Trim() ?? string.Empty;
        if (repository.TryGetActiveCapacityRoutingAuthorityRelease(
                operation,
                out ProductionCapacityRoutingActorAuthorityReleaseSaveData
                    activeRelease)
            && !string.Equals(
                activeRelease.planFingerprint,
                authorityReleasePlanFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }
        return byOperation.Remove(operation);
    }

    internal ExactAuthorityReleaseStatus TryRemoveExact(
        string operationId,
        string expectedIntentFingerprint,
        string authorityReleasePlanFingerprint,
        out string failureReason)
    {
        failureReason = string.Empty;
        string operation = operationId ?? string.Empty;
        if (!repository.TryGetActiveCapacityRoutingAuthorityRelease(
                operation,
                out ProductionCapacityRoutingActorAuthorityReleaseSaveData plan)
            || !string.Equals(
                plan.planFingerprint,
                authorityReleasePlanFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-exact-intent-release-plan-conflict";
            return ExactAuthorityReleaseStatus.Conflict;
        }
        ProductionCapacityRoutingOperationAuthorityRowSaveData row =
            plan.operations.FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.operationId,
                    operation,
                    StringComparison.Ordinal));
        if (row == null
            || !string.Equals(
                row.haulIntentFingerprint,
                expectedIntentFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-exact-intent-release-plan-conflict";
            return ExactAuthorityReleaseStatus.Conflict;
        }
        if (!byOperation.TryGetValue(
                operation,
                out HaulDeliveryIntentSaveData intent))
        {
            return ExactAuthorityReleaseStatus.Replay;
        }
        if (!string.Equals(
                CreateCapacityRoutingAuthorityFingerprint(intent),
                expectedIntentFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-exact-intent-release-live-conflict";
            return ExactAuthorityReleaseStatus.Conflict;
        }
        if (!RemoveCore(operation, authorityReleasePlanFingerprint))
        {
            failureReason =
                "capacity-routing-exact-intent-release-failed";
            return ExactAuthorityReleaseStatus.Conflict;
        }
        return ExactAuthorityReleaseStatus.Applied;
    }

    internal static string CreateCapacityRoutingAuthorityFingerprint(
        HaulDeliveryIntentSaveData intent)
    {
        if (intent == null)
            throw new ArgumentNullException(nameof(intent));
        StringBuilder canonical = new StringBuilder(512)
            .Append("capacity-routing-haul-intent@1|");
        AppendToken(canonical, intent.operationId);
        AppendToken(canonical, intent.ownerCharacterId);
        canonical.Append(((int)intent.destinationKind)
                .ToString(CultureInfo.InvariantCulture))
            .Append('|');
        AppendToken(canonical, intent.destinationId);
        canonical.Append(intent.deliveryGridX).Append(':')
            .Append(intent.deliveryGridY).Append(':')
            .Append(intent.dropGridX).Append(':')
            .Append(intent.dropGridY).Append('|');
        foreach (WarehouseHaulAdmissionSaveData admission in
                 (intent.warehouseAdmissions
                     ?? new List<WarehouseHaulAdmissionSaveData>())
                 .OrderBy(value => value?.tokenId, StringComparer.Ordinal))
        {
            AppendToken(canonical, admission?.tokenId);
            AppendToken(canonical, admission?.ownerAdmissionOperationId);
            AppendToken(canonical, admission?.warehouseId);
            AppendToken(canonical, admission?.sourceWarehouseId);
            AppendToken(canonical, admission?.sourceStackId);
            AppendToken(canonical, admission?.itemId);
            AppendToken(canonical, admission?.itemInstanceId);
            AppendToken(canonical, admission?.lotFingerprint);
            canonical.Append(admission?.quantity ?? -1).Append(':')
                .Append(admission?.reservedMassGrams ?? -1L).Append(':')
                .Append(admission?.catalogRevision ?? -1L).Append(':')
                .Append(admission?.sourceRevision ?? -1L).Append(';');
        }
        canonical.Append('|');
        foreach (HaulDeliveryItemCommitmentSaveData commitment in
                 (intent.commitments
                     ?? new List<HaulDeliveryItemCommitmentSaveData>())
                 .OrderBy(value => value?.carriedStackId, StringComparer.Ordinal))
        {
            AppendToken(canonical, commitment?.carriedStackId);
            AppendToken(canonical, commitment?.sourceStackId);
            AppendToken(canonical, commitment?.itemId);
            AppendToken(canonical, commitment?.expectedStackSignature);
            canonical.Append(commitment?.quantity ?? -1).Append(';');
        }
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        StringBuilder result = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static void AppendToken(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append(';');
    }

    public IReadOnlyList<HaulDeliveryIntentSaveData> CaptureRuntimeState() =>
        byOperation.Values
            .OrderBy(intent => intent.operationId, StringComparer.Ordinal)
            .Select(Clone)
            .ToArray();

    public void ReplaceRuntimeState(IReadOnlyList<HaulDeliveryIntentSaveData> intents)
    {
        Dictionary<string, HaulDeliveryIntentSaveData> replacement =
            new(StringComparer.Ordinal);
        foreach (HaulDeliveryIntentSaveData intent in intents
                     ?? Array.Empty<HaulDeliveryIntentSaveData>())
        {
            string failure = Validate(intent);
            if (failure.Length > 0
                || !replacement.TryAdd(intent.operationId.Trim(), Clone(intent)))
            {
                throw new InvalidOperationException(
                    failure.Length > 0 ? failure : "duplicate haul delivery intent");
            }
        }
        byOperation.Clear();
        foreach (KeyValuePair<string, HaulDeliveryIntentSaveData> pair in replacement)
            byOperation.Add(pair.Key, pair.Value);
    }

    private static string Validate(HaulDeliveryIntentSaveData intent)
    {
        if (intent == null
            || string.IsNullOrWhiteSpace(intent.operationId)
            || string.IsNullOrWhiteSpace(intent.ownerCharacterId)
            || string.IsNullOrWhiteSpace(intent.destinationId)
            || !HaulDeliveryOperationIdentity.TryParse(
                intent.operationId,
                intent.ownerCharacterId,
                out _)
             || !Enum.IsDefined(
                 typeof(WorldItemHaulDestinationKind),
                 intent.destinationKind)
             || intent.warehouseAdmissions == null
             || intent.commitments == null)
        {
            return "haul-delivery-intent-invalid";
        }
        HashSet<string> admissionOperations = new(StringComparer.Ordinal);
        foreach (WarehouseHaulAdmissionSaveData admission in intent.warehouseAdmissions)
        {
            if (admission == null
                || string.IsNullOrWhiteSpace(admission.tokenId)
                || string.IsNullOrWhiteSpace(admission.ownerAdmissionOperationId)
                || !admissionOperations.Add(admission.ownerAdmissionOperationId.Trim())
                || string.IsNullOrWhiteSpace(admission.warehouseId)
                || string.IsNullOrWhiteSpace(admission.sourceStackId)
                || string.IsNullOrWhiteSpace(admission.itemId)
                || string.IsNullOrWhiteSpace(admission.lotFingerprint)
                || admission.quantity <= 0
                || admission.reservedMassGrams <= 0L)
            {
                return "haul-delivery-warehouse-admission-invalid:" + intent.operationId;
            }
        }
        HashSet<string> stacks = new(StringComparer.Ordinal);
        foreach (HaulDeliveryItemCommitmentSaveData commitment in intent.commitments)
        {
            if (commitment == null
                || string.IsNullOrWhiteSpace(commitment.carriedStackId)
                || !stacks.Add(commitment.carriedStackId.Trim())
                || string.IsNullOrWhiteSpace(commitment.itemId)
                || string.IsNullOrWhiteSpace(commitment.expectedStackSignature)
                || commitment.quantity <= 0)
            {
                return "haul-delivery-commitment-invalid:" + intent.operationId;
            }
        }
        return string.Empty;
    }

    private bool TryValidatePhysicalCommitment(
        HaulDeliveryIntentSaveData intent,
        HaulDeliveryItemCommitmentSaveData commitment,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (intent == null
            || commitment == null
            || !repository.RecordsById.TryGetValue(
                commitment.carriedStackId,
                out WorldItemStackRecord record)
            || record == null
            || record.state != WorldItemStackState.Carried
            || !string.Equals(
                record.destinationId?.Trim(),
                intent.ownerCharacterId?.Trim(),
                StringComparison.Ordinal)
            || record.quantity != commitment.quantity
            || !string.Equals(record.itemId, commitment.itemId, StringComparison.Ordinal)
            || !string.Equals(
                ItemReservationSignature.Create(record.itemId, record.components),
                commitment.expectedStackSignature,
                StringComparison.Ordinal))
        {
            failureReason =
                $"haul-delivery-physical-lease-mismatch:{intent?.operationId}:"
                + commitment?.carriedStackId;
            return false;
        }
        return true;
    }

    private static HaulDeliveryIntentSaveData Clone(HaulDeliveryIntentSaveData source) =>
        new()
        {
            operationId = source?.operationId?.Trim() ?? string.Empty,
            ownerCharacterId = source?.ownerCharacterId?.Trim() ?? string.Empty,
            destinationKind = source?.destinationKind ?? default,
            destinationId = source?.destinationId?.Trim() ?? string.Empty,
            deliveryGridX = source?.deliveryGridX ?? 0,
            deliveryGridY = source?.deliveryGridY ?? 0,
            dropGridX = source?.dropGridX ?? 0,
            dropGridY = source?.dropGridY ?? 0,
            warehouseAdmissions = (source?.warehouseAdmissions
                    ?? new List<WarehouseHaulAdmissionSaveData>())
                .Where(value => value != null)
                .Select(CloneAdmission)
                .ToList(),
            commitments = (source?.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Where(value => value != null)
                .Select(value => new HaulDeliveryItemCommitmentSaveData
                {
                    carriedStackId = value.carriedStackId?.Trim() ?? string.Empty,
                    sourceStackId = value.sourceStackId?.Trim() ?? string.Empty,
                    itemId = value.itemId?.Trim() ?? string.Empty,
                    expectedStackSignature =
                        value.expectedStackSignature?.Trim() ?? string.Empty,
                    quantity = value.quantity
                })
                .ToList()
        };

    private static WarehouseHaulAdmissionSaveData CloneAdmission(
        WarehouseHaulAdmissionSaveData source) =>
        new()
        {
            tokenId = source?.tokenId?.Trim() ?? string.Empty,
            ownerAdmissionOperationId =
                source?.ownerAdmissionOperationId?.Trim() ?? string.Empty,
            warehouseId = source?.warehouseId?.Trim() ?? string.Empty,
            sourceWarehouseId = source?.sourceWarehouseId?.Trim() ?? string.Empty,
            sourceStackId = source?.sourceStackId?.Trim() ?? string.Empty,
            itemId = source?.itemId?.Trim() ?? string.Empty,
            itemInstanceId = source?.itemInstanceId?.Trim() ?? string.Empty,
            lotFingerprint = source?.lotFingerprint?.Trim() ?? string.Empty,
            quantity = source?.quantity ?? 0,
            reservedMassGrams = source?.reservedMassGrams ?? 0L,
            catalogRevision = source?.catalogRevision ?? 0L,
            sourceRevision = source?.sourceRevision ?? 0L
        };
}
