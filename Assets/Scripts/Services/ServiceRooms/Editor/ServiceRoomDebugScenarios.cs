#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ServiceRoomDebugScenarios
{
    [MenuItem("DungeonStory/QA/Service Rooms/Run Contract Scenarios")]
    public static void Run()
    {
        List<string> failures = Validate();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", failures));
        }

        Debug.Log(
            "Service room contracts PASS: five Direct services, closed demand "
            + "policy, explicit modes, 16 support facilities, five process "
            + "assets, 129 research projects and service.rooms V1.");
    }

    public static List<string> Validate()
    {
        List<string> failures = new();
        ValidateProcesses(failures);
        ValidateDirectHubs(failures);
        ValidateSupports(failures);
        ValidateResearch(failures);
        ValidateDemandAndSaveContracts(failures);
        return failures;
    }

    private static void ValidateProcesses(ICollection<string> failures)
    {
        ServiceProcessSO[] processes = LoadAll<ServiceProcessSO>(
            "Assets/Resources/SO/ServiceRooms/Processes");
        string[] expected =
        {
            "service:dining:meal",
            "service:retail:sale",
            "service:lodging:rest",
            "service:bathing:wash",
            "service:medical:treat"
        };
        foreach (string processId in expected)
        {
            ServiceProcessSO process = processes.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.ProcessId,
                    processId,
                    StringComparison.Ordinal));
            if (process == null)
            {
                failures.Add($"Missing service process: {processId}");
                continue;
            }

            foreach (ServiceOperationMode mode in Enum.GetValues(
                         typeof(ServiceOperationMode)))
            {
                if (!process.TryGetContract(mode, out _))
                {
                    failures.Add(
                        $"{processId} has no {mode} process contract.");
                }
            }

            if (!process.TryGetContract(
                    ServiceOperationMode.Direct,
                    out ServiceModeProcessContract direct)
                || (direct.activeStages & ServiceProcessStageMask.Service) == 0
                || (direct.activeStages & ServiceProcessStageMask.Payment) == 0
                || (direct.activeStages
                    & (ServiceProcessStageMask.Reception
                        | ServiceProcessStageMask.Waiting)) != 0)
            {
                failures.Add(
                    $"{processId} does not fold Direct reception/waiting.");
            }
        }

        ServiceProcessSO bathing = processes.FirstOrDefault(process =>
            process.ProcessId == "service:bathing:wash");
        if (bathing == null
            || bathing.CleanWater <= 0f
            || bathing.Wastewater <= 0f
            || !bathing.AllowsManualWaterFallback)
        {
            failures.Add(
                "Bathing must support both bucket fallback and piped wastewater.");
        }
    }

    private static void ValidateDirectHubs(ICollection<string> failures)
    {
        (string Path, ServiceCategory Category)[] hubs =
        {
            ("Assets/Resources/SO/Building/Modular/D04_배식카운터.asset",
                ServiceCategory.Dining),
            ("Assets/Resources/SO/Building/Modular/S01_판매카운터.asset",
                ServiceCategory.Retail),
            ("Assets/Resources/SO/Building/Modular/R01_간이침대.asset",
                ServiceCategory.Lodging),
            ("Assets/Resources/SO/Building/Modular/H04_목욕통.asset",
                ServiceCategory.Bathing),
            ("Assets/Resources/SO/Building/Medical/M01_응급처치대.asset",
                ServiceCategory.Medical)
        };

        foreach ((string path, ServiceCategory category) in hubs)
        {
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            BuildingServiceHubAbility hub =
                building?.GetAbility<BuildingServiceHubAbility>();
            if (building == null
                || hub == null
                || hub.serviceCategory != category
                || !hub.Allows(ServiceOperationMode.Direct)
                || !building.unlocked)
            {
                failures.Add(
                    $"{category} basic hub is not available in Direct mode.");
            }
        }
    }

    private static void ValidateSupports(ICollection<string> failures)
    {
        BuildingSO[] supports = LoadAll<BuildingSO>(
                ServiceRoomContentAssetBuilder.Root)
            .Where(building => building.id is >= 1700 and <= 1715)
            .OrderBy(building => building.id)
            .ToArray();
        if (supports.Length != 16)
        {
            failures.Add(
                $"Expected 16 service supports, found {supports.Length}.");
        }
        if (supports.Any(building =>
                building.GetAbility<BuildingServiceSupportAbility>() == null))
        {
            failures.Add("A service support lacks its support ability.");
        }
        if (supports.Any(building => building.unlocked))
        {
            failures.Add(
                "Research service supports must not start unlocked.");
        }
    }

    private static void ValidateResearch(ICollection<string> failures)
    {
        ResearchProjectSO[] projects = LoadAll<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");
        if (projects.Length != 135)
        {
            failures.Add(
                $"Expected 135 research projects, found {projects.Length}.");
        }

        foreach ((string id, int[] buildingIds) in
                 ServiceRoomContentAssetBuilder.GetResearchUnlockIds())
        {
            ResearchProjectSO project = projects.FirstOrDefault(candidate =>
                candidate.ProjectId.Value == id);
            if (project == null)
            {
                failures.Add($"Missing service research: {id}");
                continue;
            }

            int[] actual = project.Unlocks
                .OfType<BlueprintBuildingUnlock>()
                .Select(unlock => unlock.buildingId)
                .ToArray();
            foreach (int buildingId in buildingIds)
            {
                if (!actual.Contains(buildingId))
                {
                    failures.Add(
                        $"{id} does not unlock building {buildingId}.");
                }
            }
        }
    }

    private static void ValidateDemandAndSaveContracts(
        ICollection<string> failures)
    {
        ServiceAvailabilitySnapshot closed = new ServiceAvailabilitySnapshot
        {
            State = ServiceOperatingState.Closed,
            AdvertisingEnabled = false
        };
        if (closed.AcceptsNewDemand)
        {
            failures.Add("Closed service accepts new demand.");
        }

        ServiceHubModeSaveData legacyDefault = new ServiceHubModeSaveData();
        if (legacyDefault.mode != ServiceOperationMode.Direct)
        {
            failures.Add("Legacy service hubs do not default to Direct.");
        }
        if (ServiceRoomsSaveSection.Id != "service.rooms")
        {
            failures.Add("Service room save section id changed.");
        }

        ServiceSessionSnapshot source = new ServiceSessionSnapshot
        {
            SessionId = "session:save-roundtrip",
            HubId = "hub:save-roundtrip",
            ActorId = "actor:save-roundtrip",
            ProcessId = "service:dining",
            Category = ServiceCategory.Dining,
            Stage = ServiceSessionStage.Payment,
            StartedAt = 10f,
            StageStartedAt = 15f,
            AdvertisedDemand = true,
            PaymentCommitted = false,
            Contract = new ServiceSessionContractSnapshot
            {
                mode = ServiceOperationMode.Managed,
                activeStages =
                    ServiceProcessStageMask.Reception
                    | ServiceProcessStageMask.Waiting
                    | ServiceProcessStageMask.Service
                    | ServiceProcessStageMask.Payment
                    | ServiceProcessStageMask.Cleanup,
                receptionSeconds = 2f,
                waitingSeconds = 3f,
                serviceSeconds = 4f,
                paymentSeconds = 5f,
                cleanupSeconds = 6f,
                price = 17,
                satisfaction = 64f,
                paymentRequired = true,
                internalActor = false,
                supportIds = new[] { "support:queue", "support:checkout" }
            }
        };
        ServiceSessionSaveData saved =
            ServiceRoomsSaveData.FromSnapshot(source);
        ServiceSessionSnapshot restored = saved?.ToSnapshot();
        if (restored == null
            || restored.SessionId != source.SessionId
            || restored.HubId != source.HubId
            || restored.Stage != source.Stage
            || restored.Contract.mode != source.Contract.mode
            || restored.Contract.activeStages != source.Contract.activeStages
            || restored.Contract.price != source.Contract.price
            || restored.Contract.supportIds.Length != 2
            || restored.Contract.supportIds[1] != "support:checkout")
        {
            failures.Add(
                "Active service session contract does not survive save roundtrip.");
        }
    }

    private static T[] LoadAll<T>(string root)
        where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
}
#endif
