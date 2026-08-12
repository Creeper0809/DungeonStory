#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V25ProficiencyContentAssetBuilder
{
    private const string Root =
        "Assets/Resources/SO/V25/Proficiencies";
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";

    private readonly struct Spec
    {
        public Spec(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
    }

    private static readonly Spec[] Specs =
    {
        new("proficiency:fieldwork", "현장 작업", "운반, 보급, 채집, 벌목, 채광·채석, 급수와 연료 보급의 숙련이다."),
        new("proficiency:construction-engineering", "건설·공학", "건설, 수리, 배관, 해체, 기반 시설과 대형 건설의 숙련이다."),
        new("proficiency:crafting", "제작", "일반 제작, 의복, 무기·방어구, 목공·단조, 수선과 개조의 숙련이다."),
        new("proficiency:food-production", "식량 생산", "농업, 사냥, 도축, 축산과 요리를 잇는 식량 생산 숙련이다."),
        new("proficiency:scholarship", "학술", "연구, 비전 분석, 기록과 검증의 숙련이다."),
        new("proficiency:medicine", "의료", "구조, 진단, 치료와 수술의 숙련이다."),
        new("proficiency:social", "사교", "접객, 외교, 공연, 포로 설득과 관리의 숙련이다."),
        new("proficiency:melee-combat", "근접 전투", "근접 공격, 방패 방어와 근접 제압의 숙련이다."),
        new("proficiency:ranged-combat", "원거리 전투", "활, 석궁, 화기 공격과 원거리 엄호의 숙련이다.")
    };

    [MenuItem("DungeonStory/Content/V25/Build Proficiency Content")]
    public static void Build()
    {
        EnsureFolder(Root);
        List<ProficiencyDefinitionSO> authored = new(Specs.Length);
        for (int index = 0; index < Specs.Length; index++)
        {
            Spec spec = Specs[index];
            string fileName = spec.Id.Replace(':', '_') + ".asset";
            string path = $"{Root}/{fileName}";
            ProficiencyDefinitionSO definition =
                AssetDatabase.LoadAssetAtPath<ProficiencyDefinitionSO>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ProficiencyDefinitionSO>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.ConfigureMetadata(
                spec.Id,
                spec.Name,
                spec.Description,
                1,
                "V25 9종 숙련 체계 권위");
            definition.ConfigureProficiency(index);
            EditorUtility.SetDirty(definition);
            authored.Add(definition);
        }

        GameDomainContentCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException(
                $"Missing domain catalog at '{CatalogPath}'.");
        catalog.SetDefinitions(catalog.Definitions
            .Where(value => value != null
                && value is not ProficiencyDefinitionSO)
            .Concat(authored));
        EditorUtility.SetDirty(catalog);
        AuthorGameplayProfiles();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("V25_PROFICIENCY_CONTENT=PASS; proficiencies=9; "
            + "all buildings/recipes/equipment/apparel authored");
    }

    private static void AuthorGameplayProfiles()
    {
        BuildingSO[] buildings = LoadAll<BuildingSO>();
        ProductionRecipeSO[] recipes = LoadAll<ProductionRecipeSO>();
        CombatEquipmentDefinitionSO[] equipment =
            LoadAll<CombatEquipmentDefinitionSO>();
        ApparelDefinitionSO[] apparel = LoadAll<ApparelDefinitionSO>();

        foreach (BuildingSO building in buildings)
        {
            ResolveConstructionRanks(
                V23BalanceWorkCalculator.ResolveConstructionClass(building),
                out CharacterProficiencyRank recommended,
                out CharacterProficiencyRank minimumRisk);
            ProficiencyWorkProfile operation = ResolveOperation(
                building,
                out ProficiencyCombinationMode mode);
            building.ConfigureProficiencies(
                BuiltInCharacterProficiencyIds.ConstructionEngineering,
                recommended,
                minimumRisk,
                operation.Primary,
                operation.Secondary,
                operation.PrimaryWeight,
                ResolveOperationRank(building.ResearchFacilityCommand),
                ResolveOperationMinimumRisk(building.ResearchFacilityCommand),
                mode);
            EditorUtility.SetDirty(building);
        }

        foreach (ProductionRecipeSO recipe in recipes)
        {
            ProficiencyWorkProfile profile = ResolveRecipe(recipe);
            CharacterProficiencyRank rank = ResolveWorkRank(recipe.RequiredWork);
            CharacterProficiencyRank minimumRisk =
                recipe.ProcessClass is ProductionProcessClass.Medical
                    or ProductionProcessClass.Chemical
                    or ProductionProcessClass.HeavyIndustrial
                    ? CharacterProficiencyRank.Technician
                    : CharacterProficiencyRank.Apprentice;
            recipe.ConfigureProficiency(
                profile.Primary,
                profile.Secondary,
                profile.PrimaryWeight,
                rank,
                minimumRisk);
            EditorUtility.SetDirty(recipe);
        }

        foreach (CombatEquipmentDefinitionSO definition in equipment)
        {
            CharacterProficiencyRank rank = definition.Tier switch
            {
                <= 0 => CharacterProficiencyRank.Apprentice,
                1 => CharacterProficiencyRank.Skilled,
                2 => CharacterProficiencyRank.Technician,
                _ => CharacterProficiencyRank.Expert
            };
            definition.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.Crafting,
                rank,
                definition.Tier >= 3
                    ? CharacterProficiencyRank.Technician
                    : CharacterProficiencyRank.Apprentice);
            EditorUtility.SetDirty(definition);
        }

        foreach (ApparelDefinitionSO definition in apparel)
        {
            CharacterProficiencyRank rank = definition.TailoringCoefficient switch
            {
                <= 1f => CharacterProficiencyRank.Apprentice,
                <= 1.3f => CharacterProficiencyRank.Skilled,
                <= 1.7f => CharacterProficiencyRank.Technician,
                _ => CharacterProficiencyRank.Expert
            };
            definition.ConfigureProficiency(
                rank,
                (definition.UseTags & ApparelUseTag.Medical) != 0
                    ? CharacterProficiencyRank.Technician
                    : CharacterProficiencyRank.Apprentice);
            EditorUtility.SetDirty(definition);
        }

        string[] failures = buildings
            .Where(value => !value.ConstructionProficiency.IsValid
                || (value.Facility?.SupportsWork(BuiltInWorkTypeIds.Operate) == true
                    && !value.OperationProficiency.IsValid))
            .Select(value => $"building:{value.name}")
            .Concat(recipes.Where(value => !value.Proficiency.IsValid)
                .Select(value => $"recipe:{value.RecipeId}"))
            .Concat(equipment.Where(value => !value.Proficiency.IsValid)
                .Select(value => $"equipment:{value.EquipmentId}"))
            .Concat(apparel.Where(value => !value.Proficiency.IsValid)
                .Select(value => $"apparel:{value.ApparelId}"))
            .ToArray();
        if (failures.Length > 0)
        {
            throw new InvalidOperationException(
                "V25 proficiency authoring failed: "
                + string.Join(", ", failures.Take(20)));
        }
        Debug.Log($"V25_PROFICIENCY_LINKS=PASS; buildings={buildings.Length}; "
            + $"recipes={recipes.Length}; equipment={equipment.Length}; "
            + $"apparel={apparel.Length}");
    }

    public static string AuditAuthoredGameplayProfiles()
    {
        BuildingSO[] buildings = LoadAll<BuildingSO>();
        ProductionRecipeSO[] recipes = LoadAll<ProductionRecipeSO>();
        CombatEquipmentDefinitionSO[] equipment = LoadAll<CombatEquipmentDefinitionSO>();
        ApparelDefinitionSO[] apparel = LoadAll<ApparelDefinitionSO>();
        int badConstruction = buildings.Count(value =>
            !value.ConstructionProficiency.IsValid);
        int operateBuildings = buildings.Count(value =>
            value.Facility?.SupportsWork(BuiltInWorkTypeIds.Operate) == true);
        int badOperation = buildings.Count(value =>
            value.Facility?.SupportsWork(BuiltInWorkTypeIds.Operate) == true
                && !value.OperationProficiency.IsValid);
        string[] badOperationIds = buildings
            .Where(value =>
                value.Facility?.SupportsWork(BuiltInWorkTypeIds.Operate) == true
                    && !value.OperationProficiency.IsValid)
            .Select(value => value.name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        int badBuildings = badConstruction + badOperation;
        int badRecipes = recipes.Count(value => !value.Proficiency.IsValid);
        int badEquipment = equipment.Count(value => !value.Proficiency.IsValid);
        int badApparel = apparel.Count(value => !value.Proficiency.IsValid);
        if (badBuildings + badRecipes + badEquipment + badApparel > 0)
        {
            throw new InvalidOperationException(
                $"V25 proficiency links failed: buildings={badBuildings} "
                + $"(construction={badConstruction}, operation={badOperation}/"
                + $"{operateBuildings}:"
                + string.Join(",", badOperationIds)
                + "), "
                + $"recipes={badRecipes}, equipment={badEquipment}, "
                + $"apparel={badApparel}.");
        }
        return $"V25 proficiency links PASS: buildings={buildings.Length}, "
            + $"operate={operateBuildings}, "
            + $"recipes={recipes.Length}, equipment={equipment.Length}, "
            + $"apparel={apparel.Length}";
    }

    [MenuItem("DungeonStory/Content/V25/Generate Proficiency Mapping Report")]
    public static string GenerateMappingReport()
    {
        const string reportPath =
            "Artifacts/QA/v25-proficiency-authored-mapping.md";
        BuildingSO[] buildings = LoadAll<BuildingSO>();
        ProductionRecipeSO[] recipes = LoadAll<ProductionRecipeSO>();
        CombatEquipmentDefinitionSO[] equipment = LoadAll<CombatEquipmentDefinitionSO>();
        ApparelDefinitionSO[] apparel = LoadAll<ApparelDefinitionSO>();
        StringBuilder output = new(196608);
        output.AppendLine("# V25 숙련 연결 자동 생성 부록")
            .AppendLine()
            .AppendLine("> 생성 권위: 루트 콘텐츠 에셋의 authored proficiency profile")
            .AppendLine()
            .AppendLine("## 31개 작업")
            .AppendLine()
            .AppendLine("| 작업 ID | 숙련 연결 |")
            .AppendLine("|---|---|");
        foreach (WorkTypeId workType in BuiltInWorkTypeIds.All)
        {
            string profile = WorkTypeProficiencyRules.TryResolve(
                workType,
                out ProficiencyWorkProfile value)
                ? Describe(value)
                : "직접 XP 없음 또는 시설 typed 역할 사용";
            output.Append("| `").Append(workType.Value).Append("` | ")
                .Append(Escape(profile)).AppendLine(" |");
        }

        AppendBuildings(output, buildings);
        AppendRecipes(output, recipes);
        AppendEquipment(output, equipment);
        AppendApparel(output, apparel);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, output.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        return $"V25 proficiency mapping report PASS: path={reportPath}, "
            + $"workTypes={BuiltInWorkTypeIds.All.Count}, buildings={buildings.Length}, "
            + $"recipes={recipes.Length}, equipment={equipment.Length}, apparel={apparel.Length}";
    }

    private static void AppendBuildings(StringBuilder output, BuildingSO[] values)
    {
        output.AppendLine().AppendLine("## 시설")
            .AppendLine()
            .AppendLine("| 정의 ID | 이름 | 건설 | 가동 | 권장/위험 최소 |")
            .AppendLine("|---|---|---|---|---|");
        foreach (BuildingSO value in values.OrderBy(value => value.ContentDefinitionId)
                     .ThenBy(value => value.name))
        {
            output.Append("| `").Append(Escape(string.IsNullOrWhiteSpace(value.ContentDefinitionId)
                    ? value.name
                    : value.ContentDefinitionId))
                .Append("` | ").Append(Escape(value.objectName))
                .Append(" | ").Append(Escape(Describe(value.ConstructionProficiency)))
                .Append(" | ").Append(Escape(value.OperationProficiency.IsValid
                    ? Describe(value.OperationProficiency)
                    : "해당 없음"))
                .Append(" | ")
                .Append(value.ConstructionProficiency.RecommendedRank)
                .Append("/").Append(value.ConstructionProficiency.MinimumRiskRank)
                .AppendLine(" |");
        }
    }

    private static void AppendRecipes(StringBuilder output, ProductionRecipeSO[] values)
    {
        output.AppendLine().AppendLine("## 조합식")
            .AppendLine()
            .AppendLine("| 조합식 ID | 이름 | 숙련 연결 | 권장/위험 최소 |")
            .AppendLine("|---|---|---|---|");
        foreach (ProductionRecipeSO value in values.OrderBy(value => value.RecipeId))
        {
            output.Append("| `").Append(Escape(value.RecipeId)).Append("` | ")
                .Append(Escape(value.DisplayName)).Append(" | ")
                .Append(Escape(Describe(value.Proficiency))).Append(" | ")
                .Append(value.Proficiency.RecommendedRank).Append("/")
                .Append(value.Proficiency.MinimumRiskRank).AppendLine(" |");
        }
    }

    private static void AppendEquipment(
        StringBuilder output,
        CombatEquipmentDefinitionSO[] values)
    {
        output.AppendLine().AppendLine("## 전투 장비")
            .AppendLine()
            .AppendLine("| 장비 ID | 이름 | 숙련 연결 | 권장/위험 최소 |")
            .AppendLine("|---|---|---|---|");
        foreach (CombatEquipmentDefinitionSO value in values.OrderBy(value => value.EquipmentId))
        {
            output.Append("| `").Append(Escape(value.EquipmentId)).Append("` | ")
                .Append(Escape(value.DisplayName)).Append(" | ")
                .Append(Escape(Describe(value.Proficiency))).Append(" | ")
                .Append(value.Proficiency.RecommendedRank).Append("/")
                .Append(value.Proficiency.MinimumRiskRank).AppendLine(" |");
        }
    }

    private static void AppendApparel(StringBuilder output, ApparelDefinitionSO[] values)
    {
        output.AppendLine().AppendLine("## 의복")
            .AppendLine()
            .AppendLine("| 의복 ID | 이름 | 숙련 연결 | 권장/위험 최소 |")
            .AppendLine("|---|---|---|---|");
        foreach (ApparelDefinitionSO value in values.OrderBy(value => value.ApparelId))
        {
            output.Append("| `").Append(Escape(value.ApparelId)).Append("` | ")
                .Append(Escape(value.DisplayName)).Append(" | ")
                .Append(Escape(Describe(value.Proficiency))).Append(" | ")
                .Append(value.Proficiency.RecommendedRank).Append("/")
                .Append(value.Proficiency.MinimumRiskRank).AppendLine(" |");
        }
    }

    private static string Describe(ProficiencyWorkProfile value) =>
        !value.Secondary.IsValid
            ? value.Primary.Value
            : $"{value.Primary.Value} {value.PrimaryWeight:P0} + "
                + $"{value.Secondary.Value} {value.SecondaryWeight:P0}";

    private static string Describe(ProficiencyWorkProfileAuthoring value) =>
        !value.Secondary.IsValid
            ? value.Primary.Value
            : value.CombinationMode == ProficiencyCombinationMode.Higher
                ? $"max({value.Primary.Value}, {value.Secondary.Value})"
                : $"{value.Primary.Value} {value.PrimaryWeight:P0} + "
                    + $"{value.Secondary.Value} {value.SecondaryWeight:P0}";

    private static string Escape(string value) =>
        (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static ProficiencyWorkProfile ResolveRecipe(ProductionRecipeSO recipe)
    {
        if (recipe.ProcessClass == ProductionProcessClass.Gathering)
            return new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Fieldwork);
        if (recipe.ProcessClass == ProductionProcessClass.CookingSimpleMixing)
            return new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.FoodProduction);
        if (recipe.ProcessClass == ProductionProcessClass.Medical)
            return new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Medicine);
        if (recipe.ProcessClass == ProductionProcessClass.Rune)
            return new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Crafting,
                BuiltInCharacterProficiencyIds.Scholarship,
                0.80f);
        if (WorkTypeProficiencyRules.TryResolve(recipe.WorkTypeId, out ProficiencyWorkProfile profile))
            return profile;
        return new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Crafting);
    }

    private static ProficiencyWorkProfile ResolveOperation(
        BuildingSO building,
        out ProficiencyCombinationMode mode)
    {
        ResearchFacilityCommandKind command =
            building?.ResearchFacilityCommand
            ?? ResearchFacilityCommandKind.None;
        mode = ProficiencyCombinationMode.PrimaryOnly;
        if (building?.GetAbility<BuildingTrainingAbility>() != null)
        {
            ProficiencyWorkProfile training = building.GetFacilityCode() switch
            {
                "T01" or "T04" => new ProficiencyWorkProfile(
                    BuiltInCharacterProficiencyIds.MeleeCombat),
                "T02" => new ProficiencyWorkProfile(
                    BuiltInCharacterProficiencyIds.RangedCombat),
                _ => default
            };
            if (training.IsValid)
                return training;
        }
        if (command == ResearchFacilityCommandKind.DefenseControl)
        {
            mode = ProficiencyCombinationMode.Higher;
            return new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.MeleeCombat,
                BuiltInCharacterProficiencyIds.RangedCombat,
                0.50f);
        }
        if (FacilityCommandProficiencyRules.TryResolve(command, out ProficiencyWorkProfile profile))
        {
            mode = profile.Secondary.IsValid
                ? ProficiencyCombinationMode.Weighted
                : ProficiencyCombinationMode.PrimaryOnly;
            return profile;
        }
        if (building?.Facility?.SupportsWork(
                BuiltInWorkTypeIds.Operate) == true)
        {
            return ResolveExplicitFacilityOperation(building.name, out mode);
        }
        return default;
    }

    private static ProficiencyWorkProfile ResolveExplicitFacilityOperation(
        string facilityId,
        out ProficiencyCombinationMode mode)
    {
        string id = facilityId?.Trim() ?? string.Empty;
        if (id is "G01_경비초소책상" or "G04_전술지도탁자")
        {
            mode = ProficiencyCombinationMode.Higher;
            return new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.MeleeCombat,
                BuiltInCharacterProficiencyIds.RangedCombat,
                .50f);
        }

        ProficiencyWorkProfile profile = id switch
        {
            "D01_간이화덕" or "D02_고기그릴" or "D12_술음료장"
                or "HamburgerStore" or "P1_LowFoodShop"
                or "P1_MeatRestaurant" or "P1_PremiumMeatRestaurant" =>
                Primary(BuiltInCharacterProficiencyIds.FoodProduction),
            "D04_배식카운터" or "P1_BattleDining"
                or "P1_BattlefieldDining" or "P1_NobleDining" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.FoodProduction,
                    BuiltInCharacterProficiencyIds.Social),
            "P1_GeneralStore" or "P1_WeaponShop" or "S01_판매카운터"
                or "WeaponStore" =>
                Primary(BuiltInCharacterProficiencyIds.Social),
            "P1_RestRoom" or "RF06_운반_멜빵_걸이"
                or "RF53_계절_저장_선반" =>
                Primary(BuiltInCharacterProficiencyIds.Fieldwork),
            "P1_TrainingRoom" or "P1_WarBarracks"
                or "T03_중량훈련석" =>
                Primary(BuiltInCharacterProficiencyIds.MeleeCombat),
            "M01_마력수정선반" or "M02_마력저장조"
                or "M04_의식초점석" or "P1_ManaStorage"
                or "RF05_의식_화로" or "RF33_룬_제어반"
                or "RF49_계절력_기록대" =>
                Primary(BuiltInCharacterProficiencyIds.Scholarship),
            "Q02_연금술작업대" or "RF23_룬_버스_결합기"
                or "RF25_시제품_연구실" or "RF26_재료_시험기"
                or "RF28_냉각_매니폴드" or "RF32_정밀_게이지"
                or "RF40_화약_분쇄소" or "RF42_부품_감정대"
                or "RF43_부품_복원_작업대" or "RF44_정밀_장착대"
                or "RF45_성장형_골격_지그" or "RF46_계측_작업대"
                or "RF47_구성체_핵_공학대" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Crafting,
                    BuiltInCharacterProficiencyIds.Scholarship),
            "R07_영주집무책상" or "RF12_동맹_신호기"
                or "RF94_경력_기록대" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Social,
                    BuiltInCharacterProficiencyIds.Scholarship),
            "RF02_중력식_수문" or "RF37_권양_작업대" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.ConstructionEngineering,
                    BuiltInCharacterProficiencyIds.Fieldwork),
            "RF03_동굴_재배_선반" or "RF13_균사_재배_선반"
                or "RF54_재배_온실" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.FoodProduction,
                    BuiltInCharacterProficiencyIds.Fieldwork),
            "RF04_문장_깃발_제작대" or "RF10_강화_구속구_선반"
                or "RF35_방어구_맞춤대" or "RF38_사슬_조립틀"
                or "RF39_관절_지그" =>
                Primary(BuiltInCharacterProficiencyIds.Crafting),
            "RF07_가격표_게시판" or "RF11_공연_소품_보관대"
                or "RF82_방_배정대" or "RF87_추모실" =>
                Primary(BuiltInCharacterProficiencyIds.Social),
            "RF09_포로_작업_도구함" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Social,
                    BuiltInCharacterProficiencyIds.MeleeCombat),
            "RF100_탄도_시험장" =>
                Primary(BuiltInCharacterProficiencyIds.RangedCombat),
            "RF101_흑강_주조_보조로" or "RF21_동력_공구날_연마대"
                or "RF27_기계_기초대" or "RF30_정비_부품함"
                or "RF31_전동_선반" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Crafting,
                    BuiltInCharacterProficiencyIds.ConstructionEngineering),
            "RF16_방부_처리_목재대" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Crafting,
                    BuiltInCharacterProficiencyIds.Medicine),
            "RF17_번식_장부대" or "RF91_방제_조제대"
                or "RF93_육종_온실" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.FoodProduction,
                    BuiltInCharacterProficiencyIds.Scholarship),
            "RF22_자동_세척기" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Fieldwork,
                    BuiltInCharacterProficiencyIds.ConstructionEngineering),
            "RF24_방어시설_장전기" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.RangedCombat,
                    BuiltInCharacterProficiencyIds.ConstructionEngineering),
            "RF36_궁시_지그" or "RF41_탄약_압착기" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Crafting,
                    BuiltInCharacterProficiencyIds.RangedCombat),
            "RF48_배식_운영판" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.FoodProduction,
                    BuiltInCharacterProficiencyIds.Social),
            "RF51_기상_관측탑" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Scholarship,
                    BuiltInCharacterProficiencyIds.Fieldwork),
            "RF59_산과실" or "RF67_재생_배양조"
                or "RF74_격리_병동" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Medicine,
                    BuiltInCharacterProficiencyIds.Scholarship),
            "RF76_백신_연구실" or "RF79_형질_분석기"
                or "RF81_교차계통_배양기" or "RF92_작물_병리실" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Scholarship,
                    BuiltInCharacterProficiencyIds.Medicine),
            "RF85_상담실" =>
                Weighted(
                    BuiltInCharacterProficiencyIds.Social,
                    BuiltInCharacterProficiencyIds.Medicine),
            _ => throw new InvalidOperationException(
                $"MissingExplicitFacilityOperationProficiency: '{id}'.")
        };
        mode = profile.Secondary.IsValid
            ? ProficiencyCombinationMode.Weighted
            : ProficiencyCombinationMode.PrimaryOnly;
        return profile;
    }

    private static ProficiencyWorkProfile Primary(
        CharacterProficiencyId primary) =>
        new(primary);

    private static ProficiencyWorkProfile Weighted(
        CharacterProficiencyId primary,
        CharacterProficiencyId secondary) =>
        new(primary, secondary, .80f);

    private static CharacterProficiencyRank ResolveWorkRank(float work) => work switch
    {
        <= 12f => CharacterProficiencyRank.Apprentice,
        <= 30f => CharacterProficiencyRank.Skilled,
        <= 80f => CharacterProficiencyRank.Technician,
        _ => CharacterProficiencyRank.Expert
    };

    private static CharacterProficiencyRank ResolveOperationRank(
        ResearchFacilityCommandKind command) => command switch
    {
        ResearchFacilityCommandKind.ResonanceTuning or
        ResearchFacilityCommandKind.ChronometricNavigation or
        ResearchFacilityCommandKind.GeneticCounseling or
        ResearchFacilityCommandKind.DefenseControl => CharacterProficiencyRank.Expert,
        ResearchFacilityCommandKind.BiologicalAgeMeasurement or
        ResearchFacilityCommandKind.PoweredLaundry or
        ResearchFacilityCommandKind.PoweredSpinning or
        ResearchFacilityCommandKind.PoweredWeaving => CharacterProficiencyRank.Technician,
        ResearchFacilityCommandKind.None => CharacterProficiencyRank.Apprentice,
        _ => CharacterProficiencyRank.Skilled
    };

    private static CharacterProficiencyRank ResolveOperationMinimumRisk(
        ResearchFacilityCommandKind command) => command switch
    {
        ResearchFacilityCommandKind.ResonanceTuning or
        ResearchFacilityCommandKind.DefenseControl or
        ResearchFacilityCommandKind.GeriatricCare or
        ResearchFacilityCommandKind.ChronicCare or
        ResearchFacilityCommandKind.PathogenDiagnosis or
        ResearchFacilityCommandKind.Serology => CharacterProficiencyRank.Technician,
        _ => CharacterProficiencyRank.Apprentice
    };

    private static void ResolveConstructionRanks(
        ConstructionBalanceClass balanceClass,
        out CharacterProficiencyRank recommended,
        out CharacterProficiencyRank minimumRisk)
    {
        recommended = balanceClass switch
        {
            ConstructionBalanceClass.Structure or
            ConstructionBalanceClass.Decoration or
            ConstructionBalanceClass.Furnishing => CharacterProficiencyRank.Apprentice,
            ConstructionBalanceClass.Storage or
            ConstructionBalanceClass.Workstation or
            ConstructionBalanceClass.Service => CharacterProficiencyRank.Skilled,
            ConstructionBalanceClass.Environment or
            ConstructionBalanceClass.Defense or
            ConstructionBalanceClass.Medical => CharacterProficiencyRank.Technician,
            _ => CharacterProficiencyRank.Expert
        };
        minimumRisk = balanceClass is ConstructionBalanceClass.Industrial
            or ConstructionBalanceClass.Arcane
            or ConstructionBalanceClass.Landmark
            ? CharacterProficiencyRank.Technician
            : CharacterProficiencyRank.Apprentice;
    }

    private static T[] LoadAll<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .ToArray();

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }
            current = next;
        }
    }
}
#endif
