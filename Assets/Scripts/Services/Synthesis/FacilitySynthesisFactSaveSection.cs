using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class FacilitySynthesisFactSaveSection :
    DungeonStrictJsonSaveSection<
        FacilitySynthesisFactSaveData,
        FacilitySynthesisFactSaveData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "facility.synthesis-observations";

    private readonly FacilitySynthesisFactOutbox owner;

    public FacilitySynthesisFactSaveSection(FacilitySynthesisFactOutbox owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public override string SectionId => Id;
    public override int SectionVersion => FacilitySynthesisFactSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[] { ModularFacilityWorldSaveSection.Id };

    protected override FacilitySynthesisFactSaveData CapturePayload() =>
        owner.Capture();

    protected override string SerializePayload(
        FacilitySynthesisFactSaveData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);

    protected override FacilitySynthesisFactSaveData BuildRestoreCandidate(
        FacilitySynthesisFactSaveData source) =>
        FacilitySynthesisFactOutbox.ValidateAndClone(source);

    protected override void PublishRestoreCandidate(
        FacilitySynthesisFactSaveData candidate) => owner.Restore(candidate);

    protected override void ValidateRawPayload(string json) => ValidateJson(json);

    public static void ValidateJson(string json) => ValidateShape(
        JObject.Parse(
            json,
            new JsonLoadSettings
            {
                DuplicatePropertyNameHandling =
                    DuplicatePropertyNameHandling.Error
            }),
        typeof(FacilitySynthesisFactSaveData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null)
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-json-field-missing:"
                + type.Name);
        }

        bool valid;
        if (type == typeof(string))
        {
            valid = token.Type == JTokenType.String;
        }
        else if (type == typeof(int) || type == typeof(long) || type.IsEnum)
        {
            valid = token.Type == JTokenType.Integer;
        }
        else if (type.IsArray
            || type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values)
            {
                throw new InvalidOperationException(
                    "facility-synthesis-observation-json-array-missing:"
                    + type.Name);
            }

            Type elementType = type.IsArray
                ? type.GetElementType()
                : type.GetGenericArguments()[0];
            foreach (JToken value in values)
                ValidateShape(value, elementType);
            return;
        }
        else
        {
            if (token is not JObject value)
            {
                throw new InvalidOperationException(
                    "facility-synthesis-observation-json-object-missing:"
                    + type.Name);
            }

            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Public | BindingFlags.Instance))
            {
                ValidateShape(value[field.Name], field.FieldType);
            }
            return;
        }

        if (!valid)
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-json-field-invalid:"
                + type.Name);
        }
    }
}
