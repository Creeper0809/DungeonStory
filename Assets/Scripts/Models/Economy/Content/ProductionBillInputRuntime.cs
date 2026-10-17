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

internal sealed class ProductionBillInputRuntime
{
    private readonly IProductionAssemblyBridge inputLogistics;
    private readonly IProductionInputDestinationClaimRuntime inputDestinationClaims;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly ProductionBillRuntimeQuery billQueries;
    private IReadOnlyList<ProductionBillRecord> bills => stateStore.Bills;

    internal ProductionBillInputRuntime(
        ProductionBillOrderDependencies order,
        ProductionBillExecutionDependencies execution,
        ProductionBillRuntimeQuery billQueries)
    {
        this.inputLogistics = order.Bridge;
        this.inputDestinationClaims = order.InputDestinationClaims;
        this.outputPlanning = execution.OutputPlanning;
        this.stateStore = order.StateStore;
        this.billQueries = billQueries;
    }

    internal bool EnsureOutputReservation(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        string destinationId = string.IsNullOrWhiteSpace(
            record?.outputDestinationId)
                ? ResolveOutputDestinationId(facility)
                : record.outputDestinationId;
        if (!outputPlanning.TryCreateReservation(
            recipe,
            facility,
            destinationId,
            GetOtherOutputReservations(record, destinationId),
            record?.outputReservations.Count > 0,
            out ProductionOutputReservationPlan plan,
            out failureReason))
        {
            return false;
        }

        if (record != null && record.outputReservations.Count == 0)
        {
            record.SetOutputDestination(plan.DestinationId);
            foreach (KeyValuePair<string, int> reservation in plan.Reservations)
            {
                record.SetOutputReservation(reservation.Key, reservation.Value);
            }
        }

        return true;
    }

    private Dictionary<string, int> GetOtherOutputReservations(
        ProductionBillRecord record,
        string destinationId)
    {
        return bills
            .Where(candidate => candidate != record
                && string.Equals(
                    candidate.outputDestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .SelectMany(candidate => candidate.outputReservations)
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.Value),
                StringComparer.Ordinal);
    }

    internal string ResolveOutputDestinationId(ProductionFacilityHandle facility)
    {
        return outputPlanning.ResolveDestinationId(facility);
    }

    internal void RevokeInputDestinationClaimOrThrow(
        ProductionBillRecord record,
        bool required,
        string operation)
    {
        string failureReason;
        bool revoked = required
            ? inputDestinationClaims.TryRevoke(record, out failureReason)
            : inputDestinationClaims.TryRevokeIfPresent(
                record,
                out failureReason);
        if (!revoked)
        {
            throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' input destination claim could not be {operation}: {failureReason}");
        }
    }

    internal void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (!RequiresPhysicalInputDestination(record, recipe, facility))
            return;

        long inputBufferMassGrams = inputLogistics
            .ResolveInputBufferMassCapacity(record, recipe, facility);
        if (!inputDestinationClaims.TryEnsureCapacity(
                record,
                inputBufferMassGrams,
                out string capacityFailure))
        {
            throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' input-buffer capacity could not be published: {capacityFailure}");
        }
        inputLogistics.RequestMissingInputs(record, recipe, facility);
    }

    internal long ResolveInputBufferMassCapacity(
        ProductionBillRecord record,
        IReadOnlyList<ProductionFacilityHandle> facilities)
    {
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' recipe is missing during input-buffer restore.");
        ProductionFacilityHandle facility = (facilities
                ?? Array.Empty<ProductionFacilityHandle>())
            .SingleOrDefault(value => ProductionBillRuntimeQuery.MatchesFacility(record, value))
            ?? throw new InvalidOperationException(
                $"Production bill '{record.billId.Value}' facility is missing during input-buffer restore.");
        return inputLogistics.ResolveInputBufferMassCapacity(
            record,
            recipe,
            facility);
    }

    internal bool RequiresPhysicalInputDestination(
        ProductionBillRecord record,
        IReadOnlyList<ProductionFacilityHandle> facilities)
    {
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' recipe is missing while resolving its input-destination requirement.");
        ProductionFacilityHandle facility = (facilities
                ?? Array.Empty<ProductionFacilityHandle>())
            .SingleOrDefault(value => ProductionBillRuntimeQuery.MatchesFacility(record, value))
            ?? throw new InvalidOperationException(
                $"Production bill '{record.billId.Value}' facility is missing while resolving its input-destination requirement.");
        return RequiresPhysicalInputDestination(record, recipe, facility);
    }

    internal bool RequiresPhysicalInputDestination(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility) =>
        inputLogistics.ToCycleInputMap(record, recipe, facility).Count > 0;

    internal void RecalculatePrefetch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionWorkerHandle worker)
    {
        inputLogistics.RecalculatePrefetch(record, recipe, worker);
    }

    internal bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        return inputLogistics.ShouldRunAnotherCycle(record, recipe);
    }

    internal bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out DomainFailure failure)
    {
        return inputLogistics.IsResearchUnlocked(recipe, out failure);
    }

    internal Dictionary<string, int> ToCycleInputMap(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        return inputLogistics.ToCycleInputMap(record, recipe, facility);
    }
}
