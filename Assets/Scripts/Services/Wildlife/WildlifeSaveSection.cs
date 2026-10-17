using System;
using System.Collections.Generic;

public sealed class WildlifeSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonWildlifeSaveData,
        WildlifeRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "wildlife.population";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly IWildlifeRuntime runtime;

    public WildlifeSaveSection(IWildlifeRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonWildlifeSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonWildlifeSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override void ValidateRawPayload(string payloadJson) => ValidateProcessingShape(payloadJson);

    public static void ValidateProcessingShape(string json)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        ValidateGameplayOutcomeRows(root);
        ValidateFoodRaidRows(root);
        if (root["carcassProcessing"] is not Newtonsoft.Json.Linq.JArray rows)
            throw new InvalidOperationException("Wildlife current schema requires carcassProcessing.");
        foreach (var row in rows)
        {
            RequireFields(row, typeof(WildlifeCarcassProcessingSaveData));
            RequireFields(row["fact"], typeof(WildlifeCarcassProcessingData));
        }
        static void RequireFields(Newtonsoft.Json.Linq.JToken token, Type type)
        {
            if (token is not Newtonsoft.Json.Linq.JObject data)
                throw new InvalidOperationException("Carcass processing requires an object.");
            foreach (var field in type.GetFields())
            {
                var expected = field.FieldType == typeof(string) ? Newtonsoft.Json.Linq.JTokenType.String
                    : field.FieldType == typeof(bool) ? Newtonsoft.Json.Linq.JTokenType.Boolean
                    : field.FieldType == typeof(int) ? Newtonsoft.Json.Linq.JTokenType.Integer
                    : Newtonsoft.Json.Linq.JTokenType.Object;
                if (data[field.Name]?.Type != expected)
                    throw new InvalidOperationException("Carcass processing field missing or invalid: " + field.Name);
            }
        }
    }

    private static void ValidateFoodRaidRows(Newtonsoft.Json.Linq.JObject root)
    {
        if (root["foodRaidOrders"] is not Newtonsoft.Json.Linq.JArray rows)
            throw new InvalidOperationException("Wildlife current schema requires foodRaidOrders.");
        foreach (var row in rows)
        {
            Check(row, typeof(WildlifeFoodRaidOrderSaveData));
            Check(row["fact"], typeof(WildlifeFoodRaidFactData));
        }
        static void Check(Newtonsoft.Json.Linq.JToken token, Type type)
        {
            if (token is not Newtonsoft.Json.Linq.JObject data)
                throw new InvalidOperationException("Food raid requires an object.");
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var value = data[field.Name];
                bool valid = field.FieldType == typeof(string) ? value?.Type == Newtonsoft.Json.Linq.JTokenType.String
                    : field.FieldType == typeof(float) ? value?.Type is Newtonsoft.Json.Linq.JTokenType.Float or Newtonsoft.Json.Linq.JTokenType.Integer
                    : field.FieldType == typeof(int) || field.FieldType == typeof(long) || field.FieldType.IsEnum
                        ? value?.Type == Newtonsoft.Json.Linq.JTokenType.Integer
                    : field.FieldType == typeof(List<string>) ? value is Newtonsoft.Json.Linq.JArray
                    : field.FieldType == typeof(WildlifeFoodRaidFactData) && value?.Type == Newtonsoft.Json.Linq.JTokenType.Object;
                if (!valid) throw new InvalidOperationException("Food raid field missing or invalid: " + field.Name);
                if (value is Newtonsoft.Json.Linq.JArray strings)
                    foreach (var item in strings)
                        if (item.Type != Newtonsoft.Json.Linq.JTokenType.String)
                            throw new InvalidOperationException("Food raid stack identity must be a string.");
            }
        }
    }

    private static void ValidateGameplayOutcomeRows(
        Newtonsoft.Json.Linq.JObject root)
    {
        if (root["nextGameplayOutcomeSequence"]?.Type
                != Newtonsoft.Json.Linq.JTokenType.Integer
            || root["gameplayOutcomes"]
                is not Newtonsoft.Json.Linq.JArray rows)
        {
            throw new InvalidOperationException(
                "Wildlife current schema requires gameplay-outcome state.");
        }

        foreach (Newtonsoft.Json.Linq.JToken row in rows)
        {
            if (row is not Newtonsoft.Json.Linq.JObject data)
            {
                throw new InvalidOperationException(
                    "Wildlife gameplay outcome requires an object.");
            }
            foreach (System.Reflection.FieldInfo field in
                     typeof(WildlifeGameplayOutcomeSaveData).GetFields(
                         System.Reflection.BindingFlags.Public
                         | System.Reflection.BindingFlags.Instance))
            {
                Newtonsoft.Json.Linq.JTokenType expected =
                    field.FieldType == typeof(string)
                        ? Newtonsoft.Json.Linq.JTokenType.String
                        : field.FieldType == typeof(bool)
                            ? Newtonsoft.Json.Linq.JTokenType.Boolean
                            : field.FieldType.IsEnum
                                || field.FieldType == typeof(int)
                                || field.FieldType == typeof(long)
                                ? Newtonsoft.Json.Linq.JTokenType.Integer
                                : throw new InvalidOperationException(
                                    "Unsupported wildlife gameplay outcome field: "
                                    + field.Name);
                if (data[field.Name]?.Type != expected)
                {
                    throw new InvalidOperationException(
                        "Wildlife gameplay outcome field missing or invalid: "
                        + field.Name);
                }
            }
        }
    }

    protected override void NormalizeRestorePayload(
        DungeonWildlifeSaveData payload,
        DungeonGameRestoreReport report) =>
        V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        DungeonWildlifeSaveData payload) =>
        runtime.ValidateRestorePayload(payload);

    protected override WildlifeRestoreCandidate BuildRestoreCandidate(
        DungeonWildlifeSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        WildlifeRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
