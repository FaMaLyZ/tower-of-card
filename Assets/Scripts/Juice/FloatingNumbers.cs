using System;
using TMPro;
using UnityEngine;

/// Little numbers that pop off things:
///   Pop: "+11" appears where a card scored, rises and fades.
///   Fly: "+260" lifts off the hand's total, then arcs into the Pot sign trailing sparks; the caller is told
///        when it arrives, so the Pot can count up at that moment.
/// Put it on the HUD's full-screen effects layer (HudFx). Display only: the numbers are already counted.
[RequireComponent(typeof(RectTransform))]
public class FloatingNumbers : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Font and material are copied from this text (e.g. a plaque number)")]
    public TMP_Text fontSource;
    public int poolSize = 16;

    [Header("Pop (rises and fades where it appears)")]
    public float popFontSize = 54f;
    public float popRisePixels = 80f;
    public float popSeconds = 0.75f;
    [Tooltip("How much bigger it starts before snapping to size")]
    public float popStartScale = 2f;

    [Header("Fly (lifts off, then arcs into a target)")]
    public float flyFontSize = 76f;
    public float liftPixels = 70f;
    public float liftSeconds = 0.22f;
    public float flySeconds = 0.5f;
    [Tooltip("How high the arc bows above the straight line, in canvas pixels")]
    public float arcPixels = 180f;
    [Tooltip("How small it gets by the time it lands")]
    public float landScale = 0.55f;
    public float trailSparksPerSecond = 70f;
    public int arrivalShards = 26;

    [Header("Optional effects")]
    public UiShardBurst burst;

    enum Kind { Pop, Fly }

    class Item
    {
        public TextMeshProUGUI text;
        public bool alive;
        public Kind kind;
        public float start;
        public Vector3 from;
        public RectTransform target;
        public Color sparkColor;
        public float sparkDebt;
        public Action arrived;
    }

    Item[] items;
    int next;

    void Awake()
    {
        if (burst == null) burst = GetComponent<UiShardBurst>();
        items = new Item[Mathf.Max(1, poolSize)];
        for (int i = 0; i < items.Length; i++) items[i] = new Item { text = MakeText(i) };
    }

    TextMeshProUGUI MakeText(int index)
    {
        var go = new GameObject("Floating" + index, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(600f, 140f);
        go.SetActive(false);
        return text;
    }

    // ---------- Starting one ----------

    /// "+11" at a point on the canvas: pops in big, rises and fades
    public void Pop(string words, Vector3 worldPosition, Color color)
    {
        Item item = Take(words, color, popFontSize);
        item.kind = Kind.Pop;
        item.from = worldPosition;
        AudioManager.Instance.PlayOneShotSFX(SoundType.ScorePopUp_Pop);
    }

    /// "+260" lifts off `fromWorld`, then arcs into `target`; `arrived` runs when it lands (or is cut short)
    public void Fly(string words, Vector3 fromWorld, RectTransform target, Color color, Color sparkColor, Action arrived)
    {
        Item item = Take(words, color, flyFontSize);
        item.kind = Kind.Fly;
        item.from = fromWorld;
        item.target = target;
        item.sparkColor = sparkColor;
        item.arrived = arrived;
        AudioManager.Instance.PlayOneShotSFX(SoundType.ScorePopUp_Fly);
    }

    Item Take(string words, Color color, float fontSize)
    {
        if (items == null) Awake();
        Item item = items[next];
        next = (next + 1) % items.Length;
        Finish(item);                                   // a reused item lands its old flight at once

        CopyLook(item.text, words, color, fontSize);
        item.alive = true;
        item.start = Time.unscaledTime;
        item.sparkDebt = 0f;
        item.text.gameObject.SetActive(true);
        item.text.transform.SetAsLastSibling();
        return item;
    }

    void CopyLook(TextMeshProUGUI text, string words, Color color, float fontSize)
    {
        if (fontSource != null)
        {
            text.font = fontSource.font;
            text.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
        text.fontSize = fontSize;
        text.color = color;
        text.text = words;
    }

    // ---------- Every frame ----------

    void Update()
    {
        if (items == null) return;
        foreach (Item item in items)
        {
            if (!item.alive) continue;
            float elapsed = Time.unscaledTime - item.start;
            if (item.kind == Kind.Pop) StepPop(item, elapsed);
            else StepFly(item, elapsed);
        }
    }

    // Snaps from big to full size, drifts up, fades over its last 40%
    void StepPop(Item item, float elapsed)
    {
        float t = elapsed / popSeconds;
        if (t >= 1f) { Finish(item); return; }

        float rise = 1f - (1f - t) * (1f - t);
        Place(item, item.from + Up(popRisePixels * rise), Mathf.Lerp(popStartScale, 1f, Mathf.Clamp01(t * 6f)),
              t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
    }

    // Lifts off with a little punch, then curves into the target, shrinking and trailing sparks
    void StepFly(Item item, float elapsed)
    {
        Vector3 top = item.from + Up(liftPixels);
        if (elapsed < liftSeconds)
        {
            float t = elapsed / liftSeconds;
            Place(item, Vector3.Lerp(item.from, top, 1f - (1f - t) * (1f - t)), 1f + 0.3f * Mathf.Sin(t * Mathf.PI), 1f);
            return;
        }

        float u = (elapsed - liftSeconds) / flySeconds;
        if (u >= 1f) { Arrive(item); return; }

        float eased = u * u;                            // speeds up into the sign
        Vector3 end = MiddleOf(item.target);
        Vector3 control = (top + end) * 0.5f + Up(arcPixels);
        Place(item, Bezier(top, control, end, eased), Mathf.Lerp(1f, landScale, eased), 1f);
        EmitTrail(item);
    }

    void EmitTrail(Item item)
    {
        if (burst == null) return;
        item.sparkDebt += trailSparksPerSecond * Time.unscaledDeltaTime;
        int sparks = Mathf.FloorToInt(item.sparkDebt);
        if (sparks <= 0) return;

        item.sparkDebt -= sparks;
        burst.Emit(item.text.rectTransform.position, sparks, item.sparkColor, item.text.color, 0.35f, 0.8f, 0.8f);
    }

    void Arrive(Item item)
    {
        if (burst != null) burst.Emit(MiddleOf(item.target), arrivalShards, item.sparkColor, item.text.color, 1.1f, 1.3f, 1f);
        Finish(item);
    }

    // Hides it and tells the caller (once)
    void Finish(Item item)
    {
        item.alive = false;
        item.text.gameObject.SetActive(false);
        Action arrived = item.arrived;
        item.arrived = null;
        arrived?.Invoke();
    }

    void OnDisable()
    {
        if (items == null) return;
        foreach (Item item in items)
            if (item != null && item.alive) Finish(item);
    }

    // ---------- Helpers ----------

    static void Place(Item item, Vector3 worldPosition, float scale, float alpha)
    {
        RectTransform rect = item.text.rectTransform;
        rect.position = worldPosition;
        rect.localScale = Vector3.one * scale;
        item.text.alpha = alpha;
    }

    // Canvas pixels upward, in world units (the canvas may be scaled)
    Vector3 Up(float pixels) => transform.TransformVector(Vector3.up * pixels);

    static Vector3 MiddleOf(RectTransform rect)
    {
        return rect != null ? rect.TransformPoint(rect.rect.center) : Vector3.zero;
    }

    static Vector3 Bezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }
}
