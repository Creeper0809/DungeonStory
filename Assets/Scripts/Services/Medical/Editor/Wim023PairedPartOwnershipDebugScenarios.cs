#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Root-owned detached preflight witness, not a live surgery or whole-save replay.
public static class Wim023PairedPartOwnershipDebugScenarios
{
    public static string RunFocused()
    {
        var authored = AssetDatabase.LoadAssetAtPath<AnatomyProfileSO>(
            "Assets/Resources/SO/Medical/Anatomy/anatomy_humanoid.asset");
        Require(authored != null, "Authored humanoid profile missing.");
        IAnatomyProfileCatalog profiles = new ResourceAnatomyProfileCatalog(new[] { authored });
        var physical = new DungeonPhysicalItemSaveData
        {
            stacks = new List<WorldItemStackSaveData>(),
            pendingBatchDispositions = new List<PhysicalItemBatchDispositionSaveData>()
        };
        var surgery = NewSurgery();
        var body = NewBody("arm:left");
        Validate(physical, surgery, body, profiles);

        body.characters[0].anatomyNodes[0].nodeId = "arm:right";
        Validate(physical, surgery, body, profiles);
        // Persisted intrinsic identity stays left; the anatomy forward link owns right.
        var restoredSurgery = JsonUtility.FromJson<DungeonSurgerySaveData>(JsonUtility.ToJson(surgery));
        var restoredBody = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(JsonUtility.ToJson(body));
        var restoredPhysical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(physical));
        Require(restoredSurgery.parts[0].nodeId == "arm:left"
            && restoredBody.characters[0].anatomyNodes[0].nodeId == "arm:right",
            "JSON round trip rewrote intrinsic or attachment identity.");
        Validate(restoredPhysical, restoredSurgery, restoredBody, profiles);

        Reject("different authored pair", "no exact surgery owner", s => { }, b => b.characters[0].anatomyNodes[0].nodeId = "leg:left");
        Reject("wrong subject", "no exact surgery owner", s => s.parts[0].installedSubjectId = "character:qa:other", b => { });
        Reject("missing forward reference", "lacks one anatomy owner", s => { }, b => b.characters[0].anatomyNodes.Clear());
        Reject("duplicate anatomy owner", "lacks one anatomy owner", s => { }, b =>
            b.characters[0].anatomyNodes.Add(NewNode("arm:left")));
        Reject("missing part owner", "no exact surgery owner", s => s.parts.Clear(), b => { });
        Reject("missing authored profile", "unknown anatomy profile", s => { }, b => b.characters[0].anatomyProfileId = "anatomy:qa:missing");
        Reject("wrong installed kind", "no exact surgery owner", s => { }, b => b.characters[0].anatomyNodes[0].installedPartKind = SurgicalPartKind.Implant);
        Reject("unknown exact node", "no exact surgery owner", s => s.parts[0].nodeId = "node:qa:missing",
            b => b.characters[0].anatomyNodes[0].nodeId = "node:qa:missing");
        return "PASS exact+authored paired+current JSON; rejected8 invalid joins unchanged; scope=detached ownership preflight only, not live install/effects/maxHP/whole-world restore";

        void Reject(string label, string expectedReason, Action<DungeonSurgerySaveData> editSurgery, Action<DungeonCharacterBodyHealthSaveData> editBody)
        {
            var invalidSurgery = NewSurgery();
            var invalidBody = NewBody("arm:right");
            editSurgery(invalidSurgery);
            editBody(invalidBody);
            string beforeSurgery = JsonUtility.ToJson(invalidSurgery);
            string beforeBody = JsonUtility.ToJson(invalidBody);
            string beforePhysical = JsonUtility.ToJson(physical);
            bool rejected = false;
            try { Validate(physical, invalidSurgery, invalidBody, profiles); }
            catch (InvalidOperationException exception)
            {
                Require(exception.Message.IndexOf(expectedReason, StringComparison.OrdinalIgnoreCase) >= 0,
                    "Wrong rejection for " + label + ": " + exception.Message);
                rejected = true;
            }
            Require(rejected, "Ownership preflight accepted " + label);
            Require(JsonUtility.ToJson(invalidSurgery) == beforeSurgery
                && JsonUtility.ToJson(invalidBody) == beforeBody
                && JsonUtility.ToJson(physical) == beforePhysical,
                "Rejected preflight mutated detached data: " + label);
        }
    }

    private static void Validate(DungeonPhysicalItemSaveData physical, DungeonSurgerySaveData surgery,
        DungeonCharacterBodyHealthSaveData body, IAnatomyProfileCatalog profiles)
    {
        string beforePhysical = JsonUtility.ToJson(physical), beforeSurgery = JsonUtility.ToJson(surgery), beforeBody = JsonUtility.ToJson(body);
        SurgicalPartProductionOutputCrossAggregateSaveValidation.ValidatePartOwnership(physical, surgery, body, profiles);
        Require(JsonUtility.ToJson(physical) == beforePhysical && JsonUtility.ToJson(surgery) == beforeSurgery
            && JsonUtility.ToJson(body) == beforeBody, "Successful preflight mutated detached input.");
    }

    private static DungeonSurgerySaveData NewSurgery() => new()
    {
        parts = new List<SurgicalPartInstance>
        {
            new()
            {
                partInstanceId = "surgical-part:qa:paired-arm",
                itemDefinitionId = "component:prosthetic-arm",
                physicalItemInstanceId = "item-instance:qa:paired-arm",
                nodeId = "arm:left",
                kind = SurgicalPartKind.Prosthetic,
                installed = true,
                installedSubjectId = "character:qa:paired-owner",
                quality = 1f
            }
        }
    };

    private static DungeonCharacterBodyHealthSaveData NewBody(string nodeId) => new()
    {
        characters = new List<CharacterBodyHealthState>
        {
            new()
            {
                characterId = "character:qa:paired-owner",
                anatomyProfileId = "anatomy:humanoid",
                anatomyNodes = new List<AnatomyNodeHealthState> { NewNode(nodeId) }
            }
        }
    };

    private static AnatomyNodeHealthState NewNode(string nodeId) => new()
    {
        nodeId = nodeId,
        maxHealth = 20f,
        currentHealth = 20f,
        installedPartId = "surgical-part:qa:paired-arm",
        installedPartKind = SurgicalPartKind.Prosthetic
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
