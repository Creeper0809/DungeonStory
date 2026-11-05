#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;

// Pure queue-state fixture. No transport or model is invoked.
public static class ResponsiveLlmLaneDebugScenarios
{
    public static void VerifyForBatch()
    {
        VerifyRejectionContract();
        VerifyBubbleGenerationLength();
        LocalLlmRequestProfile bubble = LocalLlmRequestProfiles.BubbleLine.WithMaxQueueAge(3f);
        LocalLlmRequestProfile background = LocalLlmRequestProfiles.CharacterSkillModuleSelection;
        LocalLlmRequestProfile persistent = LocalLlmRequestProfiles.Persona;
        LocalLlmRequestProfile history = LocalLlmRequestProfiles.EvolutionHistoryLegacyV2;
        Require(bubble.Interactive && persistent.Interactive && !background.Interactive,
            "Production request profiles have unexpected lane assignments.");
        Require(history.Id == "EvolutionHistoryLegacyV2" && history.Interactive
                && history.Priority == 17 && history.Temperature == 0.65f
                && history.QueueFullBehavior == LocalLlmQueueFullBehavior.RejectQuietly
                && !history.CanBeEvictedForQueuePressure && history.MaxQueueAgeSeconds == 0f
                && !history.LogFailureWarnings && history.MaxOutputTokens == 384,
            "Evolution history profile changed a retained field or ordinary lane assignment.");
        LocalLlmRequestProfile agedBubble = bubble.WithMaxQueueAge(7f);
        LocalLlmRequestProfile agedBackground = background.WithMaxQueueAge(7f);
        Require(agedBubble.Interactive && agedBubble.MaxQueueAgeSeconds == 7f
                && agedBubble.Id == bubble.Id
                && !agedBackground.Interactive && agedBackground.MaxQueueAgeSeconds == 7f
                && agedBackground.Id == background.Id,
            "Changing maximum queue age lost the interactive lane flag.");

        var lanes = new StateFixture();
        object background1 = lanes.Put(background, "background:1");
        object background2 = lanes.Put(background, "background:2");
        Require(ReferenceEquals(lanes.Begin(true), background1), "First background did not start.");
        Require(lanes.Begin(true) == null, "A second background occupied the interactive lane.");
        object front1 = lanes.Put(persistent, "interactive:1");
        Require(ReferenceEquals(lanes.Begin(true), front1), "Interactive work did not run alongside background work.");
        object front2 = lanes.Put(persistent, "interactive:2");
        Require(lanes.Begin(true) == null, "A busy lane accepted another request.");
        Require(lanes.Cancel("background:1") == 1 && lanes.IsRunning(background1)
                && lanes.Contains("background:1") && lanes.Begin(true) == null,
            "Background cancellation released the lane or correlation before Finish.");
        Require(lanes.Cancel("interactive:1") == 1 && lanes.IsRunning(front1)
                && lanes.Contains("interactive:1") && lanes.Begin(true) == null,
            "Running cancellation released the lane or correlation before Finish.");
        lanes.Finish(front1);
        Require(!lanes.Contains("interactive:1") && ReferenceEquals(lanes.Begin(true), front2),
            "Finish did not release the cancelled interactive lane.");
        lanes.Finish(front2);
        lanes.Finish(background1);
        Require(ReferenceEquals(lanes.Begin(true), background2), "Waiting background did not start after Finish.");
        lanes.Finish(background2);

        var fairness = new StateFixture();
        object waiting = fairness.Put(persistent, "persistent");
        object bubble1 = fairness.Put(bubble, "bubble:1");
        object bubble2 = fairness.Put(bubble, "bubble:2");
        object bubble3 = fairness.Put(bubble, "bubble:3");
        Require(ReferenceEquals(fairness.Begin(true), bubble1), "The first ephemeral request did not take priority.");
        fairness.Finish(bubble1);
        Require(ReferenceEquals(fairness.Begin(true), bubble2), "The second ephemeral request did not take priority.");
        fairness.Finish(bubble2);
        Require(ReferenceEquals(fairness.Begin(true), waiting), "Persistent interactive work was not offered after two bubbles.");
        fairness.Finish(waiting);
        Require(ReferenceEquals(fairness.Begin(true), bubble3), "Pending bubble did not resume after the persistent request.");
        fairness.Finish(bubble3);

        var dedicated = new StateFixture();
        object dedicatedBackground = dedicated.Put(background, "dedicated:background:1");
        Require(ReferenceEquals(dedicated.Begin(true, 3), dedicatedBackground),
            "Dedicated background did not start.");
        object dedicatedPersistent = dedicated.Put(persistent, "dedicated:persistent:1");
        Require(ReferenceEquals(dedicated.Begin(true, 3), dedicatedPersistent),
            "Dedicated persistent interactive request did not start alongside background.");
        object dedicatedBubble = dedicated.Put(bubble, "dedicated:bubble:1");
        Require(ReferenceEquals(dedicated.Begin(true, 3), dedicatedBubble),
            "Dedicated bubble did not start alongside the other lanes.");
        object waitingBackground = dedicated.Put(background, "dedicated:background:2");
        object waitingPersistent = dedicated.Put(persistent, "dedicated:persistent:2");
        object waitingBubble = dedicated.Put(bubble, "dedicated:bubble:2");
        Require(dedicated.Begin(true, 3) == null, "A busy dedicated lane accepted another request.");
        Require(dedicated.Cancel("dedicated:background:1") == 1
                && dedicated.Cancel("dedicated:persistent:1") == 1
                && dedicated.Cancel("dedicated:bubble:1") == 1
                && dedicated.IsRunning(dedicatedBackground)
                && dedicated.IsRunning(dedicatedPersistent)
                && dedicated.IsRunning(dedicatedBubble)
                && dedicated.Begin(true, 3) == null,
            "Cancellation released a dedicated lane before Finish.");
        dedicated.Finish(dedicatedBackground);
        Require(ReferenceEquals(dedicated.Begin(true, 3), waitingBackground)
                && dedicated.Begin(true, 3) == null,
            "Background Finish did not release only its dedicated lane.");
        dedicated.Finish(dedicatedPersistent);
        Require(ReferenceEquals(dedicated.Begin(true, 3), waitingPersistent)
                && dedicated.Begin(true, 3) == null,
            "Persistent Finish did not release only its dedicated lane.");
        dedicated.Finish(dedicatedBubble);
        Require(ReferenceEquals(dedicated.Begin(true, 3), waitingBubble),
            "Bubble Finish did not release its dedicated lane.");
        dedicated.Finish(waitingBackground);
        dedicated.Finish(waitingPersistent);
        dedicated.Finish(waitingBubble);

        var four = new StateFixture();
        object fourBackground = four.Put(background, "four:background:1");
        Require(ReferenceEquals(four.Begin(true, 4), fourBackground),
            "Four-slot background did not start.");
        object fourBackground2 = four.Put(background, "four:background:2");
        Require(ReferenceEquals(four.Begin(true, 4), fourBackground2),
            "Second background did not start in its own lane capacity.");
        object fourBackgroundWaiting = four.Put(background, "four:background:3");
        Require(four.Begin(true, 4) == null,
            "Third background occupied an idle interactive lane.");
        object fourPersistent1 = four.Put(persistent, "four:persistent:1");
        Require(ReferenceEquals(four.Begin(true, 4), fourPersistent1),
            "Persistent interactive request did not start alongside background.");
        object fourPersistentWaiting = four.Put(persistent, "four:persistent:2");
        Require(four.Begin(true, 4) == null,
            "Second persistent interactive request occupied the bubble lane.");
        object fourBubble = four.Put(bubble, "four:bubble:1");
        Require(ReferenceEquals(four.Begin(true, 4), fourBubble),
            "Bubble did not start alongside the three other requests.");
        object fourBubbleWaiting = four.Put(bubble, "four:bubble:2");
        Require(four.Begin(true, 4) == null, "A full four-slot schedule started another request.");
        Require(four.Cancel("four:background:1") == 1
                && four.Cancel("four:background:2") == 1
                && four.Cancel("four:persistent:1") == 1
                && four.Cancel("four:bubble:1") == 1
                && four.IsRunning(fourBackground) && four.IsRunning(fourBackground2)
                && four.IsRunning(fourPersistent1) && four.IsRunning(fourBubble)
                && four.Begin(true, 4) == null,
            "Four-slot cancellation released a running position before Finish.");
        four.Finish(fourBackground);
        Require(ReferenceEquals(four.Begin(true, 4), fourBackgroundWaiting),
            "Background Finish did not release its four-slot lane.");
        object fourthBackground = four.Put(background, "four:background:4");
        four.Finish(fourBackgroundWaiting);
        Require(ReferenceEquals(four.Begin(true, 4), fourthBackground),
            "The remaining background position was not reusable.");
        object fifthBackground = four.Put(background, "four:background:5");
        four.Finish(fourthBackground);
        Require(four.IsRunning(fourBackground2),
            "Cancelled background did not retain its position before Finish.");
        Require(ReferenceEquals(four.Begin(true, 4), fifthBackground),
            "One free background position was not reusable.");
        object sixthBackground = four.Put(background, "four:background:6");
        four.Finish(fourPersistent1);
        Require(ReferenceEquals(four.Begin(true, 4), fourPersistentWaiting),
            "Persistent Finish did not release one interactive position.");
        four.Finish(fourPersistentWaiting);
        Require(four.Begin(true, 4) == null && four.IsRunning(fourBackground2),
            "A cancelled background released its position before Finish.");
        four.Finish(fourBackground2);
        Require(ReferenceEquals(four.Begin(true, 4), sixthBackground),
            "Finishing cancelled background did not release its position.");
        four.Finish(fourBubble);
        Require(ReferenceEquals(four.Begin(true, 4), fourBubbleWaiting),
            "Bubble Finish did not release its four-slot lane.");
        four.Finish(sixthBackground);
        four.Finish(fifthBackground);
        four.Finish(fourBubbleWaiting);

        VerifyEvolutionHistoryLane(background, persistent, bubble, history);
        VerifyDurableProfileConcurrency(background, persistent, bubble);

        var legacy = new StateFixture();
        object old1 = legacy.Put(background, "legacy:1");
        object old2 = legacy.Put(background, "legacy:2");
        Require(ReferenceEquals(legacy.Begin(false), old1)
                && ReferenceEquals(legacy.Begin(false), old2),
            "Legacy scheduling unexpectedly restricted two background slots.");
        Require(legacy.Begin(false) == null, "Legacy scheduling exceeded its maximum running count.");
        legacy.Finish(old1);
        legacy.Finish(old2);

        VerifyRetiredOwners(background, persistent);
        VerifyRealtimeCapacity(background, persistent, bubble);
        VerifyOrdinaryCapacity(background, persistent, bubble);
        VerifyOneWaitingRecord(background, persistent, bubble);
        VerifyRealtimeClock();
    }

    private static void VerifyEvolutionHistoryLane(LocalLlmRequestProfile background,
        LocalLlmRequestProfile persistent, LocalLlmRequestProfile bubble,
        LocalLlmRequestProfile history)
    {
        var state = new StateFixture();
        object firstBackground = state.Put(background, "history:background:1");
        Require(ReferenceEquals(state.Begin(true, 4), firstBackground), "First background did not start.");
        object secondBackground = state.Put(background, "history:background:2");
        Require(ReferenceEquals(state.Begin(true, 4), secondBackground), "Second background did not start.");
        object historyRequest = state.Put(history, "history:ordinary");
        Require(ReferenceEquals(state.Begin(true, 4), historyRequest),
            "History did not use the ordinary interactive lane alongside two background requests.");
        object waitingOrdinary = state.Put(persistent, "history:waiting-ordinary");
        Require(state.Begin(true, 4) == null,
            "An ordinary request occupied the idle bubble lane while history was running.");
        object speech = state.Put(bubble, "history:bubble");
        Require(ReferenceEquals(state.Begin(true, 4), speech),
            "History occupied the bubble lane.");
        Require(state.Cancel("history:ordinary") == 1 && state.IsRunning(historyRequest),
            "History cancellation did not retain its running ownership.");
        state.Finish(firstBackground);
        object waitingBackground = state.Put(background, "history:background:3");
        Require(ReferenceEquals(state.Begin(true, 4), waitingBackground),
            "History occupied a released background position.");
        state.Finish(waitingBackground);
        Require(state.Begin(true, 4) == null,
            "Cancelled history released the ordinary lane before Finish.");
        state.Finish(historyRequest);
        Require(ReferenceEquals(state.Begin(true, 4), waitingOrdinary),
            "History Finish did not release the ordinary lane.");
        state.Finish(waitingOrdinary);
        state.Finish(secondBackground);
        state.Finish(speech);
    }

    private static void VerifyDurableProfileConcurrency(LocalLlmRequestProfile skill,
        LocalLlmRequestProfile ordinary, LocalLlmRequestProfile bubble)
    {
        LocalLlmRequestProfile trait = LocalLlmRequestProfiles.AcquiredTraitModuleSelection;
        Require(trait.DedicatedConcurrencyLimit == 1
                && trait.WithMaxQueueAge(7f).DedicatedConcurrencyLimit == 1
                && skill.DedicatedConcurrencyLimit == 0,
            "Profile copy changed the dedicated concurrency contract.");
        bool invalidRejected = false;
        try { _ = new LocalLlmRequestProfile("invalid", 1, dedicatedConcurrencyLimit: -1); }
        catch (ArgumentOutOfRangeException) { invalidRejected = true; }
        Require(invalidRejected, "A negative dedicated concurrency limit was accepted.");

        var state = new StateFixture();
        object first = state.Put(trait, "limited:trait:1");
        Require(ReferenceEquals(state.Begin(true, 4), first), "First automatic trait did not start.");
        object next = state.Put(trait, "limited:trait:2");
        Require(state.Begin(true, 4) == null && state.Contains("limited:trait:2"),
            "A second automatic trait occupied both background positions or was dropped.");
        object skill1 = state.Put(skill, "limited:skill:1");
        Require(ReferenceEquals(state.Begin(true, 4), skill1),
            "Automatic traits blocked the available skill position.");
        object shortRequest = state.Put(ordinary, "limited:ordinary");
        object speech = state.Put(bubble, "limited:speech");
        object firstShort = state.Begin(true, 4);
        object secondShort = state.Begin(true, 4);
        Require((ReferenceEquals(firstShort, speech) && ReferenceEquals(secondShort, shortRequest)
                    || ReferenceEquals(firstShort, shortRequest) && ReferenceEquals(secondShort, speech))
                && state.IsRunning(speech) && state.IsRunning(shortRequest)
                && state.Begin(true, 4) == null,
            "Profile-limited background work changed speech or ordinary capacity.");
        Require(state.Cancel("limited:trait:1") == 1 && state.IsRunning(first),
            "Trait cancellation lost the running-slot owner before Finish.");
        state.Finish(skill1);
        Require(state.Begin(true, 4) == null && state.Contains("limited:trait:2"),
            "Cancelled trait released its profile allowance before Finish.");
        object skill2 = state.Put(skill, "limited:skill:2");
        Require(ReferenceEquals(state.Begin(true, 4), skill2),
            "A cancelled trait prevented a later skill from using the other position.");
        state.Finish(first);
        Require(ReferenceEquals(state.Begin(true, 4), next),
            "Waiting automatic trait did not resume after its previous owner finished.");
        state.Finish(next);
        state.Finish(skill2);
        state.Finish(shortRequest);
        state.Finish(speech);

        var legacy = new StateFixture();
        object legacy1 = legacy.Put(trait, "limited:legacy:1");
        object legacy2 = legacy.Put(trait, "limited:legacy:2");
        Require(ReferenceEquals(legacy.Begin(false), legacy1)
                && ReferenceEquals(legacy.Begin(false), legacy2),
            "Dedicated profile policy leaked into the legacy scheduler.");
        legacy.Finish(legacy1);
        legacy.Finish(legacy2);
    }

    private static void VerifyRetiredOwners(LocalLlmRequestProfile background,
        LocalLlmRequestProfile persistent)
    {
        var state = new StateFixture();
        bool runningAlive = true, queuedAlive = true, rollbackAlive = true;
        int runningCallbacks = 0, queuedCallbacks = 0;
        object running = state.Put(background, "retired:running", () => runningAlive,
            _ => runningCallbacks++);
        Require(ReferenceEquals(state.Begin(true, 3), running), "Retirement setup did not start running work.");
        object live = state.Put(persistent, "retired:live", () => true);
        object rollback = state.Put(background, "retired:rollback", () => rollbackAlive);
        object queued = state.Put(background, "retired:queued", () => queuedAlive,
            _ => queuedCallbacks++);
        Require(state.TakeRetired().Count == 0 && state.Contains("retired:rollback"),
            "An unchanged live graph was retired during rollback.");
        runningAlive = false;
        queuedAlive = false;
        IList retired = state.TakeRetired();
        Require(retired.Count == 2 && retired.Contains(running) && retired.Contains(queued)
                && state.IsRunning(running) && state.Contains("retired:running")
                && !state.Contains("retired:queued") && state.Contains("retired:live")
                && state.Contains("retired:rollback"),
            "Retirement did not preserve live owners or hold the running slot.");
        Require(ReferenceEquals(state.Begin(true, 3), live),
            "Retired background blocked a separate live lane.");
        Require(state.Begin(true, 3) == null,
            "Retired running work released its background slot before Finish.");
        Require(state.CompleteCancelled(running) && state.CompleteCancelled(queued)
                && runningCallbacks == 1 && queuedCallbacks == 1,
            "Retired requests did not deliver exactly one cancellation callback.");
        Require(state.TakeRetired().Count == 0 && !state.CompleteCancelled(running)
                && !state.CompleteCancelled(queued) && runningCallbacks == 1 && queuedCallbacks == 1,
            "Completed retired work was returned or delivered again.");
        Require(state.IsRunning(running), "Completed cancellation released the running slot before Finish.");
        state.Finish(running);
        Require(ReferenceEquals(state.Begin(true, 3), rollback),
            "The live rollback graph could not proceed after the retired slot finished.");
        state.Finish(rollback);
        state.Finish(live);
    }

    private static void VerifyRealtimeCapacity(LocalLlmRequestProfile background,
        LocalLlmRequestProfile persistent, LocalLlmRequestProfile bubble)
    {
        var state = new StateFixture();
        Require(state.CanAdmitRealtime(), "An empty queue rejected realtime capacity.");
        state.Put(background, "capacity:background");
        state.Put(persistent, "capacity:persistent");
        Require(state.CanAdmitRealtime(), "Non-bubble requests affected realtime capacity.");

        var bubbleState = new StateFixture();
        object first = bubbleState.Put(bubble, "capacity:bubble:1");
        Require(!bubbleState.CanAdmitRealtime(), "One queued bubble did not fill realtime capacity.");
        Require(ReferenceEquals(bubbleState.Begin(true, 3), first), "Capacity fixture bubble did not start.");
        Require(!bubbleState.CanAdmitRealtime(), "One running bubble did not fill realtime capacity.");
        Require(bubbleState.CompleteCancelled(first) && bubbleState.CanAdmitRealtime(),
            "A completed bubble continued to block realtime admission.");
        bubbleState.Finish(first);
        Require(bubbleState.CanAdmitRealtime(), "Finishing the bubble did not preserve realtime capacity.");
    }

    private static void VerifyOneWaitingRecord(LocalLlmRequestProfile background,
        LocalLlmRequestProfile ordinary, LocalLlmRequestProfile bubble)
    {
        LocalLlmRequestProfile record = LocalLlmRequestProfiles.CharacterRecord;
        Require(record.WaitForOrdinaryLane && record.WithMaxQueueAge(0f).WaitForOrdinaryLane
                && !ordinary.WaitForOrdinaryLane && !bubble.WaitForOrdinaryLane
                && record.Priority == 12 && record.Interactive
                && record.QueueFullBehavior == LocalLlmQueueFullBehavior.RejectQuietly,
            "Only the declared record profile may wait for the ordinary lane.");
        var state = new StateFixture();
        Require(state.CanAdmitOrdinaryProfile(record) && state.CanAdmitOrdinaryProfile(ordinary),
            "An empty ordinary lane rejected admission.");
        object busy = state.Put(ordinary, "record:busy");
        Require(ReferenceEquals(state.Begin(true, 3), busy)
                && state.CanAdmitOrdinaryProfile(record) && !state.CanAdmitOrdinaryProfile(ordinary),
            "A record could not wait behind one busy ordinary response.");
        object waiting = state.Put(record, "record:one-waiting");
        Require(!state.CanAdmitOrdinaryProfile(record) && !state.CanAdmitOrdinaryProfile(ordinary),
            "The one-record waiting boundary accepted more ordinary work.");
        object bulk = state.Put(background, "record:bulk");
        object speech = state.Put(bubble, "record:speech");
        object first = state.Begin(true, 3);
        object second = state.Begin(true, 3);
        Require((ReferenceEquals(first, bulk) && ReferenceEquals(second, speech))
                || (ReferenceEquals(first, speech) && ReferenceEquals(second, bulk)),
            "A waiting record consumed bulk or speech capacity.");
        state.Finish(busy);
        Require(ReferenceEquals(state.Begin(true, 3), waiting)
                && !state.CanAdmitOrdinaryProfile(record),
            "The waiting record did not acquire the released ordinary lane exactly once.");
        state.Finish(waiting);
        Require(state.CanAdmitOrdinaryProfile(record) && state.CanAdmitOrdinaryProfile(ordinary),
            "Completing a record did not free ordinary admission.");
        state.Finish(bulk);
        state.Finish(speech);

        var cancelled = new StateFixture();
        object running = cancelled.Put(ordinary, "record:cancel:busy");
        Require(ReferenceEquals(cancelled.Begin(true, 3), running), "Ordinary cancellation setup failed.");
        object queued = cancelled.Put(record, "record:cancel:waiting");
        Require(cancelled.Cancel("record:cancel:waiting") == 1
                && cancelled.CompleteCancelled(queued) && cancelled.CanAdmitOrdinaryProfile(record)
                && cancelled.IsRunning(running) && cancelled.Begin(true, 3) == null,
            "Cancelling a waiting record released the running ordinary slot or retained admission.");
        bool alive = true;
        object retired = cancelled.Put(record, "record:retired", () => alive);
        alive = false;
        Require(cancelled.TakeRetired().Contains(retired)
                && cancelled.CompleteCancelled(retired) && cancelled.CanAdmitOrdinaryProfile(record),
            "A retired waiting record was not removed from admission capacity.");
        cancelled.Finish(running);
    }

    private static void VerifyOrdinaryCapacity(LocalLlmRequestProfile background,
        LocalLlmRequestProfile persistent, LocalLlmRequestProfile bubble)
    {
        var state = new StateFixture();
        object first = state.Put(persistent, "ordinary:1");
        state.Put(persistent, "ordinary:2");
        state.Put(persistent, "ordinary:3");
        Require(state.CanAdmitOrdinary(4), "Three ordinary requests exhausted a four-request cap.");
        state.Put(persistent, "ordinary:4");
        Require(!state.CanAdmitOrdinary(4), "Four queued ordinary requests did not fill the cap.");
        Require(ReferenceEquals(state.Begin(true, 4), first) && !state.CanAdmitOrdinary(4),
            "Moving ordinary work from queued to running changed the cap count.");
        state.Finish(first);
        Require(state.CanAdmitOrdinary(4), "Finishing ordinary work did not free capacity.");
        state.Put(background, "ordinary:background");
        state.Put(bubble, "ordinary:bubble");
        Require(state.CanAdmitOrdinary(4), "Background or bubble work affected the ordinary cap.");

        var one = new StateFixture();
        Require(one.CanAdmitOrdinary(1), "Empty queue rejected a one-request ordinary cap.");
        object only = one.Put(persistent, "ordinary:single");
        Require(!one.CanAdmitOrdinary(1), "One queued ordinary request did not fill the cap.");
        Require(ReferenceEquals(one.Begin(true, 4), only) && !one.CanAdmitOrdinary(1),
            "One running ordinary request did not retain the cap.");
        one.Finish(only);
        one.Put(background, "ordinary:single-background");
        one.Put(bubble, "ordinary:single-bubble");
        Require(one.CanAdmitOrdinary(1), "Finishing ordinary work did not free its cap.");
    }

    private static void VerifyRealtimeClock()
    {
        var clock = new UnityUiClock();
        int frame = UnityEngine.Time.frameCount;
        float before = clock.Time;
        System.Threading.Thread.Sleep(40);
        float elapsed = clock.Time - before;
        Require(UnityEngine.Time.frameCount == frame && elapsed >= 0.025f,
            "UI clock did not advance with elapsed time inside the same frame.");
    }

    private static void VerifyBubbleGenerationLength()
    {
        const string prompt = "원문 유지: 피해 3.5\n상황: \"출혈\"";
        LocalLlmRequestProfile profile = LocalLlmRequestProfiles.BubbleLine;
        LlmStaticSchemaDefinition catalog = LlmStaticSchemaCatalog.Require(profile.Id);
        byte[] before = catalog.Utf8Bytes.ToArray();
        string beforeHash = catalog.Hash;
        JObject original = JObject.Parse(Encoding.UTF8.GetString(before));
        Require((int)original["properties"]["line"]["maxLength"] == 80,
            "The consumer BubbleLine schema changed its original limit.");
        JObject wire = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload("test", profile, catalog, prompt)));
        JObject narrowed = (JObject)wire["response_format"]["json_schema"]["schema"];
        JObject expected = (JObject)original.DeepClone();
        expected["properties"]["line"]["maxLength"] = 24;
        Require(JToken.DeepEquals(narrowed, expected)
                && (string)wire["messages"][1]["content"] == prompt
                && ((string)wire["messages"][0]["content"]).Contains("최대 24자", StringComparison.Ordinal)
                && ((string)wire["messages"][0]["content"]).Contains("자연스러운", StringComparison.Ordinal)
                && ((string)wire["messages"][0]["content"]).Contains("한 문장", StringComparison.Ordinal)
                && before.SequenceEqual(catalog.Utf8Bytes)
                && beforeHash == catalog.Hash
                && (int)JObject.Parse(catalog.Json)["properties"]["line"]["maxLength"] == 80
                && profile.MaxQueueAgeSeconds == 3f,
            "Bubble generation changed caller text, original schema, or queue age.");

        LocalLlmRequestProfile record = LocalLlmRequestProfiles.CharacterRecord;
        LlmStaticSchemaDefinition recordCatalog = LlmStaticSchemaCatalog.Require(record.Id);
        byte[] recordBefore = recordCatalog.Utf8Bytes.ToArray();
        JObject recordOriginal = JObject.Parse(Encoding.UTF8.GetString(recordBefore));
        JObject recordWire = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload("test", record, recordCatalog, prompt)));
        Require((int)recordOriginal["properties"]["line"]["maxLength"] == 60
                && JToken.DeepEquals(recordOriginal, recordWire["response_format"]["json_schema"]["schema"])
                && (string)recordWire["messages"][1]["content"] == prompt
                && recordBefore.SequenceEqual(recordCatalog.Utf8Bytes),
            "Bubble generation limit leaked into CharacterRecord or its caller text.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void VerifyRejectionContract()
    {
        const string original = "{\"error\":{\"message\":\"invalid effect evidence\"},\"requiresInputChange\":true}";
        Require(DungeonStoryHostStructuredChatBackend.TryReadInputRejection(original, out string reason)
                && reason == "invalid effect evidence", "Typed content rejection lost its original reason.");
        foreach (string transientOrInvalid in new[] { "not json", "{}",
                     "{\"error\":{\"message\":\"timeout\"},\"requiresInputChange\":false}",
                     "{\"error\":\"invalid\",\"requiresInputChange\":true}",
                     "{\"error\":{\"message\":\"invalid\"},\"requiresInputChange\":\"true\"}" })
            Require(!DungeonStoryHostStructuredChatBackend.TryReadInputRejection(transientOrInvalid, out _),
                "A transport or malformed body was treated as a typed content rejection.");
        var result = new LocalLlmResult(LocalLlmRequestStatus.Failed, string.Empty, reason,
            "original event", rawModelContent: original, requiresInputChange: true);
        Require(result.RequiresInputChange && result.RawModelContent == original && !result.IsSuccess
                && !new LocalLlmResult(LocalLlmRequestStatus.TimedOut, "", "timeout", "").RequiresInputChange,
            "Content rejection became success or altered the legacy timeout contract.");
    }

    // The queue state lives in a different runtime assembly and is internal.
    // Reflection invokes its real methods, as in LocalLlmRequestQueueLifecycleFixture.
    private sealed class StateFixture
    {
        private const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly Type stateType = typeof(LocalLlmRequestQueue).Assembly.GetType("LocalLlmRequestState", true);
        private readonly Type requestType = typeof(LocalLlmRequestQueue).Assembly.GetType("LocalLlmQueuedRequest", true);
        private readonly object state;

        public StateFixture() { state = Activator.CreateInstance(stateType, nonPublic: true); }

        public object Put(LocalLlmRequestProfile profile, string correlation,
            Func<bool> ownerIsAlive = null, Action<LocalLlmResult> callback = null)
        {
            object request = Activator.CreateInstance(requestType, new object[]
                { profile, "controlled queue input", string.Empty, 30f, 0f, correlation, null, callback, false, 0, ownerIsAlive });
            object[] arguments = { request, 32, null };
            object admission = Method("Admit").Invoke(state, arguments);
            Require(admission.ToString() == "Accepted" && arguments[2] == null, "Queue admission failed.");
            return request;
        }

        public object Begin(bool separateInteractiveLane, int slots = 2)
        {
            object[] arguments = { 1f, slots, null, separateInteractiveLane };
            bool began = (bool)Method("TryBeginNext").Invoke(state, arguments);
            return began ? arguments[2] : null;
        }

        public void Finish(object request) => Method("Finish").Invoke(state, new[] { request });
        public bool IsRunning(object request) => (bool)Method("IsRunning").Invoke(state, new[] { request });
        public bool Contains(string correlation) => (bool)Method("ContainsCorrelation").Invoke(state, new object[] { correlation });
        public bool CanAdmitRealtime() => (bool)Method("CanAdmitRealtime").Invoke(state, Array.Empty<object>());
        public bool CanAdmitOrdinary(int maximumPending) => (bool)Method("CanAdmitOrdinary")
            .Invoke(state, new object[] { maximumPending });
        public bool CanAdmitOrdinaryProfile(LocalLlmRequestProfile profile) => (bool)Method("CanAdmitOrdinaryProfile")
            .Invoke(state, new object[] { profile });
        public IList TakeRetired() => (IList)Method("TakeRetired").Invoke(state, Array.Empty<object>());
        public bool CompleteCancelled(object request)
        {
            object[] arguments = { null };
            bool taken = (bool)requestType.GetMethod("TryTakeCallback").Invoke(request, arguments);
            if (taken)
                ((Action<LocalLlmResult>)arguments[0])?.Invoke(new LocalLlmResult(
                    LocalLlmRequestStatus.Cancelled, string.Empty, string.Empty, string.Empty));
            return taken;
        }
        public int Cancel(string correlation) => ((ICollection)Method("CancelCorrelation")
            .Invoke(state, new object[] { correlation })).Count;
        private MethodInfo Method(string name) => stateType.GetMethod(name, InstanceMethods)
            ?? throw new MissingMethodException(stateType.FullName, name);
    }
}
#endif
