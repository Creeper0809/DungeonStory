using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Medical/Surgical Graft Effect")]
public sealed class SurgicalGraftEffectDefinitionSO : ScriptableObject
{
    [ContentIdentity, SerializeField] private int numericId;
    [ContentIdentity, SerializeField] private string graftId = string.Empty;
    [SerializeField] private WildlifeSpeciesSO donorSpecies;
    [SerializeField] private AnatomyNodeKind requiredNodeKind;
    [SerializeField] private AnatomyFunction requiredFunction;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea] private string description = string.Empty;
    [SerializeField] private List<GameplayEffectBinding> effects = new();

    public int NumericId => numericId;
    public string GraftId => graftId?.Trim() ?? string.Empty;
    public string DonorSpeciesId => donorSpecies != null ? donorSpecies.SpeciesId : string.Empty;
    public AnatomyFunction RequiredFunction => requiredFunction;
    public AnatomyNodeKind RequiredNodeKind => requiredNodeKind;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;
    public IReadOnlyList<GameplayEffectBinding> Effects => Array.AsReadOnly(effects.Select(value =>
        new GameplayEffectBinding { bindingId = value.bindingId, definition = value.definition,
            value = value.value, condition = value.condition }).ToArray());

    public IReadOnlyList<string> ValidateDefinition()
    {
        var errors = new List<string>();
        if (numericId <= 0 || GraftId.Length == 0 || DisplayName.Length == 0)
            errors.Add("Surgical graft requires an identity and display name.");
        if (DonorSpeciesId.Length == 0 || requiredFunction == AnatomyFunction.None
            || !Enum.IsDefined(typeof(AnatomyNodeKind), requiredNodeKind))
            errors.Add($"Graft '{GraftId}' requires a donor species and anatomy function.");
        if (effects == null || effects.Count == 0)
            errors.Add($"Graft '{GraftId}' requires an effect.");
        var source = new GameplayEffectSourceRef(GameplayEffectSourceKind.SurgicalPart, GraftId);
        foreach (var effect in effects ?? new List<GameplayEffectBinding>())
        {
            if (effect == null) { errors.Add($"Graft '{GraftId}' has a null effect."); continue; }
            if (!effect.IsValidFor(source, out string reason)) errors.Add(reason);
            if (effect.definition == null) continue;
            errors.AddRange(effect.definition.ValidateDefinition());
            if (effect.definition.Operation != GameplayEffectOperation.Multiply
                && effect.definition.Operation != GameplayEffectOperation.AddFlat
                && effect.definition.Operation != GameplayEffectOperation.AddPercent)
                errors.Add($"Graft '{GraftId}' has an unsupported scaling operation.");
            if (effect.definition.Operation == GameplayEffectOperation.Multiply && effect.value <= 0f)
                errors.Add($"Graft '{GraftId}' requires a positive multiplier.");
        }
        if (effects != null && effects.Where(value => value != null)
            .GroupBy(value => value.bindingId, StringComparer.Ordinal).Any(group => group.Count() > 1))
            errors.Add($"Graft '{GraftId}' has duplicate effect bindings.");
        return errors;
    }
}

// 정의 조회만 소유한다. 장기 상태와 저장 값은 기존 수술 원장이 소유한다.
public sealed class SurgicalGraftEffectCatalog
{
    private readonly IReadOnlyList<SurgicalGraftEffectDefinitionSO> definitions;
    private readonly Dictionary<string, SurgicalGraftEffectDefinitionSO> byId;

    public SurgicalGraftEffectCatalog(IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        definitions = content.GetAll<SurgicalGraftEffectDefinitionSO>();
        byId = new Dictionary<string, SurgicalGraftEffectDefinitionSO>(StringComparer.Ordinal);
        var numericIds = new HashSet<int>();
        foreach (var definition in definitions)
        {
            if (definition == null) throw new InvalidOperationException("Surgical graft definition is null.");
            var errors = definition.ValidateDefinition();
            if (errors.Count != 0) throw new InvalidOperationException(string.Join(" | ", errors));
            if (byId.ContainsKey(definition.GraftId))
                throw new InvalidOperationException("Duplicate surgical graft: " + definition.GraftId);
            if (!numericIds.Add(definition.NumericId))
                throw new InvalidOperationException("Duplicate surgical graft numeric id: " + definition.NumericId);
            byId.Add(definition.GraftId, definition);
        }
    }

    public SurgicalGraftEffectDefinitionSO FindForExtraction(string speciesId, AnatomyNodeDefinition node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        var matches = definitions.Where(value =>
            string.Equals(value.DonorSpeciesId, speciesId, StringComparison.OrdinalIgnoreCase)
            && node.Kind == value.RequiredNodeKind
            && (node.Functions & value.RequiredFunction) == value.RequiredFunction).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"Ambiguous surgical graft for '{speciesId}:{node.NodeId}'.");
        return matches.SingleOrDefault();
    }

    public SurgicalGraftEffectDefinitionSO Require(string graftId) =>
        byId.TryGetValue(graftId?.Trim() ?? string.Empty, out var definition)
            ? definition
            : throw new InvalidOperationException("Unknown surgical graft: " + graftId);

    public void ValidateSavedParts(IEnumerable<SurgicalPartInstance> parts)
    {
        foreach (var part in parts ?? Array.Empty<SurgicalPartInstance>())
            if (part != null && !string.IsNullOrWhiteSpace(part.specialEffectId))
                Require(part.specialEffectId);
    }
}
