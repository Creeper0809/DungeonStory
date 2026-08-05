using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonEconomySettings
{
    [Min(0)] public int baseStaffWage = 35;
    [Min(0)] public int workingStaffBonus = 10;
    [Min(0)] public int emergencyFundingAmount = 1000;
    [Min(0)] public int emergencyFundingDebt = 1200;
    [Min(0f)] public float unpaidWageMoodPenaltyPerDay = 4f;
    [Min(0f)] public float unpaidWageMoodDurationSeconds = 180f;
    [Min(1)] public int breakdownAfterShortfallDays = 2;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct OperatingCostForecast
{
    public OperatingCostForecast(
        int availableMoney,
        int maintenanceCost,
        int payrollCost,
        int outstandingDebt)
    {
        AvailableMoney = Mathf.Max(0, availableMoney);
        MaintenanceCost = Mathf.Max(0, maintenanceCost);
        PayrollCost = Mathf.Max(0, payrollCost);
        OutstandingDebt = Mathf.Max(0, outstandingDebt);
        TotalDue = MaintenanceCost + PayrollCost + OutstandingDebt;
        ExpectedShortfall = Mathf.Max(0, TotalDue - AvailableMoney);
    }

    public int AvailableMoney { get; }
    public int MaintenanceCost { get; }
    public int PayrollCost { get; }
    public int OutstandingDebt { get; }
    public int TotalDue { get; }
    public int ExpectedShortfall { get; }
    public bool CanPayInFull => ExpectedShortfall == 0;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct OperatingCostSettlement
{
    public OperatingCostSettlement(
        OperatingCostForecast forecast,
        int paidAmount,
        int closingBalance,
        int carriedDebt,
        int consecutiveShortfallDays)
    {
        Forecast = forecast;
        PaidAmount = Mathf.Clamp(paidAmount, 0, forecast.TotalDue);
        ClosingBalance = Mathf.Max(0, closingBalance);
        CarriedDebt = Mathf.Max(0, carriedDebt);
        ConsecutiveShortfallDays = Mathf.Max(0, consecutiveShortfallDays);
    }

    public OperatingCostForecast Forecast { get; }
    public int PaidAmount { get; }
    public int ClosingBalance { get; }
    public int CarriedDebt { get; }
    public int ConsecutiveShortfallDays { get; }
    public int UnpaidAmount => Mathf.Max(0, Forecast.TotalDue - PaidAmount);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class DungeonEconomyCalculator
{
    public static int CalculatePayroll(
        int staffCount,
        int workingCount,
        DungeonEconomySettings settings)
    {
        settings ??= new DungeonEconomySettings();
        int safeStaff = Mathf.Max(0, staffCount);
        int safeWorking = Mathf.Clamp(workingCount, 0, safeStaff);
        return (safeStaff * Mathf.Max(0, settings.baseStaffWage))
            + (safeWorking * Mathf.Max(0, settings.workingStaffBonus));
    }

    public static OperatingCostSettlement Settle(
        OperatingCostForecast forecast,
        int previousConsecutiveShortfallDays)
    {
        int paid = Mathf.Min(forecast.AvailableMoney, forecast.TotalDue);
        int debt = Mathf.Max(0, forecast.TotalDue - paid);
        int consecutive = debt > 0
            ? Mathf.Max(0, previousConsecutiveShortfallDays) + 1
            : 0;
        return new OperatingCostSettlement(
            forecast,
            paid,
            forecast.AvailableMoney - paid,
            debt,
            consecutive);
    }
}
