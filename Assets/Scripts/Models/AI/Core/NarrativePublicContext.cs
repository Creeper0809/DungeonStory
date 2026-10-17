using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public enum NarrativePublicSubjectKind
{
    Character,
    Equipment,
    Facility
}

public enum NarrativePublicEntityKind
{
    Character,
    Equipment,
    Facility,
    Place,
    Group
}

[Flags]
public enum NarrativePublicFactCategory
{
    None = 0,
    Background = 1 << 0,
    Relationship = 1 << 1,
    Memory = 1 << 2,
    PriorHistory = 1 << 3
}

public sealed class NarrativePublicEntityInput
{
    public NarrativePublicEntityInput(
        string entityId,
        NarrativePublicEntityKind kind,
        string displayName)
    {
        EntityId = entityId;
        Kind = kind;
        DisplayName = displayName;
    }

    public string EntityId { get; }
    public NarrativePublicEntityKind Kind { get; }
    public string DisplayName { get; }
}

public sealed class NarrativePublicEventInput
{
    public NarrativePublicEventInput(
        string evidenceId,
        string actorId,
        string targetId,
        string eventType,
        string outcome,
        int? day,
        int count,
        int? generation)
    {
        EvidenceId = evidenceId;
        ActorId = actorId;
        TargetId = targetId;
        EventType = eventType;
        Outcome = outcome;
        Day = day;
        Count = count;
        Generation = generation;
    }

    public string EvidenceId { get; }
    public string ActorId { get; }
    public string TargetId { get; }
    public string EventType { get; }
    public string Outcome { get; }
    public int? Day { get; }
    public int Count { get; }
    public int? Generation { get; }
}

public sealed class NarrativePublicFactInput
{
    public NarrativePublicFactInput(
        string domain,
        string originalFactId,
        string sourceSubjectId,
        string text,
        int priority,
        NarrativePublicFactCategory category = NarrativePublicFactCategory.None,
        string publicSubjectId = "",
        string outcome = "",
        int? lastDay = null,
        int? count = null,
        int? milestoneCount = null,
        decimal? totalValue = null,
        NarrativePublicEventInput eventData = null)
    {
        Domain = domain;
        OriginalFactId = originalFactId;
        SourceSubjectId = sourceSubjectId;
        Text = text;
        Priority = priority;
        Category = category;
        PublicSubjectId = publicSubjectId;
        Outcome = outcome;
        LastDay = lastDay;
        Count = count;
        MilestoneCount = milestoneCount;
        TotalValue = totalValue;
        EventData = eventData;
    }

    public string Domain { get; }
    public string OriginalFactId { get; }
    public string SourceSubjectId { get; }
    public string Text { get; }
    public int Priority { get; }
    public NarrativePublicFactCategory Category { get; }
    public string PublicSubjectId { get; }
    public string Outcome { get; }
    public int? LastDay { get; }
    public int? Count { get; }
    public int? MilestoneCount { get; }
    public decimal? TotalValue { get; }
    public NarrativePublicEventInput EventData { get; }
}

public sealed class NarrativePublicEntity
{
    internal NarrativePublicEntity(
        string entityId,
        NarrativePublicEntityKind kind,
        string displayName)
    {
        EntityId = entityId;
        Kind = kind;
        DisplayName = displayName;
    }

    public string EntityId { get; }
    public NarrativePublicEntityKind Kind { get; }
    public string KindToken => NarrativePublicContextFactory.EntityKindToken(Kind);
    public string DisplayName { get; }
}

public sealed class NarrativePublicFact
{
    internal NarrativePublicFact(
        string factId,
        string domain,
        string originalFactId,
        string sourceSubjectId,
        string subjectId,
        string text,
        int priority,
        NarrativePublicFactCategory category,
        string outcome,
        int? lastDay,
        int? count,
        int? milestoneCount,
        string totalValueDecimal,
        NarrativePublicEventInput eventData,
        string canonicalSourceTuple)
    {
        FactId = factId;
        Domain = domain;
        OriginalFactId = originalFactId;
        SourceSubjectId = sourceSubjectId;
        SubjectId = subjectId;
        Text = text;
        Priority = priority;
        Category = category;
        Outcome = outcome;
        LastDay = lastDay;
        Count = count;
        MilestoneCount = milestoneCount;
        TotalValueDecimal = totalValueDecimal;
        EventData = eventData;
        CanonicalSourceTuple = canonicalSourceTuple;
    }

    public string FactId { get; }
    public string Domain { get; }
    public string OriginalFactId { get; }
    public string SourceSubjectId { get; }
    public string SubjectId { get; }
    public string Text { get; }
    public int Priority { get; }
    public NarrativePublicFactCategory Category { get; }
    public string Outcome { get; }
    public int? LastDay { get; }
    public int? Count { get; }
    public int? MilestoneCount { get; }
    public string TotalValueDecimal { get; }
    /// <summary>
    /// Immutable structured event snapshot retained with this public fact.
    /// Cross-assembly evidence composers must copy this value instead of
    /// reconstructing or dropping event identity, actors, targets, or time.
    /// </summary>
    public NarrativePublicEventInput EventData { get; }
    internal string CanonicalSourceTuple { get; }
}

public sealed class NarrativePublicEvent
{
    internal NarrativePublicEvent(
        string factId,
        string evidenceId,
        string actorId,
        string targetId,
        string eventType,
        string outcome,
        int? day,
        int count,
        int? generation)
    {
        FactId = factId;
        EvidenceId = evidenceId;
        ActorId = actorId;
        TargetId = targetId;
        EventType = eventType;
        Outcome = outcome;
        Day = day;
        Count = count;
        Generation = generation;
    }

    public string FactId { get; }
    public string EvidenceId { get; }
    public string ActorId { get; }
    public string TargetId { get; }
    public string EventType { get; }
    public string Outcome { get; }
    public int? Day { get; }
    public int Count { get; }
    public int? Generation { get; }
}

public sealed class NarrativePublicContextSelection
{
    internal NarrativePublicContextSelection(
        string policyId,
        int availableFactCount,
        int omittedFactCount)
    {
        PolicyId = policyId;
        AvailableFactCount = availableFactCount;
        OmittedFactCount = omittedFactCount;
    }

    public string PolicyId { get; }
    public int AvailableFactCount { get; }
    public int OmittedFactCount { get; }
}

[Serializable]
public sealed class NarrativePublicEntitySnapshot
{
    public string entityId = string.Empty;
    public NarrativePublicEntityKind kind;
    public string displayName = string.Empty;

    public NarrativePublicEntitySnapshot Clone() => new()
    {
        entityId = entityId ?? string.Empty,
        kind = kind,
        displayName = displayName ?? string.Empty
    };
}

[Serializable]
public sealed class NarrativePublicEventSnapshot
{
    public string factId = string.Empty;
    public string evidenceId = string.Empty;
    public string actorId = string.Empty;
    public string targetId = string.Empty;
    public string eventType = string.Empty;
    public string outcome = string.Empty;
    public bool hasDay;
    public int day;
    public int count = 1;
    public bool hasGeneration;
    public int generation;

    public NarrativePublicEventSnapshot Clone() => new()
    {
        factId = factId ?? string.Empty,
        evidenceId = evidenceId ?? string.Empty,
        actorId = actorId ?? string.Empty,
        targetId = targetId ?? string.Empty,
        eventType = eventType ?? string.Empty,
        outcome = outcome ?? string.Empty,
        hasDay = hasDay,
        day = day,
        count = count,
        hasGeneration = hasGeneration,
        generation = generation
    };
}

[Serializable]
public sealed class NarrativePublicFactSnapshot
{
    public string factId = string.Empty;
    public string domain = string.Empty;
    public string originalFactId = string.Empty;
    public string sourceSubjectId = string.Empty;
    public string subjectId = string.Empty;
    public string text = string.Empty;
    public int priority;
    public NarrativePublicFactCategory category;
    public string outcome = string.Empty;
    public bool hasLastDay;
    public int lastDay;
    public bool hasCount;
    public int count;
    public bool hasMilestoneCount;
    public int milestoneCount;
    public string totalValueDecimal = string.Empty;

    public NarrativePublicFactSnapshot Clone() => new()
    {
        factId = factId ?? string.Empty,
        domain = domain ?? string.Empty,
        originalFactId = originalFactId ?? string.Empty,
        sourceSubjectId = sourceSubjectId ?? string.Empty,
        subjectId = subjectId ?? string.Empty,
        text = text ?? string.Empty,
        priority = priority,
        category = category,
        outcome = outcome ?? string.Empty,
        hasLastDay = hasLastDay,
        lastDay = lastDay,
        hasCount = hasCount,
        count = count,
        hasMilestoneCount = hasMilestoneCount,
        milestoneCount = milestoneCount,
        totalValueDecimal = totalValueDecimal ?? string.Empty
    };
}

[Serializable]
public sealed class NarrativePublicMotifSnapshot
{
    public string stableId = string.Empty;
    public string label = string.Empty;
    public int priority;

    public NarrativePublicMotifSnapshot Clone() => new()
    {
        stableId = stableId ?? string.Empty,
        label = label ?? string.Empty,
        priority = priority
    };
}

[Serializable]
public sealed class NarrativePublicContextMaterialSnapshot
{
    public int schemaVersion = NarrativePublicContextFactory.SchemaVersion;
    public string profileId = string.Empty;
    public string subjectId = string.Empty;
    public NarrativePublicSubjectKind subjectKind;
    public string cultureStyleId = string.Empty;
    public bool requireCharacterFact;
    public bool requireMotif;
    public List<NarrativePublicEntitySnapshot> entities = new();
    public List<NarrativePublicFactSnapshot> publicFacts = new();
    public List<NarrativePublicEventSnapshot> events = new();
    public List<string> backgroundFactIds = new();
    public List<string> relationshipFactIds = new();
    public List<string> memoryFactIds = new();
    public List<string> priorHistoryFactIds = new();
    public string selectionPolicyId = string.Empty;
    public int availableFactCount;
    public int omittedFactCount;
    public List<NarrativePublicMotifSnapshot> motifs = new();
    public string semanticHash = string.Empty;

    public NarrativePublicContextMaterialSnapshot Clone() => new()
    {
        schemaVersion = schemaVersion,
        profileId = profileId ?? string.Empty,
        subjectId = subjectId ?? string.Empty,
        subjectKind = subjectKind,
        cultureStyleId = cultureStyleId ?? string.Empty,
        requireCharacterFact = requireCharacterFact,
        requireMotif = requireMotif,
        entities = (entities ?? new List<NarrativePublicEntitySnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToList(),
        publicFacts = (publicFacts ?? new List<NarrativePublicFactSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToList(),
        events = (events ?? new List<NarrativePublicEventSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToList(),
        backgroundFactIds = (backgroundFactIds ?? new List<string>()).ToList(),
        relationshipFactIds = (relationshipFactIds ?? new List<string>()).ToList(),
        memoryFactIds = (memoryFactIds ?? new List<string>()).ToList(),
        priorHistoryFactIds = (priorHistoryFactIds ?? new List<string>()).ToList(),
        selectionPolicyId = selectionPolicyId ?? string.Empty,
        availableFactCount = availableFactCount,
        omittedFactCount = omittedFactCount,
        motifs = (motifs ?? new List<NarrativePublicMotifSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToList(),
        semanticHash = semanticHash ?? string.Empty
    };
}

public sealed class NarrativePublicContextMaterial
{
    private readonly IReadOnlyList<NarrativePublicEntity> entities;
    private readonly IReadOnlyList<NarrativePublicFact> publicFacts;
    private readonly IReadOnlyList<NarrativePublicEvent> events;
    private readonly IReadOnlyList<string> backgroundFactIds;
    private readonly IReadOnlyList<string> relationshipFactIds;
    private readonly IReadOnlyList<string> memoryFactIds;
    private readonly IReadOnlyList<string> priorHistoryFactIds;

    internal NarrativePublicContextMaterial(
        string profileId,
        string subjectId,
        NarrativePublicSubjectKind subjectKind,
        IEnumerable<NarrativePublicEntity> entities,
        IEnumerable<NarrativePublicFact> publicFacts,
        IEnumerable<NarrativePublicEvent> events,
        IEnumerable<string> backgroundFactIds,
        IEnumerable<string> relationshipFactIds,
        IEnumerable<string> memoryFactIds,
        IEnumerable<string> priorHistoryFactIds,
        NarrativePublicContextSelection selection,
        NarrativeRequestContext requestContext,
        string semanticHash)
    {
        ProfileId = profileId;
        SubjectId = subjectId;
        SubjectKind = subjectKind;
        this.entities = Array.AsReadOnly((entities ?? Array.Empty<NarrativePublicEntity>()).ToArray());
        this.publicFacts = Array.AsReadOnly((publicFacts ?? Array.Empty<NarrativePublicFact>()).ToArray());
        this.events = Array.AsReadOnly((events ?? Array.Empty<NarrativePublicEvent>()).ToArray());
        this.backgroundFactIds = ReadOnlyStrings(backgroundFactIds);
        this.relationshipFactIds = ReadOnlyStrings(relationshipFactIds);
        this.memoryFactIds = ReadOnlyStrings(memoryFactIds);
        this.priorHistoryFactIds = ReadOnlyStrings(priorHistoryFactIds);
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        if (!RequestContext.IsFrozen)
            throw new ArgumentException("Public material requires a frozen request context.", nameof(requestContext));
        SemanticHash = semanticHash;
    }

    public int SchemaVersion => NarrativePublicContextFactory.SchemaVersion;
    public string ProfileId { get; }
    public string SubjectId { get; }
    public NarrativePublicSubjectKind SubjectKind { get; }
    public string SubjectKindToken => NarrativePublicContextFactory.SubjectKindToken(SubjectKind);
    public IReadOnlyList<NarrativePublicEntity> Entities => entities;
    public IReadOnlyList<NarrativePublicFact> PublicFacts => publicFacts;
    public IReadOnlyList<NarrativePublicEvent> Events => events;
    public IReadOnlyList<string> BackgroundFactIds => backgroundFactIds;
    public IReadOnlyList<string> RelationshipFactIds => relationshipFactIds;
    public IReadOnlyList<string> MemoryFactIds => memoryFactIds;
    public IReadOnlyList<string> PriorHistoryFactIds => priorHistoryFactIds;
    public NarrativePublicContextSelection Selection { get; }
    public NarrativeRequestContext RequestContext { get; }
    public string SemanticHash { get; }

    public NarrativePublicModelInput ToModelInput() =>
        NarrativePublicModelInput.Create(this);

    public string AppendToPrompt(string prompt)
    {
        return AppendToPrompt(prompt, ToModelInput());
    }

    internal string AppendToPrompt(string prompt, NarrativePublicModelInput modelInput)
    {
        if ((prompt ?? string.Empty).Contains(NarrativeRequestContext.BeginMarker))
            throw new InvalidOperationException(
                "Public material cannot append to a prompt that already contains narrative context.");
        if (modelInput == null
            || !string.Equals(modelInput.SemanticHash, SemanticHash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Public material and model input must share one semantic identity.");
        return modelInput.AppendToPrompt(RequestContext.AppendToPrompt(prompt));
    }

    public bool TryGetProjectedFactId(
        string domain,
        string originalFactId,
        string sourceSubjectId,
        out string factId)
    {
        string tuple = NarrativePublicContextFactory.BuildCanonicalFactTuple(
            domain,
            originalFactId,
            sourceSubjectId);
        NarrativePublicFact fact = publicFacts.FirstOrDefault(value =>
            string.Equals(value.CanonicalSourceTuple, tuple, StringComparison.Ordinal));
        factId = fact?.FactId ?? string.Empty;
        return fact != null;
    }

    public NarrativePublicContextMaterialSnapshot CaptureSnapshot()
    {
        return new NarrativePublicContextMaterialSnapshot
        {
            profileId = ProfileId,
            subjectId = SubjectId,
            subjectKind = SubjectKind,
            cultureStyleId = RequestContext.CultureStyleId,
            requireCharacterFact = RequestContext.RequireCharacterFact,
            requireMotif = RequestContext.RequireMotif,
            entities = entities.Select(value => new NarrativePublicEntitySnapshot
            {
                entityId = value.EntityId,
                kind = value.Kind,
                displayName = value.DisplayName
            }).ToList(),
            publicFacts = publicFacts.Select(value => new NarrativePublicFactSnapshot
            {
                factId = value.FactId,
                domain = value.Domain,
                originalFactId = value.OriginalFactId,
                sourceSubjectId = value.SourceSubjectId,
                subjectId = value.SubjectId,
                text = value.Text,
                priority = value.Priority,
                category = value.Category,
                outcome = value.Outcome,
                hasLastDay = value.LastDay.HasValue,
                lastDay = value.LastDay.GetValueOrDefault(),
                hasCount = value.Count.HasValue,
                count = value.Count.GetValueOrDefault(),
                hasMilestoneCount = value.MilestoneCount.HasValue,
                milestoneCount = value.MilestoneCount.GetValueOrDefault(),
                totalValueDecimal = value.TotalValueDecimal
            }).ToList(),
            events = events.Select(value => new NarrativePublicEventSnapshot
            {
                factId = value.FactId,
                evidenceId = value.EvidenceId,
                actorId = value.ActorId,
                targetId = value.TargetId,
                eventType = value.EventType,
                outcome = value.Outcome,
                hasDay = value.Day.HasValue,
                day = value.Day.GetValueOrDefault(),
                count = value.Count,
                hasGeneration = value.Generation.HasValue,
                generation = value.Generation.GetValueOrDefault()
            }).ToList(),
            backgroundFactIds = backgroundFactIds.ToList(),
            relationshipFactIds = relationshipFactIds.ToList(),
            memoryFactIds = memoryFactIds.ToList(),
            priorHistoryFactIds = priorHistoryFactIds.ToList(),
            selectionPolicyId = Selection.PolicyId,
            availableFactCount = Selection.AvailableFactCount,
            omittedFactCount = Selection.OmittedFactCount,
            motifs = RequestContext.Motifs.Select(value => new NarrativePublicMotifSnapshot
            {
                stableId = value.StableId,
                label = value.Label,
                priority = value.Priority
            }).ToList(),
            semanticHash = SemanticHash
        };
    }

    private static IReadOnlyList<string> ReadOnlyStrings(IEnumerable<string> values) =>
        Array.AsReadOnly((values ?? Array.Empty<string>()).ToArray());
}

public sealed class NarrativePublicPromptEnvelope
{
    private NarrativePublicPromptEnvelope(
        string prompt,
        NarrativePublicContextMaterial material,
        NarrativePublicModelInput modelInput)
    {
        Prompt = prompt;
        Material = material;
        ModelInput = modelInput;
    }

    public string Prompt { get; }
    public NarrativePublicContextMaterial Material { get; }
    public NarrativePublicModelInput ModelInput { get; }
    public string PublicContextSemanticHash => Material.SemanticHash;

    public static NarrativePublicPromptEnvelope Create(
        string basePrompt,
        NarrativePublicContextMaterial material)
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        NarrativePublicModelInput modelInput = material.ToModelInput();
        string prompt = material.AppendToPrompt(basePrompt ?? string.Empty, modelInput);
        return new NarrativePublicPromptEnvelope(prompt, material, modelInput);
    }
}

public static class NarrativePublicContextIdentity
{
    public static string Bind(string baseIdentity, string publicContextSemanticHash)
    {
        string canonicalBase = Require(baseIdentity, nameof(baseIdentity));
        string canonicalHash = Require(publicContextSemanticHash, nameof(publicContextSemanticHash));
        if (!canonicalHash.StartsWith("sha256:", StringComparison.Ordinal)
            || canonicalHash.Length != 71
            || canonicalHash.Skip(7).Any(character =>
                !(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Public-context identity requires a full sha256: semantic hash.",
                nameof(publicContextSemanticHash));
        }

        StringBuilder builder = new();
        NarrativePublicContextFactory.AppendLengthPrefixed(builder, "base", canonicalBase);
        NarrativePublicContextFactory.AppendLengthPrefixed(builder, "context", canonicalHash);
        return "narrative-context:" + NarrativeInferenceHash.ComputeSha256Utf8(builder.ToString());
    }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Narrative request identity cannot be empty.", parameterName);
        return value.Trim();
    }
}

public static class NarrativePublicContextFactory
{
    private sealed class ProfileContract
    {
        public NarrativePublicSubjectKind[] SubjectKinds;
        public bool RequiresEvent;
    }

    public const int SchemaVersion = 1;
    public const int MaximumEntities = 64;
    public const int MaximumPublicFacts = 128;
    public const int MaximumEvents = 64;

    private static readonly IReadOnlyDictionary<string, ProfileContract> Profiles =
        new Dictionary<string, ProfileContract>(StringComparer.Ordinal)
        {
            ["CharacterSkill"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Character }, RequiresEvent = true },
            ["CharacterSkillLegacyV2"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Character }, RequiresEvent = true },
            ["AcquiredTrait"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Character }, RequiresEvent = true },
            ["AcquiredTraitLegacyV2"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Character }, RequiresEvent = true },
            ["Persona"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Character }, RequiresEvent = false },
            ["EquipmentChoiceLegacyV2"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Equipment }, RequiresEvent = true },
            ["EvolutionHistory"] = new ProfileContract
                {
                    SubjectKinds = new[]
                    {
                        NarrativePublicSubjectKind.Equipment,
                        NarrativePublicSubjectKind.Facility
                    },
                    RequiresEvent = true
                },
            ["EvolutionHistoryLegacyV2"] = new ProfileContract
                {
                    SubjectKinds = new[]
                    {
                        NarrativePublicSubjectKind.Equipment,
                        NarrativePublicSubjectKind.Facility
                    },
                    RequiresEvent = true
                },
            ["FacilityEvolution"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Facility }, RequiresEvent = true },
            ["FacilityEvolutionModuleSelection"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Facility }, RequiresEvent = true },
            ["FacilityEvolutionLegacyV2"] = new ProfileContract
                { SubjectKinds = new[] { NarrativePublicSubjectKind.Facility }, RequiresEvent = true }
        };

    public static bool SupportsProfile(string profileId) =>
        !string.IsNullOrWhiteSpace(profileId)
        && Profiles.ContainsKey(profileId.Trim());

    internal static bool SupportsProfile(
        string profileId,
        NarrativePublicSubjectKind subjectKind) =>
        !string.IsNullOrWhiteSpace(profileId)
        && Profiles.TryGetValue(profileId.Trim(), out ProfileContract contract)
        && contract.SubjectKinds.Contains(subjectKind);

    public static NarrativePublicContextMaterial Build(
        string profileId,
        string subjectId,
        NarrativePublicSubjectKind subjectKind,
        string cultureOrSpecies,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<NarrativePublicEntityInput> entities,
        IEnumerable<NarrativePublicFactInput> facts,
        string selectionPolicyId,
        int maximumFacts = NarrativeRequestContext.MaximumFacts)
    {
        string profile = RequireProfile(profileId, subjectKind);
        string subject = RequireId(subjectId, nameof(subjectId));
        string policy = RequireId(selectionPolicyId, nameof(selectionPolicyId));
        if (maximumFacts <= 0 || maximumFacts > NarrativeRequestContext.MaximumFacts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFacts),
                $"Public prompt selection must be within 1..{NarrativeRequestContext.MaximumFacts} facts.");
        }

        NarrativePublicEntity[] normalizedEntities = NormalizeEntities(entities);
        NarrativePublicEntity subjectEntity = normalizedEntities.FirstOrDefault(value =>
            string.Equals(value.EntityId, subject, StringComparison.Ordinal));
        if (subjectEntity == null
            || !EntityMatchesSubject(subjectEntity.Kind, subjectKind))
        {
            throw new InvalidOperationException(
                $"Public narrative subject '{subject}' is missing or has the wrong entity kind.");
        }

        NarrativePublicFact[] available = NormalizeFacts(facts, normalizedEntities);
        if (available.Length == 0)
            throw new InvalidOperationException("Public narrative context requires at least one public fact.");
        NarrativePublicFact[] selected = available
            .OrderByDescending(value => value.Priority)
            .ThenBy(value => value.CanonicalSourceTuple, StringComparer.Ordinal)
            .Take(maximumFacts)
            .ToArray();
        NarrativePublicEvent[] selectedEvents = BuildEvents(selected, normalizedEntities);
        if (Profiles[profile].RequiresEvent && selectedEvents.Length == 0)
        {
            throw new InvalidOperationException(
                $"Narrative profile '{profile}' requires at least one readable public event.");
        }

        NarrativePublicContextSelection selection = new(
            policy,
            available.Length,
            available.Length - selected.Length);
        NarrativeRequestContext requestContext = NarrativeCultureStyleCatalog.Create(
            profile,
            cultureOrSpecies,
            requireCharacterFact,
            requireMotif);
        foreach (NarrativePublicFact fact in selected)
            requestContext.AddFact(fact.FactId, fact.Text, fact.Priority);
        requestContext.Freeze();

        return CreateMaterial(
            profile,
            subject,
            subjectKind,
            normalizedEntities,
            selected,
            selectedEvents,
            selection,
            requestContext);
    }

    public static NarrativePublicContextMaterial Restore(
        NarrativePublicContextMaterialSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.schemaVersion != SchemaVersion)
            throw new InvalidOperationException(
                $"Unsupported public narrative context snapshot version {snapshot.schemaVersion}.");

        string profile = RequireProfile(snapshot.profileId, snapshot.subjectKind);
        string subject = RequireId(snapshot.subjectId, nameof(snapshot.subjectId));
        NarrativePublicEntity[] entities = NormalizeEntities(
            (snapshot.entities ?? new List<NarrativePublicEntitySnapshot>())
                .Where(value => value != null)
                .Select(value => new NarrativePublicEntityInput(
                    value.entityId,
                    value.kind,
                    value.displayName)));
        NarrativePublicEntity subjectEntity = entities.FirstOrDefault(value =>
            string.Equals(value.EntityId, subject, StringComparison.Ordinal));
        if (subjectEntity == null
            || !EntityMatchesSubject(subjectEntity.Kind, snapshot.subjectKind))
        {
            throw new InvalidOperationException(
                $"Restored public narrative subject '{subject}' is unresolved.");
        }

        NarrativePublicFactSnapshot[] factSnapshots =
            (snapshot.publicFacts ?? new List<NarrativePublicFactSnapshot>())
            .Where(value => value != null).ToArray();
        Dictionary<string, NarrativePublicEventSnapshot> eventByFact =
            (snapshot.events ?? new List<NarrativePublicEventSnapshot>())
            .Where(value => value != null)
            .ToDictionary(value => value.factId ?? string.Empty, StringComparer.Ordinal);
        NarrativePublicFact[] facts = NormalizeFacts(factSnapshots.Select(value =>
        {
            eventByFact.TryGetValue(value.factId ?? string.Empty, out NarrativePublicEventSnapshot savedEvent);
            NarrativePublicEventInput eventData = savedEvent == null
                ? null
                : new NarrativePublicEventInput(
                    savedEvent.evidenceId,
                    savedEvent.actorId,
                    savedEvent.targetId,
                    savedEvent.eventType,
                    savedEvent.outcome,
                    savedEvent.hasDay ? savedEvent.day : null,
                    savedEvent.count,
                    savedEvent.hasGeneration ? savedEvent.generation : null);
            decimal? total = ParseOptionalDecimal(value.totalValueDecimal);
            return new NarrativePublicFactInput(
                value.domain,
                value.originalFactId,
                value.sourceSubjectId,
                value.text,
                value.priority,
                value.category,
                value.subjectId,
                value.outcome,
                value.hasLastDay ? value.lastDay : null,
                value.hasCount ? value.count : null,
                value.hasMilestoneCount ? value.milestoneCount : null,
                total,
                eventData);
        }), entities);
        for (int index = 0; index < facts.Length; index++)
        {
            if (!string.Equals(
                    facts[index].FactId,
                    factSnapshots[index].factId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Restored public fact ID does not match its source tuple.");
            }
        }

        string policy = RequireId(snapshot.selectionPolicyId, nameof(snapshot.selectionPolicyId));
        if (snapshot.availableFactCount != facts.Length + snapshot.omittedFactCount
            || snapshot.omittedFactCount < 0)
        {
            throw new InvalidOperationException(
                "Restored public fact selection counts do not reconcile.");
        }
        NarrativePublicEvent[] events = BuildEvents(facts, entities);
        if (Profiles[profile].RequiresEvent && events.Length == 0)
            throw new InvalidOperationException(
                $"Restored narrative profile '{profile}' has no readable event.");

        NarrativeRequestContext requestContext = new(
            profile,
            snapshot.cultureStyleId,
            snapshot.requireCharacterFact,
            snapshot.requireMotif);
        foreach (NarrativePublicFact fact in facts)
            requestContext.AddFact(fact.FactId, fact.Text, fact.Priority);
        foreach (NarrativePublicMotifSnapshot motif in
                 snapshot.motifs ?? new List<NarrativePublicMotifSnapshot>())
        {
            if (motif == null) continue;
            requestContext.AddMotif(motif.stableId, motif.label, motif.priority);
        }
        requestContext.Freeze();

        NarrativePublicContextMaterial material = CreateMaterial(
            profile,
            subject,
            snapshot.subjectKind,
            entities,
            facts,
            events,
            new NarrativePublicContextSelection(
                policy,
                snapshot.availableFactCount,
                snapshot.omittedFactCount),
            requestContext);
        RequireRestoredReferenceGroup(
            "backgroundFactIds",
            snapshot.backgroundFactIds,
            material.BackgroundFactIds);
        RequireRestoredReferenceGroup(
            "relationshipFactIds",
            snapshot.relationshipFactIds,
            material.RelationshipFactIds);
        RequireRestoredReferenceGroup(
            "memoryFactIds",
            snapshot.memoryFactIds,
            material.MemoryFactIds);
        RequireRestoredReferenceGroup(
            "priorHistoryFactIds",
            snapshot.priorHistoryFactIds,
            material.PriorHistoryFactIds);
        if (!string.Equals(material.SemanticHash, snapshot.semanticHash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Restored public narrative context semantic hash does not match its snapshot.");
        return material;
    }

    private static void RequireRestoredReferenceGroup(
        string fieldName,
        IEnumerable<string> snapshotValues,
        IEnumerable<string> computedValues)
    {
        string[] saved = (snapshotValues ?? Array.Empty<string>()).ToArray();
        string[] computed = (computedValues ?? Array.Empty<string>()).ToArray();
        if (!saved.SequenceEqual(computed, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Restored public narrative reference group '{fieldName}' does not match its facts.");
        }
    }

    public static string BuildProjectedFactId(
        string domain,
        string originalFactId,
        string sourceSubjectId)
    {
        return "public-fact:" + NarrativeInferenceHash.ComputeSha256Utf8(
            BuildCanonicalFactTuple(domain, originalFactId, sourceSubjectId));
    }

    internal static string BuildCanonicalFactTuple(
        string domain,
        string originalFactId,
        string sourceSubjectId)
    {
        string canonicalDomain = RequireComponent(domain, nameof(domain), 128, allowEmpty: false);
        string canonicalFact = RequireComponent(
            originalFactId,
            nameof(originalFactId),
            512,
            allowEmpty: false);
        string canonicalSubject = RequireComponent(
            sourceSubjectId,
            nameof(sourceSubjectId),
            512,
            allowEmpty: true);
        StringBuilder builder = new();
        AppendLengthPrefixed(builder, "domain", canonicalDomain);
        AppendLengthPrefixed(builder, "originalFactId", canonicalFact);
        AppendLengthPrefixed(builder, "subjectId", canonicalSubject);
        return builder.ToString();
    }

    internal static void AppendLengthPrefixed(
        StringBuilder builder,
        string field,
        string value)
    {
        string normalized = value ?? string.Empty;
        builder.Append(field).Append('#')
            .Append(Encoding.UTF8.GetByteCount(normalized).ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(normalized).Append(';');
    }

    internal static string SubjectKindToken(NarrativePublicSubjectKind kind) => kind switch
    {
        NarrativePublicSubjectKind.Character => "character",
        NarrativePublicSubjectKind.Equipment => "equipment",
        NarrativePublicSubjectKind.Facility => "facility",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    internal static string EntityKindToken(NarrativePublicEntityKind kind) => kind switch
    {
        NarrativePublicEntityKind.Character => "character",
        NarrativePublicEntityKind.Equipment => "equipment",
        NarrativePublicEntityKind.Facility => "facility",
        NarrativePublicEntityKind.Place => "place",
        NarrativePublicEntityKind.Group => "group",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static NarrativePublicContextMaterial CreateMaterial(
        string profile,
        string subject,
        NarrativePublicSubjectKind subjectKind,
        NarrativePublicEntity[] entities,
        NarrativePublicFact[] facts,
        NarrativePublicEvent[] events,
        NarrativePublicContextSelection selection,
        NarrativeRequestContext requestContext)
    {
        if (requestContext == null || !requestContext.IsFrozen)
            throw new InvalidOperationException(
                "Public narrative material requires its exact frozen request context.");
        if (requestContext.RequireCharacterFact && facts.Length == 0)
            throw new InvalidOperationException(
                "Public narrative material requires at least one selectable fact.");
        if (requestContext.RequireMotif && requestContext.Motifs.Count == 0)
            throw new InvalidOperationException(
                $"Narrative profile '{profile}' requires a resolved public culture motif.");
        string[] background = ReferenceGroup(facts, NarrativePublicFactCategory.Background);
        string[] relationships = ReferenceGroup(facts, NarrativePublicFactCategory.Relationship);
        string[] memories = ReferenceGroup(facts, NarrativePublicFactCategory.Memory);
        string[] priorHistory = ReferenceGroup(facts, NarrativePublicFactCategory.PriorHistory);
        string semanticHash = ComputeSemanticHash(
            profile,
            subject,
            subjectKind,
            entities,
            facts,
            events,
            background,
            relationships,
            memories,
            priorHistory,
            selection,
            requestContext);
        return new NarrativePublicContextMaterial(
            profile,
            subject,
            subjectKind,
            entities,
            facts,
            events,
            background,
            relationships,
            memories,
            priorHistory,
            selection,
            requestContext,
            semanticHash);
    }

    private static NarrativePublicEntity[] NormalizeEntities(
        IEnumerable<NarrativePublicEntityInput> source)
    {
        Dictionary<string, NarrativePublicEntity> byId = new(StringComparer.Ordinal);
        foreach (NarrativePublicEntityInput input in source ?? Array.Empty<NarrativePublicEntityInput>())
        {
            if (input == null)
                throw new InvalidOperationException("Public entity input cannot contain null.");
            string id = RequireId(input.EntityId, nameof(input.EntityId));
            string displayName = RequireText(input.DisplayName, nameof(input.DisplayName), 120);
            NarrativePublicEntity entity = new(id, input.Kind, displayName);
            if (byId.TryGetValue(id, out NarrativePublicEntity duplicate))
            {
                if (duplicate.Kind != entity.Kind
                    || !string.Equals(duplicate.DisplayName, entity.DisplayName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Public entity '{id}' has conflicting definitions.");
                }
                continue;
            }
            byId.Add(id, entity);
        }
        if (byId.Count == 0 || byId.Count > MaximumEntities)
            throw new InvalidOperationException(
                $"Public narrative context requires 1..{MaximumEntities} resolved entities.");
        return byId.Values.OrderBy(value => value.EntityId, StringComparer.Ordinal).ToArray();
    }

    private static NarrativePublicFact[] NormalizeFacts(
        IEnumerable<NarrativePublicFactInput> source,
        IReadOnlyCollection<NarrativePublicEntity> entities)
    {
        Dictionary<string, string> tupleByProjectedId = new(StringComparer.Ordinal);
        HashSet<string> sourceTuples = new(StringComparer.Ordinal);
        HashSet<string> entityIds = entities.Select(value => value.EntityId)
            .ToHashSet(StringComparer.Ordinal);
        List<NarrativePublicFact> result = new();
        foreach (NarrativePublicFactInput input in source ?? Array.Empty<NarrativePublicFactInput>())
        {
            if (input == null)
                throw new InvalidOperationException("Public fact input cannot contain null.");
            string tuple = BuildCanonicalFactTuple(
                input.Domain,
                input.OriginalFactId,
                input.SourceSubjectId);
            if (!sourceTuples.Add(tuple))
                throw new InvalidOperationException("Duplicate public fact source tuple.");
            string factId = "public-fact:" + NarrativeInferenceHash.ComputeSha256Utf8(tuple);
            if (tupleByProjectedId.TryGetValue(factId, out string existingTuple)
                && !string.Equals(existingTuple, tuple, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Distinct public fact source tuples produced one ID '{factId}'.");
            }
            tupleByProjectedId[factId] = tuple;

            string publicSubject = OptionalId(input.PublicSubjectId, nameof(input.PublicSubjectId));
            if (publicSubject.Length > 0 && !entityIds.Contains(publicSubject))
                throw new InvalidOperationException(
                    $"Public fact '{factId}' references unresolved subject '{publicSubject}'.");
            if (input.LastDay < 0 || input.Count < 0 || input.MilestoneCount < 0)
                throw new InvalidOperationException("Public fact numeric metadata cannot be negative.");
            const NarrativePublicFactCategory knownCategories =
                NarrativePublicFactCategory.Background
                | NarrativePublicFactCategory.Relationship
                | NarrativePublicFactCategory.Memory
                | NarrativePublicFactCategory.PriorHistory;
            if ((input.Category & ~knownCategories) != 0)
                throw new InvalidOperationException(
                    "Public fact contains an unknown reference category.");
            string outcome = RequireComponent(input.Outcome, nameof(input.Outcome), 512, allowEmpty: true);
            NarrativePublicEventInput eventData = NormalizeEventInput(input.EventData, entityIds);
            if (eventData != null
                && !string.Equals(publicSubject, eventData.TargetId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Public fact '{factId}' subject does not match its event target.");
            }
            if (eventData != null
                && !string.Equals(outcome, eventData.Outcome, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Public fact '{factId}' outcome does not match its event outcome.");
            }
            if (eventData != null
                && (input.LastDay != eventData.Day || input.Count != eventData.Count))
            {
                throw new InvalidOperationException(
                    $"Public fact '{factId}' time/count metadata does not match its event.");
            }

            result.Add(new NarrativePublicFact(
                factId,
                RequireComponent(input.Domain, nameof(input.Domain), 128, allowEmpty: false),
                RequireComponent(input.OriginalFactId, nameof(input.OriginalFactId), 512, allowEmpty: false),
                RequireComponent(input.SourceSubjectId, nameof(input.SourceSubjectId), 512, allowEmpty: true),
                publicSubject,
                RequireText(input.Text, nameof(input.Text), 2000),
                input.Priority,
                input.Category,
                outcome,
                input.LastDay,
                input.Count,
                input.MilestoneCount,
                input.TotalValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                eventData,
                tuple));
        }
        return result.ToArray();
    }

    private static NarrativePublicEventInput NormalizeEventInput(
        NarrativePublicEventInput input,
        ISet<string> entityIds)
    {
        if (input == null) return null;
        string actor = OptionalId(input.ActorId, nameof(input.ActorId));
        string target = OptionalId(input.TargetId, nameof(input.TargetId));
        if ((actor.Length > 0 && !entityIds.Contains(actor))
            || (target.Length > 0 && !entityIds.Contains(target)))
        {
            throw new InvalidOperationException(
                "Public event contains an unresolved actor or target entity.");
        }
        if (input.Count <= 0 || input.Day < 0 || input.Generation < 0)
            throw new InvalidOperationException(
                "Public event count must be positive and optional time metadata cannot be negative.");
        return new NarrativePublicEventInput(
            RequireComponent(input.EvidenceId, nameof(input.EvidenceId), 512, allowEmpty: true),
            actor,
            target,
            RequireId(input.EventType, nameof(input.EventType)),
            RequireComponent(input.Outcome, nameof(input.Outcome), 512, allowEmpty: true),
            input.Day,
            input.Count,
            input.Generation);
    }

    private static NarrativePublicEvent[] BuildEvents(
        IEnumerable<NarrativePublicFact> facts,
        IReadOnlyCollection<NarrativePublicEntity> entities)
    {
        HashSet<string> entityIds = entities.Select(value => value.EntityId)
            .ToHashSet(StringComparer.Ordinal);
        NarrativePublicEvent[] result = facts
            .Where(value => value.EventData != null)
            .Select(value =>
            {
                NarrativePublicEventInput input = value.EventData;
                if ((input.ActorId.Length > 0 && !entityIds.Contains(input.ActorId))
                    || (input.TargetId.Length > 0 && !entityIds.Contains(input.TargetId)))
                {
                    throw new InvalidOperationException(
                        $"Public event for '{value.FactId}' has an orphan participant.");
                }
                return new NarrativePublicEvent(
                    value.FactId,
                    input.EvidenceId,
                    input.ActorId,
                    input.TargetId,
                    input.EventType,
                    input.Outcome,
                    input.Day,
                    input.Count,
                    input.Generation);
            })
            .ToArray();
        if (result.Length > MaximumEvents)
            throw new InvalidOperationException(
                $"Public event selection exceeds the schema limit of {MaximumEvents}.");
        return result;
    }

    private static string ComputeSemanticHash(
        string profile,
        string subject,
        NarrativePublicSubjectKind subjectKind,
        IEnumerable<NarrativePublicEntity> entities,
        IEnumerable<NarrativePublicFact> facts,
        IEnumerable<NarrativePublicEvent> events,
        IEnumerable<string> background,
        IEnumerable<string> relationships,
        IEnumerable<string> memories,
        IEnumerable<string> priorHistory,
        NarrativePublicContextSelection selection,
        NarrativeRequestContext requestContext)
    {
        StringBuilder builder = new(8192);
        AppendScalar(builder, "schemaVersion", SchemaVersion);
        AppendLengthPrefixed(builder, "profileId", profile);
        AppendLengthPrefixed(builder, "subjectId", subject);
        AppendLengthPrefixed(builder, "subjectKind", SubjectKindToken(subjectKind));
        AppendLengthPrefixed(builder, "cultureStyleId", requestContext.CultureStyleId);
        AppendScalar(builder, "requireCharacterFact", requestContext.RequireCharacterFact ? 1 : 0);
        AppendScalar(builder, "requireMotif", requestContext.RequireMotif ? 1 : 0);
        AppendCollection(builder, "entities", entities, (target, value) =>
        {
            AppendLengthPrefixed(target, "entityId", value.EntityId);
            AppendLengthPrefixed(target, "kind", value.KindToken);
            AppendLengthPrefixed(target, "displayName", value.DisplayName);
        });
        AppendCollection(builder, "facts", facts, (target, value) =>
        {
            AppendLengthPrefixed(target, "factId", value.FactId);
            AppendLengthPrefixed(target, "domain", value.Domain);
            AppendLengthPrefixed(target, "originalFactId", value.OriginalFactId);
            AppendLengthPrefixed(target, "sourceSubjectId", value.SourceSubjectId);
            AppendLengthPrefixed(target, "subjectId", value.SubjectId);
            AppendLengthPrefixed(target, "text", value.Text);
            AppendScalar(target, "priority", value.Priority);
            AppendScalar(target, "category", (int)value.Category);
            AppendLengthPrefixed(target, "outcome", value.Outcome);
            AppendNullable(target, "lastDay", value.LastDay);
            AppendNullable(target, "count", value.Count);
            AppendNullable(target, "milestoneCount", value.MilestoneCount);
            AppendLengthPrefixed(target, "totalValueDecimal", value.TotalValueDecimal);
        });
        AppendCollection(builder, "events", events, (target, value) =>
        {
            AppendLengthPrefixed(target, "factId", value.FactId);
            AppendLengthPrefixed(target, "evidenceId", value.EvidenceId);
            AppendLengthPrefixed(target, "actorId", value.ActorId);
            AppendLengthPrefixed(target, "targetId", value.TargetId);
            AppendLengthPrefixed(target, "eventType", value.EventType);
            AppendLengthPrefixed(target, "outcome", value.Outcome);
            AppendNullable(target, "day", value.Day);
            AppendScalar(target, "count", value.Count);
            AppendNullable(target, "generation", value.Generation);
        });
        AppendStrings(builder, "backgroundFactIds", background);
        AppendStrings(builder, "relationshipFactIds", relationships);
        AppendStrings(builder, "memoryFactIds", memories);
        AppendStrings(builder, "priorHistoryFactIds", priorHistory);
        AppendLengthPrefixed(builder, "selectionPolicyId", selection.PolicyId);
        AppendScalar(builder, "availableFactCount", selection.AvailableFactCount);
        AppendScalar(builder, "omittedFactCount", selection.OmittedFactCount);
        AppendCollection(builder, "motifs", requestContext.Motifs, (target, value) =>
        {
            AppendLengthPrefixed(target, "stableId", value.StableId);
            AppendLengthPrefixed(target, "label", value.Label);
            AppendScalar(target, "priority", value.Priority);
        });
        return NarrativeInferenceHash.ComputeSha256Utf8(builder.ToString());
    }

    private static void AppendCollection<T>(
        StringBuilder builder,
        string name,
        IEnumerable<T> source,
        Action<StringBuilder, T> append)
    {
        T[] values = (source ?? Array.Empty<T>()).ToArray();
        AppendScalar(builder, name + ".count", values.Length);
        foreach (T value in values) append(builder, value);
    }

    private static void AppendStrings(
        StringBuilder builder,
        string name,
        IEnumerable<string> values) =>
        AppendCollection(builder, name, values, (target, value) =>
            AppendLengthPrefixed(target, "value", value));

    private static void AppendScalar(StringBuilder builder, string name, int value) =>
        AppendLengthPrefixed(builder, name, value.ToString(CultureInfo.InvariantCulture));

    private static void AppendNullable(StringBuilder builder, string name, int? value)
    {
        AppendScalar(builder, name + ".hasValue", value.HasValue ? 1 : 0);
        if (value.HasValue) AppendScalar(builder, name, value.Value);
    }

    private static string[] ReferenceGroup(
        IEnumerable<NarrativePublicFact> facts,
        NarrativePublicFactCategory category) =>
        facts.Where(value => (value.Category & category) != 0)
            .Select(value => value.FactId)
            .ToArray();

    private static string RequireProfile(
        string profileId,
        NarrativePublicSubjectKind subjectKind)
    {
        string profile = RequireComponent(profileId, nameof(profileId), 64, allowEmpty: false);
        if (!Profiles.TryGetValue(profile, out ProfileContract contract))
            throw new ArgumentException(
                $"Narrative profile '{profile}' has no public-context contract.",
                nameof(profileId));
        if (contract.SubjectKinds == null || !contract.SubjectKinds.Contains(subjectKind))
            throw new ArgumentException(
                $"Narrative profile '{profile}' does not allow subject kind '{SubjectKindToken(subjectKind)}'.",
                nameof(subjectKind));
        return profile;
    }

    private static bool EntityMatchesSubject(
        NarrativePublicEntityKind entityKind,
        NarrativePublicSubjectKind subjectKind) =>
        (entityKind == NarrativePublicEntityKind.Character
            && subjectKind == NarrativePublicSubjectKind.Character)
        || (entityKind == NarrativePublicEntityKind.Equipment
            && subjectKind == NarrativePublicSubjectKind.Equipment)
        || (entityKind == NarrativePublicEntityKind.Facility
            && subjectKind == NarrativePublicSubjectKind.Facility);

    private static string RequireId(string value, string parameterName)
    {
        string normalized = RequireComponent(value, parameterName, 512, allowEmpty: false);
        if (normalized.Any(char.IsWhiteSpace))
            throw new ArgumentException("Public narrative identifiers cannot contain whitespace.", parameterName);
        return normalized;
    }

    private static string OptionalId(string value, string parameterName)
    {
        string normalized = RequireComponent(value, parameterName, 512, allowEmpty: true);
        if (normalized.Any(char.IsWhiteSpace))
            throw new ArgumentException("Public narrative identifiers cannot contain whitespace.", parameterName);
        return normalized;
    }

    private static string RequireText(string value, string parameterName, int maximumLength)
    {
        string normalized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw new ArgumentException(
                $"Public narrative text must contain 1..{maximumLength} characters.",
                parameterName);
        return normalized;
    }

    private static string RequireComponent(
        string value,
        string parameterName,
        int maximumLength,
        bool allowEmpty)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if ((!allowEmpty && normalized.Length == 0) || normalized.Length > maximumLength)
            throw new ArgumentException(
                $"Public narrative value must contain {(allowEmpty ? 0 : 1)}..{maximumLength} characters.",
                parameterName);
        return normalized;
    }

    private static decimal? ParseOptionalDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal parsed))
        {
            throw new InvalidOperationException(
                "Restored public fact contains a non-canonical decimal value.");
        }
        return parsed;
    }
}
