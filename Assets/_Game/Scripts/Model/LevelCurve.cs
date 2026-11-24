using System;
using System.Collections.Generic;

namespace FreshCatch.Model
{
    public sealed class CurveLevel
    {
        public int Number;              // 1-based
        public LevelDefinition Level;
        public LevelAnalysis Analysis;
        public float TargetWinRate;
        public int Seed;
        public bool Handmade;
    }

    /// <summary>
    /// Builds a level sequence with a rising difficulty curve. The first levels are the hand-made
    /// tutorials; the rest are generated: knobs tighten with progress, and for each level the
    /// seed whose "noisy player" win rate is closest to the target is picked.
    /// Every chosen level is winnable by the greedy (careful) player.
    /// </summary>
    public static class LevelCurve
    {
        public const int TutorialLevels = 3;

        /// <summary>Target noisy win rate: ~0.9 early, ~0.45 at the end, with a small dip every 5th level.</summary>
        public static float TargetWinRate(int number, int total)
        {
            var t = total <= 1 ? 0f : (number - 1f) / (total - 1f);
            var target = 0.92f - 0.47f * t;
            if (number % 5 == 0) target -= 0.08f;   // "boss" level
            if (number % 5 == 1 && number > 1) target += 0.06f; // breather after the boss
            return Math.Max(0.3f, Math.Min(0.95f, target));
        }

        /// <summary>Level shape for a given progress t (0..1).</summary>
        public static (LevelDefinition shape, GeneratorSettings gen) Knobs(float t, int number)
        {
            int Lerp(int a, int b) => (int)Math.Round(a + (b - a) * t);

            var shape = new LevelDefinition
            {
                StallSlots = Lerp(6, 5),
                VisibleCustomers = t < 0.6f ? 3 : 2,
                MaxRottenFish = t < 0.25f ? 2 : t < 0.75f ? 1 : 0,
                FreshnessMode = FreshnessMode.MoveBased,
                FreshnessMoves = Lerp(11, 8),
            };
            var gen = new GeneratorSettings
            {
                FishTypes = Lerp(3, 5),
                Customers = Lerp(5, 10),
                MinOrder = 2,
                MaxOrder = t < 0.3f ? 3 : 4,
                BoatSize = Lerp(4, 6),
                ExtraFish = t < 0.5f ? 0 : t < 0.8f ? 1 : 2,
                Scramble = Lerp(4, 9),
                Seed = number * 1000
            };
            return (shape, gen);
        }

        public static List<CurveLevel> Build(int total = 20, int seedsPerLevel = 150, int searchPlayouts = 80, int finalPlayouts = 200,
            Action<int, int> progress = null)
        {
            var result = new List<CurveLevel>();
            var tutorials = BuiltInLevels.All();

            for (var n = 1; n <= total; n++)
            {
                progress?.Invoke(n, total);
                var target = TargetWinRate(n, total);

                if (n <= TutorialLevels && n <= tutorials.Count)
                {
                    var tut = tutorials[n - 1];
                    result.Add(new CurveLevel { Number = n, Level = tut, Analysis = LevelSolver.Analyze(tut, finalPlayouts), TargetWinRate = target, Handmade = true });
                    continue;
                }

                var t = (n - TutorialLevels - 1f) / Math.Max(1f, total - TutorialLevels - 1f);
                var (shape, gen) = Knobs(t, n);

                LevelDefinition best = null;
                var bestSeed = -1;
                var bestDist = float.MaxValue;
                for (var i = 0; i < seedsPerLevel; i++)
                {
                    gen.Seed = n * 1000 + i;
                    var candidate = Clone(shape);
                    LevelGenerator.Generate(gen, candidate);
                    if (!LevelSolver.Play(candidate, null, 0f).Won) continue;

                    var rate = LevelSolver.Analyze(candidate, searchPlayouts, 0.3f, gen.Seed).WinRate;
                    var dist = Math.Abs(rate - target);
                    if (dist >= bestDist) continue;
                    bestDist = dist;
                    best = candidate;
                    bestSeed = gen.Seed;
                    if (dist < 0.03f) break;
                }

                if (best == null) throw new InvalidOperationException($"Level {n}: no winnable seed in {seedsPerLevel} tries.");
                result.Add(new CurveLevel
                {
                    Number = n, Level = best, Seed = bestSeed, TargetWinRate = target,
                    Analysis = LevelSolver.Analyze(best, finalPlayouts)
                });
            }
            return result;
        }

        private static LevelDefinition Clone(LevelDefinition s) => new LevelDefinition
        {
            StallSlots = s.StallSlots,
            VisibleCustomers = s.VisibleCustomers,
            MaxRottenFish = s.MaxRottenFish,
            FreshnessMode = s.FreshnessMode,
            FreshnessSeconds = s.FreshnessSeconds,
            FreshnessMoves = s.FreshnessMoves,
            AutoServe = s.AutoServe
        };
    }
}
