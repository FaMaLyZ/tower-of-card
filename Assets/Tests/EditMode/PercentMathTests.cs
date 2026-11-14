using NUnit.Framework;

public class PercentMathTests
{
    [Test]
    public void ToRate_TurnsWholePercentIntoDecimalRate()
    {
        Assert.AreEqual(0.10m, PercentMath.ToRate(10));
        Assert.AreEqual(1.20m, PercentMath.ToRate(120));
    }

    [Test]
    public void FloorToChips_RoundsDown()
    {
        Assert.AreEqual(244L, PercentMath.FloorToChips(244.62m));
        Assert.AreEqual(351L, PercentMath.FloorToChips(351.36m));
    }

    [Test]
    public void FloorToChips_KeepsWholeNumbers()
    {
        Assert.AreEqual(300L, PercentMath.FloorToChips(300m));
    }
}
