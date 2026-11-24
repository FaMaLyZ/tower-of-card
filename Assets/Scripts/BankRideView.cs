using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The Bank / Ride choice after a shake the tower survived: the cashier drawer under the right HUD
/// (HUD Design §5.6), with a receipt line and two register keys.
/// A view: shows the drawer and sends the click to the conductor. With a DrawerSlideJuice on the drawer it drops
/// out of the HUD and snaps back (C5); without one it simply appears.
public class BankRideView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("UI")]
    [Tooltip("The cashier drawer: shown only while choosing")]
    public GameObject panel;
    public Button bankButton;
    public Button rideButton;
    public TMP_Text bankLabel;
    public TMP_Text rideLabel;
    public TMP_Text receipt;

    [Header("Key texts")]
    [Tooltip("{0} = what Bank pays now")]
    public string bankFormat = "BANK\n<size=80%>{0:N0}</size>";
    public string rideText = "RIDE\n<size=60%>build higher</size>";
    public string towerFullText = "RIDE\n<size=60%>Tower full</size>";

    [Header("Receipt texts")]
    [Tooltip("{0} = Safe now, {1} = Safe after the Bank")]
    public string receiptTargetMetFormat = "SAFE {0:N0} » {1:N0}  TARGET MET";
    [Tooltip("{0} = Safe now, {1} = Safe after the Bank, {2} = target")]
    public string receiptBelowTargetFormat = "SAFE {0:N0} » {1:N0} / {2:N0}";
    public string receiptTowerFull = "Tower full — Bank to continue";

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
        if (!choosing) return;

        RefreshKeys();
        RefreshReceipt();
    }

    // The keys live inside the drawer, so they leave with it (they stay visible while it slides away)
    void ShowChoice(bool show)
    {
        DrawerSlideJuice.Show(panel, show);
        if (!show) return;
        if (bankButton != null) bankButton.gameObject.SetActive(true);
        if (rideButton != null) rideButton.gameObject.SetActive(true);
    }

    void RefreshKeys()
    {
        if (bankLabel != null) bankLabel.text = string.Format(bankFormat, flow.BankQuote);
        if (rideLabel != null) rideLabel.text = flow.CanRide ? rideText : towerFullText;
        if (rideButton != null) rideButton.interactable = flow.CanRide;     // feedback only: the conductor checks again
    }

    // What a Bank would do to Safe: "SAFE 0 » 336  TARGET MET", or "/ 300" while still short
    void RefreshReceipt()
    {
        if (receipt == null) return;

        if (!flow.CanRide) receipt.text = receiptTowerFull;
        else if (flow.BankReachesTarget) receipt.text = string.Format(receiptTargetMetFormat, flow.SafePoints, flow.SafeAfterBank);
        else receipt.text = string.Format(receiptBelowTargetFormat, flow.SafePoints, flow.SafeAfterBank, flow.Target);
    }

    // ---------- Button actions (public, so they can also be wired in the Inspector) ----------

    public void OnBankClicked()
    {
        if (flow != null) flow.RequestBank();
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }

    public void OnRideClicked()
    {
        if (flow != null) flow.RequestRide();
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }
}
