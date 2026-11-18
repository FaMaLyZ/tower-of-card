using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Blind menu effects (C6):
///   A new Ante: "ANTE 2" slams over the screen and flies into the menu's header (once per Ante, not every blind).
///   Select: the chosen blind's chip punches and throws a few gold sparks as the menu slides away.
///   Skip: a small "SKIPPED" slams into that blind's status (the Glass heal, if any, plays on the left).
/// Put it on any object in the game scene. Listen-only: delete it and the menu works the same.
public class BlindMenuJuice : MonoBehaviour
{
    [Header("Game references")]
    public HudTheme theme;

    [Header("Menu parts")]
    public TMP_Text header;
    [Tooltip("The big chips of Small, Big and Boss, in that order")]
    public List<Image> chips = new List<Image>();
    [Tooltip("The status texts of Small, Big and Boss, in that order")]
    public List<TMP_Text> statuses = new List<TMP_Text>();

    [Header("Effects")]
    public ScreenSlam slam;
    public UiShardBurst burst;

    [Header("Words")]
    [Tooltip("{0} = Ante number")]
    public string anteFormat = "ANTE {0}";
    public string skippedWord = "SKIPPED";

    [Header("Drama")]
    [Tooltip("Drama of the Ante slam for the first and the last Ante (climbs in between)")]
    public Vector2 anteDrama = new Vector2(0.3f, 0.75f);
    public int antesPerRun = 4;
    public float skippedDrama = 0.1f;
    public float selectPunch = 0.35f;

    int lastAnteShown;
    readonly List<UiPunch> chipPunches = new List<UiPunch>();

    void Awake()
    {
        if (slam == null) slam = FindAnyObjectByType<ScreenSlam>();
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();
        foreach (Image chip in chips) chipPunches.Add(chip != null ? new UiPunch(chip.rectTransform) : null);
    }

    void OnEnable()
    {
        GameEvents.BlindOffered += OnBlindOffered;
        GameEvents.BlindStarted += OnBlindStarted;
        GameEvents.BlindSkipped += OnBlindSkipped;
        GameEvents.RunEnded += OnRunEnded;
    }

    void OnDisable()
    {
        GameEvents.BlindOffered -= OnBlindOffered;
        GameEvents.BlindStarted -= OnBlindStarted;
        GameEvents.BlindSkipped -= OnBlindSkipped;
        GameEvents.RunEnded -= OnRunEnded;
        foreach (UiPunch punch in chipPunches) punch?.ReturnToRest();
    }

    // Once per Ante: the first time its menu opens
    void OnBlindOffered(BlindKind kind, int ante, long target)
    {
        if (ante == lastAnteShown) return;
        lastAnteShown = ante;
        PlayAnte(ante);
    }

    /// The Ante slam (public for the juice debug panel)
    public void PlayAnte(int ante)
    {
        if (slam == null || header == null) return;
        float progress = antesPerRun > 1 ? Mathf.Clamp01((ante - 1f) / (antesPerRun - 1f)) : 0f;
        slam.Play(string.Format(anteFormat, ante), header, theme != null ? theme.goldLight : Color.yellow,
                  Mathf.Lerp(anteDrama.x, anteDrama.y, progress), null, hideTarget: true);
    }

    void OnBlindStarted(BlindKind kind, int ante, long target)
    {
        int index = (int)kind;
        if (index < 0 || index >= chipPunches.Count || chipPunches[index] == null) return;

        chipPunches[index].Play(selectPunch, 0f, 0.3f);
        if (burst != null) burst.Emit(MiddleOf(chips[index].rectTransform), 14, Gold, Pale, 0.9f, 1.2f, 1f);
    }

    void OnBlindSkipped(BlindKind kind, int ante)
    {
        int index = (int)kind;
        if (slam == null || index < 0 || index >= statuses.Count || statuses[index] == null) return;
        slam.Play(skippedWord, statuses[index], Pale, skippedDrama, null, hideTarget: true);
    }

    // A new run starts again from Ante 1, which should slam again
    void OnRunEnded(long runTotal, bool cleared) => lastAnteShown = 0;

    void LateUpdate()
    {
        foreach (UiPunch punch in chipPunches) punch?.Apply();
    }

    Color Gold => theme != null ? theme.gold : Color.yellow;
    Color Pale => theme != null ? theme.cream : Color.white;

    static Vector3 MiddleOf(RectTransform rect) => rect.TransformPoint(rect.rect.center);
}
