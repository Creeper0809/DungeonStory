using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IFacilityBufferDestinationReleaseService
{
    bool TryReleaseAtOwnerPosition(
        string destinationId,
        Vector2Int ownerPosition,
        string reasonCode,
        out int releasedQuantity,
        out string failureReason);
}

/// <summary>
/// Terminally closes a physical facility-buffer destination without sending
/// picked cargo back to its source. Unpicked leases are released logically;
/// picked slices are dropped at their carrier; deposited slices are released
/// at the former owner position. The destination claim/profile may be retired
/// only after this service succeeds.
/// </summary>
public sealed class FacilityBufferDestinationReleaseService :
    IFacilityBufferDestinationReleaseService
{
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IItemTransferService transfers;
    private readonly ICharacterWorldQuery characterWorld;

    public FacilityBufferDestinationReleaseService(
        IWorldItemStackRuntime worldItems,
        IItemTransferService transfers,
        ICharacterWorldQuery characterWorld)
    {
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public bool TryReleaseAtOwnerPosition(
        string destinationId,
        Vector2Int ownerPosition,
        string reasonCode,
        out int releasedQuantity,
        out string failureReason)
    {
        releasedQuantity = 0;
        failureReason = string.Empty;
        string destination = destinationId ?? string.Empty;
        string reason = reasonCode ?? string.Empty;
        if (destination.Length == 0
            || !string.Equals(destination, destination.Trim(), StringComparison.Ordinal)
            || reason.Length == 0
            || !string.Equals(reason, reason.Trim(), StringComparison.Ordinal))
        {
            failureReason = "facility-buffer-terminal-release-identity-invalid";
            return false;
        }

        HaulDeliveryIntentSaveData[] intents = worldItems
            .CaptureHaulDeliveryIntentsByDestination(destination)
            .OrderBy(intent => intent.operationId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, CharacterActor> actorsById =
            new(StringComparer.Ordinal);
        foreach (HaulDeliveryIntentSaveData intent in intents
                     .Where(intent => intent.HasCommittedPickup))
        {
            CharacterActor actor = (characterWorld.Characters
                    ?? Array.Empty<CharacterActor>())
                .SingleOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.BuildingCharacterId.Value,
                        intent.ownerCharacterId,
                        StringComparison.Ordinal));
            CharacterCarryInventory inventory = actor?.CarryInventory;
            AbilityHaul haul = actor?.GetComponent<AbilityHaul>();
            if (actor == null
                || inventory == null
                || haul == null
                || !haul.OwnsHaulOperation(intent.operationId)
                || intent.commitments.Any(commitment => commitment == null
                    || inventory.Items.Where(item => item != null
                            && string.Equals(
                                item.ownerOperationId,
                                intent.operationId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                item.carriedStackId,
                                commitment.carriedStackId,
                                StringComparison.Ordinal))
                        .Sum(item => Mathf.Max(0, item.quantity))
                    != commitment.quantity))
            {
                failureReason =
                    "facility-buffer-terminal-carried-preflight-failed:"
                    + intent.operationId;
                return false;
            }

            actorsById[intent.ownerCharacterId] = actor;
        }

        if (actorsById.Count > 0
            && worldItems is not IWorldItemCarryRecoveryRuntime)
        {
            failureReason =
                "facility-buffer-terminal-carry-recovery-unavailable";
            return false;
        }

        foreach (CharacterActor actor in actorsById.Values
                     .OrderBy(
                         value => value.BuildingCharacterId.Value,
                         StringComparer.Ordinal))
        {
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            if (!haul.TryStopHauling(
                    reason,
                    HaulInterruptionDisposition
                        .ReleaseUnpickedAndDropCarriedAtActor,
                    out string stopFailure))
            {
                failureReason =
                    "facility-buffer-terminal-carried-drop-failed:"
                    + actor.BuildingCharacterId.Value + ":" + stopFailure;
                return false;
            }
        }

        foreach (HaulDeliveryIntentSaveData intent in intents)
        {
            if (!worldItems.TryCaptureHaulDeliveryIntent(intent.operationId, out _))
                continue;

            if (!worldItems.ReleaseHaulDeliveryIntent(intent.operationId))
            {
                failureReason =
                    "facility-buffer-terminal-intent-release-failed:"
                    + intent.operationId;
                return false;
            }
        }

        if (worldItems.CaptureHaulDeliveryIntentsByDestination(destination).Count != 0)
        {
            failureReason = "facility-buffer-terminal-intent-release-incomplete";
            return false;
        }

        releasedQuantity = transfers.ReleaseDestination(
            destination,
            ownerPosition);
        return true;
    }
}
