using UnityEngine;

/// The cashier drawer's motion (C5). It hangs under the HUD it belongs to and is drawn behind it:
///   Open:  slides DOWN out of the HUD, overshoots, and bounces up and down a few times before it settles.
///   Close: is pulled down a little first (stretching, like a string pulled tight), then snaps up into the HUD.
/// Its top edge never leaves the HUD: going lower than its rest place stretches it longer instead of moving it,
/// so there is never a gap between the HUD and the drawer.
/// Views call DrawerSlideJuice.Show(drawer, shown); with this component missing or off, that simply switches the
/// drawer on or off. It moves and stretches its own RectTransform and always ends exactly at rest.
[RequireComponent(typeof(RectTransform))]
public class DrawerSlideJuice : MonoBehaviour
{
    [Tooltip("How far up it hides behind the HUD, in canvas pixels (0 = its own height)")]
    public float travel = 0f;

    [Header("Open")]
    public float openSeconds = 0.7f;
    [Tooltip("Swings past its rest place before it settles (down, up, down...)")]
    [Range(1, 8)] public int swings = 4;
    [Tooltip("Each swing is this share of the one before (0.2 = a fifth as big)")]
    [Range(0f, 0.6f)] public float swingSize = 0.2f;

    [Header("Close")]
    [Tooltip("How much longer it is pulled before it snaps up, in canvas pixels")]
    public float pullPixels = 30f;
    public float pullSeconds = 0.14f;
    public float snapSeconds = 0.16f;

    enum Motion { Hidden, Opening, Open, Pulling, Snapping }

    RectTransform rect;
    Vector2 restPosition;
    Vector3 restScale;
    Motion motion = Motion.Hidden;
    float motionStart;
    float startOffset;          // how far above rest it was when the current motion began (negative = below)
    float offset;               // the same, right now: above 0 slides it up into the HUD, below 0 stretches it

    /// Shows or hides a drawer, sliding if it has an active DrawerSlideJuice. Safe to call every frame.
    public static void Show(GameObject drawer, bool shown)
    {
        if (drawer == null) return;
        DrawerSlideJuice slide = drawer.GetComponent<DrawerSlideJuice>();
        if (slide != null && slide.enabled) slide.SetShown(shown);
        else if (drawer.activeSelf != shown) drawer.SetActive(shown);
    }

    // Runs once: SetShown may call it before Unity does (the drawer starts switched off)
    void Awake()
    {
        if (rect != null) return;
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
        restScale = rect.localScale;
    }

    float HiddenOffset => travel > 0f ? travel : rect.rect.height;

    /// Open: switch on and drop out of the HUD. Close: pull, then snap up and switch off. Repeats do nothing.
    public void SetShown(bool shown)
    {
        if (rect == null) Awake();

        if (shown)
        {
            if (motion == Motion.Open || motion == Motion.Opening) return;
            if (motion == Motion.Hidden) offset = HiddenOffset;
            gameObject.SetActive(true);
            Begin(Motion.Opening);
        }
        else
        {
            if (motion == Motion.Hidden || motion == Motion.Pulling || motion == Motion.Snapping) return;
            if (!gameObject.activeInHierarchy) { FinishHidden(); return; }
            Begin(Motion.Pulling);
        }
        Apply();
    }

    void Begin(Motion next)
    {
        motion = next;
        motionStart = Time.unscaledTime;
        startOffset = offset;
    }

    void Update()
    {
        float elapsed = Time.unscaledTime - motionStart;
        switch (motion)
        {
            case Motion.Opening:  StepOpening(elapsed); break;
            case Motion.Pulling:  StepPulling(elapsed); break;
            case Motion.Snapping: StepSnapping(elapsed); break;
            default: return;
        }
        Apply();
    }

    // A swing that loses most of its size each time it passes rest: down past it, up past it, ... settled
    void StepOpening(float elapsed)
    {
        float t = elapsed / Mathf.Max(0.01f, openSeconds);
        if (t >= 1f)
        {
            offset = 0f;
            motion = Motion.Open;
            return;
        }

        float halfSwings = t * swings;
        offset = startOffset * Mathf.Cos(halfSwings * Mathf.PI) * Mathf.Pow(swingSize, halfSwings);
    }

    // Down a little, slowing as it stretches, like pulling a string tight
    void StepPulling(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, pullSeconds));
        float eased = 1f - (1f - t) * (1f - t);
        offset = Mathf.Lerp(startOffset, -pullPixels, eased);
        if (t >= 1f) Begin(Motion.Snapping);
    }

    // Let go: it shoots up into the HUD, speeding up, and the stretch springs back
    void StepSnapping(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, snapSeconds));
        offset = Mathf.Lerp(startOffset, HiddenOffset, t * t);
        if (t >= 1f) FinishHidden();
    }

    void FinishHidden()
    {
        motion = Motion.Hidden;
        offset = HiddenOffset;
        Apply();
        gameObject.SetActive(false);
    }

    // Above rest it slides up into the HUD. Below rest its top stays pinned and it stretches longer instead
    // (it hangs from its top edge, so a taller drawer reaches further down).
    void Apply()
    {
        float slide = Mathf.Max(0f, offset);
        float stretch = Mathf.Max(0f, -offset) / Mathf.Max(1f, rect.rect.height);
        rect.anchoredPosition = restPosition + new Vector2(0f, slide);
        rect.localScale = new Vector3(restScale.x, restScale.y * (1f + stretch), restScale.z);
    }

    // Switched off or removed mid-motion: leave it at rest so the drawer is never stuck out of place
    void OnDisable()
    {
        if (rect == null) return;
        if (motion == Motion.Pulling || motion == Motion.Snapping) motion = Motion.Hidden;
        if (motion == Motion.Opening) motion = Motion.Open;
        offset = motion == Motion.Hidden ? HiddenOffset : 0f;
        Apply();
    }
}
