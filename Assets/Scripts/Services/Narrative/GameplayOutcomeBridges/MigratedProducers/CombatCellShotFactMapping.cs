using System;
using System.Globalization;

public static class CombatCellShotFactMapping
{
    private const string None = "none";
    private const string ResultFlags =
        "executed=1;hit=0;coverBlocked=0;shieldBlocked=0;evaded=0;nonlethal=0";
    private const string SpecialEffects = "flags=None;potency=0;turns=0";

    public static MigratedDomainFactCapture Capture(CombatCellShotFact fact)
    {
        CombatCellShotFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        CombatCellShotFactData data = value.Snapshot();
        string targetX = data.targetX.ToString(CultureInfo.InvariantCulture);
        string targetY = data.targetY.ToString(CultureInfo.InvariantCulture);
        string attackRevision = data.attackRevision.ToString(
            CultureInfo.InvariantCulture);
        string detail = "target=grid-cell;cell="
            + targetX
            + ","
            + targetY
            + ";manaBefore="
            + data.manaBefore.ToString("R", CultureInfo.InvariantCulture)
            + ";manaCost="
            + data.manaCost.ToString("R", CultureInfo.InvariantCulture)
            + ";manaSpent="
            + data.manaSpent.ToString("R", CultureInfo.InvariantCulture)
            + ";manaAfter="
            + data.manaAfter.ToString("R", CultureInfo.InvariantCulture)
            + ";hit=0;damage=0";

        var actor = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            data.actorId,
            data.actorName,
            MigratedProducerOutcomeIds.ActorRole);
        var target = new MigratedProducerOutcomeSubject(
            new GameplayEntityKindId("grid-cell"),
            "grid-cell:" + targetX + ":" + targetY,
            "지정 칸 (" + targetX + ", " + targetY + ")",
            MigratedProducerOutcomeIds.TargetRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { actor, target },
            Array.Empty<GameplayOutcomeMetric>(),
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.DetailFact,
                    detail),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.WeaponDefinitionId,
                    data.weaponDefinitionId),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.WeaponInstanceId,
                    data.weaponInstanceId),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.DamageType,
                    data.damageType.ToString()),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.FireMode,
                    data.fireMode.ToString()),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.AmmunitionItemId,
                    data.requiresAmmunition ? data.ammunitionItemId : None),
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
                    ResultFlags),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.SpecialEffects,
                    SpecialEffects),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.CoverSourceId,
                    None),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.CoverDamage,
                    "0"),
                new GameplayOutcomeFact(
                    CombatAttackEvidenceFactIds.ArmorHits,
                    None)
            },
            data.actorName
                + "이(가) 지정 칸 ("
                + targetX
                + ", "
                + targetY
                + ")에 사격했다.",
            summaryFirst: true,
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "domain-commit",
                    value.OperationId)
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.CombatAttackResult,
            value.OperationId + ":" + attackRevision,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }
}
