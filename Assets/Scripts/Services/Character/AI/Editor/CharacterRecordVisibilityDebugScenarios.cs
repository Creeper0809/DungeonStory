#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled log visibility fixture. No model response or replacement text is supplied.
public static class CharacterRecordVisibilityDebugScenarios
{
    public static void VerifyForBatch()
    {
        VerifySynchronousRejection();
        VerifyPendingRelease();
        VerifyStaleExpectedLine();
        VerifyPendingSaveVisibility();
    }

    private static void VerifySynchronousRejection()
    {
        GameObject owner = new GameObject("CharacterRecordRejectedFixture");
        try
        {
            var service = new DeferredNarrativeService(false);
            CharacterLog log = owner.AddComponent<CharacterLog>();
            log.ConstructCharacterLog(service);
            CharacterActivityEvent activity = CreateActivity("기록 원문 하나");
            log.AddActivity(activity);
            Require(service.RequestCount == 1, "The original request was not attempted.");
            RequireVisibleOriginal(log, service.Entry, activity);
            Require(!log.TryEndNarrativeDisplayWait(service.Entry.EntryId, service.Entry.DisplayLine),
                "An already released entry must not be released twice.");
        }
        finally { Object.DestroyImmediate(owner); }
    }

    private static void VerifyPendingRelease()
    {
        GameObject owner = new GameObject("CharacterRecordPendingFixture");
        try
        {
            var service = new DeferredNarrativeService(true);
            CharacterLog log = owner.AddComponent<CharacterLog>();
            log.ConstructCharacterLog(service);
            CharacterActivityEvent activity = CreateActivity("기록 원문 둘");
            log.AddActivity(activity);
            Require(service.RequestCount == 1,
                "An accepted request must remain pending.");
            RequireVisibleOriginal(log, service.Entry, activity);
            RequireActivityOriginal(log, service.Entry, activity);
            Require(log.TryEndNarrativeDisplayWait(service.Entry.EntryId, service.Entry.DisplayLine),
                "The exact pending entry must be released.");
            RequireVisibleOriginal(log, service.Entry, activity);
            Require(!log.TryEndNarrativeDisplayWait(service.Entry.EntryId, service.Entry.DisplayLine),
                "A completed entry must reject a duplicate release.");
        }
        finally { Object.DestroyImmediate(owner); }
    }

    private static void VerifyStaleExpectedLine()
    {
        GameObject owner = new GameObject("CharacterRecordStaleFixture");
        try
        {
            var service = new DeferredNarrativeService(true);
            CharacterLog log = owner.AddComponent<CharacterLog>();
            log.ConstructCharacterLog(service);
            CharacterActivityEvent activity = CreateActivity("기록 원문 셋");
            log.AddActivity(activity);
            Require(service.RequestCount == 1,
                "The entry must initially remain pending.");
            RequireVisibleOriginal(log, service.Entry, activity);
            Require(!log.TryEndNarrativeDisplayWait(service.Entry.EntryId, "다른 문장")
                    && log.Entries.Count == 1 && log.Entries[0] == activity.FactText,
                "A stale expected line must not change the original entry.");
            Require(!log.TryEndNarrativeDisplayWait(service.Entry.EntryId + 100, service.Entry.DisplayLine)
                    && log.Entries.Count == 1 && log.Entries[0] == activity.FactText,
                "A stale entry identity must not change the original entry.");
            RequireActivityOriginal(log, service.Entry, activity);
            Require(log.TryEndNarrativeDisplayWait(service.Entry.EntryId, service.Entry.DisplayLine),
                "The exact line must still release the entry after stale attempts.");
            RequireVisibleOriginal(log, service.Entry, activity);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    private static void VerifyPendingSaveVisibility()
    {
        GameObject owner = new GameObject("CharacterRecordSaveFixture");
        GameObject restoredOwner = new GameObject("CharacterRecordRestoredFixture");
        try
        {
            var service = new DeferredNarrativeService(true);
            CharacterLog log = owner.AddComponent<CharacterLog>();
            log.ConstructCharacterLog(service);
            CharacterActivityEvent activity = CreateActivity("저장할 원문");
            log.AddActivity(activity);
            CharacterLogEntry pending = service.Entry;
            CharacterActivityEvent hidden = CharacterActivityEvent.Create(
                CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
                "숨겨진 기록", actionId: "work:hidden-visibility-fixture",
                narrativeEligible: true, visibleToPlayer: false);
            log.AddActivity(hidden);
            CharacterLogEntry hiddenPending = service.Entry;
            Require(service.RequestCount == 2 && log.Entries.Count == 1
                    && log.Entries[0] == activity.FactText,
                "Pending facts must be saved while hidden entries stay excluded.");
            var saved = new List<string>(log.Entries);
            CharacterLog restored = restoredOwner.AddComponent<CharacterLog>();
            restored.RestoreVisibleEntries(saved);
            Require(restored.Entries.Count == 1 && restored.Entries[0] == activity.FactText
                    && restored.ActivityEntries.Count == 1
                    && restored.ActivityEntries[0].FactText == activity.FactText,
                "A save and restore must preserve the visible original fact only.");
            Require(log.ActivityEntries.Count == 2
                    && ReferenceEquals(log.ActivityEntries[0], activity)
                    && ReferenceEquals(log.ActivityEntries[1], hidden)
                    && pending.OriginalMessage == activity.FactText
                    && hiddenPending.OriginalMessage == hidden.FactText,
                "Saving the pending view must not change source activities or text.");
            Require(log.TryEndNarrativeDisplayWait(pending.EntryId, pending.DisplayLine)
                    && log.TryEndNarrativeDisplayWait(hiddenPending.EntryId, hiddenPending.DisplayLine)
                    && log.Entries.Count == 1 && log.Entries[0] == activity.FactText,
                "Releasing requests must preserve hidden and visible entry policy.");
        }
        finally
        {
            Object.DestroyImmediate(restoredOwner);
            Object.DestroyImmediate(owner);
        }
    }

    private static CharacterActivityEvent CreateActivity(string fact) =>
        CharacterActivityEvent.Create(CharacterActivityKinds.Work,
            CharacterActivityOutcomes.Completed, fact,
            actionId: "work:visibility-fixture", narrativeEligible: true);

    private static void RequireVisibleOriginal(CharacterLog log, CharacterLogEntry entry,
        CharacterActivityEvent activity)
    {
        Require(log.Entries.Count == 1 && log.Entries[0] == activity.FactText,
            "Visible text must be the unchanged source fact.");
        Require(log.TryGetVisibleDisplayLine(entry.EntryId, out string line) && line == activity.FactText,
            "The entry identity must resolve to the unchanged source fact.");
        RequireActivityOriginal(log, entry, activity);
    }

    private static void RequireActivityOriginal(CharacterLog log, CharacterLogEntry entry,
        CharacterActivityEvent activity)
    {
        Require(entry.OriginalMessage == activity.FactText && entry.DisplayLine == activity.FactText
                && log.ActivityEntries.Count == 1 && ReferenceEquals(log.ActivityEntries[0], activity)
                && log.ActivityEntries[0].FactText == activity.FactText,
            "The activity and its source text must remain unchanged.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class DeferredNarrativeService : ICharacterLogNarrativeService
    {
        private readonly bool accepted;
        public DeferredNarrativeService(bool accepted) { this.accepted = accepted; }
        public int RequestCount { get; private set; }
        public CharacterLogEntry Entry { get; private set; }
        public bool TryBuildImmediateLine(CharacterLog log, CharacterLogEntry entry, out string line)
        { line = string.Empty; return false; }
        public bool ShouldDeferDisplay(CharacterLogEntry entry) => true;
        public bool RequestNarrative(CharacterLog log, CharacterLogEntry entry)
        { RequestCount++; Entry = entry; return accepted; }
        public bool TryApplyFallback(CharacterLog log, CharacterLogEntry entry) => false;
    }
}
#endif
