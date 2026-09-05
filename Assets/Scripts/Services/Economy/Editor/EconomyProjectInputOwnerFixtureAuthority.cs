#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic Editor fixture that wires the production economy input-owner
/// runtime to the same claim, gram-capacity and atomic lifecycle authorities
/// used by gameplay. The fixture world owns no physical cargo, but terminal
/// releases are recorded rather than silently ignored.
/// </summary>
internal sealed class EconomyProjectInputOwnerFixtureAuthority
{
    private readonly FacilityBufferDestinationClaimRegistry claims = new();
    private readonly FacilityBufferMassAdmissionService capacities;
    private readonly EconomyProjectInputOwnerRuntime runtime;

    internal EconomyProjectInputOwnerFixtureAuthority(
        IPhysicalItemMassQuery massQuery)
    {
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));

        capacities = new FacilityBufferMassAdmissionService(
            claims,
            EmptyPhysicalOccupancy.Instance,
            massQuery);
        FacilityBufferDestinationLifecycleService lifecycle = new(
            claims,
            claims,
            capacities,
            capacities);
        runtime = new EconomyProjectInputOwnerRuntime(
            massQuery,
            claims,
            capacities,
            lifecycle,
            new RecordingEmptyWorldRelease());
        RestoreParticipants = new IDungeonRestoreTransactionParticipant[]
        {
            claims,
            capacities
        };
    }

    internal IEconomyProjectInputOwnerPort Runtime => runtime;
    internal IEconomyProjectInputOwnerRestoreRuntime RestoreRuntime => runtime;
    internal IReadOnlyList<IDungeonRestoreTransactionParticipant>
        RestoreParticipants { get; }

    private sealed class EmptyPhysicalOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        internal static readonly EmptyPhysicalOccupancy Instance = new();

        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId)
        {
            RequireCanonical(destinationId, nameof(destinationId));
            return new FacilityBufferPhysicalOccupancySnapshot(0L, 0L);
        }

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "fixture-world-has-no-physical-cargo";
            return false;
        }
    }

    private sealed class RecordingEmptyWorldRelease :
        IFacilityBufferDestinationReleaseService
    {
        private readonly HashSet<string> releasedDestinations =
            new(StringComparer.Ordinal);

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            RequireCanonical(destinationId, nameof(destinationId));
            RequireCanonical(reasonCode, nameof(reasonCode));
            releasedDestinations.Add(destinationId);
            releasedQuantity = 0;
            failureReason = string.Empty;
            return true;
        }
    }

    private static void RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Fixture authority requires a canonical identifier.",
                parameterName);
        }
    }
}
#endif
