using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal interface IWildlifeRestorePort
{
    WildlifePopulationState Population { get; }
    void ReplacePopulation(WildlifePopulationState replacement);
    void RebuildPopulationRuntimes();
}

internal sealed class WildlifeRestoreCoordinator
{
    public const string RestoreParticipantId = "250.world.wildlife";
    private const float CarcassTickInterval = 2f;

    private readonly IWildlifeRestorePort port;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWildlifeCarcassService carcassService;
    private readonly IGameClock gameClock;
    private readonly WildlifeRestoreServices restoreServices;
    private readonly WildlifeWorldRuntime worldRuntime;
    private bool restoreTransactionActive;
    private WildlifeRestoreCandidate restoreCandidate;
    private WildlifePublication activePublication;

    public WildlifeRestoreCoordinator(
        IWildlifeRestorePort port,
        WildlifeWorldServices world,
        WildlifeCombatServices combat,
        WildlifeExecutionServices execution,
        WildlifeRestoreServices restore,
        WildlifeWorldRuntime worldRuntime)
    {
        this.port = port ?? throw new ArgumentNullException(nameof(port));
        WildlifeWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        WildlifeCombatServices requiredCombat = combat
            ?? throw new ArgumentNullException(nameof(combat));
        WildlifeExecutionServices requiredExecution = execution
            ?? throw new ArgumentNullException(nameof(execution));
        speciesCatalog = requiredWorld.Species;
        ecosystemRuntime = requiredWorld.Ecosystem;
        itemRuntime = requiredWorld.Items;
        carcassService = requiredCombat.Carcasses;
        gameClock = requiredExecution.Clock;
        restoreServices = restore
            ?? throw new ArgumentNullException(nameof(restore));
        this.worldRuntime = worldRuntime
            ?? throw new ArgumentNullException(nameof(worldRuntime));
    }

    public void ValidatePayload(DungeonWildlifeSaveData saveData)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        WildlifeSaveValidation.Validate(saveData, report, speciesCatalog);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Wildlife payload is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    public WildlifeRestoreCandidate BuildCandidate(
        DungeonWildlifeSaveData saveData)
    {
        ValidatePayload(saveData);
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        if (!restoreServices.WorldCandidates.TryGetGrid(out Grid restoreGrid)
            || restoreGrid == null)
        {
            throw new InvalidOperationException(
                "Wildlife restore requires the detached facility grid candidate.");
        }

        WildlifeSaveValidation.ValidateWorldReferences(
            saveData,
            restoreGrid,
            itemRuntime,
            speciesCatalog,
            report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Wildlife restore candidate has invalid world references: "
                + string.Join(" | ", report.Errors));
        }

        WildlifeEcosystemRestoreCandidate ecosystemCandidate;
        try
        {
            ecosystemCandidate = ecosystemRuntime.PrepareRestoreCandidate(
                saveData.ecosystem,
                restoreGrid);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Wildlife ecosystem candidate preparation failed: {exception.Message}",
                exception);
        }

        WildlifeRestoreCandidate candidate = WildlifeRestoreCandidate.Create(
            saveData,
            gameClock.Time + CarcassTickInterval,
            ecosystemCandidate);

        foreach (WildlifeSaveData animal in saveData.wildlife)
        {
            if (TryPrepareActorCandidate(
                    restoreGrid,
                    animal,
                    candidate.Population,
                    report))
            {
                continue;
            }

            worldRuntime.DiscardCandidateActors(candidate.Population);
            throw new InvalidOperationException(
                "Wildlife actor candidate preparation failed: "
                + string.Join(" | ", report.Errors));
        }

        candidate.DiscardAction = DiscardDetachedCandidate;
        restoreServices.CandidatePublisher.SetWildlifeCandidate(
            candidate.Population.Actors);
        return candidate;
    }

    public void StageCandidate(WildlifeRestoreCandidate candidate)
    {
        if (!restoreTransactionActive
            || restoreCandidate != null
            || activePublication != null)
        {
            throw new InvalidOperationException(
                "Wildlife candidate publication requires one active V18 transaction.");
        }

        restoreCandidate = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        restoreCandidate.DiscardAction = DiscardDetachedCandidate;
    }

    public void Begin()
    {
        if (restoreTransactionActive || activePublication != null)
        {
            throw new InvalidOperationException(
                "A wildlife restore candidate is already active.");
        }

        restoreTransactionActive = true;
        restoreCandidate = null;
    }

    public void Publish()
    {
        if (!restoreTransactionActive
            || restoreCandidate == null
            || activePublication != null)
        {
            throw new InvalidOperationException(
                "No wildlife restore candidate is ready to publish.");
        }

        WildlifeRestoreCandidate published = restoreCandidate;
        WildlifePublication publication = new WildlifePublication(
            published,
            port.Population);
        activePublication = publication;

        publication.EcosystemTransaction =
            ecosystemRuntime.ApplyRestoreCandidate(published.Ecosystem);
        publication.FreshnessTransaction =
            carcassService.ApplyFreshnessRestoreCandidate(
                published.Carcasses);
        port.ReplacePopulation(published.Population);
        publication.PopulationPublished = true;
        port.RebuildPopulationRuntimes();
        foreach (WildlifeActor actor in published.Population.Actors)
        {
            actor.PublishDetachedRestore();
            publication.PublishedActors.Add(actor);
            actor.ValidateDetachedRestorePublication();
        }
    }

    public void RollbackPublished()
    {
        if (!restoreTransactionActive || activePublication == null)
        {
            Discard();
            return;
        }

        WildlifePublication publication = activePublication;
        List<Exception> failures = new List<Exception>();
        void Attempt(Action rollback)
        {
            try
            {
                rollback();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        for (int index = publication.PublishedActors.Count - 1;
             index >= 0;
             index--)
        {
            WildlifeActor actor = publication.PublishedActors[index];
            if (actor != null)
            {
                Attempt(actor.RollbackDetachedRestorePublication);
            }
        }
        publication.PublishedActors.Clear();

        if (publication.PopulationPublished)
        {
            port.ReplacePopulation(publication.PreviousPopulation);
            publication.PopulationPublished = false;
            Attempt(port.RebuildPopulationRuntimes);
        }

        if (publication.FreshnessTransaction != null)
        {
            Attempt(() => carcassService.RollbackFreshnessRestore(
                publication.FreshnessTransaction));
            publication.FreshnessTransaction = null;
        }

        if (publication.EcosystemTransaction != null)
        {
            Attempt(() => ecosystemRuntime.RollbackRestore(
                publication.EcosystemTransaction));
            publication.EcosystemTransaction = null;
        }

        Attempt(publication.Candidate.Discard);
        Attempt(restoreServices.CandidatePublisher.ClearWildlifeCandidate);
        activePublication = null;
        restoreCandidate = null;
        restoreTransactionActive = false;

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Wildlife publication rollback encountered one or more failures after attempting every reversal.",
                failures);
        }
    }

    public void Complete()
    {
        if (!restoreTransactionActive || activePublication == null)
        {
            throw new InvalidOperationException(
                "No published wildlife restore candidate is ready to complete.");
        }

        WildlifePublication publication = activePublication;
        void Attempt(Action completion)
        {
            try
            {
                completion();
            }
            catch
            {
                // Aggregate completion is non-failing; derived cleanup is best effort.
            }
        }

        if (publication.EcosystemTransaction != null)
        {
            Attempt(() => ecosystemRuntime.CompleteRestore(
                publication.EcosystemTransaction));
            publication.EcosystemTransaction = null;
        }

        if (publication.FreshnessTransaction != null)
        {
            Attempt(() => carcassService.CompleteFreshnessRestore(
                publication.FreshnessTransaction));
            publication.FreshnessTransaction = null;
        }

        foreach (WildlifeActor oldActor in publication.PreviousPopulation.Actors)
        {
            if (oldActor != null)
            {
                Attempt(() => oldActor.gameObject.SetActive(false));
            }
        }

        foreach (WildlifeActor actor in publication.PublishedActors)
        {
            if (actor == null)
            {
                continue;
            }

            Attempt(actor.CompleteDetachedRestorePublication);
            Attempt(() => actor.gameObject.SetActive(true));
        }

        Attempt(() => worldRuntime.DestroyPopulationActors(
            publication.PreviousPopulation));
        Attempt(restoreServices.CandidatePublisher.ClearWildlifeCandidate);

        publication.Candidate.DiscardAction = null;
        activePublication = null;
        restoreCandidate = null;
        restoreTransactionActive = false;
    }

    public void Discard()
    {
        if (activePublication != null)
        {
            RollbackPublished();
            return;
        }

        if (restoreCandidate != null)
        {
            restoreCandidate.Discard();
        }

        restoreServices.CandidatePublisher.ClearWildlifeCandidate();
        restoreCandidate = null;
        restoreTransactionActive = false;
    }

    private void DiscardDetachedCandidate(WildlifeRestoreCandidate candidate)
    {
        if (candidate != null)
        {
            worldRuntime.DiscardCandidateActors(candidate.Population);
        }

        restoreServices.CandidatePublisher.ClearWildlifeCandidate();
    }

    private bool TryPrepareActorCandidate(
        Grid restoreGrid,
        WildlifeSaveData saveData,
        WildlifePopulationState candidatePopulation,
        DungeonGameRestoreReport report)
    {
        if (!speciesCatalog.TryGetSpecies(
                saveData.speciesId,
                out WildlifeSpeciesDefinition species))
        {
            report.AddError(
                $"Wildlife '{saveData.wildlifeId}' references unknown species '{saveData.speciesId}'.");
            return false;
        }

        Vector2Int position = new Vector2Int(saveData.gridX, saveData.gridY);
        if (!WildlifeWorldRuntime.CanSpawnAt(
                restoreGrid,
                position,
                species.CanEnterDungeon))
        {
            report.AddError(
                $"Wildlife '{saveData.wildlifeId}' has an invalid or occupied restored position {position}.");
            return false;
        }

        WildlifeActor actor;
        try
        {
            actor = worldRuntime.CreateActor(
                restoreGrid,
                species,
                position,
                saveData.wildlifeId,
                saveData,
                detachedRestore: true);
        }
        catch (Exception exception)
        {
            report.AddError(
                $"Wildlife '{saveData.wildlifeId}' candidate creation failed: {exception.Message}");
            return false;
        }

        GridCell cell = restoreGrid.GetGridCell(position);
        if (cell == null
            || !cell.ContainsOccupant(GridLayer.Wildlife, actor))
        {
            actor.DiscardDetachedRestore();
            report.AddError(
                $"Wildlife '{saveData.wildlifeId}' did not register on the detached grid.");
            return false;
        }

        candidatePopulation.Actors.Add(actor);
        return true;
    }

    private sealed class WildlifePublication
    {
        internal WildlifePublication(
            WildlifeRestoreCandidate candidate,
            WildlifePopulationState previousPopulation)
        {
            Candidate = candidate
                ?? throw new ArgumentNullException(nameof(candidate));
            PreviousPopulation = previousPopulation
                ?? throw new ArgumentNullException(nameof(previousPopulation));
        }

        internal WildlifeRestoreCandidate Candidate { get; }
        internal WildlifePopulationState PreviousPopulation { get; }
        internal List<WildlifeActor> PublishedActors { get; } =
            new List<WildlifeActor>();
        internal WildlifeEcosystemRestoreTransaction EcosystemTransaction { get; set; }
        internal WildlifeCarcassFreshnessRestoreTransaction FreshnessTransaction { get; set; }
        internal bool PopulationPublished { get; set; }
    }
}
