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
    private const string ConditionLexiconRoot = "Assets/Resources/SO/Medical/ConditionLexicons";
    private const string RecipeRoot = "Assets/Resources/SO/Economy/Recipes";
    private const string ItemDefinitionRoot =
        "Assets/Resources/SO/Items/Definitions";
    private const string GameplayEffectRoot =
        "Assets/Resources/SO/V26/Effects/Definitions";
    private const string StandardMedicineItemId = "medicine:standard";
    private const string BloodItemId = "resource:blood";
    private const string LumberItemId = "material:lumber";
    private const string ManaCrystalItemId = "resource:mana-crystal";
    private const string ProstheticAssemblyAssetPath =
        "Assets/Resources/SO/Building/Medical/M06_보철조립대.asset";
    private const string ItemDefinitionCatalogAssetPath =
        "Assets/Resources/SO/Content/ItemDefinitionCatalog.asset";
    private const string DomainContentCatalogAssetPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private static readonly string[] Wim023GeneratedPartIds =
    {
        "surgery:prosthetic:hypha-core",
        "surgery:prosthetic:wing:left",
        "surgery:prosthetic:balance-tail",
        "surgery:prosthetic:torso",
        "surgery:prosthetic:night-eye:left",
        "surgery:prosthetic:heart/variant/orc-combat-heart",
        "surgery:prosthetic:leg:left/variant/beastkin-sprint-joint",
        "surgery:prosthetic:heat-sac/variant/demon-heat-sac",
        "surgery:prosthetic:balance-tail/variant/kobold-tail-balance",
        "surgery:prosthetic:brain/variant/human-neural-assist",
        "surgery:prosthetic:hand:left"
    };
    private static readonly string[] Wim023RecipeIds =
    {
        "recipe:surgery:prosthetic-arm",
        "recipe:surgery:prosthetic-leg",
        "recipe:surgery:artificial-eye",
        "recipe:surgery:pseudopods",
        "recipe:surgery:hypha-core",
        "recipe:surgery:wing",
        "recipe:surgery:balance-tail",
        "recipe:surgery:reinforced-torso",
        "recipe:surgery:heart-augmentation",
        "recipe:surgery:night-eye",
        "recipe:surgery:heat-sac",
        "recipe:surgery:precision-hand",
        "recipe:surgery:brain-assist",
        "recipe:surgery:sprint-joint",
        "recipe:surgery:tail-balance-augmentation"
    };
    // The three pre-existing limb/eye recipes retain their authored profile;
    // these are the twelve WIM-023 additions that must match it.
    private static readonly string[] Wim023GeneratedRecipeIds =
    {
        "recipe:surgery:pseudopods",
        "recipe:surgery:hypha-core",
        "recipe:surgery:wing",
        "recipe:surgery:balance-tail",
        "recipe:surgery:reinforced-torso",
        "recipe:surgery:heart-augmentation",
        "recipe:surgery:night-eye",
        "recipe:surgery:heat-sac",
        "recipe:surgery:precision-hand",
        "recipe:surgery:brain-assist",
        "recipe:surgery:sprint-joint",
        "recipe:surgery:tail-balance-augmentation"
    };
    private static readonly string[] Wim023GeneratedEffectFileNames =
    {
        "effect_character_maximum-health_multiply.asset",
        "effect_combat_evasion-chance_add-flat.asset"
    };
    internal const long OrganStorageMassCapacityGrams = 12_500L;

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
        public string ProductionWorkstationTag;
        public int ProductionOutputBufferCycleCapacity;
        public bool AllowsProductionOverflowDump;
    }

    private sealed class ProcedureSpec
    {
        public string Id;
        public string Name;
        public string Description;
        public SurgicalProcedureKind Kind;
        public string TargetNodeId = string.Empty;
        public string ResearchId;
        public SurgeryFacilityTag FacilityTags;
        public int PrimaryFacilityDefinitionId;
        public float Work;
        public float Difficulty;
        public float Infection;
        public float Bleeding;
        public bool Anesthesia = true;
        public bool Restraint = true;
        public bool Living = true;
        public bool Corpse;
        public bool Wildlife;
        public MedicalProcedureFamily Family = MedicalProcedureFamily.Biological;
        public MedicalProcedureUrgency Urgency = MedicalProcedureUrgency.Required;
        public string[] AnatomyFamilies = Array.Empty<string>();
        public string[] SpeciesIds = Array.Empty<string>();
        public SurgicalMaterialRequirement[] Materials = Array.Empty<SurgicalMaterialRequirement>();
        public SurgicalProcedureEffect[] Effects = Array.Empty<SurgicalProcedureEffect>();
    }

    [MenuItem("DungeonStory/Content/Rebuild Surgery And Transplant Content")]
    public static void RebuildAll()
    {
        EnsureAssets();
        GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        GameContentCatalogAssetBuilder.ReindexProductionRecipes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ResearchProjectAssetBuilder.Rebuild();
        ValidateBuiltContent();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Surgery content rebuilt: 13 facilities, 12 anatomy profiles, 7 condition lexicons, 47 procedures including 5 age treatments, 12 medical research projects.");
    }

    public static void EnsureAssets()
    {
        EnsureFolder(BuildingRoot);
        EnsureFolder(SpriteRoot);
        EnsureFolder(AnatomyRoot);
        EnsureFolder(ProcedureRoot);
        EnsureFolder(ConditionLexiconRoot);
        EnsureFolder(RecipeRoot);
        EnsureFolder(ItemDefinitionRoot);

        BuildFacilities();
        BuildAnatomyProfiles();
        BuildConditionLexicons();
        BuildProcedures();
        BuildSpeciesSurgicalParts();
        BuildProstheticRecipes();
        BuildAuthoredInstalledPartEffects();
        V23RecipeProcessClassAuthoring.NormalizeRecipeWorkUnder(RecipeRoot);
    }

    [MenuItem(
        "DungeonStory/Content/WIM/Apply WIM-021-022 Medical Effects")]
    public static void ApplyWim021022MedicalEffects()
    {
        const string sutureId = "procedure:emergency-suture";
        const string transfusionId = "procedure:blood-transfusion";
        EnsureFolder(ProcedureRoot);

        ProcedureSpec[] specs = CreateProcedureSpecs()
            .Where(spec => string.Equals(spec.Id, sutureId, StringComparison.Ordinal)
                || string.Equals(
                    spec.Id,
                    transfusionId,
                    StringComparison.Ordinal))
            .ToArray();
        if (specs.Length != 2)
        {
            throw new InvalidOperationException(
                "WIM-021-022 publication requires exactly two procedure specs.");
        }

        foreach (ProcedureSpec spec in specs)
        {
            BuildProcedure(spec);
        }

        string[] paths = specs
            .Select(spec => $"{ProcedureRoot}/{Sanitize(spec.Id)}.asset")
            .ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(
            paths,
            ForceReserializeAssetsOptions.ReserializeAssets);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateWim021022MedicalEffects(paths);
        Debug.Log(
            "WIM-021-022 medical effects published: emergency suture and blood transfusion.");
    }

    private static void ValidateWim021022MedicalEffects(
        IReadOnlyList<string> paths)
    {
        SurgicalProcedureSO[] procedures = paths
            .Select(path =>
                AssetDatabase.LoadAssetAtPath<SurgicalProcedureSO>(path))
            .ToArray();
        SurgicalProcedureSO suture = procedures.Single(procedure =>
            procedure != null
            && string.Equals(
                procedure.ProcedureId,
                "procedure:emergency-suture",
                StringComparison.Ordinal));
        SurgicalProcedureSO transfusion = procedures.Single(procedure =>
            procedure != null
            && string.Equals(
                procedure.ProcedureId,
                "procedure:blood-transfusion",
                StringComparison.Ordinal));
        HealSurgicalNodeEffect sutureHeal = suture.Effects
            .OfType<HealSurgicalNodeEffect>()
            .Single();
        RecoverBloodLossEffect recovery = transfusion.Effects
            .OfType<RecoverBloodLossEffect>()
            .Single();
        HealSurgicalNodeEffect transfusionHeal = transfusion.Effects
            .OfType<HealSurgicalNodeEffect>()
            .Single();
        if (suture.Effects.Count != 2
            || suture.Effects.OfType<StopSurgicalNodeBleedingEffect>().Count() != 1
            || !Mathf.Approximately(sutureHeal.health, 8f)
            || !Mathf.Approximately(sutureHeal.infectionReduction, 8f)
            || transfusion.AllowsWildlife
            || transfusion.Effects.Count != 2
            || !Mathf.Approximately(transfusionHeal.health, 14f)
            || !Mathf.Approximately(transfusionHeal.infectionReduction, 2f)
            || !Mathf.Approximately(recovery.amount, 25f))
        {
            throw new InvalidOperationException(
                "WIM-021-022 procedure publication validation failed.");
        }
    }

    [MenuItem(
        "DungeonStory/Content/WIM/Apply WIM-023 Procedure Semantics")]
    public static void ApplyWim023ProcedureSemantics()
    {
        string[] targetPaths = GetWim023PublicationTargetPaths();
        RejectDirtyWim023PublicationTargets(targetPaths);

        EnsureFolder(AnatomyRoot);
        EnsureFolder(ProcedureRoot);
        EnsureFolder(RecipeRoot);
        EnsureFolder(ItemDefinitionRoot);
        EnsureFolder(GameplayEffectRoot);

        BuildAnatomyAsset(AnatomyProfileDefaults.CreateAvian());
        foreach (ProcedureSpec spec in CreateSpeciesProcedureSpecs()
                     .Where(value => value.Family != MedicalProcedureFamily.Construct))
        {
            BuildProcedure(spec);
        }
        BuildSpeciesSurgicalParts();
        BuildProstheticRecipes();
        BuildAuthoredInstalledPartEffects();
        V23RecipeProcessClassAuthoring.NormalizeRecipeWorkUnder(RecipeRoot);
        GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        GameContentCatalogAssetBuilder.ReindexProductionRecipes();
        AppendWim023GeneratedEffectsToDomainCatalog();

        SaveWim023PublicationTargets(targetPaths);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateBuiltContent();
        Debug.Log(
            "WIM-023 procedure semantics published: 24 classified procedures, "
            + "15 installed-part procedures, 13 exact parts, 15 M06 recipes.");
    }

    private static string[] GetWim023PublicationTargetPaths()
    {
        AnatomyProfileDefinition avian = AnatomyProfileDefaults.CreateAvian();
        return CreateSpeciesProcedureSpecs()
            .Where(spec => spec.Family != MedicalProcedureFamily.Construct)
            .Select(spec => $"{ProcedureRoot}/{Sanitize(spec.Id)}.asset")
            .Concat(new[]
            {
                $"{AnatomyRoot}/{Sanitize(avian.ProfileId)}.asset",
                ItemDefinitionCatalogAssetPath,
                DomainContentCatalogAssetPath
            })
            .Concat(Wim023GeneratedPartIds.Select(
                itemId => $"{ItemDefinitionRoot}/{Sanitize(itemId)}.asset"))
            .Concat(Wim023RecipeIds.Select(GetRecipeAssetPath))
            .Concat(Wim023GeneratedEffectFileNames.Select(
                fileName => $"{GameplayEffectRoot}/{fileName}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void RejectDirtyWim023PublicationTargets(
        IEnumerable<string> targetPaths)
    {
        string[] dirtyPaths = (targetPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Where(path =>
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                return asset != null && EditorUtility.IsDirty(asset);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (dirtyPaths.Length > 0)
        {
            throw new InvalidOperationException(
                "WIM-023 publication refused to overwrite unsaved target assets:\n"
                + string.Join("\n", dirtyPaths));
        }
    }

    private static void SaveWim023PublicationTargets(
        IEnumerable<string> targetPaths)
    {
        foreach (string path in (targetPaths ?? Array.Empty<string>())
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }

    private static void AppendWim023GeneratedEffectsToDomainCatalog()
    {
        GameDomainContentCatalogSO domainCatalog =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(
                DomainContentCatalogAssetPath)
            ?? throw new InvalidOperationException(
                "Required domain content catalog is missing at '"
                + DomainContentCatalogAssetPath + "'.");
        List<ScriptableObject> definitions = domainCatalog.Definitions.ToList();
        bool changed = false;
        foreach (string fileName in Wim023GeneratedEffectFileNames)
        {
            string effectPath = $"{GameplayEffectRoot}/{fileName}";
            GameplayEffectDefinitionSO effect =
                AssetDatabase.LoadAssetAtPath<GameplayEffectDefinitionSO>(effectPath)
                ?? throw new InvalidOperationException(
                    "WIM-023 domain catalog requires generated effect at '"
                    + effectPath + "'.");
            if (definitions.Contains(effect))
            {
                continue;
            }

            definitions.Add(effect);
            changed = true;
        }

        if (changed)
        {
            domainCatalog.SetDefinitions(definitions);
            EditorUtility.SetDirty(domainCatalog);
        }

        IReadOnlyList<string> errors = domainCatalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "WIM-023 generated-effect domain catalog validation failed:\n"
                + string.Join("\n", errors));
        }
    }

    private static string GetRecipeAssetPath(string recipeId)
    {
        string fileName = (recipeId ?? string.Empty)
            .Replace(':', '_')
            .Replace('-', '_');
        return $"{RecipeRoot}/{fileName}.asset";
    }

    [MenuItem(
        "DungeonStory/Content/V27 Apply M06 Production Authority")]
    public static void ApplyProstheticProductionAuthority()
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            ProstheticAssemblyAssetPath);
        if (building == null)
        {
            throw new InvalidOperationException(
                "M06 prosthetic assembly asset is missing.");
        }

        if (!HasExactProstheticProductionAuthority(building))
        {
            building.ReplaceAbilities(
                CreateProstheticProductionAuthorityAbilities(
                    building.AbilityModules));
            building.AbilityModules.EnsureStableIds();
            building.ValidateAbilitiesOrThrow();
            EditorUtility.SetDirty(building);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(
                new[] { ProstheticAssemblyAssetPath },
                ForceReserializeAssetsOptions.ReserializeAssets);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        ValidateBuiltContent();
    }

    private static BuildingAbilityCollection
        CreateProstheticProductionAuthorityAbilities(
            BuildingAbilityCollection existingAbilities)
    {
        if (existingAbilities == null)
        {
            throw new InvalidOperationException(
                "M06 prosthetic assembly abilities are missing.");
        }

        BuildingAbilityCollection abilities = new();
        bool inserted = false;
        foreach (BuildingAbility existing in existingAbilities.Items)
        {
            if (existing is BuildingProductionWorkstationAbility
                or BuildingProductionBufferAbility)
            {
                continue;
            }

            if (!inserted && existing is BuildingProstheticAssemblyAbility)
            {
                abilities.Add(CreateProstheticWorkstationAbility());
                abilities.Add(CreateProstheticBufferAbility());
                inserted = true;
            }
            abilities.Add(existing);
        }

        if (!inserted)
        {
            throw new InvalidOperationException(
                "M06 prosthetic assembly ability is missing.");
        }

        return abilities;
    }

    private static BuildingProductionWorkstationAbility
        CreateProstheticWorkstationAbility() => new()
        {
            workstationTag = "m06",
            stockSensorInstallationItemId = ProductionBillRuntime.StockSensorItemId
        };

    private static BuildingProductionBufferAbility
        CreateProstheticBufferAbility() => new()
        {
            defaultBatchCapacity = 4,
            physicalOutputBufferCycleCapacity = 4,
            allowOverflowDump = false
        };

    private static bool HasExactProstheticProductionAuthority(
        BuildingSO building)
    {
        BuildingProductionWorkstationAbility workstation =
            building?.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            building?.GetProductionBufferAbility();
        return workstation != null
            && string.Equals(
                workstation.WorkstationTag,
                "m06",
                StringComparison.Ordinal)
            && string.Equals(
                workstation.StockSensorInstallationItemId,
                ProductionBillRuntime.StockSensorItemId,
                StringComparison.Ordinal)
            && buffer != null
            && buffer.defaultBatchCapacity == 4
            && buffer.physicalOutputBufferCycleCapacity == 4
            && !buffer.allowOverflowDump;
    }

    private static void BuildConditionLexicons()
    {
        CreateConditionLexicon("condition:biological", "humanoid",
            new[] { "human", "orc", "beastkin", "kobold" },
            "출혈", "감염", "쇼크", "골절·파열", "장기 정지", "거부 반응", "치료·수술", "치료");
        CreateConditionLexicon("condition:vampire", "humanoid", new[] { "vampire" },
            "혈액 고갈", "부패 감염", "혈류 쇼크", "골절·파열", "혈액낭 정지", "혈핵 거부", "혈술 처치 필요", "혈술 처치");
        CreateConditionLexicon("condition:demon", "humanoid", new[] { "demon" },
            "마력 누출", "룬 오염", "룬 붕괴", "각질 균열", "마핵 정지", "룬 비호환", "마핵 시술 필요", "룬 봉합");
        CreateConditionLexicon("condition:slime", "slime", new[] { "slime" },
            "점액 누출", "점액 오염", "응집 불안정", "외피 찢김", "핵 손상", "이질 점액 거부", "안정화·재성형", "재성형");
        CreateConditionLexicon("condition:myconid", "fungal", new[] { "myconid" },
            "수액·포자 누출", "부패·포자 오염", "군체 불안정", "균사 절단", "균핵 괴사", "접목 불화", "균사 처치", "접목");
        CreateConditionLexicon("condition:harpy", "avian", new[] { "harpy" },
            "출혈", "기낭 감염", "호흡 쇼크", "기낭·날개 파열", "기낭 정지", "이식 불화", "조류 처치 필요", "날개 고정");
        CreateConditionLexicon("condition:golem", "construct", new[] { "golem" },
            "냉각수 누수", "회로 오염·부식", "과부하", "외장 균열", "핵 균열·서보 파손", "부품 비호환", "정비·부품 교체", "정비");
    }

    private static void CreateConditionLexicon(
        string id,
        string family,
        string[] species,
        string fluidLoss,
        string contamination,
        string overstrain,
        string fracture,
        string partFailure,
        string compatibility,
        string treatmentRequired,
        string treatmentVerb)
    {
        string path = $"{ConditionLexiconRoot}/{id.Replace(':', '_')}.asset";
        AnatomyConditionLexiconSO asset =
            AssetDatabase.LoadAssetAtPath<AnatomyConditionLexiconSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<AnatomyConditionLexiconSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.Configure(id, family, species, new[]
        {
            ConditionEntry(AnatomyConditionKind.FluidLoss, fluidLoss, treatmentVerb),
            ConditionEntry(AnatomyConditionKind.Contamination, contamination, treatmentVerb),
            ConditionEntry(AnatomyConditionKind.Overstrain, overstrain, treatmentVerb),
            ConditionEntry(AnatomyConditionKind.Fracture, fracture, treatmentVerb),
            ConditionEntry(AnatomyConditionKind.PartFailure, partFailure, treatmentVerb),
            ConditionEntry(AnatomyConditionKind.CompatibilityFailure, compatibility, treatmentVerb),
            ConditionEntry(AnatomyConditionKind.TreatmentRequired, treatmentRequired, treatmentVerb)
        });
        EditorUtility.SetDirty(asset);
    }

    private static AnatomyConditionLexiconEntry ConditionEntry(
        AnatomyConditionKind condition,
        string label,
        string treatmentVerb)
    {
        string stable = condition.ToString().ToLowerInvariant();
        return new AnatomyConditionLexiconEntry
        {
            condition = condition,
            label = label,
            treatmentVerb = treatmentVerb,
            iconId = "condition:" + stable,
            vfxId = "medical:" + stable
        };
    }

    private static void BuildSpeciesSurgicalParts()
    {
        EnsureSurgicalPart(
            "surgery:prosthetic:hypha-core",
            "균핵 보철",
            "균사 구조와 접속하는 인공 균핵.",
            180);
        EnsureSurgicalPart(
            "surgery:prosthetic:wing:left",
            "왼날개 보철",
            "조류형 왼날개의 비행·조작 구조를 대체하는 보철.",
            260);
        EnsureSurgicalPart(
            "surgery:prosthetic:balance-tail",
            "평형 꼬리 보철",
            "꼬리 슬롯에 장착해 균형 기능을 대체하는 정밀 보철.",
            240);
        EnsureSurgicalPart(
            "surgery:prosthetic:torso",
            "골격 보강 몸통",
            "몸통 슬롯에 장착하는 하중 분산 골격 보철.",
            320);
        EnsureSurgicalPart(
            "surgery:prosthetic:night-eye:left",
            "왼쪽 야간안 보철",
            "뱀파이어 야간안 슬롯에 장착하는 인공 감각기.",
            220);
        EnsureSurgicalPart(
            "surgery:prosthetic:heart/variant/orc-combat-heart",
            "오크 전투 심장",
            "오크 심장 슬롯에 장착하는 강화 펌프 보철.",
            240);
        EnsureSurgicalPart(
            "surgery:prosthetic:leg:left/variant/beastkin-sprint-joint",
            "수인 질주 관절",
            "수인 왼다리 슬롯에 장착하는 질주용 관절 보철.",
            381);
        EnsureSurgicalPart(
            "surgery:prosthetic:heat-sac/variant/demon-heat-sac",
            "데몬 열낭 보강기",
            "데몬 열낭 슬롯의 열 교환을 보조하는 인공 부품.",
            240);
        EnsureSurgicalPart(
            "surgery:prosthetic:balance-tail/variant/kobold-tail-balance",
            "코볼트 평형 꼬리 보강기",
            "코볼트 꼬리 슬롯의 회피 균형을 보조하는 정밀 보철.",
            300);
        EnsureSurgicalPart(
            "surgery:prosthetic:brain/variant/human-neural-assist",
            "인간 신경 보조기",
            "인간 뇌 슬롯에 장착해 직접 작업 신호를 보조하는 인공 부품.",
            260);
        EnsureSurgicalPart(
            "surgery:prosthetic:hand:left",
            "왼손 정밀 보철",
            "코볼트 왼손 슬롯에 장착하는 정밀 작업용 보철.",
            260);

        string[] existingPartIds =
        {
            "surgery:prosthetic:arm:left",
            "surgery:prosthetic:brain",
            "surgery:prosthetic:heart",
            "surgery:prosthetic:leg:left",
            "surgery:prosthetic:pseudopods"
        };
        foreach (string itemId in existingPartIds)
        {
            string path = $"{ItemDefinitionRoot}/{Sanitize(itemId)}.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path) == null)
            {
                throw new InvalidOperationException(
                    $"Required legacy surgical part is missing: {path}");
            }
        }
    }

    private static void EnsureSurgicalPart(
        string itemId,
        string displayName,
        string description,
        int unitPrice)
    {
        string path = $"{ItemDefinitionRoot}/{Sanitize(itemId)}.asset";
        ItemDefinitionSO existing =
            AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
        if (existing == null)
        {
            GenericItemDefinitionSO created =
                ScriptableObject.CreateInstance<GenericItemDefinitionSO>();
            created.ConfigureCore(
                itemId,
                displayName,
                description,
                StockCategory.General,
                unitPrice,
                1.8f,
                1);
            created.SetFeature(new ProductionItemFeature
            {
                kind = ResourceItemKind.FinishedGood,
                ingredientTags = ResourceIngredientTag.None,
                sharedIntermediate = false
            });
            AssetDatabase.CreateAsset(created, path);
            EditorUtility.SetDirty(created);
            return;
        }

        if (existing is not GenericItemDefinitionSO item)
        {
            throw new InvalidOperationException(
                $"Surgical part path collision at '{path}' with "
                + $"'{existing.GetType().Name}'.");
        }

        if (!string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Surgical part asset '{path}' has item ID '{item.ItemId}', expected '{itemId}'.");
        }
        if (!item.TryGetFeature(out ProductionItemFeature production)
            || production.kind != ResourceItemKind.FinishedGood)
        {
            throw new InvalidOperationException(
                $"Surgical part '{itemId}' must remain an authored finished good.");
        }
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
        CreateRecipe(
            9804,
            "recipe:surgery:pseudopods",
            "위족 보철 조립",
            "강철 연결부와 유연 지지재를 조립해 위족 슬롯용 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 2),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:cloth", 1)
            },
            "surgery:prosthetic:pseudopods");
        CreateRecipe(
            9805,
            "recipe:surgery:hypha-core",
            "균핵 보철 조립",
            "금속 접속부와 목재 지지대, 고급 약품으로 인공 균핵을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 1),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("medicine:advanced", 1)
            },
            "surgery:prosthetic:hypha-core");
        CreateRecipe(
            9806,
            "recipe:surgery:wing",
            "왼날개 보철 조립",
            "강철 관절과 목재 골조, 직물 막을 조립해 왼날개 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 2),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:cloth", 1)
            },
            "surgery:prosthetic:wing:left");
        CreateRecipe(
            9807,
            "recipe:surgery:balance-tail",
            "평형 꼬리 보철 조립",
            "강철 관절과 목재 골조, 가죽 완충재로 평형 꼬리 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 2),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:leather", 1)
            },
            "surgery:prosthetic:balance-tail");
        CreateRecipe(
            9808,
            "recipe:surgery:reinforced-torso",
            "골격 보강 몸통 조립",
            "강철 하중 골격과 목재 지지대, 직물 라이너로 몸통 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 3),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:cloth", 1)
            },
            "surgery:prosthetic:torso");
        CreateRecipe(
            9809,
            "recipe:surgery:heart-augmentation",
            "전투 심장 보철 조립",
            "강철 펌프 부품과 마나 결정, 고급 약품으로 전투 심장 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 2),
                new ItemAmountDefinition("resource:mana-crystal", 1),
                new ItemAmountDefinition("medicine:advanced", 1)
            },
            "surgery:prosthetic:heart/variant/orc-combat-heart");
        CreateRecipe(
            9810,
            "recipe:surgery:night-eye",
            "왼쪽 야간안 보철 조립",
            "정밀 금속 부품과 마나 결정, 고급 약품으로 야간안 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 1),
                new ItemAmountDefinition("resource:mana-crystal", 1),
                new ItemAmountDefinition("medicine:advanced", 1)
            },
            "surgery:prosthetic:night-eye:left");
        CreateRecipe(
            9811,
            "recipe:surgery:heat-sac",
            "열낭 보철 조립",
            "강철 열교환부와 마나 결정, 고급 약품으로 열낭 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 1),
                new ItemAmountDefinition("resource:mana-crystal", 1),
                new ItemAmountDefinition("medicine:advanced", 1)
            },
            "surgery:prosthetic:heat-sac/variant/demon-heat-sac");
        CreateRecipe(
            9812,
            "recipe:surgery:precision-hand",
            "왼손 정밀 보철 조립",
            "강철 관절과 목재 지지대, 직물 완충재로 정밀 손 보철을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 2),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:cloth", 1)
            },
            "surgery:prosthetic:hand:left");
        CreateRecipe(
            9813,
            "recipe:surgery:brain-assist",
            "신경 보조기 조립",
            "정밀 금속 부품과 마나 결정, 고급 약품으로 신경 보조기를 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 1),
                new ItemAmountDefinition("resource:mana-crystal", 1),
                new ItemAmountDefinition("medicine:advanced", 1)
            },
            "surgery:prosthetic:brain/variant/human-neural-assist");
        CreateRecipe(
            9814,
            "recipe:surgery:sprint-joint",
            "수인 질주 관절 조립",
            "강철 하중 관절과 목재 지지대, 가죽 완충재로 질주 관절을 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 3),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:leather", 1)
            },
            "surgery:prosthetic:leg:left/variant/beastkin-sprint-joint");
        CreateRecipe(
            9815,
            "recipe:surgery:tail-balance-augmentation",
            "코볼트 평형 꼬리 보강기 조립",
            "강철 정밀 관절과 목재 골조, 가죽 완충재로 꼬리 보강기를 만든다.",
            34f,
            new[]
            {
                new ItemAmountDefinition("material:steel-ingot", 2),
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:leather", 1)
            },
            "surgery:prosthetic:balance-tail/variant/kobold-tail-balance");
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
        string path = GetRecipeAssetPath(recipeId);
        ProductionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(path);
        ProductionRecipeSO existing = recipe;
        float? approvedRequiredWork = recipe != null
            ? recipe.RequiredWork
            : null;
        ProductionOutputDefinition[] desiredOutputs =
        {
            new(
                ProductionOutputLineAuthoring.BuildStableId(
                    recipeId,
                    0,
                    outputItemId,
                    ProductionOutputRole.Main),
                ProductionOutputRole.Main,
                outputItemId,
                1)
        };
        ProductionOutputDefinition[] canonicalOutputs =
            ProductionOutputLineAuthoring.ResolveStableOutputs(
                recipeId,
                existing?.Outputs,
                desiredOutputs);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
            AssetDatabase.CreateAsset(recipe, path);
        }
        void ConfigureRecipe(ProductionRecipeSO target)
        {
            target.id = dataId;
            target.Configure(
                recipeId,
                displayName,
                description,
                "m06",
                BuiltInWorkTypeIds.Craft.Value,
                "research:medical:prosthetics",
                approvedRequiredWork ?? requiredWork,
                inputs,
                canonicalOutputs);
            target.ConfigureFlowRole(ProductionFlowRole.Transform);
            target.ConfigureProcessClass(ProductionProcessClass.Precision);
            if (Wim023GeneratedRecipeIds.Contains(
                    recipeId,
                    StringComparer.Ordinal))
            {
                target.ConfigureProficiency(
                    BuiltInCharacterProficiencyIds.Crafting,
                    recommendedRank: CharacterProficiencyRank.Technician);
            }
            V27ReviewedProductionMassExplanationCatalog.ApplyIfReviewed(target);
            target.ConfigureBalanceWork(
                approvedRequiredWork
                ?? V23BalanceWorkCalculator.CalculateRecipeBaseWork(
                    target,
                    ProductionProcessClass.Precision));
        }
        if (existing != null)
        {
            if (WouldChange(recipe, ConfigureRecipe))
            {
                ConfigureRecipe(recipe);
                EditorUtility.SetDirty(recipe);
            }
        }
        else
        {
            ConfigureRecipe(recipe);
            EditorUtility.SetDirty(recipe);
        }
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
            bool existed = building != null;
            if (building == null)
            {
                building = ScriptableObject.CreateInstance<BuildingSO>();
                AssetDatabase.CreateAsset(building, assetPath);
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            void ConfigureBuilding(BuildingSO target)
            {
                target.id = spec.Id;
                target.objectName = spec.Name;
                target.sprite = sprite;
                target.icon = sprite;
                target.width = Mathf.Max(1, spec.Width);
                target.height = 1;
                target.layer = GridLayer.Building;
                target.category = BuildingCategory.Crafting;
                target.horizontalDraggable = false;
                target.verticalDraggable = false;
                target.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
                target.tiles = null;
                target.movementAnchorOffset = Vector2.zero;
                target.movementTravelTime = 1.2f;
                if (!existed)
                {
                    target.unlocked = false;
                }

                target.ReplaceAbilities(CreateFacilityAbilities(
                    spec,
                    target.AbilityModules));
                IndustrialInfrastructureAssetBuilder
                    .ApplyProcessFluidConsumerOverlay(target);
                ServiceRoomContentAssetBuilder
                    .ApplyDirectMedicalHubOverlay(target);
                target.AbilityModules.EnsureStableIds();
                target.ValidateAbilitiesOrThrow();
            }

            if (existed && !WouldChange(building, ConfigureBuilding))
                continue;
            ConfigureBuilding(building);
            EditorUtility.SetDirty(building);
        }
    }

    private static BuildingAbilityCollection CreateFacilityAbilities(
        FacilitySpec spec,
        BuildingAbilityCollection existingAbilities)
    {
        BuildingWorkAmountAbility approvedWorkAmount = existingAbilities?
            .Items
            .OfType<BuildingWorkAmountAbility>()
            .SingleOrDefault();
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
        BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = spec.ConstructionWork,
            repairWorkRequired = Mathf.Max(8f, spec.ConstructionWork * 0.2f),
            cleanWorkRequired = 8f,
            operateWorkRequired = 12f
        };
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition(
                ResolveConstructionMaterialId(spec.Code),
                Mathf.Max(2, spec.Cost / 25))
        });
        abilities.Add(approvedWorkAmount ?? workAmount);
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
                maxStoredMassGrams = OrganStorageMassCapacityGrams,
                allCategories = false
            });
        }

        if (!string.IsNullOrWhiteSpace(spec.ProductionWorkstationTag))
        {
            if (spec.ProductionOutputBufferCycleCapacity is < 2 or > 4)
            {
                throw new InvalidOperationException(
                    $"{spec.Code}: production output-buffer cycle capacity must be authored in [2,4].");
            }
             abilities.Add(new BuildingProductionWorkstationAbility
             {
                 workstationTag = spec.ProductionWorkstationTag,
                 stockSensorInstallationItemId = ProductionBillRuntime.StockSensorItemId,
                 lanePolicy = ProductionWorkstationLanePolicy
                     .ManualWithDetachedBatchProcessors,
                 manualWorkLaneCount = 1,
                 automaticWorkLaneCount = 0
             });
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = spec.ProductionOutputBufferCycleCapacity,
                physicalOutputBufferCycleCapacity =
                    spec.ProductionOutputBufferCycleCapacity,
                allowOverflowDump = spec.AllowsProductionOverflowDump
            });
        }
        else if (spec.ProductionOutputBufferCycleCapacity != 0
            || spec.AllowsProductionOverflowDump)
        {
            throw new InvalidOperationException(
                $"{spec.Code}: production buffer settings require a workstation tag.");
        }

        abilities.Add(spec.SurgicalAbility);

        if (existingAbilities != null)
        {
            foreach (BuildingAbility existing in existingAbilities.Items)
            {
                if (existing == null
                    || IsMedicalFacilityBuilderOwned(existing, spec))
                {
                    continue;
                }

                abilities.Add(existing);
            }
        }

        return abilities;
    }

    private static bool IsMedicalFacilityBuilderOwned(
        BuildingAbility ability,
        FacilitySpec spec)
    {
        if (ability is BuildingFacilityPartAbility
            or BuildingSemanticTagsAbility
            or BuildingEconomyAbility
            or BuildingFacilityAbility
            or BuildingRoomRequirementAbility
            or BuildingInternalStockAbility
            or BuildingWorkAmountAbility
            or BuildingEvolutionAbility
            or BuildingMedicalAbility
            or BuildingStorageAbility
            or BuildingFuelConsumerAbility
            or BuildingProductionWorkstationAbility
            or BuildingProductionBufferAbility)
        {
            return true;
        }

        return spec?.SurgicalAbility != null
            && ability.GetType() == spec.SurgicalAbility.GetType();
    }

    private static string ResolveConstructionMaterialId(string code)
    {
        if (string.Equals(code, "M12", StringComparison.Ordinal)
            || string.Equals(code, "M13", StringComparison.Ordinal))
        {
            return "component:rune-conductor";
        }

        if (string.CompareOrdinal(code, "M05") >= 0)
        {
            return "material:steel-ingot";
        }

        return "material:lumber";
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
                }, FacilityWorkType.None),
            Facility("M06", 9506, "보철 조립대", 2, 165, 64, new Color32(178, 139, 72, 255),
                new BuildingProstheticAssemblyAbility
                {
                    assemblySpeedMultiplier = 1.1f,
                    qualityBonus = 0.06f
                }, FacilityWorkType.Craft,
                productionWorkstationTag: "m06",
                productionOutputBufferCycleCapacity: 4),
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
                }, FacilityWorkType.Haul, stores: true),
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
                }, FacilityWorkType.None),
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
                }, FacilityWorkType.Surgery | FacilityWorkType.Treat, treats: true),
            Facility("M13", 9513, "룬 봉합기", 1, 280, 86, new Color32(57, 160, 182, 255),
                new BuildingRehabilitationAbility
                {
                    adaptationSpeedMultiplier = 1.35f,
                    rejectionReductionPerWork = 0.15f,
                    primaryOperatingFacility = false,
                    runeSuture = true,
                    manaCrystalCost = 1
                }, FacilityWorkType.None)
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
        bool fuel = false,
        string productionWorkstationTag = "",
        int productionOutputBufferCycleCapacity = 0,
        bool allowsProductionOverflowDump = false)
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
            ConsumesFuel = fuel,
            ProductionWorkstationTag = productionWorkstationTag,
            ProductionOutputBufferCycleCapacity =
                productionOutputBufferCycleCapacity,
            AllowsProductionOverflowDump = allowsProductionOverflowDump
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
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateHuman());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateOrc());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateVampire());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateBeastkin());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateDemon());
        BuildAnatomyAsset(AnatomyProfileDefaults.CreateKobold());
    }

    private static void BuildAnatomyAsset(AnatomyProfileDefinition definition)
    {
        EnsureNumericFunctionalCapacityCoverage(definition);
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
        asset.ConfigureNotApplicableCapacities(
            Array.Empty<AnatomyFunctionalCapacityNotApplicable>());
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureNumericFunctionalCapacityCoverage(
        AnatomyProfileDefinition definition)
    {
        foreach (CharacterFunctionalCapacityId capacityId in Enum
                     .GetValues(typeof(CharacterFunctionalCapacityId))
                     .Cast<CharacterFunctionalCapacityId>())
        {
            AnatomyFunction function = CapacityFunction(capacityId);
            if (definition.Nodes.Any(node =>
                    (node.ExpandedFunctions & function) != 0))
            {
                continue;
            }

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
                    AnatomyFunction.IntakeProcessing
                    | AnatomyFunction.PowerCirculation,
                CharacterFunctionalCapacityId.PurificationProcessing =>
                    AnatomyFunction.PurificationProcessing
                    | AnatomyFunction.IntakeProcessing
                    | AnatomyFunction.PowerCirculation,
                _ => AnatomyFunction.PowerCirculation
            };
            AnatomyNodeDefinition producer = definition.Nodes
                .Where(node => (node.ExpandedFunctions & preferred) != 0)
                .OrderByDescending(node => node.CapacityWeight)
                .FirstOrDefault();
            if (producer == null)
            {
                throw new InvalidOperationException(
                    $"Anatomy profile '{definition.ProfileId}' cannot author "
                    + $"numeric capacity '{capacityId}'.");
            }
            producer.AddFunctions(function);
        }
    }

    private static AnatomyFunction CapacityFunction(
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
        _ => throw new ArgumentOutOfRangeException(nameof(capacityId), capacityId, null)
    };

    private static void BuildProcedures()
    {
        foreach (ProcedureSpec spec in CreateProcedureSpecs())
        {
            BuildProcedure(spec);
        }
    }

    public static void EnsureAgeTreatmentProcedures()
    {
        EnsureFolder(ProcedureRoot);
        foreach (ProcedureSpec spec in CreateAgeTreatmentProcedureSpecs())
        {
            BuildProcedure(spec);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void BuildProcedure(ProcedureSpec spec)
    {
        string path = $"{ProcedureRoot}/{Sanitize(spec.Id)}.asset";
        SurgicalProcedureSO asset =
            AssetDatabase.LoadAssetAtPath<SurgicalProcedureSO>(path);
        bool existed = asset != null;
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SurgicalProcedureSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        void ConfigureProcedure(SurgicalProcedureSO target) => target.Configure(
            spec.Id,
            spec.Name,
            spec.Description,
            spec.Kind,
            spec.TargetNodeId,
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
            spec.Effects,
            spec.Family,
            spec.Urgency,
            spec.AnatomyFamilies,
            requirement: null,
            speciesIds: spec.SpeciesIds,
            primaryFacilityDefinitionId: spec.PrimaryFacilityDefinitionId);
        if (existed && !WouldChange(asset, ConfigureProcedure))
            return;
        ConfigureProcedure(asset);
        EditorUtility.SetDirty(asset);
    }

    private static ProcedureSpec[] CreateProcedureSpecs()
    {
        string medicine = StandardMedicineItemId;
        string biological = BloodItemId;
        string general = LumberItemId;
        ProcedureSpec[] core = new[]
        {
            Procedure("procedure:emergency-suture", "응급 봉합", "열린 상처를 닫고 출혈과 감염 위험을 낮춘다.",
                SurgicalProcedureKind.Suture, "research:survival:medical", SurgeryFacilityTag.Emergency,
                12f, 0.04f, 0.16f, 0.06f, false, false, true, false, true,
                Materials(Material(medicine, 1)), Effects(
                    new HealSurgicalNodeEffect { health = 8f, infectionReduction = 8f },
                    new StopSurgicalNodeBleedingEffect())),
            Procedure("procedure:blood-transfusion", "수혈", "혈액 제제를 투여해 급격한 혈액 손실을 완화한다.",
                SurgicalProcedureKind.Transfusion, "research:survival:medical", SurgeryFacilityTag.Emergency,
                10f, 0.05f, 0.12f, 0.05f, false, false, true, false, false,
                Materials(Material(SurgeryItemDefinitions.BloodPackId, 1)), Effects(
                    new HealSurgicalNodeEffect { health = 14f, infectionReduction = 2f },
                    new RecoverBloodLossEffect { amount = 25f })),
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
                Materials(Material(ManaCrystalItemId, 3), Material(medicine, 2)),
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
        return core
            .Concat(CreateSpeciesProcedureSpecs())
            .Concat(CreateAgeTreatmentProcedureSpecs())
            .ToArray();
    }

    private static ProcedureSpec[] CreateAgeTreatmentProcedureSpecs()
    {
        ProcedureSpec Create(
            string id,
            string name,
            string description,
            SurgicalProcedureKind kind,
            string researchId,
            int primaryFacilityDefinitionId,
            float work,
            bool anesthesia,
            AgeTreatmentEffectKind effect,
            params SurgicalMaterialRequirement[] materials)
        {
            ProcedureSpec spec = Procedure(
                id,
                name,
                description,
                kind,
                researchId,
                SurgeryFacilityTag.AgeTreatment,
                work,
                difficulty: 0.16f,
                infection: anesthesia ? 0.10f : 0.02f,
                bleeding: anesthesia ? 0.08f : 0f,
                anesthesia,
                restraint: false,
                living: true,
                corpse: false,
                wildlife: false,
                materials,
                Effects(new ApplyAgeTreatmentEffect { treatment = effect }));
            spec.Family = MedicalProcedureFamily.Arcane;
            spec.Urgency = MedicalProcedureUrgency.Elective;
            spec.PrimaryFacilityDefinitionId = primaryFacilityDefinitionId;
            return spec;
        }

        return new[]
        {
            Create(
                "procedure:organ-regeneration",
                "장기 재생",
                "재생 골격과 배양액으로 노화 손상 기관을 복원한다.",
                SurgicalProcedureKind.HealOrgan,
                "research:medical:organ-regeneration",
                8868,
                96f,
                anesthesia: true,
                AgeTreatmentEffectKind.OrganRegeneration,
                Material("medical:organ-regeneration-scaffold", 1),
                Material("medical:regenerative-medium", 1)),
            Create(
                "procedure:blood-rejuvenation",
                "혈액 회춘",
                "회춘 혈청을 수혈해 생물학적 나이를 낮춘다.",
                SurgicalProcedureKind.Transfusion,
                "research:medical:blood-rejuvenation",
                8869,
                72f,
                anesthesia: false,
                AgeTreatmentEffectKind.BloodRejuvenation,
                Material("medical:rejuvenation-serum", 1)),
            Create(
                "procedure:rune-hibernation",
                "룬 동면",
                "룬 동면 촉매를 사용해 활동을 멈추고 노화를 늦춘다.",
                SurgicalProcedureKind.SpeciesStabilization,
                "research:medical:rune-hibernation",
                8870,
                64f,
                anesthesia: false,
                AgeTreatmentEffectKind.RuneHibernation,
                Material("medical:rune-hibernation-catalyst", 1)),
            Create(
                "procedure:whole-body-regeneration",
                "전신 재생",
                "전신 재생 배지를 사용해 생물학적 나이와 초기 노화 질환을 되돌린다.",
                SurgicalProcedureKind.ArcaneModification,
                "research:medical:whole-body-regeneration",
                8871,
                180f,
                anesthesia: true,
                AgeTreatmentEffectKind.WholeBodyRegeneration,
                Material("medical:whole-body-regeneration-medium", 1)),
            Create(
                "procedure:temporal-stasis",
                "시간 고정",
                "시간 고정 인장을 결속해 동력 공급 중 노화와 신규 노화 질환을 멈춘다.",
                SurgicalProcedureKind.SpeciesStabilization,
                "research:medical:temporal-stasis",
                8872,
                140f,
                anesthesia: false,
                AgeTreatmentEffectKind.TemporalStasis,
                Material("component:temporal-stasis-seal", 1))
        };
    }

    private static ProcedureSpec[] CreateSpeciesProcedureSpecs()
    {
        string medicine = StandardMedicineItemId;
        string biological = BloodItemId;
        string general = LumberItemId;

        return new[]
        {
            SpeciesProcedure("slime-replenishment", "점액 보충", MedicalProcedureFamily.Slime,
                "research:medical:slime-bioengineering", "slime", new[] { "slime" },
                new HealSurgicalNodeEffect { health = 18f, infectionReduction = 6f }, medicine),
            SpeciesProcedure("slime-membrane-suture", "외피 봉합", MedicalProcedureFamily.Slime,
                "research:medical:slime-bioengineering", "slime", new[] { "slime" },
                new HealSurgicalNodeEffect { health = 26f, infectionReduction = 14f }, biological),
            SpeciesProcedure("slime-core-stabilization", "응집핵 안정화", MedicalProcedureFamily.Slime,
                "research:medical:slime-bioengineering", "slime", new[] { "slime" },
                new HealSurgicalNodeEffect { health = 22f, infectionReduction = 18f },
                "medical:slime-coagulation-frame"),
            SpeciesProcedure("slime-pseudopod-reshape", "위족 재성형", MedicalProcedureFamily.Slime,
                "research:medical:slime-bioengineering", "slime", new[] { "slime" },
                InstallPart("pseudopods", "surgery:prosthetic:pseudopods"), biological,
                SurgicalProcedureKind.InstallProsthetic, "pseudopods"),

            SpeciesProcedure("myconid-hypha-binding", "균사 결속", MedicalProcedureFamily.Myconid,
                "research:medical:mycelial-grafting", "fungal", new[] { "myconid" },
                new HealSurgicalNodeEffect { health = 24f, infectionReduction = 8f }, biological),
            SpeciesProcedure("myconid-spore-cleaning", "포자낭 세정", MedicalProcedureFamily.Myconid,
                "research:medical:mycelial-grafting", "fungal", new[] { "myconid" },
                new HealSurgicalNodeEffect { health = 12f, infectionReduction = 28f }, medicine),
            SpeciesProcedure("myconid-core-graft", "균핵 접목", MedicalProcedureFamily.Myconid,
                "research:medical:mycelial-grafting", "fungal", new[] { "myconid" },
                InstallPart("hypha-core", "surgery:prosthetic:hypha-core"), "medical:sterile-mycelium-graft",
                SurgicalProcedureKind.TransplantOrgan, "hypha-core"),
            SpeciesProcedure("myconid-regrowth", "균사 재배양", MedicalProcedureFamily.Myconid,
                "research:medical:mycelial-grafting", "fungal", new[] { "myconid" },
                InstallPart("arm:left", "surgery:prosthetic:arm:left"), biological,
                SurgicalProcedureKind.InstallProsthetic, "arm:left"),

            SpeciesProcedure("harpy-air-sac-suture", "기낭 봉합", MedicalProcedureFamily.Avian,
                "research:medical:avian-prosthetics", "avian", new[] { "harpy" },
                new HealSurgicalNodeEffect { health = 26f, infectionReduction = 16f }, medicine),
            SpeciesProcedure("harpy-wing-fixation", "날개 고정", MedicalProcedureFamily.Avian,
                "research:medical:avian-prosthetics", "avian", new[] { "harpy" },
                new HealSurgicalNodeEffect { health = 32f, infectionReduction = 8f }, general),
            SpeciesProcedure("harpy-feather-regrowth", "깃축 재생", MedicalProcedureFamily.Avian,
                "research:medical:avian-prosthetics", "avian", new[] { "harpy" },
                InstallPart("wing:left", "surgery:prosthetic:wing:left"), biological,
                SurgicalProcedureKind.InstallProsthetic, "wing:left"),
            SpeciesProcedure("harpy-tail-graft", "평형 꼬리깃 이식", MedicalProcedureFamily.Avian,
                "research:medical:avian-prosthetics", "avian", new[] { "harpy" },
                InstallPart("balance-tail", "surgery:prosthetic:balance-tail"), biological,
                SurgicalProcedureKind.TransplantOrgan, "balance-tail"),

            MaintenanceProcedure("golem-coolant-refill", "냉각수 보충", "research:medical:construct-core-maintenance", 20f),
            MaintenanceProcedure("golem-body-recast", "외장 재주조", "research:medical:construct-core-maintenance", 34f),
            MaintenanceProcedure("golem-servo-alignment", "서보 정렬", "research:medical:construct-core-maintenance", 28f),
            MaintenanceProcedure("golem-sensor-core", "감지핵 정비", "research:medical:construct-core-maintenance", 26f),
            MaintenanceProcedure("golem-power-core", "동력핵 정비", "research:medical:construct-core-maintenance", 30f),

            InstalledHumanoidProcedure("orc-skeletal-reinforcement", "골격 보강", "research:medical:surgery", "orc", "torso", "surgery:prosthetic:torso"),
            InstalledHumanoidProcedure("orc-combat-heart", "전투 심장 강화", "research:medical:surgery", "orc", "heart", "surgery:prosthetic:heart/variant/orc-combat-heart"),
            HumanoidTreatmentProcedure("vampire-blood-sac", "혈액낭 처치", "research:medical:bloodcraft-augmentation", "vampire", 30f, MedicalProcedureFamily.Vampiric),
            InstalledHumanoidProcedure("vampire-night-eye", "야간안 이식", "research:medical:bloodcraft-augmentation", "vampire", "night-eye:left", "surgery:prosthetic:night-eye:left", family: MedicalProcedureFamily.Vampiric, kind: SurgicalProcedureKind.TransplantOrgan),
            InstalledHumanoidProcedure("beastkin-tail-reconstruction", "균형 꼬리 재건", "research:medical:prosthetics", "beastkin", "balance-tail", "surgery:prosthetic:balance-tail", kind: SurgicalProcedureKind.InstallProsthetic),
            InstalledHumanoidProcedure("beastkin-sprint-joint", "질주 관절 보강", "research:medical:prosthetics", "beastkin", "leg:left", "surgery:prosthetic:leg:left/variant/beastkin-sprint-joint"),
            HumanoidTreatmentProcedure(
                "demon-mana-core-suture",
                "마핵 봉합",
                "research:medical:mana-core-engineering",
                "demon",
                30f,
                MedicalProcedureFamily.Demonic,
                "medical:mana-core-case"),
            InstalledHumanoidProcedure("demon-heat-sac", "열낭 강화", "research:medical:mana-core-engineering", "demon", "heat-sac", "surgery:prosthetic:heat-sac/variant/demon-heat-sac", family: MedicalProcedureFamily.Demonic),
            InstalledHumanoidProcedure("kobold-precision-hand", "정밀 손 보철", "research:medical:prosthetics", "kobold", "hand:left", "surgery:prosthetic:hand:left", kind: SurgicalProcedureKind.TransplantOrgan),
            InstalledHumanoidProcedure("kobold-tail-balance", "꼬리 평형 보강", "research:medical:prosthetics", "kobold", "balance-tail", "surgery:prosthetic:balance-tail/variant/kobold-tail-balance"),
            InstalledHumanoidProcedure("human-neural-assist", "범용 신경 보조기", "research:medical:prosthetics", "human", "brain", "surgery:prosthetic:brain/variant/human-neural-assist"),
            InstalledHumanoidProcedure("human-precision-prosthetic", "정밀 보철 조율", "research:medical:prosthetics", "human", "arm:left", "surgery:prosthetic:arm:left", kind: SurgicalProcedureKind.InstallProsthetic)
        };
    }

    private static ProcedureSpec SpeciesProcedure(
        string suffix,
        string name,
        MedicalProcedureFamily family,
        string researchId,
        string anatomyFamily,
        string[] speciesIds,
        SurgicalProcedureEffect effect,
        string materialId,
        SurgicalProcedureKind kind = SurgicalProcedureKind.SpeciesStabilization,
        string targetNodeId = "")
    {
        ProcedureSpec spec = Procedure(
            $"procedure:{suffix}", name, $"{name}을(를) 종족 해부 구조에 맞춰 시행한다.",
            SurgicalProcedureKind.SpeciesStabilization, researchId,
            SurgeryFacilityTag.GeneralSurgery | SurgeryFacilityTag.Sterilization,
            34f, 0.12f, 0.08f, 0.08f, false, false, true, false, false,
            Materials(Material(materialId, 1)), Effects(effect));
        spec.Family = family;
        spec.Kind = kind;
        spec.TargetNodeId = targetNodeId?.Trim() ?? string.Empty;
        spec.AnatomyFamilies = new[] { anatomyFamily };
        spec.SpeciesIds = speciesIds;
        return spec;
    }

    private static ProcedureSpec MaintenanceProcedure(
        string suffix,
        string name,
        string researchId,
        float durability)
    {
        ProcedureSpec spec = Procedure(
            $"procedure:{suffix}", name, $"{name} 후 시험 가동으로 파츠 내구도를 복구한다.",
            SurgicalProcedureKind.Maintenance, researchId,
            SurgeryFacilityTag.ProstheticAssembly,
            26f, 0.04f, 0f, 0f, false, false, true, false, false,
            Materials(Material(LumberItemId, 1)),
            Effects(new MaintainSurgicalPartEffect
            {
                durability = durability,
                contaminationReduction = 12f
            }));
        spec.Family = MedicalProcedureFamily.Construct;
        spec.Urgency = MedicalProcedureUrgency.Maintenance;
        spec.AnatomyFamilies = new[] { "construct" };
        spec.SpeciesIds = new[] { "golem" };
        return spec;
    }

    private static ProcedureSpec HumanoidTreatmentProcedure(
        string suffix,
        string name,
        string researchId,
        string speciesId,
        float health,
        MedicalProcedureFamily family = MedicalProcedureFamily.Biological,
        string materialItemId = "")
    {
        ProcedureSpec spec = SpeciesProcedure(
            suffix, name, family, researchId, "humanoid", new[] { speciesId },
            new HealSurgicalNodeEffect { health = health, infectionReduction = 10f },
            string.IsNullOrWhiteSpace(materialItemId)
                ? StandardMedicineItemId
                : materialItemId);
        spec.Urgency = MedicalProcedureUrgency.Elective;
        return spec;
    }

    private static ProcedureSpec InstalledHumanoidProcedure(
        string suffix,
        string name,
        string researchId,
        string speciesId,
        string targetNodeId,
        string requiredItemDefinitionId = "",
        MedicalProcedureFamily family = MedicalProcedureFamily.Biological,
        SurgicalProcedureKind kind = SurgicalProcedureKind.SpeciesAugmentation,
        SurgicalPartKind partKind = SurgicalPartKind.Prosthetic,
        string materialItemId = "")
    {
        ProcedureSpec spec = SpeciesProcedure(
            suffix,
            name,
            family,
            researchId,
            "humanoid",
            new[] { speciesId },
            InstallPart(targetNodeId, requiredItemDefinitionId, partKind),
            string.IsNullOrWhiteSpace(materialItemId)
                ? StandardMedicineItemId
                : materialItemId,
            kind,
            targetNodeId);
        spec.Urgency = MedicalProcedureUrgency.Elective;
        return spec;
    }

    private static InstallSurgicalPartEffect InstallPart(
        string targetNodeId,
        string requiredItemDefinitionId = "",
        SurgicalPartKind partKind = SurgicalPartKind.Prosthetic)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            throw new ArgumentException(
                "Installed-part procedures require a target node.",
                nameof(targetNodeId));
        }

        return new InstallSurgicalPartEffect
        {
            partKind = partKind,
            requiredItemDefinitionId = requiredItemDefinitionId?.Trim()
                ?? string.Empty,
            efficiency = 1f
        };
    }

    private static void BuildAuthoredInstalledPartEffects()
    {
        GameplayEffectDefinitionSO move = LoadRequiredEffect(
            "effect_character_move-speed_multiply.asset");
        GameplayEffectDefinitionSO work = LoadRequiredEffect(
            "effect_character_work-speed_multiply.asset");
        GameplayEffectDefinitionSO heat = LoadRequiredEffect(
            "effect_character_heat-exposure_multiply.asset");
        string healthPath =
            $"{GameplayEffectRoot}/effect_character_maximum-health_multiply.asset";
        GameplayEffectDefinitionSO health =
            AssetDatabase.LoadAssetAtPath<GameplayEffectDefinitionSO>(healthPath);
        if (health == null)
        {
            health = ScriptableObject.CreateInstance<GameplayEffectDefinitionSO>();
            AssetDatabase.CreateAsset(health, healthPath);
        }
        health.Configure(
            1170235001,
            "effect:character:maximum-health:multiply",
            GameplayEffectTargetIds.MaximumHealth,
            GameplayEffectOperation.Multiply,
            GameplayEffectProjectionPhase.Multiplicative,
            GameplayEffectSourceKind.SurgicalPart,
            GameplayEffectStackingPolicy.StackAll,
            0f,
            float.MaxValue);
        EditorUtility.SetDirty(health);

        string evasionPath =
            $"{GameplayEffectRoot}/effect_combat_evasion-chance_add-flat.asset";
        GameplayEffectDefinitionSO evasion =
            AssetDatabase.LoadAssetAtPath<GameplayEffectDefinitionSO>(evasionPath);
        if (evasion == null)
        {
            evasion = ScriptableObject.CreateInstance<GameplayEffectDefinitionSO>();
            AssetDatabase.CreateAsset(evasion, evasionPath);
        }
        evasion.Configure(
            1170235002,
            "effect:combat:evasion-chance:add-flat",
            GameplayEffectTargetIds.EvasionChance,
            GameplayEffectOperation.AddFlat,
            GameplayEffectProjectionPhase.BaseAdd,
            GameplayEffectSourceKind.SurgicalPart,
            GameplayEffectStackingPolicy.StackAll,
            0f,
            0.35f);
        EditorUtility.SetDirty(evasion);

        SetInstalledPartEffect(
            "surgery_prosthetic_leg_left_variant_beastkin-sprint-joint.asset",
            "part:beastkin-sprint-joint:move-speed",
            move,
            1.08f);
        SetInstalledPartEffect(
            "surgery_prosthetic_heart_variant_orc-combat-heart.asset",
            "part:orc-combat-heart:maximum-health",
            health,
            1.10f);
        SetInstalledPartEffect(
            "surgery_prosthetic_brain_variant_human-neural-assist.asset",
            "part:human-neural-assist:work-speed",
            work,
            1.05f);
        SetInstalledPartEffect(
            "surgery_prosthetic_heat-sac_variant_demon-heat-sac.asset",
            "part:demon-heat-sac:heat-exposure",
            heat,
            0.80f);
        SetInstalledPartEffect(
            "surgery_prosthetic_balance-tail_variant_kobold-tail-balance.asset",
            "part:kobold-tail-balance:evasion-chance",
            evasion,
            0.03f);
    }

    private static GameplayEffectDefinitionSO LoadRequiredEffect(
        string fileName)
    {
        string path = $"{GameplayEffectRoot}/{fileName}";
        GameplayEffectDefinitionSO effect =
            AssetDatabase.LoadAssetAtPath<GameplayEffectDefinitionSO>(path);
        return effect != null
            ? effect
            : throw new InvalidOperationException(
                $"Required gameplay effect definition is missing: {path}");
    }

    private static void SetInstalledPartEffect(
        string itemFileName,
        string bindingId,
        GameplayEffectDefinitionSO definition,
        float value)
    {
        string path = $"{ItemDefinitionRoot}/{itemFileName}";
        ItemDefinitionSO item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
        if (item == null)
        {
            throw new InvalidOperationException(
                $"Installed-effect surgical part is missing: {path}");
        }
        InstalledSurgicalPartEffectItemFeature existingFeature = item
            .GetFeatureOrDefault<InstalledSurgicalPartEffectItemFeature>();
        List<GameplayEffectBinding> existingEffects = existingFeature?.effects;
        List<GameplayEffectBinding> effects = existingEffects?
            .Where(existing => existing != null
                && !string.Equals(
                    existing.bindingId?.Trim(),
                    bindingId,
                    StringComparison.Ordinal))
            .ToList()
            ?? new List<GameplayEffectBinding>();
        effects.Add(new GameplayEffectBinding
        {
            bindingId = bindingId,
            definition = definition,
            value = value
        });
        effects = effects
            .OrderBy(effect => effect.bindingId, StringComparer.Ordinal)
            .ToList();
        bool unchanged = existingEffects != null
            && existingEffects.Count == effects.Count;
        for (int index = 0; unchanged && index < effects.Count; index++)
        {
            GameplayEffectBinding current = existingEffects[index];
            GameplayEffectBinding intended = effects[index];
            unchanged = current != null
                && intended != null
                && string.Equals(
                    current.bindingId,
                    intended.bindingId,
                    StringComparison.Ordinal)
                && ReferenceEquals(current.definition, intended.definition)
                && current.value == intended.value
                && ReferenceEquals(current.condition, intended.condition);
        }
        if (unchanged)
        {
            return;
        }
        item.SetFeature(new InstalledSurgicalPartEffectItemFeature
        {
            effects = effects
        });
        EditorUtility.SetDirty(item);
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

    public static void ValidateBuiltContent()
    {
        BuildingSO[] buildings = LoadAssets<BuildingSO>(BuildingRoot);
        SurgicalProcedureSO[] procedures = LoadAssets<SurgicalProcedureSO>(ProcedureRoot);
        AnatomyProfileSO[] anatomy = LoadAssets<AnatomyProfileSO>(AnatomyRoot);
        AnatomyConditionLexiconSO[] conditionLexicons =
            LoadAssets<AnatomyConditionLexiconSO>(ConditionLexiconRoot);
        ResearchProjectSO[] research = LoadAssets<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");
        ProductionRecipeSO[] prostheticRecipes = LoadAssets<ProductionRecipeSO>(
                RecipeRoot)
            .Where(recipe => recipe.RecipeId.StartsWith(
                "recipe:surgery:",
                StringComparison.Ordinal))
            .ToArray();

        if (buildings.Length != 13)
        {
            throw new InvalidOperationException($"Expected 13 surgery facilities, found {buildings.Length}.");
        }
        if (procedures.Length != 47)
        {
            throw new InvalidOperationException($"Expected 47 surgical procedures, found {procedures.Length}.");
        }
        if (anatomy.Length != 12)
        {
            throw new InvalidOperationException($"Expected 12 anatomy profiles, found {anatomy.Length}.");
        }
        if (conditionLexicons.Length != 7)
        {
            throw new InvalidOperationException(
                $"Expected 7 anatomy condition lexicons, found {conditionLexicons.Length}.");
        }
        if (research.Length != 180)
        {
            throw new InvalidOperationException(
                $"Expected 180 research projects, found {research.Length}.");
        }
        if (prostheticRecipes.Length != 15)
        {
            throw new InvalidOperationException(
                $"Expected 15 prosthetic production recipes, found {prostheticRecipes.Length}.");
        }
        ValidateWim023GeneratedRecipeProficiencies(prostheticRecipes);

        HashSet<string> speciesProcedureIds = new(
            CreateSpeciesProcedureSpecs()
                .Where(spec => spec.Family != MedicalProcedureFamily.Construct)
                .Select(spec => spec.Id),
            StringComparer.Ordinal);
        SurgicalProcedureSO[] speciesProcedures = procedures
            .Where(procedure => speciesProcedureIds.Contains(procedure.ProcedureId))
            .ToArray();
        SurgicalProcedureSO[] installedSpeciesProcedures = speciesProcedures
            .Where(procedure => procedure.TryGetInstallationEffect(out _))
            .ToArray();
        if (speciesProcedures.Length != 24
            || installedSpeciesProcedures.Length != 15
            || installedSpeciesProcedures.Count(procedure =>
                procedure.Kind == SurgicalProcedureKind.TransplantOrgan) != 4
            || installedSpeciesProcedures.Count(procedure =>
                procedure.Kind == SurgicalProcedureKind.InstallProsthetic) != 5
            || installedSpeciesProcedures.Count(procedure =>
                procedure.Kind == SurgicalProcedureKind.SpeciesAugmentation) != 6)
        {
            throw new InvalidOperationException(
                "WIM-023 species procedure classification must remain "
                + "24 total: 9 treatment, 4 replacement, 5 prosthetic, 6 augmentation.");
        }

        ItemDefinitionSO[] itemDefinitions =
            LoadAssets<ItemDefinitionSO>(ItemDefinitionRoot);
        IReadOnlyDictionary<string, ItemDefinitionSO> itemById = itemDefinitions
            .ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        ILookup<string, ProductionRecipeSO> producerByItemId = prostheticRecipes
            .SelectMany(recipe => recipe.Outputs
                .Where(output => output.Role == ProductionOutputRole.Main)
                .Select(output => new { output.ItemId, Recipe = recipe }))
            .ToLookup(value => value.ItemId, value => value.Recipe, StringComparer.Ordinal);
        foreach (SurgicalProcedureSO procedure in installedSpeciesProcedures)
        {
            procedure.TryGetInstallationEffect(out InstallSurgicalPartEffect install);
            string itemId = install.requiredItemDefinitionId?.Trim() ?? string.Empty;
            if (!itemById.TryGetValue(itemId, out ItemDefinitionSO item)
                || !item.TryGetFeature(out ProductionItemFeature production)
                || production.kind != ResourceItemKind.FinishedGood
                || producerByItemId[itemId].Count() != 1)
            {
                throw new InvalidOperationException(
                    $"WIM-023 procedure '{procedure.ProcedureId}' requires one authored "
                    + $"finished-good part and one M06 recipe for '{itemId}'.");
            }
        }

        BuildingSO prostheticAssembly = buildings.SingleOrDefault(building =>
            string.Equals(
                building.GetAbility<BuildingFacilityPartAbility>()?.code,
                "M06",
                StringComparison.Ordinal));
        BuildingProductionWorkstationAbility prostheticWorkstation =
            prostheticAssembly?.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility prostheticBuffer =
            prostheticAssembly?.GetProductionBufferAbility();
        if (prostheticAssembly == null
            || prostheticWorkstation == null
            || !string.Equals(
                prostheticWorkstation.WorkstationTag,
                "m06",
                StringComparison.Ordinal)
            || !string.Equals(
                prostheticWorkstation.StockSensorInstallationItemId,
                ProductionBillRuntime.StockSensorItemId,
                StringComparison.Ordinal)
            || prostheticBuffer == null
            || prostheticBuffer.defaultBatchCapacity != 4
            || prostheticBuffer.physicalOutputBufferCycleCapacity != 4
            || prostheticBuffer.allowOverflowDump
            || prostheticRecipes.Any(recipe => !string.Equals(
                recipe.WorkstationTag,
                prostheticWorkstation.WorkstationTag,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "M06 prosthetic production workstation/buffer authority is incomplete or drifted.");
        }

        ResourceAnatomyProfileCatalog anatomyCatalog =
            new ResourceAnatomyProfileCatalog(anatomy);
        IReadOnlyList<string> anatomyErrors = anatomyCatalog.Validate();
        IReadOnlyList<string> lexiconErrors =
            new ResourceAnatomyConditionLexicon(conditionLexicons)
                .Validate(anatomyCatalog);
        IReadOnlyList<string> procedureErrors =
            new ResourceSurgicalProcedureCatalog(procedures).Validate();
        IReadOnlyList<string> researchErrors =
            new ResourceResearchProjectCatalog(research).Validate();
        string[] errors = anatomyErrors
            .Concat(lexiconErrors)
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

    private static void ValidateWim023GeneratedRecipeProficiencies(
        IEnumerable<ProductionRecipeSO> recipes)
    {
        foreach (string recipeId in Wim023GeneratedRecipeIds)
        {
            ProductionRecipeSO[] matches = (recipes ?? Array.Empty<ProductionRecipeSO>())
                .Where(recipe => recipe != null
                    && string.Equals(
                        recipe.RecipeId,
                        recipeId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"WIM-023 generated recipe '{recipeId}' must be authored exactly once.");
            }

            ProficiencyWorkProfileAuthoring proficiency = matches[0].Proficiency;
            if (!proficiency.IsValid
                || !string.Equals(
                    proficiency.Primary.Value,
                    BuiltInCharacterProficiencyIds.Crafting.Value,
                    StringComparison.Ordinal)
                || proficiency.Secondary.IsValid
                || proficiency.PrimaryWeight != 1f
                || proficiency.CombinationMode != ProficiencyCombinationMode.PrimaryOnly
                || proficiency.RecommendedRank != CharacterProficiencyRank.Technician
                || proficiency.MinimumRiskRank != CharacterProficiencyRank.Apprentice)
            {
                throw new InvalidOperationException(
                    $"WIM-023 generated recipe '{recipeId}' must use the canonical "
                    + "crafting Technician proficiency profile.");
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

    private static bool WouldChange<T>(T asset, Action<T> configure)
        where T : ScriptableObject
    {
        string rawBefore = EditorJsonUtility.ToJson(asset, false);
        string before = CanonicalizeSemanticJson(rawBefore);
        T candidate = ScriptableObject.CreateInstance<T>();
        try
        {
            EditorJsonUtility.FromJsonOverwrite(rawBefore, candidate);
            candidate.name = asset.name;
            configure(candidate);
            if (asset is ProductionRecipeSO leftRecipe
                && candidate is ProductionRecipeSO rightRecipe)
            {
                return !ProductionRecipeAuthoringComparison.AreEquivalent(
                    leftRecipe,
                    rightRecipe);
            }
            if (asset is BuildingSO leftBuilding
                && candidate is BuildingSO rightBuilding)
            {
                return !AreEquivalentBuildingAuthoring(
                    leftBuilding,
                    rightBuilding);
            }
            return !string.Equals(
                before,
                CanonicalizeSemanticJson(
                    EditorJsonUtility.ToJson(candidate, false)),
                StringComparison.Ordinal);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(candidate);
        }
    }

    private static bool AreEquivalentBuildingAuthoring(
        BuildingSO left,
        BuildingSO right)
    {
        SerializedObject leftSerialized = new(left);
        SerializedObject rightSerialized = new(right);
        SerializedProperty leftProperty = leftSerialized.GetIterator();
        SerializedProperty rightProperty = rightSerialized.GetIterator();
        bool hasLeft = leftProperty.NextVisible(true);
        bool hasRight = rightProperty.NextVisible(true);
        while (hasLeft || hasRight)
        {
            if (hasLeft != hasRight
                || !string.Equals(
                    leftProperty.propertyPath,
                    rightProperty.propertyPath,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(
                    leftProperty.propertyPath,
                    "m_Script",
                    StringComparison.Ordinal)
                && !string.Equals(
                    leftProperty.propertyPath,
                    "abilityModules",
                    StringComparison.Ordinal)
                && !SerializedProperty.DataEquals(
                    leftProperty,
                    rightProperty))
            {
                return false;
            }

            hasLeft = leftProperty.NextVisible(false);
            hasRight = rightProperty.NextVisible(false);
        }

        IReadOnlyList<BuildingAbility> leftAbilities =
            left.AbilityModules?.Items ?? Array.Empty<BuildingAbility>();
        IReadOnlyList<BuildingAbility> rightAbilities =
            right.AbilityModules?.Items ?? Array.Empty<BuildingAbility>();
        if (leftAbilities.Count != rightAbilities.Count)
            return false;
        for (int index = 0; index < leftAbilities.Count; index++)
        {
            BuildingAbility leftAbility = leftAbilities[index];
            BuildingAbility rightAbility = rightAbilities[index];
            if (leftAbility == null || rightAbility == null)
            {
                if (leftAbility != rightAbility)
                    return false;
                continue;
            }
            if (leftAbility.GetType() != rightAbility.GetType()
                || !string.Equals(
                    JsonUtility.ToJson(leftAbility, false),
                    JsonUtility.ToJson(rightAbility, false),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static string CanonicalizeSemanticJson(string json) =>
        System.Text.RegularExpressions.Regex.Replace(
            json ?? string.Empty,
            "\\\"rid\\\"\\s*:\\s*-?[0-9]+",
            "\\\"rid\\\":0",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string Sanitize(string value)
    {
        return (value ?? string.Empty)
            .Replace(':', '_')
            .Replace('/', '_')
            .Replace(' ', '_');
    }
}
#endif
