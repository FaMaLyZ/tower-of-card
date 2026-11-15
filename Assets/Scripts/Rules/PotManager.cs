/// The Pot: points that are NOT safe yet (Master Plan 1.3).
///
///   hand score  -> added to the Pot
///   shake       -> each broken card cuts the Pot by the break rate; a collapse empties it
///   Bank        -> pays the Pot plus the height bonus, then the Pot is 0
///
/// The height bonus is worked out fresh for every quote. It is NEVER stored in the Pot.
/// A worker: it does the maths and returns the result. The conductor decides when.
public class PotManager
{
    readonly decimal breakRate;     // 0.10 = each broken card takes 10% off
    readonly decimal heightRate;    // 0.10 = each standing level adds 10% to a Bank

    /// The raw, unsafe chips. This is the only number a Ride carries to the next hand.
    public long PotChips { get; private set; }

    public PotManager(int breakPercent, int heightPercent)
    {
        breakRate = PercentMath.ToRate(breakPercent);
        heightRate = PercentMath.ToRate(heightPercent);
    }

    public void AddHandScore(long handScore)
    {
        if (handScore > 0) PotChips += handScore;
    }

    /// Called once per shake. A collapse beats the break penalties: the Pot is simply 0.
    public void ApplyShakeResult(ShakeResult shake)
    {
        if (shake.Collapsed)
        {
            PotChips = 0;
            return;
        }

        PotChips = PercentMath.FloorToChips(PotChips * GetShareKeptAfterBreaks(shake.BrokenCount));
    }

    // (1 - break rate) multiplied once per broken card: 2 breaks at 10% keep 0.81
    decimal GetShareKeptAfterBreaks(int brokenCount)
    {
        decimal kept = 1m;
        for (int i = 0; i < brokenCount; i++) kept *= 1m - breakRate;
        return kept;
    }

    /// Pot plus height bonus, before the reward multiplier. For the breakdown on screen only.
    public long QuoteHeightValue(int standingLevels)
    {
        return PercentMath.FloorToChips(PotChips * GetHeightFactor(standingLevels));
    }

    /// What a Bank would pay right now. Worked out from the raw Pot in one go, rounded once.
    public long QuoteBankPayout(int standingLevels, decimal rewardMultiplier)
    {
        return PercentMath.FloorToChips(PotChips * GetHeightFactor(standingLevels) * rewardMultiplier);
    }

    /// Bank: returns the payout and empties the Pot.
    public long BankPot(int standingLevels, decimal rewardMultiplier)
    {
        long payout = QuoteBankPayout(standingLevels, rewardMultiplier);
        PotChips = 0;
        return payout;
    }

    public void Clear()
    {
        PotChips = 0;
    }

    decimal GetHeightFactor(int standingLevels)
    {
        return 1m + heightRate * standingLevels;
    }
}
