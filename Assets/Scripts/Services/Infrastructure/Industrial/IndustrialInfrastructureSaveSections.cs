using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PowerInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "infrastructure.power";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    private readonly IPowerInfrastructurePersistence persistence;

    public PowerInfrastructureSaveSection(
        IPowerInfrastructurePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonPowerInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "power infrastructure",
                report))
        {
            return;
        }

        IndustrialInfrastructureSaveValidation.Validate(
            JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(payloadJson),
            report);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "power infrastructure");
        ElectricalNetworkRestoreCandidate candidate =
            persistence.PrepareRestore(
                JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(
                    payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

public sealed class FluidInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "infrastructure.fluids";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IFluidInfrastructurePersistence persistence;

    public FluidInfrastructureSaveSection(
        IFluidInfrastructurePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonFluidInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "fluid infrastructure",
                report))
        {
            return;
        }

        IndustrialInfrastructureSaveValidation.Validate(
            JsonUtility.FromJson<DungeonFluidInfrastructureSaveData>(payloadJson),
            report);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "fluid infrastructure");
        FluidNetworkRestoreCandidate candidate =
            persistence.PrepareRestore(
                JsonUtility.FromJson<DungeonFluidInfrastructureSaveData>(
                    payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

public sealed class ConveyorInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "infrastructure.conveyor";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IConveyorInfrastructurePersistence persistence;

    public ConveyorInfrastructureSaveSection(
        IConveyorInfrastructurePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonConveyorInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "conveyor infrastructure",
                report))
        {
            return;
        }

        IndustrialInfrastructureSaveValidation.Validate(
            JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(
                payloadJson),
            report);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "conveyor infrastructure");
        ConveyorRestoreState candidate = persistence.PrepareRestore(
            JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(
                payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

public sealed class AutomationInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.automation";
    private static readonly string[] Dependencies =
    {
        ProductionBillsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IAutomationInfrastructurePersistence persistence;

    public AutomationInfrastructureSaveSection(
        IAutomationInfrastructurePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonAutomationSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "automation infrastructure",
                report))
        {
            return;
        }

        IndustrialInfrastructureSaveValidation.Validate(
            JsonUtility.FromJson<DungeonAutomationSaveData>(payloadJson),
            report);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "automation infrastructure");
        AutomationRestoreCandidate candidate = persistence.PrepareRestore(
            JsonUtility.FromJson<DungeonAutomationSaveData>(payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

internal static class IndustrialSaveSectionParsing
{
    public static bool ValidateHeader(
        string payloadJson,
        int sectionVersion,
        int expectedVersion,
        string label,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != expectedVersion)
        {
            report.AddError(
                $"Unsupported {label} section version {sectionVersion}; "
                + $"expected {expectedVersion}.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            report.AddError($"Required {label} payload is missing.");
            return false;
        }

        return true;
    }

    public static void RequireHeader(
        string payloadJson,
        int sectionVersion,
        int expectedVersion,
        string label)
    {
        if (sectionVersion != expectedVersion
            || string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"Required {label} payload must use exact version "
                + $"{expectedVersion}.");
        }
    }
}
