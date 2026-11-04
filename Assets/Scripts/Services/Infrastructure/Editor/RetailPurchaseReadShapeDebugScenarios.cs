#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

public static class RetailPurchaseReadShapeDebugScenarios
{
    public static void VerifyForBatch()
    {
        const string characters = "{\"actors\":[]}";
        const string ledger = "{\"outbox\":[],\"exactOutcomes\":[],\"tombstones\":[]}";
        const string counters = "{\"owners\":[]}";
        var valid = Sections(characters, ledger, counters);
        Compare(valid);
        using (DungeonSaveJson.BeginReadScope())
        {
            var first = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(ledger);
            DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(characters);
            first.outbox = null;
            long copies = DungeonSaveJson.EditorSnapshotCopyCount;
            long shapes = DungeonSaveJson.EditorRootShapeReplayCount;
            Require(DungeonSaveJson.HasNonNullRootFields<GameplayOutcomeLedgerSaveData>(ledger,
                nameof(GameplayOutcomeLedgerSaveData.outbox)), "Shape query exposed the caller's mutable copy.");
            Require(DungeonSaveJson.EditorSnapshotCopyCount == copies
                && DungeonSaveJson.EditorRootShapeReplayCount == shapes + 1,
                "A shape query cloned the complete cached ledger.");
            var report = new DungeonGameRestoreReport();
            new RetailPurchaseFactPreflight().Validate(valid, report);
            Require(report.Success && DungeonSaveJson.EditorSnapshotCopyCount == copies + 1,
                "Empty retail validation copied the cached ledger or skipped the character copy.");
            Require(DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(ledger).outbox != null,
                "Shape query changed the private snapshot.");
        }
        foreach (string bad in new[] {
            "{\"outbox\":null,\"exactOutcomes\":[],\"tombstones\":[]}",
            "{\"outbox\":[],\"exactOutcomes\":[{\"sequence\":\"bad\"}],\"tombstones\":[]}",
            "{\"outbox\":[],\"exactOutcomes\":[],\"tombstones\":[],\"revision\":\"bad\"}",
            "null", "{} {}", "{\"outbox\":" })
            Compare(Sections(characters, bad, counters));
        Compare(Sections(characters, "{\"outbox\":null}", "{\"owners\":\"bad\"}"));
        Compare(Sections(characters, ledger, "{\"owners\":null}"));
        Compare(Sections("{\"actors\":[{\"pendingRetailPurchases\":null}]}", ledger, counters));
        Compare(Sections("{\"actors\":[],\"actors\":[]}", ledger, counters));
        var missing = Sections(characters, ledger, counters);
        missing.Remove(GameplayOutcomeLedgerSaveSection.Id);
        Compare(missing);
        var wrongVersion = Sections(characters, ledger, counters);
        wrongVersion[MigratedProducerOutcomeSaveSection.Id].sectionVersion++;
        Compare(wrongVersion);
        using (DungeonSaveJson.BeginReadScope())
        {
            string bad = "{\"revision\":\"bad\"}";
            long parses = DungeonSaveJson.EditorSourceParseCount;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    DungeonSaveJson.HasNonNullRootFields<GameplayOutcomeLedgerSaveData>(bad, "outbox");
                    throw new InvalidOperationException("Invalid typed JSON was accepted.");
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 2,
                "Failed typed shape validation entered the success memo.");
        }
        RetailPurchaseRecoveryB0T04DebugScenarios.VerifyReadShapePendingRecoveryForBatch();
    }

    internal static Dictionary<string, DungeonSaveSectionEnvelope> Sections(string characters, string ledger, string counters) => new()
    {
        [CharacterWorldSaveSection.Id] = new() { sectionId = CharacterWorldSaveSection.Id,
            sectionVersion = CharacterWorldSaveSection.CurrentVersion, payloadJson = characters },
        [GameplayOutcomeLedgerSaveSection.Id] = new() { sectionId = GameplayOutcomeLedgerSaveSection.Id,
            sectionVersion = GameplayOutcomeLedgerSaveData.CurrentVersion, payloadJson = ledger },
        [MigratedProducerOutcomeSaveSection.Id] = new() { sectionId = MigratedProducerOutcomeSaveSection.Id,
            sectionVersion = DungeonMigratedProducerOutcomeSaveData.CurrentVersion, payloadJson = counters }
    };

    private static void Compare(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections)
    {
        string expected = Baseline(sections);
        var report = new DungeonGameRestoreReport();
        new RetailPurchaseFactPreflight().Validate(sections, report);
        Require(string.Join(" | ", report.Errors) == expected, "Retail preflight rejection/order changed.");
        using (DungeonSaveJson.BeginReadScope())
        {
            report = new DungeonGameRestoreReport();
            new RetailPurchaseFactPreflight().Validate(sections, report);
            Require(string.Join(" | ", report.Errors) == expected, "Scoped retail rejection/order changed.");
        }
    }

    private static string Baseline(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections)
    {
        try
        {
            if (!sections.TryGetValue(CharacterWorldSaveSection.Id, out var section)
                || section.sectionVersion != CharacterWorldSaveSection.CurrentVersion)
                throw new InvalidOperationException("retail-observation-character-section-missing");
            CharacterWorldSaveSection.ValidateRetailPurchaseJson(section.payloadJson);
            var characters = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(section.payloadJson);
            RetailPurchaseFactPreflight.ValidatePending(characters,
                Read<GameplayOutcomeLedgerSaveData>(GameplayOutcomeLedgerSaveSection.Id, GameplayOutcomeLedgerSaveData.CurrentVersion),
                Read<DungeonMigratedProducerOutcomeSaveData>(MigratedProducerOutcomeSaveSection.Id, DungeonMigratedProducerOutcomeSaveData.CurrentVersion));
            return string.Empty;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { return "retail-observation-save-join:" + error.Message; }

        T Read<T>(string id, int version) where T : class
        {
            if (!sections.TryGetValue(id, out var row) || row == null || row.sectionVersion != version)
                throw new InvalidOperationException("retail-observation-section-missing:" + id);
            return DungeonSaveJson.Deserialize<T>(row.payloadJson);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

public static partial class RetailPurchaseRecoveryB0T04DebugScenarios
{
    public static void VerifyReadShapePendingRecoveryForBatch()
    {
        ObservationRecovery(false);
        ObservationRecovery(true);
        using var context = new Context(1);
        context.BindVisit();
        context.Facts.HideNextAcknowledgement = true;
        RequireRetailFailure(context.RunBoundPurchase, "durably pending recovery");
        var characters = context.CharacterCheckpoint();
        var native = context.Fixture.Ledger.CaptureGameplayOutcomes();
        var counters = context.Fixture.Transaction.Capture();
        var sections = RetailPurchaseReadShapeDebugScenarios.Sections(
            DungeonCharacterWorldSaveCodec.SerializeFields(characters),
            DungeonCharacterWorldSaveCodec.SerializeFields(native),
            DungeonCharacterWorldSaveCodec.SerializeFields(counters));
        var report = new DungeonGameRestoreReport();
        using (DungeonSaveJson.BeginReadScope()) new RetailPurchaseFactPreflight().Validate(sections, report);
        if (!report.Success) throw new InvalidOperationException("Actual pending retail recovery was rejected: " + report.FailureDescription);
        characters.actors[0].pendingRetailPurchases[0].buyerMoneyBefore++;
        characters.actors[0].pendingRetailPurchases[0].buyerMoneyAfter++;
        sections[CharacterWorldSaveSection.Id].payloadJson = DungeonCharacterWorldSaveCodec.SerializeFields(characters);
        report = new DungeonGameRestoreReport();
        using (DungeonSaveJson.BeginReadScope()) new RetailPurchaseFactPreflight().Validate(sections, report);
        if (report.Success || !report.Errors.Any(value => value.Contains("canonical-fact-conflict")))
            throw new InvalidOperationException("Changed actual retail proof escaped canonical validation.");
    }
}
#endif
