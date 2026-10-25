using System;
using System.Collections.Generic;
using UnityEngine;

/// The deck the player draws from each turn.
///
///   draw pile    -> cards waiting to be drawn
///   discard pile -> cards that were played (or discarded); reshuffled into the draw pile when it runs out
///
/// Add cards to the deck from the Inspector (Starting Deck) or from code:
///     deckManager.AddCard(someCardData);            // shuffled into the deck
///     deckManager.AddCard(someCardData, 3);         // three copies
///     deckManager.AddCard(someCardData, 1, true);   // on top = drawn next
public class DeckManager : MonoBehaviour
{
    [Serializable]
    public class DeckEntry
    {
        public CardData card;
        [Min(1)] public int count = 1;      // how many copies of this card
    }

    [Header("Starting deck")]
    public List<DeckEntry> startingDeck = new List<DeckEntry>();
    public bool shuffleOnStart = true;

    [Header("Card set (quick way to build a standard 52-card deck)")]
    [Tooltip("Drag many CardData here at once, or use the menu CardTower > 3. Fill Selected DeckManager.")]
    public CardData[] cardSet;
    [Min(0)] public int copiesOfEachCard = 1;

    [Header("Rules")]
    [Tooltip("When the draw pile is empty, shuffle the discard pile back into it.")]
    public bool reshuffleDiscardWhenEmpty = true;

    // The END of the list is the TOP of the pile (cheapest to remove from)
    readonly List<CardData> drawPile = new List<CardData>();
    readonly List<CardData> discardPile = new List<CardData>();

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

    // Rebuilds the deck from the Starting Deck list (also usable for a restart)
    public void ResetDeck()
    {
        drawPile.Clear();
        discardPile.Clear();

        foreach (var entry in startingDeck)
        {
            if (entry == null || entry.card == null) continue;
            for (int i = 0; i < entry.count; i++) drawPile.Add(entry.card);
        }

        if (cardSet != null)
        {
            foreach (var card in cardSet)
            {
                if (card == null) continue;
                for (int i = 0; i < copiesOfEachCard; i++) drawPile.Add(card);
            }
        }

        if (drawPile.Count == 0)
            Debug.LogWarning("DeckManager: the Starting Deck is empty - add some cards to it.", this);

        if (shuffleOnStart) Shuffle();
        OnDeckChanged?.Invoke();
    }

    // ---------- Adding cards ----------

    /// Adds copies of a card to the deck.
    /// putOnTop = false: shuffled in at random positions.   putOnTop = true: drawn next.
    public void AddCard(CardData card, int count = 1, bool putOnTop = false)
    {
        if (card == null) return;

        for (int i = 0; i < count; i++)
        {
            if (putOnTop) drawPile.Add(card);
            else drawPile.Insert(UnityEngine.Random.Range(0, drawPile.Count + 1), card);
        }
        OnDeckChanged?.Invoke();
    }

    /// Adds a card straight to the discard pile.
    public void AddCardToDiscard(CardData card)
    {
        if (card == null) return;
        discardPile.Add(card);
        OnDeckChanged?.Invoke();
    }

    // ---------- Drawing ----------

    /// Draws the top card. Returns null if there is nothing left to draw.
    public CardData Draw()
    {
        if (drawPile.Count == 0 && reshuffleDiscardWhenEmpty && discardPile.Count > 0)
            RefillFromDiscard();

        if (drawPile.Count == 0) return null;

        CardData card = drawPile[drawPile.Count - 1];
        drawPile.RemoveAt(drawPile.Count - 1);
        OnDeckChanged?.Invoke();
        return card;
    }

    /// Puts a used card on the discard pile.
    public void Discard(CardData card)
    {
        if (card == null) return;
        discardPile.Add(card);
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