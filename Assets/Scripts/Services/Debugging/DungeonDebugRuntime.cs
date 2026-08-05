using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using UnityEngine;
using VContainer.Unity;

public sealed class DungeonDebugModeService :
    IDungeonDebugModeService,
    IStartable,
    IDisposable
{
    private readonly IDungeonUserSettingsService settingsService;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CoreSessionRulesDefinition rules;

    private DungeonDebugModeState state
    {
        get => aggregateRootStore.GetOrCreate(() => new DungeonDebugModeState());
        set => aggregateRootStore.Replace(value);
    }

    private HashSet<DungeonDebugCheat> enabledCheats => state.EnabledCheats;
    private HashSet<DungeonDebugOverlayKind> enabledOverlays => state.EnabledOverlays;
    private List<DungeonDebugCommandHistorySaveData> recentCommands => state.RecentCommands;

    public DungeonDebugModeService(
        IDungeonUserSettingsService settingsService,
        IGameSessionStateProvider gameDataProvider,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICoreSessionRulesProvider rulesProvider)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        rules = (rulesProvider
                ?? throw new ArgumentNullException(nameof(rulesProvider)))
            .CoreSessionRules
            ?? throw new InvalidOperationException(
                "Core-session rules are not authored.");
    }

    public bool IsDeveloperModeEnabled => settingsService.Current.developerMode;
    public bool IsDebugModified => state.DebugModified;
    public DungeonDebugOverlayScope OverlayScope => state.OverlayScope;
    public IReadOnlyList<DungeonDebugCommandHistorySaveData> RecentCommands => recentCommands;
    public event Action StateChanged;

    public void Start()
    {
        settingsService.Changed += OnSettingsChanged;
        if (!IsDeveloperModeEnabled)
        {
            ResetTransientState();
        }
    }

    public void Dispose()
    {
        settingsService.Changed -= OnSettingsChanged;
        ResetTransientState();
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

        state.DebugModified = true;
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
        if (state.OverlayScope == scope)
        {
            return;
        }

        state.OverlayScope = scope;
        StateChanged?.Invoke();
    }

    public void MarkMutation(
        string commandId,
        string target,
        DungeonDebugCommandResult result)
    {
        state.DebugModified = true;
        AppendHistory(commandId, target, result.Message);
        StateChanged?.Invoke();
    }

    public DungeonDebugRunSaveData Capture()
    {
        return new DungeonDebugRunSaveData
        {
            debugModified = state.DebugModified,
            recentCommands = recentCommands
                .TakeLast(rules.DebugHistoryLimit)
                .Select(CloneHistory)
                .ToList()
        };
    }

    public DungeonDebugRestoreCandidate PrepareRestoreCandidate(
        DungeonDebugRunSaveData data)
    {
        if (data?.recentCommands == null)
        {
            throw new InvalidOperationException(
                "Dungeon-debug restore payload or history is missing.");
        }

        DungeonDebugModeState restored = new()
        {
            DebugModified = data.debugModified,
            OverlayScope = DungeonDebugOverlayScope.SelectedOnly
        };
        foreach (DungeonDebugCommandHistorySaveData entry in data.recentCommands)
        {
            restored.RecentCommands.Add(CloneHistory(entry));
        }

        return new DungeonDebugRestoreCandidate(restored, data);
    }

    public void PublishRestoreCandidate(DungeonDebugRestoreCandidate candidate)
    {
        state = (candidate
                ?? throw new ArgumentNullException(nameof(candidate)))
            .State;
        if (!aggregateRootStore.IsRestoreStaging)
        {
            StateChanged?.Invoke();
        }
    }

    public void ResetTransientState()
    {
        bool changed = enabledCheats.Count > 0 || enabledOverlays.Count > 0;
        enabledCheats.Clear();
        enabledOverlays.Clear();
        state.OverlayScope = DungeonDebugOverlayScope.SelectedOnly;
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
        if (recentCommands.Count > rules.DebugHistoryLimit)
        {
            recentCommands.RemoveRange(
                0,
                recentCommands.Count - rules.DebugHistoryLimit);
        }
    }

    private string ResolveGameTime()
    {
        if (!gameDataProvider.TryGetSessionState(out GameSessionState gameData))
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
    private readonly IDungeonDebugRuleRuntime ruleRuntime;
    private readonly List<IDungeonDebugCommand> commands;
    private readonly Dictionary<string, IDungeonDebugCommand> byId;

    public DungeonDebugCommandRegistry(
        IDungeonDebugModeService modeService,
        IDungeonDebugRuleRuntime ruleRuntime,
        IEnumerable<IDungeonDebugCommandProvider> providers)
    {
        this.modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
        this.ruleRuntime = ruleRuntime
            ?? throw new ArgumentNullException(nameof(ruleRuntime));

        commands = (providers ?? throw new ArgumentNullException(nameof(providers)))
            .Where(provider => provider != null)
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
            ruleRuntime.BeginCommandExecution();
            result = command.Execute(context);
        }
        catch (Exception exception)
        {
            result = DungeonDebugCommandResult.Failed(exception.Message);
        }
        finally
        {
            ruleRuntime.EndCommandExecution();
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

public sealed class DungeonDebugRuleRuntime : IDungeonDebugRuleRuntime
{
    private readonly IDungeonDebugModeService modeService;
    private int commandExecutionDepth;

    public DungeonDebugRuleRuntime(IDungeonDebugModeService modeService)
    {
        this.modeService = modeService
            ?? throw new ArgumentNullException(nameof(modeService));
    }

    public bool IsExecutingCommand => commandExecutionDepth > 0;

    public bool IsEnabled(DungeonDebugCheat cheat)
    {
        return modeService.IsCheatEnabled(cheat);
    }

    public bool ShouldFreezeNeed(CharacterCondition condition, float delta)
    {
        return !IsExecutingCommand
            && delta < 0f
            && condition != CharacterCondition.MOOD
            && IsEnabled(DungeonDebugCheat.FreezeNeeds);
    }

    public bool ShouldBlockFriendlyDamage(CharacterActor actor)
    {
        return !IsExecutingCommand
            && actor != null
            && actor.characterType == CharacterType.NPC
            && IsEnabled(DungeonDebugCheat.FriendlyInvincible);
    }

    public bool ShouldBlockFacilityDamage(bool damaged)
    {
        return !IsExecutingCommand
            && damaged
            && IsEnabled(DungeonDebugCheat.FacilityInvincible);
    }

    public bool ShouldSkipCosts()
    {
        return IsEnabled(DungeonDebugCheat.NoMoneyOrItemCost);
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
