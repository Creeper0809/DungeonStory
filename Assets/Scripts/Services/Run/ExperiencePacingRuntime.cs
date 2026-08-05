using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;
using VContainer.Unity;

/// <summary>
/// Scene-facing experience pacing runtime. Mutable pacing state is owned by
/// <see cref="ExperiencePacingAggregate"/>; this type only exposes the
/// application contract and Unity entry-point lifecycle.
/// </summary>
public sealed class ExperiencePacingRuntime :
    IExperiencePacingRuntime,
    IStartable,
    IDisposable
{
    private readonly ExperiencePacingApplicationAdapter application;
    private readonly ExperiencePacingAggregate aggregate;
    private IDisposable dayStartedSubscription;

    [Inject]
    public ExperiencePacingRuntime(
        ExperiencePacingApplicationAdapter application)
    {
        this.application = application
            ?? throw new ArgumentNullException(nameof(application));
        aggregate = application.Aggregate;
    }

    public int CurrentDay => aggregate.CurrentDay;
    public bool AllowsRandomInvasion =>
        CurrentDay >= application.RandomInvasionStartDay;
    public int MaximumConcurrentExternalProblems =>
        application.ResolveMaximumConcurrentExternalProblems(CurrentDay);
    public bool IsRehearsalActive => aggregate.IsRehearsalActive;
    public int ActiveRehearsalDay => aggregate.ActiveRehearsalDay;

    public void Start()
    {
        dayStartedSubscription ??=
            application.SubscribeToOperatingDayStarted(AdvanceToDay);
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
    }

    public void AdvanceToDay(int day) => aggregate.AdvanceToDay(day);

    public bool TryBeginRehearsal(
        int day,
        out RehearsalInvasionProfile profile)
    {
        if (!application.TryResolveRehearsal(
                day,
                out int rehearsalBit,
                out profile))
        {
            return false;
        }

        return aggregate.TryBeginRehearsal(day, rehearsalBit);
    }

    public void ResolveRehearsal()
    {
        int rehearsalBit = application.ResolveRehearsalBit(
            aggregate.ActiveRehearsalDay);
        if (rehearsalBit != 0)
        {
            aggregate.ResolveRehearsal(rehearsalBit);
        }
    }

    public bool CanStartExteriorIncident(ExteriorIncidentKind kind) =>
        kind != ExteriorIncidentKind.None
        && application.IsExteriorIncidentAllowed(CurrentDay, kind);

    public void MarkExteriorIncidentStarted(ExteriorIncidentKind kind) =>
        aggregate.MarkExteriorIncidentStarted(kind);

    public DungeonExperiencePacingSaveData Capture() => new()
    {
        currentDay = aggregate.CurrentDay,
        scheduledRehearsalMask = aggregate.ScheduledRehearsalMask,
        completedRehearsalMask = aggregate.CompletedRehearsalMask,
        activeRehearsalDay = aggregate.ActiveRehearsalDay,
        introducedConcepts = aggregate.IntroducedConcepts
            .Select(value => (int)value)
            .ToList()
    };

    public ExperiencePacingAggregateState PrepareRestoreCandidate(
        DungeonExperiencePacingSaveData data) =>
        application.PrepareRestoreCandidate(data);

    public void PublishRestoreCandidate(
        ExperiencePacingAggregateState candidate) =>
        application.PublishRestoreCandidate(candidate);
}
