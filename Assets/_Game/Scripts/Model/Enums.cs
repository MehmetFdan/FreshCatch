namespace FreshCatch.Model
{
    public enum FishType
    {
        Hamsi,
        Levrek,
        Cipura,
        Lufer,
        Palamut
    }

    /// <summary>How fish age: by real time, or one tick per player move.</summary>
    public enum FreshnessMode
    {
        TimeBased,
        MoveBased
    }

    public enum FreshnessStage
    {
        Fresh,
        Faded,
        Rotten
    }

    public enum GameResult
    {
        Playing,
        Won,
        Lost
    }

    public enum LoseReason
    {
        None,
        TooManyRotten,
        NoMovesLeft
    }
}
