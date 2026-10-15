using System;

/// <summary>A neutral immutable observation of an actual spatial transition.</summary>
public sealed class DungeonSpaceExpandedFact
{
    private readonly DungeonSpaceExpansionPublicationData data;

    public DungeonSpaceExpandedFact(DungeonSpaceExpansionPublicationData source)
    {
        data = (source ?? throw new ArgumentNullException(nameof(source))).Clone();
        if (string.IsNullOrWhiteSpace(data.projectId) || string.IsNullOrWhiteSpace(data.projectName)
            || data.absoluteDay < 0 || data.previousColumns <= 0
            || data.currentColumns <= data.previousColumns
            || data.previousWidth < data.previousColumns || data.currentWidth < data.previousWidth
            || data.currentWidth > DungeonSpaceExpansionCatalog.MaximumSupportedGridWidth
            || data.currentWidth < data.currentColumns || data.entranceX < 0
            || data.entranceX >= data.previousWidth || data.entranceY < 0
            || data.entranceY >= DungeonSpaceExpansionCatalog.SupportedGridHeight)
            throw new ArgumentException("Dungeon-space transition facts are invalid.");
        DungeonSpaceExpansionDefinition definition;
        if (data.trigger == DungeonSpaceExpansionTrigger.NewRunInitialization)
        {
            definition = DungeonSpaceExpansionCatalog.TierZeroInitialization;
            if (!string.IsNullOrEmpty(data.parentResearchPublicationKey)
                || data.previousColumns != DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns)
                throw new ArgumentException("Initial space preparation has no research parent and requires the scene seed.");
        }
        else if (data.trigger == DungeonSpaceExpansionTrigger.ResearchCompletion
            && DungeonSpaceExpansionCatalog.TryGet(data.projectId, out definition))
        {
            if (string.IsNullOrWhiteSpace(data.parentResearchPublicationKey))
                throw new ArgumentException("Research expansion requires its completed parent reference.");
        }
        else throw new ArgumentException("Unknown dungeon-space transition cause.");
        if (data.projectId != definition.ResearchProjectId || data.tier != definition.Tier
            || data.currentColumns != definition.TargetInteriorColumns)
            throw new ArgumentException("Dungeon-space transition does not match its definition.");
        // Transport state is not part of the immutable domain fact.
        data.active = false;
        data.publicationKey = string.Empty;
    }

    public long Revision => data.currentColumns;
    public DungeonSpaceExpansionPublicationData Capture() => data.Clone();

    public static void ValidateSavedPublication(ModularFacilityWorldSaveData snapshot)
    {
        var pending = snapshot?.spaceExpansionPublication
            ?? throw new InvalidOperationException("Spatial publication state is missing.");
        if (!pending.active)
        {
            if (!string.IsNullOrEmpty(pending.publicationKey))
                throw new InvalidOperationException("Inactive spatial publication retains an identity.");
            return;
        }
        _ = new DungeonSpaceExpandedFact(pending);
        if (string.IsNullOrWhiteSpace(pending.publicationKey)
            || snapshot.gridWidth != pending.currentWidth
            || !DungeonSpaceGridLayout.TryCapture(snapshot, out var layout, out _)
            || layout.ColumnCount != pending.currentColumns
            || layout.EntrancePosition.x != pending.entranceX || layout.EntrancePosition.y != pending.entranceY)
            throw new InvalidOperationException("Saved spatial publication differs from the saved layout.");
    }

    public void ValidateAppliedGrid(Grid grid)
    {
        if (grid == null || grid.width != data.currentWidth
            || !DungeonSpaceGridLayout.TryCapture(grid, out var layout, out _)
            || layout.ColumnCount != data.currentColumns
            || layout.EntrancePosition.x != data.entranceX || layout.EntrancePosition.y != data.entranceY)
            throw new InvalidOperationException("Pending dungeon-space transition differs from the authoritative layout.");
    }
}
