using System;

public readonly struct WorkTypeId : IEquatable<WorkTypeId>
{
    public WorkTypeId(string value)
    {
        Value = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Work type id is required.", nameof(value))
            : value.Trim();
    }

    public string Value { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public bool Equals(WorkTypeId other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is WorkTypeId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    }

    public override string ToString()
    {
        return Value ?? string.Empty;
    }

    public static bool operator ==(WorkTypeId left, WorkTypeId right) => left.Equals(right);
    public static bool operator !=(WorkTypeId left, WorkTypeId right) => !left.Equals(right);
}

public static class BuiltInWorkTypeIds
{
    public static readonly WorkTypeId Operate = new WorkTypeId("work:operate");
    public static readonly WorkTypeId Restock = new WorkTypeId("work:restock");
    public static readonly WorkTypeId Construct = new WorkTypeId("work:construct");
    public static readonly WorkTypeId Repair = new WorkTypeId("work:repair");
    public static readonly WorkTypeId Clean = new WorkTypeId("work:clean");
    public static readonly WorkTypeId Research = new WorkTypeId("work:research");
    public static readonly WorkTypeId Guard = new WorkTypeId("work:guard");
    public static readonly WorkTypeId Reception = new WorkTypeId("work:reception");
    public static readonly WorkTypeId Rescue = new WorkTypeId("work:rescue");
    public static readonly WorkTypeId Rest = new WorkTypeId("work:rest");
    public static readonly WorkTypeId Craft = new WorkTypeId("work:craft");
    public static readonly WorkTypeId Haul = new WorkTypeId("work:haul");
    public static readonly WorkTypeId Hunt = new WorkTypeId("work:hunt");
    public static readonly WorkTypeId Butcher = new WorkTypeId("work:butcher");
    public static readonly WorkTypeId DrawWater = new WorkTypeId("work:draw-water");
    public static readonly WorkTypeId Cook = new WorkTypeId("work:cook");
    public static readonly WorkTypeId Treat = new WorkTypeId("work:treat");
    public static readonly WorkTypeId Refuel = new WorkTypeId("work:refuel");
    public static readonly WorkTypeId Warden = new WorkTypeId("work:warden");
    public static readonly WorkTypeId Perform = new WorkTypeId("work:perform");
}
