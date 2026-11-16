using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The Continue / Leave Table choice after a Bank that reached the target with hands left.
/// A view: shows the buttons and sends the click to the conductor.
public class ContinueLeaveView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("UI")]
    public GameObject panel;
    public Button continueButton;
    public Button leaveButton;
    public TMP_Text continueLabel;
    public TMP_Text leaveLabel;

    [Header("Texts")]
    public string continueText = "Continue";
    [Tooltip("{0} = the leave bonus, {1} = hands left")]
    public string leaveFormat = "Leave Table +{0:N0}";

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
        if (choosing) RefreshLabels();
    }

    void ShowChoice(bool show)
    {
        if (panel != null) panel.SetActive(show);
        if (continueButton != null) continueButton.gameObject.SetActive(show);
        if (leaveButton != null) leaveButton.gameObject.SetActive(show);
    }

    void RefreshLabels()
    {
        if (continueLabel != null) continueLabel.text = continueText;
        if (leaveLabel != null) leaveLabel.text = string.Format(leaveFormat, flow.LeaveBonusQuote, flow.HandsLeft);
    }

    // ---------- Button actions (public, so they can also be wired in the Inspector) ----------

    public void OnContinueClicked()
    {
        if (flow != null) flow.RequestContinue();
        AudioManager.UISFX(SoundType.UI_Button);
    }

    public void OnLeaveClicked()
    {
        if (flow != null) flow.RequestLeaveTable();
        AudioManager.UISFX(SoundType.UI_Button);
    }
}
