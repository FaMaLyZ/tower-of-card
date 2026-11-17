using UnityEngine;

/// Slides a panel up from below the screen when it is shown, and back down before it is hidden
/// (blind menu, blind result, Continue / Leave, Run Info, Options).
/// Presentation only: views call PanelSlideJuice.Show(panel, shown); with this component missing or off,
/// that simply switches the panel on or off. It moves its own RectTransform and always ends at rest.
[RequireComponent(typeof(RectTransform))]
public class PanelSlideJuice : MonoBehaviour
{
    [Tooltip("How far below its rest place the panel travels, in canvas pixels")]
    public float travel = 1100f;
    [Min(0.01f)] public float slideUpSeconds = 0.35f;
    [Min(0.01f)] public float slideDownSeconds = 0.25f;
    [Tooltip("How far it overshoots before settling when sliding up (0 = no bounce)")]
    [Range(0f, 0.2f)] public float overshoot = 0.04f;

    enum Motion { None, Up, Down }

    RectTransform rect;
    Vector2 restPosition;
    Motion motion = Motion.None;
    float motionStart;
    float dropAtStart;        // how far below rest the panel was when the current motion began

    /// Shows or hides a panel, sliding if it has an active PanelSlideJuice. Safe to call every frame.
    public static void Show(GameObject panel, bool shown)
    {
        if (panel == null) return;
        PanelSlideJuice slide = panel.GetComponent<PanelSlideJuice>();
        if (slide != null && slide.enabled) slide.SetShown(shown);
        else if (panel.activeSelf != shown) panel.SetActive(shown);
    }

    // Runs once: SetShown may call it before Unity does (the panel starts switched off),
    // and Unity's own call comes after the panel was already moved down, so it must not read the position again
    void Awake()
    {
        if (rect != null) return;
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
    }

    /// Up: switch on and rise from wherever it is. Down: sink, then switch off. Repeated calls do nothing.
    public void SetShown(bool shown)
    {
        if (rect == null) Awake();

        if (shown)
        {
            if (!gameObject.activeSelf)
            {
                SetDrop(travel);
                gameObject.SetActive(true);
                Begin(Motion.Up);
            }
            else if (motion == Motion.Down) Begin(Motion.Up);
        }
        else if (gameObject.activeSelf && motion != Motion.Down)
        {
            Begin(Motion.Down);
        }
    }

    void OnDisable()
    {
        if (rect != null) rect.anchoredPosition = restPosition;
        motion = Motion.None;
    }

    void Update()
    {
        if (motion == Motion.Up) StepUp();
        else if (motion == Motion.Down) StepDown();
    }

    void Begin(Motion next)
    {
        dropAtStart = restPosition.y - rect.anchoredPosition.y;
        motionStart = Time.unscaledTime;
        motion = next;
    }

    void StepUp()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - motionStart) / slideUpSeconds);
        SetDrop(dropAtStart * (1f - EaseOutBack(t)));
        if (t >= 1f)
        {
            SetDrop(0f);
            motion = Motion.None;
        }
    }

    void StepDown()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - motionStart) / slideDownSeconds);
        SetDrop(Mathf.Lerp(dropAtStart, travel, t * t));        // eases in: starts slow, falls away fast
        if (t >= 1f)
        {
            motion = Motion.None;
            gameObject.SetActive(false);                         // OnDisable puts it back at rest
        }
    }

    void SetDrop(float drop)
    {
        rect.anchoredPosition = restPosition - new Vector2(0f, drop);
    }

    // Rises fast, goes a little past its place, then settles
    float EaseOutBack(float t)
    {
        float s = overshoot * 40f;
        float p = t - 1f;
        return 1f + p * p * ((s + 1f) * p + s);
    }
}
