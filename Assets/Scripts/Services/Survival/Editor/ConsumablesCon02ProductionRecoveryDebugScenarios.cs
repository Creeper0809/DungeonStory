#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

// Production owners under controlled inputs; not a whole-world or AI journey.
public static class ConsumablesCon02ProductionRecoveryDebugScenarios
{
    private const string FoodId = "food:roasted-meat";
    private const string OperationId = "consumable-operation:con02-meal";
    private const string DrugId = "drug:vitality-tonic";
    private const string SubstanceOperationId = "consumable-operation:con02-substance";

    public static void RunCon03RecoveryOrThrow()
    {
        Con03Report report = new();
        Application.LogCallback capture = (message, trace, type) =>
        {
            if (type == LogType.Error && (message == "character-consumables-meal-effects-failed:meal-effect-transaction-rolled-back:InvalidOperationException"
                || message == "character-consumables-fact-publication-rejected:con02-injected-parent-rejection"))
                report.expectedFaults++;
            else if (type is LogType.Error or LogType.Exception or LogType.Assert)
                report.unexpectedErrors.Add(message + "\n" + trace);
            else if (type == LogType.Log && message.StartsWith("character-consumables-meal-live-observers-retired:recipient-retired:", StringComparison.Ordinal))
                report.retiredObserverNotices++;
        };
        Application.logMessageReceived += capture;
        try
        {
            VerifyCon03Prephysical(report);
            VerifyCon03Detached(report, "departure-item-committed", false, false);
            VerifyCon03Detached(report, "death-item-committed", true, false);
            VerifyCon03Detached(report, "departure-effects-applied", false, true);
            Require(report.expectedFaults == 6 && report.unexpectedErrors.Count == 0 && report.retiredObserverNotices == 1,
                "CON03 unexpected runtime errors or incorrect fault/retirement count: " + string.Join(" | ", report.unexpectedErrors));
            report.passed = true;
            Debug.Log("CON03_PRODUCTION_LIFECYCLE_RECOVERY_PASS");
        }
        catch (Exception exception) { report.error = exception.ToString(); throw; }
        finally { Application.logMessageReceived -= capture; WriteReport(report); }
    }

    private static void VerifyCon03Prephysical(Con03Report report)
    {
        using Fixture fixture = new();
        var actorId = CharacterPersistentIdentity.Require(fixture.Actor);
        fixture.Actor.stats[CharacterCondition.HUNGER] = 0f;
        var command = new ConsumeMealCommand(new ConsumableOperationId(OperationId), actorId,
            fixture.Facility.RequirePersistentInstanceId(), new ItemStackId(fixture.StackId));
        Require(!fixture.Runtime.TryConsumeMeal(command, out var pending) && pending.IsAcceptedPending,
            "CON03 prephysical meal did not begin: " + pending.FailureCode
                + ";" + string.Join(",", pending.Parameters));
        fixture.RoundTripConsumables(); // No Tick: the transient lease ID is not rebound yet.
        Require(fixture.Reservations.TryGetLeasesByOwner(OperationId, out var leases) && leases.Count == 1,
            "CON03 restored prephysical meal must still own its physical reservation.");
        fixture.Actor.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
        Require(fixture.Cancellation.Calls == 1 && fixture.Runtime.Capture().activeMealPlans.Count == 0
                && fixture.Items.GetAllStacks().Single(value => value.StackId == fixture.StackId).Quantity == 1
                && !fixture.Batch.TryGetPending(OperationId, out _) && fixture.ParentCount == 0
                && !fixture.Reservations.TryGetLeasesByOwner(OperationId, out _),
            "Real lifecycle cancellation did not release the prephysical meal.");
        fixture.RoundTripConsumables();
        Require(!fixture.Runtime.TryConsumeMeal(command, out var failed) && !failed.IsAcceptedPending,
            "Saved prephysical cancellation restarted the same operation.");
        report.prephysicalLifecycleAbortAndReplay = true;
    }

    private static void VerifyCon03Detached(Con03Report report, string name, bool kill, bool applied)
    {
        using Fixture fixture = new();
        var actor = fixture.Actor;
        var actorId = CharacterPersistentIdentity.Require(actor);
        actor.stats[CharacterCondition.HUNGER] = 0f;
        var command = new ConsumeMealCommand(new ConsumableOperationId(OperationId), actorId,
            fixture.Facility.RequirePersistentInstanceId(), new ItemStackId(fixture.StackId));
        Require(!fixture.Runtime.TryConsumeMeal(command, out var pending) && pending.IsAcceptedPending,
            "CON03 meal did not begin: " + name);
        fixture.Clock.Advance(4.1f);
        fixture.Runtime.Tick();
        var physical = RequirePhase(fixture, CharacterMealPlanPhase.ItemCommitted);
        RequirePhysicalPending(fixture, physical);
        if (applied)
        {
            fixture.Runtime.Tick();
            RequirePhase(fixture, CharacterMealPlanPhase.EffectsApplied);
        }
        int damageCalls = fixture.Fault.DamageCalls;
        int cancellationBefore = fixture.Cancellation.Calls;
        if (kill) fixture.Health.Kill(actor, "con03-test-death-after-consumption");
        else actor.SetLifecycleState(CharacterLifecycleState.Despawned);
        Require(fixture.Cancellation.Calls == cancellationBefore + 1 && !actor.CanRunAi,
            "Actual actor lifecycle did not call the production meal cancellation gateway: " + name);
        float health = actor.Stats.CurrentHealth;
        float hunger = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
        string mood = MoodState(actor);
        string narrative = JsonUtility.ToJson(actor.Progression.NarrativeLedger);
        fixture.DetachActorAndFacility();
        fixture.Runtime.ReconcilePersistentActorReferences(Array.Empty<CharacterId>());
        var decided = RequirePhase(fixture, CharacterMealPlanPhase.EffectsApplied);
        Require(applied == string.IsNullOrEmpty(decided.effectsSkippedReason)
                && decided.effectEvidenceFrozen && decided.physicalContextFrozen,
            "Cancellation lost the exact effect decision: " + name);
        RequireRejectedCon03Payload(fixture, value => value.activeMealPlans[0].physicalContextFrozen = false);
        RequireRejectedCon03Payload(fixture, value => value.activeMealPlans[0].physicalCommitInputMassGrams++);
        if (!applied)
            RequireRejectedCon03Payload(fixture, value => value.activeMealPlans[0].hungerAfter++);
        fixture.RoundTripConsumables();
        if (!applied)
        {
            fixture.Runtime.Tick(); // first parent rejection; physical child remains durable
            RequirePhase(fixture, CharacterMealPlanPhase.EffectsApplied);
            Require(fixture.ParentCount == 0 && fixture.Parent.Rejected == 1,
                "Partial parent rejection did not retain its recovery boundary.");
            fixture.RoundTripConsumables();
        }
        fixture.Tare.RejectNext = true;
        fixture.Runtime.Tick();
        var published = RequirePhase(fixture, CharacterMealPlanPhase.EffectsPublished);
        Require(fixture.ParentCount == 1 && fixture.Batch.TryGetPending(OperationId, out _),
            "ACK rejection did not preserve one published parent and its physical receipt.");
        var exact = fixture.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Single();
        Require(exact.status == (applied ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.PartiallySucceeded)
                && JsonUtility.ToJson(exact).Contains(physical.physicalCommitId)
                && (!applied || JsonUtility.ToJson(exact).Contains("liveObservers=recipient-retired")),
            "Meal parent lost its real status or exact physical identity.");
        fixture.Runtime.ReconcilePersistentActorReferences(Array.Empty<CharacterId>());
        fixture.RoundTripConsumables();
        fixture.Runtime.Tick();
        bool succeeded = fixture.Runtime.TryGetMealOperationResult(command.OperationId, out var result);
        Require(succeeded == applied && result.Success == applied && result.OutcomeResultKey.Length > 0
                && fixture.Runtime.Capture().activeMealPlans.Count == 0
                && !fixture.Batch.TryGetPending(OperationId, out _)
                && fixture.ParentCount == 1 && fixture.Fault.DamageCalls == damageCalls
                && fixture.Random.ChanceCalls == 1
                && actor.Stats.CurrentHealth == health
                && actor.Stats.GetConditionValue(CharacterCondition.HUNGER) == hunger
                && MoodState(actor) == mood && JsonUtility.ToJson(actor.Progression.NarrativeLedger) == narrative
                && fixture.Tare.LastPosition == physical.consumptionPosition,
            "Detached recovery duplicated effects, consumed again, or falsely returned success: " + name);
        string ledger = JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes());
        fixture.Runtime.ReconcilePersistentActorReferences(Array.Empty<CharacterId>());
        fixture.RoundTripConsumables();
        fixture.Runtime.Tick();
        Require(!fixture.Runtime.TryConsumeMeal(command, out _)
                && fixture.Runtime.Capture().activeMealPlans.Count == 0
                && !fixture.Batch.TryGetPending(OperationId, out _)
                && JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes()) == ledger,
            "Retired actor replay changed the physical or outcome ledger.");
        report.cases.Add(new Con03Case
        {
            name = name, passed = true, status = exact.status.ToString(),
            physicalCommitId = physical.physicalCommitId, resultKey = result.OutcomeResultKey,
            consumedQuantity = physical.physicalCommitQuantity, consumedMassGrams = physical.physicalCommitInputMassGrams,
            parentCount = fixture.ParentCount, damageAttempts = fixture.Fault.DamageCalls,
            activePlans = fixture.Runtime.Capture().activeMealPlans.Count, pendingPhysical = false,
            consumablesJsonRoundTrips = applied ? 3 : 4, actorRemovedFromRegistry = true,
            parentRejectionRecovered = true, ackRejectionRecovered = true, malformedRestoreRejected = true,
            terminalReplayUnchanged = true
        });
    }

    private static void RequireRejectedCon03Payload(Fixture fixture, Action<DungeonCharacterConsumablesSaveData> mutate)
    {
        string before = JsonUtility.ToJson(fixture.Runtime.Capture());
        var payload = JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(before);
        mutate(payload);
        bool rejected = false;
        try { fixture.Runtime.BuildRestoreCandidate(payload); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && JsonUtility.ToJson(fixture.Runtime.Capture()) == before,
            "Malformed detached meal was accepted or partially mutated the live owner.");
    }

    public static void RunCon03DepartureBaselineOrThrow()
    {
        Con03DepartureReport report = new();
        try
        {
            using Fixture fixture = new();
            var actorId = CharacterPersistentIdentity.Require(fixture.Actor);
            fixture.Actor.stats[CharacterCondition.HUNGER] = 0f;
            ConsumeMealCommand command = new(new ConsumableOperationId(OperationId),
                actorId, fixture.Facility.RequirePersistentInstanceId(), new ItemStackId(fixture.StackId));
            Require(!fixture.Runtime.TryConsumeMeal(command, out var pending) && pending.IsAcceptedPending,
                "CON03 departure fixture did not begin its real meal.");
            fixture.Clock.Advance(4.1f);
            fixture.Runtime.Tick();
            var committed = RequirePhase(fixture, CharacterMealPlanPhase.ItemCommitted);
            RequirePhysicalPending(fixture, committed);
            report.physicalCommitId = committed.physicalCommitId;
            report.physicalSourceStackId = fixture.StackId;
            report.physicalQuantity = committed.physicalCommitQuantity;
            report.phaseBeforeDeparture = committed.phase.ToString();

            // The actor remains inspectable, but the production lifecycle has
            // retired it and characters.world will not retain this live owner.
            fixture.Actor.SetLifecycleState(CharacterLifecycleState.Despawned);
            fixture.Runtime.CancelActiveMealOperations(actorId, "con03-departure-after-physical");
            fixture.Runtime.Tick();
            fixture.Runtime.Tick();
            var after = fixture.Runtime.Capture();
            report.activePlansAfterDeparture = after.activeMealPlans.Count;
            report.pendingPhysicalAfterDeparture = fixture.Batch.TryGetPending(OperationId, out _);
            report.parentCommitCount = fixture.ParentCount;
            report.parentCommitAttempts = fixture.Parent.CommitAttempts;
            report.healthAfterDeparture = fixture.Actor.Stats.CurrentHealth;
            report.actorCanRunAi = fixture.Actor.CanRunAi;
            try
            {
                fixture.Runtime.ReconcilePersistentActorReferences(Array.Empty<CharacterId>());
            }
            catch (InvalidOperationException exception)
            {
                report.captureFailure = exception.Message;
            }
            Require(!report.actorCanRunAi && report.activePlansAfterDeparture == 0
                    && !report.pendingPhysicalAfterDeparture && report.captureFailure.Length == 0,
                "CON03 departure left a physically committed meal without a persistent actor owner: "
                + JsonUtility.ToJson(report));
            report.passed = true;
            Debug.Log("CON03_DEPARTURE_RECOVERY_PASS");
        }
        catch (Exception exception)
        {
            report.error = exception.ToString();
            throw;
        }
        finally
        {
            WriteReport(report);
        }
    }

    public static void RunOrThrow()
    {
        Report report = new();
        try
        {
            VerifyMeal(report);
            VerifySubstance(report);
            report.passed = true;
            Debug.Log("CON02_PRODUCTION_EFFECT_RECOVERY_PASS");
        }
        catch (Exception exception)
        {
            report.error = exception.ToString();
            throw;
        }
        finally
        {
            WriteReport(report);
        }
    }

    public static string VerifyObserverRecoveryFocused()
    {
        var report = new Report();
        VerifyMeal(report);
        VerifySubstance(report);
        VerifyHiddenPublicationAcknowledgement(false);
        VerifyHiddenPublicationAcknowledgement(true);
        return "consumption-production-owner=PASS; meal/substance physical+effects+rejection+save/replay=PASS; "
            + "hidden-durable-ack+pending-json-recovery=2-PASS; duplicate-consumption/effects/outcomes=0; "
            + "scope=controlled-production-owners-not-AI-journey";
    }

    private static void VerifyHiddenPublicationAcknowledgement(bool substance)
    {
        using var fixture = new Fixture(substance);
        fixture.Fault.InjectedFaults = 1; // Disable the unrelated effect-fault scenario.
        fixture.Parent.Rejected = 1;
        fixture.Parent.HideNextAcknowledgement = true;
        var actorId = CharacterPersistentIdentity.Require(fixture.Actor);
        string operationId = substance ? SubstanceOperationId : OperationId;
        if (substance)
        {
            Require(fixture.Runtime.SetSubstancePolicy(actorId, "substance:vitality-tonic",
                    SubstancePolicyMode.MoodThreshold, 100f, 0).Code == CharacterConsumablesPolicyChangeCode.Changed,
                "Hidden-ACK substance policy setup failed.");
            Require(!fixture.Runtime.TryConsumeSubstance(new ConsumeSubstanceByIdCommand(
                    new ConsumableOperationId(operationId), actorId, new ConsumableItemDefinitionId(DrugId),
                    new ItemStackId(fixture.StackId), false, false), out var result)
                && result.FailureCode == CharacterConsumablesFailureCode.DeliveryPending,
                "A substance with uncertain publication was reported completed.");
        }
        else
        {
            fixture.Actor.stats[CharacterCondition.HUNGER] = 0f;
            Require(!fixture.Runtime.TryConsumeMeal(new ConsumeMealCommand(new ConsumableOperationId(operationId),
                    actorId, fixture.Facility.RequirePersistentInstanceId(), new ItemStackId(fixture.StackId)), out _),
                "Timed meal unexpectedly completed immediately.");
            fixture.Clock.Advance(4.1f);
            fixture.Runtime.Tick();
        }
        var saved = fixture.Runtime.Capture();
        bool pending = substance ? saved.activeSubstanceUsePlans.Single().publicationPending
            : saved.activeMealPlans.Single().publicationPending;
        string key = substance ? saved.activeSubstanceUsePlans.Single().publicationKey
            : saved.activeMealPlans.Single().publicationKey;
        Require(pending && fixture.ParentCount == 1 && fixture.Parent.HiddenAcknowledgements == 1
                && string.IsNullOrEmpty(saved.completedOperations.Single().outcomeResultKey)
                && fixture.Batch.TryGetPending(operationId, out _),
            "Uncertain publication did not retain exact saved fact and physical acknowledgement.");
        string body = JsonUtility.ToJson(fixture.Health.Capture());
        string narrative = JsonUtility.ToJson(fixture.Actor.Progression.NarrativeLedger);
        string ledger = JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes());
        string mood = MoodState(fixture.Actor);
        float hunger = fixture.Actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
        int rolls = fixture.Random.ChanceCalls;
        int damageCalls = fixture.Fault.DamageCalls;
        fixture.RoundTripConsumables();
        fixture.Runtime.Tick();
        var restored = fixture.Runtime.Capture();
        Require(restored.activeMealPlans.Count == 0 && restored.activeSubstanceUsePlans.Count == 0
                && restored.completedOperations.Single().outcomeResultKey == key
                && fixture.ParentCount == 1 && fixture.Parent.ReplayCalls == 1
                && !fixture.Batch.TryGetPending(operationId, out _),
            "Exact pending fact recovery failed or created another result.");
        fixture.RoundTripConsumables();
        fixture.Runtime.Tick();
        Require(JsonUtility.ToJson(fixture.Health.Capture()) == body
                && JsonUtility.ToJson(fixture.Actor.Progression.NarrativeLedger) == narrative
                && JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes()) == ledger
                && MoodState(fixture.Actor) == mood
                && fixture.Actor.Stats.GetConditionValue(CharacterCondition.HUNGER).Equals(hunger)
                && fixture.Random.ChanceCalls == rolls && fixture.Fault.DamageCalls == damageCalls
                && fixture.Parent.ReplayCalls == 1,
            "Save/replay duplicated physical effects, random decisions, or exact outcomes.");
    }

    private static void WriteReport(object report)
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        using FileStream file = new(Path.Combine(output, "report.json"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(file, new UTF8Encoding(false));
        writer.WriteLine(JsonUtility.ToJson(report, true));
    }

    private static void VerifySubstance(Report report)
    {
        using Fixture fixture = new(substance: true);
        CharacterActor actor = fixture.Actor;
        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        CharacterConsumablesPolicyChangeResult policy =
            fixture.Runtime.SetSubstancePolicy(actorId, "substance:vitality-tonic",
                SubstancePolicyMode.MoodThreshold, 100f, 0);
        Require(policy.Code == CharacterConsumablesPolicyChangeCode.Changed
                && fixture.PolicyFacts.Ledger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count == 1,
            "Substance setup policy was not recorded as one typed fact outcome.");
        string beforeBody = JsonUtility.ToJson(fixture.Health.Capture());
        string beforeNarrative = JsonUtility.ToJson(actor.Progression.NarrativeLedger);
        string beforeMood = MoodState(actor);
        string beforeState = JsonUtility.ToJson(fixture.Runtime.GetSubstanceState(actorId, DrugId));
        float beforeHealth = actor.Stats.CurrentHealth;
        fixture.Fault.BeforeDamage = () =>
        {
            Require(MoodState(actor) != beforeMood
                    && JsonUtility.ToJson(actor.Progression.NarrativeLedger) != beforeNarrative,
                "Substance did not apply real mood and narrative before overdose damage.");
        };
        ConsumeSubstanceByIdCommand command = new(new ConsumableOperationId(SubstanceOperationId),
            actorId, new ConsumableItemDefinitionId(DrugId), new ItemStackId(fixture.StackId), false, false);
        Require(!fixture.Runtime.TryConsumeSubstance(command, out var pending)
                && pending.FailureCode == CharacterConsumablesFailureCode.DeliveryPending,
            "Injected substance effect fault did not retain a pending physical consumption.");
        var first = fixture.Runtime.Capture().activeSubstanceUsePlans.Single();
        Require(first.phase == CharacterSubstanceUsePlanPhase.ItemCommitted
                && first.resolvedOverdosed && fixture.Random.ChanceCalls == 2
                && fixture.Fault.InjectedFaults == 1 && fixture.Fault.DamageActuallyChangedHealth
                && fixture.ParentCount == 0 && fixture.Parent.CommitAttempts == 0,
            "Substance did not stop after physical commit with frozen overdose/addiction decisions.");
        Require(JsonUtility.ToJson(fixture.Health.Capture()) == beforeBody
                && JsonUtility.ToJson(actor.Progression.NarrativeLedger) == beforeNarrative
                && MoodState(actor) == beforeMood
                && JsonUtility.ToJson(fixture.Runtime.GetSubstanceState(actorId, DrugId)) == beforeState
                && Mathf.Approximately(fixture.Runtime.GetToxicityStatus(actorId).Toxicity, 0f),
            "Substance fault failed to roll back real body/mood/narrative and tolerance/toxicity.");
        string physicalId = first.physicalCommitId;
        RequireSubstancePending(fixture, first);
        fixture.RoundTripConsumables();
        fixture.Runtime.Tick();
        var applied = fixture.Runtime.Capture().activeSubstanceUsePlans.Single();
        Require(applied.phase == CharacterSubstanceUsePlanPhase.EffectsApplied
                && applied.physicalCommitId == physicalId
                && applied.resolvedOverdosed && applied.resolvedOverdoseDamage == first.resolvedOverdoseDamage
                && applied.resolvedEffectMood == first.resolvedEffectMood
                && fixture.Random.ChanceCalls == 2 && fixture.Fault.DamageCalls == 2
                && fixture.Parent.Rejected == 1 && fixture.ParentCount == 0
                && Mathf.Approximately(actor.Stats.CurrentHealth, beforeHealth - first.resolvedOverdoseDamage)
                && Mathf.Approximately(fixture.Runtime.GetToxicityStatus(actorId).Toxicity,
                    CharacterToxicityPolicy.OverdoseGain),
            "Substance retry did not apply frozen actual overdose effects exactly once before parent rejection.");
        RequireSubstancePending(fixture, applied);
        string appliedBody = JsonUtility.ToJson(fixture.Health.Capture());
        string appliedNarrative = JsonUtility.ToJson(actor.Progression.NarrativeLedger);
        string appliedMood = MoodState(actor);
        string appliedState = JsonUtility.ToJson(fixture.Runtime.GetSubstanceState(actorId, DrugId));
        fixture.RoundTripConsumables();
        fixture.Runtime.Tick();
        var terminal = fixture.Runtime.Capture();
        Require(terminal.activeSubstanceUsePlans.Count == 0 && terminal.completedOperations.Count == 1
                && fixture.ParentCount == 1 && fixture.Parent.CommitAttempts == 2
                && !fixture.Batch.TryGetPending(SubstanceOperationId, out _)
                && !string.IsNullOrWhiteSpace(terminal.completedOperations[0].outcomeResultKey),
            "Substance parent-only resume did not finish one exact outcome and acknowledgement.");
        string ledger = JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes());
        fixture.RoundTripConsumables();
        fixture.Runtime.Tick();
        Require(!fixture.Runtime.TryConsumeSubstance(command, out var duplicate)
                && duplicate.FailureCode == CharacterConsumablesFailureCode.AlreadyProcessed,
            "Terminal substance identity replay was not rejected.");
        Require(JsonUtility.ToJson(fixture.Health.Capture()) == appliedBody
                && JsonUtility.ToJson(actor.Progression.NarrativeLedger) == appliedNarrative
                && MoodState(actor) == appliedMood
                && JsonUtility.ToJson(fixture.Runtime.GetSubstanceState(actorId, DrugId)) == appliedState
                && JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes()) == ledger
                && Mathf.Approximately(fixture.Runtime.GetToxicityStatus(actorId).Toxicity,
                    CharacterToxicityPolicy.OverdoseGain)
                && fixture.Random.ChanceCalls == 2 && fixture.Fault.DamageCalls == 2
                && fixture.Items.GetAllStacks().Where(value => value.ItemId == DrugId).Sum(value => value.Quantity) == 1,
            "Substance save/terminal replay duplicated real effects, physical consumption, rolls or parent.");
        report.substance = new SubstanceReport
        {
            passed = true, consumablesJsonRoundTrips = 3,
            healthBefore = beforeHealth, healthAfter = actor.Stats.CurrentHealth,
            toxicityAfter = fixture.Runtime.GetToxicityStatus(actorId).Toxicity,
            resolvedMood = applied.resolvedEffectMood, resolvedOverdoseDamage = applied.resolvedOverdoseDamage,
            rollCount = fixture.Random.ChanceCalls, damageAttempts = fixture.Fault.DamageCalls,
            rolledBackDamageAttempts = fixture.Fault.InjectedFaults, parentCommitCount = fixture.ParentCount,
            physicalQuantity = applied.physicalCommitQuantity, physicalMassGrams = applied.physicalCommitInputMassGrams,
            parentResultKey = terminal.completedOperations[0].outcomeResultKey,
            physicalCommitId = physicalId, physicalSourceStackId = fixture.StackId,
            effectRollbackVerified = true, terminalReplayUnchanged = true
        };
        report.expectedRuntimeErrors += 2;
        report.substanceProductionRecoveryVerified = true;
    }

    private static void RequireSubstancePending(Fixture fixture, CharacterSubstanceUsePlanSaveData plan)
    {
        Require(fixture.Items.GetAllStacks().Where(value => value.ItemId == DrugId).Sum(value => value.Quantity) == 1
                && fixture.Batch.TryGetPending(SubstanceOperationId, out _)
                && plan.physicalCommitQuantity == 1 && plan.physicalCommitInputMassGrams == 425
                && plan.physicalCommitSourceStackIds.SequenceEqual(new[] { fixture.StackId }),
            "Substance did not consume exactly one real 425g dose with an exact pending receipt.");
    }

    private static void VerifyMeal(Report report)
    {
        using Fixture fixture = new();
        CharacterActor actor = fixture.Actor;
        var actorId = CharacterPersistentIdentity.Require(actor);
        CharacterConsumablesPolicyChangeResult policy = fixture.Runtime.SetDietPolicy(
            actorId, CharacterDietPolicyKind.Vegan);
        Require(policy.Code == CharacterConsumablesPolicyChangeCode.Changed
                && fixture.PolicyFacts.Ledger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count == 1,
            "Meal setup policy was not recorded as one typed fact outcome.");
        actor.stats[CharacterCondition.HUNGER] = 0f;
        string beforeBody = JsonUtility.ToJson(fixture.Health.Capture());
        string beforeNarrative = JsonUtility.ToJson(actor.Progression.NarrativeLedger);
        string beforeMood = MoodState(actor);
        float beforeHealth = actor.Stats.CurrentHealth;
        float beforeHunger = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
        fixture.Fault.BeforeDamage = () =>
        {
            Require(actor.Stats.GetConditionValue(CharacterCondition.HUNGER) > beforeHunger,
                "Real hunger did not change before the damage boundary.");
            Require(MoodState(actor) != beforeMood,
                "Real mood memory did not change before the damage boundary.");
            Require(JsonUtility.ToJson(actor.Progression.NarrativeLedger) != beforeNarrative,
                "Real diet narrative did not change before the damage boundary.");
        };
        ConsumeMealCommand command = new(new ConsumableOperationId(OperationId),
            actorId, fixture.Facility.RequirePersistentInstanceId(),
            new ItemStackId(fixture.StackId));
        Require(!fixture.Runtime.TryConsumeMeal(command, out var pending)
                && pending.FailureCode == CharacterConsumablesFailureCode.DeliveryPending,
            "Meal was not accepted as a timed physical reservation: " + pending.FailureCode);
        fixture.Clock.Advance(4.1f);
        fixture.Runtime.Tick();

        CharacterMealPlanSaveData first = RequirePhase(fixture,
            CharacterMealPlanPhase.ItemCommitted);
        Require(fixture.Fault.InjectedFaults == 1 && fixture.Fault.DamageCalls == 1
                && fixture.Fault.DamageActuallyChangedHealth,
            "The exact post-body-mutation fault was not exercised.");
        Require(JsonUtility.ToJson(fixture.Health.Capture()) == beforeBody
                && JsonUtility.ToJson(actor.Progression.NarrativeLedger) == beforeNarrative
                && MoodState(actor) == beforeMood
                && Mathf.Approximately(actor.Stats.CurrentHealth, beforeHealth)
                && Mathf.Approximately(actor.Stats.GetConditionValue(CharacterCondition.HUNGER), beforeHunger),
            "Production effect rollback did not restore body, hunger, mood and narrative.");
        Require(first.contaminationRollFrozen && first.resolvedContaminationDamage
                && first.committedContaminated && first.committedPolicyViolation
                && fixture.Random.ChanceCalls == 1 && !first.effectEvidenceFrozen,
            "ItemCommitted did not preserve one frozen contamination decision.");
        RequirePhysicalPending(fixture, first);
        Require(fixture.ParentCount == 0 && fixture.Parent.CommitAttempts == 0,
            "Parent was attempted before the failed effects were recovered.");
        string physicalId = first.physicalCommitId;
        fixture.RoundTripConsumables();
        report.consumablesJsonRoundTrips++;

        fixture.Runtime.Tick();
        CharacterMealPlanSaveData applied = RequirePhase(fixture,
            CharacterMealPlanPhase.EffectsApplied);
        Require(applied.effectEvidenceFrozen && fixture.Parent.Rejected == 1
                && fixture.ParentCount == 0 && fixture.Fault.DamageCalls == 2
                && fixture.Random.ChanceCalls == 1,
            "Effect recovery or the post-effect parent rejection was not exact-once.");
        Require(applied.physicalCommitId == physicalId
                && Mathf.Approximately(applied.hungerBefore, beforeHunger)
                && Mathf.Approximately(applied.hungerAfter, actor.Stats.GetConditionValue(CharacterCondition.HUNGER))
                && applied.hungerAfter > applied.hungerBefore
                && Mathf.Approximately(applied.healthBefore, beforeHealth)
                && Mathf.Approximately(applied.healthAfter, actor.Stats.CurrentHealth)
                && Mathf.Approximately(applied.healthBefore - applied.healthAfter, 3f),
            "Frozen effect evidence is not the real hunger and health delta.");
        Require(MoodState(actor) != beforeMood
                && JsonUtility.ToJson(actor.Progression.NarrativeLedger) != beforeNarrative,
            "Recovered mood/narrative effects were not observable on the actor.");
        RequirePhysicalPending(fixture, applied);
        string appliedBody = JsonUtility.ToJson(fixture.Health.Capture());
        string appliedNarrative = JsonUtility.ToJson(actor.Progression.NarrativeLedger);
        string appliedMood = MoodState(actor);
        float appliedHunger = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
        fixture.RoundTripConsumables();
        report.consumablesJsonRoundTrips++;
        fixture.Runtime.Tick();

        var terminal = fixture.Runtime.Capture();
        Require(terminal.activeMealPlans.Count == 0 && terminal.completedOperations.Count == 1
                && fixture.ParentCount == 1 && fixture.Parent.CommitAttempts == 2
                && !fixture.Batch.TryGetPending(OperationId, out _),
            "Parent-only resume did not finish one exact outcome and physical acknowledgement.");
        Require(fixture.Runtime.TryGetMealOperationResult(command.OperationId, out var completed)
                && !string.IsNullOrWhiteSpace(completed.OutcomeResultKey),
            "Completed meal did not retain its actual parent result key.");
        string ledger = JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes());
        fixture.RoundTripConsumables();
        report.consumablesJsonRoundTrips++;
        fixture.Runtime.Tick();
        _ = fixture.Runtime.TryConsumeMeal(command, out _);
        fixture.Runtime.Tick();
        Require(JsonUtility.ToJson(fixture.Health.Capture()) == appliedBody
                && JsonUtility.ToJson(actor.Progression.NarrativeLedger) == appliedNarrative
                && MoodState(actor) == appliedMood
                && Mathf.Approximately(actor.Stats.GetConditionValue(CharacterCondition.HUNGER), appliedHunger)
                && JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes()) == ledger
                && fixture.Fault.DamageCalls == 2 && fixture.Random.ChanceCalls == 1
                && fixture.Items.GetAllStacks().All(value => value.ItemId != FoodId),
            "Saved terminal replay duplicated effects, roll, physical consumption or parent.");

        report.healthBefore = beforeHealth;
        report.healthAfter = actor.Stats.CurrentHealth;
        report.hungerBefore = beforeHunger;
        report.hungerAfter = appliedHunger;
        report.moodBefore = applied.moodBefore;
        report.moodAfter = applied.moodAfter;
        report.parentResultKey = completed.OutcomeResultKey;
        report.physicalCommitId = physicalId;
        report.physicalSourceStackId = fixture.StackId;
        report.physicalQuantity = applied.physicalCommitQuantity;
        report.physicalMassGrams = applied.physicalCommitInputMassGrams;
        report.contaminationRollCount = fixture.Random.ChanceCalls;
        report.damageAttempts = fixture.Fault.DamageCalls;
        report.rolledBackDamageAttempts = fixture.Fault.InjectedFaults;
        report.parentCommitCount = fixture.ParentCount;
        report.expectedRuntimeErrors = 2;
        report.effectRollbackVerified = true;
        report.terminalReplayUnchanged = true;
    }

    private static void RequirePhysicalPending(Fixture fixture, CharacterMealPlanSaveData plan)
    {
        Require(fixture.Items.GetAllStacks().All(value => value.ItemId != FoodId)
                && fixture.Batch.TryGetPending(OperationId, out _)
                && plan.physicalCommitQuantity == 1
                && plan.physicalCommitInputMassGrams == 750
                && plan.physicalCommitSourceStackIds.SequenceEqual(new[] { fixture.StackId }),
            "One exact real serving was not consumed with its durable pending physical receipt.");
    }

    private static CharacterMealPlanSaveData RequirePhase(Fixture fixture, CharacterMealPlanPhase phase)
    {
        var save = fixture.Runtime.Capture();
        Require(save.activeMealPlans.Count == 1 && save.activeMealPlans[0].phase == phase,
            "Unexpected meal phase; expected=" + phase + "; save=" + JsonUtility.ToJson(save));
        return save.activeMealPlans[0];
    }

    private static string MoodState(CharacterActor actor) =>
        string.Join("|", actor.Stats.CaptureMoodDeliveryTransactionState().Factors
            .Select(value => JsonUtility.ToJson(value)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterSO actorData;
        private readonly BuildingSO buildingData;
        private readonly DungeonRuntimeAggregateRootStore root = new();
        private readonly CharacterConsumablesApplicationPorts ports;
        internal readonly MutableClock Clock = new();
        internal readonly CountingRandom Random = new();
        internal readonly CharacterActor Actor;
        internal readonly BuildableObject Facility;
        internal readonly WorldItemStackRuntime Items;
        internal readonly CharacterBodyHealthRuntime Health;
        internal readonly FaultAfterDamage Fault;
        internal readonly RejectFirstParent Parent;
        internal readonly MigratedProducerOutcomeEditorFixture Outcomes;
        internal readonly CharacterConsumablesPolicyFactFixture PolicyFacts;
        internal readonly IPhysicalItemBatchDispositionService Batch;
        internal readonly IItemQuantityReservationService Reservations;
        internal readonly LifecycleCancellation Cancellation;
        internal readonly RetryTare Tare;
        internal readonly string StackId;
        internal CharacterConsumablesRuntime Runtime;
        internal int ParentCount => Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;

        internal Fixture(bool substance = false)
        {
            var actorObject = new GameObject("CON02 production meal actor");
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            Cancellation = new LifecycleCancellation(() => Runtime);
            CharacterAiEditorTestDependencies.InjectWithMealCancellation(actorObject, Cancellation);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC, "CON02 recovery slime", "Slime");
            Actor.Initialization(actorData);
            Actor.Identity.SetPersistentId("character:con02-production-meal");
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (Actor.IsUnpublishedComposition)
                Actor.PublishComposition();
            var world = CharacterAiEditorTestDependencies.WorldRegistry;
            world.RegisterCharacterLifetime(Actor);
            world.RegisterCharacter(Actor);
            Health = CharacterAiEditorTestDependencies.BodyHealthRuntime;
            Health.ConfigureVitals(Actor, 100f, true);
            Require(Actor.CanRunAi, "The production meal actor must be active before reserving food.");
            Fault = new FaultAfterDamage(Health);
            if (substance)
            {
                Actor.Stats.ConstructCharacterVitals(new CharacterStatsVitalsService(
                    Health, Fault, CharacterAiEditorTestDependencies.GameEvents,
                    new CharacterDeathEventFactory(world, CharacterAiEditorTestDependencies.GameCalendar),
                    new NoOwnerDeath()));
                Health.ConfigureVitals(Actor, 100f, true);
            }

            Facility = new GameObject("CON02 meal fixture").AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(Facility);
            buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            buildingData.id = 99162;
            buildingData.objectName = "CON02 meal fixture";
            buildingData.width = buildingData.height = 1;
            buildingData.category = BuildingCategory.Shop;
            buildingData.Facility = new FacilityData { roles = FacilityRole.Meal, capacity = 1 };
            Facility.Initialization(buildingData, Vector2Int.zero);
            world.RegisterBuilding(Facility);

            var catalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            var provider = new ResourceDungeonItemCatalogProvider(catalog);
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                provider, root, out var repository, out _, out var reservations,
                out var transfers, out var reservedBatch, out var batch);
            Batch = batch;
            Reservations = reservations;
            var spawner = new WorldItemSpawner(provider, repository, EditorNullItemMarkerPresenter.Instance);
            string destination = CharacterConsumablesRuntime.GetMealDestinationId(
                Facility.RequirePersistentInstanceId(), new ConsumableItemDefinitionId(FoodId));
            string itemId = substance ? DrugId : FoodId;
            int quantity = substance ? 2 : 1;
            Require(spawner.Spawn(itemId, quantity, Vector2Int.zero,
                    substance ? WorldItemStackState.Loose : WorldItemStackState.FacilityBuffer,
                    substance ? string.Empty : destination, contamination: substance ? 0f : 50f) == quantity,
                "Failed to spawn the authored physical consumable.");
            StackId = Items.GetAllStacks().Single(value => value.ItemId == itemId).StackId;
            Tare = new RetryTare(new PackagedLotTareDispositionService(
                (IPackagedLotDefinitionQuery)Items.MassQuery, new NoTareExpected()));
            ports = new CharacterConsumablesApplicationPorts(catalog, Items, world,
                new GameEventBus(), new NoCombat(), CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations, reservedTransfers: transfers,
                reservedBatchDispositions: reservedBatch, batchDispositions: batch,
                bodyHealthCommands: Fault, bodyHealthPersistence: Health,
                tareDispositions: Tare);
            Outcomes = new MigratedProducerOutcomeEditorFixture("run:con02-production-meal", root);
            PolicyFacts = new CharacterConsumablesPolicyFactFixture(
                substance
                    ? "run:con02-production-substance-policy"
                    : "run:con02-production-meal-policy");
            CharacterConsumptionFactFixture.Attach(PolicyFacts.Dispatcher,
                Outcomes.Transaction, Outcomes.Recorder, Outcomes.Ledger);
            Parent = new RejectFirstParent(PolicyFacts.Dispatcher);
            Runtime = NewRuntime();
        }

        private CharacterConsumablesRuntime NewRuntime() => new(ports, ports, ports,
            Clock, Random, root, DefaultCharacterNeedBalanceRuntime.Instance,
            domainFacts: Parent,
            calendar: new EditorFixedGameCalendar(1));

        internal void RoundTripConsumables()
        {
            var save = JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                JsonUtility.ToJson(Runtime.Capture()));
            Runtime = NewRuntime();
            Runtime.PublishRestoreCandidate(Runtime.BuildRestoreCandidate(save));
        }

        internal void DetachActorAndFacility()
        {
            var world = CharacterAiEditorTestDependencies.WorldRegistry;
            world.UnregisterCharacter(Actor);
            world.UnregisterCharacterLifetime(Actor);
            world.UnregisterBuilding(Facility);
            Require(!ports.TryGetActor(CharacterPersistentIdentity.Require(Actor), out _),
                "Detached actor is still reachable through the production adapter.");
        }

        public void Dispose()
        {
            var world = CharacterAiEditorTestDependencies.WorldRegistry;
            world.UnregisterBuilding(Facility);
            world.UnregisterCharacter(Actor);
            world.UnregisterCharacterLifetime(Actor);
            PolicyFacts.Dispose();
            Items.Dispose();
            UnityEngine.Object.DestroyImmediate(Facility.gameObject);
            UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            UnityEngine.Object.DestroyImmediate(buildingData);
            UnityEngine.Object.DestroyImmediate(actorData);
        }
    }

    private sealed class NoCombat : ICharacterCombatStanceQuery
    {
        public bool IsInCombatStance(CharacterActor actor) => false;
    }

    private sealed class LifecycleCancellation : ICharacterMealOperationCancellation
    {
        private readonly Func<CharacterConsumablesRuntime> runtime;
        internal int Calls;
        internal LifecycleCancellation(Func<CharacterConsumablesRuntime> runtime) => this.runtime = runtime;
        public int CancelActiveMealOperations(CharacterActor actor, string reason)
        {
            if (runtime() == null) return 0;
            Calls++;
            return new CharacterConsumablesCompatibilityAdapter(runtime()).CancelActiveMealOperations(actor, reason);
        }
    }

    private sealed class RetryTare : IPackagedLotTareDispositionService
    {
        private readonly IPackagedLotTareDispositionService inner;
        internal bool RejectNext;
        internal Vector2Int LastPosition;
        internal RetryTare(IPackagedLotTareDispositionService inner) => this.inner = inner;
        public bool EnsureTerminalSinkOutputs(IReadOnlyDictionary<string, int> items, Vector2Int position,
            string parent, out PackagedLotTareOutputReceipt receipt, out string failure)
        {
            LastPosition = position;
            if (RejectNext)
            {
                RejectNext = false;
                receipt = default;
                failure = "con03-injected-ack-preparation-rejection";
                return false;
            }
            return inner.EnsureTerminalSinkOutputs(items, position, parent, out receipt, out failure);
        }
    }

    private sealed class NoTareExpected : IPackagedLotTareOutputGateway
    {
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => Array.Empty<WorldItemStackSnapshot>();
        public bool SpawnOutput(string id, int quantity, Vector2Int position,
            IReadOnlyList<ItemInstanceComponentSaveData> components, out int spawned) =>
            throw new InvalidOperationException("CON03 unpackaged meal fixture unexpectedly requires tare output.");
    }

    private sealed class NoOwnerDeath : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor actor, string reason) =>
            throw new InvalidOperationException("CON02 nonlethal fixture unexpectedly killed its owner.");
    }

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance(float seconds) { DeltaTime = seconds; Time += seconds; FrameCount++; }
    }

    private sealed class CountingRandom : IRandomStreamProvider, IRandomStream
    {
        internal int ChanceCalls;
        public int RootSeed => 162;
        public ulong State => 1;
        public IRandomStream Get(string streamId) => this;
        public bool Chance(float probability) { ChanceCalls++; return probability > 0f; }
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
        public void Restore(ulong state) => throw new NotSupportedException();
        public void Reseed(int rootSeed) => throw new NotSupportedException();
        public IReadOnlyList<RandomStreamStateSnapshot> CaptureStates() => Array.Empty<RandomStreamStateSnapshot>();
        public RandomStreamRestoreCandidate BuildRestoreStates(int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots) => throw new NotSupportedException();
        public void RestoreStates(RandomStreamRestoreCandidate candidate) => throw new NotSupportedException();
        public void RestoreStates(int rootSeed, IEnumerable<RandomStreamStateSnapshot> snapshots) =>
            throw new NotSupportedException();
    }

    private sealed class RejectFirstParent : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal int CommitAttempts;
        internal int Rejected;
        internal bool HideNextAcknowledgement;
        internal int HiddenAcknowledgements;
        internal int ReplayCalls;
        internal RejectFirstParent(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact) => inner.Publish(lease, fact);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results,
            out string failure)
        { ReplayCalls++; return inner.ReplayBatch(facts, results, out failure); }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results,
            out string failure)
        {
            CommitAttempts++;
            if (Rejected == 0)
            {
                Rejected++;
                failure = "con02-injected-parent-rejection";
                for (int i = 0; i < facts.Count; i++)
                { inner.Cancel(facts[i].Lease); results[i] = DomainFactDelivery.Reject(failure); }
                return false;
            }
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (accepted && HideNextAcknowledgement)
            {
                Require(results.All(value => value.Completed), "Hidden ACK must follow actual durable commit.");
                HideNextAcknowledgement = false;
                HiddenAcknowledgements++;
                for (int i = 0; i < results.Length; i++)
                    results[i] = new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt,
                        results[i].OperationKey, null, null, "con02-hidden-durable-ack");
            }
            return accepted;
        }
    }

    private sealed class FaultAfterDamage : ICharacterBodyHealthCommand
    {
        private readonly CharacterBodyHealthRuntime inner;
        internal Action BeforeDamage;
        internal int DamageCalls;
        internal int InjectedFaults;
        internal bool DamageActuallyChangedHealth;
        internal FaultAfterDamage(CharacterBodyHealthRuntime inner) => this.inner = inner;
        public void ApplyLocalizedDamage(CharacterActor actor, CombatBodyPart part, float amount,
            string reason, bool allowDeath)
        {
            BeforeDamage?.Invoke();
            float before = inner.GetVitals(actor).CurrentHealth;
            inner.ApplyLocalizedDamage(actor, part, amount, reason, allowDeath);
            AfterDamage(actor, before);
        }
        private void AfterDamage(CharacterActor actor, float before)
        {
            DamageCalls++;
            DamageActuallyChangedHealth = inner.GetVitals(actor).CurrentHealth < before;
            if (InjectedFaults == 0)
            {
                InjectedFaults++;
                throw new InvalidOperationException("con02-injected-after-real-body-damage");
            }
        }
        public void ConfigureVitals(CharacterActor a, float n, bool reset) => inner.ConfigureVitals(a, n, reset);
        public void RefreshMaximumHealthPreservingCurrent(CharacterActor a, float n) => inner.RefreshMaximumHealthPreservingCurrent(a, n);
        public void RestoreLegacyVitalsProjection(CharacterActor a, float max, float current, float injury) => inner.RestoreLegacyVitalsProjection(a, max, current, injury);
        public void ApplyLegacyDamage(CharacterActor a, float n, string r, bool death)
        {
            BeforeDamage?.Invoke();
            float before = inner.GetVitals(a).CurrentHealth;
            inner.ApplyLegacyDamage(a, n, r, death);
            AfterDamage(a, before);
        }
        public void ApplyLegacyDamageWithCause(CharacterActor a, float n, CharacterDeathCauseCode c, string r, bool death) => inner.ApplyLegacyDamageWithCause(a, n, c, r, death);
        public void HealLegacyVitals(CharacterActor a, float n) => inner.HealLegacyVitals(a, n);
        public void ScaleLegacyVitals(CharacterActor a, float n) => inner.ScaleLegacyVitals(a, n);
        public void SetLegacyInjurySeverity(CharacterActor a, float n) => inner.SetLegacyInjurySeverity(a, n);
        public void Kill(CharacterActor a, string r) => inner.Kill(a, r);
        public void Kill(CharacterActor a, CharacterDeathCauseCode c, string r) => inner.Kill(a, c, r);
        public void ApplyCombatResult(CharacterActor a, CombatAttackResult r, string reason) => inner.ApplyCombatResult(a, r, reason);
        public void ApplySnapshot(CharacterActor a, CharacterBodyHealthSnapshot s, string r) => inner.ApplySnapshot(a, s, r);
        public void AddSuppression(CharacterActor a, float n) => inner.AddSuppression(a, n);
        public void ReduceSuppression(CharacterActor a, float n) => inner.ReduceSuppression(a, n);
        public void Heal(CharacterActor a, float n, bool stop) => inner.Heal(a, n, stop);
        public bool Stabilize(CharacterActor a) => inner.Stabilize(a);
        public bool ApplyTreatment(CharacterActor a, float n, float blood) => inner.ApplyTreatment(a, n, blood);
    }

    [Serializable]
    private sealed class Report
    {
        public string scenario = "CON02 production meal and substance effect rollback and durable recovery";
        public bool passed;
        public string error = "";
        public int consumablesJsonRoundTrips;
        public float healthBefore, healthAfter, hungerBefore, hungerAfter, moodBefore, moodAfter;
        public string parentResultKey, physicalCommitId, physicalSourceStackId;
        public int physicalQuantity, contaminationRollCount, damageAttempts, rolledBackDamageAttempts, parentCommitCount;
        public long physicalMassGrams;
        public int expectedRuntimeErrors;
        public bool effectRollbackVerified, terminalReplayUnchanged;
        public bool substanceProductionRecoveryVerified = false;
        public SubstanceReport substance;
        public bool wholeWorldRestoreVerified = false;
        public bool fullCon02Closure = false;
        public bool fullAuditClosure = false;
        public bool humanApprovalClaimed = false;
        public bool trainingEligible = false;
    }

    [Serializable]
    private sealed class SubstanceReport
    {
        public bool passed;
        public int consumablesJsonRoundTrips;
        public float healthBefore, healthAfter, toxicityAfter, resolvedMood, resolvedOverdoseDamage;
        public int rollCount, damageAttempts, rolledBackDamageAttempts, parentCommitCount, physicalQuantity;
        public long physicalMassGrams;
        public string parentResultKey, physicalCommitId, physicalSourceStackId;
        public bool effectRollbackVerified, terminalReplayUnchanged;
    }

    [Serializable]
    private sealed class Con03DepartureReport
    {
        public string scenario = "CON03 postphysical departure persistent-owner reconciliation";
        public bool passed;
        public string error = "";
        public string physicalCommitId, physicalSourceStackId, phaseBeforeDeparture;
        public int physicalQuantity, activePlansAfterDeparture, parentCommitCount, parentCommitAttempts;
        public bool pendingPhysicalAfterDeparture, actorCanRunAi;
        public float healthAfterDeparture;
        public string captureFailure = "";
        public bool fullAuditClosure = false;
        public bool humanApprovalClaimed = false;
        public bool trainingEligible = false;
    }

    [Serializable]
    private sealed class Con03Report
    {
        public string scenario = "CON03 actual lifecycle gateway and detached meal recovery";
        public bool passed, prephysicalLifecycleAbortAndReplay;
        public int expectedFaults, retiredObserverNotices;
        public List<string> unexpectedErrors = new();
        public string error = "";
        public List<Con03Case> cases = new();
        public bool wholeWorldRestoreVerified = false;
        public bool fullAuditClosure = false;
        public bool humanApprovalClaimed = false;
        public bool trainingEligible = false;
    }

    [Serializable]
    private sealed class Con03Case
    {
        public string name, status, physicalCommitId, resultKey;
        public bool passed, pendingPhysical, actorRemovedFromRegistry;
        public bool parentRejectionRecovered, ackRejectionRecovered, malformedRestoreRejected, terminalReplayUnchanged;
        public int consumedQuantity, parentCount, damageAttempts, activePlans, consumablesJsonRoundTrips;
        public long consumedMassGrams;
    }
}
#endif
