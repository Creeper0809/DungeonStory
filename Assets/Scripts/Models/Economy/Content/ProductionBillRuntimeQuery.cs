using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;
using static ProductionBillIdentity;

internal sealed class ProductionBillRuntimeQuery
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionAssemblyBridge items;
    private readonly IProductionAssemblyBridge workshops;
    private readonly IProductionAssemblyBridge buildingWorld;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IProductionBillSnapshotProjector snapshotProjector;
    private IReadOnlyList<ProductionBillRecord> bills => stateStore.Bills;

    private int Version => stateStore.BillVersion;

    internal ProductionBillRuntimeQuery(
        ProductionBillOrderDependencies order,
        ProductionBillExecutionDependencies execution)
    {
        this.catalog = order.Catalog;
        this.items = order.Bridge;
        this.workshops = order.Bridge;
        this.buildingWorld = execution.Bridge;
        this.stateStore = order.StateStore;
        this.snapshotProjector = execution.SnapshotProjector;
    }

    public IReadOnlyList<ProductionBillSnapshot> GetBills(ProductionFacilityHandle facility)
    {
        if (facility == null)
        {
            return Array.Empty<ProductionBillSnapshot>();
        }

        return bills
            .Where(record => MatchesFacility(record, facility))
            .Select(record => ToSnapshot(record, facility))
            .ToArray();
    }

    public ProductionFacilityBillLifecycleSnapshot CaptureFacilityLifecycle(
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            throw new ArgumentException(
                "A valid facility ID is required.",
                nameof(facilityId));
        }

        ProductionBillRecord[] owned = bills
            .Where(record => record != null
                && record.buildingInstanceId.Equals(facilityId))
            .OrderBy(record => record.billId.Value, StringComparer.Ordinal)
            .ToArray();
        int activeWipCount = 0;
        int waitingCount = 0;
        int publicationPreparedCount = 0;
        int physicalCommitPendingCount = 0;
        StringBuilder canonical = new StringBuilder(128 + owned.Length * 192)
            .Append(GenericBillLifecycleSchema).Append('|')
            .Append(facilityId.Value).Append('|')
            .Append(Version).Append('|');
        StringBuilder durableCanonical = new StringBuilder(
            128 + owned.Length * 192)
            .Append(GenericBillLifecycleSchema).Append('|')
            .Append(facilityId.Value).Append('|');

        foreach (ProductionBillRecord record in owned)
        {
            ProductionPreparedOutputPhase phase = record.preparedOutput?.phase
                ?? ProductionPreparedOutputPhase.Unresolved;
            bool exactPublicationPrepared = record.resolvedOutputs.Any(output =>
                output != null
                && !string.IsNullOrEmpty(output.pendingCommitId)
                && !output.pendingCommitApplied);
            bool exactPhysicalCommitPending = record.resolvedOutputs.Any(output =>
                output != null
                && !string.IsNullOrEmpty(output.pendingCommitId)
                && output.pendingCommitApplied
                && output.pendingOutputPublication?.phase
                    == ProductionExactOutputPublicationPhase.Published);
            bool hasActiveWip = record.materialsConsumed
                || record.wipInputQuantity > 0
                || record.wipInputMassGrams > 0L
                || record.batchStage != ProductionBatchStage.None
                || record.completedWork > 0f
                || phase != ProductionPreparedOutputPhase.Unresolved;
            if (hasActiveWip)
                activeWipCount++;
            if (phase == ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace)
                waitingCount++;
            else if (phase == ProductionPreparedOutputPhase.PublicationPrepared
                     || exactPublicationPrepared)
                publicationPreparedCount++;
            else if (phase == ProductionPreparedOutputPhase
                         .PhysicalBatchCommittedPublicationPending
                     || exactPhysicalCommitPending)
                physicalCommitPendingCount++;

            AppendLifecycleRecord(canonical, record);
            AppendLifecycleRecord(durableCanonical, record);
        }

        string fingerprint;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(
                Encoding.UTF8.GetBytes(canonical.ToString()));
            StringBuilder hex = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
                hex.Append(value.ToString("x2"));
            fingerprint = hex.ToString();
        }
        return new ProductionFacilityBillLifecycleSnapshot(
            facilityId,
            owned.Length,
            activeWipCount,
            waitingCount,
            publicationPreparedCount,
            physicalCommitPendingCount,
            Version,
            fingerprint,
            ComputeLifecycleFingerprint(durableCanonical));
    }

    public IReadOnlyList<ProductionFacilityDestructiveDrainPreparedOutputOwner>
        CapturePreparedOutputOwners(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            throw new ArgumentException(
                "A valid facility ID is required.",
                nameof(facilityId));
        }

        return bills
            .Where(record => record != null
                && record.buildingInstanceId.Equals(facilityId))
            .OrderBy(record => record.billId.Value, StringComparer.Ordinal)
            .Select(record =>
            {
                ProductionPreparedOutputBatchSaveData batch =
                    record.preparedOutput
                    ?? ProductionPreparedOutputBatchSaveData.Unresolved();
                if (batch.phase != ProductionPreparedOutputPhase.Unresolved)
                {
                    ProductionPreparedOutputContract.ValidateForBill(
                        batch,
                        record.billId,
                        record.recipeId,
                        record.cycleSequence,
                        record.outputDestinationId);
                }

                return new
                    ProductionFacilityDestructiveDrainPreparedOutputOwner(
                        record.billId,
                        record.buildingInstanceId,
                        record.recipeId,
                        record.cycleSequence,
                        record.outputDestinationId,
                        batch.phase,
                        batch.batchCommitId,
                        batch.outcomeFingerprint);
            })
            .ToArray();
    }

    private static void AppendLifecycleRecord(
        StringBuilder canonical,
        ProductionBillRecord record)
    {
        ProductionBillSaveData saved = ProductionBillStateCodec.ToSaveData(record);
        string payload = JsonUtility.ToJson(saved);
        canonical.Append(Encoding.UTF8.GetByteCount(payload)).Append(':')
            .Append(payload).Append(';');
    }

    private static string ComputeLifecycleFingerprint(StringBuilder canonical)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical?.ToString() ?? string.Empty));
        StringBuilder hex = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2"));
        return hex.ToString();
    }

    internal static bool HasLegacyOutputAuthority(
        ProductionBillRecord record) => record == null
        || record.outputOutcomeResolved
        || record.resolvedOutputs.Count != 0
        || record.outputReservations.Count != 0;

    internal static DomainFailure PreparedOutputAuthorityConflict(
        ProductionBillRecord record) => new(
        FailureCode.ProductionOutputUnavailable,
        record == null ? string.Empty : record.billId.Value,
        "prepared-output-legacy-authority-conflict");

    internal ProductionFacilityHandle ResolveFacility(ProductionBillRecord record)
    {
        return buildingWorld.Facilities.FirstOrDefault(building =>
            MatchesFacility(record, building));
    }


    internal ProductionBillSnapshot ToSnapshot(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        return snapshotProjector.Project(record, facility, bills);
    }


    internal ProductionRecipeSO ResolveRecipe(ProductionBillRecord record)
    {
        return record != null
            && catalog.TryGetRecipe(record.recipeId, out ProductionRecipeSO recipe)
                ? recipe
                : null;
    }

    internal bool UsesPreparedOutput(ProductionBillRecord record) =>
        UsesPreparedOutput(ResolveRecipe(record));

    internal bool UsesPreparedOutput(ProductionRecipeSO recipe) =>
        ProductionPreparedOutputCapabilitySelection
            .UsesPreparedOutputMaterializer(recipe, items);

    internal ProductionBillRecord Find(ProductionBillId billId)
    {
        return !billId.IsValid
            ? null
            : bills.FirstOrDefault(record =>
                record.billId.Equals(billId));
    }

    internal static bool MatchesFacility(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        return record != null
            && facility != null
            && !facility.IsDestroyed
            && record.buildingInstanceId.Equals(
                facility.InstanceId);
    }

    internal bool MatchesRecipeWorkstation(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe)
    {
        return workshops.MatchesWorkstation(facility, recipe);
    }
}
