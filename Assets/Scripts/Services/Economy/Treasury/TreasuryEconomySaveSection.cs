using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class TreasuryEconomySaveSection :
    DungeonStrictJsonSaveSection<
        TreasuryEconomySaveData,
        TreasuryEconomyRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.treasury";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id
    };

    private readonly ITreasuryEconomyPersistence persistence;

    public TreasuryEconomySaveSection(
        ITreasuryEconomyPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => TreasuryEconomySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override TreasuryEconomySaveData CapturePayload() =>
        persistence.Capture();

    protected override void ValidateRawPayload(string payloadJson)
        => ValidateObservationJson(payloadJson);

    public static void ValidateObservationJson(string payloadJson)
    {
        var root = JObject.Parse(payloadJson, new JsonLoadSettings
        { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (root["autoProcurement"] is not JObject procurement || procurement["pendingFacts"] is not JArray)
            throw new InvalidOperationException("Treasury payload is missing the procurement pendingFacts array.");
        if (root["warehouseSupply"] is not JObject supply || supply["nextSequence"]?.Type != JTokenType.Integer
            || supply["pending"] is not JArray pending)
            throw new InvalidOperationException("Treasury payload is missing the warehouse supply observation state.");
        foreach (var row in pending)
        {
            if (row is not JObject || row["publicationKey"]?.Type != JTokenType.String
                || row["fact"] is not JObject fact || fact["sequence"]?.Type != JTokenType.Integer
                || fact["absoluteDay"]?.Type != JTokenType.Integer || fact["result"] is not JObject)
                throw new InvalidOperationException("Treasury warehouse supply observation shape is invalid.");
            var parsed = DungeonSaveJson.Deserialize<StockSupplyCompletedFactData>(fact.ToString());
            if (!JToken.DeepEquals(fact, JObject.Parse(DungeonCharacterWorldSaveCodec.SerializeFields(new StockSupplyCompletedFact(parsed).Snapshot()))))
                throw new InvalidOperationException("Treasury warehouse supply observation fields are incomplete.");
        }
    }

    protected override void NormalizeRestorePayload(
        TreasuryEconomySaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override TreasuryEconomyRestoreCandidate BuildRestoreCandidate(
        TreasuryEconomySaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        TreasuryEconomyRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
