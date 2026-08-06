using System;
using NUnit.Framework;

namespace DungeonStory.Tests.Foundation
{
    public sealed class CharacterIdTests
    {
        [TestCase(
            "invasion:raid-a:intruder:1",
            "character:invasion:raid-a:intruder:1")]
        [TestCase(
            "faction-route:4:ally:2",
            "character:faction-route:4:ally:2")]
        [TestCase(
            "return:7:prisoner:3",
            "character:return:7:prisoner:3")]
        [TestCase(
            "incident:Thief:9:actor",
            "character:incident:Thief:9:actor")]
        public void FromStableSuffixPreservesOperationalIdentity(
            string suffix,
            string expected)
        {
            CharacterId id = CharacterId.FromStableSuffix(suffix);

            Assert.That(id.IsValid, Is.True);
            Assert.That(id.Value, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("owner")]
        [TestCase("character:already-scoped")]
        public void FromStableSuffixRejectsMissingOrAlreadyScopedValues(string suffix)
        {
            Assert.Throws<ArgumentException>(
                () => CharacterId.FromStableSuffix(suffix));
        }

        [TestCase(
            "invasion:0123456789abcdef0123456789abcdef",
            "character:invasion:0123456789abcdef0123456789abcdef")]
        [TestCase(
            "faction-route:4:ally:2",
            "character:faction-route:4:ally:2")]
        [TestCase(
            "return:7:prisoner:3",
            "character:return:7:prisoner:3")]
        [TestCase(
            "incident:Thief:9:actor",
            "character:incident:Thief:9:actor")]
        public void RestoreCanonicalizationAcceptsExactEarlyV18OperationalIds(
            string legacy,
            string expected)
        {
            bool resolved = CharacterId.TryCanonicalizeV18Restore(
                legacy,
                out CharacterId canonical,
                out bool wasLegacy);

            Assert.That(resolved, Is.True);
            Assert.That(wasLegacy, Is.True);
            Assert.That(canonical.Value, Is.EqualTo(expected));
        }

        [TestCase("invasion:raid-a")]
        [TestCase("faction-route:04:ally:2")]
        [TestCase("faction-route:4:ally:02")]
        [TestCase("return:07:prisoner:3")]
        [TestCase("return:7:prisoner:03")]
        [TestCase("incident:None:9:actor")]
        [TestCase("incident:Thief:09:actor")]
        [TestCase(" staff:73:01")]
        [TestCase("staff:73:01 ")]
        [TestCase(" character:staff:73:01")]
        public void RestoreCanonicalizationRejectsNearbyMalformedOperationalIds(
            string malformed)
        {
            Assert.That(
                CharacterId.TryCanonicalizeV18Restore(
                    malformed,
                    out _,
                    out _),
                Is.False);
        }
    }
}
