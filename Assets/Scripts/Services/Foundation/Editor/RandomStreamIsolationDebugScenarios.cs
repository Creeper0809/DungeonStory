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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
