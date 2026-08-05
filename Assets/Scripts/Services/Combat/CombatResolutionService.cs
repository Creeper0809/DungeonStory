using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface ICombatRandomSource
{
    float Next01();
}

public sealed class UnityCombatRandomSource : ICombatRandomSource
{
    private readonly IRandomStream randomStream;

    public UnityCombatRandomSource(IRandomStreamProvider randomStreamProvider)
    {
        randomStream = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("combat-resolution");
    }

    public float Next01()
    {
        return randomStream.NextFloat();
    }
}

public interface ICombatResolutionService
{
    CombatAttackResult Resolve(CombatAttackRequest request);
    CombatAttackPreview Preview(CombatAttackRequest request);
    float CalculateAttackInterval(CombatStatSnapshot attacker, CombatWeaponSnapshot weapon, CombatFireMode mode);
    float CalculateReloadTime(CombatStatSnapshot actor, CombatWeaponSnapshot weapon);
    float CalculateWeaponSwitchTime(CombatStatSnapshot actor, float weaponWeight);
}

public sealed class CombatResolutionService : ICombatResolutionService
{
    private readonly ICombatRandomSource random;
    private readonly IEquipmentEvolutionRuntime evolution;
    private readonly IEquipmentOverclockRuntime overclock;
    private readonly ICharacterEnvironmentStatusQuery environmentStatus;
    private readonly IEnvironmentalFieldQuery environmentalField;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterEnvironmentExposureCommand environmentExposure;

    public CombatResolutionService(
        ICombatRandomSource random,
        IEquipmentEvolutionRuntime evolution,
        IEquipmentOverclockRuntime overclock,
        ICharacterEnvironmentStatusQuery environmentStatus,
        IEnvironmentalFieldQuery environmentalField,
        ICharacterWorldQuery characters,
        ICharacterEnvironmentExposureCommand environmentExposure)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        this.evolution = evolution;
        this.overclock = overclock;
        this.environmentStatus = environmentStatus;
        this.environmentalField = environmentalField
            ?? throw new ArgumentNullException(nameof(environmentalField));
        this.characters = characters;
        this.environmentExposure = environmentExposure
            ?? throw new ArgumentNullException(nameof(environmentExposure));
    }

    public CombatAttackResult Resolve(CombatAttackRequest request)
    {
        CombatWeaponSnapshot weapon = request.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        CombatAttackVerb verb = weapon.Verb ?? CombatWeaponSnapshot.CreateUnarmed().Verb;
        if (!string.IsNullOrWhiteSpace(weapon.InstanceId)
            && overclock?.TryRollActionMalfunction(
                OverclockTargetKind.Equipment,
                weapon.InstanceId) == true)
        {
            return Record(request, Failure("오버클럭 오작동"));
        }

        CombatRangeBand band = CombatRangeRules.GetBand(request.Distance);
        bool isRanged = weapon.IsRanged;
        if (request.Distance > weapon.MaximumRange
            || band == CombatRangeBand.OutOfRange
            || (!isRanged && request.Distance > 1))
        {
            return Record(request, Failure("사거리 밖"));
        }

        if (isRanged && !request.HasLineOfSight)
        {
            return Record(request, Failure("사선 차단"));
        }

        if (isRanged && request.FriendlyFireRisk && !request.ForceFire)
        {
            return Record(request, Failure("아군 사격 위험"));
        }

        if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            return Record(request, Failure("탄약 없음"));
        }

        if (weapon.GunpowderWeapon
            && weapon.MisfireChance > 0f
            && random.Next01() < weapon.MisfireChance)
        {
            return Record(request, new CombatAttackResult(
                true,
                false,
                false,
                false,
                CombatBodyPart.Torso,
                0f,
                0f,
                0f,
                0f,
                0f,
                string.Empty,
                "오발"));
        }

        float rangeAccuracy = weapon.GetAccuracyMultiplier(band);
        float rangeDamage = weapon.GetDamageMultiplier(band);
        if (rangeAccuracy <= 0f || rangeDamage <= 0f)
        {
            return Record(request, Failure("사용할 수 없는 거리"));
        }

        float hitChance = isRanged
            ? CalculateRangedHitChance(request, rangeAccuracy)
            : CalculateMeleeHitChance(request, rangeAccuracy);
        hitChance = ApplyEnvironmentAccuracyPenalty(
            request.AttackerId,
            hitChance,
            isRanged);
        if (random.Next01() > hitChance)
        {
            return Record(request, new CombatAttackResult(
                true, false, false, false, CombatBodyPart.Torso,
                0f, 0f, 0f, GetSuppressionOnMiss(request, verb), 0f, string.Empty, string.Empty));
        }

        if (isRanged && request.Cover.Height != CombatCoverHeight.None)
        {
            float blockChance = request.Cover.BaseBlockChance
                * request.Cover.GetDirectionalMultiplier();
            if (request.Cover.Height == CombatCoverHeight.High)
            {
                blockChance = Mathf.Max(blockChance, 0.95f);
            }

            if (random.Next01() < Mathf.Clamp01(blockChance))
            {
                return Record(request, new CombatAttackResult(
                    true, false, true, false, CombatBodyPart.Torso,
                    verb.baseDamage,
                    0f,
                    0f,
                    GetSuppressionOnMiss(request, verb),
                    0f,
                    string.Empty,
                    string.Empty,
                    coverSourceId: request.Cover.SourceId,
                    coverDamage: Mathf.Max(0.5f, verb.baseDamage * 0.18f)));
            }
        }

        if (request.DefenderShield.IsValid
            && random.Next01() < Mathf.Clamp01(request.DefenderShield.GetBlockChance()))
        {
            return Record(request, new CombatAttackResult(
                true,
                false,
                false,
                false,
                CombatBodyPart.Torso,
                verb.baseDamage,
                0f,
                0f,
                GetSuppressionOnMiss(request, verb),
                Mathf.Max(0.5f, verb.baseDamage * 0.1f),
                request.DefenderShield.InstanceId,
                string.Empty,
                shieldBlocked: true));
        }

        float evasionChance = CalculateEvasionChance(request, verb);
        if (random.Next01() < evasionChance)
        {
            return Record(request, new CombatAttackResult(
                true, false, false, true, CombatBodyPart.Torso,
                verb.baseDamage, 0f, 0f, GetSuppressionOnMiss(request, verb), 0f, string.Empty, string.Empty));
        }

        CombatBodyPart bodyPart = RollBodyPart();
        float quality = CombatQualityRules.GetMultiplier(weapon.Quality);
        float rawDamage = CalculateRawDamage(
            request,
            weapon,
            verb,
            rangeDamage,
            quality);
        float toughnessReduction = Mathf.Clamp(request.Defender.Toughness * 0.0125f, 0f, 0.2f);
        float postToughnessDamage = rawDamage * (1f - toughnessReduction);
        ResolveArmor(
            request,
            bodyPart,
            verb,
            postToughnessDamage,
            out float appliedDamage,
            out float durabilityDamage,
            out string armorInstanceId,
            out IReadOnlyList<CombatArmorDurabilityHit> armorDurabilityHits);
        float bleeding = verb.damageType == CombatDamageType.Blunt
            ? appliedDamage * 0.02f
            : appliedDamage * 0.12f;
        float suppression = GetSuppressionOnHit(request, verb);

        return Record(request, new CombatAttackResult(
            true,
            true,
            false,
            false,
            bodyPart,
            rawDamage,
            appliedDamage,
            bleeding,
            suppression,
            durabilityDamage,
            armorInstanceId,
            string.Empty,
            armorDurabilityHits: armorDurabilityHits));
    }

    private CombatAttackResult Record(
        CombatAttackRequest request,
        CombatAttackResult result)
    {
        CombatWeaponSnapshot resolvedWeapon = request.Weapon;
        if (result.Executed
            && resolvedWeapon?.GunpowderWeapon == true
            && resolvedWeapon.SmokeExposure > 0f)
        {
            bool misfire = !result.Hit
                && !result.CoverBlocked
                && !result.ShieldBlocked
                && !result.Evaded
                && result.RawDamage <= 0f
                && result.AppliedDamage <= 0f
                && !string.IsNullOrWhiteSpace(result.FailureReason);
            result = result.WithSmokeExposure(
                resolvedWeapon.SmokeExposure,
                clearSuppression: misfire);
        }
        if (result.SmokeExposure > 0f)
        {
            environmentExposure.AddAirborneExposure(
                new CharacterId(request.AttackerId),
                result.SmokeExposure);
        }

        if (evolution == null)
        {
            return result;
        }

        string weaponId = request.Weapon?.InstanceId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(weaponId))
        {
            string eventId = result.Hit
                ? "combat:hit"
                : result.Executed
                    ? "combat:attack"
                    : "combat:failed-attack";
            evolution.TryRecordUsage(
                weaponId,
                eventId,
                result.Hit ? 2f : 0.5f,
                result.AppliedDamage,
                request.AttackerId,
                1,
                new[]
                {
                    request.Weapon.IsRanged ? "ranged" : "melee",
                    CombatRangeRules.GetBand(request.Distance).ToString()
                });
        }

        if (result.ShieldBlocked
            && !string.IsNullOrWhiteSpace(
                request.DefenderShield.InstanceId))
        {
            evolution.TryRecordUsage(
                request.DefenderShield.InstanceId,
                "combat:block",
                2f,
                result.RawDamage,
                request.DefenderId,
                1,
                new[] { "shield", "defense" });
        }

        foreach (CombatArmorDurabilityHit hit in result.ArmorDurabilityHits
                     ?? Array.Empty<CombatArmorDurabilityHit>())
        {
            evolution.TryRecordUsage(
                hit.InstanceId,
                "combat:absorb",
                1f,
                hit.Damage,
                request.DefenderId,
                1,
                new[] { "armor", "defense" });
        }

        return result;
    }

    public CombatAttackPreview Preview(CombatAttackRequest request)
    {
        CombatWeaponSnapshot weapon = request.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        CombatAttackVerb verb = weapon.Verb ?? CombatWeaponSnapshot.CreateUnarmed().Verb;
        CombatRangeBand band = CombatRangeRules.GetBand(request.Distance);
        bool isRanged = weapon.IsRanged;
        string failureReason = string.Empty;
        if (request.Distance > weapon.MaximumRange
            || band == CombatRangeBand.OutOfRange
            || (!isRanged && request.Distance > 1))
        {
            failureReason = "사거리 밖";
        }
        else if (isRanged && !request.HasLineOfSight)
        {
            failureReason = "사선 차단";
        }
        else if (isRanged && request.FriendlyFireRisk && !request.ForceFire)
        {
            failureReason = "아군 사격 위험";
        }
        else if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            failureReason = "탄약 없음";
        }

        float rangeAccuracy = weapon.GetAccuracyMultiplier(band);
        float rangeDamage = weapon.GetDamageMultiplier(band);
        if (string.IsNullOrEmpty(failureReason)
            && (rangeAccuracy <= 0f || rangeDamage <= 0f))
        {
            failureReason = "사용할 수 없는 거리";
        }

        if (!string.IsNullOrEmpty(failureReason))
        {
            return new CombatAttackPreview(
                false,
                failureReason,
                band,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f);
        }

        float hitChance = isRanged
            ? CalculateRangedHitChance(request, rangeAccuracy)
            : CalculateMeleeHitChance(request, rangeAccuracy);
        hitChance = ApplyEnvironmentAccuracyPenalty(
            request.AttackerId,
            hitChance,
            isRanged);
        float coverChance = isRanged && request.Cover.Height != CombatCoverHeight.None
            ? request.Cover.BaseBlockChance * request.Cover.GetDirectionalMultiplier()
            : 0f;
        if (request.Cover.Height == CombatCoverHeight.High)
        {
            coverChance = Mathf.Max(coverChance, 0.95f);
        }

        coverChance = Mathf.Clamp01(coverChance);
        float shieldChance = request.DefenderShield.IsValid
            ? Mathf.Clamp01(request.DefenderShield.GetBlockChance())
            : 0f;
        float evasionChance = CalculateEvasionChance(request, verb);
        float quality = CombatQualityRules.GetMultiplier(weapon.Quality);
        float rawDamage = CalculateRawDamage(
            request,
            weapon,
            verb,
            rangeDamage,
            quality);
        float toughnessReduction = Mathf.Clamp(
            request.Defender.Toughness * 0.0125f,
            0f,
            0.2f);
        ResolveArmor(
            request,
            CombatBodyPart.Torso,
            verb,
            rawDamage * (1f - toughnessReduction),
            out float damageOnHit,
            out _,
            out _,
            out _);
        float expectedDamage = damageOnHit
            * hitChance
            * (1f - coverChance)
            * (1f - shieldChance)
            * (1f - evasionChance);
        return new CombatAttackPreview(
            true,
            string.Empty,
            band,
            hitChance,
            coverChance,
            shieldChance,
            evasionChance,
            damageOnHit,
            expectedDamage);
    }

    public float CalculateAttackInterval(
        CombatStatSnapshot attacker,
        CombatWeaponSnapshot weapon,
        CombatFireMode mode)
    {
        CombatAttackVerb verb = weapon?.Verb ?? CombatWeaponSnapshot.CreateUnarmed().Verb;
        float dexterityFactor = Mathf.Clamp(1f - attacker.Dexterity * 0.025f, 0.55f, 1f);
        float modeFactor = mode switch
        {
            CombatFireMode.Aimed => 1.5f,
            CombatFireMode.Rapid => 0.65f,
            _ => 1f
        };
        return Mathf.Clamp(verb.attackTime * dexterityFactor * modeFactor, 0.3f, 4f);
    }

    public float CalculateReloadTime(CombatStatSnapshot actor, CombatWeaponSnapshot weapon)
    {
        if (weapon == null)
        {
            return 0f;
        }

        float dexterityFactor = Mathf.Clamp(1.2f - actor.Dexterity * 0.035f, 0.55f, 1.2f);
        return Mathf.Max(0.15f, weapon.ReloadSeconds * dexterityFactor);
    }

    public float CalculateWeaponSwitchTime(CombatStatSnapshot actor, float weaponWeight)
    {
        float dexterityFactor = Mathf.Clamp(1.1f - actor.Dexterity * 0.03f, 0.55f, 1.1f);
        return Mathf.Clamp((0.45f + Mathf.Max(0f, weaponWeight) * 0.08f) * dexterityFactor, 0.2f, 2f);
    }

    private static float CalculateRangedHitChance(CombatAttackRequest request, float rangeAccuracy)
    {
        float mode = request.FireMode switch
        {
            CombatFireMode.Aimed => 1.25f,
            CombatFireMode.Rapid => 0.75f,
            CombatFireMode.Suppressive => 0.55f,
            _ => 1f
        };
        float health = Mathf.Clamp(request.Attacker.HealthMultiplier, 0.25f, 1f);
        float suppression = Mathf.Lerp(1f, 0.55f, request.AttackerSuppression / 100f);
        float chance = (0.45f
            + request.Attacker.Shooting * 0.025f
            + request.Attacker.Dexterity * 0.01f)
            * rangeAccuracy
            * mode
            * health
            * request.LightMultiplier
            * request.WeatherMultiplier
            * suppression;
        return Mathf.Clamp(chance, 0.05f, 0.95f);
    }

    private float ApplyEnvironmentAccuracyPenalty(
        string attackerId,
        float hitChance,
        bool isRanged)
    {
        float penaltyPoints =
            environmentStatus?.GetAccuracyPenaltyPoints(
                new CharacterId(attackerId)) ?? 0f;
        if (isRanged
            && characters != null)
        {
            CharacterActor attacker = characters.Characters
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.Identity?.PersistentId,
                        attackerId,
                        StringComparison.Ordinal));
            if (attacker != null
                && environmentalField.TryGetCell(
                    attacker.GetNowXY(),
                    out EnvironmentalCellSnapshot environment))
            {
                penaltyPoints += environment.LightLevel switch
                {
                    < 20f => 40f,
                    < 40f => 25f,
                    < 50f => 10f,
                    _ => 0f
                };
            }
        }

        return Mathf.Clamp(
            hitChance - penaltyPoints / 100f,
            0.05f,
            0.95f);
    }

    private static float CalculateMeleeHitChance(CombatAttackRequest request, float rangeAccuracy)
    {
        float difference = (request.Attacker.Melee + request.Attacker.Dexterity)
            - (request.Defender.Evasion + request.Defender.Dexterity);
        return Mathf.Clamp((0.72f + difference * 0.018f) * rangeAccuracy, 0.1f, 0.95f);
    }

    private static float CalculateEvasionChance(CombatAttackRequest request, CombatAttackVerb verb)
    {
        if (request.DefenderDowned
            || request.DefenderMeleeLocked
            || request.DefenderSuppression >= 75f)
        {
            return 0f;
        }

        float suppressionPenalty = request.DefenderSuppression >= 40f ? 0.08f : 0f;
        return Mathf.Clamp(
            0.02f
            + request.Defender.Evasion * 0.01f
            + request.Defender.MoveSpeed * 0.003f
            - Mathf.Max(0f, verb.tracking)
            - suppressionPenalty,
            0f,
            0.35f);
    }

    private float CalculateRawDamage(
        CombatAttackRequest request,
        CombatWeaponSnapshot weapon,
        CombatAttackVerb verb,
        float rangeDamage,
        float quality)
    {
        float statDamage = weapon.IsRanged
            ? request.Attacker.Shooting * 0.45f + request.Attacker.Dexterity * 0.15f
            : request.Attacker.Melee * 0.75f + request.Attacker.Strength * 0.45f;
        float overclockMultiplier = string.IsNullOrWhiteSpace(
                weapon.InstanceId)
            ? 1f
            : overclock?.GetPerformanceMultiplier(
                OverclockTargetKind.Equipment,
                weapon.InstanceId) ?? 1f;
        return Mathf.Max(1f, (verb.baseDamage + statDamage)
            * rangeDamage
            * quality
            * weapon.MaterialDamageMultiplier
            * overclockMultiplier
            * Mathf.Max(0.01f, request.Attacker.HealthMultiplier)
            * Mathf.Max(0.01f, request.AttackPowerMultiplier));
    }

    private static float GetSuppressionOnMiss(CombatAttackRequest request, CombatAttackVerb verb)
    {
        return request.FireMode == CombatFireMode.Suppressive
            ? Mathf.Max(8f, verb.baseDamage * 0.8f)
            : Mathf.Max(0f, verb.baseDamage * 0.08f);
    }

    private static float GetSuppressionOnHit(CombatAttackRequest request, CombatAttackVerb verb)
    {
        float multiplier = request.FireMode == CombatFireMode.Suppressive ? 1.5f : 0.5f;
        return Mathf.Max(2f, verb.baseDamage * multiplier);
    }

    private CombatBodyPart RollBodyPart()
    {
        float roll = random.Next01();
        if (roll < 0.12f) return CombatBodyPart.Head;
        if (roll < 0.52f) return CombatBodyPart.Torso;
        if (roll < 0.64f) return CombatBodyPart.LeftArm;
        if (roll < 0.76f) return CombatBodyPart.RightArm;
        if (roll < 0.88f) return CombatBodyPart.LeftLeg;
        return CombatBodyPart.RightLeg;
    }

    private static void ResolveArmor(
        CombatAttackRequest request,
        CombatBodyPart bodyPart,
        CombatAttackVerb verb,
        float incomingDamage,
        out float appliedDamage,
        out float durabilityDamage,
        out string armorInstanceId,
        out IReadOnlyList<CombatArmorDurabilityHit> durabilityHits)
    {
        appliedDamage = Mathf.Max(0f, incomingDamage);
        durabilityDamage = 0f;
        armorInstanceId = string.Empty;
        durabilityHits = Array.Empty<CombatArmorDurabilityHit>();
        if (request.DefenderArmor == null || request.DefenderArmor.Count == 0)
        {
            return;
        }

        float penetration = Mathf.Max(0f, verb.penetration)
            * CombatQualityRules.GetMultiplier(
                request.Weapon?.Quality ?? CombatEquipmentQuality.Normal)
            * (request.Weapon?.MaterialPenetrationMultiplier ?? 1f);
        List<CombatArmorSnapshot> layers = new List<CombatArmorSnapshot>(5);
        for (int i = 0; i < request.DefenderArmor.Count; i++)
        {
            CombatArmorSnapshot armor = request.DefenderArmor[i];
            if (armor.BodyPart == bodyPart && armor.DurabilityRatio > 0f)
            {
                layers.Add(armor);
            }
        }

        if (layers.Count == 0)
        {
            return;
        }

        layers.Sort((left, right) => right.Layer.CompareTo(left.Layer));
        List<CombatArmorDurabilityHit> hits = new List<CombatArmorDurabilityHit>(layers.Count);
        float remainingDamage = incomingDamage;
        float remainingPenetration = penetration;
        for (int i = 0; i < layers.Count && remainingDamage > 0.01f; i++)
        {
            CombatArmorSnapshot armor = layers[i];
            float defense = armor.GetDefense(verb.damageType);
            float layerDurabilityDamage = Mathf.Max(
                0.15f,
                remainingDamage * Mathf.Lerp(0.035f, 0.085f, armor.DurabilityRatio));
            hits.Add(new CombatArmorDurabilityHit(armor.InstanceId, layerDurabilityDamage));

            if (string.IsNullOrEmpty(armorInstanceId))
            {
                armorInstanceId = armor.InstanceId;
                durabilityDamage = layerDurabilityDamage;
            }

            if (verb.damageType == CombatDamageType.Blunt)
            {
                float reduction = Mathf.Clamp(
                    defense / Mathf.Max(1f, remainingPenetration + defense),
                    0f,
                    0.48f);
                remainingDamage *= 1f - reduction;
                remainingPenetration = Mathf.Max(0f, remainingPenetration - defense * 0.35f);
                continue;
            }

            if (remainingPenetration >= defense)
            {
                float partialReduction = Mathf.Clamp(
                    defense / Mathf.Max(1f, remainingPenetration) * 0.22f,
                    0f,
                    0.22f);
                remainingDamage *= 1f - partialReduction;
                remainingPenetration = Mathf.Max(0f, remainingPenetration - defense * 0.8f);
                continue;
            }

            float stoppedRatio = Mathf.Clamp01(
                (defense - remainingPenetration) / Mathf.Max(1f, defense));
            remainingDamage *= Mathf.Lerp(0.52f, 0.16f, stoppedRatio);
            remainingPenetration = 0f;
        }

        durabilityHits = hits;
        appliedDamage = Mathf.Max(0.5f, remainingDamage);
    }

    private static CombatAttackResult Failure(string reason)
    {
        return new CombatAttackResult(
            false, false, false, false, CombatBodyPart.Torso,
            0f, 0f, 0f, 0f, 0f, string.Empty, reason);
    }
}
