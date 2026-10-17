#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WimCraftQualityEstimateDebugScenarios
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-056-quality-estimate.txt";

    [MenuItem("DungeonStory/QA/WIM/056 Quality Estimate Focused")]
    public static void Run()
    {
        DeterministicCraftQualityResolver resolver = new();
        string initialRoll = JsonUtility.ToJson(resolver.Roll(157181, "wim:056", "quality:fixture", 7));
        double good = resolver.EstimateSuccessProbability(CraftsmanshipQualityTier.Good, 50, 0, 0, 0);
        Require(good == 3171d / 9261d, "Known three-draw distribution fixture differs.");
        Require(resolver.EstimateSuccessProbability(CraftsmanshipQualityTier.Awful, 50, 0, 0, 0) == 1d,
            "Awful target must be certain.");
        Require(resolver.EstimateSuccessProbability(CraftsmanshipQualityTier.Legendary, 0, 0, 0, 0) == 0d,
            "Unreachable target must be zero probability.");
        Require(resolver.EstimateSuccessProbability(CraftsmanshipQualityTier.Mythic, 100, 100, 0, 0) == 0d,
            "Normal score resolver cannot manufacture mythic inspiration.");

        int comparisons = 0;
        foreach (float skill in new[] { 0f, 50f, 100f })
        foreach (CraftsmanshipQualityTier tier in Enum.GetValues(typeof(CraftsmanshipQualityTier)))
        {
            int successes = 0;
            for (int a = -10; a <= 10; a++)
            for (int b = -10; b <= 10; b++)
            for (int c = -10; c <= 10; c++)
            {
                CraftQualityRollSaveData roll = new() { randomA = a, randomB = b, randomC = c };
                if (resolver.Resolve(roll, skill, 3.5f, 2f, 7f).Tier >= tier) successes++;
            }
            Require(resolver.EstimateSuccessProbability(tier, skill, 3.5f, 2f, 7f)
                == successes / 9261d, "Probability diverged from production resolver.");
            comparisons++;
        }
        CraftQualityAttemptEstimate atTwenty = Estimate(.05, 10);
        Require(atTwenty.ExpectedAttempts == 20d && !atTwenty.NeedsLowProbabilityWarning, "20-attempt boundary.");
        Require(Estimate(.049, 10).NeedsLowProbabilityWarning, "Greater-than20 warning missing.");
        CraftQualityAttemptEstimate limited = Estimate(.25, 3);
        Require(limited.ExpectedAttempts == 4d && limited.SuccessWithinLimit == .578125d
            && limited.MaximumCraftWork == 42d && limited.MaximumGrossMaterial == 6L,
            "Limited probability or gross upper cost differs.");
        CraftQualityAttemptEstimate impossible = Estimate(0, 10);
        Require(double.IsPositiveInfinity(impossible.ExpectedAttempts) && impossible.SuccessWithinLimit == 0d,
            "Impossible target presented as zero attempts.");
        Require(Estimate(1, 1).SuccessWithinLimit == 1d && Estimate(1, 0).SuccessWithinLimit == 0d,
            "Certain target/zero-attempt boundary.");
        Require(Estimate(.25, null).MaximumCraftWork == null
            && Estimate(.25, null).SuccessWithinLimit == null, "Unlimited mode invented finite maximum.");
        string text = GameplayUiPresentationText.QualityEstimate(limited);
        Require(text.Contains("첫 성공 기대 4회") && text.Contains("42 WU") && text.Contains("재료 6개")
            && text.Contains("해체·운반 별도") && text.Contains("조건 고정"), "Player text omits estimate scope.");
        Require(GameplayUiPresentationText.QualityEstimate(impossible).Contains("도달 불가"), "Zero-p text missing.");
        Require(GameplayUiPresentationText.QualityEstimate(
            CraftQualityAttemptEstimate.Unavailable("작업자 미정")).Contains("작업자 미정"), "Unknown worker became p=0.");
        for (int i = 0; i < 100; i++)
            resolver.EstimateSuccessProbability(CraftsmanshipQualityTier.Good, 50, 0, 0, 0);
        Require(initialRoll == JsonUtility.ToJson(resolver.Roll(157181, "wim:056", "quality:fixture", 7)),
            "Preview changed deterministic production roll.");
        bool invalidRejected = false;
        try { Estimate(double.NaN, 1); } catch (ArgumentOutOfRangeException) { invalidRejected = true; }
        Require(invalidRejected, "NaN probability was accepted.");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "PASS\nresolver-distributions=" + comparisons
            + "\nknown-distribution=3171/9261\np-zero-one-and-20-attempt-boundary=PASS"
            + "\nlimited-success-and-gross-cost=PASS\nplayer-text=PASS\npreview-roll-invariant=PASS"
            + "\nlive-apparel-order-and-render=NOT_RUN\ncombat-construction-query-ui=NOT_CONNECTED\n",
            new System.Text.UTF8Encoding(false));
        Debug.Log("WIM056 quality estimate focused PASS: " + ReportPath);
    }

    private static CraftQualityAttemptEstimate Estimate(double p, int? limit) =>
        CraftQualityAttemptEstimate.Create(p, limit, 14, "material:cloth", 2, "작업자 조건 고정 추정");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
