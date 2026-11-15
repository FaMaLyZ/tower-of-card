using System;
using System.Collections.Generic;
using UnityEngine;

/// Discard system.
///
///   1. The player selects one or more cards in the hand (each card costs one swap).
///   2. The player clicks a Discard Zone (any 3D object with a DiscardZone component).
///   3. All the selected cards go back into the deck in one go, and the player draws that many new cards.
///
/// How many swaps the player gets each hand comes from GameConfig (the conductor passes it in).
public class DiscardManager : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public HandManager handManager;
    public DeckManager deckManager;

    [Header("Rules")]
    [Tooltip("No longer used: the number comes from GameConfig > Swaps Per Hand. Kept so the scene value is not lost.")]
    [Min(0)] public int discardsPerTurn = 1;

    [Tooltip("Where the discarded card goes in the deck.")]
    public DeckReturn returnToDeck = DeckReturn.ShuffleIn;

    [Tooltip("On = the new cards are drawn BEFORE the discarded cards go back, so the player never gets the same card straight back.\n" +
             "Off = the cards go back first, then the player draws (the same card is possible).")]
    public bool drawReplacementFirst = true;

    // ---------- Read-only info ----------
    public int DiscardsUsed { get; private set; }
    public int DiscardsLeft => Mathf.Max(0, swapsThisHand - DiscardsUsed);

    int swapsThisHand;

    /// True when the rules allow swapping the selected cards (the conductor checks the phase).
    public bool CanDiscard =>
        handManager != null && deckManager != null &&
        SelectedCount > 0 &&
        SelectedCount <= DiscardsLeft;

    int SelectedCount => handManager.GetSelectedCardIds().Count;

    // (how many cards were swapped out, how many new cards were drawn)
    public event Action<int, int> OnDiscarded;

    // =====================================================================
    //  Lifecycle
    // =====================================================================

    void Awake()
    {
        if (handManager == null) handManager = FindFirstObjectByType<HandManager>();
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
    }

    /// New hand = swaps are available again. (Called by the conductor.)
    public void ResetSwaps(int swapsAllowed)
    {
        swapsThisHand = swapsAllowed;
        DiscardsUsed = 0;
    }

    // =====================================================================
    //  Discarding
    // =====================================================================

    /// Swaps every selected card in one go. Returns true if it worked. (Called by the conductor.)
    public bool TryDiscardSelected()
    {
        if (!CanDiscard) return false;

        // take the cards out of the hand (they do NOT go to the discard pile)
        List<CardInstance> swappedOut = RemoveSelectedCardsFromHand();
        if (swappedOut.Count == 0) return false;

        int drawn = drawReplacementFirst
            ? DrawThenReturn(swappedOut)
            : ReturnThenDraw(swappedOut);

        DiscardsUsed += swappedOut.Count;
        DiscardSound();
        OnDiscarded?.Invoke(swappedOut.Count, drawn);
        return true;
    }

    List<CardInstance> RemoveSelectedCardsFromHand()
    {
        var removed = new List<CardInstance>();
        foreach (int cardId in handManager.GetSelectedCardIds())
        {
            CardInstance card = handManager.RemoveCardById(cardId);
            if (card != null) removed.Add(card);
        }
        return removed;
    }

    // New cards first, so the player can't get a swapped card straight back
    int DrawThenReturn(List<CardInstance> swappedOut)
    {
        int drawn = handManager.DrawCards(swappedOut.Count);
        ReturnCardsToDeck(swappedOut);

        // the deck ran out before every card was replaced: draw the rest now that cards are back
        if (drawn < swappedOut.Count) drawn += handManager.DrawCards(swappedOut.Count - drawn);
        return drawn;
    }

    int ReturnThenDraw(List<CardInstance> swappedOut)
    {
        ReturnCardsToDeck(swappedOut);
        return handManager.DrawCards(swappedOut.Count);
    }

    void ReturnCardsToDeck(List<CardInstance> cards)
    {
        foreach (CardInstance card in cards)
            deckManager.ReturnToDeck(card, returnToDeck);
    }

    public void DiscardSound()
    {
        AudioManager.SFX(SoundType.UnselectCard);
    }
}