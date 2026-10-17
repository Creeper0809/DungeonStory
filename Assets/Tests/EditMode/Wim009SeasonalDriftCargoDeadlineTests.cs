using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Run
{
    public sealed class Wim009SeasonalDriftCargoDeadlineTests
    {
        [Test]
        public void ExpiredUnspawnedCargoTerminatesBeforeSelectingOrSpawning()
        {
            string adapter = ReadAdapterSource();
            Match guard = Regex.Match(
                adapter,
                @"if\s*\(\s*!cargo\.spawned\s*&&\s*"
                    + @"absoluteDay\s*>\s*occurrence\.deadlineAbsoluteDay\s*\)"
                    + @"(?<body>.*?)\n\s*if\s*\(\s*!items\.CatalogProvider",
                RegexOptions.Singleline);

            Assert.That(guard.Success, Is.True);
            Assert.That(
                guard.Groups["body"].Value,
                Does.Contain("campaign.TryExpireSeasonalDriftCargo("));
            Assert.That(
                guard.Groups["body"].Value,
                Does.Contain("Array.Empty<PhysicalItemTransformInput>()"));
            Assert.That(
                guard.Groups["body"].Value,
                Does.Contain("PublishCargoExpiration("));
            Assert.That(
                guard.Groups["body"].Value,
                Does.Not.Contain("TrySelectExteriorCargoCell("));
            Assert.That(
                guard.Groups["body"].Value,
                Does.Not.Contain("SpawnItemAtWithComponents("));
        }

        [Test]
        public void CargoCellRequiresActorSpecificReachablePlannerPickupStand()
        {
            string adapter = ReadAdapterSource();
            Match selector = Regex.Match(
                adapter,
                @"private bool TrySelectExteriorCargoCell\s*\("
                    + @"(?<body>.*?)\n\s*private bool TryGetHaulerSearch",
                RegexOptions.Singleline);
            Match search = Regex.Match(
                adapter,
                @"private bool TryGetHaulerSearch\s*\("
                    + @"(?<body>.*?)\n\s*private static bool HasReachablePickupStand",
                RegexOptions.Singleline);
            Match stand = Regex.Match(
                adapter,
                @"private static bool HasReachablePickupStand\s*\("
                    + @"(?<body>.*?)\n\s*private ",
                RegexOptions.Singleline);

            Assert.That(selector.Success, Is.True);
            Assert.That(selector.Groups["body"].Value,
                Does.Contain("value.CurrentLifecycleState == CharacterLifecycleState.Active"));
            Assert.That(selector.Groups["body"].Value,
                Does.Contain("value.CarryInventory != null"));
            Assert.That(selector.Groups["body"].Value,
                Does.Contain("OrderBy(value => PersistentEntityId.GetStableHash32("));
            Assert.That(selector.Groups["body"].Value,
                Does.Contain("HasReachablePickupStand(grid, reachable, value.Position)"));

            Assert.That(search.Success, Is.True);
            Assert.That(search.Groups["body"].Value,
                Does.Contain("GridPathSearchPriority.Normal"));
            Assert.That(search.Groups["body"].Value,
                Does.Contain("GridTraversalContext.ForCharacter("));
            Assert.That(search.Groups["body"].Value,
                Does.Contain("CharacterPersistentIdentity.Require(actor)"));

            Assert.That(stand.Success, Is.True);
            Assert.That(stand.Groups["body"].Value,
                Does.Contain("itemPosition,"));
            Assert.That(stand.Groups["body"].Value,
                Does.Contain("itemPosition + Vector2Int.left"));
            Assert.That(stand.Groups["body"].Value,
                Does.Contain("itemPosition + Vector2Int.right"));
            Assert.That(stand.Groups["body"].Value,
                Does.Contain("reachable.GetMoveCostTo(candidate) != int.MaxValue"));
        }

        private static string ReadAdapterSource() => File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts",
            "Services",
            "Run",
            "SeasonalWildlifeVisitorApplicationAdapter.cs"));
    }
}
