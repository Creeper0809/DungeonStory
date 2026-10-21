#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using UnityEngine;

public static class SurgeryReservationFailureDebugScenarios
{
    public static void RunForBatch()
    {
        Require(Application.isBatchMode, "Batch execution required.");
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject("Reservation failure doctor");
        CharacterSO data = CharacterAiPlanDebugFixtures.CreateCharacterData(
            CharacterType.NPC, "Reservation failure doctor", "Human");
        try
        {
            CharacterActor doctor = actorObject.GetComponent<CharacterActor>();
            doctor.Initialize(data);
            doctor.SetLifecycleState(CharacterLifecycleState.Active);
            Require(doctor.CanRunAi && !doctor.IsDead,
                "Doctor fixture must reach reservation guards: lifecycle="
                + doctor.CurrentLifecycleState
                + "; active=" + doctor.gameObject.activeInHierarchy
                + "; unpublished=" + doctor.IsUnpublishedComposition
                + "; detached=" + doctor.IsDetachedRestoreCandidate
                + "; dead=" + doctor.IsDead + ".");

            var state = new SurgeryAggregateStateStore(new DungeonRuntimeAggregateRootStore());
            ISurgicalProcedureCatalog procedures = Proxy<ISurgicalProcedureCatalog>((method, args) =>
            {
                Require(method.Name == "TryGet", "Unexpected procedure query: " + method.Name);
                args[1] = null;
                return false;
            });
            // Bind only the real entry's rejection dependencies. Any accidental progress
            // beyond these guards fails instead of being accepted by a permissive fake.
            var runtime = (SurgeryRuntime)FormatterServices.GetUninitializedObject(typeof(SurgeryRuntime));
            Set(runtime, "stateStore", state);
            Set(runtime, "procedures", procedures);
            Set(runtime, "facilities", Proxy<ISurgicalFacilityQuery>((method, _) =>
            {
                Require(method.Name == "GetFacilityId", "Unexpected facility query: " + method.Name);
                return "building:reservation-failure";
            }));
            Set(runtime, "captivity", Proxy<ICaptivityRuntime>((method, _) =>
            {
                Require(method.Name == "IsCaptive", "Unexpected captivity query: " + method.Name);
                return false;
            }));
            Type environmentType = typeof(SurgeryRuntime).Assembly.GetType("SurgeryEnvironmentRuntime", true);
            object environment = FormatterServices.GetUninitializedObject(environmentType);
            Set(environment, "procedures", procedures);
            Set(runtime, "surgeryEnvironment", environment);

            Verify("character:preferred", "", FailureCode.SurgeryPreferredDoctorOnly, "character:preferred");
            Verify("", "character:assigned", FailureCode.SurgeryDoctorAlreadyAssigned, "character:assigned");
            Verify("", "", FailureCode.SurgeryProcedureMissing, "procedure:absent");
            Debug.Log("SURGERY_RESERVATION_FAILURES=PASS; branches=3; attempts=6; stateUnchanged=True");

            void Verify(string preferred, string assigned, FailureCode code, string parameter)
            {
                var order = new SurgeryOrder
                {
                    orderId = "surgery:reservation-failure", procedureId = "procedure:absent",
                    facilityId = "building:reservation-failure", state = SurgeryOrderState.Procedure,
                    preferredDoctorId = preferred, doctorId = assigned, completedWork = 4f,
                    requiredWork = 10f, createdAt = 1f
                };
                state.State.Orders.Clear();
                state.State.Orders.Add(order);
                string before = JsonUtility.ToJson(order);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    bool accepted = runtime.TryReserveWork(null, doctor, out SurgeryOrder reserved, out DomainFailure failure);
                    Require(!accepted && reserved == null, "Rejected reservation exposed a work order.");
                    Require(failure.Equals(new DomainFailure(code, parameter)), "Rejection lost its reason or context: " + code);
                    Require(state.State.Orders.Count == 1 && ReferenceEquals(state.State.Orders[0], order)
                        && JsonUtility.ToJson(order) == before, "Rejected reservation mutated authoritative order state.");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object[], object> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, OrganPreservationRestoreJoinFixture.ConfigurableDispatchProxy>();
        ((OrganPreservationRestoreJoinFixture.ConfigurableDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "Missing existing field: " + name);
        field.SetValue(target, value);
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
#endif
