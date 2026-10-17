#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;

internal static class LocalLlmRequestQueueLifecycleFixture
{
    internal static IEnumerator Verify(Action<string> record)
    {
        Require(Application.isPlaying && Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification,
            "Queue lifecycle fixture requires isolated PlayMode.");
        const string captureVariable = "DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE";
        string previous = Environment.GetEnvironmentVariable(captureVariable);
        Environment.SetEnvironmentVariable(captureVariable, "1");
        try
        {
            IEnumerator verification = VerifyCapturedQueue(record);
            using (verification as IDisposable)
                while (verification.MoveNext()) yield return null;
        }
        finally { Environment.SetEnvironmentVariable(captureVariable, previous); }
    }

    private static IEnumerator VerifyCapturedQueue(Action<string> record)
    {
        Require(Application.isPlaying && Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification
                && Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE") == "1",
            "Queue lifecycle fixture requires isolated PlayMode.");
        var clock = new QueueClock();
        var instance = new GameObject("G10_RequestQueue_Lifecycle");
        var queue = instance.AddComponent<LocalLlmRequestQueue>();
        queue.Construct(clock);
        int captures = 0;
        int wireBytes = 0;
        Action<LocalLlmCapturedRequest> observe = capture =>
        {
            captures++;
            wireBytes += capture.CopyWireBodyBytes().Length;
        };
        queue.ConfigureIsolatedRequestCapture(observe);
        queue.ConfigureIsolatedAutomaticDispatch(false);
        Set(queue, "maxQueueSize", 2);
        try
        {
            int failed = 0, dropped = 0, quietCallbacks = 0;
            Require(Put(queue, LocalLlmRequestProfiles.SocialRumor, "capacity:1")
                    && Put(queue, LocalLlmRequestProfiles.SocialRumor, "capacity:2"),
                "Protected capacity setup failed.");
            Require(!Put(queue, LocalLlmRequestProfiles.Persona, "capacity:fail", result =>
                { Require(result.Status == LocalLlmRequestStatus.Failed, "Full queue failed status changed."); failed++; })
                && failed == 1, "Full persistent request did not fail exactly once.");
            Require(!Put(queue, LocalLlmRequestProfiles.BubbleLine, "capacity:drop", result =>
                { Require(result.Status == LocalLlmRequestStatus.Dropped, "Full ephemeral status changed."); dropped++; })
                && dropped == 1, "Full ephemeral request did not drop exactly once.");
            // Exercise only synchronous public binary admission. Restore capture mode before
            // yielding; no transport/coroutine is started with this explicit fixture endpoint.
            Set(queue, "isIsolatedRequestCaptureMode", false);
            Set(queue, "endpointUrl", "http://queue-admission.invalid");
            try
            {
                Require(!queue.GenerateEquipmentChoiceAsync("capacity:binary", "후보 0과 1 중 선택", 2,
                            _ => quietCallbacks++)
                        && quietCallbacks == 0 && queue.QueuedCount == 2 && queue.RunningCount == 0,
                    "Binary request bypassed capacity or quiet rejection invoked a callback.");
            }
            finally
            {
                Set(queue, "endpointUrl", string.Empty);
                Set(queue, "isIsolatedRequestCaptureMode", true);
            }
            record("capacity_fail_drop_binary_quiet\tPASS");

            queue.ClearForDebug();
            int binaryCancelled = 0;
            Set(queue, "isIsolatedRequestCaptureMode", false);
            Set(queue, "endpointUrl", "http://queue-admission.invalid");
            try
            {
                Action<LocalLlmChoiceResult> binaryCallback = result =>
                { Require(!result.Succeeded, "Admission-only binary fixture received a success."); binaryCancelled++; };
                Require(queue.GenerateEquipmentChoiceAsync("binary:same", "후보 0과 1 중 선택", 2, binaryCallback)
                        && !queue.GenerateEquipmentChoiceAsync("binary:same", "후보 0과 1 중 선택", 2, binaryCallback)
                        && !Put(queue, LocalLlmRequestProfiles.Persona, "binary:same")
                        && binaryCancelled == 0 && queue.QueuedCount == 1,
                    "Binary admission retained duplicate correlations.");
                queue.CancelEvolutionHistoryRequest("binary:same");
                Require(binaryCancelled == 1 && queue.QueuedCount == 0
                        && queue.GenerateEquipmentChoiceAsync("binary:same", "후보 0과 1 중 선택", 2, binaryCallback),
                    "Binary cancellation could not retry the same key.");
                queue.AbortAllForDebug();
                Require(binaryCancelled == 2 && queue.RunningCount == 0,
                    "Binary admission cleanup did not complete each accepted request exactly once.");
            }
            finally
            {
                Set(queue, "endpointUrl", string.Empty);
                Set(queue, "isIsolatedRequestCaptureMode", true);
            }
            record("binary_admission_duplicate_cancel_same_key_retry\tPASS");

            queue.ClearForDebug();
            bool nestedAccepted = false;
            int evicted = 0;
            Action<LocalLlmResult> onEvicted = result =>
            {
                if (result.Status != LocalLlmRequestStatus.Dropped) return;
                evicted++;
                if (evicted == 1)
                    nestedAccepted = Put(queue, LocalLlmRequestProfiles.SocialRumor, "pressure:nested");
            };
            Require(Put(queue, LocalLlmRequestProfiles.BubbleLine, "pressure:1", onEvicted)
                    && Put(queue, LocalLlmRequestProfiles.BubbleLine, "pressure:2", onEvicted)
                    && Put(queue, LocalLlmRequestProfiles.SocialRumor, "pressure:outer")
                    && nestedAccepted && evicted == 2 && queue.QueuedCount == 2
                    && queue.DroppedEphemeralRequestCount == 2,
                "Reentrant eviction exceeded capacity or lost a terminal notification.");
            record("eviction_callback_reentry\tPASS");

            queue.ClearForDebug();
            var expired = new List<string>();
            var expiring = LocalLlmRequestProfiles.BubbleLine.WithMaxQueueAge(1);
            Require(Put(queue, expiring, "expiry:1", result =>
                { Require(result.Status == LocalLlmRequestStatus.Dropped, "Fixed expiry batch changed terminal status."); expired.Add("1"); }),
                "First expiry setup failed.");
            Require(Put(queue, expiring, "expiry:2", result =>
            {
                Require(result.Status == LocalLlmRequestStatus.Dropped, "Second expiry status changed.");
                expired.Add("2");
                queue.ClearForDebug();
                Require(Put(queue, LocalLlmRequestProfiles.SocialRumor, "expiry:new"), "Expiry reentry was rejected.");
            }), "Second expiry setup failed.");
            clock.Time = 1;
            Invoke(queue, "DropExpiredRequests");
            Require(expired.Count == 0 && queue.QueuedCount == 2, "Exact expiry boundary was dropped.");
            clock.Time = 1.01f;
            Invoke(queue, "DropExpiredRequests");
            Require(expired.SequenceEqual(new[] { "2", "1" }) && queue.QueuedCount == 1,
                "Expiry callbacks changed the fixed batch or removed the newly admitted request.");
            record("expiry_boundary_callback_reset\tPASS");

            queue.ClearForDebug();
            int cancelled = 0;
            bool callbackReentryAccepted = true;
            Require(Put(queue, LocalLlmRequestProfiles.Persona, "reset:same", result =>
            {
                Require(result.IsCancelled, "Reset did not cancel its accepted request.");
                cancelled++;
                callbackReentryAccepted = Put(queue, LocalLlmRequestProfiles.Persona, "reset:reentry");
            }), "Reset setup failed.");
            queue.AbortAllForDebug();
            queue.AbortAllForDebug();
            Require(cancelled == 1 && !callbackReentryAccepted && queue.QueuedCount == 0 && queue.RunningCount == 0,
                "Reset accepted reentry while suspended or completed a request twice.");
            record("reset_cancel_once_reentry_rejected\tPASS");

            int runningCompletions = 0;
            LocalLlmRequestStatus runningStatus = LocalLlmRequestStatus.Succeeded;
            Require(Put(queue, LocalLlmRequestProfiles.Persona, "running:same", result =>
                { runningCompletions++; runningStatus = result.Status; }), "Execution setup failed.");
            Require(!Put(queue, LocalLlmRequestProfiles.SocialRumor, "running:same")
                    && queue.QueuedCount == 1, "Queued correlation was accepted twice.");
            Require(queue.TryDispatchQueuedRequestForIsolatedCapture("running:same")
                    && queue.QueuedCount == 0 && queue.RunningCount == 1,
                "Dispatch did not atomically claim the running slot.");
            while (captures == 0) yield return null;
            Require(wireBytes > 0, "Execution did not serialize the production wire request.");
            object token = RunningToken(queue);
            queue.CancelEvolutionHistoryRequest("running:same");
            queue.CancelEvolutionHistoryRequest("running:same");
            // Controlled late terminal delivery tests the production exactly-once completion
            // gate, without inventing a model response or claiming transport inference.
            Invoke(queue, "Complete", token,
                new LocalLlmResult(LocalLlmRequestStatus.Succeeded, "late", string.Empty, string.Empty), false);
            Require(runningCompletions == 1 && runningStatus == LocalLlmRequestStatus.Cancelled
                    && queue.RunningCount == 1
                    && !Put(queue, LocalLlmRequestProfiles.Persona, "running:same"),
                "Cancellation released a live execution slot/correlation or accepted a late completion.");
            while (queue.RunningCount != 0) yield return null;
            int disabledRunningCancelled = 0;
            Require(Put(queue, LocalLlmRequestProfiles.Persona, "running:same", result =>
                    { Require(result.IsCancelled, "Disable running terminal status changed."); disabledRunningCancelled++; })
                    && queue.TryDispatchQueuedRequestForIsolatedCapture("running:same"),
                "The same correlation could not retry after the execution finally finished.");
            while (captures < 2) yield return null;
            record("actual_coroutine_cancel_late_completion_same_key_retry\tPASS");

            int disabledCancelled = 0;
            Require(Put(queue, LocalLlmRequestProfiles.Persona, "disable:queued", result =>
                { Require(result.IsCancelled, "Disable queued terminal status changed."); disabledCancelled++; }),
                "Disable setup failed.");
            instance.SetActive(false);
            Require(disabledCancelled == 1 && disabledRunningCancelled == 1
                    && queue.QueuedCount == 0 && queue.RunningCount == 0,
                "Actual OnDisable retained pending execution state.");
            instance.SetActive(true);
            queue.ConfigureIsolatedRequestCapture(observe);
            queue.ConfigureIsolatedAutomaticDispatch(false);
            Require(Put(queue, LocalLlmRequestProfiles.Persona, "running:same"),
                "Actual OnEnable retained an earlier lifetime's correlation.");
            queue.AbortAllForDebug();
            record("actual_disable_enable_new_lifetime\tPASS");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static bool Put(LocalLlmRequestQueue queue, LocalLlmRequestProfile profile, string key,
        Action<LocalLlmResult> callback = null) =>
        queue.Enqueue(profile, "Queue lifecycle fixture material.", string.Empty, 30, callback, key);

    private static object RunningToken(LocalLlmRequestQueue queue)
    {
        object owner = typeof(LocalLlmRequestQueue).GetField("requests", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(queue) ?? throw new InvalidOperationException("Queue owner is missing.");
        var running = (IEnumerable)owner.GetType().GetProperty("Running", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(owner);
        return running.Cast<object>().Single();
    }

    private static void Set(LocalLlmRequestQueue queue, string name, object value) =>
        (typeof(LocalLlmRequestQueue).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(name)).SetValue(queue, value);

    private static void Invoke(LocalLlmRequestQueue queue, string name, params object[] arguments) =>
        (typeof(LocalLlmRequestQueue).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(name)).Invoke(queue, arguments);

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class QueueClock : IUiClock
    {
        public float Time { get; set; }
        public float DeltaTime => 0;
    }
}
#endif
