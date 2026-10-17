using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotOperationIdentity;

internal static class CropPlotSaveContract
{


    internal static string BuildTreatmentDestinationId(
        BuildingInstanceId plotId,
        int operationSequence) =>
        CropPlotInputOwnerAuthority.BuildTreatmentDestinationId(
            plotId.Value,
            operationSequence);

    internal static string BuildSowDestinationId(
        BuildingInstanceId plotId,
        int operationSequence) =>
        CropPlotInputOwnerAuthority.BuildSowDestinationId(
            plotId.Value,
            operationSequence);

    internal static string CaptureWaterRefillRequestFingerprint(
        BuildingInstanceId plotId,
        int sequence,
        string destinationId,
        float requiredWork)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-water-refill-request@1");
        digest.Append(plotId.Value);
        digest.Append(sequence);
        digest.Append(FormatWaterRefillOperationId(plotId, sequence));
        digest.Append(destinationId);
        digest.Append(CropCycleInputRequirementAuthority.CleanWaterItemId);
        digest.Append(CropWaterRules.RefillQuantity);
        digest.AppendFloat(requiredWork);
        digest.AppendFloat(CropWaterRules.WaterCapacity);
        return digest.ComputeSha256();
    }

    internal static string CaptureHarvestOutcomeFingerprint(
        BuildingInstanceId plotId,
        CropHarvestOutputSaveData pending)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-harvest-frozen-output@3");
        digest.Append(pending.operationId);
        digest.Append(plotId.Value);
        digest.Append(pending.cropId);
        digest.Append(pending.indoor);
        digest.Append(pending.harvesterId);
        digest.Append(pending.outcomeId);
        digest.Append(pending.ecologyOutcomeFingerprint);
        digest.Append(pending.ecologyBeforeFingerprint);
        digest.Append(pending.ecologyAfterFingerprint);
        digest.Append(pending.goldenPrepared);
        digest.Append(pending.goldenTraitDefinitionId);
        digest.Append(pending.goldenOutcomeFingerprint);
        digest.Append((int)pending.goldenOutcome);
        digest.AppendFloat(pending.goldenPrimaryMultiplier);
        digest.AppendFloat(pending.goldenSecondaryMultiplier);
        digest.Append(pending.goldenRollHash.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append(pending.harvestItemId);
        digest.Append(pending.harvestQuantity);
        digest.Append(pending.seasonalDamageSourceEventInstanceId);
        digest.Append(pending.seasonalDamageSourceDefinitionId);
        digest.Append(pending.seasonalPrimaryBatchLossPercent);
        digest.Append(pending.primaryQuantityBeforeSeasonalLoss);
        digest.Append(pending.seasonalPrimaryLossQuantity);
        digest.Append(pending.seedItemId);
        digest.Append(pending.seedQuantity);
        digest.Append(SeedLotItemStateCodec.Encode(pending.returnedSeedLot)
            .ToCanonicalString());
        digest.Append(pending.maximumHarvestQuantity);
        digest.Append(pending.maximumSeedQuantity);
        digest.Append(pending.harvestCapability.fingerprint);
        digest.Append(pending.seedCapability.fingerprint);
        return digest.ComputeSha256();
    }

    internal static string CaptureFrozenSowInputVectorDigest(
        BuildingInstanceId plotId,
        string cropId,
        int operationSequence,
        IReadOnlyDictionary<string, int> requirements)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-sow-frozen-input-vector@1");
        digest.Append(plotId.Value);
        digest.Append(cropId);
        digest.Append(operationSequence);
        digest.Append(requirements.Count);
        foreach (KeyValuePair<string, int> value in requirements.OrderBy(
                     entry => entry.Key,
                     StringComparer.Ordinal))
        {
            digest.Append(value.Key);
            digest.Append(value.Value);
        }
        return digest.ComputeSha256();
    }





    internal static bool IsSerializedEmptySeedLot(SeedLotState value) =>
        value == null
        || string.IsNullOrEmpty(value.cropId)
        && string.IsNullOrEmpty(value.cultivarGenomeId)
        && value.generation == 0
        && value.pathogenLoad == 0f;

    internal static bool IsCanonicalToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    internal static bool CapabilityMatches(
        ProductionOutputCapabilitySaveData saved,
        ProductionOutputCapabilityDescriptor expected) =>
        saved != null
        && string.Equals(saved.outputLineId, expected.OutputLineId,
            StringComparison.Ordinal)
        && string.Equals(saved.itemId, expected.ItemId,
            StringComparison.Ordinal)
        && string.Equals(saved.capabilityId, expected.CapabilityId,
            StringComparison.Ordinal)
        && saved.capabilityVersion == expected.CapabilityVersion
        && string.Equals(saved.componentCodecId, expected.ComponentCodecId,
            StringComparison.Ordinal)
        && saved.componentCodecVersion == expected.ComponentCodecVersion
        && string.Equals(saved.fingerprint, expected.Fingerprint,
            StringComparison.Ordinal);

    internal static void ValidateSeasonalYieldDamage(
        CropSeasonalYieldDamageSaveData damage,
        string cropId,
        CropCycleExecutionReceiptSaveData cycleReceipt,
        ISet<string> handledSeasonalStarts)
    {
        if (damage == null)
            throw new InvalidOperationException(
                "Crop seasonal yield-damage provenance is missing.");
        if (damage.IsEmpty)
            return;
        bool parsedSource = TryParseSeasonalEventInstanceId(
            damage.sourceEventInstanceId,
            out int parsedDay,
            out string parsedDefinitionId);
        if (!parsedSource
            || !IsCanonicalToken(damage.sourceDefinitionId)
            || damage.sourceStartedAbsoluteDay < 1
            || parsedDay != damage.sourceStartedAbsoluteDay
            || !string.Equals(
                parsedDefinitionId,
                damage.sourceDefinitionId,
                StringComparison.Ordinal)
            || !IsCanonicalToken(damage.cropId)
            || !string.Equals(damage.cropId, cropId, StringComparison.Ordinal)
            || damage.sowOperationSequence < 0
            || damage.primaryBatchLossPercent is < 1
                or > CropHarvestOutputRules
                    .MaximumSeasonalPrimaryBatchLossPercent
            || cycleReceipt == null
            || cycleReceipt.IsEmpty
            || cycleReceipt.status != CropCycleExecutionReceiptStatus.Active
            || cycleReceipt.sowOperationSequence != damage.sowOperationSequence
            || !string.Equals(
                cycleReceipt.cropId,
                damage.cropId,
                StringComparison.Ordinal)
            || handledSeasonalStarts == null
            || !handledSeasonalStarts.Contains(damage.sourceEventInstanceId))
        {
            throw new InvalidOperationException(
                "Crop seasonal yield-damage provenance contradicts its live cycle or handled start.");
        }
    }

    internal static bool TryParseSeasonalEventInstanceId(
        string value,
        out int startedAbsoluteDay,
        out string definitionId)
    {
        startedAbsoluteDay = 0;
        definitionId = string.Empty;
        if (!IsCanonicalToken(value)
            || !value.StartsWith("event:", StringComparison.Ordinal))
            return false;
        int dayEnd = value.IndexOf(':', "event:".Length);
        int rollStart = value.LastIndexOf(':');
        if (dayEnd <= "event:".Length
            || rollStart <= dayEnd + 1
            || rollStart + 9 != value.Length
            || !int.TryParse(
                value.Substring("event:".Length, dayEnd - "event:".Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out startedAbsoluteDay)
            || startedAbsoluteDay < 1)
        {
            return false;
        }
        definitionId = value.Substring(
            dayEnd + 1,
            rollStart - dayEnd - 1);
        string roll = value.Substring(rollStart + 1);
        return IsCanonicalToken(definitionId)
            && roll.All(character => character is >= '0' and <= '9'
                or >= 'A' and <= 'F');
    }

    internal static void ValidateCropSelectionOutcome(
        CropPlotSaveData saved,
        BuildingInstanceId plotId)
    {
        CropSelectionCommandSaveData owner = saved.cropSelectionOutcome;
        if (owner.IsEmpty)
            return;
        _ = new CropSelectionChangedFact(plotId, owner);
        ValidateCropCommandPublicationKey(owner.publicationKey);
        string expectedOperationId = CropSelectionOperationPrefix
            + plotId.Value + ":"
            + owner.operationSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        bool ledgerExact = owner.phase == CropCommandOutcomePhase.LedgerCommitted
            ? IsCanonicalToken(owner.gameplayOutcomeId)
                && IsCropCommandFingerprint(
                    owner.gameplayOutcomeDigest)
            : string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest);
        if (!Enum.IsDefined(typeof(CropCommandOutcomePhase), owner.phase)
            || owner.phase == CropCommandOutcomePhase.None
            || owner.operationSequence
                != saved.nextCropSelectionOperationSequence
            || !string.Equals(
                owner.operationId,
                expectedOperationId,
                StringComparison.Ordinal)
            || !IsCanonicalToken(owner.cropAfter)
            || !string.Equals(owner.cropAfter, saved.cropId, StringComparison.Ordinal)
            || !float.IsFinite(owner.waterBefore)
            || !float.IsFinite(owner.waterAfter)
            || owner.waterBefore < 0f
            || owner.waterAfter != 0f
            || owner.completedAbsoluteDay < 0
            || !ledgerExact)
            throw new InvalidOperationException(
                "Crop selection command outcome is invalid.");
    }

    internal static void ValidateGoldenHarvestScheduleOutcome(
        CropPlotSaveData saved,
        BuildingInstanceId plotId)
    {
        CropGoldenHarvestScheduleSaveData owner =
            saved.goldenHarvestScheduleOutcome;
        if (owner.IsEmpty)
            return;
        _ = new GoldenHarvestScheduledFact(plotId, owner);
        ValidateCropCommandPublicationKey(owner.publicationKey);
        string expectedOperationId = GoldenHarvestScheduleOperationPrefix
            + plotId.Value + ":"
            + owner.operationSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        bool ledgerExact = owner.phase == CropCommandOutcomePhase.LedgerCommitted
            ? IsCanonicalToken(owner.gameplayOutcomeId)
                && IsCropCommandFingerprint(
                    owner.gameplayOutcomeDigest)
            : string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest);
        if (!Enum.IsDefined(typeof(CropCommandOutcomePhase), owner.phase)
            || owner.phase == CropCommandOutcomePhase.None
            || owner.operationSequence != owner.attemptBefore
            || owner.attemptAfter != checked(owner.attemptBefore + 1)
            || owner.attemptAfter != saved.goldenHarvestAttemptSequence
            || !string.Equals(
                owner.operationId,
                expectedOperationId,
                StringComparison.Ordinal)
            || !string.Equals(owner.cropId, saved.cropId, StringComparison.Ordinal)
            || !IsCanonicalToken(owner.harvesterId)
            || string.IsNullOrWhiteSpace(owner.harvesterDisplayName)
            || !string.IsNullOrEmpty(owner.harvesterBefore)
            || !string.Equals(
                owner.harvesterAfter,
                saved.goldenHarvestHarvesterId,
                StringComparison.Ordinal)
            || owner.completedAbsoluteDay < 0
            || !ledgerExact)
            throw new InvalidOperationException(
                "Golden harvest schedule outcome is invalid.");
    }

    internal static void ValidateCropTerminalPublicationFields(string key, string id, string fingerprint, bool acknowledged = false)
    {
        if (key == null || id == null || fingerprint == null)
            throw new InvalidOperationException("Crop terminal publication fields must be explicit.");
        if (key.Length == 0)
        {
            if (id.Length != 0 || fingerprint.Length != 0 || acknowledged)
                throw new InvalidOperationException("Crop terminal response is missing its publication key.");
            return;
        }
        ValidateCropCommandPublicationKey(key);
        bool hasIdentity = id.Length != 0;
        if (hasIdentity != (fingerprint.Length != 0)
            || hasIdentity && (!IsCanonicalToken(id) || !IsCropCommandFingerprint(fingerprint))
            || acknowledged && !hasIdentity)
            throw new InvalidOperationException("Crop terminal publication acknowledgement is incomplete.");
    }

    internal static void ValidateCropCommandPublicationKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512
            || !string.Equals(key, key.Trim(), StringComparison.Ordinal)
            || key.Any(char.IsControl))
            throw new InvalidOperationException("Crop command publication key is invalid.");
    }

    internal static bool IsCropCommandFingerprint(string value) =>
        value != null && value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static void ValidateCycleExecutionReceipt(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        CropCycleExecutionReceiptSaveData receipt =
            saved.cycleExecutionReceipt;
        CropPlanExecutionReceiptAuthority.Validate(
            receipt,
            requireCompleted: false);
        if (receipt.IsEmpty)
        {
            if (saved.materialsConsumed
                || saved.pendingHarvest.phase != CropHarvestOutputPhase.None
                || saved.failedOutcome != null
                    && !saved.failedOutcome.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Active crop cycle is missing its durable execution receipt.");
            }
            return;
        }

        if (!string.Equals(receipt.plotId, plotId.Value, StringComparison.Ordinal)
            || !string.Equals(receipt.cropId, crop.CropId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop execution receipt drifted from its plot or crop.");
        }

        CropFailedOutcomeSaveData failedOutcome = saved.failedOutcome
            ?? new CropFailedOutcomeSaveData();
        if (IsFailedCycleReceipt(receipt))
        {
            if (failedOutcome.IsEmpty
                || !ProductionOutputClearanceProfileObservation
                    .IsLowercaseSha256(failedOutcome.receiptDigest)
                || !string.Equals(
                    failedOutcome.receiptDigest,
                    receipt.sourceDigest,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(failedOutcome.plotDisplayName)
                || string.IsNullOrWhiteSpace(failedOutcome.cropDisplayName)
                || !string.Equals(
                    saved.plotDisplayName,
                    failedOutcome.plotDisplayName,
                    StringComparison.Ordinal)
                || failedOutcome.absoluteDay < 0
                || failedOutcome.ownerRevision != checked((long)saved.nextHarvestOperationSequence + 1L))
                throw new InvalidOperationException(
                    "Crop failure outcome snapshot contradicts its terminal receipt.");
            ValidateCropTerminalPublicationFields(failedOutcome.publicationKey,
                failedOutcome.publicationId, failedOutcome.publicationFingerprint);
            _ = new CropCycleTerminalFact(plotId, receipt, failedOutcome.plotDisplayName,
                failedOutcome.cropDisplayName, failedOutcome.absoluteDay, failedOutcome.ownerRevision);
        }
        else if (!failedOutcome.IsEmpty)
            throw new InvalidOperationException(
                "A non-failed crop cycle retained a failure outcome snapshot.");

        bool activeCycle = saved.materialsConsumed
            || saved.pendingHarvest.phase != CropHarvestOutputPhase.None;
        if (activeCycle
            && receipt.status != CropCycleExecutionReceiptStatus.Active
            && !(IsFailedCycleReceipt(receipt)
                && saved.phase == CropPlotPhase.Blocked
                && saved.pendingHarvest.phase == CropHarvestOutputPhase.None))
            throw new InvalidOperationException(
                "Active crop cycle must retain its active execution receipt: "
                + $"status={receipt.status}, phase={saved.phase}, "
                + $"materialsConsumed={saved.materialsConsumed}, "
                + $"pendingHarvest={saved.pendingHarvest.phase}.");
        if (!activeCycle
            && receipt.status == CropCycleExecutionReceiptStatus.Active)
            throw new InvalidOperationException(
                "Inactive crop plot cannot retain an incomplete execution receipt.");
        if (saved.pendingHarvest.phase != CropHarvestOutputPhase.None
            && !string.Equals(
                receipt.cropId,
                saved.pendingHarvest.cropId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop harvest owner drifted from its cycle execution receipt.");
        }
    }

    internal static bool IsFailedCycleReceipt(
        CropCycleExecutionReceiptSaveData receipt) => receipt != null
        && receipt.status is CropCycleExecutionReceiptStatus.FailedCropDeath
            or CropCycleExecutionReceiptStatus.FailedPlotDestroyed;

    internal static void ValidatePendingSow(
        CropPlotSaveData saved,
        CropDefinitionSO crop)
    {
        CropPhysicalCommitSaveData owner = saved.pendingSow;
        if (owner.phase == CropPhysicalCommitPhase.None)
        {
            if (owner.inputs == null
                || owner.inputs.Count != 0
                || owner.hasSeedLot
                || !string.IsNullOrEmpty(owner.operationId)
                || !string.IsNullOrEmpty(owner.commitId)
                || owner.inputQuantity != 0
                || owner.inputMassGrams != 0L
                || owner.terminalDisposition != CropWipTerminalDisposition.None
                || !string.IsNullOrEmpty(owner.terminalOperationId)
                || !string.IsNullOrEmpty(owner.terminalReasonCode)
                || owner.terminalLossQuantity != 0
                || owner.terminalLossMassGrams != 0L)
                throw new InvalidOperationException(
                    "Empty crop sow owner contains transaction provenance: "
                    + DescribeEmptySowOwnerConflict(owner));
            return;
        }
        if (owner.phase is not (CropPhysicalCommitPhase.InputCommitted
                or CropPhysicalCommitPhase.OutcomePublished
                or CropPhysicalCommitPhase.PlotDestroyedLossPending)
            || owner.operationSequence != saved.nextSowOperationSequence
            || owner.inputs == null
            || owner.inputs.Count == 0
            || !owner.hasSeedLot
            || owner.seedLot == null
            || !string.Equals(owner.cropId, crop.CropId, StringComparison.Ordinal)
            || !string.Equals(owner.seedItemId, crop.SeedItemId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(owner.ecologyBeforeFingerprint)
            || owner.phase == CropPhysicalCommitPhase.InputCommitted
                && (saved.materialsConsumed
                    || !HasEmptyCropTerminalDisposition(owner))
            || owner.phase == CropPhysicalCommitPhase.OutcomePublished
                && (!saved.materialsConsumed
                    || string.IsNullOrWhiteSpace(owner.ecologyAfterFingerprint)
                    || !HasEmptyCropTerminalDisposition(owner))
            || owner.phase == CropPhysicalCommitPhase.PlotDestroyedLossPending
                && (saved.materialsConsumed
                    || !string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                    || !CropPhysicalTransactionOutbox.ValidateDestroyedPlotLoss(
                        owner,
                        out _)))
            throw new InvalidOperationException(
                "Crop sow transaction owner contradicts plot state.");
    }

    internal static string DescribeEmptySowOwnerConflict(
        CropPhysicalCommitSaveData owner)
    {
        List<string> fields = new();
        if (owner.inputs == null) fields.Add("inputs=null");
        else if (owner.inputs.Count != 0)
            fields.Add($"inputs={owner.inputs.Count}");
        if (owner.hasSeedLot) fields.Add("hasSeedLot");
        if (!string.IsNullOrEmpty(owner.operationId)) fields.Add("operationId");
        if (!string.IsNullOrEmpty(owner.commitId)) fields.Add("commitId");
        if (owner.inputQuantity != 0) fields.Add($"quantity={owner.inputQuantity}");
        if (owner.inputMassGrams != 0L) fields.Add($"grams={owner.inputMassGrams}");
        if (owner.terminalDisposition != CropWipTerminalDisposition.None)
            fields.Add($"terminal={(int)owner.terminalDisposition}");
        if (!string.IsNullOrEmpty(owner.terminalOperationId))
            fields.Add("terminalOperationId");
        if (!string.IsNullOrEmpty(owner.terminalReasonCode))
            fields.Add("terminalReasonCode");
        if (owner.terminalLossQuantity != 0)
            fields.Add($"terminalQuantity={owner.terminalLossQuantity}");
        if (owner.terminalLossMassGrams != 0L)
            fields.Add($"terminalGrams={owner.terminalLossMassGrams}");
        return fields.Count == 0 ? "unknown" : string.Join(",", fields);
    }

    internal static bool HasEmptyCropTerminalDisposition(
        CropPhysicalCommitSaveData owner) =>
        owner != null
        && owner.terminalDisposition == CropWipTerminalDisposition.None
        && string.IsNullOrEmpty(owner.terminalOperationId)
        && string.IsNullOrEmpty(owner.terminalReasonCode)
        && owner.terminalLossQuantity == 0
        && owner.terminalLossMassGrams == 0L;

    internal static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        string label)
    {
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"Crop-plot {label} {value} is outside [{minimum}, {maximum}].");
        }
    }

    internal static void ValidateIrrigationPublication(CropPlotSaveData saved, BuildingInstanceId plotId)
    {
        CropIrrigationPublicationSaveData owner = saved.irrigationPublication
            ?? throw new InvalidOperationException("Crop irrigation publication owner is missing.");
        if (owner.IsEmpty) return;
        _ = new CropIrrigationSuppliedFact(owner);
        ValidateCropCommandPublicationKey(owner.publicationKey);
        bool acknowledged = !string.IsNullOrEmpty(owner.publicationId);
        if (owner.plotId != plotId.Value
            || saved.nextWaterRefillOperationSequence != owner.operationSequence + 1
            || owner.quantity != CropWaterRules.RefillQuantity
            || owner.waterAfter > saved.waterCapacity
            || Math.Abs(saved.currentWater - owner.waterAfter) > 0.0001f
            || (acknowledged
                ? !IsCanonicalToken(owner.publicationId) || !IsCropCommandFingerprint(owner.fingerprint)
                : !string.IsNullOrEmpty(owner.fingerprint))
            || saved.cycleExecutionReceipt == null || saved.cycleExecutionReceipt.IsEmpty
            || Math.Abs(saved.cycleExecutionReceipt.waterAfter - owner.waterAfter) > 0.0001f
            || saved.cycleExecutionReceipt.irrigationChildResultKeys == null
            || !saved.cycleExecutionReceipt.irrigationChildResultKeys.Contains(owner.publicationKey))
            throw new InvalidOperationException("Crop irrigation publication contradicts its applied owner.");
    }
}
