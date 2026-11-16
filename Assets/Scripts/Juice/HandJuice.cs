using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Hand effects: hover shake, select punch, deselect pop, mouse tilt, lock-in pop, drag lag and lean.
/// Put it on its own object. It gives every hand card a HandCardJuice and only listens to events,
/// so deleting it leaves the hand working the same.
public class HandJuice : MonoBehaviour
{
    [Header("Game references")]
    public HandManager hand;
    public GameFlowController flow;

    [Header("Hover shake")]
    public float hoverShakeDegrees = 6f;
    public float hoverShakeCycles = 2.5f;
    public float hoverShakeSeconds = 0.2f;

    [Header("Select punch / deselect pop")]
    public float punchSeconds = 0.15f;
    public float punchOvershoot = 0.3f;
    public float selectPunchScale = 1.12f;
    public float deselectPopScale = 0.92f;

    [Header("Tilt toward the mouse")]
    public float maxTiltDegrees = 10f;
    public float tiltRadiusPixels = 120f;
    public bool pressUnderMouse = true;
    public float tiltSpeed = 18f;

    [Header("Play Hand accepted")]
    public float lockInPunchScale = 1.15f;
    public float waitingDipScale = 0.85f;
    public float waitingReturnScale = 1.1f;
    public float lockInPunchSeconds = 0.2f;

    [Header("Dragging")]
    public float dragFollowSpeed = 20f;
    public float dragReturnSpeed = 14f;
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

    /// the lean for a hovered card, in its root's local space (so it rides along with the fan).
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

    /// how far the Visual must move (root space) to sit under the mouse, on the card's own plane.
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

    /// lean from the Visual's sideways speed on screen (moving right leans right).
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
