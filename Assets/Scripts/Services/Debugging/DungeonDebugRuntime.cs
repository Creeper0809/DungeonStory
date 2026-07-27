using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public sealed class DungeonDebugModeService :
    IDungeonDebugModeService,
    IStartable,
    IDisposable
{
    private const int HistoryLimit = 50;

    private readonly IDungeonUserSettingsService settingsService;
    private readonly IGameDataProvider gameDataProvider;
    private readonly HashSet<DungeonDebugCheat> enabledCheats = new HashSet<DungeonDebugCheat>();
    private readonly HashSet<DungeonDebugOverlayKind> enabledOverlays =
        new HashSet<DungeonDebugOverlayKind>();
    private readonly List<DungeonDebugCommandHistorySaveData> recentCommands =
        new List<DungeonDebugCommandHistorySaveData>();
    private bool debugModified;
    private DungeonDebugOverlayScope overlayScope = DungeonDebugOverlayScope.SelectedOnly;

    public DungeonDebugModeService(
        IDungeonUserSettingsService settingsService,
        IGameDataProvider gameDataProvider)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
    }

    public bool IsDeveloperModeEnabled => settingsService.Current.developerMode;
    public bool IsDebugModified => debugModified;
    public DungeonDebugOverlayScope OverlayScope => overlayScope;
    public IReadOnlyList<DungeonDebugCommandHistorySaveData> RecentCommands => recentCommands;
    public event Action StateChanged;

    public void Start()
    {
        DungeonDebugRuntimeRules.Attach(this);
        DungeonUserSettingsRuntime.Changed += OnSettingsChanged;
        if (!IsDeveloperModeEnabled)
        {
            ResetTransientState();
        }
    }

    public void Dispose()
    {
        DungeonUserSettingsRuntime.Changed -= OnSettingsChanged;
        ResetTransientState();
        DungeonDebugRuntimeRules.Detach(this);
    }

    public bool IsCheatEnabled(DungeonDebugCheat cheat)
    {
        return IsDeveloperModeEnabled && enabledCheats.Contains(cheat);
    }

    public bool IsOverlayEnabled(DungeonDebugOverlayKind overlay)
    {
        return IsDeveloperModeEnabled && enabledOverlays.Contains(overlay);
    }

    public void SetCheat(DungeonDebugCheat cheat, bool enabled)
    {
        if (!IsDeveloperModeEnabled)
        {
            return;
        }

        bool changed = enabled ? enabledCheats.Add(cheat) : enabledCheats.Remove(cheat);
        if (!changed)
        {
            return;
        }

        debugModified = true;
        AppendHistory(
            $"cheat:{cheat}",
            "전체",
            enabled ? "활성화" : "비활성화");
        StateChanged?.Invoke();
    }

    public void SetOverlay(DungeonDebugOverlayKind overlay, bool enabled)
    {
        if (!IsDeveloperModeEnabled)
        {
            return;
        }

        bool changed = enabled ? enabledOverlays.Add(overlay) : enabledOverlays.Remove(overlay);
        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    public void SetOverlayScope(DungeonDebugOverlayScope scope)
    {
        if (overlayScope == scope)
        {
            return;
        }

        overlayScope = scope;
        StateChanged?.Invoke();
    }

    public void MarkMutation(
        string commandId,
        string target,
        DungeonDebugCommandResult result)
    {
        debugModified = true;
        AppendHistory(commandId, target, result.Message);
        StateChanged?.Invoke();
    }

    public DungeonDebugRunSaveData Capture()
    {
        return new DungeonDebugRunSaveData
        {
            debugModified = debugModified,
            recentCommands = recentCommands
                .TakeLast(HistoryLimit)
                .Select(CloneHistory)
                .ToList()
        };
    }

    public void Restore(DungeonDebugRunSaveData data)
    {
        enabledCheats.Clear();
        enabledOverlays.Clear();
        overlayScope = DungeonDebugOverlayScope.SelectedOnly;
        debugModified = data != null && data.debugModified;
        recentCommands.Clear();
        foreach (DungeonDebugCommandHistorySaveData entry in
                 data?.recentCommands?.TakeLast(HistoryLimit)
                 ?? Enumerable.Empty<DungeonDebugCommandHistorySaveData>())
        {
            if (entry != null)
            {
                recentCommands.Add(CloneHistory(entry));
            }
        }

        StateChanged?.Invoke();
    }

    public void ResetTransientState()
    {
        bool changed = enabledCheats.Count > 0 || enabledOverlays.Count > 0;
        enabledCheats.Clear();
        enabledOverlays.Clear();
        overlayScope = DungeonDebugOverlayScope.SelectedOnly;
        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    private void OnSettingsChanged()
    {
        if (!IsDeveloperModeEnabled)
        {
            ResetTransientState();
        }

        StateChanged?.Invoke();
    }

    private void AppendHistory(string commandId, string target, string result)
    {
        recentCommands.Add(new DungeonDebugCommandHistorySaveData
        {
            gameTime = ResolveGameTime(),
            commandId = commandId ?? string.Empty,
            target = target ?? string.Empty,
            result = result ?? string.Empty
        });
        if (recentCommands.Count > HistoryLimit)
        {
            recentCommands.RemoveRange(0, recentCommands.Count - HistoryLimit);
        }
    }

    private string ResolveGameTime()
    {
        if (!gameDataProvider.TryGetGameData(out GameData gameData))
        {
            return "월드 준비 전";
        }

        return $"{Math.Max(1, gameData.day?.Value ?? 1)}일 "
            + $"{Math.Max(0, gameData.hour?.Value ?? 0):00}:00";
    }

    private static DungeonDebugCommandHistorySaveData CloneHistory(
        DungeonDebugCommandHistorySaveData source)
    {
        return new DungeonDebugCommandHistorySaveData
        {
            gameTime = source?.gameTime ?? string.Empty,
            commandId = source?.commandId ?? string.Empty,
            target = source?.target ?? string.Empty,
            result = source?.result ?? string.Empty
        };
    }
}

public sealed class DelegateDungeonDebugCommand : IDungeonDebugCommand
{
    private readonly Func<DungeonDebugExecutionContext, DungeonDebugCommandResult> execute;

    public DelegateDungeonDebugCommand(
        string id,
        string displayName,
        string description,
        DungeonDebugCategory category,
        DungeonDebugTargetKind targetKind,
        Func<DungeonDebugExecutionContext, DungeonDebugCommandResult> execute,
        bool mutatesWorld = true,
        bool isDangerous = false,
        float defaultNumericValue = 10f)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? id;
        Description = description ?? string.Empty;
        Category = category;
        TargetKind = targetKind;
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        MutatesWorld = mutatesWorld;
        IsDangerous = isDangerous;
        DefaultNumericValue = defaultNumericValue;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public DungeonDebugCategory Category { get; }
    public DungeonDebugTargetKind TargetKind { get; }
    public bool IsDangerous { get; }
    public bool MutatesWorld { get; }
    public float DefaultNumericValue { get; }

    public DungeonDebugCommandResult Execute(DungeonDebugExecutionContext context)
    {
        return execute(context ?? new DungeonDebugExecutionContext());
    }
}

public sealed class DungeonDebugCommandRegistry : IDungeonDebugCommandRegistry
{
    private readonly IDungeonDebugModeService modeService;
    private readonly List<IDungeonDebugCommand> commands;
    private readonly Dictionary<string, IDungeonDebugCommand> byId;

    public DungeonDebugCommandRegistry(
        IDungeonDebugModeService modeService,
        DungeonDebugCheatCommandProvider cheatProvider,
        DungeonDebugEconomyCommandProvider economyProvider,
        DungeonDebugItemCommandProvider itemProvider,
        DungeonDebugCharacterCommandProvider characterProvider,
        DungeonDebugWorkCommandProvider workProvider,
        DungeonDebugSurvivalWildlifeCommandProvider survivalProvider,
        DungeonDebugDefenseCommandProvider defenseProvider,
        DungeonDebugOverlayCommandProvider overlayProvider,
        DungeonDebugPerformanceCommandProvider performanceProvider)
    {
        this.modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
        IDungeonDebugCommandProvider[] providers =
        {
            cheatProvider,
            economyProvider,
            itemProvider,
            characterProvider,
            workProvider,
            survivalProvider,
            defenseProvider,
            overlayProvider,
            performanceProvider
        };

        commands = providers
            .SelectMany(provider => provider.GetCommands() ?? Enumerable.Empty<IDungeonDebugCommand>())
            .OrderBy(command => command.Category)
            .ThenBy(command => command.DisplayName, StringComparer.CurrentCulture)
            .ToList();
        byId = new Dictionary<string, IDungeonDebugCommand>(StringComparer.Ordinal);
        foreach (IDungeonDebugCommand command in commands)
        {
            if (!byId.TryAdd(command.Id, command))
            {
                throw new InvalidOperationException($"중복 디버그 명령 ID: {command.Id}");
            }
        }
    }

    public IReadOnlyList<IDungeonDebugCommand> Commands => commands;

    public bool TryGet(string commandId, out IDungeonDebugCommand command)
    {
        return byId.TryGetValue(commandId ?? string.Empty, out command);
    }

    public DungeonDebugCommandResult Execute(
        IDungeonDebugCommand command,
        DungeonDebugExecutionContext context)
    {
        if (!modeService.IsDeveloperModeEnabled)
        {
            return DungeonDebugCommandResult.Failed("개발자 모드가 꺼져 있습니다.");
        }

        if (command == null)
        {
            return DungeonDebugCommandResult.Failed("명령을 찾을 수 없습니다.");
        }

        context ??= new DungeonDebugExecutionContext();
        if (!context.Target.Matches(command.TargetKind))
        {
            return DungeonDebugCommandResult.Failed($"정확한 {TargetLabel(command.TargetKind)} 대상이 필요합니다.");
        }

        DungeonDebugCommandResult result;
        try
        {
            DungeonDebugRuntimeRules.BeginCommandExecution();
            result = command.Execute(context);
        }
        catch (Exception exception)
        {
            result = DungeonDebugCommandResult.Failed(exception.Message);
        }
        finally
        {
            DungeonDebugRuntimeRules.EndCommandExecution();
        }

        if (result.Success && command.MutatesWorld)
        {
            modeService.MarkMutation(command.Id, context.Target.Describe(), result);
        }

        return result;
    }

    private static string TargetLabel(DungeonDebugTargetKind targetKind)
    {
        return targetKind switch
        {
            DungeonDebugTargetKind.GridCell => "그리드 칸",
            DungeonDebugTargetKind.Character => "캐릭터",
            DungeonDebugTargetKind.Building => "건물",
            DungeonDebugTargetKind.ItemPile => "아이템",
            DungeonDebugTargetKind.Wildlife => "야생동물",
            _ => "월드"
        };
    }
}

public static class DungeonDebugRuntimeRules
{
    private static readonly DungeonDebugRuntimeRuleState State = new DungeonDebugRuntimeRuleState();

    public static bool IsExecutingCommand => State.IsExecutingCommand;

    public static bool IsEnabled(DungeonDebugCheat cheat)
    {
        return State.IsEnabled(cheat);
    }

    public static void Attach(IDungeonDebugModeService service)
    {
        State.Attach(service);
    }

    public static void Detach(IDungeonDebugModeService service)
    {
        State.Detach(service);
    }

    public static bool ShouldFreezeNeed(CharacterCondition condition, float delta)
    {
        return !IsExecutingCommand
            && delta < 0f
            && condition != CharacterCondition.MOOD
            && IsEnabled(DungeonDebugCheat.FreezeNeeds);
    }

    public static bool ShouldBlockFriendlyDamage(CharacterActor actor)
    {
        return !IsExecutingCommand
            && actor != null
            && actor.characterType == CharacterType.NPC
            && IsEnabled(DungeonDebugCheat.FriendlyInvincible);
    }

    public static bool ShouldBlockFacilityDamage(bool damaged)
    {
        return !IsExecutingCommand
            && damaged
            && IsEnabled(DungeonDebugCheat.FacilityInvincible);
    }

    public static bool ShouldSkipCosts()
    {
        return IsEnabled(DungeonDebugCheat.NoMoneyOrItemCost);
    }

    public static void BeginCommandExecution()
    {
        State.BeginCommandExecution();
    }

    public static void EndCommandExecution()
    {
        State.EndCommandExecution();
    }

    private sealed class DungeonDebugRuntimeRuleState
    {
        [ThreadStatic] private static int commandExecutionDepth;
        private IDungeonDebugModeService modeService;

        public bool IsExecutingCommand => commandExecutionDepth > 0;

        public bool IsEnabled(DungeonDebugCheat cheat)
        {
            return modeService?.IsCheatEnabled(cheat) == true;
        }

        public void Attach(IDungeonDebugModeService service)
        {
            modeService = service;
        }

        public void Detach(IDungeonDebugModeService service)
        {
            if (ReferenceEquals(modeService, service))
            {
                modeService = null;
            }
        }

        public void BeginCommandExecution()
        {
            commandExecutionDepth++;
        }

        public void EndCommandExecution()
        {
            commandExecutionDepth = Mathf.Max(0, commandExecutionDepth - 1);
        }
    }
}
