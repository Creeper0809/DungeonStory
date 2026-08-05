using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResourceAnatomyConditionLexicon : IAnatomyConditionLexicon
{
    private static readonly string[] RequiredSpecies =
    {
        "human", "slime", "orc", "vampire", "beastkin",
        "demon", "kobold", "myconid", "harpy", "golem"
    };

    private readonly IReadOnlyList<LexiconDefinition> lexicons;

    public ResourceAnatomyConditionLexicon(IGameContentCatalog content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<AnatomyConditionLexiconSO>())
    {
    }

    public ResourceAnatomyConditionLexicon(
        IEnumerable<AnatomyConditionLexiconSO> assets)
    {
        LexiconDefinition[] loaded = (assets ?? Array.Empty<AnatomyConditionLexiconSO>())
            .Where(asset => asset != null)
            .Select(asset => new LexiconDefinition(
                asset.LexiconId,
                asset.AnatomyFamily,
                asset.SpeciesIds,
                asset.Entries))
            .ToArray();
        lexicons = loaded.Length > 0
            ? loaded
            : throw new InvalidOperationException(
                "The root content catalog contains no authored anatomy lexicons.");
    }

    public bool TryResolve(
        string speciesId,
        string anatomyFamily,
        AnatomyConditionKind condition,
        out AnatomyConditionPresentation presentation)
    {
        string species = speciesId?.Trim() ?? string.Empty;
        string family = anatomyFamily?.Trim() ?? string.Empty;
        LexiconDefinition lexicon = lexicons.FirstOrDefault(item =>
                item.SpeciesIds.Contains(species, StringComparer.OrdinalIgnoreCase))
            ?? lexicons.FirstOrDefault(item => string.Equals(
                item.AnatomyFamily,
                family,
                StringComparison.OrdinalIgnoreCase));
        if (lexicon != null
            && lexicon.Entries.TryGetValue(
                condition,
                out AnatomyConditionLexiconEntry entry))
        {
            presentation = new AnatomyConditionPresentation(
                condition,
                entry.label,
                entry.treatmentVerb,
                entry.iconId,
                entry.vfxId);
            return true;
        }

        presentation = default;
        return false;
    }

    public IReadOnlyList<string> Validate(IAnatomyProfileCatalog anatomyProfiles)
    {
        List<string> errors = new();
        foreach (string species in RequiredSpecies)
        {
            AnatomyProfileDefinition profile = anatomyProfiles.GetForSpecies(species);
            foreach (AnatomyConditionKind condition in
                     Enum.GetValues(typeof(AnatomyConditionKind)))
            {
                if (!TryResolve(
                        species,
                        profile?.AnatomyFamily,
                        condition,
                        out AnatomyConditionPresentation presentation)
                    || string.IsNullOrWhiteSpace(presentation.Label)
                    || string.IsNullOrWhiteSpace(presentation.TreatmentVerb))
                {
                    errors.Add($"{species}: missing {condition} condition lexicon.");
                }
            }
        }

        return errors;
    }

    private static IReadOnlyList<LexiconDefinition> BuildDefaults()
    {
        return new[]
        {
            Lexicon("condition:biological", "humanoid",
                new[] { "human", "orc", "beastkin", "kobold" },
                "출혈", "감염", "쇼크", "골절·파열", "장기 정지", "거부 반응", "치료·수술", "치료"),
            Lexicon("condition:vampire", "humanoid", new[] { "vampire" },
                "혈액 고갈", "부패 감염", "혈류 쇼크", "골절·파열", "혈액낭 정지", "혈핵 거부", "혈술 처치 필요", "혈술 처치"),
            Lexicon("condition:demon", "humanoid", new[] { "demon" },
                "마력 누출", "룬 오염", "룬 붕괴", "각질 균열", "마핵 정지", "룬 비호환", "마핵 시술 필요", "룬 봉합"),
            Lexicon("condition:slime", "slime", new[] { "slime" },
                "점액 누출", "점액 오염", "응집 불안정", "외피 찢김", "핵 손상", "이질 점액 거부", "안정화·재성형", "재성형"),
            Lexicon("condition:myconid", "fungal", new[] { "myconid" },
                "수액·포자 누출", "부패·포자 오염", "군체 불안정", "균사 절단", "균핵 괴사", "접목 불화", "균사 처치", "접목"),
            Lexicon("condition:harpy", "avian", new[] { "harpy" },
                "출혈", "기낭 감염", "호흡 쇼크", "기낭·날개 파열", "기낭 정지", "이식 불화", "조류 처치 필요", "날개 고정"),
            Lexicon("condition:golem", "construct", new[] { "golem" },
                "냉각수 누수", "회로 오염·부식", "과부하", "외장 균열", "핵 균열·서보 파손", "부품 비호환", "정비·부품 교체", "정비")
        };
    }

    private static LexiconDefinition Lexicon(
        string id,
        string family,
        IEnumerable<string> species,
        string fluidLoss,
        string contamination,
        string overstrain,
        string fracture,
        string partFailure,
        string compatibility,
        string treatmentRequired,
        string verb)
    {
        return new LexiconDefinition(id, family, species, new[]
        {
            Entry(AnatomyConditionKind.FluidLoss, fluidLoss, verb),
            Entry(AnatomyConditionKind.Contamination, contamination, verb),
            Entry(AnatomyConditionKind.Overstrain, overstrain, verb),
            Entry(AnatomyConditionKind.Fracture, fracture, verb),
            Entry(AnatomyConditionKind.PartFailure, partFailure, verb),
            Entry(AnatomyConditionKind.CompatibilityFailure, compatibility, verb),
            Entry(AnatomyConditionKind.TreatmentRequired, treatmentRequired, verb)
        });
    }

    private static AnatomyConditionLexiconEntry Entry(
        AnatomyConditionKind condition,
        string label,
        string verb)
    {
        return new AnatomyConditionLexiconEntry
        {
            condition = condition,
            label = label,
            treatmentVerb = verb,
            iconId = $"condition:{condition.ToString().ToLowerInvariant()}",
            vfxId = $"medical:{condition.ToString().ToLowerInvariant()}"
        };
    }

    private sealed class LexiconDefinition
    {
        public LexiconDefinition(
            string id,
            string anatomyFamily,
            IEnumerable<string> speciesIds,
            IEnumerable<AnatomyConditionLexiconEntry> entries)
        {
            Id = id?.Trim() ?? string.Empty;
            AnatomyFamily = anatomyFamily?.Trim() ?? string.Empty;
            SpeciesIds = (speciesIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Entries = (entries ?? Array.Empty<AnatomyConditionLexiconEntry>())
                .Where(value => value != null)
                .GroupBy(value => value.condition)
                .ToDictionary(group => group.Key, group => group.First().Clone());
        }

        public string Id { get; }
        public string AnatomyFamily { get; }
        public IReadOnlyList<string> SpeciesIds { get; }
        public IReadOnlyDictionary<AnatomyConditionKind, AnatomyConditionLexiconEntry> Entries { get; }
    }
}
