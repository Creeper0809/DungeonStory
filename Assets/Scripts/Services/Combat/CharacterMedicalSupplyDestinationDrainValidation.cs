using System;
using System.Collections.Generic;
using System.Linq;

internal static class CharacterMedicalSupplyDestinationDrainValidation
{
    internal const int MaximumJoinsPerOrder = 64;

    internal static void ValidateLocal(
        CharacterMedicalOrder order,
        DungeonGameRestoreReport report)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        List<CharacterMedicalSupplyDestinationDrainJoinData> joins =
            order.treatmentDestinationDrainJoins;
        if (joins == null)
        {
            report.AddError(
                $"Medical order '{order.orderId}' is missing its destination-drain join collection.");
            return;
        }
        if (joins.Count > MaximumJoinsPerOrder)
        {
            report.AddError(
                $"Medical order '{order.orderId}' exceeds {MaximumJoinsPerOrder} destination-drain joins.");
        }

        int previousSequence = 0;
        int activeCount = 0;
        CharacterMedicalSupplyDestinationDrainJoinData active = null;
        foreach (CharacterMedicalSupplyDestinationDrainJoinData join in joins)
        {
            if (!TryValidateJoin(order, join, out string failureReason))
            {
                report.AddError(failureReason);
                continue;
            }
            if (join.destinationSequence <= previousSequence)
            {
                report.AddError(
                    $"Medical order '{order.orderId}' destination-drain joins are not strictly sequence ordered.");
            }
            previousSequence = Math.Max(previousSequence, join.destinationSequence);
            if (join.phase != CharacterMedicalSupplyDestinationDrainPhase
                    .ClosedAwaitingCheckpointGc)
            {
                activeCount++;
                active = join;
            }
        }

        if (activeCount > 1)
        {
            report.AddError(
                $"Medical order '{order.orderId}' has more than one active destination drain.");
        }
        if (order.nextTreatmentMaterialDestinationSequence <=
            Math.Max(order.treatmentDestinationSequence, previousSequence))
        {
            report.AddError(
                $"Medical order '{order.orderId}' can reuse a destination lifetime sequence.");
        }

        bool isDraining = order.state ==
            CharacterMedicalOrderState.MaterialDestinationDraining;
        if ((activeCount == 1) != isDraining)
        {
            report.AddError(
                $"Medical order '{order.orderId}' has an invalid active destination-drain state join.");
        }
        if (active == null)
        {
            return;
        }

        if (order.treatmentDestinationSequence != active.destinationSequence
            || !string.Equals(
                order.treatmentFacilityId,
                active.ownerFacilityId,
                StringComparison.Ordinal)
            || !string.Equals(
                order.treatmentMaterialDestinationId,
                active.sourceDestinationId,
                StringComparison.Ordinal)
            || order.treatmentBufferCapacityGrams !=
                active.sourceBufferCapacityGrams
            || order.treatmentMassAuthorityRevision !=
                active.sourceMassAuthorityRevision
            || !string.Equals(
                order.treatmentCapacityFingerprint,
                active.sourceCapacityFingerprint,
                StringComparison.Ordinal))
        {
            report.AddError(
                $"Medical order '{order.orderId}' active destination drain does not freeze its current authority exactly.");
        }
    }

    internal static bool TryValidateJoin(
        CharacterMedicalOrder order,
        CharacterMedicalSupplyDestinationDrainJoinData join,
        out string failureReason)
    {
        failureReason = string.Empty;
        string orderId = order?.orderId ?? string.Empty;
        if (join == null
            || join.destinationSequence <= 0
            || !Enum.IsDefined(
                typeof(CharacterMedicalSupplyDestinationDrainPhase),
                join.phase)
            || join.phase == CharacterMedicalSupplyDestinationDrainPhase.None
            || !IsAllowedTarget(join.targetState)
            || !Enum.IsDefined(
                typeof(CharacterMedicalStatusCode),
                join.targetStatusCode)
            || join.targetStatusCode == CharacterMedicalStatusCode.Unknown
            || join.targetStatusCode ==
                CharacterMedicalStatusCode.MaterialDestinationDraining
            || !AreCanonicalParameters(join.targetStatusParameters)
            || !string.Equals(
                join.parentOperationId,
                CharacterMedicalSupplyDestinationAuthority
                    .FormatParentOperationId(
                        orderId,
                        join.destinationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                join.stepOperationId,
                CharacterMedicalSupplyDestinationAuthority
                    .FormatStepOperationId(
                        orderId,
                        join.destinationSequence),
                StringComparison.Ordinal)
            || !IsCanonical(join.ownerFacilityId)
            || !string.Equals(
                join.sourceDestinationId,
                CharacterMedicalSupplyDestinationAuthority.FormatDestinationId(
                    orderId,
                    join.destinationSequence),
                StringComparison.Ordinal)
            || join.sourceBufferCapacityGrams <= 0L
            || join.sourceMassAuthorityRevision <= 0L
            || !IsLowerHexSha256(join.sourceCapacityFingerprint)
            || !IsLowerHexSha256(join.requestFingerprint)
            || join.inputQuantity < 0
            || join.inputMassGrams < 0L
            || (join.inputQuantity == 0) != (join.inputMassGrams == 0L))
        {
            failureReason =
                $"Medical order '{orderId}' contains an invalid destination-drain join.";
            return false;
        }

        bool effectCommitted = join.phase is
            CharacterMedicalSupplyDestinationDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or CharacterMedicalSupplyDestinationDrainPhase
                .OwnerAcknowledgedAwaitingClosure
            or CharacterMedicalSupplyDestinationDrainPhase
                .ClosedAwaitingCheckpointGc;
        if (effectCommitted
            && (!IsCanonical(join.commitId)
                || !IsLowerHexSha256(join.receiptFingerprint))
            || !effectCommitted
            && (!string.IsNullOrEmpty(join.commitId)
                || !string.IsNullOrEmpty(join.receiptFingerprint)))
        {
            failureReason =
                $"Medical order '{orderId}' destination-drain effect provenance is invalid.";
            return false;
        }
        return true;
    }

    private static bool IsAllowedTarget(CharacterMedicalOrderState value) =>
        value is CharacterMedicalOrderState.AwaitingStabilization
            or CharacterMedicalOrderState.AwaitingRescue
            or CharacterMedicalOrderState.AwaitingBed
            or CharacterMedicalOrderState.Completed
            or CharacterMedicalOrderState.Cancelled;

    private static bool AreCanonicalParameters(IReadOnlyList<string> values) =>
        values != null
        && values.Count <= 4
        && values.All(value => value != null
            && value.Length <= 128
            && string.Equals(value, value.Trim(), StringComparison.Ordinal));

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static bool IsLowerHexSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}
