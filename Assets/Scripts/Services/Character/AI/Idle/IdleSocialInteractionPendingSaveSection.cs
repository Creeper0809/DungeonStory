using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class IdleSocialInteractionPendingSaveSection :
    DungeonStrictJsonSaveSection<IdleSocialInteractionPendingSaveData, IdleSocialInteractionPendingState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "social.idle-pending";
    private readonly IdleSocialInteractionCommandRuntime owner;
    public IdleSocialInteractionPendingSaveSection(IdleSocialInteractionCommandRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => IdleSocialInteractionPendingSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Array.Empty<string>();
    protected override IdleSocialInteractionPendingSaveData CapturePayload() => owner.Capture();
    protected override IdleSocialInteractionPendingState BuildRestoreCandidate(IdleSocialInteractionPendingSaveData payload) => owner.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(IdleSocialInteractionPendingState candidate) => owner.PublishRestore(candidate);
    protected override void ValidateRawPayload(string json)
    {
        var root = JObject.Parse(json);
        if (root["version"]?.Type != JTokenType.Integer
            || root["version"].Value<int>() != IdleSocialInteractionPendingSaveData.CurrentVersion
            || root["pending"] is not JArray pending)
            throw new InvalidOperationException("idle-social-pending-json-shape-invalid");
        foreach (var item in pending)
        {
            if (item is not JObject entry || entry["publicationKey"]?.Type != JTokenType.String || entry["fact"] is not JObject fact)
                throw new InvalidOperationException("idle-social-pending-json-entry-invalid");
            foreach (var field in typeof(IdleSocialInteractionFactData).GetFields())
            {
                var value = fact[field.Name];
                bool valid = field.FieldType == typeof(string) ? value?.Type == JTokenType.String
                    : field.FieldType == typeof(int) ? value?.Type == JTokenType.Integer
                    : value?.Type is JTokenType.Integer or JTokenType.Float;
                if (!valid) throw new InvalidOperationException("idle-social-pending-json-field-invalid:" + field.Name);
            }
        }
    }
}
