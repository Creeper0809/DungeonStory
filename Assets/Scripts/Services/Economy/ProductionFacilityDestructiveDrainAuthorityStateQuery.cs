using System;
using System.Linq;

public enum ProductionFacilityDestructiveDrainAuthorityPairState
{
    Absent = 0,
    Exact = 1,
    Invalid = 2
}

public readonly struct ProductionFacilityDestructiveDrainAuthorityPairSnapshot
{
    public ProductionFacilityDestructiveDrainAuthorityPairSnapshot(
        string destinationId,
        ProductionFacilityDestructiveDrainAuthorityPairState state,
        string failureReason)
    {
        DestinationId = destinationId ?? string.Empty;
        State = state;
        FailureReason = failureReason ?? string.Empty;
    }

    public string DestinationId { get; }
    public ProductionFacilityDestructiveDrainAuthorityPairState State { get; }
    public string FailureReason { get; }
    public bool IsAbsent =>
        State == ProductionFacilityDestructiveDrainAuthorityPairState.Absent;
    public bool IsExact =>
        State == ProductionFacilityDestructiveDrainAuthorityPairState.Exact;
    public bool IsInvalid =>
        State == ProductionFacilityDestructiveDrainAuthorityPairState.Invalid;
}

public readonly struct ProductionFacilityDestructiveDrainAuthoritySnapshot
{
    public ProductionFacilityDestructiveDrainAuthoritySnapshot(
        ProductionFacilityDestructiveDrainAuthorityPairSnapshot sensor,
        ProductionFacilityDestructiveDrainAuthorityPairSnapshot output)
    {
        Sensor = sensor;
        Output = output;
    }

    public ProductionFacilityDestructiveDrainAuthorityPairSnapshot Sensor { get; }
    public ProductionFacilityDestructiveDrainAuthorityPairSnapshot Output { get; }
    public bool HasInvalidPair => Sensor.IsInvalid || Output.IsInvalid;
    public bool AllAbsent => Sensor.IsAbsent && Output.IsAbsent;
    public string FailureReason => Sensor.IsInvalid
        ? Sensor.FailureReason
        : Output.IsInvalid
            ? Output.FailureReason
            : string.Empty;
}

public interface IProductionFacilityDestructiveDrainAuthorityStateQuery
{
    ProductionFacilityDestructiveDrainAuthoritySnapshot Capture(
        BuildingInstanceId facilityId);
}

/// <summary>
/// Shared read-only topology authority for destructive removal. Both the
/// revoker and upper coordinator consume the same exact claim/profile view so
/// a sensor socket cannot survive an otherwise empty lifecycle projection.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainAuthorityStateQuery :
    IProductionFacilityDestructiveDrainAuthorityStateQuery
{
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;

    public ProductionFacilityDestructiveDrainAuthorityStateQuery(
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities)
    {
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
    }

    public ProductionFacilityDestructiveDrainAuthoritySnapshot Capture(
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            throw new ArgumentException(
                "A valid facility identity is required.",
                nameof(facilityId));
        }

        return new ProductionFacilityDestructiveDrainAuthoritySnapshot(
            CapturePair(
                ProductionStockSensorRuntime.BuildDestinationId(facilityId.Value),
                ProductionStockSensorDestinationAuthorityRuntime.OwnerDomain,
                facilityId,
                ProductionStockSensorDestinationAuthorityRuntime
                    .CapacitySchemaRevision),
            CapturePair(
                ProductionBillRuntime.OutputDestinationPrefix + facilityId.Value,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                facilityId,
                ProductionOutputDestinationAuthorityRuntime
                    .CapacitySchemaRevision));
    }

    private ProductionFacilityDestructiveDrainAuthorityPairSnapshot CapturePair(
        string destinationId,
        string ownerDomain,
        BuildingInstanceId facilityId,
        long expectedCapacityRevision)
    {
        FacilityBufferDestinationClaim[] matchingClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length == 0 && matchingProfiles.Length == 0)
        {
            return new ProductionFacilityDestructiveDrainAuthorityPairSnapshot(
                destinationId,
                ProductionFacilityDestructiveDrainAuthorityPairState.Absent,
                string.Empty);
        }

        if (matchingClaims.Length != 1 || matchingProfiles.Length != 1)
        {
            return Invalid(
                destinationId,
                "production-destructive-drain-authority-pair-cardinality-invalid:"
                + destinationId + ":claim=" + matchingClaims.Length
                + ":profile=" + matchingProfiles.Length);
        }

        FacilityBufferDestinationClaim claim = matchingClaims[0];
        FacilityBufferCapacityProfile profile = matchingProfiles[0];
        if (!string.Equals(claim.OwnerDomain, ownerDomain, StringComparison.Ordinal)
            || !string.Equals(profile.OwnerDomain, ownerDomain, StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerFacilityId,
                facilityId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                profile.OwnerFacilityId,
                facilityId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerOperationId,
                destinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                profile.OwnerOperationId,
                destinationId,
                StringComparison.Ordinal)
            || claim.DropPosition != profile.DropPosition
            || claim.AnchorKind != FacilityBufferDestinationAnchorKind.LiveFacility
            || profile.MaxMassGrams <= 0L
            || profile.CapacityRevision != expectedCapacityRevision)
        {
            return Invalid(
                destinationId,
                "production-destructive-drain-authority-pair-semantic-invalid:"
                + destinationId);
        }

        return new ProductionFacilityDestructiveDrainAuthorityPairSnapshot(
            destinationId,
            ProductionFacilityDestructiveDrainAuthorityPairState.Exact,
            string.Empty);
    }

    private static ProductionFacilityDestructiveDrainAuthorityPairSnapshot Invalid(
        string destinationId,
        string reason) => new(
        destinationId,
        ProductionFacilityDestructiveDrainAuthorityPairState.Invalid,
        reason);
}
