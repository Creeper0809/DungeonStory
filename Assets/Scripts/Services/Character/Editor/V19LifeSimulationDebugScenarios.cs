#if UNITY_EDITOR
using System;
using UnityEngine;

public static class V19LifeSimulationDebugScenarios
{
    private const int Population = 200_000;
    private const int SeverityProgressionYears = 4;

    public static string RunAll()
    {
        VerifyLifeStageBoundaries();
        VerifyDailyAgingRates();
        VerifyFertilityTreatmentPersistenceAndEffects();
        (double meanAfterElder, int medianAfterElder) =
            SimulateUntreatedPopulation();

        Require(
            Math.Abs(meanAfterElder - 32.17d) <= 0.15d,
            $"Untreated mean after elder was {meanAfterElder:F4}, expected 32.17±0.15.");
        Require(
            medianAfterElder == 33,
            $"Untreated median after elder was {medianAfterElder}, expected 33.");

        (int ElderAge, double ExpectedLife)[] species =
        {
            (42, 74.2d),
            (45, 77.2d),
            (130, 162.2d),
            (48, 80.2d),
            (85, 117.2d),
            (38, 70.2d),
            (30, 62.2d),
            (52, 84.2d),
            (90, 122.2d)
        };
        foreach ((int elderAge, double expectedLife) in species)
        {
            double simulatedLife = elderAge + meanAfterElder;
            Require(
                Math.Abs(simulatedLife - expectedLife) <= 1d,
                $"Species expectancy {simulatedLife:F2} was outside {expectedLife:F1}±1.");
        }

        string report =
            $"V19_LIFE_SIMULATION=PASS; population={Population};"
            + $"meanAfterElder={meanAfterElder:F2};medianAfterElder={medianAfterElder};"
            + "species=9;dailyRates=4/6";
        Debug.Log(report);
        return report;
    }

    private static void VerifyLifeStageBoundaries()
    {
        SpeciesLifeHistoryDefinition orc = OrcLifeHistory();
        Require(
            orc.ResolveStage(orc.AdultAgeDayUnits - 1d)
                == CharacterLifeStage.Adolescent,
            "The day before adulthood was not adolescent.");
        Require(
            orc.ResolveStage(orc.AdultAgeDayUnits)
                == CharacterLifeStage.Adult,
            "The adulthood boundary was not adult.");
        Require(
            orc.ResolveStage(orc.ElderAgeDayUnits - 1d)
                == CharacterLifeStage.Adult,
            "The day before elderhood was not adult.");
        Require(
            orc.ResolveStage(orc.ElderAgeDayUnits)
                == CharacterLifeStage.Elder,
            "The elderhood boundary was not elder.");
    }

    private static void VerifyDailyAgingRates()
    {
        SpeciesLifeHistoryDefinition orc = OrcLifeHistory();
        CharacterLifeRecord minor = new(
            new CharacterId("character:v19-life-minor"),
            orc.SpeciesId,
            0,
            orc.AdultAgeDayUnits - 4d,
            1,
            orc);
        minor.AdvanceOneChronologicalDay(
            orc,
            Array.Empty<AgeConditionDefinition>(),
            () => 0.99d);
        Require(
            minor.BiologicalAgeDayUnits == orc.AdultAgeDayUnits,
            "A minor did not age exactly four biological day units.");
        minor.AdvanceOneChronologicalDay(
            orc,
            Array.Empty<AgeConditionDefinition>(),
            () => 0.99d);
        Require(
            minor.BiologicalAgeDayUnits == orc.AdultAgeDayUnits + 6d,
            "An adult did not age exactly six biological day units.");
    }

    private static void VerifyFertilityTreatmentPersistenceAndEffects()
    {
        ReproductionDefinition definition = new(
            new CharacterSpeciesId("Orc"),
            ReproductionMode.Pregnancy,
            0.5f,
            10f,
            32f,
            new[]
            {
                new ReproductionPhaseDefinition
                {
                    phase = ReproductionPhaseKind.Attempt,
                    durationDays = 1
                },
                new ReproductionPhaseDefinition
                {
                    phase = ReproductionPhaseKind.Pregnancy,
                    durationDays = 2
                }
            });
        ReproductionProcess source = new(
            "reproduction:qa:fertility-treatment",
            new CharacterId("character:qa:first-parent"),
            new CharacterId("character:qa:second-parent"),
            new CharacterId("character:qa:first-parent"),
            definition.SpeciesId,
            definition,
            1,
            crossLineageIncubatorUsed: false,
            supportFacilityInstanceId: "facility:qa:maternity",
            expressedTraitIds: Array.Empty<string>(),
            latentTraitIds: Array.Empty<string>(),
            aptitudes: Array.Empty<InnateAptitudeSaveData>(),
            startActive: false,
            fertilityTreatmentUsed: false);
        source.SelectFertilityTreatment(useTreatment: true);
        ReproductionProcess restored = ReproductionProcess.Restore(
            source.Capture(),
            definition);
        Require(restored.FertilityTreatmentUsed,
            "Fertility-treatment usage did not round-trip with the reproduction process.");
        Require(Math.Abs(
                ReproductionRules.ApplyFertilityTreatmentToCoefficient(0.5f, true)
                - 0.6f) < 0.0001f,
            "Fertility treatment did not increase conception coefficient by 20%.");
        Require(Math.Abs(
                ReproductionRules.ApplyFertilityTreatmentToGestationStability(1f, true)
                - 1.15f) < 0.0001f,
            "Fertility treatment did not increase gestation stability by 15%.");
    }

    private static (double Mean, int Median) SimulateUntreatedPopulation()
    {
        System.Random random = new(19019);
        int[] yearsAfterElder = new int[Population];
        double total = 0d;
        for (int subject = 0; subject < Population; subject++)
        {
            for (int offset = 0; ; offset++)
            {
                double probability =
                    CharacterLifeRecord.CalculateAgeConditionProbability(
                        offset,
                        0);
                if (random.NextDouble() >= probability)
                {
                    continue;
                }

                int years = offset + SeverityProgressionYears;
                yearsAfterElder[subject] = years;
                total += years;
                break;
            }
        }

        Array.Sort(yearsAfterElder);
        return (total / Population, yearsAfterElder[Population / 2]);
    }

    private static SpeciesLifeHistoryDefinition OrcLifeHistory() => new(
        new CharacterSpeciesId("Orc"),
        2,
        11,
        14,
        45,
        77.2f,
        false);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
