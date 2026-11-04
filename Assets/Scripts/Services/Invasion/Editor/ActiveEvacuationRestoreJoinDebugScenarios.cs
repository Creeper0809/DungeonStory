#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ActiveEvacuationRestoreJoinDebugScenarios
{
    public static void VerifyForBatch()
    {
        const string sharedId = "character:qa:evacuation-shared";
        const string otherId = "character:qa:evacuation-other";
        var objects = new List<GameObject>();
        var definitions = new List<CharacterSO>();
        try
        {
            CharacterActor live = CreateActor("EvacuationLive", sharedId, objects, definitions);
            CharacterActor dead = CreateActor("EvacuationDead", sharedId, objects, definitions);
            CharacterActor inactive = CreateActor("EvacuationInactive", sharedId, objects, definitions);
            CharacterActor other = CreateActor("EvacuationOther", otherId, objects, definitions);
            dead.SetLifecycleState(CharacterLifecycleState.Despawned);
            inactive.gameObject.SetActive(false);

            Require(live.gameObject.activeInHierarchy && !live.IsDead
                    && live.CurrentLifecycleState == CharacterLifecycleState.Active
                    && dead.IsDead && inactive.CurrentLifecycleState == CharacterLifecycleState.Active
                    && !inactive.gameObject.activeInHierarchy,
                "Fixture did not establish the intended lifecycle states.");
            CharacterActor[] forward = { live, dead, inactive, other, null };
            CharacterActor[] reverse = { null, other, inactive, dead, live };
            string before = DescribeActors(live, dead, inactive, other);
            RequireIndex(InvasionOwnerEvacuationService.BuildActiveRestoreActorIndex(forward),
                sharedId, live, otherId, other);
            RequireIndex(InvasionOwnerEvacuationService.BuildActiveRestoreActorIndex(reverse),
                sharedId, live, otherId, other);
            Require(DescribeActors(live, dead, inactive, other) == before,
                "Restore indexing changed an actor identity or lifecycle state.");

            dead.SetLifecycleState(CharacterLifecycleState.Downed);
            Require(!dead.IsDead && dead.CurrentLifecycleState == CharacterLifecycleState.Downed,
                "Non-active living fixture did not enter the downed state.");
            before = DescribeActors(live, dead, inactive, other);
            RequireIndex(InvasionOwnerEvacuationService.BuildActiveRestoreActorIndex(reverse),
                sharedId, live, otherId, other);
            Require(DescribeActors(live, dead, inactive, other) == before,
                "Indexing a downed actor changed an actor identity or lifecycle state.");

            inactive.gameObject.SetActive(true);
            Require(inactive.gameObject.activeInHierarchy && !inactive.IsDead
                    && inactive.CurrentLifecycleState == CharacterLifecycleState.Active,
                "Duplicate fixture did not become active and alive.");
            before = DescribeActors(live, dead, inactive, other);
            bool rejected = false;
            try
            {
                InvasionOwnerEvacuationService.BuildActiveRestoreActorIndex(forward);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }
            Require(rejected, "Two active living actors with the same ID must be rejected.");
            Require(DescribeActors(live, dead, inactive, other) == before,
                "Rejected restore indexing changed an actor identity or lifecycle state.");
        }
        finally
        {
            foreach (GameObject value in objects)
                if (value != null) Object.DestroyImmediate(value);
            foreach (CharacterSO value in definitions)
                if (value != null) Object.DestroyImmediate(value);
        }
    }

    private static CharacterActor CreateActor(string name, string id,
        List<GameObject> objects, List<CharacterSO> definitions)
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(name);
        objects.Add(actorObject);
        CharacterSO data = CharacterAiPlanDebugFixtures.CreateCharacterData(
            CharacterType.Customer, name, "Slime");
        definitions.Add(data);
        CharacterActor actor = actorObject.GetComponent<CharacterActor>();
        actor.EnsureRuntimeState();
        actor.Initialize(data);
        actor.Identity.SetPersistentId(id);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        return actor;
    }

    private static void RequireIndex(Dictionary<string, CharacterActor> actual,
        string sharedId, CharacterActor live, string otherId, CharacterActor other)
    {
        Require(actual.Count == 2
                && actual.TryGetValue(sharedId, out CharacterActor shared)
                && ReferenceEquals(shared, live)
                && actual.TryGetValue(otherId, out CharacterActor distinct)
                && ReferenceEquals(distinct, other),
            "Restore indexing did not preserve exactly the active living references.");
    }

    private static string DescribeActors(params CharacterActor[] actors)
    {
        var states = new List<string>();
        foreach (CharacterActor actor in actors)
            states.Add(string.Join("|", actor.Identity.PersistentId,
                actor.CurrentLifecycleState, actor.IsDead, actor.gameObject.activeInHierarchy));
        return string.Join(";", states);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
