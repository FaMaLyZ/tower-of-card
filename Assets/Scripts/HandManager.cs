using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The player's hand.
///  - At the start of every turn it draws "Cards Draw Per Turn" cards from the DeckManager.
///  - The player clicks (or presses 1-9) to choose which card in the hand to play.
///  - A played card goes to the deck's discard pile.
///  - Cards are shown fanned out (layout code from your original HandManager).
public class HandManager : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public DeckManager deckManager;
    public TurnManager turnManager;

    [Header("Card visuals")]
    public GameObject cardPrefab;
    public Transform handTranform;
    public float fanSpread = 5f;
    public float cardSpacing = 5f;
    public float verticalSpacing = 10f;
    public float deepSpacing = 0.2f;

    [Header("Drawing")]
    [Tooltip("How many cards the player draws at the start of each turn.")]
    public int cardsDrawPerTurn = 5;
    [Tooltip("The hand never holds more cards than this.")]
    public int maxHandSize = 10;
    [Tooltip("On = unplayed cards go to the discard pile when the turn ends. Off = they stay in your hand.")]
    public bool discardHandAtEndOfTurn = false;
    [Tooltip("End the turn automatically when the hand is empty.")]
    public bool autoEndTurnWhenHandEmpty = true;

    [Header("Selection look")]
    public float selectedLift = 10f;        // chosen card moves up by this much
    public float selectedScale = 1.1f;      // and gets a bit bigger

    public List<GameObject> cardInHand = new List<GameObject>();
    readonly List<CardData> dataInHand = new List<CardData>();   // same order as cardInHand

    // ---------- Public info ----------
    public int Count => cardInHand.Count;
    public int SelectedIndex { get; private set; } = -1;
    public CardData SelectedCard => GetCard(SelectedIndex);
    public IReadOnlyList<CardData> CardsInHandData => dataInHand;

    public event Action OnHandChanged;
    public event Action<int> OnCardChosen;      // player clicked a card (index in hand)

    Vector3 baseScale = Vector3.one;

    // ---------- Lifecycle ----------

    void Awake()
    {
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();

        if (cardPrefab == null || handTranform == null)
            Debug.LogError("HandManager: assign Card Prefab and Hand Tranform.", this);
        else
            baseScale = cardPrefab.transform.localScale;

        if (turnManager != null)
        {
            turnManager.OnStartTurn += HandleStartTurn;
            turnManager.OnEndTurn += HandleEndTurn;
        }
    }

    void OnDestroy()
    {
        if (turnManager != null)
        {
            turnManager.OnStartTurn -= HandleStartTurn;
            turnManager.OnEndTurn -= HandleEndTurn;
        }
    }

    void Update()
    {
        UpdateCardInHand();
        CheckAutoEndTurn();
    }

    // ---------- Turn events ----------

    // Start of every turn: draw cards
    void HandleStartTurn(int turn)
    {
        DrawCards(cardsDrawPerTurn);
    }

    // End of turn: optionally throw the hand away
    void HandleEndTurn(int turn)
    {
        ClearSelection();
        if (discardHandAtEndOfTurn) DiscardHand();
    }

    // Nothing left to play = nothing left to do this turn
    void CheckAutoEndTurn()
    {
        if (!autoEndTurnWhenHandEmpty || turnManager == null) return;
        if (turnManager.CanEndTurn && Count == 0) turnManager.EndTurn();
    }

    // ---------- Drawing ----------

    /// Draws up to "amount" cards from the deck. Returns how many were drawn.
    public int DrawCards(int amount)
    {
        if (deckManager == null) return 0;

        int drawn = 0;
        for (int i = 0; i < amount && Count < maxHandSize; i++)
        {
            CardData data = deckManager.Draw();
            if (data == null) break;            // deck is completely empty

            AddCardToHand(data);
            drawn++;
        }
        return drawn;
    }

    /// Draws a single card from the deck (original method name kept).
    public void AddCardToHand()
    {
        DrawCards(1);
    }

    /// Puts a specific card into the hand (e.g. a reward card that skips the deck).
    public void AddCardToHand(CardData data)
    {
        if (data == null || Count >= maxHandSize) return;

        GameObject newCard = Instantiate(cardPrefab, handTranform.position, Quaternion.identity, handTranform);

        var view = newCard.GetComponent<HandCardView>();
        if (view == null) view = newCard.AddComponent<HandCardView>();
        view.hand = this;

        ApplyVisual(newCard, data);

        cardInHand.Add(newCard);
        dataInHand.Add(data);

        UpdateCardInHand();
        OnHandChanged?.Invoke();
    }

    // Colours the card and writes its name (works for UI cards and 3D cards)
    void ApplyVisual(GameObject card, CardData data)
    {
        var image = card.GetComponentInChildren<Image>();
        var rend = card.GetComponentInChildren<Renderer>();

        if (image != null) image.color = data.tint;
        else if (rend != null) rend.material.color = data.tint;

        var label = card.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = data.cardName;
    }

    // ---------- Choosing and playing ----------

    public CardData GetCard(int index)
    {
        return index >= 0 && index < dataInHand.Count ? dataInHand[index] : null;
    }

    public void SetSelected(int index)
    {
        SelectedIndex = (index >= 0 && index < Count) ? index : -1;
    }

    public void ClearSelection()
    {
        SelectedIndex = -1;
    }

    // Called by HandCardView when the player clicks a card in the hand
    public void ClickCard(GameObject card)
    {
        if (turnManager != null && !turnManager.CanPlaceCard) return;

        int index = cardInHand.IndexOf(card);
        if (index >= 0) OnCardChosen?.Invoke(index);
    }

    /// The card was played: remove it from the hand and discard it.
    public CardData PlayCard(int index)
    {
        if (index < 0 || index >= Count) return null;

        CardData data = dataInHand[index];
        GameObject visual = cardInHand[index];

        cardInHand.RemoveAt(index);
        dataInHand.RemoveAt(index);
        if (visual != null) Destroy(visual);

        if (deckManager != null) deckManager.Discard(data);

        SelectedIndex = -1;
        UpdateCardInHand();
        OnHandChanged?.Invoke();
        return data;
    }

    public CardData PlaySelectedCard()
    {
        return PlayCard(SelectedIndex);
    }

    /// Sends every card in the hand to the discard pile.
    public void DiscardHand()
    {
        for (int i = 0; i < cardInHand.Count; i++)
        {
            if (cardInHand[i] != null) Destroy(cardInHand[i]);
            if (deckManager != null) deckManager.Discard(dataInHand[i]);
        }

        cardInHand.Clear();
        dataInHand.Clear();
        SelectedIndex = -1;
        OnHandChanged?.Invoke();
    }

    // ---------- Layout (your original fan layout) ----------

    private void UpdateCardInHand()
    {
        if (cardInHand.Count <= 0)
        {
            return;
        }
        int cardCount = cardInHand.Count;
        float middle = (cardCount - 1) / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            if (cardInHand[i] == null) continue;
            bool isSelected = i == SelectedIndex;

            float rotationAngle = (fanSpread * (i - middle));
            cardInHand[i].transform.localRotation = Quaternion.Euler(rotationAngle, 90, 0);

            float horizontalOffset = (cardSpacing * (i - middle));
            float verticalOffset = (verticalSpacing * (i - middle)) + (isSelected ? selectedLift : 0f);
            float deeOffset = (deepSpacing * (i - middle)) + (isSelected ? selectedLift : 0f);
            cardInHand[i].transform.localPosition = new Vector3(horizontalOffset, verticalOffset, deeOffset);

            cardInHand[i].transform.localScale = baseScale * (isSelected ? selectedScale : 1f);
        }
    }
}