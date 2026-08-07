using System;
using System.Collections.Generic;

public readonly struct CharacterBackgroundId : IEquatable<CharacterBackgroundId>
{
    public CharacterBackgroundId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value.StartsWith("background:", StringComparison.Ordinal);
    public bool Equals(CharacterBackgroundId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is CharacterBackgroundId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}

public readonly struct CharacterAmbitionId : IEquatable<CharacterAmbitionId>
{
    public CharacterAmbitionId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value.StartsWith("ambition:", StringComparison.Ordinal);
    public bool Equals(CharacterAmbitionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is CharacterAmbitionId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}

public readonly struct SpeciesCultureId : IEquatable<SpeciesCultureId>
{
    public SpeciesCultureId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value.StartsWith("culture:", StringComparison.Ordinal);
    public bool Equals(SpeciesCultureId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is SpeciesCultureId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}

public readonly struct NarrativeEventId : IEquatable<NarrativeEventId>
{
    public NarrativeEventId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value.StartsWith("life-event:", StringComparison.Ordinal);
    public bool Equals(NarrativeEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is NarrativeEventId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}

public enum CharacterAmbitionStatus
{
    None,
    Active,
    Completed,
    Failed,
    Abandoned
}

public interface ICharacterNarrativeCatalog
{
    IReadOnlyList<CharacterBackgroundDefinitionSO> Backgrounds { get; }
    IReadOnlyList<CharacterAmbitionDefinitionSO> Ambitions { get; }
    IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    IReadOnlyList<SpeciesCultureDefinitionSO> Cultures { get; }
    IReadOnlyList<CulturalPracticeDefinitionSO> Practices { get; }
    CharacterBackgroundDefinitionSO Require(CharacterBackgroundId id);
    CharacterAmbitionDefinitionSO Require(CharacterAmbitionId id);
    LifeEventDefinitionSO Require(NarrativeEventId id);
    SpeciesCultureDefinitionSO Require(SpeciesCultureId id);
    SpeciesCultureDefinitionSO RequireDefaultCulture(string speciesId);
}

public interface ICharacterNarrativeQuery
{
    int Version { get; }
    IReadOnlyCollection<CharacterNarrativeSnapshot> All { get; }
    bool TryGet(CharacterId characterId, out CharacterNarrativeSnapshot snapshot);
}

public interface ICharacterNarrativeCommand
{
    CharacterNarrativeSnapshot Register(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressedHeritableTraitIds,
        IReadOnlyList<string> latentHeritableTraitIds);
    CharacterNarrativeSnapshot RegisterEnemyOrigin(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressedHeritableTraitIds,
        IReadOnlyList<string> latentHeritableTraitIds,
        CharacterBackgroundId backgroundId,
        SpeciesCultureId cultureId,
        string enemyArchetypeId,
        string originFactionId,
        string militaryTrainingId,
        float loyalty);
    void StartAmbition(CharacterId characterId, CharacterAmbitionId ambitionId, int absoluteDay);
    void AddAmbitionProgress(CharacterId characterId, int amount, int absoluteDay);
    void FailAmbition(CharacterId characterId, int absoluteDay);
    void AbandonAmbition(CharacterId characterId, int absoluteDay);
    void BeginAssimilation(CharacterId characterId, SpeciesCultureId targetCultureId);
    void AdvanceAssimilationDay(CharacterId characterId);
    void RecordResolvedEvent(
        CharacterId characterId,
        NarrativeEventId eventId,
        string choiceId,
        int absoluteDay);
}

public interface ICharacterNarrativePersistence
{
    CharacterNarrativeWorldSaveData Capture();
    CharacterNarrativeAggregateState PrepareRestore(CharacterNarrativeWorldSaveData data);
    void PublishRestore(CharacterNarrativeAggregateState candidate);
}

public sealed class CharacterNarrativeSnapshot
{
    public CharacterId CharacterId { get; internal set; }
    public CharacterBackgroundId BackgroundId { get; internal set; }
    public SpeciesCultureId CultureId { get; internal set; }
    public CharacterAmbitionId ActiveAmbitionId { get; internal set; }
    public CharacterAmbitionStatus AmbitionStatus { get; internal set; }
    public int AmbitionProgress { get; internal set; }
    public int NextAmbitionAllowedAbsoluteDay { get; internal set; }
    public SpeciesCultureId AssimilationTargetCultureId { get; internal set; }
    public int AssimilationDays { get; internal set; }
    public IReadOnlyList<string> ExpressedHeritableTraitIds { get; internal set; }
    public IReadOnlyList<string> LatentHeritableTraitIds { get; internal set; }
    public IReadOnlyList<CharacterNarrativeEventSaveData> RecentEvents { get; internal set; }
    public IReadOnlyList<CharacterNarrativeEventSummarySaveData> EventSummaries { get; internal set; }
    public string OriginEnemyArchetypeId { get; internal set; }
    public string OriginFactionId { get; internal set; }
    public string MilitaryTrainingId { get; internal set; }
    public float Loyalty { get; internal set; }
    public bool HasEnemyOrigin => !string.IsNullOrWhiteSpace(OriginEnemyArchetypeId);
}

[Serializable]
public sealed class CharacterNarrativeWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterNarrativeSaveData> characters = new();
}

[Serializable]
public sealed class CharacterNarrativeSaveData
{
    public string characterId = string.Empty;
    public string phenotypeSpeciesId = string.Empty;
    public string backgroundId = string.Empty;
    public string cultureId = string.Empty;
    public string activeAmbitionId = string.Empty;
    public CharacterAmbitionStatus ambitionStatus;
    public int ambitionProgress;
    public int nextAmbitionAllowedAbsoluteDay;
    public string assimilationTargetCultureId = string.Empty;
    public int assimilationDays;
    public List<string> expressedHeritableTraitIds = new();
    public List<string> latentHeritableTraitIds = new();
    public List<CharacterNarrativeEventSaveData> recentEvents = new();
    public List<CharacterNarrativeEventSummarySaveData> eventSummaries = new();
    public string originEnemyArchetypeId = string.Empty;
    public string originFactionId = string.Empty;
    public string militaryTrainingId = string.Empty;
    public float loyalty;
}

[Serializable]
public sealed class CharacterNarrativeEventSaveData
{
    public string eventId = string.Empty;
    public string choiceId = string.Empty;
    public int absoluteDay;
}

[Serializable]
public sealed class CharacterNarrativeEventSummarySaveData
{
    public LifeEventCategory category;
    public int count;
    public int lastAbsoluteDay;
}
