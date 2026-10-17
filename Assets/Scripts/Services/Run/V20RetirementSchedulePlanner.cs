using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

internal sealed class V20RetirementSchedulePlanner : IRetirementScheduleContentQuery
{
    private readonly IContentRequirementEvaluator requirements;
    private readonly ICareerPersistence careers;

    internal V20RetirementSchedulePlanner(
        IContentRequirementEvaluator requirements,
        ICareerPersistence careers)
    {
        this.requirements = requirements;
        this.careers = careers;
    }

    public bool CanScheduleRetirement(CharacterId characterId)
    {
        if (!IsEligibleLivingElder(characterId)) return false;
        CharacterCareerAggregate candidate = careers.PrepareRestore(
            careers.Capture());
        return candidate.CanScheduleRetirement(characterId, out _);
    }

    internal bool TryPrepareRetirementSchedule(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        out RetirementSchedulePlan plan,
        out DomainFailure failure)
    {
        plan = default;
        failure = DomainFailure.None;
        V20ResolvedEventResult[] scheduled = resolutions
            .Where(value => value.Effects.Any(effect => effect != null
                && effect.kind == V20ContentEffectKind.RetirementSchedule))
            .ToArray();
        if (scheduled.Length == 0)
            return true;
        if (request.Kind != ContentResolutionRequestKind.SocietyChoice
            || scheduled.Length != 1)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        V20ResolvedEventResult resolution = scheduled[0];
        V20ContentEffect[] effects = resolution.Effects
            .Where(effect => effect != null
                && effect.kind == V20ContentEffectKind.RetirementSchedule)
            .ToArray();
        string[] participants = resolution.ParticipantCharacterIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (effects.Length != 1 || participants.Length != 1)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        CharacterId characterId = new(participants[0]);
        V20ContentEffect effect = effects[0];
        int decisionAbsoluteDay = Math.Max(1, request.AbsoluteDay);
        int dueAbsoluteDay;
        try
        {
            dueAbsoluteDay = checked(decisionAbsoluteDay + effect.durationDays);
        }
        catch (OverflowException)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        CharacterCareerAggregate candidate = careers.PrepareRestore(
            careers.Capture());
        if (effect.durationDays < 0
            || !IsEligibleLivingElder(characterId)
            || !candidate.TryScheduleRetirement(
                characterId,
                resolution.DefinitionId,
                resolution.ResolutionId,
                decisionAbsoluteDay,
                dueAbsoluteDay,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }
        plan = new RetirementSchedulePlan(
            characterId,
            resolution.DefinitionId,
            resolution.ResolutionId,
            decisionAbsoluteDay,
            dueAbsoluteDay);
        return true;
    }

    internal bool IsEligibleLivingElder(CharacterId characterId) =>
        requirements.IsLivingCharacterAtStage(
            characterId,
            CharacterLifeStage.Elder);

    internal readonly struct RetirementSchedulePlan
    {
        public RetirementSchedulePlan(
            CharacterId characterId,
            string eventId,
            string choiceId,
            int decisionAbsoluteDay,
            int dueAbsoluteDay)
        {
            CharacterId = characterId;
            EventId = eventId;
            ChoiceId = choiceId;
            DecisionAbsoluteDay = decisionAbsoluteDay;
            DueAbsoluteDay = dueAbsoluteDay;
            IsRequired = true;
        }

        public CharacterId CharacterId { get; }
        public string EventId { get; }
        public string ChoiceId { get; }
        public int DecisionAbsoluteDay { get; }
        public int DueAbsoluteDay { get; }
        public bool IsRequired { get; }
    }
}
