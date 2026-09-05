using System.Collections.Generic;

internal static class CharacterMedicalOrderPersistence
{
    public static CharacterMedicalOrder Clone(CharacterMedicalOrder source)
    {
        return new CharacterMedicalOrder
        {
            orderId = source.orderId ?? string.Empty,
            patientId = source.patientId ?? string.Empty,
            rescuerId = source.rescuerId ?? string.Empty,
            treatmentFacilityId = source.treatmentFacilityId ?? string.Empty,
            state = source.state,
            stabilized = source.stabilized,
            carried = source.carried,
            requiredStabilizationWork = source.requiredStabilizationWork,
            completedStabilizationWork = source.completedStabilizationWork,
            requiredTreatmentWork = source.requiredTreatmentWork,
            completedTreatmentWork = source.completedTreatmentWork,
            treatmentSupply = source.treatmentSupply,
            treatmentSupplyConsumed = source.treatmentSupplyConsumed,
            treatmentSupplyDeliveryRequested = source.treatmentSupplyDeliveryRequested,
            treatmentItemId = source.treatmentItemId ?? string.Empty,
            treatmentPotency = source.treatmentPotency,
            treatmentInfectionReduction = source.treatmentInfectionReduction,
            treatmentPainReduction = source.treatmentPainReduction,
            treatmentMaterialDestinationId =
                source.treatmentMaterialDestinationId ?? string.Empty,
            nextTreatmentMaterialDestinationSequence =
                source.nextTreatmentMaterialDestinationSequence,
            treatmentDestinationSequence = source.treatmentDestinationSequence,
            treatmentBufferCapacityGrams =
                source.treatmentBufferCapacityGrams,
            treatmentMassAuthorityRevision =
                source.treatmentMassAuthorityRevision,
            treatmentCapacityFingerprint =
                source.treatmentCapacityFingerprint ?? string.Empty,
            treatmentDestinationDrainJoins =
                CloneDestinationDrainJoins(
                    source.treatmentDestinationDrainJoins),
            treatmentSupplyCommitPhase = source.treatmentSupplyCommitPhase,
            treatmentSupplyOperationSequence =
                source.treatmentSupplyOperationSequence,
            treatmentSupplyOperationId =
                source.treatmentSupplyOperationId ?? string.Empty,
            treatmentSupplyReasonCode =
                source.treatmentSupplyReasonCode ?? string.Empty,
            treatmentPhysicalItemId =
                source.treatmentPhysicalItemId ?? string.Empty,
            treatmentPhysicalQuantity = source.treatmentPhysicalQuantity,
            treatmentOutputX = source.treatmentOutputX,
            treatmentOutputY = source.treatmentOutputY,
            treatmentSourceStackIds = source.treatmentSourceStackIds != null
                ? new List<string>(source.treatmentSourceStackIds)
                : new List<string>(),
            treatmentInputMassGrams = source.treatmentInputMassGrams,
            treatmentPhysicalCommitId =
                source.treatmentPhysicalCommitId ?? string.Empty,
            patientX = source.patientX,
            patientY = source.patientY,
            bedX = source.bedX,
            bedY = source.bedY,
            statusCode = source.statusCode,
            statusParameters = source.statusParameters != null
                ? new List<string>(source.statusParameters)
                : new List<string>()
        };
    }

    internal static CharacterMedicalSupplyDestinationDrainJoinData
        CloneDestinationDrainJoin(
            CharacterMedicalSupplyDestinationDrainJoinData source)
    {
        if (source == null)
        {
            return null;
        }

        return new CharacterMedicalSupplyDestinationDrainJoinData
        {
            destinationSequence = source.destinationSequence,
            phase = source.phase,
            targetState = source.targetState,
            targetStatusCode = source.targetStatusCode,
            targetStatusParameters = source.targetStatusParameters != null
                ? new List<string>(source.targetStatusParameters)
                : new List<string>(),
            parentOperationId = source.parentOperationId ?? string.Empty,
            stepOperationId = source.stepOperationId ?? string.Empty,
            ownerFacilityId = source.ownerFacilityId ?? string.Empty,
            sourceDestinationId = source.sourceDestinationId ?? string.Empty,
            sourceBufferCapacityGrams = source.sourceBufferCapacityGrams,
            sourceMassAuthorityRevision = source.sourceMassAuthorityRevision,
            sourceCapacityFingerprint =
                source.sourceCapacityFingerprint ?? string.Empty,
            requestFingerprint = source.requestFingerprint ?? string.Empty,
            commitId = source.commitId ?? string.Empty,
            receiptFingerprint = source.receiptFingerprint ?? string.Empty,
            inputQuantity = source.inputQuantity,
            inputMassGrams = source.inputMassGrams,
            ownerX = source.ownerX,
            ownerY = source.ownerY
        };
    }

    private static List<CharacterMedicalSupplyDestinationDrainJoinData>
        CloneDestinationDrainJoins(
            IEnumerable<CharacterMedicalSupplyDestinationDrainJoinData>
                source)
    {
        List<CharacterMedicalSupplyDestinationDrainJoinData> result = new();
        if (source == null)
        {
            return result;
        }

        foreach (CharacterMedicalSupplyDestinationDrainJoinData value in source)
        {
            result.Add(CloneDestinationDrainJoin(value));
        }
        return result;
    }
}
