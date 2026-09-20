using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public static class NarrativeInferenceHash
{
    public static string ComputeSha256Utf8(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        StringBuilder builder = new StringBuilder(hash.Length * 2);
        for (int index = 0; index < hash.Length; index++)
        {
            builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
        }
        return "sha256:" + builder;
    }
}

public enum NarrativeInferenceTimeAuthority
{
    GameTick,
    Utc
}

public readonly struct NarrativeInferenceTimestamp
{
    private NarrativeInferenceTimestamp(
        NarrativeInferenceTimeAuthority authority,
        long gameTick,
        string utcTimestamp)
    {
        Authority = authority;
        GameTick = gameTick;
        UtcTimestamp = utcTimestamp ?? string.Empty;
    }

    public NarrativeInferenceTimeAuthority Authority { get; }
    public long GameTick { get; }
    public string UtcTimestamp { get; }
    public bool IsValid => Authority == NarrativeInferenceTimeAuthority.GameTick
        ? GameTick >= 0
        : DateTime.TryParseExact(
            UtcTimestamp,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTime parsed)
            && parsed.Kind == DateTimeKind.Utc;

    public static NarrativeInferenceTimestamp FromGameTick(long gameTick)
    {
        if (gameTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameTick));
        }
        return new NarrativeInferenceTimestamp(
            NarrativeInferenceTimeAuthority.GameTick,
            gameTick,
            string.Empty);
    }

    public static NarrativeInferenceTimestamp FromUtc(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Narrative inference UTC timestamps must use DateTimeKind.Utc.",
                nameof(utc));
        }
        return new NarrativeInferenceTimestamp(
            NarrativeInferenceTimeAuthority.Utc,
            -1,
            utc.ToString("O", CultureInfo.InvariantCulture));
    }
}

public readonly struct NarrativeInferenceAuditRecord
{
    public NarrativeInferenceAuditRecord(
        string profileId,
        string requestKey,
        string candidatePacketHash,
        bool succeeded,
        string validationError,
        bool fallbackUsed,
        string fallbackReason,
        string selectedId,
        int selectedIndex,
        string targetPersistentId,
        NarrativeInferenceTimestamp timestamp)
    {
        ProfileId = Require(profileId, nameof(profileId));
        RequestKey = Require(requestKey, nameof(requestKey));
        CandidatePacketHash = Require(candidatePacketHash, nameof(candidatePacketHash));
        if (!timestamp.IsValid)
        {
            throw new ArgumentException(
                "Narrative inference audit requires a valid game-tick or UTC timestamp.",
                nameof(timestamp));
        }
        if (fallbackUsed && string.IsNullOrWhiteSpace(fallbackReason))
        {
            throw new ArgumentException(
                "A narrative inference fallback must expose its reason.",
                nameof(fallbackReason));
        }

        Succeeded = succeeded;
        ValidationError = validationError?.Trim() ?? string.Empty;
        FallbackUsed = fallbackUsed;
        FallbackReason = fallbackReason?.Trim() ?? string.Empty;
        SelectedId = selectedId?.Trim() ?? string.Empty;
        SelectedIndex = selectedIndex >= 0 ? selectedIndex : -1;
        TargetPersistentId = targetPersistentId?.Trim() ?? string.Empty;
        Timestamp = timestamp;
    }

    public string ProfileId { get; }
    public string RequestKey { get; }
    public string CandidatePacketHash { get; }
    public bool Succeeded { get; }
    public string ValidationError { get; }
    public bool FallbackUsed { get; }
    public string FallbackReason { get; }
    public string SelectedId { get; }
    public int SelectedIndex { get; }
    public bool HasSelectedIndex => SelectedIndex >= 0;
    public string TargetPersistentId { get; }
    public NarrativeInferenceTimestamp Timestamp { get; }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Narrative inference audit identifiers cannot be empty.",
                parameterName);
        }
        return value.Trim();
    }
}

public static class NarrativeExactKeyContract
{
    private static readonly string[] CharacterSkillRootKeys =
    {
        "presentationId", "displayName", "narrativeFlavor"
    };
    private static readonly string[] CharacterSkillModuleSelectionBatchRootKeys =
    {
        "candidates"
    };
    private static readonly string[] ModuleSelectionItemKeys =
    {
        "selectionId", "positiveModuleIds", "drawbackModuleIds", "evidenceFactIds",
        "displayName", "narrativeFlavor"
    };
    private static readonly string[] CharacterSkillLegacyRootKeys = { "candidates" };
    private static readonly string[] CharacterSkillLegacyCandidateKeys =
    {
        "ruleId", "combinationId", "displayName", "description", "narrativeReason"
    };
    private static readonly string[] PresentationRootKeys =
    {
        "presentationId", "displayName", "narrativeFlavor"
    };
    private static readonly string[] FacilityEvolutionLegacyRootKeys =
    {
        "proposalIds", "mutationTags", "reasons", "flavorText", "confidence"
    };
    private static readonly string[] FacilityEvolutionReasonKeys = { "proposalId", "reason" };
    private static readonly string[] EquipmentChoiceLegacyRootKeys = { "selectedIndex" };
    private static readonly string[] EvolutionHistoryLegacyRootKeys =
    {
        "requestKey", "targetPersistentId", "nodeId", "parentNodeId", "effectId",
        "effectBudget", "evidenceIds", "displayName", "description", "historyReason"
    };
    private static readonly string[] AcquiredTraitLegacyRootKeys =
    {
        "combinationId", "displayName", "description", "narrativeReason", "evidenceFactIds"
    };
    private static readonly string[] PersonaRootKeys = { "personaName", "flavorText" };

    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> RootKeysByProfile =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["CharacterSkill"] = CharacterSkillRootKeys,
            ["CharacterSkillModuleSelection"] = CharacterSkillModuleSelectionBatchRootKeys,
            ["CharacterSkillLegacyV2"] = CharacterSkillLegacyRootKeys,
            ["FacilityEvolution"] = PresentationRootKeys,
            ["FacilityEvolutionModuleSelection"] = ModuleSelectionItemKeys,
            ["FacilityEvolutionLegacyV2"] = FacilityEvolutionLegacyRootKeys,
            ["EquipmentChoiceLegacyV2"] = EquipmentChoiceLegacyRootKeys,
            ["EquipmentEvolutionModuleSelection"] = ModuleSelectionItemKeys,
            ["EvolutionHistory"] = PresentationRootKeys,
            ["EvolutionHistoryLegacyV2"] = EvolutionHistoryLegacyRootKeys,
            ["AcquiredTrait"] = PresentationRootKeys,
            ["AcquiredTraitModuleSelection"] = ModuleSelectionItemKeys,
            ["AcquiredTraitLegacyV2"] = AcquiredTraitLegacyRootKeys,
            ["Persona"] = PersonaRootKeys
        };

    public static bool IsRegisteredProfile(string profileId)
    {
        return !string.IsNullOrWhiteSpace(profileId)
            && RootKeysByProfile.ContainsKey(profileId.Trim());
    }

    public static bool AllowsNarrativeReferenceKeys(string profileId) =>
        !IsRegisteredProfile(profileId);

    public static bool TryValidateProfileResponse(
        string profileId,
        string response,
        out string json,
        out string error)
    {
        json = string.Empty;
        error = string.Empty;
        string normalizedProfile = profileId?.Trim() ?? string.Empty;
        if (!RootKeysByProfile.TryGetValue(
                normalizedProfile,
                out IReadOnlyCollection<string> expectedKeys))
        {
            error = $"Narrative response profile '{normalizedProfile}' has no exact-key contract.";
            return false;
        }

        if (!StrictJsonReader.TryReadSingleObject(response, out json, out _, out error)
            || !TryValidateExactObject(json, expectedKeys, out error))
        {
            error = $"{normalizedProfile}: {error}";
            return false;
        }

        if (string.Equals(normalizedProfile, "FacilityEvolutionLegacyV2", StringComparison.Ordinal))
        {
            return TryValidateExactArrayObjects(
                json,
                "reasons",
                FacilityEvolutionReasonKeys,
                "FacilityEvolutionLegacyV2",
                out error);
        }

        if (string.Equals(normalizedProfile, "CharacterSkillLegacyV2", StringComparison.Ordinal))
        {
            return TryValidateExactArrayObjects(
                json,
                "candidates",
                CharacterSkillLegacyCandidateKeys,
                "CharacterSkillLegacyV2",
                out error);
        }
        if (string.Equals(
                normalizedProfile,
                "CharacterSkillModuleSelection",
                StringComparison.Ordinal))
        {
            return TryValidateExactArrayObjects(
                json,
                "candidates",
                ModuleSelectionItemKeys,
                "CharacterSkillModuleSelection",
                out error);
        }
        return true;
    }

    public static bool TryValidateModuleSelectionItem(
        string response,
        out string json,
        out string error)
    {
        json = string.Empty;
        error = string.Empty;
        if (!StrictJsonReader.TryReadSingleObject(
                response,
                out json,
                out _,
                out error)
            || !TryValidateExactObject(json, ModuleSelectionItemKeys, out error))
        {
            error = "CharacterSkillModuleSelection item: " + error;
            return false;
        }
        return true;
    }

    private static bool TryValidateExactArrayObjects(
        string json,
        string propertyName,
        IReadOnlyCollection<string> expectedKeys,
        string profileId,
        out string error)
    {
        if (!StrictJsonReader.TryGetProperty(json, propertyName, out string arrayJson, out error)
            || !StrictJsonReader.TryReadArrayElements(arrayJson, out List<string> elements, out error))
        {
            error = $"{profileId}: {propertyName} must be a JSON array. {error}";
            return false;
        }
        for (int index = 0; index < elements.Count; index++)
        {
            if (!TryValidateExactObject(elements[index], expectedKeys, out error))
            {
                error = $"{profileId}: {propertyName}[{index}] {error}";
                return false;
            }
        }
        return true;
    }

    public static bool TryValidateExactObject(
        string json,
        IReadOnlyCollection<string> expectedKeys,
        out string error)
    {
        error = string.Empty;
        if (expectedKeys == null)
        {
            error = "Expected key collection is missing.";
            return false;
        }
        if (!StrictJsonReader.TryReadSingleObject(
                json,
                out _,
                out Dictionary<string, string> properties,
                out error))
        {
            return false;
        }

        HashSet<string> expected = new HashSet<string>(expectedKeys, StringComparer.Ordinal);
        if (expected.Count != expectedKeys.Count)
        {
            error = "Expected key collection contains a duplicate.";
            return false;
        }
        string[] missing = expected.Where(key => !properties.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        string[] extra = properties.Keys.Where(key => !expected.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0 || extra.Length > 0)
        {
            error = "object keys differ from the exact contract"
                + $"; missing=[{string.Join(",", missing)}]"
                + $"; extra=[{string.Join(",", extra)}].";
            return false;
        }
        return true;
    }

    internal static bool TryReadIntegerProperty(
        string json,
        string propertyName,
        out int value,
        out string error)
    {
        value = -1;
        if (!StrictJsonReader.TryGetProperty(json, propertyName, out string raw, out error)
            || !int.TryParse(
                raw,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value))
        {
            error = string.IsNullOrWhiteSpace(error)
                ? $"{propertyName} must be a JSON integer."
                : error;
            value = -1;
            return false;
        }
        return true;
    }

    private sealed class StrictJsonReader
    {
        private readonly string source;
        private int position;

        private StrictJsonReader(string source)
        {
            this.source = source ?? string.Empty;
        }

        public static bool TryReadSingleObject(
            string source,
            out string json,
            out Dictionary<string, string> properties,
            out string error)
        {
            json = string.Empty;
            properties = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                error = "JSON response is empty.";
                return false;
            }

            StrictJsonReader reader = new StrictJsonReader(source);
            reader.SkipWhitespace();
            int start = reader.position;
            if (!reader.TryReadObject(out properties, out error))
            {
                return false;
            }
            int end = reader.position;
            reader.SkipWhitespace();
            if (reader.position != reader.source.Length)
            {
                error = "JSON response contains text outside the single object.";
                return false;
            }
            json = reader.source.Substring(start, end - start);
            return true;
        }

        public static bool TryGetProperty(
            string json,
            string propertyName,
            out string rawValue,
            out string error)
        {
            rawValue = string.Empty;
            if (!TryReadSingleObject(json, out _, out Dictionary<string, string> values, out error))
            {
                return false;
            }
            if (!values.TryGetValue(propertyName, out rawValue))
            {
                error = $"JSON object has no '{propertyName}' property.";
                return false;
            }
            return true;
        }

        public static bool TryReadArrayElements(
            string json,
            out List<string> elements,
            out string error)
        {
            StrictJsonReader reader = new StrictJsonReader(json);
            reader.SkipWhitespace();
            if (!reader.TryReadArray(out elements, out error))
            {
                return false;
            }
            reader.SkipWhitespace();
            if (reader.position != reader.source.Length)
            {
                error = "JSON array contains trailing content.";
                return false;
            }
            return true;
        }

        private bool TryReadObject(
            out Dictionary<string, string> properties,
            out string error)
        {
            properties = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;
            if (!TryConsume('{'))
            {
                error = "JSON value must be an object.";
                return false;
            }
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return true;
            }

            while (true)
            {
                SkipWhitespace();
                if (!TryReadString(out string key, out error))
                {
                    return false;
                }
                if (properties.ContainsKey(key))
                {
                    error = $"JSON object contains duplicate key '{key}'.";
                    return false;
                }
                SkipWhitespace();
                if (!TryConsume(':'))
                {
                    error = $"JSON property '{key}' has no colon.";
                    return false;
                }
                SkipWhitespace();
                int valueStart = position;
                if (!TrySkipValue(out error))
                {
                    return false;
                }
                properties.Add(key, source.Substring(valueStart, position - valueStart));
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return true;
                }
                if (!TryConsume(','))
                {
                    error = "JSON object properties must be separated by commas.";
                    return false;
                }
            }
        }

        private bool TryReadArray(out List<string> elements, out string error)
        {
            elements = new List<string>();
            error = string.Empty;
            if (!TryConsume('['))
            {
                error = "JSON value must be an array.";
                return false;
            }
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return true;
            }
            while (true)
            {
                SkipWhitespace();
                int start = position;
                if (!TrySkipValue(out error))
                {
                    return false;
                }
                elements.Add(source.Substring(start, position - start));
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return true;
                }
                if (!TryConsume(','))
                {
                    error = "JSON array values must be separated by commas.";
                    return false;
                }
            }
        }

        private bool TrySkipValue(out string error)
        {
            error = string.Empty;
            SkipWhitespace();
            if (position >= source.Length)
            {
                error = "JSON value is incomplete.";
                return false;
            }

            char current = source[position];
            if (current == '{')
            {
                return TryReadObject(out _, out error);
            }
            if (current == '[')
            {
                return TryReadArray(out _, out error);
            }
            if (current == '"')
            {
                return TryReadString(out _, out error);
            }
            if (current == 't') return TryReadLiteral("true", out error);
            if (current == 'f') return TryReadLiteral("false", out error);
            if (current == 'n') return TryReadLiteral("null", out error);
            return TryReadNumber(out error);
        }

        private bool TryReadString(out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            if (!TryConsume('"'))
            {
                error = "JSON object key or string value must begin with a quote.";
                return false;
            }
            StringBuilder builder = new StringBuilder();
            while (position < source.Length)
            {
                char current = source[position++];
                if (current == '"')
                {
                    value = builder.ToString();
                    return true;
                }
                if (current < 0x20)
                {
                    error = "JSON string contains an unescaped control character.";
                    return false;
                }
                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }
                if (position >= source.Length)
                {
                    error = "JSON string ends in an escape character.";
                    return false;
                }
                char escaped = source[position++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (position + 4 > source.Length
                            || !int.TryParse(
                                source.Substring(position, 4),
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out int codePoint))
                        {
                            error = "JSON string contains an invalid Unicode escape.";
                            return false;
                        }
                        builder.Append((char)codePoint);
                        position += 4;
                        break;
                    default:
                        error = $"JSON string contains invalid escape '\\{escaped}'.";
                        return false;
                }
            }
            error = "JSON string is not terminated.";
            return false;
        }

        private bool TryReadLiteral(string literal, out string error)
        {
            error = string.Empty;
            if (position + literal.Length > source.Length
                || !string.Equals(
                    source.Substring(position, literal.Length),
                    literal,
                    StringComparison.Ordinal))
            {
                error = $"JSON literal at offset {position} is invalid.";
                return false;
            }
            position += literal.Length;
            return true;
        }

        private bool TryReadNumber(out string error)
        {
            error = string.Empty;
            int start = position;
            TryConsume('-');
            if (TryConsume('0'))
            {
                if (position < source.Length && char.IsDigit(source[position]))
                {
                    error = "JSON number cannot contain a leading zero.";
                    return false;
                }
            }
            else if (!TryReadDigits(requireOne: true))
            {
                error = $"JSON value at offset {start} is invalid.";
                return false;
            }
            if (TryConsume('.') && !TryReadDigits(requireOne: true))
            {
                error = "JSON number fraction is incomplete.";
                return false;
            }
            if (position < source.Length && (source[position] == 'e' || source[position] == 'E'))
            {
                position++;
                if (position < source.Length && (source[position] == '+' || source[position] == '-'))
                {
                    position++;
                }
                if (!TryReadDigits(requireOne: true))
                {
                    error = "JSON number exponent is incomplete.";
                    return false;
                }
            }
            return position > start;
        }

        private bool TryReadDigits(bool requireOne)
        {
            int start = position;
            while (position < source.Length && char.IsDigit(source[position]))
            {
                position++;
            }
            return !requireOne || position > start;
        }

        private void SkipWhitespace()
        {
            while (position < source.Length
                && (source[position] == ' '
                    || source[position] == '\t'
                    || source[position] == '\r'
                    || source[position] == '\n'))
            {
                position++;
            }
        }

        private bool TryConsume(char expected)
        {
            if (position >= source.Length || source[position] != expected)
            {
                return false;
            }
            position++;
            return true;
        }
    }
}

public readonly struct PrefixAffinityKey : IEquatable<PrefixAffinityKey>
{
    public PrefixAffinityKey(
        string schemaHash,
        string eventId,
        string factPacketHash,
        int knowledgeSnapshotVersion,
        int cultureStyleVersion)
    {
        SchemaHash = Normalize(schemaHash);
        EventId = Normalize(eventId);
        FactPacketHash = Normalize(factPacketHash);
        KnowledgeSnapshotVersion = Math.Max(0, knowledgeSnapshotVersion);
        CultureStyleVersion = Math.Max(0, cultureStyleVersion);
    }

    public string SchemaHash { get; }
    public string EventId { get; }
    public string FactPacketHash { get; }
    public int KnowledgeSnapshotVersion { get; }
    public int CultureStyleVersion { get; }
    public bool IsValid => SchemaHash.Length > 0 && FactPacketHash.Length > 0;

    public bool Equals(PrefixAffinityKey other)
    {
        return KnowledgeSnapshotVersion == other.KnowledgeSnapshotVersion
            && CultureStyleVersion == other.CultureStyleVersion
            && string.Equals(SchemaHash, other.SchemaHash, StringComparison.Ordinal)
            && string.Equals(EventId, other.EventId, StringComparison.Ordinal)
            && string.Equals(FactPacketHash, other.FactPacketHash, StringComparison.Ordinal);
    }

    public override bool Equals(object obj) => obj is PrefixAffinityKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(SchemaHash);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(EventId);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FactPacketHash);
            hash = hash * 31 + KnowledgeSnapshotVersion;
            hash = hash * 31 + CultureStyleVersion;
            return hash;
        }
    }

    public override string ToString()
    {
        return $"{SchemaHash}:{EventId}:{FactPacketHash}:{KnowledgeSnapshotVersion}:{CultureStyleVersion}";
    }

    public static bool operator ==(PrefixAffinityKey left, PrefixAffinityKey right) => left.Equals(right);
    public static bool operator !=(PrefixAffinityKey left, PrefixAffinityKey right) => !left.Equals(right);

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
}

public sealed class NarrativeSchedulingMetadata
{
    public PrefixAffinityKey AffinityKey { get; set; }
    public bool Persistent { get; set; }
    public bool Urgent { get; set; }
    public float ExpiresAt { get; set; }

    public static NarrativeSchedulingMetadata CreateDefault(
        LocalLlmRequestProfile profile,
        string prompt,
        string correlationId,
        float enqueuedAt)
    {
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(profile.Id);
        string eventId = string.IsNullOrWhiteSpace(correlationId)
            ? profile.Id
            : correlationId.Trim();
        return new NarrativeSchedulingMetadata
        {
            AffinityKey = new PrefixAffinityKey(
                schema.Hash,
                eventId,
                StableUtf8Hash(prompt),
                0,
                1),
            Persistent = schema.PersistentNarrative,
            Urgent = string.Equals(profile.Id, LocalLlmRequestProfiles.BubbleLine.Id, StringComparison.Ordinal),
            ExpiresAt = profile.MaxQueueAgeSeconds > 0f
                ? enqueuedAt + profile.MaxQueueAgeSeconds
                : float.PositiveInfinity
        };
    }

    public static string StableUtf8Hash(string text)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
        StringBuilder builder = new StringBuilder(16);
        for (int i = 0; i < 8; i++)
        {
            builder.Append(hash[i].ToString("x2"));
        }
        return builder.ToString();
    }
}

public interface IContextAwareLlmRequest
{
    int Priority { get; }
    float EnqueuedAt { get; }
    NarrativeSchedulingMetadata Scheduling { get; }
}

public static class ContextAwareLlmScheduler
{
    public const float CoalescingWindowSeconds = 0.075f;
    public const float AgingThresholdSeconds = 0.5f;
    public const float DeadlineOverrideSeconds = 0.25f;
    public const int MaximumAffinityBurst = 4;

    public static bool CanDispatch(IContextAwareLlmRequest request, float now, int queueCount)
    {
        if (request == null || request.Scheduling == null)
        {
            return true;
        }

        return request.Scheduling.Urgent
            || queueCount > 1
            || now - request.EnqueuedAt >= CoalescingWindowSeconds;
    }

    public static int FindNext<T>(
        IReadOnlyList<T> requests,
        float now,
        PrefixAffinityKey currentAffinity,
        int currentAffinityBurst)
        where T : IContextAwareLlmRequest
    {
        if (requests == null || requests.Count == 0)
        {
            return -1;
        }

        int best = 0;
        for (int index = 1; index < requests.Count; index++)
        {
            if (ComesBefore(
                    requests[index],
                    requests[best],
                    now,
                    currentAffinity,
                    currentAffinityBurst))
            {
                best = index;
            }
        }
        return best;
    }

    private static bool ComesBefore(
        IContextAwareLlmRequest candidate,
        IContextAwareLlmRequest incumbent,
        float now,
        PrefixAffinityKey currentAffinity,
        int currentAffinityBurst)
    {
        NarrativeSchedulingMetadata left = candidate.Scheduling;
        NarrativeSchedulingMetadata right = incumbent.Scheduling;
        bool leftDeadline = left != null && left.ExpiresAt - now <= DeadlineOverrideSeconds;
        bool rightDeadline = right != null && right.ExpiresAt - now <= DeadlineOverrideSeconds;
        if (leftDeadline != rightDeadline)
        {
            return leftDeadline;
        }

        bool leftPersistent = left?.Persistent == true;
        bool rightPersistent = right?.Persistent == true;
        if (leftPersistent != rightPersistent)
        {
            return leftPersistent;
        }

        bool allowAffinity = currentAffinityBurst < MaximumAffinityBurst;
        bool leftAffinity = allowAffinity && left != null && left.AffinityKey == currentAffinity;
        bool rightAffinity = allowAffinity && right != null && right.AffinityKey == currentAffinity;
        if (leftAffinity != rightAffinity)
        {
            return leftAffinity;
        }

        float leftAge = Math.Max(0f, now - candidate.EnqueuedAt);
        float rightAge = Math.Max(0f, now - incumbent.EnqueuedAt);
        bool leftAged = leftAge >= AgingThresholdSeconds;
        bool rightAged = rightAge >= AgingThresholdSeconds;
        if (leftAged != rightAged)
        {
            return leftAged;
        }

        if (candidate.Priority != incumbent.Priority)
        {
            return candidate.Priority > incumbent.Priority;
        }
        return candidate.EnqueuedAt < incumbent.EnqueuedAt;
    }
}

public readonly struct ChoicePromptDiagnostic
{
    public ChoicePromptDiagnostic(string prompt, string hash, string lastBytesHex)
    {
        Prompt = prompt ?? string.Empty;
        Hash = hash ?? string.Empty;
        LastBytesHex = lastBytesHex ?? string.Empty;
    }

    public string Prompt { get; }
    public string Hash { get; }
    public string LastBytesHex { get; }
}

public static class ChoicePromptCanonicalizer
{
    public const string FinalMarker = "선택 JSON:";

    public static bool TryCanonicalize(
        string prompt,
        out ChoicePromptDiagnostic diagnostic,
        out string error)
    {
        diagnostic = default;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            error = "Choice prompt is empty.";
            return false;
        }

        string normalized = prompt.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();
        if (normalized.EndsWith(FinalMarker, StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - FinalMarker.Length).TrimEnd();
        }
        normalized = normalized + "\n" + FinalMarker;
        if (char.IsWhiteSpace(normalized[normalized.Length - 1]))
        {
            error = "Canonical choice prompt ends in whitespace.";
            return false;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(normalized);
        int start = Math.Max(0, bytes.Length - 16);
        StringBuilder suffix = new StringBuilder((bytes.Length - start) * 2);
        for (int i = start; i < bytes.Length; i++)
        {
            suffix.Append(bytes[i].ToString("x2"));
        }
        diagnostic = new ChoicePromptDiagnostic(
            normalized,
            NarrativeSchedulingMetadata.StableUtf8Hash(normalized),
            suffix.ToString());
        return true;
    }
}

public static class EquipmentChoiceGrammarCatalog
{
    public const string Choice2 = "root ::= ws \"{\" ws \"\\\"selectedIndex\\\"\" ws \":\" ws (\"0\" | \"1\") ws \"}\" ws\nws ::= [ \\t\\n\\r]*";
    public const string Choice3 = "root ::= ws \"{\" ws \"\\\"selectedIndex\\\"\" ws \":\" ws (\"0\" | \"1\" | \"2\") ws \"}\" ws\nws ::= [ \\t\\n\\r]*";

    public static string Require(int candidateCount)
    {
        return candidateCount switch
        {
            2 => Choice2,
            3 => Choice3,
            _ => throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "Only two or three candidates are supported.")
        };
    }
}

public static class EquipmentChoiceResultParser
{
    public static bool TryParse(string content, int candidateCount, out int selectedIndex)
    {
        return TryParse(content, candidateCount, out selectedIndex, out _);
    }

    public static bool TryParse(
        string content,
        int candidateCount,
        out int selectedIndex,
        out string error)
    {
        selectedIndex = -1;
        error = string.Empty;
        if (candidateCount < 2 || candidateCount > 3)
        {
            error = "EquipmentChoice.CandidateCountInvalid";
            return false;
        }

        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                "EquipmentChoiceLegacyV2",
                content,
                out string json,
                out string contractError)
            || !NarrativeExactKeyContract.TryReadIntegerProperty(
                json,
                "selectedIndex",
                out selectedIndex,
                out contractError))
        {
            error = "EquipmentChoice.SchemaInvalid: " + contractError;
            selectedIndex = -1;
            return false;
        }
        if (selectedIndex < 0 || selectedIndex >= candidateCount)
        {
            error = "EquipmentChoice.SelectedIndexOutOfRange";
            return false;
        }
        return true;
    }
}

[Serializable]
public sealed class NarrativeViewpointRequest
{
    public string eventId = string.Empty;
    public string viewpointCharacterId = string.Empty;
    public string knowledgeSnapshotHash = string.Empty;
    public int knowledgeSnapshotVersion;
    public string relationshipFactHash = string.Empty;
    public string cultureStyleId = string.Empty;
    public string modelVersion = string.Empty;
}

[Serializable]
public sealed class NarrativeMultiPerspectiveRequest
{
    public string sharedFactPacket = string.Empty;
    public List<NarrativeViewpointRequest> viewpoints = new List<NarrativeViewpointRequest>(4);

    public bool IsValidInitialRequest => viewpoints != null
        && viewpoints.Count >= 2
        && viewpoints.Count <= 4;

    public int CultureStyleVersion
    {
        get
        {
            if (viewpoints == null || viewpoints.Count == 0)
            {
                return 0;
            }

            unchecked
            {
                uint hash = 2166136261u;
                IEnumerable<string> styles = viewpoints
                    .Where(value => value != null)
                    .Select(value => value.cultureStyleId ?? string.Empty)
                    .OrderBy(value => value, StringComparer.Ordinal);
                foreach (string value in styles)
                {
                    for (int index = 0; index < value.Length; index++)
                    {
                        hash = (hash ^ value[index]) * 16777619u;
                    }
                    hash = (hash ^ 0xffu) * 16777619u;
                }
                return (int)(hash & 0x7fffffffu);
            }
        }
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (!IsValidInitialRequest || string.IsNullOrWhiteSpace(sharedFactPacket))
        {
            error = "A multi-perspective request requires one fact packet and two to four viewpoints.";
            return false;
        }

        HashSet<string> characters = new HashSet<string>(StringComparer.Ordinal);
        string eventId = viewpoints[0]?.eventId?.Trim() ?? string.Empty;
        foreach (NarrativeViewpointRequest viewpoint in viewpoints)
        {
            if (viewpoint == null
                || string.IsNullOrWhiteSpace(viewpoint.viewpointCharacterId)
                || string.IsNullOrWhiteSpace(viewpoint.knowledgeSnapshotHash)
                || string.IsNullOrWhiteSpace(eventId)
                || !string.Equals(eventId, viewpoint.eventId?.Trim(), StringComparison.Ordinal)
                || !characters.Add(viewpoint.viewpointCharacterId.Trim()))
            {
                error = "Viewpoints require one event, unique persistent characters, and captured knowledge.";
                return false;
            }
        }
        return true;
    }
}

[Serializable]
public sealed class NarrativePerspectiveOutput
{
    public string viewpointCharacterId = string.Empty;
    public string line = string.Empty;
}

[Serializable]
public sealed class NarrativeMultiPerspectiveOutput
{
    public string eventId = string.Empty;
    public List<NarrativePerspectiveOutput> perspectives = new List<NarrativePerspectiveOutput>(4);
    public string[] usedMotifIds = Array.Empty<string>();
    public string[] usedCharacterFactIds = Array.Empty<string>();

    public bool Matches(NarrativeMultiPerspectiveRequest request)
    {
        if (request == null
            || !request.TryValidate(out _)
            || !string.Equals(eventId, request.viewpoints[0].eventId, StringComparison.Ordinal)
            || perspectives == null
            || perspectives.Count != request.viewpoints.Count)
        {
            return false;
        }

        HashSet<string> expected = new HashSet<string>(
            request.viewpoints.Select(value => value.viewpointCharacterId),
            StringComparer.Ordinal);
        return perspectives.All(value => value != null
            && !string.IsNullOrWhiteSpace(value.line)
            && expected.Remove(value.viewpointCharacterId))
            && expected.Count == 0;
    }
}
