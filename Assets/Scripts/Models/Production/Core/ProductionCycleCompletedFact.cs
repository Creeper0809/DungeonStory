using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class ProductionCompletedLineData
{
    public string outputLineId = "", itemId = "", capabilityFingerprint = "";
    public List<string> commitIds = new();
    public int quantity;
    public long massGrams;
    public ProductionCompletedLineData Clone()
    { var copy = (ProductionCompletedLineData)MemberwiseClone(); copy.commitIds = commitIds?.ToList(); return copy; }
}

[Serializable]
public sealed class ProductionCompletedStackData
{
    public string stackId = "", outputLineId = "", itemId = "", itemInstanceId = "", sourceCommitId = "", componentSignature = "";
    public int quantity;
    public long massGrams;
    public ProductionCompletedStackData Clone() => (ProductionCompletedStackData)MemberwiseClone();
}

[Serializable]
public sealed class ProductionCycleCompletedFactData
{
    public string billId = "", recipeId = "", facilityId = "", facilityDisplayName = "", workerId = "", workerDisplayName = "";
    public int cycleSequence, absoluteDay;
    public string batchCommitId = "", outcomeFingerprint = "", plannedOutputFingerprint = "", destinationId = "", wipInputCommitId = "";
    public int wipInputQuantity;
    public long wipInputMassGrams, processCleanWaterMassGrams, processWastewaterMassGrams, declaredLossMassGrams, declaredExternalInputMassGrams;
    public List<ProductionCompletedLineData> lines = new();
    public List<ProductionCompletedStackData> stacks = new();
    public ProductionCycleCompletedFactData Clone()
    {
        var copy = (ProductionCycleCompletedFactData)MemberwiseClone();
        copy.lines = lines?.Select(value => value?.Clone()).ToList();
        copy.stacks = stacks?.Select(value => value?.Clone()).ToList();
        return copy;
    }
}

// A frozen physical production fact. No ledger IDs, text rendering or scoring contract.
public sealed class ProductionCycleCompletedFact
{
    private readonly ProductionCycleCompletedFactData data;
    public ProductionCycleCompletedFact(ProductionCycleCompletedFactData value)
    {
        if (value == null || !Id(value.billId) || !Id(value.recipeId) || !Id(value.facilityId)
            || value.cycleSequence < 1 || value.absoluteDay < 1 || !Text(value.facilityDisplayName, false)
            || !OptionalId(value.workerId) || !Text(value.workerDisplayName, value.workerId.Length == 0)
            || !Id(value.batchCommitId) || !Hash(value.outcomeFingerprint) || !Hash(value.plannedOutputFingerprint)
            || !Id(value.destinationId) || !OptionalId(value.wipInputCommitId) || value.wipInputQuantity < 0
            || value.wipInputMassGrams < 0 || value.processCleanWaterMassGrams < 0 || value.processWastewaterMassGrams < 0
            || value.declaredLossMassGrams < 0 || value.declaredExternalInputMassGrams < 0
            || value.lines == null || value.stacks == null || value.lines.Count == 0 || value.stacks.Count == 0)
            throw new InvalidOperationException("production-completion-fact-invalid");
        foreach (var line in value.lines)
            if (line == null || !Id(line.outputLineId) || !Id(line.itemId) || !Hash(line.capabilityFingerprint)
                || line.quantity <= 0 || line.massGrams <= 0 || line.commitIds == null || line.commitIds.Count == 0
                || line.commitIds.Any(id => !Id(id)) || line.commitIds.Distinct(StringComparer.Ordinal).Count() != line.commitIds.Count)
                throw new InvalidOperationException("production-completion-line-invalid");
        foreach (var stack in value.stacks)
            if (stack == null || !Id(stack.stackId) || !Id(stack.outputLineId) || !Id(stack.itemId)
                || !OptionalId(stack.itemInstanceId) || !OptionalId(stack.sourceCommitId)
                || stack.componentSignature == null || stack.componentSignature.Length > 0 && !Hash(stack.componentSignature)
                || stack.quantity <= 0 || stack.massGrams <= 0)
                throw new InvalidOperationException("production-completion-stack-invalid");
        if (value.lines.Select(line => line.outputLineId).Distinct(StringComparer.Ordinal).Count() != value.lines.Count
            || value.stacks.Select(stack => stack.stackId).Distinct(StringComparer.Ordinal).Count() != value.stacks.Count
            || value.stacks.Any(stack => !value.lines.Any(line => line.outputLineId == stack.outputLineId && line.itemId == stack.itemId))
            || value.lines.Any(line => value.stacks.Where(stack => stack.outputLineId == line.outputLineId).Sum(stack => stack.quantity) != line.quantity
                || value.stacks.Where(stack => stack.outputLineId == line.outputLineId).Sum(stack => stack.massGrams) != line.massGrams))
            throw new InvalidOperationException("production-completion-physical-lines-conflict");
        data = value.Clone();
    }
    public ProductionCycleCompletedFactData Snapshot() => data.Clone();
    public string BillId => data.billId;
    public int CycleSequence => data.cycleSequence;
    public static bool Id(string value) => DomainFactValueSyntax.IsIdentity(value);
    private static bool OptionalId(string value) => value != null && (value.Length == 0 || Id(value));
    private static bool Text(string value, bool empty) => DomainFactValueSyntax.IsText(value, 4096, empty);
    public static bool Hash(string value) => value?.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}

[Serializable]
public sealed class ProductionCompletionObservationSaveData
{
    public ProductionCycleCompletedFactData fact = new();
    public string publicationKey = "", publicationId = "", publicationFingerprint = "";
    public ProductionCompletionObservationSaveData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, publicationId = publicationId, publicationFingerprint = publicationFingerprint };
    public void Validate(string billId, int cycle, string recipeId, string facilityId)
    {
        _ = new ProductionCycleCompletedFact(fact);
        if (fact.billId != billId || fact.cycleSequence != cycle || fact.recipeId != recipeId || fact.facilityId != facilityId
            || !DomainFactValueSyntax.IsText(publicationKey, 4096, false) || publicationKey != publicationKey.Trim()
            || publicationId == null || publicationFingerprint == null
            || (publicationId.Length == 0) != (publicationFingerprint.Length == 0)
            || publicationId.Length > 0 && (!DomainFactValueSyntax.IsText(publicationId, 4096, false)
                || publicationId != publicationId.Trim() || !ProductionCycleCompletedFact.Hash(publicationFingerprint)))
            throw new InvalidOperationException("production-completion-observation-invalid");
    }
}

