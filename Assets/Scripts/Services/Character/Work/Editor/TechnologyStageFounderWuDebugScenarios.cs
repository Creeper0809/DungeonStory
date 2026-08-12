#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TechnologyStageFounderWuDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/phase157-technology-founder-wu.md";

    [MenuItem("DungeonStory/Debug/Balance/Phase 157 Technology Founder WU")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        IReadOnlyList<TechnologyWuCheckpoint> checkpoints =
            SettlementLaborBalanceRules.TechnologyCheckpoints;
        IReadOnlyList<V26FounderIndustryBalanceDebugScenarios
            .FounderIndustryDistribution> founderDistributions =
            V26FounderIndustryBalanceDebugScenarios
                .MeasureFounderIndustryDistributions();

        Require(checkpoints.Count == Enum.GetValues(typeof(SettlementTechnologyStage)).Length,
            "Every technology stage requires one WU checkpoint.");
        Require(founderDistributions.Count == 3,
            "Natural, compromise and upper founder distributions are required.");
        Require(founderDistributions.All(value => value.SampleCount >= 500),
            "Every founder regime requires a meaningful deterministic sample.");

        StringBuilder report = new(16384);
        report.AppendLine("# Phase 157 기술 단계·최초 3인방 WU 재계산");
        report.AppendLine();
        report.AppendLine("이 보고서는 하루 180초 안에서 수면·식사·음수·위생·여가를 먼저 차감한 실제 작업시간에, 실제 창립자 생성기에서 나온 숙련·나이·건강·특성 작업 성능을 곱한다. 공정 환산과 자동화는 물리 노동과 분리하며, 정착지 공용 유지·사고·부패·비상 예비 WU는 라이브 회계가 없으면 임의로 지어내지 않는다.");
        report.AppendLine();
        report.AppendLine("## 기술 단계별 1인 일과와 중립 산출");
        report.AppendLine();
        report.AppendLine("| 단계 | 기준일 | 플레이타임 | 수면 | 식사 | 음수 | 위생 | 여가 | 작업 | 실제 노동 WU | 공정 환산 | 자동화 | 산출 등가 WU | 지수 |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        float dayOneSettlementOutput = 0f;
        List<StageSnapshot> stages = new(checkpoints.Count);
        for (int index = 0; index < checkpoints.Count; index++)
        {
            SettlementTechnologyStage stage = (SettlementTechnologyStage)index;
            TechnologyWuCheckpoint checkpoint = checkpoints[index];
            TechnologyDailyRoutineSnapshot routine =
                SettlementLaborBalanceRules.EvaluateTechnologyDailyRoutine(stage);
            float neutralOutput = routine.ActualLaborWu
                * checkpoint.ProcessConversion
                + checkpoint.AutomationWu;
            Require(Mathf.Abs(routine.Budget.TotalSeconds
                    - SettlementLaborBalanceRules.SecondsPerDay) <= .001f,
                $"{stage} routine does not total one day.");
            Require(Mathf.Abs(neutralOutput - checkpoint.OutputEquivalentWu) <= .5f,
                $"{stage} neutral output {neutralOutput:0.###} no longer reproduces checkpoint {checkpoint.OutputEquivalentWu:0.###}.");
            if (index == 0)
            {
                dayOneSettlementOutput = checkpoint.MedianPopulation * neutralOutput;
            }

            float settlementOutput = checkpoint.MedianPopulation * neutralOutput;
            float totalGrowth = dayOneSettlementOutput > 0f
                ? settlementOutput / dayOneSettlementOutput
                : 0f;
            stages.Add(new StageSnapshot(
                stage,
                checkpoint,
                routine,
                neutralOutput,
                settlementOutput,
                totalGrowth));
            report.Append("| ").Append(StageName(stage))
                .Append(" | ").Append(checkpoint.AbsoluteDay)
                .Append(" | ").Append(Playtime(checkpoint.AbsoluteDay))
                .Append(" | ").Append(F(routine.Budget.SleepSeconds))
                .Append(" | ").Append(F(routine.Budget.MealSeconds))
                .Append(" | ").Append(F(routine.Budget.DrinkSeconds))
                .Append(" | ").Append(F(routine.Budget.HygieneSeconds))
                .Append(" | ").Append(F(routine.Budget.RecreationSeconds))
                .Append(" | ").Append(F(routine.Budget.ActiveWorkSeconds))
                .Append(" | ").Append(F(routine.ActualLaborWu))
                .Append(" | ×").Append(F(checkpoint.ProcessConversion))
                .Append(" | +").Append(F(checkpoint.AutomationWu))
                .Append(" | ").Append(F(neutralOutput))
                .Append(" | ").Append(F(neutralOutput
                    / SettlementLaborBalanceRules.BaselineWuPerAdultDay))
                .AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("## 최초 3인방 분포 × 기술 단계");
        report.AppendLine();
        report.AppendLine("필수산업은 현장·식량·제작에 서로 다른 작업자를 최적으로 배치한 3인 합계다. 제작·연구는 파티 안의 최고 담당자 1명이며 연구는 현재 단일 연구 권위와 일치한다. 산출 등가는 필수산업 노동에 해당 단계 공정 환산을 적용하고 3명분의 도메인 자동화만 더한다.");
        report.AppendLine();
        report.AppendLine("| 리롤 집단 | 단계 | 표본 | 필수산업 순 노동 p10/중앙/p90 | 필수산업 산출 등가 p10/중앙/p90 | 최고 제작 WU p10/중앙/p90 | 최고 연구 WU p10/중앙/p90 | 음식수요 지수 p10/중앙/p90 |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");
        foreach (V26FounderIndustryBalanceDebugScenarios.FounderIndustryDistribution distribution
                 in founderDistributions)
        {
            Require(distribution.AssignmentP10 <= distribution.AssignmentMedian
                && distribution.AssignmentMedian <= distribution.AssignmentP90,
                $"Founder distribution '{distribution.RegimeId}' is not ordered.");
            foreach (StageSnapshot stage in stages)
            {
                float physicalScale = stage.Routine.ActualLaborWu;
                float processScale = physicalScale * stage.Checkpoint.ProcessConversion;
                float partyAutomation = stage.Checkpoint.AutomationWu * 3f;
                report.Append("| ").Append(RegimeName(distribution.RegimeId))
                    .Append(" | ").Append(StageName(stage.Stage))
                    .Append(" | ").Append(distribution.SampleCount)
                    .Append(" | ").Append(Range(
                        distribution.AssignmentP10 * physicalScale,
                        distribution.AssignmentMedian * physicalScale,
                        distribution.AssignmentP90 * physicalScale))
                    .Append(" | ").Append(Range(
                        distribution.AssignmentP10 * processScale + partyAutomation,
                        distribution.AssignmentMedian * processScale + partyAutomation,
                        distribution.AssignmentP90 * processScale + partyAutomation))
                    .Append(" | ").Append(Range(
                        distribution.CraftP10 * physicalScale,
                        distribution.CraftMedian * physicalScale,
                        distribution.CraftP90 * physicalScale))
                    .Append(" | ").Append(Range(
                        distribution.ResearchP10 * physicalScale,
                        distribution.ResearchMedian * physicalScale,
                        distribution.ResearchP90 * physicalScale))
                    .Append(" | ").Append(Range(
                        distribution.ConsumptionP10,
                        distribution.ConsumptionMedian,
                        distribution.ConsumptionP90))
                    .AppendLine(" |");
            }
        }

        V26FounderIndustryBalanceDebugScenarios.FounderIndustryDistribution natural =
            founderDistributions.Single(value => value.RegimeId == "natural");
        V26FounderIndustryBalanceDebugScenarios.FounderIndustryDistribution compromise =
            founderDistributions.Single(value => value.RegimeId == "compromise-3");
        V26FounderIndustryBalanceDebugScenarios.FounderIndustryDistribution upper =
            founderDistributions.Single(value => value.RegimeId == "upper-20");
        Require(compromise.AssignmentMedian > natural.AssignmentMedian,
            "Compromise reroll no longer improves median industry assignment.");
        Require(upper.AssignmentMedian > compromise.AssignmentMedian,
            "Upper reroll no longer improves median industry assignment.");

        report.AppendLine();
        report.AppendLine("## 인구 증가와 단일 프로젝트 상한");
        report.AppendLine();
        report.AppendLine("전체 산출은 인구와 인당 효율이 함께 증가하므로 크게 늘지만, 아래 프로젝트 상한 때문에 한 연구나 랜드마크에 전부 집중할 수 없다. 연구 상한은 4명 곡선의 2.40명분, 랜드마크 상한은 8명 곡선의 5.00명분이다.");
        report.AppendLine();
        report.AppendLine("| 단계 | 중앙 인구 | 정착지 산출 등가 WU/일 | Day 1 대비 | 대형연구 최대 WU/일 | 랜드마크 최대 등가 WU/일 |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|");
        float researchEffectiveWorkers = SumContribution(ProjectScale.MajorResearch);
        float landmarkEffectiveWorkers = SumContribution(ProjectScale.Landmark);
        Require(Mathf.Approximately(researchEffectiveWorkers, 2.4f),
            "Major research contribution cap changed.");
        Require(Mathf.Approximately(landmarkEffectiveWorkers, 5f),
            "Landmark contribution cap changed.");
        foreach (StageSnapshot stage in stages)
        {
            report.Append("| ").Append(StageName(stage.Stage))
                .Append(" | ").Append(stage.Checkpoint.MedianPopulation)
                .Append(" | ").Append(F(stage.SettlementOutputEquivalentWu))
                .Append(" | ×").Append(F(stage.TotalGrowthFromDayOne))
                .Append(" | ").Append(F(
                    stage.Routine.ActualLaborWu * researchEffectiveWorkers))
                .Append(" | ").Append(F(
                    stage.Routine.ActualLaborWu
                    * stage.Checkpoint.ProcessConversion
                    * landmarkEffectiveWorkers))
                .AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("## 해석 경계");
        report.AppendLine();
        report.AppendLine("- ‘순 노동 WU’는 욕구·시설 이용·기준 동선·1% 전환 손실을 차감한 개인 작업량이다.");
        report.AppendLine("- ‘산출 등가 WU’는 공정·시설의 물리 산출 환산과 도메인 자동화를 더한 값이며, 연구나 랜드마크에 자유 전용되는 노동력이 아니다.");
        report.AppendLine("- 식량 생산·조리·운반·부패, 시설 정비, 사고 치료와 비상 예비는 실제 정착지 배치에 따라 달라진다. 해당 라이브 회계 없이 보장 성장 WU를 이 표에서 만들어내지 않는다.");
        report.AppendLine("- 기술 단계는 숨은 전역 버프가 아니다. 이 표는 시설·식사·수면·상하수도·물류·공정 기술이 실제 소비처에 연결됐을 때 지켜야 할 감사 목표다.");

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        string result = $"PHASE157_TECH_FOUNDER_WU=PASS; stages={stages.Count}; "
            + $"regimes={founderDistributions.Count}; naturalSamples={natural.SampleCount}; "
            + $"naturalDay1={Range(natural.AssignmentP10 * SettlementLaborBalanceRules.BaselineWuPerAdultDay, natural.AssignmentMedian * SettlementLaborBalanceRules.BaselineWuPerAdultDay, natural.AssignmentP90 * SettlementLaborBalanceRules.BaselineWuPerAdultDay)}; "
            + $"endlessIndex={stages[^1].NeutralOutputEquivalentWu / SettlementLaborBalanceRules.BaselineWuPerAdultDay:0.000}";
        Debug.Log(result);
        return result;
    }

    private static float SumContribution(ProjectScale scale)
    {
        float result = 0f;
        int maximum = SettlementLaborBalanceRules.GetMaximumWorkers(scale);
        for (int index = 0; index < maximum; index++)
        {
            result += SettlementLaborBalanceRules.GetWorkerContribution(scale, index);
        }
        return result;
    }

    private static string StageName(SettlementTechnologyStage stage) => stage switch
    {
        SettlementTechnologyStage.NoResearch => "무연구",
        SettlementTechnologyStage.Early => "초기",
        SettlementTechnologyStage.Middle => "중기",
        SettlementTechnologyStage.Industrial => "산업",
        SettlementTechnologyStage.Late => "후기",
        SettlementTechnologyStage.Endless => "엔드리스",
        _ => stage.ToString()
    };

    private static string RegimeName(string regimeId) => regimeId switch
    {
        "natural" => "무리롤 자연분포",
        "compromise-3" => "현실적 타협(3파티 중 선택)",
        "upper-20" => "상위 리롤(20파티 중 선택)",
        _ => regimeId ?? string.Empty
    };

    private static string Range(float p10, float median, float p90) =>
        $"{F(p10)}/{F(median)}/{F(p90)}";

    private static string F(float value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Playtime(float days)
    {
        float minutes = days * SettlementLaborBalanceRules.SecondsPerDay / 60f;
        return minutes < 60f
            ? $"{minutes:0.0}m"
            : $"{minutes / 60f:0.00}h";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly struct StageSnapshot
    {
        public StageSnapshot(
            SettlementTechnologyStage stage,
            TechnologyWuCheckpoint checkpoint,
            TechnologyDailyRoutineSnapshot routine,
            float neutralOutputEquivalentWu,
            float settlementOutputEquivalentWu,
            float totalGrowthFromDayOne)
        {
            Stage = stage;
            Checkpoint = checkpoint;
            Routine = routine;
            NeutralOutputEquivalentWu = neutralOutputEquivalentWu;
            SettlementOutputEquivalentWu = settlementOutputEquivalentWu;
            TotalGrowthFromDayOne = totalGrowthFromDayOne;
        }

        public SettlementTechnologyStage Stage { get; }
        public TechnologyWuCheckpoint Checkpoint { get; }
        public TechnologyDailyRoutineSnapshot Routine { get; }
        public float NeutralOutputEquivalentWu { get; }
        public float SettlementOutputEquivalentWu { get; }
        public float TotalGrowthFromDayOne { get; }
    }
}
#endif
