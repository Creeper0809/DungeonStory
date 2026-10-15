using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class EquipmentModuleSelectionPublicationSaveSection :
    DungeonStrictJsonSaveSection<EquipmentModuleSelectionPublicationSaveData, EquipmentModuleSelectionPublicationSaveData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "equipment.module-selections";
    private readonly EquipmentModuleSelectionPublicationRuntime owner;
    public EquipmentModuleSelectionPublicationSaveSection(EquipmentModuleSelectionPublicationRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => EquipmentModuleSelectionPublicationSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[] { EquipmentEvolutionSaveSection.Id };
    protected override EquipmentModuleSelectionPublicationSaveData CapturePayload() => owner.Capture();
    protected override string SerializePayload(EquipmentModuleSelectionPublicationSaveData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);
    protected override EquipmentModuleSelectionPublicationSaveData BuildRestoreCandidate(EquipmentModuleSelectionPublicationSaveData source) =>
        EquipmentModuleSelectionPublicationRuntime.ValidateAndClone(source);
    protected override void PublishRestoreCandidate(EquipmentModuleSelectionPublicationSaveData candidate) => owner.Restore(candidate);
    protected override void ValidateRawPayload(string json) => ValidateJson(json);
    public static void ValidateJson(string json) => ValidateShape(JObject.Parse(json,
        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }),
        typeof(EquipmentModuleSelectionPublicationSaveData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null) throw new InvalidOperationException("equipment-module-selection-json-field-missing:" + type.Name);
        bool valid;
        if (type == typeof(string)) valid = token.Type == JTokenType.String;
        else if (type == typeof(bool)) valid = token.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token.Type == JTokenType.Integer;
        else if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values) throw new InvalidOperationException("equipment-module-selection-json-array-missing");
            foreach (var value in values) ValidateShape(value, type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject value) throw new InvalidOperationException("equipment-module-selection-json-object-missing:" + type.Name);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                ValidateShape(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("equipment-module-selection-json-field-invalid:" + type.Name);
    }
}
