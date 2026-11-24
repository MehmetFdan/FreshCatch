using System;
using System.Collections.Generic;

namespace FreshCatch.Model
{
    [Serializable]
    public sealed class GeneratorSettings
    {
        public int FishTypes = 3;      // uses the first N FishType values
        public int Customers = 5;
        public int MinOrder = 2;
        public int MaxOrder = 3;
        public int BoatSize = 4;
        /// <summary>Fish nobody ordered: they must rot or be cleared, so they add pressure.</summary>
        public int ExtraFish;
        /// <summary>0 = boats arrive in exactly the order customers need; higher = more scrambled.</summary>
        public int Scramble = 4;
        public int Seed = 1;
    }

    /// <summary>Builds a consistent level (supply covers demand) from a few knobs.</summary>
    public static class LevelGenerator
    {
        public static int TypeCount => Enum.GetValues(typeof(FishType)).Length;

        /// <summary>Fills Boats and Customers of <paramref name="target"/>; other settings are kept.</summary>
        public static LevelDefinition Generate(GeneratorSettings s, LevelDefinition target = null)
        {
            var rng = new Random(s.Seed);
            var level = target ?? new LevelDefinition();
            level.Boats = new List<List<FishType>>();
            level.Customers = new List<List<FishType>>();

            var types = Math.Max(1, Math.Min(s.FishTypes, TypeCount));
            var minOrder = Math.Max(1, s.MinOrder);
            var maxOrder = Math.Max(minOrder, s.MaxOrder);

            var demandSequence = new List<FishType>();
            for (var c = 0; c < Math.Max(1, s.Customers); c++)
            {
                var size = rng.Next(minOrder, maxOrder + 1);
                var order = new List<FishType>();
                for (var i = 0; i < size; i++) order.Add((FishType)rng.Next(types));
                level.Customers.Add(order);
                demandSequence.AddRange(order);
            }

            for (var i = 0; i < s.ExtraFish; i++)
                demandSequence.Insert(rng.Next(demandSequence.Count + 1), (FishType)rng.Next(types));

            // Local scramble: each fish may move up to `Scramble` places, so boats loosely follow demand.
            var scramble = Math.Max(0, s.Scramble);
            for (var i = 0; i < demandSequence.Count; i++)
            {
                var j = Math.Min(demandSequence.Count - 1, i + rng.Next(scramble + 1));
                (demandSequence[i], demandSequence[j]) = (demandSequence[j], demandSequence[i]);
            }

            var boatSize = Math.Max(1, s.BoatSize);
            for (var i = 0; i < demandSequence.Count; i += boatSize)
                level.Boats.Add(demandSequence.GetRange(i, Math.Min(boatSize, demandSequence.Count - i)));

            return level;
        }

        /// <summary>
        /// Tries consecutive seeds until the greedy solver wins. Returns the seed used, or -1.
        /// </summary>
        public static int GenerateSolvable(GeneratorSettings s, LevelDefinition target, int attempts = 50)
        {
            var start = s.Seed;
            for (var i = 0; i < attempts; i++)
            {
                s.Seed = start + i;
                Generate(s, target);
                if (LevelSolver.Play(target, null, 0f).Won) return s.Seed;
            }
            s.Seed = start;
            return -1;
        }
    }
}
