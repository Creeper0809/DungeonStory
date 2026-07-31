using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    menuName = "DungeonStory/Environment/Workwear",
    order = 0)]
public sealed class EnvironmentalWorkwearSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Environment/Workwear";

    [SerializeField] private string workwearId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private string[] allowedSpecies = Array.Empty<string>();
    [SerializeField] private ThermalProtectionProfile protection =
        new ThermalProtectionProfile();
    [SerializeField] private string requiredResearchId = string.Empty;

    public string WorkwearId => workwearId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public ThermalProtectionProfile Protection =>
        protection ??= new ThermalProtectionProfile();
    public string RequiredResearchId =>
        requiredResearchId?.Trim() ?? string.Empty;

    public bool AllowsSpecies(string speciesTag)
    {
        return allowedSpecies == null
            || allowedSpecies.Length == 0
            || allowedSpecies.Any(candidate => string.Equals(
                candidate?.Trim(),
                speciesTag?.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    public void Configure(
        string stableId,
        string name,
        string details,
        IEnumerable<string> species,
        ThermalProtectionProfile thermalProtection,
        string researchId)
    {
        workwearId = stableId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        description = details?.Trim() ?? string.Empty;
        allowedSpecies = species?
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
        protection = thermalProtection?.Clone()
            ?? new ThermalProtectionProfile();
        requiredResearchId = researchId?.Trim() ?? string.Empty;
    }
}

public interface IEnvironmentalWorkwearCatalog
{
    IReadOnlyList<EnvironmentalWorkwearSO> Definitions { get; }
    bool TryGet(string workwearId, out EnvironmentalWorkwearSO definition);
}

public sealed class ResourceEnvironmentalWorkwearCatalog :
    IEnvironmentalWorkwearCatalog
{
    private readonly IReadOnlyList<EnvironmentalWorkwearSO> definitions;
    private readonly Dictionary<string, EnvironmentalWorkwearSO> byId;

    public ResourceEnvironmentalWorkwearCatalog(
        IResourcesAssetLoader loader)
    {
        if (loader == null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        definitions = loader
            .LoadAllRequired<EnvironmentalWorkwearSO>(
                EnvironmentalWorkwearSO.ResourcePath)
            .Where(candidate => candidate != null)
            .OrderBy(candidate => candidate.WorkwearId, StringComparer.Ordinal)
            .ToArray();
        byId = definitions.ToDictionary(
            candidate => candidate.WorkwearId,
            candidate => candidate,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<EnvironmentalWorkwearSO> Definitions => definitions;

    public bool TryGet(
        string workwearId,
        out EnvironmentalWorkwearSO definition)
    {
        return byId.TryGetValue(
            workwearId?.Trim() ?? string.Empty,
            out definition);
    }
}

