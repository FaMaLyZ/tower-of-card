using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The player's hand.
///  - When the conductor starts a hand it draws "Cards Draw Per Turn" cards from the DeckManager.
///  - The player clicks (or presses 1-9) to choose which card in the hand to play.
///  - A played card goes to the deck's discard pile.
///  - Cards are shown fanned out (layout code from your original HandManager).
///  - If a CardData has a Hand Model it is shown in the hand; otherwise Card Prefab is used.
///
/// B0 hand contract:
///  - Every hand card is a ROOT object (HandCardView + click collider) with a "Visual" child (the model).
///    This script moves the root only. Effects may move the Visual only.
///  - Cards are told apart by card ID, not by hand position. Selection and hover remember the ID,
///    so they stay on the same card when the hand changes.
public class HandManager : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public DeckManager deckManager;
    public GameFlowController flow;

    [Header("Card visuals")]
    [Tooltip("Used for cards that have no Hand Model.")]
    public GameObject cardPrefab;
    [RequiredField] public Transform handTranform;
    public float fanSpread = 5f;
    public float cardSpacing = 5f;
    public float verticalSpacing = 10f;
    public float deepSpacing = 0.01f;
    [Tooltip("Size of every card in the hand. Change it while playing to see the result live.")]
    public float handCardScale = 1f;
    [Tooltip("Extra rotation applied to cards that use a Hand Model (use multiples of 90). Fix sideways / upside-down models here.")]
    public Vector3 handModelEuler = Vector3.zero;

    [Header("Drawing")]
    [Tooltip("How many cards the player draws at the start of each turn.")]
    public int cardsDrawPerTurn = 5;
    [Tooltip("The hand never holds more cards than this.")]
    public int maxHandSize = 10;
    [Tooltip("No longer used: unplayed cards always stay in the hand. Kept so the scene value is not lost.")]
    public bool discardHandAtEndOfTurn = false;
    [Tooltip("No longer used: the conductor ends the hand when the hand is empty. Kept so the scene value is not lost.")]
    public bool autoEndTurnWhenHandEmpty = true;

    [Header("Smoothing")]
    [Tooltip("How fast cards glide to their place. Higher = snappier, 0 = instant (no smoothing).")]
    public float moveSpeed = 12f;
    [Tooltip("Optional: new cards fly in from this object (for example the deck). Empty = the middle of the hand.")]
    public Transform drawFromPoint;
    [Tooltip("New cards start at this fraction of their size and grow to full size. 1 = no growing.")]
    [Range(0f, 1f)] public float spawnScale = 0.5f;

    [Header("Selection look")]
    public float selectedLift = 10f;        // chosen card moves up by this much
    public float selectedScale = 1.1f;      // and gets a bit bigger

    [Header("Check Gameover")]
    public int allCardLeft = 0;

    const int NoCard = -1;

    // The hand, left to right. dataInHand is always in the same order as cardsInHand.
    readonly List<HandCardView> cardsInHand = new List<HandCardView>();
    readonly List<CardData> dataInHand = new List<CardData>();

    // Selection and hover remember the card ID, never the hand position
    int selectedCardId = NoCard;
    int hoveredCardId = NoCard;

    // ---------- Public info ----------
    public int Count => cardsInHand.Count;
    public int SelectedCardId => selectedCardId;
    public int SelectedIndex => FindIndexOfCard(selectedCardId);
    public CardData SelectedCard => GetCard(SelectedIndex);

    // The card under the mouse (-1 = none)
    public int HoveredIndex => FindIndexOfCard(hoveredCardId);

    /// The card whose details should be shown: the one under the mouse, otherwise the selected one.
    public CardData InspectedCard
    {
        get
        {
            CardData hovered = GetCard(HoveredIndex);
            return hovered != null ? hovered : SelectedCard;
        }
    }
    public IReadOnlyList<CardData> CardsInHandData => dataInHand;

    public event Action OnHandChanged;

    // ---------- Lifecycle ----------

    void Awake()
    {
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();

        if (handTranform == null)
            Debug.LogError("HandManager: assign Hand Tranform.", this);
        if (cardPrefab == null)
            Debug.LogWarning("HandManager: no Card Prefab - cards without a Hand Model will not be shown.", this);
    }

    void Update()
    {
        UpdateCardInHand();
    }

    // ---------- Drawing ----------

    /// Start of a hand: draw "Cards Draw Per Turn" cards (the hand never goes above Max Hand Size).
    public int DrawCardsForNewHand()
    {
        return DrawCards(cardsDrawPerTurn);
    }

    /// Draws up to "amount" cards from the deck. Returns how many were drawn.
    public int DrawCards(int amount)
    {
        if (deckManager == null) return 0;

        int drawn = 0;
        for (int i = 0; i < amount && Count < maxHandSize; i++)
        {
            CardInstance card = deckManager.Draw();
            if (card == null) break;            // deck is completely empty

            AddCardToHand(card);
            drawn++;
        }
        return drawn;
    }

    /// Draws a single card from the deck (original method name kept).
    public void AddCardToHand()
    {
        DrawCards(1);
    }

    // Creates the card in the hand: a root we move, with the model as its "Visual" child
    void AddCardToHand(CardInstance card)
    {
        if (card == null || card.Data == null || Count >= maxHandSize || handTranform == null) return;

        GameObject prefab = PickHandPrefab(card.Data);
        if (prefab == null) return;

        HandCardView view = CreateCardRoot(card, prefab.layer);
        Transform visual = CreateCardVisual(view.transform, prefab, card.Data);
        view.Setup(this, card, visual);
        EnsureClickable(view.gameObject);       // measured now, while the root is still at full size

        cardsInHand.Add(view);
        dataInHand.Add(card.Data);
        MoveToSpawnPoint(view.transform, cardsInHand.Count - 1);

        OnHandChanged?.Invoke();
    }

    GameObject PickHandPrefab(CardData data)
    {
        return data.handModel != null ? data.handModel : cardPrefab;
    }

    // The root: an empty object that holds the card's place in the hand and takes the clicks
    HandCardView CreateCardRoot(CardInstance card, int layer)
    {
        var root = new GameObject($"HandCard #{card.Id} ({card.Data.cardName})");
        root.layer = layer;                     // same layer as the model, so the camera's raycaster still sees it
        root.transform.SetParent(handTranform, false);
        return root.AddComponent<HandCardView>();
    }

    // The Visual: the model the player sees. It sits exactly on the root until an effect moves it.
    Transform CreateCardVisual(Transform root, GameObject prefab, CardData data)
    {
        bool useModel = data.handModel != null;

        GameObject visual = Instantiate(prefab, root);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = useModel ? Quaternion.Euler(handModelEuler) : Quaternion.identity;
        visual.transform.localScale = prefab.transform.localScale;

        if (!useModel) ApplyVisual(visual, data);   // tint + name for the generic card prefab
        return visual.transform;
    }

    // Start at the spawn point so the card glides into its place
    void MoveToSpawnPoint(Transform root, int index)
    {
        GetHandTarget(index, out _, out Quaternion rotation, out Vector3 scale);

        root.localPosition = drawFromPoint != null
            ? handTranform.InverseTransformPoint(drawFromPoint.position)
            : Vector3.zero;
        root.localRotation = rotation;
        root.localScale = scale * spawnScale;
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

    // A 3D model with no collider can't be clicked - add one to the ROOT that fits the model.
    // The collider is on the root on purpose: effects move the Visual, the click target stays put.
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

    /// The ID of the card at this hand position (-1 = no card there).
    public int GetCardId(int index)
    {
        return index >= 0 && index < cardsInHand.Count ? cardsInHand[index].CardId : NoCard;
    }

    /// Where this card is in the hand right now (-1 = not in the hand).
    public int FindIndexOfCard(int cardId)
    {
        if (cardId == NoCard) return -1;

        for (int i = 0; i < cardsInHand.Count; i++)
            if (cardsInHand[i].CardId == cardId) return i;
        return -1;
    }

    public void SetSelected(int index)
    {
        selectedCardId = GetCardId(index);
        SelectCardSound();
    }

    public void ClearSelection()
    {
        if (SelectedIndex < 0) return;          // nothing was selected: no change, no sound
        selectedCardId = NoCard;
        ClearSelectedCardSound();
    }

    // Called by HandCardView when the mouse moves onto / off a card
    public void HoverCard(HandCardView card)
    {
        hoveredCardId = card != null ? card.CardId : NoCard;
    }

    public void UnhoverCard(HandCardView card)
    {
        if (card != null && card.CardId == hoveredCardId) hoveredCardId = NoCard;
    }

    // Called by HandCardView when the player clicks a card in the hand.
    // The click is a command: the conductor decides if it counts in the current phase.
    public void ClickCard(HandCardView card)
    {
        int index = cardsInHand.IndexOf(card);
        if (index >= 0 && flow != null) flow.RequestSelectCard(index);
    }

    /// Takes a card out of the hand WITHOUT sending it anywhere (DiscardManager uses this).
    public CardInstance RemoveCard(int index)
    {
        if (index < 0 || index >= Count) return null;

        HandCardView view = cardsInHand[index];
        CardInstance card = view != null ? view.Card : null;

        cardsInHand.RemoveAt(index);
        dataInHand.RemoveAt(index);
        if (view != null) Destroy(view.gameObject);

        selectedCardId = NoCard;
        hoveredCardId = NoCard;
        OnHandChanged?.Invoke();
        return card;
    }

    /// The card was played: remove it from the hand and put it on the discard pile.
    public CardData PlayCard(int index)
    {
        CardInstance card = RemoveCard(index);
        if (card == null) return null;

        if (deckManager != null) deckManager.Discard(card);
        return card.Data;
    }

    public CardData PlaySelectedCard()
    {
        return PlayCard(SelectedIndex);
    }

    /// Sends every card in the hand to the discard pile.
    public void DiscardHand()
    {
        foreach (HandCardView view in cardsInHand)
            if (deckManager != null && view != null) deckManager.Discard(view.Card);

        EmptyHand();
    }

    /// Removes every card from the hand WITHOUT sending it anywhere (a restart resets the deck itself).
    public void EmptyHand()
    {
        foreach (HandCardView view in cardsInHand)
            if (view != null) Destroy(view.gameObject);

        cardsInHand.Clear();
        dataInHand.Clear();
        selectedCardId = NoCard;
        hoveredCardId = NoCard;
        OnHandChanged?.Invoke();
    }

    // ---------- Layout (your original fan layout, now smoothed) ----------

    // Where the ROOT of card number i wants to be (the Visual child just rides along)
    void GetHandTarget(int i, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        float middle = (cardsInHand.Count - 1) / 2f;
        bool isSelected = cardsInHand[i].CardId == selectedCardId;

        float rotationAngle = fanSpread * (i - middle);
        rotation = Quaternion.Euler(0, 180, rotationAngle);

        float horizontalOffset = cardSpacing * (i - middle);
        float verticalOffset = verticalSpacing * (i - middle) + (isSelected ? selectedLift : 0f);
        float deedOffset = (deepSpacing * (i - middle)) + (isSelected ? selectedLift : 0f);
        position = new Vector3(horizontalOffset, verticalOffset, deedOffset);
        scale = Vector3.one * handCardScale * (isSelected ? selectedScale : 1f);
    }

    // Moves every card a little towards its place each frame
    private void UpdateCardInHand()
    {
        if (cardsInHand.Count <= 0)
        {
            return;
        }

        // frame-rate independent smoothing (moveSpeed 0 = jump straight there)
        float t = moveSpeed <= 0f ? 1f : 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            if (cardsInHand[i] == null) continue;

            GetHandTarget(i, out Vector3 position, out Quaternion rotation, out Vector3 scale);

            Transform card = cardsInHand[i].transform;
            card.localPosition = Vector3.Lerp(card.localPosition, position, t);
            card.localRotation = Quaternion.Slerp(card.localRotation, rotation, t);
            card.localScale = Vector3.Lerp(card.localScale, scale, t);
        }
    }    
    public int GetAllCardCount()
    {
        return countAllCardLeft();
    }
    public int countAllCardLeft()
    {
        allCardLeft = deckManager.DrawPileCount + cardsInHand.Count;
        return allCardLeft;
    }

    //for fillykung
    public void DarwSound()
    {
        AudioManager.SFX(SoundType.DrawCard);
    }
    public void SelectCardSound()
    {
        AudioManager.SFX(SoundType.SelectCard);
    }
    public void ClearSelectedCardSound()
    {
        AudioManager.SFX(SoundType.UnselectCard);
    }
}