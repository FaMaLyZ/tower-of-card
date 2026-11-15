using NUnit.Framework;

public class BlindManagerTests
{
    static BlindManager MakeBlind() => new BlindManager(4, 150);     // 4 hands, targets x1.5 per Ante

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
}
