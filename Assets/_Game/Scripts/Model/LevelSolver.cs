using System;
using System.Collections.Generic;

namespace FreshCatch.Model
{
    public sealed class PlayResult
    {
        public bool Won;
        public int Moves;
        public int Rotten;
        public int CustomersServed;
        public LoseReason Reason;
    }

    public sealed class LevelAnalysis
    {
        /// <summary>One run with a sensible human-like strategy. If this loses, the level is probably too hard.</summary>
        public PlayResult Greedy;
        public int Playouts;
        public int Wins;
        public float WinRate => Playouts == 0 ? 0f : (float)Wins / Playouts;
        public float AvgMovesOnWin;
        public readonly Dictionary<LoseReason, int> Losses = new Dictionary<LoseReason, int>();

        /// <summary>Turkish label for the editor: Kolay / Orta / Zor / Çok Zor / Çözülemedi.</summary>
        public string Difficulty =>
            !Greedy.Won ? "Çözülemedi" :
            WinRate >= 0.8f ? "Kolay" :
            WinRate >= 0.5f ? "Orta" :
            WinRate >= 0.25f ? "Zor" : "Çok Zor";
    }

    /// <summary>
    /// Plays levels automatically. "Greedy" approximates a careful player; "noisy" playouts
    /// make random mistakes (epsilon) to approximate a casual player. The noisy win rate is
    /// the difficulty signal.
    /// </summary>
    public static class LevelSolver
    {
        public const int MaxMoves = 2000;

        public static LevelAnalysis Analyze(LevelDefinition level, int playouts = 200, float epsilon = 0.3f, int seed = 12345,
            float secondsPerMove = 1f, bool knowsFuture = false)
        {
            var a = new LevelAnalysis { Greedy = Play(level, null, 0f, secondsPerMove, knowsFuture), Playouts = playouts };
            var rng = new Random(seed);
            long winMoves = 0;
            for (var i = 0; i < playouts; i++)
            {
                var r = Play(level, rng, epsilon, secondsPerMove, knowsFuture);
                if (r.Won) { a.Wins++; winMoves += r.Moves; }
                else a.Losses[r.Reason] = a.Losses.TryGetValue(r.Reason, out var n) ? n + 1 : 1;
            }
            a.AvgMovesOnWin = a.Wins == 0 ? 0f : (float)winMoves / a.Wins;
            return a;
        }

        /// <summary>
        /// Plays one game. rng == null means fully greedy.
        /// knowsFuture = false (default) limits the player to what is on screen: the visible customers,
        /// the docked boat and the stall. true also lets it read the hidden customer queue (cheating).
        /// </summary>
        public static PlayResult Play(LevelDefinition level, Random rng, float epsilon, float secondsPerMove = 1f, bool knowsFuture = false)
        {
            var b = new BoardState(level);
            b.Start();
            while (b.Result == GameResult.Playing && b.MoveCount < MaxMoves)
            {
                var moved = rng != null && rng.NextDouble() < epsilon ? RandomMove(b, rng) : GreedyMove(b, knowsFuture);
                if (!moved) break;
                if (level.FreshnessMode == FreshnessMode.TimeBased) b.Tick(secondsPerMove);
            }
            return new PlayResult
            {
                Won = b.Result == GameResult.Won,
                Moves = b.MoveCount,
                Rotten = b.RottenCount,
                CustomersServed = b.CustomersServed,
                Reason = b.Result == GameResult.Won ? LoseReason.None :
                         b.LoseReason == LoseReason.None ? LoseReason.NoMovesLeft : b.LoseReason
            };
        }

        public static bool GreedyMove(BoardState b, bool knowsFuture = false)
        {
            // 1) Serve from the stall, most faded fish first.
            var bestSlot = -1;
            var bestFresh = float.MaxValue;
            for (var i = 0; i < b.Stall.Capacity; i++)
            {
                if (!b.CanTapStallFish(i)) continue;
                if (b.Stall[i].Freshness < bestFresh) { bestFresh = b.Stall[i].Freshness; bestSlot = i; }
            }
            if (bestSlot >= 0) return b.TapStallFish(bestSlot);

            // 2) Take the boat fish that is needed soonest.
            if (b.CurrentBoat == null) return false;

            var activeNeed = new Dictionary<FishType, int>();
            foreach (var c in b.ActiveCustomers)
                foreach (var t in c.Remaining) activeNeed[t] = activeNeed.TryGetValue(t, out var n) ? n + 1 : 1;
            var onStall = new Dictionary<FishType, int>();
            foreach (var f in b.Stall.Fish)
                if (!f.IsRotten) onStall[f.Type] = onStall.TryGetValue(f.Type, out var n) ? n + 1 : 1;

            var firstUpcoming = new Dictionary<FishType, int>();
            if (knowsFuture)
            {
                var idx = 0;
                foreach (var order in b.UpcomingCustomers)
                {
                    foreach (var t in order) if (!firstUpcoming.ContainsKey(t)) firstUpcoming[t] = idx;
                    idx++;
                }
            }

            var bestIndex = -1;
            var bestScore = int.MinValue;
            for (var i = 0; i < b.CurrentBoat.Fish.Count; i++)
            {
                if (!b.CanTapBoatFish(i)) continue;
                var t = b.CurrentBoat.Fish[i].Type;
                activeNeed.TryGetValue(t, out var need);
                onStall.TryGetValue(t, out var have);
                int score;
                if (need > have) score = 1000;
                else if (knowsFuture && firstUpcoming.TryGetValue(t, out var when)) score = 500 - when * 10;
                // Blind guess: avoid stockpiling a type the stall already holds.
                else score = 100 - have * 10;
                if (score > bestScore) { bestScore = score; bestIndex = i; }
            }
            return bestIndex >= 0 && b.TapBoatFish(bestIndex);
        }

        public static bool RandomMove(BoardState b, Random rng)
        {
            var moves = new List<int>(); // >= 0: boat index, < 0: -(slot + 1)
            for (var i = 0; i < b.Stall.Capacity; i++) if (b.CanTapStallFish(i)) moves.Add(-(i + 1));
            if (b.CurrentBoat != null)
                for (var i = 0; i < b.CurrentBoat.Fish.Count; i++) if (b.CanTapBoatFish(i)) moves.Add(i);
            if (moves.Count == 0) return false;
            var m = moves[rng.Next(moves.Count)];
            return m >= 0 ? b.TapBoatFish(m) : b.TapStallFish(-m - 1);
        }
    }
}
