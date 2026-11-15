using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// House-of-cards builder.
/// Card prefab = a root object with Rigidbody + BoxCollider + Card script (scale 1,1,1).
/// The 3D model of each card comes from its CardData and is fitted automatically.
///
///   TENT  = a LEFT card + a RIGHT card leaning on each other (/\)
///   FLOOR = a flat card laid on top of two neighbouring tents
///   Next level = tents standing on the floor cards, and so on up the pyramid.
///
/// The conductor (GameFlowController) says when placing is allowed and which card is held.
/// A click on the table or on a snap point is a command: it goes to the conductor, which asks
/// this script to do the placing. The slot rules live in TowerSlots; the HUD is controlled by GameUI.
public class TowerBuilder : MonoBehaviour
{
    [Header("Setup")]
    [RequiredField] public Card cardPrefab;
    public CardModelSettings cardModel = new CardModelSettings();
    public float groundY = 0f;
    [Tooltip("The first card of a tower can only be placed on something in this layer (the table). Keeps players from starting a tower off the table.")]
    public LayerMask tableLayerMask = 1 << 7;   // "Table" layer

    [Header("References (auto-found if empty)")]
    public GameFlowController flow;

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

    const int LEFT = TowerSlots.Left, RIGHT = TowerSlots.Right, FLOOR = TowerSlots.Floor;

    struct Slot
    {
        public (int, int, int) key;   // (kind, level, index)
        public Vector3 pos;
        public Quaternion rot;
    }

    // One card placed during the current hand (Undo takes these back, newest first)
    struct Placement
    {
        public (int, int, int) key;
        public CardInstance card;
    }

    enum State { WaitingFirstClick, Choosing, Placing }
    State state = State.WaitingFirstClick;

    readonly Dictionary<(int, int, int), Card> cards = new Dictionary<(int, int, int), Card>();
    readonly List<Slot> slots = new List<Slot>();
    readonly List<Placement> placementsThisHand = new List<Placement>();
    int bestSlot = -1;

    CardInstance heldCard;            // the committed card the player picked up to place
    Vector3? clickedTablePoint;       // where the table was clicked (only while that click is being handled)
    Card ghost;
    Camera cam;

    // Geometry (computed from prefab + tilt)
    Vector3 origin;                   // x = centre of tent 0, y = ground, z = depth of the structure
    float L, W, T, sin, cos, S, levelH;

    // ---------- Public info (used by GameUI / OrbitCamera) ----------

    // True while the conductor has placement switched on (the Build phase)
    public bool CanAct => placementEnabled;
    public int CardsPlacedThisHand => placementsThisHand.Count;
    public bool IsHoldingCard => heldCard != null;

    bool placementEnabled;

    /// What the last shake did. Made once per shake by ResolveShake; the Pot is updated from it.
    public ShakeResult LastShakeResult { get; private set; }

    // Every card that broke since the last ShakeResult. A set, so a card can never count twice.
    readonly HashSet<int> brokenCardIds = new HashSet<int>();
    bool isShaking;
    bool shakeTimedOut;
    int shakeCount;
    float queuedPushSpeed;            // debug cheat: shove the tower when the next shake starts

    public string HintText
    {
        get
        {
            if (!CanAct) return "";
            switch (state)
            {
                case State.WaitingFirstClick: return "Click the table to start the tower";
                case State.Placing: return "Click a snap point to place. Right-click: put the card back. Orbit camera: A / D.";
                default: return "Pick one of your raised cards to place it. Right-click: undo the last card.";
            }
        }
    }

    // --- Read by GameUI ---
    public bool IsWaitingFirstClick => state == State.WaitingFirstClick;
    public bool ShowSlots => state == State.Placing && CanAct;   // show snap markers?
    public int SlotCount => slots.Count;
    public int BestSlotIndex => bestSlot;
    public Vector3 GetSlotPosition(int i) => slots[i].pos;

    // The mass a card really gets in the tower (its own mass x the Mass Multiplier)
    public float GetCardMass(CardData data) => Mathf.Max(0.001f, data.mass * cardModel.massMultiplier);

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

    /// Building starts: lock every card in place and start a fresh list of this hand's placements.
    public void EnablePlacement()
    {
        placementsThisHand.Clear();
        placementEnabled = true;

        if (freezeWhenTurnBegins) SetCardsFrozen(true);
    }

    /// Building is over: drop the card the player was holding.
    public void DisablePlacement()
    {
        placementEnabled = false;
        DropHeldCard();
    }

    /// Can this many cards all be placed, in some order? Logic only, nothing is moved (Master Plan 3.2).
    public bool CanFit(int cardCount)
    {
        return ReadSlotRules().CanFit(cardCount);
    }

    /// Is there at least one spot where a card may go?
    public bool HasLegalPlacement()
    {
        return ReadSlotRules().HasOpenSlot();
    }

    TowerSlots ReadSlotRules()
    {
        return new TowerSlots(cards.Keys, maxBaseTents);
    }

    /// The player picked up a committed card: show it as the preview that follows the mouse.
    public void HoldCard(CardInstance card)
    {
        if (!CanAct || card == null) return;

        heldCard = card;
        AudioManager.SFX(SoundType.SelectCard);

        // Before the tower exists there are no snap points: the next click on the table places the card
        if (state == State.WaitingFirstClick) return;

        ShowGhost(card.Data);
        state = State.Placing;
    }

    /// Places the held card where the player just clicked (the table for the first card, otherwise
    /// the snapped slot). Returns the card that was placed, or null if nothing happened.
    public CardInstance PlaceHeldCard()
    {
        if (!CanAct || heldCard == null) return null;
        if (!TryFindClickedSlot(out (int, int, int) key)) return null;

        CardInstance placed = heldCard;
        PlaceCard(key, placed);
        PlaceCardSound();
        heldCard = null;
        ClearGhost();
        state = State.Choosing;
        return placed;
    }

    // Which slot did the click choose? The first card of a tower goes where the table was clicked.
    bool TryFindClickedSlot(out (int, int, int) key)
    {
        key = (LEFT, 0, 0);

        if (state == State.WaitingFirstClick)
        {
            if (!clickedTablePoint.HasValue) return false;
            origin = new Vector3(clickedTablePoint.Value.x, groundY, clickedTablePoint.Value.z);
            return true;
        }

        if (state != State.Placing || !CanPlace()) return false;
        key = slots[bestSlot].key;
        return true;
    }

    /// Undo: takes this hand's newest placement out of the tower. Cards from earlier hands are
    /// never touched. Returns the card that came out, or null if there is nothing to undo.
    public CardInstance UndoLastPlacement()
    {
        if (!CanAct || placementsThisHand.Count == 0) return null;

        Placement last = placementsThisHand[placementsThisHand.Count - 1];
        placementsThisHand.RemoveAt(placementsThisHand.Count - 1);

        if (cards.TryGetValue(last.key, out Card towerCard) && towerCard != null) Destroy(towerCard.gameObject);
        cards.Remove(last.key);

        DropHeldCard();
        AudioManager.SFX(SoundType.UnselectCard);
        if (cards.Count == 0) ResetTower();             // that was the only card: back to "click the table"
        return last.card;
    }

    /// The tower card that was made from this deck card (null = not in the tower).
    public Card FindTowerCard(int cardId)
    {
        foreach (Card c in cards.Values)
            if (c != null && c.CardId == cardId) return c;
        return null;
    }

    /// Removes every card and lets the player start a new tower with a click on the table.
    /// Sweeping never counts as a break.
    public void SweepTower()
    {
        isShaking = false;
        queuedPushSpeed = 0f;
        brokenCardIds.Clear();
        ResetTower();
    }

    /// Debug cheat: shove every card sideways. Outside a shake the push waits for the next shake,
    /// so the fall is always counted by a ShakeResult.
    public void PushTowerOver(float pushSpeed)
    {
        if (!isShaking)
        {
            queuedPushSpeed = pushSpeed;
            return;
        }

        foreach (var c in cards.Values)
        {
            if (c == null) continue;
            c.Unfreeze();
            c.GetComponent<Rigidbody>().AddForce(Vector3.right * pushSpeed, ForceMode.VelocityChange);
        }
    }

    // ---------- The shake (Master Plan 3.3) ----------

    /// The whole shake: release physics, wait until the tower is quiet (never forever), work out
    /// what broke and what still stands, then freeze the survivors. Leaves ONE result in
    /// LastShakeResult. After that nothing may change the payout.
    public IEnumerator ResolveShake(GameConfig config)
    {
        isShaking = true;
        ReleasePhysics();
        ApplyQueuedPush();

        yield return WaitUntilTowerIsQuiet(config);

        BreakFallenCards();                 // out of their slot = broken, counted once
        BreakUnsupportedCards();            // floating above a gap = broken too
        FreezeTower();                      // survivors stay where they are, no snapping back
        LastShakeResult = BuildShakeResult();
        isShaking = false;

        if (LastShakeResult.Collapsed) ResetTower();    // clear the debris; the table is free again
    }

    void ReleasePhysics()
    {
        if (physicsOnEndTurn) SetCardsFrozen(false);
    }

    void FreezeTower()
    {
        SetCardsFrozen(true);
    }

    void ApplyQueuedPush()
    {
        if (queuedPushSpeed <= 0f) return;

        float pushSpeed = queuedPushSpeed;
        queuedPushSpeed = 0f;
        PushTowerOver(pushSpeed);
    }

    // Time is counted in physics steps, so a paused game does not use up the window.
    // Ends when the tower has been still for the quiet time, or at the maximum time.
    IEnumerator WaitUntilTowerIsQuiet(GameConfig config)
    {
        var nextPhysicsStep = new WaitForFixedUpdate();
        float elapsed = 0f;
        float quietFor = 0f;
        shakeTimedOut = false;

        while (true)
        {
            yield return nextPhysicsStep;
            elapsed += Time.fixedDeltaTime;
            quietFor = IsTowerQuiet(config) ? quietFor + Time.fixedDeltaTime : 0f;

            if (elapsed >= config.shakeMinSeconds && quietFor >= config.shakeQuietSeconds) yield break;

            if (elapsed >= config.shakeMaxSeconds)
            {
                shakeTimedOut = true;
                Debug.LogWarning($"TowerBuilder: the tower was still moving after {config.shakeMaxSeconds} s. The shake was ended by the time limit.", this);
                yield break;
            }
        }
    }

    bool IsTowerQuiet(GameConfig config)
    {
        foreach (Card c in cards.Values)
            if (c != null && c.IsMoving(config.shakeSpeedLimit, config.shakeSpinLimit)) return false;
        return true;
    }

    // A card knocked out of its slot stays registered under that slot's key, so the spot looks
    // empty but refuses new cards. Break anything that is no longer where it was placed:
    // Card.Break raises Broken, and HandleCardBroken counts it and frees the slot.
    void BreakFallenCards()
    {
        var fallen = new List<Card>();
        foreach (var pair in cards)
        {
            Card c = pair.Value;
            if (c != null && HasLeftItsSlot(pair.Key, c)) fallen.Add(c);
        }

        foreach (Card c in fallen) c.Break();   // modifies "cards", so never break while iterating it
    }

    bool HasLeftItsSlot((int, int, int) key, Card c)
    {
        GetPose(key.Item1, key.Item2, key.Item3, out Vector3 pos, out Quaternion rot);
        return Vector3.Distance(c.transform.position, pos) > fallenDistance ||
               Quaternion.Angle(c.transform.rotation, rot) > fallenAngle;
    }

    // A card whose support is gone (the tent or floor under it broke) is not a survivor
    void BreakUnsupportedCards()
    {
        ForgetDestroyedCards();

        foreach (var key in ReadSlotRules().FindUnsupportedCards())
            if (cards.TryGetValue(key, out Card c) && c != null) c.Break();
    }

    ShakeResult BuildShakeResult()
    {
        ForgetDestroyedCards();
        TowerSlots survivors = ReadSlotRules();

        shakeCount++;
        var result = new ShakeResult(
            shakeCount,
            new List<int>(brokenCardIds),
            survivors.CountStandingLevels(),
            !survivors.HasBaseCard(),               // collapsed = no base card left standing
            shakeTimedOut);

        brokenCardIds.Clear();
        return result;
    }

    // If every ground-level card is gone (broken or destroyed) there is nothing left to build on.
    // Without this the game gets stuck: no snap points exist and the "first click" never comes back.
    // During a shake this waits: ResolveShake decides what collapsed, once, at the end.
    void CheckTowerStillExists()
    {
        if (state == State.WaitingFirstClick || isShaking) return;

        ForgetDestroyedCards();
        if (!ReadSlotRules().HasBaseCard()) ResetTower();
    }

    void ForgetDestroyedCards()
    {
        List<(int, int, int)> dead = null;
        foreach (var pair in cards)
        {
            if (pair.Value != null) continue;
            if (dead == null) dead = new List<(int, int, int)>();
            dead.Add(pair.Key);
        }
        if (dead != null)
            foreach (var key in dead) cards.Remove(key);
    }

    // The tower is gone: clear what is left and let the player start a new one with a click on the ground
    void ResetTower()
    {
        foreach (var c in cards.Values)
            if (c != null) Destroy(c.gameObject);

        cards.Clear();
        slots.Clear();
        placementsThisHand.Clear();
        bestSlot = -1;

        heldCard = null;
        ClearGhost();

        state = State.WaitingFirstClick;
        TowerReset?.Invoke();
    }

    // A tower card broke: count it (once) for the next ShakeResult and free its slot
    void HandleCardBroken(Card broken)
    {
        (int, int, int)? foundKey = null;
        foreach (var pair in cards)
        {
            if (pair.Value == broken) { foundKey = pair.Key; break; }
        }
        if (!foundKey.HasValue) return;     // not a tower card (or already counted)

        cards.Remove(foundKey.Value);
        brokenCardIds.Add(broken.CardId);
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

    // Which slots are free right now (the rules are in TowerSlots; this adds where each slot is)
    void BuildSlots()
    {
        slots.Clear();

        foreach (var key in ReadSlotRules().FindOpenSlots())
        {
            GetPose(key.kind, key.level, key.index, out Vector3 pos, out Quaternion rot);
            slots.Add(new Slot { key = key, pos = pos, rot = rot });
        }
    }

    // ---------- Update ----------

    // Per-frame mouse info (filled by ReadMouse, used by the handlers below)
    Vector2 mouseScreenPos;
    Ray mouseRay;
    bool mouseOverUI;

    void Update()
    {
        CheckTowerStillExists();

        var mouse = Mouse.current;
        if (mouse == null) return;

        ReadMouse(mouse);

        // The conductor says "not now" (every phase except Build)
        if (!CanAct)
        {
            DropHeldCard();
            return;
        }

        switch (state)
        {
            case State.WaitingFirstClick:
                HandleFirstClick(mouse);
                break;

            case State.Choosing:
                HandleUndoClick(mouse);
                break;

            case State.Placing:
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
        if (!Physics.Raycast(mouseRay, out RaycastHit hit, 1000f, tableLayerMask)) return;

        clickedTablePoint = hit.point;
        RequestPlaceHeldCard();
        clickedTablePoint = null;
    }

    // ----- State: Choosing (no card held) -----

    void HandleUndoClick(Mouse mouse)
    {
        if (mouse.rightButton.wasPressedThisFrame && !mouseOverUI && flow != null) flow.RequestUndo();
    }

    // ----- State: Placing -----

    void HandlePlacing(Mouse mouse)
    {
        if (ghost == null) return;

        BuildSlots();                 // which slots are free right now
        bestSlot = FindBestSlot();    // which one is closest to the mouse
        MoveGhost();                  // show the preview card

        if (mouse.leftButton.wasPressedThisFrame && CanPlace())
            RequestPlaceHeldCard();
        else if (mouse.rightButton.wasPressedThisFrame)
            DropHeldCard();
    }

    // A placing click is a command: the conductor checks the phase, then calls PlaceHeldCard()
    void RequestPlaceHeldCard()
    {
        if (flow != null) flow.RequestPlaceHeldCard();
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

    // Put the held card back (it stays in the hand, still waiting to be placed)
    void DropHeldCard()
    {
        heldCard = null;
        ClearGhost();
        if (state == State.Placing) state = State.Choosing;
    }

    void ShowGhost(CardData data)
    {
        ClearGhost();
        ghost = Instantiate(cardPrefab, FocusPoint, Quaternion.identity);
        ghost.Setup(data, cardModel);
        ghost.SetGhost(false);
    }

    void ClearGhost()
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
    }

    // ---------- Cards ----------

    void PlaceCard((int, int, int) key, CardInstance card)
    {
        GetPose(key.Item1, key.Item2, key.Item3, out Vector3 pos, out Quaternion rot);
        Card c = Instantiate(cardPrefab, pos, rot, transform);
        c.name = $"TowerCard #{card.Id} ({card.Data.cardName})";
        c.CardId = card.Id;
        c.Setup(card.Data, cardModel);
        c.SetPlaced();
        cards[key] = c;
        placementsThisHand.Add(new Placement { key = key, card = card });
    }

    public void PlaceCardSound()
    {
        AudioManager.SFX(SoundType.PlayCard);
    }
}