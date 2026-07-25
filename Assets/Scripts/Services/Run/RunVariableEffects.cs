using System;
using System.Collections.Generic;
using UnityEngine;

public interface IRunVariableEffect
{
}

public interface IRunVariableMultiplierEffect<in TContext> : IRunVariableEffect
{
    float GetMultiplier(TContext context);
}

public interface IRunVariableInvasionEffect : IRunVariableEffect
{
    void Apply(InvasionIntruderSettings settings);
}

public readonly struct RunThreatRiseQuery
{
}

public readonly struct RunWarningThresholdQuery
{
}

public sealed class RunGuestDemandEffect : IRunVariableMultiplierEffect<string>
{
    private readonly string speciesTag;
    private readonly float multiplier;

    public RunGuestDemandEffect(string speciesTag, float multiplier)
    {
        this.speciesTag = speciesTag?.Trim() ?? string.Empty;
        this.multiplier = Mathf.Max(0.1f, multiplier);
    }

    public float GetMultiplier(string context)
    {
        return !string.IsNullOrWhiteSpace(context)
            && string.Equals(speciesTag, context, StringComparison.OrdinalIgnoreCase)
                ? multiplier
                : 1f;
    }
}

public sealed class RunStockCostEffect : IRunVariableMultiplierEffect<StockCategory>
{
    private readonly StockCategory category;
    private readonly float multiplier;

    public RunStockCostEffect(StockCategory category, float multiplier)
    {
        this.category = category;
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public float GetMultiplier(StockCategory context)
    {
        return category == context ? multiplier : 1f;
    }
}

public sealed class RunFacilityShopCostEffect : IRunVariableMultiplierEffect<BuildingSO>
{
    private readonly bool defenseOnly;
    private readonly float multiplier;

    public RunFacilityShopCostEffect(float multiplier, bool defenseOnly = false)
    {
        this.defenseOnly = defenseOnly;
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public float GetMultiplier(BuildingSO context)
    {
        if (!defenseOnly)
        {
            return multiplier;
        }

        return context != null && context.Defense != null && context.Defense.IsDefenseFacility
            ? multiplier
            : 1f;
    }
}

public sealed class RunBlueprintCostEffect : IRunVariableMultiplierEffect<FacilityBlueprintSO>
{
    private readonly float multiplier;

    public RunBlueprintCostEffect(float multiplier)
    {
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public float GetMultiplier(FacilityBlueprintSO context)
    {
        return multiplier;
    }
}

public sealed class RunThreatRiseEffect : IRunVariableMultiplierEffect<RunThreatRiseQuery>
{
    private readonly float multiplier;

    public RunThreatRiseEffect(float multiplier)
    {
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public float GetMultiplier(RunThreatRiseQuery context)
    {
        return multiplier;
    }
}

public sealed class RunWarningThresholdEffect : IRunVariableMultiplierEffect<RunWarningThresholdQuery>
{
    private readonly float multiplier;

    public RunWarningThresholdEffect(float multiplier)
    {
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public float GetMultiplier(RunWarningThresholdQuery context)
    {
        return multiplier;
    }
}

public sealed class RunIntruderPatternEffect : IRunVariableInvasionEffect
{
    private readonly string patternId;

    public RunIntruderPatternEffect(string patternId)
    {
        this.patternId = string.IsNullOrWhiteSpace(patternId)
            ? InvasionIntruderPatternIds.Hunter
            : patternId.Trim();
    }

    public void Apply(InvasionIntruderSettings settings)
    {
        if (settings != null)
        {
            settings.patternId = patternId;
        }
    }
}

public sealed class RunFocusTimeEffect : IRunVariableInvasionEffect
{
    private readonly float multiplier;

    public RunFocusTimeEffect(float multiplier)
    {
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public void Apply(InvasionIntruderSettings settings)
    {
        settings.secondsToFullFocus = Mathf.Max(0.1f, settings.secondsToFullFocus * multiplier);
    }
}

public sealed class RunRepathIntervalEffect : IRunVariableInvasionEffect
{
    private readonly float multiplier;

    public RunRepathIntervalEffect(float multiplier)
    {
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public void Apply(InvasionIntruderSettings settings)
    {
        settings.repathIntervalSeconds = Mathf.Max(0.1f, settings.repathIntervalSeconds * multiplier);
    }
}

public sealed class RunFacilityDamageIntervalEffect : IRunVariableInvasionEffect
{
    private readonly float multiplier;

    public RunFacilityDamageIntervalEffect(float multiplier)
    {
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public void Apply(InvasionIntruderSettings settings)
    {
        settings.facilityDamageIntervalSeconds = Mathf.Max(
            0f,
            settings.facilityDamageIntervalSeconds * multiplier);
    }
}

public sealed class RunFinalCombatDamageEffect : IRunVariableInvasionEffect
{
    private readonly float multiplier;

    public RunFinalCombatDamageEffect(float multiplier)
    {
        this.multiplier = Mathf.Max(0.05f, multiplier);
    }

    public void Apply(InvasionIntruderSettings settings)
    {
        settings.finalCombatDamage = Mathf.Max(0f, settings.finalCombatDamage * multiplier);
    }
}

public static class RunVariableEffects
{
    public static float GetGuestDemandMultiplier(IRunVariableStateView state, string speciesTag)
    {
        return AggregateOperationMultiplier(state, speciesTag);
    }

    public static float GetStockCostMultiplier(IRunVariableStateView state, StockCategory category)
    {
        return AggregateOperationMultiplier(state, category);
    }

    public static float GetFacilityShopCostMultiplier(IRunVariableStateView state, BuildingSO building)
    {
        return AggregateOperationMultiplier(state, building);
    }

    public static float GetBlueprintCostMultiplier(IRunVariableStateView state, FacilityBlueprintSO blueprint)
    {
        return AggregateOperationMultiplier(state, blueprint);
    }

    public static float GetThreatRiseMultiplier(IRunVariableStateView state)
    {
        float startMultiplier = state?.StartVariables != null
            ? Mathf.Max(0.05f, state.StartVariables.threatRiseMultiplier)
            : 1f;
        return startMultiplier * AggregateOperationMultiplier(state, new RunThreatRiseQuery());
    }

    public static float GetWarningThresholdMultiplier(IRunVariableStateView state)
    {
        return AggregateOperationMultiplier(state, new RunWarningThresholdQuery());
    }

    public static InvasionIntruderSettings ApplyInvasionSettings(
        IRunVariableStateView state,
        InvasionIntruderSettings source)
    {
        InvasionIntruderSettings result = CloneSettings(source);
        ApplyInvasionEffects(
            OwnerDoctrineCatalog.Get(state?.StartVariables?.ownerDoctrineId)?.effects,
            result);
        RunVariableDefinition definition = state?.CurrentInvasionVariable;
        ApplyInvasionEffects(definition?.effects, result);

        return result;
    }

    private static float AggregateOperationMultiplier<TContext>(
        IRunVariableStateView state,
        TContext context)
    {
        if (state == null)
        {
            return 1f;
        }

        float multiplier = AggregateEffects(
            OwnerDoctrineCatalog.Get(state.StartVariables?.ownerDoctrineId)?.effects,
            context);
        IReadOnlyList<ActiveRunVariable> activeVariables = state.ActiveOperationVariables;
        for (int i = 0; i < activeVariables.Count; i++)
        {
            IReadOnlyList<IRunVariableEffect> effects = activeVariables[i]?.Definition?.effects;
            if (effects == null)
            {
                continue;
            }

            multiplier *= AggregateEffects(effects, context);
        }

        return multiplier;
    }

    private static float AggregateEffects<TContext>(
        IReadOnlyList<IRunVariableEffect> effects,
        TContext context)
    {
        float multiplier = 1f;
        if (effects == null)
        {
            return multiplier;
        }

        for (int index = 0; index < effects.Count; index++)
        {
            if (effects[index] is IRunVariableMultiplierEffect<TContext> effect)
            {
                multiplier *= Mathf.Max(0.05f, effect.GetMultiplier(context));
            }
        }

        return multiplier;
    }

    private static void ApplyInvasionEffects(
        IReadOnlyList<IRunVariableEffect> effects,
        InvasionIntruderSettings settings)
    {
        if (effects == null || settings == null)
        {
            return;
        }

        for (int index = 0; index < effects.Count; index++)
        {
            if (effects[index] is IRunVariableInvasionEffect invasionEffect)
            {
                invasionEffect.Apply(settings);
            }
        }
    }

    private static InvasionIntruderSettings CloneSettings(InvasionIntruderSettings source)
    {
        InvasionIntruderSettings result = new InvasionIntruderSettings();
        if (source == null)
        {
            return result;
        }

        result.patternId = source.patternId;
        result.rallyDurationSeconds = source.rallyDurationSeconds;
        result.secondsToFullFocus = source.secondsToFullFocus;
        result.repathIntervalSeconds = source.repathIntervalSeconds;
        result.facilityDamageIntervalSeconds = source.facilityDamageIntervalSeconds;
        result.finalCombatDamage = source.finalCombatDamage;
        result.finalCombatWindupSeconds = source.finalCombatWindupSeconds;
        result.healthMultiplier = source.healthMultiplier;
        result.meleeDamageMultiplier = source.meleeDamageMultiplier;
        result.attackSpeedMultiplier = source.attackSpeedMultiplier;
        return result;
    }
}
