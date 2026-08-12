#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V26FounderTraitContentBuilder
{
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string TraitRoot = "Assets/Resources/SO/V26/Traits/Founder";
    private const string EffectRoot = "Assets/Resources/SO/V26/Effects/Definitions";
    private const string ConditionRoot = "Assets/Resources/SO/V26/Effects/Conditions";
    private const string SharedEffectEquipmentPath =
        "Assets/Resources/SO/Combat/Equipment/A15_PoweredHarness.asset";

    public static readonly int[] RetainedIds =
    {
        101, 102, 103, 104, 105, 106, 107, 108, 109,
        200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210,
        211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221,
        222, 223, 224, 225, 226, 227, 228, 229, 230, 235, 239, 245
    };

    public static readonly int[] RetiredIds =
    {
        231, 232, 233, 234, 236, 237, 238, 240, 241, 242, 243, 244, 246
    };

    private sealed class EffectSpec
    {
        public string Target;
        public float Value;
        public GameplayEffectOperation Operation;
        public string Condition;
    }

    private sealed class TraitSpec
    {
        public int Id;
        public string Key;
        public string Name;
        public string Description;
        public CharacterTraitPolarity Polarity;
        public CharacterTraitSelectionRarity Rarity;
        public string Family;
        public string[] Species = Array.Empty<string>();
        public EffectSpec[] Effects = Array.Empty<EffectSpec>();
    }

    private static readonly TraitSpec[] NewTraits =
    {
        T(247,"rough-hands","거친 손","현장 경험은 많지만 섬세한 의료 처치에는 약하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("fieldwork"),25),A(P("medicine"),-15)),
        T(248,"field-builder","현장 시공 감각","현장 구조는 빨리 읽지만 이론 학습은 더디다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("construction-engineering"),25),A(P("scholarship"),-15)),
        T(249,"fine-craft-sense","세공 감각","세공에는 능하지만 몸싸움 경험은 부족하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Uncommon,"aptitude-tradeoff", A(P("crafting"),25),A(P("melee-combat"),-15)),
        T(250,"palate","손맛","조리 감각이 좋지만 이론 공부는 덜 익숙하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("food-production"),20),A(P("scholarship"),-10)),
        T(251,"numbers-only","숫자만 믿음","학술적 판단은 빠르지만 사람의 사정에는 둔하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("scholarship"),15),A(P("social"),-25)),
        T(252,"careful-hand","신중한 손","의료 손놀림은 안정적이지만 거친 시공에는 약하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Uncommon,"aptitude-tradeoff", A(P("medicine"),20),A(P("construction-engineering"),-15)),
        T(253,"sociable","붙임성","사람과 쉽게 어울리지만 학술적 집중은 약하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("social"),20),A(P("scholarship"),-10)),
        T(254,"strength-first","완력 의존","근접 싸움에는 익숙하지만 정밀 제작은 서툴다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("melee-combat"),20),A(P("crafting"),-25)),
        T(255,"range-sense","거리 감각","원거리 조준은 좋지만 밀착전 경험은 부족하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Uncommon,"aptitude-tradeoff", A(P("ranged-combat"),25),A(P("melee-combat"),-15)),
        T(256,"foraging-eye","채집 눈썰미","현장 자원은 잘 찾지만 대인 업무는 불편해한다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("fieldwork"),15),A(P("social"),-10)),
        T(257,"tool-affinity","공구 친화","공구와 구조는 잘 다루지만 의료 도구에는 낯설다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("construction-engineering"),15),A(P("medicine"),-10)),
        T(258,"close-focus","눈앞 집중","손앞 제작에는 강하지만 먼 표적을 놓치기 쉽다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("crafting"),15),A(P("ranged-combat"),-10)),
        T(259,"talkative","수다쟁이","사교에는 익숙하지만 혼자 하는 현장 일은 덜 익숙하다.",CharacterTraitPolarity.Tradeoff,CharacterTraitSelectionRarity.Common,"aptitude-tradeoff", A(P("social"),15),A(P("fieldwork"),-20)),

        T(300,"possessed-inspiration","신들린 영감","다양한 완제품을 만들 때 오직 이 특성으로만 신화 품질을 탄생시킬 수 있다.",CharacterTraitPolarity.Extreme,CharacterTraitSelectionRarity.Exceptional,"extreme:craft"),
        T(301,"last-stand","사선 각성","죽음 직전에 통증을 잊고 폭발적인 전투력을 내지만 긴 탈진을 치른다.",CharacterTraitPolarity.Extreme,CharacterTraitSelectionRarity.Exceptional,"extreme:combat", M(GameplayEffectTargetIds.CombatPower,1.50f,"state:last-stand"),M(GameplayEffectTargetIds.MoveSpeed,1.20f,"state:last-stand"),M(GameplayEffectTargetIds.WorkSpeed,.50f,"state:last-stand-aftermath")),
        T(302,"forbidden-leap","금단의 도약","연구 진척을 걸고 금단의 돌파를 시도한다.",CharacterTraitPolarity.Extreme,CharacterTraitSelectionRarity.Exceptional,"extreme:research", M(GameplayEffectTargetIds.ResearchSpeed,.70f,"state:forbidden-leap-aftermath")),
        T(303,"miracle-surgery","기적의 집도","치명 수술에서 기적과 중증 합병증을 함께 건다.",CharacterTraitPolarity.Extreme,CharacterTraitSelectionRarity.Exceptional,"extreme:medicine", M(GameplayEffectTargetIds.WorkSpeed,.60f,"state:miracle-surgery-aftermath")),
        T(304,"golden-harvest","황금 수확","수확을 미뤄 대수확이나 큰 손실을 노린다.",CharacterTraitPolarity.Extreme,CharacterTraitSelectionRarity.Exceptional,"extreme:harvest", M("harvest:yield",2.50f,"state:golden-harvest-jackpot"),M("harvest:seed-yield",1.50f,"state:golden-harvest-jackpot")),
        T(305,"production-limit-break","한계 돌파","긴급 생산에서 사고와 탈진을 감수하고 한계를 넘는다.",CharacterTraitPolarity.Extreme,CharacterTraitSelectionRarity.Exceptional,"extreme:production", M(GameplayEffectTargetIds.WorkSpeed,1.50f,"state:production-limit-break"),M(GameplayEffectTargetIds.AccidentChance,1.50f,"state:production-limit-break"),M(GameplayEffectTargetIds.FatigueRate,2f,"state:production-limit-break"),M(GameplayEffectTargetIds.WorkSpeed,.65f,"state:production-limit-break-aftermath")),
        T(306,"arcane-overcharge","마력 과충전","생명과 장비 내구도를 태워 마력을 폭주시킨다.",CharacterTraitPolarity.Extreme,CharacterTraitSelectionRarity.Exceptional,"extreme:arcane", M("arcane:power",1.60f,"state:arcane-overcharge"),M("arcane:mana-recovery",.50f,"state:arcane-overcharge-aftermath")),

        T(400,"fieldwork-aptitude","들일 소질","현장 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:fieldwork",A(P("fieldwork"),15)),
        T(401,"construction-aptitude","시공 소질","건설·공학 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:construction",A(P("construction-engineering"),15)),
        T(402,"crafting-aptitude","제작 소질","제작 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:crafting",A(P("crafting"),15)),
        T(403,"cooking-aptitude","조리 소질","식량 생산 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:food",A(P("food-production"),15)),
        T(404,"scholarship-aptitude","학술 소질","학술 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:scholarship",A(P("scholarship"),15)),
        T(405,"medicine-aptitude","의료 소질","의료 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:medicine",A(P("medicine"),15)),
        T(406,"social-aptitude","사교 소질","사교 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:social",A(P("social"),15)),
        T(407,"melee-aptitude","근접 소질","근접 전투 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:melee",A(P("melee-combat"),15)),
        T(408,"ranged-aptitude","사격 소질","원거리 전투 숙련의 출발이 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"aptitude:ranged",A(P("ranged-combat"),15)),
        T(409,"diligent","부지런함","대부분의 일을 조금 더 빠르게 끝낸다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"simple:work",M(GameplayEffectTargetIds.WorkSpeed,1.05f)),
        T(410,"agile","민첩함","이동이 조금 빠르다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"simple:move",M(GameplayEffectTargetIds.MoveSpeed,1.05f)),
        T(411,"prudent","신중함","작업 사고를 덜 낸다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"simple:safety",M(GameplayEffectTargetIds.AccidentChance,.90f)),
        T(412,"patient","인내심","대기와 지연을 더 오래 견딘다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"simple:patience",M(GameplayEffectTargetIds.WaitPatience,1.20f)),
        T(413,"light-eater","소식가","식량 소비가 적다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"simple:consumption",M(GameplayEffectTargetIds.Consumption,.90f)),
        T(414,"focused","집중력","연구를 조금 더 빠르게 진행한다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"simple:research",M(GameplayEffectTargetIds.ResearchSpeed,1.05f)),
        T(415,"brave-heart","강심장","전투에서 조금 더 강하다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Uncommon,"simple:combat",M(GameplayEffectTargetIds.CombatPower,1.05f)),
        T(416,"quick-study","빠른 습득","승인된 작업 경험을 더 빨리 얻는다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Rare,"simple:learning",M(GameplayEffectTargetIds.EarnedWorkExperience,1.10f)),
        T(417,"climate-adapted","기후 적응","냉기와 열기 노출이 덜 쌓인다.",CharacterTraitPolarity.Advantage,CharacterTraitSelectionRarity.Rare,"simple:climate",M(GameplayEffectTargetIds.ColdExposure,.90f),M(GameplayEffectTargetIds.HeatExposure,.90f)),

        T(500,"fieldwork-insensitive","들일 둔감","현장 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:fieldwork",A(P("fieldwork"),-15)),
        T(501,"construction-clumsy","시공 서툼","건설·공학 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:construction",A(P("construction-engineering"),-15)),
        T(502,"crafting-clumsy","제작 서툼","제작 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:crafting",A(P("crafting"),-15)),
        T(503,"cooking-clumsy","조리 서툼","식량 생산 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:food",A(P("food-production"),-15)),
        T(504,"scholarship-insensitive","학문 둔감","학술 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:scholarship",A(P("scholarship"),-15)),
        T(505,"medicine-clumsy","의료 서툼","의료 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:medicine",A(P("medicine"),-15)),
        T(506,"shy","낯가림","사교 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:social",A(P("social"),-15)),
        T(507,"melee-clumsy","몸싸움 서툼","근접 전투 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:melee",A(P("melee-combat"),-15)),
        T(508,"ranged-clumsy","사격 서툼","원거리 전투 숙련의 출발이 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"aptitude:ranged",A(P("ranged-combat"),-15)),
        T(509,"lazy","게으름","작업 속도가 조금 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:work",M(GameplayEffectTargetIds.WorkSpeed,.95f)),
        T(510,"slow-footed","둔한 발","이동 속도가 조금 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:move",M(GameplayEffectTargetIds.MoveSpeed,.95f)),
        T(511,"careless","부주의","작업 사고를 더 자주 낸다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:safety",M(GameplayEffectTargetIds.AccidentChance,1.12f)),
        T(512,"poor-waiter","기다림에 약함","대기와 지연을 잘 견디지 못한다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:patience",M(GameplayEffectTargetIds.WaitPatience,.80f)),
        T(513,"overeater","과식","식량을 더 많이 소비한다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:consumption",M(GameplayEffectTargetIds.Consumption,1.10f)),
        T(514,"distracted","산만함","연구 속도가 조금 느리다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:research",M(GameplayEffectTargetIds.ResearchSpeed,.95f)),
        T(515,"combat-passive","전투 소극적","전투에서 힘을 온전히 쓰지 못한다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:combat",M(GameplayEffectTargetIds.CombatPower,.95f)),
        T(516,"slow-study","느린 습득","승인된 작업 경험을 더 천천히 얻는다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:learning",M(GameplayEffectTargetIds.EarnedWorkExperience,.90f)),
        T(517,"cold-vulnerable","추위 취약","냉기 노출이 더 빨리 쌓인다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:cold",M(GameplayEffectTargetIds.ColdExposure,1.15f)),
        T(518,"heat-vulnerable","더위 취약","열기 노출이 더 빨리 쌓인다.",CharacterTraitPolarity.Negative,CharacterTraitSelectionRarity.Common,"simple:heat",M(GameplayEffectTargetIds.HeatExposure,1.15f))
    };

    private static readonly Dictionary<string, GameplayEffectDefinitionSO> EffectCache =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GameplayEffectConditionDefinitionSO> ConditionCache =
        new(StringComparer.Ordinal);

    [MenuItem("DungeonStory/V26/Build Founder Trait Content (100)")]
    public static void Build()
    {
        EffectCache.Clear();
        ConditionCache.Clear();
        EnsureFolders();
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("The root domain catalog is missing.");

        Dictionary<int, CharacterTraitSO> allTraits = AssetDatabase
            .FindAssets("t:CharacterTraitSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterTraitSO>)
            .Where(value => value != null)
            .GroupBy(value => value.id)
            .ToDictionary(group => group.Key, group => group.First());
        List<CharacterTraitSO> retained = RetainedIds
            .Select(id => allTraits.TryGetValue(id, out CharacterTraitSO value)
                ? value
                : throw new InvalidOperationException($"Retained trait {id} is missing."))
            .ToList();
        foreach (CharacterTraitSO trait in retained) ConfigureRetained(trait);

        List<CharacterTraitSO> created = NewTraits.Select(CreateNew).ToList();
        ConfigureSharedEffectEquipmentSlice();
        CharacterTraitSO[] founderTraits = retained.Concat(created)
            .OrderBy(value => value.id)
            .ToArray();
        if (founderTraits.Length != 100)
            throw new InvalidOperationException(
                $"Expected exactly 100 selectable founder traits, found {founderTraits.Length}.");
        if (founderTraits.GroupBy(value => value.id).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Founder trait ids are duplicated.");
        if (founderTraits.Any(value => RetiredIds.Contains(value.id)))
            throw new InvalidOperationException("A retired founder trait remains selectable.");

        List<string> errors = founderTraits.SelectMany(value => value.ValidateDefinition())
            .Concat(EffectCache.Values.SelectMany(value => value.ValidateDefinition()))
            .ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" | ", errors));

        HashSet<int> managedIds = new(RetainedIds.Concat(RetiredIds)
            .Concat(NewTraits.Select(value => value.Id)));
        catalog.SetDefinitions(catalog.Definitions
            .Where(value => value != null
                && value is not GameplayEffectDefinitionSO
                && value is not GameplayEffectConditionDefinitionSO
                && !(value is CharacterTraitSO trait && managedIds.Contains(trait.id)))
            .Concat(founderTraits)
            .Concat(EffectCache.Values)
            .Concat(ConditionCache.Values));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"V26_FOUNDER_TRAITS=PASS; selectable={founderTraits.Length}; retained={retained.Count}; "
            + $"new={created.Count}; retired={RetiredIds.Length}; effects={EffectCache.Count}; "
            + $"conditions={ConditionCache.Count}");
    }

    private static void ConfigureRetained(CharacterTraitSO trait)
    {
        trait.polarity = RetainedPolarity(trait.id);
        trait.effects = RetainedEffects(trait.id)
            .Select((spec, index) => Bind(trait.id, index, spec))
            .ToList();
        trait.identityRules = MigrateIdentityRules(trait);
        AddRetainedIdentityRules(trait);
        if (trait.id == 239)
        {
            trait.traitName = "양심 결여";
            trait.description = "적대자 살해와 처형에 죄책감을 느끼지 않지만 목격자와 공동체의 반발은 그대로 남는다.";
        }

        trait.modifiers = new CharacterModelModifiers();
        trait.earnedWorkExperienceMultiplier = 1f;
        trait.behaviorPreferences = new List<CharacterTraitBehaviorPreference>();
        trait.moodReactions = new List<CharacterTraitMoodReaction>();
        trait.eventWeights = new List<CharacterTraitEventWeight>();
        EditorUtility.SetDirty(trait);
    }

    private static CharacterTraitSO CreateNew(TraitSpec spec)
    {
        string path = $"{TraitRoot}/Trait_{spec.Id}_{spec.Key}.asset";
        CharacterTraitSO trait = AssetDatabase.LoadAssetAtPath<CharacterTraitSO>(path);
        if (trait == null)
        {
            trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
            AssetDatabase.CreateAsset(trait, path);
        }
        trait.id = spec.Id;
        trait.traitName = spec.Name;
        trait.description = spec.Description;
        trait.polarity = spec.Polarity;
        trait.selectionRarity = spec.Rarity;
        trait.selectionFamilyId = spec.Family;
        trait.eligibleSpeciesTags = spec.Species.ToList();
        trait.incompatibilityGroups = new List<string>();
        trait.modifiers = new CharacterModelModifiers();
        trait.combatAbilities = new CharacterCombatAbilityCollection();
        trait.environmentalProtection = new ThermalProtectionProfile();
        trait.earnedWorkExperienceMultiplier = 1f;
        trait.effects = spec.Effects
            .Select((effect, index) => Bind(spec.Id, index, effect))
            .ToList();
        trait.identityRules = ExtremeRules(spec.Id);
        trait.behaviorPreferences = new List<CharacterTraitBehaviorPreference>();
        trait.moodReactions = new List<CharacterTraitMoodReaction>();
        trait.eventWeights = new List<CharacterTraitEventWeight>();
        EditorUtility.SetDirty(trait);
        return trait;
    }

    private static void ConfigureSharedEffectEquipmentSlice()
    {
        CombatEquipmentDefinitionSO equipment = AssetDatabase
            .LoadAssetAtPath<CombatEquipmentDefinitionSO>(
                SharedEffectEquipmentPath)
            ?? throw new InvalidOperationException(
                "The powered harness shared-effect slice equipment is missing.");
        equipment.ConfigureGameplayEffects(new[]
        {
            new GameplayEffectBinding
            {
                bindingId = "equipment:powered-harness:work-speed",
                definition = ResolveEffect(
                    GameplayEffectTargetIds.WorkSpeed,
                    GameplayEffectOperation.Multiply),
                value = 1.05f
            }
        });
        EditorUtility.SetDirty(equipment);
    }

    private static EffectSpec[] RetainedEffects(int id) => id switch
    {
        101 => new[] { M(GameplayEffectTargetIds.Consumption,1.25f),M(GameplayEffectTargetIds.WorkSpeed,1.08f,"state:sated") },
        102 => new[] { M(GameplayEffectTargetIds.Consumption,.88f),M(GameplayEffectTargetIds.WorkSpeed,.97f),M(GameplayEffectTargetIds.AccidentChance,.95f) },
        103 => new[] { A(P("social"),15),M(GameplayEffectTargetIds.Spending,1.40f) },
        104 => new[] { M(GameplayEffectTargetIds.MoveSpeed,1.10f),M(GameplayEffectTargetIds.WorkSpeed,1.04f),M(GameplayEffectTargetIds.AccidentChance,1.15f) },
        105 => new[] { M(GameplayEffectTargetIds.CombatPower,.82f),M(GameplayEffectTargetIds.AccidentChance,.80f),M(GameplayEffectTargetIds.CrowdSensitivity,1.20f) },
        106 => new[] { M(GameplayEffectTargetIds.CombatPower,1.18f),M(GameplayEffectTargetIds.AccidentChance,1.12f) },
        107 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.15f,"work:clean-maintenance"),M(GameplayEffectTargetIds.AccidentChance,.85f,"work:contamination") },
        108 => new[] { M(GameplayEffectTargetIds.ResearchSpeed,1.18f),M(GameplayEffectTargetIds.WorkSpeed,.97f,"work:not-research"),M(GameplayEffectTargetIds.AccidentChance,.95f,"work:research") },
        109 => new[] { A("environment:comfort-minimum-offset",-4),A("environment:safe-minimum-offset",-2),M(GameplayEffectTargetIds.ColdExposure,.60f) },
        200 => new[] { M(GameplayEffectTargetIds.FatigueRate,.85f,"work:long-shift"),M(GameplayEffectTargetIds.WorkSpeed,1.05f,"work:long-shift") },
        201 => new[] { M("character:alarm-response-delay",.50f),M("character:sleep-recovery",.80f,"room:noise") },
        202 => new[] { M(GameplayEffectTargetIds.MoveSpeed,1.04f,"terrain:rough"),M(GameplayEffectTargetIds.AccidentChance,.80f,"accident:fall-slip") },
        203 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.03f,"work:precision"),M(GameplayEffectTargetIds.CombatPower,.96f),M("damage:blunt-taken",1.12f) },
        204 => new[] { M("character:pain-work-penalty",.75f),M(GameplayEffectTargetIds.CombatPower,1.03f,"state:pain") },
        205 => new[] { M("character:danger-detection",1.15f),M("food:spoilage-detection",1.25f),M(GameplayEffectTargetIds.CrowdSensitivity,1.10f) },
        206 => new[] { M(GameplayEffectTargetIds.HaulCapacity,1.12f),M(GameplayEffectTargetIds.CombatPower,1.04f),M(GameplayEffectTargetIds.MoveSpeed,.97f) },
        207 => new[]
        {
            M(GameplayEffectTargetIds.RecoverySpeed,1.15f),
            M(GameplayEffectTargetIds.DiseaseResistance,1.10f),
            M(GameplayEffectTargetIds.DiseaseRecoverySpeed,1.10f),
            M(GameplayEffectTargetIds.ImmunityGain,1.10f),
            M(GameplayEffectTargetIds.ImmunityRetention,1.10f),
            M("medical:aftermath-duration",.85f)
        },
        208 => new[] { M(GameplayEffectTargetIds.Consumption,.88f),M(GameplayEffectTargetIds.MoveSpeed,.98f),M(GameplayEffectTargetIds.ColdExposure,1.15f) },
        209 => new[] { M(GameplayEffectTargetIds.ColdExposure,.90f),M(GameplayEffectTargetIds.CombatPower,1.08f,"state:insulted") },
        210 => new[] { M(GameplayEffectTargetIds.Consumption,1.05f),M(GameplayEffectTargetIds.WorkSpeed,1.05f,"state:sweet-fed") },
        211 => new[] { M(GameplayEffectTargetIds.FoodPoisoningChance,.70f),M(GameplayEffectTargetIds.AccidentChance,1.03f,"work:contaminated-food") },
        212 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.03f,"temperature:comfortable"),M(GameplayEffectTargetIds.WorkSpeed,.90f,"temperature:uncomfortable"),M(GameplayEffectTargetIds.ColdExposure,1.20f,"temperature:cold"),M(GameplayEffectTargetIds.HeatExposure,1.20f,"temperature:hot") },
        213 => new[] { M(GameplayEffectTargetIds.Consumption,0f,"state:ritual-fasting"),M(GameplayEffectTargetIds.Consumption,1.15f,"state:ritual-fast-ended") },
        214 => new[] { M(GameplayEffectTargetIds.Consumption,1.03f) },
        215 => new[] { M(GameplayEffectTargetIds.AccidentChance,.90f,"work:emergency") },
        216 => new[] { M(GameplayEffectTargetIds.AccidentChance,.85f,"work:retry-after-failure") },
        217 => new[] { M(GameplayEffectTargetIds.NegativeMoodDuration,.75f) },
        218 => new[] { M(GameplayEffectTargetIds.RelationshipRecovery,.25f,"relationship:negative") },
        219 => new[] { M(GameplayEffectTargetIds.RelationshipRecovery,1.50f,"relationship:first-apology") },
        220 => new[] { M(GameplayEffectTargetIds.AccidentChance,.85f,"state:emergency-stocked") },
        221 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.08f,"work:dangerous"),M(GameplayEffectTargetIds.AccidentChance,1.08f) },
        222 => new[] { M(GameplayEffectTargetIds.CrowdSensitivity,.80f) },
        223 => new[] { M(GameplayEffectTargetIds.WorkSpeed,.97f),M(GameplayEffectTargetIds.AccidentChance,.88f) },
        224 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.08f,"work:substitute-material"),M(GameplayEffectTargetIds.WorkSpeed,1.03f),M(GameplayEffectTargetIds.AccidentChance,1.05f) },
        225 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.04f,"work:on-schedule") },
        226 => new[] { M(GameplayEffectTargetIds.WorkSpeed,.98f),M("character:mentee-xp",1.20f,"work:mentoring") },
        227 => new[] { M(GameplayEffectTargetIds.SalvageYield,1.08f) },
        228 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.08f,"shift:night"),M(GameplayEffectTargetIds.WorkSpeed,.95f,"shift:day") },
        229 => new[] { A(GameplayEffectTargetIds.CraftQualityScore,4f),M(GameplayEffectTargetIds.WorkSpeed,.98f,"work:craft-finished") },
        230 => new[] { M(GameplayEffectTargetIds.EarnedWorkExperience,1.30f) },
        235 => new[] { M("social:negotiation",1.08f,"state:formal-status") },
        239 => new[] { M(GameplayEffectTargetIds.CombatPower,1.03f),M("character:combat-stress",0f,"event:hostile-execution") },
        245 => new[] { M(GameplayEffectTargetIds.WorkSpeed,1.20f,"work:clean"),M(GameplayEffectTargetIds.WorkSpeed,.99f,"work:not-clean") },
        _ => throw new InvalidOperationException($"Retained trait {id} has no effect manifest.")
    };

    private static CharacterTraitPolarity RetainedPolarity(int id) => id switch
    {
        107 or 109 or 200 or 202 or 207 or 215 or 217 or 219 or 227 or 230 =>
            CharacterTraitPolarity.Advantage,
        218 => CharacterTraitPolarity.Negative,
        213 or 214 or 222 => CharacterTraitPolarity.Quirk,
        _ => CharacterTraitPolarity.Tradeoff
    };

    private static List<CharacterIdentityRule> MigrateIdentityRules(CharacterTraitSO trait)
    {
        List<CharacterIdentityRule> rules = new();
        int serial = 0;
        foreach (CharacterTraitBehaviorPreference value in trait.behaviorPreferences
                     ?? new List<CharacterTraitBehaviorPreference>())
        {
            if (value == null || !value.IsValid) continue;
            rules.Add(new BehaviorUtilityRule
            {
                ruleId = $"behavior:{serial++}:{value.behaviorTag}",
                behaviorTag = value.behaviorTag,
                utilityDelta = value.utilityDelta
            });
        }
        serial = 0;
        foreach (CharacterTraitMoodReaction value in trait.moodReactions
                     ?? new List<CharacterTraitMoodReaction>())
        {
            if (value == null || !value.IsValid) continue;
            rules.Add(new EventMoodRule
            {
                ruleId = $"mood:{serial++}:{value.triggerTag}",
                eventId = value.triggerTag,
                moodDelta = value.moodDelta,
                durationDays = value.durationDays
            });
        }
        serial = 0;
        foreach (CharacterTraitEventWeight value in trait.eventWeights
                     ?? new List<CharacterTraitEventWeight>())
        {
            if (value == null || !value.IsValid) continue;
            rules.Add(new IncidentWeightRule
            {
                ruleId = $"incident:{serial++}:{value.eventCategoryId}",
                incidentId = value.eventCategoryId,
                multiplier = value.multiplier
            });
        }
        return rules;
    }

    private static void AddRetainedIdentityRules(CharacterTraitSO trait)
    {
        switch (trait.id)
        {
            case 101:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:meal-priority",behaviorTag="work:eat",utilityDelta=.8f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:large-meal",needId="need:large-meal",satisfiedEventId="food:sated",deprivedEventId="food:meal-missed",deprivationDays=1,deprivedMoodDelta=-5,satisfiedMoodDelta=3,moodDurationDays=1 });
                break;
            case 102:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:avoid-luxury",behaviorTag="consume:luxury",utilityDelta=-.7f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:stockpile-met",eventId="stockpile:target-met",moodDelta=2,durationDays=1 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:resource-wasted",eventId="resource:wasted",moodDelta=-3,durationDays=2 });
                break;
            case 103:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:prefer-luxury",behaviorTag="consume:luxury",utilityDelta=.65f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:luxury-standard",needId="need:luxury-standard",satisfiedEventId="living:luxury-satisfied",deprivedEventId="living:basic-only",deprivationDays=3,deprivedMoodDelta=-3,satisfiedMoodDelta=2,moodDurationDays=2 });
                break;
            case 104:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:resolve-now",behaviorTag="work:immediate",utilityDelta=.65f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:wait-exceeded",eventId="wait:exceeded",moodDelta=-3,durationDays=1 });
                break;
            case 105:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:avoid-danger",behaviorTag="work:dangerous",utilityDelta=-.8f });
                trait.identityRules.Add(new AutonomousWorkRestrictionRule { ruleId="restriction:dangerous-work",actionTag="work:dangerous",requiredConditionId="condition:no-safe-alternative",failureReason="소심함 때문에 자율 위험 작업을 피함" });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:forced-danger",eventId="danger:directly-assigned",moodDelta=-4,durationDays=2 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:safe-return",eventId="danger:safe-return",moodDelta=2,durationDays=1 });
                break;
            case 106:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:seek-combat",behaviorTag="work:combat-training",utilityDelta=.65f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:combat-action",needId="need:combat-action",satisfiedEventId="combat:victory",deprivedEventId="combat:inactive-five-days",deprivationDays=5,deprivedMoodDelta=-2,satisfiedMoodDelta=3,moodDurationDays=2 });
                break;
            case 107:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:clean-dirt",behaviorTag="work:clean",utilityDelta=.65f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:clean-complete",eventId="room:cleaned",moodDelta=2,durationDays=1 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:severe-filth",eventId="room:dirty",moodDelta=-3,durationDays=1 });
                break;
            case 108:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:prefer-research",behaviorTag="work:research",utilityDelta=.75f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:research-access",needId="need:research-access",satisfiedEventId="research:completed",deprivedEventId="research:no-access",deprivationDays=3,deprivedMoodDelta=-2,satisfiedMoodDelta=3,moodDurationDays=2 });
                break;
            case 109:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:prefer-cold-work",behaviorTag="work:cold-zone",utilityDelta=.55f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:safe-cold-work",eventId="temperature:safe-cold-work-complete",moodDelta=2,durationDays=1 });
                break;
            case 200:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:long-shift",behaviorTag="work:long-shift",utilityDelta=.45f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:well-rested",eventId="rest:sufficient",moodDelta=2,durationDays=1 });
                break;
            case 201:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:wake-on-alert",behaviorTag="alert:minor",utilityDelta=.9f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:noisy-sleep",eventId="sleep:noisy",moodDelta=-3,durationDays=1 });
                break;
            case 202:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:rough-rescue",behaviorTag="work:rough-terrain-rescue",utilityDelta=.55f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:rough-crossing",eventId="terrain:rough-crossed-safely",moodDelta=2,durationDays=1 });
                break;
            case 203:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:precision-craft",behaviorTag="work:precision",utilityDelta=.55f });
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:avoid-heavy-haul",behaviorTag="work:heavy-haul",utilityDelta=-.55f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:blunt-injury",eventId="injury:blunt",moodDelta=-3,durationDays=2 });
                break;
            case 204:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:persist-through-pain",behaviorTag="work:while-in-pain",utilityDelta=.55f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:clinic-memory",eventId="medical:entered-clinic",moodDelta=-2,durationDays=1 });
                break;
            case 205:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:inspect-scout",behaviorTag="work:inspect-scout",utilityDelta=.65f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:rot-stench",eventId="environment:rot-stench",moodDelta=-3,durationDays=1 });
                break;
            case 206:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:heavy-haul",behaviorTag="work:heavy-haul",utilityDelta=.55f });
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:subdue",behaviorTag="work:subdue",utilityDelta=.45f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:cramped",eventId="room:cramped-long",moodDelta=-3,durationDays=1 });
                break;
            case 207:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:follow-treatment",behaviorTag="medical:rest-treatment",utilityDelta=.55f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:recovery-stage",eventId="medical:severity-reduced",moodDelta=3,durationDays=2 });
                break;
            case 208:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:delay-meal",behaviorTag="work:eat",utilityDelta=-.35f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:cold-exposure",eventId="temperature:cold-long",moodDelta=-3,durationDays=1 });
                break;
            case 209:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:answer-insult",behaviorTag="social:answer-insult",utilityDelta=.8f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:insulted",eventId="social:insulted",moodDelta=-4,durationDays=1 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:answered-insult",eventId="social:insult-answered",moodDelta=3,durationDays=1 });
                break;
            case 210:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:prefer-sweets",behaviorTag="food:sweet",utilityDelta=.75f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:sweets",needId="need:sweets",satisfiedEventId="food:sweet",deprivedEventId="food:no-sweets",deprivationDays=3,deprivedMoodDelta=-2,satisfiedMoodDelta=4,moodDurationDays=1 });
                break;
            case 211:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:try-unfamiliar-food",behaviorTag="food:unfamiliar",utilityDelta=.45f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:new-meal",eventId="food:new-meal",moodDelta=2,durationDays=1 });
                break;
            case 212:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:conditioned-room",behaviorTag="room:temperature-controlled",utilityDelta=.7f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:uncomfortable-temperature",eventId="temperature:uncomfortable-long",moodDelta=-4,durationDays=1 });
                break;
            case 213:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:ritual-fast",behaviorTag="ritual:fast",utilityDelta=.7f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:ritual-fast",needId="need:ritual-fast",satisfiedEventId="ritual:fast-completed",deprivedEventId="ritual:fast-broken",deprivationDays=1,deprivedMoodDelta=-3,satisfiedMoodDelta=3,moodDurationDays=2 });
                break;
            case 214:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:prefer-salted",behaviorTag="food:salted",utilityDelta=.7f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:salt",needId="need:salt",satisfiedEventId="food:salted",deprivedEventId="food:bland-streak",deprivationDays=3,deprivedMoodDelta=-2,satisfiedMoodDelta=3,moodDurationDays=1 });
                break;
            case 215:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:crisis-rescue",behaviorTag="work:crisis-rescue",utilityDelta=.65f });
                trait.identityRules.Add(new MoodTransformRule { ruleId="mood:panic-quarter",eventId="event:panic",multiplier=.25f });
                break;
            case 216:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:prevent-repeat-failure",behaviorTag="work:prevent-repeat-failure",utilityDelta=.65f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:work-failure",eventId="work:failed",moodDelta=-4,durationDays=5 });
                break;
            case 217:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:encourage",behaviorTag="social:encourage",utilityDelta=.55f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:small-success",eventId="work:small-success",moodDelta=3,durationDays=1 });
                break;
            case 218:
                trait.identityRules.Add(new RelationshipMemoryRule { ruleId="memory:grudge",eventId="social:betrayal-or-assault",relationshipDelta=-5,dailyDecay=.25f,apologyCanClear=true,restitutionRequired=true });
                break;
            case 219:
                trait.identityRules.Add(new RelationshipMemoryRule { ruleId="memory:forgiving",eventId="social:sincere-apology",relationshipDelta=3,dailyDecay=1.5f,apologyCanClear=true,restitutionRequired=false });
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:reconcile",behaviorTag="social:reconcile",utilityDelta=.65f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:reconciled",eventId="social:sincere-apology",moodDelta=3,durationDays=2 });
                break;
            case 220:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:check-emergency-stock",behaviorTag="work:emergency-check",utilityDelta=.75f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:emergency-readiness",needId="need:emergency-readiness",satisfiedEventId="stockpile:emergency-ready",deprivedEventId="stockpile:emergency-shortage",deprivationDays=1,deprivedMoodDelta=-4,satisfiedMoodDelta=2,moodDurationDays=1 });
                break;
            case 221:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:seek-danger",behaviorTag="work:dangerous",utilityDelta=.75f });
                trait.identityRules.Add(new PersistentNeedRule { ruleId="need:stimulation",needId="need:stimulation",satisfiedEventId="danger:success",deprivedEventId="work:repetitive-three-days",deprivationDays=3,deprivedMoodDelta=-2,satisfiedMoodDelta=4,moodDurationDays=2 });
                break;
            case 222:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:rest-alone",behaviorTag="rest:private",utilityDelta=.75f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:private-rest",eventId="rest:private",moodDelta=3,durationDays=1 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:public-question",eventId="social:public-question",moodDelta=-3,durationDays=1 });
                break;
            case 223:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:inspect-before-release",behaviorTag="work:inspect",utilityDelta=.75f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:defect-found",eventId="product:defect-found",moodDelta=-3,durationDays=1 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:defect-caught",eventId="product:defect-caught-before-release",moodDelta=3,durationDays=1 });
                break;
            case 224:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:prototype",behaviorTag="work:prototype",utilityDelta=.7f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:strict-procedure",eventId="work:strict-procedure",moodDelta=-2,durationDays=1 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:substitute-success",eventId="work:substitute-success",moodDelta=3,durationDays=1 });
                break;
            case 225:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:follow-schedule",behaviorTag="work:on-schedule",utilityDelta=.7f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:sudden-reassignment",eventId="schedule:sudden-reassignment",moodDelta=-3,durationDays=1 });
                break;
            case 226:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:mentor-novice",behaviorTag="work:mentoring",utilityDelta=.75f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:mentee-ranked-up",eventId="mentee:rank-up",moodDelta=3,durationDays=2 });
                break;
            case 227:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:salvage-before-discard",behaviorTag="work:salvage",utilityDelta=.75f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:salvageable-discarded",eventId="resource:salvageable-discarded",moodDelta=-2,durationDays=1 });
                break;
            case 228:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:night-shift",behaviorTag="shift:night",utilityDelta=.75f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:forced-day-shift",eventId="shift:forced-day",moodDelta=-2,durationDays=1 });
                break;
            case 229:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:quality-policy",behaviorTag="work:quality-first",utilityDelta=.75f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:low-quality-product",eventId="product:quality-low",moodDelta=-4,durationDays=2 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:masterwork-product",eventId="product:quality-masterwork",moodDelta=3,durationDays=2 });
                break;
            case 230:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:new-process",behaviorTag="work:new-process",utilityDelta=.75f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:first-process-success",eventId="work:first-process-success",moodDelta=3,durationDays=1 });
                break;
            case 235:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:formal-etiquette",behaviorTag="social:formal-etiquette",utilityDelta=.7f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:publicly-ignored",eventId="status:publicly-ignored",moodDelta=-4,durationDays=2 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:formal-recognition",eventId="status:recognized",moodDelta=3,durationDays=2 });
                break;
            case 239:
                trait.identityRules.Add(new MoodImmunityRule { ruleId="immunity:hostile-kill-guilt",eventId="mood:hostile-kill-guilt" });
                trait.identityRules.Add(new MoodImmunityRule { ruleId="immunity:hostile-execution-guilt",eventId="mood:hostile-execution-guilt" });
                trait.identityRules.Add(new MoodImmunityRule { ruleId="immunity:butchery-guilt",eventId="mood:butchery-guilt" });
                break;
            case 245:
                trait.identityRules.Add(new BehaviorUtilityRule { ruleId="behavior:clean-now",behaviorTag="work:clean",utilityDelta=.95f });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:dirty-room",eventId="room:dirty",moodDelta=-4,durationDays=1 });
                trait.identityRules.Add(new EventMoodRule { ruleId="mood:cleaned-room",eventId="room:cleaned",moodDelta=2,durationDays=1 });
                trait.identityRules.Add(new PostActionConsequenceRule { ruleId="post:defer-cleaning",actionTag="order:defer-cleaning",directOrdersOnly=true,moodDelta=-2,stressDelta=3,durationDays=1 });
                break;
        }
    }

    private static List<CharacterIdentityRule> ExtremeRules(int id) => id switch
    {
        300 => new List<CharacterIdentityRule> { new ExtremeCraftInspirationRule { ruleId="extreme:mythic-inspiration",priority=100 } },
        301 => new List<CharacterIdentityRule> { new LastStandRule { ruleId="extreme:last-stand",priority=100 } },
        302 => new List<CharacterIdentityRule> { new ForbiddenResearchLeapRule { ruleId="extreme:forbidden-leap",priority=100 } },
        303 => new List<CharacterIdentityRule> { new MiracleSurgeryRule { ruleId="extreme:miracle-surgery",priority=100 } },
        304 => new List<CharacterIdentityRule> { new GoldenHarvestRule { ruleId="extreme:golden-harvest",priority=100 } },
        305 => new List<CharacterIdentityRule> { new ProductionLimitBreakRule { ruleId="extreme:production-limit-break",priority=100 } },
        306 => new List<CharacterIdentityRule> { new ArcaneOverchargeRule { ruleId="extreme:arcane-overcharge",priority=100 } },
        _ => new List<CharacterIdentityRule>()
    };

    private static GameplayEffectBinding Bind(int traitId, int index, EffectSpec spec) =>
        new()
        {
            bindingId = $"trait:{traitId}:effect:{index}",
            definition = ResolveEffect(spec.Target, spec.Operation),
            value = spec.Value,
            condition = string.IsNullOrWhiteSpace(spec.Condition)
                ? null
                : ResolveCondition(spec.Condition)
        };

    internal static GameplayEffectDefinitionSO ResolveEffect(
        string target,
        GameplayEffectOperation operation)
    {
        string effectId = $"effect:{target}:{operation.ToString().ToLowerInvariant()}";
        if (EffectCache.TryGetValue(effectId, out GameplayEffectDefinitionSO cached))
            return cached;
        string path = $"{EffectRoot}/{Safe(effectId)}.asset";
        GameplayEffectDefinitionSO value = AssetDatabase
            .LoadAssetAtPath<GameplayEffectDefinitionSO>(path);
        if (value != null && MonoScript.FromScriptableObject(value) == null)
        {
            AssetDatabase.DeleteAsset(path);
            value = null;
        }
        if (value == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            value = ScriptableObject.CreateInstance<GameplayEffectDefinitionSO>();
            AssetDatabase.CreateAsset(value, path);
        }
        value.Configure(
            StableNumericId(effectId),
            effectId,
            target,
            operation,
            Phase(operation),
            GameplayEffectSourceKind.All,
            GameplayEffectStackingPolicy.StackAll,
            operation is GameplayEffectOperation.Multiply
                ? 0f
                : float.MinValue,
            operation is GameplayEffectOperation.Multiply
                ? 10f
                : float.MaxValue);
        EditorUtility.SetDirty(value);
        EffectCache.Add(effectId, value);
        return value;
    }

    private static GameplayEffectConditionDefinitionSO ResolveCondition(string conditionId)
    {
        if (ConditionCache.TryGetValue(conditionId, out GameplayEffectConditionDefinitionSO cached))
            return cached;
        string path = $"{ConditionRoot}/{Safe(conditionId)}.asset";
        GameplayEffectConditionDefinitionSO value = AssetDatabase
            .LoadAssetAtPath<GameplayEffectConditionDefinitionSO>(path);
        if (value != null && MonoScript.FromScriptableObject(value) == null)
        {
            AssetDatabase.DeleteAsset(path);
            value = null;
        }
        if (value == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            value = ScriptableObject.CreateInstance<GameplayEffectConditionDefinitionSO>();
            AssetDatabase.CreateAsset(value, path);
        }
        value.Configure(
            StableNumericId($"condition:{conditionId}"),
            conditionId,
            $"V26 trait effect condition '{conditionId}'.");
        EditorUtility.SetDirty(value);
        ConditionCache.Add(conditionId, value);
        return value;
    }

    private static GameplayEffectProjectionPhase Phase(GameplayEffectOperation operation) =>
        operation switch
        {
            GameplayEffectOperation.AddFlat => GameplayEffectProjectionPhase.BaseAdd,
            GameplayEffectOperation.AddPercent => GameplayEffectProjectionPhase.AdditivePercent,
            GameplayEffectOperation.Multiply => GameplayEffectProjectionPhase.Multiplicative,
            GameplayEffectOperation.Override => GameplayEffectProjectionPhase.Override,
            _ => GameplayEffectProjectionPhase.Clamp
        };

    private static EffectSpec M(string target, float value, string condition = null) =>
        new() { Target=target,Value=value,Operation=GameplayEffectOperation.Multiply,Condition=condition };
    private static EffectSpec A(string target, float value, string condition = null) =>
        new() { Target=target,Value=value,Operation=GameplayEffectOperation.AddFlat,Condition=condition };
    private static string P(string shortId) =>
        GameplayEffectTargetIds.StartingProficiencyExperience($"proficiency:{shortId}");

    private static TraitSpec T(
        int id,
        string key,
        string name,
        string description,
        CharacterTraitPolarity polarity,
        CharacterTraitSelectionRarity rarity,
        string family,
        params EffectSpec[] effects) => new()
        {
            Id=id,Key=key,Name=name,Description=description,Polarity=polarity,
            Rarity=rarity,Family=family,Effects=effects ?? Array.Empty<EffectSpec>()
        };

    private static int StableNumericId(string value)
    {
        int hash = CharacterGrowthRules.StableHash(value);
        if (hash == int.MinValue) return int.MaxValue;
        hash = Math.Abs(hash);
        return hash == 0 ? 1 : hash;
    }

    private static string Safe(string value) => value
        .Replace(':','_')
        .Replace('/','_')
        .Replace(' ','_');

    private static void EnsureFolders()
    {
        Ensure("Assets/Resources/SO", "V26");
        Ensure("Assets/Resources/SO/V26", "Traits");
        Ensure("Assets/Resources/SO/V26/Traits", "Founder");
        Ensure("Assets/Resources/SO/V26", "Effects");
        Ensure("Assets/Resources/SO/V26/Effects", "Definitions");
        Ensure("Assets/Resources/SO/V26/Effects", "Conditions");
    }

    private static void Ensure(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
