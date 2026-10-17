#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;

// One execution/error boundary for Editor-owned integration coroutines.
internal static class PlayModeVerificationCoroutine
{
    // Unity runs nested IEnumerator values itself; observe the entire tree so a
    // child exception cannot strand an Editor verification with a pending result.
    internal static IEnumerator Observe(
        Func<IEnumerator> createRun, Action<Exception> onFailure)
    {
        if (createRun == null) throw new ArgumentNullException(nameof(createRun));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));
        Stack<IEnumerator> routines = new();
        Exception failure = null;
        bool completed = false;
        try
        {
            try
            {
                routines.Push(createRun()
                    ?? throw new InvalidOperationException("PlayMode verification coroutine factory returned null."));
            }
            catch (Exception exception) { failure = exception; }
            while (failure == null && routines.Count > 0)
            {
                object yielded = null;
                bool yieldToUnity = false;
                try
                {
                    IEnumerator current = routines.Peek();
                    if (!current.MoveNext())
                    {
                        routines.Pop();
                        (current as IDisposable)?.Dispose();
                    }
                    else
                    {
                        yielded = current.Current;
                        if (yielded is IEnumerator child) routines.Push(child);
                        else yieldToUnity = true;
                    }
                }
                catch (Exception exception) { failure = exception; }
                if (yieldToUnity) yield return yielded;
            }
            completed = failure == null;
        }
        finally
        {
            while (routines.Count > 0)
            {
                try { (routines.Pop() as IDisposable)?.Dispose(); }
                catch (Exception exception)
                {
                    failure = failure == null ? exception : new AggregateException(failure, exception);
                }
            }
            if (!completed && failure == null)
                failure = new InvalidOperationException("PlayMode verification coroutine was stopped before completion.");
            if (failure != null) onFailure(failure);
        }
    }

}
#endif
