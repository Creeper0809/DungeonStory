#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V27CharacterPerformanceContentAssetBuilder
{
    private const string Root = "Assets/Resources/SO/V27/CharacterPerformance";
    private const string CapacityRoot = Root + "/Capacities";
    private const string FormulaRoot = Root + "/Formulas";
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";

    private sealed class WorkSpec
    {
        public WorkSpec(
            WorkTypeId workType,
            string displayName,
            string primary,
            string secondary,
            float secondaryWeight,
            Func<CharacterPerformanceCapacityInput[]> inputs)
        {
            WorkType = workType;
            DisplayName = displayName;
            Primary = primary ?? string.Empty;
            Secondary = secondary ?? string.Empty;
            SecondaryWeight = secondaryWeight;
            Inputs = inputs;
        }

        public WorkTypeId WorkType { get; }
        public string DisplayName { get; }
        public string Primary { get; }
        public string Secondary { get; }
        public float SecondaryWeight { get; }
        public Func<CharacterPerformanceCapacityInput[]> Inputs { get; }
    }

    [MenuItem("DungeonStory/Content/V27/Build Character Performance Content")]
    public static void Build()
    {
        EnsureFolder(CapacityRoot);
        EnsureFolder(FormulaRoot);
        DeleteObsoleteCapacityAsset();
        List<ScriptableObject> authored = new();
        AuthorAnatomyCapacityCoverage();
        foreach (CharacterFunctionalCapacityId id in Enum
                     .GetValues(typeof(CharacterFunctionalCapacityId))
                     .Cast<CharacterFunctionalCapacityId>())
        {
            CharacterFunctionalCapacityDefinitionSO definition = GetOrCreate<
                CharacterFunctionalCapacityDefinitionSO>(
                $"{CapacityRoot}/{Safe(CharacterFunctionalCapacityIds.GetStableId(id))}.asset");
            definition.Configure(
                id,
                CharacterFunctionalCapacityIds.GetDisplayName(id),
                CapacityDescription(id));
            EditorUtility.SetDirty(definition);
            authored.Add(definition);
        }

        authored.AddRange(BuildCompositeFormulas());
        authored.AddRange(BuildWorkFormulas());
        authored.AddRange(BuildCombatFormulas());
        authored.AddRange(BuildMedicalFormulas());
        authored.AddRange(BuildSurvivalSocialFormulas());
        authored.Add(BuildMovementFormula());
        authored.Add(BuildHaulCapacityFormula());

        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException(
                $"Missing domain catalog at '{CatalogPath}'.");
        AuthorSpeciesEffects();
        catalog.SetDefinitions(catalog.Definitions
            .Where(value => value != null
                && value is not CharacterFunctionalCapacityDefinitionSO
                && value is not CharacterPerformanceFormulaDefinitionSO)
            .Concat(authored));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "V27_CHARACTER_PERFORMANCE_CONTENT=PASS; capacities=14; composites=5; "
            + "workSpeed=30; workAccident=30; workQuality=4; workYield=1; "
            + "rest=3; combat=8; medical=10; "
            + "survivalSocial=16");
    }

    private static void AuthorSpeciesEffects()
    {
        string[] speciesGuids = AssetDatabase.FindAssets(
            "t:CharacterSpeciesSO",
            new[] { "Assets/Resources/SO/Character/Species" });
        foreach (CharacterSpeciesSO species in speciesGuids
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Select(AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>)
                     .Where(value => value != null))
        {
            List<GameplayEffectBinding> bindings = new();
            if (TryGetSpeciesCapacityMultipliers(
                    species.speciesTag,
                    out float[] capacityMultipliers))
            {
                CharacterFunctionalCapacityId[] capacityIds = Enum
                    .GetValues(typeof(CharacterFunctionalCapacityId))
                    .Cast<CharacterFunctionalCapacityId>()
                    .ToArray();
                if (capacityMultipliers.Length != capacityIds.Length)
                    throw new InvalidOperationException(
                        $"Species '{species.speciesTag}' capacity matrix must contain exactly "
                        + $"{capacityIds.Length} values.");
                for (int index = 0; index < capacityIds.Length; index++)
                {
                    AddSpeciesMultiplier(
                        bindings,
                        species,
                        CharacterFunctionalCapacityIds.GetStableId(capacityIds[index]),
                        capacityMultipliers[index],
                        includeNeutral: true);
                }
            }

            // Social/economic authored identity remains separate from physiology.
            switch (species.speciesTag)
            {
                case "Slime":
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.Spending, .85f);
                    break;
                case "Orc":
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.Spending, 1.25f);
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.WaitPatience, .90f);
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.CrowdSensitivity, .80f);
                    break;
                case "Vampire":
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.Spending, 1.25f);
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.WaitPatience, 1.10f);
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.CrowdSensitivity, 1.25f);
                    break;
                case "Beastkin":
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.CrowdSensitivity, 1.30f);
                    break;
                case "Demon":
                    AddSpeciesMultiplier(bindings, species, GameplayEffectTargetIds.Spending, 1.60f);
                    break;
            }

            species.ConfigureGameplayEffects(bindings);
            EditorUtility.SetDirty(species);
        }
    }

    private static void AddSpeciesMultiplier(
        ICollection<GameplayEffectBinding> bindings,
        CharacterSpeciesSO species,
        string targetId,
        float value,
        bool includeNeutral = false)
    {
        if (!includeNeutral && Mathf.Approximately(value, 1f))
        {
            return;
        }

        bindings.Add(new GameplayEffectBinding
        {
            bindingId = $"species:{species.DefinitionId.Value}:{Safe(targetId)}",
            definition = V26FounderTraitContentBuilder.ResolveEffect(
                targetId,
                GameplayEffectOperation.Multiply),
            value = value
        });
    }

    internal static bool TryGetSpeciesCapacityMultipliers(
        string speciesTag,
        out float[] values)
    {
        values = speciesTag switch
        {
            // mental, visual, auditory, respiratory, circulation, intake,
            // purification, vitality, power, precision, mobility,
            // communication, arcane, immune
            "Slime" => CapacityRow(100, 90, 90, 100, 110, 105, 115, 115, 80, 85, 110, 85, 105, 115),
            "Orc" => CapacityRow(85, 90, 95, 110, 115, 85, 90, 115, 125, 95, 110, 90, 80, 105),
            "Vampire" => CapacityRow(115, 110, 100, 85, 100, 115, 100, 110, 90, 105, 100, 105, 115, 105),
            "Beastkin" => CapacityRow(90, 115, 120, 110, 115, 100, 90, 105, 110, 95, 125, 100, 80, 100),
            "Demon" => CapacityRow(110, 100, 100, 105, 110, 90, 105, 100, 105, 100, 90, 115, 115, 105),
            "Kobold" => CapacityRow(105, 110, 105, 95, 95, 90, 100, 90, 80, 125, 105, 90, 100, 95),
            "Myconid" => CapacityRow(105, 95, 80, 105, 110, 110, 120, 110, 80, 110, 110, 90, 100, 115),
            "Harpy" => CapacityRow(95, 125, 110, 115, 115, 90, 85, 95, 80, 110, 125, 105, 90, 90),
            "Golem" => CapacityRow(90, 100, 85, 105, 115, 90, 110, 100, 125, 105, 110, 80, 110, 115),
            _ => null
        };
        return values != null;
    }

    private static float[] CapacityRow(params int[] percentages) =>
        percentages.Select(value => value / 100f).ToArray();

    private static IEnumerable<CharacterPerformanceFormulaDefinitionSO>
        BuildCombatFormulas()
    {
        string melee = BuiltInCharacterProficiencyIds.MeleeCombat.Value;
        string ranged = BuiltInCharacterProficiencyIds.RangedCombat.Value;
        string study = BuiltInCharacterProficiencyIds.Scholarship.Value;
        yield return Formula("performance:combat:melee-hit", "근접 명중",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.SuccessChance,
            1f, MeleePrecisionInputs(), melee, "", 0f, GameplayEffectTargetIds.CombatPower);
        yield return Formula(CharacterPerformanceFormulaIds.MeleePower, "근접 위력",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.Factor,
            1f, MeleePowerInputs(), melee, "", 0f, GameplayEffectTargetIds.CombatPower);
        yield return Formula("performance:combat:ranged-hit", "원거리 명중",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.SuccessChance,
            1f, RangedPrecisionInputs(), ranged, "", 0f, GameplayEffectTargetIds.CombatPower);
        yield return Formula("performance:combat:evasion", "회피",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.SuccessChance,
            1f, EvasionInputs(), "", "", 0f, "");
        yield return Formula("performance:combat:movement", "전투 이동",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.Speed,
            1f, MobilityInputs(), "", "", 0f, GameplayEffectTargetIds.MoveSpeed);
        yield return Formula("performance:combat:defense-reaction", "방어 반응",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.SuccessChance,
            1f, DefenseReactionInputs(), "", "", 0f, "");
        yield return Formula("performance:combat:arcane-power", "마력 위력",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.Factor,
            1f, ArcanePowerInputs(), study, "", 0f, GameplayEffectTargetIds.CombatPower);
        yield return Formula("performance:combat:mana-recovery", "마나 회복",
            CharacterPerformanceFormulaDomain.Combat, CharacterPerformanceResultChannel.Recovery,
            1f, ManaRecoveryInputs(), "", "", 0f, GameplayEffectTargetIds.RecoverySpeed);
    }

    private static IEnumerable<CharacterPerformanceFormulaDefinitionSO>
        BuildMedicalFormulas()
    {
        string medicine = BuiltInCharacterProficiencyIds.Medicine.Value;
        string study = BuiltInCharacterProficiencyIds.Scholarship.Value;
        yield return Formula("performance:medical:treatment-speed", "치료 속도",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.Speed,
            1f, MedicalInputs(), medicine, "", 0f, GameplayEffectTargetIds.WorkSpeed,
            BuiltInWorkTypeIds.Treat.Value);
        yield return Formula("performance:medical:treatment-efficiency", "치료 효율",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.SuccessChance,
            1f, MedicalInputs(), medicine, "", 0f, "");
        yield return Formula("performance:medical:surgery-speed", "수술 속도",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.Speed,
            1f, MedicalInputs(), medicine, study, .2f, GameplayEffectTargetIds.WorkSpeed,
            BuiltInWorkTypeIds.Surgery.Value);
        yield return Formula("performance:medical:surgery-success", "수술 성공",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.SuccessChance,
            1f, MedicalInputs(), medicine, study, .2f, "");
        yield return Formula("performance:medical:complication-risk", "합병증 위험",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.AccidentRisk,
            1f, MedicalInputs(), medicine, study, .2f, GameplayEffectTargetIds.AccidentChance);
        yield return Formula("performance:medical:wound-recovery", "상처 회복",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.Recovery,
            1f, RecoveryInputs(), "", "", 0f, GameplayEffectTargetIds.RecoverySpeed);
        yield return Formula(CharacterPerformanceFormulaIds.DiseaseResistance, "질병 저항",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.SuccessChance,
            1f, DiseaseResistanceInputs(), "", "", 0f, GameplayEffectTargetIds.DiseaseResistance);
        yield return Formula("performance:medical:disease-recovery", "질병 회복",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.Recovery,
            1f, DiseaseRecoveryInputs(), "", "", 0f, GameplayEffectTargetIds.DiseaseRecoverySpeed);
        yield return Formula(CharacterPerformanceFormulaIds.ImmunityGain, "면역 획득",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.Recovery,
            1f, ImmunityGainInputs(), "", "", 0f, GameplayEffectTargetIds.ImmunityGain);
        yield return Formula("performance:medical:immunity-retention", "면역 유지",
            CharacterPerformanceFormulaDomain.Medical, CharacterPerformanceResultChannel.Recovery,
            1f, ImmunityRetentionInputs(), "", "", 0f, GameplayEffectTargetIds.ImmunityRetention);
    }

    private static IEnumerable<CharacterPerformanceFormulaDefinitionSO>
        BuildSurvivalSocialFormulas()
    {
        string social = BuiltInCharacterProficiencyIds.Social.Value;
        yield return Formula("performance:survival:food-consumption", "음식 소비",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Consumption,
            1f, ResourceUseInputs(), "", "", 0f, GameplayEffectTargetIds.Consumption);
        yield return Formula("performance:survival:nutrition-efficiency", "영양 효율",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Factor,
            1f, NutritionInputs(), "", "", 0f, "");
        yield return Formula("performance:survival:fatigue-rate", "피로 축적",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Consumption,
            1f, SustainedInputs(), "", "", 0f, GameplayEffectTargetIds.FatigueRate);
        yield return Formula("performance:survival:cold-exposure", "냉기 노출",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Exposure,
            1f, ThermalInputs(), "", "", 0f, GameplayEffectTargetIds.ColdExposure);
        yield return Formula("performance:survival:heat-exposure", "열기 노출",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Exposure,
            1f, ThermalInputs(), "", "", 0f, GameplayEffectTargetIds.HeatExposure);
        yield return Formula("performance:survival:food-poisoning", "식중독 위험",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.AccidentRisk,
            1f, FoodSafetyInputs(), "", "", 0f, GameplayEffectTargetIds.FoodPoisoningChance);
        yield return Formula("performance:survival:alarm-response", "경보 반응",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Speed,
            1f, AwarenessInputs(), "", "", 0f, "");
        yield return Formula("performance:survival:risk-detection", "위험 탐지",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Detection,
            1f, AwarenessInputs(), "", "", 0f, "");
        yield return Formula("performance:social:wait-patience", "대기 인내",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Factor,
            1f, MentalInputs(), "", "", 0f, GameplayEffectTargetIds.WaitPatience);
        yield return Formula("performance:social:crowd-sensitivity", "군중 민감도",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Exposure,
            1f, SocialResilienceInputs(), "", "", 0f, GameplayEffectTargetIds.CrowdSensitivity);
        yield return Formula("performance:social:negotiation", "협상",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.SuccessChance,
            1f, SocialInputs(), social, "", 0f, "");
        yield return Formula("performance:social:spending", "소비 성향",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.Consumption,
            1f, SocialInputs(), social, "", 0f, GameplayEffectTargetIds.Spending);
        yield return Formula("performance:social:negative-mood-duration", "부정 기분 지속",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.MoodDuration,
            1f, MentalInputs(), "", "", 0f, GameplayEffectTargetIds.NegativeMoodDuration);
        yield return Formula("performance:social:relationship-recovery", "관계 회복",
            CharacterPerformanceFormulaDomain.SurvivalSocial, CharacterPerformanceResultChannel.RelationshipRecovery,
            1f, SocialInputs(), social, "", 0f, GameplayEffectTargetIds.RelationshipRecovery);
    }

    private static void AuthorAnatomyCapacityCoverage()
    {
        foreach (AnatomyProfileSO profile in AssetDatabase
                     .FindAssets("t:AnatomyProfileSO")
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Select(AssetDatabase.LoadAssetAtPath<AnatomyProfileSO>)
                     .Where(value => value != null))
        {
            List<AnatomyFunctionalCapacityNotApplicable> notApplicable = new();
            if (string.Equals(profile.ProfileId, "anatomy:construct", StringComparison.Ordinal))
            {
                notApplicable.Add(NA(
                    CharacterFunctionalCapacityId.RespiratoryExchange,
                    "구조체는 생물학적 가스 교환을 사용하지 않습니다."));
                notApplicable.Add(NA(
                    CharacterFunctionalCapacityId.IntakeProcessing,
                    "구조체는 음식과 약물을 소화·흡수하지 않습니다."));
                notApplicable.Add(NA(
                    CharacterFunctionalCapacityId.PurificationProcessing,
                    "구조체는 생물학적 독소·질병 부산물 배출 기관이 없습니다."));
            }
            else if (string.Equals(profile.ProfileId, "anatomy:slime", StringComparison.Ordinal))
            {
                notApplicable.Add(NA(
                    CharacterFunctionalCapacityId.RespiratoryExchange,
                    "슬라임은 별도의 호흡 기관 없이 막 전체로 교환합니다."));
            }
            // Phase 153 assigns an explicit numeric baseline to every playable
            // species capacity. Membrane exchange and construct charge/purge are
            // physical equivalents, so former biological N/A entries are retired.
            notApplicable.Clear();
            profile.ConfigureNotApplicableCapacities(notApplicable);

            foreach (CharacterFunctionalCapacityId capacityId in Enum
                         .GetValues(typeof(CharacterFunctionalCapacityId))
                         .Cast<CharacterFunctionalCapacityId>())
            {
                if (notApplicable.Any(value => value.CapacityId == capacityId))
                    continue;
                AnatomyFunction function = ToAnatomyFunction(capacityId);
                if (profile.Nodes.Any(node =>
                        (node.ExpandedFunctions & function) != 0))
                    continue;
                AnatomyNodeDefinition producer = SelectCapacityProducer(
                    profile,
                    capacityId);
                if (producer == null)
                {
                    throw new InvalidOperationException(
                        $"Anatomy profile '{profile.ProfileId}' has no suitable node for "
                        + CharacterFunctionalCapacityIds.GetStableId(capacityId));
                }
                producer.AddFunctions(function);
            }
            EditorUtility.SetDirty(profile);
        }
    }

    private static AnatomyNodeDefinition SelectCapacityProducer(
        AnatomyProfileSO profile,
        CharacterFunctionalCapacityId capacityId)
    {
        AnatomyFunction preferred = capacityId switch
        {
            CharacterFunctionalCapacityId.PhysicalPower =>
                AnatomyFunction.PhysicalMobility
                | AnatomyFunction.PrecisionManipulation
                | AnatomyFunction.PowerCirculation,
            CharacterFunctionalCapacityId.ImmuneDefense =>
                AnatomyFunction.PurificationProcessing
                | AnatomyFunction.VitalityResponse
                | AnatomyFunction.PowerCirculation,
            CharacterFunctionalCapacityId.IntakeProcessing =>
                AnatomyFunction.IntakeProcessing | AnatomyFunction.PowerCirculation,
            CharacterFunctionalCapacityId.PurificationProcessing =>
                AnatomyFunction.PurificationProcessing
                | AnatomyFunction.IntakeProcessing
                | AnatomyFunction.PowerCirculation,
            _ => AnatomyFunction.PowerCirculation
        };
        return profile.Nodes
            .Where(node => (node.ExpandedFunctions & preferred) != 0)
            .OrderByDescending(node => node.CapacityWeight)
            .FirstOrDefault();
    }

    private static AnatomyFunctionalCapacityNotApplicable NA(
        CharacterFunctionalCapacityId id,
        string reason) => new(id, reason);

    private static AnatomyFunction ToAnatomyFunction(
        CharacterFunctionalCapacityId capacityId) => capacityId switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => AnatomyFunction.MentalMaintenance,
        CharacterFunctionalCapacityId.VisualDiscernment => AnatomyFunction.VisualDiscernment,
        CharacterFunctionalCapacityId.AuditorySensing => AnatomyFunction.AuditorySensing,
        CharacterFunctionalCapacityId.RespiratoryExchange => AnatomyFunction.RespiratoryExchange,
        CharacterFunctionalCapacityId.PowerCirculation => AnatomyFunction.PowerCirculation,
        CharacterFunctionalCapacityId.IntakeProcessing => AnatomyFunction.IntakeProcessing,
        CharacterFunctionalCapacityId.PurificationProcessing => AnatomyFunction.PurificationProcessing,
        CharacterFunctionalCapacityId.VitalityResponse => AnatomyFunction.VitalityResponse,
        CharacterFunctionalCapacityId.PhysicalPower => AnatomyFunction.PhysicalPower,
        CharacterFunctionalCapacityId.PrecisionManipulation => AnatomyFunction.PrecisionManipulation,
        CharacterFunctionalCapacityId.PhysicalMobility => AnatomyFunction.PhysicalMobility,
        CharacterFunctionalCapacityId.Communication => AnatomyFunction.Communication,
        CharacterFunctionalCapacityId.ArcaneConduction => AnatomyFunction.ArcaneConduction,
        CharacterFunctionalCapacityId.ImmuneDefense => AnatomyFunction.ImmuneDefense,
        _ => throw new ArgumentOutOfRangeException(nameof(capacityId),capacityId,null)
    };

    private static CharacterPerformanceFormulaDefinitionSO BuildMovementFormula() =>
        Formula(
            "performance:survival:movement-speed",
            "실제 이동 속도",
            CharacterPerformanceFormulaDomain.SurvivalSocial,
            CharacterPerformanceResultChannel.Speed,
            1f,
            Inputs(
                I(CharacterFunctionalCapacityId.PhysicalMobility,.65f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.PowerCirculation,.20f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck),
                I(CharacterFunctionalCapacityId.RespiratoryExchange,.15f,
                    CharacterPerformanceInputRole.Contribution)),
            string.Empty,string.Empty,0f,GameplayEffectTargetIds.MoveSpeed);

    private static CharacterPerformanceFormulaDefinitionSO BuildHaulCapacityFormula() =>
        Formula(
            CharacterPerformanceFormulaIds.HaulCapacity,
            "운반 한도",
            CharacterPerformanceFormulaDomain.SurvivalSocial,
            CharacterPerformanceResultChannel.Factor,
            1f,
            Inputs(
                I(CharacterFunctionalCapacityId.PhysicalPower,.50f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.PhysicalMobility,.20f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.PowerCirculation,.15f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck),
                I(CharacterFunctionalCapacityId.RespiratoryExchange,.10f,
                    CharacterPerformanceInputRole.Contribution),
                I(CharacterFunctionalCapacityId.VitalityResponse,.05f,
                    CharacterPerformanceInputRole.Contribution)),
            BuiltInCharacterProficiencyIds.Fieldwork.Value,
            string.Empty,
            0f,
            GameplayEffectTargetIds.HaulCapacity);

    private static IEnumerable<CharacterPerformanceFormulaDefinitionSO>
        BuildCompositeFormulas()
    {
        yield return Formula(
            CharacterCompositePerformanceIds.SituationalAwareness,
            "상황 파악",
            CharacterPerformanceFormulaDomain.Composite,
            CharacterPerformanceResultChannel.Factor,
            1f,
            Inputs(
                I(CharacterFunctionalCapacityId.MentalMaintenance, .45f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.VisualDiscernment, .35f,
                    CharacterPerformanceInputRole.Contribution),
                I(CharacterFunctionalCapacityId.AuditorySensing, .20f,
                    CharacterPerformanceInputRole.Contribution)),
            string.Empty, string.Empty, 0f, string.Empty);
        yield return Formula(
            CharacterCompositePerformanceIds.PrecisionExecution,
            "정밀 수행",
            CharacterPerformanceFormulaDomain.Composite,
            CharacterPerformanceResultChannel.Factor,
            1f,
            Inputs(
                I(CharacterFunctionalCapacityId.MentalMaintenance, .20f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.VisualDiscernment, .25f,
                    CharacterPerformanceInputRole.Contribution),
                I(CharacterFunctionalCapacityId.PrecisionManipulation, .55f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck
                    | CharacterPerformanceInputRole.Required)),
            string.Empty, string.Empty, 0f, string.Empty);
        yield return Formula(
            CharacterCompositePerformanceIds.MobilityExecution,
            "기동 수행",
            CharacterPerformanceFormulaDomain.Composite,
            CharacterPerformanceResultChannel.Factor,
            1f,
            Inputs(
                I(CharacterFunctionalCapacityId.PhysicalMobility, .65f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.PowerCirculation, .20f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck),
                I(CharacterFunctionalCapacityId.RespiratoryExchange, .15f,
                    CharacterPerformanceInputRole.Contribution)),
            string.Empty, string.Empty, 0f, string.Empty);
        yield return Formula(
            CharacterCompositePerformanceIds.SustainedExecution,
            "지속 수행",
            CharacterPerformanceFormulaDomain.Composite,
            CharacterPerformanceResultChannel.Factor,
            1f,
            Inputs(
                I(CharacterFunctionalCapacityId.PowerCirculation, .30f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.RespiratoryExchange, .20f,
                    CharacterPerformanceInputRole.Contribution),
                I(CharacterFunctionalCapacityId.VitalityResponse, .35f,
                    CharacterPerformanceInputRole.Contribution),
                I(CharacterFunctionalCapacityId.IntakeProcessing, .15f,
                    CharacterPerformanceInputRole.Contribution)),
            string.Empty, string.Empty, 0f, string.Empty);
        yield return Formula(
            CharacterCompositePerformanceIds.RecoveryFoundation,
            "회복 기반",
            CharacterPerformanceFormulaDomain.Composite,
            CharacterPerformanceResultChannel.Factor,
            1f,
            Inputs(
                I(CharacterFunctionalCapacityId.PowerCirculation, .20f,
                    CharacterPerformanceInputRole.Contribution),
                I(CharacterFunctionalCapacityId.IntakeProcessing, .15f,
                    CharacterPerformanceInputRole.Contribution),
                I(CharacterFunctionalCapacityId.PurificationProcessing, .15f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Bottleneck),
                I(CharacterFunctionalCapacityId.VitalityResponse, .30f,
                    CharacterPerformanceInputRole.Contribution
                    | CharacterPerformanceInputRole.Required),
                I(CharacterFunctionalCapacityId.ImmuneDefense, .20f,
                    CharacterPerformanceInputRole.Contribution)),
            string.Empty, string.Empty, 0f, string.Empty);
    }

    private static IEnumerable<CharacterPerformanceFormulaDefinitionSO>
        BuildWorkFormulas()
    {
        foreach (WorkSpec spec in WorkSpecs())
        {
            string suffix = spec.WorkType.Value.Substring("work:".Length);
            if (spec.WorkType == BuiltInWorkTypeIds.Rest)
            {
                yield return Formula(
                    $"performance:work:{suffix}:sleep-recovery",
                    "휴식 · 수면 회복",
                    CharacterPerformanceFormulaDomain.Work,
                    CharacterPerformanceResultChannel.Recovery,
                    1f,
                    RecoveryInputs(), string.Empty, string.Empty, 0f,
                    GameplayEffectTargetIds.RecoverySpeed);
                yield return Formula(
                    $"performance:work:{suffix}:fatigue-recovery",
                    "휴식 · 피로 회수",
                    CharacterPerformanceFormulaDomain.Work,
                    CharacterPerformanceResultChannel.Recovery,
                    1f,
                    RecoveryInputs(), string.Empty, string.Empty, 0f,
                    GameplayEffectTargetIds.RecoverySpeed);
                yield return Formula(
                    $"performance:work:{suffix}:wound-recovery",
                    "휴식 · 상처 회복",
                    CharacterPerformanceFormulaDomain.Work,
                    CharacterPerformanceResultChannel.Recovery,
                    1f,
                    RecoveryInputs(), string.Empty, string.Empty, 0f,
                    GameplayEffectTargetIds.RecoverySpeed);
                continue;
            }

            yield return Formula(
                $"performance:work:{suffix}:speed",
                $"{spec.DisplayName} · 작업 속도",
                CharacterPerformanceFormulaDomain.Work,
                CharacterPerformanceResultChannel.Speed,
                1f,
                spec.Inputs(),
                spec.Primary,
                spec.Secondary,
                spec.SecondaryWeight,
                GameplayEffectTargetIds.WorkSpeed,
                spec.WorkType == BuiltInWorkTypeIds.Treat
                    || spec.WorkType == BuiltInWorkTypeIds.Surgery
                    ? string.Empty
                    : spec.WorkType.Value);
            yield return Formula(
                $"performance:work:{suffix}:accident",
                $"{spec.DisplayName} · 사고 위험",
                CharacterPerformanceFormulaDomain.Work,
                CharacterPerformanceResultChannel.AccidentRisk,
                1f,
                spec.Inputs(),
                spec.Primary,
                spec.Secondary,
                spec.SecondaryWeight,
                GameplayEffectTargetIds.AccidentChance,
                spec.WorkType.Value);
            if (spec.WorkType == BuiltInWorkTypeIds.Construct
                || spec.WorkType == BuiltInWorkTypeIds.Repair
                || spec.WorkType == BuiltInWorkTypeIds.Craft
                || spec.WorkType == BuiltInWorkTypeIds.Cook)
            {
                yield return Formula(
                    $"performance:work:{suffix}:quality",
                    $"{spec.DisplayName} · 품질",
                    CharacterPerformanceFormulaDomain.Work,
                    CharacterPerformanceResultChannel.Quality,
                    1f,
                    spec.Inputs(),
                    spec.Primary,
                    spec.Secondary,
                    spec.SecondaryWeight,
                    spec.WorkType == BuiltInWorkTypeIds.Craft
                        ? GameplayEffectTargetIds.CraftQualityScore
                        : string.Empty);
            }
            if (spec.WorkType == BuiltInWorkTypeIds.Harvest)
            {
                yield return Formula(
                    $"performance:work:{suffix}:yield",
                    $"{spec.DisplayName} · 수율",
                    CharacterPerformanceFormulaDomain.Work,
                    CharacterPerformanceResultChannel.Yield,
                    1f,
                    spec.Inputs(),
                    spec.Primary,
                    spec.Secondary,
                    spec.SecondaryWeight,
                    "harvest:yield");
            }
        }
    }

    private static WorkSpec[] WorkSpecs()
    {
        string field = BuiltInCharacterProficiencyIds.Fieldwork.Value;
        string construction = BuiltInCharacterProficiencyIds.ConstructionEngineering.Value;
        string craft = BuiltInCharacterProficiencyIds.Crafting.Value;
        string food = BuiltInCharacterProficiencyIds.FoodProduction.Value;
        string study = BuiltInCharacterProficiencyIds.Scholarship.Value;
        string medicine = BuiltInCharacterProficiencyIds.Medicine.Value;
        string social = BuiltInCharacterProficiencyIds.Social.Value;
        return new[]
        {
            W(BuiltInWorkTypeIds.Operate,"가동","selector:facility-primary","selector:facility-secondary",.2f,PrecisionInputs),
            W(BuiltInWorkTypeIds.Restock,"재고 보충",field,"",0f,ForceWorkInputs),
            W(BuiltInWorkTypeIds.Construct,"건설",construction,field,.2f,ConstructionInputs),
            W(BuiltInWorkTypeIds.Repair,"수리",construction,craft,.2f,PrecisionInputs),
            W(BuiltInWorkTypeIds.Clean,"청소",field,"",0f,FieldInputs),
            W(BuiltInWorkTypeIds.Research,"연구",study,"",0f,ResearchInputs),
            W(BuiltInWorkTypeIds.Guard,"경비","selector:active-combat","",0f,CombatInputs),
            W(BuiltInWorkTypeIds.Reception,"접객",social,"",0f,SocialInputs),
            W(BuiltInWorkTypeIds.Rescue,"구조",medicine,field,.2f,RescueInputs),
            W(BuiltInWorkTypeIds.Rest,"휴식","","",0f,RecoveryInputs),
            W(BuiltInWorkTypeIds.Craft,"제작",craft,"selector:rune-scholarship",.2f,PrecisionInputs),
            W(BuiltInWorkTypeIds.Haul,"운반",field,"",0f,ForceWorkInputs),
            W(BuiltInWorkTypeIds.Hunt,"사냥",food,"selector:active-combat",.2f,CombatInputs),
            W(BuiltInWorkTypeIds.Butcher,"도축",food,medicine,.2f,MedicalInputs),
            W(BuiltInWorkTypeIds.DrawWater,"물 긷기",field,"",0f,ForceWorkInputs),
            W(BuiltInWorkTypeIds.Cook,"조리",food,"",0f,PrecisionInputs),
            W(BuiltInWorkTypeIds.Treat,"치료",medicine,"",0f,MedicalInputs),
            W(BuiltInWorkTypeIds.Surgery,"수술",medicine,study,.2f,MedicalInputs),
            W(BuiltInWorkTypeIds.Refuel,"급유",field,"",0f,ForceWorkInputs),
            W(BuiltInWorkTypeIds.Warden,"간수",social,"selector:higher-combat",.2f,SocialInputs),
            W(BuiltInWorkTypeIds.Perform,"공연",social,"",0f,SocialInputs),
            W(BuiltInWorkTypeIds.Gather,"채집",field,"",0f,FieldInputs),
            W(BuiltInWorkTypeIds.Sow,"파종",food,field,.2f,FieldInputs),
            W(BuiltInWorkTypeIds.Harvest,"수확",food,field,.2f,FieldInputs),
            W(BuiltInWorkTypeIds.Logging,"벌목",field,"",0f,ForceWorkInputs),
            W(BuiltInWorkTypeIds.Quarry,"채석",field,construction,.2f,ForceWorkInputs),
            W(BuiltInWorkTypeIds.AnimalCare,"동물 관리",food,medicine,.2f,MedicalInputs),
            W(BuiltInWorkTypeIds.GrandProject,"대형 사업",construction,study,.2f,ConstructionInputs),
            W(BuiltInWorkTypeIds.ThreatMitigation,"위협 완화",study,field,.2f,ResearchInputs),
            W(BuiltInWorkTypeIds.Plumbing,"배관",construction,study,.2f,PrecisionInputs),
            W(BuiltInWorkTypeIds.Dismantle,"해체",construction,craft,.2f,ConstructionInputs)
        };
    }

    private static CharacterPerformanceCapacityInput[] FieldInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.10f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.20f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ForceWorkInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.05f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalPower,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ConstructionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.10f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalPower,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] RescueInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.15f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PhysicalPower,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.05f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] PrecisionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.40f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ResearchInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.50f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.25f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] MedicalInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] SocialInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.45f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.AuditorySensing,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.Communication,.40f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required));

    private static CharacterPerformanceCapacityInput[] CombatInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.15f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalPower,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] RecoveryInputs() => Inputs(
        I(CharacterFunctionalCapacityId.PowerCirculation,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.IntakeProcessing,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.15f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.VitalityResponse,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] MeleePrecisionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] MeleePowerInputs() => Inputs(
        I(CharacterFunctionalCapacityId.PhysicalPower,.50f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.10f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.15f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.05f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.05f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] RangedPrecisionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PrecisionManipulation,.30f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.AuditorySensing,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] EvasionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.AuditorySensing,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] MobilityInputs() => Inputs(
        I(CharacterFunctionalCapacityId.PhysicalMobility,.65f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.20f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.15f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] DefenseReactionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.25f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.AuditorySensing,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PhysicalMobility,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck));

    private static CharacterPerformanceCapacityInput[] ArcanePowerInputs() => Inputs(
        I(CharacterFunctionalCapacityId.ArcaneConduction,.60f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.MentalMaintenance,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ManaRecoveryInputs() => Inputs(
        I(CharacterFunctionalCapacityId.ArcaneConduction,.50f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VitalityResponse,.25f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.MentalMaintenance,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] DiseaseResistanceInputs() => Inputs(
        I(CharacterFunctionalCapacityId.ImmuneDefense,.50f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.IntakeProcessing,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] DiseaseRecoveryInputs() => Inputs(
        I(CharacterFunctionalCapacityId.ImmuneDefense,.30f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.VitalityResponse,.25f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.PowerCirculation,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.IntakeProcessing,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ImmunityGainInputs() => Inputs(
        I(CharacterFunctionalCapacityId.ImmuneDefense,.55f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VitalityResponse,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ImmunityRetentionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.ImmuneDefense,.60f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VitalityResponse,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.10f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ResourceUseInputs() => Inputs(
        I(CharacterFunctionalCapacityId.IntakeProcessing,.50f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] NutritionInputs() => Inputs(
        I(CharacterFunctionalCapacityId.IntakeProcessing,.55f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.PowerCirculation,.15f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] SustainedInputs() => Inputs(
        I(CharacterFunctionalCapacityId.PowerCirculation,.30f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.IntakeProcessing,.15f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] ThermalInputs() => Inputs(
        I(CharacterFunctionalCapacityId.PowerCirculation,.45f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.RespiratoryExchange,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.IntakeProcessing,.15f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] FoodSafetyInputs() => Inputs(
        I(CharacterFunctionalCapacityId.ImmuneDefense,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.PurificationProcessing,.35f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck),
        I(CharacterFunctionalCapacityId.IntakeProcessing,.20f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.VitalityResponse,.10f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] AwarenessInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.45f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VisualDiscernment,.35f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.AuditorySensing,.20f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] MentalInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.70f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.VitalityResponse,.30f,CharacterPerformanceInputRole.Contribution));

    private static CharacterPerformanceCapacityInput[] SocialResilienceInputs() => Inputs(
        I(CharacterFunctionalCapacityId.MentalMaintenance,.60f,CharacterPerformanceInputRole.Contribution|CharacterPerformanceInputRole.Bottleneck|CharacterPerformanceInputRole.Required),
        I(CharacterFunctionalCapacityId.Communication,.25f,CharacterPerformanceInputRole.Contribution),
        I(CharacterFunctionalCapacityId.AuditorySensing,.15f,CharacterPerformanceInputRole.Contribution));

    private static WorkSpec W(WorkTypeId id,string name,string primary,string secondary,float weight,Func<CharacterPerformanceCapacityInput[]> inputs) =>
        new(id,name,primary,secondary,weight,inputs);
    private static CharacterPerformanceCapacityInput I(CharacterFunctionalCapacityId id,float weight,CharacterPerformanceInputRole role) =>
        new(id,weight,role);
    private static CharacterPerformanceCapacityInput[] Inputs(params CharacterPerformanceCapacityInput[] values) => values;

    private static CharacterPerformanceFormulaDefinitionSO Formula(
        string id,string name,CharacterPerformanceFormulaDomain domain,
        CharacterPerformanceResultChannel channel,float baseValue,
        IEnumerable<CharacterPerformanceCapacityInput> inputs,
        string primary,string secondary,float secondaryWeight,string effectTarget,
        string executionWorkTypeId = "")
    {
        string path = $"{FormulaRoot}/{Safe(id)}.asset";
        CharacterPerformanceFormulaDefinitionSO value = GetOrCreate<
            CharacterPerformanceFormulaDefinitionSO>(path);
        value.Configure(id,name,domain,channel,baseValue,inputs,primary,secondary,
            secondaryWeight,effectTarget,executionWorkTypeId);
        EditorUtility.SetDirty(value);
        return value;
    }

    private static string CapacityDescription(CharacterFunctionalCapacityId id) => id switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => "각성·집중·행동 지속",
        CharacterFunctionalCapacityId.VisualDiscernment => "거리·형상·세부 판독",
        CharacterFunctionalCapacityId.AuditorySensing => "경보·은폐·대화 수신",
        CharacterFunctionalCapacityId.RespiratoryExchange => "가스 교환·연기 대응",
        CharacterFunctionalCapacityId.PowerCirculation => "혈류·골렘 회로·슬라임 핵 순환",
        CharacterFunctionalCapacityId.IntakeProcessing => "음식·약물 흡수",
        CharacterFunctionalCapacityId.PurificationProcessing => "독소·질병 부산물·약물 배출",
        CharacterFunctionalCapacityId.VitalityResponse => "회복·피로 회수 속도",
        CharacterFunctionalCapacityId.PhysicalPower => "근육·구조체·점액질의 물리적 힘 출력",
        CharacterFunctionalCapacityId.PrecisionManipulation => "손·촉수·도구 조작",
        CharacterFunctionalCapacityId.PhysicalMobility => "보행·비행·점액 이동",
        CharacterFunctionalCapacityId.Communication => "발성·몸짓·정신 교신",
        CharacterFunctionalCapacityId.ArcaneConduction => "주문·마나 회복·과충전 안정성",
        CharacterFunctionalCapacityId.ImmuneDefense => "감염·병원체·부식성 오염에 대한 방어 반응",
        _ => throw new ArgumentOutOfRangeException(nameof(id),id,null)
    };

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value != null) return value;
        value = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(value,path);
        return value;
    }

    private static string Safe(string value) => value.Replace(':','_').Replace('/','_');

    private static void DeleteObsoleteCapacityAsset()
    {
        string obsoletePath = $"{CapacityRoot}/capacity_resource-efficiency.asset";
        if (AssetDatabase.LoadMainAssetAtPath(obsoletePath) != null
            && !AssetDatabase.DeleteAsset(obsoletePath))
        {
            throw new InvalidOperationException(
                $"Failed to delete obsolete capacity asset '{obsoletePath}'.");
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current,parts[index]);
            current = next;
        }
    }
}
#endif
