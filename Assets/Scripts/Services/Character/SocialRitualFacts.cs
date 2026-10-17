using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;

[Serializable]
public sealed class FestivalResolutionFactData
{
    public string occurrenceId, festivalId, displayName;
    public int absoluteDay, assignedParticipantCount, venueCapacity;
    public float plannedDurationSeconds, elapsedFestivalSeconds;
    public FestivalResolutionGrade grade;
    public bool stoppedByVenueHazard;
    public List<string> participantIds = new();
    public FestivalResolutionFactData Clone()
    {
        var copy = (FestivalResolutionFactData)MemberwiseClone();
        copy.participantIds = participantIds == null ? null : new List<string>(participantIds);
        return copy;
    }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(occurrenceId) || occurrenceId != occurrenceId.Trim()
            || string.IsNullOrWhiteSpace(festivalId) || string.IsNullOrWhiteSpace(displayName)
            || absoluteDay < 1 || assignedParticipantCount < 0 || venueCapacity < 0
            || !float.IsFinite(plannedDurationSeconds) || plannedDurationSeconds < 0
            || !float.IsFinite(elapsedFestivalSeconds) || elapsedFestivalSeconds < 0
            || !Enum.IsDefined(typeof(FestivalResolutionGrade), grade) || participantIds == null
            || participantIds.Count > assignedParticipantCount
            || participantIds.Any(id => string.IsNullOrWhiteSpace(id) || id != id.Trim())
            || participantIds.Distinct(StringComparer.Ordinal).Count() != participantIds.Count)
            throw new InvalidOperationException("festival-resolution-fact-invalid");
    }
}

public sealed class FestivalResolutionFact
{
    private readonly FestivalResolutionFactData fact;
    public FestivalResolutionFact(FestivalResolutionFactData fact)
    { this.fact = fact?.Clone() ?? throw new ArgumentNullException(nameof(fact)); this.fact.Validate(); }
    public FestivalResolutionFactData Snapshot() => fact.Clone();
}

// These are domain receipt formats already used by the campaign owner, not log payloads.
public enum SocialRitualFactKind { Funeral = 1, Lineage = 2, ProductionLoss = 3, LastLesson = 4, Promotion = 5, Festival = 6, ContentResolution = 7, ServiceIncident = 8, MentorshipAward = 9 }

[Serializable]
public sealed class SocialRitualPendingFact
{
    public SocialRitualFactKind kind;
    public string payload, publicationKey;
    public SocialRitualPendingFact Clone() => (SocialRitualPendingFact)MemberwiseClone();
}

[Serializable]
public sealed class SocialRitualFactSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<SocialRitualPendingFact> pending = new();
}

internal static class SocialRitualFactCodec
{
    internal static SocialRitualPendingFact Encode<T>(in T fact) => fact switch
    {
        ObservedFuneralLifeEventReceipt v => Row(SocialRitualFactKind.Funeral, v.CanonicalPayload),
        ObservedLineageCompressionLifeEventReceipt v => Row(SocialRitualFactKind.Lineage, v.CanonicalPayload),
        ObservedProductionLossLifeEventReceipt v => Row(SocialRitualFactKind.ProductionLoss, v.CanonicalPayload),
        ObservedLastLessonLifeEventReceipt v => Row(SocialRitualFactKind.LastLesson, v.CanonicalPayload),
        ObservedQuietPromotionLifeEventReceipt v => Row(SocialRitualFactKind.Promotion, v.CanonicalPayload),
        FestivalResolutionFact v => Row(SocialRitualFactKind.Festival, JsonConvert.SerializeObject(v.Snapshot())),
        ContentResolutionFact v => Row(SocialRitualFactKind.ContentResolution, DungeonCharacterWorldSaveCodec.SerializeFields(v.Snapshot())),
        ObservedServiceIncidentFact v => Row(SocialRitualFactKind.ServiceIncident, v.CanonicalPayload),
        CareerMentorshipAwardFact v => Row(SocialRitualFactKind.MentorshipAward, DungeonCharacterWorldSaveCodec.SerializeFields(v.Snapshot())),
        _ => throw new InvalidOperationException("unsupported-social-ritual-fact:" + typeof(T).Name)
    };
    private static SocialRitualPendingFact Row(SocialRitualFactKind kind, string payload) => new() { kind = kind, payload = payload };
    internal static object Decode(SocialRitualPendingFact row)
    {
        if (row == null) throw new InvalidOperationException("social-ritual-fact-missing");
        switch (row.kind)
        {
            case SocialRitualFactKind.Funeral when ObservedFuneralLifeEventReceipt.TryParse(row.payload, out var funeral): return funeral;
            case SocialRitualFactKind.Lineage when ObservedLineageCompressionLifeEventReceipt.TryParse(row.payload, out var lineage): return lineage;
            case SocialRitualFactKind.ProductionLoss when ObservedProductionLossLifeEventReceipt.TryParse(row.payload, out var loss): return loss;
            case SocialRitualFactKind.LastLesson when ObservedLastLessonLifeEventReceipt.TryParse(row.payload, out var lesson): return lesson;
            case SocialRitualFactKind.Promotion when ObservedQuietPromotionLifeEventReceipt.TryParse(row.payload, out var promotion): return promotion;
            case SocialRitualFactKind.Festival:
                var value = new FestivalResolutionFact(JsonConvert.DeserializeObject<FestivalResolutionFactData>(row.payload));
                if (Encode(value).payload != row.payload) break;
                return value;
            case SocialRitualFactKind.ContentResolution:
                var resolved = new ContentResolutionFact(DungeonSaveJson.Deserialize<ContentResolutionFactData>(row.payload));
                if (Encode(resolved).payload != row.payload) break;
                return resolved;
            case SocialRitualFactKind.ServiceIncident:
                var incident = new ObservedServiceIncidentFact(DungeonSaveJson.Deserialize<ObservedMealIncidentEvidenceSaveData>(row.payload));
                if (Encode(incident).payload != row.payload) break;
                return incident;
            case SocialRitualFactKind.MentorshipAward:
                var award = new CareerMentorshipAwardFact(DungeonSaveJson.Deserialize<CareerMentorshipAwardFactData>(row.payload));
                if (Encode(award).payload != row.payload) break;
                return award;
        }
        throw new InvalidOperationException("social-ritual-payload-invalid");
    }
    internal static DomainFactReplay Replay(SocialRitualPendingFact row) => Decode(row) switch
    {
        ObservedFuneralLifeEventReceipt v => DomainFactReplay.Create(row.publicationKey, v),
        ObservedLineageCompressionLifeEventReceipt v => DomainFactReplay.Create(row.publicationKey, v),
        ObservedProductionLossLifeEventReceipt v => DomainFactReplay.Create(row.publicationKey, v),
        ObservedLastLessonLifeEventReceipt v => DomainFactReplay.Create(row.publicationKey, v),
        ObservedQuietPromotionLifeEventReceipt v => DomainFactReplay.Create(row.publicationKey, v),
        FestivalResolutionFact v => DomainFactReplay.Create(row.publicationKey, v),
        ContentResolutionFact v => DomainFactReplay.Create(row.publicationKey, v),
        ObservedServiceIncidentFact v => DomainFactReplay.Create(row.publicationKey, v),
        CareerMentorshipAwardFact v => DomainFactReplay.Create(row.publicationKey, v),
        _ => throw new InvalidOperationException("social-ritual-replay-invalid")
    };
}
