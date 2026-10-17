using static V20CampaignIdentity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;

internal static class V20ContentDefinitionRules
{

    internal static bool RequirementsSatisfied(V20ContentRequirementSet requirements, RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot == null) return false;
        requirements ??= new V20ContentRequirementSet();
        return (requirements.research ?? new()).All(value => snapshot.CompletedResearchIds.Contains(value.researchNumericId))
            && (requirements.requiredFlags ?? new()).All(snapshot.WorldFlags.Contains)
            && (requirements.excludedFlags ?? new()).All(value => !snapshot.WorldFlags.Contains(value))
            && (requirements.worldMetrics ?? new()).All(value => snapshot.WorldMetrics.TryGetValue(value.kind, out float actual) && actual >= value.minimumValue)
            && (requirements.items ?? new()).All(value => snapshot.ItemQuantities.TryGetValue(value.itemDefinitionId, out int quantity) && quantity >= value.amount)
            && (requirements.facilities ?? new()).All(value => snapshot.FacilityCounts.TryGetValue(FacilityRequirementKey(value), out int count) && count >= value.minimumCount)
            && (requirements.factions ?? new()).All(value => snapshot.Factions.TryGetValue(value.factionId, out FactionCampaignStateSaveData faction)
                && faction.rapport >= value.minimumRapport
                && faction.grievance <= value.maximumGrievance
                && faction.obligationTokens >= value.minimumObligationTokens)
            && ((requirements.characters?.Count ?? 0) == 0
                || snapshot.EligibleCharacterCount >= requirements.characters.Count);
    }

    internal static LifeEventOccurrencePolicy RequireOccurrencePolicy(
        LifeEventDefinitionSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (!Enum.IsDefined(
                typeof(LifeEventOccurrencePolicy),
                definition.occurrencePolicy))
            throw new InvalidOperationException(
                $"Life event '{definition.StableId}' has invalid occurrence policy "
                + $"'{definition.occurrencePolicy}'.");
        return definition.occurrencePolicy;
    }

    internal static IReadOnlyList<V20ChoiceDefinition> GetChoices(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.choices,
            ServiceIncidentDefinitionSO value => value.responses,
            _ => Array.Empty<V20ChoiceDefinition>()
        };

    internal static bool IsEmergency(V20AuthoredContentSO definition) =>
        definition is LifeEventDefinitionSO { emergency: true }
        || definition is ServiceIncidentDefinitionSO;

    internal static string FacilityRequirementKey(V20FacilityRequirement value) =>
        !string.IsNullOrWhiteSpace(value.buildingDefinitionId)
            ? value.buildingDefinitionId
            : $"capability:{value.capabilityId}";
    internal static void RequireUniqueIds(
        IEnumerable<string> source,
        string label)
    {
        string[] values = (source ?? Array.Empty<string>()).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException($"{label} ids are invalid.");
    }


    internal static string CategoryKey(V20AuthoredContentSO definition) =>
        definition switch
        {
            LifeEventDefinitionSO value =>
                $"event-category:life:{value.category}",
            GuestRequestDefinitionSO value =>
                $"event-category:guest:{value.kind}",
            ServiceIncidentDefinitionSO value =>
                $"event-category:incident:{value.kind}",
            _ => $"event-category:definition:{definition?.StableId}"
        };

    internal static IReadOnlyList<V20ContentEffect> WithConsumedRequirements(
        IEnumerable<V20ContentEffect> effects,
        V20ContentRequirementSet requirements)
    {
        List<V20ContentEffect> result = (effects ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null)
            .ToList();
        foreach (IGrouping<string, V20ItemAmountRequirement> group in
                 (requirements?.items ?? new List<V20ItemAmountRequirement>())
                 .Where(value => value != null
                     && value.consume
                     && !string.IsNullOrWhiteSpace(value.itemDefinitionId))
                 .GroupBy(value => value.itemDefinitionId.Trim(), StringComparer.Ordinal))
        {
            result.Add(new V20ContentEffect
            {
                kind = V20ContentEffectKind.ItemConsume,
                targetId = group.Key,
                amount = group.Sum(value => Math.Max(0, value.amount))
            });
        }
        return result.AsReadOnly();
    }

    internal static double WeightedDefinitionRoll(
        V20DailyEventContext context,
        V20AuthoredContentSO definition)
    {
        float weight = context.ParticipantCharacterIds
            .Select(characterId => ContentWeight(context, characterId, definition))
            .DefaultIfEmpty(1f)
            .Max();
        return EventRoll(context, definition.StableId) / Math.Max(0.1f, weight);
    }

    internal static double WeightedParticipantRoll(
        V20DailyEventContext context,
        V20AuthoredContentSO definition,
        string characterId) =>
        PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{context.AbsoluteDay}:{definition.StableId}:{characterId}")
        / Math.Max(0.1f, ContentWeight(context, characterId, definition));

    internal static float ContentWeight(
        V20DailyEventContext context,
        string characterId,
        V20AuthoredContentSO definition)
    {
        if (!context.ParticipantContentWeights.TryGetValue(
                characterId,
                out Dictionary<string, float> weights))
            return 1f;
        float result = 1f;
        foreach (string key in ContentWeightKeys(definition))
            if (weights.TryGetValue(key, out float value)) result *= value;
        return Math.Clamp(result, 0.1f, 10f);
    }

    internal static IEnumerable<string> ContentWeightKeys(
        V20AuthoredContentSO definition)
    {
        yield return definition.StableId;
        switch (definition)
        {
            case LifeEventDefinitionSO life:
                yield return "life-event";
                yield return KebabCase(life.category.ToString());
                break;
            case GuestRequestDefinitionSO:
                yield return "guest-request";
                break;
            case ServiceIncidentDefinitionSO:
                yield return "service-incident";
                break;
        }
    }

    internal static string KebabCase(string value) => string.Concat(
        (value ?? string.Empty).Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? "-" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));

    internal static bool CanStartSocietyDefinition(
        ISocietyEventCatalog catalog,
        V20AuthoredContentSO definition,
        V20DailyEventContext context,
        SocietyEventWorldSaveData state,
        bool dailyCadenceCandidate = false)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (definition is LifeEventDefinitionSO lifeEvent
            && RequireOccurrencePolicy(lifeEvent) != LifeEventOccurrencePolicy.DailyCadence
            && dailyCadenceCandidate)
            return false;
        if (state.activeEvents.Any(value => string.Equals(
                value.definitionId,
                definition.StableId,
                StringComparison.Ordinal))
            || state.cooldowns.Any(value => string.Equals(
                value.definitionId,
                definition.StableId,
                StringComparison.Ordinal))
            || state.activeEvents.Any(value => string.Equals(
                CategoryKey(((ISocietyEventCatalog)catalog).Require(
                    value.definitionId)),
                CategoryKey(definition),
                StringComparison.Ordinal))
            || state.cooldowns.Any(value => string.Equals(
                value.definitionId,
                CategoryKey(definition),
                StringComparison.Ordinal)))
            return false;
        return RequirementsSatisfied(TriggerRequirements(definition), context.Requirements);
    }

    internal static bool CanAssignLifeEvent(
        LifeEventDefinitionSO definition,
        string participantCharacterId,
        V20DailyEventContext context,
        SocietyEventWorldSaveData state)
    {
        if (definition == null
            || context == null
            || string.IsNullOrWhiteSpace(participantCharacterId)
            || RequiresRetirementSchedule(definition)
                && !context.RetirementEligibleParticipantIds.Contains(
                    participantCharacterId))
            return false;
        return definition.frequencyRule switch
        {
            LifeEventFrequencyRule.Repeatable => true,
            LifeEventFrequencyRule.OncePerRun =>
                !state.completedOnceEventIds.Contains(
                    definition.StableId,
                    StringComparer.Ordinal),
            LifeEventFrequencyRule.OncePerGeneration =>
                !state.recurrenceKeys.Contains(
                    GenerationRecurrenceKey(
                        definition.StableId,
                        context.Generation),
                    StringComparer.Ordinal),
            LifeEventFrequencyRule.OncePerCharacter =>
                !state.recurrenceKeys.Contains(
                    CharacterRecurrenceKey(
                        definition.StableId,
                        participantCharacterId),
                    StringComparer.Ordinal),
            _ => false
        };
    }

    internal static bool RequiresRetirementSchedule(
        LifeEventDefinitionSO definition) =>
        (definition?.choices ?? new List<V20ChoiceDefinition>())
            .Where(value => value != null)
            .SelectMany(value => value.effects ?? new List<V20ContentEffect>())
            .Any(value => value != null
                && value.kind == V20ContentEffectKind.RetirementSchedule);

    internal static V20ActiveEventSaveData CreateObservedLifeEvent(
        IFactionStoryCatalog catalog,
        ObservedLifeEventSourceKind sourceKind,
        string sourceOperationId,
        string canonicalPayload,
        int absoluteDay,
        int generation,
        string participantCharacterId,
        LifeEventDefinitionSO definition,
        bool resolveAutomatically = true)
    {
        string[] factionIds = catalog.Arcs.Select(value => value.factionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (factionIds.Length == 0)
            throw new InvalidOperationException(
                "Observed life-event creation requires at least one faction campaign.");
        (ExperienceEventRiskTier riskTier, string riskReason) =
            SocietyRisk(definition);
        SocietyEventRiskContract.RequireValid(
            riskTier,
            riskReason,
            definition.StableId);
        V20ActiveEventSaveData occurrence = new()
        {
            instanceId = ObservedLifeOccurrenceInstanceId(
                sourceKind,
                sourceOperationId),
            definitionId = definition.StableId,
            startedAbsoluteDay = absoluteDay,
            deadlineAbsoluteDay = resolveAutomatically
                ? absoluteDay
                : absoluteDay + Math.Max(1, definition.responseDeadlineDays),
            generation = generation,
            deterministicRoll = PersistentEntityId.GetStableHash32(canonicalPayload),
            contextFactionId = factionIds[0],
            participantCharacterIds = new List<string>
            {
                participantCharacterId
            },
            riskTier = riskTier,
            riskReason = riskReason,
            resolved = resolveAutomatically,
            resolutionId = resolveAutomatically ? "automatic" : string.Empty
        };
        if (resolveAutomatically)
        {
            FreezeSocietyTerminalOutcome(
                occurrence,
                definition,
                "자동 처리",
                RequireAuthoredNarrative(definition, null),
                definition.automaticEffects);
        }
        return occurrence;
    }

    internal static V20ActiveEventSaveData CreateEvent(
        IFactionStoryCatalog catalog,
        string definitionId,
        V20DailyEventContext context,
        int durationDays,
        IEnumerable<string> participants,
        V20AuthoredContentSO societyDefinition = null)
    {
        string[] sorted = (participants ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        uint roll = EventRoll(context, definitionId, sorted);
        string[] factionIds = catalog.Arcs.Select(value => value.factionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (factionIds.Length == 0)
            throw new InvalidOperationException(
                "V20 event creation requires at least one faction campaign.");
        ExperienceEventRiskTier riskTier = ExperienceEventRiskTier.None;
        string riskReason = string.Empty;
        if (societyDefinition != null)
        {
            (riskTier, riskReason) = SocietyRisk(societyDefinition);
            SocietyEventRiskContract.RequireValid(
                riskTier,
                riskReason,
                societyDefinition.StableId);
        }
        return new V20ActiveEventSaveData
        {
            instanceId = $"event:{context.AbsoluteDay}:{definitionId}:{roll:X8}",
            definitionId = definitionId,
            startedAbsoluteDay = context.AbsoluteDay,
            deadlineAbsoluteDay = context.AbsoluteDay + Math.Max(0, durationDays),
            generation = Math.Max(0, context.Generation),
            deterministicRoll = roll,
            contextFactionId = factionIds[(int)(roll % (uint)factionIds.Length)],
            participantCharacterIds = sorted.ToList(),
            riskTier = riskTier,
            riskReason = riskReason
        };
    }

    internal static (ExperienceEventRiskTier Tier, string Reason) SocietyRisk(
        V20AuthoredContentSO definition) => definition switch
        {
            GuestRequestDefinitionSO value =>
                (value.riskTier, value.riskReason),
            LifeEventDefinitionSO value =>
                (value.riskTier, value.riskReason),
            ServiceIncidentDefinitionSO value =>
                (value.riskTier, value.riskReason),
            _ => throw new InvalidOperationException(
                $"'{definition?.StableId}' is not a society-event definition.")
        };

    internal static void FreezeSocietyTerminalOutcome(
        V20ActiveEventSaveData occurrence,
        V20AuthoredContentSO definition,
        string actionLabel,
        string authoredNarrative,
        IEnumerable<V20ContentEffect> effects)
    {
        if (occurrence == null
            || definition == null
            || string.IsNullOrWhiteSpace(occurrence.resolutionId)
            || string.IsNullOrWhiteSpace(actionLabel)
            || !string.Equals(
                actionLabel,
                actionLabel.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(authoredNarrative)
            || !string.Equals(
                authoredNarrative,
                authoredNarrative.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Society event '{occurrence?.instanceId}' has no canonical terminal narrative.");
        }
        SocietyEventRiskContract.RequireValid(
            occurrence.riskTier,
            occurrence.riskReason,
            occurrence.instanceId);
        occurrence.terminalActionLabel = actionLabel;
        occurrence.terminalOutcomeNarrative = authoredNarrative;
        occurrence.terminalEffectKinds = (effects
                ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null)
            .Select(value => value.kind)
            .Distinct()
            .OrderBy(value => value)
            .ToList();
        if (occurrence.terminalEffectKinds.Any(value =>
                value == V20ContentEffectKind.None
                || !Enum.IsDefined(typeof(V20ContentEffectKind), value)))
        {
            throw new InvalidOperationException(
                $"Society event '{occurrence.instanceId}' has an invalid terminal effect type.");
        }
    }

    internal static string RequireAuthoredNarrative(
        V20AuthoredContentSO definition,
        V20ChoiceDefinition choice)
    {
        string narrative = choice == null
            ? definition?.Description ?? string.Empty
            : choice.outcomeText?.Trim() ?? string.Empty;
        if (narrative.Length == 0)
        {
            throw new InvalidOperationException(
                $"Society event '{definition?.StableId}' has no authored terminal narrative.");
        }
        return narrative;
    }

    internal static string GuestResolutionLabel(string resolutionId) =>
        resolutionId switch
        {
            "fulfill" => "요청 이행",
            "decline" => "요청 거절",
            "expired" => "기한 만료",
            _ => throw new InvalidOperationException(
                $"Unknown guest-request terminal resolution '{resolutionId}'.")
        };

    internal static V20ResolvedEventResult CreateSocietyResolvedResult(
        V20ActiveEventSaveData occurrence,
        V20AuthoredContentSO definition,
        IReadOnlyList<V20ContentEffect> effects) =>
        new(
            definition.StableId,
            occurrence.resolutionId,
            effects,
            occurrence.participantCharacterIds.AsReadOnly(),
            occurrence.contextFactionId,
            occurrence.instanceId,
            definition.DisplayName,
            occurrence.riskTier,
            occurrence.riskReason,
            occurrence.terminalActionLabel,
            occurrence.terminalOutcomeNarrative,
            occurrence.terminalEffectKinds.AsReadOnly(),
            occurrence.hasObservedMealIncident
                ? occurrence.observedMealIncident?.observedCause
                    ?? string.Empty
                : string.Empty);

    internal static uint EventRoll(
        V20DailyEventContext context,
        string definitionId,
        IEnumerable<string> participants = null) =>
        PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{definitionId}:{context.AbsoluteDay}:"
            + string.Join(",", (participants ?? context.ParticipantCharacterIds)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(value => value, StringComparer.Ordinal)));

    internal static T SelectDeterministic<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        V20DailyEventContext context,
        string salt) => values
        .OrderBy(value => PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{salt}:{id(value)}:{context.AbsoluteDay}"))
        .ThenBy(id, StringComparer.Ordinal)
        .First();

    internal static V20ContentRequirementSet TriggerRequirements(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.triggerRequirements,
            GuestRequestDefinitionSO value => value.serviceRequirements,
            ServiceIncidentDefinitionSO value => value.triggerRequirements,
            _ => new V20ContentRequirementSet()
        };

    internal static IReadOnlyList<V20ContentEffect> ExpirationEffects(
        V20AuthoredContentSO definition) => definition switch
        {
            GuestRequestDefinitionSO value => value.failureEffects,
            _ => Array.Empty<V20ContentEffect>()
        };

    internal static int DeadlineDays(V20AuthoredContentSO definition) =>
        definition switch
        {
            LifeEventDefinitionSO value => value.responseDeadlineDays,
            GuestRequestDefinitionSO value => value.deadlineDays,
            _ => 3
        };

    internal static string GenerationRecurrenceKey(
        string definitionId,
        int generation) =>
        $"{definitionId}:generation:{Math.Max(0, generation)}";
}
