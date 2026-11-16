using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// The player's hand.
///  - When the conductor starts a hand it fills the hand from the DeckManager.
///  - The player clicks a card (or presses 1-9). The click is a command: the conductor decides what
///    it means in the current phase (pick a card to swap, add it to the commit, pick it up to place).
///  - Several cards can be selected at once (the commit). Selected cards are shown lifted.
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
    [Tooltip("The hand never holds more cards than this.")]
    public int maxHandSize = 8;

    [Header("Smoothing")]
    [Tooltip("How fast cards glide to a new place when the hand changes. Higher = snappier, 0 = instant (no smoothing). " +
             "New cards appear in their place at once; the flight from the deck is an effect (DrawJuice).")]
    public float moveSpeed = 12f;

    [Header("Selection look")]
    public float selectedLift = 10f;        // chosen card moves up by this much
    public float selectedScale = 1.1f;      // and gets a bit bigger
    [Tooltip("Selected cards come this much closer to the camera, so they are drawn on top of the rest.")]
    public float selectedForward = 0.05f;

    [Header("Hover look")]
    [Tooltip("The hovered card comes this much closer to the camera, so it is on top and its whole face can be pointed at.")]
    public float hoverForward = 0.08f;
    [Tooltip("How far the hovered card's neighbours move aside (hand units). They really move, so their click areas move too.")]
    public float hoverGap = 0.15f;
    [Tooltip("How many cards on each side make room. The gap gets smaller further out.")]
    public float hoverGapCards = 2f;

    [Header("After Play Hand (Build and the rest of the hand)")]
    [Tooltip("How high the committed cards sit while you build (instead of Selected Lift).")]
    public float lockedInLift = 0.1f;
    [Tooltip("How far the waiting cards drop toward the bottom of the screen.")]
    public float waitingDrop = 0.35f;
    [Tooltip("Size of the waiting cards (1 = normal).")]
    public float waitingScale = 0.75f;
    [Tooltip("Colour of the waiting cards (1 = normal, 0 = black).")]
    [Range(0f, 1f)] public float waitingBrightness = 0.5f;
    [Tooltip("Spacing of the waiting cards, as a part of Card Spacing (they are smaller, so they sit closer).")]
    public float waitingSpacing = 0.75f;

    [Header("Check Gameover")]
    public int allCardLeft = 0;

    const int NoCard = -1;

    // The hand, left to right. dataInHand is always in the same order as cardsInHand.
    readonly List<HandCardView> cardsInHand = new List<HandCardView>();
    readonly List<CardData> dataInHand = new List<CardData>();

    // Selection and hover remember the card ID, never the hand position
    // In the order the player picked them (one card when swapping, up to five for a commit).
    readonly List<int> selectedCardIds = new List<int>();
    int hoveredCardId = NoCard;

    // ---------- Public info ----------
    public int Count => cardsInHand.Count;
    /// The card picked most recently (-1 = nothing selected).
    public int SelectedCardId => selectedCardIds.Count > 0 ? selectedCardIds[selectedCardIds.Count - 1] : NoCard;
    public int SelectedIndex => FindIndexOfCard(SelectedCardId);
    public CardData SelectedCard => GetCard(SelectedIndex);

    /// The card being dragged to a new place in the hand (-1 = none).
    public int DraggedCardId { get; private set; } = NoCard;
    public bool IsDragging => DraggedCardId != NoCard;

    // The card under the mouse (-1 = none)
    // (only a card the player can use right now counts as hovered: waiting cards ignore the mouse)
    public int HoveredIndex => IsInteractable(hoveredCardId) ? FindIndexOfCard(hoveredCardId) : -1;

    /// The card picked up by dragging it out of the hand in Build (-1 = none).
    public int PlacingDragCardId { get; private set; } = NoCard;

    /// Can the player use this card right now? Only while hand cards can be clicked, and after
    /// Play Hand only the committed cards: the waiting cards ignore hover, clicks and drags.
    public bool IsInteractable(int cardId)
    {
        if (cardId == NoCard || flow == null || !flow.IsReady || !flow.CanClickHandCards) return false;
        return !(IsLockedIn && !IsSelected(cardId));
    }

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
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();

        if (handTranform == null)
            Debug.LogError("HandManager: assign Hand Tranform.", this);
        if (cardPrefab == null)
            Debug.LogWarning("HandManager: no Card Prefab - cards without a Hand Model will not be shown.", this);
    }

    void Update()
    {
        HandleNumberKeys();
        UpdateCardInHand();
    }

    // Keys 1-9 do the same as clicking that card in the hand
    void HandleNumberKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || flow == null) return;

        int keyCount = Mathf.Min(9, Count);
        for (int i = 0; i < keyCount; i++)
            if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                flow.RequestSelectCard(i);
    }

    // ---------- Drawing ----------

    /// Start of a hand: draw until the hand holds "handSize" cards (it never goes above Max Hand Size).
    public int DrawCardsUntilHandHas(int handSize)
    {
        return DrawCards(handSize - Count);
    }

    /// Draws up to "amount" cards from the deck. Returns how many were drawn.
    public int DrawCards(int amount)
    {
        if (deckManager == null) return 0;

        var drawnIds = new List<int>();
        for (int i = 0; i < amount && Count < maxHandSize; i++)
        {
            CardInstance card = deckManager.Draw();
            if (card == null) break;            // deck is completely empty

            AddCardToHand(card);
            if (FindIndexOfCard(card.Id) >= 0) drawnIds.Add(card.Id);
        }

        if (drawnIds.Count > 0) GameEvents.RaiseCardsDrawn(drawnIds);
        return drawnIds.Count;
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
        PutInPlace(view.transform, cardsInHand.Count - 1);

        AnnounceHandChanged();
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

    // A new card's root starts exactly at its place in the hand, so it can be clicked at once
    void PutInPlace(Transform root, int index)
    {
        GetHandTarget(index, out Vector3 position, out Quaternion rotation, out Vector3 scale);
        root.localPosition = position;
        root.localRotation = rotation;
        root.localScale = scale;
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

    /// The hand card showing this card ID (null = not in the hand). Effects use it to find a card's Visual.
    public HandCardView FindView(int cardId)
    {
        int index = FindIndexOfCard(cardId);
        return index >= 0 ? cardsInHand[index] : null;
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
        selectedCardIds.Clear();
        int cardId = GetCardId(index);
        if (cardId != NoCard) selectedCardIds.Add(cardId);
        SelectCardSound();
        AnnounceSelectionChanged();
    }

    public void ClearSelection()
    {
        if (selectedCardIds.Count == 0) return; // nothing was selected: no change, no sound
        selectedCardIds.Clear();
        ClearSelectedCardSound();
        AnnounceSelectionChanged();
    }

    /// Multi-select (the commit): adds the card to the selection, or takes it out if it is already in.
    /// Returns false when nothing changed: the card is not in the hand, or the selection is already full.
    public bool ToggleSelected(int cardId, int maxSelected)
    {
        if (FindIndexOfCard(cardId) < 0) return false;

        if (selectedCardIds.Remove(cardId))
        {
            ClearSelectedCardSound();
        }
        else
        {
            if (selectedCardIds.Count >= maxSelected) return false;
            selectedCardIds.Add(cardId);
            SelectCardSound();
        }

        AnnounceSelectionChanged();
        return true;
    }

    public bool IsSelected(int cardId)
    {
        return selectedCardIds.Contains(cardId);
    }

    /// The selected cards, in the order they were picked.
    public List<CardInstance> GetSelectedCards()
    {
        var cards = new List<CardInstance>(selectedCardIds.Count);
        foreach (int cardId in selectedCardIds)
        {
            int index = FindIndexOfCard(cardId);
            if (index >= 0) cards.Add(cardsInHand[index].Card);
        }
        return cards;
    }

    // Used when ONE card leaves the hand: the other cards stay selected. No sound.
    void ForgetCard(int cardId)
    {
        if (hoveredCardId == cardId) hoveredCardId = NoCard;
        if (DraggedCardId == cardId) DraggedCardId = NoCard;
        if (PlacingDragCardId == cardId) PlacingDragCardId = NoCard;
        if (selectedCardIds.Remove(cardId)) AnnounceSelectionChanged();
    }

    // Used when the whole hand is emptied: no sound, and listeners are told only if something was selected
    void ForgetSelectionAndHover()
    {
        bool hadSelection = selectedCardIds.Count > 0;
        selectedCardIds.Clear();
        hoveredCardId = NoCard;
        DraggedCardId = NoCard;
        PlacingDragCardId = NoCard;
        if (hadSelection) AnnounceSelectionChanged();
    }

    // ---------- Announcements (told AFTER the change is done) ----------

    void AnnounceHandChanged()
    {
        OnHandChanged?.Invoke();
        GameEvents.RaiseHandChanged(GetCardIdsInOrder());
    }

    void AnnounceSelectionChanged()
    {
        GameEvents.RaiseSelectionChanged(GetSelectedCardIds());
    }

    /// The card IDs in the hand, left to right.
    public List<int> GetCardIdsInOrder()
    {
        var ids = new List<int>(cardsInHand.Count);
        foreach (HandCardView view in cardsInHand) ids.Add(view.CardId);
        return ids;
    }

    /// A copy of the selected card IDs, in the order they were picked.
    public List<int> GetSelectedCardIds()
    {
        return new List<int>(selectedCardIds);
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
        if (card == null || flow == null || !IsInteractable(card.CardId)) return;
        flow.RequestSelectCardById(card.CardId);
    }

    // ---------- Reordering (B4) ----------

    // Called by HandCardView when a drag starts.
    //  - Build: the drag picks the committed card up; letting go over a snap point places it.
    //  - Swap / Commit: the drag reorders the hand.
    public void BeginDragCard(HandCardView card)
    {
        if (card == null || flow == null || !IsInteractable(card.CardId)) return;

        if (flow.IsBuilding)
        {
            PlacingDragCardId = card.CardId;
            flow.RequestSelectCardById(card.CardId);    // in Build this picks the card up to place it
            return;
        }

        if (flow.CanReorderHand) DraggedCardId = card.CardId;
    }

    // The dragged card asks to move to the hand place closest to the mouse (left-right on screen)
    public void DragCardTo(HandCardView card, Vector2 screenPosition, Camera cam)
    {
        if (card == null || card.CardId != DraggedCardId || flow == null) return;

        int target = FindPlaceNearestScreenX(screenPosition.x, cam);
        if (target >= 0 && target != FindIndexOfCard(card.CardId)) flow.RequestMoveCard(card.CardId, target);
    }

    public void EndDragCard(HandCardView card)
    {
        if (card == null) return;
        if (card.CardId == DraggedCardId) DraggedCardId = NoCard;
        if (card.CardId == PlacingDragCardId) DropPlacingCard(card.CardId);
    }

    // Let go of a card dragged out in Build: placed if it is over a snap point (or the table for the
    // first card), otherwise it goes back to the hand. Placing removes it from the hand.
    void DropPlacingCard(int cardId)
    {
        PlacingDragCardId = NoCard;
        flow.RequestPlaceHeldCard();
        if (FindIndexOfCard(cardId) >= 0) flow.RequestDropHeldCard();
    }

    /// Moves one card to another place in the hand. Selection follows the card ID, so it is unchanged.
    /// Returns false if nothing moved. (Called by the conductor.)
    public bool MoveCard(int cardId, int newIndex)
    {
        int oldIndex = FindIndexOfCard(cardId);
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Count || newIndex == oldIndex) return false;

        HandCardView view = cardsInHand[oldIndex];
        CardData data = dataInHand[oldIndex];
        cardsInHand.RemoveAt(oldIndex);
        dataInHand.RemoveAt(oldIndex);
        cardsInHand.Insert(newIndex, view);
        dataInHand.Insert(newIndex, data);

        AnnounceHandChanged();
        return true;
    }

    // Which hand place is closest to this screen x (the places, not where the cards are right now)
    int FindPlaceNearestScreenX(float screenX, Camera cam)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < cardsInHand.Count; i++)
        {
            GetHandTarget(i, out Vector3 position, out _, out _);
            Vector3 screen = cam.WorldToScreenPoint(handTranform.TransformPoint(position));
            float distance = Mathf.Abs(screen.x - screenX);
            if (screen.z > 0f && distance < bestDistance) { bestDistance = distance; best = i; }
        }
        return best;
    }

    /// Takes a card out of the hand WITHOUT sending it anywhere. The caller decides where it goes
    /// (back to the deck for a swap, into the tower for a placement).
    public CardInstance RemoveCard(int index)
    {
        if (index < 0 || index >= Count) return null;

        HandCardView view = cardsInHand[index];
        CardInstance card = view != null ? view.Card : null;

        cardsInHand.RemoveAt(index);
        dataInHand.RemoveAt(index);
        if (view != null) Destroy(view.gameObject);

        if (card != null) ForgetCard(card.Id);
        AnnounceHandChanged();
        return card;
    }

    public CardInstance RemoveCardById(int cardId)
    {
        return RemoveCard(FindIndexOfCard(cardId));
    }

    /// Undo: a card that was placed in the tower comes back to the hand, selected again.
    public void ReturnCardToHand(CardInstance card)
    {
        if (card == null || FindIndexOfCard(card.Id) >= 0) return;

        AddCardToHand(card);
        if (FindIndexOfCard(card.Id) < 0) return;       // the hand could not show it

        selectedCardIds.Add(card.Id);
        AnnounceSelectionChanged();
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
        ForgetSelectionAndHover();
        AnnounceHandChanged();
    }

    // ---------- Layout (your original fan layout, now smoothed) ----------

    // Where the ROOT of card number i wants to be (the Visual child just rides along).
    // The hand lives in camera space: +x right, +y up, +z AWAY from the camera (so closer = minus z).
    void GetHandTarget(int i, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        int cardId = cardsInHand[i].CardId;
        bool isSelected = IsSelected(cardId);
        bool isLockedIn = IsLockedIn;
        bool isWaiting = isLockedIn && !isSelected;

        // After Play Hand the committed cards and the waiting cards are two separate rows,
        // each centred on its own. Before that the whole hand is one row.
        GetPlaceInRow(i, out int place, out int rowCount);
        float fromMiddle = place - (rowCount - 1) / 2f;
        float spacing = isWaiting ? cardSpacing * waitingSpacing : cardSpacing;

        rotation = Quaternion.Euler(0, 180, fanSpread * fromMiddle);

        float x = spacing * fromMiddle + GetHoverGap(i);
        float y = verticalSpacing * fromMiddle;
        float z = deepSpacing * fromMiddle;
        float size = handCardScale;

        if (isSelected)
        {
            y += isLockedIn ? lockedInLift : selectedLift;
            z -= selectedForward;                           // in front of the other cards
            size *= selectedScale;
        }
        else if (isLockedIn)
        {
            y -= waitingDrop;                               // the rest of the hand waits lower down
            size *= waitingScale;
        }

        if (cardId == hoveredCardId && IsInteractable(cardId) && !IsDragging) z -= hoverForward;

        position = new Vector3(x, y, z);
        scale = Vector3.one * size;
    }

    bool IsLockedIn => flow != null && flow.IsReady && flow.IsHandLockedIn;

    // Which row a card is in: before Play Hand there is one row; after it, committed and waiting cards
    // each have their own. Cards keep their hand order inside their row.
    bool IsInSameRow(int a, int b)
    {
        if (!IsLockedIn) return true;
        return IsSelected(cardsInHand[a].CardId) == IsSelected(cardsInHand[b].CardId);
    }

    // The card's place in its row (0 = leftmost) and how many cards that row has
    void GetPlaceInRow(int i, out int place, out int rowCount)
    {
        place = 0;
        rowCount = 0;
        for (int other = 0; other < cardsInHand.Count; other++)
        {
            if (!IsInSameRow(i, other)) continue;
            if (other < i) place++;
            rowCount++;
        }
    }

    // The hovered card's neighbours in the same row make room for it: the nearest the most,
    // nothing past Hover Gap Cards
    float GetHoverGap(int i)
    {
        int hovered = HoveredIndex;
        if (hovered < 0 || i == hovered || IsDragging || !IsInSameRow(i, hovered)) return 0f;

        GetPlaceInRow(i, out int place, out _);
        GetPlaceInRow(hovered, out int hoveredPlace, out _);
        int distance = Mathf.Abs(place - hoveredPlace);
        float strength = 1f - (distance - 1) / Mathf.Max(1f, hoverGapCards);
        return strength <= 0f ? 0f : Mathf.Sign(place - hoveredPlace) * hoverGap * strength;
    }

    // The waiting cards fade darker while the committed cards are played, and back afterwards
    float GetDimTarget(int i)
    {
        return IsLockedIn && !IsSelected(cardsInHand[i].CardId) ? 1f : 0f;
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

            HandCardView view = cardsInHand[i];
            view.SetDim(Mathf.MoveTowards(view.DimAmount, GetDimTarget(i), t), waitingBrightness);
        }
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