using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Infrastructure;
using UnityEngine;

/// <summary>
/// Owns exact route-cargo publication and its pending physical/fact transaction
/// until release or rollback. Persistent route state stays in the shared domain.
/// </summary>
internal sealed class FactionRouteCargoRuntime
{
    private const string CargoSourceOwnerDomain = "faction.route-cargo";
    private const string CargoReleaseReason =
        "faction-route-cargo-delivery";
    private const string CargoOutcomeRollbackReason =
        "faction-route-cargo-outcome-rollback";
    private readonly FactionDomainRuntime domain;
    private readonly IReadOnlyList<FactionDefinitionSnapshot> definitions;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IPhysicalItemExactSourcePublicationService exactSources;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IDomainFactPublisher domainFacts;
    private readonly IDomainFactRecoveryPublisher factRecovery;
    private readonly Dictionary<string, PendingFactionCargoPublication>
        pendingCargo = new(StringComparer.Ordinal);

    private sealed class PendingFactionCargoPublication
    {
        public PhysicalItemExactSourcePublicationPlan Plan;
        public PhysicalItemExactSourcePublicationTransaction Transaction;
        public FactionRouteCargoDeliveryReceipt ExpectedReceipt;
        public FactionRouteCargoDeliveryReceipt BeforeReceipt;
        public DungeonPhysicalItemSaveData PhysicalItemsBefore;
        public DomainFactLease FactLease;
        public FactionCargoDeliveredFact Fact;
        public string PublicationKey;
        public bool PublicationUncertain;
        public bool OutcomeCommitted;
    }

    // The adapter preserves production dependency validation and controlled
    // fixture ports. Do not eagerly use optional ports while constructing.
    internal FactionRouteCargoRuntime(
        FactionDomainRuntime domain,
        IReadOnlyList<FactionDefinitionSnapshot> definitions,
        IWorldDropZoneQuery dropZones,
        IPhysicalItemExactSourcePublicationService exactSources,
        IWorldItemStackRuntime itemRuntime,
        IDomainFactPublisher domainFacts,
        IDomainFactRecoveryPublisher factRecovery)
    {
        this.domain = domain;
        this.definitions = definitions;
        this.dropZones = dropZones;
        this.exactSources = exactSources;
        this.itemRuntime = itemRuntime;
        this.domainFacts = domainFacts;
        this.factRecovery = factRecovery;
    }

    private IReadOnlyList<FactionRouteState> routes => domain.Routes;
    private int currentDay => domain.CurrentDay;

    internal void EnsureCaptureSafe(string boundary)
    {
        string[] publishingRouteIds = routes
            .Where(value => value?.cargoDelivery?.state
                == FactionRouteCargoDeliveryState.Publishing)
            .Select(value => value.routeId ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (pendingCargo.Count > 0 || publishingRouteIds.Length > 0)
        {
            throw new InvalidOperationException(
                "Faction exact cargo publication is pending during "
                + (boundary ?? "an unknown boundary")
                + ": transient="
                + pendingCargo.Count
                + ", routes="
                + string.Join(",", publishingRouteIds)
                + ".");
        }
    }

    internal bool TryDeliverCargoCore(FactionRouteState route)
    {
        if (route?.cargoDelivery?.state
            == FactionRouteCargoDeliveryState.Delivered)
        {
            return true;
        }
        if (route?.cargoDelivery?.state
            == FactionRouteCargoDeliveryState.Publishing)
        {
            if (!pendingCargo.TryGetValue(
                    route.routeId,
                    out PendingFactionCargoPublication pending))
            {
                throw new InvalidOperationException(
                    $"Faction route '{route.routeId}' lost its exact cargo publication transaction.");
            }
            return TryCommitCargo(route, pending);
        }
        if (route?.cargoDelivery?.state
                != FactionRouteCargoDeliveryState.Ready
            || !dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            return false;
        }

        PhysicalItemExactSourcePublicationPlan plan =
            CreateCargoPublicationPlan(route, dropoff);
        DungeonPhysicalItemSaveData physicalItemsBefore;
        try
        {
            physicalItemsBefore = itemRuntime.Capture();
        }
        catch
        {
            throw;
        }
        PhysicalItemExactSourcePublicationTransaction transaction;
        try
        {
            if (!exactSources.TryPrepare(
                    plan,
                    out transaction,
                    out _))
            {
                return false;
            }
        }
        catch
        {
            throw;
        }

        FactionRouteCargoDeliveryReceipt expectedReceipt =
            ProjectCargoReceipt(plan, transaction);
        FactionCargoDeliveredFact fact;
        DomainFactLease prepared = default;
        bool reserved;
        try
        {
            fact = new FactionCargoDeliveredFact(route, DisplayName(route.factionId), Math.Max(1, currentDay), expectedReceipt);
            reserved = domainFacts.TryReserve(fact, out prepared, out _);
            reserved = reserved && prepared.IsValid && !string.IsNullOrWhiteSpace(prepared.PublicationKey);
        }
        catch
        {
            domainFacts.Cancel(prepared);
            if (!exactSources.TryRollback(transaction, CargoOutcomeRollbackReason, out string rollbackFailure))
                throw new InvalidOperationException("Faction cargo reservation exception could not roll back: " + rollbackFailure);
            itemRuntime.Restore(physicalItemsBefore);
            throw;
        }
        if (!reserved)
        {
            domainFacts.Cancel(prepared);
            if (!exactSources.TryRollback(
                    transaction,
                    CargoOutcomeRollbackReason,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Faction cargo outcome reservation failed and its exact-source transaction could not roll back: "
                    + rollbackFailure);
            }
            itemRuntime.Restore(physicalItemsBefore);
            return false;
        }

        PendingFactionCargoPublication created = new()
        {
            Plan = plan,
            Transaction = transaction,
            ExpectedReceipt = expectedReceipt,
            BeforeReceipt = route.cargoDelivery.Clone(),
            PhysicalItemsBefore = physicalItemsBefore,
            FactLease = prepared,
            Fact = fact,
            PublicationKey = prepared.PublicationKey
        };
        try
        {
            pendingCargo.Add(route.routeId, created);
            route.cargoDelivery.state =
                FactionRouteCargoDeliveryState.Publishing;
        }
        catch
        {
            domainFacts.Cancel(prepared);
            if (!exactSources.TryRollback(
                    transaction,
                    CargoOutcomeRollbackReason,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Faction cargo prepare failed and its physical candidate could not roll back: "
                    + rollbackFailure);
            }
            itemRuntime.Restore(physicalItemsBefore);
            route.cargoDelivery = created.BeforeReceipt.Clone();
            pendingCargo.Remove(route.routeId);
            throw;
        }
        return TryCommitCargo(route, created);
    }

    private bool TryCommitCargo(
        FactionRouteState route,
        PendingFactionCargoPublication pending)
    {
        if (pending?.ExpectedReceipt == null)
        {
            throw new InvalidOperationException(
                $"Faction route '{route?.routeId}' has no prebuilt durable cargo receipt.");
        }
        bool rollbackAttempted = false;
        bool publicationAttempted = false;
        try
        {
            if (!pending.OutcomeCommitted)
            {
                domain.MarkCargoDelivered(route, pending.ExpectedReceipt);
                DomainFactDelivery delivered;
                if (pending.PublicationUncertain)
                {
                    var deliveries = new DomainFactDelivery[1];
                    if (!factRecovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.PublicationKey, pending.Fact) }, deliveries, out _))
                    {
                        route.cargoDelivery = pending.BeforeReceipt.Clone();
                        route.cargoDelivery.state = FactionRouteCargoDeliveryState.Publishing;
                        return false;
                    }
                    delivered = deliveries[0];
                }
                else
                {
                    publicationAttempted = true;
                    delivered = domainFacts.Publish(pending.FactLease, pending.Fact);
                }
                if (!delivered.MayBeCommitted && !pending.PublicationUncertain)
                {
                    rollbackAttempted = true;
                    RollbackPreparedCargo(route, pending);
                    return false;
                }
                if (delivered.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
                    || delivered.OperationKey != pending.PublicationKey || string.IsNullOrWhiteSpace(delivered.PublicationId)
                    || delivered.Fingerprint?.Length != 64 || !delivered.Fingerprint.All(Uri.IsHexDigit))
                {
                    pending.PublicationUncertain = true;
                    domainFacts.Cancel(pending.FactLease);
                    route.cargoDelivery = pending.BeforeReceipt.Clone();
                    route.cargoDelivery.state = FactionRouteCargoDeliveryState.Publishing;
                    return false;
                }
                route.cargoPublicationKey = delivered.OperationKey;
                route.cargoPublicationId = delivered.PublicationId;
                route.cargoPublicationFingerprint = delivered.Fingerprint;
                route.cargoObservationDay = pending.Fact.AbsoluteDay;
                route.cargoFactionDisplayName = pending.Fact.FactionDisplayName;
                pending.OutcomeCommitted = true;
                route.cargoDelivery = pending.BeforeReceipt.Clone();
                route.cargoDelivery.state =
                    FactionRouteCargoDeliveryState.Publishing;
            }

            if (!exactSources.TryCommitReleased(
                    pending.Transaction,
                    FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
                    CargoReleaseReason,
                    out PhysicalItemExactSourcePublicationReceipt physicalReceipt,
                    out _))
            {
                return false;
            }
            if (!CargoReceiptMatches(pending.ExpectedReceipt, physicalReceipt))
            {
                throw new InvalidOperationException(
                    $"Faction route '{route.routeId}' exact cargo receipt drifted after publication.");
            }

            domain.MarkCargoDelivered(route, pending.ExpectedReceipt);
            pendingCargo.Remove(route.routeId);
            return true;
        }
        catch
        {
            // A transport exception cannot prove that observers did not commit.
            // Keep the same physical candidate and recover its frozen fact.
            if (publicationAttempted && !pending.OutcomeCommitted && !rollbackAttempted)
            {
                pending.PublicationUncertain = true;
                domainFacts.Cancel(pending.FactLease);
            }
            if (!pending.OutcomeCommitted && !pending.PublicationUncertain && !rollbackAttempted)
            {
                domainFacts.Cancel(pending.FactLease);
                RollbackPreparedCargo(route, pending);
            }
            else if (route?.cargoDelivery != null)
            {
                route.cargoDelivery = pending.BeforeReceipt.Clone();
                route.cargoDelivery.state =
                    FactionRouteCargoDeliveryState.Publishing;
            }
            throw;
        }
    }

    private static PhysicalItemExactSourcePublicationPlan
        CreateCargoPublicationPlan(
            FactionRouteState route,
            Vector2Int dropoff)
    {
        if (route == null
            || route.cargo == null
            || route.cargo.Count == 0)
        {
            throw new InvalidOperationException(
                "Faction cargo publication requires a frozen non-empty vector.");
        }

        FacilityBufferPlannedOutputSlice[] outputs = route.cargo
            .Select((line, index) =>
            {
                if (line == null
                    || string.IsNullOrWhiteSpace(line.itemId)
                    || line.amount <= 0)
                {
                    throw new InvalidOperationException(
                        "Faction cargo contains an invalid frozen line.");
                }
                ItemDefinitionId itemId = (ItemDefinitionId)line.itemId;
                return new FacilityBufferPlannedOutputSlice(
                    $"cargo:{index:D4}:{line.itemId}",
                    PhysicalItemMassSubject.ForDefinition(itemId),
                    ResolveCargoDeliveryQuantity(
                        line.amount,
                        route.strength));
            })
            .ToArray();
        return new PhysicalItemExactSourcePublicationPlan(
            CargoSourceOwnerDomain,
            route.routeId,
            dropoff,
            outputs);
    }

    private static int ResolveCargoDeliveryQuantity(int amount, int strength)
    {
        long scaled = checked((long)amount * strength);
        long whole = scaled / 100L;
        long remainder = scaled % 100L;
        if (remainder > 50L
            || (remainder == 50L && (whole & 1L) != 0L))
        {
            whole = checked(whole + 1L);
        }
        return Math.Max(1, checked((int)whole));
    }

    private static FactionRouteCargoDeliveryReceipt ProjectCargoReceipt(
        PhysicalItemExactSourcePublicationPlan plan,
        PhysicalItemExactSourcePublicationTransaction transaction)
    {
        List<ProductionDomainPublishedStackSaveData> stacks = transaction
            .PreparedStacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => new ProductionDomainPublishedStackSaveData
            {
                outputLineId = value.OutputLineId,
                itemId = value.ItemDefinitionId.Value,
                itemInstanceId = value.ItemInstanceId ?? string.Empty,
                stackId = value.StackId,
                quantity = value.Quantity,
                massGrams = value.MassGrams
            })
            .ToList();
        long totalMass = 0L;
        foreach (ProductionDomainPublishedStackSaveData stack in stacks)
        {
            totalMass = checked(totalMass + stack.massGrams);
        }
        return new FactionRouteCargoDeliveryReceipt
        {
            state = FactionRouteCargoDeliveryState.Delivered,
            batchCommitId = plan.BatchCommitId,
            destinationId = plan.DestinationId,
            outcomeFingerprint = plan.OutcomeFingerprint,
            deliveryX = plan.DropPosition.x,
            deliveryY = plan.DropPosition.y,
            totalMassGrams = totalMass,
            stacks = stacks
        };
    }

    private static bool CargoReceiptMatches(
        FactionRouteCargoDeliveryReceipt expected,
        PhysicalItemExactSourcePublicationReceipt actual)
    {
        if (expected == null
            || actual.IsRetained
            || !string.Equals(
                expected.batchCommitId,
                actual.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                expected.destinationId,
                actual.DestinationId,
                StringComparison.Ordinal)
            || expected.totalMassGrams != actual.TotalMassGrams
            || expected.stacks.Count != actual.Stacks.Count)
        {
            return false;
        }
        for (int index = 0; index < expected.stacks.Count; index++)
        {
            ProductionDomainPublishedStackSaveData left =
                expected.stacks[index];
            FacilityBufferPublishedOutputStackReceipt right =
                actual.Stacks[index];
            if (!string.Equals(
                    left.outputLineId,
                    right.OutputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    left.itemId,
                    right.ItemDefinitionId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    left.itemInstanceId,
                    right.ItemInstanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    left.stackId,
                    right.StackId,
                    StringComparison.Ordinal)
                || left.quantity != right.Quantity
                || left.massGrams != right.MassGrams)
            {
                return false;
            }
        }
        return true;
    }

    private void RollbackPreparedCargo(
        FactionRouteState route,
        PendingFactionCargoPublication pending)
    {
        domainFacts.Cancel(pending.FactLease);
        if (!exactSources.TryRollback(
                pending.Transaction,
                CargoOutcomeRollbackReason,
                out string rollbackFailure))
        {
            throw new InvalidOperationException(
                "Faction cargo outcome was rejected but its exact-source transaction could not roll back: "
                + rollbackFailure);
        }
        itemRuntime.Restore(
            pending?.PhysicalItemsBefore
            ?? throw new ArgumentNullException(nameof(pending)));
        route.cargoDelivery = (pending.BeforeReceipt
            ?? throw new InvalidOperationException(
                "Faction cargo rollback receipt is missing.")).Clone();
        pendingCargo.Remove(route.routeId);
    }

    private FactionDefinitionSnapshot FindDefinition(string factionId)
    {
        return definitions.FirstOrDefault(value =>
            string.Equals(value.StableId, factionId, StringComparison.Ordinal));
    }

    private string DisplayName(string factionId)
    {
        return FindDefinition(factionId)?.DisplayName ?? factionId;
    }
}
