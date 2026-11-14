using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// House-of-cards builder.
/// Card prefab = a root object with Rigidbody + BoxCollider + Card script (scale 1,1,1).
/// The 3D model of each card comes from its CardData and is fitted automatically.
///
///   TENT  = a LEFT card + a RIGHT card leaning on each other (/\)
///   FLOOR = a flat card laid on top of two neighbouring tents
///   Next level = tents standing on the floor cards, and so on up the pyramid.
///
/// The conductor (GameFlowController) says when placing is allowed; the HUD is controlled by GameUI.
public class TowerBuilder : MonoBehaviour
{
    [Header("Setup")]
    public Card cardPrefab;
    public CardModelSettings cardModel = new CardModelSettings();
    public float groundY = 0f;
    [Tooltip("The first card of a tower can only be placed on something in this layer (the table). Keeps players from starting a tower off the table.")]
    public LayerMask tableLayerMask = 1 << 7;   // "Table" layer

    [Header("References (auto-found if empty)")]
    public GameFlowController flow;
    public HandManager handManager;

    [Header("House of cards")]
    [Range(5f, 45f)] public float tiltAngle = 20f;   // how far cards lean from vertical
    public int maxBaseTents = 4;                     // tents on the bottom row (4 = 4 levels high)

    [Header("Fallen cards")]
    [Tooltip("A placed card counts as fallen once it has slipped this far from the slot it was placed in (world units). Cards settle about 0.05, so keep this well above that.")]
    public float fallenDistance = 0.35f;
    [Tooltip("...or once it has tipped this many degrees away from how it was placed. Cards settle about 7 degrees.")]
    public float fallenAngle = 35f;

    [Header("Physics")]
    [Tooltip("During the shake, unfreeze all cards so gravity and collisions apply.")]
    public bool physicsOnEndTurn = true;
    [Tooltip("When building starts again, freeze all cards where they are.")]
    public bool freezeWhenTurnBegins = true;

    [Header("Snapping")]
    public float snapRadiusPixels = 90f;             // mouse must be this close to a slot (screen pixels)
    public float snapSpeed = 25f;

    public bool isGameOver;

    const int LEFT = 0, RIGHT = 1, FLOOR = 2;

    struct Slot
    {
        public (int, int, int) key;   // (kind, level, index)
        public Vector3 pos;
        public Quaternion rot;
    }

    enum State { WaitingFirstClick, Choosing, Placing }
    State state = State.WaitingFirstClick;

    readonly Dictionary<(int, int, int), Card> cards = new Dictionary<(int, int, int), Card>();
    readonly List<Slot> slots = new List<Slot>();
    int bestSlot = -1;

    Card ghost;
    Camera cam;

    // Geometry (computed from prefab + tilt)
    Vector3 origin;                   // x = centre of tent 0, y = ground, z = depth of the structure
    float L, W, T, sin, cos, S, levelH;

    // ---------- Public info (used by GameUI / OrbitCamera) ----------

    // True while the conductor has placement switched on and this hand still has cards to place
    public bool CanAct => CanPlaceMoreCards;
    public bool CanPlaceMoreCards => placementEnabled && CardsPlacedThisHand < placementLimit;
    public int CardsPlacedThisHand { get; private set; }

    bool placementEnabled;
    int placementLimit;

    public string HintText
    {
        get
        {
            if (!CanAct) return "";
            switch (state)
            {
                case State.WaitingFirstClick: return "Pick a card from your hand, then click the ground to start the tower";
                case State.Placing: return "Click a snap point to place. Right-click: cancel. Orbit camera: A / D.";
                default: return "Choose a card from your hand (click it or press 1-" + HandCount + ")";
            }
        }
    }

    // --- Read by GameUI ---
    public IReadOnlyList<CardData> Hand => handManager != null ? handManager.CardsInHandData : null;
    public int SelectedIndex => handManager != null ? handManager.SelectedIndex : -1;
    int HandCount => handManager != null ? Mathf.Min(9, handManager.Count) : 0;
    public bool IsWaitingFirstClick => state == State.WaitingFirstClick;
    public bool ShowSlots => state == State.Placing && CanAct;   // show snap markers?
    public int SlotCount => slots.Count;
    public int BestSlotIndex => bestSlot;
    public Vector3 GetSlotPosition(int i) => slots[i].pos;

    // The mass a card really gets in the tower (its own mass x the Mass Multiplier)
    public float GetCardMass(CardData data) => Mathf.Max(0.001f, data.mass * cardModel.massMultiplier);

    // Raised every time a card is placed in the tower (ScoreManager listens)
    public event System.Action<CardData, Card> CardPlaced;

    // The point the camera looks at when there is no tower (game start, or after the tower is gone)
    public Vector3 StartFocusPoint => new Vector3(0, groundY + 1f, 0);

    // Middle of the tower, or the start point when there are no cards
    public Vector3 FocusPoint
    {
        get
        {
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            bool any = false;

            foreach (var c in cards.Values)
            {
                if (c == null) continue;            // destroyed card
                min = Vector3.Min(min, c.transform.position);
                max = Vector3.Max(max, c.transform.position);
                any = true;
            }

            return any ? (min + max) * 0.5f : StartFocusPoint;
        }
    }

    // Raised when the whole tower is gone and the player has to start over (OrbitCamera listens)
    public event System.Action TowerReset;

    // ---------- Lifecycle ----------

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (handManager == null) handManager = FindFirstObjectByType<HandManager>();

        Card.Broken += HandleCardBroken;
    }

    void OnDestroy()
    {
        Card.Broken -= HandleCardBroken;
    }

    void Start()
    {
        cam = Camera.main;

        T = cardModel.thickness;
        L = cardModel.length;
        W = cardModel.width;
        sin = Mathf.Sin(tiltAngle * Mathf.Deg2Rad);
        cos = Mathf.Cos(tiltAngle * Mathf.Deg2Rad);
        S = 2f * L * sin;             // distance between neighbouring tents (bases touch)
        levelH = L * cos + T;         // height of one level (tent + floor card)
    }

    // ---------- Jobs the conductor asks for ----------

    /// Building starts: lock every card in place and allow up to maxCards placements.
    public void EnablePlacement(int maxCards)
    {
        CardsPlacedThisHand = 0;
        placementLimit = maxCards;
        placementEnabled = true;

        if (freezeWhenTurnBegins) SetCardsFrozen(true);
        BreakFallenCards();
    }

    /// Building is over: drop the card the player was holding.
    public void DisablePlacement()
    {
        placementEnabled = false;
        if (state == State.Placing) CancelSelection();
    }

    /// The shake: let gravity and collisions act on the tower.
    public void ReleasePhysics()
    {
        if (physicsOnEndTurn) SetCardsFrozen(false);
    }

    /// The shake is over: lock every card where it is now.
    public void FreezeTower()
    {
        SetCardsFrozen(true);
    }

    /// Removes every card and lets the player start a new tower with a click on the table.
    public void SweepTower()
    {
        ResetTower();
    }

    /// Debug cheat: unfreeze the tower and shove every card sideways.
    public void PushTowerOver(float pushSpeed)
    {
        foreach (var c in cards.Values)
        {
            if (c == null) continue;
            c.Unfreeze();
            c.GetComponent<Rigidbody>().AddForce(Vector3.right * pushSpeed, ForceMode.VelocityChange);
        }
    }

    // A card knocked out of its slot by the end-of-turn physics stays registered under that slot's
    // key, so the spot looks empty but refuses new cards for the rest of the game. Once the dust has
    // settled, break anything that is no longer where it was placed - Card.Break raises Broken, and
    // HandleCardBroken then drops the key so the slot opens up again.
    void BreakFallenCards()
    {
        if (state == State.WaitingFirstClick) return;

        List<Card> fallen = null;
        foreach (var pair in cards)
        {
            Card c = pair.Value;
            if (c == null) continue;

            GetPose(pair.Key.Item1, pair.Key.Item2, pair.Key.Item3, out Vector3 pos, out Quaternion rot);
            if (Vector3.Distance(c.transform.position, pos) <= fallenDistance &&
                Quaternion.Angle(c.transform.rotation, rot) <= fallenAngle) continue;

            if (fallen == null) fallen = new List<Card>();
            fallen.Add(c);
        }

        if (fallen == null) return;
        foreach (Card c in fallen) c.Break();   // modifies "cards", so never break while iterating it
    }

    // If every ground-level card is gone (broken or destroyed) there is nothing left to build on.
    // Without this the game gets stuck: no snap points exist and the "first click" never comes back.
    void CheckTowerStillExists()
    {
        if (state == State.WaitingFirstClick) return;

        // forget cards that were destroyed
        List<(int, int, int)> dead = null;
        foreach (var pair in cards)
        {
            if (pair.Value != null) continue;
            if (dead == null) dead = new List<(int, int, int)>();
            dead.Add(pair.Key);
        }
        if (dead != null)
            foreach (var key in dead) cards.Remove(key);

        if (!HasBaseCard()) ResetTower();
    }

    // Is there at least one card standing on the ground (a tent card at level 0)?
    bool HasBaseCard()
    {
        foreach (var key in cards.Keys)
            if (key.Item1 != FLOOR && key.Item2 == 0) return true;
        return false;
    }

    // The tower is gone: clear what is left and let the player start a new one with a click on the ground
    void ResetTower()
    {
        foreach (var c in cards.Values)
            if (c != null) Destroy(c.gameObject);

        cards.Clear();
        slots.Clear();
        bestSlot = -1;

        ClearGhost();
        if (handManager != null) handManager.ClearSelection();

        state = State.WaitingFirstClick;
        TowerReset?.Invoke();
    }

    // A card broke (hit the ground too hard): forget it so its slot becomes free again
    void HandleCardBroken(Card broken)
    {
        (int, int, int)? foundKey = null;
        foreach (var pair in cards)
        {
            if (pair.Value == broken) { foundKey = pair.Key; break; }
        }

        if (foundKey.HasValue) cards.Remove(foundKey.Value);
    }

    // true = freeze all placed cards, false = apply physics to them
    void SetCardsFrozen(bool frozen)
    {
        foreach (var c in cards.Values)
        {
            if (c == null) continue;            // destroyed card
            if (frozen) c.Freeze();
            else c.Unfreeze();
        }
    }

    // ---------- Geometry ----------

    void GetPose(int kind, int level, int idx, out Vector3 pos, out Quaternion rot)
    {
        float cx = origin.x + (idx + 0.5f * level) * S;   // tent centre x
        float by = origin.y + level * levelH;             // level base height

        switch (kind)
        {
            case LEFT:
                pos = new Vector3(cx - 0.5f * L * sin, by + 0.5f * L * cos, origin.z);
                // Both halves of a tent lean in the same plane, so without this the left card turns
                // its back to the camera while the right one shows its face. Spinning it 180 degrees
                // about its own long axis (applied before the lean) shows the face without changing
                // how it leans or where it sits.
                rot = Quaternion.Euler(0, 0, -tiltAngle) * Quaternion.Euler(0, 180f, 0);
                break;
            case RIGHT:
                pos = new Vector3(cx + 0.5f * L * sin, by + 0.5f * L * cos, origin.z);
                rot = Quaternion.Euler(0, 0, tiltAngle);
                break;
            default: // FLOOR: lies flat across tent idx and tent idx+1
                pos = new Vector3(cx + 0.5f * S, by + L * cos + 0.5f * T, origin.z);
                rot = Quaternion.Euler(0, 0, 90);
                break;
        }
    }

    // ---------- Slots ----------

    bool Has(int k, int l, int i) => cards.ContainsKey((k, l, i));
    bool TentComplete(int l, int i) => Has(LEFT, l, i) && Has(RIGHT, l, i);

    void BuildSlots()
    {
        slots.Clear();
        var seen = new HashSet<(int, int, int)>();

        // tents on the bottom row
        var baseTents = new HashSet<int>();
        foreach (var key in cards.Keys)
            if (key.Item1 != FLOOR && key.Item2 == 0) baseTents.Add(key.Item3);

        foreach (var key in cards.Keys)
        {
            int k = key.Item1, l = key.Item2, i = key.Item3;

            if (k != FLOOR)
            {
                // finish this tent
                TryAdd(seen, k == LEFT ? RIGHT : LEFT, l, i);

                // flat card across two complete neighbouring tents
                if (TentComplete(l, i))
                {
                    if (TentComplete(l, i + 1)) TryAdd(seen, FLOOR, l, i);
                    if (TentComplete(l, i - 1)) TryAdd(seen, FLOOR, l, i - 1);
                }

                // start a new tent next to it on the ground row
                if (l == 0)
                {
                    foreach (int n in new[] { i - 1, i + 1 })
                    {
                        if (baseTents.Contains(n) || baseTents.Count < maxBaseTents)
                        {
                            TryAdd(seen, LEFT, 0, n);
                            TryAdd(seen, RIGHT, 0, n);
                        }
                    }
                }
            }
            else
            {
                // a floor card lets you build a tent on top of it
                TryAdd(seen, LEFT, l + 1, i);
                TryAdd(seen, RIGHT, l + 1, i);
            }
        }
    }

    void TryAdd(HashSet<(int, int, int)> seen, int k, int l, int i)
    {
        var key = (k, l, i);
        if (cards.ContainsKey(key) || !seen.Add(key)) return;
        GetPose(k, l, i, out Vector3 pos, out Quaternion rot);
        slots.Add(new Slot { key = key, pos = pos, rot = rot });
    }

    // ---------- Update ----------

    // Per-frame mouse info (filled by ReadMouse, used by the handlers below)
    Vector2 mouseScreenPos;
    Ray mouseRay;
    bool mouseOverUI;

    void Update()
    {
        CheckTowerStillExists();
        CheckGameOver();

        var mouse = Mouse.current;
        if (mouse == null) return;

        ReadMouse(mouse);

        // The conductor says "not now" (drawing, scoring, shaking, run over)
        if (!CanAct)
        {
            if (state == State.Placing) CancelSelection();
            return;
        }

        switch (state)
        {
            case State.WaitingFirstClick:
                HandleFirstClick(mouse);
                break;

            case State.Choosing:
                HandleHandKeys();
                break;

            case State.Placing:
                HandleHandKeys();
                HandlePlacing(mouse);
                break;
        }
    }

    // Reads the mouse once per frame
    void ReadMouse(Mouse mouse)
    {
        mouseScreenPos = mouse.position.ReadValue();
        mouseOverUI = IsPointerOverBlockingUI(mouseScreenPos);
        mouseRay = cam.ScreenPointToRay(mouseScreenPos);
    }

    // True when the mouse is over something that should block placing a card:
    //  - a UI element (button, panel...)
    //  - a card in the hand
    // Ground, tower cards and other 3D objects do NOT block, even if a Physics Raycaster is on the camera.
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    bool IsPointerOverBlockingUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        var pointer = new PointerEventData(EventSystem.current) { position = screenPos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointer, uiHits);

        foreach (var hit in uiHits)
        {
            if (hit.module is GraphicRaycaster) return true;                          // UI element
            if (hit.gameObject.GetComponentInParent<HandCardView>() != null) return true;   // hand card
            if (hit.gameObject.GetComponentInParent<DiscardZone>() != null) return true;    // discard zone
        }
        return false;
    }

    // ----- State: WaitingFirstClick -----

    void HandleFirstClick(Mouse mouse)
    {
        if (!mouse.leftButton.wasPressedThisFrame || mouseOverUI) return;

        // Only start a tower where the click actually lands on the table, not anywhere on the infinite ground plane.
        if (Physics.Raycast(mouseRay, out RaycastHit hit, 1000f, tableLayerMask))
            StartTower(hit.point);
    }

    // Sets the tower origin and places the very first card
    void StartTower(Vector3 clickPoint)
    {
        if (handManager == null || handManager.Count == 0) return;

        // use the card the player picked, or the first card in the hand
        int index = handManager.SelectedIndex >= 0 ? handManager.SelectedIndex : 0;
        CardData data = handManager.GetCard(index);

        origin = new Vector3(clickPoint.x, groundY, clickPoint.z);
        PlaceCard((LEFT, 0, 0), data);
        handManager.PlayCard(index);

        state = State.Choosing;
        ReportCardPlaced();
    }

    // ----- Choosing a card (keys 1..9) -----

    void HandleHandKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        for (int i = 0; i < HandCount; i++)
            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                SelectCard(i);
    }

    // ----- State: Placing -----

    void HandlePlacing(Mouse mouse)
    {
        // the chosen card is gone (it was discarded...): drop the preview
        if (handManager == null || handManager.SelectedIndex < 0)
        {
            CancelSelection();
            return;
        }
        if (ghost == null) return;

        BuildSlots();                 // which slots are free right now
        bestSlot = FindBestSlot();    // which one is closest to the mouse
        MoveGhost();                  // show the preview card

        if (mouse.leftButton.wasPressedThisFrame && CanPlace())
            PlaceSelectedCard();
        else if (mouse.rightButton.wasPressedThisFrame)
            CancelSelection();
    }

    // Slot closest to the mouse (screen space) within snapRadiusPixels, or -1
    int FindBestSlot()
    {
        int best = -1;
        float bestDist = snapRadiusPixels;

        for (int i = 0; i < slots.Count; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(slots[i].pos);
            if (sp.z <= 0) continue;   // behind camera

            float d = Vector2.Distance(new Vector2(sp.x, sp.y), mouseScreenPos);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    // Where the ghost should be: on the snapped slot, or following the mouse
    void GetGhostTarget(out Vector3 pos, out Quaternion rot)
    {
        if (bestSlot >= 0)
        {
            pos = slots[bestSlot].pos;
            rot = slots[bestSlot].rot;
            return;
        }

        var plane = new Plane(Vector3.forward, origin);
        pos = plane.Raycast(mouseRay, out float d) ? mouseRay.GetPoint(d) : mouseRay.GetPoint(10f);
        rot = Quaternion.identity;
    }

    void MoveGhost()
    {
        GetGhostTarget(out Vector3 targetPos, out Quaternion targetRot);

        float t = 1f - Mathf.Exp(-snapSpeed * Time.deltaTime);
        ghost.transform.position = Vector3.Lerp(ghost.transform.position, targetPos, t);
        ghost.transform.rotation = Quaternion.Slerp(ghost.transform.rotation, targetRot, t);
        ghost.SetGhost(bestSlot >= 0);   // green when snapped, red otherwise
    }

    // Add extra rules here (e.g. require a certain card type...)
    bool CanPlace()
    {
        return bestSlot >= 0 && !mouseOverUI;
    }

    void PlaceSelectedCard()
    {
        CardData data = handManager != null ? handManager.SelectedCard : null;
        if (data == null) return;

        PlaceCard(slots[bestSlot].key, data);
        PlaceCardSound();
        ClearGhost();
        handManager.PlaySelectedCard();   // removes it from the hand -> discard pile
        state = State.Choosing;
        ReportCardPlaced();               // the conductor ends the hand at the limit
    }

    void CancelSelection()
    {
        ClearGhost();
        if (handManager != null) handManager.ClearSelection();
        state = State.Choosing;
    }

    void ClearGhost()
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
    }

    // Counted here; the conductor reads CanPlaceMoreCards to know when the hand is done
    void ReportCardPlaced()
    {
        CardsPlacedThisHand++;
    }

    // ---------- Cards ----------

    void PlaceCard((int, int, int) key, CardData data)
    {
        GetPose(key.Item1, key.Item2, key.Item3, out Vector3 pos, out Quaternion rot);
        Card c = Instantiate(cardPrefab, pos, rot, transform);
        c.Setup(data, cardModel);
        c.SetPlaced();
        cards[key] = c;
        CardPlaced?.Invoke(data, c);
    }

    // Called by the conductor (hand card click, HUD button) and by the number keys
    public void SelectCard(int index)
    {
        if (!CanAct || handManager == null) return;

        CardData data = handManager.GetCard(index);
        if (data == null) return;

        handManager.SetSelected(index);

        // Before the tower exists the player just picks which card the first click will place
        if (state == State.WaitingFirstClick) return;

        if (ghost != null) Destroy(ghost.gameObject);
        ghost = Instantiate(cardPrefab, FocusPoint, Quaternion.identity);
        ghost.Setup(data, cardModel);
        ghost.SetGhost(false);
        state = State.Placing;
    }
    public void CheckGameOver()
    {
        if (isGameOver || state == State.WaitingFirstClick) return;

        // Refresh available slots based on the current tower state
        BuildSlots();

        bool noSlotsAvailable = slots.Count == 0;
        bool noCardsInHand = handManager == null || handManager.Count == 0;
        var allCardCount = handManager.GetAllCardCount();
        // Trigger game over if there are no places to build and no cards left to play
        if (noSlotsAvailable || noCardsInHand)
        {
            if (flow != null) flow.RequestEndHand();
            StartCoroutine(DelayForCalSocre());
            if (state == State.Placing) CancelSelection();
        }
    }
    IEnumerator DelayForCalSocre()
    {
        yield return new WaitForSeconds(3f);    
        isGameOver = true;
    }
    public void PlaceCardSound()
    {
        AudioManager.SFX(SoundType.PlayCard);
    }
}