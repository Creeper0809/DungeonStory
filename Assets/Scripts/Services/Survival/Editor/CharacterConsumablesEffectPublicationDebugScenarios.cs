#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

/// <summary>
/// B0 CON02/CON03 recovery boundaries for the consumables aggregate.  This is
/// intentionally standalone: it exercises the production runtime through
/// bounded ports and is not part of a broad Editor batch runner.
/// </summary>
public static class CharacterConsumablesEffectPublicationDebugScenarios
{
    internal readonly struct RitualMealExecutionAudit
    {
        internal RitualMealExecutionAudit(
            bool completed,
            int physicalCommits,
            int effectApplications,
            int acknowledgements,
            int fastingNotifications,
            int replayMutations)
        {
            Completed = completed;
            PhysicalCommits = physicalCommits;
            EffectApplications = effectApplications;
            Acknowledgements = acknowledgements;
            FastingNotifications = fastingNotifications;
            ReplayMutations = replayMutations;
        }

        internal bool Completed { get; }
        internal int PhysicalCommits { get; }
        internal int EffectApplications { get; }
        internal int Acknowledgements { get; }
        internal int FastingNotifications { get; }
        internal int ReplayMutations { get; }
    }

    private static readonly CharacterId ActorId = new("character:qa-b0-con");
    private static readonly BuildingInstanceId FacilityId =
        new("building:qa-b0-con-meal");
    private static readonly ConsumableItemDefinitionId MealId =
        new("food:qa-b0-con-meal");
    private static readonly ConsumableItemDefinitionId SubstanceId =
        new("drug:qa-b0-con-substance");
    private static readonly ItemStackId MealStackId = new("stack:qa-b0-con-meal");
    private static readonly ItemStackId SubstanceStackId =
        new("stack:qa-b0-con-substance");

    [MenuItem("Tools/Dungeon Story/QA/Character Consumables Effect Publication")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CHARACTER_CONSUMABLES_EFFECT_PUBLICATION_B0_CON02_CON03_PASS");
    }

    public static void RunAll()
    {
        VerifyInterruptedHungerRecoveryAdmission();
        VerifyMissingMealOutcomeIdentityRetainsPlan();
        VerifyMealEffectsPrecedeRejectedParentAndCancelRetainsPlan();
        VerifyContaminationResultSurvivesJsonRestore();
        VerifySubstanceEffectsAndResolvedValuesSurviveRestore();
        VerifyMealRestoreUsesDurableTimelineForPublicationAndCooldown();
        VerifyFutureConsumableTimesAndMissingTimelineAreRejectedAtomically();
        VerifyPrephysicalMealFailureSurvivesRestoreAndBlocksReplay();
        VerifyMealFailurePayloadValidationAndDurableHistory();
    }

    public static string RunSharedOperationOwnershipForArchitectureBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Shared operation verification requires isolated batch persistence.");
        Fixture source = CreateFixture(false, mealStackState: CharacterConsumablesStackState.Loose,
            outcomeRejects: 0);
        Require(source.Runtime.TryConsumeFieldMeal(ActorId, MealStackId, out var meal)
                && meal.Success
                && source.Runtime.TryConsumeSubstance(ActorId, SubstanceId.Value,
                    medicalContext: true, combatContext: false, out var substance)
                && substance.Success,
            "Shared operation fixture did not complete a field meal followed by a substance.");
        DungeonCharacterConsumablesSaveData saved = CloneSave(source.Runtime.Capture());
        WriteOwnershipCheckpoint("shared-operation-completed.json", saved);
        Require(saved.completedOperations.Count == 2
                && saved.nextOperationSequence == 3L
                && saved.completedOperations[0].meal && !saved.completedOperations[1].meal
                && saved.completedOperations[0].operationId == CharacterConsumableIdForSequence(1)
                && saved.completedOperations[1].operationId == CharacterConsumableIdForSequence(2)
                && saved.substanceStates.Count == 1 && saved.toxicityStates.Count == 1,
            "Meal and substance owners did not share one operation watermark and completion journal.");
        BoundedOutcomePort outcomes = new(source.Port);
        CharacterConsumablesRuntime restored = CreateRuntime(source.Port, source.Clock, outcomes);
        Require(restored.GetSubstanceState(ActorId, SubstanceId.Value).activeSeconds == 0f,
            "Fresh substance owner started with another root's state.");
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        Require(JsonUtility.ToJson(restored.Capture()) == JsonUtility.ToJson(saved)
                && restored.GetSubstanceState(ActorId, SubstanceId.Value).activeSeconds > 0f,
            "Fresh owners did not restore the common journal and substance state together.");
        restored.ReconcilePersistentActorReferences(Array.Empty<CharacterId>());
        DungeonCharacterConsumablesSaveData retired = restored.Capture();
        WriteOwnershipCheckpoint("shared-operation-retired.json", retired);
        Require(retired.completedOperations.Count == 0 && retired.substanceStates.Count == 0
                && retired.toxicityStates.Count == 0 && retired.nextOperationSequence == 3L,
            "Retirement did not route completion/substance/toxicity cleanup to their owners or lost its watermark.");
        restored.ValidateRestorePayload(retired, requireWorldReferences: true);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        Require(JsonUtility.ToJson(restored.Capture()) == JsonUtility.ToJson(saved)
                && restored.TryGetMealOperationResult(meal.OperationId, out var completedMeal)
                && completedMeal.Success
                && source.Port.MealPhysicalCommits == 1 && source.Port.SubstancePhysicalCommits == 1
                && source.Port.MealEffectApplications == 1 && source.Port.SubstanceEffectApplications == 1
                && outcomes.ParentCommitAttempts == 0,
            "Repeated root replacement wrote stale owner state or replayed committed effects.");
        return "shared-operation-owner=PASS; meal/substance IDs=auto1/auto2; watermark=3; "
            + "JSON fresh root=exact; retirement=completion/substance/toxicity; repeated root=exact; replay=0";
    }

    private static string CharacterConsumableIdForSequence(long sequence) =>
        "consumable-operation:auto:v1:" + sequence.ToString("D16", CultureInfo.InvariantCulture);

    private static void WriteOwnershipCheckpoint(string name, DungeonCharacterConsumablesSaveData save)
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output),
            "Shared operation verification requires the launcher's evidence directory.");
        using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.Write(JsonUtility.ToJson(save));
    }

    internal static void VerifyInterruptedHungerRecoveryAdmission()
    {
        CharacterNeedResponseProfile response = DefaultCharacterNeedBalanceRuntime
            .Instance.GetResponse(CharacterCondition.HUNGER);
        float partialRecovery = (response.routineStart + response.resumeTarget) * 0.5f;
        Fixture healthy = CreateFixture(false, hunger: partialRecovery,
            mealStackState: CharacterConsumablesStackState.Loose, outcomeRejects: 0);
        Require(!healthy.Runtime.TryFindFieldMeal(ActorId, out _, out _, out _)
            && !healthy.Runtime.TryConsumeFieldMeal(ActorId, MealStackId, out _)
            && healthy.Port.MealPhysicalCommits == 0
            && healthy.Port.MealEffectApplications == 0,
            "Uninterrupted worker above routineStart was allowed to eat.");

        Fixture recovering = CreateFixture(false, hunger: partialRecovery,
            mealStackState: CharacterConsumablesStackState.Loose, outcomeRejects: 0,
            requiresHungerRecovery: true);
        Require(recovering.Runtime.TryFindFieldMeal(ActorId, out ItemStackId stack,
                out _, out _) && stack.Equals(MealStackId)
            && recovering.Runtime.TryConsumeFieldMeal(ActorId, stack, out var result)
            && result.Success && recovering.Port.MealPhysicalCommits == 1
            && recovering.Port.MealEffectApplications == 1
            && Mathf.Approximately(recovering.Port.MealHungerRecovered, 30f),
            "Interrupted worker could not finish recovery through the physical field meal.");

        Fixture satisfied = CreateFixture(false, hunger: response.resumeTarget,
            mealStackState: CharacterConsumablesStackState.Loose, outcomeRejects: 0,
            requiresHungerRecovery: true);
        Require(!satisfied.Runtime.TryFindFieldMeal(ActorId, out _, out _, out _)
            && !satisfied.Runtime.TryConsumeFieldMeal(ActorId, MealStackId, out _)
            && satisfied.Port.MealPhysicalCommits == 0,
            "Stale recovery projection allowed another meal at resumeTarget.");

        Fixture registered = CreateFixture(false, hunger: partialRecovery,
            outcomeRejects: 0, requiresHungerRecovery: true);
        Require(!registered.Runtime.TryConsumeMeal(ActorId, FacilityId, out var pending)
            && pending.IsAcceptedPending,
            "Registered meal rejected the interrupted worker's recovery need.");
        registered.Clock.Advance(4f);
        registered.Runtime.Tick();
        Require(registered.Port.MealPhysicalCommits == 1
            && registered.Port.MealEffectApplications == 1
            && registered.Port.MealAcknowledgements == 1,
            $"Registered recovery meal did not commit physical/effects exactly once: physical={registered.Port.MealPhysicalCommits}; effects={registered.Port.MealEffectApplications}; ack={registered.Port.MealAcknowledgements}; fasting={registered.Port.RitualMealNotifications}.");
    }

    private static void VerifyMissingMealOutcomeIdentityRetainsPlan()
    {
        Fixture fixture = CreateFixture(contaminatedMeal: false, outcomeRejects: 0);
        fixture.Outcomes.OmitAcceptedResultKey = true;
        ConsumableOperationId operation = new("consumable-operation:qa-missing-result-key");
        ConsumeMealCommand command = new(operation, ActorId, FacilityId, MealStackId);
        Require(!fixture.Runtime.TryConsumeMeal(command, out var pending)
            && pending.IsAcceptedPending, "Missing-result fixture did not start a meal.");
        fixture.Clock.Advance(4f);
        fixture.Runtime.Tick();
        fixture.Runtime.Tick();
        DungeonCharacterConsumablesSaveData saved = fixture.Runtime.Capture();
        Require(saved.activeMealPlans.Count == 1
            && saved.activeMealPlans[0].phase == CharacterMealPlanPhase.EffectsApplied
            && saved.activeMealPlans[0].factAbsoluteDay == 0
            && string.IsNullOrEmpty(saved.activeMealPlans[0].publicationKey)
            && !saved.activeMealPlans[0].publicationPending
            && !saved.activeMealPlans[0].ritualFastingConsequenceDelivered
            && saved.completedOperations.Count == 0
            && fixture.Port.MealPhysicalCommits == 1
            && fixture.Port.MealEffectApplications == 1
            && fixture.Port.MealAcknowledgements == 0
            && fixture.Port.RitualMealNotifications == 0
            && fixture.Outcomes.ParentCommitAttempts == 0,
            "Missing meal publication identity was not rejected before publication, or repeated physical/effects.");
        Debug.Log("CON02_MISSING_MEAL_RESULT_IDENTITY_RETAINED_PASS");
    }

    internal static RitualMealExecutionAudit RunRitualFastingMealAudit(
        ICharacterRitualFastingMealPort ritualFasting,
        string operationId)
    {
        if (ritualFasting == null)
            throw new ArgumentNullException(nameof(ritualFasting));
        Fixture fixture = CreateFixture(
            contaminatedMeal: false,
            ritualFasting: ritualFasting,
            outcomeRejects: 0);
        ConsumeMealCommand command = new(
            new ConsumableOperationId(operationId),
            ActorId,
            FacilityId,
            MealStackId);
        Require(!fixture.Runtime.TryConsumeMeal(
                command,
                out CharacterConsumablesMealResult pending)
            && pending.IsAcceptedPending,
            "Ritual meal did not begin through the production consumables runtime.");
        fixture.Clock.Advance(4f);
        fixture.Runtime.Tick();
        bool completed = fixture.Runtime.TryGetMealOperationResult(
                command.OperationId,
                out CharacterConsumablesMealResult terminal)
            && terminal.Success;
        int physical = fixture.Port.MealPhysicalCommits;
        int effects = fixture.Port.MealEffectApplications;
        int acknowledgements = fixture.Port.MealAcknowledgements;
        int notifications = fixture.Port.RitualMealNotifications;
        fixture.Runtime.TryConsumeMeal(command, out _);
        int replayMutations = fixture.Port.MealPhysicalCommits - physical
            + fixture.Port.MealEffectApplications - effects
            + fixture.Port.MealAcknowledgements - acknowledgements
            + fixture.Port.RitualMealNotifications - notifications;
        return new RitualMealExecutionAudit(
            completed,
            physical,
            effects,
            acknowledgements,
            notifications,
            replayMutations);
    }

    private static void VerifyMealEffectsPrecedeRejectedParentAndCancelRetainsPlan()
    {
        Fixture fixture = CreateFixture(contaminatedMeal: false);
        ConsumableOperationId operationId =
            new("consumable-operation:qa-b0-con02-meal");
        ConsumeMealCommand command = new(
            operationId,
            ActorId,
            FacilityId,
            MealStackId);

        Require(
            !fixture.Runtime.TryConsumeMeal(command, out CharacterConsumablesMealResult pending)
            && pending.IsAcceptedPending,
            "B0 CON02 fixture did not begin the authored meal plan.");
        fixture.Clock.Advance(4f);
        fixture.Runtime.Tick();

        DungeonCharacterConsumablesSaveData rejected = fixture.Runtime.Capture();
        Require(
            rejected.activeMealPlans.Count == 1
            && rejected.activeMealPlans[0].phase
                == CharacterMealPlanPhase.EffectsApplied
            && fixture.Port.MealPhysicalCommits == 1
            && fixture.Port.MealEffectApplications == 1
            && Mathf.Approximately(fixture.Port.MealHungerRecovered, 30f)
            && fixture.Outcomes.ParentCommitAttempts == 1
            && fixture.Outcomes.AllParentsObservedEffects,
            "Meal parent publication did not follow one durable physical/effect application.");

        int cancelled = fixture.Runtime.CancelActiveMealOperations(
            ActorId,
            "qa-cancel-after-physical");
        DungeonCharacterConsumablesSaveData afterCancel = fixture.Runtime.Capture();
        Require(
            cancelled == 0
            && afterCancel.activeMealPlans.Count == 1
            && afterCancel.activeMealPlans[0].phase
                == CharacterMealPlanPhase.EffectsApplied
            && afterCancel.failedMealOperations.Count == 1
            && afterCancel.failedMealOperations[0].operationId == operationId.Value
            && afterCancel.failedMealOperations[0].characterId == ActorId.Value
            && afterCancel.failedMealOperations[0].facilityInstanceId == FacilityId.Value
            && afterCancel.failedMealOperations[0].failureCode
                == CharacterConsumablesFailureCode.PhysicalConsumptionFailed
            && afterCancel.failedMealOperations[0].detail != null
            && afterCancel.failedMealOperations[0].detail.Contains(
                "qa-cancel-after-physical")
            && afterCancel.failedMealOperations[0].recoveryPending,
            "Cancelling a post-physical meal removed its recovery plan.");

        fixture.Runtime.Tick();
        DungeonCharacterConsumablesSaveData completed = fixture.Runtime.Capture();
        Require(
            completed.activeMealPlans.Count == 0
            && completed.completedOperations.Count == 1
            && fixture.Port.MealPhysicalCommits == 1
            && fixture.Port.MealEffectApplications == 1
            && fixture.Port.MealAcknowledgements == 1
            && fixture.Outcomes.ParentCommitAttempts == 2
            && fixture.Outcomes.AllParentsObservedEffects,
            "Meal retry duplicated a physical/effect application or failed to publish its parent outcome.");
        Require(
            completed.failedMealOperations.Count == 0,
            "Meal acknowledgement retained a post-physical recovery failure after completion.");
    }

    private static void VerifyContaminationResultSurvivesJsonRestore()
    {
        Fixture source = CreateFixture(contaminatedMeal: true);
        ConsumableOperationId operationId =
            new("consumable-operation:qa-b0-con02-contamination");
        ConsumeMealCommand command = new(
            operationId,
            ActorId,
            FacilityId,
            MealStackId);

        Require(
            !source.Runtime.TryConsumeMeal(command, out CharacterConsumablesMealResult pending)
            && pending.IsAcceptedPending,
            "Contamination fixture did not begin the emergency meal plan.");
        source.Clock.Advance(4f);
        source.Runtime.Tick();

        DungeonCharacterConsumablesSaveData saved = JsonUtility.FromJson<
            DungeonCharacterConsumablesSaveData>(
            JsonUtility.ToJson(source.Runtime.Capture()));
        Require(
            saved.activeMealPlans.Count == 1
            && saved.activeMealPlans[0].phase
                == CharacterMealPlanPhase.EffectsApplied
            && saved.activeMealPlans[0].contaminationRollFrozen
            && saved.activeMealPlans[0].resolvedContaminationDamage,
            "Contaminated meal did not persist its one-time resolved roll before parent publication.");

        BoundedOutcomePort restoredOutcomes = new(source.Port);
        CharacterConsumablesRuntime restored = CreateRuntime(
            source.Port,
            source.Clock,
            restoredOutcomes);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        DungeonCharacterConsumablesSaveData restoredSave = restored.Capture();
        Require(
            restoredSave.activeMealPlans.Count == 1
            && restoredSave.activeMealPlans[0].phase
                == CharacterMealPlanPhase.EffectsApplied
            && restoredSave.activeMealPlans[0].contaminationRollFrozen
                == saved.activeMealPlans[0].contaminationRollFrozen
            && restoredSave.activeMealPlans[0].resolvedContaminationDamage
                == saved.activeMealPlans[0].resolvedContaminationDamage,
            "Contaminated meal JSON restore changed the frozen roll/result.");

        restored.Tick();
        Require(
            restored.Capture().activeMealPlans.Count == 0
            && source.Port.MealPhysicalCommits == 1
            && source.Port.MealEffectApplications == 1
            && source.Port.MealAcknowledgements == 1
            && restoredOutcomes.ParentCommitAttempts == 1
            && restoredOutcomes.AllParentsObservedEffects,
            "Contaminated meal restore retried physical/effects instead of only its parent outcome.");
    }

    private static void VerifySubstanceEffectsAndResolvedValuesSurviveRestore()
    {
        Fixture source = CreateFixture(contaminatedMeal: false);
        ConsumableOperationId operationId =
            new("consumable-operation:qa-b0-con03-substance");
        ConsumeSubstanceByIdCommand command = new(
            operationId,
            ActorId,
            SubstanceId,
            SubstanceStackId,
            medicalContext: true,
            combatContext: false);

        Require(
            !source.Runtime.TryConsumeSubstance(
                command,
                out CharacterConsumablesSubstanceResult pending)
            && pending.FailureCode == CharacterConsumablesFailureCode.DeliveryPending,
            "B0 CON03 fixture did not retain a rejected substance parent outcome.");

        DungeonCharacterConsumablesSaveData saved = JsonUtility.FromJson<
            DungeonCharacterConsumablesSaveData>(
            JsonUtility.ToJson(source.Runtime.Capture()));
        Require(
            saved.activeSubstanceUsePlans.Count == 1
            && saved.activeSubstanceUsePlans[0].phase
                == CharacterSubstanceUsePlanPhase.EffectsApplied
            && Mathf.Approximately(saved.activeSubstanceUsePlans[0].resolvedEffectMood, 8f)
            && saved.activeSubstanceUsePlans[0].resolvedOverdosed
            && Mathf.Approximately(saved.activeSubstanceUsePlans[0].resolvedOverdoseDamage, 12f)
            && source.Port.SubstancePhysicalCommits == 1
            && source.Port.SubstanceEffectApplications == 1
            && Mathf.Approximately(source.Port.SubstanceMoodApplied, 8f)
            && Mathf.Approximately(source.Port.SubstanceDamageApplied, 12f)
            && source.Outcomes.ParentCommitAttempts == 1
            && source.Outcomes.AllParentsObservedEffects,
            "Substance effects were not applied before its rejected parent outcome.");

        BoundedOutcomePort restoredOutcomes = new(source.Port);
        CharacterConsumablesRuntime restored = CreateRuntime(
            source.Port,
            source.Clock,
            restoredOutcomes);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        DungeonCharacterConsumablesSaveData restoredSave = restored.Capture();
        Require(
            restoredSave.activeSubstanceUsePlans.Count == 1
            && restoredSave.activeSubstanceUsePlans[0].phase
                == CharacterSubstanceUsePlanPhase.EffectsApplied
            && Mathf.Approximately(
                restoredSave.activeSubstanceUsePlans[0].resolvedEffectMood,
                saved.activeSubstanceUsePlans[0].resolvedEffectMood)
            && restoredSave.activeSubstanceUsePlans[0].resolvedOverdosed
                == saved.activeSubstanceUsePlans[0].resolvedOverdosed
            && Mathf.Approximately(
                restoredSave.activeSubstanceUsePlans[0].resolvedOverdoseDamage,
                saved.activeSubstanceUsePlans[0].resolvedOverdoseDamage),
            "Substance JSON restore changed resolved mood or overdose damage.");

        restored.Tick();
        Require(
            restored.Capture().activeSubstanceUsePlans.Count == 0
            && source.Port.SubstancePhysicalCommits == 1
            && source.Port.SubstanceEffectApplications == 1
            && source.Port.SubstanceAcknowledgements == 1
            && restoredOutcomes.ParentCommitAttempts == 1
            && restoredOutcomes.AllParentsObservedEffects,
            "Substance parent retry duplicated physical/effects after restore.");
    }

    private static void VerifyMealRestoreUsesDurableTimelineForPublicationAndCooldown()
    {
        Fixture source = CreateFixture(
            contaminatedMeal: false,
            hunger: 40f,
            mealServingRole: MealServingRole.Snack);
        source.Clock.Set(100f);
        ConsumableOperationId operationId =
            new("consumable-operation:qa-b0-con-timeline-meal");
        ConsumeMealCommand command = new(
            operationId,
            ActorId,
            FacilityId,
            MealStackId);

        Require(
            !source.Runtime.TryConsumeMeal(command, out CharacterConsumablesMealResult pending)
            && pending.IsAcceptedPending,
            "Timeline fixture did not begin its snack meal plan.");
        source.Clock.Advance(1f);
        DungeonCharacterConsumablesSaveData saved = CloneSave(source.Runtime.Capture());
        Require(
            Mathf.Approximately(saved.timelineTime, 101f)
            && saved.activeMealPlans.Count == 1
            && Math.Abs(saved.activeMealPlans[0].createdAt - 100d) < 0.0001d,
            "Meal capture did not preserve the raw-start timestamp and durable timeline.");

        MutableClock restoredClock = new();
        BoundedOutcomePort restoredOutcomes = new(source.Port) { RejectsRemaining = 1 };
        CharacterConsumablesRuntime restored = CreateRuntime(
            source.Port,
            restoredClock,
            restoredOutcomes);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        DungeonCharacterConsumablesSaveData rebound = restored.Capture();
        Require(
            Mathf.Approximately(rebound.timelineTime, 101f)
            && rebound.activeMealPlans.Count == 1
            && Math.Abs(rebound.activeMealPlans[0].createdAt - 100d) < 0.0001d,
            "Restored meal did not bind raw time zero to its saved logical timeline.");

        restoredClock.Advance(2.9f);
        restored.Tick();
        Require(
            source.Port.MealPhysicalCommits == 0
            && source.Port.MealEffectApplications == 0,
            "Restored meal committed before its remaining logical action time elapsed.");

        // Use a tiny positive margin because the production comparison is at
        // the exact four-second authored action boundary.
        restoredClock.Advance(0.11f);
        restored.Tick();
        Require(
            source.Port.MealPhysicalCommits == 1
            && source.Port.MealEffectApplications == 1
            && restoredOutcomes.ParentCommitAttempts == 1
            && restored.Capture().activeMealPlans.Count == 1,
            "Restored meal did not commit once before retaining its rejected parent outcome.");

        restored.Tick();
        DungeonCharacterConsumablesSaveData completed = restored.Capture();
        Require(
            completed.activeMealPlans.Count == 0
            && completed.completedOperations.Count == 1
            && source.Port.MealPhysicalCommits == 1
            && source.Port.MealEffectApplications == 1
            && source.Port.MealAcknowledgements == 1
            && completed.mealFollowupCooldowns.Count == 1
            && Mathf.Approximately(
                completed.mealFollowupCooldowns[0].untilGameSeconds,
                completed.timelineTime + 15f),
            "Snack completion did not retain its cooldown on the restored logical timeline.");

        Require(
            !restored.HasMealAvailable(ActorId, FacilityId, out CharacterConsumablesFailure blocked)
            && blocked.Code == CharacterConsumablesFailureCode.PolicyForbidden,
            "Restored snack cooldown was not active at its logical completion time.");
        restoredClock.Advance(14.89f);
        Require(
            !restored.HasMealAvailable(ActorId, FacilityId, out blocked)
            && blocked.Code == CharacterConsumablesFailureCode.PolicyForbidden,
            "Restored snack cooldown elapsed against the raw clock instead of the logical timeline.");
        restoredClock.Advance(0.12f);
        Require(
            restored.HasMealAvailable(ActorId, FacilityId, out _),
            "Restored snack cooldown did not expire after its remaining logical duration.");
    }

    private static void VerifyFutureConsumableTimesAndMissingTimelineAreRejectedAtomically()
    {
        Fixture meal = CreateFixture(contaminatedMeal: false);
        meal.Clock.Set(100f);
        ConsumableOperationId operationId =
            new("consumable-operation:qa-b0-con-invalid-time-meal");
        Require(
            !meal.Runtime.TryConsumeMeal(
                new ConsumeMealCommand(operationId, ActorId, FacilityId, MealStackId),
                out CharacterConsumablesMealResult pending)
            && pending.IsAcceptedPending,
            "Invalid-time fixture did not begin its meal plan.");
        meal.Clock.Advance(1f);
        DungeonCharacterConsumablesSaveData active = CloneSave(meal.Runtime.Capture());

        DungeonCharacterConsumablesSaveData missingTimeline = DungeonSaveJson.Deserialize<
            DungeonCharacterConsumablesSaveData>(
            RemoveTimelineTime(JsonUtility.ToJson(active)));
        Require(
            Mathf.Approximately(missingTimeline.timelineTime, -1f),
            "Omitted timelineTime did not decode to its required invalid sentinel.");
        RequireRestoreRejectedWithoutMutation(
            meal.Runtime,
            missingTimeline,
            "missing timelineTime");

        DungeonCharacterConsumablesSaveData negativeTimeline = CloneSave(active);
        negativeTimeline.timelineTime = -1f;
        RequireRestoreRejectedWithoutMutation(
            meal.Runtime,
            negativeTimeline,
            "negative timelineTime");

        DungeonCharacterConsumablesSaveData nonFiniteTimeline = CloneSave(active);
        nonFiniteTimeline.timelineTime = float.NaN;
        RequireRestoreRejectedWithoutMutation(
            meal.Runtime,
            nonFiniteTimeline,
            "non-finite timelineTime");

        DungeonCharacterConsumablesSaveData futureCreatedAt = CloneSave(active);
        futureCreatedAt.activeMealPlans[0].createdAt = futureCreatedAt.timelineTime + 0.1f;
        RequireRestoreRejectedWithoutMutation(
            meal.Runtime,
            futureCreatedAt,
            "meal createdAt beyond timelineTime");

        meal.Clock.Advance(3.1f);
        meal.Runtime.Tick();
        meal.Runtime.Tick();
        DungeonCharacterConsumablesSaveData completed = CloneSave(meal.Runtime.Capture());
        Require(
            completed.completedOperations.Count == 1,
            "Invalid completedAt fixture did not complete its meal operation.");
        DungeonCharacterConsumablesSaveData futureCompletedAt = CloneSave(completed);
        futureCompletedAt.completedOperations[0].completedAt =
            futureCompletedAt.timelineTime + 0.1f;
        RequireRestoreRejectedWithoutMutation(
            meal.Runtime,
            futureCompletedAt,
            "operation completedAt beyond timelineTime");

        Fixture delivery = CreateFixture(
            contaminatedMeal: false,
            mealStackState: CharacterConsumablesStackState.Stored,
            acceptsMealDelivery: true);
        delivery.Clock.Set(100f);
        delivery.Clock.Advance(0.1f);
        delivery.Runtime.Tick();
        DungeonCharacterConsumablesSaveData pendingDelivery = CloneSave(delivery.Runtime.Capture());
        Require(
            pendingDelivery.pendingMealDeliveries.Count == 1,
            "Invalid requestedAt fixture did not author an actual pending meal delivery.");
        DungeonCharacterConsumablesSaveData futureRequestedAt = CloneSave(pendingDelivery);
        futureRequestedAt.pendingMealDeliveries[0].requestedAt =
            futureRequestedAt.timelineTime + 0.1f;
        RequireRestoreRejectedWithoutMutation(
            delivery.Runtime,
            futureRequestedAt,
            "delivery requestedAt beyond timelineTime");
    }

    private static void VerifyPrephysicalMealFailureSurvivesRestoreAndBlocksReplay()
    {
        const string CancellationReason = "qa-prephysical-cancel";
        Fixture source = CreateFixture(contaminatedMeal: false);
        ConsumableOperationId operationId =
            new("consumable-operation:qa-b0-con-prephysical-cancel");
        ConsumeMealCommand command = new(
            operationId,
            ActorId,
            FacilityId,
            MealStackId);

        Require(
            !source.Runtime.TryConsumeMeal(command, out CharacterConsumablesMealResult pending)
            && pending.IsAcceptedPending,
            "Prephysical failure fixture did not begin its meal plan.");
        Require(
            source.Runtime.CancelActiveMealOperations(ActorId, CancellationReason) == 1,
            "Prephysical cancellation did not remove its active meal plan.");
        DungeonCharacterConsumablesSaveData saved = CloneSave(source.Runtime.Capture());
        Require(
            saved.activeMealPlans.Count == 0
            && saved.failedMealOperations.Count == 1
            && IsFailure(
                saved.failedMealOperations[0],
                operationId,
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                CancellationReason,
                recoveryPending: false)
            && source.Port.MealPhysicalCommits == 0
            && source.Port.MealEffectApplications == 0,
            "Prephysical cancellation did not persist its exact terminal failure row.");

        MutableClock restoredClock = new();
        CharacterConsumablesRuntime restored = CreateRuntime(
            source.Port,
            restoredClock,
            new BoundedOutcomePort(source.Port));
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        DungeonCharacterConsumablesSaveData restoredSave = restored.Capture();
        Require(
            restoredSave.failedMealOperations.Count == 1
            && IsFailure(
                restoredSave.failedMealOperations[0],
                operationId,
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                CancellationReason,
                recoveryPending: false),
            "Prephysical terminal row changed across JSON capture and fresh-runtime restore.");

        Require(
            !restored.TryGetMealOperationResult(
                operationId,
                out CharacterConsumablesMealResult terminal)
            && terminal.OperationId.Equals(operationId)
            && terminal.FailureCode == CharacterConsumablesFailureCode.PhysicalConsumptionFailed
            && terminal.Parameters.Contains(CancellationReason),
            "Known terminal meal failure did not return its original ID, code, and detail.");
        Require(
            !restored.TryConsumeMeal(command, out CharacterConsumablesMealResult replay)
            && replay.OperationId.Equals(operationId)
            && replay.FailureCode == CharacterConsumablesFailureCode.PhysicalConsumptionFailed
            && replay.Parameters.Contains(CancellationReason)
            && source.Port.MealPhysicalCommits == 0
            && source.Port.MealEffectApplications == 0,
            "Replaying an aborted meal regenerated work or lost its terminal result.");

        ConsumableOperationId unknownId =
            new("consumable-operation:qa-b0-con-terminal-unknown");
        Require(
            !restored.TryGetMealOperationResult(
                unknownId,
                out CharacterConsumablesMealResult unknown)
            && !unknown.OperationId.IsValid,
            "Unknown meal operation returned a terminal operation identity.");
    }

    private static void VerifyMealFailurePayloadValidationAndDurableHistory()
    {
        Fixture source = CreateFixture(contaminatedMeal: false);
        ConsumableOperationId failureId =
            new("consumable-operation:qa-b0-con-failure-validation");
        ConsumeMealCommand command = new(
            failureId,
            ActorId,
            FacilityId,
            MealStackId);
        Require(
            !source.Runtime.TryConsumeMeal(command, out CharacterConsumablesMealResult pending)
            && pending.IsAcceptedPending
            && source.Runtime.CancelActiveMealOperations(ActorId, "qa-failure-validation") == 1,
            "Failure validation fixture did not create a terminal meal row.");
        DungeonCharacterConsumablesSaveData valid = CloneSave(source.Runtime.Capture());
        Require(
            valid.failedMealOperations.Count == 1,
            "Failure validation fixture did not capture its terminal meal row.");

        DungeonCharacterConsumablesSaveData nullRow = CloneSave(valid);
        nullRow.failedMealOperations[0] = null;
        RequireRestoreRejectedWithoutMutation(source.Runtime, nullRow, "null meal failure row");

        DungeonCharacterConsumablesSaveData duplicate = CloneSave(valid);
        duplicate.failedMealOperations.Add(CloneFailure(duplicate.failedMealOperations[0]));
        RequireRestoreRejectedWithoutMutation(
            source.Runtime,
            duplicate,
            "duplicate meal failure operation ID");

        DungeonCharacterConsumablesSaveData unknownCode = CloneSave(valid);
        unknownCode.failedMealOperations[0].failureCode =
            (CharacterConsumablesFailureCode)999;
        RequireRestoreRejectedWithoutMutation(
            source.Runtime,
            unknownCode,
            "unknown meal failure code");

        DungeonCharacterConsumablesSaveData deliveryPending = CloneSave(valid);
        deliveryPending.failedMealOperations[0].failureCode =
            CharacterConsumablesFailureCode.DeliveryPending;
        RequireRestoreRejectedWithoutMutation(
            source.Runtime,
            deliveryPending,
            "delivery-pending terminal meal failure");

        DungeonCharacterConsumablesSaveData blankOperationId = CloneSave(valid);
        blankOperationId.failedMealOperations[0].operationId = string.Empty;
        RequireRestoreRejectedWithoutMutation(
            source.Runtime,
            blankOperationId,
            "blank meal failure operation ID");

        DungeonCharacterConsumablesSaveData wrongActor = CloneSave(valid);
        wrongActor.failedMealOperations[0].characterId = "character:qa-b0-con-other";
        RequireRestoreRejectedWithoutMutation(
            source.Runtime,
            wrongActor,
            "meal failure actor outside the live world");

        Fixture active = CreateFixture(contaminatedMeal: false);
        ConsumableOperationId activeId =
            new("consumable-operation:qa-b0-con-failure-overlap");
        Require(
            !active.Runtime.TryConsumeMeal(
                new ConsumeMealCommand(activeId, ActorId, FacilityId, MealStackId),
                out CharacterConsumablesMealResult activePending)
            && activePending.IsAcceptedPending,
            "Failure-overlap fixture did not begin its active meal plan.");
        DungeonCharacterConsumablesSaveData overlapping = CloneSave(active.Runtime.Capture());
        CharacterMealOperationFailureState overlap = CloneFailure(valid.failedMealOperations[0]);
        overlap.operationId = activeId.Value;
        overlap.characterId = ActorId.Value;
        overlap.facilityInstanceId = FacilityId.Value;
        overlap.recoveryPending = false;
        overlapping.failedMealOperations.Add(overlap);
        RequireRestoreRejectedWithoutMutation(
            active.Runtime,
            overlapping,
            "prephysical meal failure overlapping an active plan");

        Fixture history = CreateFixture(contaminatedMeal: false);
        ConsumableOperationId firstFailure = default;
        for (int index = 0; index < 65; index++)
        {
            ConsumableOperationId operationId = new(
                "consumable-operation:qa-b0-con-history-"
                + index.ToString("D2", CultureInfo.InvariantCulture));
            if (index == 0)
                firstFailure = operationId;
            Require(
                !history.Runtime.TryConsumeMeal(
                    new ConsumeMealCommand(operationId, ActorId, FacilityId, MealStackId),
                    out CharacterConsumablesMealResult historyPending)
                && historyPending.IsAcceptedPending
                && history.Runtime.CancelActiveMealOperations(
                    ActorId,
                    "qa-history-cancel-" + index.ToString("D2", CultureInfo.InvariantCulture))
                    == 1,
                "Meal failure history fixture did not record a distinct prephysical cancellation.");
        }
        DungeonCharacterConsumablesSaveData durableHistory = history.Runtime.Capture();
        Require(
            durableHistory.failedMealOperations.Count == 65
            && durableHistory.failedMealOperations.Exists(
                value => value != null && value.operationId == firstFailure.Value)
            && !history.Runtime.TryGetMealOperationResult(
                firstFailure,
                out CharacterConsumablesMealResult firstTerminal)
            && firstTerminal.OperationId.Equals(firstFailure),
            "Meal failure history evicted its first durable terminal row after 65 cancellations.");
    }

    private static Fixture CreateFixture(
        bool contaminatedMeal,
        float hunger = 0f,
        MealServingRole mealServingRole = MealServingRole.FullMeal,
        CharacterConsumablesStackState mealStackState =
            CharacterConsumablesStackState.FacilityBuffer,
        bool acceptsMealDelivery = false,
        ICharacterRitualFastingMealPort ritualFasting = null,
        int outcomeRejects = 1,
        bool requiresHungerRecovery = false)
    {
        BoundedConsumablesPort port = new(
            contaminatedMeal,
            hunger,
            mealServingRole,
            mealStackState,
            acceptsMealDelivery,
            ritualFasting,
            requiresHungerRecovery);
        MutableClock clock = new();
        BoundedOutcomePort outcomes = new(port)
        {
            RejectsRemaining = outcomeRejects
        };
        return new Fixture(
            port,
            clock,
            outcomes,
            CreateRuntime(port, clock, outcomes));
    }

    private static CharacterConsumablesRuntime CreateRuntime(
        BoundedConsumablesPort port,
        MutableClock clock,
        BoundedOutcomePort outcomes) => new(
        port,
        port,
        port,
        clock,
        new FixedRandomStreams(),
        new DungeonRuntimeAggregateRootStore(),
        DefaultCharacterNeedBalanceRuntime.Instance,
        domainFacts: outcomes,
        calendar: new EditorFixedGameCalendar(1));

    private static DungeonCharacterConsumablesSaveData CloneSave(
        DungeonCharacterConsumablesSaveData source) =>
        JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
            JsonUtility.ToJson(source));

    private static string RemoveTimelineTime(string json)
    {
        var payload = Newtonsoft.Json.Linq.JObject.Parse(json);
        payload.Remove(nameof(DungeonCharacterConsumablesSaveData.timelineTime));
        return payload.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static CharacterMealOperationFailureState CloneFailure(
        CharacterMealOperationFailureState source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        failureCode = source?.failureCode ?? CharacterConsumablesFailureCode.None,
        detail = source?.detail ?? string.Empty,
        recoveryPending = source?.recoveryPending ?? false
    };

    private static bool IsFailure(
        CharacterMealOperationFailureState failure,
        ConsumableOperationId operationId,
        CharacterConsumablesFailureCode failureCode,
        string detail,
        bool recoveryPending) =>
        failure != null
        && failure.operationId == operationId.Value
        && failure.characterId == ActorId.Value
        && failure.facilityInstanceId == FacilityId.Value
        && failure.failureCode == failureCode
        && failure.detail == detail
        && failure.recoveryPending == recoveryPending;

    private static void RequireRestoreRejectedWithoutMutation(
        CharacterConsumablesRuntime runtime,
        DungeonCharacterConsumablesSaveData invalid,
        string description)
    {
        string before = JsonUtility.ToJson(runtime.Capture());
        bool rejected = false;
        try
        {
            runtime.BuildRestoreCandidate(invalid);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, $"Consumables restore accepted {description}.");
        Require(
            string.Equals(before, JsonUtility.ToJson(runtime.Capture()), StringComparison.Ordinal),
            $"Rejected {description} mutated the live consumables aggregate.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal Fixture(
            BoundedConsumablesPort port,
            MutableClock clock,
            BoundedOutcomePort outcomes,
            CharacterConsumablesRuntime runtime)
        {
            Port = port;
            Clock = clock;
            Outcomes = outcomes;
            Runtime = runtime;
        }

        internal BoundedConsumablesPort Port { get; }
        internal MutableClock Clock { get; }
        internal BoundedOutcomePort Outcomes { get; }
        internal CharacterConsumablesRuntime Runtime { get; }
    }

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;

        internal void Advance(float seconds)
        {
            DeltaTime = Mathf.Max(0f, seconds);
            Time += DeltaTime;
            FrameCount++;
        }

        internal void Set(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            DeltaTime = 0f;
            Time = seconds;
            FrameCount++;
        }
    }

    private sealed class FixedRandomStreams : IRandomStreamProvider
    {
        private readonly FixedRandomStream stream = new();

        public int RootSeed => 1;
        public IRandomStream Get(string streamId) => stream;
        public void Reseed(int rootSeed) { }
        public IReadOnlyList<RandomStreamStateSnapshot> CaptureStates() =>
            Array.Empty<RandomStreamStateSnapshot>();
        public RandomStreamRestoreCandidate BuildRestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots) =>
            throw new NotSupportedException();
        public void RestoreStates(RandomStreamRestoreCandidate candidate) =>
            throw new NotSupportedException();
        public void RestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots) =>
            throw new NotSupportedException();
    }

    private sealed class FixedRandomStream : IRandomStream
    {
        public ulong State => 1UL;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
        public bool Chance(float probability) => probability > 0f;
        public void Restore(ulong state) { }
    }

    private sealed class BoundedOutcomePort :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly BoundedConsumablesPort port;
        private readonly DomainFactDispatcher dispatcher = new();
        private readonly BoundedCommitGroup commits;

        internal BoundedOutcomePort(BoundedConsumablesPort port)
        {
            this.port = port ?? throw new ArgumentNullException(nameof(port));
            commits = new BoundedCommitGroup(this);
            Subscribe<CharacterMealConsumedFact>(
                "fixture:bounded-consumables-meal",
                "meal",
                fact => fact.OperationId.Value);
            Subscribe<CharacterSubstanceConsumedFact>(
                "fixture:bounded-consumables-substance",
                "substance",
                fact => fact.OperationId.Value);
            Subscribe<CharacterDetoxAppliedFact>(
                "fixture:bounded-consumables-detox",
                "detox",
                fact => fact.OperationId.Value);
            Subscribe<CharacterPrimitiveMealCompletedFact>(
                "fixture:bounded-consumables-primitive-meal",
                "primitive-meal",
                fact => fact.OperationId.Value);
        }

        internal int RejectsRemaining { get; set; }
        internal bool OmitAcceptedResultKey { get; set; }
        internal int ParentCommitAttempts { get; private set; }
        internal bool AllParentsObservedEffects { get; private set; } = true;

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => dispatcher.TryReserve(
            proposed,
            out lease,
            out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual) => dispatcher.Publish(lease, actual);

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => dispatcher.PublishBatch(
            facts,
            results,
            out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            dispatcher.Replay(fact);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) => dispatcher.ReplayBatch(
            facts,
            results,
            out failure);

        public void Cancel(in DomainFactLease lease) => dispatcher.Cancel(lease);

        private void Subscribe<TFact>(
            string observerId,
            string kind,
            Func<TFact, string> operationId) => dispatcher.Subscribe(
            observerId,
            new BoundedObserver<TFact>(this, kind, operationId));

        private string PublicationKey(string kind, string operationId) =>
            OmitAcceptedResultKey
                ? string.Empty
                : new GameplayResultKey(
                    "qa-bounded-consumables:" + kind,
                    new GameplayOperationId(operationId),
                    1L,
                    0).ToString();

        private sealed class BoundedObserver<TFact> :
            IDurableDomainFactObserver<TFact>,
            IDurableDomainFactRecoveryObserver<TFact>
        {
            private readonly BoundedOutcomePort owner;
            private readonly string kind;
            private readonly Func<TFact, string> operationId;

            internal BoundedObserver(
                BoundedOutcomePort owner,
                string kind,
                Func<TFact, string> operationId)
            {
                this.owner = owner;
                this.kind = kind;
                this.operationId = operationId;
            }

            public bool TryReserve(
                in TFact proposed,
                out object reservation,
                out string failure)
            {
                string operation = operationId(proposed);
                if (string.IsNullOrWhiteSpace(operation))
                {
                    reservation = null;
                    failure = "qa-consumption-operation-id-missing";
                    return false;
                }
                reservation = new Reservation(
                    operation,
                    owner.PublicationKey(kind, operation));
                failure = string.Empty;
                return true;
            }

            public bool TryPrepare(
                in TFact actual,
                object reservation,
                out IDomainFactPreparedPublication prepared,
                out string failure)
            {
                if (reservation is not Reservation value
                    || !string.Equals(
                        value.OperationId,
                        operationId(actual),
                        StringComparison.Ordinal))
                {
                    prepared = null;
                    failure = "qa-consumption-reservation-mismatch";
                    return false;
                }
                prepared = new Publication(
                    owner.commits,
                    value.PublicationKey,
                    value.OperationId,
                    isReplay: false);
                failure = string.Empty;
                return true;
            }

            public DomainFactDelivery Replay(in TFact fact) =>
                DomainFactDelivery.Reject(
                    "qa-consumption-replay-requires-publication-key");

            public bool TryPrepareReplay(
                string publicationKey,
                in TFact fact,
                out IDomainFactPreparedPublication prepared,
                out string failure)
            {
                string operation = operationId(fact);
                if (string.IsNullOrWhiteSpace(publicationKey)
                    || string.IsNullOrWhiteSpace(operation)
                    || !string.Equals(
                        publicationKey,
                        owner.PublicationKey(kind, operation),
                        StringComparison.Ordinal))
                {
                    prepared = null;
                    failure = "qa-consumption-replay-identity-invalid";
                    return false;
                }
                prepared = new Publication(
                    owner.commits,
                    publicationKey,
                    operation,
                    isReplay: true);
                failure = string.Empty;
                return true;
            }

            public void Cancel(object reservation) { }
        }

        private sealed class Reservation : IDomainFactReservedIdentity
        {
            internal Reservation(string operationId, string publicationKey)
            {
                OperationId = operationId;
                PublicationKey = publicationKey;
            }

            internal string OperationId { get; }
            public string PublicationKey { get; }
        }

        private sealed class Publication :
            IDomainFactPreparedPublication,
            IDomainFactPreparedIdentity
        {
            internal Publication(
                BoundedCommitGroup commits,
                string operationKey,
                string operationId,
                bool isReplay)
            {
                CommitGroup = commits;
                OperationKey = operationKey;
                OperationId = operationId;
                IsReplay = isReplay;
            }

            public IDomainFactCommitGroup CommitGroup { get; }
            public string OperationKey { get; }
            public long OwnerRevision => 1L;
            public bool IsReplay { get; }
            internal string OperationId { get; }
            public void Cancel() { }
        }

        private sealed class BoundedCommitGroup : IDomainFactCommitGroup
        {
            private readonly BoundedOutcomePort owner;

            internal BoundedCommitGroup(BoundedOutcomePort owner) =>
                this.owner = owner;

            public bool Commit(
                IReadOnlyList<IDomainFactPreparedPublication> publications,
                DomainFactDelivery[] results,
                out string failure)
            {
                owner.ParentCommitAttempts++;
                Publication[] bounded = publications
                    .Cast<Publication>()
                    .ToArray();
                if (bounded.Any(value =>
                        !owner.port.HasAppliedEffect(value.OperationId)))
                {
                    owner.AllParentsObservedEffects = false;
                }
                if (owner.RejectsRemaining > 0)
                {
                    owner.RejectsRemaining--;
                    failure = "qa-parent-rejected";
                    for (int index = 0; index < results.Length; index++)
                        results[index] = DomainFactDelivery.Reject(failure);
                    return false;
                }
                failure = string.Empty;
                for (int index = 0; index < results.Length; index++)
                {
                    results[index] = new DomainFactDelivery(
                        DomainFactDeliveryPhase.Delivered,
                        bounded[index].OperationKey,
                        bounded[index].OperationKey,
                        string.Empty,
                        "qa-parent-accepted");
                }
                return true;
            }
        }
    }

    private sealed class BoundedConsumablesPort :
        ICharacterConsumablesWorldPort,
        ICharacterConsumablesInventoryPort,
        ICharacterConsumablesEventPort,
        ICharacterConsumablesEffectTransactionPort,
        ICharacterRitualFastingMealPort
    {
        private readonly CharacterConsumablesMealDefinitionSnapshot meal;
        private readonly CharacterConsumablesSubstanceDefinitionSnapshot substance;
        private readonly List<CharacterConsumablesStackSnapshot> stacks;
        private readonly float hunger;
        private readonly bool requiresHungerRecovery;
        private readonly bool acceptsMealDelivery;
        private readonly ICharacterRitualFastingMealPort ritualFasting;
        private readonly Dictionary<string, ItemStackId> mealStacksByLease =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterMealPhysicalCommitSnapshot>
            pendingMeals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterSubstancePhysicalCommitSnapshot>
            pendingSubstances = new(StringComparer.Ordinal);
        private readonly HashSet<string> appliedOperationIds =
            new(StringComparer.Ordinal);

        internal BoundedConsumablesPort(
            bool contaminatedMeal,
            float hunger = 0f,
            MealServingRole mealServingRole = MealServingRole.FullMeal,
            CharacterConsumablesStackState mealStackState =
                CharacterConsumablesStackState.FacilityBuffer,
            bool acceptsMealDelivery = false,
            ICharacterRitualFastingMealPort ritualFasting = null,
            bool requiresHungerRecovery = false)
        {
            this.hunger = hunger;
            this.requiresHungerRecovery = requiresHungerRecovery;
            this.acceptsMealDelivery = acceptsMealDelivery;
            this.ritualFasting = ritualFasting;
            meal = new CharacterConsumablesMealDefinitionSnapshot(
                MealId,
                "QA B0 meal",
                MealDietClass.Vegan,
                MealQualityTier.Simple,
                nutrition: 30f,
                mood: 4f,
                unitPrice: 1,
                forbiddenIngredient: false,
                sweet: false,
                salted: false,
                servingRole: mealServingRole);
            substance = new CharacterConsumablesSubstanceDefinitionSnapshot(
                SubstanceId,
                new SubstanceDefinitionView(
                    "substance:qa-b0-con",
                    SubstanceId.Value,
                    "QA B0 substance",
                    SubstanceUseClass.Medicine,
                    addictionChance: 0f,
                    overdoseChance: 1f,
                    toleranceGain: 10f,
                    withdrawalPerHour: 0f,
                    moodEffect: 8f,
                    workSpeedEffect: 0f,
                    combatEffect: 0f,
                    fatigueAccumulationReduction: 0f,
                    researchSpeedEffect: 0f,
                    suppressesPerceivedPain: false,
                    durationSeconds: 60f,
                    requiredResearchId: string.Empty));
            stacks = new List<CharacterConsumablesStackSnapshot>
            {
                new CharacterConsumablesStackSnapshot(
                    MealStackId,
                    MealId,
                    quantity: 1,
                    mealStackState,
                    mealStackState == CharacterConsumablesStackState.FacilityBuffer
                        ? CharacterConsumablesRuntime.GetMealDestinationId(
                            FacilityId,
                            MealId)
                        : "storage:qa-b0-con-meal",
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: contaminatedMeal ? 1f : 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 60f,
                    preserved: false),
                new CharacterConsumablesStackSnapshot(
                    SubstanceStackId,
                    SubstanceId,
                    quantity: 1,
                    CharacterConsumablesStackState.Carried,
                    ActorId.Value,
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 60f,
                    preserved: false)
            };
        }

        internal int MealPhysicalCommits { get; private set; }
        internal int SubstancePhysicalCommits { get; private set; }
        internal int MealEffectApplications { get; private set; }
        internal int SubstanceEffectApplications { get; private set; }
        internal int MealAcknowledgements { get; private set; }
        internal int SubstanceAcknowledgements { get; private set; }
        internal float MealHungerRecovered { get; private set; }
        internal float SubstanceMoodApplied { get; private set; }
        internal float SubstanceDamageApplied { get; private set; }
        internal int RitualMealNotifications { get; private set; }
        internal bool HasAppliedEffect(string operationId) =>
            appliedOperationIds.Contains(operationId ?? string.Empty);

        public IReadOnlyList<CharacterId> CharacterIds => new[] { ActorId };
        public IReadOnlyList<BuildingInstanceId> FacilityIds => new[] { FacilityId };

        public bool IsRitualFasting(CharacterId characterId) =>
            ritualFasting?.IsRitualFasting(characterId) == true;

        public bool RecordMealConsumed(
            CharacterId characterId,
            bool directPlayerOrder,
            CharacterMealConsumedFact meal,
            string mealResultKey,
            out string failureReason)
        {
            RitualMealNotifications++;
            failureReason = string.Empty;
            return ritualFasting == null || ritualFasting.RecordMealConsumed(
                characterId, directPlayerOrder, meal, mealResultKey, out failureReason);
        }

        public bool TryGetActor(
            CharacterId id,
            out CharacterConsumablesActorSnapshot actor)
        {
            actor = id.Equals(ActorId)
                ? new CharacterConsumablesActorSnapshot(
                    ActorId,
                    active: true,
                    health: 100f,
                    maxHealth: 100f,
                    mood: 0f,
                    hunger: hunger,
                    combatStance: false,
                    requiresHungerRecovery: requiresHungerRecovery)
                : default;
            return actor.Id.IsValid;
        }

        public bool TryGetFacility(
            BuildingInstanceId id,
            out CharacterConsumablesFacilitySnapshot facility)
        {
            facility = id.Equals(FacilityId)
                ? new CharacterConsumablesFacilitySnapshot(
                    FacilityId,
                    mealFacility: true,
                    recreationalSubstanceFacility: false,
                    Vector2Int.zero)
                : default;
            return facility.Id.IsValid;
        }

        public CharacterCultureMealPreference GetCultureMealPreference(
            CharacterId characterId,
            ConsumableItemDefinitionId itemId) =>
            CharacterCultureMealPreference.Neutral;

        public float ProjectGameplayEffect(
            CharacterId characterId,
            string targetId,
            float baseValue) => baseValue;

        public float GetBehaviorUtilityMultiplier(
            CharacterId characterId,
            IReadOnlyCollection<string> semanticTags) => 1f;

        public bool TryReserveMealFacilitySlot(
            ConsumableOperationId operationId,
            CharacterId characterId,
            BuildingInstanceId facilityId) => true;

        public void ReleaseMealFacilitySlot(
            ConsumableOperationId operationId,
            BuildingInstanceId facilityId) { }

        public void RecoverHunger(CharacterId id, float amount) =>
            MealHungerRecovered += amount;

        public void ApplyMood(
            CharacterId id,
            string sourceId,
            string label,
            float value,
            float durationSeconds) => SubstanceMoodApplied += value;

        public void ApplyDamage(CharacterId id, float amount, string reason) =>
            SubstanceDamageApplied += amount;

        public void RecordNeedNarrative(
            CharacterId id,
            string factId,
            string subjectId,
            string outcome,
            float value) { }

        public bool TryApplyMealEffects(
            in CharacterMealEffectCommand command,
            out CharacterConsumablesEffectEvidence evidence,
            out string failureReason)
        {
            MealEffectApplications++;
            MealHungerRecovered += command.Nutrition;
            appliedOperationIds.Add(command.OperationId.Value);
            evidence = new CharacterConsumablesEffectEvidence(
                0f,
                MealHungerRecovered,
                100f,
                command.ApplyContaminationDamage ? 97f : 100f,
                0f,
                command.Mood);
            failureReason = string.Empty;
            return true;
        }

        public bool TryApplySubstanceEffects(
            in CharacterSubstanceEffectCommand command,
            out string failureReason)
        {
            SubstanceEffectApplications++;
            SubstanceMoodApplied += command.Mood;
            if (command.Overdosed)
                SubstanceDamageApplied += command.OverdoseDamage;
            appliedOperationIds.Add(command.OperationId.Value);
            failureReason = string.Empty;
            return true;
        }

        public IReadOnlyList<CharacterConsumablesStackSnapshot> GetAllStacks() =>
            stacks;

        public IReadOnlyList<CharacterConsumablesSubstanceDefinitionSnapshot>
            GetSubstances() => new[] { substance };

        public bool TryGetMeal(
            ConsumableItemDefinitionId id,
            out CharacterConsumablesMealDefinitionSnapshot value)
        {
            value = id.Equals(MealId) ? meal : default;
            return value.Id.IsValid;
        }

        public bool TryResolveSubstance(
            string substanceOrItemId,
            out CharacterConsumablesSubstanceDefinitionSnapshot value)
        {
            value = string.Equals(
                    substanceOrItemId,
                    SubstanceId.Value,
                    StringComparison.Ordinal)
                || string.Equals(
                    substanceOrItemId,
                    substance.Definition.SubstanceId,
                    StringComparison.Ordinal)
                ? substance
                : default;
            return value.Id.IsValid;
        }

        public bool TryResolveSubstance(
            ConsumableItemDefinitionId id,
            out CharacterConsumablesSubstanceDefinitionSnapshot value)
        {
            value = id.Equals(SubstanceId) ? substance : default;
            return value.Id.IsValid;
        }

        public bool TryConsume(ItemStackId stackId, int quantity) => true;

        public bool TryReserveMealQuantity(
            ConsumableOperationId operationId,
            CharacterId characterId,
            BuildingInstanceId facilityId,
            ItemStackId stackId,
            out string leaseId)
        {
            leaseId = "lease:" + operationId.Value;
            mealStacksByLease[leaseId] = stackId;
            return true;
        }

        public bool RevalidateMealQuantity(string leaseId, ItemStackId stackId) =>
            mealStacksByLease.TryGetValue(leaseId, out ItemStackId reserved)
            && reserved.Equals(stackId);

        public bool TryRebindMealQuantityLease(
            ConsumableOperationId operationId,
            out string leaseId,
            out ItemStackId stackId)
        {
            leaseId = "lease:" + operationId.Value;
            return mealStacksByLease.TryGetValue(leaseId, out stackId);
        }

        public bool TryResolveMealQuantityStack(
            string leaseId,
            out ItemStackId stackId) =>
            mealStacksByLease.TryGetValue(leaseId, out stackId);

        public bool TryCommitReservedMealQuantityPending(
            ConsumableOperationId operationId,
            string leaseId,
            int quantity,
            out CharacterMealPhysicalCommitSnapshot commit,
            out string failureReason)
        {
            if (!mealStacksByLease.TryGetValue(leaseId, out ItemStackId stackId))
            {
                commit = default;
                failureReason = "qa-meal-lease-missing";
                return false;
            }
            MealPhysicalCommits++;
            commit = new CharacterMealPhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.MealPhysicalSinkReason,
                "physical-batch-disposition:qa-meal:"
                    + operationId.Value,
                new[] { stackId.Value },
                quantity,
                inputMassGrams: 100L);
            pendingMeals[operationId.Value] = commit;
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPendingMealConsumption(
            ConsumableOperationId operationId,
            out CharacterMealPhysicalCommitSnapshot commit) =>
            pendingMeals.TryGetValue(operationId.Value, out commit);

        public bool TryAcknowledgeMealConsumption(
            string commitId,
            out string failureReason)
        {
            string acknowledgedOperationId = string.Empty;
            foreach (KeyValuePair<string, CharacterMealPhysicalCommitSnapshot> pair
                     in pendingMeals)
            {
                if (!string.Equals(pair.Value.CommitId, commitId, StringComparison.Ordinal))
                    continue;
                acknowledgedOperationId = pair.Key;
                break;
            }
            if (!string.IsNullOrWhiteSpace(acknowledgedOperationId))
            {
                pendingMeals.Remove(acknowledgedOperationId);
                MealAcknowledgements++;
                failureReason = string.Empty;
                return true;
            }
            failureReason = "qa-meal-commit-missing";
            return false;
        }

        public bool TryCommitSubstanceConsumptionPending(
            ConsumableOperationId operationId,
            CharacterId characterId,
            ItemStackId stackId,
            out CharacterSubstancePhysicalCommitSnapshot commit,
            out string failureReason)
        {
            SubstancePhysicalCommits++;
            commit = new CharacterSubstancePhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.SubstancePhysicalSinkReason,
                "physical-batch-disposition:qa-substance:"
                    + operationId.Value,
                new[] { stackId.Value },
                quantity: 1,
                inputMassGrams: 10L);
            pendingSubstances[operationId.Value] = commit;
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPendingSubstanceConsumption(
            ConsumableOperationId operationId,
            out CharacterSubstancePhysicalCommitSnapshot commit) =>
            pendingSubstances.TryGetValue(operationId.Value, out commit);

        public bool TryAcknowledgeSubstanceConsumption(
            string commitId,
            out string failureReason)
        {
            string acknowledgedOperationId = string.Empty;
            foreach (KeyValuePair<string, CharacterSubstancePhysicalCommitSnapshot>
                     pair in pendingSubstances)
            {
                if (!string.Equals(pair.Value.CommitId, commitId, StringComparison.Ordinal))
                    continue;
                acknowledgedOperationId = pair.Key;
                break;
            }
            if (!string.IsNullOrWhiteSpace(acknowledgedOperationId))
            {
                pendingSubstances.Remove(acknowledgedOperationId);
                SubstanceAcknowledgements++;
                failureReason = string.Empty;
                return true;
            }
            failureReason = "qa-substance-commit-missing";
            return false;
        }

        public bool TryRequestDelivery(
            ConsumableItemDefinitionId itemId,
            int quantity,
            Vector2Int position,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            if (acceptsMealDelivery && itemId.Equals(MealId) && quantity == 1)
            {
                requested = 1;
                failureReason = string.Empty;
                return true;
            }
            requested = 0;
            failureReason = "qa-delivery-not-required";
            return false;
        }

        public void Publish(CharacterConsumablesMealConsumedEvent consumedEvent) { }
    }
}
#endif
