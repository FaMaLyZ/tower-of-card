using NUnit.Framework;

public class BlindManagerTests
{
    static BlindManager MakeBlind() => new BlindManager(4, 150, 10);     // 4 hands, targets x1.5 per Ante, 10% leave bonus

    [Test]
    public void BlindScoreStartsAtZeroOnEveryStart()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);
        blind.AddSafePoints(90);

        blind.StartBlind(150, 1);

        Assert.AreEqual(0L, blind.BlindScore);
        Assert.AreEqual(4, blind.HandsLeft);
    }

    [Test]
    public void TargetGrowsFromTheBaseValue()
    {
        Assert.AreEqual(150L, BlindManager.CalculateTarget(150, 1, 150));
        Assert.AreEqual(225L, BlindManager.CalculateTarget(150, 2, 150));
        Assert.AreEqual(337L, BlindManager.CalculateTarget(150, 3, 150));   // 337.5, rounded once
        Assert.AreEqual(506L, BlindManager.CalculateTarget(150, 4, 150));   // 506.25, not floor(337 x 1.5) = 505
    }

    [Test]
    public void TargetReachedFlipsExactlyAtTheTarget()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);

        blind.AddSafePoints(149);
        Assert.IsFalse(blind.IsTargetReached());
        Assert.AreEqual(BlindResult.Failed, blind.GetResult());

        blind.AddSafePoints(1);
        Assert.IsTrue(blind.IsTargetReached());
        Assert.AreEqual(BlindResult.Won, blind.GetResult());
    }

    [Test]
    public void HandsAreSpentOnceEachAndNeverBelowZero()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);

        for (int i = 0; i < 4; i++)
            Assert.IsTrue(blind.UseOneHand());

        Assert.IsFalse(blind.UseOneHand());
        Assert.AreEqual(0, blind.HandsLeft);
        Assert.AreEqual(4, blind.HandsUsed);
    }

    [Test]
    public void NegativeOrZeroPointsAreIgnored()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);

        blind.AddSafePoints(0);
        blind.AddSafePoints(-50);

        Assert.AreEqual(0L, blind.BlindScore);
    }

    // ---------- Leave Table (A4a) ----------

    [Test]
    public void LeaveBonusIsTargetTimesRateTimesHandsLeft()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);
        blind.UseOneHand();                                     // 3 hands left

        Assert.AreEqual(45L, blind.GetLeaveTableBonus());       // 150 x 0.10 x 3
    }

    [Test]
    public void LeaveBonusRoundsDownOnceAtTheEnd()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 3);                               // target 337
        blind.UseOneHand();                                     // 3 hands left

        Assert.AreEqual(101L, blind.GetLeaveTableBonus());      // 337 x 0.10 x 3 = 101.1
    }

    [Test]
    public void LeaveIsNotAllowedBeforeTheTarget()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);
        blind.UseOneHand();
        blind.AddSafePoints(149);

        Assert.IsFalse(blind.CanLeaveTable());
        Assert.AreEqual(0L, blind.LeaveTable());
        Assert.AreEqual(149L, blind.BlindScore);
        Assert.IsFalse(blind.HasLeftTable);
    }

    [Test]
    public void LeaveIsNotAllowedWithNoHandsLeft()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);
        for (int i = 0; i < 4; i++) blind.UseOneHand();
        blind.AddSafePoints(200);

        Assert.AreEqual(0L, blind.GetLeaveTableBonus());
        Assert.IsFalse(blind.CanLeaveTable());
        Assert.AreEqual(0L, blind.LeaveTable());
        Assert.AreEqual(200L, blind.BlindScore);
    }

    [Test]
    public void LeaveAddsTheBonusOnceAndWinsTheBlind()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);
        blind.UseOneHand();                                     // 3 hands left
        blind.AddSafePoints(160);

        Assert.AreEqual(45L, blind.LeaveTable());
        Assert.AreEqual(0L, blind.LeaveTable());                // a second click pays nothing

        Assert.AreEqual(205L, blind.BlindScore);
        Assert.AreEqual(45L, blind.LeaveBonusPaid);
        Assert.IsTrue(blind.HasLeftTable);
        Assert.AreEqual(BlindResult.Won, blind.GetResult());
    }

    [Test]
    public void ANewBlindForgetsTheLastLeave()
    {
        BlindManager blind = MakeBlind();
        blind.StartBlind(150, 1);
        blind.UseOneHand();
        blind.AddSafePoints(160);
        blind.LeaveTable();

        blind.StartBlind(150, 1);

        Assert.IsFalse(blind.HasLeftTable);
        Assert.AreEqual(0L, blind.LeaveBonusPaid);
    }
}
