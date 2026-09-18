using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// Shared stat-to-consumer policy for facility evolution. Runtime projection
/// keeps using the role/work selectors below; new-generation authorities use
/// <see cref="AreAllStatsReachable"/> to additionally require an authored
/// operative work consumer on the target definition.
/// </summary>
public static class FacilityEvolutionModifierApplicability
{
    private const string WorkOutputStatId = "work.output";
    private const string ServiceSpeedStatId = "service.speed";
    private const string TrainingSpeedStatId = "training.speed";
    private const string SecuritySpeedStatId = "security.speed";
    private const string ServiceSupportSpeedStatId = "service.support-speed";
    private const string ResearchOutputStatId = "research.output";
    private const string SurvivalOutputStatId = "survival.output";
    private const string DefenseOutputStatId = "defense.output";
    private const string EntertainmentOutputStatId = "entertainment.output";
    private const FacilityRole ServiceRoles =
        FacilityRole.Meal
        | FacilityRole.Purchase
        | FacilityRole.Rest
        | FacilityRole.Training
        | FacilityRole.Toilet
        | FacilityRole.Hygiene;

    public static string ResolveOutputStat(
        FacilityRole roles,
        WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Research)
            return ResearchOutputStatId;
        if ((roles & FacilityRole.Entertainment) != 0)
            return EntertainmentOutputStatId;
        if ((roles & FacilityRole.Security) != 0)
            return DefenseOutputStatId;
        if ((roles & (FacilityRole.Meal
                      | FacilityRole.Rest
                      | FacilityRole.Toilet
                      | FacilityRole.Hygiene)) != 0)
        {
            return SurvivalOutputStatId;
        }

        return WorkOutputStatId;
    }

    public static bool IsServiceSpeedProjected(
        FacilityRole roles,
        WorkTypeId workTypeId) =>
        workTypeId == BuiltInWorkTypeIds.Operate
        && (roles & ServiceRoles) != 0;

    public static bool AreAllStatsReachable(
        BuildingSO targetDefinition,
        IEnumerable<EvolutionEffectModifier> modifiers,
        string modifierKind,
        out string failureReason)
    {
        failureReason = string.Empty;
        EvolutionEffectModifier[] values = (modifiers
                ?? Array.Empty<EvolutionEffectModifier>())
            .Where(value => value != null)
            .ToArray();
        foreach (string statId in values
                     .Select(value => value.statId?.Trim() ?? string.Empty)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!IsStatReachable(targetDefinition, statId, out string statReason))
            {
                failureReason = (modifierKind?.Trim() ?? "facility modifier")
                    + " stat '" + statId + "' has no reachable target consumer: "
                    + statReason;
                return false;
            }
        }

        return true;
    }

    public static bool IsPositiveModuleEligibleForNewGeneration(
        BuildingSO targetDefinition,
        EvolutionModuleDefinition module,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (module == null || !module.IsPositiveModule || module.Benefits.Count == 0)
        {
            failureReason = "module is not an authored positive benefit";
            return false;
        }
        if (module.HasInternallySelfCancellingPositiveAxis)
        {
            failureReason = "module '" + module.ModuleId
                + "' has an internally self-cancelling positive stat axis";
            return false;
        }
        if (!AreAllStatsReachable(
                targetDefinition,
                module.Benefits,
                "positive benefit",
                out failureReason))
        {
            return false;
        }
        return AreAllStatsReachable(
            targetDefinition,
            module.Burdens,
            "mandatory burden",
            out failureReason);
    }

    public static bool IsStatReachable(
        BuildingSO targetDefinition,
        string statId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (targetDefinition == null)
        {
            failureReason = "target building definition is missing";
            return false;
        }

        string canonical = statId?.Trim() ?? string.Empty;
        if (string.Equals(
                canonical,
                ServiceSupportSpeedStatId,
                StringComparison.Ordinal))
        {
            BuildingServiceSupportAbility support = targetDefinition
                .GetAbility<BuildingServiceSupportAbility>();
            if (support?.IsValid != true)
            {
                failureReason = "target building has no valid service-support ability that consumes service support speed";
                return false;
            }

            return true;
        }

        FacilityData facility = targetDefinition.Facility;
        if (facility == null)
        {
            failureReason = "target building has no facility metadata";
            return false;
        }

        WorkTypeId[] supported = facility.SupportedWorkTypeIds
            .Where(value => value.IsValid)
            .Distinct()
            .ToArray();
        if (supported.Length == 0)
        {
            failureReason = "target building has no supported operative work type";
            return false;
        }

        WorkTypeId[] productionWork = supported
            .Where(IsProductionWork)
            .ToArray();
        if (IsOutputStat(canonical)
            && targetDefinition.GetProductionAbility() == null)
        {
            failureReason =
                "target building has no valid production ability that consumes output modifiers";
            return false;
        }
        if (IsOutputStat(canonical) && productionWork.Length == 0)
        {
            failureReason =
                "target building has no supported operate or research work for its production ability";
            return false;
        }
        bool reachable = canonical switch
        {
            // GetOutputMultiplier always composes work.output with a specific
            // output stat, so any actual production work consumes this axis.
            WorkOutputStatId => productionWork.Length > 0,
            ServiceSpeedStatId => facility.SupportsWork(BuiltInWorkTypeIds.Operate)
                && IsServiceSpeedProjected(facility.roles, BuiltInWorkTypeIds.Operate),
            TrainingSpeedStatId => facility.SupportsRole(FacilityRole.Training)
                && facility.SupportsWork(BuiltInWorkTypeIds.Operate),
            SecuritySpeedStatId => facility.SupportsRole(FacilityRole.Security)
                && facility.SupportsWork(BuiltInWorkTypeIds.Guard)
                && targetDefinition.GetAbility<BuildingSecurityAbility>() != null,
            ResearchOutputStatId => productionWork.Any(workTypeId =>
                string.Equals(
                    ResolveOutputStat(facility.roles, workTypeId),
                    ResearchOutputStatId,
                    StringComparison.Ordinal)),
            SurvivalOutputStatId or DefenseOutputStatId or EntertainmentOutputStatId =>
                productionWork.Any(workTypeId => string.Equals(
                    ResolveOutputStat(facility.roles, workTypeId),
                    canonical,
                    StringComparison.Ordinal)),
            _ => false
        };
        if (!reachable)
        {
            failureReason = "target role/work metadata does not project this stat";
            return false;
        }

        return true;
    }

    private static bool IsOutputStat(string statId) => statId is
        WorkOutputStatId
        or ResearchOutputStatId
        or SurvivalOutputStatId
        or DefenseOutputStatId
        or EntertainmentOutputStatId;

    private static bool IsProductionWork(WorkTypeId workTypeId) =>
        workTypeId == BuiltInWorkTypeIds.Operate
        || workTypeId == BuiltInWorkTypeIds.Research;
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
        string statId = FacilityEvolutionModifierApplicability.ResolveOutputStat(
            facility?.BuildingData?.Facility?.roles ?? FacilityRole.None,
            workTypeId);
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
        if (facility?.BuildingData?.Facility != null
            )
        {
            FacilityRole roles = facility.BuildingData.Facility.roles;
            float multiplier = 1f;
            if (FacilityEvolutionModifierApplicability.IsServiceSpeedProjected(
                    roles,
                    workTypeId))
            {
                multiplier *= GetMultiplier(facility, "service.speed");
            }
            if (workTypeId == BuiltInWorkTypeIds.Operate
                && (roles & FacilityRole.Training) != 0)
            {
                multiplier *= GetMultiplier(facility, "training.speed");
            }
            if (workTypeId == BuiltInWorkTypeIds.Guard
                && (roles & FacilityRole.Security) != 0)
            {
                multiplier *= GetMultiplier(facility, "security.speed");
            }

            return Mathf.Clamp(multiplier, 0.1f, 8f);
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

                if (node.formulaVersion <
                        FacilityFormulaEvolutionAuthority.DrawbackModuleSelectionFormulaVersion
                    || module.BurdenKind is EvolutionModuleBurdenKind.OperatingCost
                        or EvolutionModuleBurdenKind.InseparableRisk)
                {
                    Apply(
                        module.Burdens,
                        statId,
                        potency,
                        ref additive,
                        ref multiplier);
                }
                else if (module.BurdenKind == EvolutionModuleBurdenKind.OptionalDrawback)
                {
                    throw new InvalidOperationException(
                        "An optional facility drawback cannot be applied as a positive module.");
                }
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
                    node.formulaVersion >=
                        FacilityFormulaEvolutionAuthority.DrawbackModuleSelectionFormulaVersion
                        ? Mathf.Max(1f, node.burdenPotencyMultiplier)
                        : potency,
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

}
