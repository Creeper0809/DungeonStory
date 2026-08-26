using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CombatEquipmentSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCombatEquipmentSaveData,
        CombatEquipmentRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.equipment";
    public const int CurrentVersion = 8;
    private readonly ICombatEquipmentRuntime runtime;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public CombatEquipmentSaveSection(
        ICombatEquipmentRuntime runtime,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[] { PhysicalItemsSaveSection.Id };

    protected override DungeonCombatEquipmentSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        DungeonCombatEquipmentSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override CombatEquipmentRestoreCandidate BuildRestoreCandidate(
        DungeonCombatEquipmentSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        CombatEquipmentRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);

    protected override void PublishRestoreCandidateProjection(
        DungeonCombatEquipmentSaveData payload,
        CombatEquipmentRestoreCandidate candidate) =>
        lifecycleRestoreCandidates.SetCombat(payload);
}

public sealed class EquipmentEvolutionSaveSection :
    DungeonStrictJsonSaveSection<
        EquipmentEvolutionSaveData,
        EquipmentEvolutionRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.equipment-evolution";
    public const int CurrentVersion = 4;
    private readonly IEquipmentEvolutionPersistence runtime;

    public EquipmentEvolutionSaveSection(IEquipmentEvolutionPersistence runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CombatEquipmentSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };
    protected override EquipmentEvolutionSaveData CapturePayload() =>
        runtime.Capture();

    protected override EquipmentEvolutionRestoreCandidate BuildRestoreCandidate(
        EquipmentEvolutionSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        EquipmentEvolutionRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}

public sealed class CharacterBodyHealthSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCharacterBodyHealthSaveData,
        CharacterBodyHealthRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.body-health";
    private readonly ICharacterBodyHealthPersistence persistence;

    public CharacterBodyHealthSaveSection(
        ICharacterBodyHealthPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonCharacterBodyHealthSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[] { CharacterWorldSaveSection.Id };

    protected override DungeonCharacterBodyHealthSaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        DungeonCharacterBodyHealthSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override CharacterBodyHealthRestoreCandidate
        BuildRestoreCandidate(DungeonCharacterBodyHealthSaveData payload) =>
        persistence.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        CharacterBodyHealthRestoreCandidate candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class CharacterMedicalSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCharacterMedicalSaveData,
        CharacterMedicalRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.medical";
    private static readonly string[] Dependencies =
    {
        CharacterBodyHealthSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly ICharacterMedicalPersistence persistence;

    public CharacterMedicalSaveSection(ICharacterMedicalPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonCharacterMedicalSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCharacterMedicalSaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        DungeonCharacterMedicalSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override CharacterMedicalRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterMedicalSaveData payload) =>
        persistence.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        CharacterMedicalRestoreCandidate candidate) =>
        persistence.PublishRestore(candidate);
}

public sealed class SurgerySaveSection :
    DungeonStrictJsonSaveSection<
        DungeonSurgerySaveData,
        SurgeryRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "medical.surgery";
    private static readonly string[] Dependencies =
    {
        CharacterBodyHealthSaveSection.Id,
        CharacterMedicalSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        WildlifeSaveSection.Id
    };

    private readonly ISurgeryPersistence persistence;
    private readonly SurgeryRestoreCoordinator restoreCoordinator;

    public SurgerySaveSection(
        ISurgeryPersistence persistence,
        SurgeryRestoreCoordinator restoreCoordinator)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.restoreCoordinator = restoreCoordinator
            ?? throw new ArgumentNullException(nameof(restoreCoordinator));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonSurgerySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonSurgerySaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        DungeonSurgerySaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override SurgeryRestoreCandidate BuildRestoreCandidate(
        DungeonSurgerySaveData payload) =>
        restoreCoordinator.PrepareRestore(payload);

    protected override void ValidateParsedPayload(
        DungeonSurgerySaveData payload) =>
        restoreCoordinator.ValidatePayload(payload);

    protected override void PublishRestoreCandidate(
        SurgeryRestoreCandidate candidate) =>
        restoreCoordinator.PublishRestore(candidate);
}

public sealed class DefenseTacticalSaveSection :
    DungeonStrictJsonSaveSection<
        DefenseTacticalCoordinatorSaveData,
        DefenseTacticalRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.defense-tactics";
    private static readonly string[] Dependencies =
    {
        CharacterBodyHealthSaveSection.Id,
        CombatEquipmentSaveSection.Id
    };

    private readonly IDefenseTacticalCoordinator runtime;

    public DefenseTacticalSaveSection(IDefenseTacticalCoordinator runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 2;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DefenseTacticalCoordinatorSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        DefenseTacticalCoordinatorSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override DefenseTacticalRestoreCandidate BuildRestoreCandidate(
        DefenseTacticalCoordinatorSaveData payload) =>
        runtime.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        DefenseTacticalRestoreCandidate candidate) =>
        runtime.PublishRestore(candidate);
}

public sealed class EquipmentMaintenanceSaveSection :
    DungeonStrictJsonSaveSection<
        CombatEquipmentMaintenanceSaveData,
        EquipmentMaintenanceRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.equipment-maintenance";
    public const int CurrentVersion = 4;
    private static readonly string[] Dependencies =
    {
        CombatEquipmentSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterBodyHealthSaveSection.Id
    };

    private readonly ICombatEquipmentMaintenanceRuntime runtime;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public EquipmentMaintenanceSaveSection(
        ICombatEquipmentMaintenanceRuntime runtime,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CombatEquipmentMaintenanceSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        CombatEquipmentMaintenanceSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override EquipmentMaintenanceRestoreCandidate
        BuildRestoreCandidate(CombatEquipmentMaintenanceSaveData payload) =>
        runtime.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        EquipmentMaintenanceRestoreCandidate candidate) =>
        runtime.PublishRestore(candidate);

    protected override void PublishRestoreCandidateProjection(
        CombatEquipmentMaintenanceSaveData payload,
        EquipmentMaintenanceRestoreCandidate candidate) =>
        lifecycleRestoreCandidates.SetMaintenance(payload);
}

public sealed class CharacterCombatCommandSaveSection :
    DungeonStrictJsonSaveSection<
        CharacterCombatCommandSaveData,
        CharacterCombatCommandRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.commands";
    private static readonly string[] Dependencies =
    {
        CharacterBodyHealthSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        DefenseTacticalSaveSection.Id
    };

    private readonly ICharacterCombatCommandRuntime runtime;

    public CharacterCombatCommandSaveSection(
        ICharacterCombatCommandRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 2;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CharacterCombatCommandSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        CharacterCombatCommandSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override CharacterCombatCommandRestoreCandidate
        BuildRestoreCandidate(CharacterCombatCommandSaveData payload) =>
        runtime.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        CharacterCombatCommandRestoreCandidate candidate) =>
        runtime.PublishRestore(candidate);
}
