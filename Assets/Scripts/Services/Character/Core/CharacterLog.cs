using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public class CharacterLog : SerializedMonoBehaviour
{
    [SerializeField]
    [ReadOnly]
    private List<string> log;
    [SerializeField]
    [ReadOnly]
    private List<CharacterActivityEvent> activities;

    private Dictionary<string, int> logTagCounts;
    [NonSerialized] private List<int> logEntryIds;
    [NonSerialized] private HashSet<int> pendingDisplayEntryIds;
    [NonSerialized] private HashSet<int> hiddenEntryIds;
    [NonSerialized] private List<string> visibleEntries;
    [NonSerialized] private IReadOnlyList<string> visibleEntriesView;
    [NonSerialized] private IReadOnlyList<CharacterActivityEvent> activityEntriesView;
    [NonSerialized] private bool visibleEntriesDirty = true;
    [NonSerialized] private int nextEntryId;
    private string lastLogTag;
    private ICharacterLogNarrativeService narrativeService;

    public IReadOnlyList<string> Entries
    {
        get
        {
            EnsureLog();
            RebuildVisibleEntriesIfNeeded();
            return visibleEntriesView;
        }
    }
    public IReadOnlyList<CharacterActivityEvent> ActivityEntries
    {
        get
        {
            EnsureLog();
            return activityEntriesView;
        }
    }
    public event Action<CharacterLogEntry> OnLogAdded;
    public event Action OnLogDisplayChanged;

    [Inject]
    public void ConstructCharacterLog(ICharacterLogNarrativeService narrativeService)
    {
        this.narrativeService = narrativeService
            ?? throw new ArgumentNullException(nameof(narrativeService));
    }

    private void Awake()
    {
        EnsureLog();
    }

    public void Bind()
    {
        EnsureLog();
    }

    public void AddLog(string message)
    {
        AddActivity(CharacterActivityEvent.FreeText(message));
    }

    public void RestoreVisibleEntries(IEnumerable<string> entries)
    {
        EnsureLog();
        log.Clear();
        activities.Clear();
        logTagCounts.Clear();
        logEntryIds.Clear();
        pendingDisplayEntryIds.Clear();
        hiddenEntryIds.Clear();
        visibleEntries.Clear();
        lastLogTag = string.Empty;
        nextEntryId = 0;

        List<string> restored = entries?
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim())
            .TakeLast(80)
            .ToList() ?? new List<string>();
        foreach (string entry in restored)
        {
            log.Add(entry);
            activities.Add(CharacterActivityEvent.FreeText(entry, narrativeEligible: false));
            logEntryIds.Add(++nextEntryId);
        }

        visibleEntriesDirty = true;
        OnLogDisplayChanged?.Invoke();
    }

    public void AddActivity(CharacterActivityEvent activity)
    {
        if (activity == null)
        {
            return;
        }

        EnsureLog();
        activity = activity.WithActor(GetComponent<CharacterActor>());
        string semanticKey = activity.AggregationKey;
        string displayText = CharacterActivityFormatter.Format(activity);
        int count = lastLogTag == semanticKey
            && logTagCounts.TryGetValue(semanticKey, out int currentCount)
            ? currentCount + 1
            : 1;
        logTagCounts[semanticKey] = count;

        string line = count > 1 ? $"{displayText} x{count}" : displayText;
        int entryId;
        if (lastLogTag == semanticKey && log.Count > 0)
        {
            line = FormatRepeatedDisplayLine(log[log.Count - 1], line, count);
            log[log.Count - 1] = line;
            activities[activities.Count - 1] = activity;
            entryId = logEntryIds[logEntryIds.Count - 1];
        }
        else
        {
            log.Add(line);
            activities.Add(activity);
            entryId = ++nextEntryId;
            logEntryIds.Add(entryId);
            lastLogTag = semanticKey;
        }

        if (activity.VisibleToPlayer)
        {
            hiddenEntryIds.Remove(entryId);
        }
        else
        {
            hiddenEntryIds.Add(entryId);
        }

        visibleEntriesDirty = true;

        const int maxLogCount = 80;
        if (log.Count > maxLogCount)
        {
            int removeCount = log.Count - maxLogCount;
            log.RemoveRange(0, removeCount);
            activities.RemoveRange(0, removeCount);
            logEntryIds.RemoveRange(0, removeCount);
        }

        CharacterLogEntry entry = new CharacterLogEntry(
            entryId,
            activity.KindId,
            line,
            count,
            displayText,
            activity);
        bool wasPending = pendingDisplayEntryIds.Contains(entryId);
        string immediateLine = string.Empty;
        bool usedImmediateLine = narrativeService != null
            && narrativeService.TryBuildImmediateLine(this, entry, out immediateLine);
        if (usedImmediateLine)
        {
            line = immediateLine.Trim();
            int index = logEntryIds.IndexOf(entryId);
            if (index >= 0 && index < log.Count)
            {
                log[index] = line;
            }

            entry = new CharacterLogEntry(
                entryId,
                activity.KindId,
                line,
                count,
                displayText,
                activity);
        }

        bool deferDisplay = !usedImmediateLine
            && narrativeService != null
            && narrativeService.ShouldDeferDisplay(entry);
        SetDisplayPending(entryId, deferDisplay || wasPending);
        OnLogAdded?.Invoke(entry);

        if (deferDisplay)
        {
            narrativeService.RequestNarrative(this, entry);
        }
        else if (wasPending)
        {
            narrativeService?.RequestNarrative(this, entry);
        }
    }

    private static string FormatRepeatedDisplayLine(string previousLine, string fallbackLine, int count)
    {
        if (count <= 1)
        {
            return fallbackLine;
        }

        string baseLine = StripRepeatSuffix(previousLine);
        return string.IsNullOrWhiteSpace(baseLine)
            ? fallbackLine
            : $"{baseLine} x{count}";
    }

    private static string StripRepeatSuffix(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        string trimmed = line.Trim();
        int marker = trimmed.LastIndexOf(" x", StringComparison.Ordinal);
        if (marker < 0 || marker >= trimmed.Length - 2)
        {
            return trimmed;
        }

        for (int i = marker + 2; i < trimmed.Length; i++)
        {
            if (!char.IsDigit(trimmed[i]))
            {
                return trimmed;
            }
        }

        return trimmed.Substring(0, marker).TrimEnd();
    }

    public bool TryUpdateDisplayLine(int entryId, string expectedLine, string displayLine)
    {
        return TryUpdateDisplayLine(entryId, expectedLine, string.Empty, displayLine);
    }

    public bool TryUpdateDisplayLine(
        int entryId,
        string expectedLine,
        string alternateExpectedLine,
        string displayLine)
    {
        if (entryId <= 0 || string.IsNullOrWhiteSpace(displayLine))
        {
            return false;
        }

        EnsureLog();
        int index = logEntryIds.IndexOf(entryId);
        if (index < 0
            || index >= log.Count
            || (!string.Equals(log[index], expectedLine, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(alternateExpectedLine)
                    || !string.Equals(log[index], alternateExpectedLine, StringComparison.Ordinal))))
        {
            return false;
        }

        string normalized = displayLine.Trim();
        if (string.Equals(log[index], normalized, StringComparison.Ordinal))
        {
            return false;
        }

        log[index] = normalized;
        SetDisplayPending(entryId, false);
        visibleEntriesDirty = true;
        OnLogDisplayChanged?.Invoke();
        return true;
    }

    public bool TryGetVisibleDisplayLine(int entryId, out string displayLine)
    {
        displayLine = string.Empty;
        if (entryId <= 0)
        {
            return false;
        }

        EnsureLog();
        int index = logEntryIds.IndexOf(entryId);
        if (index < 0
            || index >= log.Count
            || pendingDisplayEntryIds.Contains(entryId))
        {
            return false;
        }

        displayLine = log[index];
        return !string.IsNullOrWhiteSpace(displayLine);
    }

    private void EnsureLog()
    {
        log ??= new List<string>();
        activities ??= new List<CharacterActivityEvent>();
        logTagCounts ??= new Dictionary<string, int>();
        logEntryIds ??= new List<int>();
        pendingDisplayEntryIds ??= new HashSet<int>();
        hiddenEntryIds ??= new HashSet<int>();
        visibleEntries ??= new List<string>();
        visibleEntriesView ??= visibleEntries.AsReadOnly();
        activityEntriesView ??= activities.AsReadOnly();

        if (activities.Count > log.Count)
        {
            activities.RemoveRange(log.Count, activities.Count - log.Count);
        }

        while (activities.Count < log.Count)
        {
            activities.Add(CharacterActivityEvent.FreeText(log[activities.Count]));
        }

        if (logEntryIds.Count > log.Count)
        {
            logEntryIds.RemoveRange(log.Count, logEntryIds.Count - log.Count);
        }

        while (logEntryIds.Count < log.Count)
        {
            logEntryIds.Add(++nextEntryId);
        }

        for (int i = 0; i < logEntryIds.Count; i++)
        {
            nextEntryId = Math.Max(nextEntryId, logEntryIds[i]);
        }

        pendingDisplayEntryIds.RemoveWhere(entryId => !logEntryIds.Contains(entryId));
        hiddenEntryIds.RemoveWhere(entryId => !logEntryIds.Contains(entryId));
    }

    private void SetDisplayPending(int entryId, bool pending)
    {
        bool changed = pending
            ? pendingDisplayEntryIds.Add(entryId)
            : pendingDisplayEntryIds.Remove(entryId);
        if (changed)
        {
            visibleEntriesDirty = true;
        }
    }

    private void RebuildVisibleEntriesIfNeeded()
    {
        if (!visibleEntriesDirty)
        {
            return;
        }

        visibleEntries.Clear();
        for (int i = 0; i < log.Count; i++)
        {
            if (!pendingDisplayEntryIds.Contains(logEntryIds[i])
                && !hiddenEntryIds.Contains(logEntryIds[i]))
            {
                visibleEntries.Add(log[i]);
            }
        }

        visibleEntriesDirty = false;
    }
}

public readonly struct CharacterLogEntry
{
    public int EntryId { get; }
    public string Tag { get; }
    public string DisplayLine { get; }
    public int Count { get; }
    public string OriginalMessage { get; }
    public CharacterActivityEvent Activity { get; }

    public CharacterLogEntry(string tag, string displayLine, int count, string originalMessage)
        : this(
            -1,
            tag,
            displayLine,
            count,
            originalMessage,
            CharacterActivityEvent.FreeText(originalMessage, narrativeEligible: true))
    {
    }

    public CharacterLogEntry(int entryId, string tag, string displayLine, int count, string originalMessage)
        : this(
            entryId,
            tag,
            displayLine,
            count,
            originalMessage,
            CharacterActivityEvent.FreeText(originalMessage, narrativeEligible: true))
    {
    }

    public CharacterLogEntry(
        int entryId,
        string tag,
        string displayLine,
        int count,
        string originalMessage,
        CharacterActivityEvent activity)
    {
        EntryId = entryId;
        Tag = tag;
        DisplayLine = displayLine;
        Count = count;
        OriginalMessage = originalMessage;
        Activity = activity;
    }
}
