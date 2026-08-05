using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CaptivityInteractionContext
{
    public CaptivityInteractionContext(
        CaptiveState captive,
        bool subjectAvailable,
        bool wardenAvailable,
        bool facilityAvailable,
        Vector2Int resultPosition)
    {
        Captive = captive;
        SubjectAvailable = subjectAvailable;
        WardenAvailable = wardenAvailable;
        FacilityAvailable = facilityAvailable;
        ResultPosition = resultPosition;
    }

    public CaptiveState Captive { get; }
    public bool SubjectAvailable { get; }
    public bool WardenAvailable { get; }
    public bool FacilityAvailable { get; }
    public Vector2Int ResultPosition { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CaptivityInteractionResult
{
    public CaptivityInteractionResult(
        bool success,
        string message,
        float willDelta = 0f,
        float fearDelta = 0f,
        float trustDelta = 0f,
        float grudgeDelta = 0f,
        float corruptionDelta = 0f,
        float healthDelta = 0f,
        string outputItemId = "",
        int outputAmount = 0)
    {
        Success = success;
        Message = message ?? string.Empty;
        WillDelta = willDelta;
        FearDelta = fearDelta;
        TrustDelta = trustDelta;
        GrudgeDelta = grudgeDelta;
        CorruptionDelta = corruptionDelta;
        HealthDelta = healthDelta;
        OutputItemId = outputItemId ?? string.Empty;
        OutputAmount = Mathf.Max(0, outputAmount);
    }

    public bool Success { get; }
    public string Message { get; }
    public float WillDelta { get; }
    public float FearDelta { get; }
    public float TrustDelta { get; }
    public float GrudgeDelta { get; }
    public float CorruptionDelta { get; }
    public float HealthDelta { get; }
    public string OutputItemId { get; }
    public int OutputAmount { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICaptivityInteractionHandler
{
    string InteractionId { get; }
    string DisplayName { get; }
    CaptiveInteractionKind Kind { get; }
    float RequiredWork { get; }
    IReadOnlyDictionary<StockCategory, int> MaterialRequirements { get; }
    bool CanExecute(CaptivityInteractionContext context, out string failureReason);
    CaptivityInteractionResult Execute(CaptivityInteractionContext context);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CaptivePerformerMilestoneEvent
{
    public CaptivePerformerMilestoneEvent(
        string captiveId,
        int fameThreshold,
        string message)
    {
        CaptiveId = captiveId ?? string.Empty;
        FameThreshold = fameThreshold;
        Message = message ?? string.Empty;
    }

    public string CaptiveId { get; }
    public int FameThreshold { get; }
    public string Message { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CaptiveRansomedEvent
{
    public CaptiveRansomedEvent(
        string captiveId,
        int amount,
        float retaliationPressure)
    {
        CaptiveId = captiveId ?? string.Empty;
        Amount = Mathf.Max(0, amount);
        RetaliationPressure = Mathf.Clamp(retaliationPressure, 0f, 100f);
    }

    public string CaptiveId { get; }
    public int Amount { get; }
    public float RetaliationPressure { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CaptiveEscapedEvent
{
    public CaptiveEscapedEvent(string captiveId, string trigger, bool betrayal)
    {
        CaptiveId = captiveId ?? string.Empty;
        Trigger = trigger ?? string.Empty;
        Betrayal = betrayal;
    }

    public string CaptiveId { get; }
    public string Trigger { get; }
    public bool Betrayal { get; }
}
