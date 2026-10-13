using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TowerBuilder : MonoBehaviour
{
    [Header("Setup")]
    public Card cardPrefab;
    public CardData[] cardTypes;
    public int handSize = 3;
    public float groundY = 0f;

    [Header("House of cards")]
    [Range(5f, 45f)] public float tiltAngle = 20f;   // how far cards lean from vertical
    public int maxBaseTents = 4;                     // tents on the bottom row (4 = 4 levels high)

    [Header("Snapping")]
    public float snapRadiusPixels = 90f;             // mouse must be this close to a slot (screen pixels)
    public float snapSpeed = 25f;

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

    CardData[] hand;
    Card ghost;
    int selected = -1;
    Camera cam;

    // Geometry (computed from prefab + tilt)
    Vector3 origin;                   // x = centre of tent 0, y = ground, z = depth of the structure
    float L, W, T, sin, cos, S, levelH;

    // Used by OrbitCamera
    public Vector3 FocusPoint
    {
        get
        {
            if (cards.Count == 0) return new Vector3(0, groundY + 1f, 0);
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (var c in cards.Values)
            {
                min = Vector3.Min(min, c.transform.position);
                max = Vector3.Max(max, c.transform.position);
            }
            return (min + max) * 0.5f;
        }
    }

    void Start()
    {
        cam = Camera.main;

        Vector3 s = cardPrefab.transform.localScale;
        T = s.x; L = s.y; W = s.z;
        sin = Mathf.Sin(tiltAngle * Mathf.Deg2Rad);
        cos = Mathf.Cos(tiltAngle * Mathf.Deg2Rad);
        S = 2f * L * sin;             // distance between neighbouring tents (bases touch)
        levelH = L * cos + T;         // height of one level (tent + floor card)

        DealHand();
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
                rot = Quaternion.Euler(0, 0, -tiltAngle);
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
    bool TentExists(int l, int i) => Has(LEFT, l, i) || Has(RIGHT, l, i);
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
    Vector2 mouseScreenPos;
    Ray mouseRay;
    bool mouseOverUI;
    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
 
        ReadMouse(mouse);
 
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
    void ReadMouse(Mouse mouse)
    {
        mouseScreenPos = mouse.position.ReadValue();
        mouseOverUI = MouseOverHandUI(mouseScreenPos);
        mouseRay = cam.ScreenPointToRay(mouseScreenPos);
    }
    void HandleFirstClick(Mouse mouse)
    {
        if (!mouse.leftButton.wasPressedThisFrame || mouseOverUI) return;
 
        var ground = new Plane(Vector3.up, new Vector3(0, groundY, 0));
        if (ground.Raycast(mouseRay, out float d))
            StartTower(mouseRay.GetPoint(d));
    }
    void StartTower(Vector3 clickPoint)
    {
        origin = new Vector3(clickPoint.x, groundY, clickPoint.z);
        PlaceCard((LEFT, 0, 0), RandomCard());
        state = State.Choosing;
    }
    void HandleHandKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
 
        for (int i = 0; i < handSize; i++)
            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                SelectCard(i);
    }
    void HandlePlacing(Mouse mouse)
    {
        if (ghost == null) return;
 
        BuildSlots();                 // which slots are free right now
        bestSlot = FindBestSlot();    // which one is closest to the mouse
        MoveGhost();                  // show the preview card
 
        if (mouse.leftButton.wasPressedThisFrame && CanPlace())
            PlaceSelectedCard();
        else if (mouse.rightButton.wasPressedThisFrame)
            CancelSelection();
    }
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
    bool CanPlace()
    {
        return bestSlot >= 0 && !mouseOverUI;
    }
    void PlaceSelectedCard()
    {
        PlaceCard(slots[bestSlot].key, hand[selected]);
        ClearGhost();
        DealHand();
        state = State.Choosing;
    }
    void CancelSelection()
    {
        ClearGhost();
        state = State.Choosing;
    }
    void ClearGhost()
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
        selected = -1;
    }


    // ---------- Cards ----------

    void PlaceCard((int, int, int) key, CardData data)
    {
        GetPose(key.Item1, key.Item2, key.Item3, out Vector3 pos, out Quaternion rot);
        Card c = Instantiate(cardPrefab, pos, rot, transform);
        c.Setup(data);
        c.SetPlaced();
        cards[key] = c;
    }

    void SelectCard(int index)
    {
        if (index < 0 || index >= hand.Length) return;
        if (ghost != null) Destroy(ghost.gameObject);

        selected = index;
        ghost = Instantiate(cardPrefab, FocusPoint, Quaternion.identity);
        ghost.Setup(hand[index]);
        ghost.SetGhost(false);
        state = State.Placing;
    }

    void DealHand()
    {
        hand = new CardData[handSize];
        for (int i = 0; i < handSize; i++) hand[i] = RandomCard();
    }

    CardData RandomCard() => cardTypes[Random.Range(0, cardTypes.Length)];

    // ---------- Simple UI (IMGUI, no Canvas needed) ----------

    Rect HandRect => new Rect(10, Screen.height - 70, handSize * 130 + 10, 60);

    bool MouseOverHandUI(Vector2 screenPos)
    {
        screenPos.y = Screen.height - screenPos.y;
        return HandRect.Contains(screenPos);
    }

    void OnGUI()
    {
        if (state == State.WaitingFirstClick)
        {
            GUI.Label(new Rect(10, 10, 500, 30), "Click on the ground to place the first card");
            return;
        }
        // snap point markers
        if (state == State.Placing && Event.current.type == EventType.Repaint)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(slots[i].pos);
                if (sp.z <= 0) continue;
                GUI.color = i == bestSlot ? Color.green : new Color(1, 1, 1, 0.6f);
                GUI.DrawTexture(new Rect(sp.x - 6, Screen.height - sp.y - 6, 12, 12), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        for (int i = 0; i < hand.Length; i++)
        {
            var r = new Rect(15 + i * 130, Screen.height - 65, 120, 50);
            string label = (i + 1) + ": " + hand[i].cardName + (i == selected ? " *" : "");
            if (GUI.Button(r, label)) SelectCard(i);
        }
    }
}