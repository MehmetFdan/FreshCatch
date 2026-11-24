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
                result.Add(Search(n, shape, gen, target, seedsPerLevel, searchPlayouts, finalPlayouts));
            }
            return result;
        }

        // ------------------------------------------------------------------ chapters (21+)

        public const int ChapterSize = 10;

        /// <summary>
        /// Late-game target: a sawtooth per 10-level chapter (breather → ramp → boss on the 10th),
        /// drifting slightly harder each chapter.
        /// </summary>
        public static float ChapterTargetWinRate(int number)
        {
            var chapter = (number - 1) / ChapterSize;          // level 21 -> chapter 2
            var pos = (number - 1) % ChapterSize;              // 0..9
            var drift = Math.Min(0.10f, (chapter - 2) * 0.012f);
            float target;
            if (pos == 0) target = 0.78f;                      // breather
            else if (pos == ChapterSize - 1) target = 0.33f;   // boss
            else target = 0.70f - (pos - 1) * 0.035f;          // 0.70 → 0.455
            if (pos == 4) target += 0.08f;                     // mid-chapter rest
            return Math.Max(0.25f, Math.Min(0.9f, target - drift));
        }

        /// <summary>
        /// Late-game shape. Each chapter has a theme so levels feel different, not just harder:
        /// 0 = tight stall, 1 = many types, 2 = big orders, 3 = fast rot, 4 = long queues.
        /// </summary>
        public static (LevelDefinition shape, GeneratorSettings gen) ChapterKnobs(int number)
        {
            var chapter = (number - 1) / ChapterSize;
            var pos = (number - 1) % ChapterSize;
            var theme = (chapter - 2) % 5;
            var p = pos / (ChapterSize - 1f);                  // 0..1 inside the chapter
            int Lerp(int a, int b) => (int)Math.Round(a + (b - a) * p);

            var shape = new LevelDefinition
            {
                StallSlots = 6,
                VisibleCustomers = 3,
                MaxRottenFish = 1,
                FreshnessMode = FreshnessMode.MoveBased,
                FreshnessMoves = Lerp(11, 9),
            };
            var gen = new GeneratorSettings
            {
                FishTypes = 4,
                Customers = Lerp(7, 11),
                MinOrder = 2,
                MaxOrder = 4,
                BoatSize = 5,
                ExtraFish = pos >= 6 ? 1 : 0,
                Scramble = Lerp(5, 9),
                Seed = number * 1000
            };

            switch (theme)
            {
                case 0: // tight stall
                    shape.StallSlots = Lerp(5, 4);
                    shape.VisibleCustomers = 2;
                    gen.FishTypes = 3;
                    gen.MaxOrder = 3;
                    break;
                case 1: // many types
                    gen.FishTypes = 5;
                    shape.StallSlots = 6;
                    gen.BoatSize = 6;
                    break;
                case 2: // big orders
                    gen.MinOrder = 3;
                    gen.MaxOrder = 5;
                    gen.Customers = Lerp(6, 9);
                    shape.StallSlots = 6;
                    shape.FreshnessMoves = Lerp(10, 9);
                    break;
                case 3: // fast rot
                    shape.FreshnessMoves = Lerp(9, 6);
                    shape.MaxRottenFish = 2;
                    gen.ExtraFish = Lerp(1, 3);
                    break;
                default: // long queues
                    gen.Customers = Lerp(10, 15);
                    shape.VisibleCustomers = 2;
                    gen.BoatSize = 6;
                    break;
            }

            if (pos == 0) // breather: a bit more room
            {
                shape.StallSlots += 1;
                shape.FreshnessMoves += 2;
            }
            return (shape, gen);
        }

        /// <summary>Generates levels <paramref name="from"/>..<paramref name="to"/> (inclusive) with the chapter curve.</summary>
        public static List<CurveLevel> BuildChapters(int from, int to, int seedsPerLevel = 150, int searchPlayouts = 80, int finalPlayouts = 200,
            Action<int, int> progress = null)
        {
            var result = new List<CurveLevel>();
            for (var n = from; n <= to; n++)
            {
                progress?.Invoke(n - from + 1, to - from + 1);
                var (shape, gen) = ChapterKnobs(n);
                result.Add(Search(n, shape, gen, ChapterTargetWinRate(n), seedsPerLevel, searchPlayouts, finalPlayouts));
            }
            return result;
        }

        /// <summary>Tries seeds and keeps the winnable level whose noisy win rate is closest to the target.</summary>
        private static CurveLevel Search(int n, LevelDefinition shape, GeneratorSettings gen, float target,
            int seedsPerLevel, int searchPlayouts, int finalPlayouts)
        {
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
            return new CurveLevel
            {
                Number = n, Level = best, Seed = bestSeed, TargetWinRate = target,
                Analysis = LevelSolver.Analyze(best, finalPlayouts)
            };
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
