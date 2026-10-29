using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DefenseEngagementDebugScenarios
{
    public static bool RunPolicyRoundTripFocused() => VerifyPolicyRoundTrip();

    [MenuItem("DungeonStory/Debug/Invasion/Run Defense Engagement Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Defense engagement scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("상호 전투 공식", VerifyCombatFormula, errors);
        Run("인접 저지 칸과 도착 우선", VerifyInterceptPlan, errors);
        Run("외부와 입구는 저지선에서 제외", VerifyExteriorCellsAreNotIntercepted, errors);
        Run("예약 칸 회피", VerifyUnavailableCells, errors);
        Run("경비 정책 기본값과 복원", VerifyPolicyRoundTrip, errors);
        Run("침입자 전투 설정 저장", VerifyIntruderSettingsPersistence, errors);
        Run("집결 상태 JSON 저장 계약", VerifyRallySaveContract, errors);
        Run("원거리 경비 2인 저장 계약", VerifySecondRangedGuardSaveContract, errors);
        Run("V24 저장 계약", () => DungeonGameSaveData.CurrentVersion == 24, errors);

        foreach (string error in errors)
        {
            Debug.LogError($"Defense engagement scenario failed: {error}");
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log("Defense engagement scenarios passed.");
        }

        return errors.Count == 0;
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        errors.Add(name);
    }

    private static bool VerifySecondRangedGuardSaveContract()
    {
        DefenseEngagementSaveData source = new DefenseEngagementSaveData
        {
            id = "engagement:test",
            intruderId = "intruder:test",
            rangedGuardId = "guard:ranged:1",
            secondaryRangedGuardId = "guard:ranged:2",
            rangedX = 8,
            rangedY = 1,
            secondaryRangedX = 9,
            secondaryRangedY = 1,
            rangedAttackRemaining = 0.35f,
            secondaryRangedAttackRemaining = 0.8f
        };
        string json = JsonUtility.ToJson(source);
        DefenseEngagementSaveData restored =
            JsonUtility.FromJson<DefenseEngagementSaveData>(json);
        return restored != null
            && restored.rangedGuardId == "guard:ranged:1"
            && restored.secondaryRangedGuardId == "guard:ranged:2"
            && restored.rangedX != restored.secondaryRangedX
            && Mathf.Approximately(restored.rangedAttackRemaining, 0.35f)
            && Mathf.Approximately(restored.secondaryRangedAttackRemaining, 0.8f);
    }

    private static bool VerifyCombatFormula()
    {
        float damage = DefenseCombatFormula.CalculateDamage(
            attack: 10f,
            strength: 5f,
            combatPowerMultiplier: 1f,
            defenderToughness: 10f);
        float cappedMitigationDamage = DefenseCombatFormula.CalculateDamage(
            attack: 10f,
            strength: 5f,
            combatPowerMultiplier: 1f,
            defenderToughness: 100f);
        float normalInterval = DefenseCombatFormula.CalculateAttackInterval(10f);
        float fastInterval = DefenseCombatFormula.CalculateAttackInterval(30f, 2f);
        return Mathf.Approximately(damage, 14.25f)
            && Mathf.Approximately(cappedMitigationDamage, 10.45f)
            && Mathf.Approximately(normalInterval, 0.75f)
            && Mathf.Approximately(fastInterval, 0.35f);
    }

    private static bool VerifyInterceptPlan()
    {
        Grid grid = CreateHallwayGrid(10);
        Queue<GridMoveStep> route = grid.GetMovePath(
            new Vector2Int(0, 0),
            cell => cell == new Vector2Int(9, 0));
        DefenseInterceptPlanner planner = new DefenseInterceptPlanner();
        bool planned = planner.TryCreatePlan(
            grid,
            route.ToArray(),
            new Vector2Int(9, 0),
            new HashSet<Vector2Int>(),
            out DefenseInterceptPlan plan);
        int adjacency = Mathf.Abs(plan.IntruderStopCell.x - plan.GuardCell.x)
            + Mathf.Abs(plan.IntruderStopCell.y - plan.GuardCell.y);
        return planned
            && adjacency == 1
            && plan.IntruderStopCell != plan.GuardCell
            && plan.LeadPath.Count <= plan.IntruderSteps
            && plan.ReserveCell != plan.IntruderStopCell
            && Mathf.Abs(plan.ReserveCell.x - plan.GuardCell.x) == 2;
    }

    private static bool VerifyUnavailableCells()
    {
        Grid grid = CreateHallwayGrid(12);
        Queue<GridMoveStep> route = grid.GetMovePath(
            new Vector2Int(0, 0),
            cell => cell == new Vector2Int(11, 0));
        HashSet<Vector2Int> unavailable = new HashSet<Vector2Int>
        {
            new Vector2Int(5, 0),
            new Vector2Int(6, 0)
        };
        DefenseInterceptPlanner planner = new DefenseInterceptPlanner();
        return planner.TryCreatePlan(
                grid,
                route.ToArray(),
                new Vector2Int(11, 0),
                unavailable,
                out DefenseInterceptPlan plan)
            && !unavailable.Contains(plan.IntruderStopCell)
            && !unavailable.Contains(plan.GuardCell)
            && !unavailable.Contains(plan.ReserveCell);
    }

    private static bool VerifyExteriorCellsAreNotIntercepted()
    {
        Grid grid = CreateHallwayGrid(10);
        grid.GetGridCell(new Vector2Int(0, 0)).SetAreaType(GridCellAreaType.ExteriorPath);
        grid.GetGridCell(new Vector2Int(1, 0)).SetAreaType(GridCellAreaType.ExteriorPath);
        grid.GetGridCell(new Vector2Int(2, 0)).SetAreaType(GridCellAreaType.Entrance);
        Queue<GridMoveStep> route = grid.GetMovePath(
            new Vector2Int(0, 0),
            cell => cell == new Vector2Int(9, 0));
        DefenseInterceptPlanner planner = new DefenseInterceptPlanner();
        bool planned = planner.TryCreatePlan(
            grid,
            route.ToArray(),
            new Vector2Int(4, 0),
            new HashSet<Vector2Int>(),
            out DefenseInterceptPlan plan);
        return planned
            && plan.IntruderStopCell == new Vector2Int(3, 0)
            && plan.GuardCell == new Vector2Int(4, 0)
            && grid.GetGridCell(plan.IntruderStopCell).AreaType == GridCellAreaType.DungeonInterior
            && grid.GetGridCell(plan.GuardCell).AreaType == GridCellAreaType.DungeonInterior;
    }

    private static bool VerifyPolicyRoundTrip()
    {
        DefenseResponsePolicyRuntime source = new DefenseResponsePolicyRuntime(
            new InvasionAggregateStateStore(
                new DungeonRuntimeAggregateRootStore()));
        DefenseResponsePolicyData standard = source.Policies.FirstOrDefault(
            policy => policy.id == DefenseResponsePolicyRuntime.StandardPolicyId);
        DefenseResponsePolicyData survival = source.Policies.FirstOrDefault(
            policy => policy.id == DefenseResponsePolicyRuntime.SurvivalFirstPolicyId);
        DefenseResponsePolicyData hold = source.Policies.FirstOrDefault(
            policy => policy.id == DefenseResponsePolicyRuntime.HoldTheLinePolicyId);
        if (standard == null
            || survival == null
            || hold == null
            || !Mathf.Approximately(standard.retreatHealthRatio, 0.2f)
            || !standard.holdWithoutReplacement
            || !Mathf.Approximately(survival.retreatHealthRatio, 0.35f)
            || survival.holdWithoutReplacement
            || hold.retreatHealthRatio > 0f)
        {
            return false;
        }

        standard.autoRespond = false;
        standard.minimumDispatchHealthRatio = 1f;
        standard.id = "policy:external-standard";
        standard.kind = DefenseResponsePolicyKind.Custom;
        DefenseResponsePolicyData currentStandard = source.Policies.FirstOrDefault(
            policy => policy.id == DefenseResponsePolicyRuntime.StandardPolicyId);
        if (currentStandard == null
            || !currentStandard.autoRespond
            || !Mathf.Approximately(
                currentStandard.minimumDispatchHealthRatio,
                0.4f))
        {
            return false;
        }

        DefenseResponsePolicyData defaultPolicy = source.GetPolicy(null);
        if (defaultPolicy == null)
        {
            return false;
        }

        defaultPolicy.holdWithoutReplacement = false;
        if (!source.GetPolicy(null).holdWithoutReplacement)
        {
            return false;
        }

        if (!source.TryCreatePolicy("야간 경계", out DefenseResponsePolicyData custom))
        {
            return false;
        }

        string customId = custom.id;
        custom.displayName = "외부 생성 변경";
        custom.autoRespond = false;
        custom.minimumDispatchHealthRatio = 1f;
        custom.id = "policy:external-created";
        DefenseResponsePolicyData storedCreated = source.Policies.FirstOrDefault(
            policy => policy.id == customId);
        if (storedCreated == null
            || storedCreated.displayName != "야간 경계"
            || !storedCreated.autoRespond
            || !Mathf.Approximately(
                storedCreated.minimumDispatchHealthRatio,
                0.4f))
        {
            return false;
        }

        custom.displayName = " 야간 경계 ";
        custom.id = customId;
        custom.kind = DefenseResponsePolicyKind.Standard;
        custom.minimumDispatchHealthRatio = 0.5f;
        custom.retreatHealthRatio = 0.3f;
        custom.rejoinHealthRatio = 0.75f;
        if (!source.TryUpdatePolicy(custom))
        {
            return false;
        }

        DefenseResponsePolicyData updated = source.Policies.FirstOrDefault(
            policy => policy.id == customId);
        if (updated == null
            || updated.kind != DefenseResponsePolicyKind.Custom
            || updated.displayName != "야간 경계"
            || !Mathf.Approximately(updated.minimumDispatchHealthRatio, 0.5f)
            || !Mathf.Approximately(updated.retreatHealthRatio, 0.3f)
            || !Mathf.Approximately(updated.rejoinHealthRatio, 0.75f))
        {
            return false;
        }

        PolicyFixtureSubject guard = new PolicyFixtureSubject("character:policy-fixture");
        if (!source.AssignPolicy(guard, customId)
            || source.GetAssignedPolicyId(guard) != customId)
        {
            return false;
        }

        DefenseResponsePolicyData assigned = source.GetPolicy(guard);
        assigned.autoRespond = true;
        if (source.GetPolicy(guard).autoRespond)
        {
            return false;
        }

        if (!source.TryDuplicatePolicy(
                customId,
                "야간 경계 복제",
                out DefenseResponsePolicyData duplicate))
        {
            return false;
        }

        string duplicateId = duplicate.id;
        duplicate.displayName = "외부 복제 변경";
        duplicate.holdWithoutReplacement = false;
        duplicate.id = "policy:external-duplicate";
        duplicate.kind = DefenseResponsePolicyKind.Standard;
        DefenseResponsePolicyData storedDuplicate = source.Policies.FirstOrDefault(
            policy => policy.id == duplicateId);
        if (storedDuplicate == null
            || storedDuplicate.displayName != "야간 경계 복제"
            || !storedDuplicate.holdWithoutReplacement
            || !Mathf.Approximately(
                storedDuplicate.minimumDispatchHealthRatio,
                0.5f))
        {
            return false;
        }

        DefenseResponsePolicySaveSnapshot snapshot = source.Capture();
        DefenseResponsePolicyRuntime restored = new DefenseResponsePolicyRuntime(
            new InvasionAggregateStateStore(
                new DungeonRuntimeAggregateRootStore()));
        restored.ReplaceFromValidatedSnapshot(snapshot);
        DefenseResponsePolicyData restoredCustom = restored.Policies.FirstOrDefault(
            policy => policy.id == customId);
        return restoredCustom != null
            && restoredCustom.displayName == "야간 경계"
            && Mathf.Approximately(restoredCustom.minimumDispatchHealthRatio, 0.5f)
            && Mathf.Approximately(restoredCustom.retreatHealthRatio, 0.3f)
            && Mathf.Approximately(restoredCustom.rejoinHealthRatio, 0.75f)
            && restored.GetAssignedPolicyId(guard) == customId
            && restored.TryCreatePolicy("새 정책", out DefenseResponsePolicyData next)
            && next.id != restoredCustom.id
            && next.id != duplicateId;
    }

    private sealed class PolicyFixtureSubject : IInvasionThreatSubject
    {
        private readonly CharacterId characterId;

        public PolicyFixtureSubject(string characterId)
        {
            this.characterId = new CharacterId(characterId);
        }

        public InvasionThreatSubjectSnapshot CaptureInvasionThreatSubject()
        {
            return new InvasionThreatSubjectSnapshot(characterId, "정책 fixture");
        }
    }

    private static bool VerifyIntruderSettingsPersistence()
    {
        InvasionIntruderSettings settings = new InvasionIntruderSettings
        {
            rallyDurationSeconds = 12f,
            healthMultiplier = 1.35f,
            meleeDamageMultiplier = 1.7f,
            attackSpeedMultiplier = 1.25f
        };
        InvasionIntruderPersistenceState state = new InvasionIntruderPersistenceState(
            2001,
            Vector3.zero,
            Vector2Int.zero,
            InvasionIntruderState.Engaged,
            2f,
            0.4f,
            0,
            80f,
            0f,
            50f,
            new Dictionary<CharacterCondition, float>(),
            settings,
            Array.Empty<DefenseStatusSnapshot>(),
            "invasion:test",
            rallyRemainingSeconds: 8.5f,
            hasBreachedDungeonInterior: false);
        return state.RuntimeId == "invasion:test"
            && state.State == InvasionIntruderState.Engaged
            && Mathf.Approximately(state.Settings.rallyDurationSeconds, 12f)
            && Mathf.Approximately(state.RallyRemainingSeconds, 8.5f)
            && !state.HasBreachedDungeonInterior
            && Mathf.Approximately(state.Settings.healthMultiplier, 1.35f)
            && Mathf.Approximately(state.Settings.meleeDamageMultiplier, 1.7f)
            && Mathf.Approximately(state.Settings.attackSpeedMultiplier, 1.25f);
    }

    private static bool VerifyRallySaveContract()
    {
        DungeonInvasionSaveData source = new DungeonInvasionSaveData
        {
            activeIntruders = new List<DungeonInvasionIntruderSaveData>
            {
                new DungeonInvasionIntruderSaveData
                {
                    runtimeId = "invasion:rally-save",
                    state = InvasionIntruderState.Rallying,
                    rallyRemainingSeconds = 7.25f,
                    hasBreachedDungeonInterior = false,
                    settings = new DungeonInvasionIntruderSettingsSaveData
                    {
                        rallyDurationSeconds = 12f
                    }
                }
            }
        };
        DungeonInvasionSaveData restored = JsonUtility.FromJson<DungeonInvasionSaveData>(
            JsonUtility.ToJson(source));
        DungeonInvasionIntruderSaveData intruder = restored?.activeIntruders?.SingleOrDefault();
        return intruder != null
            && intruder.runtimeId == "invasion:rally-save"
            && intruder.state == InvasionIntruderState.Rallying
            && Mathf.Approximately(intruder.rallyRemainingSeconds, 7.25f)
            && !intruder.hasBreachedDungeonInterior
            && intruder.settings != null
            && Mathf.Approximately(intruder.settings.rallyDurationSeconds, 12f);
    }

    private static Grid CreateHallwayGrid(int width)
    {
        Grid grid = new Grid(width, 1);
        TestHallway hallway = new TestHallway();
        Vector2Int[] cells = Enumerable.Range(0, width)
            .Select(x => new Vector2Int(x, 0))
            .ToArray();
        if (!grid.RegisterOccupant(hallway, GridLayer.Hallway, cells, connectPositions: false))
        {
            throw new InvalidOperationException("Could not create defense hallway fixture.");
        }

        return grid;
    }

    private sealed class TestHallway : IGridOccupant
    {
        public int GridId => 9912;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }
}
