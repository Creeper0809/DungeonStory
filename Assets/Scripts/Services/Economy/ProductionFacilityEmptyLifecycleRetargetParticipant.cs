using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Residual production-authority guard. Contributors owned by registered staged
/// retarget adapters are verified by those adapters; every other contributor
/// must remain empty and fingerprint-stable for the full transaction.
/// </summary>
public sealed class ProductionFacilityEmptyLifecycleRetargetParticipant :
    IProductionFacilityRetargetParticipant
{
    private const string CanonicalVersion = "residual-lifecycle-retarget@2";
    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly HashSet<string> adapterOwnedContributorIds;

    public ProductionFacilityEmptyLifecycleRetargetParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle)
        : this(
            lifecycle,
            Array.Empty<IProductionFacilityRetargetAuthorityAdapter>())
    {
    }

    public ProductionFacilityEmptyLifecycleRetargetParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IEnumerable<IProductionFacilityRetargetAuthorityAdapter> adapters)
    {
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        adapterOwnedContributorIds = (adapters
                ?? throw new ArgumentNullException(nameof(adapters)))
            .SelectMany(value => value?.OwnedLifecycleContributorIds
                ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
    }

    public string ParticipantId => "empty-lifecycle-guard";

    public bool TryPrepare(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        string operationId,
        out ProductionFacilityRetargetParticipantPlan plan,
        out string failureReason)
    {
        plan = null;
        if (!TryCaptureEmptySources(
                orderedRequests,
                out SourceSnapshot[] sources,
                out string fingerprint,
                out failureReason))
        {
            return false;
        }

        plan = ProductionFacilityRetargetParticipantPlan.Create(
            ParticipantId,
            fingerprint,
            new ParticipantState(sources));
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
            || state.IsCommitted
            || !TryVerifySources(state.Sources, out failureReason))
        {
            return false;
        }

        ProductionFacilityRetargetBinding[] bindings = (orderedBindings
                ?? Array.Empty<ProductionFacilityRetargetBinding>())
            .OrderBy(value => value?.SourceFacilityId.Value, StringComparer.Ordinal)
            .ToArray();
        if (bindings.Length != state.Sources.Count)
        {
            failureReason = "empty-lifecycle-retarget-binding-coverage-invalid";
            return false;
        }
        for (int index = 0; index < bindings.Length; index++)
        {
            if (bindings[index] == null
                || !bindings[index].SourceFacilityId.Equals(
                    state.Sources[index].FacilityId)
                || bindings[index].TargetFacility == null
                || bindings[index].TargetFacility.IsDestroyed)
            {
                failureReason = "empty-lifecycle-retarget-binding-invalid";
                return false;
            }
        }

        state.Bindings = bindings;
        state.IsCommitted = true;
        committedFingerprint = CaptureCommittedFingerprint(state);
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
            || !TryVerifySources(state.Sources, out failureReason))
        {
            return false;
        }

        state.IsCommitted = false;
        state.Bindings = Array.Empty<ProductionFacilityRetargetBinding>();
        rolledBackFingerprint = CapturePreparedFingerprint(state.Sources);
        failureReason = string.Empty;
        return true;
    }

    public bool TryCaptureCurrentFingerprint(
        ProductionFacilityRetargetParticipantPlan plan,
        out string currentFingerprint,
        out string failureReason)
    {
        currentFingerprint = string.Empty;
        if (!TryRequireState(plan, out ParticipantState state, out failureReason)
            || !TryVerifySources(state.Sources, out failureReason))
        {
            return false;
        }

        currentFingerprint = state.IsCommitted
            ? CaptureCommittedFingerprint(state)
            : CapturePreparedFingerprint(state.Sources);
        failureReason = string.Empty;
        return true;
    }

    private bool TryCaptureEmptySources(
        IReadOnlyList<ProductionFacilityRetargetRequest> requests,
        out SourceSnapshot[] sources,
        out string fingerprint,
        out string failureReason)
    {
        sources = Array.Empty<SourceSnapshot>();
        fingerprint = string.Empty;
        failureReason = string.Empty;
        if (requests == null || requests.Count == 0)
        {
            failureReason = "empty-lifecycle-retarget-source-missing";
            return false;
        }

        List<SourceSnapshot> captured = new(requests.Count);
        foreach (ProductionFacilityRetargetRequest request in requests
                     .OrderBy(value => value?.SourceFacilityId.Value,
                         StringComparer.Ordinal))
        {
            if (request == null || !request.SourceFacilityId.IsValid)
            {
                failureReason = "empty-lifecycle-retarget-source-invalid";
                return false;
            }
            ProductionOutputDestinationLifecycleSnapshot snapshot =
                lifecycle.Capture(request.SourceFacilityId);
            ProductionOutputDestinationLifecycleContribution[] residual =
                CaptureResidual(snapshot);
            if (snapshot == null
                || residual.Any(value => value.HasAuthority
                    || !value.IsEmpty))
            {
                failureReason = "empty-lifecycle-retarget-active-authority:"
                    + request.SourceFacilityId.Value + ":"
                    + (snapshot?.SemanticFingerprint ?? "missing");
                return false;
            }
            captured.Add(new SourceSnapshot(
                request.SourceFacilityId,
                CaptureResidualFingerprint(
                    request.SourceFacilityId,
                    residual)));
        }

        sources = captured.ToArray();
        fingerprint = CapturePreparedFingerprint(sources);
        return true;
    }

    private bool TryVerifySources(
        IReadOnlyList<SourceSnapshot> sources,
        out string failureReason)
    {
        foreach (SourceSnapshot source in sources)
        {
            ProductionOutputDestinationLifecycleSnapshot snapshot =
                lifecycle.Capture(source.FacilityId);
            ProductionOutputDestinationLifecycleContribution[] residual =
                CaptureResidual(snapshot);
            if (snapshot == null
                || residual.Any(value => value.HasAuthority
                    || !value.IsEmpty)
                || !string.Equals(
                    CaptureResidualFingerprint(source.FacilityId, residual),
                    source.SemanticFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "empty-lifecycle-retarget-source-drift:"
                    + source.FacilityId.Value;
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    private ProductionOutputDestinationLifecycleContribution[] CaptureResidual(
        ProductionOutputDestinationLifecycleSnapshot snapshot) => (snapshot?
            .Contributions
            ?? Array.Empty<ProductionOutputDestinationLifecycleContribution>())
        .Where(value => value != null
            && !adapterOwnedContributorIds.Contains(value.ContributorId))
        .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
        .ToArray();

    private static string CaptureResidualFingerprint(
        BuildingInstanceId facilityId,
        IReadOnlyList<ProductionOutputDestinationLifecycleContribution> residual)
    {
        StringBuilder canonical = new StringBuilder(CanonicalVersion)
            .Append("|residual|").Append(facilityId.Value).Append('|');
        foreach (ProductionOutputDestinationLifecycleContribution contribution in
                 residual ?? Array.Empty<ProductionOutputDestinationLifecycleContribution>())
        {
            canonical.Append(contribution.ContributorId).Append('|')
                .Append(contribution.DurableSemanticFingerprint).Append(';');
        }
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical.ToString());
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
                "empty-lifecycle-guard",
                StringComparison.Ordinal))
        {
            failureReason = "empty-lifecycle-retarget-plan-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static string CapturePreparedFingerprint(
        IReadOnlyList<SourceSnapshot> sources)
    {
        StringBuilder canonical = new StringBuilder(CanonicalVersion)
            .Append("|prepared|");
        foreach (SourceSnapshot source in sources)
        {
            canonical.Append(source.FacilityId.Value).Append('|')
                .Append(source.SemanticFingerprint).Append(';');
        }
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical.ToString());
    }

    private static string CaptureCommittedFingerprint(ParticipantState state)
    {
        StringBuilder canonical = new StringBuilder(CanonicalVersion)
            .Append("|committed|");
        foreach (SourceSnapshot source in state.Sources)
        {
            canonical.Append(source.FacilityId.Value).Append('|')
                .Append(source.SemanticFingerprint).Append(';');
        }
        foreach (ProductionFacilityRetargetBinding binding in state.Bindings)
        {
            canonical.Append(binding.SourceFacilityId.Value).Append("->")
                .Append(binding.TargetFacilityId.Value).Append(';');
        }
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical.ToString());
    }

    private readonly struct SourceSnapshot
    {
        public SourceSnapshot(
            BuildingInstanceId facilityId,
            string semanticFingerprint)
        {
            FacilityId = facilityId;
            SemanticFingerprint = semanticFingerprint;
        }

        public BuildingInstanceId FacilityId { get; }
        public string SemanticFingerprint { get; }
    }

    private sealed class ParticipantState
    {
        public ParticipantState(IReadOnlyList<SourceSnapshot> sources)
        {
            Sources = sources;
        }

        public IReadOnlyList<SourceSnapshot> Sources { get; }
        public IReadOnlyList<ProductionFacilityRetargetBinding> Bindings { get; set; } =
            Array.Empty<ProductionFacilityRetargetBinding>();
        public bool IsCommitted { get; set; }
    }
}
