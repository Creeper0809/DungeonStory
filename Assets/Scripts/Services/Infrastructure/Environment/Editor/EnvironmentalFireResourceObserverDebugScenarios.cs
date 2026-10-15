#if UNITY_EDITOR
using System;
using DungeonStory.Foundation;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class EnvironmentalFireResourceObserverDebugScenarios
{
    public static string Run()
    {
        using (var missing = new Fixture(subscribe: false))
            Require(!missing.Consume(out _) && missing.Quantity == 2 && missing.Owner.Capture().Count == 0,
                "unobserved fire debit was applied");
        using (var rejected = new Fixture(fault: 1))
            Require(!rejected.Consume(out _) && rejected.Quantity == 2 && rejected.Owner.Capture().Count == 0
                && rejected.Items.Capture().pendingBatchDispositions.Count == 0,
                "known rejection did not roll back physical debit and frozen fact");
        VerifyRecovery(2); VerifyRecovery(3);
        EnvironmentFirePhysicalProductionEntryDebugScenarios.RunAll();
        return "fire-resource/actual-fuel-water-entry/missing-observer/rejection/unknown-before-after/json-recovery/no-second-debit/save-join=PASS";
    }

    private static void VerifyRecovery(int fault)
    {
        using var source = new Fixture(fault: fault);
        Require(source.Consume(out var receipt) && source.Quantity == 0 && source.Owner.Capture().Count == 1,
            "unknown observer response reversed consumption or lost the fact");
        var physical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(source.Items.Capture()));
        var fire = new DungeonEnvironmentalFireSaveData { resourceOperations = source.Owner.Capture().ToList() };
        fire.activeFires.Add(new EnvironmentalFireSaveRecord
        {
            fireId = "fire:resource-test", fuelLossTargetId = source.StackId, fuelLossOperationId = receipt.OperationId,
            fuelLossTargetKind = (int)EnvironmentalFireTargetKind.ItemStack,
            fuelLossQuantity = receipt.Quantity, fuelLossMassGrams = receipt.InputMassGrams, fuelLossCommitId = receipt.CommitId
        });
        fire = JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(fire));
        var ledger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(source.Ledger.CaptureGameplayOutcomes()));
        CheckJoin(fire, physical, ledger, true);
        var changed = JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(fire));
        changed.resourceOperations[0].physical.sourceFacts[0].displayText += "-변조";
        CheckJoin(changed, physical, ledger, false);
        changed = JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(fire));
        changed.resourceOperations[0].publicationKey += "-변조";
        CheckJoin(changed, physical, ledger, false);
        using var restored = new Fixture(physical, ledger, fire);
        string before = Newtonsoft.Json.JsonConvert.SerializeObject(restored.Items.Capture().stacks);
        restored.Owner.OnRestoreCompleted();
        long sequence = restored.Ledger.CaptureGameplayOutcomes().nextSequence;
        restored.Owner.Recover();
        Require(restored.Ledger.CaptureGameplayOutcomes().nextSequence == sequence
            && restored.Quantity == 0 && Newtonsoft.Json.JsonConvert.SerializeObject(restored.Items.Capture().stacks) == before,
            "fire resource recovery consumed or recorded twice");
        CheckJoin(fire, restored.Items.Capture(), restored.Ledger.CaptureGameplayOutcomes(), true);
        restored.Owner.BeginRestoreCandidate(); restored.Owner.Restore(Array.Empty<EnvironmentalFireResourceRecord>());
        Require(restored.Owner.Capture().Count == 1, "staged restore changed live journal");
        restored.Owner.PublishRestoreCandidate(); restored.Owner.RollbackPublishedRestoreCandidate();
        Require(restored.Owner.Capture().Count == 1, "restore rollback lost journal");
        bool duplicateRejected = false;
        try { restored.Owner.Restore(new[] { fire.resourceOperations[0], fire.resourceOperations[0] }); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Require(duplicateRejected && restored.Owner.Capture().Count == 1, "duplicate restore partially changed journal");
        Require(restored.Owner.TryAcknowledge(receipt.CommitId, out var failure)
            && restored.Owner.Capture().Count == 0 && restored.Items.Capture().pendingBatchDispositions.Count == 0,
            "fire resource acknowledgement failed: " + failure);
    }
    private static void CheckJoin(DungeonEnvironmentalFireSaveData fire, DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData ledger, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        EnvironmentalFireResourceFactPreflight.Validate(fire, physical, ledger, report);
        Require(report.Success == expected, "fire resource canonical join returned an unexpected result");
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private sealed class Fixture : IDisposable
    {
        private readonly DomainFactDispatcher facts = new();
        private readonly PhysicalItemBatchDispositionService dispositions;
        private readonly WorldItemRepository repository;
        public readonly WorldItemStackRuntime Items;
        public readonly EnvironmentalFireResourceTransaction Owner;
        public readonly GameplayOutcomeLedger Ledger;
        public readonly string StackId;
        private static readonly Vector2Int Position = new(2, 1);
        public Fixture(DungeonPhysicalItemSaveData saved = null, GameplayOutcomeLedgerSaveData ledger = null,
            DungeonEnvironmentalFireSaveData fire = null, int fault = 0, bool subscribe = true)
        {
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(out var store, out _, out var reservations, out _);
            repository = store;
            if (saved != null)
            { Items.Restore(saved); StackId = saved.pendingBatchDispositions.Single().sourceStackIds.Single(); }
            else StackId = WorldItemRepositoryEditorAccess.AddStack(repository, "item:buffer", 2, WorldItemStackState.Stored, position: Position);
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new FireFuelOutcomeDescriptor(new KoreanJosaFormatter()), new FireWaterOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new EnvironmentalFireFuelOutcomeAdapter(), new EnvironmentalFireWaterOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(registry, GameplayOutcomeBufferLimits.Default);
            if (ledger != null)
            {
                var candidate = Ledger.PrepareGameplayOutcomeRestore(ledger);
                Ledger.BeginRestoreCandidate(); Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate(); Ledger.CompleteRestoreCandidate();
            }
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, new GameEventBus());
            if (subscribe) facts.Subscribe("log:environment.fire-fuel-loss", new GameplayDomainFactObserver<
                EnvironmentalFireFuelLostFact, EnvironmentalFireFuelOutcomeReceipt>(EnvironmentalFireResourceFactMapping.Capture,
                registry, Ledger, recorder, new GameplayDomainFactCommitGroup(recorder, Ledger)));
            dispositions = new PhysicalItemBatchDispositionService(repository, Items.MassQuery,
                EditorNullItemMarkerPresenter.Instance, reservations, Items.CatalogProvider,
                null, null, new GameplayDomainFactDeliveryQuery(recorder, Ledger), null);
            Owner = new EnvironmentalFireResourceTransaction(fault == 0 ? facts : new Fault(facts, fault), facts, dispositions);
            if (fire != null) Owner.Restore(fire.resourceOperations);
        }
        public int Quantity => repository.GetEditorTestQuantity(StackId);
        public bool Consume(out PhysicalItemBatchDispositionReceipt receipt)
        {
            var request = new EnvironmentalFireFuelLossRequest(new EnvironmentalFireTargetRef(EnvironmentalFireTargetKind.ItemStack, StackId), 2);
            return dispositions.TryCommitPending(new[] { new PhysicalItemTransformInput(StackId, 2) }, PhysicalItemDispositionKind.Sink,
                "qa:fire-resource-consume", EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode,
                Owner.PrepareFuel(request, Position, 3), out receipt, out _);
        }
        public void Dispose() => facts.Dispose();
    }
    private sealed class Fault : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly int mode;
        public Fault(IDomainFactStagingPublisher inner, int mode) { this.inner = inner; this.mode = mode; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            stage = null;
            if (!inner.TryStage(fact, out var staged, out failure)) return false;
            stage = new FaultStage(staged, mode); return true;
        }
    }
    private sealed class FaultStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;
        private readonly int mode;
        public FaultStage(IDomainFactStage inner, int mode) { this.inner = inner; this.mode = mode; }
        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;
        public void Cancel() => inner.Cancel();
        public DomainFactDelivery Commit()
        {
            if (mode == 1) { inner.Cancel(); return DomainFactDelivery.Reject("qa-known-rejection"); }
            if (mode == 3) inner.Commit(); else inner.Cancel();
            throw new InvalidOperationException("qa-hidden-fire-resource-response");
        }
    }
}
#endif
