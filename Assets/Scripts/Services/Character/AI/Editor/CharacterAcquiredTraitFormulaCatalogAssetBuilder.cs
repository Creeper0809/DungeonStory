using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>One-time authoring migration from the approved legacy trait definitions.
/// It is never called by runtime generation and may only be run in a controlled verify window.</summary>
public static class CharacterAcquiredTraitFormulaCatalogAssetBuilder
{
    private static readonly IReadOnlyDictionary<string, string[]> DrawbackBindingsByModule =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["acquired-trait:module:night-vigil"] = new[]
            {
                "acquired-trait:module:night-vigil:effect:1"
            }
        };

    [MenuItem("DungeonStory/Narrative Formula/Build AcquiredTrait Catalog")]
    public static void Build()
    {
        CharacterAcquiredTraitSettingsSO settings = RequireSingleSettings();
        CharacterAcquiredTraitModuleSO[] modules = RequireModules();
        Undo.RecordObject(settings, "Build acquired-trait formula policy");
        foreach (CharacterAcquiredTraitModuleSO module in modules)
        {
            Undo.RecordObject(module, "Build acquired-trait formula descriptor");
            string capabilityId = "acquired-trait:" + module.ModuleId;
            module.ApplyDrawbackBindingAuthoring(
                DrawbackBindingsByModule.TryGetValue(module.ModuleId, out string[] drawbackIds)
                    ? drawbackIds : Array.Empty<string>());
            module.ApplyFormulaAuthoring(capabilityId, BuildDescriptor(module, capabilityId));
            EditorUtility.SetDirty(module);
        }
        settings.ApplyFormulaAuthoring(
            BuildPolicy(settings),
            BuildDrawbackDefinitions(modules));
        settings.FormulaPolicy.catalogSha256 = ComputeCanonicalCatalogSha256(settings, modules);
        Validate(settings, modules);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("Acquired-trait narrative formula catalog authored and validated: "
            + settings.FormulaPolicy.catalogSha256);
    }

    // This is intentionally formula-only: ApplyFormulaAuthoring on the settings
    // and modules does not rewrite their gate, effect, conflict, or display data.
    [MenuItem("DungeonStory/Narrative Formula/Apply AcquiredTrait Formula Metadata Only")]
    public static void ApplyFormulaMetadataOnlyFromMenu() => Build();

    [MenuItem("DungeonStory/Narrative Formula/Validate AcquiredTrait Catalog")]
    public static void ValidateMenu() => Validate(RequireSingleSettings(), RequireModules());

    public static void PopulateForEditorTest(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        CharacterAcquiredTraitModuleSO[] modules = (source
                ?? throw new ArgumentNullException(nameof(source)))
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        foreach (CharacterAcquiredTraitModuleSO module in modules)
        {
            string capabilityId = "acquired-trait:" + module.ModuleId;
            module.ApplyDrawbackBindingAuthoring(
                DrawbackBindingsByModule.TryGetValue(module.ModuleId, out string[] drawbackIds)
                    ? drawbackIds : Array.Empty<string>());
            module.ApplyFormulaAuthoring(
                capabilityId,
                BuildDescriptor(module, capabilityId));
        }
        settings.ApplyFormulaAuthoring(
            BuildPolicy(settings),
            BuildEditorTestDrawbackDefinitions(modules));
        settings.FormulaPolicy.catalogSha256 =
            ComputeCanonicalCatalogSha256(settings, modules);
        Validate(settings, modules);
    }

    public static void Validate(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        CharacterAcquiredTraitModuleSO[] modules = (source ?? throw new ArgumentNullException(nameof(source)))
            .Where(value => value != null).OrderBy(value => value.ModuleId, StringComparer.Ordinal).ToArray();
        if (modules.Length == 0 || modules.Select(value => value.ModuleId).Distinct(StringComparer.Ordinal).Count() != modules.Length)
            throw new InvalidOperationException("Acquired-trait formula catalog requires distinct authored modules.");
        settings.RequireFormulaPolicy();
        NarrativeFormulaCapabilityDescriptor[] descriptors = modules.Select(value => value.RequireFormulaDescriptor()).ToArray();
        if (descriptors.Select(value => value.CapabilityId).Distinct(StringComparer.Ordinal).Count() != descriptors.Length)
            throw new InvalidOperationException("Acquired-trait formula catalog contains duplicate capability IDs.");
        foreach (NarrativeFormulaCapabilityDescriptor descriptor in descriptors)
        {
            NarrativeFormulaQuantizedRange magnitude = descriptor.RequireRange(
                NarrativeFormulaParameterIds.Magnitude);
            if (magnitude.ToDecimal(magnitude.MinimumUnits) != 1m
                || magnitude.ToDecimal(magnitude.MaximumUnits) <= 1m
                || magnitude.StepCount < 2)
                throw new InvalidOperationException(
                    $"Acquired-trait capability '{descriptor.CapabilityId}' requires a dynamic potency scale beginning at 1.");
            if (descriptor.ForbiddenSynergies.Any(value => descriptors.All(other => other.CapabilityId != value)))
                throw new InvalidOperationException($"Acquired-trait capability '{descriptor.CapabilityId}' has an unknown forbidden synergy.");
        }
        CharacterAcquiredTraitDrawbackCapabilityDefinition[] drawbacks =
            settings.DrawbackCapabilities.ToArray();
        if (drawbacks.Length < 3
            || drawbacks.Select(value => value.DrawbackId)
                .Distinct(StringComparer.Ordinal).Count() != drawbacks.Length)
            throw new InvalidOperationException(
                "Acquired-trait formula v2 requires distinct compositional drawbacks.");
        foreach (CharacterAcquiredTraitDrawbackCapabilityDefinition drawbackDefinition in drawbacks)
        {
            IReadOnlyList<string> errors = drawbackDefinition.ValidateDefinition();
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(" | ", errors));
            NarrativeFormulaCapabilityDescriptor descriptor =
                drawbackDefinition.RequireFormulaDescriptor();
            NarrativeFormulaQuantizedRange magnitude = descriptor.RequireRange(
                NarrativeFormulaParameterIds.Magnitude);
            if (magnitude.ToDecimal(magnitude.MinimumUnits) != 1m
                || magnitude.StepCount < drawbackDefinition.MaximumCredit)
                throw new InvalidOperationException(
                    $"Acquired-trait drawback '{drawbackDefinition.DrawbackId}' lacks severity tiers for its credit cap.");
        }
        string expected = ComputeCanonicalCatalogSha256(settings, modules);
        if (!string.Equals(expected, settings.FormulaPolicy.RequireCatalogSha256(), StringComparison.Ordinal))
            throw new InvalidOperationException("Acquired-trait formula catalog SHA does not match current authoring.");
    }

    public static string ComputeCanonicalCatalogSha256(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source)
    {
        if (settings?.FormulaPolicy == null) throw new ArgumentNullException(nameof(settings));
        StringBuilder text = new();
        CharacterAcquiredTraitFormulaPolicyDefinition policy = settings.FormulaPolicy;
        Append(text, "version", policy.formulaVersion);
        Append(text, "base", policy.baseBudget);
        Append(text, "scale", policy.powerScale);
        Append(text, "softCap", policy.softCapK.ToString("R", CultureInfo.InvariantCulture));
        Append(text, "importance", policy.minimumImportance.ToString("R", CultureInfo.InvariantCulture)
            + ":" + policy.maximumImportance.ToString("R", CultureInfo.InvariantCulture));
        NarrativeFormulaDrawbackCreditPolicyDefinition drawback = policy.drawbackCredit
            ?? throw new InvalidOperationException("Acquired-trait drawback-credit policy is missing.");
        Append(text, "drawback", string.Join(":",
            drawback.playerChoiceMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture),
            drawback.automaticMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture),
            drawback.absoluteMaximumCredit,
            drawback.requireNegativeEvidenceForAutomatic));
        foreach (float weight in policy.milestoneWeights ?? new List<float>())
            Append(text, "milestone", weight.ToString("R", CultureInfo.InvariantCulture));
        Append(text, "context", policy.triggerFrequencyUnits + ":" + policy.guaranteedProc + ":" + policy.targetCount);
        foreach (CharacterAcquiredTraitModuleSO module in (source ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
                     .Where(value => value != null).OrderBy(value => value.ModuleId, StringComparer.Ordinal))
        {
            CharacterSkillCapabilityFormulaDefinition formula = module.Formula
                ?? throw new InvalidOperationException($"Module '{module.ModuleId}' has no formula authoring.");
            Append(text, "module", module.ModuleId + ":" + module.CapabilityId);
            foreach (string bindingId in module.DrawbackBindingIds.OrderBy(value => value, StringComparer.Ordinal))
                Append(text, "drawbackBinding", bindingId);
            foreach (GameplayEffectBinding binding in module.Effects
                         .Where(value => value != null)
                         .OrderBy(value => value.bindingId, StringComparer.Ordinal))
                Append(text, "moduleEffect", string.Join(":",
                    binding.bindingId,
                    binding.definition?.EffectId ?? string.Empty,
                    binding.definition?.TargetId ?? string.Empty,
                    binding.definition?.Operation.ToString() ?? string.Empty,
                    binding.value.ToString("R", CultureInfo.InvariantCulture),
                    binding.condition?.ConditionId ?? string.Empty));
            foreach (CharacterAcquiredTraitSpecialReactionDefinition reaction
                     in module.SpecialReactions.Where(value => value != null)
                         .OrderBy(value => value.ReactionId, StringComparer.Ordinal))
            {
                Append(text, "moduleReaction", string.Join(":",
                    reaction.ReactionId,
                    reaction.Trigger,
                    reaction.OwnerRole,
                    reaction.Condition,
                    reaction.Action,
                    reaction.BaseValue.ToString("R", CultureInfo.InvariantCulture),
                    reaction.DurationDays,
                    reaction.CooldownDays,
                    reaction.MaximumTriggersPerDay,
                    reaction.MaximumLifetimeTriggers,
                    reaction.DisplayLabel));
            }
            Append(text, "cost", string.Join(":", formula.baseCost, formula.triggerFrequencyCostPerUnit,
                formula.guaranteedProcCost, formula.areaCostPerExtraTarget, formula.multiEffectCostPerExtraEffect));
            foreach (string key in Ordered(formula.affinityKeys)) Append(text, "affinity", key);
            foreach (string key in Ordered(formula.conflictGroups)) Append(text, "conflict", key);
            foreach (string key in Ordered(formula.forbiddenSynergies)) Append(text, "forbidden", key);
            foreach (CharacterSkillFormulaAxisDefinition axis in (formula.parameterRanges ?? new List<CharacterSkillFormulaAxisDefinition>())
                         .OrderBy(value => value.parameterId, StringComparer.Ordinal))
                Append(text, "axis", string.Join(":", axis.parameterId, axis.minimumUnits, axis.maximumUnits,
                    axis.quantumUnits, axis.decimalPlaces, axis.costPerQuantum));
        }
        foreach (CharacterAcquiredTraitDrawbackCapabilityDefinition drawbackDefinition
                 in settings.DrawbackCapabilities.OrderBy(
                     value => value.DrawbackId, StringComparer.Ordinal))
        {
            Append(text, "traitDrawback", string.Join(":",
                drawbackDefinition.DrawbackId,
                drawbackDefinition.DisplayName,
                drawbackDefinition.MaximumCredit,
                drawbackDefinition.CapabilityId));
            foreach (CharacterNarrativeDomain domain in drawbackDefinition.DomainAffinities
                         .OrderBy(value => value))
                Append(text, "traitDrawbackDomain", domain);
            foreach (string group in Ordered(drawbackDefinition.ConflictGroups))
                Append(text, "traitDrawbackConflict", group);
            foreach (GameplayEffectBinding binding in drawbackDefinition.Effects
                         .OrderBy(value => value.bindingId, StringComparer.Ordinal))
            {
                Append(text, "traitDrawbackEffect", string.Join(":",
                    binding.bindingId,
                    binding.definition?.EffectId ?? string.Empty,
                    binding.definition?.TargetId ?? string.Empty,
                    binding.definition?.Operation.ToString() ?? string.Empty,
                    binding.value.ToString("R", CultureInfo.InvariantCulture),
                    binding.condition?.ConditionId ?? string.Empty));
            }
            CharacterSkillCapabilityFormulaDefinition formula =
                drawbackDefinition.Formula;
            Append(text, "traitDrawbackCost", string.Join(":",
                formula.baseCost, formula.triggerFrequencyCostPerUnit,
                formula.guaranteedProcCost, formula.areaCostPerExtraTarget,
                formula.multiEffectCostPerExtraEffect));
            foreach (CharacterSkillFormulaAxisDefinition axis in formula.parameterRanges
                         .OrderBy(value => value.parameterId, StringComparer.Ordinal))
                Append(text, "traitDrawbackAxis", string.Join(":",
                    axis.parameterId, axis.minimumUnits, axis.maximumUnits,
                    axis.quantumUnits, axis.decimalPlaces, axis.costPerQuantum));
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(text.ToString()).Substring("sha256:".Length);
    }

    private static CharacterAcquiredTraitFormulaPolicyDefinition BuildPolicy(CharacterAcquiredTraitSettingsSO settings)
    {
        int[] budgets = settings.ManifestationGates.Select(value => value.Budget).OrderBy(value => value).ToArray();
        if (budgets.Length == 0) throw new InvalidOperationException("Acquired-trait formula seed requires manifestation gates.");
        return new CharacterAcquiredTraitFormulaPolicyDefinition
        {
            formulaVersion = CharacterAcquiredTraitFormulaGeneration.ModuleSelectionFormulaVersion,
            baseBudget = budgets[0],
            powerScale = budgets[budgets.Length - 1] - budgets[0],
            softCapK = 8f,
            minimumImportance = 0f,
            maximumImportance = 4f,
            milestoneWeights = new List<float> { 1f, 1.5f, 2f, 3f, 5f },
            drawbackCredit = new NarrativeFormulaDrawbackCreditPolicyDefinition
            {
                playerChoiceMaximumBudgetFraction = 0.35f,
                automaticMaximumBudgetFraction = 0.35f,
                absoluteMaximumCredit = 3,
                requireNegativeEvidenceForAutomatic = true
            },
            triggerFrequencyUnits = 1,
            guaranteedProc = true,
            targetCount = 1
        };
    }

    private static CharacterSkillCapabilityFormulaDefinition BuildDescriptor(
        CharacterAcquiredTraitModuleSO module,
        string capabilityId)
    {
        if (module.Effects.Count == 0 || module.Effects.Any(value => value?.definition == null))
            throw new InvalidOperationException(
                $"Acquired-trait module '{module.ModuleId}' requires complete effect bindings before formula authoring.");
        return new CharacterSkillCapabilityFormulaDefinition
        {
            appliedParameterIds = new List<string>
            {
                NarrativeFormulaParameterIds.Magnitude
            },
            // A special reaction is a second mechanical output of the same
            // selected module. Charge it before magnitude allocation so a
            // triggered action never rides on a scalar projection for free.
            baseCost = Math.Max(0,
                module.Cost - 1 + module.SpecialReactions.Count),
            triggerFrequencyCostPerUnit = 1,
            guaranteedProcCost = 1,
            areaCostPerExtraTarget = 1,
            multiEffectCostPerExtraEffect = 1,
            pairSynergyCosts = new List<CharacterSkillPairSynergyCostDefinition>(),
            narrativeAffinity = 1f,
            affinityKeys = module.DomainAffinities.Select(value => value.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            conflictGroups = (module.ConflictGroups.Count == 0
                ? new[] { "capability:" + capabilityId }
                : module.ConflictGroups).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            forbiddenSynergies = new List<string>(),
            formatterId = capabilityId,
            applicatorId = capabilityId,
            parameterRanges = new List<CharacterSkillFormulaAxisDefinition>
            {
                AxisRange(NarrativeFormulaParameterIds.Magnitude, 10000, 14000, 1000, 4),
                Axis(NarrativeFormulaParameterIds.Duration, 0, 0),
                Axis(NarrativeFormulaParameterIds.Count, 1, 0),
                Axis(NarrativeFormulaParameterIds.TargetCount, 1, 0)
            }
        };
    }

    private static IReadOnlyList<CharacterAcquiredTraitDrawbackCapabilityDefinition>
        BuildDrawbackDefinitions(IReadOnlyCollection<CharacterAcquiredTraitModuleSO> modules)
    {
        CharacterAcquiredTraitModuleSO[] values = (modules
                ?? throw new ArgumentNullException(nameof(modules)))
            .Where(value => value != null).ToArray();
        return new[]
        {
            Drawback("acquired-trait:drawback:battle-shock", "전투 후유증",
                "격전의 기억이 남아 평상시 전투력이 흔들립니다.",
                new[] { CharacterNarrativeDomain.Combat, CharacterNarrativeDomain.Injury },
                "drawback:combat", 2,
                FindBinding(values, GameplayEffectTargetIds.CombatPower), 0.96f),
            Drawback("acquired-trait:drawback:lingering-dread", "남은 불안",
                "위험한 기억이 가라앉지 않아 부정적인 기분이 오래갑니다.",
                new[] { CharacterNarrativeDomain.Mood, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Combat },
                "drawback:mood", 2,
                FindBinding(values, GameplayEffectTargetIds.NegativeMoodDuration), 1.08f),
            Drawback("acquired-trait:drawback:overstrain", "무리한 버릇",
                "위기를 힘으로 넘기는 버릇 때문에 위험 작업 사고율이 높아집니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival },
                "drawback:risk", 2,
                FindBinding(values, GameplayEffectTargetIds.AccidentChance), 1.06f),
            Drawback("acquired-trait:drawback:social-withdrawal", "관계 회피",
                "갈등의 기억 때문에 관계를 회복하는 속도가 느려집니다.",
                new[] { CharacterNarrativeDomain.Relationship, CharacterNarrativeDomain.Mood },
                "drawback:social", 2,
                FindBinding(values, GameplayEffectTargetIds.RelationshipRecovery), 0.94f),
            Drawback("acquired-trait:drawback:tunnel-vision", "편협한 몰입",
                "한 방식에 매달리는 습관 때문에 연구 속도가 느려집니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse },
                "drawback:research", 2,
                FindBinding(values, GameplayEffectTargetIds.ResearchSpeed), 0.96f),
            Drawback("acquired-trait:drawback:daylight-fatigue", "낮의 피로",
                "밤에 익숙해진 몸 때문에 낮 작업 속도가 느려집니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival },
                "drawback:work-cycle", 2,
                FindBinding(values, GameplayEffectTargetIds.WorkSpeed, "shift:day"), 0.98f),
            Drawback("acquired-trait:drawback:heavy-step", "무거운 걸음",
                "부상과 무리한 이동의 흔적 때문에 이동 속도가 느려집니다.",
                new[] { CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Expedition },
                "drawback:mobility", 2,
                LoadBinding("effect_character_move-speed_multiply.asset"), 0.96f),
            Drawback("acquired-trait:drawback:voracious", "커진 소모",
                "결핍을 버틴 뒤 평소 소비량이 늘어납니다.",
                new[] { CharacterNarrativeDomain.Need, CharacterNarrativeDomain.Survival },
                "drawback:consumption", 2,
                LoadBinding("effect_character_consumption_multiply.asset"), 1.06f),
            Drawback("acquired-trait:drawback:work-fatigue", "쉽게 쌓이는 피로",
                "무리한 작업의 후유증으로 피로가 더 빨리 쌓입니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Injury },
                "drawback:fatigue", 2,
                LoadBinding("effect_character_fatigue-rate_multiply.asset"), 1.08f),
            Drawback("acquired-trait:drawback:slow-recovery", "더딘 회복",
                "상처를 안고 버틴 대가로 회복 속도가 느려집니다.",
                new[] { CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Combat },
                "drawback:recovery", 2,
                LoadBinding("effect_character_recovery-speed_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:frailty", "남은 쇠약",
                "치명적인 위기를 넘긴 뒤 최대 체력이 줄어듭니다.",
                new[] { CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Combat },
                "drawback:vitality", 2,
                LoadBinding("effect_character_maximum-health_multiply.asset"), 0.96f),
            Drawback("acquired-trait:drawback:stunted-learning", "굳어진 습관",
                "익숙한 방식만 되풀이해 작업 경험을 얻는 속도가 느려집니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse },
                "drawback:learning", 2,
                LoadBinding("effect_character_earned-work-xp_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:cold-sensitive", "추위 민감",
                "혹한을 견딘 후유증으로 추위의 영향을 더 크게 받습니다.",
                new[] { CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Expedition,
                    CharacterNarrativeDomain.Injury },
                "drawback:temperature", 2,
                LoadBinding("effect_character_cold-exposure_multiply.asset"), 1.08f),
            Drawback("acquired-trait:drawback:heat-sensitive", "더위 민감",
                "고열 환경을 견딘 후유증으로 더위의 영향을 더 크게 받습니다.",
                new[] { CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Expedition,
                    CharacterNarrativeDomain.Injury },
                "drawback:temperature", 2,
                LoadBinding("effect_character_heat-exposure_multiply.asset"), 1.08f),
            Drawback("acquired-trait:drawback:worn-reflex", "무뎌진 반사",
                "거듭 공격을 받아 회피 확률이 낮아집니다.",
                new[] { CharacterNarrativeDomain.Combat, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Invasion },
                "drawback:evasion", 2,
                LoadBinding("effect_combat_evasion-chance_add-flat.asset"), -0.02f),
            Drawback("acquired-trait:drawback:rough-craft", "거친 손끝",
                "서두르는 제작 습관 때문에 제작 품질 점수가 낮아집니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse },
                "drawback:craft", 2,
                FindBinding(values, GameplayEffectTargetIds.CraftQualityScore), -2f),
            Drawback("acquired-trait:drawback:wasteful-salvage", "성긴 해체",
                "급하게 회수하는 버릇 때문에 해체 수율이 낮아집니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Expedition },
                "drawback:salvage", 2,
                LoadBinding("effect_work_salvage-yield_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:weak-haul", "줄어든 운반력",
                "반복된 과로 때문에 한 번에 옮기는 양이 줄어듭니다.",
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Injury },
                "drawback:haul", 2,
                LoadBinding("effect_work_haul-capacity_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:sickly", "약해진 저항",
                "질병을 거듭 앓은 뒤 질병 저항이 낮아집니다.",
                new[] { CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Need },
                "drawback:disease", 2,
                LoadBinding("effect_character_disease-resistance_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:slow-disease-recovery", "긴 병치레",
                "오랜 병치레의 흔적으로 질병 회복 속도가 느려집니다.",
                new[] { CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Need },
                "drawback:disease", 2,
                LoadBinding("effect_character_disease-recovery-speed_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:weak-immunity-gain", "더딘 면역",
                "감염에서 회복해도 면역을 얻는 속도가 느립니다.",
                new[] { CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Need },
                "drawback:immunity", 2,
                LoadBinding("effect_character_immunity-gain_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:weak-immunity-retention", "옅은 면역",
                "얻은 면역이 오래 유지되지 않습니다.",
                new[] { CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Need },
                "drawback:immunity", 2,
                LoadBinding("effect_character_immunity-retention_multiply.asset"), 0.94f),
            Drawback("acquired-trait:drawback:unsafe-appetite", "불안한 식성",
                "결핍 뒤 음식을 가리지 않는 버릇으로 식중독 확률이 높아집니다.",
                new[] { CharacterNarrativeDomain.Need, CharacterNarrativeDomain.Survival },
                "drawback:food-safety", 2,
                LoadBinding("effect_character_food-poisoning-chance_multiply.asset"), 1.08f)
        };
    }

    private static IReadOnlyList<CharacterAcquiredTraitDrawbackCapabilityDefinition>
        BuildEditorTestDrawbackDefinitions(
            IReadOnlyCollection<CharacterAcquiredTraitModuleSO> modules)
    {
        GameplayEffectBinding source = (modules
                ?? throw new ArgumentNullException(nameof(modules)))
            .Where(value => value != null)
            .SelectMany(value => value.Effects)
            .FirstOrDefault(value => value?.definition != null)
            ?? throw new InvalidOperationException(
                "Acquired-trait editor fixture requires one effect definition.");
        return new[]
        {
            Drawback("acquired-trait:drawback:qa-fatigue", "검증 피로",
                "부정 작업 경험이 피로로 남습니다.",
                new[] { CharacterNarrativeDomain.Work },
                "drawback:qa-fatigue", 2, source, 0.99f),
            Drawback("acquired-trait:drawback:qa-hesitation", "검증 주저",
                "부정 작업 경험이 주저함으로 남습니다.",
                new[] { CharacterNarrativeDomain.Work },
                "drawback:qa-hesitation", 2, source, 0.98f),
            Drawback("acquired-trait:drawback:qa-strain", "검증 무리",
                "부정 작업 경험이 무리한 버릇으로 남습니다.",
                new[] { CharacterNarrativeDomain.Work },
                "drawback:qa-strain", 2, source, 0.97f)
        };
    }

    private static CharacterAcquiredTraitDrawbackCapabilityDefinition Drawback(
        string id,
        string displayName,
        string description,
        IEnumerable<CharacterNarrativeDomain> domains,
        string conflictGroup,
        int maximumCredit,
        GameplayEffectBinding source,
        float harmfulValue)
    {
        string capabilityId = id + ":severity";
        return new CharacterAcquiredTraitDrawbackCapabilityDefinition
        {
            drawbackId = id,
            displayName = displayName,
            description = description,
            domainAffinities = domains.OrderBy(value => value).ToList(),
            conflictGroups = new List<string> { conflictGroup },
            maximumCredit = maximumCredit,
            effects = new List<GameplayEffectBinding>
            {
                new()
                {
                    bindingId = id + ":effect:0",
                    definition = source.definition,
                    condition = source.condition,
                    value = harmfulValue
                }
            },
            capabilityId = capabilityId,
            formula = BuildDrawbackDescriptor(capabilityId, domains, conflictGroup)
        };
    }

    private static CharacterSkillCapabilityFormulaDefinition BuildDrawbackDescriptor(
        string capabilityId,
        IEnumerable<CharacterNarrativeDomain> domains,
        string conflictGroup) => new()
    {
        appliedParameterIds = new List<string> { NarrativeFormulaParameterIds.Magnitude },
        baseCost = 0,
        triggerFrequencyCostPerUnit = 0,
        guaranteedProcCost = 0,
        areaCostPerExtraTarget = 0,
        multiEffectCostPerExtraEffect = 0,
        pairSynergyCosts = new List<CharacterSkillPairSynergyCostDefinition>(),
        narrativeAffinity = 1f,
        affinityKeys = domains.Select(value => value.ToString())
            .OrderBy(value => value, StringComparer.Ordinal).ToList(),
        conflictGroups = new List<string> { conflictGroup },
        forbiddenSynergies = new List<string>(),
        formatterId = capabilityId,
        applicatorId = capabilityId,
        parameterRanges = new List<CharacterSkillFormulaAxisDefinition>
        {
            AxisRange(NarrativeFormulaParameterIds.Magnitude, 10000, 13000, 1000, 4),
            Axis(NarrativeFormulaParameterIds.Duration, 0, 0),
            Axis(NarrativeFormulaParameterIds.Count, 1, 0),
            Axis(NarrativeFormulaParameterIds.TargetCount, 1, 0)
        }
    };

    private static GameplayEffectBinding FindBinding(
        IEnumerable<CharacterAcquiredTraitModuleSO> modules,
        string targetId,
        string conditionId = null)
    {
        GameplayEffectBinding binding = modules.SelectMany(value => value.Effects)
            .Where(value => value?.definition != null
                && string.Equals(value.definition.TargetId, targetId,
                    StringComparison.Ordinal)
                && (conditionId == null || string.Equals(
                    value.condition?.ConditionId, conditionId,
                    StringComparison.Ordinal)))
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .FirstOrDefault();
        return binding ?? throw new InvalidOperationException(
            $"No acquired-trait effect binding supplies target '{targetId}' and condition '{conditionId ?? "<any>"}'.");
    }

    private static GameplayEffectBinding LoadBinding(string assetName)
    {
        const string root = "Assets/Resources/SO/V26/Effects/Definitions/";
        GameplayEffectDefinitionSO definition = AssetDatabase.LoadAssetAtPath<GameplayEffectDefinitionSO>(
            root + assetName);
        if (definition == null)
            throw new InvalidOperationException(
                $"Acquired-trait drawback effect asset is missing: {root + assetName}");
        return new GameplayEffectBinding
        {
            bindingId = "drawback-source:" + definition.EffectId,
            definition = definition,
            value = definition.Operation == GameplayEffectOperation.Multiply ? 1f : 0f
        };
    }

    private static CharacterSkillFormulaAxisDefinition Axis(string id, long value, int decimals) => new()
    {
        parameterId = id, minimumUnits = value, maximumUnits = value,
        quantumUnits = 1, decimalPlaces = decimals, costPerQuantum = 1
    };

    private static CharacterSkillFormulaAxisDefinition AxisRange(
        string id, long minimum, long maximum, long quantum, int decimals) => new()
    {
        parameterId = id,
        minimumUnits = minimum,
        maximumUnits = maximum,
        quantumUnits = quantum,
        decimalPlaces = decimals,
        costPerQuantum = 1
    };

    private static CharacterAcquiredTraitSettingsSO RequireSingleSettings()
    {
        string[] paths = AssetDatabase.FindAssets("t:CharacterAcquiredTraitSettingsSO")
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        if (paths.Length != 1) throw new InvalidOperationException($"Expected exactly one acquired-trait settings asset, found {paths.Length}.");
        return AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitSettingsSO>(paths[0])
            ?? throw new InvalidOperationException("Acquired-trait settings asset could not load.");
    }

    private static CharacterAcquiredTraitModuleSO[] RequireModules() => AssetDatabase
        .FindAssets("t:CharacterAcquiredTraitModuleSO")
        .Select(AssetDatabase.GUIDToAssetPath)
        .Select(AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitModuleSO>)
        .Where(value => value != null).ToArray();

    private static IEnumerable<string> Ordered(IEnumerable<string> values) => (values ?? Array.Empty<string>())
        .OrderBy(value => value, StringComparer.Ordinal);
    private static void Append(StringBuilder value, string key, object content) => value.Append(key).Append('=').Append(content).Append('\n');
}
