using System.Collections.Generic;
using FreshCatch.Data;
using FreshCatch.Model;
using NUnit.Framework;
using UnityEditor;
using static FreshCatch.Model.FishType;

namespace FreshCatch.Tests
{
    /// <summary>Guards the shipped level assets: every level must be fair to a player who only sees the screen.</summary>
    public class LevelDatabaseTests
    {
        private const string DatabasePath = "Assets/_Game/Levels/LevelDatabase.asset";

        private static LevelDatabase Load()
        {
            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            Assert.IsNotNull(db, $"Missing {DatabasePath}");
            return db;
        }

        [Test]
        public void EveryLevel_IsValid_AndWinnableWithVisibleInfoOnly()
        {
            var db = Load();
            var failures = new List<string>();
            for (var i = 0; i < db.levels.Count; i++)
            {
                Assert.IsNotNull(db.levels[i], $"Level {i + 1} asset is missing");
                var def = db.levels[i].ToDefinition();
                foreach (var e in def.Validate()) failures.Add($"Level {i + 1}: {e}");
                var r = LevelSolver.Play(def, null, 0f, 1f, knowsFuture: false);
                if (!r.Won) failures.Add($"Level {i + 1}: visible-info player loses ({r.Reason}, {r.CustomersServed}/{def.Customers.Count} customers)");
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void VisibleSolver_DoesNotReadHiddenQueue()
        {
            // Visible customer wants Hamsi. The boat has Levrek (only a hidden customer wants it) and Cipura (nobody).
            // A player who can't see the queue has no reason to prefer Levrek; the cheating solver does.
            var level = new LevelDefinition
            {
                StallSlots = 4, VisibleCustomers = 1, MaxRottenFish = 5, FreshnessMoves = 50,
                Boats = { L(Cipura, Levrek), L(Hamsi) },
                Customers = { L(Hamsi), L(Levrek) }
            };
            var omni = new BoardState(level); omni.Start();
            LevelSolver.GreedyMove(omni, knowsFuture: true);
            Assert.AreEqual(Levrek, omni.Stall[0].Type, "Omniscient solver should take the fish a hidden customer wants.");

            var fair = new BoardState(level); fair.Start();
            LevelSolver.GreedyMove(fair, knowsFuture: false);
            Assert.AreEqual(Cipura, fair.Stall[0].Type, "Visible-only solver must not use the hidden queue (falls back to first fish).");
        }

        private static List<FishType> L(params FishType[] t) => new List<FishType>(t);
    }
}
