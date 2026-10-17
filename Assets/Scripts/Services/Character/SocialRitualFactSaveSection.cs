using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class SocialRitualFactSaveSection :
    DungeonStrictJsonSaveSection<SocialRitualFactSaveData, SocialRitualFactSaveData>, IDungeonRollbackFreeSaveSection
{
    public const string Id = "social.ritual-facts";
    private readonly SocialRitualFactJournal journal;
    public SocialRitualFactSaveSection(SocialRitualFactJournal journal) => this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
    public override string SectionId => Id;
    public override int SectionVersion => SocialRitualFactSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[] { FestivalExecutionSaveSection.Id, SocietyEventsSaveSection.Id, KinshipHouseholdSaveSection.Id };
    protected override SocialRitualFactSaveData CapturePayload() => journal.Capture();
    protected override SocialRitualFactSaveData BuildRestoreCandidate(SocialRitualFactSaveData source) => SocialRitualFactJournal.ValidateAndClone(source);
    protected override void PublishRestoreCandidate(SocialRitualFactSaveData source) => journal.Restore(source);
    protected override void ValidateRawPayload(string json) => ValidateJson(json);
    public static void ValidateJson(string json)
    {
        var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (root["version"]?.Type != JTokenType.Integer || root["pending"] is not JArray rows)
            throw new InvalidOperationException("social-ritual-save-shape-invalid");
        foreach (var row in rows)
            if (row is not JObject || row["kind"]?.Type != JTokenType.Integer
                || row["payload"]?.Type != JTokenType.String || row["publicationKey"]?.Type != JTokenType.String)
                throw new InvalidOperationException("social-ritual-row-shape-invalid");
    }
}
