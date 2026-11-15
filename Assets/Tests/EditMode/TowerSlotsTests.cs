using System.Collections.Generic;
using NUnit.Framework;

public class TowerSlotsTests
{
    const int Left = TowerSlots.Left, Right = TowerSlots.Right, Floor = TowerSlots.Floor;

    static TowerSlots MakeTower(int maxBaseTents, params (int, int, int)[] cards)
    {
        return new TowerSlots(cards, maxBaseTents);
    }

    [Test]
    public void EmptyTable_HasOneStartingSlot()
    {
        TowerSlots tower = MakeTower(4);

        List<(int kind, int level, int index)> open = tower.FindOpenSlots();

        Assert.AreEqual(1, open.Count);
        Assert.AreEqual((Left, 0, 0), open[0]);
    }

    [Test]
    public void EmptyTable_FitsAFullHandOfFive()
    {
        Assert.IsTrue(MakeTower(4).CanFit(5));
    }

    [Test]
    public void OneOpenSlotThatUnlocksMore_AcceptsFourCards()
    {
        // Two tents allowed; the second tent is missing its right half. Only that slot is open.
        TowerSlots tower = MakeTower(2, (Left, 0, 0), (Right, 0, 0), (Left, 0, 1));

        Assert.AreEqual(1, tower.FindOpenSlots().Count);
        Assert.IsTrue(tower.CanFit(4));      // right half, floor, then a tent on the floor
    }

    [Test]
    public void MoreCardsThanTheTowerCanEverHold_IsRefused()
    {
        TowerSlots tower = MakeTower(2, (Left, 0, 0), (Right, 0, 0), (Left, 0, 1));

        Assert.IsFalse(tower.CanFit(5));     // only 4 spots are left in a 2-tent tower
    }

    [Test]
    public void CanFit_LeavesTheTowerUnchanged()
    {
        TowerSlots tower = MakeTower(4, (Left, 0, 0));

        tower.CanFit(5);

        Assert.AreEqual(1, tower.CardCount);
    }

    [Test]
    public void FloorNeedsTwoCompleteTents()
    {
        TowerSlots oneTent = MakeTower(4, (Left, 0, 0), (Right, 0, 0));
        TowerSlots twoTents = MakeTower(4, (Left, 0, 0), (Right, 0, 0), (Left, 0, 1), (Right, 0, 1));

        Assert.IsFalse(oneTent.FindOpenSlots().Contains((Floor, 0, 0)));
        Assert.IsTrue(twoTents.FindOpenSlots().Contains((Floor, 0, 0)));
    }

    [Test]
    public void EmptyTable_HasNoBaseCard()
    {
        Assert.IsFalse(MakeTower(4).HasBaseCard());
    }

    [Test]
    public void LoneBaseCard_StopsACollapse_ButGivesNoLevel()
    {
        TowerSlots tower = MakeTower(4, (Left, 0, 0));

        Assert.IsTrue(tower.HasBaseCard());
        Assert.AreEqual(0, tower.CountStandingLevels());
    }

    [Test]
    public void TwoTentsAFloorAndATent_AreTwoLevels()
    {
        TowerSlots tower = MakeTower(4,
            (Left, 0, 0), (Right, 0, 0), (Left, 0, 1), (Right, 0, 1),
            (Floor, 0, 0),
            (Left, 1, 0), (Right, 1, 0));

        Assert.AreEqual(2, tower.CountStandingLevels());
        Assert.AreEqual(0, tower.FindUnsupportedCards().Count);
    }

    [Test]
    public void FloorCardAlone_AddsNoLevel()
    {
        TowerSlots tower = MakeTower(4,
            (Left, 0, 0), (Right, 0, 0), (Left, 0, 1), (Right, 0, 1),
            (Floor, 0, 0));

        Assert.AreEqual(1, tower.CountStandingLevels());
    }

    [Test]
    public void BrokenCardUnderAFloor_LeavesEverythingAboveUnsupported()
    {
        // the right half of the second tent broke: the floor and the tent on it have lost their support
        TowerSlots tower = MakeTower(4,
            (Left, 0, 0), (Right, 0, 0), (Left, 0, 1),
            (Floor, 0, 0),
            (Left, 1, 0), (Right, 1, 0));

        List<(int kind, int level, int index)> unsupported = tower.FindUnsupportedCards();

        Assert.AreEqual(3, unsupported.Count);
        Assert.IsTrue(unsupported.Contains((Floor, 0, 0)));
        Assert.IsTrue(unsupported.Contains((Left, 1, 0)));
        Assert.IsTrue(unsupported.Contains((Right, 1, 0)));
        Assert.AreEqual(1, tower.CountStandingLevels());        // only the first tent still counts
    }

    [Test]
    public void FullTower_HasNoOpenSlot()
    {
        TowerSlots tower = MakeTower(4);

        while (tower.HasOpenSlot()) tower.Place(tower.FindOpenSlots()[0]);

        Assert.AreEqual(26, tower.CardCount);    // 4 + 3 + 2 + 1 tents (20 cards) and 3 + 2 + 1 floors
        Assert.IsFalse(tower.CanFit(1));
    }
}
