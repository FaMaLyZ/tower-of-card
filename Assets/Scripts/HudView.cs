using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// The centre of the HUD: the instruction line, the banner, the action buttons under the hand
/// (Done swapping / Play hand, Undo), and the end panel when the run is over (HUD Design §7, §9.5).
/// The other areas have their own views (LeftHudView, HandPlaqueView, the right HUD, DeckHudView).
/// A view: it only reads the game and sends clicks to the conductor. Empty fields are skipped.
public class HudView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    [Tooltip("Gives the placing tip shown in the Build phase")]
    public TowerPlacementInput placement;
    [FormerlySerializedAs("discardManager")]
    public SwapManager swapManager;
    public HudTheme theme;

    [Header("Run end")]
    [FormerlySerializedAs("gameOverPanel")]
    public GameObject runEndPanel;

    [Header("Instruction line")]
    [Tooltip("The pill behind the instruction; hidden when there is nothing to say")]
    public GameObject instructionRoot;
    [FormerlySerializedAs("stateText")]
    public TMP_Text instructionText;

    [Header("Banner")]
    [Tooltip("The pill behind the banner; shown and hidden with it")]
    public GameObject bannerRoot;
    public TMP_Text bannerText;

    [Header("Buttons")]
    public Button endTurnButton;
    public Button undoButton;

    [Header("Instructions (HUD Design §7.1)")]
    public string drawText = "Dealing your hand...";
    [Tooltip("{0} = swaps left")]
    public string swapFormat = "Swap: pick up to {0} cards, then click the deck";
    [Tooltip("{0} = most cards a hand can use")]
    public string commitNothingPickedFormat = "Pick 1-{0} cards for your poker hand";
    [Tooltip("{0} = cards picked")]
    public string commitReadyFormat = "Play hand to commit {0} cards";
    public string commitReadyOneCard = "Play hand to commit 1 card";
    [Tooltip("{0} = cards still to place, {1} = the placing tip")]
    public string buildFormat = "Place your cards: {0} left\n<size=75%>{1}</size>";
    public string scoreText = "Scoring...";
    public string shakeText = "The tower shakes!";
    public string bankOrRideText = "Bank the pot to make it safe, or Ride and build higher";

    [Header("Banner texts")]
    public string bannerStartFormat = "Hand {0}";
    public string bannerEndFormat = "Hand {0} complete";

    [Header("Button labels")]
    public string doneSwappingLabel = "Done swapping";
    public string playHandLabel = "Play hand";

    [Header("Deck click refused")]
    [Min(0f)] public float swapRejectSeconds = 2f;
    public string swapNotNowText = "You can only swap at the start of a hand";
    public string swapNothingPickedText = "Pick the cards to swap first, then click the deck";
    public string swapNoneLeftText = "No swaps left this hand";
    public string swapTooManyText = "Too many cards picked for the swaps left";

    enum Tone { Normal, Refusal, Decision }

    TMP_Text mainButtonLabel;
    bool isRunEndPanelOpen;
    string swapRejectText = "";
    float swapRejectHideTime;

    bool GameRunning => flow != null && flow.IsReady && flow.Phase != GamePhase.None;

    // =====================================================================
    //  LIFECYCLE
    // =====================================================================

    void Awake()
    {
        FindMissingReferences();
        PreventTextsFromBlockingClicks();
        HookButtons();
    }

    void OnEnable()
    {
        GameEvents.SwapRejected += OnSwapRejected;
    }

    void OnDisable()
    {
        GameEvents.SwapRejected -= OnSwapRejected;
    }

    void OnSwapRejected(SwapRejectReason reason)
    {
        swapRejectText = GetSwapRejectText(reason);
        swapRejectHideTime = Time.time + swapRejectSeconds;
    }

    string GetSwapRejectText(SwapRejectReason reason)
    {
        switch (reason)
        {
            case SwapRejectReason.NotSwapPhase: return swapNotNowText;
            case SwapRejectReason.NothingPicked: return swapNothingPickedText;
            case SwapRejectReason.NoSwapsLeft: return swapNoneLeftText;
            default: return swapTooManyText;
        }
    }

    void FindMissingReferences()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (placement == null) placement = FindAnyObjectByType<TowerPlacementInput>();
        if (swapManager == null) swapManager = FindAnyObjectByType<SwapManager>();
    }

    // Texts must not block mouse clicks on the game (TowerPlacementInput ignores clicks over UI)
    void PreventTextsFromBlockingClicks()
    {
        foreach (var t in new[] { instructionText, bannerText })
            if (t != null) t.raycastTarget = false;
    }

    void HookButtons()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnMainButtonClicked);
            mainButtonLabel = endTurnButton.GetComponentInChildren<TMP_Text>(true);
        }
        if (undoButton != null) undoButton.onClick.AddListener(OnUndoClicked);
    }

    void Update()
    {
        RefreshInstruction();
        RefreshBanner();
        RefreshMainButton();
        RefreshUndoButton();
        RefreshRunEndPanel();
    }

    // =====================================================================
    //  REFRESH (every frame) - one method per part of the HUD
    // =====================================================================

    void RefreshInstruction()
    {
        if (!GameRunning) return;

        string text = GetInstruction(out Tone tone);
        bool visible = !string.IsNullOrEmpty(text);
        if (instructionRoot != null) instructionRoot.SetActive(visible);
        if (instructionText == null || !visible) return;

        instructionText.text = text;
        instructionText.color = GetToneColor(tone);
    }

    void RefreshBanner()
    {
        if (bannerText == null) return;

        string text = GetBannerText();
        bool visible = !string.IsNullOrEmpty(text);
        if (bannerRoot != null) bannerRoot.SetActive(visible);
        else bannerText.gameObject.SetActive(visible);
        bannerText.text = text;
    }

    // One button, two jobs: "Done swapping" in the Swap phase, "Play hand" in the Commit phase.
    // Shown only in those two phases, so it never sits next to Undo or the Bank / Ride choice.
    void RefreshMainButton()
    {
        if (endTurnButton == null) return;

        bool swapping = GameRunning && flow.Phase == GamePhase.Swap;
        bool committing = GameRunning && flow.Phase == GamePhase.Commit;
        endTurnButton.gameObject.SetActive(swapping || committing);
        if (!swapping && !committing) return;

        endTurnButton.interactable = swapping ? flow.IsSwapping : flow.CanPlayHand;
        if (mainButtonLabel != null) mainButtonLabel.text = swapping ? doneSwappingLabel : playHandLabel;
    }

    void RefreshUndoButton()
    {
        if (undoButton == null) return;

        undoButton.gameObject.SetActive(GameRunning && flow.Phase == GamePhase.Build);
        undoButton.interactable = GameRunning && flow.CanUndo;
    }

    // The end panel is open exactly while the run is over (Play Again / F9 closes it again)
    void RefreshRunEndPanel()
    {
        bool runOver = flow != null && flow.IsReady && flow.IsRunOver;
        if (runOver == isRunEndPanelOpen) return;

        isRunEndPanelOpen = runOver;
        if (runEndPanel != null) runEndPanel.SetActive(runOver);
        if (runOver) PlayRunEndSound();
    }

    void PlayRunEndSound()
    {
        AudioManager.MusicStop();
        AudioManager.SFX(SoundType.SFX_EndGame);
    }

    // =====================================================================
    //  BUTTON ACTIONS (public, so you can also wire them in the Inspector)
    // =====================================================================

    /// The main button. The conductor ignores a click in the wrong phase.
    public void OnMainButtonClicked()
    {
        if (flow != null)
        {
            if (flow.Phase == GamePhase.Swap) flow.RequestDoneSwapping();
            else flow.RequestPlayHand();
        }
        AudioManager.UISFX(SoundType.UI_Button);
    }

    public void OnUndoClicked()
    {
        if (flow != null) flow.RequestUndo();
        AudioManager.UISFX(SoundType.UI_Button);
    }

    // =====================================================================
    //  INSTRUCTION TEXT
    // =====================================================================

    // What to do now, or why the last action was refused
    string GetInstruction(out Tone tone)
    {
        tone = Tone.Normal;
        if (Time.time < swapRejectHideTime)
        {
            tone = Tone.Refusal;
            return swapRejectText;
        }

        switch (flow.Phase)
        {
            case GamePhase.Draw:       return drawText;
            case GamePhase.Swap:       return string.Format(swapFormat, swapManager != null ? swapManager.SwapsLeft : 0);
            case GamePhase.Commit:     return GetCommitInstruction(out tone);
            case GamePhase.Build:      return string.Format(buildFormat, flow.Commit.UnplacedCount, placement != null ? placement.HintText : "");
            case GamePhase.Score:      return scoreText;
            case GamePhase.Shake:      return shakeText;
            case GamePhase.BankOrRide: tone = Tone.Decision; return bankOrRideText;
            default:                   return "";     // the full-screen panels (blind menu, result, Continue / Leave, run end) say it
        }
    }

    // Nothing picked: what to do. Picked but refused: why (in salmon). Picked and fine: how many will be played.
    string GetCommitInstruction(out Tone tone)
    {
        tone = Tone.Normal;
        int picked = flow.Commit.SelectedCount;
        int maxCards = flow.config != null ? flow.config.maxCommitCards : 5;

        if (picked == 0) return string.Format(commitNothingPickedFormat, maxCards);
        if (flow.Commit.Problem != "")
        {
            tone = Tone.Refusal;
            return flow.Commit.Problem;
        }
        return picked == 1 ? commitReadyOneCard : string.Format(commitReadyFormat, picked);
    }

    Color GetToneColor(Tone tone)
    {
        if (theme == null) return Color.white;
        switch (tone)
        {
            case Tone.Refusal:  return theme.danger;
            case Tone.Decision: return theme.goldText;
            default:            return theme.cream;
        }
    }

    // =====================================================================
    //  BANNER TEXT
    // =====================================================================

    string GetBannerText()
    {
        if (!GameRunning) return "";

        switch (flow.Phase)
        {
            case GamePhase.Draw:   return string.Format(bannerStartFormat, flow.HandNumber);
            case GamePhase.Shake:  return string.Format(bannerEndFormat, flow.HandNumber);
            default:               return "";    // the menu, end panel and choice buttons speak for themselves
        }
    }
}
