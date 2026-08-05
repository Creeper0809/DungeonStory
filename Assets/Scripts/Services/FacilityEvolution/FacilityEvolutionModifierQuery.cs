using System;
using System.Collections.Generic;
using UnityEngine;

public interface IFacilityEvolutionModifierQuery
{
    float GetMultiplier(BuildableObject facility, string statId);
    float GetAdditive(BuildableObject facility, string statId);
    float GetOutputMultiplier(
        BuildableObject facility,
        WorkTypeId workTypeId);
    float GetWorkSpeedMultiplier(
        BuildableObject facility,
        WorkTypeId workTypeId);
}

public sealed class FacilityEvolutionModifierQuery :
    IFacilityEvolutionModifierQuery
{
    private readonly IEvolutionModuleRegistry modules;

    public FacilityEvolutionModifierQuery(
        IEvolutionModuleRegistry modules)
    {
        this.modules = modules
            ?? throw new ArgumentNullException(nameof(modules));
    }

    public float GetMultiplier(BuildableObject facility, string statId)
    {
        Evaluate(facility, statId, out _, out float multiplier);
        return multiplier;
    }

    public float GetAdditive(BuildableObject facility, string statId)
    {
        Evaluate(facility, statId, out float additive, out _);
        return additive;
    }

    public float GetOutputMultiplier(
        BuildableObject facility,
        WorkTypeId workTypeId)
    {
        string statId = workTypeId == BuiltInWorkTypeIds.Research
            ? "research.output"
            : ResolveRoleOutputStat(facility);
        float specific = GetMultiplier(facility, statId);
        float general = string.Equals(
                statId,
                "work.output",
                StringComparison.Ordinal)
            ? 1f
            : GetMultiplier(facility, "work.output");
        return Mathf.Clamp(specific * general, 0.1f, 8f);
    }

    public float GetWorkSpeedMultiplier(
        BuildableObject facility,
        WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Operate
            && facility?.BuildingData?.Facility != null
            && IsServiceRole(facility.BuildingData.Facility.roles))
        {
            return Mathf.Clamp(
                GetMultiplier(facility, "service.speed"),
                0.1f,
                8f);
        }

        return 1f;
    }

    private void Evaluate(
        BuildableObject facility,
        string statId,
        out float additive,
        out float multiplier)
    {
        additive = 0f;
        multiplier = 1f;
        if (facility == null || string.IsNullOrWhiteSpace(statId))
        {
            return;
        }

        // Modifier evaluation is a read model. Refreshing room activation here
        // would turn a work-result query into a state-changing command and make
        // the building ability dispatcher depend back on the full evolution
        // runtime (room -> filth -> ability dispatcher -> modifier query).
        // Activation is refreshed by the evolution command/presentation paths;
        // work execution consumes the last committed component snapshot.
        FacilityEvolutionStateComponent component =
            facility.GetComponent<FacilityEvolutionStateComponent>();
        if (component == null)
        {
            return;
        }

        FacilityEvolutionState state = component.InstanceEvolution;
        HashSet<string> activeBenefits = new HashSet<string>(
            state.activeNodeIds ?? new List<string>(),
            StringComparer.Ordinal);
        foreach (EvolutionNode node in state.evolutionNodes
                     ?? new List<EvolutionNode>())
        {
            if (node == null || node.historical || !node.active)
            {
                continue;
            }

            float potency = Mathf.Max(0.01f, node.potencyMultiplier);
            if (modules.TryGet(
                    node.effectId,
                    out EvolutionModuleDefinition module))
            {
                if (activeBenefits.Contains(node.nodeId))
                {
                    Apply(
                        module.Benefits,
                        statId,
                        potency,
                        ref additive,
                        ref multiplier);
                }

                Apply(
                    module.Burdens,
                    statId,
                    potency,
                    ref additive,
                    ref multiplier);
            }

            if (!string.IsNullOrWhiteSpace(node.burdenEffectId)
                && !string.Equals(
                    node.burdenEffectId,
                    node.effectId,
                    StringComparison.Ordinal)
                && modules.TryGet(
                    node.burdenEffectId,
                    out EvolutionModuleDefinition burdenModule))
            {
                Apply(
                    burdenModule.Burdens,
                    statId,
                    potency,
                    ref additive,
                    ref multiplier);
            }
        }

        multiplier = Mathf.Max(0.05f, multiplier);
    }

    private static void Apply(
        IReadOnlyList<EvolutionEffectModifier> modifiers,
        string statId,
        float potency,
        ref float additive,
        ref float multiplier)
    {
        foreach (EvolutionEffectModifier modifier in modifiers
                     ?? Array.Empty<EvolutionEffectModifier>())
        {
            if (modifier == null
                || !string.Equals(
                    modifier.statId,
                    statId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            additive += modifier.additive * potency;
            multiplier *= Mathf.Max(
                0f,
                1f + (modifier.multiplier - 1f) * potency);
        }
    }

    private static string ResolveRoleOutputStat(BuildableObject facility)
    {
        FacilityRole roles = facility?.BuildingData?.Facility?.roles
            ?? FacilityRole.None;
        if ((roles & FacilityRole.Entertainment) != 0)
        {
            return "entertainment.output";
        }

        if ((roles & FacilityRole.Security) != 0)
        {
            return "defense.output";
        }

        if ((roles & (FacilityRole.Meal
                      | FacilityRole.Rest
                      | FacilityRole.Toilet
                      | FacilityRole.Hygiene)) != 0)
        {
            return "survival.output";
        }

        return "work.output";
    }

    private static bool IsServiceRole(FacilityRole roles)
    {
        const FacilityRole serviceRoles =
            FacilityRole.Meal
            | FacilityRole.Purchase
            | FacilityRole.Rest
            | FacilityRole.Training
            | FacilityRole.Toilet
            | FacilityRole.Hygiene;
        return (roles & serviceRoles) != 0;
    }
}
