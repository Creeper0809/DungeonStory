#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterTraitIdentityRuleContractDebugScenarios
{
    private readonly struct ExpectedKey
    {
        public ExpectedKey(
            int traitId,
            Type ruleType,
            string memberName,
            string value)
        {
            TraitId = traitId;
            RuleType = ruleType;
            MemberName = memberName;
            Value = value;
        }

        public int TraitId { get; }
        public Type RuleType { get; }
        public string MemberName { get; }
        public string Value { get; }
    }

    private static readonly ExpectedKey[] ExpectedKeys =
    {
        new(
            CharacterTraitIdentityRuleContract.RitualFastingTraitId,
            typeof(BehaviorUtilityRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.RitualFastingBehaviorRuleId),
        new(
            CharacterTraitIdentityRuleContract.RitualFastingTraitId,
            typeof(PersistentNeedRule),
            nameof(PersistentNeedRule.needId),
            CharacterTraitIdentityRuleContract.RitualFastingNeedId),
        new(
            CharacterTraitIdentityRuleContract.MythicInspirationTraitId,
            typeof(ExtremeCraftInspirationRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.MythicInspirationRuleId),
        new(
            CharacterTraitIdentityRuleContract.LastStandTraitId,
            typeof(LastStandRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.LastStandRuleId),
        new(
            CharacterTraitIdentityRuleContract.ForbiddenLeapTraitId,
            typeof(ForbiddenResearchLeapRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.ForbiddenLeapRuleId),
        new(
            CharacterTraitIdentityRuleContract.MiracleSurgeryTraitId,
            typeof(MiracleSurgeryRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.MiracleSurgeryRuleId),
        new(
            CharacterTraitIdentityRuleContract.GoldenHarvestTraitId,
            typeof(GoldenHarvestRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.GoldenHarvestRuleId),
        new(
            CharacterTraitIdentityRuleContract.ProductionLimitBreakTraitId,
            typeof(ProductionLimitBreakRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.ProductionLimitBreakRuleId),
        new(
            CharacterTraitIdentityRuleContract.ArcaneOverchargeTraitId,
            typeof(ArcaneOverchargeRule),
            nameof(CharacterIdentityRule.ruleId),
            CharacterTraitIdentityRuleContract.ArcaneOverchargeRuleId)
    };

    [MenuItem("Dungeon Story/QA/R18/Character Trait Identity Rule Contract")]
    public static void Run()
    {
        VerifyNineExpectedKeys();
        VerifyValidDefinitions();
        VerifyRuleIdTypoRejected();
        VerifyNeedIdTypoRejected();
        VerifyWrongRuleTypeRejected();
        VerifyMissingRuleRejected();
        VerifyCorrectionRetry();
        Debug.Log(
            "R18_07_CHARACTER_TRAIT_IDENTITY_CONTRACT=PASS; "
            + "fixedKeys=9; valid=9; typoRejected=true; "
            + "typeRejected=true; missingRejected=true; retry=true");
    }

    private static void VerifyNineExpectedKeys()
    {
        Require(ExpectedKeys.Length == 9, "Expected nine fixed trait identity keys.");
        foreach (ExpectedKey expected in ExpectedKeys)
        {
            Require(
                CharacterTraitIdentityRuleContract.TryGetExpectedKey(
                    expected.TraitId,
                    expected.RuleType,
                    expected.MemberName,
                    out string actual)
                && string.Equals(actual, expected.Value, StringComparison.Ordinal),
                $"Fixed key lookup failed for trait {expected.TraitId} "
                + $"{expected.RuleType.Name}.{expected.MemberName}.");
        }

        Require(
            !CharacterTraitIdentityRuleContract.TryGetExpectedKey(
                214,
                typeof(BehaviorUtilityRule),
                nameof(CharacterIdentityRule.ruleId),
                out _),
            "A general identity rule was incorrectly treated as fixed.");
    }

    private static void VerifyValidDefinitions()
    {
        CharacterTraitSO[] traits =
        {
            CreateTrait(
                CharacterTraitIdentityRuleContract.RitualFastingTraitId,
                CreateRitualBehaviorRule(),
                CreateRitualNeedRule()),
            CreateTrait(
                CharacterTraitIdentityRuleContract.MythicInspirationTraitId,
                new ExtremeCraftInspirationRule
                {
                    ruleId = CharacterTraitIdentityRuleContract
                        .MythicInspirationRuleId
                }),
            CreateTrait(
                CharacterTraitIdentityRuleContract.LastStandTraitId,
                new LastStandRule
                {
                    ruleId = CharacterTraitIdentityRuleContract.LastStandRuleId
                }),
            CreateTrait(
                CharacterTraitIdentityRuleContract.ForbiddenLeapTraitId,
                new ForbiddenResearchLeapRule
                {
                    ruleId = CharacterTraitIdentityRuleContract.ForbiddenLeapRuleId
                }),
            CreateTrait(
                CharacterTraitIdentityRuleContract.MiracleSurgeryTraitId,
                new MiracleSurgeryRule
                {
                    ruleId = CharacterTraitIdentityRuleContract.MiracleSurgeryRuleId
                }),
            CreateTrait(
                CharacterTraitIdentityRuleContract.GoldenHarvestTraitId,
                new GoldenHarvestRule
                {
                    ruleId = CharacterTraitIdentityRuleContract.GoldenHarvestRuleId
                }),
            CreateTrait(
                CharacterTraitIdentityRuleContract.ProductionLimitBreakTraitId,
                new ProductionLimitBreakRule
                {
                    ruleId = CharacterTraitIdentityRuleContract
                        .ProductionLimitBreakRuleId
                }),
            CreateTrait(
                CharacterTraitIdentityRuleContract.ArcaneOverchargeTraitId,
                new ArcaneOverchargeRule
                {
                    ruleId = CharacterTraitIdentityRuleContract.ArcaneOverchargeRuleId
                })
        };

        try
        {
            foreach (CharacterTraitSO trait in traits)
                RequireValid(trait);
        }
        finally
        {
            foreach (CharacterTraitSO trait in traits)
                UnityEngine.Object.DestroyImmediate(trait);
        }
    }

    private static void VerifyRuleIdTypoRejected()
    {
        CharacterTraitSO trait = CreateTrait(
            CharacterTraitIdentityRuleContract.LastStandTraitId,
            new LastStandRule { ruleId = "extreme:last-stand-typo" });
        try
        {
            RequireRejected(trait, "LastStandRule.ruleId must be");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trait);
        }
    }

    private static void VerifyNeedIdTypoRejected()
    {
        PersistentNeedRule need = CreateRitualNeedRule();
        need.needId = "need:ritual-fast-typo";
        CharacterTraitSO trait = CreateTrait(
            CharacterTraitIdentityRuleContract.RitualFastingTraitId,
            CreateRitualBehaviorRule(),
            need);
        try
        {
            RequireRejected(trait, "PersistentNeedRule.needId must be");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trait);
        }
    }

    private static void VerifyWrongRuleTypeRejected()
    {
        CharacterTraitSO trait = CreateTrait(
            CharacterTraitIdentityRuleContract.LastStandTraitId,
            new BehaviorUtilityRule
            {
                ruleId = CharacterTraitIdentityRuleContract.LastStandRuleId,
                behaviorTag = "audit:last-stand",
                utilityDelta = 0.25f
            });
        try
        {
            RequireRejected(trait, "requires LastStandRule, not BehaviorUtilityRule");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trait);
        }
    }

    private static void VerifyMissingRuleRejected()
    {
        CharacterTraitSO trait = CreateTrait(
            CharacterTraitIdentityRuleContract.ArcaneOverchargeTraitId,
            new BehaviorUtilityRule
            {
                ruleId = "behavior:audit",
                behaviorTag = "audit:arcane",
                utilityDelta = 0.25f
            });
        try
        {
            RequireRejected(trait, "requires ArcaneOverchargeRule.ruleId");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trait);
        }
    }

    private static void VerifyCorrectionRetry()
    {
        LastStandRule rule = new() { ruleId = "extreme:last-stand-typo" };
        CharacterTraitSO trait = CreateTrait(
            CharacterTraitIdentityRuleContract.LastStandTraitId,
            rule);
        try
        {
            Require(trait.ValidateDefinition().Count > 0,
                "The invalid fixed key unexpectedly passed before correction.");
            rule.ruleId = CharacterTraitIdentityRuleContract.LastStandRuleId;
            RequireValid(trait);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trait);
        }
    }

    private static CharacterTraitSO CreateTrait(
        int traitId,
        params CharacterIdentityRule[] rules)
    {
        CharacterTraitSO trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
        trait.id = traitId;
        trait.traitName = $"R18 trait {traitId}";
        trait.selectionFamilyId = $"audit:r18:{traitId}";
        trait.identityRules = rules.ToList();
        return trait;
    }

    private static BehaviorUtilityRule CreateRitualBehaviorRule() => new()
    {
        ruleId = CharacterTraitIdentityRuleContract.RitualFastingBehaviorRuleId,
        behaviorTag = "ritual:fast",
        utilityDelta = 0.7f
    };

    private static PersistentNeedRule CreateRitualNeedRule() => new()
    {
        ruleId = CharacterTraitIdentityRuleContract.RitualFastingNeedId,
        needId = CharacterTraitIdentityRuleContract.RitualFastingNeedId,
        satisfiedEventId = "ritual:fast-completed",
        deprivedEventId = "ritual:fast-broken"
    };

    private static void RequireValid(CharacterTraitSO trait)
    {
        IReadOnlyList<string> errors = trait.ValidateDefinition();
        Require(errors.Count == 0,
            $"Trait {trait.id} failed validation: {string.Join(" | ", errors)}");
    }

    private static void RequireRejected(CharacterTraitSO trait, string expected)
    {
        IReadOnlyList<string> errors = trait.ValidateDefinition();
        Require(errors.Any(error => error.Contains(expected)),
            $"Trait {trait.id} did not report '{expected}': "
            + string.Join(" | ", errors));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
