using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct DefenseCombatExecutionResult
{
    public DefenseCombatExecutionResult(
        bool executed,
        bool defenderDefeated,
        string statusText)
    {
        Executed = executed;
        DefenderDefeated = defenderDefeated;
        StatusText = statusText ?? string.Empty;
    }

    public bool Executed { get; }
    public bool DefenderDefeated { get; }
    public string StatusText { get; }
}

public interface IDefenseCombatExecutor
{
    CharacterCombatLoadoutProfile GetActiveProfile(CharacterActor actor);
    bool TryGetActiveRangedWeapon(
        CharacterActor actor,
        out CombatWeaponSnapshot weapon);
    bool HasActiveRangedWeapon(CharacterActor actor);
    bool TrySwitchFallbackWeapon(
        CharacterActor actor,
        out CombatWeaponSnapshot selected);
    bool TryReload(
        CharacterActor actor,
        CombatWeaponSnapshot weapon,
        CharacterCarryInventory inventory,
        out float reloadDuration);
    CombatFireMode ResolveSupportedFireMode(
        CombatWeaponSnapshot weapon,
        CombatFireMode requested);
    float GetAttackInterval(
        CharacterActor actor,
        float attackSpeedMultiplier = 1f);
    float GetAttackInterval(
        CharacterActor actor,
        CombatWeaponSnapshot weapon,
        CombatFireMode mode);
    DefenseCombatExecutionResult ExecuteMelee(
        DefenseEngagement engagement,
        CharacterActor attacker,
        CharacterActor defender,
        float attackMultiplier,
        bool attackerIsGuard);
    DefenseCombatExecutionResult ExecuteRanged(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor attacker,
        CharacterActor defender,
        CombatWeaponSnapshot weapon,
        CombatFireMode mode,
        CombatLineOfSightResult sight,
        int distance);
}

public sealed class DefenseCombatExecutor : IDefenseCombatExecutor
{
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipment;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly ICombatCoverQuery coverQuery;
    private readonly IWorldItemStackRuntime itemStackRuntime;

    public DefenseCombatExecutor(
        ICombatResolutionService combatResolution,
        ICombatEquipmentRuntime combatEquipment,
        ICharacterBodyHealthRuntime bodyHealth,
        ICombatCoverQuery coverQuery,
        IWorldItemStackRuntime itemStackRuntime)
    {
        this.combatResolution = combatResolution
            ?? throw new ArgumentNullException(nameof(combatResolution));
        this.combatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.coverQuery = coverQuery
            ?? throw new ArgumentNullException(nameof(coverQuery));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
    }

    public CharacterCombatLoadoutProfile GetActiveProfile(CharacterActor actor)
    {
        return actor == null
            ? null
            : combatEquipment.GetActiveProfileSnapshot(GetPersistentId(actor));
    }

    public bool TryGetActiveRangedWeapon(
        CharacterActor actor,
        out CombatWeaponSnapshot weapon)
    {
        weapon = null;
        return actor != null
            && combatEquipment.TryGetActiveWeapon(GetPersistentId(actor), out weapon)
            && weapon != null
            && weapon.IsRanged;
    }

    public bool HasActiveRangedWeapon(CharacterActor actor)
    {
        return TryGetActiveRangedWeapon(actor, out _);
    }

    public bool TrySwitchFallbackWeapon(
        CharacterActor actor,
        out CombatWeaponSnapshot selected)
    {
        selected = null;
        if (actor == null)
        {
            return false;
        }

        string actorId = GetPersistentId(actor);
        CharacterCombatLoadoutProfile profile =
            combatEquipment.GetActiveProfileSnapshot(actorId);
        if (profile == null)
        {
            return false;
        }

        string original = profile.activeWeaponInstanceId;
        List<(string id, CombatWeaponSnapshot weapon)> candidates =
            new List<(string id, CombatWeaponSnapshot weapon)>();
        foreach (string instanceId in profile.weaponInstanceIds)
        {
            if (string.Equals(instanceId, original, StringComparison.Ordinal)
                || !combatEquipment.TrySetActiveWeapon(actorId, instanceId, out _)
                || !combatEquipment.TryGetActiveWeapon(
                    actorId,
                    out CombatWeaponSnapshot candidate)
                || candidate == null)
            {
                continue;
            }

            candidates.Add((instanceId, candidate));
        }

        (string id, CombatWeaponSnapshot weapon) choice = candidates
            .OrderBy(candidate =>
                candidate.weapon.IsRanged
                && (!candidate.weapon.RequiresAmmo || candidate.weapon.LoadedAmmo > 0)
                    ? 0
                    : !candidate.weapon.IsRanged ? 1 : 2)
            .FirstOrDefault(candidate =>
                !candidate.weapon.IsRanged
                || !candidate.weapon.RequiresAmmo
                || candidate.weapon.LoadedAmmo > 0);
        if (choice.weapon != null
            && combatEquipment.TrySetActiveWeapon(actorId, choice.id, out _))
        {
            selected = choice.weapon;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(original))
        {
            combatEquipment.TrySetActiveWeapon(actorId, original, out _);
        }

        return false;
    }

    public bool TryReload(
        CharacterActor actor,
        CombatWeaponSnapshot weapon,
        CharacterCarryInventory inventory,
        out float reloadDuration)
    {
        reloadDuration = 0f;
        if (actor == null
            || weapon == null
            || inventory == null
            || !combatEquipment.TryReloadFromInventory(
                weapon.InstanceId,
                inventory,
                out int consumed)
            || consumed <= 0)
        {
            return false;
        }

        reloadDuration = combatResolution.CalculateReloadTime(
            CreateCombatStats(actor, bodyHealth.GetSnapshot(actor)),
            weapon);
        return true;
    }

    public CombatFireMode ResolveSupportedFireMode(
        CombatWeaponSnapshot weapon,
        CombatFireMode requested)
    {
        if (weapon == null)
        {
            return CombatFireMode.Aimed;
        }

        return requested switch
        {
            CombatFireMode.Rapid when weapon.SupportsRapid =>
                CombatFireMode.Rapid,
            CombatFireMode.Suppressive when weapon.SupportsSuppressive =>
                CombatFireMode.Suppressive,
            _ => CombatFireMode.Aimed
        };
    }

    public float GetAttackInterval(
        CharacterActor actor,
        float attackSpeedMultiplier = 1f)
    {
        combatEquipment.TryGetActiveWeapon(
            GetPersistentId(actor),
            out CombatWeaponSnapshot weapon);
        float interval = combatResolution.CalculateAttackInterval(
            CreateCombatStats(actor, bodyHealth.GetSnapshot(actor)),
            weapon,
            CombatFireMode.Aimed);
        return Mathf.Clamp(
            interval / Mathf.Max(0.1f, attackSpeedMultiplier),
            0.25f,
            4f);
    }

    public float GetAttackInterval(
        CharacterActor actor,
        CombatWeaponSnapshot weapon,
        CombatFireMode mode)
    {
        return combatResolution.CalculateAttackInterval(
            CreateCombatStats(actor, bodyHealth.GetSnapshot(actor)),
            weapon,
            mode);
    }

    public DefenseCombatExecutionResult ExecuteMelee(
        DefenseEngagement engagement,
        CharacterActor attacker,
        CharacterActor defender,
        float attackMultiplier,
        bool attackerIsGuard)
    {
        if (!CanExecute(engagement, attacker, defender))
        {
            return default;
        }

        string attackerId = GetPersistentId(attacker);
        string defenderId = GetPersistentId(defender);
        combatEquipment.TryGetActiveWeapon(
            attackerId,
            out CombatWeaponSnapshot weapon);
        CharacterBodyHealthSnapshot attackerBody = bodyHealth.GetSnapshot(attacker);
        CharacterBodyHealthSnapshot defenderBody = bodyHealth.GetSnapshot(defender);
        CombatAttackResult result = combatResolution.Resolve(new CombatAttackRequest(
            engagement.Id + ":exchange:" + (engagement.ExchangeCount + 1),
            attackerId,
            defenderId,
            CreateCombatStats(attacker, attackerBody),
            CreateCombatStats(defender, defenderBody),
            weapon,
            1,
            CombatFireMode.Aimed,
            default,
            defenderDowned: defenderBody.Downed,
            defenderMeleeLocked: true,
            attackerSuppression: attackerBody.Suppression,
            defenderSuppression: defenderBody.Suppression,
            attackPowerMultiplier:
                attacker.GetCombatPowerMultiplier() * attackMultiplier,
            defenderArmor: combatEquipment.GetArmor(defenderId),
            defenderShield: combatEquipment.GetShield(defenderId)));
        if (!result.Executed)
        {
            return new DefenseCombatExecutionResult(
                false,
                defender.IsDead,
                result.FailureReason);
        }

        PresentAttack(attacker, defender, weapon);
        ConsumeAttackResource(weapon, defender.GetNowXY());
        ApplyResult(
            attacker,
            defender,
            weapon,
            result,
            "던전 방어 교전");
        engagement.ExchangeCount++;
        TriggerDamagePassive(
            defender,
            attacker,
            engagement,
            attackerIsGuard ? "intruder-hit" : "guard-hit");
        return new DefenseCombatExecutionResult(
            true,
            defender.IsDead,
            "근접 교전");
    }

    public DefenseCombatExecutionResult ExecuteRanged(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor attacker,
        CharacterActor defender,
        CombatWeaponSnapshot weapon,
        CombatFireMode mode,
        CombatLineOfSightResult sight,
        int distance)
    {
        if (grid == null || weapon == null
            || !CanExecute(engagement, attacker, defender))
        {
            return default;
        }

        string attackerId = GetPersistentId(attacker);
        string defenderId = GetPersistentId(defender);
        CharacterBodyHealthSnapshot attackerBody = bodyHealth.GetSnapshot(attacker);
        CharacterBodyHealthSnapshot defenderBody = bodyHealth.GetSnapshot(defender);
        CombatAttackResult result = combatResolution.Resolve(new CombatAttackRequest(
            engagement.Id + ":ranged:" + (engagement.ExchangeCount + 1),
            attackerId,
            defenderId,
            CreateCombatStats(attacker, attackerBody),
            CreateCombatStats(defender, defenderBody),
            weapon,
            distance,
            mode,
            coverQuery.GetCover(
                grid,
                attacker.GetNowXY(),
                defender.GetNowXY()),
            hasLineOfSight: sight.HasLineOfSight,
            friendlyFireRisk: sight.FriendlyFireRisk,
            defenderDowned: defenderBody.Downed,
            defenderMeleeLocked:
                engagement.State == DefenseEngagementState.Engaged,
            attackerSuppression: attackerBody.Suppression,
            defenderSuppression: defenderBody.Suppression,
            attackPowerMultiplier: attacker.GetCombatPowerMultiplier(),
            defenderArmor: combatEquipment.GetArmor(defenderId),
            defenderShield: combatEquipment.GetShield(defenderId)));
        if (!result.Executed)
        {
            return new DefenseCombatExecutionResult(
                false,
                defender.IsDead,
                result.FailureReason);
        }

        PresentProjectile(attacker, defender, weapon);
        PresentAttack(attacker, defender, weapon);
        ConsumeAttackResource(weapon, defender.GetNowXY());

        string status;
        if (result.CoverBlocked)
        {
            CombatCoverDurability.TryApplyDamage(
                result.CoverSourceId,
                result.CoverDamage);
            CombatImpactPresentation.Play(
                defender.transform.position,
                weapon.Verb?.damageType ?? CombatDamageType.Pierce,
                defender.GameClock ?? attacker.GameClock,
                coverHit: true);
            bodyHealth.AddSuppression(defender, result.Suppression);
            status = "엄폐물에 막힘";
        }
        else
        {
            ApplyResult(
                attacker,
                defender,
                weapon,
                result,
                "원거리 방어 사격");
            status = result.ShieldBlocked ? "방패에 막힘" : "원거리 교전";
        }

        engagement.ExchangeCount++;
        TriggerDamagePassive(
            defender,
            attacker,
            engagement,
            "ranged-hit");
        return new DefenseCombatExecutionResult(
            true,
            defender.IsDead,
            status);
    }

    private static bool CanExecute(
        DefenseEngagement engagement,
        CharacterActor attacker,
        CharacterActor defender)
    {
        return engagement != null
            && attacker != null
            && !attacker.IsDead
            && defender != null
            && !defender.IsDead;
    }

    private void ApplyResult(
        CharacterActor attacker,
        CharacterActor defender,
        CombatWeaponSnapshot weapon,
        CombatAttackResult result,
        string source)
    {
        if (result.Hit)
        {
            bodyHealth.ApplyCombatResult(
                defender,
                result,
                $"{source}: {attacker.Identity?.DisplayName ?? attacker.name}");
            DefenseCombatPresentation.Ensure(defender)?.PlayHit(
                result.AppliedDamage,
                weapon?.Verb?.damageType ?? CombatDamageType.Slash);
            ApplyArmorDurabilityDamage(result);
            return;
        }

        bodyHealth.AddSuppression(defender, result.Suppression);
    }

    private static void PresentAttack(
        CharacterActor attacker,
        CharacterActor defender,
        CombatWeaponSnapshot weapon)
    {
        DefenseCombatPresentation
            .Ensure(attacker)?
            .PlayAttack(defender.transform.position, weapon);
    }

    private static void PresentProjectile(
        CharacterActor attacker,
        CharacterActor defender,
        CombatWeaponSnapshot weapon)
    {
        CombatAttackVerb verb = weapon.Verb;
        float projectileSpeed = verb switch
        {
            ProjectileVerb projectile => projectile.projectileSpeed,
            RecoverableThrowVerb recoverable => recoverable.projectileSpeed,
            _ => 12f
        };
        CombatProjectilePresentation.Launch(
            attacker.transform.position,
            defender.transform.position,
            projectileSpeed,
            verb?.damageType ?? CombatDamageType.Pierce,
            weapon.Kind == CombatEquipmentKind.RecoverableThrowingWeapon,
            attacker.GameClock);
    }

    private void ConsumeAttackResource(
        CombatWeaponSnapshot weapon,
        Vector2Int impactPosition)
    {
        if (weapon == null)
        {
            return;
        }

        if (weapon.RequiresAmmo && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            combatEquipment.TryConsumeLoadedAmmo(weapon.InstanceId);
        }
        else if (weapon.Verb?.DropsWeaponOnUse == true)
        {
            DropRecoverableWeapon(weapon, impactPosition);
        }
    }

    private void DropRecoverableWeapon(
        CombatWeaponSnapshot weapon,
        Vector2Int impactPosition)
    {
        if (weapon == null
            || string.IsNullOrWhiteSpace(weapon.InstanceId)
            || string.IsNullOrWhiteSpace(weapon.DefinitionId)
            || !itemStackRuntime.SpawnUniqueItemAt(
                DungeonItemCatalogSO.EquipmentItemId(weapon.DefinitionId),
                impactPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId))
        {
            return;
        }

        combatEquipment.TryLinkToWorldStack(
            weapon.InstanceId,
            stackId,
            CombatEquipmentWorldState.Loose);
    }

    private void ApplyArmorDurabilityDamage(CombatAttackResult result)
    {
        if (result.ArmorDurabilityHits.Count > 0)
        {
            for (int i = 0; i < result.ArmorDurabilityHits.Count; i++)
            {
                CombatArmorDurabilityHit hit = result.ArmorDurabilityHits[i];
                combatEquipment.TryApplyDurabilityDamage(
                    hit.InstanceId,
                    hit.Damage);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ArmorInstanceId))
        {
            combatEquipment.TryApplyDurabilityDamage(
                result.ArmorInstanceId,
                result.ArmorDurabilityDamage);
        }
    }

    private static CombatStatSnapshot CreateCombatStats(
        CharacterActor actor,
        CharacterBodyHealthSnapshot body)
    {
        if (actor == null)
        {
            return default;
        }

        float health = Mathf.Clamp01(
            actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        float bodyEfficiency = Mathf.Min(
            body.Consciousness,
            Mathf.Lerp(0.5f, 1f, body.Manipulation));
        return new CombatStatSnapshot(
            actor.GetCharacterStat(CharacterStatType.Attack),
            actor.GetCharacterStat(CharacterStatType.Shooting),
            actor.GetCharacterStat(CharacterStatType.Evasion),
            actor.GetCharacterStat(CharacterStatType.MoveSpeed) * body.Mobility,
            actor.GetCharacterStat(CharacterStatType.Strength),
            actor.GetCharacterStat(CharacterStatType.Toughness),
            actor.GetCharacterStat(CharacterStatType.Dexterity)
                * body.Manipulation,
            health * bodyEfficiency);
    }

    private static void TriggerDamagePassive(
        CharacterActor defender,
        CharacterActor attacker,
        DefenseEngagement engagement,
        string suffix)
    {
        if (defender == null || engagement == null)
        {
            return;
        }

        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(
            new CharacterSkillExecutionContext(
                defender,
                CharacterSkillTrigger.DamageTaken,
                $"{engagement.Id}:{suffix}:{engagement.ExchangeCount}",
                targetActor: attacker));
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
    }
}
