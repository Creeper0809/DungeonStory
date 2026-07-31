#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class DungeonGameSaveDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Save/Run Full Game Round Trip")]
    public static void RunFullGameRoundTrip()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException("Enter PlayMode before running the full game save round trip.");
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            throw new InvalidOperationException("DungeonRuntimeLifetimeScope is not ready.");
        }

        IDungeonGameSaveService saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        IGameDataProvider gameDataProvider = scope.Container.Resolve<IGameDataProvider>();
        IBlueprintResearchRuntimeProvider researchProvider = scope.Container.Resolve<IBlueprintResearchRuntimeProvider>();
        IResearchProjectCatalog researchProjectCatalog =
            scope.Container.Resolve<IResearchProjectCatalog>();
        IDailyFacilityShopRuntimeProvider shopProvider = scope.Container.Resolve<IDailyFacilityShopRuntimeProvider>();
        IRunVariableRuntimeProvider runProvider = scope.Container.Resolve<IRunVariableRuntimeProvider>();
        IMetaProgressionRuntimeProvider metaProvider = scope.Container.Resolve<IMetaProgressionRuntimeProvider>();
        IRegularCustomerRuntimeProvider customerProvider = scope.Container.Resolve<IRegularCustomerRuntimeProvider>();
        IStaffDiscontentRuntimeProvider discontentProvider = scope.Container.Resolve<IStaffDiscontentRuntimeProvider>();
        ICodexRuntimeProvider codexProvider = scope.Container.Resolve<ICodexRuntimeProvider>();
        IFacilityShopCatalog catalog = scope.Container.Resolve<IFacilityShopCatalog>();
        IGridSystemProvider gridProvider = scope.Container.Resolve<IGridSystemProvider>();
        IOwnerRunManagerProvider ownerProvider = scope.Container.Resolve<IOwnerRunManagerProvider>();
        IOperatingDaySettlementRuntimeProvider settlementProvider =
            scope.Container.Resolve<IOperatingDaySettlementRuntimeProvider>();
        IEventAlertRuntimeProvider alertProvider = scope.Container.Resolve<IEventAlertRuntimeProvider>();
        ICharacterWorldQuery characterWorld =
            scope.Container.Resolve<ICharacterWorldQuery>();
        IOffenseWorldMapRuntimeProvider offenseWorldMapProvider =
            scope.Container.Resolve<IOffenseWorldMapRuntimeProvider>();
        IOffenseRewardRuntimeProvider offenseRewardProvider =
            scope.Container.Resolve<IOffenseRewardRuntimeProvider>();
        IOffenseExpeditionRuntimeProvider offenseExpeditionProvider =
            scope.Container.Resolve<IOffenseExpeditionRuntimeProvider>();
        IOffenseBattleRuntime offenseBattle = scope.Container.Resolve<IOffenseBattleRuntime>();
        IInvasionThreatRuntimeProvider invasionThreatProvider =
            scope.Container.Resolve<IInvasionThreatRuntimeProvider>();
        IInvasionDirectorRuntimeProvider invasionDirectorProvider =
            scope.Container.Resolve<IInvasionDirectorRuntimeProvider>();
        IDefenseStatusRuntimeService defenseStatusRuntimeService =
            scope.Container.Resolve<IDefenseStatusRuntimeService>();
        ICharacterIdRegistry characterIdRegistry = scope.Container.Resolve<ICharacterIdRegistry>();
        ICombatEquipmentRuntime equipmentRuntime = scope.Container.Resolve<ICombatEquipmentRuntime>();

        Require(gameDataProvider.TryGetGameData(out GameData gameData), "GameData runtime is missing.");
        Require(researchProvider.TryGetRuntime(out BlueprintResearchRuntime research), "Research runtime is missing.");
        Require(shopProvider.TryGetRuntime(out DailyFacilityShopRuntime shop), "Shop runtime is missing.");
        Require(runProvider.TryGetRuntime(out RunVariableRuntime run), "Run variable runtime is missing.");
        Require(metaProvider.TryGetRuntime(out MetaProgressionRuntime meta), "Meta runtime is missing.");
        Require(customerProvider.TryGetRuntime(out RegularCustomerRuntime customers), "Customer runtime is missing.");
        Require(discontentProvider.TryGetRuntime(out StaffDiscontentRuntime discontent),
            "Staff discontent runtime is missing.");
        Require(codexProvider.TryGetRuntime(out CodexRuntime codex), "Codex runtime is missing.");
        Require(gridProvider.TryGetGrid(out Grid grid), "Grid runtime is missing.");
        Require(ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ownerManager.CurrentOwnerActor != null, "Owner runtime is missing.");
        Require(settlementProvider.TryGetRuntime(out OperatingDaySettlementRuntime settlement),
            "Settlement runtime is missing.");
        Require(alertProvider.TryGetRuntime(out EventAlertRuntime alerts), "Event alert runtime is missing.");
        Require(offenseWorldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime offenseWorldMap),
            "Offense world map runtime is missing.");
        Require(offenseRewardProvider.TryGetRuntime(out OffenseRewardRuntime offenseRewards),
            "Offense reward runtime is missing.");
        Require(offenseExpeditionProvider.TryGetRuntime(out OffenseExpeditionRuntime expeditions),
            "Offense expedition runtime is missing.");
        Require(invasionThreatProvider.TryGetRuntime(out InvasionThreatRuntime invasionThreat),
            "Invasion threat runtime is missing.");
        Require(invasionDirectorProvider.TryGetRuntime(out InvasionDirectorRuntime invasionDirector),
            "Invasion director runtime is missing.");

        CharacterSO temporaryOffenseStaffData = null;
        DungeonGameSaveData baseline = saveService.Capture();
        try
        {
            int markerMoney = gameData.holdingMoney.Value + 137;
            gameData.holdingMoney.Value = markerMoney;

            const string RecipeMarker = "qa:save-round-trip";
            research.State.UnlockRecipe(RecipeMarker);
            ResearchProjectSO migratedRecipeProject = researchProjectCatalog.Projects
                .FirstOrDefault(project => project != null
                    && project.Unlocks.OfType<BlueprintRecipeUnlock>().Any());
            Require(migratedRecipeProject != null,
                "A research project with a recipe reward is missing.");
            research.State.Projects.Complete(migratedRecipeProject.ProjectId);
            string migratedRecipeId = migratedRecipeProject.Unlocks
                .OfType<BlueprintRecipeUnlock>()
                .Select(unlock => unlock.recipeId)
                .FirstOrDefault();
            Require(!string.IsNullOrWhiteSpace(migratedRecipeId),
                "The completed research project has no current recipe reward.");
            research.State.UnlockRecipe(migratedRecipeId);
            BuildingSO researchUnlockBuilding = catalog.Buildings.FirstOrDefault(candidate => candidate != null
                && candidate.IsModularFacility()
                && candidate.GetUnlockPhase() > 1);
            Require(researchUnlockBuilding != null, "No phase-gated modular building exists for the save test.");
            research.State.UnlockBuilding(researchUnlockBuilding.id);

            BuildingSO building = catalog.Buildings.FirstOrDefault(candidate => candidate != null
                && FacilityShopService.CanEnterBasicPurchase(candidate));
            Require(building != null, "No basic-purchase building exists for the save test.");
            shop.UnlockState.UnlockBasicPurchaseById(building.id);

            run.ActivateOperationVariable(RunVariableIds.VisitingMerchant, 3, false);
            meta.State.AddCurrency(19);
            meta.State.SetUpgradeLevelForDebug(MetaUpgradeIds.CommerceSupplyNetwork, 2);
            const string CustomerMarker = "world:saveqa:919191";
            customers.State.Restore(new[]
            {
                new RegularCustomerRecord(
                    CustomerMarker,
                    "Save QA Guest",
                    "Slime",
                    null,
                    4,
                    82f,
                    true,
                    true,
                    false,
                    RecruitCapability.All)
            });

            const string CodexMarker = "qa:save-entry";
            codex.State.AddInfo(
                CodexEntryCategory.Facility,
                CodexMarker,
                "Save QA",
                "Round trip marker",
                CodexInfoSource.System);

            const string CharacterLogMarker = "Save QA character memory";
            const string MoodMarker = "qa:save-character-mood";
            CharacterActor owner = ownerManager.CurrentOwnerActor;
            owner.Heal(owner.MaxHealth);
            owner.ApplyDamage(9f, "save-qa");
            owner.stats[CharacterCondition.HUNGER] = 43f;
            owner.ApplyMoodFactor(MoodMarker, "Save QA mood", 7f, 240f, 1);
            owner.LogComponent.RestoreVisibleEntries(owner.Log.Concat(new[] { CharacterLogMarker }));
            AbilityWork ownerWork = owner.GetAbility<AbilityWork>();
            Require(ownerWork != null, "Owner work ability is missing.");
            ownerWork.SetWorkPriority(BuiltInWorkTypeIds.Research, WorkPriorityLevel.Off);
            ownerWork.SetDutyState(AbilityWork.DutyState.OffDuty);
            Vector2Int savedOwnerPosition = grid.GetXY(owner.transform.position);
            float savedOwnerHealth = owner.CurrentHealth;

            alerts.OnTriggerEvent(new EventAlertRequestedEvent(new EventAlertRequest(
                "Save QA Alert",
                "Alert history round trip marker",
                EventAlertImportance.High,
                "Save QA")));
            alerts.OnTriggerEvent(new EventAlertRequestedEvent(new EventAlertRequest(
                "Save QA Alert",
                "Alert history round trip marker",
                EventAlertImportance.High,
                "Save QA")));

            OperatingDayReport settlementReportMarker = OperatingDayReport.Create(
                day: 11,
                totalRevenue: 777,
                totalVisits: 8,
                averageSatisfaction: 73f,
                incidents: new[] { "Save QA incident" },
                maintenanceCost: 44,
                payrollCost: 66,
                previousDebt: 88,
                paidOperatingCost: 100,
                unpaidOperatingCost: 98,
                closingBalance: 500,
                consecutiveShortfallDays: 3);
            settlement.RestorePersistentState(new OperatingDaySettlementPersistenceState(
                12,
                321,
                4,
                2,
                new Dictionary<string, int> { ["Save QA Facility"] = 321 },
                new Dictionary<string, int> { ["Slime"] = 4 },
                new Dictionary<StockCategory, int> { [StockCategory.Food] = 7 },
                new[] { 60f, 80f },
                new[] { new StockSupplyResult(true, StockCategory.Food, 7, 7, 14, "Save QA", string.Empty) },
                new[] { "Save QA incident" },
                new[] { "Save QA alert" },
                new[] { settlementReportMarker },
                outstandingDebt: 222,
                consecutiveShortfallDays: 3,
                emergencyFundingUsed: true));

            OffenseTargetDefinition completedOffenseTarget = offenseWorldMap.TargetDefinitions.FirstOrDefault();
            OffenseTargetDefinition offenseTarget = offenseWorldMap.TargetDefinitions.Skip(1).FirstOrDefault();
            Require(completedOffenseTarget != null && offenseTarget != null,
                "The offense campaign has too few targets for the save test.");
            offenseWorldMap.RestorePersistentState(
                2,
                offenseTarget.id,
                new[] { completedOffenseTarget.id, offenseTarget.id },
                new[] { completedOffenseTarget.id },
                string.Empty);
            offenseRewards.RestorePersistentState(
                411,
                new Dictionary<StockCategory, int> { [StockCategory.Food] = 13 },
                new[] { 9191 },
                new[] { 8181 });
            CharacterActor expeditionMember = characterWorld.Characters
                .FirstOrDefault(actor => actor != null
                    && !actor.IsOwner
                    && !actor.IsDead
                    && actor.Identity != null
                    && actor.Identity.CharacterType == CharacterType.NPC
                    && actor.GetAbility<AbilityWork>() != null);
            if (expeditionMember == null)
            {
                IRunCharacterCatalog characterCatalog = scope.Container.Resolve<IRunCharacterCatalog>();
                ICharacterSpawnerProvider spawnerProvider = scope.Container.Resolve<ICharacterSpawnerProvider>();
                ICharacterSpawnObjectFactory characterFactory =
                    scope.Container.Resolve<ICharacterSpawnObjectFactory>();
                CharacterSO staffData = characterCatalog.Characters.FirstOrDefault(data => data != null
                    && data.characterType == CharacterType.NPC
                    && data.role != CharacterRole.Owner);
                if (staffData == null)
                {
                    temporaryOffenseStaffData = ScriptableObject.CreateInstance<CharacterSO>();
                    temporaryOffenseStaffData.id = 991337;
                    temporaryOffenseStaffData.characterName = "Save QA Expedition Staff";
                    temporaryOffenseStaffData.characterType = CharacterType.NPC;
                    temporaryOffenseStaffData.role = CharacterRole.Regular;
                    temporaryOffenseStaffData.speciesTag = "Slime";
                    staffData = temporaryOffenseStaffData;
                }

                Require(spawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
                    && spawner.characterPrefab != null,
                    "The character prefab is missing for the offense save test.");
                GameObject staffObject = characterFactory.Create(spawner.characterPrefab);
                if (staffObject.GetComponent<AbilityWork>() == null)
                {
                    staffObject.AddComponent<AbilityWork>();
                }

                characterFactory.Inject(staffObject);
                expeditionMember = staffObject.GetComponent<CharacterActor>();
                Require(expeditionMember != null, "The character prefab has no CharacterActor.");
                staffObject.name = "Save QA Expedition Staff";
                expeditionMember.EnsureRuntimeState();
                expeditionMember.RefreshAbilityCache();
                expeditionMember.Initialize(staffData);
                expeditionMember.transform.position = grid.GetWorldPos(savedOwnerPosition);
                staffObject.SetActive(true);
            }

            if (expeditionMember.IsOnExpedition)
            {
                expeditionMember.EndExpedition(alive: true);
            }
            else
            {
                expeditionMember.SetLifecycleState(CharacterLifecycleState.Active);
            }

            string discontentStaffId = characterIdRegistry.GetOrAssignPersistentId(expeditionMember);
            expeditionMember.stats[CharacterCondition.MOOD] = 20f;
            StaffDiscontentRecord savedDiscontent = discontent.ProcessStaff(
                expeditionMember,
                out StaffDiscontentOutcome savedDiscontentOutcome);
            Require(savedDiscontent != null
                    && savedDiscontentOutcome == StaffDiscontentOutcome.WorkDisruption
                    && savedDiscontent.Stage == StaffDiscontentStage.WorkDisruption,
                "The staff discontent save marker was not created.");

            const string EquipmentWeaponMarker = "weapon:dagger";
            const string EquipmentArmorMarker = "armor:gambeson";
            const string EquipmentCraftMarker = "weapon:mace";
            const string EquipmentWeaponInstanceMarker = "qa-save-weapon-equipped";
            const string EquipmentSpareWeaponInstanceMarker = "qa-save-weapon-stored";
            const string EquipmentArmorInstanceMarker = "qa-save-armor-equipped";
            const string EquipmentCraftOrderMarker = "qa-save-craft-order";
            const float ExpeditionStressMarker = 42f;
            string equipmentStaffId = characterIdRegistry.GetOrAssignPersistentId(expeditionMember);
            expeditionMember.Progression?.AddExperience(CharacterProgression.GetExperienceRequired(1));
            Require(expeditionMember.Progression != null
                    && expeditionMember.Progression.Level >= 2
                    && expeditionMember.Progression.GrowthState.allocationRecords.Count > 0,
                "The save test member did not create a level-growth allocation record.");
            equipmentRuntime.Restore(new DungeonCombatEquipmentSaveData
            {
                instances = new List<CombatEquipmentInstance>
                {
                    new CombatEquipmentInstance
                    {
                        instanceId = EquipmentWeaponInstanceMarker,
                        definitionId = EquipmentWeaponMarker,
                        quality = CombatEquipmentQuality.Good,
                        durabilityRatio = 1f,
                        worldState = CombatEquipmentWorldState.Equipped,
                        ownerCharacterId = equipmentStaffId
                    },
                    new CombatEquipmentInstance
                    {
                        instanceId = EquipmentSpareWeaponInstanceMarker,
                        definitionId = EquipmentWeaponMarker,
                        quality = CombatEquipmentQuality.Normal,
                        durabilityRatio = 1f,
                        worldState = CombatEquipmentWorldState.Stored
                    },
                    new CombatEquipmentInstance
                    {
                        instanceId = EquipmentArmorInstanceMarker,
                        definitionId = EquipmentArmorMarker,
                        quality = CombatEquipmentQuality.Normal,
                        durabilityRatio = 0.8f,
                        worldState = CombatEquipmentWorldState.Equipped,
                        ownerCharacterId = equipmentStaffId
                    }
                },
                loadouts = new List<CharacterCombatLoadoutState>
                {
                    new CharacterCombatLoadoutState
                    {
                        characterId = equipmentStaffId,
                        activeProfileId = CombatLoadoutPresetIds.Peace,
                        profiles = new List<CharacterCombatLoadoutProfile>
                        {
                            new CharacterCombatLoadoutProfile
                            {
                                profileId = CombatLoadoutPresetIds.Peace,
                                displayName = "평시",
                                weaponInstanceIds = new List<string>
                                {
                                    EquipmentWeaponInstanceMarker
                                },
                                armorInstanceIds = new List<string>
                                {
                                    EquipmentArmorInstanceMarker
                                },
                                activeWeaponInstanceId = EquipmentWeaponInstanceMarker
                            }
                        }
                    }
                },
                craftOrders = new List<CombatEquipmentCraftOrderSaveData>
                {
                    new CombatEquipmentCraftOrderSaveData
                    {
                        orderId = EquipmentCraftOrderMarker,
                        definitionId = EquipmentCraftMarker,
                        requiredWork = 10f,
                        completedWork = 6.75f,
                        materialsReady = true
                    }
                }
            });
            expeditionMember.Lifecycle?.RestoreExpeditionRecovery(new CharacterExpeditionRecoveryState
            {
                stress = ExpeditionStressMarker
            });

            Require(expeditionMember.BeginExpedition(), "The offense save test member could not depart.");
            const string ExpeditionMarker = "qa-save-expedition";
            const string ExpeditionResultMarker = "qa-save-expedition-result";
            offenseBattle.ClearForPersistentRestore();
            expeditions.RestorePersistentState(
                new[]
                {
                    new OffenseExpeditionRun(
                        ExpeditionMarker,
                        offenseTarget,
                        new[] { expeditionMember },
                        77f,
                        41f)
                },
                new[]
                {
                    new OffenseExpeditionResult(
                        ExpeditionResultMarker,
                        offenseTarget.id,
                        offenseTarget.title,
                        true,
                        88f,
                        50f,
                        12f,
                        90f,
                        new[]
                        {
                            new OffenseExpeditionMemberSnapshot(
                                "Save QA Staff",
                                "Slime",
                                88f,
                                true,
                                2f)
                        },
                        new[] { "Save QA reward" })
                });

            InvasionThreatSnapshot invasionSpawnSnapshot = new InvasionThreatSnapshot(
                101f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(6f, 4f, 3f, 2f),
                0f,
                0f);
            run.SelectInvasionVariable(RunVariableIds.LootPriority, false);
            Require(invasionDirector.TrySpawnIntruder(invasionSpawnSnapshot, out CharacterActor savedIntruder)
                && savedIntruder != null,
                "The invasion save test could not spawn an intruder.");
            InvasionIntruderRuntime savedIntruderRuntime = savedIntruder.GetComponent<InvasionIntruderRuntime>();
            FieldInfo facilityDamageCountField = typeof(InvasionIntruderRuntime).GetField(
                "facilityDamageCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(savedIntruderRuntime != null && facilityDamageCountField != null,
                "The invasion save test could not access facility damage progress.");
            facilityDamageCountField.SetValue(savedIntruderRuntime, 1);
            savedIntruder.ApplyDamage(7f, "save-qa");
            DefenseStatusRuntime savedIntruderStatuses = defenseStatusRuntimeService.GetOrAdd(savedIntruder);
            savedIntruderStatuses.ApplyStatus(DefenseStatusKind.Burn, 1.5f, 33f, 2);
            float savedIntruderHealth = savedIntruder.CurrentHealth;
            string savedIntruderPatternId = savedIntruderRuntime.Pattern?.id
                ?? string.Empty;
            Require(!string.IsNullOrWhiteSpace(savedIntruderPatternId),
                "The invasion save test spawned an intruder without a pattern.");
            invasionThreat.RestorePersistentState(new InvasionThreatPersistenceState(
                84f,
                123f,
                0f,
                4f,
                7f,
                true,
                false,
                2f,
                new InvasionThreatFactors(5f, 4f, 3f, 2f)));

            // Event-producing setup above is intentional; pin the ledger marker last.
            settlement.RestorePersistentState(new OperatingDaySettlementPersistenceState(
                12,
                321,
                4,
                2,
                new Dictionary<string, int> { ["Save QA Facility"] = 321 },
                new Dictionary<string, int> { ["Slime"] = 4 },
                new Dictionary<StockCategory, int> { [StockCategory.Food] = 7 },
                new[] { 60f, 80f },
                new[] { new StockSupplyResult(true, StockCategory.Food, 7, 7, 14, "Save QA", string.Empty) },
                new[] { "Save QA incident" },
                new[] { "Save QA alert" },
                new[] { settlementReportMarker },
                outstandingDebt: 222,
                consecutiveShortfallDays: 3,
                emergencyFundingUsed: true));

            string savedOwnerDoctrineId = run.State.StartVariables?.ownerDoctrineId ?? string.Empty;
            Require(!string.IsNullOrWhiteSpace(savedOwnerDoctrineId),
                "Owner doctrine was not active before capture.");
            string json = saveService.ToJson(saveService.Capture());
            DungeonGameSaveData parsed = saveService.FromJson(json);
            DungeonResearchSaveData parsedResearch =
                DungeonSaveSectionPayload.ReadOrNew<DungeonResearchSaveData>(
                    parsed,
                    BlueprintResearchSaveSection.Id);
            DungeonCharacterWorldSaveData parsedCharacters =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                    parsed,
                    CharacterWorldSaveSection.Id);
            DungeonOffenseSaveData parsedOffense =
                DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseSaveData>(
                    parsed,
                    OffenseSaveSection.Id);
            DungeonCombatEquipmentSaveData parsedEquipment =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCombatEquipmentSaveData>(
                    parsed,
                    CombatEquipmentSaveSection.Id);
            parsedResearch.unlockedRecipeIds.Remove(migratedRecipeId);
            parsedResearch.unlockedRecipeIds.Add("recipe_battlefield_dining_2");
            DungeonSaveSectionPayload.Write(
                parsed,
                BlueprintResearchSaveSection.Id,
                3,
                DungeonSaveRestorePhase.RuntimeState,
                parsedResearch);
            int savedCharacterCount = parsedCharacters.actors?.Count ?? 0;
            Require(savedCharacterCount > 0, "No persistent characters were captured.");
            DungeonOffenseExpeditionRunSaveData savedExpedition = parsedOffense.activeExpeditions?
                .FirstOrDefault(active => active != null && active.expeditionId == ExpeditionMarker);
            Require(parsedOffense.completedTargetIds.Contains(completedOffenseTarget.id)
                    && string.IsNullOrWhiteSpace(parsedOffense.revealedTruthTargetId),
                "Offense campaign progress was not captured.");
            Require(savedExpedition != null
                && savedExpedition.memberPersistentIds.Count == 1
                && parsedCharacters.actors.Any(actor => actor != null
                    && actor.persistentId == savedExpedition.memberPersistentIds[0]),
                "The active expedition member was not captured with a persistent character id.");
            DungeonCharacterSaveData savedEquipmentActor = parsedCharacters.actors
                .FirstOrDefault(actor => actor != null && actor.persistentId == equipmentStaffId);
            Require(savedEquipmentActor != null
                    && Mathf.Approximately(
                        savedEquipmentActor.expeditionRecovery?.stress ?? -1f,
                        ExpeditionStressMarker)
                    && savedEquipmentActor.growth?.allocationRecords != null
                    && savedEquipmentActor.growth.allocationRecords.Count > 0,
                "Expedition recovery stress or growth allocation records were not captured with the character.");
            Require(parsedEquipment.loadouts.Any(loadout => loadout != null
                        && loadout.characterId == equipmentStaffId
                        && loadout.profiles.Any(profile => profile != null
                            && profile.weaponInstanceIds.Contains(
                                EquipmentWeaponInstanceMarker)
                            && profile.armorInstanceIds.Contains(
                                EquipmentArmorInstanceMarker)))
                    && parsedEquipment.instances.Count(entry => entry != null
                        && entry.definitionId == EquipmentWeaponMarker) == 2
                    && parsedEquipment.craftOrders.Any(order => order != null
                        && order.orderId == EquipmentCraftOrderMarker
                        && order.definitionId == EquipmentCraftMarker
                        && Mathf.Approximately(order.RemainingWork, 3.25f)),
                "Combat equipment instances, loadout, or work queue were not captured.");

            gameData.holdingMoney.Value = 1;
            research.State.ClearForRestore();
            shop.RestoreState(1, Array.Empty<int>(), Array.Empty<int>());
            run.StartRun(123456, null, InvasionThreatDifficulty.Normal);
            meta.State.Restore(
                0,
                0,
                Array.Empty<KeyValuePair<string, int>>(),
                Array.Empty<string>());
            customers.State.Restore(Array.Empty<RegularCustomerRecord>());
            codex.State.ClearForRestore();
            alerts.RestoreHistory(Array.Empty<EventAlertRecordSnapshot>());
            settlement.RestorePersistentState(new OperatingDaySettlementPersistenceState(
                1,
                0,
                0,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
            offenseWorldMap.StartWorldMap();
            offenseRewards.ResetState();
            expeditions.RestorePersistentState(
                Array.Empty<OffenseExpeditionRun>(),
                Array.Empty<OffenseExpeditionResult>());
            offenseBattle.ClearForPersistentRestore();
            expeditionMember.EndExpedition(alive: true);
            invasionDirector.ClearForPersistentRestore();
            invasionThreat.RestorePersistentState(new InvasionThreatPersistenceState(
                0f,
                0f,
                0f,
                -1f,
                0f,
                false,
                false,
                0f,
                default));
            owner.stats[CharacterCondition.HUNGER] = 1f;
            owner.Stats.RemoveMoodFactor(MoodMarker);
            ownerWork.SetWorkPriority(BuiltInWorkTypeIds.Research, WorkPriorityLevel.Priority1);
            ownerWork.SetDutyState(AbilityWork.DutyState.OnDuty);
            owner.LogComponent.RestoreVisibleEntries(Array.Empty<string>());
            discontent.RestoreSnapshots(Array.Empty<StaffDiscontentSnapshot>());

            Require(
                saveService.TryRestore(parsed, out DungeonGameRestoreReport report),
                "Marker restore failed: " + string.Join(" | ", report.Errors));
            Require(gameData.holdingMoney.Value == markerMoney, "Money did not round-trip.");
            Require(research.State.UnlockedRecipeIds.Contains(RecipeMarker), "Research state did not round-trip.");
            Require(research.State.UnlockedRecipeIds.Contains(migratedRecipeId),
                "Completed research project did not restore its current recipe reward.");
            Require(research.State.IsBuildingUnlocked(researchUnlockBuilding.id),
                "Research construction unlock did not round-trip.");
            Require(shop.UnlockState.BasicPurchaseBuildingIds.Contains(building.id), "Shop state did not round-trip.");
            Require(run.State.ActiveOperationVariables.Any(active => active.Definition != null
                && active.Definition.id == RunVariableIds.VisitingMerchant), "Run variables did not round-trip.");
            Require(run.State.StartVariables != null
                && run.State.StartVariables.ownerDoctrineId == savedOwnerDoctrineId,
                "Owner doctrine did not round-trip.");
            Require(meta.State.LifetimeEarnedCurrency >= 19, "Meta progression currency did not round-trip.");
            Require(meta.State.GetUpgradeLevel(MetaUpgradeIds.CommerceSupplyNetwork) == 2
                && Mathf.Approximately(meta.GetCommerceStockCostMultiplier(StockCategory.Food), 0.92f),
                "Strategy meta upgrade did not round-trip with its runtime effect.");
            Require(customers.State.TryGetRecord(CustomerMarker, out RegularCustomerRecord restoredCustomer)
                && restoredCustomer.VisitCount == 4
                && Mathf.Approximately(restoredCustomer.AverageSatisfaction, 82f),
                "Regular customer state did not round-trip.");
            Require(discontent.State.TryGetRecord(discontentStaffId, out StaffDiscontentRecord restoredDiscontent)
                && restoredDiscontent.Stage == StaffDiscontentStage.WorkDisruption
                && restoredDiscontent.LowMoodDays == 1,
                "Staff discontent state did not round-trip.");
            Require(codex.State.HasInfo(
                CodexEntryCategory.Facility,
                CodexMarker,
                "Round trip marker"), "Codex state did not round-trip.");
            Require(ownerManager.CurrentOwnerActor != null, "Owner was not recreated during restore.");
            CharacterActor restoredOwner = ownerManager.CurrentOwnerActor;
            Require(report.RestoredCharacterCount == savedCharacterCount,
                $"Character restore count mismatch: {report.RestoredCharacterCount}/{savedCharacterCount}.");
            Require(Mathf.Approximately(restoredOwner.CurrentHealth, savedOwnerHealth),
                "Owner health did not round-trip.");
            Require(Mathf.Approximately(restoredOwner.stats[CharacterCondition.HUNGER], 43f),
                "Owner needs did not round-trip.");
            Require(restoredOwner.Mood.Factors.Any(factor => factor.Id == MoodMarker),
                "Owner mood memory did not round-trip.");
            Require(restoredOwner.Log.Contains(CharacterLogMarker),
                "Owner visible log did not round-trip.");
            Require(restoredOwner.GetAbility<AbilityWork>().WorkPriorities
                    .GetPriority(BuiltInWorkTypeIds.Research) == WorkPriorityLevel.Off,
                "Owner work priority did not round-trip.");
            Require(restoredOwner.GetAbility<AbilityWork>().CurrentDutyState == AbilityWork.DutyState.OffDuty,
                "Owner duty state did not round-trip.");
            Require(grid.GetXY(restoredOwner.transform.position) == savedOwnerPosition,
                "Owner grid position did not round-trip.");
            Require(settlement.CurrentDay == 12
                && settlement.CurrentRevenue == 321
                && settlement.CurrentVisits == 4
                && settlement.CurrentConsumedStock == 7
                && settlement.CurrentIncidentCount == 1
                && settlement.CurrentEventCount == 1
                && settlement.OutstandingDebt == 222
                && settlement.ConsecutiveShortfallDays == 3
                && settlement.EmergencyFundingUsed,
                "Current operating-day ledger did not round-trip.");
            Require(settlement.ReportHistory.Count == 1
                && settlement.LatestReport != null
                && settlement.LatestReport.day == 11
                && settlement.LatestReport.totalRevenue == 777
                && settlement.LatestReport.incidents.Contains("Save QA incident")
                && settlement.LatestReport.maintenanceCost == 44
                && settlement.LatestReport.payrollCost == 66
                && settlement.LatestReport.previousDebt == 88
                && settlement.LatestReport.paidOperatingCost == 100
                && settlement.LatestReport.unpaidOperatingCost == 98
                && settlement.LatestReport.closingBalance == 500
                && settlement.LatestReport.consecutiveShortfallDays == 3,
                "Operating-day report history did not round-trip.");
            Require(alerts.EventLog.Any(record => record.Title == "Save QA Alert"
                && record.Count == 2
                && record.Detail == "Alert history round trip marker"),
                "Event alert history did not round-trip.");
            Require(offenseWorldMap.State.ReconLevel == 2
                && offenseWorldMap.State.SelectedTargetId == offenseTarget.id
                && offenseWorldMap.State.KnownTargetIds.Contains(offenseTarget.id)
                && offenseWorldMap.State.IsTargetCompleted(completedOffenseTarget.id)
                && !offenseWorldMap.State.TruthRevealed,
                "Offense world map state did not round-trip.");
            Require(offenseRewards.State.MoneyEarned == 411
                && offenseRewards.State.StockGrantedByCategory.TryGetValue(StockCategory.Food, out int restoredFood)
                && restoredFood == 13
                && offenseRewards.State.RareFacilityBuildingIds.Contains(9191)
                && offenseRewards.State.AcquiredBlueprintIds.Contains(8181),
                "Offense reward state did not round-trip.");
            OffenseExpeditionRun restoredExpedition = expeditions.ActiveExpeditions
                .FirstOrDefault(active => active.ExpeditionId == ExpeditionMarker);
            Require(report.RestoredExpeditionCount == 1
                && restoredExpedition != null
                && Mathf.Approximately(restoredExpedition.TotalPower, 77f)
                && restoredExpedition.MemberActors.Count == 1
                && restoredExpedition.MemberActors[0].IsOnExpedition
                && restoredExpedition.Phase == OffenseExpeditionPhase.ChoosingRoute
                && !offenseBattle.HasActiveBattle,
                "Active multi-node expedition did not restore at its route choice with its member. "
                + $"restored={report.RestoredExpeditionCount} active={expeditions.ActiveExpeditions.Count} "
                + $"members={restoredExpedition?.MemberActors.Count ?? 0} "
                + $"battle={offenseBattle.Session?.BattleId} actor={offenseBattle.Session?.CurrentActor?.PersistentId} "
                + $"warnings={string.Join(" | ", report.Warnings)}");
            CharacterActor restoredEquipmentMember = restoredExpedition?.MemberActors.FirstOrDefault();
            Require(restoredEquipmentMember != null
                    && restoredEquipmentMember.Identity?.PersistentId == equipmentStaffId
                    && Mathf.Approximately(
                        restoredEquipmentMember.Lifecycle?.ExpeditionRecovery?.stress ?? -1f,
                        ExpeditionStressMarker)
                    && restoredEquipmentMember.Progression != null
                    && restoredEquipmentMember.Progression.Level >= 2
                    && restoredEquipmentMember.Progression.GrowthState.allocationRecords.Count > 0,
                "Expedition recovery stress or growth allocation records did not round-trip on the restored member.");
            CharacterCombatLoadoutProfile restoredLoadout =
                equipmentRuntime.GetActiveProfileSnapshot(equipmentStaffId);
            Require(restoredLoadout != null
                    && restoredLoadout.activeWeaponInstanceId
                        == EquipmentWeaponInstanceMarker
                    && restoredLoadout.armorInstanceIds.Contains(
                        EquipmentArmorInstanceMarker)
                    && equipmentRuntime.GetAvailableCount(EquipmentWeaponMarker) == 1
                    && equipmentRuntime.GetAvailableCount(EquipmentArmorMarker) == 0
                    && equipmentRuntime.CraftQueue.Any(order => order != null
                        && order.orderId == EquipmentCraftOrderMarker
                        && order.definitionId == EquipmentCraftMarker
                        && Mathf.Approximately(order.RemainingWork, 3.25f)),
                "Combat equipment instances, ownership, or work queue did not round-trip.");
            Require(expeditions.ResultHistory.Any(result => result.expeditionId == ExpeditionResultMarker
                && result.success
                && result.members.Count == 1
                && result.rewardSummaries.Contains("Save QA reward")),
                "Offense expedition result history did not round-trip.");
            InvasionThreatPersistenceState restoredThreat = invasionThreat.CapturePersistentState();
            Require(Mathf.Approximately(restoredThreat.CurrentThreat, 84f)
                && Mathf.Approximately(restoredThreat.SecondsSinceLastInvasion, 123f)
                && Mathf.Approximately(restoredThreat.CandidateDelayRemaining, 4f)
                && Mathf.Approximately(restoredThreat.WarningCooldownRemaining, 7f)
                && restoredThreat.WarningRaisedThisCycle
                && !restoredThreat.CandidateRaisedThisCycle
                && Mathf.Approximately(restoredThreat.ResidualRisk, 2f)
                && Mathf.Approximately(restoredThreat.LastFactors.dungeonValue, 5f),
                "Invasion threat cycle state did not round-trip.");
            InvasionIntruderRuntime restoredIntruder = invasionDirector.ActiveIntruders.FirstOrDefault();
            DefenseStatusRuntime restoredIntruderStatuses = restoredIntruder != null
                ? defenseStatusRuntimeService.Get(restoredIntruder.IntruderActor)
                : null;
            string restoredStatusSummary = restoredIntruderStatuses == null
                ? string.Empty
                : string.Join(
                    ",",
                    restoredIntruderStatuses.ActiveStatuses.Select(status =>
                        $"{status.Kind}:{status.Value:0.###}:{status.Stacks}"));
            Require(report.RestoredIntruderCount == 1
                && invasionDirector.ActiveIntruders.Count == 1
                && restoredIntruder != null
                && restoredIntruder.State == InvasionIntruderState.Rallying
                && restoredIntruder.RallySecondsRemaining > 0f
                && !restoredIntruder.HasBreachedDungeonInterior
                && restoredIntruder.Pattern.id == savedIntruderPatternId
                && restoredIntruder.FacilityDamageCount == 1
                && Mathf.Approximately(restoredIntruder.IntruderActor.CurrentHealth, savedIntruderHealth)
                && restoredIntruderStatuses != null
                && restoredIntruderStatuses.ActiveStatuses.Any(status => status.Kind == DefenseStatusKind.Burn
                    && Mathf.Approximately(status.Value, 1.5f)
                    && status.Stacks == 2),
                "Active invasion intruder did not round-trip with health and defense statuses. "
                + $"reported={report.RestoredIntruderCount}, "
                + $"active={invasionDirector.ActiveIntruders.Count}, "
                + $"state={restoredIntruder?.State}, "
                + $"rally={restoredIntruder?.RallySecondsRemaining:0.###}, "
                + $"breached={restoredIntruder?.HasBreachedDungeonInterior}, "
                + $"pattern={restoredIntruder?.Pattern?.id}/{savedIntruderPatternId}, "
                + $"facilityDamage={restoredIntruder?.FacilityDamageCount}, "
                + $"health={restoredIntruder?.IntruderActor?.CurrentHealth:0.###}/"
                + $"{savedIntruderHealth:0.###}, "
                + $"statuses={restoredStatusSummary}.");
            Require(report.Warnings.Count == 0,
                "V16 restore emitted warnings: " + string.Join(" | ", report.Warnings));

            DungeonGameSaveData legacyWithoutDoctrine = saveService.FromJson(json);
            DungeonRunVariableSaveData legacyRunVariables =
                DungeonSaveSectionPayload.ReadOrNew<DungeonRunVariableSaveData>(
                    legacyWithoutDoctrine,
                    RunVariableSaveSection.Id);
            legacyRunVariables.startVariables.ownerDoctrineId = string.Empty;
            DungeonSaveSectionPayload.Write(
                legacyWithoutDoctrine,
                RunVariableSaveSection.Id,
                1,
                DungeonSaveRestorePhase.Foundation,
                legacyRunVariables);
            Require(
                saveService.TryRestore(legacyWithoutDoctrine, out DungeonGameRestoreReport legacyReport),
                "Legacy owner doctrine fallback restore failed: " + string.Join(" | ", legacyReport.Errors));
            Require(run.State.StartVariables != null
                    && run.State.StartVariables.ownerDoctrineId == savedOwnerDoctrineId,
                "Legacy save did not infer the owner doctrine from the owner species.");

            if (report.Warnings.Count > 0)
            {
                Debug.Log("SAVE_ROUND_TRIP_REPORT_WARNINGS " + string.Join(" | ", report.Warnings));
            }

            Debug.Log(
                $"SAVE_ROUND_TRIP PASS jsonBytes={json.Length} "
                + $"buildings={report.RestoredBuildingCount} characters={report.RestoredCharacterCount} "
                + $"expeditions={report.RestoredExpeditionCount} "
                + $"intruders={report.RestoredIntruderCount} "
                + $"warnings={report.Warnings.Count}");
        }
        finally
        {
            if (!saveService.TryRestore(baseline, out DungeonGameRestoreReport cleanupReport))
            {
                Debug.LogError("Save QA baseline restore failed: " + string.Join(" | ", cleanupReport.Errors));
            }

            if (temporaryOffenseStaffData != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryOffenseStaffData);
            }
        }
    }

    [MenuItem("DungeonStory/Debug/Save/Run File Slot Round Trip")]
    public static void RunFileSlotRoundTrip()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException("Enter PlayMode before running the file slot round trip.");
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            throw new InvalidOperationException("DungeonRuntimeLifetimeScope is not ready.");
        }

        const string SlotId = "qa_round_trip";
        IDungeonGameSaveSlotService slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        IGameDataProvider gameDataProvider = scope.Container.Resolve<IGameDataProvider>();
        Require(gameDataProvider.TryGetGameData(out GameData gameData), "GameData runtime is missing.");

        int originalMoney = gameData.holdingMoney.Value;
        try
        {
            gameData.holdingMoney.Value = originalMoney + 271;
            string firstPath = slots.Save(SlotId);
            Require(System.IO.File.Exists(firstPath), "The slot file was not written.");
            Require(slots.HasSave(SlotId), "HasSave did not see the written slot.");

            string secondPath = slots.Save(SlotId);
            Require(firstPath == secondPath, "Overwriting changed the slot path.");

            gameData.holdingMoney.Value = 0;
            Require(slots.TryLoad(SlotId, out DungeonGameRestoreReport report),
                "File slot load failed: " + string.Join(" | ", report.Errors));
            Require(gameData.holdingMoney.Value == originalMoney + 271, "The file slot did not restore money.");

            DungeonSaveSlotInfo info = slots.GetSlots().FirstOrDefault(candidate => candidate.SlotId == SlotId);
            Require(info != null && info.IsValid, "The written slot metadata is invalid.");
            Require(info.Money == originalMoney + 271, "The slot metadata money is stale.");

            Debug.Log(
                $"SAVE_FILE_SLOT PASS path={firstPath} buildings={report.RestoredBuildingCount} "
                + $"characters={report.RestoredCharacterCount}");
        }
        finally
        {
            gameData.holdingMoney.Value = originalMoney;
            slots.Delete(SlotId);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
