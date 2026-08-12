using System;
using System.Collections;
using UnityEngine.Scripting.APIUpdating;

public interface ICaptivityWorkExecutionSession
{
    bool CanContinue { get; }
    bool HasCurrentWork { get; }
    bool IsCompleted { get; }
    bool TryAdvance(out string status);
    bool TrySuspendAtCheckpoint();
    void SetStatus(string status);
    void Complete(bool succeeded);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WardenWorkExecutionHandler
{
    public IEnumerator Execute(ICaptivityWorkExecutionSession session) =>
        CaptivityWorkExecutionFlow.Execute(
            session ?? throw new ArgumentNullException(nameof(session)));
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class PerformWorkExecutionHandler
{
    public IEnumerator Execute(ICaptivityWorkExecutionSession session) =>
        CaptivityWorkExecutionFlow.Execute(
            session ?? throw new ArgumentNullException(nameof(session)));
}

internal static class CaptivityWorkExecutionFlow
{
    public static IEnumerator Execute(ICaptivityWorkExecutionSession session)
    {
        if (!session.HasCurrentWork)
        {
            session.Complete(succeeded: false);
            yield break;
        }

        while (session.CanContinue && session.HasCurrentWork)
        {
            if (!session.TryAdvance(out string status))
            {
                session.SetStatus(status);
                session.Complete(succeeded: false);
                yield break;
            }

            session.SetStatus(status);
            if (!session.IsCompleted && session.TrySuspendAtCheckpoint())
            {
                session.Complete(succeeded: false);
                yield break;
            }
            yield return null;
        }

        session.Complete(session.IsCompleted);
    }
}
