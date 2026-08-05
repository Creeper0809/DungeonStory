#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Content.CoreSession;
using DungeonStory.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.ServiceRooms;
using UnityEditor;
using UnityEngine;

public static class BatchACoreSessionSaveDebugScenarios
{
    [MenuItem("DungeonStory/QA/V18/Run Batch A Core Session Save Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            throw new InvalidOperationException(
                "Batch A core/session save scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new();
        Verify("ExperiencePacing", VerifyExperiencePacing, failures);
        Verify("ExternalInfluence", VerifyExternalInfluence, failures);
        Verify("RunFlow", VerifyRunFlow, failures);
        Verify("RunVariable", VerifyRunVariablePreflight, failures);
        Verify("DungeonDebug", VerifyDungeonDebug, failures);
        Verify("ServiceRooms", VerifyServiceRooms, failures);
        Verify(
            "CoreSessionStateOwnership",
            VerifyCoreSessionStateOwnership,
            failures);
        Verify(
            "RunVariableDoctrineEdge",
            VerifyRunVariableDoctrineEdge,
            failures);
        Verify("AtomicBatchBoundary", VerifyAtomicBatchBoundary, failures);
        Verify("FinalSectionFailure", VerifyFinalSectionFailure, failures);
        Verify("IntegratedRuntimeFlow", VerifyIntegratedRuntimeFlow, failures);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }
        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log(
                "Batch A core/session strict saves PASS: six required exact-version "
                + "typed sections, canonical restore/preflight, invalid no-mutation, "
                + "named aggregate ownership, explicit doctrine effects, and "
                + "rollback-free contracts.");
        }
        return failures.Count == 0;
    }

    private static bool VerifyCoreSessionStateOwnership()
    {
        const string expectedAssembly = "DungeonStory.CoreSession";
        Type[] aggregateTypes =
        {
            typeof(ExperiencePacingAggregateState),
            typeof(ExternalInfluenceAggregateState),
            typeof(DungeonRunFlowAggregateState),
            typeof(RunVariableAggregateState),
            typeof(DungeonDebugModeState)
        };
        bool aggregatesOwnedByCoreSession = aggregateTypes.All(type =>
            string.Equals(
                type.Assembly.GetName().Name,
                expectedAssembly,
                StringComparison.Ordinal));
        bool stateDoesNotOwnDoctrineCatalog =
            typeof(IRunVariableStateView).GetProperty(
                "OwnerDoctrines",
                BindingFlags.Public | BindingFlags.Instance) == null;
        bool contractsDoNotOwnPresentationFormatters =
            typeof(RunStartVariableSnapshot)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .All(method => !string.Equals(
                    method.Name,
                    "ToSummaryText",
                    StringComparison.Ordinal))
            && typeof(RunVariableDefinition)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .All(method => !string.Equals(
                    method.Name,
                    "ToDetailText",
                    StringComparison.Ordinal));
        bool serviceSessionsOwnedByServiceRooms = string.Equals(
            typeof(ServiceSessionAggregate).Assembly.GetName().Name,
            "DungeonStory.ServiceRooms",
            StringComparison.Ordinal);
        return aggregatesOwnedByCoreSession
            && serviceSessionsOwnedByServiceRooms
            && stateDoesNotOwnDoctrineCatalog
            && contractsDoNotOwnPresentationFormatters;
    }

    private static bool VerifyRunVariableDoctrineEdge()
    {
        string[] doctrineEffectEdges =
        {
            "GetGuestDemandMultiplier",
            "GetStockCostMultiplier",
            "GetFacilityShopCostMultiplier",
            "GetBlueprintCostMultiplier",
            "GetThreatRiseMultiplier",
            "GetWarningThresholdMultiplier",
            "ApplyInvasionSettings"
        };
        MethodInfo[] effectMethods = typeof(RunVariableEffects).GetMethods(
            BindingFlags.Public | BindingFlags.Static);
        return doctrineEffectEdges.All(edge => effectMethods.Any(method =>
        {
            if (!string.Equals(method.Name, edge, StringComparison.Ordinal))
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length >= 2
                && parameters[0].ParameterType == typeof(IRunVariableStateView)
                && parameters[1].ParameterType
                    == typeof(IOwnerDoctrineDefinitionCatalog);
        }));
    }

    private static bool VerifyExperiencePacing()
    {
        ExperienceRuntimeFake runtime = new();
        ExperiencePacingSaveSection section = new(
            runtime,
            LoadRulesProvider());
        DungeonExperiencePacingSaveData canonical = new();
        DungeonExperiencePacingSaveData invalid = new()
        {
            scheduledRehearsalMask = 0,
            completedRehearsalMask = 1
        };
        return VerifyStrictRestoreBoundary(
            section,
            JsonUtility.ToJson(canonical),
            JsonUtility.ToJson(invalid),
            JsonUtility.ToJson(
                new DungeonExperiencePacingSaveData { currentDay = 2 }),
            () => runtime.RestoreCount);
    }

    private static bool VerifyExternalInfluence()
    {
        ExternalRuntimeFake runtime = new();
        ExternalInfluenceSaveSection section = new(runtime);
        DungeonExternalInfluenceSaveData canonical = new();
        DungeonExternalInfluenceSaveData invalid = new() { renown = 1000f };
        DungeonExternalInfluenceSaveData lateDiscard = new()
        {
            scoutingLabor = 1f
        };
        return VerifyStrictRestoreBoundary(
            section,
            JsonUtility.ToJson(canonical),
            JsonUtility.ToJson(invalid),
            JsonUtility.ToJson(lateDiscard),
            () => runtime.RestoreCount);
    }

    private static bool VerifyRunFlow()
    {
        RunFlowRuntimeFake runtime = new();
        RunFlowSaveSection section = new(runtime, LoadRulesProvider());
        DungeonRunFlowSaveData canonical = new();
        DungeonRunFlowSaveData invalid = new()
        {
            phase = DungeonRunPhase.Growth,
            currentDay = 1
        };
        return VerifyStrictRestoreBoundary(
            section,
            JsonUtility.ToJson(canonical),
            JsonUtility.ToJson(invalid),
            JsonUtility.ToJson(new DungeonRunFlowSaveData
            {
                phase = DungeonRunPhase.Growth,
                currentDay = 4
            }),
            () => runtime.RestoreCount);
    }

    private static bool VerifyRunVariablePreflight()
    {
        GameObject host = new("BatchA_RunVariable_Preflight");
        try
        {
            DungeonRuntimeAggregateRootStore store = new();
            InvasionThreatRuntime threat =
                host.AddComponent<InvasionThreatRuntime>();
            RunVariableRuntime runtime = host.AddComponent<RunVariableRuntime>();
            runtime.Construct(
                EmptyOwnerRunDataProvider.Instance,
                new InvasionSceneRuntimeReferences(threat, null, null),
                DisabledRunStartVariableSelector.Instance,
                new RandomStreamProvider(store),
                new GameEventBus(),
                EmptyRunVariableCatalog.Instance,
                EmptyDoctrineCatalog.Instance,
                store);
            RunVariableSaveSection section = new(
                runtime,
                EmptyRunVariableCatalog.Instance,
                EmptyDoctrineCatalog.Instance);
            DungeonRunVariableSaveData canonical = new()
            {
                runSeed = 17,
                currentDay = 1,
                hasStartVariables = false,
                startVariables = null,
                invasionVariableId = string.Empty
            };
            DungeonRunVariableSaveData invalid = new()
            {
                runSeed = 0,
                currentDay = 1,
                hasStartVariables = false,
                startVariables = null,
                invasionVariableId = string.Empty
            };
            string canonicalJson = JsonUtility.ToJson(canonical);
            string before = section.Capture();
            DungeonGameRestoreReport stageReport = new();
            IDungeonSaveRestoreStage stage = section.StageRestore(
                canonicalJson,
                section.SectionVersion,
                stageReport);
            if (!stageReport.Success || section.Capture() != before)
            {
                return false;
            }
            stage.Commit(stageReport);
            if (!stageReport.Success || section.Capture() != canonicalJson)
            {
                return false;
            }

            string committed = section.Capture();
            string legacyPayload = WithPayloadVersion(
                canonicalJson,
                section.SectionVersion,
                section.SectionVersion - 1);
            if (!RejectsStrictRestore(
                    section,
                    canonicalJson,
                    section.SectionVersion - 1)
                || !RejectsStrictRestore(
                    section,
                    legacyPayload,
                    section.SectionVersion)
                || !RejectsStrictRestore(
                    section,
                    JsonUtility.ToJson(invalid),
                    section.SectionVersion)
                || !RejectsStrictRestore(
                    section,
                    string.Empty,
                    section.SectionVersion)
                || section.Capture() != committed)
            {
                return false;
            }

            DungeonRunVariableSaveData alternate = new()
            {
                runSeed = 18,
                currentDay = 1,
                hasStartVariables = false,
                startVariables = null,
                invasionVariableId = string.Empty
            };
            DungeonGameRestoreReport discardReport = new();
            section.StageRestore(
                JsonUtility.ToJson(alternate),
                section.SectionVersion,
                discardReport);
            return discardReport.Success
                && section.Capture() == committed
                && HasStrictContracts(section);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static bool VerifyDungeonDebug()
    {
        DebugModeFake runtime = new();
        DungeonDebugSaveSection section = new(
            runtime,
            LoadRulesProvider());
        DungeonDebugRunSaveData canonical = new();
        DungeonDebugRunSaveData invalid = new();
        for (int index = 0; index < 51; index++)
        {
            invalid.recentCommands.Add(new DungeonDebugCommandHistorySaveData());
        }
        DungeonDebugRunSaveData lateDiscard = new()
        {
            debugModified = true
        };
        return VerifyStrictRestoreBoundary(
            section,
            JsonUtility.ToJson(canonical),
            JsonUtility.ToJson(invalid),
            JsonUtility.ToJson(lateDiscard),
            () => runtime.RestoreCount);
    }

    private static bool VerifyServiceRooms()
    {
        ServiceRuntimeFake runtime = new();
        ServiceRoomsSaveSection section = new(
            runtime,
            EmptyServiceProcessCatalog.Instance);
        ServiceRoomsSaveData canonical = new();
        ServiceRoomsSaveData invalid = new() { version = 1 };
        return VerifyRestoreBoundary(
            section,
            JsonUtility.ToJson(canonical),
            JsonUtility.ToJson(invalid),
            () => runtime.RestoreCount);
    }

    private static bool VerifyAtomicBatchBoundary()
    {
        ExperienceRuntimeFake experience = new();
        ExternalRuntimeFake external = new();
        RunFlowRuntimeFake runFlow = new();
        DebugModeFake debug = new();
        ServiceRuntimeFake services = new();
        DungeonRuntimeAggregateRootStore store = new();
        GameObject host = new("BatchA_Atomic_Save_Boundary");
        try
        {
            RunVariableRuntime runVariables = host.AddComponent<RunVariableRuntime>();
            DungeonSceneRuntimeReferences references =
                new DungeonSceneRuntimeReferences(
                    new DungeonSceneServiceReferences(null, null, null, runVariables),
                    new DungeonSceneViewReferences(
                        null, null, null, null, null, null, null, null));
            IDungeonSaveSection facilitiesDependency =
                new RequiredDependencyStubSection(
                    ModularFacilityWorldSaveSection.Id,
                    DungeonSaveRestorePhase.World);
            IDungeonSaveSection charactersDependency =
                new RequiredDependencyStubSection(
                    CharacterWorldSaveSection.Id,
                    DungeonSaveRestorePhase.Characters);
            IDungeonSaveSection itemsDependency =
                new RequiredDependencyStubSection(
                    PhysicalItemsSaveSection.Id,
                    DungeonSaveRestorePhase.Items);
            IDungeonSaveSection wildlifeDependency =
                new RequiredDependencyStubSection(
                    WildlifeSaveSection.Id,
                    DungeonSaveRestorePhase.RuntimeState);
            IDungeonSaveSection offenseDependency =
                new RequiredDependencyStubSection(
                    OffenseAggregateSaveSection.Id,
                    DungeonSaveRestorePhase.LateRuntimeState);
            IDungeonSaveSection invasionDependency =
                new RequiredDependencyStubSection(
                    InvasionSaveSection.Id,
                    DungeonSaveRestorePhase.LateRuntimeState);
            IDungeonSaveSection experienceSection =
                new ExperiencePacingSaveSection(
                    experience,
                    LoadRulesProvider());
            IDungeonSaveSection externalSection =
                new ExternalInfluenceSaveSection(external);
            IDungeonSaveSection runFlowSection =
                new RunFlowSaveSection(runFlow, LoadRulesProvider());
            IDungeonSaveSection runVariableSection =
                new RunVariableSaveSection(
                    references.RunVariables,
                    EmptyRunVariableCatalog.Instance,
                    EmptyDoctrineCatalog.Instance);
            IDungeonSaveSection debugSection =
                new DungeonDebugSaveSection(debug, LoadRulesProvider());
            IDungeonSaveSection serviceSection =
                new ServiceRoomsSaveSection(
                    services,
                    EmptyServiceProcessCatalog.Instance);
            IDungeonSaveSection[] sections =
            {
                facilitiesDependency,
                charactersDependency,
                itemsDependency,
                wildlifeDependency,
                offenseDependency,
                invasionDependency,
                experienceSection,
                externalSection,
                runFlowSection,
                runVariableSection,
                debugSection,
                serviceSection
            };
            if (sections.Any(section => !HasStrictContracts(section)))
            {
                return false;
            }

            DungeonSaveSectionRegistry registry = new(sections, store);
            List<DungeonSaveSectionEnvelope> envelopes = new()
            {
                CreateRawEnvelope(facilitiesDependency, "{}"),
                CreateRawEnvelope(charactersDependency, "{}"),
                CreateRawEnvelope(itemsDependency, "{}"),
                CreateRawEnvelope(wildlifeDependency, "{}"),
                CreateRawEnvelope(offenseDependency, "{}"),
                CreateRawEnvelope(invasionDependency, "{}"),
                CreateEnvelope(
                    experienceSection,
                    new DungeonExperiencePacingSaveData()),
                CreateEnvelope(
                    externalSection,
                    new DungeonExternalInfluenceSaveData()),
                CreateEnvelope(runFlowSection, new DungeonRunFlowSaveData()),
                CreateEnvelope(
                    runVariableSection,
                    new DungeonRunVariableSaveData
                    {
                        runSeed = 17,
                        currentDay = 1,
                        hasStartVariables = false,
                        startVariables = null,
                        invasionVariableId = string.Empty
                    }),
                CreateEnvelope(debugSection, new DungeonDebugRunSaveData()),
                CreateEnvelope(
                    serviceSection,
                    new ServiceRoomsSaveData { version = 1 })
            };

            DungeonGameRestoreReport report = new();
            bool restored = registry.RestoreAll(envelopes, report);
            return !restored
                && !report.Success
                && registry.OrderedSections.Count == 12
                && sections
                    .Where(section => section is not RequiredDependencyStubSection)
                    .Select(section => section.SectionId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 6
                && experience.RestoreCount == 0
                && external.RestoreCount == 0
                && runFlow.RestoreCount == 0
                && debug.RestoreCount == 0
                && services.RestoreCount == 0;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static bool VerifyFinalSectionFailure()
    {
        DungeonRuntimeAggregateRootStore store = new();
        ExperienceRuntimeFake experience = new(store);
        ExternalRuntimeFake external = new(store);
        RunFlowRuntimeFake runFlow = new(store);
        DebugModeFake debug = new(store);
        ServiceRuntimeFake services = new(store);
        GameObject host = new("BatchA_Final_Section_Failure");
        try
        {
            InvasionThreatRuntime threat =
                host.AddComponent<InvasionThreatRuntime>();
            RunVariableRuntime runVariables =
                host.AddComponent<RunVariableRuntime>();
            runVariables.Construct(
                EmptyOwnerRunDataProvider.Instance,
                new InvasionSceneRuntimeReferences(threat, null, null),
                DisabledRunStartVariableSelector.Instance,
                new RandomStreamProvider(store),
                new GameEventBus(),
                EmptyRunVariableCatalog.Instance,
                EmptyDoctrineCatalog.Instance,
                store);
            DungeonSceneRuntimeReferences references =
                new DungeonSceneRuntimeReferences(
                    new DungeonSceneServiceReferences(null, null, null, runVariables),
                    new DungeonSceneViewReferences(
                        null, null, null, null, null, null, null, null));

            IDungeonSaveSection facilitiesDependency =
                CreateDependency(
                    ModularFacilityWorldSaveSection.Id,
                    DungeonSaveRestorePhase.World);
            IDungeonSaveSection charactersDependency =
                CreateDependency(
                    CharacterWorldSaveSection.Id,
                    DungeonSaveRestorePhase.Characters);
            IDungeonSaveSection itemsDependency =
                CreateDependency(
                    PhysicalItemsSaveSection.Id,
                    DungeonSaveRestorePhase.Items);
            IDungeonSaveSection wildlifeDependency =
                CreateDependency(
                    WildlifeSaveSection.Id,
                    DungeonSaveRestorePhase.RuntimeState);
            IDungeonSaveSection offenseDependency =
                CreateDependency(
                    OffenseAggregateSaveSection.Id,
                    DungeonSaveRestorePhase.LateRuntimeState);
            IDungeonSaveSection invasionDependency =
                CreateDependency(
                    InvasionSaveSection.Id,
                    DungeonSaveRestorePhase.LateRuntimeState);
            IDungeonSaveSection experienceSection =
                new ExperiencePacingSaveSection(
                    experience,
                    LoadRulesProvider());
            IDungeonSaveSection externalSection =
                new ExternalInfluenceSaveSection(external);
            IDungeonSaveSection runFlowSection =
                new RunFlowSaveSection(runFlow, LoadRulesProvider());
            IDungeonSaveSection runVariableSection =
                new RunVariableSaveSection(
                    references.RunVariables,
                    EmptyRunVariableCatalog.Instance,
                    EmptyDoctrineCatalog.Instance);
            IDungeonSaveSection debugSection =
                new DungeonDebugSaveSection(debug, LoadRulesProvider());
            IDungeonSaveSection serviceSection =
                new ServiceRoomsSaveSection(
                    services,
                    EmptyServiceProcessCatalog.Instance);
            FinalFailingSection finalFailure = new(new[]
            {
                experienceSection.SectionId,
                externalSection.SectionId,
                runFlowSection.SectionId,
                runVariableSection.SectionId,
                debugSection.SectionId,
                serviceSection.SectionId
            });
            IDungeonSaveSection[] sections =
            {
                facilitiesDependency,
                charactersDependency,
                itemsDependency,
                wildlifeDependency,
                offenseDependency,
                invasionDependency,
                experienceSection,
                externalSection,
                runFlowSection,
                runVariableSection,
                debugSection,
                serviceSection,
                finalFailure
            };
            if (sections.Any(section => !HasStrictContracts(section)))
            {
                return false;
            }

            string experienceBefore = JsonUtility.ToJson(experience.Capture());
            string externalBefore = JsonUtility.ToJson(external.Capture());
            string runFlowBefore = JsonUtility.ToJson(runFlow.CaptureState());
            string debugBefore = JsonUtility.ToJson(debug.Capture());
            string serviceBefore = JsonUtility.ToJson(services.Capture());
            int runVariableSeedBefore = runVariables.RunSeed;
            int runVariableDayBefore = runVariables.CurrentDay;
            int revisionBefore = store.PublishedRestoreRevision;

            List<DungeonSaveSectionEnvelope> envelopes = new()
            {
                CreateRawEnvelope(facilitiesDependency, "{}"),
                CreateRawEnvelope(charactersDependency, "{}"),
                CreateRawEnvelope(itemsDependency, "{}"),
                CreateRawEnvelope(wildlifeDependency, "{}"),
                CreateRawEnvelope(offenseDependency, "{}"),
                CreateRawEnvelope(invasionDependency, "{}"),
                CreateEnvelope(
                    experienceSection,
                    new DungeonExperiencePacingSaveData { currentDay = 4 }),
                CreateEnvelope(
                    externalSection,
                    new DungeonExternalInfluenceSaveData { renown = 5f }),
                CreateEnvelope(
                    runFlowSection,
                    new DungeonRunFlowSaveData
                    {
                        phase = DungeonRunPhase.Growth,
                        currentDay = 4
                    }),
                CreateEnvelope(
                    runVariableSection,
                    new DungeonRunVariableSaveData
                    {
                        runSeed = 17,
                        currentDay = 2,
                        hasStartVariables = false,
                        startVariables = null,
                        invasionVariableId = string.Empty
                    }),
                CreateEnvelope(
                    debugSection,
                    new DungeonDebugRunSaveData { debugModified = true }),
                CreateEnvelope(
                    serviceSection,
                    new ServiceRoomsSaveData
                    {
                        advertisedCategories =
                            new List<ServiceCategory> { ServiceCategory.Dining }
                    }),
                CreateRawEnvelope(finalFailure, "{}")
            };

            DungeonSaveSectionRegistry registry = new(sections, store);
            DungeonGameRestoreReport report = new();
            bool restored = registry.RestoreAll(envelopes, report);
            return !restored
                && !report.Success
                && finalFailure.WasCommitted
                && !store.IsRestoreStaging
                && store.PublishedRestoreRevision == revisionBefore
                && JsonUtility.ToJson(experience.Capture()) == experienceBefore
                && JsonUtility.ToJson(external.Capture()) == externalBefore
                && JsonUtility.ToJson(runFlow.CaptureState()) == runFlowBefore
                && JsonUtility.ToJson(debug.Capture()) == debugBefore
                && JsonUtility.ToJson(services.Capture()) == serviceBefore
                && runVariables.RunSeed == runVariableSeedBefore
                && runVariables.CurrentDay == runVariableDayBefore;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static bool VerifyIntegratedRuntimeFlow()
    {
        DungeonRuntimeAggregateRootStore store = new();
        GameEventBus events = new();
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        ICoreSessionRulesProvider rulesProvider = content;
        ResourceItemDefinitionCatalog items = new(
            content.Items.Definitions);
        ResourceServiceProcessCatalog serviceProcesses = new(content);
        AuthoredGameplayCatalog authored = new(content);
        GameObject host = new("BatchA_Integrated_Runtime_Flow");
        ExperiencePacingRuntime experience = null;
        DungeonRunFlowRuntime runFlow = null;
        ExternalInfluenceRuntimeApplicationAdapter external = null;
        DungeonDebugModeService debug = null;
        try
        {
            InvasionThreatRuntime threat =
                host.AddComponent<InvasionThreatRuntime>();
            InvasionDirectorRuntime director =
                host.AddComponent<InvasionDirectorRuntime>();
            InvasionSceneRuntimeReferences invasion = new(
                threat,
                director,
                null);

            experience = new ExperiencePacingRuntime(
                new ExperiencePacingApplicationAdapter(
                    events,
                    store,
                    rulesProvider));
            runFlow = new DungeonRunFlowRuntime(
                EmptyOwnerRunManagerProvider.Instance,
                invasion,
                events,
                experience,
                store,
                rulesProvider);
            external = new ExternalInfluenceRuntimeApplicationAdapter(
                events,
                new IntegratedMoneyAccount(1000),
                DefaultInterfaceProxy.Create<IWorldItemStackRuntime>(),
                items,
                DefaultInterfaceProxy.Create<IWildlifeRuntime>(),
                DefaultInterfaceProxy.Create<ISurvivalEnvironmentQuery>(),
                new ExternalInfluenceRuntimeApplicationAdapter.Dependencies(
                    IntegratedGameClock.Instance,
                    rulesProvider),
                new ExternalInfluenceAggregateStateStore(store));
            IntegratedSettings settings = new();
            debug = new DungeonDebugModeService(
                settings,
                new IntegratedSessionStateProvider(),
                store,
                rulesProvider);

            experience.Start();
            runFlow.Start();
            external.Start();
            debug.Start();
            events.Publish(new OperatingDayStartedEvent(4));

            external.AddRenown(20f, "batch-a-integrated");
            external.AddHostileRumor(15f, "batch-a-integrated");
            bool mitigated = external.TryMitigateHostileRumor(
                HostileRumorMitigationMethod.Renown,
                out float reducedRumor,
                out int renownCost,
                out DomainFailure externalFailure);

            RunVariableRuntime runVariables =
                host.AddComponent<RunVariableRuntime>();
            runVariables.Construct(
                EmptyOwnerRunDataProvider.Instance,
                invasion,
                DisabledRunStartVariableSelector.Instance,
                new RandomStreamProvider(store),
                events,
                authored,
                authored,
                store);
            runVariables.RestoreRun(
                23,
                4,
                null,
                Array.Empty<ActiveRunVariable>(),
                null);

            debug.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
            debug.MarkMutation(
                "batch-a:integrated",
                "core-session",
                DungeonDebugCommandResult.Succeeded("ok"));

            ServiceSessionRuntime services = new(
                DefaultInterfaceProxy.Create<IBuildingWorldQuery>(),
                DefaultInterfaceProxy.Create<IServiceRoomLinkRuntime>(),
                serviceProcesses,
                new ServiceSessionRuntime.Dependencies(
                    IntegratedGameClock.Instance,
                    new IntegratedMoneyAccount(1000),
                    DefaultInterfaceProxy.Create<IPowerInfrastructureQuery>(),
                    DefaultInterfaceProxy.Create<IServiceRoomResearchQuery>(),
                    rulesProvider),
                store,
                DefaultInterfaceProxy.Create<IRestoreWorldCandidateQuery>());
            services.SetAdvertisingEnabled(ServiceCategory.Dining, true);

            DungeonSceneRuntimeReferences references =
                new DungeonSceneRuntimeReferences(
                    new DungeonSceneServiceReferences(null, null, null, runVariables),
                    new DungeonSceneViewReferences(
                        null, null, null, null, null, null, null, null));
            IDungeonSaveSection[] ownerSections =
            {
                new ExperiencePacingSaveSection(experience, rulesProvider),
                new ExternalInfluenceSaveSection(external),
                new RunFlowSaveSection(runFlow, rulesProvider),
                new RunVariableSaveSection(runVariables, authored, authored),
                new DungeonDebugSaveSection(debug, rulesProvider),
                new ServiceRoomsSaveSection(services, serviceProcesses)
            };
            bool capturedAll = ownerSections.All(section =>
            {
                string payload = section.Capture();
                return !string.IsNullOrWhiteSpace(payload)
                    && payload != "{}";
            });

            DomainFailureLocalizer localizer = new();
            string externalMessage = localizer.Localize(new DomainFailure(
                FailureCode.InsufficientRenown,
                "0",
                "10"));
            string serviceMessage = localizer.Localize(new DomainFailure(
                FailureCode.ServiceHubUnavailable));
            bool presentationMapped =
                !string.IsNullOrWhiteSpace(externalMessage)
                && !externalMessage.Contains("{0}", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(serviceMessage)
                && !string.Equals(
                    serviceMessage,
                    FailureCode.ServiceHubUnavailable.ToString(),
                    StringComparison.Ordinal);

            return ReferenceEquals(
                    content.CoreSessionRules,
                    rulesProvider.CoreSessionRules)
                && experience.CurrentDay == 4
                && runFlow.CurrentDay == 4
                && runFlow.Phase == DungeonRunPhase.Growth
                && mitigated
                && reducedRumor == rulesProvider
                    .CoreSessionRules.MaximumRumorMitigation
                && renownCost == rulesProvider
                    .CoreSessionRules.MaximumRumorRenownCost
                && !externalFailure.IsFailure
                && external.HostileRumor == 0f
                && runVariables.RunSeed == 23
                && runVariables.CurrentDay == 4
                && debug.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)
                && debug.IsDebugModified
                && services.IsAdvertisingEnabled(ServiceCategory.Dining)
                && capturedAll
                && presentationMapped;
        }
        finally
        {
            debug?.Dispose();
            external?.Dispose();
            runFlow?.Dispose();
            experience?.Dispose();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static IDungeonSaveSection CreateDependency(
        string sectionId,
        DungeonSaveRestorePhase restorePhase) =>
        new RequiredDependencyStubSection(sectionId, restorePhase);

    private static DungeonSaveSectionEnvelope CreateEnvelope<TPayload>(
        IDungeonSaveSection section,
        TPayload payload)
    {
        return new DungeonSaveSectionEnvelope
        {
            sectionId = section.SectionId,
            sectionVersion = section.SectionVersion,
            restorePhase = section.RestorePhase,
            optional = false,
            payloadJson = JsonUtility.ToJson(payload)
        };
    }

    private static DungeonSaveSectionEnvelope CreateRawEnvelope(
        IDungeonSaveSection section,
        string payloadJson)
    {
        return new DungeonSaveSectionEnvelope
        {
            sectionId = section.SectionId,
            sectionVersion = section.SectionVersion,
            restorePhase = section.RestorePhase,
            optional = false,
            payloadJson = payloadJson
        };
    }

    private static bool VerifyRestoreBoundary(
        IDungeonSaveSection section,
        string canonicalJson,
        string invalidJson,
        Func<int> restoreCount)
    {
        if (!HasStrictContracts(section))
        {
            return false;
        }

        DungeonGameRestoreReport validReport = new();
        section.Restore(canonicalJson, section.SectionVersion, validReport);
        if (!validReport.Success || restoreCount() != 1)
        {
            return false;
        }

        int beforeInvalid = restoreCount();
        DungeonGameRestoreReport invalidReport = new();
        bool rejected = false;
        try
        {
            section.Restore(
                invalidJson,
                section.SectionVersion,
                invalidReport);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        return (rejected || !invalidReport.Success)
            && restoreCount() == beforeInvalid;
    }

    private static bool VerifyStrictRestoreBoundary(
        IDungeonSaveSection section,
        string canonicalJson,
        string invalidJson,
        string lateDiscardJson,
        Func<int> restoreCount)
    {
        if (!HasStrictContracts(section)
            || section is not IDungeonStagedSaveSection staged)
        {
            return false;
        }

        int initialCount = restoreCount();
        string initialState = section.Capture();
        DungeonGameRestoreReport stageReport = new();
        IDungeonSaveRestoreStage stage = staged.StageRestore(
            canonicalJson,
            section.SectionVersion,
            stageReport);
        if (!stageReport.Success
            || restoreCount() != initialCount
            || section.Capture() != initialState)
        {
            return false;
        }

        stage.Commit(stageReport);
        if (!stageReport.Success
            || restoreCount() != initialCount + 1
            || section.Capture() != canonicalJson)
        {
            return false;
        }

        int committedCount = restoreCount();
        string committedState = section.Capture();
        string legacyPayload = WithPayloadVersion(
            canonicalJson,
            section.SectionVersion,
            section.SectionVersion - 1);
        if (!RejectsStrictRestore(
                section,
                canonicalJson,
                section.SectionVersion - 1)
            || !RejectsStrictRestore(
                section,
                legacyPayload,
                section.SectionVersion)
            || !RejectsStrictRestore(
                section,
                invalidJson,
                section.SectionVersion)
            || !RejectsStrictRestore(
                section,
                string.Empty,
                section.SectionVersion)
            || restoreCount() != committedCount
            || section.Capture() != committedState)
        {
            return false;
        }

        DungeonGameRestoreReport discardReport = new();
        staged.StageRestore(
            lateDiscardJson,
            section.SectionVersion,
            discardReport);
        return discardReport.Success
            && restoreCount() == committedCount
            && section.Capture() == committedState;
    }

    private static bool RejectsStrictRestore(
        IDungeonSaveSection section,
        string payloadJson,
        int sectionVersion)
    {
        try
        {
            ((IDungeonStagedSaveSection)section).StageRestore(
                payloadJson,
                sectionVersion,
                new DungeonGameRestoreReport());
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string WithPayloadVersion(
        string payloadJson,
        int currentVersion,
        int replacementVersion)
    {
        string current = $"\"version\":{currentVersion}";
        if (string.IsNullOrWhiteSpace(payloadJson)
            || !payloadJson.Contains(current, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Canonical strict-save fixture payload has no version field.");
        }
        return payloadJson.Replace(
            current,
            $"\"version\":{replacementVersion}");
    }

    private static bool HasStrictContracts(object section) =>
        section is IDungeonSaveSectionPreflight
        && section is IDungeonStagedSaveSection
        && section is IDungeonRollbackFreeSaveSection
        && section is not IOptionalDungeonSaveSection
        && section is not IDungeonStagedOptionalSaveSection;

    private sealed class RequiredDependencyStubSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        internal RequiredDependencyStubSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            SectionId = sectionId
                ?? throw new ArgumentNullException(nameof(sectionId));
            RestorePhase = restorePhase;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError(
                    $"Invalid prerequisite payload for '{SectionId}'.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ => { });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class FinalFailingSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private const string Id = "batch-a.injected-final-failure";
        private readonly IReadOnlyList<string> dependencies;

        internal FinalFailingSection(IReadOnlyList<string> dependencies)
        {
            this.dependencies = dependencies
                ?? throw new ArgumentNullException(nameof(dependencies));
        }

        public bool WasCommitted { get; private set; }
        public string SectionId => Id;
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn => dependencies;
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Injected final section payload is invalid.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                commitReport =>
                {
                    WasCommitted = true;
                    commitReport.AddError(
                        "Injected Batch A final-section failure.");
                });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private static void Verify(
        string label,
        Func<bool> scenario,
        ICollection<string> failures)
    {
        try
        {
            if (!scenario())
            {
                failures.Add(label);
            }
        }
        catch (Exception exception)
        {
            Exception root = exception.GetBaseException();
            failures.Add(
                $"{label}: {root}");
        }
    }

    private static ICoreSessionRulesProvider LoadRulesProvider()
    {
        CoreSessionRulesSO rules =
            AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
                "Assets/Resources/SO/Content/CoreSessionRules.asset")
            ?? throw new InvalidOperationException(
                "Authored CoreSessionRules asset is missing.");
        return new FixedCoreSessionRulesProvider(
            rules.CreateRuntimeDefinition());
    }

    private sealed class FixedCoreSessionRulesProvider :
        ICoreSessionRulesProvider
    {
        internal FixedCoreSessionRulesProvider(
            CoreSessionRulesDefinition rules)
        {
            CoreSessionRules = rules
                ?? throw new ArgumentNullException(nameof(rules));
        }

        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    private sealed class ExperienceRuntimeFake :
        IExperiencePacingRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private ExperiencePacingAggregateState localState = new();

        internal ExperienceRuntimeFake(
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
        }

        private ExperiencePacingAggregateState State
        {
            get => store != null
                ? store.GetOrCreate(
                    () => new ExperiencePacingAggregateState())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public int CurrentDay => State.CurrentDay;
        public bool AllowsRandomInvasion => false;
        public int MaximumConcurrentExternalProblems => 0;
        public bool IsRehearsalActive => false;
        public int ActiveRehearsalDay => State.ActiveRehearsalDay;
        public void AdvanceToDay(int day) => State.CurrentDay = day;
        public bool TryBeginRehearsal(int day, out RehearsalInvasionProfile profile)
        {
            profile = default;
            return false;
        }
        public void ResolveRehearsal() { }
        public bool CanStartExteriorIncident(ExteriorIncidentKind kind) => false;
        public void MarkExteriorIncidentStarted(ExteriorIncidentKind kind) { }
        public DungeonExperiencePacingSaveData Capture() => new()
        {
            currentDay = State.CurrentDay,
            scheduledRehearsalMask = State.ScheduledRehearsalMask,
            completedRehearsalMask = State.CompletedRehearsalMask,
            activeRehearsalDay = State.ActiveRehearsalDay,
            introducedConcepts = State.IntroducedConcepts
                .OrderBy(value => value)
                .Select(value => (int)value)
                .ToList()
        };
        public ExperiencePacingAggregateState PrepareRestoreCandidate(
            DungeonExperiencePacingSaveData data)
        {
            ExperiencePacingAggregateState restored = new()
            {
                CurrentDay = data.currentDay,
                ScheduledRehearsalMask = data.scheduledRehearsalMask,
                CompletedRehearsalMask = data.completedRehearsalMask,
                ActiveRehearsalDay = data.activeRehearsalDay
            };
            foreach (int raw in data.introducedConcepts)
            {
                restored.IntroducedConcepts.Add(
                    (ExperienceEventConcept)raw);
            }
            return restored;
        }

        public void PublishRestoreCandidate(
            ExperiencePacingAggregateState candidate)
        {
            State = candidate;
            RestoreCount++;
        }
    }

    private sealed class ExternalRuntimeFake : IExternalInfluenceRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonExternalInfluenceSaveData localState = new();

        internal ExternalRuntimeFake(
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
        }

        private DungeonExternalInfluenceSaveData State
        {
            get => store != null
                ? store.GetOrCreate(
                    () => new DungeonExternalInfluenceSaveData())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public float Renown => State.renown;
        public float Dread => State.dread;
        public float HostileRumor => State.hostileRumor;
        public float EcologyPressure => State.ecologyPressure;
        public float ScoutingLabor => State.scoutingLabor;
        public bool IsDreadDefenseArmed => State.dreadDefenseArmed;
        public bool IsDreadDefenseActive => State.dreadDefenseActive;
        public EcologyRaidSnapshot GetEcologyRaidSnapshot() => default;
        public void AddRenown(float amount, string source) { }
        public void AddDread(float amount, string source) { }
        public void AddHostileRumor(float amount, string source) { }
        public void AddEcologyPressure(float amount, string source) { }
        public void AddScoutingLabor(float amount) { }
        public bool TryMitigateHostileRumor(
            HostileRumorMitigationMethod method,
            out float reducedAmount,
            out int cost,
            out DomainFailure failure)
        {
            reducedAmount = 0f;
            cost = 0;
            failure = DomainFailure.None;
            return false;
        }
        public bool TryArmDreadDefense(out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }
        public bool BeginInvasionDread(bool boss) => false;
        public float GetMoveSpeedMultiplier(string characterId) => 1f;
        public float GetAttackSpeedMultiplier(string characterId) => 1f;
        public bool IsIntelUnlocked(string siteId) => false;
        public bool TryUnlockIntel(
            string siteId,
            ExpeditionIntelPaymentMethod payment,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }
        public bool TryUnlockIntelForActiveSite(
            string siteId,
            bool fixedBoss,
            int expiresDay,
            int currentDay,
            ExpeditionIntelPaymentMethod payment,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }
        public DungeonExternalInfluenceSaveData Capture() => State;
        public ExternalInfluenceRestoreCandidate BuildRestoreCandidate(
            DungeonExternalInfluenceSaveData saveData) =>
            new ExternalInfluenceRestoreCandidate(
                saveData,
                saveData.intelUnlockedSiteIds,
                saveData.dreadAffectedIntruderIds);
        public void PublishRestoreCandidate(
            ExternalInfluenceRestoreCandidate candidate)
        {
            State = candidate.Data;
            RestoreCount++;
        }
        public void Reset() => State = new DungeonExternalInfluenceSaveData();
    }

    private sealed class RunFlowRuntimeFake :
        IDungeonRunFlowRuntime,
        IDungeonRunFlowRestorePublisher
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonRunFlowAggregateState localState = new();

        internal RunFlowRuntimeFake(
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
        }

        private DungeonRunFlowAggregateState State
        {
            get => store != null
                ? store.GetOrCreate(() => new DungeonRunFlowAggregateState())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public DungeonRunPhase Phase => State.Phase;
        public DungeonRunOutcome Outcome => State.Outcome;
        public int CurrentDay => State.CurrentDay;
        public int BossCycle => State.BossCycle;
        public bool IsBossArmed => State.BossArmed;
        public bool IsBossActive => State.BossActive;

        internal DungeonRunFlowSaveData CaptureState() => new()
        {
            phase = State.Phase,
            outcome = State.Outcome,
            currentDay = State.CurrentDay,
            bossArmed = State.BossArmed,
            bossActive = State.BossActive,
            bossCycle = State.BossCycle
        };

        public void RestoreState(
            DungeonRunPhase phase,
            DungeonRunOutcome outcome,
            int currentDay,
            bool bossArmed,
            bool bossActive,
            int bossCycle)
        {
            State = new DungeonRunFlowAggregateState
            {
                Phase = phase,
                Outcome = outcome,
                CurrentDay = currentDay,
                BossArmed = bossArmed,
                BossActive = bossActive,
                BossCycle = bossCycle
            };
            RestoreCount++;
        }

        public void PublishRestoreState(
            DungeonRunFlowAggregateState candidate)
        {
            State = candidate;
            RestoreCount++;
        }
    }

    private sealed class DebugModeFake : IDungeonDebugModeService
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonDebugRunSaveData localState = new();

        internal DebugModeFake(DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
        }

        private DungeonDebugRunSaveData State
        {
            get => store != null
                ? store.GetOrCreate(() => new DungeonDebugRunSaveData())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public bool IsDeveloperModeEnabled => true;
        public bool IsDebugModified => State.debugModified;
        public DungeonDebugOverlayScope OverlayScope =>
            DungeonDebugOverlayScope.SelectedOnly;
        public IReadOnlyList<DungeonDebugCommandHistorySaveData> RecentCommands =>
            State.recentCommands;
        public event Action StateChanged
        {
            add { }
            remove { }
        }
        public bool IsCheatEnabled(DungeonDebugCheat cheat) => false;
        public bool IsOverlayEnabled(DungeonDebugOverlayKind overlay) => false;
        public void SetCheat(DungeonDebugCheat cheat, bool enabled) { }
        public void SetOverlay(DungeonDebugOverlayKind overlay, bool enabled) { }
        public void SetOverlayScope(DungeonDebugOverlayScope scope) { }
        public void MarkMutation(
            string commandId,
            string target,
            DungeonDebugCommandResult result) { }
        public DungeonDebugRunSaveData Capture() => State;
        public DungeonDebugRestoreCandidate PrepareRestoreCandidate(
            DungeonDebugRunSaveData data)
        {
            DungeonDebugRunSaveData payload =
                JsonUtility.FromJson<DungeonDebugRunSaveData>(
                    JsonUtility.ToJson(data));
            DungeonDebugModeState candidateState = new()
            {
                DebugModified = payload.debugModified,
                OverlayScope = DungeonDebugOverlayScope.SelectedOnly
            };
            foreach (DungeonDebugCommandHistorySaveData entry in
                     payload.recentCommands)
            {
                candidateState.RecentCommands.Add(entry);
            }
            return new DungeonDebugRestoreCandidate(
                candidateState,
                payload);
        }
        public void PublishRestoreCandidate(
            DungeonDebugRestoreCandidate candidate)
        {
            State = candidate.Payload;
            if (store == null)
            {
                RestoreCount++;
            }
        }
        public void ResetTransientState() { }
    }

    private sealed class ServiceRuntimeFake : IServiceSessionRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private ServiceSessionAggregate localState = new();

        internal ServiceRuntimeFake(
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
        }

        private ServiceSessionAggregate State
        {
            get => store != null
                ? store.GetOrCreate(() => new ServiceSessionAggregate())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public int Version => 0;
        public IReadOnlyList<ServiceSessionSnapshot> ActiveSessions =>
            Array.Empty<ServiceSessionSnapshot>();
        public ServiceAvailabilitySnapshot GetAvailability(
            ServiceCategory category) => new();
        public bool ShouldAcceptDemand(ServiceCategory category) => false;
        public bool ShouldRecordUnservedDemand(
            ServiceCategory category,
            bool demandWasAdvertised) => false;
        public bool IsAdvertisingEnabled(ServiceCategory category) => false;
        public void SetAdvertisingEnabled(ServiceCategory category, bool enabled) { }
        public ServiceHubSnapshot GetHubSnapshot(BuildableObject hub) => new();
        public ServiceModeChangeResult SetMode(
            BuildableObject hub,
            ServiceOperationMode mode) => new();
        public ServiceModeChangeResult SwitchToDirect(BuildableObject hub) => new();
        public bool TryBeginSession(
            ServiceSessionRequest request,
            out ServiceSessionSnapshot session,
            out DomainFailure failure)
        {
            session = null;
            failure = DomainFailure.None;
            return false;
        }
        public bool TrySetStage(
            string sessionId,
            ServiceSessionStage stage,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }
        public bool TryCompleteSession(
            string sessionId,
            out ServiceSessionSnapshot completed,
            out DomainFailure failure)
        {
            completed = null;
            failure = DomainFailure.None;
            return false;
        }
        public bool CancelSession(string sessionId, string reason) => false;
        public ServiceRoomsSaveData Capture() => State.Capture();
        public ServiceRoomsRestoreCandidate PrepareRestoreCandidate(
            ServiceRoomsSaveData saveData)
        {
            ServiceSessionAggregate restored =
                ServiceSessionAggregate.CreateRestored(
                    saveData,
                    unchecked(State.Version + 1));
            return new ServiceRoomsRestoreCandidate(restored);
        }
        public void PublishRestoreCandidate(ServiceRoomsRestoreCandidate candidate)
        {
            State = candidate.Aggregate;
            RestoreCount++;
        }
    }

    private sealed class EmptyServiceProcessCatalog : IServiceProcessCatalog
    {
        internal static readonly EmptyServiceProcessCatalog Instance = new();
        public IReadOnlyList<ServiceProcessSO> All =>
            Array.Empty<ServiceProcessSO>();
        public bool TryGet(string processId, out ServiceProcessSO process)
        {
            process = null;
            return false;
        }
        public ServiceProcessSO Require(string processId) =>
            throw new KeyNotFoundException(processId);
    }

    private sealed class EmptyRunVariableCatalog : IRunVariableDefinitionCatalog
    {
        internal static readonly EmptyRunVariableCatalog Instance = new();
        public IReadOnlyCollection<RunVariableDefinition> All =>
            Array.Empty<RunVariableDefinition>();
        public RunVariableDefinition Get(string id) => null;
        public RunVariableDefinition Require(string id) =>
            throw new KeyNotFoundException(id);
        public IReadOnlyList<RunVariableDefinition> GetByCategory(
            RunVariableCategory category) => Array.Empty<RunVariableDefinition>();
    }

    private sealed class EmptyDoctrineCatalog : IOwnerDoctrineDefinitionCatalog
    {
        internal static readonly EmptyDoctrineCatalog Instance = new();
        public IReadOnlyCollection<OwnerDoctrineDefinition> All =>
            Array.Empty<OwnerDoctrineDefinition>();
        public OwnerDoctrineDefinition Get(string id) => null;
        public OwnerDoctrineDefinition Require(string id) =>
            throw new KeyNotFoundException(id);
        public OwnerDoctrineDefinition ResolveFor(CharacterSO owner) => null;
        public OwnerDoctrineDefinition ResolveForSpecies(string speciesTag) => null;
    }

    private sealed class EmptyOwnerRunDataProvider : IOwnerRunDataProvider
    {
        internal static readonly EmptyOwnerRunDataProvider Instance = new();
        public CharacterSO SelectedOwnerData => null;
    }

    private sealed class DisabledRunStartVariableSelector :
        IRunStartVariableSelector
    {
        internal static readonly DisabledRunStartVariableSelector Instance =
            new();

        public RunStartVariableSnapshot Create(
            int seed,
            CharacterSO ownerData,
            DungeonDifficulty difficulty,
            DungeonSurvivalPressure survivalPressure =
                DungeonSurvivalPressure.Standard) =>
            throw new InvalidOperationException(
                "The atomic restore scenario must not start a new run.");
    }

    private sealed class EmptyOwnerRunManagerProvider :
        IOwnerRunManagerProvider
    {
        internal static readonly EmptyOwnerRunManagerProvider Instance = new();

        public bool TryGetManager(out OwnerRunManager manager)
        {
            manager = null;
            return false;
        }
    }

    private sealed class IntegratedGameClock : IGameClock
    {
        internal static readonly IntegratedGameClock Instance = new();
        public float DeltaTime => 0.1f;
        public float Time => 10f;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class IntegratedMoneyAccount : IGameMoneyAccount
    {
        internal IntegratedMoneyAccount(int balance)
        {
            Balance = balance;
        }

        public int Balance { get; private set; }
        public bool CanSpend(int amount) => amount >= 0 && Balance >= amount;
        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);
        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (!CanSpend(amount))
            {
                reason = "insufficient";
                return false;
            }

            Balance -= amount;
            reason = string.Empty;
            return true;
        }
        public void Add(int amount) => Balance += amount;
        public void Add(int amount, EconomyTransactionContext context) =>
            Add(amount);
        public void SetBalance(
            int amount,
            EconomyTransactionContext context) =>
            Balance = amount;
    }

    private sealed class IntegratedSettings : IDungeonUserSettingsService
    {
        public DungeonUserSettingsData Current { get; } =
            new DungeonUserSettingsData { developerMode = true };
        public string SettingsPath => string.Empty;
        public string LastError => string.Empty;
        public event Action Changed;

        public void Update(Action<DungeonUserSettingsData> change)
        {
            change?.Invoke(Current);
            Changed?.Invoke();
        }

        public void ResetDefaults() =>
            Update(current => current.developerMode = false);
        public void ApplyCurrent() => Changed?.Invoke();
    }

    private sealed class IntegratedSessionStateProvider :
        IGameSessionStateProvider
    {
        private readonly GameSessionState state = new();

        internal IntegratedSessionStateProvider()
        {
            state.day.Initialize(4);
            state.hour.Initialize(7);
        }

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }
    }

    public class DefaultInterfaceProxy : DispatchProxy
    {
        public DefaultInterfaceProxy()
        {
        }

        internal static TContract Create<TContract>()
            where TContract : class =>
            DispatchProxy.Create<TContract, DefaultInterfaceProxy>();

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] arguments)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (parameters[index].IsOut || parameterType.IsByRef)
                {
                    Type valueType = parameterType.IsByRef
                        ? parameterType.GetElementType()
                        : parameterType;
                    arguments[index] = CreateDefault(valueType);
                }
            }

            return CreateDefault(targetMethod.ReturnType);
        }

        private static object CreateDefault(Type type)
        {
            if (type == null || type == typeof(void))
            {
                return null;
            }

            if (type.IsArray)
            {
                return Array.CreateInstance(type.GetElementType(), 0);
            }

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(IEnumerable<>)
                    || definition == typeof(IReadOnlyCollection<>)
                    || definition == typeof(IReadOnlyList<>)
                    || definition == typeof(ICollection<>)
                    || definition == typeof(IList<>))
                {
                    return Array.CreateInstance(
                        type.GetGenericArguments()[0],
                        0);
                }
            }

            return type.IsValueType
                ? Activator.CreateInstance(type)
                : null;
        }
    }
}
#endif
