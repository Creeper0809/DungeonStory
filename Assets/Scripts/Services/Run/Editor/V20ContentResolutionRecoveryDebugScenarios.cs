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
        RunRegisteredHandlerContracts();
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

    public static void RunRegisteredHandlerContracts()
    {
        V21ContentEffectExecutionRegistry defaults = V21ContentEffectExecutionRegistry.Default;
        V20ContentEffectHandler health = defaults.Require(V20ContentEffectKind.Health);
        bool duplicateRejected = false;
        try { _ = new V21ContentEffectExecutionRegistry(defaults.Handlers.Append(health)); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Require(duplicateRejected, "Duplicate registered effect silently replaced an owner.");
        bool incompleteRejected = false;
        try
        {
            _ = new V20ContentEffectHandler(health.Kind, health.Owner, health.Semantic,
                V20ContentEffectStage.Typed, health.Observe, health.AppliedAmount);
        }
        catch (ArgumentException) { incompleteRejected = true; }
        Require(incompleteRejected, "Typed effect without execution was accepted.");

        using var content = new FixtureContent();
        var missing = new V21ContentEffectExecutionRegistry(defaults.Handlers.Where(value => value.Kind != health.Kind));
        using (var fixture = new Fixture(content, new SharedGrantState { FailNextCommit = false }, effectHandlers: missing))
        {
            string before = JsonUtility.ToJson(fixture.CaptureRoundTrip());
            Require(!fixture.Service.TryExecute(CreateRequest(), out _, out DomainFailure failure)
                    && failure.Code == FailureCode.ServiceFeatureMissing
                    && before == JsonUtility.ToJson(fixture.CaptureRoundTrip()),
                "Unregistered effect changed owners, physical receipts or pending state before rejection.");
        }
        var rejected = new V20ContentEffectHandler(health.Kind, health.Owner, health.Semantic, health.Stage,
            health.Observe, health.AppliedAmount, minimumParticipants: health.MinimumParticipants,
            participantTargeted: health.ParticipantTargeted, forwardRecovery: health.RequiresForwardRecovery,
            persistenceOwners: health.PersistenceOwners, apply: (_, _) => throw new InvalidOperationException("Rejected effect executed."),
            preflight: (_, _) => new DomainFailure(FailureCode.ExternalInfluenceUnavailable, "registered-preflight-rejected"));
        var rejectingRegistry = new V21ContentEffectExecutionRegistry(defaults.Handlers
            .Where(value => value.Kind != health.Kind).Append(rejected));
        using (var fixture = new Fixture(content, new SharedGrantState { FailNextCommit = false }, effectHandlers: rejectingRegistry))
        {
            string before = JsonUtility.ToJson(fixture.CaptureRoundTrip());
            Require(!fixture.Service.TryExecute(CreateRequest(), out _, out DomainFailure failure)
                    && failure.Code == FailureCode.ExternalInfluenceUnavailable
                    && before == JsonUtility.ToJson(fixture.CaptureRoundTrip()),
                "Registered preflight rejection was ignored or changed an owner.");
        }

        // Register a different handler under an existing wire ID: the common
        // dispatcher, save schema and owner implementations are not modified.
        int attempts = 0;
        bool failNext = true;
        var registered = new V20ContentEffectHandler(health.Kind, health.Owner, "registered-health-probe",
            health.Stage, health.Observe, health.AppliedAmount,
            minimumParticipants: health.MinimumParticipants, participantTargeted: health.ParticipantTargeted,
            forwardRecovery: health.RequiresForwardRecovery, persistenceOwners: health.PersistenceOwners,
            apply: (runtime, invocation) =>
            {
                health.Apply(runtime, invocation);
                attempts++;
                if (failNext)
                {
                    failNext = false;
                    throw new InvalidOperationException("injected-registered-effect-after-mutation");
                }
            });
        var registry = new V21ContentEffectExecutionRegistry(defaults.Handlers
            .Where(value => value.Kind != health.Kind).Append(registered));
        var grants = new SharedGrantState { FailNextCommit = false };
        RecoverySaveBundle pending;
        using (var first = new Fixture(content, grants, effectHandlers: registry))
        {
            string before = JsonUtility.ToJson(first.Health.Capture());
            bool surfaced = false;
            try { first.Service.TryExecute(CreateRequest(), out _, out _); }
            catch (InvalidOperationException ex) { surfaced = ex.Message.Contains("injected-registered-effect-after-mutation"); }
            Require(surfaced && attempts == 1 && before == JsonUtility.ToJson(first.Health.Capture()),
                "Registered effect partial mutation was not rolled back to the original authority.");
            _ = RequirePending(first, V20ContentResolutionRecoveryPhase.ItemGrantsCommitted);
            Require(first.Money.Balance == StartingMoney + MoneyReward && grants.SuccessfulCommits == 1
                    && first.Probe.CommitCount(MigratedProducerOutcomeKind.V20ResolvedEventResult) == 0,
                "Registered effect failure lost committed physical children or published a false parent success.");
            pending = first.CaptureRoundTrip();
        }

        RecoverySaveBundle completed;
        using (var restored = new Fixture(content, grants, pending, effectHandlers: registry))
        {
            Require(restored.Service.TryExecute(CreateRequest(), out _, out DomainFailure failure) && !failure.IsFailure,
                "Registered effect did not resume through the saved production journal: " + failure);
            Require(attempts == 2 && Mathf.Approximately(restored.Health.GetVitals(restored.Actor).CurrentHealth, 90f + HealthReward)
                    && grants.SuccessfulCommits == 1 && restored.Money.Balance == StartingMoney + MoneyReward
                    && restored.Stock.GetGlobalQuantity(ConsumedItemId) == 0
                    && restored.Stock.GetGlobalQuantity(GrantedItemId) == 1
                    && restored.Campaign.CaptureMilestones().pendingContentResolutions.Count == 0,
                "Registered effect recovery duplicated a child or did not apply exactly one health change.");
            var identity = MigratedProducerOutcomeE2EAssertions.RequireExact(restored.Outcomes,
                MigratedProducerOutcomeKind.V20ResolvedEventResult, restored.Probe);
            Require(restored.Outcomes.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot outcome)
                    && ApplicationFact(outcome).Contains("semantic=registered-health-probe", StringComparison.Ordinal),
                "Registered observation did not reach the authoritative parent receipt.");
            string beforeReplay = JsonUtility.ToJson(restored.CaptureRoundTrip());
            _ = restored.Service.TryExecute(CreateRequest(), out _, out _);
            Require(attempts == 2 && beforeReplay == JsonUtility.ToJson(restored.CaptureRoundTrip()),
                "Completed registered effect replay changed a state owner.");
            completed = restored.CaptureRoundTrip();
        }
        using (var restored = new Fixture(content, grants, completed, effectHandlers: registry))
        {
            string before = JsonUtility.ToJson(restored.CaptureRoundTrip());
            _ = restored.Service.TryExecute(CreateRequest(), out _, out _);
            Require(attempts == 2 && grants.SuccessfulCommits == 1
                    && before == JsonUtility.ToJson(restored.CaptureRoundTrip()),
                "Completed JSON restore replay duplicated a registered effect or its receipts.");
        }
        _ = RunG04Scenario();
        RunPrioritizedEffectBoundaries();
        Debug.Log("REGISTERED_CONTENT_EFFECTS=PASS; duplicate/missing/incomplete rejection; actual owner rollback; pending/completed JSON restore; exact-once retry; custom receipt; built-in health/exposure/pressure/retirement");
    }

    public static void RunPrioritizedEffectBoundaries()
    {
        foreach (int delta in new[] { MoneyReward, -MoneyReward })
        {
            using var content = new FixtureContent(configureEffects: effects =>
                effects.Single(value => value.kind == V20ContentEffectKind.Money).amount = delta);
            RecoverySaveBundle rejectedSave;
            RecoverySaveBundle completed;
            var grants = new SharedGrantState { FailNextCommit = false };
            int rejections = 0;
            using (var first = new Fixture(content, grants, failConsumption: true, consumptionProbe: money =>
            {
                rejections++;
                Require(money.Balance == StartingMoney + delta
                        && ((IGameMoneyTransactionQuery)money).TryGetSuccessfulTransaction(
                            delta > 0 ? EconomyTransactionKind.ContractIncome : EconomyTransactionKind.LegacyExpense,
                            "content:" + ActionId, out EconomyTransactionRecord receipt) && receipt.amount == delta,
                    "Consumption fault was not injected after the actual money/ledger commit.");
            }))
            {
                string ledger = JsonUtility.ToJson(first.Economy.Capture());
                Require(!first.Service.TryExecute(CreateRequest(), out _, out DomainFailure failure)
                        && failure.Code == FailureCode.ItemTransferConsumptionFailed && rejections == 1
                        && first.Money.Balance == StartingMoney
                        && ledger == JsonUtility.ToJson(first.Economy.Capture())
                        && first.Stock.GetGlobalQuantity(ConsumedItemId) == 1
                        && first.Stock.GetAllStacks().Where(value => value.ItemId == ConsumedItemId)
                            .Sum(value => value.AvailableQuantity) == 1
                        && first.Campaign.CaptureMilestones().pendingContentResolutions.Count == 0,
                    "Consumption rejection did not restore both money and its exact ledger checkpoint.");
                rejectedSave = first.CaptureRoundTrip();
                bool retried = first.Service.TryExecute(CreateRequest(), out _, out DomainFailure retryFailure);
                Require(retried
                        && first.Money.Balance == StartingMoney + delta
                        && first.Stock.GetGlobalQuantity(ConsumedItemId) == 0
                        && grants.SuccessfulCommits == 1,
                    $"Same-instance money retry failed: delta={delta}; success={retried}; failure={retryFailure.Code}:{string.Join(";", retryFailure.Parameters.ToArray())}; balance={first.Money.Balance}; cost={first.Stock.GetGlobalQuantity(ConsumedItemId)}; grants={grants.SuccessfulCommits}; pending={first.Campaign.CaptureMilestones().pendingContentResolutions.Count}.");
                completed = first.CaptureRoundTrip();
            }
            using (var restored = new Fixture(content, grants, completed))
            {
                string before = JsonUtility.ToJson(restored.CaptureRoundTrip());
                _ = restored.Service.TryExecute(CreateRequest(), out _, out _);
                Require(before == JsonUtility.ToJson(restored.CaptureRoundTrip()) && grants.SuccessfulCommits == 1,
                    "Completed money JSON restore/retry duplicated a child.");
            }
            var freshGrants = new SharedGrantState { FailNextCommit = false };
            using (var restored = new Fixture(content, freshGrants, rejectedSave))
                Require(restored.Service.TryExecute(CreateRequest(), out _, out _)
                        && restored.Money.Balance == StartingMoney + delta && freshGrants.SuccessfulCommits == 1,
                    "Rejected money checkpoint could not retry after JSON/fresh-owner restore.");
        }

        V21ContentEffectExecutionRegistry defaults = V21ContentEffectExecutionRegistry.Default;
        V20ContentEffectHandler flag = defaults.Require(V20ContentEffectKind.WorldFlag);
        int campaignCalls = 0;
        var registered = new V20ContentEffectHandler(flag.Kind, flag.Owner, "registered-campaign-boundary", flag.Stage,
            flag.Observe, flag.AppliedAmount,
            applyCampaign: (runtime, effect, faction) => { campaignCalls++; flag.ApplyCampaign(runtime, effect, faction); },
            resolveTarget: flag.ResolveTarget, metricUnit: MigratedProducerOutcomeIds.DayUnit,
            targetKind: MigratedProducerOutcomeIds.OperationKind, targetRole: MigratedProducerOutcomeIds.RewardRole);
        var registry = new V21ContentEffectExecutionRegistry(defaults.Handlers.Where(value => value.Kind != flag.Kind).Append(registered));
        using (var content = new FixtureContent(configureEffects: effects =>
        {
            effects.RemoveAll(value => value.kind == V20ContentEffectKind.Health);
            foreach (float amount in new[] { 2f, 3f })
                effects.Add(new V20ContentEffect { kind = V20ContentEffectKind.Health, amount = amount });
            for (int index = 0; index < 2; index++)
                effects.Add(new V20ContentEffect { kind = flag.Kind, targetId = "flag:effect-boundary", amount = 1f });
        }))
        {
            var grants = new SharedGrantState();
            RecoverySaveBundle pending;
            using (var first = new Fixture(content, grants, effectHandlers: registry))
            {
                try { first.Service.TryExecute(CreateRequest(), out _, out _); }
                catch (InvalidOperationException error) when (error.Message.Contains("injected-g08-grant-publication-failure")) { }
                _ = RequirePending(first, V20ContentResolutionRecoveryPhase.ItemCostsCommitted);
                Require(campaignCalls == 2 && first.Campaign.WorldFlags.Contains("flag:effect-boundary"),
                    "Candidate bypassed the configured campaign handler.");
                pending = first.CaptureRoundTrip();
            }
            RecoverySaveBundle completed;
            using (var restored = new Fixture(content, grants, pending, effectHandlers: registry))
            {
                Require(restored.Service.TryExecute(CreateRequest(), out ContentResolutionResult result, out _),
                    "Composed effects did not recover through the production journal.");
                V20ResolvedEventResult resolution = result.Resolutions.Single();
                var health = resolution.Applications.Where(value => resolution.Effects[value.effectIndex].kind == V20ContentEffectKind.Health).ToArray();
                var flags = resolution.Applications.Where(value => resolution.Effects[value.effectIndex].kind == flag.Kind).ToArray();
                Require(campaignCalls == 2 && Mathf.Approximately(restored.Health.GetVitals(restored.Actor).CurrentHealth, 95f)
                        && health.Length == 2 && health[0].beforeValue == "90" && health[0].afterValue == "92"
                        && health[1].beforeValue == "92" && health[1].afterValue == "95"
                        && Mathf.Approximately(health[0].appliedAmount, 2f) && Mathf.Approximately(health[1].appliedAmount, 3f)
                        && health.All(value => value.status == V20EffectApplicationStatus.Applied)
                        && flags.Length == 2 && flags[0].beforeValue == "false" && flags[0].afterValue == "true"
                        && flags[1].beforeValue == "true" && flags[1].afterValue == "true"
                        && flags[0].status == V20EffectApplicationStatus.Applied && flags[1].status == V20EffectApplicationStatus.NoOp
                        && flags.All(value => value.projectionVersion == 1 && value.metricUnitId == MigratedProducerOutcomeIds.DayUnit.Value),
                    "Per-effect observations were replaced by the batch delta or replayed a committed campaign command.");
                var identity = MigratedProducerOutcomeE2EAssertions.RequireExact(restored.Outcomes,
                    MigratedProducerOutcomeKind.V20ResolvedEventResult, restored.Probe);
                Require(restored.Outcomes.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot outcome)
                        && outcome.metrics.Count(value => value.referenceId == "flag:effect-boundary") == 2
                        && outcome.metrics.Where(value => value.referenceId == "flag:effect-boundary")
                            .All(value => value.unitId == MigratedProducerOutcomeIds.DayUnit.Value)
                        && ApplicationFact(outcome).Contains("semantic=registered-campaign-boundary"),
                    "The frozen campaign registration was reinterpreted by the default fact mapper.");
                completed = restored.CaptureRoundTrip();
            }
            using (var restored = new Fixture(content, grants, completed, effectHandlers: registry))
            {
                string before = JsonUtility.ToJson(restored.CaptureRoundTrip());
                _ = restored.Service.TryExecute(CreateRequest(), out _, out _);
                Require(before == JsonUtility.ToJson(restored.CaptureRoundTrip()) && campaignCalls == 2,
                    "Completed composed-effect JSON restore replayed campaign or typed changes.");
            }
        }
        RunFactionAliasBoundary();
        Debug.Log("CONTENT_EFFECT_PRIORITY_BOUNDARIES=PASS; debit/credit consumption rejection; same/fresh-owner retry; per-effect health/campaign receipts; frozen registry projection; faction alias");
    }

    private static void RunFactionAliasBoundary()
    {
        using var content = new FixtureContent();
        RecoverySaveBundle save;
        ContentResolutionRequest request;
        using (var fixture = new Fixture(content, new SharedGrantState { FailNextCommit = false }))
        {
            LifeEventDefinitionSO definition = content.StoryCatalog.LifeEvents.Single(value => value.StableId == "life-event:inherited-debt");
            Require(definition.choices.Single(value => value.choiceId == "second").effects.Single().targetId == "affected-faction",
                "Faction boundary fixture no longer exercises the authored alias.");
            string faction = content.StoryCatalog.Arcs.First().factionId;
            const string instance = "society-event:effect-alias-boundary";
            var society = fixture.Campaign.CaptureSociety();
            society.activeEvents.Add(new V20ActiveEventSaveData
            {
                instanceId = instance, definitionId = definition.StableId, startedAbsoluteDay = AbsoluteDay,
                deadlineAbsoluteDay = AbsoluteDay + 3, generation = 1, deterministicRoll = 404u,
                contextFactionId = faction, participantCharacterIds = new List<string> { ActorId },
                riskTier = definition.riskTier, riskReason = definition.riskReason
            });
            fixture.Campaign.PublishSociety(fixture.Campaign.PrepareSociety(society));
            request = new ContentResolutionRequest
            {
                ActionId = V21ContentAlertActionIds.Society(instance, "second"), InstanceId = instance, ChoiceId = "second",
                Kind = ContentResolutionRequestKind.SocietyChoice, AbsoluteDay = AbsoluteDay,
                Requirements = new RunMilestoneEvaluationSnapshot { AbsoluteDay = AbsoluteDay }
            };
            Require(fixture.Service.TryExecute(request, out ContentResolutionResult result, out _)
                    && result.Resolutions.Single().Applications.Single().targetId == faction,
                "Faction alias did not resolve to its executing campaign authority.");
            var identity = MigratedProducerOutcomeE2EAssertions.RequireExact(fixture.Outcomes,
                MigratedProducerOutcomeKind.V20ResolvedEventResult, fixture.Probe);
            Require(fixture.Outcomes.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot outcome)
                    && outcome.metrics.Single().referenceId == faction
                    && outcome.metrics.Single().referenceKindId == MigratedProducerOutcomeIds.FactionKind.Value
                    && outcome.subjects.Any(value => value.entityKindId == MigratedProducerOutcomeIds.FactionKind.Value && value.entityId == faction)
                    && outcome.subjects.All(value => value.entityId != "affected-faction"),
                "Faction outcome still references an authored alias instead of the actual faction.");
            save = fixture.CaptureRoundTrip();
        }
        using var restored = new Fixture(content, new SharedGrantState { FailNextCommit = false }, save);
        string before = JsonUtility.ToJson(restored.CaptureRoundTrip());
        _ = restored.Service.TryExecute(request, out _, out _);
        Require(before == JsonUtility.ToJson(restored.CaptureRoundTrip()), "Faction alias terminal JSON retry changed state.");
    }

    private sealed class RejectConsumptionOnce : IIdempotentAtomicItemConsumptionService
    {
        private readonly IIdempotentAtomicItemConsumptionService inner;
        private readonly Action beforeRejection;
        private bool reject = true;
        internal RejectConsumptionOnce(IIdempotentAtomicItemConsumptionService inner, Action beforeRejection)
        { this.inner = inner; this.beforeRejection = beforeRejection; }
        public bool TryConsumeReservedOnce(IReadOnlyList<ReservedItemConsumption> consumptions, string owner,
            out PhysicalEffectReceiptSaveData receipt, out DomainFailure failure)
        {
            if (!reject) return inner.TryConsumeReservedOnce(consumptions, owner, out receipt, out failure);
            beforeRejection?.Invoke();
            reject = false;
            receipt = null;
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed, "injected-consumption-rejection");
            return false;
        }
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

    public static void RunAuthoredReferenceBoundaries()
    {
        ContentReferenceSelectionDebugScenarios.RunAll();
        var catalog = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        var culture = catalog.GetAll<SpeciesCultureDefinitionSO>().First();
        try
        {
            culture.preferredItemIds.Add("item:unregistered-reference-probe");
            bool rejected = false;
            try { catalog.ValidateNarrativeReferences(); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains(culture.StableId); }
            Require(rejected, "Broken culture item reference was accepted.");
        }
        finally { culture.preferredItemIds.Remove("item:unregistered-reference-probe"); }
        var badReward = catalog.GetAll<CharacterAmbitionDefinitionSO>()
            .Single(value => value.StableId == "ambition:break-debt").completionRewards.Single();
        string originalTarget = badReward.targetId;
        try
        {
            badReward.targetId = "faction:creditor";
            bool rejected = false;
            try { _ = new V20StoryContentCatalog(catalog); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains("faction:creditor"); }
            Require(rejected, "Legacy dangling ambition faction target was accepted.");
        }
        finally { badReward.targetId = originalTarget; }
        foreach (string ambitionId in new[] { "ambition:faction-peacemaker", "ambition:faction-champion", "ambition:break-debt" })
        {
            using var content = new FixtureContent();
            RecoverySaveBundle before, after;
            ContentResolutionRequest request;
            string faction = content.StoryCatalog.Arcs.Last().factionId;
            using (var fixture = new Fixture(content, new SharedGrantState { FailNextCommit = false }))
            {
                var ambition = content.NarrativeCatalog.Ambitions.Single(value => value.StableId == ambitionId);
                fixture.Narrative.StartAmbition(new CharacterId(ActorId), new CharacterAmbitionId(ambitionId), AbsoluteDay);
                fixture.Narrative.AddAmbitionProgress(new CharacterId(ActorId), ambition.targetProgress - 50, AbsoluteDay);
                var definition = content.StoryCatalog.LifeEvents.Single(value => value.StableId == "life-event:masterpiece-commission");
                string instance = "society-event:authored-reference:" + ambitionId;
                var society = fixture.Campaign.CaptureSociety();
                society.activeEvents.Add(new V20ActiveEventSaveData
                {
                    instanceId = instance, definitionId = definition.StableId, startedAbsoluteDay = AbsoluteDay,
                    deadlineAbsoluteDay = AbsoluteDay + 3, generation = 1, deterministicRoll = 405u,
                    contextFactionId = faction, participantCharacterIds = new List<string> { ActorId },
                    riskTier = definition.riskTier, riskReason = definition.riskReason
                });
                fixture.Campaign.PublishSociety(fixture.Campaign.PrepareSociety(society));
                request = new ContentResolutionRequest
                {
                    ActionId = V21ContentAlertActionIds.Society(instance, "first"), InstanceId = instance, ChoiceId = "first",
                    Kind = ContentResolutionRequestKind.SocietyChoice, AbsoluteDay = AbsoluteDay,
                    Requirements = new RunMilestoneEvaluationSnapshot { AbsoluteDay = AbsoluteDay }
                };
                before = fixture.CaptureRoundTrip();
            }
            using (var restored = new Fixture(content, new SharedGrantState { FailNextCommit = false }, before))
            {
                Require(restored.Service.TryExecute(request, out var result, out var failure), "Authored ambition failed: " + ambitionId + " " + failure);
                var reward = result.Resolutions.Single(value => value.DefinitionId == "ambition-reward:" + ambitionId);
                Require(reward.Applications.Single().targetId == faction, "Ambition reward lost saved event faction context.");
                Require(restored.Narrative.TryGet(new CharacterId(ActorId), out var character)
                    && character.AmbitionStatus == CharacterAmbitionStatus.Completed, "Ambition did not complete.");
                after = restored.CaptureRoundTrip();
            }
            using (var restored = new Fixture(content, new SharedGrantState { FailNextCommit = false }, after))
            {
                string exact = JsonUtility.ToJson(restored.CaptureRoundTrip());
                _ = restored.Service.TryExecute(request, out _, out _);
                Require(exact == JsonUtility.ToJson(restored.CaptureRoundTrip()), "Restored ambition retry duplicated reward.");
            }
        }
        foreach (string id in new[] { "beastkin-scent-vigil", "demon-third-bell", "harpy-dawn-chorus", "kobold-bench-feast",
                     "myconid-spore-return", "vampire-consent-cup", "vampire-incense-memory" })
        {
            string practiceId = "practice:" + id;
            using var content = new FixtureContent(practiceId: practiceId, includeFixtureEffects: false);
            RecoverySaveBundle before, after;
            var request = CreateRequest();
            request.DefinitionId = practiceId;
            request.ActionId = "content:authored-reference:" + id;
            if (content.RequiresPracticePartner) request.ParticipantCharacterIds = new[] { ActorId, ActorId + ":partner" };
            using (var fixture = new Fixture(content, new SharedGrantState { FailNextCommit = false })) before = fixture.CaptureRoundTrip();
            using (var restored = new Fixture(content, new SharedGrantState { FailNextCommit = false }, before))
            {
                Require(restored.Service.TryExecute(request, out var result, out var failure), "Authored practice failed: " + practiceId + " " + failure.Code + " " + string.Join(";", failure.Parameters.ToArray()));
                Require(result.Resolutions.Any(value => value.DefinitionId == practiceId && value.Applications.Count > 0), "Practice produced no actual effects.");
                foreach (var item in content.Practice.requirements.items)
                    Require(restored.Stock.GetGlobalQuantity(item.itemDefinitionId) == 0, "Practice did not consume its authored item once.");
                after = restored.CaptureRoundTrip();
            }
            using (var restored = new Fixture(content, new SharedGrantState { FailNextCommit = false }, after))
            {
                string exact = JsonUtility.ToJson(restored.CaptureRoundTrip());
                _ = restored.Service.TryExecute(request, out _, out _);
                Require(exact == JsonUtility.ToJson(restored.CaptureRoundTrip()), "Restored practice retry duplicated effects or consumption.");
            }
        }
        Debug.Log("CONTENT_REFERENCE_RUNTIME=PASS; authored ambition3/practice7; legacy string/int IDs; fresh JSON owners; exact duplicate retry");
    }

    public static void RunR20ProficiencyRewardContracts()
    {
        Require((int)V20ContentEffectKind.SkillExperience == 3
            && (int)V20ContentEffectKind.RetirementSchedule == 18
            && (int)V20ContentEffectKind.ProficiencyExperience == 19,
            "R20 changed an existing saved effect number.");
        var actorId = new CharacterId(ActorId);
        foreach (string ambitionId in new[] { "ambition:field-scholar", "ambition:master-healer",
                     "ambition:master-smith", "ambition:mentor-generation" })
        {
            using var content = new FixtureContent();
            CharacterAmbitionDefinitionSO ambition = content.NarrativeCatalog.Ambitions.Single(value => value.StableId == ambitionId);
            V20ContentEffect reward = ambition.completionRewards.Single();
            Require(reward.kind == V20ContentEffectKind.ProficiencyExperience && reward.amount == 100f,
                "Authored ambition still grants generic experience: " + ambitionId);
            RecoverySaveBundle saved, completed;
            ContentResolutionRequest request;
            using (var fixture = new Fixture(content, new SharedGrantState { FailNextCommit = false }))
            {
                fixture.Narrative.StartAmbition(actorId, new CharacterAmbitionId(ambitionId), AbsoluteDay);
                fixture.Narrative.AddAmbitionProgress(actorId, ambition.targetProgress - 50, AbsoluteDay);
                var definition = content.StoryCatalog.LifeEvents.Single(value => value.StableId == "life-event:masterpiece-commission");
                string instance = "society-event:r20:" + ambitionId;
                var society = fixture.Campaign.CaptureSociety();
                society.activeEvents.Add(new V20ActiveEventSaveData
                {
                    instanceId = instance, definitionId = definition.StableId, startedAbsoluteDay = AbsoluteDay,
                    deadlineAbsoluteDay = AbsoluteDay + 3, generation = 1, deterministicRoll = 405u,
                    contextFactionId = content.StoryCatalog.Arcs.Last().factionId,
                    participantCharacterIds = new List<string> { ActorId },
                    riskTier = definition.riskTier, riskReason = definition.riskReason
                });
                fixture.Campaign.PublishSociety(fixture.Campaign.PrepareSociety(society));
                request = new ContentResolutionRequest
                {
                    ActionId = V21ContentAlertActionIds.Society(instance, "first"), InstanceId = instance, ChoiceId = "first",
                    Kind = ContentResolutionRequestKind.SocietyChoice, AbsoluteDay = AbsoluteDay,
                    Requirements = new RunMilestoneEvaluationSnapshot { AbsoluteDay = AbsoluteDay }
                };
                saved = fixture.CaptureRoundTrip();
            }
            using (var restored = new Fixture(content, new SharedGrantState { FailNextCommit = false }, saved))
            {
                Require(restored.Service.TryExecute(request, out var result, out var failure),
                    "R20 ambition execution failed: " + ambitionId + " " + failure);
                var resolution = result.Resolutions.Single(value => value.DefinitionId == "ambition-reward:" + ambitionId);
                Require(resolution.Applications.Single().appliedAmount == 100f,
                    "R20 proficiency evidence did not observe the actual award.");
                completed = restored.CaptureRoundTrip();
                Require(ProficiencyXp(completed, reward.targetId) - ProficiencyXp(saved, reward.targetId) == 100000L,
                    "R20 proficiency reward changed the wrong authority or amount.");
            }
            using (var restored = new Fixture(content, new SharedGrantState { FailNextCommit = false }, completed))
            {
                string before = JsonUtility.ToJson(restored.CaptureRoundTrip());
                _ = restored.Service.TryExecute(request, out _, out _);
                Require(before == JsonUtility.ToJson(restored.CaptureRoundTrip()), "R20 restored ambition reward duplicated.");
            }
        }

        using (var legacy = new FixtureContent(configureEffects: effects =>
        {
            effects.Clear();
            effects.Add(new V20ContentEffect { kind = (V20ContentEffectKind)3,
                targetId = "proficiency:crafting", amount = 5f });
        }, includeFixtureEffects: false))
        {
            RecoverySaveBundle completed;
            using (var fixture = new Fixture(legacy, new SharedGrantState { FailNextCommit = false }))
            {
                CharacterProgressionSnapshot progression = fixture.Actor.Progression.CapturePersistentState();
                long proficiencyBefore = ProficiencyXp(fixture.CaptureRoundTrip(), "proficiency:crafting");
                Require(fixture.Service.TryExecute(CreateRequest(), out _, out var failure), "Legacy effect failed: " + failure);
                completed = fixture.CaptureRoundTrip();
                CharacterProgressionSnapshot actual = fixture.Actor.Progression.CapturePersistentState();
                Require((progression.Level != actual.Level || progression.CurrentExperience != actual.CurrentExperience)
                    && ProficiencyXp(completed, "proficiency:crafting") == proficiencyBefore,
                    "Old wire kind3 was reinterpreted as a proficiency reward.");
            }
            using (var restored = new Fixture(legacy, new SharedGrantState { FailNextCommit = false }, completed))
            {
                string before = JsonUtility.ToJson(restored.CaptureRoundTrip());
                _ = restored.Service.TryExecute(CreateRequest(), out _, out _);
                Require(before == JsonUtility.ToJson(restored.CaptureRoundTrip()), "Legacy restored effect duplicated.");
            }
        }
        var defaults = V21ContentEffectExecutionRegistry.Default;
        V20ContentEffectHandler handler = defaults.Require(V20ContentEffectKind.ProficiencyExperience);
        bool fail = true;
        var injected = new V20ContentEffectHandler(handler.Kind, handler.Owner, handler.Semantic,
            handler.Stage, handler.Observe, handler.AppliedAmount,
            minimumParticipants: handler.MinimumParticipants, participantTargeted: handler.ParticipantTargeted,
            forwardRecovery: handler.RequiresForwardRecovery, persistenceOwners: handler.PersistenceOwners,
            preflight: handler.Preflight, apply: (runtime, invocation) =>
            {
                handler.Apply(runtime, invocation);
                if (fail) { fail = false; throw new InvalidOperationException("r20-after-proficiency-award"); }
            });
        var registry = new V21ContentEffectExecutionRegistry(defaults.Handlers.Where(value => value.Kind != handler.Kind).Append(injected));
        using (var content = new FixtureContent(configureEffects: effects => effects.Add(new V20ContentEffect
        { kind = V20ContentEffectKind.ProficiencyExperience, targetId = "proficiency:crafting", amount = 100f })))
        {
            var grants = new SharedGrantState { FailNextCommit = false };
            RecoverySaveBundle pending;
            long proficiencyBefore;
            using (var fixture = new Fixture(content, grants, effectHandlers: registry, useSessionCalendar: true))
            {
                var proficiencyId = new CharacterProficiencyId("proficiency:crafting");
                fixture.Calendar.SetDateTime(AbsoluteDay, 8);
                long hour = fixture.Calendar.AbsoluteHour;
                Require(fixture.Narrative.TryGetProficiency(actorId, proficiencyId, hour, out var initial),
                    "R20 crafting authority is missing.");
                int nextRank = new[] { 100, 400, 1200, 3000 }.First(value => value > initial.CurrentExperience);
                fixture.Narrative.AddDirectExperience(actorId, proficiencyId,
                    nextRank - 1 - initial.CurrentExperience, hour, applyLearningMultiplier: false);
                proficiencyBefore = ProficiencyXp(fixture.CaptureRoundTrip(), proficiencyId.Value);
                string before = JsonUtility.ToJson(fixture.Narrative.Capture());
                bool surfaced = false;
                try { fixture.Service.TryExecute(CreateRequest(), out _, out _); }
                catch (InvalidOperationException error) { surfaced = error.Message.Contains("r20-after-proficiency-award"); }
                Require(surfaced && before == JsonUtility.ToJson(fixture.Narrative.Capture()),
                    "R20 failed proficiency command left partial narrative state.");
                pending = fixture.CaptureRoundTrip();
                var recovery = pending.milestones.pendingContentResolutions.Single();
                Require(recovery.proficiencyAwardClockVersion == 1
                    && recovery.proficiencyAwardAbsoluteHour == hour,
                    "R20 pending reward did not retain its original award time.");
            }
            foreach (bool missing in new[] { true, false })
            {
                var damaged = RoundTrip(pending).milestones.pendingContentResolutions;
                damaged.Single().proficiencyAwardClockVersion = missing ? 0 : 1;
                damaged.Single().proficiencyAwardAbsoluteHour = missing ? 0L : -1L;
                bool rejected = false;
                try { V20ContentRecoveryEvidence.ValidateMetadata(damaged); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "R20 accepted missing or negative proficiency award time.");
            }
            using (var restored = new Fixture(content, grants, pending, effectHandlers: registry, useSessionCalendar: true))
            {
                restored.Calendar.SetDateTime(AbsoluteDay + 1, 9);
                Require(restored.Service.TryExecute(CreateRequest(), out _, out var failure), "R20 pending reward did not recover: " + failure);
                Require(ProficiencyXp(restored.CaptureRoundTrip(), "proficiency:crafting") == proficiencyBefore + 100000L
                    && restored.Probe.CommitCount(MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt) == 1,
                    "R20 retry after game-time advance did not grant proficiency and promotion exactly once.");
                _ = MigratedProducerOutcomeE2EAssertions.RequireExact(restored.Outcomes,
                    MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt, restored.Probe);
            }
        }
        Debug.Log("R20_PROFICIENCY_REWARDS=PASS; authored4; effect wire compatibility; fresh JSON restore; failed mutation rollback; frozen award time; next-day pending retry; invalid clock rejection; no duplicate");
    }

    private static long ProficiencyXp(RecoverySaveBundle save, string proficiencyId) => save.narrative.characters
        .Single(value => value.characterId == ActorId).skillExperience
        .FirstOrDefault(value => value.proficiencyId == proficiencyId)?.lifetimeMilliExperience ?? 0L;

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterSO actorData;
        private readonly CharacterSO partnerData;
        private CharacterActor Partner { get; }

        internal Fixture(
            FixtureContent content,
            SharedGrantState grantState,
            RecoverySaveBundle restore = null,
            MigratedProducerOutcomeFaultMode parentFault =
                MigratedProducerOutcomeFaultMode.None,
            V21ContentEffectExecutionRegistry effectHandlers = null,
            bool failConsumption = false,
            Action<IGameMoneyAccount> consumptionProbe = null,
            bool useSessionCalendar = false)
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
                content.PracticeSpecies);
            Actor.Initialization(actorData);
            Actor.Identity.SetPersistentId(ActorId);
            CharacterAiEditorTestDependencies.WorldRegistry
                .RegisterCharacterLifetime(Actor);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                Actor);
            if (content.RequiresPracticePartner)
            {
                var partnerObject = new GameObject("Authored practice partner");
                partnerObject.AddComponent<SpriteRenderer>();
                Partner = partnerObject.AddComponent<CharacterActor>();
                CharacterAiEditorTestDependencies.Inject(partnerObject);
                partnerData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                    CharacterType.NPC, "Practice partner", content.PracticeSpecies);
                Partner.Initialization(partnerData);
                Partner.Identity.SetPersistentId(ActorId + ":partner");
                CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacterLifetime(Partner);
                CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(Partner);
            }
            Characters = new FixtureCharacterWorld(Actor, Partner);

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

            Health = new CharacterBodyHealthRuntime(new DungeonStory.Foundation.UnityRealtimeClock(),
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
                content: content.Source,
                domainFacts: Outcomes.CreateProficiencyAwardFacts(Probe));
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
                EmptySeasonalFeedTargetQuery.Instance, null, null, null, effectHandlers);

            if (restore == null)
            {
                Repository.AddEditorTestStack(
                    ConsumedItemId,
                    1,
                    WorldItemStackState.Loose);
                foreach (var item in content.Practice.requirements.items.Where(value => value.itemDefinitionId != ConsumedItemId))
                    Repository.AddEditorTestStack(item.itemDefinitionId, item.amount, WorldItemStackState.Loose);
                Health.ConfigureVitals(Actor, 100f, resetCurrentHealth: true);
                Health.ApplyLegacyDamage(
                    Actor,
                    10f,
                    "g08-fixture-damage",
                    allowDeath: false);
                Narrative.Register(
                    new CharacterId(ActorId),
                    new CharacterSpeciesId(content.PracticeSpecies),
                    Array.Empty<string>(),
                    Array.Empty<string>());
                if (Partner != null)
                {
                    Health.ConfigureVitals(Partner, 100f, resetCurrentHealth: true);
                    Narrative.Register(new CharacterId(ActorId + ":partner"),
                        new CharacterSpeciesId(content.PracticeSpecies), Array.Empty<string>(), Array.Empty<string>());
                }
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

            Calendar = useSessionCalendar
                ? new GameCalendarRuntime(Session, new GameEventBus(), CharacterAiEditorTestDependencies.GameClock)
                : CharacterAiEditorTestDependencies.GameCalendar;
            Service = DungeonCharacterRegistration.CreateContentResolutionForDebug(
                Campaign,
                content.StoryCatalog,
                content.IncludeG04Effects
                    ? G04Requirements.Instance
                    : AlwaysSatisfiedRequirements.Instance,
                Characters,
                Stock,
                failConsumption ? new RejectConsumptionOnce(AtomicItems, () => consumptionProbe?.Invoke(Money)) : AtomicItems,
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
                Grief,
                effectHandlers,
                Calendar);
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
        internal IGameCalendar Calendar { get; }
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
                actorLevel = Actor.Progression.Level,
                actorExperience = Actor.Progression.CurrentExperience,
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
                observations = RoundTrip(Journal.Capture()),
                actorSocial = RoundTrip(Actor.SocialMemory.CaptureSnapshot()),
                partnerSocial = Partner != null ? RoundTrip(Partner.SocialMemory.CaptureSnapshot()) : null
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
            Actor.SocialMemory.RestoreSnapshot(save.actorSocial);
            Actor.Progression.RestorePersistentState(save.actorLevel, save.actorExperience,
                Array.Empty<string>(), Array.Empty<string>());
            if (Partner != null) Partner.SocialMemory.RestoreSnapshot(save.partnerSocial);
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
            if (Partner != null)
            {
                CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(Partner);
                CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacterLifetime(Partner);
                UnityEngine.Object.DestroyImmediate(Partner.gameObject);
            }
            if (partnerData != null) UnityEngine.Object.DestroyImmediate(partnerData);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            Session.Dispose();
        }
    }

    private sealed class FixtureContent : IDisposable
    {
        private readonly CulturalPracticeDefinitionSO clonedPractice;
        internal bool RequiresPracticePartner { get; }

        internal FixtureContent(bool includeG04Effects = false,
            Action<List<V20ContentEffect>> configureEffects = null,
            string practiceId = PracticeId, bool includeFixtureEffects = true)
        {
            IncludeG04Effects = includeG04Effects;
            Base = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            CulturalPracticeDefinitionSO authored = Base
                .GetAll<CulturalPracticeDefinitionSO>()
                .Single(value => string.Equals(
                    value.StableId,
                    practiceId,
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
            else if (includeFixtureEffects)
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
            configureEffects?.Invoke(clonedPractice.successEffects);
            Source = new CloneDefinitionSource(
                Base,
                authored,
                clonedPractice);
            StoryCatalog = new V20StoryContentCatalog(Source);
            NarrativeCatalog = new CharacterNarrativeCatalog(Source);
            ItemCatalog = new ResourceDungeonItemCatalogProvider(
                new ResourceItemDefinitionCatalog(
                    Source.GetAll<ItemDefinitionSO>()));
            RequiresPracticePartner = !includeFixtureEffects && clonedPractice.successEffects
                .Any(effect => effect.kind == V20ContentEffectKind.Relationship);
        }

        internal ResourceGameContentCatalog Base { get; }
        internal CulturalPracticeDefinitionSO Practice => clonedPractice;
        internal string PracticeSpecies => Base.GetAll<SpeciesCultureDefinitionSO>()
            .Single(value => value.StableId == clonedPractice.cultureId).defaultSpeciesId;
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
        private readonly CharacterActor[] actors;
        internal FixtureCharacterWorld(CharacterActor actor, CharacterActor partner = null) =>
            actors = partner == null ? new[] { actor } : new[] { actor, partner };
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
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
                    EditorNullItemMarkerPresenter.Instance, null, clock: new DungeonStory.Foundation.UnityUiClock()),
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
        public CharacterSocialMemorySnapshot actorSocial;
        public int actorLevel;
        public int actorExperience;
        public CharacterSocialMemorySnapshot partnerSocial;
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
