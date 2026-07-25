using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Flags]
public enum DefenseTriggerTiming
{
    None = 0,
    OnEnter = 1 << 0,
    Periodic = 1 << 1,
    Cooldown = 1 << 2,
    GuardResponse = 1 << 3
}

public enum DefenseAttackConcept
{
    None,
    Physical,
    Poison,
    Fire,
    Lightning,
    Ice,
    Guard
}

public enum DefenseTargetRule
{
    EnteringIntruder,
    IntrudersInRoom,
    AllIntrudersInRoom,
    GuardTarget
}

public enum DefenseStatusKind
{
    Corrosion,
    Burn,
    Charge,
    Slow
}

[Serializable]
public class DefenseFacilityData
{
    public bool enabled;
    public DefenseAttackConcept concept;
    public DefenseTriggerTiming triggerTimings;
    public DefenseTargetRule targetRule;
    [Min(0f)] public float cooldownSeconds;
    [Min(0f)] public float periodicIntervalSeconds;
    [Min(0)] public int range;
    [Min(1)] public int star = 1;
    public string combatLogText;
    public DefenseEffectSO[] effectAssets = Array.Empty<DefenseEffectSO>();

    public bool IsDefenseFacility => enabled && concept != DefenseAttackConcept.None;

    public bool SupportsTrigger(DefenseTriggerTiming timing)
    {
        return timing != DefenseTriggerTiming.None && (triggerTimings & timing) != 0;
    }
}

public class DefenseActivationReport
{
    private readonly List<string> effectTags = new List<string>();
    private readonly IReadOnlyList<string> effectTagsView;

    public DefenseActivationReport(DefenseFacility facility, CharacterActor target, DefenseTriggerTiming timing)
    {
        effectTagsView = effectTags.AsReadOnly();
        Facility = facility;
        TargetActor = target;
        Timing = timing;
        Concept = facility != null && facility.BuildingData != null
            ? facility.BuildingData.Defense.concept
            : DefenseAttackConcept.None;
    }

    public DefenseFacility Facility { get; }
    public CharacterActor TargetActor { get; }
    public DefenseTriggerTiming Timing { get; }
    public DefenseAttackConcept Concept { get; }
    public float TotalDamage { get; private set; }
    public float MovementDelaySeconds { get; private set; }
    public IReadOnlyList<string> EffectTags => effectTagsView;
    public bool Triggered => Facility != null && TargetActor != null;

    public void AddDamage(float amount)
    {
        TotalDamage += Mathf.Max(0f, amount);
    }

    public void AddMovementDelay(float seconds)
    {
        MovementDelaySeconds = Mathf.Max(MovementDelaySeconds, seconds);
    }

    public void AddEffectTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag) && !effectTags.Contains(tag))
        {
            effectTags.Add(tag);
        }
    }

    public string FormatSummary()
    {
        string facilityName = Facility != null && Facility.BuildingData != null
            ? Facility.BuildingData.objectName
            : "방어 시설";
        string damageText = TotalDamage > 0f ? $" 피해 {TotalDamage:0.#}" : string.Empty;
        string tags = effectTags.Count > 0 ? $" [{string.Join(", ", effectTags)}]" : string.Empty;
        return $"{facilityName} 발동{damageText}{tags}";
    }

    public DefenseActivationSnapshot CreateSnapshot()
    {
        return new DefenseActivationSnapshot(this);
    }
}

public sealed class DefenseActivationSnapshot
{
    internal DefenseActivationSnapshot(DefenseActivationReport report)
    {
        SourceFacility = report?.Facility;
        Facility = report?.Facility != null ? report.Facility.BuildingData : null;
        FacilityRuntimeId = report?.Facility != null ? report.Facility.GetInstanceID() : 0;
        FacilityName = report?.Facility != null && report.Facility.BuildingData != null
            ? report.Facility.BuildingData.objectName
            : "방어 시설";
        TargetName = report?.TargetActor != null ? report.TargetActor.name : string.Empty;
        Timing = report?.Timing ?? DefenseTriggerTiming.None;
        Concept = report?.Concept ?? DefenseAttackConcept.None;
        TotalDamage = report?.TotalDamage ?? 0f;
        MovementDelaySeconds = report?.MovementDelaySeconds ?? 0f;
        EffectTags = EventPayloadSnapshot.Copy(report?.EffectTags);
        Summary = report?.FormatSummary() ?? string.Empty;
    }

    public DefenseFacility SourceFacility { get; }
    public BuildingSO Facility { get; }
    public int FacilityRuntimeId { get; }
    public string FacilityName { get; }
    public string TargetName { get; }
    public DefenseTriggerTiming Timing { get; }
    public DefenseAttackConcept Concept { get; }
    public float TotalDamage { get; }
    public float MovementDelaySeconds { get; }
    public IReadOnlyList<string> EffectTags { get; }
    public string Summary { get; }

    public string FormatSummary()
    {
        return Summary;
    }
}

public readonly struct DefenseFacilityTriggeredEvent
{
    public DefenseActivationSnapshot report { get; }

    public DefenseFacilityTriggeredEvent(DefenseActivationReport report)
    {
        this.report = report?.CreateSnapshot();
    }

    public DefenseFacilityTriggeredEvent(DefenseActivationSnapshot report)
    {
        this.report = report;
    }
}

public class DefenseFacility : Facility
{
    private float nextTriggerTime;
    private DungeonStory.Foundation.IGameEventBus gameEventBus;

    [VContainer.Inject]
    public void ConstructDefenseFacilityEventBus(
        DungeonStory.Foundation.IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public DefenseFacilityData Defense => BuildingData != null ? BuildingData.Defense : null;
    public float CooldownRemaining => Mathf.Max(0f, nextTriggerTime - GameTime);

    public bool CanTrigger(DefenseTriggerTiming timing, out string failureReason)
    {
        failureReason = string.Empty;
        DefenseFacilityData defense = Defense;
        if (defense == null || !defense.IsDefenseFacility)
        {
            failureReason = "방어 시설 아님";
            return false;
        }

        if (isDestroy)
        {
            failureReason = "시설 파괴됨";
            return false;
        }

        if (IsDamaged && (Facility == null || Facility.disabledWhenDamaged))
        {
            failureReason = "시설 파손";
            return false;
        }

        if (!defense.SupportsTrigger(timing))
        {
            failureReason = "발동 조건 불일치";
            return false;
        }

        if (GameTime < nextTriggerTime)
        {
            failureReason = "쿨타임";
            return false;
        }

        return true;
    }

    public DefenseActivationReport Trigger(
        CharacterActor intruder,
        DefenseTriggerTiming timing,
        IDefenseStatusRuntimeService statusRuntimeService)
    {
        if (intruder == null || !CanTrigger(timing, out _))
        {
            return null;
        }

        DefenseActivationReport report = new DefenseActivationReport(this, intruder, timing);
        nextTriggerTime = GameTime + Mathf.Max(0f, Defense.cooldownSeconds);
        DefenseEffectResolver.ApplyEffects(this, intruder, report, statusRuntimeService);
        intruder.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Damaged,
            report.FormatSummary(),
            this,
            actionId: $"defense:{Defense.concept}",
            reasonCode: timing.ToString(),
            value: report.TotalDamage,
            quantity: report.EffectTags.Count,
            bubbleEligible: true));
        (gameEventBus
            ?? throw new InvalidOperationException(
                $"{nameof(DefenseFacility)} requires "
                + $"{nameof(DungeonStory.Foundation.IGameEventBus)} injection."))
            .Publish(new DefenseFacilityTriggeredEvent(report));
        return report;
    }

}

public static class DefenseFacilityResolver
{
    public static List<DefenseActivationReport> TriggerAt(
        Grid grid,
        CharacterActor intruder,
        Vector2Int position,
        DefenseTriggerTiming timing,
        IDefenseStatusRuntimeService statusRuntimeService)
    {
        if (statusRuntimeService == null)
        {
            throw new ArgumentNullException(nameof(statusRuntimeService));
        }

        List<DefenseActivationReport> reports = new List<DefenseActivationReport>();
        if (grid == null || intruder == null || !grid.IsValidGridPos(position))
        {
            return reports;
        }

        GridCell cell = grid.GetGridCell(position);
        if (cell == null)
        {
            return reports;
        }

        IEnumerable<DefenseFacility> defenses = cell.GetAllOccupants()
            .OfType<DefenseFacility>()
            .Distinct();
        foreach (DefenseFacility defense in defenses)
        {
            DefenseActivationReport report = defense.Trigger(intruder, timing, statusRuntimeService);
            if (report != null)
            {
                reports.Add(report);
            }
        }

        return reports;
    }

}

public static class DefenseEffectResolver
{
    public static void ApplyEffects(
        DefenseFacility facility,
        CharacterActor target,
        DefenseActivationReport report,
        IDefenseStatusRuntimeService statusRuntimeService)
    {
        if (facility == null || target == null || report == null || facility.Defense == null)
        {
            return;
        }

        if (statusRuntimeService == null)
        {
            throw new ArgumentNullException(nameof(statusRuntimeService));
        }

        DefenseFacilityData defense = facility.Defense;
        DefenseStatusRuntime statusRuntime = statusRuntimeService.GetOrAdd(target)
            ?? throw new InvalidOperationException($"{nameof(IDefenseStatusRuntimeService)} could not provide {nameof(DefenseStatusRuntime)}.");
        DefenseEffectContext context = new DefenseEffectContext(target, statusRuntime, report, defense);
        foreach (DefenseEffectSO effectAsset in defense.effectAssets ?? Array.Empty<DefenseEffectSO>())
        {
            if (effectAsset != null)
            {
                effectAsset.Apply(context);
            }
        }
    }

    public static float TickStatuses(
        CharacterActor target,
        float deltaSeconds,
        IDefenseStatusRuntimeService statusRuntimeService)
    {
        if (target == null || target.IsDead)
        {
            return 0f;
        }

        if (statusRuntimeService == null)
        {
            throw new ArgumentNullException(nameof(statusRuntimeService));
        }

        return statusRuntimeService.TickStatuses(target, deltaSeconds);
    }
}

public readonly struct DefenseStatusSnapshot
{
    public DefenseStatusSnapshot(DefenseStatusKind kind, float value, float remainingSeconds, int stacks)
    {
        Kind = kind;
        Value = value;
        RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        Stacks = Mathf.Max(0, stacks);
    }

    public DefenseStatusKind Kind { get; }
    public float Value { get; }
    public float RemainingSeconds { get; }
    public int Stacks { get; }
}

public class DefenseStatusRuntime : MonoBehaviour
{
    private readonly List<DefenseRuntimeStatus> statuses = new List<DefenseRuntimeStatus>();

    public IReadOnlyList<DefenseStatusSnapshot> ActiveStatuses => statuses
        .Select((status) => new DefenseStatusSnapshot(
            status.kind,
            status.value,
            status.remainingSeconds,
            status.stacks))
        .ToArray();

    public int ApplyStatus(DefenseStatusKind kind, float value, float duration, int stacks)
    {
        DefenseRuntimeStatus status = statuses.FirstOrDefault((item) => item.kind == kind);
        if (status == null)
        {
            status = new DefenseRuntimeStatus(kind);
            statuses.Add(status);
        }

        status.value = Mathf.Max(status.value, value);
        status.remainingSeconds = Mathf.Max(status.remainingSeconds, duration);
        status.stacks = kind == DefenseStatusKind.Charge
            ? Mathf.Max(0, status.stacks) + Mathf.Max(1, stacks)
            : Mathf.Max(1, stacks);
        return status.stacks;
    }

    public void ClearStatus(DefenseStatusKind kind)
    {
        statuses.RemoveAll((status) => status.kind == kind);
    }

    public float GetIncomingDamageMultiplier()
    {
        DefenseRuntimeStatus corrosion = statuses.FirstOrDefault((status) => status.kind == DefenseStatusKind.Corrosion);
        return corrosion != null ? 1f + Mathf.Max(0f, corrosion.value) : 1f;
    }

    public float Tick(CharacterActor target, float deltaSeconds)
    {
        if (target == null || deltaSeconds <= 0f)
        {
            return 0f;
        }

        float burnDamage = 0f;
        foreach (DefenseRuntimeStatus status in statuses.ToArray())
        {
            if (status.kind == DefenseStatusKind.Burn && status.value > 0f)
            {
                burnDamage += status.value * deltaSeconds;
            }

            status.remainingSeconds -= deltaSeconds;
            if (status.remainingSeconds <= 0f)
            {
                statuses.Remove(status);
            }
        }

        if (burnDamage > 0f && !target.IsDead)
        {
            target.ApplyDamage(burnDamage, "연소");
        }

        return burnDamage;
    }

    private sealed class DefenseRuntimeStatus
    {
        public DefenseRuntimeStatus(DefenseStatusKind kind)
        {
            this.kind = kind;
        }

        public DefenseStatusKind kind;
        public float value;
        public float remainingSeconds;
        public int stacks;
    }
}
