#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Root-owned cross-authority tests. Synthetic incoming receipts exercise the
// production restore join, not actual watering, physical pickup or world restore.
public static class Wim016WaterRefillRestoreDebugScenarios
{
    private const string Plot = "building:wim016-water-guard";
    private const string Operation = "crop-water-refill:building:wim016-water-guard:000003";
    private const string Commit = "physical:wim016:water:commit";
    private const string Fingerprint = "physical:wim016:water:request";
    private const string Source = "stack:wim016:water";

    public static bool Run(out string report)
    {
        var lines = new List<string> { "WIM016 water refill incoming-owner/receipt guard",
            "scope=synthetic candidate join and current refill DTO round-trip; no runtime watering/AI claim" };
        int passed = 0;
        string current = "not-started";
        try
        {
            foreach (var phase in new[] { CropWaterRefillPhase.InputCommitted, CropWaterRefillPhase.OutcomePublished })
            {
                current = "valid-" + phase;
                var owner = Owner(phase);
                string before = JsonUtility.ToJson(owner.Owner);
                Validate(new[] { owner }, Receipt());
                Require(JsonUtility.ToJson(owner.Owner) == before, "guard mutated incoming owner");
                var restored = JsonUtility.FromJson<CropWaterRefillSaveData>(before);
                Require(restored.physicalRequestFingerprint == Fingerprint
                    && restored.sourceStackIds.SequenceEqual(new[] { Source })
                    && restored.phase == phase && restored.inputMassGrams == 1000,
                    "current DTO lost committed provenance");
                owner.Owner = restored;
                Validate(new[] { owner }, Receipt());
                passed++;
            }

            Reject("missing-receipt", new[] { Owner() });
            Reject("wrong-kind", new[] { Owner() }, Receipt(kind: PhysicalItemDispositionKind.Sink));
            Reject("wrong-reason", new[] { Owner() }, Receipt(reason: "other-domain-input"));
            Reject("wrong-commit", new[] { Owner() }, Receipt(commit: "physical:foreign:commit"));
            Reject("wrong-fingerprint", new[] { Owner() }, Receipt(fingerprint: "physical:foreign:request"));
            Reject("wrong-source", new[] { Owner() }, Receipt(source: "stack:foreign:water"));
            Reject("wrong-quantity", new[] { Owner() }, Receipt(quantity: 2));
            Reject("wrong-mass", new[] { Owner() }, Receipt(mass: 999));
            Reject("wrong-operation", new[] { Owner() }, Receipt(operation: Operation + "-foreign"));
            Reject("duplicate-owner", new[] { Owner(), Owner() }, Receipt());
            Reject("orphan-receipt", Array.Empty<CropWaterRefillOwnerValidationSnapshot>(), Receipt());
            Reject("precommit-must-not-own-receipt", new[] { Owner(CropWaterRefillPhase.Working) }, Receipt());

            current = "precommit-no-receipt";
            Validate(new[] { Owner(CropWaterRefillPhase.Working) });
            passed++;
            current = "unrelated-domain-preserved";
            Validate(Array.Empty<CropWaterRefillOwnerValidationSnapshot>(),
                Receipt(operation: "production:wim016:unrelated"));
            passed++;
            current = "missing-candidate-with-committed-owner";
            bool rejected = false;
            try { CropPhysicalRestoreGuard.ValidateWaterRefillOwnerSnapshots(new[] { Owner() }, null); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "committed owner accepted without incoming item candidate");
            passed++;
            lines.Add("PASS cases=" + passed + "; no mutation; exact bidirectional join");
            report = string.Join("\n", lines);
            return true;
        }
        catch (Exception error)
        {
            lines.Add("FAIL case=" + current + "; passed=" + passed + "; " + error);
            report = string.Join("\n", lines);
            return false;
        }

        void Reject(string name, CropWaterRefillOwnerValidationSnapshot[] owners,
            params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
        {
            current = name;
            string[] before = owners.Select(o => JsonUtility.ToJson(o.Owner)).ToArray();
            // Orphan has no owner. Do not require a fictional domain record.
            bool rejected = false;
            try { Validate(owners, receipts); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "contradictory incoming candidate was accepted");
            for (int index = 0; index < owners.Length; index++)
                Require(JsonUtility.ToJson(owners[index].Owner) == before[index],
                    "failed guard mutated incoming owner " + index);
            passed++;
        }
    }

    private static CropWaterRefillOwnerValidationSnapshot Owner(
        CropWaterRefillPhase phase = CropWaterRefillPhase.InputCommitted)
    {
        bool committed = phase is CropWaterRefillPhase.InputCommitted or CropWaterRefillPhase.OutcomePublished;
        return new CropWaterRefillOwnerValidationSnapshot
        {
            PlotId = Plot, NextOperationSequence = 3,
            Owner = new CropWaterRefillSaveData
            {
                phase = phase, operationSequence = 3, operationId = Operation,
                reasonCode = "crop-water-refill-input", destinationId = "crop-water-refill:qa-destination",
                itemId = "resource:clean-water", quantity = 1, requiredWork = 10,
                completedWork = committed ? 10 : 5, requestFingerprint = "local:wim016:refill-request",
                commitId = committed ? Commit : string.Empty,
                inputQuantity = committed ? 1 : 0, inputMassGrams = committed ? 1000 : 0,
                physicalRequestFingerprint = committed ? Fingerprint : string.Empty,
                sourceStackIds = committed ? new List<string> { Source } : new List<string>()
            }
        };
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Receipt(
        PhysicalItemDispositionKind kind = PhysicalItemDispositionKind.Transfer,
        string operation = Operation, string reason = "production.inputs-to-wip",
        string fingerprint = Fingerprint, string source = Source, int quantity = 1,
        long mass = 1000, string commit = Commit) =>
        new(kind, operation, reason, fingerprint, new[] { source }, quantity, mass, commit);

    private static void Validate(IReadOnlyCollection<CropWaterRefillOwnerValidationSnapshot> owners,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts) =>
        CropPhysicalRestoreGuard.ValidateWaterRefillOwnerSnapshots(owners, new CandidateQuery(receipts));

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        public CandidateQuery(IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> values) => PendingBatchDispositions = values;
        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions { get; }
        public bool TryGetPendingBatchDisposition(string operationId, out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
        {
            receipt = PendingBatchDispositions.FirstOrDefault(r => r.OperationId == operationId);
            return receipt != null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
