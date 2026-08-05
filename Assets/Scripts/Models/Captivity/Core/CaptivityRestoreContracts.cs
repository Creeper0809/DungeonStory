using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CaptivityRestoreCandidate
{
    internal CaptivityRestoreCandidate(CaptivityAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CaptivityAggregateState State { get; }

    public IReadOnlyList<CaptiveState> Captives =>
        State.Captives.Select(item => item.Clone()).ToArray();

    public static CaptivityRestoreCandidate Create(CaptivitySaveData payload) =>
        new CaptivityRestoreCandidate(CaptivitySaveValidation.CreateState(payload));

    public bool TryGetCaptive(string captiveId, out CaptiveState captive)
    {
        CaptiveState found = State.Captives.FirstOrDefault(item =>
            string.Equals(
                item?.captiveId,
                captiveId?.Trim(),
                StringComparison.Ordinal));
        captive = found?.Clone();
        return captive != null;
    }
}

public sealed class CircusRestoreCandidate
{
    internal CircusRestoreCandidate(
        CircusAggregateState circus,
        CapturedWildlifeAggregateState capturedWildlife)
    {
        Circus = circus ?? throw new ArgumentNullException(nameof(circus));
        CapturedWildlife = capturedWildlife
            ?? throw new ArgumentNullException(nameof(capturedWildlife));
    }

    internal CircusAggregateState Circus { get; }
    internal CapturedWildlifeAggregateState CapturedWildlife { get; }

    public IReadOnlyList<CircusShowOrder> Orders =>
        Circus.Orders.Select(item => item.Clone()).ToArray();

    public IReadOnlyList<CapturedWildlifeState> CapturedWildlifeStates =>
        CapturedWildlife.Captured.Values.Select(item => item.Clone()).ToArray();

    public static CircusRestoreCandidate Create(CircusSaveData payload) =>
        new CircusRestoreCandidate(
            CircusSaveValidation.CreateCircusState(payload),
            CircusSaveValidation.CreateCapturedWildlifeState(payload));
}

public interface ICaptivityPersistence
{
    CaptivitySaveData Capture();
    CaptivityRestoreCandidate BuildRestore(CaptivitySaveData payload);
    void PublishRestoreCandidate(CaptivityRestoreCandidate candidate);
}

public interface ICaptivityRestoreCandidateSource
{
    bool TryTakePreparedRestoreCandidate(
        out CaptivityRestoreCandidate candidate);
}

public interface ICaptivityEscortRestoreLifecycle
{
    void ClearTransientState();
    void RestoreCaptiveParent(string captiveId);
}

public interface ICircusPersistence
{
    CircusSaveData Capture();
    CircusRestoreCandidate BuildRestore(CircusSaveData payload);
    void PublishRestoreCandidate(CircusRestoreCandidate candidate);
}
