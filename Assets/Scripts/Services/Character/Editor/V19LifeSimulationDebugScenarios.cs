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
