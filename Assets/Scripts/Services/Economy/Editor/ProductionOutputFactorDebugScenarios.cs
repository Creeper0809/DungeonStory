#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputFactorDebugScenarios
{
    [MenuItem("DungeonStory/V27/Production/Run Output Factor Scenarios")]
    public static void RunAll()
    {
        Require(
            ProductionOutputFactor.FromAuthoredMultiplier(1.25f)
                .Equals(new ProductionOutputFactor(5, 4)),
            "1.25 did not canonicalize to 5/4.");
        Require(
            ProductionOutputFactor.FromAuthoredMultiplier(1.2f)
                .Equals(new ProductionOutputFactor(6, 5)),
            "1.20 did not canonicalize to 6/5.");
        ProductionOutputFactor alchemy =
            ProductionOutputFactor.FromAuthoredMultiplier(1.15f);
        Require(alchemy.Equals(new ProductionOutputFactor(23, 20)),
            "1.15 did not canonicalize to 23/20.");
        Require(new ProductionOutputFactor(5, 4).CeilQuantity(4) == 5,
            "4 x 5/4 maximum quantity must be 5.");
        Require(new ProductionOutputFactor(5, 4).CeilQuantity(1) == 2,
            "1 x 5/4 maximum quantity must be 2.");
        Require(alchemy.CeilQuantity(1) == 2
                && alchemy.CeilQuantity(2) == 3
                && alchemy.CeilQuantity(20) == 23,
            "23/20 ceiling boundaries drifted.");
        Require(new ProductionOutputFactor(long.MaxValue, 2)
                .Multiply(new ProductionOutputFactor(2, long.MaxValue))
                .Equals(ProductionOutputFactor.One),
            "Cross-reduction did not prevent safe multiplication overflow.");
        Expect<OverflowException>(() =>
            new ProductionOutputFactor(long.MaxValue, 1)
                .Multiply(new ProductionOutputFactor(2, 1)));
        Expect<InvalidOperationException>(() =>
            ProductionOutputFactor.FromAuthoredMultiplier(1.23456f));
        Require(ProductionOutputFactorAuthority
                .ResolveMaximumGrandProject("quarry")
                .Equals(new ProductionOutputFactor(5, 4))
                && ProductionOutputFactorAuthority
                    .ResolveMaximumGrandProject("alchemy")
                    .Equals(new ProductionOutputFactor(23, 20))
                && ProductionOutputFactorAuthority
                    .ResolveMaximumGrandProject("feedbench")
                    .Equals(ProductionOutputFactor.One),
            "Maximum Grand Project factor authority drifted.");
        Debug.Log("[ProductionOutputFactor] focused scenarios passed.");
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected exception was not thrown: " + typeof(T).Name + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
