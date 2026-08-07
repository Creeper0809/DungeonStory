#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.ServiceRooms;
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
            + "assets, 216 research projects and service.rooms V2.");
    }

    public static List<string> Validate()
    {
        List<string> failures = new();
        ValidateProcesses(failures);
        ValidateDirectHubs(failures);
        ValidateSupports(failures);
        ValidateResearch(failures);
        ValidateDemandAndSaveContracts(failures);
        ValidateAggregateAuthority(failures);
        ValidateHubUnsubscribe(failures);
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
        if (projects.Length != 216)
        {
            failures.Add(
                $"Expected 216 research projects, found {projects.Length}.");
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
        if (!string.Equals(
                ServiceRoomsSaveSection.Id,
                "service.rooms",
                StringComparison.Ordinal))
        {
            failures.Add("Service room save section id changed.");
        }
        object serviceRoomsVersion = typeof(ServiceRoomsSaveData)
            .GetField(nameof(ServiceRoomsSaveData.CurrentVersion))?
            .GetRawConstantValue();
        if (serviceRoomsVersion is not int version || version != 2)
        {
            failures.Add("Service room save payload is not exact V2.");
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

    private static void ValidateAggregateAuthority(
        ICollection<string> failures)
    {
        ServiceSessionContractSnapshot contract = new()
        {
            mode = ServiceOperationMode.Direct,
            activeStages = ServiceProcessStageMask.Service
                | ServiceProcessStageMask.Payment,
            serviceSeconds = 2f,
            price = 17,
            paymentRequired = true,
            supportIds = Array.Empty<string>()
        };
        ServiceSessionAggregate aggregate = new();
        if (!aggregate.TryBegin(
                new ServiceSessionBeginCommand
                {
                    HubId = "hub:aggregate-test",
                    ActorId = "actor:aggregate-test",
                    ProcessId = "service:test",
                    Category = ServiceCategory.Dining,
                    Capacity = 1,
                    StartedAt = 10f,
                    Contract = contract
                },
                out ServiceSessionSnapshot active,
                out DomainFailure beginFailure))
        {
            failures.Add(
                $"Service aggregate rejected a valid begin command: {beginFailure.Code}.");
            return;
        }

        int versionBeforeCompletion = aggregate.Version;
        bool firstCompletion = aggregate.TryComplete(
            active.SessionId,
            20f,
            out ServiceSessionCompletionTransition first,
            out DomainFailure firstFailure);
        bool duplicateCompletion = aggregate.TryComplete(
            active.SessionId,
            21f,
            out ServiceSessionCompletionTransition duplicate,
            out DomainFailure duplicateFailure);
        if (!firstCompletion
            || firstFailure.IsFailure
            || first?.Completed?.Stage != ServiceSessionStage.Completed
            || first.Completed.PaymentCommitted != true
            || first.EconomicCommand?.Amount != 17
            || first.EconomicCommand.CommandId
                != "service-payment:" + active.SessionId
            || aggregate.Version != versionBeforeCompletion + 1
            || duplicateCompletion
            || duplicate != null
            || duplicateFailure.Code != FailureCode.ServiceSessionMissing)
        {
            failures.Add(
                "Service completion did not issue exactly one economic command or reject duplicate completion.");
        }

        ServiceSessionAggregate roundtripSource = new();
        roundtripSource.SetMode(
            "hub:roundtrip",
            ServiceOperationMode.Managed,
            out _);
        roundtripSource.SetAdvertisingEnabled(ServiceCategory.Lodging, true);
        ServiceSessionContractSnapshot roundtripContract = contract.Clone();
        roundtripContract.mode = ServiceOperationMode.Managed;
        if (!roundtripSource.TryBegin(
                new ServiceSessionBeginCommand
                {
                    HubId = "hub:roundtrip",
                    ActorId = "actor:roundtrip",
                    ProcessId = "service:roundtrip",
                    Category = ServiceCategory.Lodging,
                    Capacity = 2,
                    StartedAt = 30f,
                    Contract = roundtripContract
                },
                out ServiceSessionSnapshot sourceSession,
                out _))
        {
            failures.Add("Service aggregate restore fixture could not begin.");
            return;
        }
        ServiceRoomsSaveData captured = roundtripSource.Capture();
        ServiceSessionAggregate restored = ServiceSessionAggregate.CreateRestored(
            captured,
            roundtripSource.Version + 1);
        ServiceRoomsSaveData recaptured = restored.Capture();
        ServiceSessionSaveData restoredSession = recaptured.sessions.SingleOrDefault();
        if (restoredSession == null
            || restoredSession.sessionId != sourceSession.SessionId
            || restoredSession.hubId != sourceSession.HubId
            || restoredSession.actorId != sourceSession.ActorId
            || restoredSession.processId != sourceSession.ProcessId
            || restored.Version != roundtripSource.Version + 1)
        {
            failures.Add(
                "Service aggregate restore did not preserve canonical persistent IDs and version transition.");
        }

        captured.sessions[0].sessionId = " " + captured.sessions[0].sessionId;
        bool rejectedNonCanonical = false;
        try
        {
            ServiceSessionAggregate.CreateRestored(captured, 99);
        }
        catch (ArgumentException)
        {
            rejectedNonCanonical = true;
        }
        if (!rejectedNonCanonical)
        {
            failures.Add(
                "Service aggregate restore accepted a repaired/non-canonical session ID.");
        }
    }

    private static void ValidateHubUnsubscribe(ICollection<string> failures)
    {
        Type registryDefinition = typeof(ServiceSessionRuntime).Assembly.GetType(
            "ServiceHubSubscriptionRegistry`1",
            throwOnError: false);
        Type registryType = registryDefinition?.MakeGenericType(
            typeof(FakeServiceHub));
        MethodInfo synchronize = registryType?.GetMethod(
            "Synchronize",
            BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo count = registryType?.GetProperty(
            "Count",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (registryType == null
            || synchronize == null
            || count == null)
        {
            failures.Add("Service hub subscription registry is not testable.");
            return;
        }

        Action<FakeServiceHub, Action> attach =
            (hub, handler) => hub.Destroyed += handler;
        Action<FakeServiceHub, Action> detach =
            (hub, handler) => hub.Destroyed -= handler;
        object registry = Activator.CreateInstance(
            registryType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { attach, detach },
            culture: null);
        FakeServiceHub hub = new();
        int callbacks = 0;
        Action<FakeServiceHub> onDestroyed = _ => callbacks++;
        synchronize.Invoke(
            registry,
            new object[] { new[] { hub }, onDestroyed });
        synchronize.Invoke(
            registry,
            new object[] { Array.Empty<FakeServiceHub>(), onDestroyed });
        hub.RaiseDestroyed();
        if ((int)count.GetValue(registry) != 0 || callbacks != 0)
        {
            failures.Add(
                "Removed service hubs retain a destruction subscription after synchronization.");
        }
    }

    private sealed class FakeServiceHub
    {
        internal event Action Destroyed;

        internal void RaiseDestroyed() => Destroyed?.Invoke();
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
