using System;
using System.Linq;
using DungeonStory.Foundation;

public sealed class CombatCommandResultApplier
{
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICharacterBodyHealthCommand bodyHealth;
    private readonly ICombatCoverDurabilityRegistry coverDurability;
    private readonly IGameClock gameClock;
    private readonly IWorldUiHierarchy worldUiHierarchy;
    private readonly IGameEventBus gameEvents;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IGameCalendar calendar;
    private readonly CharacterIdentityEventPublisher identityEvents;

    public CombatCommandResultApplier(
        ICombatEquipmentRuntime equipment,
        ICharacterBodyHealthCommand bodyHealth,
        ICombatCoverDurabilityRegistry coverDurability,
        IGameClock gameClock,
        IWorldUiHierarchy worldUiHierarchy,
        IGameEventBus gameEvents,
        ICharacterWorldQuery characterWorld,
        IGameCalendar calendar,
        CharacterIdentityEventPublisher identityEvents = null)
    {
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.coverDurability = coverDurability
            ?? throw new ArgumentNullException(nameof(coverDurability));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.worldUiHierarchy = worldUiHierarchy
            ?? throw new ArgumentNullException(nameof(worldUiHierarchy));
        this.gameEvents = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
        this.characterWorld = characterWorld ?? throw new ArgumentNullException(nameof(characterWorld));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.identityEvents = identityEvents;
    }

    public void Apply(
        CombatParticipantRef target,
        CombatAttackResult result,
        CharacterActor attacker,
        string attackerName,
        CombatDamageType damageType)
    {
        if ((result.SpecialEffects & CombatSpecialEffectFlags.SignalSupport) != 0
            && attacker != null)
        {
            bodyHealth.ReduceSuppression(
                attacker,
                result.StatusPotency * 100f);
        }
        if (result.CoverBlocked)
        {
            coverDurability.TryApplyDamage(result.CoverSourceId, result.CoverDamage);
            CombatImpactPresentation.Play(
                target.IsCharacter
                    ? target.Character.transform.position
                    : target.Wildlife.transform.position,
                damageType,
                gameClock,
                worldUiHierarchy,
                coverHit: true);
            if (target.IsCharacter)
            {
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            }
            return;
        }

        if (target.IsCharacter)
        {
            CombatAttackResult appliedResult = damageType == CombatDamageType.Blunt
                ? result.WithAppliedDamageMultiplier(
                    target.Character.GetDetailedStatMultiplier(
                        "damage:blunt-taken"))
                : result;
            bool wasAlive = !target.Character.IsDead;
            if (appliedResult.Hit)
            {
                bodyHealth.ApplyCombatResult(
                    target.Character,
                    appliedResult,
                    $"직접 전투 명령: {attackerName}");
                if (appliedResult.AppliedDamage > 0f
                    && CharacterPersistentIdentity.TryGet(
                        target.Character,
                        out CharacterId injuredId))
                {
                    CharacterId attackerId = attacker != null
                        && CharacterPersistentIdentity.TryGet(
                            attacker,
                            out CharacterId resolvedAttackerId)
                            ? resolvedAttackerId
                            : default;
                    identityEvents?.Publish(new CharacterInjuredIdentityEvent(
                        injuredId,
                        attackerId,
                        damageType,
                        appliedResult.AppliedDamage,
                        calendar.Day));
                }
                DefenseCombatPresentation.Ensure(target.Character)?.PlayHit(
                    appliedResult.AppliedDamage,
                    damageType,
                    worldUiHierarchy);
                if (wasAlive && target.Character.IsDead)
                {
                    PublishKilledEvent(attacker, target.Character);
                }
            }
            else
            {
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            }
        }
        else if (target.IsWildlife)
        {
            target.Wildlife.ApplyCombatDamage(result, attacker);
        }
    }

    private void PublishKilledEvent(CharacterActor killer, CharacterActor victim)
    {
        if (killer == null || victim == null
            || !CharacterPersistentIdentity.TryGet(killer, out CharacterId killerId)
            || !CharacterPersistentIdentity.TryGet(victim, out CharacterId victimId))
        {
            return;
        }

        const float witnessRadius = 12f;
        float radiusSquared = witnessRadius * witnessRadius;
        CharacterId[] witnesses = characterWorld.Characters
            .Where(actor => actor != null && !actor.IsDead && actor != killer && actor != victim)
            .Where(actor => (actor.transform.position - victim.transform.position).sqrMagnitude <= radiusSquared)
            .Select(actor => CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                ? id
                : default)
            .Where(id => id.IsValid)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();

        gameEvents.Publish(new CharacterKilledEvent(
            killerId,
            victimId,
            witnesses,
            wasHostile: true,
            wasPrisoner: false,
            wasInnocent: false,
            CharacterCommandOrigin.DirectPlayerOrder,
            calendar.Day));
    }

    public void ApplyArmorDurabilityDamage(CombatAttackResult result)
    {
        foreach (CombatArmorDurabilityHit hit in result.ArmorDurabilityHits)
        {
            equipment.TryApplyDurabilityDamage(hit.InstanceId, hit.Damage);
        }
        if (result.ArmorDurabilityHits.Count == 0
            && !string.IsNullOrWhiteSpace(result.ArmorInstanceId))
        {
            equipment.TryApplyDurabilityDamage(
                result.ArmorInstanceId,
                result.ArmorDurabilityDamage);
        }
    }
}
