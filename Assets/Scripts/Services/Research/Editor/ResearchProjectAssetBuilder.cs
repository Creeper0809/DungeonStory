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
            S("research:commerce:secure-trade", 7014, "상권 통합", "요새화된 교역망으로 상권 전체를 묶는다.", ResearchField.CommerceAndCraft, 132, ResearchBlueprintRule.Shortcut, 6191, "research:commerce:expansion", "research:defense:fortification"),

            S("research:defense:watch", 7021, "경계 근무", "당직과 순찰, 침입 경보 절차를 확립한다.", ResearchField.DefenseAndTactics, 38),
            S("research:defense:fortification", 7022, "요새화", "성벽과 방어 설비를 보강한다.", ResearchField.DefenseAndTactics, 62, ResearchBlueprintRule.Required, 6102, "research:defense:watch"),
            S("research:defense:ranged-positions", 7023, "사격 방책", "엄폐와 원거리 교전 위치를 체계화한다.", ResearchField.DefenseAndTactics, 92, prerequisites: new[] { "research:defense:fortification" }),
            S("research:defense:tactical-command", 7024, "전술 지휘", "다중 경비의 전선과 교대를 통합 지휘한다.", ResearchField.DefenseAndTactics, 138, ResearchBlueprintRule.Shortcut, 6192, "research:defense:ranged-positions"),

            S("research:arcane:records", 7031, "기록 체계", "관찰과 실험 결과를 재현 가능한 기록으로 남긴다.", ResearchField.RecordsAndArcane, 36),
            S("research:arcane:alchemy", 7032, "연금 가공", "시약과 생체 물질을 안정적으로 가공한다.", ResearchField.RecordsAndArcane, 60, prerequisites: new[] { "research:arcane:records" }),
            S("research:arcane:advanced", 7033, "비전 연구", "마력과 의식 설비의 고급 원리를 해석한다.", ResearchField.RecordsAndArcane, 94, ResearchBlueprintRule.Required, 6104, "research:arcane:alchemy"),
            S("research:arcane:resonance", 7034, "비전 공명", "의식 장식과 비전 장치를 하나의 공명망으로 연결한다.", ResearchField.RecordsAndArcane, 142, ResearchBlueprintRule.Shortcut, 6193, "research:arcane:advanced", "research:authority:ritual"),

            S("research:control:restraints", 7041, "구속 관리", "포획과 구속, 감방 운용 절차를 정립한다.", ResearchField.CaptivityAndEntertainment, 40),
            S("research:control:labor", 7042, "노역 감독", "포로 노역의 작업과 감시 체계를 만든다.", ResearchField.CaptivityAndEntertainment, 66, prerequisites: new[] { "research:control:restraints" }),
            S("research:control:show", 7043, "흥행 운영", "무대와 관객, 공연자를 실제 운영 흐름으로 묶는다.", ResearchField.CaptivityAndEntertainment, 100, prerequisites: new[] { "research:control:labor", "research:commerce:retail" }),
            S("research:control:blood-show", 7044, "피의 흥행", "위험 공연과 공개 처벌을 통제된 흥행으로 만든다.", ResearchField.CaptivityAndEntertainment, 146, prerequisites: new[] { "research:control:show", "research:defense:watch" }),

            S("research:authority:quarters", 7051, "기본 숙소", "직원과 영주의 생활 구역을 분리한다.", ResearchField.AuthorityAndHousing, 34),
            S("research:authority:prestige", 7052, "장식과 위신", "장식과 공간 품질을 권위의 언어로 사용한다.", ResearchField.AuthorityAndHousing, 58, prerequisites: new[] { "research:authority:quarters" }),
            S("research:authority:office", 7053, "영주 집무", "방어 지휘와 경영 판단을 위한 집무 공간을 연다.", ResearchField.AuthorityAndHousing, 96, prerequisites: new[] { "research:authority:prestige", "research:defense:watch" }),
            S("research:authority:ritual", 7054, "의식 장식", "권위의 장식을 비전 의식의 매개로 가공한다.", ResearchField.AuthorityAndHousing, 128, prerequisites: new[] { "research:authority:office" })
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
