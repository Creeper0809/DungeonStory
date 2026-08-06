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
        CharacterSkillRuntimeEffects.ResetTransientExecutionStateForDebug();
        List<string> errors = new List<string>();
        Run("level 50 experience curve", VerifyExperienceCurve, errors);
        Run("potential distribution and rarity pity", VerifyPotentialAndRarityRules, errors);
        Run("initial stats and level-50 growth", VerifyStatsAndLevelGrowth, errors);
        Run("active, passive, and ultimate slots", VerifySkillMilestones, errors);
        Run("permanent choice and save round trip", VerifyPermanentChoiceAndPersistence, errors);
        Run("module response validation", VerifyModuleValidation, errors);
        Run("LLM retry and request-key resume", VerifyRetryAndRequestKeyResume, errors);
        Run("ultimate domain use limits", VerifyUltimateUseLimits, errors);
        Run("independent scoped transient roots", VerifyIndependentTransientRoots, errors);
        Run("management and defense runtime effects", VerifySkillRuntimeEffects, errors);
        Run("training experience", VerifyTrainingExperience, errors);

        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log("Character progression scenarios passed.");
        }

        return errors.Count == 0;
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
        CharacterStatBlock initial = progression.GrowthState.initialBaseStats;
        int initialTotal = Enum.GetValues(typeof(CharacterStatType))
            .Cast<CharacterStatType>()
            .Sum(initial.Get);
        Require(initialTotal == 60, $"Initial stat total was {initialTotal}, not 60.");
        Require(Enum.GetValues(typeof(CharacterStatType))
                .Cast<CharacterStatType>()
                .All(stat => initial.Get(stat) >= 1 && initial.Get(stat) <= 10),
            "An initial stat was outside 1-10.");

        progression.AddExperience(GetExperienceToReach(50));
        Require(progression.Level == 50, $"Expected level 50, got {progression.Level}.");
        Require(progression.GrowthState.allocatedGrowthPoints == 59,
            $"Expected 59 level-growth points, got {progression.GrowthState.allocatedGrowthPoints}.");
        Require(progression.GrowthState.allocationRecords.Count == 59
                && progression.GrowthState.allocationRecords.All(record => record != null
                    && record.level >= 2
                    && record.level <= 50
                    && !string.IsNullOrWhiteSpace(record.reason)),
            "Level growth allocation records were not captured with level and reason.");
        Require(progression.CurrentExperience == 0 && progression.ExperienceToNextLevel == 0,
            "Level 50 retained experience or a next-level requirement.");
        Require(Enum.GetValues(typeof(CharacterStatType))
                .Cast<CharacterStatType>()
                .All(stat => progression.GrowthState.levelGrowthStats.Get(stat) <= 30),
            "A stat exceeded the configured level-growth cap.");
        Require(Enum.GetValues(typeof(CharacterStatType))
                .Cast<CharacterStatType>()
                .All(stat => progression.GrowthState.allocationRecords.Count(record => record.statType == stat)
                    == progression.GrowthState.levelGrowthStats.Get(stat)),
            "Level growth allocation records did not match levelGrowthStats.");
        Require(Enum.GetValues(typeof(CharacterStatType))
                .Cast<CharacterStatType>()
                .All(stat =>
                {
                    CharacterStatBreakdown breakdown = progression.GetStatBreakdown(stat);
                    return breakdown.BaseValue == initial.Get(stat)
                        && breakdown.LevelGrowthValue == progression.GrowthState.levelGrowthStats.Get(stat)
                        && breakdown.FinalValue == progression.GetFinalStat(stat);
                }),
            "Character stat breakdown no longer matches final stat calculation.");
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
        return true;
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
        Require(restoredDraft.candidates.Select(skill => skill.id)
                .SequenceEqual(sourceDraft.candidates.Select(skill => skill.id)),
            "Reloading regenerated prepared candidates.");
        Require(restored.Actor.Progression.GrowthState.traitIds.Count == 3
            && restored.Actor.Progression.PassiveSkills.Count == 1,
            "Traits and learned passives were not persisted as separate concepts.");
        return true;
    }

    private static bool VerifyModuleValidation()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        GameObject preview = new GameObject("SkillValidationPreview");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                settingsProvider,
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = preview.AddComponent<CharacterProgression>();
            progression.ConfigurePreview(
                service,
                settingsProvider,
                new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())));
            progression.ApplyPreparedIdentity(
                "검증자",
                "테스트",
                Array.Empty<int>(),
                CharacterStatBlock.CreateDefault(),
                CharacterPotentialGrade.Ordinary,
                7717,
                autoChooseDrafts: false);
            CharacterSkillDraft draft = service.CreateDraft(progression, CharacterSkillKind.Active, 1);
            CharacterSkillGenerationResponseDto valid = BuildValidResponse(draft, settings);
            string validJson = JsonUtility.ToJson(valid);
            Require(service.TryValidateResponse(draft, validJson, out List<CharacterSkillInstance> skills, out string validError)
                && skills.Count == 3,
                $"A valid constrained response was rejected: {validError}");

            CharacterSkillGenerationResponseDto pairIds = BuildValidResponse(draft, settings);
            foreach (CharacterSkillModuleResponseDto module in pairIds.candidates
                .SelectMany(candidate => candidate.modules))
            {
                module.pairId = $"{module.moduleId}|{module.variantId}";
                module.moduleId = string.Empty;
                module.variantId = string.Empty;
            }

            Require(service.TryValidateResponse(
                    draft,
                    JsonUtility.ToJson(pairIds),
                    out List<CharacterSkillInstance> pairSkills,
                    out string pairError)
                && pairSkills.Count == 3,
                $"A valid authored module-pair response was rejected: {pairError}");

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
            Require(passivePrompt.Contains("candidateCount=1; candidateIndexes=0")
                && passivePrompt.Contains("index 0 후보 하나만")
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
            combinationResponse.candidates[0].modules.Clear();
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

            CharacterSkillGenerationResponseDto unknown = BuildValidResponse(draft, settings);
            unknown.candidates[0].modules[0].moduleId = "unknown-module";
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(unknown), out _, out _),
                "An unknown module ID was accepted.");

            CharacterSkillGenerationResponseDto duplicate = BuildValidResponse(draft, settings);
            duplicate.candidates[0].modules.Add(new CharacterSkillModuleResponseDto
            {
                moduleId = duplicate.candidates[0].modules[0].moduleId,
                variantId = duplicate.candidates[0].modules[0].variantId
            });
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(duplicate), out _, out _),
                "A duplicated module was accepted.");

            CharacterSkillGenerationResponseDto overBudget = BuildValidResponse(draft, settings);
            draft.rules[0].budget = 0;
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(overBudget), out _, out _),
                "A response above its authored budget was accepted.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preview);
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
        GameObject sourceObject = new GameObject("SkillRetrySource");
        GameObject restoredObject = new GameObject("SkillRetryRestored");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            SequencedLlmRuntime runtime = new SequencedLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression source = sourceObject.AddComponent<CharacterProgression>();
            source.ConfigurePreview(
                service,
                settingsProvider,
                new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())));
            source.ApplyPreparedIdentity(
                "재시도 원본",
                "테스트",
                Array.Empty<int>(),
                CharacterStatBlock.CreateDefault(),
                CharacterPotentialGrade.Promising,
                991,
                autoChooseDrafts: false);
            CharacterSkillDraft draft = service.CreateDraft(source, CharacterSkillKind.Active, 1, revision: 4);
            string requestKey = draft.requestKey;
            runtime.Enqueue(new LocalLlmResult(
                LocalLlmRequestStatus.TimedOut,
                string.Empty,
                "timeout",
                string.Empty));
            service.RequestDraft(source, draft);
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 1 && !draft.isReady,
                "A timed-out generation request did not remain pending.");

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
            service.CancelRequests(source);

            CharacterProgression restored = restoredObject.AddComponent<CharacterProgression>();
            restored.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())));
            restored.RestorePersistentState(new CharacterProgressionSnapshot(
                1,
                0,
                restoredGrowth,
                new CharacterNarrativeLedger()));
            CharacterSkillDraft restoredDraft = restored.Drafts.First(item => item.requestKey == requestKey);
            runtime.Enqueue(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                JsonUtility.ToJson(BuildValidResponse(
                    restoredDraft,
                    settings,
                    restored.GrowthState.displayName)),
                string.Empty,
                string.Empty));
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 2,
                "The restored request was not submitted again.");
            Require(restoredDraft.isReady && restoredDraft.requestKey == requestKey,
                "The restored request changed key or failed to commit its prepared result.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(restoredObject);
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static CharacterSkillGenerationResponseDto BuildValidResponse(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        string characterName = "검증자")
    {
        CharacterSkillGenerationResponseDto response = new CharacterSkillGenerationResponseDto();
        HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < draft.rules.Count; i++)
        {
            CharacterSkillCandidateRule rule = draft.rules[i];
            CharacterSkillModuleRule module = rule.allowedModuleIds
                .Select(settings.FindModule)
                .Where(candidate => candidate != null)
                .First(candidate =>
                {
                    CharacterSkillNumericVariant variant = candidate.variants?
                        .FirstOrDefault(value => value != null
                            && rule.allowedVariantIds.Contains(value.id)
                            && value.cost <= rule.budget);
                    return variant != null && !used.Contains($"{candidate.id}:{variant.id}");
                });
            CharacterSkillNumericVariant selectedVariant = module.variants
                .First(value => value != null
                    && rule.allowedVariantIds.Contains(value.id)
                    && value.cost <= rule.budget
                    && !used.Contains($"{module.id}:{value.id}"));
            used.Add($"{module.id}:{selectedVariant.id}");
            response.candidates.Add(new CharacterSkillCandidateResponseDto
            {
                index = i,
                name = $"{characterName} 기술 {i + 1}",
                description = "검증된 효과를 적용한다.",
                narrativeReason = "반복된 경험에서 익혔다.",
                trigger = rule.trigger.ToString(),
                target = rule.target.ToString(),
                cooldownTurns = 1,
                modules = new List<CharacterSkillModuleResponseDto>
                {
                    new CharacterSkillModuleResponseDto
                    {
                        moduleId = module.id,
                        variantId = selectedVariant.id
                    }
                }
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

    private sealed class ActorFixture : IDisposable
    {
        private readonly CharacterSO data;
        private readonly CharacterSpeciesSO species;
        private readonly CharacterSkillSystemSettingsSO settings;

        public ActorFixture(int id, string displayName, string speciesTag)
        {
            settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
            species = ScriptableObject.CreateInstance<CharacterSpeciesSO>();
            species.speciesTag = speciesTag;
            species.displayName = speciesTag;

            data = ScriptableObject.CreateInstance<CharacterSO>();
            data.id = id;
            data.characterName = displayName;
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.speciesTag = speciesTag;
            data.species = species;
            data.traits = Array.Empty<CharacterTraitSO>();
            data.baseStats = CharacterStatBlock.CreateDefault();
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            GameObject actorObject = new GameObject($"ProgressionActor_{id}");
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AbilityWork>();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.Progression.ConstructCharacterProgression(
                new ImmediateSkillGenerationService(),
                new TestSettingsProvider(settings),
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())));
            Actor.RefreshAbilityCache();
            Actor.Identity.SetPersistentId($"character:qa-{id}");
            Actor.Initialization(data);
            System.Random random = new System.Random(id);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                $"{speciesTag} test origin",
                new[] { 1, 2, 3 },
                CharacterGrowthRules.RollInitialStats(settings, random),
                CharacterGrowthRules.RollPotential(settings, random),
                id,
                autoChooseDrafts: false);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            if (Actor != null) UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (species != null) UnityEngine.Object.DestroyImmediate(species);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
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
                && profile.growth.traitIds.Count == 3),
            "A visitor profile was missing initialized growth or three traits.");
        Require(acquired.All(profile => Enum.GetValues(typeof(CharacterStatType))
                .Cast<CharacterStatType>()
                .Sum(profile.growth.initialBaseStats.Get) == 60),
            "A visitor profile did not receive exactly 55 initial stat points.");

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
            CharacterPopulationApplicationAdapter adapter = new(
                new PopulationSettingsProvider(Settings),
                content,
                new ImmediateSkillGenerationService(),
                EditorRuntimeReferenceFixtures.DungeonWithRunVariables,
                new MissingFactionContractQuery(),
                new ResourceRunCharacterCatalog(content));
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

        public void Dispose()
        {
            Service.Dispose();
            UnityEngine.Object.DestroyImmediate(Settings);
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
