using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ApparelConditionRuntime
{
    private readonly IEnvironmentalFieldQuery field;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterApparelQuery apparel;
    private readonly IWorldItemStackRuntime items;
    private readonly IClimateQuery climate;
    private readonly IClimateDefinitionCatalog climateDefinitions;
    private readonly IGridSystemProvider gridProvider;
    private readonly IApparelDefinitionCatalog apparelDefinitions;
    private readonly ITextileMaterialCatalog materials;
    private readonly IApparelMaterialProjector projector;
    private readonly IAnatomyAttachmentQuery anatomy;

    public ApparelConditionRuntime(
        IEnvironmentalFieldQuery field,
        ICharacterWorldQuery characters,
        ICharacterApparelQuery apparel,
        IWorldItemStackRuntime items,
        IClimateQuery climate,
        IClimateDefinitionCatalog climateDefinitions,
        IGridSystemProvider gridProvider,
        IApparelDefinitionCatalog apparelDefinitions,
        ITextileMaterialCatalog materials,
        IApparelMaterialProjector projector,
        IAnatomyAttachmentQuery anatomy)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.climate = climate ?? throw new ArgumentNullException(nameof(climate));
        this.climateDefinitions = climateDefinitions
            ?? throw new ArgumentNullException(nameof(climateDefinitions));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.apparelDefinitions = apparelDefinitions
            ?? throw new ArgumentNullException(nameof(apparelDefinitions));
        this.materials = materials
            ?? throw new ArgumentNullException(nameof(materials));
        this.projector = projector
            ?? throw new ArgumentNullException(nameof(projector));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
    }

    public bool TryAdvance(
        float deltaSeconds,
        IReadOnlyDictionary<CharacterId, EnvironmentalWorkKind> workContexts,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (float.IsNaN(deltaSeconds)
            || float.IsInfinity(deltaSeconds)
            || deltaSeconds < 0f
            || workContexts == null)
        {
            failure = new DomainFailure(
                FailureCode.ApparelStateInvalid,
                "condition-step-input");
            return false;
        }
        if (deltaSeconds <= 0f)
        {
            return true;
        }

        int characterVersion = characters.CharacterVersion;
        int apparelVersion = apparel.Version;
        int itemVersion = items.ItemStackVersion;
        int climateVersion = climate.Version;
        int fieldVersion = field.Version;
        IReadOnlyList<EquippedApparelSnapshot> equipped =
            apparel.GetAllEquipped() ?? Array.Empty<EquippedApparelSnapshot>();
        if (equipped.Count == 0)
        {
            return true;
        }
        if (!field.IsInitialized
            || !gridProvider.TryGetGrid(out Grid grid)
            || grid == null)
        {
            failure = new DomainFailure(
                FailureCode.ApparelStateInvalid,
                !field.IsInitialized ? "environment-field-missing" : "grid-missing");
            return false;
        }

        int gridStructuralVersion = grid.StructuralVersion;
        int gridNavigationVersion = grid.NavigationVersion;
        WeatherFrontDefinition front;
        try
        {
            front = climateDefinitions.RequireFront(climate.WeatherFrontId);
        }
        catch (Exception exception)
        {
            failure = new DomainFailure(
                FailureCode.ApparelStateInvalid,
                "weather-front",
                exception.Message);
            return false;
        }
        bool raining = front.Kind is WeatherFrontKind.Rain or WeatherFrontKind.Storm;

        Dictionary<CharacterId, CharacterActor> actors = new();
        IReadOnlyList<CharacterActor> worldActors =
            characters.Characters ?? Array.Empty<CharacterActor>();
        for (int index = 0; index < worldActors.Count; index++)
        {
            CharacterActor actor = worldActors[index];
            CharacterId characterId = new(actor?.Identity?.PersistentId);
            if (actor == null
                || actor.IsDead
                || actor.IsOnExpedition
                || !characterId.IsValid)
            {
                continue;
            }
            if (!actors.TryAdd(characterId, actor))
            {
                failure = new DomainFailure(
                    FailureCode.ApparelStateInvalid,
                    "duplicate-character",
                    characterId.Value);
                return false;
            }
        }

        Dictionary<string, WorldItemStackSnapshot> physicalByInstance = new(
            StringComparer.Ordinal);
        IReadOnlyList<WorldItemStackSnapshot> physical =
            items.GetAllStacks() ?? Array.Empty<WorldItemStackSnapshot>();
        for (int index = 0; index < physical.Count; index++)
        {
            WorldItemStackSnapshot stack = physical[index];
            if (stack == null || string.IsNullOrWhiteSpace(stack.ItemInstanceId))
            {
                continue;
            }
            if (!physicalByInstance.TryAdd(stack.ItemInstanceId, stack))
            {
                failure = new DomainFailure(
                    FailureCode.ApparelStateInvalid,
                    "duplicate-physical-instance",
                    stack.ItemInstanceId);
                return false;
            }
        }

        Dictionary<CharacterId, ApparelConditionCoverage[]> coverageByCharacter =
            equipped
                .GroupBy(value => value.CharacterId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(value => new ApparelConditionCoverage(
                            value.Layer,
                            value.OccupiedPoints))
                        .ToArray());
        HashSet<ItemInstanceId> visited = new();
        List<PreparedUpdate> updates = new(equipped.Count);
        foreach (EquippedApparelSnapshot entry in equipped
                     .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
                     .ThenBy(value => value.Layer)
                     .ThenBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal))
        {
            if (!actors.TryGetValue(entry.CharacterId, out CharacterActor actor))
            {
                continue;
            }
            if (!entry.ItemInstanceId.IsValid || !visited.Add(entry.ItemInstanceId))
            {
                failure = InvalidEntry(entry, "duplicate-equipped-instance");
                return false;
            }
            if (!physicalByInstance.TryGetValue(
                    entry.ItemInstanceId.Value,
                    out WorldItemStackSnapshot stack)
                || stack.Quantity != 1
                || stack.State != WorldItemStackState.Carried
                || !string.Equals(
                    stack.DestinationId,
                    CharacterApparelAggregate.EquippedDestinationPrefix
                        + entry.CharacterId.Value,
                    StringComparison.Ordinal))
            {
                failure = InvalidEntry(entry, "physical-ownership");
                return false;
            }
            if (!ApparelItemStateCodec.TryRead(
                    stack.Components,
                    out ApparelInstanceState current))
            {
                failure = InvalidEntry(entry, "apparel-component");
                return false;
            }
            if (!apparelDefinitions.TryGetByItemId(
                    stack.ItemId,
                    out ApparelDefinitionSO definition)
                || !string.Equals(
                    definition.ApparelId,
                    entry.ApparelDefinitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    definition.ApparelId,
                    current.apparelDefinitionId,
                    StringComparison.Ordinal))
            {
                failure = InvalidEntry(entry, "apparel-definition");
                return false;
            }
            if (!materials.TryGet(
                    current.primaryMaterialId,
                    out TextileMaterialDefinitionSO material))
            {
                failure = InvalidEntry(entry, "textile-material");
                return false;
            }
            if (!field.TryGetCell(actor.GetNowXY(), out _))
            {
                continue;
            }
            GridCell cell = grid.GetGridCell(actor.GetNowXY());
            if (cell == null)
            {
                failure = InvalidEntry(entry, "grid-cell-missing");
                return false;
            }
            if (actor.Stats == null
                || !actor.Stats.TryGetConditionValue(
                    CharacterCondition.HYGIENE,
                    out float hygiene))
            {
                failure = InvalidEntry(entry, "hygiene-missing");
                return false;
            }

            int apparelIndex = apparelDefinitions.GetIndex(definition.ApparelId);
            int materialIndex = materials.GetIndex(material.MaterialId);
            if (apparelIndex < 0 || materialIndex < 0)
            {
                failure = InvalidEntry(entry, "projection-catalog");
                return false;
            }
            ApparelFitAssessment fit = default;
            _ = anatomy.CanEquip(
                entry.CharacterId,
                definition,
                current,
                out fit,
                out _);
            ApparelDerivedStats nominal = projector.GetOrCreate(
                new ApparelProjectionKey(
                    apparelIndex,
                    materialIndex,
                    current.craftsmanshipQuality,
                    4,
                    TextileConditionBand.Ready,
                    fit.UnusedOpenings,
                    fit.AdjacentSize));
            ApparelDerivedStats projected = projector.GetOrCreate(
                new ApparelProjectionKey(
                    apparelIndex,
                    materialIndex,
                    current.craftsmanshipQuality,
                    ApparelMaterialProtectionRules.ResolveDurabilityBand(
                        current.durability),
                    TextileConditionRules.ResolveCondition(
                        current.moisture,
                        current.contamination),
                    fit.UnusedOpenings,
                    fit.AdjacentSize));
            float exposed = ApparelConditionRules.ResolveExposedFraction(
                entry.Layer,
                entry.OccupiedPoints,
                coverageByCharacter[entry.CharacterId]);
            ApparelConditionStepResult advanced = ApparelConditionRules.Step(
                new ApparelConditionStepInput(
                    deltaSeconds,
                    entry.Layer,
                    hygiene,
                    workContexts.ContainsKey(entry.CharacterId),
                    cell.AreaType == GridCellAreaType.ExteriorPath,
                    raining,
                    exposed,
                    nominal.Durability,
                    projected.WaterResistance,
                    material.DryingRate,
                    current.durability,
                    current.moisture,
                    current.contamination));
            ApparelInstanceState next = Clone(current);
            next.durability = advanced.Durability;
            next.moisture = advanced.Moisture;
            next.contamination = advanced.Contamination;
            ItemInstanceComponentSaveData originalComponent =
                TextileBatchItemState.Find(
                    stack.Components,
                    ItemInstanceComponentIds.Apparel)?.Clone();
            if (originalComponent == null)
            {
                failure = InvalidEntry(entry, "apparel-component");
                return false;
            }
            updates.Add(new PreparedUpdate(
                stack.StackId,
                entry.ItemInstanceId,
                originalComponent,
                ApparelItemStateCodec.Create(next)));
        }

        if (characters.CharacterVersion != characterVersion
            || apparel.Version != apparelVersion
            || items.ItemStackVersion != itemVersion
            || climate.Version != climateVersion
            || field.Version != fieldVersion
            || !gridProvider.TryGetGrid(out Grid currentGrid)
            || !ReferenceEquals(currentGrid, grid)
            || grid.StructuralVersion != gridStructuralVersion
            || grid.NavigationVersion != gridNavigationVersion)
        {
            failure = new DomainFailure(
                FailureCode.ApparelPlanStale,
                "condition-step-snapshot");
            return false;
        }

        List<PreparedUpdate> applied = new(updates.Count);
        int expectedItemVersion = itemVersion;
        for (int index = 0; index < updates.Count; index++)
        {
            PreparedUpdate update = updates[index];
            int versionBeforeWrite = items.ItemStackVersion;
            if (items.ItemStackVersion != expectedItemVersion
                || !items.TrySetInstanceComponent(
                    update.StackId,
                    update.NextComponent))
            {
                RollBack(applied);
                failure = new DomainFailure(
                    FailureCode.ApparelTransferFailed,
                    update.ItemInstanceId.Value,
                    "condition-step");
                return false;
            }
            applied.Add(update);
            if (items.ItemStackVersion == versionBeforeWrite)
            {
                RollBack(applied);
                failure = new DomainFailure(
                    FailureCode.ApparelTransferFailed,
                    update.ItemInstanceId.Value,
                    "condition-step-revision");
                return false;
            }
            expectedItemVersion = items.ItemStackVersion;
        }
        return true;
    }

    private void RollBack(IReadOnlyList<PreparedUpdate> applied)
    {
        for (int index = applied.Count - 1; index >= 0; index--)
        {
            PreparedUpdate update = applied[index];
            if (!items.TrySetInstanceComponent(
                    update.StackId,
                    update.OriginalComponent))
            {
                throw new InvalidOperationException(
                    $"Could not roll back apparel condition '{update.ItemInstanceId.Value}'.");
            }
        }
    }

    private static DomainFailure InvalidEntry(
        EquippedApparelSnapshot entry,
        string reason) => new(
        FailureCode.ApparelStateInvalid,
        entry.ItemInstanceId.Value,
        reason);

    private static ApparelInstanceState Clone(ApparelInstanceState state) => new()
    {
        apparelDefinitionId = state.apparelDefinitionId,
        primaryMaterialId = state.primaryMaterialId,
        craftsmanshipQuality = state.craftsmanshipQuality,
        sourceKind = state.sourceKind,
        sourceDefinitionId = state.sourceDefinitionId,
        size = state.size,
        modifications = state.modifications,
        closedOpenings = state.closedOpenings,
        durability = state.durability,
        moisture = state.moisture,
        contamination = state.contamination,
        designatedWearerCharacterId = state.designatedWearerCharacterId,
        craftedAbsoluteDay = state.craftedAbsoluteDay,
        deterministicBatchHash = state.deterministicBatchHash,
        mythicProvenance = state.mythicProvenance?.Clone()
    };

    private readonly struct PreparedUpdate
    {
        public PreparedUpdate(
            string stackId,
            ItemInstanceId itemInstanceId,
            ItemInstanceComponentSaveData originalComponent,
            ItemInstanceComponentSaveData nextComponent)
        {
            StackId = stackId ?? string.Empty;
            ItemInstanceId = itemInstanceId;
            OriginalComponent = originalComponent;
            NextComponent = nextComponent;
        }

        public string StackId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public ItemInstanceComponentSaveData OriginalComponent { get; }
        public ItemInstanceComponentSaveData NextComponent { get; }
    }
}
