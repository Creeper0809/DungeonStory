using System;
using UnityEngine;

public readonly struct ProductionOutputFactor : IEquatable<ProductionOutputFactor>
{
    public ProductionOutputFactor(long numerator, long denominator)
    {
        if (numerator <= 0)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator));
        long divisor = GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public long Numerator { get; }
    public long Denominator { get; }
    public static ProductionOutputFactor One => new(1L, 1L);

    public ProductionOutputFactor Multiply(ProductionOutputFactor other)
    {
        RequireInitialized(this);
        RequireInitialized(other);
        long leftDivisor = GreatestCommonDivisor(Numerator, other.Denominator);
        long rightDivisor = GreatestCommonDivisor(other.Numerator, Denominator);
        return new ProductionOutputFactor(
            checked((Numerator / leftDivisor)
                * (other.Numerator / rightDivisor)),
            checked((Denominator / rightDivisor)
                * (other.Denominator / leftDivisor)));
    }

    public decimal Scale(int authoredQuantity)
    {
        RequireInitialized(this);
        if (authoredQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(authoredQuantity));
        return checked(
            authoredQuantity * (decimal)Numerator / Denominator);
    }

    public int CeilQuantity(int authoredQuantity)
    {
        decimal scaled = Scale(authoredQuantity);
        if (scaled > int.MaxValue)
            throw new OverflowException("Scaled production quantity exceeds Int32.");
        return checked((int)decimal.Ceiling(scaled));
    }

    public float ToFloat() => checked((float)Numerator / Denominator);

    public static ProductionOutputFactor FromAuthoredMultiplier(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(nameof(value));
        decimal scaled = decimal.Round(
            (decimal)value * 1000m,
            0,
            MidpointRounding.AwayFromZero);
        if (scaled <= 0m || scaled > long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
        decimal reconstructed = scaled / 1000m;
        if (Math.Abs((decimal)value - reconstructed) > 0.00001m)
        {
            throw new InvalidOperationException(
                $"Output multiplier '{value}' is not canonical to one permille.");
        }
        return new ProductionOutputFactor((long)scaled, 1000L);
    }

    public bool Equals(ProductionOutputFactor other) =>
        Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object obj) =>
        obj is ProductionOutputFactor other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public override string ToString() => $"{Numerator}/{Denominator}";

    private static void RequireInitialized(ProductionOutputFactor value)
    {
        if (value.Numerator <= 0 || value.Denominator <= 0)
            throw new InvalidOperationException("Production output factor is uninitialized.");
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0)
        {
            long remainder = left % right;
            left = right;
            right = remainder;
        }
        return left;
    }
}

public static class ProductionOutputFactorAuthority
{
    public static ProductionOutputFactor ResolveCurrent(
        IGrandProjectBenefitQuery benefits,
        string facilityTag)
    {
        if (benefits == null)
            throw new ArgumentNullException(nameof(benefits));
        return ProductionOutputFactor.FromAuthoredMultiplier(
            benefits.GetProductionOutputMultiplier(facilityTag));
    }

    public static ProductionOutputFactor ResolveMaximumGrandProject(
        string facilityTag) => facilityTag switch
        {
            "quarry" => new ProductionOutputFactor(5L, 4L),
            "crop-indoor" => new ProductionOutputFactor(6L, 5L),
            "alchemy" or "apothecary" or "distillery" =>
                new ProductionOutputFactor(23L, 20L),
            _ => ProductionOutputFactor.One
        };
}
