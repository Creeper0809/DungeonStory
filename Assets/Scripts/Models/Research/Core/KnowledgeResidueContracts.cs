using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum KnowledgeResidueUse
{
    CodexAnalysis = 0,
    RegionReconnaissance = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum KnowledgeResidueDispositionPhase
{
    AwaitingInput = 0,
    InputCommitted = 1,
    OutcomePublished = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class KnowledgeResidueTaskSaveData
{
    public string taskId = string.Empty;
    public KnowledgeResidueUse use;
    public string regionId = string.Empty;
    public float requiredWork = 24f;
    public float completedWork;
    public int facilityId;
    public int facilityX;
    public int facilityY;
    public int assignmentSequence;
    public string destinationId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public long inputCapacityGrams;
    public long massAuthorityRevision;
    public string inputCapacityFingerprint = string.Empty;
    public KnowledgeResidueDispositionPhase dispositionPhase;
    public string sinkOperationId = string.Empty;
    public string sinkReasonCode = string.Empty;
    public string sinkRequestFingerprint = string.Empty;
    public List<string> sinkSourceStackIds = new List<string>();
    public long sinkInputMassGrams;
    public string sinkCommitId = string.Empty;
    public string codexCluePayload = string.Empty;
    public float appliedReconnaissanceAmount;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct KnowledgeResidueTaskSnapshot
{
    public KnowledgeResidueTaskSnapshot(KnowledgeResidueTaskSaveData source)
    {
        TaskId = source?.taskId ?? string.Empty;
        Use = source?.use ?? KnowledgeResidueUse.CodexAnalysis;
        RegionId = source?.regionId ?? string.Empty;
        RequiredWork = Mathf.Max(1f, source?.requiredWork ?? 1f);
        CompletedWork = Mathf.Clamp(
            source?.completedWork ?? 0f,
            0f,
            RequiredWork);
        FacilityId = source?.facilityId ?? 0;
        FacilityPosition = new Vector2Int(
            source?.facilityX ?? 0,
            source?.facilityY ?? 0);
        DestinationId = source?.destinationId ?? string.Empty;
        FacilityInstanceId = source?.facilityInstanceId ?? string.Empty;
        InputCapacityGrams = source?.inputCapacityGrams ?? 0L;
        DispositionPhase = source?.dispositionPhase
            ?? KnowledgeResidueDispositionPhase.AwaitingInput;
    }

    public string TaskId { get; }
    public KnowledgeResidueUse Use { get; }
    public string RegionId { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public float ProgressRatio => Mathf.Clamp01(CompletedWork / RequiredWork);
    public int FacilityId { get; }
    public Vector2Int FacilityPosition { get; }
    public string DestinationId { get; }
    public string FacilityInstanceId { get; }
    public long InputCapacityGrams { get; }
    public KnowledgeResidueDispositionPhase DispositionPhase { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class KnowledgeResidueAggregateState
{
    private readonly List<KnowledgeResidueTaskSaveData> tasks =
        new List<KnowledgeResidueTaskSaveData>();
    private int nextTaskSequence = 1;
    private float nextDeliveryCheckAt;
    private string readySignaledTaskId = string.Empty;

    public IReadOnlyList<KnowledgeResidueTaskSaveData> Tasks => tasks;
    public bool HasTasks => tasks.Count > 0;
    public KnowledgeResidueTaskSaveData FirstTask =>
        tasks.Count > 0 ? tasks[0] : null;
    public string ReadySignaledTaskId => readySignaledTaskId;

    public bool IsDeliveryCheckDue(float time) =>
        time >= nextDeliveryCheckAt;

    public void ScheduleNextDeliveryCheck(float time)
    {
        nextDeliveryCheckAt = time;
    }

    public bool SetReadySignal(string taskId)
    {
        string normalized = taskId?.Trim() ?? string.Empty;
        if (string.Equals(
                readySignaledTaskId,
                normalized,
                StringComparison.Ordinal))
        {
            return false;
        }

        readySignaledTaskId = normalized;
        return true;
    }

    public void ClearReadySignal()
    {
        readySignaledTaskId = string.Empty;
    }

    public int AllocateTaskSequence() => nextTaskSequence++;

    public void AddTask(KnowledgeResidueTaskSaveData task)
    {
        tasks.Add(task ?? throw new ArgumentNullException(nameof(task)));
    }

    public void AddRestoredTask(
        KnowledgeResidueTaskSaveData task,
        int sequence)
    {
        AddTask(task);
        nextTaskSequence = Math.Max(nextTaskSequence, sequence + 1);
    }

    public bool RemoveFirstTask()
    {
        if (tasks.Count == 0)
        {
            return false;
        }

        tasks.RemoveAt(0);
        return true;
    }

    public KnowledgeResidueAggregateState DeepClone()
    {
        KnowledgeResidueAggregateState clone = new KnowledgeResidueAggregateState
        {
            nextTaskSequence = nextTaskSequence,
            nextDeliveryCheckAt = nextDeliveryCheckAt,
            readySignaledTaskId = readySignaledTaskId
        };
        clone.tasks.AddRange(tasks.Select(CloneTask));
        return clone;
    }

    private static KnowledgeResidueTaskSaveData CloneTask(
        KnowledgeResidueTaskSaveData source)
    {
        return new KnowledgeResidueTaskSaveData
        {
            taskId = source?.taskId ?? string.Empty,
            use = source?.use ?? KnowledgeResidueUse.CodexAnalysis,
            regionId = source?.regionId ?? string.Empty,
            requiredWork = source?.requiredWork ?? 24f,
            completedWork = source?.completedWork ?? 0f,
            facilityId = source?.facilityId ?? 0,
            facilityX = source?.facilityX ?? 0,
            facilityY = source?.facilityY ?? 0,
            assignmentSequence = source?.assignmentSequence ?? 0,
            destinationId = source?.destinationId ?? string.Empty,
            facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
            inputCapacityGrams = source?.inputCapacityGrams ?? 0L,
            massAuthorityRevision = source?.massAuthorityRevision ?? 0L,
            inputCapacityFingerprint = source?.inputCapacityFingerprint
                ?? string.Empty,
            dispositionPhase = source?.dispositionPhase
                ?? KnowledgeResidueDispositionPhase.AwaitingInput,
            sinkOperationId = source?.sinkOperationId ?? string.Empty,
            sinkReasonCode = source?.sinkReasonCode ?? string.Empty,
            sinkRequestFingerprint = source?.sinkRequestFingerprint
                ?? string.Empty,
            sinkSourceStackIds = new List<string>(
                source?.sinkSourceStackIds ?? new List<string>()),
            sinkInputMassGrams = source?.sinkInputMassGrams ?? 0L,
            sinkCommitId = source?.sinkCommitId ?? string.Empty,
            codexCluePayload = source?.codexCluePayload ?? string.Empty,
            appliedReconnaissanceAmount =
                source?.appliedReconnaissanceAmount ?? 0f
        };
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class KnowledgeResidueRestoreCandidate
{
    public KnowledgeResidueRestoreCandidate(
        KnowledgeResidueAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    private KnowledgeResidueAggregateState State { get; }

    public IReadOnlyList<KnowledgeResidueTaskSaveData> CaptureTasks() =>
        State.DeepClone().Tasks;

    public KnowledgeResidueAggregateState TakeStateForRestore() => State;
}
