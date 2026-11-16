using System;
using System.Collections.Generic;
using UnityEngine;

/// Where a card goes when it is put back into the deck.
public enum DeckReturn
{
    ShuffleIn,   // random position in the draw pile
    Bottom,      // drawn last
    Top          // drawn next
}

/// The deck the player draws from each turn. The deck is set up in the Inspector and does not change during play.
///
///   draw pile    -> cards waiting to be drawn
///   discard pile -> cards that were played; reshuffled into the draw pile when it runs out
public class DeckManager : MonoBehaviour
{
    [Serializable]
    public class DeckEntry
    {
        public CardData card;
        [Min(1)] public int count = 1;
    }

    [Header("Starting deck")]
    public List<DeckEntry> startingDeck = new List<DeckEntry>();
    public bool shuffleOnStart = true;

    [Header("Card set")]
    public CardData[] cardSet;
    [Min(0)] public int copiesOfEachCard = 1;

    [Header("Rules")]
    public bool reshuffleDiscardWhenEmpty = true;

    // The END of the list is the TOP of the pile (cheapest to remove from)
    readonly List<CardInstance> drawPile = new List<CardInstance>();
    readonly List<CardInstance> discardPile = new List<CardInstance>();

    // Card IDs start again at 0 every time the deck is reset
    int nextCardId;

    // Raised whenever the deck or discard pile changes (UI can listen)
    public event Action OnDeckChanged;

    public int DrawPileCount => drawPile.Count;
    public int DiscardCount => discardPile.Count;
    public int TotalCards => drawPile.Count + discardPile.Count;

    // ---------- Lifecycle ----------

    void Awake()
    {
        ResetDeck();
    }

    // Rebuilds the deck from the Starting Deck list (also usable for a restart).
    // Every card gets a fresh ID, so empty the hand and the tower BEFORE calling this.
    public void ResetDeck()
    {
        drawPile.Clear();
        discardPile.Clear();
        nextCardId = 0;

        foreach (var entry in startingDeck)
        {
            if (entry == null || entry.card == null) continue;
            for (int i = 0; i < entry.count; i++) AddNewCardToDrawPile(entry.card);
        }

        if (cardSet != null)
        {
            foreach (var card in cardSet)
            {
                if (card == null) continue;
                for (int i = 0; i < copiesOfEachCard; i++) AddNewCardToDrawPile(card);
            }
        }

        if (drawPile.Count == 0)
            Debug.LogWarning("DeckManager: the Starting Deck is empty - add some cards to it.", this);

        if (shuffleOnStart) Shuffle();
        OnDeckChanged?.Invoke();
    }

    void AddNewCardToDrawPile(CardData data)
    {
        drawPile.Add(new CardInstance(nextCardId, data));
        nextCardId++;
    }

    // ---------- Drawing ----------

    /// Draws the top card. Returns null if there is nothing left to draw.
    public CardInstance Draw()
    {
        if (drawPile.Count == 0 && reshuffleDiscardWhenEmpty && discardPile.Count > 0)
            RefillFromDiscard();

        if (drawPile.Count == 0) return null;

        CardInstance card = drawPile[drawPile.Count - 1];
        drawPile.RemoveAt(drawPile.Count - 1);
        OnDeckChanged?.Invoke();
        return card;
    }

    /// Puts a used card on the discard pile.
    public void Discard(CardInstance card)
    {
        if (card == null) return;
        discardPile.Add(card);
        OnDeckChanged?.Invoke();
    }

    /// Puts a card back into the draw pile (used when the player discards).
    public void ReturnToDeck(CardInstance card, DeckReturn where = DeckReturn.ShuffleIn)
    {
        if (card == null) return;

        switch (where)
        {
            case DeckReturn.Top:
                drawPile.Add(card);                       // end of the list = top of the pile
                break;
            case DeckReturn.Bottom:
                drawPile.Insert(0, card);
                break;
            default:
                drawPile.Insert(UnityEngine.Random.Range(0, drawPile.Count + 1), card);
                break;
        }
        OnDeckChanged?.Invoke();
    }

    // ---------- Shuffling ----------

    public void Shuffle()
    {
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
        }
    }

    void RefillFromDiscard()
    {
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Shuffle();
    }
}