using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

// Host ownership crosses worker and Unity threads. The open handle is the
// lease; keeping its path avoids a close/delete race with the next owner.
internal sealed class DungeonStoryLlmHostOwnership : IDisposable
{
    private FileStream lease;

    private DungeonStoryLlmHostOwnership(FileStream lease) => this.lease = lease;

    internal static bool TryAcquire(out DungeonStoryLlmHostOwnership ownership, out string error)
    {
        string scope = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "session-" + Process.GetCurrentProcess().SessionId.ToString(CultureInfo.InvariantCulture)
            : "user";
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DungeonStory", "LlmHost", scope + ".lock");
        return TryAcquire(path, out ownership, out error);
    }

    internal static bool TryAcquire(string path, out DungeonStoryLlmHostOwnership ownership, out string error)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            throw new ArgumentException("Host ownership requires an absolute lease path.", nameof(path));
        ownership = null;
        error = string.Empty;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ownership = new DungeonStoryLlmHostOwnership(new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            return true;
        }
        catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33)
        {
            error = "Another DungeonStory process already owns the local narrative host.";
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = "Local narrative host ownership could not be acquired: " + exception.Message;
            return false;
        }
    }

    public void Dispose() => Interlocked.Exchange(ref lease, null)?.Dispose();
}
