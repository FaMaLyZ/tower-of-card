using TMPro;
using UnityEngine;

/// The hand plaque at the top (HUD Design §4): building name, poker name, and
/// base chips + card chips x mult, then "= score" once the hand is scored.
///
/// Before the hand is scored it shows the live preview of the picked cards (Commit) or the locked hand (Build).
/// A view: every number comes from the scorer; it never works anything out.
public class HandPlaqueView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public ScoreManager scoreManager;

    [Header("Parts")]
    [Tooltip("Fades the whole plaque by phase")]
    public CanvasGroup canvasGroup;
    public TMP_Text buildingName;
    public TMP_Text pokerName;
    public TMP_Text baseChipsNumber;
    public TMP_Text cardChipsNumber;
    public TMP_Text multNumber;
    public TMP_Text totalText;

    [Header("Text formats")]
    public string noHandTitle = "Your hand";
    [Tooltip("{0} = most cards a hand can use")]
    public string noHandSubtitleFormat = "Select 1-{0} cards";
    public string emptyChip = "–";
    public string chipsFormat = "{0:0}";
    public string multFormat = "{0:0.##}";
    [Tooltip("{0} = the hand's score")]
    public string totalFormat = "= {0:N0}";
    [Tooltip("{0} = poker name, {1} = kicker count. Kickers are picked cards outside the poker hand: they add no chips")]
    public string oneKickerFormat = "{0} · {1} kicker";
    public string manyKickersFormat = "{0} · {1} kickers";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;     // a display must never swallow clicks meant for the table
        }
    }

    void Update()
    {
        if (flow == null || !flow.IsReady) return;

        bool hidden = IsHidden(flow.Phase);
        SetAlpha(hidden ? 0f : 1f);
        if (hidden) return;

        if (IsBetweenBlinds(flow.Phase)) ShowNoHand();          // no old hand left over on the menu
        else if (scoreManager != null && scoreManager.HasResult) ShowScoredHand();
        else if (GetPreview() != null) ShowPreview(flow.Commit.Preview, GetPreview());
        else ShowNoHand();
    }

    // ---------- What to show ----------

    // The hand being scored (counting up), then its final score
    void ShowScoredHand()
    {
        int kickers = scoreManager.LastContext != null ? scoreManager.LastContext.KickerCount : 0;
        ShowNames(scoreManager.HandShapeName, WithKickers(scoreManager.HandName, kickers));
        ShowChips(scoreManager.BaseChips, scoreManager.CurrentCardChips, scoreManager.CurrentMult);
        ShowTotal(!scoreManager.IsScoring, scoreManager.TurnScore);
    }

    // Commit: the picked cards, updated on every pick. Build: the locked hand.
    void ShowPreview(PokerHandResult hand, ScoreContext score)
    {
        ShowNames(hand != null ? hand.shapeName : "", WithKickers(hand != null ? hand.displayName : "", score.KickerCount));
        ShowChips(score.baseChips, score.cardChips, score.mult);
        ShowTotal(false, 0);
    }

    void ShowNoHand()
    {
        int maxCards = flow.config != null ? flow.config.maxCommitCards : 5;
        ShowNames(noHandTitle, string.Format(noHandSubtitleFormat, maxCards));
        SetText(baseChipsNumber, emptyChip);
        SetText(cardChipsNumber, emptyChip);
        SetText(multNumber, emptyChip);
        ShowTotal(false, 0);
    }

    // "High Card · 1 kicker": says why some picked cards add no chips
    string WithKickers(string pokerName, int kickers)
    {
        if (kickers <= 0 || string.IsNullOrEmpty(pokerName)) return pokerName;
        return string.Format(kickers == 1 ? oneKickerFormat : manyKickersFormat, pokerName, kickers);
    }

    ScoreContext GetPreview()
    {
        if (flow.Phase != GamePhase.Commit && flow.Phase != GamePhase.Build) return null;
        return flow.Commit.PreviewScore;
    }

    // ---------- Visibility ----------

    // Hidden on the result panels; always fully bright otherwise (no dimming, by choice). It stays up during the
    // run intro and the blind menu (as "Your hand"), so it can arrive with the rest of the HUD (decided 2026-11-18)
    static bool IsHidden(GamePhase phase)
    {
        return phase == GamePhase.None || phase == GamePhase.ContinueOrLeave ||
               phase == GamePhase.BlindEnd || phase == GamePhase.RunEnd;
    }

    static bool IsBetweenBlinds(GamePhase phase) => phase == GamePhase.RunIntro || phase == GamePhase.BlindMenu;

    void SetAlpha(float alpha)
    {
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }

    // ---------- Helpers ----------

    void ShowNames(string building, string poker)
    {
        SetText(buildingName, string.IsNullOrEmpty(building) ? poker : building);
        SetText(pokerName, string.IsNullOrEmpty(building) ? "" : poker);
    }

    void ShowChips(float baseChips, float cardChips, float mult)
    {
        SetText(baseChipsNumber, string.Format(chipsFormat, baseChips));
        SetText(cardChipsNumber, string.Format(chipsFormat, cardChips));
        SetText(multNumber, string.Format(multFormat, mult));
    }

    void ShowTotal(bool visible, long score)
    {
        if (totalText == null) return;
        totalText.gameObject.SetActive(visible);
        if (visible) totalText.text = string.Format(totalFormat, score);
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }
}
