using System;
using UnityEditor;
using UnityEngine;

public static class CaptivityFacilityAssetBuilder
{
    private const string OutputFolder =
        "Assets/Resources/SO/Building/Captivity";

    [MenuItem("DungeonStory/Content/Build Captivity And Circus Facilities")]
    public static void BuildFromMenu()
    {
        BuildAll();
    }

    public static void BuildAll()
    {
        EnsureFolder("Assets/Resources/SO/Building");
        EnsureFolder(OutputFolder);

        Build(
            $"{OutputFolder}/CP01_\uAC10\uBC29\uAD6C\uC18D\uB300.asset",
            "Assets/Resources/SO/Building/Modular/R01_\uAC04\uC774\uCE68\uB300.asset",
            1200,
            "\uAC10\uBC29 \uAD6C\uC18D\uB300",
            "CP01",
            FacilityRole.None,
            new[] { BuiltInWorkTypeIds.Warden },
            new BuildingCaptiveHousingAbility
            {
                capacity = 1,
                restraintSlots = 1,
                baseSecurity = 45f
            });
        Build(
            $"{OutputFolder}/CS01_\uC911\uC559\uBB34\uB300.asset",
            "Assets/Resources/SO/Building/Modular/T04_\uB300\uB828\uB9E4\uD2B8.asset",
            1201,
            "\uC911\uC559 \uBB34\uB300",
            "CS01",
            FacilityRole.Entertainment,
            new[] { BuiltInWorkTypeIds.Perform },
            new BuildingCircusStageAbility
            {
                performerCapacity = 2,
                baseTicketPrice = 12,
                preparationWork = 16f,
                showDurationSeconds = 45f
            },
            requiresRoom: true);
        Build(
            $"{OutputFolder}/CS02_\uAD00\uB78C\uC11D.asset",
            "Assets/Resources/SO/Building/Modular/D09_\uAE34\uBCA4\uCE58.asset",
            1202,
            "\uC11C\uCEE4\uC2A4 \uAD00\uB78C\uC11D",
            "CS02",
            FacilityRole.Entertainment,
            Array.Empty<WorkTypeId>(),
            new BuildingAudienceSeatingAbility
            {
                capacity = 2,
                sightQuality = 0.8f
            },
            requiresRoom: true,
            addLegacySeating: true);
        Build(
            $"{OutputFolder}/CB01_\uC57C\uC218\uC6B0\uB9AC.asset",
            "Assets/Resources/SO/Building/Modular/H02_\uD654\uC7A5\uC2E4\uCE78\uB9C9\uC774.asset",
            1203,
            "\uC57C\uC218 \uC6B0\uB9AC",
            "CB01",
            FacilityRole.Entertainment,
            new[] { BuiltInWorkTypeIds.AnimalCare },
            new BuildingBeastPenAbility
            {
                capacity = 2,
                baseSecurity = 55f,
                dailyFood = 2f,
                dailyWater = 2f,
                tamingWork = 18f,
                productCollectionWork = 8f
            },
            requiresRoom: true);
        Build(
            $"{OutputFolder}/CT01_매표소.asset",
            "Assets/Resources/SO/Building/Modular/S01_판매카운터.asset",
            1204,
            "서커스 매표소",
            "CT01",
            FacilityRole.Entertainment,
            Array.Empty<WorkTypeId>(),
            new BuildingCircusTicketBoothAbility
            {
                revenueMultiplier = 1.15f,
                flatRevenuePerAudience = 1
            },
            requiresRoom: true);
        Build(
            $"{OutputFolder}/CG01_도박창구.asset",
            "Assets/Resources/SO/Building/Modular/G04_전술지도탁자.asset",
            1205,
            "서커스 도박 창구",
            "CG01",
            FacilityRole.Entertainment,
            Array.Empty<WorkTypeId>(),
            new BuildingCircusGamblingAbility
            {
                revenuePerAudience = 3,
                satisfactionVariance = 5f
            },
            requiresRoom: true);
        Build(
            $"{OutputFolder}/CA01_진행자단상.asset",
            "Assets/Resources/SO/Building/Modular/R08_지휘의자.asset",
            1206,
            "진행자 단상",
            "CA01",
            FacilityRole.Entertainment,
            Array.Empty<WorkTypeId>(),
            new BuildingCircusAnnouncerAbility
            {
                satisfactionBonus = 6f,
                preparationWorkMultiplier = 0.9f
            },
            requiresRoom: true);
        Build(
            $"{OutputFolder}/CH01_위험장치.asset",
            "Assets/Resources/SO/Building/Modular/T02_사격과녁.asset",
            1207,
            "공연 위험 장치",
            "CH01",
            FacilityRole.Entertainment,
            Array.Empty<WorkTypeId>(),
            new BuildingCircusHazardAbility
            {
                accidentRiskBonus = 0.08f,
                satisfactionBonus = 8f
            },
            requiresRoom: true);
        Build(
            $"{OutputFolder}/CM01_치료구역.asset",
            "Assets/Resources/SO/Building/Modular/R01_간이침대.asset",
            1208,
            "공연 치료 구역",
            "CM01",
            FacilityRole.Entertainment,
            Array.Empty<WorkTypeId>(),
            new BuildingCircusTreatmentZoneAbility
            {
                accidentDamageMultiplier = 0.65f
            },
            requiresRoom: true);
        Build(
            $"{OutputFolder}/CP02_공개형벌장치.asset",
            "Assets/Resources/SO/Building/Modular/T01_훈련허수아비.asset",
            1209,
            "공개 형벌 장치",
            "CP02",
            FacilityRole.Entertainment,
            Array.Empty<WorkTypeId>(),
            new BuildingPublicPunishmentAbility
            {
                cruelSatisfactionBonus = 8f,
                filthMultiplier = 1.35f,
                witnessMoodPenalty = 5f
            },
            requiresRoom: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Captivity and circus facility assets built.");
    }

    private static void Build(
        string outputPath,
        string sourcePath,
        int id,
        string displayName,
        string code,
        FacilityRole roles,
        WorkTypeId[] workTypes,
        BuildingAbility domainAbility,
        bool requiresRoom = false,
        bool addLegacySeating = false)
    {
        BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(sourcePath);
        if (source == null)
        {
            throw new InvalidOperationException(
                $"Captivity facility source asset is missing: {sourcePath}");
        }

        BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(outputPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<BuildingSO>();
            AssetDatabase.CreateAsset(asset, outputPath);
        }

        asset.id = id;
        asset.objectName = displayName;
        asset.sprite = source.sprite;
        asset.icon = source.icon;
        asset.width = source.width;
        asset.height = source.height;
        asset.layer = source.layer;
        asset.category = source.category;
        asset.horizontalDraggable = false;
        asset.verticalDraggable = false;
        asset.type = typeof(Facility);
        asset.tiles = null;
        asset.movementAnchorOffset = Vector2.zero;
        asset.movementTravelTime = 2f;
        asset.unlocked = true;

        FacilityData facility = new FacilityData
        {
            roles = roles,
            capacity = Mathf.Max(
                1,
                domainAbility is BuildingAudienceSeatingAbility seating
                    ? seating.capacity
                    : 1),
            useDuration = 1f,
            requiredWorkers = workTypes != null && workTypes.Length > 0 ? 1 : 0
        };
        facility.SetSupportedWorkTypeIds(workTypes ?? Array.Empty<WorkTypeId>());

        BuildingAbilityCollection abilities = new BuildingAbilityCollection();
        abilities.Add(new BuildingFacilityPartAbility { code = code });
        abilities.Add(new BuildingEconomyAbility
        {
            constructionCost = id == 1201 ? 180 : 90,
            maintenance = id == 1201 ? 3 : 1,
            unlockPhase = 1,
            demolitionRefundRate = 0.5f
        });
        abilities.Add(new BuildingFacilityAbility { settings = facility });
        if (requiresRoom)
        {
            abilities.Add(new BuildingRoomRequirementAbility());
        }

        if (addLegacySeating
            && domainAbility is BuildingAudienceSeatingAbility audience)
        {
            abilities.Add(new BuildingSeatingAbility
            {
                capacity = audience.capacity
            });
        }

        abilities.Add(domainAbility);
        abilities.EnsureStableIds();
        asset.ReplaceAbilities(abilities);
        asset.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        int split = folder.LastIndexOf('/');
        string parent = folder.Substring(0, split);
        string name = folder.Substring(split + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
