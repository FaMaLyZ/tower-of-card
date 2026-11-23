using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// Track B5 (juice-plan 6.2): the deck reacts.
///   - Hover while a swap is possible: a short wobble ("you can click me").
///   - Every card drawn: a wobble and a small squash, timed with each card leaving (DrawJuice spacing).
///   - Cards swapped back in: a bump and a white flash.
///   - A refused click: a sideways shake and a red flash. (The HUD also says why, in words.)
///
/// Put it on the deck object (next to SwapZone). It moves and tints every child of the deck together,
/// as one piece around the deck's centre, but never the SwapZone root itself (SwapZone scales that on hover).
/// The deck has no physics, so moving its click box for a moment is harmless.
/// It only listens to things that already happened. Delete it and the deck works the same.
public class DeckJuice : MonoBehaviour, IPointerEnterHandler
{
    [Header("Game references (auto-found if empty)")]
    public GameFlowController flow;
    [Tooltip("The parts that move and flash together. Empty = every child of the deck that has a Renderer.")]
    public List<Transform> parts = new List<Transform>();

    [Header("Hover wobble")]
    public float wobbleDegrees = 8f;
    public float wobbleCycles = 2f;
    public float wobbleSeconds = 0.25f;

    [Header("Cards in / out")]
    [Tooltip("Wobble once for every card drawn, not once per draw.")]
    public bool wobbleForEachCardDrawn = true;
    [Tooltip("Seconds between those wobbles. Empty DrawJuice = use this; with DrawJuice its Card Interval is used, so the wobble matches each card leaving.")]
    public float drawInterval = 0.1f;
    public DrawJuice drawJuice;
    public float arrivePunchScale = 1.15f;
    public float departPunchScale = 0.88f;
    public float punchSeconds = 0.12f;
    [Tooltip("Colour the deck flashes toward when cards come back. Above 1 = brighter than the material.")]
    [ColorUsage(false, true)] public Color arriveFlashColor = new Color(2f, 2f, 2f, 1f);
    public float flashSeconds = 0.2f;

    [Header("Refused click")]
    [Tooltip("Sideways shake, as a part of the deck's width.")]
    public float rejectShakeAmount = 0.06f;
    public float rejectShakeCycles = 4f;
    public float rejectShakeSeconds = 0.3f;
    public Color rejectFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    // Each part's normal pose, so every effect is an offset that can never drift
    struct Part
    {
        public Transform transform;
        public Vector3 restPosition;
        public Quaternion restRotation;
        public Vector3 restScale;
    }

    struct Tint
    {
        public Renderer renderer;
        public Color restColor;
        public int colorId;
    }

    readonly List<Part> restPoses = new List<Part>();
    readonly List<Tint> tints = new List<Tint>();
    MaterialPropertyBlock block;

    // A squash or bump: when it started and how big it gets
    struct Punch
    {
        public float start;
        public float peak;
    }

    // Wobbles and punches can overlap (a card leaves every 0.1 s): they add up instead of restarting,
    // so the deck never jumps back to rest in the middle of one
    readonly List<float> wobbleStarts = new List<float>();
    readonly List<Punch> punches = new List<Punch>();
    readonly List<float> pendingDrawPulses = new List<float>();     // times when a drawn card leaves the deck
    float shakeStart = -1f, flashStart = -1f;
    Color flashColor;
    float shakeWidth;
    bool isAtRest = true;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (drawJuice == null) drawJuice = FindAnyObjectByType<DrawJuice>();
        if (parts.Count == 0) FindDeckParts();
        RememberRestPoses();
        block = new MaterialPropertyBlock();
    }

    void FindDeckParts()
    {
        foreach (Transform child in transform)
            if (child.GetComponentInChildren<Renderer>() != null) parts.Add(child);
    }

    void RememberRestPoses()
    {
        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (Transform part in parts)
        {
            if (part == null) continue;
            restPoses.Add(new Part { transform = part, restPosition = part.localPosition, restRotation = part.localRotation, restScale = part.localScale });

            foreach (Renderer r in part.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(r.bounds);
                RememberTint(r);
            }
        }
        shakeWidth = bounds.size.x;
    }

    void RememberTint(Renderer r)
    {
        Material m = r.sharedMaterial;
        if (m == null) return;
        int id = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : -1;
        if (id >= 0) tints.Add(new Tint { renderer = r, restColor = m.GetColor(id), colorId = id });
    }

    void OnEnable()
    {
        GameEvents.CardsDrawn += OnCardsDrawn;
        GameEvents.CardsSwapped += OnCardsSwapped;
        GameEvents.SwapRejected += OnSwapRejected;
    }

    void OnDisable()
    {
        GameEvents.CardsDrawn -= OnCardsDrawn;
        GameEvents.CardsSwapped -= OnCardsSwapped;
        GameEvents.SwapRejected -= OnSwapRejected;
        shakeStart = flashStart = -1f;
        wobbleStarts.Clear();
        punches.Clear();
        pendingDrawPulses.Clear();
        isAtRest = false;
        ReturnToRest();
    }

    // ---------- Triggers ----------

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (flow != null && flow.IsReady && flow.CanSwapSelectedCard) wobbleStarts.Add(Time.time);
    }

    // One wobble and squash per card, at the moment that card leaves the deck
    void OnCardsDrawn(IReadOnlyList<int> cardIds)
    {
        if (!wobbleForEachCardDrawn)
        {
            PlayDrawPulse();
            return;
        }

        float interval = drawJuice != null && drawJuice.isActiveAndEnabled ? drawJuice.cardInterval : drawInterval;
        for (int i = 0; i < cardIds.Count; i++)
            pendingDrawPulses.Add(Time.time + i * interval);
    }

    void PlayDrawPulse()
    {
        wobbleStarts.Add(Time.time);
        PlayPunch(departPunchScale);
    }

    void FireDueDrawPulses()
    {
        for (int i = pendingDrawPulses.Count - 1; i >= 0; i--)
        {
            if (Time.time < pendingDrawPulses[i]) continue;
            pendingDrawPulses.RemoveAt(i);
            PlayDrawPulse();
        }
    }

    void OnCardsSwapped(int cardCount)
    {
        PlayPunch(arrivePunchScale);
        PlayFlash(arriveFlashColor);

        //เสียง Swap
        AudioManager.Instance.PlayOneShotSFX(SoundType.CardSwap_Success);
    }

    void OnSwapRejected(SwapRejectReason reason)
    {
        shakeStart = Time.time;
        PlayFlash(rejectFlashColor);

        //เสียง SwapReject
        AudioManager.Instance.PlayOneShotSFX(SoundType.CardSwap_Reject);
    }

    void PlayPunch(float peak)
    {
        punches.Add(new Punch { start = Time.time, peak = peak });
    }

    void PlayFlash(Color color)
    {
        flashStart = Time.time;
        flashColor = color;
    }

    // ---------- Every frame ----------

    void LateUpdate()
    {
        FireDueDrawPulses();

        float wobble = SumWobbles();
        float shake = Decaying(ref shakeStart, rejectShakeSeconds, rejectShakeCycles) * rejectShakeAmount * shakeWidth;
        float scale = SumPunches();
        float flash = Fade(ref flashStart, flashSeconds);

        if (wobbleStarts.Count == 0 && punches.Count == 0 && shakeStart < 0f && flashStart < 0f && pendingDrawPulses.Count == 0)
        {
            ReturnToRest();
            return;
        }

        isAtRest = false;
        ApplyPose(wobble, shake, scale);
        ApplyFlash(flash);
    }

    // Every wobble still playing, added up (kept within 1.5x one wobble so a fast draw can't spin the deck)
    float SumWobbles()
    {
        float total = 0f;
        for (int i = wobbleStarts.Count - 1; i >= 0; i--)
        {
            float start = wobbleStarts[i];
            total += Decaying(ref start, wobbleSeconds, wobbleCycles) * wobbleDegrees;
            if (start < 0f) wobbleStarts.RemoveAt(i);
        }
        float limit = wobbleDegrees * 1.5f;
        return Mathf.Clamp(total, -limit, limit);
    }

    // Every squash / bump still playing, added up into one size
    float SumPunches()
    {
        float scale = 1f;
        for (int i = punches.Count - 1; i >= 0; i--)
        {
            float start = punches[i].start;
            scale += (punches[i].peak - 1f) * Bump(ref start, punchSeconds);
            if (start < 0f) punches.RemoveAt(i);
        }
        return Mathf.Clamp(scale, 0.75f, 1.3f);
    }

    // A wave that dies out over "seconds" (-1..1), or 0 when finished
    static float Decaying(ref float start, float seconds, float cycles)
    {
        float t = Progress(ref start, seconds);
        return t < 0f ? 0f : Mathf.Sin(t * cycles * 2f * Mathf.PI) * (1f - t);
    }

    // 0 -> 1 -> 0
    static float Bump(ref float start, float seconds)
    {
        float t = Progress(ref start, seconds);
        return t < 0f ? 0f : Mathf.Sin(t * Mathf.PI);
    }

    // 1 -> 0
    static float Fade(ref float start, float seconds)
    {
        float t = Progress(ref start, seconds);
        return t < 0f ? 0f : 1f - t;
    }

    // 0..1 while playing, -1 when not playing (and switches the effect off once it is done)
    static float Progress(ref float start, float seconds)
    {
        if (start < 0f) return -1f;
        float t = (Time.time - start) / Mathf.Max(0.01f, seconds);
        if (t >= 1f) { start = -1f; return -1f; }
        return t;
    }

    // The whole deck moves as one piece around its centre (the deck object's origin):
    // every part is scaled, turned and shifted together, so the stack never comes apart.
    void ApplyPose(float wobbleDegreesNow, float shakeWorld, float scale)
    {
        Camera cam = Camera.main;
        Vector3 shakeLocal = cam != null ? transform.InverseTransformVector(cam.transform.right * shakeWorld) : Vector3.zero;
        Quaternion turn = Quaternion.AngleAxis(wobbleDegreesNow, Vector3.up);

        foreach (Part p in restPoses)
        {
            if (p.transform == null) continue;
            p.transform.localPosition = turn * (p.restPosition * scale) + shakeLocal;
            p.transform.localRotation = turn * p.restRotation;
            p.transform.localScale = p.restScale * scale;
        }
    }

    void ApplyFlash(float amount)
    {
        foreach (Tint tint in tints)
        {
            if (tint.renderer == null) continue;
            if (amount <= 0f) { tint.renderer.SetPropertyBlock(null); continue; }

            tint.renderer.GetPropertyBlock(block);
            block.SetColor(tint.colorId, Color.Lerp(tint.restColor, flashColor, amount));
            tint.renderer.SetPropertyBlock(block);
        }
    }

    void ReturnToRest()
    {
        if (isAtRest) return;
        foreach (Part p in restPoses)
        {
            if (p.transform == null) continue;
            p.transform.localPosition = p.restPosition;
            p.transform.localRotation = p.restRotation;
            p.transform.localScale = p.restScale;
        }
        foreach (Tint tint in tints)
            if (tint.renderer != null) tint.renderer.SetPropertyBlock(null);
        isAtRest = true;
    }
}
