using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

public enum SocialRumorType
{
    None = 0,
    Complaint = 1,
    Recommendation = 2,
    Warning = 3,
    Praise = 4
}

public enum SocialRumorTargetType
{
    None = 0,
    Facility = 1,
    Character = 2
}

[Serializable]
public sealed class SocialRumor
{
    public SocialRumorType type;
    public SocialRumorTargetType targetType;
    public string sourceActorId = string.Empty;
    public string sourceActorName;
    public int targetFacilityId = -1;
    public string targetFacilityTag;
    public string targetCharacterId = string.Empty;
    public string targetCharacterName;
    [Range(-1f, 1f)] public float sentiment;
    [Range(0f, 1f)] public float spreadChance;
    [Range(-1f, 1f)] public float trustImpact;
    public float validUntil;
    public string summary;
    public string source;

    public bool IsActionableAt(float now) => type != SocialRumorType.None
        && targetType != SocialRumorTargetType.None
        && !IsExpiredAt(now);

    public bool IsExpiredAt(float now) => validUntil > 0f && now > validUntil;

    public SocialRumor Clone()
    {
        return (SocialRumor)MemberwiseClone();
    }
}

[Serializable]
public sealed class SocialRumorSnapshot
{
    public SocialRumorType type;
    public SocialRumorTargetType targetType;
    public string sourceActorId = string.Empty;
    public string sourceActorName = string.Empty;
    public int targetFacilityId = -1;
    public string targetFacilityTag = string.Empty;
    public string targetCharacterId = string.Empty;
    public string targetCharacterName = string.Empty;
    public float sentiment;
    public float spreadChance;
    public float trustImpact;
    public float remainingSeconds;
    public string summary = string.Empty;
    public string source = string.Empty;

    public static SocialRumorSnapshot Capture(SocialRumor rumor, float now)
    {
        if (rumor == null)
        {
            return null;
        }

        return new SocialRumorSnapshot
        {
            type = rumor.type,
            targetType = rumor.targetType,
            sourceActorId = rumor.sourceActorId,
            sourceActorName = rumor.sourceActorName,
            targetFacilityId = rumor.targetFacilityId,
            targetFacilityTag = rumor.targetFacilityTag,
            targetCharacterId = rumor.targetCharacterId,
            targetCharacterName = rumor.targetCharacterName,
            sentiment = rumor.sentiment,
            spreadChance = rumor.spreadChance,
            trustImpact = rumor.trustImpact,
            remainingSeconds = rumor.validUntil > 0f
                ? Mathf.Max(0f, rumor.validUntil - now)
                : 0f,
            summary = rumor.summary,
            source = rumor.source
        };
    }

    public SocialRumor Restore(float now)
    {
        return new SocialRumor
        {
            type = type,
            targetType = targetType,
            sourceActorId = sourceActorId,
            sourceActorName = sourceActorName,
            targetFacilityId = targetFacilityId,
            targetFacilityTag = targetFacilityTag,
            targetCharacterId = targetCharacterId,
            targetCharacterName = targetCharacterName,
            sentiment = sentiment,
            spreadChance = spreadChance,
            trustImpact = trustImpact,
            validUntil = remainingSeconds > 0f ? now + remainingSeconds : 0f,
            summary = summary,
            source = source
        };
    }

    public SocialRumorSnapshot Clone()
    {
        return (SocialRumorSnapshot)MemberwiseClone();
    }
}

[Serializable]
public sealed class CharacterSocialMemorySnapshot
{
    public List<SocialRumorSnapshot> recentRumors = new List<SocialRumorSnapshot>();
    public List<SocialMemoryFloat> facilitySentiments = new List<SocialMemoryFloat>();
    public List<SocialMemoryFloat> characterSentiments = new List<SocialMemoryFloat>();
    public List<SocialMemoryFloat> sourceTrust = new List<SocialMemoryFloat>();

    public CharacterSocialMemorySnapshot Clone()
    {
        return new CharacterSocialMemorySnapshot
        {
            recentRumors = recentRumors?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<SocialRumorSnapshot>(),
            facilitySentiments = CloneValues(facilitySentiments),
            characterSentiments = CloneValues(characterSentiments),
            sourceTrust = CloneValues(sourceTrust)
        };
    }

    private static List<SocialMemoryFloat> CloneValues(IEnumerable<SocialMemoryFloat> source)
    {
        return source?.Where(item => item != null)
            .Select(item => new SocialMemoryFloat(item.key, item.value))
            .ToList() ?? new List<SocialMemoryFloat>();
    }
}

[Serializable]
public sealed class SocialMemoryFloat
{
    public string key;
    public float value;

    public SocialMemoryFloat(string key, float value)
    {
        this.key = key;
        this.value = value;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterActor))]
public sealed class CharacterSocialMemory : SerializedMonoBehaviour
{
    [SerializeField, Min(1)] private int maxRumorMemory = 24;
    [SerializeField, Range(0f, 1f)] private float newRumorBlend = 0.35f;
    [SerializeField, ReadOnly] private CharacterActor actor;
    [SerializeField, ReadOnly] private List<SocialRumor> recentRumors = new List<SocialRumor>();
    [SerializeField, ReadOnly] private List<SocialMemoryFloat> facilitySentimentDebug = new List<SocialMemoryFloat>();
    [SerializeField, ReadOnly] private List<SocialMemoryFloat> characterSentimentDebug = new List<SocialMemoryFloat>();
    [SerializeField, ReadOnly] private List<SocialMemoryFloat> sourceTrustDebug = new List<SocialMemoryFloat>();

    private readonly Dictionary<string, float> facilitySentimentByKey = new Dictionary<string, float>();
    private readonly Dictionary<string, float> characterSentimentByKey = new Dictionary<string, float>();
    private readonly Dictionary<string, float> sourceTrustByKey = new Dictionary<string, float>();
    private IGameClock gameClock;

    public IReadOnlyList<SocialRumor> RecentRumors => Array.AsReadOnly(
        (recentRumors ?? new List<SocialRumor>())
            .Where((rumor) => rumor != null)
            .Select((rumor) => rumor.Clone())
            .ToArray());

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    [Inject]
    public void Construct(IGameClock gameClock)
    {
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
    }

    public void HearRumor(SocialRumor rumor, CharacterActor speaker)
    {
        if (rumor == null || !rumor.IsActionableAt(Now))
        {
            return;
        }

        SocialRumor copy = rumor.Clone();
        float trust = GetSourceTrustScore(speaker, copy);
        float weightedSentiment = Mathf.Clamp(copy.sentiment * trust, -1f, 1f);
        copy.sentiment = weightedSentiment;
        RememberRumor(copy);

        float moodImpact = Mathf.Clamp(weightedSentiment * 6f, -6f, 6f);
        if (actor != null && Mathf.Abs(moodImpact) >= 0.5f)
        {
            string sourceKey = speaker != null
                ? SocialRumorUtility.GetActorKey(speaker)
                : "unknown";
            actor.ApplyMoodFactor(
                $"social:rumor:{sourceKey}:{copy.targetType}",
                moodImpact > 0f ? "반가운 소문을 들음" : "불쾌한 소문을 들음",
                moodImpact,
                180f,
                2);
        }

        if (copy.targetType == SocialRumorTargetType.Facility)
        {
            foreach (string key in SocialRumorUtility.GetFacilityKeys(copy))
            {
                Blend(facilitySentimentByKey, key, copy.sentiment, newRumorBlend);
            }
        }
        else if (copy.targetType == SocialRumorTargetType.Character)
        {
            foreach (string key in SocialRumorUtility.GetCharacterKeys(copy))
            {
                Blend(characterSentimentByKey, key, copy.sentiment, newRumorBlend);
            }
        }

        if (speaker != null)
        {
            string trustKey = SocialRumorUtility.GetActorKey(speaker);
            float nextTrust = GetDictionaryValue(sourceTrustByKey, trustKey, 1f) + copy.trustImpact * 0.15f;
            sourceTrustByKey[trustKey] = Mathf.Clamp(nextTrust, 0.25f, 1.5f);
        }

        SyncDebugLists();
    }

    public float GetFacilitySentiment(BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        PruneExpiredRumors();
        float sum = 0f;
        int count = 0;
        foreach (KeyValuePair<string, float> entry in facilitySentimentByKey)
        {
            if (!SocialRumorUtility.MatchesFacilityKey(building, entry.Key))
            {
                continue;
            }

            sum += entry.Value;
            count++;
        }

        return count > 0 ? Mathf.Clamp(sum / count, -1f, 1f) : 0f;
    }

    public void RememberFacilityExperience(
        BuildableObject building,
        float sentiment,
        string summary,
        float durationSeconds = 600f)
    {
        if (building == null || Mathf.Abs(sentiment) < 0.01f)
        {
            return;
        }

        SocialRumor experience = new SocialRumor
        {
            type = sentiment < 0f ? SocialRumorType.Complaint : SocialRumorType.Praise,
            targetType = SocialRumorTargetType.Facility,
            sourceActorId = actor != null && actor.Identity != null
                ? actor.Identity.PersistentId
                : string.Empty,
            sourceActorName = SocialRumorUtility.GetActorLabel(actor),
            targetFacilityId = building.id,
            targetFacilityTag = SocialRumorUtility.GetFacilityTag(building),
            sentiment = Mathf.Clamp(sentiment, -1f, 1f),
            spreadChance = 0f,
            trustImpact = 0f,
            validUntil = durationSeconds > 0f ? Now + durationSeconds : 0f,
            summary = summary?.Trim() ?? string.Empty,
            source = "direct-experience"
        };

        RememberRumor(experience);
        foreach (string key in SocialRumorUtility.GetFacilityKeys(experience))
        {
            Blend(facilitySentimentByKey, key, experience.sentiment, newRumorBlend);
        }

        SyncDebugLists();
    }

    public void RememberCharacterExperience(
        CharacterActor target,
        float sentiment,
        string summary,
        float durationSeconds = 600f)
    {
        if (target == null || target == actor || Mathf.Abs(sentiment) < 0.01f)
        {
            return;
        }

        SocialRumor experience = new SocialRumor
        {
            type = sentiment < 0f ? SocialRumorType.Warning : SocialRumorType.Praise,
            targetType = SocialRumorTargetType.Character,
            sourceActorId = actor != null && actor.Identity != null
                ? actor.Identity.PersistentId
                : string.Empty,
            sourceActorName = SocialRumorUtility.GetActorLabel(actor),
            targetCharacterId = target.Identity != null
                ? target.Identity.PersistentId
                : string.Empty,
            targetCharacterName = SocialRumorUtility.GetActorLabel(target),
            sentiment = Mathf.Clamp(sentiment, -1f, 1f),
            spreadChance = 0f,
            trustImpact = 0f,
            validUntil = durationSeconds > 0f ? Now + durationSeconds : 0f,
            summary = summary?.Trim() ?? string.Empty,
            source = "direct-character-experience"
        };

        RememberRumor(experience);
        foreach (string key in SocialRumorUtility.GetCharacterKeys(experience))
        {
            Blend(characterSentimentByKey, key, experience.sentiment, newRumorBlend);
        }

        SyncDebugLists();
    }

    public float GetRelationshipSentiment(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        string idKey = SocialRumorUtility.GetActorKey(target);
        if (characterSentimentByKey.TryGetValue(idKey, out float idValue))
        {
            return Mathf.Clamp(idValue, -1f, 1f);
        }

        string nameKey = SocialRumorUtility.GetActorNameKey(target);
        return characterSentimentByKey.TryGetValue(nameKey, out float nameValue)
            ? Mathf.Clamp(nameValue, -1f, 1f)
            : 0f;
    }

    public float GetSourceTrust(CharacterActor source)
    {
        if (source == null)
        {
            return 1f;
        }

        string key = SocialRumorUtility.GetActorKey(source);
        return Mathf.Clamp(GetDictionaryValue(sourceTrustByKey, key, 1f), 0.25f, 1.5f);
    }

    public CharacterSocialMemorySnapshot CaptureSnapshot()
    {
        PruneExpiredRumors();
        float now = Now;
        return new CharacterSocialMemorySnapshot
        {
            recentRumors = recentRumors
                .Where(rumor => rumor != null && !rumor.IsExpiredAt(now))
                .Select(rumor => SocialRumorSnapshot.Capture(rumor, now))
                .Where(snapshot => snapshot != null)
                .ToList(),
            facilitySentiments = facilitySentimentByKey
                .Select(entry => new SocialMemoryFloat(entry.Key, entry.Value)).ToList(),
            characterSentiments = characterSentimentByKey
                .Select(entry => new SocialMemoryFloat(entry.Key, entry.Value)).ToList(),
            sourceTrust = sourceTrustByKey
                .Select(entry => new SocialMemoryFloat(entry.Key, entry.Value)).ToList()
        };
    }

    public void RestoreSnapshot(CharacterSocialMemorySnapshot snapshot)
    {
        float now = Now;
        recentRumors.Clear();
        facilitySentimentByKey.Clear();
        characterSentimentByKey.Clear();
        sourceTrustByKey.Clear();
        if (snapshot != null)
        {
            recentRumors.AddRange(snapshot.recentRumors?
                .Where(item => item != null && item.remainingSeconds >= 0f)
                .Select(item => item.Restore(now))
                .Where(rumor => rumor != null) ?? Enumerable.Empty<SocialRumor>());
            RestoreValues(snapshot.facilitySentiments, facilitySentimentByKey);
            RestoreValues(snapshot.characterSentiments, characterSentimentByKey);
            RestoreValues(snapshot.sourceTrust, sourceTrustByKey);
        }

        SyncDebugLists();
    }

    private float GetSourceTrustScore(CharacterActor speaker, SocialRumor rumor)
    {
        if (speaker == null)
        {
            return 1f;
        }

        string key = SocialRumorUtility.GetActorKey(speaker);
        float trust = GetDictionaryValue(sourceTrustByKey, key, 1f);
        if (actor != null
            && actor.Identity != null
            && speaker.Identity != null
            && !string.IsNullOrWhiteSpace(actor.Identity.SpeciesTag)
            && string.Equals(actor.Identity.SpeciesTag, speaker.Identity.SpeciesTag, StringComparison.OrdinalIgnoreCase))
        {
            trust += 0.1f;
        }

        if (speaker.Identity != null
            && string.Equals(rumor.sourceActorId, speaker.Identity.PersistentId, StringComparison.Ordinal))
        {
            trust += 0.05f;
        }

        return Mathf.Clamp(trust, 0.25f, 1.5f);
    }

    private void RememberRumor(SocialRumor rumor)
    {
        recentRumors.Add(rumor);
        PruneExpiredRumors();
        while (recentRumors.Count > Mathf.Max(1, maxRumorMemory))
        {
            recentRumors.RemoveAt(0);
        }
    }

    private void PruneExpiredRumors()
    {
        float now = Now;
        bool removed = false;
        for (int i = recentRumors.Count - 1; i >= 0; i--)
        {
            if (recentRumors[i] == null || recentRumors[i].IsExpiredAt(now))
            {
                recentRumors.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            RebuildSentimentMaps();
        }
    }

    private void RebuildSentimentMaps()
    {
        float now = Now;
        facilitySentimentByKey.Clear();
        characterSentimentByKey.Clear();
        foreach (SocialRumor rumor in recentRumors)
        {
            if (rumor == null || rumor.IsExpiredAt(now))
            {
                continue;
            }

            if (rumor.targetType == SocialRumorTargetType.Facility)
            {
                foreach (string key in SocialRumorUtility.GetFacilityKeys(rumor))
                {
                    Blend(facilitySentimentByKey, key, rumor.sentiment, newRumorBlend);
                }
            }
            else if (rumor.targetType == SocialRumorTargetType.Character)
            {
                foreach (string key in SocialRumorUtility.GetCharacterKeys(rumor))
                {
                    Blend(characterSentimentByKey, key, rumor.sentiment, newRumorBlend);
                }
            }
        }

        SyncDebugLists();
    }

    private float Now => gameClock != null
        ? gameClock.Time
        : throw new InvalidOperationException(
            $"{nameof(CharacterSocialMemory)} requires {nameof(IGameClock)} injection.");

    private void SyncDebugLists()
    {
        SyncDebugList(facilitySentimentByKey, facilitySentimentDebug);
        SyncDebugList(characterSentimentByKey, characterSentimentDebug);
        SyncDebugList(sourceTrustByKey, sourceTrustDebug);
    }

    private static void Blend(Dictionary<string, float> map, string key, float value, float blend)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        float current = GetDictionaryValue(map, key, 0f);
        map[key] = Mathf.Clamp(Mathf.Lerp(current, value, Mathf.Clamp01(blend)), -1f, 1f);
    }

    private static float GetDictionaryValue(Dictionary<string, float> map, string key, float fallback)
    {
        return !string.IsNullOrWhiteSpace(key) && map.TryGetValue(key, out float value)
            ? value
            : fallback;
    }

    private static void SyncDebugList(Dictionary<string, float> source, List<SocialMemoryFloat> target)
    {
        target.Clear();
        foreach (KeyValuePair<string, float> entry in source)
        {
            target.Add(new SocialMemoryFloat(entry.Key, entry.Value));
        }
    }

    private static void RestoreValues(
        IEnumerable<SocialMemoryFloat> source,
        Dictionary<string, float> target)
    {
        foreach (SocialMemoryFloat entry in source ?? Enumerable.Empty<SocialMemoryFloat>())
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
            {
                target[entry.key] = entry.value;
            }
        }
    }
}

public static class SocialRumorUtility
{
    public static IEnumerable<string> GetFacilityKeys(SocialRumor rumor)
    {
        if (rumor == null)
        {
            yield break;
        }

        if (rumor.targetFacilityId >= 0)
        {
            yield return $"facility:{rumor.targetFacilityId}";
        }

        if (!string.IsNullOrWhiteSpace(rumor.targetFacilityTag))
        {
            yield return $"tag:{Normalize(rumor.targetFacilityTag)}";
        }
    }

    public static IEnumerable<string> GetCharacterKeys(SocialRumor rumor)
    {
        if (rumor == null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(rumor.targetCharacterId))
        {
            yield return rumor.targetCharacterId.Trim();
        }
    }

    public static string GetActorKey(CharacterActor actor)
    {
        if (actor == null)
        {
            return string.Empty;
        }

        string id = actor.Identity != null ? actor.Identity.PersistentId : string.Empty;
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }

    public static string GetActorNameKey(CharacterActor actor)
    {
        string displayName = actor != null && actor.Identity != null
            ? actor.Identity.DisplayName
            : actor != null ? actor.name : string.Empty;
        return string.IsNullOrWhiteSpace(displayName)
            ? string.Empty
            : $"actor-name:{Normalize(displayName)}";
    }

    public static bool MatchesFacilityKey(BuildableObject building, string key)
    {
        if (building == null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (key.StartsWith("facility:", StringComparison.Ordinal))
        {
            return key == $"facility:{building.id}";
        }

        if (!key.StartsWith("tag:", StringComparison.Ordinal))
        {
            return false;
        }

        string tag = key.Substring("tag:".Length);
        return MatchesFacilityTag(building, tag);
    }

    public static bool MatchesFacilityTag(BuildableObject building, string tag)
    {
        if (building == null || string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return building.HasSemanticTag(tag);
    }

    public static string GetActorLabel(CharacterActor actor)
    {
        if (actor == null)
        {
            return "Unknown";
        }

        return actor.Identity != null ? actor.Identity.DisplayName : actor.name;
    }

    public static string GetFacilityLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "none";
        }

        if (building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName))
        {
            return building.BuildingData.objectName;
        }

        return building.name;
    }

    public static string GetFacilityTag(BuildableObject building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        return building.BuildingData.GetPrimarySemanticTag();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
