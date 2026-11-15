using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The Bank / Ride choice after a shake the tower survived.
///
/// A view: it shows the two buttons, says what Bank pays and why Ride is off, and sends the
/// click to the conductor as a command. It never touches the Pot or the tower itself.
/// Every field is optional; build the buttons in the editor and drag them in.
public class BankRideView : MonoBehaviour
{
    [Header("Game references (auto-found if empty)")]
    public GameFlowController flow;

    [Header("UI")]
    [Tooltip("Optional: shown only while the player is choosing. Leave empty to show / hide the two buttons instead.")]
    public GameObject panel;
    public Button bankButton;
    public Button rideButton;
    public TMP_Text bankLabel;
    public TMP_Text rideLabel;

    [Header("Texts")]
    [Tooltip("{0} = what Bank pays now")]
    public string bankFormat = "Bank {0:N0}";
    public string rideText = "Ride";
    public string towerFullText = "Tower full - Bank to continue";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();

        if (bankButton != null) bankButton.onClick.AddListener(OnBankClicked);
        if (rideButton != null) rideButton.onClick.AddListener(OnRideClicked);
    }

    void Update()
    {
        bool choosing = flow != null && flow.IsChoosingBankOrRide;
        ShowChoice(choosing);
        if (choosing) RefreshButtons();
    }

    void ShowChoice(bool show)
    {
        if (panel != null) panel.SetActive(show);
        if (bankButton != null) bankButton.gameObject.SetActive(show);
        if (rideButton != null) rideButton.gameObject.SetActive(show);
    }

    void RefreshButtons()
    {
        if (bankLabel != null) bankLabel.text = string.Format(bankFormat, flow.BankQuote);
        if (rideLabel != null) rideLabel.text = flow.CanRide ? rideText : towerFullText;
        if (rideButton != null) rideButton.interactable = flow.CanRide;     // feedback only: the conductor checks again
    }

    // ---------- Button actions (public, so they can also be wired in the Inspector) ----------

    public void OnBankClicked()
    {
        if (flow != null) flow.RequestBank();
        AudioManager.UISFX(SoundType.UI_Button);
    }

    public void OnRideClicked()
    {
        if (flow != null) flow.RequestRide();
        AudioManager.UISFX(SoundType.UI_Button);
    }
}
