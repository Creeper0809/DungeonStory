using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed class FacilityShopPurchaseFactSaveSection :
    DungeonStrictJsonSaveSection<
        FacilityShopPurchaseJournalData,
        FacilityShopPurchaseJournalData>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "facility-shop.observations";

    private readonly FacilityShopPurchaseJournal owner;

    public FacilityShopPurchaseFactSaveSection(
        FacilityShopPurchaseJournal owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public override string SectionId => Id;
    public override int SectionVersion =>
        FacilityShopPurchaseJournalData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[]
        {
            FacilityShopSaveSection.Id,
            BlueprintResearchSaveSection.Id
        };

    protected override FacilityShopPurchaseJournalData CapturePayload() =>
        owner.Capture();

    protected override string SerializePayload(
        FacilityShopPurchaseJournalData payload) =>
        DungeonCharacterWorldSaveCodec.SerializeFields(payload);

    protected override FacilityShopPurchaseJournalData BuildRestoreCandidate(
        FacilityShopPurchaseJournalData source) =>
        FacilityShopPurchaseJournal.ValidateAndClone(source);

    protected override void PublishRestoreCandidate(
        FacilityShopPurchaseJournalData candidate) => owner.Restore(candidate);

    protected override void ValidateRawPayload(string json) => ValidateJson(json);

    public static void ValidateJson(string json) => ValidateShape(
        JObject.Parse(
            json,
            new JsonLoadSettings
            {
                DuplicatePropertyNameHandling =
                    DuplicatePropertyNameHandling.Error
            }),
        typeof(FacilityShopPurchaseJournalData));

    private static void ValidateShape(JToken token, Type type)
    {
        if (token == null)
        {
            throw new InvalidOperationException(
                "facility-shop-observation-json-field-missing:"
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
        else if (type.IsArray
            || type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray values)
            {
                throw new InvalidOperationException(
                    "facility-shop-observation-json-array-missing:"
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
                    "facility-shop-observation-json-object-missing:"
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
                "facility-shop-observation-json-field-invalid:"
                + type.Name);
        }
    }
}
