#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

// Root-owned focused contract witness. Detached orders and a controlled field
// exercise production classification/environment/save/UI, not natural AI surgery.
public static class Wim023EmergencyCauseDebugScenarios
{
    public static string RunFocused()
    {
        var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope != null, "Main dungeon scope is required.");
        var content = scope.Container.Resolve<SurgeryContentServices>();
        var world = scope.Container.Resolve<SurgeryWorldServices>();
        var resources = scope.Container.Resolve<SurgeryResourceServices>();
        var execution = scope.Container.Resolve<SurgeryExecutionServices>();
        var suture = Procedure("procedure:emergency-suture");
        var foreignBody = Procedure("procedure:foreign-body-removal");
        var amputation = Procedure("procedure:amputation");
        var prosthetic = content.Procedures.Procedures.First(p => p.Kind == SurgicalProcedureKind.InstallProsthetic);
        var profile = content.AnatomyProfiles.GetDefaultHumanoid();
        var definition = profile.Nodes.First(n => n.Removable && !n.Vital);
        var node = new AnatomyNodeHealthState
        {
            nodeId = definition.NodeId, maxHealth = 100f, currentHealth = 100f
        };
        ExpectCause(suture, SurgeryEmergencyCause.None);
        node.bleedingPerSecond = .079f;
        ExpectCause(suture, SurgeryEmergencyCause.None);
        node.bleedingPerSecond = .08f;
        ExpectCause(suture, SurgeryEmergencyCause.AcuteBleeding);
        ExpectCause(prosthetic, SurgeryEmergencyCause.None);
        node.bleedingPerSecond = 0f;
        node.infection = 34.99f;
        ExpectCause(foreignBody, SurgeryEmergencyCause.None);
        node.infection = 35f;
        ExpectCause(foreignBody, SurgeryEmergencyCause.AcuteInfection);
        node.infection = 79.99f;
        ExpectCause(amputation, SurgeryEmergencyCause.None);
        node.infection = 80f;
        ExpectCause(amputation, SurgeryEmergencyCause.CriticalNonVitalNode);
        node.infection = 0f;
        node.currentHealth = 1f;
        ExpectCause(amputation, SurgeryEmergencyCause.CriticalNonVitalNode);
        node.currentHealth = 1.01f;
        ExpectCause(amputation, SurgeryEmergencyCause.None);
        node.currentHealth = 100f;

        var facility = world.Buildings.Buildings.First(b => b != null && !b.isDestroy);
        var cell = new EnvironmentalCellSnapshot(facility.centerPos, 45f, 20f, 10f);
        float step = 0f;
        int recoveryRequests = 0;
        var field = Proxy<IEnvironmentalFieldQuery>((method, args) =>
        {
            if (method.Name == nameof(IEnvironmentalFieldQuery.TryGetCell))
            {
                args[1] = cell;
                return true;
            }
            throw new InvalidOperationException("Unexpected field query: " + method.Name);
        });
        var clock = Proxy<IGameClock>((method, _) => method.Name switch
        {
            "get_DeltaTime" => step,
            "get_Time" => 0f,
            "get_FrameCount" => 0,
            "get_IsPaused" => false,
            _ => throw new InvalidOperationException("Unexpected clock query: " + method.Name)
        });
        var workforce = Proxy<IWorkforceReplanService>((method, _) =>
        {
            if (method.Name != nameof(IWorkforceReplanService.RequestOneWorkerToReplanFor))
                throw new InvalidOperationException("Unexpected workforce call: " + method.Name);
            recoveryRequests++;
            return null;
        });
        var risk = new SurgeryEnvironmentRiskEvaluator(field, execution.EnvironmentStatus, world.Characters);
        var controlledResources = new SurgeryResourceServices(
            resources.ExtractionLedger, resources.Items, resources.Anatomy, resources.WildlifeAnatomy,
            resources.Research, workforce, resources.ProcessFluids, field,
            resources.DestinationClaims, resources.DestinationClaimCommands, resources.MaterialDestinations,
            resources.MaterialTerminal, resources.BatchDispositions, resources.PhysicalMass,
            resources.PlannedOutputAdmission, resources.PlannedOutputPublication, resources.TareDispositions);
        var controlledExecution = new SurgeryExecutionServices(clock,
            scope.Container.Resolve<IRandomStreamProvider>(), execution.EnvironmentStatus, risk);
        var environment = new EnvironmentProbe(content, world, controlledResources, controlledExecution);
        var normal = Order(prosthetic, SurgeryEmergencyCause.None);
        Require(!environment.IsEmergency(normal), "Automatic permission bypassed ordinary surgery safety.");
        Require(!environment.IsEmergency(Order(prosthetic, SurgeryEmergencyCause.AcuteBleeding)),
            "Forged acute cause bypassed prosthetic surgery safety.");
        var nameOnly = Order(suture, SurgeryEmergencyCause.None);
        Require(!environment.IsEmergency(nameOnly), "Procedure name alone bypassed safety.");
        var emergency = Order(suture, SurgeryEmergencyCause.AcuteBleeding);
        Require(environment.IsEmergency(emergency)
            && environment.GetUrgency(emergency) == (int)MedicalProcedureUrgency.Emergency,
            "Actual emergency cause did not drive effective urgency.");

        var unsafeRisk = risk.Evaluate(facility.centerPos, null, normal.subject);
        Require(unsafeRisk.Extreme && !unsafeRisk.Normal, "Controlled field is not severely unsafe.");
        normal.state = SurgeryOrderState.Procedure;
        normal.completedWork = 3f;
        environment.EnterWait(normal, SurgeryOrderState.Procedure, unsafeRisk);
        Require(normal.state == SurgeryOrderState.EnvironmentWaiting && normal.completedWork == 3f,
            "Wait reset clinical progress.");
        step = 10f;
        environment.TickWaiting(normal, facility);
        Require(normal.state == SurgeryOrderState.EnvironmentWaiting && normal.environmentStableSeconds == 0f,
            "Unsafe time incorrectly counted as stabilization.");
        cell = new EnvironmentalCellSnapshot(facility.centerPos, 22f, 100f, 100f);
        step = 4f;
        environment.TickWaiting(normal, facility);
        Require(normal.state == SurgeryOrderState.EnvironmentWaiting, "Normal environment resumed before five seconds.");
        step = 1f;
        environment.TickWaiting(normal, facility);
        Require(normal.state == SurgeryOrderState.Procedure && normal.completedWork == 3f,
            "Stable environment did not resume the existing stage/progress.");
        cell = new EnvironmentalCellSnapshot(facility.centerPos, 45f, 20f, 10f);
        emergency.state = SurgeryOrderState.Procedure;
        emergency.risk.successChance = .9f;
        environment.ApplyRisk(emergency, null, facility);
        Require(emergency.state == SurgeryOrderState.Procedure
            && emergency.statusData.code == SurgeryStatusCode.EmergencyProcedureContinuing
            && !string.IsNullOrEmpty(emergency.statusData.primaryId)
            && emergency.risk.environmentSuccessPenalty > 0f,
            "Emergency continuation lost cause or waived environmental risk.");
        string notice = CharacterSurgeryUiText.LocalizeStatus(emergency.statusData);
        Require(!string.IsNullOrWhiteSpace(notice) && notice.Contains("45") && notice.Contains("20"),
            "Actual localized risk presentation lost environmental values.");
        Require(notice.Contains("출혈") || notice.IndexOf("bleeding", StringComparison.OrdinalIgnoreCase) >= 0,
            "Actual localized notice omitted the clinical reason.");

        var queued = Order(suture, SurgeryEmergencyCause.AcuteBleeding);
        var cloned = SurgeryStateCloner.CloneOrder(queued);
        Require(cloned.emergencyCause == SurgeryEmergencyCause.AcuteBleeding, "Order clone lost emergency cause.");
        var payload = new DungeonSurgerySaveData { orderSequence = 1 };
        payload.orders.Add(cloned);
        string json = JsonUtility.ToJson(payload);
        var restored = JsonUtility.FromJson<DungeonSurgerySaveData>(json);
        var good = Validate(restored);
        Require(good.Success && environment.IsEmergency(restored.orders.Single()),
            "Current queued emergency save failed: " + string.Join(";", good.Errors));
        var restoredState = SurgerySaveValidation.CreateState(restored);
        Require(restoredState.Orders.Single().emergencyCause == SurgeryEmergencyCause.AcuteBleeding,
            "Restore state construction lost the queued emergency cause.");
        // Later symptom recovery must not rewrite the saved admission cause.
        ExpectCause(suture, SurgeryEmergencyCause.None);
        Require(restored.orders[0].emergencyCause == SurgeryEmergencyCause.AcuteBleeding,
            "Restored order cause was recomputed from healed anatomy.");
        restored.orders[0].emergencyCause = (SurgeryEmergencyCause)999;
        Require(!Validate(restored).Success, "Unknown saved emergency cause accepted.");
        restored.orders[0].emergencyCause = SurgeryEmergencyCause.AcuteInfection;
        Require(!Validate(restored).Success, "Mismatched saved cause/procedure accepted.");
        restored.orders[0] = Order(prosthetic, SurgeryEmergencyCause.AcuteBleeding);
        Require(!Validate(restored).Success, "Forged prosthetic emergency accepted by save validation.");
        restored.orders[0] = Order(suture, SurgeryEmergencyCause.AcuteBleeding);
        restored.orders[0].subject.anatomyProfileId = "anatomy:qa:unknown";
        Require(!Validate(restored).Success, "Unknown explicit profile silently fell back during restore.");
        Require(recoveryRequests > 0, "Environment recovery did not request existing workforce path.");
        return "PASS clinical thresholds;permission/name/forged-prosthetic no-bypass;effective urgency;unsafe wait/stable5s resume;emergency risk+localized cause;clone/current queued JSON+invalid4;scope=controlled production gates, not schedule producer/natural UI/AI surgery;notice=" + notice;

        SurgicalProcedureSO Procedure(string id)
        {
            Require(content.Procedures.TryGet(id, out var found), "Missing authored procedure: " + id);
            return found;
        }
        void ExpectCause(SurgicalProcedureSO procedure, SurgeryEmergencyCause expected) =>
            Require(SurgeryEmergencyCauseRules.Classify(procedure, node, definition) == expected,
                "Clinical cause mismatch for " + procedure.ProcedureId);
        SurgeryOrder Order(SurgicalProcedureSO procedure, SurgeryEmergencyCause cause) => new()
        {
            orderId = "surgery:1", procedureId = procedure.ProcedureId,
            emergencyCause = cause, targetNodeId = definition.NodeId,
            subject = new SurgicalSubjectRef
            {
                subjectId = "character:qa:wim023-emergency", automaticEmergencyDefault = true,
                anatomyProfileId = profile.ProfileId, speciesId = profile.SpeciesIds.First()
            },
            facilityId = content.Facilities.GetFacilityId(facility),
            state = SurgeryOrderState.PatientWaiting, requiredWork = procedure.RequiredWork
        };
        DungeonGameRestoreReport Validate(DungeonSurgerySaveData saved)
        {
            var report = new DungeonGameRestoreReport();
            SurgerySaveValidation.Validate(saved, content.Procedures, content.AnatomyProfiles, report);
            return report;
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object[], object> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, OrganPreservationRestoreJoinFixture.ConfigurableDispatchProxy>();
        ((OrganPreservationRestoreJoinFixture.ConfigurableDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    // Exact existing internal boundary, invoked only by this Editor test. No
    // production visibility changes or replacement environment implementation.
    private sealed class EnvironmentProbe
    {
        private readonly Type type = typeof(SurgeryRuntime).Assembly.GetType("SurgeryEnvironmentRuntime", true);
        private readonly object target;
        public EnvironmentProbe(SurgeryContentServices content, SurgeryWorldServices world,
            SurgeryResourceServices resources, SurgeryExecutionServices execution)
        {
            target = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { content, world, resources, execution }, null);
        }
        public bool IsEmergency(SurgeryOrder order) => (bool)Invoke("IsEmergency", order);
        public int GetUrgency(SurgeryOrder order) => (int)Invoke("GetUrgency", order);
        public void EnterWait(SurgeryOrder order, SurgeryOrderState stage, SurgeryEnvironmentRiskSnapshot risk) =>
            Invoke("EnterWait", order, stage, risk);
        public void TickWaiting(SurgeryOrder order, BuildableObject facility) => Invoke("TickWaiting", order, facility);
        public void ApplyRisk(SurgeryOrder order, CharacterActor doctor, BuildableObject facility) =>
            Invoke("ApplyRisk", order, doctor, facility);
        private object Invoke(string name, params object[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Require(method != null, "Existing environment seam is missing: " + name);
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
