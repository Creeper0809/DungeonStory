using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Wim003ApparelConditionRulesDebugScenarios
{
    private const float Epsilon = 0.00001f;

    [MenuItem("DungeonStory/QA/WIM/003 Apparel Condition Rules")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("[PASS] WIM003_APPAREL_CONDITION_RULES");
    }

    public static void RunAll()
    {
        VerifyDailyConditionTable();
        VerifyCoverageRules();
        VerifyAutomaticSelectionThresholds();
    }

    private static void VerifyDailyConditionTable()
    {
        AssertStep("underwear idle hygienic",
            Step(), 99.5f, 0f, 8f);
        AssertStep("underwear idle unhygienic",
            Step(hygiene: 0f), 99.5f, 0f, 16f);
        AssertStep("outer exterior work",
            Step(layer: ApparelLayer.Outer, working: true, exterior: true),
            97.5f, 0f, 10f);
        AssertStep("outer rain ingress",
            Step(
                layer: ApparelLayer.Outer,
                exterior: true,
                rainIngress: true,
                waterResistance: 0.5f),
            99.5f, 100f, 6f);
        AssertStep("covered underwear rain drying",
            Step(
                exterior: true,
                rainIngress: true,
                exposedFraction: 0f,
                moisture: 80f),
            99.5f, 60f, 8f);
        AssertStep("natural drying half rate",
            Step(moisture: 100f, dryingRate: 0.5f),
            99.5f, 60f, 8f);
        AssertStep("natural drying capped",
            Step(moisture: 100f, dryingRate: 2f),
            99.5f, 0f, 8f);
        AssertStep("lower durability material",
            Step(nominalMaterialDurability: 30f),
            99f, 0f, 8f);
    }

    private static void VerifyCoverageRules()
    {
        AnatomyAttachmentPoint headAndFace = AnatomyAttachmentPoint.Head
            | AnatomyAttachmentPoint.Face;

        AssertClose("underwear partial outer cover", 0.5f,
            ApparelConditionRules.ResolveExposedFraction(
                ApparelLayer.Underwear,
                headAndFace,
                new List<ApparelConditionCoverage>
                {
                    new(ApparelLayer.Outer, AnatomyAttachmentPoint.Head)
                }));
        AssertClose("underwear complete outer cover", 0f,
            ApparelConditionRules.ResolveExposedFraction(
                ApparelLayer.Underwear,
                headAndFace,
                new List<ApparelConditionCoverage>
                {
                    new(ApparelLayer.Outer, headAndFace)
                }));
        AssertClose("accessory does not shield", 1f,
            ApparelConditionRules.ResolveExposedFraction(
                ApparelLayer.Underwear,
                headAndFace,
                new List<ApparelConditionCoverage>
                {
                    new(ApparelLayer.Accessory, headAndFace)
                }));
        AssertClose("same layer does not shield", 1f,
            ApparelConditionRules.ResolveExposedFraction(
                ApparelLayer.Underwear,
                headAndFace,
                new List<ApparelConditionCoverage>
                {
                    new(ApparelLayer.Underwear, headAndFace)
                }));
    }

    private static void VerifyAutomaticSelectionThresholds()
    {
        Require(ApparelConditionRules.ShouldRetainCurrent(40f, 59.999f, 59.999f),
            "Current apparel must remain below every automatic replacement threshold.");
        Require(!ApparelConditionRules.ShouldRetainCurrent(39.999f, 0f, 0f),
            "Durability below 40 must not retain current apparel.");
        Require(!ApparelConditionRules.ShouldRetainCurrent(100f, 60f, 0f),
            "Moisture at 60 must not retain current apparel.");
        Require(!ApparelConditionRules.ShouldRetainCurrent(100f, 0f, 60f),
            "Contamination at 60 must not retain current apparel.");

        Require(ApparelConditionRules.IsReplacementEligible(70f, 19.999f, 20f),
            "Replacement apparel must accept the authored inclusive contamination limit.");
        Require(!ApparelConditionRules.IsReplacementEligible(69.999f, 0f, 0f),
            "Durability below 70 must not be a replacement candidate.");
        Require(!ApparelConditionRules.IsReplacementEligible(100f, 20f, 0f),
            "Moisture at 20 must not be a replacement candidate.");
        Require(!ApparelConditionRules.IsReplacementEligible(100f, 0f, 20.001f),
            "Contamination above 20 must not be a replacement candidate.");
    }

    private static ApparelConditionStepResult Step(
        ApparelLayer layer = ApparelLayer.Underwear,
        float hygiene = 100f,
        bool working = false,
        bool exterior = false,
        bool rainIngress = false,
        float exposedFraction = 1f,
        float nominalMaterialDurability = 60f,
        float waterResistance = 0f,
        float dryingRate = 1f,
        float durability = 100f,
        float moisture = 0f,
        float contamination = 0f)
    {
        return ApparelConditionRules.Step(new ApparelConditionStepInput(
            GameCalendarRules.SecondsPerDay,
            layer,
            hygiene,
            working,
            exterior,
            rainIngress,
            exposedFraction,
            nominalMaterialDurability,
            waterResistance,
            dryingRate,
            durability,
            moisture,
            contamination));
    }

    private static void AssertStep(
        string label,
        ApparelConditionStepResult actual,
        float expectedDurability,
        float expectedMoisture,
        float expectedContamination)
    {
        AssertClose(label + " durability", expectedDurability, actual.Durability);
        AssertClose(label + " moisture", expectedMoisture, actual.Moisture);
        AssertClose(label + " contamination", expectedContamination, actual.Contamination);
    }

    private static void AssertClose(string label, float expected, float actual)
    {
        if (float.IsNaN(actual)
            || float.IsInfinity(actual)
            || Mathf.Abs(expected - actual) > Epsilon)
        {
            throw new InvalidOperationException(
                $"{label}: expected {expected:0.#####}, actual {actual:0.#####}.");
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
