using System;
using System.Collections.Generic;
using UnityEngine;

public delegate bool FacilityAnchorFallbackResolver(
    BuildableObject building,
    Vector3 fromWorld,
    out Vector3 worldPosition);

public sealed class FacilityAnchorPurposeDefinition
{
    public FacilityAnchorPurposeDefinition(string purposeId, FacilityAnchorFallbackResolver fallbackResolver)
    {
        PurposeId = string.IsNullOrWhiteSpace(purposeId)
            ? throw new ArgumentException("Anchor purpose ID is required.", nameof(purposeId))
            : purposeId;
        FallbackResolver = fallbackResolver
            ?? throw new ArgumentNullException(nameof(fallbackResolver));
    }

    public string PurposeId { get; }
    public FacilityAnchorFallbackResolver FallbackResolver { get; }
}

public static class FacilityAnchorPurposeCatalog
{
    public static IReadOnlyList<FacilityAnchorPurposeDefinition> All =>
        new List<FacilityAnchorPurposeDefinition>(Definitions.Values).AsReadOnly();
    private static readonly Dictionary<string, FacilityAnchorPurposeDefinition> Definitions =
        new Dictionary<string, FacilityAnchorPurposeDefinition>(StringComparer.Ordinal);

    static FacilityAnchorPurposeCatalog()
    {
        ResetBuiltIns();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForSubsystemRegistration()
    {
        ResetBuiltIns();
    }

    public static bool Register(FacilityAnchorPurposeDefinition definition, bool replace = false)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (!replace && Definitions.ContainsKey(definition.PurposeId))
        {
            return false;
        }

        Definitions[definition.PurposeId] = definition;
        return true;
    }

    public static bool Unregister(string purposeId)
    {
        return !string.IsNullOrWhiteSpace(purposeId) && Definitions.Remove(purposeId);
    }

    public static bool TryGet(string purposeId, out FacilityAnchorPurposeDefinition definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(purposeId)
            && Definitions.TryGetValue(purposeId, out definition);
    }

    private static void ResetBuiltIns()
    {
        Definitions.Clear();
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Use, ResolveOccupiedAnchor));
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Work, ResolveWorkAnchor));
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Checkout, ResolveCheckoutAnchor));
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Exit, ResolveOccupiedAnchor));
    }

    private static bool ResolveOccupiedAnchor(BuildableObject building, Vector3 fromWorld, out Vector3 worldPosition)
    {
        return building.TryGetFacilityOccupiedWorldPosition(fromWorld, out worldPosition)
            || building.TryGetHorizontalFootprintAnchorWorldPosition(0.5f, out worldPosition);
    }

    private static bool ResolveWorkAnchor(BuildableObject building, Vector3 fromWorld, out Vector3 worldPosition)
    {
        return building.TryGetHorizontalFootprintAnchorWorldPosition(0.85f, out worldPosition);
    }

    private static bool ResolveCheckoutAnchor(BuildableObject building, Vector3 fromWorld, out Vector3 worldPosition)
    {
        return building.TryGetHorizontalFootprintAnchorWorldPosition(0.75f, out worldPosition);
    }
}
