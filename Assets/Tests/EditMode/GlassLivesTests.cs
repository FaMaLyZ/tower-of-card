using NUnit.Framework;

public class GlassLivesTests
{
    [Test]
    public void StartsWhole()
    {
        GlassLives glass = new GlassLives(3);

        Assert.AreEqual(3, glass.LivesRemaining);
        Assert.IsFalse(glass.IsShattered);
    }

    [Test]
    public void CrackingToZeroShatters()
    {
        GlassLives glass = new GlassLives(3);

        glass.Crack();
        glass.Crack();
        Assert.IsFalse(glass.IsShattered);

        glass.Crack();
        Assert.IsTrue(glass.IsShattered);
        Assert.AreEqual(0, glass.LivesRemaining);
    }

    [Test]
    public void CannotCrackBelowZero()
    {
        GlassLives glass = new GlassLives(1);
        glass.Crack();

        Assert.IsFalse(glass.Crack());
        Assert.AreEqual(0, glass.LivesRemaining);
    }

    [Test]
    public void RepairNeverGoesAboveTheMaximum()
    {
        GlassLives glass = new GlassLives(3);

        Assert.IsFalse(glass.Repair());
        Assert.AreEqual(3, glass.LivesRemaining);

        glass.Crack();
        Assert.IsTrue(glass.Repair());
        Assert.IsFalse(glass.Repair());
        Assert.AreEqual(3, glass.LivesRemaining);
    }
}
