#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class RandomStreamIsolationDebugScenarios
{
    [MenuItem("Tools/DungeonStory/V27/Verify Random Stream Isolation")]
    public static void RunFromMenu()
    {
        Debug.Log(RunAll());
    }

    public static string RunAll()
    {
        VerifyCharacterScopeIsolation();
        VerifyMovementDecisionIsolation();
        VerifyActorSpawnDespawnIsolation();
        VerifyCausalConeIsolation();
        VerifyDiagnosticDrawCountsAndRestore();
        VerifyRegistryRestoreStagingIsolatesRandomStreams();
        VerifyKeyAddressedEventOrderIndependence();
        VerifyDuplicateEventKeyFails();
        VerifyLegacyGlobalStreamsFail();
        VerifyExternalCharacterIdAuthority();
        return "RESULT=PASS; suite=RandomStreamIsolationDebugScenarios\n"
            + "PASS RNG_ACTOR_EXTRA_DRAWS_CROSS_TALK_ZERO draws=100\n"
            + "PASS RNG_DECISION_MOVEMENT_CROSS_TALK_ZERO draws=100\n"
            + "PASS RNG_ACTOR_SPAWN_DESPAWN_EXISTING_STREAMS_UNCHANGED\n"
            + "PASS RNG_KEYED_EVENT_ORDER_INDEPENDENT\n"
            + "PASS RNG_DUPLICATE_EVENT_KEY_REJECTED\n"
            + "PASS RNG_SAVE_RESTORE_NEXT_DRAW_EXACT\n"
            + "PASS RNG_DIAGNOSTIC_DRAW_COUNT_RESTORED_TO_ZERO\n"
            + "PASS RNG_REGISTRY_RESTORE_STAGE_MUTATION_TRANSACTIONAL\n"
            + "PASS RNG_DUPLICATE_CHARACTER_ID_REJECTED\n"
            + "PASS RNG_CAUSAL_CONE_OUTSIDE_STREAMS_UNCHANGED\n"
            + "PASS RNG_LEGACY_GLOBAL_CHARACTER_STREAMS_REJECTED\n";
    }

    private static void VerifyCharacterScopeIsolation()
    {
        RandomStreamProvider provider = new(271828);
        CharacterId actorA = CharacterId.FromStableSuffix("rng-isolation-a");
        CharacterId actorB = CharacterId.FromStableSuffix("rng-isolation-b");
        IRandomStream streamA = provider.Get(
            Decision(actorA));
        IRandomStream streamB = provider.Get(
            Decision(actorB));
        ulong beforeB = streamB.State;
        for (int index = 0; index < 100; index++)
        {
            streamA.NextInt(0, 1000);
        }

        Require(streamB.State == beforeB,
            "Actor A decision draws changed actor B decision state.");
        Require(streamB.NextInt(0, 1000)
            == new RandomStreamProvider(271828)
                .Get(Decision(actorB))
                .NextInt(0, 1000),
            "Actor B sequence changed after actor A extra draws.");
    }

    private static void VerifyMovementDecisionIsolation()
    {
        RandomStreamProvider provider = new(314159);
        CharacterId actor = CharacterId.FromStableSuffix("rng-isolation-same-actor");
        IRandomStream decision = provider.Get(
            Decision(actor));
        IRandomStream movement = provider.Get(
            Movement(actor));
        ulong beforeMovement = movement.State;
        for (int index = 0; index < 100; index++)
        {
            decision.NextFloat();
        }

        Require(movement.State == beforeMovement,
            "Decision draws changed the same actor's movement stream.");
    }

    private static void VerifyActorSpawnDespawnIsolation()
    {
        RandomStreamProvider provider = new(271829);
        CharacterId actorA = CharacterId.FromStableSuffix("rng-existing-a");
        CharacterId actorB = CharacterId.FromStableSuffix("rng-existing-b");
        IRandomStream a = provider.Get(Decision(actorA));
        IRandomStream b = provider.Get(Decision(actorB));
        ulong beforeA = a.State;
        ulong beforeB = b.State;
        CharacterId transient = CharacterId.FromStableSuffix("rng-transient");
        IRandomStream transientDecision = provider.Get(Decision(transient));
        IRandomStream transientMovement = provider.Get(Movement(transient));
        transientDecision.NextInt(0, 100);
        transientMovement.NextInt(0, 100);
        transientDecision = null;
        transientMovement = null;
        Require(a.State == beforeA && b.State == beforeB,
            "Creating and releasing an actor's streams changed existing actors.");
    }

    private static void VerifyCausalConeIsolation()
    {
        RandomStreamProvider provider = new(271830);
        CharacterId affected = CharacterId.FromStableSuffix("rng-cone-affected");
        CharacterId unrelated = CharacterId.FromStableSuffix("rng-cone-unrelated");
        IRandomStream affectedDecision = provider.Get(Decision(affected));
        IRandomStream affectedMovement = provider.Get(Movement(affected));
        IRandomStream unrelatedDecision = provider.Get(Decision(unrelated));
        IRandomStream exogenous = provider.Get("crop:genetics");
        ulong movementBefore = affectedMovement.State;
        ulong unrelatedBefore = unrelatedDecision.State;
        ulong exogenousBefore = exogenous.State;
        affectedDecision.NextInt(0, 100);
        Require(affectedMovement.State == movementBefore
                && unrelatedDecision.State == unrelatedBefore
                && exogenous.State == exogenousBefore,
            "An affected decision stream escaped its causal cone.");
    }

    private static void VerifyDiagnosticDrawCountsAndRestore()
    {
        RandomStreamProvider provider = new(161803);
        CharacterId actor = CharacterId.FromStableSuffix("rng-diagnostics");
        string streamId = Decision(actor);
        IRandomStream stream = provider.Get(streamId);
        stream.NextFloat();
        stream.NextInt(0, 10);
        stream.Chance(0.5f);
        RandomStreamDiagnosticSnapshot snapshot = Find(provider.Capture(), streamId);
        Require(snapshot.DrawCount == 3L,
            $"Expected three state-advancing draws, got {snapshot.DrawCount}.");

        IReadOnlyList<RandomStreamStateSnapshot> saved = provider.CaptureStates();
        int expectedNext = stream.NextInt(0, 10000);
        provider.RestoreStates(provider.RootSeed, saved);
        RandomStreamDiagnosticSnapshot restored = Find(provider.Capture(), streamId);
        Require(restored.DrawCount == 0L,
            "DrawCount must reset at the diagnostic restore boundary.");
        Require(provider.Get(streamId).NextInt(0, 10000) == expectedNext,
            "Saved random state did not reproduce the next draw.");
    }

    private static void VerifyRegistryRestoreStagingIsolatesRandomStreams()
    {
        VerifyRegistryRestoreStagingCase(
            RandomStreamStageBehavior.ThrowDuringStage,
            expectSuccess: false);
        VerifyRegistryRestoreStagingCase(
            RandomStreamStageBehavior.ThrowDuringCommit,
            expectSuccess: false);
        VerifyRegistryRestoreStagingCase(
            RandomStreamStageBehavior.Succeed,
            expectSuccess: true);
    }

    private static void VerifyRegistryRestoreStagingCase(
        RandomStreamStageBehavior behavior,
        bool expectSuccess)
    {
        DungeonRuntimeAggregateRootStore root = new();
        RandomStreamProvider provider = new(root);
        IRandomStream existing = provider.Get("qa:existing");
        existing.Restore(0x123456789ABCDEF0UL);
        existing.NextInt(0, 1000);

        RandomStreamStageMutationSection section = new(
            provider,
            root,
            behavior);
        DungeonSaveSectionRegistry registry = new(
            new IDungeonSaveSection[] { section },
            root,
            new IDungeonRestoreTransactionParticipant[] { provider });
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        IReadOnlyList<RandomStreamStateSnapshot> statesBefore =
            provider.CaptureStates();
        IReadOnlyList<RandomStreamDiagnosticSnapshot> diagnosticsBefore =
            provider.Capture();
        int revisionBefore = root.PublishedRestoreRevision;
        DungeonGameRestoreReport report = new();

        bool restored = registry.RestoreAll(envelopes, report);

        Require(section.ObservedRestoreStaging,
            $"Random stream {behavior} stage ran outside aggregate restore staging.");
        Require(section.StageMutationCompleted,
            $"Random stream {behavior} stage did not exercise both stream mutations.");
        Require(!root.IsRestoreStaging,
            $"Random stream {behavior} restore left aggregate staging active.");
        if (!expectSuccess)
        {
            Require(!restored && !report.Success,
                $"Random stream {behavior} restore unexpectedly succeeded.");
            Require(root.PublishedRestoreRevision == revisionBefore,
                $"Random stream {behavior} failure published an aggregate root.");
            Require(SameStates(statesBefore, provider.CaptureStates()),
                $"Random stream {behavior} failure changed persisted stream IDs or states.");
            Require(SameDiagnostics(diagnosticsBefore, provider.Capture()),
                $"Random stream {behavior} failure changed diagnostic stream state.");
            return;
        }

        Require(restored && report.Success,
            "Successful random stream restore-stage mutation was rejected: "
            + string.Join(" | ", report.Errors));
        Require(root.PublishedRestoreRevision == revisionBefore + 1,
            "Successful random stream restore-stage mutation did not publish its root.");
        Require(section.StagedStates.Count == 2
                && string.Equals(
                    section.StagedStates[0].StreamId,
                    "qa:existing",
                    StringComparison.Ordinal)
                && string.Equals(
                    section.StagedStates[1].StreamId,
                    "qa:staged",
                    StringComparison.Ordinal)
                && section.StagedStates[0].State != statesBefore[0].State,
            "Successful random stream stage did not change the existing stream and add the staged stream.");
        Require(SameStates(section.StagedStates, provider.CaptureStates()),
            "Successful random stream stage states were not published exactly.");
        Require(SameDiagnostics(
                section.StagedDiagnostics,
                provider.Capture()),
            "Successful random stream stage diagnostics were not published exactly.");
    }

    private static bool SameStates(
        IReadOnlyList<RandomStreamStateSnapshot> expected,
        IReadOnlyList<RandomStreamStateSnapshot> actual)
    {
        if (expected == null || actual == null || expected.Count != actual.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (!string.Equals(
                    expected[index].StreamId,
                    actual[index].StreamId,
                    StringComparison.Ordinal)
                || expected[index].State != actual[index].State)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameDiagnostics(
        IReadOnlyList<RandomStreamDiagnosticSnapshot> expected,
        IReadOnlyList<RandomStreamDiagnosticSnapshot> actual)
    {
        if (expected == null || actual == null || expected.Count != actual.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (!string.Equals(
                    expected[index].StreamId,
                    actual[index].StreamId,
                    StringComparison.Ordinal)
                || expected[index].State != actual[index].State
                || expected[index].DrawCount != actual[index].DrawCount)
            {
                return false;
            }
        }

        return true;
    }

    private static void VerifyKeyAddressedEventOrderIndependence()
    {
        CounterfactualRandomKey a = new(
            424242, "v27-six-adult", "harvest-burst", "crop:barley", 1, 0);
        CounterfactualRandomKey b = new(
            424242, "v27-six-adult", "hauler-downed", "character:test", 1, 0);
        int aFirst = a.CreateSequence().NextInt(0, int.MaxValue);
        int bSecond = b.CreateSequence().NextInt(0, int.MaxValue);
        int bFirst = b.CreateSequence().NextInt(0, int.MaxValue);
        int aSecond = a.CreateSequence().NextInt(0, int.MaxValue);
        Require(aFirst == aSecond && bFirst == bSecond,
            "Key-addressed event output changed with call order.");
    }

    private static void VerifyDuplicateEventKeyFails()
    {
        CounterfactualRandomKeySet set = new();
        CounterfactualRandomKey key = new(
            777, "v27-six-adult", "facility-disabled", "facility:kitchen", 0, 0);
        set.CreateUnique(key);
        bool rejected = false;
        try
        {
            set.CreateUnique(key);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "Duplicate counterfactual event key was accepted.");
    }

    private static void VerifyLegacyGlobalStreamsFail()
    {
        RandomStreamProvider provider = new(19);
        foreach (string legacyId in new[] { "character-ai", "character-movement" })
        {
            bool rejected = false;
            try
            {
                provider.Get(legacyId);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Require(rejected, $"Legacy global stream '{legacyId}' was accepted.");
        }
    }

    private static void VerifyExternalCharacterIdAuthority()
    {
        Type authority = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(
                "CharacterDeprivationAuthorityDebugScenarios",
                throwOnError: false))
            .FirstOrDefault(value => value != null)
            ?? throw new InvalidOperationException(
                "Character deprivation authority scenario type is missing.");
        MethodInfo runAll = authority.GetMethod(
            "RunAll",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "Character deprivation authority RunAll is missing.");
        object raw = runAll.Invoke(null, null);
        Require(raw is List<string> errors && errors.Count == 0,
            "CharacterId authority regression failed: "
            + (raw is List<string> values
                ? string.Join(" | ", values)
                : "invalid-result"));
    }

    private static RandomStreamDiagnosticSnapshot Find(
        IReadOnlyList<RandomStreamDiagnosticSnapshot> snapshots,
        string streamId)
    {
        for (int index = 0; index < snapshots.Count; index++)
        {
            if (string.Equals(snapshots[index].StreamId, streamId, StringComparison.Ordinal))
            {
                return snapshots[index];
            }
        }

        throw new InvalidOperationException(
            $"Random stream diagnostic '{streamId}' is missing.");
    }

    private static string Decision(CharacterId actor) =>
        "character-ai:" + actor.Value;

    private static string Movement(CharacterId actor) =>
        "character-movement:" + actor.Value;

    private enum RandomStreamStageBehavior
    {
        ThrowDuringStage,
        ThrowDuringCommit,
        Succeed
    }

    private sealed class RandomStreamStageMutationSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly RandomStreamProvider provider;
        private readonly DungeonRuntimeAggregateRootStore root;
        private readonly RandomStreamStageBehavior behavior;

        public RandomStreamStageMutationSection(
            RandomStreamProvider provider,
            DungeonRuntimeAggregateRootStore root,
            RandomStreamStageBehavior behavior)
        {
            this.provider = provider
                ?? throw new ArgumentNullException(nameof(provider));
            this.root = root
                ?? throw new ArgumentNullException(nameof(root));
            this.behavior = behavior;
        }

        public string SectionId => "qa.random-stream-stage-mutation";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public bool ObservedRestoreStaging { get; private set; }
        public bool StageMutationCompleted { get; private set; }
        public IReadOnlyList<RandomStreamStateSnapshot> StagedStates { get; private set; } =
            Array.Empty<RandomStreamStateSnapshot>();
        public IReadOnlyList<RandomStreamDiagnosticSnapshot> StagedDiagnostics { get; private set; } =
            Array.Empty<RandomStreamDiagnosticSnapshot>();

        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || !string.Equals(payloadJson, "{}", StringComparison.Ordinal))
            {
                report.AddError("Invalid random stream stage-mutation payload.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            ObservedRestoreStaging = root.IsRestoreStaging;
            provider.Get("qa:existing").NextInt(0, 1000);
            provider.Get("qa:staged").NextInt(0, 1000);
            StagedStates = provider.CaptureStates();
            StagedDiagnostics = provider.Capture();
            StageMutationCompleted = true;
            if (behavior == RandomStreamStageBehavior.ThrowDuringStage)
            {
                throw new InvalidOperationException(
                    "Expected random stream stage failure.");
            }

            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ =>
                {
                    if (behavior == RandomStreamStageBehavior.ThrowDuringCommit)
                    {
                        throw new InvalidOperationException(
                            "Expected random stream commit failure.");
                    }
                });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            IDungeonSaveRestoreStage stage = StageRestore(
                payloadJson,
                sectionVersion,
                report);
            if (report.Success)
            {
                stage.Commit(report);
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
