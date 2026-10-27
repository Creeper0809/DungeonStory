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

    [ContentIdentity, SerializeField] private string profileId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [ContentIdentity, SerializeField] private string anatomyFamily = "humanoid";
    [ContentReference(ContentReferenceKind.AnatomySpecies, Aliases = "Goblin|LegacyHumanoid", IgnoreCase = true), SerializeField]
    private List<string> speciesIds = new List<string>();
    // Historical bodies retain their injuries/installed parts. This list only
    // authorizes loading that old shape; it is never used for new bodies.
    [ContentReference(ContentReferenceKind.AnatomySpecies), SerializeField]
    private List<string> legacySpeciesIds = new();
    [SerializeField] private List<AnatomyNodeDefinition> nodes =
        new List<AnatomyNodeDefinition>();
    [SerializeField] private List<AnatomyFunctionalCapacityNotApplicable> notApplicableCapacities =
        new List<AnatomyFunctionalCapacityNotApplicable>();
    [SerializeField] private ApparelBodyForm apparelBodyForm;
    [SerializeField] private ApparelSizeClass apparelSize = ApparelSizeClass.Medium;
    [SerializeField] private AnatomyAttachmentPoint apparelBasePoints = (AnatomyAttachmentPoint)16383;
    [SerializeField] private List<AnatomyApparelAttachment> apparelAttachments = new();

    public string ProfileId => profileId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? ProfileId
        : displayName.Trim();
    public string AnatomyFamily => string.IsNullOrWhiteSpace(anatomyFamily)
        ? ProfileId
        : anatomyFamily.Trim();
    public IReadOnlyList<string> SpeciesIds => speciesIds;
    public IReadOnlyList<string> LegacySpeciesIds => legacySpeciesIds;
    public IReadOnlyList<AnatomyNodeDefinition> Nodes => nodes;
    public IReadOnlyList<AnatomyFunctionalCapacityNotApplicable> NotApplicableCapacities =>
        notApplicableCapacities;
    public ApparelBodyForm ApparelBodyForm => apparelBodyForm;
    public ApparelSizeClass ApparelSize => apparelSize;
    public AnatomyAttachmentPoint ApparelBasePoints => apparelBasePoints;
    public IReadOnlyList<AnatomyApparelAttachment> ApparelAttachments => apparelAttachments;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ProfileId))
        {
            errors.Add("Anatomy profile ID is required.");
        }

        foreach (IGrouping<string, AnatomyNodeDefinition> group in
                 (nodes ?? new List<AnatomyNodeDefinition>())
                 .Where(node => node != null
                     && !string.IsNullOrWhiteSpace(node.PairedGroupId))
                 .GroupBy(node => node.PairedGroupId, StringComparer.Ordinal))
        {
            AnatomyNodeDefinition[] members = group.ToArray();
            if (members.Length != 2)
            {
                errors.Add(
                    $"Anatomy pair group '{group.Key}' must contain exactly two nodes.");
                continue;
            }

            if (members.Any(node => string.IsNullOrWhiteSpace(node.NodeId))
                || members.Select(node => node.NodeId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() != 2)
            {
                errors.Add(
                    $"Anatomy pair group '{group.Key}' must contain two distinct non-empty node IDs.");
            }
        }

        return errors;
    }

#if UNITY_EDITOR
    public void ConfigureApparelFit(ApparelBodyForm form, ApparelSizeClass size,
        AnatomyAttachmentPoint basePoints, IEnumerable<AnatomyApparelAttachment> attachments)
    {
        apparelBodyForm = form;
        apparelSize = size;
        apparelBasePoints = basePoints;
        apparelAttachments = (attachments ?? Array.Empty<AnatomyApparelAttachment>()).ToList();
    }

    public void Configure(
        string id,
        string label,
        string family,
        IEnumerable<string> species,
        IEnumerable<AnatomyNodeDefinition> anatomyNodes,
        IEnumerable<AnatomyFunctionalCapacityNotApplicable> authoredNotApplicable = null,
        IEnumerable<string> legacySpecies = null)
    {
        profileId = id?.Trim() ?? string.Empty;
        displayName = label?.Trim() ?? string.Empty;
        anatomyFamily = family?.Trim() ?? string.Empty;
        speciesIds = (species ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        legacySpeciesIds = (legacySpecies ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

public enum AnatomyApparelAttachmentMode { RemoveWhenMissing, AddWhenPresent }

[Serializable]
public sealed class AnatomyApparelAttachment
{
    [ContentReference(ContentReferenceKind.AnatomyNode)]
    public string nodeId;
    public AnatomyAttachmentPoint points;
    public AnatomyApparelAttachmentMode mode;
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
