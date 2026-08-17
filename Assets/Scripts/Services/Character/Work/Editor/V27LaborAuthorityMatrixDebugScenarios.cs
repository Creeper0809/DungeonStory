#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V27LaborAuthorityMatrixDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-labor-authority-matrix.txt";

    private static readonly int[] Populations = { 1, 3, 6, 12, 24 };
    private static readonly SurvivalProfile[] SurvivalProfiles =
    {
        new SurvivalProfile("shortage", 0.90f, 2),
        new SurvivalProfile("normal", 0.50f, 7),
        new SurvivalProfile("surplus", 0.30f, 14)
    };
    private static readonly EmergencyProfile[] EmergencyProfiles =
    {
        new EmergencyProfile("none", EmergencyKind.None),
        new EmergencyProfile("medical", EmergencyKind.Medical),
        new EmergencyProfile("invasion", EmergencyKind.Invasion),
        new EmergencyProfile("guard", EmergencyKind.Guard)
    };

    [MenuItem("DungeonStory/V27/Verify Population Technology Survival Emergency Matrix")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        Debug.Log(report.Split('\n')[0]);
    }

    public static string RunAll()
    {
        IReadOnlyList<TechnologyWuCheckpoint> checkpoints =
            SettlementLaborBalanceRules.TechnologyCheckpoints;
        Require(checkpoints.Count == 6, "The matrix requires all six technology stages.");

        List<MatrixCell> cells = new List<MatrixCell>(360);
        foreach (int population in Populations)
        {
            for (int stageIndex = 0; stageIndex < checkpoints.Count; stageIndex++)
            {
                TechnologyWuCheckpoint checkpoint = checkpoints[stageIndex];
                SettlementTechnologyStage stage = (SettlementTechnologyStage)stageIndex;
                foreach (SurvivalProfile survival in SurvivalProfiles)
                {
                    foreach (EmergencyProfile emergency in EmergencyProfiles)
                    {
                        EmergencyCounts counts = ResolveEmergencyCounts(
                            population,
                            emergency.Kind);
                        int crisisDays = emergency.Kind == EmergencyKind.None ? 0 : 3;
                        float essentialWu = population
                            * SettlementLaborAuthority.EffectiveOutputWuPerAdultDay
                            * survival.EssentialRatio;
                        DisasterShadowScenarioInput input = new DisasterShadowScenarioInput(
                            population,
                            counts.UnavailableAdults,
                            counts.ResponderAdults,
                            checkpoint.OutputEquivalentWu,
                            essentialWu,
                            survival.SupplyDays,
                            survival.SupplyDays,
                            crisisDays,
                            counts.RecoveredAdultsByDaySeven);
                        DisasterShadowSimulationSnapshot snapshot =
                            SettlementLaborBalanceRules.EvaluateDisasterShadow(in input);
                        cells.Add(new MatrixCell(
                            population,
                            stage,
                            stageIndex,
                            checkpoint,
                            survival,
                            emergency,
                            counts,
                            essentialWu,
                            crisisDays,
                            snapshot));
                    }
                }
            }
        }

        ValidateMatrix(cells);
        string report = BuildReport(cells);
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        return report;
    }

    private static void ValidateMatrix(IReadOnlyList<MatrixCell> cells)
    {
        Require(cells.Count == 360, $"Expected 360 matrix cells, got {cells.Count}.");
        Require(cells.All(cell =>
                cell.Counts.UnavailableAdults >= 0
                && cell.Counts.ResponderAdults >= 0
                && cell.Counts.UnavailableAdults + cell.Counts.ResponderAdults
                    <= cell.Population
                && cell.Snapshot.AvailableAdults >= 0
                && cell.Snapshot.AvailableAdults <= cell.Population
                && IsFiniteNonNegative(cell.Snapshot.AvailableWuPerDay)
                && IsFiniteNonNegative(cell.Snapshot.EssentialCoverage)
                && IsFiniteNonNegative(cell.Snapshot.GrowthWuPerDay)
                && IsFiniteNonNegative(cell.Snapshot.EssentialDeficitWuPerDay)),
            "A matrix cell produced an invalid count or non-finite labor result.");

        foreach (MatrixCell cell in cells)
        {
            int expectedAvailable = cell.Population
                - cell.Counts.UnavailableAdults
                - cell.Counts.ResponderAdults;
            Require(cell.Snapshot.AvailableAdults == expectedAvailable,
                $"Available adult drift in {cell.Key}.");
            Require(Approximately(
                    cell.Snapshot.AvailableWuPerDay,
                    expectedAvailable * cell.Checkpoint.OutputEquivalentWu),
                $"Effective output drift in {cell.Key}.");
            Require(Approximately(
                    cell.Checkpoint.OutputEquivalentWu,
                    cell.Checkpoint.ActualLaborWu
                        * SettlementLaborAuthority.EffectiveToActualRatio),
                $"Actual/effective ratio drift at {cell.Stage}.");
            Require(cell.Snapshot.EssentialDeficitWuPerDay <= 0.0001f
                    || cell.Snapshot.GrowthWuPerDay <= 0.0001f,
                $"Growth was retained ahead of essentials in {cell.Key}.");
            if (cell.Emergency.Kind != EmergencyKind.None
                && cell.Survival.Id == "shortage")
            {
                Require(!cell.Snapshot.SurvivesCrisisWindow,
                    $"A three-day crisis ignored the two-day shortage reserve in {cell.Key}.");
            }
        }

        foreach (IGrouping<string, MatrixCell> group in cells.GroupBy(cell =>
                     $"{cell.Population}|{cell.Survival.Id}|{cell.Emergency.Id}"))
        {
            MatrixCell[] ordered = group.OrderBy(cell => cell.StageIndex).ToArray();
            for (int index = 1; index < ordered.Length; index++)
            {
                Require(ordered[index].Checkpoint.ActualLaborWu
                        >= ordered[index - 1].Checkpoint.ActualLaborWu
                    && ordered[index].Checkpoint.OutputEquivalentWu
                        >= ordered[index - 1].Checkpoint.OutputEquivalentWu
                    && ordered[index].Snapshot.EssentialCoverage + 0.0001f
                        >= ordered[index - 1].Snapshot.EssentialCoverage
                    && ordered[index].Snapshot.GrowthWuPerDay + 0.0001f
                        >= ordered[index - 1].Snapshot.GrowthWuPerDay,
                    $"Technology progression regressed labor in {group.Key}.");
            }
        }

        foreach (IGrouping<string, MatrixCell> group in cells.GroupBy(cell =>
                     $"{cell.Population}|{cell.StageIndex}|{cell.Emergency.Id}"))
        {
            MatrixCell shortage = group.Single(cell => cell.Survival.Id == "shortage");
            MatrixCell normal = group.Single(cell => cell.Survival.Id == "normal");
            MatrixCell surplus = group.Single(cell => cell.Survival.Id == "surplus");
            Require(shortage.EssentialWuPerDay > normal.EssentialWuPerDay
                    && normal.EssentialWuPerDay > surplus.EssentialWuPerDay
                    && shortage.Snapshot.EssentialCoverage
                        <= normal.Snapshot.EssentialCoverage + 0.0001f
                    && normal.Snapshot.EssentialCoverage
                        <= surplus.Snapshot.EssentialCoverage + 0.0001f
                    && shortage.Snapshot.GrowthWuPerDay
                        <= normal.Snapshot.GrowthWuPerDay + 0.0001f
                    && normal.Snapshot.GrowthWuPerDay
                        <= surplus.Snapshot.GrowthWuPerDay + 0.0001f,
                $"Survival burden ordering regressed in {group.Key}.");
        }

        foreach (IGrouping<string, MatrixCell> group in cells.GroupBy(cell =>
                     $"{cell.Population}|{cell.StageIndex}|{cell.Survival.Id}"))
        {
            MatrixCell none = group.Single(cell => cell.Emergency.Kind == EmergencyKind.None);
            Require(group.All(cell =>
                    cell.Snapshot.AvailableAdults <= none.Snapshot.AvailableAdults
                    && cell.Snapshot.AvailableWuPerDay
                        <= none.Snapshot.AvailableWuPerDay + 0.0001f),
                $"An emergency increased available labor in {group.Key}.");
        }
    }

    private static EmergencyCounts ResolveEmergencyCounts(
        int population,
        EmergencyKind kind)
    {
        int unavailable = 0;
        int responders = 0;
        int recovered = 0;
        switch (kind)
        {
            case EmergencyKind.None:
                break;
            case EmergencyKind.Medical:
                unavailable = CeilFraction(population, 1, 5);
                responders = population >= 3 ? 1 : 0;
                recovered = unavailable;
                break;
            case EmergencyKind.Invasion:
                unavailable = population >= 3 ? CeilFraction(population, 1, 10) : 0;
                responders = CeilFraction(population, 1, 4);
                recovered = unavailable;
                break;
            case EmergencyKind.Guard:
                responders = CeilFraction(population, 1, 5);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        unavailable = Math.Min(population, unavailable);
        responders = Math.Min(population - unavailable, responders);
        return new EmergencyCounts(unavailable, responders, recovered);
    }

    private static int CeilFraction(int value, int numerator, int denominator) =>
        (value * numerator + denominator - 1) / denominator;

    private static string BuildReport(IReadOnlyList<MatrixCell> cells)
    {
        int surviving = cells.Count(cell => cell.Snapshot.SurvivesCrisisWindow);
        int recovered = cells.Count(cell => cell.Snapshot.RecoversByDaySeven);
        int zeroGrowth = cells.Count(cell => cell.Snapshot.GrowthWuPerDay <= 0.0001f);
        StringBuilder report = new StringBuilder(131072);
        report.Append("RESULT=PASS; cells=360; populations=5; technologyStages=6; survivalProfiles=3; emergencies=4; surviving=")
            .Append(surviving.ToString(CultureInfo.InvariantCulture))
            .Append("; recoveredByDay7=")
            .Append(recovered.ToString(CultureInfo.InvariantCulture))
            .Append("; zeroGrowth=")
            .Append(zeroGrowth.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_LABOR_MATRIX_360_CELLS count=360\n");
        report.Append("PASS V27_LABOR_MATRIX_ACTUAL_EFFECTIVE_RATIO actual=50; effective=45\n");
        report.Append("PASS V27_LABOR_MATRIX_TECH_MONOTONIC stages=6\n");
        report.Append("PASS V27_LABOR_MATRIX_SURVIVAL_MONOTONIC profiles=shortage,normal,surplus\n");
        report.Append("PASS V27_LABOR_MATRIX_GROWTH_CUT_FIRST deficitGrowthCells=0\n");
        report.Append("PASS V27_LABOR_MATRIX_SHORTAGE_CRISIS_EXPOSED emergencyShortageSurvivors=0\n");
        report.Append("population|stage|survival|emergency|actualPerAdult|effectivePerAdult|unavailable|responders|available|essentialWu|coverage|growthWu|deficitWu|foodAfter|waterAfter|survives|recoversDay7\n");
        foreach (MatrixCell cell in cells)
        {
            report.Append(cell.Population.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(cell.Stage).Append('|')
                .Append(cell.Survival.Id).Append('|')
                .Append(cell.Emergency.Id).Append('|')
                .Append(F(cell.Checkpoint.ActualLaborWu)).Append('|')
                .Append(F(cell.Checkpoint.OutputEquivalentWu)).Append('|')
                .Append(cell.Counts.UnavailableAdults.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(cell.Counts.ResponderAdults.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(cell.Snapshot.AvailableAdults.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(F(cell.EssentialWuPerDay)).Append('|')
                .Append(F(cell.Snapshot.EssentialCoverage)).Append('|')
                .Append(F(cell.Snapshot.GrowthWuPerDay)).Append('|')
                .Append(F(cell.Snapshot.EssentialDeficitWuPerDay)).Append('|')
                .Append(cell.Snapshot.FoodDaysAfterCrisis.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(cell.Snapshot.WaterDaysAfterCrisis.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(cell.Snapshot.SurvivesCrisisWindow ? "true" : "false").Append('|')
                .Append(cell.Snapshot.RecoversByDaySeven ? "true" : "false")
                .Append('\n');
        }
        return report.ToString();
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.001f;

    private static string F(float value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private enum EmergencyKind
    {
        None,
        Medical,
        Invasion,
        Guard
    }

    private readonly struct SurvivalProfile
    {
        public SurvivalProfile(string id, float essentialRatio, int supplyDays)
        {
            Id = id;
            EssentialRatio = essentialRatio;
            SupplyDays = supplyDays;
        }

        public string Id { get; }
        public float EssentialRatio { get; }
        public int SupplyDays { get; }
    }

    private readonly struct EmergencyProfile
    {
        public EmergencyProfile(string id, EmergencyKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public string Id { get; }
        public EmergencyKind Kind { get; }
    }

    private readonly struct EmergencyCounts
    {
        public EmergencyCounts(
            int unavailableAdults,
            int responderAdults,
            int recoveredAdultsByDaySeven)
        {
            UnavailableAdults = unavailableAdults;
            ResponderAdults = responderAdults;
            RecoveredAdultsByDaySeven = recoveredAdultsByDaySeven;
        }

        public int UnavailableAdults { get; }
        public int ResponderAdults { get; }
        public int RecoveredAdultsByDaySeven { get; }
    }

    private readonly struct MatrixCell
    {
        public MatrixCell(
            int population,
            SettlementTechnologyStage stage,
            int stageIndex,
            TechnologyWuCheckpoint checkpoint,
            SurvivalProfile survival,
            EmergencyProfile emergency,
            EmergencyCounts counts,
            float essentialWuPerDay,
            int crisisDays,
            DisasterShadowSimulationSnapshot snapshot)
        {
            Population = population;
            Stage = stage;
            StageIndex = stageIndex;
            Checkpoint = checkpoint;
            Survival = survival;
            Emergency = emergency;
            Counts = counts;
            EssentialWuPerDay = essentialWuPerDay;
            CrisisDays = crisisDays;
            Snapshot = snapshot;
        }

        public int Population { get; }
        public SettlementTechnologyStage Stage { get; }
        public int StageIndex { get; }
        public TechnologyWuCheckpoint Checkpoint { get; }
        public SurvivalProfile Survival { get; }
        public EmergencyProfile Emergency { get; }
        public EmergencyCounts Counts { get; }
        public float EssentialWuPerDay { get; }
        public int CrisisDays { get; }
        public DisasterShadowSimulationSnapshot Snapshot { get; }
        public string Key => $"population={Population};stage={Stage};survival={Survival.Id};emergency={Emergency.Id}";
    }
}
#endif
