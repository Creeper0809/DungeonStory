using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
public sealed class AnatomyConditionLexiconEntry
{
    public AnatomyConditionKind condition;
    public string label = string.Empty;
    public string treatmentVerb = string.Empty;
    public string iconId = string.Empty;
    public string vfxId = string.Empty;

    public AnatomyConditionLexiconEntry Clone()
    {
        return new AnatomyConditionLexiconEntry
        {
            condition = condition,
            label = label?.Trim() ?? string.Empty,
            treatmentVerb = treatmentVerb?.Trim() ?? string.Empty,
            iconId = iconId?.Trim() ?? string.Empty,
            vfxId = vfxId?.Trim() ?? string.Empty
        };
    }
}

[CreateAssetMenu(
    menuName = "DungeonStory/Medical/Anatomy Condition Lexicon",
    fileName = "AnatomyConditionLexicon")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnatomyConditionLexiconSO : ScriptableObject
{
    public const string ResourcePath = "SO/Medical/ConditionLexicons";

    [ContentIdentity, SerializeField] private string lexiconId = string.Empty;
    [ContentReference(ContentReferenceKind.AnatomyFamily, IgnoreCase = true), SerializeField]
    private string anatomyFamily = string.Empty;
    [ContentReference(ContentReferenceKind.Species, IgnoreCase = true), SerializeField]
    private List<string> speciesIds = new();
    [SerializeField] private List<AnatomyConditionLexiconEntry> entries = new();

    public string LexiconId => lexiconId?.Trim() ?? string.Empty;
    public string AnatomyFamily => anatomyFamily?.Trim() ?? string.Empty;
    public IReadOnlyList<string> SpeciesIds => speciesIds;
    public IReadOnlyList<AnatomyConditionLexiconEntry> Entries => entries;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(LexiconId))
        {
            errors.Add("Anatomy condition lexicon ID is required.");
        }

        HashSet<AnatomyConditionKind> conditions = new();
        foreach (AnatomyConditionLexiconEntry entry in
                 entries ?? new List<AnatomyConditionLexiconEntry>())
        {
            if (entry != null && !conditions.Add(entry.condition))
            {
                errors.Add(
                    $"Anatomy condition lexicon '{LexiconId}' contains duplicate "
                    + $"condition '{entry.condition}'.");
            }
        }

        return errors;
    }

#if UNITY_EDITOR
    public void Configure(
        string id,
        string family,
        IEnumerable<string> species,
        IEnumerable<AnatomyConditionLexiconEntry> conditionEntries)
    {
        lexiconId = id?.Trim() ?? string.Empty;
        anatomyFamily = family?.Trim() ?? string.Empty;
        speciesIds = (species ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        entries = (conditionEntries ?? Array.Empty<AnatomyConditionLexiconEntry>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .ToList();
    }
#endif
}

public readonly struct AnatomyConditionPresentation
{
    public AnatomyConditionPresentation(
        AnatomyConditionKind condition,
        string label,
        string treatmentVerb,
        string iconId,
        string vfxId)
    {
        Condition = condition;
        Label = label ?? string.Empty;
        TreatmentVerb = treatmentVerb ?? string.Empty;
        IconId = iconId ?? string.Empty;
        VfxId = vfxId ?? string.Empty;
    }

    public AnatomyConditionKind Condition { get; }
    public string Label { get; }
    public string TreatmentVerb { get; }
    public string IconId { get; }
    public string VfxId { get; }
}

public interface IAnatomyConditionLexicon
{
    bool TryResolve(
        string speciesId,
        string anatomyFamily,
        AnatomyConditionKind condition,
        out AnatomyConditionPresentation presentation);
    IReadOnlyList<string> Validate(IAnatomyProfileCatalog anatomyProfiles);
}
