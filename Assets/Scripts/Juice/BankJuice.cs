using TMPro;
using UnityEngine;

/// Bank effects (C5): the payout travels Pot → Safe → Run Score.
///   1. "+336" lifts off the Pot sign (which counts down to 0) and flies into the Safe bar.
///   2. It lands: Safe counts up and the bar fills along with it.
///   3. A gold "+336" flies on into the Run Score, which counts up as it lands.
/// The numbers are already banked; this only holds each display at its old value until its flight arrives.
/// Put it on any object in the game scene. Listen-only: delete it and the numbers just change at once.
public class BankJuice : MonoBehaviour
{
    [Header("Parts")]
    public FloatingNumbers floating;
    public TMP_Text potNumber;
    [Tooltip("The Safe bar's amount text (needs a NumberRoller)")]
    public NumberRoller safeAmount;
    [Tooltip("The Run Score number (needs a NumberRoller)")]
    public NumberRoller runScore;
    public HudTheme theme;

    [Header("Timing")]
    [Tooltip("Pause after Safe lands before the second flight leaves for the Run Score")]
    public float pauseBeforeRunScore = 0.15f;

    float secondFlightAt = -1f;
    long pendingPayout;

    void Awake()
    {
        if (floating == null) floating = FindAnyObjectByType<FloatingNumbers>();
    }

    void OnEnable() => GameEvents.PotBanked += OnPotBanked;

    void OnDisable()
    {
        GameEvents.PotBanked -= OnPotBanked;
        secondFlightAt = -1f;
        Release(safeAmount);
        Release(runScore);
    }

    void OnPotBanked(long payout, long safePoints, long runTotal)
    {
        if (floating == null || potNumber == null || safeAmount == null) return;

        Hold(safeAmount);
        Hold(runScore);
        floating.Fly(string.Format("+{0:N0}", payout), MiddleOf(potNumber.rectTransform), RectOf(safeAmount),
                     Safe, Gold, () => OnReachedSafe(payout));
    }

    void OnReachedSafe(long payout)
    {
        Release(safeAmount);
        if (runScore == null) return;

        pendingPayout = payout;
        secondFlightAt = Time.unscaledTime + pauseBeforeRunScore;
    }

    void Update()
    {
        if (secondFlightAt < 0f || Time.unscaledTime < secondFlightAt) return;
        secondFlightAt = -1f;

        floating.Fly(string.Format("+{0:N0}", pendingPayout), MiddleOf(RectOf(safeAmount)), RectOf(runScore),
                     Gold, Safe, () => Release(runScore));
    }

    // ---------- Helpers ----------

    Color Safe => theme != null ? theme.safe : Color.green;
    Color Gold => theme != null ? theme.gold : Color.yellow;

    static void Hold(NumberRoller roller)
    {
        if (roller != null) roller.Hold();
    }

    static void Release(NumberRoller roller)
    {
        if (roller != null) roller.Release();
    }

    static RectTransform RectOf(Component part) => part != null ? part.transform as RectTransform : null;

    static Vector3 MiddleOf(RectTransform rect) => rect != null ? rect.TransformPoint(rect.rect.center) : Vector3.zero;

    // ---------- Debug panel ----------

    /// The two flights with no Bank (nothing is held; the numbers just punch as each flight lands)
    public void DebugFlights(long payout)
    {
        if (floating == null || potNumber == null || safeAmount == null) return;
        floating.Fly(string.Format("+{0:N0}", payout), MiddleOf(potNumber.rectTransform), RectOf(safeAmount), Safe, Gold, () =>
        {
            safeAmount.Punch();
            if (runScore != null)
                floating.Fly(string.Format("+{0:N0}", payout), MiddleOf(RectOf(safeAmount)), RectOf(runScore), Gold, Safe, runScore.Punch);
        });
    }
}
