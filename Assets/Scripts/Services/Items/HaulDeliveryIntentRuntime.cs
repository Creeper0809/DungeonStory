using System;
using System.Collections.Generic;
using System.Linq;
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
            result.Add(Clone(intent));
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
        byOperation.Remove(operationId?.Trim() ?? string.Empty);

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
            || intent.commitments == null)
        {
            return "haul-delivery-intent-invalid";
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
}
