using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/Economy/Resource Item", order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceItemDefinitionSO : ItemDefinitionSO
{
    public const string ResourcePath = "SO/Economy/Items";
    public ResourceItemKind Kind => GetFeatureOrDefault<ProductionItemFeature>()?.kind ?? ResourceItemKind.Raw;
    public ResourceIngredientTag IngredientTags => GetFeatureOrDefault<ProductionItemFeature>()?.ingredientTags ?? ResourceIngredientTag.None;
    public float MarketSaleRate => Mathf.Clamp01(GetFeatureOrDefault<MarketItemFeature>()?.saleRate ?? 0.6f);
    public bool CanSellToMarket => UnitPrice > 0 && MarketSaleRate > 0f;
    public string RequiredResearchId => GetFeatureOrDefault<ResearchGateItemFeature>()?.requiredResearchId?.Trim() ?? string.Empty;
    public bool IsMeal => Kind == ResourceItemKind.Food;
    public MealDietClass MealDietClass => ResourceMealClassification.Classify(IngredientTags);
    public MealQualityTier MealQuality => GetFeatureOrDefault<FoodItemFeature>()?.quality ?? MealQualityTier.Simple;
    public MealQualityBand QualityBand => GetFeatureOrDefault<FoodItemFeature>()?.qualityBand ?? MealQualityBand.Simple;
    public MealServingRole ServingRole => GetFeatureOrDefault<FoodItemFeature>()?.servingRole ?? MealServingRole.FullMeal;
    public float Nutrition => Mathf.Max(0f, GetFeatureOrDefault<FoodItemFeature>()?.nutrition ?? 0f);
    public float FuelValue => Mathf.Max(0f, GetFeatureOrDefault<FacilitySupplyItemFeature>()?.fuelValue ?? 0f);
    public float FacilityNutritionValue => Mathf.Max(0f, GetFeatureOrDefault<FacilitySupplyItemFeature>()?.nutritionValue ?? 0f);
    public bool FacilityFeedEligible => GetFeatureOrDefault<FacilitySupplyItemFeature>()?.feedEligible ?? false;
    public bool SharedIntermediate => GetFeatureOrDefault<ProductionItemFeature>()?.sharedIntermediate ?? false;
    public float MealMood => GetFeatureOrDefault<FoodItemFeature>()?.mood ?? 0f;
    public float FreshnessSeconds => Mathf.Max(0f, GetFeatureOrDefault<FoodItemFeature>()?.freshnessSeconds ?? 0f);
    public bool Preserved => GetFeatureOrDefault<FoodItemFeature>()?.preserved ?? false;
    public bool SupportsInjuryTreatment => GetFeatureOrDefault<MedicineItemFeature>()?.supportsInjuryTreatment ?? false;
    public float TreatmentPotency => Mathf.Max(0.1f, GetFeatureOrDefault<MedicineItemFeature>()?.treatmentPotency ?? 1f);
    public float InfectionReduction => Mathf.Max(0f, GetFeatureOrDefault<MedicineItemFeature>()?.infectionReduction ?? 0f);
    public float DetoxReduction => Mathf.Max(0f, GetFeatureOrDefault<MedicineItemFeature>()?.detoxReduction ?? 0f);
    public float PainReduction => Mathf.Max(0f, GetFeatureOrDefault<MedicineItemFeature>()?.painReduction ?? 0f);
    public string VaccineDiseaseId => GetFeatureOrDefault<VaccineItemFeature>()?.diseaseId?.Trim() ?? string.Empty;
    public int VaccineDoses => Mathf.Max(0, GetFeatureOrDefault<VaccineItemFeature>()?.doses ?? 0);
    public string PathogenSampleDiseaseId => GetFeatureOrDefault<PathogenSampleItemFeature>()?.diseaseId?.Trim() ?? string.Empty;
    public string MedicalProcedureId => GetFeatureOrDefault<MedicalProcedureSupplyItemFeature>()?.procedureId?.Trim() ?? string.Empty;
    public bool TryGetCropTreatment(out CropTreatmentKind kind)
    {
        CropTreatmentItemFeature feature = GetFeatureOrDefault<CropTreatmentItemFeature>();
        kind = feature?.treatmentKind ?? default;
        return feature != null;
    }

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string name,
        string itemDescription,
        StockCategory category,
        ResourceItemKind itemKind,
        ResourceIngredientTag tags,
        int price,
        float weight,
        int stackLimit,
        string researchId)
    {
        ConfigureCore(stableId, name, itemDescription, category, price, weight, stackLimit);
        SetFeature(new ProductionItemFeature
        {
            kind = itemKind,
            ingredientTags = tags,
            sharedIntermediate = SharedIntermediate
        });
        if (itemKind == ResourceItemKind.Ammunition)
        {
            SetFeature(new AmmunitionItemFeature { ammunitionKindId = stableId?.Trim() ?? string.Empty });
        }
        else
        {
            RemoveFeature<AmmunitionItemFeature>();
        }
        if (string.IsNullOrWhiteSpace(researchId))
        {
            RemoveFeature<ResearchGateItemFeature>();
        }
        else
        {
            SetFeature(new ResearchGateItemFeature { requiredResearchId = researchId.Trim() });
        }
    }

    public void ConfigureMeal(
        MealQualityTier quality,
        float nutritionAmount,
        float moodAmount,
        float shelfLifeSeconds,
        bool isPreserved,
        MealQualityBand? authoredQualityBand = null,
        MealServingRole servingRole = MealServingRole.FullMeal)
    {
        SetFeature(new FoodItemFeature
        {
            quality = quality,
            qualityBand = authoredQualityBand ?? DefaultQualityBand(quality),
            servingRole = servingRole,
            nutrition = Mathf.Max(0f, nutritionAmount),
            mood = moodAmount,
            freshnessSeconds = Mathf.Max(0f, shelfLifeSeconds),
            preserved = isPreserved
        });
    }

    private static MealQualityBand DefaultQualityBand(MealQualityTier quality) =>
        quality switch
        {
            MealQualityTier.Lavish => MealQualityBand.Lavish,
            MealQualityTier.Fine => MealQualityBand.Decent,
            MealQualityTier.Preserved => MealQualityBand.Simple,
            _ => MealQualityBand.Simple
        };

    public void ConfigureMedicine(
        bool canTreatInjuries,
        float potency,
        float infection,
        float detox,
        float pain)
    {
        SetFeature(new MedicineItemFeature
        {
            supportsInjuryTreatment = canTreatInjuries,
            treatmentPotency = Mathf.Max(0.1f, potency),
            infectionReduction = Mathf.Max(0f, infection),
            detoxReduction = Mathf.Max(0f, detox),
            painReduction = Mathf.Max(0f, pain)
        });
    }

    public void ConfigureVaccine(string diseaseId, int doses)
    {
        SetFeature(new VaccineItemFeature
        {
            diseaseId = ItemDefinitionId.Normalize(diseaseId),
            doses = Mathf.Max(1, doses)
        });
    }

    public void ConfigurePathogenSample(string diseaseId)
    {
        SetFeature(new PathogenSampleItemFeature
        {
            diseaseId = ItemDefinitionId.Normalize(diseaseId)
        });
    }

    public void ConfigureMedicalProcedureSupply(string procedureId)
    {
        SetFeature(new MedicalProcedureSupplyItemFeature
        {
            procedureId = ItemDefinitionId.Normalize(procedureId)
        });
    }

    public void ConfigureCropTreatment(CropTreatmentKind kind)
    {
        SetFeature(new CropTreatmentItemFeature { treatmentKind = kind });
    }

    public void ConfigureSubstance(
        string substanceId,
        SubstanceUseClass useClass,
        float addictionChance,
        float overdoseChance,
        float toleranceGain,
        float withdrawalPerHour,
        float moodEffect,
        float workSpeedEffect,
        float combatEffect,
        float durationSeconds)
    {
        SetFeature(new SubstanceItemFeature
        {
            substanceId = ItemDefinitionId.Normalize(substanceId),
            useClass = useClass,
            addictionChance = Mathf.Clamp01(addictionChance),
            overdoseChance = Mathf.Clamp01(overdoseChance),
            toleranceGain = Mathf.Max(0f, toleranceGain),
            withdrawalPerHour = Mathf.Max(0f, withdrawalPerHour),
            moodEffect = moodEffect,
            workSpeedEffect = workSpeedEffect,
            combatEffect = combatEffect,
            durationSeconds = Mathf.Max(1f, durationSeconds)
        });
    }

    public void ClearSubstance()
    {
        RemoveFeature<SubstanceItemFeature>();
    }

    public void ConfigureMarketSaleRate(float saleRate)
    {
        SetFeature(new MarketItemFeature { saleRate = Mathf.Clamp01(saleRate) });
    }

    public void ConfigureFacilitySupply(
        float authoredFuelValue,
        bool canFeedFacilities,
        bool isSharedIntermediate)
    {
        ConfigureFacilitySupply(
            authoredFuelValue,
            0f,
            canFeedFacilities,
            isSharedIntermediate);
    }

    public void ConfigureFacilitySupply(
        float authoredFuelValue,
        float authoredNutritionValue,
        bool canFeedFacilities,
        bool isSharedIntermediate)
    {
        if (authoredFuelValue > 0f || authoredNutritionValue > 0f || canFeedFacilities)
        {
            SetFeature(new FacilitySupplyItemFeature
            {
                fuelValue = Mathf.Max(0f, authoredFuelValue),
                nutritionValue = Mathf.Max(0f, authoredNutritionValue),
                feedEligible = canFeedFacilities
            });
        }
        else
        {
            RemoveFeature<FacilitySupplyItemFeature>();
        }

        ProductionItemFeature production = GetFeatureOrDefault<ProductionItemFeature>()
            ?? new ProductionItemFeature();
        production.sharedIntermediate = isSharedIntermediate;
        SetFeature(production);
    }
#endif
}
