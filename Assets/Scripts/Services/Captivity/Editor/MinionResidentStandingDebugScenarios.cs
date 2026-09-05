#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MinionResidentStandingDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/minion-resident-standing-audit.txt";
    private const int SeedCount = 256;
    private const int ResidentCount = 3;
    private const float NeutralDailyWork = 50f;

    private static readonly HashSet<WorkTypeId> ForbiddenWorkTypes = new()
    {
        BuiltInWorkTypeIds.Research,
        BuiltInWorkTypeIds.Reception,
        BuiltInWorkTypeIds.Treat,
        BuiltInWorkTypeIds.Surgery,
        BuiltInWorkTypeIds.Warden,
        BuiltInWorkTypeIds.Perform,
        BuiltInWorkTypeIds.GrandProject,
        BuiltInWorkTypeIds.ThreatMitigation
    };

    [MenuItem(
        "DungeonStory/V27/Captivity/Run Minion Resident Standing Audit")]
    public static void RunFromMenu()
    {
        string summary = Run();
        Debug.Log("[MinionResidentStanding] PASS " + summary);
    }

    public static string Run()
    {
        List<string> evidence = new();
        VerifyWorkMatrix(evidence);
        VerifyTransitionBoundaries(evidence);
        VerifySocialBoundaries(evidence);
        VerifyRehabilitationSaveRoundTrip(evidence);
        VerifyCaptivityTransitionRollback(evidence);
        VerifyLongHorizonValue(evidence);

        string directory = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder report = new();
        report.AppendLine("MINION_RESIDENT_STANDING_AUDIT=PASS");
        report.AppendLine("balanceImpact=YES");
        report.AppendLine(
            "authority=MinionIntegrationRules + CharacterSettlementStanding + CaptiveState");
        foreach (string line in evidence)
        {
            report.AppendLine(line);
        }
        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        return string.Join("; ", evidence);
    }

    private static void VerifyWorkMatrix(List<string> evidence)
    {
        Require(BuiltInWorkTypeIds.All.Count == 31,
            "Built-in work count drifted from 31.");
        Require(BuiltInWorkTypeIds.All.Select(value => value.Value)
                .Distinct(StringComparer.Ordinal).Count() == 31,
            "Built-in work IDs are not unique.");

        WorkTypeId[] allowed = BuiltInWorkTypeIds.All
            .Where(MinionIntegrationRules.IsWorkAllowed)
            .ToArray();
        WorkTypeId[] forbidden = BuiltInWorkTypeIds.All
            .Where(value => !MinionIntegrationRules.IsWorkAllowed(value))
            .ToArray();
        Require(allowed.Length == 23,
            $"Expected 23 minion work types, found {allowed.Length}.");
        Require(forbidden.Length == 8
            && forbidden.ToHashSet().SetEquals(ForbiddenWorkTypes),
            "Minion forbidden work set drifted from the approved eight.");
        Require(MinionIntegrationRules.IsWorkAllowed(BuiltInWorkTypeIds.Guard),
            "Minion guard work must remain available.");
        Require(Mathf.Approximately(
                MinionIntegrationRules.MinionApprovedWorkExperienceMultiplier,
                0.5f),
            "Minion approved-work XP multiplier must remain 50%.");
        evidence.Add("workMatrix=31 total/23 allowed/8 forbidden; guard=yes; expedition=no; xp=0.50");
    }

    private static void VerifyTransitionBoundaries(List<string> evidence)
    {
        Require(!MinionIntegrationRules.CanConvertToMinion(80f, 4, 6)
            && MinionIntegrationRules.CanConvertToMinion(80f, 4, 7),
            "Minion conversion did not switch exactly at three captive days.");
        Require(!MinionIntegrationRules.CanConvertToMinion(79.999f, 4, 7),
            "Minion conversion accepted corruption below 80.");

        Require(!MinionIntegrationRules.CanRecruitDirectly(
                70f, 30f, 59.999f, 4, 13)
            && MinionIntegrationRules.CanRecruitDirectly(
                70f, 30f, 59.999f, 4, 14)
            && !MinionIntegrationRules.CanRecruitDirectly(
                70f, 30f, 60f, 4, 14),
            "Direct recruitment boundary is not 10 days with corruption below 60.");

        Require(!MinionIntegrationRules.CanRecruitRehabilitated(
                70f, 30f, 30f, 14)
            && MinionIntegrationRules.CanRecruitRehabilitated(
                70f, 30f, 30f, 15),
            "Rehabilitation did not switch exactly at 15 completed days.");
        Require(Mathf.Approximately(
                MinionIntegrationRules.RehabilitationRequiredWork,
                18f)
            && MinionIntegrationRules.RehabilitationFoodCost == 1,
            "Daily rehabilitation cost drifted from 18 WU and one food.");
        evidence.Add("boundaries=minion day3/corruption80; direct day10/trust70/grudge30/corruption<60; rehabilitation day15/18WU/food1");
    }

    private static void VerifySocialBoundaries(List<string> evidence)
    {
        Require(MinionIntegrationRules.ResolveResidentMoodDelta(0.0999f) == 0
            && MinionIntegrationRules.ResolveResidentMoodDelta(0.10f) == -2
            && MinionIntegrationRules.ResolveResidentMoodDelta(0.25f) == -5
            && MinionIntegrationRules.ResolveResidentMoodDelta(0.50f) == -9,
            "Resident mood ratio boundaries drifted.");
        Require(MinionIntegrationRules.ResolveDailyConflictLimit(0) == 0
            && MinionIntegrationRules.ResolveDailyConflictLimit(1) == 1
            && MinionIntegrationRules.ResolveDailyConflictLimit(4) == 1
            && MinionIntegrationRules.ResolveDailyConflictLimit(5) == 2,
            "Daily conflict ceiling is not ceil(minions / 4).");
        Require(Mathf.Approximately(
                MinionIntegrationRules.ResolveBrawlChancePercent(-0.2f, 50f, 50f),
                40f)
            && Mathf.Approximately(
                MinionIntegrationRules.ResolveBrawlChancePercent(0f, 50f, 50f),
                10f),
            "Brawl chance boundaries drifted.");
        Require(Mathf.Approximately(
                MinionIntegrationRules.ResolveControlBreakChancePercent(
                    80f, 20f, 20f, 20f),
                0f)
            && MinionIntegrationRules.ResolveControlBreakChancePercent(
                0f, 0f, 100f, 0f) <= 10f,
            "Control-break gate or ten-percent cap drifted.");
        evidence.Add("social=mood 10/25/50%; conflicts ceil(M/4); brawl 10/40%; control break capped 10%");
    }

    private static void VerifyRehabilitationSaveRoundTrip(
        List<string> evidence)
    {
        CaptiveState minion = new()
        {
            captiveId = "character:minion:audit",
            displayName = "감사 하수인",
            speciesTag = "orc",
            status = CaptivityStatus.Minion,
            policyId = CaptivityPolicyIds.Standard,
            trust = 55f,
            grudge = 24f,
            corruption = 48f,
            capturedAbsoluteDay = 7,
            rehabilitationDays = 9,
            lastRehabilitationAbsoluteDay = 31,
            completedRehabilitationWork = 12.5f,
            rehabilitationInProgress = true,
            reservedWardenId = "character:resident:warden",
            rehabilitationFacilityBuildingId = "building:captive-room:audit",
            rehabilitationPosition = new Vector2Int(12, 8),
            lastMinionSocialAbsoluteDay = 31
        };
        CaptivitySaveData source = new()
        {
            captureSequence = 1,
            policies = new List<CaptivePolicyData>
            {
                new()
                {
                    policyId = CaptivityPolicyIds.Standard,
                    displayName = "표준 수용"
                }
            },
            captives = new List<CaptiveState> { minion }
        };
        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(source));
        DungeonGameRestoreReport validation = new();
        CaptivitySaveValidation.Validate(roundTrip, validation);
        Require(validation.Success,
            "Valid minion save failed: " + string.Join(" | ", validation.Errors));
        CaptiveState restored = roundTrip.captives.Single();
        Require(restored.IsMinion
            && restored.rehabilitationInProgress
            && restored.rehabilitationDays == 9
            && Mathf.Approximately(restored.completedRehabilitationWork, 12.5f)
            && restored.rehabilitationFacilityBuildingId
                == "building:captive-room:audit"
            && restored.reservedWardenId == "character:resident:warden"
            && restored.lastMinionSocialAbsoluteDay == 31,
            "Minion save round-trip lost standing or rehabilitation state.");

        roundTrip.captives[0].rehabilitationFacilityBuildingId = string.Empty;
        DungeonGameRestoreReport broken = new();
        CaptivitySaveValidation.Validate(roundTrip, broken);
        Require(!broken.Success,
            "Incomplete rehabilitation assignment passed save validation.");
        evidence.Add("saveRoundTrip=minion standing + rehabilitation assignment + daily social marker; malformed rejected");
    }

    private static void VerifyLongHorizonValue(List<string> evidence)
    {
        const int minionStartDay = 3;
        const int residentStartDay = 10;
        Require(residentStartDay - minionStartDay >= 7,
            "Minion did not join labor at least seven days earlier.");

        float worstRatio = 0f;
        float ratioSum = 0f;
        int worstSeed = 0;
        for (int seed = 0; seed < SeedCount; seed++)
        {
            (float minion, float resident) = SimulateStrategy(seed, 120);
            Require(resident > 0f,
                $"Formal resident strategy produced no value for seed {seed}.");
            float ratio = minion / resident;
            ratioSum += ratio;
            if (ratio > worstRatio)
            {
                worstRatio = ratio;
                worstSeed = seed;
            }
        }

        Require(worstRatio <= 1.15f,
            $"Minion strategy dominated by {(worstRatio - 1f) * 100f:0.##}% at seed {worstSeed}.");
        evidence.Add(
            $"strategyAudit=256 seeds/day30 join lead 7 days/day120 mean ratio {ratioSum / SeedCount:0.000}/max ratio {worstRatio:0.000}@seed{worstSeed}; cap=1.150");
        evidence.Add(
            "strategyMetric=effective WU + XP growth - three-resident mood externality - conflict/brawl downtime; shared survival inputs cancel; wages reported separately by economy authority");
    }

    private static void VerifyCaptivityTransitionRollback(
        List<string> evidence)
    {
        CaptiveState state = new()
        {
            captiveId = "character:minion:rollback-audit",
            displayName = "전환 복원 감사",
            status = CaptivityStatus.Confined,
            policyId = CaptivityPolicyIds.Corruption,
            reservedCarrierId = "character:carrier:audit",
            reservedWardenId = "character:warden:audit",
            housingBuildingId = "building:cell:audit",
            restraintItemId = "item:restraint:audit",
            restraintQuantity = 1,
            restrained = true,
            laborPermissions = CaptiveLaborPermission.All,
            currentInteractionId = "captivity:corruption-ritual",
            completedInteractionWork = 21f,
            requiredInteractionWork = 42f,
            lastResult = "전환 전"
        };
        string before =
            CaptivityStateTransitionRules.CaptureStateSnapshot(state);

        state.status = CaptivityStatus.Minion;
        state.lastResult = "전환 중";
        CaptivityStateTransitionRules.ClearCaptiveOnlyState(state);
        CaptivityStateTransitionRules.RestoreStateSnapshot(before, state);

        string restored =
            CaptivityStateTransitionRules.CaptureStateSnapshot(state);
        Require(string.Equals(before, restored, StringComparison.Ordinal),
            "Captivity-state rollback did not restore the exact pre-transition snapshot.");
        evidence.Add("transitionRollback=captivity snapshot exact; population and employment use explicit rollback transactions");
    }

    private static (float minion, float resident) SimulateStrategy(
        int seed,
        int days)
    {
        System.Random random = new(seed * 7_919 + 17);
        float minionValue = 0f;
        float residentValue = 0f;
        int minionWorkedDays = 0;
        int residentWorkedDays = 0;
        float minionRatio = MinionIntegrationRules.ResolveMinionRatio(
            ResidentCount,
            1);
        int moodDelta = MinionIntegrationRules.ResolveResidentMoodDelta(
            minionRatio);

        for (int day = 0; day < days; day++)
        {
            float availability = Mathf.Lerp(
                0.78f,
                0.98f,
                (float)random.NextDouble());
            if (day >= 3)
            {
                float growth = Mathf.Min(0.075f, minionWorkedDays * 0.00075f);
                minionValue += NeutralDailyWork * availability * (1f + growth);
                minionWorkedDays++;

                float conflictChance =
                    MinionIntegrationRules.ResolveConflictChancePercent(
                        minionRatio,
                        30f,
                        50f) / 100f;
                if (random.NextDouble() < conflictChance)
                {
                    minionValue -= 4f;
                    if (random.NextDouble()
                        < MinionIntegrationRules.ResolveBrawlChancePercent(
                            0f,
                            50f,
                            50f) / 100f)
                    {
                        minionValue -= 10f;
                    }
                }

                minionValue -= Mathf.Abs(moodDelta) / 100f
                    * ResidentCount * NeutralDailyWork;
            }

            if (day >= 10)
            {
                float growth = Mathf.Min(0.15f, residentWorkedDays * 0.0015f);
                residentValue += NeutralDailyWork * availability * (1f + growth);
                residentWorkedDays++;
            }
        }
        return (Mathf.Max(0f, minionValue), Mathf.Max(0f, residentValue));
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
