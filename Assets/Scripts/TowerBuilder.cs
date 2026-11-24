using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// One free spot in the tower: which slot it is, and where a card placed there would stand.
public struct OpenSlot
{
    public (int kind, int level, int index) key;
    public Vector3 position;
    public Quaternion rotation;
}

/// The house of cards itself: which cards stand where, the shake, and the geometry.
/// Card prefab = a root object with Rigidbody + BoxCollider + Card script (scale 1,1,1).
/// The 3D model of each card comes from its CardData and is fitted automatically.
///
///   TENT  = a LEFT card + a RIGHT card leaning on each other (/\)
///   FLOOR = a flat card laid on top of two neighbouring tents
///   Next level = tents standing on the floor cards, and so on up the pyramid.
///
/// It never reads the mouse: TowerPlacementInput does that and the conductor (GameFlowController)
/// asks this script to place, undo, shake or sweep. The slot rules live in TowerSlots.
public class TowerBuilder : MonoBehaviour
{
    [Header("Setup")]
    [RequiredField] public Card cardPrefab;
    public CardModelSettings cardModel = new CardModelSettings();
    public float groundY = 0f;
    [Tooltip("Where the tower always stands: the middle of the table (x and z; the height is Ground Y)")]
    public Vector3 towerSpotPosition = new Vector3(-0.0001290226f, 0.003030785f, 8.953639e-05f);
    [Tooltip("Optional: drag an object here and the tower stands on it instead of Tower Spot Position")]
    public Transform towerSpot;

    [Header("House of cards")]
    [Range(5f, 45f)] public float tiltAngle = 20f;
    public int maxBaseTents = 4;

    [Header("Fallen cards")]
    public float fallenDistance = 0.35f;
    public float fallenAngle = 35f;

    [Header("Physics")]
    public bool physicsOnEndTurn = true;
    public bool freezeWhenTurnBegins = true;

    const int LEFT = TowerSlots.Left, RIGHT = TowerSlots.Right;

    // One card placed during the current hand (Undo takes these back, newest first)
    struct Placement
    {
        public (int, int, int) key;
        public CardInstance card;
    }

    readonly Dictionary<(int, int, int), Card> cards = new Dictionary<(int, int, int), Card>();
    readonly List<Placement> placementsThisHand = new List<Placement>();

    // Geometry (computed from prefab + tilt)
    Vector3 origin;                   // x = centre of tent 0, y = ground, z = depth of the structure
    float L, T, sin, cos, S, levelH;

    // Every card that broke since the last ShakeResult. A set, so a card can never count twice.
    readonly HashSet<int> brokenCardIds = new HashSet<int>();
    bool isShaking;
    bool shakeTimedOut;
    int shakeCount;
    float queuedPushSpeed;            // debug cheat: shove the tower when the next shake starts

    // ---------- Public info ----------

    public int CardsPlacedThisHand => placementsThisHand.Count;

    /// No card stands: the next card starts a new tower on the tower spot.
    public bool IsEmpty => cards.Count == 0;

    /// What the last shake did. Made once per shake by ResolveShake; the Pot is updated from it.
    public ShakeResult LastShakeResult { get; private set; }

    // The mass a card really gets in the tower (its own mass x the Mass Multiplier)
    public float GetCardMass(CardData data) => Mathf.Max(0.001f, data.mass * cardModel.massMultiplier);

    // The point the camera looks at when there is no tower (game start, or after the tower is gone)
    public Vector3 StartFocusPoint => TowerSpot + Vector3.up * 1f;

    /// The middle of the table, on the ground: every tower is built around this point.
    public Vector3 TowerSpot
    {
        get
        {
            Vector3 spot = towerSpot != null ? towerSpot.position : towerSpotPosition;
            return new Vector3(spot.x, groundY, spot.z);
        }
    }

    /// Above the middle of the tower, at its highest card (for HUD tags that float over the tower).
    /// The start point when there are no cards.
    public Vector3 TopPoint
    {
        get
        {
            float top = float.MinValue;
            foreach (var c in cards.Values)
                if (c != null) top = Mathf.Max(top, c.transform.position.y);

            Vector3 middle = FocusPoint;
            return top > float.MinValue ? new Vector3(middle.x, top, middle.z) : StartFocusPoint;
        }
    }

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

    /// The whole tower is gone and the player has to start over (OrbitCamera and TowerPlacementInput listen).
    public event System.Action TowerReset;

    // ---------- Lifecycle ----------

    void Awake()
    {
        ComputeGeometry();
        GameEvents.CardBroken += HandleCardBroken;
    }

    void OnDestroy()
    {
        GameEvents.CardBroken -= HandleCardBroken;
    }

    void ComputeGeometry()
    {
        T = cardModel.thickness;
        L = cardModel.length;
        sin = Mathf.Sin(tiltAngle * Mathf.Deg2Rad);
        cos = Mathf.Cos(tiltAngle * Mathf.Deg2Rad);
        S = 2f * L * sin;             // distance between neighbouring tents (bases touch)
        levelH = L * cos + T;         // height of one level (tent + floor card)
    }

    void Update()
    {
        CheckTowerStillExists();
    }

    // ---------- Jobs the conductor asks for ----------

    /// A new hand starts building: lock every card in place and start a fresh list of this hand's placements.
    public void StartHandPlacements()
    {
        placementsThisHand.Clear();
        if (freezeWhenTurnBegins) SetCardsFrozen(true);
    }

    /// Can this many cards all be placed, in some order? Logic only, nothing is moved.
    public bool CanFit(int cardCount)
    {
        return ReadSlotRules().CanFit(cardCount);
    }

    /// Is there at least one spot where a card may go?
    public bool HasLegalPlacement()
    {
        return ReadSlotRules().HasOpenSlot();
    }

    /// Every free slot right now, with where a card placed there would stand. Empty while the tower is empty.
    public List<OpenSlot> FindOpenSlots()
    {
        var open = new List<OpenSlot>();
        if (IsEmpty) return open;

        foreach (var key in ReadSlotRules().FindOpenSlots())
        {
            GetPose(key, out Vector3 pos, out Quaternion rot);
            open.Add(new OpenSlot { key = key, position = pos, rotation = rot });
        }
        return open;
    }

    /// The first card of a tower: the first tent always stands right on the tower spot.
    public void PlaceFirstCard(CardInstance card)
    {
        if (!IsEmpty || card == null) return;

        origin = TowerSpot;
        PlaceCard((LEFT, 0, 0), card);
    }

    /// Any later card: into one of the open slots.
    public void PlaceCard((int kind, int level, int index) key, CardInstance card)
    {
        if (card == null || cards.ContainsKey(key)) return;

        GetPose(key, out Vector3 pos, out Quaternion rot);
        Card c = Instantiate(cardPrefab, pos, rot, transform);
        c.name = $"TowerCard #{card.Id} ({card.Data.cardName})";
        c.CardId = card.Id;
        c.Setup(card.Data, cardModel);
        c.SetPlaced();
        cards[key] = c;
        placementsThisHand.Add(new Placement { key = key, card = card });
        PlaceCardSound();
    }

    /// Undo: takes this hand's newest placement out of the tower. Cards from earlier hands are
    /// never touched. Returns the card that came out, or null if there is nothing to undo.
    public CardInstance UndoLastPlacement()
    {
        if (placementsThisHand.Count == 0) return null;

        Placement last = placementsThisHand[placementsThisHand.Count - 1];
        placementsThisHand.RemoveAt(placementsThisHand.Count - 1);

        if (cards.TryGetValue(last.key, out Card towerCard) && towerCard != null) Destroy(towerCard.gameObject);
        cards.Remove(last.key);

        AudioManager.Instance.PlayOneShotSFX(SoundType.UnselectCard);
        if (IsEmpty) ResetTower();                      // that was the only card: back to "click the table"
        return last.card;
    }

    /// A see-through preview of a card, for the player to aim with. The caller destroys it.
    public Card CreatePreviewCard(CardData data)
    {
        Card preview = Instantiate(cardPrefab, FocusPoint, Quaternion.identity);
        preview.Setup(data, cardModel);
        preview.SetGhost(false);
        return preview;
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

    // ---------- The shake ----------

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
    // Card.Break raises CardBroken, and HandleCardBroken counts it and frees the slot.
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
        GetPose(key, out Vector3 pos, out Quaternion rot);
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

    // ---------- Keeping the tower consistent ----------

    TowerSlots ReadSlotRules()
    {
        return new TowerSlots(cards.Keys, maxBaseTents);
    }

    // If every ground-level card is gone (broken or destroyed) there is nothing left to build on.
    // Without this the game gets stuck: no snap points exist and the "first click" never comes back.
    // During a shake this waits: ResolveShake decides what collapsed, once, at the end.
    void CheckTowerStillExists()
    {
        if (IsEmpty || isShaking) return;

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

    // The tower is gone: clear what is left and let the player start a new one with a click on the table
    void ResetTower()
    {
        foreach (var c in cards.Values)
            if (c != null) Destroy(c.gameObject);

        cards.Clear();
        placementsThisHand.Clear();
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

    void GetPose((int kind, int level, int index) key, out Vector3 pos, out Quaternion rot)
    {
        float cx = origin.x + (key.index + 0.5f * key.level) * S;   // tent centre x
        float by = origin.y + key.level * levelH;                   // level base height

        switch (key.kind)
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

    public void PlaceCardSound()
    {
        AudioManager.Instance.PlayOneShotSFX(SoundType.PlayCard);
    }
}
