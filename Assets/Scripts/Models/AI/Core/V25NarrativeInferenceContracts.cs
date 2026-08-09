using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
    public const string FinalMarker = "선택 번호:";

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
    public const string Choice2 = "root ::= ws? (\"0\" | \"1\")\nws ::= [ \\t\\n]";
    public const string Choice3 = "root ::= ws? (\"0\" | \"1\" | \"2\")\nws ::= [ \\t\\n]";

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
    private static readonly Regex Pattern = new Regex("^[0-2]$", RegexOptions.CultureInvariant);

    public static bool TryParse(string content, int candidateCount, out int selectedIndex)
    {
        selectedIndex = -1;
        if (candidateCount < 2 || candidateCount > 3)
        {
            return false;
        }

        string normalized = content?.Trim() ?? string.Empty;
        return Pattern.IsMatch(normalized)
            && int.TryParse(normalized, out selectedIndex)
            && selectedIndex >= 0
            && selectedIndex < candidateCount;
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
