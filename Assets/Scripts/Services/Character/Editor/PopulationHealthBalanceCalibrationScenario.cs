#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PopulationHealthBalanceCalibrationScenario
{
    private const int SamplesPerDisease = 100_000;
    private const string ReportPath =
        "Artifacts/QA/population-health-balance.txt";

    public static string RunAll()
    {
        AssetDiseaseCatalog catalog = LoadCatalog();
        DiseaseDefinition[] contagious = catalog.Definitions
            .Where(value => value.Contagious)
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();

        Require(catalog.Definitions.Count == 16,
            $"Expected 16 disease definitions, found {catalog.Definitions.Count}.");
        Require(contagious.Length == 15,
            $"Expected 15 contagious diseases, found {contagious.Length}.");

        VerifyProbabilityBands(contagious);
        VerifyVaccinationCurve(catalog.Require("disease:cave-flu"));
        VerifyDetailedDiseaseStats(catalog);
        VerifyEpidemicLifecycleAndSave(catalog);

        float minimumBase = contagious.Min(value => value.BaseInfectionProbability);
        float maximumBase = contagious.Max(value => value.BaseInfectionProbability);
        float minimumSeverity = contagious.Min(value => value.BaseSeverity);
        float maximumSeverity = contagious.Max(value => value.BaseSeverity);
        string report =
            "POPULATION_HEALTH_BALANCE=PASS\n"
            + $"definitions={catalog.Definitions.Count}\n"
            + $"contagious={contagious.Length}\n"
            + $"samplesPerDisease={SamplesPerDisease}\n"
            + $"fullDayBaseInfectionRange={minimumBase:F3}-{maximumBase:F3}\n"
            + $"severityRange={minimumSeverity:F1}-{maximumSeverity:F1}\n"
            + "mitigatedExposure=8h@0.50_environment\n"
            + "vaccineInitialImmunity=70.0\n"
            + "vaccineDay30Immunity=68.5\n"
            + "detailedStats=disease_resistance,disease_recovery,immunity_gain,immunity_retention\n"
            + "epidemicDeclare=3_diagnoses_within_10_days\n"
            + "epidemicClose=14_days_without_new_case";

        string absolutePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            ReportPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? string.Empty);
        File.WriteAllText(absolutePath, report + Environment.NewLine);
        Debug.Log(report.Replace('\n', ';'));
        return report;
    }

    private static void VerifyProbabilityBands(
        IReadOnlyList<DiseaseDefinition> diseases)
    {
        foreach (DiseaseDefinition disease in diseases)
        {
            double exposedProbability =
                PopulationHealthAggregateState.CalculateInfectionProbability(
                    disease.BaseInfectionProbability,
                    24f,
                    immunity: 0f,
                    susceptibility: 1f,
                    environmentCoefficient: 1f);
            double mitigatedProbability =
                PopulationHealthAggregateState.CalculateInfectionProbability(
                    disease.BaseInfectionProbability,
                    8f,
                    immunity: 0f,
                    susceptibility: 1f,
                    environmentCoefficient: 0.5f);
            double vaccinatedProbability =
                PopulationHealthAggregateState.CalculateInfectionProbability(
                    disease.BaseInfectionProbability,
                    24f,
                    immunity: 70f,
                    susceptibility: 1f,
                    environmentCoefficient: 1f);

            Require(exposedProbability >= 0.07d && exposedProbability <= 0.25d,
                $"{disease.Id} full-day infection probability {exposedProbability:F4} is outside 7-25%.");
            Require(mitigatedProbability <= exposedProbability * 0.17d,
                $"{disease.Id} ventilation/exposure mitigation is too weak.");
            Require(vaccinatedProbability <= exposedProbability * 0.301d,
                $"{disease.Id} initial vaccine protection is too weak.");

            int observedExposed = Simulate(exposedProbability, disease.Id, 0x51A7u);
            int observedMitigated = Simulate(mitigatedProbability, disease.Id, 0xB10Cu);
            AssertObservedRate(disease.Id, "full-day", exposedProbability, observedExposed);
            AssertObservedRate(disease.Id, "mitigated", mitigatedProbability, observedMitigated);
        }
    }

    private static void VerifyVaccinationCurve(DiseaseDefinition disease)
    {
        double initial = PopulationHealthAggregateState.CalculateInfectionProbability(
            disease.BaseInfectionProbability, 24f, 70f, 1f, 1f);
        double day30 = PopulationHealthAggregateState.CalculateInfectionProbability(
            disease.BaseInfectionProbability, 24f, 68.5f, 1f, 1f);
        double untreated = PopulationHealthAggregateState.CalculateInfectionProbability(
            disease.BaseInfectionProbability, 24f, 0f, 1f, 1f);
        Require(initial < day30 && day30 < untreated,
            "Vaccination decay did not produce a gradual, bounded loss of protection.");
        Require(day30 <= untreated * 0.32d,
            "The day-30 vaccine probability exceeded 32% of untreated risk.");
    }

    private static void VerifyEpidemicLifecycleAndSave(
        IDiseaseDefinitionCatalog catalog)
    {
        const string diseaseId = "disease:cave-flu";
        DiseaseDefinition disease = catalog.Require(diseaseId);
        PopulationHealthAggregateState state = new();
        PopulationExposureTarget[] targets =
        {
            new(new CharacterId("character:balance:disease:01"), 1f),
            new(new CharacterId("character:balance:disease:02"), 1f),
            new(new CharacterId("character:balance:disease:03"), 1f)
        };
        state.RecordExposure(diseaseId, targets, 24f, 1f, catalog);
        IReadOnlyList<PopulationHealthChange> infections =
            state.AdvanceToDay(2, catalog, () => 0d);
        Require(infections.Count(value => value.Kind == PopulationHealthChangeKind.Infected) == 3,
            "Forced epidemic fixture did not infect all three targets.");

        int diagnosisDay = 2 + disease.IncubationDays;
        state.AdvanceToDay(diagnosisDay, catalog, () => 1d);
        Require(state.IsEpidemicDeclared(diseaseId),
            "Three diagnoses inside ten days did not declare an epidemic.");

        PopulationHealthWorldSaveData captured = state.Capture();
        PopulationHealthAggregateState restored =
            PopulationHealthAggregateState.Restore(captured, catalog);
        Require(restored.IsEpidemicDeclared(diseaseId),
            "Declared epidemic did not survive save restoration.");
        Require(restored.CurrentAbsoluteDay == diagnosisDay,
            "Population-health absolute day did not survive save restoration.");

        restored.AdvanceToDay(diagnosisDay + 13, catalog, () => 1d);
        Require(restored.IsEpidemicDeclared(diseaseId),
            "Epidemic closed before fourteen clear days elapsed.");
        restored.AdvanceToDay(diagnosisDay + 14, catalog, () => 1d);
        Require(!restored.IsEpidemicDeclared(diseaseId),
            "Epidemic remained declared after fourteen clear days.");
    }

    private static void VerifyDetailedDiseaseStats(
        IDiseaseDefinitionCatalog catalog)
    {
        DiseaseDefinition disease = catalog.Require("disease:cave-flu");
        double neutralRisk = PopulationHealthAggregateState.CalculateInfectionProbability(
            disease.BaseInfectionProbability, 24f, 0f, 1f, 1f);
        double resistantRisk = PopulationHealthAggregateState.CalculateInfectionProbability(
            disease.BaseInfectionProbability, 24f, 0f, 0.5f, 1f);
        Require(Math.Abs(resistantRisk * 2d - neutralRisk) <= 0.000001d,
            "Disease resistance did not divide susceptibility independently.");

        CharacterId fastRecoveryId = new("character:balance:disease:fast-recovery");
        PopulationHealthAggregateState recovery = new();
        recovery.RecordExposure(
            disease.Id,
            new[] { new PopulationExposureTarget(fastRecoveryId, 1f) },
            24f,
            1f,
            catalog);
        recovery.AdvanceToDay(
            2,
            catalog,
            () => 0d,
            (_, _) => new PopulationDiseaseStatModifiers(1f, 2f, 1f, 1f));
        Require(recovery.TryGetCharacterSnapshot(fastRecoveryId, out PopulationCharacterHealthSnapshot active),
            "Fast-recovery fixture did not create a population-health record.");
        ActiveDiseaseSnapshot infection = active.ActiveDiseases.Single();
        int expectedDuration = PopulationHealthAggregateState.ResolveContagiousDurationDays(
            disease.ContagiousDays,
            2f);
        Require(infection.RecoveryDay - infection.SymptomDay == expectedDuration,
            "Disease recovery speed did not determine and persist the contagious duration.");

        CharacterId immunityId = new("character:balance:disease:immunity");
        PopulationHealthAggregateState immunity = new();
        PopulationDiseaseStatModifiers immuneModifiers = new(1f, 1f, 1.2f, 2f);
        immunity.Vaccinate(immunityId, disease.Id, catalog, immuneModifiers);
        Require(Math.Abs(immunity.GetImmunity(immunityId, disease.Id) - 84f) <= 0.0001f,
            "Immunity gain did not scale the vaccine award.");
        immunity.AdvanceToDay(2, catalog, () => 1d, (_, _) => immuneModifiers);
        Require(Math.Abs(immunity.GetImmunity(immunityId, disease.Id) - 83.975f) <= 0.0001f,
            "Immunity retention did not divide daily immunity decay.");

        CharacterId neutralId = new("character:balance:disease:neutral");
        PopulationHealthAggregateState neutral = new();
        neutral.Vaccinate(neutralId, disease.Id, catalog);
        neutral.AdvanceToDay(2, catalog, () => 1d);
        Require(Math.Abs(neutral.GetImmunity(neutralId, disease.Id) - 69.95f) <= 0.0001f,
            "Neutral disease modifiers changed the legacy vaccine curve.");
    }

    private static int Simulate(double probability, string id, uint salt)
    {
        uint state = StableHash(id) ^ salt;
        int infected = 0;
        for (int index = 0; index < SamplesPerDisease; index++)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            double sample = (state >> 8) / 16777216d;
            if (sample < probability) infected++;
        }
        return infected;
    }

    private static void AssertObservedRate(
        string diseaseId,
        string label,
        double expected,
        int observedCount)
    {
        double observed = observedCount / (double)SamplesPerDisease;
        double tolerance = Math.Max(0.0025d, 5d * Math.Sqrt(
            expected * (1d - expected) / SamplesPerDisease));
        Require(Math.Abs(observed - expected) <= tolerance,
            $"{diseaseId} {label} observed {observed:P3}, expected {expected:P3}±{tolerance:P3}.");
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261u;
        foreach (char character in value ?? string.Empty)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        return hash;
    }

    private static AssetDiseaseCatalog LoadCatalog()
    {
        DiseaseDefinition[] definitions = AssetDatabase
            .FindAssets("t:DiseaseDefinitionSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<DiseaseDefinitionSO>)
            .Where(value => value != null)
            .Select(value => value.CreateRuntimeDefinition())
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
        return new AssetDiseaseCatalog(definitions);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class AssetDiseaseCatalog : IDiseaseDefinitionCatalog
    {
        private readonly Dictionary<string, DiseaseDefinition> byId;

        public AssetDiseaseCatalog(IEnumerable<DiseaseDefinition> definitions)
        {
            DiseaseDefinition[] values = (definitions ?? Array.Empty<DiseaseDefinition>())
                .ToArray();
            byId = values.ToDictionary(value => value.Id, StringComparer.Ordinal);
            Definitions = values;
        }

        public IReadOnlyList<DiseaseDefinition> Definitions { get; }

        public DiseaseDefinition Require(string diseaseId) =>
            byId.TryGetValue(diseaseId?.Trim() ?? string.Empty, out DiseaseDefinition value)
                ? value
                : throw new KeyNotFoundException($"Unknown disease '{diseaseId}'.");
    }
}
#endif
