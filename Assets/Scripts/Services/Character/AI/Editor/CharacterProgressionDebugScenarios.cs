#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CharacterProgressionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run Progression Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Character progression scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        CleanupLeakedActorFixtures();
        CharacterSkillRuntimeEffects.ResetTransientExecutionStateForDebug();
        List<string> errors = new List<string>();
        Run("level 50 experience curve", VerifyExperienceCurve, errors);
        Run("potential distribution and rarity pity", VerifyPotentialAndRarityRules, errors);
        Run("initial stats and level-50 growth", VerifyStatsAndLevelGrowth, errors);
        Run("active, passive, and ultimate slots", VerifySkillMilestones, errors);
        Run("permanent choice and save round trip", VerifyPermanentChoiceAndPersistence, errors);
        Run("formula presentation response validation",
            VerifyFormulaPresentationContract,
            errors);
        Run("management passive reachable-effect legality",
            VerifyManagementPassiveReachability,
            errors);
        Run("empty-ledger skill generation fails closed",
            VerifyEmptyLedgerSkillPublicContextFallback,
            errors);
        Run("acquired-trait experience score", VerifyAcquiredTraitExperienceScore, errors);
        Run("acquired-trait milestone lifecycle", VerifyAcquiredTraitMilestoneLifecycle, errors);
        Run("acquired-trait formula v2 compositional drawbacks",
            VerifyAcquiredTraitFormulaV2Composition,
            errors);
        Run("authored acquired-trait effect projection",
            VerifyAuthoredAcquiredTraitEffectProjection,
            errors);
        Run("acquired-trait automatic producer resume",
            VerifyAcquiredTraitAutomaticProducerResume,
            errors);
        Run("acquired-trait whole-root save round trip",
            VerifyAcquiredTraitWholeRootSaveRoundTrip,
            errors);
        Run("memory-erasure seal atomicity", VerifyMemoryErasureSealAtomicity, errors);
        Run("in-flight generation timeout awaits narrative retry", VerifyInFlightGenerationTimeout, errors);
        Run("provider timeout circuit preserves queued requests",
            VerifyProviderCircuitDrainsQueuedRequests,
            errors);
        Run("LLM retry and request-key resume", VerifyRetryAndRequestKeyResume, errors);
        Run("ultimate domain use limits", VerifyUltimateUseLimits, errors);
        Run("independent scoped transient roots", VerifyIndependentTransientRoots, errors);
        Run("management and defense runtime effects", VerifySkillRuntimeEffects, errors);
        Run("manual work skill targeting, areas, and cooldown",
            VerifyManualWorkSkillTargetingAreasAndCooldown,
            errors);
        Run("manual work expedition formation projection",
            VerifyManualWorkExpeditionFormationProjection,
            errors);
        Run("training experience", VerifyTrainingExperience, errors);

        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log("Character progression scenarios passed.");
        }

        CleanupLeakedActorFixtures();
        return errors.Count == 0;
    }

    public static bool RunGenerationTimeoutScenario() =>
        VerifyInFlightGenerationTimeout();

    public static bool RunProviderCircuitScenario() =>
        VerifyProviderCircuitDrainsQueuedRequests();

    public static bool RunTransientRootLifetimeScenario() =>
        VerifyIndependentTransientRoots();

    public static bool RunEmptyLedgerSkillPublicContextFallbackScenario() =>
        VerifyEmptyLedgerSkillPublicContextFallback();

    public static bool RunFormulaPresentationContractScenario() =>
        VerifyFormulaPresentationContract();

    public static bool RunManagementPassiveReachabilityScenario() =>
        VerifyManagementPassiveReachability();

    public static bool RunAcquiredTraitFormulaV2CompositionScenario() =>
        VerifyAcquiredTraitFormulaV2Composition();

    public static bool RunAcquiredTraitMilestoneLifecycleScenario() =>
        VerifyAcquiredTraitMilestoneLifecycle();

    public static bool RunAcquiredTraitAutomaticProducerResumeScenario() =>
        VerifyAcquiredTraitAutomaticProducerResume();

    public static bool RunManualWorkSkillTargetingScenario() =>
        VerifyManualWorkSkillTargetingAreasAndCooldown();

    public static bool RunManualWorkExpeditionScenario() =>
        VerifyManualWorkExpeditionFormationProjection();

    private static bool VerifyManualWorkSkillTargetingAreasAndCooldown()
    {
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        world.Clear();
        GameObject gridObject = new GameObject(
            "Manual Skill Scenario GridSystemManager");
        gridObject.SetActive(false);
        GridSystemManager manager = gridObject.AddComponent<GridSystemManager>();
        manager.defaultGridWidth = 20;
        manager.defaultGridHeight = 20;
        gridObject.SetActive(true);
        manager.EnsureGridInitialized();
        Grid grid = manager.grid;
        world.SetGrid(grid);
        try
        {
            using ActorFixture source = new ActorFixture(8501, "시전자", "human");
            using ActorFixture nearA = new ActorFixture(8502, "근접 대상 A", "human");
            using ActorFixture nearB = new ActorFixture(8503, "근접 대상 B", "human");
            using ActorFixture far = new ActorFixture(8504, "원거리 대상", "human");
            CharacterActor[] actors =
            {
                source.Actor,
                nearA.Actor,
                nearB.Actor,
                far.Actor
            };
            foreach (CharacterActor actor in actors)
                world.RegisterCharacter(actor);

            source.Actor.transform.position = grid.GetWorldPos(new Vector2Int(10, 10));
            nearA.Actor.transform.position = grid.GetWorldPos(new Vector2Int(11, 10));
            nearB.Actor.transform.position = grid.GetWorldPos(new Vector2Int(12, 10));
            far.Actor.transform.position = grid.GetWorldPos(new Vector2Int(17, 17));

            CharacterSkillInstance self = CreateManualWorkTestSkill(
                "skill:qa:manual-self",
                CharacterSkillTargetingMode.Self,
                CharacterSkillEffectArea.Single,
                1);
            CharacterSkillInstance square = CreateManualWorkTestSkill(
                "skill:qa:manual-square",
                CharacterSkillTargetingMode.PlayerSelected,
                CharacterSkillEffectArea.Square,
                3);
            CharacterSkillInstance random = CreateManualWorkTestSkill(
                "skill:qa:manual-random",
                CharacterSkillTargetingMode.DeterministicRandom,
                CharacterSkillEffectArea.Single,
                1);
            CharacterSkillInstance all = CreateManualWorkTestSkill(
                "skill:qa:manual-all",
                CharacterSkillTargetingMode.AllEligible,
                CharacterSkillEffectArea.Dungeon,
                1);
            CharacterSkillInstance room = CreateManualWorkTestSkill(
                "skill:qa:manual-room",
                CharacterSkillTargetingMode.PlayerSelected,
                CharacterSkillEffectArea.Room,
                1);
            source.Actor.Progression.GrowthState.activeSkills.AddRange(
                new[] { self, square, random, all, room });

            FakeRoomLayoutCache rooms = new FakeRoomLayoutCache(new RoomInstance(
                1,
                new[]
                {
                    nearA.Actor.GetNowXY(),
                    nearB.Actor.GetNowXY()
                },
                Array.Empty<BuildableObject>(),
                Array.Empty<BuildableObject>(),
                Array.Empty<BuildableObject>(),
                solidBoundaryCount: 1,
                openBoundaryCount: 0,
                selfContained: true));

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, self.id, null, null, world, rooms, 100L,
                    out _),
                "Self manual work skills must activate immediately without a target click.");
            Require(HasManualBuff(source.Actor, self.id, 100L)
                    && !actors.Skip(1).Any(actor => HasManualBuff(actor, self.id, 100L)),
                "Self manual work skills must affect only the source actor.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, square.id, nearA.Actor, null, world, rooms,
                    100L, out _),
                "Player-selected square manual work skills must accept a legal actor anchor.");
            Require(HasManualBuff(nearA.Actor, square.id, 100L)
                    && HasManualBuff(nearB.Actor, square.id, 100L)
                    && !HasManualBuff(far.Actor, square.id, 100L),
                "A 3x3 manual work area must include legal actors within one cell of its anchor only.");
            Require(!CharacterManualSkillRuntime.TryActivate(
                    source.Actor, square.id, nearA.Actor, null, world, rooms,
                    101L, out string cooldownMessage)
                    && cooldownMessage.Contains("47시간", StringComparison.Ordinal),
                "Manual work skills must enforce their two-day in-game cooldown.");
            CharacterSkillUseLimitState roundTrip =
                source.Actor.Progression.GrowthState.useLimits.Clone();
            Require(roundTrip.manualSkillCooldowns.Any(value => value != null
                        && value.skillId == square.id
                        && value.readyAbsoluteHour == 148L)
                    && nearA.Actor.Progression.GrowthState.useLimits.Clone()
                        .manualSkillBuffs.Any(value => value != null
                            && value.skillId == square.id
                            && value.expiresAbsoluteHour == 124L),
                "Manual cooldown and temporary buff state must survive a save-state clone.");
            Require(HasManualBuff(nearA.Actor, square.id, 123L)
                    && !HasManualBuff(nearA.Actor, square.id, 124L),
                "Manual work buffs must expire at the authored in-game hour boundary.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, room.id, nearA.Actor, null, world, rooms,
                    100L, out _),
                "Room-area manual work skills must accept an actor inside a usable room.");
            Require(HasManualBuff(nearA.Actor, room.id, 100L)
                    && HasManualBuff(nearB.Actor, room.id, 100L)
                    && !HasManualBuff(far.Actor, room.id, 100L),
                "Room-area manual work skills must affect only legal actors in the anchor room.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, all.id, null, null, world, rooms, 100L,
                    out _),
                "Dungeon-area manual work skills must activate without a target click.");
            Require(actors.Skip(1).All(actor => HasManualBuff(actor, all.id, 100L))
                    && !HasManualBuff(source.Actor, all.id, 100L),
                "Dungeon-area ally skills must affect every legal ally and not the source.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, random.id, null, null, world, rooms, 100L,
                    out _),
                "Deterministic-random manual work skills must choose one current legal target.");
            string firstRandomTarget = actors.Skip(1)
                .Single(actor => HasManualBuff(actor, random.id, 100L))
                .Identity.PersistentId;
            ClearManualSkillState(source.Actor, actors, random.id);
            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, random.id, null, null, world, rooms, 777L,
                    out _),
                "Deterministic-random manual work skills must remain usable after test-state reset.");
            string secondRandomTarget = actors.Skip(1)
                .Single(actor => HasManualBuff(actor, random.id, 777L))
                .Identity.PersistentId;
            Require(string.Equals(firstRandomTarget, secondRandomTarget,
                    StringComparison.Ordinal),
                "Deterministic-random targeting must not change merely because the clock hour changed.");

            bool rejectedEvenSquare = false;
            bool rejectedSelfRoom = false;
            try
            {
                CharacterSkillAreaRules.RequireValid(
                    CharacterSkillTargetingMode.PlayerSelected,
                    CharacterSkillEffectArea.Square,
                    4);
            }
            catch (InvalidOperationException)
            {
                rejectedEvenSquare = true;
            }
            try
            {
                CharacterSkillAreaRules.RequireValid(
                    CharacterSkillTargetingMode.Self,
                    CharacterSkillEffectArea.Room,
                    1);
            }
            catch (InvalidOperationException)
            {
                rejectedSelfRoom = true;
            }
            Require(rejectedEvenSquare && rejectedSelfRoom,
                "Invalid even-sized square and self-room combinations must fail closed.");
            return true;
        }
        finally
        {
            world.Clear();
            if (gridObject != null)
                UnityEngine.Object.DestroyImmediate(gridObject);
        }
    }

    private static CharacterSkillInstance CreateManualWorkTestSkill(
        string id,
        CharacterSkillTargetingMode targetingMode,
        CharacterSkillEffectArea effectArea,
        int areaSize)
    {
        return new CharacterSkillInstance
        {
            id = id,
            displayName = id,
            kind = CharacterSkillKind.Active,
            trigger = CharacterSkillTrigger.ManualWork,
            target = targetingMode == CharacterSkillTargetingMode.Self
                ? CharacterSkillTarget.Self
                : CharacterSkillTarget.Ally,
            targetingMode = targetingMode,
            effectArea = effectArea,
            areaSize = areaSize,
            manualDurationHours = GameCalendarRules.HoursPerDay,
            manualCooldownDays = 2,
            modules = new List<CharacterSkillModuleSelection>
            {
                new CharacterSkillModuleSelection
                {
                    moduleId = "work_speed",
                    variantId = "small"
                }
            }
        };
    }

    private static bool VerifyManualWorkExpeditionFormationProjection()
    {
        using ActorFixture source = new ActorFixture(8511, "원정 시전자", "human");
        using ActorFixture frontA = new ActorFixture(8512, "전열 대상 A", "human");
        using ActorFixture frontB = new ActorFixture(8513, "전열 대상 B", "human");
        using ActorFixture middle = new ActorFixture(8514, "중열 대상", "human");
        using ActorFixture rear = new ActorFixture(8515, "후열 대상", "human");
        CharacterActor[] party =
        {
            source.Actor,
            frontA.Actor,
            frontB.Actor,
            middle.Actor,
            rear.Actor
        };
        Dictionary<CharacterActor, int> formation = new()
        {
            [source.Actor] = 0,
            [frontA.Actor] = 0,
            [frontB.Actor] = 0,
            [middle.Actor] = 1,
            [rear.Actor] = 2
        };

        CharacterSkillInstance room = CreateManualWorkTestSkill(
            "skill:qa:expedition-room",
            CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Room,
            1);
        CharacterSkillInstance square5 = CreateManualWorkTestSkill(
            "skill:qa:expedition-square5",
            CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Square,
            5);
        CharacterSkillInstance square7 = CreateManualWorkTestSkill(
            "skill:qa:expedition-square7",
            CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Square,
            7);
        CharacterSkillInstance random = CreateManualWorkTestSkill(
            "skill:qa:expedition-random",
            CharacterSkillTargetingMode.DeterministicRandom,
            CharacterSkillEffectArea.Single,
            1);
        CharacterSkillInstance all = CreateManualWorkTestSkill(
            "skill:qa:expedition-all",
            CharacterSkillTargetingMode.AllEligible,
            CharacterSkillEffectArea.Dungeon,
            1);
        source.Actor.Progression.GrowthState.activeSkills.AddRange(
            new[] { room, square5, square7, random, all });

        Require(!CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                room.id,
                null,
                party,
                actor => formation[actor],
                200L,
                out _),
            "Canceling expedition target selection must fail without applying a skill.");
        Require(CharacterManualSkillRuntime.GetCommands(source.Actor, 200L)
                .Single(value => value.SkillId == room.id)
                .IsReady,
            "Canceled expedition targeting must not consume cooldown.");
        Require(!CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                room.id,
                source.Actor,
                party,
                actor => formation[actor],
                200L,
                out _)
                && CharacterManualSkillRuntime.GetCommands(source.Actor, 200L)
                    .Single(value => value.SkillId == room.id)
                    .IsReady,
            "An illegal expedition target must not consume cooldown.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                room.id,
                frontA.Actor,
                party,
                actor => formation[actor],
                200L,
                out _),
            "Room scope must accept a living expedition ally anchor.");
        Require(HasManualBuff(frontA.Actor, room.id, 200L)
                && HasManualBuff(frontB.Actor, room.id, 200L)
                && !HasManualBuff(middle.Actor, room.id, 200L)
                && !HasManualBuff(rear.Actor, room.id, 200L),
            "Room scope must project to the anchor's expedition formation only.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                square5.id,
                frontA.Actor,
                party,
                actor => formation[actor],
                200L,
                out _),
            "5x5 scope must accept a living expedition ally anchor.");
        Require(HasManualBuff(frontA.Actor, square5.id, 200L)
                && HasManualBuff(frontB.Actor, square5.id, 200L)
                && HasManualBuff(middle.Actor, square5.id, 200L)
                && !HasManualBuff(rear.Actor, square5.id, 200L),
            "5x5 scope must project to the anchor and adjacent expedition formations.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                square7.id,
                frontA.Actor,
                party,
                actor => formation[actor],
                200L,
                out _),
            "7x7 scope must accept a living expedition ally anchor.");
        Require(party.Skip(1).All(actor => HasManualBuff(
                    actor,
                    square7.id,
                    200L))
                && !HasManualBuff(source.Actor, square7.id, 200L),
            "7x7 scope must project to every living legal expedition ally.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                random.id,
                null,
                party,
                actor => formation[actor],
                200L,
                out _),
            "Deterministic random expedition targeting must choose one legal ally.");
        string firstRandomTarget = party.Skip(1)
            .Single(actor => HasManualBuff(actor, random.id, 200L))
            .Identity.PersistentId;
        ClearManualSkillState(source.Actor, party, random.id);
        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                random.id,
                null,
                party,
                actor => formation[actor],
                999L,
                out _),
            "Reset deterministic random expedition targeting must remain usable.");
        string secondRandomTarget = party.Skip(1)
            .Single(actor => HasManualBuff(actor, random.id, 999L))
            .Identity.PersistentId;
        Require(string.Equals(firstRandomTarget, secondRandomTarget,
                StringComparison.Ordinal),
            "Expedition random targeting must not reroll when only time changes.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                all.id,
                null,
                party,
                actor => formation[actor],
                200L,
                out _),
            "All-eligible expedition targeting must activate without a click.");
        Require(party.Skip(1).All(actor => HasManualBuff(actor, all.id, 200L))
                && !HasManualBuff(source.Actor, all.id, 200L),
            "All-eligible expedition targeting must affect every living legal ally.");
        return true;
    }

    private static bool HasManualBuff(
        CharacterActor actor,
        string skillId,
        long absoluteHour) => CharacterManualSkillRuntime
        .GetActiveBuffSkills(actor, absoluteHour)
        .Any(value => value != null
            && string.Equals(value.id, skillId, StringComparison.Ordinal));

    private static void ClearManualSkillState(
        CharacterActor source,
        IEnumerable<CharacterActor> actors,
        string skillId)
    {
        source.Progression.GrowthState.useLimits.manualSkillCooldowns
            .RemoveAll(value => value != null
                && string.Equals(value.skillId, skillId, StringComparison.Ordinal));
        foreach (CharacterActor actor in actors)
        {
            actor.Progression.GrowthState.useLimits.manualSkillBuffs
                .RemoveAll(value => value != null
                    && string.Equals(value.skillId, skillId, StringComparison.Ordinal));
        }
    }

    private static bool VerifyAcquiredTraitExperienceScore()
    {
        CharacterNarrativeLedger ledger = new CharacterNarrativeLedger();
        ledger.Record(
            CharacterNarrativeDomain.Work,
            "work:craft-furniture",
            "building:chair-a",
            "completed");
        ledger.Record(
            CharacterNarrativeDomain.Work,
            "work:craft-furniture",
            "building:chair-b",
            "completed");
        ledger.Record(
            CharacterNarrativeDomain.Work,
            "work:craft-furniture",
            "building:chair-c",
            "completed");

        Require(
            ledger.MeaningfulRecordCount == 3,
            "The fixture no longer proves that subject-specific rows are distinct in the source ledger.");
        Require(
            CharacterAcquiredTraitExperienceScore.Require(ledger) == 2,
            "Three copies of one experience type must cross only the 1 and 3 aggregate thresholds.");

        for (int index = 3; index < 8; index++)
        {
            ledger.Record(
                CharacterNarrativeDomain.Work,
                "work:craft-furniture",
                $"building:chair-{index}",
                "completed");
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(ledger) == 3,
            "Changing only the target identity must not farm more than the aggregate 1, 3, and 8 thresholds.");

        for (int index = 0; index < 3; index++)
        {
            ledger.Record(
                CharacterNarrativeDomain.Survival,
                "survival:hunt",
                $"wildlife:wolf-{index}",
                "completed");
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(ledger) == 5,
            "A distinct experience type must contribute its own aggregate 1 and 3 thresholds.");

        CharacterNarrativeFact corrupted = ledger.facts[0];
        corrupted.milestoneCount++;
        Require(
            !CharacterAcquiredTraitExperienceScore.TryCalculate(
                ledger,
                out _,
                out string failure)
            && failure.Contains("expected exactly", StringComparison.Ordinal),
            "Acquired-trait scoring accepted a forged source-ledger milestone count.");
        return true;
    }

    private static bool VerifyAcquiredTraitFormulaV2Composition()
    {
        LoadAuthoredAcquiredTraitContent(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        Require(settings.FormulaPolicy.formulaVersion >= 2
                && settings.DrawbackCapabilities.Count >= 3,
            "The authored acquired-trait catalog does not expose formula v2 compositional drawbacks.");

        using ActorFixture negative = new(
            812570,
            "TraitV2Negative",
            "Slime",
            persistentId: "character:qa-trait-v2-negative");
        for (int index = 0; index < 3; index++)
            negative.Actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Combat,
                "combat:injury:qa-" + index,
                "target:qa-combat",
                "injury",
                day: index + 1,
                triggerPassives: false);
        string[] negativeEvidence = negative.Actor.Progression.NarrativeLedger.Facts
            .Select(CharacterAcquiredTraitEvidenceProjection.Project)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitPendingRequestState first =
            CharacterAcquiredTraitFormulaGeneration.Freeze(
                negative.Actor.Progression,
                "request:qa-trait-v2-negative",
                "request-key:qa-trait-v2-negative",
                3,
                settings,
                modules,
                negativeEvidence);
        CharacterAcquiredTraitPendingRequestState repeated =
            CharacterAcquiredTraitFormulaGeneration.Freeze(
                negative.Actor.Progression,
                "request:qa-trait-v2-negative",
                "request-key:qa-trait-v2-negative",
                3,
                settings,
                modules,
                negativeEvidence);
        Require(first.formulaVersion >= 2
                && first.benefitModuleIds.Count == 1
                && first.drawbackCapabilityIds.Count == 1
                && first.drawbackCredit > 0
                && first.formulaCapabilities.Count == 2
                && string.Equals(JsonUtility.ToJson(first),
                    JsonUtility.ToJson(repeated), StringComparison.Ordinal),
            "Formula v2 did not deterministically freeze one benefit and one credited drawback.");
        CharacterAcquiredTraitDrawbackCapabilityDefinition selectedDrawback =
            settings.DrawbackCapabilities.Single(value => string.Equals(
                value.DrawbackId,
                first.drawbackCapabilityIds[0],
                StringComparison.Ordinal));
        Require(selectedDrawback.DomainAffinities.Contains(
                CharacterNarrativeDomain.Combat)
                && string.Equals(first.drawbackId,
                    selectedDrawback.DrawbackId, StringComparison.Ordinal),
            "Formula v2 selected a drawback outside the negative evidence domain.");
        CharacterAcquiredTraitModuleSO selectedBenefit = modules.Single(value =>
            string.Equals(value.ModuleId, first.benefitModuleIds[0],
                StringComparison.Ordinal));
        Require(!first.effectOverrides.Any(value =>
                selectedBenefit.DrawbackBindingIds.Contains(
                    value.bindingId, StringComparer.Ordinal))
                && selectedDrawback.Effects.All(effect => first.effectOverrides.Any(
                    value => string.Equals(value.bindingId, effect.bindingId,
                        StringComparison.Ordinal))),
            "Formula v2 leaked a legacy fixed drawback or omitted the selected independent drawback.");

        using ActorFixture positive = new(
            812571,
            "TraitV2Positive",
            "Slime",
            persistentId: "character:qa-trait-v2-positive");
        for (int index = 0; index < 3; index++)
            positive.Actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Combat,
                "combat:victory:qa-" + index,
                "target:qa-combat",
                "completed",
                day: index + 1,
                triggerPassives: false);
        string[] positiveEvidence = positive.Actor.Progression.NarrativeLedger.Facts
            .Select(CharacterAcquiredTraitEvidenceProjection.Project)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitPendingRequestState noDrawback =
            CharacterAcquiredTraitFormulaGeneration.Freeze(
                positive.Actor.Progression,
                "request:qa-trait-v2-positive",
                "request-key:qa-trait-v2-positive",
                3,
                settings,
                modules,
                positiveEvidence);
        Require(noDrawback.drawbackCapabilityIds.Count == 0
                && noDrawback.drawbackCredit == 0
                && string.IsNullOrEmpty(noDrawback.drawbackId)
                && noDrawback.formulaCapabilities.Count == 1,
            "Formula v2 invented a drawback without compatible negative evidence.");
        return true;
    }

    private static bool VerifyAcquiredTraitMilestoneLifecycle()
    {
        using ActorFixture actor = new ActorFixture(
            812503,
            "Acquired Trait Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterProgression progression = actor.Actor.Progression;
        string targetId = actor.Actor.Identity.PersistentId;
        string[] evidence =
        {
            "work:craft-furniture",
            "work:operate-kitchen",
            "work:repair-equipment",
            "work:research-ritual"
        };
        foreach (string factId in evidence)
        {
            for (int count = 0; count < 50; count++)
            {
                progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    $"target:{factId}:{count}",
                    "completed",
                    1f,
                    count / 10);
            }
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(
                progression.NarrativeLedger) == 20,
            "The milestone fixture did not reach the exact 20-point acquired-trait score.");

        CharacterAcquiredTraitInferenceService service = new(
            content.Settings,
            content.Modules);
        CharacterAcquiredTraitInferenceCommandResult outOfOrder =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(progression, targetId, 8, 0, evidence));
        Require(
            !outOfOrder.Succeeded
            && outOfOrder.Audit.IssueCode
                == CharacterAcquiredTraitInferenceIssueCode.OutOfOrderMilestone,
            "A reached later milestone bypassed the lowest unprocessed milestone.");

        CharacterAcquiredTraitInferenceCommandResult firstSubmission =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(progression, targetId, 3, 0, evidence));
        Require(firstSubmission.Succeeded && firstSubmission.Packet != null,
            "The first acquired-trait milestone was not submitted.");
        CharacterAcquiredTraitInferenceCommandResult concurrent =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(
                    progression,
                    targetId,
                    3,
                    1,
                    evidence,
                    "parallel"));
        Require(
            !concurrent.Succeeded
            && concurrent.Audit.IssueCode
                == CharacterAcquiredTraitInferenceIssueCode.PendingRequestInFlight,
            "A second acquired-trait request was accepted while one was pending.");

        CharacterAcquiredTraitInferenceCommandResult firstCompletion =
            CompleteAcquiredMilestone(
                service,
                progression,
                targetId,
                firstSubmission,
                1);
        Require(firstCompletion.Succeeded,
            "The first acquired-trait milestone did not complete.");
        CharacterAcquiredTraitAggregateState afterFirst =
            progression.CaptureAcquiredTraitState();
        Require(
            afterFirst.revision == 2
            && afterFirst.ActiveCount == 1
            && afterFirst.processedMilestones.SequenceEqual(new[] { 3 }),
            "The first milestone did not commit exactly one active acquired trait.");

        CharacterAcquiredTraitInferenceCommandResult duplicateCompletion =
            CompleteAcquiredMilestone(
                service,
                progression,
                targetId,
                firstSubmission,
                1);
        Require(
            !duplicateCompletion.Succeeded
            && duplicateCompletion.Audit.IssueCode
                == CharacterAcquiredTraitInferenceIssueCode.DuplicateCallback
            && progression.CaptureAcquiredTraitState().revision == 2,
            "A duplicate completion callback changed acquired-trait state.");

        CompleteNextAcquiredMilestone(
            service,
            progression,
            targetId,
            8,
            evidence);
        CompleteNextAcquiredMilestone(
            service,
            progression,
            targetId,
            20,
            evidence);

        CharacterAcquiredTraitAggregateState completed =
            progression.CaptureAcquiredTraitState();
        Require(
            completed.revision == 6
            && completed.ActiveCount == 3
            && completed.processedMilestones.SequenceEqual(new[] { 3, 8, 20 }),
            "The 3/8/20 milestones did not complete once each in order.");
        Require(
            completed.CaptureActiveInstances()
                .SelectMany(value => value.moduleIds)
                .Distinct(StringComparer.Ordinal)
                .Count()
            == completed.CaptureActiveInstances()
                .Sum(value => value.moduleIds.Count),
            "An active acquired-trait module was selected more than once.");

        IReadOnlyList<IGameplayEffectSource> sources =
            CharacterAcquiredTraitEffectSourceProjection.Project(
                completed,
                progression.NarrativeLedger,
                content.Settings,
                content.Modules);
        Require(
            sources.Count == 3
            && sources.Select(value => value.SourceRef.SourceId)
                .Distinct(StringComparer.Ordinal)
                .Count() == 3,
            "Acquired traits did not project one independently removable effect source per instance.");

        CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
        using ActorFixture restoredActor = new ActorFixture(
            812504,
            "Acquired Trait Restore Fixture",
            "human");
        restoredActor.Actor.Progression.RestorePersistentState(snapshot);
        CharacterAcquiredTraitAggregateState restored = restoredActor.Actor
            .Progression.CaptureAcquiredTraitState();
        Require(
            restored.revision == completed.revision
            && restored.processedMilestones.SequenceEqual(
                completed.processedMilestones)
            && restored.instances.Select(value => value.instanceId)
                .SequenceEqual(
                    completed.instances.Select(value => value.instanceId),
                    StringComparer.Ordinal)
            && restored.instances.Select(value => value.combinationId)
                .SequenceEqual(
                    completed.instances.Select(value => value.combinationId),
                    StringComparer.Ordinal)
            && restored.instances.SelectMany(value => value.evidenceFactIds)
                .SequenceEqual(
                    completed.instances.SelectMany(value => value.evidenceFactIds),
                    StringComparer.Ordinal),
            "Acquired-trait milestones, identities, combinations, or evidence changed across save round trip.");
        return true;
    }

    private static CharacterAcquiredTraitSubmissionCommand BuildAcquiredSubmission(
        CharacterProgression progression,
        string targetId,
        int milestone,
        int expectedRevision,
        IEnumerable<string> evidence,
        string suffix = null)
    {
        string token = string.IsNullOrEmpty(suffix)
            ? milestone.ToString()
            : $"{milestone}:{suffix}";
        NarrativePublicContextMaterial publicMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                progression,
                evidence);
        IReadOnlyList<string> projectedEvidence =
            CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(
                publicMaterial,
                evidence);
        return new CharacterAcquiredTraitSubmissionCommand(
            targetId,
            $"request:acquired:{token}",
            $"request-key:acquired:{token}",
            milestone,
            projectedEvidence,
            expectedRevision,
            NarrativeInferenceTimestamp.FromGameTick(milestone));
    }

    private static CharacterAcquiredTraitInferenceCommandResult
        CompleteAcquiredMilestone(
            CharacterAcquiredTraitInferenceService service,
            CharacterProgression progression,
            string targetId,
            CharacterAcquiredTraitInferenceCommandResult submission,
            int expectedRevision)
    {
        CharacterAcquiredTraitRequestPacketDto packet = submission.Packet;
        CharacterAcquiredTraitAggregateState state = progression
            .CaptureAcquiredTraitState();
        state.TryGetPendingRequest(
            packet.requestId,
            out CharacterAcquiredTraitPendingRequestState pending);
        CharacterAcquiredTraitInstanceState completed = state.instances
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.originatingRequestId,
                    packet.requestId,
                    StringComparison.Ordinal));
        Require(pending != null || completed != null,
            "The acquired-trait completion fixture cannot resolve its pending or completed request.");
        string response;
        if (pending?.presentationState
            == CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
        {
            response = JsonUtility.ToJson(
                new CharacterAcquiredTraitModuleSelectionResponseDto
                {
                    selectionId = pending.moduleSelectionId,
                    positiveModuleIds = new List<string>
                    {
                        pending.offeredBenefitModuleIds[0]
                    },
                    drawbackModuleIds = new List<string>(),
                    evidenceFactIds = new List<string> { pending.evidenceFactIds[0] },
                    displayName = "후천 특성 발현",
                    narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                });
        }
        else if ((pending?.formulaVersion ?? completed.formulaVersion) > 0)
        {
            response = JsonUtility.ToJson(
                new CharacterAcquiredTraitFormulaPresentationDto
                {
                    presentationId = pending?.presentationId
                        ?? completed.presentationId,
                    displayName = "후천 특성 발현",
                    narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                });
        }
        else
        {
            CharacterAcquiredTraitCombinationPacketDto selected =
                packet.combinationOptions[0];
            response = JsonUtility.ToJson(new CharacterAcquiredTraitResponseDto
            {
                combinationId = selected.combinationId,
                displayName = $"후천 특성 {packet.manifestationMilestone}",
                description = "반복된 경험이 새로운 성향으로 발현되었다.",
                narrativeReason = "검증된 경험 장부를 근거로 선택되었다.",
                evidenceFactIds = new List<string> { packet.evidenceFactIds[0] }
            });
        }
        return service.CompleteMilestone(
            progression,
            new CharacterAcquiredTraitCompletionCommand(
                targetId,
                packet.requestId,
                packet.requestKey,
                packet.candidatePacketHash,
                expectedRevision,
                expectedRevision,
                response,
                NarrativeInferenceTimestamp.FromGameTick(
                    packet.manifestationMilestone + 1L)),
            packet);
    }

    private static void CompleteNextAcquiredMilestone(
        CharacterAcquiredTraitInferenceService service,
        CharacterProgression progression,
        string targetId,
        int milestone,
        IEnumerable<string> evidence)
    {
        int submissionRevision = progression.AcquiredTraitRevision;
        CharacterAcquiredTraitInferenceCommandResult submission =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(
                    progression,
                    targetId,
                    milestone,
                    submissionRevision,
                    evidence));
        Require(submission.Succeeded,
            $"Acquired-trait milestone {milestone} submission failed: "
            + submission.Audit.ValidationError);
        CharacterAcquiredTraitInferenceCommandResult completion =
            CompleteAcquiredMilestone(
                service,
                progression,
                targetId,
                submission,
                submissionRevision + 1);
        Require(completion.Succeeded,
            $"Acquired-trait milestone {milestone} completion failed: "
            + completion.Audit.ValidationError);
    }

    private static bool VerifyMemoryErasureSealAtomicity()
    {
        VerifyMemoryErasureSealSuccessAndIdempotence();
        VerifyMemoryErasureSealPhysicalFailureLeavesEverythingUnchanged();
        VerifyMemoryErasureSealObserverFailureRollsBackEverything();
        return true;
    }

    private static bool VerifyAuthoredAcquiredTraitEffectProjection()
    {
        LoadAuthoredAcquiredTraitContent(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        Require(
            settings.ValidateDefinition().Count == 0
            && modules.Length == 8
            && modules.All(module => module.ValidateDefinition().Count == 0),
            "The authored acquired-trait catalog is missing or invalid.");

        int projectedBindingCount = 0;
        foreach (CharacterAcquiredTraitModuleSO module in modules)
        {
            CharacterNarrativeDomain domain = module.DomainAffinities[0];
            string token = module.ModuleId.Substring(
                module.ModuleId.LastIndexOf(':') + 1);
            string[] evidence =
            {
                $"qa:acquired:{token}:a",
                $"qa:acquired:{token}:b",
                $"qa:acquired:{token}:c"
            };
            CharacterNarrativeLedger ledger = new();
            foreach (string factId in evidence)
            {
                ledger.Record(
                    domain,
                    factId,
                    "target:qa:authored-acquired",
                    "completed");
            }

            string combinationId = CharacterAcquiredTraitCombinationIdentity
                .Build(new[] { module.ModuleId });
            CharacterAcquiredTraitAggregateState state = new()
            {
                revision = 1,
                processedMilestones = new List<int> { 3 },
                instances = new List<CharacterAcquiredTraitInstanceState>
                {
                    new()
                    {
                        instanceId = $"acquired-trait-instance:qa:{token}",
                        combinationId = combinationId,
                        moduleIds = new List<string> { module.ModuleId },
                        displayName = module.DisplayName,
                        description = module.Description,
                        narrativeReason = "작성된 경험이 후천 특성으로 발현되었다.",
                        evidenceFactIds = evidence.OrderBy(
                            value => value,
                            StringComparer.Ordinal).ToList(),
                        manifestationMilestone = 3,
                        originatingRequestId = $"request:qa:{token}",
                        originatingRequestKey = $"request-key:qa:{token}",
                        candidatePacketHash = NarrativeInferenceHash
                            .ComputeSha256Utf8($"packet:qa:{token}"),
                        selectionAuditId = $"audit:qa:{token}",
                        acceptedRevision = 1,
                        erasedAt = CharacterAcquiredTraitInstanceState
                            .NotErasedAtAbsoluteHour
                    }
                },
                pendingRequests = new List<CharacterAcquiredTraitPendingRequestState>()
            };
            IReadOnlyList<IGameplayEffectSource> sources =
                CharacterAcquiredTraitEffectSourceProjection.Project(
                    state,
                    ledger,
                    settings,
                    modules);
            Require(
                sources.Count == 1
                && sources[0].SourceRef.Kind
                    == GameplayEffectSourceKind.AcquiredTrait
                && string.Equals(
                    sources[0].SourceRef.SourceId,
                    state.instances[0].instanceId,
                    StringComparison.Ordinal)
                && sources[0].Effects.Count == module.Effects.Count,
                $"Authored module '{module.ModuleId}' did not project through the acquired-trait source boundary.");

            foreach (GameplayEffectBinding binding in module.Effects)
            {
                string conditionId = binding.condition?.ConditionId;
                GameplayEffectContext activeContext = new(
                    conditionId == null
                        ? Array.Empty<string>()
                        : new[] { conditionId });
                GameplayEffectProjectionResult projection =
                    CharacterGameplayEffectProjector.Resolve(
                        binding.definition.TargetId,
                        100f,
                        sources,
                        activeContext);
                GameplayEffectContribution contribution = projection
                    .Contributions.Single(value => string.Equals(
                        value.BindingId,
                        binding.bindingId,
                        StringComparison.Ordinal));
                Require(
                    !contribution.Suppressed
                    && Mathf.Approximately(
                        projection.Value,
                        ProjectSingleAuthoredBinding(100f, binding)),
                    $"Authored acquired-trait binding '{binding.bindingId}' did not reach its gameplay target with the authored value.");
                if (binding.condition != null)
                {
                    GameplayEffectProjectionResult inactive =
                        CharacterGameplayEffectProjector.Resolve(
                            binding.definition.TargetId,
                            100f,
                            sources,
                            new GameplayEffectContext());
                    Require(
                        inactive.Contributions.Single(value => string.Equals(
                            value.BindingId,
                            binding.bindingId,
                            StringComparison.Ordinal)).Suppressed,
                        $"Conditional acquired-trait binding '{binding.bindingId}' applied without its authored condition.");
                }
                projectedBindingCount++;
            }
        }

        Require(
            projectedBindingCount == 9,
            $"Expected nine authored acquired-trait bindings, projected {projectedBindingCount}.");
        return true;
    }

    private static float ProjectSingleAuthoredBinding(
        float baseValue,
        GameplayEffectBinding binding)
    {
        float value = binding.definition.Operation switch
        {
            GameplayEffectOperation.AddFlat => baseValue + binding.value,
            GameplayEffectOperation.AddPercent => baseValue * (1f + binding.value),
            GameplayEffectOperation.Multiply => baseValue * binding.value,
            GameplayEffectOperation.Override => binding.value,
            GameplayEffectOperation.ClampMinimum => Mathf.Max(
                baseValue,
                binding.value),
            GameplayEffectOperation.ClampMaximum => Mathf.Min(
                baseValue,
                binding.value),
            _ => throw new InvalidOperationException(
                $"Unsupported authored acquired-trait operation '{binding.definition.Operation}'.")
        };
        return Mathf.Clamp(
            value,
            binding.definition.MinimumResult,
            binding.definition.MaximumResult);
    }

    private static bool VerifyAcquiredTraitAutomaticProducerResume()
    {
        LoadAuthoredAcquiredTraitContent(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        CharacterProgressionSnapshot pendingSnapshot;
        const string PersistentId = "character:qa-acquired-producer-resume";
        using (ActorFixture source = new(
                   812508,
                   "Acquired Producer Source",
                   "human",
                   publishComposition: true,
                   persistentId: PersistentId))
        {
            foreach (string factId in new[]
                     {
                         "work:qa-producer-a",
                         "work:qa-producer-b",
                         "work:qa-producer-c"
                     })
            {
                source.Actor.Progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    "target:qa:producer",
                    "completed");
            }

            DeferredAcquiredTraitLlmRuntime transport = new();
            using CharacterAcquiredTraitManifestationRuntime runtime = new(
                new AcquiredTraitDefinitionSource(settings, modules),
                new FixedCharacterWorld(source.Actor),
                new TestLlmRuntimeProvider(transport),
                new FixedGameCalendar(day: 17, hour: 4),
                new MutableUiClock(),
                new GameEventBus(),
                EmptyGameplayOutcomeNarrativeEvidenceQuery.Instance);
            runtime.Start();
            runtime.Tick();
            CharacterAcquiredTraitAggregateState pending = source.Actor
                .Progression.CaptureAcquiredTraitState();
            Require(
                runtime.PendingRequestCount == 1
                && transport.PendingCallbackCount == 1
                && pending.pendingRequests.Count == 1
                && pending.ActiveCount == 0,
                "The automatic acquired-trait producer did not persist and dispatch the reached milestone.");
            pendingSnapshot = source.Actor.Progression.CapturePersistentState();
        }

        using ActorFixture restored = new(
            812509,
            "Acquired Producer Restored",
            "human",
            publishComposition: true,
            persistentId: PersistentId);
        restored.Actor.Progression.RestorePersistentState(pendingSnapshot);
        DeferredAcquiredTraitLlmRuntime resumedTransport = new();
        using CharacterAcquiredTraitManifestationRuntime resumedRuntime = new(
            new AcquiredTraitDefinitionSource(settings, modules),
            new FixedCharacterWorld(restored.Actor),
            new TestLlmRuntimeProvider(resumedTransport),
            new FixedGameCalendar(day: 17, hour: 5),
            new MutableUiClock(),
            new GameEventBus(),
            EmptyGameplayOutcomeNarrativeEvidenceQuery.Instance);
        resumedRuntime.Start();
        resumedRuntime.Tick();
        Require(
            resumedTransport.PendingCallbackCount == 1
            && resumedRuntime.PendingRequestCount == 1,
            "The automatic acquired-trait producer did not resume the exact persisted request.");
        resumedTransport.CompleteNextSuccess();
        CharacterAcquiredTraitAggregateState completed = restored.Actor
            .Progression.CaptureAcquiredTraitState();
        bool hasDiagnostic = resumedRuntime.TryGetLatestDiagnostic(
            PersistentId,
            out CharacterAcquiredTraitManifestationDiagnostic diagnostic);
        Require(
            completed.ActiveCount == 1
            && completed.pendingRequests.Count == 0
            && completed.processedMilestones.SequenceEqual(new[] { 3 })
            && hasDiagnostic
            && diagnostic.Status
                == CharacterAcquiredTraitManifestationStatus.CompletionSucceeded,
            "The resumed automatic acquired-trait callback did not commit exactly one manifestation. "
            + $"status={diagnostic.Status}; issue={diagnostic.IssueCode}; detail={diagnostic.Detail}");
        return true;
    }

    private static bool VerifyAcquiredTraitWholeRootSaveRoundTrip()
    {
        using ActorFixture actor = new(
            812510,
            "Acquired Whole Root Source",
            "human");
        using AcquiredTraitContentFixture content = new();
        string[] evidence =
        {
            "work:qa-root-a",
            "work:qa-root-b",
            "work:qa-root-c",
            "work:qa-root-d"
        };
        foreach (string factId in evidence)
        {
            for (int count = 0; count < 50; count++)
            {
                actor.Actor.Progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    $"target:qa:root:{factId}:{count}",
                    "completed");
            }
        }

        CharacterAcquiredTraitInferenceService inference = new(
            content.Settings,
            content.Modules);
        CompleteNextAcquiredMilestone(
            inference,
            actor.Actor.Progression,
            actor.Actor.Identity.PersistentId,
            3,
            evidence);
        CharacterAcquiredTraitInstanceState erasedCandidate = actor.Actor
            .Progression.CaptureAcquiredTraitState()
            .CaptureActiveInstances().Single();
        CompleteNextAcquiredMilestone(
            inference,
            actor.Actor.Progression,
            actor.Actor.Identity.PersistentId,
            8,
            evidence);

        const string EraseOperation = "operation:qa:root-erasure";
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            EraseOperation);
        MemoryErasureSealUseResult erase = new MemoryErasureSealTransactionService(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 18, hour: 6),
            new FakeReversibleCarriedDisposition()).TryErase(
                actor.Actor,
                erasedCandidate.instanceId,
                carry,
                "carried:qa:memory-erasure",
                EraseOperation);
        Require(
            erase.Status == MemoryErasureSealUseStatus.Succeeded,
            "The whole-root fixture could not establish erased history.");

        CharacterAcquiredTraitAggregateState beforePending = actor.Actor
            .Progression.CaptureAcquiredTraitState();
        CharacterAcquiredTraitInferenceCommandResult pending =
            inference.SubmitMilestone(
                actor.Actor.Progression,
                BuildAcquiredSubmission(
                    actor.Actor.Progression,
                    actor.Actor.Identity.PersistentId,
                    20,
                    beforePending.revision,
                    evidence));
        Require(pending.Succeeded, "The whole-root fixture could not persist a pending request.");

        CharacterProgressionSnapshot captured = actor.Actor.Progression
            .CapturePersistentState();
        DungeonCharacterWorldSaveData root = new()
        {
            actors = new List<DungeonCharacterSaveData>
            {
                new()
                {
                    persistentId = actor.Actor.Identity.PersistentId,
                    dataId = actor.Actor.Identity.Data.id,
                    displayName = actor.Actor.Identity.DisplayName,
                    characterType = actor.Actor.Identity.CharacterType,
                    lifecycleState = actor.Actor.CurrentLifecycleState,
                    level = captured.Level,
                    currentExperience = captured.CurrentExperience,
                    growth = captured.GrowthState.Clone(),
                    narrative = captured.NarrativeLedger.Clone(),
                    acquiredTraits = captured.AcquiredTraitState.Clone(),
                    carryInventory = carry.Capture()
                }
            }
        };
        DungeonCharacterWorldSaveData decoded = JsonUtility.FromJson<
            DungeonCharacterWorldSaveData>(JsonUtility.ToJson(root));
        DungeonCharacterSaveData saved = decoded.actors.Single();

        using ActorFixture restored = new(
            812511,
            "Acquired Whole Root Restored",
            "human");
        restored.Actor.Progression.RestorePersistentState(
            new CharacterProgressionSnapshot(
                saved.level,
                saved.currentExperience,
                saved.growth,
                saved.narrative,
                saved.acquiredTraits));
        CharacterAcquiredTraitAggregateState after = restored.Actor.Progression
            .CaptureAcquiredTraitState();
        Require(
            after.ActiveCount == 1
            && after.instances.Count(value => value.erased) == 1
            && after.pendingRequests.Count == 1
            && after.pendingRequests[0].manifestationMilestone == 20
            && after.processedMilestones.SequenceEqual(new[] { 3, 8 })
            && string.Equals(
                JsonUtility.ToJson(captured.AcquiredTraitState),
                JsonUtility.ToJson(after),
                StringComparison.Ordinal)
            && CharacterAcquiredTraitEffectSourceProjection.Project(
                after,
                restored.Actor.Progression.NarrativeLedger,
                content.Settings,
                content.Modules).Count == 1,
            "Active, erased, pending, processed, evidence, or projected acquired-trait state changed across the whole-root JSON boundary.");
        return true;
    }

    private static void LoadAuthoredAcquiredTraitContent(
        out CharacterAcquiredTraitSettingsSO settings,
        out CharacterAcquiredTraitModuleSO[] modules)
    {
        settings = AssetDatabase.LoadAssetAtPath<
            CharacterAcquiredTraitSettingsSO>(
            V25AcquiredTraitContentAssetBuilder.SettingsPath);
        modules = AssetDatabase.FindAssets(
                "t:CharacterAcquiredTraitModuleSO",
                new[] { V25AcquiredTraitContentAssetBuilder.ModuleRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<
                CharacterAcquiredTraitModuleSO>)
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        Require(settings != null, "The authored acquired-trait settings asset is missing.");
    }

    private static void VerifyMemoryErasureSealSuccessAndIdempotence()
    {
        using ActorFixture actor = new ActorFixture(
            812505,
            "Memory Erasure Success Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:success";
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        int ledgerFactCount = actor.Actor.Progression.NarrativeLedger.facts.Count;
        FakeReversibleCarriedDisposition disposition = new();
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 14, hour: 7),
            disposition);

        MemoryErasureSealUseResult result = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            "carried:qa:memory-erasure",
            OperationId);
        CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        Require(
            result.Status == MemoryErasureSealUseStatus.Succeeded
            && disposition.BeginCount == 1
            && disposition.AcknowledgeCount == 1
            && disposition.RollbackCount == 0,
            "A valid memory-erasure seal transaction did not commit exactly once.");
        Require(
            carry.CountItem(MemoryErasureSealItemRules.ItemId) == 0,
            "A successful memory-erasure transaction retained its physical seal.");
        Require(
            after.revision == before.revision + 1
            && after.processedMilestones.SequenceEqual(before.processedMilestones)
            && after.TryGetInstance(selected.instanceId, out var erased)
            && erased.erased
            && erased.erasedAt == GameCalendarRules.Project(14, 7).AbsoluteHour
            && string.Equals(erased.erasureAuditId, result.AuditId, StringComparison.Ordinal),
            "A successful memory-erasure transaction changed the wrong acquired-trait state.");
        Require(
            actor.Actor.Progression.NarrativeLedger.facts.Count == ledgerFactCount,
            "Memory erasure changed the source narrative ledger.");
        Require(
            CharacterAcquiredTraitEffectSourceProjection.Project(
                after,
                actor.Actor.Progression.NarrativeLedger,
                content.Settings,
                content.Modules).Count == 0,
            "Memory erasure left the selected acquired-trait effect source active.");

        MemoryErasureSealUseResult duplicate = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            "carried:qa:memory-erasure",
            OperationId);
        Require(
            duplicate.Status == MemoryErasureSealUseStatus.AlreadyCompleted
            && disposition.BeginCount == 1
            && actor.Actor.Progression.CaptureAcquiredTraitState().revision
                == after.revision,
            "Retrying the same committed erasure consumed or mutated state twice.");
    }

    private static void VerifyMemoryErasureSealPhysicalFailureLeavesEverythingUnchanged()
    {
        using ActorFixture actor = new ActorFixture(
            812506,
            "Memory Erasure Physical Failure Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:physical-failure";
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        CharacterCarryInventorySaveData carryBefore = carry.Capture();
        FakeReversibleCarriedDisposition disposition = new()
        {
            AllowCommit = false
        };
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 15, hour: 3),
            disposition);

        MemoryErasureSealUseResult result = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            "carried:qa:memory-erasure",
            OperationId);
        CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        Require(
            result.Status == MemoryErasureSealUseStatus.PhysicalCommitFailed
            && disposition.BeginCount == 1
            && disposition.AcknowledgeCount == 0
            && disposition.RollbackCount == 0,
            "A rejected physical seal debit reported the wrong terminal result.");
        Require(
            after.revision == before.revision
            && after.TryGetInstance(selected.instanceId, out var retained)
            && !retained.erased
            && CarrySnapshotsEqual(carryBefore, carry.Capture()),
            "A rejected physical seal debit changed the trait or carried item.");
    }

    private static void VerifyMemoryErasureSealObserverFailureRollsBackEverything()
    {
        using ActorFixture actor = new ActorFixture(
            812507,
            "Memory Erasure Observer Failure Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:observer-failure";
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        CharacterCarryInventorySaveData carryBefore = carry.Capture();
        int ledgerFactCount = actor.Actor.Progression.NarrativeLedger.facts.Count;
        FakeReversibleCarriedDisposition disposition = new();
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 16, hour: 11),
            disposition);
        actor.Actor.Progression.Changed += ThrowErasureObserverFailure;
        try
        {
            MemoryErasureSealUseResult result = service.TryErase(
                actor.Actor,
                selected.instanceId,
                carry,
                "carried:qa:memory-erasure",
                OperationId);
            CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
                .CaptureAcquiredTraitState();
            Require(
                result.Status == MemoryErasureSealUseStatus.TraitCommitFailed
                && disposition.BeginCount == 1
                && disposition.AcknowledgeCount == 0
                && disposition.RollbackCount == 1,
                "A post-publication observer failure did not enter atomic rollback.");
            Require(
                after.revision == before.revision
                && after.TryGetInstance(selected.instanceId, out var retained)
                && !retained.erased
                && CarrySnapshotsEqual(carryBefore, carry.Capture())
                && actor.Actor.Progression.NarrativeLedger.facts.Count == ledgerFactCount,
                "Observer failure rollback did not restore item, trait, and ledger state.");
        }
        finally
        {
            actor.Actor.Progression.Changed -= ThrowErasureObserverFailure;
        }
    }

    private static void ThrowErasureObserverFailure() =>
        throw new InvalidOperationException("qa-erasure-observer-failure");

    private static CharacterAcquiredTraitInstanceState ManifestFirstAcquiredTrait(
        CharacterActor actor,
        AcquiredTraitContentFixture content)
    {
        string[] evidence =
        {
            "work:qa-memory-seal-a",
            "work:qa-memory-seal-b",
            "work:qa-memory-seal-c"
        };
        foreach (string factId in evidence)
        {
            actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                factId,
                "target:" + factId,
                "completed",
                1f,
                0);
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(
                actor.Progression.NarrativeLedger) == 3,
            "Memory-erasure fixture did not reach the first manifestation gate.");
        CharacterAcquiredTraitInferenceService inference = new(
            content.Settings,
            content.Modules);
        CharacterAcquiredTraitInferenceCommandResult submission =
            inference.SubmitMilestone(
                actor.Progression,
                BuildAcquiredSubmission(
                    actor.Progression,
                    actor.Identity.PersistentId,
                    3,
                    0,
                    evidence));
        Require(submission.Succeeded,
            "Memory-erasure fixture could not submit its acquired trait.");
        CharacterAcquiredTraitInferenceCommandResult completion =
            CompleteAcquiredMilestone(
                inference,
                actor.Progression,
                actor.Identity.PersistentId,
                submission,
                1);
        Require(completion.Succeeded,
            "Memory-erasure fixture could not manifest its acquired trait.");
        return actor.Progression.CaptureAcquiredTraitState()
            .CaptureActiveInstances()
            .Single();
    }

    private static CharacterCarryInventory SeedCarriedMemoryErasureSeal(
        CharacterActor actor,
        string operationId)
    {
        CharacterCarryInventory carry = actor.gameObject
            .GetComponent<CharacterCarryInventory>()
            ?? actor.gameObject.AddComponent<CharacterCarryInventory>();
        carry.Restore(new CharacterCarryInventorySaveData
        {
            items = new List<CharacterCarriedItemSaveData>
            {
                new()
                {
                    carriedStackId = "carried:qa:memory-erasure",
                    sourceStackId = "stack:qa:memory-erasure",
                    ownerOperationId = operationId,
                    itemId = MemoryErasureSealItemRules.ItemId,
                    quantity = MemoryErasureSealItemRules.UseQuantity,
                    components = new List<ItemInstanceComponentSaveData>()
                }
            }
        });
        return carry;
    }

    private static bool CarrySnapshotsEqual(
        CharacterCarryInventorySaveData left,
        CharacterCarryInventorySaveData right)
    {
        CharacterCarriedItemSaveData[] leftItems = (left?.items
                ?? new List<CharacterCarriedItemSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        CharacterCarriedItemSaveData[] rightItems = (right?.items
                ?? new List<CharacterCarriedItemSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        return leftItems.Length == rightItems.Length
            && leftItems.Zip(rightItems, (a, b) =>
                    string.Equals(a.carriedStackId, b.carriedStackId, StringComparison.Ordinal)
                    && string.Equals(a.sourceStackId, b.sourceStackId, StringComparison.Ordinal)
                    && string.Equals(a.ownerOperationId, b.ownerOperationId, StringComparison.Ordinal)
                    && string.Equals(a.itemId, b.itemId, StringComparison.Ordinal)
                    && a.quantity == b.quantity)
                .All(value => value);
    }

    private static void CleanupLeakedActorFixtures()
    {
        foreach (CharacterActor actor in UnityEngine.Object
            .FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(value => value != null
                && value.name.StartsWith(
                    "ProgressionActor_",
                    StringComparison.Ordinal)))
        {
            UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }
    }

    private static bool VerifyPotentialAndRarityRules()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            const int Samples = 100000;
            int[] potentialCounts = new int[5];
            System.Random potentialRandom = new System.Random(104729);
            for (int i = 0; i < Samples; i++)
            {
                potentialCounts[(int)CharacterGrowthRules.RollPotential(settings, potentialRandom)]++;
            }

            float[] expected = { 0.45f, 0.30f, 0.15f, 0.08f, 0.02f };
            for (int i = 0; i < expected.Length; i++)
            {
                float actual = potentialCounts[i] / (float)Samples;
                Require(Mathf.Abs(actual - expected[i]) < 0.012f,
                    $"Potential grade {i} distribution was {actual:P2}.");
            }

            HashSet<CharacterSkillRarity> ordinaryRarities = new HashSet<CharacterSkillRarity>();
            int baselineUpper = 0;
            int pityUpper = 0;
            System.Random baselineRandom = new System.Random(13007);
            System.Random pityRandom = new System.Random(13007);
            for (int i = 0; i < Samples; i++)
            {
                CharacterSkillRarity baseline = CharacterGrowthRules.RollRarity(
                    settings,
                    CharacterPotentialGrade.Ordinary,
                    applyPity: false,
                    baselineRandom);
                CharacterSkillRarity pity = CharacterGrowthRules.RollRarity(
                    settings,
                    CharacterPotentialGrade.Ordinary,
                    applyPity: true,
                    pityRandom);
                ordinaryRarities.Add(baseline);
                if (baseline >= CharacterSkillRarity.Rare) baselineUpper++;
                if (pity >= CharacterSkillRarity.Rare) pityUpper++;
            }

            Require(ordinaryRarities.Count == Enum.GetValues(typeof(CharacterSkillRarity)).Length,
                "Ordinary potential could not roll every rarity.");
            Require(pityUpper > baselineUpper,
                "The missed-upper-rarity correction did not increase Rare-or-higher outcomes.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyIndependentTransientRoots()
    {
        const string CharacterKey = "character:transient-root-proof";
        CharacterId characterId = (CharacterId)CharacterKey;
        GameObject actorObject = new GameObject("TransientRootProofActor");
        CharacterCarryInventory inventory = null;
        ICharacterCarryInventoryRegistry firstCarry = null;
        ICharacterSkillTransientStateRegistry firstSkills = null;
        try
        {
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(CharacterKey);
            inventory = actorObject.AddComponent<CharacterCarryInventory>();

            using (IObjectResolver firstRoot = BuildTransientStateRoot())
            {
                firstCarry = firstRoot.Resolve<ICharacterCarryInventoryRegistry>();
                firstSkills = firstRoot.Resolve<ICharacterSkillTransientStateRegistry>();
                Require(
                    ReferenceEquals(firstCarry, firstSkills),
                    "Carry and skill transient state did not share one scoped owner.");
                firstCarry.Register(inventory);
                Require(
                    ReferenceEquals(firstCarry.Find(characterId), inventory),
                    "First root did not index its active carry inventory.");
                Require(
                    firstSkills.TryEnter(characterId, "event:base"),
                    "First root rejected a fresh execution key.");
                firstSkills.Exit(characterId, "event:base");
                Require(
                    !firstSkills.TryEnter(characterId, "event:base"),
                    "First root executed the same event key twice.");
                firstSkills.BeginWork(characterId, BuiltInWorkTypeIds.Operate, 1.4f);
                Require(
                    Mathf.Approximately(
                        firstSkills.GetWorkSpeedMultiplier(characterId),
                        1.4f),
                    "First root did not retain its work-speed snapshot.");

                for (int index = 0; index < 512; index++)
                {
                    string key = $"event:fifo:{index}";
                    Require(
                        firstSkills.TryEnter(characterId, key),
                        $"FIFO key {index} was unexpectedly rejected.");
                    firstSkills.Exit(characterId, key);
                }

                Require(
                    !firstSkills.TryEnter(characterId, "event:fifo:510"),
                    "The run-scoped owner forgot a recent executed-event key.");
                Require(
                    !firstSkills.TryEnter(characterId, "event:base"),
                    "The run-scoped owner evicted an earlier exactly-once key.");
            }

            Require(
                firstCarry.Find(characterId) == null,
                "Disposed first root retained an active carry inventory.");
            bool firstSkillOwnerDisposed = false;
            try
            {
                firstSkills.GetWorkSpeedMultiplier(characterId);
            }
            catch (ObjectDisposedException)
            {
                firstSkillOwnerDisposed = true;
            }
            Require(
                firstSkillOwnerDisposed,
                "Disposed first root still accepted skill-state queries.");

            using IObjectResolver secondRoot = BuildTransientStateRoot();
            ICharacterCarryInventoryRegistry secondCarry =
                secondRoot.Resolve<ICharacterCarryInventoryRegistry>();
            ICharacterSkillTransientStateRegistry secondSkills =
                secondRoot.Resolve<ICharacterSkillTransientStateRegistry>();
            Require(
                ReferenceEquals(secondCarry, secondSkills),
                "Second root did not resolve one composite transient owner.");
            Require(
                secondCarry.Find(characterId) == null,
                "Second root inherited the first root's carry inventory.");
            Require(
                Mathf.Approximately(
                    secondSkills.GetWorkSpeedMultiplier(characterId),
                    1f),
                "Second root inherited the first root's work-speed snapshot.");
            Require(
                secondSkills.TryEnter(characterId, "event:fifo:510"),
                "Second root inherited the first root's executed-event keys.");
            secondSkills.Exit(characterId, "event:fifo:510");
            return true;
        }
        finally
        {
            if (actorObject != null)
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }
    }

    private static IObjectResolver BuildTransientStateRoot()
    {
        ContainerBuilder builder = new ContainerBuilder();
        builder.Register<CharacterCarryInventoryRegistry>(Lifetime.Singleton)
            .As<ICharacterCarryInventoryRegistry>()
            .As<ICharacterSkillTransientStateRegistry>()
            .As<ICharacterRuntimeTransientStateRegistry>();
        return builder.Build();
    }

    private static bool VerifyStatsAndLevelGrowth()
    {
        using ActorFixture fixture = new ActorFixture(81001, "Growth", "Orc");
        CharacterProgression progression = fixture.Actor.Progression;
        IReadOnlyList<CharacterStartingProficiencyExperience> starts =
            progression.GrowthState.startingProficiencies;
        CharacterStartingProficiencyRules.Validate(starts);
        string initialProjection = string.Join(
            "|",
            starts.OrderBy(value => value.proficiencyId, StringComparer.Ordinal)
                .Select(value => $"{value.proficiencyId}:{value.experience}"));

        progression.AddExperience(GetExperienceToReach(50));
        Require(progression.Level == 50, $"Expected level 50, got {progression.Level}.");
        Require(progression.CurrentExperience == 0 && progression.ExperienceToNextLevel == 0,
            "Level 50 retained experience or a next-level requirement.");
        Require(string.Equals(
                initialProjection,
                string.Join(
                    "|",
                    progression.GrowthState.startingProficiencies
                        .OrderBy(value => value.proficiencyId, StringComparer.Ordinal)
                        .Select(value => $"{value.proficiencyId}:{value.experience}")),
                StringComparison.Ordinal),
            "Generic character levels changed proficiency-derived performance.");
        return true;
    }

    private static bool VerifyExperienceCurve()
    {
        Require(CharacterProgression.GetExperienceRequired(1) == 20,
            "Level 1 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(10) == 20,
            "Level 10 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(11) == 25,
            "Level 11 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(21) == 30,
            "Level 21 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(41) == 40,
            "Level 41 XP requirement changed.");
        Require(GetExperienceToReach(50) == 1460,
            $"Level 1->50 cumulative XP was {GetExperienceToReach(50)}, not 1460.");
        return true;
    }

    private static bool VerifySkillMilestones()
    {
        using ActorFixture fixture = new ActorFixture(81002, "Milestone", "Slime");
        CharacterProgression progression = fixture.Actor.Progression;
        Require(progression.PassiveSkills.Count == 1,
            "The identity passive was not granted at level 1. "
            + DescribeDraftState(progression));
        Require(ChooseActive(progression, 1, 0), "The level-1 active could not be chosen.");

        progression.AddExperience(GetExperienceToReach(5));
        Require(ChooseActive(progression, 5, 1), "The level-5 active could not be chosen.");

        progression.AddExperience(GetExperienceBetween(5, 25));
        CharacterNarrativeDomain[] domains =
        {
            CharacterNarrativeDomain.Work,
            CharacterNarrativeDomain.Relationship,
            CharacterNarrativeDomain.Combat
        };
        for (int i = 0; i < 8; i++)
        {
            progression.RecordNarrative(domains[i % domains.Length], $"fact-{i}", "qa", "completed", 1f, i + 1);
        }

        Require(progression.NarrativeLedger.MeaningfulRecordCount >= 8,
            "Eight structured narrative records were not retained.");
        Require(progression.NarrativeLedger.MeaningfulDomainCount >= 3,
            "Narrative breadth did not reach three domains.");
        Require(progression.PassiveSkills.Count == 2,
            "The level-25 narrative passive was not granted.");

        progression.AddExperience(GetExperienceBetween(25, 30));
        Require(ChooseActive(progression, 30, 2), "The level-30 active could not be chosen.");
        Require(progression.ActiveSkills.Count == CharacterProgression.NormalActiveSlots,
            "The three fixed normal-active slots were not filled.");

        progression.AddExperience(GetExperienceBetween(30, 50));
        Require(progression.Ultimate != null
            && progression.Ultimate.kind == CharacterSkillKind.Ultimate
            && progression.Ultimate.ultimateDomain != CharacterUltimateDomain.None,
            "The level-50 narrative ultimate was not committed.");
        return true;
    }

    private static bool VerifyUltimateUseLimits()
    {
        using ActorFixture fixture = new ActorFixture(81008, "Ultimate", "Orc");
        CharacterProgression progression = fixture.Actor.Progression;
        progression.AddExperience(GetExperienceToReach(50));
        CharacterSkillInstance ultimate = progression.Ultimate;
        Require(ultimate != null,
            "The level-50 ultimate was not available for use-limit checks. "
            + DescribeDraftState(progression));

        ultimate.ultimateDomain = CharacterUltimateDomain.Offense;
        CharacterCombatAbilityDefinition combatAbility = CharacterSkillRuntimeEffects.ToCombatAbility(
            ultimate,
            progression.SkillSettings);
        Require(combatAbility != null && combatAbility.CooldownTurns >= 999,
            "The offense ultimate was not restricted to one use per battle.");
        Require(progression.TryMarkUltimateUsed(CharacterUltimateDomain.Offense, 101),
            "The first offense ultimate use was rejected.");
        Require(!progression.TryMarkUltimateUsed(CharacterUltimateDomain.Offense, 101)
            && progression.CanUseUltimate(CharacterUltimateDomain.Offense, 102),
            "The offense ultimate did not reset on a new battle serial.");

        ultimate.ultimateDomain = CharacterUltimateDomain.Defense;
        Require(progression.TryMarkUltimateUsed(CharacterUltimateDomain.Defense, 202)
            && !progression.TryMarkUltimateUsed(CharacterUltimateDomain.Defense, 202)
            && progression.CanUseUltimate(CharacterUltimateDomain.Defense, 203),
            "The defense ultimate was not limited per invasion.");

        ultimate.ultimateDomain = CharacterUltimateDomain.Management;
        Require(progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 303)
            && !progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 303)
            && progression.CanUseUltimate(CharacterUltimateDomain.Management, 304),
            "The management ultimate was not limited per operating day.");

        CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
        using ActorFixture restored = new ActorFixture(81009, "UltimateRestored", "Orc");
        restored.Actor.Progression.RestorePersistentState(snapshot);
        CharacterSkillUseLimitState restoredLimits = restored.Actor.Progression.GrowthState.useLimits;
        Require(!restoredLimits.CanUse(CharacterUltimateDomain.Offense, 101)
            && !restoredLimits.CanUse(CharacterUltimateDomain.Defense, 202)
            && !restoredLimits.CanUse(CharacterUltimateDomain.Management, 303),
            "Ultimate use limits changed during save/restore.");
        return true;
    }

    private static bool VerifySkillRuntimeEffects()
    {
        using ActorFixture workerFixture = new ActorFixture(81010, "RuntimeWorker", "Slime");
        CharacterActor worker = workerFixture.Actor;
        CharacterGrowthState growth = worker.Progression.GrowthState;
        growth.passiveSkills.Clear();
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-management-passive",
            displayName = "살림 감각",
            description = "실제 경영 수치 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.WorkCompleted,
            target = CharacterSkillTarget.Facility,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("work_speed", "small"),
                Module("output", "small"),
                Module("stock", "small"),
                Module("cleaning", "small"),
                Module("repair", "small"),
                Module("research", "small"),
                Module("revenue", "small")
            }
        });
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-relationship-passive",
            displayName = "부드러운 말씨",
            description = "긍정적 관계 반응 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.RelationshipChanged,
            target = CharacterSkillTarget.Ally,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("relationship", "small")
            }
        });

        Require(
            worker.Progression.OwnerFixedSkills.Count == 0,
            "A regular employee received owner-only fixed skills.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(worker), 1f),
            "Work-speed module leaked as an always-on bonus before work started.");
        CharacterSkillRuntimeEffects.BeginWork(
            worker,
            null,
            BuiltInWorkTypeIds.Operate,
            "qa-management-work-started");
        float workSpeedModuleTotal = CharacterSkillRuntimeEffects.GetManagementModuleTotal(
            worker,
            "work_speed",
            CharacterSkillTrigger.WorkCompleted);
        float workSpeedSnapshot =
            CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(worker);
        Require(
            Mathf.Approximately(workSpeedSnapshot, 1.1f),
            $"Work-speed module did not snapshot at work start. "
            + $"snapshot={workSpeedSnapshot:0.###}; "
            + $"moduleTotal={workSpeedModuleTotal:0.###}; "
            + $"passives={growth.passiveSkills.Count}");
        CharacterSkillRuntimeEffects.EndWork(worker);
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(worker), 1f),
            "Work-speed snapshot was not cleared after work ended.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(worker), 1.1f),
            "Output module did not affect the real production multiplier.");
        Require(CharacterSkillRuntimeEffects.GetStockProductionBonus(worker) == 1,
            "Stock module did not add a real produced item.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetCleaningSpeedMultiplier(worker), 1.05f),
            "Cleaning module did not affect cleaning duration.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetRepairSpeedMultiplier(worker), 1.08f),
            "Repair module did not affect repair duration.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetResearchWorkBonus(worker, 1f), 5f),
            "Research module did not add real research work.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetRevenueMultiplier(worker), 1.05f),
            "Revenue module did not affect real shop revenue.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.ApplyPositiveRelationshipBonus(worker, 0.5f), 0.52f),
            "Relationship module did not strengthen a positive social result.");

        growth.passiveSkills.Clear();
        growth.ultimate = new CharacterSkillInstance
        {
            id = "qa-management-ultimate",
            displayName = "오늘의 운영법",
            description = "하루 생산량 증가",
            kind = CharacterSkillKind.Ultimate,
            trigger = CharacterSkillTrigger.OperatingDayStarted,
            target = CharacterSkillTarget.Dungeon,
            ultimateDomain = CharacterUltimateDomain.Management,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("output", "large")
            }
        };
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(worker), 1f),
            "Management ultimate became active before the operating-day trigger.");
        Require(worker.Progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 5)
            && Mathf.Approximately(CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(worker), 1.3f)
            && !worker.Progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 5)
            && worker.Progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 6),
            "Management ultimate activation or once-per-day reset was incorrect.");

        using ActorFixture intruderFixture = new ActorFixture(81011, "RuntimeIntruder", "Orc");
        CharacterActor intruder = intruderFixture.Actor;
        float healthBefore = intruder.Stats.CurrentHealth;
        CharacterSkillInstance defenseUltimate = new CharacterSkillInstance
        {
            id = "qa-defense-ultimate",
            displayName = "수호자의 일격",
            description = "침입자에게 실제 피해",
            kind = CharacterSkillKind.Ultimate,
            trigger = CharacterSkillTrigger.InvasionStarted,
            target = CharacterSkillTarget.Enemy,
            ultimateDomain = CharacterUltimateDomain.Defense,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("damage", "light")
            }
        };
        CharacterSkillRuntimeEffects.ApplyDefenseUltimate(worker, defenseUltimate, intruder);
        Require(intruder.Stats.CurrentHealth < healthBefore,
            "Defense ultimate did not change the spawned intruder's health.");

        growth.passiveSkills.Clear();
        CharacterSkillRuntimeEffects.ResetTransientExecutionStateForDebug();
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-battle-start-passive",
            displayName = "Battle opener",
            description = "Battle-start passive damage probe.",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.BattleStarted,
            target = CharacterSkillTarget.Enemy,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("damage", "light")
            }
        });
        OffenseBattleCombatant ally = new OffenseBattleCombatant(
            worker.Identity.PersistentId,
            worker.Identity.DisplayName,
            worker.Identity.SpeciesTag,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 8f, 4f, 1f, 4f, 4f),
            100f);
        OffenseBattleCombatant enemy = new OffenseBattleCombatant(
            "qa-battle-passive-enemy",
            "Passive Target",
            "Training",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(100f, 4f, 2f, 1f, 2f, 2f),
            100f);
        CombatEquipmentRuntime battleEquipment = CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog: EmptyResourceEconomyContentCatalog.Instance,
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: EmptyEquipmentModuleCatalog.Instance,
            itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        CombatResolutionService battleResolution = new CombatResolutionService(
            new UnityCombatRandomSource(new DungeonStory.Foundation.RandomStreamProvider(40405)),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure:
                NoOpCharacterEnvironmentExposureCommand.Instance);
        OffenseBattleSession passiveSession = new OffenseBattleSession(
            "qa-battle-passive-session",
            "qa-expedition",
            "qa-target",
            "QA Passive Battle",
            DungeonDifficulty.Normal,
            new[] { ally, enemy },
            battleResolution,
            battleEquipment);
        CharacterCombatAbilityDefinition convertedPassive =
            CharacterSkillRuntimeEffects.ToCombatAbility(
                growth.passiveSkills[0],
                worker.Progression.SkillSettings);
        float enemyHealthBefore = enemy.CurrentHealth;
        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
            worker,
            CharacterSkillTrigger.BattleStarted,
            "qa-battle-passive-started",
            passiveSession,
            ally,
            enemy));
        if (!(enemy.CurrentHealth < enemyHealthBefore))
        {
            Debug.LogError(
                "BATTLE_PASSIVE_DIAGNOSTIC "
                + $"passives={growth.passiveSkills.Count}; converted={convertedPassive != null}; "
                + $"effects={convertedPassive?.Effects.Count ?? -1}; basic={passiveSession.CalculateBasicDamage(ally, enemy):0.##}; "
                + $"enemy={enemyHealthBefore:0.##}->{enemy.CurrentHealth:0.##}; "
                + $"actorId={worker.Identity.PersistentId}; source={ally.PersistentId}/{ally.Formation}; "
                + $"target={enemy.PersistentId}/{enemy.Formation}; logs={string.Join(" / ", passiveSession.Log)}");
        }
        Require(enemy.CurrentHealth < enemyHealthBefore,
            "Battle-start passive module did not affect the real battle session. "
            + $"passives={growth.passiveSkills.Count}; converted={convertedPassive != null}; "
            + $"effects={convertedPassive?.Effects.Count ?? -1}; basic={passiveSession.CalculateBasicDamage(ally, enemy):0.##}; "
            + $"enemy={enemyHealthBefore:0.##}->{enemy.CurrentHealth:0.##}; "
            + $"actorId={worker.Identity.PersistentId}; source={ally.PersistentId}/{ally.Formation}; "
            + $"target={enemy.PersistentId}/{enemy.Formation}; logs={string.Join(" / ", passiveSession.Log)}");
        RequireCombatFormation(
            CharacterSkillTarget.Enemy,
            Module("damage", "light"),
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            "Direct attack generated the wrong formation constraints.");
        RequireCombatFormation(
            CharacterSkillTarget.Enemy,
            Module("delay", "short"),
            OffenseFormationMask.Middle | OffenseFormationMask.Rear,
            OffenseFormationMask.Any,
            "Ranged control generated the wrong formation constraints.");
        RequireCombatFormation(
            CharacterSkillTarget.Ally,
            Module("heal", "minor"),
            OffenseFormationMask.Middle | OffenseFormationMask.Rear,
            OffenseFormationMask.Any,
            "Support generated the wrong formation constraints.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 50.01f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: false,
            "Wounded amplification triggered above the selected target's threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 50f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: true,
            "Wounded amplification did not include the exact selected-target threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 49.99f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: true,
            "Wounded amplification did not trigger below the selected target's threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 24f,
            targetHealth: 50.01f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: false,
            "Wounded amplification inspected the caster instead of the selected target.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 25.01f,
            threshold: 0.25f,
            extraDamageMultiplier: 0.7f,
            expectedToApply: false,
            "Critical amplification triggered above the selected target's threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 25f,
            threshold: 0.25f,
            extraDamageMultiplier: 0.7f,
            expectedToApply: true,
            "Critical amplification did not include the exact selected-target threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 24.99f,
            threshold: 0.25f,
            extraDamageMultiplier: 0.7f,
            expectedToApply: true,
            "Critical amplification did not trigger below the selected target's threshold.");

        CharacterSkillInstance defenseConditional = new CharacterSkillInstance
        {
            id = "qa-defense-conditional",
            displayName = "조건부 방어 기여",
            description = "방어 궁극기 경로의 현재 무조건 피해 기여 검증",
            kind = CharacterSkillKind.Ultimate,
            trigger = CharacterSkillTrigger.InvasionStarted,
            target = CharacterSkillTarget.Enemy,
            ultimateDomain = CharacterUltimateDomain.Defense,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("conditional_amplify", "wounded")
            }
        };
        using ActorFixture healthyDefenseTarget =
            new ActorFixture(81012, "HealthyDefenseTarget", "Orc");
        using ActorFixture woundedDefenseTarget =
            new ActorFixture(81013, "WoundedDefenseTarget", "Orc");
        woundedDefenseTarget.Actor.ApplyDamage(
            woundedDefenseTarget.Actor.MaxHealth * 0.6f,
            "qa-defense-threshold-setup");
        float healthyDefenseBefore = healthyDefenseTarget.Actor.CurrentHealth;
        float woundedDefenseBefore = woundedDefenseTarget.Actor.CurrentHealth;
        CharacterSkillRuntimeEffects.ApplyDefenseUltimate(
            worker,
            defenseConditional,
            healthyDefenseTarget.Actor);
        CharacterSkillRuntimeEffects.ApplyDefenseUltimate(
            worker,
            defenseConditional,
            woundedDefenseTarget.Actor);
        float healthyDefenseDamage = healthyDefenseBefore
            - healthyDefenseTarget.Actor.CurrentHealth;
        float woundedDefenseDamage = woundedDefenseBefore
            - woundedDefenseTarget.Actor.CurrentHealth;
        Require(healthyDefenseDamage > 0f
                && Mathf.Approximately(healthyDefenseDamage, woundedDefenseDamage),
            "Defense-ultimate conditional amplification no longer matches its current "
            + "unconditional fixed-contribution runtime semantics.");
        return true;
    }

    private static void RequireConditionalAmplifyBoundary(
        CharacterActor actor,
        CombatEquipmentRuntime equipment,
        CombatResolutionService resolution,
        float sourceHealth,
        float targetHealth,
        float threshold,
        float extraDamageMultiplier,
        bool expectedToApply,
        string message)
    {
        OffenseBattleCombatant source = new OffenseBattleCombatant(
            actor.Identity.PersistentId,
            "Conditional Source",
            "Training",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 8f, 4f, 1f, 4f, 4f),
            sourceHealth);
        OffenseBattleCombatant target = new OffenseBattleCombatant(
            $"qa-conditional-target-{sourceHealth:R}-{targetHealth:R}-{threshold:R}",
            "Conditional Target",
            "Training",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(100f, 4f, 2f, 1f, 2f, 2f),
            targetHealth);
        OffenseBattleSession session = new OffenseBattleSession(
            $"qa-conditional-session-{sourceHealth:R}-{targetHealth:R}-{threshold:R}",
            "qa-expedition",
            "qa-target",
            "QA Conditional Amplification",
            DungeonDifficulty.Normal,
            new[] { source, target },
            resolution,
            equipment);
        float before = target.CurrentHealth;
        float expectedDamage = session.CalculateBasicDamage(source, target)
            * extraDamageMultiplier;
        string variantId = threshold <= 0.25f ? "critical" : "wounded";
        CharacterSkillInstance skill = new CharacterSkillInstance
        {
            id = $"qa-conditional-{variantId}-{sourceHealth:R}-{targetHealth:R}",
            displayName = "Conditional Amplification QA",
            description = "Production conditional amplification boundary verification.",
            narrativeReason = "QA",
            kind = CharacterSkillKind.Active,
            rarity = CharacterSkillRarity.Legendary,
            trigger = CharacterSkillTrigger.ManualCombat,
            target = CharacterSkillTarget.Enemy,
            cooldownTurns = 1,
            usableFrom = OffenseFormationMask.Any,
            targetPositions = OffenseFormationMask.Any,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("conditional_amplify", variantId)
            }
        };
        CharacterSkillRuntimeEffects.ExecuteSkill(
            new CharacterSkillExecutionContext(
                actor,
                CharacterSkillTrigger.ManualCombat,
                $"qa-conditional-event-{variantId}-{sourceHealth:R}-{targetHealth:R}",
                session,
                source,
                target),
            skill);
        float actualDamage = before - target.CurrentHealth;
        Require(expectedToApply
                ? Mathf.Approximately(actualDamage, expectedDamage)
                : Mathf.Approximately(actualDamage, 0f),
            message + $" source={sourceHealth:R}; target={targetHealth:R}; "
            + $"threshold={threshold:R}; multiplier={extraDamageMultiplier:R}; "
            + $"damage={actualDamage:R}; expected={expectedDamage:R}");
    }

    private static void RequireCombatFormation(
        CharacterSkillTarget target,
        CharacterSkillModuleSelection module,
        OffenseFormationMask expectedUsableFrom,
        OffenseFormationMask expectedTargets,
        string message)
    {
        CharacterSkillFormationRules.Resolve(
            target,
            new[] { module },
            out OffenseFormationMask usableFrom,
            out OffenseFormationMask targetPositions);
        CharacterSkillInstance skill = new CharacterSkillInstance
        {
            id = $"qa-formation-{module.moduleId}",
            displayName = "Formation QA",
            description = "Formation verification.",
            narrativeReason = "Formation verification.",
            kind = CharacterSkillKind.Active,
            rarity = CharacterSkillRarity.Common,
            trigger = CharacterSkillTrigger.ManualCombat,
            target = target,
            usableFrom = usableFrom,
            targetPositions = targetPositions,
            modules = new List<CharacterSkillModuleSelection> { module }
        };
        CharacterCombatAbilityDefinition ability = CharacterSkillRuntimeEffects.ToCombatAbility(
            skill,
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults());
        Require(ability != null
                && ability.UsableFrom == expectedUsableFrom
                && ability.TargetPositions == expectedTargets,
            message);
    }

    private static CharacterSkillModuleSelection Module(string moduleId, string variantId)
    {
        return new CharacterSkillModuleSelection
        {
            moduleId = moduleId,
            variantId = variantId
        };
    }

    private static bool VerifyPermanentChoiceAndPersistence()
    {
        using ActorFixture source = new ActorFixture(81003, "Source", "Vampire");
        CharacterProgression progression = source.Actor.Progression;
        Require(ChooseActive(progression, 1, 2),
            "Initial active selection failed. " + DescribeDraftState(progression));
        string selectedId = progression.ActiveSkills[0].id;
        Require(!progression.TryChooseActiveSkill(1, 0, confirmed: true, out _),
            "A permanently selected active could be replaced.");

        CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
        DungeonCharacterSaveData serialized = new DungeonCharacterSaveData
        {
            level = snapshot.Level,
            currentExperience = snapshot.CurrentExperience,
            growth = snapshot.GrowthState.Clone(),
            narrative = snapshot.NarrativeLedger.Clone()
        };
        string json = JsonUtility.ToJson(serialized);
        DungeonCharacterSaveData restoredData = JsonUtility.FromJson<DungeonCharacterSaveData>(json);

        using ActorFixture restored = new ActorFixture(81004, "Restored", "Vampire");
        restored.Actor.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
            restoredData.level,
            restoredData.currentExperience,
            restoredData.growth,
            restoredData.narrative));
        Require(restored.Actor.Progression.ActiveSkills.Count == 1
            && restored.Actor.Progression.ActiveSkills[0].id == selectedId,
            "The permanent active changed during JSON save/restore.");
        CharacterSkillDraft restoredDraft = restored.Actor.Progression.Drafts
            .First(draft => draft.kind == CharacterSkillKind.Active && draft.unlockLevel == 1);
        CharacterSkillDraft sourceDraft = progression.Drafts
            .First(draft => draft.kind == CharacterSkillKind.Active && draft.unlockLevel == 1);
        Require(restoredDraft.rules.Select(rule => rule.rarity)
                .SequenceEqual(sourceDraft.rules.Select(rule => rule.rarity)),
            "Reloading rerolled a prepared candidate rarity.");
        Require(restoredDraft.rules.Select(rule => rule.ruleId)
                .SequenceEqual(sourceDraft.rules.Select(rule => rule.ruleId)),
            "Reloading changed stable skill rule identities.");
        Require(restoredDraft.candidates.Select(skill => skill.id)
                .SequenceEqual(sourceDraft.candidates.Select(skill => skill.id)),
            "Reloading regenerated prepared candidates.");
        Require(restoredDraft.candidates.Select(skill => (skill.ruleId, skill.combinationId))
                .SequenceEqual(sourceDraft.candidates.Select(skill => (skill.ruleId, skill.combinationId))),
            "Reloading changed the selected rule/combination mapping.");
        Require(restored.Actor.Progression.GrowthState.traitIds.Count == 0
            && restored.Actor.Progression.PassiveSkills.Count == 1,
            "Traits and learned passives were not persisted as separate concepts.");
        return true;
    }

    private static bool VerifyFormulaPresentationContract()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.formulaPolicy.formulaVersion =
            CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        CharacterSkillFormulaCatalogAssetBuilder.Populate(settings);
        using ActorFixture actor = new ActorFixture(7717, "검증자", "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                settingsProvider,
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:formula-presentation",
                "facility:formula-presentation",
                CharacterActivityOutcomes.Completed,
                day: 1);
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:formula-presentation-failed",
                "facility:formula-presentation",
                CharacterActivityOutcomes.Failed,
                day: 2);

            CharacterSkillDraft active = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                1);
            Require(active.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion
                    && active.presentationState
                        == CharacterSkillPresentationState.PresentationPending
                    && active.moduleSelectionOffers.Count == 3
                    && active.frozenMechanics.Count == 0
                    && active.candidates.Count == 0
                    && active.nextPresentationIndex == 0,
                "A new active draft did not expose three unresolved module-selection offers.");
            Require(active.moduleSelectionOffers.All(value => value != null
                    && value.positiveModuleIds.Count > 0
                    && value.evidenceFactIds.Count > 0
                    && value.drawbackModuleIds.Count >= 3
                    && value.qualifiedNegativeEvidenceFactIds.Count > 0
                    && value.maximumDrawbackModules == 1),
                "A negative work history did not expose both cooldown and runtime-stat drawbacks per active offer.");

            int originalPresentationIndex = active.nextPresentationIndex;
            int selfCancellingOfferIndex = active.moduleSelectionOffers.FindIndex(value =>
                value.positiveModuleIds.Contains("research", StringComparer.Ordinal)
                && value.drawbackModuleIds.Contains(
                    "character-skill:drawback:research-speed",
                    StringComparer.Ordinal));
            Require(selfCancellingOfferIndex >= 0,
                "The fixture did not expose the research same-axis conflict witness.");
            active.nextPresentationIndex = selfCancellingOfferIndex;
            NarrativeFormulaModuleSelectionRequest conflictRequest =
                CharacterSkillFormulaGeneration.BuildModuleSelectionRequest(active, settings);
            NarrativeFormulaModuleOffer researchSlowdown = conflictRequest.Offers.Single(value =>
                string.Equals(value.ModuleId,
                    "character-skill:drawback:research-speed",
                    StringComparison.Ordinal));
            Require(researchSlowdown.SemanticDescription.Contains(
                    "함께 선택 금지 이로운 기능=research",
                    StringComparison.Ordinal),
                "The prompt does not explain the research same-axis exclusion.");
            Require(!NarrativeFormulaModuleSelectionValidator.TryValidate(
                    conflictRequest,
                    new NarrativeFormulaModuleSelectionChoice(
                        conflictRequest.SelectionId,
                        new[] { "research" },
                        new[] { "character-skill:drawback:research-speed" },
                        new[] { conflictRequest.EvidenceFactIds[0] }),
                    out _,
                    out string sameAxisError)
                && sameAxisError.Contains("conflict", StringComparison.OrdinalIgnoreCase),
                "A CharacterSkill benefit and drawback on the same research axis were accepted.");
            active.nextPresentationIndex = originalPresentationIndex;

            string validJson = BuildValidModuleSelectionJson(active, "검증자");
            Require(service.TryValidateResponse(
                    active,
                    validJson,
                    out List<CharacterSkillInstance> skills,
                    out string validError)
                    && skills.Count == 1
                    && skills[0].formulaCapabilities.Count == 1
                    && skills[0].calculatedCost <= active.formulaBudget
                    && string.Equals(skills[0].modules[0].moduleId,
                        active.moduleSelectionOffers[0].positiveModuleIds[0], StringComparison.Ordinal),
                $"A valid post-selection numeric allocation was rejected: {validError}");

            CharacterSkillModuleSelectionResponseDto drawbackDto =
                BuildValidModuleSelectionDto(active, "검증자");
            CharacterSkillModuleOfferState drawbackOffer =
                active.moduleSelectionOffers[active.nextPresentationIndex];
            drawbackDto.drawbackModuleIds.Add(
                drawbackOffer.drawbackModuleIds.Single(value => string.Equals(
                    value,
                    "character-skill:drawback:work-cooldown",
                    StringComparison.Ordinal)));
            drawbackDto.evidenceFactIds = new List<string>
            {
                drawbackOffer.qualifiedNegativeEvidenceFactIds[0]
            };
            Require(service.TryValidateResponse(
                    active,
                    JsonUtility.ToJson(drawbackDto),
                    out List<CharacterSkillInstance> drawbackSkills,
                    out string drawbackError)
                    && drawbackSkills.Count == 1
                    && drawbackSkills[0].drawbackCredit is >= 1 and <= 2
                    && drawbackSkills[0].drawbackEvidenceQualified
                    && drawbackSkills[0].manualCooldownDays is >= 1 and <= 2
                    && drawbackSkills[0].drawbackId.StartsWith(
                        "character-skill:work-cooldown:+",
                        StringComparison.Ordinal),
                $"An authored work cooldown drawback was not allocated by C#: {drawbackError}");

            CharacterSkillModuleSelectionResponseDto unsupportedDrawback =
                BuildValidModuleSelectionDto(active, "검증자");
            unsupportedDrawback.drawbackModuleIds.Add(
                drawbackOffer.drawbackModuleIds[0]);
            unsupportedDrawback.evidenceFactIds = new List<string>
            {
                drawbackOffer.evidenceFactIds.First(value =>
                    !drawbackOffer.qualifiedNegativeEvidenceFactIds.Contains(value,
                        StringComparer.Ordinal))
            };
            Require(!service.TryValidateResponse(
                    active,
                    JsonUtility.ToJson(unsupportedDrawback),
                    out _,
                    out string unsupportedError)
                    && unsupportedError.Contains(
                        "qualified negative evidence", StringComparison.Ordinal),
                "A selected drawback without cited negative evidence was accepted.");

            CharacterSkillModuleSelectionResponseDto statDrawback =
                BuildValidModuleSelectionDto(active, "검증자");
            CharacterSkillModuleOfferState statOffer =
                active.moduleSelectionOffers[active.nextPresentationIndex];
            string statDrawbackId = statOffer.drawbackModuleIds.First(value =>
                !string.Equals(value,
                    "character-skill:drawback:work-cooldown",
                    StringComparison.Ordinal));
            statDrawback.drawbackModuleIds.Add(statDrawbackId);
            statDrawback.evidenceFactIds = new List<string>
            {
                statOffer.qualifiedNegativeEvidenceFactIds[0]
            };
            Require(service.TryValidateResponse(
                    active,
                    JsonUtility.ToJson(statDrawback),
                    out List<CharacterSkillInstance> statSkills,
                    out string statError)
                    && statSkills.Count == 1
                    && statSkills[0].drawbackEffect != null
                    && statSkills[0].drawbackEffect.severityUnits is >= 1 and <= 2
                    && statSkills[0].mechanicalDescription.Contains(
                        "장착 부담", StringComparison.Ordinal)
                    && AcquiredTraitBurdenTargetCatalog.IsHarmful(
                        settings.FindDrawback(statDrawbackId).effectDefinition,
                        statSkills[0].drawbackEffect.value,
                        out _),
                $"An authored runtime-stat drawback was not frozen by C#: {statError}");
            CharacterSkillDrawbackCapabilityDefinition statDefinition =
                settings.FindDrawback(statDrawbackId);
            GameplayEffectBinding statBinding = new GameplayEffectBinding
            {
                bindingId = "qa:skill-burden",
                definition = statDefinition.effectDefinition,
                value = statSkills[0].drawbackEffect.value
            };
            GameplayEffectProjectionResult statProjection =
                CharacterGameplayEffectProjector.Resolve(
                    statSkills[0].drawbackEffect.targetId,
                    1f,
                    new IGameplayEffectSource[]
                    {
                        new DebugGameplayEffectSource(
                            new GameplayEffectSourceRef(
                                GameplayEffectSourceKind.Status,
                                "qa:skill-burden"),
                            statBinding)
                    },
                    new GameplayEffectContext());
            Require(Math.Abs(statProjection.Value
                    - statSkills[0].drawbackEffect.value) < 0.000001f,
                "The frozen CharacterSkill burden value did not reach the shared gameplay-effect projector.");
            CharacterSkillInstance restoredStatSkill =
                JsonUtility.FromJson<CharacterSkillInstance>(
                    JsonUtility.ToJson(statSkills[0]));
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                restoredStatSkill,
                settings);
            Require(restoredStatSkill.drawbackEffect != null
                    && string.Equals(
                        restoredStatSkill.drawbackEffect.effectId,
                        statSkills[0].drawbackEffect.effectId,
                        StringComparison.Ordinal)
                    && Math.Abs(restoredStatSkill.drawbackEffect.value
                        - statSkills[0].drawbackEffect.value) < 0.000001f,
                "The frozen CharacterSkill burden did not survive a JSON save round trip.");

            string extraMechanicalField = validJson.Insert(
                validJson.Length - 1,
                ",\"combinationId\":\"forbidden\"");
            Require(!service.TryValidateResponse(
                    active,
                    extraMechanicalField,
                    out _,
                    out _),
                "A CharacterSkill response containing an extra mechanical field was accepted.");
            CharacterSkillModuleSelectionResponseDto staleDto = BuildValidModuleSelectionDto(active, "검증자");
            staleDto.selectionId = "selection:skill:" + new string('0', 64);
            string stalePresentation = JsonUtility.ToJson(staleDto);
            Require(!service.TryValidateResponse(
                    active,
                    stalePresentation,
                    out _,
                    out string staleError)
                    && staleError.Contains("selectionId", StringComparison.Ordinal),
                "A stale formula selectionId was accepted.");
            CharacterSkillModuleSelectionResponseDto numericDto = BuildValidModuleSelectionDto(active, "검증자");
            numericDto.narrativeFlavor = "검증자의 작업 경험에서 피해 5를 익혔다.";
            string numericPresentation = JsonUtility.ToJson(numericDto);
            Require(!service.TryValidateResponse(
                    active,
                    numericPresentation,
                    out _,
                    out _),
                "A presentation that restates a mechanical number was accepted.");
            string missingField = "{\"selectionId\":\""
                + active.moduleSelectionOffers[0].selectionId
                + "\",\"positiveModuleIds\":[\""
                + active.moduleSelectionOffers[0].positiveModuleIds[0]
                + "\"],\"drawbackModuleIds\":[],\"evidenceFactIds\":[\""
                + active.moduleSelectionOffers[0].evidenceFactIds[0]
                + "\"],\"displayName\":\"검증자 감각\"}";
            Require(!service.TryValidateResponse(active, missingField, out _, out _),
                "A CharacterSkill presentation missing narrativeFlavor was accepted.");

            CharacterSkillModuleSelectionResponseDto negativeOnly = BuildValidModuleSelectionDto(active, "검증자");
            negativeOnly.positiveModuleIds.Clear();
            Require(!service.TryValidateResponse(active, JsonUtility.ToJson(negativeOnly), out _, out _),
                "A negative-only/empty-positive module selection was accepted.");
            CharacterSkillModuleSelectionResponseDto invented = BuildValidModuleSelectionDto(active, "검증자");
            invented.positiveModuleIds[0] = "invented-module";
            Require(!service.TryValidateResponse(active, JsonUtility.ToJson(invented), out _, out _),
                "An unoffered module ID was accepted.");

            CharacterSkillDraft passive = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            Require(passive.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion
                    && passive.moduleSelectionOffers.Count == 1
                    && passive.frozenMechanics.Count == 0
                    && passive.candidates.Count == 0,
                "A new passive draft did not expose exactly one unresolved selection offer.");
            string passivePrompt = CharacterSkillPromptBuilder.Build(
                progression,
                passive,
                settings);
            Require(passivePrompt.Contains("selectionId=", StringComparison.Ordinal)
                    && passivePrompt.Contains("positiveModules:", StringComparison.Ordinal)
                    && passivePrompt.Contains(
                        "positiveModuleIds, drawbackModuleIds, evidenceFactIds",
                        StringComparison.Ordinal)
                    && passivePrompt.Contains("수치와 비용은 응답 후 C#이 계산", StringComparison.Ordinal)
                    && passivePrompt.Contains("선택적 해로운 모듈", StringComparison.Ordinal)
                    && !passivePrompt.Contains("selectedIndex", StringComparison.Ordinal),
                "The formula prompt does not expose the post-selection allocation contract.");
            Require(service.TryValidateResponse(
                    passive,
                    BuildValidModuleSelectionJson(passive, "검증자"),
                    out List<CharacterSkillInstance> passiveSkills,
                    out string passiveError)
                    && passiveSkills.Count == 1,
                $"A valid passive module selection was rejected: {passiveError}");

            string legacySelection =
                "{\"candidates\":[{\"ruleId\":\"legacy\",\"combinationId\":\"legacy\"}]}";
            Require(!service.TryValidateResponse(
                    passive,
                    legacySelection,
                    out _,
                    out _),
                "A legacy candidate-selection response was accepted for new formula generation.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyModuleValidation()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture actor = new ActorFixture(
            7717,
            "검증자",
            "human");
        try
        {
            CharacterSkillCombinationSemanticsFactory.ValidateSettingsCoverage(settings);
            CharacterSkillCandidateRule semanticsRule = new CharacterSkillCandidateRule
            {
                rarity = CharacterSkillRarity.Legendary,
                budget = 99,
                trigger = CharacterSkillTrigger.ManualCombat,
                target = CharacterSkillTarget.Enemy,
                ultimateDomain = CharacterUltimateDomain.None,
                cooldownTurns = 1,
                mechanicalPolicySource = CharacterSkillMechanicalPolicySource.AuthoredRule,
                usableFrom = OffenseFormationMask.Front | OffenseFormationMask.Middle,
                targetPositions = OffenseFormationMask.Front | OffenseFormationMask.Middle,
                allowedModuleIds = new List<string> { "conditional_amplify" },
                allowedVariantIds = new List<string> { "critical", "wounded" }
            };
            CharacterSkillDraft semanticsDraft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Active,
                requestKey = "qa:character-skill-public-semantics",
                rules = new List<CharacterSkillCandidateRule> { semanticsRule }
            };
            CharacterSkillRuleIdentity.Ensure(semanticsDraft);
            CharacterSkillAllowedCombination woundedCombination =
                CharacterSkillCombinationCatalog
                    .Build(semanticsRule, settings, semanticsDraft.kind)
                    .Single(candidate => candidate.Modules.Count == 1
                        && candidate.Modules[0].moduleId == "conditional_amplify"
                        && candidate.Modules[0].variantId == "wounded");
            CharacterSkillCombinationSemanticsDto woundedSemantics =
                CharacterSkillCombinationSemanticsFactory.Create(
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings);
            CharacterSkillModuleSemanticsDto woundedModule = woundedSemantics.modules.Single();
            string canonicalWounded = CharacterSkillCombinationSemanticsFactory
                .SerializeCanonical(woundedSemantics);
            Require(woundedModule.executionScope == "offense_battle"
                    && woundedModule.effectKind == "conditional_basic_damage"
                    && woundedModule.terms.Contains("health_ratio_subject=selected_target")
                    && woundedModule.terms.Contains("health_ratio_formula=current_health/max(1,max_health)")
                    && woundedModule.terms.Contains("comparison=less_than_or_equal")
                    && woundedModule.terms.Contains("health_ratio_threshold=0.5")
                    && woundedModule.terms.Contains("amplified_effect=separate_basic_damage")
                     && woundedModule.terms.Contains("separate_basic_damage_multiplier=0.35")
                     && woundedModule.descriptionKo.Contains("선택 대상", StringComparison.Ordinal)
                     && woundedModule.descriptionKo.Contains("50%", StringComparison.Ordinal)
                     && woundedModule.descriptionKo.Contains("비율 0.5", StringComparison.Ordinal)
                     && woundedModule.descriptionKo.Contains("이하", StringComparison.Ordinal),
                "The public wounded-amplification contract omitted or changed its actual runtime semantics: "
                + canonicalWounded);
            Require(CharacterSkillCombinationSemanticsFactory.TryValidateCanonicalJson(
                    canonicalWounded,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out string canonicalError),
                $"Exact canonical CharacterSkill semantics were rejected: {canonicalError}");

            CharacterSkillCombinationSemanticsDto missingCondition = woundedSemantics.Clone();
            missingCondition.modules[0].terms.Remove("comparison=less_than_or_equal");
            Require(!CharacterSkillCombinationSemanticsFactory.TryValidateExact(
                    missingCondition,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out _),
                "CharacterSkill semantics accepted a missing comparison condition.");

            CharacterSkillCombinationSemanticsDto reversedComparison = woundedSemantics.Clone();
            int comparisonIndex = reversedComparison.modules[0].terms.IndexOf(
                "comparison=less_than_or_equal");
            reversedComparison.modules[0].terms[comparisonIndex] = "comparison=greater_than";
            Require(!CharacterSkillCombinationSemanticsFactory.TryValidateExact(
                    reversedComparison,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out _),
                "CharacterSkill semantics accepted the opposite health comparison.");

            CharacterSkillCombinationSemanticsDto wrongThreshold = woundedSemantics.Clone();
            int thresholdIndex = wrongThreshold.modules[0].terms.IndexOf(
                "health_ratio_threshold=0.5");
            wrongThreshold.modules[0].terms[thresholdIndex] = "health_ratio_threshold=0.5001";
            Require(!CharacterSkillCombinationSemanticsFactory.TryValidateExact(
                    wrongThreshold,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out _),
                "CharacterSkill semantics accepted a runtime-mismatched health threshold.");

            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                settingsProvider,
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-validation",
                "facility:skill-validation",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(progression, CharacterSkillKind.Active, 1);
            CharacterSkillGenerationResponseDto valid = BuildValidResponse(draft, settings);
            string validJson = JsonUtility.ToJson(valid);
            Require(service.TryValidateResponse(draft, validJson, out List<CharacterSkillInstance> skills, out string validError)
                && skills.Count == 3,
                $"A valid constrained response was rejected: {validError}");
            Require(skills.Select(skill => skill.combinationId)
                    .SequenceEqual(valid.candidates.Select(candidate => candidate.combinationId))
                && skills.Select(skill => skill.ruleId)
                    .SequenceEqual(draft.rules.Select(rule => rule.ruleId)),
                "The validated response replaced the model-selected rule/combination identity.");

            CharacterSkillGenerationResponseDto reordered = BuildValidResponse(draft, settings);
            reordered.candidates.Reverse();
            Require(service.TryValidateResponse(
                    draft,
                    JsonUtility.ToJson(reordered),
                    out List<CharacterSkillInstance> reorderedSkills,
                    out string reorderedError)
                && reorderedSkills.Select(skill => skill.ruleId)
                    .SequenceEqual(draft.rules.Select(rule => rule.ruleId)),
                $"Rule identity did not preserve authored rule order independently of JSON array order: {reorderedError}");

            CharacterSkillGenerationResponseDto wrongCount = BuildValidResponse(draft, settings);
            wrongCount.candidates.RemoveAt(wrongCount.candidates.Count - 1);
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(wrongCount), out _, out _),
                "A response with the wrong candidate count was accepted.");

            string extraRootField = validJson.Insert(validJson.Length - 1, ",\"mechanics\":1");
            Require(!service.TryValidateResponse(draft, extraRootField, out _, out _),
                "A CharacterSkill response with an extra root mechanics field was accepted.");

            string candidateMarker = "\"narrativeReason\":\"";
            int candidateMarkerIndex = validJson.IndexOf(candidateMarker, StringComparison.Ordinal);
            int candidateObjectEnd = validJson.IndexOf('}', candidateMarkerIndex);
            string extraCandidateField = validJson.Insert(candidateObjectEnd, ",\"cooldownTurns\":0");
            Require(!service.TryValidateResponse(draft, extraCandidateField, out _, out _),
                "A CharacterSkill candidate with an extra mechanical field was accepted.");

            CharacterSkillDraft passiveDraft = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            Require(passiveDraft.rules.All(rule => rule.allowedModuleIds.All(moduleId =>
                    !CharacterSkillValidation.WouldSelfTrigger(moduleId, rule.trigger))),
                "A self-triggering module was offered in a passive draft.");
            string passivePrompt = CharacterSkillPromptBuilder.Build(
                progression,
                passiveDraft,
                settings);
            Require(passivePrompt.Contains("candidateCount=1")
                && passivePrompt.Contains("제공된 ruleId의 후보 하나만")
                && !passivePrompt.Contains("세 후보의"),
                "The single-passive prompt contains conflicting candidate-count instructions.");
            CharacterSkillGenerationResponseDto passive = BuildValidResponse(passiveDraft, settings);
            Require(service.TryValidateResponse(
                    passiveDraft,
                    JsonUtility.ToJson(passive),
                    out List<CharacterSkillInstance> passiveSkills,
                    out string passiveError)
                && passiveSkills.Count == 1,
                $"A valid non-ultimate management passive was rejected: {passiveError}");

            CharacterSkillAllowedCombination allowedCombination = CharacterSkillCombinationCatalog
                .Build(passiveDraft.rules[0], settings, CharacterSkillKind.Passive)
                .First();
            CharacterSkillGenerationResponseDto combinationResponse = BuildValidResponse(
                passiveDraft,
                settings);
            combinationResponse.candidates[0].combinationId = allowedCombination.Id;
            Require(service.TryValidateResponse(
                    passiveDraft,
                    JsonUtility.ToJson(combinationResponse),
                    out List<CharacterSkillInstance> combinationSkills,
                    out string combinationError)
                && combinationSkills.Count == 1
                && combinationSkills[0].modules.Count == allowedCombination.Modules.Count,
                $"A valid authored combination ID was rejected: {combinationError}");

            combinationResponse.candidates[0].combinationId = "unknown-combination";
            Require(!service.TryValidateResponse(
                    passiveDraft,
                    JsonUtility.ToJson(combinationResponse),
                    out _,
                    out _),
                "An unknown authored combination ID was accepted.");

            CharacterSkillGenerationResponseDto unknownRule = BuildValidResponse(draft, settings);
            unknownRule.candidates[0].ruleId = "skill-rule:unknown";
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(unknownRule), out _, out _),
                "An unknown rule ID was accepted.");

            CharacterSkillGenerationResponseDto repairedRule = BuildValidResponse(draft, settings);
            repairedRule.candidates[0].ruleId = " " + repairedRule.candidates[0].ruleId + " ";
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(repairedRule), out _, out _),
                "A whitespace-repaired rule ID was accepted instead of failing exact validation.");

            CharacterSkillGenerationResponseDto repairedCombination = BuildValidResponse(draft, settings);
            repairedCombination.candidates[0].combinationId =
                " " + repairedCombination.candidates[0].combinationId + " ";
            Require(!service.TryValidateResponse(
                    draft,
                    JsonUtility.ToJson(repairedCombination),
                    out _,
                    out _),
                "A whitespace-repaired combination ID was accepted instead of failing exact validation.");

            CharacterSkillGenerationResponseDto duplicateRule = BuildValidResponse(draft, settings);
            duplicateRule.candidates[1].ruleId = duplicateRule.candidates[0].ruleId;
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(duplicateRule), out _, out _),
                "A duplicated response rule ID was accepted.");

            CharacterSkillGenerationResponseDto overBudget = BuildValidResponse(draft, settings);
            draft.rules[0].budget = 0;
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(overBudget), out _, out _),
                "A response above its authored budget was accepted.");

            CharacterSkillDraft duplicateMechanicalDraft = new CharacterSkillDraft
            {
                kind = passiveDraft.kind,
                requestKey = passiveDraft.requestKey + ":mechanical-duplicate",
                rules = new List<CharacterSkillCandidateRule>
                {
                    passiveDraft.rules[0].Clone(),
                    passiveDraft.rules[0].Clone()
                }
            };
            duplicateMechanicalDraft.rules[0].ruleId = "skill-rule:duplicate-a";
            duplicateMechanicalDraft.rules[1].ruleId = "skill-rule:duplicate-b";
            CharacterSkillGenerationResponseDto duplicateMechanical = BuildValidResponse(
                duplicateMechanicalDraft,
                settings);
            CharacterSkillAllowedCombination firstRuleCombination = CharacterSkillCombinationCatalog
                .Build(duplicateMechanicalDraft.rules[0], settings, duplicateMechanicalDraft.kind)
                .First();
            CharacterSkillAllowedCombination secondRuleSameMechanical = CharacterSkillCombinationCatalog
                .Build(duplicateMechanicalDraft.rules[1], settings, duplicateMechanicalDraft.kind)
                .First(candidate => string.Equals(
                    candidate.MechanicalIdentity,
                    firstRuleCombination.MechanicalIdentity,
                    StringComparison.Ordinal));
            duplicateMechanical.candidates[0].combinationId = firstRuleCombination.Id;
            duplicateMechanical.candidates[1].combinationId = secondRuleSameMechanical.Id;
            Require(!service.TryValidateResponse(
                    duplicateMechanicalDraft,
                    JsonUtility.ToJson(duplicateMechanical),
                    out _,
                    out _),
                "Two rule-local IDs that resolve to the same mechanical combination were accepted.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyTrainingExperience()
    {
        using ActorFixture fixture = new ActorFixture(81005, "Trainee", "Slime");
        GameObject buildingObject = new GameObject("ProgressionTrainingFacility");
        try
        {
            Facility facility = buildingObject.AddComponent<Facility>();
            BuildingTrainingAbility training = new BuildingTrainingAbility
            {
                experienceAmount = 24,
                moodAmount = 0f
            };
            training.ApplyUseCompleted(fixture.Actor, facility);
            Require(fixture.Actor.Progression.Level == 2
                    && fixture.Actor.Progression.CurrentExperience == 4,
                "Training did not award its configured experience.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(buildingObject);
        }

        return true;
    }

    private static bool VerifyRetryAndRequestKeyResume()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 0f;
        settings.maximumRetrySeconds = 0f;
        const string PersistentId = "character:qa-skill-retry";
        using ActorFixture sourceActor = new ActorFixture(
            991,
            "재시도 원본",
            "human",
            persistentId: PersistentId);
        using ActorFixture restoredActor = new ActorFixture(
            993,
            "재시도 복원",
            "human",
            persistentId: PersistentId);
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            SequencedLlmRuntime runtime = new SequencedLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression source = sourceActor.Actor.Progression;
            source.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-retry",
                "facility:skill-retry",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(source, CharacterSkillKind.Active, 1, revision: 4);
            string requestKey = draft.requestKey;
            runtime.Enqueue(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                "{}",
                string.Empty,
                string.Empty));
            service.RequestDraft(source, draft);
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 1 && !draft.isReady,
                "A schema-rejected generation request did not remain pending.");

            CharacterGrowthState restoredGrowth = source.GrowthState.Clone();
            restoredGrowth.drafts.Clear();
            restoredGrowth.drafts.Add(draft.Clone());
            restoredGrowth.passiveSkills.Add(new CharacterSkillInstance
            {
                id = "qa:restored-passive",
                displayName = "복원 패시브",
                description = "재개 요청 격리용 패시브",
                narrativeReason = "이미 검증됨",
                kind = CharacterSkillKind.Passive,
                rarity = CharacterSkillRarity.Advanced,
                trigger = CharacterSkillTrigger.WorkCompleted,
                target = CharacterSkillTarget.Self,
                modules = new List<CharacterSkillModuleSelection>
                {
                    new CharacterSkillModuleSelection
                    {
                        moduleId = "work_speed",
                        variantId = "small"
                    }
                }
            });
            restoredGrowth.pendingRequestKeys.Clear();
            restoredGrowth.pendingRequestKeys.Add(requestKey);
            CharacterNarrativeLedger restoredLedger =
                source.NarrativeLedger.Clone();
            service.CancelRequests(source);

            CharacterProgression restored = restoredActor.Actor.Progression;
            restored.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            restored.RestorePersistentState(new CharacterProgressionSnapshot(
                1,
                0,
                restoredGrowth,
                restoredLedger));
            CharacterSkillDraft restoredDraft = restored.Drafts.First(item => item.requestKey == requestKey);
            int expectedCalls = runtime.CharacterSkillCallCount;
            while (!restoredDraft.isReady)
            {
                runtime.Enqueue(new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    BuildValidFormulaPresentationJson(
                        restoredDraft,
                        restored.GrowthState.displayName),
                    string.Empty,
                    string.Empty));
                service.Tick();
                expectedCalls++;
                Require(runtime.CharacterSkillCallCount == expectedCalls,
                    "A restored formula presentation slot was not submitted exactly once.");
            }
            Require(restoredDraft.isReady && restoredDraft.requestKey == requestKey,
                "The restored request changed key or failed to commit its prepared result.");
            Require(restoredDraft.candidates.Count == 3
                    && restoredDraft.nextPresentationIndex == 3,
                "The restored active draft did not complete all three frozen presentations.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyInFlightGenerationTimeout()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 1f;
        settings.maximumRetrySeconds = 2f;
        using ActorFixture actor = new ActorFixture(
            992,
            "시간 검증자",
            "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            MutableUiClock clock = new MutableUiClock();
            NeverCompletingLlmRuntime runtime = new NeverCompletingLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                clock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-timeout",
                "facility:skill-timeout",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            service.RequestDraft(progression, draft);
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                service.Tick();
                Require(runtime.CharacterSkillCallCount == attempt,
                    "A timed-out formula presentation attempt was not submitted exactly once.");
                clock.Advance(service.RequestTimeoutSeconds + 0.1f);
                service.Tick();
                Require(draft.presentationFailureCount == attempt,
                    "A timed-out formula presentation did not increment its visible failure count.");
                if (attempt < 5)
                {
                    Require(service.PendingRequestCount == 1
                            && !draft.isReady
                            && draft.candidates.Count == 0,
                        "A timeout applied mechanics or discarded the retryable presentation early.");
                    clock.Advance(
                        service.ProviderCircuitCooldownRemainingSeconds + 0.1f);
                }
            }
            Require(service.PendingRequestCount == 0
                    && !draft.isReady
                    && draft.candidates.Count == 0
                    && draft.presentationState
                        == CharacterSkillPresentationState.AwaitingNarrativeRetry
                    && !draft.requestSubmitted,
                "Five timeouts did not persist an unapplied AwaitingNarrativeRetry draft.");
            Require(service.ProviderCircuitTripCount == 5
                    && service.LastDiagnostic.Contains(
                        "accepted-request-timeout",
                        StringComparison.Ordinal),
                "Timed-out generation did not preserve its explicit failure diagnostic.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyProviderCircuitDrainsQueuedRequests()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 1f;
        settings.maximumRetrySeconds = 2f;
        List<ActorFixture> actors = new List<ActorFixture>();
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            MutableUiClock clock = new MutableUiClock();
            DelayedSkillLlmRuntime runtime = new DelayedSkillLlmRuntime();
            CharacterSkillGenerationService service =
                new CharacterSkillGenerationService(
                    settingsProvider,
                    new TestLlmRuntimeProvider(runtime),
                    clock);
            List<CharacterProgression> progressions =
                new List<CharacterProgression>();
            for (int index = 0; index < 3; index++)
            {
                ActorFixture actor = new ActorFixture(
                    12000 + index,
                    "Circuit verifier " + index,
                    "human");
                actors.Add(actor);
                CharacterProgression progression = actor.Actor.Progression;
                progression.ConstructCharacterProgression(
                    service,
                    settingsProvider,
                    gameEventBus: new GameEventBus(),
                    profileProjector: new CharacterProgressionProfileProjector(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()),
                        new CharacterRuntimeProfileFactory(
                            new ResourceGameContentCatalog(
                                new UnityGameContentRootLoader()))));
                progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    "work:provider-circuit-" + index,
                    "facility:provider-circuit",
                    CharacterActivityOutcomes.Completed,
                    day: 1);
                progression.ApplyPreparedIdentity(
                    "Circuit verifier " + index,
                    "deterministic provider-health test",
                    Array.Empty<int>(),
                    CharacterPotentialGrade.Ordinary,
                    12000 + index,
                    autoChooseDrafts: true);
                progressions.Add(progression);
            }

            int queuedCount = service.PendingRequestCount;
            Require(queuedCount > 2,
                "The provider circuit fixture did not exceed concurrent capacity.");
            service.Tick();
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 2
                    && runtime.PendingCallbackCount == 2
                    && service.PendingRequestCount == queuedCount,
                "The fixture did not retain queued work behind two accepted requests.");

            clock.Advance(service.RequestTimeoutSeconds + 0.1f);
            service.Tick();
            Require(service.PendingRequestCount == queuedCount,
                "A provider timeout discarded queued formula presentation requests.");
            Require(progressions.SelectMany(value => value.Drafts)
                    .All(draft => draft != null
                        && !draft.isReady
                        && draft.candidates.Count == 0),
                "The provider circuit applied hidden fallback mechanics.");
            Require(service.IsProviderCircuitOpen
                    && service.ProviderCircuitTripCount == 1
                    && service.ProviderCircuitCooldownRemainingSeconds > 0f,
                "The provider timeout did not open a visible bounded circuit.");
            Require(service.LastDiagnostic.Contains(
                    "accepted-request-timeout",
                    StringComparison.Ordinal),
                "The provider circuit diagnostic did not retain the timeout cause.");

            ActorFixture cooldownActor = new ActorFixture(
                12003,
                "Circuit cooldown verifier",
                "human");
            actors.Add(cooldownActor);
            CharacterProgression cooldownProgression =
                cooldownActor.Actor.Progression;
            cooldownProgression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            cooldownProgression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:provider-circuit-cooldown",
                "facility:provider-circuit",
                CharacterActivityOutcomes.Completed,
                day: 1);
            cooldownProgression.ApplyPreparedIdentity(
                "Circuit cooldown verifier",
                "deterministic provider-health test",
                Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                12003,
                autoChooseDrafts: true);
            int cooldownQueued = service.PendingRequestCount;
            Require(cooldownQueued > queuedCount,
                "The cooldown fixture did not enqueue generation work.");
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 2
                    && service.PendingRequestCount == cooldownQueued
                    && cooldownProgression.Drafts.All(
                        draft => draft != null
                            && !draft.isReady
                            && draft.candidates.Count == 0),
                "An open provider circuit submitted work or applied a hidden fallback.");
            progressions.Add(cooldownProgression);

            runtime.CompleteAcceptedCallbacks(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                "{}",
                string.Empty,
                string.Empty));
            service.Tick();
            Require(runtime.DeliveredCallbackCount == 2
                    && service.PendingRequestCount == cooldownQueued
                    && progressions.SelectMany(value => value.Drafts)
                        .All(draft => draft != null
                            && !draft.isReady
                            && draft.candidates.Count == 0),
                "A late provider callback committed mechanics or discarded queued work.");
            return true;
        }
        finally
        {
            foreach (ActorFixture actor in actors)
            {
                actor.Dispose();
            }
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static CharacterSkillCandidateRule CreateManagementReachabilityRule(
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        CharacterSkillTarget target,
        CharacterUltimateDomain ultimateDomain,
        string requestKey,
        IEnumerable<string> restrictedModuleIds = null)
    {
        HashSet<string> restriction = restrictedModuleIds == null
            ? null
            : new HashSet<string>(restrictedModuleIds, StringComparer.Ordinal);
        List<CharacterSkillModuleRule> modules = settings.Modules
            .OfType<CharacterManagementSkillModuleRule>()
            .Where(value => restriction == null || restriction.Contains(value.id))
            .Where(value => !CharacterSkillValidation.WouldSelfTrigger(value.id, trigger))
            .Cast<CharacterSkillModuleRule>()
            .ToList();
        CharacterSkillCandidateRule rule = new CharacterSkillCandidateRule
        {
            rarity = CharacterSkillRarity.Legendary,
            budget = 99,
            trigger = trigger,
            target = target,
            ultimateDomain = ultimateDomain,
            cooldownTurns = 0,
            mechanicalPolicySource = CharacterSkillMechanicalPolicySource.AuthoredRule,
            allowedModuleIds = modules
                .Select(value => value.id)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            allowedVariantIds = modules
                .SelectMany(value => value.variants ?? new List<CharacterSkillNumericVariant>())
                .Where(value => value != null)
                .Select(value => value.id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList()
        };
        CharacterSkillDraft identityDraft = new CharacterSkillDraft
        {
            kind = kind,
            requestKey = requestKey,
            requestedUltimateDomain = ultimateDomain,
            rules = new List<CharacterSkillCandidateRule> { rule }
        };
        CharacterSkillRuleIdentity.Ensure(identityDraft);
        return rule;
    }

    private static void VerifyManagementReachabilityRuntimePaths()
    {
        using ActorFixture fixture = new ActorFixture(
            81018,
            "ReachabilityRuntime",
            "Slime");
        CharacterActor actor = fixture.Actor;
        CharacterGrowthState growth = actor.Progression.GrowthState;
        growth.passiveSkills.Clear();
        actor.Stats.Stats[CharacterCondition.HUNGER] = 0f;
        actor.Stats.Stats[CharacterCondition.SLEEP] = 80f;
        actor.Stats.Stats[CharacterCondition.FUN] = 80f;
        actor.Stats.Stats[CharacterCondition.EXCRETION] = 80f;
        actor.Stats.Stats[CharacterCondition.HYGIENE] = 50f;
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-reachability-work-started",
            combinationId = "qa-reachability-work-started-combination",
            displayName = "작업 시작 경로",
            description = "작업 시작 이벤트와 조회 경로 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.WorkStarted,
            target = CharacterSkillTarget.Self,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("cleaning", "small"),
                Module("needs", "small"),
                Module("mood", "small"),
                Module("work_speed", "small")
            }
        });
        float moodBefore = actor.Stats.Mood;
        CharacterSkillRuntimeEffects.BeginWork(
            actor,
            null,
            BuiltInWorkTypeIds.Operate,
            "qa-reachability-work-started-event");
        Require(Mathf.Approximately(
                actor.Stats.GetConditionValue(CharacterCondition.HYGIENE),
                55f)
            && Mathf.Approximately(
                actor.Stats.GetConditionValue(CharacterCondition.HUNGER),
                5f)
            && actor.Stats.Mood > moodBefore
            && Mathf.Approximately(
                CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor),
                1.1f),
            "WorkStarted did not reach cleaning/needs/mood event effects and work-speed query effect.");
        CharacterSkillRuntimeEffects.EndWork(actor);

        CharacterSkillTrigger[] immediateTriggers =
        {
            CharacterSkillTrigger.NeedChanged,
            CharacterSkillTrigger.MoodChanged,
            CharacterSkillTrigger.RelationshipChanged,
            CharacterSkillTrigger.OperatingDayStarted
        };
        foreach (CharacterSkillTrigger trigger in immediateTriggers)
        {
            growth.passiveSkills.Clear();
            growth.passiveSkills.Add(new CharacterSkillInstance
            {
                id = "qa-reachability-event-" + trigger,
                combinationId = "qa-reachability-event-combination-" + trigger,
                displayName = "관리 이벤트 경로",
                description = "관리 이벤트 즉시 효과 검증",
                kind = CharacterSkillKind.Passive,
                trigger = trigger,
                target = CharacterSkillTarget.Self,
                modules = new List<CharacterSkillModuleSelection>
                {
                    Module("cleaning", "small")
                }
            });
            actor.Stats.Stats[CharacterCondition.HYGIENE] = 10f;
            CharacterSkillRuntimeEffects.ApplyTriggeredPassives(actor, trigger);
            Require(Mathf.Approximately(
                    actor.Stats.GetConditionValue(CharacterCondition.HYGIENE),
                    15f),
                $"{trigger} did not reach the retained cleaning event effect.");
        }

        growth.passiveSkills.Clear();
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-unreachable-cross-trigger",
            combinationId = "qa-unreachable-cross-trigger-combination",
            displayName = "교차 발동 차단",
            description = "다른 발동의 조회 효과 차단 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.WorkStarted,
            target = CharacterSkillTarget.Self,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("output", "small"),
                Module("stock", "small")
            }
        });
        CharacterSkillRuntimeEffects.BeginWork(
            actor,
            null,
            BuiltInWorkTypeIds.Operate,
            "qa-unreachable-cross-trigger-event");
        Require(Mathf.Approximately(
                CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(actor),
                1f)
            && CharacterSkillRuntimeEffects.GetStockProductionBonus(actor) == 0,
            "WorkStarted leaked WorkCompleted-only output or stock query effects.");
        CharacterSkillRuntimeEffects.EndWork(actor);
    }

    private static void VerifyInstalledLegacyManagementPassivePreservation()
    {
        const string LegacyCombinationId =
            "skill-combination:qa-legacy-work-started-output";
        using ActorFixture source = new ActorFixture(
            81019,
            "LegacySkillSource",
            "Slime");
        source.Actor.Progression.GrowthState.passiveSkills.Clear();
        CharacterSkillInstance original = new CharacterSkillInstance
        {
            id = "qa-legacy-work-started-output",
            ruleId = "skill-rule:qa-legacy-work-started-output",
            combinationId = LegacyCombinationId,
            displayName = "이전 작업 시작 기술",
            description = "이전 저장에서 유지되는 기술",
            narrativeReason = "이전 승인 결과를 그대로 보존한다.",
            kind = CharacterSkillKind.Passive,
            rarity = CharacterSkillRarity.Advanced,
            trigger = CharacterSkillTrigger.WorkStarted,
            target = CharacterSkillTarget.Self,
            ultimateDomain = CharacterUltimateDomain.None,
            cooldownTurns = 3,
            usableFrom = OffenseFormationMask.Front | OffenseFormationMask.Middle,
            targetPositions = OffenseFormationMask.Rear,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("output", "small")
            },
            requestKey = "qa:legacy:work-started-output",
            narrativeTrace = new NarrativeGenerationTrace
            {
                schemaId = "qa:legacy:skill",
                schemaVersion = 17,
                schemaHash = "sha256:qa-legacy-skill",
                cultureStyleId = "qa:legacy:culture",
                usedMotifIds = new[] { "motif:legacy:a", "motif:legacy:b" },
                usedCharacterFactIds = new[] { "fact:legacy:a" },
                verdict = NarrativeQualityVerdict.SoftPass,
                retryCount = 2,
                usedFallback = false
            }
        };
        source.Actor.Progression.GrowthState.passiveSkills.Add(original);
        string exactBefore = JsonUtility.ToJson(original);
        CharacterProgressionSnapshot snapshot =
            source.Actor.Progression.CapturePersistentState();

        using ActorFixture restored = new ActorFixture(
            81020,
            "LegacySkillRestored",
            "Slime");
        restored.Actor.Progression.RestorePersistentState(snapshot);
        CharacterSkillInstance legacy = restored.Actor.Progression.PassiveSkills.Single();
        string exactAfterRestore = JsonUtility.ToJson(legacy);
        CharacterProgressionSnapshot recaptured =
            restored.Actor.Progression.CapturePersistentState();
        string exactAfterRecapture = JsonUtility.ToJson(
            recaptured.GrowthState.passiveSkills.Single());
        Require(string.Equals(exactAfterRestore, exactBefore, StringComparison.Ordinal)
                && string.Equals(exactAfterRecapture, exactBefore, StringComparison.Ordinal),
            "An installed legacy management passive was deleted, replaced, or rewritten on restore.");
        Require(Mathf.Approximately(
                CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(restored.Actor),
                1f),
            "An installed legacy WorkStarted/output skill gained a new invented effect.");
    }

    [Serializable]
    private sealed class SkillPresentationFixtureDto
    {
        public string presentationId = string.Empty;
        public string displayName = string.Empty;
        public string narrativeFlavor = string.Empty;
    }

    private static string BuildValidFormulaPresentationJson(
        CharacterSkillDraft draft,
        string characterName)
    {
        Require(draft != null
                && draft.frozenMechanics != null
                && draft.nextPresentationIndex >= 0
                && draft.nextPresentationIndex < draft.frozenMechanics.Count,
            "The formula presentation fixture has no pending frozen mechanic.");
        CharacterSkillInstance frozen =
            draft.frozenMechanics[draft.nextPresentationIndex];
        return JsonUtility.ToJson(new SkillPresentationFixtureDto
        {
            presentationId = frozen.presentationId,
            displayName = characterName + " 감각",
            narrativeFlavor = characterName + "의 반복된 경험에서 익힌 감각이다."
        });
    }

    private static string BuildValidModuleSelectionJson(
        CharacterSkillDraft draft,
        string characterName) =>
        JsonUtility.ToJson(BuildValidModuleSelectionDto(draft, characterName));

    private static CharacterSkillModuleSelectionResponseDto BuildValidModuleSelectionDto(
        CharacterSkillDraft draft,
        string characterName)
    {
        Require(draft != null
                && draft.moduleSelectionOffers != null
                && draft.nextPresentationIndex >= 0
                && draft.nextPresentationIndex < draft.moduleSelectionOffers.Count,
            "The formula fixture has no pending module-selection offer.");
        CharacterSkillModuleOfferState offer =
            draft.moduleSelectionOffers[draft.nextPresentationIndex];
        return new CharacterSkillModuleSelectionResponseDto
        {
            selectionId = offer.selectionId,
            positiveModuleIds = new List<string> { offer.positiveModuleIds[0] },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { offer.evidenceFactIds[0] },
            displayName = characterName + " 감각",
            narrativeFlavor = characterName + "의 반복된 경험에서 익힌 감각이다."
        };
    }

    private static CharacterSkillGenerationResponseDto BuildValidResponse(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        string characterName = "검증자")
    {
        CharacterSkillRuleIdentity.Ensure(draft);
        CharacterSkillGenerationResponseDto response = new CharacterSkillGenerationResponseDto();
        HashSet<string> usedMechanicalIdentities = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < draft.rules.Count; i++)
        {
            CharacterSkillCandidateRule rule = draft.rules[i];
            List<CharacterSkillAllowedCombination> combinations = CharacterSkillCombinationCatalog
                .Build(rule, settings, draft.kind);
            CharacterSkillAllowedCombination selected = combinations
                .FirstOrDefault(candidate => usedMechanicalIdentities.Add(candidate.MechanicalIdentity))
                ?? combinations.First();
            response.candidates.Add(new CharacterSkillCandidateResponseDto
            {
                ruleId = rule.ruleId,
                combinationId = selected.Id,
                displayName = $"{characterName} 기술 {i + 1}",
                description = "검증된 효과를 적용한다.",
                narrativeReason = "반복된 경험에서 익혔다."
            });
        }

        return response;
    }

    private static bool ChooseActive(CharacterProgression progression, int level, int index)
    {
        CharacterSkillDraft draft = progression.Drafts.FirstOrDefault(candidate => candidate != null
            && candidate.kind == CharacterSkillKind.Active
            && candidate.unlockLevel == level
            && candidate.isReady);
        return draft != null
            && progression.TryChooseActiveSkill(level, Mathf.Clamp(index, 0, draft.candidates.Count - 1), true, out _);
    }

    private static int GetExperienceToReach(int targetLevel)
    {
        return GetExperienceBetween(1, targetLevel);
    }

    private static int GetExperienceBetween(int currentLevel, int targetLevel)
    {
        int total = 0;
        for (int level = Mathf.Max(1, currentLevel); level < Mathf.Min(CharacterProgression.MaxLevel, targetLevel); level++)
        {
            total += CharacterProgression.GetExperienceRequired(level);
        }

        return total;
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add($"{name}: returned false");
            }
        }
        catch (Exception exception)
        {
            errors.Add($"{name}: {exception.Message}\n{exception.StackTrace}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string DescribeDraftState(CharacterProgression progression)
    {
        return progression == null
            ? "progression=null"
            : $"initialized={progression.GrowthState.initialized}; "
                + $"level={progression.Level}; active={progression.ActiveSkills.Count}; "
                + $"passive={progression.PassiveSkills.Count}; drafts="
                + string.Join(",", progression.Drafts.Select(draft => draft == null
                    ? "null"
                    : $"{draft.kind}@{draft.unlockLevel}:ready={draft.isReady}:candidates={draft.candidates?.Count ?? 0}:chosen={draft.permanentlyChosen}"));
    }

    private sealed class AcquiredTraitDefinitionSource :
        IGameContentDefinitionSource
    {
        private readonly CharacterAcquiredTraitSettingsSO settings;
        private readonly CharacterAcquiredTraitModuleSO[] modules;

        public AcquiredTraitDefinitionSource(
            CharacterAcquiredTraitSettingsSO settings,
            IEnumerable<CharacterAcquiredTraitModuleSO> modules)
        {
            this.settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            this.modules = (modules
                    ?? throw new ArgumentNullException(nameof(modules)))
                .Where(value => value != null)
                .ToArray();
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(CharacterAcquiredTraitSettingsSO))
            {
                return new[] { settings }.Cast<T>().ToArray();
            }
            if (typeof(T) == typeof(CharacterAcquiredTraitModuleSO))
            {
                return modules.Cast<T>().ToArray();
            }
            return Array.Empty<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            if (values.Count != 1)
            {
                throw new InvalidOperationException(
                    $"QA content expected one {typeof(T).Name}, found {values.Count}.");
            }
            return values[0];
        }
    }

    private static bool VerifyManagementPassiveReachability()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            (CharacterSkillTrigger Trigger, string[] ExpectedModules)[] cases =
            {
                (CharacterSkillTrigger.WorkStarted,
                    new[] { "cleaning", "mood", "needs", "work_speed" }),
                (CharacterSkillTrigger.WorkCompleted,
                    new[]
                    {
                        "cleaning", "mood", "needs", "output", "repair", "research",
                        "revenue", "stock", "work_speed"
                    }),
                (CharacterSkillTrigger.NeedChanged,
                    new[] { "cleaning", "mood" }),
                (CharacterSkillTrigger.MoodChanged,
                    new[] { "cleaning", "needs" }),
                (CharacterSkillTrigger.RelationshipChanged,
                    new[] { "cleaning", "mood", "needs" }),
                (CharacterSkillTrigger.OperatingDayStarted,
                    new[] { "cleaning", "mood", "needs" })
            };

            foreach ((CharacterSkillTrigger trigger, string[] expectedModules) in cases)
            {
                CharacterSkillCandidateRule rule = CreateManagementReachabilityRule(
                    settings,
                    CharacterSkillKind.Passive,
                    trigger,
                    CharacterSkillTarget.Self,
                    CharacterUltimateDomain.None,
                    $"qa:management-reachability:{trigger}");
                List<CharacterSkillAllowedCombination> combinations =
                    CharacterSkillCombinationCatalog.Build(
                        rule,
                        settings,
                        CharacterSkillKind.Passive,
                        maximumCount: 4096);
                string[] actualModules = combinations
                    .SelectMany(value => value.Modules)
                    .Select(value => value.moduleId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string[] expected = expectedModules
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                Require(actualModules.SequenceEqual(expected, StringComparer.Ordinal),
                    $"Management passive reachability mismatch for {trigger}: "
                    + $"expected={string.Join(",", expected)}; "
                    + $"actual={string.Join(",", actualModules)}");
                Require(combinations.Count > 0,
                    $"Management passive reachability removed every {trigger} combination.");

                foreach (string expectedModule in expected)
                {
                    CharacterSkillCandidateRule moduleRule =
                        CreateManagementReachabilityRule(
                            settings,
                            CharacterSkillKind.Passive,
                            trigger,
                            CharacterSkillTarget.Self,
                            CharacterUltimateDomain.None,
                            $"qa:management-reachability:{trigger}:{expectedModule}",
                            new[] { expectedModule });
                    CharacterSkillAllowedCombination combination =
                        CharacterSkillCombinationCatalog.Build(
                                moduleRule,
                                settings,
                                CharacterSkillKind.Passive)
                            .FirstOrDefault();
                    Require(combination != null,
                        $"Reachable {trigger}/{expectedModule} produced no legal combination.");
                    CharacterSkillCombinationSemanticsDto semantics =
                        CharacterSkillCombinationSemanticsFactory.Create(
                            combination,
                            moduleRule,
                            CharacterSkillKind.Passive,
                            settings);
                    foreach (IGrouping<string, CharacterSkillModuleSemanticsDto> module in
                             semantics.modules.GroupBy(
                                 value => value.moduleId + "|" + value.variantId,
                                 StringComparer.Ordinal))
                    {
                        Require(module.Any(value => !string.Equals(
                                value.effectKind,
                                "no_op",
                                StringComparison.Ordinal)),
                            $"Legal {trigger} combination '{combination.Id}' contains "
                            + $"permanently inactive module '{module.Key}'.");
                    }
                }
            }

            CharacterSkillCandidateRule ultimateRule = CreateManagementReachabilityRule(
                settings,
                CharacterSkillKind.Ultimate,
                CharacterSkillTrigger.OperatingDayStarted,
                CharacterSkillTarget.Dungeon,
                CharacterUltimateDomain.Management,
                "qa:management-reachability:ultimate");
            string[] ultimateModules = CharacterSkillCombinationCatalog.Build(
                    ultimateRule,
                    settings,
                    CharacterSkillKind.Ultimate,
                    maximumCount: 4096)
                .SelectMany(value => value.Modules)
                .Select(value => value.moduleId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] allManagementModules = settings.Modules
                .OfType<CharacterManagementSkillModuleRule>()
                .Select(value => value.id)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Require(ultimateModules.SequenceEqual(allManagementModules, StringComparer.Ordinal),
                "The reachability correction changed management-ultimate module coverage.");

            CharacterSkillCandidateRule validRule = CreateManagementReachabilityRule(
                settings,
                CharacterSkillKind.Passive,
                CharacterSkillTrigger.WorkCompleted,
                CharacterSkillTarget.Self,
                CharacterUltimateDomain.None,
                "qa:management-reachability:valid-id");
            CharacterSkillDraft validDraft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Passive,
                requestKey = "qa:management-reachability:valid-id",
                rules = new List<CharacterSkillCandidateRule> { validRule }
            };
            CharacterSkillRuleIdentity.Ensure(validDraft);
            CharacterSkillAllowedCombination selected = CharacterSkillCombinationCatalog.Build(
                    validRule,
                    settings,
                    validDraft.kind)
                .First();
            CharacterSkillGenerationResponseDto validResponse = new CharacterSkillGenerationResponseDto
            {
                candidates = new List<CharacterSkillCandidateResponseDto>
                {
                    new CharacterSkillCandidateResponseDto
                    {
                        ruleId = validRule.ruleId,
                        combinationId = selected.Id,
                        displayName = "검증자의 운영 감각",
                        description = "검증된 관리 효과를 적용한다.",
                        narrativeReason = "검증자의 반복된 작업 기록에서 익혔다."
                    }
                }
            };
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                new TestSettingsProvider(settings),
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            Require(!service.TryValidateResponse(
                    validDraft,
                    JsonUtility.ToJson(validResponse),
                    out _,
                    out string validError)
                && validError.Contains("legacy candidate selection", StringComparison.Ordinal),
                "A legacy LLM-selected combination was accepted as a new formula skill: "
                    + validError);

            CharacterSkillCandidateRule starvedRule = CreateManagementReachabilityRule(
                settings,
                CharacterSkillKind.Passive,
                CharacterSkillTrigger.NeedChanged,
                CharacterSkillTarget.Self,
                CharacterUltimateDomain.None,
                "qa:management-reachability:starved",
                new[] { "stock" });
            CharacterSkillDraft starvedDraft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Passive,
                requestKey = "qa:management-reachability:starved",
                rules = new List<CharacterSkillCandidateRule> { starvedRule }
            };
            CharacterSkillRuleIdentity.Ensure(starvedDraft);
            Require(CharacterSkillCombinationCatalog.Build(
                    starvedRule,
                    settings,
                    starvedDraft.kind).Count == 0,
                "A permanently inactive NeedChanged/stock combination remained legal.");

            SequencedLlmRuntime starvationRuntime = new SequencedLlmRuntime();
            CharacterSkillGenerationService starvationService =
                new CharacterSkillGenerationService(
                    new TestSettingsProvider(settings),
                    new TestLlmRuntimeProvider(starvationRuntime),
                    CharacterAiEditorTestDependencies.UiClock);
            using (ActorFixture starvationActor = new ActorFixture(
                       81017,
                       "StarvedRequest",
                       "Slime"))
            {
                InvalidOperationException starvation = null;
                try
                {
                    starvationService.RequestDraft(
                        starvationActor.Actor.Progression,
                        starvedDraft);
                }
                catch (InvalidOperationException exception)
                {
                    starvation = exception;
                }
                starvationService.Tick();
                Require(starvation != null
                        && starvation.Message.Contains(
                            "frozen formula mechanics",
                            StringComparison.Ordinal)
                        && starvationRuntime.CharacterSkillCallCount == 0
                        && starvationService.PendingRequestCount == 0,
                    "A legacy zero-combination draft was submitted instead of failing closed.");
            }

            string staleMechanicalIdentity = string.Join("|",
                starvedDraft.kind,
                starvedRule.rarity,
                starvedRule.trigger,
                starvedRule.target,
                starvedRule.ultimateDomain,
                starvedRule.cooldownTurns,
                "stock|small");
            string staleCombinationId = "skill-combination:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    starvedRule.ruleId + "|" + staleMechanicalIdentity);
            CharacterSkillGenerationResponseDto staleResponse =
                new CharacterSkillGenerationResponseDto
                {
                    candidates = new List<CharacterSkillCandidateResponseDto>
                    {
                        new CharacterSkillCandidateResponseDto
                        {
                            ruleId = starvedRule.ruleId,
                            combinationId = staleCombinationId,
                            displayName = "검증자의 낡은 선택",
                            description = "이전 후보 식별자를 그대로 반환한다.",
                            narrativeReason = "검증자의 오래된 요청 결과다."
                        }
                    }
                };
            Require(!service.TryValidateResponse(
                    starvedDraft,
                    JsonUtility.ToJson(staleResponse),
                    out _,
                    out string staleError)
                && staleError.Contains("legacy candidate selection", StringComparison.Ordinal),
                "A stale formerly-enumerable combination ID bypassed the formula-only gate.");

            VerifyManagementReachabilityRuntimePaths();
            VerifyInstalledLegacyManagementPassivePreservation();
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyEmptyLedgerSkillPublicContextFallback()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture actor = new ActorFixture(
            91771,
            "빈 원장 검증자",
            "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            SequencedLlmRuntime runtime = new SequencedLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));
            Require(progression.NarrativeLedger.facts.Count == 0,
                "Empty-ledger fail-closed fixture unexpectedly contains narrative facts.");

            InvalidOperationException failure = null;
            try
            {
                CharacterSkillDraft draft = service.CreateDraft(
                    progression,
                    CharacterSkillKind.Active,
                    1);
                service.RequestDraft(progression, draft);
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            Require(failure != null
                    && runtime.CharacterSkillCallCount == 0
                    && service.PendingRequestCount == 0
                    && progression.ActiveSkills.Count == 0,
                "CharacterSkill invented or submitted mechanics without narrative evidence.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private sealed class FixedGameCalendar : IGameCalendar
    {
        private int day;
        private int hour;

        public FixedGameCalendar(int day, int hour)
        {
            SetDateTime(day, hour);
        }

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => hour / 24f * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private sealed class FakeReversibleCarriedDisposition :
        IReversibleCarriedPhysicalItemBatchDispositionService
    {
        public bool AllowCommit { get; set; } = true;
        public bool AllowAcknowledge { get; set; } = true;
        public bool AllowRollback { get; set; } = true;
        public int BeginCount { get; private set; }
        public int AcknowledgeCount { get; private set; }
        public int RollbackCount { get; private set; }

        public bool TryCommitCarriedSinkReversible(
            string stackId,
            int quantity,
            string operationId,
            string reasonCode,
            out IReversiblePhysicalItemDisposition transaction,
            out string failureReason)
        {
            BeginCount++;
            bool exact = string.Equals(
                    stackId,
                    "carried:qa:memory-erasure",
                    StringComparison.Ordinal)
                && quantity == MemoryErasureSealItemRules.UseQuantity
                && !string.IsNullOrWhiteSpace(operationId)
                && string.Equals(
                    reasonCode,
                    "memory-erasure-seal-consume",
                    StringComparison.Ordinal);
            if (!AllowCommit || !exact)
            {
                transaction = null;
                failureReason = exact
                    ? "qa-physical-commit-rejected"
                    : "qa-physical-request-mismatch";
                return false;
            }

            transaction = new FakeReversibleDisposition(this);
            failureReason = string.Empty;
            return true;
        }

        private sealed class FakeReversibleDisposition :
            IReversiblePhysicalItemDisposition
        {
            private readonly FakeReversibleCarriedDisposition owner;
            private bool terminal;

            public FakeReversibleDisposition(
                FakeReversibleCarriedDisposition owner)
            {
                this.owner = owner;
            }

            public PhysicalItemBatchDispositionReceipt Receipt => default;

            public bool TryRollback(out string failureReason)
            {
                owner.RollbackCount++;
                if (terminal || !owner.AllowRollback)
                {
                    failureReason = terminal
                        ? "qa-physical-already-terminal"
                        : "qa-physical-rollback-rejected";
                    return false;
                }
                terminal = true;
                failureReason = string.Empty;
                return true;
            }

            public bool TryAcknowledge(out string failureReason)
            {
                owner.AcknowledgeCount++;
                if (terminal || !owner.AllowAcknowledge)
                {
                    failureReason = terminal
                        ? "qa-physical-already-terminal"
                        : "qa-physical-acknowledge-rejected";
                    return false;
                }
                terminal = true;
                failureReason = string.Empty;
                return true;
            }
        }
    }

    private sealed class FakeRoomLayoutCache : IRoomLayoutCache
    {
        private readonly RoomLayout layout;

        public FakeRoomLayoutCache(params RoomInstance[] rooms)
        {
            layout = new RoomLayout(rooms ?? Array.Empty<RoomInstance>());
        }

        public RoomLayout GetLayout(Grid grid) => layout;

        public bool TryGetRoom(
            Grid grid,
            Vector2Int cell,
            out RoomInstance room) => layout.TryGetRoom(cell, out room);

        public bool TryGetRoom(
            BuildableObject part,
            out RoomInstance room) => layout.TryGetRoom(part, out room);

        public void Clear()
        {
        }
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly CharacterSO data;
        private readonly CharacterSkillSystemSettingsSO settings;

        public ActorFixture(
            int id,
            string displayName,
            string speciesTag,
            bool publishComposition = false,
            string persistentId = null)
        {
            settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                speciesTag);
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            GameObject actorObject = new GameObject($"ProgressionActor_{id}");
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AbilityWork>();
            if (publishComposition)
                Actor.PrepareForComposition();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.Progression.ConstructCharacterProgression(
                new ImmediateSkillGenerationService(),
                new TestSettingsProvider(settings),
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            Actor.RefreshAbilityCache();
            Actor.Identity.SetPersistentId(
                persistentId ?? $"character:qa-{id}");
            Actor.Initialization(data);
            System.Random random = new System.Random(id);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                $"{speciesTag} test origin",
                Array.Empty<int>(),
                CharacterGrowthRules.RollPotential(settings, random),
                id,
                autoChooseDrafts: false);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (publishComposition)
                Actor.PublishComposition();
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            if (Application.isPlaying
                && Actor != null
                && Actor.Identity != null)
            {
                CharacterId characterId = new(Actor.Identity.PersistentId);
                if (characterId.IsValid)
                {
                    DungeonRuntimeLifetimeScope scope = UnityEngine.Object
                        .FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
                    scope?.Container?.Resolve<IPopulationHealthService>()
                        ?.RemovePendingExposures(characterId);
                }
            }
            if (Actor != null) UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private sealed class DebugGameplayEffectSource : IGameplayEffectSource
    {
        public DebugGameplayEffectSource(
            GameplayEffectSourceRef sourceRef,
            params GameplayEffectBinding[] effects)
        {
            SourceRef = sourceRef;
            Effects = effects ?? Array.Empty<GameplayEffectBinding>();
        }

        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    private sealed class AcquiredTraitContentFixture : IDisposable
    {
        private readonly GameplayEffectDefinitionSO effect;

        public AcquiredTraitContentFixture()
        {
            Settings = ScriptableObject
                .CreateInstance<CharacterAcquiredTraitSettingsSO>();
            ConfigureSettings(Settings);

            effect = ScriptableObject
                .CreateInstance<GameplayEffectDefinitionSO>();
            effect.Configure(
                812503,
                "effect:qa:acquired-work-speed",
                GameplayEffectTargetIds.WorkSpeed,
                GameplayEffectOperation.Multiply,
                GameplayEffectProjectionPhase.Multiplicative,
                GameplayEffectSourceKind.AcquiredTrait,
                GameplayEffectStackingPolicy.StackAll,
                0.1f,
                10f);

            Modules = new[]
            {
                CreateModule("acquired:qa:focus", "집중", "집중력을 익혔다.",
                    "binding:qa:acquired:focus", 1.01f),
                CreateModule("acquired:qa:craft", "숙련", "손놀림이 안정되었다.",
                    "binding:qa:acquired:craft", 1.02f),
                CreateModule("acquired:qa:rhythm", "리듬", "작업 리듬을 익혔다.",
                    "binding:qa:acquired:rhythm", 1.03f),
                CreateModule("acquired:qa:insight", "통찰", "반복 속에서 통찰을 얻었다.",
                    "binding:qa:acquired:insight", 1.04f)
            };
            CharacterAcquiredTraitFormulaCatalogAssetBuilder.PopulateForEditorTest(
                Settings,
                Modules);
        }

        public CharacterAcquiredTraitSettingsSO Settings { get; }
        public CharacterAcquiredTraitModuleSO[] Modules { get; }

        public void Dispose()
        {
            foreach (CharacterAcquiredTraitModuleSO module in Modules)
            {
                if (module != null)
                    UnityEngine.Object.DestroyImmediate(module);
            }
            if (effect != null)
                UnityEngine.Object.DestroyImmediate(effect);
            if (Settings != null)
                UnityEngine.Object.DestroyImmediate(Settings);
        }

        private static void ConfigureSettings(
            CharacterAcquiredTraitSettingsSO settings)
        {
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("settingsId").stringValue =
                "acquired-trait-settings:qa";
            serialized.FindProperty("maximumActiveTraits").intValue = 3;
            SerializedProperty gates = serialized.FindProperty(
                "manifestationGates");
            gates.arraySize = 3;
            ConfigureGate(gates.GetArrayElementAtIndex(0), 3, 3,
                CharacterSkillRarity.Common);
            ConfigureGate(gates.GetArrayElementAtIndex(1), 8, 5,
                CharacterSkillRarity.Advanced);
            ConfigureGate(gates.GetArrayElementAtIndex(2), 20, 7,
                CharacterSkillRarity.Rare);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGate(
            SerializedProperty gate,
            int milestone,
            int budget,
            CharacterSkillRarity rarity)
        {
            gate.FindPropertyRelative("meaningfulRecordMilestone").intValue =
                milestone;
            gate.FindPropertyRelative("budget").intValue = budget;
            gate.FindPropertyRelative("rarity").enumValueIndex = (int)rarity;
        }

        private CharacterAcquiredTraitModuleSO CreateModule(
            string moduleId,
            string displayName,
            string description,
            string bindingId,
            float value)
        {
            CharacterAcquiredTraitModuleSO module = ScriptableObject
                .CreateInstance<CharacterAcquiredTraitModuleSO>();
            SerializedObject serialized = new SerializedObject(module);
            serialized.FindProperty("moduleId").stringValue = moduleId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("cost").intValue = 2;

            SerializedProperty domains = serialized.FindProperty(
                "domainAffinities");
            domains.arraySize = 1;
            domains.GetArrayElementAtIndex(0).enumValueIndex =
                (int)CharacterNarrativeDomain.Work;

            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            SerializedProperty binding = effects.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("bindingId").stringValue = bindingId;
            binding.FindPropertyRelative("definition").objectReferenceValue =
                effect;
            binding.FindPropertyRelative("value").floatValue = value;
            binding.FindPropertyRelative("condition").objectReferenceValue =
                null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return module;
        }
    }

    private sealed class TestSettingsProvider : ICharacterSkillSystemSettingsProvider
    {
        public TestSettingsProvider(CharacterSkillSystemSettingsSO settings)
        {
            Settings = settings;
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        private readonly CharacterActor[] actors;

        public FixedCharacterWorld(params CharacterActor[] actors)
        {
            this.actors = (actors ?? Array.Empty<CharacterActor>())
                .Where(value => value != null)
                .ToArray();
        }

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class DeferredAcquiredTraitLlmRuntime :
        ILocalLlmRuntime,
        ICorrelatedAcquiredTraitLlmRuntime,
        ICorrelatedAcquiredTraitModuleSelectionLlmRuntime
    {
        private sealed class Pending
        {
            public string RequestKey;
            public string Prompt;
            public Action<LocalLlmResult> Callback;
        }

        private readonly Queue<Pending> pending = new();

        public int PendingCallbackCount => pending.Count;

        public bool GenerateAcquiredTraitAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback)
        {
            pending.Enqueue(new Pending
            {
                RequestKey = requestKey,
                Prompt = prompt,
                Callback = callback
            });
            return true;
        }

        public bool GenerateAcquiredTraitModuleSelectionAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback) =>
            GenerateAcquiredTraitAsync(requestKey, prompt, callback);

        public void CancelAcquiredTraitRequest(string requestKey)
        {
            Pending[] retained = pending
                .Where(value => !string.Equals(
                    value.RequestKey,
                    requestKey,
                    StringComparison.Ordinal))
                .ToArray();
            pending.Clear();
            foreach (Pending value in retained)
                pending.Enqueue(value);
        }

        public void CompleteNextSuccess()
        {
            Require(pending.Count > 0, "No acquired-trait callback is pending.");
            Pending value = pending.Dequeue();
            if (value.Prompt.Contains("selectionId=", StringComparison.Ordinal))
            {
                string selectionId = ReadPromptLine(value.Prompt, "selectionId=");
                string positiveModuleId = ReadPromptToken(
                    value.Prompt, "- moduleId=", ';');
                string selectionEvidence = ReadPromptLine(value.Prompt, "evidenceFactIds=")
                    .Split(',').First(item => !string.IsNullOrWhiteSpace(item)).Trim();
                string selectionResponse = JsonUtility.ToJson(
                    new CharacterAcquiredTraitModuleSelectionResponseDto
                    {
                        selectionId = selectionId,
                        positiveModuleIds = new List<string> { positiveModuleId },
                        drawbackModuleIds = new List<string>(),
                        evidenceFactIds = new List<string> { selectionEvidence },
                        displayName = "후천 특성 발현",
                        narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                    });
                value.Callback?.Invoke(new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    selectionResponse,
                    string.Empty,
                    string.Empty));
                return;
            }
            if (value.Prompt.Contains("presentationId=", StringComparison.Ordinal))
            {
                string presentationId = ReadPromptLine(
                    value.Prompt,
                    "presentationId=");
                string formulaResponse = JsonUtility.ToJson(
                    new CharacterAcquiredTraitFormulaPresentationDto
                    {
                        presentationId = presentationId,
                        displayName = "후천 특성 발현",
                        narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                    });
                value.Callback?.Invoke(new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    formulaResponse,
                    string.Empty,
                    string.Empty));
                return;
            }
            string combinationId = ReadPromptToken(
                value.Prompt,
                "- combinationId=",
                ';');
            string evidenceList = ReadPromptLine(
                value.Prompt,
                "evidenceFactIds=");
            string evidence = evidenceList.Split(',')
                .First(item => !string.IsNullOrWhiteSpace(item))
                .Trim();
            string response = JsonUtility.ToJson(
                new CharacterAcquiredTraitResponseDto
                {
                    combinationId = combinationId,
                    displayName = "후천 특성 발현",
                    description = "반복된 경험이 새로운 성향으로 발현되었다.",
                    narrativeReason = "저장된 경험 근거에 따라 선택되었다.",
                    evidenceFactIds = new List<string> { evidence }
                });
            value.Callback?.Invoke(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                response,
                string.Empty,
                string.Empty));
        }

        private static string ReadPromptToken(
            string prompt,
            string prefix,
            char terminator)
        {
            int start = prompt.IndexOf(prefix, StringComparison.Ordinal);
            Require(start >= 0, $"Prompt token '{prefix}' is missing.");
            start += prefix.Length;
            int end = prompt.IndexOf(terminator, start);
            Require(end > start, $"Prompt token '{prefix}' is malformed.");
            return prompt.Substring(start, end - start).Trim();
        }

        private static string ReadPromptLine(string prompt, string prefix)
        {
            int start = prompt.IndexOf(prefix, StringComparison.Ordinal);
            Require(start >= 0, $"Prompt line '{prefix}' is missing.");
            start += prefix.Length;
            int end = prompt.IndexOf('\n', start);
            if (end < 0)
                end = prompt.Length;
            return prompt.Substring(start, end - start).Trim();
        }

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GeneratePersonaAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
    }

    private sealed class ImmediateSkillGenerationService : ICharacterSkillGenerationService
    {
        public CharacterSkillDraft CreateDraft(
            CharacterProgression progression,
            CharacterSkillKind kind,
            int unlockLevel,
            int revision = 0)
        {
            CharacterSkillDraft draft = new CharacterSkillDraft
            {
                kind = kind,
                unlockLevel = unlockLevel,
                requestKey = $"qa:{progression.GetInstanceID()}:{kind}:{unlockLevel}:r{revision}"
            };
            int count = kind == CharacterSkillKind.Active ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                draft.rules.Add(new CharacterSkillCandidateRule
                {
                    rarity = kind == CharacterSkillKind.Ultimate
                        ? CharacterSkillRarity.Legendary
                        : CharacterSkillRarity.Advanced,
                    budget = 10,
                    trigger = kind == CharacterSkillKind.Active
                        ? CharacterSkillTrigger.ManualCombat
                        : CharacterSkillTrigger.WorkCompleted,
                    target = kind == CharacterSkillKind.Active
                        ? CharacterSkillTarget.Enemy
                        : CharacterSkillTarget.Self
                });
            }

            return draft;
        }

        public void RequestDraft(CharacterProgression progression, CharacterSkillDraft draft)
        {
            if (draft == null || draft.isReady || draft.permanentlyChosen)
            {
                return;
            }

            for (int i = 0; i < draft.rules.Count; i++)
            {
                draft.candidates.Add(new CharacterSkillInstance
                {
                    id = $"{draft.requestKey}:{i}",
                    displayName = $"QA {draft.kind} {draft.unlockLevel}-{i}",
                    description = "검증용 기술",
                    narrativeReason = "검증용 경험",
                    kind = draft.kind,
                    rarity = draft.rules[i].rarity,
                    trigger = draft.kind == CharacterSkillKind.Ultimate
                        ? CharacterSkillTrigger.ManualCombat
                        : draft.rules[i].trigger,
                    target = draft.rules[i].target,
                    ultimateDomain = draft.kind == CharacterSkillKind.Ultimate
                        ? CharacterUltimateDomain.Offense
                        : CharacterUltimateDomain.None,
                    modules = new List<CharacterSkillModuleSelection>
                    {
                        new CharacterSkillModuleSelection
                        {
                            moduleId = draft.kind == CharacterSkillKind.Passive ? "work_speed" : "damage",
                            variantId = draft.kind == CharacterSkillKind.Passive ? "small" : "light"
                        }
                    },
                    requestKey = draft.requestKey
                });
            }

            draft.isReady = true;
            progression.OnDraftReady(draft);
        }

        public void CancelRequests(CharacterProgression progression)
        {
        }

        public bool TryValidateResponse(
            CharacterSkillDraft draft,
            string response,
            out List<CharacterSkillInstance> skills,
            out string error)
        {
            skills = new List<CharacterSkillInstance>();
            error = string.Empty;
            return false;
        }
    }

    private sealed class MissingLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            throw new InvalidOperationException("No runtime is expected in validation scenarios.");
        }
    }

    private sealed class TestLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        private readonly ILocalLlmRuntime runtime;

        public TestLlmRuntimeProvider(ILocalLlmRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool TryGetRuntime(out ILocalLlmRuntime resolvedRuntime)
        {
            resolvedRuntime = runtime;
            return runtime != null;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            return runtime ?? throw new InvalidOperationException("Missing test LLM runtime.");
        }
    }

    private sealed class SequencedLlmRuntime : ILocalLlmRuntime
    {
        private readonly Queue<LocalLlmResult> results = new Queue<LocalLlmResult>();

        public int CharacterSkillCallCount { get; private set; }

        public void Enqueue(LocalLlmResult result)
        {
            results.Enqueue(result);
        }

        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback)
        {
            CharacterSkillCallCount++;
            callback?.Invoke(results.Count > 0
                ? results.Dequeue()
                : new LocalLlmResult(LocalLlmRequestStatus.Failed, string.Empty, "missing result", string.Empty));
            return true;
        }

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private sealed class NeverCompletingLlmRuntime : ILocalLlmRuntime
    {
        public int CharacterSkillCallCount { get; private set; }

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> callback)
        {
            CharacterSkillCallCount++;
            return true;
        }

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private sealed class DelayedSkillLlmRuntime : ILocalLlmRuntime
    {
        private readonly List<Action<LocalLlmResult>> acceptedCallbacks =
            new List<Action<LocalLlmResult>>();

        public int CharacterSkillCallCount { get; private set; }
        public int PendingCallbackCount => acceptedCallbacks.Count;
        public int DeliveredCallbackCount { get; private set; }

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> callback)
        {
            CharacterSkillCallCount++;
            acceptedCallbacks.Add(callback);
            return true;
        }

        public void CompleteAcceptedCallbacks(LocalLlmResult result)
        {
            Action<LocalLlmResult>[] callbacks = acceptedCallbacks.ToArray();
            acceptedCallbacks.Clear();
            foreach (Action<LocalLlmResult> callback in callbacks)
            {
                DeliveredCallbackCount++;
                callback?.Invoke(result);
            }
        }

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private sealed class MutableUiClock : IUiClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }

        public void Advance(float seconds)
        {
            DeltaTime = Mathf.Max(0f, seconds);
            Time += DeltaTime;
        }
    }
}

public static class CharacterPopulationDebugScenarios
{
    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("ready pool and persistent identity", VerifyReadyPool, errors);
        Run("profile save restore", VerifyProfileRestore, errors);
        Run("duplicate profile IDs rejected", VerifyDuplicateProfileRestoreRejected, errors);
        Run("recruited visitor promotion", VerifyStaffPromotion, errors);
        Run("long-running population remains bounded", VerifyPopulationBound, errors);
        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log("Character population scenarios passed.");
        }

        return errors.Count == 0;
    }

    private static bool VerifyReadyPool()
    {
        using PopulationFixture fixture = new PopulationFixture();
        List<WorldCharacterProfile> acquired = new List<WorldCharacterProfile>();
        WorldCharacterProfile first = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        Require(first != null && first.IsReady,
            "The first visitor entered before its active and passive were prepared.");
        acquired.Add(first);
        Require(fixture.Service.Profiles.Count == fixture.Settings.guestReadyTarget
            && fixture.Service.Profiles.All(profile => profile.IsReady),
            "The initial ready pool was not prepared to the configured target.");
        Require(fixture.LifePublication.CharacterIds.SetEquals(
                fixture.Service.Profiles.Select(profile => profile.persistentId)),
            "Every persisted ready-pool profile must publish exactly one V19 life record.");

        while (acquired.Count < fixture.Settings.maximumAliveNonStaffGuests)
        {
            WorldCharacterProfile profile = fixture.Customers
                .Select(candidate => fixture.Service.AcquireVisitor(candidate))
                .FirstOrDefault(candidate => candidate != null);
            if (profile == null)
            {
                break;
            }

            Require(profile.IsReady, "An unprepared world character was admitted.");
            acquired.Add(profile);
        }

        Require(acquired.Count == fixture.Settings.maximumAliveNonStaffGuests,
            $"Expected the living guest cap {fixture.Settings.maximumAliveNonStaffGuests}, got {acquired.Count}.");
        Require(fixture.Customers.All(candidate => fixture.Service.AcquireVisitor(candidate) == null),
            "The living non-staff cap allowed an extra visitor profile.");
        Require(acquired.Select(profile => profile.persistentId).Distinct(StringComparer.Ordinal).Count()
                == fixture.Settings.maximumAliveNonStaffGuests,
            "World-character persistent IDs were duplicated.");
        Require(acquired.All(profile => profile.growth != null
                && profile.growth.initialized
                && profile.growth.traitIds.Count >= 1
                && profile.growth.traitIds.Count <= 4),
            "A visitor profile was missing initialized growth or a valid 1-4 trait roll.");
        Require(acquired.All(profile =>
                profile.growth.startingProficiencies != null
                && profile.growth.startingProficiencies.Count == 9
                && profile.growth.startingProficiencies.All(value =>
                    value != null
                    && value.experience
                        >= CharacterStartingProficiencyRules.MinimumStartingExperience
                    && value.experience
                        <= CharacterStartingProficiencyRules.MaximumStartingExperience)),
            "A visitor profile did not receive the nine initial proficiencies.");

        WorldCharacterProfile returning = first;
        returning.isVisiting = false;
        returning.visitCount = 4;
        CharacterSO returningTemplate = fixture.Customers.First(candidate => candidate.id == returning.characterDataId);
        WorldCharacterProfile reacquired = fixture.Service.AcquireVisitor(returningTemplate);
        Require(reacquired != null && reacquired.persistentId == returning.persistentId,
            "A returning guest was replaced by a new identity.");
        Require(reacquired.visitCount == 4,
            "The returning guest lost visit history.");
        return true;
    }

    private static bool VerifyProfileRestore()
    {
        using PopulationFixture source = new PopulationFixture();
        WorldCharacterProfile first = source.Service.AcquireVisitor(source.Customers[0]);
        WorldCharacterProfile second = source.Service.AcquireVisitor(source.Customers[0]);
        first.visitCount = 7;
        first.socialMemory ??= new CharacterSocialMemorySnapshot();
        first.socialMemory.characterSentiments.Add(new SocialMemoryFloat("world:test:friend", 0.45f));
        first.socialMemory.sourceTrust.Add(new SocialMemoryFloat("world:test:source", 0.8f));
        first.socialMemory.recentRumors.Add(new SocialRumorSnapshot
        {
            type = SocialRumorType.Praise,
            targetType = SocialRumorTargetType.Character,
            sourceActorId = "world:test:source",
            targetCharacterId = "world:test:friend",
            sentiment = 0.5f,
            spreadChance = 0.75f,
            trustImpact = 0.1f,
            remainingSeconds = 300f,
            summary = "helped at the counter",
            source = "test"
        });
        first.growth.nextActiveDraftHasPity = true;
        first.narrative.Record(
            CharacterNarrativeDomain.Relationship,
            "returning-guest",
            "dungeon",
            "trusted",
            3f,
            9);
        List<WorldCharacterProfile> snapshot = source.Service.CaptureProfiles();

        using PopulationFixture restored = new PopulationFixture();
        restored.Service.RestoreProfiles(snapshot);
        Require(restored.Service.Profiles.Count == snapshot.Count,
            "The restored population count changed.");
        WorldCharacterProfile restoredFirst = restored.Service.Profiles
            .First(profile => profile.persistentId == first.persistentId);
        bool socialRestored = restoredFirst.socialMemory != null
            && restoredFirst.socialMemory.characterSentiments.Any(item =>
                item != null
                && item.key == "world:test:friend"
                && Mathf.Approximately(item.value, 0.45f))
            && restoredFirst.socialMemory.sourceTrust.Any(item =>
                item != null
                && item.key == "world:test:source"
                && Mathf.Approximately(item.value, 0.8f))
            && restoredFirst.socialMemory.recentRumors.Any(item =>
                item != null
                && item.targetCharacterId == "world:test:friend"
                && item.remainingSeconds > 0f);
        Require(!restoredFirst.isVisiting
            && restoredFirst.visitCount == 7
            && socialRestored,
            "Visit state or social memory changed during restore.");
        Require(restoredFirst.growth.nextActiveDraftHasPity
            && restoredFirst.narrative.MeaningfulRecordCount == 1,
            "Growth correction or narrative ledger was lost during restore.");
        Require(restored.Service.Profiles.Any(profile => profile.persistentId == second.persistentId),
            "A second profile disappeared during restore.");
        return true;
    }

    private static bool VerifyStaffPromotion()
    {
        using PopulationFixture fixture = new PopulationFixture();
        WorldCharacterProfile profile = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        GameObject actorObject = new GameObject("PopulationPromotionActor");
        try
        {
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AbilityWork>();
            actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Identity.SetPersistentId(profile.persistentId);
            actor.Initialize(fixture.Customers[0]);
            fixture.Service.BindActor(profile, actor);
            fixture.Service.PromoteToStaff(actor);
            Require(profile.isStaff && !profile.isVisiting,
                "A recruited visitor profile was not promoted to staff.");
            actor.characterType = CharacterType.Customer;
            fixture.Service.ReleaseVisitor(actor);
            Require(profile.isStaff
                && !profile.isVisiting
                && actor.Identity.CharacterType == CharacterType.NPC
                && fixture.Service.TryGetProfile(actor, out WorldCharacterProfile retained)
                && retained.persistentId == profile.persistentId,
                "A hired profile was treated as a visitor after its actor type was reset.");
            WorldCharacterProfile next = fixture.Service.AcquireVisitor(fixture.Customers[0]);
            Require(next != null && next.persistentId != profile.persistentId,
                "A hired profile returned through the guest pool.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyDuplicateProfileRestoreRejected()
    {
        using PopulationFixture fixture = new PopulationFixture();
        WorldCharacterProfile first = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        Require(first != null, "No profile was available for duplicate restore verification.");
        WorldCharacterProfile duplicate = first.Clone();
        duplicate.visitCount++;

        try
        {
            fixture.Service.RestoreProfiles(new[] { first, duplicate });
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.Contains(first.persistentId, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool VerifyPopulationBound()
    {
        using PopulationFixture fixture = new PopulationFixture();
        for (int cycle = 0; cycle < 200; cycle++)
        {
            CharacterSO template = fixture.Customers[cycle % fixture.Customers.Length];
            WorldCharacterProfile visitor = fixture.Service.AcquireVisitor(template);
            if (visitor != null)
            {
                visitor.visitCount++;
                visitor.isVisiting = false;
            }
        }

        Require(fixture.Service.Profiles.Count <= fixture.Settings.maximumAliveNonStaffGuests,
            "Repeated visits grew the world-character pool beyond its configured cap.");
        Require(fixture.Service.Profiles.Select(profile => profile.persistentId)
                .Distinct(StringComparer.Ordinal).Count() == fixture.Service.Profiles.Count,
            "Repeated visits duplicated a persistent identity.");
        Require(fixture.Service.Profiles.All(profile => profile.IsReady),
            "The long-running pool retained an unfinished profile.");
        return true;
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario()) errors.Add($"{name}: returned false");
        }
        catch (Exception exception)
        {
            errors.Add($"{name}: {exception.Message}\n{exception.StackTrace}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MissingFactionContractQuery : IFactionContractQuery
    {
        public bool IsContractUnlocked(
            string factionId,
            FactionContractKind contract) => false;
    }

    private sealed class PopulationFixture : IDisposable
    {
        public PopulationFixture()
        {
            Settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
            Settings.guestReadyTarget = 8;
            Settings.guestReadyLowWatermark = 4;
            Settings.maximumAliveNonStaffGuests = 24;
            UnityGameContentRootLoader loader = new UnityGameContentRootLoader();
            ResourceGameContentCatalog content = new ResourceGameContentCatalog(loader);
            LifePublication = new RecordingCharacterLifePublicationService();
            CharacterPopulationApplicationAdapter adapter = new(
                new PopulationSettingsProvider(Settings),
                content,
                new ImmediateSkillGenerationService(),
                new FixedPopulationRunSeedProvider(1),
                new MissingFactionContractQuery(),
                new ResourceRunCharacterCatalog(content),
                new CharacterRuntimeProfileFactory(content),
                LifePublication);
            Service = new CharacterPopulationService(adapter);
            Customers = content.GetAll<CharacterSO>()
                .Where(candidate => candidate != null && candidate.characterType == CharacterType.Customer)
                .OrderBy(candidate => candidate.id)
                .ToArray();
            Require(Customers.Length > 0, "No customer templates were available for population tests.");
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
        public CharacterPopulationService Service { get; }
        public CharacterSO[] Customers { get; }
        public RecordingCharacterLifePublicationService LifePublication { get; }

        public void Dispose()
        {
            Service.Dispose();
            UnityEngine.Object.DestroyImmediate(Settings);
        }

        public sealed class RecordingCharacterLifePublicationService :
            ICharacterLifePublicationService
        {
            public HashSet<string> CharacterIds { get; } =
                new(StringComparer.Ordinal);

            public void EnsureRegistered(CharacterActor actor)
            {
                if (actor == null)
                {
                    throw new ArgumentNullException(nameof(actor));
                }

                CharacterIds.Add(CharacterPersistentIdentity.Require(actor).Value);
            }

            public void EnsureRegistered(
                CharacterId characterId,
                CharacterSpeciesId phenotypeSpeciesId)
            {
                if (!characterId.IsValid || !phenotypeSpeciesId.IsValid)
                {
                    throw new ArgumentException(
                        "Population fixture requires valid life IDs.");
                }

                CharacterIds.Add(characterId.Value);
            }
        }

        private sealed class FixedPopulationRunSeedProvider : IRunSeedProvider
        {
            public FixedPopulationRunSeedProvider(int runSeed)
            {
                RunSeed = runSeed;
            }

            public int RunSeed { get; }
        }
    }

    private sealed class ImmediateSkillGenerationService : ICharacterSkillGenerationService
    {
        public CharacterSkillDraft CreateDraft(
            CharacterProgression progression,
            CharacterSkillKind kind,
            int unlockLevel,
            int revision = 0)
        {
            int count = kind == CharacterSkillKind.Active ? 3 : 1;
            return new CharacterSkillDraft
            {
                kind = kind,
                unlockLevel = unlockLevel,
                requestKey = $"population-test:{progression.GetInstanceID()}:{kind}:{unlockLevel}:{revision}",
                rules = Enumerable.Range(0, count)
                    .Select(_ => new CharacterSkillCandidateRule { rarity = CharacterSkillRarity.Common, budget = 2 })
                    .ToList()
            };
        }

        public void RequestDraft(CharacterProgression progression, CharacterSkillDraft draft)
        {
            if (progression == null || draft == null || draft.isReady)
            {
                return;
            }

            int count = draft.kind == CharacterSkillKind.Active ? 3 : 1;
            draft.requestSubmitted = true;
            draft.candidates = Enumerable.Range(0, count)
                .Select(index => CreateSkill(draft, index))
                .ToList();
            draft.isReady = true;
            progression.OnDraftReady(draft);
        }

        public void CancelRequests(CharacterProgression progression)
        {
        }

        public bool TryValidateResponse(
            CharacterSkillDraft draft,
            string response,
            out List<CharacterSkillInstance> skills,
            out string error)
        {
            skills = null;
            error = "Not used by the immediate population test generator.";
            return false;
        }

        private static CharacterSkillInstance CreateSkill(CharacterSkillDraft draft, int index)
        {
            bool passive = draft.kind == CharacterSkillKind.Passive;
            return new CharacterSkillInstance
            {
                id = $"population-{draft.kind}-{draft.unlockLevel}-{index}",
                displayName = passive ? "생활 감각" : $"기본기 {index + 1}",
                description = "준비된 인물 풀 검증 기술",
                narrativeReason = "정체성과 경험을 바탕으로 익혔다.",
                kind = draft.kind,
                rarity = CharacterSkillRarity.Common,
                trigger = passive ? CharacterSkillTrigger.WorkCompleted : CharacterSkillTrigger.ManualCombat,
                target = passive ? CharacterSkillTarget.Self : CharacterSkillTarget.Enemy,
                modules = new List<CharacterSkillModuleSelection>
                {
                    new CharacterSkillModuleSelection
                    {
                        moduleId = passive ? "work_speed" : "damage",
                        variantId = passive ? "small" : "light"
                    }
                },
                requestKey = draft.requestKey
            };
        }
    }

    private sealed class PopulationSettingsProvider : ICharacterSkillSystemSettingsProvider
    {
        public PopulationSettingsProvider(CharacterSkillSystemSettingsSO settings)
        {
            Settings = settings;
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
    }
}
#endif
