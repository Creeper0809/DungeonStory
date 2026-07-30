#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResearchProjectAssetBuilder
{
    private const string Root = "Assets/Resources/SO/Research/Projects";

    private sealed class Spec
    {
        public string Id;
        public int NumericId;
        public string Name;
        public string Description;
        public ResearchField Field;
        public float Work;
        public ResearchBlueprintRule Rule;
        public int BlueprintId;
        public string[] Prerequisites;
    }

    [MenuItem("Tools/DungeonStory/Research/Rebuild Research Tree Assets")]
    public static void Rebuild()
    {
        EnsureFolders();
        IndustrialInfrastructureAssetBuilder.EnsureAssets();
        Dictionary<int, FacilityBlueprintSO> blueprints = AssetDatabase
            .FindAssets("t:FacilityBlueprintSO", new[] { "Assets/Resources/SO/Blueprint" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>)
            .Where(asset => asset != null)
            .GroupBy(asset => asset.id)
            .ToDictionary(group => group.Key, group => group.First());

        Dictionary<string, ResearchProjectSO> projects = new Dictionary<string, ResearchProjectSO>(
            StringComparer.Ordinal);
        foreach (Spec spec in CreateSpecs())
        {
            string assetPath = $"{Root}/{Sanitize(spec.Id)}.asset";
            ResearchProjectSO project = AssetDatabase.LoadAssetAtPath<ResearchProjectSO>(assetPath);
            MonoScript projectScript = project != null
                ? MonoScript.FromScriptableObject(project)
                : null;
            if (project != null
                && (projectScript == null
                    || string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(projectScript))))
            {
                AssetDatabase.DeleteAsset(assetPath);
                project = null;
            }
            if (project == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                project = ScriptableObject.CreateInstance<ResearchProjectSO>();
                AssetDatabase.CreateAsset(project, assetPath);
            }
            project.id = spec.NumericId;
            projects[spec.Id] = project;
        }

        foreach (Spec spec in CreateSpecs())
        {
            ResearchProjectSO project = projects[spec.Id];
            blueprints.TryGetValue(spec.BlueprintId, out FacilityBlueprintSO blueprint);
            if (blueprint != null)
            {
                blueprint.targetResearchProjectId = spec.Id;
                EditorUtility.SetDirty(blueprint);
            }

            BlueprintUnlockCollection unlocks = project.UnlockCollection;
            if (blueprint != null && blueprint.unlocks != null && blueprint.unlocks.Count > 0)
            {
                if (unlocks.Count == 0)
                {
                    unlocks = CloneUnlocks(blueprint.Unlocks);
                }
                blueprint.unlocks = new BlueprintUnlockCollection();
                EditorUtility.SetDirty(blueprint);
            }

            AppendProductionStationUnlocks(spec.Id, unlocks);
            project.Configure(
                spec.Id,
                spec.Name,
                spec.Description,
                spec.Field,
                spec.Work,
                spec.Rule,
                blueprint,
                spec.Prerequisites.Select(id => projects[id]),
                unlocks);
            EditorUtility.SetDirty(project);
        }

        AttachArchiveAbility();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects.Values);
        IReadOnlyList<string> errors = catalog.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }

        Debug.Log($"Research tree assets rebuilt: {projects.Count} projects.");
    }

    private static void AppendProductionStationUnlocks(
        string researchId,
        BlueprintUnlockCollection unlocks)
    {
        if (unlocks == null)
        {
            return;
        }

        IReadOnlyDictionary<string, string[]> industrialUnlocks =
            IndustrialInfrastructureAssetBuilder.GetResearchUnlockCodes();
        string[] facilityCodes = industrialUnlocks.TryGetValue(
            researchId,
            out string[] industrialCodes)
                ? industrialCodes
                : researchId switch
        {
            "research:cuisine:milling" => new[] { "P01" },
            "research:cuisine:fermentation" => new[] { "P02" },
            "research:forestry:sawmill" => new[] { "P03" },
            "research:forestry:charcoal" => new[] { "P04" },
            "research:mining:stonecutting" => new[] { "P05" },
            "research:mining:sorting" => new[] { "P06" },
            "research:metallurgy:iron" => new[] { "P07" },
            "research:metallurgy:steel" => new[] { "P08" },
            "research:metallurgy:precious" => new[] { "P09" },
            "research:metallurgy:blacksteel" => new[] { "P10" },
            "research:textile:fiber" => new[] { "P11" },
            "research:textile:tanning" => new[] { "P12" },
            "research:agriculture:compost" => new[] { "P13" },
            "research:pharmacology:distillation" => new[] { "P14" },
            "research:cuisine:crops" => new[] { "P15" },
            "research:survival:preservation" => new[] { "P16" },
            "research:husbandry:feed" => new[] { "P17" },
            "research:pharmacology:antiseptic" => new[] { "P18" },
            "research:arcane:alchemy" => new[] { "P19" },
            "research:textile:dreamweave" => new[] { "P20" },
            "research:metallurgy:primitive" => new[] { "P21" },
            "research:mining:quarry" => new[] { "P22" },
            "research:agriculture:field" => new[] { "P23" },
            "research:agriculture:indoor" => new[] { "P24" },
            "research:survival:sanitation" => new[] { "P25" },
            "research:survival:medical" => new[] { "M01" },
            "research:medical:anatomy" => new[] { "M02" },
            "research:medical:surgery" => new[] { "M03", "M04", "M05" },
            "research:medical:prosthetics" => new[] { "M06", "M07" },
            "research:medical:organ-preservation" => new[] { "M08" },
            "research:medical:xenotransplant" => new[] { "M09", "M10", "M11" },
            "research:medical:aberrant-augmentation" => new[] { "M12", "M13" },
            "research:defense:tactical-command" => new[] { "T01" },
            _ => Array.Empty<string>()
        };
        if (facilityCodes.Length == 0)
        {
            return;
        }

        Dictionary<string, BuildingSO> buildingsByCode = AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .Select(building => new
            {
                Building = building,
                Code = building.GetAbility<BuildingFacilityPartAbility>()?.code
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
            .GroupBy(entry => entry.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Building,
                StringComparer.Ordinal);

        foreach (string code in facilityCodes)
        {
            if (!buildingsByCode.TryGetValue(code, out BuildingSO building))
            {
                throw new InvalidOperationException(
                    $"Research '{researchId}' cannot find production facility '{code}'.");
            }

            bool exists = unlocks.Items
                .OfType<BlueprintBuildingUnlock>()
                .Any(unlock => unlock.buildingId == building.id);
            if (!exists)
            {
                unlocks.Add(new BlueprintBuildingUnlock
                {
                    buildingId = building.id
                });
            }
        }
    }

    private static BlueprintUnlockCollection CloneUnlocks(
        IEnumerable<BlueprintUnlock> source)
    {
        BlueprintUnlockCollection clone = new BlueprintUnlockCollection();
        foreach (BlueprintUnlock unlock in source ?? Array.Empty<BlueprintUnlock>())
        {
            switch (unlock)
            {
                case BlueprintBuildingUnlock building:
                    clone.Add(new BlueprintBuildingUnlock
                    {
                        buildingId = building.buildingId
                    });
                    break;
                case BlueprintBasicPurchaseUnlock purchase:
                    clone.Add(new BlueprintBasicPurchaseUnlock
                    {
                        buildingId = purchase.buildingId
                    });
                    break;
                case BlueprintRecipeUnlock recipe:
                    clone.Add(new BlueprintRecipeUnlock
                    {
                        recipeId = recipe.recipeId
                    });
                    break;
                default:
                    throw new InvalidOperationException(
                        $"연구 해금 이관을 지원하지 않는 타입입니다: {unlock?.GetType().FullName ?? "<null>"}");
            }
        }
        return clone;
    }

    private static void AttachArchiveAbility()
    {
        BuildingSO archive = AssetDatabase.FindAssets("Q03 t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(asset => asset != null
                && asset.GetAbility<BuildingFacilityPartAbility>()?.code == "Q03");
        if (archive == null)
        {
            throw new InvalidOperationException("Q03 연구용책장 BuildingSO를 찾지 못했습니다.");
        }

        archive.AbilityModules.Remove<BuildingResearchArchiveAbility>();
        archive.AbilityModules.Add(new BuildingResearchArchiveAbility { capacity = 8 });
        archive.AbilityModules.EnsureStableIds();
        archive.ValidateAbilitiesOrThrow();
        archive.unlocked = true;
        EditorUtility.SetDirty(archive);

        BuildingSO desk = AssetDatabase.FindAssets("Q01 t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(asset => asset != null
                && asset.GetAbility<BuildingFacilityPartAbility>()?.code == "Q01");
        if (desk != null)
        {
            desk.unlocked = true;
            EditorUtility.SetDirty(desk);
        }
    }

    private static IReadOnlyList<Spec> CreateSpecs()
    {
        return new[]
        {
            S("research:survival:sanitation", 7001, "기초 위생", "오염을 통제하고 기본 위생 설비를 운용한다.", ResearchField.LifeAndSurvival, 36),
            S("research:survival:support", 7002, "생활 지원", "식사와 휴식, 기본 생활 설비의 효율을 높인다.", ResearchField.LifeAndSurvival, 56, ResearchBlueprintRule.Required, 6103, "research:survival:sanitation"),
            S("research:survival:preservation", 7003, "식량 보존", "식량 부패를 늦추고 보존 조리법을 정리한다.", ResearchField.LifeAndSurvival, 84, prerequisites: new[] { "research:survival:support" }),
            S("research:survival:medical", 7004, "의료 회복", "부상과 질병을 체계적으로 안정화하고 치료한다.", ResearchField.LifeAndSurvival, 126, prerequisites: new[] { "research:survival:preservation", "research:arcane:alchemy" }),

            S("research:commerce:logistics", 7011, "창고 구획", "물자를 분류하고 운반 동선을 표준화한다.", ResearchField.CommerceAndCraft, 36),
            S("research:commerce:retail", 7012, "상업 진열", "손님 동선과 상품 진열을 정비한다.", ResearchField.CommerceAndCraft, 58, prerequisites: new[] { "research:commerce:logistics" }),
            S("research:commerce:expansion", 7013, "상업 확장", "전문 상점과 고급 제작 설비를 개방한다.", ResearchField.CommerceAndCraft, 88, ResearchBlueprintRule.Required, 6101, "research:commerce:retail"),
            S("research:commerce:secure-trade", 7014, "상권 통합", "지역 공급 계약과 요새화된 교역망을 연다.", ResearchField.CommerceAndCraft, 132, ResearchBlueprintRule.Shortcut, 6191, "research:commerce:expansion", "research:defense:fortification"),

            S("research:defense:watch", 7021, "경계 근무", "당직과 순찰, 침입 경보 절차를 확립한다.", ResearchField.DefenseAndTactics, 38),
            S("research:defense:fortification", 7022, "요새화", "성벽과 방어 설비를 보강한다.", ResearchField.DefenseAndTactics, 62, ResearchBlueprintRule.Required, 6102, "research:defense:watch"),
            S("research:defense:ranged-positions", 7023, "사격 방책", "엄폐와 원거리 교전 위치를 체계화한다.", ResearchField.DefenseAndTactics, 92, prerequisites: new[] { "research:defense:fortification" }),
            S("research:defense:tactical-command", 7024, "전술 지휘", "다중 경비의 전선과 교대를 통합 지휘한다.", ResearchField.DefenseAndTactics, 138, ResearchBlueprintRule.Shortcut, 6192, "research:defense:ranged-positions"),

            S("research:arcane:records", 7031, "기록 체계", "관찰과 실험 결과를 재현 가능한 기록으로 남긴다.", ResearchField.RecordsAndArcane, 36),
            S("research:arcane:alchemy", 7032, "연금 가공", "시약과 생체 물질을 안정적으로 가공한다.", ResearchField.RecordsAndArcane, 60, prerequisites: new[] { "research:arcane:records" }),
            S("research:arcane:advanced", 7033, "비전 연구", "마력과 의식 설비의 고급 원리를 해석한다.", ResearchField.RecordsAndArcane, 94, ResearchBlueprintRule.Required, 6104, "research:arcane:alchemy"),
            S("research:arcane:resonance", 7034, "비전 공명", "마나와 흑강을 쓰는 대형 비전 사업을 해금한다.", ResearchField.RecordsAndArcane, 142, ResearchBlueprintRule.Shortcut, 6193, "research:arcane:advanced", "research:authority:ritual"),

            S("research:control:restraints", 7041, "구속 관리", "포획과 구속, 감방 운용 절차를 정립한다.", ResearchField.CaptivityAndEntertainment, 40),
            S("research:control:labor", 7042, "노역 감독", "포로 노역의 작업과 감시 체계를 만든다.", ResearchField.CaptivityAndEntertainment, 66, prerequisites: new[] { "research:control:restraints" }),
            S("research:control:show", 7043, "흥행 운영", "무대와 관객, 공연자를 실제 운영 흐름으로 묶는다.", ResearchField.CaptivityAndEntertainment, 100, prerequisites: new[] { "research:control:labor", "research:commerce:retail" }),
            S("research:control:blood-show", 7044, "피의 흥행", "위험 공연과 공개 처벌을 통제된 흥행으로 만든다.", ResearchField.CaptivityAndEntertainment, 146, prerequisites: new[] { "research:control:show", "research:defense:watch" }),

            S("research:authority:quarters", 7051, "기본 숙소", "직원과 영주의 생활 구역을 분리한다.", ResearchField.AuthorityAndHousing, 34),
            S("research:authority:prestige", 7052, "장식과 위신", "장식과 공간 품질을 권위의 언어로 사용한다.", ResearchField.AuthorityAndHousing, 58, prerequisites: new[] { "research:authority:quarters" }),
            S("research:authority:office", 7053, "영주 집무", "방어 지휘와 대형 사업을 관리할 집무 공간을 연다.", ResearchField.AuthorityAndHousing, 96, prerequisites: new[] { "research:authority:prestige", "research:defense:watch" }),
            S("research:authority:ritual", 7054, "의식 장식", "권위의 장식을 비전 의식의 매개로 가공한다.", ResearchField.AuthorityAndHousing, 128, prerequisites: new[] { "research:authority:office" }),

            S("research:agriculture:gathering", 7061, "야생 채집", "외부의 풀, 꽃과 약초를 자원 노드로 채집한다.", ResearchField.Agriculture, 32),
            S("research:agriculture:field", 7062, "외부 경작", "야외 밭에 작물을 파종하고 수확한다.", ResearchField.Agriculture, 52, prerequisites: new[] { "research:agriculture:gathering" }),
            S("research:agriculture:compost", 7063, "퇴비·윤작", "부패물과 분뇨를 토양 영양으로 되돌린다.", ResearchField.Agriculture, 76, prerequisites: new[] { "research:agriculture:field" }),
            S("research:agriculture:irrigation", 7064, "관개", "물 저장과 급수 작업으로 수확 변동을 줄인다.", ResearchField.Agriculture, 104, prerequisites: new[] { "research:agriculture:compost" }),
            S("research:agriculture:indoor", 7065, "실내 재배", "물, 퇴비와 연료를 써서 실내에서 작물을 기른다.", ResearchField.Agriculture, 138, prerequisites: new[] { "research:agriculture:irrigation", "research:survival:support" }),
            S("research:agriculture:subterranean", 7066, "지하 자급", "균류와 영양 순환으로 계절과 밤을 넘는 자급망을 만든다.", ResearchField.Agriculture, 184, prerequisites: new[] { "research:agriculture:indoor" }),

            S("research:forestry:tools", 7071, "벌목 도구", "나무를 안전하게 베고 운반할 도구를 만든다.", ResearchField.Forestry, 32),
            S("research:forestry:logging", 7072, "벌목", "외부 수목에서 원목과 수액을 얻는다.", ResearchField.Forestry, 52, prerequisites: new[] { "research:forestry:tools" }),
            S("research:forestry:sawmill", 7073, "제재", "원목을 규격 목재와 제작용 자루로 가공한다.", ResearchField.Forestry, 76, prerequisites: new[] { "research:forestry:logging" }),
            S("research:forestry:charcoal", 7074, "숯가마", "원목을 고열 연료인 숯으로 바꾼다.", ResearchField.Forestry, 104, prerequisites: new[] { "research:forestry:sawmill" }),
            S("research:forestry:treated", 7075, "목재 처리", "수액과 숯으로 목재의 내구와 방습성을 높인다.", ResearchField.Forestry, 138, prerequisites: new[] { "research:forestry:charcoal" }),
            S("research:forestry:fungal", 7076, "실내 균목림", "지하 균목을 재배해 목재와 버섯을 함께 생산한다.", ResearchField.Forestry, 180, prerequisites: new[] { "research:forestry:treated", "research:agriculture:indoor" }),

            S("research:mining:surface", 7081, "노천 채석", "외부 암석에서 석재와 얕은 광석을 채취한다.", ResearchField.Mining, 34),
            S("research:mining:quarry", 7082, "채석장", "석재를 지속적으로 캐며 희귀 광맥을 탐색한다.", ResearchField.Mining, 56, prerequisites: new[] { "research:mining:surface" }),
            S("research:mining:stonecutting", 7083, "석재 가공", "거친 석재를 건축용 블록으로 절단한다.", ResearchField.Mining, 80, prerequisites: new[] { "research:mining:quarry" }),
            S("research:mining:sorting", 7084, "광석 선별", "석탄, 철, 금과 마나 결정을 분리한다.", ResearchField.Mining, 108, prerequisites: new[] { "research:mining:stonecutting" }),
            S("research:mining:deep", 7085, "심부 채굴", "연료와 유지보수를 대가로 깊은 광맥을 판다.", ResearchField.Mining, 144, prerequisites: new[] { "research:mining:sorting" }),
            S("research:mining:mana", 7086, "마나 시추", "불안정한 마나 광맥에서 결정을 추출한다.", ResearchField.Mining, 190, prerequisites: new[] { "research:mining:deep", "research:arcane:advanced" }),

            S("research:husbandry:capture", 7091, "야생 포획", "살아 있는 야생동물을 안정화해 우리로 옮긴다.", ResearchField.Husbandry, 36),
            S("research:husbandry:stable", 7092, "축사 관리", "방목장, 울타리, 물통과 사료통을 관리한다.", ResearchField.Husbandry, 58, prerequisites: new[] { "research:husbandry:capture" }),
            S("research:husbandry:feed", 7093, "사료·깔짚", "식성과 위생에 맞는 사료와 깔짚을 공급한다.", ResearchField.Husbandry, 82, prerequisites: new[] { "research:husbandry:stable" }),
            S("research:husbandry:taming", 7094, "길들이기", "공포를 낮추고 반복 돌봄으로 가축화한다.", ResearchField.Husbandry, 112, prerequisites: new[] { "research:husbandry:feed" }),
            S("research:husbandry:breeding", 7095, "번식 관리", "성별, 성장 단계와 임신을 고려해 개체 수를 관리한다.", ResearchField.Husbandry, 148, prerequisites: new[] { "research:husbandry:taming" }),
            S("research:husbandry:selective", 7096, "선별 사육", "위생과 혈통을 관리해 안정적인 산출물을 얻는다.", ResearchField.Husbandry, 194, prerequisites: new[] { "research:husbandry:breeding", "research:survival:sanitation" }),

            S("research:metallurgy:primitive", 7101, "원시 단조", "돌, 뼈와 연철로 기본 도구와 무기를 만든다.", ResearchField.Metallurgy, 38),
            S("research:metallurgy:iron", 7102, "철제 가공", "철괴를 표준 장비와 건축 부품으로 가공한다.", ResearchField.Metallurgy, 62, prerequisites: new[] { "research:metallurgy:primitive" }),
            S("research:metallurgy:steel", 7103, "제강", "철과 숯으로 더 단단하고 가벼운 강철을 만든다.", ResearchField.Metallurgy, 94, prerequisites: new[] { "research:metallurgy:iron", "research:forestry:charcoal" }),
            S("research:metallurgy:advanced", 7104, "고급 단조", "정밀 열처리로 걸작 장비의 기반을 만든다.", ResearchField.Metallurgy, 128, prerequisites: new[] { "research:metallurgy:steel" }),
            S("research:metallurgy:precious", 7105, "귀금 세공", "금과 보석을 권위 시설과 고가 장비에 사용한다.", ResearchField.Metallurgy, 164, prerequisites: new[] { "research:metallurgy:advanced", "research:authority:prestige" }),
            S("research:metallurgy:blacksteel", 7106, "흑강", "강철과 마나 결정을 결합해 비전 금속을 만든다.", ResearchField.Metallurgy, 216, prerequisites: new[] { "research:metallurgy:advanced", "research:arcane:advanced" }),

            S("research:textile:fiber", 7111, "섬유 가공", "그늘섬유와 털을 천, 붕대와 활시위로 잣는다.", ResearchField.Textiles, 34),
            S("research:textile:tanning", 7112, "무두질", "가죽과 소금석을 내구성 있는 원단으로 가공한다.", ResearchField.Textiles, 58, prerequisites: new[] { "research:textile:fiber" }),
            S("research:textile:tailoring", 7113, "재봉", "직물과 가죽으로 의복과 연갑을 만든다.", ResearchField.Textiles, 86, prerequisites: new[] { "research:textile:tanning" }),
            S("research:textile:layered", 7114, "층상 방어구", "여러 원단층으로 부위별 방어를 강화한다.", ResearchField.Textiles, 118, prerequisites: new[] { "research:textile:tailoring" }),
            S("research:textile:rune-leather", 7115, "룬가죽", "가죽에 마나 문양을 새겨 방어와 마법 저항을 높인다.", ResearchField.Textiles, 154, prerequisites: new[] { "research:textile:tanning", "research:arcane:advanced" }),
            S("research:textile:dreamweave", 7116, "몽직물", "몽엽과 섬유를 엮어 초경량 정신 저항 원단을 만든다.", ResearchField.Textiles, 202, prerequisites: new[] { "research:textile:layered", "research:pharmacology:anesthesia" }),

            S("research:cuisine:crops", 7121, "농산 조리", "곡물, 뿌리, 버섯으로 안전한 기본식을 만든다.", ResearchField.Cuisine, 32),
            S("research:cuisine:milling", 7122, "제분·제빵", "황혼곡을 밀가루와 빵으로 가공한다.", ResearchField.Cuisine, 54, prerequisites: new[] { "research:cuisine:crops" }),
            S("research:cuisine:vegan", 7123, "채식 조리", "비건과 채식 식단을 실제 재료로 구분해 조리한다.", ResearchField.Cuisine, 78, prerequisites: new[] { "research:cuisine:milling", "research:agriculture:field" }),
            S("research:cuisine:livestock", 7124, "축산 조리", "고기, 우유와 알을 고급 식사로 가공한다.", ResearchField.Cuisine, 106, prerequisites: new[] { "research:cuisine:vegan", "research:husbandry:feed" }),
            S("research:cuisine:fermentation", 7125, "발효", "과일, 곡물과 버섯을 술과 조미료로 바꾼다.", ResearchField.Cuisine, 140, prerequisites: new[] { "research:cuisine:livestock" }),
            S("research:cuisine:lavish", 7126, "호화·보존식", "세 재료군과 보존 기술로 장기 저장 가능한 호화식을 만든다.", ResearchField.Cuisine, 186, prerequisites: new[] { "research:cuisine:fermentation", "research:survival:preservation" }),

            S("research:pharmacology:herbalism", 7131, "약초학", "약용 식물의 효능과 독성을 분류한다.", ResearchField.Pharmacology, 34),
            S("research:pharmacology:antiseptic", 7132, "소독·붕대", "섬유와 약초로 감염을 막는 치료재를 만든다.", ResearchField.Pharmacology, 58, prerequisites: new[] { "research:pharmacology:herbalism", "research:textile:fiber" }),
            S("research:pharmacology:distillation", 7133, "증류", "알코올과 연금 용매로 유효 성분을 농축한다.", ResearchField.Pharmacology, 86, prerequisites: new[] { "research:pharmacology:antiseptic", "research:arcane:alchemy" }),
            S("research:pharmacology:anesthesia", 7134, "진통·마취", "몽엽으로 통증과 의식을 안전하게 조절한다.", ResearchField.Pharmacology, 118, prerequisites: new[] { "research:pharmacology:distillation" }),
            S("research:pharmacology:stimulants", 7135, "각성제", "혈엽과 마나로 전투·작업 촉진제를 만든다.", ResearchField.Pharmacology, 154, prerequisites: new[] { "research:pharmacology:anesthesia" }),
            S("research:pharmacology:advanced", 7136, "고급 약리", "의료와 연금 지식을 결합해 고급 약품과 해독제를 만든다.", ResearchField.Pharmacology, 204, prerequisites: new[] { "research:pharmacology:stimulants", "research:survival:medical", "research:arcane:alchemy" }),

            S("research:medical:anatomy", 7141, "해부학", "인간형과 동물의 기관 구조를 기록해 치료와 적출의 기준을 세운다.", ResearchField.SurgeryAndTransplant, 96, prerequisites: new[] { "research:survival:medical", "research:arcane:records" }),
            S("research:medical:surgery", 7142, "외과술", "마취, 절개와 봉합을 표준화해 생체 수술을 가능하게 한다.", ResearchField.SurgeryAndTransplant, 138, prerequisites: new[] { "research:medical:anatomy", "research:pharmacology:anesthesia" }),
            S("research:medical:prosthetics", 7143, "보철 공학", "결손된 팔다리와 감각 기관을 금속과 목재 보철로 대체한다.", ResearchField.SurgeryAndTransplant, 174, prerequisites: new[] { "research:medical:surgery", "research:metallurgy:iron" }),
            S("research:medical:organ-preservation", 7144, "장기 보존", "적출 기관의 기증자 기록과 신선도를 유지하는 저온 보관법을 확립한다.", ResearchField.SurgeryAndTransplant, 188, prerequisites: new[] { "research:medical:surgery", "research:survival:preservation", "research:pharmacology:antiseptic" }),
            S("research:medical:xenotransplant", 7145, "이종 이식", "다른 종의 기관을 순환계에 연결하고 거부 반응을 통제한다.", ResearchField.SurgeryAndTransplant, 238, prerequisites: new[] { "research:medical:organ-preservation", "research:husbandry:selective", "research:pharmacology:advanced" }),
            S("research:medical:aberrant-augmentation", 7146, "이형 개조", "비전 기관과 룬 봉합으로 생명의 원형을 의도적으로 다시 쓴다.", ResearchField.SurgeryAndTransplant, 310, prerequisites: new[] { "research:medical:xenotransplant", "research:arcane:resonance", "research:metallurgy:blacksteel" }),

            S("research:industry:steam-power", 7151, "증기 동력", "목재와 석탄을 태워 생산 설비를 움직일 축 동력을 만든다.", ResearchField.IndustryAndAutomation, 72, prerequisites: new[] { "research:forestry:charcoal", "research:metallurgy:iron" }),
            S("research:industry:distribution", 7152, "배전", "전선과 회로 구역으로 발전원과 소비 시설을 연결한다.", ResearchField.IndustryAndAutomation, 94, prerequisites: new[] { "research:industry:steam-power" }),
            S("research:industry:breakers", 7153, "차단과 보호", "과부하 회로를 분리하고 고장을 국소화한다.", ResearchField.IndustryAndAutomation, 116, prerequisites: new[] { "research:industry:distribution" }),
            S("research:industry:storage", 7154, "축전", "남는 전력을 저장해 정전과 수요 급증에 대비한다.", ResearchField.IndustryAndAutomation, 142, prerequisites: new[] { "research:industry:breakers" }),
            S("research:industry:waterwheel", 7155, "수차 발전", "외부 수원을 이용해 연료 없는 완만한 전력을 생산한다.", ResearchField.IndustryAndAutomation, 154, prerequisites: new[] { "research:industry:distribution", "research:agriculture:irrigation" }),
            S("research:industry:transformers", 7156, "변압과 회로 구역", "대규모 배전망을 우선순위 회로로 나누어 운용한다.", ResearchField.IndustryAndAutomation, 178, prerequisites: new[] { "research:industry:storage" }),
            S("research:industry:mana-power", 7157, "마나 발전", "마나 결정을 안정된 전력으로 변환한다.", ResearchField.IndustryAndAutomation, 218, prerequisites: new[] { "research:industry:transformers", "research:arcane:advanced" }),
            S("research:industry:rune-grid", 7158, "룬 전력망", "룬 안정기로 고밀도 전력망의 손실과 과부하를 줄인다.", ResearchField.IndustryAndAutomation, 274, prerequisites: new[] { "research:industry:mana-power", "research:arcane:resonance" }),

            S("research:industry:conveyor", 7161, "컨베이어", "전동 벨트로 물리 아이템을 정해진 방향으로 운송한다.", ResearchField.IndustryAndAutomation, 108, prerequisites: new[] { "research:industry:distribution", "research:commerce:logistics" }),
            S("research:industry:ports", 7162, "입출력 포트", "시설 버퍼와 컨베이어 사이에서 아이템을 보존한 채 인계한다.", ResearchField.IndustryAndAutomation, 128, prerequisites: new[] { "research:industry:conveyor" }),
            S("research:industry:junctions", 7163, "분배와 합류", "한 물류선을 여러 목적지로 나누고 다시 합친다.", ResearchField.IndustryAndAutomation, 150, prerequisites: new[] { "research:industry:ports" }),
            S("research:industry:filters", 7164, "물류 필터", "품목, 재질, 품질과 신선도로 운송 경로를 분리한다.", ResearchField.IndustryAndAutomation, 172, prerequisites: new[] { "research:industry:junctions" }),
            S("research:industry:priority-gates", 7165, "우선순위 게이트", "중요 생산선에 먼저 공간을 내주고 저순위 흐름을 대기시킨다.", ResearchField.IndustryAndAutomation, 194, prerequisites: new[] { "research:industry:filters" }),
            S("research:industry:lifts", 7166, "층간 물류 리프트", "층 사이에서도 고유 아이템의 메타데이터를 유지해 운송한다.", ResearchField.IndustryAndAutomation, 224, prerequisites: new[] { "research:industry:priority-gates", "research:metallurgy:steel" }),
            S("research:industry:overflow", 7167, "오버플로 배출", "교착된 물류를 예비 창고나 바닥 스택으로 안전하게 배출한다.", ResearchField.IndustryAndAutomation, 242, prerequisites: new[] { "research:industry:filters" }),
            S("research:industry:high-speed-belts", 7168, "고속 물류망", "강철 구동부와 회로 제어로 벨트 처리량을 높인다.", ResearchField.IndustryAndAutomation, 288, prerequisites: new[] { "research:industry:lifts", "research:industry:overflow" }),

            S("research:industry:powered-tools", 7171, "전동 공구", "전력을 사용해 작업자의 생산 작업량을 보조한다.", ResearchField.IndustryAndAutomation, 112, prerequisites: new[] { "research:industry:distribution", "research:metallurgy:iron" }),
            S("research:industry:assisted-processing", 7172, "동력 보조 가공", "기존 생산 시설에 전동 모듈을 부착해 작업 속도를 높인다.", ResearchField.IndustryAndAutomation, 138, prerequisites: new[] { "research:industry:powered-tools" }),
            S("research:industry:automatic-bills", 7173, "자동 생산 주문", "공급과 출력이 확보된 반복 주문을 무인으로 진행한다.", ResearchField.IndustryAndAutomation, 168, prerequisites: new[] { "research:industry:assisted-processing", "research:industry:ports" }),
            S("research:industry:stock-sensors", 7174, "재고 감지기", "목표 재고와 시설 버퍼를 읽어 과잉 생산을 멈춘다.", ResearchField.IndustryAndAutomation, 192, prerequisites: new[] { "research:industry:automatic-bills", "research:commerce:logistics" }),
            S("research:industry:maintenance", 7175, "예방 정비", "오염과 마모가 고장으로 번지기 전에 정비 주문을 만든다.", ResearchField.IndustryAndAutomation, 214, prerequisites: new[] { "research:industry:stock-sensors", "research:industry:breakers" }),
            S("research:industry:precision", 7176, "정밀 자동화", "자동 생산의 품질 편차와 재료 손실을 줄인다.", ResearchField.IndustryAndAutomation, 246, prerequisites: new[] { "research:industry:maintenance", "research:metallurgy:advanced" }),
            S("research:industry:automatic-sanitation", 7177, "자동 위생 관리", "펌프와 배수구를 제어해 세척과 오수 처리를 자동화한다.", ResearchField.IndustryAndAutomation, 260, prerequisites: new[] { "research:industry:maintenance", "research:plumbing:sewer" }),
            S("research:industry:rune-automation", 7178, "룬 자동화", "룬 제어반으로 복잡한 생산선의 작업과 유지보수를 보조한다.", ResearchField.IndustryAndAutomation, 324, prerequisites: new[] { "research:industry:precision", "research:industry:rune-grid" }),

            S("research:industry:factory-layout", 7181, "공장 배치", "입출력 포트, 작업 위치와 정비 통로를 표준화한다.", ResearchField.IndustryAndAutomation, 118, prerequisites: new[] { "research:industry:powered-tools", "research:commerce:logistics" }),
            S("research:industry:electric-smelting", 7182, "전기 제련", "용광로와 제강로의 열을 전력으로 안정화한다.", ResearchField.IndustryAndAutomation, 154, prerequisites: new[] { "research:industry:factory-layout", "research:metallurgy:steel" }),
            S("research:industry:industrial-cooling", 7183, "산업 냉각", "재이용수로 과열과 자동화 고장을 낮춘다.", ResearchField.IndustryAndAutomation, 182, prerequisites: new[] { "research:industry:electric-smelting", "research:plumbing:reuse" }),
            S("research:industry:electric-lighting", 7184, "산업 조명", "작업 구역의 명중·속도·안전 저하를 전기 조명으로 완화한다.", ResearchField.IndustryAndAutomation, 164, prerequisites: new[] { "research:industry:distribution", "research:defense:watch" }),
            S("research:industry:line-balancing", 7185, "생산선 균형", "병목과 대기 원인을 분석해 분배 비율을 조정한다.", ResearchField.IndustryAndAutomation, 218, prerequisites: new[] { "research:industry:stock-sensors", "research:industry:junctions" }),
            S("research:industry:defense-supply", 7186, "방어 보급 자동화", "탄약과 연료를 방어 시설 버퍼까지 자동 공급한다.", ResearchField.IndustryAndAutomation, 252, prerequisites: new[] { "research:industry:line-balancing", "research:defense:tactical-command" }),
            S("research:industry:safety", 7187, "산업 안전", "누전, 누수, 역류와 기계 사고를 감지하고 회로를 격리한다.", ResearchField.IndustryAndAutomation, 276, prerequisites: new[] { "research:industry:maintenance", "research:plumbing:sewer" }),
            S("research:industry:dark-foundry", 7188, "심연 공장", "흑강과 마나를 대가로 고밀도 자동 생산을 운용한다.", ResearchField.IndustryAndAutomation, 360, prerequisites: new[] { "research:industry:rune-automation", "research:metallurgy:blacksteel", "research:medical:aberrant-augmentation" }),

            S("research:plumbing:basics", 7191, "배관 기초", "상수와 하수를 분리해 벽과 바닥 아래로 연결한다.", ResearchField.WaterAndSanitation, 72, prerequisites: new[] { "research:survival:sanitation", "research:metallurgy:iron" }),
            S("research:plumbing:storage-valves", 7192, "저수와 밸브", "수질별 저장 탱크와 구역 차단 밸브를 운용한다.", ResearchField.WaterAndSanitation, 98, prerequisites: new[] { "research:plumbing:basics" }),
            S("research:plumbing:pumped-water", 7193, "전동 급수", "펌프와 전력으로 수원을 던전 내부 상수망에 공급한다.", ResearchField.WaterAndSanitation, 126, prerequisites: new[] { "research:plumbing:storage-valves", "research:industry:distribution" }),
            S("research:plumbing:flush-sanitation", 7194, "수세 위생", "변기, 세면대, 목욕과 샤워를 상수망에 연결한다.", ResearchField.WaterAndSanitation, 152, prerequisites: new[] { "research:plumbing:pumped-water", "research:survival:support" }),
            S("research:plumbing:sewer", 7195, "하수 배관", "폐수 저장과 역류 방지를 위한 별도 하수망을 구축한다.", ResearchField.WaterAndSanitation, 174, prerequisites: new[] { "research:plumbing:flush-sanitation" }),
            S("research:plumbing:settling", 7196, "오수 침전", "폐수에서 재이용수와 슬러지를 분리한다.", ResearchField.WaterAndSanitation, 204, prerequisites: new[] { "research:plumbing:sewer", "research:agriculture:compost" }),
            S("research:plumbing:reuse", 7197, "정수와 재이용", "침전수와 소독제를 사용해 깨끗한 물로 되돌린다.", ResearchField.WaterAndSanitation, 242, prerequisites: new[] { "research:plumbing:settling", "research:pharmacology:distillation" }),
            S("research:plumbing:rune-purification", 7198, "룬 정화 순환", "마나와 룬으로 폐수 손실을 줄인 고효율 순환망을 만든다.", ResearchField.WaterAndSanitation, 304, prerequisites: new[] { "research:plumbing:reuse", "research:arcane:resonance", "research:industry:rune-grid" })
        };
    }

    private static Spec S(
        string id,
        int numericId,
        string name,
        string description,
        ResearchField field,
        float work,
        ResearchBlueprintRule rule = ResearchBlueprintRule.None,
        int blueprintId = -1,
        params string[] prerequisites)
    {
        return new Spec
        {
            Id = id,
            NumericId = numericId,
            Name = name,
            Description = description,
            Field = field,
            Work = work,
            Rule = rule,
            BlueprintId = blueprintId,
            Prerequisites = prerequisites ?? Array.Empty<string>()
        };
    }

    private static void EnsureFolders()
    {
        string current = "Assets";
        foreach (string segment in Root.Substring("Assets/".Length).Split('/'))
        {
            string next = $"{current}/{segment}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segment);
            }
            current = next;
        }
    }

    private static string Sanitize(string id)
    {
        return id.Replace("research:", string.Empty)
            .Replace(':', '_')
            .Replace('-', '_');
    }
}
#endif
