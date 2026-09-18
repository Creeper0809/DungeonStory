#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using DungeonStory.Operation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Main-owned important integration witness. Preparation is controlled; consumption,
// notification, Customer ownership and current save use their production paths.
public sealed class Wim040ObservedIncidentLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-040-observed-meal-customer-live.txt";
    private static bool running;
    private readonly List<string> lines = new();
    private readonly List<PhysicalMealConsumedEvent> consumed = new();
    private IGameTimeScaleController scale;
    private IDisposable subscription;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running, "Fresh disposable main Play required.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime missing.");
        var persistence = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot isolate user saves.");
        persistence.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Coroutine host missing.");
        var runner = new Wim040ObservedIncidentLiveRunner
        { scale = scope.Container.Resolve<IGameTimeScaleController>() };
        runner.Pause();
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try { host.StartCoroutine(runner.Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object value = null;
            bool moved;
            try { moved = pending.Peek().MoveNext(); if (moved) value = pending.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) pending.Push(nested); else yield return value;
        }
        try { while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose(); subscription?.Dispose(); }
        finally { Pause(); }
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("scope=real Orc Customer spawn/entry, physical emergency field-meal commands, controlled above-threshold torso precondition, actual contaminated-meal body collapse and staff AIRescue transfer to a controlled authored M11 placement. Contaminated stock is a controlled existing physical seed, not a natural spoilage claim. The M11 placement is test preparation, not construction-balance or completed-treatment evidence. Not natural restaurant scheduling, tribal taboo ingredient or other incident causes. Invalid replay bus inputs are controlled negative probes.");
        lines.Add("cleanup=paused disposable main Play; operator stops; disk persistence disabled before party selection");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(failure == null ? "WIM040 live PASS" : "WIM040 live FAIL: " + failure.Message);
        running = false;
    }

    private IEnumerator Run()
    {
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner UI unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>().TryReconcileNewRunTierZero(
                out _, out string reason), "TierZero: " + reason);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Actual party did not publish owner.");
        }
        var spawner = FindFirstObjectByType<CharacterSpawner>();
        Require(spawner != null, "Existing visitor spawner missing.");
        foreach (var actor in Actors()) actor.SetAiPaused(true);
        var beforeIds = Actors().Select(x => x.Identity.PersistentId).ToHashSet(StringComparer.Ordinal);
        Resume();
        bool requested = false;
        CharacterActor visitor = null;
        CharacterSpawnRejection rejection = CharacterSpawnRejection.None;
        float deadline = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!requested) requested = spawner.TrySpawnCharacter(2, out rejection);
            visitor = Actors().FirstOrDefault(x => !beforeIds.Contains(x.Identity.PersistentId)
                && x.characterType == CharacterType.Customer && x.Identity.Data?.id == 2);
            if (visitor != null && visitor.CurrentLifecycleState == CharacterLifecycleState.Active) break;
            yield return null;
        }
        Pause();
        Require(visitor != null && visitor.CurrentLifecycleState == CharacterLifecycleState.Active,
            "Actual Orc Customer entry failed: " + rejection + "; state=" + visitor?.CurrentLifecycleState);
        // The game clock is paused; keep this target executable because the
        // physical consumption port deliberately uses CanRunAi as admission.
        visitor.SetAiPaused(false);
        Require(visitor.CanRunAi, "Entered Customer is not executable for the physical meal command.");
        string target = visitor.Identity.PersistentId;
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var campaign = scope.Container.Resolve<V20CampaignRuntime>();
        var owned = scope.Container.Resolve<ISocietyIncidentOwnedCharacterIdQuery>();
        var items = scope.Container.Resolve<IWorldItemStackRuntime>();
        var money = scope.Container.Resolve<IGameMoneyAccount>();
        var bus = scope.Container.Resolve<IGameEventBus>();
        var beforeSave = saves.Capture();
        var beforeWorld = DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(beforeSave, CharacterWorldSaveSection.Id);
        Require(!beforeWorld.actors.Any(x => x.persistentId == target) && !owned.GetOwnedCustomerIds().Any(x => x.Value == target),
            "Fixture visitor already had another persistent owner; cannot isolate incident ownership.");

        // Only unaccepted emergency startup choices are removed from this
        // disposable scenario; no committed guest/physical ownership is erased.
        var catalog = scope.Container.Resolve<ISocietyEventCatalog>();
        var state = campaign.CaptureSociety();
        var startup = state.activeEvents.Where(x => catalog.Require(x.definitionId) is ServiceIncidentDefinitionSO
            or LifeEventDefinitionSO { emergency: true }).ToArray();
        Require(startup.All(x => !x.resolved && string.IsNullOrEmpty(x.selectedChoiceId)
            && x.guestDelivery.phase == GuestRequestDeliveryPhase.None && !x.hasObservedMealIncident),
            "Cannot remove a committed startup occurrence.");
        foreach (var entry in startup) state.activeEvents.Remove(entry);
        campaign.PublishSociety(campaign.PrepareSociety(state));
        scope.Container.Resolve<ICharacterDietPolicyCommand>().SetPolicy(visitor, CharacterDietPolicyKind.Vegan);
        scope.Container.Resolve<ICharacterDietPolicyCommand>().SetMealQualityLimit(visitor, CharacterMealQualityLimit.Lavish);
        visitor.Stats.ChangesStat(CharacterCondition.HUNGER, -visitor.Stats.GetConditionValue(CharacterCondition.HUNGER, 0));
        const string food = "food:lavish-meat";
        int Quantity() => items.GetAllStacks().Where(x => x.ItemId == food).Sum(x => x.Quantity);
        int beforeFood = Quantity();
        var cell = visitor.GetNowXY();
        Require(items.SpawnItemAt(food, 1, cell, WorldItemStackState.Loose, string.Empty, out int added)
            && added == 1 && Quantity() == beforeFood + 1, "Exact physical meal preparation failed.");
        var serving = items.GetAllStacks().Where(x => x.ItemId == food && x.Quantity > 0
            && x.State == WorldItemStackState.Loose && x.Position == cell).OrderBy(x => x.StackId, StringComparer.Ordinal).First();
        subscription = bus.Subscribe<PhysicalMealConsumedEvent>(x => consumed.Add(x));
        bool mealCommitted = scope.Container.Resolve<IFieldMealConsumptionCommand>().TryConsumeFieldMeal(visitor, new ItemStackId(serving.StackId), out var meal);
        Require(mealCommitted && meal.Success && meal.PolicyViolation && Quantity() == beforeFood && consumed.Count == 1,
            "Actual physical emergency dietary violation was not committed exactly once: code=" + meal.FailureCode
            + "; parameters=" + string.Join("/", meal.Parameters) + "; policyViolation=" + meal.PolicyViolation
            + "; quantityDelta=" + (Quantity() - beforeFood) + "; callbacks=" + consumed.Count);
        string operation = meal.OperationId.Value;
        var occurrence = campaign.ActiveSocietyEvents.Single(x => x.observedMealIncident?.operationId == operation);
        string instance = occurrence.instanceId;
        var observation = occurrence.observedMealIncident;
        Require(occurrence.hasObservedMealIncident && occurrence.definitionId == "service-incident:forbiddenmeal"
            && occurrence.participantCharacterIds.SequenceEqual(new[] { target })
            && observation.targetCharacterId == target && observation.itemDefinitionId == food
            && observation.fieldMeal && observation.observedPolicyViolation && !observation.observedDowned
            && owned.GetOwnedCustomerIds().Any(x => x.Value == target), "Actual callback cause/target/owner mismatch.");
        var notice = FindFirstObjectByType<EventAlertRuntime>().EventLog.Single(x => x.SourceId == instance);
        Require(notice.Detail.Contains(observation.observedCause) && !notice.Detail.Contains("조리표 오류"),
            "Notice displayed an authored but unobserved cooking-error cause.");
        lines.Add("PASS actual Customer/physical meal callback; quantityDelta=-1; target=" + target + "; operation=" + operation
            + "; instance=" + instance + "; observedCause=" + observation.observedCause);

        string beforeReplay = JsonUtility.ToJson(campaign.CaptureSociety());
        float hungerAfter = visitor.Stats.GetConditionValue(CharacterCondition.HUNGER, 0);
        bus.Publish(consumed[0]);
        bus.Publish(new PhysicalMealConsumedEvent(new ConsumableOperationId("consumable-operation:wim040:mismatch"), visitor, null, meal));
        bus.Publish(new PhysicalMealConsumedEvent(meal.OperationId, owner.CurrentOwnerActor, null, meal));
        Require(JsonUtility.ToJson(campaign.CaptureSociety()) == beforeReplay && Quantity() == beforeFood
            && visitor.Stats.GetConditionValue(CharacterCondition.HUNGER, 0) == hungerAfter,
            "Replay/mismatched operation/non-Customer reissued incident or physical meal effects.");
        lines.Add("PASS callback exact replay, mismatched operation and non-Customer probes leave Society/meal/hunger unchanged");

        var saved = saves.FromJson(saves.ToJson(saves.Capture()));
        var world = DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(saved, CharacterWorldSaveSection.Id);
        string characterWorldBefore = JsonUtility.ToJson(world);
        Require(world.actors.Count(x => x.persistentId == target) == 1, "Exact active incident visitor absent/duplicated in current capture.");
        var invalid = saves.FromJson(saves.ToJson(saved));
        var badWorld = DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(invalid, CharacterWorldSaveSection.Id);
        Require(badWorld.actors.RemoveAll(x => x.persistentId == target) == 1, "Invalid target fixture did not remove exactly one actor.");
        DungeonSaveSectionPayload.Write(invalid, CharacterWorldSaveSection.Id, CharacterWorldSaveSection.CurrentVersion,
            DungeonSaveRestorePhase.Characters, badWorld);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        string physicalBefore = JsonUtility.ToJson(items.Capture());
        int moneyBefore = money.Balance;
        Require(!saves.TryRestore(invalid, out var invalidResult) && !invalidResult.Success
            && JsonUtility.ToJson(campaign.CaptureSociety()) == beforeReplay
            && JsonUtility.ToJson(items.Capture()) == physicalBefore && money.Balance == moneyBefore
            && JsonUtility.ToJson(DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(saves.Capture(), CharacterWorldSaveSection.Id)) == characterWorldBefore
            && Actors().Count(x => x.Identity.PersistentId == target) == 1,
            "Missing incoming owned visitor accepted or failed whole restore changed live state.");
        lines.Add("PASS missing incoming visitor rejected atomically; validators=" + string.Join(" | ", invalidResult.Errors));
        // Conversely, keep the incoming Customer but remove its incident owner.
        // Live Society still owns it: restore must use incoming, not live, joins.
        var orphan = saves.FromJson(saves.ToJson(saved));
        var orphanSociety = DungeonSaveSectionPayload.ReadOrNew<SocietyEventWorldSaveData>(orphan, SocietyEventsSaveSection.Id);
        Require(orphanSociety.activeEvents.RemoveAll(x => x.instanceId == instance) == 1,
            "Orphan preparation did not remove exactly one incoming incident owner.");
        DungeonSaveSectionPayload.Write(orphan, SocietyEventsSaveSection.Id, SocietyEventWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState, orphanSociety);
        orphan.manifest = DungeonSaveManifest.Capture(orphan.sections);
        Require(!saves.TryRestore(orphan, out var orphanResult) && !orphanResult.Success
            && orphanResult.Errors.Any(x =>
                (x.Contains("exterior.activities") && x.Contains(target))
                || x.Contains("survival.character-consumables"))
            && JsonUtility.ToJson(campaign.CaptureSociety()) == beforeReplay
            && JsonUtility.ToJson(items.Capture()) == physicalBefore && money.Balance == moneyBefore
            && JsonUtility.ToJson(DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(saves.Capture(), CharacterWorldSaveSection.Id)) == characterWorldBefore,
            "Incoming orphan Customer borrowed the live incident owner or failed restore partially changed state: "
            + string.Join(" | ", orphanResult.Errors));
        lines.Add("PASS incoming orphan Customer rejected despite live incident ownership; CharacterWorld/Society/items/money unchanged");
        bool restoredOk;
        DungeonGameRestoreReport restoreResult;
        try { restoredOk = saves.TryRestore(saved, out restoreResult); }
        finally { Pause(); }
        Require(restoredOk && restoreResult.Success, "Current whole incident visitor restore failed: " + string.Join(" | ", restoreResult.Errors));
        visitor = Actors().Single(x => x.Identity.PersistentId == target);
        visitor.SetAiPaused(true);
        Require(visitor.characterType == CharacterType.Customer && owned.GetOwnedCustomerIds().Any(x => x.Value == target)
            && JsonUtility.ToJson(campaign.CaptureSociety()) == beforeReplay && Quantity() == beforeFood,
            "Whole restore lost target, frozen occurrence or physical meal conservation.");
        lines.Add("PASS current whole JSON restore preserves exact incident-owned Customer/frozen cause/consumed meal");

        if (money.Balance < 60) money.Add(60 - money.Balance);
        int funded = money.Balance;
        Require(scope.Container.Resolve<IEventAlertChoiceActionDispatcher>().TryDispatch(
            V21ContentAlertActionIds.Society(instance, "compensate"), out _), "Existing compensation command failed.");
        Require(money.Balance == funded - 60 && !owned.GetOwnedCustomerIds().Any(x => x.Value == target)
            && campaign.RecentResolvedSocietyEvents.Count(x => x.instanceId == instance && x.resolved) == 1,
            "Committed compensation failed to release exact visitor owner once.");
        scope.Container.Resolve<IEventAlertChoiceActionDispatcher>().TryDispatch(V21ContentAlertActionIds.Society(instance, "compensate"), out _);
        Require(money.Balance == funded - 60 && Quantity() == beforeFood, "Resolved replay repeated money or meal effects.");
        lines.Add("PASS real compensation dispatcher -60 once; exact visitor ownership released; no physical/financial replay");

        // The resolved incident releases this visitor's persistence owner. Let
        // the normal exit handoff retire that visit, then exercise contamination
        // with a separate real entry instead of reusing a stale pooled actor.
        (spawner.Interact(visitor) as IDisposable)?.Dispose();
        HashSet<string> contaminationBeforeIds = Actors()
            .Select(value => value.Identity.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        Resume();
        bool contaminationSpawnRequested = false;
        CharacterActor contaminationVisitor = null;
        CharacterSpawnRejection contaminationRejection =
            CharacterSpawnRejection.None;
        float contaminationSpawnDeadline = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < contaminationSpawnDeadline)
        {
            if (!contaminationSpawnRequested)
            {
                contaminationSpawnRequested = spawner.TrySpawnCharacter(
                    2,
                    out contaminationRejection);
            }
            contaminationVisitor = Actors().FirstOrDefault(value =>
                !contaminationBeforeIds.Contains(value.Identity.PersistentId)
                && value.characterType == CharacterType.Customer
                && value.Identity.Data?.id == 2);
            if (contaminationVisitor != null
                && contaminationVisitor.CurrentLifecycleState
                    == CharacterLifecycleState.Active)
            {
                break;
            }
            yield return null;
        }
        Pause();
        Require(contaminationVisitor != null
            && contaminationVisitor.CurrentLifecycleState
                == CharacterLifecycleState.Active,
            "Independent contamination Customer entry failed: "
                + contaminationRejection);
        visitor = contaminationVisitor;
        target = visitor.Identity.PersistentId;
        visitor.SetAiPaused(false);
        Require(visitor.CanRunAi,
            "Independent contamination Customer cannot execute the physical meal command.");
        ICharacterDietPolicyCommand contaminationDiet =
            scope.Container.Resolve<ICharacterDietPolicyCommand>();
        contaminationDiet.SetPolicy(visitor, CharacterDietPolicyKind.Free);
        contaminationDiet.SetMealQualityLimit(
            visitor,
            CharacterMealQualityLimit.Lavish);
        scope.Container.Resolve<IGameCalendar>().SetDateTime(
            scope.Container.Resolve<IGameCalendar>().Day + 1,
            12);
        visitor.Stats.ChangesStat(
            CharacterCondition.HUNGER,
            -visitor.Stats.GetConditionValue(CharacterCondition.HUNGER, 0f));
        cell = visitor.GetNowXY();
        int callbacksBeforeContamination = consumed.Count;
        Require(items.SpawnWasteAt(
                food,
                1,
                cell,
                WasteOriginKind.Plant,
                50f,
                out added)
            && added == 1
            && Quantity() == beforeFood + 1,
            "Controlled contaminated physical meal preparation failed.");
        WorldItemStackSnapshot contaminatedServing = items.GetAllStacks()
            .Where(x => x.ItemId == food
                && x.Quantity > 0
                && x.State == WorldItemStackState.Loose
                && x.Position == cell
                && x.Contamination > 0.01f)
            .OrderBy(x => x.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(contaminatedServing != null,
            "The prepared physical serving did not retain contamination.");
        bool contaminationCommitted = scope.Container.Resolve<
                IFieldMealConsumptionCommand>()
            .TryConsumeFieldMeal(
                visitor,
                new ItemStackId(contaminatedServing.StackId),
                out MealConsumptionResult contaminatedMeal);
        Require(contaminationCommitted
            && contaminatedMeal.Success
            && contaminatedMeal.Contaminated
            && Quantity() == beforeFood
            && consumed.Count == callbacksBeforeContamination + 1,
            "Actual contaminated physical meal was not committed exactly once: code="
                + contaminatedMeal.FailureCode
                + "; parameters=" + string.Join("/", contaminatedMeal.Parameters)
                + "; contaminated=" + contaminatedMeal.Contaminated
                + "; quantityDelta=" + (Quantity() - beforeFood)
                + "; callbacks=" + (consumed.Count - callbacksBeforeContamination));
        V20ActiveEventSaveData contaminationOccurrence = campaign
            .ActiveSocietyEvents.Single(x => string.Equals(
                x.observedMealIncident?.operationId,
                contaminatedMeal.OperationId.Value,
                StringComparison.Ordinal));
        Require(contaminationOccurrence.hasObservedMealIncident
            && string.Equals(
                contaminationOccurrence.definitionId,
                "service-incident:contamination",
                StringComparison.Ordinal)
            && contaminationOccurrence.observedMealIncident.incidentKind
                == ServiceIncidentKind.Contamination
            && contaminationOccurrence.participantCharacterIds.SequenceEqual(
                new[] { target })
            && owned.GetOwnedCustomerIds().Any(x => x.Value == target),
            "Actual contaminated meal did not publish the exact Contamination occurrence and owner.");
        // Call the existing eager exit handoff only while this second incident
        // owns the live visitor.  Leaving a pending exit on the first incident
        // and then releasing that owner would correctly retire the actor, making
        // it an invalid subject for this independent contamination scenario.
        var beforeExitCell = visitor.GetNowXY();
        (spawner.Interact(visitor) as IDisposable)?.Dispose();
        Require(visitor != null && visitor.gameObject.activeInHierarchy
            && visitor.CurrentLifecycleState == CharacterLifecycleState.Active
            && visitor.GetNowXY() == beforeExitCell && Quantity() == beforeFood
            && owned.GetOwnedCustomerIds().Any(x => x.Value == target),
            "Active contamination incident exit handoff retired/moved its exact target or meal stock.");
        lines.Add("PASS controlled public exit handoff retains active contamination Customer at the same cell; not a natural exit-path claim");
        EventAlertRecord contaminationNotice = FindFirstObjectByType<EventAlertRuntime>()
            .EventLog.Single(x => x.SourceId == contaminationOccurrence.instanceId);
        Require(string.Equals(
                contaminationNotice.Title,
                "오염 식사 섭취",
                StringComparison.Ordinal)
            && contaminationNotice.Detail.Contains(
                contaminationOccurrence.observedMealIncident.observedCause,
                StringComparison.Ordinal),
            "Contamination notice lost the observed meal cause or displayed the old room narrative.");
        DungeonGameSaveData contaminatedSaved = saves.FromJson(
            saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(contaminatedSaved, out DungeonGameRestoreReport contaminatedRestore)
            && contaminatedRestore.Success
            && campaign.ActiveSocietyEvents.Count(x => string.Equals(
                x.observedMealIncident?.operationId,
                contaminatedMeal.OperationId.Value,
                StringComparison.Ordinal)) == 1
            && Quantity() == beforeFood,
            "Current whole restore lost or replayed the consumed-contamination occurrence: "
            + string.Join(" | ", contaminatedRestore.Errors));
        lines.Add("PASS actual contaminated physical meal -> Contamination owner/notice; consumed stock remains -1 and current whole restore does not replay it");

        string contaminationInstance = contaminationOccurrence.instanceId;
        Require(scope.Container.Resolve<IEventAlertChoiceActionDispatcher>()
                .TryDispatch(
                    V21ContentAlertActionIds.Society(
                        contaminationInstance,
                        "quarantine"),
                    out DomainFailure contaminationChoiceFailure)
            && campaign.RecentResolvedSocietyEvents.Any(value =>
                value.instanceId == contaminationInstance && value.resolved)
            && !owned.GetOwnedCustomerIds().Any(value => value.Value == target),
            "Contamination incident did not resolve through its authored quarantine choice: "
                + contaminationChoiceFailure);
        lines.Add("PASS Contamination uses its own authored quarantine choice; transfer is not dispatched against the wrong incident kind");

        visitor = Actors().Single(value => string.Equals(
            value.Identity.PersistentId,
            target,
            StringComparison.Ordinal));
        (spawner.Interact(visitor) as IDisposable)?.Dispose();
        Resume();
        float contaminationExitDeadline = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < contaminationExitDeadline
            && Actors().Any(value => string.Equals(
                value.Identity.PersistentId,
                target,
                StringComparison.Ordinal)))
        {
            yield return null;
        }
        Pause();
        Require(!Actors().Any(value => string.Equals(
                value.Identity.PersistentId,
                target,
                StringComparison.Ordinal)),
            "Resolved contamination Customer did not complete the public exit handoff.");

        HashSet<string> collapseBeforeIds = Actors()
            .Select(value => value.Identity.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        Resume();
        bool collapseSpawnRequested = false;
        CharacterActor collapseVisitor = null;
        CharacterSpawnRejection collapseRejection =
            CharacterSpawnRejection.None;
        float collapseSpawnDeadline = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < collapseSpawnDeadline)
        {
            if (!collapseSpawnRequested)
            {
                collapseSpawnRequested = spawner.TrySpawnCharacter(
                    2,
                    out collapseRejection);
            }
            collapseVisitor = Actors().FirstOrDefault(value =>
                !collapseBeforeIds.Contains(value.Identity.PersistentId)
                && value.characterType == CharacterType.Customer
                && value.Identity.Data?.id == 2);
            if (collapseVisitor != null
                && collapseVisitor.CurrentLifecycleState
                    == CharacterLifecycleState.Active)
            {
                break;
            }
            yield return null;
        }
        Pause();
        Require(collapseVisitor != null
            && collapseVisitor.CurrentLifecycleState
                == CharacterLifecycleState.Active,
            "Independent MedicalCollapse Customer entry failed: "
                + collapseRejection);
        visitor = collapseVisitor;
        target = visitor.Identity.PersistentId;
        visitor.SetAiPaused(false);
        Require(visitor.CanRunAi,
            "MedicalCollapse Customer cannot execute the physical meal command before collapse.");
        ICharacterDietPolicyCommand collapseDiet =
            scope.Container.Resolve<ICharacterDietPolicyCommand>();
        collapseDiet.SetPolicy(visitor, CharacterDietPolicyKind.Free);
        collapseDiet.SetMealQualityLimit(
            visitor,
            CharacterMealQualityLimit.Lavish);
        visitor.Stats.ChangesStat(
            CharacterCondition.HUNGER,
            -visitor.Stats.GetConditionValue(CharacterCondition.HUNGER, 0f));

        BuildableObject isolation = EnsureIsolationRecoveryFacility(
            scope.Container,
            visitor);
        Vector2Int transferStart = visitor.GetNowXY();
        Require(isolation.centerPos != transferStart,
            "Controlled M11 must be distinct from the collapse cell.");

        ICharacterBodyHealthQuery bodyQuery =
            scope.Container.Resolve<ICharacterBodyHealthQuery>();
        ICharacterBodyHealthCommand bodyCommands =
            scope.Container.Resolve<ICharacterBodyHealthCommand>();
        CharacterBodyPartHealthState torso = bodyQuery.GetSnapshot(visitor).Parts
            .Single(value => value.bodyPart == CombatBodyPart.Torso);
        float targetTorsoHealth = torso.maxHealth * 0.25f + 1f;
        float preparationDamage = torso.currentHealth - targetTorsoHealth;
        Require(preparationDamage > 3f,
            "Fresh Customer lacks enough torso-health headroom for the controlled collapse boundary.");
        bodyCommands.ApplyLocalizedDamage(
            visitor,
            CombatBodyPart.Torso,
            preparationDamage,
            "qa:wim040-medical-collapse-precondition",
            allowDeath: false);
        CharacterBodyHealthSnapshot preparedBody = bodyQuery.GetSnapshot(visitor);
        CharacterBodyPartHealthState preparedTorso = preparedBody.Parts
            .Single(value => value.bodyPart == CombatBodyPart.Torso);
        float poisoningChance = Mathf.Clamp01(scope.Container
            .Resolve<ICharacterConsumablesWorldPort>()
            .ProjectGameplayEffect(
                new CharacterId(target),
                GameplayEffectTargetIds.FoodPoisoningChance,
                1f));
        Require(!preparedBody.Downed
            && visitor.CurrentLifecycleState == CharacterLifecycleState.Active
            && Mathf.Abs(preparedTorso.currentHealth - targetTorsoHealth) < 0.01f
            && poisoningChance >= 0.999f,
            "Controlled precondition did not remain living/active above the canonical torso collapse boundary; poisoningChance="
                + poisoningChance);

        cell = visitor.GetNowXY();
        int callbacksBeforeCollapse = consumed.Count;
        Require(items.SpawnWasteAt(
                food,
                1,
                cell,
                WasteOriginKind.Plant,
                50f,
                out added)
            && added == 1
            && Quantity() == beforeFood + 1,
            "Controlled MedicalCollapse meal preparation failed.");
        WorldItemStackSnapshot collapseServing = items.GetAllStacks()
            .Where(value => value.ItemId == food
                && value.Quantity > 0
                && value.State == WorldItemStackState.Loose
                && value.Position == cell
                && value.Contamination > 0.01f)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(collapseServing != null,
            "The controlled MedicalCollapse serving did not retain contamination.");
        bool collapseCommitted = scope.Container.Resolve<
                IFieldMealConsumptionCommand>()
            .TryConsumeFieldMeal(
                visitor,
                new ItemStackId(collapseServing.StackId),
                out MealConsumptionResult collapseMeal);
        CharacterBodyHealthSnapshot collapsedBody = bodyQuery.GetSnapshot(visitor);
        Require(collapseCommitted
            && collapseMeal.Success
            && collapseMeal.Contaminated
            && Quantity() == beforeFood
            && consumed.Count == callbacksBeforeCollapse + 1
            && collapsedBody.Downed
            && visitor.CurrentLifecycleState == CharacterLifecycleState.Downed,
            "Actual contaminated physical meal did not cross the canonical body/lifecycle collapse boundary exactly once: code="
                + collapseMeal.FailureCode
                + "; downed=" + collapsedBody.Downed
                + "; lifecycle=" + visitor.CurrentLifecycleState
                + "; quantityDelta=" + (Quantity() - beforeFood));
        V20ActiveEventSaveData collapseOccurrence = campaign
            .ActiveSocietyEvents.Single(value => string.Equals(
                value.observedMealIncident?.operationId,
                collapseMeal.OperationId.Value,
                StringComparison.Ordinal));
        Require(collapseOccurrence.hasObservedMealIncident
            && collapseOccurrence.definitionId
                == "service-incident:medicalcollapse"
            && collapseOccurrence.observedMealIncident.incidentKind
                == ServiceIncidentKind.MedicalCollapse
            && collapseOccurrence.observedMealIncident.observedDowned
            && collapseOccurrence.participantCharacterIds.SequenceEqual(
                new[] { target })
            && owned.GetOwnedCustomerIds().Any(value => value.Value == target),
            "Actual collapse callback did not publish the exact MedicalCollapse occurrence and owner.");
        string transferInstance = collapseOccurrence.instanceId;
        Require(scope.Container.Resolve<IEventAlertChoiceActionDispatcher>()
                .TryDispatch(
                    V21ContentAlertActionIds.Society(
                        transferInstance,
                        "transfer"),
                    out DomainFailure transferDispatchFailure),
            "Actual transfer choice dispatch failed: " + transferDispatchFailure);
        V20ActiveEventSaveData acceptedTransfer = campaign.ActiveSocietyEvents
            .Single(value => string.Equals(
                value.instanceId,
                transferInstance,
                StringComparison.Ordinal));
        Require(acceptedTransfer.observedIncidentResponse.phase
                == ObservedIncidentResponsePhase.Accepted
            && string.IsNullOrEmpty(
                acceptedTransfer.observedIncidentResponse.receiptId),
            "Transfer choice completed before the actual medical transport operation started.");

        CharacterActor rescuer = Actors()
            .Where(value => value != visitor
                && !value.IsDead
                && value.characterType is not CharacterType.Customer
                    and not CharacterType.Intruder
                && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && value.Brain?.availableActions?.Any(action =>
                    action?.actionset is AIRescue) == true
                && value.GetAbility<AbilityWork>() != null)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(rescuer != null,
            "No actual staff actor exposes the authored AIRescue path.");
        foreach (CharacterActor actor in Actors())
        {
            if (actor != visitor && actor != rescuer)
            {
                actor.SetAiPaused(true);
            }
        }
        AIAction rescueAction = rescuer.Brain.availableActions
            .First(action => action?.actionset is AIRescue);
        AbilityWork rescueWork = rescuer.GetAbility<AbilityWork>();
        rescuer.SetAiPaused(true);
        rescuer.Brain.enabled = true;
        if (rescuer.BehaviorTree != null)
        {
            rescuer.BehaviorTree.enabled = true;
        }
        rescueWork.SetDutyState(AbilityWork.DutyState.OnDuty);
        rescueWork.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Rescue,
            WorkPriorityLevel.Priority1);
        Neutralize(rescuer);
        rescuer.Brain.StopCurrentActionForReplan(
            "wim040-medical-transfer-setup");
        rescuer.Brain.availableActions = new[] { rescueAction };
        rescuer.Brain.PreferActionOnNextDecision<AIRescue>(300f);
        rescuer.SetAiPaused(false);
        rescuer.Brain.RequestImmediateReplan(clearFailures: true);

        Resume();
        ICharacterMedicalQuery medicalQuery =
            scope.Container.Resolve<ICharacterMedicalQuery>();
        bool rescueSelected = false;
        bool rescueAbilityRan = false;
        bool carried = false;
        bool physicallyCarried = false;
        bool treatmentPlacement = false;
        bool responseRestoreJoinChecked = false;
        string medicalOrderId = string.Empty;
        float transferDeadline = Time.realtimeSinceStartup + 75f;
        while (Time.realtimeSinceStartup < transferDeadline
            && !campaign.RecentResolvedSocietyEvents.Any(value =>
                string.Equals(
                    value.instanceId,
                    transferInstance,
                    StringComparison.Ordinal)))
        {
            Neutralize(rescuer);
            rescueSelected |= rescuer.Brain.bestAction?.actionset is AIRescue;
            rescueAbilityRan |= rescuer.GetComponent<AbilityRescue>()
                ?.IsRescuing == true;
            V20ActiveEventSaveData activeTransfer = campaign.ActiveSocietyEvents
                .SingleOrDefault(value => value.instanceId == transferInstance);
            if (activeTransfer != null
                && !string.IsNullOrEmpty(
                    activeTransfer.observedIncidentResponse
                        .externalOperationId))
            {
                medicalOrderId = activeTransfer.observedIncidentResponse
                    .externalOperationId;
            }
            if (!string.IsNullOrEmpty(medicalOrderId)
                && medicalQuery.TryGetOrder(
                    medicalOrderId,
                    out CharacterMedicalOrder currentOrder))
            {
                if (!responseRestoreJoinChecked
                    && activeTransfer?.observedIncidentResponse.phase
                        == ObservedIncidentResponsePhase.ExternalOperationStarted)
                {
                    Pause();
                    CharacterBodyHealthSnapshot joinBody =
                        bodyQuery.GetSnapshot(visitor);
                    CharacterBodyPartHealthState joinTorso = joinBody.Parts
                        .Single(value =>
                            value.bodyPart == CombatBodyPart.Torso);
                    lines.Add(FormattableString.Invariant(
                        $"DIAG transfer-start patient lifecycle={visitor.CurrentLifecycleState};isDead={visitor.IsDead};stats={visitor.Stats.CurrentHealth:0.###}/{visitor.Stats.MaxHealth:0.###};bodyDowned={joinBody.Downed};torso={joinTorso.currentHealth:0.###}/{joinTorso.maxHealth:0.###};bloodLoss={joinBody.BloodLoss:0.###};orderState={currentOrder.state};stabilized={currentOrder.stabilized};carried={currentOrder.carried}"));
                    string societyBeforeInvalidJoin = JsonUtility.ToJson(
                        campaign.CaptureSociety());
                    DungeonGameSaveData invalidJoin = saves.FromJson(
                        saves.ToJson(saves.Capture()));
                    DungeonCharacterMedicalSaveData invalidMedical =
                        DungeonSaveSectionPayload.ReadOrNew<
                            DungeonCharacterMedicalSaveData>(
                            invalidJoin,
                            CharacterMedicalSaveSection.Id);
                    CharacterMedicalOrder foreign = invalidMedical.orders.Single(
                        value => string.Equals(
                            value.orderId,
                            medicalOrderId,
                            StringComparison.Ordinal));
                    foreign.societyResponseOperationId += ":foreign";
                    DungeonSaveSectionPayload.Write(
                        invalidJoin,
                        CharacterMedicalSaveSection.Id,
                        DungeonCharacterMedicalSaveData.CurrentVersion,
                        DungeonSaveRestorePhase.RuntimeState,
                        invalidMedical);
                    invalidJoin.manifest = DungeonSaveManifest.Capture(
                        invalidJoin.sections);
                    Require(
                        !saves.TryRestore(
                            invalidJoin,
                            out DungeonGameRestoreReport invalidJoinResult)
                        && !invalidJoinResult.Success
                        && invalidJoinResult.Errors.Any(value =>
                            value.Contains(
                                "incident-response",
                                StringComparison.OrdinalIgnoreCase)
                            || value.Contains(
                                "Society response provenance",
                                StringComparison.Ordinal))
                        && JsonUtility.ToJson(campaign.CaptureSociety())
                            == societyBeforeInvalidJoin
                        && medicalQuery.TryGetOrder(
                            medicalOrderId,
                            out CharacterMedicalOrder unchangedOrder)
                        && string.Equals(
                            unchangedOrder.societyResponseOperationId,
                            activeTransfer.observedIncidentResponse.operationId,
                            StringComparison.Ordinal),
                        "Foreign medical response provenance was not rejected atomically: "
                            + string.Join(" | ", invalidJoinResult.Errors));
                    responseRestoreJoinChecked = true;
                    lines.Add("PASS started emergency transfer save captured exact Society/medical provenance; foreign parent operation restore rejected atomically");
                    Resume();
                }
                carried |= currentOrder.state
                        == CharacterMedicalOrderState.Carrying
                    || currentOrder.carried;
                physicallyCarried |= currentOrder.carried
                    && visitor.transform.IsChildOf(rescuer.transform);
                treatmentPlacement |= currentOrder.state is
                    CharacterMedicalOrderState.Treating
                    or CharacterMedicalOrderState.Recovering
                    or CharacterMedicalOrderState.Completed;
            }
            yield return null;
        }
        Pause();
        V20ActiveEventSaveData completedTransfer = campaign
            .RecentResolvedSocietyEvents
            .SingleOrDefault(value => string.Equals(
                value.instanceId,
                transferInstance,
                StringComparison.Ordinal));
        Require(completedTransfer != null
            && completedTransfer.observedIncidentResponse.phase
                == ObservedIncidentResponsePhase.EffectsPublished
            && completedTransfer.observedIncidentResponse.externalOperationId
                .StartsWith("medical:", StringComparison.Ordinal)
            && string.Equals(
                completedTransfer.observedIncidentResponse.receiptId,
                "character-arrived:"
                    + completedTransfer.observedIncidentResponse
                        .externalOperationId
                    + ":"
                    + target,
                StringComparison.Ordinal)
            && rescueSelected
            && rescueAbilityRan
            && responseRestoreJoinChecked
            && carried
            && physicallyCarried
            && treatmentPlacement
            && transferStart != isolation.centerPos
            && visitor.GetNowXY() == isolation.centerPos
            && medicalQuery.TryGetOrder(
                completedTransfer.observedIncidentResponse
                    .externalOperationId,
                out CharacterMedicalOrder completedMedicalOrder)
            && completedMedicalOrder.patientId == target
            && completedMedicalOrder.treatmentFacilityId
                == isolation.PersistentInstanceId.Value,
            "Actual isolation transfer did not complete through rescue carry into the frozen M11 with its exact arrival receipt.");
        lines.Add("PASS MedicalCollapse transfer stayed pending; actual staff AIRescue stabilized/carried the exact Downed Customer into frozen M11; medical-order arrival receipt published effects once");
    }

    private static BuildableObject EnsureIsolationRecoveryFacility(
        IObjectResolver container,
        CharacterActor customer)
    {
        IFacilityCapabilityQuery capabilities = container
            .Resolve<IFacilityCapabilityQuery>();
        Vector2Int start = customer.GetNowXY();
        BuildableObject existing = capabilities
            .FindOperational(FacilityCapabilityKind.IsolationRecovery)
            .Where(value => value != null)
            .OrderBy(value => Mathf.Abs(value.centerPos.x - start.x)
                + Mathf.Abs(value.centerPos.y - start.y))
            .ThenBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault(value =>
                value.TryGetNearestWorkAccessGridPosition(
                    value.Grid,
                    start,
                    out Vector2Int destination)
                && destination != start
                && value.Grid.GetMovePathTo(
                    start,
                    destination)?.Count > 0);
        if (existing != null)
        {
            return existing;
        }

        BuildingSO definition = Resources.Load<BuildingSO>(
            "SO/Building/Medical/M11_격리회복침상");
        Require(definition != null
            && definition.Abilities.OfType<ISurgicalFacilityAbility>()
                .Any(value => (value.FacilityTags
                        & SurgeryFacilityTag.IsolationRecovery)
                    == SurgeryFacilityTag.IsolationRecovery),
            "Authored M11 isolation-recovery facility is missing.");
        Require(container.Resolve<IGridSystemProvider>()
                .TryGetGrid(out Grid grid)
            && grid != null,
            "Main grid unavailable for controlled M11 placement.");
        GridPlacementValidator geometry = new();
        IRoomLayoutCache rooms = container.Resolve<IRoomLayoutCache>();
        Vector2Int? anchor = null;
        for (int y = 0; y < grid.height && !anchor.HasValue; y++)
        {
            for (int x = 0; x < grid.width && !anchor.HasValue; x++)
            {
                Vector2Int candidate = new(x, y);
                IReadOnlyList<Vector2Int> footprint = definition
                    .GetGridPosList(candidate);
                if (!rooms.TryGetRoom(grid, candidate, out RoomInstance room)
                    || room == null
                    || !room.IsUsable
                    || !footprint.All(room.ContainsCell)
                    || !geometry.AreInsideHorizontalBounds(
                        grid,
                        footprint,
                        1)
                    || !geometry.CanBuildInArea(
                        grid,
                        definition,
                        footprint)
                    || !geometry.CanOccupy(
                        grid,
                        definition.Placement.Layer,
                        footprint)
                    || !geometry.HasSupportBelow(grid, footprint))
                {
                    continue;
                }
                bool hasDistinctReachableAccess = BuildingWorkAccessRules
                    .EnumerateCandidates(
                        footprint,
                        definition.IsGridMovement)
                    .Any(access => access != start
                        && grid.IsValidGridPos(access)
                        && grid.IsWalkable(access)
                        && grid.GetMovePathTo(start, access)?.Count > 0);
                if (hasDistinctReachableAccess)
                {
                    anchor = candidate;
                }
            }
        }
        Require(anchor.HasValue,
            "No legal reachable footprint exists for controlled M11 placement.");
        DungeonStoryGridBuildingController builder =
            FindFirstObjectByType<DungeonStoryGridBuildingController>();
        string placementFailure = "building-controller-missing";
        bool placementCommitted = builder != null
            && builder.TryPlaceInitialBuildings(
                new[]
                {
                    new InitialBuildInfo
                    {
                        Building = definition,
                        Position = anchor.Value
                    }
                },
                out placementFailure);
        Require(placementCommitted,
            "Controlled M11 placement failed: " + placementFailure);
        BuildableObject placed = capabilities
            .FindOperational(FacilityCapabilityKind.IsolationRecovery)
            .Where(value => value != null)
            .OrderBy(value => Mathf.Abs(value.centerPos.x - start.x)
                + Mathf.Abs(value.centerPos.y - start.y))
            .ThenBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault(value => ReferenceEquals(
                    value.BuildingData,
                    definition)
                && value.TryGetNearestWorkAccessGridPosition(
                    value.Grid,
                    start,
                    out Vector2Int destination)
                && destination != start
                && value.Grid.GetMovePathTo(start, destination)?.Count > 0);
        Require(placed != null,
            "Placed M11 was not published through the typed facility query.");
        return placed;
    }

    private static IEnumerable<CharacterActor> Actors() => FindObjectsByType<CharacterActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(x => x != null && x.Identity != null && x.CurrentLifecycleState != CharacterLifecycleState.Despawned);
    private static void Neutralize(CharacterActor actor)
    {
        if (actor?.Stats == null) return;
        foreach (CharacterCondition condition in actor.Stats.StatSnapshot.Keys.ToArray())
            actor.Stats.Stats[condition] = 100f;
    }
    private static void Click(string name)
    {
        var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null && x.name == name
            && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy);
        Require(button != null && button.IsInteractable() && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual UI unavailable: " + name);
    }
    private void Pause() { var host = FindFirstObjectByType<GameManager>(); if (host != null) host.isPause = true; if (scale != null) scale.Scale = 0; }
    private void Resume() { FindFirstObjectByType<GameManager>().isPause = false; scale.Scale = 2; }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
#endif
