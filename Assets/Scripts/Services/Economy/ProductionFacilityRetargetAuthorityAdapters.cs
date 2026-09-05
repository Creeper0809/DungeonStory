using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Staged, authority-local extension point for production facility retargeting.
/// An adapter owns no parallel save state: its plan is transaction-scoped and
/// must be sufficient to prove publish or exact rollback against the existing
/// live authority.
/// </summary>
public interface IProductionFacilityRetargetAuthorityAdapter
{
    string AdapterId { get; }
    IReadOnlyList<string> OwnedLifecycleContributorIds { get; }

    bool TryStage(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        string operationId,
        out ProductionFacilityRetargetAuthorityPlan plan,
        out string failureReason);

    bool TryPublish(
        ProductionFacilityRetargetAuthorityPlan plan,
        IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
        out string publishedFingerprint,
        out string failureReason);

    bool TryRollback(
        ProductionFacilityRetargetAuthorityPlan plan,
        out string rolledBackFingerprint,
        out string failureReason);

    bool TryCaptureCurrentFingerprint(
        ProductionFacilityRetargetAuthorityPlan plan,
        out string currentFingerprint,
        out string failureReason);
}

public sealed class ProductionFacilityRetargetAuthorityPlan
{
    private ProductionFacilityRetargetAuthorityPlan(
        string adapterId,
        string stagedFingerprint,
        object adapterState)
    {
        if (!ProductionFacilityRetargetCanonical.IsToken(adapterId)
            || !ProductionFacilityRetargetCanonical.IsFingerprint(
                stagedFingerprint))
        {
            throw new ArgumentException(
                "A retarget authority plan requires canonical identity and fingerprint.");
        }

        AdapterId = adapterId;
        StagedFingerprint = stagedFingerprint;
        AdapterState = adapterState
            ?? throw new ArgumentNullException(nameof(adapterState));
    }

    public string AdapterId { get; }
    public string StagedFingerprint { get; }
    public object AdapterState { get; }

    public static ProductionFacilityRetargetAuthorityPlan Create(
        string adapterId,
        string stagedFingerprint,
        object adapterState) => new(
        adapterId,
        stagedFingerprint,
        adapterState);
}

/// <summary>
/// Bridges authority-local staged adapters into the existing sorted
/// all-or-none retarget participant transaction.
/// </summary>
public sealed class ProductionFacilityRetargetAuthorityParticipant :
    IProductionFacilityRetargetParticipant
{
    private const string CanonicalVersion = "retarget-authority-adapters@1";
    private readonly IReadOnlyList<IProductionFacilityRetargetAuthorityAdapter>
        adapters;

    public ProductionFacilityRetargetAuthorityParticipant(
        IEnumerable<IProductionFacilityRetargetAuthorityAdapter> adapters)
    {
        IProductionFacilityRetargetAuthorityAdapter[] ordered = (adapters
                ?? throw new ArgumentNullException(nameof(adapters)))
            .OrderBy(value => value?.AdapterId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null
                || !ProductionFacilityRetargetCanonical.IsToken(value.AdapterId))
            || ordered.Select(value => value.AdapterId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Production retarget authority adapters are missing or duplicated.");
        }
        this.adapters = Array.AsReadOnly(ordered);
    }

    public string ParticipantId => "active-authority-adapters";

    public bool TryPrepare(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        string operationId,
        out ProductionFacilityRetargetParticipantPlan plan,
        out string failureReason)
    {
        plan = null;
        List<AdapterEntry> entries = new(adapters.Count);
        foreach (IProductionFacilityRetargetAuthorityAdapter adapter in adapters)
        {
            if (!adapter.TryStage(
                    orderedRequests,
                    operationId,
                    out ProductionFacilityRetargetAuthorityPlan staged,
                    out failureReason))
            {
                return false;
            }
            entries.Add(new AdapterEntry(adapter, staged));
        }

        ParticipantState state = new(entries);
        plan = ProductionFacilityRetargetParticipantPlan.Create(
            ParticipantId,
            CaptureFingerprint("staged", entries),
            state);
        failureReason = string.Empty;
        return true;
    }

    public bool TryCommit(
        ProductionFacilityRetargetParticipantPlan plan,
        IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
        out string committedFingerprint,
        out string failureReason)
    {
        committedFingerprint = string.Empty;
        if (!TryRequireState(plan, out ParticipantState state, out failureReason)
            || state.IsCommitted)
        {
            return false;
        }

        foreach (AdapterEntry entry in state.Entries)
        {
            entry.PublishAttempted = true;
            if (!entry.Adapter.TryPublish(
                    entry.Plan,
                    orderedBindings,
                    out string publishedFingerprint,
                    out failureReason))
            {
                if (!TryRollbackAttempted(state, out string rollbackFailure))
                {
                    failureReason += "|adapter-rollback-failed:" + rollbackFailure;
                }
                return false;
            }
            entry.PublishedFingerprint = publishedFingerprint;
            entry.Published = true;
        }

        state.IsCommitted = true;
        committedFingerprint = CaptureFingerprint("published", state.Entries);
        failureReason = string.Empty;
        return true;
    }

    public bool TryRollback(
        ProductionFacilityRetargetParticipantPlan plan,
        out string rolledBackFingerprint,
        out string failureReason)
    {
        rolledBackFingerprint = string.Empty;
        if (!TryRequireState(plan, out ParticipantState state, out failureReason)
            || !TryRollbackAttempted(state, out failureReason))
        {
            return false;
        }

        state.IsCommitted = false;
        rolledBackFingerprint = CaptureFingerprint("staged", state.Entries);
        return true;
    }

    public bool TryCaptureCurrentFingerprint(
        ProductionFacilityRetargetParticipantPlan plan,
        out string currentFingerprint,
        out string failureReason)
    {
        currentFingerprint = string.Empty;
        if (!TryRequireState(plan, out ParticipantState state, out failureReason))
            return false;

        foreach (AdapterEntry entry in state.Entries)
        {
            if (!entry.Adapter.TryCaptureCurrentFingerprint(
                    entry.Plan,
                    out string capturedFingerprint,
                    out failureReason))
            {
                return false;
            }
            entry.CurrentFingerprint = capturedFingerprint;
        }
        currentFingerprint = CaptureFingerprint(
            state.IsCommitted ? "published" : "staged",
            state.Entries,
            useCurrent: true);
        return true;
    }

    private static bool TryRollbackAttempted(
        ParticipantState state,
        out string failureReason)
    {
        for (int index = state.Entries.Count - 1; index >= 0; index--)
        {
            AdapterEntry entry = state.Entries[index];
            if (!entry.PublishAttempted)
                continue;
            if (!entry.Adapter.TryRollback(
                    entry.Plan,
                    out string rolledBackFingerprint,
                    out failureReason))
            {
                return false;
            }
            entry.RolledBackFingerprint = rolledBackFingerprint;
            entry.PublishAttempted = false;
            entry.Published = false;
            entry.PublishedFingerprint = string.Empty;
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool TryRequireState(
        ProductionFacilityRetargetParticipantPlan plan,
        out ParticipantState state,
        out string failureReason)
    {
        state = plan?.ParticipantState as ParticipantState;
        if (state == null
            || !string.Equals(
                plan.ParticipantId,
                "active-authority-adapters",
                StringComparison.Ordinal))
        {
            failureReason = "retarget-authority-adapter-plan-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static string CaptureFingerprint(
        string phase,
        IReadOnlyList<AdapterEntry> entries,
        bool useCurrent = false)
    {
        StringBuilder canonical = new StringBuilder(CanonicalVersion)
            .Append('|').Append(phase).Append('|');
        foreach (AdapterEntry entry in entries)
        {
            string fingerprint = useCurrent
                ? entry.CurrentFingerprint
                : phase == "published"
                    ? entry.PublishedFingerprint
                    : entry.Plan.StagedFingerprint;
            canonical.Append(entry.Adapter.AdapterId).Append('|')
                .Append(fingerprint).Append(';');
        }
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical.ToString());
    }

    private sealed class ParticipantState
    {
        public ParticipantState(IReadOnlyList<AdapterEntry> entries) =>
            Entries = entries;

        public IReadOnlyList<AdapterEntry> Entries { get; }
        public bool IsCommitted { get; set; }
    }

    private sealed class AdapterEntry
    {
        public AdapterEntry(
            IProductionFacilityRetargetAuthorityAdapter adapter,
            ProductionFacilityRetargetAuthorityPlan plan)
        {
            Adapter = adapter;
            Plan = plan;
        }

        public IProductionFacilityRetargetAuthorityAdapter Adapter { get; }
        public ProductionFacilityRetargetAuthorityPlan Plan { get; }
        public bool PublishAttempted { get; set; }
        public bool Published { get; set; }
        public string PublishedFingerprint { get; set; } = string.Empty;
        public string RolledBackFingerprint { get; set; } = string.Empty;
        public string CurrentFingerprint { get; set; } = string.Empty;
    }
}
