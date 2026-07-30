using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PowerInfrastructureSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "infrastructure.power";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    private readonly IElectricalNetworkRuntime runtime;

    public PowerInfrastructureSaveSection(IElectricalNetworkRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonPowerInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported power infrastructure section version "
                + $"{sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(string.IsNullOrWhiteSpace(payloadJson)
            ? new DungeonPowerInfrastructureSaveData()
            : JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(
                payloadJson) ?? new DungeonPowerInfrastructureSaveData());
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Restore(new DungeonPowerInfrastructureSaveData());
        report?.AddWarning(
            "Power infrastructure data was absent and initialized empty.");
    }
}

public sealed class FluidInfrastructureSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "infrastructure.fluids";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IWaterNetworkRuntime runtime;

    public FluidInfrastructureSaveSection(IWaterNetworkRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonFluidInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported fluid infrastructure section version "
                + $"{sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(string.IsNullOrWhiteSpace(payloadJson)
            ? new DungeonFluidInfrastructureSaveData()
            : JsonUtility.FromJson<DungeonFluidInfrastructureSaveData>(
                payloadJson) ?? new DungeonFluidInfrastructureSaveData());
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Restore(new DungeonFluidInfrastructureSaveData());
        report?.AddWarning(
            "Fluid infrastructure data was absent and initialized empty.");
    }
}

public sealed class ConveyorInfrastructureSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "infrastructure.conveyor";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IConveyorRuntime runtime;

    public ConveyorInfrastructureSaveSection(IConveyorRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonConveyorInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported conveyor infrastructure section version "
                + $"{sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(string.IsNullOrWhiteSpace(payloadJson)
            ? new DungeonConveyorInfrastructureSaveData()
            : JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(
                payloadJson) ?? new DungeonConveyorInfrastructureSaveData());
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Restore(new DungeonConveyorInfrastructureSaveData());
        report?.AddWarning(
            "Conveyor infrastructure data was absent and initialized empty.");
    }
}

public sealed class AutomationInfrastructureSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "economy.automation";
    private static readonly string[] Dependencies =
    {
        ProductionBillsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IAutomationRuntime runtime;

    public AutomationInfrastructureSaveSection(IAutomationRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonAutomationSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported automation section version {sectionVersion}; "
                + $"expected {SectionVersion}.");
            return;
        }

        runtime.Restore(string.IsNullOrWhiteSpace(payloadJson)
            ? new DungeonAutomationSaveData()
            : JsonUtility.FromJson<DungeonAutomationSaveData>(
                payloadJson) ?? new DungeonAutomationSaveData());
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Restore(new DungeonAutomationSaveData());
        report?.AddWarning(
            "Automation data was absent and initialized in manual mode.");
    }
}
