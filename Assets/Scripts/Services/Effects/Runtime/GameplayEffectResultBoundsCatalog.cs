using System;
using System.Collections.Generic;
using System.Linq;

public interface IGameplayEffectResultBoundsQuery
{
    float RequireFiniteMaximum(string targetId);
    GameplayEffectResultBoundsSnapshot CaptureFiniteBounds(string targetId);
}

public readonly struct GameplayEffectResultBoundsSnapshot
{
    public GameplayEffectResultBoundsSnapshot(
        string targetId,
        float minimum,
        float maximum,
        string sourceDigest)
    {
        if (string.IsNullOrWhiteSpace(targetId)
            || !string.Equals(targetId, targetId.Trim(), StringComparison.Ordinal)
            || float.IsNaN(minimum)
            || float.IsInfinity(minimum)
            || float.IsNaN(maximum)
            || float.IsInfinity(maximum)
            || minimum > maximum
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Gameplay effect result-bound snapshot is invalid.");
        }
        TargetId = targetId;
        Minimum = minimum;
        Maximum = maximum;
        SourceDigest = sourceDigest;
    }

    public string TargetId { get; }
    public float Minimum { get; }
    public float Maximum { get; }
    public string SourceDigest { get; }
    public double AbsoluteMaximum => Math.Max(
        Math.Abs((double)Minimum),
        Math.Abs((double)Maximum));
}

/// <summary>
/// Captures authored result clamps once so capacity projectors can prove a
/// future-safe upper envelope without inspecting active character state.
/// Every definition that can own a target must publish a finite maximum;
/// otherwise definition-only projection fails loudly.
/// </summary>
public sealed class GameplayEffectResultBoundsCatalog :
    IGameplayEffectResultBoundsQuery
{
    private readonly IReadOnlyDictionary<string, GameplayEffectResultBoundsSnapshot>
        boundsByTarget;

    public GameplayEffectResultBoundsCatalog(IGameContentDefinitionSource content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<GameplayEffectDefinitionSO>())
    {
    }

    public GameplayEffectResultBoundsCatalog(
        IEnumerable<GameplayEffectDefinitionSO> definitions)
    {
        GameplayEffectDefinitionSO[] source = (definitions
                ?? throw new ArgumentNullException(nameof(definitions)))
            .Where(value => value != null)
            .OrderBy(value => value.EffectId, StringComparer.Ordinal)
            .ToArray();
        if (source.Any(value => !Canonical(value.EffectId)
                || !Canonical(value.TargetId)
                || value.ValidateDefinition().Count != 0)
            || source.Select(value => value.EffectId)
                .Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            throw new InvalidOperationException(
                "Gameplay effect result-bound catalog contains invalid definitions.");
        }

        Dictionary<string, GameplayEffectResultBoundsSnapshot> captured = new(
            StringComparer.Ordinal);
        foreach (IGrouping<string, GameplayEffectDefinitionSO> group in source
                     .GroupBy(value => value.TargetId, StringComparer.Ordinal))
        {
            GameplayEffectDefinitionSO[] ordered = group
                .OrderBy(value => value.EffectId, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Any(value => float.IsNaN(value.MinimumResult)
                    || float.IsInfinity(value.MinimumResult)
                    || float.IsNaN(value.MaximumResult)
                    || float.IsInfinity(value.MaximumResult)))
            {
                throw new InvalidOperationException(
                    "Gameplay effect target requires finite authored bounds: "
                    + group.Key);
            }
            float minimum = Math.Min(
                1f,
                ordered.Min(value => value.MinimumResult));
            float maximum = Math.Max(
                1f,
                ordered.Max(value => value.MaximumResult));
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("gameplay-effect-result-bounds@2");
            digest.Append(group.Key);
            digest.Append(ordered.Length);
            foreach (GameplayEffectDefinitionSO definition in ordered)
            {
                digest.Append(definition.EffectId);
                digest.Append(definition.NumericId);
                digest.Append(definition.TargetId);
                digest.AppendEnum(definition.Operation);
                digest.AppendEnum(definition.ProjectionPhase);
                digest.AppendEnum(definition.AllowedSources);
                digest.AppendEnum(definition.StackingPolicy);
                digest.AppendFloat(definition.MinimumResult);
                digest.AppendFloat(definition.MaximumResult);
            }
            digest.AppendFloat(minimum);
            digest.AppendFloat(maximum);
            captured.Add(
                group.Key,
                new GameplayEffectResultBoundsSnapshot(
                    group.Key,
                    minimum,
                    maximum,
                    digest.ComputeSha256()));
        }
        boundsByTarget = captured;
    }

    public float RequireFiniteMaximum(string targetId)
    {
        if (!Canonical(targetId)
            || !boundsByTarget.TryGetValue(
                targetId,
                out GameplayEffectResultBoundsSnapshot bounds))
        {
            throw new InvalidOperationException(
                "Gameplay effect target has no finite maximum authority: "
                + (targetId ?? string.Empty));
        }
        return bounds.Maximum;
    }

    public GameplayEffectResultBoundsSnapshot CaptureFiniteBounds(
        string targetId)
    {
        if (!Canonical(targetId)
            || !boundsByTarget.TryGetValue(
                targetId,
                out GameplayEffectResultBoundsSnapshot bounds))
        {
            throw new InvalidOperationException(
                "Gameplay effect target has no finite result-bound authority: "
                + (targetId ?? string.Empty));
        }
        return bounds;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
