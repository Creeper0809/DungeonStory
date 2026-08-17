using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SettlementPopulationPowerCheckpointDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/v26-population-power-checkpoints.md";

    [MenuItem("DungeonStory/Debug/Balance/Validate Population Power Checkpoints")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        IReadOnlyList<CombatBalanceCheckpoint> checkpoints =
            CombatBalanceCheckpointAuthority.All;
        IOffenseCampaignCatalog campaign =
            OffenseEditorTestDependencies.CreateCampaignCatalog();
        EquipmentFixture equipment = CreateEquipmentFixture();
        StringBuilder report = new StringBuilder(4096);
        report.AppendLine("# V26 population, proficiency and equipment checkpoints");
        report.AppendLine();
        report.AppendLine(
            "This is a deterministic theoretical baseline, not long-run player telemetry. "
            + "Character power is projected before the authored campaign requirement is read.");
        report.AppendLine();
        report.AppendLine(
            "| Day | Total | Working adults | Dependents | Combat ready | "
            + "Projected member | Authored loadout | Party | Target | Required | Ratio |");
        report.AppendLine(
            "|---:|---:|---:|---:|---:|---:|---|---:|---|---:|---:|");

        float previousMemberPower = 0f;
        foreach (CombatBalanceCheckpoint checkpoint in checkpoints)
        {
            ValidatePopulationBand(checkpoint);
            OffenseTargetDefinition target = RequireTarget(
                campaign,
                checkpoint.TargetId);
            float basePower =
                CombatBalanceCheckpointAuthority.CalculateProjectedBasePower(
                    checkpoint.Day);
            float equipmentPower = equipment.ProjectLoadout(
                $"checkpoint:{checkpoint.Day}",
                checkpoint.WeaponId,
                checkpoint.ArmorId,
                checkpoint.ShieldId,
                checkpoint.Quality);
            float memberPower = basePower
                + OffenseEquipmentPowerRules.CalculateLoadoutContribution(
                    basePower,
                    equipment.ActiveWeapon,
                    equipment.ActiveArmor,
                    equipment.ActiveShield);
            float partyPower = memberPower * target.requiredMembers;
            float ratio = target.requiredPower <= 0f
                ? 0f
                : partyPower / target.requiredPower;

            Require(
                memberPower + 0.001f >= previousMemberPower,
                $"Projected member power regressed at day {checkpoint.Day}.");
            Require(
                partyPower + 0.001f >= target.requiredPower,
                $"Checkpoint day {checkpoint.Day} cannot meet target "
                + $"'{target.id}' ({partyPower:0.0}/{target.requiredPower:0.0}).");
            Require(
                checkpoint.CombatReadyMinimum >= target.requiredMembers,
                $"Checkpoint day {checkpoint.Day} has fewer combat-ready adults "
                + "than the authored minimum party size.");
            Require(
                equipmentPower >= 0f,
                $"Checkpoint day {checkpoint.Day} produced invalid equipment power.");

            report.Append("| ").Append(checkpoint.Day)
                .Append(" | ").Append(Band(checkpoint.TotalMinimum, checkpoint.TotalMaximum))
                .Append(" | ").Append(Band(checkpoint.WorkingMinimum, checkpoint.WorkingMaximum))
                .Append(" | ").Append(Band(checkpoint.DependentMinimum, checkpoint.DependentMaximum))
                .Append(" | ").Append(Band(checkpoint.CombatReadyMinimum, checkpoint.CombatReadyMaximum))
                .Append(" | ").Append(memberPower.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" | ").Append(DescribeLoadout(checkpoint))
                .Append(" | ").Append(partyPower.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" | ").Append(target.id)
                .Append(" | ").Append(target.requiredPower.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" | ").Append(ratio.ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine(" |");
            previousMemberPower = memberPower;
        }

        report.AppendLine();
        report.AppendLine("## Authority notes");
        report.AppendLine();
        report.AppendLine(
            "- Biological births are dependents until species adulthood: "
            + "myconid 180d, slime 240d, kobold 300d, orc/beastkin/harpy 420d, "
            + "demon/vampire/human 540d; constructed golems are adults after assembly.");
        report.AppendLine(
            "- Early working-adult growth therefore depends on adult recruitment, "
            + "captivity recruitment and golem assembly rather than instant child labor.");
        report.AppendLine(
            "- Combat proficiency assumes a focused resident can earn at most 2 safe-training XP/day; "
            + "field, construction and food XP are conservative approved-work shares.");
        report.AppendLine(
            "- Loadouts are real authored equipment definitions projected through CombatEquipmentRuntime. "
            + "Only the checkpoint bands and training shares are theoretical targets.");
        report.AppendLine(
            "- Required power is a readiness gate, not a win-rate proof. Multi-seed battle calibration remains required.");

        string absolutePath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Checkpoint report directory is unavailable."));
        File.WriteAllText(absolutePath, report.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        return $"PASS: {checkpoints.Count} population/power checkpoints -> {ReportPath}";
    }

    private static void ValidatePopulationBand(CombatBalanceCheckpoint point)
    {
        Require(point.Day > 0, "Checkpoint day must be positive.");
        Require(point.TotalMinimum <= point.TotalMaximum, "Invalid total-population band.");
        Require(point.WorkingMinimum <= point.WorkingMaximum, "Invalid working-adult band.");
        Require(point.DependentMinimum <= point.DependentMaximum, "Invalid dependent band.");
        Require(point.CombatReadyMinimum <= point.CombatReadyMaximum, "Invalid combat-ready band.");
        Require(point.WorkingMaximum <= point.TotalMaximum, "Working adults exceed total population.");
        Require(point.DependentMaximum <= point.TotalMaximum, "Dependents exceed total population.");
        Require(point.CombatReadyMaximum <= point.WorkingMaximum, "Combat-ready adults exceed working adults.");
        Require(
            point.WorkingMinimum + point.DependentMinimum <= point.TotalMaximum,
            "Population subgroups cannot fit inside the total band.");
    }

    private static OffenseTargetDefinition RequireTarget(
        IOffenseCampaignCatalog catalog,
        string targetId)
    {
        if (catalog == null || !catalog.TryGet(targetId, out OffenseTargetDefinition target))
        {
            throw new InvalidOperationException(
                $"Missing authored campaign target '{targetId}'.");
        }
        return target;
    }

    private static EquipmentFixture CreateEquipmentFixture()
    {
        IGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        WorldItemRepository repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(content),
            repository,
            new CharacterCarryInventoryRegistry(),
            new ResourceEconomyContentCatalog(content),
            EmptyEvolutionModuleRegistry.Instance,
            EditorAllResearchRuntimeProvider.Instance,
            new ResourceEquipmentModuleCatalog(content),
            UnavailableEquipmentPhysicalItemGateway.Instance);
        return new EquipmentFixture(runtime, repository);
    }

    private sealed class EquipmentFixture
    {
        private readonly CombatEquipmentRuntime runtime;
        private readonly WorldItemRepository repository;

        public EquipmentFixture(
            CombatEquipmentRuntime runtime,
            WorldItemRepository repository)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public CombatWeaponSnapshot ActiveWeapon { get; private set; } =
            CombatWeaponSnapshot.CreateUnarmed();
        public IReadOnlyList<CombatArmorSnapshot> ActiveArmor { get; private set; } =
            Array.Empty<CombatArmorSnapshot>();
        public CombatShieldSnapshot ActiveShield { get; private set; }

        public float ProjectLoadout(
            string characterId,
            string weaponId,
            string armorId,
            string shieldId,
            CombatEquipmentQuality quality)
        {
            Assign(characterId, weaponId, quality, setActiveWeapon: true);
            Assign(characterId, armorId, quality, setActiveWeapon: false);
            Assign(characterId, shieldId, quality, setActiveWeapon: false);
            runtime.TryGetActiveWeapon(characterId, out CombatWeaponSnapshot weapon);
            ActiveWeapon = weapon ?? CombatWeaponSnapshot.CreateUnarmed();
            ActiveArmor = runtime.GetArmor(characterId);
            ActiveShield = runtime.GetShield(characterId);
            return OffenseEquipmentPowerRules.CalculateWeaponContribution(ActiveWeapon);
        }

        private void Assign(
            string characterId,
            string definitionId,
            CombatEquipmentQuality quality,
            bool setActiveWeapon)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return;
            }
            CombatEquipmentInstance instance = runtime.CreateInstance(definitionId, quality);
            if (!repository.EquipmentInstances.TryGetValue(
                    instance.instanceId,
                    out CombatEquipmentInstance stored))
            {
                throw new InvalidOperationException(
                    $"Created equipment '{definitionId}' has no repository instance.");
            }
            if (setActiveWeapon
                && runtime.TryGetDefinition(definitionId, out CombatEquipmentDefinitionSO definition)
                && definition is CombatWeaponSO weapon
                && weapon.MagazineCapacity > 0)
            {
                stored.loadedAmmunition = new LoadedAmmunitionBatch
                {
                    ammunitionItemId = weapon.AmmunitionItemId,
                    remaining = Math.Max(1, weapon.MagazineCapacity)
                };
            }
            if (!runtime.TryAssignToCharacter(
                    characterId,
                    instance.instanceId,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    $"Could not assign '{definitionId}' to '{characterId}': {failureReason}");
            }
            if (setActiveWeapon
                && !runtime.TrySetActiveWeapon(
                    characterId,
                    instance.instanceId,
                    out failureReason))
            {
                throw new InvalidOperationException(
                    $"Could not activate '{definitionId}' for '{characterId}': {failureReason}");
            }
        }
    }

    private static string Band(int minimum, int maximum) =>
        minimum == maximum ? minimum.ToString() : $"{minimum}-{maximum}";

    private static string DescribeLoadout(CombatBalanceCheckpoint point)
    {
        string[] parts = new[]
            {
                point.WeaponId,
                point.RangedWeaponId,
                point.ArmorId,
                point.ShieldId
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return $"{string.Join(" + ", parts)} ({point.Quality})";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
