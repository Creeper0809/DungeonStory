#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ProductionMassExplanationCapabilityDebugScenarios
{
    [MenuItem("DungeonStory/V27/Physical Mass/Verify Process Loss Capability")]
    public static void RunFromMenu()
    {
        Verify();
        Debug.Log("V27_PROCESS_MASS_EXPLANATION_CAPABILITIES=PASS");
    }

    public static void Verify()
    {
        string payload = ProcessLossProductionMassExplanationCapability
            .BuildPayload(
                PhysicalMassLossKind.CuttingWaste,
                "cutting-dust-or-offcut");
        ProductionMassExplanationAuthoringSnapshot authoring = new(
            ProcessLossProductionMassExplanationCapability.Id,
            ProcessLossProductionMassExplanationCapability.Version,
            payload);
        ProductionMassExplanationCapabilityRegistry registry =
            ProductionMassExplanationCapabilityRegistry.CreateDefault();
        ProductionMassExplanationDisposition first = registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a",
                3_600L,
                0L,
                0L,
                3_300L,
                0L,
                0L));
        ProductionMassExplanationDisposition repeated = registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a",
                3_600L,
                0L,
                0L,
                3_300L,
                0L,
                0L));
        Require(first.DeclaredLossGrams == 300L
            && first.LossKind == PhysicalMassLossKind.CuttingWaste
            && first.ReasonCode == "cutting-dust-or-offcut"
            && first.CanonicalReceiptPayload.Contains(
                "equation=",
                StringComparison.Ordinal)
            && string.Equals(
                first.Fingerprint,
                repeated.Fingerprint,
                StringComparison.Ordinal),
            "Process-loss canary did not resolve deterministically.");

        ProductionMassExplanationDisposition differentRecipe = registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-b",
                3_600L,
                0L,
                0L,
                3_300L,
                0L,
                0L));
        Require(differentRecipe.DeclaredLossGrams == 300L,
            "Process-loss capability depends on a recipe-specific code branch.");

        ExpectFailure(() => registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a",
                3_600L,
                0L,
                0L,
                3_601L,
                0L,
                0L)), "equation drift");
        ExpectFailure(() => registry.Resolve(
            new ProductionMassExplanationAuthoringSnapshot(
                "unknown-capability",
                1,
                payload),
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a", 3_600L, 0L, 0L, 3_300L, 0L, 0L)),
            "unknown capability");
        ExpectFailure(() => new ProductionMassExplanationCapabilityRegistry(
            new IProductionMassExplanationCapability[]
            {
                new ProcessLossProductionMassExplanationCapability(),
                new ProcessLossProductionMassExplanationCapability()
            }), "duplicate capability");
        ExpectFailure(() => registry.Resolve(
            new ProductionMassExplanationAuthoringSnapshot(
                ProcessLossProductionMassExplanationCapability.Id,
                ProcessLossProductionMassExplanationCapability.Version,
                payload.Replace("mode=residual", "mode=fixed")),
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a", 3_600L, 0L, 0L, 3_300L, 0L, 0L)),
            "noncanonical payload");

        string additionPayload =
            ProcessAdditionProductionMassExplanationCapability.BuildPayload(
                PhysicalMassExternalInputKind.AbstractProcessAddition,
                "game-unit-abstraction-addition");
        ProductionMassExplanationAuthoringSnapshot additionAuthoring = new(
            ProcessAdditionProductionMassExplanationCapability.Id,
            ProcessAdditionProductionMassExplanationCapability.Version,
            additionPayload);
        ProductionMassExplanationDisposition addition = registry.Resolve(
            additionAuthoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-addition",
                800L,
                0L,
                0L,
                1_000L,
                0L,
                0L));
        Require(addition.DeclaredExternalInputGrams == 200L
            && addition.ExternalInputKind
                == PhysicalMassExternalInputKind.AbstractProcessAddition
            && addition.DeclaredLossGrams == 0L
            && addition.LossKind == PhysicalMassLossKind.None
            && addition.ReasonCode == "game-unit-abstraction-addition",
            "Process-addition canary did not resolve the exact residual.");
        ExpectFailure(() => registry.Resolve(
            additionAuthoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-addition",
                1_000L,
                0L,
                0L,
                800L,
                0L,
                0L)),
            "negative process addition");
    }

    private static void ExpectFailure(Action action, string label)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Process-loss scenario accepted " + label + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
