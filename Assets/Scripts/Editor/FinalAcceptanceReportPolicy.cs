using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class FinalAcceptanceReportPolicy
{
    internal enum CoordinatorAction
    {
        Wait = 0,
        EvaluateReport = 1,
        Timeout = 2
    }

    internal static bool IsFreshPass(
        string report,
        long reportWrittenUtcTicks,
        long targetStartedUtcTicks)
    {
        if (reportWrittenUtcTicks < targetStartedUtcTicks
            || string.IsNullOrWhiteSpace(report))
        {
            return false;
        }

        return report
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Any(line => string.Equals(
                line.Trim(),
                "RESULT=PASS",
                StringComparison.Ordinal)
                || line.Trim().StartsWith(
                    "RESULT=PASS;",
                    StringComparison.Ordinal));
    }

    internal static bool AreFreshArtifacts(
        IEnumerable<string> paths,
        long targetStartedUtcTicks,
        out string[] failures)
    {
        List<string> artifactFailures = new();
        foreach (string path in (paths ?? Array.Empty<string>())
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!File.Exists(path))
            {
                artifactFailures.Add("missing=" + path);
                continue;
            }

            FileInfo file = new(path);
            if (file.Length <= 0)
            {
                artifactFailures.Add("empty=" + path);
            }
            if (file.LastWriteTimeUtc.Ticks < targetStartedUtcTicks)
            {
                artifactFailures.Add(
                    $"stale={path}; writtenUtcTicks="
                    + file.LastWriteTimeUtc.Ticks
                    + $"; startedUtcTicks={targetStartedUtcTicks}");
            }
        }

        failures = artifactFailures.ToArray();
        return failures.Length == 0;
    }

    internal static CoordinatorAction ResolveCoordinatorAction(
        bool isPlayingOrChanging,
        bool reportExists,
        long nowUtcTicks,
        long targetStartedUtcTicks,
        double timeoutSeconds)
    {
        double elapsedSeconds = new TimeSpan(
            nowUtcTicks - targetStartedUtcTicks).TotalSeconds;
        if (!isPlayingOrChanging && reportExists)
        {
            return CoordinatorAction.EvaluateReport;
        }

        if (elapsedSeconds > timeoutSeconds)
        {
            return CoordinatorAction.Timeout;
        }
        if (isPlayingOrChanging)
        {
            return CoordinatorAction.Wait;
        }
        return CoordinatorAction.Wait;
    }

    internal static void DeleteFiles(IEnumerable<string> paths)
    {
        foreach (string path in (paths ?? Array.Empty<string>())
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.Ordinal))
        {
            File.Delete(path);
        }
    }
}
