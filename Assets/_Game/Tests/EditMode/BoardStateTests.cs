using System.Collections.Generic;
using FreshCatch.Model;
using NUnit.Framework;
using static FreshCatch.Model.FishType;

namespace FreshCatch.Tests
{
    public class BoardStateTests
    {
        private static List<FishType> L(params FishType[] t) => new List<FishType>(t);

        private static LevelDefinition SimpleLevel() => new LevelDefinition
        {
            StallSlots = 4,
            VisibleCustomers = 2,
            MaxRottenFish = 0,
            FreshnessMode = FreshnessMode.MoveBased,
            FreshnessMoves = 20,
            Boats = { L(Hamsi, Hamsi, Levrek) },
            Customers = { L(Hamsi, Hamsi), L(Levrek) }
        };

        private static BoardState Started(LevelDefinition level)
        {
            var b = new BoardState(level);
            b.Start();
            return b;
        }

        // ---------------------------------------------------------------- Fish

        [Test]
        public void Fish_StagesProgress_FreshFadedRotten()
        {
            var f = new Fish(Hamsi, 4);
            Assert.AreEqual(FreshnessStage.Fresh, f.Stage);
            f.Age(2);
            Assert.AreEqual(FreshnessStage.Faded, f.Stage);
            Assert.IsTrue(f.Age(5));
            Assert.AreEqual(FreshnessStage.Rotten, f.Stage);
            Assert.IsFalse(f.Age(1), "Already rotten fish must not report rotting again.");
        }

        [Test]
        public void Fish_RottenCannotBeRefreshed()
        {
            var f = new Fish(Hamsi, 2);
            f.Age(2);
            f.Refresh(10);
            Assert.IsTrue(f.IsRotten);
        }

        // ---------------------------------------------------------------- Level

        [Test]
        public void Validate_FlagsMissingSupply()
        {
            var level = SimpleLevel();
            level.Customers.Add(L(Cipura));
            var errors = level.Validate();
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("Cipura", errors[0]);
        }

        [Test]
        public void Validate_SimpleLevelIsValid()
        {
            CollectionAssert.IsEmpty(SimpleLevel().Validate());
        }

        [Test]
        public void BuiltInLevels_AreAllValid()
        {
            var all = BuiltInLevels.All();
            for (var i = 0; i < all.Count; i++)
                CollectionAssert.IsEmpty(all[i].Validate(), $"Built-in level {i + 1}");
        }

        // ---------------------------------------------------------------- Flow

        [Test]
        public void Start_DocksBoatAndSeatsVisibleCustomers()
        {
            var b = Started(SimpleLevel());
            Assert.AreEqual(3, b.CurrentBoat.Fish.Count);
            Assert.AreEqual(2, b.ActiveCustomers.Count);
            Assert.AreEqual(GameResult.Playing, b.Result);
        }

        [Test]
        public void TapBoatFish_MovesFishToFirstFreeSlot()
        {
            var b = Started(SimpleLevel());
            int slot = -1;
            b.FishMovedToStall += (_, s) => slot = s;

            Assert.IsTrue(b.TapBoatFish(0));
            Assert.AreEqual(0, slot);
            Assert.AreEqual(Hamsi, b.Stall[0].Type);
            Assert.AreEqual(2, b.CurrentBoat.Fish.Count);
            Assert.AreEqual(1, b.MoveCount);
        }

        [Test]
        public void FullManualPlaythrough_Wins()
        {
            var b = Started(SimpleLevel());
            GameResult? ended = null;
            b.GameEnded += (r, _) => ended = r;

            b.TapBoatFish(0); b.TapBoatFish(0); b.TapBoatFish(0); // H, H, L onto stall
            Assert.IsTrue(b.TapStallFish(0));
            Assert.IsTrue(b.TapStallFish(1)); // first customer satisfied
            Assert.IsTrue(b.TapStallFish(2)); // second customer satisfied

            Assert.AreEqual(GameResult.Won, b.Result);
            Assert.AreEqual(GameResult.Won, ended);
            Assert.AreEqual(2, b.CustomersServed);
        }

        [Test]
        public void TapStallFish_NobodyWantsIt_Rejected()
        {
            var level = SimpleLevel();
            level.Boats[0].Add(Cipura);
            var b = Started(level);
            b.TapBoatFish(3); // Cipura -> slot 0
            Assert.IsFalse(b.TapStallFish(0));
            Assert.AreEqual(1, b.MoveCount);
        }

        [Test]
        public void AutoServe_SendsFishStraightToCustomer()
        {
            var level = SimpleLevel();
            level.AutoServe = true;
            var b = Started(level);
            b.TapBoatFish(0); b.TapBoatFish(0); b.TapBoatFish(0);
            Assert.AreEqual(0, b.Stall.Count);
            Assert.AreEqual(GameResult.Won, b.Result);
        }

        [Test]
        public void CustomerQueue_RefillsWhenOneLeaves()
        {
            var level = SimpleLevel();
            level.VisibleCustomers = 1;
            level.AutoServe = true;
            var b = Started(level);
            Assert.AreEqual(1, b.ActiveCustomers.Count);
            b.TapBoatFish(0); b.TapBoatFish(0); // first customer done
            Assert.AreEqual(1, b.ActiveCustomers.Count);
            Assert.IsTrue(b.ActiveCustomers[0].Wants(Levrek));
        }

        [Test]
        public void NextBoatDocks_WhenCurrentIsEmpty()
        {
            var level = SimpleLevel();
            level.Boats = new List<List<FishType>> { L(Hamsi), L(Hamsi, Levrek) };
            var b = Started(level);
            Assert.AreEqual(1, b.BoatsRemaining);
            b.TapBoatFish(0);
            Assert.AreEqual(2, b.CurrentBoat.Fish.Count);
            Assert.AreEqual(0, b.BoatsRemaining);
        }

        // ---------------------------------------------------------------- Lose conditions

        [Test]
        public void MoveBased_FishRotsOnStall_Loses()
        {
            var level = SimpleLevel();
            level.FreshnessMoves = 2;
            level.Boats[0].Add(Cipura); // nobody wants it, will rot
            var b = Started(level);
            b.TapBoatFish(3); // Cipura on stall, aged 1
            b.TapBoatFish(0); // aged 2 -> rotten
            Assert.AreEqual(1, b.RottenCount);
            Assert.AreEqual(GameResult.Lost, b.Result);
            Assert.AreEqual(LoseReason.TooManyRotten, b.LoseReason);
        }

        [Test]
        public void TimeBased_TickAgesStallFish_ButNotBoatFish()
        {
            var level = SimpleLevel();
            level.FreshnessMode = FreshnessMode.TimeBased;
            level.FreshnessSeconds = 10;
            level.MaxRottenFish = 5;
            var b = Started(level);
            b.TapBoatFish(0);
            b.Tick(4);
            Assert.AreEqual(6f, b.Stall[0].Freshness, 0.001f);
            Assert.AreEqual(10f, b.CurrentBoat.Fish[0].Freshness, 0.001f);
            b.Tick(6);
            Assert.IsTrue(b.Stall[0].IsRotten);
            Assert.AreEqual(1, b.RottenCount);
        }

        [Test]
        public void StallFullOfUnwantedFish_NoMovesLeft_Loses()
        {
            var level = new LevelDefinition
            {
                StallSlots = 2,
                VisibleCustomers = 1,
                MaxRottenFish = 5,
                FreshnessMoves = 50,
                Boats = { L(Cipura, Cipura, Hamsi) },
                Customers = { L(Hamsi) }
            };
            var b = Started(level);
            b.TapBoatFish(0); b.TapBoatFish(0);
            Assert.AreEqual(GameResult.Lost, b.Result);
            Assert.AreEqual(LoseReason.NoMovesLeft, b.LoseReason);
        }

        [Test]
        public void MovesAreRejected_AfterGameEnds()
        {
            var level = SimpleLevel();
            level.AutoServe = true;
            var b = Started(level);
            b.TapBoatFish(0); b.TapBoatFish(0); b.TapBoatFish(0);
            Assert.AreEqual(GameResult.Won, b.Result);
            Assert.IsFalse(b.TapBoatFish(0));
            Assert.AreEqual(3, b.MoveCount);
        }

        // ---------------------------------------------------------------- Boosters

        [Test]
        public void ClearRotten_FreesSlot_KeepsRottenCount()
        {
            var level = SimpleLevel();
            level.FreshnessMoves = 1;
            level.MaxRottenFish = 3;
            var b = Started(level);
            b.TapBoatFish(2); // Levrek, rots immediately (1 move)
            Assert.IsTrue(b.Stall[0].IsRotten);
            Assert.IsTrue(b.ClearRotten(0));
            Assert.IsNull(b.Stall[0]);
            Assert.AreEqual(1, b.RottenCount);
        }
    }
}
