using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// Deck effects: hover shake, tilt and lift (like the hand cards), a squash per card drawn, a flash on swap-in,
/// a red shake on a refused click.
/// Put it on the deck object. It moves the deck's children, never the SwapZone root.
/// Listen-only: delete it and the deck works the same.
public class DeckJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Game references")]
    public GameFlowController flow;
    public List<Transform> parts = new List<Transform>();

    [Header("Hover (like the hand cards)")]
    [Tooltip("Quick left-right shake when the mouse arrives")]
    public float hoverShakeDegrees = 6f;
    public float hoverShakeCycles = 2.5f;
    public float hoverShakeSeconds = 0.2f;
    [Tooltip("Most the deck leans toward the mouse while hovered")]
    public float maxTiltDegrees = 10f;
    [Tooltip("Mouse distance from the deck's centre that gives the full lean")]
    public float tiltRadiusPixels = 150f;
    [Tooltip("True: the side under the mouse dips, like pressing on it")]
    public bool pressUnderMouse = true;
    [Tooltip("How fast the lean, lift and grow follow the mouse")]
    public float hoverSpeed = 18f;
    [Tooltip("World units the deck rises on screen while hovered")]
    public float hoverLift = 0.05f;
    public float hoverScale = 1.05f;

    [Header("Swap-ready wobble (hover while picked cards can be swapped)")]
    public float wobbleDegrees = 8f;
    public float wobbleCycles = 2f;
    public float wobbleSeconds = 0.25f;

    [Header("Cards in / out")]
    public bool wobbleForEachCardDrawn = true;
    public float drawInterval = 0.1f;
    public DrawJuice drawJuice;
    public float arrivePunchScale = 1.15f;
    public float departPunchScale = 0.88f;
    public float punchSeconds = 0.12f;
    [ColorUsage(false, true)] public Color arriveFlashColor = new Color(2f, 2f, 2f, 1f);
    public float flashSeconds = 0.2f;

    [Header("Refused click")]
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

    // Hover: the mouse is over the deck; the lean and lift ease toward it every frame
    bool isHovered;
    float hoverShakeStart = -1f;
    float hoverAmount;                          // 0 = resting, 1 = fully lifted and grown
    Quaternion tilt = Quaternion.identity;      // in the deck's local space
    Vector3 boundsCentre;                       // the deck's middle at rest, for the lean

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
        bool hasRenderer = false;
        foreach (Transform part in parts)
        {
            if (part == null) continue;
            restPoses.Add(new Part { transform = part, restPosition = part.localPosition, restRotation = part.localRotation, restScale = part.localScale });

            foreach (Renderer r in part.GetComponentsInChildren<Renderer>())
            {
                // Start from the first piece, so the deck object's own pivot never pulls the middle off-centre
                if (!hasRenderer) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                hasRenderer = true;
                RememberTint(r);
            }
        }
        shakeWidth = bounds.size.x;
        boundsCentre = transform.InverseTransformPoint(bounds.center);
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
        isHovered = false;
        hoverShakeStart = -1f;
        hoverAmount = 0f;
        tilt = Quaternion.identity;
        isAtRest = false;
        ReturnToRest();
    }

    // ---------- Triggers ----------

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        hoverShakeStart = Time.time;
        if (flow != null && flow.IsReady && flow.CanSwapSelectedCard) wobbleStarts.Add(Time.time);    // "click me"
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
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
    }

    void OnSwapRejected(SwapRejectReason reason)
    {
        shakeStart = Time.time;
        PlayFlash(rejectFlashColor);
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
        float hoverShake = Decaying(ref hoverShakeStart, hoverShakeSeconds, hoverShakeCycles) * hoverShakeDegrees;
        UpdateHover();

        if (wobbleStarts.Count == 0 && punches.Count == 0 && shakeStart < 0f && flashStart < 0f &&
            pendingDrawPulses.Count == 0 && hoverShakeStart < 0f && IsHoverAtRest())
        {
            ReturnToRest();
            return;
        }

        isAtRest = false;
        ApplyPose(wobble, shake, scale * Mathf.Lerp(1f, hoverScale, hoverAmount), GetHoverRotation(hoverShake), GetHoverLift());
        ApplyFlash(flash);
    }

    // ---------- Hover ----------

    // Lean and lift ease toward their targets: toward the mouse while hovered, back to rest after
    void UpdateHover()
    {
        float ease = hoverSpeed <= 0f ? 1f : 1f - Mathf.Exp(-hoverSpeed * Time.deltaTime);
        hoverAmount = Mathf.Lerp(hoverAmount, isHovered ? 1f : 0f, ease);
        tilt = Quaternion.Slerp(tilt, isHovered ? GetTiltTowardMouse() : Quaternion.identity, ease);
    }

    bool IsHoverAtRest()
    {
        return !isHovered && hoverAmount < 0.001f && Quaternion.Angle(tilt, Quaternion.identity) < 0.01f;
    }

    // The same lean as a hand card: the mouse's offset from the deck's middle, worked out in camera space
    Quaternion GetTiltTowardMouse()
    {
        Camera cam = Camera.main;
        if (cam == null || Mouse.current == null) return Quaternion.identity;

        Vector3 centre = cam.WorldToScreenPoint(transform.TransformPoint(boundsCentre));
        if (centre.z <= 0f) return Quaternion.identity;

        Vector2 offset = (Mouse.current.position.ReadValue() - (Vector2)centre) / Mathf.Max(1f, tiltRadiusPixels);
        offset = Vector2.ClampMagnitude(offset, 1f) * maxTiltDegrees;
        float sign = pressUnderMouse ? 1f : -1f;

        Quaternion worldTilt = Quaternion.AngleAxis(sign * offset.y, cam.transform.right) *
                               Quaternion.AngleAxis(-sign * offset.x, cam.transform.up);
        return Quaternion.Inverse(transform.rotation) * worldTilt * transform.rotation;
    }

    // The lean plus the arrival shake, which turns around the axis pointing at the camera (flat on screen)
    Quaternion GetHoverRotation(float shakeDegrees)
    {
        Camera cam = Camera.main;
        if (cam == null || Mathf.Abs(shakeDegrees) < 0.001f) return tilt;
        Vector3 towardCamera = transform.InverseTransformDirection(-cam.transform.forward);
        return tilt * Quaternion.AngleAxis(shakeDegrees, towardCamera);
    }

    // Up on screen, in the deck's local space
    Vector3 GetHoverLift()
    {
        Camera cam = Camera.main;
        if (cam == null || hoverAmount < 0.001f) return Vector3.zero;
        return transform.InverseTransformVector(cam.transform.up * hoverLift * hoverAmount);
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

    // The whole deck moves as one piece around its middle: every part is scaled, turned and shifted
    // together, so the stack never comes apart. The hover lean turns it around its visible middle.
    void ApplyPose(float wobbleDegreesNow, float shakeWorld, float scale, Quaternion hoverRotation, Vector3 lift)
    {
        Camera cam = Camera.main;
        Vector3 shakeLocal = cam != null ? transform.InverseTransformVector(cam.transform.right * shakeWorld) : Vector3.zero;
        Quaternion turn = hoverRotation * Quaternion.AngleAxis(wobbleDegreesNow, Vector3.up);

        foreach (Part p in restPoses)
        {
            if (p.transform == null) continue;
            Vector3 fromMiddle = p.restPosition * scale - boundsCentre;
            p.transform.localPosition = boundsCentre + turn * fromMiddle + shakeLocal + lift;
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
