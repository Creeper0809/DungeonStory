using System;
using System.Collections.Generic;

/// <summary>
/// Stable protocol IDs. Pattern content is authored in GameDomainContentCatalogSO.
/// </summary>
public static class InvasionIntruderPatternIds
{
    public const string Hunter = "invasion:pattern:hunter";
    public const string Ambusher = "invasion:pattern:ambusher";
    public const string Breaker = "invasion:pattern:breaker";
    public const string Plunderer = "invasion:pattern:plunderer";
    public const string Straggler = "invasion:pattern:straggler";
    public const string Executioner = "invasion:pattern:executioner";
}

public interface IInvasionIntruderPatternDefinitionCatalog
{
    IReadOnlyCollection<InvasionIntruderPatternDefinition> All { get; }
    InvasionIntruderPatternDefinition Default { get; }
    InvasionIntruderPatternDefinition Get(string id);
    InvasionIntruderPatternDefinition Require(string id);
}

public enum InvasionIntruderTargetPreference
{
    Owner,
    DefenseFacility,
    ValuableFacility
}

public sealed class InvasionIntruderPatternDefinition
{
    public InvasionIntruderPatternDefinition(
        string id,
        string title,
        string detail,
        InvasionIntruderTargetPreference targetPreference,
        float directOwnerFocus,
        float facilityDiversionFocus,
        int maxFacilityDamageCount,
        float riskTolerance = 0.55f,
        float routeCommitmentSeconds = 2f,
        float structureDamageMultiplier = 1f,
        params string[] preferredFacilityFamilyIds)
    {
        this.id = id?.Trim() ?? string.Empty;
        this.title = title?.Trim() ?? string.Empty;
        this.detail = detail?.Trim() ?? string.Empty;
        this.targetPreference = targetPreference;
        this.directOwnerFocus = UnityEngine.Mathf.Clamp01(directOwnerFocus);
        this.facilityDiversionFocus = UnityEngine.Mathf.Clamp01(facilityDiversionFocus);
        this.maxFacilityDamageCount = UnityEngine.Mathf.Max(0, maxFacilityDamageCount);
        this.riskTolerance = UnityEngine.Mathf.Clamp01(riskTolerance);
        this.routeCommitmentSeconds = UnityEngine.Mathf.Max(0f, routeCommitmentSeconds);
        this.structureDamageMultiplier = UnityEngine.Mathf.Max(0.01f, structureDamageMultiplier);
        this.preferredFacilityFamilyIds = Array.AsReadOnly(
            preferredFacilityFamilyIds ?? Array.Empty<string>());
    }

    public string id { get; }
    public string title { get; }
    public string detail { get; }
    public InvasionIntruderTargetPreference targetPreference { get; }
    public float directOwnerFocus { get; }
    public float facilityDiversionFocus { get; }
    public int maxFacilityDamageCount { get; }
    public float riskTolerance { get; }
    public float routeCommitmentSeconds { get; }
    public float structureDamageMultiplier { get; }
    public IReadOnlyList<string> preferredFacilityFamilyIds { get; }
}
