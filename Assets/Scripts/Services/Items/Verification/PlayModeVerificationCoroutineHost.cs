#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime-assembly host for Editor-owned PlayMode verification coroutines.
/// Unity cannot attach a MonoBehaviour compiled into an Editor assembly, so
/// the Editor coordinator publishes exactly one routine factory to this host.
/// </summary>
public sealed class PlayModeVerificationCoroutineHost : MonoBehaviour
{
    public static Func<IEnumerator> RunFactory { get; set; }

    private IEnumerator Start()
    {
        Func<IEnumerator> factory = RunFactory;
        RunFactory = null;
        if (factory == null)
        {
            throw new InvalidOperationException(
                "The PlayMode verification coroutine host has no run factory.");
        }

        IEnumerator routine = factory();
        if (routine == null)
        {
            throw new InvalidOperationException(
                "The PlayMode verification coroutine factory returned null.");
        }

        try
        {
            yield return routine;
        }
        finally
        {
            Destroy(gameObject);
        }
    }
}
#endif
