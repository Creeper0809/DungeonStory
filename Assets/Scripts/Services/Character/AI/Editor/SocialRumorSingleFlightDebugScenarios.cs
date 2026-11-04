#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled queue lifecycle fixture. This does not call a model.
public static class SocialRumorSingleFlightDebugScenarios
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
            actorObjectA = CharacterAiPlanDebugFixtures.CreateActorObject("SingleFlightA");
            actorObjectB = CharacterAiPlanDebugFixtures.CreateActorObject("SingleFlightB");
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

            var clock = new TestClock { Now = 10f };
            var queue = new DeferredRuntime();
            contexts = NarrativeRequestContextTestFixture.CreateEmpty();
            runtimeObject = new GameObject("SocialRumorSingleFlightFixture");
            runtimeObject.SetActive(false);
            SocialReputationRuntime runtime = runtimeObject.AddComponent<SocialReputationRuntime>();
            runtime.ConstructSocialReputationRuntime(contexts, queue,
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.WorldRegistry,
                new TestMemoryFactory(), clock, new RandomStreamProvider(9173), null);

            CharacterActivityEvent activity = CharacterActivityEvent.Create(
                CharacterActivityKinds.Social, CharacterActivityOutcomes.Failed,
                "A conversation ended badly", actionId: "social:single-flight",
                sentiment: -0.7f, narrativeEligible: true);
            var entry = new CharacterLogEntry(1, activity.KindId,
                activity.FactText, 1, activity.FactText, activity);

            Require(runtime.RequestSocialInterpretation(actorA, entry) && queue.CallCount == 1,
                "First request for actor A must be accepted.");
            clock.Now = 15f;
            Require(!runtime.RequestSocialInterpretation(actorA, entry) && queue.CallCount == 1,
                "Actor A must remain single-flight beyond the cooldown.");
            Require(runtime.RequestSocialInterpretation(actorB, entry) && queue.CallCount == 2,
                "Actor B must be independent while actor A is pending.");
            queue.Complete(0, LocalLlmRequestStatus.Cancelled);
            clock.Now = 20f;
            Require(runtime.RequestSocialInterpretation(actorA, entry) && queue.CallCount == 3,
                "Cancellation must release actor A after cooldown.");
            queue.Complete(2, LocalLlmRequestStatus.Cancelled);
            queue.Complete(1, LocalLlmRequestStatus.Cancelled);

            clock.Now = 25f;
            queue.RejectNext = true;
            Require(!runtime.RequestSocialInterpretation(actorA, entry) && queue.CallCount == 4,
                "Synchronous queue rejection must be observable.");
            clock.Now = 30f;
            Require(runtime.RequestSocialInterpretation(actorA, entry) && queue.CallCount == 5,
                "Queue rejection must release the reservation.");
            queue.Complete(4, LocalLlmRequestStatus.Cancelled);

            queue.ThrowNext = true;
            bool threw = false;
            try { runtime.RequestSocialInterpretation(actorB, entry); }
            catch (InvalidOperationException error) when (error.Message == DeferredRuntime.InjectedFailure)
            { threw = true; }
            Require(threw && queue.CallCount == 6, "Enqueue exception must reach the caller.");
            clock.Now = 35f;
            Require(runtime.RequestSocialInterpretation(actorB, entry) && queue.CallCount == 7,
                "Enqueue exception must release the reservation.");
            queue.Complete(6, LocalLlmRequestStatus.Failed);
            clock.Now = 40f;
            Require(runtime.RequestSocialInterpretation(actorB, entry) && queue.CallCount == 8,
                "Failure callback must release the reservation.");
            queue.Complete(7, LocalLlmRequestStatus.Succeeded);
            clock.Now = 45f;
            Require(runtime.RequestSocialInterpretation(actorB, entry) && queue.CallCount == 9,
                "Success callback must release the reservation.");
            queue.Complete(8, LocalLlmRequestStatus.Cancelled);
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

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TestClock : IGameClock
    {
        public float Now;
        public float Time => Now;
        public float DeltaTime => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class TestMemoryFactory : ICharacterSocialMemoryFactory
    {
        public CharacterSocialMemory GetOrAdd(CharacterActor actor) =>
            throw new InvalidOperationException("This fixture has no facility rumor.");
    }

    private sealed class DeferredRuntime : ILocalLlmRuntimeProvider, ILocalLlmRuntime
    {
        public const string InjectedFailure = "injected enqueue failure";
        private readonly List<Action<LocalLlmResult>> callbacks = new();
        public int CallCount { get; private set; }
        public bool RejectNext;
        public bool ThrowNext;

        public bool TryGetRuntime(out ILocalLlmRuntime runtime) { runtime = this; return true; }
        public ILocalLlmRuntime GetRequiredRuntime() => this;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback)
        {
            CallCount++;
            callbacks.Add(callback);
            if (ThrowNext) { ThrowNext = false; throw new InvalidOperationException(InjectedFailure); }
            if (RejectNext) { RejectNext = false; return false; }
            return true;
        }
        public void Complete(int index, LocalLlmRequestStatus status)
        {
            const string none = "{\"rumorType\":\"None\",\"targetType\":\"None\","
                + "\"targetFacilityId\":-1,\"targetFacilityTag\":\"\","
                + "\"targetCharacterId\":\"\",\"targetCharacterName\":\"\","
                + "\"sentiment\":0,\"summary\":\"\",\"spreadChance\":0,"
                + "\"trustImpact\":0,\"validSeconds\":0}";
            callbacks[index](new LocalLlmResult(status,
                status == LocalLlmRequestStatus.Succeeded ? none : string.Empty,
                status == LocalLlmRequestStatus.Failed ? "controlled failure" : string.Empty,
                string.Empty));
        }
        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => throw new NotSupportedException();
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => throw new NotSupportedException();
    }
}
#endif
