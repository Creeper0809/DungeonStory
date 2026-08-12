using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public enum CharacterDetailedStatsTab
{
    Summary = 0,
    BaseStats = 1,
    Work = 2,
    CombatEquipment = 3,
    HealthAnatomy = 4,
    Modifiers = 5,
    Proficiencies = 6
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
    private readonly IAnatomyProfileCatalog profiles;
    private readonly IAnatomyConditionLexicon lexicon;
    private readonly ICombatEquipmentRuntime equipment;
    private ICharacterProficiencyQuery proficiencies;
    private ICharacterNarrativeCatalog narrativeCatalog;
    private IGameCalendar calendar;
    private ICareerService careers;
    private CharacterDerivedStatsSnapshotProjector gameplayEffects;
    private ICharacterPerformanceQuery performance;

    public CharacterDetailedStatsRuntime(
        IAnatomyHealthRuntime anatomy,
        IAnatomyProfileCatalog profiles,
        IAnatomyConditionLexicon lexicon,
        ICombatEquipmentRuntime equipment)
    {
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.lexicon = lexicon ?? throw new ArgumentNullException(nameof(lexicon));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
    }

    [Inject]
    public void ConstructProficiencies(
        ICharacterProficiencyQuery proficiencyQuery,
        ICharacterNarrativeCatalog catalog,
        IGameCalendar gameCalendar,
        ICareerService careerService)
    {
        proficiencies = proficiencyQuery;
        narrativeCatalog = catalog;
        calendar = gameCalendar;
        careers = careerService;
    }

    [Inject]
    public void ConstructGameplayEffects(
        CharacterDerivedStatsSnapshotProjector projector)
    {
        gameplayEffects = projector
            ?? throw new ArgumentNullException(nameof(projector));
    }

    [Inject]
    public void ConstructPerformance(ICharacterPerformanceQuery query)
    {
        performance = query ?? throw new ArgumentNullException(nameof(query));
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
            [CharacterDetailedStatsTab.BaseStats] =
                BuildProficiencyEffects(actor, characterId),
            [CharacterDetailedStatsTab.Work] = BuildWork(actor),
            [CharacterDetailedStatsTab.CombatEquipment] = BuildCombat(actor, characterId),
            [CharacterDetailedStatsTab.HealthAnatomy] = BuildHealth(actor, species, ownsDetails),
            [CharacterDetailedStatsTab.Modifiers] = BuildModifiers(actor),
            [CharacterDetailedStatsTab.Proficiencies] =
                BuildProficiencies(actor, characterId)
        };
        return new CharacterDetailedStatsSnapshot(
            characterId,
            displayName,
            species,
            ownsDetails,
            rows);
    }

    private IReadOnlyList<CharacterDetailedStatRow> BuildProficiencies(
        CharacterActor actor,
        string characterId)
    {
        CharacterId id = new(characterId);
        if (!id.IsValid || proficiencies == null || narrativeCatalog == null
            || calendar == null)
        {
            return new[]
            {
                Row(
                    "proficiency:unavailable",
                    "숙련",
                    "정보 없음",
                    "숙련 런타임이 아직 연결되지 않았습니다.")
            };
        }

        IReadOnlyList<CharacterProficiencySnapshot> values =
            proficiencies.GetAllProficiencies(id, calendar.AbsoluteHour);
        Dictionary<string, string> names = narrativeCatalog.Proficiencies
            .ToDictionary(
                value => value.ProficiencyId.Value,
                value => value.DisplayName,
                StringComparer.Ordinal);
        return values.Select(value =>
        {
            CharacterProficiencyBandSnapshot band = value.Band;
            long nextThreshold = band.NextMilliExperience;
            string rank = band.Rank switch
            {
                CharacterProficiencyRank.Apprentice => "\uACAC\uC2B5\uC0DD",
                CharacterProficiencyRank.Skilled => "\uC219\uB828\uC790",
                CharacterProficiencyRank.Technician => "\uAE30\uC220\uC790",
                CharacterProficiencyRank.Expert => "\uC804\uBB38\uAC00",
                _ => "\uB300\uAC00"
            };
            string subgrade = band.Subgrade switch
            {
                CharacterProficiencySubgrade.Fourth => "IV",
                CharacterProficiencySubgrade.Third => "III",
                CharacterProficiencySubgrade.Second => "II",
                CharacterProficiencySubgrade.First => "I",
                _ => string.Empty
            };
            string decay = BuildDecayText(value, calendar.AbsoluteHour);
            string mentorship = BuildMentorshipText(id, value.ProficiencyId);
            return Row(
                value.ProficiencyId.Value,
                names.TryGetValue(value.ProficiencyId.Value, out string name)
                    ? name
                    : value.ProficiencyId.Value,
                $"{rank} {subgrade} · {value.CurrentExperience:N0} XP",
                $"다음 기준 {nextThreshold / ProficiencyProgressionRules.MilliPerExperience:N0} XP"
                + (decay.Length > 0 ? $" · {decay}" : string.Empty)
                + (mentorship.Length > 0 ? $" · {mentorship}" : string.Empty));
        }).ToArray();
    }

    private static string BuildDecayText(
        CharacterProficiencySnapshot value,
        long absoluteHour)
    {
        if (value.Rank < CharacterProficiencyRank.Expert)
        {
            return string.Empty;
        }
        long grace = value.Rank == CharacterProficiencyRank.Master
            ? 5L * GameCalendarRules.HoursPerDay
            : 15L * GameCalendarRules.HoursPerDay;
        long start = value.LastPracticeAbsoluteHour + grace;
        if (absoluteHour < start)
        {
            return $"쇠퇴 시작까지 {(start - absoluteHour) / 24f:0.#}일";
        }
        long floor = value.Rank == CharacterProficiencyRank.Master
            ? ProficiencyProgressionRules.ExpertThreshold
            : ProficiencyProgressionRules.TechnicianThreshold;
        long rate = value.Rank == CharacterProficiencyRank.Master ? 100L : 250L;
        long hours = Math.Max(
            0L,
            (value.CurrentMilliExperience - floor + rate - 1L) / rate);
        return $"강등 예상 {hours / 24f:0.#}일";
    }

    private string BuildMentorshipText(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId)
    {
        if (careers == null) return string.Empty;
        CareerMentorshipSnapshot student = careers.Mentorships.FirstOrDefault(
            value => value.StudentCharacterId.Equals(characterId)
                && value.ProficiencyId == proficiencyId);
        if (student.StudentCharacterId.IsValid)
        {
            return $"멘토 {student.MentorCharacterId.Value}";
        }
        int count = careers.Mentorships.Count(value =>
            value.MentorCharacterId.Equals(characterId)
            && value.ProficiencyId == proficiencyId);
        return count > 0 ? $"학생 {count}명" : string.Empty;
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

    private IReadOnlyList<CharacterDetailedStatRow> BuildProficiencyEffects(
        CharacterActor actor,
        string characterId)
    {
        CharacterId id = new(characterId);
        Dictionary<CharacterProficiencyId, long> current = new();
        Dictionary<CharacterProficiencyId, float> learning = new();
        if (id.IsValid && proficiencies != null && calendar != null)
        {
            foreach (CharacterProficiencySnapshot value in
                     proficiencies.GetAllProficiencies(id, calendar.AbsoluteHour))
            {
                current[value.ProficiencyId] = value.CurrentMilliExperience;
                learning[value.ProficiencyId] = value.LearningMultiplier;
            }
        }

        IReadOnlyList<CharacterStartingProficiencyExperience> starts =
            actor?.Progression?.GrowthState?.startingProficiencies;
        if (starts != null)
        {
            foreach (CharacterStartingProficiencyExperience value in starts)
            {
                CharacterProficiencyId proficiencyId = new(value?.proficiencyId);
                if (proficiencyId.IsValid && !current.ContainsKey(proficiencyId))
                {
                    current[proficiencyId] = Math.Max(0, value.experience)
                        * ProficiencyProgressionRules.MilliPerExperience;
                    learning[proficiencyId] =
                        CharacterProficiencySpecializationRules
                            .NormalizeSerializedMultiplier(
                                value.learningMultiplier);
                }
            }
        }

        return BuiltInCharacterProficiencyIds.All.Select(proficiencyId =>
        {
            current.TryGetValue(proficiencyId, out long experience);
            float learningMultiplier = learning.TryGetValue(
                proficiencyId,
                out float resolvedLearning)
                ? resolvedLearning
                : CharacterProficiencySpecializationRules
                    .NeutralLearningMultiplier;
            CharacterProficiencyEffectSnapshot effects =
                ProficiencyProgressionRules.ResolveEffects(experience);
            string detail = proficiencyId == BuiltInCharacterProficiencyIds.MeleeCombat
                ? $"근접 공격·방패 방어·제압 성능 {effects.QualityScore:0.#}"
                : proficiencyId == BuiltInCharacterProficiencyIds.RangedCombat
                    ? $"활·석궁·화기 공격과 엄호 성능 {effects.QualityScore:0.#}"
                    : $"작업 속도 ×{effects.WorkSpeedMultiplier:0.##} · 완성 품질 {effects.QualityScore:0.#} · 사고 위험 ×{effects.AccidentMultiplier:0.##}";
            detail += $" · 학습 x{learningMultiplier:0.00}";
            return Row(
                "effect:" + proficiencyId.Value,
                StartPartyPreparationPresentation.ProficiencyLabel(proficiencyId),
                $"{experience / ProficiencyProgressionRules.MilliPerExperience:N0} XP",
                detail);
        }).ToArray();
    }

    private IReadOnlyList<CharacterDetailedStatRow> BuildWork(CharacterActor actor)
    {
        if (performance == null)
            throw new InvalidOperationException("Character performance query was not injected.");
        return performance.EvaluateDomain(
                actor,
                CharacterPerformanceFormulaDomain.Work)
            .Select(PerformanceRow)
            .ToArray();
    }

    private IReadOnlyList<CharacterDetailedStatRow> BuildCombat(
        CharacterActor actor,
        string characterId)
    {
        List<CharacterDetailedStatRow> rows = new();
        if (performance == null)
            throw new InvalidOperationException("Character performance query was not injected.");
        CharacterFunctionalCapacitySnapshot capacitySnapshot =
            performance.GetFunctionalCapacities(actor);
        rows.AddRange(capacitySnapshot.Values
            .OrderBy(value => value.CapacityId)
            .Select(value => Row(
                value.StableId,
                CharacterFunctionalCapacityIds.GetDisplayName(value.CapacityId),
                value.IsApplicable ? $"{value.Value * 100f:0.#}%" : "N/A",
                value.IsApplicable
                    ? string.Join(" · ", value.Contributions.Select(item => item.Detail))
                    : value.NonApplicableReason)));
        // Functional capacities are presented once in the health/anatomy group.
        // Combat keeps only actual combat equipment and performance results.
        rows.Clear();
        rows.AddRange(performance.EvaluateDomain(
                actor,
                CharacterPerformanceFormulaDomain.Combat)
            .Select(PerformanceRow));
        equipment.TryGetActiveWeapon(characterId, out CombatWeaponSnapshot weapon);
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();
        string weaponName = equipment.TryGetDefinition(
                weapon.DefinitionId,
                out CombatEquipmentDefinitionSO weaponDefinition)
            ? weaponDefinition.DisplayName
            : CharacterDetailedStatsTextFormatter.Get(
                "CharacterSummary.Common.None");
        rows.Add(Row(
            "combat:weapon",
            CharacterDetailedStatsTextFormatter.Get(
                "CharacterSummary.Detailed.Combat.PrimaryWeapon"),
            weaponName,
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

        if (armor.Length == 0 && !shield.IsValid)
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
        if (performance == null)
            throw new InvalidOperationException("Character performance query was not injected.");
        CharacterFunctionalCapacitySnapshot capacitySnapshot =
            performance.GetFunctionalCapacities(actor);
        rows.AddRange(capacitySnapshot.Values
            .OrderBy(value => value.CapacityId)
            .Select(value => Row(
                value.StableId,
                CharacterFunctionalCapacityIds.GetDisplayName(value.CapacityId),
                value.IsApplicable ? $"{value.Value * 100f:0.#}%" : "N/A",
                value.IsApplicable
                    ? string.Join(" / ", value.Contributions.Select(item =>
                        $"{item.SourceKind}:{item.SourceId} {item.Detail}"))
                    : value.NonApplicableReason)));
        rows.AddRange(performance.EvaluateDomain(
                actor,
                CharacterPerformanceFormulaDomain.Medical)
            .Select(PerformanceRow));
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
        if (performance == null)
            throw new InvalidOperationException("Character performance query was not injected.");
        List<CharacterDetailedStatRow> rows = performance.EvaluateDomain(
                actor,
                CharacterPerformanceFormulaDomain.Composite)
            .Select(PerformanceRow)
            .ToList();
        rows.AddRange(performance.EvaluateDomain(
                actor,
                CharacterPerformanceFormulaDomain.SurvivalSocial)
            .Select(PerformanceRow));
        rows.AddRange(BuildGameplayEffectTrace(actor));
        return rows;
    }

    private static CharacterDetailedStatRow PerformanceRow(
        CharacterPerformanceSnapshot snapshot)
    {
        if (!snapshot.IsApplicable)
        {
            return Row(
                snapshot.FormulaId,
                snapshot.DisplayName,
                "실행 불가",
                snapshot.Failure?.Message ?? "성능 공식을 계산할 수 없습니다.");
        }
        string value = snapshot.ResultChannel == CharacterPerformanceResultChannel.AccidentRisk
            ? $"×{snapshot.Value:0.###} 위험"
            : $"×{snapshot.Value:0.###}";
        string bottleneck = float.IsPositiveInfinity(snapshot.BottleneckCap)
            ? "없음"
            : snapshot.BottleneckCap.ToString("0.###");
        string contributions = string.Join(" / ", (snapshot.Contributions
                ?? Array.Empty<CharacterPerformanceContributionTrace>())
            .Select(item => $"{item.SourceKind}:{item.SourceId} {item.Detail}")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal));
        return Row(
            snapshot.FormulaId,
            snapshot.DisplayName,
            value,
            $"기능 ×{snapshot.FunctionalCapacityFactor:0.###} · 숙련 ×{snapshot.ProficiencyFactor:0.###} "
            + $"· 효과 ×{snapshot.GameplayEffectFactor:0.###} · 문맥 ×{snapshot.ContextFactor:0.###} "
            + $"· 병목 상한 {bottleneck}"
            + (contributions.Length > 0 ? $" / {contributions}" : string.Empty));
    }

    private IReadOnlyList<CharacterDetailedStatRow> BuildGameplayEffectTrace(
        CharacterActor actor)
    {
        if (gameplayEffects == null)
            return Array.Empty<CharacterDetailedStatRow>();

        IReadOnlyList<IGameplayEffectSource> sources =
            gameplayEffects.CollectSources(actor);
        string[] targets = sources
            .SelectMany(source => source.Effects
                ?? Array.Empty<GameplayEffectBinding>())
            .Where(binding => binding?.definition != null)
            .Select(binding => binding.definition.TargetId)
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToArray();
        if (targets.Length == 0)
            return Array.Empty<CharacterDetailedStatRow>();

        Dictionary<string, float> neutralBases = targets.ToDictionary(
            target => target,
            target => target.StartsWith("proficiency:", StringComparison.Ordinal)
                || target.EndsWith(":quality-score", StringComparison.Ordinal)
                || target.EndsWith(":offset", StringComparison.Ordinal)
                    ? 0f
                    : 1f,
            StringComparer.Ordinal);
        CharacterDerivedStatsSnapshot snapshot = gameplayEffects.Project(
            actor,
            neutralBases);
        return snapshot.Contributions.Select((contribution, index) =>
        {
            string status = contribution.Suppressed
                ? $"억제: {contribution.SuppressionReason}"
                : $"적용 {contribution.AppliedValue:0.###}";
            return Row(
                $"gameplay-effect:{index}:{contribution.BindingId}",
                CharacterDetailedStatsTextFormatter.GameplayEffectTargetLabel(
                    contribution.Definition?.TargetId ?? contribution.EffectId),
                contribution.Source.ToString(),
                $"{contribution.EffectId} · 작성값 {contribution.AuthoredValue:0.###} · {status}");
        }).ToArray();
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
