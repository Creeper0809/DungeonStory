using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class ShopliftingFactSaveSection :
    DungeonStrictJsonSaveSection<
        ShopliftingFactJournalData,
        ShopliftingFactJournalData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "shoplifting.observations";

    private readonly ShopliftingFactJournal owner;

    public ShopliftingFactSaveSection(
        ShopliftingFactJournal owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public override string SectionId => Id;
    public override int SectionVersion =>
        ShopliftingFactJournalData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[]
        {
            ModularFacilityWorldSaveSection.Id,
            PhysicalItemsSaveSection.Id
        };

    protected override ShopliftingFactJournalData CapturePayload() =>
        owner.Capture();

    protected override string SerializePayload(
        ShopliftingFactJournalData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);

    protected override ShopliftingFactJournalData BuildRestoreCandidate(
        ShopliftingFactJournalData source) =>
        ShopliftingFactJournal.ValidateAndClone(source);

    protected override void PublishRestoreCandidate(
        ShopliftingFactJournalData candidate) => owner.Restore(candidate);

    protected override void ValidateRawPayload(string json) => ValidateJson(json);

    public static void ValidateJson(string json) => ValidateShape(
        JObject.Parse(
            json,
            new JsonLoadSettings
            {
                DuplicatePropertyNameHandling =
                    DuplicatePropertyNameHandling.Error
            }),
        typeof(ShopliftingFactJournalData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null)
        {
            throw new InvalidOperationException(
                "shoplifting-observation-json-field-missing:"
                + type.Name);
        }

        bool valid;
        if (type == typeof(string))
        {
            valid = token.Type == JTokenType.String;
        }
        else if (type == typeof(bool))
        {
            valid = token.Type == JTokenType.Boolean;
        }
        else if (type == typeof(int) || type == typeof(long) || type.IsEnum)
        {
            valid = token.Type == JTokenType.Integer;
        }
        else if (type == typeof(float) || type == typeof(double))
        {
            valid = token.Type == JTokenType.Float || token.Type == JTokenType.Integer;
        }
        else if (type.IsArray
            || type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values)
            {
                throw new InvalidOperationException(
                    "shoplifting-observation-json-array-missing:"
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
                    "shoplifting-observation-json-object-missing:"
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
                "shoplifting-observation-json-field-invalid:"
                + type.Name);
        }
    }
}
