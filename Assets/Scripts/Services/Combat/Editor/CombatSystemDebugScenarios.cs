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

    public static bool RunAll(bool logSuccess)
    {
        CombatEquipmentAssetBuilder.BuildAll();
        CombatCoverAssetBuilder.BuildAll();

        List<string> failures = new List<string>();
        Verify("거리 구간", VerifyRangeBands, failures);
        Verify("장비 품질", VerifyQualityMultipliers, failures);
        Verify("사격 판정 순서", VerifyRangedResolutionOrder, failures);
        Verify("공격 예측 계약", VerifyAttackPreview, failures);
        Verify("방패와 방어구", VerifyShieldAndArmor, failures);
        Verify("중간 치명도", VerifyTargetLethality, failures);
        Verify("장비 개체와 탄약 저장", VerifyEquipmentRuntime, failures);
        Verify("쓰러짐 회복 히스테리시스", VerifyDownedHysteresis, failures);
        Verify("대장작업대 제작 연결", VerifyForgeRecipeBridge, failures);
        Verify("층간 사선", VerifyLineOfSight, failures);
        Verify("건설형 엄폐물", VerifyCoverAssets, failures);
        Verify("11종 초기 스탯", VerifyInitialStats, failures);
        Verify("V14 생활 전투 저장", VerifyV14CombatLifecycleSave, failures);
        Verify("V14 저장 계약", VerifySaveContract, failures);

        Verify("부위 피해가 총체력 즉사를 선행하지 않음", VerifyBodyDamageOwnsDeath, failures);

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

        CombatResolutionService blocked = new CombatResolutionService(new SequenceRandom(0f));
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

        CombatResolutionService coverService = new CombatResolutionService(new SequenceRandom(0f, 0f));
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
        CombatResolutionService service = new CombatResolutionService(new SequenceRandom(0.5f));
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
        CombatResolutionService shieldService = new CombatResolutionService(new SequenceRandom(0f, 0f));
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
        CombatResolutionService armorService = new CombatResolutionService(new SequenceRandom(0f, 0.99f, 0.2f));
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
        CombatResolutionService service = new CombatResolutionService(new SequenceRandom(0f, 0.99f, 0.2f));
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

    private static bool VerifyEquipmentRuntime()
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog();
        if (!catalog.TryGet("weapon:shortbow", out _)
            || !catalog.TryGet("armor:cloth-hood", out _)
            || !catalog.TryGet("shield:wood", out _))
        {
            return false;
        }

        CombatEquipmentRuntime runtime = new CombatEquipmentRuntime(catalog);
        CombatEquipmentInstance bow = runtime.CreateInstance("weapon:shortbow", CombatEquipmentQuality.Good);
        CombatEquipmentInstance hood = runtime.CreateInstance("armor:cloth-hood", CombatEquipmentQuality.Normal);
        CombatEquipmentInstance cap = runtime.CreateInstance("armor:leather-cap", CombatEquipmentQuality.Normal);
        CombatEquipmentInstance shield = runtime.CreateInstance("shield:wood", CombatEquipmentQuality.Normal);
        CombatEquipmentInstance sword = runtime.CreateInstance("weapon:longsword", CombatEquipmentQuality.Normal);
        if (!runtime.TryLinkToWorldStack(
                bow.instanceId,
                "stack:test-bow",
                CombatEquipmentWorldState.Stored)
            || !runtime.TryAssignToCharacter("worker:1", bow.instanceId, out _)
            || !runtime.TryAssignToCharacter("worker:1", hood.instanceId, out _)
            || runtime.TryAssignToCharacter("worker:1", cap.instanceId, out _)
            || runtime.TryAssignToCharacter("worker:1", shield.instanceId, out _)
            || !runtime.TryAssignToCharacter("worker:1", sword.instanceId, out _)
            || !runtime.TrySetActiveWeapon("worker:1", sword.instanceId, out _)
            || !runtime.TryAssignToCharacter("worker:1", shield.instanceId, out _)
            || runtime.TrySetActiveWeapon("worker:1", bow.instanceId, out _)
            || !runtime.TryReload(bow.instanceId, 10, out int consumed)
            || consumed != 1
            || !runtime.TryConsumeLoadedAmmo(bow.instanceId)
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
        CombatEquipmentRuntime restored = new CombatEquipmentRuntime(catalog);
        restored.Restore(save);
        if (!restored.TryGetInstance(bow.instanceId, out CombatEquipmentInstance restoredBow)
            || restoredBow.quality != CombatEquipmentQuality.Good
            || restoredBow.loadedAmmo != 0
            || !restored.GetArmor("worker:1").Any()
            || !restored.GetShield("worker:1").IsValid)
        {
            return false;
        }

        return restored.TryLinkToWorldStack(
                bow.instanceId,
                "stack:dropped-bow",
                CombatEquipmentWorldState.Loose)
            && restored.TryGetInstance(bow.instanceId, out CombatEquipmentInstance droppedBow)
            && droppedBow.quality == CombatEquipmentQuality.Good
            && droppedBow.worldState == CombatEquipmentWorldState.Loose
            && string.Equals(droppedBow.sourceStackId, "stack:dropped-bow", StringComparison.Ordinal);
    }

    private static bool VerifyDownedHysteresis()
    {
        GameObject gameObject = new GameObject("V14 Downed Hysteresis Test");
        try
        {
            CharacterActor actor = gameObject.AddComponent<CharacterActor>();
            CharacterBodyHealthRuntime bodyHealth =
                new CharacterBodyHealthRuntime(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    new UnityGameClock(),
                    new GameEventBus());
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
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog();
        return catalog.All.All(definition => recipes.Contains(definition.EquipmentId))
            && recipes.Contains(CombatItemDefinitions.ArrowBundleRecipeId)
            && recipes.Contains(CombatItemDefinitions.BoltBundleRecipeId);
    }

    private static bool VerifyLineOfSight()
    {
        Grid grid = new Grid(3, 2);
        GridCombatLineOfSightService service = new GridCombatLineOfSightService();
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
            Dictionary<StockCategory, int> requirements = building?.GetConstructionMaterials();
            if (building == null
                || building.type != typeof(BuildableObject)
                || building.layer != GridLayer.Building
                || cover == null
                || !Mathf.Approximately(cover.blockChance, chance)
                || !Mathf.Approximately(cover.coverHitPoints, hitPoints)
                || building.GetRequiredWork(BuiltInWorkTypeIds.Construct) <= 0f
                || requirements == null
                || !requirements.TryGetValue(StockCategory.General, out int amount)
                || amount != materials
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
        if (definitions.Length != 11
            || definitions.All(item => item.Id != CharacterStatIds.Shooting)
            || definitions.All(item => item.Id != CharacterStatIds.Evasion))
        {
            return false;
        }

        CharacterSkillSystemSettingsSO settings = ScriptableObject.CreateInstance<CharacterSkillSystemSettingsSO>();
        settings.initialStatTotal = 55;
        settings.initialStatMin = 1;
        settings.initialStatMax = 10;
        CharacterStatBlock block = CharacterGrowthRules.RollInitialStats(settings, new System.Random(991));
        int total = Enum.GetValues(typeof(CharacterStatType))
            .Cast<CharacterStatType>()
            .Sum(block.Get);
        UnityEngine.Object.DestroyImmediate(settings);
        return total == 55;
    }

    private static bool VerifySaveContract()
    {
        return DungeonGameSaveData.CurrentVersion == 15
            && CombatItemDefinitions.TryGetDefinition(CombatItemDefinitions.ArrowItemId, out DungeonItemDefinition arrow)
            && CombatItemDefinitions.TryGetDefinition(CombatItemDefinitions.BoltItemId, out DungeonItemDefinition bolt)
            && arrow.StockCategory == StockCategory.Ammunition
            && bolt.StockCategory == StockCategory.Ammunition;
    }

    private static bool VerifyV14CombatLifecycleSave()
    {
        DungeonGameSaveData source = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            source,
            CharacterMedicalSaveSection.Id,
            1,
            DungeonSaveRestorePhase.RuntimeState,
            new DungeonCharacterMedicalSaveData
        {
            orderSequence = 3,
            orders = new List<CharacterMedicalOrder>
            {
                new CharacterMedicalOrder
                {
                    orderId = "medical:3",
                    patientId = "worker:downed",
                    rescuerId = "worker:rescuer",
                    stabilized = true,
                    carried = true,
                    state = CharacterMedicalOrderState.Carrying,
                    requiredTreatmentWork = 36f,
                    completedTreatmentWork = 12f
                }
            }
        });
        DungeonSaveSectionPayload.Write(
            source,
            CharacterCombatCommandSaveSection.Id,
            1,
            DungeonSaveRestorePhase.LateRuntimeState,
            new CharacterCombatCommandSaveData
        {
            stanceCharacterIds = new List<string> { "worker:rescuer" },
            commands = new List<CharacterCombatCommand>
            {
                new CharacterCombatCommand
                {
                    commandId = "combat-command:1",
                    actorId = "worker:rescuer",
                    type = CombatCommandType.Rescue,
                    targetId = "worker:downed",
                    state = CharacterCombatCommandState.Executing
                }
            }
        });
        DungeonSaveSectionPayload.Write(
            source,
            DefenseTacticalSaveSection.Id,
            1,
            DungeonSaveRestorePhase.RuntimeState,
            new DefenseTacticalCoordinatorSaveData
        {
            reservations = new List<CombatPositionReservation>
            {
                new CombatPositionReservation
                {
                    reservationId = "combat-position:1",
                    actorId = "worker:rescuer",
                    targetId = "worker:downed",
                    kind = CombatPositionReservationKind.Rescue,
                    x = 4,
                    y = 2
                }
            }
        });
        DungeonSaveSectionPayload.Write(
            source,
            EquipmentMaintenanceSaveSection.Id,
            1,
            DungeonSaveRestorePhase.RuntimeState,
            new CombatEquipmentMaintenanceSaveData
        {
            policies = new List<EquipmentMaintenancePolicyData>
            {
                new EquipmentMaintenancePolicyData
                {
                    id = EquipmentMaintenancePolicyRuntime.StandardPolicyId,
                    displayName = "표준",
                    automaticRepair = true,
                    sendAtDurability = 0.35f,
                    returnAtDurability = 0.9f
                }
            },
            orders = new List<CombatEquipmentRepairOrder>
            {
                new CombatEquipmentRepairOrder
                {
                    orderId = "equipment-repair:1",
                    equipmentInstanceId = "armor:instance:1",
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
            && restored.version == 15
            && medical.orders.Single().carried
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
}
#endif
