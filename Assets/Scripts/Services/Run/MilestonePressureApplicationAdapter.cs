using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Turns persisted milestone pressure IDs into deterministic recurring world
/// consequences. The pressure schedule is derived from the absolute day, so it
/// remains stable after save restore without creating another shadow state.
/// </summary>
public sealed class MilestonePressureApplicationAdapter :
    IStartable,
    IDisposable
{
    private readonly IMilestoneGameplayModifierQuery milestones;
    private readonly IInvasionCampaignRuntime invasions;
    private readonly IFactionCampaignQuery factionQuery;
    private readonly IFactionCampaignCommand factionCommands;
    private readonly IContentWorkDelayCommand workDelays;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeCommand lifeCommands;
    private readonly IGameEventBus events;
    private IDisposable dayEndedSubscription;

    public MilestonePressureApplicationAdapter(
        IMilestoneGameplayModifierQuery milestones,
        IInvasionCampaignRuntime invasions,
        IFactionCampaignQuery factionQuery,
        IFactionCampaignCommand factionCommands,
        IContentWorkDelayCommand workDelays,
        ICharacterLifeQuery life,
        ICharacterLifeCommand lifeCommands,
        IGameEventBus events)
    {
        this.milestones = milestones
            ?? throw new ArgumentNullException(nameof(milestones));
        this.invasions = invasions
            ?? throw new ArgumentNullException(nameof(invasions));
        this.factionQuery = factionQuery
            ?? throw new ArgumentNullException(nameof(factionQuery));
        this.factionCommands = factionCommands
            ?? throw new ArgumentNullException(nameof(factionCommands));
        this.workDelays = workDelays
            ?? throw new ArgumentNullException(nameof(workDelays));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.lifeCommands = lifeCommands
            ?? throw new ArgumentNullException(nameof(lifeCommands));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(
            value => ApplyPressure(Math.Max(1, value.day)));
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    private void ApplyPressure(int absoluteDay)
    {
        ApplyAccordObligation(absoluteDay);
        ApplyInternalPressures(absoluteDay);
        ApplyTemporalAnomaly(absoluteDay);
        ApplyInvasionPressure(absoluteDay);
    }

    private void ApplyAccordObligation(int absoluteDay)
    {
        if (!milestones.HasPressure("ending:monster-accord")
            || absoluteDay % GameCalendarRules.DaysPerSeason != 0)
        {
            return;
        }

        int affectedCount = 0;
        foreach (FactionCampaignStateSaveData faction in factionQuery.Factions
                     .Where(value => value != null)
                     .OrderBy(value => value.factionId, StringComparer.Ordinal))
        {
            factionCommands.ApplyFactionChange(
                faction.factionId,
                rapportDelta: 0,
                grievanceDelta: 0,
                obligationDelta: 1);
            affectedCount++;
        }

        events.Publish(new MilestonePressureAppliedFact(
            MilestonePressureEffectKind.DefenseObligation,
            absoluteDay,
            "ending:monster-accord",
            "monster-accord",
            affectedCount: affectedCount,
            obligationDelta: 1));
    }

    private void ApplyInternalPressures(int absoluteDay)
    {
        if (absoluteDay % GameCalendarRules.DaysPerSeason == 0
            && milestones.HasPressure("ending:sealed-paradise"))
        {
            workDelays.ApplyWorkDelay("farm", 3, absoluteDay);
            events.Publish(new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.FarmingWorkDelay,
                absoluteDay,
                "ending:sealed-paradise",
                "sealed-paradise",
                workScopeId: "farm",
                workDelayDays: 3));
        }

        if (absoluteDay % 40 == 0
            && milestones.HasPressure("ending:eternal-lineage"))
        {
            workDelays.ApplyWorkDelay("global", 1, absoluteDay);
            events.Publish(new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.InheritanceWorkDelay,
                absoluteDay,
                "ending:eternal-lineage",
                "eternal-lineage",
                workScopeId: "global",
                workDelayDays: 1));
        }

        if (absoluteDay % 20 == 0
            && milestones.HasPressure("ending:steel-apotheosis"))
        {
            workDelays.ApplyWorkDelay("repair", 2, absoluteDay);
            events.Publish(new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.RepairWorkDelay,
                absoluteDay,
                "ending:steel-apotheosis",
                "steel-apotheosis",
                workScopeId: "repair",
                workDelayDays: 2));
        }
    }

    private void ApplyTemporalAnomaly(int absoluteDay)
    {
        if (absoluteDay % GameCalendarRules.DaysPerSeason != 0
            || !milestones.HasPressure("ending:timeless-sanctuary"))
        {
            return;
        }

        int acceleratedMaintenanceDay = absoluteDay + 3;
        int changed = 0;
        foreach (CharacterLifeRecord record in life.Records
                     .Where(value => value != null
                         && value.RequestedAgingCareMode
                             == AgingCareMode.TemporalStasis)
                     .OrderBy(value => value.CharacterId.Value,
                         StringComparer.Ordinal))
        {
            if (record.TemporalStasisNextMaintenanceAbsoluteDay
                <= acceleratedMaintenanceDay)
            {
                continue;
            }

            lifeCommands.ConfigureTemporalStasis(
                record.CharacterId,
                record.TemporalStasisFacilityId,
                record.EffectiveAgingCareMode == AgingCareMode.TemporalStasis,
                acceleratedMaintenanceDay);
            changed++;
        }

        if (changed > 0)
        {
            events.Publish(new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.TemporalMaintenanceAcceleration,
                absoluteDay,
                "ending:timeless-sanctuary",
                "timeless-sanctuary",
                affectedCount: changed,
                maintenanceLeadDays: 3));
        }
    }

    private void ApplyInvasionPressure(int absoluteDay)
    {
        float threat = 0f;
        string source = string.Empty;
        string pressureId = string.Empty;
        if (milestones.HasPressure("ending:truth-revealed")
            && absoluteDay % 20 == 0)
        {
            threat = 75f;
            source = "truth-revealed";
            pressureId = "ending:truth-revealed";
        }
        if (milestones.HasPressure("ending:surface-hegemony")
            && absoluteDay % 15 == 0
            && threat < 90f)
        {
            threat = 90f;
            source = "surface-hegemony";
            pressureId = "ending:surface-hegemony";
        }
        if (milestones.HasPressure("ending:dungeon-sovereignty")
            && absoluteDay % 10 == 0
            && threat < 100f)
        {
            threat = 100f;
            source = "dungeon-sovereignty";
            pressureId = "ending:dungeon-sovereignty";
        }
        if (milestones.HasPressure("ending:arcane-ascension")
            && absoluteDay % 20 == 0
            && threat < 95f)
        {
            threat = 95f;
            source = "arcane-ascension";
            pressureId = "ending:arcane-ascension";
        }

        if (threat <= 0f)
        {
            return;
        }

        string sourceOwnerId =
            $"milestone-pressure:{source}:{absoluteDay}";
        ScheduledInvasionOperationState operation =
            invasions.ScheduleMilestonePressureOperation(
                threat,
                sourceOwnerId,
                absoluteDay,
                out bool created);
        if (operation != null && created)
        {
            events.Publish(new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.InvasionScheduled,
                absoluteDay,
                pressureId,
                source,
                invasionOperationId: operation.operationId,
                invasionThreat: threat));
        }
    }
}
