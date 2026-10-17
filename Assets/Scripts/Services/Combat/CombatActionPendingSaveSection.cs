using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class CombatActionPendingSaveSection :
    DungeonStrictJsonSaveSection<CombatActionPendingSaveData, CombatActionPendingState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "combat.action-pending";
    private readonly CombatActionPublicationRuntime owner;

    public CombatActionPendingSaveSection(CombatActionPublicationRuntime owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public override string SectionId => Id;
    public override int SectionVersion => CombatActionPendingSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Array.Empty<string>();

    protected override CombatActionPendingSaveData CapturePayload() =>
        owner.Capture();

    protected override CombatActionPendingState BuildRestoreCandidate(
        CombatActionPendingSaveData payload) => owner.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        CombatActionPendingState candidate) => owner.PublishRestore(candidate);

    protected override void ValidateRawPayload(string json) =>
        ValidatePendingJson(json);

    internal static void ValidatePendingJson(string json)
    {
        var root = JObject.Parse(json);
        RequireType(root, JTokenType.Integer, "version", "root");
        if (root["version"].Value<int>()
            != CombatActionPendingSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                "Combat action pending version is unsupported.");
        }
        JArray pending = RequireArray(root, "pending", "root");
        for (int index = 0; index < pending.Count; index++)
        {
            if (pending[index] is not JObject entry)
            {
                throw new InvalidOperationException(
                    "Combat action pending entry is missing or null.");
            }
            ValidatePending(entry);
        }
    }

    private static void ValidatePending(JObject pending)
    {
        RequireType(pending, JTokenType.String, "publicationKey", "pending");
        ValidateFact(RequireObject(pending, "fact", "pending"));
    }

    private static void ValidateFact(JObject fact)
    {
        RequireTypes(
            fact,
            JTokenType.String,
            "fact",
            "operationId",
            "damageSource");
        RequireTypes(
            fact,
            JTokenType.Integer,
            "fact",
            "attackRevision",
            "absoluteDay",
            "targetX",
            "targetY",
            "origin");
        RequireTypes(
            fact,
            JTokenType.Boolean,
            "fact",
            "hasConflict",
            "downedBefore",
            "downedAfter");
        RequireNumbers(
            fact,
            "fact",
            "healthBefore",
            "maxHealthBefore",
            "healthAfter",
            "maxHealthAfter");
        ValidateParticipant(RequireObject(fact, "attacker", "fact"));
        ValidateParticipant(RequireObject(fact, "target", "fact"));
        ValidateEvidence(RequireObject(fact, "evidence", "fact"));

        if (!fact.TryGetValue("conflict", out JToken conflict))
            throw Missing("fact", "conflict");
        if (conflict is not JObject conflictObject)
            throw Missing("fact", "conflict");
        ValidateConflict(conflictObject);
    }

    private static void ValidateParticipant(JObject participant)
    {
        RequireType(participant, JTokenType.Integer, "kind", "participant");
        RequireTypes(
            participant,
            JTokenType.String,
            "participant",
            "id",
            "name");
    }

    private static void ValidateEvidence(JObject evidence)
    {
        RequireTypes(
            evidence,
            JTokenType.String,
            "evidence",
            "weaponDefinitionId",
            "weaponInstanceId",
            "ammunitionItemId",
            "coverSourceId");
        RequireTypes(
            evidence,
            JTokenType.Integer,
            "evidence",
            "damageType",
            "fireMode",
            "ammunitionBefore",
            "ammunitionConsumed",
            "ammunitionAfter",
            "specialEffects",
            "statusTurns",
            "bodyPart");
        RequireTypes(
            evidence,
            JTokenType.Boolean,
            "evidence",
            "requestKnown",
            "executed",
            "hit",
            "coverBlocked",
            "shieldBlocked",
            "evaded",
            "nonlethal");
        RequireNumbers(
            evidence,
            "evidence",
            "statusPotency",
            "coverDamage",
            "appliedDamage",
            "suppression");

        JArray hits = RequireArray(evidence, "armorHits", "evidence");
        for (int index = 0; index < hits.Count; index++)
        {
            if (hits[index] is not JObject hit)
            {
                throw new InvalidOperationException(
                    "Combat action armor hit is missing or null.");
            }
            RequireType(hit, JTokenType.String, "instanceId", "armor-hit");
            RequireNumber(hit, "damage", "armor-hit");
        }
    }

    private static void ValidateConflict(JObject conflict)
    {
        RequireTypes(
            conflict,
            JTokenType.String,
            "conflict",
            "operationId",
            "conflictId");
        RequireTypes(
            conflict,
            JTokenType.Integer,
            "conflict",
            "origin",
            "absoluteDay");
        RequireType(
            conflict,
            JTokenType.Boolean,
            "alreadyPublished",
            "conflict");
        RequireNumbers(
            conflict,
            "conflict",
            "severity",
            "committedMoodDelta");
        ValidateParticipant(RequireObject(conflict, "instigator", "conflict"));
        ValidateParticipant(RequireObject(conflict, "target", "conflict"));
    }

    private static JObject RequireObject(
        JObject owner,
        string field,
        string scope)
    {
        if (owner[field] is not JObject value)
            throw Missing(scope, field);
        return value;
    }

    private static JArray RequireArray(
        JObject owner,
        string field,
        string scope)
    {
        if (owner[field] is not JArray value)
            throw Missing(scope, field);
        return value;
    }

    private static void RequireTypes(
        JObject owner,
        JTokenType type,
        string scope,
        params string[] fields)
    {
        for (int index = 0; index < fields.Length; index++)
            RequireType(owner, type, fields[index], scope);
    }

    private static void RequireType(
        JObject owner,
        JTokenType type,
        string field,
        string scope)
    {
        if (owner[field]?.Type != type)
            throw Missing(scope, field);
    }

    private static void RequireNumbers(
        JObject owner,
        string scope,
        params string[] fields)
    {
        for (int index = 0; index < fields.Length; index++)
            RequireNumber(owner, fields[index], scope);
    }

    private static void RequireNumber(
        JObject owner,
        string field,
        string scope)
    {
        if (owner[field]?.Type is not (JTokenType.Integer or JTokenType.Float))
            throw Missing(scope, field);
    }

    private static InvalidOperationException Missing(
        string scope,
        string field) => new(
        "Combat action pending " + scope + " field is missing or invalid: " + field);
}
