using UnityEngine;
using UnityEngine.EventSystems;

/// The effects on ONE hand card (Track B1 - B4): hover shake, select punch, deselect pop,
/// 3D tilt toward the mouse, the flight in from the deck,
/// and while dragged: following the mouse with a little lag and leaning the way it moves.
///
/// HandJuice adds this to every hand card. It moves the card's Visual child only, as an offset
/// on top of the Visual's rest pose, and puts the Visual back exactly when nothing is playing.
/// It never selects, moves or reorders a card: the root (and its click collider) stays where the
/// hand puts it. Remove HandJuice from the scene and the hand works the same, just without effects.
public class HandCardJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    const float RestEpsilon = 0.0001f;

    HandCardView view;
    HandJuice settings;

    // B1: each effect remembers when it started; < 0 = not playing
    float shakeStart = -1f;
    float punchStart = -1f;
    float punchSeconds;
    float punchLift;                    // + = up (select), - = down (deselect), 0 = scale only (landing)
    float punchPeakScale = 1f;

    // B3: the flight in from the deck; flightStart < 0 = not flying
    DrawJuice flight;
    float flightStart = -1f;
    Vector3 flightFrom;

    // B2: eased toward their targets every frame
    bool isHovered;
    Quaternion tilt = Quaternion.identity;          // in the root's local space

    // B4: while this card is dragged its Visual follows the mouse; after release it glides home
    bool wasDragged;
    Vector3 dragOffset;                             // in the root's local space
    float dragLean;                                 // degrees, around the camera's forward axis
    Vector3 lastVisualWorld;

    bool isAtRest = true;

    public void Setup(HandCardView cardView, HandJuice juiceSettings)
    {
        view = cardView;
        settings = juiceSettings;
    }

    // ---------- Triggers ----------

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (settings != null && settings.CanHoverShake && IsUsable) PlayHoverShake();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    // Waiting cards (after Play Hand) ignore the mouse
    bool IsUsable => view != null && settings != null && settings.hand != null && settings.hand.IsInteractable(view.CardId);

    public void PlayHoverShake()
    {
        shakeStart = Time.time;
    }

    public void PlaySelectPunch()
    {
        PlayPunch(settings.punchSeconds, settings.GetPunchLift(), settings.selectPunchScale);
    }

    public void PlayDeselectPop()
    {
        PlayPunch(settings.punchSeconds, -settings.GetPunchLift(), settings.deselectPopScale);
    }

    /// One bump: up (or down) by "lift" and to "peakScale" at the middle, back to rest at the end.
    public void PlayPunch(float seconds, float lift, float peakScale)
    {
        punchStart = Time.time;
        punchSeconds = Mathf.Max(0.01f, seconds);
        punchLift = lift;
        punchPeakScale = peakScale;
    }

    /// B3: fly in from the deck after "delay" seconds. Until then the card is hidden at the deck.
    public void PlayDrawFlight(DrawJuice drawSettings, Vector3 fromWorld, float delay)
    {
        flight = drawSettings;
        flightFrom = fromWorld;
        flightStart = Time.time + delay;
    }

    public bool IsFlying => flightStart >= 0f;

    // ---------- Every frame: add up the effects, apply them to the Visual ----------

    void LateUpdate()
    {
        if (view == null || view.Visual == null || settings == null) return;

        // HandJuice switched off: every effect stops and the card sits exactly on its root
        if (!settings.isActiveAndEnabled)
        {
            shakeStart = punchStart = flightStart = -1f;
            wasDragged = false;
            ReturnToRest();
            return;
        }

        if (IsFlying)
        {
            FlyFromDeck();
            return;
        }

        float shakeAngle = GetShakeAngle();
        UpdateDrag();
        GetPunch(out float lift, out float scale);
        UpdateTilt();

        if (NothingIsPlaying())
        {
            ReturnToRest();
            return;
        }

        ApplyOffsets(shakeAngle, lift, scale);
    }

    bool NothingIsPlaying()
    {
        return shakeStart < 0f && punchStart < 0f && !isHovered && !wasDragged &&
               dragOffset.sqrMagnitude < RestEpsilon && Mathf.Abs(dragLean) < 0.01f &&
               Quaternion.Angle(tilt, Quaternion.identity) < 0.01f;
    }

    // B1: a quick left-right wobble around the card's own Z axis that dies out
    float GetShakeAngle()
    {
        if (shakeStart < 0f) return 0f;

        float t = (Time.time - shakeStart) / settings.hoverShakeSeconds;
        if (t >= 1f) { shakeStart = -1f; return 0f; }

        float wave = Mathf.Sin(t * settings.hoverShakeCycles * 2f * Mathf.PI);
        return settings.hoverShakeDegrees * wave * (1f - t);
    }

    // B1 / B3: one bump out and back (select goes up, deselect dips, landing only grows). Scale follows.
    void GetPunch(out float lift, out float scale)
    {
        lift = 0f;
        scale = 1f;
        if (punchStart < 0f) return;

        float t = (Time.time - punchStart) / punchSeconds;
        if (t >= 1f) { punchStart = -1f; return; }

        float bump = Mathf.Sin(t * Mathf.PI);           // 0 -> 1 -> 0
        lift = punchLift * bump;
        scale = 1f + (punchPeakScale - 1f) * bump;
    }

    // ---------- B3: the flight in from the deck ----------

    // Along a curve that rises above the straight line, leaning the way it travels, growing to full size.
    // The root is already in its place (and clickable); only the Visual is still on its way.
    void FlyFromDeck()
    {
        // DrawJuice switched off mid-flight: the card just appears in its place
        if (flight == null || !flight.isActiveAndEnabled)
        {
            flightStart = -1f;
            view.ResetVisualPose();
            return;
        }

        isAtRest = false;
        Transform visual = view.Visual;
        Vector3 to = transform.TransformPoint(view.VisualRestPosition);    // follows the root if the fan shifts
        Quaternion restWorld = transform.rotation * view.VisualRestRotation;

        float t = (Time.time - flightStart) / Mathf.Max(0.01f, flight.flightSeconds);
        if (t < 0f)
        {
            HideAtDeck(visual, restWorld);
            return;
        }
        if (t >= 1f)
        {
            Land();
            return;
        }

        float eased = 1f - (1f - t) * (1f - t);         // fast out of the deck, slowing into the hand
        Vector3 control = flight.GetArcControlPoint(flightFrom, to);
        visual.position = Bezier(flightFrom, control, to, eased);
        visual.rotation = flight.GetFlightTilt(BezierDirection(flightFrom, control, to, eased), t) * restWorld;
        visual.localScale = view.VisualRestScale * Mathf.Lerp(flight.startScale, 1f, eased);
    }

    void HideAtDeck(Transform visual, Quaternion restWorld)
    {
        visual.position = flightFrom;
        visual.rotation = restWorld;
        visual.localScale = Vector3.zero;               // not drawn yet: invisible
    }

    void Land()
    {
        flightStart = -1f;
        view.ResetVisualPose();
        PlayPunch(flight.landPunchSeconds, 0f, flight.landPunchScale);
        flight.PlayLandSound();
    }

    static Vector3 Bezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }

    static Vector3 BezierDirection(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        return 2f * (1f - t) * (control - a) + 2f * t * (b - control);
    }

    // ---------- B4: dragging ----------

    bool IsDragged => settings.hand != null && settings.hand.DraggedCardId == view.CardId;

    // Follow the mouse (eased, so it lags a little) and lean the way it moves across the screen.
    // Released: glide back into the card's place in the hand and punch once.
    void UpdateDrag()
    {
        bool dragged = IsDragged;
        Vector3 targetOffset = dragged ? settings.GetDragOffset(transform, view.VisualRestPosition) : Vector3.zero;
        float speed = dragged ? settings.dragFollowSpeed : settings.dragReturnSpeed;
        dragOffset = Vector3.Lerp(dragOffset, targetOffset, settings.EaseFactor(speed));

        Vector3 visualWorld = transform.TransformPoint(view.VisualRestPosition + dragOffset);
        float targetLean = dragged ? settings.GetDragLean(lastVisualWorld, visualWorld) : 0f;
        dragLean = Mathf.Lerp(dragLean, targetLean, settings.EaseFactor(settings.dragLeanSpeed));
        lastVisualWorld = visualWorld;

        if (wasDragged && !dragged) PlayPunch(settings.dropPunchSeconds, 0f, settings.dropPunchScale);
        wasDragged = dragged;
    }

    // B2: while hovered, lean toward the mouse; otherwise ease back to flat
    void UpdateTilt()
    {
        Quaternion target = isHovered && !wasDragged && IsUsable ? settings.GetTiltTowardMouse(transform) : Quaternion.identity;
        tilt = Quaternion.Slerp(tilt, target, settings.EaseFactor(settings.tiltSpeed));
    }

    void ApplyOffsets(float shakeAngle, float lift, float scale)
    {
        isAtRest = false;
        Transform visual = view.Visual;

        // The root's local "up" is the card's up (it leans with the fan)
        visual.localPosition = view.VisualRestPosition + Vector3.up * lift + dragOffset;
        visual.localRotation = settings.GetLeanInRootSpace(transform, dragLean) * tilt *
                               Quaternion.AngleAxis(shakeAngle, Vector3.forward) * view.VisualRestRotation;
        visual.localScale = view.VisualRestScale * scale;
    }

    void ReturnToRest()
    {
        if (isAtRest) return;
        view.ResetVisualPose();
        tilt = Quaternion.identity;
        dragOffset = Vector3.zero;
        dragLean = 0f;
        isAtRest = true;
    }

    // Switched off or removed mid-effect: never leave the Visual out of place
    void OnDisable()
    {
        shakeStart = -1f;
        punchStart = -1f;
        flightStart = -1f;
        isHovered = false;
        wasDragged = false;
        isAtRest = false;
        if (view != null) ReturnToRest();
    }
}
