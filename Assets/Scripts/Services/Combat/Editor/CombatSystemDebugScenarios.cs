#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CombatSystemDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Combat/Run V14 Combat Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("V14 combat scenarios failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Combat/Run V18 BodyHealth Strict Save")]
    public static void RunV18BodyHealthFromMenu()
    {
        try
        {
            bool ok = VerifyBodyHealthStrictSave();
            if (!ok)
            {
                Debug.LogError("V18 BodyHealth Strict Save failed.");
            }
            else
            {
                Debug.Log("V18 BodyHealth Strict Save passed.");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [MenuItem("DungeonStory/Debug/Combat/Run V18 Medical Strict Save")]
    public static void RunV18MedicalFromMenu()
    {
        try
        {
            bool ok = VerifyCharacterMedicalStrictSave();
            if (!ok)
            {
                Debug.LogError("V18 Medical Strict Save failed.");
            }
            else
            {
                Debug.Log("V18 Medical Strict Save passed.");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        IReadOnlyList<string> failures = ValidateAll();

        foreach (string failure in failures)
        {
            Debug.LogError($"Combat scenario failed: {failure}");
        }

        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("V14 combat scenarios passed.");
        }

        return failures.Count == 0;
    }

    public static IReadOnlyList<string> ValidateAll()
    {
        List<string> failures = new List<string>();
        Verify("거리 구간", VerifyRangeBands, failures);
        Verify("장비 품질", VerifyQualityMultipliers, failures);
        Verify("사격 판정 순서", VerifyRangedResolutionOrder, failures);
        Verify("공격 예측 계약", VerifyAttackPreview, failures);
        Verify("방패와 방어구", VerifyShieldAndArmor, failures);
        Verify("중간 치명도", VerifyTargetLethality, failures);
        Verify("장비 개체와 탄약 저장", VerifyEquipmentRuntime, failures);
        Verify("탄약 소비 권위", VerifyAmmunitionConsumerAuthority, failures);
        Verify("사망 이벤트 장비 소실", VerifyEquipmentDeathEventBridge, failures);
        Verify("쓰러짐 회복 히스테리시스", VerifyDownedHysteresis, failures);
        Verify("대장작업대 제작 연결", VerifyForgeRecipeBridge, failures);
        Verify("층간 사선", VerifyLineOfSight, failures);
        Verify("건설형 엄폐물", VerifyCoverAssets, failures);
        Verify("12종 초기 스탯", VerifyInitialStats, failures);
        Verify("V14 생활 전투 저장", VerifyV14CombatLifecycleSave, failures);
        Verify("V14 저장 계약", VerifySaveContract, failures);
        Verify("V18 체력 strict 저장", VerifyBodyHealthStrictSave, failures);
        Verify("V18 의료 strict 저장", VerifyCharacterMedicalStrictSave, failures);
        Verify(
            "V18 의료 게시 단계별 오류 롤백",
            CharacterMedicalRestoreFaultScenarios.Run,
            failures);

        Verify("부위 피해가 총체력 즉사를 선행하지 않음", VerifyBodyDamageOwnsDeath, failures);

        Verify("gunpowder smoke misfire and ranged roles",
            VerifyGunpowderSmokeMisfireAndRangedRoles,
            failures);

        return failures;
    }

    private static void Verify(string name, Func<bool> scenario, ICollection<string> failures)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        failures.Add(name);
    }

    private static bool VerifyRangeBands()
    {
        return CombatRangeRules.GetBand(1) == CombatRangeBand.Contact
            && CombatRangeRules.GetBand(2) == CombatRangeBand.Near
            && CombatRangeRules.GetBand(5) == CombatRangeBand.Near
            && CombatRangeRules.GetBand(6) == CombatRangeBand.Medium
            && CombatRangeRules.GetBand(11) == CombatRangeBand.Medium
            && CombatRangeRules.GetBand(12) == CombatRangeBand.Long
            && CombatRangeRules.GetBand(18) == CombatRangeBand.Long
            && CombatRangeRules.GetBand(19) == CombatRangeBand.OutOfRange;
    }

    private static bool VerifyQualityMultipliers()
    {
        return Mathf.Approximately(CombatQualityRules.GetMultiplier(CombatEquipmentQuality.Awful), 0.8f)
            && Mathf.Approximately(CombatQualityRules.GetMultiplier(CombatEquipmentQuality.Poor), 0.9f)
            && Mathf.Approximately(CombatQualityRules.GetMultiplier(CombatEquipmentQuality.Normal), 1f)
            && Mathf.Approximately(CombatQualityRules.GetMultiplier(CombatEquipmentQuality.Good), 1.1f)
            && Mathf.Approximately(CombatQualityRules.GetMultiplier(CombatEquipmentQuality.Excellent), 1.2f)
            && Mathf.Approximately(CombatQualityRules.GetMultiplier(CombatEquipmentQuality.Masterwork), 1.32f)
            && Mathf.Approximately(CombatQualityRules.GetMultiplier(CombatEquipmentQuality.Legendary), 1.48f);
    }

    private static bool VerifyRangedResolutionOrder()
    {
        CombatWeaponSnapshot bow = CreateRangedWeapon(loadedAmmo: 1);
        CombatStatSnapshot attacker = new CombatStatSnapshot(5f, 10f, 4f, 5f, 5f, 5f, 8f);
        CombatStatSnapshot defender = new CombatStatSnapshot(5f, 5f, 5f, 5f, 5f, 5f, 5f);

        CombatResolutionService blocked = new CombatResolutionService(new SequenceRandom(0f), evolution: null, overclock: null, environmentStatus: null, environmentalField: NoEnvironmentalFieldQuery.Instance, characters: null, environmentExposure: NoOpCharacterEnvironmentExposureCommand.Instance);
        CombatAttackResult friendlyRisk = blocked.Resolve(new CombatAttackRequest(
            "friendly-risk",
            "a",
            "b",
            attacker,
            defender,
            bow,
            7,
            CombatFireMode.Aimed,
            default,
            friendlyFireRisk: true));
        if (friendlyRisk.Executed || friendlyRisk.FailureReason != "아군 사격 위험")
        {
            return false;
        }

        CombatResolutionService coverService = new CombatResolutionService(new SequenceRandom(0f, 0f), evolution: null, overclock: null, environmentStatus: null, environmentalField: NoEnvironmentalFieldQuery.Instance, characters: null, environmentExposure: NoOpCharacterEnvironmentExposureCommand.Instance);
        CombatAttackResult cover = coverService.Resolve(new CombatAttackRequest(
            "cover",
            "a",
            "b",
            attacker,
            defender,
            bow,
            7,
            CombatFireMode.Aimed,
            new CombatCoverSnapshot(CombatCoverHeight.Low, 1f, 0f)));
        return cover.Executed && cover.CoverBlocked && !cover.Hit;
    }

    private static bool VerifyAttackPreview()
    {
        CombatWeaponSnapshot bow = CreateRangedWeapon(loadedAmmo: 1);
        CombatResolutionService service = new CombatResolutionService(new SequenceRandom(0.5f), evolution: null, overclock: null, environmentStatus: null, environmentalField: NoEnvironmentalFieldQuery.Instance, characters: null, environmentExposure: NoOpCharacterEnvironmentExposureCommand.Instance);
        CombatAttackRequest request = new CombatAttackRequest(
            "preview",
            "a",
            "b",
            new CombatStatSnapshot(5f, 10f, 4f, 5f, 5f, 5f, 8f),
            new CombatStatSnapshot(5f, 5f, 5f, 5f, 5f, 8f, 5f),
            bow,
            7,
            CombatFireMode.Aimed,
            new CombatCoverSnapshot(CombatCoverHeight.Low, 0.35f, 10f));
        CombatAttackPreview preview = service.Preview(request);
        CombatAttackPreview blocked = service.Preview(new CombatAttackRequest(
            "preview-blocked",
            "a",
            "b",
            request.Attacker,
            request.Defender,
            bow,
            7,
            CombatFireMode.Aimed,
            default,
            friendlyFireRisk: true));
        return preview.Valid
            && preview.RangeBand == CombatRangeBand.Medium
            && preview.HitChance > 0f
            && preview.CoverBlockChance > 0f
            && preview.DamageOnHit > 0f
            && preview.ExpectedDamage < preview.DamageOnHit
            && !blocked.Valid
            && blocked.FailureReason == "아군 사격 위험";
    }

    private static bool VerifyShieldAndArmor()
    {
        CombatWeaponSnapshot bow = CreateRangedWeapon(loadedAmmo: 1);
        CombatStatSnapshot attacker = new CombatStatSnapshot(8f, 10f, 3f, 5f, 7f, 5f, 8f);
        CombatStatSnapshot defender = new CombatStatSnapshot(5f, 5f, 0f, 0f, 5f, 8f, 5f);
        CombatShieldSnapshot shield = new CombatShieldSnapshot(
            "shield:1",
            CombatEquipmentQuality.Normal,
            1f,
            1f,
            0f,
            10f,
            8f,
            5f);
        CombatResolutionService shieldService = new CombatResolutionService(new SequenceRandom(0f, 0f), evolution: null, overclock: null, environmentStatus: null, environmentalField: NoEnvironmentalFieldQuery.Instance, characters: null, environmentExposure: NoOpCharacterEnvironmentExposureCommand.Instance);
        CombatAttackResult blocked = shieldService.Resolve(new CombatAttackRequest(
            "shield",
            "a",
            "b",
            attacker,
            defender,
            bow,
            7,
            CombatFireMode.Aimed,
            default,
            defenderShield: shield));
        if (!blocked.Executed || !blocked.ShieldBlocked || blocked.ArmorInstanceId != "shield:1")
        {
            return false;
        }

        CombatArmorSnapshot armor = new CombatArmorSnapshot(
            "armor:plate",
            CombatBodyPart.Torso,
            CombatArmorLayer.Plate,
            CombatEquipmentQuality.Normal,
            1f,
            24f,
            22f,
            14f);
        CombatArmorSnapshot underArmor = new CombatArmorSnapshot(
            "armor:mail",
            CombatBodyPart.Torso,
            CombatArmorLayer.Mail,
            CombatEquipmentQuality.Normal,
            1f,
            12f,
            9f,
            8f);
        CombatResolutionService armorService = new CombatResolutionService(new SequenceRandom(0f, 0.99f, 0.2f), evolution: null, overclock: null, environmentStatus: null, environmentalField: NoEnvironmentalFieldQuery.Instance, characters: null, environmentExposure: NoOpCharacterEnvironmentExposureCommand.Instance);
        CombatAttackResult armored = armorService.Resolve(new CombatAttackRequest(
            "armor",
            "a",
            "b",
            attacker,
            defender,
            bow,
            7,
            CombatFireMode.Aimed,
            default,
            defenderArmor: new[] { underArmor, armor }));
        return armored.Executed
            && armored.Hit
            && armored.BodyPart == CombatBodyPart.Torso
            && armored.ArmorInstanceId == "armor:plate"
            && armored.ArmorDurabilityHits.Count == 2
            && armored.ArmorDurabilityHits[0].InstanceId == "armor:plate"
            && armored.ArmorDurabilityHits[1].InstanceId == "armor:mail"
            && armored.AppliedDamage < armored.RawDamage
            && armored.ArmorDurabilityDamage > 0f;
    }

    private static bool VerifyTargetLethality()
    {
        CombatWeaponSnapshot sword = new CombatWeaponSnapshot(
            "weapon:test-sword",
            "weapon-instance:test",
            CombatEquipmentKind.MeleeWeapon,
            new MeleeStrikeVerb
            {
                attackTime = 1f,
                baseDamage = 10f,
                penetration = 7f,
                damageType = CombatDamageType.Slash,
                tracking = 0.08f
            },
            new[]
            {
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Contact,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                }
            },
            1,
            CombatEquipmentQuality.Normal,
            string.Empty,
            0,
            0,
            0f,
            true,
            false,
            false);
        CombatResolutionService service = new CombatResolutionService(new SequenceRandom(0f, 0.99f, 0.2f), evolution: null, overclock: null, environmentStatus: null, environmentalField: NoEnvironmentalFieldQuery.Instance, characters: null, environmentExposure: NoOpCharacterEnvironmentExposureCommand.Instance);
        CombatAttackResult result = service.Resolve(new CombatAttackRequest(
            "lethality",
            "a",
            "b",
            new CombatStatSnapshot(10f, 5f, 5f, 5f, 8f, 5f, 8f),
            new CombatStatSnapshot(5f, 5f, 5f, 5f, 5f, 8f, 5f),
            sword,
            1,
            CombatFireMode.Aimed,
            default,
            defenderMeleeLocked: true));
        int hitsToDown = Mathf.CeilToInt(120f / Mathf.Max(1f, result.AppliedDamage));
        return result.Hit && hitsToDown >= 4 && hitsToDown <= 7;
    }

    private static bool VerifyGunpowderSmokeMisfireAndRangedRoles()
    {
        CombatWeaponSnapshot bow = CreateAuthoredWeaponSnapshot(
            "weapon:composite-bow",
            "combat-role:bow",
            1f);
        CombatWeaponSnapshot crossbow = CreateAuthoredWeaponSnapshot(
            "weapon:windlass-crossbow",
            "combat-role:crossbow",
            1f);
        CombatWeaponSnapshot reliableGun = CreateAuthoredWeaponSnapshot(
            "weapon:handgonne",
            "combat-role:gun-reliable",
            0.8f);
        CombatWeaponSnapshot wornGun = CreateAuthoredWeaponSnapshot(
            "weapon:handgonne",
            "combat-role:gun-worn",
            0.1f);
        CombatStatSnapshot attacker = new CombatStatSnapshot(
            8f, 10f, 6f, 10f, 8f, 5f, 8f);
        CombatStatSnapshot defender = new CombatStatSnapshot(
            5f, 5f, 5f, 5f, 5f, 8f, 5f);
        CombatArmorSnapshot plate = new CombatArmorSnapshot(
            "armor:role-test",
            CombatBodyPart.Torso,
            CombatArmorLayer.Plate,
            CombatEquipmentQuality.Normal,
            1f,
            24f,
            22f,
            14f);

        RecordingEnvironmentExposureCommand misfireExposure = new();
        CombatAttackResult misfire = new CombatResolutionService(
            new SequenceRandom(0f),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure: misfireExposure).Resolve(new CombatAttackRequest(
                "gunpowder:worn",
                "character:combat-role:gunner",
                "combat-role:target",
                attacker,
                defender,
                wornGun,
                7,
                CombatFireMode.Aimed,
                default,
                defenderArmor: new[] { plate }));
        if (!wornGun.GunpowderWeapon
            || wornGun.MisfireChance <= 0f
            || wornGun.SmokeExposure <= 0f
            || !misfire.Executed
            || misfire.Hit
            || string.IsNullOrWhiteSpace(misfire.FailureReason)
            || misfire.Suppression > 0f
            || !Mathf.Approximately(
                misfire.SmokeExposure,
                wornGun.SmokeExposure)
            || misfireExposure.CallCount != 1
            || !misfireExposure.CharacterId.Equals(
                new CharacterId("character:combat-role:gunner"))
            || !Mathf.Approximately(
                misfireExposure.Amount,
                wornGun.SmokeExposure))
        {
            return false;
        }

        RecordingEnvironmentExposureCommand hitExposure = new();
        CombatResolutionService resolution = new CombatResolutionService(
            new SequenceRandom(0f, 0.99f, 0.2f),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure: hitExposure);
        CombatAttackResult gunHit = resolution.Resolve(new CombatAttackRequest(
            "gunpowder:hit",
            "character:combat-role:gunner",
            "combat-role:target",
            attacker,
            defender,
            reliableGun,
            7,
            CombatFireMode.Aimed,
            default,
            defenderArmor: new[] { plate }));
        if (!gunHit.Executed
            || !gunHit.Hit
            || gunHit.AppliedDamage <= 0f
            || !Mathf.Approximately(
                gunHit.SmokeExposure,
                reliableGun.SmokeExposure)
            || hitExposure.CallCount != 1
            || !hitExposure.CharacterId.Equals(
                new CharacterId("character:combat-role:gunner"))
            || !Mathf.Approximately(
                hitExposure.Amount,
                reliableGun.SmokeExposure)
            || !reliableGun.RequiresAmmo
            || string.IsNullOrWhiteSpace(reliableGun.AmmunitionItemId)
            || reliableGun.LoadedAmmo != 1)
        {
            return false;
        }

        RecordingEnvironmentExposureCommand missExposure = new();
        CombatAttackResult gunMiss = new CombatResolutionService(
            new SequenceRandom(1f),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure: missExposure).Resolve(new CombatAttackRequest(
                "gunpowder:miss",
                "character:combat-role:gunner",
                "combat-role:target",
                attacker,
                defender,
                reliableGun,
                7,
                CombatFireMode.Aimed,
                default));
        if (!gunMiss.Executed
            || gunMiss.Hit
            || !Mathf.Approximately(
                gunMiss.SmokeExposure,
                reliableGun.SmokeExposure)
            || missExposure.CallCount != 1
            || !missExposure.CharacterId.Equals(
                new CharacterId("character:combat-role:gunner"))
            || !Mathf.Approximately(
                missExposure.Amount,
                reliableGun.SmokeExposure))
        {
            return false;
        }

        CombatAttackPreview bowPreview = PreviewAgainstPlate(
            bow,
            attacker,
            defender,
            plate);
        CombatAttackPreview crossbowPreview = PreviewAgainstPlate(
            crossbow,
            attacker,
            defender,
            plate);
        CombatAttackPreview gunPreview = PreviewAgainstPlate(
            reliableGun,
            attacker,
            defender,
            plate);
        float bowReload = resolution.CalculateReloadTime(attacker, bow);
        float crossbowReload = resolution.CalculateReloadTime(attacker, crossbow);
        float gunReload = resolution.CalculateReloadTime(attacker, reliableGun);
        float bowInterval = resolution.CalculateAttackInterval(
            attacker,
            bow,
            CombatFireMode.Aimed);
        float crossbowInterval = resolution.CalculateAttackInterval(
            attacker,
            crossbow,
            CombatFireMode.Aimed);
        float gunInterval = resolution.CalculateAttackInterval(
            attacker,
            reliableGun,
            CombatFireMode.Aimed);

        return bowPreview.Valid
            && crossbowPreview.Valid
            && gunPreview.Valid
            && bowReload < crossbowReload
            && crossbowReload < gunReload
            && bowInterval < crossbowInterval
            && crossbowInterval < gunInterval
            && bowPreview.HitChance > gunPreview.HitChance
            && crossbowPreview.HitChance > gunPreview.HitChance
            && crossbowPreview.DamageOnHit > bowPreview.DamageOnHit
            && gunPreview.DamageOnHit > crossbowPreview.DamageOnHit
            && reliableGun.Verb.penetration > crossbow.Verb.penetration
            && crossbow.Verb.penetration > bow.Verb.penetration
            && !bow.GunpowderWeapon
            && !crossbow.GunpowderWeapon
            && bow.MisfireChance <= 0f
            && crossbow.MisfireChance <= 0f;
    }

    private static CombatAttackPreview PreviewAgainstPlate(
        CombatWeaponSnapshot weapon,
        CombatStatSnapshot attacker,
        CombatStatSnapshot defender,
        CombatArmorSnapshot plate)
    {
        CombatResolutionService resolution = new CombatResolutionService(
            new SequenceRandom(0.5f),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure:
                NoOpCharacterEnvironmentExposureCommand.Instance);
        return resolution.Preview(new CombatAttackRequest(
            $"role-preview:{weapon.DefinitionId}",
            "combat-role:attacker",
            "combat-role:defender",
            attacker,
            defender,
            weapon,
            7,
            CombatFireMode.Aimed,
            default,
            defenderArmor: new[] { plate }));
    }

    private static CombatWeaponSnapshot CreateAuthoredWeaponSnapshot(
        string definitionId,
        string characterId,
        float durabilityRatio)
    {
        IGameContentCatalog gameContent = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        WorldItemRepository repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        CombatEquipmentRuntime equipment = CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(gameContent),
            repository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: new ResourceEquipmentModuleCatalog(gameContent),
            materialCatalog: new ResourceEconomyContentCatalog(gameContent),
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        CombatEquipmentInstance instance = equipment.CreateInstance(
            definitionId,
            CombatEquipmentQuality.Normal);
        repository.EquipmentInstances[instance.instanceId].loadedAmmo = 1;
        repository.EquipmentInstances[instance.instanceId].durabilityRatio =
            Mathf.Clamp01(durabilityRatio);
        string assignFailure = string.Empty;
        string activeFailure = string.Empty;
        if (!equipment.TryAssignToCharacter(
                characterId,
                instance.instanceId,
                out assignFailure)
            || !equipment.TrySetActiveWeapon(
                characterId,
                instance.instanceId,
                out activeFailure)
            || !equipment.TryGetActiveWeapon(
                characterId,
                out CombatWeaponSnapshot weapon))
        {
            throw new InvalidOperationException(
                $"Could not project authored weapon '{definitionId}': "
                + $"assign={assignFailure}; active={activeFailure}");
        }

        return weapon;
    }

    private static bool VerifyEquipmentRuntime()
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        if (!catalog.TryGet("weapon:shortbow", out _)
            || !catalog.TryGet("armor:cloth-hood", out _)
            || !catalog.TryGet("shield:wood", out _))
        {
            return false;
        }

        WorldItemRepository itemRepository =
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            catalog,
            itemRepository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        CombatEquipmentInstance bow = runtime.CreateInstance("weapon:shortbow", CombatEquipmentQuality.Good);
        CombatEquipmentInstance hood = runtime.CreateInstance("armor:cloth-hood", CombatEquipmentQuality.Normal);
        CombatEquipmentInstance cap = runtime.CreateInstance("armor:leather-cap", CombatEquipmentQuality.Normal);
        CombatEquipmentInstance shield = runtime.CreateInstance("shield:wood", CombatEquipmentQuality.Normal);
        CombatEquipmentInstance sword = runtime.CreateInstance("weapon:longsword", CombatEquipmentQuality.Normal);
        if (!runtime.TryAssignToCharacter("worker:1", bow.instanceId, out _)
            || !runtime.TryAssignToCharacter("worker:1", hood.instanceId, out _)
            || runtime.TryAssignToCharacter("worker:1", cap.instanceId, out _)
            || runtime.TryAssignToCharacter("worker:1", shield.instanceId, out _)
            || !runtime.TryAssignToCharacter("worker:1", sword.instanceId, out _)
            || !runtime.TrySetActiveWeapon("worker:1", sword.instanceId, out _)
            || !runtime.TryAssignToCharacter("worker:1", shield.instanceId, out _)
            || runtime.TrySetActiveWeapon("worker:1", bow.instanceId, out _)
            || !VerifyPhysicalAmmunitionReload(runtime, bow)
            || !runtime.GetShield("worker:1").IsValid)
        {
            return false;
        }

        if (!runtime.TryApplyDurabilityDamage(hood.instanceId, 50f)
            || runtime.TryRestoreDurability(bow.instanceId, 1f)
            || !runtime.TryRestoreDurability(hood.instanceId, 0.9f)
            || !runtime.TryGetInstance(hood.instanceId, out CombatEquipmentInstance restoredHood)
            || restoredHood.durabilityRatio < 0.899f)
        {
            return false;
        }

        CharacterCombatLoadoutState loadout = runtime.GetOrCreateLoadout("worker:2");
        CharacterCombatLoadoutProfile archer = loadout.profiles.FirstOrDefault(profile =>
            profile.profileId == CombatLoadoutPresetIds.Archer);
        if (archer == null
            || !archer.desiredWeaponDefinitionIds.Contains("weapon:shortbow")
            || !archer.desiredWeaponDefinitionIds.Contains("weapon:dagger")
            || !archer.desiredArmorDefinitionIds.Contains("armor:leather")
            || archer.desiredAmmo != 30)
        {
            return false;
        }

        DungeonCombatEquipmentSaveData save = runtime.Capture();
        CombatEquipmentRuntime restored = CombatEquipmentEditorTestFactory.Create(
            catalog,
            itemRepository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        restored.PublishRestoreCandidate(
            restored.BuildRestoreCandidate(save));
        if (!restored.TryGetInstance(bow.instanceId, out CombatEquipmentInstance restoredBow)
            || restoredBow.quality != CombatEquipmentQuality.Good
            || restoredBow.loadedAmmo != 0
            || !restored.GetArmor("worker:1").Any()
            || !restored.GetShield("worker:1").IsValid)
        {
            return false;
        }

        return restored.TryGetInstance(
                bow.instanceId,
                out CombatEquipmentInstance savedBow)
            && savedBow.quality == CombatEquipmentQuality.Good;
    }

    private static bool VerifyAmmunitionConsumerAuthority()
    {
        ResourceGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        ResourceCombatEquipmentCatalog equipmentCatalog =
            new ResourceCombatEquipmentCatalog(content);
        HashSet<ItemDefinitionId> consumedAmmunitionIds =
            new HashSet<ItemDefinitionId>();

        foreach (CombatWeaponSO weapon in equipmentCatalog.All.OfType<CombatWeaponSO>())
        {
            IReadOnlyList<ItemDefinitionId> compatibleIds =
                weapon.CompatibleAmmunitionItemIds;
            if (weapon.Verbs.Any(verb => verb?.ConsumesAmmo == true)
                && compatibleIds.Count == 0)
            {
                return false;
            }

            if (compatibleIds.Any(itemId => !itemId.IsValid)
                || compatibleIds.Distinct().Count() != compatibleIds.Count)
            {
                return false;
            }

            foreach (ItemDefinitionId itemId in compatibleIds)
            {
                consumedAmmunitionIds.Add(itemId);
            }
        }

        foreach (BuildingSO building in LoadAuthoredAssets<BuildingSO>())
        {
            DefenseFacilityData defense =
                building?.GetAbility<BuildingDefenseAbility>()?.settings;
            if (defense?.supplyKind != DefenseSupplyKind.Ammunition)
            {
                continue;
            }

            ItemDefinitionId supplyItemId =
                (ItemDefinitionId)defense.supplyItemId;
            if (!supplyItemId.IsValid
                || defense.supplyCapacity <= 0
                || defense.supplyPerActivation <= 0)
            {
                return false;
            }

            consumedAmmunitionIds.Add(supplyItemId);
        }

        ResourceItemDefinitionSO[] resourceAmmunition =
            LoadAuthoredAssets<ResourceItemDefinitionSO>()
            .Where(item => item != null
                && item.Kind == ResourceItemKind.Ammunition)
            .ToArray();
        if (resourceAmmunition.Length != 11
            || resourceAmmunition.Any(item =>
                !consumedAmmunitionIds.Contains(
                    (ItemDefinitionId)item.ItemId)))
        {
            return false;
        }

        string[] bows =
        {
            "weapon:shortbow",
            "weapon:longbow",
            "weapon:composite-bow"
        };
        string[] crossbows =
        {
            "weapon:crossbow",
            "weapon:windlass-crossbow",
            "weapon:siege-arbalest"
        };
        string[] guns =
        {
            "weapon:handgonne",
            "weapon:matchlock-pistol",
            "weapon:arquebus"
        };
        string[] expectedArrows =
        {
            "ammo:arrow",
            "ammo:arrow-bone",
            "ammo:arrow-iron",
            "ammo:arrow-steel",
            "ammo:arrow-rune"
        };
        string[] expectedBolts =
        {
            "ammo:bolt",
            "ammo:bolt-bone",
            "ammo:bolt-iron",
            "ammo:bolt-steel",
            "ammo:bolt-rune"
        };
        string[] expectedCartridges = { "ammo:paper-cartridge" };

        return bows.All(id => HasExactAmmunition(
                equipmentCatalog,
                id,
                expectedArrows))
            && crossbows.All(id => HasExactAmmunition(
                equipmentCatalog,
                id,
                expectedBolts))
            && guns.All(id => HasExactAmmunition(
                equipmentCatalog,
                id,
                expectedCartridges))
            && consumedAmmunitionIds.Contains(
                (ItemDefinitionId)"ammo:blasting-charge")
            && consumedAmmunitionIds.Contains(
                (ItemDefinitionId)"ammo:trap-canister");
    }

    private static bool HasExactAmmunition(
        ICombatEquipmentCatalog catalog,
        string weaponId,
        IReadOnlyList<string> expectedItemIds)
    {
        return catalog.TryGet(weaponId, out CombatEquipmentDefinitionSO definition)
            && definition is CombatWeaponSO weapon
            && weapon.CompatibleAmmunitionItemIds
                .Select(itemId => itemId.Value)
                .SequenceEqual(expectedItemIds);
    }

    private static IReadOnlyList<T> LoadAuthoredAssets<T>()
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static bool VerifyPhysicalAmmunitionReload(
        CombatEquipmentRuntime runtime,
        CombatEquipmentInstance weapon)
    {
        GameObject inventoryObject = new GameObject(
            "CombatEquipment_PhysicalAmmoInventory");
        try
        {
            CharacterCarryInventory inventory =
                inventoryObject.AddComponent<CharacterCarryInventory>();
            inventory.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new CharacterCarriedItemSaveData
                    {
                        sourceStackId = "test:ammo:arrow-steel",
                        itemId = "ammo:arrow-steel",
                        quantity = 1
                    }
                }
            });

            return runtime.TryReloadFromInventory(
                    weapon.instanceId,
                    inventory,
                    out ItemDefinitionId consumedItemId,
                    out int consumed)
                && consumedItemId.Equals(
                    (ItemDefinitionId)"ammo:arrow-steel")
                && consumed == 1
                && inventory.CountItem("ammo:arrow-steel") == 0
                && runtime.TryConsumeLoadedAmmo(weapon.instanceId);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventoryObject);
        }
    }

    private static bool VerifyEquipmentDeathEventBridge()
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            catalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        GameEventBus events = new GameEventBus();
        CombatEquipmentCharacterDeathConnector connector =
            new CombatEquipmentCharacterDeathConnector(runtime, events);
        GameObject actorObject = new GameObject("CombatEquipmentDeath_Test");

        try
        {
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:combat:death:test");

            CombatEquipmentInstance first = runtime.CreateInstance(
                "weapon:dagger",
                CombatEquipmentQuality.Normal);
            if (!runtime.TryAssignToCharacter(
                    actor.Identity.PersistentId,
                    first.instanceId,
                    out _))
            {
                return false;
            }

            connector.Start();
            events.Publish(new CharacterDeathEvent(actor, "전투 검증"));
            if (!runtime.TryGetInstance(
                    first.instanceId,
                    out CombatEquipmentInstance lost)
                || lost.worldState != CombatEquipmentWorldState.Lost
                || !string.IsNullOrWhiteSpace(lost.ownerCharacterId))
            {
                return false;
            }

            CombatEquipmentInstance second = runtime.CreateInstance(
                "weapon:longsword",
                CombatEquipmentQuality.Normal);
            if (!runtime.TryAssignToCharacter(
                    actor.Identity.PersistentId,
                    second.instanceId,
                    out _))
            {
                return false;
            }

            connector.Dispose();
            events.Publish(new CharacterDeathEvent(actor, "구독 해제 검증"));
            return runtime.TryGetInstance(
                    second.instanceId,
                    out CombatEquipmentInstance retained)
                && retained.worldState == CombatEquipmentWorldState.Equipped
                && string.Equals(
                    retained.ownerCharacterId,
                    actor.Identity.PersistentId,
                    StringComparison.Ordinal);
        }
        finally
        {
            connector.Dispose();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyDownedHysteresis()
    {
        GameObject gameObject = new GameObject("V14 Downed Hysteresis Test");
        try
        {
            CharacterActor actor = gameObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:combat-downed-hysteresis");
            CharacterBodyHealthRuntime bodyHealth =
                new CharacterBodyHealthRuntime(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    new UnityGameClock(),
                    new GameEventBus(),
                    new DynamicFrameWorkBudget(
                        new UnityGameClock(),
                        new UnityUiClock()),
                    new ResourceAnatomyProfileCatalog(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader())),
                    new DefaultAnatomyActivityProfileCatalog(),
                    new DungeonRuntimeAggregateRootStore());
            CharacterBodyHealthSnapshot critical = new CharacterBodyHealthSnapshot(
                CreateBodyParts(headAndTorsoRatio: 0.2f, legRatio: 1f),
                bloodLoss: 0f,
                suppression: 0f,
                consciousness: 1f,
                manipulation: 1f,
                mobility: 1f,
                downed: false);
            bodyHealth.ApplySnapshot(actor, critical, "test-critical");
            if (!bodyHealth.GetSnapshot(actor).Downed)
            {
                return false;
            }

            CharacterBodyHealthSnapshot stillCritical = new CharacterBodyHealthSnapshot(
                CreateBodyParts(headAndTorsoRatio: 0.34f, legRatio: 1f),
                bloodLoss: 0f,
                suppression: 0f,
                consciousness: 1f,
                manipulation: 1f,
                mobility: 1f,
                downed: true);
            bodyHealth.ApplySnapshot(actor, stillCritical, "test-threshold");
            if (!bodyHealth.GetSnapshot(actor).Downed)
            {
                return false;
            }

            CharacterBodyHealthSnapshot recovered = new CharacterBodyHealthSnapshot(
                CreateBodyParts(headAndTorsoRatio: 0.35f, legRatio: 1f),
                bloodLoss: 0f,
                suppression: 0f,
                consciousness: 1f,
                manipulation: 1f,
                mobility: 1f,
                downed: true);
            bodyHealth.ApplySnapshot(actor, recovered, "test-recovered");
            return !bodyHealth.GetSnapshot(actor).Downed;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static bool VerifyBodyDamageOwnsDeath()
    {
        GameObject gameObject = new GameObject("V14 Body Damage Ownership Test");
        try
        {
            CharacterActor actor = gameObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:combat-body-damage-owner");
            CharacterBodyHealthRuntime bodyHealth =
                new CharacterBodyHealthRuntime(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    new UnityGameClock(),
                    new GameEventBus(),
                    new DynamicFrameWorkBudget(
                        new UnityGameClock(),
                        new UnityUiClock()),
                    new ResourceAnatomyProfileCatalog(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader())),
                    new DefaultAnatomyActivityProfileCatalog(),
                    new DungeonRuntimeAggregateRootStore());
            actor.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    bodyHealth,
                    bodyHealth,
                    new GameEventBus(),
                    new NoopOwnerRunLifecycleService()));
            actor.ApplyBodyDamage(actor.MaxHealth * 2f, "body-system-test");
            return !actor.IsDead
                && actor.CurrentHealth >= 1f
                && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static IReadOnlyList<CharacterBodyPartHealthState> CreateBodyParts(
        float headAndTorsoRatio,
        float legRatio)
    {
        return new[]
        {
            CreateBodyPart(CombatBodyPart.Head, 18f, headAndTorsoRatio),
            CreateBodyPart(CombatBodyPart.Torso, 45f, headAndTorsoRatio),
            CreateBodyPart(CombatBodyPart.LeftArm, 22f, 1f),
            CreateBodyPart(CombatBodyPart.RightArm, 22f, 1f),
            CreateBodyPart(CombatBodyPart.LeftLeg, 26f, legRatio),
            CreateBodyPart(CombatBodyPart.RightLeg, 26f, legRatio)
        };
    }

    private static CharacterBodyPartHealthState CreateBodyPart(
        CombatBodyPart bodyPart,
        float health,
        float ratio)
    {
        return new CharacterBodyPartHealthState
        {
            bodyPart = bodyPart,
            maxHealth = health,
            currentHealth = health * Mathf.Clamp01(ratio)
        };
    }

    private static bool VerifyForgeRecipeBridge()
    {
        BuildingSO forge = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset");
        BuildingEquipmentCraftingAbility crafting =
            forge?.GetAbility<BuildingEquipmentCraftingAbility>();
        if (crafting == null)
        {
            return false;
        }

        HashSet<string> recipes = new HashSet<string>(
            crafting.CraftableEquipmentIds,
            StringComparer.Ordinal);
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        return catalog.All.All(definition => recipes.Contains(definition.EquipmentId))
            && recipes.Contains(CombatItemDefinitions.ArrowBundleRecipeId)
            && recipes.Contains(CombatItemDefinitions.BoltBundleRecipeId);
    }

    private static bool VerifyLineOfSight()
    {
        Grid grid = new Grid(3, 2);
        GridCombatLineOfSightService service = new GridCombatLineOfSightService(affiliation: null, worldRegistry: null);
        CombatLineOfSightResult closed = service.Evaluate(grid, new Vector2Int(0, 0), new Vector2Int(0, 1));
        grid.SetAreaType(new Vector2Int(0, 0), GridCellAreaType.Entrance);
        grid.SetAreaType(new Vector2Int(0, 1), GridCellAreaType.Entrance);
        CombatLineOfSightResult open = service.Evaluate(grid, new Vector2Int(0, 0), new Vector2Int(0, 1));
        CombatCoverSnapshot front = new CombatCoverSnapshot(CombatCoverHeight.Low, 0.55f, 15f);
        CombatCoverSnapshot side = new CombatCoverSnapshot(CombatCoverHeight.Low, 0.55f, 60f);
        return !closed.HasLineOfSight
            && open.HasLineOfSight
            && Mathf.Approximately(front.GetDirectionalMultiplier(), 1f)
            && Mathf.Approximately(side.GetDirectionalMultiplier(), 0f);
    }

    private static bool VerifyCoverAssets()
    {
        (string path, float chance, float hitPoints, int materials)[] expected =
        {
            ("C01_WoodBarricade", 0.35f, 60f, 3),
            ("C02_SackBulwark", 0.55f, 90f, 4),
            ("C03_ArrowScreen", 0.70f, 110f, 5)
        };

        foreach ((string file, float chance, float hitPoints, int materials) in expected)
        {
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/Combat/{file}.asset");
            BuildingCoverAbility cover = building?.GetCoverAbility();
            IReadOnlyList<ItemAmountDefinition> requirements =
                building?.GetConstructionMaterials();
            if (building == null
                || building.runtimeArchetype != BuildingRuntimeArchetypeKind.Generic
                || building.layer != GridLayer.Building
                || cover == null
                || !Mathf.Approximately(cover.blockChance, chance)
                || !Mathf.Approximately(cover.coverHitPoints, hitPoints)
                || building.GetRequiredWork(BuiltInWorkTypeIds.Construct) <= 0f
                || requirements == null
                || requirements.Count != 1
                || requirements[0].Amount != materials
                || requirements[0].ItemId.StartsWith(
                    "stock-item:",
                    StringComparison.Ordinal)
                || building.sprite == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifyInitialStats()
    {
        CharacterStatDefinition[] definitions = CharacterStatCatalog.All.ToArray();
        if (definitions.Length != 12
            || definitions.All(item => item.Id != CharacterStatIds.Shooting)
            || definitions.All(item => item.Id != CharacterStatIds.Evasion)
            || definitions.All(item => item.Id != CharacterStatIds.Medical))
        {
            return false;
        }

        CharacterSkillSystemSettingsSO settings = ScriptableObject.CreateInstance<CharacterSkillSystemSettingsSO>();
        settings.initialStatTotal = 60;
        settings.initialStatMin = 1;
        settings.initialStatMax = 10;
        CharacterStatBlock block = CharacterGrowthRules.RollInitialStats(settings, new System.Random(991));
        int total = Enum.GetValues(typeof(CharacterStatType))
            .Cast<CharacterStatType>()
            .Sum(block.Get);
        UnityEngine.Object.DestroyImmediate(settings);
        return total == 60;
    }

    private static bool VerifySaveContract()
    {
        ResourceDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        return DungeonGameSaveData.CurrentVersion >= 16
            && catalog.TryGetDefinition(CombatItemDefinitions.ArrowItemId, out DungeonItemDefinition arrow)
            && catalog.TryGetDefinition(CombatItemDefinitions.BoltItemId, out DungeonItemDefinition bolt)
            && arrow.StockCategory == StockCategory.Ammunition
            && bolt.StockCategory == StockCategory.Ammunition
            && VerifyCanonicalPositiveIdParser(
                typeof(CharacterCombatCommandSaveValidation),
                "TryParseCommandId",
                "combat-command:")
            && VerifyCanonicalPositiveIdParser(
                typeof(DefenseTacticalCoordinator).Assembly.GetType(
                    "DefenseTacticalSaveValidation",
                    throwOnError: true),
                "TryParseReservationId",
                "combat-position:")
            && VerifyCanonicalPositiveIdParser(
                typeof(CharacterMedicalRuntime).Assembly.GetType(
                    "CharacterMedicalSaveValidation",
                    throwOnError: true),
                "TryParseOrderId",
                "medical:")
            && VerifyCanonicalPositiveIdParser(
                typeof(CharacterMedicalRuntime).Assembly.GetType(
                    "EquipmentMaintenanceSaveValidation",
                    throwOnError: true),
                "TryParseRepairOrderId",
                "equipment-repair:",
                canonicalSuffix: "000001")
            && VerifyCanonicalPositiveIdParser(
                typeof(CharacterMedicalRuntime).Assembly.GetType(
                    "EquipmentMaintenanceSaveValidation",
                    throwOnError: true),
                "TryParsePositiveSequence",
                "equipment-maintenance:custom:",
                passesPrefixArgument: true);
    }

    private static bool VerifyCanonicalPositiveIdParser(
        Type validatorType,
        string methodName,
        string prefix,
        bool passesPrefixArgument = false,
        string canonicalSuffix = "1")
    {
        System.Reflection.MethodInfo parser = validatorType.GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static)
            ?? throw new MissingMethodException(validatorType.FullName, methodName);

        return Parse(prefix + canonicalSuffix)
            && (canonicalSuffix == "1"
                || !Parse(prefix + "1")
                && !Parse(prefix + "000000")
                && !Parse(prefix + "0000001"))
            && !Parse(prefix + "+1")
            && !Parse(prefix + "01")
            && !Parse(prefix + "0")
            && !Parse(prefix + " 1");

        bool Parse(string value)
        {
            object[] arguments = passesPrefixArgument
                ? new object[] { value, prefix, 0 }
                : new object[] { value, 0 };
            return (bool)parser.Invoke(null, arguments);
        }
    }

    private static bool VerifyBodyHealthStrictSave()
    {
        GameObject gameObject = new GameObject("V18 Body Health Strict Save Test");
        try
        {
            CharacterActor actor = gameObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:body-health-strict-save");
            DungeonRuntimeAggregateRootStore root = new DungeonRuntimeAggregateRootStore();
            CharacterBodyHealthRuntime runtime = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                new UnityGameClock(),
                new GameEventBus(),
                new DynamicFrameWorkBudget(new UnityGameClock(), new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
                new DefaultAnatomyActivityProfileCatalog(),
                root);
            runtime.ConfigureVitals(actor, 123f, resetCurrentHealth: true);
            CharacterBodyHealthSaveSection section = new CharacterBodyHealthSaveSection(runtime);
            if (section is not IDungeonRollbackFreeSaveSection
                || section.SectionVersion != DungeonCharacterBodyHealthSaveData.CurrentVersion)
            {
                return false;
            }

            string validJson = section.Capture();
            DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
            section.Restore(validJson, section.SectionVersion, validReport);
            if (!validReport.Success || section.Capture() != validJson)
            {
                return false;
            }

            DungeonCharacterBodyHealthSaveData invalid = JsonUtility.FromJson<
                DungeonCharacterBodyHealthSaveData>(validJson);
            invalid.version--;
            invalid.characters[0].parts.Clear();
            string before = section.Capture();
            bool invalidRejected = false;
            try
            {
                section.StageRestore(
                    JsonUtility.ToJson(invalid),
                    section.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                invalidRejected = true;
            }

            bool directRejected = false;
            try
            {
                runtime.PrepareRestore(invalid);
            }
            catch (InvalidOperationException)
            {
                directRejected = true;
            }
            bool legacySectionRejected = false;
            try
            {
                section.ValidatePayload(
                    validJson,
                    section.SectionVersion - 1,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                legacySectionRejected = true;
            }

            return invalidRejected
                && directRejected
                && legacySectionRejected
                && section.Capture() == before
                && root.PublishedRestoreRevision == 0;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    public static bool VerifyCharacterMedicalStrictSave()
    {
        DungeonCharacterMedicalSaveData canonical = new()
        {
            version = DungeonCharacterMedicalSaveData.CurrentVersion,
            orderSequence = 1,
            orders = new List<CharacterMedicalOrder>
            {
                new CharacterMedicalOrder
                {
                    orderId = "medical:1",
                    patientId = "character:worker:downed",
                    state = CharacterMedicalOrderState.AwaitingRescue,
                    stabilized = true,
                    statusCode = CharacterMedicalStatusCode.AwaitingRescue
                }
            }
        };
        DungeonGameRestoreReport canonicalReport = new();
        ValidateCharacterMedicalSave(
            canonical,
            canonicalReport);
        DungeonCharacterMedicalSaveData roundTrip =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));

        DungeonCharacterMedicalSaveData legacy =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        legacy.version = DungeonCharacterMedicalSaveData.CurrentVersion - 1;
        string legacyBefore = JsonUtility.ToJson(legacy);
        DungeonGameRestoreReport legacyReport = new();
        ValidateCharacterMedicalSave(
            legacy,
            legacyReport);

        DungeonCharacterMedicalSaveData unknownStatus =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        unknownStatus.orders[0].statusCode =
            (CharacterMedicalStatusCode)int.MaxValue;
        string unknownBefore = JsonUtility.ToJson(unknownStatus);
        DungeonGameRestoreReport unknownReport = new();
        ValidateCharacterMedicalSave(
            unknownStatus,
            unknownReport);

        DungeonCharacterMedicalSaveData missingOrders =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        missingOrders.orders = null;
        string missingBefore = JsonUtility.ToJson(missingOrders);
        DungeonGameRestoreReport missingReport = new();
        ValidateCharacterMedicalSave(missingOrders, missingReport);

        DungeonCharacterMedicalSaveData duplicateOrder =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        duplicateOrder.orders.Add(
            JsonUtility.FromJson<CharacterMedicalOrder>(
                JsonUtility.ToJson(duplicateOrder.orders[0])));
        string duplicateBefore = JsonUtility.ToJson(duplicateOrder);
        DungeonGameRestoreReport duplicateReport = new();
        ValidateCharacterMedicalSave(duplicateOrder, duplicateReport);

        DungeonCharacterMedicalSaveData whitespacePatient =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        whitespacePatient.orders[0].patientId =
            " character:worker:downed ";
        DungeonGameRestoreReport whitespacePatientReport = new();
        ValidateCharacterMedicalSave(
            whitespacePatient,
            whitespacePatientReport);

        DungeonCharacterMedicalSaveData whitespaceRescuer =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        whitespaceRescuer.orders[0].rescuerId =
            " character:worker:rescuer ";
        DungeonGameRestoreReport whitespaceRescuerReport = new();
        ValidateCharacterMedicalSave(
            whitespaceRescuer,
            whitespaceRescuerReport);

        DungeonCharacterMedicalSaveData whitespaceOrderId =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        whitespaceOrderId.orders[0].orderId = " medical:1 ";
        DungeonGameRestoreReport whitespaceOrderReport = new();
        ValidateCharacterMedicalSave(
            whitespaceOrderId,
            whitespaceOrderReport);

        DungeonCharacterMedicalSaveData whitespaceFacility =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        whitespaceFacility.orders[0].treatmentFacilityId =
            " building:medical:fixture ";
        DungeonGameRestoreReport whitespaceFacilityReport = new();
        ValidateCharacterMedicalSave(
            whitespaceFacility,
            whitespaceFacilityReport);

        DungeonCharacterMedicalSaveData signedOrderId =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        signedOrderId.orders[0].orderId = "medical:+1";
        DungeonGameRestoreReport signedOrderReport = new();
        ValidateCharacterMedicalSave(signedOrderId, signedOrderReport);

        DungeonCharacterMedicalSaveData leadingZeroOrderId =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(canonical));
        leadingZeroOrderId.orders[0].orderId = "medical:01";
        DungeonGameRestoreReport leadingZeroOrderReport = new();
        ValidateCharacterMedicalSave(
            leadingZeroOrderId,
            leadingZeroOrderReport);

        return canonicalReport.Success
            && roundTrip.version == DungeonCharacterMedicalSaveData.CurrentVersion
            && roundTrip.orders.Single().statusCode
                == CharacterMedicalStatusCode.AwaitingRescue
            && !legacyReport.Success
            && string.Equals(
                legacyBefore,
                JsonUtility.ToJson(legacy),
                StringComparison.Ordinal)
            && !unknownReport.Success
            && string.Equals(
                unknownBefore,
                JsonUtility.ToJson(unknownStatus),
                StringComparison.Ordinal)
            && !missingReport.Success
            && string.Equals(
                missingBefore,
                JsonUtility.ToJson(missingOrders),
                StringComparison.Ordinal)
            && !duplicateReport.Success
            && string.Equals(
                duplicateBefore,
                JsonUtility.ToJson(duplicateOrder),
                StringComparison.Ordinal)
            && !whitespacePatientReport.Success
            && !whitespaceRescuerReport.Success
            && !whitespaceOrderReport.Success
            && !whitespaceFacilityReport.Success
            && !signedOrderReport.Success
            && !leadingZeroOrderReport.Success;
    }

    private static void ValidateCharacterMedicalSave(
        DungeonCharacterMedicalSaveData payload,
        DungeonGameRestoreReport report)
    {
        Type validator = typeof(CharacterMedicalRuntime).Assembly.GetType(
            "CharacterMedicalSaveValidation",
            throwOnError: true);
        System.Reflection.MethodInfo validate = validator.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static);
        validate.Invoke(null, new object[] { payload, report, null });
    }

    private static bool VerifyV14CombatLifecycleSave()
    {
        DungeonGameSaveData source = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            source,
            CharacterMedicalSaveSection.Id,
            DungeonCharacterMedicalSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState,
            new DungeonCharacterMedicalSaveData
        {
            version = DungeonCharacterMedicalSaveData.CurrentVersion,
            orderSequence = 3,
            orders = new List<CharacterMedicalOrder>
            {
                new CharacterMedicalOrder
                {
                    orderId = "medical:3",
                    patientId = "character:worker:downed",
                    rescuerId = "character:worker:rescuer",
                    stabilized = true,
                    carried = true,
                    state = CharacterMedicalOrderState.Carrying,
                    statusCode = CharacterMedicalStatusCode.Carrying,
                    requiredTreatmentWork = 36f,
                    completedTreatmentWork = 12f
                }
            }
        });
            DungeonSaveSectionPayload.Write(
            source,
            CharacterCombatCommandSaveSection.Id,
            2,
            DungeonSaveRestorePhase.LateRuntimeState,
            new CharacterCombatCommandSaveData
        {
            commandSequence = 1,
            stanceCharacterIds = new List<string> { "character:worker:rescuer" },
            revisions = new List<CharacterCombatCommandRevisionSaveData>
            {
                new CharacterCombatCommandRevisionSaveData
                {
                    actorId = "character:worker:rescuer",
                    revision = 1
                }
            },
            commands = new List<CharacterCombatCommand>
            {
                new CharacterCombatCommand
                {
                    commandId = "combat-command:1",
                    actorId = "character:worker:rescuer",
                    type = CombatCommandType.Rescue,
                    targetId = "character:worker:downed",
                    state = CharacterCombatCommandState.Executing,
                    revision = 1
                }
            }
        });
        DungeonSaveSectionPayload.Write(
            source,
            DefenseTacticalSaveSection.Id,
            2,
            DungeonSaveRestorePhase.RuntimeState,
            new DefenseTacticalCoordinatorSaveData
        {
            sequence = 1,
            reservations = new List<CombatPositionReservation>
            {
                new CombatPositionReservation
                {
                    reservationId = "combat-position:1",
                    actorId = "character:worker:rescuer",
                    targetId = "character:worker:downed",
                    kind = CombatPositionReservationKind.Rescue,
                    x = 4,
                    y = 2
                }
            }
        });
        DungeonSaveSectionPayload.Write(
            source,
            EquipmentMaintenanceSaveSection.Id,
            2,
            DungeonSaveRestorePhase.RuntimeState,
            new CombatEquipmentMaintenanceSaveData
        {
            policySequence = 0,
            orderSequence = 1,
            policies = new List<EquipmentMaintenancePolicyData>
            {
                new EquipmentMaintenancePolicyData
                {
                    id = EquipmentMaintenancePolicyRuntime.StandardPolicyId,
                    displayName = "표준",
                    automaticRepair = true,
                    sendAtDurability = 0.35f,
                    returnAtDurability = 0.9f
                },
                new EquipmentMaintenancePolicyData
                {
                    id = EquipmentMaintenancePolicyRuntime.PreventivePolicyId,
                    displayName = "예방 정비",
                    automaticRepair = true,
                    sendAtDurability = 0.6f,
                    returnAtDurability = 1f
                },
                new EquipmentMaintenancePolicyData
                {
                    id = EquipmentMaintenancePolicyRuntime.ManualPolicyId,
                    displayName = "수동",
                    automaticRepair = false,
                    sendAtDurability = 0f,
                    returnAtDurability = 1f
                }
            },
            orders = new List<CombatEquipmentRepairOrder>
            {
                new CombatEquipmentRepairOrder
                {
                    orderId = "equipment-repair:000001",
                    equipmentInstanceId = "item-instance:test-armor",
                    facilityBuildingId = "building:test-maintenance",
                    materialItemId = "material:test-metal",
                    requiredMaterialAmount = 1,
                    requiredWork = 24f,
                    completedWork = 8f,
                    state = CombatEquipmentRepairOrderState.InProgress
                }
            }
        });
        string json = JsonUtility.ToJson(source);
        DungeonGameSaveData restored = JsonUtility.FromJson<DungeonGameSaveData>(json);
        DungeonCharacterMedicalSaveData medical =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterMedicalSaveData>(
                restored,
                CharacterMedicalSaveSection.Id);
        CharacterCombatCommandSaveData commands =
            DungeonSaveSectionPayload.ReadOrNew<CharacterCombatCommandSaveData>(
                restored,
                CharacterCombatCommandSaveSection.Id);
        DefenseTacticalCoordinatorSaveData tactics =
            DungeonSaveSectionPayload.ReadOrNew<DefenseTacticalCoordinatorSaveData>(
                restored,
                DefenseTacticalSaveSection.Id);
        CombatEquipmentMaintenanceSaveData maintenance =
            DungeonSaveSectionPayload.ReadOrNew<CombatEquipmentMaintenanceSaveData>(
                restored,
                EquipmentMaintenanceSaveSection.Id);
        return restored != null
            && restored.version == DungeonGameSaveData.CurrentVersion
            && medical.version == DungeonCharacterMedicalSaveData.CurrentVersion
            && medical.orders.Single().carried
            && medical.orders.Single().statusCode
                == CharacterMedicalStatusCode.Carrying
            && medical.orders.Single().statusParameters.Count == 0
            && commands.commands.Single().type == CombatCommandType.Rescue
            && tactics.reservations.Single().kind
                == CombatPositionReservationKind.Rescue
            && Mathf.Approximately(
                maintenance.orders.Single().completedWork,
                8f);
    }

    private static CombatWeaponSnapshot CreateRangedWeapon(int loadedAmmo)
    {
        return new CombatWeaponSnapshot(
            "weapon:test-bow",
            "weapon-instance:test-bow",
            CombatEquipmentKind.RangedWeapon,
            new ProjectileVerb
            {
                attackTime = 1f,
                baseDamage = 10f,
                penetration = 6f,
                damageType = CombatDamageType.Pierce,
                projectileSpeed = 15f,
                tracking = 0.05f
            },
            new[]
            {
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Near,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                },
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Medium,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                },
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Long,
                    accuracyMultiplier = 0.75f,
                    damageMultiplier = 0.9f
                }
            },
            18,
            CombatEquipmentQuality.Normal,
            CombatItemDefinitions.ArrowItemId,
            1,
            loadedAmmo,
            1f,
            true,
            true,
            true);
    }

    private sealed class SequenceRandom : ICombatRandomSource
    {
        private readonly Queue<float> values;
        private readonly float fallback;

        public SequenceRandom(params float[] values)
        {
            this.values = new Queue<float>(values ?? Array.Empty<float>());
            fallback = values != null && values.Length > 0 ? values[values.Length - 1] : 0.5f;
        }

        public float Next01()
        {
            return values.Count > 0 ? Mathf.Clamp01(values.Dequeue()) : Mathf.Clamp01(fallback);
        }
    }

    private sealed class RecordingEnvironmentExposureCommand :
        ICharacterEnvironmentExposureCommand
    {
        public CharacterId CharacterId { get; private set; }
        public float Amount { get; private set; }
        public int CallCount { get; private set; }

        public bool AddAirborneExposure(CharacterId characterId, float amount)
        {
            CallCount++;
            CharacterId = characterId;
            Amount += Mathf.Max(0f, amount);
            return characterId.IsValid && amount > 0f;
        }
    }

    private sealed class NoopOwnerRunLifecycleService :
        IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
        }
    }
}
#endif
