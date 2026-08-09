using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum FactionChapterKind { FirstContact, InternalProblem, RivalConflict, Intervention, CrisisOrBetrayal, Resolution }

[CreateAssetMenu(fileName = "FactionChapter", menuName = "DungeonStory/V20/Faction Chapter")]
public sealed class FactionChapterDefinitionSO : V20AuthoredContentSO
{
    public string factionId = string.Empty;
    [Range(1, 6)] public int chapterNumber = 1;
    public FactionChapterKind kind;
    public string crossFactionId = string.Empty;
    public V20ContentRequirementSet triggerRequirements = new();
    public List<V20ChoiceDefinition> choices = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(factionId)) errors.Add($"'{StableId}' requires a faction id.");
        if (kind == FactionChapterKind.RivalConflict
            && string.IsNullOrWhiteSpace(crossFactionId))
            errors.Add($"'{StableId}' rival chapter requires a cross-faction id.");
        if (choices == null || choices.Count != 3)
            errors.Add($"'{StableId}' requires support, bargain, and refuse outcomes.");
        else
        {
            foreach (V20ChoiceDefinition choice in choices)
                errors.AddRange(choice.Validate(StableId));
            string[] expected = { "support", "bargain", "refuse" };
            if (!expected.All(id => choices.Count(choice => string.Equals(
                    choice.choiceId,
                    id,
                    System.StringComparison.Ordinal)) == 1))
                errors.Add($"'{StableId}' must define exactly one support, bargain, and refuse outcome.");

            V20ChoiceDefinition support = choices.FirstOrDefault(value =>
                string.Equals(value.choiceId, "support", System.StringComparison.Ordinal));
            V20ChoiceDefinition bargain = choices.FirstOrDefault(value =>
                string.Equals(value.choiceId, "bargain", System.StringComparison.Ordinal));
            V20ChoiceDefinition refuse = choices.FirstOrDefault(value =>
                string.Equals(value.choiceId, "refuse", System.StringComparison.Ordinal));
            int supportCost = ConsumableCost(support);
            int bargainCost = ConsumableCost(bargain);
            if (supportCost <= 0 || bargainCost <= 0 || bargainCost >= supportCost)
                errors.Add($"'{StableId}' support must consume items and bargain must consume a smaller amount.");
            if (!HasOperationalFacility(support) || !HasOperationalFacility(bargain))
                errors.Add($"'{StableId}' support and bargain require an operational facility.");
            if (!HasEffect(refuse, V20ContentEffectKind.FactionGrievance)
                || !HasEffect(refuse, V20ContentEffectKind.Threat))
                errors.Add($"'{StableId}' refusal must create grievance and a follow-up threat.");
            if (!string.IsNullOrWhiteSpace(crossFactionId)
                && !choices.SelectMany(value => value.effects ?? new())
                    .Any(effect => effect != null && string.Equals(
                        effect.targetId,
                        crossFactionId,
                        System.StringComparison.Ordinal)))
                errors.Add($"'{StableId}' cross-faction chapter must change the counterpart faction.");
        }
        return errors;
    }

    public string MechanicalSignature() => string.Join("||", (choices ?? new())
        .OrderBy(value => value.choiceId, System.StringComparer.Ordinal)
        .Select(ChoiceSignature));

    private static int ConsumableCost(V20ChoiceDefinition choice) =>
        (choice?.requirements?.items ?? new())
        .Where(value => value != null && value.consume)
        .Sum(value => value.amount);

    private static bool HasOperationalFacility(V20ChoiceDefinition choice) =>
        (choice?.requirements?.facilities ?? new()).Any(value =>
            value != null && value.mustBeOperational);

    private static bool HasEffect(
        V20ChoiceDefinition choice,
        V20ContentEffectKind kind) =>
        (choice?.effects ?? new()).Any(value => value != null && value.kind == kind);

    private static string ChoiceSignature(V20ChoiceDefinition choice)
    {
        string items = string.Join(",", (choice?.requirements?.items ?? new())
            .Where(value => value != null)
            .OrderBy(value => value.itemDefinitionId, System.StringComparer.Ordinal)
            .Select(value => $"{value.itemDefinitionId}:{value.amount}:{value.consume}"));
        string facilities = string.Join(",", (choice?.requirements?.facilities ?? new())
            .Where(value => value != null)
            .OrderBy(value => value.buildingDefinitionId, System.StringComparer.Ordinal)
            .ThenBy(value => value.capabilityId, System.StringComparer.Ordinal)
            .Select(value => $"{value.buildingDefinitionId}:{value.capabilityId}:{value.minimumCount}:{value.mustBeOperational}"));
        string effects = string.Join(",", (choice?.effects ?? new())
            .Where(value => value != null)
            .OrderBy(value => value.kind)
            .ThenBy(value => value.targetId, System.StringComparer.Ordinal)
            .Select(value => $"{value.kind}:{value.targetId}:{value.amount}:{value.durationDays}"));
        return $"{choice?.choiceId}[{items}][{facilities}][{effects}]";
    }
}
