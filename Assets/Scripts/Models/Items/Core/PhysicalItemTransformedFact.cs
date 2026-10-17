using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class PhysicalItemTransformationInputData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public string itemInstanceId = string.Empty;
    public int quantity;
    public PhysicalItemTransformationInputData Clone() => (PhysicalItemTransformationInputData)MemberwiseClone();
}

[Serializable]
public sealed class PhysicalItemTransformationOutputData
{
    public string itemId = string.Empty;
    public int quantity;
    public int x;
    public int y;
    public int state;
    public string destinationId = string.Empty;
    public PhysicalItemTransformationOutputData Clone() => (PhysicalItemTransformationOutputData)MemberwiseClone();
}

[Serializable]
public sealed class PhysicalItemTransformationData
{
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public int absoluteDay;
    public bool wholeStack;
    public int inputQuantity;
    public long inputMassGrams;
    public long outputMassGrams;
    public int outputQuantity;
    public List<PhysicalItemTransformationInputData> inputs = new();
    public List<PhysicalItemTransformationOutputData> outputs = new();

    public PhysicalItemTransformationData Clone()
    {
        var result = (PhysicalItemTransformationData)MemberwiseClone();
        result.inputs = inputs?.Select(value => value?.Clone()).ToList();
        result.outputs = outputs?.Select(value => value?.Clone()).ToList();
        return result;
    }

    public void Validate()
    {
        if (!Id(operationId) || !Id(reasonCode) || absoluteDay < 0 || inputQuantity <= 0
            || outputQuantity <= 0 || inputMassGrams <= 0 || outputMassGrams <= 0
            || outputMassGrams > inputMassGrams || inputs == null || outputs == null
            || inputs.Count is < 1 or > 4096 || outputs.Count is < 1 or > 4096
            || wholeStack && inputs.Count != 1
            || inputs.Any(value => value == null || !Id(value.stackId) || !Id(value.itemId)
                || !OptionalId(value.itemInstanceId) || value.quantity <= 0)
            || inputs.Select(value => value.stackId).Distinct(StringComparer.Ordinal).Count() != inputs.Count
            || !inputs.Select(value => value.stackId).SequenceEqual(inputs.Select(value => value.stackId)
                .OrderBy(value => value, StringComparer.Ordinal))
            || inputs.Sum(value => (long)value.quantity) != inputQuantity
            || outputs.Any(value => value == null || !Id(value.itemId) || value.quantity <= 0
                || !Enum.IsDefined(typeof(WorldItemStackState), value.state)
                || value.state is (int)WorldItemStackState.Carried or (int)WorldItemStackState.InTransit
                || !DomainFactValueSyntax.IsText(value.destinationId, 256, true)
                || value.destinationId != value.destinationId.Trim())
            || outputs.Sum(value => (long)value.quantity) != outputQuantity)
            throw new InvalidOperationException("physical-transform-fact-invalid");
        for (int i = 1; i < outputs.Count; i++)
            if (Compare(outputs[i - 1], outputs[i]) >= 0)
                throw new InvalidOperationException("physical-transform-output-order-invalid");
    }

    public static int Compare(PhysicalItemTransformationOutputData left, PhysicalItemTransformationOutputData right)
    {
        int order = string.CompareOrdinal(left.itemId, right.itemId);
        if (order == 0) order = left.y.CompareTo(right.y);
        if (order == 0) order = left.x.CompareTo(right.x);
        if (order == 0) order = left.state.CompareTo(right.state);
        if (order == 0) order = string.CompareOrdinal(left.destinationId, right.destinationId);
        return order;
    }
    private static bool Id(string value) => DomainFactValueSyntax.IsIdentity(value);
    private static bool OptionalId(string value) => value == string.Empty || Id(value);
}

public sealed class PhysicalItemTransformedFact
{
    private readonly PhysicalItemTransformationData frozen;
    public PhysicalItemTransformedFact(PhysicalItemTransformationData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        frozen = source.Clone();
    }
    public PhysicalItemTransformationData Snapshot() => frozen.Clone();
}

[Serializable]
public sealed class PhysicalItemTransformationSaveData
{
    public PhysicalItemTransformationData fact = new();
    public string publicationKey = string.Empty;
    public string companionPublicationKey = string.Empty;
    public bool publicationConfirmed;
    public bool rollbackBlocked;
    public string rollbackFailure = string.Empty;

    public PhysicalItemTransformationSaveData Clone() => new()
    {
        fact = fact?.Clone(), publicationKey = publicationKey,
        companionPublicationKey = companionPublicationKey, publicationConfirmed = publicationConfirmed,
        rollbackBlocked = rollbackBlocked, rollbackFailure = rollbackFailure
    };

    public void Validate()
    {
        if (fact == null || !DomainFactValueSyntax.IsText(publicationKey, 1024, false)
            || !DomainFactValueSyntax.IsText(companionPublicationKey, 1024, true)
            || companionPublicationKey == publicationKey || rollbackBlocked && publicationConfirmed
            || !DomainFactValueSyntax.IsText(rollbackFailure, 2048, !rollbackBlocked)
            || !rollbackBlocked && rollbackFailure != string.Empty)
            throw new InvalidOperationException("physical-transform-journal-invalid");
        fact.Validate();
    }
}
