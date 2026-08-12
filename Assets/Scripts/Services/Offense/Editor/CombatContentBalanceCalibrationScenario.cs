#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CombatContentBalanceCalibrationScenario
{
    private const string ReportPath =
        "Artifacts/QA/combat-content-balance.txt";

    private sealed class EncounterBudget
    {
        public OffenseEncounterSO Encounter;
        public int Campaign;
        public int StrengthBand;
        public float TargetRequiredPower;
        public float CampaignReferencePower;
        public float RawThreat;
        public float Threat;
        public float RewardEwu;
        public int ExpectedEnemies;
        public int UnpricedRewards;
    }

    [MenuItem("DungeonStory/Balance/Run Combat Content Calibration")]
    public static void RunFromMenu()
    {
        string report = Run();
        Debug.Log(report);
    }

    public static string Run()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaignCatalog = new(content);
        IReadOnlyDictionary<int, OffenseTargetDefinition> targetsByCampaign =
            campaignCatalog.Targets
                .GroupBy(value => value.campaignOrder)
                .ToDictionary(group => group.Key, group => group.Single());
        ResourceCombatEquipmentCatalog equipment = new(content);
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(content);
        V23BalanceWorkCalculator work = new(materialProfiles);
        ItemDefinitionSO[] itemDefinitions = content
            .GetAll<ItemDefinitionSO>()
            .Concat(Resources.LoadAll<ResourceItemDefinitionSO>(
                ResourceItemDefinitionSO.ResourcePath))
            .Where(value => value != null)
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        EmbeddedWorkValueSnapshot embeddedWork =
            new V23EmbeddedWorkValueCalculator(
                content.GetAll<ProductionRecipeSO>(),
                itemDefinitions,
                content.GetAll<CombatEquipmentDefinitionSO>(),
                content.GetAll<CraftMaterialDefinitionSO>(),
                work)
            .Calculate();
        IReadOnlyDictionary<string, ItemDefinitionSO> items = itemDefinitions
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        IEncounterCatalog encounterCatalog = combat;
        IEnemyAbilityCatalog abilityCatalog = combat;
        List<string> failures = new();
        ValidateCampaignScaleRules(failures);
        List<EncounterBudget> budgets = encounterCatalog.All
            .OrderBy(value => value.encounterId, StringComparer.Ordinal)
            .Select(value => Calculate(
                value,
                combat,
                abilityCatalog,
                equipment,
                embeddedWork,
                items,
                targetsByCampaign,
                failures))
            .ToList();

        Require(budgets.Count == 36,
            $"Expected 36 encounters, found {budgets.Count}.",
            failures);
        for (int campaign = 1; campaign <= 6; campaign++)
        {
            EncounterBudget[] chapter = budgets
                .Where(value => value.Campaign == campaign)
                .OrderBy(value => value.Threat)
                .ToArray();
            Require(chapter.Length == 6,
                $"Campaign {campaign} has {chapter.Length}/6 encounters.",
                failures);
            if (chapter.Length == 0)
            {
                continue;
            }

            float median = Median(chapter.Select(value => value.Threat));
            foreach (EncounterBudget budget in chapter)
            {
                float ratio = budget.Threat / Mathf.Max(0.01f, median);
                Require(ratio is >= 0.25f and <= 4f,
                    $"{budget.Encounter.encounterId} threat is {ratio:0.00}x its campaign median.",
                    failures);
            }
        }

        float[] campaignMedians = Enumerable.Range(1, 6)
            .Select(campaign => Median(budgets
                .Where(value => value.Campaign == campaign)
                .Select(value => value.Threat)))
            .ToArray();
        for (int index = 1; index < campaignMedians.Length; index++)
        {
            Require(campaignMedians[index]
                    >= campaignMedians[index - 1] * 1.05f,
                $"Campaign {index + 1} median threat did not rise at least 5% over campaign {index}.",
                failures);
        }

        StringBuilder report = new();
        report.AppendLine("COMBAT_CONTENT_BALANCE_V1");
        report.AppendLine(
            $"encounters={budgets.Count}; archetypes={combat.All.Count}; failures={failures.Count}");
        report.AppendLine(
            "Columns: encounter | campaign | target power | enemy reference power | site metadata | flags | objective | expected enemies | raw threat | projected threat | reward EWU | unpriced rewards | reward/threat");
        foreach (EncounterBudget budget in budgets)
        {
            string flags = budget.Encounter.boss
                ? "boss"
                : budget.Encounter.elite ? "elite" : "standard";
            report.AppendLine(string.Join(" | ",
                budget.Encounter.encounterId,
                budget.Campaign.ToString(CultureInfo.InvariantCulture),
                budget.TargetRequiredPower.ToString("0.0", CultureInfo.InvariantCulture),
                budget.CampaignReferencePower.ToString("0.0", CultureInfo.InvariantCulture),
                $"{budget.Encounter.minimumSiteStrength}-{budget.Encounter.maximumSiteStrength} (band {budget.StrengthBand})",
                flags,
                budget.Encounter.objective,
                budget.ExpectedEnemies.ToString(CultureInfo.InvariantCulture),
                budget.RawThreat.ToString("0.00", CultureInfo.InvariantCulture),
                budget.Threat.ToString("0.00", CultureInfo.InvariantCulture),
                budget.RewardEwu.ToString("0.00", CultureInfo.InvariantCulture),
                budget.UnpricedRewards.ToString(CultureInfo.InvariantCulture),
                (budget.RewardEwu / Mathf.Max(0.01f, budget.Threat))
                    .ToString("0.000", CultureInfo.InvariantCulture)));
        }

        report.AppendLine("CAMPAIGN_MEDIANS");
        for (int index = 0; index < campaignMedians.Length; index++)
        {
            int campaignNumber = index + 1;
            EncounterBudget[] campaign = budgets
                .Where(value => value.Campaign == campaignNumber)
                .ToArray();
            report.AppendLine(
                $"campaign={campaignNumber}; target_power={campaign[0].TargetRequiredPower:0.0}; enemy_reference_power={campaign[0].CampaignReferencePower:0.0}; encounters={campaign.Length}; threat_median={campaignMedians[index]:0.00}; "
                + $"threat_min={campaign.Min(value => value.Threat):0.00}; "
                + $"threat_max={campaign.Max(value => value.Threat):0.00}; "
                + $"reward_ewu_median={Median(campaign.Select(value => value.RewardEwu)):0.00}");
        }

        if (failures.Count > 0)
        {
            report.AppendLine("FAILURES");
            foreach (string failure in failures)
            {
                report.AppendLine("- " + failure);
            }
        }

        WriteReport(report.ToString());
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Combat content balance calibration failed ({failures.Count}). See {ReportPath}.");
        }

        return $"COMBAT_CONTENT_BALANCE=PASS; encounters={budgets.Count}; "
            + $"campaign_medians={string.Join("/", campaignMedians.Select(value => value.ToString("0.0", CultureInfo.InvariantCulture)))}";
    }

    private static EncounterBudget Calculate(
        OffenseEncounterSO encounter,
        IEnemyArchetypeCatalog archetypes,
        IEnemyAbilityCatalog abilities,
        ICombatEquipmentCatalog equipment,
        EmbeddedWorkValueSnapshot embeddedWork,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IReadOnlyDictionary<int, OffenseTargetDefinition> targetsByCampaign,
        ICollection<string> failures)
    {
        int expectedEnemies = 0;
        float threat = 0f;
        float rewardEwu = 0f;
        int unpricedRewards = 0;
        foreach (OffenseEnemyArchetypeEntry entry in encounter.enemies)
        {
            float expectedCount = (entry.minimumCount + entry.maximumCount) * 0.5f;
            expectedEnemies += Mathf.RoundToInt(expectedCount);
            EnemyArchetypeDefinitionSO archetype = archetypes.Require(
                entry.enemyArchetypeId);
            threat += CalculateArchetypeThreat(
                archetype,
                abilities,
                equipment) * expectedCount;
            AddRewards(archetype.rewardItemIds, expectedCount);
        }

        AddRewards(encounter.rewardItemIds, 1f);
        float objectiveMultiplier = encounter.objective switch
        {
            OffenseEncounterObjective.DefeatAll => 1f,
            OffenseEncounterObjective.SurviveRounds => 1.08f,
            OffenseEncounterObjective.ProtectTarget => 1.12f,
            OffenseEncounterObjective.SabotageTarget => 1.10f,
            OffenseEncounterObjective.Escape => 1.05f,
            OffenseEncounterObjective.CaptureLeader => 1.16f,
            _ => 1f
        };
        float flagMultiplier = encounter.boss
            ? 1.30f
            : encounter.elite ? 1.15f : 1f;
        float modifierMultiplier = 1f
            + (encounter.battlefieldModifierIds?.Count ?? 0) * 0.05f;
        threat *= objectiveMultiplier * flagMultiplier * modifierMultiplier;
        float rawThreat = threat;
        int encounterNumber = ParseEncounterNumber(encounter.encounterId);
        int campaign = encounterNumber <= 0
            ? 0
            : ((encounterNumber - 1) / 6) + 1;
        if (!targetsByCampaign.TryGetValue(
                campaign,
                out OffenseTargetDefinition target))
        {
            failures.Add(
                $"{encounter.encounterId} has no campaign target for campaign {campaign}.");
        }

        float targetRequiredPower = target?.requiredPower ?? 0f;
        float campaignReferencePower =
            OffenseCampaignCombatBalanceRules.GetCampaignReferencePower(campaign);
        threat *= OffenseCampaignCombatBalanceRules.CalculateThreatScale(
            campaign);
        Require(float.IsFinite(threat) && threat > 0f,
            $"{encounter.encounterId} has invalid threat {threat}.",
            failures);

        return new EncounterBudget
        {
            Encounter = encounter,
            Campaign = campaign,
            StrengthBand = Mathf.RoundToInt(
                (encounter.minimumSiteStrength
                    + encounter.maximumSiteStrength) * 0.5f),
            TargetRequiredPower = targetRequiredPower,
            CampaignReferencePower = campaignReferencePower,
            RawThreat = rawThreat,
            Threat = threat,
            RewardEwu = rewardEwu,
            ExpectedEnemies = expectedEnemies,
            UnpricedRewards = unpricedRewards
        };

        void AddRewards(IEnumerable<string> rewardIds, float multiplier)
        {
            foreach (string rewardId in rewardIds ?? Array.Empty<string>())
            {
                if (!items.ContainsKey(rewardId))
                {
                    failures.Add(
                        $"{encounter.encounterId} references unknown reward '{rewardId}'.");
                    continue;
                }

                string pricedRewardId = string.Equals(
                    rewardId,
                    OffenseLootItemIds.UnappraisedLoot,
                    StringComparison.Ordinal)
                        ? OffenseLootItemIds.AppraisedValuables
                        : rewardId;
                if (embeddedWork.ItemWork.TryGetValue(
                        pricedRewardId,
                        out float itemWork)
                    && itemWork > 0f)
                {
                    rewardEwu += itemWork * multiplier;
                }
                else
                {
                    unpricedRewards++;
                }
            }
        }
    }

    private static int ParseEncounterNumber(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId))
        {
            return 0;
        }

        int separator = encounterId.LastIndexOf(':');
        return separator >= 0
            && int.TryParse(
                encounterId.Substring(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsed)
            ? parsed
            : 0;
    }

    private static void ValidateCampaignScaleRules(
        ICollection<string> failures)
    {
        Require(Mathf.Approximately(
                OffenseCampaignCombatBalanceRules.CalculateStatScale(1),
                1f),
            "Campaign 1 stat scale baseline must be 1.0.",
            failures);
        Require(Mathf.Abs(
                OffenseCampaignCombatBalanceRules.CalculateStatScale(2)
                - Mathf.Sqrt(1.6f)) < 0.0001f,
            "Campaign stat scale must follow its fixed reference-power curve.",
            failures);
        Require(Mathf.Abs(
                OffenseCampaignCombatBalanceRules.CalculateThreatScale(6)
                - 8.5f) < 0.0001f,
            "Campaign threat scale must preserve fixed reference-power ratios.",
            failures);
        Require(OffenseCampaignCombatBalanceRules.CalculateInitiativeScale(6)
                <= 1.55f,
            "Campaign initiative scale exceeded its hard cap.",
            failures);
    }

    private static float CalculateArchetypeThreat(
        EnemyArchetypeDefinitionSO archetype,
        IEnemyAbilityCatalog abilities,
        ICombatEquipmentCatalog equipment)
    {
        float weapon = 5f;
        if (equipment.TryGet(
                archetype.equipment?.weaponDefinitionId,
                out CombatEquipmentDefinitionSO weaponDefinition)
            && weaponDefinition is CombatWeaponSO weaponSo)
        {
            CombatWeaponSnapshot snapshot = weaponSo.CreateSnapshot(null);
            CombatAttackVerb verb = snapshot.Verb;
            weapon = verb == null
                ? 5f
                : verb.baseDamage / Mathf.Max(0.25f, verb.attackTime)
                    + verb.penetration * 0.35f
                    + snapshot.MaximumRange * 0.5f
                    + (snapshot.SupportsAimed ? 1.5f : 0f)
                    + (snapshot.SupportsRapid ? 2f : 0f)
                    + (snapshot.SupportsSuppressive ? 2f : 0f);
        }

        float armor = 0f;
        if (equipment.TryGet(
                archetype.equipment?.armorDefinitionId,
                out CombatEquipmentDefinitionSO armorDefinition)
            && armorDefinition is CombatArmorSO armorSo
            && armorSo.BodyPartDefense.Count > 0)
        {
            armor = armorSo.BodyPartDefense.Average(value =>
                value.slashDefense + value.pierceDefense + value.bluntDefense)
                * 0.10f;
        }

        float shield = 0f;
        if (equipment.TryGet(
                archetype.equipment?.shieldDefinitionId,
                out CombatEquipmentDefinitionSO shieldDefinition)
            && shieldDefinition is CombatShieldSO shieldSo)
        {
            shield = shieldSo.FrontalBlockChance * 20f
                + (shieldSo.SlashDefense
                    + shieldSo.PierceDefense
                    + shieldSo.BluntDefense) * 0.08f;
        }

        float ability = (archetype.abilityIds ?? new List<string>())
            .Select(abilities.Require)
            .Sum(CalculateAbilityThreat);
        float roleMultiplier = archetype.role switch
        {
            EnemyCombatRole.Boss => 1.20f,
            EnemyCombatRole.Support or EnemyCombatRole.Controller => 1.08f,
            EnemyCombatRole.Defender => 1.05f,
            _ => 1f
        };
        return (archetype.maxHealth * 0.20f
            + archetype.attack * 2f
            + archetype.strength * 1.20f
            + archetype.toughness * 1.50f
            + archetype.dexterity * 0.80f
            + archetype.moveSpeed * 0.80f
            + weapon
            + armor
            + shield
            + ability) * roleMultiplier;
    }

    private static float CalculateAbilityThreat(
        EnemyAbilityDefinitionSO ability)
    {
        float raw = (ability.effects ?? new List<EnemyAbilityEffectRecord>())
            .Sum(effect => effect.kind switch
            {
                EnemyAbilityEffectKind.Damage => effect.magnitude,
                EnemyAbilityEffectKind.DamageOverTime =>
                    effect.magnitude * Mathf.Max(1, effect.durationRounds) * 0.65f,
                EnemyAbilityEffectKind.Heal => effect.magnitude * 0.70f,
                EnemyAbilityEffectKind.Delay => effect.magnitude * 4f,
                EnemyAbilityEffectKind.Vulnerability =>
                    effect.magnitude * Mathf.Max(1, effect.durationRounds) * 20f,
                EnemyAbilityEffectKind.Suppression => effect.magnitude * 5f,
                EnemyAbilityEffectKind.Smoke =>
                    effect.magnitude * Mathf.Max(1, effect.durationRounds) * 6f,
                EnemyAbilityEffectKind.Summon => effect.magnitude * 12f,
                EnemyAbilityEffectKind.Dispel => effect.magnitude * 5f,
                EnemyAbilityEffectKind.Guard =>
                    effect.magnitude * Mathf.Max(1, effect.durationRounds) * 12f,
                _ => effect.magnitude
            });
        return raw / Mathf.Max(1f, ability.cooldownRounds + 1f);
    }

    private static float Median(IEnumerable<float> source)
    {
        float[] values = source.OrderBy(value => value).ToArray();
        if (values.Length == 0)
        {
            return 0f;
        }

        int middle = values.Length / 2;
        return values.Length % 2 == 0
            ? (values[middle - 1] + values[middle]) * 0.5f
            : values[middle];
    }

    private static void Require(
        bool condition,
        string failure,
        ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }

    private static void WriteReport(string text)
    {
        string absolute = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException("Report directory is invalid."));
        File.WriteAllText(absolute, text, new UTF8Encoding(false));
    }
}
#endif
