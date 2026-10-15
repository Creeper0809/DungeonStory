using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Observes committed, typed facts. Observers neither mutate gameplay nor subscribe
/// a scene object's lifetime to the durability of an event. Replaying the ledger is
/// the observation source, including after a world restore.
/// </summary>
public interface IGameplayExperienceObserver
{
    string ObserverId { get; }
    IReadOnlyList<string> OutcomeTypes { get; }
    GameplayExperienceAssessmentSnapshot Observe(GameplayOutcomeEvidenceBindingSnapshot fact);
    bool CanJoin(
        GameplayOutcomeEvidenceBindingSnapshot previous,
        GameplayOutcomeEvidenceBindingSnapshot next);
}

public enum GameplayExperienceCategory
{
    Unassessed = 0,
    Routine = 1,
    Mild = 2,
    Meaningful = 3,
    Intense = 4,
    Transformative = 5,
    NotExperience = 6
}

public static class GameplayExperienceImportancePolicy
{
    public const int Version = 3;

    public static float Importance(GameplayExperienceCategory category) => category switch
    {
        GameplayExperienceCategory.Unassessed => 0f,
        GameplayExperienceCategory.NotExperience => 0f,
        GameplayExperienceCategory.Routine => .10f,
        GameplayExperienceCategory.Mild => .30f,
        GameplayExperienceCategory.Meaningful => .55f,
        GameplayExperienceCategory.Intense => .80f,
        GameplayExperienceCategory.Transformative => .95f,
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static GameplayExperienceAssessmentSnapshot Assess(
        string observerId, GameplayExperienceCategory category, string reason,
        params string[] evidenceMetricIds) => new()
    {
        policyVersion = Version,
        observerId = observerId,
        category = (int)category,
        importance = Importance(category),
        reasonCode = reason,
        evidenceMetricIds = (evidenceMetricIds ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()
    };

    public static bool Equivalent(
        GameplayExperienceAssessmentSnapshot left,
        GameplayExperienceAssessmentSnapshot right) => left != null && right != null
        && left.policyVersion == right.policyVersion
        && left.observerId == right.observerId && left.category == right.category
        && left.importance.Equals(right.importance) && left.reasonCode == right.reasonCode
        && left.explanationKo == right.explanationKo
        && left.evidenceMetricIds != null && right.evidenceMetricIds != null
        && left.evidenceMetricIds.SequenceEqual(right.evidenceMetricIds, StringComparer.Ordinal)
        && left.evidenceFactIds != null && right.evidenceFactIds != null
        && left.evidenceFactIds.SequenceEqual(right.evidenceFactIds, StringComparer.Ordinal)
        && left.evidenceRoleIds != null && right.evidenceRoleIds != null
        && left.evidenceRoleIds.SequenceEqual(right.evidenceRoleIds, StringComparer.Ordinal);

    public static string Describe(GameplayExperienceAssessmentSnapshot value)
    {
        if (value == null) return "미평가: 경험 관찰 정보 없음";
        string label = (GameplayExperienceCategory)value.category switch
        {
            GameplayExperienceCategory.Routine => "일상",
            GameplayExperienceCategory.Mild => "가벼운 경험",
            GameplayExperienceCategory.Meaningful => "의미 있는 경험",
            GameplayExperienceCategory.Intense => "강렬한 경험",
            GameplayExperienceCategory.Transformative => "결정적 경험",
            GameplayExperienceCategory.NotExperience => "개인 경험 아님",
            _ => "미평가"
        };
        return label + " / 판단 근거: " + (string.IsNullOrEmpty(value.explanationKo)
            ? value.reasonCode : value.explanationKo);
    }
}

/// <summary>Explicit opt-in composition; no type-name/prose heuristics or raw-score fallback.</summary>
public sealed class GameplayExperienceObserverRegistry
{
    private readonly Dictionary<string, IGameplayExperienceObserver> observers =
        new(StringComparer.Ordinal);
    public IReadOnlyList<string> SupportedOutcomeTypes => observers.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public GameplayExperienceObserverRegistry(IEnumerable<IGameplayExperienceObserver> source)
    {
        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (IGameplayExperienceObserver observer in source
                 ?? throw new ArgumentNullException(nameof(source)))
        {
            if (observer == null || !GameplayOutcomeStableIdSyntax.IsValid(observer.ObserverId)
                || !identities.Add(observer.ObserverId) || observer.OutcomeTypes == null
                || observer.OutcomeTypes.Count == 0)
                throw new ArgumentException("Experience observer registration is invalid.");
            foreach (string type in observer.OutcomeTypes)
            {
                if (!GameplayOutcomeStableIdSyntax.IsValid(type) || observers.ContainsKey(type))
                    throw new ArgumentException("Duplicate or invalid experience outcome registration: " + type);
                observers.Add(type, observer);
            }
        }
    }

    public GameplayExperienceAssessmentSnapshot Observe(GameplayOutcomeEvidenceBindingSnapshot fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        if (!observers.TryGetValue(fact.outcomeTypeId, out IGameplayExperienceObserver observer))
            return GameplayExperienceImportancePolicy.Assess("experience:unregistered",
                GameplayExperienceCategory.Unassessed, "observer-not-registered");
        GameplayExperienceAssessmentSnapshot result = observer.Observe(fact);
        if (result == null || result.observerId != observer.ObserverId
            || result.policyVersion != GameplayExperienceImportancePolicy.Version
            || !Enum.IsDefined(typeof(GameplayExperienceCategory), result.category)
            || result.importance != GameplayExperienceImportancePolicy.Importance(
                (GameplayExperienceCategory)result.category)
            || !GameplayOutcomeStableIdSyntax.IsValid(result.reasonCode)
            || result.evidenceMetricIds == null || result.evidenceFactIds == null || result.evidenceRoleIds == null)
            throw new InvalidOperationException("Experience observer returned an invalid assessment.");
        return result;
    }

    public bool CanJoin(GameplayOutcomeEvidenceBindingSnapshot previous,
        GameplayOutcomeEvidenceBindingSnapshot next) => previous != null && next != null
        && previous.subjectKindId == next.subjectKindId && previous.subjectId == next.subjectId
        && previous.outcomeRunId == next.outcomeRunId && previous.sourceKind == "exact"
        && next.sourceKind == "exact" && previous.outcomeSequence < next.outcomeSequence
        && observers.TryGetValue(previous.outcomeTypeId, out IGameplayExperienceObserver observer)
        && observers.TryGetValue(next.outcomeTypeId, out IGameplayExperienceObserver nextObserver)
        && ReferenceEquals(observer, nextObserver) && observer.CanJoin(previous, next);
}

/// <summary>
/// A read-side group, not a new world fact or a synthetic causal parent. All members
/// retain their exact identities. Importance is the strongest evidenced member,
/// never a sum of ticks, novelty bonuses or a storage pin.
/// </summary>
public sealed class GameplayExperienceEpisode
{
    private readonly Dictionary<string, GameplayExperienceAssessmentSnapshot> assessments;

    internal GameplayExperienceEpisode(IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> members,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        if (members == null || members.Count == 0 || bindings == null || members.Count != bindings.Count)
            throw new ArgumentException("Empty or inconsistent experience.");
        Members = members.ToArray();
        assessments = bindings.ToDictionary(value => value.publicFactId,
            value => value.experience.Clone(), StringComparer.Ordinal);
        ExperienceId = "experience:" + NarrativeInferenceHash.ComputeSha256Utf8(
            GameplayExperienceImportancePolicy.Version + "|"
            + string.Join("|", bindings.Select(value => value.experience.observerId + ":" + value.publicFactId).OrderBy(value => value,
                StringComparer.Ordinal)));
        Importance = bindings.Max(value => value.experience.importance);
        IsAssessed = bindings.Any(value => value.experience.category != 0);
    }
    public string ExperienceId { get; }
    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> Members { get; }
    public float Importance { get; }
    public bool IsAssessed { get; }
    internal GameplayExperienceAssessmentSnapshot AssessmentFor(string sourceId) => assessments[sourceId];
}

public static class GameplayExperienceProjection
{
    public static IReadOnlyList<GameplayExperienceEpisode> Build(
        IEnumerable<GameplayOutcomeNarrativeEvidenceSource> sources,
        GameplayExperienceObserverRegistry registry)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        var ordered = (sources ?? throw new ArgumentNullException(nameof(sources)))
            .OrderBy(value => value.FirstDay)
            .ThenBy(value => value.HasExactOutcome ? value.ExactOutcomeId.Sequence : long.MaxValue)
            .ThenBy(value => value.SourceId, StringComparer.Ordinal).ToArray();
        if (ordered.Select(value => value.SourceId).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new InvalidOperationException("Duplicate facts cannot create additional experience.");
        var episodes = new List<GameplayExperienceEpisode>();
        var members = new List<GameplayOutcomeNarrativeEvidenceSource>();
        var bindings = new List<GameplayOutcomeEvidenceBindingSnapshot>();
        GameplayOutcomeEvidenceBindingSnapshot previous = null;
        foreach (var source in ordered)
        {
            var fact = GameplayOutcomeEvidenceFormulaProjection.Capture(new[] { source })[0];
            if (!ReferenceEquals(registry, GameplayExperienceObserverRegistration.Default))
                fact.experience = registry.Observe(fact);
            if (previous != null && !registry.CanJoin(previous, fact))
            {
                episodes.Add(new GameplayExperienceEpisode(members, bindings));
                members.Clear();
                bindings.Clear();
            }
            members.Add(source);
            bindings.Add(fact);
            previous = fact;
        }
        if (members.Count > 0) episodes.Add(new GameplayExperienceEpisode(members, bindings));
        return episodes.OrderByDescending(value => value.Importance)
            .ThenByDescending(value => value.Members.Max(member => member.LastDay))
            .ThenBy(value => value.ExperienceId, StringComparer.Ordinal).ToArray();
    }
}
