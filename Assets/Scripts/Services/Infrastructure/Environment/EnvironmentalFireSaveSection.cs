using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class EnvironmentalFireSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "environment.fire";
    private static readonly string[] Dependencies =
    {
        FoundationSessionSaveSection.Id,
        RandomStreamSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };

    private readonly IEnvironmentalFirePersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IEnvironmentalFireDamagePersistence damageOutcomes;
    private readonly EnvironmentalFireResourceTransaction resourceFacts;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;
    private readonly IRestoreWorldCandidateQuery worldCandidates;
    private readonly IFacilityBufferDestinationClaimCommand destinationClaims;
    private readonly EnvironmentalFireRuntimeSettings settings;

    public EnvironmentalFireSaveSection(
        IEnvironmentalFirePersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IEnvironmentalFireDamagePersistence damageOutcomes,
        EnvironmentalFireResourceTransaction resourceFacts,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates,
        IRestoreWorldCandidateQuery worldCandidates,
        IFacilityBufferDestinationClaimCommand destinationClaims,
        EnvironmentalFireRuntimeSettings settings)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.damageOutcomes = damageOutcomes
            ?? throw new ArgumentNullException(nameof(damageOutcomes));
        this.resourceFacts = resourceFacts ?? throw new ArgumentNullException(nameof(resourceFacts));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(
                nameof(lifecycleRestoreCandidates));
        this.worldCandidates = worldCandidates ?? throw new ArgumentNullException(nameof(worldCandidates));
        this.destinationClaims = destinationClaims ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonEnvironmentalFireSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture()
    {
        DungeonEnvironmentalFireSaveData payload = persistence.Capture();
        payload.damageOperations = damageOutcomes.Capture()
            .Select(value => value.Clone())
            .ToList();
        payload.resourceOperations = resourceFacts.Capture().ToList();
        return JsonUtility.ToJson(payload);
    }

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireHeader(payloadJson, sectionVersion);
        EnvironmentalFireResourceTransaction.Validate(Parse(payloadJson).resourceOperations);
        persistence.PrepareRestore(Parse(payloadJson));
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireHeader(payloadJson, sectionVersion);
        DungeonEnvironmentalFireSaveData payload = Parse(payloadJson);
        EnvironmentalFireResourceTransaction.Validate(payload.resourceOperations);
        EnvironmentalFireRestoreCandidate candidate =
            persistence.PrepareRestore(payload);
        DungeonEnvironmentalFireSaveData canonical = candidate.Capture();
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        FacilityBufferDestinationClaim[] waterClaims = BuildWaterClaims(canonical);
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            restoreReport =>
            {
                // Claims are derived from the incoming owners, not copied from
                // the old world or rebuilt one frame after haul rebind. The
                // existing claim participant publishes/rolls them back atomically.
                if (!destinationClaims.TryReplaceOwnedClaims(
                        EnvironmentalFireWaterDestinationIdentity.OwnerDomain,
                        waterClaims, out _, out string claimFailure))
                    throw new InvalidOperationException("Fire water restore claims rejected: " + claimFailure);
                lifecycleRestoreCandidates.SetFire(canonical);
                persistence.Restore(candidate);
                damageOutcomes.Restore(canonical.damageOperations);
                resourceFacts.Restore(canonical.resourceOperations);
            });
    }

    private FacilityBufferDestinationClaim[] BuildWaterClaims(DungeonEnvironmentalFireSaveData payload)
    {
        if (!worldCandidates.TryGetBuildings(out IReadOnlyList<BuildableObject> buildings))
            throw new InvalidOperationException("Fire restore requires the detached facility-world candidate.");
        var claims = new List<FacilityBufferDestinationClaim>();
        foreach (EnvironmentalFireSaveRecord fire in payload.activeFires)
        {
            if (fire.targetKind != (int)EnvironmentalFireTargetKind.Building
                || fire.intensity <= settings.MaximumInitialAttackIntensity + 0.0001f)
                continue;
            BuildableObject building = buildings.SingleOrDefault(value => value != null
                && !value.isDestroy && value.BuildingData != null
                && value.PersistentInstanceId.Value == fire.targetId);
            // A vanished target has no destination authority. Any carried
            // delivery to it will be rejected by the exact-claim rebind check.
            if (building == null) continue;
            claims.Add(new FacilityBufferDestinationClaim(
                EnvironmentalFireWaterDestinationIdentity.Create(fire.fireId),
                building.centerPos, EnvironmentalFireWaterDestinationIdentity.OwnerDomain,
                fire.fireId, fire.targetId, FacilityBufferDestinationAnchorKind.LiveBuilding));
        }
        return claims.ToArray();
    }

    public static void ValidatePhysicalRestoreCandidate(
        DungeonEnvironmentalFireSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.suppressionOperations == null
            || payload.activeFires == null
            || payload.history == null
            || query == null
            || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Environmental-fire restore requires the incoming physical candidate.");
        }

        Dictionary<string, EnvironmentalFireSuppressionSaveRecord> owners =
            payload.suppressionOperations
                .Where(value => value != null
                    && value.mode == (int)EnvironmentalFireSuppressionMode.Water
                    && value.waterCommittedQuantity > 0
                    && !value.waterAcknowledged)
                .ToDictionary(value => value.operationId, StringComparer.Ordinal);
        Dictionary<string, EnvironmentalFireSaveRecord> fuelOwners =
            EnumerateFires(payload)
                .Where(value => value.fuelLossQuantity > 0
                    && !value.fuelLossAcknowledged)
                .ToDictionary(
                    value => value.fuelLossOperationId,
                    StringComparer.Ordinal);

        foreach (KeyValuePair<string, EnvironmentalFireSuppressionSaveRecord> pair
                 in owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Environmental-fire water owner '{pair.Key}' has no exact incoming physical Sink receipt.");
            }
        }

        foreach (KeyValuePair<string, EnvironmentalFireSaveRecord> pair
                 in fuelOwners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !MatchesFuelLoss(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Environmental-fire fuel owner '{pair.Key}' has no exact incoming physical Sink receipt.");
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt == null
                || (!string.Equals(
                        receipt.ReasonCode,
                        EnvironmentalFireWorldAdapter.WaterSinkReasonCode,
                        StringComparison.Ordinal)
                    && !string.Equals(
                        receipt.ReasonCode,
                        EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            bool isWater = string.Equals(
                receipt.ReasonCode,
                EnvironmentalFireWorldAdapter.WaterSinkReasonCode,
                StringComparison.Ordinal);
            bool matched;
            if (isWater)
            {
                matched = owners.TryGetValue(
                        receipt.OperationId,
                        out EnvironmentalFireSuppressionSaveRecord owner)
                    && Matches(owner, receipt);
            }
            else
            {
                matched = fuelOwners.TryGetValue(
                        receipt.OperationId,
                        out EnvironmentalFireSaveRecord fuelOwner)
                    && MatchesFuelLoss(fuelOwner, receipt);
            }
            if (!matched)
            {
                throw new InvalidOperationException(
                    $"Incoming environmental-fire Sink '{receipt.OperationId}' has no exact unacknowledged fire owner.");
            }
        }
    }

    private static IEnumerable<EnvironmentalFireSaveRecord> EnumerateFires(
        DungeonEnvironmentalFireSaveData payload)
    {
        foreach (EnvironmentalFireSaveRecord fire in payload.activeFires)
            yield return fire;
        foreach (EnvironmentalFireHistorySaveRecord entry in payload.history)
        {
            if (entry?.fire != null)
                yield return entry.fire;
        }
    }

    private static bool Matches(
        EnvironmentalFireSuppressionSaveRecord owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            owner.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            EnvironmentalFireWorldAdapter.WaterSinkReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.waterCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.waterCommittedQuantity;

    private static bool MatchesFuelLoss(
        EnvironmentalFireSaveRecord owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            owner.fuelLossOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.fuelLossCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.fuelLossQuantity
        && receipt.InputMassGrams == owner.fuelLossMassGrams
        && receipt.SourceStackIds != null
        && receipt.SourceStackIds.Count == 1
        && string.Equals(
            receipt.SourceStackIds[0],
            owner.fuelLossTargetId,
            StringComparison.Ordinal);

    private static DungeonEnvironmentalFireSaveData Parse(string payloadJson)
    {
        try
        {
            return JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(
                payloadJson)
                ?? throw new InvalidOperationException(
                    "Environmental-fire payload deserialized to null.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Environmental-fire payload JSON is invalid: "
                + exception.Message,
                exception);
        }
    }

    private void RequireHeader(string payloadJson, int sectionVersion)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new InvalidOperationException(
                "Environmental-fire payload is empty.");
        if (sectionVersion != SectionVersion)
            throw new InvalidOperationException(
                $"Environmental-fire section version {sectionVersion} is unsupported; expected {SectionVersion}.");
        string[] requiredArrays = new[]
            {
                "activeFires",
                "history",
                "processedCauses",
                "suppressionOperations",
                "damageOperations",
                "resourceOperations"
            };
        DungeonStrictJsonShape.RequireTopLevelArrays(
            SectionId,
            payloadJson,
            requiredArrays);
        JObject root = JObject.Parse(payloadJson, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        ValidateDamagePublicationJson(root);
        ValidateActionPublicationJson(root);
        foreach (var token in (JArray)root["resourceOperations"])
            if (token is not JObject row || row["publicationKey"]?.Type != JTokenType.String
                || row["physical"] is not JObject || row["use"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("Fire resource publication members are missing.");
        foreach (JToken token in (JArray)root["suppressionOperations"])
        {
            if (token is not JObject operation
                || operation["targetId"]?.Type != JTokenType.String
                || operation["commandOrigin"]?.Type != JTokenType.Integer
                || !Enum.IsDefined(typeof(CharacterCommandOrigin), operation.Value<int>("commandOrigin"))
                || operation["completionIdentityAcknowledged"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Environmental-fire suppression is missing typed completion members.");
        }
    }

    internal static void ValidateActionPublicationJson(JObject root)
    {
        if (root["version"]?.Type != JTokenType.Integer
            || root["version"].Value<int>() != DungeonEnvironmentalFireSaveData.CurrentVersion
            || root["processedCauses"] is not JArray causes || root["suppressionOperations"] is not JArray suppressions)
            throw new InvalidOperationException("Fire action publication schema is missing or unsupported.");
        foreach (var token in causes)
        {
            if (token is not JObject row) throw new InvalidOperationException("Fire ignition owner is missing.");
            Require(row, JTokenType.String, "producerId", "evidenceId", "targetId", "fuelTargetId", "publicationKey", "fingerprint", "targetDisplayName", "reason");
            Require(row, JTokenType.Integer, "ignitionKind", "targetKind", "fuelTargetKind", "fuelQuantity", "positionX", "positionY", "absoluteDay", "outcomeRevision", "disposition");
            Require(row, JTokenType.Boolean, "hasLocation", "outcomePending");
            RequireNumber(row, "requestedIntensity", "appliedIntensity");
        }
        foreach (var token in suppressions)
        {
            if (token is not JObject row) throw new InvalidOperationException("Fire suppression owner is missing.");
            Require(row, JTokenType.String, "publicationKey", "completionPublicationKey", "targetId", "workerDisplayName", "fingerprint");
            Require(row, JTokenType.Integer, "positionX", "positionY", "absoluteDay", "outcomeRevision", "phase", "commandOrigin");
            Require(row, JTokenType.Boolean, "outcomePending", "completionIdentityAcknowledged");
            RequireNumber(row, "intensityBefore", "intensityAfter", "approvedWork");
        }
        static void Require(JObject row, JTokenType type, params string[] names)
        {
            foreach (string name in names)
                if (row[name]?.Type != type) throw new InvalidOperationException("Fire action field missing or mistyped: " + name);
        }
        static void RequireNumber(JObject row, params string[] names)
        {
            foreach (string name in names)
                if (row[name]?.Type != JTokenType.Float && row[name]?.Type != JTokenType.Integer)
                    throw new InvalidOperationException("Fire action number missing: " + name);
        }
    }

    internal static void ValidateDamagePublicationJson(JObject root)
    {
        if (root["version"]?.Type != JTokenType.Integer
            || root["version"].Value<int>() != DungeonEnvironmentalFireSaveData.CurrentVersion
            || root["damageOperations"] is not JArray operations)
            throw new InvalidOperationException("Fire damage publication schema is missing or unsupported.");
        foreach (var token in operations)
        {
            if (token is not JObject row) throw new InvalidOperationException("Fire damage owner is missing.");
            foreach (string field in new[] { "publicationKey", "publicationId", "publicationFingerprint" })
                if (row[field]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Fire damage publication field missing: " + field);
        }
    }
}
