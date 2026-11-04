using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DungeonSaveSlotCatalog : IDungeonSaveSlotCatalog
{
    private readonly string saveDirectory;

    [VContainer.Inject]
    public DungeonSaveSlotCatalog()
        : this(Path.Combine(DungeonPersistencePaths.Root, "Saves"))
    {
    }

    internal DungeonSaveSlotCatalog(string saveDirectory)
    {
        this.saveDirectory = string.IsNullOrWhiteSpace(saveDirectory)
            ? throw new ArgumentException("Save directory is required.", nameof(saveDirectory))
            : saveDirectory;
    }

    public bool HasSave(string slotId)
    {
        return File.Exists(GetPath(slotId));
    }

    public IReadOnlyList<DungeonSaveSlotInfo> GetSlots()
    {
        if (!Directory.Exists(saveDirectory))
        {
            return Array.Empty<DungeonSaveSlotInfo>();
        }

        List<DungeonSaveSlotInfo> slots = new List<DungeonSaveSlotInfo>();
        foreach (string path in Directory.GetFiles(saveDirectory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            string slotId = Path.GetFileNameWithoutExtension(path);

            try
            {
                DungeonSaveSlotHeaderData data = JsonUtility.FromJson<DungeonSaveSlotHeaderData>(File.ReadAllText(path));
                GameSessionSaveData session = ReadSectionPayload<GameSessionSaveData>(
                    data?.sections,
                    FoundationSessionSaveSection.Id);
                DungeonDebugRunSaveData debug = ReadSectionPayload<DungeonDebugRunSaveData>(
                    data?.sections,
                    DungeonDebugSaveSection.Id);
                DungeonRunVariableSaveData runVariables =
                    ReadSectionPayload<DungeonRunVariableSaveData>(
                        data?.sections,
                        RunVariableSaveSection.Id);
                string incompatibilityReason = GetIncompatibilityReason(data);
                slots.Add(new DungeonSaveSlotInfo(
                    new DungeonSaveSlotLocation(slotId, path),
                    new DungeonSaveSlotSummary(
                        data?.savedAtUtc,
                        data?.sceneName,
                        session?.absoluteDay ?? 1,
                        session?.money ?? 0,
                        debug != null && debug.debugModified,
                        (int)(runVariables?.startVariables?.survivalPressure
                            ?? DungeonSurvivalPressure.Standard)),
                    new DungeonSaveSlotCompatibility(
                        data != null
                            && data.version == DungeonGameSaveData.CurrentVersion
                            && string.IsNullOrWhiteSpace(incompatibilityReason),
                        incompatibilityReason)));
            }
            catch
            {
                slots.Add(new DungeonSaveSlotInfo(slotId, path));
            }
        }

        return slots;
    }

    private static string GetIncompatibilityReason(DungeonSaveSlotHeaderData data)
    {
        if (data == null)
        {
            return "저장 파일을 읽을 수 없습니다.";
        }

        if (DungeonSaveCompatibility.TryGetIncompatibilityReason(
                data.version,
                out string incompatibilityReason))
        {
            return incompatibilityReason;
        }

        return DungeonSaveManifest.TryValidate(
            data.manifest,
            data.sections,
            out string manifestError)
                ? string.Empty
                : manifestError;
    }

    public bool Delete(string slotId)
    {
        string path = GetPath(slotId);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public string GetPath(string slotId)
    {
        return Path.Combine(saveDirectory, NormalizeSlotId(slotId) + ".json");
    }

    private static string NormalizeSlotId(string slotId)
    {
        string normalized = (slotId ?? string.Empty).Trim();
        if (normalized.Length == 0
            || normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
        {
            throw new ArgumentException("Save slot ids may only contain letters, numbers, '-' and '_'.", nameof(slotId));
        }

        return normalized;
    }

    private static TPayload ReadSectionPayload<TPayload>(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId)
        where TPayload : class
    {
        DungeonSaveSectionEnvelope envelope = envelopes?.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sectionId?.Trim(),
                sectionId,
                StringComparison.Ordinal));
        return envelope == null || string.IsNullOrWhiteSpace(envelope.payloadJson)
            ? null
            : JsonUtility.FromJson<TPayload>(envelope.payloadJson);
    }

    [Serializable]
    private sealed class DungeonSaveSlotHeaderData
    {
        public int version;
        public string savedAtUtc;
        public string sceneName;
        public DungeonSaveManifestData manifest;
        public List<DungeonSaveSectionEnvelope> sections;
    }
}

public sealed class DungeonGameSaveService : IDungeonGameSaveService
{
    private readonly IDungeonSaveSectionRegistry saveSectionRegistry;
    private readonly IReadOnlyList<IDungeonSavePreflightValidator> preflightValidators;
    private readonly IReadOnlyList<IDungeonSaveCaptureGuard> captureGuards;
    private readonly IReadOnlyList<IDungeonCapturedSavePreflightValidator>
        capturedSaveValidators;
    private readonly IReadOnlyList<IDungeonSaveRestoreCompletedHook> restoreCompletedHooks;
    private readonly DungeonRestoreSafetyRuntime restoreSafety;

    public DungeonGameSaveService(
        IDungeonSaveSectionRegistry saveSectionRegistry,
        IEnumerable<IDungeonSavePreflightValidator> preflightValidators,
        IEnumerable<IDungeonSaveCaptureGuard> captureGuards,
        IEnumerable<IDungeonCapturedSavePreflightValidator>
            capturedSaveValidators,
        IEnumerable<IDungeonSaveRestoreCompletedHook> restoreCompletedHooks,
        DungeonRestoreSafetyRuntime restoreSafety)
    {
        this.saveSectionRegistry = saveSectionRegistry
            ?? throw new ArgumentNullException(nameof(saveSectionRegistry));
        this.restoreSafety = restoreSafety
            ?? throw new ArgumentNullException(nameof(restoreSafety));
        this.preflightValidators = (preflightValidators
                ?? throw new ArgumentNullException(nameof(preflightValidators)))
            .Where(validator => validator != null)
            .ToArray();
        this.captureGuards = (captureGuards
                ?? throw new ArgumentNullException(nameof(captureGuards)))
            .Where(guard => guard != null)
            .ToArray();
        this.capturedSaveValidators = (capturedSaveValidators
                ?? throw new ArgumentNullException(
                    nameof(capturedSaveValidators)))
            .Where(validator => validator != null)
            .OrderBy(validator => validator.GetType().FullName,
                StringComparer.Ordinal)
            .ToArray();
        this.restoreCompletedHooks = (restoreCompletedHooks
                ?? throw new ArgumentNullException(nameof(restoreCompletedHooks)))
            .Where(hook => hook != null)
            .ToArray();
    }

    private readonly int ownerThreadId = Thread.CurrentThread.ManagedThreadId;

    public DungeonGameSaveData Capture()
    {
        using IDungeonGameSaveCapture capture = CreateCapture(useDetachedValidators: false);
        while (!capture.IsCompleted) capture.Advance();
        return capture.Result;
    }

    public IDungeonGameSaveCapture BeginCapture() => CreateCapture(useDetachedValidators: true);

    internal static bool IsWaitingForCaptureWork(IDungeonGameSaveCapture capture) =>
        capture is CaptureOperation operation && operation.IsWaitingForDetachedWork;

    private CaptureOperation CreateCapture(bool useDetachedValidators)
    {
        if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("Save capture must run on its owning thread.");
        return new CaptureOperation(this, useDetachedValidators);
    }

    private DungeonGameSaveData CaptureSnapshot()
    {
        restoreSafety.RequireSafeState("Save capture is blocked");
        DungeonSaveEditorTrace.Record("capture.begin");
        for (int index = 0; index < captureGuards.Count; index++)
        {
            captureGuards[index].ValidateBeforeCapture();
        }

        List<DungeonSaveSectionEnvelope> sections = saveSectionRegistry.CaptureAll();
        DungeonGameSaveData save = new DungeonGameSaveData
        {
            version = DungeonGameSaveData.CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            sceneName = SceneManager.GetActiveScene().name,
            manifest = DungeonSaveManifest.Capture(sections),
            sections = sections
        };

        return save;
    }

    private sealed class CaptureOperation : IDungeonGameSaveCapture
    {
        private readonly DungeonGameSaveService owner;
        private readonly DungeonSaveJson.ReadSession reads = new();
        private readonly DungeonGameSaveData snapshot;
        private readonly string snapshotSha;
        private readonly DungeonGameRestoreReport report = new();
        private readonly bool useDetachedValidators;
        private Task detachedValidation;
        private readonly CancellationTokenSource stopValidation = new();
        private readonly ManualResetEventSlim mainValidatorCompleted = new(false);
        private int pendingMainValidator = -1;
        private bool detachedTaskObserved;
        private bool detachedCompletionRecorded;
        private int nextValidator;
        private bool disposed;
        public bool IsCompleted { get; private set; }
        public bool IsWaitingForDetachedWork => useDetachedValidators && detachedValidation != null
            && !detachedValidation.IsCompleted && Volatile.Read(ref pendingMainValidator) < 0;
        public DungeonGameSaveData Result => IsCompleted && !disposed ? snapshot
            : throw new InvalidOperationException("Save capture has not completed.");

        public CaptureOperation(DungeonGameSaveService owner, bool allowDetachedValidators)
        {
            this.owner = owner;
            useDetachedValidators = allowDetachedValidators
                && owner.capturedSaveValidators.Any(value =>
                    value is IDungeonDetachedCapturedSavePreflightValidator);
            try
            {
                using (reads.Activate()) snapshot = owner.CaptureSnapshot();
                snapshotSha = snapshot.manifest.snapshotSha256;
            }
            catch
            {
                reads.Dispose();
                mainValidatorCompleted.Dispose();
                stopValidation.Dispose();
                throw;
            }
        }

        public void Advance()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CaptureOperation));
            if (IsCompleted) return;
            using var activation = reads.Activate();
            if (useDetachedValidators)
            {
                if (!AdvanceDetachedValidation()) return;
            }
            else if (nextValidator < owner.capturedSaveValidators.Count)
            {
                ValidateOne(nextValidator++, report, traceOnMainThread: true);
                return;
            }
            if (!report.Success)
                throw new InvalidOperationException("Captured save failed aggregate preflight: "
                    + string.Join(" | ", report.Errors));
            if (snapshot.manifest == null
                || snapshot.manifest.payloadHashVersion != DungeonSavePayloadIntegrity.CurrentVersion
                || snapshot.manifest.snapshotSha256 != snapshotSha)
                throw new InvalidOperationException("Captured save integrity failed: preflight changed the snapshot seal.");
            if (!DungeonSaveManifest.TryValidate(snapshot.manifest, snapshot.sections, out string failure))
                throw new InvalidOperationException("Captured save integrity failed: " + failure);
            IsCompleted = true;
        }

        private bool AdvanceDetachedValidation()
        {
            if (detachedValidation == null)
            {
                DungeonSaveEditorTrace.Record("capture-detached-preflight.queued");
                Func<DungeonSaveJson.ReadSession> createWorkerReads = reads.PrepareDetachedContinuation();
                detachedValidation = Task.Run(() =>
                {
                    using var workerReads = createWorkerReads();
                    using var activation = workerReads.Activate();
                    for (int index = 0; index < owner.capturedSaveValidators.Count; index++)
                    {
                        stopValidation.Token.ThrowIfCancellationRequested();
                        if (owner.capturedSaveValidators[index] is IDungeonDetachedCapturedSavePreflightValidator)
                            ValidateOne(index, report, traceOnMainThread: false);
                        else
                        {
                            // 공유 report의 앞선 결과까지 보존한다. 두 스레드가
                            // 동시에 DTO나 report를 검사하지 않게 순서대로 넘긴다.
                            mainValidatorCompleted.Reset();
                            Volatile.Write(ref pendingMainValidator, index);
                            mainValidatorCompleted.Wait(stopValidation.Token);
                        }
                    }
                });
                return false;
            }
            int mainIndex = Volatile.Read(ref pendingMainValidator);
            if (mainIndex >= 0)
            {
                try { ValidateOne(mainIndex, report, traceOnMainThread: true); }
                finally
                {
                    Volatile.Write(ref pendingMainValidator, -1);
                    mainValidatorCompleted.Set();
                }
                return false;
            }
            if (!detachedValidation.IsCompleted) return false;
            ObserveDetachedTask();
            if (!detachedCompletionRecorded)
            {
                DungeonSaveEditorTrace.Record("capture-detached-preflight.after");
                detachedCompletionRecorded = true;
            }
            return true;
        }

        private void ValidateOne(int index, DungeonGameRestoreReport target, bool traceOnMainThread)
        {
            IDungeonCapturedSavePreflightValidator validator = owner.capturedSaveValidators[index];
            try
            {
                if (traceOnMainThread)
                    DungeonSaveEditorTrace.Record("capture-preflight.before:" + validator.GetType().Name);
                validator.Validate(snapshot, target);
                if (traceOnMainThread)
                    DungeonSaveEditorTrace.Record("capture-preflight.after:" + validator.GetType().Name);
            }
            catch (Exception exception)
            {
                target.AddError($"Captured-save aggregate preflight '{validator.GetType().Name}' failed: {exception.Message}");
            }
        }

        private void ObserveDetachedTask()
        {
            try { detachedValidation.GetAwaiter().GetResult(); }
            finally { detachedTaskObserved = true; }
        }

        public void Dispose()
        {
            if (disposed) return;
            if (Thread.CurrentThread.ManagedThreadId != owner.ownerThreadId)
                throw new InvalidOperationException("Save read session must stay on its owning thread.");
            try
            {
                stopValidation.Cancel();
                if (detachedValidation != null && !detachedTaskObserved)
                {
                    try { ObserveDetachedTask(); }
                    catch (OperationCanceledException) when (stopValidation.IsCancellationRequested) { }
                }
            }
            finally
            {
                reads.Dispose();
                mainValidatorCompleted.Dispose();
                stopValidation.Dispose();
                disposed = true;
            }
        }
    }

    public string ToJson(DungeonGameSaveData saveData, bool prettyPrint = false)
    {
        if (Thread.CurrentThread.ManagedThreadId == ownerThreadId)
            DungeonSaveEditorTrace.Record("serialize.before");
        string json = JsonUtility.ToJson(saveData ?? new DungeonGameSaveData(), prettyPrint);
        if (Thread.CurrentThread.ManagedThreadId == ownerThreadId)
            DungeonSaveEditorTrace.Record("serialize.after");
        return json;
    }

    public DungeonGameSaveData FromJson(string json)
    {
        DungeonSaveEditorTrace.Record("deserialize.before");
        DungeonGameSaveData result = string.IsNullOrWhiteSpace(json)
            ? new DungeonGameSaveData()
            : JsonUtility.FromJson<DungeonGameSaveData>(json) ?? new DungeonGameSaveData();
        DungeonSaveEditorTrace.Record("deserialize.after");
        return result;
    }

    public bool TryRestore(DungeonGameSaveData saveData, out DungeonGameRestoreReport report)
    {
        using var reads = DungeonSaveJson.BeginReadScope();
        report = new DungeonGameRestoreReport();
        if (restoreSafety.RequiresRecovery)
        {
            report.AddError(restoreSafety.RecoveryMessage);
            return false;
        }
        try { return TryRestoreCore(saveData, report); }
        catch (Exception exception)
        {
            report.AddError(exception.Message);
            return false;
        }
        finally { restoreSafety.ObserveRestoreResult(report); }
    }

    private bool TryRestoreCore(DungeonGameSaveData saveData, DungeonGameRestoreReport report)
    {
        DungeonSaveEditorTrace.Record("restore.begin");
        if (saveData == null)
        {
            report.AddError("Save data is null.");
            return false;
        }

        if (saveData.version != DungeonGameSaveData.CurrentVersion)
        {
            DungeonSaveCompatibility.TryGetIncompatibilityReason(
                saveData.version,
                out string incompatibilityReason);
            report.AddError(incompatibilityReason);
            return false;
        }

        if (!DungeonSaveManifest.TryValidate(
                saveData.manifest,
                saveData.sections,
                out string manifestError))
        {
            report.AddError(manifestError);
            return false;
        }

        int payloadHashVersion = saveData.manifest.payloadHashVersion;
        string snapshotSha256 = saveData.manifest.snapshotSha256;
        if (payloadHashVersion == 0)
            report.AddWarning("LegacySnapshotIntegrityAbsent: this legacy V24 save has no payload seal; "
                + "content integrity is unverified. Domain preflight still applies; the input is not resealed.");

        foreach (IDungeonSavePreflightValidator validator in preflightValidators)
        {
            try
            {
                DungeonSaveEditorTrace.Record("restore-preflight.before:" + validator.GetType().Name);
                validator.Validate(saveData, report);
                DungeonSaveEditorTrace.Record("restore-preflight.after:" + validator.GetType().Name);
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Save aggregate preflight '{validator.GetType().Name}' failed: {exception.Message}");
            }
        }

        if (!report.Success)
        {
            return false;
        }

        if (saveData.manifest == null
            || saveData.manifest.payloadHashVersion != payloadHashVersion
            || saveData.manifest.snapshotSha256 != snapshotSha256)
        {
            report.AddError("Save preflight changed the snapshot seal.");
            return false;
        }
        if (!DungeonSaveManifest.TryValidate(saveData.manifest, saveData.sections, out manifestError))
        {
            report.AddError("Save preflight changed snapshot content: " + manifestError);
            return false;
        }

        try
        {
            if (!saveSectionRegistry.RestoreAll(saveData.sections, report))
            {
                return false;
            }
            for (int index = 0; index < restoreCompletedHooks.Count; index++)
            {
                IDungeonSaveRestoreCompletedHook hook = restoreCompletedHooks[index];
                try
                {
                    DungeonSaveEditorTrace.Record("restore-hook.before:" + hook.GetType().Name);
                    hook.OnRestoreCompleted();
                    DungeonSaveEditorTrace.Record("restore-hook.after:" + hook.GetType().Name);
                }
                catch (Exception exception)
                {
                    report.AddError("Published restore hook '" + hook.GetType().Name
                        + "' failed: " + exception.Message);
                }
            }
        }
        catch (Exception exception)
        {
            report.AddError(exception.Message);
        }

        return report.Success;
    }
}

public sealed class DungeonGameSaveSlotService : IDungeonGameSaveSlotService, IDisposable
{
    public const string AutoSaveSlot = "autosave";
    public const string QuickSaveSlot = "quicksave";
    public const string ManualSaveSlot = "manual";
    private const double ValidationSliceSeconds = 0.008;

    private readonly IDungeonGameSaveService saveService;
    private readonly IDungeonSaveSlotCatalog slotCatalog;
    private readonly IDungeonDurableSaveCommitCoordinator durableSaveCommits;
    private readonly IDungeonAtomicSaveFilePort filePort;
    private readonly Func<double> validationClock;
    private readonly DungeonStory.Foundation.IGameTimeScalePauseAuthority simulationPause;
    private readonly int ownerThreadId = Thread.CurrentThread.ManagedThreadId;
    private SaveOperation pendingSave;
    private IDisposable failedSimulationHold;
    private bool synchronousSave;
    private bool disposed;
    public bool IsSaving => pendingSave != null || synchronousSave || failedSimulationHold != null;


    public DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IPreparedOutputCheckpointGcCoordinator checkpointGc)
        : this(
            saveService,
            slotCatalog,
            CreateLegacyDurableSaveCoordinator(checkpointGc),
            new DungeonAtomicSaveFilePort())
    {
    }

    public DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IDungeonDurableSaveCommitCoordinator durableSaveCommits)
        : this(
            saveService,
            slotCatalog,
            durableSaveCommits,
            new DungeonAtomicSaveFilePort())
    {
    }

    [VContainer.Inject]
    public DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IDungeonDurableSaveCommitCoordinator durableSaveCommits,
        DungeonStory.Foundation.IGameTimeScalePauseAuthority simulationPause)
        : this(saveService, slotCatalog, durableSaveCommits, new DungeonAtomicSaveFilePort(),
            null, simulationPause ?? throw new ArgumentNullException(nameof(simulationPause)))
    {
    }

    internal DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IPreparedOutputCheckpointGcCoordinator checkpointGc,
        IDungeonAtomicSaveFilePort filePort)
        : this(
            saveService,
            slotCatalog,
            CreateLegacyDurableSaveCoordinator(checkpointGc),
            filePort)
    {
    }

    internal DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IDungeonDurableSaveCommitCoordinator durableSaveCommits,
        IDungeonAtomicSaveFilePort filePort,
        Func<double> validationClock = null,
        DungeonStory.Foundation.IGameTimeScalePauseAuthority simulationPause = null)
    {
        this.simulationPause = simulationPause;
        this.validationClock = validationClock ?? (() => System.Diagnostics.Stopwatch.GetTimestamp()
            / (double)System.Diagnostics.Stopwatch.Frequency);
        this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        this.slotCatalog = slotCatalog ?? throw new ArgumentNullException(nameof(slotCatalog));
        this.durableSaveCommits = durableSaveCommits
            ?? throw new ArgumentNullException(nameof(durableSaveCommits));
        this.filePort = filePort ?? throw new ArgumentNullException(nameof(filePort));
    }

    internal DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        string saveDirectory,
        IPreparedOutputCheckpointGcCoordinator checkpointGc,
        IDungeonAtomicSaveFilePort filePort)
        : this(saveService, new DungeonSaveSlotCatalog(saveDirectory),
            checkpointGc, filePort)
    {
    }

    public string Save(string slotId, bool prettyPrint = false)
    {
        RequireIdle();
        synchronousSave = true;
        try { return SaveSynchronously(slotId, prettyPrint); }
        finally { synchronousSave = false; }
    }

    private string SaveSynchronously(string slotId, bool prettyPrint)
    {
        string path = slotCatalog.GetPath(slotId);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? DungeonPersistencePaths.Root);
        string temporaryPath = path + ".tmp";
        string backupPath = path + ".bak";
        string serialized = saveService.ToJson(saveService.Capture(), prettyPrint);
        byte[] serializedBytes = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetBytes(serialized);
        string serializedByteDigest = ComputeSha256(serializedBytes);
        try
        {
            DungeonSaveEditorTrace.Record("disk-write.before");
            filePort.WriteTemporary(temporaryPath, serializedBytes);
            filePort.CommitTemporary(temporaryPath, path, backupPath);
            DungeonSaveEditorTrace.Record("disk-write.after");
        }
        catch
        {
            filePort.TryDelete(temporaryPath);
            throw;
        }

        DungeonDurableSaveCommitResult commitResult = durableSaveCommits
            .OnDurableSaveCommitted(slotId, serializedByteDigest);
        if (commitResult.Status == DungeonDurableSaveCommitStatus.Corruption)
        {
            throw new InvalidOperationException(
                "Save bytes are durable, but durable-save commit processing failed at '"
                + commitResult.ParticipantId + "': " + commitResult.Message);
        }
        filePort.TryDelete(backupPath);

        return path;
    }

    public IDungeonSaveOperation BeginSave(string slotId, bool prettyPrint = false)
    {
        RequireIdle();
        if (simulationPause == null)
            throw new InvalidOperationException("Scheduled saves require an explicit simulation pause authority.");
        var operation = new SaveOperation(slotId, slotCatalog.GetPath(slotId), prettyPrint);
        if (!Path.IsPathRooted(operation.Path))
            throw new InvalidOperationException("Scheduled saves require an absolute slot path.");
        pendingSave = operation;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            // 전체 소유 상태는 한 시점에서 캡처한다. 검증만 다음 프레임으로 나눈다.
            operation.Capture = saveService.BeginCapture();
            operation.CaptureSeconds = ElapsedSeconds(started);
            operation.LongestMainThreadStepSeconds = operation.CaptureSeconds;
            operation.MainThreadStepCount = 1;
            operation.SimulationHold = simulationPause.HoldSimulation()
                ?? throw new InvalidOperationException("Simulation pause authority returned no owned hold.");
            return operation;
        }
        catch (Exception startFailure)
        {
            pendingSave = null;
            try { operation.Capture?.Dispose(); }
            catch (Exception cleanup)
            { throw new AggregateException("Save start and capture cleanup failed.", startFailure, cleanup); }
            throw;
        }
    }

    public void AdvanceSave() => AdvanceSave(waitForWrite: false);

    private void AdvanceSave(bool waitForWrite)
    {
        RequireThread();
        SaveOperation operation = pendingSave;
        if (operation == null) return;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            if (operation.Status == DungeonSaveOperationStatus.Validating)
            {
                double validationStarted = validationClock();
                // 짧은 검사마다 프레임 전체를 기다리지 않는다. 개별 검사는
                // 끝까지 실행하고, 예산을 쓴 뒤에는 다음 프레임에 이어 간다.
                do
                {
                    operation.Capture.Advance();
                }
                while (!operation.Capture.IsCompleted
                    && !DungeonGameSaveService.IsWaitingForCaptureWork(operation.Capture)
                    && (waitForWrite || validationClock() - validationStarted < ValidationSliceSeconds));
                if (!operation.Capture.IsCompleted) return;
                DungeonGameSaveData snapshot = operation.Capture.Result;
                operation.Capture.Dispose();
                operation.Capture = null;
                operation.Status = DungeonSaveOperationStatus.Writing;
                DungeonSaveEditorTrace.Record("scheduled-write.before");
                operation.Write = Task.Run(() => WriteDetachedSnapshot(operation, snapshot));
                return;
            }
            if (!waitForWrite && !operation.Write.IsCompleted) return;
            string digest = operation.Write.GetAwaiter().GetResult();
            DungeonSaveEditorTrace.Record("scheduled-write.after");
            operation.CommitResult = durableSaveCommits.OnDurableSaveCommitted(
                operation.SlotId, digest, allowCurrentRuntimeCollection: false);
            if (operation.CommitResult.Status == DungeonDurableSaveCommitStatus.Corruption)
                throw new InvalidOperationException("Save bytes are durable, but durable-save commit processing failed at '"
                    + operation.CommitResult.ParticipantId + "': " + operation.CommitResult.Message);
            filePort.TryDelete(operation.Path + ".bak");
            operation.Status = DungeonSaveOperationStatus.Succeeded;
            pendingSave = null;
        }
        catch (Exception exception)
        {
            operation.Failure = exception;
            operation.Status = DungeonSaveOperationStatus.Failed;
            pendingSave = null;
            try { operation.Capture?.Dispose(); }
            catch (Exception cleanup)
            { operation.Failure = new AggregateException("Save and capture cleanup failed.", exception, cleanup); }
            operation.Capture = null;
        }
        finally
        {
            if (pendingSave == null && operation.SimulationHold != null)
            {
                try { operation.SimulationHold.Dispose(); }
                catch (Exception cleanup)
                {
                    failedSimulationHold = operation.SimulationHold;
                    operation.Failure = operation.Failure == null ? cleanup
                        : new AggregateException("Save and simulation hold release failed.", operation.Failure, cleanup);
                    operation.Status = DungeonSaveOperationStatus.Failed;
                }
                operation.SimulationHold = null;
            }
            operation.MainThreadStepCount++;
            operation.LongestMainThreadStepSeconds = Math.Max(
                operation.LongestMainThreadStepSeconds, ElapsedSeconds(started));
        }
    }

    private static double ElapsedSeconds(long started) =>
        (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            / (double)System.Diagnostics.Stopwatch.Frequency;

    private string WriteDetachedSnapshot(SaveOperation operation, DungeonGameSaveData snapshot)
    {
        // Unity 오브젝트가 없는 DTO와 파일만 다룬다. 게임 상태/후속 확정은 읽지 않는다.
        string serialized = saveService.ToJson(snapshot, operation.PrettyPrint);
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(serialized);
        string digest = ComputeSha256(bytes);
        string temporary = operation.Path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(operation.Path)
                ?? throw new InvalidOperationException("Save directory is missing."));
            filePort.WriteTemporary(temporary, bytes);
            filePort.CommitTemporary(temporary, operation.Path, operation.Path + ".bak");
            return digest;
        }
        catch { filePort.TryDelete(temporary); throw; }
    }

    // 종료 시 main thread 단계는 직접 끝내고 순수 파일 worker만 기다린다.
    public void CompletePendingSave()
    {
        RequireThread();
        SaveOperation operation = pendingSave;
        while (pendingSave != null)
        {
            AdvanceSave(waitForWrite: true);
            if (pendingSave?.Status == DungeonSaveOperationStatus.Validating)
                Thread.Sleep(1);
        }
        if (operation?.Failure != null) ExceptionDispatchInfo.Capture(operation.Failure).Throw();
    }

    public void Dispose()
    {
        if (disposed) return;
        try { CompletePendingSave(); }
        finally
        {
            try { failedSimulationHold?.Dispose(); failedSimulationHold = null; }
            finally { disposed = true; }
        }
    }

    private void RequireThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("Save slot commands must run on their owning thread.");
    }

    private void RequireIdle()
    {
        RequireThread();
        if (disposed) throw new ObjectDisposedException(nameof(DungeonGameSaveSlotService));
        if (failedSimulationHold != null)
            throw new InvalidOperationException("A failed save still owns its simulation pause; dispose this slot service before recovery.");
        if (IsSaving) throw new InvalidOperationException("A save is already in progress.");
    }

    private sealed class SaveOperation : IDungeonSaveOperation
    {
        public SaveOperation(string slotId, string path, bool prettyPrint)
        { SlotId = slotId; Path = path; PrettyPrint = prettyPrint; }
        public string SlotId { get; }
        public string Path { get; }
        public bool PrettyPrint { get; }
        public DungeonSaveOperationStatus Status { get; set; }
        public bool IsCompleted => Status == DungeonSaveOperationStatus.Succeeded
            || Status == DungeonSaveOperationStatus.Failed;
        public bool Succeeded => Status == DungeonSaveOperationStatus.Succeeded;
        public string Error => Failure?.Message ?? string.Empty;
        public DungeonDurableSaveCommitResult CommitResult { get; set; }
        public double CaptureSeconds { get; set; }
        public double LongestMainThreadStepSeconds { get; set; }
        public int MainThreadStepCount { get; set; }
        public Exception Failure;
        public IDungeonGameSaveCapture Capture;
        public Task<string> Write;
        public IDisposable SimulationHold;
    }

    private static IDungeonDurableSaveCommitCoordinator
        CreateLegacyDurableSaveCoordinator(
            IPreparedOutputCheckpointGcCoordinator checkpointGc) =>
        new DungeonDurableSaveCommitCoordinator(
            new IDungeonDurableSaveCommitParticipant[]
            {
                new PreparedOutputCheckpointGcDurableSaveParticipant(
                    checkpointGc ?? throw new ArgumentNullException(
                        nameof(checkpointGc)))
            });

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes ?? Array.Empty<byte>());
        StringBuilder builder = new(digest.Length * 2);
        for (int index = 0; index < digest.Length; index++)
            builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    public bool TryLoad(string slotId, out DungeonGameRestoreReport report)
    {
        RequireThread();
        if (IsSaving)
        {
            report = new DungeonGameRestoreReport();
            report.AddError("A save is already in progress.");
            return false;
        }
        if (disposed) throw new ObjectDisposedException(nameof(DungeonGameSaveSlotService));
        string path = slotCatalog.GetPath(slotId);
        if (!File.Exists(path))
        {
            report = new DungeonGameRestoreReport();
            report.AddError($"Save slot '{slotId}' does not exist.");
            return false;
        }

        try
        {
            DungeonSaveEditorTrace.Record("disk-read.before");
            string json = File.ReadAllText(path);
            DungeonSaveEditorTrace.Record("disk-read.after");
            return saveService.TryRestore(saveService.FromJson(json), out report);
        }
        catch (Exception exception)
        {
            report = new DungeonGameRestoreReport();
            string preservedPath = PreserveUnreadableSave(path);
            report.AddError(string.IsNullOrWhiteSpace(preservedPath)
                ? exception.Message
                : $"{exception.Message} 손상된 저장 사본: {preservedPath}");
            return false;
        }
    }

    public bool HasSave(string slotId)
    {
        return slotCatalog.HasSave(slotId);
    }

    public IReadOnlyList<DungeonSaveSlotInfo> GetSlots()
    {
        return slotCatalog.GetSlots();
    }

    public bool Delete(string slotId)
    {
        RequireIdle();
        return slotCatalog.Delete(slotId);
    }

    private string PreserveUnreadableSave(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            string corruptDirectory = Path.Combine(
                Path.GetDirectoryName(path) ?? DungeonPersistencePaths.Root,
                "Corrupt");
            Directory.CreateDirectory(corruptDirectory);
            string name = Path.GetFileNameWithoutExtension(path);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string preservedPath = Path.Combine(corruptDirectory, $"{name}-{timestamp}.json");
            File.Copy(path, preservedPath, overwrite: false);
            return preservedPath;
        }
        catch
        {
            return string.Empty;
        }
    }

}

internal interface IDungeonAtomicSaveFilePort
{
    void WriteTemporary(string temporaryPath, byte[] serializedBytes);
    void CommitTemporary(
        string temporaryPath,
        string destinationPath,
        string backupPath);
    void TryDelete(string path);
}

internal sealed class DungeonAtomicSaveFilePort : IDungeonAtomicSaveFilePort
{
    public void WriteTemporary(string temporaryPath, byte[] serializedBytes)
    {
        string path = temporaryPath
            ?? throw new ArgumentNullException(nameof(temporaryPath));
        byte[] bytes = serializedBytes
            ?? throw new ArgumentNullException(nameof(serializedBytes));
        using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    public void CommitTemporary(
        string temporaryPath,
        string destinationPath,
        string backupPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, backupPath, true);
        else
            File.Move(temporaryPath, destinationPath);
    }

    public void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Backup/temp cleanup is non-authoritative after atomic replacement.
        }
    }
}
