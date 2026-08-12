#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RuntimeBuildingArchetypeAssetBuilder
{
    private const string Folder = "Assets/Resources/SO/Buildings/RuntimeArchetypes";

    [MenuItem("Tools/DungeonStory/Content/Migrate Runtime Building Archetypes")]
    public static void Migrate()
    {
        EnsureFolders();
        Configure(
            GetOrCreate("WorldResourceNode"),
            RuntimeBuildingArchetypeIds.WorldResourceNode,
            "외부 자원",
            GridLayer.FloorOverlay,
            BuildingCategory.Resource,
            new[]
            {
                BuiltInWorkTypeIds.Gather,
                BuiltInWorkTypeIds.Logging,
                BuiltInWorkTypeIds.Quarry
            });
        Configure(
            GetOrCreate("WorldFilthWorkTarget"),
            RuntimeBuildingArchetypeIds.WorldFilthWorkTarget,
            "오염",
            GridLayer.Filth,
            BuildingCategory.Special,
            new[] { BuiltInWorkTypeIds.Clean },
            addWorkAnchor: true);

        foreach (ExteriorZoneType zoneType in Enum.GetValues(typeof(ExteriorZoneType)))
        {
            foreach (GridLayer layer in ExteriorLayers())
            {
                BuildingSO definition = GetOrCreate($"Exterior_{zoneType}_{layer}");
                Configure(
                    definition,
                    RuntimeBuildingArchetypeIds.ExteriorZone(zoneType, layer),
                    DisplayName(zoneType),
                    layer,
                    BuildingCategory.Special,
                    SupportedWork(zoneType),
                    addWorkAnchor: true);
                AddExteriorAbilities(definition, zoneType);
                definition.AbilityModules.EnsureStableIds();
                definition.ValidateAbilitiesOrThrow();
                EditorUtility.SetDirty(definition);
            }
        }

        AssetDatabase.SaveAssets();
        GameContentCatalogAssetBuilder.Rebuild();
        Debug.Log("Authored immutable runtime building archetypes and rebuilt the root content catalog.");
    }

    private static void Configure(
        BuildingSO definition,
        int id,
        string displayName,
        GridLayer layer,
        BuildingCategory category,
        IEnumerable<WorkTypeId> supportedWork,
        bool addWorkAnchor = false)
    {
        definition.id = id;
        definition.objectName = displayName;
        definition.width = 1;
        definition.height = 1;
        definition.layer = layer;
        definition.category = category;
        definition.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
        definition.unlocked = false;
        definition.ReplaceAbilities(new BuildingAbilityCollection());
        definition.Facility = new FacilityData
        {
            roles = FacilityRole.None,
            capacity = 0,
            useDuration = 1f,
            requiredWorkers = 1,
            disabledWhenDamaged = false
        };
        definition.Facility.SetSupportedWorkTypeIds(
            supportedWork ?? Array.Empty<WorkTypeId>());
        BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = 18f,
            repairWorkRequired = 10f,
            cleanWorkRequired = 6.25f,
            researchWorkRequired = 6f,
            operateWorkRequired = 10f
        };
        workAmount.SetConstructionProjectScale(ProjectScale.SmallFacility);
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition(
                layer is GridLayer.WallFixture or GridLayer.CeilingFixture
                    ? "material:lumber"
                    : "material:stone-block",
                1)
        });
        definition.AbilityModules.Add(workAmount);
        definition.facilityAnchors = new FacilityAnchorData();
        if (addWorkAnchor)
        {
            definition.FacilityAnchors.Add(FacilityAnchorPurposeIds.Work, Vector2.zero);
        }

        definition.AbilityModules.EnsureStableIds();
        definition.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(definition);
    }

    private static void AddExteriorAbilities(BuildingSO definition, ExteriorZoneType zoneType)
    {
        switch (zoneType)
        {
            case ExteriorZoneType.DropZone:
                definition.AbilityModules.Add(new BuildingExteriorMaintenanceAbility
                {
                    cleanlinessGain = 40f,
                    damageReduction = 35f
                });
                break;
            case ExteriorZoneType.ReceptionPoint:
                definition.AbilityModules.Add(new BuildingReceptionAbility());
                definition.AbilityModules.Add(new BuildingExteriorMaintenanceAbility());
                break;
            case ExteriorZoneType.GuardPost:
                definition.AbilityModules.Add(new BuildingPatrolPostAbility());
                definition.AbilityModules.Add(new BuildingExteriorMaintenanceAbility());
                break;
            case ExteriorZoneType.PatrolPoint:
                definition.AbilityModules.Add(new BuildingPatrolPostAbility
                {
                    patrolReadinessGain = 25f
                });
                break;
            case ExteriorZoneType.OutdoorRestSpot:
                definition.AbilityModules.Add(new BuildingOutdoorRestAbility());
                definition.AbilityModules.Add(new BuildingExteriorMaintenanceAbility());
                break;
            case ExteriorZoneType.IncidentPoint:
                definition.AbilityModules.Add(new BuildingReceptionAbility
                {
                    readinessGain = 45f
                });
                definition.AbilityModules.Add(new BuildingPatrolPostAbility
                {
                    patrolReadinessGain = 45f
                });
                break;
        }
    }

    private static IReadOnlyList<WorkTypeId> SupportedWork(ExteriorZoneType zoneType)
    {
        FacilityWorkType legacy = zoneType switch
        {
            ExteriorZoneType.DropZone => FacilityWorkType.Clean | FacilityWorkType.Repair,
            ExteriorZoneType.ReceptionPoint => FacilityWorkType.Reception | FacilityWorkType.Clean,
            ExteriorZoneType.GuardPost => FacilityWorkType.Guard | FacilityWorkType.Repair,
            ExteriorZoneType.PatrolPoint => FacilityWorkType.Guard,
            ExteriorZoneType.OutdoorRestSpot => FacilityWorkType.Rest | FacilityWorkType.Clean,
            ExteriorZoneType.IncidentPoint => FacilityWorkType.Reception | FacilityWorkType.Guard,
            _ => FacilityWorkType.None
        };
        return FacilityWorkTypeMap.Enumerate(legacy)
            .Select(definition => definition.WorkTypeId)
            .ToArray();
    }

    private static IEnumerable<GridLayer> ExteriorLayers()
    {
        yield return GridLayer.FloorOverlay;
        yield return GridLayer.WallFixture;
        yield return GridLayer.CeilingFixture;
        yield return GridLayer.Building;
        yield return GridLayer.Hallway;
    }

    private static string DisplayName(ExteriorZoneType zoneType) => zoneType switch
    {
        ExteriorZoneType.Entrance => "입구",
        ExteriorZoneType.DropZone => "하차장",
        ExteriorZoneType.ReceptionPoint => "응대 지점",
        ExteriorZoneType.GuardPost => "경비 초소",
        ExteriorZoneType.PatrolPoint => "순찰 지점",
        ExteriorZoneType.OutdoorRestSpot => "외부 휴식처",
        ExteriorZoneType.ExpeditionStaging => "출정 집결지",
        ExteriorZoneType.IncidentPoint => "외부 사건 지점",
        _ => "외부 구역"
    };

    private static BuildingSO GetOrCreate(string name)
    {
        string path = $"{Folder}/{name}.asset";
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        if (definition != null)
        {
            return definition;
        }

        definition = ScriptableObject.CreateInstance<BuildingSO>();
        AssetDatabase.CreateAsset(definition, path);
        return definition;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources/SO", "Buildings");
        EnsureFolder("Assets/Resources/SO/Buildings", "RuntimeArchetypes");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
