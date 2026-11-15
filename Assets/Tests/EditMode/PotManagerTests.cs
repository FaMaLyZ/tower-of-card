using NUnit.Framework;

public class PotManagerTests
{
    static PotManager MakePot() => new PotManager(10, 10);     // 10% per break, 10% per level

    static ShakeResult Shake(int levels, params int[] brokenCardIds)
    {
        return new ShakeResult(1, brokenCardIds, levels, false, false);
    }

    static ShakeResult Collapse(params int[] brokenCardIds)
    {
        return new ShakeResult(1, brokenCardIds, 0, true, false);
    }

    [Test]
    public void WorkedExampleFromThePlan()
    {
        PotManager pot = MakePot();
        pot.AddHandScore(200);
        pot.AddHandScore(102);

        pot.ApplyShakeResult(Shake(2, 7, 8));                   // 2 breaks: floor(302 x 0.81)
        Assert.AreEqual(244L, pot.PotChips);
        Assert.AreEqual(292L, pot.QuoteHeightValue(2));
        Assert.AreEqual(351L, pot.QuoteBankPayout(2, 1.2m));    // not 350: rounded once, at the end

        pot.AddHandScore(52);                                   // Ride, then a Pair, no breaks
        pot.ApplyShakeResult(Shake(2));
        Assert.AreEqual(296L, pot.PotChips);                    // starts from 244, the height bonus was never stored
        Assert.AreEqual(426L, pot.QuoteBankPayout(2, 1.2m));
    }

    [Test]
    public void HeightBonusNeverCompounds()
    {
        PotManager pot = MakePot();
        pot.AddHandScore(100);

        pot.ApplyShakeResult(Shake(3));
        long firstQuote = pot.QuoteBankPayout(3, 1m);
        pot.ApplyShakeResult(Shake(3));
        long secondQuote = pot.QuoteBankPayout(3, 1m);

        Assert.AreEqual(130L, firstQuote);
        Assert.AreEqual(firstQuote, secondQuote);
        Assert.AreEqual(100L, pot.PotChips);
    }

    [Test]
    public void SameBrokenCardReportedTwice_CountsOnce()
    {
        PotManager pot = MakePot();
        pot.AddHandScore(100);

        pot.ApplyShakeResult(Shake(1, 5, 5));

        Assert.AreEqual(90L, pot.PotChips);
    }

    [Test]
    public void Collapse_LeavesNothing_WhateverTheBreakCount()
    {
        PotManager pot = MakePot();
        pot.AddHandScore(500);

        pot.ApplyShakeResult(Collapse(1, 2, 3));

        Assert.AreEqual(0L, pot.PotChips);
    }

    [Test]
    public void BankPot_ReturnsTheQuote_AndLeavesNothing()
    {
        PotManager pot = MakePot();
        pot.AddHandScore(244);
        long quote = pot.QuoteBankPayout(2, 1.2m);

        long payout = pot.BankPot(2, 1.2m);

        Assert.AreEqual(quote, payout);
        Assert.AreEqual(0L, pot.PotChips);
    }
}
