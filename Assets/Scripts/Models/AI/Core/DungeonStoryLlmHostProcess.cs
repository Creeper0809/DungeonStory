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
    private const int LogCapacityBytes = 1024 * 1024;
    private readonly object logGate = new object();
    private readonly Queue<byte> logRing = new Queue<byte>(LogCapacityBytes);
    private readonly CancellationTokenSource lifetimeCancellation =
        new CancellationTokenSource();
    private readonly Mutex userMutex;
    private Process process;
    private IDisposable platformContainment;
    private AnonymousPipeServerStream lifetimePipe;
    private Stream heartbeatStream;
    private Task heartbeatTask;
    private Task stdoutTask;
    private Task stderrTask;

    private DungeonStoryLlmHostProcess(Mutex userMutex)
    {
        this.userMutex = userMutex;
    }

    public string Endpoint { get; private set; } = string.Empty;
    public string SessionToken { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public string BackendKind { get; private set; } = string.Empty;
    public string TrainingState { get; private set; } = string.Empty;
    public bool ReleaseCertified { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public bool IsRunning => process != null && !process.HasExited;

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
        string mutexName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"Local\DungeonStory.LlmHost." + Environment.UserName
            : "DungeonStory.LlmHost." + Environment.UserName;
        Mutex mutex = new Mutex(false, mutexName);
        bool ownsMutex;
        try
        {
            ownsMutex = mutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }

        if (!ownsMutex)
        {
            mutex.Dispose();
            error = "Another DungeonStory process already owns the local narrative host.";
            return false;
        }

        DungeonStoryLlmHostProcess candidate = new DungeonStoryLlmHostProcess(mutex);
        if (!candidate.TryStartInternal(streamingAssetsPath, out error))
        {
            candidate.Dispose();
            return false;
        }
        host = candidate;
        return true;
    }

    private bool TryStartInternal(string streamingAssetsPath, out string error)
    {
        error = string.Empty;
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
            manifest = JsonUtility.FromJson<DungeonStoryLlmHostManifest>(
                File.ReadAllText(manifestPath, Encoding.UTF8));
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

        bool llamaCppServer = string.Equals(
            manifest.hostKind?.Trim(),
            "LlamaCppServer",
            StringComparison.OrdinalIgnoreCase);
        if (llamaCppServer && !windows)
        {
            error = "The mounted base model currently has a Windows CPU host only; deterministic prose remains active.";
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
                "--alias", Quote("DungeonStory-Qwen3-1.7B-Q4_K_M"),
                "--host", "127.0.0.1",
                "--port", port.ToString(CultureInfo.InvariantCulture),
                "--api-key", Quote(SessionToken),
                "--ctx-size", "8192",
                "--parallel", "2",
                "--threads", workerThreads.ToString(CultureInfo.InvariantCulture),
                "--threads-batch", workerThreads.ToString(CultureInfo.InvariantCulture),
                "--gpu-layers", "0",
                "--reasoning", "off",
                "--reasoning-budget", "0",
                "--cache-prompt",
                "--cache-ram", "256",
                "--batch-size", "512",
                "--ubatch-size", "128",
                "--no-warmup",
                "--no-webui"
            });
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
        lifetimeCancellation.Cancel();
        lifetimePipe?.Dispose();
        heartbeatStream?.Dispose();
        StopHost();
        platformContainment?.Dispose();
        process?.Dispose();
        try
        {
            userMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        userMutex?.Dispose();
        lifetimeCancellation.Dispose();
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
