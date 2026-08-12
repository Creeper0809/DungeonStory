using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(
    menuName = "DungeonStory/Medical/Anatomy Profile",
    order = 0)]
public sealed class AnatomyProfileSO : ScriptableObject
{
    public const string ResourcePath = "SO/Medical/Anatomy";

    [SerializeField] private string profileId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private string anatomyFamily = "humanoid";
    [SerializeField] private List<string> speciesIds = new List<string>();
    [SerializeField] private List<AnatomyNodeDefinition> nodes =
        new List<AnatomyNodeDefinition>();
    [SerializeField] private List<AnatomyFunctionalCapacityNotApplicable> notApplicableCapacities =
        new List<AnatomyFunctionalCapacityNotApplicable>();

    public string ProfileId => string.IsNullOrWhiteSpace(profileId)
        ? name
        : profileId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? ProfileId
        : displayName.Trim();
    public string AnatomyFamily => string.IsNullOrWhiteSpace(anatomyFamily)
        ? ProfileId
        : anatomyFamily.Trim();
    public IReadOnlyList<string> SpeciesIds => speciesIds;
    public IReadOnlyList<AnatomyNodeDefinition> Nodes => nodes;
    public IReadOnlyList<AnatomyFunctionalCapacityNotApplicable> NotApplicableCapacities =>
        notApplicableCapacities;

#if UNITY_EDITOR
    public void Configure(
        string id,
        string label,
        string family,
        IEnumerable<string> species,
        IEnumerable<AnatomyNodeDefinition> anatomyNodes,
        IEnumerable<AnatomyFunctionalCapacityNotApplicable> authoredNotApplicable = null)
    {
        profileId = id?.Trim() ?? string.Empty;
        displayName = label?.Trim() ?? string.Empty;
        anatomyFamily = family?.Trim() ?? string.Empty;
        speciesIds = (species ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        nodes = (anatomyNodes ?? Array.Empty<AnatomyNodeDefinition>())
            .Where(node => node != null)
            .ToList();
        if (authoredNotApplicable != null)
        {
            notApplicableCapacities = authoredNotApplicable
                .Where(value => value != null)
                .ToList();
        }
    }

    public void ConfigureNotApplicableCapacities(
        IEnumerable<AnatomyFunctionalCapacityNotApplicable> values)
    {
        notApplicableCapacities = (values
                ?? Array.Empty<AnatomyFunctionalCapacityNotApplicable>())
            .Where(value => value != null)
            .ToList();
    }
#endif
}

[Serializable]
public sealed class AnatomyFunctionalCapacityNotApplicable
{
    [SerializeField] private CharacterFunctionalCapacityId capacityId;
    [SerializeField, TextArea] private string reason = string.Empty;

    public AnatomyFunctionalCapacityNotApplicable()
    {
    }

    public AnatomyFunctionalCapacityNotApplicable(
        CharacterFunctionalCapacityId capacityId,
        string reason)
    {
        this.capacityId = capacityId;
        this.reason = reason?.Trim() ?? string.Empty;
    }

    public CharacterFunctionalCapacityId CapacityId => capacityId;
    public string Reason => reason?.Trim() ?? string.Empty;
}
