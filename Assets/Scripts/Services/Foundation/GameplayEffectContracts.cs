using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Flags]
public enum GameplayEffectSourceKind
{
    None = 0,
    Trait = 1 << 0,
    Species = 1 << 1,
    Equipment = 1 << 2,
    EquipmentModule = 1 << 3,
    Status = 1 << 4,
    Research = 1 << 5,
    SurgicalPart = 1 << 6,
    AcquiredTrait = 1 << 7,
    All = Trait | Species | Equipment | EquipmentModule | Status | Research
        | SurgicalPart | AcquiredTrait
}

public enum GameplayEffectOperation
{
    AddFlat,
    AddPercent,
    Multiply,
    Override,
    ClampMinimum,
    ClampMaximum
}

public enum GameplayEffectProjectionPhase
{
    BaseAdd = 0,
    AdditivePercent = 1,
    Multiplicative = 2,
    Override = 3,
    Clamp = 4
}

public enum GameplayEffectStackingPolicy
{
    StackAll,
    HighestMagnitude,
    LowestMagnitude,
    UniquePerDefinition,
    UniquePerSource
}

public static class GameplayEffectTargetIds
{
    public const string WorkSpeed = "character:work-speed";
    public const string ResearchSpeed = "character:research-speed";
    public const string CombatPower = "character:combat-power";
    public const string MoveSpeed = "character:move-speed";
    public const string Consumption = "character:consumption";
    public const string Spending = "character:spending";
    public const string WaitPatience = "character:wait-patience";
    public const string CrowdSensitivity = "character:crowd-sensitivity";
    public const string AccidentChance = "character:accident-chance";
    public const string StayDuration = "character:stay-duration";
    public const string EarnedWorkExperience = "character:earned-work-xp";
    public const string ColdExposure = "character:cold-exposure";
    public const string HeatExposure = "character:heat-exposure";
    public const string MaximumHealth = "character:maximum-health";
    public const string EvasionChance = "combat:evasion-chance";
    public const string CraftQualityScore = "craft:quality-score";
    public const string SalvageYield = "work:salvage-yield";
    public const string HaulCapacity = "work:haul-capacity";
    public const string FatigueRate = "character:fatigue-rate";
    public const string RecoverySpeed = "character:recovery-speed";
    public const string DiseaseResistance = "character:disease-resistance";
    public const string DiseaseRecoverySpeed = "character:disease-recovery-speed";
    public const string ImmunityGain = "character:immunity-gain";
    public const string ImmunityRetention = "character:immunity-retention";
    public const string FoodPoisoningChance = "character:food-poisoning-chance";
    public const string RelationshipRecovery = "character:relationship-recovery";
    public const string NegativeMoodDuration = "character:negative-mood-duration";

    public static string StartingProficiencyExperience(string proficiencyId) =>
        $"proficiency:{NormalizeProficiencyId(proficiencyId)}:starting-xp";

    private static string NormalizeProficiencyId(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.StartsWith("proficiency:", StringComparison.Ordinal)
            ? normalized.Substring("proficiency:".Length)
            : normalized;
    }
}

public sealed class AcquiredTraitBurdenTargetDefinition
{
    public AcquiredTraitBurdenTargetDefinition(
        string targetId,
        string displayName,
        bool higherIsHarmful,
        string runtimeConsumer)
    {
        TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        HigherIsHarmful = higherIsHarmful;
        RuntimeConsumer = runtimeConsumer ?? throw new ArgumentNullException(nameof(runtimeConsumer));
    }

    public string TargetId { get; }
    public string DisplayName { get; }
    public bool HigherIsHarmful { get; }
    public string RuntimeConsumer { get; }
}

/// <summary>Closed list of directionally unambiguous acquired-trait burdens.</summary>
public static class AcquiredTraitBurdenTargetCatalog
{
    private static readonly IReadOnlyDictionary<string, AcquiredTraitBurdenTargetDefinition> ById;

    static AcquiredTraitBurdenTargetCatalog()
    {
        AcquiredTraitBurdenTargetDefinition[] values =
        {
            Lower(GameplayEffectTargetIds.WorkSpeed, "작업 속도", "CharacterWorkSpeed"),
            Lower(GameplayEffectTargetIds.ResearchSpeed, "연구 속도", "CharacterResearchSpeed"),
            Lower(GameplayEffectTargetIds.CombatPower, "전투력", "CharacterCombatPower"),
            Lower(GameplayEffectTargetIds.MoveSpeed, "이동 속도", "CharacterMoveSpeed"),
            Higher(GameplayEffectTargetIds.Consumption, "소비량", "CharacterConsumption"),
            Higher(GameplayEffectTargetIds.AccidentChance, "사고 확률", "CharacterAccidentChance"),
            Lower(GameplayEffectTargetIds.EarnedWorkExperience, "작업 경험치", "CharacterEarnedWorkExperience"),
            Higher(GameplayEffectTargetIds.ColdExposure, "추위 노출", "CharacterColdExposure"),
            Higher(GameplayEffectTargetIds.HeatExposure, "더위 노출", "CharacterHeatExposure"),
            Lower(GameplayEffectTargetIds.MaximumHealth, "최대 체력", "CharacterMaximumHealth"),
            Lower(GameplayEffectTargetIds.EvasionChance, "회피 확률", "CombatEvasionChance"),
            Lower(GameplayEffectTargetIds.CraftQualityScore, "제작 품질", "CraftQualityScore"),
            Lower(GameplayEffectTargetIds.SalvageYield, "해체 수율", "SalvageYield"),
            Lower(GameplayEffectTargetIds.HaulCapacity, "운반량", "HaulCapacity"),
            Higher(GameplayEffectTargetIds.FatigueRate, "피로 누적", "FatigueRate"),
            Lower(GameplayEffectTargetIds.RecoverySpeed, "회복 속도", "RecoverySpeed"),
            Lower(GameplayEffectTargetIds.DiseaseResistance, "질병 저항", "DiseaseResistance"),
            Lower(GameplayEffectTargetIds.DiseaseRecoverySpeed, "질병 회복", "DiseaseRecoverySpeed"),
            Lower(GameplayEffectTargetIds.ImmunityGain, "면역 획득", "ImmunityGain"),
            Lower(GameplayEffectTargetIds.ImmunityRetention, "면역 유지", "ImmunityRetention"),
            Higher(GameplayEffectTargetIds.FoodPoisoningChance, "식중독 확률", "FoodPoisoningChance"),
            Lower(GameplayEffectTargetIds.RelationshipRecovery, "관계 회복", "RelationshipRecovery"),
            Higher(GameplayEffectTargetIds.NegativeMoodDuration, "부정 기분 지속", "NegativeMoodDuration")
        };
        ById = values.ToDictionary(value => value.TargetId, StringComparer.Ordinal);
        All = Array.AsReadOnly(values.OrderBy(value => value.TargetId, StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<AcquiredTraitBurdenTargetDefinition> All { get; }

    public static bool TryGet(string targetId, out AcquiredTraitBurdenTargetDefinition definition) =>
        ById.TryGetValue(targetId?.Trim() ?? string.Empty, out definition);

    public static string DisplayName(string targetId) =>
        TryGet(targetId, out AcquiredTraitBurdenTargetDefinition definition)
            ? definition.DisplayName : targetId?.Trim() ?? string.Empty;

    public static bool IsHarmful(
        GameplayEffectDefinitionSO definition,
        float value,
        out string reason)
    {
        if (definition == null)
        {
            reason = "burden effect definition is missing";
            return false;
        }
        if (!TryGet(definition.TargetId, out AcquiredTraitBurdenTargetDefinition target))
        {
            reason = $"burden target '{definition.TargetId}' has no acquired-trait runtime registration";
            return false;
        }
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            reason = $"burden target '{definition.TargetId}' has a non-finite value";
            return false;
        }
        float delta;
        switch (definition.Operation)
        {
            case GameplayEffectOperation.Multiply: delta = value - 1f; break;
            case GameplayEffectOperation.AddFlat:
            case GameplayEffectOperation.AddPercent: delta = value; break;
            default:
                reason = $"burden target '{definition.TargetId}' uses unsupported operation {definition.Operation}";
                return false;
        }
        bool harmful = target.HigherIsHarmful ? delta > 0.000001f : delta < -0.000001f;
        reason = harmful ? string.Empty
            : $"burden target '{definition.TargetId}' changes in a beneficial or neutral direction";
        return harmful;
    }

    private static AcquiredTraitBurdenTargetDefinition Lower(
        string id, string name, string consumer) => new(id, name, false, consumer);

    private static AcquiredTraitBurdenTargetDefinition Higher(
        string id, string name, string consumer) => new(id, name, true, consumer);
}

[Serializable]
public readonly struct GameplayEffectSourceRef : IEquatable<GameplayEffectSourceRef>
{
    public GameplayEffectSourceRef(GameplayEffectSourceKind kind, string sourceId)
    {
        Kind = kind;
        SourceId = sourceId?.Trim() ?? string.Empty;
    }

    public GameplayEffectSourceKind Kind { get; }
    public string SourceId { get; }

    public bool Equals(GameplayEffectSourceRef other) =>
        Kind == other.Kind
        && string.Equals(SourceId, other.SourceId, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is GameplayEffectSourceRef other && Equals(other);

    public override int GetHashCode() => HashCode.Combine((int)Kind, SourceId);
    public override string ToString() => $"{Kind}:{SourceId}";
}

public interface IGameplayEffectSource
{
    GameplayEffectSourceRef SourceRef { get; }
    IReadOnlyList<GameplayEffectBinding> Effects { get; }
}

[Serializable]
public sealed class GameplayEffectBinding
{
    public string bindingId = string.Empty;
    public GameplayEffectDefinitionSO definition;
    public float value = 1f;
    public GameplayEffectConditionDefinitionSO condition;

    public bool IsValidFor(GameplayEffectSourceRef source, out string reason)
    {
        if (string.IsNullOrWhiteSpace(bindingId))
        {
            reason = "binding id is empty";
            return false;
        }
        if (definition == null)
        {
            reason = $"binding '{bindingId}' has no definition";
            return false;
        }
        if ((definition.AllowedSources & source.Kind) == 0)
        {
            reason = $"effect '{definition.EffectId}' does not allow source {source.Kind}";
            return false;
        }
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            reason = $"binding '{bindingId}' has a non-finite value";
            return false;
        }
        reason = string.Empty;
        return true;
    }
}

public sealed class GameplayEffectContext
{
    private readonly HashSet<string> activeConditions;

    public GameplayEffectContext(IEnumerable<string> activeConditionIds = null)
    {
        activeConditions = new HashSet<string>(
            (activeConditionIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()),
            StringComparer.Ordinal);
    }

    public bool IsActive(GameplayEffectConditionDefinitionSO condition) =>
        condition == null || activeConditions.Contains(condition.ConditionId);

    public IReadOnlyCollection<string> ActiveConditionIds => activeConditions;

    public GameplayEffectContext WithConditions(IEnumerable<string> conditionIds) =>
        new GameplayEffectContext(activeConditions.Concat(
            conditionIds ?? Array.Empty<string>()));
}

public sealed class GameplayEffectContribution
{
    public string EffectId { get; set; }
    public GameplayEffectSourceRef Source { get; set; }
    public string BindingId { get; set; }
    public float AuthoredValue { get; set; }
    public float AppliedValue { get; set; }
    public bool Suppressed { get; set; }
    public string SuppressionReason { get; set; } = string.Empty;
    public GameplayEffectDefinitionSO Definition { get; set; }
}

public sealed class GameplayEffectProjectionResult
{
    public GameplayEffectProjectionResult(
        float value,
        IReadOnlyList<GameplayEffectContribution> contributions)
    {
        Value = value;
        Contributions = contributions ?? Array.Empty<GameplayEffectContribution>();
    }

    public float Value { get; }
    public IReadOnlyList<GameplayEffectContribution> Contributions { get; }
}
