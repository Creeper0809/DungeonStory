using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class CharacterSkillAcquisitionPublicationSaveSection :
    DungeonStrictJsonSaveSection<CharacterSkillAcquisitionPublicationSaveData, CharacterSkillAcquisitionPublicationSaveData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.skill-acquisitions";
    private readonly CharacterSkillAcquisitionPublicationRuntime owner;
    public CharacterSkillAcquisitionPublicationSaveSection(CharacterSkillAcquisitionPublicationRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => CharacterSkillAcquisitionPublicationSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => new[] { CharacterWorldSaveSection.Id };
    protected override CharacterSkillAcquisitionPublicationSaveData CapturePayload() => owner.Capture();
    protected override string SerializePayload(CharacterSkillAcquisitionPublicationSaveData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);
    protected override CharacterSkillAcquisitionPublicationSaveData BuildRestoreCandidate(CharacterSkillAcquisitionPublicationSaveData source) =>
        CharacterSkillAcquisitionPublicationRuntime.ValidateAndClone(source);
    protected override void PublishRestoreCandidate(CharacterSkillAcquisitionPublicationSaveData candidate) => owner.Restore(candidate);
    protected override void ValidateRawPayload(string json) => ValidateJson(json);
    public static void ValidateJson(string json) => ValidateShape(JObject.Parse(json,
        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }),
        typeof(CharacterSkillAcquisitionPublicationSaveData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null) throw new InvalidOperationException("skill-acquisition-json-field-missing:" + type.Name);
        // These are optional parts of the actual selected skill, not missing observations.
        if (token.Type == JTokenType.Null && (type == typeof(NarrativeGenerationTrace)
            || type == typeof(CharacterSkillDrawbackEffectEnvelope) || type == typeof(string[]))) return;
        bool valid;
        if (type == typeof(string)) valid = token.Type == JTokenType.String;
        else if (type == typeof(bool)) valid = token.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token.Type == JTokenType.Integer;
        else if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values) throw new InvalidOperationException("skill-acquisition-json-array-missing");
            foreach (var value in values) ValidateShape(value, type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject value) throw new InvalidOperationException("skill-acquisition-json-object-missing:" + type.Name);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                ValidateShape(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("skill-acquisition-json-field-invalid:" + type.Name);
    }
}
