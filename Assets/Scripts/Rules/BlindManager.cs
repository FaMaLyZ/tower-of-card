/// How a blind ended.
public enum BlindResult
{
    Won,
    Failed
}

/// The blind being played: its target, the hands left and the points that are already safe (Master Plan 2.4).
///
/// A4a adds Leave Table: once the target is met with hands left, the player may leave for a bonus.
/// A worker: it keeps the numbers and returns results. It never starts the next blind and never cracks the Glass.
public class BlindManager
{
    readonly int handsPerBlind;
    readonly int targetGrowthPercent;
    readonly int leaveBonusPercent;

    /// Safe Points: every banked payout of this blind. A collapse never touches them.
    public long BlindScore { get; private set; }

    /// The score this blind needs by the end.
    public long Target { get; private set; }

    /// Hands not yet spent. A hand is spent when Play Hand is accepted, exactly once.
    public int HandsLeft { get; private set; }

    public int HandsPerBlind => handsPerBlind;

    /// True once the player took Leave Table. The blind ends as a win and no more hands are played.
    public bool HasLeftTable { get; private set; }

    /// The leave bonus that was paid (0 if the player did not leave).
    public long LeaveBonusPaid { get; private set; }

    /// The hand being played now, counted from 1 (for the HUD).
    public int HandsUsed => handsPerBlind - HandsLeft;

    public BlindManager(int handsPerBlind, int targetGrowthPercent, int leaveBonusPercent)
    {
        this.handsPerBlind = handsPerBlind;
        this.targetGrowthPercent = targetGrowthPercent;
        this.leaveBonusPercent = leaveBonusPercent;
    }

    /// A new blind: Safe Points back to 0, all hands back, target worked out for this Ante.
    public void StartBlind(long baseTarget, int ante)
    {
        BlindScore = 0;
        HandsLeft = handsPerBlind;
        HasLeftTable = false;
        LeaveBonusPaid = 0;
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

    /// Leave Table is offered only when the target is met, a hand is still unplayed, and the player has not left yet.
    public bool CanLeaveTable()
    {
        return IsTargetReached() && HandsLeft > 0 && !HasLeftTable;
    }

    /// floor(target x leaveBonusRate x HandsLeft). No height or reward multiplier (Master Plan 1.2).
    public long GetLeaveTableBonus()
    {
        decimal rate = PercentMath.ToRate(leaveBonusPercent);
        return PercentMath.FloorToChips(Target * rate * HandsLeft);
    }

    /// Pays the leave bonus into Safe Points, once, and ends the blind early.
    /// Returns the bonus paid, or 0 (and changes nothing) if leaving is not allowed now.
    public long LeaveTable()
    {
        if (!CanLeaveTable()) return 0;

        LeaveBonusPaid = GetLeaveTableBonus();
        AddSafePoints(LeaveBonusPaid);
        HasLeftTable = true;
        return LeaveBonusPaid;
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
