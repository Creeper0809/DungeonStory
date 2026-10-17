#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class V20ContentResolutionRecoveryDebugScenarios
{
    private static readonly string ActionId = V21ContentAlertActionIds.CulturalPractice(
        PracticeId, new[] { new CharacterId(ActorId) });
    private const string ActorId = "character:g08-content-resolution";
    private const string PracticeId = "practice:slime-core-ring";
    private const string ConsumedItemId = "component:mana-shield-plate";
    private const string GrantedItemId = "resource:clean-water";
    private const string RunId = "run:g08-content-resolution-recovery";
    private const int AbsoluteDay = 7;
    private const int StartingMoney = 100;
    private const int MoneyReward = 25;
    private const float HealthReward = 3f;
    private const string MissingActorId =
        "character:g04-content-resolution-missing";
    private const string DiseaseId = "disease:spore-lung";
    private const string PressureId = "pressure:g04-v20-evidence";
    private const string OutputEnvironmentVariable =
        "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT";

    public static void RunReceiptContractBatch()
    {
        Require(Application.isBatchMode, "Receipt contract verification requires isolated batch execution.");
        RunObserverBatch();
        PhysicalItemExactSourcePublicationDebugScenarios.RunAll();
        DurableFacilityEquipmentSaveDebugScenarios.RunAll();
        DurableFacilityEquipmentSlotCheckpointGcDebugScenarios.RunAll();
        Debug.Log("DESIGN_RECOVERY_CONTRACTS=PASS");
    }

    public static void RunObserverBatch()
    {
        Require(Application.isBatchMode, "B112 requires isolated batch execution.");
        _ = RunScenario();
        RunDurableReceiptBoundaries();
        RunReleasedVectorRetry();
        RunReceiptCapacityBoundary();
        Debug.Log("B112_CONTENT_CHILD_RECOVERY=PASS");
        RunObservationRecovery(beforeCommit: true);
        RunObservationRecovery(beforeCommit: false);
        RunObservedFuneralBatch();
        Debug.Log("B112_CONTENT_OBSERVERS=PASS");
    }

    private static void RunDurableReceiptBoundaries()
    {
        using var content = new FixtureContent();
        var grants = new SharedGrantState { FailNextCommit = false, LoseNextCommitReply = true };
        RecoverySaveBundle saved;
        string consumptionId;
        string grantId;
        using (var first = new Fixture(content, grants))
        {
            bool lostReply = false;
            try { first.Service.TryExecute(CreateRequest(), out _, out _); }
            catch (InvalidOperationException ex) { lostReply = ex.Message.Contains("injected-lost-successful-grant-reply"); }
            Require(lostReply && grants.SuccessfulCommits == 1, "Actual grant did not commit before the lost reply.");
            var pending = RequirePending(first, V20ContentResolutionRecoveryPhase.ItemCostsCommitted);
            var receipts = first.Items.Capture().physicalEffectReceipts;
            var consumption = receipts.Single(value => value.kind == (int)PhysicalEffectReceiptKind.ReservedConsumption);
            var grant = receipts.Single(value => value.kind == (int)PhysicalEffectReceiptKind.ReleasedPublication);
            consumptionId = consumption.commitId;
            grantId = grant.commitId;
            Require(pending.itemConsumptionCommitId == consumptionId && pending.itemGrantReceiptId.Length == 0,
                "Parent phase did not preserve the exact consumption/lost grant reply boundary.");
            // Destroy the current grant vector through the real reservation/consumption command.
            var grantCosts = grant.lines.Select(value => new ReservedItemConsumption(value.stackId, value.quantity)).ToArray();
            const string owner = "qa:consume-grant-before-recovery";
            Require(first.Reservations.TryReserveQuantities(grantCosts, owner, ItemReservationPurpose.DirectPlayerOrder, owner)
                    && first.AtomicItems.TryConsumeReserved(grantCosts, owner, out _), "Could not consume the released grant.");
            Require(first.Stock.GetGlobalQuantity(GrantedItemId) == 0, "Grant fixture was not fully consumed.");
            saved = first.CaptureRoundTrip();
        }

        using var restored = new Fixture(content, grants, saved);
        Require(restored.Repository.TryGetPhysicalEffectReceipt(PhysicalEffectReceiptKind.ReservedConsumption,
            "content-resolution:" + ActionId, out var originalConsumption), "Restored consumption receipt missing.");
        var originalCosts = originalConsumption.lines.Select(value => new ReservedItemConsumption(value.stackId, value.quantity)).ToArray();
        string originalPhysical = JsonUtility.ToJson(restored.Items.Capture());
        Require(restored.AtomicItems.TryConsumeReservedOnce(originalCosts, originalConsumption.operationId,
                out var repeated, out _) && repeated.commitId == consumptionId
                && JsonUtility.ToJson(restored.Items.Capture()) == originalPhysical,
            "Consumption replay after source deletion mutated physical state or changed its receipt.");
        Require(!restored.AtomicItems.TryConsumeReservedOnce(Array.Empty<ReservedItemConsumption>(),
                originalConsumption.operationId, out _, out _)
                && JsonUtility.ToJson(restored.Items.Capture()) == originalPhysical,
            "Empty retry bypassed the retained consumption request fingerprint.");
        var rejectedAck = new RejectReceiptAcknowledgement();
        Require(!restored.Items.AcknowledgePhysicalEffectReceipts(new[] { consumptionId, grantId }, rejectedAck, out _)
                && rejectedAck.RolledBack && JsonUtility.ToJson(restored.Items.Capture()) == originalPhysical,
            "Rejected parent acknowledgement retired its physical child evidence.");
        var conflict = new[] { new ReservedItemConsumption(originalCosts[0].StackId, originalCosts[0].Quantity + 1) };
        Require(!restored.AtomicItems.TryConsumeReservedOnce(conflict, originalConsumption.operationId, out _, out _)
                && JsonUtility.ToJson(restored.Items.Capture()) == originalPhysical, "Changed consumption replay was accepted.");

        int rejected = 0;
        void RejectCandidate(Action<RecoverySaveBundle> corrupt)
        {
            var bad = RoundTrip(saved);
            corrupt(bad);
            string physical = JsonUtility.ToJson(restored.Items.Capture());
            string milestones = JsonUtility.ToJson(restored.Campaign.CaptureMilestones());
            bool failed = false;
            try { restored.ValidateRestoreBoundary(bad); }
            catch (InvalidOperationException) { failed = true; }
            Require(failed && physical == JsonUtility.ToJson(restored.Items.Capture())
                    && milestones == JsonUtility.ToJson(restored.Campaign.CaptureMilestones())
                    && !restored.Items.IsPhysicalEffectReceiptCandidateAvailable,
                "Bad physical receipt candidate was accepted or altered live state.");
            restored.ValidateRestoreBoundary(saved);
            rejected++;
        }
        RejectCandidate(value => value.physicalItems.physicalEffectReceipts.Clear());
        RejectCandidate(value => value.physicalItems.physicalEffectReceipts[0].lines[0].quantity++);
        RejectCandidate(value => value.physicalItems.physicalEffectReceipts.Add(value.physicalItems.physicalEffectReceipts[0].Clone()));
        RejectCandidate(value => value.milestones.pendingContentResolutions[0].itemConsumptionCommitId = grantId);
        RejectCandidate(value => value.milestones.pendingContentResolutions[0].grantDropoffX++);
        RejectCandidate(value =>
        {
            var row = value.milestones.pendingContentResolutions[0];
            row.childCommitEvidenceVersion = 1;
            row.itemConsumptionCommitId = string.Empty;
        });
        // Even internally consistent physical evidence must match the parent effect quantity.
        RejectCandidate(value =>
        {
            var receipt = value.physicalItems.physicalEffectReceipts.Single(x => x.kind == (int)PhysicalEffectReceiptKind.ReservedConsumption);
            receipt.lines[0].quantity++;
            receipt.lines[0].quantityBefore++;
            receipt.requestFingerprint = PhysicalEffectReceiptRules.ConsumptionFingerprint(receipt.lines.Select(x => new KeyValuePair<string,int>(x.stackId,x.quantity)));
            receipt.commitId = PhysicalEffectReceiptRules.CommitId(receipt);
            value.milestones.pendingContentResolutions[0].itemConsumptionCommitId = receipt.commitId;
        });
        var legacy = Newtonsoft.Json.Linq.JObject.Parse(originalPhysical);
        legacy.Remove("physicalEffectReceiptSchemaVersion");
        legacy.Remove("physicalEffectReceipts");
        PhysicalItemsSaveSection.ValidateRequiredCurrentSchemaShape(legacy.ToString());
        legacy["physicalEffectReceiptSchemaVersion"] = 1;
        bool partialRejected = false;
        try { PhysicalItemsSaveSection.ValidateRequiredCurrentSchemaShape(legacy.ToString()); }
        catch (InvalidOperationException) { partialRejected = true; }
        Require(partialRejected, "Partial physical receipt extension was treated as a legacy save.");

        Require(restored.Service.TryExecute(CreateRequest(), out _, out var failure) && !failure.IsFailure,
            "Lost grant reply recovery failed after actual grant consumption: " + failure);
        Require(grants.SuccessfulCommits == 1 && restored.Stock.GetGlobalQuantity(GrantedItemId) == 0
                && restored.Stock.GetGlobalQuantity(ConsumedItemId) == 0
                && restored.Money.Balance == StartingMoney + MoneyReward
                && restored.Campaign.CaptureMilestones().pendingContentResolutions.Count == 0,
            "Restore/retry duplicated a consumed child or failed to complete its parent.");
        var completed = restored.CaptureRoundTrip();
        Require(completed.physicalItems.physicalEffectReceipts.Count == 0
                && restored.Items.AvailablePhysicalEffectReceiptCapacity == PhysicalEffectReceiptRules.MaximumReceipts,
            "Completed parent did not atomically retire its recovery receipts.");
        _ = restored.Service.TryExecute(CreateRequest(), out _, out _);
        Require(grants.SuccessfulCommits == 1 && restored.Stock.GetGlobalQuantity(GrantedItemId) == 0,
            "Terminal parent replay after receipt retirement republished an already consumed reward.");
        WriteReceiptEvidence("receipt-pending.json", saved);
        WriteReceiptEvidence("receipt-completed.json", completed);
        Debug.Log("DURABLE_PHYSICAL_RECOVERY=PASS; actual-consume/grant; consumed-output replay; rejected-candidates=" + rejected);
    }

    private static void RunReleasedVectorRetry()
    {
        using var content = new FixtureContent();
        var shared = new SharedGrantState { FailNextCommit = false };
        var position = new Vector2Int(3, 4);
        var plan = new PhysicalItemExactSourcePublicationPlan("qa.receipt", "partial-consumption", position,
            new[] { new FacilityBufferPlannedOutputSlice("water", PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)GrantedItemId), 3) }, true);
        RecoverySaveBundle saved;
        string commitId;
        using (var first = new Fixture(content, shared))
        {
            Require(first.ExactSources.TryPrepare(plan, out var transaction, out _)
                    && first.ExactSources.TryPrepare(plan, out var same, out _)
                    && transaction.PreparedStacks.SequenceEqual(same.PreparedStacks),
                "Same prepared recovery plan could not resume its exact output vector.");
            var changed = new PhysicalItemExactSourcePublicationPlan("qa.receipt", "partial-consumption", position,
                new[] { new FacilityBufferPlannedOutputSlice("water", PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)GrantedItemId), 4) }, true);
            Require(!first.ExactSources.TryPrepare(changed, out _, out _), "Conflicting prepared plan was accepted.");
            Require(!first.ExactSources.TryCommitReleased(transaction, position + Vector2Int.one, "qa-position", out _, out _),
                "Conflicting release position was accepted.");
            Require(first.ExactSources.TryCommitReleased(transaction, position, "qa-release", out var receipt, out _),
                "Actual retained recovery receipt publication failed.");
            commitId = receipt.RecoveryCommitId;
            var cost = new[] { new ReservedItemConsumption(receipt.Stacks.Single().StackId, 1) };
            Require(first.Reservations.TryReserveQuantities(cost, "qa:partial", ItemReservationPurpose.DirectPlayerOrder, "qa:partial")
                && first.AtomicItems.TryConsumeReserved(cost, "qa:partial", out _), "Actual partial grant consumption failed.");
            saved = first.CaptureRoundTrip();
        }
        using var second = new Fixture(content, shared, saved);
        Require(second.Stock.GetGlobalQuantity(GrantedItemId) == 2, "Partially consumed grant did not restore.");
        string before = JsonUtility.ToJson(second.Items.Capture());
        Require(second.ExactSources.TryPrepare(plan, out var replay, out _)
            && second.ExactSources.TryCommitReleased(replay, position, "qa-replay", out var repeated, out _)
            && repeated.RecoveryCommitId == commitId && JsonUtility.ToJson(second.Items.Capture()) == before,
            "Historical grant replay replenished a partially consumed stack.");
        Debug.Log("DURABLE_PHYSICAL_PARTIAL_REPLAY=PASS; exact-plan resume; conflicts rejected; partial stock preserved");
    }

    private static void RunReceiptCapacityBoundary()
    {
        using var content = new FixtureContent();
        var grants = new SharedGrantState { FailNextCommit = false };
        using var fixture = new Fixture(content, grants);
        var initial = fixture.Items.Capture();
        var stack = fixture.Repository.Records.Single(value => value.itemId == ConsumedItemId);
        var costs = new[] { new ReservedItemConsumption(stack.stackId, 1) };
        Require(fixture.Reservations.TryReserveQuantities(costs, "qa:capacity-seed", ItemReservationPurpose.DirectPlayerOrder, "qa:capacity")
            && fixture.AtomicItems.TryConsumeReservedOnce(costs, "qa:capacity-seed", out _, out _), "Receipt capacity seed failed.");
        var seed = fixture.Items.Capture().physicalEffectReceipts.Single();
        // Capacity-only fixture history derives from a real commit; none is accepted as a content child.
        for (int i = 0; i < PhysicalEffectReceiptRules.MaximumReceipts - 1; i++)
        {
            var receipt = seed.Clone();
            receipt.operationId = "qa:capacity:" + i;
            receipt.commitId = PhysicalEffectReceiptRules.CommitId(receipt);
            initial.physicalEffectReceipts.Add(receipt);
        }
        fixture.Items.Restore(initial);
        string before = JsonUtility.ToJson(fixture.Items.Capture());
        Require(!fixture.Service.TryExecute(CreateRequest(), out _, out var failure) && failure.IsFailure
            && fixture.Money.Balance == StartingMoney && grants.CommitAttempts == 0
            && before == JsonUtility.ToJson(fixture.Items.Capture()),
            "Two-child content spent resources with capacity for only one receipt.");
        Require(fixture.Items.AcknowledgePhysicalEffectReceipts(initial.physicalEffectReceipts.Select(value => value.commitId).ToArray(),
            new CompleteCapacityFixture(), out _), "Capacity fixture acknowledgement failed.");
        Require(fixture.Service.TryExecute(CreateRequest(), out _, out failure) && !failure.IsFailure
            && fixture.Stock.GetGlobalQuantity(GrantedItemId) == 1 && grants.SuccessfulCommits == 1
            && fixture.Items.AvailablePhysicalEffectReceiptCapacity == PhysicalEffectReceiptRules.MaximumReceipts,
            "Capacity rejection could not retry after completed receipt retirement.");
        Debug.Log("DURABLE_PHYSICAL_CAPACITY=PASS; preflight-no-mutation; joint-ack-reclaims-capacity; retry-completes");
    }

    private sealed class CompleteCapacityFixture : IPhysicalItemDispositionAcknowledgementParticipant
    {
        public bool TryCommit(out string reason) { reason = string.Empty; return true; }
        public void Rollback() { }
    }

    private static void RunObservedFuneralBatch()
    {
        using var content = new FixtureContent();
        using var fixture = new Fixture(content, new SharedGrantState());
        var participant = new CharacterId(ActorId);
        fixture.Grief.ApplyTraumaDelta(participant, "B112 fixture trauma", AbsoluteDay, 10f);
        var receipt = new ObservedFuneralLifeEventReceipt("funeral:b112:batch", new CharacterId("character:b112:deceased"),
            "building:b112:memorial", AbsoluteDay, 0, new[] { participant });
        fixture.Service.RequireCanCommitObservedFuneralLifeEvent(receipt);
        string society = JsonUtility.ToJson(fixture.Campaign.CaptureSociety());
        string grief = JsonUtility.ToJson(fixture.Grief.Capture());
        fixture.Probe.TargetKind = MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt;
        fixture.Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
        Require(fixture.Journal.TryReserve(receipt, out var lease, out _), "B112 funeral reservation failed.");
        bool rejected = false;
        try { fixture.Service.CommitObservedFuneralLifeEvent(receipt, lease); }
        catch (ContentResolutionFactRejectedException) { rejected = true; }
        var failed = fixture.Outcomes.Ledger.CaptureGameplayOutcomes();
        Require(rejected && failed.exactOutcomes.Count + failed.outbox.Count == 0
            && JsonUtility.ToJson(fixture.Campaign.CaptureSociety()) == society
            && JsonUtility.ToJson(fixture.Grief.Capture()) == grief && fixture.Journal.Capture().pending.Count == 0,
            "B112 rejected parent left a committed child or an applied effect. rejected=" + rejected
            + "; records=" + (failed.exactOutcomes.Count + failed.outbox.Count)
            + "; trauma=" + fixture.Grief.Require(participant).Trauma
            + "; physicalRecoveries=" + fixture.Campaign.CaptureMilestones().pendingContentResolutions.Count);
        fixture.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        Require(fixture.Journal.TryReserve(receipt, out lease, out _), "B112 funeral retry reservation failed.");
        Require(fixture.Service.CommitObservedFuneralLifeEvent(receipt, lease).Count == 1,
            "B112 funeral retry did not resolve the authored event.");
        var completed = fixture.Outcomes.Ledger.CaptureGameplayOutcomes();
        Require(completed.exactOutcomes.Count + completed.outbox.Count == 2
            && Mathf.Approximately(fixture.Grief.Require(participant).Trauma, 8f)
            && fixture.Campaign.CaptureSociety().successfulLifeEventOperations.Count == 1
            && fixture.Journal.Capture().pending.Count == 0,
            "B112 parent/child retry was not one batch with one actual trauma reduction.");
        Debug.Log("B112_PARENT_CHILD_ATOMIC=PASS");
    }

    private static void RunObservationRecovery(bool beforeCommit)
    {
        using var content = new FixtureContent();
        var grants = new SharedGrantState { FailNextCommit = false };
        RecoverySaveBundle saved;
        string key;
        using (var source = new Fixture(content, grants))
        {
            source.Transport.ThrowBeforeCommit = beforeCommit;
            source.Transport.HideAcknowledgement = !beforeCommit;
            Require(source.Service.TryExecute(CreateRequest(), out _, out var failure) && !failure.IsFailure,
                "B112 uncertain delivery rolled back an applied effect: " + failure);
            saved = source.CaptureRoundTrip();
            Require(saved.observations.pending.Count == 1
                && saved.milestones.pendingContentResolutions.Count == 0,
                "B112 delivery ownership was not transferred to the frozen journal.");
            key = saved.observations.pending.Single().publicationKey;
            SocialRitualFactPreflight.ValidateJournal(saved.observations, saved.gameplayOutcomes, saved.migratedProducerOutcomes);
            SocialRitualFactPreflight.ValidateContentRecoveries(saved.observations, saved.milestones);
        }
        using (var restored = new Fixture(content, grants, saved))
        {
            var before = restored.CaptureRoundTrip();
            Require(restored.Journal.RecoverPending(out var failure), "B112 frozen replay failed: " + failure);
            var after = restored.CaptureRoundTrip();
            Require(after.observations.pending.Count == 0 && grants.SuccessfulCommits == 1 && grants.CommitAttempts == 1
                && after.session.money == before.session.money
                && JsonUtility.ToJson(after.physicalItems) == JsonUtility.ToJson(before.physicalItems)
                && JsonUtility.ToJson(after.bodyHealth) == JsonUtility.ToJson(before.bodyHealth)
                && JsonUtility.ToJson(after.narrative) == JsonUtility.ToJson(before.narrative),
                "B112 observer replay repeated or changed a real owner effect.");
            var exact = after.gameplayOutcomes.exactOutcomes.Concat(after.gameplayOutcomes.outbox)
                .Single(v => new GameplayResultKey(v.producerId, new GameplayOperationId(v.operationId),
                    v.commitRevision, v.localResultIndex).ToString() == key);
            RequireRequestCausation(exact, ActionId);
            string completed = JsonUtility.ToJson(after);
            Require(restored.Journal.RecoverPending(out _) && JsonUtility.ToJson(restored.CaptureRoundTrip()) == completed,
                "B112 repeat acknowledgement changed a terminal state.");
        }
        Debug.Log(beforeCommit ? "B112_CONTENT_ACK_BEFORE=PASS" : "B112_CONTENT_ACK_AFTER=PASS");
    }

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            _ = RunScenario();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void RunOrThrow()
    {
        try
        {
            ScenarioReport report = RunScenario();
            WriteReport(report);
        }
        catch (Exception exception)
        {
            WriteReport(new ScenarioReport
            {
                passed = false,
                scenario = "G08 V20 content child-commit recovery",
                error = exception.ToString(),
                fullAuditClosure = false,
                humanApprovalClaimed = false,
                trainingEligible = false
            });
            throw;
        }
    }

    public static void RunIsolatedOrThrow()
    {
        Require(Application.isBatchMode, "G08 recovery requires the owned isolated batch launcher.");
        string output = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
        Require(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output),
            "G08 recovery requires an explicit absolute QA report output.");
        string qaRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA"))
            + Path.DirectorySeparatorChar;
        Require(Path.GetFullPath(output).StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase)
            && !File.Exists(Path.Combine(output, "report.json")),
            "G08 recovery evidence must use a new create-only project QA output.");
        RunOrThrow();
    }

    public static void RunG04OrThrow()
    {
        try
        {
            WriteG04Report(RunG04Scenario());
        }
        catch (Exception exception)
        {
            WriteG04Report(new G04ScenarioReport
            {
                passed = false,
                scenario = "G04 V20 per-target application evidence",
                error = exception.ToString(),
                fullAuditClosure = false,
                humanApprovalClaimed = false,
                trainingEligible = false
            });
            throw;
        }
    }

    private static G04ScenarioReport RunG04Scenario()
    {
        using var content = new FixtureContent(includeG04Effects: true);
        var grantState = new SharedGrantState();
        RecoverySaveBundle saved;
        GameplayOutcomeId practiceOutcomeId;
        GameplayOutcomeId retirementOutcomeId;
        string practiceApplicationEvidence;
        string retirementApplicationEvidence;

        using (var fixture = new Fixture(content, grantState))
        {
            float healthBefore = fixture.Health.GetVitals(fixture.Actor)
                .CurrentHealth;
            ContentResolutionRequest practiceRequest = new()
            {
                ActionId = "g04-v20-practice",
                Kind = ContentResolutionRequestKind.CulturalPractice,
                DefinitionId = PracticeId,
                ParticipantCharacterIds = new[] { ActorId, MissingActorId },
                AbsoluteDay = AbsoluteDay,
                Requirements = new RunMilestoneEvaluationSnapshot
                {
                    AbsoluteDay = AbsoluteDay
                }
            };
            Require(fixture.Service.TryExecute(
                        practiceRequest,
                        out ContentResolutionResult practice,
                        out DomainFailure practiceFailure)
                    && !practiceFailure.IsFailure
                    && practice.Resolutions.Count == 1,
                "G04 practice resolution failed: " + practiceFailure);

            V20EffectApplicationEvidence healthApplied = RequireApplication(
                practice,
                V20ContentEffectKind.Health,
                ActorId);
            V20EffectApplicationEvidence healthSkipped = RequireApplication(
                practice,
                V20ContentEffectKind.Health,
                MissingActorId);
            V20EffectApplicationEvidence exposureApplied = RequireApplication(
                practice,
                V20ContentEffectKind.DiseaseExposure,
                ActorId);
            V20EffectApplicationEvidence exposureSkipped = RequireApplication(
                practice,
                V20ContentEffectKind.DiseaseExposure,
                MissingActorId);
            V20EffectApplicationEvidence pressureApplied = RequireApplication(
                practice,
                V20ContentEffectKind.Threat,
                PressureId);
            float healthAfter = fixture.Health.GetVitals(fixture.Actor)
                .CurrentHealth;
            DiseaseExposureSaveData exposure = fixture.PopulationHealth.Capture()
                .pendingExposures.Single(value => string.Equals(
                    value.characterId,
                    ActorId,
                    StringComparison.Ordinal)
                    && string.Equals(
                        value.diseaseId,
                        DiseaseId,
                        StringComparison.Ordinal));
            Require(healthApplied.status == V20EffectApplicationStatus.Applied
                    && Mathf.Approximately(healthApplied.appliedAmount, HealthReward)
                    && healthSkipped.status == V20EffectApplicationStatus.Skipped
                    && string.Equals(
                        healthSkipped.beforeValue,
                        "unavailable",
                        StringComparison.Ordinal)
                    && exposureApplied.status
                        == V20EffectApplicationStatus.Clamped
                    && Mathf.Approximately(exposureApplied.appliedAmount, 24f)
                    && exposureSkipped.status
                        == V20EffectApplicationStatus.Skipped
                    && exposureApplied.afterValue.Contains(
                        "infected=false",
                        StringComparison.Ordinal)
                    && pressureApplied.status
                        == V20EffectApplicationStatus.Applied
                    && string.Equals(
                        pressureApplied.beforeValue,
                        "false",
                        StringComparison.Ordinal)
                    && string.Equals(
                        pressureApplied.afterValue,
                        "true",
                        StringComparison.Ordinal)
                    && Mathf.Approximately(healthAfter, healthBefore + HealthReward)
                    && Mathf.Approximately(exposure.weightedExposureHours, 24f)
                    && fixture.Campaign.ActivePressureIds.Contains(
                        PressureId,
                        StringComparer.Ordinal),
                "G04 practice did not distinguish health, pending exposure, pressure, clamp, and missing-participant skip.");

            GameplayOutcomeReplayIdentity practiceIdentity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    fixture.Outcomes,
                    MigratedProducerOutcomeKind.V20ResolvedEventResult,
                    fixture.Probe);
            Require(fixture.Outcomes.Ledger.TryGetExact(
                    practiceIdentity.OutcomeId,
                    out GameplayOutcomeSnapshot practiceOutcome),
                "G04 practice outcome identity did not resolve.");
            practiceOutcomeId = practiceIdentity.OutcomeId;
            practiceApplicationEvidence = ApplicationFact(practiceOutcome);
            Require(practiceApplicationEvidence.Contains(
                        "semantic=exposure-pending-not-infection",
                        StringComparison.Ordinal)
                    && practiceApplicationEvidence.Contains(
                        "semantic=pressure-activated-not-health-damage",
                        StringComparison.Ordinal)
                    && practiceApplicationEvidence.Contains(
                        "target=" + MissingActorId + "; status=skipped",
                        StringComparison.Ordinal)
                    && practiceApplicationEvidence.Contains(
                        "status=clamped; requested=30; applied=24",
                        StringComparison.Ordinal),
                "G04 practice ledger lost its per-target application evidence.");

            LifeEventDefinitionSO retirement = content.StoryCatalog.LifeEvents
                .Single(value => string.Equals(
                    value.StableId,
                    "life-event:retirement-request",
                    StringComparison.Ordinal));
            const string RetirementInstanceId =
                "society-event:g04-retirement-request";
            SocietyEventWorldSaveData society = fixture.Campaign.CaptureSociety();
            society.activeEvents.Add(new V20ActiveEventSaveData
            {
                instanceId = RetirementInstanceId,
                definitionId = retirement.StableId,
                startedAbsoluteDay = AbsoluteDay + 1,
                deadlineAbsoluteDay = AbsoluteDay + 2,
                generation = 1,
                deterministicRoll = 404u,
                contextFactionId = content.StoryCatalog.Arcs.First().factionId,
                participantCharacterIds = new List<string> { ActorId },
                riskTier = retirement.riskTier,
                riskReason = retirement.riskReason
            });
            fixture.Campaign.PublishSociety(
                fixture.Campaign.PrepareSociety(society));
            ContentResolutionRequest retirementRequest = new()
            {
                ActionId = V21ContentAlertActionIds.Society(
                    RetirementInstanceId, "second"),
                Kind = ContentResolutionRequestKind.SocietyChoice,
                InstanceId = RetirementInstanceId,
                ChoiceId = "second",
                AbsoluteDay = AbsoluteDay + 1,
                Requirements = new RunMilestoneEvaluationSnapshot
                {
                    AbsoluteDay = AbsoluteDay + 1
                }
            };
            Require(fixture.Service.TryExecute(
                        retirementRequest,
                        out ContentResolutionResult retirementResult,
                        out DomainFailure retirementFailure)
                    && !retirementFailure.IsFailure
                    && retirementResult.Resolutions.Count == 1,
                "G04 retirement resolution failed: " + retirementFailure);
            V20EffectApplicationEvidence retirementApplication =
                RequireApplication(
                    retirementResult,
                    V20ContentEffectKind.RetirementSchedule,
                    ActorId);
            Require(retirementApplication.status
                        == V20EffectApplicationStatus.Applied
                    && string.Equals(
                        retirementApplication.semantic,
                        "retirement-scheduled-not-character-exit",
                        StringComparison.Ordinal)
                    && Mathf.Approximately(
                        retirementApplication.appliedAmount,
                        GameCalendarRules.DaysPerSeason)
                    && retirementApplication.afterValue.Contains(
                        "status=Pending",
                        StringComparison.Ordinal)
                    && retirementApplication.afterValue.Contains(
                        "retired=false",
                        StringComparison.Ordinal)
                    && fixture.Careers.TryGet(
                        new CharacterId(ActorId),
                        out CharacterCareerSnapshot career)
                    && career.RetirementScheduleStatus
                        == RetirementScheduleStatus.Pending
                    && !career.Retired
                    && fixture.Characters.Characters.Contains(fixture.Actor),
                "G04 retirement evidence conflated a pending schedule with character exit.");

            GameplayOutcomeReplayIdentity retirementIdentity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    fixture.Outcomes,
                    MigratedProducerOutcomeKind.V20ResolvedEventResult,
                    fixture.Probe);
            Require(fixture.Outcomes.Ledger.TryGetExact(
                    retirementIdentity.OutcomeId,
                    out GameplayOutcomeSnapshot retirementOutcome),
                "G04 retirement outcome identity did not resolve.");
            retirementOutcomeId = retirementIdentity.OutcomeId;
            RequireRequestCausation(retirementOutcome, retirementRequest.ActionId);
            RequireRequestCausation(practiceOutcome, practiceRequest.ActionId);
            Require(!string.Equals(retirementOutcome.rootOperationId,
                    practiceOutcome.rootOperationId, StringComparison.Ordinal),
                "Distinct content requests shared a ledger root identity.");
            retirementApplicationEvidence = ApplicationFact(retirementOutcome);
            Require(retirementApplicationEvidence.Contains(
                    "semantic=retirement-scheduled-not-character-exit",
                    StringComparison.Ordinal),
                "G04 retirement ledger lost its schedule-versus-exit evidence.");
            saved = fixture.CaptureRoundTrip();
        }

        using (var restored = new Fixture(content, new SharedGrantState(), saved))
        {
            PopulationHealthWorldSaveData population =
                restored.PopulationHealth.Capture();
            Require(population.pendingExposures.Any(value => value != null
                        && string.Equals(
                            value.characterId,
                            ActorId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.diseaseId,
                            DiseaseId,
                            StringComparison.Ordinal)
                        && Mathf.Approximately(
                            value.weightedExposureHours,
                            24f))
                    && population.characters.All(value => value == null
                        || !string.Equals(
                            value.characterId,
                            ActorId,
                            StringComparison.Ordinal)
                        || value.activeDiseases.All(disease => disease == null
                            || !string.Equals(
                                disease.diseaseId,
                                DiseaseId,
                                StringComparison.Ordinal)))
                    && restored.Campaign.ActivePressureIds.Contains(
                        PressureId,
                        StringComparer.Ordinal)
                    && restored.Careers.TryGet(
                        new CharacterId(ActorId),
                        out CharacterCareerSnapshot restoredCareer)
                    && restoredCareer.RetirementScheduleStatus
                        == RetirementScheduleStatus.Pending
                    && !restoredCareer.Retired
                    && restored.Outcomes.Ledger.TryGetExact(
                        practiceOutcomeId,
                        out GameplayOutcomeSnapshot restoredPractice)
                    && string.Equals(
                        ApplicationFact(restoredPractice),
                        practiceApplicationEvidence,
                        StringComparison.Ordinal)
                    && restored.Outcomes.Ledger.TryGetExact(
                        retirementOutcomeId,
                        out GameplayOutcomeSnapshot restoredRetirement)
                    && string.Equals(
                        ApplicationFact(restoredRetirement),
                        retirementApplicationEvidence,
                        StringComparison.Ordinal),
                "G04 save/restore changed exposure, pressure, retirement, or exact ledger evidence.");
        }

        return new G04ScenarioReport
        {
            passed = true,
            scenario = "G04 V20 per-target application evidence",
            branches = new List<string>
            {
                "health-applied-and-missing-participant-skipped",
                "disease-exposure-clamped-without-infection",
                "pressure-activated-without-health-damage",
                "retirement-scheduled-without-character-exit",
                "exact-ledger-application-fact",
                "whole-save-round-trip"
            },
            practiceOutcomeId = practiceOutcomeId.ToString(),
            retirementOutcomeId = retirementOutcomeId.ToString(),
            practiceApplicationEvidence = practiceApplicationEvidence,
            retirementApplicationEvidence = retirementApplicationEvidence,
            fullAuditClosure = false,
            humanApprovalClaimed = false,
            trainingEligible = false
        };
    }

    private static ScenarioReport RunScenario()
    {
        using var content = new FixtureContent();
        var grantState = new SharedGrantState();
        RecoverySaveBundle afterGrantFailure;
        RecoverySaveBundle afterParentRejection;
        string moneyTransactionId;
        string consumptionOperationId;
        string grantBatchCommitId;
        float initialHealth;

        using (var first = new Fixture(content, grantState))
        {
            initialHealth = first.Health.GetVitals(first.Actor).CurrentHealth;
            Require(Mathf.Approximately(initialHealth, 90f),
                "Initial body-health fixture did not own the expected damaged state.");
            Require(first.Stock.GetGlobalQuantity(ConsumedItemId) == 1
                    && first.Stock.GetGlobalQuantity(GrantedItemId) == 0,
                "Initial physical inventory was not the authored practice input only.");
            Require(first.Narrative.CanPerformPractice(
                    new CharacterId(ActorId),
                    PracticeId,
                    AbsoluteDay,
                    out _),
                "Initial narrative owner rejected the authored practice.");

            bool exactGrantFaultSurfaced = false;
            try
            {
                _ = first.Service.TryExecute(
                    CreateRequest(),
                    out _,
                    out _);
            }
            catch (InvalidOperationException exception)
            {
                exactGrantFaultSurfaced = exception.Message.Contains(
                    "injected-g08-grant-publication-failure",
                    StringComparison.Ordinal);
            }
            Require(exactGrantFaultSurfaced,
                "The injected grant failure did not surface at the exact publication boundary.");
            Require(grantState.CommitAttempts == 1
                    && grantState.SuccessfulCommits == 0,
                "The first grant boundary did not fail before physical publication.");
            Require(first.Money.Balance == StartingMoney + MoneyReward,
                "Money child did not commit exactly once before the grant fault.");
            Require(first.Stock.GetGlobalQuantity(ConsumedItemId) == 0
                    && first.Stock.GetGlobalQuantity(GrantedItemId) == 0,
                "Item cost/grant state did not stop at the item-cost phase.");
            Require(Mathf.Approximately(
                    first.Health.GetVitals(first.Actor).CurrentHealth,
                    initialHealth),
                "Typed health effect ran before grant recovery completed.");
            Require(first.Narrative.CanPerformPractice(
                    new CharacterId(ActorId),
                    PracticeId,
                    AbsoluteDay,
                    out _),
                "Narrative participation ran before grant recovery completed.");
            Require(first.Probe.CommitCount(
                    MigratedProducerOutcomeKind.V20ResolvedEventResult) == 0,
                "Parent outcome committed before the grant fault was recovered.");

            V20ContentResolutionRecoverySaveData pending = RequirePending(
                first,
                V20ContentResolutionRecoveryPhase.ItemCostsCommitted);
            Require(first.Money.TryGetSuccessfulTransaction(
                    EconomyTransactionKind.ContractIncome,
                    "content:" + ActionId,
                    out EconomyTransactionRecord moneyRecord)
                    && moneyRecord != null
                    && moneyRecord.amount == MoneyReward
                    && moneyRecord.balanceBefore == StartingMoney
                    && moneyRecord.balanceAfter == StartingMoney + MoneyReward
                    && string.Equals(
                        moneyRecord.transactionId,
                        pending.moneyTransactionId,
                        StringComparison.Ordinal),
                "Durable recovery did not reference the actual money transaction.");
            moneyTransactionId = pending.moneyTransactionId;
            consumptionOperationId = pending.itemConsumptionOperationId;
            grantBatchCommitId = pending.itemGrantBatchCommitId;
            Require(string.Equals(
                        consumptionOperationId,
                        "content-resolution:" + ActionId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        grantBatchCommitId,
                        "physical-source-batch:run.v20-grant:content-resolution:"
                            + ActionId,
                        StringComparison.Ordinal),
                "Recovery row did not freeze the exact item child identities.");
            afterGrantFailure = first.CaptureRoundTrip();
        }

        using (var second = new Fixture(
                   content,
                   grantState,
                   afterGrantFailure,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(second.Money.Balance == StartingMoney + MoneyReward
                    && second.Stock.GetGlobalQuantity(ConsumedItemId) == 0
                    && second.Stock.GetGlobalQuantity(GrantedItemId) == 0
                    && Mathf.Approximately(
                        second.Health.GetVitals(second.Actor).CurrentHealth,
                        initialHealth),
                "First save/restore did not preserve the item-cost recovery boundary.");

            Require(!second.Service.TryExecute(
                    CreateRequest(),
                    out _,
                    out DomainFailure parentFailure)
                    && parentFailure.IsFailure,
                "Injected parent outcome rejection was not surfaced after child recovery.");
            Require(grantState.CommitAttempts == 2
                    && grantState.SuccessfulCommits == 1,
                "Grant recovery did not publish exactly one physical batch.");
            Require(second.Money.Balance == StartingMoney + MoneyReward,
                "Grant recovery replayed the money child.");
            Require(second.Stock.GetGlobalQuantity(ConsumedItemId) == 0
                    && second.Stock.GetGlobalQuantity(GrantedItemId) == 1,
                "Grant recovery did not preserve one consumption and one grant.");
            Require(Mathf.Approximately(
                    second.Health.GetVitals(second.Actor).CurrentHealth,
                    initialHealth + HealthReward),
                "Typed health recovery did not apply exactly once.");
            Require(!second.Narrative.CanPerformPractice(
                    new CharacterId(ActorId),
                    PracticeId,
                    AbsoluteDay,
                    out _),
                "Recovered cultural-practice participation was not recorded.");
            Require(second.Probe.CommitCount(
                    MigratedProducerOutcomeKind.V20ResolvedEventResult) == 0,
                "Rejected parent outcome was counted as committed.");
            V20ContentResolutionRecoverySaveData pending = RequirePending(
                second,
                V20ContentResolutionRecoveryPhase.TypedEffectsApplied);
            Require(string.Equals(
                        pending.moneyTransactionId,
                        moneyTransactionId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        pending.itemConsumptionOperationId,
                        consumptionOperationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        pending.itemGrantBatchCommitId,
                        grantBatchCommitId,
                        StringComparison.Ordinal),
                "Parent rejection changed a frozen child commit identity.");
            afterParentRejection = second.CaptureRoundTrip();
        }

        GameplayOutcomeReplayIdentity parentIdentity;
        string effectEvidence;
        RecoverySaveBundle completed;
        using (var third = new Fixture(
                   content,
                   grantState,
                   afterParentRejection))
        {
            int balanceBeforeParentRetry = third.Money.Balance;
            int grantedBeforeParentRetry = third.Stock.GetGlobalQuantity(
                GrantedItemId);
            float healthBeforeParentRetry = third.Health.GetVitals(
                third.Actor).CurrentHealth;
            string narrativeBeforeParentRetry = JsonUtility.ToJson(
                third.Narrative.Capture());

            Require(third.Service.TryExecute(
                    CreateRequest(),
                    out ContentResolutionResult recovered,
                    out DomainFailure failure)
                    && !failure.IsFailure
                    && recovered != null
                    && string.Equals(
                        recovered.ActionId,
                        ActionId,
                        StringComparison.Ordinal),
                "Parent-only recovery did not complete after the second restore.");
            Require(third.Campaign.CaptureMilestones()
                    .pendingContentResolutions.Count == 0,
                "Completed parent recovery did not acknowledge its durable row.");
            Require(third.Money.Balance == balanceBeforeParentRetry
                    && third.Stock.GetGlobalQuantity(ConsumedItemId) == 0
                    && third.Stock.GetGlobalQuantity(GrantedItemId)
                        == grantedBeforeParentRetry
                    && Mathf.Approximately(
                        third.Health.GetVitals(third.Actor).CurrentHealth,
                        healthBeforeParentRetry)
                    && string.Equals(
                        JsonUtility.ToJson(third.Narrative.Capture()),
                        narrativeBeforeParentRetry,
                        StringComparison.Ordinal),
                "Parent retry replayed an already committed child owner.");
            Require(grantState.CommitAttempts == 2
                    && grantState.SuccessfulCommits == 1,
                "Parent retry touched the exact grant boundary again.");
            Require(third.Probe.CommitCount(
                    MigratedProducerOutcomeKind.V20ResolvedEventResult) == 1,
                "Parent recovery did not commit exactly one resolved-event outcome.");

            parentIdentity = MigratedProducerOutcomeE2EAssertions.RequireExact(
                third.Outcomes,
                MigratedProducerOutcomeKind.V20ResolvedEventResult,
                third.Probe);
            Require(third.Outcomes.Ledger.TryGetExact(
                    parentIdentity.OutcomeId,
                    out GameplayOutcomeSnapshot exact),
                "The committed parent identity did not resolve to an exact ledger row.");
            RequireRequestCausation(exact, ActionId);
            effectEvidence = exact.facts.Single(value => string.Equals(
                value.factId,
                MigratedProducerOutcomeIds.EffectFact.Value,
                StringComparison.Ordinal)).value;
            Require(effectEvidence.Contains(
                        "kind=Money; target=participants; amount=25",
                        StringComparison.Ordinal)
                    && effectEvidence.Contains(
                        "childOutcomeId=" + moneyTransactionId,
                        StringComparison.Ordinal)
                    && effectEvidence.Contains(
                        "kind=ItemGrant; target=" + GrantedItemId,
                        StringComparison.Ordinal)
                    && effectEvidence.Contains(
                        "childOutcomeId=" + grantBatchCommitId,
                        StringComparison.Ordinal)
                    && effectEvidence.Contains(
                        "kind=ItemConsume; target=" + ConsumedItemId,
                        StringComparison.Ordinal)
                    && effectEvidence.Contains(
                        "childOutcomeId=" + consumptionOperationId,
                        StringComparison.Ordinal)
                    && effectEvidence.Contains(
                        "kind=Health; target=" + PracticeId
                            + "; amount=3; durationDays=5; childOutcomeId=none",
                        StringComparison.Ordinal),
                "Parent ledger evidence did not reference each actual transactional child.");

            int outcomeCountBeforeReplay = third.Probe.CommitCount(
                MigratedProducerOutcomeKind.V20ResolvedEventResult);
            _ = third.Service.TryExecute(
                CreateRequest(),
                out _,
                out _);
            Require(third.Money.Balance == balanceBeforeParentRetry
                    && third.Stock.GetGlobalQuantity(GrantedItemId)
                        == grantedBeforeParentRetry
                    && Mathf.Approximately(
                        third.Health.GetVitals(third.Actor).CurrentHealth,
                        healthBeforeParentRetry)
                    && grantState.SuccessfulCommits == 1
                    && third.Probe.CommitCount(
                        MigratedProducerOutcomeKind.V20ResolvedEventResult)
                        == outcomeCountBeforeReplay,
                "Terminal replay duplicated a child mutation or parent outcome.");
            completed = third.CaptureRoundTrip();
        }

        using (var restored = new Fixture(content, grantState, completed))
        {
            Require(restored.Outcomes.Ledger.TryGetExact(
                    parentIdentity.OutcomeId,
                    out GameplayOutcomeSnapshot exact),
                "Completed restore lost the exact parent outcome.");
            RequireRequestCausation(exact, ActionId);
            string before = JsonUtility.ToJson(restored.CaptureRoundTrip());
            _ = restored.Service.TryExecute(CreateRequest(), out _, out _);
            Require(string.Equals(before,
                    JsonUtility.ToJson(restored.CaptureRoundTrip()),
                    StringComparison.Ordinal)
                    && grantState.CommitAttempts == 2
                    && grantState.SuccessfulCommits == 1,
                "Completed save/restore replay changed an owner or duplicated a child.");
        }

        return new ScenarioReport
        {
            passed = true,
            scenario = "G08 V20 content child-commit recovery",
            branches = new List<string>
            {
                "grant-failure-after-money-and-consumption",
                "save-restore-item-costs-committed",
                "grant-recovery-parent-reject",
                "save-restore-typed-effects-applied",
                "parent-only-retry",
                "terminal-replay-no-duplicate",
                "exact-child-identities-in-parent-ledger",
                "ui-action-raw-identity-and-stable-ledger-root",
                "completed-save-restore-replay-no-duplicate"
            },
            saveRestoreRoundTrips = 3,
            initialMoney = StartingMoney,
            finalMoney = StartingMoney + MoneyReward,
            consumedItemInitial = 1,
            consumedItemFinal = 0,
            grantedItemFinal = 1,
            initialHealth = initialHealth,
            finalHealth = initialHealth + HealthReward,
            exactGrantCommitAttempts = grantState.CommitAttempts,
            exactGrantSuccessfulCommits = grantState.SuccessfulCommits,
            parentOutcomeCommits = 1,
            moneyTransactionId = moneyTransactionId,
            itemConsumptionOperationId = consumptionOperationId,
            itemGrantBatchCommitId = grantBatchCommitId,
            parentOutcomeId = parentIdentity.OutcomeId.ToString(),
            parentEffectEvidence = effectEvidence,
            rawActionId = ActionId,
            parentRootOperationId = "content-resolution:"
                + NarrativeInferenceHash.ComputeSha256Utf8(ActionId),
            fullAuditClosure = false,
            humanApprovalClaimed = false,
            trainingEligible = false
        };
    }

    private static void RequireRequestCausation(
        GameplayOutcomeSnapshot outcome, string actionId)
    {
        Require(string.Equals(outcome.rootOperationId,
                    "content-resolution:"
                        + NarrativeInferenceHash.ComputeSha256Utf8(actionId),
                    StringComparison.Ordinal)
                && new GameplayOperationId(outcome.rootOperationId).IsValid
                && outcome.facts.Any(value => value != null
                    && string.Equals(value.factId,
                        MigratedProducerOutcomeIds.OperationFact.Value,
                        StringComparison.Ordinal)
                    && string.Equals(value.value, actionId, StringComparison.Ordinal)),
            "Content resolution lost its exact request ID or deterministic ledger root.");
    }

    private static ContentResolutionRequest CreateRequest() => new()
    {
        ActionId = ActionId,
        Kind = ContentResolutionRequestKind.CulturalPractice,
        DefinitionId = PracticeId,
        ParticipantCharacterIds = new[] { ActorId },
        AbsoluteDay = AbsoluteDay,
        Requirements = new RunMilestoneEvaluationSnapshot
        {
            AbsoluteDay = AbsoluteDay
        }
    };

    private static V20ContentResolutionRecoverySaveData RequirePending(
        Fixture fixture,
        V20ContentResolutionRecoveryPhase phase)
    {
        V20ContentResolutionRecoverySaveData pending = fixture.Campaign
            .CaptureMilestones()
            .pendingContentResolutions
            .SingleOrDefault(value => value != null && string.Equals(
                value.actionId,
                ActionId,
                StringComparison.Ordinal));
        Require(pending != null && pending.phase == phase,
            "Durable content recovery phase mismatch. Expected " + phase + ".");
        return pending;
    }

    private static V20EffectApplicationEvidence RequireApplication(
        ContentResolutionResult result,
        V20ContentEffectKind kind,
        string targetId)
    {
        V20ResolvedEventResult resolution = result.Resolutions.Single();
        return resolution.Applications.Single(application =>
            application != null
            && application.effectIndex >= 0
            && application.effectIndex < resolution.Effects.Count
            && resolution.Effects[application.effectIndex].kind == kind
            && string.Equals(
                application.targetId,
                targetId,
                StringComparison.Ordinal));
    }

    private static string ApplicationFact(GameplayOutcomeSnapshot outcome) =>
        outcome.facts.Single(value => value != null && string.Equals(
            value.factId,
            MigratedProducerOutcomeIds.EffectApplicationFact.Value,
            StringComparison.Ordinal)).value;

    private static void WriteReceiptEvidence<T>(string filename, T data)
    {
        string directory = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.FindIndex(args, value => value.Equals("-logFile", StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("Missing isolated evidence directory.");
            directory = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        }
        Directory.CreateDirectory(directory);
        using var stream = new FileStream(Path.Combine(directory, filename), FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(JsonUtility.ToJson(data, true));
    }

    private static void WriteReport(ScenarioReport report)
    {
        string output = Environment.GetEnvironmentVariable(
            OutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(output))
            return;

        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonUtility.ToJson(report, prettyPrint: true));
        writer.Write('\n');
    }

    private static void WriteG04Report(G04ScenarioReport report)
    {
        string output = Environment.GetEnvironmentVariable(
            OutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(output))
            return;

        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonUtility.ToJson(report, prettyPrint: true));
        writer.Write('\n');
    }

    private static T RoundTrip<T>(T value) where T : class
    {
        string json = JsonUtility.ToJson(
            value ?? throw new ArgumentNullException(nameof(value)));
        T restored = JsonUtility.FromJson<T>(json);
        return restored ?? throw new InvalidOperationException(
            "JSON round-trip returned a null " + typeof(T).Name + ".");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterSO actorData;

        internal Fixture(
            FixtureContent content,
            SharedGrantState grantState,
            RecoverySaveBundle restore = null,
            MigratedProducerOutcomeFaultMode parentFault =
                MigratedProducerOutcomeFaultMode.None)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Root = new DungeonRuntimeAggregateRootStore();
            Outcomes = new MigratedProducerOutcomeEditorFixture(RunId, Root);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind.V20ResolvedEventResult,
                FaultMode = parentFault
            };
            Facts = new SocialRitualFactEditorFixture(Outcomes, Probe);
            Transport = new ObservationTransport(Facts.Dispatcher);
            Journal = new SocialRitualFactJournal(Transport, Transport);

            GameObject actorObject = new("G08 Content Resolution Actor");
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "G08 복구 검증 슬라임",
                "Slime");
            Actor.Initialization(actorData);
            Actor.Identity.SetPersistentId(ActorId);
            CharacterAiEditorTestDependencies.WorldRegistry
                .RegisterCharacterLifetime(Actor);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                Actor);
            Characters = new FixtureCharacterWorld(Actor);

            Session = new RestorableSessionProvider(
                new GameSessionSnapshot(
                    StartingMoney,
                    AbsoluteDay,
                    1,
                    0f,
                    0,
                    TimeOfDay.Night,
                    false));
            EconomyStore = new TreasuryEconomyAggregateStateStore(Root);
            Economy = new EconomyTransactionLedgerRuntime(
                CharacterAiEditorTestDependencies.GameClock,
                EconomyStore);
            Money = new GameMoneyAccount(Session, Economy);

            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                content.ItemCatalog,
                Root,
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _,
                out _,
                out _);
            Repository = repository;
            QuantityReservations = quantityReservations;
            Reservations = new ItemReservationService(
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                QuantityReservations);
            AtomicItems = new AtomicItemConsumptionService(
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                QuantityReservations);
            Stock = new PhysicalStockQuery(
                Repository,
                content.ItemCatalog,
                Items.MassQuery);
            ExactSources = new FailOnceExactSourcePublication(
                Repository, content.ItemCatalog, Items,
                grantState ?? throw new ArgumentNullException(nameof(grantState)));

            Health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.GameClock,
                CharacterAiEditorTestDependencies.GameEvents,
                new DynamicFrameWorkBudget(
                    CharacterAiEditorTestDependencies.GameClock,
                    CharacterAiEditorTestDependencies.UiClock),
                new ResourceAnatomyProfileCatalog(
                    content.Source.GetAll<AnatomyProfileSO>()),
                Root,
                CharacterAiEditorTestDependencies.GameCalendar);
            Narrative = new CharacterNarrativeRuntime(
                Root,
                content.NarrativeCatalog,
                content: content.Source);
            Careers = new CareerRuntime(Root);
            Grief = new GriefTraumaRuntime(Root);
            Diseases = new ResourceDiseaseDefinitionCatalog(content.Base);
            PopulationHealth = new PopulationHealthRuntime(
                Root,
                Diseases,
                NeutralDiseaseModifiers.Instance,
                CharacterAiEditorTestDependencies.GameCalendar,
                new RandomStreamProvider(rootSeed: 9173));
            Campaign = new V20CampaignRuntime(
                Root,
                content.StoryCatalog,
                null,
                EmptySeasonalFeedTargetQuery.Instance);

            if (restore == null)
            {
                Repository.AddEditorTestStack(
                    ConsumedItemId,
                    1,
                    WorldItemStackState.Loose);
                Health.ConfigureVitals(Actor, 100f, resetCurrentHealth: true);
                Health.ApplyLegacyDamage(
                    Actor,
                    10f,
                    "g08-fixture-damage",
                    allowDeath: false);
                Narrative.Register(
                    new CharacterId(ActorId),
                    new CharacterSpeciesId("Slime"),
                    Array.Empty<string>(),
                    Array.Empty<string>());
                if (content.IncludeG04Effects)
                {
                    Narrative.Register(
                        new CharacterId(MissingActorId),
                        new CharacterSpeciesId("Slime"),
                        Array.Empty<string>(),
                        Array.Empty<string>());
                }
            }
            else
            {
                Restore(restore);
            }

            Service = new V20ContentResolutionService(
                Campaign,
                content.StoryCatalog,
                content.IncludeG04Effects
                    ? G04Requirements.Instance
                    : AlwaysSatisfiedRequirements.Instance,
                Characters,
                Stock,
                AtomicItems,
                Journal,
                Narrative,
                ExactSources,
                content.NarrativeCatalog,
                Reservations,
                Items,
                FixedDropZone.Instance,
                Money,
                Health,
                Careers,
                Careers,
                PopulationHealth,
                Diseases,
                Grief);
        }

        internal FixtureContent Content { get; }
        internal DungeonRuntimeAggregateRootStore Root { get; }
        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal SocialRitualFactEditorFixture Facts { get; }
        internal ObservationTransport Transport { get; }
        internal SocialRitualFactJournal Journal { get; }
        internal CharacterActor Actor { get; }
        internal FixtureCharacterWorld Characters { get; }
        internal RestorableSessionProvider Session { get; }
        internal TreasuryEconomyAggregateStateStore EconomyStore { get; }
        internal EconomyTransactionLedgerRuntime Economy { get; }
        internal GameMoneyAccount Money { get; }
        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal ItemQuantityReservationService QuantityReservations { get; }
        internal ItemReservationService Reservations { get; }
        internal AtomicItemConsumptionService AtomicItems { get; }
        internal PhysicalStockQuery Stock { get; }
        internal FailOnceExactSourcePublication ExactSources { get; }
        internal CharacterBodyHealthRuntime Health { get; }
        internal CharacterNarrativeRuntime Narrative { get; }
        internal CareerRuntime Careers { get; }
        internal GriefTraumaRuntime Grief { get; }
        internal ResourceDiseaseDefinitionCatalog Diseases { get; }
        internal PopulationHealthRuntime PopulationHealth { get; }
        internal V20CampaignRuntime Campaign { get; }
        internal V20ContentResolutionService Service { get; }

        internal RecoverySaveBundle CaptureRoundTrip()
        {
            ExactSources.ValidateBeforeCapture();
            Journal.AssertIdle();
            foreach (var row in Campaign.CaptureMilestones().pendingContentResolutions)
                V20ContentRecoveryEvidence.RequirePhysicalReceipts(row, Items);
            return new RecoverySaveBundle
            {
                session = RoundTrip(GameSessionSaveData.From(
                    Session.State.Capture())),
                economy = RoundTrip(Economy.Capture()),
                physicalItems = RoundTrip(Items.Capture()),
                bodyHealth = RoundTrip(Health.Capture()),
                narrative = RoundTrip(Narrative.Capture()),
                careers = RoundTrip(Careers.Capture()),
                psychosocial = RoundTrip(Grief.Capture()),
                populationHealth = RoundTrip(PopulationHealth.Capture()),
                seasonal = RoundTrip(Campaign.CaptureSeasonal()),
                society = RoundTrip(Campaign.CaptureSociety()),
                factions = RoundTrip(Campaign.CaptureFactions()),
                milestones = RoundTrip(Campaign.CaptureMilestones()),
                gameplayOutcomes = RoundTrip(
                    Outcomes.Ledger.CaptureGameplayOutcomes()),
                migratedProducerOutcomes = RoundTrip(
                    Outcomes.Transaction.Capture()),
                observations = RoundTrip(Journal.Capture())
            };
        }

        internal void ValidateRestoreBoundary(RecoverySaveBundle save)
        {
            IDungeonSaveRestoreStage physical = null;
            try
            {
                physical = Items.StageTransactionalRestore(save.physicalItems, EmptyRestoreWorld.Instance);
                var section = new RunMilestonesSaveSection(Campaign, Items);
                var report = new DungeonGameRestoreReport();
                _ = section.StageRestore(JsonUtility.ToJson(save.milestones), section.SectionVersion, report);
                if (!report.Success) throw new InvalidOperationException("Milestone stage rejected physical receipts: " + report);
            }
            finally
            {
                (physical as IDungeonDiscardableSaveRestoreStage)?.Discard();
                Items.DiscardRestoreCandidate();
            }
        }

        private void Restore(RecoverySaveBundle save)
        {
            ValidateRestoreBoundary(save);
            Session.Restore(save.session.ToSnapshot());
            Economy.PopulateRestoreState(EconomyStore.Current, save.economy);
            Items.Restore(save.physicalItems);
            Health.PublishRestore(Health.PrepareRestore(save.bodyHealth));
            Narrative.PublishRestore(Narrative.PrepareRestore(save.narrative));
            Careers.PublishRestore(Careers.PrepareRestore(save.careers));
            Grief.PublishRestore(Grief.PrepareRestore(save.psychosocial));
            PopulationHealth.PublishRestore(PopulationHealth.PrepareRestore(
                save.populationHealth));
            Campaign.PublishSeasonal(Campaign.PrepareSeasonal(save.seasonal));
            Campaign.PublishSociety(Campaign.PrepareSociety(save.society));
            Campaign.PublishFactions(Campaign.PrepareFactions(save.factions));
            Campaign.PublishMilestones(Campaign.PrepareMilestones(
                save.milestones));
            Outcomes.Transaction.Restore(
                Outcomes.Transaction.PrepareRestore(
                    save.migratedProducerOutcomes));

            Outcomes.Ledger.BeginRestoreCandidate();
            GameplayOutcomeLedgerRestoreCandidate outcomeCandidate =
                Outcomes.Ledger.PrepareGameplayOutcomeRestore(
                    save.gameplayOutcomes);
            Outcomes.Ledger.PublishGameplayOutcomeRestore(outcomeCandidate);
            Outcomes.Ledger.PublishRestoreCandidate();
            Outcomes.Ledger.CompleteRestoreCandidate();
            Journal.Restore(save.observations);
        }

        public void Dispose()
        {
            Journal.AssertIdle();
            Facts.Dispose();
            if (Actor != null)
            {
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacter(Actor);
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacterLifetime(Actor);
                UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            }
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            Session.Dispose();
        }
    }

    private sealed class FixtureContent : IDisposable
    {
        private readonly CulturalPracticeDefinitionSO clonedPractice;

        internal FixtureContent(bool includeG04Effects = false)
        {
            IncludeG04Effects = includeG04Effects;
            Base = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            CulturalPracticeDefinitionSO authored = Base
                .GetAll<CulturalPracticeDefinitionSO>()
                .Single(value => string.Equals(
                    value.StableId,
                    PracticeId,
                    StringComparison.Ordinal));
            clonedPractice = UnityEngine.Object.Instantiate(authored);
            clonedPractice.successEffects = authored.successEffects
                .Where(value => value != null)
                .Select(CloneEffect)
                .ToList();
            if (includeG04Effects)
            {
                clonedPractice.successEffects.Add(new V20ContentEffect
                {
                    kind = V20ContentEffectKind.DiseaseExposure,
                    targetId = DiseaseId,
                    amount = 30f
                });
                clonedPractice.successEffects.Add(new V20ContentEffect
                {
                    kind = V20ContentEffectKind.Threat,
                    targetId = PressureId,
                    amount = 6f
                });
            }
            else
            {
                clonedPractice.successEffects.Add(new V20ContentEffect
                {
                    kind = V20ContentEffectKind.Money,
                    amount = MoneyReward
                });
                clonedPractice.successEffects.Add(new V20ContentEffect
                {
                    kind = V20ContentEffectKind.ItemGrant,
                    targetId = GrantedItemId,
                    amount = 1f
                });
            }
            Source = new CloneDefinitionSource(
                Base,
                authored,
                clonedPractice);
            StoryCatalog = new V20StoryContentCatalog(Source);
            NarrativeCatalog = new CharacterNarrativeCatalog(Source);
            ItemCatalog = new ResourceDungeonItemCatalogProvider(
                new ResourceItemDefinitionCatalog(
                    Source.GetAll<ItemDefinitionSO>()));
        }

        internal ResourceGameContentCatalog Base { get; }
        internal bool IncludeG04Effects { get; }
        internal IGameContentDefinitionSource Source { get; }
        internal V20StoryContentCatalog StoryCatalog { get; }
        internal CharacterNarrativeCatalog NarrativeCatalog { get; }
        internal IDungeonItemCatalogProvider ItemCatalog { get; }

        public void Dispose()
        {
            if (clonedPractice != null)
                UnityEngine.Object.DestroyImmediate(clonedPractice);
        }
    }

    private sealed class CloneDefinitionSource : IGameContentDefinitionSource
    {
        private readonly IGameContentDefinitionSource inner;
        private readonly CulturalPracticeDefinitionSO original;
        private readonly CulturalPracticeDefinitionSO replacement;

        internal CloneDefinitionSource(
            IGameContentDefinitionSource inner,
            CulturalPracticeDefinitionSO original,
            CulturalPracticeDefinitionSO replacement)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.original = original
                ?? throw new ArgumentNullException(nameof(original));
            this.replacement = replacement
                ?? throw new ArgumentNullException(nameof(replacement));
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = inner.GetAll<T>();
            if (typeof(T) != typeof(CulturalPracticeDefinitionSO))
                return values;
            return values.Select(value => ReferenceEquals(value, original)
                    ? (T)(ScriptableObject)replacement
                    : value)
                .ToArray();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            return values.Count == 1
                ? values[0]
                : throw new InvalidOperationException(
                    "Expected one " + typeof(T).Name + ", found "
                    + values.Count + ".");
        }
    }

    private sealed class RestorableSessionProvider :
        IGameSessionStateStore,
        IDisposable
    {
        private readonly GameData settings;
        private readonly IGameSessionStateMutation mutation;

        internal RestorableSessionProvider(GameSessionSnapshot initial)
        {
            settings = ScriptableObject.CreateInstance<GameData>();
            State = GameSessionState.Create(settings, out mutation);
            mutation.Restore(initial);
        }

        internal GameSessionState State { get; }

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = State;
            return true;
        }

        public void Restore(GameSessionSnapshot snapshot) =>
            mutation.Restore(snapshot);

        public void Dispose()
        {
            if (settings != null)
                UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private sealed class FixtureCharacterWorld : ICharacterWorldQuery
    {
        private readonly CharacterActor actor;

        internal FixtureCharacterWorld(CharacterActor actor) => this.actor =
            actor ?? throw new ArgumentNullException(nameof(actor));

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters =>
            new[] { actor };
    }

    private sealed class RejectReceiptAcknowledgement : IPhysicalItemDispositionAcknowledgementParticipant
    {
        public bool RolledBack { get; private set; }
        public bool TryCommit(out string reason) { reason = "injected-parent-ack-rejection"; return false; }
        public void Rollback() { RolledBack = true; }
    }

    private sealed class EmptyRestoreWorld : IRestoreWorldCandidateQuery
    {
        internal static readonly EmptyRestoreWorld Instance = new();
        public int Revision => 1;
        public bool TryGetBuildings(out IReadOnlyList<BuildableObject> value) { value = Array.Empty<BuildableObject>(); return true; }
        public bool TryGetGrid(out Grid value) { value = null; return false; }
        public bool TryGetCharacters(out IReadOnlyList<CharacterActor> value) { value = Array.Empty<CharacterActor>(); return true; }
        public bool TryGetWildlife(out IReadOnlyList<WildlifeActor> value) { value = Array.Empty<WildlifeActor>(); return true; }
        public bool TryGetExteriorZones(out IReadOnlyList<ExteriorZoneMarker> value) { value = Array.Empty<ExteriorZoneMarker>(); return true; }
    }

    private sealed class SharedGrantState
    {
        internal bool FailNextCommit { get; set; } = true;
        internal bool LoseNextCommitReply { get; set; }
        internal int CommitAttempts { get; set; }
        internal int SuccessfulCommits { get; set; }
        internal HashSet<string> CommittedBatchIds { get; } =
            new(StringComparer.Ordinal);
    }

    private sealed class FailOnceExactSourcePublication :
        IPhysicalItemExactSourcePublicationService, IFacilityBufferDestinationReleaseService
    {
        private readonly PhysicalItemExactSourcePublicationService actual;
        private readonly SharedGrantState shared;

        internal FailOnceExactSourcePublication(WorldItemRepository repository,
            IDungeonItemCatalogProvider catalog, WorldItemStackRuntime items, SharedGrantState shared)
        {
            this.shared = shared;
            var claims = new FacilityBufferDestinationClaimRegistry();
            var admission = new FacilityBufferMassAdmissionService(claims,
                new FacilityBufferPlannedOutputPublicationDebugScenarios.EmptyOccupancy(), items.MassQuery);
            var lifecycle = new FacilityBufferDestinationLifecycleService(claims, claims, admission, admission);
            var publication = new FacilityBufferPlannedOutputPublicationService(repository, catalog,
                items.MassQuery, admission);
            actual = new PhysicalItemExactSourcePublicationService(repository, items.MassQuery,
                lifecycle, claims, admission, admission, publication, this,
                new PhysicalItemBatchDispositionService(repository, items.MassQuery,
                    EditorNullItemMarkerPresenter.Instance, null),
                EmptyFacilityBufferAcknowledgedOutputRestoreCandidateQuery.Instance);
        }

        public void ValidateBeforeCapture() => actual.ValidateBeforeCapture();

        public bool TryPrepare(PhysicalItemExactSourcePublicationPlan plan,
            out PhysicalItemExactSourcePublicationTransaction transaction, out string failureReason) =>
            actual.TryPrepare(plan, out transaction, out failureReason);

        public bool TryCommitReleased(PhysicalItemExactSourcePublicationTransaction transaction,
            Vector2Int releasePosition, string reasonCode,
            out PhysicalItemExactSourcePublicationReceipt receipt, out string failureReason)
        {
            shared.CommitAttempts++;
            if (shared.FailNextCommit)
            {
                shared.FailNextCommit = false;
                receipt = default;
                failureReason = "injected-g08-grant-publication-failure";
                return false;
            }
            if (!actual.TryCommitReleased(transaction, releasePosition, reasonCode, out receipt, out failureReason))
                return false;
            if (shared.CommittedBatchIds.Add(transaction.BatchCommitId)) shared.SuccessfulCommits++;
            if (shared.LoseNextCommitReply)
            {
                shared.LoseNextCommitReply = false;
                receipt = default;
                failureReason = "injected-lost-successful-grant-reply";
                return false;
            }
            return true;
        }

        public bool TryRollback(PhysicalItemExactSourcePublicationTransaction transaction,
            string reasonCode, out string failureReason) => actual.TryRollback(transaction, reasonCode, out failureReason);
        public bool TryCommitRetained(PhysicalItemExactSourcePublicationTransaction transaction,
            out PhysicalItemExactSourcePublicationReceipt receipt, out string failureReason) =>
            actual.TryCommitRetained(transaction, out receipt, out failureReason);
        public bool TryCommitReleased(PhysicalItemExactSourcePublicationTransaction transaction,
            FacilityBufferAcknowledgedOutputReleaseTarget target, string reasonCode,
            out PhysicalItemExactSourcePublicationReceipt receipt, out string failureReason) =>
            actual.TryCommitReleased(transaction, target, reasonCode, out receipt, out failureReason);
        public bool TryReleaseRetained(PhysicalItemExactSourcePublicationPlan plan,
            Vector2Int releasePosition, string reasonCode, out int releasedQuantity, out string failureReason) =>
            actual.TryReleaseRetained(plan, releasePosition, reasonCode, out releasedQuantity, out failureReason);
        public bool TrySinkRetained(PhysicalItemExactSourcePublicationPlan plan,
            string reasonCode, out PhysicalItemBatchDispositionReceipt disposition, out string failureReason) =>
            actual.TrySinkRetained(plan, reasonCode, out disposition, out failureReason);
        public bool TryReleaseAtOwnerPosition(string destinationId, Vector2Int ownerPosition,
            string reasonCode, out int releasedQuantity, out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = "fixture-does-not-support-retained-output";
            return false;
        }
    }

    private sealed class AlwaysSatisfiedRequirements :
        IContentRequirementEvaluator
    {
        internal static readonly AlwaysSatisfiedRequirements Instance = new();

        public bool TryEvaluate(
            V20ContentRequirementSet requirements,
            RunMilestoneEvaluationSnapshot world,
            IReadOnlyList<string> participantCharacterIds,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool IsLivingCharacterAtStage(
            CharacterId characterId,
            CharacterLifeStage lifeStage) => false;
    }

    private sealed class G04Requirements : IContentRequirementEvaluator
    {
        internal static readonly G04Requirements Instance = new();

        public bool TryEvaluate(
            V20ContentRequirementSet requirements,
            RunMilestoneEvaluationSnapshot world,
            IReadOnlyList<string> participantCharacterIds,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool IsLivingCharacterAtStage(
            CharacterId characterId,
            CharacterLifeStage lifeStage) =>
            characterId.IsValid && lifeStage == CharacterLifeStage.Elder;
    }

    private sealed class NeutralDiseaseModifiers :
        IPopulationDiseaseModifierQuery
    {
        internal static readonly NeutralDiseaseModifiers Instance = new();

        public PopulationDiseaseStatModifiers Resolve(
            CharacterId characterId,
            DiseaseDefinition disease) =>
            PopulationDiseaseStatModifiers.Neutral;
    }

    private sealed class FixedDropZone : IWorldDropZoneQuery
    {
        internal static readonly FixedDropZone Instance = new();

        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = new Vector2Int(3, 4);
            return true;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }

        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class EmptySeasonalFeedTargetQuery :
        ISeasonalFeedSelfHeatingTargetQuery
    {
        internal static readonly EmptySeasonalFeedTargetQuery Instance = new();

        public bool TrySelect(
            SeasonalFeedSelfHeatingFireProfile profile,
            out SeasonalFeedSelfHeatingTarget target,
            out SeasonalFeedSelfHeatingTargetFailure reason)
        {
            target = default;
            reason = default;
            return false;
        }

        public bool TryRevalidate(
            in SeasonalFeedSelfHeatingTarget selected,
            SeasonalFeedSelfHeatingFireProfile profile,
            out SeasonalFeedSelfHeatingTarget current,
            out SeasonalFeedSelfHeatingTargetFailure reason)
        {
            current = default;
            reason = default;
            return false;
        }
    }

    [Serializable]
    private sealed class RecoverySaveBundle
    {
        public GameSessionSaveData session;
        public EconomyTransactionLedgerSaveData economy;
        public DungeonPhysicalItemSaveData physicalItems;
        public DungeonCharacterBodyHealthSaveData bodyHealth;
        public CharacterNarrativeWorldSaveData narrative;
        public CharacterCareerWorldSaveData careers;
        public CharacterPsychosocialWorldSaveData psychosocial;
        public PopulationHealthWorldSaveData populationHealth;
        public SeasonalEventWorldSaveData seasonal;
        public SocietyEventWorldSaveData society;
        public FactionCampaignWorldSaveData factions;
        public RunMilestoneWorldSaveData milestones;
        public GameplayOutcomeLedgerSaveData gameplayOutcomes;
        public DungeonMigratedProducerOutcomeSaveData migratedProducerOutcomes;
        public SocialRitualFactSaveData observations;
    }

    private sealed class ObservationTransport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher dispatcher;
        internal bool ThrowBeforeCommit, HideAcknowledgement;
        internal ObservationTransport(DomainFactDispatcher dispatcher) => this.dispatcher = dispatcher;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) =>
            dispatcher.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact) => dispatcher.Publish(lease, fact);
        public DomainFactDelivery Replay<T>(in T fact) => dispatcher.Replay(fact);
        public void Cancel(in DomainFactLease lease) => dispatcher.Cancel(lease);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
        {
            if (ThrowBeforeCommit) throw new InvalidOperationException("B112 controlled pre-commit transport failure");
            bool accepted = dispatcher.PublishBatch(facts, results, out failure);
            if (HideAcknowledgement && accepted)
                for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain("B112 controlled lost acknowledgement");
            return accepted;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure) =>
            dispatcher.ReplayBatch(facts, results, out failure);
    }

    [Serializable]
    private sealed class ScenarioReport
    {
        public bool passed;
        public string scenario = string.Empty;
        public string error = string.Empty;
        public List<string> branches = new();
        public int saveRestoreRoundTrips;
        public int initialMoney;
        public int finalMoney;
        public int consumedItemInitial;
        public int consumedItemFinal;
        public int grantedItemFinal;
        public float initialHealth;
        public float finalHealth;
        public int exactGrantCommitAttempts;
        public int exactGrantSuccessfulCommits;
        public int parentOutcomeCommits;
        public string moneyTransactionId = string.Empty;
        public string itemConsumptionOperationId = string.Empty;
        public string itemGrantBatchCommitId = string.Empty;
        public string parentOutcomeId = string.Empty;
        public string parentEffectEvidence = string.Empty;
        public string rawActionId = string.Empty;
        public string parentRootOperationId = string.Empty;
        public bool fullAuditClosure;
        public bool humanApprovalClaimed;
        public bool trainingEligible;
    }

    [Serializable]
    private sealed class G04ScenarioReport
    {
        public bool passed;
        public string scenario = string.Empty;
        public string error = string.Empty;
        public List<string> branches = new();
        public string practiceOutcomeId = string.Empty;
        public string retirementOutcomeId = string.Empty;
        public string practiceApplicationEvidence = string.Empty;
        public string retirementApplicationEvidence = string.Empty;
        public bool fullAuditClosure;
        public bool humanApprovalClaimed;
        public bool trainingEligible;
    }

    private static V20ContentEffect CloneEffect(V20ContentEffect value) => new()
    {
        kind = value.kind,
        targetId = value.targetId,
        amount = value.amount,
        durationDays = value.durationDays
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
