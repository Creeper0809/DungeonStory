using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonDebugCategory
{
    Cheats,
    Spawn,
    Character,
    BuildingWork,
    SurvivalWildlife,
    EventsDefense,
    Overlay,
    History
}

public enum DungeonDebugTargetKind
{
    None,
    GridCell,
    Character,
    Building,
    ItemPile,
    Wildlife
}

public enum DungeonDebugCheat
{
    FriendlyInvincible,
    FacilityInvincible,
    FreezeNeeds,
    PreventBreakdowns,
    NoMoneyOrItemCost,
    FreeConstruction,
    IgnorePlacementRules,
    InstantConstruction,
    InstantWork,
    IgnoreUnlocks,
    PauseHumanoidAi,
    PauseWildlifeAi
}

public enum DungeonDebugOverlayKind
{
    Grid,
    GridOccupancy,
    Rooms,
    BuildingRanges,
    Lighting,
    CharacterAi,
    Hauling,
    Wildlife,
    WaterAndFilth,
    ExteriorZones,
    Defense
}

public enum DungeonDebugOverlayScope
{
    SelectedOnly,
    VisibleWorld
}

[Serializable]
public sealed class DungeonDebugCommandHistorySaveData
{
    public string gameTime = string.Empty;
    public string commandId = string.Empty;
    public string target = string.Empty;
    public string result = string.Empty;
}

[Serializable]
public sealed class DungeonDebugRunSaveData
{
    public bool debugModified;
    public List<DungeonDebugCommandHistorySaveData> recentCommands =
        new List<DungeonDebugCommandHistorySaveData>();
}

public readonly struct DungeonDebugCommandResult
{
    private DungeonDebugCommandResult(bool success, string message)
    {
        Success = success;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public string Message { get; }

    public static DungeonDebugCommandResult Succeeded(string message)
    {
        return new DungeonDebugCommandResult(true, message);
    }

    public static DungeonDebugCommandResult Failed(string message)
    {
        return new DungeonDebugCommandResult(false, message);
    }
}

public sealed class DungeonDebugTargetSelection
{
    public DungeonDebugTargetKind Kind { get; set; }
    public bool HasGridPosition { get; set; }
    public Vector2Int GridPosition { get; set; }
    public CharacterActor Character { get; set; }
    public BuildableObject Building { get; set; }
    public WorldItemStackSnapshot ItemStack { get; set; }
    public WildlifeActor Wildlife { get; set; }
    public UnityEngine.Object SourceObject { get; set; }

    public string Describe()
    {
        return Kind switch
        {
            DungeonDebugTargetKind.Character =>
                Character?.Identity?.DisplayName ?? "캐릭터 없음",
            DungeonDebugTargetKind.Building =>
                Building?.BuildingData?.objectName ?? "건물 없음",
            DungeonDebugTargetKind.ItemPile =>
                ItemStack != null
                    ? $"{ItemStack.DisplayName} x{ItemStack.Quantity} ({GridPosition.x}, {GridPosition.y})"
                    : $"아이템 더미 ({GridPosition.x}, {GridPosition.y})",
            DungeonDebugTargetKind.Wildlife =>
                Wildlife != null ? Wildlife.DisplayName : "야생동물 없음",
            DungeonDebugTargetKind.GridCell =>
                HasGridPosition ? $"칸 ({GridPosition.x}, {GridPosition.y})" : "칸 없음",
            _ => "전체"
        };
    }

    public bool Matches(DungeonDebugTargetKind required)
    {
        return required switch
        {
            DungeonDebugTargetKind.None => true,
            DungeonDebugTargetKind.GridCell => HasGridPosition,
            DungeonDebugTargetKind.Character => Character != null,
            DungeonDebugTargetKind.Building => Building != null,
            DungeonDebugTargetKind.ItemPile => HasGridPosition && ItemStack != null,
            DungeonDebugTargetKind.Wildlife => Wildlife != null,
            _ => false
        };
    }
}

public sealed class DungeonDebugExecutionContext
{
    public DungeonDebugTargetSelection Target { get; set; } = new DungeonDebugTargetSelection();
    public float NumericValue { get; set; } = 10f;
    public string TextValue { get; set; } = string.Empty;
    public bool RepeatRequested { get; set; }
}

public interface IDungeonDebugCommand
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    DungeonDebugCategory Category { get; }
    DungeonDebugTargetKind TargetKind { get; }
    bool IsDangerous { get; }
    bool MutatesWorld { get; }
    float DefaultNumericValue { get; }
    DungeonDebugCommandResult Execute(DungeonDebugExecutionContext context);
}

public interface IDungeonDebugCommandProvider
{
    IEnumerable<IDungeonDebugCommand> GetCommands();
}

public interface IDungeonDebugCommandRegistry
{
    IReadOnlyList<IDungeonDebugCommand> Commands { get; }
    bool TryGet(string commandId, out IDungeonDebugCommand command);
    DungeonDebugCommandResult Execute(IDungeonDebugCommand command, DungeonDebugExecutionContext context);
}

public interface IDungeonDebugModeService
{
    bool IsDeveloperModeEnabled { get; }
    bool IsDebugModified { get; }
    DungeonDebugOverlayScope OverlayScope { get; }
    IReadOnlyList<DungeonDebugCommandHistorySaveData> RecentCommands { get; }
    event Action StateChanged;
    bool IsCheatEnabled(DungeonDebugCheat cheat);
    bool IsOverlayEnabled(DungeonDebugOverlayKind overlay);
    void SetCheat(DungeonDebugCheat cheat, bool enabled);
    void SetOverlay(DungeonDebugOverlayKind overlay, bool enabled);
    void SetOverlayScope(DungeonDebugOverlayScope scope);
    void MarkMutation(string commandId, string target, DungeonDebugCommandResult result);
    DungeonDebugRunSaveData Capture();
    void Restore(DungeonDebugRunSaveData data);
    void ResetTransientState();
}

public interface IDungeonDebugOverlayProvider
{
    DungeonDebugOverlayKind Kind { get; }
    void Render(DungeonDebugOverlayRenderContext context);
}

public sealed class DungeonDebugOverlayRenderContext
{
    public Grid Grid { get; set; }
    public Camera Camera { get; set; }
    public DungeonDebugOverlayScope Scope { get; set; }
    public DungeonDebugTargetSelection Selection { get; set; }
    public DungeonDebugOverlayRenderer Renderer { get; set; }
}

