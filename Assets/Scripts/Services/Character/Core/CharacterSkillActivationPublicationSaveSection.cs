using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class CharacterSkillActivationPublicationSaveSection :
    DungeonStrictJsonSaveSection<CharacterSkillActivationPublicationSaveData, CharacterSkillActivationPublicationSaveData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.skill-publications";
    private readonly CharacterSkillActivationPublicationRuntime owner;
    public CharacterSkillActivationPublicationSaveSection(CharacterSkillActivationPublicationRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => CharacterSkillActivationPublicationSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => new[] { CharacterWorldSaveSection.Id };
    protected override CharacterSkillActivationPublicationSaveData CapturePayload() => owner.Capture();
    protected override CharacterSkillActivationPublicationSaveData BuildRestoreCandidate(CharacterSkillActivationPublicationSaveData source) =>
        CharacterSkillActivationPublicationRuntime.ValidateAndClone(source);
    protected override void PublishRestoreCandidate(CharacterSkillActivationPublicationSaveData candidate) => owner.Restore(candidate);
    protected override void ValidateRawPayload(string json) => ValidateShape(JObject.Parse(json), typeof(CharacterSkillActivationPublicationSaveData));
    private static void ValidateShape(JToken token, Type type)
    {
        bool valid;
        if (type == typeof(string)) valid = token?.Type == JTokenType.String;
        else if (type == typeof(bool)) valid = token?.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token?.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token?.Type == JTokenType.Integer;
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values) throw new InvalidOperationException("skill-publication-json-array-missing");
            foreach (var value in values) ValidateShape(value, type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject value) throw new InvalidOperationException("skill-publication-json-object-missing");
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                ValidateShape(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("skill-publication-json-field-invalid:" + type.Name);
    }
}
