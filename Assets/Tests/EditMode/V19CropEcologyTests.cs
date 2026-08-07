using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DungeonStory.Tests.Architecture
{
    public sealed class V19CropEcologyTests
    {
        [Test]
        public void PhysicalSeedLotRoundTripsAsStackAffectingItemState()
        {
            SeedLotState source = new()
            {
                cropId = "crop:twilight-grain",
                cultivarGenomeId = "genome:twilight-grain:base",
                quality = 82.5f,
                generation = 3,
                pathogenLoad = 4.25f
            };
            ItemInstanceComponentSaveData component = SeedLotItemStateCodec.Encode(source);
            SeedLotState restored = SeedLotItemStateCodec.Decode(new[] { component });
            Assert.That(component.affectsStacking, Is.True);
            Assert.That(component.componentTypeId, Is.EqualTo(ItemInstanceComponentIds.SeedLot));
            Assert.That(restored.cropId, Is.EqualTo(source.cropId));
            Assert.That(restored.cultivarGenomeId, Is.EqualTo(source.cultivarGenomeId));
            Assert.That(restored.quality, Is.EqualTo(source.quality).Within(0.001f));
            Assert.That(restored.generation, Is.EqualTo(source.generation));
            Assert.That(restored.pathogenLoad, Is.EqualTo(source.pathogenLoad).Within(0.001f));
        }

        [Test]
        public void RotationFertilityPestsDeathAndSeedReturnFollowApprovedRules()
        {
            CropEcologyAggregateState state = CreateState();
            SeedLotState seed = BaseSeed();
            state.Sow("building:plot-a", CropFamilyGroup.Grain, seed);
            CropHarvestEcologyResult first = state.Harvest(
                "building:plot-a", ConstantRandom(0.5d));
            CropEcologyPlotSaveData afterFirst = state.Plots.Single();
            Assert.That(afterFirst.fertility, Is.EqualTo(85f));
            Assert.That(first.ReturnedSeedCount, Is.InRange(2, 4));

            state.ApplyCompost("building:plot-a");
            Assert.That(state.Plots.Single().fertility, Is.EqualTo(100f));
            state.Sow("building:plot-a", CropFamilyGroup.Grain, first.ReturnedSeedLot);
            CropEcologyPlotSaveData repeated = state.Plots.Single();
            Assert.That(repeated.pestPressure, Is.EqualTo(15f));
            Assert.That(repeated.diseasePressure, Is.EqualTo(10f).Within(0.001f));

            CropHarvestEcologyResult repeatedHarvest = state.Harvest(
                "building:plot-a", ConstantRandom(0.5d));
            Assert.That(repeatedHarvest.YieldMultiplier, Is.LessThan(0.85f));

            CropEcologyWorldSaveData highPest = state.Capture();
            CropEcologyPlotSaveData plot = highPest.plots.Single();
            plot.cropId = seed.cropId;
            plot.cultivarGenomeId = seed.cultivarGenomeId;
            plot.currentGroup = CropFamilyGroup.Grain;
            plot.pestPressure = 85f;
            plot.cropDead = false;
            CropEcologyAggregateState restored = CropEcologyAggregateState.Restore(highPest);
            Assert.That(restored.AdvanceDay("building:plot-a", false, () => 0.24d), Is.False);
            Assert.That(restored.Plots.Single().cropDead, Is.True);
        }

        [Test]
        public void CultivarMutationAndSaveRoundTripAreDeterministic()
        {
            CropEcologyAggregateState state = CreateState();
            state.Sow("building:plot-a", CropFamilyGroup.Grain, BaseSeed());
            Queue<double> values = new(new[]
            {
                0d, 0d, 0d,
                1d, 1d, 1d, 1d, 1d,
                0.5d
            });
            CropHarvestEcologyResult harvest = state.Harvest(
                "building:plot-a",
                () => values.Count > 0 ? values.Dequeue() : 0.5d);
            Assert.That(harvest.ReturnedSeedLot.generation, Is.EqualTo(1));
            Assert.That(harvest.ReturnedSeedLot.cultivarGenomeId,
                Does.StartWith("genome:crop:twilight-grain:g1:").Or.StartWith("genome:twilight-grain:g1:"));
            CropEcologyAggregateState restored = CropEcologyAggregateState.Restore(state.Capture());
            Assert.That(restored.Capture().activeCultivars.Select(value => value.genomeId),
                Is.EquivalentTo(state.Capture().activeCultivars.Select(value => value.genomeId)));
        }

        [Test]
        public void InitialPhysicalSeedGrantIsExactlyOnceAndPersistent()
        {
            CropEcologyAggregateState state = new();
            foreach (int index in Enumerable.Range(0, 8))
            {
                state.RegisterBaseGenome(new CultivarGenomeSaveData
                {
                    genomeId = $"genome:test-{index}:base",
                    cropId = $"crop:test-{index}",
                    loci = Enum.GetValues(typeof(CropGenomeLocus)).Cast<CropGenomeLocus>()
                        .Select(value => new DiploidLocusSaveData { locus = value }).ToList()
                });
            }
            Assert.That(state.TryClaimInitialSeedGrant(out IReadOnlyList<SeedLotState> first), Is.True);
            Assert.That(first.Count, Is.EqualTo(8));
            Assert.That(first.All(value => value.quality == 70f && value.generation == 0), Is.True);
            Assert.That(state.TryClaimInitialSeedGrant(out IReadOnlyList<SeedLotState> second), Is.False);
            Assert.That(second, Is.Empty);
            CropEcologyAggregateState restored = CropEcologyAggregateState.Restore(state.Capture());
            Assert.That(restored.TryClaimInitialSeedGrant(out _), Is.False);
        }

        private static CropEcologyAggregateState CreateState()
        {
            CropEcologyAggregateState state = new();
            state.RegisterBaseGenome(new CultivarGenomeSaveData
            {
                genomeId = "genome:twilight-grain:base",
                cropId = "crop:twilight-grain",
                loci = Enum.GetValues(typeof(CropGenomeLocus)).Cast<CropGenomeLocus>()
                    .Select(value => new DiploidLocusSaveData { locus = value }).ToList()
            });
            return state;
        }

        private static SeedLotState BaseSeed() => new()
        {
            cropId = "crop:twilight-grain",
            cultivarGenomeId = "genome:twilight-grain:base",
            quality = 70f
        };

        private static Func<double> ConstantRandom(double value) => () => value;
    }
}
