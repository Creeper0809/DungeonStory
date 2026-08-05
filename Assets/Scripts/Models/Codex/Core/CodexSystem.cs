using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CodexInfoLine
{
    public CodexInfoLine(string text, CodexInfoSource source)
    {
        Text = text ?? string.Empty;
        Source = source;
    }

    public string Text { get; }
    public CodexInfoSource Source { get; }
}
public sealed class CodexEntrySnapshot
{
    public CodexEntryCategory category;
    public string entryId;
    public string title;
    public bool discovered;
    public CodexInfoLine[] lines = Array.Empty<CodexInfoLine>();

    public string ToDisplayText()
    {
        List<string> result = new List<string>
        {
            string.IsNullOrWhiteSpace(title) ? entryId : title
        };
        result.AddRange(lines == null || lines.Length == 0
            ? new[] { "- 정보 없음" }
            : lines.Select(line => $"- {line.Text}"));
        return string.Join("\n", result);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CodexEntryRecord
{
    private readonly List<CodexInfoLine> lines = new List<CodexInfoLine>();
    private readonly HashSet<string> lineSet = new HashSet<string>(StringComparer.Ordinal);
    private readonly IReadOnlyList<CodexInfoLine> linesView;

    public CodexEntryRecord(CodexEntryCategory category, string entryId, string title)
    {
        Category = category;
        EntryId = CodexIdentity.Require(entryId, nameof(entryId));
        Title = string.IsNullOrWhiteSpace(title) ? EntryId : title.Trim();
        Discovered = true;
        linesView = ReadOnlyView.List(lines);
    }

    public CodexEntryCategory Category { get; }
    public string EntryId { get; }
    public string Title { get; private set; }
    public bool Discovered { get; private set; }
    public IReadOnlyList<CodexInfoLine> Lines => linesView;

    public void Rename(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }
    }

    public bool AddInfo(string text, CodexInfoSource source)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.Trim();
        if (!lineSet.Add(normalized))
        {
            return false;
        }

        lines.Add(new CodexInfoLine(normalized, source));
        Discovered = true;
        return true;
    }

    public CodexEntrySnapshot ToSnapshot()
    {
        return new CodexEntrySnapshot
        {
            category = Category,
            entryId = EntryId,
            title = Title,
            discovered = Discovered,
            lines = lines
                .OrderBy(line => line.Source)
                .ThenBy(line => line.Text, StringComparer.Ordinal)
                .ToArray()
        };
    }

    internal CodexEntryRecord DeepClone()
    {
        CodexEntryRecord clone = new CodexEntryRecord(Category, EntryId, Title)
        {
            Discovered = Discovered
        };
        foreach (CodexInfoLine line in lines)
        {
            clone.lines.Add(line);
            clone.lineSet.Add(line.Text);
        }

        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CodexAggregateState
{
    internal readonly Dictionary<string, CodexEntryRecord> Entries =
        new Dictionary<string, CodexEntryRecord>(StringComparer.Ordinal);
    internal readonly IReadOnlyCollection<CodexEntryRecord> EntriesView;

    public CodexAggregateState()
    {
        EntriesView = ReadOnlyView.Collection(Entries.Values);
    }

    public CodexAggregateState DeepClone()
    {
        CodexAggregateState clone = new CodexAggregateState();
        foreach (KeyValuePair<string, CodexEntryRecord> pair in Entries
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            clone.Entries.Add(pair.Key, pair.Value.DeepClone());
        }

        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CodexState
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private CodexAggregateState localState;

    public CodexState()
    {
        localState = new CodexAggregateState();
    }

    internal CodexState(DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyCollection<CodexEntryRecord> Entries => Current.EntriesView;

    public CodexEntryRecord GetOrCreate(
        CodexEntryCategory category,
        string entryId,
        string title)
    {
        string normalizedId = CodexIdentity.Require(entryId, nameof(entryId));
        Dictionary<string, CodexEntryRecord> entries = Writable.Entries;
        string key = GetKey(category, normalizedId);
        if (!entries.TryGetValue(key, out CodexEntryRecord entry))
        {
            entry = new CodexEntryRecord(category, normalizedId, title);
            entries.Add(key, entry);
        }
        else
        {
            entry.Rename(title);
        }

        return entry;
    }

    public bool AddInfo(
        CodexEntryCategory category,
        string entryId,
        string title,
        string info,
        CodexInfoSource source)
    {
        return GetOrCreate(category, entryId, title).AddInfo(info, source);
    }

    public bool HasInfo(CodexEntryCategory category, string entryId, string info)
    {
        string key = GetKey(category, CodexIdentity.Require(entryId, nameof(entryId)));
        return Current.Entries.TryGetValue(key, out CodexEntryRecord entry)
            && entry.Lines.Any(line => string.Equals(line.Text, info, StringComparison.Ordinal));
    }

    public IReadOnlyList<CodexEntrySnapshot> GetSnapshots(CodexEntryCategory category)
    {
        return Current.Entries.Values
            .Where(entry => entry.Category == category)
            .OrderBy(entry => entry.Title, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .Select(entry => entry.ToSnapshot())
            .ToArray();
    }

    public CodexEntrySnapshot GetSnapshot(CodexEntryCategory category, string entryId)
    {
        string key = GetKey(category, CodexIdentity.Require(entryId, nameof(entryId)));
        return Current.Entries.TryGetValue(key, out CodexEntryRecord entry)
            ? entry.ToSnapshot()
            : null;
    }

    internal void ReplaceFrom(CodexState source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ReplaceAggregate(source.Current.DeepClone());
    }

    private CodexAggregateState Current => aggregateRootStore != null
        ? aggregateRootStore.GetOrCreate(() => new CodexAggregateState())
        : localState;

    private CodexAggregateState Writable => aggregateRootStore != null
        ? aggregateRootStore.GetOrCreateWritable(
            () => new CodexAggregateState(),
            aggregate => aggregate.DeepClone())
        : localState;

    private void ReplaceAggregate(CodexAggregateState aggregate)
    {
        if (aggregateRootStore != null)
        {
            aggregateRootStore.Replace(aggregate);
            return;
        }

        localState = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
    }

    private static string GetKey(CodexEntryCategory category, string entryId)
    {
        return $"{(int)category}:{entryId}";
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CodexUpdatedEvent
{
    public CodexUpdatedEvent(CodexEntryCategory category, string entryId)
    {
        this.category = category;
        this.entryId = CodexIdentity.Require(entryId, nameof(entryId));
    }

    public readonly CodexEntryCategory category;
    public readonly string entryId;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CodexService
{
    public const string BreakthroughIntruderId = CodexInvasionRecorder.BreakthroughIntruderId;

    public static void ImportReferenceData(CodexState state, ICodexReferenceImporter importer)
    {
        (importer ?? throw new ArgumentNullException(nameof(importer))).Import(state);
    }

    public static void ObserveCharacter(CodexState state, CodexCharacterObservationSnapshot snapshot)
    {
        CodexObservationRecorder.ObserveCharacter(state, snapshot);
    }

    public static void ObserveFacility(CodexState state, CodexFacilityObservationSnapshot snapshot)
    {
        CodexObservationRecorder.ObserveFacility(state, snapshot);
    }

    public static void RecordInvasion(CodexState state, CodexInvasionObservationSnapshot snapshot)
    {
        CodexInvasionRecorder.Record(state, snapshot);
    }

    public static void RecordResearch(CodexState state, CodexResearchObservationSnapshot snapshot)
    {
        CodexRecipeRecorder.RecordResearch(state, snapshot);
    }

    public static void RecordSynthesis(CodexState state, CodexRecipeObservationSnapshot snapshot)
    {
        CodexRecipeRecorder.RecordSynthesis(state, snapshot);
    }

    public static void RecordEvolution(CodexState state, CodexEvolutionObservationSnapshot snapshot)
    {
        CodexEvolutionRecorder.Record(state, snapshot);
    }

    public static void ImportSynthesisRecipes(CodexState state, CodexRecipeObservationSnapshot snapshot)
    {
        CodexRecipeRecorder.ImportSynthesisRecipes(state, snapshot);
    }

    public static void SeedBreakthroughIntruder(CodexState state)
    {
        CodexInvasionRecorder.SeedBreakthroughIntruder(state);
    }
}
