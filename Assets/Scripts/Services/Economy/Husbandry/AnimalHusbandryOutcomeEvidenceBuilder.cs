using System;
using System.Collections.Generic;
using System.Linq;

internal static class AnimalHusbandryOutcomeEvidenceBuilder
{
    internal static AnimalHusbandryOutcomeOperationKind ToOutcomeKind(
        AnimalHusbandryWorkKind kind) => kind switch
    {
        AnimalHusbandryWorkKind.Tame =>
            AnimalHusbandryOutcomeOperationKind.Tame,
        AnimalHusbandryWorkKind.CollectProduct =>
            AnimalHusbandryOutcomeOperationKind.CollectProduct,
        AnimalHusbandryWorkKind.CollectManure =>
            AnimalHusbandryOutcomeOperationKind.CollectManure,
        AnimalHusbandryWorkKind.Slaughter =>
            AnimalHusbandryOutcomeOperationKind.Slaughter,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    internal static int ToMilliWork(float work)
    {
        double value = Math.Round(Math.Max(0f, work) * 1000d);
        if (value <= 0d || value > int.MaxValue)
            throw new InvalidOperationException("Husbandry completed work is out of range.");
        return (int)value;
    }

    internal static List<CraftContributionSaveData> CaptureContributions(
        HusbandryAnimalState state,
        string workerId,
        float accepted)
    {
        var contributions = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (CraftContributionSaveData value in
                 state.PendingWorkContributions
                 ?? new List<CraftContributionSaveData>())
        {
            if (value != null && !string.IsNullOrWhiteSpace(value.characterId))
                contributions[value.characterId] = value.contributedWork;
        }
        contributions.TryGetValue(workerId, out float current);
        contributions[workerId] = current + accepted;
        return contributions.OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new CraftContributionSaveData
            { characterId = value.Key, contributedWork = value.Value }).ToList();
    }

    internal static List<AnimalHusbandryOutputStack> CaptureOutputDeltas(
        IReadOnlyDictionary<string, int> before,
        IReadOnlyDictionary<string, int> after,
        int expectedQuantity)
    {
        var deltas = new List<AnimalHusbandryOutputStack>();
        int total = 0;
        foreach (KeyValuePair<string, int> value in after
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            int previous = before != null
                && before.TryGetValue(value.Key, out int found)
                    ? found
                    : 0;
            int delta = value.Value - previous;
            if (delta <= 0)
                continue;
            deltas.Add(new AnimalHusbandryOutputStack { StackId = value.Key, Quantity = delta });
            total = checked(total + delta);
        }
        if (total != expectedQuantity || deltas.Count == 0)
        {
            throw new InvalidOperationException(
                "Husbandry physical output vector did not match the exact spawned quantity.");
        }
        return deltas;
    }
}
