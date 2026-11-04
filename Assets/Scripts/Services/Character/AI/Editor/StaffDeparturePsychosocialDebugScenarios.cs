#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static partial class StaffDiscontentDebugScenarios
{
    public static void VerifyPsychosocialDepartureForBatch()
    {
        VerifyPermanentDepartureRetiresOnlyTargetPsychosocialState();
        VerifyRejectedDepartureRestoresPsychosocialState();
    }

    private static void VerifyPermanentDepartureRetiresOnlyTargetPsychosocialState()
    {
        using ScenarioRuntime runtime = new();
        CharacterActor staff = CreateStaff(
            219,
            "Psychosocial Departing Staff",
            10f);
        try
        {
            CharacterId staffId = CharacterPersistentIdentity.Require(staff);
            CharacterId survivorId = new(
                "character:staff-discontent-test:psychosocial-survivor");
            GriefTraumaRuntime grief = new(runtime.RootStore);
            grief.Require(staffId);
            CharacterGriefAggregate survivor = grief.Require(survivorId);
            string survivorBefore = CapturePsychosocialRecord(survivor);

            StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(
                CharacterActor.From(staff),
                out StaffDiscontentOutcome outcome);

            RequirePsychosocialDeparture(
                record != null
                && outcome == StaffDiscontentOutcome.PermanentDeparture
                && record.IsDeparted
                && record.IsPermanentLoss
                && staff.CurrentLifecycleState == CharacterLifecycleState.Despawned,
                "Permanent departure did not reach its terminal staff state.");
            RequirePsychosocialDeparture(
                !grief.TryGet(staffId, out _)
                && grief.Capture().characters.All(value =>
                    !string.Equals(
                        value.characterId,
                        staffId.Value,
                        StringComparison.Ordinal)),
                "Permanent departure retained the departed staff psychosocial record.");
            RequirePsychosocialDeparture(
                grief.TryGet(survivorId, out CharacterGriefAggregate survivorAfter)
                && string.Equals(
                    CapturePsychosocialRecord(survivorAfter),
                    survivorBefore,
                    StringComparison.Ordinal),
                "Permanent departure changed an unrelated psychosocial record.");

            VerifyPsychosocialSnapshotRoundTrip(grief);
            RequirePsychosocialDeparture(
                !grief.TryGet(staffId, out _)
                && grief.TryGet(survivorId, out CharacterGriefAggregate restoredSurvivor)
                && string.Equals(
                    CapturePsychosocialRecord(restoredSurvivor),
                    survivorBefore,
                    StringComparison.Ordinal),
                "Psychosocial restore changed the post-departure owner state.");
        }
        finally
        {
            DestroyStaff(staff);
        }
    }

    private static void VerifyRejectedDepartureRestoresPsychosocialState()
    {
        using ScenarioRuntime runtime = new(rejectTransitionCommit: true);
        CharacterActor staff = CreateStaff(
            220,
            "Psychosocial Rejected Departing Staff",
            10f);
        try
        {
            CharacterId staffId = CharacterPersistentIdentity.Require(staff);
            CharacterId survivorId = new(
                "character:staff-discontent-test:psychosocial-rollback-survivor");
            GriefTraumaRuntime grief = new(runtime.RootStore);
            CharacterGriefAggregate target = grief.Require(staffId);
            CharacterGriefAggregate survivor = grief.Require(survivorId);
            string targetBefore = CapturePsychosocialRecord(target);
            string survivorBefore = CapturePsychosocialRecord(survivor);
            string psychosocialBefore = CapturePsychosocialWorld(grief);
            CharacterLifecycleState lifecycleBefore = staff.CurrentLifecycleState;
            bool rejected = false;

            try
            {
                runtime.Runtime.ProcessStaff(
                    CharacterActor.From(staff),
                    out _);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            RequirePsychosocialDeparture(
                rejected
                && runtime.Runtime.State.Records.Count == 0
                && runtime.Runtime.OutcomeRevision == 0L
                && runtime.Runtime.TransitionRevision == 0L
                && staff.CurrentLifecycleState == lifecycleBefore
                && runtime.CountAllTransitions() == 0
                && runtime.Ledger.GetOutboxSnapshot().Count == 0,
                "Rejected departure did not restore the staff command state.");
            RequirePsychosocialDeparture(
                grief.TryGet(staffId, out CharacterGriefAggregate targetAfter)
                && grief.TryGet(survivorId, out CharacterGriefAggregate survivorAfter)
                && string.Equals(
                    CapturePsychosocialRecord(targetAfter),
                    targetBefore,
                    StringComparison.Ordinal)
                && string.Equals(
                    CapturePsychosocialRecord(survivorAfter),
                    survivorBefore,
                    StringComparison.Ordinal)
                && string.Equals(
                    CapturePsychosocialWorld(grief),
                    psychosocialBefore,
                    StringComparison.Ordinal),
                "Rejected departure did not restore the exact psychosocial owner state.");

            VerifyPsychosocialSnapshotRoundTrip(grief);
            RequirePsychosocialDeparture(
                string.Equals(
                    CapturePsychosocialWorld(grief),
                    psychosocialBefore,
                    StringComparison.Ordinal),
                "Psychosocial restore changed the rolled-back owner state.");
        }
        finally
        {
            DestroyStaff(staff);
        }
    }

    private static void VerifyPsychosocialSnapshotRoundTrip(
        GriefTraumaRuntime grief)
    {
        CharacterPsychosocialWorldSaveData snapshot = grief.Capture();
        string before = JsonUtility.ToJson(snapshot);
        grief.PublishRestore(grief.PrepareRestore(snapshot));
        RequirePsychosocialDeparture(
            string.Equals(
                CapturePsychosocialWorld(grief),
                before,
                StringComparison.Ordinal),
            "Psychosocial snapshot did not survive prepare and publish restore.");
    }

    private static string CapturePsychosocialRecord(
        CharacterGriefAggregate record) =>
        JsonUtility.ToJson(record.Capture());

    private static string CapturePsychosocialWorld(
        GriefTraumaRuntime grief) =>
        JsonUtility.ToJson(grief.Capture());

    private static void RequirePsychosocialDeparture(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
