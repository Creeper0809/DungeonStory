using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class OffenseJourneyDebugScenarios
{
    private const string AppraisalFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF42_부품_감정대.asset";
    private const string RestorationFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF43_부품_복원_작업대.asset";
    private const string PrecisionFittingFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF44_정밀_장착대.asset";

    [MenuItem("DungeonStory/Debug/Offense/Run Journey Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true)) Debug.LogError("Offense journey scenarios failed.");
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("branching event and loot", VerifyBranchingEventAndLoot, errors);
        Run("low light stress", VerifyLowLightStress, errors);
        Run("camp recovery and formation", VerifyCampRecoveryAndFormation, errors);
        Run("experience pacing", VerifyExperiencePacing, errors);
        Run("return recovery readiness", VerifyReturnRecoveryReadiness, errors);
        Run("return resource retry commits once",
            VerifyReturnResourceRetryCommitsOnce,
            errors);
        Run("expedition death loses equipment modules together",
            VerifyExpeditionDeathLosesEquipmentModulesTogether,
            errors);
        Run("journey state restore", VerifyJourneyStateRestore, errors);
        Run("offense save payload round trip", VerifySavePayloadRoundTrip, errors);

        foreach (string error in errors) Debug.LogError(error);
        if (errors.Count == 0 && logSuccess) Debug.Log("Offense journey scenarios passed.");
        return errors.Count == 0;
    }

    private static bool VerifyBranchingEventAndLoot()
    {
        using ActorFixture fixture = new ActorFixture("Route Tester");
        OffenseSupplyLoadout supplies = Loadout(
            (OffenseSupplyType.Rations, 4),
            (OffenseSupplyType.Tools, 1));
        OffenseExpeditionRun run = CreateRun(fixture.Actor, supplies);

        Require(run.GetAvailableRouteNodes().Count == 2, "Entrance did not expose two route choices.");
        OffenseRouteNode eventNode = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        Require(run.TryEnterNode(eventNode.Id, out _), "Could not enter the event branch.");
        Require(run.Phase == OffenseExpeditionPhase.ResolvingNode, "Event did not enter resolving phase.");
        Require(supplies.Get(OffenseSupplyType.Rations) == 3, "Travel did not consume one ration.");
        Require(run.TryResolveCurrentNode(true, out OffenseExpeditionNodeResult result, out _),
            "Tool-backed event choice failed.");
        Require(result.UsedSupply && result.GainedLoot, "Event result did not record supply and loot.");
        Require(supplies.Get(OffenseSupplyType.Tools) == 0, "Event did not consume a tool.");
        Require(run.CarriedStock.GetValueOrDefault(StockCategory.General) > 0,
            "Event did not add carried stock.");
        Require(run.Phase == OffenseExpeditionPhase.ChoosingRoute,
            "Resolved event did not return to route choice.");
        return true;
    }

    private static bool VerifyLowLightStress()
    {
        using ActorFixture fixture = new ActorFixture("Low Light Tester");
        OffenseExpeditionRun run = new OffenseExpeditionRun(
            "journey:dark",
            Target(),
            new[] { fixture.Actor },
            10f,
            100f,
            null,
            new OffenseSupplyLoadout(),
            new OffenseExpeditionPreparation(startingLight: 10f));
        OffenseRouteNode battle = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Battle);
        Require(run.TryEnterNode(battle.Id, out _), "Could not enter dark battle route.");
        Require(run.MemberStates[0].Stress >= 10f,
            "Missing rations and low light did not accumulate stress.");
        Require(run.Light <= 0.01f, "Travel did not drain low starting light.");
        return true;
    }

    private static bool VerifyCampRecoveryAndFormation()
    {
        using ActorFixture first = new ActorFixture("Front Tester");
        using ActorFixture second = new ActorFixture("Rear Tester");
        first.Actor.ApplyDamage(50f, "journey test");
        OffenseExpeditionRun run = new OffenseExpeditionRun(
            "journey:camp",
            Target(),
            new[] { first.Actor, second.Actor },
            20f,
            100f,
            null,
            Loadout((OffenseSupplyType.Rations, 5)),
            new OffenseExpeditionPreparation(campHealRatio: 0.25f, campStressRecovery: 25f));

        OffenseRouteNode eventNode = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        Require(run.TryEnterNode(eventNode.Id, out _), "Could not enter camp approach event.");
        Require(run.TryResolveCurrentNode(false, out _, out _), "Could not resolve camp approach.");
        OffenseRouteNode camp = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Camp);
        Require(run.TryEnterNode(camp.Id, out _), "Could not enter camp.");
        float healthBeforeCamp = first.Actor.CurrentHealth;
        Require(run.TryResolveCurrentNode(true, out _, out _), "Camp supply choice failed.");
        Require(first.Actor.CurrentHealth > healthBeforeCamp, "Camp did not heal the injured member.");
        Require(run.MemberStates.All(member => member.Stress < 15f), "Camp did not recover stress.");
        run.MemberStates[1].Restore(OffenseFormationSlot.Rear, 0f, 0f);
        Require(run.TrySwapFormation(0, 1, out _), "Formation swap failed between nodes.");
        Require(run.MemberStates[0].Actor == second.Actor
            && run.MemberStates[0].Formation == OffenseFormationSlot.Front,
            "Formation order did not follow the swap.");
        return true;
    }

    private static bool VerifyExperiencePacing()
    {
        int level50Experience = Enumerable.Range(1, CharacterProgression.MaxLevel - 1)
            .Sum(CharacterProgression.GetExperienceRequired);
        Require(level50Experience == 1460,
            $"Level-50 cumulative XP changed to {level50Experience}.");

        int combatByStage2 = SumRouteExperience(1, 2, combatPath: true);
        int combatByStage3 = SumRouteExperience(1, 3, combatPath: true);
        Require(combatByStage2 < level50Experience,
            $"Combat path reached level 50 too early at stage 2 with {combatByStage2} XP.");
        Require(combatByStage3 >= level50Experience,
            $"Combat path did not reach level 50 by stage 3 with {combatByStage3} XP.");

        int safeByStage3 = SumRouteExperience(1, 3, combatPath: false);
        int safeByStage4 = SumRouteExperience(1, 4, combatPath: false);
        Require(safeByStage3 < level50Experience,
            $"Safe path reached level 50 too early at stage 3 with {safeByStage3} XP.");
        Require(safeByStage4 >= level50Experience,
            $"Safe path did not reach level 50 by stage 4 with {safeByStage4} XP.");
        return true;
    }

    private static int SumRouteExperience(int firstStage, int lastStage, bool combatPath)
    {
        int total = 0;
        for (int stage = firstStage; stage <= lastStage; stage++)
        {
            OffenseRouteGraph route = OffenseRouteGenerator.Create(Target(stage));
            string[] nodeSuffixes = combatPath
                ? new[] { ":approach-battle", ":camp", ":elite-battle", ":boss" }
                : new[] { ":approach-event", ":camp", ":deep-event", ":boss" };
            foreach (string suffix in nodeSuffixes)
            {
                OffenseRouteNode node = route.Nodes.First(item => item.Id.EndsWith(suffix, StringComparison.Ordinal));
                total += OffenseExpeditionRuntime.CalculateNodeExperience(node, stage);
            }

            total += OffenseExpeditionRuntime.CalculateSuccessfulReturnExperience(stage);
        }

        return total;
    }

    private static bool VerifyReturnRecoveryReadiness()
    {
        using ActorFixture fixture = new ActorFixture("Recovery Tester");
        Require(OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out _),
            "Fresh active staff member could not join an expedition.");

        fixture.Actor.ApplyDamage(fixture.Actor.MaxHealth * 0.9f, "recovery test");
        Require(!OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out string healthReason)
                && healthReason == "expedition-health-too-low",
            $"Low-health expedition member was not blocked. reason={healthReason}");

        BuildingExpeditionRecoveryAbility recovery = new BuildingExpeditionRecoveryAbility
        {
            healthHealRatio = 1f,
            injuryReduction = 0f,
            stressRecovery = 0f
        };
        recovery.ApplyUseCompleted(fixture.Actor, null);
        Require(OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out _),
            "Health recovery did not restore expedition readiness.");

        fixture.Actor.Lifecycle.RecordExpeditionReturn(85f, alive: true);
        Require(!OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out string stressReason)
                && stressReason == "expedition-stress-too-high",
            $"High-stress expedition member was not blocked. reason={stressReason}");

        recovery.stressRecovery = 90f;
        recovery.ApplyUseCompleted(fixture.Actor, null);
        Require(OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out _),
            "Stress recovery did not restore expedition readiness.");

        fixture.Actor.Lifecycle.RecordExpeditionReturn(42f, alive: true);
        DungeonCharacterSaveData saved = new DungeonCharacterSaveData
        {
            expeditionRecovery = fixture.Actor.Lifecycle.ExpeditionRecovery.Clone()
        };
        string json = JsonUtility.ToJson(saved);
        DungeonCharacterSaveData restored = JsonUtility.FromJson<DungeonCharacterSaveData>(json);
        Require(restored.expeditionRecovery != null
                && Mathf.Approximately(restored.expeditionRecovery.stress, 42f),
            "Expedition recovery state did not survive JSON save data.");
        return true;
    }

    private static bool VerifyReturnResourceRetryCommitsOnce()
    {
        VerifyCommittedReturnStageIsReused();

        using ActorFixture fixture = new ActorFixture("Return Retry Tester");
        OffenseExpeditionRun run = CreateRun(
            fixture.Actor,
            new OffenseSupplyLoadout());
        RetryableReturnPort returnPort = new RetryableReturnPort();
        RecordingResultFinalizer finalizer = new RecordingResultFinalizer();
        OffenseExpeditionReturnCoordinator coordinator = new(
            returnPort,
            finalizer,
            new GameEventBus(),
            CharacterAiEditorTestDependencies.NeutralPerformance);
        List<OffenseExpeditionResult> history = new();
        int levelBefore = fixture.Actor.Progression.Level;
        int experienceBefore = fixture.Actor.Progression.CurrentExperience;

        coordinator.Complete(
            run,
            true,
            "retry-proof",
            history,
            null);
        Require(run.ReturnPending
                && !run.ReturnFinalized
                && !run.ReturnResourcesCommitted
                && !string.IsNullOrEmpty(run.ReturnResourceFailure)
                && history.Count == 0
                && finalizer.CallCount == 0
                && returnPort.PhysicalStock == 2
                && fixture.Actor.Progression.Level == levelBefore
                && fixture.Actor.Progression.CurrentExperience
                    == experienceBefore,
            "A failed return output commit latched finalization or granted result/XP before the retry.");

        coordinator.Complete(
            run,
            true,
            "retry-proof",
            history,
            null);
        Require(run.ReturnFinalized
                && run.ReturnResourcesCommitted
                && string.IsNullOrEmpty(run.ReturnResourceFailure)
                && history.Count == 1
                && finalizer.CallCount == 1
                && returnPort.PhysicalStock == 2
                && run.ReturnItemReceipts.Count == 1
                && history[0].itemReceipts.Count == 1
                && (fixture.Actor.Progression.Level > levelBefore
                    || (fixture.Actor.Progression.Level == levelBefore
                        && fixture.Actor.Progression.CurrentExperience
                            > experienceBefore)),
            "The return retry did not publish one physical receipt, result, and XP grant.");
        int levelAfter = fixture.Actor.Progression.Level;
        int experienceAfter = fixture.Actor.Progression.CurrentExperience;
        coordinator.Complete(
            run,
            true,
            "retry-proof",
            history,
            null);
        Require(history.Count == 1
                && finalizer.CallCount == 1
                && returnPort.PhysicalStock == 2
                && fixture.Actor.Progression.Level == levelAfter
                && fixture.Actor.Progression.CurrentExperience
                    == experienceAfter,
            "A completed return retry duplicated physical stock, result history, or XP.");
        return true;
    }

    private static void VerifyCommittedReturnStageIsReused()
    {
        using ActorFixture fixture = new ActorFixture("Return Stage Tester");
        OffenseSupplyLoadout supplies = new();
        supplies.Add(OffenseSupplyType.Rations, 2);
        OffenseExpeditionRun run = CreateRun(fixture.Actor, supplies);
        run.AddCarriedLoot(StockCategory.General, 3);
        StageRetryPreparation preparation = new();
        ChangingWorkValueProjection values = new();
        OffenseExpeditionReturnPort port = new(
            preparation,
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IExpeditionReturnService>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IOffenseReturnArrivalRuntime>(),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IGameMoneyAccount>(),
            new GameEventBus(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IWorldDropZoneQuery>(),
            values);

        bool firstFailed = false;
        try
        {
            port.ReleaseResourcesWithReceipt(run, hasSurvivor: true);
        }
        catch (InvalidOperationException)
        {
            firstFailed = true;
        }

        OffenseExpeditionItemReceipt committedSupply = run.ReturnItemReceipts
            .Single(value => value.kind
                == OffenseExpeditionItemReceiptKind.SupplyReturned);
        string committedBasis = committedSupply.valuation.basisId;
        Require(firstFailed
                && preparation.SupplyCalls == 1
                && preparation.LootCalls == 1
                && values.QueryCount == 1
                && !run.ReturnResourcesCommitted,
            "The fixture did not stop after the committed supply stage and failed loot stage.");

        port.ReleaseResourcesWithReceipt(run, hasSurvivor: true);
        Require(run.ReturnResourcesCommitted
                && preparation.SupplyCalls == 1
                && preparation.LootCalls == 2
                && values.QueryCount == 2
                && ReferenceEquals(
                    committedSupply,
                    run.ReturnItemReceipts.Single(value => value.kind
                        == OffenseExpeditionItemReceiptKind.SupplyReturned))
                && string.Equals(
                    committedBasis,
                    committedSupply.valuation.basisId,
                    StringComparison.Ordinal),
            "A retry republished or revalued an already committed physical return stage.");
    }

    private static bool VerifyExpeditionDeathLosesEquipmentModulesTogether()
    {
        using ActorFixture fixture = new ActorFixture("Equipment Loss Tester");
        WorldItemStackRuntime physicalItems =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out _,
                out CombatEquipmentRuntime equipment);
        physicalItems.Start();
        List<GameObject> facilityObjects = new List<GameObject>();
        BuildableObject appraisal = CreateProgressionFacility(
            AppraisalFacilityPath,
            "OffenseJourney_Appraisal",
            new Vector2Int(60, 60),
            facilityObjects);
        BuildableObject restoration = CreateProgressionFacility(
            RestorationFacilityPath,
            "OffenseJourney_Restoration",
            new Vector2Int(62, 60),
            facilityObjects);
        BuildableObject fitting = CreateProgressionFacility(
            PrecisionFittingFacilityPath,
            "OffenseJourney_Fitting",
            new Vector2Int(64, 60),
            facilityObjects);
        string appraisalDestination = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(appraisal);
        string restorationDestination = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(restoration);
        string fittingDestination = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(fitting);

        CombatEquipmentInstance weapon = equipment.CreateInstance(
            "weapon:greatsword",
            CombatEquipmentQuality.Good);
        EquipmentModuleInstance module = equipment.CreateExpeditionModule(
            "module:weapon:balanced-core",
            3,
            appraisal.centerPos,
            WorldItemStackState.FacilityBuffer,
            appraisalDestination);
        foreach (string supplyItemId in new[]
                 {
                     "component:material-test-coupon",
                     DurableToolItemRules.InspectionGauge,
                     DurableToolItemRules.RuneIdentificationLens
                 })
        {
            Require(physicalItems.SpawnItemAt(
                    supplyItemId,
                    1,
                    appraisal.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    appraisalDestination,
                    out int spawnedSupply)
                    && spawnedSupply == 1,
                $"failed to supply expedition module appraisal item: {supplyItemId}");
        }
        Require(equipment.TryAppraiseModule(
                module.instanceId,
                appraisal,
                out DomainFailure appraiseFailure),
            $"expedition module appraisal failed: {appraiseFailure.Code}");
        Require(physicalItems.TryRouteStackToDestination(
                module.sourceStackId,
                WorldItemStackState.FacilityBuffer,
                restorationDestination,
                restoration.centerPos,
                out string restorationRouteFailure),
            $"expedition module restoration routing failed: {restorationRouteFailure}");
        Require(equipment.TryRestoreModule(
                module.instanceId,
                restoration,
                out DomainFailure restoreFailure),
            $"expedition module restoration failed: {restoreFailure.Code}");
        Require(physicalItems.TryRouteStackToDestination(
                module.sourceStackId,
                WorldItemStackState.FacilityBuffer,
                fittingDestination,
                fitting.centerPos,
                out string fittingRouteFailure),
            $"expedition module fitting routing failed: {fittingRouteFailure}");
        Require(physicalItems.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipment(weapon.definitionId),
                (ItemInstanceId)weapon.instanceId,
                fitting.centerPos,
                WorldItemStackState.FacilityBuffer,
                fittingDestination,
                out string weaponStackId)
                && equipment.TryLinkToWorldStack(
                    weapon.instanceId,
                    weaponStackId,
                    CombatEquipmentWorldState.Stored),
            "expedition equipment was not materialized in the fitting buffer");
        Require(equipment.TryInstallModule(
                weapon.instanceId,
                module.instanceId,
                0,
                fitting,
                out DomainFailure installFailure),
            $"expedition module installation failed: {installFailure.Code}");

        string characterId = fixture.Actor.Identity.PersistentId;
        Require(equipment.TryAssignToCharacter(
                characterId,
                weapon.instanceId,
                out string assignFailure),
            $"expedition equipment assignment failed: {assignFailure}");

        OffenseExpeditionReturnPort returnPort = new OffenseExpeditionReturnPort(
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IOffensePreparationService>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IExpeditionReturnService>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IOffenseReturnArrivalRuntime>(),
            equipment,
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IGameMoneyAccount>(),
            new GameEventBus(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IWorldDropZoneQuery>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IV27EmbeddedWorkValueProjectionQuery>());
        returnPort.HandleMemberDeath(fixture.Actor);

        Require(equipment.TryGetInstance(
                weapon.instanceId,
                out CombatEquipmentInstance lostEquipment)
            && lostEquipment.worldState == CombatEquipmentWorldState.Lost
            && string.IsNullOrWhiteSpace(lostEquipment.ownerCharacterId)
            && lostEquipment.moduleSlots.Any(slot => slot != null
                && slot.moduleInstanceId == module.instanceId),
            "actual expedition death path did not lose the equipped item with its slot payload");
        Require(equipment.ModuleInstances.Any(candidate => candidate != null
                && candidate.instanceId == module.instanceId
                && candidate.state == EquipmentModuleProcessState.Lost
                && Mathf.Approximately(candidate.condition, 0f)
                && string.IsNullOrWhiteSpace(candidate.attachedEquipmentInstanceId)),
            "installed module did not become lost with its expedition equipment");
        CharacterCombatLoadoutProfile loadout = equipment.GetActiveProfileSnapshot(characterId);
        Require(loadout == null
                || !loadout.weaponInstanceIds.Contains(weapon.instanceId),
            "lost expedition equipment remained in the character loadout");
        physicalItems.Dispose();
        foreach (GameObject facilityObject in facilityObjects)
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
        return true;
    }

    private static bool VerifyJourneyStateRestore()
    {
        using ActorFixture fixture = new ActorFixture("Restore Tester");
        OffenseExpeditionRun source = CreateRun(
            fixture.Actor,
            Loadout((OffenseSupplyType.Rations, 4), (OffenseSupplyType.Tools, 1)));
        OffenseRouteNode eventNode = source.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        source.TryEnterNode(eventNode.Id, out _);
        source.TryResolveCurrentNode(false, out _, out _);
        source.AddRecoveredEquipment("equipment:recovered:test");

        OffenseExpeditionRun restored = CreateRun(
            fixture.Actor,
            new OffenseSupplyLoadout(source.Supplies.Amounts));
        restored.MemberStates[0].Restore(
            OffenseFormationSlot.Rear,
            source.MemberStates[0].Stress,
            12f);
        restored.RestoreJourneyState(
            source.Phase,
            source.CurrentNodeId,
            source.Light,
            source.CompletedNodeIds,
            source.CarriedStock);
        restored.RestoreRecoveredEquipment(
            source.RecoveredEquipmentInstanceIds);

        Require(restored.Phase == source.Phase, "Journey phase changed during restore.");
        Require(restored.CurrentNodeId == source.CurrentNodeId, "Current route node changed during restore.");
        Require(Mathf.Approximately(restored.Light, source.Light), "Light changed during restore.");
        Require(new HashSet<string>(restored.CompletedNodeIds).SetEquals(source.CompletedNodeIds),
            "Completed route nodes changed during restore.");
        Require(restored.MemberStates[0].Formation == OffenseFormationSlot.Rear
            && Mathf.Approximately(restored.MemberStates[0].TotalDamageTaken, 12f),
            "Member journey state did not restore.");
        Require(new HashSet<string>(restored.RecoveredEquipmentInstanceIds)
                .SetEquals(source.RecoveredEquipmentInstanceIds),
            "Recovered physical equipment IDs changed during restore.");
        return true;
    }

    private static bool VerifySavePayloadRoundTrip()
    {
        using ActorFixture fixture = new ActorFixture("Save Payload Tester");
        OffenseExpeditionRun run = CreateRun(
            fixture.Actor,
            Loadout(
                (OffenseSupplyType.Rations, 4),
                (OffenseSupplyType.Tools, 1),
                (OffenseSupplyType.Medicine, 1)));
        ICombatEquipmentRuntime settlementEquipment =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        CombatEquipmentInstance settlementWeapon =
            settlementEquipment.CreateExternalInstance(
                "weapon:crossbow",
                CombatEquipmentQuality.Normal);
        Require(settlementEquipment.TryAssignToCharacter(
                fixture.Actor.Identity.PersistentId,
                settlementWeapon.instanceId,
                out string equipmentFailure),
            $"Settlement equipment setup failed: {equipmentFailure}");
        run.CaptureEquipmentBaseline(settlementEquipment);
        run.RecordAmmunitionConsumption(
            settlementWeapon.instanceId,
            "ammo:bolt-iron",
            2);
        OffenseRouteNode eventNode = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        run.TryEnterNode(eventNode.Id, out _);
        run.TryResolveCurrentNode(false, out _, out _);
        fixture.Actor.ApplyDamage(20f, "settlement receipt test");
        Require(run.TryUseSupply(OffenseSupplyType.Medicine, 0, out _),
            "Medicine receipt setup failed.");
        run.MemberStates[0].Restore(OffenseFormationSlot.Rear, 37f, 14f);
        run.AddRecoveredEquipment("equipment:save-recovered:test");
        run.RestorePhysicalReturn(
            true,
            true,
            "return-resource-save-proof",
            new[]
            {
                new ExpeditionReturnProgress(
                    fixture.Actor.Identity.PersistentId,
                    ExpeditionReturnStage.Arrived)
            });
        run.RestoreReturnResourceSettlement(
            false,
            "return-resource-retry-pending",
            new[]
            {
                new OffenseExpeditionItemReceipt(
                    OffenseExpeditionItemReceiptKind.SupplyReturned,
                    OffenseSupplyCatalog.GetPhysicalItemId(
                        OffenseSupplyType.Rations),
                    1,
                    string.Empty,
                    0f,
                    OffenseExpeditionRun.CreateValuation(
                        null,
                        OffenseSupplyCatalog.GetPhysicalItemId(
                            OffenseSupplyType.Rations),
                        1))
            },
            Array.Empty<OffenseExpeditionCurrencyReceipt>());

        OffenseExpeditionResult settlementResult = new OffenseExpeditionResult(
                "result:settlement:test",
                Target().id,
                Target().title,
                true,
                10f,
                10f,
                1f,
                12f,
                Array.Empty<OffenseExpeditionMemberSnapshot>(),
                Array.Empty<string>())
            .WithSettlement(
                run.FreezeItemReceipts(settlementEquipment, null),
                run.TreatmentReceipts)
            .WithGrantedRewards(new[]
            {
                new OffenseRewardGrantResult(
                    OffenseRewardCategory.Money,
                    "receipt reward",
                    3,
                    3,
                    true,
                    "committed",
                    new[]
                    {
                        new OffenseRewardPhysicalItemGrant(
                            OffenseLootItemIds.UnappraisedLoot,
                            3)
                    })
            })
            .WithAdditionalItemReceipts(new[]
            {
                new OffenseExpeditionItemReceipt(
                    OffenseExpeditionItemReceiptKind.RewardGranted,
                    OffenseLootItemIds.UnappraisedLoot,
                    3,
                    string.Empty,
                    0f,
                    OffenseExpeditionRun.CreateValuation(
                        null,
                        OffenseLootItemIds.UnappraisedLoot,
                        3))
            })
            .WithAdditionalCurrencyReceipts(new[]
            {
                new OffenseExpeditionCurrencyReceipt(
                    OffenseSettlementCurrencyIds.Gold,
                    7,
                    "field-funds:return:save-fixture")
            });

        GameObject runtimeObject = new GameObject("Offense Save Payload Runtime");
        try
        {
            OffenseExpeditionRuntime runtime = runtimeObject.AddComponent<OffenseExpeditionRuntime>();
            OffenseWorldMapRuntime worldMap = runtimeObject.AddComponent<OffenseWorldMapRuntime>();
            OffenseRewardRuntime rewards = runtimeObject.AddComponent<OffenseRewardRuntime>();
            runtime.PublishRestoreCandidate(
                runtime.BuildRestoreCandidate(
                    new List<OffenseExpeditionRun> { run },
                    new List<OffenseExpeditionResult> { settlementResult }));
            FakeCharacterSaveService characterSave = new FakeCharacterSaveService(
                fixture.Actor,
                fixture.Actor.Identity.PersistentId);
            OffenseSaveService service = new OffenseSaveService(
                new OffenseSceneRuntimeReferences(
                    worldMap,
                    rewards,
                    runtime,
                    null,
                    null),
                new ResourceOffenseCampaignCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                characterSave,
                new EmptyBattleRuntime());

            DungeonOffenseSaveData captured = service.Capture();
            string json = JsonUtility.ToJson(captured);
            DungeonOffenseSaveData restored = JsonUtility.FromJson<DungeonOffenseSaveData>(json);
            DungeonOffenseExpeditionRunSaveData saved = restored.activeExpeditions.Single();
            DungeonOffenseExpeditionMemberStateSaveData savedMember = saved.memberStates.Single();

            Require(saved.journeyVersion == DungeonOffenseExpeditionRunSaveData.CurrentVersion, "Journey save version was not written.");
            Require(saved.phase == run.Phase && saved.currentNodeId == run.CurrentNodeId,
                "Journey phase or current node did not round-trip.");
            Require(Mathf.Approximately(saved.light, run.Light), "Journey light did not round-trip.");
            Require(new HashSet<string>(saved.completedNodeIds).SetEquals(run.CompletedNodeIds),
                "Completed route nodes did not round-trip.");
            Require(saved.supplies.Sum(entry => entry.amount) == run.Supplies.TotalCount,
                "Remaining supplies did not round-trip.");
            Require(savedMember.formation == OffenseFormationSlot.Rear
                && Mathf.Approximately(savedMember.stress, 37f)
                && Mathf.Approximately(savedMember.totalDamageTaken, 14f),
                "Member formation, stress, or damage did not round-trip.");
            Require(saved.recoveredEquipmentInstanceIds.SequenceEqual(
                    new[] { "equipment:save-recovered:test" }),
                "Recovered physical equipment IDs did not round-trip.");
            Require(saved.consumedSupplies.Any(value =>
                    value.type == OffenseSupplyType.Rations && value.amount == 1)
                && saved.consumedSupplies.Any(value =>
                    value.type == OffenseSupplyType.Medicine && value.amount == 1),
                "Committed supply receipts did not round-trip.");
            Require(saved.treatmentReceipts.Count == 1
                    && saved.treatmentReceipts[0].kind
                        == OffenseExpeditionTreatmentKind.Healing
                    && saved.treatmentReceipts[0].healedAmount > 0f,
                "Actual healing receipt did not round-trip.");
            Require(saved.equipmentBaselines.Count == 1
                    && saved.ammunitionConsumptions.Count == 1
                    && saved.ammunitionConsumptions[0].instanceId
                        == settlementWeapon.instanceId
                    && saved.ammunitionConsumptions[0].itemId
                        == "ammo:bolt-iron"
                    && saved.ammunitionConsumptions[0].quantity == 2,
                "Committed ammunition consumption did not round-trip with its equipment owner.");
            Require(saved.returnPending
                    && !saved.returnResourcesCommitted
                    && saved.returnResourceFailure
                        == "return-resource-retry-pending"
                    && saved.returnItemReceipts.Count == 1
                    && saved.returnItemReceipts[0].kind
                        == OffenseExpeditionItemReceiptKind.SupplyReturned,
                "Retryable return resource state and its successful partial receipt did not round-trip.");
            DungeonOffenseExpeditionResultSaveData savedResult =
                restored.resultHistory.Single();
            Require(savedResult.grantedRewards.Count == 1
                    && savedResult.grantedRewards[0].grantedAmount == 3
                    && savedResult.grantedRewards[0].physicalItems.Count == 1
                    && savedResult.itemReceipts.Count >= 3
                    && savedResult.treatmentReceipts.Count == 1
                    && savedResult.currencyReceipts.Count == 1
                    && savedResult.currencyReceipts[0].amount == 7,
                "Immutable settlement result details did not round-trip.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(runtimeObject);
        }
    }

    private static OffenseExpeditionRun CreateRun(CharacterActor actor, OffenseSupplyLoadout supplies)
    {
        return new OffenseExpeditionRun(
            Guid.NewGuid().ToString("N"),
            Target(),
            new[] { actor },
            0f,
            100f,
            null,
            supplies,
            new OffenseExpeditionPreparation());
    }

    private static OffenseTargetDefinition Target()
    {
        return Target(2);
    }

    private static OffenseTargetDefinition Target(int stage)
    {
        return new OffenseTargetDefinition
        {
            id = "journey-test-target",
            title = "Journey Test",
            campaignOrder = Mathf.Clamp(stage, 1, 6),
            requiredMembers = 1,
            requiredPower = 10f,
            durationSeconds = 100f
        };
    }

    private static OffenseSupplyLoadout Loadout(
        params (OffenseSupplyType Type, int Amount)[] values)
    {
        return new OffenseSupplyLoadout(values.ToDictionary(value => value.Type, value => value.Amount));
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario()) errors.Add(name);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            errors.Add($"{name}: {exception.Message}");
        }
    }

    private static BuildableObject CreateProgressionFacility(
        string assetPath,
        string objectName,
        Vector2Int position,
        ICollection<GameObject> created)
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath)
            ?? throw new InvalidOperationException(
                $"Missing progression facility asset '{assetPath}'.");
        GameObject facilityObject = new GameObject(objectName);
        created.Add(facilityObject);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        CharacterAiEditorTestDependencies.Inject(facility);
        facility.Initialization(definition, position);
        return facility;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly GameObject gameObject;

        public ActorFixture(string name)
        {
            CharacterSO data =
                OffenseEditorTestDependencies.RequireCharacterArchetype("Orc");

            gameObject = new GameObject(name);
            gameObject.AddComponent<SpriteRenderer>();
            // CharacterActor binds its required progression component during
            // Awake. Configure that component first so the fixture exercises
            // the same complete-at-creation dependency contract as composition.
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                gameObject);
            Actor = gameObject.AddComponent<CharacterActor>();
            gameObject.AddComponent<AbilityMove>();
            gameObject.AddComponent<AbilityWork>();
            Actor.RefreshAbilityCache();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            Actor.Identity.SetPersistentId(
                new GuidPersistentIdGenerator().NewCharacterId());
            Actor.Initialization(data);
            Actor.characterType = CharacterType.NPC;
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private sealed class RetryableReturnPort :
        IOffenseExpeditionReturnPort,
        IOffenseExpeditionReturnSettlementPort
    {
        private bool physicalOutputCommitted;

        public int PhysicalStock { get; private set; }

        public void Begin(string expeditionId)
        {
        }

        public void ReleaseResources(
            OffenseExpeditionRun expedition,
            bool hasSurvivor) =>
            ReleaseResourcesWithReceipt(expedition, hasSurvivor);

        public OffenseExpeditionReturnReceipt ReleaseResourcesWithReceipt(
            OffenseExpeditionRun expedition,
            bool hasSurvivor)
        {
            if (!physicalOutputCommitted)
            {
                PhysicalStock += 2;
                physicalOutputCommitted = true;
                throw new InvalidOperationException(
                    "fixture-output-publication-interrupted");
            }

            return new OffenseExpeditionReturnReceipt(new[]
            {
                new OffenseExpeditionItemReceipt(
                    OffenseExpeditionItemReceiptKind.SupplyReturned,
                    "item:return-retry-proof",
                    2,
                    string.Empty,
                    0f,
                    new OffenseItemValuationSnapshot(
                        "item:return-retry-proof",
                        2,
                        OffenseSettlementValuationState.UnvaluedItem))
            });
        }

        public bool TryBeginMemberReturn(
            string expeditionId,
            CharacterActor actor,
            Action<ExpeditionReturnOutcome> completed,
            ExpeditionReturnProgress progress = null)
        {
            completed?.Invoke(ExpeditionReturnOutcome.Arrived);
            return true;
        }

        public void EndMemberImmediately(CharacterActor actor, bool survived)
        {
        }

        public void HandleMemberDeath(CharacterActor actor)
        {
        }

        public void Seal(string expeditionId)
        {
        }
    }

    private sealed class StageRetryPreparation :
        IOffensePreparationService,
        IOffensePreparationSettlementPort
    {
        public int SupplyCalls { get; private set; }
        public int LootCalls { get; private set; }

        public IReadOnlyList<OffensePhysicalItemCommitReceipt>
            ReturnSuppliesWithReceipt(
                OffenseSupplyLoadout loadout,
                string packageId)
        {
            SupplyCalls++;
            return new[]
            {
                new OffensePhysicalItemCommitReceipt(
                    OffenseSupplyCatalog.GetPhysicalItemId(
                        OffenseSupplyType.Rations),
                    loadout.Get(OffenseSupplyType.Rations),
                    "return-stage:supply")
            };
        }

        public IReadOnlyList<OffensePhysicalItemCommitReceipt>
            DepositLootWithReceipt(
                IReadOnlyDictionary<StockCategory, int> loot,
                string expeditionId)
        {
            LootCalls++;
            if (LootCalls == 1)
            {
                throw new InvalidOperationException(
                    "fixture-loot-publication-interrupted");
            }

            return new[]
            {
                new OffensePhysicalItemCommitReceipt(
                    OffenseLootItemIds.UnappraisedLoot,
                    loot.Values.Sum(),
                    "return-stage:loot")
            };
        }

        public OffensePreparationSnapshot Evaluate() =>
            throw new NotSupportedException();
        public OffenseSupplyPackingSnapshot GetPackingSnapshot(string packageId) =>
            throw new NotSupportedException();
        public bool IsPackageReady(string packageId) => false;
        public bool TryCommitLoadout(
            OffenseSupplyLoadout loadout,
            OffenseExpeditionPreparation preparation,
            string packageId,
            out string message)
        {
            message = string.Empty;
            return false;
        }
        public bool TryConsumePackedSupplies(
            string packageId,
            out string message)
        {
            message = string.Empty;
            return false;
        }
        public void ConsumePackedSupplies(string packageId) { }
        public void AbandonPackedSupplies(string packageId) { }
        public void ReturnSupplies(
            OffenseSupplyLoadout loadout,
            string packageId = "") =>
            ReturnSuppliesWithReceipt(loadout, packageId);
        public void DepositLoot(IReadOnlyDictionary<StockCategory, int> loot) =>
            DepositLootWithReceipt(loot, "fixture");
        public IReadOnlyList<OffenseSupplyPackingStateData> CapturePackingState() =>
            Array.Empty<OffenseSupplyPackingStateData>();
        public void RestorePackingState(
            IEnumerable<OffenseSupplyPackingStateData> restored,
            DungeonGameRestoreReport report = null) { }
    }

    private sealed class ChangingWorkValueProjection :
        IV27EmbeddedWorkValueProjectionQuery
    {
        public bool AuthorityAvailable => true;
        public string AuthorityFailure => string.Empty;
        public int QueryCount { get; private set; }

        public bool TryGet(
            string itemId,
            out V27EmbeddedWorkValueProjection value)
        {
            QueryCount++;
            value = new V27EmbeddedWorkValueProjection(
                itemId,
                1000L + QueryCount,
                500L + QueryCount,
                $"basis:return-stage-{QueryCount}",
                $"source:return-stage-{QueryCount}");
            return true;
        }
    }

    private sealed class RecordingResultFinalizer :
        IOffenseExpeditionResultFinalizer
    {
        public int CallCount { get; private set; }

        public OffenseExpeditionResult Finalize(
            OffenseExpeditionRun expedition,
            OffenseExpeditionResult result,
            List<OffenseExpeditionResult> resultHistory,
            Action finalizeReturn = null)
        {
            CallCount++;
            resultHistory.Insert(0, result);
            finalizeReturn?.Invoke();
            return result;
        }
    }

    private sealed class FakeCharacterSaveService : ICharacterWorldSaveService
    {
        private readonly CharacterActor actor;
        private readonly string persistentId;

        public FakeCharacterSaveService(CharacterActor actor, string persistentId)
        {
            this.actor = actor;
            this.persistentId = persistentId;
        }

        public DungeonCharacterWorldSaveData Capture(Grid grid) => new DungeonCharacterWorldSaveData();
        public void ValidateRestorePayload(
            Grid grid,
            DungeonCharacterWorldSaveData source) { }
        public CharacterWorldRestoreCandidate PrepareRestoreCandidate(
            Grid grid,
            DungeonCharacterWorldSaveData source) =>
            throw new NotSupportedException();
        public void StageRestoreCandidate(
            CharacterWorldRestoreCandidate candidate) { }
        public bool TryGetPersistentId(CharacterActor candidate, out string value)
        {
            value = ReferenceEquals(candidate, actor) ? persistentId : string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
        public string GetOrAssignPersistentId(CharacterActor candidate)
        {
            return ReferenceEquals(candidate, actor) ? persistentId : string.Empty;
        }
        public bool TryGetRestoredActor(string id, out CharacterActor value)
        {
            value = string.Equals(id, persistentId, StringComparison.Ordinal) ? actor : null;
            return value != null;
        }
    }

    private sealed class EmptyBattleRuntime : IOffenseBattleRuntime
    {
        public OffenseBattleSession Session => null;
        public bool HasActiveBattle => false;
        public bool IsBattleViewVisible => false;
        public event Action StateChanged { add { } remove { } }
        public event Action<OffenseBattleSession> BattleCompleted { add { } remove { } }
        public bool TryStartBattle(OffenseExpeditionRun expedition, out string message)
        {
            message = string.Empty;
            return false;
        }
        public void AdvanceToPlayerDecision() { }
        public bool TryIssuePlayerCommand(OffenseBattleActionType actionType, string targetId, string abilityId, out OffenseBattleCommandResult result)
        {
            result = null;
            return false;
        }
        public bool TryExecuteCommand(OffenseBattleCommand command, out OffenseBattleCommandResult result)
        {
            result = null;
            return false;
        }
        public bool TryExecutePlannedCommand(
            int directorTurn,
            string actorPersistentId,
            string targetPersistentId,
            OffenseBattleActionType actionType,
            string abilityId,
            out OffenseBattleCommandResult result)
        {
            result = null;
            return false;
        }
        public bool FinalizePlannedTurn(
            int directorTurn,
            out string failureReason)
        {
            failureReason = "fixture has no active planned battle";
            return false;
        }
        public bool TryGetActor(string persistentId, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
        public IReadOnlyList<EnemyIndividualSaveData> GetEnemyIndividuals() =>
            Array.Empty<EnemyIndividualSaveData>();
        public OffenseBattlePersistenceState CapturePersistentState() => null;
        public bool TryRestoreBattle(OffenseExpeditionRun expedition, OffenseBattlePersistenceState state, out string message)
        {
            message = string.Empty;
            return false;
        }
        public void ClearForPersistentRestore() { }
        public void SetBattleViewVisible(bool visible) { }
        public void ClearCompletedBattle() { }
    }
}
