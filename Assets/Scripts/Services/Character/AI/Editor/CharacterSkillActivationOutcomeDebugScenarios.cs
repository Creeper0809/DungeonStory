#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public static class CharacterSkillActivationOutcomeDebugScenarios
{
    private const string ReportEnvironmentVariable =
        "DUNGEONSTORY_A05_REPORT_PATH";
    private const string DefaultReportPath =
        "Artifacts/QA/GameplayActionLedgerAudit-20260926-r1/A05/"
        + "focused-latest.txt";

    public static void RunBatch()
    {
        string reportPath = Environment.GetEnvironmentVariable(ReportEnvironmentVariable);
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || string.IsNullOrWhiteSpace(reportPath) || Directory.Exists(output)
            || !string.Equals(reportPath, Path.Combine(output, "a05-caller-boundaries.txt"),
                StringComparison.OrdinalIgnoreCase) || File.Exists(reportPath))
            throw new InvalidOperationException("A05 batch requires an isolated, explicit new report path.");
        // Edit fixtures do not use full-world persistence. Validate the
        // launcher's new in-project evidence boundary without touching saves.
        _ = DungeonPersistencePaths.ValidateVerificationRoot(Path.Combine(output, "persistent-data"),
            output, Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
        UnityEditor.EditorApplication.Exit(RunAll(log: true) ? 0 : 1);
    }

    public static void VerifyAppliedUltimateFocused() =>
        VerifyManualCombatUltimateSharedContract(new List<string>());

    public static void VerifyStandaloneObserverFocused()
    {
        var evidence = new List<string>();
        VerifyOwnerCommandProductionEntry(evidence);
        VerifyOwnerCommandTargetingLifetime(evidence);
        VerifyExpeditionProductionEntry(evidence);
        VerifyAutomaticPassiveProductionEntry(evidence, ownerFixed: true);
        VerifyAutomaticUltimateProductionEntry(evidence);
        VerifyDefenseUltimateProductionEntry(evidence);
        VerifyAutomaticFailureRollsBack(evidence);
        VerifyOutcomeRejectionRollsBack(evidence);
        VerifyRetainedUncertaintyRestoresAndReplays(evidence);
    }

    public static bool RunAll(bool log = false)
    {
        var evidence = new List<string>();
        try
        {
            VerifyOwnerCommandProductionEntry(evidence);
            VerifyOwnerCommandTargetingLifetime(evidence);
            VerifyExpeditionProductionEntry(evidence);
            VerifyAutomaticPassiveProductionEntry(evidence);
            VerifyAutomaticPassiveProductionEntry(evidence, ownerFixed: true);
            VerifyBattlePassiveActualEffect(evidence);
            VerifyAutomaticUltimateProductionEntry(evidence);
            VerifyDefenseUltimateProductionEntry(evidence);
            VerifyManualCombatUltimateSharedContract(evidence);
            VerifyAutomaticFailureRollsBack(evidence);
            VerifyCalculationOnlyPassiveDoesNotClaimEffect(evidence);
            VerifyAutomaticOwnerRewindRejection(evidence);
            VerifyExactMultiTargetSaveAndOwnerRewindRejection(evidence);
            VerifyRetainedUncertaintyRestoresAndReplays(evidence);
            VerifyMiddleRecipientFailureRollsBack(evidence);
            VerifyOutcomeRejectionRollsBack(evidence);
            VerifyInvalidRecipientsDoNotPrepare(evidence);
            WriteReport("PASS", evidence, string.Empty);
            if (log)
                Debug.Log("A05 character-skill activation scenarios passed.");
            return true;
        }
        catch (Exception exception)
        {
            WriteReport("FAIL", evidence, exception.ToString());
            if (log)
                Debug.LogError(exception);
            return false;
        }
    }

    private static void VerifyAutomaticPassiveProductionEntry(
        ICollection<string> evidence, bool ownerFixed = false)
    {
        string sourceId = ownerFixed ? CharacterId.Owner.Value : "character:g13-passive-source";
        using var source = new ActorFixture(
            "automatic-passive-source",
            sourceId, ownerFixed);
        Require(source.Actor.Identity.PersistentId == sourceId, "Passive fixture owner identity changed unexpectedly.");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-passive",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = ownerFixed
            ? source.Actor.Progression.OwnerFixedSkills.Single(value => value.id == "owner_fixed_morale")
            : CreateAutomaticSkill(
            "skill:g13:passive-mood",
            CharacterSkillKind.Passive,
            CharacterSkillTrigger.WorkCompleted,
            CharacterUltimateDomain.None,
            "mood",
            "small");
        if (!ownerFixed)
            source.Actor.Progression.GrowthState.passiveSkills.Add(skill.Clone());
        source.Actor.Progression.ConstructSkillActivationRuntime(
            outcomes.Transaction);
        int factorsBefore = source.Actor.Stats
            .CaptureMoodDeliveryTransactionState().Factors.Count;
        var context = new CharacterSkillExecutionContext(
            source.Actor,
            CharacterSkillTrigger.WorkCompleted,
            "work-completed:g13:passive:0001",
            workTypeId: BuiltInWorkTypeIds.Clean);

        if (ownerFixed)
        {
            AbilityWork work = source.Actor.GetAbility<AbilityWork>();
            WorkPriorityLevel priorityBefore = work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Clean);
            bool liveRestoreRejected = false;
            try { work.RestoreWorkPriority(BuiltInWorkTypeIds.Clean, WorkPriorityLevel.Priority1); }
            catch (InvalidOperationException) { liveRestoreRejected = true; }
            Require(liveRestoreRejected && work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Clean) == priorityBefore,
                "A published actor bypassed the work command through the detached restore API.");
            long narrativeSequence = source.Actor.Progression.NarrativeLedger.nextEventSequence;
            foreach (string outcome in new[] { CharacterActivityOutcomes.Changed,
                CharacterActivityOutcomes.Failed, CharacterActivityOutcomes.Cancelled })
                source.Actor.AddActivity(CharacterActivityEvent.Work(FacilityWorkType.Clean,
                    outcome, "업무 설정 또는 미완료 결과", reasonCode: "qa:non-completion"));
            Require(Count(outcomes.Ledger) == 0
                    && source.Actor.Stats.CaptureMoodDeliveryTransactionState().Factors.Count == factorsBefore
                    && source.Actor.Progression.NarrativeLedger.nextEventSequence == narrativeSequence + 3,
                "A non-completed work activity triggered WorkCompleted effects or lost its narrative record.");
            CharacterSkillInstance wrongKind = skill.Clone();
            wrongKind.kind = CharacterSkillKind.Active;
            Require(!outcomes.Transaction.TryCommitAutomatic(
                    new CharacterSkillAutomaticActivationRequest(context, wrongKind,
                        CharacterSkillActivationKind.AutomaticPassive, context.EventId),
                    out _, out string wrongKindReason)
                    && wrongKindReason == "skill-automatic-passive-contract-invalid"
                    && Count(outcomes.Ledger) == 0
                    && source.Actor.Stats.CaptureMoodDeliveryTransactionState().Factors.Count == factorsBefore,
                "Allowing learned owner-fixed passives also admitted an active skill or changed state on rejection.");
        }

        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(context);
        int factorsAfter = source.Actor.Stats
            .CaptureMoodDeliveryTransactionState().Factors.Count;
        GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
        CharacterSkillActivationPublicationData publication =
            outcomes.Publications.Find(sourceId, 1L);
        Require(factorsAfter == factorsBefore + 1
                && Count(outcomes.Ledger) == 1
                && Limits(source.Actor).automaticSkillActivations.Count == 1
                && Limits(source.Actor).activationSequence == 1L
                && publication != null
                && !publication.pending
                && publication.ownerFinalized
                && publication.publicationKey
                    == Limits(source.Actor).lastSkillActivation.resultKey
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.ActivationKind)
                    == CharacterSkillActivationKind.AutomaticPassive.ToString()
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.TriggerEventId)
                    == context.EventId,
            "Automatic passive did not commit its actual effect and source event.");

        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(context);
        Require(Count(outcomes.Ledger) == 1
                && Limits(source.Actor).automaticSkillActivations.Count == 1
                && source.Actor.Stats.CaptureMoodDeliveryTransactionState()
                    .Factors.Count == factorsAfter,
            "Automatic passive source-event redelivery applied twice.");

        string serialized = JsonUtility.ToJson(Limits(source.Actor));
        CharacterSkillUseLimitState restored =
            JsonUtility.FromJson<CharacterSkillUseLimitState>(serialized);
        restored.ValidatePersistentState(
            sourceId,
            source.Actor.Progression.PassiveSkills.Concat(source.Actor.Progression.OwnerFixedSkills));
        restored.automaticSkillActivations[0].eventKeySha256 =
            new string('0', 64);
        RequireThrows(
            () => restored.ValidatePersistentState(
                sourceId,
                source.Actor.Progression.PassiveSkills.Concat(source.Actor.Progression.OwnerFixedSkills)),
            "Tampered automatic-passive event key survived save validation.");
        if (ownerFixed)
            VerifyOwnerFixedWorldValidation(source.Actor, evidence);
        evidence.Add(
            (ownerFixed ? "ownerFixedPassive=" : "automaticPassive=") + "production-dispatch+actual-mood-effect;"
            + "sameEventReplay=deduplicated;saveRoundTrip=pass;tamper=rejected");
    }

    private static void VerifyOwnerFixedWorldValidation(
        CharacterActor source, ICollection<string> evidence)
    {
        CharacterSO definition = source.Identity.Data;
        var saved = new DungeonCharacterSaveData
        {
            persistentId = source.Identity.PersistentId, dataId = definition.id,
            isOwner = source.IsOwner, lifecycleRevision = source.Lifecycle.LifecycleRevision,
            level = source.Progression.Level, currentExperience = source.Progression.CurrentExperience,
            restObservation = source.Stats.CaptureRestObservation(),
            growth = source.Progression.GrowthState.Clone(),
            narrative = source.Progression.NarrativeLedger.Clone(),
            acquiredTraits = source.Progression.CaptureAcquiredTraitState()
        };
        bool hadRestObservation = saved.restObservation != null;
        saved = DungeonSaveJson.Deserialize<DungeonCharacterSaveData>(
            DungeonCharacterWorldSaveCodec.SerializeFields(saved));
        Require((saved.restObservation != null) == hadRestObservation,
            "Actor fields JSON changed the presence of an optional rest receipt.");
        var valid = new DungeonGameRestoreReport();
        CharacterWorldSaveValidation.ValidateActor(saved, saved.persistentId, valid, definition);
        Require(valid.Success, "Authored fixed-skill activation failed actor save validation: "
            + string.Join(" | ", valid.Errors));
        var missing = new DungeonGameRestoreReport();
        CharacterWorldSaveValidation.ValidateActor(saved, saved.persistentId, missing);
        Require(missing.Errors.Any(value => value.Contains("activation owner is incoherent")),
            "Missing authored fixed-skill definition was silently supplied.");

        var profile = new WorldCharacterProfile
        {
            persistentId = saved.persistentId, characterDataId = saved.dataId,
            isOwner = saved.isOwner, growth = saved.growth.Clone(), narrative = saved.narrative.Clone()
        };
        var definitions = new Dictionary<int, CharacterSO> { [definition.id] = definition };
        var population = new DungeonGameRestoreReport();
        CharacterWorldSaveValidation.ValidatePopulationProfiles(new[] { profile }, population, definitions);
        Require(population.Success, "Authored fixed-skill activation failed population save validation: "
            + string.Join(" | ", population.Errors));

        using var fresh = new ActorFixture("restored-fixed-skill", saved.persistentId, owner: true);
        var progressionSnapshot = new CharacterProgressionSnapshot(saved.level, saved.currentExperience,
            saved.growth, saved.narrative, saved.acquiredTraits);
        fresh.Actor.Progression.RestorePersistentState(progressionSnapshot);
        string restoredLimits = JsonUtility.ToJson(fresh.Actor.Progression.GrowthState.useLimits);
        Require(restoredLimits == JsonUtility.ToJson(saved.growth.useLimits)
                && !fresh.Actor.Progression.ActiveSkills.Concat(fresh.Actor.Progression.PassiveSkills)
                    .Any(value => value.id == saved.growth.useLimits.lastSkillActivation.skillId),
            "Fresh progression restore changed fixed-skill activation or copied it into mutable learned skills.");
        progressionSnapshot.GrowthState.useLimits.lastSkillActivation.mechanicsSha256 = new string('0', 64);
        RequireThrows(() => fresh.Actor.Progression.RestorePersistentState(progressionSnapshot),
            "Fresh progression restore accepted forged fixed-skill mechanics.");
        Require(JsonUtility.ToJson(fresh.Actor.Progression.GrowthState.useLimits) == restoredLimits,
            "Rejected fixed-skill restore changed the live activation owner.");

        saved.growth.useLimits.lastSkillActivation.mechanicsSha256 = new string('0', 64);
        profile.growth.useLimits.lastSkillActivation.mechanicsSha256 = new string('0', 64);
        var tampered = new DungeonGameRestoreReport();
        CharacterWorldSaveValidation.ValidateActor(saved, saved.persistentId, tampered, definition);
        CharacterWorldSaveValidation.ValidatePopulationProfiles(new[] { profile }, tampered, definitions);
        Require(tampered.Errors.Count(value => value.Contains("activation owner is incoherent")) == 2,
            "Actor or population validation accepted a forged fixed-skill mechanics hash.");
        saved.dataId = definition.id + 1;
        var wrongDefinition = new DungeonGameRestoreReport();
        CharacterWorldSaveValidation.ValidateActor(saved, saved.persistentId, wrongDefinition, definition);
        Require(wrongDefinition.Errors.Any(value => value.Contains("skill definition does not match")),
            "Save validation accepted another character definition.");
        evidence.Add("ownerFixedWorldValidation=actor+population+json+fresh-progression-restore;authored-definition-required;"
            + "forged-mechanics+wrong-definition=rejected;no-mutable-fixed-skill-copy");
    }

    private static void VerifyAutomaticUltimateProductionEntry(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "automatic-ultimate-source",
            "character:g13-ultimate-source");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-ultimate",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateAutomaticSkill(
            "skill:g13:management-ultimate",
            CharacterSkillKind.Ultimate,
            CharacterSkillTrigger.OperatingDayStarted,
            CharacterUltimateDomain.Management,
            "mood",
            "small");
        source.Actor.Progression.GrowthState.ultimate = skill.Clone();
        var world = new TestWorldRegistry();
        world.RegisterCharacter(source.Actor);
        var events = new GameEventBus();
        using var runtime = new CharacterSkillAutomaticTriggerRuntime(
            world,
            events,
            outcomes.Transaction);
        runtime.Start();

        events.Publish(new OperatingDayStartedEvent(12));
        GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
        int factorCount = source.Actor.Stats
            .CaptureMoodDeliveryTransactionState().Factors.Count;
        Require(Limits(source.Actor).managementOperatingDay == 12
                && factorCount == 1
                && Count(outcomes.Ledger) == 1
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.ActivationKind)
                    == CharacterSkillActivationKind.AutomaticUltimate.ToString()
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.UltimateDomain)
                    == CharacterUltimateDomain.Management.ToString()
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.TriggerSerial) == "12",
            "Operating-day ultimate bypassed the automatic transaction.");
        events.Publish(new OperatingDayStartedEvent(12));
        Require(Count(outcomes.Ledger) == 1
                && Limits(source.Actor).automaticSkillActivations.Count == 1
                && source.Actor.Stats.CaptureMoodDeliveryTransactionState()
                    .Factors.Count == factorCount,
            "Operating-day ultimate replay consumed or applied twice.");
        evidence.Add(
            "automaticUltimate=OperatingDayStarted-public-observer;"
            + "limit+effect+outcome=atomic;sameDayReplay=deduplicated");
    }

    private static void VerifyBattlePassiveActualEffect(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "automatic-battle-passive-source",
            "character:g13-battle-passive-source");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-battle-passive",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateAutomaticSkill(
            "skill:g13:battle-start-damage",
            CharacterSkillKind.Passive,
            CharacterSkillTrigger.BattleStarted,
            CharacterUltimateDomain.None,
            "damage",
            "light");
        skill.target = CharacterSkillTarget.Enemy;
        skill.targetingMode = CharacterSkillTargetingMode.PlayerSelected;
        source.Actor.Progression.GrowthState.passiveSkills.Add(skill.Clone());
        source.Actor.Progression.ConstructSkillActivationRuntime(
            outcomes.Transaction);

        OffenseBattleCombatant ally = new(
            CharacterPersistentIdentity.Require(source.Actor).Value,
            source.Actor.Identity.DisplayName,
            source.Actor.Identity.SpeciesTag,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 8f, 4f, 1f, 4f, 4f),
            100f);
        OffenseBattleCombatant enemy = new(
            "character:g13-battle-passive-target",
            "G13 Passive Target",
            "Training",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(100f, 4f, 2f, 1f, 2f, 2f),
            100f);
        OffenseBattleSession session = CreateBattleSession(
            "g13-battle-passive-session",
            ally,
            enemy);
        var context = new CharacterSkillExecutionContext(
            source.Actor,
            CharacterSkillTrigger.BattleStarted,
            "battle:g13:passive:started",
            session,
            ally,
            enemy);
        float healthBefore = enemy.CurrentHealth;

        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(context);
        float healthAfter = enemy.CurrentHealth;
        GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
        Require(healthAfter < healthBefore
                && Count(outcomes.Ledger) == 1
                && Limits(source.Actor).automaticSkillActivations.Count == 1
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.ActivationKind)
                    == CharacterSkillActivationKind.AutomaticPassive.ToString(),
            "Battle-start passive did not preserve its actual combat target delta.");

        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(context);
        Require(Mathf.Approximately(enemy.CurrentHealth, healthAfter)
                && Count(outcomes.Ledger) == 1,
            "Battle-start passive source event applied twice.");
        evidence.Add(
            "battlePassive=actual-OffenseBattleSession-target-delta;"
            + "sameEventReplay=deduplicated;outcome=1");
    }

    private static void VerifyDefenseUltimateProductionEntry(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "automatic-defense-source",
            "character:g13-defense-source");
        using var intruder = new ActorFixture(
            "automatic-defense-intruder",
            "character:g13-defense-intruder");
        using var witness = new ActorFixture(
            "automatic-defense-witness",
            "character:g13-defense-witness");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-defense-ultimate",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateAutomaticSkill(
            "skill:g13:defense-ultimate",
            CharacterSkillKind.Ultimate,
            CharacterSkillTrigger.InvasionStarted,
            CharacterUltimateDomain.Defense,
            "damage",
            "light");
        skill.target = CharacterSkillTarget.Enemy;
        skill.targetingMode = CharacterSkillTargetingMode.PlayerSelected;
        source.Actor.Progression.GrowthState.ultimate = skill.Clone();
        var world = new TestWorldRegistry();
        world.RegisterCharacter(source.Actor);
        world.RegisterCharacter(witness.Actor);
        var events = new GameEventBus();
        using var runtime = new CharacterSkillAutomaticTriggerRuntime(
            world,
            events,
            outcomes.Transaction);
        using var exposure = new InvasionExposureExperienceObserver(
            events,
            world);
        runtime.Start();
        exposure.Start();
        exposure.Start();
        var spawned = new InvasionSpawnedEvent(
            intruder.Actor,
            new InvasionThreatSnapshot(
                100f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(1f, 1f, 1f, 1f),
                0f,
                0f));
        float healthBefore = intruder.Actor.Stats.CurrentHealth;
        long sourceNarrativeBefore = source.Actor.Progression
            .NarrativeLedger.nextEventSequence;
        long witnessNarrativeBefore = witness.Actor.Progression
            .NarrativeLedger.nextEventSequence;

        events.Publish(spawned);
        float healthAfter = intruder.Actor.Stats.CurrentHealth;
        GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
        int serial = Limits(source.Actor).defenseInvasionSerial;
        Require(healthAfter < healthBefore
                && serial >= 0
                && Count(outcomes.Ledger) == 1
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.UltimateDomain)
                    == CharacterUltimateDomain.Defense.ToString()
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.TriggerSerial)
                    == serial.ToString(CultureInfo.InvariantCulture)
                && source.Actor.Progression.NarrativeLedger
                    .nextEventSequence == sourceNarrativeBefore + 1L
                && witness.Actor.Progression.NarrativeLedger
                    .nextEventSequence == witnessNarrativeBefore + 1L,
            "Invasion observer bypassed the defense-ultimate transaction.");

        events.Publish(spawned);
        Require(Mathf.Approximately(
                    intruder.Actor.Stats.CurrentHealth,
                    healthAfter)
                && Count(outcomes.Ledger) == 1
                && Limits(source.Actor).automaticSkillActivations.Count == 1
                && source.Actor.Progression.NarrativeLedger
                    .nextEventSequence == sourceNarrativeBefore + 2L
                && witness.Actor.Progression.NarrativeLedger
                    .nextEventSequence == witnessNarrativeBefore + 2L,
            "The same invasion applied the defense ultimate twice.");
        exposure.Dispose();
        events.Publish(spawned);
        Require(source.Actor.Progression.NarrativeLedger.nextEventSequence
                    == sourceNarrativeBefore + 2L
                && witness.Actor.Progression.NarrativeLedger.nextEventSequence
                    == witnessNarrativeBefore + 2L,
            "Disposed invasion exposure observer recorded a later event.");
        evidence.Add(
            "defenseUltimate=InvasionSpawned-public-observer+actual-target-damage;"
            + "limit+effect+outcome=atomic;sameInvasionReplay=deduplicated;"
            + "exposureObserver=start-idempotent+one-per-actor-event+disposed");
    }

    private static void VerifyAutomaticFailureRollsBack(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "automatic-reject-source",
            "character:g13-reject-source");
        CharacterSkillInstance skill = CreateAutomaticSkill(
            "skill:g13:reject-ultimate",
            CharacterSkillKind.Ultimate,
            CharacterSkillTrigger.OperatingDayStarted,
            CharacterUltimateDomain.Management,
            "mood",
            "small");
        source.Actor.Progression.GrowthState.ultimate = skill.Clone();
        var rejectingObserver = new RejectingActivationObserver();
        using var rejectedOutcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-reject-ultimate",
            calendar: EditorFixedGameCalendar.Instance,
            activationObserver: rejectingObserver);
        int factorsBefore = source.Actor.Stats
            .CaptureMoodDeliveryTransactionState().Factors.Count;
        var request = new CharacterSkillAutomaticActivationRequest(
            new CharacterSkillExecutionContext(
                source.Actor,
                CharacterSkillTrigger.OperatingDayStarted,
                "operating-day-started:77"),
            skill,
            CharacterSkillActivationKind.AutomaticUltimate,
            "operating-day-started:77",
            CharacterUltimateDomain.Management,
            77);
        Require(!rejectedOutcomes.Transaction.TryCommitAutomatic(
                    request,
                    out _,
                    out string rejectFailure)
                && rejectFailure.Contains(
                    RejectingActivationObserver.Failure,
                    StringComparison.Ordinal)
                && rejectingObserver.ReservationCount == 1
                && rejectingObserver.PrepareCount == 0
                && Limits(source.Actor).managementOperatingDay == -1
                && Limits(source.Actor).activationSequence == 0L
                && Limits(source.Actor).automaticSkillActivations.Count == 0
                && source.Actor.Stats.CaptureMoodDeliveryTransactionState()
                    .Factors.Count == factorsBefore
                && rejectedOutcomes.Publications.PendingForSource(
                    "character:g13-reject-source") == null
                && Count(rejectedOutcomes.Ledger) == 0,
            "Rejected automatic ultimate leaked an effect or use limit.");

        using var faultedOutcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-faulted-ultimate",
            new FaultingEffectExecutor(failAt: 1),
            EditorFixedGameCalendar.Instance);
        Require(!faultedOutcomes.Transaction.TryCommitAutomatic(
                    request,
                    out _,
                    out string effectFailure)
                && effectFailure.Contains(
                    "a05-injected-middle-effect-failure",
                    StringComparison.Ordinal)
                && Limits(source.Actor).managementOperatingDay == -1
                && Limits(source.Actor).automaticSkillActivations.Count == 0
                && faultedOutcomes.Publications.PendingForSource(
                    "character:g13-reject-source") == null
                && Count(faultedOutcomes.Ledger) == 0,
            "Effect failure consumed the automatic ultimate limit.");
        evidence.Add(
            "automaticFailure=effect+fact-reservation-rejection rollback;"
            + "ultimateLimitUnconsumed=true;durableOutcome=0");
    }

    private static void VerifyRetainedUncertaintyRestoresAndReplays(
        ICollection<string> evidence)
    {
        const string sourceId = "character:g13-uncertain-source";
        const string eventId = "operating-day-started:g13:uncertain:88";
        using var source = new ActorFixture(
            "uncertain-source",
            sourceId);
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-uncertain-recovery",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateAutomaticSkill(
            "skill:g13:uncertain-management-ultimate",
            CharacterSkillKind.Ultimate,
            CharacterSkillTrigger.OperatingDayStarted,
            CharacterUltimateDomain.Management,
            "mood",
            "small");
        source.Actor.Progression.GrowthState.ultimate = skill.Clone();
        var uncertainPublisher = new UncertainActivationFactPublisher(
            outcomes.Dispatcher);
        var uncertainPublications =
            new CharacterSkillActivationPublicationRuntime(
                uncertainPublisher,
                uncertainPublisher);
        var uncertainTransaction = new CharacterSkillActivationTransaction(
            new CharacterSkillActivationEffectExecutor(),
            EditorFixedGameCalendar.Instance,
            uncertainPublisher,
            uncertainPublications);
        var request = new CharacterSkillAutomaticActivationRequest(
            new CharacterSkillExecutionContext(
                source.Actor,
                CharacterSkillTrigger.OperatingDayStarted,
                eventId),
            skill,
            CharacterSkillActivationKind.AutomaticUltimate,
            eventId,
            CharacterUltimateDomain.Management,
            88);
        int factorsBefore = source.Actor.Stats
            .CaptureMoodDeliveryTransactionState().Factors.Count;

        Require(uncertainTransaction.TryCommitAutomatic(
                    request,
                    out string publicationKey,
                    out string uncertainFailure)
                && !string.IsNullOrWhiteSpace(publicationKey)
                && uncertainFailure.Contains(
                    UncertainActivationFactPublisher.Failure,
                    StringComparison.Ordinal)
                && uncertainPublisher.PublishCount == 1
                && uncertainPublisher.RecoveryCount == 1
                && outcomes.Dispatcher.ActiveLeaseCount == 0
                && source.Actor.Stats.CaptureMoodDeliveryTransactionState()
                    .Factors.Count == factorsBefore + 1
                && Limits(source.Actor).activationSequence == 1L
                && Limits(source.Actor).automaticSkillActivations.Count == 1
                && Limits(source.Actor).lastSkillActivation.observationPending
                && Count(outcomes.Ledger) == 0,
            "Uncertain skill publication did not retain exactly one applied effect and pending owner.");
        CharacterSkillActivationPublicationData pendingPublication =
            uncertainPublications.PendingForSource(sourceId);
        Require(pendingPublication != null
                && pendingPublication.pending
                && !pendingPublication.ownerFinalized
                && pendingPublication.publicationKey == publicationKey,
            "Uncertain skill publication was not retained as the exact pending journal row.");

        CharacterMoodDeliveryTransactionSnapshot retainedStats =
            source.Actor.Stats.CaptureMoodDeliveryTransactionState();
        string journalJson = JsonUtility.ToJson(
            uncertainPublications.Capture());
        string ownerJson = JsonUtility.ToJson(Limits(source.Actor));
        CharacterSkillActivationPublicationSaveData restoredJournal =
            JsonUtility.FromJson<CharacterSkillActivationPublicationSaveData>(
                journalJson);
        CharacterSkillUseLimitState restoredLimits =
            JsonUtility.FromJson<CharacterSkillUseLimitState>(ownerJson);

        source.Dispose();
        using var restoredSource = new ActorFixture(
            "uncertain-restored-source",
            sourceId);
        restoredSource.Actor.Progression.GrowthState.ultimate = skill.Clone();
        restoredSource.Actor.Progression.GrowthState.useLimits = restoredLimits;
        restoredSource.Actor.Stats.RestoreMoodDeliveryTransactionState(
            retainedStats,
            publish: false);
        var restoredPublications =
            new CharacterSkillActivationPublicationRuntime(
                outcomes.Dispatcher,
                outcomes.Dispatcher);
        restoredPublications.Restore(restoredJournal);
        var restoredTransaction = new CharacterSkillActivationTransaction(
            new CharacterSkillActivationEffectExecutor(),
            EditorFixedGameCalendar.Instance,
            outcomes.Dispatcher,
            restoredPublications);
        int restoredFactorCount = restoredSource.Actor.Stats
            .CaptureMoodDeliveryTransactionState().Factors.Count;
        Require(JsonUtility.ToJson(restoredPublications.Capture())
                    == journalJson
                && JsonUtility.ToJson(Limits(restoredSource.Actor))
                    == ownerJson,
            "Skill publication journal or owner use limits changed during JSON restore.");

        bool recovered = restoredTransaction.TryRecoverStandalone(
            restoredSource.Actor,
            out string recoveryFailure);
        Require(recovered
                && string.IsNullOrEmpty(recoveryFailure)
                && Count(outcomes.Ledger) == 1
                && restoredSource.Actor.Stats
                    .CaptureMoodDeliveryTransactionState().Factors.Count
                    == restoredFactorCount
                && !Limits(restoredSource.Actor).lastSkillActivation
                    .observationPending,
            "Restored skill publication did not replay through the real observer exactly once: "
            + recoveryFailure);
        GameplayOutcomeSnapshot recoveredSnapshot = Single(outcomes.Ledger);
        string recoveredPublicationKey = new GameplayResultKey(
            recoveredSnapshot.producerId,
            new GameplayOperationId(recoveredSnapshot.operationId),
            recoveredSnapshot.commitRevision,
            recoveredSnapshot.localResultIndex).ToString();
        CharacterSkillActivationPublicationData restoredRow =
            restoredPublications.Find(sourceId, 1L);
        Require(restoredRow != null
                && !restoredRow.pending
                && restoredRow.ownerFinalized
                && recoveredPublicationKey == publicationKey
                && restoredRow.publicationKey == publicationKey,
            "Recovered skill journal did not finalize its exact publication identity.");

        var replayRequest = new CharacterSkillAutomaticActivationRequest(
            new CharacterSkillExecutionContext(
                restoredSource.Actor,
                CharacterSkillTrigger.OperatingDayStarted,
                eventId),
            skill,
            CharacterSkillActivationKind.AutomaticUltimate,
            eventId,
            CharacterUltimateDomain.Management,
            88);
        Require(restoredTransaction.TryCommitAutomatic(
                    replayRequest,
                    out string replayKey,
                    out string replayFailure)
                && string.IsNullOrEmpty(replayFailure)
                && replayKey == publicationKey
                && Count(outcomes.Ledger) == 1
                && restoredSource.Actor.Stats
                    .CaptureMoodDeliveryTransactionState().Factors.Count
                    == restoredFactorCount
                && Limits(restoredSource.Actor)
                    .automaticSkillActivations.Count == 1,
            "Recovered automatic event replay repeated its effect or outcome: "
            + replayFailure);
        evidence.Add(
            "uncertainStandalone=retained-success+pending-owner;"
            + "journal+ownerJsonRestore=real-observer-replay;"
            + "effect=once;outcome=once;sameEvent=deduplicated");
    }

    private static void VerifyManualCombatUltimateSharedContract(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "manual-combat-ultimate-source",
            "character:g13-offense-source");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-offense-ultimate",
            calendar: EditorFixedGameCalendar.Instance,
            includeBattleCommand: true);
        CharacterSkillInstance skill = CreateAutomaticSkill(
            "skill:g13:offense-ultimate",
            CharacterSkillKind.Ultimate,
            CharacterSkillTrigger.ManualCombat,
            CharacterUltimateDomain.Offense,
            "damage",
            "standard");
        skill.target = CharacterSkillTarget.Enemy;
        skill.targetingMode = CharacterSkillTargetingMode.PlayerSelected;
        source.Actor.Progression.GrowthState.ultimate = skill.Clone();
        CharacterCombatAbilityDefinition ability =
            CharacterSkillRuntimeEffects.ToCombatAbility(
                skill,
                source.Actor.Progression.SkillSettings);
        Require(ability != null, "Offense ultimate ability conversion failed.");
        OffenseBattleCombatant ally = new(
            CharacterPersistentIdentity.Require(source.Actor).Value,
            source.Actor.Identity.DisplayName,
            source.Actor.Identity.SpeciesTag,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 12f, 8f, 6f, 20f, 8f),
            100f,
            new[] { ability });
        OffenseBattleCombatant enemy = new(
            "character:g13-offense-target",
            "G13 Offense Target",
            "Training",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(300f, 6f, 6f, 6f, 2f, 2f),
            300f);
        OffenseBattleSession session = CreateBattleSession(
            "g13-offense-ultimate-session",
            ally,
            enemy);
        int battleSerial = CharacterGrowthRules.StableNonNegativeHash(
            session.BattleId);
        var commandData = new OffenseBattleCommandFactData
        {
            battleId = session.BattleId,
            expeditionId = session.ExpeditionId,
            actorId = ally.PersistentId,
            actorDisplayName = ally.DisplayName ?? string.Empty,
            targetId = enemy.PersistentId,
            targetDisplayName = enemy.DisplayName ?? string.Empty,
            abilityId = ability.Id,
            commandId = 1L,
            absoluteDay = Math.Max(1, EditorFixedGameCalendar.Instance.Day),
            absoluteHour = Math.Max(
                0L,
                EditorFixedGameCalendar.Instance.AbsoluteHour),
            actionType = OffenseBattleActionType.Ability,
            targetWasDead = enemy.IsDead,
            targetIsDead = enemy.IsDead,
            targetHealthBefore = enemy.CurrentHealth,
            targetHealthAfter = enemy.CurrentHealth
        };
        Require(outcomes.CommandPublications.TryBegin(
                new OffenseBattleCommandFact(commandData),
                out string commandBeginFailure),
            "Offense command reservation failed: " + commandBeginFailure);
        string child = outcomes.CommandPublications.ReservedPublicationKey;
        string eventId = "battle:" + session.BattleId
            + ":command:1:offense-ultimate";
        var request = new CharacterSkillAppliedActivationRequest(
            new CharacterSkillExecutionContext(
                source.Actor,
                CharacterSkillTrigger.ManualCombat,
                eventId,
                session,
                ally,
                enemy),
            skill,
            eventId,
            battleSerial,
            child);
        float healthBefore = enemy.CurrentHealth;
        try
        {
            Require(outcomes.Transaction.TryBeginAppliedAutomatic(
                    request,
                    out CharacterSkillAppliedActivationScope scope,
                    out string beginFailure),
                "Offense ultimate scope failed: " + beginFailure);
            Require(session.TryExecuteCommand(
                    new OffenseBattleCommand(
                        1,
                        ally.PersistentId,
                        OffenseBattleActionType.Ability,
                        enemy.PersistentId,
                        ability.Id),
                    out OffenseBattleCommandResult commandResult)
                && commandResult.Accepted,
                "Offense ultimate command did not execute.");
            Require(outcomes.Transaction.TryPrepareAppliedAutomatic(
                    scope,
                    out PreparedCharacterSkillAppliedActivation prepared,
                    out string prepareFailure),
                "Offense ultimate shared outcome failed: " + prepareFailure);
            commandData.resolved = true;
            commandData.targetHealthAfter = enemy.CurrentHealth;
            commandData.targetIsDead = enemy.IsDead;
            commandData.amount = commandResult.Amount;
            commandData.hit = commandResult.Hit;
            commandData.shieldBlocked = commandResult.ShieldBlocked;
            commandData.coverBlocked = commandResult.CoverBlocked;
            DomainFactDelivery commandDelivery =
                outcomes.CommandPublications.PublishApplied(
                    new OffenseBattleCommandFact(commandData),
                    prepared.Fact,
                    prepared.Lease);
            OffenseBattleCommandObservationData row = outcomes.CommandPublications
                .PendingForBattle(session.BattleId);
            Require(commandDelivery.Completed && row != null,
                "Offense command and skill batch was not durably delivered.");
            DomainFactDelivery skillDelivery =
                OffenseBattleCommandPublicationRuntime.SkillDelivery(row);
            Require(outcomes.Transaction.TryFinalizeAppliedAutomatic(
                    source.Actor,
                    prepared.Fact,
                    skillDelivery,
                    out string finalizeFailure),
                "Offense ultimate finalization failed: " + finalizeFailure);
        }
        finally
        {
            outcomes.CommandPublications.End();
        }
        GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
        Require(enemy.CurrentHealth < healthBefore
                && Limits(source.Actor).offenseBattleSerial == battleSerial
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.ActivationKind)
                    == CharacterSkillActivationKind.ManualCombatUltimate
                        .ToString()
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.ChildResultKeys)
                    == child,
            "Offense ultimate effect, child or limit was not preserved.");
        evidence.Add(
            "offenseUltimate=manual-combat-shared-contract;"
            + "actualBattleDelta+childKey+useLimit=preserved");
    }

    private static void VerifyCalculationOnlyPassiveDoesNotClaimEffect(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "calculation-only-source",
            "character:g13-calculation-only-source");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:g13-calculation-only",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateAutomaticSkill(
            "skill:g13:work-speed-passive",
            CharacterSkillKind.Passive,
            CharacterSkillTrigger.WorkCompleted,
            CharacterUltimateDomain.None,
            "work_speed",
            "small");
        source.Actor.Progression.GrowthState.passiveSkills.Add(skill.Clone());
        var request = new CharacterSkillAutomaticActivationRequest(
            new CharacterSkillExecutionContext(
                source.Actor,
                CharacterSkillTrigger.WorkCompleted,
                "work-completed:g13:modifier-only"),
            skill,
            CharacterSkillActivationKind.AutomaticPassive,
            "work-completed:g13:modifier-only");
        Require(!outcomes.Transaction.TryCommitAutomatic(
                    request,
                    out _,
                    out string failure)
                && failure == "skill-automatic-activation-no-effect"
                && Count(outcomes.Ledger) == 0
                && Limits(source.Actor).automaticSkillActivations.Count == 0,
            "Calculation-only passive claimed a standalone applied effect.");
        string provenance = CharacterSkillRuntimeEffects
            .CaptureWorkModifierProvenance(
                source.Actor,
                BuiltInWorkTypeIds.Harvest);
        string mechanicsSha256 = CharacterSkillActivationTransaction
            .ComputeMechanicsSha256(skill);
        Require(
            provenance.Contains(
                "passive|" + skill.id + "|work_speed|",
                StringComparison.Ordinal)
            && provenance.EndsWith(
                "|" + mechanicsSha256,
                StringComparison.Ordinal),
            "Calculation-only passive provenance was not attached to the "
            + "terminal work contract.");
        evidence.Add(
            "calculationOnlyPassive=standaloneOutcome0;"
            + "terminalWorkProvenance=skill+module+mechanicsHash");
    }

    public static void VerifyOwnerTargetingFocused() =>
        VerifyOwnerCommandTargetingLifetime(new List<string>());

    internal static IEnumerator VerifyOwnerTargetingPlay(
        DungeonRuntimeLifetimeScope scope, ICollection<string> evidence)
    {
        Require(Application.isPlaying, "C1 pointer/UI verification requires actual Play mode.");
        var container = scope.Container;
        var workers = container.Resolve<IStaffWorkPriorityPanelModelBuilder>().BuildRows();
        Require(workers.Count >= 2, "C1 requires two actual prepared workers.");
        CharacterActor source = workers[0].Character;
        CharacterActor recipient = workers[1].Character;
        Require(source.CurrentLifecycleState == CharacterLifecycleState.Active
            && recipient.CurrentLifecycleState == CharacterLifecycleState.Active,
            "C1 prepared workers have not finished entry.");
        Collider2D collider = recipient.GetComponentInChildren<Collider2D>();
        Require(collider != null && collider.enabled && collider.gameObject.activeInHierarchy,
            "C1 actual recipient collider missing.");
        EventSystem events = EventSystem.current;
        Require(events != null && events.isActiveAndEnabled, "C1 actual EventSystem missing.");
        int oldLayer = collider.gameObject.layer;
        const int targetLayer = 30;
        Require(!UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
            .Any(value => value.gameObject.layer == targetLayer), "C1 controlled raycast layer is occupied.");
        var root = new GameObject("C1 Actual Pointer UI Verification");
        root.SetActive(false);
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:c1-actual-pointer-ui", calendar: EditorFixedGameCalendar.Instance);
        try
        {
            CharacterSkillInstance skill = CreateSkill("skill:c1:actual-pointer-ui");
            skill.targetingMode = CharacterSkillTargetingMode.PlayerSelected;
            skill.effectArea = CharacterSkillEffectArea.Single;
            source.Progression.GrowthState.activeSkills.Add(skill.Clone());
            var input = new TargetingInput();
            var cameraObject = new GameObject("C1 Pointer Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.transform.position = new Vector3(collider.bounds.center.x, collider.bounds.center.y, -10);
            var raycaster = new PhysicsWorldPointerRaycaster(input);
            var blocker = new EventSystemUiPointerBlocker(input);
            var controllerObject = new GameObject("C1 Owner Command Controller");
            controllerObject.transform.SetParent(root.transform, false);
            OwnerCommandController controller = controllerObject.AddComponent<OwnerCommandController>();
            controller.ConstructOwnerCommandController(
                container.Resolve<IStaffDiscontentRuntimeService>(), new TargetingCamera(camera), input,
                raycaster, blocker, container.Resolve<ICharacterCombatCommandRuntime>(),
                container.Resolve<ICharacterAiWorldRegistry>(), container.Resolve<IDefenseEngagementRuntime>(),
                container.Resolve<IGameEventBus>());
            controller.ConstructManualSkillCommands(EditorFixedGameCalendar.Instance,
                new RoomLayoutCache(), outcomes.Transaction);
            typeof(OwnerCommandController).GetField("commandTargetMask", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, (LayerMask)(1 << targetLayer));
            collider.gameObject.layer = targetLayer;
            var canvasObject = new GameObject("C1 Staff Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var panelObject = new GameObject("C1 Staff Management", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            StaffWorkPriorityPanel panel = panelObject.AddComponent<StaffWorkPriorityPanel>();
            container.Inject(panel);
            panel.ConstructStaffManagementDependencies(container.Resolve<CharacterSceneRuntimeReferences>(),
                container.Resolve<ICharacterWorldQuery>(), container.Resolve<IBuildingWorldQuery>(),
                controller, container.Resolve<ICharacterMoodImpulseQuery>());
            root.SetActive(true);
            panel.Refresh();
            int frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return null;
            Require(controller.didAwake && controller.isActiveAndEnabled,
                "C1 controller did not enter Unity's Play lifecycle.");
            Button FindButton(string name) => panel.GetComponentsInChildren<Button>()
                .Single(value => value.name == name);
            void Click(Button button)
            {
                Require(button.isActiveAndEnabled && button.interactable, "C1 production button is unavailable: " + button.name);
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(events)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            }
            Click(FindButton("P1Action_StaffModeManagement"));
            int commandIndex = controller.GetManualSkillCommands(source).ToList()
                .FindIndex(value => value.SkillId == skill.id);
            Require(commandIndex >= 0, "C1 actual UI command was not projected.");
            void Begin()
            {
                Click(FindButton("P1Action_ManualSkill_" + commandIndex));
                Require(controller.SelectedActor == source && controller.HasPendingTargeting,
                    "C1 production staff button did not arm the selected-source command.");
            }
            string sourceBefore = ActorFingerprint(source);
            string recipientBefore = ActorFingerprint(recipient);
            Begin();
            controllerObject.SetActive(false);
            Require(!controller.HasPendingTargeting && controller.SelectedActor == null
                && controller.CombatInputMode == CombatCommandType.None,
                "C1 automatic Play OnDisable retained selection or targeting.");
            controllerObject.SetActive(true);
            Begin();
            input.Escape = true;
            frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return null;
            input.Escape = false;
            Require(!controller.HasPendingTargeting, "C1 automatic Update did not cancel on Escape.");
            Begin();
            Require(controller.TrySelectActor(recipient, out _) && !controller.HasPendingTargeting,
                "C1 actual selection replacement retained the old command.");
            Begin();
            Canvas.ForceUpdateCanvases();
            input.Position = RectTransformUtility.WorldToScreenPoint(null,
                FindButton("P1Action_StaffModeManagement").transform.position);
            Require(blocker.IsPointerOverUi(), "C1 production GraphicRaycaster did not block the staff UI.");
            input.RightDown = true;
            frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return null;
            Require(!input.RightDown && controller.HasPendingTargeting
                && Count(outcomes.Ledger) == 0
                && sourceBefore == ActorFingerprint(source) && recipientBefore == ActorFingerprint(recipient),
                "C1 UI-blocked/cancelled commands mutated skill state.");
            evidence.Add("PASS actual StaffWorkPriorityPanel/Button pointer event -> controller; automatic Play disable/enable, Escape and selection cancel; GraphicRaycaster blocks world command; effects/outcomes unchanged");
            canvasObject.SetActive(false);
            Vector2? freePointer = null;
            foreach (float x in new[] { .2f, .5f, .8f })
            foreach (float y in new[] { .2f, .5f, .8f })
            {
                input.Position = new Vector3(Screen.width * x, Screen.height * y);
                if (!blocker.IsPointerOverUi()) freePointer ??= input.Position;
            }
            Require(freePointer.HasValue, "C1 found no unobstructed screen point for the controlled camera.");
            camera.pixelRect = new Rect(freePointer.Value.x - 32, freePointer.Value.y - 32, 64, 64);
            input.Position = new Vector3(-100000, -100000);
            Physics2D.SyncTransforms();
            Require(!blocker.IsPointerOverUi() && !raycaster.TryRaycast(camera, 1 << targetLayer, out _),
                "C1 invalid pointer unexpectedly hit UI or a target.");
            input.RightDown = true;
            frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return null;
            Require(!input.RightDown && controller.HasPendingTargeting
                && sourceBefore == ActorFingerprint(source) && recipientBefore == ActorFingerprint(recipient),
                "C1 invalid physics target consumed the command or changed authoritative state.");
            input.Position = camera.WorldToScreenPoint(collider.bounds.center);
            Require(!blocker.IsPointerOverUi()
                && raycaster.TryRaycast(camera, 1 << targetLayer, out RaycastHit2D hit)
                && hit.collider.GetComponentInParent<CharacterActor>() == recipient,
                "C1 production camera/physics raycaster did not resolve the actual recipient.");
            input.RightDown = true;
            frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return null;
            Require(!input.RightDown && !controller.HasPendingTargeting,
                "C1 actual controller Update did not complete the valid physics target.");
            GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
            Require(Fact(snapshot, CharacterSkillActivationOutcomeIds.RecipientCount) == "1"
                && Limits(source).manualSkillCooldowns.Single(value => value.skillId == skill.id).useCount == 1
                && Limits(recipient).manualSkillBuffs.Count(value => value.skillId == skill.id) == 1,
                "C1 actual pointer command did not commit one cooldown, buff and outcome.");
            string committedSource = ActorFingerprint(source);
            string committedRecipient = ActorFingerprint(recipient);
            string committedLedger = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
            // With no pending intent, a repeated click may route a normal work command.
            // It must not reuse the completed skill intent or produce another skill fact.
            input.RightDown = true;
            frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return null;
            Require(!controller.TryCancelTargeting(out _)
                && committedSource == ActorFingerprint(source) && committedRecipient == ActorFingerprint(recipient)
                && committedLedger == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()),
                "C1 repeated pointer/cancel replayed the completed skill.");
            evidence.Add("PASS production camera + actual recipient collider + PhysicsWorldPointerRaycaster + automatic controller Update; invalid target retains retry; valid retry commits one cooldown/buff/outcome; repeated pointer/cancel inert for skill state");
        }
        finally
        {
            collider.gameObject.layer = oldLayer;
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
        }
    }

    private static void VerifyOwnerCommandTargetingLifetime(ICollection<string> evidence)
    {
        using var source = new ActorFixture("targeting-source", "character:c1-targeting-source");
        using var recipient = new ActorFixture("targeting-recipient", "character:c1-targeting-recipient");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:c1-owner-targeting", calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateSkill("skill:c1:targeting");
        skill.targetingMode = CharacterSkillTargetingMode.PlayerSelected;
        skill.effectArea = CharacterSkillEffectArea.Single;
        source.Actor.Progression.GrowthState.activeSkills.Add(skill.Clone());
        var world = new TestWorldRegistry();
        world.RegisterCharacter(source.Actor);
        world.RegisterCharacter(recipient.Actor);
        GameObject root = new("C1 Owner Targeting Lifetime");
        try
        {
            var input = new TargetingInput();
            OwnerCommandController controller = root.AddComponent<OwnerCommandController>();
            controller.ConstructOwnerCommandController(
                NullProxy<IStaffDiscontentRuntimeService>.Create(),
                NullProxy<IMainCameraProvider>.Create(), input,
                NullProxy<IWorldPointerRaycaster>.Create(),
                NullProxy<IUiPointerBlocker>.Create(),
                NullProxy<ICharacterCombatCommandRuntime>.Create(), world,
                NullProxy<IDefenseEngagementRuntime>.Create(), new GameEventBus());
            controller.ConstructManualSkillCommands(EditorFixedGameCalendar.Instance,
                new RoomLayoutCache(), outcomes.Transaction);
            MethodInfo complete = typeof(OwnerCommandController).GetMethod(
                "TryCompleteManualSkillTargeting", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo update = typeof(OwnerCommandController).GetMethod(
                "Update", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo disable = typeof(OwnerCommandController).GetMethod(
                "OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo enable = typeof(OwnerCommandController).GetMethod(
                "OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(complete != null && update != null && disable != null && enable != null,
                "Targeting producer boundary missing.");
            // Controlled target completion uses the production controller path and
            // real transaction. It does not claim a camera/raycast or live UI test.
            bool Complete(CharacterActor target) => (bool)complete.Invoke(controller,
                new object[] { target, null, null });
            void Begin()
            {
                Require(controller.TryIssueManualSkillCommand(source.Actor, skill.id, out string message),
                    "PlayerSelected entry rejected: " + message);
                Require(controller.HasPendingTargeting && controller.CombatInputMode == CombatCommandType.None,
                    "Manual entry did not replace the combat intent.");
            }

            Require(controller.TrySelectActor(source.Actor, out _), "Targeting source selection failed.");
            controller.SetCombatInputMode(CombatCommandType.Attack);
            Begin();
            Require(controller.TrySelectActor(source.Actor, out _) && controller.HasPendingTargeting,
                "Reselecting the same actor cancelled an unchanged intent.");
            string sourceBefore = ActorFingerprint(source.Actor);
            string recipientBefore = ActorFingerprint(recipient.Actor);
            string ledgerBefore = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
            Require(!Complete(null) && controller.HasPendingTargeting,
                "An invalid target consumed the retryable intent.");
            Require(sourceBefore == ActorFingerprint(source.Actor)
                    && recipientBefore == ActorFingerprint(recipient.Actor)
                    && ledgerBefore == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()),
                "Rejected target partially changed cooldown, effects or outcomes.");

            controller.SetCombatInputMode(CombatCommandType.Move);
            Require(controller.CombatInputMode == CombatCommandType.Move && !Complete(recipient.Actor)
                    && controller.CombatInputMode == CombatCommandType.Move,
                "Manual targeting survived combat replacement or stale completion erased the combat intent.");
            controller.SetCombatInputMode(CombatCommandType.None);
            Require(!controller.HasPendingTargeting, "None mode failed to cancel targeting.");
            Begin();
            Require(controller.TryCancelTargeting(out _) && !controller.TryCancelTargeting(out _)
                    && !Complete(recipient.Actor), "Explicit cancellation failed or applied a stale skill.");
            Begin();
            Require(!controller.TryIssueManualSkillCommand(source.Actor, "skill:c1:unavailable", out _)
                    && !controller.HasPendingTargeting && !Complete(recipient.Actor),
                "A rejected immediate skill left the previous manual intent armed.");
            Begin();
            input.Escape = true;
            update.Invoke(controller, null);
            input.Escape = false;
            Require(!controller.HasPendingTargeting && !Complete(recipient.Actor),
                "Escape did not stop the pending controller command.");
            Begin();
            Require(controller.TrySelectActor(recipient.Actor, out _) && !controller.HasPendingTargeting,
                "Selection change retained the previous source's target intent.");
            Require(sourceBefore == ActorFingerprint(source.Actor)
                    && recipientBefore == ActorFingerprint(recipient.Actor)
                    && ledgerBefore == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()),
                "Replacement/cancel/selection changed authoritative skill state.");

            Begin();
            Require(!Complete(null) && Complete(recipient.Actor) && !controller.HasPendingTargeting,
                "Valid retry failed or successful targeting was not consumed.");
            GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
            Require(Fact(snapshot, CharacterSkillActivationOutcomeIds.RecipientCount) == "1"
                    && Limits(source.Actor).manualSkillCooldowns.Single().useCount == 1
                    && Limits(recipient.Actor).manualSkillBuffs.Count == 1,
                "Targeted command did not commit exactly one cooldown, buff and canonical outcome.");
            string committed = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
            Require(!Complete(recipient.Actor)
                    && committed == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes())
                    && Limits(source.Actor).manualSkillCooldowns.Single().useCount == 1,
                "Completed intent could be applied twice.");

            Begin();
            evidence.Add("C1-lifecycle: playing=" + Application.isPlaying
                + ";didAwake=" + typeof(MonoBehaviour).GetProperty("didAwake")?.GetValue(controller));
            root.SetActive(false);
            // A plain MonoBehaviour in Edit fixtures has not entered its Play
            // lifecycle. Exercise the message body explicitly in that mode;
            // Play verification must retain the automatic callback assertion.
            if (!Application.isPlaying)
            {
                disable.Invoke(controller, null);
                evidence.Add("C1-disable=explicit-Edit-message;Unity-Play-callback=not-covered");
            }
            Require(!controller.HasPendingTargeting && controller.SelectedActor == null
                    && controller.CombatInputMode == CombatCommandType.None,
                "Disable retained selection or targeting state.");
            root.SetActive(true);
            if (!Application.isPlaying) enable.Invoke(controller, null);
            Begin();
            UnityEngine.Object.DestroyImmediate(source.Actor.GetComponent<AbilityWork>());
            source.Actor.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
            Require(!controller.HasPendingTargeting && !Complete(recipient.Actor)
                    && committed == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()),
                "A source without command capability retained a stale activation intent.");
            evidence.Add("C1=PlayerSelected-controller+real-transaction;invalid-target=unchanged+retry;"
                + "manual/combat=exclusive;cancel=explicit+Escape+selection+disable+source;"
                + "same-selection=retained;success=one-cooldown+buff+outcome;raycast/liveUI=not-covered");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyOwnerCommandProductionEntry(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "owner-entry-source",
            "character:a05-owner-entry-source");
        using var recipient = new ActorFixture(
            "owner-entry-recipient",
            "character:a05-owner-entry-recipient");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:a05-owner-command-entry",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateSkill(
            "skill:a05:owner-command-entry");
        source.Actor.Progression.GrowthState.activeSkills.Add(skill.Clone());
        var world = new TestWorldRegistry();
        world.RegisterCharacter(source.Actor);
        world.RegisterCharacter(recipient.Actor);
        GameObject root = new("A05 Owner Command Production Entry");
        try
        {
            OwnerCommandController controller =
                root.AddComponent<OwnerCommandController>();
            controller.ConstructOwnerCommandController(
                NullProxy<IStaffDiscontentRuntimeService>.Create(),
                NullProxy<IMainCameraProvider>.Create(),
                NullProxy<IPlayerInputReader>.Create(),
                NullProxy<IWorldPointerRaycaster>.Create(),
                NullProxy<IUiPointerBlocker>.Create(),
                NullProxy<ICharacterCombatCommandRuntime>.Create(),
                world,
                NullProxy<IDefenseEngagementRuntime>.Create(),
                new GameEventBus());
            controller.ConstructManualSkillCommands(
                EditorFixedGameCalendar.Instance,
                new RoomLayoutCache(),
                outcomes.Transaction);

            var statsBefore = recipient.Actor.Stats.StatSnapshot
                .OrderBy(value => (int)value.Key).ToArray();
            float healthBefore = recipient.Actor.Stats.CurrentHealth;
            int moodFactorsBefore = recipient.Actor.Stats
                .CaptureMoodDeliveryTransactionState().Factors.Count;
            Require(controller.TryIssueManualSkillCommand(
                    source.Actor,
                    skill.id,
                    out string message),
                "Owner-command production entry failed: " + message);
            GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
            CharacterSkillActivationPublicationData publication =
                outcomes.Publications.Find(
                    "character:a05-owner-entry-source",
                    1L);
            Require(Fact(
                        snapshot,
                        CharacterSkillActivationOutcomeIds.ActivationKind)
                    == CharacterSkillActivationKind.ManualDungeon.ToString()
                    && Fact(
                        snapshot,
                        CharacterSkillActivationOutcomeIds.RecipientCount)
                    == "1"
                    && publication != null
                    && !publication.pending
                    && publication.ownerFinalized
                    && publication.publicationKey
                        == Limits(source.Actor).lastSkillActivation.resultKey
                    && Limits(recipient.Actor).manualSkillBuffs.Count == 1
                    && Limits(source.Actor).manualSkillCooldowns.Single()
                        .useCount == 1,
                "Owner-command entry bypassed the shared dungeon transaction.");
            // This actual work-speed command installs a saved buff, not an
            // immediate heal/stat change. Its use is still a canonical action.
            Require(statsBefore.SequenceEqual(recipient.Actor.Stats.StatSnapshot
                        .OrderBy(value => (int)value.Key))
                    && recipient.Actor.Stats.CurrentHealth.Equals(healthBefore)
                    && recipient.Actor.Stats.CaptureMoodDeliveryTransactionState()
                        .Factors.Count == moodFactorsBefore,
                "The calculation-only manual command unexpectedly changed immediate stats.");
            string sourceAfter = ActorFingerprint(source.Actor);
            string recipientAfter = ActorFingerprint(recipient.Actor);
            string outcomesAfter = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
            Require(controller.GetManualSkillCommands(source.Actor)
                        .Single(value => value.SkillId == skill.id).RemainingCooldownHours
                    == 2L * GameCalendarRules.HoursPerDay
                    && !controller.TryIssueManualSkillCommand(source.Actor, skill.id,
                        out string cooldownMessage)
                    && cooldownMessage.Contains("재사용까지", StringComparison.Ordinal)
                    && sourceAfter == ActorFingerprint(source.Actor)
                    && recipientAfter == ActorFingerprint(recipient.Actor)
                    && outcomesAfter == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()),
                "A public cooldown query/rejection changed skill owners or added a narrative outcome.");
            evidence.Add(
                "ownerCommandEntry=public-command->ManualDungeon;"
                + "exactRecipients=1;cooldown=committed");
            evidence.Add("manualCalculationOnly=public-command;immediateNumericDelta=0;"
                + "savedBuff=1;activationOutcome=1;publicCooldownRejection=diagnostic-only;"
                + "rejectedCommandOwnerAndLedgerDelta=0");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyExpeditionProductionEntry(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "expedition-entry-source",
            "character:a05-expedition-entry-source");
        using var recipient = new ActorFixture(
            "expedition-entry-recipient",
            "character:a05-expedition-entry-recipient");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:a05-expedition-entry",
            calendar: EditorFixedGameCalendar.Instance);
        var nodeOutcomes = new MigratedProducerOutcomeEditorFixture(
            "run:a05-expedition-entry-node");
        using var nodeFacts = new EditorOffenseNodeFactFixture(
            new GameEventBus(),
            nodeOutcomes.Transaction,
            nodeOutcomes.Recorder,
            nodeOutcomes.Ledger);
        CharacterSkillInstance skill = CreateSkill(
            "skill:a05:expedition-entry");
        source.Actor.Progression.GrowthState.activeSkills.Add(skill.Clone());
        GameObject root = new("A05 Expedition Production Entry");
        try
        {
            OffenseExpeditionRuntime runtime =
                root.AddComponent<OffenseExpeditionRuntime>();
            runtime.ConstructNodePublications(
                EditorFixedGameCalendar.Instance,
                nodeFacts.Publications);
            runtime.ConstructSkillActivationTransactions(
                outcomes.Transaction);
            var target = new OffenseTargetDefinition
            {
                id = "a05-expedition-entry-target",
                title = "A05 원정 진입 검증",
                durationSeconds = 10f,
                campaignOrder = 1
            };
            var expedition = new OffenseExpeditionRun(
                "expedition:a05-production-entry",
                target,
                new[] { source.Actor, recipient.Actor },
                totalPower: 10f);
            runtime.PublishPersistentState(
                new List<OffenseExpeditionRun> { expedition },
                new List<OffenseExpeditionResult>());

            Require(runtime.TryActivateManualSkill(
                    expedition.ExpeditionId,
                    sourceMemberIndex: 0,
                    skill.id,
                    selectedTargetMemberIndex: -1,
                    out string message),
                "Expedition production entry failed: " + message);
            GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
            Require(Fact(
                        snapshot,
                        CharacterSkillActivationOutcomeIds.ActivationKind)
                    == CharacterSkillActivationKind.ManualExpedition.ToString()
                    && Fact(
                        snapshot,
                        CharacterSkillActivationOutcomeIds.RecipientCount)
                    == "1"
                    && Limits(recipient.Actor).manualSkillBuffs.Count == 1
                    && Limits(source.Actor).manualSkillCooldowns.Single()
                        .useCount == 1,
                "Expedition entry bypassed the shared expedition transaction.");
            evidence.Add(
                "expeditionEntry=public-runtime->ManualExpedition;"
                + "exactRecipients=1;cooldown=committed");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyExactMultiTargetSaveAndOwnerRewindRejection(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "exact-source",
            "character:a05-exact-source");
        using var first = new ActorFixture(
            "exact-first",
            "character:a05-exact-first");
        using var second = new ActorFixture(
            "exact-second",
            "character:a05-exact-second");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:a05-exact",
            calendar: EditorFixedGameCalendar.Instance);
        CharacterActor[] recipients = { first.Actor, second.Actor };
        CharacterSkillInstance skill = CreateSkill("skill:a05:exact");
        source.Actor.Progression.GrowthState.activeSkills.Add(skill.Clone());
        CharacterSkillUseLimitState sourceBefore = Limits(source.Actor).Clone();
        CharacterSkillUseLimitState firstBefore = Limits(first.Actor).Clone();
        CharacterSkillUseLimitState secondBefore = Limits(second.Actor).Clone();
        CharacterMoodDeliveryTransactionSnapshot firstStatsBefore =
            first.Actor.Stats.CaptureMoodDeliveryTransactionState();
        CharacterMoodDeliveryTransactionSnapshot secondStatsBefore =
            second.Actor.Stats.CaptureMoodDeliveryTransactionState();

        var request = new CharacterSkillActivationRequest(
            source.Actor,
            skill,
            recipients,
            CharacterSkillActivationKind.ManualDungeon,
            120L);
        Require(outcomes.Transaction.TryCommit(
                request,
                out string publicationKey,
                out string failure),
            "Exact multi-target transaction failed: " + failure);
        GameplayResultKey resultKey = ParsePublicationKey(publicationKey);
        CharacterSkillUseLimitState committed = Limits(source.Actor);
        GameplayOutcomeSnapshot snapshot = Single(outcomes.Ledger);
        CharacterSkillActivationPublicationData publication =
            outcomes.Publications.Find(
                "character:a05-exact-source",
                1L);
        Require(resultKey.IsValid
                && committed.activationSequence == 1L
                && publication != null
                && !publication.pending
                && publication.ownerFinalized
                && publication.publicationKey == publicationKey
                && committed.lastSkillActivation != null
                && committed.lastSkillActivation.resultKey
                    == resultKey.ToString()
                && committed.lastSkillActivation.recipientCharacterIds
                    .SequenceEqual(new[]
                    {
                        "character:a05-exact-first",
                        "character:a05-exact-second"
                    })
                && Limits(first.Actor).manualSkillBuffs.Count == 1
                && Limits(second.Actor).manualSkillBuffs.Count == 1
                && committed.manualSkillCooldowns.Single().useCount == 1
                && committed.manualSkillCooldowns.Single()
                    .readyAbsoluteHour == 168L
                && snapshot.participants.Count == 3
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.RecipientCount) == "2"
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.MechanicsSha256)
                    == committed.lastSkillActivation.mechanicsSha256
                && Fact(snapshot,
                    CharacterSkillActivationOutcomeIds.RecipientStateSha256)
                    == committed.lastSkillActivation.recipientStateSha256,
            "Exact multi-target outcome omitted owner, cooldown, recipients, or state hashes.");

        string serialized = JsonUtility.ToJson(committed);
        CharacterSkillUseLimitState restored =
            JsonUtility.FromJson<CharacterSkillUseLimitState>(serialized);
        restored.ValidatePersistentState(
            "character:a05-exact-source",
            source.Actor.Progression.GrowthState.activeSkills);
        restored.lastSkillActivation.mechanicsSha256 = new string('0', 64);
        RequireThrows(
            () => restored.ValidatePersistentState(
                "character:a05-exact-source",
                source.Actor.Progression.GrowthState.activeSkills),
            "Tampered mechanics hash survived save validation.");

        source.Actor.Progression.GrowthState.useLimits = sourceBefore.Clone();
        first.Actor.Progression.GrowthState.useLimits = firstBefore.Clone();
        second.Actor.Progression.GrowthState.useLimits = secondBefore.Clone();
        first.Actor.Stats.RestoreMoodDeliveryTransactionState(
            firstStatsBefore,
            publish: false);
        second.Actor.Stats.RestoreMoodDeliveryTransactionState(
            secondStatsBefore,
            publish: false);
        string ownerBeforeReplay = ActorFingerprint(source.Actor);
        string firstBeforeReplay = ActorFingerprint(first.Actor);
        string secondBeforeReplay = ActorFingerprint(second.Actor);
        string ledgerBeforeReplay = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
        string publicationsBeforeReplay = JsonUtility.ToJson(outcomes.Publications.Capture());
        Require(!outcomes.Transaction.TryCommit(
                request, out string replayKey, out string replayFailure)
                && string.IsNullOrEmpty(replayKey)
                && replayFailure == "skill-activation-owner-publication-history-mismatch"
                && Count(outcomes.Ledger) == 1
                && ownerBeforeReplay == ActorFingerprint(source.Actor)
                && firstBeforeReplay == ActorFingerprint(first.Actor)
                && secondBeforeReplay == ActorFingerprint(second.Actor)
                && ledgerBeforeReplay == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes())
                && publicationsBeforeReplay == JsonUtility.ToJson(outcomes.Publications.Capture()),
            "An incoherent owner-only rewind was accepted or changed state: " + replayFailure);
        evidence.Add(
            "exact=source+2recipients+beforeAfterState+cooldown;"
            + "saveRoundTrip=pass;tamper=rejected;owner-only-rewind=rejected-before-mutation;"
            + "owner+recipients+ledger+journal=unchanged");
    }

    private static void VerifyAutomaticOwnerRewindRejection(ICollection<string> evidence)
    {
        using var source = new ActorFixture("automatic-owner-rewind", "character:c1-automatic-owner-rewind");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:c1-automatic-owner-rewind", calendar: EditorFixedGameCalendar.Instance);
        CharacterSkillInstance skill = CreateAutomaticSkill("skill:c1:automatic-owner-rewind",
            CharacterSkillKind.Passive, CharacterSkillTrigger.WorkCompleted,
            CharacterUltimateDomain.None, "mood", "small");
        source.Actor.Progression.GrowthState.passiveSkills.Add(skill.Clone());
        var context = new CharacterSkillExecutionContext(source.Actor,
            CharacterSkillTrigger.WorkCompleted, "work-completed:c1:automatic-owner-rewind:1",
            workTypeId: BuiltInWorkTypeIds.Clean);
        var request = new CharacterSkillAutomaticActivationRequest(context, skill,
            CharacterSkillActivationKind.AutomaticPassive, context.EventId);
        CharacterSkillUseLimitState beforeLimits = Limits(source.Actor).Clone();
        CharacterMoodDeliveryTransactionSnapshot beforeStats = source.Actor.Stats.CaptureMoodDeliveryTransactionState();
        Require(outcomes.Transaction.TryCommitAutomatic(request, out string key, out string failure)
                && Count(outcomes.Ledger) == 1,
            "Automatic owner rewind setup failed: " + failure);
        string committedOwner = ActorFingerprint(source.Actor);
        Require(outcomes.Transaction.TryCommitAutomatic(request, out string sameKey, out failure)
                && key == sameKey && committedOwner == ActorFingerprint(source.Actor)
                && Count(outcomes.Ledger) == 1,
            "A valid completed automatic event did not replay without mutation: " + failure);
        source.Actor.Progression.GrowthState.useLimits = beforeLimits.Clone();
        source.Actor.Stats.RestoreMoodDeliveryTransactionState(beforeStats, publish: false);
        string rewoundOwner = ActorFingerprint(source.Actor);
        string ledgerBefore = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
        string journalBefore = JsonUtility.ToJson(outcomes.Publications.Capture());
        Require(!outcomes.Transaction.TryCommitAutomatic(request, out string rejectedKey, out failure)
                && string.IsNullOrEmpty(rejectedKey)
                && failure == "skill-activation-owner-publication-history-mismatch"
                && rewoundOwner == ActorFingerprint(source.Actor)
                && ledgerBefore == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes())
                && journalBefore == JsonUtility.ToJson(outcomes.Publications.Capture()),
            "An automatic owner-only rewind was accepted or changed effects/limits/journal: " + failure);
        evidence.Add("automaticOwnerRewind=known-owner-revision-rejected-before-effects;"
            + "owner+ledger+journal=unchanged;valid-completed-event=replay-without-effects");
    }

    private static void VerifyMiddleRecipientFailureRollsBack(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "middle-source",
            "character:a05-middle-source");
        using var first = new ActorFixture(
            "middle-first",
            "character:a05-middle-first");
        using var second = new ActorFixture(
            "middle-second",
            "character:a05-middle-second");
        var faulting = new FaultingEffectExecutor(failAt: 2);
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:a05-middle-failure",
            faulting,
            EditorFixedGameCalendar.Instance);
        string sourceBefore = OwnerFingerprint(source.Actor);
        string firstBefore = ActorFingerprint(first.Actor);
        string secondBefore = ActorFingerprint(second.Actor);
        var request = new CharacterSkillActivationRequest(
            source.Actor,
            CreateSkill("skill:a05:middle-failure"),
            new[] { first.Actor, second.Actor },
            CharacterSkillActivationKind.ManualDungeon,
            240L);

        Require(!outcomes.Transaction.TryCommit(
                    request,
                    out string publicationKey,
                    out string failure)
                && publicationKey.Length == 0
                && failure.Contains(
                    "a05-injected-middle-effect-failure",
                    StringComparison.Ordinal)
                && OwnerFingerprint(source.Actor) == sourceBefore
                && ActorFingerprint(first.Actor) == firstBefore
                && ActorFingerprint(second.Actor) == secondBefore
                && Count(outcomes.Ledger) == 0,
            "Middle-recipient failure leaked a buff, stat, cooldown, owner, or outcome.");
        evidence.Add(
            "middleFailure=recipient1+recipient2+cooldown+owner-rollback;outcomes=0");
    }

    private static void VerifyOutcomeRejectionRollsBack(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "reject-source",
            "character:a05-reject-source");
        using var recipient = new ActorFixture(
            "reject-recipient",
            "character:a05-reject-recipient");
        var rejectingObserver = new RejectingActivationObserver();
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:a05-commit-reject",
            calendar: EditorFixedGameCalendar.Instance,
            activationObserver: rejectingObserver);
        string sourceBefore = OwnerFingerprint(source.Actor);
        string recipientBefore = ActorFingerprint(recipient.Actor);
        var request = new CharacterSkillActivationRequest(
            source.Actor,
            CreateSkill("skill:a05:commit-reject"),
            new[] { recipient.Actor },
            CharacterSkillActivationKind.ManualExpedition,
            360L);

        Require(!outcomes.Transaction.TryCommit(
                    request,
                    out string publicationKey,
                    out string failure)
                && publicationKey.Length == 0
                && failure.Contains(
                    RejectingActivationObserver.Failure,
                    StringComparison.Ordinal)
                && rejectingObserver.ReservationCount == 1
                && rejectingObserver.PrepareCount == 0
                && OwnerFingerprint(source.Actor) == sourceBefore
                && ActorFingerprint(recipient.Actor) == recipientBefore
                && outcomes.Publications.PendingForSource(
                    "character:a05-reject-source") == null
                && Count(outcomes.Ledger) == 0,
            "Rejected outcome did not restore all gameplay owners.");
        evidence.Add(
            "reserveReject=recipient+cooldown+owner-rollback;durableOutcome=0");
    }

    private static void VerifyInvalidRecipientsDoNotPrepare(
        ICollection<string> evidence)
    {
        using var source = new ActorFixture(
            "invalid-source",
            "character:a05-invalid-source");
        var rejectingObserver = new RejectingActivationObserver();
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:a05-invalid-empty",
            calendar: EditorFixedGameCalendar.Instance,
            activationObserver: rejectingObserver);
        var request = new CharacterSkillActivationRequest(
            source.Actor,
            CreateSkill("skill:a05:invalid-empty"),
            Array.Empty<CharacterActor>(),
            CharacterSkillActivationKind.ManualDungeon,
            480L);
        Require(!outcomes.Transaction.TryCommit(
                    request,
                    out _,
                    out string failure)
                && failure == "skill-activation-recipient-empty"
                && rejectingObserver.ReservationCount == 0
                && rejectingObserver.PrepareCount == 0
                && Limits(source.Actor).activationSequence == 0L
                && outcomes.Publications.PendingForSource(
                    "character:a05-invalid-source") == null
                && Count(outcomes.Ledger) == 0,
            "Target-zero request reached outcome preparation or changed owner state.");
        evidence.Add("target0=rejected-before-prepare;ownerUnchanged=true");
    }

    private static CharacterSkillInstance CreateSkill(string id) => new()
    {
        id = id,
        displayName = "A05 작업 호흡",
        mechanicalDescription = "대상의 작업 효율을 일시적으로 높인다.",
        kind = CharacterSkillKind.Active,
        trigger = CharacterSkillTrigger.ManualWork,
        target = CharacterSkillTarget.Ally,
        targetingMode = CharacterSkillTargetingMode.AllEligible,
        effectArea = CharacterSkillEffectArea.Dungeon,
        areaSize = 1,
        manualDurationHours = GameCalendarRules.HoursPerDay,
        manualCooldownDays = 2,
        modules = new List<CharacterSkillModuleSelection>
        {
            new()
            {
                moduleId = "work_speed",
                variantId = "small"
            }
        }
    };

    private static CharacterSkillInstance CreateAutomaticSkill(
        string id,
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        CharacterUltimateDomain domain,
        string moduleId,
        string variantId) => new()
    {
        id = id,
        displayName = "G13 자동 발동 검증",
        description = "실제 자동 발동 효과와 사용 제한을 함께 검증한다.",
        mechanicalDescription = "자동 발동 시 정의된 효과를 적용한다.",
        kind = kind,
        trigger = trigger,
        target = CharacterSkillTarget.Self,
        targetingMode = CharacterSkillTargetingMode.Self,
        effectArea = CharacterSkillEffectArea.Single,
        areaSize = 1,
        ultimateDomain = domain,
        modules = new List<CharacterSkillModuleSelection>
        {
            new()
            {
                moduleId = moduleId,
                variantId = variantId
            }
        }
    };

    private static OffenseBattleSession CreateBattleSession(
        string battleId,
        params OffenseBattleCombatant[] combatants)
    {
        CombatEquipmentRuntime equipment = CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader())),
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog: EmptyResourceEconomyContentCatalog.Instance,
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: EmptyEquipmentModuleCatalog.Instance,
            itemStackRuntime:
                UnavailableEquipmentPhysicalItemGateway.Instance,
            calendar: EditorFixedGameCalendar.Instance);
        CombatResolutionService resolution = new(
            new UnityCombatRandomSource(
                new RandomStreamProvider(40405)),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure:
                NoOpCharacterEnvironmentExposureCommand.Instance);
        return new OffenseBattleSession(
            battleId,
            "expedition:g13",
            "target:g13",
            "G13 Automatic Skill Battle",
            DungeonDifficulty.Normal,
            combatants,
            resolution,
            equipment);
    }

    private static CharacterSkillUseLimitState Limits(CharacterActor actor) =>
        actor.Progression.GrowthState.useLimits
        ??= new CharacterSkillUseLimitState();

    private static string OwnerFingerprint(CharacterActor actor) =>
        JsonUtility.ToJson(Limits(actor));

    private static string ActorFingerprint(CharacterActor actor)
    {
        StringBuilder value = new(OwnerFingerprint(actor));
        value.Append('|')
            .Append(actor.Stats.MaxHealth.ToString("R",
                CultureInfo.InvariantCulture))
            .Append('|')
            .Append(actor.Stats.CurrentHealth.ToString("R",
                CultureInfo.InvariantCulture))
            .Append('|')
            .Append(actor.Stats.InjurySeverity.ToString("R",
                CultureInfo.InvariantCulture));
        foreach (KeyValuePair<CharacterCondition, float> condition in
                 actor.Stats.StatSnapshot.OrderBy(value => (int)value.Key))
        {
            value.Append('|').Append((int)condition.Key).Append(':')
                .Append(condition.Value.ToString("R",
                    CultureInfo.InvariantCulture));
        }
        return value.ToString();
    }

    private static GameplayOutcomeSnapshot Single(
        GameplayOutcomeLedger ledger) => ledger.CaptureGameplayOutcomes()
        .exactOutcomes.Single(value => value != null
            && value.outcomeTypeId
                == CharacterSkillActivationOutcomeIds.Activated.Value);

    private static int Count(GameplayOutcomeLedger ledger) => ledger
        .CaptureGameplayOutcomes().exactOutcomes.Count(value => value != null
            && value.outcomeTypeId
                == CharacterSkillActivationOutcomeIds.Activated.Value);

    private static string Fact(
        GameplayOutcomeSnapshot snapshot,
        GameplayOutcomeFactId factId) => snapshot.facts.Single(value =>
        string.Equals(value.factId, factId.Value, StringComparison.Ordinal)).value;

    private static GameplayResultKey ParsePublicationKey(string value)
    {
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(
                value,
                out GameplayResultKey key))
        {
            throw new InvalidOperationException(
                "Skill activation returned a non-canonical publication key.");
        }
        return key;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void WriteReport(
        string status,
        IEnumerable<string> evidence,
        string error)
    {
        string relative = Environment.GetEnvironmentVariable(
            ReportEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(relative))
            relative = DefaultReportPath;
        string path = Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(Directory.GetCurrentDirectory(), relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? Directory.GetCurrentDirectory());
        var lines = new List<string>
        {
            "A05_CHARACTER_SKILL_ACTIVATION=" + status,
            "utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        lines.AddRange((evidence ?? Array.Empty<string>())
            .Select(value => "evidence=" + value));
        if (!string.IsNullOrWhiteSpace(error))
            lines.Add("error=" + error.Replace('\r', ' ').Replace('\n', ' '));
        File.WriteAllLines(path, lines);
    }

    private sealed class FaultingEffectExecutor :
        ICharacterSkillActivationEffectExecutor
    {
        private readonly CharacterSkillActivationEffectExecutor inner = new();
        private readonly int failAt;
        private int count;

        internal FaultingEffectExecutor(int failAt) => this.failAt = failAt;

        public void Execute(
            in CharacterSkillExecutionContext context,
            CharacterSkillInstance skill)
        {
            count++;
            if (count == failAt)
            {
                throw new InvalidOperationException(
                    "a05-injected-middle-effect-failure");
            }
            inner.Execute(context, skill);
        }
    }

    private sealed class RejectingActivationObserver :
        IDurableDomainFactObserver<CharacterSkillActivationFact>
    {
        internal const string Failure =
            "a05-injected-skill-fact-reservation-rejection";
        internal int ReservationCount { get; private set; }
        internal int PrepareCount { get; private set; }

        public bool TryReserve(
            in CharacterSkillActivationFact proposed,
            out object reservation,
            out string failure)
        {
            _ = proposed;
            ReservationCount++;
            reservation = null;
            failure = Failure;
            return false;
        }

        public bool TryPrepare(
            in CharacterSkillActivationFact actual,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            _ = actual;
            _ = reservation;
            PrepareCount++;
            prepared = null;
            failure = "a05-unexpected-skill-fact-prepare";
            return false;
        }

        public DomainFactDelivery Replay(
            in CharacterSkillActivationFact fact)
        {
            _ = fact;
            return DomainFactDelivery.Reject(
                "a05-unexpected-skill-fact-replay");
        }

        public void Cancel(object reservation) => _ = reservation;
    }

    private sealed class UncertainActivationFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        internal const string Failure =
            "a05-injected-skill-publication-unavailable";
        private readonly IDomainFactPublisher inner;

        internal UncertainActivationFactPublisher(
            IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal int PublishCount { get; private set; }
        internal int RecoveryCount { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            _ = lease;
            _ = actual;
            PublishCount++;
            return DomainFactDelivery.Uncertain(Failure);
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (facts == null || results == null || facts.Count == 0
                || facts.Count != results.Length)
            {
                throw new ArgumentException(
                    "Invalid uncertain domain fact cohort shape.");
            }
            PublishCount += facts.Count;
            for (int index = 0; index < results.Length; index++)
                results[index] = DomainFactDelivery.Uncertain(Failure);
            failure = Failure;
            return true;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact)
        {
            _ = fact;
            RecoveryCount++;
            return DomainFactDelivery.Reject(Failure);
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (facts == null || results == null || facts.Count == 0
                || facts.Count != results.Length)
            {
                throw new ArgumentException(
                    "Invalid unavailable domain fact recovery shape.");
            }
            RecoveryCount++;
            for (int index = 0; index < results.Length; index++)
                results[index] = DomainFactDelivery.Reject(Failure);
            failure = Failure;
            return false;
        }

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class TargetingInput : IPlayerInputReader
    {
        internal bool Escape;
        internal bool RightDown;
        internal Vector3 Position;
        public Vector3 MousePosition => Position;
        public float ScreenWidth => 1920f;
        public float ScreenHeight => 1080f;
        public float ScrollDeltaY => 0f;
        public bool GetKey(KeyCode keyCode) => false;
        public bool GetKeyDown(KeyCode keyCode) => Escape && keyCode == KeyCode.Escape;
        public bool GetMouseButton(int button) => false;
        public bool GetMouseButtonDown(int button)
        {
            if (button != 1 || !RightDown) return false;
            RightDown = false;
            return true;
        }
    }

    private sealed class TargetingCamera : IMainCameraProvider
    {
        internal TargetingCamera(Camera camera) => Camera = camera;
        public Camera Camera { get; }
        public bool TryGetCamera(out Camera camera) { camera = Camera; return camera != null; }
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly GameObject root;
        private readonly CharacterSO definition;

        internal ActorFixture(string suffix, string persistentId, bool owner = false)
        {
            root = CharacterAiPlanDebugFixtures.CreateActorObject(
                "A05 Actor " + suffix);
            root.SetActive(false);
            if (root.GetComponent<AbilityWork>() == null)
                root.AddComponent<AbilityWork>();
            CharacterAiEditorTestDependencies.Inject(root);
            root.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
            root.SetActive(true);
            definition = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "A05 Actor " + suffix,
                "Orc");
            if (owner)
                definition.role = CharacterRole.Owner;
            Actor = root.GetComponent<CharacterActor>();
            Actor.EnsureRuntimeState();
            Actor.Identity.SetPersistentId(persistentId);
            Actor.Initialization(definition);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        internal CharacterActor Actor { get; }

        public void Dispose()
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            if (definition != null)
                UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    public class NullProxy<T> : DispatchProxy where T : class
    {
        internal static T Create() => DispatchProxy.Create<T, NullProxy<T>>();

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] args)
        {
            ParameterInfo[] parameters = targetMethod?.GetParameters()
                ?? Array.Empty<ParameterInfo>();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].ParameterType.IsByRef)
                    continue;
                Type element = parameters[index].ParameterType
                    .GetElementType();
                args[index] = element != null && element.IsValueType
                    ? Activator.CreateInstance(element)
                    : null;
            }
            Type returnType = targetMethod?.ReturnType;
            return returnType == null || returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }

    private sealed class TestWorldRegistry : ICharacterAiWorldRegistry
    {
        private readonly List<CharacterActor> characters = new();
        private readonly List<CharacterActor> lifetimeCharacters = new();
        private readonly List<WildlifeActor> wildlife = new();
        private readonly List<BuildableObject> buildings = new();
        private readonly List<IWarehouseFacility> warehouses = new();
        private readonly List<IRetailFacility> retail = new();
        private Grid grid;
        private int version;

        public int Version => version;
        public int CharacterVersion => version;
        public int LifetimeCharacterVersion => version;
        public int WildlifeVersion => version;
        public int BuildingVersion => version;
        public int WarehouseVersion => version;
        public int RetailVersion => version;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public IReadOnlyList<CharacterActor> AllCharacters =>
            lifetimeCharacters;
        public IReadOnlyList<WildlifeActor> Wildlife => wildlife;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
        public IReadOnlyList<IWarehouseFacility> Warehouses => warehouses;
        public IReadOnlyList<IRetailFacility> RetailFacilities => retail;

        public void RegisterCharacter(CharacterActor actor)
        {
            if (actor != null && !characters.Contains(actor))
            {
                characters.Add(actor);
                version++;
            }
        }

        public void UnregisterCharacter(CharacterActor actor)
        {
            if (characters.Remove(actor))
                version++;
        }

        public void RegisterCharacterLifetime(CharacterActor actor)
        {
            if (actor != null && !lifetimeCharacters.Contains(actor))
            {
                lifetimeCharacters.Add(actor);
                version++;
            }
        }

        public void UnregisterCharacterLifetime(CharacterActor actor)
        {
            if (lifetimeCharacters.Remove(actor))
                version++;
        }

        public void RegisterWildlife(WildlifeActor actor)
        {
            if (actor != null && !wildlife.Contains(actor))
            {
                wildlife.Add(actor);
                version++;
            }
        }

        public void UnregisterWildlife(WildlifeActor actor)
        {
            if (wildlife.Remove(actor))
                version++;
        }

        public void RegisterBuilding(BuildableObject building)
        {
            if (building != null && !buildings.Contains(building))
            {
                buildings.Add(building);
                version++;
            }
        }

        public void UnregisterBuilding(BuildableObject building)
        {
            if (buildings.Remove(building))
                version++;
        }

        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor,
            string reason) => 0;

        public int GetTransientBuildingOwnershipCount(
            CharacterId characterId) => 0;

        public void RegisterWarehouse(IWarehouseFacility warehouse)
        {
            if (warehouse != null && !warehouses.Contains(warehouse))
            {
                warehouses.Add(warehouse);
                version++;
            }
        }

        public void UnregisterWarehouse(IWarehouseFacility warehouse)
        {
            if (warehouses.Remove(warehouse))
                version++;
        }

        public void SetGrid(Grid value)
        {
            grid = value;
            version++;
        }

        public bool TryGetGrid(out Grid value)
        {
            value = grid;
            return value != null;
        }

        public bool TryGetSessionState(out GameSessionState data)
        {
            data = default;
            return false;
        }

        public void Clear()
        {
            characters.Clear();
            lifetimeCharacters.Clear();
            wildlife.Clear();
            buildings.Clear();
            warehouses.Clear();
            retail.Clear();
            grid = null;
            version++;
        }
    }
}
#endif
