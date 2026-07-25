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

    public static float CalculateMemberPower(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        CharacterStats stats = actor != null ? actor.Stats : null;
        if (actor == null || stats == null || stats.IsDead)
        {
            return 0f;
        }

        float basePower =
            stats.GetCharacterStat(CharacterStatType.Attack) * 1.4f
            + stats.GetCharacterStat(CharacterStatType.Strength) * 0.8f
            + stats.GetCharacterStat(CharacterStatType.Toughness) * 0.6f
            + stats.GetCharacterStat(CharacterStatType.Endurance) * 0.4f
            + stats.GetCharacterStat(CharacterStatType.MoveSpeed) * 0.25f;
        return Mathf.Max(0f, basePower * actor.GetCombatPowerMultiplier());
    }

    public static float CalculatePartyPower(IEnumerable<CharacterActor> members)
    {
        return members?.Where((member) => member != null).Sum(CalculateMemberPower) ?? 0f;
    }

}
