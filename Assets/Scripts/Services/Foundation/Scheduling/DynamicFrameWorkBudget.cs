using System;

namespace DungeonStory.Foundation
{
    public enum DynamicFrameWorkDomain
    {
        AiDecision,
        Pathfinding,
        WorldIndex,
        CharacterNeeds,
        CharacterDeprivation,
        CharacterHealth,
        Presentation,
        Wildlife,
        Work
    }

    public readonly struct DynamicFrameWorkSnapshot
    {
        public DynamicFrameWorkSnapshot(
            double smoothedFrameMilliseconds,
            double availableMilliseconds,
            double consumedMilliseconds,
            int totalBacklog)
        {
            SmoothedFrameMilliseconds = smoothedFrameMilliseconds;
            AvailableMilliseconds = availableMilliseconds;
            ConsumedMilliseconds = consumedMilliseconds;
            TotalBacklog = totalBacklog;
        }

        public double SmoothedFrameMilliseconds { get; }
        public double AvailableMilliseconds { get; }
        public double ConsumedMilliseconds { get; }
        public int TotalBacklog { get; }
    }

    public interface IDynamicFrameWorkBudget
    {
        void SetBacklog(DynamicFrameWorkDomain domain, int count);
        bool CanStart(
            DynamicFrameWorkDomain domain,
            bool urgent = false);
        double GetSliceMilliseconds(
            DynamicFrameWorkDomain domain,
            double minimumMilliseconds,
            double maximumMilliseconds,
            bool urgent = false);
        void ReportConsumed(
            DynamicFrameWorkDomain domain,
            double elapsedMilliseconds);
        DynamicFrameWorkSnapshot GetSnapshot();
    }

    public sealed class DynamicFrameWorkBudget : IDynamicFrameWorkBudget
    {
        private const double TargetFrameMilliseconds = 1000.0 / 60.0;
        private const double MinimumSharedBudgetMilliseconds = 0.25;
        private const double MaximumSharedBudgetMilliseconds = 8.0;
        private const double FrameSampleWeight = 0.12;

        private static readonly double[] BaseWeights =
        {
            3.0,
            2.0,
            1.0,
            1.0,
            1.0,
            1.0,
            0.75,
            1.0,
            2.0
        };

        private readonly IGameClock gameClock;
        private readonly IUiClock uiClock;
        private readonly int[] backlogs =
            new int[Enum.GetValues(typeof(DynamicFrameWorkDomain)).Length];
        private readonly double[] consumed =
            new double[Enum.GetValues(typeof(DynamicFrameWorkDomain)).Length];
        private int activeFrame = -1;
        private double smoothedFrameMilliseconds = TargetFrameMilliseconds;
        private double availableMilliseconds = 2.0;
        private double consumedMilliseconds;

        public DynamicFrameWorkBudget(IGameClock gameClock, IUiClock uiClock)
        {
            this.gameClock = gameClock
                ?? throw new ArgumentNullException(nameof(gameClock));
            this.uiClock = uiClock
                ?? throw new ArgumentNullException(nameof(uiClock));
        }

        public void SetBacklog(DynamicFrameWorkDomain domain, int count)
        {
            EnsureFrame();
            backlogs[(int)domain] = Math.Max(0, count);
        }

        public bool CanStart(
            DynamicFrameWorkDomain domain,
            bool urgent = false)
        {
            EnsureFrame();
            if (urgent)
            {
                return true;
            }

            int index = (int)domain;
            return consumedMilliseconds < availableMilliseconds
                && consumed[index] < GetDomainShare(index);
        }

        public double GetSliceMilliseconds(
            DynamicFrameWorkDomain domain,
            double minimumMilliseconds,
            double maximumMilliseconds,
            bool urgent = false)
        {
            EnsureFrame();
            double minimum = Math.Max(0.01, minimumMilliseconds);
            double maximum = Math.Max(minimum, maximumMilliseconds);
            int index = (int)domain;
            double share = GetDomainShare(index);
            double remainingDomainShare = share - consumed[index];
            double remainingGlobal = availableMilliseconds - consumedMilliseconds;
            double borrowMultiplier = urgent ? 1.5 : 1.0;
            double result = Math.Min(
                maximum,
                Math.Max(
                    minimum,
                    Math.Min(
                        Math.Max(minimum, remainingDomainShare),
                        Math.Max(minimum, remainingGlobal * borrowMultiplier))));
            return result;
        }

        public void ReportConsumed(
            DynamicFrameWorkDomain domain,
            double elapsedMilliseconds)
        {
            EnsureFrame();
            double elapsed = Math.Max(0.0, elapsedMilliseconds);
            consumed[(int)domain] += elapsed;
            consumedMilliseconds += elapsed;
        }

        public DynamicFrameWorkSnapshot GetSnapshot()
        {
            EnsureFrame();
            int totalBacklog = 0;
            for (int i = 0; i < backlogs.Length; i++)
            {
                totalBacklog += backlogs[i];
            }

            return new DynamicFrameWorkSnapshot(
                smoothedFrameMilliseconds,
                availableMilliseconds,
                consumedMilliseconds,
                totalBacklog);
        }

        private void EnsureFrame()
        {
            int frame = gameClock.FrameCount;
            if (frame == activeFrame)
            {
                return;
            }

            activeFrame = frame;
            double frameMilliseconds = Math.Max(
                0.01,
                uiClock.DeltaTime * 1000.0);
            smoothedFrameMilliseconds +=
                (frameMilliseconds - smoothedFrameMilliseconds)
                * FrameSampleWeight;
            double headroom = TargetFrameMilliseconds
                - smoothedFrameMilliseconds;
            availableMilliseconds = Math.Clamp(
                1.25 + Math.Max(-1.0, headroom) * 0.55,
                MinimumSharedBudgetMilliseconds,
                MaximumSharedBudgetMilliseconds);
            Array.Clear(consumed, 0, consumed.Length);
            consumedMilliseconds = 0.0;
        }

        private double GetPressure(int index)
        {
            int backlog = backlogs[index];
            return BaseWeights[index]
                + (backlog > 0 ? Math.Log(backlog + 1, 2.0) : 0.0);
        }

        private double GetDomainShare(int index)
        {
            double totalPressure = 0.0;
            for (int i = 0; i < backlogs.Length; i++)
            {
                totalPressure += GetPressure(i);
            }

            return totalPressure > 0.0
                ? availableMilliseconds * GetPressure(index) / totalPressure
                : availableMilliseconds / backlogs.Length;
        }
    }
}
