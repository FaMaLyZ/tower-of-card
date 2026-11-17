using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Broken strip effects (C3): each cracked-card icon pops in with a little burst of fragments the moment
/// its card breaks on the tower, and the icons flicker until the shake settles.
/// Put it on the BrokenStrip object. Listen-only: BrokenStripView still decides which icons show.
public class BrokenStripJuice : MonoBehaviour
{
    [Header("Parts")]
    public GameFlowController flow;
    [Tooltip("The same icons as BrokenStripView, left to right")]
    public List<Image> icons = new List<Image>();
    public UiShardBurst burst;
    public HudTheme theme;

    [Header("Pop in")]
    public float popScale = 1.6f;
    public float popSeconds = 0.25f;
    public int shardsPerIcon = 8;
    [Tooltip("Fragment speed compared with the burst layer's own setting")]
    public float shardStrength = 0.6f;

    [Header("Flicker while the tower settles")]
    public float flickerSpeed = 14f;
    [Range(0f, 1f)] public float flickerLowestAlpha = 0.4f;

    bool[] wasShown;
    float[] popStarts;
    Color[] restColors;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();

        wasShown = new bool[icons.Count];
        popStarts = new float[icons.Count];
        restColors = new Color[icons.Count];
        for (int i = 0; i < icons.Count; i++)
        {
            popStarts[i] = -1f;
            if (icons[i] != null) restColors[i] = icons[i].color;
        }
    }

    // After BrokenStripView.Update has switched the icons on or off for this frame
    void LateUpdate()
    {
        bool shaking = flow != null && flow.Phase == GamePhase.Shake;
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null) continue;

            bool shown = icons[i].gameObject.activeSelf;
            if (shown && !wasShown[i] && shaking) Pop(i);
            wasShown[i] = shown;

            ApplyScale(i);
            ApplyFlicker(i, shaking);
        }
    }

    void Pop(int index)
    {
        popStarts[index] = Time.unscaledTime;
        if (burst == null) return;

        Color cream = theme != null ? theme.cream : Color.white;
        Color red = theme != null ? theme.danger : Color.red;
        burst.Emit(icons[index].rectTransform.position, shardsPerIcon, cream, red, shardStrength);
    }

    // From nothing up past full size, then settles: 0 -> popScale -> 1
    void ApplyScale(int index)
    {
        float scale = 1f;
        if (popStarts[index] >= 0f)
        {
            float t = (Time.unscaledTime - popStarts[index]) / popSeconds;
            if (t >= 1f) popStarts[index] = -1f;
            else scale = t < 0.4f ? Mathf.Lerp(0f, popScale, t / 0.4f) : Mathf.Lerp(popScale, 1f, (t - 0.4f) / 0.6f);
        }
        icons[index].rectTransform.localScale = Vector3.one * scale;
    }

    void ApplyFlicker(int index, bool shaking)
    {
        Color color = restColors[index];
        if (shaking)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime * flickerSpeed) + index * 1.3f);
            color.a *= Mathf.Lerp(flickerLowestAlpha, 1f, wave);
        }
        icons[index].color = color;
    }
}
