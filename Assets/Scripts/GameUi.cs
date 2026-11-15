using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Controls a HUD that YOU build in the Unity editor (Canvas, texts, buttons).
/// This script creates nothing - it only fills in texts, enables/disables buttons,
/// highlights the selected card and moves snap markers.
///
/// Every field is optional: leave one empty and that part of the UI is simply skipped.
/// Each UI element has its own RefreshXxx() method - edit or remove them in Update().
public class GameUI : MonoBehaviour
{
    // One button in the hand (drag your 3 hand buttons in here)
    [Serializable]
    public class HandSlotUI
    {
        public Button button;
        public TMP_Text label;
        [Tooltip("Optional: tinted with the card colour")]
        public Image background;
        [Tooltip("Optional: object shown only on the selected card (e.g. a yellow frame)")]
        public GameObject selectedHighlight;
    }

    [Header("Game references (auto-found if empty)")]
    public GameFlowController flow;
    public TowerBuilder tower;
    public HandManager handManager;             // used to find the card to describe
    public Canvas canvas;                       // only needed for snap markers
    public GameObject gameOverPanel;
    public bool isGameOverPanelActive = false;

    [Header("Turn info texts")]
    public TMP_Text turnText;                   // "Hand 1 / 4   Target 150"
    [Tooltip("Optional: the run line, e.g. \"Ante 1 / 4  Small blind   Run 320   Reward x1.2\"")]
    public TMP_Text runText;
    public TMP_Text cardsText;                  // "Cards: 2 / 5" (placed / committed)
    public TMP_Text stateText;                  // what to do now, or why Play Hand is refused
    public TMP_Text hintText;                   // help text from TowerBuilder

    [Header("Deck info (optional)")]
    public DeckManager deckManager;             // auto-found if empty
    public TMP_Text deckText;                   // "Deck: 12"
    public TMP_Text discardText;                // "Discard: 3"

    [Header("Discards (optional)")]
    public DiscardManager discardManager;       // auto-found if empty
    public TMP_Text discardsLeftText;           // "Discards left: 1"

    [Header("Score (optional)")]
    public ScoreManager scoreManager;           // auto-found if empty
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
    public Button nextTurnButton;               // no longer used (the next hand always starts by itself); kept hidden

    [Header("Hand")]
    public HandSlotUI[] handSlots;
    public float selectedScale = 1.15f;         // selected card button gets bigger

    [Header("Snap markers")]
    [Tooltip("A small inactive Image under the Canvas. It is cloned for every snap point.")]
    public Image snapMarkerTemplate;
    public Color snapMarkerColor = new Color(1f, 1f, 1f, 0.6f);
    public Color snapMarkerBestColor = new Color(0.2f, 1f, 0.3f, 1f);

    [Header("Text formats  ({0}, {1} are replaced by numbers)")]
    [Tooltip("Shown in the Hint text for the card under the mouse (or the selected card). {0} = rank, {1} = suit, {2} = weight (Ace 1, 2-10 face value, J/Q/K 10). Use a new line for separate lines.")]
    [TextArea(1, 4)]
    public string cardDetailFormat = "Rank: {0}   Suit: {1}   Weight: {2}";
    [Tooltip("When no card is hovered or selected, show the normal help text instead.")]
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
    public string discardsLeftFormat = "Discards left: {0}";
    public string handLabelFormat = "{0}: {1}";      // {0} = key number, {1} = card name
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
    public string yourTurnLabel = "Your turn";
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

    // ---------- Runtime ----------
    Camera cam;
    readonly List<RectTransform> markers = new List<RectTransform>();
    readonly List<Image> markerImages = new List<Image>();
    TMP_Text mainButtonLabel;

    // =====================================================================
    //  LIFECYCLE
    // =====================================================================

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (tower == null) tower = FindFirstObjectByType<TowerBuilder>();
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
        if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
        if (discardManager == null) discardManager = FindFirstObjectByType<DiscardManager>();
        if (handManager == null) handManager = FindFirstObjectByType<HandManager>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        PreventUIFromBlockingClicks();
        HookButtons();

        if (snapMarkerTemplate != null) snapMarkerTemplate.gameObject.SetActive(false);
    }

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        RefreshTurnInfo();
        RefreshHint();
        RefreshDeckInfo();
        RefreshScore();
        RefreshPot();
        RefreshDiscards();
        RefreshBanner();
        RefreshMainButton();
        RefreshUndoButton();
        RefreshNextTurnButton();
        RefreshHand();
        RefreshGameOverPanel();
    }

    void LateUpdate()
    {
        RefreshSnapMarkers();   // after TowerBuilder has updated its slots this frame
    }

    // =====================================================================
    //  SETUP
    // =====================================================================

    // Texts and markers must not block mouse clicks on the game (TowerBuilder ignores clicks over UI)
    void PreventUIFromBlockingClicks()
    {
        foreach (var t in new[] { turnText, runText, cardsText, stateText, hintText, bannerText, deckText, discardText, handNameText, chipsText, multText, turnScoreText, totalScoreText, discardsLeftText, potText })
            if (t != null) t.raycastTarget = false;

        if (snapMarkerTemplate != null) snapMarkerTemplate.raycastTarget = false;

        foreach (var slot in handSlots)
            if (slot != null && slot.label != null) slot.label.raycastTarget = false;
    }

    void HookButtons()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
            mainButtonLabel = endTurnButton.GetComponentInChildren<TMP_Text>(true);
        }
        if (undoButton != null) undoButton.onClick.AddListener(OnUndoClicked);

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i] == null || handSlots[i].button == null) continue;
            int index = i;   // capture for the click handler
            handSlots[i].button.onClick.AddListener(() => OnHandButtonClicked(index));
        }
    }

    // =====================================================================
    //  REFRESH (every frame) - one method per UI element
    // =====================================================================

    bool GameRunning => flow != null && flow.IsReady && flow.Phase != GamePhase.None;

    void RefreshTurnInfo()
    {
        if (!GameRunning) return;

        if (turnText != null)
            turnText.text = string.Format(turnFormat, flow.HandNumber, flow.HandsPerBlind, flow.SafePoints, flow.Target);

        if (runText != null)
            runText.text = string.Format(runFormat, flow.AnteNumber, flow.AntesPerRun, flow.CurrentBlind,
                                         flow.RunTotal, flow.RewardMultiplier, flow.IsRetry ? retryLabel : "");

        if (cardsText != null)
            cardsText.text = string.Format(cardsFormat, flow.CardsPlacedThisHand, flow.Commit.CommittedCards.Count);

        if (stateText != null)
            stateText.text = GetStateLabel();
    }

    void RefreshDeckInfo()
    {
        if (deckManager == null) return;

        if (deckText != null)
            deckText.text = string.Format(deckFormat, deckManager.DrawPileCount);

        if (discardText != null)
            discardText.text = string.Format(discardFormat, deckManager.DiscardCount);
    }

    // Balatro-style score display: hand name, chips, mult, then the turn score
    void RefreshScore()
    {
        if (scoreManager == null) return;

        bool showHand = scoreManager.HasResult;

        if (handNameText != null)
        {
            string handName = showHand ? scoreManager.HandName : GetCommitHandName();
            handNameText.gameObject.SetActive(handName != "");
            handNameText.text = handName;
        }
        if (chipsText != null)
        {
            chipsText.gameObject.SetActive(showHand);
            chipsText.text = string.Format(chipsFormat, scoreManager.CurrentChips);
        }
        if (multText != null)
        {
            multText.gameObject.SetActive(showHand);
            multText.text = string.Format(multFormat, scoreManager.CurrentMult);
        }
        if (turnScoreText != null)
        {
            turnScoreText.gameObject.SetActive(showHand && !scoreManager.IsScoring);   // appears when counting is done
            turnScoreText.text = string.Format(turnScoreFormat, scoreManager.TurnScore);
        }
        if (totalScoreText != null)
            totalScoreText.text = string.Format(totalScoreFormat, flow != null ? flow.SafePoints : 0);
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

    void RefreshDiscards()
    {
        if (discardsLeftText == null || discardManager == null) return;
        discardsLeftText.text = string.Format(discardsLeftFormat, discardManager.DiscardsLeft);
    }

    // The hint text shows the details of the card under the mouse (or the selected card)
    void RefreshHint()
    {
        if (hintText == null) return;

        CardData card = handManager != null ? handManager.InspectedCard : null;
        if (card != null)
        {
            hintText.text = BuildCardDetail(card);
            return;
        }

        hintText.text = showInstructionsWhenNoCard && tower != null ? tower.HintText : "";
    }

    string BuildCardDetail(CardData card)
    {
        return string.Format(cardDetailFormat, card.rank, card.suit, card.Weight);
    }

    // Big text while a turn is starting / ended / game over
    void RefreshBanner()
    {
        if (bannerText == null) return;

        string text = GetBannerText();
        bannerText.text = text;
        bannerText.gameObject.SetActive(!string.IsNullOrEmpty(text));
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

    void RefreshNextTurnButton()
    {
        if (nextTurnButton == null) return;

        nextTurnButton.gameObject.SetActive(false);
    }

    void RefreshHand()
    {
        bool show = handManager != null;

        for (int i = 0; i < handSlots.Length; i++)
        {
            var slot = handSlots[i];
            if (slot == null || slot.button == null) continue;

            bool active = show && i < handManager.Count;
            slot.button.gameObject.SetActive(active);
            if (!active) continue;

            CardData data = handManager.GetCard(i);
            bool isSelected = handManager.IsSelected(handManager.GetCardId(i));

            slot.button.interactable = flow != null && flow.CanClickHandCards;
            slot.button.transform.localScale = Vector3.one * (isSelected ? selectedScale : 1f);

            if (slot.label != null)
            {
                slot.label.text = string.Format(handLabelFormat, i + 1, data.cardName);
                slot.label.color = IsBright(data.tint) ? Color.black : Color.white;
            }

            if (slot.background != null) slot.background.color = data.tint;
            if (slot.selectedHighlight != null) slot.selectedHighlight.SetActive(isSelected);
        }
    }

    // Markers at every free snap point (green = the one the ghost will snap to)
    void RefreshSnapMarkers()
    {
        if (snapMarkerTemplate == null || canvas == null) return;
        if (cam == null) cam = Camera.main;

        bool show = tower != null && cam != null && tower.ShowSlots;
        int count = show ? tower.SlotCount : 0;

        while (markers.Count < count) CreateMarker();

        var parent = (RectTransform)snapMarkerTemplate.transform.parent;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        for (int i = 0; i < markers.Count; i++)
        {
            if (i >= count) { markers[i].gameObject.SetActive(false); continue; }

            Vector3 sp = cam.WorldToScreenPoint(tower.GetSlotPosition(i));
            if (sp.z <= 0) { markers[i].gameObject.SetActive(false); continue; }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, uiCam, out Vector2 local);

            markers[i].gameObject.SetActive(true);
            markers[i].anchoredPosition = local - parent.rect.center;
            markerImages[i].color = i == tower.BestSlotIndex ? snapMarkerBestColor : snapMarkerColor;
        }
    }

    // =====================================================================
    //  BUTTON ACTIONS (public, so you can also wire them in the Inspector)
    // =====================================================================

    /// The main button (the old End Turn button). The conductor ignores a click in the wrong phase.
    public void OnEndTurnClicked()
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

    public void OnHandButtonClicked(int index)
    {
        if (flow != null) flow.RequestSelectCard(index);
        AudioManager.UISFX(SoundType.UI_Button);
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    string GetStateLabel()
    {
        switch (flow.Phase)
        {
            case GamePhase.BlindEnd: return GetBlindResultLabel();
            case GamePhase.RunEnd: return runEndStateLabel;
            case GamePhase.BlindMenu: return blindMenuStateLabel;
            case GamePhase.Draw:   return startingLabel;
            case GamePhase.Swap:   return string.Format(swapStateFormat, discardManager != null ? discardManager.DiscardsLeft : 0);
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

    // The end panel is open exactly while the run is over (Play Again / F9 closes it again)
    void RefreshGameOverPanel()
    {
        bool runOver = flow != null && flow.IsRunOver;
        if (runOver == isGameOverPanelActive) return;

        setGameoverPanelActive(runOver);
        isGameOverPanelActive = runOver;
    }

    string GetBannerText()
    {
        if (!GameRunning) return "";

        switch (flow.Phase)
        {
            case GamePhase.BlindEnd: return BuildBlindResultBanner();
            case GamePhase.RunEnd:                           // the end panel shows the run result
            case GamePhase.BlindMenu: return "";             // the blind menu shows itself
            case GamePhase.Draw:   return string.Format(bannerStartFormat, flow.HandNumber);
            case GamePhase.Swap:
            case GamePhase.Commit:
            case GamePhase.Build:
            case GamePhase.BankOrRide:
            case GamePhase.ContinueOrLeave: return "";
            default:               return string.Format(bannerEndFormat, flow.HandNumber);
        }
    }

    void CreateMarker()
    {
        var clone = Instantiate(snapMarkerTemplate.gameObject, snapMarkerTemplate.transform.parent);
        clone.name = "SnapMarker";

        var rt = (RectTransform)clone.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        markers.Add(rt);
        markerImages.Add(clone.GetComponent<Image>());
    }
    void setGameoverPanelActive(bool active)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(active);

        if (active)
        {
            AudioManager.MusicStop();
            GameoverSound();
        }
    }   
    void GameoverSound()
    {
        //เสียง game over
        AudioManager.SFX(SoundType.SFX_EndGame);
    }

    static bool IsBright(Color c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) > 0.6f;
}