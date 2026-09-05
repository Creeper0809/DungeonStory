using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CertifiedSeedFacilityEligibilityDebugScenarios
{
    [MenuItem("DungeonStory/V27/Physical Mass/Verify Certified Seed Facility Eligibility")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_CERTIFIED_SEED_FACILITY_ELIGIBILITY_PASS");
    }

    public static void RunAll()
    {
        VerifyPlanExecutionReceipt();
        BuildingSO matching = Create(
            7_777,
            CertifiedSeedFacilityEligibility.WorkstationTag,
            includeBuffer: true,
            cycleCapacity: 4);
        BuildingSO legacyIdWrongCapability = Create(
            8_893,
            "workstation:qa:not-cultivar-breeding",
            includeBuffer: true,
            cycleCapacity: 4);
        BuildingSO missingBuffer = Create(
            8_894,
            CertifiedSeedFacilityEligibility.WorkstationTag,
            includeBuffer: false,
            cycleCapacity: 4);
        BuildingSO invalidBuffer = Create(
            8_895,
            CertifiedSeedFacilityEligibility.WorkstationTag,
            includeBuffer: true,
            cycleCapacity: 1);
        try
        {
            if (!CertifiedSeedFacilityEligibility.IsEligible(matching))
                throw new InvalidOperationException(
                    "Capability-authored cultivar facility was not eligible.");
            if (CertifiedSeedFacilityEligibility.IsEligible(
                    legacyIdWrongCapability))
            {
                throw new InvalidOperationException(
                    "Legacy numeric building ID incorrectly granted eligibility.");
            }
            if (CertifiedSeedFacilityEligibility.IsEligible(missingBuffer)
                || CertifiedSeedFacilityEligibility.IsEligible(invalidBuffer))
            {
                throw new InvalidOperationException(
                    "Certified-seed eligibility accepted an invalid physical output buffer.");
            }
            ProductionFacilityCapacitySubject subject = new(
                (BuildingInstanceId)"building:qa:certified-seed",
                Vector2Int.zero,
                "building-definition:qa:certified-seed",
                CertifiedSeedFacilityEligibility.WorkstationTag,
                4,
                new ProductionFacilityWorkstationLaneCapacityProfile(
                    ProductionWorkstationLanePolicy
                        .ManualWithDetachedBatchProcessors,
                    1,
                    0),
                ProductionFacilityProcessFluidCapacityProfile.Empty);
            if (!CertifiedSeedFacilityEligibility.IsEligible(subject))
            {
                throw new InvalidOperationException(
                    "Detached certified-seed capacity subject drifted from live eligibility.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(matching);
            UnityEngine.Object.DestroyImmediate(legacyIdWrongCapability);
            UnityEngine.Object.DestroyImmediate(missingBuffer);
            UnityEngine.Object.DestroyImmediate(invalidBuffer);
        }
    }

    private static void VerifyPlanExecutionReceipt()
    {
        CertifiedSeedOrderSaveData order = new()
        {
            actionId = "action:qa-certified-seed",
            orderId = "certified-seed-order:00000017",
            orderSequence = 17,
            destinationId = CertifiedSeedInputOwnerAuthority.BuildDestinationId(
                "building:qa",
                "crop:qa",
                17),
            facilityInstanceId = "building:qa:certified-seed",
            cropId = "crop:qa"
        };
        CertifiedSeedPlanExecutionReceipt receipt =
            CertifiedSeedPlanExecutionReceipt.CaptureIdentifiers(
                order.actionId,
                order.orderId,
                order.orderSequence,
                order.destinationId,
                order.facilityInstanceId,
                order.cropId);
        if (!string.Equals(receipt.ActionId, order.actionId,
                StringComparison.Ordinal)
            || !string.Equals(receipt.OrderId, order.orderId,
                StringComparison.Ordinal)
            || receipt.OrderSequence != order.orderSequence
            || !string.Equals(receipt.DestinationId, order.destinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.InputOperationId,
                CropPhysicalTransactionOutbox.FormatCertifiedOperationId(
                    order.orderId),
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.OutputOwnerId,
                order.orderId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.OutputBatchCommitId,
                CertifiedSeedRuntime.CertifiedOutputBatchCommitPrefix
                    + order.orderId,
                StringComparison.Ordinal)
            || receipt.SourceDigest.Length != 64)
        {
            throw new InvalidOperationException(
                "Certified-seed plan receipt lost execution correlation authority.");
        }

        CertifiedSeedOrderSaveData invalid = order.DeepClone();
        invalid.actionId = " action:qa-certified-seed";
        bool rejected = false;
        try
        {
            _ = CertifiedSeedPlanExecutionReceipt.CaptureIdentifiers(
                invalid.actionId,
                invalid.orderId,
                invalid.orderSequence,
                invalid.destinationId,
                invalid.facilityInstanceId,
                invalid.cropId);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        if (!rejected)
            throw new InvalidOperationException(
                "Certified-seed receipt accepted a non-canonical action ID.");
    }

    private static BuildingSO Create(
        int numericId,
        string workstationTag,
        bool includeBuffer,
        int cycleCapacity)
    {
        BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
        definition.id = numericId;
        definition.objectName = "QA certified-seed eligibility";
        definition.ConfigureAuthoredContentIdentity(
            "building:qa:certified-seed-eligibility:"
            + numericId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            1,
            "Capability-based certified-seed eligibility fixture.");
        BuildingAbilityCollection abilities = new();
         abilities.Add(new BuildingProductionWorkstationAbility
         {
             workstationTag = workstationTag,
             lanePolicy = ProductionWorkstationLanePolicy
                 .ManualWithDetachedBatchProcessors,
             manualWorkLaneCount = 1,
             automaticWorkLaneCount = 0
         });
        if (includeBuffer)
        {
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 2,
                physicalOutputBufferCycleCapacity = cycleCapacity,
                allowOverflowDump = false
            });
        }
        definition.ReplaceAbilities(abilities);
        return definition;
    }
}
