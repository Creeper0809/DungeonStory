using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public sealed class V21ContentAlertChoiceActionDispatcher :
    IEventAlertChoiceActionDispatcher,
    IEventAlertChoiceActionDispositionDispatcher
{
    private readonly IContentResolutionService content;
    private readonly IV20MilestoneWorldSnapshotQuery world;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private readonly IReproductionCommand reproduction;
    private readonly IFestivalCommand festivals;
    private readonly IAgeTreatmentCommand ageTreatments;
    private readonly ISocialCareCommand socialCare;
    private readonly IDiseaseFieldResponseCommand diseaseResponses;
    private readonly ICertifiedSeedCommand certifiedSeeds;
    private readonly ITraitAnalysisCommand traitAnalysis;

    public V21ContentAlertChoiceActionDispatcher(
        IContentResolutionService content,
        IV20MilestoneWorldSnapshotQuery world,
        IGameCalendar calendar,
        IGameEventBus events,
        IReproductionCommand reproduction,
        IFestivalCommand festivals,
        IAgeTreatmentCommand ageTreatments,
        ISocialCareCommand socialCare,
        IDiseaseFieldResponseCommand diseaseResponses,
        ICertifiedSeedCommand certifiedSeeds,
        ITraitAnalysisCommand traitAnalysis)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.festivals = festivals
            ?? throw new ArgumentNullException(nameof(festivals));
        this.ageTreatments = ageTreatments
            ?? throw new ArgumentNullException(nameof(ageTreatments));
        this.socialCare = socialCare
            ?? throw new ArgumentNullException(nameof(socialCare));
        this.diseaseResponses = diseaseResponses
            ?? throw new ArgumentNullException(nameof(diseaseResponses));
        this.certifiedSeeds = certifiedSeeds
            ?? throw new ArgumentNullException(nameof(certifiedSeeds));
        this.traitAnalysis = traitAnalysis
            ?? throw new ArgumentNullException(nameof(traitAnalysis));
    }

    public bool TryDispatch(string actionId, out DomainFailure failure) =>
        TryDispatch(actionId, out _, out failure);

    public bool TryDispatch(
        string actionId,
        out EventAlertChoiceActionDisposition disposition,
        out DomainFailure failure)
    {
        disposition = EventAlertChoiceActionDisposition.Terminal;
        failure = DomainFailure.None;
        if (!V21ContentAlertActionIds.TryParse(
                actionId,
                out string kind,
                out string first,
                out string second))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        int absoluteDay = Math.Max(1, calendar.Day);
        if (string.Equals(kind, "reproduction-start", StringComparison.Ordinal))
        {
            bool useTreatment = string.Equals(
                second,
                "treatment",
                StringComparison.Ordinal);
            if (!useTreatment
                && !string.Equals(second, "standard", StringComparison.Ordinal)
                && !string.Equals(second, "start", StringComparison.Ordinal))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            return reproduction.TryStart(first, useTreatment, out failure);
        }
        if (string.Equals(kind, "festival", StringComparison.Ordinal))
        {
            string[] details = second.Split(',');
            string festivalAction = details[0];
            int occurrenceYear = calendar.Year;
            bool occurrenceBound = details.Length == 2;
            if (details.Length == 2
                && (!int.TryParse(details[1], out occurrenceYear)
                    || occurrenceYear <= 0))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            if (details.Length > 2)
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            if (string.Equals(festivalAction, "skip", StringComparison.Ordinal))
            {
                return festivals.Schedule(
                    new FestivalScheduleRequest
                    {
                        ActionId = actionId.Trim(),
                        FestivalId = first,
                        OccurrenceYear = occurrenceYear,
                        Decline = true
                    },
                    out _,
                    out failure);
            }
            if (!string.Equals(festivalAction, "resolve", StringComparison.Ordinal))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            FestivalScheduleRequest schedule = new()
            {
                ActionId = actionId.Trim(),
                FestivalId = first,
                OccurrenceYear = occurrenceYear,
                ParticipantIds = world.LivingCharacters
                    .Select(CharacterPersistentIdentity.Require)
                    .OrderBy(value => value.Value, StringComparer.Ordinal)
                    .ToArray()
            };
            if (!festivals.Schedule(
                    schedule,
                    out FestivalPreparedOrder order,
                    out failure))
                return false;
            if (!occurrenceBound)
                return festivals.Resolve(order, out failure);
            disposition = EventAlertChoiceActionDisposition.AcceptedPending;
            return true;
        }
        if (string.Equals(kind, "age-treatment", StringComparison.Ordinal))
        {
            string[] details = second.Split(',');
            if (details.Length != 2
                || !int.TryParse(details[0], out int rawTreatment)
                || !Enum.IsDefined(typeof(AgeTreatmentKind), rawTreatment))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            AgeTreatmentOrderRequest treatment = new(
                new CharacterId(first),
                (AgeTreatmentKind)rawTreatment,
                preferredDoctorId: string.Empty,
                facilityInstanceId: details[1]);
            return ageTreatments.TryCreateOrder(
                treatment,
                out _,
                out failure);
        }
        if (string.Equals(kind, "social-care", StringComparison.Ordinal))
        {
            CharacterId subjectId = new(first);
            if (string.Equals(second, "funeral", StringComparison.Ordinal))
            {
                return socialCare.TryHoldFuneral(
                    actionId,
                    subjectId,
                    world.LivingCharacters
                        .Select(CharacterPersistentIdentity.Require)
                        .OrderBy(value => value.Value, StringComparer.Ordinal)
                        .ToArray(),
                    facilityInstanceId: string.Empty,
                    out failure);
            }
            if (string.Equals(second, "counsel", StringComparison.Ordinal))
            {
                return socialCare.TryCounsel(actionId, subjectId, out failure);
            }
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (string.Equals(kind, "disease-response", StringComparison.Ordinal))
        {
            string[] details = second.Split(',');
            if (details.Length != 3)
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            return diseaseResponses.TryApply(
                new CharacterId(first),
                details[0],
                details[1],
                details[2],
                out failure);
        }
        if (string.Equals(kind, "certified-seed", StringComparison.Ordinal))
        {
            return certifiedSeeds.TryPlan(
                actionId,
                first,
                second,
                out failure);
        }
        if (string.Equals(kind, "trait-analysis", StringComparison.Ordinal))
        {
            return traitAnalysis.TryAnalyze(
                new CharacterId(first),
                out _,
                out failure);
        }

        ContentResolutionRequest request = new()
        {
            ActionId = actionId.Trim(),
            AbsoluteDay = absoluteDay,
            Requirements = world.Build(absoluteDay)
        };
        switch (kind)
        {
            case "society":
                request.Kind = ContentResolutionRequestKind.SocietyChoice;
                request.InstanceId = first;
                request.ChoiceId = second;
                break;
            case "faction-chapter":
                request.Kind = ContentResolutionRequestKind.FactionChapterChoice;
                request.FactionId = first;
                request.ChoiceId = second;
                break;
            case "faction-contract-accept":
                request.Kind = ContentResolutionRequestKind.FactionContractAccept;
                request.FactionId = first;
                request.ContractId = second;
                break;
            case "seasonal-faction-contract-accept":
                request.Kind =
                    ContentResolutionRequestKind.SeasonalFactionContractAccept;
                request.InstanceId = first;
                request.ContractId = second;
                break;
            case "faction-contract-outcome":
                request.Kind = ContentResolutionRequestKind.FactionContractOutcome;
                request.FactionId = first;
                request.ContractSucceeded = string.Equals(
                    second,
                    "success",
                    StringComparison.Ordinal);
                break;
            case "cultural-practice":
                request.Kind = ContentResolutionRequestKind.CulturalPractice;
                request.DefinitionId = first;
                request.ParticipantCharacterIds = second
                    .Split(',')
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                break;
            case "cultural-practice-neglect":
                request.Kind = ContentResolutionRequestKind.CulturalPracticeNeglect;
                request.DefinitionId = first;
                request.ParticipantCharacterIds = second
                    .Split(',')
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                break;
            default:
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
        }

        if (!content.TryExecute(request, out ContentResolutionResult result, out failure))
        {
            return false;
        }

        disposition = result.Disposition ==
                ContentResolutionDisposition.AcceptedPending
            ? EventAlertChoiceActionDisposition.AcceptedPending
            : EventAlertChoiceActionDisposition.Terminal;

        foreach (V20ResolvedEventResult resolved in result.Resolutions)
        {
            events.Publish(new V20ContentEffectsResolvedEvent(
                resolved,
                physicalEffectsApplied: true));
        }
        return true;
    }
}

