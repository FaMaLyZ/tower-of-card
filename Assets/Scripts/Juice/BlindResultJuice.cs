using TMPro;
using UnityEngine;

/// Blind result effects (C6):
///   Won:    "WON!" slams over the screen and flies into the result panel's title as the panel slides up.
///           Harder for a bigger blind and a bigger margin (a Boss won by double the target is near the top).
///   Failed: "FAILED" slams in red, heavy; the Glass crack follows once it has landed (GlassJuice waits).
/// When the slam lands, the score counts up from 0, the reward / crack line punches and confetti bursts.
/// The result screen waits for this (it holds PresentationGate, capped by the game).
/// Put it on any object in the game scene. Listen-only: delete it and the panel just shows its numbers.
public class BlindResultJuice : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public HudTheme theme;

    [Header("Result panel parts")]
    public TMP_Text resultTitle;
    [Tooltip("The score line ('366 / 150'); needs a NumberRoller to count up")]
    public NumberRoller score;
    [Tooltip("The reward / crack line; punched when the slam lands")]
    public TMP_Text changeLine;

    [Header("Effects")]
    public ScreenSlam slam;
    public UiShardBurst burst;

    [Header("Words")]
    public string wonWord = "WON!";
    public string failedWord = "FAILED";

    [Header("Drama")]
    [Tooltip("Drama for winning a Small, Big and Boss blind")]
    public Vector3 wonDramaByBlind = new Vector3(0.35f, 0.55f, 0.85f);
    [Tooltip("Extra drama for winning by double the target (scaled down for smaller margins)")]
    public float marginBonus = 0.15f;
    public float failedDrama = 0.65f;

    [Header("Landing")]
    public int confetti = 60;
    public float landPunch = 0.4f;

    UiPunch changePunch;
    bool holdingGate;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (slam == null) slam = FindAnyObjectByType<ScreenSlam>();
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();
        if (changeLine != null) changePunch = new UiPunch(changeLine.rectTransform);
    }

    void OnEnable()
    {
        GameEvents.BlindWon += OnBlindWon;
        GameEvents.BlindFailed += OnBlindFailed;
    }

    void OnDisable()
    {
        GameEvents.BlindWon -= OnBlindWon;
        GameEvents.BlindFailed -= OnBlindFailed;
        if (score != null) score.Release();
        changePunch?.ReturnToRest();
        ReleaseGate();
    }

    void OnBlindWon(long safePoints, long target) => Play(true, WonDrama(safePoints, target));

    void OnBlindFailed(long safePoints, long target) => Play(false, failedDrama);

    /// The slam and its landing (public for the juice debug panel; without a real result the panel stays shut)
    public void Play(bool won, float drama)
    {
        if (slam == null || resultTitle == null) return;

        HoldGate();
        if (score != null) score.StartFrom(0);

        resultTitle.color = won ? Gold : Red;           // the slam copies this colour (the view sets the same one)
        Color accent = won ? GoldLight : Pale;
        slam.Play(won ? wonWord : failedWord, resultTitle, accent, drama, () => Land(won, drama), hideTarget: true);
    }

    // The word has landed in the panel: count the score, punch the change line, throw confetti
    void Land(bool won, float drama)
    {
        if (score != null) score.Release();
        changePunch?.Play(landPunch, 0f, 0.35f);

        if (burst != null && resultTitle != null)
        {
            Vector3 middle = resultTitle.rectTransform.TransformPoint(resultTitle.rectTransform.rect.center);
            if (won) burst.Emit(middle, Mathf.RoundToInt(confetti * (0.5f + drama)), Gold, Pale, 1.6f, 1.6f, 1.5f);
            else burst.Emit(middle, confetti / 3, Red, Pale, 0.8f, 1.2f, 1f);
        }
        ReleaseGate();
    }

    // Bigger blind, bigger margin, bigger slam
    float WonDrama(long safePoints, long target)
    {
        BlindKind kind = flow != null ? flow.LastBlindKind : BlindKind.Small;
        float baseDrama = kind == BlindKind.Small ? wonDramaByBlind.x : kind == BlindKind.Big ? wonDramaByBlind.y : wonDramaByBlind.z;
        float overTarget = target > 0 ? Mathf.Clamp01((float)safePoints / target - 1f) : 0f;
        return Mathf.Clamp01(baseDrama + marginBonus * overTarget);
    }

    void LateUpdate() => changePunch?.Apply();

    Color Gold => theme != null ? theme.goldText : Color.yellow;
    Color GoldLight => theme != null ? theme.goldLight : Color.yellow;
    Color Red => theme != null ? theme.danger : Color.red;
    Color Pale => theme != null ? theme.cream : Color.white;

    void HoldGate()
    {
        if (holdingGate) return;
        holdingGate = true;
        PresentationGate.Hold();
    }

    void ReleaseGate()
    {
        if (!holdingGate) return;
        holdingGate = false;
        PresentationGate.Release();
    }
}
