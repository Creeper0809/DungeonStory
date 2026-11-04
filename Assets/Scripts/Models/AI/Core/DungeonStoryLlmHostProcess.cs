using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;

[Serializable]
public sealed class DungeonStoryLlmHostManifest
{
    public int protocolVersion = 25;
    public string hostKind = "DungeonStoryNative";
    public string hostWindows = "DungeonStoryLlmHost.exe";
    public string hostLinux = "DungeonStoryLlmHost";
    public string hostWindowsSha256 = string.Empty;
    public string hostLinuxSha256 = string.Empty;
    public DungeonStoryLlmHostSupportFile[] supportFiles = Array.Empty<DungeonStoryLlmHostSupportFile>();
    public string modelFile = "DungeonStory-Qwen3-1.7B-Q4_K_M.gguf";
    public string modelSha256 = string.Empty;
    public string modelAlias = "DungeonStory-Qwen3-1.7B-Q4_K_M";
    public string adapterFile = string.Empty;
    public string adapterSha256 = string.Empty;
    public int gpuLayers;
    public int contextSize = 8192;
    public int parallelSlots = 2;
    public int sharedContextTokens;
    public long maximumModelBytes = 1500000000L;
    public string modelVersion = string.Empty;
    public bool releaseCertified;
    public string trainingState = string.Empty;
}

[Serializable]
public sealed class DungeonStoryLlmHostSupportFile
{
    public string file = string.Empty;
    public string sha256 = string.Empty;
}

/// <summary>
/// Owns the bundled inference host. A failed safety precondition disables local
/// inference; it never launches an uncontained process as a convenience fallback.
/// </summary>
public sealed class DungeonStoryLlmHostProcess : IDisposable
{
    public const string R80BackendKind = "R80StagedServer";

    [Serializable]
    private sealed class ActiveRuntime
    {
        public string relativeRoot;
        public string manifestSha256;
    }

    // A separate activation record preserves the original 1.7B manifest and
    // weights. Invalid activation fails explicitly, without switching models.
    public static string ResolveDefaultRoot(string streamingAssetsPath)
    {
        string activation = Path.Combine(streamingAssetsPath, "narrative-runtime.json");
        if (!File.Exists(activation)) return streamingAssetsPath;
        ActiveRuntime active = JsonUtility.FromJson<ActiveRuntime>(File.ReadAllText(activation));
        string boundary = Path.GetFullPath(streamingAssetsPath).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (active == null || string.IsNullOrWhiteSpace(active.relativeRoot)
            || Path.IsPathRooted(active.relativeRoot))
            throw new InvalidOperationException("Active narrative runtime requires a relative bundle root.");
        string selected = Path.GetFullPath(Path.Combine(streamingAssetsPath, active.relativeRoot));
        string manifest = Path.Combine(selected, "DungeonStoryLlm", "manifest.json");
        if (!selected.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(manifest) || string.IsNullOrWhiteSpace(active.manifestSha256)
            || !HashMatches(manifest, active.manifestSha256))
            throw new InvalidOperationException("Active narrative runtime failed path or manifest validation.");
        return selected;
    }

    private const int LogCapacityBytes = 1024 * 1024;
    private static int editorBatchDiagnosticScope;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CaptureNativeDiagnosticScope()
    {
        // Unity 실행 정보는 주 스레드에서만 읽고, host 작업 스레드는 이 값만 참조한다.
        Volatile.Write(ref editorBatchDiagnosticScope, Application.isEditor && Application.isBatchMode ? 1 : 0);
    }
    private readonly object logGate = new object();
    private readonly Queue<byte> logRing = new Queue<byte>(LogCapacityBytes);
    private readonly CancellationTokenSource lifetimeCancellation =
        new CancellationTokenSource();
    private readonly IDisposable ownership;
    private int disposed;
    private Process process;
    private IDisposable platformContainment;
    private AnonymousPipeServerStream lifetimePipe;
    private Stream heartbeatStream;
    private Task heartbeatTask;
    private Task stdoutTask;
    private Task stderrTask;

    private DungeonStoryLlmHostProcess(IDisposable ownership)
    {
        this.ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
    }

    public string Endpoint { get; private set; } = string.Empty;
    public string SessionToken { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public string ModelAlias { get; private set; } = string.Empty;
    public int ParallelSlots { get; private set; } = 2;
    public string BackendKind { get; private set; } = string.Empty;
    public string TrainingState { get; private set; } = string.Empty;
    public bool ReleaseCertified { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public bool IsRunning => Volatile.Read(ref disposed) == 0 && process != null && !process.HasExited;

    public static bool TryStart(out DungeonStoryLlmHostProcess host, out string error)
    {
        return TryStart(Application.streamingAssetsPath, out host, out error);
    }

    public static bool TryStart(
        string streamingAssetsPath,
        out DungeonStoryLlmHostProcess host,
        out string error)
    {
        host = null;
        error = string.Empty;
        if (!DungeonStoryLlmHostOwnership.TryAcquire(out var ownership, out error))
            return false;
        DungeonStoryLlmHostProcess candidate = null;
        try
        {
            candidate = new DungeonStoryLlmHostProcess(ownership);
            if (!candidate.TryStartInternal(streamingAssetsPath, out error)) return false;
            host = candidate;
            return true;
        }
        finally
        {
            if (host == null)
            {
                if (candidate != null) candidate.Dispose();
                else ownership.Dispose();
            }
        }
    }

    private bool TryStartInternal(string streamingAssetsPath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(streamingAssetsPath) || !Path.IsPathRooted(streamingAssetsPath))
        {
            error = "Narrative model configuration requires an absolute streaming-assets root.";
            return false;
        }
        string root = Path.Combine(
            streamingAssetsPath ?? string.Empty,
            "DungeonStoryLlm");
        string manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            error = "Bundled narrative host manifest is missing.";
            return false;
        }

        DungeonStoryLlmHostManifest manifest;
        try
        {
            manifest = new DungeonStoryLlmHostManifest();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(manifestPath, Encoding.UTF8), manifest);
        }
        catch (Exception exception)
        {
            error = "Narrative host manifest is invalid: " + exception.Message;
            return false;
        }

        if (manifest == null || manifest.protocolVersion != 25)
        {
            error = "Narrative host protocol version does not match V25.";
            return false;
        }

        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool linux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (!windows && !linux)
        {
            error = "This platform has no certified DungeonStory local inference host.";
            return false;
        }

        bool stagedR80 = string.Equals(manifest.hostKind, R80BackendKind, StringComparison.Ordinal);
        bool llamaCppServer = stagedR80 || string.Equals(
            manifest.hostKind?.Trim(),
            "LlamaCppServer",
            StringComparison.OrdinalIgnoreCase);
        if (llamaCppServer && !windows)
        {
            error = "The mounted llama.cpp inference bundle requires Windows; deterministic prose remains active.";
            return false;
        }

        string executablePath = Path.Combine(
            root,
            windows ? manifest.hostWindows : manifest.hostLinux);
        string modelPath = Path.Combine(root, manifest.modelFile ?? string.Empty);
        if (!File.Exists(executablePath) || !File.Exists(modelPath))
        {
            error = "Bundled narrative host or model is missing.";
            return false;
        }

        string expectedHostHash = windows
            ? manifest.hostWindowsSha256
            : manifest.hostLinuxSha256;
        if (string.IsNullOrWhiteSpace(expectedHostHash)
            || !HashMatches(executablePath, expectedHostHash))
        {
            error = "Bundled narrative host failed SHA-256 validation.";
            return false;
        }

        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(executablePath).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFullPath(modelPath).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(manifest.modelAlias)
            || manifest.modelAlias.Length > 128
            || Array.Exists(manifest.modelAlias.ToCharArray(), char.IsControl)
            || manifest.gpuLayers < 0 || manifest.gpuLayers > 99
            || manifest.contextSize < 2048 || manifest.contextSize > 32768
            || manifest.parallelSlots < 1 || manifest.parallelSlots > (stagedR80 ? 4 : 2)
            || manifest.sharedContextTokens < 0
            || manifest.sharedContextTokens > 32768
            || (manifest.sharedContextTokens > 0 && (!stagedR80 || manifest.sharedContextTokens < manifest.contextSize))
            || (stagedR80 && manifest.parallelSlots == 4 && manifest.sharedContextTokens == 0))
        {
            error = "Narrative host paths or inference settings are invalid.";
            return false;
        }
        string adapterPath = string.Empty;
        if (!string.IsNullOrWhiteSpace(manifest.adapterFile)
            || !string.IsNullOrWhiteSpace(manifest.adapterSha256))
        {
            adapterPath = Path.GetFullPath(Path.Combine(root, manifest.adapterFile ?? string.Empty));
            if (!llamaCppServer || string.IsNullOrWhiteSpace(manifest.adapterFile)
                || !adapterPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(adapterPath) || new FileInfo(adapterPath).Length <= 0
                || string.IsNullOrWhiteSpace(manifest.adapterSha256)
                || !HashMatches(adapterPath, manifest.adapterSha256))
            {
                error = "Narrative adapter failed path or SHA-256 validation.";
                return false;
            }
        }
        foreach (DungeonStoryLlmHostSupportFile support in
                 manifest.supportFiles ?? Array.Empty<DungeonStoryLlmHostSupportFile>())
        {
            string supportPath = Path.GetFullPath(Path.Combine(root, support?.file ?? string.Empty));
            if (!supportPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(supportPath)
                || string.IsNullOrWhiteSpace(support?.sha256)
                || !HashMatches(supportPath, support.sha256))
            {
                error = "Bundled narrative host support file failed SHA-256 validation.";
                return false;
            }
        }

        FileInfo modelInfo = new FileInfo(modelPath);
        if (modelInfo.Length <= 0L
            || modelInfo.Length > Math.Max(1L, manifest.maximumModelBytes)
            || string.IsNullOrWhiteSpace(manifest.modelSha256)
            || !HashMatches(modelPath, manifest.modelSha256))
        {
            error = "Bundled narrative model failed size or SHA-256 validation.";
            return false;
        }

        int port = ReserveLoopbackPort();
        SessionToken = CreateToken(32);
        ModelVersion = manifest.modelVersion?.Trim() ?? string.Empty;
        ModelAlias = manifest.modelAlias.Trim();
        ParallelSlots = manifest.parallelSlots;
        BackendKind = manifest.hostKind?.Trim() ?? string.Empty;
        TrainingState = manifest.trainingState?.Trim() ?? string.Empty;
        ReleaseCertified = manifest.releaseCertified;
        string commonArguments;
        if (llamaCppServer)
        {
            int workerThreads = Math.Max(2, Math.Min(6, Environment.ProcessorCount / 2));
            commonArguments = string.Join(" ", new[]
            {
                "--model", Quote(modelPath),
                "--alias", Quote(ModelAlias),
                "--host", "127.0.0.1",
                "--port", port.ToString(CultureInfo.InvariantCulture),
                "--api-key", Quote(SessionToken),
                "--ctx-size", (manifest.sharedContextTokens > 0 ? manifest.sharedContextTokens
                    : stagedR80 ? checked(manifest.contextSize * manifest.parallelSlots)
                    : manifest.contextSize).ToString(CultureInfo.InvariantCulture),
                "--parallel", manifest.parallelSlots.ToString(CultureInfo.InvariantCulture),
                "--threads", workerThreads.ToString(CultureInfo.InvariantCulture),
                "--threads-batch", workerThreads.ToString(CultureInfo.InvariantCulture),
                "--gpu-layers", manifest.gpuLayers.ToString(CultureInfo.InvariantCulture),
                "--reasoning", "off",
                "--reasoning-budget", "0",
                "--cache-prompt",
                "--cache-ram", ReadNativePromptCacheMiB(
                    Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_CACHE_DIAGNOSTIC")).ToString(CultureInfo.InvariantCulture),
                "--batch-size", stagedR80 && manifest.sharedContextTokens > 0 ? "256" : "512",
                "--ubatch-size", "128",
                "--no-warmup",
                "--no-webui"
            });
            string speculativeArguments = ReadNativeSpeculativeArguments(
                Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_SPEC_DIAGNOSTIC"));
            if (speculativeArguments.Length > 0 && !stagedR80)
                throw new InvalidOperationException("Single-model decoding diagnostics require the exact R80 checkpoint.");
            commonArguments += speculativeArguments;
            if (!string.IsNullOrEmpty(adapterPath)) commonArguments += " --lora " + Quote(adapterPath);
            if (stagedR80)
            {
                if (!string.IsNullOrEmpty(adapterPath)
                    || manifest.modelSha256 != "2894ec5261b3a8352a375eb6319bbc8dd8c68c9b7b64fba255d51ce1bc2eb9b2"
                    || manifest.trainingState != "r80-single-checkpoint-prototype")
                    throw new InvalidOperationException("R80 requires its exact merged single checkpoint.");
                string traceRoot = Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_TRACE_ROOT");
                if (string.IsNullOrWhiteSpace(traceRoot))
                    traceRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "DungeonStory", "R80RuntimeLogs");
                commonArguments = "-B " + Quote(Path.Combine(root, "r80_game_bridge.py"))
                    + " --engine " + Quote(Path.Combine(root, "llama-server.exe"))
                    + " --trace-dir " + Quote(Path.Combine(traceRoot, Guid.NewGuid().ToString("N")))
                    + " --request-context-size " + manifest.contextSize.ToString(CultureInfo.InvariantCulture)
                    + " " + commonArguments
                    + (manifest.sharedContextTokens > 0 ? " --kv-unified --no-context-shift" : string.Empty);
            }
        }
        else
        {
            lifetimePipe = new AnonymousPipeServerStream(
                PipeDirection.Out,
                HandleInheritability.Inheritable);
            commonArguments = string.Join(" ", new[]
            {
                "--protocol", "25",
                "--parent-pid", Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture),
                "--port", port.ToString(CultureInfo.InvariantCulture),
                "--token", Quote(SessionToken),
                "--model", Quote(modelPath),
                "--lifetime-handle", lifetimePipe.GetClientHandleAsString(),
                "--kv-slots", "2",
                "--context-tokens", "4096",
                "--prompt-cache-mb", "256",
                "--no-disk-cache",
                "--non-thinking"
            });
        }

        try
        {
            if (windows)
            {
                WindowsContainedProcess launched = WindowsContainedProcess.Start(
                    executablePath,
                    commonArguments,
                    requireHeartbeat: !llamaCppServer);
                process = launched.Process;
                platformContainment = launched;
                heartbeatStream = launched.HeartbeatStream;
                stdoutTask = DrainAsync(launched.StandardOutput, lifetimeCancellation.Token);
                stderrTask = DrainAsync(launched.StandardError, lifetimeCancellation.Token);
            }
            else
            {
                UnixContainedProcess launched = UnixContainedProcess.Start(
                    executablePath,
                    commonArguments);
                process = launched.Process;
                platformContainment = launched;
                heartbeatStream = launched.HeartbeatStream;
                stdoutTask = DrainAsync(process.StandardOutput.BaseStream, lifetimeCancellation.Token);
                stderrTask = DrainAsync(process.StandardError.BaseStream, lifetimeCancellation.Token);
            }

            lifetimePipe?.DisposeLocalCopyOfClientHandle();
            Endpoint = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
            if (llamaCppServer && !WaitForHostReady(
                    port,
                    SessionToken,
                    TimeSpan.FromSeconds(45)))
            {
                if (!IsRunning)
                    throw new InvalidOperationException(
                        "Bundled narrative host exited before readiness, exit code " + process.ExitCode + ".");
                throw new TimeoutException("Bundled llama.cpp host did not become ready within 45 seconds.");
            }
            if (heartbeatStream != null)
            {
                heartbeatTask = RunHeartbeatAsync(lifetimeCancellation.Token);
            }
            return true;
        }
        catch (Exception exception)
        {
            error = "Narrative host launch failed closed: " + exception.Message;
            LastError = error;
            return false;
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (heartbeatStream == null)
        {
            return;
        }

        byte[] ping = new byte[8];
        byte[] pong = new byte[8];
        long sequence = 0L;
        Stopwatch monotonic = Stopwatch.StartNew();
        long previousMilliseconds = monotonic.ElapsedMilliseconds;
        while (!cancellationToken.IsCancellationRequested && IsRunning)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            long now = monotonic.ElapsedMilliseconds;
            if (now - previousMilliseconds > 30000L)
            {
                previousMilliseconds = now;
                continue;
            }
            previousMilliseconds = now;

            sequence++;
            byte[] encoded = BitConverter.GetBytes(sequence);
            Buffer.BlockCopy(encoded, 0, ping, 0, 8);
            using CancellationTokenSource writeTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            writeTimeout.CancelAfter(250);
            try
            {
                await heartbeatStream.WriteAsync(ping, 0, ping.Length, writeTimeout.Token)
                    .ConfigureAwait(false);
                await heartbeatStream.FlushAsync(writeTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is IOException || exception is OperationCanceledException)
            {
                LastError = "Narrative host heartbeat write failed.";
                heartbeatStream.Dispose();
                StopHost();
                return;
            }

            using CancellationTokenSource readTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            if (!await ReadExactlyAsync(
                    heartbeatStream,
                    pong,
                    readTimeout.Token).ConfigureAwait(false)
                || BitConverter.ToInt64(pong, 0) != sequence)
            {
                LastError = "Narrative host heartbeat pong was missing or invalid.";
                heartbeatStream.Dispose();
                StopHost();
                return;
            }
        }
    }

    private async Task DrainAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream == null)
        {
            return;
        }
        byte[] buffer = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }
                lock (logGate)
                {
                    for (int i = 0; i < read; i++)
                    {
                        while (logRing.Count >= LogCapacityBytes)
                        {
                            logRing.Dequeue();
                        }
                        logRing.Enqueue(buffer[i]);
                    }
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException || exception is OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try
        {
            lifetimeCancellation.Cancel();
            lifetimePipe?.Dispose();
            heartbeatStream?.Dispose();
        }
        finally
        {
            try
            {
                StopHost();
                platformContainment?.Dispose();
                process?.Dispose();
            }
            finally
            {
                try { ownership.Dispose(); }
                finally { lifetimeCancellation.Dispose(); }
            }
        }
    }

    private void StopHost()
    {
        try
        {
            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
        }
        catch (Exception exception)
        {
            Debug.Log("DungeonStory narrative host stop: " + exception.Message);
        }
    }

    private static async Task<bool> ReadExactlyAsync(
        Stream stream,
        byte[] destination,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        try
        {
            while (offset < destination.Length)
            {
                int read = await stream.ReadAsync(
                        destination,
                        offset,
                        destination.Length - offset,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read <= 0)
                {
                    return false;
                }
                offset += read;
            }
            return true;
        }
        catch (Exception exception) when (
            exception is IOException || exception is OperationCanceledException)
        {
            return false;
        }
    }

    private static int ReadNativePromptCacheMiB(string diagnostic) =>
        ParseNativePromptCacheMiB(diagnostic, Volatile.Read(ref editorBatchDiagnosticScope) == 1);

    private static int ParseNativePromptCacheMiB(string diagnostic, bool isolatedEditorBatch)
    {
        if (string.IsNullOrEmpty(diagnostic)) return 256;
        if (isolatedEditorBatch && diagnostic == "0") return 0;
        throw new InvalidOperationException("Native cache diagnostics require an explicit isolated Editor batch.");
    }

    private static string ReadNativeSpeculativeArguments(string diagnostic) =>
        ParseNativeSpeculativeArguments(diagnostic, Volatile.Read(ref editorBatchDiagnosticScope) == 1);

    private static string ParseNativeSpeculativeArguments(string diagnostic, bool isolatedEditorBatch)
    {
        if (string.IsNullOrEmpty(diagnostic)) return string.Empty;
        if (isolatedEditorBatch && diagnostic == "ngram-simple")
            return " --spec-type ngram-simple --spec-draft-n-max 8 --spec-ngram-simple-size-m 8";
        throw new InvalidOperationException("Only explicit single-model ngram diagnostics are allowed in Editor batch mode.");
    }

#if UNITY_EDITOR
    public static void VerifyNativeSingleModelSpeculativeScopeForBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch verification required.");
        CaptureNativeDiagnosticScope();
        const string expected = " --spec-type ngram-simple --spec-draft-n-max 8 --spec-ngram-simple-size-m 8";
        if (Task.Run(() => ReadNativeSpeculativeArguments(null)).GetAwaiter().GetResult() != string.Empty
            || Task.Run(() => ReadNativeSpeculativeArguments("ngram-simple")).GetAwaiter().GetResult() != expected)
            throw new InvalidOperationException("Single-model decoding defaults or worker-safe scope changed.");
        foreach (var test in new[] { (value: "ngram-simple", allowed: false), (value: "draft", allowed: true) })
        {
            bool rejected = false;
            try { ParseNativeSpeculativeArguments(test.value, test.allowed); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Unsupported or second-model diagnostic was accepted.");
        }
    }

    public static void VerifyNativeCacheDiagnosticScopeForBatch()
    {
        if (!Application.isBatchMode
            || ParseNativePromptCacheMiB(null, false) != 256
            || ParseNativePromptCacheMiB("0", true) != 0)
            throw new InvalidOperationException("Native cache default or private diagnostic scope changed.");
        CaptureNativeDiagnosticScope();
        if (Task.Run(() => ReadNativePromptCacheMiB(null)).GetAwaiter().GetResult() != 256
            || Task.Run(() => ReadNativePromptCacheMiB("0")).GetAwaiter().GetResult() != 0)
            throw new InvalidOperationException("Native cache settings cannot be read safely from the host worker.");
        foreach (var test in new[] { (value: "0", allowed: false), (value: "128", allowed: true) })
        {
            bool rejected = false;
            try { ParseNativePromptCacheMiB(test.value, test.allowed); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid native cache override was accepted.");
        }
    }
#endif

    private static int ReserveLoopbackPort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private bool WaitForHostReady(int port, string token, TimeSpan timeout)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout && IsRunning)
        {
            try
            {
                using TcpClient client = new TcpClient
                {
                    ReceiveTimeout = 500,
                    SendTimeout = 500
                };
                IAsyncResult connect = client.BeginConnect(IPAddress.Loopback, port, null, null);
                using (connect.AsyncWaitHandle)
                {
                    if (!connect.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(250)))
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                }
                client.EndConnect(connect);
                using NetworkStream stream = client.GetStream();
                string request =
                    "GET /health HTTP/1.1\r\n" +
                    "Host: 127.0.0.1\r\n" +
                    "Authorization: Bearer " + (token ?? string.Empty) + "\r\n" +
                    "Connection: close\r\n\r\n";
                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                stream.Write(requestBytes, 0, requestBytes.Length);
                using StreamReader reader = new StreamReader(
                    stream,
                    Encoding.ASCII,
                    false,
                    256,
                    leaveOpen: true);
                string statusLine = reader.ReadLine() ?? string.Empty;
                if (statusLine.IndexOf(" 200 ", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            catch (Exception exception) when (
                exception is SocketException || exception is IOException)
            {
            }
            Thread.Sleep(100);
        }
        return false;
    }

    private static bool HashMatches(string path, string expected)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        string actual = BitConverter.ToString(hash).Replace("-", string.Empty);
        return string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateToken(int byteCount)
    {
        byte[] bytes = new byte[Math.Max(16, byteCount)];
        using RandomNumberGenerator random = RandomNumberGenerator.Create();
        random.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    internal static string Quote(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }

    private sealed class UnixContainedProcess : IDisposable
    {
        private UnixContainedProcess(Process process, Stream heartbeatStream, int childSocket)
        {
            Process = process;
            HeartbeatStream = heartbeatStream;
            this.childSocket = childSocket;
        }

        private readonly int childSocket;
        public Process Process { get; }
        public Stream HeartbeatStream { get; }

        public static UnixContainedProcess Start(string executable, string arguments)
        {
            int[] sockets = new int[2];
            if (socketpair(1, 1 | 0x800, 0, sockets) != 0)
            {
                throw new InvalidOperationException("socketpair failed: " + Marshal.GetLastWin32Error());
            }

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments + " --heartbeat-fd " + sockets[1],
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            Process process = Process.Start(info)
                ?? throw new InvalidOperationException("Failed to start the Linux narrative host.");
            close(sockets[1]);
            SafeFileHandle handle = new SafeFileHandle(new IntPtr(sockets[0]), true);
            FileStream stream = new FileStream(handle, FileAccess.ReadWrite, 8, true);
            return new UnixContainedProcess(process, stream, -1);
        }

        public void Dispose()
        {
            HeartbeatStream?.Dispose();
            if (childSocket >= 0)
            {
                close(childSocket);
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int socketpair(int domain, int type, int protocol, int[] sockets);

        [DllImport("libc", SetLastError = true)]
        private static extern int close(int fd);
    }

    private sealed class WindowsContainedProcess : IDisposable
    {
        private readonly IntPtr jobHandle;
        private readonly IntPtr processHandle;
        private readonly NamedPipeServerStream heartbeatPipe;

        private WindowsContainedProcess(
            Process process,
            IntPtr jobHandle,
            IntPtr processHandle,
            NamedPipeServerStream heartbeatPipe,
            Stream stdout,
            Stream stderr)
        {
            Process = process;
            this.jobHandle = jobHandle;
            this.processHandle = processHandle;
            this.heartbeatPipe = heartbeatPipe;
            HeartbeatStream = heartbeatPipe;
            StandardOutput = stdout;
            StandardError = stderr;
        }

        public Process Process { get; }
        public Stream HeartbeatStream { get; }
        public Stream StandardOutput { get; }
        public Stream StandardError { get; }

        public static WindowsContainedProcess Start(
            string executable,
            string arguments,
            bool requireHeartbeat)
        {
            string pipeName = "DungeonStory.LlmHeartbeat." + Guid.NewGuid().ToString("N");
            NamedPipeServerStream heartbeat = requireHeartbeat
                ? new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous)
                : null;
            AnonymousPipeServerStream stdout = new AnonymousPipeServerStream(
                PipeDirection.In,
                HandleInheritability.Inheritable);
            AnonymousPipeServerStream stderr = new AnonymousPipeServerStream(
                PipeDirection.In,
                HandleInheritability.Inheritable);
            IntPtr job = CreateKillOnCloseJob();
            STARTUPINFO startup = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>(),
                dwFlags = 0x00000100,
                hStdOutput = ParseHandle(stdout.GetClientHandleAsString()),
                hStdError = ParseHandle(stderr.GetClientHandleAsString()),
                hStdInput = IntPtr.Zero
            };
            string commandLine = Quote(executable) + " " + arguments;
            if (requireHeartbeat)
            {
                commandLine += " --heartbeat-pipe " + Quote(pipeName);
            }
            if (!CreateProcessW(
                    executable,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    0x00000004 | 0x08000000,
                    IntPtr.Zero,
                    Path.GetDirectoryName(executable),
                    ref startup,
                    out PROCESS_INFORMATION processInfo))
            {
                CloseHandle(job);
                throw new InvalidOperationException("CreateProcessW failed: " + Marshal.GetLastWin32Error());
            }

            if (!AssignProcessToJobObject(job, processInfo.hProcess))
            {
                TerminateProcess(processInfo.hProcess, 1);
                CloseHandle(processInfo.hThread);
                CloseHandle(processInfo.hProcess);
                CloseHandle(job);
                throw new InvalidOperationException("AssignProcessToJobObject failed: " + Marshal.GetLastWin32Error());
            }
            if (ResumeThread(processInfo.hThread) == uint.MaxValue)
            {
                TerminateProcess(processInfo.hProcess, 1);
                CloseHandle(processInfo.hThread);
                CloseHandle(processInfo.hProcess);
                CloseHandle(job);
                throw new InvalidOperationException("ResumeThread failed: " + Marshal.GetLastWin32Error());
            }
            CloseHandle(processInfo.hThread);
            stdout.DisposeLocalCopyOfClientHandle();
            stderr.DisposeLocalCopyOfClientHandle();
            Process process = Process.GetProcessById((int)processInfo.dwProcessId);
            Task wait = heartbeat?.WaitForConnectionAsync();
            if (requireHeartbeat && (wait == null || !wait.Wait(TimeSpan.FromSeconds(8))))
            {
                TerminateProcess(processInfo.hProcess, 1);
                CloseHandle(processInfo.hProcess);
                CloseHandle(job);
                heartbeat.Dispose();
                throw new TimeoutException("Narrative host did not connect its heartbeat pipe.");
            }
            return new WindowsContainedProcess(
                process,
                job,
                processInfo.hProcess,
                heartbeat,
                stdout,
                stderr);
        }

        public void Dispose()
        {
            heartbeatPipe?.Dispose();
            StandardOutput?.Dispose();
            StandardError?.Dispose();
            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
            }
            if (jobHandle != IntPtr.Zero)
            {
                CloseHandle(jobHandle);
            }
        }

        private static IntPtr CreateKillOnCloseJob()
        {
            IntPtr job = CreateJobObjectW(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateJobObjectW failed: " + Marshal.GetLastWin32Error());
            }
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION info =
                new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = 0x00002000;
            int size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            IntPtr pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, pointer, false);
                if (!SetInformationJobObject(job, 9, pointer, (uint)size))
                {
                    CloseHandle(job);
                    throw new InvalidOperationException("SetInformationJobObject failed: " + Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
            return job;
        }

        private static IntPtr ParseHandle(string value)
        {
            return new IntPtr(long.Parse(value, CultureInfo.InvariantCulture));
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcessW(
            string applicationName,
            string commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref STARTUPINFO startupInfo,
            out PROCESS_INFORMATION processInformation);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObjectW(IntPtr attributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            int informationClass,
            IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
