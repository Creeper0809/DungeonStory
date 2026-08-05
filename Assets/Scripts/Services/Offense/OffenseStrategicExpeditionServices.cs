using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IOffenseStrategicExpeditionHost
{
    OffenseExpeditionRun FindActiveExpedition(string expeditionId);
    void RemoveActiveExpedition(OffenseExpeditionRun expedition);
    void CompleteExpedition(
        OffenseExpeditionRun expedition,
        bool success,
        string message);
    void NotifyStateChanged();
}

public interface IOffenseStrategicTargetService
{
    bool TryCreateTarget(
        string siteId,
        out OffenseTargetDefinition target,
        out OffenseHexCoord destination);
    bool TryCreateRescueTarget(
        string targetId,
        out OffenseTargetDefinition target,
        out OffenseHexCoord destination,
        out string strandedExpeditionId);
    bool TryPrepareTravel(
        OffenseExpeditionRun expedition,
        OffenseHexCoord destination,
        bool pauseUntilDepartureCompletes,
        bool startsSiteAttack,
        out string message);
    bool TryRedirect(
        OffenseExpeditionRun expedition,
        OffenseHexCoord destination,
        string siteId,
        bool startsSiteAttack,
        out string message);
    void RegisterRescueDispatch(
        bool isRescue,
        string strandedExpeditionId,
        OffenseExpeditionRun rescue,
        IEnumerable<CharacterActor> party);
}

public interface IOffenseStrategicBattleLauncher
{
    bool TryBegin(
        OffenseExpeditionRun expedition,
        bool objectiveBattle,
        out string message);
}

public interface IOffenseStrategicTravelEventHandler
{
    void HandleStepCompleted(
        IOffenseStrategicExpeditionHost host,
        string expeditionId,
        OffenseTravelStepResult step);
    void HandleDecisionRequired(
        IOffenseStrategicExpeditionHost host,
        string expeditionId);
    void HandleSiteReached(
        IOffenseStrategicExpeditionHost host,
        string expeditionId,
        string siteId);
}

public sealed class OffenseStrategicTargetService :
    IOffenseStrategicTargetService
{
    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseTravelRuntime travel;
    private readonly IOffenseContentCatalog content;
    private readonly IOffenseFieldMedicalRuntime fieldMedical;

    public OffenseStrategicTargetService(
        IOffenseWorldSimulation world,
        IOffenseTravelRuntime travel,
        IOffenseContentCatalog content,
        IOffenseFieldMedicalRuntime fieldMedical)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.fieldMedical = fieldMedical
            ?? throw new ArgumentNullException(nameof(fieldMedical));
    }

    public bool TryCreateTarget(
        string siteId,
        out OffenseTargetDefinition target,
        out OffenseHexCoord destination)
    {
        target = null;
        destination = default;
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return false;
        }

        if (world.TryGetSite(siteId, out OffenseWorldSiteStateData site)
            && site != null
            && site.IsActive
            && site.state != OffenseWorldSiteState.Hidden)
        {
            destination = site.Coord;
            target = new OffenseTargetDefinition
            {
                id = site.siteId,
                title = site.displayName,
                description = "strategic-world-site",
                kind = site.fixedBoss
                    ? OffenseTargetKind.RivalDungeon
                    : OffenseTargetKind.HumanOutpost,
                regionId = site.regionId,
                regionDisplayName = site.regionId,
                factionId = site.factionId,
                strategicPressureAxis = site.pressureAxis,
                strategicPressureAmount = site.pressureAmount,
                campaignOrder = Mathf.Clamp(site.strength, 1, 6),
                revealsTruth = string.Equals(
                    site.archetypeId,
                    "truth_core",
                    StringComparison.Ordinal),
                distance = world.GetMinimumStepDistance(
                    world.DungeonCoord,
                    site.Coord),
                danger = Mathf.Max(1f, site.strength * 10f),
                durationSeconds = 90f + site.strength * 20f,
                requiredMembers = Mathf.Clamp((site.strength + 1) / 2, 1, 5),
                requiredPower = site.strength * 12f,
                rewards = CreateSiteRewards(site)
            };
            return true;
        }

        if (world.TryGetUrgentSite(
                siteId,
                out OffenseUrgentSiteStateData urgent)
            && urgent != null
            && urgent.IsActive)
        {
            destination = urgent.Coord;
            target = new OffenseTargetDefinition
            {
                id = urgent.siteId,
                title = urgent.displayName,
                description = "strategic-urgent-site",
                kind = OffenseTargetKind.SpecialEvent,
                regionId = "urgent",
                regionDisplayName = "urgent-threat",
                factionId = "hostile",
                campaignOrder = Mathf.Clamp((int)urgent.stage + 1, 1, 6),
                distance = world.GetMinimumStepDistance(
                    world.DungeonCoord,
                    urgent.Coord),
                danger = 15f + (int)urgent.stage * 12f,
                durationSeconds = 80f,
                requiredMembers = Mathf.Clamp(
                    2 + (int)urgent.stage / 2,
                    1,
                    5),
                requiredPower = 18f + (int)urgent.stage * 10f,
                rewards = Array.Empty<OffenseRewardPreview>()
            };
            return true;
        }

        return false;
    }

    public bool TryCreateRescueTarget(
        string targetId,
        out OffenseTargetDefinition target,
        out OffenseHexCoord destination,
        out string strandedExpeditionId)
    {
        target = null;
        destination = default;
        strandedExpeditionId = string.Empty;
        const string Prefix = "rescue:";
        if (string.IsNullOrWhiteSpace(targetId)
            || !targetId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        strandedExpeditionId = targetId.Substring(Prefix.Length).Trim();
        if (!fieldMedical.TryGetStrandedState(
                strandedExpeditionId,
                out OffenseStrandedState strandedState))
        {
            return false;
        }

        destination = new OffenseHexCoord(strandedState.q, strandedState.r);
        target = new OffenseTargetDefinition
        {
            id = targetId,
            title = "stranded-expedition-rescue",
            description = "rescue-and-return",
            kind = OffenseTargetKind.SpecialEvent,
            distance = 1f,
            danger = 0f,
            durationSeconds = 30f,
            requiredMembers = 1,
            requiredPower = 0f,
            rewards = Array.Empty<OffenseRewardPreview>()
        };
        return true;
    }

    public bool TryPrepareTravel(
        OffenseExpeditionRun expedition,
        OffenseHexCoord destination,
        bool pauseUntilDepartureCompletes,
        bool startsSiteAttack,
        out string message)
    {
        message = string.Empty;
        if (expedition == null
            || !travel.TryCreateExpedition(expedition.ExpeditionId, out message))
        {
            return false;
        }

        if (!travel.TrySetDestination(
                expedition.ExpeditionId,
                destination,
                expedition.WorldSiteId,
                OffenseTravelProfile.Default,
                startsSiteAttack,
                out message))
        {
            travel.TryRemove(expedition.ExpeditionId);
            return false;
        }

        if (pauseUntilDepartureCompletes)
        {
            travel.TryPauseForBattle(expedition.ExpeditionId);
        }

        return true;
    }

    public bool TryRedirect(
        OffenseExpeditionRun expedition,
        OffenseHexCoord destination,
        string siteId,
        bool startsSiteAttack,
        out string message)
    {
        if (expedition == null || !expedition.UsesWorldTravel)
        {
            message = "expedition-travel-not-found";
            return false;
        }

        OffenseTargetDefinition redirectedTarget = null;
        if (startsSiteAttack
            && (!TryCreateTarget(
                    siteId,
                    out redirectedTarget,
                    out OffenseHexCoord siteCoord)
                || siteCoord != destination))
        {
            message = "strategic-site-location-changed";
            return false;
        }

        if (!travel.TrySetDestination(
                expedition.ExpeditionId,
                destination,
                startsSiteAttack ? siteId : string.Empty,
                OffenseTravelProfile.Default,
                startsSiteAttack,
                out message))
        {
            return false;
        }

        if (startsSiteAttack
            && !string.Equals(
                expedition.WorldSiteId,
                siteId,
                StringComparison.Ordinal)
            && !expedition.RetargetWorldObjective(redirectedTarget))
        {
            message = "expedition-retarget-not-allowed";
            return false;
        }

        message = startsSiteAttack
            ? "strategic-site-attack-started"
            : "expedition-route-redirected";
        return true;
    }

    public void RegisterRescueDispatch(
        bool isRescue,
        string strandedExpeditionId,
        OffenseExpeditionRun rescue,
        IEnumerable<CharacterActor> party)
    {
        if (!isRescue || rescue == null)
        {
            return;
        }

        fieldMedical.TryDispatchRescue(
            strandedExpeditionId,
            rescue.ExpeditionId,
            (party ?? Array.Empty<CharacterActor>())
                .Where(member => member?.Identity != null)
                .Select(member => member.Identity.PersistentId),
            out _);
    }

    private OffenseRewardPreview[] CreateSiteRewards(
        OffenseWorldSiteStateData site)
    {
        OffenseSiteArchetypeSO archetype = content.SiteArchetypes?
            .FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.siteTypeId,
                    site.archetypeId,
                    StringComparison.Ordinal));
        return archetype?.rewards?
            .Where(reward => reward != null && reward.IsConfigured)
            .Select(reward => reward.CreatePreview(site.strength))
            .Where(preview => preview != null && preview.IsConfigured)
            .ToArray() ?? Array.Empty<OffenseRewardPreview>();
    }
}

public sealed class OffenseStrategicBattleLauncher :
    IOffenseStrategicBattleLauncher
{
    private readonly IOffenseTravelRuntime travel;
    private readonly IOffenseBattleRuntime battles;
    private readonly IOffenseBattleDirector director;

    public OffenseStrategicBattleLauncher(
        IOffenseTravelRuntime travel,
        IOffenseBattleRuntime battles,
        IOffenseBattleDirector director)
    {
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.battles = battles ?? throw new ArgumentNullException(nameof(battles));
        this.director = director ?? throw new ArgumentNullException(nameof(director));
    }

    public bool TryBegin(
        OffenseExpeditionRun expedition,
        bool objectiveBattle,
        out string message)
    {
        if (expedition == null || !expedition.BeginWorldBattle(objectiveBattle))
        {
            message = "expedition-battle-state-transition-failed";
            return false;
        }

        travel.TryPauseForBattle(expedition.ExpeditionId);
        if (!battles.TryStartBattle(expedition, out message))
        {
            expedition.CompleteWorldBattle(victory: false);
            return false;
        }

        director.Clear();
        IReadOnlyList<OffenseBattleMemberDeckSeed> members =
            OffenseStrategicBattleSetupFactory.CreateMemberDecks(battles.Session);
        IReadOnlyList<OffenseEnemyIntentStateData> intents =
            OffenseStrategicBattleSetupFactory.CreateEnemyIntents(battles.Session);
        if (!director.TryStartBattle(
                battles.Session.BattleId,
                members,
                intents,
                expedition.ExpeditionId.GetHashCode(),
                out message)
            || !director.TryDrawTurn(out message))
        {
            battles.ClearCompletedBattle();
            expedition.CompleteWorldBattle(victory: false);
            return false;
        }

        message = objectiveBattle
            ? "strategic-objective-battle-started"
            : "strategic-encounter-battle-started";
        return true;
    }
}

public sealed class OffenseStrategicTravelEventHandler :
    IOffenseStrategicTravelEventHandler
{
    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseTravelRuntime travel;
    private readonly IOffenseDecisionRuntime decisions;
    private readonly IOffenseReturnSafetyRuntime returnSafety;
    private readonly IOffenseFieldMedicalRuntime fieldMedical;
    private readonly IGameEventBus events;
    private readonly IOffenseStrategicBattleLauncher battleLauncher;

    public OffenseStrategicTravelEventHandler(
        IOffenseWorldSimulation world,
        IOffenseTravelRuntime travel,
        IOffenseDecisionRuntime decisions,
        IOffenseReturnSafetyRuntime returnSafety,
        IOffenseFieldMedicalRuntime fieldMedical,
        IGameEventBus events,
        IOffenseStrategicBattleLauncher battleLauncher)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        this.returnSafety = returnSafety
            ?? throw new ArgumentNullException(nameof(returnSafety));
        this.fieldMedical = fieldMedical
            ?? throw new ArgumentNullException(nameof(fieldMedical));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.battleLauncher = battleLauncher
            ?? throw new ArgumentNullException(nameof(battleLauncher));
    }

    public void HandleStepCompleted(
        IOffenseStrategicExpeditionHost host,
        string expeditionId,
        OffenseTravelStepResult step)
    {
        OffenseExpeditionRun expedition = host.FindActiveExpedition(expeditionId);
        if (expedition == null || !expedition.UsesWorldTravel)
        {
            return;
        }

        if (step.Arrived
            && step.Position == world.DungeonCoord
            && expedition.WorldObjectiveCompleted)
        {
            travel.TryRemove(expeditionId);
            host.CompleteExpedition(
                expedition,
                success: true,
                "expedition-returned-to-dungeon");
            return;
        }

        host.NotifyStateChanged();
    }

    public void HandleDecisionRequired(
        IOffenseStrategicExpeditionHost host,
        string expeditionId)
    {
        OffenseExpeditionRun expedition = host.FindActiveExpedition(expeditionId);
        if (expedition == null || !expedition.UsesWorldTravel)
        {
            travel.TryResumeAfterDecision(expeditionId);
            return;
        }

        expedition.SetDecisionPaused(true);
        if (!decisions.TryGetActiveDecision(expeditionId, out _)
            && travel.TryGetState(
                expeditionId,
                out OffenseTravelStateData travelState))
        {
            OffenseReturnSafetySnapshot safety = returnSafety.Get(expeditionId);
            bool hasDowned = expedition.MemberStates.Any(member =>
                member?.Actor?.Lifecycle?.CurrentState
                    == CharacterLifecycleState.Downed);
            decisions.TryCreateDecision(
                new OffenseDecisionContext
                {
                    expeditionId = expeditionId,
                    sequence = travelState.eventSequence,
                    stage = expedition.WorldObjectiveCompleted
                        ? OffenseDecisionStage.Return
                        : OffenseDecisionStage.Travel,
                    protectedMovement = safety.IsProtected,
                    forceNonCombat = returnSafety.MustUseNonCombatCard(expeditionId),
                    canGenerateForcedCombat = returnSafety.CanGenerateForcedCombat(
                        expeditionId,
                        GetAverageHealthRatio(expedition),
                        hasDowned,
                        hasUsableWeaponForEveryActiveMember: true)
                },
                out _,
                out _);
        }

        events.RaiseAlert(
            "expedition-decision-required",
            "expedition-decision-required",
            EventAlertImportance.Medium,
            "offense");
        host.NotifyStateChanged();
    }

    public void HandleSiteReached(
        IOffenseStrategicExpeditionHost host,
        string expeditionId,
        string siteId)
    {
        OffenseExpeditionRun expedition = host.FindActiveExpedition(expeditionId);
        if (expedition != null
            && fieldMedical.TryGetRescueConvoy(
                expeditionId,
                out RescueConvoyState convoy))
        {
            CompleteRescueArrival(host, expedition, convoy);
            return;
        }

        if (expedition == null
            || !expedition.UsesWorldTravel
            || !string.Equals(
                expedition.WorldSiteId,
                siteId,
                StringComparison.Ordinal))
        {
            return;
        }

        battleLauncher.TryBegin(
            expedition,
            objectiveBattle: true,
            out string message);
        if (!string.IsNullOrWhiteSpace(message))
        {
            events.RaiseAlert(
                "strategic-site-battle",
                message,
                EventAlertImportance.High,
                "offense");
        }
    }

    public static float GetAverageHealthRatio(OffenseExpeditionRun expedition)
    {
        float[] ratios = expedition?.MemberStates
            .Select(member => member?.Actor)
            .Where(actor => actor != null && !actor.IsDead)
            .Select(actor => actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth))
            .ToArray() ?? Array.Empty<float>();
        return ratios.Length > 0 ? ratios.Average() : 0f;
    }

    private void CompleteRescueArrival(
        IOffenseStrategicExpeditionHost host,
        OffenseExpeditionRun rescue,
        RescueConvoyState convoy)
    {
        OffenseExpeditionRun stranded = host.FindActiveExpedition(
            convoy.strandedExpeditionId);
        if (stranded == null)
        {
            events.RaiseAlert(
                "rescue-failed",
                "stranded-expedition-not-found",
                EventAlertImportance.High,
                "offense");
            return;
        }

        string[] protectedIds = stranded.MemberActors
            .Where(member => member?.Identity != null)
            .Select(member => member.Identity.PersistentId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        if (!fieldMedical.TryMergeRescue(
                rescue.ExpeditionId,
                protectedIds,
                out string reason))
        {
            events.RaiseAlert(
                "rescue-merge-failed",
                reason,
                EventAlertImportance.High,
                "offense");
            return;
        }

        rescue.MergeProtectedRescueMembers(stranded.MemberActors);
        rescue.BeginRescueReturn();
        host.RemoveActiveExpedition(stranded);
        travel.TryRemove(stranded.ExpeditionId);
        if (!travel.TrySetDestination(
                rescue.ExpeditionId,
                world.DungeonCoord,
                string.Empty,
                OffenseTravelProfile.Default,
                startsSiteAttack: false,
                out reason))
        {
            events.RaiseAlert(
                "rescue-return-blocked",
                reason,
                EventAlertImportance.High,
                "offense");
        }
        else
        {
            events.RaiseAlert(
                "rescue-merged",
                $"rescued-members:{protectedIds.Length}",
                EventAlertImportance.High,
                "offense");
        }

        host.NotifyStateChanged();
    }
}
