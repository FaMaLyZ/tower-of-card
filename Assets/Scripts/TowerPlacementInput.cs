using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// The player's hand on the tower: the card being held, the see-through preview that follows the
/// mouse, snapping it to the nearest free slot, and the clicks.
///
///   Empty tower  -> a click on the table places the next card there
///   Holding card -> the preview snaps to the nearest slot; left-click places, right-click puts it back
///   No card held -> right-click undoes this hand's last card
///
/// A click is a command: it goes to the conductor (GameFlowController), which checks the phase and
/// then calls PlaceHeldCard() here. Where the card ends up is TowerBuilder's job.
public class TowerPlacementInput : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public GameFlowController flow;
    public TowerBuilder tower;

    [Header("First card")]
    [Tooltip("The first card of a tower can only be placed on something in this layer (the table). Keeps players from starting a tower off the table.")]
    public LayerMask tableLayerMask = 1 << 7;   // "Table" layer

    [Header("Snapping")]
    public float snapRadiusPixels = 90f;             // mouse must be this close to a slot (screen pixels)
    public float snapSpeed = 25f;

    [Header("Hints (shown by HudView)")]
    public string firstClickHint = "Click the table to start the tower";
    public string placingHint = "Click a snap point to place. Right-click: put the card back. Orbit camera: A / D.";
    public string choosingHint = "Pick one of your raised cards to place it. Right-click: undo the last card.";

    bool inputEnabled;
    CardInstance heldCard;            // the committed card the player picked up to place
    Card preview;                     // the see-through card that follows the mouse
    List<OpenSlot> openSlots = new List<OpenSlot>();
    int bestSlot = -1;

    // Per-frame mouse info (filled by ReadMouse, used by the handlers below)
    Camera cam;
    Vector2 mouseScreenPos;
    Ray mouseRay;
    bool mouseOverUI;
    Vector3? clickedTablePoint;       // where the table was clicked (only while that click is being handled)

    // ---------- Public info (the conductor, HudView and SnapMarkerView read these) ----------

    /// True while the conductor has placing switched on (the Build phase).
    public bool IsInputEnabled => inputEnabled;
    public bool IsHoldingCard => heldCard != null;

    bool IsAiming => preview != null;                           // holding a card over a standing tower

    public string HintText
    {
        get
        {
            if (!inputEnabled) return "";
            if (tower.IsEmpty) return firstClickHint;
            return IsAiming ? placingHint : choosingHint;
        }
    }

    /// The free slots are shown while the player aims a card.
    public bool ShowSlots => inputEnabled && IsAiming;
    public int SlotCount => openSlots.Count;
    public int BestSlotIndex => bestSlot;
    public Vector3 GetSlotPosition(int i) => openSlots[i].position;

    // ---------- Lifecycle ----------

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (tower == null) tower = GetComponent<TowerBuilder>();
        if (tower == null) tower = FindAnyObjectByType<TowerBuilder>();
        if (tower != null) tower.TowerReset += DropHeldCard;
    }

    void OnDestroy()
    {
        if (tower != null) tower.TowerReset -= DropHeldCard;
    }

    void Start()
    {
        cam = Camera.main;
    }

    // ---------- Jobs the conductor asks for ----------

    public void EnableInput()
    {
        inputEnabled = true;
    }

    /// Building is over: drop the card the player was holding.
    public void DisableInput()
    {
        inputEnabled = false;
        DropHeldCard();
    }

    /// The player picked up a committed card: show it as the preview that follows the mouse.
    public void HoldCard(CardInstance card)
    {
        if (!inputEnabled || card == null) return;

        heldCard = card;
        AudioManager.SFX(SoundType.SelectCard);

        // Before the tower exists there are no slots: the next click on the table places the card
        if (tower.IsEmpty) return;
        ShowPreview(card.Data);
    }

    /// Places the held card where the player just clicked (the table for the first card, otherwise
    /// the snapped slot). Returns the card that was placed, or null if nothing happened.
    public CardInstance PlaceHeldCard()
    {
        if (!inputEnabled || heldCard == null) return null;

        CardInstance placed = heldCard;
        if (tower.IsEmpty)
        {
            if (!clickedTablePoint.HasValue) return null;
            tower.PlaceFirstCard(clickedTablePoint.Value, placed);
        }
        else
        {
            if (!CanPlace()) return null;
            tower.PlaceCard(openSlots[bestSlot].key, placed);
        }

        DropHeldCard();
        return placed;
    }

    /// Put the held card back (it stays in the hand, still waiting to be placed).
    public void DropHeldCard()
    {
        heldCard = null;
        ClearPreview();
    }

    // ---------- Update ----------

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || tower == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        ReadMouse(mouse);

        // The conductor says "not now" (every phase except Build)
        if (!inputEnabled)
        {
            DropHeldCard();
            return;
        }

        if (tower.IsEmpty) HandleFirstClick(mouse);
        else if (IsAiming) HandleAiming(mouse);
        else HandleUndoClick(mouse);
    }

    void ReadMouse(Mouse mouse)
    {
        mouseScreenPos = mouse.position.ReadValue();
        mouseOverUI = IsPointerOverBlockingUI(mouseScreenPos);
        mouseRay = cam.ScreenPointToRay(mouseScreenPos);
    }

    // True when the mouse is over something that should block placing a card:
    // a UI element, a card in the hand, or the swap zone.
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
            if (hit.module is GraphicRaycaster) return true;
            if (hit.gameObject.GetComponentInParent<HandCardView>() != null) return true;
            if (hit.gameObject.GetComponentInParent<SwapZone>() != null) return true;
        }
        return false;
    }

    // ----- Empty tower: a click on the table -----

    void HandleFirstClick(Mouse mouse)
    {
        if (!mouse.leftButton.wasPressedThisFrame || mouseOverUI) return;

        // Only start a tower where the click actually lands on the table, not anywhere on the infinite ground plane.
        if (!Physics.Raycast(mouseRay, out RaycastHit hit, 1000f, tableLayerMask)) return;

        clickedTablePoint = hit.point;
        RequestPlaceHeldCard();
        clickedTablePoint = null;
    }

    // ----- No card held: right-click undoes -----

    void HandleUndoClick(Mouse mouse)
    {
        if (mouse.rightButton.wasPressedThisFrame && !mouseOverUI && flow != null) flow.RequestUndo();
    }

    // ----- Holding a card over the tower -----

    void HandleAiming(Mouse mouse)
    {
        openSlots = tower.FindOpenSlots();  // which slots are free right now
        bestSlot = FindBestSlot();          // which one is closest to the mouse
        MovePreview();

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

        for (int i = 0; i < openSlots.Count; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(openSlots[i].position);
            if (sp.z <= 0) continue;   // behind camera

            float d = Vector2.Distance(new Vector2(sp.x, sp.y), mouseScreenPos);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    // Add extra rules here (e.g. require a certain card type...)
    bool CanPlace()
    {
        return bestSlot >= 0 && bestSlot < openSlots.Count && !mouseOverUI;
    }

    // ---------- The preview card ----------

    void ShowPreview(CardData data)
    {
        ClearPreview();
        preview = tower.CreatePreviewCard(data);
    }

    void ClearPreview()
    {
        if (preview != null) Destroy(preview.gameObject);
        preview = null;
        openSlots.Clear();
        bestSlot = -1;
    }

    // On the snapped slot, or following the mouse on the tower's plane
    void GetPreviewTarget(out Vector3 pos, out Quaternion rot)
    {
        if (bestSlot >= 0)
        {
            pos = openSlots[bestSlot].position;
            rot = openSlots[bestSlot].rotation;
            return;
        }

        var plane = new Plane(Vector3.forward, tower.FocusPoint);
        pos = plane.Raycast(mouseRay, out float d) ? mouseRay.GetPoint(d) : mouseRay.GetPoint(10f);
        rot = Quaternion.identity;
    }

    void MovePreview()
    {
        GetPreviewTarget(out Vector3 targetPos, out Quaternion targetRot);

        float t = 1f - Mathf.Exp(-snapSpeed * Time.deltaTime);
        preview.transform.position = Vector3.Lerp(preview.transform.position, targetPos, t);
        preview.transform.rotation = Quaternion.Slerp(preview.transform.rotation, targetRot, t);
        preview.SetGhost(bestSlot >= 0);   // green when snapped, red otherwise
    }
}
