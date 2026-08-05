using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusRestoreTransactionState
{
    public bool IsActive { get; private set; }
    public bool IsPrepared { get; private set; }

    public void Begin()
    {
        if (IsActive)
        {
            throw new InvalidOperationException(
                "A circus restore candidate is already active.");
        }

        IsActive = true;
        IsPrepared = false;
    }

    public void EnsureCanStage(bool aggregateRestoreIsStaging)
    {
        if (!IsActive || !aggregateRestoreIsStaging)
        {
            throw new InvalidOperationException(
                "Circus restore requires the V18 save registry transaction boundary.");
        }
        if (IsPrepared)
        {
            throw new InvalidOperationException(
                "A circus restore candidate was staged more than once.");
        }
    }

    public void MarkPrepared()
    {
        if (!IsActive || IsPrepared)
        {
            throw new InvalidOperationException(
                "Circus restore candidate staging was not valid.");
        }

        IsPrepared = true;
    }

    public void EnsureCanPublish()
    {
        if (!IsActive || !IsPrepared)
        {
            throw new InvalidOperationException(
                "No circus restore candidate is ready to publish.");
        }
    }

    public void CompletePublish()
    {
        EnsureCanPublish();
        IsPrepared = false;
        IsActive = false;
    }

    public void Discard()
    {
        IsPrepared = false;
        IsActive = false;
    }
}

public interface ICircusRestoreLifecycle
{
    string ParticipantId { get; }
    CircusRestoreCandidate BuildRestore(CircusSaveData saveData);
    void StageRestore(CircusRestoreCandidate candidate);
    void BeginRestoreCandidate();
    void PublishRestoreCandidate();
    void RollbackPublishedRestoreCandidate();
    void CompleteRestoreCandidate();
    void DiscardRestoreCandidate();
}
