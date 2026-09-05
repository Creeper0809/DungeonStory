using System;
using System.Collections.Generic;
using System.Linq;

public interface ICharacterPerformanceQuery
{
    CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(CharacterActor actor);
    CharacterPerformanceSnapshot Evaluate(
        CharacterActor actor,
        string formulaId,
        float contextFactor = 1f,
        GameplayEffectContext effectContext = null);
    CharacterPerformanceSnapshot Evaluate(
        CharacterActor actor,
        string formulaId,
        CharacterPerformanceEvaluationContext context);
    CharacterPerformanceSnapshot EvaluateWork(
        CharacterActor actor,
        WorkTypeId workTypeId,
        CharacterPerformanceResultChannel resultChannel,
        CharacterPerformanceEvaluationContext context);
    IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
        CharacterActor actor,
        CharacterPerformanceFormulaDomain domain);
}

public sealed class CharacterPerformanceEvaluationContext
{
    public float ContextFactor { get; set; } = 1f;
    public GameplayEffectContext GameplayEffectContext { get; set; }
    public string PrimaryProficiencyOverride { get; set; } = string.Empty;
    public string SecondaryProficiencyOverride { get; set; } = string.Empty;
}

public sealed class CharacterPerformanceFormulaCatalog
{
    private readonly IReadOnlyDictionary<string, CharacterPerformanceFormulaDefinitionSO> formulas;
    private readonly IReadOnlyDictionary<string, CharacterPerformanceFormulaDefinitionSO> workFormulas;

    public CharacterPerformanceFormulaCatalog(IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        CharacterPerformanceFormulaDefinitionSO[] authored = content
            .GetAll<CharacterPerformanceFormulaDefinitionSO>()
            .Where(value => value != null)
            .ToArray();
        List<string> errors = authored
            .SelectMany(value => value.ValidateDefinition()
                .Select(error => $"{value.name}: {error}"))
            .ToList();
        IGrouping<string, CharacterPerformanceFormulaDefinitionSO>[] duplicates = authored
            .GroupBy(value => value.FormulaId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();
        errors.AddRange(duplicates.Select(group =>
            $"Duplicate performance formula id '{group.Key}'."));
        IGrouping<string, CharacterPerformanceFormulaDefinitionSO>[] duplicateWorkMappings = authored
            .Where(value => value.ExecutionWorkTypeId.Length > 0)
            .GroupBy(
                value => WorkKey(value.ExecutionWorkTypeId, value.ResultChannel),
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();
        errors.AddRange(duplicateWorkMappings.Select(group =>
            $"Duplicate work performance mapping '{group.Key}': "
            + string.Join(",", group.Select(value => value.FormulaId))));
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Character performance formula catalog is invalid:\n"
                + string.Join("\n", errors));
        }
        formulas = authored.ToDictionary(value => value.FormulaId, StringComparer.Ordinal);
        workFormulas = authored
            .Where(value => value.ExecutionWorkTypeId.Length > 0)
            .ToDictionary(
                value => WorkKey(value.ExecutionWorkTypeId, value.ResultChannel),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<CharacterPerformanceFormulaDefinitionSO> All =>
        formulas.Values.OrderBy(value => value.FormulaId, StringComparer.Ordinal).ToArray();

    public CharacterPerformanceFormulaDefinitionSO Require(string formulaId)
    {
        string normalized = formulaId?.Trim() ?? string.Empty;
        return formulas.TryGetValue(normalized, out CharacterPerformanceFormulaDefinitionSO value)
            ? value
            : throw new KeyNotFoundException(
                $"Character performance formula '{normalized}' is not authored.");
    }

    public CharacterPerformanceFormulaDefinitionSO RequireWork(
        WorkTypeId workTypeId,
        CharacterPerformanceResultChannel resultChannel)
    {
        if (!workTypeId.IsValid)
            throw new ArgumentException(
                "A valid work type id is required.",
                nameof(workTypeId));
        string key = WorkKey(workTypeId.Value, resultChannel);
        return workFormulas.TryGetValue(
                key,
                out CharacterPerformanceFormulaDefinitionSO value)
            ? value
            : throw new KeyNotFoundException(
                $"No authored performance formula maps work '{workTypeId.Value}' "
                + $"to channel '{resultChannel}'.");
    }

    private static string WorkKey(
        string workTypeId,
        CharacterPerformanceResultChannel resultChannel) =>
        $"{workTypeId?.Trim()}|{(int)resultChannel}";
}

public sealed class CharacterPerformanceQuery : ICharacterPerformanceQuery
{
    private const float DefaultRequiredThreshold = 0.10f;

    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly ICharacterProficiencyQuery proficiencies;
    private readonly IGameCalendar calendar;
    private readonly CharacterDerivedStatsSnapshotProjector gameplayEffects;
    private readonly CharacterPerformanceFormulaCatalog formulas;

    public CharacterPerformanceQuery(
        IAnatomyHealthRuntime anatomy,
        IAnatomyProfileCatalog anatomyProfiles,
        ICharacterProficiencyQuery proficiencies,
        IGameCalendar calendar,
        CharacterDerivedStatsSnapshotProjector gameplayEffects,
        CharacterPerformanceFormulaCatalog formulas)
    {
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.proficiencies = proficiencies
            ?? throw new ArgumentNullException(nameof(proficiencies));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.gameplayEffects = gameplayEffects
            ?? throw new ArgumentNullException(nameof(gameplayEffects));
        this.formulas = formulas ?? throw new ArgumentNullException(nameof(formulas));
    }

    public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(
        CharacterActor actor)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        AnatomyHealthSnapshot anatomySnapshot = anatomy.GetAnatomySnapshot(actor);
        if (!anatomyProfiles.TryGet(
                anatomySnapshot.ProfileId,
                out AnatomyProfileDefinition profile))
        {
            throw new InvalidOperationException(
                $"Anatomy profile '{anatomySnapshot.ProfileId}' is not available for character "
                + $"'{actor.Identity?.PersistentId}'.");
        }

        Dictionary<string, AnatomyNodeHealthState> nodeStates = anatomySnapshot.Nodes
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.nodeId))
            .ToDictionary(value => value.nodeId, StringComparer.Ordinal);
        List<CharacterFunctionalCapacityValue> values = new();
        foreach (CharacterFunctionalCapacityId capacityId in Enum
                     .GetValues(typeof(CharacterFunctionalCapacityId))
                     .Cast<CharacterFunctionalCapacityId>())
        {
            AnatomyFunction function = ToAnatomyFunction(capacityId);
            AnatomyNodeDefinition[] producers = profile.Nodes
                .Where(value => (value.ExpandedFunctions & function) != 0)
                .ToArray();
            if (producers.Length == 0)
            {
                if (capacityId == CharacterFunctionalCapacityId.ArcaneConduction)
                {
                    throw new InvalidOperationException(
                        $"Anatomy profile '{profile.ProfileId}' has no arcane-conduction producer. "
                        + "Arcane conduction must be numeric for every species.");
                }
                if (!profile.TryGetNotApplicableReason(capacityId, out string reason))
                {
                    throw new InvalidOperationException(
                        $"Anatomy profile '{profile.ProfileId}' has neither a producer nor "
                        + $"an explicit N/A reason for {CharacterFunctionalCapacityIds.GetStableId(capacityId)}.");
                }
                values.Add(new CharacterFunctionalCapacityValue(
                    capacityId,
                    false,
                    0f,
                    reason,
                    Array.Empty<CharacterPerformanceContributionTrace>()));
                continue;
            }

            float weightedTotal = 0f;
            float totalWeight = 0f;
            List<CharacterPerformanceContributionTrace> trace = new();
            foreach (AnatomyNodeDefinition producer in producers)
            {
                float weight = Math.Max(0.01f, producer.CapacityWeight);
                float efficiency = nodeStates.TryGetValue(
                        producer.NodeId,
                        out AnatomyNodeHealthState node)
                    ? node.FunctionalEfficiency
                    : 0f;
                weightedTotal += efficiency * weight;
                totalWeight += weight;
                trace.Add(new CharacterPerformanceContributionTrace
                {
                    SourceKind = "anatomy-node",
                    SourceId = producer.NodeId,
                    TargetId = CharacterFunctionalCapacityIds.GetStableId(capacityId),
                    AuthoredValue = weight,
                    AppliedValue = efficiency * weight,
                    Detail = $"{producer.DisplayName}: efficiency {efficiency:0.###} × weight {weight:0.###}"
                });
            }
            float value = totalWeight > 0f ? weightedTotal / totalWeight : 0f;
            if (capacityId == CharacterFunctionalCapacityId.PowerCirculation)
            {
                value = anatomySnapshot.PowerCirculation;
                trace.Add(new CharacterPerformanceContributionTrace
                {
                    SourceKind = "health-state",
                    SourceId = "blood-loss",
                    TargetId = CharacterFunctionalCapacityIds.PowerCirculation,
                    AuthoredValue = 1f,
                    AppliedValue = value,
                    Detail = "Power circulation includes current blood-loss pressure."
                });
            }
            string targetId = CharacterFunctionalCapacityIds.GetStableId(capacityId);
            GameplayEffectProjectionResult projection = gameplayEffects.ProjectValue(
                actor,
                targetId,
                value);
            value = projection.Value;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new InvalidOperationException(
                    $"Capacity projection '{targetId}' produced invalid value {value} "
                    + $"for character '{actor.Identity?.PersistentId}'.");
            trace.AddRange(projection.Contributions.Select(contribution =>
                new CharacterPerformanceContributionTrace
                {
                    SourceKind = contribution.Source.Kind.ToString(),
                    SourceId = contribution.Source.SourceId,
                    TargetId = targetId,
                    AuthoredValue = contribution.AuthoredValue,
                    AppliedValue = contribution.AppliedValue,
                    Detail = contribution.Suppressed
                        ? $"suppressed: {contribution.SuppressionReason}"
                        : contribution.EffectId
                }));
            values.Add(new CharacterFunctionalCapacityValue(
                capacityId,
                true,
                value,
                string.Empty,
                trace));
        }
        return new CharacterFunctionalCapacitySnapshot(values);
    }

    public CharacterPerformanceSnapshot Evaluate(
        CharacterActor actor,
        string formulaId,
        float contextFactor = 1f,
        GameplayEffectContext effectContext = null) => Evaluate(
            actor,
            formulaId,
            new CharacterPerformanceEvaluationContext
            {
                ContextFactor = contextFactor,
                GameplayEffectContext = effectContext
            });

    public CharacterPerformanceSnapshot Evaluate(
        CharacterActor actor,
        string formulaId,
        CharacterPerformanceEvaluationContext context)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        context ??= new CharacterPerformanceEvaluationContext();
        float contextFactor = context.ContextFactor;
        if (float.IsNaN(contextFactor) || float.IsInfinity(contextFactor)
            || contextFactor < 0f)
            throw new ArgumentOutOfRangeException(nameof(contextFactor));
        CharacterPerformanceFormulaDefinitionSO formula = formulas.Require(formulaId);
        CharacterFunctionalCapacitySnapshot capacities = GetFunctionalCapacities(actor);
        List<CharacterPerformanceContributionTrace> trace = new();
        float weightedTotal = 0f;
        float totalWeight = 0f;
        float bottleneckCap = float.PositiveInfinity;

        foreach (CharacterPerformanceCapacityInput input in formula.CapacityInputs)
        {
            CharacterFunctionalCapacityValue capacity = capacities.Get(input.CapacityId);
            if (!capacity.IsApplicable)
            {
                trace.Add(new CharacterPerformanceContributionTrace
                {
                    SourceKind = "capacity-na",
                    SourceId = capacity.StableId,
                    TargetId = formula.FormulaId,
                    Detail = capacity.NonApplicableReason
                });
                continue;
            }
            if ((input.Role & CharacterPerformanceInputRole.Required) != 0)
            {
                float threshold = input.RequiredThreshold > 0f
                    ? input.RequiredThreshold
                    : DefaultRequiredThreshold;
                if (capacity.Value < threshold)
                {
                    return FailureSnapshot(formula, capacity, threshold, contextFactor, trace);
                }
            }
            if ((input.Role & CharacterPerformanceInputRole.Contribution) != 0
                && input.Weight > 0f)
            {
                weightedTotal += capacity.Value * input.Weight;
                totalWeight += input.Weight;
            }
            if ((input.Role & CharacterPerformanceInputRole.Bottleneck) != 0)
            {
                bottleneckCap = Math.Min(
                    bottleneckCap,
                    0.25f + 0.75f * capacity.Value);
            }
            trace.AddRange(capacity.Contributions);
        }

        if (totalWeight <= 0f)
        {
            throw new InvalidOperationException(
                $"Formula '{formula.FormulaId}' has no applicable weighted capacity input.");
        }
        float weightedCapacity = weightedTotal / totalWeight;
        float rawCapacityFactor = Math.Min(weightedCapacity, bottleneckCap);
        float capacityFactor = UsesInverseCapacity(formula.ResultChannel)
            ? 1f / Math.Max(0.05f, rawCapacityFactor)
            : rawCapacityFactor;
        if (!TryResolveProficiencyFactor(
                actor,
                formula,
                context,
                trace,
                out float proficiencyFactor,
                out CharacterPerformanceFailure proficiencyFailure))
        {
            return new CharacterPerformanceSnapshot
            {
                FormulaId = formula.FormulaId,
                DisplayName = formula.DisplayName,
                ResultChannel = formula.ResultChannel,
                BaseValue = formula.BaseValue,
                ContextFactor = contextFactor,
                FunctionalCapacityFactor = capacityFactor,
                WeightedCapacityValue = weightedCapacity,
                BottleneckCap = bottleneckCap,
                Value = 0f,
                IsApplicable = false,
                Failure = proficiencyFailure,
                Contributions = trace
            };
        }
        float effectFactor = 1f;
        if (!string.IsNullOrWhiteSpace(formula.GameplayEffectTargetId))
        {
            GameplayEffectProjectionResult effect = gameplayEffects.ProjectValue(
                actor,
                formula.GameplayEffectTargetId,
                1f,
                context.GameplayEffectContext);
            effectFactor = effect.Value;
            trace.AddRange(effect.Contributions.Select(value =>
                new CharacterPerformanceContributionTrace
                {
                    SourceKind = value.Source.Kind.ToString(),
                    SourceId = value.Source.SourceId,
                    TargetId = formula.GameplayEffectTargetId,
                    AuthoredValue = value.AuthoredValue,
                    AppliedValue = value.AppliedValue,
                    Detail = value.Suppressed
                        ? $"suppressed: {value.SuppressionReason}"
                        : value.EffectId
                }));
        }
        float finalValue = formula.BaseValue
            * capacityFactor
            * proficiencyFactor
            * effectFactor
            * contextFactor;
        return new CharacterPerformanceSnapshot
        {
            FormulaId = formula.FormulaId,
            DisplayName = formula.DisplayName,
            ResultChannel = formula.ResultChannel,
            BaseValue = formula.BaseValue,
            FunctionalCapacityFactor = capacityFactor,
            ProficiencyFactor = proficiencyFactor,
            GameplayEffectFactor = effectFactor,
            ContextFactor = contextFactor,
            WeightedCapacityValue = weightedCapacity,
            BottleneckCap = bottleneckCap,
            Value = finalValue,
            IsApplicable = true,
            Contributions = trace
        };
    }

    public CharacterPerformanceSnapshot EvaluateWork(
        CharacterActor actor,
        WorkTypeId workTypeId,
        CharacterPerformanceResultChannel resultChannel,
        CharacterPerformanceEvaluationContext context)
    {
        CharacterPerformanceFormulaDefinitionSO formula = formulas.RequireWork(
            workTypeId,
            resultChannel);
        return Evaluate(actor, formula.FormulaId, context);
    }

    public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
        CharacterActor actor,
        CharacterPerformanceFormulaDomain domain) => formulas.All
        .Where(value => value.Domain == domain)
        .Select(value => Evaluate(actor, value.FormulaId))
        .ToArray();

    private bool TryResolveProficiencyFactor(
        CharacterActor actor,
        CharacterPerformanceFormulaDefinitionSO formula,
        CharacterPerformanceEvaluationContext context,
        ICollection<CharacterPerformanceContributionTrace> trace,
        out float factor,
        out CharacterPerformanceFailure failure)
    {
        factor = 1f;
        failure = null;
        if (string.IsNullOrWhiteSpace(formula.PrimaryProficiencyId)) return true;
        if (!TryResolveProficiencyId(
                formula.PrimaryProficiencyId,
                context.PrimaryProficiencyOverride,
                out string primaryId,
                out failure))
            return false;
        float primary = ResolveOneProficiency(
            actor, primaryId, formula.ResultChannel, trace);
        float secondaryWeight = formula.SecondaryProficiencyWeight;
        if (secondaryWeight <= 0f)
        {
            factor = primary;
            return true;
        }
        if (!TryResolveProficiencyId(
                formula.SecondaryProficiencyId,
                context.SecondaryProficiencyOverride,
                out string secondaryId,
                out failure))
            return false;
        float secondary = ResolveOneProficiency(
            actor, secondaryId, formula.ResultChannel, trace);
        factor = primary * (1f - secondaryWeight) + secondary * secondaryWeight;
        return true;
    }

    private static bool TryResolveProficiencyId(
        string authoredId,
        string contextOverride,
        out string resolvedId,
        out CharacterPerformanceFailure failure)
    {
        resolvedId = authoredId?.Trim() ?? string.Empty;
        failure = null;
        if (!resolvedId.StartsWith("selector:", StringComparison.Ordinal))
            return true;
        if (!string.IsNullOrWhiteSpace(contextOverride)
            && contextOverride.StartsWith("proficiency:", StringComparison.Ordinal))
        {
            resolvedId = contextOverride.Trim();
            return true;
        }
        failure = new CharacterPerformanceFailure
        {
            Code = "MissingProficiencyContext",
            Message = $"Performance formula requires '{resolvedId}' from its execution context."
        };
        return false;
    }

    private float ResolveOneProficiency(
        CharacterActor actor,
        string proficiencyId,
        CharacterPerformanceResultChannel resultChannel,
        ICollection<CharacterPerformanceContributionTrace> trace)
    {
        CharacterId characterId = new(actor.Identity?.PersistentId);
        if (!characterId.IsValid)
            throw new InvalidOperationException("Character has no stable id for proficiency projection.");
        CharacterProficiencyId id = new(proficiencyId);
        if (!proficiencies.TryGetProficiency(
                characterId,
                id,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot snapshot))
        {
            throw new InvalidOperationException(
                $"Character '{characterId.Value}' has no proficiency '{proficiencyId}'.");
        }
        CharacterProficiencyEffectSnapshot effects =
            ProficiencyProgressionRules.ResolveEffects(snapshot.CurrentMilliExperience);
        float factor = CharacterPerformanceProficiencyFactorAuthority.Resolve(
            resultChannel,
            effects);
        trace.Add(new CharacterPerformanceContributionTrace
        {
            SourceKind = "proficiency",
            SourceId = proficiencyId,
            TargetId = "performance:proficiency-factor",
            AuthoredValue = snapshot.CurrentExperience,
            AppliedValue = factor,
            Detail = $"{snapshot.Rank}, {snapshot.CurrentExperience} XP"
        });
        return factor;
    }

    private static CharacterPerformanceSnapshot FailureSnapshot(
        CharacterPerformanceFormulaDefinitionSO formula,
        CharacterFunctionalCapacityValue capacity,
        float threshold,
        float contextFactor,
        IReadOnlyList<CharacterPerformanceContributionTrace> trace) => new()
    {
        FormulaId = formula.FormulaId,
        DisplayName = formula.DisplayName,
        ResultChannel = formula.ResultChannel,
        BaseValue = formula.BaseValue,
        ContextFactor = contextFactor,
        Value = 0f,
        IsApplicable = false,
        Failure = new CharacterPerformanceFailure
        {
            Code = "RequiredFunctionalCapacityBelowThreshold",
            CapacityId = capacity.StableId,
            CurrentValue = capacity.Value,
            RequiredValue = threshold,
            Message = $"{CharacterFunctionalCapacityIds.GetDisplayName(capacity.CapacityId)} "
                + $"{capacity.Value:P0} / required {threshold:P0}"
        },
        Contributions = trace.ToArray()
    };

    private static AnatomyFunction ToAnatomyFunction(
        CharacterFunctionalCapacityId capacityId) => capacityId switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => AnatomyFunction.MentalMaintenance,
        CharacterFunctionalCapacityId.VisualDiscernment => AnatomyFunction.VisualDiscernment,
        CharacterFunctionalCapacityId.AuditorySensing => AnatomyFunction.AuditorySensing,
        CharacterFunctionalCapacityId.RespiratoryExchange => AnatomyFunction.RespiratoryExchange,
        CharacterFunctionalCapacityId.PowerCirculation => AnatomyFunction.PowerCirculation,
        CharacterFunctionalCapacityId.IntakeProcessing => AnatomyFunction.IntakeProcessing,
        CharacterFunctionalCapacityId.PurificationProcessing => AnatomyFunction.PurificationProcessing,
        CharacterFunctionalCapacityId.VitalityResponse => AnatomyFunction.VitalityResponse,
        CharacterFunctionalCapacityId.PhysicalPower => AnatomyFunction.PhysicalPower,
        CharacterFunctionalCapacityId.PrecisionManipulation => AnatomyFunction.PrecisionManipulation,
        CharacterFunctionalCapacityId.PhysicalMobility => AnatomyFunction.PhysicalMobility,
        CharacterFunctionalCapacityId.Communication => AnatomyFunction.Communication,
        CharacterFunctionalCapacityId.ArcaneConduction => AnatomyFunction.ArcaneConduction,
        CharacterFunctionalCapacityId.ImmuneDefense => AnatomyFunction.ImmuneDefense,
        _ => throw new ArgumentOutOfRangeException(nameof(capacityId), capacityId, null)
    };

    private static bool UsesInverseCapacity(
        CharacterPerformanceResultChannel channel) => channel is
        CharacterPerformanceResultChannel.AccidentRisk
        or CharacterPerformanceResultChannel.Consumption
        or CharacterPerformanceResultChannel.Exposure;
}
