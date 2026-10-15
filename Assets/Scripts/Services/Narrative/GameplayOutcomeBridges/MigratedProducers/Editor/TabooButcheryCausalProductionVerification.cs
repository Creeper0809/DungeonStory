#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class TabooButcheryCausalProductionEntryDebugScenarios
{
    public static void RunBoundaryFromEnvironment()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_TABOO_OWNER_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "Boundary output is required.");
        // First prove accepted inputs in the same source/process, then mutate them.
        RunBatchFromEnvironment();
        JObject first = JObject.Parse(File.ReadAllText(Path.Combine(output, "first-checkpoint.json")));
        JObject terminal = JObject.Parse(File.ReadAllText(Path.Combine(output, "terminal-checkpoint.json")));
        var rejects = new JArray();
        foreach (var kind in new[]
        {
            MigratedProducerOutcomeKind.CharacterDeathEvent,
            MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
            MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent
        })
        foreach (var fault in new[] { MigratedProducerOutcomeFaultMode.RejectReservation, MigratedProducerOutcomeFaultMode.RejectCommit })
        {
            // Downed is reserved then cancelled by this authored Kill; its commit
            // is never selected, so do not claim to inject a reached commit.
            if (kind == MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent
                && fault == MigratedProducerOutcomeFaultMode.RejectCommit) continue;
            bool death = kind is MigratedProducerOutcomeKind.CharacterDeathEvent
                or MigratedProducerOutcomeKind.HealthThresholdCrossedEvent
                or MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent;
            using var context = new Context(FocusedOwner, death ? null : Copy(first));
            JObject before = context.RejectionState();
            context.ConfigureFault(kind, fault);
            string failure = null;
            try { if (death) context.Kill(); else context.Butcher(); }
            catch (InvalidOperationException exception) { failure = exception.Message; }
            Require(failure != null, "Injected rejection did not reach production: " + kind + "/" + fault);
            JObject after = context.RejectionState();
            Require(JToken.DeepEquals(before, after), "Rejected action changed gameplay: " + kind + "/" + fault
                + " before=" + before + " after=" + after);
            rejects.Add(new JObject { ["kind"] = kind.ToString(), ["fault"] = fault.ToString(),
                ["failure"] = failure, ["before"] = before, ["after"] = after,
                ["monotonicStateAfter"] = context.Checkpoint()["domainState"].DeepClone() });
            context.ConfigureFault(kind, MigratedProducerOutcomeFaultMode.None);
            Require(death ? context.Kill() : context.Butcher(), "Legitimate retry after rejection failed.");
        }
        var tamper = new JArray();
        void Reject(string label, JObject basis, Action<JObject> mutate)
        {
            JObject changed = Copy(basis); mutate(changed);
            string failure = null;
            try { using var invalid = new Context(FocusedOwner, changed); }
            catch (InvalidOperationException exception) { failure = exception.Message; }
            Require(failure != null, "Tampered checkpoint accepted: " + label);
            tamper.Add(new JObject { ["case"] = label, ["failure"] = failure });
        }
        Reject("owner", first, value => value["ownerId"] = "taboo-owner:foreign");
        Reject("family", first, value => value["ownerRestoreKind"] = "owner-family:foreign");
        Reject("schema", first, value => value["domainState"]["schemaVersion"] = 2);
        Reject("day", first, value => value["domainState"]["day"] = 8);
        Reject("stage", first, value => value["domainState"]["stage"] = 2);
        Reject("actor-name", first, value => value["domainState"]["actors"][0]["displayName"] = "forged");
        Reject("corpse-source", first, value => value["domainState"]["physical"]["stacks"][0]["sourceCharacterId"] = "character:foreign");
        Reject("corpse-quantity", first, value => value["domainState"]["physical"]["stacks"][0]["quantity"] = 2);
        Reject("victim-revived", first, value =>
        {
            foreach (JToken row in (JArray)value["domainState"]["bodyHealth"]["characters"])
                if (((string)row["characterId"]).Contains("taboo-victim")) row["currentHealth"] = 1f;
        });
        Reject("missing-tombstone", first, value =>
        {
            var rows = (JArray)value["domainState"]["bodyHealth"]["characters"];
            for (int index = rows.Count - 1; index >= 0; index--)
                if (((string)rows[index]["characterId"]).Contains("taboo-victim")) rows.RemoveAt(index);
        });
        Reject("blood-amount", first, value => value["domainState"]["deprivation"]["filth"][0]["amount"] = 11f);
        Reject("negative-cursor", first, value => value["domainState"]["idSequence"]["stacks"] = -1);
        Reject("missing-witness-mood", terminal, value => ((JArray)value["domainState"]["actors"][0]["moodFactors"]).Clear());
        Reject("missing-witness-memory", terminal, value => ((JArray)value["domainState"]["actors"][0]["socialMemory"]["recentRumors"]).Clear());
        Reject("witness-day", terminal, value => value["domainState"]["actors"][0]["narrative"]["facts"][0]["lastDay"] = 0);
        using (var dates = new Context(FocusedOwner)) dates.TestDatedMoodBoundary();
        WriteNew(Path.Combine(output, "boundary-report.json"), new JObject
        {
            ["status"] = "PASS_OWNER_BOUNDARIES", ["rejections"] = rejects, ["tamper"] = tamper,
            ["datedMoodCases"] = 6, ["wholeWorldSave"] = false,
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false, ["releaseGate"] = "REJECT_BEFORE_DPO"
        });
        Debug.Log("TABOO_OWNER_BOUNDARY=PASS; rejections=" + rejects.Count + "; tamper=" + tamper.Count + "; date=6");
    }
}
#endif
