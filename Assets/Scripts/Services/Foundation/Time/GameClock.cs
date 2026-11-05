using UnityEngine;

namespace DungeonStory.Foundation
{
    public interface IGameClock
    {
        float DeltaTime { get; }
        float Time { get; }
        int FrameCount { get; }
        bool IsPaused { get; }
    }

    public interface IUiClock
    {
        float DeltaTime { get; }
        float Time { get; }
    }

    /// <summary>Monotonic wall time for recovery and external-operation deadlines, including while paused.</summary>
    public interface IRealtimeClock
    {
        float Time { get; }
        double TimeAsDouble { get; }
    }

    public interface IGameTimeScaleController
    {
        float Scale { get; set; }
    }

    public interface IGameTimeScalePauseAuthority
    {
        System.IDisposable HoldSimulation();
    }

    public interface IGameClockDiagnosticsControl
    {
        bool IsDeterministicCheckpointTimeEnabled { get; }
        void RebaseDeterministicCheckpointTime(float time, int frameCount);
        void DisableDeterministicCheckpointTime();
    }

    public sealed class UnityGameClock : IGameClock, IGameClockDiagnosticsControl
    {
        private bool deterministicCheckpointTimeEnabled;
        private float deterministicTimeOffset;
        private int deterministicFrameOffset;

        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float Time => UnityEngine.Time.time
            + (deterministicCheckpointTimeEnabled ? deterministicTimeOffset : 0f);
        public int FrameCount => UnityEngine.Time.frameCount
            + (deterministicCheckpointTimeEnabled ? deterministicFrameOffset : 0);
        public bool IsPaused => UnityEngine.Time.timeScale <= 0f;
        public bool IsDeterministicCheckpointTimeEnabled =>
            deterministicCheckpointTimeEnabled;

        public void RebaseDeterministicCheckpointTime(float time, int frameCount)
        {
            if (!Application.isEditor)
            {
                throw new System.InvalidOperationException(
                    "Deterministic checkpoint time is editor-only.");
            }
            deterministicTimeOffset = time - UnityEngine.Time.time;
            deterministicFrameOffset = frameCount - UnityEngine.Time.frameCount;
            deterministicCheckpointTimeEnabled = true;
        }

        public void DisableDeterministicCheckpointTime()
        {
            deterministicCheckpointTimeEnabled = false;
            deterministicTimeOffset = 0f;
            deterministicFrameOffset = 0;
        }
    }

    public sealed class UnityUiClock : IUiClock
    {
        public float DeltaTime => UnityEngine.Time.unscaledDeltaTime;
        // UI deadlines need the current wall clock even during synchronous
        // save/restore. unscaledTime is frozen at the beginning of the frame.
        public float Time => UnityEngine.Time.realtimeSinceStartup;
    }

    public sealed class UnityRealtimeClock : IRealtimeClock
    {
        public float Time => UnityEngine.Time.realtimeSinceStartup;
        public double TimeAsDouble => UnityEngine.Time.realtimeSinceStartupAsDouble;
    }

    public sealed class UnityGameTimeScaleController : IGameTimeScaleController, IGameTimeScalePauseAuthority
    {
        private static readonly float BaseFixedDeltaTime =
            ResolveBaseFixedDeltaTime();
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private int simulationHolds;
        private float requestedScale;

        public float Scale
        {
            get => UnityEngine.Time.timeScale;
            set
            {
                RequireThread();
                if (simulationHolds == 0) ApplyScale(value);
                else
                {
                    // 같은 동기 호출에서 원래 Unity 입력 검사를 거친다. 프레임은 진행하지 않는다.
                    try { ApplyScale(value); requestedScale = UnityEngine.Time.timeScale; }
                    finally { ApplyScale(0f); }
                }
            }
        }

        public System.IDisposable HoldSimulation()
        {
            RequireThread();
            if (simulationHolds == 0)
            {
                requestedScale = UnityEngine.Time.timeScale;
                ApplyScale(0f);
            }
            simulationHolds++;
            return new SimulationHold(this);
        }

        private void ReleaseSimulation()
        {
            RequireThread();
            if (simulationHolds <= 0) throw new System.InvalidOperationException("No simulation hold is owned.");
            if (simulationHolds == 1) ApplyScale(requestedScale);
            simulationHolds--;
        }

        private void RequireThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new System.InvalidOperationException("Simulation holds must use the owning thread.");
        }

        private static void ApplyScale(float value)
        {
            float scale = Mathf.Max(0f, value);
            UnityEngine.Time.timeScale = scale;
            UnityEngine.Time.fixedDeltaTime = scale > 0f ? BaseFixedDeltaTime * scale : BaseFixedDeltaTime;
        }

        private sealed class SimulationHold : System.IDisposable
        {
            private UnityGameTimeScaleController owner;
            public SimulationHold(UnityGameTimeScaleController owner) => this.owner = owner;
            public void Dispose()
            {
                if (owner == null) return;
                owner.ReleaseSimulation();
                owner = null;
            }
        }

        private static float ResolveBaseFixedDeltaTime()
        {
            float fixedDeltaTime = Mathf.Max(
                0.001f,
                UnityEngine.Time.fixedDeltaTime);
            float timeScale = UnityEngine.Time.timeScale;
            if (timeScale > 1f && fixedDeltaTime > 0.05f)
            {
                return Mathf.Max(0.001f, fixedDeltaTime / timeScale);
            }

            return fixedDeltaTime;
        }
    }
}
