using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public interface IArcaneOverchargeCommand
{
    bool TryActivate(
        CharacterActor actor,
        string equipmentInstanceId,
        out ArcaneOverchargeActivation activation,
        out string failureReason);
}

public sealed class ArcaneOverchargeCommandRuntime : IArcaneOverchargeCommand
{
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IGameClock gameClock;
    private readonly ICharacterManaQuery mana;

    public ArcaneOverchargeCommandRuntime(
        ExtremeTraitRuntime extremeTraits,
        ICombatEquipmentRuntime equipment,
        IGameClock gameClock,
        ICharacterManaQuery mana)
    {
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.mana = mana ?? throw new ArgumentNullException(nameof(mana));
    }

    [GameplayEntryPoint(
        "StaffManagementSurfacePanel overcharge button; V26 extreme-trait focused audit")]
    public bool TryActivate(
        CharacterActor actor,
        string equipmentInstanceId,
        out ArcaneOverchargeActivation activation,
        out string failureReason)
    {
        activation = default;
        failureReason = string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(actor.Identity?.PersistentId))
        {
            failureReason = "유효한 시전자가 필요합니다.";
            return false;
        }
        string instanceId = equipmentInstanceId?.Trim() ?? string.Empty;
        if (!IsEquippedBy(actor.Identity.PersistentId, instanceId)
            || !equipment.TryGetDerivedStats(
                instanceId,
                out CombatEquipmentDerivedStats stats)
            || !CharacterArcaneWeaponRules.IsArcane(stats.DefinitionId))
        {
            failureReason = "시전자가 실제 착용한 마력 장비가 필요합니다.";
            return false;
        }
        if (!extremeTraits.TryActivateArcaneOvercharge(
                actor,
                $"arcane-overcharge:{actor.Identity.PersistentId}:"
                    + Math.Max(0, (int)(gameClock.Time * 1000f)),
                mana.GetMana(actor).Ratio,
                gameClock.Time,
                out activation))
        {
            failureReason = "마력이 30% 미만이 아니거나 과충전 후유증이 남아 있습니다.";
            return false;
        }

        actor.ApplyDamage(
            actor.MaxHealth * activation.SelfDamageFraction,
            "trait:arcane-overcharge");
        equipment.TryApplyDurabilityDamage(
            instanceId,
            stats.MaxDurability * activation.EquipmentDurabilityFraction);
        return true;
    }

    private bool IsEquippedBy(string characterId, string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;
        CharacterCombatLoadoutProfile loadout =
            equipment.GetActiveProfileSnapshot(characterId);
        IEnumerable<string> equipped = (loadout?.weaponInstanceIds
                ?? new List<string>())
            .Concat(loadout?.armorInstanceIds ?? new List<string>())
            .Append(loadout?.shieldInstanceId);
        return equipped.Any(value => string.Equals(
            value?.Trim(),
            instanceId,
            StringComparison.Ordinal));
    }
}
