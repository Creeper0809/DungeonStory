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

        string[] facilityCodes = researchId switch
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
            _ => Array.Empty<string>()
        };
        if (facilityCodes.Length == 0)
        {
            return;
        }

        Dictionary<string, BuildingSO> buildingsByCode = AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building/Modular" })
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
            S("research:pharmacology:advanced", 7136, "고급 약리", "의료와 연금 지식을 결합해 고급 약품과 해독제를 만든다.", ResearchField.Pharmacology, 204, prerequisites: new[] { "research:pharmacology:stimulants", "research:survival:medical", "research:arcane:alchemy" })
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
