using System;

public static class Wim016CropWaterRulesDebugScenarios
{
    private const float Epsilon = 0.00001f;

    public static bool Run(out string report)
    {
        ValidCase[] validCases =
        {
            new ValidCase("zero_demand_empty_water", 0f, 0f, 1f),
            new ValidCase("zero_demand_available_water", 1f, 0f, 1f),
            new ValidCase("empty_water_with_demand", 0f, 1f, 0f),
            new ValidCase("quarter_water", 0.25f, 1f, 0.25f),
            new ValidCase("full_water", 1f, 1f, 1f),
            new ValidCase("water_above_demand", 2f, 1f, 1f)
        };
        InvalidCase[] invalidCases =
        {
            new InvalidCase("negative_water", -1f, 1f),
            new InvalidCase("negative_demand", 1f, -1f),
            new InvalidCase("nan_water", float.NaN, 1f),
            new InvalidCase("nan_demand", 1f, float.NaN),
            new InvalidCase("infinite_water", float.PositiveInfinity, 1f),
            new InvalidCase("infinite_demand", 1f, float.PositiveInfinity)
        };

        int total = validCases.Length + invalidCases.Length;
        int passed = 0;
        foreach (ValidCase testCase in validCases)
        {
            float actual;
            try
            {
                actual = CropWaterRules.ResolveGrowthMultiplier(
                    testCase.CurrentWater,
                    testCase.DailyDemand);
            }
            catch (Exception exception)
            {
                report = Fail(
                    testCase.Name,
                    "finite multiplier",
                    exception.GetType().Name,
                    passed,
                    total);
                return false;
            }

            if (!float.IsFinite(actual)
                || Math.Abs(actual - testCase.Expected) > Epsilon)
            {
                report = Fail(
                    testCase.Name,
                    testCase.Expected.ToString("0.#####"),
                    actual.ToString("0.#####"),
                    passed,
                    total);
                return false;
            }

            passed++;
        }

        foreach (InvalidCase testCase in invalidCases)
        {
            try
            {
                _ = CropWaterRules.ResolveGrowthMultiplier(
                    testCase.CurrentWater,
                    testCase.DailyDemand);
            }
            catch (ArgumentOutOfRangeException)
            {
                passed++;
                continue;
            }
            catch (Exception exception)
            {
                report = Fail(
                    testCase.Name,
                    nameof(ArgumentOutOfRangeException),
                    exception.GetType().Name,
                    passed,
                    total);
                return false;
            }

            report = Fail(
                testCase.Name,
                nameof(ArgumentOutOfRangeException),
                "no exception",
                passed,
                total);
            return false;
        }

        report = $"PASS cases={passed};total={total}";
        return true;
    }

    private static string Fail(
        string name,
        string expected,
        string actual,
        int passed,
        int total) =>
        $"FAIL case={name};expected={expected};actual={actual};passed={passed};total={total}";

    private readonly struct ValidCase
    {
        public ValidCase(
            string name,
            float currentWater,
            float dailyDemand,
            float expected)
        {
            Name = name;
            CurrentWater = currentWater;
            DailyDemand = dailyDemand;
            Expected = expected;
        }

        public string Name { get; }
        public float CurrentWater { get; }
        public float DailyDemand { get; }
        public float Expected { get; }
    }

    private readonly struct InvalidCase
    {
        public InvalidCase(string name, float currentWater, float dailyDemand)
        {
            Name = name;
            CurrentWater = currentWater;
            DailyDemand = dailyDemand;
        }

        public string Name { get; }
        public float CurrentWater { get; }
        public float DailyDemand { get; }
    }
}
