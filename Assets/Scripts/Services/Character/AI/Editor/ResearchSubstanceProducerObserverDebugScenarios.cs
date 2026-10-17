#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class StaffDutyDebugScenarios
{
    // Actual production iterators with explicit controlled service results. This
    // is not a natural AI, physical consumables transaction, or whole-world test.
    public static void RunResearchSubstanceObserversForBatch()
    {
        ResearchSubstanceActivityProjectionDebugScenarios.Run();
        Debug.Log("B101_PROJECTION_AND_OBSERVER_LIFETIME=PASS");
        VerifyResearchProducerObservers();
        Debug.Log("B101_RESEARCH_PRODUCER_SUCCESS_REJECT_DEFER_CANCEL=PASS");
        VerifySubstanceProducerObserver(true);
        VerifySubstanceProducerObserver(false);
        Debug.Log("B101_SUBSTANCE_PRODUCER_SUCCESS_REJECT=PASS");
    }

    private static void VerifyResearchProducerObservers()
    {
        CharacterActor actor = CreateStaff("B101 Research", false, true);
        var host = new GameObject("B101 Research Table");
        try
        {
            var target = host.AddComponent<BuildableObject>();
            target.RestorePersistentIdentity(new BuildingInstanceId("building:b101-research"));
            var service = new ObserverResearchService();
            var handler = new ResearchWorkExecutionHandler(service);
            var facts = new List<WorkExecutionChangedEvent>();
            var activities = new List<CharacterActivityEvent>();
            using var subscription = CharacterAiEditorTestDependencies.GameEvents
                .Subscribe<WorkExecutionChangedEvent>(value =>
                {
                    if (value.Character.Value != CharacterPersistentIdentity.Require(actor).Value) return;
                    B101Require(service.Calls > 0, "Research published before its owner returned.");
                    facts.Add(value);
                });
            void Capture(CharacterLogEntry entry)
            {
                if (entry.Activity?.ActionId == BuiltInWorkTypeIds.Research.Value)
                    activities.Add(entry.Activity);
            }
            actor.LogComponent.OnLogAdded += Capture;
            try
            {
                RunCase(true, false, false, true, WorkExecutionTransition.ResearchProgressed);
                RunCase(true, true, false, true, WorkExecutionTransition.ResearchCompleted);
                RunCase(false, false, false, true, WorkExecutionTransition.ResearchRejected);
                RunCase(false, false, true, true, null);
                RunCase(true, false, false, false, null);
            }
            finally { actor.LogComponent.OnLogAdded -= Capture; }

            void RunCase(bool success, bool completed, bool deferred, bool canContinue,
                WorkExecutionTransition? expected)
            {
                service.Calls = 0;
                service.Result = new BlueprintResearchWorkResult(success, null, 1f,
                    completed ? 4f : 1f, 4f, completed, "B101 research", deferred);
                facts.Clear(); activities.Clear();
                var leases = new List<IDisposable>();
                int workCalls = 0;
                IEnumerator ApprovedWork(float amount, string label, float multiplier)
                {
                    B101Require(amount > 0f && label == "연구" && multiplier > 0f,
                        "Research skipped its approved-work request.");
                    workCalls++;
                    yield return null;
                }
                var context = new WorkExecutionContext(101, actor.GetComponent<AbilityWork>(),
                    actor, target, BuiltInWorkTypeIds.Research, ApprovedWork, () => canContinue,
                    registerCancellationResource: leases.Add);
                var result = new WorkExecutionResult();
                try { DrainB101(handler.Execute(context, result)); }
                finally { foreach (IDisposable lease in leases) lease.Dispose(); }
                B101Require(workCalls == 1 && leases.Count == 1
                    && service.Calls == (canContinue ? 1 : 0)
                    && result.CompletedSuccessfully == (canContinue && success),
                    "Research producer changed work/cancellation/result semantics.");
                B101Require(facts.Count == (expected.HasValue ? 1 : 0)
                    && activities.Count == facts.Count,
                    "Research notification missing/duplicated or emitted on defer/cancel.");
                if (expected.HasValue)
                    B101Require(facts[0].Transition == expected.Value
                        && facts[0].TargetId == target.RequirePersistentInstanceId().Value
                        && facts[0].WorkLabel == "B101 research"
                        && Mathf.Approximately(facts[0].Progress, completed ? 1f : 0.25f),
                        "Research producer published incorrect applied facts.");
            }
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(actor.gameObject); }
    }

    private static void VerifySubstanceProducerObserver(bool accept)
    {
        CharacterActor actor = CreateStaff("B101 Substance " + accept, false, true);
        try
        {
            var ability = actor.gameObject.AddComponent<AbilityUseSubstance>();
            AbilityUseSubstance.Ensure(actor);
            // Ensure only adds components in Play; this explicit Edit fixture owns its inventory.
            var inventory = actor.gameObject.AddComponent<CharacterCarryInventory>();
            inventory.Construct(new CharacterCarryInventoryRegistry());
            const string itemId = "item:qa:b101-tonic";
            const string stackId = "stack:qa:b101-tonic";
            B101Field(inventory, "carriedItems", new List<CharacterCarriedItemSaveData>
            {
                new() { itemId = itemId, carriedStackId = stackId,
                    sourceStackId = stackId, quantity = 1 }
            });
            ICharacterSubstanceRuntime port = DispatchProxy.Create<ICharacterSubstanceRuntime, B101SubstancePort>();
            var control = (B101SubstancePort)(object)port;
            control.Accept = accept; control.Inventory = inventory;
            var bridge = actor.GetComponent<CharacterActorRuntimeBridge>();
            typeof(CharacterActorRuntimeBridge).GetProperty("SubstanceRuntime").SetValue(bridge, port);
            typeof(CharacterActorRuntimeBridge).GetProperty("WorldItemStackRuntime").SetValue(bridge,
                DispatchProxy.Create<IWorldItemStackRuntime, B101NoWorldCalls>());
            B101Field(ability, "request", new CharacterSubstanceUseRequest("substance:b101",
                new ItemDefinitionId(itemId), "진정제", SubstanceUseClass.Medicine,
                1f, true, false, "explicit controlled fixture"));
            B101Field(ability, "useExecutionActive", true);
            int facts = 0, activities = 0;
            using var subscription = CharacterAiEditorTestDependencies.GameEvents
                .Subscribe<SubstanceUseCompletedEvent>(value =>
                {
                    if (value.Actor != actor) return;
                    B101Require(control.Calls == 1 && inventory.CountItem(itemId) == 0
                        && value.Item.Value == itemId && value.Stack.Value == stackId
                        && value.Operation.Value == control.Command.OperationId.Value,
                        "Substance notification preceded acceptance or changed identity.");
                    facts++;
                });
            void Capture(CharacterLogEntry entry)
            {
                if (entry.Activity?.FactText != "진정제을 복용했다.") return;
                B101Require(!entry.Activity.NarrativeEligible,
                    "Routine substance UI activity became a narrative memory.");
                activities++;
            }
            actor.LogComponent.OnLogAdded += Capture;
            try
            {
                DrainB101((IEnumerator)typeof(AbilityUseSubstance)
                    .GetMethod("UseRoutine", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ability, null));
            }
            finally { actor.LogComponent.OnLogAdded -= Capture; }
            B101Require(control.Calls == 1 && control.Command.IsValid
                && control.Command.CharacterId.Value == CharacterPersistentIdentity.Require(actor).Value
                && control.Command.MedicalContext && !control.Command.CombatContext
                && facts == (accept ? 1 : 0) && activities == facts
                && inventory.CountItem(itemId) == (accept ? 0 : 1)
                && !ability.IsUsingSubstance && !ability.ActiveRequest.IsValid,
                "Substance routine lost acceptance/rejection, activity or terminal semantics.");
        }
        finally { Object.DestroyImmediate(actor.gameObject); }
    }

    public class B101NoWorldCalls : DispatchProxy
    {
        protected override object Invoke(MethodInfo method, object[] args) =>
            throw new InvalidOperationException("Unexpected physical-world call: " + method.Name);
    }

    public class B101SubstancePort : DispatchProxy
    {
        public bool Accept;
        public int Calls;
        public CharacterCarryInventory Inventory;
        public ConsumeSubstanceCommand Command;
        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name != "TryConsume")
                throw new InvalidOperationException("Unexpected substance call: " + method.Name);
            Calls++; Command = (ConsumeSubstanceCommand)args[0];
            if (Accept)
                B101Require(Inventory.TryConsumeItem(Command.ItemDefinitionId.Value, 1),
                    "Controlled consumption port failed to remove the prepared inventory unit.");
            args[1] = new SubstanceUseResult(Accept,
                Accept ? CharacterConsumablesFailureCode.None : CharacterConsumablesFailureCode.PolicyForbidden,
                "substance:b101", Command.ItemDefinitionId, Command.ItemStackId,
                "진정제", 0f, 0f, false, false, string.Empty);
            return Accept;
        }
    }

    private sealed class ObserverResearchService : IBlueprintResearchWorkService
    {
        public int Calls;
        public BlueprintResearchWorkResult Result;
        public bool HasResearchWorkFor(BuildableObject target) => true;
        public BlueprintResearchWorkResult ApplyResearchWork(CharacterActor actor,
            BuildableObject target, float seconds) => throw new InvalidOperationException("Expected approved work.");
        public BlueprintResearchWorkResult ApplyApprovedResearchWork(CharacterActor actor,
            BuildableObject target, float work, DurableFacilityEquipmentUseContext equipment = null)
        {
            B101Require(work > 0f, "Research contribution missing.");
            Calls++; return Result;
        }
    }

    private static void B101Field(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void DrainB101(IEnumerator iterator)
    {
        try
        {
            for (int step = 0; step < 40; step++)
            {
                if (!iterator.MoveNext()) return;
                if (iterator.Current is IEnumerator child) DrainB101(child);
            }
            throw new InvalidOperationException("B101 controlled iterator did not terminate.");
        }
        finally { (iterator as IDisposable)?.Dispose(); }
    }

    private static void B101Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
