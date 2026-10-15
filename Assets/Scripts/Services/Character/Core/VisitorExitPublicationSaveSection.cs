using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class VisitorExitPublicationSaveSection :
    DungeonStrictJsonSaveSection<VisitorExitPublicationSaveData, VisitorExitPublicationSaveData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "character.visitor-exit";
    private readonly VisitorExitPublicationRuntime owner;
    public VisitorExitPublicationSaveSection(VisitorExitPublicationRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => VisitorExitPublicationSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[] { CharacterWorldSaveSection.Id };
    protected override VisitorExitPublicationSaveData CapturePayload() => owner.Capture();
    protected override string SerializePayload(VisitorExitPublicationSaveData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);
    protected override VisitorExitPublicationSaveData BuildRestoreCandidate(VisitorExitPublicationSaveData source) =>
        VisitorExitPublicationRuntime.ValidateAndClone(source);
    protected override void PublishRestoreCandidate(VisitorExitPublicationSaveData candidate) => owner.Restore(candidate);
    protected override void ValidateRawPayload(string json) => ValidateJson(json);
    public static void ValidateJson(string json) => ValidateShape(JObject.Parse(json,
        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }),
        typeof(VisitorExitPublicationSaveData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null) throw new InvalidOperationException("visitor-exit-json-field-missing:" + type.Name);
        if (token.Type == JTokenType.Null && (type == typeof(FloorItemStolenFactData)
            || type == typeof(VisitorDepartureFactData))) return;
        bool valid;
        if (type == typeof(string)) valid = token.Type == JTokenType.String;
        else if (type == typeof(bool)) valid = token.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token.Type == JTokenType.Integer;
        else if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values) throw new InvalidOperationException("visitor-exit-json-array-missing");
            foreach (var value in values) ValidateShape(value, type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject value) throw new InvalidOperationException("visitor-exit-json-object-missing:" + type.Name);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                ValidateShape(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("visitor-exit-json-field-invalid:" + type.Name);
    }
}
