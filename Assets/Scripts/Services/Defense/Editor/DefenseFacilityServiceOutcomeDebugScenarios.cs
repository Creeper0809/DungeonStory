using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Exercises the real UI command and facility runtime without starting Unity
/// play mode. The physical and ledger ports are deterministic fault injectors.
/// </summary>
public static class DefenseFacilityServiceOutcomeDebugScenarios
{
    public static void RunR28ForAutomation()
    {
        BuildingSO launcherDefinition = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/DefenseWallLauncher.asset");
        BuildingSO controlDefinition = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/DefenseControlDesk.asset");
        Require(launcherDefinition?.id == 1805
            && launcherDefinition.Defense.requiresAutomaticControl,
            "The current wall launcher lost its automatic-control requirement.");
        Require(!new DefenseFacilityData().requiresAutomaticControl
            && !JsonUtility.FromJson<DefenseFacilityData>("{\"enabled\":true}")
                .requiresAutomaticControl,
            "Existing definitions without the new policy no longer default to false.");
        string launcherBefore = UnityEditor.EditorJsonUtility.ToJson(launcherDefinition);
        string abilityBefore = JsonUtility.ToJson(
            launcherDefinition.GetAbility<BuildingDefenseAbility>());
        BuildingDefenseAbility restoredAbility = JsonUtility.FromJson<BuildingDefenseAbility>(abilityBefore);
        Require(restoredAbility.settings.requiresAutomaticControl
            && JsonUtility.ToJson(restoredAbility) == abilityBefore,
            "Building defense ability JSON did not preserve the policy and existing settings.");

        using DefenseFacilityDebugScenarios.DefenseScenarioWorld scene = new();
        BuildingSO copiedDefinition = UnityEngine.Object.Instantiate(launcherDefinition);
        scene.TrackScriptableObject(copiedDefinition);
        copiedDefinition.id = 91805;
        BuildingSO globalControlDefinition = UnityEngine.Object.Instantiate(controlDefinition);
        scene.TrackScriptableObject(globalControlDefinition);
        globalControlDefinition.id = 91801;
        globalControlDefinition.ConfigureGameplayExecution(
            globalControlDefinition.UseClassification, ResearchFacilityCommandKind.DefenseControl);
        DefenseFacility launcher = scene.PlaceDefense(launcherDefinition, new Vector2Int(2, 0));
        DefenseFacility copiedLauncher = scene.PlaceDefense(copiedDefinition, new Vector2Int(4, 0));
        DefenseFacility ordinary = scene.PlaceDefense("P1_SpikeTrap", new Vector2Int(6, 0));
        DefenseFacility localControl = scene.PlaceDefense(controlDefinition, new Vector2Int(3, 0));
        DefenseFacility globalControl = scene.PlaceDefense(globalControlDefinition, new Vector2Int(20, 0));
        Require(!ordinary.Defense.requiresAutomaticControl,
            "An existing ordinary defense facility unexpectedly requires automatic control.");
        string ordinaryBefore = UnityEditor.EditorJsonUtility.ToJson(ordinary.BuildingData);
        string launcherId = launcher.RequirePersistentInstanceId().Value;
        string copiedId = copiedLauncher.RequirePersistentInstanceId().Value;
        FixtureWorld world = new(launcher, copiedLauncher, ordinary);
        FixturePower power = new();
        DefenseFacilityNetworkRuntime network = new(world, power);
        FixturePhysical physical = new();
        DefenseFacilityRuntime CreateRuntime(DungeonRuntimeAggregateRootStore root) => new(
            UnavailableEquipmentPhysicalItemGateway.Instance, physical, new FixtureInputOwner(),
            CharacterAiEditorTestDependencies.GameClock, CharacterAiEditorTestDependencies.GameEvents,
            power, network, new FacilityCapabilityQuery(world), root,
            new MigratedProducerCombatDefenseEntryE2EDebugScenarios.R30DefensePreparationBenefits(1f));
        DefenseFacilityRuntime runtime = CreateRuntime(new DungeonRuntimeAggregateRootStore());

        foreach (DefenseFacility facility in new[] { launcher, copiedLauncher })
        {
            Require(!network.HasAutomaticControl(facility)
                && !runtime.CanActivate(facility, null, DefenseTriggerTiming.OnEnter, out DomainFailure failure)
                && failure.Code == FailureCode.DefenseAutomaticControlUnavailable,
                "A control-required definition activated without control: " + facility.BuildingData.id);
        }
        Require(network.HasAutomaticControl(ordinary)
            && runtime.CanActivate(ordinary, null, DefenseTriggerTiming.OnEnter, out _),
            "A false-policy facility changed behavior without a control link.");

        world.SetFacilities(launcher, copiedLauncher, ordinary, localControl);
        foreach (DefenseFacility facility in new[] { launcher, copiedLauncher })
        {
            Require(network.GetSnapshot(facility).ControlDesk == localControl
                && network.HasAutomaticControl(facility)
                && runtime.CanActivate(facility, null, DefenseTriggerTiming.OnEnter, out _),
                "The local control link did not enable a required definition.");
        }
        power.Powered = false;
        Require(!network.HasAutomaticControl(launcher)
            && !runtime.CanActivate(launcher, null, DefenseTriggerTiming.OnEnter, out DomainFailure noPower)
            && noPower.Code == FailureCode.DefenseAutomaticControlUnavailable,
            "An unpowered local control link was treated as available.");

        // 전역 DefenseControl 조회는 기존대로 지역 거리와 전력 조회에 종속되지 않는다.
        world.SetFacilities(launcher, copiedLauncher, ordinary, globalControl);
        foreach (DefenseFacility facility in new[] { launcher, copiedLauncher })
        {
            Require(!network.HasAutomaticControl(facility)
                && runtime.CanActivate(facility, null, DefenseTriggerTiming.OnEnter, out _),
                "The existing global DefenseControl path lost its independent meaning.");
        }

        string savedJson = JsonUtility.ToJson(runtime.CaptureState());
        DefenseFacilitySaveData saved = JsonUtility.FromJson<DefenseFacilitySaveData>(savedJson);
        Require(saved.facilities.Any(record => record.facilityPersistentId == launcherId)
            && saved.facilities.Any(record => record.facilityPersistentId == copiedId)
            && !savedJson.Contains("requiresAutomaticControl"),
            "Runtime persistence changed identity or took ownership of the definition policy.");
        DefenseFacilityRuntime restored = CreateRuntime(new DungeonRuntimeAggregateRootStore());
        restored.PublishRestoreState(restored.PrepareRestoreState(saved));
        Require(JsonUtility.ToJson(restored.CaptureState()) == savedJson
            && launcher.RequirePersistentInstanceId().Value == launcherId
            && copiedLauncher.RequirePersistentInstanceId().Value == copiedId,
            "JSON restore changed existing defense state or persistent instance identity.");
        world.SetFacilities(launcher, copiedLauncher, ordinary);
        Require(!restored.CanActivate(launcher, null, DefenseTriggerTiming.OnEnter, out DomainFailure restoredFailure)
            && restoredFailure.Code == FailureCode.DefenseAutomaticControlUnavailable
            && restored.CanActivate(ordinary, null, DefenseTriggerTiming.OnEnter, out _),
            "Restored facilities did not continue to use their definition policies.");
        Require(physical.CommitCount == 0
            && UnityEditor.EditorJsonUtility.ToJson(launcherDefinition) == launcherBefore
            && UnityEditor.EditorJsonUtility.ToJson(ordinary.BuildingData) == ordinaryBefore
            && JsonUtility.ToJson(launcherDefinition.GetAbility<BuildingDefenseAbility>()) == abilityBefore,
            "Control queries changed physical supply or authored ability settings.");
        Debug.Log("R28_DEFENSE_AUTOMATIC_CONTROL_POLICY=PASS");
    }

    public static void RunObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode && !string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output)
            && Path.GetFullPath(output).StartsWith(qa, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(output),
            "Requires fresh project-local isolated evidence.");
        string probe = File.ReadAllText(Path.Combine(Directory.GetParent(Path.GetFullPath(output)).FullName, "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE") && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true") && probe.Contains("inJob=true"), "Missing isolation probe.");
        RunOrThrow();
        VerifyUiToActualPhysicalDisposition();
        VerifyActualServiceObserver(false);
        VerifyActualServiceObserver(true);
        Debug.Log("B93_DEFENSE_SERVICE_OBSERVER_FOCUSED=PASS");
    }

    private static void VerifyActualServiceObserver(bool jammed)
    {
        using DefenseFacilityDebugScenarios.DefenseScenarioWorld scene = new();
        var facility = scene.PlaceDefense("P1_SpikeTrap", new Vector2Int(2, 0));
        var world = new FixtureWorld(facility);
        var root = new DungeonRuntimeAggregateRootStore();
        using var physical = new DefenseFacilityPhysicalTransactionFixture.FixtureGateway(EditorItemCatalogFactory.Create(), root);
        var binding = DefenseServiceFactMapping.CreateBinding(physical.Registry, physical.Ledger, physical.Recorder,
            new GameplayDomainFactCommitGroup(physical.Recorder, physical.Ledger));
        var connection = binding.Connect(physical.Facts);
        ExpectInvalid(() => binding.Connect(physical.Facts));
        connection.Dispose();
        using var connected = binding.Connect(physical.Facts);
        var fault = new FaultServiceStaging(physical.Facts);
        var journal = new DefenseServicePublicationRuntime(fault, physical.Facts);
        var runtime = NewRuntime(root, world, physical, journal);
        var ui = NewUi(root, world, runtime);
        if (jammed) SetJammed(runtime, facility);
        else runtime.GetSnapshot(facility);
        string facilityId = facility.RequirePersistentInstanceId().Value;
        int quantity = jammed ? 1 : facility.Defense.supplyCapacity - facility.Defense.initialSupply;
        string stack = physical.Add(jammed ? "material:iron-ingot" : facility.Defense.supplyItemId, quantity,
            jammed ? DefenseFacilityInputOwnerAuthority.BuildMaintenanceDestinationId(facilityId)
                : DefenseFacilityInputOwnerAuthority.BuildSupplyDestinationId(facilityId));
        string before = JsonUtility.ToJson(runtime.CaptureState());
        fault.RejectPhase = DefenseFacilityServiceOutcomePhase.Requested;
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded && fault.RejectPhase == null && physical.Quantity(stack) == quantity
            && journal.Capture().observations.Count == 0 && before == JsonUtility.ToJson(runtime.CaptureState())
            && physical.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
            "Actual request rejection altered gameplay or canonical state; failure=" + journal.LastFailure);
        fault.RejectPhase = DefenseFacilityServiceOutcomePhase.Completed;
        var rejectedTerminal = ui.RequestFacilityService(facility.GetInstanceID());
        Require(!rejectedTerminal.Succeeded && fault.RejectPhase == null && physical.Quantity(stack) == 0
            && journal.Capture().observations.Count == 1
            && runtime.CaptureState().facilities.Single().pendingService.phase == DefenseFacilityServicePendingPhase.Requested,
            "Rejected terminal lost physical intent or did not roll back facility state; result=" + rejectedTerminal.Succeeded
            + ";message=" + rejectedTerminal.Message + ";remaining=" + physical.Quantity(stack)
            + ";failure=" + journal.LastFailure + ";owner=" + JsonUtility.ToJson(runtime.CaptureState())
            + ";journal=" + JsonUtility.ToJson(journal.Capture()));
        DefenseServiceFactPreflight.ValidateOwners(journal.Capture(), runtime.CaptureState());
        DefenseServiceFactPreflight.ValidateJournal(journal.Capture(), physical.Ledger.CaptureGameplayOutcomes());
        fault.HideCompleted = true;
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded && physical.Quantity(stack) == 0,
            "Lost terminal ACK did not defer completion.");
        var owner = JsonUtility.FromJson<DefenseFacilitySaveData>(JsonUtility.ToJson(runtime.CaptureState()));
        string json = DungeonCharacterWorldSaveCodec.SerializeFields(journal.Capture());
        DefenseServicePublicationSaveSection.ValidateJson(json);
        var saved = DungeonSaveJson.Deserialize<DefenseServicePublicationSaveData>(json);
        var canonical = physical.Ledger.CaptureGameplayOutcomes();
        Require(saved.observations.Count == 2 && saved.observations.Last().pending
            && canonical.exactOutcomes.Count == 3 && saved.observations.All(row =>
                row.fact.absoluteDay == CharacterAiEditorTestDependencies.GameCalendar.Day),
            "Real commit did not retain exact request, physical child and terminal result.");
        DefenseServiceFactPreflight.ValidateOwners(saved, owner);
        DefenseServiceFactPreflight.ValidateJournal(saved, canonical);
        var badOwner = JsonUtility.FromJson<DefenseFacilitySaveData>(JsonUtility.ToJson(owner));
        badOwner.facilities.Single().pendingService.requestAcknowledged = false;
        ExpectInvalid(() => DefenseServiceFactPreflight.ValidateOwners(saved, badOwner));
        var terminal = canonical.exactOutcomes.Single(row => row.operationId == saved.observations.Last().OperationId);
        var child = canonical.exactOutcomes.Single(row => row.sequence == terminal.parentSequence);
        Require(child.producerId == DefenseServiceFactMapping.PhysicalChildKey(saved.observations.Last().fact).ProducerId
            && terminal.parentRunId == child.runId && child.sequence < terminal.sequence,
            "Terminal did not reference the actual physical child.");
        var bad = DungeonSaveJson.Deserialize<DefenseServicePublicationSaveData>(json);
        bad.observations.Last().fact.inputQuantity++;
        ExpectInvalid(() => DefenseServiceFactPreflight.ValidateJournal(bad, canonical));
        bad = DungeonSaveJson.Deserialize<DefenseServicePublicationSaveData>(json);
        bad.observations.RemoveAt(1);
        ExpectInvalid(() => DefenseServiceFactPreflight.ValidateOwners(bad, owner));
        var raw = JObject.Parse(json);
        ((JObject)raw["observations"][0]["fact"]).Remove("absoluteDay");
        ExpectInvalid(() => DefenseServicePublicationSaveSection.ValidateJson(raw.ToString()));

        // Fresh ledger, dispatcher, journal and facility owner. Physical owner is deliberately retained.
        var ledger = new GameplayOutcomeLedger(physical.Registry, new GameplayOutcomeBufferLimits(64, 4, 256));
        var candidate = ledger.PrepareGameplayOutcomeRestore(canonical);
        ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(ledger, physical.Registry, new GameEventBus());
        using var facts = new DomainFactDispatcher();
        var pendingFact = new DefenseServiceChangedFact(saved.observations.Last().fact);
        Require(!facts.TryStage(pendingFact, out _, out _), "Missing observer accepted pending fact.");
        using var restoredConnection = DefenseServiceFactMapping.CreateBinding(physical.Registry, ledger, recorder,
            new GameplayDomainFactCommitGroup(recorder, ledger)).Connect(facts);
        var restoredJournal = new DefenseServicePublicationRuntime(facts, facts);
        restoredJournal.Restore(saved);
        var restoredRoot = new DungeonRuntimeAggregateRootStore();
        var restored = NewRuntime(restoredRoot, world, physical, restoredJournal);
        restored.PublishRestoreState(restored.PrepareRestoreState(owner));
        var restoredUi = NewUi(restoredRoot, world, restored);
        physical.FailNextAcknowledgement = true;
        Require(!restoredUi.RequestFacilityService(facility.GetInstanceID()).Succeeded,
            "Physical ACK failure did not remain retryable.");
        Require(restoredUi.RequestFacilityService(facility.GetInstanceID()).Succeeded && physical.Quantity(stack) == 0
            && restored.CaptureState().facilities.Single().nextServiceOperationSequence == 1
            && restoredJournal.Capture().observations.All(row => !row.pending)
            && ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 3
            && physical.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 3,
            "Fresh-owner replay repeated physical effects or native results.");
        var exact = ledger.CaptureGameplayOutcomes().exactOutcomes.Single(row => row.sequence == terminal.sequence);
        Require(exact.immutablePayloadHash == terminal.immutablePayloadHash && exact.parentSequence == child.sequence,
            "Replay changed immutable terminal identity or causation.");
        DefenseServiceFactPreflight.ValidateOwners(restoredJournal.Capture(), restored.CaptureState());
        DefenseServiceFactPreflight.ValidateJournal(restoredJournal.Capture(), ledger.CaptureGameplayOutcomes());
        if (jammed)
        {
            SetJammed(restored, facility);
            Require(!restoredUi.RequestFacilityService(facility.GetInstanceID()).Succeeded
                && restoredJournal.Capture().observations.Last().fact.phase == DefenseFacilityServiceOutcomePhase.FailedWithImpact
                && ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 5 && physical.Quantity(stack) == 0,
                "Missing maintenance input failed to record a real failed transition without a physical child.");
            DefenseServiceFactPreflight.ValidateOwners(restoredJournal.Capture(), restored.CaptureState());
            DefenseServiceFactPreflight.ValidateJournal(restoredJournal.Capture(), ledger.CaptureGameplayOutcomes());
        }
        Debug.Log("B93_DEFENSE_SERVICE_" + (jammed ? "MAINTENANCE" : "RELOAD") + "_RECOVERY=PASS");
    }

    private static void ExpectInvalid(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Newtonsoft.Json.JsonException)
        { rejected = true; }
        Require(rejected, "Invalid observer/save contract was accepted.");
    }

    private sealed class FaultServiceStaging : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        public DefenseFacilityServiceOutcomePhase? RejectPhase;
        public bool HideCompleted;
        public FaultServiceStaging(IDomainFactStagingPublisher inner) => this.inner = inner;
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out stage, out failure)) return false;
            stage = new Stage(this, stage, ((DefenseServiceChangedFact)(object)fact).Snapshot().phase);
            return true;
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly FaultServiceStaging owner;
            private readonly IDomainFactStage inner;
            private readonly DefenseFacilityServiceOutcomePhase phase;
            public Stage(FaultServiceStaging owner, IDomainFactStage inner, DefenseFacilityServiceOutcomePhase phase)
            { this.owner = owner; this.inner = inner; this.phase = phase; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                if (owner.RejectPhase == phase)
                { owner.RejectPhase = null; inner.Cancel(); return DomainFactDelivery.Reject("qa:defense-service-reject"); }
                var result = inner.Commit();
                if (owner.HideCompleted && phase == DefenseFacilityServiceOutcomePhase.Completed)
                {
                    owner.HideCompleted = false;
                    Require(result.Completed, "ACK fault requires a real canonical commit.");
                    return DomainFactDelivery.Uncertain("qa:defense-service-lost-ack");
                }
                return result;
            }
            public void Cancel() => inner.Cancel();
        }
    }

    public static void RunUI01FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_UI01_DEFENSE_SERVICE_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "UI-01 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "UI-01 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "UI-01 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "UI-01",
            ["passed"] = false,
            ["missingSupplyNoMutation"] = false,
            ["partialDeliveryAtomic"] = false,
            ["reloadCompletedWithPhysicalChild"] = false,
            ["jamClearedWithPhysicalChild"] = false,
            ["childThenParentFailureSaved"] = false,
            ["freshRestoreParentOnlyRetry"] = false,
            ["physicalAcknowledgementRetryExact"] = false,
            ["uiToActualPhysicalDispositionReached"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Models/Defense/Core/DefenseDomain.cs",
                "Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs",
                "Assets/Scripts/Services/Defense/DefenseFacilityPhysicalTransactionOutbox.cs",
                "Assets/Scripts/Services/Defense/Editor/DefenseFacilityServiceOutcomeDebugScenarios.cs",
                "Assets/Scripts/Services/Defense/Editor/DefenseFacilityPhysicalTransactionFixture.cs"
            }
        };

        try
        {
            RunOrThrow();
            DefenseFacilityPhysicalTransactionFixture.RunOrThrow();
            report["actualPhysicalPath"] = VerifyUiToActualPhysicalDisposition();
            report["uiToActualPhysicalDispositionReached"] = true;
            report["missingSupplyNoMutation"] = true;
            report["partialDeliveryAtomic"] = true;
            report["reloadCompletedWithPhysicalChild"] = true;
            report["jamClearedWithPhysicalChild"] = true;
            report["childThenParentFailureSaved"] = true;
            report["freshRestoreParentOnlyRetry"] = true;
            report["physicalAcknowledgementRetryExact"] = true;
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][UI-01] PASS missing=1; partial=1; "
                + "reload=1; clear-jam=1; parent-retry=1; physical-ack=1");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteCreateOnlyJson(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunOrThrow()
    {
        using DefenseFacilityDebugScenarios.DefenseScenarioWorld scene = new();
        DefenseFacility facility = scene.PlaceDefense(
            "P1_SpikeTrap", new Vector2Int(2, 0));
        FixtureWorld world = new(facility);
        FixturePhysical physical = new();
        FixtureOutcomes outcomes = new();
        DungeonRuntimeAggregateRootStore root = new();
        DefenseFacilityRuntime runtime = NewRuntime(root, world, physical, outcomes);
        SetJammed(runtime, facility);
        DefenseFeatureCommandService ui = NewUi(root, world, runtime);
        string maintenanceDestination =
            DefenseFacilityInputOwnerAuthority.BuildMaintenanceDestinationId(
                facility.RequirePersistentInstanceId().Value);
        physical.Add("material:iron-ingot", 1, maintenanceDestination);

        outcomes.RejectNextPrepare = true;
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded
            && physical.CommitCount == 0
            && outcomes.ResultCount == 0
            && outcomes.Journal.Capture().observations.Count == 0
            && runtime.CaptureState().facilities.Single().pendingService.phase
                == DefenseFacilityServicePendingPhase.None,
            "prepare rejection mutated the facility or physical child");

        Require(ui.RequestFacilityService(facility.GetInstanceID()).Succeeded,
            "UI clear-jam did not complete");
        DefenseFacilityRecordSaveData cleared = runtime.CaptureState()
            .facilities.Single();
        Require(cleared.nextServiceOperationSequence == 1
            && cleared.serviceRevision == 2L
            && cleared.pendingService.phase == DefenseFacilityServicePendingPhase.None
            && physical.CommitCount == 1
            && physical.AcknowledgementCount == 1
            && outcomes.Count(DefenseFacilityServiceOutcomePhase.Requested) == 1
            && outcomes.Count(DefenseFacilityServiceOutcomePhase.Completed) == 1
            && PhysicalChild(outcomes.LastCompleted).IsValid,
            "clear-jam lost its exact physical child or terminal parent");

        // A full-supply UI reload is a true no-op, not another service result.
        DefenseFacilitySaveData full = runtime.CaptureState();
        full.facilities.Single().supply = facility.Defense.supplyCapacity;
        runtime.PublishRestoreState(runtime.PrepareRestoreState(full));
        int resultCount = outcomes.ResultCount;
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded
            && outcomes.ResultCount == resultCount,
            "full-supply no-op emitted another parent result");

        // Start a second composition with the same physical and result ports.
        physical = new FixturePhysical();
        outcomes = new FixtureOutcomes { FailNextCompletedDelivery = true };
        root = new DungeonRuntimeAggregateRootStore();
        runtime = NewRuntime(root, world, physical, outcomes);
        SetJammed(runtime, facility);
        ui = NewUi(root, world, runtime);
        physical.Add("material:iron-ingot", 1, maintenanceDestination);
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded,
            "injected completed-parent delivery fault did not defer service");
        DefenseFacilitySaveData pending = JsonUtility.FromJson<DefenseFacilitySaveData>(
            JsonUtility.ToJson(runtime.CaptureState()));
        Require(pending.facilities.Single().pendingService.phase
                == DefenseFacilityServicePendingPhase.TerminalApplied
            && pending.facilities.Single().pendingMaintenance.phase
                == DefenseFacilityPhysicalCommitPhase.OutcomePublished
            && physical.CommitCount == 1
            && physical.AcknowledgementCount == 0
            && outcomes.Count(DefenseFacilityServiceOutcomePhase.Requested) == 1
            && outcomes.Count(DefenseFacilityServiceOutcomePhase.Completed) == 1
            && outcomes.Journal.Capture().observations.Count == 2
            && outcomes.Journal.Capture().observations.Single(value =>
                    value.fact.phase
                        == DefenseFacilityServiceOutcomePhase.Completed)
                .pending,
            "post-physical fault did not preserve both outboxes");

        DungeonRuntimeAggregateRootStore restoredRoot = new();
        DefenseFacilityRuntime restored = NewRuntime(
            restoredRoot, world, physical, outcomes);
        restored.PublishRestoreState(restored.PrepareRestoreState(pending));
        DefenseFeatureCommandService restoredUi = NewUi(
            restoredRoot, world, restored);
        Require(restoredUi.RequestFacilityService(facility.GetInstanceID()).Succeeded
            && physical.CommitCount == 1
            && physical.AcknowledgementCount == 1
            && outcomes.Count(DefenseFacilityServiceOutcomePhase.Completed) == 1
            && outcomes.Journal.Capture().observations.Count == 2
            && outcomes.Journal.Capture().observations.All(value =>
                !value.pending)
            && restored.CaptureState().facilities.Single().pendingService.phase
                == DefenseFacilityServicePendingPhase.None,
            "new composition duplicated the child or failed parent retry");

        // Manual reload consumes a distinct physical transfer and parent child.
        physical = new FixturePhysical();
        outcomes = new FixtureOutcomes();
        root = new DungeonRuntimeAggregateRootStore();
        runtime = NewRuntime(root, world, physical, outcomes);
        ui = NewUi(root, world, runtime);
        int wanted = facility.Defense.supplyCapacity
            - facility.Defense.initialSupply;
        physical.Add(facility.Defense.supplyItemId, wanted,
            DefenseFacilityInputOwnerAuthority.BuildSupplyDestinationId(
                facility.RequirePersistentInstanceId().Value));
        Require(ui.RequestFacilityService(facility.GetInstanceID()).Succeeded
            && physical.CommitCount == 1
            && outcomes.LastCompleted.kind
                == DefenseFacilityPhysicalCommitKind.SupplyTransfer
            && outcomes.LastCompleted.afterSupply
                == facility.Defense.supplyCapacity,
            "UI reload did not link its physical transfer to a terminal parent");

        physical = new FixturePhysical();
        outcomes = new FixtureOutcomes();
        root = new DungeonRuntimeAggregateRootStore();
        runtime = NewRuntime(root, world, physical, outcomes);
        ui = NewUi(root, world, runtime);
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded
            && physical.CommitCount == 0
            && outcomes.Count(
                DefenseFacilityServiceOutcomePhase.FailedWithImpact) == 1
            && runtime.CaptureState().facilities.Single().pendingService.phase
                == DefenseFacilityServicePendingPhase.None,
            "unavailable reload did not terminally report its Empty impact");
    }

    private static JObject VerifyUiToActualPhysicalDisposition()
    {
        using DefenseFacilityDebugScenarios.DefenseScenarioWorld scene = new();
        DefenseFacility facility = scene.PlaceDefense("P1_SpikeTrap", new Vector2Int(2, 0));
        FixtureWorld world = new(facility);
        DungeonRuntimeAggregateRootStore root = new();
        using var physical = new DefenseFacilityPhysicalTransactionFixture.FixtureGateway(
            EditorItemCatalogFactory.Create(), root);
        FixtureOutcomes outcomes = new();
        DefenseFacilityRuntime runtime = NewRuntime(root, world, physical, outcomes);
        DefenseFeatureCommandService ui = NewUi(root, world, runtime);
        string facilityId = facility.RequirePersistentInstanceId().Value;
        string supplyDestination = DefenseFacilityInputOwnerAuthority.BuildSupplyDestinationId(facilityId);
        int wanted = facility.Defense.supplyCapacity - facility.Defense.initialSupply;
        Require(wanted > 1, "actual physical case requires multi-stack supply");
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded
                && physical.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0
                && outcomes.Count(DefenseFacilityServiceOutcomePhase.FailedWithImpact) == 1,
            "missing actual supply produced a physical result");
        string first = physical.Add(facility.Defense.supplyItemId, wanted - 1, supplyDestination);
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded
                && physical.Quantity(first) == wanted - 1
                && physical.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0
                && runtime.CaptureState().facilities.Single().supply == facility.Defense.initialSupply,
            "partial actual delivery was consumed or changed supply");
        string second = physical.Add(facility.Defense.supplyItemId, 1, supplyDestination);
        Require(ui.RequestFacilityService(facility.GetInstanceID()).Succeeded
                && physical.Quantity(first) == 0 && physical.Quantity(second) == 0
                && runtime.CaptureState().facilities.Single().supply == facility.Defense.supplyCapacity
                && physical.Ledger.TryGetResultIdentity(
                    PhysicalChild(outcomes.LastCompleted),
                    out GameplayOutcomeReplayIdentity reloadIdentity)
                && reloadIdentity.State >= GameplayOutcomeReplayState.Committed
                && physical.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
            "UI reload did not consume actual stacks and retain its exact physical child");
        JObject reload = JObject.FromObject(outcomes.LastCompleted);

        SetJammed(runtime, facility);
        string maintenance = physical.Add("material:iron-ingot", 1,
            DefenseFacilityInputOwnerAuthority.BuildMaintenanceDestinationId(facilityId));
        outcomes.FailNextCompletedDelivery = true;
        Require(!ui.RequestFacilityService(facility.GetInstanceID()).Succeeded,
            "actual maintenance parent delivery fault did not defer");
        DefenseFacilitySaveData saved = JsonUtility.FromJson<DefenseFacilitySaveData>(
            JsonUtility.ToJson(runtime.CaptureState()));
        DefenseFacilityRecordSaveData pending = saved.facilities.Single();
        string operation = pending.pendingMaintenance.operationId;
        Require(physical.Quantity(maintenance) == 0
                && pending.pendingService.phase == DefenseFacilityServicePendingPhase.TerminalApplied
                && pending.pendingMaintenance.phase == DefenseFacilityPhysicalCommitPhase.OutcomePublished
                && physical.TryGetPendingBatchPhysicalDisposition(operation, out var receipt)
                && receipt.Kind == PhysicalItemDispositionKind.Sink
                && physical.Ledger.TryGetResultIdentity(
                    PhysicalChild(outcomes.LastCompleted),
                    out GameplayOutcomeReplayIdentity maintenanceIdentity)
                && maintenanceIdentity.State >= GameplayOutcomeReplayState.Committed
                && physical.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2,
            "actual maintenance fault lost body/physical/parent phase");
        string childLedgerBeforeRetry = JsonUtility.ToJson(physical.Ledger.CaptureGameplayOutcomes());
        DungeonRuntimeAggregateRootStore restoredRoot = new();
        DefenseFacilityRuntime restored = NewRuntime(restoredRoot, world, physical, outcomes);
        restored.PublishRestoreState(restored.PrepareRestoreState(saved));
        DefenseFeatureCommandService restoredUi = NewUi(restoredRoot, world, restored);
        physical.FailNextAcknowledgement = true;
        Require(!restoredUi.RequestFacilityService(facility.GetInstanceID()).Succeeded
                && physical.TryGetPendingBatchPhysicalDisposition(operation, out _),
            "actual physical acknowledgement fault lost its retry receipt");
        Require(restoredUi.RequestFacilityService(facility.GetInstanceID()).Succeeded
                && !physical.TryGetPendingBatchPhysicalDisposition(operation, out _)
                && physical.Quantity(maintenance) == 0
                && restored.CaptureState().facilities.Single().pendingService.phase
                    == DefenseFacilityServicePendingPhase.None
                && restored.CaptureState().facilities.Single().operationalState
                    != DefenseFacilityOperationalState.Jammed
                && outcomes.Count(DefenseFacilityServiceOutcomePhase.Completed) == 2
                && childLedgerBeforeRetry == JsonUtility.ToJson(physical.Ledger.CaptureGameplayOutcomes()),
            "actual physical retry duplicated the child or failed to clear jam");
        int parentsBeforeNoOp = outcomes.ResultCount;
        Require(!restoredUi.RequestFacilityService(facility.GetInstanceID()).Succeeded
                && outcomes.ResultCount == parentsBeforeNoOp
                && childLedgerBeforeRetry == JsonUtility.ToJson(physical.Ledger.CaptureGameplayOutcomes()),
            "completed actual service replay changed either result stream");
        return new JObject
        {
            ["reload"] = reload,
            ["maintenance"] = JObject.FromObject(outcomes.LastCompleted),
            ["requestedSupply"] = wanted,
            ["supplyAfter"] = restored.CaptureState().facilities.Single().supply,
            ["physicalOutcomes"] = JArray.FromObject(physical.Ledger.CaptureGameplayOutcomes().exactOutcomes),
            ["partialDeliveryUnchanged"] = true,
            ["parentFailureSaved"] = true,
            ["acknowledgementFailureRetried"] = true,
            ["duplicatePhysicalResults"] = 0,
            ["scope"] = "Real UI command/runtime/repository/disposition/canonical physical ledger. Controlled parent fact staging/recovery publisher, power and input authority. Fresh defense owner; physical owner retained, not whole-world restore."
        };
    }

    private static DefenseFacilityRuntime NewRuntime(
        DungeonRuntimeAggregateRootStore root,
        FixtureWorld world,
        IDefenseFacilityPhysicalItemGateway physical,
        FixtureOutcomes outcomes)
        => NewRuntime(root, world, physical, outcomes.Journal);

    private static DefenseFacilityRuntime NewRuntime(
        DungeonRuntimeAggregateRootStore root,
        FixtureWorld world,
        IDefenseFacilityPhysicalItemGateway physical,
        DefenseServicePublicationRuntime journal)
    {
        FixturePower power = new();
        return new DefenseFacilityRuntime(
            UnavailableEquipmentPhysicalItemGateway.Instance,
            physical,
            new FixtureInputOwner(),
            CharacterAiEditorTestDependencies.GameClock,
            CharacterAiEditorTestDependencies.GameEvents,
            power,
            new DefenseFacilityNetworkRuntime(world, power),
            new FacilityCapabilityQuery(world),
            root,
            journal,
            CharacterAiEditorTestDependencies.GameCalendar,
            new MigratedProducerCombatDefenseEntryE2EDebugScenarios.R30DefensePreparationBenefits(1f));
    }

    private static GameplayResultKey PhysicalChild(
        DefenseServiceFactData fact)
    {
        if (fact == null
            || fact.phase != DefenseFacilityServiceOutcomePhase.Completed)
        {
            return default;
        }
        return new GameplayResultKey(
            TradeInventoryOutcomeDefinitions.Get(
                    TradeInventoryOutcomeKind.PhysicalItemBatchDisposition)
                .ProducerId,
            new GameplayOperationId(fact.physicalOperationId),
            fact.physicalOwnerRevision,
            0);
    }

    private static DefenseFeatureCommandService NewUi(
        DungeonRuntimeAggregateRootStore root,
        FixtureWorld world,
        DefenseFacilityRuntime runtime) => new(
        new DefenseResponsePolicyRuntime(new InvasionAggregateStateStore(root)),
        world, world, runtime, new FixtureText(), new FixtureEvacuation());

    private static void SetJammed(
        DefenseFacilityRuntime runtime, DefenseFacility facility)
    {
        runtime.GetSnapshot(facility);
        DefenseFacilitySaveData saved = runtime.CaptureState();
        saved.facilities.Single().operationalState =
            DefenseFacilityOperationalState.Jammed;
        runtime.PublishRestoreState(runtime.PrepareRestoreState(saved));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("UI01: " + message);
    }

    private static void WriteCreateOnlyJson(string path, JObject value)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new(stream);
        writer.Write(value.ToString());
    }

    private sealed class FixtureWorld : ICharacterWorldQuery, IBuildingWorldQuery
    {
        private BuildableObject[] buildings;
        internal FixtureWorld(params DefenseFacility[] facilities) => SetFacilities(facilities);
        internal void SetFacilities(params DefenseFacility[] facilities)
        {
            buildings = facilities.Cast<BuildableObject>().ToArray();
            BuildingVersion++;
        }
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters =>
            Array.Empty<CharacterActor>();
        public int BuildingVersion { get; private set; }
        public IReadOnlyList<BuildableObject> Buildings => buildings;
    }

    private sealed class FixtureInputOwner : IDefenseFacilityInputOwnerRuntime
    {
        public bool TryEnsureAuthority(DefenseFacility facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileLive(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileRestore(
            IReadOnlyCollection<DefenseFacilityState> restoredStates,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixturePower : IPowerInfrastructureQuery
    {
        internal bool Powered = true;
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks =>
            Array.Empty<PowerNetworkSnapshot>();
        public bool IsPowered(BuildableObject building) => Powered;
        public bool TryGetNode(BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }

    private sealed class FixtureText : IDefenseUiTextQuery
    {
        public string Get(string key, params object[] arguments) => key;
        public string Get(DomainFailure failure) => failure.Code.ToString();
    }

    private sealed class FixtureEvacuation : IInvasionOwnerEvacuationService
    {
        public bool IsEvacuating => false;
        public bool HasReachedTarget => false;
        public CharacterActor Owner => null;
        public Vector2Int TargetCell => default;
        public string StatusText => string.Empty;
        public ResidentEvacuationZoneStatus ResidentZoneStatus => default;
        public string ResidentEvacuationStatusText => string.Empty;
        public IReadOnlyList<ResidentEvacuationParticipantView>
            ResidentEvacuationParticipants =>
            Array.Empty<ResidentEvacuationParticipantView>();
        public bool IsResidentEvacuationRoom(Grid grid, RoomInstance room) => false;
        public bool TryDesignateResidentEvacuationRoom(Grid grid,
            RoomInstance room, out string failureReason)
        {
            failureReason = string.Empty;
            return false;
        }
        public bool ClearResidentEvacuationRoom(out string failureReason)
        {
            failureReason = string.Empty;
            return false;
        }
        public bool TryRequestResidentEvacuation(out string failureReason)
        {
            failureReason = string.Empty;
            return false;
        }
        public OwnerEvacuationSaveSnapshot Capture() => default;
        public void PrepareRestoreCandidate(OwnerEvacuationSaveSnapshot snapshot,
            DungeonGameRestoreReport report) { }
        public void PublishRestoreCandidate() { }
        public void RollbackPublishedRestoreCandidate() { }
        public void RetirePreviousRestoreProjection() { }
        public void ActivateRestoreProjection() { }
        public void CompleteRestoreCandidate() { }
        public void DiscardRestoreCandidate() { }
    }

    private sealed class FixturePhysical : IDefenseFacilityPhysicalItemGateway
    {
        private readonly List<WorldItemStackSnapshot> stacks = new();
        private readonly Dictionary<string, PhysicalItemBatchDispositionReceipt>
            pending = new(StringComparer.Ordinal);
        private int nextStack;
        internal int CommitCount { get; private set; }
        internal int AcknowledgementCount { get; private set; }
        internal void Add(string itemId, int quantity, string destinationId) =>
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = "ui01-stack:" + ++nextStack,
                ItemId = itemId,
                Quantity = quantity,
                State = WorldItemStackState.FacilityBuffer,
                DestinationId = destinationId
            });
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;
        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            if (pending.TryGetValue(operationId, out receipt))
            {
                failureReason = string.Empty;
                return true;
            }
            WorldItemStackSnapshot stack = inputs.Count == 1
                ? stacks.FirstOrDefault(value => value.StackId == inputs[0].StackId)
                : null;
            if (stack == null || stack.AvailableQuantity < inputs[0].Quantity)
            {
                receipt = default;
                failureReason = "ui01-physical-input-missing";
                return false;
            }
            receipt = new PhysicalItemBatchDispositionReceipt(
                kind, operationId, reasonCode, "ui01-fingerprint",
                new[] { stack.StackId }, inputs[0].Quantity,
                100L * inputs[0].Quantity, CommitCount + 1L,
                Array.Empty<PhysicalItemDispositionSourceFact>());
            stack.Quantity -= inputs[0].Quantity;
            pending.Add(operationId, receipt);
            CommitCount++;
            failureReason = string.Empty;
            return true;
        }
        public bool TryGetPendingBatchPhysicalDisposition(string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            pending.TryGetValue(operationId, out receipt);
        public bool AcknowledgeBatchPhysicalDisposition(string commitId,
            out string failureReason)
        {
            string operation = pending.FirstOrDefault(value =>
                value.Value.CommitId == commitId).Key;
            if (operation == null)
            {
                failureReason = "ui01-physical-pending-missing";
                return false;
            }
            pending.Remove(operation);
            AcknowledgementCount++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixtureOutcomes :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly Dictionary<string, DefenseServiceFactData> committed =
            new(StringComparer.Ordinal);

        internal FixtureOutcomes()
        {
            Journal = new DefenseServicePublicationRuntime(this, this);
        }

        internal DefenseServicePublicationRuntime Journal { get; }
        internal bool RejectNextPrepare { get; set; }
        internal bool FailNextCompletedDelivery { get; set; }
        internal int ResultCount => committed.Count;
        internal DefenseServiceFactData LastCompleted => committed.Values
            .Last(value => value.phase
                == DefenseFacilityServiceOutcomePhase.Completed);
        internal int Count(DefenseFacilityServiceOutcomePhase phase) =>
            committed.Values.Count(value => value.phase == phase);

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            stage = null;
            if (RejectNextPrepare)
            {
                RejectNextPrepare = false;
                failure = "ui01-injected-prepare-rejection";
                return false;
            }
            if (fact is not DefenseServiceChangedFact serviceFact)
            {
                failure = "ui01-defense-service-fact-required";
                return false;
            }

            DefenseServiceFactData frozen = serviceFact.Snapshot();
            string key = DefenseServiceFactMapping.ResultKey(frozen).ToString();
            stage = new FixtureStage(this, frozen, key);
            failure = string.Empty;
            return true;
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            failure = string.Empty;
            if (facts == null || results == null || results.Length != facts.Count)
            {
                failure = "ui01-defense-service-replay-shape-invalid";
                return false;
            }
            for (int index = 0; index < facts.Count; index++)
            {
                string key = facts[index]?.PublicationKey ?? string.Empty;
                if (!committed.TryGetValue(key, out DefenseServiceFactData frozen))
                {
                    failure = "ui01-defense-service-replay-missing";
                    results[index] = DomainFactDelivery.Reject(failure);
                    return false;
                }
                results[index] = Delivered(key, frozen);
            }
            return true;
        }

        private DomainFactDelivery Commit(
            DefenseServiceFactData frozen,
            string key)
        {
            committed[key] = frozen.Clone();
            if (frozen.phase == DefenseFacilityServiceOutcomePhase.Completed
                && FailNextCompletedDelivery)
            {
                FailNextCompletedDelivery = false;
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    key,
                    string.Empty,
                    string.Empty,
                    "injected-delivery-failure");
            }
            return Delivered(key, frozen);
        }

        private static DomainFactDelivery Delivered(
            string key,
            DefenseServiceFactData frozen) => new(
                DomainFactDeliveryPhase.Delivered,
                key,
                "fixture:defense-service:" + frozen.revision,
                new string('a', 64),
                string.Empty);

        private sealed class FixtureStage : IDomainFactStage
        {
            private readonly FixtureOutcomes owner;
            private readonly DefenseServiceFactData frozen;
            private bool active = true;
            private bool committed;

            internal FixtureStage(
                FixtureOutcomes owner,
                DefenseServiceFactData frozen,
                string operationKey)
            {
                this.owner = owner;
                this.frozen = frozen;
                OperationKey = operationKey;
            }

            public string OperationKey { get; }
            public long OwnerRevision => frozen.revision;
            public bool IsReplay => false;
            public bool IsActive => active;

            public DomainFactDelivery Commit()
            {
                if (!active || committed)
                    throw new InvalidOperationException(
                        "ui01-defense-service-stage-not-active");
                committed = true;
                return owner.Commit(frozen, OperationKey);
            }

            public void Cancel() => active = false;
        }
    }
}
