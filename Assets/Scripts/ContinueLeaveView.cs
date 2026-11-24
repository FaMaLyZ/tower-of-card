using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The Continue / Leave Table choice after a Bank that reached the target with hands left (HUD Design §10.2).
/// A view: shows the panel and sends the click to the conductor.
public class ContinueLeaveView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("UI")]
    public GameObject panel;
    public TMP_Text safeText;
    public TMP_Text bodyText;
    public Button continueButton;
    public Button leaveButton;
    public TMP_Text continueLabel;
    public TMP_Text leaveLabel;

    [Header("Texts")]
    [Tooltip("{0} = Safe Points, {1} = target")]
    public string safeFormat = "Safe {0:N0} / {1:N0}";
    [Tooltip("{0} = hands left")]
    public string bodyFormat = "{0} hands left. Keep playing for more, or leave now for a sure bonus.";
    public string bodyOneHand = "1 hand left. Keep playing for more, or leave now for a sure bonus.";
    public string continueText = "Continue";
    [Tooltip("{0} = the leave bonus, {1} = hands left")]
    public string leaveFormat = "Leave table +{0:N0}";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();

        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    void Update()
    {
        bool choosing = flow != null && flow.IsChoosingContinueOrLeave;
        ShowChoice(choosing);
        if (choosing) RefreshTexts();
    }

    void ShowChoice(bool show)
    {
        PanelSlideJuice.Show(panel, show);     // the buttons are inside the panel and leave with it
    }

    void RefreshTexts()
    {
        if (safeText != null) safeText.text = string.Format(safeFormat, flow.SafePoints, flow.Target);
        if (bodyText != null) bodyText.text = flow.HandsLeft == 1 ? bodyOneHand : string.Format(bodyFormat, flow.HandsLeft);
        if (continueLabel != null) continueLabel.text = continueText;
        if (leaveLabel != null) leaveLabel.text = string.Format(leaveFormat, flow.LeaveBonusQuote, flow.HandsLeft);
    }

    // ---------- Button actions (public, so they can also be wired in the Inspector) ----------

    public void OnContinueClicked()
    {
        if (flow != null) flow.RequestContinue();
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }

    public void OnLeaveClicked()
    {
        if (flow != null) flow.RequestLeaveTable();
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }
}
