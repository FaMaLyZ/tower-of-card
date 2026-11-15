using System.Collections.Generic;

/// The slot rules of the house of cards: which spots are open, and whether a number of cards
/// can all be placed (Master Plan 3.2). Logic only: no Unity objects, no physics, so it can be tested.
///
/// A slot is named by (kind, level, index):
///   kind  = Left or Right half of a tent, or a Floor card lying across two tents
///   level = 0 on the table, 1 on the first floor, and so on
///   index = position along the row, counted from the first tent
public class TowerSlots
{
    public const int Left = 0, Right = 1, Floor = 2;

    readonly HashSet<(int kind, int level, int index)> occupied = new HashSet<(int kind, int level, int index)>();
    readonly int maxBaseTents;

    public TowerSlots(IEnumerable<(int kind, int level, int index)> occupiedSlots, int maxBaseTents)
    {
        foreach (var slot in occupiedSlots) occupied.Add(slot);
        this.maxBaseTents = maxBaseTents;
    }

    public int CardCount => occupied.Count;

    public void Place((int kind, int level, int index) slot) => occupied.Add(slot);

    public void Remove((int kind, int level, int index) slot) => occupied.Remove(slot);

    public bool HasOpenSlot() => FindOpenSlots().Count > 0;

    /// True if SOME order of placements fits all the cards. A placed card can open new slots
    /// (the other half of a tent, a floor, a new level), so this is not "open slots >= cards".
    public bool CanFit(int cardCount)
    {
        if (cardCount <= 0) return true;

        foreach (var slot in FindOpenSlots())
        {
            occupied.Add(slot);                         // try this slot...
            bool restFits = CanFit(cardCount - 1);
            occupied.Remove(slot);                      // ...and put the tower back as it was
            if (restFits) return true;
        }
        return false;
    }

    /// Every slot a card may be placed in right now.
    public List<(int kind, int level, int index)> FindOpenSlots()
    {
        var open = new List<(int kind, int level, int index)>();
        var seen = new HashSet<(int kind, int level, int index)>();

        if (occupied.Count == 0)
        {
            AddIfOpen(open, seen, Left, 0, 0);          // an empty table: the tower starts here
            return open;
        }

        HashSet<int> baseTents = FindBaseTents();
        foreach (var card in occupied)
        {
            if (card.kind == Floor) AddSlotsAboveFloor(open, seen, card.level, card.index);
            else AddSlotsAroundTentCard(open, seen, baseTents, card);
        }
        return open;
    }

    // Which tents on the table have at least one card
    HashSet<int> FindBaseTents()
    {
        var baseTents = new HashSet<int>();
        foreach (var card in occupied)
            if (card.kind != Floor && card.level == 0) baseTents.Add(card.index);
        return baseTents;
    }

    // A floor card lets you build a tent on top of it
    void AddSlotsAboveFloor(List<(int, int, int)> open, HashSet<(int, int, int)> seen, int level, int index)
    {
        AddIfOpen(open, seen, Left, level + 1, index);
        AddIfOpen(open, seen, Right, level + 1, index);
    }

    void AddSlotsAroundTentCard(List<(int, int, int)> open, HashSet<(int, int, int)> seen,
                                HashSet<int> baseTents, (int kind, int level, int index) card)
    {
        // finish this tent
        AddIfOpen(open, seen, card.kind == Left ? Right : Left, card.level, card.index);

        // a flat card across two complete neighbouring tents
        if (IsTentComplete(card.level, card.index))
        {
            if (IsTentComplete(card.level, card.index + 1)) AddIfOpen(open, seen, Floor, card.level, card.index);
            if (IsTentComplete(card.level, card.index - 1)) AddIfOpen(open, seen, Floor, card.level, card.index - 1);
        }

        // start a new tent next to it on the table
        if (card.level == 0)
        {
            AddBaseTentSlots(open, seen, baseTents, card.index - 1);
            AddBaseTentSlots(open, seen, baseTents, card.index + 1);
        }
    }

    void AddBaseTentSlots(List<(int, int, int)> open, HashSet<(int, int, int)> seen, HashSet<int> baseTents, int index)
    {
        bool tentAlreadyStarted = baseTents.Contains(index);
        bool roomForNewTent = baseTents.Count < maxBaseTents;
        if (!tentAlreadyStarted && !roomForNewTent) return;

        AddIfOpen(open, seen, Left, 0, index);
        AddIfOpen(open, seen, Right, 0, index);
    }

    bool IsTentComplete(int level, int index)
    {
        return occupied.Contains((Left, level, index)) && occupied.Contains((Right, level, index));
    }

    void AddIfOpen(List<(int, int, int)> open, HashSet<(int, int, int)> seen, int kind, int level, int index)
    {
        var slot = (kind, level, index);
        if (occupied.Contains(slot) || !seen.Add(slot)) return;
        open.Add(slot);
    }
}
