using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CodexInvasionRecorder
{
    public const string BreakthroughIntruderId = "intruder_breakthrough";

    public static void Record(CodexState state, CodexInvasionObservationSnapshot snapshot)
    {
        if (state == null || snapshot == null)
        {
            return;
        }

        SeedBreakthroughIntruder(state);
        foreach (CodexFacilityObservationSnapshot facility in snapshot.Facilities
                     ?? Array.Empty<CodexFacilityObservationSnapshot>())
        {
            CodexObservationRecorder.ObserveFacility(state, facility);
        }

        foreach (string observation in snapshot.Observations ?? Array.Empty<string>())
        {
            AddInvasionInfo(state, observation, CodexInfoSource.Observation);
        }
    }

    public static void SeedBreakthroughIntruder(CodexState state)
    {
        if (state == null)
        {
            return;
        }

        CodexEntryRecord entry = state.GetOrCreate(
            CodexEntryCategory.Invasion,
            BreakthroughIntruderId,
            "돌파형 침입자");
        entry.AddInfo("주의: 사장 캐릭터 처치", CodexInfoSource.System);
        entry.AddInfo("주의: 사장방 돌파", CodexInfoSource.System);
        entry.AddInfo("성향: 시간이 지날수록 사장 위치 추적", CodexInfoSource.System);
        entry.AddInfo("저항: 공포 효과", CodexInfoSource.System);
    }

    private static void AddInvasionInfo(CodexState state, string info, CodexInfoSource source)
    {
        if (state == null || string.IsNullOrWhiteSpace(info))
        {
            return;
        }

        state.AddInfo(
            CodexEntryCategory.Invasion,
            BreakthroughIntruderId,
            "돌파형 침입자",
            info,
            source);
    }
}
