using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public enum BuildingCrackStage
{
    None = 0,
    Hairline = 1,
    Cracked = 2,
    Critical = 3
}

public readonly struct BuildingStructuralIntegritySnapshot
{
    public BuildingStructuralIntegritySnapshot(
        BuildableObject building,
        float currentHitPoints,
        float maxHitPoints,
        float toughness,
        bool breachable,
        BuildingCrackStage crackStage)
    {
        Building = building;
        CurrentHitPoints = Mathf.Max(0f, currentHitPoints);
        MaxHitPoints = Mathf.Max(1f, maxHitPoints);
        Toughness = Mathf.Max(0f, toughness);
        Breachable = breachable;
        CrackStage = crackStage;
    }

    public BuildableObject Building { get; }
    public float CurrentHitPoints { get; }
    public float MaxHitPoints { get; }
    public float Toughness { get; }
    public bool Breachable { get; }
    public BuildingCrackStage CrackStage { get; }
    public float IntegrityRatio => Mathf.Clamp01(CurrentHitPoints / MaxHitPoints);
}

public readonly struct BuildingStructuralDamageResult
{
    public BuildingStructuralDamageResult(
        bool applied,
        bool destroyed,
        float damage,
        BuildingStructuralIntegritySnapshot snapshot,
        string failureReason = "")
    {
        Applied = applied;
        Destroyed = destroyed;
        Damage = Mathf.Max(0f, damage);
        Snapshot = snapshot;
        FailureReason = failureReason ?? string.Empty;
    }

    public bool Applied { get; }
    public bool Destroyed { get; }
    public float Damage { get; }
    public BuildingStructuralIntegritySnapshot Snapshot { get; }
    public string FailureReason { get; }
}

public interface IBuildingStructuralIntegrityRuntime
{
    bool TryGet(
        BuildableObject building,
        out BuildingStructuralIntegritySnapshot snapshot);
    bool IsBreachable(BuildableObject building);
    BuildingStructuralDamageResult ApplyDamage(
        BuildableObject building,
        float damage);
    bool TryApplyRepairWork(
        BuildableObject building,
        float workAmount,
        out bool completed,
        out BuildingStructuralIntegritySnapshot snapshot);
}

[Serializable]
public sealed class BuildingStructuralIntegritySaveData
{
    public float currentHitPoints;
}

public static class BuildingStructuralIntegrityDefaults
{
    public const float WallHitPoints = 300f;
    public const float WallToughness = 18f;
    public const float InteriorDoorHitPoints = 120f;
    public const float InteriorDoorToughness = 8f;
    public const float EntranceDoorHitPoints = 220f;
    public const float EntranceDoorToughness = 14f;
    public const float ReinforcedDropGateHitPoints = 450f;
    public const float ReinforcedDropGateToughness = 24f;

    public static bool TryCreate(
        BuildingSO building,
        out BuildingStructuralIntegrityAbility ability)
    {
        ability = null;
        if (building == null)
        {
            return false;
        }

        if (building.Defense != null
            && string.Equals(
                building.Defense.facilityFamilyId,
                "defense:barrier",
                StringComparison.Ordinal))
        {
            ability = Create(
                ReinforcedDropGateHitPoints,
                ReinforcedDropGateToughness);
            return true;
        }

        if (building.IsInteriorDoor)
        {
            ability = Create(
                InteriorDoorHitPoints,
                InteriorDoorToughness);
            return true;
        }

        if (building.IsDoor)
        {
            ability = Create(
                EntranceDoorHitPoints,
                EntranceDoorToughness);
            return true;
        }

        if (building.IsStructuralWall || building.IsWall)
        {
            ability = Create(WallHitPoints, WallToughness);
            return true;
        }

        return false;
    }

    private static BuildingStructuralIntegrityAbility Create(
        float hitPoints,
        float toughness)
    {
        return new BuildingStructuralIntegrityAbility
        {
            maxHitPoints = hitPoints,
            toughness = toughness,
            repairHitPointsPerWork = 2f,
            breachable = true
        };
    }
}

public sealed class BuildingStructuralIntegrity :
    MonoBehaviour,
    IBuildingStateModule
{
    private BuildableObject building;
    private BuildingStructuralIntegrityAbility ability;
    private float currentHitPoints;
    private bool initialized;

    public string ModuleId => BuildingStateModuleIds.ForAbility(
        "structural",
        ability?.AbilityId ?? nameof(BuildingStructuralIntegrityAbility));
    public int CurrentVersion => 1;
    public float MaxHitPoints => Mathf.Max(1f, ability?.maxHitPoints ?? 1f);
    public float CurrentHitPoints => Mathf.Clamp(
        currentHitPoints,
        0f,
        MaxHitPoints);
    public float Toughness => Mathf.Max(0f, ability?.toughness ?? 0f);
    public float RepairHitPointsPerWork => Mathf.Max(
        0.01f,
        ability?.repairHitPointsPerWork ?? 1f);
    public bool Breachable => ability?.breachable == true;
    public float IntegrityRatio => CurrentHitPoints / MaxHitPoints;
    public BuildingCrackStage CrackStage => ResolveCrackStage(IntegrityRatio);
    public bool NeedsRepair => CurrentHitPoints < MaxHitPoints - 0.001f;

    public static BuildingStructuralIntegrity Ensure(
        BuildableObject building,
        BuildingStructuralIntegrityAbility ability)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        BuildingStructuralIntegrity runtime =
            building.GetComponent<BuildingStructuralIntegrity>();
        if (runtime == null)
        {
            runtime = building.gameObject.AddComponent<
                BuildingStructuralIntegrity>();
        }

        runtime.Configure(
            building,
            ability ?? throw new ArgumentNullException(nameof(ability)));
        return runtime;
    }

    public static bool TryGet(
        BuildableObject building,
        out BuildingStructuralIntegrity runtime)
    {
        runtime = building != null
            ? building.GetComponent<BuildingStructuralIntegrity>()
            : null;
        return runtime != null && runtime.building != null;
    }

    public BuildingStructuralIntegritySnapshot GetSnapshot()
    {
        return new BuildingStructuralIntegritySnapshot(
            building,
            CurrentHitPoints,
            MaxHitPoints,
            Toughness,
            Breachable,
            CrackStage);
    }

    public BuildingStructuralDamageResult ApplyDamage(float damage)
    {
        if (damage <= 0f
            || building == null
            || building.isDestroy
            || !Breachable)
        {
            return new BuildingStructuralDamageResult(
                false,
                false,
                0f,
                GetSnapshot());
        }

        float before = CurrentHitPoints;
        currentHitPoints = Mathf.Max(0f, before - damage);
        float applied = before - currentHitPoints;
        bool destroyed = currentHitPoints <= 0f;
        building.SetDamaged(!destroyed && IntegrityRatio <= 0.5f);
        building.NotifyStructuralStateChanged();
        BuildingStructuralIntegritySnapshot snapshot = GetSnapshot();
        StructuralDamagePresentation.Present(
            building,
            snapshot,
            applied,
            destroyed,
            building.ReducedMotion);
        return new BuildingStructuralDamageResult(
            applied > 0f,
            destroyed,
            applied,
            snapshot);
    }

    public bool ApplyRepairWork(
        float workAmount,
        out bool completed)
    {
        completed = !NeedsRepair;
        if (workAmount <= 0f
            || building == null
            || building.isDestroy
            || completed)
        {
            return false;
        }

        float before = CurrentHitPoints;
        currentHitPoints = Mathf.Min(
            MaxHitPoints,
            currentHitPoints + workAmount * RepairHitPointsPerWork);
        completed = !NeedsRepair;
        building.SetDamaged(!completed && IntegrityRatio <= 0.5f);
        building.NotifyStructuralStateChanged();
        if (CurrentHitPoints > before)
        {
            BuildingStructuralIntegritySnapshot snapshot = GetSnapshot();
            StructuralDamagePresentation.Present(
                building,
                snapshot,
                -(CurrentHitPoints - before),
                false,
                building.ReducedMotion);
        }

        return CurrentHitPoints > before;
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new BuildingStructuralIntegritySaveData
        {
            currentHitPoints = CurrentHitPoints
        });
    }

    public bool TryRestoreState(
        int version,
        string payload,
        out string error)
    {
        if (version != CurrentVersion)
        {
            error = $"지원하지 않는 구조 내구도 버전 {version}";
            return false;
        }

        BuildingStructuralIntegritySaveData save =
            JsonUtility.FromJson<BuildingStructuralIntegritySaveData>(
                payload ?? string.Empty);
        if (save == null)
        {
            error = "구조 내구도 데이터가 없습니다.";
            return false;
        }

        currentHitPoints = Mathf.Clamp(
            save.currentHitPoints,
            0f,
            MaxHitPoints);
        initialized = true;
        building?.SetDamaged(
            currentHitPoints > 0f
            && IntegrityRatio <= 0.5f);
        error = string.Empty;
        return true;
    }

    public static BuildingCrackStage ResolveCrackStage(float ratio)
    {
        if (ratio <= 0.25f)
        {
            return BuildingCrackStage.Critical;
        }

        if (ratio <= 0.5f)
        {
            return BuildingCrackStage.Cracked;
        }

        return ratio <= 0.75f
            ? BuildingCrackStage.Hairline
            : BuildingCrackStage.None;
    }

    private void Configure(
        BuildableObject owner,
        BuildingStructuralIntegrityAbility sourceAbility)
    {
        building = owner;
        ability = sourceAbility;
        if (!initialized)
        {
            currentHitPoints = MaxHitPoints;
            initialized = true;
        }
        else
        {
            currentHitPoints = Mathf.Clamp(
                currentHitPoints,
                0f,
                MaxHitPoints);
        }
    }

}

public sealed class BuildingStructuralIntegrityRuntime :
    IBuildingStructuralIntegrityRuntime
{
    private readonly IBuildingDestructiveLossRuntime destructiveLoss;

    public BuildingStructuralIntegrityRuntime()
    {
    }

    [Inject]
    public BuildingStructuralIntegrityRuntime(
        IBuildingDestructiveLossRuntime destructiveLoss)
    {
        this.destructiveLoss = destructiveLoss
            ?? throw new ArgumentNullException(nameof(destructiveLoss));
    }

    public bool TryGet(
        BuildableObject building,
        out BuildingStructuralIntegritySnapshot snapshot)
    {
        if (BuildingStructuralIntegrity.TryGet(
                building,
                out BuildingStructuralIntegrity runtime))
        {
            snapshot = runtime.GetSnapshot();
            return true;
        }

        snapshot = default;
        return false;
    }

    public bool IsBreachable(BuildableObject building)
    {
        return TryGet(building, out BuildingStructuralIntegritySnapshot snapshot)
            && snapshot.Breachable
            && !building.isDestroy
            && IsInBreachableArea(building);
    }

    public BuildingStructuralDamageResult ApplyDamage(
        BuildableObject building,
        float damage)
    {
        if (!BuildingStructuralIntegrity.TryGet(
                building,
                out BuildingStructuralIntegrity runtime))
        {
            return new BuildingStructuralDamageResult(
                false,
                false,
                0f,
                default,
                "building-structural-integrity-missing");
        }

        BuildingStructuralIntegritySnapshot before = runtime.GetSnapshot();
        bool lethal = damage > 0f
            && before.Breachable
            && damage >= before.CurrentHitPoints;
        if (!lethal)
            return runtime.ApplyDamage(damage);

        if (destructiveLoss == null)
        {
            return new BuildingStructuralDamageResult(
                false,
                false,
                0f,
                before,
                "building-destructive-loss-runtime-missing");
        }

        BuildingDestructiveLossResult removal = destructiveLoss.Apply(
            building,
            ProductionFacilityDestructiveDrainCause.StructuralIntegrity);
        if (!removal.Removed)
        {
            return new BuildingStructuralDamageResult(
                false,
                false,
                0f,
                before,
                removal.FailureReason);
        }

        float applied = Mathf.Min(damage, before.CurrentHitPoints);
        BuildingStructuralIntegritySnapshot destroyed =
            new BuildingStructuralIntegritySnapshot(
                building,
                0f,
                before.MaxHitPoints,
                before.Toughness,
                before.Breachable,
                BuildingCrackStage.Critical);
        return new BuildingStructuralDamageResult(
            true,
            true,
            applied,
            destroyed,
            removal.FailureReason);
    }

    public bool TryApplyRepairWork(
        BuildableObject building,
        float workAmount,
        out bool completed,
        out BuildingStructuralIntegritySnapshot snapshot)
    {
        completed = false;
        snapshot = default;
        if (!BuildingStructuralIntegrity.TryGet(
                building,
                out BuildingStructuralIntegrity runtime))
        {
            return false;
        }

        bool applied = runtime.ApplyRepairWork(workAmount, out completed);
        snapshot = runtime.GetSnapshot();
        return applied;
    }

    private static bool IsInBreachableArea(BuildableObject building)
    {
        Grid grid = building?.Grid;
        IReadOnlyList<Vector2Int> positions = building?.buildPoses;
        if (grid == null || positions == null || positions.Count == 0)
        {
            return true;
        }

        foreach (Vector2Int position in positions)
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null
                || cell.AreaType == GridCellAreaType.BlockedExterior)
            {
                return false;
            }
        }

        return true;
    }
}
