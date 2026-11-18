using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Run end effects (C6):
///   Cleared:   "RUN CLEARED" slams at full drama into the end panel's title, then gold confetti bursts
///              across the top of the screen in waves.
///   Shattered: every Glass crystal blows apart into red shards, then "GLASS SHATTERED" slams in red.
/// When the title lands, the Run Score counts up from 0 in its marquee.
/// Put it on any object in the game scene. Listen-only: delete it and the end panel just shows its numbers.
public class RunEndJuice : MonoBehaviour
{
    [Header("Game references")]
    public HudTheme theme;

    [Header("End panel parts")]
    public TMP_Text title;
    [Tooltip("The Run Score in the marquee; needs a NumberRoller to count up")]
    public NumberRoller runScore;

    [Header("Effects")]
    public ScreenSlam slam;
    public UiShardBurst burst;
    public GlassJuice glass;

    [Header("Words")]
    public string clearedWords = "RUN CLEARED";
    public string shatteredWords = "GLASS SHATTERED";

    [Header("Timing")]
    [Tooltip("After the crystals shatter, the words slam in this much later")]
    public float slamAfterShatter = 0.5f;

    [Header("Confetti (cleared)")]
    public int confettiWaves = 4;
    public float secondsBetweenWaves = 0.35f;
    public int confettiPerBurst = 40;

    readonly List<float> waveTimes = new List<float>();
    float slamAt = -1f;
    bool pendingCleared;

    void Awake()
    {
        if (slam == null) slam = FindAnyObjectByType<ScreenSlam>();
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();
        if (glass == null) glass = FindAnyObjectByType<GlassJuice>();
    }

    void OnEnable() => GameEvents.RunEnded += OnRunEnded;

    void OnDisable()
    {
        GameEvents.RunEnded -= OnRunEnded;
        waveTimes.Clear();
        slamAt = -1f;
        if (runScore != null) runScore.Release();
    }

    void OnRunEnded(long runTotal, bool cleared) => Play(cleared);

    /// The whole ending (public for the juice debug panel; without a real end the panel stays shut)
    public void Play(bool cleared)
    {
        if (runScore != null) runScore.StartFrom(0);
        pendingCleared = cleared;

        if (cleared)
        {
            slamAt = Time.unscaledTime;
            return;
        }
        if (glass != null) glass.ShatterAll();
        slamAt = Time.unscaledTime + slamAfterShatter;
    }

    void Update()
    {
        if (slamAt >= 0f && Time.unscaledTime >= slamAt)
        {
            slamAt = -1f;
            SlamTitle(pendingCleared);
        }
        FireDueConfetti();
    }

    void SlamTitle(bool cleared)
    {
        if (slam == null || title == null)
        {
            Landed(cleared);
            return;
        }
        Color accent = cleared ? (theme != null ? theme.goldLight : Color.yellow) : (theme != null ? theme.cream : Color.white);
        title.color = cleared ? (theme != null ? theme.goldText : Color.yellow) : (theme != null ? theme.danger : Color.red);
        slam.Play(cleared ? clearedWords : shatteredWords, title, accent, cleared ? 1f : 0.9f, () => Landed(cleared), hideTarget: true);
    }

    void Landed(bool cleared)
    {
        if (runScore != null) runScore.Release();
        if (!cleared) return;

        for (int i = 0; i < confettiWaves; i++) waveTimes.Add(Time.unscaledTime + i * secondsBetweenWaves);
    }

    // Each wave: a few bursts spread along the top of the screen, raining down
    void FireDueConfetti()
    {
        if (burst == null) return;
        for (int i = waveTimes.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime < waveTimes[i]) continue;
            waveTimes.RemoveAt(i);
            for (int b = 0; b < 5; b++) burst.Emit(PointAlongTop((b + Random.value) / 5f), confettiPerBurst, Gold, Pale, 1.4f, 1.8f, 2f);
        }
    }

    Vector3 PointAlongTop(float across)
    {
        var layer = (RectTransform)burst.transform;
        Rect r = layer.rect;
        return layer.TransformPoint(new Vector2(Mathf.Lerp(r.xMin, r.xMax, across), r.yMax - r.height * 0.15f));
    }

    Color Gold => theme != null ? theme.gold : Color.yellow;
    Color Pale => theme != null ? theme.cream : Color.white;
}
