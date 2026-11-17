using System.Collections.Generic;

/// The commit step: the player selects 1-5 hand cards and presses Play Hand.
/// Previews the poker hand, accepts the commit only if every card can be placed,
/// and tracks which locked cards are still waiting. The conductor changes the phase.
public class CommitManager
{
    readonly HandManager hand;
    readonly TowerBuilder tower;
    readonly ScoreManager scorer;
    readonly int maxCommitCards;

    readonly List<CardInstance> committedCards = new List<CardInstance>();
    readonly List<CardInstance> unplacedCards = new List<CardInstance>();

    public CommitManager(HandManager hand, TowerBuilder tower, ScoreManager scorer, int maxCommitCards)
    {
        this.hand = hand;
        this.tower = tower;
        this.scorer = scorer;
        this.maxCommitCards = maxCommitCards;
    }

    // ---------- Read-only info (the HUD reads these) ----------

    /// The locked set, in the order the player picked the cards. The poker score uses exactly these.
    public IReadOnlyList<CardInstance> CommittedCards => committedCards;
    public int UnplacedCount => unplacedCards.Count;

    /// The poker hand the selected cards make (null = nothing selected).
    public PokerHandResult Preview { get; private set; }

    /// What the selected cards would score: base chips, card chips, mult and total (null = nothing selected).
    /// Worked out by the scorer, so the HUD shows it without doing any maths.
    public ScoreContext PreviewScore { get; private set; }

    /// Why Play Hand would be refused right now ("" = the commit is fine).
    public string Problem { get; private set; } = "";

    /// How many hand cards are picked for the commit right now.
    public int SelectedCount { get; private set; }

    // ---------- Before Play Hand ----------

    /// Works out the hand name and the problem (if any) for the cards selected right now.
    /// The conductor calls this every time the selection changes.
    public PokerHandResult PreviewSelectedHand()
    {
        List<CardInstance> selected = hand.GetSelectedCards();

        SelectedCount = selected.Count;
        List<CardData> data = GetCardData(selected);
        Preview = selected.Count > 0 ? PokerHandEvaluator.Evaluate(data) : null;
        PreviewScore = selected.Count > 0 && scorer != null ? scorer.PreviewHand(data) : null;
        Problem = FindProblem(selected.Count);
        return Preview;
    }

    string FindProblem(int cardCount)
    {
        if (cardCount < 1) return $"Select 1 to {maxCommitCards} cards";
        if (cardCount > maxCommitCards) return $"Select at most {maxCommitCards} cards";
        if (!tower.CanFit(cardCount)) return $"The tower has no room for {cardCount} cards. Select fewer.";
        return "";
    }

    /// Play Hand: locks the selected cards if the commit is legal. A refused commit changes nothing.
    public bool TryCommitSelectedCards()
    {
        PreviewSelectedHand();                  // check again now; a greyed-out button is not the safety check
        if (Problem != "") return false;

        committedCards.Clear();
        committedCards.AddRange(hand.GetSelectedCards());
        unplacedCards.Clear();
        unplacedCards.AddRange(committedCards);
        return true;
    }

    // ---------- While building ----------

    public bool HasUnplacedCards() => unplacedCards.Count > 0;

    /// The committed card with this ID that is still waiting to be placed (null = not one of them).
    public CardInstance FindUnplacedCard(int cardId)
    {
        foreach (CardInstance card in unplacedCards)
            if (card.Id == cardId) return card;
        return null;
    }

    public CardInstance GetFirstUnplacedCard()
    {
        return unplacedCards.Count > 0 ? unplacedCards[0] : null;
    }

    public void MarkPlaced(CardInstance card)
    {
        unplacedCards.Remove(card);
    }

    /// Undo: the card is waiting to be placed again.
    public void MarkUnplaced(CardInstance card)
    {
        if (committedCards.Contains(card) && !unplacedCards.Contains(card)) unplacedCards.Add(card);
    }

    /// New hand: forget the last commit.
    public void Clear()
    {
        committedCards.Clear();
        unplacedCards.Clear();
        Preview = null;
        PreviewScore = null;
        Problem = "";
        SelectedCount = 0;
    }

    static List<CardData> GetCardData(List<CardInstance> cards)
    {
        var data = new List<CardData>(cards.Count);
        foreach (CardInstance card in cards) data.Add(card.Data);
        return data;
    }
}
