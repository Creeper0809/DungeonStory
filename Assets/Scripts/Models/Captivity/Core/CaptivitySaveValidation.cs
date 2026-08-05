using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CaptivitySaveValidation
{
    public const int MaximumCaptives = 512;
    public const int MaximumPolicies = 128;
    private const string CustomPolicyPrefix = "captivity:custom:";

    public static void Validate(
        CaptivitySaveData payload,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (payload == null)
        {
            report.AddError("Captivity payload is null.");
            return;
        }
        if (payload.version != CaptivitySaveData.CurrentVersion)
        {
            report.AddError(
                $"Captivity payload version {payload.version} is invalid.");
        }
        if (payload.captureSequence < 0 || payload.policySequence < 0)
        {
            report.AddError("Captivity sequences cannot be negative.");
        }
        if (payload.policies == null || payload.captives == null)
        {
            report.AddError("Captivity payload is missing a required list.");
            return;
        }
        if (payload.policies.Count > MaximumPolicies)
        {
            report.AddError(
                $"Captivity payload exceeds {MaximumPolicies} policies.");
        }
        if (payload.captives.Count > MaximumCaptives)
        {
            report.AddError(
                $"Captivity payload exceeds {MaximumCaptives} captives.");
        }

        HashSet<string> policyIds = new(StringComparer.Ordinal);
        int highestPolicySequence = 0;
        bool hasStandardPolicy = false;
        foreach (CaptivePolicyData policy in payload.policies)
        {
            string policyId = policy?.policyId ?? string.Empty;
            if (policy == null
                || !IsCanonicalNonEmpty(policyId)
                || !policyIds.Add(policyId)
                || !IsCanonicalNonEmpty(policy.displayName)
                || HasUnknownLaborFlags(policy.allowedLabor))
            {
                report.AddError(
                    $"Captivity payload contains invalid policy '{policyId}'.");
                continue;
            }

            if (CaptivityPolicyIds.IsBuiltIn(policyId))
            {
                hasStandardPolicy |= string.Equals(
                    policyId,
                    CaptivityPolicyIds.Standard,
                    StringComparison.Ordinal);
            }
            else if (!TryParseCustomPolicyId(
                         policyId,
                         out int policySequence))
            {
                report.AddError(
                    $"Captivity policy ID '{policyId}' is not canonical.");
            }
            else
            {
                highestPolicySequence = Math.Max(
                    highestPolicySequence,
                    policySequence);
            }
        }

        if (!hasStandardPolicy)
        {
            report.AddError(
                "Captivity payload is missing the standard policy.");
        }
        if (payload.policySequence < highestPolicySequence)
        {
            report.AddError(
                $"Captivity policy sequence {payload.policySequence} is below saved policy sequence {highestPolicySequence}.");
        }

        HashSet<string> captiveIds = new(StringComparer.Ordinal);
        foreach (CaptiveState captive in payload.captives)
        {
            ValidateCaptive(captive, policyIds, captiveIds, report);
        }
    }

    internal static CaptivityAggregateState CreateState(
        CaptivitySaveData payload)
    {
        CaptivityAggregateState state = new()
        {
            CaptureSequence = payload.captureSequence,
            PolicySequence = payload.policySequence
        };
        foreach (CaptivePolicyData policy in payload.policies)
        {
            state.Policies.Add(policy.Clone());
        }
        foreach (CaptiveState source in payload.captives)
        {
            state.Captives.Add(CreateRestoredCaptive(source));
        }
        return state;
    }

    public static CaptiveState CreateRestoredCaptive(CaptiveState source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        CaptiveState captive = source.Clone();
        if (captive.status == CaptivityStatus.Escorting)
        {
            captive.status = CaptivityStatus.AwaitingCapture;
            captive.reservedCarrierId = string.Empty;
            captive.housingBuildingId = string.Empty;
            captive.restraintStackId = string.Empty;
            captive.restraintItemId = string.Empty;
            captive.restraintQuantity = 0;
            captive.restrained = false;
            captive.lastResult = "호송 예약 재설정 필요";
        }
        return captive;
    }

    public static bool RequiresHousing(CaptivityStatus status)
    {
        return status is CaptivityStatus.Confined
            or CaptivityStatus.Labor
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer
            or CaptivityStatus.EscapeAttempt;
    }

    public static bool IsDoorCaptive(CaptivityStatus status)
    {
        return status is CaptivityStatus.AwaitingCapture
            or CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort
            or CaptivityStatus.Escorting
            or CaptivityStatus.Confined
            or CaptivityStatus.Labor
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer
            or CaptivityStatus.EscapeAttempt;
    }

    private static void ValidateCaptive(
        CaptiveState captive,
        ISet<string> policyIds,
        ISet<string> captiveIds,
        DungeonGameRestoreReport report)
    {
        string captiveId = captive?.captiveId ?? string.Empty;
        if (captive == null
            || !IsCanonicalCharacterId(captiveId)
            || !captiveIds.Add(captiveId)
            || !Enum.IsDefined(typeof(CaptivityStatus), captive.status)
            || captive.status == CaptivityStatus.None
            || !IsCanonicalNonEmpty(captive.policyId)
            || !policyIds.Contains(captive.policyId)
            || HasNullString(captive))
        {
            report.AddError(
                $"Captivity payload contains invalid captive '{captiveId}'.");
            return;
        }

        if (!IsOptionalCharacterId(captive.reservedCarrierId)
            || !IsOptionalCharacterId(captive.reservedWardenId)
            || string.Equals(
                captive.reservedCarrierId,
                captiveId,
                StringComparison.Ordinal)
            || string.Equals(
                captive.reservedWardenId,
                captiveId,
                StringComparison.Ordinal)
            || !IsOptionalBuildingId(captive.housingBuildingId)
            || !IsOptionalStackId(captive.restraintStackId)
            || !IsOptionalItemId(captive.restraintItemId))
        {
            report.AddError(
                $"Captive '{captiveId}' contains an invalid persistent reference.");
        }

        bool hasRestraintStack = captive.restraintStackId.Length > 0;
        bool hasRestraintItem = captive.restraintItemId.Length > 0;
        if (hasRestraintStack != hasRestraintItem
            || (hasRestraintItem && captive.restraintQuantity <= 0)
            || (!hasRestraintItem && captive.restraintQuantity != 0))
        {
            report.AddError(
                $"Captive '{captiveId}' has incoherent restraint state.");
        }

        if (!IsPercentage(captive.will)
            || !IsPercentage(captive.fear)
            || !IsPercentage(captive.trust)
            || !IsPercentage(captive.grudge)
            || !IsPercentage(captive.corruption)
            || !IsPercentage(captive.compliance)
            || !IsPercentage(captive.escapeRisk)
            || !IsPercentage(captive.health)
            || !IsPercentage(captive.performerSkill)
            || !IsPercentage(captive.performerFame)
            || !IsPercentage(captive.retaliationPressure)
            || !IsFiniteAtLeast(captive.nextCareSupplyAt, 0f)
            || !IsFiniteAtLeast(captive.nextSecurityCheckAt, 0f)
            || !IsFiniteAtLeast(captive.completedInteractionWork, 0f)
            || !IsFiniteAtLeast(captive.requiredInteractionWork, 0f)
            || captive.completedInteractionWork
                > captive.requiredInteractionWork
            || captive.performerInjuries < 0
            || captive.privilegeTier < 0
            || captive.privilegeTier > 2
            || captive.failedEscapeAttempts < 0
            || HasUnknownLaborFlags(captive.laborPermissions)
            || !Enum.IsDefined(
                typeof(CaptivePerformerMilestoneChoice),
                captive.resolvedMilestoneChoice))
        {
            report.AddError(
                $"Captive '{captiveId}' contains invalid numeric or enum state.");
        }

        bool hasInteraction = captive.currentInteractionId.Length > 0;
        if (hasInteraction !=
                (captive.status == CaptivityStatus.Interaction)
            || (hasInteraction
                && (captive.reservedWardenId.Length == 0
                    || captive.housingBuildingId.Length == 0
                    || captive.interactionMaterialDestinationId.Length == 0
                    || captive.requiredInteractionWork <= 0f))
            || (!hasInteraction
                && (captive.reservedWardenId.Length > 0
                    || captive.interactionMaterialDestinationId.Length > 0
                    || captive.interactionMaterialsConsumed)))
        {
            report.AddError(
                $"Captive '{captiveId}' has incoherent interaction state.");
        }

        if (RequiresHousing(captive.status)
            && captive.housingBuildingId.Length == 0)
        {
            report.AddError(
                $"Captive '{captiveId}' requires a housing building.");
        }
        bool transportActive = captive.status is CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort
            or CaptivityStatus.Escorting;
        if (transportActive
            && (captive.reservedCarrierId.Length == 0
                || captive.housingBuildingId.Length == 0
                || !hasRestraintItem))
        {
            report.AddError(
                $"Captive '{captiveId}' has incomplete escort state.");
        }
        if (captive.status == CaptivityStatus.AwaitingCapture
            && captive.reservedCarrierId.Length > 0)
        {
            report.AddError(
                $"Captive '{captiveId}' has a carrier without an active escort order.");
        }
        if (captive.status == CaptivityStatus.Labor
            && captive.laborPermissions == CaptiveLaborPermission.None)
        {
            report.AddError(
                $"Captive '{captiveId}' is in labor state without labor permissions.");
        }
        if (captive.finalContractPending
            && captive.resolvedMilestoneChoice
                != CaptivePerformerMilestoneChoice.None)
        {
            report.AddError(
                $"Captive '{captiveId}' has both pending and resolved final contracts.");
        }
    }

    private static bool HasNullString(CaptiveState captive)
    {
        return captive.displayName == null
            || captive.speciesTag == null
            || captive.policyId == null
            || captive.reservedCarrierId == null
            || captive.reservedWardenId == null
            || captive.housingBuildingId == null
            || captive.restraintStackId == null
            || captive.restraintItemId == null
            || captive.currentInteractionId == null
            || captive.interactionMaterialDestinationId == null
            || captive.lastResult == null
            || captive.betrayalTrigger == null;
    }

    private static bool IsCanonicalNonEmpty(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool IsCanonicalCharacterId(string value)
    {
        return IsCanonicalNonEmpty(value) && ((CharacterId)value).IsValid;
    }

    private static bool IsOptionalCharacterId(string value)
    {
        return value != null
            && (value.Length == 0 || IsCanonicalCharacterId(value));
    }

    private static bool IsOptionalBuildingId(string value)
    {
        return value != null
            && (value.Length == 0
                || (IsCanonicalNonEmpty(value)
                    && ((BuildingInstanceId)value).IsValid));
    }

    private static bool IsOptionalStackId(string value)
    {
        return value != null
            && (value.Length == 0
                || (IsCanonicalNonEmpty(value)
                    && ((ItemStackId)value).IsValid));
    }

    private static bool IsOptionalItemId(string value)
    {
        return value != null
            && (value.Length == 0
                || (IsCanonicalNonEmpty(value)
                    && IsValidItemDefinitionId(value)));
    }

    private static bool IsValidItemDefinitionId(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(normalized);
    }

    private static bool TryParseCustomPolicyId(
        string policyId,
        out int sequence)
    {
        sequence = 0;
        return policyId.StartsWith(CustomPolicyPrefix, StringComparison.Ordinal)
            && int.TryParse(
                policyId.Substring(CustomPolicyPrefix.Length),
                out sequence)
            && sequence > 0;
    }

    private static bool HasUnknownLaborFlags(
        CaptiveLaborPermission permissions)
    {
        return (permissions & ~CaptiveLaborPermission.All) != 0;
    }

    private static bool IsPercentage(float value)
    {
        return IsFiniteAtLeast(value, 0f) && value <= 100f;
    }

    private static bool IsFiniteAtLeast(float value, float minimum)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum;
    }
}
