using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class MemoryErasurePublicationSaveSection :
    DungeonStrictJsonSaveSection<MemoryErasurePublicationSaveData, MemoryErasurePublicationSaveData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.memory-erasure";
    private readonly MemoryErasurePublicationRuntime owner;
    public MemoryErasurePublicationSaveSection(MemoryErasurePublicationRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => MemoryErasurePublicationSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => new[] { CharacterWorldSaveSection.Id };
    protected override MemoryErasurePublicationSaveData CapturePayload() => owner.Capture();
    protected override string SerializePayload(MemoryErasurePublicationSaveData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);
    protected override MemoryErasurePublicationSaveData BuildRestoreCandidate(MemoryErasurePublicationSaveData source) =>
        MemoryErasurePublicationRuntime.ValidateAndClone(source);
    protected override void PublishRestoreCandidate(MemoryErasurePublicationSaveData candidate) => owner.Restore(candidate);
    protected override void ValidateRawPayload(string json) => ValidateJson(json);
    public static void ValidateJson(string json) => ValidateShape(JObject.Parse(json,
        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }),
        typeof(MemoryErasurePublicationSaveData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null) throw new InvalidOperationException("memory-erasure-json-field-missing:" + type.Name);
        bool valid;
        if (type == typeof(string)) valid = token.Type == JTokenType.String;
        else if (type == typeof(bool)) valid = token.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token.Type == JTokenType.Integer;
        else if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values) throw new InvalidOperationException("memory-erasure-json-array-missing");
            foreach (var value in values) ValidateShape(value, type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject value) throw new InvalidOperationException("memory-erasure-json-object-missing:" + type.Name);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                ValidateShape(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("memory-erasure-json-field-invalid:" + type.Name);
    }
}
