using System;
using DungeonStory.Foundation;

public sealed class CombatCommandResultApplier
{
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICharacterBodyHealthCommand bodyHealth;
    private readonly ICombatCoverDurabilityRegistry coverDurability;
    private readonly IGameClock gameClock;
    private readonly IWorldUiHierarchy worldUiHierarchy;

    public CombatCommandResultApplier(
        ICombatEquipmentRuntime equipment,
        ICharacterBodyHealthCommand bodyHealth,
        ICombatCoverDurabilityRegistry coverDurability,
        IGameClock gameClock,
        IWorldUiHierarchy worldUiHierarchy)
    {
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.coverDurability = coverDurability
            ?? throw new ArgumentNullException(nameof(coverDurability));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.worldUiHierarchy = worldUiHierarchy
            ?? throw new ArgumentNullException(nameof(worldUiHierarchy));
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
            if (result.Hit)
            {
                bodyHealth.ApplyCombatResult(
                    target.Character,
                    result,
                    $"직접 전투 명령: {attackerName}");
                DefenseCombatPresentation.Ensure(target.Character)?.PlayHit(
                    result.AppliedDamage,
                    damageType,
                    worldUiHierarchy);
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
