using System;
using System.Globalization;
using System.Linq;

public static class CombatActionEvidenceMapping
{
    private const string None = "none";

    public static GameplayOutcomeFact[] Facts(CombatActionEvidenceData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (!data.IsValid)
            throw new ArgumentException(
                "combat-action-evidence-invalid",
                nameof(data));

        return new[]
        {
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.WeaponDefinitionId,
                data.requestKnown ? Normalize(data.weaponDefinitionId) : None),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.WeaponInstanceId,
                data.requestKnown ? Normalize(data.weaponInstanceId) : None),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.DamageType,
                data.damageType.ToString()),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.FireMode,
                data.requestKnown ? data.fireMode.ToString() : None),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.AmmunitionItemId,
                Normalize(data.ammunitionItemId)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.AmmunitionBefore,
                data.ammunitionBefore.ToString(CultureInfo.InvariantCulture)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.AmmunitionConsumed,
                data.ammunitionConsumed.ToString(CultureInfo.InvariantCulture)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.AmmunitionAfter,
                data.ammunitionAfter.ToString(CultureInfo.InvariantCulture)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.ResultFlags,
                FormatResultFlags(data)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.SpecialEffects,
                FormatSpecialEffects(data)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.CoverSourceId,
                Normalize(data.coverSourceId)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.CoverDamage,
                data.coverDamage.ToString("0.###", CultureInfo.InvariantCulture)),
            new GameplayOutcomeFact(
                CombatAttackEvidenceFactIds.ArmorHits,
                FormatArmorHits(data))
        };
    }

    private static string FormatResultFlags(CombatActionEvidenceData data) =>
        $"executed={Flag(data.executed)};hit={Flag(data.hit)};"
        + $"coverBlocked={Flag(data.coverBlocked)};"
        + $"shieldBlocked={Flag(data.shieldBlocked)};"
        + $"evaded={Flag(data.evaded)};nonlethal={Flag(data.nonlethal)}";

    private static string FormatSpecialEffects(CombatActionEvidenceData data) =>
        $"flags={data.specialEffects};"
        + $"potency={data.statusPotency.ToString("0.###", CultureInfo.InvariantCulture)};"
        + $"turns={data.statusTurns.ToString(CultureInfo.InvariantCulture)}";

    private static string FormatArmorHits(CombatActionEvidenceData data)
    {
        if (data.armorHits.Count == 0)
            return None;
        return string.Join(
            ",",
            data.armorHits
                .OrderBy(item => item.instanceId, StringComparer.Ordinal)
                .ThenBy(item => item.damage)
                .Select(item => Normalize(item.instanceId)
                    + "="
                    + item.damage.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)));
    }

    private static string Flag(bool value) => value ? "1" : "0";

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? None : value.Trim();
}
