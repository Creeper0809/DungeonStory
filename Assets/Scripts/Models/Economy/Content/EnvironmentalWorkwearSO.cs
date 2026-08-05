using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    menuName = "DungeonStory/Environment/Workwear",
    order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EnvironmentalWorkwearSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Environment/Workwear";

    [SerializeField] private string workwearId = string.Empty;
    [SerializeField] private string itemDefinitionId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private string[] allowedSpecies = Array.Empty<string>();
    [SerializeField] private ThermalProtectionProfile protection =
        new ThermalProtectionProfile();
    [SerializeField] private string requiredResearchId = string.Empty;

    public string WorkwearId => workwearId?.Trim() ?? string.Empty;
    public string ItemDefinitionId => itemDefinitionId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public ThermalProtectionProfile Protection =>
        protection ?? new ThermalProtectionProfile();
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

    public DungeonStory.Environment.EnvironmentalWorkwearDefinitionSnapshot
        CreateSnapshot()
    {
        ThermalProtectionProfile thermal = Protection;
        return new DungeonStory.Environment.EnvironmentalWorkwearDefinitionSnapshot(
            WorkwearId,
            ItemDefinitionId,
            DisplayName,
            Description,
            allowedSpecies?.ToArray() ?? Array.Empty<string>(),
            new DungeonStory.Environment.ThermalProtectionSnapshot(
                thermal.comfortMinimumOffset,
                thermal.comfortMaximumOffset,
                thermal.safeMinimumOffset,
                thermal.safeMaximumOffset,
                thermal.coldExposureMultiplier,
                thermal.heatExposureMultiplier),
            RequiredResearchId);
    }

    public void Configure(
        string stableId,
        string physicalItemId,
        string name,
        string details,
        IEnumerable<string> species,
        ThermalProtectionProfile thermalProtection,
        string researchId)
    {
        workwearId = stableId?.Trim() ?? string.Empty;
        itemDefinitionId = physicalItemId?.Trim() ?? string.Empty;
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
    bool TryGetByItemDefinitionId(
        string itemDefinitionId,
        out EnvironmentalWorkwearSO definition);
}

public sealed class ResourceEnvironmentalWorkwearCatalog :
    IEnvironmentalWorkwearCatalog
{
    private readonly IReadOnlyList<EnvironmentalWorkwearSO> definitions;
    private readonly Dictionary<string, EnvironmentalWorkwearSO> byId;
    private readonly Dictionary<string, EnvironmentalWorkwearSO> byItemDefinitionId;

    public ResourceEnvironmentalWorkwearCatalog(
        IGameContentDefinitionSource content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        definitions = content
            .GetAll<EnvironmentalWorkwearSO>()
            .Where(candidate => candidate != null)
            .OrderBy(candidate => candidate.WorkwearId, StringComparer.Ordinal)
            .ToArray();
        EnvironmentalWorkwearSO invalid = definitions.FirstOrDefault(candidate =>
            string.IsNullOrWhiteSpace(candidate.WorkwearId)
            || string.IsNullOrWhiteSpace(candidate.ItemDefinitionId));
        if (invalid != null)
        {
            throw new InvalidOperationException(
                $"Environmental workwear '{invalid.name}' requires stable workwear and physical item IDs.");
        }
        byId = definitions.ToDictionary(
            candidate => candidate.WorkwearId,
            candidate => candidate,
            StringComparer.Ordinal);
        byItemDefinitionId = definitions.ToDictionary(
            candidate => candidate.ItemDefinitionId,
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

    public bool TryGetByItemDefinitionId(
        string itemDefinitionId,
        out EnvironmentalWorkwearSO definition)
    {
        return byItemDefinitionId.TryGetValue(
            itemDefinitionId?.Trim() ?? string.Empty,
            out definition);
    }
}
