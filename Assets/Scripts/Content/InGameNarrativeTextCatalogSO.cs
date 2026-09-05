using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum InGameNarrativeTextKind
{
    Item = 0,
    ProductionRecipe = 1,
    Facility = 2,
    ExpeditionCard = 3,
    ExpeditionChoice = 4,
    ExpeditionSite = 5
}

[Serializable]
public sealed class InGameNarrativeTextEntry
{
    [SerializeField] private InGameNarrativeTextKind kind;
    [SerializeField] private string stableId = string.Empty;
    [SerializeField, TextArea(2, 5)] private string inGameDescription = string.Empty;
    [SerializeField] private string worldBranchTag = string.Empty;

    public InGameNarrativeTextKind Kind => kind;
    public string StableId => stableId?.Trim() ?? string.Empty;
    public string InGameDescription => inGameDescription?.Trim() ?? string.Empty;
    public string WorldBranchTag => worldBranchTag?.Trim() ?? string.Empty;

#if UNITY_EDITOR
    public void Configure(
        InGameNarrativeTextKind entryKind,
        string id,
        string description,
        string branchTag = "")
    {
        kind = entryKind;
        stableId = id?.Trim() ?? string.Empty;
        inGameDescription = description?.Trim() ?? string.Empty;
        worldBranchTag = branchTag?.Trim() ?? string.Empty;
    }
#endif
}

[CreateAssetMenu(
    fileName = "InGameNarrativeTextCatalog",
    menuName = "DungeonStory/Content/In-Game Narrative Text Catalog",
    order = -90)]
public sealed class InGameNarrativeTextCatalogSO : ScriptableObject
{
    public const string ResourcePath = "SO/InGameNarrativeTextCatalog";

    [SerializeField] private List<InGameNarrativeTextEntry> entries = new();

    public IReadOnlyList<InGameNarrativeTextEntry> Entries =>
        entries ?? (IReadOnlyList<InGameNarrativeTextEntry>)
            Array.Empty<InGameNarrativeTextEntry>();

    public IReadOnlyList<string> ValidateCatalog()
    {
        List<string> errors = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        IReadOnlyList<InGameNarrativeTextEntry> source = Entries;
        for (int index = 0; index < source.Count; index++)
        {
            InGameNarrativeTextEntry entry = source[index];
            if (entry == null)
            {
                errors.Add($"Narrative entry at index {index} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.StableId))
            {
                errors.Add($"Narrative entry at index {index} has no stable ID.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.InGameDescription))
            {
                errors.Add(
                    $"Narrative entry '{entry.Kind}:{entry.StableId}' has no in-game description.");
            }

            string key = ComposeKey(entry.Kind, entry.StableId);
            if (!keys.Add(key))
            {
                errors.Add($"Duplicate narrative entry '{key}'.");
            }
        }

        return errors;
    }

    public static string ComposeKey(
        InGameNarrativeTextKind kind,
        string stableId)
    {
        string normalized = stableId?.Trim() ?? string.Empty;
        return $"{(int)kind}:{normalized}";
    }

    public static string ComposeExpeditionChoiceStableId(
        string cardId,
        string choiceId)
    {
        string card = cardId?.Trim() ?? string.Empty;
        string choice = choiceId?.Trim() ?? string.Empty;
        return $"{card}/{choice}";
    }

    public static string ComposeFacilityStableId(
        string contentDefinitionId,
        int numericId)
    {
        if (!string.IsNullOrWhiteSpace(contentDefinitionId))
        {
            return contentDefinitionId.Trim();
        }
        return numericId > 0 ? $"building:{numericId}" : string.Empty;
    }

#if UNITY_EDITOR
    public void SetEntries(IEnumerable<InGameNarrativeTextEntry> values)
    {
        entries = values?
            .Where(value => value != null)
            .OrderBy(value => value.Kind)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .ToList()
            ?? new List<InGameNarrativeTextEntry>();
    }
#endif
}

public interface IInGameNarrativeTextQuery
{
    string GetRequired(InGameNarrativeTextKind kind, string stableId);
    bool Contains(InGameNarrativeTextKind kind, string stableId);
}

public sealed class ResourceInGameNarrativeTextQuery : IInGameNarrativeTextQuery
{
    private readonly IReadOnlyDictionary<string, string> textByKey;

    public ResourceInGameNarrativeTextQuery()
        : this(Resources.Load<InGameNarrativeTextCatalogSO>(
            InGameNarrativeTextCatalogSO.ResourcePath))
    {
    }

    internal ResourceInGameNarrativeTextQuery(InGameNarrativeTextCatalogSO catalog)
    {
        if (catalog == null)
        {
            throw new InvalidOperationException(
                $"Required in-game narrative catalogue is missing at Resources/{InGameNarrativeTextCatalogSO.ResourcePath}.");
        }

        IReadOnlyList<string> errors = catalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "In-game narrative catalogue is invalid:\n"
                + string.Join("\n", errors));
        }

        textByKey = catalog.Entries.ToDictionary(
            entry => InGameNarrativeTextCatalogSO.ComposeKey(
                entry.Kind,
                entry.StableId),
            entry => entry.InGameDescription,
            StringComparer.Ordinal);
    }

    public string GetRequired(
        InGameNarrativeTextKind kind,
        string stableId)
    {
        string key = InGameNarrativeTextCatalogSO.ComposeKey(kind, stableId);
        if (string.IsNullOrWhiteSpace(stableId)
            || !textByKey.TryGetValue(key, out string value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new KeyNotFoundException(
                $"Required in-game narrative text '{key}' is missing.");
        }

        return value;
    }

    public bool Contains(InGameNarrativeTextKind kind, string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            return false;
        }

        return textByKey.ContainsKey(
            InGameNarrativeTextCatalogSO.ComposeKey(kind, stableId));
    }
}
