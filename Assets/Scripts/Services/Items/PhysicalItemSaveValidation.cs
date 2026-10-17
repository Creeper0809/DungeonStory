using System;
using System.Collections.Generic;

internal static class PhysicalItemSaveValidation
{
    internal const int MaxSavedStacks = 262_144;
    internal const int MaxSavedUniqueItems = 65_536;
    internal const int MaxPendingBatchDispositions = 16_384;
    internal const int MaxPhysicalItemRelocations = 16_384;
    internal const int MaxPhysicalItemTransformations = 16_384;
    internal const int MaxPendingExactOutputRoutes = 16_384;
    internal const int MaxPendingProductionCustodyDrains = 4_096;
    internal const int MaxPendingProductionInputDestinationDrains = 4_096;
    internal const int MaxPendingCapacityRoutingDrains = 16_384;

    internal static void Validate(
        DungeonPhysicalItemSaveData snapshot,
        DungeonGameRestoreReport report,
        IDungeonItemCatalogProvider catalog)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }
        if (snapshot == null)
        {
            report.AddError("Physical item payload is null.");
            return;
        }
        if (snapshot.version != DungeonPhysicalItemSaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported physical item payload version {snapshot.version}; expected {DungeonPhysicalItemSaveData.CurrentVersion}.");
        }
        if (snapshot.nextHaulOperationSequence <= 0)
        {
            report.AddError("Physical item haul-operation sequence must be positive.");
        }
        if (snapshot.lastConfirmedExactRouteCheckpointSequence < 0L
            || (snapshot.lastConfirmedExactRouteCheckpointSequence == 0L
                && !string.IsNullOrEmpty(
                    snapshot.lastConfirmedExactRouteCheckpointDigest))
            || (snapshot.lastConfirmedExactRouteCheckpointSequence > 0L
                && !PhysicalItemSaveValidationRules.IsLowerSha256(
                    snapshot.lastConfirmedExactRouteCheckpointDigest)))
        {
            report.AddError(
                "Physical item exact-route checkpoint sequence/digest is invalid.");
        }

        PhysicalItemStateSaveValidation.ValidateHaulingSettings(
            snapshot.haulingSettings,
            report);
        PhysicalItemJournalSaveValidation.ValidateEffectReceipts(
            snapshot,
            report);
        if (!PhysicalItemJournalSaveValidation.ValidateTransformJournal(
                snapshot.physicalItemTransformations,
                MaxPhysicalItemTransformations,
                report))
        {
            return;
        }
        if (snapshot.stacks == null)
        {
            report.AddError("Physical item payload has no stack list.");
            return;
        }
        if (snapshot.uniqueItems == null)
        {
            report.AddError("Physical item payload has no unique-item list.");
            return;
        }
        if (snapshot.reservationIntents == null)
        {
            report.AddError("Physical item payload has no reservation-intent list.");
            return;
        }
        if (snapshot.pendingBatchDispositions == null)
        {
            report.AddError("Physical item payload has no pending batch-disposition list.");
            return;
        }
        if (snapshot.physicalItemRelocations == null)
        {
            report.AddError(
                "Physical item payload has no relocation journal list.");
            return;
        }
        if (snapshot.pendingExactOutputRoutes == null)
        {
            report.AddError("Physical item payload has no pending exact-output-route list.");
            return;
        }
        if (snapshot.pendingProductionCustodyDrains == null)
        {
            report.AddError(
                "Physical item payload has no pending production custody-drain list.");
            return;
        }
        if (snapshot.pendingProductionInputDestinationDrains == null)
        {
            report.AddError(
                "Physical item payload has no pending production input-destination drain list.");
            return;
        }
        if (snapshot.pendingCapacityRoutingDrains == null)
        {
            report.AddError(
                "Physical item payload has no pending capacity-routing drain list.");
            return;
        }
        if (snapshot.stacks.Count > MaxSavedStacks)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxSavedStacks}-stack limit.");
        }
        if (snapshot.uniqueItems.Count > MaxSavedUniqueItems)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxSavedUniqueItems}-unique-item limit.");
        }
        if (snapshot.pendingBatchDispositions.Count > MaxPendingBatchDispositions)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingBatchDispositions}-pending-disposition limit.");
        }
        if (snapshot.physicalItemRelocations.Count
            > MaxPhysicalItemRelocations)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPhysicalItemRelocations}-relocation-journal limit.");
        }
        if (snapshot.pendingExactOutputRoutes.Count > MaxPendingExactOutputRoutes)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingExactOutputRoutes}-pending-route limit.");
        }
        if (snapshot.pendingProductionCustodyDrains.Count
            > MaxPendingProductionCustodyDrains)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingProductionCustodyDrains}-pending-production-custody-drain limit.");
        }
        if (snapshot.pendingProductionInputDestinationDrains.Count
            > MaxPendingProductionInputDestinationDrains)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingProductionInputDestinationDrains}-pending-production-input-destination-drain limit.");
        }
        if (snapshot.pendingCapacityRoutingDrains.Count
            > MaxPendingCapacityRoutingDrains)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingCapacityRoutingDrains}-pending-capacity-routing-drain limit.");
        }

        Dictionary<string, UniqueItemInstanceSaveData> uniqueById =
            PhysicalItemStateSaveValidation.ValidateUniqueItems(
                snapshot.uniqueItems,
                report);
        PhysicalItemStateSaveValidation.ValidateStacks(
            snapshot.stacks,
            uniqueById,
            report,
            catalog);
        PhysicalItemStateSaveValidation.ValidateReservationIntents(
            snapshot,
            report);
        PhysicalItemJournalSaveValidation.ValidatePendingBatchDispositions(
            snapshot.pendingBatchDispositions,
            report);
        PhysicalItemJournalSaveValidation.ValidatePhysicalItemRelocations(
            snapshot.physicalItemRelocations,
            report);
        PhysicalItemRoutingSaveValidation.ValidatePendingExactOutputRoutes(
            snapshot.pendingExactOutputRoutes,
            report);
        PhysicalItemRoutingSaveValidation.ValidatePendingProductionCustodyDrains(
            snapshot.pendingProductionCustodyDrains,
            report);
        PhysicalItemRoutingSaveValidation
            .ValidatePendingProductionInputDestinationDrains(
                snapshot.pendingProductionInputDestinationDrains,
                report);
        PhysicalItemCapacityRoutingSaveValidation
            .ValidatePendingCapacityRoutingDrains(
                snapshot,
                report);
        PhysicalItemJournalSaveValidation.ValidateEquipmentModuleAppraisalJoins(
            snapshot.uniqueItems,
            snapshot.pendingBatchDispositions,
            report);
        PhysicalItemJournalSaveValidation
            .ValidateQualityRejectedMarketCustodyJoins(
                snapshot.uniqueItems,
                snapshot.pendingBatchDispositions,
                report);
    }

    internal static void ValidateRecoveryDrop(
        WorldItemStackSaveData stack,
        string stackId,
        DungeonGameRestoreReport report)
    {
        PhysicalItemStateSaveValidation.ValidateRecoveryDrop(
            stack,
            stackId,
            report);
    }
}

#if UNITY_EDITOR
public static class PhysicalItemSaveValidationDiagnostics
{
    public static bool IsValidRecoveryDrop(
        WorldItemStackSaveData stack,
        string stackId,
        out string errors)
    {
        DungeonGameRestoreReport report = new();
        PhysicalItemSaveValidation.ValidateRecoveryDrop(stack, stackId, report);
        errors = string.Join(" | ", report.Errors);
        return report.Success;
    }
}
#endif
