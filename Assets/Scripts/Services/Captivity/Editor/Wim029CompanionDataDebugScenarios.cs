#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class Wim029CompanionDataDebugScenarios
{
    public static string RunAll()
    {
        List<string> rows = new();
        int passed = 0;

        Run(rows, ref passed, "PROFILE_DEFAULTS_AND_SNAPSHOT",
            VerifyProfileDefaultsAndSnapshot);
        Run(rows, ref passed, "PROFILE_REJECTS_CLOSE_RADIUS_ZERO",
            VerifyProfileRejectsCloseRadiusZero);
        Run(rows, ref passed, "PROFILE_REJECTS_RESUME_NOT_ABOVE_CLOSE",
            VerifyProfileRejectsResumeNotAboveClose);
        Run(rows, ref passed, "PROFILE_REJECTS_LEASH_BELOW_RESUME",
            VerifyProfileRejectsLeashBelowResume);
        Run(rows, ref passed, "PROFILE_REJECTS_DECISION_INTERVAL_ZERO",
            VerifyProfileRejectsDecisionIntervalZero);
        Run(rows, ref passed, "PROFILE_REJECTS_DECISION_INTERVAL_ABOVE_MAXIMUM",
            VerifyProfileRejectsDecisionIntervalAboveMaximum);
        Run(rows, ref passed, "PROFILE_REJECTS_NAN_DAMAGE",
            VerifyProfileRejectsNanDamage);
        Run(rows, ref passed, "PROFILE_REJECTS_NEGATIVE_COOLDOWN",
            VerifyProfileRejectsNegativeCooldown);
        Run(rows, ref passed, "PROFILE_REJECTS_INFINITE_COOLDOWN",
            VerifyProfileRejectsInfiniteCooldown);
        Run(rows, ref passed, "PROFILE_REJECTS_TWO_CELL_MELEE",
            VerifyProfileRejectsTwoCellMelee);
        Run(rows, ref passed, "PROFILE_REJECTS_TWO_COMPANIONS_PER_OWNER",
            VerifyProfileRejectsTwoCompanionsPerOwner);
        Run(rows, ref passed, "CAPABILITY_NONE_EXACT_TUPLE_AND_READ",
            VerifyNoneExactTupleAndRead);
        Run(rows, ref passed, "CAPABILITY_COMPANION_EXACT_ROUND_TRIP_AND_CLONE",
            VerifyCompanionExactRoundTripAndClone);
        Run(rows, ref passed, "CAPABILITY_REJECTS_NULL_STATE",
            VerifyNullStateRejected);
        Run(rows, ref passed, "CAPABILITY_REJECTS_UNKNOWN_ENVELOPE_VERSION",
            VerifyUnknownEnvelopeVersionRejected);
        Run(rows, ref passed, "CAPABILITY_REJECTS_UNKNOWN_ROLE",
            VerifyUnknownRoleRejected);
        Run(rows, ref passed, "CAPABILITY_REJECTS_SPACED_ROLE_WITHOUT_NORMALIZING",
            VerifySpacedRoleRejectedWithoutNormalizing);
        Run(rows, ref passed, "CAPABILITY_REJECTS_NONE_PAYLOAD",
            VerifyNonePayloadRejected);
        Run(rows, ref passed, "COMPANION_REJECTS_UNKNOWN_PAYLOAD_VERSION",
            VerifyUnknownCompanionPayloadVersionRejected);
        Run(rows, ref passed, "COMPANION_REJECTS_MISSING_OWNER",
            VerifyMissingCompanionOwnerRejected);
        Run(rows, ref passed, "COMPANION_REJECTS_MISSING_COOLDOWN",
            VerifyMissingCompanionCooldownRejected);
        Run(rows, ref passed, "COMPANION_REJECTS_MALFORMED_JSON",
            VerifyMalformedCompanionJsonRejected);
        Run(rows, ref passed, "COMPANION_REJECTS_NEGATIVE_COOLDOWN",
            VerifyNegativeCompanionCooldownRejected);
        Run(rows, ref passed, "CAPABILITY_RESERVED_HAUL_REJECTED",
            VerifyReservedHaulRejected);

        rows.Add($"RESULT={(passed == rows.Count ? "PASS" : "FAIL")}; "
            + $"passed={passed}; failed={rows.Count - passed}; rows={rows.Count}");
        return string.Join(Environment.NewLine, rows);
    }

    private static void VerifyProfileDefaultsAndSnapshot()
    {
        WildlifeCompanionRoleProfile profile =
            WildlifeCompanionRoleProfile.OwnerCompanion();
        WildlifeCompanionRoleProfile snapshot = profile.Snapshot();

        Require(profile.CloseRadiusCells == 2
            && profile.ResumeFollowDistanceCells == 3
            && profile.OwnerTargetLeashCells == 6
            && profile.DecisionIntervalSeconds == 0.5f
            && profile.BaseAttackDamage == 6f
            && profile.MeleeRangeCells == 1
            && profile.AttackCooldownSeconds == 2f
            && profile.MaximumCompanionsPerOwner == 1,
            "Owner-companion defaults drifted from 2/3/6/0.5/6/1/2/1.");
        Require(!ReferenceEquals(profile, snapshot)
            && snapshot.CloseRadiusCells == 2
            && snapshot.ResumeFollowDistanceCells == 3
            && snapshot.OwnerTargetLeashCells == 6
            && snapshot.DecisionIntervalSeconds == 0.5f
            && snapshot.BaseAttackDamage == 6f
            && snapshot.MeleeRangeCells == 1
            && snapshot.AttackCooldownSeconds == 2f
            && snapshot.MaximumCompanionsPerOwner == 1
            && profile.Validate().Count == 0
            && snapshot.Validate().Count == 0,
            "Snapshot was aliased, changed a default, or failed validation.");
    }

    private static void VerifyProfileRejectsCloseRadiusZero() =>
        RequireProfileRejected(0, 3, 6, 0.5f, 6f, 1, 2f, 1,
            "Close radius zero was accepted.");

    private static void VerifyProfileRejectsResumeNotAboveClose() =>
        RequireProfileRejected(2, 2, 6, 0.5f, 6f, 1, 2f, 1,
            "Resume distance equal to close radius was accepted.");

    private static void VerifyProfileRejectsLeashBelowResume() =>
        RequireProfileRejected(2, 3, 2, 0.5f, 6f, 1, 2f, 1,
            "Target leash below resume distance was accepted.");

    private static void VerifyProfileRejectsDecisionIntervalZero() =>
        RequireProfileRejected(2, 3, 6, 0f, 6f, 1, 2f, 1,
            "Zero decision interval was accepted.");

    private static void VerifyProfileRejectsDecisionIntervalAboveMaximum() =>
        RequireProfileRejected(2, 3, 6, 0.5001f, 6f, 1, 2f, 1,
            "Decision interval above 0.5 seconds was accepted.");

    private static void VerifyProfileRejectsNanDamage() =>
        RequireProfileRejected(2, 3, 6, 0.5f, float.NaN, 1, 2f, 1,
            "NaN attack damage was accepted.");

    private static void VerifyProfileRejectsNegativeCooldown() =>
        RequireProfileRejected(2, 3, 6, 0.5f, 6f, 1, -1f, 1,
            "Negative attack cooldown was accepted.");

    private static void VerifyProfileRejectsInfiniteCooldown() =>
        RequireProfileRejected(2, 3, 6, 0.5f, 6f, 1,
            float.PositiveInfinity, 1,
            "Infinite attack cooldown was accepted.");

    private static void VerifyProfileRejectsTwoCellMelee() =>
        RequireProfileRejected(2, 3, 6, 0.5f, 6f, 2, 2f, 1,
            "Two-cell melee range was accepted.");

    private static void VerifyProfileRejectsTwoCompanionsPerOwner() =>
        RequireProfileRejected(2, 3, 6, 0.5f, 6f, 1, 2f, 2,
            "Two companions per owner was accepted.");

    private static void VerifyNoneExactTupleAndRead()
    {
        CapturedWildlifeCapabilityState state =
            CapturedWildlifeCapabilityStateCodec.CreateNone();

        Require(state.version == 1
            && state.roleId == "wildlife-role:none"
            && state.payloadVersion == 0
            && state.payloadJson == string.Empty,
            "None capability tuple drifted from 1/none/0/empty.");
        Require(CapturedWildlifeCapabilityStateCodec.TryRead(
                state,
                out CapturedWildlifeRoleId roleId,
                out string failureReason)
            && roleId.IsValid
            && roleId.Value == "wildlife-role:none"
            && failureReason == string.Empty,
            "Exact none capability did not read as wildlife-role:none.");
    }

    private static void VerifyCompanionExactRoundTripAndClone()
    {
        CharacterId ownerId = new("character:staff:1:01");
        CapturedWildlifeCapabilityState state =
            CapturedWildlifeCapabilityStateCodec.CreateCompanion(ownerId, 12.5f);

        Require(state.version == 1
            && state.roleId == "wildlife-role:companion"
            && state.payloadVersion == 1
            && state.payloadJson
                == "{\"ownerCharacterId\":\"character:staff:1:01\",\"nextAttackAt\":12.5}",
            "Companion capability tuple drifted from the fixed owner/cooldown input.");
        Require(CapturedWildlifeCapabilityStateCodec.TryRead(
                state,
                out CapturedWildlifeRoleId roleId,
                out string roleFailure)
            && roleId.IsValid
            && roleId.Value == "wildlife-role:companion"
            && roleFailure == string.Empty,
            "Companion envelope did not read as wildlife-role:companion.");
        Require(CapturedWildlifeCapabilityStateCodec.TryReadCompanion(
                state,
                out CharacterId restoredOwner,
                out float restoredCooldown,
                out string companionFailure)
            && restoredOwner.IsValid
            && restoredOwner.Value == "character:staff:1:01"
            && restoredCooldown == 12.5f
            && companionFailure == string.Empty,
            "Companion owner/cooldown round trip was not exact.");

        CapturedWildlifeCapabilityState clone = state.Clone();
        clone.payloadJson = "{\"ownerCharacterId\":\"character:staff:1:01\",\"nextAttackAt\":99}";
        Require(!ReferenceEquals(state, clone)
            && state.payloadJson
                == "{\"ownerCharacterId\":\"character:staff:1:01\",\"nextAttackAt\":12.5}"
            && clone.payloadJson
                == "{\"ownerCharacterId\":\"character:staff:1:01\",\"nextAttackAt\":99}",
            "Companion capability clone mutation changed the source envelope.");
    }

    private static void VerifyNullStateRejected() =>
        RequireCapabilityRejected(null, "Null capability state was accepted.");

    private static void VerifyUnknownEnvelopeVersionRejected() =>
        RequireCapabilityRejected(new CapturedWildlifeCapabilityState
        {
            version = 99,
            roleId = "wildlife-role:none",
            payloadVersion = 0,
            payloadJson = string.Empty
        }, "Envelope version 99 was accepted.");

    private static void VerifyUnknownRoleRejected() =>
        RequireCapabilityRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:unknown",
            payloadVersion = 0,
            payloadJson = string.Empty
        }, "Unknown role was accepted.");

    private static void VerifySpacedRoleRejectedWithoutNormalizing()
    {
        CapturedWildlifeRoleId typedRole =
            new(" wildlife-role:companion ");
        Require(!typedRole.IsValid
            && typedRole.Value == " wildlife-role:companion ",
            "Whitespace role was silently normalized or marked valid.");
        RequireCapabilityRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = " wildlife-role:companion ",
            payloadVersion = 1,
            payloadJson = "{\"ownerCharacterId\":\"character:staff:1:01\",\"nextAttackAt\":12.5}"
        }, "Whitespace role envelope was accepted.");
    }

    private static void VerifyNonePayloadRejected() =>
        RequireCapabilityRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:none",
            payloadVersion = 0,
            payloadJson = "{}"
        }, "None role with a payload was accepted.");

    private static void VerifyUnknownCompanionPayloadVersionRejected() =>
        RequireCompanionRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:companion",
            payloadVersion = 99,
            payloadJson = "{\"ownerCharacterId\":\"character:staff:1:01\",\"nextAttackAt\":12.5}"
        }, "Companion payload version 99 was accepted.");

    private static void VerifyMissingCompanionOwnerRejected() =>
        RequireCompanionRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:companion",
            payloadVersion = 1,
            payloadJson = "{\"nextAttackAt\":12.5}"
        }, "Companion payload missing its owner was accepted.");

    private static void VerifyMissingCompanionCooldownRejected() =>
        RequireCompanionRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:companion",
            payloadVersion = 1,
            payloadJson = "{\"ownerCharacterId\":\"character:staff:1:01\"}"
        }, "Companion payload missing its cooldown was accepted.");

    private static void VerifyMalformedCompanionJsonRejected() =>
        RequireCompanionRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:companion",
            payloadVersion = 1,
            payloadJson = "{not-json}"
        }, "Malformed companion JSON was accepted.");

    private static void VerifyNegativeCompanionCooldownRejected() =>
        RequireCompanionRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:companion",
            payloadVersion = 1,
            payloadJson = "{\"ownerCharacterId\":\"character:staff:1:01\",\"nextAttackAt\":-1}"
        }, "Negative companion cooldown was accepted.");

    private static void VerifyReservedHaulRejected() =>
        RequireCapabilityRejected(new CapturedWildlifeCapabilityState
        {
            version = 1,
            roleId = "wildlife-role:haul",
            payloadVersion = 0,
            payloadJson = string.Empty
        }, "Reserved haul role was accepted before implementation.");

    private static void RequireProfileRejected(
        int closeRadiusCells,
        int resumeFollowDistanceCells,
        int ownerTargetLeashCells,
        float decisionIntervalSeconds,
        float baseAttackDamage,
        int meleeRangeCells,
        float attackCooldownSeconds,
        int maximumCompanionsPerOwner,
        string message)
    {
        bool rejected;
        try
        {
            WildlifeCompanionRoleProfile profile = new(
                closeRadiusCells,
                resumeFollowDistanceCells,
                ownerTargetLeashCells,
                decisionIntervalSeconds,
                baseAttackDamage,
                meleeRangeCells,
                attackCooldownSeconds,
                maximumCompanionsPerOwner);
            rejected = profile.Validate().Count > 0;
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, message);
    }

    private static void RequireCapabilityRejected(
        CapturedWildlifeCapabilityState state,
        string message)
    {
        bool accepted = CapturedWildlifeCapabilityStateCodec.TryRead(
            state,
            out _,
            out string failureReason);
        Require(!accepted && !string.IsNullOrWhiteSpace(failureReason), message);
    }

    private static void RequireCompanionRejected(
        CapturedWildlifeCapabilityState state,
        string message)
    {
        RequireCapabilityRejected(state, message);
        bool accepted = CapturedWildlifeCapabilityStateCodec.TryReadCompanion(
            state,
            out _,
            out _,
            out string failureReason);
        Require(!accepted && !string.IsNullOrWhiteSpace(failureReason), message);
    }

    private static void Run(
        ICollection<string> rows,
        ref int passed,
        string name,
        Action verification)
    {
        try
        {
            verification();
            rows.Add("PASS " + name);
            passed++;
        }
        catch (Exception exception)
        {
            rows.Add("FAIL " + name + ": " + exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
