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
        public float FacilityThresholdWork;
        public ResearchBlueprintRule Rule;
        public int BlueprintId;
        public string[] Prerequisites;
    }

    [MenuItem("Tools/DungeonStory/Research/Rebuild Research Tree Assets")]
    public static void Rebuild()
    {
        EnsureFolders();
        IndustrialInfrastructureAssetBuilder.EnsureAssets();
        ProductionWorkshopContentAssetBuilder.EnsureAssets();
        ServiceRoomContentAssetBuilder.EnsureAssets();
        P1DefenseFacilityAssetBuilder.EnsureP1DefenseAssets();
        ResearchOverhaulContentAssetBuilder.EnsureAssets();
        V22ApparelContentAssetBuilder.EnsureAssets();
        CombatEquipmentAssetBuilder.BuildAll();
        V21OperationalContentLinkBuilder.EnsureAssets();
        Dictionary<int, FacilityBlueprintSO> blueprints = AssetDatabase
            .FindAssets("t:FacilityBlueprintSO", new[] { "Assets/Resources/SO/Blueprint" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>)
            .Where(asset => asset != null)
            .GroupBy(asset => asset.id)
            .ToDictionary(group => group.Key, group => group.First());

        IReadOnlyList<Spec> specs = CreateSpecs();
        Dictionary<string, BlueprintUnlockCollection> carriedUnlocks =
            CaptureConsolidatedUnlocks();
        DeleteAbsorbedProjectAssets(specs);

        Dictionary<string, ResearchProjectSO> projects = new Dictionary<string, ResearchProjectSO>(
            StringComparer.Ordinal);
        foreach (Spec spec in specs)
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

        IReadOnlyDictionary<int, string> canonicalBuildingOwners =
            BuildCanonicalBuildingOwners();
        foreach (Spec spec in specs)
        {
            ResearchProjectSO project = projects[spec.Id];
            blueprints.TryGetValue(spec.BlueprintId, out FacilityBlueprintSO blueprint);
            if (blueprint != null)
            {
                blueprint.targetResearchProjectId = spec.Id;
                EditorUtility.SetDirty(blueprint);
            }

            IEnumerable<BlueprintUnlock> sourceUnlocks = carriedUnlocks.TryGetValue(
                spec.Id,
                out BlueprintUnlockCollection carried)
                    ? carried.Items
                    : project.Unlocks;
            if (blueprint != null && blueprint.unlocks != null && blueprint.unlocks.Count > 0)
            {
                if (!sourceUnlocks.Any())
                {
                    sourceUnlocks = blueprint.Unlocks;
                }
                blueprint.unlocks = new BlueprintUnlockCollection();
                EditorUtility.SetDirty(blueprint);
            }
            BlueprintUnlockCollection unlocks = CloneUnlocks(sourceUnlocks.Where(unlock =>
                unlock is not BlueprintBuildingUnlock building
                || !canonicalBuildingOwners.TryGetValue(building.buildingId, out string owner)
                || string.Equals(owner, spec.Id, StringComparison.Ordinal)));

            AppendProductionStationUnlocks(spec.Id, unlocks);
            AppendServiceRoomUnlocks(spec.Id, unlocks);
            AppendResearchOverhaulUnlocks(spec.Id, unlocks);
            AppendV22ApparelUnlocks(spec.Id, unlocks);
            project.Configure(
                spec.Id,
                spec.Name,
                spec.Description,
                spec.Field,
                spec.Work,
                spec.Rule,
                blueprint,
                spec.Prerequisites.Select(id => projects[id]),
                unlocks,
                requiredFacilityCapacity: ResolveFacilityRequirements(spec),
                causalPrerequisites: BuildCausalPrerequisites(spec, projects));
            EditorUtility.SetDirty(project);
        }

        AttachArchiveAbility();
        RewriteAbsorbedResearchRequirements();
        V21OperationalContentLinkBuilder.WireInstallationComponents(projects);
        ResearchUnlockBundleAssetBuilder.EnsureAssets(projects.Values);
        GameContentCatalogAssetBuilder.ReindexResearchProjects();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects.Values);
        IReadOnlyList<string> errors = catalog.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }

        IReadOnlyList<string> productionErrors =
            BranchedProductionNetworkDebugScenarios.Validate();
        if (productionErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", productionErrors));
        }

        Debug.Log($"Research tree assets rebuilt: {projects.Count} projects.");
    }

    [MenuItem("Tools/DungeonStory/Research/Validate Mining Expansion Gates")]
    public static void EnsureDungeonExpansionProjects()
    {
        IReadOnlyList<Spec> allSpecs = CreateSpecs();
        Spec[] expansionSpecs = allSpecs
            .Where(spec => DungeonSpaceExpansionCatalog.TryGet(spec.Id, out _))
            .OrderBy(spec => spec.NumericId)
            .ToArray();
        if (expansionSpecs.Length != DungeonSpaceExpansionCatalog.All.Count)
        {
            throw new InvalidOperationException(
                $"Dungeon expansion research spec count {expansionSpecs.Length} does not match runtime definition count {DungeonSpaceExpansionCatalog.All.Count}.");
        }

        Dictionary<string, ResearchProjectSO> projects = AssetDatabase
            .FindAssets("t:ResearchProjectSO", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null && project.ProjectId.IsValid)
            .GroupBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.Ordinal);

        string[] legacyIds = projects.Keys
            .Where(id => id.StartsWith(
                "research:dungeon-expansion:",
                StringComparison.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (legacyIds.Length > 0)
        {
            throw new InvalidOperationException(
                "Legacy standalone dungeon-expansion research must not exist: "
                + string.Join(", ", legacyIds));
        }

        foreach (Spec spec in expansionSpecs)
        {
            if (!projects.TryGetValue(spec.Id, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"Existing mining expansion research is missing: {spec.Id}.");
            }
            if (project.id != spec.NumericId)
            {
                throw new InvalidOperationException(
                    $"Expansion research '{spec.Id}' has numeric ID {project.id}; expected {spec.NumericId}.");
            }
            if (!Mathf.Approximately(project.RequiredWork, spec.Work))
            {
                throw new InvalidOperationException(
                    $"Expansion research '{spec.Id}' has {project.RequiredWork:0.###} WU; expected {spec.Work:0.###}.");
            }
            string[] actualPrerequisites = project.Prerequisites
                .Select(value => value.ProjectId.Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedPrerequisites = spec.Prerequisites
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualPrerequisites.SequenceEqual(expectedPrerequisites))
            {
                throw new InvalidOperationException(
                    $"Expansion research '{spec.Id}' prerequisites are "
                    + string.Join(",", actualPrerequisites)
                    + "; expected "
                    + string.Join(",", expectedPrerequisites)
                    + ".");
            }
        }

        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects.Values);
        IReadOnlyList<string> errors = catalog.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Dungeon expansion research catalog is invalid: "
                + string.Join(" | ", errors));
        }

        Debug.Log(
            "Existing mining research expansion gates validated: "
            + string.Join(", ", expansionSpecs.Select(spec =>
                $"{spec.Id}={spec.Work:0}WU")));
    }

    [MenuItem("Tools/DungeonStory/Research/Patch Q03 Archive Ability")]
    public static void PatchQ03ArchiveAbility()
    {
        // The archive item capacity and the research-facility capability graph
        // are one authored contract. Patching only Q03's physical archive left
        // queued research suspended because Q01/Q03 contributed no Basic or
        // Archive capability at runtime.
        AttachArchiveAbility();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Q03 archive and research facility capabilities patched.");
    }

    private static Dictionary<string, BlueprintUnlockCollection>
        CaptureConsolidatedUnlocks()
    {
        Dictionary<string, List<BlueprintUnlock>> grouped =
            new Dictionary<string, List<BlueprintUnlock>>(StringComparer.Ordinal);
        foreach (ResearchProjectSO project in AssetDatabase
                     .FindAssets("t:ResearchProjectSO", new[] { Root })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
                     .Where(project => project != null))
        {
            string ownerId = V21ResearchConsolidation.Normalize(project.ProjectId.Value);
            if (!grouped.TryGetValue(ownerId, out List<BlueprintUnlock> unlocks))
            {
                unlocks = new List<BlueprintUnlock>();
                grouped.Add(ownerId, unlocks);
            }
            unlocks.AddRange(project.Unlocks);
        }

        return grouped.ToDictionary(
            pair => pair.Key,
            pair => CloneUnlocks(pair.Value),
            StringComparer.Ordinal);
    }

    private static void DeleteAbsorbedProjectAssets(IReadOnlyList<Spec> specs)
    {
        HashSet<string> retainedIds = specs
            .Select(spec => spec.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string path in AssetDatabase.FindAssets(
                     "t:ResearchProjectSO",
                     new[] { Root }).Select(AssetDatabase.GUIDToAssetPath))
        {
            ResearchProjectSO project = AssetDatabase.LoadAssetAtPath<ResearchProjectSO>(path);
            if (project != null && !retainedIds.Contains(project.ProjectId.Value))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static void RewriteAbsorbedResearchRequirements()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            if (property.Next(true))
            {
                do
                {
                    if (property.propertyType != SerializedPropertyType.String
                        || !string.Equals(
                            property.name,
                            "requiredResearchId",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string normalized = V21ResearchConsolidation.Normalize(
                        property.stringValue);
                    if (!string.Equals(
                        normalized,
                        property.stringValue,
                        StringComparison.Ordinal))
                    {
                        property.stringValue = normalized;
                        changed = true;
                    }
                }
                while (property.Next(true));
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }
    }

    private static IReadOnlyDictionary<int, string> BuildCanonicalBuildingOwners()
    {
        Dictionary<int, string> owners = new Dictionary<int, string>();
        foreach ((string researchId, int[] ids) in
                 ResearchOverhaulContentAssetBuilder.GetFacilityUnlockIds())
        {
            foreach (int id in ids)
            {
                owners[id] = V21ResearchConsolidation.Normalize(researchId);
            }
        }
        foreach ((string researchId, int[] ids) in
                 V22ApparelContentAssetBuilder.GetFacilityUnlockIds())
        {
            foreach (int id in ids)
            {
                owners[id] = V21ResearchConsolidation.Normalize(researchId);
            }
        }

        BuildingSO[] buildings = AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
        foreach ((string researchId, string code) in
                 ResearchOverhaulContentAssetBuilder.GetExistingFacilityCodes())
        {
            BuildingSO building = buildings.FirstOrDefault(candidate => string.Equals(
                candidate.GetAbility<BuildingFacilityPartAbility>()?.code,
                code,
                StringComparison.Ordinal));
            if (building == null)
            {
                throw new InvalidOperationException(
                    $"Canonical reward facility '{code}' for '{researchId}' does not exist.");
            }
            owners[building.id] = V21ResearchConsolidation.Normalize(researchId);
        }
        return owners;
    }

    private static IEnumerable<ResearchPrerequisiteLink> BuildCausalPrerequisites(
        Spec spec,
        IReadOnlyDictionary<string, ResearchProjectSO> projects)
    {
        foreach (string prerequisiteId in spec.Prerequisites)
        {
            ResearchProjectSO prerequisite = projects[prerequisiteId];
            ResearchPrerequisiteKind kind = InferPrerequisiteKind(
                spec.Id,
                prerequisiteId);
            yield return new ResearchPrerequisiteLink(
                prerequisite,
                kind,
                $"{prerequisite.DisplayName}의 {GetCausalKnowledgeLabel(kind)}이(가) {spec.Name} 구현에 직접 필요하다.");
        }
    }

    private static ResearchPrerequisiteKind InferPrerequisiteKind(
        string projectId,
        string prerequisiteId)
    {
        string combined = $"{projectId}|{prerequisiteId}";
        if (combined.Contains("safety", StringComparison.Ordinal)
            || combined.Contains("protection", StringComparison.Ordinal)
            || combined.Contains("cold-work", StringComparison.Ordinal))
        {
            return ResearchPrerequisiteKind.Safety;
        }
        if (prerequisiteId.Contains("logistics", StringComparison.Ordinal)
            || prerequisiteId.Contains("layout", StringComparison.Ordinal)
            || prerequisiteId.Contains("maintenance", StringComparison.Ordinal)
            || prerequisiteId.Contains("service", StringComparison.Ordinal))
        {
            return ResearchPrerequisiteKind.Operations;
        }
        if (prerequisiteId.Contains("records", StringComparison.Ordinal)
            || prerequisiteId.Contains("arcane", StringComparison.Ordinal)
            || prerequisiteId.Contains("resonance", StringComparison.Ordinal)
            || prerequisiteId.Contains("testing", StringComparison.Ordinal)
            || prerequisiteId.Contains("ballistics", StringComparison.Ordinal))
        {
            return ResearchPrerequisiteKind.Theory;
        }
        if (prerequisiteId.Contains("textile", StringComparison.Ordinal)
            || prerequisiteId.Contains("tailoring", StringComparison.Ordinal)
            || prerequisiteId.Contains("forge", StringComparison.Ordinal)
            || prerequisiteId.Contains("cuisine", StringComparison.Ordinal))
        {
            return ResearchPrerequisiteKind.Technique;
        }
        return ResearchPrerequisiteKind.Engineering;
    }

    private static string GetCausalKnowledgeLabel(ResearchPrerequisiteKind kind) =>
        kind switch
        {
            ResearchPrerequisiteKind.Theory => "이론",
            ResearchPrerequisiteKind.Technique => "제작 기법",
            ResearchPrerequisiteKind.Safety => "안전 기준",
            ResearchPrerequisiteKind.Operations => "운용 절차",
            _ => "공학 원리"
        };

    private static void AppendResearchOverhaulUnlocks(
        string researchId,
        BlueprintUnlockCollection unlocks)
    {
        List<int> buildingIds = new List<int>();
        foreach (KeyValuePair<string, int[]> pair in
                 ResearchOverhaulContentAssetBuilder.GetFacilityUnlockIds())
        {
            if (string.Equals(
                V21ResearchConsolidation.Normalize(pair.Key),
                researchId,
                StringComparison.Ordinal))
            {
                buildingIds.AddRange(pair.Value);
            }
        }

        foreach (KeyValuePair<string, string> pair in
                 ResearchOverhaulContentAssetBuilder.GetExistingFacilityCodes())
        {
            if (!string.Equals(
                V21ResearchConsolidation.Normalize(pair.Key),
                researchId,
                StringComparison.Ordinal))
            {
                continue;
            }
            string existingCode = pair.Value;
            BuildingSO existing = AssetDatabase.FindAssets(
                    "t:BuildingSO",
                    new[] { "Assets/Resources/SO/Building" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                .FirstOrDefault(building => string.Equals(
                    building?.GetAbility<BuildingFacilityPartAbility>()?.code,
                    existingCode,
                    StringComparison.Ordinal));
            if (existing == null)
            {
                throw new InvalidOperationException(
                    $"Research '{researchId}' cannot find existing facility '{existingCode}'.");
            }
            buildingIds.Add(existing.id);
        }

        foreach (int buildingId in buildingIds.Distinct())
        {
            if (!unlocks.Items.OfType<BlueprintBuildingUnlock>()
                .Any(unlock => unlock.buildingId == buildingId))
            {
                unlocks.Add(new BlueprintBuildingUnlock { buildingId = buildingId });
            }
        }
    }

    private static void AppendV22ApparelUnlocks(
        string researchId,
        BlueprintUnlockCollection unlocks)
    {
        foreach (KeyValuePair<string, int[]> pair in
                 V22ApparelContentAssetBuilder.GetFacilityUnlockIds())
        {
            if (!string.Equals(
                    V21ResearchConsolidation.Normalize(pair.Key),
                    researchId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            foreach (int buildingId in pair.Value)
            {
                if (!unlocks.Items.OfType<BlueprintBuildingUnlock>()
                        .Any(value => value.buildingId == buildingId))
                {
                    unlocks.Add(new BlueprintBuildingUnlock { buildingId = buildingId });
                }
            }
        }
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
        string[] facilityCodes = industrialUnlocks
            .Where(pair => string.Equals(
                V21ResearchConsolidation.Normalize(pair.Key),
                researchId,
                StringComparison.Ordinal))
            .SelectMany(pair => pair.Value)
            .Concat(researchId switch
        {
            "research:cuisine:milling" => new[] { "P01", "WS01" },
            "research:cuisine:fermentation" =>
                new[] { "P02", "WS02", "WS03", "WS05" },
            "research:cuisine:livestock" => new[] { "WS15", "WS16" },
            "research:cuisine:baking" => new[] { "WS09" },
            "research:cuisine:kitchen-hygiene" =>
                new[] { "WS11", "WS12", "WS13" },
            "research:cuisine:controlled-fermentation" =>
                new[] { "WS04", "WS06" },
            "research:cuisine:distilling-aging" => new[] { "WS07" },
            "research:forestry:sawmill" => new[] { "P03", "WS19" },
            "research:forestry:charcoal" => new[] { "P04" },
            "research:mining:stonecutting" => new[] { "P05", "WS20" },
            "research:mining:sorting" => new[] { "P06" },
            "research:metallurgy:iron" => new[] { "P07", "WS21" },
            "research:metallurgy:steel" => new[] { "P08" },
            "research:metallurgy:precious" => new[] { "P09", "WS22" },
            "research:metallurgy:blacksteel" => new[] { "P10", "WS23" },
            "research:textile:fiber" => new[] { "P11", "WS24" },
            "research:textile:tanning" => new[] { "P12" },
            "research:agriculture:compost" => new[] { "P13" },
            "research:pharmacology:distillation" => new[] { "P14" },
            "research:cuisine:crops" => new[] { "P15" },
            "research:survival:preservation" =>
                new[] { "P16", "WS14", "WS18" },
            "research:husbandry:feed" => new[] { "P17", "WS17" },
            "research:pharmacology:antiseptic" =>
                new[] { "P18", "WS25" },
            "research:arcane:alchemy" => new[] { "P19", "WS26" },
            "research:textile:dreamweave" => new[] { "P20" },
            "research:metallurgy:primitive" => new[] { "P21", "WS27" },
            "research:mining:quarry" => new[] { "P22" },
            "research:agriculture:field" => new[] { "P23" },
            "research:agriculture:indoor" => new[] { "P24", "WS28" },
            "research:survival:sanitation" => new[] { "P25" },
            "research:survival:medical" => new[] { "M01" },
            "research:environment:cold-work" =>
                new[] { "L08", "E10", "E12", "E13", "E14" },
            "research:environment:rune-insulation" =>
                new[] { "E11" },
            "research:medical:anatomy" => new[] { "M02" },
            "research:medical:surgery" => new[] { "M03", "M04", "M05" },
            "research:medical:prosthetics" => new[] { "M06", "M07" },
            "research:medical:organ-preservation" => new[] { "M08" },
            "research:medical:xenotransplant" => new[] { "M09", "M10", "M11" },
            "research:medical:aberrant-augmentation" => new[] { "M12", "M13" },
            "research:defense:tactical-command" => new[] { "T01" },
            "research:defense:supply" => new[] { "DF03", "DF04" },
            "research:defense:corridor-mechanisms" => new[] { "DF05" },
            "research:defense:rune-identification" => new[] { "DF01" },
            "research:defense:remote-control" => new[] { "DF02" },
            "research:defense:siege-fortification" => new[] { "DF06" },
            _ => Array.Empty<string>()
        })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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

    private static void AppendServiceRoomUnlocks(
        string researchId,
        BlueprintUnlockCollection unlocks)
    {
        if (unlocks == null)
        {
            return;
        }

        int[] buildingIds = ServiceRoomContentAssetBuilder
            .GetResearchUnlockIds()
            .Where(pair => string.Equals(
                V21ResearchConsolidation.Normalize(pair.Key),
                researchId,
                StringComparison.Ordinal))
            .SelectMany(pair => pair.Value)
            .Distinct()
            .ToArray();

        foreach (int buildingId in buildingIds)
        {
            bool exists = unlocks.Items
                .OfType<BlueprintBuildingUnlock>()
                .Any(unlock => unlock.buildingId == buildingId);
            if (!exists)
            {
                unlocks.Add(new BlueprintBuildingUnlock
                {
                    buildingId = buildingId
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
        AttachQ03ArchiveAbilityDefinition();

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

        AttachResearchCapacity(
            "Q01",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Basic, 1));
        AttachResearchCapacity(
            "Q02",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Basic, 1),
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Arcane, 1));
        AttachResearchCapacity(
            "Q03",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Archive, 1));
        AttachResearchCapacity(
            "Q04",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Reagent, 1));
        AttachResearchCapacity(
            "Q05",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Specimen, 1));
        AttachResearchCapacity(
            "Q06",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Design, 1));
        AttachResearchCapacity(
            "P19",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Basic, 1),
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Reagent, 1),
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Arcane, 1));
        AttachResearchCapacity(
            "P1_ResearchLab",
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Basic, 2),
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Archive, 1),
            new ResearchFacilityContribution(ResearchFacilityCapabilityId.Advanced, 1));
    }

    private static void AttachQ03ArchiveAbilityDefinition()
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
    }

    private static void AttachResearchCapacity(
        string codeOrAssetName,
        params ResearchFacilityContribution[] contributions)
    {
        BuildingSO building = AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => new
            {
                Path = path,
                Asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(path)
            })
            .Where(entry => entry.Asset != null)
            .FirstOrDefault(entry =>
                string.Equals(
                    entry.Asset.GetAbility<BuildingFacilityPartAbility>()?.code,
                    codeOrAssetName,
                    StringComparison.Ordinal)
                || string.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(entry.Path),
                    codeOrAssetName,
                    StringComparison.Ordinal))
            ?.Asset;
        if (building == null)
        {
            throw new InvalidOperationException(
                $"연구 수용력을 연결할 시설을 찾지 못했습니다: {codeOrAssetName}");
        }

        building.AbilityModules.Remove<BuildingResearchCapacityAbility>();
        BuildingResearchCapacityAbility ability =
            new BuildingResearchCapacityAbility();
        ability.Configure(contributions);
        building.AbilityModules.Add(ability);
        building.AbilityModules.EnsureStableIds();
        building.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(building);
    }

    private static IReadOnlyList<ResearchFacilityRequirement>
        ResolveFacilityRequirements(Spec spec)
    {
        float facilityWork = spec.FacilityThresholdWork > 0f
            ? spec.FacilityThresholdWork
            : spec.Work;
        List<ResearchFacilityRequirement> requirements =
            new List<ResearchFacilityRequirement>();
        void Add(ResearchFacilityCapabilityId capability, int count = 1) =>
            requirements.Add(new ResearchFacilityRequirement(capability, count));

        if (DungeonSpaceExpansionCatalog.TryGet(spec.Id, out var expansion))
        {
            Add(ResearchFacilityCapabilityId.Basic);
            if (expansion.Tier >= 2)
            {
                Add(ResearchFacilityCapabilityId.Design);
            }
            if (expansion.Tier >= 3)
            {
                Add(ResearchFacilityCapabilityId.Advanced);
            }

            return requirements.ToArray();
        }

        switch (spec.Field)
        {
            case ResearchField.SurgeryAndTransplant:
                Add(ResearchFacilityCapabilityId.Basic);
                Add(ResearchFacilityCapabilityId.Specimen);
                if (facilityWork >= 200f)
                {
                    Add(ResearchFacilityCapabilityId.Advanced);
                }
                break;
            case ResearchField.Pharmacology:
                Add(ResearchFacilityCapabilityId.Basic);
                Add(ResearchFacilityCapabilityId.Reagent);
                if (facilityWork >= 120f)
                {
                    Add(ResearchFacilityCapabilityId.Arcane);
                }
                if (facilityWork >= 220f)
                {
                    Add(ResearchFacilityCapabilityId.Advanced);
                }
                break;
            case ResearchField.RecordsAndArcane:
                Add(ResearchFacilityCapabilityId.Basic);
                Add(ResearchFacilityCapabilityId.Archive);
                if (facilityWork >= 55f)
                {
                    Add(ResearchFacilityCapabilityId.Arcane);
                }
                if (facilityWork >= 180f)
                {
                    Add(ResearchFacilityCapabilityId.Advanced);
                }
                break;
            case ResearchField.IndustryAndAutomation:
                Add(ResearchFacilityCapabilityId.Basic, 2);
                Add(ResearchFacilityCapabilityId.Design);
                if (facilityWork >= 190f)
                {
                    Add(ResearchFacilityCapabilityId.Advanced);
                }
                break;
            case ResearchField.WaterAndSanitation:
            case ResearchField.DefenseAndTactics:
                Add(
                    ResearchFacilityCapabilityId.Basic,
                    facilityWork >= 130f ? 2 : 1);
                Add(ResearchFacilityCapabilityId.Design);
                if (facilityWork >= 220f)
                {
                    Add(ResearchFacilityCapabilityId.Advanced);
                }
                break;
            case ResearchField.CommerceAndCraft:
            case ResearchField.Agriculture:
            case ResearchField.Forestry:
            case ResearchField.Mining:
            case ResearchField.Husbandry:
            case ResearchField.Metallurgy:
            case ResearchField.Textiles:
            case ResearchField.Cuisine:
                Add(ResearchFacilityCapabilityId.Basic);
                if (facilityWork >= 90f)
                {
                    Add(ResearchFacilityCapabilityId.Design);
                }
                if (facilityWork >= 210f)
                {
                    Add(ResearchFacilityCapabilityId.Advanced);
                }
                break;
            case ResearchField.AuthorityAndHousing:
            case ResearchField.CaptivityAndEntertainment:
                Add(ResearchFacilityCapabilityId.Basic);
                if (facilityWork >= 90f)
                {
                    Add(ResearchFacilityCapabilityId.Archive);
                }
                break;
            case ResearchField.LifeAndSurvival:
                Add(ResearchFacilityCapabilityId.Basic);
                if (spec.Id.Contains(":medical", StringComparison.Ordinal))
                {
                    Add(ResearchFacilityCapabilityId.Specimen);
                }
                else if (spec.Id.Contains(":environment", StringComparison.Ordinal))
                {
                    Add(ResearchFacilityCapabilityId.Design);
                }
                break;
            default:
                Add(ResearchFacilityCapabilityId.Basic);
                break;
        }

        return requirements
            .GroupBy(requirement => requirement.capability)
            .Select(group => new ResearchFacilityRequirement(
                group.Key,
                group.Sum(requirement => requirement.requiredCount)))
            .OrderBy(requirement => requirement.capability)
            .ToArray();
    }

    private static IReadOnlyList<Spec> CreateBaseSpecs()
    {
        return new[]
        {
            S(ServiceRoomResearchIds.ServiceFlow, 7015, "서비스 동선", "간이 서비스를 접수·대기·이용·결제로 나누어 관리형으로 운영한다.", ResearchField.CommerceAndCraft, 68, prerequisites: new[] { "research:commerce:logistics" }),
            S(ServiceRoomResearchIds.HospitalityOperations, 7016, "환대 운영", "숙박 접수와 객실 배정·정리 절차를 표준화한다.", ResearchField.AuthorityAndHousing, 86, prerequisites: new[] { ServiceRoomResearchIds.ServiceFlow, "research:authority:quarters" }),
            S(ServiceRoomResearchIds.BathBusiness, 7017, "목욕 영업", "목욕 접수와 급배수·위생 관리 절차를 갖춘다.", ResearchField.WaterAndSanitation, 94, prerequisites: new[] { ServiceRoomResearchIds.ServiceFlow, "research:survival:sanitation" }),
            S(ServiceRoomResearchIds.MedicalReception, 7018, "의료 접수", "환자를 접수하고 중증도로 분류해 치료 대기를 관리한다.", ResearchField.SurgeryAndTransplant, 102, prerequisites: new[] { ServiceRoomResearchIds.ServiceFlow, "research:survival:medical" }),
            S(ServiceRoomResearchIds.ServiceAutomation, 7019, "서비스 자동화", "자동 계산·순번·보온·객실 배정 장치로 직원 부담을 줄인다.", ResearchField.IndustryAndAutomation, 176, prerequisites: new[] { ServiceRoomResearchIds.HospitalityOperations, ServiceRoomResearchIds.BathBusiness, ServiceRoomResearchIds.MedicalReception, "research:industry:distribution" }),
            S("research:survival:sanitation", 7001, "기초 위생", "오염을 통제하고 기본 위생 설비를 운용한다.", ResearchField.LifeAndSurvival, 36),
            S("research:survival:support", 7002, "생활 지원", "식사와 휴식, 기본 생활 설비의 효율을 높인다.", ResearchField.LifeAndSurvival, 56, ResearchBlueprintRule.Required, 6103, "research:survival:sanitation"),
            S("research:survival:preservation", 7003, "식량 보존", "식량 부패를 늦추고 보존 조리법을 정리한다.", ResearchField.LifeAndSurvival, 84, prerequisites: new[] { "research:survival:support" }),
            S("research:survival:medical", 7004, "의료 회복", "부상과 질병을 체계적으로 안정화하고 치료한다.", ResearchField.LifeAndSurvival, 126, prerequisites: new[] { "research:survival:preservation", "research:arcane:alchemy" }),
            S("research:environment:cold-work", 7005, "저온 작업 보호", "냉장 구역의 짧은 운반과 장기 근무를 분리하고 보호장비 보관함, 보온 점액 패드와 방한 작업복을 운용한다.", ResearchField.LifeAndSurvival, 104, prerequisites: new[] { "research:survival:preservation", "research:textile:fiber" }),
            S("research:environment:rune-insulation", 7006, "룬 단열학", "룬 방한복과 내한성 점액 배양으로 극저온 작업 교대를 가능하게 한다.", ResearchField.LifeAndSurvival, 196, prerequisites: new[] { "research:environment:cold-work", "research:textile:rune-leather", "research:arcane:advanced" }),

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
            S("research:cuisine:lavish", 7126, "호화·보존식", "세척·제빵·발효·보존 설비를 모두 활용해 호화식과 보존식을 만든다.", ResearchField.Cuisine, 186, prerequisites: new[] { "research:cuisine:baking", "research:cuisine:kitchen-hygiene", "research:cuisine:fermentation", "research:survival:preservation" }),
            S("research:cuisine:baking", 7127, "제빵", "반죽과 벽돌 오븐을 이용해 파이와 구운 고급식을 만든다.", ResearchField.Cuisine, 152, prerequisites: new[] { "research:cuisine:milling" }),
            S("research:cuisine:kitchen-hygiene", 7128, "주방 위생", "상수와 배수에 연결한 전처리 싱크로 고급식 재료를 안전하게 세척한다.", ResearchField.Cuisine, 168, prerequisites: new[] { "research:cuisine:livestock", "research:plumbing:sewer" }),
            S("research:cuisine:controlled-fermentation", 7129, "제어 발효", "전력과 배관으로 발효 온도와 세척·병입을 제어한다.", ResearchField.Cuisine, 198, prerequisites: new[] { "research:cuisine:fermentation", "research:industry:distribution", "research:plumbing:sewer" }),
            S("research:cuisine:distilling-aging", 7130, "주류 증류·숙성", "분별 증류와 오크 숙성으로 중성 알코올과 밤 증류주를 만든다.", ResearchField.Cuisine, 220, prerequisites: new[] { "research:cuisine:fermentation", "research:pharmacology:distillation" }),

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
            S("research:plumbing:rune-purification", 7198, "룬 정화 순환", "마나와 룬으로 폐수 손실을 줄인 고효율 순환망을 만든다.", ResearchField.WaterAndSanitation, 304, prerequisites: new[] { "research:plumbing:reuse", "research:arcane:resonance", "research:industry:rune-grid" }),

            S("research:defense:supply", 7201, "방어 보급학", "탄약·촉매 보급고와 함정 정비대로 방어시설의 실물 보급과 재장전을 표준화한다.", ResearchField.DefenseAndTactics, 112, prerequisites: new[] { "research:defense:fortification", "research:commerce:logistics" }),
            S("research:defense:corridor-mechanisms", 7202, "복도 기구학", "침입 감지기와 문 연동 낙하문으로 좁은 통로의 발동 순서를 설계한다.", ResearchField.DefenseAndTactics, 142, prerequisites: new[] { "research:defense:supply", "research:metallurgy:iron" }),
            S("research:defense:rune-identification", 7203, "룬 식별", "감지 장치가 허용 대상과 적대 대상을 더 정확하게 구분하도록 룬 식별 규칙을 새긴다.", ResearchField.DefenseAndTactics, 176, prerequisites: new[] { "research:defense:corridor-mechanisms", "research:arcane:advanced" }),
            S("research:defense:remote-control", 7204, "원격 통제", "방어 통제대에서 무장 정책과 발동 구역을 원격으로 전환한다.", ResearchField.DefenseAndTactics, 214, prerequisites: new[] { "research:defense:rune-identification", "research:industry:distribution" }),
            S("research:defense:siege-fortification", 7205, "공성 요새화", "강화 낙하문과 벽면 발사구를 공성 장비와 중장갑 침입자에 맞게 보강한다.", ResearchField.DefenseAndTactics, 258, prerequisites: new[] { "research:defense:remote-control", "research:defense:ranged-positions", "research:metallurgy:steel" }),
            S("research:defense:alliance-signals", 7206, "동맹 신호학", "동맹 지원군의 접근 경로와 방어시설 허용 목록을 공유하는 신호 체계를 만든다.", ResearchField.DefenseAndTactics, 296, prerequisites: new[] { "research:defense:siege-fortification", "research:commerce:secure-trade" }),
            S("research:medical:slime-bioengineering", 7211, "점액 생체공학", "점액 외피와 응집핵을 안정화하고 재성형한다.", ResearchField.SurgeryAndTransplant, 184, prerequisites: new[] { "research:medical:anatomy", "research:arcane:alchemy" }),
            S("research:medical:mycelial-grafting", 7212, "균사 접목학", "균사 군체와 포자낭을 세정하고 재배양한다.", ResearchField.SurgeryAndTransplant, 196, prerequisites: new[] { "research:medical:anatomy", "research:forestry:fungal" }),
            S("research:medical:avian-prosthetics", 7213, "조류 보철학", "기낭과 날개의 하중을 보존하는 경량 고정술을 개발한다.", ResearchField.SurgeryAndTransplant, 206, prerequisites: new[] { "research:medical:prosthetics", "research:textile:layered" }),
            S("research:medical:construct-core-maintenance", 7214, "구성체 핵 정비", "냉각관·서보·감지핵과 동력핵을 시술 주문으로 정비한다.", ResearchField.SurgeryAndTransplant, 218, prerequisites: new[] { "research:medical:prosthetics", "research:industry:distribution" }),
            S("research:medical:bloodcraft-augmentation", 7215, "혈술 개조", "혈액낭과 야간안을 혈술로 강화하고 거부 부담을 통제한다.", ResearchField.SurgeryAndTransplant, 252, prerequisites: new[] { "research:medical:xenotransplant", "research:pharmacology:advanced" }),
            S("research:medical:mana-core-engineering", 7216, "마핵 공학", "마핵과 열낭에 룬 구속을 적용해 비전 개조를 안정화한다.", ResearchField.SurgeryAndTransplant, 286, prerequisites: new[] { "research:medical:aberrant-augmentation", "research:arcane:resonance" })
        };
    }

    private static IReadOnlyList<Spec> CreateSpecs()
    {
        Spec[] authored = CreateBaseSpecs()
            .Concat(CreateExpansionSpecs())
            .Select(ApplyApprovedWorkBand)
            .ToArray();
        Spec[] consolidated = ConsolidateForV21(authored).ToArray();
        foreach (Spec spec in consolidated)
        {
            spec.FacilityThresholdWork = spec.Work;
            spec.Work = Mathf.Ceil(spec.Work
                * SettlementLaborAuthority.EffectiveOutputWuPerAdultDay
                / SettlementLaborAuthority.HistoricalTheoreticalCapacityWuPerAdultDay);
        }
        float totalWork = consolidated.Sum(spec => spec.Work);
        if (!Mathf.Approximately(totalWork, 63173f))
        {
            throw new InvalidOperationException(
                $"V27 research pacing contract mismatch: {totalWork:0.##} work; expected 63173.");
        }
        return consolidated;
    }

    private static IReadOnlyList<Spec> ConsolidateForV21(
        IReadOnlyList<Spec> authored)
    {
        Dictionary<string, Spec[]> groups = authored
            .GroupBy(spec => V21ResearchConsolidation.Normalize(spec.Id), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, Spec> merged = new Dictionary<string, Spec>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Spec[]> pair in groups)
        {
            Spec survivor = pair.Value.First(spec => string.Equals(
                spec.Id,
                pair.Key,
                StringComparison.Ordinal));
            Spec blueprintOwner = pair.Value.FirstOrDefault(spec => spec.BlueprintId >= 0)
                ?? survivor;
            merged.Add(pair.Key, new Spec
            {
                Id = survivor.Id,
                NumericId = survivor.NumericId,
                Name = ResolveV21DisplayName(survivor.Id, survivor.Name),
                Description = ResolveV21Description(
                    survivor.Id,
                    survivor.Description,
                    pair.Value.Length),
                Field = survivor.Field,
                Work = pair.Value.Sum(spec => spec.Work),
                FacilityThresholdWork = pair.Value.Sum(spec => spec.Work),
                Rule = blueprintOwner.Rule,
                BlueprintId = blueprintOwner.BlueprintId,
                Prerequisites = pair.Value
                    .SelectMany(spec => spec.Prerequisites ?? Array.Empty<string>())
                    .Select(V21ResearchConsolidation.Normalize)
                    .Where(id => !string.Equals(id, survivor.Id, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            });
        }

        foreach (Spec spec in merged.Values)
        {
            spec.Prerequisites = spec.Prerequisites
                .Where(prerequisite => !spec.Prerequisites.Any(other =>
                    !string.Equals(other, prerequisite, StringComparison.Ordinal)
                    && IsReachable(other, prerequisite, merged)))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (spec.Prerequisites.Length > 4)
            {
                throw new InvalidOperationException(
                    $"V21 research '{spec.Id}' retains {spec.Prerequisites.Length} direct prerequisites after transitive reduction.");
            }
        }

        Spec[] result = merged.Values
            .OrderBy(spec => spec.NumericId)
            .ToArray();
        float totalWork = result.Sum(spec => spec.Work);
        if (result.Length != 180 || !Mathf.Approximately(totalWork, 138824f))
        {
            throw new InvalidOperationException(
                $"V21 research contract mismatch: {result.Length} projects / {totalWork:0.##} work; expected 180 / 138824.");
        }
        return result;
    }

    private static bool IsReachable(
        string start,
        string target,
        IReadOnlyDictionary<string, Spec> projects)
    {
        Stack<string> pending = new Stack<string>();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(start);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }
            if (string.Equals(current, target, StringComparison.Ordinal))
            {
                return true;
            }
            if (projects.TryGetValue(current, out Spec project))
            {
                foreach (string prerequisite in project.Prerequisites)
                {
                    pending.Push(prerequisite);
                }
            }
        }
        return false;
    }

    private static string ResolveV21DisplayName(string id, string fallback) => id switch
    {
        "research:agriculture:phenology" => "생물계절학과 종자 선별",
        "research:agriculture:soil-cycles" => "토양 순환과 작물 보호",
        "research:society:household-records" => "가구 기록과 영아 돌봄",
        "research:housing:room-assignment" => "방 배정과 가족 생활구획",
        "research:society:child-education" => "아동 교육과 도제 제도",
        "research:society:generation-management" => "세대 관리와 보호자 승계",
        "research:society:corpse-care" => "시신 관리와 장례 의식",
        "research:society:retirement" => "은퇴와 멘토 제도",
        "research:medical:gerontology" => "노인학과 생물학적 연령 계측",
        "research:medical:geriatric-medicine" => "노인의학과 만성 관리",
        "research:health:pathogen-observation" => "병원체 관찰과 면역 혈청학",
        "research:health:vaccination" => "예방접종과 유행병 통제",
        "research:genetics:hereditary-records" => "유전 기록과 형질 분석",
        "research:climate:regional-climatology" => "지역 기후학과 시각 항법",
        "research:industry:steam-power" => "증기 동력과 배전",
        "research:industry:powered-tools" => "공장 공학",
        "research:industry:breakers" => "차단기와 산업 정비",
        "research:industry:conveyor" => "컨베이어와 물류 포트",
        "research:industry:automatic-bills" => "자동 주문과 재고 감지",
        "research:industry:electric-smelting" => "전기 제련과 산업 냉각",
        "research:equipment:relic-appraisal" => "유물 부품 감정과 복원",
        "research:equipment:pressure-barrels" => "내압 화기와 방폭",
        "research:plumbing:basics" => "기초 배관과 펌프 급수",
        "research:plumbing:sewer" => "하수 처리와 수세 위생",
        "research:industry:junctions" => "분기·필터·우선순위 제어",
        "research:industry:lifts" => "승강·오버플로·고속 운송",
        "research:industry:storage" => "산업 저장과 변압",
        "research:industry:mana-power" => "마나 동력과 룬 전력망",
        "research:industry:automatic-sanitation" => "자동 위생과 산업 안전",
        "research:industry:line-balancing" => "라인 균형과 방어 보급",
        _ => fallback
    };

    private static string ResolveV21Description(
        string id,
        string fallback,
        int mergedCount)
    {
        return mergedCount <= 1
            ? fallback
            : $"{ResolveV21DisplayName(id, fallback)}에 필요한 이론, 제작 절차, 안전 기준과 운용 보상을 하나의 완결된 연구 패키지로 통합한다.";
    }

    private static IReadOnlyList<Spec> CreateExpansionSpecs()
    {
        return new[]
        {
            S("research:service:dining-operations", 7221, "식당 운영학", "배식과 좌석 회전, 위생 점검을 하나의 서비스 작업으로 표준화한다.", ResearchField.CommerceAndCraft, 184, prerequisites: new[] { ServiceRoomResearchIds.ServiceFlow, "research:cuisine:livestock" }),
            S("research:survival:field-rations", 7222, "야전 식량학", "원정 중에도 운반과 섭취가 쉬운 보존 식량 규격을 확립한다.", ResearchField.LifeAndSurvival, 132, prerequisites: new[] { "research:survival:preservation", "research:commerce:logistics" }),
            S("research:medical:construct-core-engineering", 7223, "구성체 핵 공학", "구성체 핵의 냉각과 동력 접속을 수술 가능한 공학 규격으로 정리한다.", ResearchField.SurgeryAndTransplant, 252, prerequisites: new[] { "research:medical:construct-core-maintenance", "research:industry:distribution", "research:arcane:records" }),
            S("research:equipment:weapon-patterns", 7224, "무기 형식학", "목재와 단조 부품을 반복 제작 가능한 무기 형식으로 규격화한다.", ResearchField.CommerceAndCraft, 92, prerequisites: new[] { "research:forestry:tools", "research:metallurgy:primitive" }),
            S("research:equipment:armor-tailoring", 7225, "방어구 재단", "직물과 가죽의 하중선을 맞추는 방어구 재단법을 확립한다.", ResearchField.Textiles, 132, prerequisites: new[] { "research:textile:tailoring", "research:textile:tanning" }),
            S("research:equipment:bowyery", 7226, "궁시 제작학", "처리 목재와 섬유의 탄성을 조율해 복합 활대를 제작한다.", ResearchField.CommerceAndCraft, 132, prerequisites: new[] { "research:forestry:treated", "research:textile:fiber" }),
            S("research:equipment:mechanical-projectiles", 7227, "기계식 투사", "권양 장치와 철제 걸쇠로 높은 장력의 투사 장비를 안전하게 작동시킨다.", ResearchField.DefenseAndTactics, 184, prerequisites: new[] { "research:equipment:bowyery", "research:metallurgy:iron" }),
            S("research:equipment:mail-weaving", 7228, "사슬 편조", "철 고리와 층상 충전재를 결속해 유연한 방호 외피를 만든다.", ResearchField.Metallurgy, 184, prerequisites: new[] { "research:metallurgy:iron", "research:textile:layered" }),
            S("research:equipment:articulated-plate", 7229, "관절식 판금", "제강 판재와 사슬 연결부를 관절 구조로 조립한다.", ResearchField.Metallurgy, 252, prerequisites: new[] { "research:metallurgy:steel", "research:metallurgy:advanced", "research:equipment:mail-weaving" }),
            S("research:equipment:black-powder", 7230, "흑색화약 배합", "숯과 증류 약재를 기록된 비율로 분쇄해 추진용 화약을 만든다.", ResearchField.IndustryAndAutomation, 420, prerequisites: new[] { "research:forestry:charcoal", "research:pharmacology:distillation", "research:arcane:records" }),
            S("research:equipment:ignition-mechanisms", 7231, "점화 기구학", "강철 격발부와 제도 규격으로 휴대 화기의 점화를 제어한다.", ResearchField.IndustryAndAutomation, 560, prerequisites: new[] { "research:equipment:black-powder", "research:metallurgy:steel", "research:equipment:engineering-drawing" }),
            S("research:equipment:ballistics", 7232, "탄도학", "점화 압력과 사격 진지의 관측 기록으로 탄도 편차를 계산한다.", ResearchField.DefenseAndTactics, 560, prerequisites: new[] { "research:equipment:ignition-mechanisms", "research:defense:ranged-positions" }),
            S("research:equipment:pressure-barrels", 7233, "내압 총열", "탄도 압력과 고급 단조 검사를 결합해 파열을 견디는 총열을 만든다.", ResearchField.IndustryAndAutomation, 720, prerequisites: new[] { "research:equipment:ballistics", "research:metallurgy:advanced" }),
            S("research:equipment:blast-protection", 7234, "폭발 작업 보호", "화약 폭압과 연기, 저온 작업 위험을 함께 차단하는 보호 장비를 만든다.", ResearchField.LifeAndSurvival, 720, prerequisites: new[] { "research:equipment:black-powder", "research:textile:layered", "research:environment:cold-work" }),
            S("research:equipment:relic-appraisal", 7235, "유물 부품 감정", "회수된 부품의 계보와 기능을 기록하고 위험한 결함을 식별한다.", ResearchField.RecordsAndArcane, 252, prerequisites: new[] { "research:commerce:logistics", "research:arcane:records" }),
            S("research:equipment:relic-restoration", 7236, "유물 부품 복원", "감정된 부품의 금속 골격과 직물 결속부를 원형에 가깝게 복원한다.", ResearchField.CommerceAndCraft, 420, prerequisites: new[] { "research:equipment:relic-appraisal", "research:metallurgy:advanced", "research:textile:tailoring" }),
            S("research:equipment:precision-fitting", 7237, "정밀 부품 장착", "복원 부품을 계측하고 전동 공구로 장비 슬롯에 정밀 장착한다.", ResearchField.IndustryAndAutomation, 720, prerequisites: new[] { "research:equipment:relic-restoration", "research:industry:powered-tools", "research:equipment:material-testing" }),
            S("research:equipment:rune-module-tuning", 7238, "룬 부품 조율", "정밀 장착 부품을 비전 공명과 룬 전력망에 동조시킨다.", ResearchField.IndustryAndAutomation, 1200, prerequisites: new[] { "research:equipment:precision-fitting", "research:arcane:resonance", "research:industry:rune-grid" }),
            S("research:equipment:modular-frames", 7239, "모듈식 장비 골격", "정밀 장착 치수와 강철 시험값을 공유하는 성장형 장비 골격을 제작한다.", ResearchField.IndustryAndAutomation, 960, prerequisites: new[] { "research:equipment:precision-fitting", "research:metallurgy:steel", "research:equipment:material-testing" }),
            S("research:equipment:lineage-binding", 7240, "장비 계보 결속", "룬 조율 장비의 사용 기록과 상징을 새 장비에 결속한다.", ResearchField.RecordsAndArcane, 1200, prerequisites: new[] { "research:equipment:rune-module-tuning", "research:arcane:records", "research:authority:prestige" }),
            S("research:equipment:engineering-drawing", 7241, "공학 제도", "기록 체계와 강철 규격을 재현 가능한 공학 도면으로 바꾼다.", ResearchField.IndustryAndAutomation, 420, prerequisites: new[] { "research:arcane:records", "research:metallurgy:steel" }),
            S("research:equipment:material-testing", 7242, "재료 시험학", "공학 도면의 허용 오차를 고급 단조 시편으로 검증한다.", ResearchField.IndustryAndAutomation, 560, prerequisites: new[] { "research:equipment:engineering-drawing", "research:metallurgy:advanced" }),
            S("research:equipment:prototype-engineering", 7243, "시제품 공학", "재료 시험 결과를 공장 배치 안에서 반복 가능한 시제품 공정으로 만든다.", ResearchField.IndustryAndAutomation, 720, prerequisites: new[] { "research:equipment:material-testing", "research:industry:factory-layout" }),
            S("research:equipment:industrial-metrology", 7244, "산업 계측학", "시제품 공정과 정밀 자동화의 편차를 공장 규모에서 계측한다.", ResearchField.IndustryAndAutomation, 960, prerequisites: new[] { "research:equipment:prototype-engineering", "research:industry:precision" }),
            S("research:equipment:field-maintenance", 7245, "야전 정비학", "방어 보급 절차와 재봉 수선법을 휴대 가능한 수리 규격으로 묶는다.", ResearchField.DefenseAndTactics, 184, prerequisites: new[] { "research:defense:supply", "research:textile:tailoring" }),
            S("research:equipment:standard-ammunition", 7246, "탄약 규격화", "화약과 물류, 전동 공구 규격을 종이 탄약통 생산에 통합한다.", ResearchField.IndustryAndAutomation, 720, prerequisites: new[] { "research:equipment:black-powder", "research:commerce:logistics", "research:industry:powered-tools" }),
            S("research:equipment:powered-armor", 7247, "동력 보조 갑주", "관절식 판금에 배전과 정밀 부품 구동계를 결합한다.", ResearchField.IndustryAndAutomation, 1200, prerequisites: new[] { "research:equipment:articulated-plate", "research:industry:distribution", "research:equipment:precision-fitting" }),

            S("research:life:seasonal-calendar", 7248, "계절 역법", "절대 달력의 계절 경계와 생업 주기를 기록한다.", ResearchField.RecordsAndArcane, 184, prerequisites: new[] { "research:arcane:records", "research:agriculture:field" }),
            S("research:agriculture:phenology", 7249, "생물계절학", "계절 변화와 작물 생장 단계를 대응시킨다.", ResearchField.Agriculture, 252, prerequisites: new[] { "research:life:seasonal-calendar", "research:agriculture:field" }),
            S("research:climate:weather-observation", 7250, "기상 관측", "경계 근무 관측법으로 기온과 기상 전선을 측정한다.", ResearchField.RecordsAndArcane, 336, prerequisites: new[] { "research:life:seasonal-calendar", "research:defense:watch" }),
            S("research:agriculture:soil-cycles", 7251, "토양 순환", "생물계절 기록과 퇴비 순환을 토양 관리 기준으로 만든다.", ResearchField.Agriculture, 252, prerequisites: new[] { "research:agriculture:phenology", "research:agriculture:compost" }),
            S("research:survival:seasonal-storage", 7252, "계절 저장", "계절력에 맞춰 저장품의 입고와 소비 시기를 관리한다.", ResearchField.LifeAndSurvival, 336, prerequisites: new[] { "research:life:seasonal-calendar", "research:survival:preservation" }),
            S("research:agriculture:greenhouse-horticulture", 7253, "온실 원예", "관측 기후와 실내 재배, 급수 설비를 폐쇄형 온실에 통합한다.", ResearchField.Agriculture, 560, prerequisites: new[] { "research:climate:weather-observation", "research:agriculture:indoor", "research:plumbing:pumped-water" }),
            S("research:husbandry:seasonal-breeding", 7254, "계절 번식", "생물계절 기록을 번식 일정과 회복 주기에 반영한다.", ResearchField.Husbandry, 420, prerequisites: new[] { "research:agriculture:phenology", "research:husbandry:breeding" }),
            S("research:climate:environment-control", 7255, "기후 제어", "관측·온실·냉각·단열 기술을 능동 환경 제어로 결합한다.", ResearchField.IndustryAndAutomation, 960, prerequisites: new[] { "research:climate:weather-observation", "research:agriculture:greenhouse-horticulture", "research:industry:industrial-cooling", "research:environment:rune-insulation" }),

            S("research:society:household-records", 7256, "가구 기록", "거주자와 가족 관계를 가구 단위 기록으로 관리한다.", ResearchField.AuthorityAndHousing, 184, prerequisites: new[] { "research:arcane:records", "research:authority:quarters" }),
            S("research:life:infant-care", 7257, "영아 돌봄", "가구 기록과 의료 회복 지식을 영아 보육 절차로 만든다.", ResearchField.LifeAndSurvival, 252, prerequisites: new[] { "research:society:household-records", "research:survival:medical" }),
            S("research:medical:reproductive-medicine", 7258, "생식 의학", "영아 돌봄, 해부학과 마취를 산과 진료에 적용한다.", ResearchField.SurgeryAndTransplant, 560, prerequisites: new[] { "research:life:infant-care", "research:medical:anatomy", "research:pharmacology:anesthesia" }),
            S("research:society:child-education", 7259, "아동 교육", "보육과 기록 체계를 안전한 기초 교육 과정으로 만든다.", ResearchField.AuthorityAndHousing, 336, prerequisites: new[] { "research:life:infant-care", "research:arcane:records" }),
            S("research:society:apprenticeship", 7260, "도제 제도", "청소년 교육과 공학 도면을 감독형 실습으로 확장한다.", ResearchField.CommerceAndCraft, 560, prerequisites: new[] { "research:society:child-education", "research:equipment:engineering-drawing" }),
            S("research:society:generation-management", 7261, "세대 관리", "도제·가구·집무 기록으로 세대 승계를 관리한다.", ResearchField.AuthorityAndHousing, 960, prerequisites: new[] { "research:society:apprenticeship", "research:society:household-records", "research:authority:office" }),

            S("research:medical:gerontology", 7262, "노인학", "해부학과 가구 기록을 이용해 노화 변화를 장기간 관찰한다.", ResearchField.SurgeryAndTransplant, 420, prerequisites: new[] { "research:medical:anatomy", "research:society:household-records" }),
            S("research:medical:biological-age-measurement", 7263, "생물학적 연령 계측", "노인학, 고급 약학과 재료 계측으로 생물학적 연령을 측정한다.", ResearchField.SurgeryAndTransplant, 720, prerequisites: new[] { "research:medical:gerontology", "research:pharmacology:advanced", "research:equipment:material-testing" }),
            S("research:medical:geriatric-medicine", 7264, "노인의학", "노인학과 의료 회복을 노화 질환 완화 치료로 정립한다.", ResearchField.SurgeryAndTransplant, 560, prerequisites: new[] { "research:medical:gerontology", "research:survival:medical" }),
            S("research:medical:chronic-care", 7265, "만성 관리", "노인의학과 위생 설비를 지속적인 만성 질환 관리로 결합한다.", ResearchField.SurgeryAndTransplant, 720, prerequisites: new[] { "research:medical:geriatric-medicine", "research:plumbing:flush-sanitation" }),
            S("research:medical:regenerative-culture", 7266, "재생 배양", "생물학적 연령 계측, 장기 보존과 균사 이식으로 재생 조직을 배양한다.", ResearchField.SurgeryAndTransplant, 4800, prerequisites: new[] { "research:medical:biological-age-measurement", "research:medical:organ-preservation", "research:medical:mycelial-grafting" }),
            S("research:medical:organ-regeneration", 7267, "장기 재생", "재생 배양과 수술, 이종 이식으로 손상 장기를 교체한다.", ResearchField.SurgeryAndTransplant, 7200, prerequisites: new[] { "research:medical:regenerative-culture", "research:medical:surgery", "research:medical:xenotransplant" }),
            S("research:medical:blood-rejuvenation", 7268, "혈액 회춘", "장기 재생과 혈술, 고급 약학을 회춘 수혈에 적용한다.", ResearchField.SurgeryAndTransplant, 4800, prerequisites: new[] { "research:medical:organ-regeneration", "research:medical:bloodcraft-augmentation", "research:pharmacology:advanced" }),
            S("research:medical:rune-hibernation", 7269, "룬 동면", "생물학적 연령 계측과 공명, 룬 전력·냉각을 동면 유지에 통합한다.", ResearchField.RecordsAndArcane, 4800, prerequisites: new[] { "research:medical:biological-age-measurement", "research:arcane:resonance", "research:industry:rune-grid", "research:industry:industrial-cooling" }),
            S("research:medical:whole-body-regeneration", 7270, "전신 재생", "장기 재생, 혈액 회춘, 룬 동면과 점액 생체공학을 전신 치료로 통합한다.", ResearchField.SurgeryAndTransplant, 12000, prerequisites: new[] { "research:medical:organ-regeneration", "research:medical:blood-rejuvenation", "research:medical:rune-hibernation", "research:medical:slime-bioengineering" }),
            S("research:medical:temporal-stasis", 7271, "시간 고정", "전신 재생과 시각 항법, 교차계통 안정화, 룬 조율로 생물학적 시간을 고정한다.", ResearchField.RecordsAndArcane, 12000, prerequisites: new[] { "research:medical:whole-body-regeneration", "research:climate:chronometric-navigation", "research:genetics:cross-lineage-stabilization", "research:equipment:rune-module-tuning" }),

            S("research:health:pathogen-observation", 7272, "병원체 관찰", "해부학과 위생 지식으로 병원체와 증상을 구분한다.", ResearchField.SurgeryAndTransplant, 420, prerequisites: new[] { "research:medical:anatomy", "research:survival:sanitation" }),
            S("research:health:isolation-medicine", 7273, "격리 의학", "병원체 관찰, 의료 접수와 위생 설비를 격리 병동 운영으로 만든다.", ResearchField.SurgeryAndTransplant, 560, prerequisites: new[] { "research:health:pathogen-observation", ServiceRoomResearchIds.MedicalReception, "research:plumbing:flush-sanitation" }),
            S("research:health:immunoserology", 7274, "면역 혈청학", "병원체 관찰과 고급 약학, 연령 계측으로 항체 반응을 분석한다.", ResearchField.SurgeryAndTransplant, 720, prerequisites: new[] { "research:health:pathogen-observation", "research:pharmacology:advanced", "research:medical:biological-age-measurement" }),
            S("research:health:vaccination", 7275, "예방접종", "혈청학, 마취제 조제와 자동 위생 공정으로 백신을 생산한다.", ResearchField.SurgeryAndTransplant, 1200, prerequisites: new[] { "research:health:immunoserology", "research:pharmacology:anesthesia", "research:industry:automatic-sanitation" }),
            S("research:health:epidemic-control", 7276, "유행병 통제", "격리와 예방접종, 물류·기록을 유행 감시 체계로 통합한다.", ResearchField.AuthorityAndHousing, 960, prerequisites: new[] { "research:health:isolation-medicine", "research:health:vaccination", "research:commerce:logistics", "research:arcane:records" }),

            S("research:genetics:hereditary-records", 7277, "유전 기록", "가구 기록, 해부학과 선별 사육으로 유전 형질을 기록한다.", ResearchField.RecordsAndArcane, 560, prerequisites: new[] { "research:society:household-records", "research:medical:anatomy", "research:husbandry:selective" }),
            S("research:genetics:trait-analysis", 7278, "형질 분석", "유전 기록과 연령·재료 계측으로 발현 형질을 분석한다.", ResearchField.RecordsAndArcane, 960, prerequisites: new[] { "research:genetics:hereditary-records", "research:medical:biological-age-measurement", "research:equipment:material-testing" }),
            S("research:genetics:controlled-heredity", 7279, "통제 유전", "형질 분석과 생식 의학, 선별 사육을 유전 상담에 적용한다.", ResearchField.SurgeryAndTransplant, 1800, prerequisites: new[] { "research:genetics:trait-analysis", "research:medical:reproductive-medicine", "research:husbandry:selective" }),
            S("research:genetics:cross-lineage-stabilization", 7280, "교차계통 안정화", "통제 유전, 재생 배양, 이종 이식과 비전 공명으로 다른 생식 계통을 안정화한다.", ResearchField.SurgeryAndTransplant, 9600, prerequisites: new[] { "research:genetics:controlled-heredity", "research:medical:regenerative-culture", "research:medical:xenotransplant", "research:arcane:resonance" }),

            S("research:housing:room-assignment", 7281, "방 배정", "숙소와 가구 기록을 개인 침대·방 배정으로 연결한다.", ResearchField.AuthorityAndHousing, 184, prerequisites: new[] { "research:authority:quarters", "research:society:household-records" }),
            S("research:housing:family-quarters", 7282, "가족 생활구획", "방 배정과 물류 구획을 가족 생활 공간으로 세분한다.", ResearchField.AuthorityAndHousing, 336, prerequisites: new[] { "research:housing:room-assignment", "research:commerce:logistics" }),
            S("research:housing:guardian-succession", 7283, "보호자 승계", "가족 구획과 집무·세대 기록으로 보호자 승계를 관리한다.", ResearchField.AuthorityAndHousing, 560, prerequisites: new[] { "research:housing:family-quarters", "research:authority:office", "research:society:generation-management" }),
            S("research:medical:trauma-medicine", 7284, "트라우마 의학", "의료 접수와 고급 약학, 가구 기록으로 심리 외상을 상담한다.", ResearchField.SurgeryAndTransplant, 560, prerequisites: new[] { ServiceRoomResearchIds.MedicalReception, "research:pharmacology:advanced", "research:society:household-records" }),
            S("research:society:corpse-care", 7285, "시신 관리", "위생, 가구 기록과 보존 기술로 시신을 안전하게 처리한다.", ResearchField.LifeAndSurvival, 252, prerequisites: new[] { "research:survival:sanitation", "research:society:household-records", "research:survival:preservation" }),
            S("research:society:funeral-rites", 7286, "장례 의식", "시신 관리와 의식 장식을 종족별 장례 절차로 정립한다.", ResearchField.AuthorityAndHousing, 420, prerequisites: new[] { "research:society:corpse-care", "research:authority:ritual" }),

            S("research:climate:regional-climatology", 7287, "지역 기후학", "기상 관측과 기후 제어, 기록 체계로 지역 기후대를 분석한다.", ResearchField.RecordsAndArcane, 1200, prerequisites: new[] { "research:climate:weather-observation", "research:climate:environment-control", "research:arcane:records" }),
            S("research:climate:chronometric-navigation", 7288, "시각 항법", "지역 기후학과 산업 계측, 비전 공명으로 원정지 시차를 계산한다.", ResearchField.RecordsAndArcane, 7200, prerequisites: new[] { "research:climate:regional-climatology", "research:equipment:industrial-metrology", "research:arcane:resonance" }),

            S("research:agriculture:seed-selection", 7289, "종자 선별", "생물계절학과 경작 지식을 물리 종자 로트 선별에 적용한다.", ResearchField.Agriculture, 336, prerequisites: new[] { "research:agriculture:phenology", "research:agriculture:field" }),
            S("research:agriculture:pest-control", 7290, "해충 방제", "종자 선별, 약초학과 위생 지식으로 방제제를 조제한다.", ResearchField.Agriculture, 560, prerequisites: new[] { "research:agriculture:seed-selection", "research:pharmacology:herbalism", "research:survival:sanitation" }),
            S("research:agriculture:crop-pathology", 7291, "작물 병리학", "해충 방제, 병원체 관찰과 균목 재배 지식으로 작물병을 진단한다.", ResearchField.Agriculture, 720, prerequisites: new[] { "research:agriculture:pest-control", "research:health:pathogen-observation", "research:forestry:fungal" }),
            S("research:agriculture:cultivar-breeding", 7292, "품종 개량", "작물 병리, 형질 분석과 선별 사육을 육종 온실에 적용한다.", ResearchField.Agriculture, 1200, prerequisites: new[] { "research:agriculture:crop-pathology", "research:genetics:trait-analysis", "research:husbandry:selective" }),

            S("research:society:career-records", 7293, "경력 기록", "가구와 집무 기록으로 역할·등급·직위 변화를 보존한다.", ResearchField.AuthorityAndHousing, 252, prerequisites: new[] { "research:society:household-records", "research:authority:office" }),
            S("research:society:retirement", 7294, "은퇴 제도", "경력 기록과 노인학을 안전 작업 중심의 은퇴 정책으로 만든다.", ResearchField.AuthorityAndHousing, 420, prerequisites: new[] { "research:society:career-records", "research:medical:gerontology" }),
            S("research:society:mentor-academy", 7295, "멘토 학원", "도제·경력·세대 관리 기록을 체계적인 멘토링으로 통합한다.", ResearchField.AuthorityAndHousing, 960, prerequisites: new[] { "research:society:apprenticeship", "research:society:career-records", "research:society:generation-management" })
        };
    }

    private static Spec ApplyApprovedWorkBand(Spec spec)
    {
        if (spec.NumericId >= 7221)
        {
            return spec;
        }

        if (spec.Field == ResearchField.IndustryAndAutomation)
        {
            spec.Work = spec.Id switch
            {
                "research:industry:steam-power" => 420f,
                "research:industry:distribution" or
                "research:industry:powered-tools" or
                "research:industry:factory-layout" or
                "research:industry:breakers" => 560f,
                "research:industry:conveyor" or
                "research:industry:assisted-processing" or
                "research:industry:electric-smelting" or
                "research:industry:industrial-cooling" or
                "research:industry:electric-lighting" => 720f,
                "research:industry:precision" or
                "research:industry:line-balancing" or
                "research:industry:automatic-bills" or
                "research:industry:stock-sensors" => 960f,
                "research:industry:rune-automation" or
                "research:industry:dark-foundry" => 1200f,
                _ => spec.Work <= 160f ? 560f
                    : spec.Work <= 240f ? 720f
                    : spec.Work <= 320f ? 960f
                    : 1200f
            };
            return spec;
        }

        float[] bands = { 36f, 60f, 92f, 132f, 184f, 252f, 336f };
        spec.Work = bands.OrderBy(value => Mathf.Abs(value - spec.Work)).First();
        return spec;
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
