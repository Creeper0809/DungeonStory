#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ServiceRoomContentAssetBuilder
{
    public const string Root = "Assets/Resources/SO/Building/ServiceRooms";
    public const string ProcessRoot =
        "Assets/Resources/SO/ServiceRooms/Processes";

    private sealed class SupportSpec
    {
        public int Id;
        public string Code;
        public string Name;
        public string SourcePath;
        public string[] Features;
        public string[] HubTags;
        public ServiceSupportModifierType Modifier;
        public int Capacity;
        public bool RequiresPower;
        public float WorkSpeed = 1f;
        public float Satisfaction;
        public int Revenue;
    }

    [MenuItem("DungeonStory/Content/Build Service Room Content")]
    public static void BuildFromMenu()
    {
        EnsureAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Service room facilities and Direct hubs updated.");
    }

    public static void EnsureAssets()
    {
        EnsureFolder(Root);
        EnsureFolder(ProcessRoot);

        PatchDirectHub("D04", ServiceCategory.Dining, "service:dining",
            "service:dining:meal", 6, 42f,
            new[] { "service:reception", "service:queue" },
            new[] { "service:heated-serving", "service:auto-order" });
        PatchDirectHub("S01", ServiceCategory.Retail, "service:retail",
            "service:retail:sale", 4, 40f,
            new[] { "service:staffed-checkout", "service:display" },
            new[] { "service:auto-checkout" });

        foreach (string code in new[] { "R01", "R02", "R03" })
        {
            PatchDirectHub(code, ServiceCategory.Lodging, "service:lodging",
                "service:lodging:rest", 8, 46f,
                new[] { "service:lodging-reception", "service:room-cleanup" },
                new[] { "service:auto-room-assignment" });
        }

        foreach (string code in new[] { "H03", "H04" })
        {
            PatchDirectHub(code, ServiceCategory.Bathing, "service:bathing",
                "service:bathing:wash", 5, 44f,
                new[] { "service:bath-reception", "service:bath-hygiene" },
                new[] { "service:auto-water-control" });
        }

        PatchDirectHub("M01", ServiceCategory.Medical, "service:medical",
            "service:medical:treat", 10, 48f,
            new[] { "service:medical-triage", "service:medical-call" },
            new[] { "service:queue" });

        PatchExistingSupport("D07", "service-seat-basic",
            new[] { "service:seat" }, new[] { "service:dining" },
            ServiceSupportModifierType.Capacity, 1);
        PatchExistingSupport("D08", "service-seat-comfort",
            new[] { "service:seat", "service:seat:vampire" },
            new[] { "service:dining" },
            ServiceSupportModifierType.Satisfaction, 1, 5f);
        PatchExistingSupport("D09", "service-seat-bench",
            new[] { "service:seat", "service:seat:orc" },
            new[] { "service:dining" },
            ServiceSupportModifierType.Capacity, 2);
        PatchExistingSupport("S02", "service-retail-display",
            new[] { "service:display" }, new[] { "service:retail" },
            ServiceSupportModifierType.Revenue, 0, revenue: 2);

        foreach (SupportSpec spec in CreateSupportSpecs())
        {
            EnsureSupportAsset(spec);
        }

        EnsureProcessAssets();
    }

    public static IReadOnlyDictionary<string, int[]> GetResearchUnlockIds() =>
        new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            [ServiceRoomResearchIds.ServiceFlow] = new[] { 1703, 1712, 1713 },
            [ServiceRoomResearchIds.HospitalityOperations] = new[] { 1700, 1709 },
            [ServiceRoomResearchIds.BathBusiness] = new[] { 1701, 1710 },
            [ServiceRoomResearchIds.MedicalReception] = new[] { 1702, 1711 },
            [ServiceRoomResearchIds.ServiceAutomation] =
                new[] { 1704, 1705, 1706, 1707, 1708, 1714, 1715 }
        };

    private static void PatchDirectHub(
        string code,
        ServiceCategory category,
        string hubTag,
        string processId,
        int price,
        float satisfaction,
        string[] managedFeatures,
        string[] automatedFeatures)
    {
        BuildingSO building = category == ServiceCategory.Medical
            ? AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Medical/M01_응급처치대.asset")
            : FindBuilding(code);
        if (building == null)
        {
            throw new InvalidOperationException(
                $"Direct service hub '{code}' was not found.");
        }

        building.AbilityModules.Remove<BuildingServiceHubAbility>();
        building.AbilityModules.Add(new BuildingServiceHubAbility
        {
            serviceCategory = category,
            serviceHubTag = hubTag,
            supportedProcessIds = new[] { processId },
            baseCapacity = 1,
            allowedModes = ServiceOperationModeMask.All,
            allowInternalStaffDirectUse = true,
            paymentPolicy = ServicePaymentPolicy.InternalStaffFree,
            directPrice = price,
            directSatisfaction = satisfaction,
            managedRequiredFeatureTags = managedFeatures ?? Array.Empty<string>(),
            automatedRequiredFeatureTags = automatedFeatures ?? Array.Empty<string>()
        });
        building.unlocked = true;
        FinalizeBuilding(building);
    }

    private static void PatchExistingSupport(
        string code,
        string supportId,
        string[] features,
        string[] hubs,
        ServiceSupportModifierType modifier,
        int capacity,
        float satisfaction = 0f,
        int revenue = 0)
    {
        BuildingSO building = FindBuilding(code);
        if (building == null)
        {
            throw new InvalidOperationException(
                $"Service support '{code}' was not found.");
        }

        building.AbilityModules.Remove<BuildingServiceSupportAbility>();
        building.AbilityModules.Add(new BuildingServiceSupportAbility
        {
            supportId = supportId,
            featureTags = features,
            compatibleHubTags = hubs,
            modifierType = modifier,
            capacity = capacity,
            workSpeedMultiplier = 1f,
            satisfactionModifier = satisfaction,
            revenueModifier = revenue
        });
        FinalizeBuilding(building);
    }

    private static void EnsureSupportAsset(SupportSpec spec)
    {
        string path = $"{Root}/{spec.Code}_{spec.Name}.asset";
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingSO>();
            AssetDatabase.CreateAsset(building, path);
        }

        BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(spec.SourcePath);
        if (source == null)
        {
            throw new InvalidOperationException(
                $"Service support sprite source was not found: {spec.SourcePath}");
        }

        building.id = spec.Id;
        building.objectName = spec.Name;
        building.sprite = source.sprite;
        building.icon = source.icon != null ? source.icon : source.sprite;
        building.width = Math.Max(1, source.width);
        building.height = Math.Max(1, source.height);
        building.layer = source.layer;
        building.category = BuildingCategory.Special;
        building.horizontalDraggable = false;
        building.verticalDraggable = false;
        building.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
        building.tiles = null;
        building.unlocked = false;

        BuildingAbilityCollection abilities = new BuildingAbilityCollection();
        abilities.Add(new BuildingEconomyAbility
        {
            constructionValue = spec.RequiresPower ? 40 : 24,
            maintenance = spec.RequiresPower ? 2 : 1,
            unlockPhase = spec.RequiresPower ? 3 : 2,
            demolitionRefundRate = 0.5f
        });
        BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = spec.RequiresPower ? 24.8f : 18.48f,
            repairWorkRequired = 10f,
            cleanWorkRequired = 6.25f,
            researchWorkRequired = 6f,
            operateWorkRequired = 10f
        };
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition(
                spec.RequiresPower
                    ? "component:machine-parts"
                    : "material:lumber",
                2)
        });
        abilities.Add(workAmount);
        abilities.Add(new BuildingFacilityPartAbility { code = spec.Code });
        abilities.Add(new BuildingRoomRequirementAbility());
        abilities.Add(new BuildingSemanticTagsAbility
        {
            tags = new[] { "ServiceRoom", spec.Modifier.ToString() }
        });
        abilities.Add(new BuildingServiceSupportAbility
        {
            supportId = spec.Code,
            featureTags = spec.Features,
            compatibleHubTags = spec.HubTags,
            modifierType = spec.Modifier,
            capacity = spec.Capacity,
            requiresPower = spec.RequiresPower,
            workSpeedMultiplier = spec.WorkSpeed,
            satisfactionModifier = spec.Satisfaction,
            revenueModifier = spec.Revenue
        });
        if (spec.RequiresPower)
        {
            abilities.Add(new BuildingPowerConsumerAbility
            {
                demandPerSecond = 0.5f,
                priority = PowerPriority.Production,
                minimumSupplyFraction = 1f
            });
        }

        building.ReplaceAbilities(abilities);
        FinalizeBuilding(building);
    }

    private static SupportSpec[] CreateSupportSpecs()
    {
        const string counter =
            "Assets/Resources/SO/Building/Modular/D04_배식카운터.asset";
        const string sales =
            "Assets/Resources/SO/Building/Modular/S01_판매카운터.asset";
        const string board =
            "Assets/Resources/SO/Building/Modular/G03_순찰상황판.asset";
        const string seat =
            "Assets/Resources/SO/Building/Modular/D07_목제의자.asset";
        const string cabinet =
            "Assets/Resources/SO/Building/Modular/H06_청소도구함.asset";

        return new[]
        {
            S(1700, "SR01", "숙박 접수대", counter,
                new[] { "service:lodging-reception" },
                new[] { "service:lodging" }, ServiceSupportModifierType.Stage),
            S(1701, "SR02", "목욕 접수대", counter,
                new[] { "service:bath-reception" },
                new[] { "service:bathing" }, ServiceSupportModifierType.Stage),
            S(1702, "SR03", "의료 분류대", counter,
                new[] { "service:medical-triage" },
                new[] { "service:medical" }, ServiceSupportModifierType.Stage,
                satisfaction: 4f),
            S(1703, "SR04", "순번판", board,
                new[] { "service:queue" },
                new[] { "service:dining", "service:retail", "service:medical" },
                ServiceSupportModifierType.Capacity, capacity: 2),
            S(1704, "SR05", "보온 배식대", counter,
                new[] { "service:heated-serving" },
                new[] { "service:dining" },
                ServiceSupportModifierType.WorkSpeed, requiresPower: true,
                speed: 1.35f, satisfaction: 6f),
            S(1705, "SR06", "자동 계산대", sales,
                new[] { "service:auto-checkout" },
                new[] { "service:retail" },
                ServiceSupportModifierType.WorkSpeed, requiresPower: true,
                speed: 1.5f),
            S(1706, "SR07", "슬라임 전용 좌석", seat,
                new[] { "service:seat", "service:seat:slime" },
                new[] { "service:dining" },
                ServiceSupportModifierType.Satisfaction, 1, satisfaction: 7f),
            S(1707, "SR08", "오크 전용 좌석", seat,
                new[] { "service:seat", "service:seat:orc" },
                new[] { "service:dining" },
                ServiceSupportModifierType.Satisfaction, 1, satisfaction: 7f),
            S(1708, "SR09", "뱀파이어 전용 좌석", seat,
                new[] { "service:seat", "service:seat:vampire" },
                new[] { "service:dining" },
                ServiceSupportModifierType.Satisfaction, 1, satisfaction: 7f),
            S(1709, "SR10", "객실 정리함", cabinet,
                new[] { "service:room-cleanup" },
                new[] { "service:lodging" },
                ServiceSupportModifierType.Cleanup, speed: 1.2f),
            S(1710, "SR11", "목욕 위생대", cabinet,
                new[] { "service:bath-hygiene" },
                new[] { "service:bathing" },
                ServiceSupportModifierType.Cleanup, satisfaction: 5f),
            S(1711, "SR12", "의료 호출판", board,
                new[] { "service:medical-call" },
                new[] { "service:medical" }, ServiceSupportModifierType.Stage,
                satisfaction: 3f),
            S(1712, "SR13", "주문 접수대", counter,
                new[] { "service:reception", "service:auto-order" },
                new[] { "service:dining" }, ServiceSupportModifierType.Stage),
            S(1713, "SR14", "분리 계산대", sales,
                new[] { "service:staffed-checkout" },
                new[] { "service:retail" }, ServiceSupportModifierType.Security,
                satisfaction: 3f),
            S(1714, "SR15", "자동 객실 배정판", board,
                new[] { "service:auto-room-assignment" },
                new[] { "service:lodging" },
                ServiceSupportModifierType.WorkSpeed, requiresPower: true,
                speed: 1.35f),
            S(1715, "SR16", "자동 급배수 제어기", board,
                new[] { "service:auto-water-control" },
                new[] { "service:bathing" },
                ServiceSupportModifierType.WorkSpeed, requiresPower: true,
                speed: 1.35f)
        };
    }

    private static void EnsureProcessAssets()
    {
        EnsureProcess(
            "service:dining:meal",
            ServiceCategory.Dining,
            "service:dining",
            string.Empty,
            0f,
            0f,
            false,
            6,
            42f,
            true);
        EnsureProcess(
            "service:retail:sale",
            ServiceCategory.Retail,
            "service:retail",
            string.Empty,
            0f,
            0f,
            false,
            4,
            40f,
            false);
        EnsureProcess(
            "service:lodging:rest",
            ServiceCategory.Lodging,
            "service:lodging",
            string.Empty,
            0f,
            0f,
            false,
            8,
            46f,
            true);
        EnsureProcess(
            "service:bathing:wash",
            ServiceCategory.Bathing,
            "service:bathing",
            string.Empty,
            0.45f,
            0.45f,
            true,
            5,
            44f,
            true);
        EnsureProcess(
            "service:medical:treat",
            ServiceCategory.Medical,
            "service:medical",
            "work:treat",
            0f,
            0f,
            false,
            10,
            48f,
            true);
    }

    private static void EnsureProcess(
        string processId,
        ServiceCategory category,
        string ownerHubTag,
        string workTypeId,
        float cleanWater,
        float wastewater,
        bool manualWaterFallback,
        int directPrice,
        float directSatisfaction,
        bool cleanup)
    {
        string fileName = processId.Replace(':', '_');
        string path = $"{ProcessRoot}/{fileName}.asset";
        ServiceProcessSO process =
            AssetDatabase.LoadAssetAtPath<ServiceProcessSO>(path);
        if (process == null)
        {
            process = ScriptableObject.CreateInstance<ServiceProcessSO>();
            AssetDatabase.CreateAsset(process, path);
        }

        process.Configure(
            processId,
            category,
            ownerHubTag,
            new[]
            {
                Contract(
                    ServiceOperationMode.Direct,
                    ServiceProcessStageMask.Service
                    | ServiceProcessStageMask.Payment,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    directPrice,
                    directSatisfaction),
                Contract(
                    ServiceOperationMode.Managed,
                    ServiceProcessStageMask.Reception
                    | ServiceProcessStageMask.Waiting
                    | ServiceProcessStageMask.Service
                    | ServiceProcessStageMask.Payment
                    | ServiceProcessStageMask.Cleanup,
                    0.5f,
                    0.5f,
                    0f,
                    0.25f,
                    0.5f,
                    directPrice + 2,
                    directSatisfaction + 10f),
                Contract(
                    ServiceOperationMode.Automated,
                    ServiceProcessStageMask.Waiting
                    | ServiceProcessStageMask.Service
                    | ServiceProcessStageMask.Payment
                    | ServiceProcessStageMask.Cleanup,
                    0f,
                    0.25f,
                    0f,
                    0.1f,
                    0.35f,
                    directPrice + 3,
                    directSatisfaction + 8f)
            },
            workTypeId,
            cleanWater,
            wastewater,
            manualWaterFallback,
            ServicePaymentPolicy.InternalStaffFree,
            cleanup);
        EditorUtility.SetDirty(process);
    }

    private static ServiceModeProcessContract Contract(
        ServiceOperationMode mode,
        ServiceProcessStageMask stages,
        float reception,
        float waiting,
        float service,
        float payment,
        float cleanup,
        int price,
        float satisfaction) =>
        new ServiceModeProcessContract
        {
            mode = mode,
            activeStages = stages,
            receptionSeconds = reception,
            waitingSeconds = waiting,
            serviceSeconds = service,
            paymentSeconds = payment,
            cleanupSeconds = cleanup,
            basePrice = price,
            satisfaction = satisfaction
        };

    private static SupportSpec S(
        int id,
        string code,
        string name,
        string source,
        string[] features,
        string[] hubs,
        ServiceSupportModifierType modifier,
        int capacity = 0,
        bool requiresPower = false,
        float speed = 1f,
        float satisfaction = 0f,
        int revenue = 0) =>
        new SupportSpec
        {
            Id = id,
            Code = code,
            Name = name,
            SourcePath = source,
            Features = features,
            HubTags = hubs,
            Modifier = modifier,
            Capacity = capacity,
            RequiresPower = requiresPower,
            WorkSpeed = speed,
            Satisfaction = satisfaction,
            Revenue = revenue
        };

    private static BuildingSO FindBuilding(string code) =>
        AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(building =>
                building != null
                && string.Equals(
                    building.GetAbility<BuildingFacilityPartAbility>()?.code,
                    code,
                    StringComparison.Ordinal));

    private static void FinalizeBuilding(BuildingSO building)
    {
        building.AbilityModules.EnsureStableIds();
        building.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(building);
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string segment in path.Replace('\\', '/').Split('/').Skip(1))
        {
            string next = $"{current}/{segment}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segment);
            }
            current = next;
        }
    }
}
#endif
