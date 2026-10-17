#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VContainer;

internal static class CharacterWorldRestoreLifetimeDebugScenarios
{
    internal static void VerifyActualContainer(
        IObjectResolver container,
        DungeonCharacterWorldSaveData source)
    {
        var service = container.Resolve<ICharacterWorldSaveService>();
        var participant = service as IDungeonRestoreTransactionParticipant;
        var candidates = container.Resolve<IRestoreWorldCandidateQuery>();
        var gridProvider = container.Resolve<IGridSystemProvider>();
        var ownerProvider = container.Resolve<IOwnerRunManagerProvider>();
        Require(participant != null, "Actual character restore participant is missing.");
        Require(gridProvider.TryGetGrid(out Grid grid), "Actual character restore grid is missing.");
        Require(ownerProvider.TryGetManager(out var manager), "Actual character restore owner is missing.");
        CharacterActor liveOwner = manager.CurrentOwnerActor;
        int[] liveActors = ActiveActorIds();
        string input = JsonUtility.ToJson(source);
        var rows = new List<string> { "case\tresult" };
        Require(!candidates.TryGetCharacters(out _), "A prior candidate is still indexed.");

        CharacterWorldRestoreCandidate first = null;
        CharacterWorldRestoreCandidate second = null;
        CharacterWorldRestoreCandidate third = null;
        CharacterWorldRestoreCandidate fourth = null;
        try
        {
            first = service.PrepareRestoreCandidate(grid, source);
            Require(candidates.TryGetCharacters(out var firstActors)
                    && firstActors.Count == first.RestoredCount && firstActors.Count > 0,
                "Direct preparation did not expose its detached actor view.");
            participant.BeginRestoreCandidate();
            participant.DiscardRestoreCandidate();
            Require(!candidates.TryGetCharacters(out _)
                    && firstActors.All(actor => actor == null),
                "Participant cancellation retained prepared actors or their view.");
            Pass("prepared-participant-cancel");

            second = service.PrepareRestoreCandidate(grid, source);
            Require(candidates.TryGetCharacters(out var secondActors),
                "Preparation after participant cancellation failed.");
            int revision = candidates.Revision;
            participant.BeginRestoreCandidate();
            Reject(() => service.StageRestoreCandidate(first), "does not match the prepared world");
            Reject(() => service.StageRestoreCandidate(first), "does not match the prepared world");
            CurrentSecond();
            Pass("stale-stage-rejected-twice");

            first.Discard();
            first.Discard();
            CurrentSecond();
            Pass("stale-discard-preserves-next-candidate");

            Reject(() => service.PrepareRestoreCandidate(grid, source), "already prepared");
            bool nullRejected = false;
            try { service.StageRestoreCandidate(null); }
            catch (ArgumentNullException) { nullRejected = true; }
            Require(nullRejected, "Null stage was not rejected.");
            CurrentSecond();
            Pass("prepared-duplicate-and-null-stage-preserve-state");

            service.StageRestoreCandidate(second);
            second.Discard();
            Reject(() => service.StageRestoreCandidate(second), "staging is not active");
            CurrentSecond();
            Pass("stage-consumes-once-token-discard-is-harmless");

            // Admission must reject before inspecting or constructing another
            // world, even when the attempted second payload is invalid.
            Reject(() => service.PrepareRestoreCandidate(grid, null), "staged or published");
            CurrentSecond();
            Pass("staged-admission-precedes-payload-validation");

            participant.DiscardRestoreCandidate();
            participant.DiscardRestoreCandidate();
            Require(!candidates.TryGetCharacters(out _)
                    && secondActors.All(actor => actor == null),
                "Staged cancellation retained actors or their view.");
            Pass("staged-cancel-is-idempotent");

            third = service.PrepareRestoreCandidate(grid, source);
            Reject(() => service.StageRestoreCandidate(third), "staging is not active");
            third.Discard();
            third.Discard();
            Require(!candidates.TryGetCharacters(out _),
                "Rejected direct stage could not be discarded.");
            Pass("direct-stage-rejection-retains-discardable-token");

            fourth = service.PrepareRestoreCandidate(grid, source);
            fourth.Discard();
            Require(!candidates.TryGetCharacters(out _), "Final retry left a candidate indexed.");
            Pass("fresh-prepare-after-both-cancellation-paths");

            Require(ReferenceEquals(manager.CurrentOwnerActor, liveOwner)
                    && liveOwner != null && liveOwner.gameObject.activeInHierarchy
                    && ActiveActorIds().SequenceEqual(liveActors)
                    && JsonUtility.ToJson(source) == input,
                "Candidate lifetime commands changed the live actors, owner, or input DTO.");
            Pass("live-world-and-input-unchanged");

            void CurrentSecond()
            {
                Require(candidates.TryGetCharacters(out var current)
                        && ReferenceEquals(current, secondActors)
                        && current.All(actor => actor != null && actor.IsDetachedRestoreCandidate)
                        && candidates.Revision == revision,
                    "Rejected or stale command changed the current candidate view.");
            }
        }
        finally
        {
            first?.Discard();
            second?.Discard();
            third?.Discard();
            fourth?.Discard();
            participant.DiscardRestoreCandidate();
        }

        string path = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath)
            ?? throw new InvalidOperationException("Unity log directory is unavailable."),
            "character-restore-lifetime.tsv");
        using (var writer = new StreamWriter(new FileStream(path,
                   FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
            foreach (string row in rows) writer.WriteLine(row);
        Debug.Log("CHARACTER_RESTORE_LIFETIME=PASS; actual owner; cancel; stale token; stage; retry; unchanged live world");

        void Pass(string name) => rows.Add(name + "\tPASS");
    }

    private static int[] ActiveActorIds() => UnityEngine.Object
        .FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .Select(actor => actor.GetInstanceID()).OrderBy(value => value).ToArray();

    private static void Reject(Action action, string expected)
    {
        try { action(); }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(expected, StringComparison.Ordinal),
                "Unexpected rejection: " + exception.Message);
            return;
        }
        throw new InvalidOperationException("Expected rejection: " + expected);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
