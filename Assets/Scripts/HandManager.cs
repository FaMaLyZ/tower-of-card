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
///  - If a CardData has a Hand Model it is shown in the hand; otherwise Card Prefab is used.
public class HandManager : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public DeckManager deckManager;
    public TurnManager turnManager;

    [Header("Card visuals")]
    [Tooltip("Used for cards that have no Hand Model.")]
    public GameObject cardPrefab;
    public Transform handTranform;
    public float fanSpread = 5f;
    public float cardSpacing = 5f;
    public float verticalSpacing = 10f;
    public float deepSpacing = 0.01f;
    [Tooltip("Extra rotation applied to cards that use a Hand Model (use multiples of 90). Fix sideways / upside-down models here.")]
    public Vector3 handModelEuler = Vector3.zero;

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

    // Same order as cardInHand
    class HandInfo
    {
        public Vector3 baseScale;
        public Quaternion extraRotation;
    }
    readonly List<CardData> dataInHand = new List<CardData>();
    readonly List<HandInfo> infoInHand = new List<HandInfo>();

    // ---------- Public info ----------
    public int Count => cardInHand.Count;
    public int SelectedIndex { get; private set; } = -1;
    public CardData SelectedCard => GetCard(SelectedIndex);
    public IReadOnlyList<CardData> CardsInHandData => dataInHand;

    public event Action OnHandChanged;
    public event Action<int> OnCardChosen;      // player clicked a card (index in hand)

    // ---------- Lifecycle ----------

    void Awake()
    {
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();

        if (handTranform == null)
            Debug.LogError("HandManager: assign Hand Tranform.", this);
        if (cardPrefab == null)
            Debug.LogWarning("HandManager: no Card Prefab - cards without a Hand Model will not be shown.", this);

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
        if (data == null || Count >= maxHandSize || handTranform == null) return;

        bool useModel = data.handModel != null;
        GameObject prefab = useModel ? data.handModel : cardPrefab;
        if (prefab == null) return;

        GameObject newCard = Instantiate(prefab, handTranform.position, Quaternion.identity, handTranform);

        var view = newCard.GetComponent<HandCardView>();
        if (view == null) view = newCard.AddComponent<HandCardView>();
        view.hand = this;

        if (!useModel) ApplyVisual(newCard, data);   // tint + name for the generic card prefab
        EnsureClickable(newCard);

        cardInHand.Add(newCard);
        dataInHand.Add(data);
        infoInHand.Add(new HandInfo
        {
            baseScale = prefab.transform.localScale,
            extraRotation = useModel ? Quaternion.Euler(handModelEuler) : Quaternion.identity
        });

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

    // A 3D model with no collider can't be clicked - add one that fits the model
    void EnsureClickable(GameObject card)
    {
        if (card.GetComponentInChildren<Graphic>() != null) return;    // UI card
        if (card.GetComponentInChildren<Collider>() != null) return;   // already has one

        Bounds b = CardModelUtil.GetLocalBounds(card.transform);
        if (b.size == Vector3.zero) return;

        var box = card.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size;
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
        infoInHand.RemoveAt(index);
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
        infoInHand.Clear();
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
            HandInfo info = infoInHand[i];

            float rotationAngle = (fanSpread * (i - middle));
            cardInHand[i].transform.localRotation = Quaternion.Euler(0, 180, rotationAngle) * info.extraRotation;

            float horizontalOffset = (cardSpacing * (i - middle));
            float verticalOffset = (verticalSpacing * (i - middle)) + (isSelected ? selectedLift : 0f);
            float deedOffset = (deepSpacing * (i - middle)) + (isSelected ? selectedLift : 0f);
            cardInHand[i].transform.localPosition = new Vector3(horizontalOffset, verticalOffset, deedOffset);
            cardInHand[i].transform.localScale = info.baseScale * (isSelected ? selectedScale : 1f);
        }
    }
}