#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class DungeonSpaceFactRecoveryDebugScenarios
{
    public static string Run()
    {
        VerifyLostResponse(false);
        VerifyLostResponse(true);
        using var missing = new Fixture(null, null, subscribe: false);
        Grid original = missing.Grid;
        Require(!missing.Runtime.TryReconcileNewRunTierZero(out _, out _)
            && ReferenceEquals(missing.Grid, original), "missing observer changed the grid");
        return "space-observer/missing-observer/lost-before-and-after-commit/frozen-grid-json/retry-dedup/tamper=PASS";
    }

    private static void VerifyLostResponse(bool commitFirst)
    {
        using var source = new Fixture(null, null, uncertain: true, commitFirst: commitFirst);
        Require(source.Runtime.TryReconcileNewRunTierZero(out var applied, out _)
            && applied.Changed && applied.PublicationPending && source.Publications == 1,
            "uncertain expansion did not preserve the applied grid and pending fact");
        var pending = source.Grid.CaptureSpaceExpansionPublication();
        var savedWorld = new ModularFacilityWorldSaveData
        {
            gridWidth = source.Grid.width, gridHeight = source.Grid.height,
            gridCells = source.Grid.GetCells().Select(ModularFacilityGridCellSaveData.From).ToList(),
            spaceExpansionPublication = pending
        };
        string json = ModularFacilityWorldSaveCodec.Serialize(savedWorld);
        savedWorld = ModularFacilityWorldSaveCodec.Deserialize(json);
        DungeonSpaceExpandedFact.ValidateSavedPublication(savedWorld);
        var savedLedger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
            JsonUtility.ToJson(source.Ledger.CaptureGameplayOutcomes()));
        DungeonSpaceExpansionFactObservation.ValidatePending(pending, savedLedger);
        var raw = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)raw["spaceExpansionPublication"]).Remove("previousColumns");
        ExpectRejected(() => ModularFacilityWorldSaveCodec.Deserialize(raw.ToString()), "missing frozen fact field");
        savedWorld.spaceExpansionPublication.currentWidth++;
        ExpectRejected(() => DungeonSpaceExpandedFact.ValidateSavedPublication(savedWorld), "torn grid/fact");
        savedWorld.spaceExpansionPublication.currentWidth--;

        Grid restoredGrid = ModularFacilityWorldSaveService.CreateGridFromSnapshot(source.Grid, savedWorld);
        using var restored = new Fixture(restoredGrid, savedLedger);
        restored.Runtime.OnRestoreCompleted();
        restored.Runtime.Tick();
        Require(!restored.Grid.CaptureSpaceExpansionPublication().active && restored.Publications == 0,
            "restore did not replay the pending fact independently of grid expansion");
        var key = DungeonSpaceExpansionOutcomeIds.ResultKey(DungeonSpaceExpansionCatalog.TierZeroInitializationId, 29);
        Require(restored.Ledger.TryGetResultIdentity(key, out var identity)
            && identity.State >= GameplayOutcomeReplayState.Committed,
            "recovered expansion has no committed identity");
        long sequence = restored.Ledger.CaptureGameplayOutcomes().nextSequence;
        Require(restored.Runtime.TryReconcileNewRunTierZero(out var repeated, out _)
            && !repeated.Changed && restored.Publications == 0
            && restored.Ledger.CaptureGameplayOutcomes().nextSequence == sequence,
            "same target duplicated grid or observer result");
        var confirmed = restored.Ledger.CaptureGameplayOutcomes();
        DungeonSpaceExpansionFactObservation.ValidatePending(pending, confirmed);
        var altered = pending.Clone();
        altered.projectName += "-altered";
        ExpectRejected(() => DungeonSpaceExpansionFactObservation.ValidatePending(altered, confirmed),
            "canonical evidence tampering");
        Require(source.Runtime.TryRecoverPendingPublication(out _)
            && !source.Grid.CaptureSpaceExpansionPublication().active && source.Publications == 1,
            "same-process retry reapplied or lost the expansion");
    }

    private static void ExpectRejected(Action action, string name)
    {
        try { action(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected rejection: " + name);
    }

    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }

    private sealed class Fixture : IGridSystemProvider, IGridSystemPublisher, IDisposable
    {
        private readonly DomainFactDispatcher dispatcher = new();
        private readonly GameEventBus events = new();
        public Fixture(Grid grid, GameplayOutcomeLedgerSaveData saved, bool subscribe = true,
            bool uncertain = false, bool commitFirst = false)
        {
            Grid = grid ?? CreateSeed();
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] {
                    new DungeonSpaceExpansionOutcomeDescriptor(new DungeonStory.Narrative.Korean.KoreanJosaFormatter()),
                    new ResearchWorkOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new DungeonSpaceExpansionOutcomeAdapter(), new ResearchWorkOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(registry, GameplayOutcomeBufferLimits.Default);
            if (saved != null)
            {
                var candidate = Ledger.PrepareGameplayOutcomeRestore(saved);
                Ledger.BeginRestoreCandidate();
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, events);
            if (subscribe)
                dispatcher.Subscribe("space-recovery-test", new GameplayDomainFactObserver<
                    DungeonSpaceExpandedFact, DungeonSpaceExpansionOutcomeReceipt>(
                    fact => DungeonSpaceExpansionFactObservation.Capture(fact, Ledger),
                    registry, Ledger, recorder, new GameplayDomainFactCommitGroup(recorder, Ledger)));
            Runtime = new DungeonSpaceExpansionRuntime(events, this, this, EditorFixedGameCalendar.Instance,
                uncertain ? new LostResponsePublisher(dispatcher, commitFirst) : dispatcher,
                dispatcher, new EditorBlueprintResearchStateService());
        }
        public GridSystemManager Manager => null;
        public Grid Grid { get; private set; }
        public GameplayOutcomeLedger Ledger { get; }
        public DungeonSpaceExpansionRuntime Runtime { get; }
        public int Publications { get; private set; }
        public bool TryGetGrid(out Grid grid) { grid = Grid; return true; }
        public bool TryGetManager(out GridSystemManager manager) { manager = null; return false; }
        public bool TryPublishGrid(Grid expectedCurrent, Grid replacement, out string failureReason)
        {
            if (!ReferenceEquals(expectedCurrent, Grid)) { failureReason = "stale-grid"; return false; }
            Grid = replacement; Publications++; failureReason = string.Empty; return true;
        }
        public void CompleteGridPublication() { }
        public void Dispose() { Runtime.Dispose(); dispatcher.Dispose(); events.Clear(); }
        private static Grid CreateSeed()
        {
            Grid grid = new(60, 3);
            foreach (var cell in grid.GetCells()) grid.SetAreaType(cell.Position, GridCellAreaType.BlockedExterior);
            for (int x = 17; x < 44; x++)
                for (int y = 0; y < 3; y++) grid.SetAreaType(new Vector2Int(x, y),
                    x == 17 && y == 0 ? GridCellAreaType.Entrance : GridCellAreaType.DungeonInterior);
            return grid;
        }
    }

    // Fail only the response boundary, without fabricating a receipt or a ledger result.
    private sealed class LostResponsePublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly bool commitFirst;
        public LostResponsePublisher(IDomainFactStagingPublisher inner, bool commitFirst)
        { this.inner = inner; this.commitFirst = commitFirst; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out var actual, out failure)) { stage = null; return false; }
            stage = new LostResponseStage(actual, commitFirst); return true;
        }
    }
    private sealed class LostResponseStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;
        private readonly bool commitFirst;
        public LostResponseStage(IDomainFactStage inner, bool commitFirst) { this.inner = inner; this.commitFirst = commitFirst; }
        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;
        public void Cancel() => inner.Cancel();
        public DomainFactDelivery Commit()
        {
            if (commitFirst) Require(inner.Commit().MayBeCommitted, "injected postcommit must actually commit");
            else inner.Cancel();
            return DomainFactDelivery.Uncertain("injected-space-lost-response");
        }
    }
}
#endif
