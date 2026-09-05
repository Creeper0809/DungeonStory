using System;
using System.Collections.Generic;
using UnityEngine;

public interface ICaptivityRuntime
{
    IReadOnlyList<CaptiveState> Captives { get; }
    IReadOnlyList<CaptivePolicyData> Policies { get; }
    bool TryGetCaptive(string captiveId, out CaptiveState captive);
    bool TryGetActor(string captiveId, out CharacterActor actor);
    bool TryGetHousing(string captiveId, out BuildableObject housing);
    bool TryGetRehabilitationFacility(
        string captiveId,
        out BuildableObject facility);
    bool IsCaptive(string persistentId);
    bool HasSecureHousing(CharacterActor captive, out BuildableObject housing, out string reason);
}

public interface ICaptivityRestoreStateQuery
{
    IReadOnlyList<CaptiveState> Captives { get; }
}

public interface ICaptiveLaborQuery
{
    bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string reason);
}

public interface ICaptivityWorkReadinessQuery
{
    bool IsInteractionReady(string captiveId, out string reason);
}

public interface ICaptivityCommandService
{
    bool TryOrderCapture(
        CharacterActor subject,
        CharacterActor carrier,
        out string failureReason);
    bool CancelCapture(string captiveId, string reason);
    bool TrySetPolicy(string captiveId, string policyId, out string failureReason);
    bool TryCreatePolicy(
        string displayName,
        out string policyId,
        out string failureReason);
    bool TryDuplicatePolicy(
        string sourcePolicyId,
        out string policyId,
        out string failureReason);
    bool TryUpdatePolicy(
        CaptivePolicyData policy,
        out string failureReason);
    bool TryDeletePolicy(string policyId, out string failureReason);
    bool TrySetLaborPermissions(
        string captiveId,
        CaptiveLaborPermission permissions,
        out string failureReason);
    bool TryStartInteraction(
        string captiveId,
        string interactionId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason);
    bool AdvanceInteraction(
        string captiveId,
        CharacterActor warden,
        float workAmount,
        out string status);
    bool TryRecruit(string captiveId, out string failureReason);
    bool TryConvertToMinion(string captiveId, out string failureReason);
    bool TryStartRehabilitation(
        string captiveId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason);
    bool AdvanceRehabilitation(
        string captiveId,
        CharacterActor warden,
        float approvedWork,
        out string status);
    bool TryRansom(
        string captiveId,
        out int paidAmount,
        out string failureReason);
    bool TryRelease(string captiveId, out string failureReason);
    bool TryTriggerBetrayal(
        string captiveId,
        string trigger,
        out string failureReason);
    bool TryAssignPerformer(string captiveId, bool assigned, out string failureReason);
    bool TryResolvePerformerMilestone(
        string captiveId,
        CaptivePerformerMilestoneChoice choice,
        out string failureReason);
    void RecordPerformance(
        string captiveId,
        float fameGain,
        float skillGain,
        bool injured);
}

public interface ICaptivityEscapeRuntime
{
    bool TryGetEscapeState(
        string captiveId,
        CharacterActor actor,
        out Vector2Int destination,
        out string failureReason);
    IDisposable BeginEscapePass(CharacterActor actor, string captiveId);
    void CompleteEscape(string captiveId, CharacterActor actor);
    void FailEscape(string captiveId, CharacterActor actor, string reason);
}

public interface IMinionSettlementCommand
{
    bool TryBeginDailySocialEvaluation(
        string minionId,
        int absoluteDay,
        out CaptiveState state);
    void RecordSocialConflict(
        string minionId,
        string result);
    bool TryBreakMinionControl(
        string minionId,
        string reason,
        out string failureReason);
}

public interface ICaptivityEscortRuntime
{
    IDisposable BeginEscortPass(CharacterActor carrier, string captiveId);
    bool TryGetEscortState(
        string captiveId,
        CharacterActor carrier,
        out CaptiveState captive,
        out CharacterActor subject,
        out string failureReason);
    bool TryPickupReservedRestraint(
        CaptiveState captive,
        CharacterActor carrier,
        out string failureReason);
    float AdvanceStabilization(
        string captiveId,
        CharacterActor carrier,
        float workAmount);
    bool TryBeginEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason);
    bool TryCompleteEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason);
    void FailEscort(string captiveId, CharacterActor carrier, string reason);
}
