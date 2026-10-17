using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct ReproductionEmergencyExtractionRequest
{
    public ReproductionEmergencyExtractionRequest(
        string processId,
        CharacterId medicalResponderId)
    {
        ProcessId = processId?.Trim() ?? string.Empty;
        MedicalResponderId = medicalResponderId;
    }

    public string ProcessId { get; }
    public CharacterId MedicalResponderId { get; }
}

public interface IReproductionEmergencyExtractionCommand
{
    bool TryExecute(
        ReproductionEmergencyExtractionRequest request,
        out DomainFailure failure);
}

public interface IReproductionEmergencyResponderQuery
{
    bool TryValidate(
        CharacterId responderId,
        out ReproductionFactLocation location,
        out string failureReason);
}

public sealed class ReproductionEmergencyResponderQuery :
    IReproductionEmergencyResponderQuery
{
    private readonly ICharacterWorldQuery world;

    public ReproductionEmergencyResponderQuery(ICharacterWorldQuery world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public bool TryValidate(
        CharacterId responderId,
        out ReproductionFactLocation location,
        out string failureReason)
    {
        location = default;
        failureReason = string.Empty;
        CharacterActor actor = world.Characters.FirstOrDefault(value =>
            value != null
            && !value.IsDead
            && CharacterPersistentIdentity.TryGet(value, out CharacterId id)
            && id.Equals(responderId));
        if (actor == null)
        {
            failureReason = "reproduction-emergency-responder-missing";
            return false;
        }
        if (!actor.TryGetAbility(out AbilityWork work)
            || (!work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Treat)
                && !work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Surgery)))
        {
            failureReason = "reproduction-emergency-responder-not-medical";
            return false;
        }
        Vector3 position = actor.transform.position;
        location = new ReproductionFactLocation(
            "settlement:medical-response",
            string.Empty,
            Mathf.RoundToInt(position.x),
            Mathf.RoundToInt(position.y));
        return true;
    }
}

/// <summary>
/// Resolves the red-alert medical command for a late carrier death. The
/// reproduction aggregate remains the mutable authority; the outcome is
/// prepared before publication and a definite commit rejection restores the
/// exact waiting state. Alert state is a saved projection of the still-open
/// emergency and is resolved only after the transition commits.
/// </summary>
public sealed class ReproductionEmergencyExtractionCommandRuntime :
    IReproductionEmergencyExtractionCommand
{
    private readonly IReproductionPersistence persistence;
    private readonly ReproductionTransitionPublication publications;
    private readonly IReproductionEmergencyResponderQuery responders;
    private readonly ISettlementAlertService alerts;
    private readonly IGameCalendar calendar;

    public ReproductionEmergencyExtractionCommandRuntime(
        IReproductionPersistence persistence,
        ReproductionTransitionPublication publications,
        IReproductionEmergencyResponderQuery responders,
        ISettlementAlertService alerts,
        IGameCalendar calendar)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
        this.responders = responders
            ?? throw new ArgumentNullException(nameof(responders));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public bool TryExecute(
        ReproductionEmergencyExtractionRequest request,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!ReproductionStableIdSyntax.IsValid(request.ProcessId)
            || !request.MedicalResponderId.IsValid)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "reproduction-emergency-request-invalid");
            return false;
        }
        if (!responders.TryValidate(
                request.MedicalResponderId,
                out ReproductionFactLocation location,
                out string responderFailure))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                responderFailure);
            return false;
        }

        ReproductionWorldSaveData beforeWorld = persistence.Capture();
        ReproductionWorldAggregate candidate = persistence.PrepareRestore(beforeWorld);
        if (!candidate.TryGet(request.ProcessId, out ReproductionProcess process))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "reproduction-emergency-process-missing");
            return false;
        }
        if (process.PendingTransition != null)
        {
            bool extracting = process.PendingTransition.fact.transitionKind == "emergency-extraction";
            if (!publications.TryResume(process.ProcessId, out failure)) return false;
            if (extracting) return true;
            beforeWorld = persistence.Capture();
            candidate = persistence.PrepareRestore(beforeWorld);
            candidate.TryGet(request.ProcessId, out process);
        }
        int absoluteDay = Math.Max(0, calendar.Current.AbsoluteDay);
        int deadlineDay = checked(process.CarrierDeathAbsoluteDay + 1);
        if (process.Status
                != ReproductionProcessStatus.WaitingForEmergencyExtraction
            || process.CarrierDeathAbsoluteDay <= 0)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "reproduction-emergency-not-waiting");
            return false;
        }
        if (absoluteDay > deadlineDay)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "reproduction-emergency-deadline-expired",
                deadlineDay.ToString());
            return false;
        }

        string incidentId = IncidentId(process.ProcessId);
        EmergencyAccountingResult signal = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                incidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(incidentId),
                "reproduction-emergency-command",
                "emergency-extraction-in-progress"));
        if (!signal.Success)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                signal.Code,
                signal.Message);
            return false;
        }

        ReproductionProcessSaveData before = process.Capture();
        process.EmergencyExtract(absoluteDay);
        ReproductionTransitionFact fact;
        try
        {
            fact = publications.Source.Create(before, process, "emergency-extraction",
                "medical-emergency-command", "none", absoluteDay, location,
                responder: request.MedicalResponderId, deadline: deadlineDay);
        }
        catch (Exception exception)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable,
                "reproduction-emergency-fact:" + exception.Message);
            return false;
        }
        return publications.TryCommit(beforeWorld, candidate, process, fact, out failure);
    }

    public static string IncidentId(string processId) =>
        "reproduction-emergency:"
        + ReproductionCommandIdentity.Hash(processId?.Trim() ?? string.Empty);
}
