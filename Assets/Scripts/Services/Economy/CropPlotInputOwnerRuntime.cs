using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class CropPlotInputOwnerDescriptor
{
    public CropPlotInputOwnerDescriptor(
        string plotId,
        Vector2Int position,
        string destinationId,
        string operationId,
        IReadOnlyDictionary<string, int> requirements)
    {
        RequireCanonical(plotId, nameof(plotId));
        RequireCanonical(destinationId, nameof(destinationId));
        RequireCanonical(operationId, nameof(operationId));
        if (requirements == null
            || requirements.Count == 0
            || requirements.Any(value => !IsCanonical(value.Key)
                || value.Value <= 0))
        {
            throw new ArgumentException(
                "Crop-plot input owner requires a positive exact input vector.",
                nameof(requirements));
        }

        PlotId = plotId;
        Position = position;
        DestinationId = destinationId;
        OperationId = operationId;
        Requirements = requirements
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => value.Value,
                StringComparer.Ordinal);
    }

    public string PlotId { get; }
    public Vector2Int Position { get; }
    public string DestinationId { get; }
    public string OperationId { get; }
    public IReadOnlyDictionary<string, int> Requirements { get; }

    private static void RequireCanonical(string value, string name)
    {
        if (!IsCanonical(value))
            throw new ArgumentException(
                "Crop-plot input ownership requires canonical IDs.",
                name);
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class CropPlotInputOwnerProjection
{
    internal CropPlotInputOwnerProjection(
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        Claims = claims ?? throw new ArgumentNullException(nameof(claims));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; }
    public IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; }
}

public static class CropPlotInputOwnerAuthority
{
    public const string OwnerDomain = "economy.crop-plot";
    public const long CapacitySchemaRevision = 1L;
    public const string SowCompletedReleaseReasonCode =
        "crop-plot-sow-input-completed";
    public const string TreatmentCompletedReleaseReasonCode =
        "crop-plot-treatment-input-completed";
    public const string CropChangedReleaseReasonCode =
        "crop-plot-input-crop-changed";
    public const string TreatmentCancelledReleaseReasonCode =
        "crop-plot-treatment-cancelled";
    public const string PlotLostReleaseReasonCode =
        "crop-plot-input-facility-lost";
    public const string RuntimeDisposedReleaseReasonCode =
        "crop-plot-input-runtime-disposed";

    public static string BuildSowDestinationId(
        string plotId,
        int operationSequence) => BuildDestinationId(
        "sow",
        plotId,
        operationSequence);

    public static string BuildTreatmentDestinationId(
        string plotId,
        int operationSequence) => BuildDestinationId(
        "treatment",
        plotId,
        operationSequence);

    public static CropPlotInputOwnerProjection BuildProjection(
        IEnumerable<CropPlotInputOwnerDescriptor> source,
        IPhysicalItemMassQuery massQuery)
    {
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));
        CropPlotInputOwnerDescriptor[] descriptors = (source
                ?? Array.Empty<CropPlotInputOwnerDescriptor>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Any(value => value == null)
            || descriptors.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count() != descriptors.Length)
        {
            throw new InvalidOperationException(
                "Crop-plot input descriptors must be non-null and destination-unique.");
        }

        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        foreach (CropPlotInputOwnerDescriptor descriptor in descriptors)
        {
            long maximumMass = 0L;
            foreach (KeyValuePair<string, int> requirement in
                     descriptor.Requirements)
            {
                long unitMass = massQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)requirement.Key).Value;
                if (unitMass <= 0L)
                {
                    throw new InvalidOperationException(
                        "Crop-plot input item mass must be positive: "
                        + requirement.Key);
                }
                maximumMass = checked(maximumMass
                    + checked(unitMass * requirement.Value));
            }
            if (maximumMass <= 0L)
                throw new InvalidOperationException(
                    "Crop-plot input owner capacity must be positive: "
                    + descriptor.DestinationId);

            claims.Add(new FacilityBufferDestinationClaim(
                descriptor.DestinationId,
                descriptor.Position,
                OwnerDomain,
                descriptor.OperationId,
                descriptor.PlotId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired));
            profiles.Add(new FacilityBufferCapacityProfile(
                descriptor.DestinationId,
                descriptor.Position,
                OwnerDomain,
                descriptor.OperationId,
                descriptor.PlotId,
                new PhysicalMassGrams(maximumMass),
                CapacitySchemaRevision));
        }
        return new CropPlotInputOwnerProjection(claims, profiles);
    }

    public static bool ClaimsMatch(
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

    public static bool ProfilesMatch(
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

    private static string BuildDestinationId(
        string kind,
        string plotId,
        int operationSequence)
    {
        if (string.IsNullOrWhiteSpace(plotId)
            || !string.Equals(plotId, plotId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                "Crop-plot input destination requires a canonical plot ID.",
                nameof(plotId));
        if (operationSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(operationSequence));
        return ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
            + OwnerDomain + ":" + kind + ":"
            + Uri.EscapeDataString(plotId) + ":"
            + operationSequence.ToString(
                "D8",
                System.Globalization.CultureInfo.InvariantCulture);
    }
}

public interface ICropPlotInputOwnerRuntime
{
    bool TryEnsure(
        CropPlotInputOwnerDescriptor descriptor,
        out string failureReason);

    bool TryRetireDestination(
        string destinationId,
        Vector2Int ownerPosition,
        string reasonCode,
        out string failureReason);

    bool TryReconcileLive(
        IReadOnlyList<CropPlotInputOwnerDescriptor> descriptors,
        out string failureReason);

    bool TryReplaceForRestore(
        IReadOnlyList<CropPlotInputOwnerDescriptor> descriptors,
        out string failureReason);
}

public interface ICropPlotInputOwnerDescriptorSource
{
    IReadOnlyList<CropPlotInputOwnerDescriptor> BuildLiveInputOwnerDescriptors();

    IReadOnlyList<CropPlotInputOwnerDescriptor> BuildInputOwnerDescriptors(
        CropPlotRestoreCandidate candidate,
        IReadOnlyList<BuildableObject> detachedBuildings);
}

/// <summary>
/// Owns the exact claim/profile pair for every live crop sow or treatment
/// destination. Terminal retirement first releases unpicked, carried, and
/// deposited custody through the shared release service and only then revokes
/// the paired authority.
/// </summary>
public sealed class CropPlotInputOwnerRuntime : ICropPlotInputOwnerRuntime
{
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public CropPlotInputOwnerRuntime(
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryEnsure(
        CropPlotInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            CropPlotInputOwnerProjection addition =
                CropPlotInputOwnerAuthority.BuildProjection(
                    new[] { descriptor },
                    massQuery);
            if (!TryCaptureOwnedPairs(
                    out FacilityBufferDestinationClaim[] currentClaims,
                    out FacilityBufferCapacityProfile[] currentProfiles,
                    out failureReason))
                return false;

            int index = Array.FindIndex(
                currentClaims,
                value => string.Equals(
                    value.DestinationId,
                    descriptor.DestinationId,
                    StringComparison.Ordinal));
            if (index >= 0)
            {
                bool exact = CropPlotInputOwnerAuthority.ClaimsMatch(
                        currentClaims[index],
                        addition.Claims[0])
                    && CropPlotInputOwnerAuthority.ProfilesMatch(
                        currentProfiles[index],
                        addition.Profiles[0]);
                if (exact)
                    return true;

                bool safeExpansion = CropPlotInputOwnerAuthority.ClaimsMatch(
                        currentClaims[index],
                        addition.Claims[0])
                    && addition.Profiles[0].MaxMassGrams
                        >= currentProfiles[index].MaxMassGrams
                    && currentProfiles[index].CapacityRevision
                        == addition.Profiles[0].CapacityRevision;
                if (!safeExpansion
                    && !TryRelease(
                        currentClaims[index],
                        "crop-plot-input-authority-replaced",
                        out failureReason))
                    return false;

                currentClaims[index] = addition.Claims[0];
                currentProfiles[index] = addition.Profiles[0];
                return TryPublish(
                    currentClaims,
                    currentProfiles,
                    out failureReason);
            }

            return TryPublish(
                currentClaims.Append(addition.Claims[0]),
                currentProfiles.Append(addition.Profiles[0]),
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "crop-plot-input-owner-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public bool TryRetireDestination(
        string destinationId,
        Vector2Int ownerPosition,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCanonical(destinationId) || !IsCanonical(reasonCode)
            || !TryCaptureOwnedPairs(
                out FacilityBufferDestinationClaim[] currentClaims,
                out FacilityBufferCapacityProfile[] currentProfiles,
                out failureReason))
        {
            if (failureReason.Length == 0)
                failureReason = "crop-plot-input-owner-retire-invalid";
            return false;
        }

        int index = Array.FindIndex(
            currentClaims,
            value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal));
        if (index < 0)
            return true;
        if (currentClaims[index].DropPosition != ownerPosition)
        {
            failureReason = "crop-plot-input-owner-retire-position-mismatch:"
                + destinationId;
            return false;
        }
        if (!TryRelease(currentClaims[index], reasonCode, out failureReason))
            return false;

        return TryPublish(
            currentClaims.Where((_, itemIndex) => itemIndex != index),
            currentProfiles.Where((_, itemIndex) => itemIndex != index),
            out failureReason);
    }

    public bool TryReconcileLive(
        IReadOnlyList<CropPlotInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            CropPlotInputOwnerProjection desired =
                CropPlotInputOwnerAuthority.BuildProjection(
                    descriptors,
                    massQuery);
            if (!TryCaptureOwnedPairs(
                    out FacilityBufferDestinationClaim[] currentClaims,
                    out FacilityBufferCapacityProfile[] currentProfiles,
                    out failureReason))
                return false;
            if (SetsMatch(
                    currentClaims,
                    currentProfiles,
                    desired.Claims,
                    desired.Profiles))
                return true;

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
                bool retained = desiredClaims.TryGetValue(
                        claim.DestinationId,
                        out FacilityBufferDestinationClaim desiredClaim)
                    && desiredProfiles.TryGetValue(
                        claim.DestinationId,
                        out FacilityBufferCapacityProfile desiredProfile)
                    && CropPlotInputOwnerAuthority.ClaimsMatch(
                        claim,
                        desiredClaim)
                    && (CropPlotInputOwnerAuthority.ProfilesMatch(
                            profile,
                            desiredProfile)
                        || desiredProfile.MaxMassGrams >= profile.MaxMassGrams
                        && desiredProfile.CapacityRevision
                            == profile.CapacityRevision);
                if (!retained
                    && !TryRelease(
                        claim,
                        "crop-plot-input-authority-retired",
                        out failureReason))
                    return false;
            }
            return TryPublish(
                desired.Claims,
                desired.Profiles,
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "crop-plot-input-owner-live-reconcile-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public bool TryReplaceForRestore(
        IReadOnlyList<CropPlotInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            CropPlotInputOwnerProjection desired =
                CropPlotInputOwnerAuthority.BuildProjection(
                    descriptors,
                    massQuery);
            return TryPublish(
                desired.Claims,
                desired.Profiles,
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "crop-plot-input-owner-restore-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private bool TryCaptureOwnedPairs(
        out FacilityBufferDestinationClaim[] ownedClaims,
        out FacilityBufferCapacityProfile[] ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CropPlotInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CropPlotInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (ownedClaims.Length != ownedProfiles.Length
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason = "crop-plot-input-owner-pair-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryRelease(
        FacilityBufferDestinationClaim claim,
        string reasonCode,
        out string failureReason)
    {
        if (releases.TryReleaseAtOwnerPosition(
                claim.DestinationId,
                claim.DropPosition,
                reasonCode,
                out _,
                out string releaseFailure))
        {
            failureReason = string.Empty;
            return true;
        }
        failureReason = "crop-plot-input-owner-terminal-release-failed:"
            + claim.DestinationId + ":" + releaseFailure;
        return false;
    }

    private bool TryPublish(
        IEnumerable<FacilityBufferDestinationClaim> desiredClaims,
        IEnumerable<FacilityBufferCapacityProfile> desiredProfiles,
        out string failureReason)
    {
        bool published = lifecycle.TryReplaceOwnedAuthorities(
            CropPlotInputOwnerAuthority.OwnerDomain,
            desiredClaims.OrderBy(
                value => value.DestinationId,
                StringComparer.Ordinal).ToArray(),
            desiredProfiles.OrderBy(
                value => value.DestinationId,
                StringComparer.Ordinal).ToArray(),
            out failureReason);
        if (!published)
            failureReason = "crop-plot-input-owner-publish-failed:"
                + failureReason;
        return published;
    }

    private static bool SetsMatch(
        IReadOnlyList<FacilityBufferDestinationClaim> leftClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> leftProfiles,
        IReadOnlyList<FacilityBufferDestinationClaim> rightClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> rightProfiles) =>
        leftClaims.Count == rightClaims.Count
        && leftProfiles.Count == rightProfiles.Count
        && leftClaims.Zip(
                rightClaims,
                CropPlotInputOwnerAuthority.ClaimsMatch)
            .All(value => value)
        && leftProfiles.Zip(
                rightProfiles,
                CropPlotInputOwnerAuthority.ProfilesMatch)
            .All(value => value);

    private static bool IsProjectionException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException
            or OverflowException;

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
