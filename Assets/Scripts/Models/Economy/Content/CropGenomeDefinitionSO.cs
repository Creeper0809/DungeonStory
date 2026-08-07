using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Economy/Crop Genome", order = 3)]
public sealed class CropGenomeDefinitionSO : DataScriptableObject
{
    [SerializeField] private string genomeId = string.Empty;
    [SerializeField] private string cropId = string.Empty;
    [SerializeField] private string cultivarName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField, Min(1)] private int authoringRevision = 1;
    [SerializeField] private List<string> tradeoffTags = new();
    [SerializeField] private List<DiploidLocusSaveData> loci = new();

    public string GenomeId => genomeId?.Trim() ?? string.Empty;
    public string CropId => cropId?.Trim() ?? string.Empty;
    public string CultivarName => cultivarName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;
    public int AuthoringRevision => authoringRevision;
    public IReadOnlyList<string> TradeoffTags => tradeoffTags;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (GenomeId.Length == 0 || CropId.Length == 0) errors.Add("Crop genome requires genome and crop IDs.");
        if (CultivarName.Length == 0) errors.Add($"'{GenomeId}' requires a cultivar name.");
        if (authoringRevision < 1) errors.Add($"'{GenomeId}' authoring revision must be positive.");
        if (loci == null || loci.Count != 6 || loci.Select(value => value.locus).Distinct().Count() != 6)
            errors.Add($"'{GenomeId}' requires exactly six distinct diploid loci.");
        return errors;
    }

    public CultivarGenomeSaveData CreateRuntimeDefinition() => new()
    {
        genomeId = GenomeId,
        cropId = CropId,
        generation = 0,
        loci = (loci ?? new()).Select(value => new DiploidLocusSaveData
        {
            locus = value.locus,
            alleleA = value.alleleA,
            alleleB = value.alleleB
        }).ToList()
    };

#if UNITY_EDITOR
    public void Configure(string stableGenomeId, string stableCropId, IReadOnlyList<DiploidLocusSaveData> authoredLoci)
    {
        genomeId = stableGenomeId?.Trim() ?? string.Empty;
        cropId = stableCropId?.Trim() ?? string.Empty;
        loci = (authoredLoci ?? Array.Empty<DiploidLocusSaveData>()).Select(value => new DiploidLocusSaveData
        {
            locus = value.locus,
            alleleA = Mathf.Clamp(value.alleleA, -2, 2),
            alleleB = Mathf.Clamp(value.alleleB, -2, 2)
        }).ToList();
        cultivarName = string.IsNullOrWhiteSpace(cultivarName)
            ? stableCropId?.Replace("crop:", string.Empty) + " 기본종"
            : cultivarName;
        description = string.IsNullOrWhiteSpace(description)
            ? "기본 환경에 적응한 표준 품종."
            : description;
        authoringRevision = Mathf.Max(1, authoringRevision);
        tradeoffTags ??= new List<string>();
    }

    public void ConfigureCultivar(
        string stableGenomeId,
        string stableCropId,
        string name,
        string detail,
        IReadOnlyList<string> costs,
        IReadOnlyList<DiploidLocusSaveData> authoredLoci)
    {
        Configure(stableGenomeId, stableCropId, authoredLoci);
        cultivarName = name?.Trim() ?? string.Empty;
        description = detail?.Trim() ?? string.Empty;
        authoringRevision = 1;
        tradeoffTags = (costs ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
#endif
}
