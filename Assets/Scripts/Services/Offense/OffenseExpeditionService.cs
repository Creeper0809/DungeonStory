using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OffenseExpeditionService
{
    public static IReadOnlyList<CharacterActor> GetDistinctMembers(
        IEnumerable<CharacterActor> actors)
    {
        return CharacterActorCollection.DistinctByGameObject(actors);
    }

    public static bool CanJoinExpedition(CharacterActor actor, out string reason)
    {
        if (actor == null)
        {
            reason = "캐릭터 없음";
            return false;
        }

        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        CharacterStats stats = actor.Stats;
        CharacterLifecycle lifecycle = actor.Lifecycle;
        CharacterAbilityCache abilityCache = actor.AbilityCache;

        if (identity != null && identity.IsOwner)
        {
            reason = "사장은 원정에 보낼 수 없습니다";
            return false;
        }

        if (stats != null && stats.IsDead)
        {
            reason = "사망한 캐릭터입니다";
            return false;
        }

        if (lifecycle != null && lifecycle.CurrentState == CharacterLifecycleState.OnExpedition)
        {
            reason = "이미 원정 중입니다";
            return false;
        }

        if (lifecycle == null || lifecycle.CurrentState != CharacterLifecycleState.Active)
        {
            reason = "현재 던전에서 활동 중인 캐릭터가 아닙니다";
            return false;
        }

        if (!lifecycle.CanStartExpedition(out reason))
        {
            return false;
        }

        if (identity == null || identity.CharacterType != CharacterType.NPC)
        {
            reason = "직원이나 방어 몬스터만 원정에 보낼 수 있습니다";
            return false;
        }

        if (abilityCache == null || !abilityCache.TryGetAbility(out AbilityWork _))
        {
            reason = "원정 가능한 작업/전투 능력이 없습니다";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static float CalculateMemberPower(
        CharacterActor actor,
        ICharacterPerformanceQuery performance)
    {
        actor?.EnsureRuntimeState();
        CharacterStats stats = actor != null ? actor.Stats : null;
        if (actor == null || stats == null || stats.IsDead)
        {
            return 0f;
        }

        if (performance == null) throw new ArgumentNullException(nameof(performance));
        return CalculateBasePower(
            5f * performance.Evaluate(actor, "performance:combat:melee-hit").Value,
            5f * performance.Evaluate(actor, "performance:combat:melee-power").Value,
            5f * performance.Evaluate(actor, "performance:combat:defense-reaction").Value,
            5f * performance.Evaluate(actor, CharacterCompositePerformanceIds.SustainedExecution).Value,
            5f * performance.Evaluate(actor, "performance:combat:movement").Value,
            1f);
    }

    public static float CalculatePartyPower(
        IEnumerable<CharacterActor> members,
        ICharacterPerformanceQuery performance)
    {
        return members?.Where((member) => member != null)
            .Sum(member => CalculateMemberPower(member, performance)) ?? 0f;
    }

    public static float CalculateProjectedProficiencyPower(
        Func<CharacterProficiencyId, long> experienceQuery,
        float combatPowerMultiplier = 1f)
    {
        if (experienceQuery == null)
        {
            throw new ArgumentNullException(nameof(experienceQuery));
        }

        CharacterProficiencyEffectSnapshot melee = ProficiencyProgressionRules.ResolveEffects(
            experienceQuery(BuiltInCharacterProficiencyIds.MeleeCombat));
        CharacterProficiencyEffectSnapshot ranged = ProficiencyProgressionRules.ResolveEffects(
            experienceQuery(BuiltInCharacterProficiencyIds.RangedCombat));
        float meleeFactor = Mathf.Max(.1f, melee.QualityScore / 58f);
        float rangedFactor = Mathf.Max(.1f, ranged.QualityScore / 58f);

        return CalculateBasePower(
            5f * Mathf.Max(meleeFactor, rangedFactor),
            5f * meleeFactor,
            5f,
            5f,
            5f,
            combatPowerMultiplier);
    }

    public static float CalculateMemberPower(
        CharacterActor actor,
        ICombatEquipmentRuntime equipment,
        ICharacterPerformanceQuery performance)
    {
        if (equipment == null)
        {
            throw new ArgumentNullException(nameof(equipment));
        }

        float characterPower = CalculateMemberPower(actor, performance);
        string characterId = actor?.Identity?.PersistentId ?? string.Empty;
        if (characterPower <= 0f || string.IsNullOrWhiteSpace(characterId))
        {
            return characterPower;
        }

        equipment.TryGetActiveWeapon(
            characterId,
            out CombatWeaponSnapshot weapon);
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();
        float loadoutPower = OffenseEquipmentPowerRules.CalculateLoadoutContribution(
            characterPower,
            weapon,
            equipment.GetArmor(characterId),
            equipment.GetShield(characterId));
        return characterPower + loadoutPower;
    }

    public static float CalculatePartyPower(
        IEnumerable<CharacterActor> members,
        ICombatEquipmentRuntime equipment,
        ICharacterPerformanceQuery performance)
    {
        if (equipment == null)
        {
            throw new ArgumentNullException(nameof(equipment));
        }

        if (members == null)
        {
            return 0f;
        }

        float totalPower = 0f;
        foreach (CharacterActor member in members)
        {
            if (member != null)
            {
                totalPower += CalculateMemberPower(member, equipment, performance);
            }
        }

        return totalPower;
    }

    private static float CalculateBasePower(
        float attack,
        float strength,
        float toughness,
        float endurance,
        float moveSpeed,
        float combatPowerMultiplier)
    {
        float basePower =
            attack * 1.4f
            + strength * 0.8f
            + toughness * 0.6f
            + endurance * 0.4f
            + moveSpeed * 0.25f;
        return Mathf.Max(
            0f,
            basePower * Mathf.Max(0f, combatPowerMultiplier));
    }

}
