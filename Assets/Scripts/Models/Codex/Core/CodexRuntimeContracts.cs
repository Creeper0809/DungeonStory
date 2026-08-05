using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;

public sealed class CodexEntryObservationSnapshot
{
    public CodexEntryObservationSnapshot(
        CodexEntryCategory category,
        string entryId,
        string title,
        IEnumerable<CodexInfoLine> lines)
    {
        Category = category;
        EntryId = CodexIdentity.Require(entryId, nameof(entryId));
        Title = string.IsNullOrWhiteSpace(title) ? EntryId : title.Trim();
        Lines = (lines ?? Array.Empty<CodexInfoLine>())
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .GroupBy(line => line.Text.Trim(), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(line => line.Text, StringComparer.Ordinal)
            .ToArray();
    }

    public CodexEntryCategory Category { get; }
    public string EntryId { get; }
    public string Title { get; }
    public IReadOnlyList<CodexInfoLine> Lines { get; }
}

public sealed class CodexCharacterObservationSnapshot
{
    public CodexCharacterObservationSnapshot(CodexEntryObservationSnapshot entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public CodexEntryObservationSnapshot Entry { get; }
}

public sealed class CodexFacilityObservationSnapshot
{
    public CodexFacilityObservationSnapshot(CodexEntryObservationSnapshot entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public CodexEntryObservationSnapshot Entry { get; }
}

public sealed class CodexInvasionObservationSnapshot
{
    public CodexInvasionObservationSnapshot(
        IEnumerable<CodexFacilityObservationSnapshot> facilities,
        IEnumerable<string> observations)
    {
        Facilities = CanonicalizeFacilities(facilities);
        Observations = CodexTextFormatter.Canonicalize(observations);
    }

    public IReadOnlyList<CodexFacilityObservationSnapshot> Facilities { get; }
    public IReadOnlyList<string> Observations { get; }

    private static IReadOnlyList<CodexFacilityObservationSnapshot> CanonicalizeFacilities(
        IEnumerable<CodexFacilityObservationSnapshot> facilities)
    {
        return (facilities ?? Array.Empty<CodexFacilityObservationSnapshot>())
            .Where(item => item?.Entry != null)
            .GroupBy(item => item.Entry.EntryId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.Entry.EntryId, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class CodexRecipeObservationSnapshot
{
    public CodexRecipeObservationSnapshot(IEnumerable<CodexEntryObservationSnapshot> entries)
    {
        Entries = CodexSnapshotOrdering.Canonicalize(entries);
    }

    public IReadOnlyList<CodexEntryObservationSnapshot> Entries { get; }
}

public sealed class CodexResearchObservationSnapshot
{
    public CodexResearchObservationSnapshot(
        IEnumerable<CodexEntryObservationSnapshot> unlockEntries,
        CodexRecipeObservationSnapshot recipes)
    {
        UnlockEntries = CodexSnapshotOrdering.Canonicalize(unlockEntries);
        Recipes = recipes ?? new CodexRecipeObservationSnapshot(
            Array.Empty<CodexEntryObservationSnapshot>());
    }

    public IReadOnlyList<CodexEntryObservationSnapshot> UnlockEntries { get; }
    public CodexRecipeObservationSnapshot Recipes { get; }
}

public sealed class CodexEvolutionObservationSnapshot
{
    public CodexEvolutionObservationSnapshot(CodexEntryObservationSnapshot entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public CodexEntryObservationSnapshot Entry { get; }
}

public sealed class CodexReferenceSnapshot
{
    public CodexReferenceSnapshot(
        IEnumerable<CodexCharacterObservationSnapshot> characters,
        IEnumerable<CodexFacilityObservationSnapshot> facilities,
        CodexRecipeObservationSnapshot recipes)
    {
        Characters = (characters ?? Array.Empty<CodexCharacterObservationSnapshot>())
            .Where(item => item?.Entry != null)
            .OrderBy(item => item.Entry.EntryId, StringComparer.Ordinal)
            .ToArray();
        Facilities = (facilities ?? Array.Empty<CodexFacilityObservationSnapshot>())
            .Where(item => item?.Entry != null)
            .OrderBy(item => item.Entry.EntryId, StringComparer.Ordinal)
            .ToArray();
        Recipes = recipes ?? new CodexRecipeObservationSnapshot(
            Array.Empty<CodexEntryObservationSnapshot>());
    }

    public IReadOnlyList<CodexCharacterObservationSnapshot> Characters { get; }
    public IReadOnlyList<CodexFacilityObservationSnapshot> Facilities { get; }
    public CodexRecipeObservationSnapshot Recipes { get; }
}

public readonly struct CodexAlertRequest
{
    public CodexAlertRequest(string title, string message, string category)
    {
        Title = title ?? string.Empty;
        Message = message ?? string.Empty;
        Category = category ?? string.Empty;
    }

    public string Title { get; }
    public string Message { get; }
    public string Category { get; }
}

public interface ICodexReferenceSnapshotQueryPort
{
    CodexReferenceSnapshot Capture();
}

public interface ICodexRuntimeApplicationPort
{
    void Bind(CodexRuntime runtime);
    void Unbind(CodexRuntime runtime);
    void PublishUpdated(CodexUpdatedEvent updatedEvent);
    void RaiseAlert(CodexAlertRequest request);
}

internal static class CodexSnapshotOrdering
{
    public static IReadOnlyList<CodexEntryObservationSnapshot> Canonicalize(
        IEnumerable<CodexEntryObservationSnapshot> entries)
    {
        return (entries ?? Array.Empty<CodexEntryObservationSnapshot>())
            .Where(entry => entry != null)
            .GroupBy(
                entry => $"{(int)entry.Category}:{entry.EntryId}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .ToArray();
    }
}

internal static class CodexIdentity
{
    public static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Codex entry ID must not be empty.", parameterName);
        }

        return value.Trim();
    }
}
