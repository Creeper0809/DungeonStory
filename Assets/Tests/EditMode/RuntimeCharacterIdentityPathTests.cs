using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    internal static class RuntimeCharacterIdentityPathContract
    {
        public static void AssertOperationalCharacterCreationPathsUseTypedCharacterScope()
        {
            string invasion = ReadSource(
                "Scripts/Services/Invasion/InvasionIntruderSystem.cs");
            string invasionRestore = ReadSource(
                "Scripts/Services/Invasion/InvasionIntruderRuntime.Restore.cs");
            string factions = ReadSource(
                "Scripts/Services/Factions/FactionRuntime.cs");
            string offense = ReadSource(
                "Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs");
            string exterior = ReadSource(
                "Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs");
            string population = ReadSource(
                "Scripts/Models/Characters/CharacterPopulationDomain.cs");
            string startParty = ReadSource(
                "Scripts/Services/Character/Core/StartPartyPreparationService.cs");
            string startPartyApplier = ReadSource(
                "Scripts/Services/Character/Core/PreparedStartPartyGameplayApplier.cs");
            string regularCustomers = ReadSource(
                "Scripts/Services/Recruitment/RegularCustomerSystem.cs");
            string identity = ReadSource(
                "Scripts/Services/Character/Core/CharacterIdentity.cs");
            string lifePublication = ReadSource(
                "Scripts/Services/Character/CharacterLifePublicationService.cs");

            Assert.That(
                invasion,
                Does.Contain("CharacterId.FromStableSuffix(runtimeId)"),
                "Invasion actors must retain their invasion runtime ID as a character-scoped suffix.");
            Assert.That(
                invasion,
                Does.Not.Contain("SetPersistentId(runtimeId)"),
                "The untyped invasion runtime ID must never be assigned as a CharacterId.");
            Assert.That(
                invasionRestore,
                Does.Contain("CharacterId.FromStableSuffix(source.RuntimeId)"),
                "Restored invasion actors must rebuild the same character-scoped identity.");
            Assert.That(
                invasionRestore,
                Does.Not.Contain("SetPersistentId(source.RuntimeId)"),
                "Restore must not assign the untyped invasion runtime ID as a CharacterId.");

            Assert.That(
                factions,
                Does.Contain("CharacterId actorId = CharacterId.FromStableSuffix("));
            Assert.That(factions, Does.Contain("$\"{route.routeId}:ally:{index + 1}\""));
            Assert.That(factions, Does.Contain("AddReinforcementActor(route, actorId.Value)"));

            Assert.That(
                offense,
                Does.Contain("CharacterId characterId = CharacterId.FromStableSuffix("));
            Assert.That(
                offense,
                Does.Contain("$\"{arrival.arrivalId}:prisoner:{arrival.materializedIds.Count + 1}\""));
            Assert.That(offense, Does.Contain("actorId = characterId.Value"));

            Assert.That(
                exterior,
                Does.Contain("string actorId = CharacterId.FromStableSuffix("));
            Assert.That(exterior, Does.Contain("$\"{state.incidentId}:actor\").Value"));

            Assert.That(
                population,
                Does.Contain("CharacterId.FromStableSuffix("));
            Assert.That(
                population,
                Does.Contain("$\"world:{runSeed}:{creationSerial:D6}\").Value"));

            Assert.That(
                startParty,
                Does.Contain("CharacterId.FromStableSuffix("));
            Assert.That(
                startParty,
                Does.Contain("$\"staff:{runSeed}:{index + 1:D2}\").Value"));
            Assert.That(
                startPartyApplier,
                Does.Contain("persistentId.StartsWith(\"character:staff:\""));
            Assert.That(
                startPartyApplier,
                Does.Not.Contain("persistentId.StartsWith(\"staff:\""));

            Assert.That(
                regularCustomers,
                Does.Contain("CharacterId customerId = (CharacterId)record.CustomerId"),
                "Recruited-character restore data must be rejected before it can reach SetPersistentId.");
            Assert.That(
                regularCustomers,
                Does.Contain("!customerId.IsValid || customerId.Equals(CharacterId.Owner)"));

            Assert.That(identity, Does.Contain(
                "private bool characterTypeExplicitlyAssigned;"));
            Assert.That(identity, Does.Contain(
                "else if (!characterTypeExplicitlyAssigned)"),
                "Rebinding authoring data must not overwrite an explicitly restored runtime character type.");
            Assert.That(identity, Does.Contain(
                "characterTypeExplicitlyAssigned = true;"));
            Assert.That(lifePublication, Does.Contain(
                "if (!IsPersistentLifeActor(actor))"));
            Assert.That(lifePublication, Does.Contain(
                "actor.Identity?.CharacterType == CharacterType.NPC"));
            Assert.That(lifePublication, Does.Contain(
                "actor.TryGetAbility(out AbilityWork _)"),
                "Transient invasion and encounter actors must not enter the persistent life aggregate.");
        }

        private static string ReadSource(string assetRelativePath)
        {
            string path = Path.Combine(
                Application.dataPath,
                assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(path), Is.True, $"Missing source contract: {path}");
            return File.ReadAllText(path);
        }
    }
}
