using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IReproductionProcessTransitionCommand
{
    bool TryAdvance(
        string processId,
        ReproductionDailyContext context,
        out DomainFailure failure);
    bool TryNotifyCarrierDeath(
        CharacterDeathEvent death,
        out DomainFailure failure);
    bool TryResumePendingCarrierDeaths(out DomainFailure failure);
}

public sealed class ReproductionProcessTransitionRuntime :
    IReproductionProcessTransitionCommand
{
    private const string RandomStreamId = "population:reproduction";
    private readonly IReproductionPersistence persistence;
    private readonly ReproductionTransitionPublication publications;
    private readonly ISettlementAlertService alerts;
    private readonly IRandomStream random;

    public ReproductionProcessTransitionRuntime(
        IReproductionPersistence persistence,
        ReproductionTransitionPublication publications,
        ISettlementAlertService alerts,
        IRandomStreamProvider randomStreams)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        random = (randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(RandomStreamId);
    }

    public bool TryAdvance(
        string processId,
        ReproductionDailyContext context,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ReproductionWorldSaveData beforeWorld = persistence.Capture();
        ReproductionWorldAggregate candidate = persistence.PrepareRestore(beforeWorld);
        if (!candidate.TryGet(processId, out ReproductionProcess process))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (process.PendingTransition != null)
        {
            var pending = process.PendingTransition.fact;
            if (!publications.TryResume(processId, out failure)) return false;
            if (pending.transitionKind == "daily-transition" && pending.absoluteDay >= context.AbsoluteDay) return true;
            beforeWorld = persistence.Capture();
            candidate = persistence.PrepareRestore(beforeWorld);
            candidate.TryGet(processId, out process);
        }
        ReproductionProcessSaveData before = process.Capture();
        process.AdvanceDay(context, random.NextFloat());
        if (process.TransitionRevision == before.transitionRevision)
            return true;
        try
        {
            var fact = publications.Source.Create(before, process, "daily-transition",
                "daily-transition", "none", context.AbsoluteDay,
                new ReproductionFactLocation(process.SupportFacilityInstanceId, string.Empty, 0, 0));
            return publications.TryCommit(beforeWorld, candidate, process, fact, out failure);
        }
        catch (Exception exception)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable, "reproduction-daily-fact:" + exception.Message);
            return false;
        }
    }

    public bool TryNotifyCarrierDeath(
        CharacterDeathEvent death,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string notificationId = "carrier-death:"
            + ReproductionCommandIdentity.Hash(string.Concat(
                death.CharacterId.Value,
                "\n",
                death.AbsoluteDay.ToString(CultureInfo.InvariantCulture),
                "\n",
                ((int)death.Cause).ToString(CultureInfo.InvariantCulture)));
        ReproductionWorldAggregate queued = persistence.PrepareRestore(
            persistence.Capture());
        queued.EnqueueCarrierDeath(new ReproductionCarrierDeathNotificationSaveData
        {
            notificationId = notificationId,
            carrierId = death.CharacterId.Value,
            absoluteDay = death.AbsoluteDay,
            cause = death.Cause,
            locationX = death.Location.X,
            locationY = death.Location.Y,
            parentPublicationKey = death.ParentPublicationKey
        });
        persistence.PublishRestore(queued);
        return TryResumePendingCarrierDeaths(out failure);
    }

    public bool TryResumePendingCarrierDeaths(out DomainFailure failure)
    {
        failure = DomainFailure.None;
        while (true)
        {
            ReproductionWorldSaveData beforeWorld = persistence.Capture();
            ReproductionWorldAggregate candidate = persistence.PrepareRestore(beforeWorld);
            ReproductionCarrierDeathNotificationSaveData notification =
                candidate.PendingCarrierDeaths.FirstOrDefault();
            if (notification == null)
                return true;
            // A committed failed process no longer matches the active filter. Deliver
            // its saved fact before removing the durable death notification.
            var pendingProcess = candidate.Processes.FirstOrDefault(value =>
                value.CarrierId.Value == notification.carrierId && value.PendingTransition != null);
            if (pendingProcess != null)
            {
                if (!publications.TryResume(pendingProcess.ProcessId, out failure)) return false;
                continue;
            }
            ReproductionProcess[] matches = candidate.Processes
                .Where(value => value.CarrierId.Value == notification.carrierId
                    && value.Mode == ReproductionMode.Pregnancy
                    && value.Status is ReproductionProcessStatus.Active
                        or ReproductionProcessStatus.WaitingForEmergencyExtraction)
                .ToArray();
            if (matches.Length == 0)
            {
                candidate.RemoveCarrierDeath(notification.notificationId);
                persistence.PublishRestore(candidate);
                continue;
            }
            if (matches.Length != 1)
            {
                failure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    "reproduction-carrier-death-active-process-conflict");
                return false;
            }

            ReproductionProcess process = matches[0];
            if (process.Status
                == ReproductionProcessStatus.WaitingForEmergencyExtraction)
            {
                if (!TryPublishEmergencyExtractionIncident(
                        process,
                        out failure))
                    return false;
                candidate.RemoveCarrierDeath(notification.notificationId);
                persistence.PublishRestore(candidate);
                continue;
            }
            ReproductionProcessSaveData before = process.Capture();
            process.NotifyCarrierDeath(notification.absoluteDay);
            if (process.TransitionRevision == before.transitionRevision)
            {
                candidate.RemoveCarrierDeath(notification.notificationId);
                persistence.PublishRestore(candidate);
                continue;
            }
            try
            {
                var fact = publications.Source.Create(before, process, "carrier-death",
                    "carrier-death-" + notification.cause.ToString().ToLowerInvariant(),
                    "none", notification.absoluteDay,
                    new ReproductionFactLocation(process.SupportFacilityInstanceId, string.Empty,
                        notification.locationX, notification.locationY),
                    notification.parentPublicationKey);
                if (!publications.TryCommit(beforeWorld, candidate, process, fact, out failure)) return false;
            }
            catch (Exception exception)
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable,
                    "reproduction-carrier-death-fact:" + exception.Message);
                return false;
            }
        }
    }

    private bool TryPublishEmergencyExtractionIncident(
        ReproductionProcess process,
        out DomainFailure failure)
    {
        string incidentId = ReproductionEmergencyExtractionCommandRuntime
            .IncidentId(process.ProcessId);
        EmergencyAccountingResult result = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                incidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(incidentId),
                "reproduction-carrier-death",
                "emergency-extraction-required"));
        if (!result.Success)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                result.Code,
                result.Message);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

}

public sealed class ReproductionTransitionRecoveryAdapter : IStartable, ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly IReproductionProcessTransitionCommand transitions;
    private readonly ReproductionTransitionPublication publications;
    private readonly IUiClock clock;
    private float nextRecovery;
    private string lastFailure = string.Empty;
    public ReproductionTransitionRecoveryAdapter(IReproductionProcessTransitionCommand transitions,
        ReproductionTransitionPublication publications,
        IUiClock clock)
    {
        this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    public void Start() => Recover();
    public void OnRestoreCompleted() { nextRecovery = 0; Recover(); }
    public void Tick()
    {
        if (clock.Time < nextRecovery) return;
        nextRecovery = clock.Time + 0.5f;
        Recover();
    }
    private void Recover()
    {
        bool delivered = publications.TryResumePending(out var deliveryFailure);
        bool notified = transitions.TryResumePendingCarrierDeaths(out var deathFailure);
        string failure = delivered && notified ? string.Empty
            : string.Join(",", (deliveryFailure.IsFailure ? deliveryFailure : deathFailure).Parameters.ToArray());
        if (failure != lastFailure && failure.Length > 0)
            Debug.LogWarning("reproduction-observation-recovery-pending:" + failure);
        lastFailure = failure;
    }
}
