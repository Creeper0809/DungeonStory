using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Canonical Resources payload for the frozen V27 output-clearance profiles.
/// The runtime loader accepts only the exact compact JsonUtility projection:
/// unknown fields, field reordering, whitespace drift, missing rows, duplicate
/// rows and digest drift therefore fail loudly before gameplay can publish a
/// capacity authority.
/// </summary>
[Serializable]
public sealed class ProductionOutputClearanceProfileResourceDocument
{
    public const string CurrentSchema =
        "production-output-clearance-profile-resource@1";

    public string schema = CurrentSchema;
    public int profileCount;
    public string catalogSourceDigest = string.Empty;
    public ProductionOutputClearanceProfileResourceRow[] profiles =
        Array.Empty<ProductionOutputClearanceProfileResourceRow>();
}

[Serializable]
public sealed class ProductionOutputClearanceProfileResourceRow
{
    public string definitionId = string.Empty;
    public string workstationTag = string.Empty;
    public long p95HaulClearanceMilliHours;
    public long peakOutputMassGramsPerHour;
    public int sampleCount;
    public int distinctSeedCount;
    public string measurementSourceDigest = string.Empty;
    public string throughputSourceDigest = string.Empty;
    public string rowSourceDigest = string.Empty;
}

public static class ProductionOutputClearanceProfileResourceCodec
{
    public const string NonCanonicalFailureToken =
        "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_RESOURCE_NON_CANONICAL";

    public static ProductionOutputClearanceProfileCatalog ParseRequired(
        string canonicalJson,
        int expectedProfileCount)
    {
        if (expectedProfileCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedProfileCount));
        if (string.IsNullOrEmpty(canonicalJson))
            throw new InvalidOperationException(NonCanonicalFailureToken);

        ProductionOutputClearanceProfileResourceDocument document;
        try
        {
            document = JsonUtility.FromJson<
                ProductionOutputClearanceProfileResourceDocument>(canonicalJson);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                NonCanonicalFailureToken,
                exception);
        }

        if (document == null
            || !string.Equals(
                document.schema,
                ProductionOutputClearanceProfileResourceDocument.CurrentSchema,
                StringComparison.Ordinal)
            || document.profileCount != expectedProfileCount
            || document.profiles == null
            || document.profiles.Length != expectedProfileCount
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                document.catalogSourceDigest)
            || !string.Equals(
                canonicalJson,
                JsonUtility.ToJson(document, prettyPrint: false),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(NonCanonicalFailureToken);
        }

        ProductionOutputClearanceProfileRecord[] records = document.profiles
            .Select(row => MaterializeRequired(row))
            .OrderBy(row => row.DefinitionId, StringComparer.Ordinal)
            .ThenBy(row => row.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceProfileCatalog catalog = new(records);
        if (!string.Equals(
                document.catalogSourceDigest,
                catalog.SourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_RESOURCE_DIGEST_DRIFT");
        }
        return catalog;
    }

    public static string SerializeCanonical(
        IReadOnlyList<ProductionOutputClearanceProfileRecord> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        ProductionOutputClearanceProfileRecord[] ordered = source
            .OrderBy(row => row?.DefinitionId ?? string.Empty,
                StringComparer.Ordinal)
            .ThenBy(row => row?.WorkstationTag ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceProfileCatalog catalog = new(ordered);
        ProductionOutputClearanceProfileResourceDocument document = new()
        {
            schema = ProductionOutputClearanceProfileResourceDocument.CurrentSchema,
            profileCount = ordered.Length,
            catalogSourceDigest = catalog.SourceDigest,
            profiles = ordered.Select(row => new
                ProductionOutputClearanceProfileResourceRow
                {
                    definitionId = row.DefinitionId,
                    workstationTag = row.WorkstationTag,
                    p95HaulClearanceMilliHours =
                        row.P95HaulClearanceMilliHours,
                    peakOutputMassGramsPerHour =
                        row.PeakOutputMassGramsPerHour,
                    sampleCount = row.SampleCount,
                    distinctSeedCount = row.DistinctSeedCount,
                    measurementSourceDigest = row.MeasurementSourceDigest,
                    throughputSourceDigest = row.ThroughputSourceDigest,
                    rowSourceDigest = row.SourceDigest
                })
                .ToArray()
        };
        return JsonUtility.ToJson(document, prettyPrint: false);
    }

    private static ProductionOutputClearanceProfileRecord MaterializeRequired(
        ProductionOutputClearanceProfileResourceRow row)
    {
        if (row == null)
            throw new InvalidOperationException(NonCanonicalFailureToken);
        ProductionOutputClearanceProfileRecord record = new(
            row.definitionId,
            row.workstationTag,
            row.p95HaulClearanceMilliHours,
            row.peakOutputMassGramsPerHour,
            row.sampleCount,
            row.distinctSeedCount,
            row.measurementSourceDigest,
            row.throughputSourceDigest);
        if (!string.Equals(
                row.rowSourceDigest,
                record.SourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_ROW_DIGEST_DRIFT");
        }
        return record;
    }
}

/// <summary>
/// Runtime-only strict source. Missing data is a typed configuration failure;
/// no empty catalog, guessed timing or default row is ever constructed.
/// </summary>
public sealed class ProductionOutputClearanceProfileResourceSource :
    IProductionOutputClearanceProfileSource
{
    public const string ResourcePath =
        "V27/production-output-clearance-profiles";
    public const int ExpectedProfileCount = 92;
    public const string MissingFailureToken =
        "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_RESOURCE_MISSING";

    private readonly ProductionOutputClearanceProfileCatalog catalog;

    public ProductionOutputClearanceProfileResourceSource()
    {
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
            throw new InvalidOperationException(MissingFailureToken);
        catalog = ProductionOutputClearanceProfileResourceCodec.ParseRequired(
            asset.text,
            ExpectedProfileCount);
    }

    public string AuthorityDigest => catalog.AuthorityDigest;
    public IReadOnlyList<ProductionOutputClearanceProfileRecord> Records =>
        catalog.Records;

    public ProductionOutputClearanceProfileSnapshot Capture(
        ProductionFacilityCapacitySubject facility) => catalog.Capture(facility);
}

/// <summary>
/// Editor-only bootstrap authority used exclusively while the 92 x 32 natural
/// cohort that creates the first frozen resource is being measured. Its one
/// milli-hour / one gram-per-hour demand can never raise the authored 2-4 cycle
/// capacity, so the bootstrap observes the pre-profile gameplay authority
/// instead of guessing the profile it is trying to measure. Production and
/// ordinary editor runs still fail loudly when the frozen resource is absent.
/// </summary>
public sealed class ProductionOutputClearanceNaturalBootstrapProfileSource :
    IProductionOutputClearanceProfileSource
{
    public const string EnvironmentVariable =
        "V27_OUTPUT_CLEARANCE_PROFILE_BOOTSTRAP";
    public const string EnvironmentContract =
        "natural-92x32-authored-cycle-baseline@1";
    public const string Schema =
        "production-output-clearance-natural-bootstrap-profile@1";

    private static readonly string CatalogDigest = BuildCatalogDigest();

    public static bool IsRequested
    {
        get
        {
#if UNITY_EDITOR
            return string.Equals(
                Environment.GetEnvironmentVariable(EnvironmentVariable),
                EnvironmentContract,
                StringComparison.Ordinal);
#else
            return false;
#endif
        }
    }

    public string AuthorityDigest => CatalogDigest;

    public ProductionOutputClearanceProfileSnapshot Capture(
        ProductionFacilityCapacitySubject facility)
    {
        if (!facility.FacilityId.IsValid
            || string.IsNullOrEmpty(facility.DefinitionId)
            || string.IsNullOrEmpty(facility.WorkstationTag)
            || facility.OutputBufferCycleCapacity is < 2 or > 4)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_BOOTSTRAP_SUBJECT_INVALID");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(facility.DefinitionId);
        digest.Append(facility.WorkstationTag);
        digest.Append(facility.OutputBufferCycleCapacity);
        digest.Append(1L);
        digest.Append(1L);
        return new ProductionOutputClearanceProfileSnapshot(
            1L,
            1L,
            digest.ComputeSha256());
    }

    private static string BuildCatalogDigest()
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(EnvironmentContract);
        digest.Append(1L);
        digest.Append(1L);
        return digest.ComputeSha256();
    }
}
