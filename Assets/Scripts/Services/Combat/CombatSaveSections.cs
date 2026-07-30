using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CombatEquipmentSaveSection : IDungeonSaveSection
{
    public const string Id = "combat.equipment";
    private readonly ICombatEquipmentRuntime runtime;

    public CombatEquipmentSaveSection(ICombatEquipmentRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 3;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => new[] { PhysicalItemsSaveSection.Id };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        runtime.Restore(JsonUtility.FromJson<DungeonCombatEquipmentSaveData>(
            payloadJson ?? string.Empty) ?? new DungeonCombatEquipmentSaveData());
    }

    private void ValidateVersion(int version)
    {
        if (version < 1 || version > SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {version}.");
        }
    }
}

public sealed class EquipmentEvolutionSaveSection : IDungeonSaveSection
{
    public const string Id = "combat.equipment-evolution";
    private readonly IEquipmentEvolutionRuntime runtime;

    public EquipmentEvolutionSaveSection(IEquipmentEvolutionRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 3;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        CombatEquipmentSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion < 1 || sectionVersion > SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {sectionVersion}.");
        }

        runtime.Restore(JsonUtility.FromJson<EquipmentEvolutionSaveData>(
            payloadJson ?? string.Empty) ?? new EquipmentEvolutionSaveData());
    }
}

public sealed class CharacterBodyHealthSaveSection : IDungeonSaveSection
{
    public const string Id = "combat.body-health";
    private readonly ICharacterBodyHealthRuntime runtime;

    public CharacterBodyHealthSaveSection(ICharacterBodyHealthRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 2;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => new[] { CharacterWorldSaveSection.Id };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        runtime.Restore(JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(
            payloadJson ?? string.Empty) ?? new DungeonCharacterBodyHealthSaveData());
    }

    private void ValidateVersion(int version)
    {
        if (version < 1 || version > SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {version}.");
        }
    }
}

public sealed class CharacterMedicalSaveSection : IDungeonSaveSection
{
    public const string Id = "combat.medical";
    private readonly ICharacterMedicalRuntime runtime;

    public CharacterMedicalSaveSection(ICharacterMedicalRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 2;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        CharacterBodyHealthSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        List<string> warnings = new List<string>();
        runtime.Restore(
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonCharacterMedicalSaveData(),
            warnings);
        AddWarnings(report, warnings);
    }

    private void ValidateVersion(int version)
    {
        if (version != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {version}.");
        }
    }

    private static void AddWarnings(
        DungeonGameRestoreReport report,
        IEnumerable<string> warnings)
    {
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }
}

public sealed class SurgerySaveSection : IDungeonSaveSection
{
    public const string Id = "medical.surgery";
    private readonly ISurgeryRuntime runtime;

    public SurgerySaveSection(ISurgeryRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 2;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        CharacterBodyHealthSaveSection.Id,
        CharacterMedicalSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        WildlifeSaveSection.Id
    };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {sectionVersion}.");
        }

        List<string> warnings = new List<string>();
        runtime.Restore(
            JsonUtility.FromJson<DungeonSurgerySaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonSurgerySaveData(),
            warnings);
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }
}

public sealed class DefenseTacticalSaveSection : IDungeonSaveSection
{
    public const string Id = "combat.defense-tactics";
    private readonly IDefenseTacticalCoordinator runtime;

    public DefenseTacticalSaveSection(IDefenseTacticalCoordinator runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        CharacterBodyHealthSaveSection.Id,
        CombatEquipmentSaveSection.Id
    };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        List<string> warnings = new List<string>();
        runtime.Restore(
            JsonUtility.FromJson<DefenseTacticalCoordinatorSaveData>(
                payloadJson ?? string.Empty)
            ?? new DefenseTacticalCoordinatorSaveData(),
            warnings);
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }

    private void ValidateVersion(int version)
    {
        if (version != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {version}.");
        }
    }
}

public sealed class EquipmentMaintenanceSaveSection : IDungeonSaveSection
{
    public const string Id = "combat.equipment-maintenance";
    private readonly ICombatEquipmentMaintenanceRuntime runtime;

    public EquipmentMaintenanceSaveSection(
        ICombatEquipmentMaintenanceRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        CombatEquipmentSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        List<string> warnings = new List<string>();
        runtime.Restore(
            JsonUtility.FromJson<CombatEquipmentMaintenanceSaveData>(
                payloadJson ?? string.Empty)
            ?? new CombatEquipmentMaintenanceSaveData(),
            warnings);
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }

    private void ValidateVersion(int version)
    {
        if (version != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {version}.");
        }
    }
}

public sealed class CharacterCombatCommandSaveSection : IDungeonSaveSection
{
    public const string Id = "combat.commands";
    private readonly ICharacterCombatCommandRuntime runtime;

    public CharacterCombatCommandSaveSection(
        ICharacterCombatCommandRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        CharacterBodyHealthSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        DefenseTacticalSaveSection.Id
    };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        List<string> warnings = new List<string>();
        runtime.Restore(
            JsonUtility.FromJson<CharacterCombatCommandSaveData>(
                payloadJson ?? string.Empty)
            ?? new CharacterCombatCommandSaveData(),
            warnings);
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }

    private void ValidateVersion(int version)
    {
        if (version != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {version}.");
        }
    }
}
