using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The left HUD (HUD Design §3): Run Score, Turn Score, Ante | Blind, Hands left.
/// The Glass stars have their own view (GlassStarsView). A view: it only reads the game.
public class LeftHudView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public ScoreManager scoreManager;

    [Header("Displays")]
    public TMP_Text runScoreNumber;
    public TMP_Text turnScoreNumber;
    public TMP_Text anteValue;
    public TMP_Text blindValue;
    public TMP_Text handsLeftText;
    public PipRow handsPips;

    [Header("Buttons")]
    public Button runInfoButton;
    public Button optionsButton;
    public HudPopup runInfoPanel;
    [Tooltip("A mock-up for now: only Back works")]
    public HudPopup optionsPanel;

    [Header("Text formats")]
    public string runScoreFormat = "{0:N0}";
    public string turnScoreFormat = "{0:N0}";
    [Tooltip("Shown until the current hand is scored")]
    public string noTurnScoreText = "–";
    [Tooltip("{0} = Ante, {1} = Antes per run")]
    public string anteFormat = "{0}/{1}";
    [Tooltip("{0} = blind number (1 Small, 2 Big, 3 Boss), {1} = blinds per Ante, {2} = retry mark")]
    public string blindFormat = "{0}/{1}{2}";
    public string retryMark = " R";
    [Tooltip("{0} = hands left")]
    public string handsLeftFormat = "{0} left";

    bool GameRunning => flow != null && flow.IsReady && flow.Phase != GamePhase.None;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        HookButtons();
    }

    void HookButtons()
    {
        if (runInfoButton != null && runInfoPanel != null) runInfoButton.onClick.AddListener(runInfoPanel.Open);
        if (optionsButton != null && optionsPanel != null) optionsButton.onClick.AddListener(optionsPanel.Open);
    }

    void Update()
    {
        if (!GameRunning) return;

        RefreshRunScore();
        RefreshTurnScore();
        RefreshAnteAndBlind();
        RefreshHands();
    }

    void RefreshRunScore()
    {
        SetText(runScoreNumber, string.Format(runScoreFormat, flow.RunTotal));
    }

    // "–" until the hand is scored, then the score stays through Shake and Bank / Ride
    void RefreshTurnScore()
    {
        bool scored = scoreManager != null && scoreManager.HasResult && !scoreManager.IsScoring;
        SetText(turnScoreNumber, scored ? string.Format(turnScoreFormat, scoreManager.TurnScore) : noTurnScoreText);
    }

    void RefreshAnteAndBlind()
    {
        SetText(anteValue, string.Format(anteFormat, flow.AnteNumber, flow.AntesPerRun));
        SetText(blindValue, string.Format(blindFormat, flow.BlindNumber, flow.BlindsPerAnte, flow.IsRetry ? retryMark : ""));
    }

    // In the blind menu the next blind has not started yet, but it will have every hand: show them all lit
    void RefreshHands()
    {
        int handsLeft = flow.Phase == GamePhase.BlindMenu ? flow.HandsPerBlind : flow.HandsLeft;
        SetText(handsLeftText, string.Format(handsLeftFormat, handsLeft));
        if (handsPips != null) handsPips.Show(handsLeft, flow.HandsPerBlind);
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }
}
