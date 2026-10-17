using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

internal sealed class CropPlotFactPublication
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IDomainFactPublisher domainFacts;

    internal CropPlotFactPublication(DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore;
    }

    private bool snapshotsDirty
    {
        set => aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).SnapshotsDirty = value;
    }

    internal void ConstructDomainFacts(IDomainFactPublisher publisher) =>
        domainFacts = publisher ?? throw new ArgumentNullException(nameof(publisher));

    internal IDomainFactPublisher RequireDomainFacts() => domainFacts
        ?? throw new InvalidOperationException("Crop domain-fact publisher is not registered.");

    internal DomainFactDelivery DeliverCropCommandFact<TFact>(
        in DomainFactLease reservation, string publicationKey, TFact fact)
    {
        IDomainFactPublisher publisher = RequireDomainFacts();
        try
        {
            if (reservation.IsValid) return publisher.Publish(reservation, fact);
            if (publisher is not IDomainFactRecoveryPublisher recovery)
                throw new InvalidOperationException("Crop command facts require exact-key recovery.");
            var results = new DomainFactDelivery[1];
            return recovery.ReplayBatch(new[] { DomainFactReplay.Create(publicationKey, fact) },
                results, out string failure) ? results[0] : DomainFactDelivery.Reject(failure);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            // The authoritative crop command has already changed state. Retain
            // its frozen fact/key; an unknown observer ACK never redoes it.
            return DomainFactDelivery.Uncertain("crop-command-publication-unconfirmed:" + exception.Message);
        }
    }

    internal static bool ConfirmCropCommandPublication(DomainFactDelivery delivery,
        string publicationKey, string previousId, string previousFingerprint, out string failure)
    {
        bool known = (delivery.Phase is DomainFactDeliveryPhase.Delivered
                or DomainFactDeliveryPhase.AcceptedPendingDelivery)
            && delivery.OperationKey == publicationKey
            && !string.IsNullOrEmpty(delivery.PublicationId)
            && IsCropCommandFingerprint(delivery.Fingerprint)
            && (string.IsNullOrEmpty(previousId) || delivery.PublicationId == previousId)
            && (string.IsNullOrEmpty(previousFingerprint) || delivery.Fingerprint == previousFingerprint);
        failure = known ? string.Empty : "crop-command-unconfirmed:" + delivery.Detail;
        return known;
    }

    internal bool TryDeliverCropFact<TFact>(TFact fact,
        ref string publicationKey, ref string publicationId, ref string fingerprint,
        out string failureReason)
    {
        DomainFactLease reservation = default;
        try
        {
            if (string.IsNullOrEmpty(publicationKey))
            {
                if (!RequireDomainFacts().TryReserve(fact, out reservation, out failureReason))
                    return false;
                publicationKey = reservation.PublicationKey;
            }
            DomainFactDelivery delivery = DeliverCropCommandFact(reservation, publicationKey, fact);
            reservation = default;
            if (!ConfirmCropCommandPublication(delivery, publicationKey, publicationId, fingerprint, out failureReason))
                return false;
            publicationId = delivery.PublicationId;
            fingerprint = delivery.Fingerprint;
            if (delivery.Phase == DomainFactDeliveryPhase.Delivered)
                return true;
            failureReason = delivery.Detail;
            return false;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            if (reservation.IsValid) RequireDomainFacts().Cancel(reservation);
            failureReason = "crop-fact-publication-unconfirmed:" + exception.Message;
            return false;
        }
        finally { snapshotsDirty = true; }
    }

    internal static string RequirePlotDisplayName(BuildableObject building)
    {
        string name = building?.BuildingData?.objectName?.Trim()
            ?? string.Empty;
        if (name.Length == 0)
            throw new InvalidOperationException(
                "Crop terminal outcome requires an immutable plot display snapshot.");
        return name;
    }
}
