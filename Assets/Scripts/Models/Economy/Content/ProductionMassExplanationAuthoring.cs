using System;
using UnityEngine;

/// <summary>
/// Opaque, data-authored recipe capability envelope. ProductionRecipeSO owns
/// the data; a registered capability owns its payload grammar and semantics.
/// </summary>
[Serializable]
public sealed class ProductionMassExplanationAuthoring
{
    [SerializeField] private string capabilityId = string.Empty;
    [SerializeField] private int contractVersion;
    [SerializeField] private string canonicalPayload = string.Empty;

    public string CapabilityId => capabilityId ?? string.Empty;
    public int ContractVersion => contractVersion;
    public string CanonicalPayload => canonicalPayload ?? string.Empty;
    public bool IsEmpty => CapabilityId.Length == 0
        && ContractVersion == 0
        && CanonicalPayload.Length == 0;

    public ProductionMassExplanationAuthoringSnapshot Capture()
    {
        if (IsEmpty)
            return ProductionMassExplanationAuthoringSnapshot.Empty;
        return new ProductionMassExplanationAuthoringSnapshot(
            CapabilityId,
            ContractVersion,
            CanonicalPayload);
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

public readonly struct ProductionMassExplanationAuthoringSnapshot
{
    public static readonly ProductionMassExplanationAuthoringSnapshot Empty =
        new(string.Empty, 0, string.Empty, allowEmpty: true);

    public ProductionMassExplanationAuthoringSnapshot(
        string capabilityId,
        int contractVersion,
        string canonicalPayload)
        : this(capabilityId, contractVersion, canonicalPayload, allowEmpty: false)
    {
    }

    private ProductionMassExplanationAuthoringSnapshot(
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
            || !string.Equals(
                CanonicalPayload,
                CanonicalPayload.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production mass-explanation authoring is noncanonical.");
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
