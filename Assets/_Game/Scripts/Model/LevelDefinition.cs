using System.Collections.Generic;

namespace FreshCatch.Model
{
    /// <summary>
    /// Engine-free description of a level. The ScriptableObject LevelData converts into this,
    /// and the solver/tests build it directly.
    /// </summary>
    public sealed class LevelDefinition
    {
        public int StallSlots = 7;
        public int VisibleCustomers = 3;
        public int MaxRottenFish = 2;

        public FreshnessMode FreshnessMode = FreshnessMode.MoveBased;
        /// <summary>Seconds per fish (TimeBased).</summary>
        public float FreshnessSeconds = 20f;
        /// <summary>Moves per fish (MoveBased).</summary>
        public int FreshnessMoves = 8;

        /// <summary>When true, a fish dropped on the stall goes straight to a customer who wants it.</summary>
        public bool AutoServe;

        public List<List<FishType>> Boats = new List<List<FishType>>();
        public List<List<FishType>> Customers = new List<List<FishType>>();

        public float MaxFreshness => FreshnessMode == FreshnessMode.TimeBased ? FreshnessSeconds : FreshnessMoves;

        /// <summary>Returns a list of problems; empty means the level is consistent.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (StallSlots < 1) errors.Add("StallSlots must be >= 1.");
            if (VisibleCustomers < 1) errors.Add("VisibleCustomers must be >= 1.");
            if (Boats.Count == 0) errors.Add("Level has no boats.");
            if (Customers.Count == 0) errors.Add("Level has no customers.");

            var supply = new Dictionary<FishType, int>();
            var demand = new Dictionary<FishType, int>();
            foreach (var b in Boats) foreach (var t in b) supply[t] = supply.TryGetValue(t, out var n) ? n + 1 : 1;
            foreach (var c in Customers)
            {
                if (c.Count == 0) errors.Add("A customer has an empty order.");
                foreach (var t in c) demand[t] = demand.TryGetValue(t, out var n) ? n + 1 : 1;
            }

            foreach (var kv in demand)
            {
                supply.TryGetValue(kv.Key, out var have);
                if (have < kv.Value) errors.Add($"Not enough {kv.Key}: need {kv.Value}, boats bring {have}.");
            }
            return errors;
        }
    }
}
