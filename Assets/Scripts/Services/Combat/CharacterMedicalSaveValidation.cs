using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class CharacterMedicalSaveValidation
{
    internal const int MaximumOrders = 512;

    public static void Validate(
        DungeonCharacterMedicalSaveData payload,
        DungeonGameRestoreReport report,
        IResourceEconomyContentCatalog content,
        IItemDefinitionCatalog itemDefinitions)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (payload == null)
        {
            report.AddError("Character medical payload is null.");
            return;
        }
        if (payload.version != DungeonCharacterMedicalSaveData.CurrentVersion)
        {
            report.AddError(
                $"Character medical payload version {payload.version} is not supported; expected {DungeonCharacterMedicalSaveData.CurrentVersion}.");
            return;
        }
        if (payload.orderSequence < 0)
        {
            report.AddError(
                "Character medical order sequence cannot be negative.");
        }
        if (payload.orders == null)
        {
            report.AddError(
                "Character medical payload is missing its order list.");
            return;
        }
        if (payload.orders.Count > MaximumOrders)
        {
            report.AddError(
                $"Character medical payload exceeds {MaximumOrders} orders.");
        }

        HashSet<string> orderIds = new(StringComparer.Ordinal);
        HashSet<string> activePatients = new(StringComparer.Ordinal);
        int highestSequence = 0;
        foreach (CharacterMedicalOrder order in payload.orders)
        {
            string orderId = order?.orderId ?? string.Empty;
            if (order == null
                || !TryParseOrderId(orderId, out int sequence)
                || !orderIds.Add(orderId)
                || !IsCharacterId(order.patientId)
                || !Enum.IsDefined(
                    typeof(CharacterMedicalOrderState),
                    order.state)
                || !Enum.IsDefined(
                    typeof(CharacterMedicalSupplyKind),
                    order.treatmentSupply)
                || !Enum.IsDefined(
                    typeof(CharacterMedicalStatusCode),
                    order.statusCode)
                || order.statusCode == CharacterMedicalStatusCode.Unknown
                || order.statusParameters == null
                || order.rescuerId == null
                || order.treatmentFacilityId == null
                || order.treatmentItemId == null
                || order.treatmentMaterialDestinationId == null
                || order.treatmentSupplyOperationId == null
                || order.treatmentSupplyReasonCode == null
                || order.treatmentPhysicalItemId == null
                || order.treatmentSourceStackIds == null
                || order.treatmentPhysicalCommitId == null
                || order.treatmentSupplyOperationSequence <= 0
                || !Enum.IsDefined(
                    typeof(CharacterMedicalSupplyCommitPhase),
                    order.treatmentSupplyCommitPhase))
            {
                report.AddError(
                    $"Character medical payload contains invalid order '{orderId}'.");
                continue;
            }

            if (!HasValidStatusParameters(order))
            {
                report.AddError(
                    $"Medical order '{orderId}' contains invalid status parameters.");
            }

            highestSequence = Math.Max(highestSequence, sequence);
            if (order.IsActive && !activePatients.Add(order.patientId))
            {
                report.AddError(
                    $"Character '{order.patientId}' has more than one active medical order.");
            }
            if (order.rescuerId.Length > 0
                && (!IsCharacterId(order.rescuerId)
                    || string.Equals(
                        order.rescuerId,
                        order.patientId,
                        StringComparison.Ordinal)))
            {
                report.AddError(
                    $"Medical order '{orderId}' has an invalid rescuer ID.");
            }
            if (order.treatmentFacilityId.Length > 0
                && !IsBuildingInstanceId(order.treatmentFacilityId))
            {
                report.AddError(
                    $"Medical order '{orderId}' has an invalid treatment facility ID.");
            }

            if (!IsFiniteAtLeast(order.requiredStabilizationWork, 0f)
                || !IsFiniteInRange(
                    order.completedStabilizationWork,
                    0f,
                    order.requiredStabilizationWork)
                || !IsFiniteAtLeast(order.requiredTreatmentWork, 0f)
                || !IsFiniteInRange(
                    order.completedTreatmentWork,
                    0f,
                    order.requiredTreatmentWork)
                || !IsFiniteAtLeast(order.treatmentPotency, 0f)
                || !IsFiniteAtLeast(order.treatmentInfectionReduction, 0f)
                || !IsFiniteAtLeast(order.treatmentPainReduction, 0f))
            {
                report.AddError(
                    $"Medical order '{orderId}' contains invalid work or treatment values.");
            }

            if (order.carried
                && (!order.IsActive || order.rescuerId.Length == 0))
            {
                report.AddError(
                    $"Medical order '{orderId}' has invalid carried state.");
            }
            if (order.treatmentSupply == CharacterMedicalSupplyKind.None
                && (order.treatmentSupplyConsumed
                    || order.treatmentSupplyDeliveryRequested
                    || order.treatmentItemId.Length > 0))
            {
                report.AddError(
                    $"Medical order '{orderId}' has supply flags without a supply kind.");
            }
            if (order.treatmentItemId.Length > 0
                && (content == null
                    || !content.TryGetItem(order.treatmentItemId, out _)))
            {
                report.AddError(
                    $"Medical order '{orderId}' references unknown treatment item '{order.treatmentItemId}'.");
            }
            ValidatePhysicalSupplyCommit(order, itemDefinitions, report);
        }

        if (payload.orderSequence < highestSequence)
        {
            report.AddError(
                $"Character medical sequence {payload.orderSequence} is below saved order sequence {highestSequence}.");
        }
    }

    private static void ValidatePhysicalSupplyCommit(
        CharacterMedicalOrder order,
        IItemDefinitionCatalog itemDefinitions,
        DungeonGameRestoreReport report)
    {
        CharacterMedicalSupplyCommitPhase phase =
            (CharacterMedicalSupplyCommitPhase)order.treatmentSupplyCommitPhase;
        if (phase == CharacterMedicalSupplyCommitPhase.None)
        {
            if (order.treatmentSupplyOperationId.Length != 0
                || order.treatmentSupplyReasonCode.Length != 0
                || order.treatmentPhysicalItemId.Length != 0
                || order.treatmentPhysicalQuantity != 0
                || order.treatmentOutputX != 0
                || order.treatmentOutputY != 0
                || order.treatmentSourceStackIds.Count != 0
                || order.treatmentInputMassGrams != 0L
                || order.treatmentPhysicalCommitId.Length != 0)
            {
                report.AddError(
                    $"Medical order '{order.orderId}' has terminal supply provenance without a pending phase.");
            }
            return;
        }

        string expectedOperation =
            $"character-medical-supply:{order.orderId}:"
            + $"{order.treatmentSupplyOperationSequence:D8}";
        bool commonValid = order.treatmentSupply
                != CharacterMedicalSupplyKind.None
            && IsCanonicalRequired(order.treatmentMaterialDestinationId)
            && IsCanonicalRequired(order.treatmentPhysicalItemId)
            && order.treatmentPhysicalQuantity == 1
            && string.Equals(
                order.treatmentSupplyOperationId,
                expectedOperation,
                StringComparison.Ordinal)
            && string.Equals(
                order.treatmentSupplyReasonCode,
                "character-medical-treatment-supply",
                StringComparison.Ordinal)
            && itemDefinitions != null
            && itemDefinitions.TryGet(
                (ItemDefinitionId)order.treatmentPhysicalItemId,
                out _);
        if (!commonValid)
        {
            report.AddError(
                $"Medical order '{order.orderId}' has invalid physical supply intent.");
            return;
        }

        if (phase == CharacterMedicalSupplyCommitPhase.IntentRecorded)
        {
            if (order.treatmentSupplyConsumed
                || order.treatmentSourceStackIds.Count != 0
                || order.treatmentInputMassGrams != 0L
                || order.treatmentPhysicalCommitId.Length != 0)
            {
                report.AddError(
                    $"Medical order '{order.orderId}' supply intent contains a published outcome.");
            }
            return;
        }

        string[] sources = order.treatmentSourceStackIds.ToArray();
        const int sinkDispositionKindCode = 3;
        string expectedCommit =
            $"physical-batch-disposition:{sinkDispositionKindCode}:"
            + $"{order.treatmentSupplyOperationId}:"
            + $"{order.treatmentPhysicalQuantity}:"
            + order.treatmentInputMassGrams;
        if (!order.treatmentSupplyConsumed
            || sources.Length == 0
            || sources.Any(value => !IsCanonicalRequired(value))
            || sources.Distinct(StringComparer.Ordinal).Count() != sources.Length
            || !sources.SequenceEqual(
                sources.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            || order.treatmentInputMassGrams <= 0L
            || !string.Equals(
                order.treatmentPhysicalCommitId,
                expectedCommit,
                StringComparison.Ordinal))
        {
            report.AddError(
                $"Medical order '{order.orderId}' has invalid published supply provenance.");
        }
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static CharacterMedicalAggregateState CreateState(
        DungeonCharacterMedicalSaveData payload)
    {
        CharacterMedicalAggregateState restored = new()
        {
            OrderSequence = payload.orderSequence
        };
        foreach (CharacterMedicalOrder order in payload.orders)
        {
            restored.Orders.Add(
                CharacterMedicalOrderPersistence.Clone(order));
        }

        return restored;
    }

    private static bool TryParseOrderId(
        string orderId,
        out int sequence)
    {
        const string prefix = "medical:";
        sequence = 0;
        if (orderId == null
            || !orderId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = orderId.Substring(prefix.Length);
        return int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence)
            && sequence > 0
            && string.Equals(
                suffix,
                sequence.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool IsCharacterId(string value)
    {
        string raw = value ?? string.Empty;
        CharacterId id = (CharacterId)raw;
        return id.IsValid
            && string.Equals(id.Value, raw, StringComparison.Ordinal);
    }

    private static bool IsBuildingInstanceId(string value)
    {
        string raw = value ?? string.Empty;
        BuildingInstanceId id = (BuildingInstanceId)raw;
        return id.IsValid
            && string.Equals(id.Value, raw, StringComparison.Ordinal);
    }

    private static bool HasValidStatusParameters(
        CharacterMedicalOrder order)
    {
        if (order.statusParameters.Count == 0)
        {
            return order.statusCode != CharacterMedicalStatusCode.MedicineReady;
        }

        if (order.statusParameters.Count != 1
            || order.statusParameters[0] == null
            || order.statusParameters[0].Length > 128
            || !string.Equals(
                order.statusParameters[0],
                order.treatmentItemId,
                StringComparison.Ordinal))
        {
            return false;
        }

        return order.statusCode is CharacterMedicalStatusCode.MedicineReady
            or CharacterMedicalStatusCode.AwaitingMedicineDelivery;
    }

    private static bool IsFiniteAtLeast(float value, float minimum)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum;
    }

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum)
    {
        return IsFiniteAtLeast(value, minimum) && value <= maximum;
    }
}
