using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CharacterDetailedStatsTab
{
    Summary = 0,
    BaseStats = 1,
    Work = 2,
    CombatEquipment = 3,
    HealthAnatomy = 4,
    Modifiers = 5
}

public sealed class CharacterDetailedStatRow
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public int Severity { get; set; }
    public bool IsRedacted { get; set; }
}

public sealed class CharacterDetailedStatsSnapshot
{
    private readonly Dictionary<CharacterDetailedStatsTab, IReadOnlyList<CharacterDetailedStatRow>> rows;

    public CharacterDetailedStatsSnapshot(
        string characterId,
        string displayName,
        string species,
        bool ownsDetails,
        Dictionary<CharacterDetailedStatsTab, IReadOnlyList<CharacterDetailedStatRow>> rows)
    {
        CharacterId = characterId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Species = species ?? string.Empty;
        OwnsDetails = ownsDetails;
        this.rows = rows ?? new();
    }

    public string CharacterId { get; }
    public string DisplayName { get; }
    public string Species { get; }
    public bool OwnsDetails { get; }

    public IReadOnlyList<CharacterDetailedStatRow> GetRows(CharacterDetailedStatsTab tab) =>
        rows.TryGetValue(tab, out IReadOnlyList<CharacterDetailedStatRow> value)
            ? value
            : Array.Empty<CharacterDetailedStatRow>();
}

public interface ICharacterDetailedStatsRuntime
{
    CharacterDetailedStatsSnapshot GetSnapshot(CharacterActor actor);
}

public sealed class CharacterDetailedStatsRuntime : ICharacterDetailedStatsRuntime
{
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IAnatomyEffectRuntime effects;
    private readonly IAnatomyProfileCatalog profiles;
    private readonly IAnatomyConditionLexicon lexicon;
    private readonly ICombatEquipmentRuntime equipment;

    public CharacterDetailedStatsRuntime(
        IAnatomyHealthRuntime anatomy,
        IAnatomyEffectRuntime effects,
        IAnatomyProfileCatalog profiles,
        IAnatomyConditionLexicon lexicon,
        ICombatEquipmentRuntime equipment)
    {
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.lexicon = lexicon ?? throw new ArgumentNullException(nameof(lexicon));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
    }

    public CharacterDetailedStatsSnapshot GetSnapshot(CharacterActor actor)
    {
        if (actor == null)
        {
            return new CharacterDetailedStatsSnapshot(
                string.Empty,
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Selection.None"),
                string.Empty,
                false,
                new());
        }

        actor.EnsureRuntimeState();
        string characterId = actor.Identity?.PersistentId ?? string.Empty;
        string displayName = actor.Identity?.DisplayName ?? actor.name;
        string species = actor.SpeciesTag;
        bool ownsDetails = actor.IsOwner || actor.characterType == CharacterType.NPC;
        Dictionary<CharacterDetailedStatsTab, IReadOnlyList<CharacterDetailedStatRow>> rows = new()
        {
            [CharacterDetailedStatsTab.Summary] = BuildSummary(actor),
            [CharacterDetailedStatsTab.BaseStats] = BuildBaseStats(actor),
            [CharacterDetailedStatsTab.Work] = BuildWork(actor),
            [CharacterDetailedStatsTab.CombatEquipment] = BuildCombat(actor, characterId),
            [CharacterDetailedStatsTab.HealthAnatomy] = BuildHealth(actor, species, ownsDetails),
            [CharacterDetailedStatsTab.Modifiers] = BuildModifiers(actor)
        };
        return new CharacterDetailedStatsSnapshot(
            characterId,
            displayName,
            species,
            ownsDetails,
            rows);
    }

    private static IReadOnlyList<CharacterDetailedStatRow> BuildSummary(CharacterActor actor)
    {
        CharacterProgression progression = actor.Progression;
        return new[]
        {
            Row(
                "summary:health",
                CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.Health"),
                $"{actor.CurrentHealth:0.#} / {actor.MaxHealth:0.#}",
                actor.IsDead
                    ? CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.Dead")
                    : CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.CombatVitality")),
            Row(
                "summary:level",
                CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.Level"),
                $"Lv.{progression?.Level ?? 1}",
                CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.LevelDetail")),
            Row(
                "summary:move",
                CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.ActualMovement"),
                actor.GetMoveSpeed().ToString("0.##"),
                CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.MovementDetail")),
            Row(
                "summary:species",
                CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.Species"),
                string.IsNullOrWhiteSpace(actor.SpeciesTag)
                    ? CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Common.Unknown")
                    : actor.SpeciesTag,
                actor.IsOwner
                    ? CharacterDetailedStatsTextFormatter.Get("CharacterSummary.Detailed.Summary.DungeonOwner")
                    : actor.characterType.ToString())
        };
    }

    private static IReadOnlyList<CharacterDetailedStatRow> BuildBaseStats(CharacterActor actor)
    {
        List<CharacterDetailedStatRow> rows = new();
        foreach (CharacterStatDefinition definition in CharacterStatCatalog.All)
        {
            if (!definition.LegacyType.HasValue) continue;
            CharacterStatType type = definition.LegacyType.Value;
            CharacterStatBreakdown breakdown = actor.Progression != null
                ? actor.Progression.GetStatBreakdown(type)
                : new CharacterStatBreakdown(type, actor.GetCharacterStat(type), 0, 0, 0,
                    actor.GetCharacterStat(type));
            rows.Add(Row(
                definition.Id,
                definition.DisplayName,
                breakdown.FinalValue.ToString(),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.BaseStats.Breakdown",
                    breakdown.BaseValue,
                    breakdown.SpeciesTraitValue,
                    breakdown.LevelGrowthValue,
                    breakdown.ConditionalPassiveValue)));
        }
        return rows;
    }

    private static IReadOnlyList<CharacterDetailedStatRow> BuildWork(CharacterActor actor)
    {
        return WorkTypeCatalog.All
            .Select(definition => Row(
                definition.Id,
                definition.DisplayName,
                $"×{actor.GetWorkSpeedMultiplier(definition.WorkTypeId):0.##}",
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Work.CurrentMultiplier")))
            .ToArray();
    }

    private IReadOnlyList<CharacterDetailedStatRow> BuildCombat(
        CharacterActor actor,
        string characterId)
    {
        List<CharacterDetailedStatRow> rows = new();
        equipment.TryGetActiveWeapon(characterId, out CombatWeaponSnapshot weapon);
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();
        rows.Add(Row(
            "combat:weapon",
            CharacterDetailedStatsTextFormatter.Get(
                "CharacterSummary.Detailed.Combat.PrimaryWeapon"),
            weapon.DefinitionId,
            CharacterDetailedStatsTextFormatter.Get(
                "CharacterSummary.Detailed.Combat.WeaponDetail",
                weapon.Verb?.baseDamage ?? 0f,
                weapon.MaterialDamageMultiplier,
                weapon.Verb?.penetration ?? 0f,
                weapon.MaterialPenetrationMultiplier,
                weapon.RequiresAmmo
                    ? CharacterDetailedStatsTextFormatter.Get(
                        "CharacterSummary.Detailed.Combat.AmmoSuffix",
                        weapon.LoadedAmmo,
                        weapon.MagazineCapacity)
                    : string.Empty)));

        CombatArmorSnapshot[] armor = equipment.GetArmor(characterId).ToArray();
        foreach (IGrouping<string, CombatArmorSnapshot> group in armor
                     .Where(value => !string.IsNullOrWhiteSpace(value.InstanceId))
                     .GroupBy(value => value.InstanceId, StringComparer.Ordinal))
        {
            CombatArmorSnapshot[] parts = group.ToArray();
            rows.Add(Row(
                "combat:armor:" + group.Key,
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.Armor"),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.Durability",
                    parts.Average(value => value.DurabilityRatio) * 100f),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.ArmorDetail",
                    parts.Length,
                    parts.Sum(value => value.GetDefense(CombatDamageType.Slash)),
                    parts.Sum(value => value.GetDefense(CombatDamageType.Pierce)),
                    parts.Sum(value => value.GetDefense(CombatDamageType.Blunt)))));
        }

        CombatShieldSnapshot shield = equipment.GetShield(characterId);
        if (shield.IsValid)
        {
            rows.Add(Row(
                "combat:shield",
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.Shield"),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.Durability",
                    shield.DurabilityRatio * 100f),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.ShieldDetail",
                    shield.GetBlockChance() * 100f)));
        }

        if (rows.Count == 1)
        {
            rows.Add(Row(
                "combat:armor:none",
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.DefensiveEquipment"),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Common.None"),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Combat.EmptySlots")));
        }
        return rows;
    }

    private IReadOnlyList<CharacterDetailedStatRow> BuildHealth(
        CharacterActor actor,
        string species,
        bool ownsDetails)
    {
        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
        profiles.TryGet(snapshot.ProfileId, out AnatomyProfileDefinition profile);
        Dictionary<string, AnatomyNodeDefinition> definitions = (profile?.Nodes
                ?? Array.Empty<AnatomyNodeDefinition>())
            .ToDictionary(value => value.NodeId, StringComparer.Ordinal);
        List<CharacterDetailedStatRow> rows = new();
        foreach (AnatomyNodeHealthState node in snapshot.Nodes ?? Array.Empty<AnatomyNodeHealthState>())
        {
            definitions.TryGetValue(node.nodeId, out AnatomyNodeDefinition definition);
            AnatomyConditionKind condition = ResolveCondition(node);
            int severity = ResolveSeverity(node, condition);
            bool normal = severity == 0
                && Mathf.Abs(node.installedPartEfficiency - 1f) < 0.001f
                && node.recoveryPolicy == PartRecoveryPolicy.Natural;
            if (normal) continue;

            string conditionLabel = ResolveConditionLabel(
                species, profile?.AnatomyFamily, condition);
            string partName = definition?.DisplayName ?? node.nodeId;
            if (!ownsDetails)
            {
                rows.Add(new CharacterDetailedStatRow
                {
                    Id = "anatomy:" + node.nodeId,
                    Label = partName,
                    Value = conditionLabel,
                    Detail = CharacterDetailedStatsTextFormatter.Get(
                        "CharacterSummary.Detailed.Anatomy.Redacted"),
                    Severity = severity,
                    IsRedacted = true
                });
                continue;
            }

            float healthPercent = node.HealthRatio * 100f;
            float naturalPercent = node.FunctionalEfficiency * 100f;
            string installed = string.IsNullOrWhiteSpace(node.installedPartId)
                ? partName
                : node.installedPartId;
            rows.Add(new CharacterDetailedStatRow
            {
                Id = "anatomy:" + node.nodeId,
                Label = installed,
                Value = CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Anatomy.Value",
                    conditionLabel,
                    healthPercent,
                    naturalPercent),
                Detail = CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Anatomy.Detail",
                    node.installedPartEfficiency,
                    node.ConditionFactor,
                    node.moduleBonus,
                    CharacterDetailedStatsTextFormatter.RecoveryLabel(
                        node.recoveryPolicy)),
                Severity = severity
            });
        }

        return rows
            .OrderByDescending(value => value.Severity)
            .ThenBy(value => value.Label, StringComparer.Ordinal)
            .DefaultIfEmpty(Row(
                "anatomy:normal",
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Anatomy.Status"),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Anatomy.Normal"),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Anatomy.NoAbnormalParts")))
            .ToArray();
    }

    private IReadOnlyList<CharacterDetailedStatRow> BuildModifiers(CharacterActor actor)
    {
        List<CharacterDetailedStatRow> rows = new();
        foreach (AnatomyActivityId activity in Enum.GetValues(typeof(AnatomyActivityId)))
        {
            AnatomyActivityFactorSnapshot factor = effects.GetActivityFactor(actor, activity);
            rows.Add(Row(
                "activity:" + activity,
                CharacterDetailedStatsTextFormatter.ActivityLabel(activity),
                $"×{factor.AppliedFactor:0.##}",
                factor.IsCapped
                    ? CharacterDetailedStatsTextFormatter.Get(
                        "CharacterSummary.Detailed.Modifier.Capped",
                        factor.RawFactor,
                        factor.Cap)
                    : CharacterDetailedStatsTextFormatter.Get(
                        "CharacterSummary.Detailed.Modifier.Uncapped",
                        factor.RawFactor,
                        factor.Cap)));
        }

        AnatomyActionAxisSnapshot axes = effects.GetActionAxes(actor);
        foreach (AnatomyActionAxisId axis in Enum.GetValues(typeof(AnatomyActionAxisId)))
        {
            rows.Add(Row(
                "axis:" + axis,
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Axis.Label",
                    CharacterDetailedStatsTextFormatter.AxisLabel(axis)),
                axes.Get(axis).ToString("0.##"),
                CharacterDetailedStatsTextFormatter.Get(
                    "CharacterSummary.Detailed.Axis.Detail")));
        }
        return rows;
    }

    private string ResolveConditionLabel(
        string species,
        string family,
        AnatomyConditionKind condition)
    {
        return lexicon.TryResolve(species, family, condition, out AnatomyConditionPresentation value)
            ? value.Label
            : CharacterDetailedStatsTextFormatter.Get(
                "CharacterSummary.Detailed.Condition.Missing",
                condition);
    }

    private static AnatomyConditionKind ResolveCondition(AnatomyNodeHealthState node)
    {
        if (node.missing || node.currentHealth <= 0f) return AnatomyConditionKind.PartFailure;
        if (node.rejectionBurden > 0.01f) return AnatomyConditionKind.CompatibilityFailure;
        if (node.infection > 0.01f) return AnatomyConditionKind.Contamination;
        if (node.bleedingPerSecond > 0.001f) return AnatomyConditionKind.FluidLoss;
        if (node.HealthRatio < 0.75f) return AnatomyConditionKind.Fracture;
        return AnatomyConditionKind.TreatmentRequired;
    }

    private static int ResolveSeverity(AnatomyNodeHealthState node, AnatomyConditionKind condition)
    {
        if (node.missing || node.currentHealth <= 0f) return 500;
        if (condition == AnatomyConditionKind.FluidLoss) return 400 + Mathf.RoundToInt(node.bleedingPerSecond * 10f);
        if (condition == AnatomyConditionKind.Contamination) return 300 + Mathf.RoundToInt(node.infection);
        if (condition == AnatomyConditionKind.CompatibilityFailure) return 250 + Mathf.RoundToInt(node.rejectionBurden);
        if (node.HealthRatio < 0.5f) return 200;
        if (node.HealthRatio < 0.99f) return 100;
        return 0;
    }

    private static CharacterDetailedStatRow Row(string id, string label, string value, string detail) => new()
    {
        Id = id,
        Label = label,
        Value = value,
        Detail = detail
    };

    public static string TabLabel(CharacterDetailedStatsTab value) =>
        CharacterDetailedStatsTextFormatter.TabLabel(value);
}
