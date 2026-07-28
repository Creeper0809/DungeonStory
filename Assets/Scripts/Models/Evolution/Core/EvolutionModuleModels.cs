using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EvolutionModuleActivationKind
{
    Always,
    RoomConditional
}

[Serializable]
public sealed class EvolutionModuleActivationRule
{
    public EvolutionModuleActivationKind kind = EvolutionModuleActivationKind.Always;
    public List<string> requiredRoomTags = new List<string>();
    public List<string> optionalRoomTags = new List<string>();
    public List<string> forbiddenRoomTags = new List<string>();
    [Range(0f, 100f)] public float minimumCleanliness;
    [Range(0f, 100f)] public float minimumBeauty;
    public float minimumTemperature;
    [Range(0f, 100f)] public float minimumSpace;

    public EvolutionModuleActivationRule Clone()
    {
        return new EvolutionModuleActivationRule
        {
            kind = kind,
            requiredRoomTags = Normalize(requiredRoomTags),
            optionalRoomTags = Normalize(optionalRoomTags),
            forbiddenRoomTags = Normalize(forbiddenRoomTags),
            minimumCleanliness = Mathf.Clamp(minimumCleanliness, 0f, 100f),
            minimumBeauty = Mathf.Clamp(minimumBeauty, 0f, 100f),
            minimumTemperature = minimumTemperature,
            minimumSpace = Mathf.Clamp(minimumSpace, 0f, 100f)
        };
    }

    private static List<string> Normalize(IEnumerable<string> values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList() ?? new List<string>();
    }
}

[Serializable]
public sealed class EvolutionEffectModifier
{
    public string statId = string.Empty;
    public float additive;
    public float multiplier = 1f;

    public EvolutionEffectModifier Clone()
    {
        return new EvolutionEffectModifier
        {
            statId = statId ?? string.Empty,
            additive = additive,
            multiplier = Mathf.Max(0f, multiplier)
        };
    }
}

public sealed class EvolutionModuleDefinition
{
    public EvolutionModuleDefinition(
        string moduleId,
        string displayName,
        string roleTag,
        IEnumerable<EvolutionEffectModifier> benefits,
        IEnumerable<EvolutionEffectModifier> burdens,
        EvolutionModuleActivationRule activationRule = null,
        int riskWeight = 0)
    {
        ModuleId = NormalizeRequired(moduleId, nameof(moduleId));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? ModuleId
            : displayName.Trim();
        RoleTag = roleTag?.Trim() ?? string.Empty;
        Benefits = NormalizeModifiers(benefits);
        Burdens = NormalizeModifiers(burdens);
        ActivationRule = activationRule?.Clone() ??
            new EvolutionModuleActivationRule();
        RiskWeight = Mathf.Max(0, riskWeight);
    }

    public string ModuleId { get; }
    public string DisplayName { get; }
    public string RoleTag { get; }
    public IReadOnlyList<EvolutionEffectModifier> Benefits { get; }
    public IReadOnlyList<EvolutionEffectModifier> Burdens { get; }
    public EvolutionModuleActivationRule ActivationRule { get; }
    public int RiskWeight { get; }

    private static string NormalizeRequired(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Evolution module IDs cannot be blank.", parameterName);
        }

        return normalized;
    }

    private static IReadOnlyList<EvolutionEffectModifier> NormalizeModifiers(
        IEnumerable<EvolutionEffectModifier> values)
    {
        return Array.AsReadOnly((values ?? Array.Empty<EvolutionEffectModifier>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.statId))
            .Select(value => value.Clone())
            .ToArray());
    }
}

public interface IEvolutionModuleRegistry
{
    IReadOnlyList<EvolutionModuleDefinition> All { get; }
    bool TryGet(string moduleId, out EvolutionModuleDefinition definition);
}

public readonly struct EvolutionRoomConditionSnapshot
{
    public EvolutionRoomConditionSnapshot(
        IEnumerable<string> tags,
        float cleanliness,
        float beauty,
        float temperature,
        float space)
    {
        Tags = new HashSet<string>(
            tags ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        Cleanliness = Mathf.Clamp(cleanliness, 0f, 100f);
        Beauty = Mathf.Clamp(beauty, 0f, 100f);
        Temperature = temperature;
        Space = Mathf.Clamp(space, 0f, 100f);
    }

    public IReadOnlyCollection<string> Tags { get; }
    public float Cleanliness { get; }
    public float Beauty { get; }
    public float Temperature { get; }
    public float Space { get; }
}

public static class EvolutionModuleActivation
{
    public static bool IsBenefitActive(
        EvolutionModuleActivationRule rule,
        EvolutionRoomConditionSnapshot room)
    {
        if (rule == null || rule.kind == EvolutionModuleActivationKind.Always)
        {
            return true;
        }

        HashSet<string> tags = room.Tags as HashSet<string>
            ?? new HashSet<string>(room.Tags ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (rule.requiredRoomTags.Any(tag => !tags.Contains(tag))
            || rule.forbiddenRoomTags.Any(tags.Contains)
            || room.Cleanliness + 0.001f < rule.minimumCleanliness
            || room.Beauty + 0.001f < rule.minimumBeauty
            || room.Temperature + 0.001f < rule.minimumTemperature
            || room.Space + 0.001f < rule.minimumSpace)
        {
            return false;
        }

        return rule.optionalRoomTags.Count == 0
            || rule.optionalRoomTags.Any(tags.Contains);
    }
}
