using System;
using UnityEngine;

/// <summary>
/// Data-authored economic allocation policy for recipes with more than one
/// physical output. The payload grammar and allocation semantics are owned by
/// a registered capability, not by ProductionRecipeSO or recipe IDs.
/// </summary>
[Serializable]
public sealed class ProductionOutputCostAllocationAuthoring
{
    [SerializeField] private string capabilityId = string.Empty;
    [SerializeField] private int contractVersion;
    [SerializeField] private string canonicalPayload = string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(capabilityId)
        && contractVersion == 0
        && string.IsNullOrEmpty(canonicalPayload);

    public ProductionOutputCostAllocationAuthoringSnapshot Capture()
    {
        if (IsEmpty)
            return ProductionOutputCostAllocationAuthoringSnapshot.Empty;
        return new ProductionOutputCostAllocationAuthoringSnapshot(
            capabilityId,
            contractVersion,
            canonicalPayload);
    }

#if UNITY_EDITOR
    public void Configure(
        string authoredCapabilityId,
        int authoredContractVersion,
        string authoredCanonicalPayload)
    {
        capabilityId = authoredCapabilityId ?? string.Empty;
        contractVersion = authoredContractVersion;
        canonicalPayload = authoredCanonicalPayload ?? string.Empty;
        _ = Capture();
    }
#endif
}

public readonly struct ProductionOutputCostAllocationAuthoringSnapshot
{
    public static readonly ProductionOutputCostAllocationAuthoringSnapshot Empty =
        new(string.Empty, 0, string.Empty, allowEmpty: true);

    public ProductionOutputCostAllocationAuthoringSnapshot(
        string capabilityId,
        int contractVersion,
        string canonicalPayload)
        : this(capabilityId, contractVersion, canonicalPayload, allowEmpty: false)
    {
    }

    private ProductionOutputCostAllocationAuthoringSnapshot(
        string capabilityId,
        int contractVersion,
        string canonicalPayload,
        bool allowEmpty)
    {
        CapabilityId = capabilityId ?? string.Empty;
        ContractVersion = contractVersion;
        CanonicalPayload = canonicalPayload ?? string.Empty;
        if (allowEmpty)
            return;
        if (!IsCanonicalToken(CapabilityId)
            || ContractVersion <= 0
            || string.IsNullOrWhiteSpace(CanonicalPayload)
            || !string.Equals(CanonicalPayload, CanonicalPayload.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production output-cost allocation authoring is noncanonical.");
        }
    }

    public string CapabilityId { get; }
    public int ContractVersion { get; }
    public string CanonicalPayload { get; }
    public bool IsEmpty => CapabilityId.Length == 0;

    private static bool IsCanonicalToken(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return false;
        foreach (char character in value)
        {
            if (!(character is >= 'a' and <= 'z')
                && !(character is >= '0' and <= '9')
                && character != '-'
                && character != ':'
                && character != '.')
                return false;
        }
        return true;
    }
}
