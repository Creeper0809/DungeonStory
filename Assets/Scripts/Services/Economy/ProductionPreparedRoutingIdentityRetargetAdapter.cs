using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Preserves prepared-output routing and its exact-route outbox when a detached
/// replacement retains the complete facility semantic identity. Position or
/// capacity changes require a future staged routing reauthor adapter; silently
/// carrying their old proofs forward is forbidden.
/// </summary>
public sealed class ProductionPreparedRoutingIdentityRetargetAdapter :
    IProductionFacilityRetargetAuthorityAdapter
{
    private const string CanonicalVersion =
        "prepared-routing-identity-retarget@1";
    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;

    public ProductionPreparedRoutingIdentityRetargetAdapter(
        IProductionOutputDestinationLifecycleQuery lifecycle)
    {
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public string AdapterId => "prepared-routing-identity";

    public IReadOnlyList<string> OwnedLifecycleContributorIds { get; } =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds
                .CapacityRoutingOutbox
        });

    public bool TryStage(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        string operationId,
        out ProductionFacilityRetargetAuthorityPlan plan,
        out string failureReason)
    {
        plan = null;
        List<SourceSnapshot> sources = new();
        try
        {
            foreach (ProductionFacilityRetargetRequest request in
                     (orderedRequests
                         ?? Array.Empty<ProductionFacilityRetargetRequest>())
                     .OrderBy(value => value?.SourceFacilityId.Value,
                         StringComparer.Ordinal))
            {
                if (request?.SourceFacility == null)
                    throw new InvalidOperationException("routing-source-invalid");
                ProductionOutputDestinationLifecycleContribution contribution =
                    CaptureContribution(request.SourceFacilityId);
                sources.Add(new SourceSnapshot(
                    request.SourceFacility,
                    contribution.HasAuthority,
                    contribution.DurableSemanticFingerprint));
            }
        }
        catch (Exception exception)
        {
            failureReason = "prepared-routing-retarget-stage-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (sources.Count == 0)
        {
            failureReason = "prepared-routing-retarget-source-missing";
            return false;
        }

        SourceSnapshot[] frozen = sources.ToArray();
        plan = ProductionFacilityRetargetAuthorityPlan.Create(
            AdapterId,
            CaptureFingerprint("staged", frozen, null),
            new AdapterState(frozen));
        failureReason = string.Empty;
        return true;
    }

    public bool TryPublish(
        ProductionFacilityRetargetAuthorityPlan plan,
        IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
        out string publishedFingerprint,
        out string failureReason)
    {
        publishedFingerprint = string.Empty;
        if (!TryRequireState(plan, out AdapterState state, out failureReason)
            || state.IsPublished
            || !TryVerifySources(state.Sources, out failureReason))
        {
            return false;
        }

        ProductionFacilityRetargetBinding[] bindings = (orderedBindings
                ?? Array.Empty<ProductionFacilityRetargetBinding>())
            .OrderBy(value => value?.SourceFacilityId.Value,
                StringComparer.Ordinal)
            .ToArray();
        if (bindings.Length != state.Sources.Count)
        {
            failureReason =
                "prepared-routing-retarget-binding-coverage-invalid";
            return false;
        }
        for (int index = 0; index < bindings.Length; index++)
        {
            ProductionFacilityRetargetBinding binding = bindings[index];
            SourceSnapshot source = state.Sources[index];
            if (binding == null
                || !binding.SourceFacilityId.Equals(source.Facility.InstanceId)
                || binding.TargetFacility == null
                || binding.TargetFacility.IsDestroyed)
            {
                failureReason = "prepared-routing-retarget-binding-invalid";
                return false;
            }
            if (source.HasAuthority
                && (!binding.TargetFacilityId.Equals(
                        source.Facility.InstanceId)
                    || !HasSameSemanticSubject(
                        source.Facility,
                        binding.TargetFacility)))
            {
                failureReason =
                    "prepared-routing-retarget-reauthor-adapter-required:"
                    + source.Facility.InstanceId.Value;
                return false;
            }
        }

        state.Bindings = bindings;
        state.IsPublished = true;
        publishedFingerprint = CaptureFingerprint(
            "published",
            state.Sources,
            bindings);
        failureReason = string.Empty;
        return true;
    }

    public bool TryRollback(
        ProductionFacilityRetargetAuthorityPlan plan,
        out string rolledBackFingerprint,
        out string failureReason)
    {
        rolledBackFingerprint = string.Empty;
        if (!TryRequireState(plan, out AdapterState state, out failureReason)
            || !TryVerifySources(state.Sources, out failureReason))
        {
            return false;
        }
        state.IsPublished = false;
        state.Bindings = Array.Empty<ProductionFacilityRetargetBinding>();
        rolledBackFingerprint = CaptureFingerprint(
            "staged",
            state.Sources,
            null);
        return true;
    }

    public bool TryCaptureCurrentFingerprint(
        ProductionFacilityRetargetAuthorityPlan plan,
        out string currentFingerprint,
        out string failureReason)
    {
        currentFingerprint = string.Empty;
        if (!TryRequireState(plan, out AdapterState state, out failureReason)
            || !TryVerifySources(state.Sources, out failureReason))
        {
            return false;
        }
        currentFingerprint = CaptureFingerprint(
            state.IsPublished ? "published" : "staged",
            state.Sources,
            state.IsPublished ? state.Bindings : null);
        return true;
    }

    private ProductionOutputDestinationLifecycleContribution CaptureContribution(
        BuildingInstanceId facilityId)
    {
        ProductionOutputDestinationLifecycleContribution[] matches = lifecycle
            .Capture(facilityId).Contributions
            .Where(value => value != null
                && string.Equals(
                    value.ContributorId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Prepared routing lifecycle contribution is missing or duplicated.");
        }
        return matches[0];
    }

    private bool TryVerifySources(
        IReadOnlyList<SourceSnapshot> sources,
        out string failureReason)
    {
        foreach (SourceSnapshot source in sources)
        {
            ProductionOutputDestinationLifecycleContribution current;
            try
            {
                current = CaptureContribution(source.Facility.InstanceId);
            }
            catch (Exception exception)
            {
                failureReason = "prepared-routing-retarget-capture-failed:"
                    + exception.GetType().Name;
                return false;
            }
            if (current.HasAuthority != source.HasAuthority
                || !string.Equals(
                    current.DurableSemanticFingerprint,
                    source.DurableFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "prepared-routing-retarget-source-drift:"
                    + source.Facility.InstanceId.Value;
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool HasSameSemanticSubject(
        ProductionFacilityHandle source,
        ProductionFacilityHandle target) =>
        source.Position == target.Position
        && string.Equals(
            source.DefinitionId,
            target.DefinitionId,
            StringComparison.Ordinal)
        && string.Equals(
            source.WorkstationTag,
            target.WorkstationTag,
            StringComparison.Ordinal)
        && source.OutputBufferCycleCapacity == target.OutputBufferCycleCapacity
        && string.Equals(
            source.WorkstationLaneProfile.SourceDigest,
            target.WorkstationLaneProfile.SourceDigest,
            StringComparison.Ordinal)
        && string.Equals(
            source.ProcessFluidProfile.SourceDigest,
            target.ProcessFluidProfile.SourceDigest,
            StringComparison.Ordinal);

    private static bool TryRequireState(
        ProductionFacilityRetargetAuthorityPlan plan,
        out AdapterState state,
        out string failureReason)
    {
        state = plan?.AdapterState as AdapterState;
        if (state == null
            || !string.Equals(
                plan.AdapterId,
                "prepared-routing-identity",
                StringComparison.Ordinal))
        {
            failureReason = "prepared-routing-retarget-plan-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static string CaptureFingerprint(
        string phase,
        IReadOnlyList<SourceSnapshot> sources,
        IReadOnlyList<ProductionFacilityRetargetBinding> bindings)
    {
        StringBuilder canonical = new StringBuilder(CanonicalVersion)
            .Append('|').Append(phase).Append('|');
        foreach (SourceSnapshot source in sources)
        {
            canonical.Append(source.Facility.InstanceId.Value).Append('|')
                .Append(source.HasAuthority ? '1' : '0').Append('|')
                .Append(source.DurableFingerprint).Append('|')
                .Append(source.Facility.Position.x).Append(',')
                .Append(source.Facility.Position.y).Append('|')
                .Append(source.Facility.DefinitionId).Append('|')
                .Append(source.Facility.WorkstationTag).Append('|')
                .Append(source.Facility.OutputBufferCycleCapacity).Append('|')
                .Append(source.Facility.WorkstationLaneProfile.SourceDigest)
                .Append('|')
                .Append(source.Facility.ProcessFluidProfile.SourceDigest)
                .Append(';');
        }
        foreach (ProductionFacilityRetargetBinding binding in bindings
                     ?? Array.Empty<ProductionFacilityRetargetBinding>())
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
            ProductionFacilityHandle facility,
            bool hasAuthority,
            string durableFingerprint)
        {
            Facility = facility;
            HasAuthority = hasAuthority;
            DurableFingerprint = durableFingerprint;
        }

        public ProductionFacilityHandle Facility { get; }
        public bool HasAuthority { get; }
        public string DurableFingerprint { get; }
    }

    private sealed class AdapterState
    {
        public AdapterState(IReadOnlyList<SourceSnapshot> sources) =>
            Sources = sources;

        public IReadOnlyList<SourceSnapshot> Sources { get; }
        public IReadOnlyList<ProductionFacilityRetargetBinding> Bindings { get; set; } =
            Array.Empty<ProductionFacilityRetargetBinding>();
        public bool IsPublished { get; set; }
    }
}
