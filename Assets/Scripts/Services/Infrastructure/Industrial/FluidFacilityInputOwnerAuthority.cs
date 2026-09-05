using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal interface IFluidFacilityInputOwnerAuthority
{
    bool TryReconcile(
        IndustrialTopologySnapshot topology,
        out string failureReason);

    bool TryEnsureManualDestination(
        BuildableObject facility,
        string destinationId,
        float requestedWaterUnits,
        out string failureReason);
}

internal sealed class FluidFacilityInputOwnerDescriptor
{
    internal FluidFacilityInputOwnerDescriptor(
        string ownerDomain,
        string destinationId,
        string operationId,
        string facilityId,
        Vector2Int position,
        FacilityBufferDestinationAnchorKind anchorKind,
        int cleanWaterQuantity,
        long cleanWaterUnitMassGrams,
        long massAuthorityRevision)
    {
        if (!Canonical(ownerDomain)
            || !Canonical(destinationId)
            || !Canonical(operationId)
            || !Canonical(facilityId)
            || !Enum.IsDefined(typeof(FacilityBufferDestinationAnchorKind), anchorKind)
            || anchorKind == FacilityBufferDestinationAnchorKind.ReservedTarget
            || cleanWaterQuantity <= 0
            || cleanWaterUnitMassGrams <= 0L
            || massAuthorityRevision < 0L)
        {
            throw new ArgumentException(
                "Fluid input ownership requires canonical live identity and positive exact clean-water mass.");
        }

        OwnerDomain = ownerDomain;
        DestinationId = destinationId;
        OperationId = operationId;
        FacilityId = facilityId;
        Position = position;
        AnchorKind = anchorKind;
        CleanWaterQuantity = cleanWaterQuantity;
        CleanWaterUnitMassGrams = cleanWaterUnitMassGrams;
        MassAuthorityRevision = massAuthorityRevision;
    }

    internal string OwnerDomain { get; }
    internal string DestinationId { get; }
    internal string OperationId { get; }
    internal string FacilityId { get; }
    internal Vector2Int Position { get; }
    internal FacilityBufferDestinationAnchorKind AnchorKind { get; }
    internal int CleanWaterQuantity { get; }
    internal long CleanWaterUnitMassGrams { get; }
    internal long MassAuthorityRevision { get; }
    internal long CapacityGrams => checked(
        CleanWaterUnitMassGrams * CleanWaterQuantity);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

internal sealed class FluidFacilityInputOwnerProjection
{
    internal FluidFacilityInputOwnerProjection(
        string ownerDomain,
        long massAuthorityRevision,
        string fingerprint,
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        OwnerDomain = ownerDomain;
        MassAuthorityRevision = massAuthorityRevision;
        Fingerprint = fingerprint;
        Claims = claims;
        Profiles = profiles;
    }

    internal string OwnerDomain { get; }
    internal long MassAuthorityRevision { get; }
    internal string Fingerprint { get; }
    internal IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; }
    internal IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; }
}

public static class FluidFacilityInputOwnerProjectionAuthority
{
    internal const string FluidOwnerDomain = "infrastructure.fluid";
    internal const string ProcessFluidOwnerDomain =
        "infrastructure.process-fluid";
    internal const long CapacitySchemaRevision = 1L;
    public const string CleanWaterItemId = "resource:clean-water";
    internal const string RetiredReleaseReason =
        "infrastructure-fluid-input-owner-retired";

    internal static FluidFacilityInputOwnerProjection Build(
        string ownerDomain,
        IEnumerable<FluidFacilityInputOwnerDescriptor> source)
    {
        FluidFacilityInputOwnerDescriptor[] descriptors = (source
                ?? Array.Empty<FluidFacilityInputOwnerDescriptor>())
            .Where(value => value != null
                && string.Equals(
                    value.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count()
            != descriptors.Length)
        {
            throw new InvalidOperationException(
                "Fluid input owner destinations must be unique: "
                + ownerDomain);
        }

        long massRevision = descriptors.Length == 0
            ? 0L
            : descriptors[0].MassAuthorityRevision;
        if (descriptors.Any(value =>
                value.MassAuthorityRevision != massRevision))
        {
            throw new InvalidOperationException(
                "Fluid input owner observed more than one mass authority revision.");
        }

        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("fluid-facility-input-owner@1");
        digest.Append(ownerDomain);
        digest.Append(massRevision);
        foreach (FluidFacilityInputOwnerDescriptor descriptor in descriptors)
        {
            long capacity = descriptor.CapacityGrams;
            if (capacity <= 0L)
                throw new InvalidOperationException(
                    "Fluid input owner capacity must be positive: "
                    + descriptor.DestinationId);

            claims.Add(new FacilityBufferDestinationClaim(
                descriptor.DestinationId,
                descriptor.Position,
                ownerDomain,
                descriptor.OperationId,
                descriptor.FacilityId,
                descriptor.AnchorKind,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired));
            profiles.Add(new FacilityBufferCapacityProfile(
                descriptor.DestinationId,
                descriptor.Position,
                ownerDomain,
                descriptor.OperationId,
                descriptor.FacilityId,
                new PhysicalMassGrams(capacity),
                CapacitySchemaRevision));

            digest.Append(descriptor.DestinationId);
            digest.Append(descriptor.OperationId);
            digest.Append(descriptor.FacilityId);
            digest.Append(descriptor.Position.x);
            digest.Append(descriptor.Position.y);
            digest.Append((int)descriptor.AnchorKind);
            digest.Append(descriptor.CleanWaterQuantity);
            digest.Append(descriptor.CleanWaterUnitMassGrams);
            digest.Append(capacity);
        }

        return new FluidFacilityInputOwnerProjection(
            ownerDomain,
            massRevision,
            digest.ComputeSha256(),
            claims,
            profiles);
    }

    internal static bool ClaimMatches(
        FacilityBufferDestinationClaim left,
        FacilityBufferDestinationClaim right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && left.AnchorKind == right.AnchorKind
        && left.AdmissionPolicy == right.AdmissionPolicy
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerDomain, right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId,
            StringComparison.Ordinal);

    internal static bool ProfileMatches(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && left.MaxMassGrams == right.MaxMassGrams
        && left.CapacityRevision == right.CapacityRevision
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerDomain, right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId,
            StringComparison.Ordinal);
}

/// <summary>
/// Owns the positive-gram FacilityBuffer boundaries used by manual fixtures,
/// container-to-network transfer, and process-fluid manual fallback. The
/// physical debit remains the Fluid V6 pending Transfer; this service owns only
/// the exact live destination pair and retires custody before that pair.
/// </summary>
internal sealed class FluidFacilityInputOwnerAuthority :
    IFluidFacilityInputOwnerAuthority
{
    private const string ManualPrefix = "plumbing:manual-water:";
    private const string TransferPrefix = "plumbing:water-transfer:";
    private const string ProcessPrefix = "plumbing:process-water:";

    private readonly IPhysicalItemMassQuery mass;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;
    private IndustrialTopologySnapshot currentTopology;

    internal FluidFacilityInputOwnerAuthority(
        IPhysicalItemMassQuery mass,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryReconcile(
        IndustrialTopologySnapshot topology,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (topology == null)
        {
            failureReason = "fluid-input-owner-topology-missing";
            return false;
        }

        currentTopology = topology;
        try
        {
            IReadOnlyList<FluidFacilityInputOwnerDescriptor> descriptors =
                BuildDescriptors(topology);
            FluidFacilityInputOwnerProjection fluid =
                FluidFacilityInputOwnerProjectionAuthority.Build(
                    FluidFacilityInputOwnerProjectionAuthority.FluidOwnerDomain,
                    descriptors);
            FluidFacilityInputOwnerProjection process =
                FluidFacilityInputOwnerProjectionAuthority.Build(
                    FluidFacilityInputOwnerProjectionAuthority
                        .ProcessFluidOwnerDomain,
                    descriptors);
            return TryReconcileOwner(fluid, out failureReason)
                && TryReconcileOwner(process, out failureReason);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            failureReason = "fluid-input-owner-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public bool TryEnsureManualDestination(
        BuildableObject facility,
        string destinationId,
        float requestedWaterUnits,
        out string failureReason)
    {
        failureReason = string.Empty;
        string destination = destinationId ?? string.Empty;
        if (facility == null
            || requestedWaterUnits < 0f
            || float.IsNaN(requestedWaterUnits)
            || float.IsInfinity(requestedWaterUnits)
            || !Canonical(destination)
            || currentTopology == null
            || !TryReconcile(currentTopology, out failureReason))
        {
            if (failureReason.Length == 0)
                failureReason = "fluid-input-owner-ensure-invalid";
            return false;
        }

        string ownerDomain = destination.StartsWith(
                ProcessPrefix,
                StringComparison.Ordinal)
            ? FluidFacilityInputOwnerProjectionAuthority.ProcessFluidOwnerDomain
            : destination.StartsWith(ManualPrefix, StringComparison.Ordinal)
                || destination.StartsWith(TransferPrefix, StringComparison.Ordinal)
                ? FluidFacilityInputOwnerProjectionAuthority.FluidOwnerDomain
                : string.Empty;
        string facilityId = IndustrialInfrastructureIdentity.GetNodeId(facility);
        FacilityBufferDestinationClaim[] matchingClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null
                && string.Equals(value.OwnerDomain, ownerDomain,
                    StringComparison.Ordinal)
                && string.Equals(value.DestinationId, destination,
                    StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null
                && string.Equals(value.OwnerDomain, ownerDomain,
                    StringComparison.Ordinal)
                && string.Equals(value.DestinationId, destination,
                    StringComparison.Ordinal))
            .ToArray();
        if (ownerDomain.Length == 0
            || matchingClaims.Length != 1
            || matchingProfiles.Length != 1
            || matchingClaims[0].DropPosition != facility.centerPos
            || matchingClaims[0].AdmissionPolicy
                != FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            || !string.Equals(
                matchingClaims[0].OwnerFacilityId,
                facilityId,
                StringComparison.Ordinal)
            || matchingProfiles[0].MaxMassGrams <= 0L
            || matchingProfiles[0].CapacityRevision
                != FluidFacilityInputOwnerProjectionAuthority
                    .CapacitySchemaRevision)
        {
            failureReason = "fluid-input-owner-pair-missing-or-mismatched:"
                + destination;
            return false;
        }
        return true;
    }

    private IReadOnlyList<FluidFacilityInputOwnerDescriptor> BuildDescriptors(
        IndustrialTopologySnapshot topology)
    {
        long massRevision = mass.AuthorityRevision;
        long unitMass = mass.GetDefinitionUnitMass(
            (ItemDefinitionId)FluidFacilityInputOwnerProjectionAuthority
                .CleanWaterItemId).Value;
        if (unitMass <= 0L || mass.AuthorityRevision != massRevision)
            throw new InvalidOperationException(
                "Fluid input owner requires stable positive clean-water mass.");

        List<FluidFacilityInputOwnerDescriptor> result = new();
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values
                     .Where(value => value?.Building != null
                         && !value.Building.isDestroy
                         && !value.Building.IsGridDestroyed)
                     .OrderBy(value => value.NodeId, StringComparer.Ordinal))
        {
            BuildableObject building = node.Building;
            BuildingWaterFixtureAbility fixture = building.BuildingData?
                .GetAbility<BuildingWaterFixtureAbility>();
            if (fixture is { allowsManualWaterFallback: true }
                && fixture.cleanWaterPerUse > 0f)
            {
                result.Add(Descriptor(
                    FluidFacilityInputOwnerProjectionAuthority.FluidOwnerDomain,
                    ManualPrefix + node.NodeId,
                    "manual-water-input-owner:" + node.NodeId,
                    node,
                    FacilityBufferDestinationAnchorKind.LiveBuilding,
                    Mathf.Max(1, Mathf.CeilToInt(fixture.cleanWaterPerUse)),
                    unitMass,
                    massRevision));
            }

            BuildingWaterContainerTransferAbility transfer = building
                .BuildingData?.GetAbility<BuildingWaterContainerTransferAbility>();
            if (transfer != null && transfer.waterPerBatch > 0f)
            {
                result.Add(Descriptor(
                    FluidFacilityInputOwnerProjectionAuthority.FluidOwnerDomain,
                    TransferPrefix + node.NodeId,
                    "container-water-input-owner:" + node.NodeId,
                    node,
                    FacilityBufferDestinationAnchorKind.LiveFacility,
                    Mathf.Max(1, Mathf.RoundToInt(transfer.waterPerBatch)),
                    unitMass,
                    massRevision));
            }

            BuildingProcessFluidAbility process = building.BuildingData?
                .GetAbility<BuildingProcessFluidAbility>();
            if (process is not { allowsManualWaterFallback: true }
                || process.cleanWaterPerCycle <= 0f)
            {
                continue;
            }
            foreach (string workTypeId in (process.workTypeIds
                         ?? Array.Empty<string>())
                     .Where(Canonical)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
            {
                string destination = ProcessPrefix + node.NodeId + ":"
                    + workTypeId;
                result.Add(Descriptor(
                    FluidFacilityInputOwnerProjectionAuthority
                        .ProcessFluidOwnerDomain,
                    destination,
                    "process-fluid-input-owner:" + node.NodeId + ":"
                        + workTypeId,
                    node,
                    FacilityBufferDestinationAnchorKind.LiveFacility,
                    Mathf.Max(1, Mathf.CeilToInt(process.cleanWaterPerCycle)),
                    unitMass,
                    massRevision));
            }
        }
        if (mass.AuthorityRevision != massRevision)
            throw new InvalidOperationException(
                "Fluid input owner mass authority changed during projection.");
        return result;
    }

    private static FluidFacilityInputOwnerDescriptor Descriptor(
        string ownerDomain,
        string destinationId,
        string operationId,
        IndustrialNodeDescriptor node,
        FacilityBufferDestinationAnchorKind anchorKind,
        int quantity,
        long unitMass,
        long massRevision) => new(
        ownerDomain,
        destinationId,
        operationId,
        node.NodeId,
        node.Building.centerPos,
        anchorKind,
        quantity,
        unitMass,
        massRevision);

    private bool TryReconcileOwner(
        FluidFacilityInputOwnerProjection desired,
        out string failureReason)
    {
        FacilityBufferDestinationClaim[] currentClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                desired.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferCapacityProfile[] currentProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                desired.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (currentClaims.Length != currentProfiles.Length
            || !currentClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    currentProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason = "fluid-input-owner-pair-set-torn:"
                + desired.OwnerDomain;
            return false;
        }

        if (currentClaims.Length == desired.Claims.Count
            && currentClaims.Select((claim, index) =>
                    FluidFacilityInputOwnerProjectionAuthority.ClaimMatches(
                        claim,
                        desired.Claims[index])
                    && FluidFacilityInputOwnerProjectionAuthority.ProfileMatches(
                        currentProfiles[index],
                        desired.Profiles[index]))
                .All(value => value))
        {
            failureReason = string.Empty;
            return true;
        }

        Dictionary<string, FacilityBufferDestinationClaim> desiredClaims =
            desired.Claims.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        Dictionary<string, FacilityBufferCapacityProfile> desiredProfiles =
            desired.Profiles.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        for (int index = 0; index < currentClaims.Length; index++)
        {
            FacilityBufferDestinationClaim claim = currentClaims[index];
            FacilityBufferCapacityProfile profile = currentProfiles[index];
            bool safe = desiredClaims.TryGetValue(
                    claim.DestinationId,
                    out FacilityBufferDestinationClaim desiredClaim)
                && desiredProfiles.TryGetValue(
                    claim.DestinationId,
                    out FacilityBufferCapacityProfile desiredProfile)
                && FluidFacilityInputOwnerProjectionAuthority.ClaimMatches(
                    claim,
                    desiredClaim)
                && (FluidFacilityInputOwnerProjectionAuthority.ProfileMatches(
                        profile,
                        desiredProfile)
                    || desiredProfile.MaxMassGrams >= profile.MaxMassGrams
                    && desiredProfile.CapacityRevision
                        == profile.CapacityRevision);
            if (safe)
                continue;
            if (!releases.TryReleaseAtOwnerPosition(
                    claim.DestinationId,
                    claim.DropPosition,
                    FluidFacilityInputOwnerProjectionAuthority
                        .RetiredReleaseReason,
                    out _,
                    out string releaseFailure))
            {
                failureReason = "fluid-input-owner-release-failed:"
                    + claim.DestinationId + ":" + releaseFailure;
                return false;
            }
        }

        if (!lifecycle.TryReplaceOwnedAuthorities(
                desired.OwnerDomain,
                desired.Claims,
                desired.Profiles,
                out failureReason))
        {
            failureReason = "fluid-input-owner-publish-failed:"
                + desired.OwnerDomain + ":" + failureReason;
            return false;
        }
        return true;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

#if UNITY_EDITOR
// Existing isolated fluid fixtures do not boot the global destination registries.
// Owner-specific scenarios exercise the real authority; this explicit fixture
// adapter preserves those narrow transaction tests without a production bypass.
internal sealed class EditorFluidFacilityInputOwnerAuthority :
    IFluidFacilityInputOwnerAuthority
{
    public bool TryReconcile(
        IndustrialTopologySnapshot topology,
        out string failureReason)
    {
        failureReason = string.Empty;
        return topology != null;
    }

    public bool TryEnsureManualDestination(
        BuildableObject facility,
        string destinationId,
        float requestedWaterUnits,
        out string failureReason)
    {
        failureReason = string.Empty;
        return facility != null
            && !string.IsNullOrWhiteSpace(destinationId)
            && requestedWaterUnits >= 0f;
    }
}
#endif
