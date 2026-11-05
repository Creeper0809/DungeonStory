#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled enqueue and callback contract fixture. No model is invoked.
public static class AiDirectorSingleFlightDebugScenarios
{
    public static void VerifyForBatch()
    {
        GameObject runtimeObject = null;
        GameObject actorObjectA = null;
        GameObject actorObjectB = null;
        CharacterSO dataA = null;
        CharacterSO dataB = null;
        NarrativeRequestContextBuilder contexts = null;
        try
        {
            actorObjectA = CharacterAiPlanDebugFixtures.CreateActorObject("DirectorSingleFlightA");
            actorObjectB = CharacterAiPlanDebugFixtures.CreateActorObject("DirectorSingleFlightB");
            dataA = CharacterAiPlanDebugFixtures.CreateCharacterData(CharacterType.Customer, "A", "Slime");
            dataB = CharacterAiPlanDebugFixtures.CreateCharacterData(CharacterType.Customer, "B", "Slime");
            CharacterActor actorA = actorObjectA.GetComponent<CharacterActor>();
            CharacterActor actorB = actorObjectB.GetComponent<CharacterActor>();
            actorA.EnsureRuntimeState();
            actorB.EnsureRuntimeState();
            actorA.Initialize(dataA);
            actorB.Initialize(dataB);
            actorA.SetLifecycleState(CharacterLifecycleState.Active);
            actorB.SetLifecycleState(CharacterLifecycleState.Active);

            var queue = new DeferredRuntime();
            contexts = NarrativeRequestContextTestFixture.CreateEmpty();
            runtimeObject = new GameObject("AiDirectorSingleFlightFixture");
            runtimeObject.SetActive(false);
            AiDirectorRuntime director = runtimeObject.AddComponent<AiDirectorRuntime>();
            director.ConstructAiDirectorRuntime(contexts, queue,
                new AiDirectorContextSceneQuery(CharacterAiEditorTestDependencies.WorldRegistry,
                    CharacterAiEditorTestDependencies.WorldRegistry),
                new TestScheduling(),
                new CharacterAiFacilityLookup(CharacterAiEditorTestDependencies.WorldRegistry),
                CharacterAiEditorTestDependencies.GameClock, null,
                CharacterAiEditorTestDependencies.AuthoredGameplay);
            director.SetWarningLogsSuppressedForDebug(true);

            Require(director.RequestMoodImpulse(actorA) && queue.MoodCalls == 1,
                "Initial mood request must enqueue.");
            Require(!director.RequestMoodImpulse(actorA) && queue.MoodCalls == 1,
                "A pending mood request must block its duplicate.");
            Require(director.RequestMoodImpulse(actorB) && queue.MoodCalls == 2,
                "A second actor must have an independent mood request.");
            Require(director.RequestMacroGoal(actorA) && queue.MacroCalls == 1,
                "Macro requests must be independent of pending mood requests.");
            Require(!director.RequestMacroGoal(actorA) && queue.MacroCalls == 1,
                "A pending macro request must block its duplicate.");
            Require(director.RequestMacroGoal(actorB) && queue.MacroCalls == 2,
                "A second actor must have an independent macro request.");
            queue.CompleteMood(LocalLlmRequestStatus.Cancelled);
            queue.CompleteMood(LocalLlmRequestStatus.Cancelled);
            queue.CompleteMacro(LocalLlmRequestStatus.Cancelled);
            queue.CompleteMacro(LocalLlmRequestStatus.Cancelled);

            VerifyRelease(() => director.RequestMoodImpulse(actorA),
                () => queue.MoodCalls, queue.CompleteMood, () => director.LastError, queue);
            VerifyRelease(() => director.RequestMacroGoal(actorA),
                () => queue.MacroCalls, queue.CompleteMacro, () => director.LastError, queue);
        }
        finally
        {
            contexts?.Dispose();
            if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);
            if (actorObjectA != null) Object.DestroyImmediate(actorObjectA);
            if (actorObjectB != null) Object.DestroyImmediate(actorObjectB);
            if (dataA != null) Object.DestroyImmediate(dataA);
            if (dataB != null) Object.DestroyImmediate(dataB);
        }
    }

    private static void VerifyRelease(Func<bool> request, Func<int> count,
        Action<LocalLlmRequestStatus> complete, Func<string> lastError, DeferredRuntime queue)
    {
        foreach (LocalLlmRequestStatus status in new[]
                 { LocalLlmRequestStatus.Succeeded, LocalLlmRequestStatus.Failed, LocalLlmRequestStatus.Cancelled })
        {
            int before = count();
            Require(request() && count() == before + 1, "Request after callback must enqueue.");
            complete(status);
            if (status == LocalLlmRequestStatus.Succeeded)
                Require(string.IsNullOrEmpty(lastError()), "Success payload was not accepted by the consumer.");
        }
        queue.RejectNext = true;
        int rejected = count();
        Require(!request() && count() == rejected + 1, "Synchronous rejection must be observed.");
        Require(request() && count() == rejected + 2, "Synchronous rejection must release the actor.");
        complete(LocalLlmRequestStatus.Cancelled);

        queue.ThrowNext = true;
        int thrown = count();
        bool caught = false;
        try { request(); }
        catch (InvalidOperationException error) when (error.Message == DeferredRuntime.InjectedFailure)
        { caught = true; }
        Require(caught && count() == thrown + 1, "Enqueue exception must reach the caller.");
        Require(request() && count() == thrown + 2, "Enqueue exception must release the actor.");
        complete(LocalLlmRequestStatus.Cancelled);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class DeferredRuntime : ILocalLlmRuntimeProvider, ILocalLlmRuntime
    {
        public const string InjectedFailure = "controlled enqueue exception";
        private readonly Queue<Action<LocalLlmResult>> moodCallbacks = new();
        private readonly Queue<Action<LocalLlmResult>> macroCallbacks = new();
        public int MoodCalls { get; private set; }
        public int MacroCalls { get; private set; }
        public bool RejectNext;
        public bool ThrowNext;
        public bool TryGetRuntime(out ILocalLlmRuntime runtime) { runtime = this; return true; }
        public ILocalLlmRuntime GetRequiredRuntime() => this;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback)
        { MoodCalls++; return Enqueue(moodCallbacks, callback); }
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback)
        { MacroCalls++; return Enqueue(macroCallbacks, callback); }
        private bool Enqueue(Queue<Action<LocalLlmResult>> callbacks, Action<LocalLlmResult> callback)
        {
            if (ThrowNext) { ThrowNext = false; throw new InvalidOperationException(InjectedFailure); }
            if (RejectNext) { RejectNext = false; return false; }
            callbacks.Enqueue(callback);
            return true;
        }
        public void CompleteMood(LocalLlmRequestStatus status) => Complete(moodCallbacks, status,
            "{\"moodImpulse\":\"FollowRoutine\",\"strength\":0.5,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"routine\",\"validSeconds\":30}");
        public void CompleteMacro(LocalLlmRequestStatus status) => Complete(macroCallbacks, status,
            "{\"macroGoal\":\"Continue\",\"reason\":\"routine\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"validSeconds\":30}");
        private static void Complete(Queue<Action<LocalLlmResult>> callbacks, LocalLlmRequestStatus status, string success)
        {
            callbacks.Dequeue()(new LocalLlmResult(status,
                status == LocalLlmRequestStatus.Succeeded ? success : string.Empty,
                status == LocalLlmRequestStatus.Failed ? "controlled failure" : string.Empty, string.Empty));
        }
        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => throw new NotSupportedException();
    }

    private sealed class TestScheduling : ICharacterAiSchedulingService
    {
        public bool IsDrivingAi => false;
        public bool IsSchedulerAvailable => false;
        public void Register(CharacterActor actor) { }
        public void Unregister(CharacterActor actor) { }
        public void RequestImmediateDecision(CharacterActor actor) { }
        public bool TryConsumePathSearchBudget() => false;
        public bool ShouldShowCharacterFeedback(CharacterActor actor) => false;
        public bool ShouldCollectDetailedDiagnostics(CharacterActor actor) => false;
        public int GetMovementFrameStride(CharacterActor actor) => 1;
        public double GetDecisionWorkSliceMilliseconds(CharacterActor actor) => 0d;
        public void ResetPathSearchBudgetForDebug() { }
    }
}
#endif
