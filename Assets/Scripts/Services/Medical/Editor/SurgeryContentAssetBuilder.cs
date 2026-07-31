#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SurgeryContentAssetBuilder
{
    private const string BuildingRoot = "Assets/Resources/SO/Building/Medical";
    private const string SpriteRoot = "Assets/Images/MedicalFacilities";
    private const string AnatomyRoot = "Assets/Resources/SO/Medical/Anatomy";
    private const string ProcedureRoot = "Assets/Resources/SO/Medical/Procedures";
    private const string RecipeRoot = "Assets/Resources/SO/Economy/Recipes";

    private sealed class FacilitySpec
    {
        public string Code;
        public int Id;
        public string Name;
        public int Width;
        public int Cost;
        public int ConstructionWork;
        public Color32 Accent;
        public BuildingAbility SurgicalAbility;
        public FacilityWorkType WorkTypes;
        public bool TreatsPatients;
        public bool StoresOrgans;
        public bool ConsumesFuel;
    }

    private sealed class ProcedureSpec
    {
        public string Id;
        public string Name;
        public string Description;
        public SurgicalProcedureKind Kind;
        public string ResearchId;
        public SurgeryFacilityTag FacilityTags;
        public float Work;
        public float Difficulty;
        public float Infection;
        public float Bleeding;
        public bool Anesthesia = true;
        public bool Restraint = true;
        public bool Living = true;
        public bool Corpse;
        public bool Wildlife;
        public SurgicalMaterialRequirement[] Materials = Array.Empty<SurgicalMaterialRequirement>();
        public SurgicalProcedureEffect[] Effects = Array.Empty<SurgicalProcedureEffect>();
    }

    [MenuItem("DungeonStory/Content/Rebuild Surgery And Transplant Content")]
    public static void RebuildAll()
    {
        EnsureFolder(BuildingRoot);
        EnsureFolder(SpriteRoot);
        EnsureFolder(AnatomyRoot);
        EnsureFolder(ProcedureRoot);
        EnsureFolder(RecipeRoot);

        BuildFacilities();
        BuildAnatomyProfiles();
        BuildProcedures();
        BuildProstheticRecipes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ResearchProjectAssetBuilder.Rebuild();
        ValidateBuiltContent();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Surgery content rebuilt: 13 facilities, 6 anatomy profiles, 13 procedures, 6 research projects.");
    }

    private static void BuildProstheticRecipes()
    {
        CreateRecipe(
            9801,
            "recipe:surgery:prosthetic-arm",
            "철제 의수 조립",
            "강철 관절과 목재 지지대를 조립해 고유 보철 팔을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 2),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:cloth", 1)
            },
            SurgicalPartProductionOutputHandler.ProstheticArmOutputId);
        CreateRecipe(
            9802,
            "recipe:surgery:prosthetic-leg",
            "철제 의족 조립",
            "하중을 견디는 강철 골격과 가죽 완충재로 고유 보철 다리를 만든다.",
            42f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 3),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:leather", 1)
            },
            SurgicalPartProductionOutputHandler.ProstheticLegOutputId);
        CreateRecipe(
            9803,
            "recipe:surgery:artificial-eye",
            "인공 안구 조립",
            "정밀 금속 부품과 마나 결정을 결합해 고유 인공 안구를 만든다.",
            52f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 1),
                new ItemAmountDefinition("resource:mana-crystal", 1),
                new ItemAmountDefinition("medicine:advanced", 1)
            },
            SurgicalPartProductionOutputHandler.ArtificialEyeOutputId);
    }

    private static void CreateRecipe(
        int dataId,
        string recipeId,
        string displayName,
        string description,
        float requiredWork,
        IEnumerable<ItemAmountDefinition> inputs,
        string outputItemId)
    {
        string fileName = recipeId.Replace(':', '_').Replace('-', '_');
        string path = $"{RecipeRoot}/{fileName}.asset";
        ProductionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        recipe.id = dataId;
        recipe.Configure(
            recipeId,
            displayName,
            description,
            "m06",
            BuiltInWorkTypeIds.Craft.Value,
            "research:medical:prosthetics",
            requiredWork,
            inputs,
            new[] { new ProductionOutputDefinition(outputItemId, 1) });
        EditorUtility.SetDirty(recipe);
    }

    private static void BuildFacilities()
    {
        foreach (FacilitySpec spec in CreateFacilitySpecs())
        {
            string spritePath = $"{SpriteRoot}/{spec.Code}.png";
            WriteFacilitySprite(spec, spritePath);
            ConfigureSprite(spritePath);

            string assetPath = $"{BuildingRoot}/{spec.Code}_{spec.Name.Replace(" ", string.Empty)}.asset";
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
            if (building == null)
            {
                building = ScriptableObject.CreateInstance<BuildingSO>();
                AssetDatabase.CreateAsset(building, assetPath);
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            building.id = spec.Id;
            building.objectName = spec.Name;
            building.sprite = sprite;
            building.icon = sprite;
            building.width = Mathf.Max(1, spec.Width);
            building.height = 1;
            building.layer = GridLayer.Building;
            building.category = BuildingCategory.Crafting;
            building.horizontalDraggable = false;
            building.verticalDraggable = false;
            building.type = typeof(Facility);
            building.tiles = null;
            building.movementAnchorOffset = Vector2.zero;
            building.movementTravelTime = 1.2f;
            building.unlocked = false;
            building.ReplaceAbilities(CreateFacilityAbilities(spec));
            building.AbilityModules.EnsureStableIds();
            building.ValidateAbilitiesOrThrow();
            EditorUtility.SetDirty(building);
        }
    }

    private static BuildingAbilityCollection CreateFacilityAbilities(FacilitySpec spec)
    {
        BuildingAbilityCollection abilities = new BuildingAbilityCollection();
        abilities.Add(new BuildingFacilityPartAbility { code = spec.Code });
        abilities.Add(new BuildingSemanticTagsAbility
        {
            tags = new[] { "medical", "surgery", spec.Code.ToLowerInvariant() }
        });
        abilities.Add(new BuildingEconomyAbility
        {
            constructionCost = spec.Cost,
            maintenance = spec.ConsumesFuel ? 2 : 1,
            unlockPhase = 1,
            demolitionRefundRate = 0.5f
        });

        FacilityData facility = new FacilityData
        {
            roles = FacilityRole.Medical,
            capacity = 1,
            useDuration = 1.5f,
            requiredWorkers = spec.WorkTypes == FacilityWorkType.None ? 0 : 1,
            disabledWhenDamaged = true
        };
        facility.SetSupportedWorkTypeIds(ToWorkTypeIds(spec.WorkTypes));
        abilities.Add(new BuildingFacilityAbility { settings = facility });
        abilities.Add(new BuildingRoomRequirementAbility());
        abilities.Add(new BuildingInternalStockAbility
        {
            capacity = spec.StoresOrgans ? 8 : 12,
            restockRequestThreshold = spec.StoresOrgans ? 2 : 3
        });
        abilities.Add(new BuildingWorkAmountAbility
        {
            constructionWorkRequired = spec.ConstructionWork,
            repairWorkRequired = Mathf.Max(8f, spec.ConstructionWork * 0.2f),
            cleanWorkRequired = 8f,
            operateWorkRequired = 12f,
            constructionMaterialCategory = StockCategory.General,
            constructionMaterialAmount = Mathf.Max(2, spec.Cost / 25)
        });
        abilities.Add(new BuildingEvolutionAbility
        {
            settings = new FacilityEvolutionContributionData
            {
                contributesToRoomProfile = true,
                tags = new[] { "medical", "surgery", spec.Code.ToLowerInvariant() }
            }
        });

        if (spec.TreatsPatients)
        {
            abilities.Add(new BuildingMedicalAbility
            {
                workSeconds = 1.8f,
                severityReduction = spec.Code == "M11" ? 0.65f : 0.4f,
                requiresMedicine = true
            });
        }

        if (spec.StoresOrgans)
        {
            abilities.Add(new BuildingStorageAbility
            {
                category = StockCategory.Biological,
                capacity = 8,
                allCategories = false
            });
        }

        if (spec.ConsumesFuel)
        {
            abilities.Add(new BuildingFuelConsumerAbility
            {
                fuelPerRefuel = 1,
                workSeconds = 1f,
                warmth = 0f,
                lightSafety = 2f
            });
        }

        abilities.Add(spec.SurgicalAbility);
        return abilities;
    }

    private static IEnumerable<WorkTypeId> ToWorkTypeIds(FacilityWorkType mask)
    {
        if ((mask & FacilityWorkType.Surgery) != 0) yield return BuiltInWorkTypeIds.Surgery;
        if ((mask & FacilityWorkType.Treat) != 0) yield return BuiltInWorkTypeIds.Treat;
        if ((mask & FacilityWorkType.Craft) != 0) yield return BuiltInWorkTypeIds.Craft;
        if ((mask & FacilityWorkType.Clean) != 0) yield return BuiltInWorkTypeIds.Clean;
        if ((mask & FacilityWorkType.Refuel) != 0) yield return BuiltInWorkTypeIds.Refuel;
        if ((mask & FacilityWorkType.Haul) != 0) yield return BuiltInWorkTypeIds.Haul;
        if ((mask & FacilityWorkType.Rest) != 0) yield return BuiltInWorkTypeIds.Rest;
    }

    private static FacilitySpec[] CreateFacilitySpecs()
    {
        return new[]
        {
            Facility("M01", 9501, "응급 처치대", 2, 80, 36, new Color32(184, 73, 75, 255),
                new BuildingSurgeryTableAbility
                {
                    allowedProcedureTags = SurgeryFacilityTag.Emergency,
                    successBonus = -0.05f,
                    workSpeedMultiplier = 1.3f,
                    baseSterility = 0.12f
                }, FacilityWorkType.Surgery | FacilityWorkType.Treat, treats: true),
            Facility("M02", 9502, "해부대", 2, 110, 48, new Color32(112, 65, 101, 255),
                new BuildingAnatomyTableAbility
                {
                    successBonus = 0.02f,
                    workSpeedMultiplier = 1f
                }, FacilityWorkType.Surgery),
            Facility("M03", 9503, "외과 수술대", 3, 180, 72, new Color32(77, 157, 145, 255),
                new BuildingSurgeryTableAbility
                {
                    allowedProcedureTags = SurgeryFacilityTag.GeneralSurgery,
                    successBonus = 0.08f,
                    workSpeedMultiplier = 1f,
                    baseSterility = 0.35f
                }, FacilityWorkType.Surgery | FacilityWorkType.Treat, treats: true),
            Facility("M04", 9504, "세정대", 1, 95, 34, new Color32(85, 172, 190, 255),
                new BuildingSterilizationAbility
                {
                    sterilityBonus = 0.3f,
                    waterCost = 1,
                    disinfectantCost = 1
                }, FacilityWorkType.Clean | FacilityWorkType.Refuel),
            Facility("M05", 9505, "마취 장치", 1, 125, 42, new Color32(109, 124, 187, 255),
                new BuildingAnesthesiaAbility
                {
                    stabilityBonus = 0.35f,
                    anesthesiaItemId = SurgeryItemDefinitions.AnestheticId,
                    anesthesiaCost = 1
                }, FacilityWorkType.Refuel, fuel: true),
            Facility("M06", 9506, "보철 조립대", 2, 165, 64, new Color32(178, 139, 72, 255),
                new BuildingProstheticAssemblyAbility
                {
                    assemblySpeedMultiplier = 1.1f,
                    qualityBonus = 0.06f,
                    outputCapacity = 3
                }, FacilityWorkType.Craft),
            Facility("M07", 9507, "재활 보조대", 2, 135, 54, new Color32(98, 156, 113, 255),
                new BuildingRehabilitationAbility
                {
                    adaptationSpeedMultiplier = 1.6f,
                    rejectionReductionPerWork = 0.12f,
                    primaryOperatingFacility = true,
                    runeSuture = false
                }, FacilityWorkType.Surgery | FacilityWorkType.Treat, treats: true),
            Facility("M08", 9508, "장기 보관함", 1, 190, 58, new Color32(74, 138, 162, 255),
                new BuildingOrganStorageAbility
                {
                    preservationDays = 15f,
                    fuelPerDay = 1,
                    capacity = 8
                }, FacilityWorkType.Haul | FacilityWorkType.Refuel, stores: true, fuel: true),
            Facility("M09", 9509, "순환 이식대", 3, 290, 96, new Color32(160, 69, 91, 255),
                new BuildingTransplantSupportAbility
                {
                    circulationSupport = true,
                    immuneControl = false,
                    isolationRecovery = false,
                    successBonus = 0.14f,
                    rejectionReduction = 0.15f,
                    bloodCost = 1,
                    immunosuppressantCost = 0
                }, FacilityWorkType.Surgery | FacilityWorkType.Treat, treats: true),
            Facility("M10", 9510, "면역 조절기", 1, 230, 72, new Color32(141, 101, 171, 255),
                new BuildingTransplantSupportAbility
                {
                    circulationSupport = false,
                    immuneControl = true,
                    isolationRecovery = false,
                    successBonus = 0.08f,
                    rejectionReduction = 0.35f,
                    bloodCost = 0,
                    immunosuppressantCost = 1
                }, FacilityWorkType.Refuel, fuel: true),
            Facility("M11", 9511, "격리 회복 침상", 2, 210, 70, new Color32(85, 133, 118, 255),
                new BuildingTransplantSupportAbility
                {
                    circulationSupport = false,
                    immuneControl = false,
                    isolationRecovery = true,
                    successBonus = 0.05f,
                    rejectionReduction = 0.2f,
                    bloodCost = 0,
                    immunosuppressantCost = 0
                }, FacilityWorkType.Treat | FacilityWorkType.Rest, treats: true),
            Facility("M12", 9512, "비전 개조대", 3, 420, 128, new Color32(112, 63, 164, 255),
                new BuildingArcaneSurgeryAbility
                {
                    successBonus = 0.12f,
                    minimumMutationRisk = 0.08f,
                    manaCrystalCost = 2
                }, FacilityWorkType.Surgery | FacilityWorkType.Treat, treats: true, fuel: true),
            Facility("M13", 9513, "룬 봉합기", 1, 280, 86, new Color32(57, 160, 182, 255),
                new BuildingRehabilitationAbility
                {
                    adaptationSpeedMultiplier = 1.35f,
                    rejectionReductionPerWork = 0.15f,
                    primaryOperatingFacility = false,
                    runeSuture = true,
                    manaCrystalCost = 1
                }, FacilityWorkType.Refuel, fuel: true)
        };
    }

    private static FacilitySpec Facility(
        string code,
        int id,
        string name,
        int width,
        int cost,
        int work,
        Color32 accent,
        BuildingAbility ability,
        FacilityWorkType workTypes,
        bool treats = false,
        bool stores = false,
        bool fuel = false)
    {
        return new FacilitySpec
        {
            Code = code,
            Id = id,
            Name = name,
            Width = width,
            Cost = cost,
            ConstructionWork = work,
            Accent = accent,
            SurgicalAbility = ability,
            WorkTypes = workTypes,
            TreatsPatients = treats,
            StoresOrgans = stores,
            ConsumesFuel = fuel
        };
    }

    private static void BuildAnatomyProfiles()
    {
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateHumanoid());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateQuadruped());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateSlime());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateFungal());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateAvian());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateConstruct());
    }

    private static void BuildAnatomyAsset(AnatomyProfileDefinition definition)
    {
        string fileName = Sanitize(definition.ProfileId);
        string path = $"{AnatomyRoot}/{fileName}.asset";
        AnatomyProfileSO asset = AssetDatabase.LoadAssetAtPath<AnatomyProfileSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<AnatomyProfileSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.Configure(
            definition.ProfileId,
            definition.DisplayName,
            definition.AnatomyFamily,
            definition.SpeciesIds,
            definition.Nodes);
        EditorUtility.SetDirty(asset);
    }

    private static void BuildProcedures()
    {
        foreach (ProcedureSpec spec in CreateProcedureSpecs())
        {
            string path = $"{ProcedureRoot}/{Sanitize(spec.Id)}.asset";
            SurgicalProcedureSO asset =
                AssetDatabase.LoadAssetAtPath<SurgicalProcedureSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SurgicalProcedureSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Configure(
                spec.Id,
                spec.Name,
                spec.Description,
                spec.Kind,
                string.Empty,
                spec.ResearchId,
                spec.FacilityTags,
                spec.Work,
                spec.Difficulty,
                spec.Infection,
                spec.Bleeding,
                spec.Anesthesia,
                spec.Restraint,
                spec.Living,
                spec.Corpse,
                spec.Wildlife,
                spec.Materials,
                spec.Effects);
            EditorUtility.SetDirty(asset);
        }
    }

    private static ProcedureSpec[] CreateProcedureSpecs()
    {
        string medicine = DungeonItemCatalogSO.StockItemId(StockCategory.Medicine);
        string biological = DungeonItemCatalogSO.StockItemId(StockCategory.Biological);
        string general = DungeonItemCatalogSO.StockItemId(StockCategory.General);
        return new[]
        {
            Procedure("procedure:emergency-suture", "응급 봉합", "열린 상처를 닫고 출혈과 감염 위험을 낮춘다.",
                SurgicalProcedureKind.Suture, "research:survival:medical", SurgeryFacilityTag.Emergency,
                12f, 0.04f, 0.16f, 0.06f, false, false, true, false, true,
                Materials(Material(medicine, 1)), Effects(new HealSurgicalNodeEffect { health = 8f, infectionReduction = 8f })),
            Procedure("procedure:blood-transfusion", "수혈", "혈액 제제를 투여해 급격한 혈액 손실을 완화한다.",
                SurgicalProcedureKind.Transfusion, "research:survival:medical", SurgeryFacilityTag.Emergency,
                10f, 0.05f, 0.12f, 0.05f, false, false, true, false, true,
                Materials(Material(SurgeryItemDefinitions.BloodPackId, 1)), Effects(new HealSurgicalNodeEffect { health = 14f, infectionReduction = 2f })),
            Procedure("procedure:foreign-body-removal", "이물 제거", "상처 속 파편과 오염원을 제거한다.",
                SurgicalProcedureKind.RemoveForeignBody, "research:survival:medical", SurgeryFacilityTag.Emergency,
                16f, 0.08f, 0.14f, 0.1f, true, true, true, false, true,
                Materials(Material(SurgeryItemDefinitions.DisinfectantId, 1)), Effects(new HealSurgicalNodeEffect { health = 10f, infectionReduction = 16f })),
            Procedure("procedure:organ-repair", "장기 봉합", "손상된 기관을 절개해 직접 복원한다.",
                SurgicalProcedureKind.HealOrgan, "research:medical:surgery",
                SurgeryFacilityTag.GeneralSurgery | SurgeryFacilityTag.Sterilization | SurgeryFacilityTag.Anesthesia,
                34f, 0.14f, 0.1f, 0.14f, true, true, true, false, true,
                Materials(Material(medicine, 2), Material(SurgeryItemDefinitions.DisinfectantId, 1)),
                Effects(new HealSurgicalNodeEffect { health = 24f, infectionReduction = 20f })),
            Procedure("procedure:amputation", "괴사 부위 절단", "회복할 수 없는 부위를 제거해 감염 확산을 막는다.",
                SurgicalProcedureKind.Amputate, "research:medical:surgery",
                SurgeryFacilityTag.GeneralSurgery | SurgeryFacilityTag.Sterilization | SurgeryFacilityTag.Anesthesia,
                38f, 0.18f, 0.12f, 0.24f, true, true, true, false, true,
                Materials(Material(medicine, 2)), Effects(new RemoveSurgicalNodeEffect { createExtractedPart = false })),
            Procedure("procedure:corpse-organ-extraction", "사체 장기 적출", "신선한 사체에서 손상되지 않은 기관을 분리한다.",
                SurgicalProcedureKind.ExtractOrgan, "research:medical:anatomy", SurgeryFacilityTag.Anatomy,
                26f, 0.08f, 0f, 0f, false, false, false, true, true,
                Materials(Material(SurgeryItemDefinitions.DisinfectantId, 1)),
                Effects(new RemoveSurgicalNodeEffect { createExtractedPart = true })),
            Procedure("procedure:live-organ-extraction", "생체 장기 적출", "살아 있는 대상에게서 기관을 적출한다. 대체 기관이 없으면 치명적일 수 있다.",
                SurgicalProcedureKind.ExtractOrgan, "research:medical:surgery",
                SurgeryFacilityTag.GeneralSurgery | SurgeryFacilityTag.Sterilization | SurgeryFacilityTag.Anesthesia,
                42f, 0.25f, 0.16f, 0.3f, true, true, true, false, true,
                Materials(Material(medicine, 2), Material(biological, 1)),
                Effects(new RemoveSurgicalNodeEffect { createExtractedPart = true })),
            Procedure("procedure:natural-organ-transplant", "장기 이식", "결손되거나 손상된 기관을 보존 장기로 교체한다.",
                SurgicalProcedureKind.TransplantOrgan, "research:medical:xenotransplant",
                SurgeryFacilityTag.Transplant | SurgeryFacilityTag.Sterilization | SurgeryFacilityTag.Anesthesia,
                58f, 0.2f, 0.14f, 0.22f, true, true, true, false, false,
                Materials(Material(medicine, 2), Material(SurgeryItemDefinitions.ImmunosuppressantId, 1)),
                Effects(new InstallSurgicalPartEffect { partKind = SurgicalPartKind.NaturalOrgan, efficiency = 1f },
                    new ApplySurgicalBurdenEffect { rejection = 8f, infection = 4f })),
            Procedure("procedure:prosthetic-installation", "보철 설치", "결손 부위에 제작된 보철을 결합한다.",
                SurgicalProcedureKind.InstallProsthetic, "research:medical:prosthetics",
                SurgeryFacilityTag.GeneralSurgery | SurgeryFacilityTag.Sterilization | SurgeryFacilityTag.Anesthesia,
                44f, 0.14f, 0.1f, 0.16f, true, true, true, false, false,
                Materials(Material(general, 2)), Effects(new InstallSurgicalPartEffect { partKind = SurgicalPartKind.Prosthetic, efficiency = 0.85f })),
            Procedure("procedure:implant-installation", "인공 안구 설치", "감각 기관에 정밀 임플란트를 연결한다.",
                SurgicalProcedureKind.InstallImplant, "research:medical:prosthetics",
                SurgeryFacilityTag.GeneralSurgery | SurgeryFacilityTag.Sterilization | SurgeryFacilityTag.Anesthesia,
                50f, 0.18f, 0.12f, 0.18f, true, true, true, false, false,
                Materials(Material(general, 2), Material(medicine, 1)),
                Effects(new InstallSurgicalPartEffect { partKind = SurgicalPartKind.Implant, efficiency = 1.05f })),
            Procedure("procedure:xenograft", "이종 장기 이식", "다른 종의 장기를 이식하고 초기 거부 반응을 억제한다.",
                SurgicalProcedureKind.TransplantOrgan, "research:medical:xenotransplant",
                SurgeryFacilityTag.Transplant | SurgeryFacilityTag.ImmuneControl | SurgeryFacilityTag.Sterilization | SurgeryFacilityTag.Anesthesia,
                72f, 0.28f, 0.18f, 0.28f, true, true, true, false, false,
                Materials(Material(medicine, 3), Material(SurgeryItemDefinitions.ImmunosuppressantId, 2)),
                Effects(new InstallSurgicalPartEffect { partKind = SurgicalPartKind.NaturalOrgan, efficiency = 1.08f },
                    new ApplySurgicalBurdenEffect { rejection = 24f, infection = 8f, mutation = 4f })),
            Procedure("procedure:arcane-modification", "이형 개조", "비전 기관을 결합해 신체의 한계를 넘기지만 돌연변이 부담을 남긴다.",
                SurgicalProcedureKind.ArcaneModification, "research:medical:aberrant-augmentation",
                SurgeryFacilityTag.ArcaneSurgery | SurgeryFacilityTag.RuneSuture | SurgeryFacilityTag.Anesthesia,
                88f, 0.32f, 0.18f, 0.24f, true, true, true, false, false,
                Materials(Material(DungeonItemCatalogSO.StockItemId(StockCategory.Mana), 3), Material(medicine, 2)),
                Effects(new InstallSurgicalPartEffect { partKind = SurgicalPartKind.ArcaneGraft, efficiency = 1.2f },
                    new ApplySurgicalBurdenEffect { rejection = 12f, infection = 6f, mutation = 18f })),
            Procedure("procedure:rehabilitation", "보철 재활", "보철 적응 훈련과 상처 관리를 통해 움직임과 조작 능력을 회복한다.",
                SurgicalProcedureKind.Rehabilitation, "research:medical:prosthetics", SurgeryFacilityTag.Rehabilitation,
                30f, 0.03f, 0.04f, 0.02f, false, false, true, false, false,
                Materials(Material(medicine, 1)), Effects(
                    new HealSurgicalNodeEffect
                    {
                        health = 16f,
                        infectionReduction = 6f
                    },
                    new ReduceSurgicalBurdenEffect
                    {
                        rejection = 18f,
                        mutation = 4f,
                        infection = 6f
                    }))
        };
    }

    private static ProcedureSpec Procedure(
        string id,
        string name,
        string description,
        SurgicalProcedureKind kind,
        string researchId,
        SurgeryFacilityTag tags,
        float work,
        float difficulty,
        float infection,
        float bleeding,
        bool anesthesia,
        bool restraint,
        bool living,
        bool corpse,
        bool wildlife,
        SurgicalMaterialRequirement[] materials,
        SurgicalProcedureEffect[] effects)
    {
        return new ProcedureSpec
        {
            Id = id,
            Name = name,
            Description = description,
            Kind = kind,
            ResearchId = researchId,
            FacilityTags = tags,
            Work = work,
            Difficulty = difficulty,
            Infection = infection,
            Bleeding = bleeding,
            Anesthesia = anesthesia,
            Restraint = restraint,
            Living = living,
            Corpse = corpse,
            Wildlife = wildlife,
            Materials = materials,
            Effects = effects
        };
    }

    private static SurgicalMaterialRequirement Material(string itemId, int quantity)
    {
        return new SurgicalMaterialRequirement
        {
            itemId = itemId,
            quantity = quantity,
            optional = false
        };
    }

    private static SurgicalMaterialRequirement[] Materials(
        params SurgicalMaterialRequirement[] values) => values;

    private static SurgicalProcedureEffect[] Effects(
        params SurgicalProcedureEffect[] values) => values;

    private static void ValidateBuiltContent()
    {
        BuildingSO[] buildings = LoadAssets<BuildingSO>(BuildingRoot);
        SurgicalProcedureSO[] procedures = LoadAssets<SurgicalProcedureSO>(ProcedureRoot);
        AnatomyProfileSO[] anatomy = LoadAssets<AnatomyProfileSO>(AnatomyRoot);
        ResearchProjectSO[] research = LoadAssets<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");

        if (buildings.Length != 13)
        {
            throw new InvalidOperationException($"Expected 13 surgery facilities, found {buildings.Length}.");
        }
        if (procedures.Length != 13)
        {
            throw new InvalidOperationException($"Expected 13 surgical procedures, found {procedures.Length}.");
        }
        if (anatomy.Length != 6)
        {
            throw new InvalidOperationException($"Expected 6 anatomy profiles, found {anatomy.Length}.");
        }
        if (research.Length < 78)
        {
            throw new InvalidOperationException(
                $"Expected at least 78 research projects, found {research.Length}.");
        }

        IReadOnlyList<string> anatomyErrors =
            new ResourceAnatomyProfileCatalog(anatomy).Validate();
        IReadOnlyList<string> procedureErrors =
            new ResourceSurgicalProcedureCatalog(procedures).Validate();
        IReadOnlyList<string> researchErrors =
            new ResourceResearchProjectCatalog(research).Validate();
        string[] errors = anatomyErrors
            .Concat(procedureErrors)
            .Concat(researchErrors)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }

        foreach (BuildingSO building in buildings)
        {
            building.ValidateAbilitiesOrThrow();
            if (!building.Abilities.Any(ability => ability is ISurgicalFacilityAbility))
            {
                throw new InvalidOperationException($"{building.objectName}: surgical ability is missing.");
            }
        }
    }

    private static T[] LoadAssets<T>(string root)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static void WriteFacilitySprite(FacilitySpec spec, string path)
    {
        const int width = 48;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = Enumerable.Repeat(new Color32(0, 0, 0, 0), width * height).ToArray();
        Color32 outline = new Color32(24, 25, 34, 255);
        Color32 metal = new Color32(78, 83, 99, 255);
        Color32 highlight = new Color32(158, 164, 177, 255);
        Color32 sheet = new Color32(72, 118, 121, 255);

        Rect(4, 4, 40, 5, outline);
        Rect(6, 6, 36, 4, metal);
        Rect(8, 9, 32, 7, sheet);
        Rect(6, 16, 36, 3, outline);
        Rect(8, 19, 4, 10, outline);
        Rect(36, 19, 4, 10, outline);
        Rect(9, 19, 2, 8, metal);
        Rect(37, 19, 2, 8, metal);
        Rect(10, 10, 28, 2, new Color32(112, 169, 166, 255));

        int variant = spec.Id - 9500;
        if (variant is 4 or 5 or 8 or 10 or 13)
        {
            Rect(18, 18, 12, 10, outline);
            Rect(20, 20, 8, 6, spec.Accent);
            Rect(22, 22, 4, 2, highlight);
        }
        if (variant is 1 or 3 or 9 or 11 or 12)
        {
            Rect(22, 19, 4, 9, spec.Accent);
            Rect(18, 22, 12, 3, spec.Accent);
        }
        if (variant is 2 or 6 or 7)
        {
            Rect(14, 20, 20, 7, outline);
            Rect(16, 22, 16, 3, spec.Accent);
        }
        if (variant == 8)
        {
            Rect(12, 7, 24, 20, outline);
            Rect(14, 9, 20, 16, new Color32(58, 82, 101, 255));
            Rect(22, 11, 4, 12, spec.Accent);
        }
        if (variant is 12 or 13)
        {
            Diamond(24, 19, 7, spec.Accent);
            Diamond(24, 19, 3, new Color32(151, 228, 220, 255));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        void Rect(int x, int y, int w, int h, Color32 color)
        {
            for (int py = Mathf.Max(0, y); py < Mathf.Min(height, y + h); py++)
            {
                for (int px = Mathf.Max(0, x); px < Mathf.Min(width, x + w); px++)
                {
                    pixels[py * width + px] = color;
                }
            }
        }

        void Diamond(int cx, int cy, int radius, Color32 color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int half = radius - Mathf.Abs(y);
                Rect(cx - half, cy + y, half * 2 + 1, 1, color);
            }
        }
    }

    private static void ConfigureSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Cannot import medical facility sprite: {path}");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/');
        string current = "Assets";
        foreach (string segment in normalized.Substring("Assets/".Length).Split('/'))
        {
            string next = $"{current}/{segment}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segment);
            }
            current = next;
        }
    }

    private static string Sanitize(string value)
    {
        return (value ?? string.Empty)
            .Replace(':', '_')
            .Replace('/', '_')
            .Replace(' ', '_');
    }
}
#endif
