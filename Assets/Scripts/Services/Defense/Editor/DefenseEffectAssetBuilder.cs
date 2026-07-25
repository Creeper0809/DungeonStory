using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public readonly struct DefenseEffectAssetSpec
{
    private DefenseEffectAssetSpec(
        Type effectType,
        float amount,
        float duration,
        int stacks,
        string logTag)
    {
        EffectType = effectType;
        Amount = Mathf.Max(0f, amount);
        Duration = Mathf.Max(0f, duration);
        Stacks = Mathf.Max(1, stacks);
        LogTag = logTag;
    }

    public Type EffectType { get; }
    public float Amount { get; }
    public float Duration { get; }
    public int Stacks { get; }
    public string LogTag { get; }

    public static DefenseEffectAssetSpec Create<TEffect>(
        float amount,
        float duration,
        int stacks,
        string logTag)
        where TEffect : DefenseEffectSO
    {
        return new DefenseEffectAssetSpec(typeof(TEffect), amount, duration, stacks, logTag);
    }

    public string GetAssetSuffix()
    {
        const string prefix = "Defense";
        const string suffix = "EffectSO";
        string typeName = EffectType?.Name ?? string.Empty;
        if (typeName.StartsWith(prefix, StringComparison.Ordinal))
        {
            typeName = typeName.Substring(prefix.Length);
        }

        if (typeName.EndsWith(suffix, StringComparison.Ordinal))
        {
            typeName = typeName.Substring(0, typeName.Length - suffix.Length);
        }

        return string.IsNullOrWhiteSpace(typeName) ? "Effect" : typeName;
    }
}

public static class DefenseEffectAssetBuilder
{
    public static DefenseEffectSO[] EnsureEffects(
        string assetPathPrefix,
        IReadOnlyList<DefenseEffectAssetSpec> specs)
    {
        if (specs == null || specs.Count == 0)
        {
            return Array.Empty<DefenseEffectSO>();
        }

        DefenseEffectSO[] result = new DefenseEffectSO[specs.Count];
        for (int i = 0; i < specs.Count; i++)
        {
            DefenseEffectAssetSpec spec = specs[i];
            Validate(spec);

            string assetPath = $"{assetPathPrefix}_{i + 1}_{spec.GetAssetSuffix()}.asset";
            DefenseEffectSO effectAsset = AssetDatabase.LoadAssetAtPath<DefenseEffectSO>(assetPath);
            if (effectAsset == null || effectAsset.GetType() != spec.EffectType)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                effectAsset = ScriptableObject.CreateInstance(spec.EffectType) as DefenseEffectSO
                    ?? throw new InvalidOperationException($"Could not create defense effect type {spec.EffectType.FullName}.");
                AssetDatabase.CreateAsset(effectAsset, assetPath);
            }

            effectAsset.Configure(spec.Amount, spec.Duration, spec.Stacks, spec.LogTag);
            EditorUtility.SetDirty(effectAsset);
            result[i] = effectAsset;
        }

        return result;
    }

    private static void Validate(DefenseEffectAssetSpec spec)
    {
        if (spec.EffectType == null
            || spec.EffectType.IsAbstract
            || !typeof(DefenseEffectSO).IsAssignableFrom(spec.EffectType))
        {
            throw new InvalidOperationException("Defense effect specs require a concrete DefenseEffectSO type.");
        }
    }
}
