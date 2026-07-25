using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public sealed class CharacterAiNaturalnessPlayModeObserver : MonoBehaviour
{
    private const string FlagRelativePath = "Temp/ai-naturalness-observe.flag";
    private const string ReportRelativePath = "Artifacts/QA/ai-naturalness-playmode-report.txt";
    private const string CaptureRelativePath = "Artifacts/QA/ai-naturalness-playmode.png";
    private const string GameplaySceneName = DungeonSceneNavigator.GameplaySceneName;
    private const float ObservationSecondsRealtime = 18f;
    private const float WarmupSecondsRealtime = 1.5f;
    private const float ObservationTimeScale = 2f;
    private const float MaxNaturalGridJumpPerFrame = 1.15f;
    private const int MaxActionSwitchesPerActor = 8;
    private const int MaxBlockedSamplesPerActor = 4;

    private readonly Dictionary<CharacterActor, ActorObservation> observations =
        new Dictionary<CharacterActor, ActorObservation>();
    private readonly List<string> capturedConsoleIssues = new List<string>();
    private readonly List<string> setupNotes = new List<string>();
    private float originalTimeScale;
    private bool capturingLogs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string flagPath = ResolveProjectPath(FlagRelativePath);
        if (!File.Exists(flagPath))
        {
            return;
        }

        try
        {
            File.Delete(flagPath);
        }
        catch (IOException)
        {
            // The flag is best-effort. If it cannot be deleted, the observer still runs once.
        }

        GameObject host = new GameObject("QA AI Naturalness PlayMode Observer");
        DontDestroyOnLoad(host);
        host.AddComponent<CharacterAiNaturalnessPlayModeObserver>();
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory(ResolveProjectPath("Artifacts/QA"));
        originalTimeScale = Time.timeScale;
        Time.timeScale = Mathf.Max(ObservationTimeScale, originalTimeScale);
        StartLogCapture();

        if (UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).Length == 0
            && !string.Equals(
                SceneManager.GetActiveScene().name,
                GameplaySceneName,
                StringComparison.Ordinal))
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }
        }

        yield return EnsureGameplayActors();
        yield return new WaitForSecondsRealtime(WarmupSecondsRealtime);

        float startedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedAt < ObservationSecondsRealtime)
        {
            ObserveFrame();
            yield return null;
        }

        ScreenCapture.CaptureScreenshot(CaptureRelativePath);
        WriteReport();
        StopLogCapture();
        Time.timeScale = originalTimeScale;
        Destroy(gameObject);
    }

    private void ObserveFrame()
    {
        CharacterActor[] actors = UnityEngine.Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (CharacterActor actor in actors)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!observations.TryGetValue(actor, out ActorObservation observation))
            {
                observation = new ActorObservation(actor);
                observations[actor] = observation;
            }

            observation.Observe();
        }
    }

    private void WriteReport()
    {
        string reportPath = ResolveProjectPath(ReportRelativePath);
        StringBuilder builder = new StringBuilder(16 * 1024);
        builder.AppendLine("# AI Naturalness PlayMode Observation");
        builder.AppendLine($"scene={SceneManager.GetActiveScene().path}");
        builder.AppendLine($"observedRealtime={ObservationSecondsRealtime:0.0}s");
        builder.AppendLine($"timeScale={Time.timeScale:0.0}");
        builder.AppendLine($"actors={observations.Count}");
        builder.AppendLine($"consoleIssues={capturedConsoleIssues.Count}");
        builder.AppendLine($"capture={CaptureRelativePath}");
        builder.AppendLine();
        builder.AppendLine("## Setup");
        if (setupNotes.Count == 0)
        {
            builder.AppendLine("no setup notes.");
        }
        else
        {
            foreach (string note in setupNotes)
            {
                builder.AppendLine("- " + note);
            }
        }

        builder.AppendLine();

        foreach (ActorObservation observation in observations.Values.OrderBy(entry => entry.DisplayName))
        {
            observation.AppendReport(builder);
            builder.AppendLine();
        }

        builder.AppendLine("## Console");
        if (capturedConsoleIssues.Count == 0)
        {
            builder.AppendLine("PASS console warning/error none during observation.");
        }
        else
        {
            foreach (string issue in capturedConsoleIssues.Take(30))
            {
                builder.AppendLine("- " + issue);
            }
        }

        File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
        Debug.Log($"AI naturalness PlayMode observation written: {ReportRelativePath}");
    }

    private void StartLogCapture()
    {
        if (capturingLogs)
        {
            return;
        }

        Application.logMessageReceived += OnLogMessageReceived;
        capturingLogs = true;
    }

    private void StopLogCapture()
    {
        if (!capturingLogs)
        {
            return;
        }

        Application.logMessageReceived -= OnLogMessageReceived;
        capturingLogs = false;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error
            && type != LogType.Assert
            && type != LogType.Exception
            && type != LogType.Warning)
        {
            return;
        }

        capturedConsoleIssues.Add($"{type}: {Compact(condition)}");
    }

    private static string ResolveProjectPath(string relativePath)
    {
        string root = Application.dataPath;
        if (!string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetFullPath(Path.Combine(root, ".."));
        }
        else
        {
            root = Directory.GetCurrentDirectory();
        }

        return Path.GetFullPath(Path.Combine(root, relativePath));
    }

    private IEnumerator EnsureGameplayActors()
    {
        float dependencyDeadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < dependencyDeadline)
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    GameplaySceneName,
                    StringComparison.Ordinal))
            {
                yield return null;
                continue;
            }

            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            if (scope != null && scope.Container != null)
            {
                break;
            }

            yield return null;
        }

        if (FindVisibleActors().Length == 0)
        {
            if (TryPrepareAndApplyStartParty(out string message))
            {
                setupNotes.Add("prepared start party applied: " + message);
            }
            else
            {
                setupNotes.Add("prepared start party failed: " + message);
            }
        }

        float actorDeadline = Time.realtimeSinceStartup + 10f;
        while (FindVisibleActors().Length == 0 && Time.realtimeSinceStartup < actorDeadline)
        {
            yield return null;
        }

        CharacterActor[] actors = FindVisibleActors();
        setupNotes.Add($"visible actors before observation={actors.Length}");
        if (actors.Length > 0)
        {
            HideOwnerSelectionPanels();
        }
    }

    private bool TryPrepareAndApplyStartParty(out string message)
    {
        message = string.Empty;
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            message = "runtime lifetime scope is not ready.";
            return false;
        }

        IStartPartyPreparationService preparation;
        IOwnerRunManagerProvider ownerProvider;
        IPreparedStartPartyGameplayApplier applier;
        try
        {
            preparation = scope.Container.Resolve<IStartPartyPreparationService>();
            ownerProvider = scope.Container.Resolve<IOwnerRunManagerProvider>();
            applier = scope.Container.Resolve<IPreparedStartPartyGameplayApplier>();
        }
        catch (Exception exception)
        {
            message = "dependency resolve failed: " + Compact(exception.Message);
            return false;
        }

        if (!ownerProvider.TryGetManager(out OwnerRunManager manager) || manager == null)
        {
            message = "owner manager is not available.";
            return false;
        }

        if (manager.CurrentOwnerActor != null)
        {
            message = "owner actor already exists.";
            return true;
        }

        IReadOnlyList<CharacterSO> candidates = manager.OwnerCandidates;
        if (candidates == null || candidates.Count == 0)
        {
            message = "owner candidates are empty.";
            return false;
        }

        int runSeed = (int)(DateTime.UtcNow.Ticks & 0x7fffffff);
        foreach (CharacterSO owner in candidates.Where(candidate => candidate != null))
        {
            if (!preparation.Begin(owner, out string beginMessage))
            {
                setupNotes.Add($"owner {owner.characterName} begin failed: {beginMessage}");
                continue;
            }

            if (!preparation.TryCreatePreparedSnapshot(
                    DungeonDifficulty.Normal,
                    runSeed,
                    out PreparedStartPartySnapshot snapshot,
                    out string snapshotMessage))
            {
                setupNotes.Add($"owner {owner.characterName} snapshot failed: {snapshotMessage}");
                preparation.Cancel();
                continue;
            }

            preparation.Cancel();
            if (applier.TryApply(snapshot, out string applyMessage))
            {
                message = $"{owner.characterName}: {applyMessage}";
                HideOwnerSelectionPanels();
                return true;
            }

            setupNotes.Add($"owner {owner.characterName} apply failed: {applyMessage}");
        }

        message = "all owner candidates failed to produce a playable party.";
        return false;
    }

    private static CharacterActor[] FindVisibleActors()
    {
        return CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null && actor.gameObject.activeInHierarchy)
            .ToArray();
    }

    private static void HideOwnerSelectionPanels()
    {
        foreach (OwnerSelectionPanel panel in Resources.FindObjectsOfTypeAll<OwnerSelectionPanel>())
        {
            if (panel == null || !panel.gameObject.scene.IsValid())
            {
                continue;
            }

            try
            {
                panel.RefreshVisibility();
            }
            catch (Exception)
            {
                panel.gameObject.SetActive(false);
            }
        }
    }

    private static string Compact(string text, int limit = 160)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return normalized.Length <= limit ? normalized : normalized.Substring(0, limit) + "...";
    }

    private sealed class ActorObservation
    {
        private readonly CharacterActor actor;
        private readonly List<string> timeline = new List<string>();
        private readonly HashSet<string> uniqueActions = new HashSet<string>();
        private readonly HashSet<string> uniqueBranches = new HashSet<string>();
        private readonly HashSet<string> uniqueIntentLabels = new HashSet<string>();
        private Vector2Int lastGrid;
        private Vector3 lastWorld;
        private string lastAction = string.Empty;
        private string lastBranch = string.Empty;
        private string lastIntent = string.Empty;
        private bool hasLast;
        private int samples;
        private int movingFrames;
        private int gridMoves;
        private int teleportLikeFrames;
        private int actionSwitches;
        private int branchSwitches;
        private int blockedSamples;
        private int softLockSamples;
        private int noPathFailures;
        private int suspiciousTextSamples;
        private float totalWorldDistance;
        private float maxFrameWorldDistance;

        public ActorObservation(CharacterActor actor)
        {
            this.actor = actor;
            DisplayName = ResolveActorName(actor);
            Role = actor != null ? actor.Role.ToString() : "None";
        }

        public string DisplayName { get; }
        public string Role { get; }

        public void Observe()
        {
            if (actor == null)
            {
                return;
            }

            samples++;
            Vector2Int grid = actor.GetNowXY();
            Vector3 world = actor.transform.position;
            CharacterBlackboard blackboard = actor.Blackboard;
            AIBrain brain = actor.Brain;
            string branch = blackboard != null
                ? CharacterAiUtilityText.GetBranchLabel(blackboard.CurrentBranch)
                : "BT 없음";
            string intent = blackboard != null ? Fallback(blackboard.CurrentIntent, "의도 없음") : "의도 없음";
            string action = brain != null ? Fallback(brain.CurrentActionDebugLabel, "행동 없음") : "행동 없음";
            string phase = brain != null ? Fallback(brain.CurrentActionPhase, "단계 없음") : "단계 없음";
            string destination = brain != null ? Fallback(brain.CurrentDestinationDebugLabel, "목표 없음") : "목표 없음";
            string softLock = blackboard != null ? blackboard.GetSoftLockDebugSummary() : "의도 유지 없음";

            uniqueActions.Add(action);
            uniqueBranches.Add(branch);
            uniqueIntentLabels.Add(intent);
            if (softLock.Contains("최소") || softLock.Contains("중단 가능"))
            {
                softLockSamples++;
            }

            if (ContainsSuspiciousText(branch)
                || ContainsSuspiciousText(intent)
                || ContainsSuspiciousText(action)
                || ContainsSuspiciousText(phase))
            {
                suspiciousTextSamples++;
            }

            if (phase.Contains("막힘") || intent.Contains("막힘"))
            {
                blockedSamples++;
            }

            if (brain != null && brain.LastActionFailure.Kind == AIActionFailureKind.NoPath)
            {
                noPathFailures++;
            }

            if (hasLast)
            {
                float worldDelta = Vector3.Distance(world, lastWorld);
                totalWorldDistance += worldDelta;
                maxFrameWorldDistance = Mathf.Max(maxFrameWorldDistance, worldDelta);
                if (worldDelta > 0.01f)
                {
                    movingFrames++;
                }

                int gridDelta = Mathf.Abs(grid.x - lastGrid.x) + Mathf.Abs(grid.y - lastGrid.y);
                if (gridDelta > 0)
                {
                    gridMoves++;
                    if (gridDelta > MaxNaturalGridJumpPerFrame)
                    {
                        teleportLikeFrames++;
                    }
                }

                if (!string.Equals(action, lastAction, StringComparison.Ordinal))
                {
                    actionSwitches++;
                    AddTimeline($"action {lastAction} -> {action} @ {grid} phase={phase} dest={destination}");
                }

                if (!string.Equals(branch, lastBranch, StringComparison.Ordinal))
                {
                    branchSwitches++;
                    AddTimeline($"branch {lastBranch} -> {branch} @ {grid} intent={intent}");
                }

                if (!string.Equals(intent, lastIntent, StringComparison.Ordinal))
                {
                    AddTimeline($"intent {lastIntent} -> {intent} @ {grid}");
                }
            }
            else
            {
                AddTimeline($"start {branch} / {action} / {intent} @ {grid} dest={destination}");
            }

            hasLast = true;
            lastGrid = grid;
            lastWorld = world;
            lastAction = action;
            lastBranch = branch;
            lastIntent = intent;
        }

        public void AppendReport(StringBuilder builder)
        {
            ActorVerdict verdict = BuildVerdict();
            builder.AppendLine($"## {DisplayName} ({Role})");
            builder.AppendLine($"{verdict.Status} {verdict.Summary}");
            builder.AppendLine(
                $"samples={samples}; actions={uniqueActions.Count}; branches={uniqueBranches.Count}; intents={uniqueIntentLabels.Count}; "
                + $"actionSwitches={actionSwitches}; branchSwitches={branchSwitches}; gridMoves={gridMoves}; "
                + $"movingFrames={movingFrames}; totalWorldDistance={totalWorldDistance:0.00}; maxFrameDistance={maxFrameWorldDistance:0.00}");
            builder.AppendLine(
                $"softLockSamples={softLockSamples}; blockedSamples={blockedSamples}; noPathFailures={noPathFailures}; "
                + $"teleportLikeFrames={teleportLikeFrames}; suspiciousTextSamples={suspiciousTextSamples}");
            builder.AppendLine($"uniqueActions={string.Join(", ", uniqueActions.OrderBy(value => value).Take(10))}");
            builder.AppendLine($"uniqueBranches={string.Join(", ", uniqueBranches.OrderBy(value => value).Take(10))}");
            builder.AppendLine("timeline:");
            foreach (string row in timeline.Take(18))
            {
                builder.AppendLine("- " + row);
            }

            if (timeline.Count > 18)
            {
                builder.AppendLine($"- ... {timeline.Count - 18} more");
            }
        }

        private ActorVerdict BuildVerdict()
        {
            List<string> failures = new List<string>();
            List<string> warnings = new List<string>();

            if (samples <= 10)
            {
                failures.Add("관찰 샘플 부족");
            }

            if (suspiciousTextSamples > 0)
            {
                failures.Add("깨진 AI 표시 문자열 감지");
            }

            if (teleportLikeFrames > 0)
            {
                failures.Add($"프레임 간 비정상 격자 점프 {teleportLikeFrames}회");
            }

            if (actionSwitches > MaxActionSwitchesPerActor)
            {
                warnings.Add($"행동 전환 과다 {actionSwitches}회");
            }

            if (blockedSamples > MaxBlockedSamplesPerActor)
            {
                warnings.Add($"이동 막힘 반복 {blockedSamples}샘플");
            }

            if (noPathFailures > 0)
            {
                warnings.Add($"NoPath 실패 {noPathFailures}샘플");
            }

            if (uniqueActions.Count <= 1 && movingFrames <= 2)
            {
                warnings.Add("관찰 시간 동안 거의 움직이지 않음");
            }

            if (softLockSamples <= 0 && actionSwitches > 0)
            {
                warnings.Add("행동 전환 중 의도 유지 샘플 없음");
            }

            if (failures.Count > 0)
            {
                return new ActorVerdict("FAIL", string.Join("; ", failures.Concat(warnings)));
            }

            if (warnings.Count > 0)
            {
                return new ActorVerdict("WARN", string.Join("; ", warnings));
            }

            return new ActorVerdict("PASS", "행동 전환과 이동이 관찰 기준 안에 있음");
        }

        private void AddTimeline(string row)
        {
            if (timeline.Count >= 40)
            {
                return;
            }

            timeline.Add($"t={Time.time:0.0} {row}");
        }

        private static string ResolveActorName(CharacterActor actor)
        {
            if (actor == null)
            {
                return "<missing>";
            }

            return actor.Identity != null && !string.IsNullOrWhiteSpace(actor.Identity.DisplayName)
                ? actor.Identity.DisplayName
                : actor.name;
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static bool ContainsSuspiciousText(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value.Contains("\uFFFD")
                    || value.Contains("??")
                    || value.Contains("\\u"));
        }

        private readonly struct ActorVerdict
        {
            public ActorVerdict(string status, string summary)
            {
                Status = status;
                Summary = summary;
            }

            public string Status { get; }
            public string Summary { get; }
        }
    }
}
