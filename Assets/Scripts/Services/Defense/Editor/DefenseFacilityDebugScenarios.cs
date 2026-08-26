using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DefenseFacilityDebugScenarios
{
    private sealed class NoAutomationInfrastructure :
        IAutomationInfrastructureQuery,
        IAutomationInfrastructureCommand
    {
        public static readonly NoAutomationInfrastructure Instance = new();

        public int Version => 0;
        public IReadOnlyList<AutomationFacilitySnapshot> Facilities =>
            Array.Empty<AutomationFacilitySnapshot>();

        public bool TryGetFacility(
            BuildableObject facility,
            out AutomationFacilitySnapshot snapshot)
        {
            snapshot = null;
            return false;
        }

        public float GetWorkSpeedMultiplier(BuildableObject facility) => 1f;

        public InfrastructureCommandResult SetMode(
            BuildableObject facility,
            AutomationMode mode) => InfrastructureCommandResult.Failed(
                FailureCode.AutomationFacilityUnavailable);

        public InfrastructureCommandResult Maintain(
            BuildableObject facility,
            float amount) => InfrastructureCommandResult.Failed(
                FailureCode.AutomationFacilityUnavailable);
    }

    private static readonly IDefenseStatusRuntimeService StatusRuntimeService =
        new DefenseStatusRuntimeService(new DefenseStatusRuntimeFactory());
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IStaffDiscontentRuntimeService StaffDiscontentRuntimeService =
        new NoopStaffDiscontentRuntimeService();
    private static readonly IFloatingIconFeedbackService FloatingIconFeedbackService =
        new NoopFloatingIconFeedbackService();
    private static readonly IWorkGridResolver WorkGridResolver =
        new ScenarioWorkGridResolver();
    private static readonly IFacilityCandidateCache FacilityCandidateCache =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IRoomFacilityPolicy RoomFacilityPolicy =
        new RoomFacilityPolicyService(new RoomLayoutCache());
    private static readonly IOwnerRunLifecycleService OwnerRunLifecycleService =
        new NoopOwnerRunLifecycleService();
    private static readonly IMetaProgressionRuntimeReader MetaProgressionRuntimeReader =
        new ScenarioMetaProgressionRuntimeReader();

    private static readonly string[] DefenseAssetNames =
    {
        "P1_SpikeTrap",
        "P1_PoisonPool",
        "P1_FireVent",
        "P1_LightningPillar",
        "P1_IceVent",
        "P1_GuardRoom"
    };

    [MenuItem("DungeonStory/Debug/Defense/Run P1 Defense Facility Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 defense facility scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("방어 시설 에셋", VerifyDefenseAssets, errors);
        RunScenario("함정 위 통행 경로", VerifyWalkableTrapRoute, errors);
        RunScenario("SO Effect 적용", VerifyEffectAssetsDriveDamage, errors);
        RunScenario("개방형 Effect 전략", VerifyOpenEffectStrategy, errors);
        RunScenario("진입 발동 피해와 이벤트", VerifyTriggerDamageAndEvent, errors);
        RunScenario("발동 이벤트 스냅샷 격리", VerifyEventSnapshotIsolation, errors);
        RunScenario("파손 비활성화와 수리 복구", VerifyDamagedDisableAndRepair, errors);
        RunScenario("독 부식 피해 보정", VerifyPoisonCorrosion, errors);
        RunScenario("화염 연소 지속 피해", VerifyFireBurn, errors);
        RunScenario("번개 축전 방전", VerifyLightningCharge, errors);
        RunScenario("냉기 감속 지연", VerifyIceSlow, errors);
        RunScenario("경비실 경비 작업과 교전", VerifyGuardRoom, errors);

        RunScenario("strict save boundary", VerifyStrictSaveBoundary, errors);
        RunScenario(
            "physical supply and maintenance transaction",
            DefenseFacilityPhysicalTransactionFixture.Run,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 defense facility scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyDefenseAssets()
    {
        BuildingSO[] assets = DefenseAssetNames.Select(LoadDefense).ToArray();
        return assets.All((asset) => asset != null
            && asset.runtimeArchetype == BuildingRuntimeArchetypeKind.DefenseFacility
            && asset.category == BuildingCategory.Special
            && asset.Facility != null
            && asset.Facility.disabledWhenDamaged
            && asset.Facility.SupportsWork(BuiltInWorkTypeIds.Repair)
            && asset.Defense != null
            && asset.Defense.IsDefenseFacility
            && asset.Defense.star == 1
            && asset.Defense.effectAssets != null
            && asset.Defense.effectAssets.Length > 0
            && asset.Defense.effectAssets.All((effect) => effect != null)
            && asset.Defense.effectAssets.All((effect) => !string.IsNullOrWhiteSpace(effect.EffectId))
            && asset.GetConstructionCost() > 0
            && asset.GetMaintenanceCost() > 0
            && asset.GetUnlockPhase() == 1
            && Mathf.Approximately(asset.GetDemolitionRefundRate(), 0.5f)
            && asset.sprite != null)
            && assets.Take(5).All((asset) => asset.layer == GridLayer.FloorOverlay)
            && LoadDefense("P1_GuardRoom").layer == GridLayer.Building
            && LoadDefense("P1_SpikeTrap").Defense.effectAssets.OfType<DefenseDamageEffectSO>().Any()
            && LoadDefense("P1_PoisonPool").Defense.effectAssets.OfType<DefenseCorrosionEffectSO>().Any()
            && LoadDefense("P1_FireVent").Defense.effectAssets.OfType<DefenseBurnEffectSO>().Any()
            && LoadDefense("P1_LightningPillar").Defense.effectAssets.OfType<DefenseChargeEffectSO>().Any()
            && LoadDefense("P1_IceVent").Defense.effectAssets.OfType<DefenseSlowEffectSO>().Any()
            && LoadDefense("P1_GuardRoom").Defense.effectAssets.OfType<DefenseGuardAttackEffectSO>().Any()
            && LoadDefense("P1_GuardRoom").Facility.SupportsWork(BuiltInWorkTypeIds.Guard);
    }

    private static bool VerifyWalkableTrapRoute()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        DefenseFacility trap = world.PlaceDefense("P1_SpikeTrap", new Vector2Int(2, 0));
        Queue<GridMoveStep> path = world.Grid.GetMovePath(
            new Vector2Int(0, 0),
            position => position == new Vector2Int(5, 0));
        HashSet<Vector2Int> traversed = path.Select(step => step.To).ToHashSet();

        return trap != null
            && trap.BuildingData.layer == GridLayer.FloorOverlay
            && trap.buildPoses.All(world.Grid.IsWalkable)
            && trap.buildPoses.All(traversed.Contains);
    }

    private static bool VerifyEffectAssetsDriveDamage()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        BuildingSO source = LoadDefense("P1_SpikeTrap");
        BuildingSO clone = Object.Instantiate(source);
        world.TrackScriptableObject(clone);
        clone.Defense = new DefenseFacilityData
        {
            enabled = source.Defense.enabled,
            concept = source.Defense.concept,
            triggerTimings = source.Defense.triggerTimings,
            targetRule = source.Defense.targetRule,
            cooldownSeconds = source.Defense.cooldownSeconds,
            periodicIntervalSeconds = source.Defense.periodicIntervalSeconds,
            range = source.Defense.range,
            star = source.Defense.star,
            combatLogText = source.Defense.combatLogText,
            effectAssets = source.Defense.effectAssets
        };

        world.PlaceDefense(clone, new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        float before = intruder.CurrentHealth;
        List<DefenseActivationReport> reports = DefenseFacilityResolver.TriggerAt(
            world.Grid,
            CharacterActor.From(intruder),
            new Vector2Int(1, 0),
            DefenseTriggerTiming.OnEnter,
            StatusRuntimeService, treasuryDefenseRuntime: null);

        return reports.Count == 1
            && reports[0].TotalDamage > 0f
            && intruder.CurrentHealth < before;
    }

    private static bool VerifyOpenEffectStrategy()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        BuildingSO source = LoadDefense("P1_SpikeTrap");
        BuildingSO clone = Object.Instantiate(source);
        DebugProbeDefenseEffectSO probe = ScriptableObject.CreateInstance<DebugProbeDefenseEffectSO>();
        world.TrackScriptableObject(clone);
        world.TrackScriptableObject(probe);
        probe.Configure(7f, 0f, 1, "확장 전략");
        clone.Defense = new DefenseFacilityData
        {
            enabled = source.Defense.enabled,
            concept = source.Defense.concept,
            triggerTimings = source.Defense.triggerTimings,
            targetRule = source.Defense.targetRule,
            cooldownSeconds = source.Defense.cooldownSeconds,
            periodicIntervalSeconds = source.Defense.periodicIntervalSeconds,
            range = source.Defense.range,
            star = source.Defense.star,
            combatLogText = source.Defense.combatLogText,
            effectAssets = new DefenseEffectSO[] { probe }
        };

        world.PlaceDefense(clone, new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        float before = intruder.CurrentHealth;
        List<DefenseActivationReport> reports = DefenseFacilityResolver.TriggerAt(
            world.Grid,
            CharacterActor.From(intruder),
            new Vector2Int(1, 0),
            DefenseTriggerTiming.OnEnter,
            StatusRuntimeService, treasuryDefenseRuntime: null);
        string summary = CodexDomainTextFormatter.FormatDefenseEffects(clone.Defense).SingleOrDefault();

        return reports.Count == 1
            && Mathf.Approximately(before - intruder.CurrentHealth, 7f)
            && reports[0].EffectTags.Contains("확장 전략")
            && summary == "확장 효과 7";
    }

    private static bool VerifyTriggerDamageAndEvent()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        DefenseFacility spike = world.PlaceDefense("P1_SpikeTrap", new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        CountingDefenseTriggerListener listener =
            new CountingDefenseTriggerListener(CharacterAiEditorTestDependencies.GameEvents);

        List<DefenseActivationReport> reports = DefenseFacilityResolver.TriggerAt(
            world.Grid,
            CharacterActor.From(intruder),
            new Vector2Int(1, 0),
            DefenseTriggerTiming.OnEnter,
            StatusRuntimeService, treasuryDefenseRuntime: null);

        bool valid = reports.Count == 1
            && reports[0].Facility == spike
            && reports[0].TotalDamage > 0f
            && intruder.CurrentHealth < intruder.MaxHealth
            && listener.Count == 1;

        listener.Dispose();
        return valid;
    }

    private static bool VerifyDamagedDisableAndRepair()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        DefenseFacility spike = world.PlaceDefense("P1_SpikeTrap", new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        CharacterActor worker = world.CreateWorker(new Vector2Int(0, 0));

        spike.SetDamaged(true);
        bool disabled = DefenseFacilityResolver.TriggerAt(
            world.Grid,
            CharacterActor.From(intruder),
            new Vector2Int(1, 0),
            DefenseTriggerTiming.OnEnter,
            StatusRuntimeService, treasuryDefenseRuntime: null).Count == 0;

        bool repairCandidate = worker.TryGetAbility(out AbilityWork work)
            && work.TrySetPriorityWorkTarget(spike, BuiltInWorkTypeIds.Repair, world.Grid.SearchPath(worker.GetNowXY()), out _)
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Repair;
        bool repaired = ExecuteRepairForTest(work, spike) && !spike.IsDamaged;
        if (!(disabled && repairCandidate && repaired))
        {
            Debug.LogError(
                "Defense repair detail: "
                + $"disabled={disabled}; repairCandidate={repairCandidate}; "
                + $"hasWork={work != null}; assigned={(work != null ? work.AssignedWorkTypeId.ToString() : "<none>")}; "
                + $"repaired={repaired}; damaged={spike.IsDamaged}");
        }

        return disabled && repairCandidate && repaired;
    }

    private static bool VerifyPoisonCorrosion()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        world.PlaceDefense("P1_PoisonPool", new Vector2Int(2, 0));
        world.PlaceDefense("P1_SpikeTrap", new Vector2Int(4, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));

        float beforePoison = intruder.CurrentHealth;
        DefenseFacilityResolver.TriggerAt(world.Grid, CharacterActor.From(intruder), new Vector2Int(1, 0), DefenseTriggerTiming.OnEnter, StatusRuntimeService, treasuryDefenseRuntime: null);
        float poisonDamage = beforePoison - intruder.CurrentHealth;
        float beforeSpike = intruder.CurrentHealth;
        DefenseFacilityResolver.TriggerAt(world.Grid, CharacterActor.From(intruder), new Vector2Int(3, 0), DefenseTriggerTiming.OnEnter, StatusRuntimeService, treasuryDefenseRuntime: null);
        float spikeDamageAfterCorrosion = beforeSpike - intruder.CurrentHealth;

        return poisonDamage > 0f && spikeDamageAfterCorrosion > 14f;
    }

    private static bool VerifyFireBurn()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        world.PlaceDefense("P1_FireVent", new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));

        DefenseFacilityResolver.TriggerAt(world.Grid, CharacterActor.From(intruder), new Vector2Int(1, 0), DefenseTriggerTiming.OnEnter, StatusRuntimeService, treasuryDefenseRuntime: null);
        float beforeTick = intruder.CurrentHealth;
        float tickDamage = DefenseEffectResolver.TickStatuses(CharacterActor.From(intruder), 2f, StatusRuntimeService);

        return tickDamage > 0f && intruder.CurrentHealth < beforeTick;
    }

    private static bool VerifyLightningCharge()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        world.PlaceDefense("P1_LightningPillar", new Vector2Int(1, 0));
        world.PlaceDefense("P1_LightningPillar", new Vector2Int(3, 0));
        world.PlaceDefense("P1_LightningPillar", new Vector2Int(5, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(0, 0));

        float before = intruder.CurrentHealth;
        DefenseFacilityResolver.TriggerAt(world.Grid, CharacterActor.From(intruder), new Vector2Int(0, 0), DefenseTriggerTiming.OnEnter, StatusRuntimeService, treasuryDefenseRuntime: null);
        DefenseFacilityResolver.TriggerAt(world.Grid, CharacterActor.From(intruder), new Vector2Int(2, 0), DefenseTriggerTiming.OnEnter, StatusRuntimeService, treasuryDefenseRuntime: null);
        DefenseFacilityResolver.TriggerAt(world.Grid, CharacterActor.From(intruder), new Vector2Int(4, 0), DefenseTriggerTiming.OnEnter, StatusRuntimeService, treasuryDefenseRuntime: null);
        float totalDamage = before - intruder.CurrentHealth;

        return Mathf.Approximately(totalDamage, 54f);
    }

    private static bool VerifyIceSlow()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        world.PlaceDefense("P1_IceVent", new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));

        List<DefenseActivationReport> reports = DefenseFacilityResolver.TriggerAt(
            world.Grid,
            CharacterActor.From(intruder),
            new Vector2Int(1, 0),
            DefenseTriggerTiming.OnEnter,
            StatusRuntimeService, treasuryDefenseRuntime: null);

        return reports.Count == 1
            && reports[0].TotalDamage > 0f
            && reports[0].MovementDelaySeconds > 0f;
    }

    private static bool VerifyGuardRoom()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        DefenseFacility guardRoom = world.PlaceDefense("P1_GuardRoom", new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));

        List<DefenseActivationReport> reports = DefenseFacilityResolver.TriggerAt(
            world.Grid,
            CharacterActor.From(intruder),
            new Vector2Int(1, 0),
            DefenseTriggerTiming.OnEnter,
            StatusRuntimeService, treasuryDefenseRuntime: null);

        return guardRoom.Facility.SupportsWork(BuiltInWorkTypeIds.Guard)
            && guardRoom.Facility.requiredWorkers == 1
            && reports.Count == 1
            && reports[0].TotalDamage > 0f
            && reports[0].EffectTags.Contains("경비 교전");
    }

    private static BuildingSO LoadDefense(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"Assets/Resources/SO/Building/P1/{assetName}.asset");
    }

    private static bool ExecuteRepairForTest(AbilityWork work, BuildableObject target)
    {
        if (work == null || target == null)
        {
            return false;
        }

        typeof(AbilityWork)
            .GetField("assignedWorkType", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(work, FacilityWorkType.Repair);
        work.assignedShop = target;

        work.isWorking = true;
        RepairWorkExecutionHandler handler = new RepairWorkExecutionHandler(
            new NoopEquipmentMaintenanceRuntime(),
            new FixedWorkAmountCalculator(),
            CharacterAiEditorTestDependencies.GameClock,
            automationQuery: NoAutomationInfrastructure.Instance,
            automationCommands: NoAutomationInfrastructure.Instance,
            structuralIntegrity: null,
            defenseFacilities: null,
            defenseNetwork: null);
        WorkExecutionResult result = new WorkExecutionResult();
        WorkExecutionContext context = new WorkExecutionContext(
            1,
            work,
            work.WorkerActor,
            target,
            BuiltInWorkTypeIds.Repair,
            CompleteWorkAmount,
            () => work.isWorking);
        IEnumerator routine = handler.Execute(context, result);
        int ticks = DriveRoutine(routine, () => !target.IsDamaged, 4096);

        if (target.IsDamaged)
        {
            Debug.LogError(
                "Defense repair routine detail: "
                + $"ticks={ticks}; isWorking={work.isWorking}; "
                + $"assigned={work.AssignedWorkTypeId}; target={target.name}");
        }

        return !target.IsDamaged;
    }

    private static IEnumerator CompleteWorkAmount(
        float requiredWork,
        string label,
        float extraMultiplier)
    {
        yield return null;
    }

    private sealed class FixedWorkAmountCalculator : IWorkAmountCalculator
    {
        public float CalculateWorkPerSecond(
            CharacterActor actor,
            BuildableObject target,
            WorkTypeId workTypeId,
            float environmentDurationMultiplier)
        {
            return 1f;
        }
    }

    private sealed class NoopEquipmentMaintenanceRuntime :
        ICombatEquipmentMaintenanceRuntime
    {
        public IReadOnlyList<EquipmentMaintenancePolicyData> Policies =>
            Array.Empty<EquipmentMaintenancePolicyData>();
        public IReadOnlyList<CombatEquipmentRepairOrder> Orders =>
            Array.Empty<CombatEquipmentRepairOrder>();

        public EquipmentMaintenancePolicyData GetPolicy(CharacterActor actor) => null;
        public string GetAssignedPolicyId(CharacterActor actor) => string.Empty;
        public bool AssignPolicy(CharacterActor actor, string policyId) => false;
        public bool TryCreatePolicy(
            string displayName,
            out EquipmentMaintenancePolicyData policy)
        {
            policy = null;
            return false;
        }

        public bool TryDuplicatePolicy(
            string sourcePolicyId,
            string displayName,
            out EquipmentMaintenancePolicyData policy)
        {
            policy = null;
            return false;
        }

        public bool TryUpdatePolicy(EquipmentMaintenancePolicyData policy) => false;
        public bool TryDeletePolicy(string policyId, bool reassignToStandard) => false;
        public bool TryRequestManualRepair(string equipmentInstanceId, out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool HasRepairWorkFor(BuildableObject building) => false;
        public float GetRepairUrgency(BuildableObject building) => 0f;
        public bool TryApplyRepairWork(
            CharacterActor worker,
            BuildableObject building,
            float workAmount,
            out bool completed,
            out string message)
        {
            completed = false;
            message = string.Empty;
            return false;
        }

        public CombatEquipmentMaintenanceSaveData Capture() => new CombatEquipmentMaintenanceSaveData();
        public EquipmentMaintenanceRestoreCandidate PrepareRestore(
            CombatEquipmentMaintenanceSaveData saveData)
        {
            throw new NotSupportedException();
        }

        public void PublishRestore(
            EquipmentMaintenanceRestoreCandidate candidate)
        {
        }
    }

    private static int DriveRoutine(IEnumerator routine, Func<bool> stopCondition, int maxTicks)
    {
        if (routine == null)
        {
            return 0;
        }

        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int ticks = 0;
        while (stack.Count > 0 && ticks < maxTicks)
        {
            if (stopCondition != null && stopCondition())
            {
                break;
            }

            IEnumerator current = stack.Peek();
            if (!current.MoveNext())
            {
                stack.Pop();
                continue;
            }

            ticks++;
            if (current.Current is IEnumerator nested)
            {
                stack.Push(nested);
            }
        }

        return ticks;
    }

    private static bool VerifyStrictSaveBoundary()
    {
        DefenseFacilitySaveData valid = new DefenseFacilitySaveData
        {
            facilities = new List<DefenseFacilityRecordSaveData>
            {
                new DefenseFacilityRecordSaveData
                {
                    facilityPersistentId = "building:defense-fixture:1",
                    buildingId = 1,
                    gridX = 2,
                    gridY = 3,
                    armingPolicy = DefenseArmingPolicy.Alert,
                    operationalState = DefenseFacilityOperationalState.Ready,
                    condition = 87.5f,
                    supply = 4,
                    activationCount = 2,
                    cooldownUntil = 12.5f,
                    forcedDangerousOperation = true,
                    allowedGroups = (int)(DoorAccessGroup.Owner | DoorAccessGroup.Staff),
                    allowedPersistentIds = new List<string>
                    {
                        "character:defense-fixture:1",
                        "owner"
                    },
                    growth = new DefenseFacilityGrowthSaveData
                    {
                        capacityLevel = 1,
                        resetSpeedLevel = 2,
                        effectStrengthLevel = 3,
                        detectionRangeLevel = 4,
                        identificationLevel = 5,
                        outageResistanceLevel = 6
                    },
                    blockedReason = string.Empty
                }
            }
        };
        StrictDefenseSaveRuntime runtime = new StrictDefenseSaveRuntime(valid);
        DefenseFacilitySaveSection section = new DefenseFacilitySaveSection(runtime);
        string canonicalJson = JsonUtility.ToJson(valid);
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        section.Restore(
            canonicalJson,
            DefenseFacilitySaveData.CurrentVersion,
            validReport);
        object sectionContract = section;
        if (!validReport.Success
            || runtime.RestoreCount != 1
            || !string.Equals(section.Capture(), canonicalJson, StringComparison.Ordinal)
            || sectionContract is not IDungeonSaveSectionPreflight
            || sectionContract is not IDungeonRollbackFreeSaveSection
            || sectionContract is IOptionalDungeonSaveSection
            || sectionContract is IDungeonStagedOptionalSaveSection)
        {
            return false;
        }

        DefenseFacilitySaveData invalid = JsonUtility.FromJson<DefenseFacilitySaveData>(
            canonicalJson);
        invalid.facilities[0].condition = 101f;
        invalid.facilities[0].allowedPersistentIds.Reverse();
        string beforeInvalid = section.Capture();
        bool invalidRejected = ThrowsInvalidOperation(() => section.Restore(
            JsonUtility.ToJson(invalid),
            DefenseFacilitySaveData.CurrentVersion,
            new DungeonGameRestoreReport()));
        bool legacyRejected = ThrowsInvalidOperation(() => section.ValidatePayload(
            canonicalJson,
            DefenseFacilitySaveData.CurrentVersion - 1,
            new DungeonGameRestoreReport()));
        bool emptyRejected = ThrowsInvalidOperation(() => section.ValidatePayload(
            string.Empty,
            DefenseFacilitySaveData.CurrentVersion,
            new DungeonGameRestoreReport()));
        return invalidRejected
            && legacyRejected
            && emptyRejected
            && runtime.RestoreCount == 1
            && string.Equals(
                section.Capture(),
                beforeInvalid,
                StringComparison.Ordinal);
    }

    private static bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class DefenseScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();

        public DefenseScenarioWorld()
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            Grid = new Grid(24, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            GameObject gridSystemObject = new GameObject("Defense Scenario GridSystemManager");
            objects.Add(gridSystemObject);
            GridSystemManager manager = gridSystemObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);
        }

        public Grid Grid { get; }

        public DefenseFacility PlaceDefense(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = LoadDefense(assetName);
            return PlaceDefense(buildingData, position);
        }

        public DefenseFacility PlaceDefense(BuildingSO buildingData, Vector2Int position)
        {
            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building is not DefenseFacility defense)
            {
                throw new InvalidOperationException($"{buildingData?.name ?? "Defense asset"} did not create DefenseFacility.");
            }

            defense.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(BlueprintResearchWorkService),
                FacilityCandidateCache,
                RoomFacilityPolicy,
                gameClock: CharacterAiEditorTestDependencies.GameClock, combatEquipmentRuntime: null, worldRegistry: null, worldItemStackRuntime: null, abilityRuntimeDispatcher: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
            defense.ConstructDefenseFacilityEventBus(
                CharacterAiEditorTestDependencies.GameEvents, worldThreatModifiers: null, defenseRuntime: null);
            defense.ConstructDebugRules(DisabledDungeonDebugRuleQuery.Instance);
            objects.Add(defense.gameObject);
            defense.SetGrid(Grid);
            defense.RestorePersistentIdentity(
                (BuildingInstanceId)$"building:defense-fixture:{buildingData.id}:{position.x}:{position.y}");
            defense.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                defense,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{buildingData.name} could not be registered.");
            }

            return defense;
        }

        public void TrackScriptableObject(ScriptableObject scriptableObject)
        {
            if (scriptableObject != null && !scriptableObjects.Contains(scriptableObject))
            {
                scriptableObjects.Add(scriptableObject);
            }
        }

        public CharacterActor CreateIntruder(Vector2Int position)
        {
            CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset");
            GameObject obj = CreateCharacterObject("Defense Scenario Intruder");
            CharacterActor character = obj.GetComponent<CharacterActor>();
            InitializeCharacter(character, data, position);
            return character;
        }

        public CharacterActor CreateWorker(Vector2Int position)
        {
            CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "Defense Repair Worker",
                "Orc");
            scriptableObjects.Add(data);
            data.characterType = CharacterType.NPC;
            data.characterName = "Defense Repair Worker";
            data.speciesTag = "Orc";
            GameObject obj = CreateCharacterObject("Defense Scenario Worker");
            AbilityWork work = obj.AddComponent<AbilityWork>();
            work.ConstructAbilityWork(
                BlueprintResearchWorkService,
                StaffDiscontentRuntimeService,
                FloatingIconFeedbackService,
                WorkGridResolver,
                FacilityCandidateCache,
                null, exteriorZoneQuery: null, workExecutionHandlerRegistry: null, workPolicyRegistry: null, workOrderRuntime: null, workAmountCalculator: null, captiveLaborQuery: null, gameClock: null, defenseEngagementRuntime: null, roomEnvironmentExperienceService: null, paidFacilityContracts: null, environmentWorkPolicy: null, characterEnvironment: NoCharacterEnvironmentWorkContext.Instance, environmentalWorkwearCommands: NoEnvironmentalWorkwearCommand.Instance,
                needDefinitionCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
                debugRules: DisabledDungeonDebugRuleQuery.Instance);
            CharacterActor character = obj.GetComponent<CharacterActor>();
            InitializeCharacter(character, data, position);
            character.RefreshAbilityCache();
            return character;
        }

        public void Dispose()
        {
            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }

        private GameObject CreateCharacterObject(string name)
        {
            GameObject obj = new GameObject(name);
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<CharacterActor>();
            return obj;
        }

        private void InitializeCharacter(CharacterActor character, CharacterSO data, Vector2Int position)
        {
            CharacterAiEditorTestDependencies.Inject(character.gameObject);
            CharacterAwakeMethod?.Invoke(character, null);
            CharacterAiEditorTestDependencies.InjectCharacterStats(
                character.GetComponent<CharacterStats>(),
                StaffDiscontentRuntimeService,
                MetaProgressionRuntimeReader,
                new DungeonStory.Foundation.UnityGameClock(),
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                DisabledDungeonDebugRuleQuery.Instance);
            character.RefreshAbilityCache();
            character.Initialization(data);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            character.transform.position = Grid.GetWorldPos(position);
        }
    }

    private sealed class StrictDefenseSaveRuntime : IDefenseFacilityPersistence
    {
        private DefenseFacilitySaveData data;

        public StrictDefenseSaveRuntime(DefenseFacilitySaveData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int RestoreCount { get; private set; }

        public DefenseFacilitySnapshot GetSnapshot(DefenseFacility facility) => default;

        public bool CanActivate(
            DefenseFacility facility,
            CharacterActor target,
            DefenseTriggerTiming timing,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public bool TryBeginActivation(
            DefenseFacility facility,
            CharacterActor target,
            DefenseTriggerTiming timing,
            out DefenseActivationAuthorization authorization,
            out DomainFailure failure)
        {
            authorization = default;
            failure = DomainFailure.None;
            return false;
        }

        public void CompleteActivation(
            DefenseFacility facility,
            DefenseActivationAuthorization authorization)
        {
        }

        public bool SetArmingPolicy(
            DefenseFacility facility,
            DefenseArmingPolicy policy)
        {
            return false;
        }

        public bool SetAllowed(
            DefenseFacility facility,
            DoorAccessGroup group,
            bool allowed) => false;

        public bool SetAllowed(
            DefenseFacility facility,
            string persistentId,
            bool allowed) => false;

        public bool TryRequestReload(
            DefenseFacility facility,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public bool TryClearJam(
            DefenseFacility facility,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public bool TryRepair(
            DefenseFacility facility,
            float condition,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public DefenseFacilitySaveData CaptureState() => data;

        public DefenseFacilityRestoreCandidate PrepareRestoreState(
            DefenseFacilitySaveData restored)
        {
            data = restored ?? throw new ArgumentNullException(nameof(restored));
            return new DefenseFacilityRestoreCandidate(
                new DefenseFacilityAggregateState());
        }

        public void PublishRestoreState(
            DefenseFacilityRestoreCandidate candidate)
        {
            RestoreCount++;
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private static bool VerifyEventSnapshotIsolation()
    {
        using DefenseScenarioWorld world = new DefenseScenarioWorld();
        DefenseFacility facility = world.PlaceDefense("P1_SpikeTrap", new Vector2Int(2, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        DefenseActivationReport mutableReport = new DefenseActivationReport(
            facility,
            intruder,
            DefenseTriggerTiming.OnEnter);
        mutableReport.AddDamage(4f);
        mutableReport.AddEffectTag("처음 효과");

        using CountingDefenseTriggerListener listener =
            new CountingDefenseTriggerListener(CharacterAiEditorTestDependencies.GameEvents);
        CharacterAiEditorTestDependencies.GameEvents.Publish(
            new DefenseFacilityTriggeredEvent(mutableReport));
        mutableReport.AddDamage(99f);
        mutableReport.AddEffectTag("나중 효과");

        DefenseActivationSnapshot snapshot = listener.LastReport;
        return listener.Count == 1
            && snapshot != null
            && Mathf.Approximately(snapshot.TotalDamage, 4f)
            && snapshot.EffectTags.SequenceEqual(new[] { "처음 효과" })
            && snapshot.SourceFacility == facility;
    }

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(
                false,
                null,
                0f,
                0f,
                1f,
                false,
                "Defense scenario fixture has no blueprint research runtime.");
        }

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            ApplyResearchWork(researcher, researchFacility, approvedWorkUnits);
    }

    private sealed class NoopStaffDiscontentRuntimeService : IStaffDiscontentRuntimeService
    {
        public float GetWorkEfficiencyMultiplier(CharacterActor staff)
        {
            return 1f;
        }

        public bool ShouldBlockWork(CharacterActor staff, out string reason)
        {
            reason = string.Empty;
            return false;
        }

        public bool IsRebellionTarget(CharacterActor target)
        {
            return false;
        }

        public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)
        {
            return false;
        }
    }

    private sealed class NoopFloatingIconFeedbackService : IFloatingIconFeedbackService
    {
        public bool Show(Component target, Sprite sprite, float maxWorldSize)
        {
            return false;
        }
    }

    private sealed class ScenarioWorkGridResolver : IWorkGridResolver
    {
        public Grid ResolveActiveGrid(
            AbilityWork work,
            GridPathSearchResult searchResult,
            Grid priorityGrid = null)
        {
            if (searchResult != null && searchResult.sourceGrid != null)
            {
                return searchResult.sourceGrid;
            }

            if (priorityGrid != null)
            {
                return priorityGrid;
            }

            return work != null ? work.CachedGrid : null;
        }

        public Vector2Int GetGridPosition(Grid activeGrid, CharacterActor actor)
        {
            if (activeGrid == null || actor == null)
            {
                return Vector2Int.zero;
            }

            Vector2Int position = activeGrid.GetXY(actor.transform.position);
            return activeGrid.IsValidGridPos(position) ? position : Vector2Int.zero;
        }
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick()
        {
            return false;
        }

        public bool TryTriggerCharacterUnderPointer()
        {
            return false;
        }

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(
            Vector3 screenPosition,
            Camera camera,
            out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }

    private sealed class NoopOwnerRunLifecycleService : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
        }
    }

    private sealed class ScenarioMetaProgressionRuntimeReader : IMetaProgressionRuntimeReader
    {
        public int GetStartingFacilityCandidateBonus()
        {
            return 0;
        }

        public int GetStartingOwnerTraitCandidateBonus()
        {
            return 0;
        }

        public float GetOwnerMaxHealthMultiplier()
        {
            return 1f;
        }

        public float GetInvasionWarningThresholdMultiplier()
        {
            return 1f;
        }

        public float GetCommerceStockCostMultiplier(StockCategory category) => 1f;
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => 1f;
        public float GetArcaneResearchWorkMultiplier() => 1f;

        public bool IsRecipePreserved(string recipeId)
        {
            return false;
        }

        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)
        {
            return Array.Empty<int>();
        }
    }

    private sealed class CountingDefenseTriggerListener : IDisposable
    {
        private readonly IDisposable subscription;

        public int Count { get; private set; }
        public DefenseActivationSnapshot LastReport { get; private set; }

        public CountingDefenseTriggerListener(
            DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription =
                gameEventBus.Subscribe<DefenseFacilityTriggeredEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)
        {
            Count++;
            LastReport = eventType.report;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}

internal sealed class DebugProbeDefenseEffectSO : DefenseEffectSO
{
    public override string EffectId => "debug.custom-defense-effect";
    public override string DisplayName => "확장 효과";

    public override void Apply(DefenseEffectContext context)
    {
        context.ApplyDamage(Amount, DisplayName);
        context.AddEffectTag(LogTag);
    }
}
