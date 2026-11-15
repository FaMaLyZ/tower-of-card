/// How a blind ended.
public enum BlindResult
{
    Won,
    Failed
}

/// The blind being played: its target, the hands left and the points that are already safe (Master Plan 2.4).
///
/// A3c: one blind with a fixed target. A4 adds the leave bonus and the blind menu around it.
/// A worker: it keeps the numbers and returns results. It never starts the next blind and never cracks the Glass.
public class BlindManager
{
    readonly int handsPerBlind;
    readonly int targetGrowthPercent;

    /// Safe Points: every banked payout of this blind. A collapse never touches them.
    public long BlindScore { get; private set; }

    /// The score this blind needs by the end.
    public long Target { get; private set; }

    /// Hands not yet spent. A hand is spent when Play Hand is accepted, exactly once.
    public int HandsLeft { get; private set; }

    public int HandsPerBlind => handsPerBlind;

    /// The hand being played now, counted from 1 (for the HUD).
    public int HandsUsed => handsPerBlind - HandsLeft;

    public BlindManager(int handsPerBlind, int targetGrowthPercent)
    {
        this.handsPerBlind = handsPerBlind;
        this.targetGrowthPercent = targetGrowthPercent;
    }

    /// A new blind: Safe Points back to 0, all hands back, target worked out for this Ante.
    public void StartBlind(long baseTarget, int ante)
    {
        BlindScore = 0;
        HandsLeft = handsPerBlind;
        Target = CalculateTarget(baseTarget, ante, targetGrowthPercent);
    }

    /// Spends one hand. Returns false (and spends nothing) if none are left.
    public bool UseOneHand()
    {
        if (HandsLeft <= 0) return false;
        HandsLeft--;
        return true;
    }

    public void AddSafePoints(long points)
    {
        if (points > 0) BlindScore += points;
    }

    public bool IsTargetReached()
    {
        return BlindScore >= Target;
    }

    /// Judged on Safe Points only. The Pot never counts.
    public BlindResult GetResult()
    {
        return IsTargetReached() ? BlindResult.Won : BlindResult.Failed;
    }

    /// floor(base x growth^(ante - 1)), always from the base value, rounded once (Master Plan 1.3).
    public static long CalculateTarget(long baseTarget, int ante, int growthPercent)
    {
        decimal growth = PercentMath.ToRate(growthPercent);
        decimal target = baseTarget;
        for (int i = 1; i < ante; i++)
            target *= growth;
        return PercentMath.FloorToChips(target);
    }
}
