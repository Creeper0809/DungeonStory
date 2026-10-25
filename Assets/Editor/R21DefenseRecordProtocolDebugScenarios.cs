#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R21DefenseRecordProtocolDebugScenarios
{
    private const string MissingDefenseFamily = "defense:contorl";
    private const string MissingRecordMetric = "VisitCounnt";

    [MenuItem("DungeonStory/R21/Run Defense And Record Protocol Scenarios")]
    public static void RunAll()
    {
        Require(!Application.isPlaying,
            "R21 protocol scenarios must run before Play mode.");

        VerifyDefenseFamilyProtocol();
        VerifyRecordMetricProtocol();

        Debug.Log("R21 defense-family and record-metric protocol scenarios passed.");
    }

    private static void VerifyDefenseFamilyProtocol()
    {
        RequireReference(
            typeof(DefenseFacilityData),
            nameof(DefenseFacilityData.facilityFamilyId),
            ContentReferenceKind.DefenseFamily);
        Require(DefenseFacilityFamilyIds.Values.Count == 13,
            "Defense family vocabulary must contain the 13 live families.");
        Require(DefenseFacilityFamilyIds.Values.Distinct(StringComparer.Ordinal).Count()
                == DefenseFacilityFamilyIds.Values.Count,
            "Defense family vocabulary contains duplicates.");

        DefenseFacilityData definition = new DefenseFacilityData
        {
            enabled = true,
            concept = DefenseAttackConcept.Physical
        };
        foreach (string family in DefenseFacilityFamilyIds.Values)
        {
            definition.facilityFamilyId = family;
            RequireClean(definition.ValidateDefinition(),
                "Known defense family " + family);
        }

        definition.facilityFamilyId = MissingDefenseFamily;
        RequireRejected(
            definition.ValidateDefinition(),
            MissingDefenseFamily,
            "Enabled defense data accepted an unknown family.");

        definition.facilityFamilyId = DefenseFacilityFamilyIds.Control;
        RequireClean(definition.ValidateDefinition(), "Defense family repair");
        RequireClean(definition.ValidateDefinition(), "Defense family repair retry");

        definition.enabled = false;
        definition.facilityFamilyId = MissingDefenseFamily;
        RequireClean(definition.ValidateDefinition(),
            "Disabled defense metadata compatibility");

        Require(InvasionIntruderDefenseObservation.IsObservable(
                new InvasionDefenseObservationSnapshot(
                    default,
                    DefenseFacilityFamilyIds.Launcher,
                    0)),
            "The invasion observer did not expose the shared launcher family.");
        Require(!InvasionIntruderDefenseObservation.IsObservable(
                new InvasionDefenseObservationSnapshot(
                    default,
                    DefenseFacilityFamilyIds.Barrier,
                    0)),
            "The invasion observer exposed a zero-range barrier family.");

        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            building.Defense = new DefenseFacilityData
            {
                enabled = true,
                concept = DefenseAttackConcept.Physical,
                facilityFamilyId = DefenseFacilityFamilyIds.Barrier
            };
            Require(BuildingStructuralIntegrityDefaults.TryCreate(
                    building,
                    out BuildingStructuralIntegrityAbility barrier)
                && barrier != null
                && Mathf.Approximately(
                    barrier.maxHitPoints,
                    BuildingStructuralIntegrityDefaults.ReinforcedDropGateHitPoints)
                && Mathf.Approximately(
                    barrier.toughness,
                    BuildingStructuralIntegrityDefaults.ReinforcedDropGateToughness),
                "Structural integrity did not consume the shared barrier family.");

            building.Defense.facilityFamilyId =
                DefenseFacilityFamilyIds.MechanicalTrap;
            Require(!BuildingStructuralIntegrityDefaults.TryCreate(building, out _),
                "A non-barrier family received the reinforced barrier profile.");
        }
        finally
        {
            Object.DestroyImmediate(building);
        }
    }

    private static void VerifyRecordMetricProtocol()
    {
        RequireReference(
            typeof(FacilityEvolutionRecordTokenDefinitionSO),
            nameof(FacilityEvolutionRecordTokenDefinitionSO.sourceMetric),
            ContentReferenceKind.FacilityRecordMetric);
        IReadOnlyList<string> produced =
            FacilityEvolutionRecordEventRecorder.ProducedMetricKeys;
        Require(produced.Count == 20,
            "Record metric vocabulary must contain the 20 live producer keys.");
        Require(produced.Distinct(StringComparer.Ordinal).Count() == produced.Count,
            "Record metric vocabulary contains duplicates.");
        Require(produced.SequenceEqual(
                FacilityEvolutionTerms.ProducedRecordMetricKeys,
                StringComparer.Ordinal),
            "Recorder metric choices diverged from FacilityEvolutionTerms.");

        FacilityEvolutionRecordTokenDefinitionSO definition =
            ScriptableObject.CreateInstance<FacilityEvolutionRecordTokenDefinitionSO>();
        try
        {
            definition.tokenId = "fixture:r21:metric-threshold";
            definition.generationTrigger =
                FacilityEvolutionRecordTokenGenerationTrigger.MetricThresholdCrossing;
            definition.sourceMetric = FacilityEvolutionTerms.VisitCount;
            definition.threshold = 2f;
            definition.decayPolicy = "long-term";
            RequireClean(definition.ValidateDefinition(),
                "Valid metric-threshold token");
            CreateRecorder(definition);

            definition.sourceMetric = MissingRecordMetric;
            RequireRejected(
                definition.ValidateDefinition(),
                MissingRecordMetric,
                "Metric-threshold token accepted an unknown metric.");
            RequireConstructorRejected(definition,
                "Recorder accepted an unknown threshold metric.");

            definition.sourceMetric = FacilityEvolutionTerms.VisitCount;
            definition.threshold = 0f;
            RequireRejected(
                definition.ValidateDefinition(),
                "positive finite threshold",
                "Metric-threshold token accepted a zero threshold.");

            definition.threshold = float.PositiveInfinity;
            RequireRejected(
                definition.ValidateDefinition(),
                "positive finite threshold",
                "Metric-threshold token accepted an infinite threshold.");

            definition.threshold = 2f;
            definition.decayPolicy = "short-term";
            RequireRejected(
                definition.ValidateDefinition(),
                "long-term retention",
                "Metric-threshold token accepted unsupported retention.");

            definition.decayPolicy = "long-term";
            RequireClean(definition.ValidateDefinition(),
                "Metric-threshold token repair");
            CreateRecorder(definition);
            RequireClean(definition.ValidateDefinition(),
                "Metric-threshold token repair retry");
            CreateRecorder(definition);

            definition.generationTrigger =
                FacilityEvolutionRecordTokenGenerationTrigger.CrimeReported;
            definition.sourceMetric = MissingRecordMetric;
            definition.threshold = float.NaN;
            definition.decayPolicy = "historical-description";
            RequireClean(definition.ValidateDefinition(),
                "Event-trigger descriptive provenance compatibility");
            CreateRecorder(definition);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    private static FacilityEvolutionRecordEventRecorder CreateRecorder(
        FacilityEvolutionRecordTokenDefinitionSO definition)
    {
        return new FacilityEvolutionRecordEventRecorder(
            new FacilityCandidateCacheStore(
                CharacterAiEditorTestDependencies.WorldRegistry,
                frameWorkBudget: null),
            new FacilityEvolutionRecordComponentService(
                new FacilityEvolutionRecordComponentFactory()),
            instanceEvolutionRuntime: null,
            tokenDefinitionProvider: new FixtureTokenProvider(definition));
    }

    private static void RequireConstructorRejected(
        FacilityEvolutionRecordTokenDefinitionSO definition,
        string message)
    {
        try
        {
            CreateRecorder(definition);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void RequireReference(
        Type ownerType,
        string fieldName,
        ContentReferenceKind expectedKind)
    {
        FieldInfo field = ownerType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing field '{ownerType.Name}.{fieldName}'.");
        ContentReferenceAttribute reference =
            field.GetCustomAttribute<ContentReferenceAttribute>();
        Require(reference != null && reference.Kind == expectedKind,
            $"'{ownerType.Name}.{fieldName}' has the wrong reference kind.");
    }

    private static void RequireClean(
        IReadOnlyList<string> errors,
        string context)
    {
        Require(errors != null && errors.Count == 0,
            context + " failed: "
            + string.Join(" | ", errors ?? Array.Empty<string>()));
    }

    private static void RequireRejected(
        IReadOnlyList<string> errors,
        string expected,
        string message)
    {
        Require(errors != null && errors.Any(error =>
                error != null
                && error.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0),
            message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixtureTokenProvider :
        IFacilityEvolutionRecordTokenDefinitionProvider
    {
        private readonly FacilityEvolutionRecordTokenDefinitionSO definition;

        public FixtureTokenProvider(
            FacilityEvolutionRecordTokenDefinitionSO definition)
        {
            this.definition = definition;
        }

        public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO>
            GetDefinitions() => new[] { definition };

        public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(
            string tokenId)
        {
            return definition != null
                && string.Equals(
                    definition.EffectiveId,
                    tokenId,
                    StringComparison.Ordinal)
                ? definition
                : null;
        }
    }
}
#endif
