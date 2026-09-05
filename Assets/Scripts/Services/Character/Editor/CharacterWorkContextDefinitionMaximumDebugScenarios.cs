#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class CharacterWorkContextDefinitionMaximumDebugScenarios
{
    [MenuItem("DungeonStory/V27/Character/Validate Work Context Definition Maximum")]
    public static void Validate()
    {
        VerifyOrdinaryAndResearchMaximums();
        VerifySharedRuntimeBoundaries();
        VerifyInvalidDefinitionsFailLoud();
        Debug.Log(
            "[CharacterWorkContextDefinitionMaximum] focused scenarios passed.");
    }

    private static void VerifyOrdinaryAndResearchMaximums()
    {
        GameplayEffectDefinitionSO research = Definition(
            "effect:qa:research-speed",
            GameplayEffectTargetIds.ResearchSpeed,
            0f,
            10f);
        try
        {
            CharacterWorkContextDefinitionMaximumQuery query = new(
                new GameplayEffectResultBoundsCatalog(new[] { research }));
            CharacterWorkContextDefinitionMaximumSnapshot craft = query.Capture(
                BuiltInWorkTypeIds.Craft);
            CharacterWorkContextDefinitionMaximumSnapshot researchWork = query
                .Capture(BuiltInWorkTypeIds.Research);
            Require(Exactly(craft.ResearchSharedMaximum, 1d)
                    && Exactly(craft.TransientSkillMaximum, 2.5d)
                    && Exactly(craft.SubstanceMaximum, 1.75d)
                    && Exactly(craft.MaximumMultiplier, 4.375d),
                "The ordinary work-context maximum is not 2.5 x 1.75.");
            Require(Exactly(
                        researchWork.ResearchSharedMaximum,
                        CharacterIncrementalGameplayEffectAuthority
                            .ResolveAbsoluteMaximum(10d))
                    && Exactly(
                        researchWork.MaximumMultiplier,
                        researchWork.ResearchSharedMaximum * 4.375d),
                "The research incremental quotient was not conservatively bounded.");
            RequireThrows<InvalidOperationException>(
                () => query.Capture(new WorkTypeId("work:qa-unknown")),
                "An unknown work type received an implicit context maximum.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(research);
        }
    }

    private static void VerifySharedRuntimeBoundaries()
    {
        Require(Exactly(
                    CharacterSkillWorkSpeedAuthority.ResolveFromAuthoredBonus(2f),
                    CharacterSkillWorkSpeedAuthority.MaximumRuntimeMultiplier)
                && Exactly(
                    CharacterEquipmentBurdenWorkSpeedAuthority.Resolve(
                        0f,
                        1f,
                        1f),
                    1f)
                && Exactly(
                    CharacterSubstanceEffectMultiplierAuthority.Resolve(
                        10d,
                        0d),
                    CharacterSubstanceEffectMultiplierAuthority
                        .MaximumMultiplier),
            "A shared live work-context boundary drifted from its maximum.");
        RequireThrows<InvalidOperationException>(
            () => CharacterSkillWorkSpeedAuthority.RequireRuntimeMultiplier(
                2.5001f),
            "An over-maximum transient skill multiplier was accepted.");
        RequireThrows<InvalidOperationException>(
            () => CharacterEquipmentBurdenWorkSpeedAuthority.Resolve(
                0f,
                1f,
                0f),
            "A zero haul-capacity denominator was accepted.");
    }

    private static void VerifyInvalidDefinitionsFailLoud()
    {
        GameplayEffectDefinitionSO invalid = Definition(
            "effect:qa:research-speed-invalid",
            GameplayEffectTargetIds.ResearchSpeed,
            float.NegativeInfinity,
            10f);
        try
        {
            RequireThrows<InvalidOperationException>(
                () => new GameplayEffectResultBoundsCatalog(
                    new[] { invalid }),
                "A non-finite gameplay-effect result bound was accepted.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(invalid);
        }
    }

    private static GameplayEffectDefinitionSO Definition(
        string effectId,
        string targetId,
        float minimum,
        float maximum)
    {
        GameplayEffectDefinitionSO definition = ScriptableObject
            .CreateInstance<GameplayEffectDefinitionSO>();
        definition.Configure(
            910001,
            effectId,
            targetId,
            GameplayEffectOperation.Multiply,
            GameplayEffectProjectionPhase.Multiplicative,
            GameplayEffectSourceKind.All,
            GameplayEffectStackingPolicy.StackAll,
            minimum,
            maximum);
        return definition;
    }

    private static bool Exactly(double left, double right) => left.Equals(right);

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
