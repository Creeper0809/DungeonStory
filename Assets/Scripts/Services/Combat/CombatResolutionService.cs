using System;
using System.Collections.Generic;
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

    public CombatResolutionService(ICombatRandomSource random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public CombatAttackResult Resolve(CombatAttackRequest request)
    {
        CombatWeaponSnapshot weapon = request.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        CombatAttackVerb verb = weapon.Verb ?? CombatWeaponSnapshot.CreateUnarmed().Verb;
        CombatRangeBand band = CombatRangeRules.GetBand(request.Distance);
        bool isRanged = weapon.IsRanged;
        if (request.Distance > weapon.MaximumRange
            || band == CombatRangeBand.OutOfRange
            || (!isRanged && request.Distance > 1))
        {
            return Failure("사거리 밖");
        }

        if (isRanged && !request.HasLineOfSight)
        {
            return Failure("사선 차단");
        }

        if (isRanged && request.FriendlyFireRisk && !request.ForceFire)
        {
            return Failure("아군 사격 위험");
        }

        if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            return Failure("탄약 없음");
        }

        float rangeAccuracy = weapon.GetAccuracyMultiplier(band);
        float rangeDamage = weapon.GetDamageMultiplier(band);
        if (rangeAccuracy <= 0f || rangeDamage <= 0f)
        {
            return Failure("사용할 수 없는 거리");
        }

        float hitChance = isRanged
            ? CalculateRangedHitChance(request, rangeAccuracy)
            : CalculateMeleeHitChance(request, rangeAccuracy);
        if (random.Next01() > hitChance)
        {
            return new CombatAttackResult(
                true, false, false, false, CombatBodyPart.Torso,
                0f, 0f, 0f, GetSuppressionOnMiss(request, verb), 0f, string.Empty, string.Empty);
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
                return new CombatAttackResult(
                    true, false, true, false, CombatBodyPart.Torso,
                    verb.baseDamage,
                    0f,
                    0f,
                    GetSuppressionOnMiss(request, verb),
                    0f,
                    string.Empty,
                    string.Empty,
                    coverSourceId: request.Cover.SourceId,
                    coverDamage: Mathf.Max(0.5f, verb.baseDamage * 0.18f));
            }
        }

        if (request.DefenderShield.IsValid
            && random.Next01() < Mathf.Clamp01(request.DefenderShield.GetBlockChance()))
        {
            return new CombatAttackResult(
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
                shieldBlocked: true);
        }

        float evasionChance = CalculateEvasionChance(request, verb);
        if (random.Next01() < evasionChance)
        {
            return new CombatAttackResult(
                true, false, false, true, CombatBodyPart.Torso,
                verb.baseDamage, 0f, 0f, GetSuppressionOnMiss(request, verb), 0f, string.Empty, string.Empty);
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

        return new CombatAttackResult(
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
            armorDurabilityHits: armorDurabilityHits);
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

    private static float CalculateRawDamage(
        CombatAttackRequest request,
        CombatWeaponSnapshot weapon,
        CombatAttackVerb verb,
        float rangeDamage,
        float quality)
    {
        float statDamage = weapon.IsRanged
            ? request.Attacker.Shooting * 0.45f + request.Attacker.Dexterity * 0.15f
            : request.Attacker.Melee * 0.75f + request.Attacker.Strength * 0.45f;
        return Mathf.Max(1f, (verb.baseDamage + statDamage)
            * rangeDamage
            * quality
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
            * CombatQualityRules.GetMultiplier(request.Weapon?.Quality ?? CombatEquipmentQuality.Normal);
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
