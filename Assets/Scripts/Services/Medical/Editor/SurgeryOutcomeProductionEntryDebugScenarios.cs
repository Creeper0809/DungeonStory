#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

/// <summary>
/// Calls SurgeryRuntime.ResolveOutcome itself with isolated owners and the
/// production ledger. No dungeon scene or live game state is required.
/// </summary>
public static class SurgeryOutcomeProductionEntryDebugScenarios
{
    public static void RunFirstInstallationFromBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA"))
            + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !Path.IsPathRooted(output)
            || !Path.GetFullPath(output).StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output))
            throw new InvalidOperationException("First installation requires new project-local batch evidence.");
        string probe = File.ReadAllText(Path.Combine(
            Directory.GetParent(Path.GetFullPath(output)).FullName, "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE") && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true") && probe.Contains("inJob=true"),
            "First installation requires the noninteractive desktop probe.");
        List<object> observations = new();
        string failure = string.Empty;
        try
        {
            RunActualFirstInstallation(observations);
            RunOrThrow();
            observations.Add(new { stage = "original-terminal-contract-regression", passed = true });
        }
        catch (Exception error) { failure = error.ToString(); }
        Directory.CreateDirectory(output);
        using (StreamWriter writer = new(new FileStream(
                   Path.Combine(output, "first-installation-report.json"), FileMode.CreateNew)))
            writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                schema = "d05-actual-first-installation@1", passed = failure.Length == 0,
                failure, observations, trainingEligible = false, wholeWorldOrCold = false,
                setup = "Test patient missing arm, supplied prosthetic/wood, frozen successful work result; not natural acquisition, surgery planning, hauling or AI work."
            }, Newtonsoft.Json.Formatting.Indented));
        Require(failure.Length == 0, failure);
        Debug.Log("D05_ACTUAL_FIRST_INSTALLATION=PASS");
    }

    private static void RunActualFirstInstallation(List<object> observations)
    {
        const string nodeId = "arm:left";
        using Fixture clinical = new("actual-first-install");
        using OrganPreservationRestoreJoinFixture.FreshnessFixture physical = new(true);
        CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            CharacterType.Customer, "First installation patient", "Human");
        CharacterBodyHealthRuntime health = CharacterAiEditorTestDependencies.BodyHealthRuntime;
        try
        {
            clinical.Patient.data = data;
            clinical.Patient.characterType = CharacterType.Customer;
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(clinical.Patient);
            health.ConfigureVitals(clinical.Patient, 100f, resetCurrentHealth: true);
            Require(health.TryRemoveNode(clinical.Patient, nodeId, out _, out DomainFailure removed),
                "Missing-arm preparation failed: " + removed);
            string stack = physical.AddStack(SurgeryItemDefinitions.GetProstheticItemId(nodeId),
                WorldItemStackState.Loose, position: physical.Position);
            SurgicalPartInstance part = physical.AddNaturalPart("surgical-part:1", stack, 0f);
            part.kind = SurgicalPartKind.Prosthetic;
            part.nodeId = nodeId;
            part.quality = 1f;
            clinical.ConfigureActualInstallation(physical.Runtime, health, part);
            physical.State.PartSequence = 1;
            physical.State.OrderSequence = 1;
            physical.State.Orders.Add(clinical.Order);
            Require(physical.Runtime.TryReserveForOrder(part.partInstanceId, clinical.Order.orderId,
                    out DomainFailure reserveFailure), "Part reservation failed: " + reserveFailure);
            Require(clinical.CanApplyInstallation(out DomainFailure preflight),
                "Real installation preflight rejected: " + preflight);

            // Material acquisition is fixture setup; its Sink below is real.
            string wood = physical.AddStack("material:lumber", WorldItemStackState.Loose,
                position: physical.Position, quantity: 2);
            Require(physical.Batch.TryCommit(new[] { new PhysicalItemTransformInput(wood, 2) },
                    PhysicalItemDispositionKind.Sink,
                    SurgeryMaterialSinkIdentity.FormatOperationId(clinical.Order.orderId),
                    "surgery-materials-consumed", out PhysicalItemBatchDispositionReceipt sink,
                    out string sinkFailure), "Material Sink failed: " + sinkFailure);
            clinical.Order.materialsConsumed = true;
            clinical.Order.materialSinkOperationId = sink.OperationId;
            clinical.Order.materialSinkCommitId = sink.CommitId;
            clinical.Order.materialSinkInputMassGrams = sink.InputMassGrams;
            clinical.Order.materialSinkAcknowledged = true;
            clinical.Order.materialDestinationId = "surgery-materials:" + clinical.Order.orderId;
            clinical.Order.materialBufferCapacityGrams = sink.InputMassGrams;
            clinical.Order.materialMassAuthorityRevision = 1;
            clinical.Order.materialCapacityFingerprint = SurgeryMaterialCapacityFingerprint.Create(clinical.Order);
            Require(physical.Repository.GetEditorTestQuantity(wood) == 0 && sink.Quantity == 2,
                "Real material Sink did not consume exactly two wood units.");

            string beforeBody = JsonUtility.ToJson(health.Capture());
            string beforePhysical = JsonUtility.ToJson(physical.CapturePhysicalSave());
            clinical.Recorder.RejectReserve = true;
            Require(!clinical.Resolve() && !part.installed && clinical.Order.resolvedEffectCount == 0
                && JsonUtility.ToJson(health.Capture()) == beforeBody
                && JsonUtility.ToJson(physical.CapturePhysicalSave()) == beforePhysical,
                "Rejected ledger reservation mutated the actual body or physical part.");
            observations.Add(new { stage = "reserve-rejected-unchanged", body = beforeBody,
                physical = beforePhysical, passed = true });

            clinical.Recorder.RejectReserve = false;
            clinical.Recorder.RejectCommit = true;
            Require(!clinical.Resolve() && part.installed
                && physical.Repository.GetEditorTestQuantity(stack) == 0
                && clinical.Order.resolvedEffectCount == 1 && !clinical.Order.resolutionFactAccepted,
                "Commit rejection did not retain the real installed part and pending result.");
            AnatomyNodeHealthState installed = health.GetAnatomySnapshot(clinical.Patient)
                .Nodes.Single(node => node.nodeId == nodeId);
            Require(!installed.missing && installed.installedPartId == part.partInstanceId
                && installed.currentHealth > 0f && part.installedSubjectId == clinical.Patient.Identity.PersistentId
                && !string.IsNullOrEmpty(part.installationCommitId),
                "Real anatomy/physical installation identities did not join.");
            DungeonSurgerySaveData savedSurgery = DungeonSaveJson.Deserialize<DungeonSurgerySaveData>(
                JsonUtility.ToJson(physical.CaptureSurgerySave()));
            DungeonGameRestoreReport surgeryValidation = new();
            SurgerySaveValidation.Validate(savedSurgery,
                new ResourceSurgicalProcedureCatalog(Resources.LoadAll<SurgicalProcedureSO>("SO/Medical/Procedures")),
                new ResourceAnatomyProfileCatalog(Resources.LoadAll<AnatomyProfileSO>("SO/Medical/Anatomy")),
                surgeryValidation);
            Require(surgeryValidation.Success,
                "Actual first-install checkpoint is invalid: " + string.Join("; ", surgeryValidation.Errors));
            DungeonPhysicalItemSaveData savedPhysical = physical.ReadPhysicalSaveThroughSection();
            DungeonCharacterBodyHealthSaveData savedBody = DungeonSaveJson.Deserialize<DungeonCharacterBodyHealthSaveData>(
                JsonUtility.ToJson(health.Capture()));
            GameplayOutcomeLedgerSaveData savedLedger = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(
                JsonUtility.ToJson(clinical.Ledger.CaptureGameplayOutcomes()));
            observations.Add(new { stage = "installed-result-pending-checkpoint", body = savedBody,
                physical = savedPhysical, surgery = savedSurgery, ledger = savedLedger });

            using OrganPreservationRestoreJoinFixture.FreshnessFixture restored = new(true,
                physicalRestore: savedPhysical, surgeryRestore: savedSurgery, bodyRestore: savedBody);
            health.PublishRestore(health.PrepareRestore(savedBody));
            clinical.RestoreInstallationOwners(restored.State.Orders.Single(), restored.Runtime, health, savedLedger);
            Require(clinical.Resolve() && clinical.Order.resolutionFactAccepted
                && clinical.Order.state == SurgeryOrderState.Recovering
                && restored.Repository.GetEditorTestQuantity(stack) == 0
                && restored.Repository.GetEditorTestQuantity(wood) == 0,
                "Restored real installation did not finish without repeated consumption.");
            GameplayOutcomeSnapshot exact = clinical.Exact();
            Require(Fact(exact, SurgeryOutcomeIds.AnatomyBefore).Contains("missing=1|part=none")
                && Fact(exact, SurgeryOutcomeIds.AnatomyAfter).Contains("missing=0|part=" + part.partInstanceId)
                && Fact(exact, SurgeryOutcomeIds.Materials).Contains("material:lumber:2")
                && Fact(exact, SurgeryOutcomeIds.ChildReferences).Contains("material-sink=" + sink.CommitId)
                && Fact(exact, SurgeryOutcomeIds.ChildReferences).Contains("body-subject=" + clinical.Patient.Identity.PersistentId)
                && exact.absoluteDay == clinical.Order.completionAbsoluteDay,
                "Actual installation result lost exact anatomy, material or patient evidence.");
            string completedBody = JsonUtility.ToJson(health.Capture());
            string completedPhysical = JsonUtility.ToJson(restored.CapturePhysicalSave());
            string completedLedger = JsonUtility.ToJson(clinical.Ledger.CaptureGameplayOutcomes());
            clinical.Resolve();
            clinical.Resolve();
            Require(JsonUtility.ToJson(health.Capture()) == completedBody
                && JsonUtility.ToJson(restored.CapturePhysicalSave()) == completedPhysical
                && JsonUtility.ToJson(clinical.Ledger.CaptureGameplayOutcomes()) == completedLedger
                && clinical.Ledger.GetGlobal(OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 1,
                "Completed installation replay duplicated body, inventory or canonical result.");
            observations.Add(new { stage = "restored-completed-replay", body = completedBody,
                physical = completedPhysical, ledger = completedLedger, exact,
                surgery = restored.CaptureSurgerySave(), replayCount = 2, passed = true });
        }
        finally
        {
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(clinical.Patient);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    public static void RunOrThrow()
    {
        using (Fixture success = new("success"))
        {
            Require(success.Resolve() && success.EffectApplications == 1
                && success.Order.state == SurgeryOrderState.Recovering,
                "surgery success did not complete its clinical effect");
            GameplayOutcomeSnapshot exact = success.Exact();
            Require(Fact(exact, SurgeryOutcomeIds.Procedure).Contains("Suture")
                && Fact(exact, SurgeryOutcomeIds.AnatomyBefore).Contains("health=40")
                && Fact(exact, SurgeryOutcomeIds.AnatomyAfter).Contains("health=45")
                && Fact(exact, SurgeryOutcomeIds.Materials).Contains("gauze:2")
                && exact.absoluteDay == 2
                && success.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 1,
                "surgery success lost patient/procedure/material/anatomy evidence");
            Require(!success.Resolve() || success.EffectApplications == 1,
                "surgery replay repeated a committed effect");
        }

        using (Fixture rejected = new("prepare"))
        {
            rejected.Order.resultRolled = false;
            rejected.Recorder.RejectReserve = true;
            Require(!rejected.Resolve()
                && rejected.EffectApplications == 0
                && rejected.Node.currentHealth == 40f
                && rejected.Order.state == SurgeryOrderState.Procedure
                && !rejected.Order.resultRolled
                && !rejected.Order.outcomeAnatomyBeforeCaptured
                && rejected.Ledger.GetDiagnostics().ReservedCount == 0,
                "surgery prepare rejection changed clinical state");
        }

        using (Fixture rejectedCommit = new("commit"))
        {
            rejectedCommit.Recorder.RejectCommit = true;
            Require(!rejectedCommit.Resolve()
                && rejectedCommit.EffectApplications == 1
                && rejectedCommit.Order.resolvedEffectCount == 1
                && rejectedCommit.Order.state == SurgeryOrderState.Procedure
                && !rejectedCommit.Order.resolutionFactAccepted,
                "surgery commit rejection falsely finalized an effect");
            rejectedCommit.Recorder.RejectCommit = false;
            Require(rejectedCommit.Resolve()
                && rejectedCommit.EffectApplications == 1
                && rejectedCommit.Order.resolutionFactAccepted
                && rejectedCommit.Exact() != null,
                "surgery commit retry repeated the clinical effect");
        }

        foreach (SurgeryFailureSeverity severity in new[]
                 {
                     SurgeryFailureSeverity.Minor,
                     SurgeryFailureSeverity.Major,
                     SurgeryFailureSeverity.Fatal
                 })
        {
            using Fixture failed = new("failure-" + severity);
            failed.ConfigureFrozenFailure(severity);
            Require(!failed.Resolve()
                && failed.Order.resolutionFactAccepted
                && failed.Order.outcomeConsequenceStep == 2
                && failed.Order.materialTerminalTargetState == SurgeryOrderState.Failed
                && Fact(failed.Exact(), SurgeryOutcomeIds.Severity)
                    == severity.ToString(),
                "frozen surgery failure was not recorded: " + severity);
            if (severity == SurgeryFailureSeverity.Fatal)
                Require(failed.Patient.IsDead
                    && Fact(failed.Exact(), SurgeryOutcomeIds.AnatomyAfter)
                        .Contains("life=dead"),
                    "fatal surgery did not record the actual patient death");
        }

        using (Fixture install = new("install"))
        {
            install.ConfigureCommittedInstallation(replacement: false);
            Require(install.Resolve()
                && Fact(install.Exact(), SurgeryOutcomeIds.AnatomyBefore)
                    .Contains("part=none")
                && Fact(install.Exact(), SurgeryOutcomeIds.AnatomyAfter)
                    .Contains("part=surgical-part:incoming")
                && Fact(install.Exact(), SurgeryOutcomeIds.ChildReferences)
                    .Contains("material-sink=physical-commit:sink"),
                "surgery installation child and anatomy joins were lost");
        }
        using (Fixture replace = new("replace"))
        {
            replace.ConfigureCommittedInstallation(replacement: true);
            Require(replace.Resolve()
                && Fact(replace.Exact(), SurgeryOutcomeIds.AnatomyBefore)
                    .Contains("part=surgical-part:previous")
                && Fact(replace.Exact(), SurgeryOutcomeIds.ChildReferences)
                    .Contains("replacement-batch=physical-commit:replacement")
                && Fact(replace.Exact(), SurgeryOutcomeIds.ChildReferences)
                    .Contains("replacement-output-item=physical-item:old-part"),
                "surgery replacement physical child references were lost");
        }

        using (Fixture pending = new("pending"))
        {
            pending.Recorder.FaultDelivery = true;
            Require(pending.Resolve() && pending.EffectApplications == 1,
                "surgery delivery fault rolled back committed clinical state");
            GameplayOutcomeLedgerSaveData checkpoint =
                pending.Ledger.CaptureGameplayOutcomes();
            SurgeryAggregateStateStore ownerStore = new(
                new DungeonRuntimeAggregateRootStore());
            ownerStore.State.Orders.Add(pending.Order);
            DungeonSurgerySaveData ownerCheckpoint =
                new SurgeryPersistence(ownerStore).Capture();
            SurgeryOrder savedOrder = SurgerySaveValidation.CreateState(
                ownerCheckpoint).Orders.Single();
            Require(checkpoint.outbox.Count == 1
                && ownerCheckpoint.version == DungeonSurgerySaveData.CurrentVersion
                && savedOrder.resolutionFactAccepted
                && !savedOrder.resolutionFactDelivered
                && !savedOrder.resolvedFact.IsEmpty
                && !string.IsNullOrWhiteSpace(
                    savedOrder.resolutionPublicationKey)
                && !string.IsNullOrWhiteSpace(
                    savedOrder.resolutionPublicationId)
                && !string.IsNullOrWhiteSpace(
                    savedOrder.resolutionFingerprint)
                && savedOrder.outcomeAnatomyBeforeCaptured,
                "surgery pending outcome or owner checkpoint was not saved");
            SurgeryResolvedFactRestoreJoin.Validate(
                new[] { savedOrder },
                checkpoint);
            pending.RestoreObservationOwner(savedOrder, checkpoint);
            Require(pending.ReplayFrozenObservation()
                && pending.Order.resolutionFactDelivered
                && pending.Ledger.GetGlobal(
                        OutcomeCursor.FirstPage(10),
                        OutcomeFilter.All).Items.Count == 1,
                "surgery pending outcome was not replayed from its frozen owner fact");
            pending.Resolve();
            Require(pending.EffectApplications == 1
                && pending.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 1,
                "surgery delivery retry duplicated its result");
        }
    }

    public static void RunObserverFocusedForBatchMode()
    {
        RequireIsolatedBatchEvidence("Surgery result observer checks");
        RunResultObserverFocusedCore();
        Debug.Log(
            "B77_SURGERY_RESULT_OBSERVER_FOCUSED=PASS; "
            + "success=true; failure=true; prepareReject=true; "
            + "unknownBeforeCommit=true; unknownAfterCommit=true; "
            + "jsonFrozenReplay=true; g12Golem=true; "
            + "scope=actual-surgery-owner-not-brain");
    }

    public static void RunCompletionObserverFocusedForBatchMode()
    {
        RequireIsolatedBatchEvidence("Surgery completion observer checks");
        RunResultObserverFocusedCore();
        WorkTerminalIdentityOwnershipDebugScenarios
            .RunCompletionObserverFocused();
        Debug.Log(
            "B78_SURGERY_COMPLETION_OBSERVER_FOCUSED=PASS; "
            + "resultRegression=true; completionSuccess=true; "
            + "completionDeferred=true; completionConflict=true; "
            + "missingObserver=true; disconnectRebind=true; "
            + "unknownAcknowledgementRestore=true; "
            + "notificationAttemptedOnce=true; genericSuppressed=true");
    }

    private static void RunResultObserverFocusedCore()
    {
        RunOrThrow();
        RunG12GolemOrThrow();
        VerifyUncertainObservationRecovery(commitBeforeUnknown: false);
        VerifyUncertainObservationRecovery(commitBeforeUnknown: true);
    }

    private static void RequireIsolatedBatchEvidence(string label)
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !Path.IsPathRooted(output)
            || !Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                label + " require new project-local batch evidence.");
        }
        string probe = File.ReadAllText(Path.Combine(
            Directory.GetParent(Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            label + " require the noninteractive desktop probe.");
    }

    private static void VerifyUncertainObservationRecovery(
        bool commitBeforeUnknown)
    {
        string label = commitBeforeUnknown ? "after-commit" : "before-commit";
        using Fixture fixture = new("uncertain-" + label);
        fixture.InjectUncertainObservationOnce(commitBeforeUnknown);
        Require(!fixture.Resolve()
            && fixture.EffectApplications == 1
            && fixture.Order.resolvedEffectCount == 1
            && !fixture.Order.resolutionFactAccepted
            && !fixture.Order.resolutionFactDelivered
            && !fixture.Order.resolvedFact.IsEmpty
            && !string.IsNullOrWhiteSpace(
                fixture.Order.resolutionPublicationKey),
            "Surgery " + label
                + " uncertainty did not retain one applied effect and its frozen fact.");

        string frozenHash = SurgeryResolvedFactMapping.ComputeContextSha256(
            fixture.Order.resolvedFact);
        float frozenAfterHealth = fixture.Order.resolvedFact.after.currentHealth;
        var ownerSave = new DungeonSurgerySaveData
        {
            orders = new List<SurgeryOrder>
            {
                SurgeryStateCloner.CloneOrder(fixture.Order)
            }
        };
        DungeonSurgerySaveData restoredOwner = JsonUtility.FromJson<
            DungeonSurgerySaveData>(JsonUtility.ToJson(ownerSave));
        GameplayOutcomeLedgerSaveData restoredLedger =
            DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(
                JsonUtility.ToJson(
                    fixture.Ledger.CaptureGameplayOutcomes()));
        SurgeryResolvedFactRestoreJoin.Validate(
            restoredOwner.orders,
            restoredLedger);

        fixture.Node.currentHealth = 83f;
        fixture.RestoreObservationOwner(
            restoredOwner.orders.Single(),
            restoredLedger);
        Require(fixture.Resolve()
            && fixture.EffectApplications == 1
            && fixture.Node.currentHealth == 83f
            && fixture.Order.resolutionFactAccepted
            && fixture.Order.resolutionFactDelivered
            && SurgeryResolvedFactMapping.ComputeContextSha256(
                    fixture.Order.resolvedFact)
                == frozenHash,
            "Surgery " + label
                + " recovery repeated effects or recaptured live anatomy.");
        GameplayOutcomeSnapshot exact = fixture.Exact();
        Require(Fact(exact, SurgeryOutcomeIds.ContextSha256) == frozenHash
            && Fact(exact, SurgeryOutcomeIds.AnatomyAfter).Contains(
                "health=" + frozenAfterHealth.ToString(
                    "R",
                    System.Globalization.CultureInfo.InvariantCulture))
            && fixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10),
                    OutcomeFilter.All).Items.Count == 1,
            "Surgery " + label
                + " recovery changed the frozen receipt or duplicated its result.");
        fixture.Resolve();
        Require(fixture.EffectApplications == 1
            && fixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10),
                    OutcomeFilter.All).Items.Count == 1,
            "Surgery " + label
                + " exact replay repeated an effect or canonical outcome.");
    }

    public static void RunG12GolemOrThrow()
    {
        using Fixture golem = new("golem-upkeep", golemPowerCore: true);
        Require(golem.Resolve()
                && golem.EffectApplications == 1
                && golem.Order.state == SurgeryOrderState.Recovering,
            "G12 golem maintenance did not complete its clinical effect");

        GameplayOutcomeSnapshot exact = golem.Exact();
        string before = Fact(exact, SurgeryOutcomeIds.AnatomyBefore);
        string after = Fact(exact, SurgeryOutcomeIds.AnatomyAfter);
        string materials = Fact(exact, SurgeryOutcomeIds.Materials);
        string children = Fact(exact, SurgeryOutcomeIds.ChildReferences);
        Require(before.Contains("node=torso")
                && before.Contains("rejection=40")
                && before.Contains("species=Golem")
                && before.Contains("charge=20")
                && before.Contains("integrity=50")
                && after.Contains("node=torso")
                && after.Contains("rejection=10")
                && after.Contains("species=Golem")
                && after.Contains("charge=20")
                && after.Contains("integrity=80"),
            "G12 golem maintenance lost its body or species before/after state");
        Require(materials.Contains("consumed=1")
                && materials.Contains("material:lumber:1:required")
                && children.Contains(
                    "material-sink=physical-commit:g12-golem-lumber")
                && children.Contains("labor-origin=Autonomous")
                && children.Contains(
                    "labor-worker=character:surgery-outcome-terminal-doctor-golem-upkeep")
                && children.Contains("labor-work=26")
                && children.Contains(
                    "body-subject=character:surgery-golem-upkeep")
                && children.Contains("body-node=torso"),
            "G12 golem maintenance parent lost material, labor, or body children");
        Require(golem.SpeciesState != null
                && Mathf.Approximately(golem.SpeciesState.Integrity, 80f)
                && Mathf.Approximately(golem.Node.rejectionBurden, 10f),
            "G12 golem maintenance receipt disagrees with authoritative state");
        Require(!golem.Resolve() || golem.EffectApplications == 1,
            "G12 golem maintenance replay repeated its clinical effect");
        Require(golem.EffectApplications == 1
                && golem.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 1,
            "G12 golem maintenance replay duplicated its result");
    }

    private static string Fact(
        GameplayOutcomeSnapshot snapshot,
        GameplayOutcomeFactId id) => snapshot.facts
        .Single(fact => fact.factId == id.Value).value;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject patientObject;
        private readonly TestEffectHandler effectHandler;
        private readonly SurgeryRuntime runtime;
        private SurgicalProcedureSO procedure;
        private ISurgicalProcedureEffectHandler installationHandler;
        private DomainFactDispatcher factDispatcher;
        private IDisposable factBinding;
        private int golemEffectApplications;

        internal Fixture(string suffix, bool golemPowerCore = false)
        {
            patientObject = new GameObject("Surgery outcome patient " + suffix);
            CharacterActor patient = patientObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(patientObject);
            patient.EnsureRuntimeState();
            patient.Identity.SetPersistentId("character:surgery-" + suffix);
            Patient = patient;
            Node = new AnatomyNodeHealthState
            {
                nodeId = golemPowerCore ? "torso" : "heart",
                maxHealth = 100f,
                currentHealth = 40f,
                rejectionBurden = golemPowerCore ? 40f : 0f
            };
            SurgicalProcedureEffect effect;
            SurgicalMaterialRequirement[] materials;
            if (golemPowerCore)
            {
                effect = new MaintainSurgicalPartEffect
                {
                    durability = 30f,
                    contaminationReduction = 12f
                };
                materials = new[]
                {
                    new SurgicalMaterialRequirement
                    {
                        itemId = "material:lumber", quantity = 1
                    }
                };
                SpeciesState = new CharacterSpeciesRuntimeState
                {
                    CharacterId = new CharacterId(patient.Identity.PersistentId),
                    SpeciesId = new CharacterSpeciesId("Golem"),
                    Charge = 20f,
                    Integrity = 50f
                };
            }
            else
            {
                effect = new TestEffect();
                effectHandler = new TestEffectHandler(Node);
                materials = new[]
                {
                    new SurgicalMaterialRequirement
                    {
                        itemId = "item:gauze", quantity = 2
                    }
                };
            }
            procedure = ScriptableObject.CreateInstance<SurgicalProcedureSO>();
            procedure.Configure(
                golemPowerCore
                    ? "procedure:golem-power-core"
                    : "procedure:surgery-outcome-" + suffix,
                golemPowerCore
                    ? "Golem power-core maintenance"
                    : "Surgery outcome test",
                string.Empty,
                golemPowerCore
                    ? SurgicalProcedureKind.Maintenance
                    : SurgicalProcedureKind.Suture,
                Node.nodeId, string.Empty, SurgeryFacilityTag.None,
                1f, 0f, 0f, 0f,
                false, false, true, false, false,
                materials,
                new SurgicalProcedureEffect[] { effect });
            Order = new SurgeryOrder
            {
                orderId = "surgery:outcome-" + suffix,
                procedureId = procedure.ProcedureId,
                facilityId = "building:surgery-outcome-fixture-" + suffix.ToLowerInvariant(),
                subject = new SurgicalSubjectRef
                {
                    kind = SurgicalSubjectKind.Character,
                    subjectId = patient.Identity.PersistentId,
                    displayName = "환자"
                },
                targetNodeId = Node.nodeId,
                state = SurgeryOrderState.Procedure,
                resultRolled = true,
                resultSucceeded = true,
                resultOutcomeId = "success",
                doctorId = "character:surgery-outcome-doctor-"
                    + suffix.ToLowerInvariant(),
                completionDoctorId =
                    "character:surgery-outcome-terminal-doctor-"
                    + suffix.ToLowerInvariant(),
                completionAbsoluteDay = 2,
                completionOrigin = CharacterCommandOrigin.Autonomous,
                requiredWork = golemPowerCore ? 26f : 1f,
                completedWork = golemPowerCore ? 26f : 1f,
                materials = procedure.Materials.Select(item => item.Clone()).ToList()
            };
            if (golemPowerCore)
            {
                Order.materialsConsumed = true;
                Order.materialSinkInputMassGrams = 1000L;
                Order.materialSinkOperationId =
                    "physical-operation:g12-golem-lumber";
                Order.materialSinkCommitId =
                    "physical-commit:g12-golem-lumber";
            }
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new SurgeryOutcomeDescriptor(new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new SurgeryOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8, largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId("run:surgery-" + suffix),
                1L);
            Recorder = new FaultingRecorder(
                new GameplayOutcomeRecorder(Ledger, Registry, new GameEventBus()));
            runtime = (SurgeryRuntime)FormatterServices.GetUninitializedObject(
                typeof(SurgeryRuntime));
            ConnectFactObserver();
            ICharacterWorldQuery characters = Proxy<ICharacterWorldQuery>((method, _) =>
                method.Name == "get_Characters"
                    ? new[] { patient }
                    : 0);
            Set("characters", characters);
            Set("wildlife", Proxy<IWildlifeWorldQuery>((method, _) =>
                method.Name == "get_Wildlife"
                    ? Array.Empty<WildlifeActor>()
                    : 0));
            IAnatomyHealthRuntime anatomyRuntime = Proxy<IAnatomyHealthRuntime>(
                (method, arguments) =>
            {
                switch (method.Name)
                {
                    case "GetAnatomySnapshot":
                        return new AnatomyHealthSnapshot(
                            "test-anatomy", new[] { Node },
                            1f, 1f, 1f, 1f, 1f, 1f, 1f);
                    case "TryDamageNode":
                        Node.currentHealth -= (float)arguments[2];
                        Node.bleedingPerSecond += (float)arguments[3];
                        return true;
                    case "TryAddNodeBurden":
                        Node.rejectionBurden += (float)arguments[2];
                        Node.mutationBurden += (float)arguments[3];
                        Node.infection += (float)arguments[4];
                        arguments[5] = DomainFailure.None;
                        return true;
                    case "TryReduceNodeBurden":
                        Node.rejectionBurden = Mathf.Max(
                            0f, Node.rejectionBurden - (float)arguments[2]);
                        Node.mutationBurden = Mathf.Max(
                            0f, Node.mutationBurden - (float)arguments[3]);
                        Node.infection = Mathf.Max(
                            0f, Node.infection - (float)arguments[4]);
                        arguments[5] = DomainFailure.None;
                        return true;
                    default:
                        throw new InvalidOperationException(
                            "Unexpected surgery anatomy call: " + method.Name);
                }
            });
            Set("anatomy", anatomyRuntime);
            ICharacterSpeciesQuery speciesQuery = Proxy<ICharacterSpeciesQuery>(
                (method, arguments) =>
                {
                    if (method.Name != "TryGet")
                        throw new InvalidOperationException(method.Name);
                    bool found = SpeciesState != null
                        && arguments[0] is CharacterId id
                        && id.Equals(SpeciesState.CharacterId);
                    arguments[1] = found ? SpeciesState : null;
                    return found;
                });
            ICharacterSpeciesCommand speciesCommands =
                Proxy<ICharacterSpeciesCommand>((method, arguments) =>
                {
                    if (method.Name != "RepairIntegrity")
                        throw new InvalidOperationException(method.Name);
                    if (SpeciesState == null
                        || arguments[0] is not CharacterId id
                        || !id.Equals(SpeciesState.CharacterId))
                    {
                        arguments[2] = new DomainFailure(
                            FailureCode.CharacterSpeciesRepairUnsupported,
                            arguments[0]?.ToString() ?? string.Empty);
                        return false;
                    }
                    SpeciesState.Integrity = Mathf.Min(
                        100f,
                        SpeciesState.Integrity + (float)arguments[1]);
                    golemEffectApplications++;
                    arguments[2] = DomainFailure.None;
                    return true;
                });
            Set("speciesRuntime", speciesQuery);
            Set("clock", Proxy<IGameClock>((method, _) => method.Name switch
            {
                "get_Time" => GameCalendarRules.SecondsPerDay,
                "get_DeltaTime" => 1f,
                "get_FrameCount" => 1,
                "get_IsPaused" => false,
                _ => throw new InvalidOperationException(method.Name)
            }));
            Set("calendar", new EditorFixedGameCalendar(2));
            Set("effectHandlers", golemPowerCore
                ? new Dictionary<Type, ISurgicalProcedureEffectHandler>
                {
                    [typeof(MaintainSurgicalPartEffect)] =
                        new MaintainSurgicalPartEffectHandler(
                            characters,
                            anatomyRuntime,
                            speciesQuery,
                            speciesCommands)
                }
                : new Dictionary<Type, ISurgicalProcedureEffectHandler>
                {
                    [typeof(TestEffect)] = effectHandler
                });
            Set("replacementParts",
                FormatterServices.GetUninitializedObject(typeof(SurgicalPartRuntime)));
            Set("materialTerminal", Proxy<ISurgeryMaterialTerminalRuntime>(
                (method, arguments) =>
                {
                    SurgeryOrder order = (SurgeryOrder)arguments[0];
                    order.state = SurgeryOrderState.TerminalDraining;
                    order.materialTerminalTargetState =
                        (SurgeryOrderState)arguments[1];
                    return new SurgeryMaterialTerminalAdvanceResult(
                        SurgeryMaterialTerminalAdvanceStatus.Deferred,
                        string.Empty);
                }));
            Set("facts", factDispatcher);
            Set("factRecovery", factDispatcher);
        }

        internal AnatomyNodeHealthState Node { get; }
        internal CharacterActor Patient { get; }
        internal SurgeryOrder Order { get; private set; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; private set; }
        internal FaultingRecorder Recorder { get; private set; }
        internal CharacterSpeciesRuntimeState SpeciesState { get; }
        internal int EffectApplications =>
            effectHandler?.Applications ?? golemEffectApplications;

        internal bool Resolve()
        {
            object[] args = { Order, procedure, null, DomainFailure.None };
            try
            {
                return (bool)typeof(SurgeryRuntime).GetMethod(
                    "ResolveOutcome",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(runtime, args);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        internal void InjectUncertainObservationOnce(
            bool commitBeforeUnknown)
        {
            var uncertain = new UncertainFactPublisher(
                factDispatcher,
                commitBeforeUnknown);
            Set("facts", uncertain);
            Set("factRecovery", uncertain);
        }

        internal void RestoreObservationOwner(
            SurgeryOrder restoredOrder,
            GameplayOutcomeLedgerSaveData checkpoint)
        {
            Order = restoredOrder
                ?? throw new ArgumentNullException(nameof(restoredOrder));
            RestoreObservationAuthority(checkpoint);
        }

        internal bool ReplayFrozenObservation()
        {
            MethodInfo replay = typeof(SurgeryRuntime).GetMethod(
                "TryPublishSurgeryResolvedFact",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(replay != null,
                "Missing SurgeryRuntime frozen-fact replay entry.");
            object[] args =
            {
                Order,
                default(DomainFactLease),
                DomainFailure.None
            };
            try
            {
                return (bool)replay.Invoke(runtime, args);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        internal GameplayOutcomeSnapshot Exact() => Ledger.GetGlobal(
            OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Single().Exact;

        internal void ConfigureActualInstallation(SurgicalPartRuntime parts,
            CharacterBodyHealthRuntime health, SurgicalPartInstance part)
        {
            SurgicalProcedureSO authored = UnityEditor.AssetDatabase.LoadAssetAtPath<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures/procedure_prosthetic-installation.asset");
            Require(authored != null, "Authored prosthetic installation procedure is missing.");
            UnityEngine.Object.DestroyImmediate(procedure);
            procedure = UnityEngine.Object.Instantiate(authored);
            Order.orderId = "surgery:1";
            Order.procedureId = procedure.ProcedureId;
            Order.targetNodeId = part.nodeId;
            Order.selectedPartInstanceId = part.partInstanceId;
            Order.requiredWork = procedure.RequiredWork;
            Order.completedWork = procedure.RequiredWork;
            Order.materials = procedure.Materials.Select(value => value.Clone()).ToList();
            UseInstallationOwners(parts, health);
        }

        internal bool CanApplyInstallation(out DomainFailure failure) =>
            installationHandler.CanApply(Order, procedure.Effects.Single(), out failure);

        private void UseInstallationOwners(SurgicalPartRuntime parts, CharacterBodyHealthRuntime health)
        {
            installationHandler = new InstallSurgicalPartEffectHandler(
                Proxy<ICharacterWorldQuery>((method, _) => method.Name == "get_Characters" ? new[] { Patient } : 0),
                Proxy<IWildlifeWorldQuery>((method, _) => method.Name == "get_Wildlife" ? Array.Empty<WildlifeActor>() : 0),
                health,
                Proxy<IWildlifeAnatomyHealthRuntime>((method, _) => throw new InvalidOperationException("Unexpected wildlife call.")),
                parts,
                Proxy<IFacilityBufferMassAdmissionService>((method, _) => throw new InvalidOperationException("Unexpected replacement admission.")),
                Proxy<IFacilityBufferPlannedOutputPublicationService>((method, _) => throw new InvalidOperationException("Unexpected replacement publication.")));
            Set("anatomy", health);
            Set("replacementParts", parts);
            Set("effectHandlers", new Dictionary<Type, ISurgicalProcedureEffectHandler>
                { [typeof(InstallSurgicalPartEffect)] = installationHandler });
        }

        internal void RestoreInstallationOwners(SurgeryOrder restoredOrder, SurgicalPartRuntime parts,
            CharacterBodyHealthRuntime health, GameplayOutcomeLedgerSaveData checkpoint)
        {
            Order = restoredOrder;
            RestoreObservationAuthority(checkpoint);
            UseInstallationOwners(parts, health);
        }

        internal void ConfigureFrozenFailure(SurgeryFailureSeverity severity)
        {
            Order.resultSucceeded = false;
            Order.resultOutcomeId = "failure";
            Order.failureSeverity = severity;
        }

        internal void ConfigureCommittedInstallation(bool replacement)
        {
            procedure.Configure(
                procedure.ProcedureId,
                "Installation", string.Empty,
                SurgicalProcedureKind.InstallProsthetic,
                "heart", string.Empty, SurgeryFacilityTag.None,
                1f, 0f, 0f, 0f,
                false, false, true, false, false,
                Order.materials,
                new SurgicalProcedureEffect[]
                {
                    new InstallSurgicalPartEffect()
                });
            Order.resolvedEffectCount = 1;
            Order.outcomeAnatomyBeforeCaptured = true;
            Order.outcomeAnatomyBefore = new SurgeryAnatomyFactData
            {
                availability = SurgeryAnatomyAvailability.Available,
                profileId = "test-anatomy",
                nodeId = "heart",
                alive = true,
                currentHealth = Node.currentHealth,
                maxHealth = Node.maxHealth,
                installedPartId = replacement
                    ? "surgical-part:previous"
                    : string.Empty
            };
            Node.installedPartId = "surgical-part:incoming";
            Order.materialSinkCommitId = "physical-commit:sink";
            if (replacement)
            {
                Order.replacementPhase = SurgicalPartReplacementPhase.Completed;
                Order.replacementOperationId = "replacement:operation";
                Order.replacementBatchCommitId =
                    "physical-commit:replacement";
                Order.replacementOutputItemInstanceId =
                    "physical-item:old-part";
            }
        }

        public void Dispose()
        {
            factBinding?.Dispose();
            factDispatcher?.Dispose();
            UnityEngine.Object.DestroyImmediate(procedure);
            UnityEngine.Object.DestroyImmediate(patientObject);
        }

        private void ConnectFactObserver()
        {
            factBinding?.Dispose();
            factDispatcher?.Dispose();
            factDispatcher = new DomainFactDispatcher();
            factBinding = SurgeryResolvedFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    new GameplayDomainFactCommitGroup(Recorder, Ledger))
                .Connect(factDispatcher);
        }

        private void RestoreObservationAuthority(
            GameplayOutcomeLedgerSaveData checkpoint)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId(
                    "run:surgery-observer-restored"),
                1L);
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(checkpoint);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
            Recorder = new FaultingRecorder(new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus()));
            ConnectFactObserver();
            Set("facts", factDispatcher);
            Set("factRecovery", factDispatcher);
        }

        private void Set(string name, object value)
        {
            FieldInfo field = typeof(SurgeryRuntime).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "Missing SurgeryRuntime field " + name);
            field.SetValue(runtime, value);
        }
    }

    [Serializable]
    private sealed class TestEffect : SurgicalProcedureEffect { }

    private sealed class TestEffectHandler :
        SurgicalProcedureEffectHandler<TestEffect>
    {
        private readonly AnatomyNodeHealthState node;
        internal TestEffectHandler(AnatomyNodeHealthState node) => this.node = node;
        internal int Applications { get; private set; }
        protected override bool ApplyTyped(
            SurgeryOrder order, TestEffect effect,
            BuildableObject facility, out DomainFailure failure)
        {
            node.currentHealth += 5f;
            Applications++;
            failure = DomainFailure.None;
            return true;
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object[], object> handler)
        where T : class
    {
        T value = DispatchProxy.Create<T, ScenarioDispatchProxy>();
        ((ScenarioDispatchProxy)(object)value).Handler = handler;
        return value;
    }

    public class ScenarioDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo method, object[] args) =>
            Handler(method, args);
    }

    private sealed class UncertainFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly bool commitBeforeUnknown;
        private bool injected;

        internal UncertainFactPublisher(
            DomainFactDispatcher inner,
            bool commitBeforeUnknown)
        {
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));
            this.commitBeforeUnknown = commitBeforeUnknown;
        }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
                proposed,
                out lease,
                out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (injected)
                return inner.Publish(lease, actual);
            injected = true;
            if (commitBeforeUnknown)
                _ = inner.Publish(lease, actual);
            return DomainFactDelivery.Uncertain(
                commitBeforeUnknown
                    ? "injected-surgery-unknown-after-commit"
                    : "injected-surgery-unknown-before-commit");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.ReplayBatch(
                facts,
                results,
                out failure);
    }

    private sealed class FaultingRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;
        internal FaultingRecorder(IGameplayOutcomeRecorder inner) =>
            this.inner = inner;
        internal bool RejectReserve { get; set; }
        internal bool RejectCommit { get; set; }
        internal bool FaultDelivery { get; set; }
        public OutcomePrepareResult TryPrepare<T>(
            in T receipt, out PreparedOutcomeToken prepared) =>
            inner.TryPrepare(receipt, out prepared);
        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation)
        {
            if (!RejectReserve)
                return inner.TryReserve(requirements, out reservation);
            reservation = default;
            return new OutcomePrepareResult(
                OutcomePrepareCode.CapacityDeferred,
                "injected-surgery-prepare-rejection");
        }
        public OutcomePrepareResult TryWriteReserved<T>(
            in T receipt, in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared) =>
            inner.TryWriteReserved(receipt, reservation, out prepared);
        public void CancelReservation(in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);
        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            inner.CancelPrepared(prepared);
        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed)
        {
            if (!RejectCommit)
                return inner.CommitPrepared(
                    prepared, expectedOwnerRevision, out committed);
            committed = default;
            return new OutcomeCommitResult(
                OutcomeCommitCode.OwnerRevisionMismatch,
                "injected-surgery-commit-rejection");
        }
        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] expectedOwnerRevisions,
            CommittedOutcomeToken[] committed)
        {
            if (!RejectCommit)
            {
                return inner.CommitPreparedBatch(
                    prepared,
                    expectedOwnerRevisions,
                    committed);
            }
            Array.Clear(committed, 0, committed.Length);
            return new OutcomeCommitResult(
                OutcomeCommitCode.OwnerRevisionMismatch,
                "injected-surgery-commit-rejection");
        }
        public OutcomeDeliveryResult TryDeliver(in CommittedOutcomeToken committed) =>
            FaultDelivery
                ? new OutcomeDeliveryResult(
                    OutcomeDeliveryCode.PendingDeliveryFault,
                    committed.OutcomeId,
                    "injected-surgery-delivery-fault")
                : inner.TryDeliver(committed);
        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            inner.Acknowledge(committed);
        public int RetryPendingDeliveries(int maximumCount) =>
            inner.RetryPendingDeliveries(maximumCount);
    }
}
#endif
