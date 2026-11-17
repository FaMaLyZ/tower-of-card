using TMPro;
using UnityEngine;

/// The blind result panel (HUD Design §10.3), shown while a blind has just ended:
/// Won! or Failed, the blind score against the target, what was banked and left with,
/// and the reward change (on a win) or the Glass crack (on a fail).
/// The game moves on by itself after the pause in GameConfig, so the footer just says what comes next.
public class BlindResultView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public HudTheme theme;

    [Header("UI")]
    public GameObject panel;
    public TMP_Text blindName;
    public TMP_Text resultTitle;
    public TMP_Text scoreText;
    public TMP_Text detailText;
    public TMP_Text changeText;
    public TMP_Text footerText;

    [Header("Texts")]
    [Tooltip("{0} = Small / Big / Boss")]
    public string blindNameFormat = "{0} Blind";
    public string wonTitle = "Won!";
    public string failedTitle = "Failed";
    [Tooltip("{0} = blind score, {1} = target")]
    public string scoreFormat = "{0:N0} / {1:N0}";
    [Tooltip("{0} = banked this blind")]
    public string bankedFormat = "Banked {0:N0}";
    [Tooltip("{0} = banked this blind, {1} = leave bonus")]
    public string bankedAndLeftFormat = "Banked {0:N0} · Left the table +{1:N0}";
    [Tooltip("{0} = reward multiplier before, {1} = after")]
    public string rewardChangeFormat = "Reward ×{0:0.0#} » ×{1:0.0#}";
    [Tooltip("{0} = lives left, {1} = max lives")]
    public string glassCrackFormat = "The Glass cracked · {0} / {1} left";
    public string nextBlindText = "Next blind...";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void Update()
    {
        bool show = flow != null && flow.IsReady && flow.IsBlindOver;
        PanelSlideJuice.Show(panel, show);
        if (show) Refresh();
    }

    void Refresh()
    {
        bool won = flow.LastBlindResult == BlindResult.Won;

        SetText(blindName, string.Format(blindNameFormat, flow.LastBlindKind));
        SetText(resultTitle, won ? wonTitle : failedTitle);
        if (resultTitle != null && theme != null) resultTitle.color = won ? theme.goldText : theme.danger;

        SetText(scoreText, string.Format(scoreFormat, flow.SafePoints, flow.Target));
        SetText(detailText, flow.LeftTable
            ? string.Format(bankedAndLeftFormat, flow.BankedThisBlind, flow.LeaveBonusPaid)
            : string.Format(bankedFormat, flow.BankedThisBlind));
        SetText(changeText, won
            ? string.Format(rewardChangeFormat, flow.RewardMultiplierBeforeLastWin, flow.RewardMultiplier)
            : string.Format(glassCrackFormat, flow.GlassLives, flow.GlassMaxLives));
        if (changeText != null && theme != null) changeText.color = won ? theme.goldText : theme.danger;
        SetText(footerText, nextBlindText);
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }
}
