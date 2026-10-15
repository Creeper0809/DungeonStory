using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

public sealed class CropPlotRetirementObserver :
    IDurableDomainFactObserver<CropPlotRetiredFact>,
    IDurableDomainFactRecoveryObserver<CropPlotRetiredFact>,
    IDomainFactCommitGroup
{
    private const string PublicationPrefix = "crop-plot-retired:";
    private const string FingerprintVersion = "crop-plot-retired@1";

    private readonly IWorkCompletionIdentityDeliveryCommand completion;

    public CropPlotRetirementObserver(
        IWorkCompletionIdentityDeliveryCommand completion)
    {
        this.completion = completion
            ?? throw new ArgumentNullException(nameof(completion));
    }

    public bool TryReserve(
        in CropPlotRetiredFact proposed,
        out object reservation,
        out string failure)
    {
        reservation = null;
        if (!proposed.PlotId.IsValid)
        {
            failure = "crop-plot-retirement-fact-plot-invalid";
            return false;
        }

        Identity identity = CaptureIdentity(proposed.PlotId);
        reservation = new Reservation(proposed.PlotId, identity);
        failure = string.Empty;
        return true;
    }

    public bool TryPrepare(
        in CropPlotRetiredFact actual,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state
            || state.Cancelled
            || state.Prepared != null)
        {
            failure = "crop-plot-retirement-fact-reservation-invalid";
            return false;
        }
        if (!actual.PlotId.IsValid)
        {
            failure = "crop-plot-retirement-fact-plot-invalid";
            return false;
        }

        Identity actualIdentity = CaptureIdentity(actual.PlotId);
        if (!state.PlotId.Equals(actual.PlotId)
            || !state.Identity.Equals(actualIdentity))
        {
            failure = "crop-plot-retirement-fact-reserved-identity-changed";
            return false;
        }

        state.Prepared = new Publication(
            this,
            actual.PlotId,
            actualIdentity,
            isReplay: false);
        prepared = state.Prepared;
        failure = string.Empty;
        return true;
    }

    public bool TryPrepareReplay(
        string publicationKey,
        in CropPlotRetiredFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (!fact.PlotId.IsValid)
        {
            failure = "crop-plot-retirement-fact-recovery-plot-invalid";
            return false;
        }

        Identity identity = CaptureIdentity(fact.PlotId);
        if (!string.Equals(
                publicationKey,
                identity.PublicationKey,
                StringComparison.Ordinal))
        {
            failure = "crop-plot-retirement-fact-recovery-key-mismatch";
            return false;
        }

        prepared = new Publication(
            this,
            fact.PlotId,
            identity,
            isReplay: true);
        failure = string.Empty;
        return true;
    }

    public DomainFactDelivery Replay(in CropPlotRetiredFact fact) =>
        DomainFactDelivery.Reject(
            "crop-plot-retirement-fact-replay-requires-saved-key");

    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state)
            return;
        state.Cancelled = true;
        state.Prepared?.Cancel();
    }

    public bool Commit(
        IReadOnlyList<IDomainFactPreparedPublication> publications,
        DomainFactDelivery[] results,
        out string failure)
    {
        if (publications == null
            || publications.Count != 1
            || results?.Length != 1
            || publications[0] is not Publication value
            || !ReferenceEquals(value.CommitGroup, this)
            || value.Terminal)
        {
            failure = "crop-plot-retirement-fact-requires-one-active-publication";
            return false;
        }

        value.Terminal = true;
        try
        {
            bool retired = completion.RetireProducerStream(
                CropHarvestCompletionObservation.StreamPrefix
                + value.PlotId.Value);
            results[0] = retired
                ? new DomainFactDelivery(
                    DomainFactDeliveryPhase.Delivered,
                    value.Identity.PublicationKey,
                    value.Identity.PublicationId,
                    value.Identity.Fingerprint,
                    string.Empty)
                : Unconfirmed(
                    value.Identity.PublicationKey,
                    "crop-plot-retirement-fact-unconfirmed");
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            results[0] = Unconfirmed(
                value.Identity.PublicationKey,
                "crop-plot-retirement-fact-unconfirmed:"
                + exception.Message);
        }

        failure = string.Empty;
        return true;
    }

    private static Identity CaptureIdentity(BuildingInstanceId plotId)
    {
        string key = PublicationPrefix + plotId.Value;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(FingerprintVersion);
        digest.Append(plotId.Value);
        return new Identity(key, key, digest.ComputeSha256());
    }

    public static void ValidateSavedPublicationKey(BuildingInstanceId plotId, string key)
    {
        if (!plotId.IsValid || key == null || key.Length > 0 && key != CaptureIdentity(plotId).PublicationKey)
            throw new InvalidOperationException("Crop retirement saved publication key differs from its plot.");
    }

    private static DomainFactDelivery Unconfirmed(
        string publicationKey,
        string detail) => new(
        DomainFactDeliveryPhase.InDoubt,
        publicationKey,
        string.Empty,
        string.Empty,
        detail);

    private readonly struct Identity : IEquatable<Identity>
    {
        internal Identity(
            string publicationKey,
            string publicationId,
            string fingerprint)
        {
            PublicationKey = publicationKey;
            PublicationId = publicationId;
            Fingerprint = fingerprint;
        }

        internal string PublicationKey { get; }
        internal string PublicationId { get; }
        internal string Fingerprint { get; }

        public bool Equals(Identity other) =>
            string.Equals(
                PublicationKey,
                other.PublicationKey,
                StringComparison.Ordinal)
            && string.Equals(
                PublicationId,
                other.PublicationId,
                StringComparison.Ordinal)
            && string.Equals(
                Fingerprint,
                other.Fingerprint,
                StringComparison.Ordinal);
    }

    private sealed class Reservation : IDomainFactReservedIdentity
    {
        internal Reservation(BuildingInstanceId plotId, Identity identity)
        {
            PlotId = plotId;
            Identity = identity;
        }

        internal BuildingInstanceId PlotId { get; }
        internal Identity Identity { get; }
        internal bool Cancelled { get; set; }
        internal Publication Prepared { get; set; }
        public string PublicationKey => Identity.PublicationKey;
    }

    private sealed class Publication :
        IDomainFactPreparedPublication,
        IDomainFactPreparedIdentity
    {
        internal Publication(
            CropPlotRetirementObserver owner,
            BuildingInstanceId plotId,
            Identity identity,
            bool isReplay)
        {
            CommitGroup = owner;
            PlotId = plotId;
            Identity = identity;
            IsReplay = isReplay;
        }

        public IDomainFactCommitGroup CommitGroup { get; }
        public string OperationKey => Identity.PublicationKey;
        public long OwnerRevision => 0L;
        public bool IsReplay { get; }
        internal BuildingInstanceId PlotId { get; }
        internal Identity Identity { get; }
        internal bool Terminal { get; set; }
        public void Cancel() => Terminal = true;
    }
}
