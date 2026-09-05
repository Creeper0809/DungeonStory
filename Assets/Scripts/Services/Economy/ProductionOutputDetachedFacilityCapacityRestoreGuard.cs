using System;
using System.Collections.Generic;
using System.Linq;

public interface IProductionOutputDetachedFacilityCapacityRestoreGuard
{
    ProductionOutputBufferCapacitySourceSnapshot Validate(
        string ownerStableId,
        string facilityInstanceId,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        string savedCapacitySourceDigest,
        long savedRequiredMinimumCapacityGrams);
}

public interface IProductionOutputDetachedFacilityCapacityProjectionQuery
{
    ProductionOutputDetachedFacilityCapacityProjection Capture(
        string ownerStableId,
        string facilityInstanceId,
        ProductionOutputBatchMaximumMassProof maximumMassProof);
}

public readonly struct ProductionOutputDetachedFacilityCapacityProjection
{
    public ProductionOutputDetachedFacilityCapacityProjection(
        string facilityInstanceId,
        UnityEngine.Vector2Int facilityPosition,
        ProductionOutputBufferCapacitySourceSnapshot capacity)
    {
        FacilityInstanceId = facilityInstanceId ?? string.Empty;
        FacilityPosition = facilityPosition;
        Capacity = capacity;
    }

    public string FacilityInstanceId { get; }
    public UnityEngine.Vector2Int FacilityPosition { get; }
    public ProductionOutputBufferCapacitySourceSnapshot Capacity { get; }
}

/// <summary>
/// Reprojects capacity only from the detached facility-world restore candidate.
/// It never falls back to the live world, repairs identifiers, or accepts an
/// epsilon. Owner adapters remain responsible for reconstructing their frozen
/// capability proof before calling this common boundary.
/// </summary>
public sealed class ProductionOutputDetachedFacilityCapacityRestoreGuard :
    IProductionOutputDetachedFacilityCapacityRestoreGuard,
    IProductionOutputDetachedFacilityCapacityProjectionQuery
{
    private readonly IRestoreWorldCandidateQuery worldCandidates;
    private readonly IProductionFacilityHandleQuery facilityHandles;
    private readonly IProductionOutputBufferCapacityProjector capacity;

    public ProductionOutputDetachedFacilityCapacityRestoreGuard(
        IRestoreWorldCandidateQuery worldCandidates,
        IProductionFacilityHandleQuery facilityHandles,
        IProductionOutputBufferCapacityProjector capacity)
    {
        this.worldCandidates = worldCandidates
            ?? throw new ArgumentNullException(nameof(worldCandidates));
        this.facilityHandles = facilityHandles
            ?? throw new ArgumentNullException(nameof(facilityHandles));
        this.capacity = capacity
            ?? throw new ArgumentNullException(nameof(capacity));
    }

    public ProductionOutputBufferCapacitySourceSnapshot Validate(
        string ownerStableId,
        string facilityInstanceId,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        string savedCapacitySourceDigest,
        long savedRequiredMinimumCapacityGrams)
    {
        RequireCanonical(ownerStableId, nameof(ownerStableId));
        RequireCanonical(facilityInstanceId, nameof(facilityInstanceId));
        if (maximumMassProof == null)
            throw new ArgumentNullException(nameof(maximumMassProof));
        if (!IsLowercaseSha256(savedCapacitySourceDigest)
            || savedRequiredMinimumCapacityGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Detached production-output capacity authority is incomplete: "
                + ownerStableId);
        }
        ProductionOutputDetachedFacilityCapacityProjection projection = Capture(
            ownerStableId,
            facilityInstanceId,
            maximumMassProof);
        ProductionOutputBufferCapacitySourceSnapshot projected =
            projection.Capacity;
        if (!string.Equals(
                projected.SourceDigest,
                savedCapacitySourceDigest,
                StringComparison.Ordinal)
            || projected.RequiredMinimumCapacityGrams
                != savedRequiredMinimumCapacityGrams)
        {
            throw new InvalidOperationException(
                "Detached production-output capacity source drifted: "
                + ownerStableId + ":" + facilityInstanceId);
        }
        return projected;
    }

    public ProductionOutputDetachedFacilityCapacityProjection Capture(
        string ownerStableId,
        string facilityInstanceId,
        ProductionOutputBatchMaximumMassProof maximumMassProof)
    {
        RequireCanonical(ownerStableId, nameof(ownerStableId));
        RequireCanonical(facilityInstanceId, nameof(facilityInstanceId));
        if (maximumMassProof == null)
            throw new ArgumentNullException(nameof(maximumMassProof));
        if (!worldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> buildings)
            || buildings == null)
        {
            throw new InvalidOperationException(
                "Detached production-output capacity validation requires the facility-world candidate: "
                + ownerStableId);
        }

        BuildableObject[] matches = buildings
            .Where(value => value != null
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    facilityInstanceId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Detached production-output capacity owner must resolve exactly one facility: "
                + ownerStableId + ":" + facilityInstanceId);
        }

        ProductionFacilityHandle facility = facilityHandles.CaptureFacility(
            matches[0]);
        if (facility == null
            || facility.IsDestroyed
            || !facility.InstanceId.IsValid
            || !string.Equals(
                facility.InstanceId.Value,
                facilityInstanceId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Detached production-output capacity facility is invalid: "
                + ownerStableId + ":" + facilityInstanceId);
        }

        ProductionOutputBufferCapacitySourceSnapshot projected =
            capacity.CaptureSource(facility, maximumMassProof);
        return new ProductionOutputDetachedFacilityCapacityProjection(
            facility.InstanceId.Value,
            facility.Position,
            projected);
    }

    private static void RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A non-empty canonical identifier is required.",
                parameterName);
        }
    }

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!(character >= '0' && character <= '9')
                && !(character >= 'a' && character <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}
