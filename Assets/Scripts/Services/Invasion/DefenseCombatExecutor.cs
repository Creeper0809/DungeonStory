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
    void AwardEncounterCompletion(
        DefenseEngagement engagement,
        CharacterActor actor,
        CharacterProficiencyId proficiencyId);
}

public sealed class DefenseCombatSupportServices
{
    public DefenseCombatSupportServices(
        IWorldThreatModifierQuery worldThreatModifiers,
        IExternalCombatInfluenceQuery externalCombatInfluence,
        IWorldUiHierarchy worldUiHierarchy,
        IGameCalendar calendar,
        IMilestoneGameplayModifierQuery milestoneModifiers,
        IRunMilestoneCommand milestoneCommands,
        IFacilityCapabilityQuery facilityCapabilities,
        IWorldItemStackRuntime worldItems,
        ICharacterProficiencyCommand proficiencyCommands)
    {
        WorldThreatModifiers = worldThreatModifiers
            ?? throw new ArgumentNullException(nameof(worldThreatModifiers));
        ExternalCombatInfluence = externalCombatInfluence
            ?? throw new ArgumentNullException(nameof(externalCombatInfluence));
        WorldUiHierarchy = worldUiHierarchy
            ?? throw new ArgumentNullException(nameof(worldUiHierarchy));
        Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        MilestoneModifiers = milestoneModifiers
            ?? throw new ArgumentNullException(nameof(milestoneModifiers));
        MilestoneCommands = milestoneCommands
            ?? throw new ArgumentNullException(nameof(milestoneCommands));
        FacilityCapabilities = facilityCapabilities
            ?? throw new ArgumentNullException(nameof(facilityCapabilities));
        WorldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        ProficiencyCommands = proficiencyCommands
            ?? throw new ArgumentNullException(nameof(proficiencyCommands));
    }

    public IWorldThreatModifierQuery WorldThreatModifiers { get; }
    public IExternalCombatInfluenceQuery ExternalCombatInfluence { get; }
    public IWorldUiHierarchy WorldUiHierarchy { get; }
    public IGameCalendar Calendar { get; }
    public IMilestoneGameplayModifierQuery MilestoneModifiers { get; }
    public IRunMilestoneCommand MilestoneCommands { get; }
    public IFacilityCapabilityQuery FacilityCapabilities { get; }
    public IWorldItemStackRuntime WorldItems { get; }
    public ICharacterProficiencyCommand ProficiencyCommands { get; }
}

public sealed class DefenseCombatExecutor : IDefenseCombatExecutor
{
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipment;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly CombatCoverServices coverServices;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldThreatModifierQuery worldThreatModifiers;
    private readonly IExternalCombatInfluenceQuery externalCombatInfluence;
    private readonly IWorldUiHierarchy worldUiHierarchy;
    private readonly IGameCalendar calendar;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IRunMilestoneCommand milestoneCommands;
    private readonly IFacilityCapabilityQuery facilityCapabilities;
    private readonly ICharacterProficiencyCommand proficiencyCommands;
    private readonly ICharacterPerformanceQuery performance;

    public DefenseCombatExecutor(
        ICombatResolutionService combatResolution,
        ICombatEquipmentRuntime combatEquipment,
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        CombatCoverServices coverServices,
        IWorldItemStackRuntime itemStackRuntime,
        DefenseCombatSupportServices supportServices,
        ICharacterPerformanceQuery performance)
    {
        this.combatResolution = combatResolution
            ?? throw new ArgumentNullException(nameof(combatResolution));
        this.combatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
        this.bodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.bodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        this.coverServices = coverServices
            ?? throw new ArgumentNullException(nameof(coverServices));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        DefenseCombatSupportServices requiredSupport = supportServices
            ?? throw new ArgumentNullException(nameof(supportServices));
        worldThreatModifiers = requiredSupport.WorldThreatModifiers;
        externalCombatInfluence = requiredSupport.ExternalCombatInfluence;
        worldUiHierarchy = requiredSupport.WorldUiHierarchy;
        calendar = requiredSupport.Calendar;
        milestoneModifiers = requiredSupport.MilestoneModifiers;
        milestoneCommands = requiredSupport.MilestoneCommands;
        facilityCapabilities = requiredSupport.FacilityCapabilities;
        proficiencyCommands = requiredSupport.ProficiencyCommands;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
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
            CreateCombatStats(actor, bodyHealthQuery.GetSnapshot(actor)),
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
            CreateCombatStats(actor, bodyHealthQuery.GetSnapshot(actor)),
            weapon,
            CombatFireMode.Aimed);
        return Mathf.Clamp(
            interval / Mathf.Max(
                0.1f,
                attackSpeedMultiplier
                * (externalCombatInfluence?.GetAttackSpeedMultiplier(
                    GetPersistentId(actor)) ?? 1f)),
            0.25f,
            4f);
    }

    public float GetAttackInterval(
        CharacterActor actor,
        CombatWeaponSnapshot weapon,
        CombatFireMode mode)
    {
        float interval = combatResolution.CalculateAttackInterval(
            CreateCombatStats(actor, bodyHealthQuery.GetSnapshot(actor)),
            weapon,
            mode);
        float multiplier =
            externalCombatInfluence?.GetAttackSpeedMultiplier(
                GetPersistentId(actor)) ?? 1f;
        return interval / Mathf.Max(0.1f, multiplier);
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
        CharacterBodyHealthSnapshot attackerBody = bodyHealthQuery.GetSnapshot(attacker);
        CharacterBodyHealthSnapshot defenderBody = bodyHealthQuery.GetSnapshot(defender);
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
                attacker.GetCombatPowerMultiplier()
                * attackMultiplier
                * ResolveAccordSupportMultiplier(attackerIsGuard),
            defenderArmor: combatEquipment.GetArmor(defenderId),
            defenderShield: combatEquipment.GetShield(defenderId),
            defenderConstruct: IsConstruct(defender)));
        if (!result.Executed)
        {
            return new DefenseCombatExecutionResult(
                false,
                defender.IsDead,
                result.FailureReason);
        }

        PresentAttack(attacker, defender, weapon);
        ConsumeAttackResource(weapon, result, defender.GetNowXY());
        ApplyResult(
            attacker,
            defender,
            weapon,
            result,
            "던전 방어 교전");
        if (attackerIsGuard)
        {
            AwardCombatExperience(
                attacker,
                BuiltInCharacterProficiencyIds.MeleeCombat,
                result,
                engagement,
                "melee-attack",
                defensiveBlock: false);
        }
        else if (result.ShieldBlocked)
        {
            AwardCombatExperience(
                defender,
                BuiltInCharacterProficiencyIds.MeleeCombat,
                result,
                engagement,
                "melee-defense",
                defensiveBlock: true);
        }
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
        CharacterBodyHealthSnapshot attackerBody = bodyHealthQuery.GetSnapshot(attacker);
        CharacterBodyHealthSnapshot defenderBody = bodyHealthQuery.GetSnapshot(defender);
        CombatAttackResult result = combatResolution.Resolve(new CombatAttackRequest(
            engagement.Id + ":ranged:" + (engagement.ExchangeCount + 1),
            attackerId,
            defenderId,
            CreateCombatStats(attacker, attackerBody),
            CreateCombatStats(defender, defenderBody),
            weapon,
            distance,
            mode,
            coverServices.Query.GetCover(
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
            lightMultiplier:
                (worldThreatModifiers?.GetMultiplier(
                    OffenseThreatModifierKind.Lighting) ?? 1f)
                * (worldThreatModifiers?.GetMultiplier(
                    OffenseThreatModifierKind.Accuracy) ?? 1f),
            attackPowerMultiplier: attacker.GetCombatPowerMultiplier()
                * ResolveAccordSupportMultiplier(attackerIsGuard: true),
            defenderArmor: combatEquipment.GetArmor(defenderId),
            defenderShield: combatEquipment.GetShield(defenderId),
            defenderConstruct: IsConstruct(defender)));
        if (!result.Executed)
        {
            return new DefenseCombatExecutionResult(
                false,
                defender.IsDead,
                result.FailureReason);
        }

        PresentProjectile(attacker, defender, weapon);
        PresentAttack(attacker, defender, weapon);
        ConsumeAttackResource(weapon, result, defender.GetNowXY());

        string status;
        if (result.CoverBlocked)
        {
            coverServices.Durability.TryApplyDamage(
                result.CoverSourceId,
                result.CoverDamage);
            CombatImpactPresentation.Play(
                defender.transform.position,
                weapon.Verb?.damageType ?? CombatDamageType.Pierce,
                defender.GameClock ?? attacker.GameClock,
                worldUiHierarchy,
                coverHit: true);
            bodyHealthCommands.AddSuppression(defender, result.Suppression);
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

        AwardCombatExperience(
            attacker,
            BuiltInCharacterProficiencyIds.RangedCombat,
            result,
            engagement,
            "ranged-attack",
            defensiveBlock: false);
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

    public void AwardEncounterCompletion(
        DefenseEngagement engagement,
        CharacterActor actor,
        CharacterProficiencyId proficiencyId)
    {
        if (engagement == null
            || actor == null
            || actor.IsDead
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            return;
        }

        proficiencyCommands.AddCombatExperience(
            characterId,
            proficiencyId,
            0.50f,
            training: false,
            stableAwardKey:
                $"{engagement.Id}:complete:{characterId.Value}:{proficiencyId.Value}",
            absoluteHour: calendar.AbsoluteHour);
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

    private void AwardCombatExperience(
        CharacterActor actor,
        CharacterProficiencyId proficiencyId,
        CombatAttackResult result,
        DefenseEngagement engagement,
        string eventKind,
        bool defensiveBlock)
    {
        if (actor == null
            || engagement == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            return;
        }

        float experience;
        if (defensiveBlock)
        {
            experience = 0.25f;
        }
        else
        {
            if (!result.Hit || result.AppliedDamage <= 0f)
            {
                return;
            }
            experience = 0.20f
                + 0.15f
                + Math.Min(0.35f, result.AppliedDamage * 0.01f);
        }

        proficiencyCommands.AddCombatExperience(
            characterId,
            proficiencyId,
            experience,
            training: false,
            stableAwardKey:
                $"{engagement.Id}:{engagement.ExchangeCount + 1}:{eventKind}:{characterId.Value}",
            absoluteHour: calendar.AbsoluteHour);
    }

    private void ApplyResult(
        CharacterActor attacker,
        CharacterActor defender,
        CombatWeaponSnapshot weapon,
        CombatAttackResult result,
        string source)
    {
        if ((result.SpecialEffects & CombatSpecialEffectFlags.SignalSupport) != 0)
        {
            bodyHealthCommands.ReduceSuppression(
                attacker,
                result.StatusPotency * 100f);
        }
        if (result.Hit)
        {
            CombatDamageType damageType =
                weapon?.Verb?.damageType ?? CombatDamageType.Slash;
            CombatAttackResult appliedResult = damageType == CombatDamageType.Blunt
                ? result.WithAppliedDamageMultiplier(
                    defender.GetDetailedStatMultiplier("damage:blunt-taken"))
                : result;
            bodyHealthCommands.ApplyCombatResult(
                defender,
                appliedResult,
                $"{source}: {attacker.Identity?.DisplayName ?? attacker.name}");
            DefenseCombatPresentation.Ensure(defender)?.PlayHit(
                appliedResult.AppliedDamage,
                damageType,
                worldUiHierarchy);
            ApplyArmorDurabilityDamage(result);
            return;
        }

        bodyHealthCommands.AddSuppression(defender, result.Suppression);
    }

    private static bool IsConstruct(CharacterActor actor)
    {
        string species = actor?.SpeciesTag ?? string.Empty;
        return species.IndexOf("golem", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("construct", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("clockwork", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private float ResolveAccordSupportMultiplier(bool attackerIsGuard)
    {
        if (!attackerIsGuard
            || !milestoneModifiers.IsAccordSignalSupportDay(calendar.Day))
        {
            return 1f;
        }

        if (milestoneModifiers.IsAccordSignalSupportActive(calendar.Day))
        {
            return 1.15f;
        }

        BuildableObject signalPost = facilityCapabilities
            .FindOperational(FacilityCapabilityKind.Security)
            .FirstOrDefault();
        if (signalPost == null)
        {
            return 1f;
        }

        const string kitId = "supply:alliance-signal-kit";
        string destinationId = signalPost.PersistentInstanceId.Value;
        bool consumed = itemStackRuntime.TryConsumeFacilityItemBuffer(
            destinationId,
            new Dictionary<string, int> { [kitId] = 1 },
            out _);
        if (!consumed)
        {
            if (!itemStackRuntime.GetAllStacks().Any(stack => stack != null
                    && string.Equals(stack.ItemId, kitId, StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)))
            {
                itemStackRuntime.TryRequestItemDelivery(
                    kitId,
                    1,
                    signalPost.centerPos,
                    destinationId,
                    out _,
                    out _);
            }
            return 1f;
        }

        return milestoneCommands.TryActivateAccordSignalSupport(calendar.Day)
            ? 1.15f
            : 1f;
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

    private void PresentProjectile(
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
            attacker.GameClock,
            worldUiHierarchy);
    }

    private void ConsumeAttackResource(
        CombatWeaponSnapshot weapon,
        CombatAttackResult result,
        Vector2Int impactPosition)
    {
        if (weapon == null)
        {
            return;
        }

        if (weapon.RequiresAmmo && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            combatEquipment.TryConsumeLoadedAmmo(
                weapon.InstanceId,
                Mathf.Max(1, result.AmmunitionConsumed));
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
                PhysicalItemIds.ForEquipment(weapon.DefinitionId),
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

    private CombatStatSnapshot CreateCombatStats(
        CharacterActor actor,
        CharacterBodyHealthSnapshot body)
    {
        return CombatRuntimeStatFactory.Create(actor, body, performance);
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
