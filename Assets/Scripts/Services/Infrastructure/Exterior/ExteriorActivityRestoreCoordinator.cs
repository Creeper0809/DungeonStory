using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public sealed class ExteriorActivityWorldRestoreCandidate :
    IDungeonDiscardableRestoreCandidate
{
    internal List<ExteriorZoneMarker> Zones { get; } = new();
    internal List<ExteriorIncidentRuntimeState> IncidentStates { get; } = new();
    internal int NextIncidentSequence { get; set; }
    internal Action<ExteriorActivityWorldRestoreCandidate> DiscardAction { get; set; }

    public void Discard()
    {
        Action<ExteriorActivityWorldRestoreCandidate> discard = DiscardAction;
        DiscardAction = null;
        discard?.Invoke(this);
    }
}

internal sealed class ExteriorActivityRestoreCoordinator
{
    private static readonly GridLayer[] MarkerLayers =
    {
        GridLayer.FloorOverlay,
        GridLayer.WallFixture,
        GridLayer.CeilingFixture,
        GridLayer.Building,
        GridLayer.Hallway
    };

    private readonly ExteriorActivityWorldServices world;
    private readonly ExteriorActivityDomainServices domain;
    private bool transactionActive;
    private bool publicationActive;
    private ExteriorActivityWorldRestoreCandidate candidate;

    public ExteriorActivityRestoreCoordinator(
        ExteriorActivityWorldServices world,
        ExteriorActivityDomainServices domain)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.domain = domain
            ?? throw new ArgumentNullException(nameof(domain));
    }

    public void Validate(
        DungeonExteriorActivitySaveData payload,
        DungeonGameRestoreReport report)
    {
        ExteriorActivitySaveValidation.Validate(
            payload,
            report,
            domain.IncidentHandlers,
            world.Items.CatalogProvider);
    }

    public void Begin()
    {
        if (transactionActive || publicationActive)
        {
            throw new InvalidOperationException(
                "An exterior activity restore candidate is already active.");
        }

        transactionActive = true;
        candidate = null;
    }

    public ExteriorActivityWorldRestoreCandidate Build(
        DungeonExteriorActivitySaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        Validate(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Exterior activity restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        if (!world.RestoreCandidates.TryGetGrid(out Grid grid)
            || grid == null)
        {
            throw new InvalidOperationException(
                "Exterior activity restore requires the detached facility grid candidate.");
        }

        ValidateWorldReferences(payload, grid, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Exterior activity restore candidate has invalid world references: "
                + string.Join(" | ", report.Errors));
        }

        ExteriorActivityWorldRestoreCandidate restored =
            new ExteriorActivityWorldRestoreCandidate
            {
                NextIncidentSequence = payload.nextIncidentSequence
            };
        foreach (ExteriorZoneSaveData zoneData in payload.zones)
        {
            if (!TryCreateZone(
                    grid,
                    zoneData,
                    report,
                    out ExteriorZoneMarker zone))
            {
                DiscardZones(restored.Zones);
                throw new InvalidOperationException(
                    "Exterior zone candidate creation failed: "
                    + string.Join(" | ", report.Errors));
            }

            restored.Zones.Add(zone);
        }

        Dictionary<string, ExteriorZoneMarker> zonesById =
            restored.Zones.ToDictionary(
                zone => zone.ZoneId,
                StringComparer.Ordinal);
        foreach (ExteriorIncidentRuntimeState source in
                 payload.incidentStates)
        {
            ExteriorIncidentRuntimeState incident = source.Clone();
            restored.IncidentStates.Add(incident);
            if (!incident.IsTerminal)
            {
                zonesById[incident.zoneId].ProjectIncident(
                    incident.kind,
                    incident.incidentId,
                    incident.text,
                    incident.remainingSeconds);
            }
        }

        IExteriorIncidentExactSourceRestoreContributor[] contributors = domain
            .IncidentHandlers.All
            .OfType<IExteriorIncidentExactSourceRestoreContributor>()
            .OrderBy(value => value.ExactSourceOwnerDomain, StringComparer.Ordinal)
            .ToArray();
        List<PhysicalItemExactSourceRestoreDescriptor> retainedSources = new();
        foreach (ExteriorIncidentRuntimeState incident in restored.IncidentStates
                     .Where(value => !value.IsTerminal))
        {
            if (!domain.IncidentHandlers.TryGet(
                    incident.kind,
                    out IExteriorIncidentHandler handler)
                || handler is not IExteriorIncidentExactSourceRestoreContributor
                    contributor)
                continue;
            if (!contributor.TryCreateRestoreDescriptor(
                    incident,
                    zonesById[incident.zoneId],
                    out PhysicalItemExactSourceRestoreDescriptor descriptor,
                    out string descriptorFailure))
            {
                DiscardZones(restored.Zones);
                throw new InvalidOperationException(
                    "Exterior exact source restore descriptor failed: "
                    + descriptorFailure);
            }
            retainedSources.Add(descriptor);
        }
        if (contributors.Length > 0
            && !world.ExactSourceRestore.TryReplaceRestoreAuthorities(
                contributors.Select(value => value.ExactSourceOwnerDomain)
                    .ToArray(),
                retainedSources,
                out string sourceRestoreFailure))
        {
            DiscardZones(restored.Zones);
            throw new InvalidOperationException(
                "Exterior exact source restore authority failed: "
                + sourceRestoreFailure);
        }

        restored.DiscardAction = DiscardDetachedCandidate;
        world.CandidatePublisher.SetExteriorZoneCandidate(
            restored.Zones);
        return restored;
    }

    public void Adopt(ExteriorActivityWorldRestoreCandidate restored)
    {
        if (!transactionActive || candidate != null)
        {
            throw new InvalidOperationException(
                "Exterior candidate publication requires one active V18 transaction.");
        }

        candidate = restored
            ?? throw new ArgumentNullException(nameof(restored));
        candidate.DiscardAction = DiscardDetachedCandidate;
    }

    public ExteriorActivityWorldRestoreCandidate Publish()
    {
        if (!transactionActive || publicationActive || candidate == null)
        {
            throw new InvalidOperationException(
                "No exterior activity restore candidate is ready to publish.");
        }

        foreach (ExteriorZoneMarker zone in candidate.Zones)
        {
            if (zone == null
                || !zone.IsDetachedRestoreCandidate
                || zone.gameObject.activeSelf
                || zone.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "An exterior zone restore candidate must remain detached and inactive until completion.");
            }
        }

        publicationActive = true;
        return candidate;
    }

    public void RollbackPublished()
    {
        if (!transactionActive || !publicationActive || candidate == null)
        {
            throw new InvalidOperationException(
                "No published exterior restore candidate is ready to roll back.");
        }

        candidate.Discard();
        world.CandidatePublisher.ClearExteriorZoneCandidate();
        candidate = null;
        publicationActive = false;
        transactionActive = false;
    }

    public void CompletePublished()
    {
        if (!transactionActive || !publicationActive || candidate == null)
        {
            throw new InvalidOperationException(
                "No published exterior restore candidate is ready to complete.");
        }

        world.CandidatePublisher.ClearExteriorZoneCandidate();
        candidate.DiscardAction = null;
        candidate = null;
        publicationActive = false;
        transactionActive = false;
    }

    public void Discard()
    {
        if (publicationActive)
        {
            RollbackPublished();
            return;
        }

        world.CandidatePublisher.ClearExteriorZoneCandidate();
        if (candidate != null)
        {
            candidate.Discard();
        }

        candidate = null;
        publicationActive = false;
        transactionActive = false;
    }

    private void DiscardDetachedCandidate(
        ExteriorActivityWorldRestoreCandidate restored)
    {
        if (restored != null)
        {
            DiscardZones(restored.Zones);
        }
        world.CandidatePublisher.ClearExteriorZoneCandidate();
    }

    private void ValidateWorldReferences(
        DungeonExteriorActivitySaveData payload,
        Grid grid,
        DungeonGameRestoreReport report)
    {
        HashSet<string> buildingIds = world.RestoreCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> buildings)
            ? buildings
                .Where(building => building != null)
                .Select(building => building.PersistentInstanceId.Value)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        foreach (ExteriorZoneSaveData zone in payload.zones)
        {
            Vector2Int position = new Vector2Int(zone.gridX, zone.gridY);
            if (!grid.IsValidGridPos(position)
                || !grid.IsWalkable(position)
                || grid.GetGridCell(position) == null
                || !buildingIds.Add(zone.buildingInstanceId))
            {
                report.AddError(
                    $"Exterior zone '{zone.zoneId}' conflicts with the restored world at {position}.");
            }
        }

        HashSet<string> characterIds =
            world.RestoreCandidates.TryGetCharacters(
                out IReadOnlyList<CharacterActor> characters)
                ? characters
                    .Where(actor => actor?.Identity != null)
                    .Select(actor => actor.Identity.PersistentId)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> wildlifeIds =
            world.RestoreCandidates.TryGetWildlife(
                out IReadOnlyList<WildlifeActor> wildlife)
                ? wildlife
                    .Where(actor => actor != null)
                    .Select(actor => actor.WildlifeId)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> itemStackIds = world.Items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        if (!world.AcknowledgedOutputs.IsCandidateAvailable)
        {
            report.AddError(
                "Exterior activity restore requires acknowledged physical-output candidates.");
        }
        else
        {
            itemStackIds.UnionWith(world.AcknowledgedOutputs.Batches
                .SelectMany(batch => batch.Stacks)
                .Select(stack => stack.StackId));
        }

        foreach (ExteriorIncidentRuntimeState incident in
                 payload.incidentStates.Where(value => !value.IsTerminal))
        {
            ValidateReferences(
                incident.actorIds,
                characterIds,
                incident.incidentId,
                "character",
                report);
            ValidateReferences(
                incident.wildlifeIds,
                wildlifeIds,
                incident.incidentId,
                "wildlife",
                report);
            ValidateReferences(
                incident.itemStackIds,
                itemStackIds,
                incident.incidentId,
                "item stack",
                report);
        }
    }

    private bool TryCreateZone(
        Grid grid,
        ExteriorZoneSaveData saveData,
        DungeonGameRestoreReport report,
        out ExteriorZoneMarker marker)
    {
        marker = null;
        Vector2Int position = new Vector2Int(
            saveData.gridX,
            saveData.gridY);
        GridCell cell = grid.GetGridCell(position);
        if (!TryGetFreeMarkerLayer(cell, out GridLayer markerLayer))
        {
            report.AddError(
                $"Exterior zone '{saveData.zoneId}' has no free marker layer at {position}.");
            return false;
        }

        GameObject zoneObject = new GameObject(
            $"ExteriorZone_{saveData.zoneType}_{position.x}_{position.y}");
        zoneObject.SetActive(false);
        try
        {
            DungeonRuntimeHierarchy.Parent(
                zoneObject,
                DungeonRuntimeHierarchy.Exterior);
            marker = zoneObject.AddComponent<ExteriorZoneMarker>();
            marker.PrepareForDetachedRestore();
            world.ObjectResolver.InjectGameObject(zoneObject);
            marker.RestorePersistentIdentity(
                (BuildingInstanceId)saveData.buildingInstanceId);
            marker.InitializeRuntime(
                grid,
                position,
                saveData.zoneType,
                world.BuildingArchetypes.RequireExteriorZone(
                    saveData.zoneType,
                    markerLayer),
                saveData);
            GridLayer registeredLayer =
                marker.BuildingData.Placement.Layer;
            if (!cell.ContainsOccupant(registeredLayer, marker))
            {
                marker.DiscardDetachedRestore();
                marker = null;
                report.AddError(
                    $"Exterior zone '{saveData.zoneId}' did not register on the detached grid.");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            if (marker != null && marker.IsDetachedRestoreCandidate)
            {
                marker.DiscardDetachedRestore();
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(zoneObject);
            }

            marker = null;
            report.AddError(
                $"Exterior zone '{saveData.zoneId}' candidate creation failed: {exception.Message}");
            return false;
        }
    }

    private static bool TryGetFreeMarkerLayer(
        GridCell cell,
        out GridLayer layer)
    {
        if (cell != null)
        {
            foreach (GridLayer candidateLayer in MarkerLayers)
            {
                if (cell.CanOccupy(candidateLayer))
                {
                    layer = candidateLayer;
                    return true;
                }
            }
        }

        layer = GridLayer.FloorOverlay;
        return false;
    }

    private static void ValidateReferences(
        IEnumerable<string> references,
        ISet<string> available,
        string incidentId,
        string label,
        DungeonGameRestoreReport report)
    {
        foreach (string reference in references)
        {
            if (!available.Contains(reference))
            {
                report.AddError(
                    $"Exterior incident '{incidentId}' references missing {label} '{reference}'.");
            }
        }
    }

    private static void DiscardZones(
        IEnumerable<ExteriorZoneMarker> zones)
    {
        foreach (ExteriorZoneMarker zone in
                 zones ?? Enumerable.Empty<ExteriorZoneMarker>())
        {
            if (zone == null)
            {
                continue;
            }

            if (zone.IsDetachedRestoreCandidate)
            {
                zone.DiscardDetachedRestore();
            }
            else
            {
                zone.RetireForWorldReplacement();
            }
        }
    }

}
