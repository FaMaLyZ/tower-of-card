using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// The plain HUD (was GameUI; same script file ID, so the scene keeps every wired text).
/// It fills in texts, shows the main button and Undo, and opens the end panel when the run is over.
///
/// A view: it only reads the conductor and the workers, and sends button clicks as commands.
/// Every field is optional: leave one empty and that part of the HUD is skipped.
/// The other screens have their own views: BankRideView, ContinueLeaveView, GlassView,
/// BlindMenuView, RunEndView and SnapMarkerView.
public class HudView : MonoBehaviour
{
    [Header("Game references (auto-found if empty)")]
    public GameFlowController flow;
    public TowerPlacementInput placement;       // the placing hint
    public HandManager handManager;             // the card to describe in the hint
    public DeckManager deckManager;
    [FormerlySerializedAs("discardManager")]
    public ScoreManager scoreManager;

    [Header("Run end")]
    [Tooltip("Opened while the run is over (Cleared or Shattered). RunEndView sits on it.")]
    [FormerlySerializedAs("gameOverPanel")]
    public GameObject runEndPanel;

    [Header("Turn info texts")]
    public TMP_Text turnText;                   // "Hand 1 / 4   Target 150"
    [Tooltip("Optional: the run line, e.g. \"Ante 1 / 4  Small blind   Run 320   Reward x1.2\"")]
    public TMP_Text runText;
    public TMP_Text cardsText;                  // "Cards: 2 / 5" (placed / committed)
    public TMP_Text stateText;                  // what to do now, or why Play Hand is refused
    public TMP_Text hintText;                   // card details, or the placing hint

    [Header("Deck info (optional)")]
    public TMP_Text deckText;                   // "Deck: 12"
    public TMP_Text discardText;                // "Discard: 3" (the used cards)
    [FormerlySerializedAs("discardsLeftText")]
    public TMP_Text swapsLeftText;              // "Swaps left: 1"

    [Header("Score (optional)")]
    public TMP_Text handNameText;               // "Pair" (live while selecting, then the scored hand)
    public TMP_Text chipsText;                  // "Chips: 25"
    public TMP_Text multText;                   // "Mult: x2"
    public TMP_Text turnScoreText;              // "Turn score: 50"
    public TMP_Text totalScoreText;             // "Safe: 120" (points that were banked)

    [Header("Pot (optional)")]
    [Tooltip("Shows the Pot, what a Bank would pay now, and what the last shake did. Needs room for 3 lines.")]
    public TMP_Text potText;

    [Header("Banner (big center text)")]
    public TMP_Text bannerText;

    [Header("Buttons")]
    [Tooltip("The main button. Its label changes with the phase: Done Swapping, then Play Hand.")]
    public Button endTurnButton;
    [Tooltip("Optional: takes back the last card placed this hand (right-click does the same).")]
    public Button undoButton;

    [Header("Text formats  ({0}, {1} are replaced by numbers)")]
    [Tooltip("Shown in the Hint text for the card under the mouse (or the selected card). {0} = rank, {1} = suit, {2} = weight (Ace 1, 2-10 face value, J/Q/K 10). Use a new line for separate lines.")]
    [TextArea(1, 4)]
    public string cardDetailFormat = "Rank: {0}   Suit: {1}   Weight: {2}";
    [Tooltip("When no card is hovered or selected, show the placing hint instead.")]
    public bool showInstructionsWhenNoCard = true;
    [Tooltip("{0} = hand number, {1} = hands per blind, {2} = Safe Points, {3} = target")]
    public string turnFormat = "Hand {0} / {1}   Target {3:N0}";
    public string cardsFormat = "Cards: {0} / {1}";
    public string deckFormat = "Deck: {0}";
    public string discardFormat = "Discard: {0}";
    public string chipsFormat = "Chips: {0:0}";
    public string multFormat = "Mult: x{0:0.##}";
    public string turnScoreFormat = "Turn score: {0:N0}";
    public string totalScoreFormat = "Safe: {0:N0}";
    [FormerlySerializedAs("discardsLeftFormat")]
    public string swapsLeftFormat = "Swaps left: {0}";
    public string bannerStartFormat = "Turn {0}";
    public string bannerEndFormat = "Turn {0} complete";
    public string blindWonLabel = "Blind won!";
    public string blindFailedLabel = "Blind failed";
    [Tooltip("Banner between blinds. {0} = blind (Small / Big / Boss), {1} = won / failed label, {2} = blind score, {3} = target")]
    [TextArea(1, 3)]
    public string blindResultFormat = "{0}: {1}\n{2:N0} / {3:N0}";
    [Tooltip("Added to the banner when the player left the table. {0} = the bonus")]
    public string leaveBonusFormat = "\nLeft the table: +{0:N0}";
    [Tooltip("{0} = Ante, {1} = Antes per run, {2} = blind, {3} = Run Total, {4} = reward multiplier, {5} = retry label")]
    public string runFormat = "Ante {0} / {1}  {2} blind{5}   Run {3:N0}   Reward x{4:0.##}";
    public string retryLabel = " (retry)";
    public string runEndStateLabel = "Run over";
    public string blindMenuStateLabel = "Choose your blind";
    public string startingLabel = "Get ready...";
    public string turnEndedLabel = "Turn ended";
    public string potUnsettledFormat = "Pot: {0:N0} (unsettled)";
    [Tooltip("{0} = Pot chips, {1} = what Bank pays, {2} = height bonus percent, {3} = standing levels")]
    public string potSettledFormat = "Pot: {0:N0}\nBank now: {1:N0} (+{2}% height, {3} levels)";
    [Tooltip("{0} = cards broken in the last shake, {1} = Pot percent lost to them")]
    public string lastShakeFormat = "\nLast shake: {0} broken (-{1}% each)";
    public string collapsedLabel = "\nThe tower collapsed: the Pot is lost";
    public string bankOrRideStateLabel = "Bank the Pot, or Ride to keep building";
    public string continueOrLeaveStateLabel = "Target reached! Continue, or Leave Table for a bonus";
    public string doneSwappingLabel = "Done Swapping";
    public string playHandLabel = "Play Hand";
    public string swapStateFormat = "Swap: pick up to {0} cards, then click the deck";
    public string commitStateLabel = "Press Play Hand";
    public string buildStateFormat = "Place your cards: {0} left";

    [Header("Deck click refused (shown in the state text)")]
    [Min(0f)] public float swapRejectSeconds = 2f;
    public string swapNotNowText = "You can only swap at the start of a hand";
    public string swapNothingPickedText = "Pick the cards to swap first, then click the deck";
    public string swapNoneLeftText = "No swaps left this hand";
    public string swapTooManyText = "Too many cards picked for the swaps left";

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
        if (handManager == null) handManager = FindAnyObjectByType<HandManager>();
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
    }

    // Texts must not block mouse clicks on the game (TowerPlacementInput ignores clicks over UI)
    void PreventTextsFromBlockingClicks()
    {
        foreach (var t in new[] { turnText, runText, cardsText, stateText, hintText, bannerText, deckText, discardText, handNameText, chipsText, multText, turnScoreText, totalScoreText, swapsLeftText, potText })
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
        RefreshTurnInfo();
        RefreshHint();
        RefreshDeckInfo();
        RefreshScore();
        RefreshPot();
        RefreshSwapsLeft();
        RefreshBanner();
        RefreshMainButton();
        RefreshUndoButton();
        RefreshRunEndPanel();
    }

    // =====================================================================
    //  REFRESH (every frame) - one method per part of the HUD
    // =====================================================================

    void RefreshTurnInfo()
    {
        if (!GameRunning) return;

        SetText(turnText, string.Format(turnFormat, flow.HandNumber, flow.HandsPerBlind, flow.SafePoints, flow.Target));
        SetText(runText, string.Format(runFormat, flow.AnteNumber, flow.AntesPerRun, flow.CurrentBlind,
                                       flow.RunTotal, flow.RewardMultiplier, flow.IsRetry ? retryLabel : ""));
        SetText(cardsText, string.Format(cardsFormat, flow.CardsPlacedThisHand, flow.Commit.CommittedCards.Count));
        SetText(stateText, GetStateLabel());
    }

    void RefreshDeckInfo()
    {
        if (deckManager == null) return;
        SetText(deckText, string.Format(deckFormat, deckManager.DrawPileCount));
        SetText(discardText, string.Format(discardFormat, deckManager.DiscardCount));
    }

    // Balatro-style score display: hand name, chips, mult, then the turn score
    void RefreshScore()
    {
        if (scoreManager == null) return;

        bool showHand = scoreManager.HasResult;
        string handName = showHand ? scoreManager.HandName : GetCommitHandName();

        ShowText(handNameText, handName, handName != "");
        ShowText(chipsText, string.Format(chipsFormat, scoreManager.CurrentChips), showHand);
        ShowText(multText, string.Format(multFormat, scoreManager.CurrentMult), showHand);
        ShowText(turnScoreText, string.Format(turnScoreFormat, scoreManager.TurnScore), showHand && !scoreManager.IsScoring);
        SetText(totalScoreText, string.Format(totalScoreFormat, flow != null && flow.IsReady ? flow.SafePoints : 0));
    }

    // The Pot: raw chips while a shake can still change it, then the Bank quote with its breakdown
    void RefreshPot()
    {
        if (potText == null || !GameRunning) return;
        potText.text = BuildPotText();
    }

    string BuildPotText()
    {
        if (!flow.IsPotSettled) return string.Format(potUnsettledFormat, flow.PotChips);

        int heightBonusPercent = flow.config.heightPercent * flow.StandingLevels;
        string text = string.Format(potSettledFormat, flow.PotChips, flow.BankQuote, heightBonusPercent, flow.StandingLevels);

        ShakeResult shake = flow.LastShake;
        if (shake == null) return text;
        if (shake.Collapsed) return text + collapsedLabel;
        return text + string.Format(lastShakeFormat, shake.BrokenCount, flow.config.breakPercent);
    }

    void RefreshSwapsLeft()
    {
        if (deckManager == null) return;
        SetText(swapsLeftText, string.Format(swapsLeftFormat, deckManager.SwapsLeft));
    }

    // The hint text shows the details of the card under the mouse (or the selected card)
    void RefreshHint()
    {
        if (hintText == null) return;

        CardData card = handManager != null ? handManager.InspectedCard : null;
        if (card != null) hintText.text = string.Format(cardDetailFormat, card.rank, card.suit, card.Weight);
        else hintText.text = showInstructionsWhenNoCard && placement != null ? placement.HintText : "";
    }

    void RefreshBanner()
    {
        if (bannerText == null) return;

        string text = GetBannerText();
        ShowText(bannerText, text, !string.IsNullOrEmpty(text));
    }

    // One button, two jobs: "Done Swapping" in the Swap phase, "Play Hand" in the Commit phase
    void RefreshMainButton()
    {
        if (endTurnButton == null) return;

        bool show = GameRunning && !IsBetweenBlinds();
        endTurnButton.gameObject.SetActive(show);
        if (!show) return;

        bool swapping = flow.Phase == GamePhase.Swap;
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
    //  HELPERS
    // =====================================================================

    string GetStateLabel()
    {
        if (Time.time < swapRejectHideTime) return swapRejectText;

        switch (flow.Phase)
        {
            case GamePhase.BlindEnd: return GetBlindResultLabel();
            case GamePhase.RunEnd: return runEndStateLabel;
            case GamePhase.BlindMenu: return blindMenuStateLabel;
            case GamePhase.Draw:   return startingLabel;
            case GamePhase.Swap:   return string.Format(swapStateFormat, deckManager != null ? deckManager.SwapsLeft : 0);
            case GamePhase.Commit: return flow.Commit.Problem != "" ? flow.Commit.Problem : commitStateLabel;
            case GamePhase.Build:  return string.Format(buildStateFormat, flow.Commit.UnplacedCount);
            case GamePhase.BankOrRide: return bankOrRideStateLabel;
            case GamePhase.ContinueOrLeave: return continueOrLeaveStateLabel;
            default:               return turnEndedLabel;
        }
    }

    // The hand the selected (or committed) cards make, while there is no scored result to show yet
    string GetCommitHandName()
    {
        if (!GameRunning || flow.Commit.Preview == null) return "";
        if (flow.Phase != GamePhase.Commit && flow.Phase != GamePhase.Build) return "";
        return flow.Commit.Preview.displayName;
    }

    // Menu, result banner and end screen: no hand is being played, so the Play Hand button hides
    bool IsBetweenBlinds()
    {
        return flow.Phase == GamePhase.BlindMenu || flow.Phase == GamePhase.BlindEnd || flow.Phase == GamePhase.RunEnd;
    }

    string GetBlindResultLabel()
    {
        return flow.LastBlindResult == BlindResult.Won ? blindWonLabel : blindFailedLabel;
    }

    // "Small: Blind won!  160 / 150" (+ the leave bonus), shown between blinds
    string BuildBlindResultBanner()
    {
        string text = string.Format(blindResultFormat, flow.LastBlindKind, GetBlindResultLabel(), flow.SafePoints, flow.Target);
        if (flow.LeftTable) text += string.Format(leaveBonusFormat, flow.LeaveBonusPaid);
        return text;
    }

    string GetBannerText()
    {
        if (!GameRunning) return "";

        switch (flow.Phase)
        {
            case GamePhase.BlindEnd: return BuildBlindResultBanner();
            case GamePhase.Draw:   return string.Format(bannerStartFormat, flow.HandNumber);
            case GamePhase.Score:
            case GamePhase.Shake:  return string.Format(bannerEndFormat, flow.HandNumber);
            default:               return "";    // the menu, end panel and choice buttons speak for themselves
        }
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    static void ShowText(TMP_Text text, string value, bool visible)
    {
        if (text == null) return;
        text.gameObject.SetActive(visible);
        text.text = value;
    }
}
