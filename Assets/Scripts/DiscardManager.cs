using System;
using UnityEngine;

/// Discard system.
///
///   1. The player selects a card in the hand.
///   2. The player clicks a Discard Zone (any 3D object with a DiscardZone component).
///   3. The selected card goes back into the deck and the player draws a new card.
///
/// How many discards the player gets each turn is set in the Inspector.
public class DiscardManager : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public HandManager handManager;
    public DeckManager deckManager;

    [Header("Rules")]
    [Tooltip("How many cards the player may discard each turn. 0 = discarding is off.")]
    [Min(0)] public int discardsPerTurn = 1;

    [Tooltip("Where the discarded card goes in the deck.")]
    public DeckReturn returnToDeck = DeckReturn.ShuffleIn;

    [Tooltip("On = the new card is drawn BEFORE the discarded card goes back, so the player never gets the same card straight back.\n" +
             "Off = the card goes back first, then the player draws (the same card is possible).")]
    public bool drawReplacementFirst = true;

    // ---------- Read-only info ----------
    public int DiscardsUsed { get; private set; }
    public int DiscardsLeft => Mathf.Max(0, discardsPerTurn - DiscardsUsed);

    /// True when the rules allow a discard (the conductor checks the phase).
    public bool CanDiscard =>
        handManager != null && deckManager != null &&
        handManager.SelectedIndex >= 0 &&
        DiscardsLeft > 0;

    // (discarded card, card drawn in its place - null if nothing could be drawn)
    public event Action<CardData, CardData> OnDiscarded;

    // =====================================================================
    //  Lifecycle
    // =====================================================================

    void Awake()
    {
        if (handManager == null) handManager = FindFirstObjectByType<HandManager>();
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
    }

    /// New hand = discards are available again. (Called by the conductor.)
    public void ResetSwaps()
    {
        DiscardsUsed = 0;
    }

    // =====================================================================
    //  Discarding
    // =====================================================================

    /// Discards the selected card. Returns true if it worked. (Called by the conductor.)
    public bool TryDiscardSelected()
    {
        if (!CanDiscard) return false;

        // take the card out of the hand (does NOT go to the discard pile)
        CardData discarded = handManager.RemoveCard(handManager.SelectedIndex);
        if (discarded == null) return false;

        int countAfterRemoval = handManager.Count;

        if (drawReplacementFirst)
        {
            handManager.DrawCards(1);
            deckManager.ReturnToDeck(discarded, returnToDeck);

            // the deck was empty, so nothing could be drawn: take a card now that one is back
            if (handManager.Count == countAfterRemoval) handManager.DrawCards(1);
        }
        else
        {
            deckManager.ReturnToDeck(discarded, returnToDeck);
            handManager.DrawCards(1);
        }

        CardData drawn = handManager.Count > countAfterRemoval
            ? handManager.GetCard(handManager.Count - 1)
            : null;

        DiscardsUsed++;
        DiscardSound();
        OnDiscarded?.Invoke(discarded, drawn);
        return true;
    }
    public void DiscardSound()
    {
        AudioManager.SFX(SoundType.UnselectCard);
    }
}