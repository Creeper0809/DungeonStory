using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IDungeonSaveCommandService
{
    bool TryQuickSave(out string message);
    bool TryQuickLoad(out string message);
    bool TryAutoSave(out string message);
}

public sealed class DungeonAutosaveService :
    IStartable,
    ITickable,
    IDisposable,
    IDungeonSaveCommandService
{
    private readonly IDungeonGameSaveSlotService slotService;
    private readonly IOwnerRunManagerProvider ownerRunManagerProvider;
    private readonly IGameEventBus gameEventBus;
    private IDisposable operatingDayReportSubscription;
    private bool started;
    private bool isSaving;
    private bool automaticSavePending;
    private IDungeonSaveOperation automaticSave;

    public DungeonAutosaveService(
        IDungeonGameSaveSlotService slotService,
        IOwnerRunManagerProvider ownerRunManagerProvider,
        IGameEventBus gameEventBus)
    {
        this.slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
        this.ownerRunManagerProvider = ownerRunManagerProvider
            ?? throw new ArgumentNullException(nameof(ownerRunManagerProvider));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        operatingDayReportSubscription =
            gameEventBus.Subscribe<OperatingDayReportEvent>(OnTriggerEvent);
        Application.quitting += HandleApplicationQuitting;
    }

    public void Dispose()
    {
        if (!started)
        {
            return;
        }

        try
        {
            slotService.CompletePendingSave();
            if (automaticSavePending && HasActiveRun())
                slotService.Save(DungeonGameSaveSlotService.AutoSaveSlot);
        }
        catch (Exception exception) { Debug.LogError("Pending autosave failed during shutdown: " + exception.Message); }
        automaticSavePending = false;
        operatingDayReportSubscription?.Dispose();
        operatingDayReportSubscription = null;
        Application.quitting -= HandleApplicationQuitting;
        started = false;
    }

    public void OnTriggerEvent(OperatingDayReportEvent eventType)
    {
        automaticSavePending = true;
        StartPendingAutosave();
    }

    public void Tick()
    {
        slotService.AdvanceSave();
        if (automaticSave != null && automaticSave.IsCompleted)
        {
            if (!automaticSave.Succeeded)
                Debug.LogWarning("Operating day autosave failed: " + automaticSave.Error);
            automaticSave = null;
        }
        StartPendingAutosave();
    }

    private void StartPendingAutosave()
    {
        if (!automaticSavePending || slotService.IsSaving) return;
        automaticSavePending = false;
        if (!HasActiveRun())
        {
            Debug.LogWarning("Autosave requires an active owner run.");
            return;
        }
        try { automaticSave = slotService.BeginSave(DungeonGameSaveSlotService.AutoSaveSlot); }
        catch (Exception exception) { Debug.LogWarning("Autosave failed: " + exception.Message); }
    }

    public bool TryQuickSave(out string message)
    {
        return TrySave(DungeonGameSaveSlotService.QuickSaveSlot, out message);
    }

    public bool TryQuickLoad(out string message)
    {
        try
        {
            bool loaded = slotService.TryLoad(
                DungeonGameSaveSlotService.QuickSaveSlot,
                out DungeonGameRestoreReport report);
            message = loaded
                ? $"Loaded {report.RestoredBuildingCount} buildings."
                : report.FailureDescription;
            return loaded;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return false;
        }
    }

    public bool TryAutoSave(out string message)
    {
        return TrySave(DungeonGameSaveSlotService.AutoSaveSlot, out message);
    }

    private bool TrySave(string slotId, out string message)
    {
        if (!HasActiveRun())
        {
            message = "사장을 선택한 진행 중인 런에서만 저장할 수 있습니다.";
            return false;
        }

        if (isSaving)
        {
            message = "A save is already in progress.";
            return false;
        }

        try
        {
            isSaving = true;
            slotService.CompletePendingSave();
            message = slotService.Save(slotId);
            if (slotId == DungeonGameSaveSlotService.AutoSaveSlot) automaticSavePending = false;
            return true;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return false;
        }
        finally
        {
            isSaving = false;
        }
    }

    private void HandleApplicationQuitting()
    {
        if (!HasActiveRun())
        {
            return;
        }

        if (!TryAutoSave(out string message))
        {
            Debug.LogWarning($"Exit autosave failed: {message}");
        }
    }

    private bool HasActiveRun()
    {
        return ownerRunManagerProvider.TryGetManager(out OwnerRunManager manager)
            && manager != null
            && manager.CurrentOwnerActor != null;
    }
}
