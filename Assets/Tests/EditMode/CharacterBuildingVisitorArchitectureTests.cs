using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class CharacterBuildingVisitorArchitectureTests
    {
        private static string ScriptPath(string relativePath)
        {
            return Path.Combine(Application.dataPath, "Scripts", relativePath);
        }

        [Test]
        public void CharacterActorOwnsIdentityWhileAdapterOwnsBuildingVisits()
        {
            string actorPath = ScriptPath("Services/Character/Core/CharacterActor.cs");
            string adapterPath = ScriptPath(
                "Services/Character/Core/CharacterBuildingVisitorPort.cs");
            string bridgePath = ScriptPath(
                "Services/Character/Core/CharacterActorRuntimeBridge.cs");
            string actor = File.ReadAllText(actorPath);
            string adapter = File.ReadAllText(adapterPath);
            string bridge = File.ReadAllText(bridgePath);

            Assert.That(File.ReadAllLines(actorPath).Length, Is.LessThanOrEqualTo(2000));
            Assert.That(File.ReadAllLines(adapterPath).Length, Is.LessThanOrEqualTo(2000));
            Assert.That(actor, Does.Contain("public class CharacterActor"));
            Assert.That(actor, Does.Not.Contain("partial class CharacterActor"));
            Assert.That(actor, Does.Contain("runtimeBridge.GetBuildingVisitor(this)"));
            Assert.That(actor, Does.Not.Contain("CharacterBuildingVisitorAdapter"));
            Assert.That(bridge, Does.Contain("buildingVisitor ??="));
            Assert.That(bridge, Does.Contain("new CharacterBuildingVisitorAdapter(owner)"));

            int declaration = actor.IndexOf(
                "public class CharacterActor",
                System.StringComparison.Ordinal);
            int body = actor.IndexOf('{', declaration);
            Assert.That(declaration, Is.GreaterThanOrEqualTo(0));
            Assert.That(body, Is.GreaterThan(declaration));
            Assert.That(
                actor.Substring(declaration, body - declaration),
                Does.Not.Contain("IBuildingVisitorPort"));

            Assert.That(
                adapter,
                Does.Contain("sealed class CharacterBuildingVisitorAdapter : IBuildingVisitorPort"));
            Assert.That(adapter, Does.Contain("private readonly CharacterActor actor"));
            Assert.That(adapter, Does.Contain("internal static bool TryGetActor("));
            Assert.That(adapter, Does.Not.Contain("partial class CharacterActor"));

            string actorMeta = File.ReadAllText(actorPath + ".meta");
            Assert.That(
                actorMeta,
                Does.Contain("guid: 5e39f730361f44bb874d5ceba8f31a70"));
        }

        [Test]
        public void AdapterRetainsVisitorBehaviorAndCallersUseTheFacade()
        {
            string adapter = File.ReadAllText(ScriptPath(
                "Services/Character/Core/CharacterBuildingVisitorPort.cs"));
            foreach (string marker in new[]
                     {
                         "new BuildingVisitorSnapshot(",
                         "IBuildingVisitorPort.Shopping",
                         "IBuildingVisitorPort.MoveTo(",
                         "IBuildingVisitorPort.RecordActivity(",
                         "IBuildingVisitorPort.RememberFacilityExperience(",
                         "IBuildingVisitorPort.ApplyNeedRecovery(",
                         "IBuildingVisitorPort.TryConsumeMeal(",
                         "IBuildingVisitorPort.ApplyRoomExperience(",
                         "IBuildingVisitorPort.ApplyFacilityUseCompleted(",
                         "IBuildingVisitorPort.ApplyExpeditionRecovery(",
                         "IBuildingVisitorPort.AddCarriedItem("
                     })
            {
                Assert.That(adapter, Does.Contain(marker), marker);
            }

            foreach (string relativePath in new[]
                     {
                         "Services/Character/CharacterSpawner.cs",
                         "Services/Infrastructure/BuildingCharacterFacilityAdapters.cs",
                         "Services/Infrastructure/BuildingCraftWorkAdapters.cs"
                     })
            {
                string source = File.ReadAllText(ScriptPath(relativePath));
                Assert.That(source, Does.Not.Contain("(IBuildingVisitorPort)"));
                Assert.That(source, Does.Not.Contain("as IBuildingVisitorPort"));
            }

            Assert.That(
                File.ReadAllText(ScriptPath(
                    "Services/Operation/OperatingDaySettlement.cs")),
                Does.Contain("CharacterBuildingVisitorAdapter.GetActorOrNull("));
            Assert.That(
                File.ReadAllText(ScriptPath(
                    "Services/Wildlife/WildlifeCarcassService.cs")),
                Does.Contain("CharacterBuildingVisitorAdapter.TryGetActor("));
        }
    }
}
