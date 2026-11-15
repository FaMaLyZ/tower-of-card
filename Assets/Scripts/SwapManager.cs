using System;
using System.Collections.Generic;
using UnityEngine;

/// Swapping cards (was DiscardManager; same script file ID, so scenes keep it).
///
///   1. The player selects one or more cards in the hand (each card costs one swap).
///   2. The player clicks a Swap Zone (any 3D object with a SwapZone component, e.g. the deck).
///   3. All the selected cards go back into the deck in one go, and the player draws that many new cards.
///
/// How many swaps the player gets each hand comes from GameConfig (the conductor passes it in).
public class SwapManager : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public HandManager handManager;
    public DeckManager deckManager;

    [Header("Rules")]
    [Tooltip("Where a swapped card goes in the deck.")]
    public DeckReturn returnToDeck = DeckReturn.ShuffleIn;

    [Tooltip("On = the new cards are drawn BEFORE the swapped cards go back, so the player never gets the same card straight back.\n" +
             "Off = the cards go back first, then the player draws (the same card is possible).")]
    public bool drawReplacementFirst = true;

    // ---------- Read-only info ----------
    public int SwapsUsed { get; private set; }
    public int SwapsLeft => Mathf.Max(0, swapsThisHand - SwapsUsed);

    int swapsThisHand;

    /// True when the rules allow swapping the selected cards (the conductor checks the phase).
    public bool CanSwap =>
        handManager != null && deckManager != null &&
        SelectedCount > 0 &&
        SelectedCount <= SwapsLeft;

    int SelectedCount => handManager.GetSelectedCardIds().Count;

    void Awake()
    {
        if (handManager == null) handManager = FindAnyObjectByType<HandManager>();
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
    }

    /// New hand = swaps are available again. (Called by the conductor.)
    public void ResetSwaps(int swapsAllowed)
    {
        swapsThisHand = swapsAllowed;
        SwapsUsed = 0;
    }

    /// Swaps every selected card in one go. Returns true if it worked. (Called by the conductor.)
    public bool TrySwapSelected()
    {
        if (!CanSwap) return false;

        // take the cards out of the hand (they do NOT go to the discard pile)
        List<CardInstance> swappedOut = RemoveSelectedCardsFromHand();
        if (swappedOut.Count == 0) return false;

        if (drawReplacementFirst) DrawThenReturn(swappedOut);
        else ReturnThenDraw(swappedOut);

        SwapsUsed += swappedOut.Count;
        SwapSound();
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
    void DrawThenReturn(List<CardInstance> swappedOut)
    {
        int drawn = handManager.DrawCards(swappedOut.Count);
        ReturnCardsToDeck(swappedOut);

        // the deck ran out before every card was replaced: draw the rest now that cards are back
        if (drawn < swappedOut.Count) handManager.DrawCards(swappedOut.Count - drawn);
    }

    void ReturnThenDraw(List<CardInstance> swappedOut)
    {
        ReturnCardsToDeck(swappedOut);
        handManager.DrawCards(swappedOut.Count);
    }

    void ReturnCardsToDeck(List<CardInstance> cards)
    {
        foreach (CardInstance card in cards)
            deckManager.ReturnToDeck(card, returnToDeck);
    }

    public void SwapSound()
    {
        AudioManager.SFX(SoundType.UnselectCard);
    }
}
