using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// First active-authority adapter: relocation/evolution retain the stable
/// facility ID, so generic bills and embedded WIP remain owned by the same
/// aggregate identity without rewriting save DTOs. N-to-one identity changes
/// fail loudly until a staged aggregate reauthor command exists.
/// </summary>
public sealed class ProductionGenericBillIdentityRetargetAdapter :
    IProductionFacilityRetargetAuthorityAdapter
{
    private const string CanonicalVersion = "generic-bill-identity-retarget@1";
    private readonly IProductionBillCoreQuery bills;

    public ProductionGenericBillIdentityRetargetAdapter(
        IProductionBillCoreQuery bills)
    {
        this.bills = bills ?? throw new ArgumentNullException(nameof(bills));
    }

    public string AdapterId => "generic-bill-identity";

    public IReadOnlyList<string> OwnedLifecycleContributorIds { get; } =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills
        });

    public bool TryStage(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        string operationId,
        out ProductionFacilityRetargetAuthorityPlan plan,
        out string failureReason)
    {
        plan = null;
        SourceSnapshot[] sources;
        try
        {
            sources = (orderedRequests
                    ?? Array.Empty<ProductionFacilityRetargetRequest>())
                .OrderBy(value => value?.SourceFacilityId.Value,
                    StringComparer.Ordinal)
                .Select(value => value == null
                    ? throw new InvalidOperationException(
                        "generic-bill-retarget-source-invalid")
                    : Capture(value.SourceFacilityId))
                .ToArray();
        }
        catch (Exception exception)
        {
            failureReason = "generic-bill-retarget-stage-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (sources.Length == 0)
        {
            failureReason = "generic-bill-retarget-source-missing";
            return false;
        }

        string fingerprint = CaptureFingerprint("staged", sources, null);
        plan = ProductionFacilityRetargetAuthorityPlan.Create(
            AdapterId,
            fingerprint,
            new AdapterState(sources));
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
            failureReason = "generic-bill-retarget-binding-coverage-invalid";
            return false;
        }
        for (int index = 0; index < bindings.Length; index++)
        {
            ProductionFacilityRetargetBinding binding = bindings[index];
            SourceSnapshot source = state.Sources[index];
            if (binding == null
                || !binding.SourceFacilityId.Equals(source.FacilityId)
                || binding.TargetFacility == null
                || binding.TargetFacility.IsDestroyed)
            {
                failureReason = "generic-bill-retarget-binding-invalid";
                return false;
            }
            if (source.BillCount > 0
                && !binding.TargetFacilityId.Equals(source.FacilityId))
            {
                failureReason =
                    "generic-bill-retarget-reauthor-adapter-required:"
                    + source.FacilityId.Value + "->"
                    + binding.TargetFacilityId.Value;
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
        failureReason = string.Empty;
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

    private SourceSnapshot Capture(BuildingInstanceId facilityId)
    {
        ProductionFacilityBillLifecycleSnapshot snapshot =
            bills.CaptureFacilityLifecycle(facilityId);
        return new SourceSnapshot(
            facilityId,
            snapshot.BillCount,
            snapshot.ActiveWipCount,
            snapshot.DurableSemanticFingerprint);
    }

    private bool TryVerifySources(
        IReadOnlyList<SourceSnapshot> sources,
        out string failureReason)
    {
        foreach (SourceSnapshot expected in sources)
        {
            ProductionFacilityBillLifecycleSnapshot current;
            try
            {
                current = bills.CaptureFacilityLifecycle(expected.FacilityId);
            }
            catch (Exception exception)
            {
                failureReason = "generic-bill-retarget-capture-failed:"
                    + exception.GetType().Name;
                return false;
            }
            if (current.BillCount != expected.BillCount
                || current.ActiveWipCount != expected.ActiveWipCount
                || !string.Equals(
                    current.DurableSemanticFingerprint,
                    expected.DurableFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "generic-bill-retarget-source-drift:"
                    + expected.FacilityId.Value;
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool TryRequireState(
        ProductionFacilityRetargetAuthorityPlan plan,
        out AdapterState state,
        out string failureReason)
    {
        state = plan?.AdapterState as AdapterState;
        if (state == null
            || !string.Equals(
                plan.AdapterId,
                "generic-bill-identity",
                StringComparison.Ordinal))
        {
            failureReason = "generic-bill-retarget-plan-invalid";
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
            canonical.Append(source.FacilityId.Value).Append('|')
                .Append(source.BillCount).Append('|')
                .Append(source.ActiveWipCount).Append('|')
                .Append(source.DurableFingerprint).Append(';');
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
            BuildingInstanceId facilityId,
            int billCount,
            int activeWipCount,
            string durableFingerprint)
        {
            FacilityId = facilityId;
            BillCount = billCount;
            ActiveWipCount = activeWipCount;
            DurableFingerprint = durableFingerprint;
        }

        public BuildingInstanceId FacilityId { get; }
        public int BillCount { get; }
        public int ActiveWipCount { get; }
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
