#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterSpeciesExpansionAssetBuilder
{
    // This builder is intentionally idempotent so content can be rebuilt safely.
    private const string SpeciesRoot =
        "Assets/Resources/SO/Character/Species";
    private const string CharacterRoot =
        "Assets/Resources/SO/Character/ExpandedSpecies";

    [MenuItem("DungeonStory/Content/Build Species Expansion")]
    public static void BuildAll()
    {
        EnsureFolder(SpeciesRoot);
        EnsureFolder(CharacterRoot);

        Dictionary<string, CharacterSpeciesSO> species = BuildSpecies();
        BuildCharacterTemplates(species);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"Species expansion built: {species.Count} species, " +
            $"{species.Values.Count(value => !value.ownerSelectable)} NPC-only species.");
    }

    private static Dictionary<string, CharacterSpeciesSO> BuildSpecies()
    {
        SpeciesSpec[] specs = CreateSpecs();
        Dictionary<string, CharacterSpeciesSO> result =
            new Dictionary<string, CharacterSpeciesSO>(
                StringComparer.OrdinalIgnoreCase);
        foreach (SpeciesSpec spec in specs)
        {
            string path = $"{SpeciesRoot}/Species_{spec.Tag}.asset";
            CharacterSpeciesSO asset =
                AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CharacterSpeciesSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            Apply(asset, spec);
            EditorUtility.SetDirty(asset);
            result.Add(spec.Tag, asset);
        }

        return result;
    }

    private static void BuildCharacterTemplates(
        IReadOnlyDictionary<string, CharacterSpeciesSO> species)
    {
        Sprite[] fallbackSprites =
        {
            LoadCharacter("Assets/Resources/SO/Character/Customer_Orc.asset")
                ?.characterSprite,
            LoadCharacter("Assets/Resources/SO/Character/Customer_Vampire.asset")
                ?.characterSprite,
            LoadCharacter("Assets/Resources/SO/Character/New Character SO.asset")
                ?.characterSprite
        };
        SpeciesSpec[] npcSpecs = CreateSpecs()
            .Where(spec => spec.Policy == SpeciesOwnerSelectionPolicy.NpcOnly)
            .ToArray();
        for (int i = 0; i < npcSpecs.Length; i++)
        {
            SpeciesSpec spec = npcSpecs[i];
            string path = $"{CharacterRoot}/Customer_{spec.Tag}.asset";
            CharacterSO asset = AssetDatabase.LoadAssetAtPath<CharacterSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CharacterSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.characterType = CharacterType.Customer;
            asset.role = CharacterRole.Regular;
            asset.id = 9004 + i;
            asset.characterName = spec.CharacterName;
            asset.speciesTag = spec.Tag;
            asset.species = species[spec.Tag];
            asset.baseStats = CharacterStatBlock.CreateDefault();
            foreach ((CharacterStatType stat, int value) in spec.BaseStats)
            {
                asset.baseStats.Set(stat, value);
            }

            asset.traits = Array.Empty<CharacterTraitSO>();
            asset.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            foreach (FacilityWorkType type in EnumerateFlags(spec.StrongWork))
            {
                if (WorkTypeCatalog.TryGet(type, out WorkTypeDefinition definition))
                {
                    asset.defaultWorkPriorities.SetPriority(
                        definition.WorkTypeId,
                        WorkPriorityLevel.Priority1);
                }
            }

            asset.aiPersonality = spec.Personality;
            asset.characterSprite = fallbackSprites[i % fallbackSprites.Length];
            SetPrivate(asset, "frequencyVisitMin", 1);
            SetPrivate(asset, "frequencyVisitMax", 3);
            SetPrivate(asset, "minHoldingMoney", spec.MinimumMoney);
            SetPrivate(asset, "maxHoldingMoney", spec.MaximumMoney);
            SetPrivate(asset, "speedType", spec.SpeedValue);
            SetPrivate(asset, "respawnSpeedType", 13);
            EditorUtility.SetDirty(asset);
        }
    }

    private static void Apply(CharacterSpeciesSO asset, SpeciesSpec spec)
    {
        asset.id = spec.Id;
        asset.speciesTag = spec.Tag;
        asset.displayName = spec.Name;
        asset.ownerSelectionPolicy = spec.Policy;
        asset.homeFactionId = spec.HomeFactionId;
        asset.anatomyProfileId = spec.AnatomyProfileId;
        asset.needs = spec.Needs;
        asset.environment = spec.Environment;
        asset.relationTags = spec.RelationTags;
        asset.defenseAffinityTags = spec.DefenseTags;
        asset.strongWorkTypeIds = WorkIds(spec.StrongWork);
        asset.weakWorkTypeIds = WorkIds(spec.WeakWork);
        if (spec.Policy == SpeciesOwnerSelectionPolicy.NpcOnly)
        {
            asset.preferredFacilityLabels = spec.PreferredFacilities;
            asset.dislikedEnvironmentLabels = spec.DislikedEnvironments;
            asset.shortDescription = spec.ShortDescription;
            asset.description = spec.Description;
        }
        asset.stayDurationMultiplier = spec.StayDurationMultiplier;
        asset.crimeRiskMultiplier = spec.CrimeRiskMultiplier;
        asset.incident = new SpeciesIncidentDefinition
        {
            incidentId = spec.IncidentId,
            displayName = spec.IncidentName,
            description = spec.IncidentDescription,
            mitigatingRoles = spec.IncidentMitigatingRoles,
            triggerTags = spec.IncidentTriggerTags
        };
        asset.incidentName = spec.IncidentName;
        asset.incidentDescription = spec.IncidentDescription;
        asset.incidentMitigatingRoles = spec.IncidentMitigatingRoles;
        asset.combatPassive = spec.Passive;
        if (spec.Policy == SpeciesOwnerSelectionPolicy.NpcOnly)
        {
            asset.combatAbilities ??= new CharacterCombatAbilityCollection();
            asset.combatAbilities.SetAbilities(new[]
            {
                new CharacterCombatAbilityDefinition(
                    $"species-active:{spec.Tag.ToLowerInvariant()}",
                    spec.ActiveName,
                    spec.ActiveDescription,
                    spec.ActiveCooldown,
                    spec.ActiveTarget,
                    spec.ActiveEffects)
            });
            asset.statBonus = CharacterStatBlock.CreateDefault(0);
            foreach ((CharacterStatType stat, int value) in spec.SpeciesStats)
            {
                asset.statBonus.Set(stat, value);
            }

            asset.modifiers = spec.Modifiers;
        }
    }

    private static SpeciesSpec[] CreateSpecs()
    {
        return new[]
        {
            Existing(
                1, "Slime", "슬라임", "anatomy:slime",
                CharacterSpeciesIncidentIds.SlimeContamination,
                "슬라임 오염", FacilityRole.Rest,
                new[] { "습기", "오염", "저가" },
                new[] { "냉기", "부식" },
                Env(16, 24, 5, 34, 0, 40, 55),
                Passive("species-passive:slime", "유동 신체",
                    "좁은 물류 동선과 부식 방어시설 운용에 능하다.", "물류", "부식")),
            Existing(
                2, "Orc", "오크", "anatomy:humanoid",
                CharacterSpeciesIncidentIds.OrcRampage,
                "오크 난동", FacilityRole.Training | FacilityRole.Security,
                new[] { "소음", "혼잡", "야외" },
                new[] { "물리", "화염", "전선-저지" },
                Env(12, 30, -5, 42, -15, 50, 65),
                Passive("species-passive:orc", "전선 본능",
                    "전선 저지와 물리·화염 시설 운용에 강하다.", "저지", "물리")),
            Existing(
                3, "Vampire", "뱀파이어", "anatomy:humanoid",
                CharacterSpeciesIncidentIds.VampireFear,
                "흡혈 공포", FacilityRole.Rest | FacilityRole.Entertainment,
                new[] { "고급", "마력", "암흑" },
                new[] { "독", "냉기", "공포" },
                Env(8, 22, 0, 34, -10, 42, 70),
                Passive("species-passive:vampire", "야행성 위압",
                    "독·냉기·공포 방어시설의 통제력이 높다.", "공포", "야간")),
            NewSpecies(
                4, "Beastkin", "수인", "붉은발 역참",
                "anatomy:humanoid",
                Needs(1.35f, 1.15f, MealDietClass.Carnivore, 1.4f),
                Env(10, 29, -4, 40, -12, 48, 70),
                FacilityWorkType.Haul | FacilityWorkType.Restock
                    | FacilityWorkType.Hunt | FacilityWorkType.Reception,
                FacilityWorkType.Research | FacilityWorkType.Surgery,
                new[] { "야외", "무리", "소음" },
                new[] { "감지", "재장전", "번개" },
                new[] { "고기 식당", "상점", "야외 휴식처" },
                new[] { "장기 고립", "밀폐 침실", "채식 전용 식당" },
                CharacterSpeciesIncidentIds.BeastkinCommotion,
                "수인 소동",
                "혼잡과 무리 욕구가 누적되면 주변 대기열을 흐트러뜨리고 실제 물품 운반을 중단한다.",
                FacilityRole.Rest | FacilityRole.Entertainment,
                "갈퀴 돌진", "적을 밀어내며 빠르게 연속 공격한다.",
                new OffenseDamageEffect(1.15f, 2f, 2),
                Passive("species-passive:beastkin", "무리 운반",
                    "같은 목적지의 운반과 재장전을 빠르게 마친다.", "물류", "무리"),
                "라카", 8, 5, 4, 7, 7, 120, 320, 5),
            NewSpecies(
                5, "Demon", "데몬", "잿불 계약정",
                "anatomy:humanoid",
                Needs(1f, 0.9f, MealDietClass.Mixed, 0.8f),
                Env(20, 34, 10, 46, -2, 56, 60),
                FacilityWorkType.Research | FacilityWorkType.Guard,
                FacilityWorkType.Clean | FacilityWorkType.AnimalCare,
                new[] { "화염", "마력", "고급" },
                new[] { "화염", "번개", "공포" },
                new[] { "마나실", "고급 객실", "연구실" },
                new[] { "저온 창고", "저가 숙소", "단순 배식소" },
                CharacterSpeciesIncidentIds.DemonContractCurse,
                "계약 저주",
                "대우가 계약 기대에 못 미치면 지정 서비스실에 저주 부담과 유지비를 남긴다.",
                FacilityRole.Administration | FacilityRole.Mana,
                "잿불 계약", "강한 화염 피해와 취약 표식을 남긴다.",
                new OffenseDamageEffect(1.4f, 4f),
                Passive("species-passive:demon", "지옥의 권위",
                    "마력 생산과 공포 통제의 효율이 높다.", "마력", "위압"),
                "아자라", 8, 7, 8, 5, 6, 300, 650, 4),
            NewSpecies(
                6, "Kobold", "코볼트", "심층 톱니굴",
                "anatomy:humanoid",
                Needs(0.85f, 0.9f, MealDietClass.Mixed, 0.9f),
                Env(11, 28, -2, 40, -10, 48, 60),
                FacilityWorkType.Quarry | FacilityWorkType.Repair
                    | FacilityWorkType.Refuel,
                FacilityWorkType.Reception | FacilityWorkType.Perform,
                new[] { "질서", "협소", "기계" },
                new[] { "기계", "수리", "탄약" },
                new[] { "정비실", "채굴 작업장", "좁은 숙소" },
                new[] { "대형 연회장", "무질서한 창고", "야외 노숙지" },
                CharacterSpeciesIncidentIds.KoboldPartsHoarding,
                "부품 사재기",
                "불만이 커지면 실제 금속 부품과 탄약을 은닉 스택으로 옮긴다.",
                FacilityRole.Logistics | FacilityRole.Security,
                "급조 함정", "적의 행동을 지연시키고 방어를 약화한다.",
                new OffenseDelayEffect(0.35f),
                Passive("species-passive:kobold", "톱니 감각",
                    "기계 함정 재설정과 수리·탄약 보급이 빠르다.", "기계", "정비"),
                "티크", 5, 5, 6, 8, 4, 70, 220, 5),
            NewSpecies(
                7, "Myconid", "균사인", "균사 심림",
                "anatomy:fungal",
                Needs(0.75f, 1.2f, MealDietClass.Vegan, 0.7f),
                Env(8, 22, 0, 32, -8, 40, 35),
                FacilityWorkType.Sow | FacilityWorkType.Harvest
                    | FacilityWorkType.Treat | FacilityWorkType.Clean,
                FacilityWorkType.Guard | FacilityWorkType.Perform,
                new[] { "습기", "오염", "저온" },
                new[] { "독", "포자", "제독" },
                new[] { "재배실", "퇴비실", "약제실" },
                new[] { "건조 열원", "강한 조명", "화염 통로" },
                CharacterSpeciesIncidentIds.MyconidSporeBloom,
                "포자 개화",
                "건조 노출과 불만이 겹치면 실제 포자 오염을 주변 셀에 생성한다.",
                FacilityRole.Hygiene | FacilityRole.Medical,
                "회복 포자", "아군을 치료하고 해로운 상태를 정화한다.",
                new OffenseHealEffect(12f),
                Passive("species-passive:myconid", "균사 순환",
                    "재배·약품·오염 처리와 독 시설 운용에 강하다.", "재배", "제독"),
                "모르", 4, 5, 7, 5, 6, 80, 250, 3),
            NewSpecies(
                8, "Harpy", "하피", "폭풍 둥지",
                "anatomy:avian",
                Needs(1.1f, 1f, MealDietClass.Mixed, 1.15f),
                Env(7, 25, -5, 36, -15, 44, 80),
                FacilityWorkType.Reception | FacilityWorkType.Guard
                    | FacilityWorkType.Hunt,
                FacilityWorkType.Quarry | FacilityWorkType.Construct,
                new[] { "야외", "청정", "개방" },
                new[] { "경보", "원거리", "외부-엄호" },
                new[] { "전망대", "접수실", "야외 휴식처" },
                new[] { "오염 공기", "낮은 천장", "혼잡 통로" },
                CharacterSpeciesIncidentIds.HarpyGaleCommotion,
                "돌풍 소동",
                "불만이 폭발하면 loose stack을 삭제하지 않고 인접 통행 셀로 흩트린다.",
                FacilityRole.Rest | FacilityRole.Logistics,
                "폭풍 사격", "원거리 공격 뒤 적의 다음 행동을 늦춘다.",
                new OffenseDamageEffect(1.1f, 3f),
                Passive("species-passive:harpy", "고지 시야",
                    "정찰·경보·원거리 엄호의 식별 범위가 넓다.", "정찰", "원거리"),
                "세라", 6, 6, 5, 8, 4, 130, 380, 5),
            NewSpecies(
                9, "Golem", "골렘", "석맥 주조소",
                "anatomy:construct",
                ConstructNeeds(),
                Env(-5, 35, -20, 50, -35, 65, 20),
                FacilityWorkType.Haul | FacilityWorkType.Construct
                    | FacilityWorkType.Repair | FacilityWorkType.Plumbing,
                FacilityWorkType.Reception | FacilityWorkType.Research,
                new[] { "질서", "마력", "기계" },
                new[] { "방벽", "중화기", "시설-복구" },
                new[] { "충전소", "정비실", "중량 하역장" },
                new[] { "침수 구역", "부식 오염", "장기 무충전 구역" },
                CharacterSpeciesIncidentIds.GolemCoreOverload,
                "핵 과부하",
                "충전 부족과 손상이 겹치면 핵이 과열되어 시설을 파손할 수 있다.",
                FacilityRole.Mana | FacilityRole.Logistics,
                "주조 방벽", "자신과 아군의 피해를 잠시 크게 줄인다.",
                new OffenseGuardEffect(0.45f, 2),
                Passive("species-passive:golem", "불굴 구조",
                    "중량 운반·건설·방벽 복구에 강하지만 생물 수술을 받을 수 없다.", "중량", "정비"),
                "바살트-7", 6, 4, 4, 4, 10, 40, 120, 2)
        };
    }

    private static SpeciesSpec Existing(
        int id,
        string tag,
        string name,
        string anatomy,
        string incidentId,
        string incidentName,
        FacilityRole mitigation,
        string[] relationTags,
        string[] defenseTags,
        SpeciesEnvironmentProfile environment,
        SpeciesPassiveDefinition passive)
    {
        return new SpeciesSpec
        {
            Id = id,
            Tag = tag,
            Name = name,
            Policy = SpeciesOwnerSelectionPolicy.Selectable,
            AnatomyProfileId = anatomy,
            Needs = Needs(1f, 1f, MealDietClass.Mixed, 1f),
            Environment = environment,
            RelationTags = relationTags,
            DefenseTags = defenseTags,
            StrongWork = FacilityWorkType.None,
            WeakWork = FacilityWorkType.None,
            PreferredFacilities = Array.Empty<string>(),
            DislikedEnvironments = Array.Empty<string>(),
            IncidentId = incidentId,
            IncidentName = incidentName,
            IncidentDescription = string.Empty,
            IncidentMitigatingRoles = mitigation,
            IncidentTriggerTags = new[] { "discontent" },
            Passive = passive,
            ActiveName = $"{name} 종족기",
            ActiveDescription = $"{name}의 고유 전투 능력",
            ActiveCooldown = 2,
            ActiveTarget = OffenseBattleTargetRule.Enemy,
            ActiveEffects = new OffenseCombatEffectModule[]
            {
                new OffenseDamageEffect(1f)
            }
        };
    }

    private static SpeciesSpec NewSpecies(
        int id,
        string tag,
        string name,
        string factionName,
        string anatomy,
        SpeciesNeedProfile needs,
        SpeciesEnvironmentProfile environment,
        FacilityWorkType strongWork,
        FacilityWorkType weakWork,
        string[] relationTags,
        string[] defenseTags,
        string[] facilities,
        string[] disliked,
        string incidentId,
        string incidentName,
        string incidentDescription,
        FacilityRole mitigation,
        string activeName,
        string activeDescription,
        OffenseCombatEffectModule activeEffect,
        SpeciesPassiveDefinition passive,
        string characterName,
        int attack,
        int sales,
        int research,
        int dexterity,
        int toughness,
        int minimumMoney,
        int maximumMoney,
        int speed)
    {
        CharacterModelModifiers modifiers = new CharacterModelModifiers
        {
            workSpeedMultiplier = 1.05f,
            moveSpeedMultiplier = tag == "Beastkin" || tag == "Harpy" ? 1.12f : 1f,
            spendingMultiplier = tag == "Demon" ? 1.6f : 1f,
            combatPowerMultiplier = tag == "Demon" || tag == "Golem" ? 1.15f : 1f,
            consumptionMultiplier = needs.hungerRateMultiplier,
            crowdSensitivityMultiplier = tag == "Beastkin" ? 1.3f : 1f
        };
        modifiers.SetWorkPreferences(strongWork, weakWork);
        return new SpeciesSpec
        {
            Id = id,
            Tag = tag,
            Name = name,
            HomeFactionId = $"faction:dungeon:{tag.ToLowerInvariant()}",
            Policy = SpeciesOwnerSelectionPolicy.NpcOnly,
            AnatomyProfileId = anatomy,
            Needs = needs,
            Environment = environment,
            StrongWork = strongWork,
            WeakWork = weakWork,
            RelationTags = relationTags,
            DefenseTags = defenseTags,
            PreferredFacilities = facilities,
            DislikedEnvironments = disliked,
            ShortDescription = $"{factionName} 출신 · {string.Join("·", facilities)} 선호",
            Description =
                $"{name}은(는) {factionName}을 대표하는 종족이다. " +
                "손님·모집 직원·상인·포로·동맹 지원군으로 등장한다.",
            StayDurationMultiplier = 1f,
            CrimeRiskMultiplier = tag == "Kobold" ? 1.15f : 1f,
            IncidentId = incidentId,
            IncidentName = incidentName,
            IncidentDescription = incidentDescription,
            IncidentMitigatingRoles = mitigation,
            IncidentTriggerTags = new[] { "discontent", "species-specific" },
            Passive = passive,
            ActiveName = activeName,
            ActiveDescription = activeDescription,
            ActiveCooldown = 2,
            ActiveTarget = activeEffect is OffenseHealEffect
                or OffenseGuardEffect
                    ? OffenseBattleTargetRule.Ally
                    : OffenseBattleTargetRule.Enemy,
            ActiveEffects = new[] { activeEffect },
            Modifiers = modifiers,
            SpeciesStats = new[]
            {
                (CharacterStatType.Attack, attack - 5),
                (CharacterStatType.Sales, sales - 5),
                (CharacterStatType.Research, research - 5),
                (CharacterStatType.Dexterity, dexterity - 5),
                (CharacterStatType.Toughness, toughness - 5)
            },
            BaseStats = new[]
            {
                (CharacterStatType.Attack, attack),
                (CharacterStatType.Sales, sales),
                (CharacterStatType.Research, research),
                (CharacterStatType.Dexterity, dexterity),
                (CharacterStatType.Toughness, toughness)
            },
            Personality = Personality(tag),
            CharacterName = characterName,
            MinimumMoney = minimumMoney,
            MaximumMoney = maximumMoney,
            SpeedValue = speed
        };
    }

    private static SpeciesNeedProfile Needs(
        float hunger,
        float thirst,
        MealDietClass diet,
        float social)
    {
        return new SpeciesNeedProfile
        {
            hungerRateMultiplier = hunger,
            thirstRateMultiplier = thirst,
            sleepRateMultiplier = 1f,
            hygieneRateMultiplier = 1f,
            socialNeedMultiplier = social,
            diet = diet
        };
    }

    private static SpeciesNeedProfile ConstructNeeds()
    {
        return new SpeciesNeedProfile
        {
            hungerRateMultiplier = 0f,
            thirstRateMultiplier = 0f,
            sleepRateMultiplier = 0.35f,
            hygieneRateMultiplier = 0.6f,
            socialNeedMultiplier = 0.3f,
            chargeRateMultiplier = 1f,
            integrityWearMultiplier = 1f,
            diet = MealDietClass.Vegan,
            metabolism = SpeciesMetabolismKind.Construct,
            treatment = SpeciesTreatmentKind.MechanicalMaintenance
        };
    }

    private static SpeciesEnvironmentProfile Env(
        float comfortMin,
        float comfortMax,
        float safeMin,
        float safeMax,
        float lethalMin,
        float lethalMax,
        float air)
    {
        return new SpeciesEnvironmentProfile
        {
            comfortMinimum = comfortMin,
            comfortMaximum = comfortMax,
            safeMinimum = safeMin,
            safeMaximum = safeMax,
            lethalMinimum = lethalMin,
            lethalMaximum = lethalMax,
            comfortableAirMinimum = air
        };
    }

    private static SpeciesPassiveDefinition Passive(
        string id,
        string name,
        string description,
        params string[] tags)
    {
        return new SpeciesPassiveDefinition
        {
            passiveId = id,
            displayName = name,
            description = description,
            mechanicTags = tags
        };
    }

    private static CharacterAiPersonality Personality(string tag)
    {
        CharacterAiPersonality value = new CharacterAiPersonality();
        switch (tag)
        {
            case "Beastkin":
                value.diligence = 1.25f;
                value.sociability = 1.4f;
                value.outdoorPreference = 1.5f;
                break;
            case "Demon":
                value.curiosity = 1.3f;
                value.shoppingInterest = 1.5f;
                value.riskTaking = 1.35f;
                break;
            case "Kobold":
                value.diligence = 1.4f;
                value.orderliness = 1.45f;
                break;
            case "Myconid":
                value.selfCare = 1.3f;
                value.patience = 1.4f;
                break;
            case "Harpy":
                value.curiosity = 1.4f;
                value.outdoorPreference = 1.6f;
                value.noveltySeeking = 1.35f;
                break;
            case "Golem":
                value.diligence = 1.5f;
                value.routineAdherence = 1.6f;
                value.orderliness = 1.6f;
                value.sociability = 0.5f;
                break;
        }

        return value;
    }

    private static string[] WorkIds(FacilityWorkType flags)
    {
        return EnumerateFlags(flags)
            .Select(type => WorkTypeCatalog.TryGet(
                    type,
                    out WorkTypeDefinition definition)
                ? definition.Id
                : type.ToString())
            .ToArray();
    }

    private static IEnumerable<FacilityWorkType> EnumerateFlags(
        FacilityWorkType flags)
    {
        foreach (FacilityWorkType value in Enum.GetValues(typeof(FacilityWorkType)))
        {
            if (value != FacilityWorkType.None && (flags & value) != 0)
            {
                yield return value;
            }
        }
    }

    private static CharacterSO LoadCharacter(string path) =>
        AssetDatabase.LoadAssetAtPath<CharacterSO>(path);

    private static void SetPrivate(
        UnityEngine.Object target,
        string property,
        int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Serialized field '{property}' was not found on {target.name}.");
        }

        field.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string segment in path.Split('/').Skip(1))
        {
            string next = $"{current}/{segment}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segment);
            }
            current = next;
        }
    }

    private sealed class SpeciesSpec
    {
        public int Id;
        public string Tag;
        public string Name;
        public string HomeFactionId = string.Empty;
        public SpeciesOwnerSelectionPolicy Policy;
        public string AnatomyProfileId;
        public SpeciesNeedProfile Needs;
        public SpeciesEnvironmentProfile Environment;
        public FacilityWorkType StrongWork;
        public FacilityWorkType WeakWork;
        public string[] RelationTags;
        public string[] DefenseTags;
        public string[] PreferredFacilities;
        public string[] DislikedEnvironments;
        public string ShortDescription = string.Empty;
        public string Description = string.Empty;
        public float StayDurationMultiplier = 1f;
        public float CrimeRiskMultiplier = 1f;
        public string IncidentId;
        public string IncidentName;
        public string IncidentDescription;
        public FacilityRole IncidentMitigatingRoles;
        public string[] IncidentTriggerTags;
        public SpeciesPassiveDefinition Passive;
        public string ActiveName;
        public string ActiveDescription;
        public int ActiveCooldown;
        public OffenseBattleTargetRule ActiveTarget;
        public OffenseCombatEffectModule[] ActiveEffects;
        public CharacterModelModifiers Modifiers = new CharacterModelModifiers();
        public (CharacterStatType, int)[] SpeciesStats =
            Array.Empty<(CharacterStatType, int)>();
        public (CharacterStatType, int)[] BaseStats =
            Array.Empty<(CharacterStatType, int)>();
        public CharacterAiPersonality Personality = new CharacterAiPersonality();
        public string CharacterName = string.Empty;
        public int MinimumMoney = 80;
        public int MaximumMoney = 250;
        public int SpeedValue = 4;
    }
}
#endif
