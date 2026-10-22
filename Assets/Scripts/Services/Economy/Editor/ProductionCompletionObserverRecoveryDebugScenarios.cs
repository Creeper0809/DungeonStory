#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionPreparedOutputFullPersistenceDebugScenarios
{
    public static void RunCompletionObserverRecoveryForBatch()
    {
        VerifyProductionCompletedOutcomeFromRuntime();
        Debug.Log("B125_PRODUCTION_COMPLETION_OWNER=PASS");
        RunCompletionCommitIdentityContractsForBatch();
        MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.VerifyProductionLossObserverFocused();
        Require(RunCausalProductionOwnerFamily(), "Production owner-family causal restore failed.");
        Debug.Log("B125_PRODUCTION_COMPLETION_CAUSAL_RESTORE=PASS");
        Debug.Log("B125_PRODUCTION_COMPLETION_OBSERVERS=PASS");
    }

    public static void RunCompletionCommitIdentityContractsForBatch()
    {
        const string shortId = "production-output:bill:1:00000001:output:main:item:1:00000000";
        Require(ProductionCycleFactCapture.CommitIdentity(shortId) == shortId, "An existing short commit identity changed.");
        string longId = shortId + new string('a', 200);
        string projected = ProductionCycleFactCapture.CommitIdentity(longId);
        Require(DomainFactValueSyntax.IsIdentity(projected) && projected.Length == "production-commit-digest:".Length + 64
            && ProductionCycleFactCapture.CommitIdentity(longId) == projected
            && ProductionCycleFactCapture.CommitIdentity(longId + "b") != projected,
            "Long physical commits do not have bounded, deterministic, content-bound observation identities.");
        foreach (string invalid in new[] { "", null, longId + " ", longId + "\n", longId + "한", longId + "\ud800" })
            CommandMustThrow(() => ProductionCycleFactCapture.CommitIdentity(invalid));
        CommandMustThrow(() => ProductionCycleFactCapture.PreparedCommitIdentity("batch:1", "output:main", "commit:wrong"));
        Require(ProductionCycleFactCapture.CommitIdentity(new string('a', 192)) == new string('a', 192)
            && DomainFactValueSyntax.IsIdentity(ProductionCycleFactCapture.CommitIdentity(new string('a', 193))),
            "Commit identity projection does not preserve the exact portable identity boundary.");
        VerifyExactCompletionCommitIdentity();
        ScenarioConfig distillery = new("distillery", "recipe:hallucinogenic-distillate", "drug:hallucinogenic-distillate",
            "building:qa:distillery", "character:qa:distillery", "Assets/Resources/SO/Building/Modular/P14_증류기.asset",
            expectedOutputQuantity: 2, expectedBatchMassGrams: 1800, expectedCapacityGrams: 7200, expectedInitialFreshnessSeconds: null);
        foreach (bool committed in new[] { false, true })
        {
            VerifyCompletionAckRecovery(committed, SawmillScenario);
            VerifyCompletionAckRecovery(committed, distillery, requireLongCommit: true);
        }
        Debug.Log("B125_COMPLETION_COMMIT_IDENTITIES=PASS; bounded observation; unchanged physical keys; malformed rejection; sawmill/distillery ACK before/after; JSON fresh owner/retry exactly once");
    }

    private static void VerifyExactCompletionCommitIdentity()
    {
        const string lineId = "output:recipe:hallucinogenic-distillate/000/main/drug:hallucinogenic-distillate";
        const string itemId = "drug:hallucinogenic-distillate";
        string fingerprint = new('a', 64);
        ProductionRecipeExecutionCorrelation correlation = new(new ProductionBillId("production-bill:exact-completion-identity-contract"),
            1, "recipe:hallucinogenic-distillate", new BuildingInstanceId("building:qa:distillery"));
        string commit = ProductionOutputCommitIdentity.Format(correlation.BillId, 1, lineId, itemId, 0);
        Require(commit.Length > 192, "Exact completion fixture did not exercise a compound unit commit.");
        ProductionRecipeExecutionReceipt receipt = new("action:exact-completion-identity", correlation, "", 0, 0,
            ProductionRecipeExecutionPublicationKind.ExactCapabilityUnits, "production-output-batch-exact:" + fingerprint,
            new[] { commit }, fingerprint, fingerprint,
            new[] { new ProductionRecipeExecutionOutputLineReceipt(lineId, itemId, 1, 900, fingerprint, new[] { commit }) },
            new[] { new ProductionRecipeExecutionPhysicalSliceReceipt(lineId, itemId, "stack:qa:distillery", 1, 900, commit) });
        var fact = ProductionCycleFactCapture.Exact(receipt, "Distillery", "character:qa:distillery", "Worker", 137, 0, 0).Snapshot();
        Require(receipt.Outputs.Single().CommitIds.Single() == commit && receipt.PhysicalSlices.Single().CommitId == commit
            && fact.lines.Single().commitIds.Single() == ProductionCycleFactCapture.CommitIdentity(commit)
            && fact.stacks.Single().sourceCommitId == fact.lines.Single().commitIds.Single(),
            "Exact completion lost its physical authority or projected its line/slice inconsistently.");
        _ = ProductionCompletionFactObservation.Capture(new ProductionCycleCompletedFact(fact));
    }

    private static void VerifyCompletionAckRecovery(bool committed, ScenarioConfig config, bool requireLongCommit = false)
    {
        string json, physicalBefore;
        using (var probe = new CompletionObserverProbe(committed))
        using (var source = new RuntimeGraph("completion-ack-source", 7210, config,
                   completionObservations: probe.Transport, completionRecovery: probe.Transport))
        {
            source.SeedOwnedCurrentFormatBill();
            var record = source.SingleBill;
            source.PrepareWip(record);
            var initial = source.Production.Capture();
            bool boundariesVerified = false;
            probe.Transport.AllowRecovery = false;
            probe.Transport.BeforeCommit = () =>
            {
                CommandMustThrow(() => source.Production.Capture());
                CommandMustThrow(() => source.Production.BuildRestore(initial));
                CommandMustThrow(() => source.Production.SetSuspended(record.billId, true));
                CommandMustThrow(() => source.Production.ExecuteWork(source.WorkerHandle, source.FacilityHandle, record.billId, 1));
                boundariesVerified = true;
            };
            var result = source.Production.ExecuteWork(source.WorkerHandle, source.FacilityHandle, record.billId, float.MaxValue);
            Require(!result.Succeeded && boundariesVerified, "Completion ACK loss or publication re-entry fence was not exercised.");
            var owner = source.Production.Capture();
            var ledger = probe.Ledger.CaptureGameplayOutcomes();
            var pending = owner.bills.Single().completionObservations.Single();
            Require(record.cycleSequence == 1 && record.preparedOutput.phase == ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
                && source.WorldItems.GetAllStacks().Count > 0 && pending.fact.absoluteDay == 137
                && pending.fact.workerDisplayName == source.WorkerHandle.DisplayName
                && pending.fact.facilityDisplayName == source.FacilityHandle.DisplayName,
                "Unconfirmed completion lost its physical batch, first names/day or pending fact: cycle=" + record.cycleSequence
                + ";phase=" + record.preparedOutput.phase + ";stacks=" + source.WorldItems.GetAllStacks().Count
                + ";day=" + pending.fact.absoluteDay + ";worker=" + pending.fact.workerDisplayName
                + ";facility=" + pending.fact.facilityDisplayName);
            ProductionCompletionFactPreflight.Validate(owner, ledger);
            foreach (var line in pending.fact.lines)
            {
                var physical = record.preparedOutput.lines.Single(value => value.outputLineId == line.outputLineId);
                string originalKey = ProductionPreparedOutputIdentity.BuildLineCommitId(record.preparedOutput.batchCommitId, line.outputLineId);
                Require(physical.lineCommitId == originalKey && line.commitIds.Single() == ProductionCycleFactCapture.CommitIdentity(originalKey),
                    "Completion observation changed or lost its exact physical commit authority.");
                if (requireLongCommit)
                    Require(!DomainFactValueSyntax.IsIdentity(originalKey) && originalKey.Length > 192
                        && DomainFactValueSyntax.IsIdentity(line.commitIds.Single()) && line.commitIds.Single() != originalKey,
                        "Distillery fixture did not exercise the overlength physical commit boundary.");
            }
            physicalBefore = CompletionPhysicalSignature(source);
            var raw = JObject.Parse(JsonUtility.ToJson(owner));
            ((JObject)raw["bills"][0]["completionObservations"][0]["fact"]).Remove("absoluteDay");
            CommandMustThrow(() => ProductionBillsSaveSection.ValidateCommandObservationsJson(raw.ToString()));
            var tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
            tampered.bills[0].completionObservations[0].fact.lines[0].quantity++;
            CommandMustThrow(() => ProductionCompletionFactPreflight.Validate(tampered, ledger));
            tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
            tampered.bills[0].completionObservations[0].fact.lines[0].commitIds[0] = "commit:wrong";
            CommandMustThrow(() => ProductionCompletionFactPreflight.Validate(tampered, ledger));
            tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
            tampered.bills[0].preparedOutput.lines.Single(value => value.outputLineId == pending.fact.lines[0].outputLineId).lineCommitId += "a";
            CommandMustThrow(() => ProductionCompletionFactPreflight.Validate(tampered, ledger));
            ProductionCompletionFactPreflight.Validate(owner, ledger);
            if (committed)
            {
                tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
                tampered.bills[0].completionObservations[0].fact.absoluteDay++;
                CommandMustThrow(() => ProductionCompletionFactPreflight.Validate(tampered, ledger));
            }
            Require(!source.Production.SetSuspended(record.billId, true).Succeeded,
                "A later command bypassed a pending physical completion.");
            Require(!source.Production.ExecuteWork(source.WorkerHandle, source.FacilityHandle, record.billId, float.MaxValue).Succeeded
                && CompletionPhysicalSignature(source) == physicalBefore, "A pending completion duplicated its physical outputs.");
            json = JsonUtility.ToJson(new CommandCheckpoint
            { sections = source.Registry.CaptureAll().Select(CloneEnvelope).ToList(), ledger = ledger, owner = owner });
        }
        using (var probe = new CompletionObserverProbe(null))
        using (var target = new RuntimeGraph("completion-ack-restored", 7211, config,
                   completionObservations: probe.Transport, completionRecovery: probe.Transport))
        {
            var checkpoint = JsonUtility.FromJson<CommandCheckpoint>(json);
            ProductionCompletionFactPreflight.Validate(checkpoint.owner, checkpoint.ledger);
            var candidate = probe.Ledger.PrepareGameplayOutcomeRestore(checkpoint.ledger);
            probe.Ledger.BeginRestoreCandidate(); probe.Ledger.PublishGameplayOutcomeRestore(candidate);
            probe.Ledger.PublishRestoreCandidate(); probe.Ledger.CompleteRestoreCandidate();
            var report = new DungeonGameRestoreReport();
            Require(target.Registry.RestoreAll(checkpoint.sections, report) && report.Success,
                "Fresh completion physical/facility/owner restore failed: " + string.Join(" | ", report.Errors));
            Require(CompletionPhysicalSignature(target) == physicalBefore, "Restore changed the pending physical output batch.");
            target.CommandCalendar.SetDateTime(190, 9);
            var result = target.Production.ExecuteWork(target.WorkerHandle, target.FacilityHandle, target.SingleBill.billId, float.MaxValue);
            Require(result.Succeeded && result.CycleCompleted && target.SingleBill.cycleSequence == 2
                && target.SingleBill.CompletionObservation == null && CompletionPhysicalSignature(target) == physicalBefore,
                "Completion recovery did not advance once while preserving the original output: " + result.Failure.Code
                + ":" + string.Join(",", result.Failure.Parameters.ToArray()));
            var ledger = probe.Ledger.CaptureGameplayOutcomes();
            var row = ledger.exactOutcomes.Concat(ledger.outbox).Single();
            var fact = checkpoint.owner.bills[0].completionObservations.Single().fact;
            Require(row.absoluteDay == 137 && row.ownerRevision == 1
                && row.participants.Any(value => value.displayText == fact.workerDisplayName)
                && row.participants.Any(value => value.displayText == fact.facilityDisplayName),
                "Completion recovery changed its first names/day or identity.");
            if (committed)
            {
                var original = checkpoint.ledger.exactOutcomes.Concat(checkpoint.ledger.outbox).Single();
                Require(row.sequence == original.sequence && row.immutablePayloadHash == original.immutablePayloadHash
                    && ledger.nextSequence == checkpoint.ledger.nextSequence, "Recovery created a second completion identity.");
            }
            ProductionCompletionFactPreflight.Validate(target.Production.Capture(), ledger);
            target.Production.ExecuteWork(target.WorkerHandle, target.FacilityHandle, target.SingleBill.billId, float.MaxValue);
            Require(CompletionPhysicalSignature(target) == physicalBefore
                && probe.Ledger.CaptureGameplayOutcomes().nextSequence == ledger.nextSequence,
                "Completed cycle replay duplicated its physical output or observation.");
        }
        Debug.Log("B125_COMPLETION_ACK_" + (committed ? "AFTER" : "BEFORE") + "_JSON_FRESH_OWNER=PASS; recipe=" + config.RecipeId);
    }

    private static string CompletionPhysicalSignature(RuntimeGraph graph) => string.Join("|", graph.WorldItems.GetAllStacks()
        .OrderBy(value => value.StackId, StringComparer.Ordinal).Select(value => value.StackId + ":" + value.Quantity));

    private sealed class CompletionObserverProbe : IDisposable
    {
        private readonly DomainFactDispatcher facts = new();
        internal readonly GameplayOutcomeLedger Ledger;
        internal readonly CommandAckTransport Transport;
        internal CompletionObserverProbe(bool? committed)
        {
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new ProductionCompletedOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new ProductionCompletedOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(), new GameplayOutcomeRunId("run:b125-production-completion"), 1);
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, new GameEventBus());
            facts.Subscribe("production.completion-observer", new GameplayDomainFactObserver<ProductionCycleCompletedFact, ProductionCompletedOutcomeReceipt>(
                ProductionCompletionFactObservation.Capture, registry, Ledger, recorder, new GameplayDomainFactCommitGroup(recorder, Ledger)));
            Transport = new CommandAckTransport(facts, committed);
        }
        public void Dispose() => facts.Dispose();
    }
}
#endif
