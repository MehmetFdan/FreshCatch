using System.Collections.Generic;
using FreshCatch.Model;
using NUnit.Framework;
using static FreshCatch.Model.FishType;

namespace FreshCatch.Tests
{
    public class LevelToolsTests
    {
        private static List<FishType> L(params FishType[] t) => new List<FishType>(t);

        [Test]
        public void Greedy_SolvesEveryBuiltInLevel()
        {
            var all = BuiltInLevels.All();
            for (var i = 0; i < all.Count; i++)
            {
                var r = LevelSolver.Play(all[i], null, 0f);
                Assert.IsTrue(r.Won, $"Built-in level {i + 1} lost by greedy: {r.Reason}, {r.Moves} moves");
            }
        }

        [Test]
        public void Greedy_LosesImpossibleLevel()
        {
            // One slot, the unwanted fish comes first and blocks the stall.
            var level = new LevelDefinition
            {
                StallSlots = 1, VisibleCustomers = 1, MaxRottenFish = 5, FreshnessMoves = 50,
                Boats = { L(Cipura), L(Hamsi) },
                Customers = { L(Hamsi) }
            };
            var r = LevelSolver.Play(level, null, 0f);
            Assert.IsFalse(r.Won);
            Assert.AreEqual(LoseReason.NoMovesLeft, r.Reason);
        }

        [Test]
        public void Analyze_IsDeterministicForSameSeed()
        {
            var level = BuiltInLevels.Get(3);
            var a = LevelSolver.Analyze(level, 50, 0.3f, 7);
            var b = LevelSolver.Analyze(BuiltInLevels.Get(3), 50, 0.3f, 7);
            Assert.AreEqual(a.Wins, b.Wins);
            Assert.IsTrue(a.Greedy.Won);
        }

        [Test]
        public void Analyze_EasyLevelRatesHigherThanHardLevel()
        {
            var easy = LevelSolver.Analyze(BuiltInLevels.Get(0), 100);
            var hard = LevelSolver.Analyze(BuiltInLevels.Get(5), 100);
            Assert.GreaterOrEqual(easy.WinRate, hard.WinRate);
        }

        [Test]
        public void Generator_ProducesValidLevels([Values(1, 2, 3, 42, 99)] int seed)
        {
            var s = new GeneratorSettings { FishTypes = 4, Customers = 6, MinOrder = 2, MaxOrder = 4, BoatSize = 5, ExtraFish = 2, Seed = seed };
            var level = LevelGenerator.Generate(s);
            CollectionAssert.IsEmpty(level.Validate());
            Assert.AreEqual(6, level.Customers.Count);
            foreach (var b in level.Boats) Assert.LessOrEqual(b.Count, 5);
        }

        [Test]
        public void Generator_SameSeedSameLevel()
        {
            var s = new GeneratorSettings { Seed = 5 };
            var a = LevelGenerator.Generate(s);
            var b = LevelGenerator.Generate(s);
            Assert.AreEqual(a.Boats.Count, b.Boats.Count);
            for (var i = 0; i < a.Boats.Count; i++) CollectionAssert.AreEqual(a.Boats[i], b.Boats[i]);
        }

        [Test]
        public void GenerateSolvable_ReturnsLevelGreedyCanWin()
        {
            var s = new GeneratorSettings { FishTypes = 3, Customers = 5, Seed = 10 };
            var target = new LevelDefinition { StallSlots = 6, VisibleCustomers = 3, MaxRottenFish = 1, FreshnessMoves = 10 };
            var seed = LevelGenerator.GenerateSolvable(s, target);
            Assert.GreaterOrEqual(seed, 10);
            Assert.IsTrue(LevelSolver.Play(target, null, 0f).Won);
            Assert.AreEqual(6, target.StallSlots, "Generator must keep the target's settings.");
        }
    }
}
