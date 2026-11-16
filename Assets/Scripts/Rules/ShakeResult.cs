using System.Collections.Generic;

/// What one shake did to the tower. There is exactly one of these per shake,
/// and the Pot is updated from it exactly once. After it is made, nothing may change the payout.
public class ShakeResult
{
    public int ShakeId { get; }
    public IReadOnlyList<int> BrokenCardIds { get; }
    public int StandingLevels { get; }
    public bool Collapsed { get; }
    public bool TimedOut { get; }

    public ShakeResult(int shakeId, IReadOnlyList<int> brokenCardIds, int standingLevels, bool collapsed, bool timedOut)
    {
        ShakeId = shakeId;
        BrokenCardIds = brokenCardIds;
        StandingLevels = standingLevels;
        Collapsed = collapsed;
        TimedOut = timedOut;
    }

    /// How many DIFFERENT cards broke. The same card reported twice counts once.
    public int BrokenCount => new HashSet<int>(BrokenCardIds).Count;
}
