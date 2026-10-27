using System;
using System.Runtime.CompilerServices;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class SocietyResponseMovementProvenanceRuntime
{
    private readonly ConditionalWeakTable<AbilityMove, State> states = new();

    internal State GetState(AbilityMove movement)
    {
        if (movement == null) return null;
        State state = states.GetValue(movement, _ => new State());
        if (state.CancellationRevision != movement.MovementCancellationRevision)
        {
            state.ClearSocietyResponseMovementProvenance();
            state.CancellationRevision = movement.MovementCancellationRevision;
        }
        return state;
    }

    internal bool TryStartSystemMove(AbilityMove movement, string responseOperationId,
        string externalOperationId, BuildingInstanceId facilityId, Vector2Int destination,
        DoorAccessOverrideKind overrideKind, out string message)
    {
        if (movement == null) { message = "Society response movement is unavailable."; return false; }
        if (!GetState(movement).CanStart(responseOperationId, externalOperationId, facilityId, destination, out message)) return false;
        if (!movement.TryStartSystemMove(destination, overrideKind, out message)) return false;
        GetState(movement).RecordStarted(responseOperationId, externalOperationId, facilityId, destination);
        return true;
    }

    internal bool TryRecordReceipt(AbilityMove movement, string responseOperationId,
        string externalOperationId, string receiptId, out string failure)
    {
        State state = GetState(movement);
        CharacterActor actor = movement != null ? movement.GetComponent<CharacterActor>() : null;
        if (state == null || actor == null)
        { failure = "society-response-movement-receipt-conflict"; return false; }
        return state.TryRecordSocietyResponseMovementReceipt(responseOperationId, externalOperationId,
            receiptId, actor.GetNowXY(), out failure);
    }

    internal sealed class State
    {
        internal long CancellationRevision;

        private string societyResponseMovementOperationId = string.Empty;
        private string societyResponseMovementExternalOperationId = string.Empty;
        private string societyResponseMovementFacilityId = string.Empty;
        private Vector2Int? societyResponseMovementDestination;
        private string societyResponseMovementReceiptId = string.Empty;
        internal string SocietyResponseMovementOperationId => societyResponseMovementOperationId;
        internal string SocietyResponseMovementExternalOperationId => societyResponseMovementExternalOperationId;
        internal string SocietyResponseMovementFacilityId => societyResponseMovementFacilityId;
        internal Vector2Int? SocietyResponseMovementDestination => societyResponseMovementDestination;
        internal string SocietyResponseMovementReceiptId => societyResponseMovementReceiptId;

        internal bool CanStart(string responseOperationId, string externalOperationId,
            BuildingInstanceId facilityId, Vector2Int destination, out string message)
        {
            if (!IsCanonicalRequiredId(responseOperationId)
                || !IsCanonicalRequiredId(externalOperationId)
                || !facilityId.IsValid)
            {
                message = "Society response movement provenance is invalid.";
                return false;
            }
            if (!string.IsNullOrEmpty(societyResponseMovementOperationId)
                && (!string.Equals(
                        societyResponseMovementOperationId,
                        responseOperationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        societyResponseMovementExternalOperationId,
                        externalOperationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        societyResponseMovementFacilityId,
                        facilityId.Value,
                        StringComparison.Ordinal)
                    || societyResponseMovementDestination != destination
                    || !string.IsNullOrEmpty(
                        societyResponseMovementReceiptId)))
            {
                message = "A different Society response movement already owns the actor.";
                return false;
            }
            message = string.Empty;
            return true;
        }
        internal void RecordStarted(string responseOperationId, string externalOperationId,
            BuildingInstanceId facilityId, Vector2Int destination)
        {
            societyResponseMovementOperationId = responseOperationId;
            societyResponseMovementExternalOperationId = externalOperationId;
            societyResponseMovementFacilityId = facilityId.Value;
            societyResponseMovementDestination = destination;
            societyResponseMovementReceiptId = string.Empty;
        }
        internal bool TryRecordSocietyResponseMovementReceipt(
            string responseOperationId,
            string externalOperationId,
            string receiptId,
            Vector2Int? currentPosition,
            out string failure)
        {
            failure = string.Empty;
            if (!IsCanonicalRequiredId(receiptId)
                || !string.Equals(
                    societyResponseMovementOperationId,
                    responseOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    societyResponseMovementExternalOperationId,
                    externalOperationId,
                    StringComparison.Ordinal)
                || !societyResponseMovementDestination.HasValue
                || !currentPosition.HasValue
                || currentPosition.Value != societyResponseMovementDestination.Value)
            {
                failure = "society-response-movement-receipt-conflict";
                return false;
            }
            if (string.IsNullOrEmpty(societyResponseMovementReceiptId))
            {
                societyResponseMovementReceiptId = receiptId;
                return true;
            }
            if (string.Equals(
                    societyResponseMovementReceiptId,
                    receiptId,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "society-response-movement-receipt-conflict";
            return false;
        }

        internal bool TryReleaseSocietyResponseMovementProvenance(
            string responseOperationId,
            string externalOperationId)
        {
            if (string.IsNullOrEmpty(societyResponseMovementOperationId))
            {
                return true;
            }
            if (!string.Equals(
                    societyResponseMovementOperationId,
                    responseOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    societyResponseMovementExternalOperationId,
                    externalOperationId,
                    StringComparison.Ordinal))
            {
                return false;
            }
            ClearSocietyResponseMovementProvenance();
            return true;
        }

        internal void RestoreSocietyResponseMovementProvenance(
            string responseOperationId,
            string externalOperationId,
            string facilityId,
            int destinationX,
            int destinationY,
            string receiptId)
        {
            responseOperationId ??= string.Empty;
            externalOperationId ??= string.Empty;
            facilityId ??= string.Empty;
            receiptId ??= string.Empty;
            if (responseOperationId.Length == 0)
            {
                if (externalOperationId.Length != 0
                    || facilityId.Length != 0
                    || destinationX != 0
                    || destinationY != 0
                    || receiptId.Length != 0)
                {
                    throw new InvalidOperationException(
                        "Empty Society response movement ownership has non-empty provenance.");
                }
                ClearSocietyResponseMovementProvenance();
                return;
            }
            if (!IsCanonicalRequiredId(responseOperationId)
                || !IsCanonicalRequiredId(externalOperationId)
                || !new BuildingInstanceId(facilityId).IsValid
                || receiptId.Length > 0 && !IsCanonicalRequiredId(receiptId))
            {
                throw new InvalidOperationException(
                    "Society response movement restore provenance is invalid.");
            }
            societyResponseMovementOperationId = responseOperationId;
            societyResponseMovementExternalOperationId = externalOperationId;
            societyResponseMovementFacilityId = facilityId;
            societyResponseMovementDestination = new Vector2Int(
                destinationX,
                destinationY);
            societyResponseMovementReceiptId = receiptId;
        }

        internal void ClearSocietyResponseMovementProvenance()
        {
            societyResponseMovementOperationId = string.Empty;
            societyResponseMovementExternalOperationId = string.Empty;
            societyResponseMovementFacilityId = string.Empty;
            societyResponseMovementDestination = null;
            societyResponseMovementReceiptId = string.Empty;
        }

        private static bool IsCanonicalRequiredId(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    }

}
