using System;
using TMPro;
using UnityEngine;

/// The Run Info panel (HUD Design §10.5): one row per poker hand with its building name, poker name,
/// base chips and mult, read from the scorer's table so it always matches the real scoring.
public class RunInfoView : MonoBehaviour
{
    [Serializable]
    public class HandRow
    {
        public TMP_Text buildingName;
        public TMP_Text pokerName;
        public TMP_Text baseChips;
        public TMP_Text mult;
    }

    [Header("Game references")]
    public ScoreManager scoreManager;

    [Header("Rows, weakest hand first. The last row is the Royal Flush (Palace)")]
    public HandRow[] rows = new HandRow[10];

    void Awake()
    {
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
    }

    // Filled every time the panel opens, so tuning the table in the Inspector shows up at once
    void OnEnable()
    {
        if (scoreManager == null) return;

        PokerHand[] hands = (PokerHand[])Enum.GetValues(typeof(PokerHand));
        for (int i = 0; i < hands.Length && i < rows.Length; i++)
            FillRow(rows[i], hands[i], PokerHandEvaluator.GetShapeName(hands[i]), PokerHandEvaluator.GetName(hands[i]));

        // A Royal Flush scores as a Straight Flush; only its names differ
        if (rows.Length > hands.Length)
            FillRow(rows[hands.Length], PokerHand.StraightFlush, "Palace", "Royal Flush");
    }

    void FillRow(HandRow row, PokerHand hand, string building, string poker)
    {
        if (row == null) return;
        HandTypeScore entry = scoreManager.GetEntry(hand);
        SetText(row.buildingName, building);
        SetText(row.pokerName, poker);
        SetText(row.baseChips, entry.chips.ToString());
        SetText(row.mult, entry.mult.ToString());
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }
}
