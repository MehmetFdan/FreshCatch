namespace FreshCatch.Model
{
    /// <summary>
    /// A single fish. Freshness counts down from <see cref="MaxFreshness"/> to 0.
    /// Units are seconds (TimeBased) or moves (MoveBased); the fish doesn't care which.
    /// </summary>
    public sealed class Fish
    {
        /// <summary>Above this fraction of max freshness the fish counts as Fresh.</summary>
        public const float FadedThreshold = 0.5f;

        private static int _nextId;

        public int Id { get; }
        public FishType Type { get; }
        public float MaxFreshness { get; }
        public float Freshness { get; private set; }

        public Fish(FishType type, float maxFreshness)
        {
            Id = ++_nextId;
            Type = type;
            MaxFreshness = maxFreshness > 0 ? maxFreshness : 1f;
            Freshness = MaxFreshness;
        }

        public float Normalized => Freshness / MaxFreshness;

        public FreshnessStage Stage =>
            Freshness <= 0f ? FreshnessStage.Rotten :
            Normalized > FadedThreshold ? FreshnessStage.Fresh :
            FreshnessStage.Faded;

        public bool IsRotten => Stage == FreshnessStage.Rotten;

        /// <summary>Ages the fish. Returns true if this call made it rot.</summary>
        public bool Age(float amount)
        {
            if (IsRotten || amount <= 0f) return false;
            Freshness -= amount;
            if (Freshness < 0f) Freshness = 0f;
            return IsRotten;
        }

        /// <summary>Restores freshness (ice booster). Rotten fish can't be saved.</summary>
        public void Refresh(float amount)
        {
            if (IsRotten) return;
            Freshness += amount;
            if (Freshness > MaxFreshness) Freshness = MaxFreshness;
        }

        public override string ToString() => $"{Type}#{Id}({Freshness:0.#}/{MaxFreshness:0.#})";
    }
}
