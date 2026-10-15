#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Environment;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class WorkTerminalDetailW07DebugScenarios
{
    public static JObject RunAll()
    {
        PlumbingWorkExecutionHandlerDebugScenarios.RunOrThrow();
        JObject operate = VerifyOperateActualEffectAndUnsupportedCommand();
        Require(
            EnvironmentFirePhysicalProductionEntryDebugScenarios.RunAll(
                log: true),
            "Environmental-fire physical production-entry regression failed.");
        JObject fire = VerifyFireDomainTerminalReference();
        JObject primitive =
            MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
                .RunW07PrimitiveFieldMealNoDuplicate();

        return new JObject
        {
            ["passed"] = true,
            ["branches"] = new JArray
            {
                Branch(
                    "plumbing-second-command-failure",
                    "real handler rejects the second command without hiding the failure"),
                Branch(
                    "plumbing-partial-completion",
                    "real handler preserves the already-applied first command and reports partial work"),
                new JObject
                {
                    ["id"] = "operate-actual-effect-vs-unsupported-command",
                    ["passed"] = true,
                    ["evidence"] = operate
                },
                new JObject
                {
                    ["id"] = "no-duplicate-existing-physical-fire-result",
                    ["passed"] = true,
                    ["evidence"] = new JObject
                    {
                        ["workTerminal"] = fire,
                        ["primitiveFieldMeal"] = primitive,
                        ["actualFireOwner"] =
                            "one fuel, one water, and one canonical suppression result"
                    }
                }
            },
            ["scope"] = "real plumbing handler; real building fallback; real "
                + "environment-fire/physical owners plus work terminal contract; "
                + "real primitive field-meal outcome transaction"
        };
    }

    private static JObject VerifyOperateActualEffectAndUnsupportedCommand()
    {
        using BuildingFixture supported = new(
            "W07 Supported Nursery",
            970701,
            ResearchFacilityCommandKind.NurseryCare);
        IBuildingVisitorPort visitor = RecordingVisitor.Create(
            out RecordingVisitor recording);
        int produced = ModularFacilityRuntimeEffects.ApplyWorkCompleted(
            visitor,
            supported.Building,
            BuiltInWorkTypeIds.Operate,
            1f);
        Require(
            produced == 0
            && supported.Building.FacilityState.completedWorkCycles == 1
            && recording.Recoveries.Count == 1
            && recording.Activities.Count == 1,
            "Supported nursery operation did not apply one recovery, one "
            + "completion activity, and one completed work cycle.");

        using BuildingFixture unsupported = new(
            "W07 Unsupported Command",
            970702,
            (ResearchFacilityCommandKind)int.MaxValue);
        IBuildingVisitorPort rejectedVisitor = RecordingVisitor.Create(
            out RecordingVisitor rejected);
        string failure = string.Empty;
        try
        {
            ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                rejectedVisitor,
                unsupported.Building,
                BuiltInWorkTypeIds.Operate,
                1f);
        }
        catch (InvalidOperationException error)
        {
            failure = error.Message;
        }
        Require(
            failure.Contains("has no execution contract", StringComparison.Ordinal)
            && unsupported.Building.FacilityState.completedWorkCycles == 0
            && rejected.Recoveries.Count == 0
            && rejected.Activities.Count == 0,
            "Unsupported research-facility command left a false work cycle or "
            + "operator side effect.");

        return new JObject
        {
            ["supportedProduced"] = produced,
            ["supportedCompletedCycles"] =
                supported.Building.FacilityState.completedWorkCycles,
            ["supportedRecoveries"] = recording.Recoveries.Count,
            ["supportedActivities"] = recording.Activities.Count,
            ["unsupportedCompletedCycles"] =
                unsupported.Building.FacilityState.completedWorkCycles,
            ["unsupportedRecoveries"] = rejected.Recoveries.Count,
            ["unsupportedActivities"] = rejected.Activities.Count,
            ["unsupportedFailure"] = failure
        };
    }

    private static JObject VerifyFireDomainTerminalReference()
    {
        const string FireId = "environmental-fire:w07";
        const string TargetId = "building:w07:fire-target";
        const string OperationId = "qa:w07:fire-suppression";
        string resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.FireSuppressionProducer,
            new GameplayOperationId(OperationId),
            1L,
            0).ToString();
        var fireRuntime = new FixedFireSuppressionRuntime(
            FireId,
            TargetId,
            OperationId,
            resultKey);
        var handler = new ThreatMitigationWorkExecutionHandler(
            DefaultInterfaceProxy.Create<IOffenseUrgentMitigationRuntime>(),
            fireRuntime);
        GameObject host = new("W07 Fire Work");
        host.SetActive(false);
        var cancellationResources = new List<IDisposable>();
        try
        {
            AbilityWork work = host.AddComponent<AbilityWork>();
            var context = new WorkExecutionContext(
                1,
                work,
                null,
                null,
                BuiltInWorkTypeIds.ThreatMitigation,
                CompleteWorkAmount,
                () => true,
                registerCancellationResource: cancellationResources.Add);
            var result = new WorkExecutionResult();
            Drain(handler.Execute(context, result));
            Require(
                result.CompletedSuccessfully
                && result.CompletionEffectsAlreadyApplied
                && result.CompletionIdentityAlreadyPublished
                && string.Equals(
                    result.TerminalOwnerId,
                    EnvironmentOutcomeIds.FireSuppressionProducer,
                    StringComparison.Ordinal)
                && string.Equals(
                    result.ChildResultKey,
                    resultKey,
                    StringComparison.Ordinal)
                && string.Equals(
                    result.TargetEntityId,
                    TargetId,
                    StringComparison.Ordinal)
                && result.ContributionWorkUnits.HasValue
                && Mathf.Approximately(
                    result.ContributionWorkUnits.Value,
                    fireRuntime.Work.RequiredWork),
                "Threat-mitigation fire work did not preserve the domain-owned "
                + "terminal identity/effect contract.");
            return new JObject
            {
                ["completedSuccessfully"] = result.CompletedSuccessfully,
                ["completionEffectsAlreadyApplied"] =
                    result.CompletionEffectsAlreadyApplied,
                ["completionIdentityAlreadyPublished"] =
                    result.CompletionIdentityAlreadyPublished,
                ["terminalOwnerId"] = result.TerminalOwnerId,
                ["childResultKey"] = result.ChildResultKey,
                ["targetEntityId"] = result.TargetEntityId,
                ["contributionWorkUnits"] =
                    result.ContributionWorkUnits.Value
            };
        }
        finally
        {
            foreach (IDisposable resource in cancellationResources)
                resource?.Dispose();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static JObject Branch(string id, string assertion) => new()
    {
        ["id"] = id,
        ["passed"] = true,
        ["assertion"] = assertion
    };

    private static IEnumerator CompleteWorkAmount(
        float requiredWork,
        string label,
        float extraMultiplier)
    {
        yield break;
    }

    private static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext())
        {
            if (routine.Current is IEnumerator nested)
                Drain(nested);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class BuildingFixture : IDisposable
    {
        private readonly BuildingSO data;
        private readonly GameObject host;

        internal BuildingFixture(
            string displayName,
            int definitionId,
            ResearchFacilityCommandKind command)
        {
            data = ScriptableObject.CreateInstance<BuildingSO>();
            data.id = definitionId;
            data.objectName = displayName;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = BuildingCategory.Special;
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
            data.ReplaceAbilities(new BuildingAbilityCollection());
            data.ConfigureGameplayExecution(
                FacilityUseClassification.DomainCommand,
                command);

            host = new GameObject(displayName);
            Building = host.AddComponent<BuildableObject>();
            Building.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(Building);
            Building.Initialization(data, Vector2Int.zero);
            SetDispatcher(
                Building,
                new BuildingAbilityRuntimeDispatcher(
                    Array.Empty<IBuildingAbilityWorkCompletedHandler>(),
                    new IBuildingWorkCompletionFallbackHandler[]
                    {
                        new ResearchFacilityOperationFallbackHandler(
                            DefaultInterfaceProxy.Create<IApparelWorkOrderCommand>(),
                            DefaultInterfaceProxy.Create<IApparelWorkOrderQuery>())
                    }));
        }

        internal BuildableObject Building { get; }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(data);
        }

        private static void SetDispatcher(
            BuildableObject building,
            IBuildingAbilityRuntimeDispatcher dispatcher)
        {
            FieldInfo field = typeof(BuildableObject).GetField(
                "abilityRuntimeDispatcher",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    typeof(BuildableObject).FullName,
                    "abilityRuntimeDispatcher");
            }
            field.SetValue(building, dispatcher);
        }
    }

    public class RecordingVisitor : DispatchProxy
    {
        public List<BuildingNeedRecoverySnapshot> Recoveries { get; } = new();
        public List<BuildingActivitySnapshot> Activities { get; } = new();

        public static IBuildingVisitorPort Create(
            out RecordingVisitor recording)
        {
            IBuildingVisitorPort visitor = DispatchProxy.Create<
                IBuildingVisitorPort,
                RecordingVisitor>();
            recording = (RecordingVisitor)(object)visitor;
            return visitor;
        }

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] arguments)
        {
            if (targetMethod?.Name == nameof(IBuildingVisitorPort.ApplyNeedRecovery)
                && arguments?.Length == 1
                && arguments[0] is BuildingNeedRecoverySnapshot recovery)
            {
                Recoveries.Add(recovery);
            }
            else if (targetMethod?.Name == nameof(IBuildingVisitorPort.RecordActivity)
                     && arguments?.Length == 2
                     && arguments[1] is BuildingActivitySnapshot activity)
            {
                Activities.Add(activity);
            }
            return DefaultInterfaceProxy.DefaultReturn(targetMethod, arguments);
        }
    }

    public class DefaultInterfaceProxy : DispatchProxy
    {
        public static TContract Create<TContract>() where TContract : class =>
            DispatchProxy.Create<TContract, DefaultInterfaceProxy>();

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] arguments) => DefaultReturn(targetMethod, arguments);

        internal static object DefaultReturn(
            MethodInfo targetMethod,
            object[] arguments)
        {
            if (targetMethod == null)
                return null;
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (parameters[index].IsOut || parameterType.IsByRef)
                {
                    Type valueType = parameterType.IsByRef
                        ? parameterType.GetElementType()
                        : parameterType;
                    arguments[index] = valueType != null && valueType.IsValueType
                        ? Activator.CreateInstance(valueType)
                        : null;
                }
            }
            Type returnType = targetMethod.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }

    private sealed class FixedFireSuppressionRuntime :
        IEnvironmentalFireSuppressionWorkRuntime
    {
        private readonly string operationId;
        private readonly string resultKey;

        internal FixedFireSuppressionRuntime(
            string fireId,
            string targetId,
            string operationId,
            string resultKey)
        {
            this.operationId = operationId;
            this.resultKey = resultKey;
            Work = new EnvironmentalFireSuppressionWorkSnapshot(
                true,
                new EnvironmentalFireSnapshot
                {
                    FireId = fireId,
                    Target = new EnvironmentalFireTargetRef(
                        EnvironmentalFireTargetKind.Building,
                        targetId),
                    Intensity = 0.4f
                },
                EnvironmentalFireSuppressionMode.InitialAttack,
                1f,
                0,
                "W07 fire suppression",
                string.Empty);
        }

        internal EnvironmentalFireSuppressionWorkSnapshot Work { get; }

        public bool TryGetWork(
            BuildableObject target,
            CharacterActor actor,
            out EnvironmentalFireSuppressionWorkSnapshot work)
        {
            work = Work;
            return true;
        }

        public bool TryBegin(
            BuildableObject target,
            CharacterActor actor,
            EnvironmentalFireSuppressionWorkSnapshot expectedWork,
            out EnvironmentalFireSuppressionAttempt attempt,
            out string failureReason,
            CharacterCommandOrigin commandOrigin = CharacterCommandOrigin.Autonomous)
        {
            ConstructorInfo constructor =
                typeof(EnvironmentalFireSuppressionAttempt).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    new[]
                    {
                        typeof(EnvironmentalFireSuppressionCommand),
                        typeof(float),
                        typeof(IEnvironmentalFireCommand),
                        typeof(IItemQuantityReservationService)
                    },
                    modifiers: null);
            if (constructor == null)
            {
                throw new MissingMethodException(
                    typeof(EnvironmentalFireSuppressionAttempt).FullName,
                    ".ctor");
            }
            attempt = (EnvironmentalFireSuppressionAttempt)constructor.Invoke(
                new object[]
                {
                    new EnvironmentalFireSuppressionCommand(
                        operationId,
                        Work.Fire.FireId,
                        "character:w07:firefighter",
                        Vector2Int.zero,
                        Work.Mode,
                        expectedWork.RequiredWork,
                        workerDisplayName: "W07 firefighter",
                        commandOrigin: commandOrigin),
                    Work.Fire.Intensity,
                    DefaultInterfaceProxy.Create<IEnvironmentalFireCommand>(),
                    DefaultInterfaceProxy.Create<IItemQuantityReservationService>()
                });
            failureReason = string.Empty;
            return true;
        }

        public bool ApplyResponderExposure(
            EnvironmentalFireSuppressionAttempt attempt,
            CharacterActor actor,
            float approvedWork) => true;

        public EnvironmentalFireSuppressionResult Complete(
            EnvironmentalFireSuppressionAttempt attempt)
        {
            return new EnvironmentalFireSuppressionResult(
                EnvironmentalFireSuppressionDisposition.Extinguished,
                0.4f,
                0f,
                0,
                string.Empty,
                "suppressed",
                resultKey);
        }
    }
}
#endif
