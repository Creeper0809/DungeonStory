using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class SettlementThreatEventAdapter : IStartable, IDisposable
{
    private const string InvasionIncidentId = "incident:invasion:active";
    private const string MedicalIncidentId = "incident:medical-capacity";
    private const string InvasionForecastId = "risk:invasion";
    private const string MedicalForecastId = "risk:medical";
    private const long AmberInvasionP90MilliWu = 18_000L;
    private const long RedInvasionP90MilliWu = 30_000L;
    private const long MinimumMedicalP90PerPatientMilliWu = 12_000L;

    private readonly IGameEventBus events;
    private readonly ISettlementAlertService alerts;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterMedicalQuery medical;
    private readonly IEmergencyRiskForecastRegistry forecasts;
    private readonly HashSet<string> downedCharacterIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<IDisposable> subscriptions = new List<IDisposable>();

    public SettlementThreatEventAdapter(
        IGameEventBus events,
        ISettlementAlertService alerts,
        ICharacterWorldQuery characters,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterMedicalQuery medical,
        IEmergencyRiskForecastRegistry forecasts)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.medical = medical ?? throw new ArgumentNullException(nameof(medical));
        this.forecasts = forecasts ?? throw new ArgumentNullException(nameof(forecasts));
    }

    public void Start()
    {
        subscriptions.Add(events.Subscribe<InvasionThreatWarningEvent>(OnInvasionWarning));
        subscriptions.Add(events.Subscribe<InvasionCandidateEvent>(OnInvasionCandidate));
        subscriptions.Add(events.Subscribe<InvasionStartedEvent>(OnInvasionStarted));
        subscriptions.Add(events.Subscribe<InvasionDungeonBreachedEvent>(OnInvasionBreached));
        subscriptions.Add(events.Subscribe<InvasionFinalCombatStartedEvent>(OnFinalCombatStarted));
        subscriptions.Add(events.Subscribe<InvasionResolvedEvent>(OnInvasionResolved));
        subscriptions.Add(events.Subscribe<CharacterBodyHealthDownedEvent>(OnCharacterDowned));
        subscriptions.Add(events.Subscribe<CharacterBodyHealthRecoveredEvent>(OnCharacterRecovered));
        RebuildDownedCharacters();
        PublishMedicalCapacity();
    }

    public void Dispose()
    {
        for (int index = 0; index < subscriptions.Count; index++)
        {
            subscriptions[index]?.Dispose();
        }
        subscriptions.Clear();
    }

    private void OnInvasionWarning(InvasionThreatWarningEvent _) =>
        PublishInvasion(SettlementThreatAlertLevel.Amber, "invasion warning");

    private void OnInvasionCandidate(InvasionCandidateEvent _) =>
        PublishInvasion(SettlementThreatAlertLevel.Amber, "invasion candidate detected");

    private void OnInvasionStarted(InvasionStartedEvent _) =>
        PublishInvasion(SettlementThreatAlertLevel.Red, "invasion started");

    private void OnInvasionBreached(InvasionDungeonBreachedEvent _) =>
        PublishInvasion(SettlementThreatAlertLevel.Red, "defense zone breached");

    private void OnFinalCombatStarted(InvasionFinalCombatStartedEvent _) =>
        PublishInvasion(SettlementThreatAlertLevel.Red, "final combat started");

    private void OnInvasionResolved(InvasionResolvedEvent resolved)
    {
        long revision = alerts.GetNextIncidentRevision(InvasionIncidentId);
        EmergencyAccountingResult result = resolved.residualRisk > 0f
            ? alerts.PublishIncidentSignal(new SettlementIncidentSignal(
                InvasionIncidentId,
                SettlementThreatAlertLevel.Amber,
                revision,
                "invasion",
                $"contained with residual risk {resolved.residualRisk:0.###}"))
            : alerts.ResolveIncident(InvasionIncidentId, revision);
        RequireSuccessOrMissingOnResolution(result);
        if (resolved.residualRisk > 0f)
        {
            RequireSuccess(forecasts.SetP90Requirement(
                InvasionForecastId,
                AmberInvasionP90MilliWu));
        }
        else
        {
            RequireSuccess(forecasts.Remove(InvasionForecastId));
        }
    }

    private void PublishInvasion(
        SettlementThreatAlertLevel level,
        string diagnostic)
    {
        RequireSuccess(forecasts.SetP90Requirement(
            InvasionForecastId,
            level == SettlementThreatAlertLevel.Red
                ? RedInvasionP90MilliWu
                : AmberInvasionP90MilliWu));
        long revision = alerts.GetNextIncidentRevision(InvasionIncidentId);
        RequireSuccess(alerts.PublishIncidentSignal(new SettlementIncidentSignal(
            InvasionIncidentId,
            level,
            revision,
            "invasion",
            diagnostic)));
    }

    private void OnCharacterDowned(CharacterBodyHealthDownedEvent downed)
    {
        RebuildDownedCharacters();
        PublishMedicalCapacity();
    }

    private void OnCharacterRecovered(CharacterBodyHealthRecoveredEvent recovered)
    {
        RebuildDownedCharacters();
        PublishMedicalCapacity();
    }

    private void RebuildDownedCharacters()
    {
        downedCharacterIds.Clear();
        IReadOnlyList<CharacterActor> all = characters.Characters;
        for (int index = 0; index < all.Count; index++)
        {
            CharacterActor actor = all[index];
            if (actor == null
                || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
                || !bodyHealth.GetSnapshot(actor).Downed)
            {
                continue;
            }
            downedCharacterIds.Add(characterId.Value);
        }
    }

    private void PublishMedicalCapacity()
    {
        long revision = alerts.GetNextIncidentRevision(MedicalIncidentId);
        if (downedCharacterIds.Count == 0)
        {
            RequireSuccessOrMissingOnResolution(
                alerts.ResolveIncident(MedicalIncidentId, revision));
            RequireSuccess(forecasts.Remove(MedicalForecastId));
            return;
        }

        SettlementThreatAlertLevel level = downedCharacterIds.Count >= 2
            ? SettlementThreatAlertLevel.Red
            : SettlementThreatAlertLevel.Amber;
        RequireSuccess(forecasts.SetP90Requirement(
            MedicalForecastId,
            CalculateMedicalP90MilliWu(
                downedCharacterIds.Count,
                medical.ActiveOrders)));
        RequireSuccess(alerts.PublishIncidentSignal(new SettlementIncidentSignal(
            MedicalIncidentId,
            level,
            revision,
            "character-body-health",
            $"{downedCharacterIds.Count} character(s) awaiting rescue")));
    }

    public static long CalculateMedicalP90MilliWu(
        int downedCount,
        IReadOnlyList<CharacterMedicalOrder> activeOrders)
    {
        if (downedCount <= 0)
        {
            return 0L;
        }

        double remaining = 0d;
        IReadOnlyList<CharacterMedicalOrder> orders =
            activeOrders ?? Array.Empty<CharacterMedicalOrder>();
        for (int index = 0; index < orders.Count; index++)
        {
            CharacterMedicalOrder order = orders[index];
            if (order == null || !order.IsActive)
            {
                continue;
            }

            remaining += Math.Max(
                0d,
                order.requiredStabilizationWork
                    - order.completedStabilizationWork);
            remaining += Math.Max(
                0d,
                order.requiredTreatmentWork
                    - order.completedTreatmentWork);
        }

        long authoredFloor = checked(
            downedCount * MinimumMedicalP90PerPatientMilliWu);
        long liveRemaining = EmergencyWuUnits.FromWu(
            (float)Math.Min(
                EmergencyWuUnits.ToWu(
                    EmergencyWuUnits.MaximumReserveWindowMilliWu),
                remaining));
        return Math.Max(authoredFloor, liveRemaining);
    }

    private static void RequireSuccess(EmergencyAccountingResult result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"{result.Code}: {result.Message}");
        }
    }

    private static void RequireSuccessOrMissingOnResolution(
        EmergencyAccountingResult result)
    {
        if (!result.Success
            && !string.Equals(
                result.Code,
                "SettlementIncidentMissing",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{result.Code}: {result.Message}");
        }
    }
}
