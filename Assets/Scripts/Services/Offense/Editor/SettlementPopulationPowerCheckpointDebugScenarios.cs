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

    private sealed class Checkpoint
    {
        public int Day;
        public int TotalMinimum;
        public int TotalMaximum;
        public int WorkingMinimum;
        public int WorkingMaximum;
        public int DependentMinimum;
        public int DependentMaximum;
        public int CombatReadyMinimum;
        public int CombatReadyMaximum;
        public string TargetId;
        public string WeaponId;
        public string ArmorId;
        public string ShieldId;
        public CombatEquipmentQuality Quality;
    }

    [MenuItem("DungeonStory/Debug/Balance/Validate Population Power Checkpoints")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        Checkpoint[] checkpoints = CreateCheckpoints();
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
        foreach (Checkpoint checkpoint in checkpoints)
        {
            ValidatePopulationBand(checkpoint);
            OffenseTargetDefinition target = RequireTarget(
                campaign,
                checkpoint.TargetId);
            float basePower = CalculateBasePower(checkpoint.Day);
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
        return $"PASS: {checkpoints.Length} population/power checkpoints -> {ReportPath}";
    }

    private static float CalculateBasePower(int day)
    {
        long Milli(float experience) =>
            checked((long)Math.Round(
                Math.Max(0f, experience) * 1000f,
                MidpointRounding.AwayFromZero));
        float fieldExperience = 30f + day * 1.20f;
        float constructionExperience = 30f + day * 0.30f;
        float foodExperience = 30f + day * 0.30f;
        float meleeExperience = 30f + day * 2.00f;
        float rangedExperience = 30f + day * 0.25f;

        return OffenseExpeditionService.CalculateProjectedProficiencyPower(id =>
        {
            if (id == BuiltInCharacterProficiencyIds.Fieldwork)
            {
                return Milli(fieldExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.ConstructionEngineering)
            {
                return Milli(constructionExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.FoodProduction)
            {
                return Milli(foodExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.MeleeCombat)
            {
                return Milli(meleeExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.RangedCombat)
            {
                return Milli(rangedExperience);
            }
            return Milli(30f);
        });
    }

    private static Checkpoint[] CreateCheckpoints() => new[]
    {
        Point(1, 3, 3, 3, 3, 0, 0, 2, 2,
            "food_farm", "weapon:spear", "armor:cloth-hood", string.Empty,
            CombatEquipmentQuality.Normal),
        Point(30, 3, 6, 3, 6, 0, 2, 2, 4,
            "merchant_road", "weapon:falchion", "armor:leather", "shield:wood",
            CombatEquipmentQuality.Normal),
        Point(120, 6, 14, 5, 12, 1, 4, 3, 7,
            "old_armory", "weapon:mace", "armor:mail-shirt", "shield:wood",
            CombatEquipmentQuality.Normal),
        Point(240, 12, 28, 8, 20, 4, 12, 5, 12,
            "mana_ruins", "weapon:estoc", "armor:articulated-plate", "shield:iron",
            CombatEquipmentQuality.Good),
        Point(400, 25, 60, 15, 40, 10, 25, 10, 24,
            "rival_dungeon", "weapon:powered-striking-gauntlet", "armor:powered-harness", "shield:powered",
            CombatEquipmentQuality.Good),
        Point(960, 80, 220, 55, 160, 25, 70, 25, 70,
            "truth_core", "weapon:rune-blade", "armor:rune-ward-mail", "shield:rune",
            CombatEquipmentQuality.Excellent)
    };

    private static Checkpoint Point(
        int day,
        int totalMin,
        int totalMax,
        int workingMin,
        int workingMax,
        int dependentMin,
        int dependentMax,
        int combatMin,
        int combatMax,
        string targetId,
        string weaponId,
        string armorId,
        string shieldId,
        CombatEquipmentQuality quality) => new Checkpoint
    {
        Day = day,
        TotalMinimum = totalMin,
        TotalMaximum = totalMax,
        WorkingMinimum = workingMin,
        WorkingMaximum = workingMax,
        DependentMinimum = dependentMin,
        DependentMaximum = dependentMax,
        CombatReadyMinimum = combatMin,
        CombatReadyMaximum = combatMax,
        TargetId = targetId,
        WeaponId = weaponId,
        ArmorId = armorId,
        ShieldId = shieldId,
        Quality = quality
    };

    private static void ValidatePopulationBand(Checkpoint point)
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

    private static string DescribeLoadout(Checkpoint point)
    {
        string[] parts = new[] { point.WeaponId, point.ArmorId, point.ShieldId }
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
