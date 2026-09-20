#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class ProductionCommandOutcomeDebugScenarios
{
    private const string FacilityId = "building:qa-production-command";

    public static bool RunAll(bool logSuccess = false)
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new ProductionCommandOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new ProductionCommandOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:production-command-editor"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        ProductionCommandGameplayOutcomeBridge bridge = new(
            recorder,
            ledger,
            new FixedDisplayNames(),
            new FakeCalendar());
        ProductionCommandOutcomeSource source = new(
            ProductionCommandOutcomeKind.SuspensionChanged,
            "production-bill:qa",
            "recipe:qa-ingot",
            "시험 주괴",
            FacilityId,
            7,
            4,
            "running",
            "suspended");

        Require(
            bridge.TryPrepare(
                source,
                1L,
                out IPreparedProductionCommandOutcome prepared,
                out string prepareFailure),
            "production command prepare failed: " + prepareFailure);
        ProductionCommandOutcomeCommitResult committed = bridge.Commit(prepared);
        Require(
            committed.DurablyCommitted,
            "production command commit failed: " + committed.DetailCode);

        GameplayEntityId facility = new(
            ProductionCommandOutcomeIds.FacilityKind,
            FacilityId);
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage facilityPage = ledger.GetForEntity(
            facility,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == 1
            && facilityPage.Items.Count == 1
            && global.Items[0].Exact != null,
            "production command queries did not expose one exact outcome");
        GameplayOutcomeSnapshot exact = global.Items[0].Exact;
        Require(
            exact.outcomeTypeId == ProductionCommandOutcomeIds.Applied.Value
            && exact.ownerRevision == 1L
            && exact.commitRevision == 1L
            && exact.absoluteDay == 12
            && exact.locationX == 7
            && exact.locationY == 4
            && exact.participants.Count == 1
            && exact.participants[0].displayText == "대장간"
            && exact.facts.Any(value =>
                value.factId == ProductionCommandOutcomeIds.CommandKindFact.Value
                && value.value == "suspension-changed")
            && exact.facts.Any(value =>
                value.factId == ProductionCommandOutcomeIds.BeforeValueFact.Value
                && value.value == "running")
            && exact.facts.Any(value =>
                value.factId == ProductionCommandOutcomeIds.AfterValueFact.Value
                && value.value == "suspended"),
            "production command outcome lost its frozen receipt data");

        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(exact.runId),
            exact.sequence);
        Require(
            ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    facility,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("대장간은", StringComparison.Ordinal)
            && view.Text.Contains("시험 주괴 주문", StringComparison.Ordinal)
            && view.Text.Contains("가동 상태 변경", StringComparison.Ordinal),
            "production command Korean projection was not stable");

        Require(
            bridge.TryPrepare(
                source,
                1L,
                out IPreparedProductionCommandOutcome replay,
                out string replayFailure),
            "production command replay prepare failed: " + replayFailure);
        ProductionCommandOutcomeCommitResult replayed = bridge.Commit(replay);
        Require(
            replayed.DurablyCommitted
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "production command replay duplicated or rejected the canonical result");

        ProductionCommandOutcomeSource drifted = new(
            ProductionCommandOutcomeKind.SuspensionChanged,
            "production-bill:qa",
            "recipe:qa-ingot",
            "시험 주괴",
            FacilityId,
            7,
            4,
            "running",
            "running");
        Require(
            !bridge.TryPrepare(
                drifted,
                1L,
                out _,
                out _),
            "same production command result key accepted a drifted receipt");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:production-command-restore"),
            1L);
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Single().Exact.immutablePayloadHash
            == exact.immutablePayloadHash,
            "production command outcome changed across save round trip");

        if (logSuccess)
            Debug.Log("[Production Command Outcome] PASS");
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedDisplayNames : IGameplayOutcomeDisplayNameQuery
    {
        public bool TryGetCurrentName(
            GameplayEntityId entityId,
            out KoreanNameSnapshot name)
        {
            if (entityId.Kind.Equals(ProductionCommandOutcomeIds.FacilityKind)
                && string.Equals(
                    entityId.Value,
                    FacilityId,
                    StringComparison.Ordinal))
            {
                name = new KoreanNameSnapshot(
                    "대장간",
                    "production-command-facility-v1",
                    KoreanPronunciationHint.AutoHangulDisplay(
                        "production-command-pronunciation-v1"),
                    "ko-KR");
                return true;
            }
            name = default;
            return false;
        }
    }

    private sealed class FakeCalendar : IGameCalendar
    {
        public int Day => 12;
        public int Hour => 9;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour)
        {
        }
    }
}
#endif
