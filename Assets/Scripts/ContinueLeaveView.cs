using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The Continue / Leave Table choice, offered after a Bank that reached the target with hands left.
///
/// A view: it shows the two buttons, says what Leave pays, and sends the click to the conductor
/// as a command. It never touches the blind score itself.
/// Every field is optional; build the buttons in the editor and drag them in.
public class ContinueLeaveView : MonoBehaviour
{
    [Header("Game references (auto-found if empty)")]
    public GameFlowController flow;

    [Header("UI")]
    [Tooltip("Optional: shown only while the player is choosing. Leave empty to show / hide the two buttons instead.")]
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
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }

    public void OnLeaveClicked()
    {
        if (flow != null) flow.RequestLeaveTable();
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }
}
