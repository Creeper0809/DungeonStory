using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using DungeonStory.Infrastructure;

public sealed class OperatingDayFactSaveSection :
    DungeonStrictJsonSaveSection<
        OperatingDayFactJournalData,
        OperatingDayFactJournalData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "operation.settlement-observations";

    private readonly OperatingDayFactJournal owner;

    public OperatingDayFactSaveSection(
        OperatingDayFactJournal owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public override string SectionId => Id;
    public override int SectionVersion =>
        OperatingDayFactJournalData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[]
        {
            OperatingDaySettlementSaveSection.Id
        };

    protected override OperatingDayFactJournalData CapturePayload() =>
        owner.Capture();

    protected override string SerializePayload(
        OperatingDayFactJournalData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);

    protected override OperatingDayFactJournalData BuildRestoreCandidate(
        OperatingDayFactJournalData source) =>
        OperatingDayFactJournal.ValidateAndClone(source);

    protected override void PublishRestoreCandidate(
        OperatingDayFactJournalData candidate) => owner.Restore(candidate);

    protected override void ValidateRawPayload(string json) => ValidateJson(json);

    public static void ValidateJson(string json) => ValidateShape(
        JObject.Parse(
            json,
            new JsonLoadSettings
            {
                DuplicatePropertyNameHandling =
                    DuplicatePropertyNameHandling.Error
            }),
        typeof(OperatingDayFactJournalData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null)
        {
            throw new InvalidOperationException(
                "operating-day-observation-json-field-missing:"
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
                    "operating-day-observation-json-array-missing:"
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
                    "operating-day-observation-json-object-missing:"
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
                "operating-day-observation-json-field-invalid:"
                + type.Name);
        }
    }
}
