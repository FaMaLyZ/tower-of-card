using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Track B hand effects:
///   B1 (juice-plan 1.1, 2.1, 2.2): hover shake, select punch, deselect pop.
///   B2 (juice-plan 1.2): 3D tilt toward the mouse. (The fan spread is in HandManager's layout, so the
///   click areas move with the cards.)
///   Lock-in: when Play Hand is accepted the committed cards pop and the waiting cards dip as they drop.
///   B4 (juice-plan 3.1 - 3.3): a dragged card follows the mouse with lag, leans the way it moves,
///   and punches when dropped. (The neighbours already glide aside: HandManager eases every root.)
///
/// Put this on its own object in the scene. It gives every hand card a HandCardJuice and tells it
/// when the selection changed. It only listens: HandChanged and SelectionChanged have already
/// happened when it hears them, so it can never change what is selected.
/// Delete this object and the hand plays exactly the same, just without these effects.
public class HandJuice : MonoBehaviour
{
    [Header("Game references (auto-found if empty)")]
    public HandManager hand;
    public GameFlowController flow;

    [Header("Hover shake")]
    [Tooltip("How far the card wobbles each way (degrees).")]
    public float hoverShakeDegrees = 6f;
    [Tooltip("Left-right wobbles in one shake.")]
    public float hoverShakeCycles = 2.5f;
    public float hoverShakeSeconds = 0.2f;

    [Header("Select punch / deselect pop")]
    public float punchSeconds = 0.15f;
    [Tooltip("Extra lift at the top of the punch, as a part of the hand's Selected Lift (0.3 = goes 30% past the lifted spot).")]
    public float punchOvershoot = 0.3f;
    [Tooltip("Size at the top of the select punch (1 = no change).")]
    public float selectPunchScale = 1.12f;
    [Tooltip("Size at the bottom of the deselect pop (1 = no change).")]
    public float deselectPopScale = 0.92f;

    [Header("Tilt toward the mouse (B2)")]
    [Tooltip("Most the hovered card leans (degrees).")]
    public float maxTiltDegrees = 10f;
    [Tooltip("Mouse distance from the card centre (screen pixels) that gives the full tilt.")]
    public float tiltRadiusPixels = 120f;
    [Tooltip("On = the side under the mouse dips away, like pressing the card. Off = it leans toward the mouse.")]
    public bool pressUnderMouse = true;
    public float tiltSpeed = 18f;

    [Header("Play Hand accepted (lock-in)")]
    [Tooltip("Size at the top of the committed cards' pop.")]
    public float lockInPunchScale = 1.15f;
    [Tooltip("Size at the bottom of the waiting cards' dip.")]
    public float waitingDipScale = 0.85f;
    [Tooltip("Size at the top of the waiting cards' pop when they come back up for the next hand.")]
    public float waitingReturnScale = 1.1f;
    public float lockInPunchSeconds = 0.2f;

    [Header("Dragging (B4)")]
    [Tooltip("How fast the dragged card catches up with the mouse. Lower = more lag.")]
    public float dragFollowSpeed = 20f;
    [Tooltip("How fast a dropped card glides back into its place.")]
    public float dragReturnSpeed = 14f;
    [Tooltip("Lean per screen pixel per second of sideways movement.")]
    public float dragLeanPerPixelSpeed = 0.02f;
    public float maxDragLeanDegrees = 25f;
    public float dragLeanSpeed = 12f;
    public float dropPunchScale = 1.1f;
    public float dropPunchSeconds = 0.15f;

    readonly HashSet<int> selectedBefore = new HashSet<int>();
    bool wasLockedIn;
    Camera cam;

    /// Shake only when clicking a card would do something (Swap, Commit or Build).
    public bool CanHoverShake => isActiveAndEnabled && flow != null && flow.IsReady && flow.CanClickHandCards;

    /// The punch lift in the card's own units (the hand's Selected Lift is in hand units).
    public float GetPunchLift()
    {
        if (hand == null) return 0f;
        float cardScale = Mathf.Max(0.0001f, hand.handCardScale);
        return hand.selectedLift * punchOvershoot / cardScale;
    }

    /// Frame-rate independent easing: the part of the remaining distance to cover this frame.
    public float EaseFactor(float speed)
    {
        return speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * Time.deltaTime);
    }

    /// B2: the lean for a hovered card, in its root's local space (so it rides along with the fan).
    public Quaternion GetTiltTowardMouse(Transform cardRoot)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Mouse.current == null) return Quaternion.identity;

        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector3 centre = cam.WorldToScreenPoint(cardRoot.position);
        if (centre.z <= 0f) return Quaternion.identity;

        Vector2 offset = (mouse - (Vector2)centre) / Mathf.Max(1f, tiltRadiusPixels);
        offset = Vector2.ClampMagnitude(offset, 1f) * maxTiltDegrees;
        float sign = pressUnderMouse ? 1f : -1f;

        // Worked out in camera space, so "up" and "right" are what the player sees
        Quaternion worldTilt = Quaternion.AngleAxis(sign * offset.y, cam.transform.right) *
                               Quaternion.AngleAxis(-sign * offset.x, cam.transform.up);
        return Quaternion.Inverse(cardRoot.rotation) * worldTilt * cardRoot.rotation;
    }

    /// B4: how far the Visual must move (root space) to sit under the mouse, on the card's own plane.
    public Vector3 GetDragOffset(Transform cardRoot, Vector3 visualRestPosition)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Mouse.current == null) return Vector3.zero;

        Vector3 restWorld = cardRoot.TransformPoint(visualRestPosition);
        var plane = new Plane(-cam.transform.forward, restWorld);
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!plane.Raycast(ray, out float distance)) return Vector3.zero;

        return cardRoot.InverseTransformPoint(ray.GetPoint(distance)) - visualRestPosition;
    }

    /// B4: lean from the Visual's sideways speed on screen (moving right leans right).
    public float GetDragLean(Vector3 lastWorld, Vector3 nowWorld)
    {
        if (cam == null || Time.deltaTime <= 0f) return 0f;
        float pixelsPerSecond = (cam.WorldToScreenPoint(nowWorld).x - cam.WorldToScreenPoint(lastWorld).x) / Time.deltaTime;
        return Mathf.Clamp(-pixelsPerSecond * dragLeanPerPixelSpeed, -maxDragLeanDegrees, maxDragLeanDegrees);
    }

    /// A lean around the camera's forward axis, turned into the card root's space.
    public Quaternion GetLeanInRootSpace(Transform cardRoot, float degrees)
    {
        if (cam == null || Mathf.Abs(degrees) < 0.001f) return Quaternion.identity;
        return Quaternion.AngleAxis(degrees, cardRoot.InverseTransformDirection(cam.transform.forward));
    }

    void Awake()
    {
        if (hand == null) hand = FindAnyObjectByType<HandManager>();
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    // Play Hand accepted: committed cards pop, waiting cards dip as they drop away.
    // Next hand: the waiting cards pop back up.
    void LateUpdate()
    {
        bool lockedIn = flow != null && flow.IsReady && flow.IsHandLockedIn;
        if (lockedIn == wasLockedIn || hand == null) { wasLockedIn = lockedIn; return; }
        wasLockedIn = lockedIn;

        for (int i = 0; i < hand.Count; i++)
        {
            int cardId = hand.GetCardId(i);
            HandCardJuice juice = FindJuice(cardId);
            if (juice == null) continue;

            if (!lockedIn) juice.PlayPunch(lockInPunchSeconds, 0f, waitingReturnScale);
            else if (hand.IsSelected(cardId)) juice.PlayPunch(lockInPunchSeconds, GetPunchLift(), lockInPunchScale);   // snaps up a little
            else juice.PlayPunch(lockInPunchSeconds, -GetPunchLift(), waitingDipScale);
        }
    }

    void OnEnable()
    {
        GameEvents.HandChanged += OnHandChanged;
        GameEvents.SelectionChanged += OnSelectionChanged;
    }

    void OnDisable()
    {
        GameEvents.HandChanged -= OnHandChanged;
        GameEvents.SelectionChanged -= OnSelectionChanged;
    }

    // New cards in the hand get their effect component
    void OnHandChanged(IReadOnlyList<int> cardIdsInOrder)
    {
        if (hand == null) return;

        foreach (int cardId in cardIdsInOrder)
        {
            HandCardView view = hand.FindView(cardId);
            if (view == null || view.GetComponent<HandCardJuice>() != null) continue;
            view.gameObject.AddComponent<HandCardJuice>().Setup(view, this);
        }
    }

    // Compare with the last selection: newly picked cards punch up, dropped cards pop down
    void OnSelectionChanged(IReadOnlyList<int> selectedCardIds)
    {
        var selectedNow = new HashSet<int>(selectedCardIds);

        foreach (int cardId in selectedNow)
        {
            HandCardJuice juice = FindJuice(cardId);
            if (!selectedBefore.Contains(cardId) && juice != null) juice.PlaySelectPunch();
        }

        foreach (int cardId in selectedBefore)
        {
            HandCardJuice juice = FindJuice(cardId);
            if (!selectedNow.Contains(cardId) && juice != null) juice.PlayDeselectPop();   // null = the card left the hand
        }

        selectedBefore.Clear();
        selectedBefore.UnionWith(selectedNow);
    }

    HandCardJuice FindJuice(int cardId)
    {
        HandCardView view = hand != null ? hand.FindView(cardId) : null;
        return view != null ? view.GetComponent<HandCardJuice>() : null;
    }
}
