using System;

namespace DungeonStory.FacilityEvolution
{
    public readonly struct FacilityDefinitionId : IEquatable<FacilityDefinitionId>
    {
        public FacilityDefinitionId(string value) => Value = Normalize(value);
        public string Value { get; }
        public bool IsValid => Value.Length > 0;
        public bool Equals(FacilityDefinitionId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) =>
            obj is FacilityDefinitionId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }

    public readonly struct FacilityEvolutionRecipeId : IEquatable<FacilityEvolutionRecipeId>
    {
        public FacilityEvolutionRecipeId(string value) => Value = value?.Trim() ?? string.Empty;
        public string Value { get; }
        public bool IsValid => Value.Length > 0;
        public bool Equals(FacilityEvolutionRecipeId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) =>
            obj is FacilityEvolutionRecipeId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct FacilityEvolutionItemId : IEquatable<FacilityEvolutionItemId>
    {
        public FacilityEvolutionItemId(string value) => Value = value?.Trim() ?? string.Empty;
        public string Value { get; }
        public bool IsValid => Value.Length > 0 && Value.IndexOf("stock-item:", StringComparison.Ordinal) < 0;
        public bool Equals(FacilityEvolutionItemId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) =>
            obj is FacilityEvolutionItemId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct FacilityEvolutionOrderId : IEquatable<FacilityEvolutionOrderId>
    {
        public FacilityEvolutionOrderId(string value) => Value = value ?? string.Empty;
        public string Value { get; }
        public bool IsValid => Value.Length > 0
            && string.Equals(Value, Value.Trim(), StringComparison.Ordinal);
        public bool Equals(FacilityEvolutionOrderId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) =>
            obj is FacilityEvolutionOrderId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct FacilityGridAddress : IEquatable<FacilityGridAddress>
    {
        public FacilityGridAddress(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
        public bool Equals(FacilityGridAddress other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is FacilityGridAddress other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"{X},{Y}";
    }
}
