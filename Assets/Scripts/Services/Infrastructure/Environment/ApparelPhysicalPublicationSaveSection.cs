using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class ApparelPhysicalPublicationSaveSection :
    DungeonStrictJsonSaveSection<ApparelPhysicalPublicationSaveData, ApparelPhysicalPublicationSaveData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "equipment.apparel-physical";
    private readonly ApparelPhysicalPublicationRuntime owner;
    public ApparelPhysicalPublicationSaveSection(ApparelPhysicalPublicationRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => ApparelPhysicalPublicationSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[] { CharacterEnvironmentSaveSection.Id };
    protected override ApparelPhysicalPublicationSaveData CapturePayload() => owner.Capture();
    protected override string SerializePayload(ApparelPhysicalPublicationSaveData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);
    protected override ApparelPhysicalPublicationSaveData BuildRestoreCandidate(ApparelPhysicalPublicationSaveData source) =>
        ApparelPhysicalPublicationRuntime.ValidateAndClone(source);
    protected override void PublishRestoreCandidate(ApparelPhysicalPublicationSaveData candidate) => owner.Restore(candidate);
    protected override void ValidateRawPayload(string json) => ValidateJson(json);
    public static void ValidateJson(string json) => ValidateShape(JObject.Parse(json,
        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }),
        typeof(ApparelPhysicalPublicationSaveData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null) throw new InvalidOperationException("apparel-physical-json-field-missing:" + type.Name);
        bool valid;
        if (type == typeof(string)) valid = token.Type == JTokenType.String;
        else if (type == typeof(bool)) valid = token.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token.Type == JTokenType.Integer;
        else if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values) throw new InvalidOperationException("apparel-physical-json-array-missing");
            foreach (var value in values) ValidateShape(value, type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject value) throw new InvalidOperationException("apparel-physical-json-object-missing:" + type.Name);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                ValidateShape(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("apparel-physical-json-field-invalid:" + type.Name);
    }
}
