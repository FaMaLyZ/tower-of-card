using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Drawn cards fly in from the deck one by one and punch when they land.
/// Drives each card's HandCardJuice (so HandJuice must be on too) and holds the
/// PresentationGate until the last card lands. Delete it and drawn cards simply appear.
public class DrawJuice : MonoBehaviour
{
    [Header("Game references")]
    public HandManager hand;
    public Transform deckPoint;

    [Header("Timing")]
    public float cardInterval = 0.1f;
    public float flightSeconds = 0.35f;

    [Header("Flight")]
    public float arcHeight = 0.4f;
    public float flightTiltDegrees = 25f;
    [Range(0f, 1f)] public float startScale = 0.5f;

    [Header("Landing")]
    public float landPunchScale = 1.15f;
    public float landPunchSeconds = 0.15f;
    public bool playLandSound = true;

    [Header("Game flow")]
    public bool holdGameUntilLanded = true;

    Camera cam;
    bool isHoldingGate;
    Coroutine releaseLater;

    void Awake()
    {
        if (hand == null) hand = FindAnyObjectByType<HandManager>();
        if (deckPoint == null)
        {
            SwapZone deck = FindAnyObjectByType<SwapZone>();
            if (deck != null) deckPoint = deck.transform;
        }
    }

    void OnEnable()
    {
        GameEvents.CardsDrawn += OnCardsDrawn;
    }

    void OnDisable()
    {
        GameEvents.CardsDrawn -= OnCardsDrawn;
        ReleaseGate();
    }

    // ---------- A draw happened: send the cards on their way, one after another ----------

    void OnCardsDrawn(IReadOnlyList<int> cardIds)
    {
        if (hand == null || deckPoint == null) return;

        int flying = 0;
        foreach (int cardId in cardIds)
        {
            HandCardJuice juice = FindJuice(cardId);
            if (juice == null) continue;                // HandJuice is off: the card just appears

            juice.PlayDrawFlight(this, deckPoint.position, flying * cardInterval);
            flying++;
        }

        if (flying > 0) HoldGateUntilLanded(flying);
    }

    HandCardJuice FindJuice(int cardId)
    {
        HandCardView view = hand.FindView(cardId);
        return view != null ? view.GetComponent<HandCardJuice>() : null;
    }

    // ---------- The PresentationGate: held once per draw, released once ----------

    void HoldGateUntilLanded(int cardCount)
    {
        if (!holdGameUntilLanded) return;

        float seconds = (cardCount - 1) * cardInterval + flightSeconds + landPunchSeconds;
        if (!isHoldingGate)
        {
            PresentationGate.Hold();
            isHoldingGate = true;
        }

        // A second draw while the first is still flying: one hold, released after the later one
        if (releaseLater != null) StopCoroutine(releaseLater);
        releaseLater = StartCoroutine(ReleaseGateAfter(seconds));
    }

    IEnumerator ReleaseGateAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        releaseLater = null;
        ReleaseGate();
    }

    void ReleaseGate()
    {
        if (releaseLater != null) StopCoroutine(releaseLater);
        releaseLater = null;
        if (!isHoldingGate) return;

        PresentationGate.Release();
        isHoldingGate = false;
    }

    // ---------- Shapes the flight (HandCardJuice asks for these) ----------

    /// The curve's middle point: above the straight line between the deck and the card's place.
    public Vector3 GetArcControlPoint(Vector3 from, Vector3 to)
    {
        Vector3 up = GetCamera() != null ? cam.transform.up : Vector3.up;
        return (from + to) * 0.5f + up * Vector3.Distance(from, to) * arcHeight;
    }

    /// Lean the way the card is moving across the screen, strongest in the middle of the flight.
    public Quaternion GetFlightTilt(Vector3 travelDirection, float flightProgress)
    {
        if (GetCamera() == null || travelDirection.sqrMagnitude < 0.000001f) return Quaternion.identity;

        float sideways = Vector3.Dot(travelDirection.normalized, cam.transform.right);   // -1 left .. +1 right
        float strength = Mathf.Sin(Mathf.Clamp01(flightProgress) * Mathf.PI);           // 0 at both ends
        return Quaternion.AngleAxis(-sideways * flightTiltDegrees * strength, cam.transform.forward);
    }

    public void PlayLandSound()
    {
        if (playLandSound) AudioManager.SFX(SoundType.DrawCard);
    }

    Camera GetCamera()
    {
        if (cam == null) cam = Camera.main;
        return cam;
    }
}
