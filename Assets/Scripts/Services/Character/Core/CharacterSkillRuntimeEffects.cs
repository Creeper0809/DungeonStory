using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public static class CharacterSkillRuntimeEffects
{
#if UNITY_EDITOR
    public static void ResetTransientExecutionStateForDebug()
    {
        foreach (CharacterSkillTransientState state in
                 UnityEngine.Object.FindObjectsByType<CharacterSkillTransientState>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            state.Clear();
        }
    }
#endif

    internal static bool IsManagementModuleReachable(
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        CharacterSkillModuleRule module)
    {
        return CharacterSkillModuleCapabilityRegistry
            .Require(module)
            .IsManagementModuleReachable(kind, trigger);
    }

    public static CharacterCombatAbilityDefinition ToCombatAbility(
        CharacterSkillInstance skill,
        CharacterSkillSystemSettingsSO settings)
    {
        if (skill == null || !skill.IsReady)
        {
            return null;
        }

        List<OffenseCombatEffectModule> effects = new List<OffenseCombatEffectModule>();
        foreach (CharacterSkillModuleSelection selection in skill.modules ?? new List<CharacterSkillModuleSelection>())
        {
            CharacterSkillModuleRule module = FindModule(selection, settings);
            OffenseCombatEffectModule effect = skill.formulaVersion > 0
                ? CreateFormulaCombatEffect(skill, module, settings)
                : CreateCombatEffect(module, module?.FindVariant(selection.variantId));
            if (effect != null)
            {
                effects.Add(effect);
            }
        }

        if (effects.Count == 0)
        {
            return null;
        }

        int cooldown = skill.kind == CharacterSkillKind.Ultimate
            ? Mathf.Max(999, skill.cooldownTurns)
            : skill.cooldownTurns;
        return new CharacterCombatAbilityDefinition(
            skill.id,
            skill.displayName,
            skill.description,
            cooldown,
            ToBattleTarget(skill.target),
            skill.usableFrom == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : skill.usableFrom,
            skill.targetPositions == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : skill.targetPositions,
            effects.ToArray());
    }

    public static void ApplyTriggeredPassives(CharacterActor actor, CharacterSkillTrigger trigger)
    {
        ApplyTriggeredPassives(new CharacterSkillExecutionContext(actor, trigger));
    }

    public static void ApplyTriggeredPassives(CharacterSkillExecutionContext context)
    {
        CharacterProgression progression = context?.Actor?.Progression;
        if (progression == null)
        {
            return;
        }

        foreach (CharacterSkillInstance passive in progression.PassiveSkills
            .Concat(progression.OwnerFixedSkills)
            .Where(item => item != null && item.trigger == context.Trigger))
        {
            ExecuteSkill(context, passive);
        }
    }

    public static void ExecuteSkill(CharacterSkillExecutionContext context, CharacterSkillInstance skill)
    {
        if (context?.Actor == null || skill == null || !TryEnterExecution(context, skill, out string key))
        {
            return;
        }

        try
        {
            if (!TryApplyBattleContext(context, skill))
            {
                ApplyOutsideCombat(context, skill);
            }
        }
        finally
        {
            CharacterSkillTransientState.Ensure(context.Actor).Exit(key);
        }
    }

    public static void BeginWork(
        CharacterActor actor,
        BuildableObject facility,
        WorkTypeId workTypeId,
        string eventId)
    {
        if (actor == null || !workTypeId.IsValid)
        {
            return;
        }

        float speedBonus = GetManagementModuleTotal(actor, "work_speed", CharacterSkillTrigger.WorkStarted)
            + GetManagementModuleTotal(
                actor,
                "work_speed",
                CharacterSkillTrigger.WorkCompleted,
                // The two trigger totals own distinct passives but share active buffs
                // and the management ultimate. Count current formula sources once;
                // preserve the frozen pre-v6 double contribution.
                includeV6AndLaterSharedSources: false);
        CharacterSkillTransientState.Ensure(actor).BeginWork(
            workTypeId,
            CharacterSkillWorkSpeedAuthority.ResolveFromAuthoredBonus(
                speedBonus));

        ApplyTriggeredPassives(new CharacterSkillExecutionContext(
            actor,
            CharacterSkillTrigger.WorkStarted,
            eventId,
            facility: facility,
            workTypeId: workTypeId));
    }

    public static void EndWork(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        CharacterSkillTransientState state =
            actor.GetComponent<CharacterSkillTransientState>();
        if (state == null || !state.IsConfigured)
        {
            if (actor.isActiveAndEnabled && actor.IsRuntimeBridgeConfigured)
            {
                throw new InvalidOperationException(
                    "An active, configured character is missing its scoped skill transient state.");
            }

            // Teardown of an unpublished or only partially injected actor has
            // no scoped skill state to release. Never manufacture a new
            // MonoBehaviour from a cancellation/finally path.
            return;
        }

        state.EndWork();
    }

    public static void TriggerWorkCompleted(
        CharacterActor actor,
        BuildableObject facility,
        WorkTypeId workTypeId,
        string eventId)
    {
        if (actor == null || !workTypeId.IsValid)
        {
            return;
        }

        ApplyTriggeredPassives(new CharacterSkillExecutionContext(
            actor,
            CharacterSkillTrigger.WorkCompleted,
            eventId,
            facility: facility,
            workTypeId: workTypeId));
    }

    public static void ApplyOutsideCombat(CharacterActor actor, CharacterSkillInstance skill)
    {
        ApplyOutsideCombat(new CharacterSkillExecutionContext(actor, skill?.trigger ?? CharacterSkillTrigger.WorkCompleted), skill);
    }

    public static void ApplyOutsideCombat(CharacterSkillExecutionContext context, CharacterSkillInstance skill)
    {
        CharacterActor actor = context?.Actor;
        if (actor == null || skill == null)
        {
            return;
        }
        CharacterActor recipient = context.Trigger == CharacterSkillTrigger.ManualWork
            && context.TargetActor != null
                ? context.TargetActor
                : actor;

        foreach (CharacterSkillModuleSelection selection in skill.modules ?? new List<CharacterSkillModuleSelection>())
        {
            CharacterSkillSystemSettingsSO settings = RequireSettings(actor);
            CharacterSkillModuleRule module = FindModule(selection, settings);
            CharacterSkillNumericVariant variant = skill.formulaVersion == 0
                ? module?.FindVariant(selection.variantId)
                : null;
            if (skill.formulaVersion == 0 && variant == null)
            {
                continue;
            }
            float magnitude = skill.formulaVersion > 0
                ? RequireFormulaParameter(skill, module, NarrativeFormulaParameterIds.Magnitude, settings)
                : variant.primaryValue;
            float duration = skill.formulaVersion > 0
                ? RequireFormulaParameter(skill, module, NarrativeFormulaParameterIds.Duration, settings)
                : variant.duration;

            switch (CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId)
            {
                case "damage":
                    context.TargetActor?.ApplyDamage(
                        Mathf.Max(1f, magnitude),
                        $"스킬: {skill.displayName}");
                    break;
                case "heal":
                    recipient.Stats?.Heal(Mathf.Max(1f, magnitude));
                    break;
                case "guard":
                case "protect":
                    recipient.Stats?.ApplyMoodFactor(
                        $"skill:{skill.id}:protected",
                        $"{skill.displayName}의 보호를 받음",
                        3f,
                        180f,
                        1);
                    break;
                case "cleanse":
                    recipient.Stats?.RemoveMoodFactor("health:injury");
                    break;
                case "needs":
                    RestoreLowestNeed(recipient, magnitude);
                    break;
                case "mood":
                    recipient.Stats?.ApplyMoodFactor(
                        $"skill:{skill.id}:mood",
                        skill.displayName,
                        magnitude,
                        Mathf.Max(30f, duration),
                        1);
                    break;
                case "cleaning":
                    recipient.Stats?.ChangesStat(CharacterCondition.HYGIENE, magnitude);
                    break;
            }
        }
    }

    public static float GetWorkSpeedMultiplier(CharacterActor actor)
    {
        if (actor == null)
        {
            return 1f;
        }

        CharacterSkillTransientState state =
            actor.GetComponent<CharacterSkillTransientState>();
        return state != null ? state.WorkSpeedMultiplier : 1f;
    }

    public static float GetProductionOutputMultiplier(CharacterActor actor)
    {
        float bonus = GetManagementModuleTotal(actor, "output", CharacterSkillTrigger.WorkCompleted);
        return 1f + Mathf.Clamp(bonus, 0f, 3f);
    }

    public static int GetStockProductionBonus(CharacterActor actor)
    {
        float bonus = GetManagementModuleTotal(actor, "stock", CharacterSkillTrigger.WorkCompleted);
        return Mathf.Max(0, Mathf.RoundToInt(bonus));
    }

    public static float GetCleaningSpeedMultiplier(CharacterActor actor)
    {
        float percent = GetManagementModuleTotal(actor, "cleaning", CharacterSkillTrigger.WorkCompleted);
        return 1f + Mathf.Clamp(percent / 100f, 0f, 2f);
    }

    public static float GetRepairSpeedMultiplier(CharacterActor actor)
    {
        float percent = GetManagementModuleTotal(actor, "repair", CharacterSkillTrigger.WorkCompleted);
        return 1f + Mathf.Clamp(percent / 100f, 0f, 2f);
    }

    public static float GetResearchWorkBonus(CharacterActor actor, float seconds)
    {
        float bonusPerSecond = GetManagementModuleTotal(actor, "research", CharacterSkillTrigger.WorkCompleted);
        return Mathf.Max(0f, seconds) * Mathf.Max(0f, bonusPerSecond);
    }

    public static float GetRevenueMultiplier(CharacterActor actor)
    {
        float bonus = GetManagementModuleTotal(actor, "revenue", CharacterSkillTrigger.WorkCompleted);
        return 1f + Mathf.Clamp(
            bonus,
            0f,
            GoldEconomyBalanceRules.MaximumWorkerRevenuePremium - 1f);
    }

    public static float ApplyPositiveRelationshipBonus(CharacterActor actor, float sentiment)
    {
        if (sentiment <= 0f)
        {
            return Mathf.Clamp(sentiment, -1f, 1f);
        }

        float points = GetManagementModuleTotal(actor, "relationship", CharacterSkillTrigger.RelationshipChanged);
        return Mathf.Clamp(sentiment + points / 100f, -1f, 1f);
    }

    public static float GetManagementModuleTotal(
        CharacterActor actor,
        string moduleId,
        CharacterSkillTrigger passiveTrigger)
    {
        return GetManagementModuleTotal(
            actor,
            moduleId,
            passiveTrigger,
            includeV6AndLaterSharedSources: true);
    }

    private static float GetManagementModuleTotal(
        CharacterActor actor,
        string moduleId,
        CharacterSkillTrigger passiveTrigger,
        bool includeV6AndLaterSharedSources)
    {
        CharacterProgression progression = actor?.Progression;
        if (progression == null || string.IsNullOrWhiteSpace(moduleId))
        {
            return 0f;
        }

        float total = 0f;
        IEnumerable<CharacterSkillInstance> passives = progression.PassiveSkills
            .Concat(progression.OwnerFixedSkills)
            .Where(skill => skill != null && skill.trigger == passiveTrigger);
        foreach (CharacterSkillInstance skill in passives)
        {
            total += GetModuleTotal(skill, moduleId, progression.SkillSettings);
        }

        if (actor.TryGetAbility(out AbilityWork work)
            && work.GameCalendar != null)
        {
            foreach (CharacterSkillInstance skill in
                     CharacterManualSkillRuntime.GetActiveBuffSkills(
                         actor,
                         work.GameCalendar.AbsoluteHour))
            {
                if (!includeV6AndLaterSharedSources
                    && skill.formulaVersion
                        >= CharacterSkillFormulaGeneration.EffectiveBoundsFormulaVersion)
                {
                    continue;
                }
                total += GetModuleTotal(
                    skill,
                    moduleId,
                    progression.SkillSettings);
            }
        }

        CharacterSkillInstance ultimate = progression.Ultimate;
        bool managementUltimateActive = ultimate != null
            && ultimate.ultimateDomain == CharacterUltimateDomain.Management
            && progression.GrowthState?.useLimits?.managementOperatingDay >= 0;
        if (managementUltimateActive
            && (includeV6AndLaterSharedSources
                || ultimate.formulaVersion
                    < CharacterSkillFormulaGeneration.EffectiveBoundsFormulaVersion))
        {
            total += GetModuleTotal(ultimate, moduleId, progression.SkillSettings);
        }

        return total;
    }

    public static void ApplyDefenseUltimate(
        CharacterActor defender,
        CharacterSkillInstance skill,
        CharacterActor intruder)
    {
        if (defender == null || skill == null)
        {
            return;
        }

        ApplyOutsideCombat(defender, skill);
        if (intruder == null || intruder.IsDead)
        {
            return;
        }

        float attack = Mathf.Max(
            1f,
            5f * defender.Stats.EvaluatePerformance(
                "performance:combat:melee-power").Value);
        float damage = 0f;
        foreach (CharacterSkillModuleSelection selection in skill.modules ?? new List<CharacterSkillModuleSelection>())
        {
            CharacterSkillSystemSettingsSO settings = RequireSettings(defender);
            CharacterSkillModuleRule module = FindModule(selection, settings);
            CharacterSkillNumericVariant variant = skill.formulaVersion == 0
                ? module?.FindVariant(selection.variantId)
                : null;
            if (skill.formulaVersion == 0 && variant == null)
            {
                continue;
            }
            float magnitude = skill.formulaVersion > 0
                ? RequireFormulaParameter(skill, module, NarrativeFormulaParameterIds.Magnitude, settings)
                : variant.primaryValue;
            float duration = skill.formulaVersion > 0
                ? RequireFormulaParameter(skill, module, NarrativeFormulaParameterIds.Duration, settings)
                : variant.duration;

            damage += CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId switch
            {
                "damage" => attack * Mathf.Max(0f, magnitude),
                "dot" => Mathf.Max(0f, magnitude) * Mathf.Max(1f, duration),
                "conditional_amplify" => attack * Mathf.Max(0f, magnitude) * 0.5f,
                _ => 0f
            };
        }

        if (damage > 0f)
        {
            intruder.ApplyDamage(damage, skill.displayName);
        }
    }

    private static bool TryApplyBattleContext(
        CharacterSkillExecutionContext context,
        CharacterSkillInstance skill)
    {
        if (context?.BattleSession == null || skill == null)
        {
            return false;
        }

        OffenseBattleCombatant source = ResolveBattleSource(context);
        if (source == null || source.IsDead)
        {
            return false;
        }

        List<OffenseCombatEffectModule> effects = new List<OffenseCombatEffectModule>();
        foreach (CharacterSkillModuleSelection selection in skill.modules
            ?? new List<CharacterSkillModuleSelection>())
        {
            CharacterSkillSystemSettingsSO settings = RequireSettings(context.Actor);
            CharacterSkillModuleRule module = FindModule(selection, settings);
            OffenseCombatEffectModule effect = skill.formulaVersion > 0
                ? CreateFormulaCombatEffect(skill, module, settings)
                : CreateCombatEffect(module, module?.FindVariant(selection.variantId));
            if (effect != null)
            {
                effects.Add(effect);
            }
        }

        if (effects.Count == 0)
        {
            return false;
        }

        List<OffenseBattleCombatant> targets = ResolveBattleTargets(context, skill, source).ToList();
        if (targets.Count == 0)
        {
            return false;
        }

        foreach (OffenseBattleCombatant target in targets)
        {
            OffenseBattleEffectContext effectContext =
                new OffenseBattleEffectContext(context.BattleSession, source, target);
            foreach (OffenseCombatEffectModule effect in effects)
            {
                OffenseCombatEffectRuntime.Apply(effect, effectContext);
            }
        }

        return true;
    }

    private static OffenseBattleCombatant ResolveBattleSource(CharacterSkillExecutionContext context)
    {
        string actorId = context.Actor?.Identity?.PersistentId;
        if (!string.IsNullOrWhiteSpace(actorId))
        {
            OffenseBattleCombatant actorCombatant = context.BattleSession.FindCombatant(actorId);
            if (actorCombatant != null)
            {
                return actorCombatant;
            }
        }

        return context.SourceCombatant;
    }

    private static IEnumerable<OffenseBattleCombatant> ResolveBattleTargets(
        CharacterSkillExecutionContext context,
        CharacterSkillInstance skill,
        OffenseBattleCombatant source)
    {
        OffenseBattleSession session = context.BattleSession;
        OffenseBattleTeam allyTeam = source.Team;
        OffenseBattleTeam enemyTeam = allyTeam == OffenseBattleTeam.Allies
            ? OffenseBattleTeam.Enemies
            : OffenseBattleTeam.Allies;

        IEnumerable<OffenseBattleCombatant> candidates = skill.target switch
        {
            CharacterSkillTarget.Self => new[] { source },
            CharacterSkillTarget.Ally => PreferredSingleTarget(context.TargetCombatant, source, allyTeam),
            CharacterSkillTarget.AllAllies => session.GetLivingTeam(allyTeam),
            CharacterSkillTarget.Enemy => PreferredSingleTarget(context.TargetCombatant, source, enemyTeam),
            CharacterSkillTarget.AllEnemies => session.GetLivingTeam(enemyTeam),
            _ => Array.Empty<OffenseBattleCombatant>()
        };

        return candidates
            .Where(target => target != null && !target.IsDead)
            .Where(target => (skill.targetPositions == OffenseFormationMask.None
                    ? OffenseFormationMask.Any
                    : skill.targetPositions) == OffenseFormationMask.Any
                || ((skill.targetPositions & OffenseFormationUtility.ToMask(target.Formation)) != 0));

        IEnumerable<OffenseBattleCombatant> PreferredSingleTarget(
            OffenseBattleCombatant preferred,
            OffenseBattleCombatant fallbackSource,
            OffenseBattleTeam team)
        {
            if (preferred != null && preferred.Team == team && !preferred.IsDead)
            {
                return new[] { preferred };
            }

            if (fallbackSource.Team == team && !fallbackSource.IsDead)
            {
                return new[] { fallbackSource };
            }

            return session.GetLivingTeam(team)
                .OrderBy(target => target.HealthRatio)
                .ThenBy(target => target.PersistentId, StringComparer.Ordinal)
                .Take(1);
        }
    }

    private static OffenseCombatEffectModule CreateCombatEffect(
        CharacterSkillModuleRule module,
        CharacterSkillNumericVariant variant)
    {
        if (module == null || variant == null)
        {
            return null;
        }

        return CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId switch
        {
            "damage" => new OffenseDamageEffect(variant.primaryValue, variant.secondaryValue, variant.count),
            "heal" => new OffenseHealEffect(variant.primaryValue, variant.secondaryValue),
            "guard" => new OffenseGuardEffect(variant.primaryValue, variant.duration),
            "dot" => new OffenseDamageOverTimeEffect(variant.primaryValue, variant.duration),
            "vulnerability" => new OffenseVulnerabilityEffect(variant.primaryValue, variant.duration),
            "delay" => new OffenseDelayEffect(variant.primaryValue),
            "buff" => new OffenseAttackModifierEffect(variant.primaryValue, variant.duration),
            "debuff" => new OffenseAttackModifierEffect(-Mathf.Abs(variant.primaryValue), variant.duration),
            "cleanse" => new OffenseCleanseEffect(variant.count),
            "protect" => new OffenseGuardEffect(variant.primaryValue, variant.duration),
            "reposition" => new OffenseRepositionEffect(Mathf.RoundToInt(variant.primaryValue)),
            "multi_target" => new OffenseMultiTargetEffect(variant.count),
            "conditional_amplify" => new OffenseConditionalAmplifyEffect(variant.primaryValue, variant.secondaryValue),
            "cooldown_adjust" => new OffenseCooldownAdjustEffect(Mathf.RoundToInt(variant.primaryValue)),
            _ => null
        };
    }

    private static OffenseCombatEffectModule CreateFormulaCombatEffect(
        CharacterSkillInstance skill,
        CharacterSkillModuleRule module,
        CharacterSkillSystemSettingsSO settings)
    {
        if (module == null)
            throw new InvalidOperationException("Formula skill references an unknown module.");
        string capabilityId = CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId;
        float magnitude = RequireFormulaParameter(
            skill, module, NarrativeFormulaParameterIds.Magnitude, settings);
        int duration = Mathf.RoundToInt(RequireFormulaParameter(
            skill, module, NarrativeFormulaParameterIds.Duration, settings));
        int count = Mathf.Max(1, Mathf.RoundToInt(RequireFormulaParameter(
            skill, module, NarrativeFormulaParameterIds.Count, settings)));
        int targetCount = Mathf.Max(1, Mathf.RoundToInt(RequireFormulaParameter(
            skill, module, NarrativeFormulaParameterIds.TargetCount, settings)));
        return capabilityId switch
        {
            "damage" => new OffenseDamageEffect(magnitude, 0f, count),
            "heal" => new OffenseHealEffect(magnitude, 0f),
            "guard" => new OffenseGuardEffect(magnitude, duration),
            "dot" => new OffenseDamageOverTimeEffect(magnitude, duration),
            "vulnerability" => new OffenseVulnerabilityEffect(magnitude, duration),
            "delay" => new OffenseDelayEffect(magnitude),
            "buff" => new OffenseAttackModifierEffect(magnitude, duration),
            "debuff" => new OffenseAttackModifierEffect(-Mathf.Abs(magnitude), duration),
            "cleanse" => new OffenseCleanseEffect(count),
            "protect" => new OffenseGuardEffect(magnitude, duration),
            "reposition" => new OffenseRepositionEffect(
                Mathf.Max(1, Mathf.RoundToInt(magnitude))),
            "multi_target" => new OffenseMultiTargetEffect(targetCount),
            "conditional_amplify" => new OffenseConditionalAmplifyEffect(magnitude, 0.5f),
            "cooldown_adjust" => new OffenseCooldownAdjustEffect(-Mathf.Max(1, Mathf.RoundToInt(magnitude))),
            _ => null
        };
    }

    private static CharacterSkillModuleRule FindModule(
        CharacterSkillModuleSelection selection,
        CharacterSkillSystemSettingsSO settings)
    {
        if (selection == null)
        {
            return null;
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }
        return settings.FindModule(selection.moduleId);
    }

    private static CharacterSkillNumericVariant FindVariant(
        CharacterSkillModuleSelection selection,
        CharacterSkillSystemSettingsSO settings)
    {
        return FindModule(selection, settings)?.FindVariant(selection.variantId);
    }

    private static float GetModuleTotal(
        CharacterSkillInstance skill,
        string capabilityId,
        CharacterSkillSystemSettingsSO settings)
    {
        float total = 0f;
        foreach (CharacterSkillModuleSelection selection in skill?.modules
            ?? new List<CharacterSkillModuleSelection>())
        {
            CharacterSkillModuleRule module = FindModule(selection, settings);
            if (module == null
                || !string.Equals(
                    CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId,
                    capabilityId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            total += skill.formulaVersion > 0
                ? Mathf.Max(0f, RequireFormulaParameter(
                    skill,
                    module,
                    NarrativeFormulaParameterIds.Magnitude,
                    settings))
                : Mathf.Max(0f, FindVariant(selection, settings)?.primaryValue ?? 0f);
        }

        return total;
    }

    private static float RequireFormulaParameter(
        CharacterSkillInstance skill,
        CharacterSkillModuleRule module,
        string parameterId,
        CharacterSkillSystemSettingsSO settings)
    {
        if (module == null)
            throw new InvalidOperationException("Formula skill references an unknown module.");
        string capabilityId = CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId;
        return CharacterSkillFormulaGeneration.RequireParameter(
            skill,
            capabilityId,
            parameterId,
            settings);
    }

    private static CharacterSkillSystemSettingsSO RequireSettings(CharacterActor actor) =>
        actor?.Progression?.SkillSettings
        ?? throw new InvalidOperationException(
            "Character progression has no authored skill settings.");

    private static OffenseBattleTargetRule ToBattleTarget(CharacterSkillTarget target)
    {
        return target switch
        {
            CharacterSkillTarget.Self => OffenseBattleTargetRule.Self,
            CharacterSkillTarget.Ally or CharacterSkillTarget.AllAllies => OffenseBattleTargetRule.Ally,
            _ => OffenseBattleTargetRule.Enemy
        };
    }

    private static void RestoreLowestNeed(CharacterActor actor, float amount)
    {
        if (actor?.Stats?.Stats == null)
        {
            return;
        }

        CharacterCondition[] needs =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.SLEEP,
            CharacterCondition.FUN,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE
        };
        CharacterCondition lowest = needs
            .OrderBy(condition => actor.Stats.Stats.TryGetValue(condition, out float value) ? value : 100f)
            .First();
        actor.Stats.ChangesStat(lowest, Mathf.Max(0f, amount));
    }

    private static bool TryEnterExecution(
        CharacterSkillExecutionContext context,
        CharacterSkillInstance skill,
        out string key)
    {
        string eventId = string.IsNullOrWhiteSpace(context.EventId)
            ? $"{context.Trigger}:{context.Actor?.GameClock?.FrameCount ?? 0}"
            : context.EventId;
        key = $"{eventId}:{GetActorExecutionId(context.Actor)}:{skill.id}";
        return CharacterSkillTransientState.Ensure(context.Actor).TryEnter(key);
    }

    private static string GetActorExecutionId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : throw new ArgumentNullException(nameof(actor));
    }
}

public interface ICharacterRuntimeTransientStateRegistry :
    ICharacterCarryInventoryRegistry,
    ICharacterSkillTransientStateRegistry
{
}

public sealed class CharacterSkillExecutionContext
{
    public CharacterSkillExecutionContext(
        CharacterActor actor,
        CharacterSkillTrigger trigger,
        string eventId = null,
        OffenseBattleSession battleSession = null,
        OffenseBattleCombatant sourceCombatant = null,
        OffenseBattleCombatant targetCombatant = null,
        CharacterActor targetActor = null,
        WorkTypeId workTypeId = default,
        BuildableObject facility = null)
    {
        Actor = actor;
        Trigger = trigger;
        EventId = eventId ?? string.Empty;
        BattleSession = battleSession;
        SourceCombatant = sourceCombatant;
        TargetCombatant = targetCombatant;
        TargetActor = targetActor;
        WorkTypeId = workTypeId;
        Facility = facility;
    }

    public CharacterActor Actor { get; }
    public CharacterSkillTrigger Trigger { get; }
    public string EventId { get; }
    public OffenseBattleSession BattleSession { get; }
    public OffenseBattleCombatant SourceCombatant { get; }
    public OffenseBattleCombatant TargetCombatant { get; }
    public CharacterActor TargetActor { get; }
    public WorkTypeId WorkTypeId { get; }
    public BuildableObject Facility { get; }
}

public sealed class CharacterSkillAutomaticTriggerRuntime :
    IStartable,
    IDisposable
{
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IGameEventBus gameEventBus;
    private IDisposable invasionSpawnedSubscription;
    private IDisposable operatingDayStartedSubscription;

    public CharacterSkillAutomaticTriggerRuntime(
        ICharacterWorldQuery characterWorld,
        IGameEventBus gameEventBus)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void Start()
    {
        invasionSpawnedSubscription =
            gameEventBus.Subscribe<InvasionSpawnedEvent>(OnInvasionSpawned);
        operatingDayStartedSubscription =
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
    }

    public void Dispose()
    {
        invasionSpawnedSubscription?.Dispose();
        invasionSpawnedSubscription = null;
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
    }

    private void OnInvasionSpawned(InvasionSpawnedEvent eventType)
    {
        CharacterActor[] actors = CharacterActorCollection.DistinctByGameObject(
                characterWorld.Characters)
            .Where(actor => actor != null && !actor.IsDead)
            .ToArray();
        int serial = actors
            .Select(actor => actor.Progression?.GrowthState?.useLimits?.defenseInvasionSerial ?? -1)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        foreach (CharacterActor actor in actors)
        {
            TriggerAutomaticUltimate(
                actor,
                CharacterUltimateDomain.Defense,
                serial,
                eventType.intruderActor);
            actor.Progression?.RecordNarrative(
                CharacterNarrativeDomain.Invasion,
                "invasion-started",
                eventType.threatSnapshot.stage.ToString(),
                "faced",
                eventType.threatSnapshot.threat);
        }
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
                characterWorld.Characters)
            .Where(actor => actor != null && !actor.IsDead))
        {
            TriggerAutomaticUltimate(actor, CharacterUltimateDomain.Management, eventType.day);
        }
    }

    private void TriggerAutomaticUltimate(
        CharacterActor actor,
        CharacterUltimateDomain domain,
        int serial,
        CharacterActor target = null)
    {
        if (actor?.Progression == null || !actor.Progression.TryMarkUltimateUsed(domain, serial))
        {
            return;
        }

        CharacterSkillInstance ultimate = actor.Progression.Ultimate;
        if (domain == CharacterUltimateDomain.Defense)
        {
            CharacterSkillRuntimeEffects.ApplyDefenseUltimate(actor, ultimate, target);
        }
        else
        {
            CharacterSkillRuntimeEffects.ApplyOutsideCombat(actor, ultimate);
        }
        gameEventBus.RaiseAlert(
            ultimate.displayName,
            $"{actor.Identity?.DisplayName ?? actor.name}: {ultimate.description}",
            EventAlertImportance.High,
            "성장");
    }
}
