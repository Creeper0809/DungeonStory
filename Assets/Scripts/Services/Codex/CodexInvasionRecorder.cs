using System;

public static class CodexInvasionRecorder
{
    public const string BreakthroughIntruderId = "intruder_breakthrough";

    public static void RecordDefenseObservation(CodexState state, DefenseActivationSnapshot report)
    {
        if (state == null || report == null)
        {
            return;
        }

        CodexObservationRecorder.ObserveFacility(state, report.Facility, CodexInfoSource.Observation);
        SeedBreakthroughIntruder(state);
        foreach (string tag in report.EffectTags)
        {
            foreach (string info in CodexInvasionObservationMapper.FromEffectTag(tag))
            {
                AddInvasionInfo(state, info, CodexInfoSource.Observation);
            }
        }

        if (report.MovementDelaySeconds > 0f)
        {
            AddInvasionInfo(state, "약점: 감속", CodexInfoSource.Observation);
        }

        if (report.TotalDamage > 0f)
        {
            AddInvasionInfo(state, "약점: 피해 누적", CodexInfoSource.Observation);
        }
    }

    public static void RecordCombatReport(CodexState state, InvasionCombatReportSnapshot report)
    {
        if (state == null || report == null)
        {
            return;
        }

        SeedBreakthroughIntruder(state);
        foreach (string observation in report.Observations ?? Array.Empty<string>())
        {
            AddInvasionInfo(
                state,
                CodexInvasionObservationMapper.NormalizeObservation(observation),
                CodexInfoSource.Observation);
        }

        foreach (InvasionFacilitySnapshot facility in report.DamagedFacilities ?? Array.Empty<InvasionFacilitySnapshot>())
        {
            CodexObservationRecorder.ObserveFacility(state, facility?.Building, CodexInfoSource.Observation);
            AddInvasionInfo(state, "성향: 시설 파괴 우선", CodexInfoSource.Observation);
        }
    }

    public static void RecordFacilityDamage(CodexState state, BuildableObject facility)
    {
        CodexObservationRecorder.ObserveFacility(state, facility, CodexInfoSource.Observation);
        AddInvasionInfo(state, "성향: 시설 파괴 우선", CodexInfoSource.Observation);
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

        SeedBreakthroughIntruder(state);
        state.AddInfo(
            CodexEntryCategory.Invasion,
            BreakthroughIntruderId,
            "돌파형 침입자",
            info,
            source);
    }
}
