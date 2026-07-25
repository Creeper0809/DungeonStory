using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FirstRunObjectiveDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Run/Run First Run Objective Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("First-run objective scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new List<string>();
        VerifyObjectiveChain(failures);
        VerifyCompletedRunPersistence(failures);

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                Debug.LogError($"FIRST_RUN_OBJECTIVE FAIL: {failure}");
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("FIRST_RUN_OBJECTIVE_SCENARIOS PASS");
        }

        return true;
    }

    private static void VerifyObjectiveChain(List<string> failures)
    {
        Expect(
            FirstRunObjectiveId.ChooseOwner,
            CreateSnapshot(hasOwner: false),
            "owner choice",
            failures);
        Expect(
            FirstRunObjectiveId.MakeUsableRoom,
            CreateSnapshot(hasUsableRoom: false),
            "formal room",
            failures);
        Expect(
            FirstRunObjectiveId.AcquireBlueprint,
            CreateSnapshot(),
            "blueprint acquisition",
            failures);
        Expect(
            FirstRunObjectiveId.CompleteResearch,
            CreateSnapshot(researchTaskCount: 1, researchRatio: 0.42f),
            "research completion",
            failures);
        Expect(
            FirstRunObjectiveId.CompleteSettlement,
            CreateSnapshot(researchTaskCount: 1, completedResearchCount: 1),
            "first settlement",
            failures);
        Expect(
            FirstRunObjectiveId.DefendInvasion,
            CreateSnapshot(
                researchTaskCount: 1,
                completedResearchCount: 1,
                settlementCount: 1),
            "first invasion",
            failures);
        Expect(
            FirstRunObjectiveId.AdvanceOffense,
            CreateSnapshot(
                researchTaskCount: 1,
                completedResearchCount: 1,
                settlementCount: 1,
                defendedInvasionCount: 1,
                completedOffenseTargetCount: 0,
                totalOffenseTargetCount: 6),
            "offense progression",
            failures);
        Expect(
            FirstRunObjectiveId.RevealTruth,
            CreateSnapshot(
                researchTaskCount: 1,
                completedResearchCount: 1,
                settlementCount: 1,
                defendedInvasionCount: 1,
                completedOffenseTargetCount: 5,
                totalOffenseTargetCount: 6),
            "truth revelation",
            failures);
        Expect(
            FirstRunObjectiveId.None,
            CreateSnapshot(completedRunCount: 1),
            "subsequent-run hidden state",
            failures);
    }

    private static void VerifyCompletedRunPersistence(List<string> failures)
    {
        MetaProgressionState state = new MetaProgressionState();
        state.Restore(
            30,
            10,
            Array.Empty<KeyValuePair<string, int>>(),
            Array.Empty<string>(),
            completedRunCount: 2);
        state.RecordRunCompleted();
        state.Merge(
            20,
            5,
            Array.Empty<KeyValuePair<string, int>>(),
            Array.Empty<string>(),
            completedRunCount: 1);
        if (state.CompletedRunCount != 3)
        {
            failures.Add($"completed run count should preserve the maximum, got {state.CompletedRunCount}");
        }

        DungeonMetaProfileData profile = new DungeonMetaProfileData { completedRunCount = 3 };
        DungeonMetaProfileData roundTrip = JsonUtility.FromJson<DungeonMetaProfileData>(
            JsonUtility.ToJson(profile));
        if (roundTrip == null || roundTrip.completedRunCount != 3)
        {
            failures.Add("meta profile JSON did not round-trip completedRunCount");
        }
    }

    private static FirstRunObjectiveSnapshot CreateSnapshot(
        bool hasOwner = true,
        bool hasUsableRoom = true,
        int researchTaskCount = 0,
        int completedResearchCount = 0,
        float researchRatio = 0f,
        int settlementCount = 0,
        int defendedInvasionCount = 0,
        int currentDay = 1,
        DungeonRunPhase phase = DungeonRunPhase.Preparation,
        DungeonRunOutcome outcome = DungeonRunOutcome.None,
        int completedRunCount = 0,
        int completedOffenseTargetCount = 0,
        int totalOffenseTargetCount = 0)
    {
        return new FirstRunObjectiveSnapshot(
            hasOwner,
            hasUsableRoom,
            researchTaskCount,
            completedResearchCount,
            researchRatio,
            settlementCount,
            defendedInvasionCount,
            currentDay,
            phase,
            outcome,
            completedRunCount,
            completedOffenseTargetCount,
            totalOffenseTargetCount);
    }

    private static void Expect(
        FirstRunObjectiveId expected,
        FirstRunObjectiveSnapshot snapshot,
        string label,
        List<string> failures)
    {
        FirstRunObjectiveId actual = FirstRunObjectiveResolver.Resolve(snapshot).Id;
        if (actual != expected)
        {
            failures.Add($"{label}: expected {expected}, got {actual}");
        }
    }
}
