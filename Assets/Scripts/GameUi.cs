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
    public TurnManager turnManager;
    public TowerBuilder tower;
    public Canvas canvas;                       // only needed for snap markers

    [Header("Turn info texts")]
    public TMP_Text turnText;                   // "Turn 1"
    public TMP_Text cardsText;                  // "Cards: 2 / 5"
    public TMP_Text stateText;                  // "Your turn"
    public TMP_Text hintText;                   // help text from TowerBuilder

    [Header("Deck info (optional)")]
    public DeckManager deckManager;             // auto-found if empty
    public TMP_Text deckText;                   // "Deck: 12"
    public TMP_Text discardText;                // "Discard: 3"

    [Header("Score (optional)")]
    public ScoreManager scoreManager;           // auto-found if empty
    public TMP_Text handNameText;               // "Pair"
    public TMP_Text chipsText;                  // "Chips: 25"
    public TMP_Text multText;                   // "Mult: x2"
    public TMP_Text turnScoreText;              // "Turn score: 50"
    public TMP_Text totalScoreText;             // "Score: 120"

    [Header("Banner (big center text)")]
    public TMP_Text bannerText;

    [Header("Buttons")]
    public Button endTurnButton;
    public Button nextTurnButton;               // only shown when TurnManager.autoStartNextTurn is off

    [Header("Hand")]
    public HandSlotUI[] handSlots;
    public float selectedScale = 1.15f;         // selected card button gets bigger

    [Header("Snap markers")]
    [Tooltip("A small inactive Image under the Canvas. It is cloned for every snap point.")]
    public Image snapMarkerTemplate;
    public Color snapMarkerColor = new Color(1f, 1f, 1f, 0.6f);
    public Color snapMarkerBestColor = new Color(0.2f, 1f, 0.3f, 1f);

    [Header("Text formats  ({0}, {1} are replaced by numbers)")]
    public string turnFormat = "Turn {0}";
    public string cardsFormat = "Cards: {0} / {1}";
    public string deckFormat = "Deck: {0}";
    public string discardFormat = "Discard: {0}";
    public string chipsFormat = "Chips: {0:0}";
    public string multFormat = "Mult: x{0:0.##}";
    public string turnScoreFormat = "Turn score: {0:N0}";
    public string totalScoreFormat = "Score: {0:N0}";
    public string handLabelFormat = "{0}: {1}";      // {0} = key number, {1} = card name
    public string bannerStartFormat = "Turn {0}";
    public string bannerEndFormat = "Turn {0} complete";
    public string gameOverLabel = "Game Over";
    public string yourTurnLabel = "Your turn";
    public string startingLabel = "Get ready...";
    public string turnEndedLabel = "Turn ended";

    // ---------- Runtime ----------
    Camera cam;
    readonly List<RectTransform> markers = new List<RectTransform>();
    readonly List<Image> markerImages = new List<Image>();

    // =====================================================================
    //  LIFECYCLE
    // =====================================================================

    void Awake()
    {
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (tower == null) tower = FindFirstObjectByType<TowerBuilder>();
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
        if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
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
        RefreshBanner();
        RefreshEndTurnButton();
        RefreshNextTurnButton();
        RefreshHand();
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
        foreach (var t in new[] { turnText, cardsText, stateText, hintText, bannerText, deckText, discardText, handNameText, chipsText, multText, turnScoreText, totalScoreText })
            if (t != null) t.raycastTarget = false;

        if (snapMarkerTemplate != null) snapMarkerTemplate.raycastTarget = false;

        foreach (var slot in handSlots)
            if (slot != null && slot.label != null) slot.label.raycastTarget = false;
    }

    void HookButtons()
    {
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (nextTurnButton != null) nextTurnButton.onClick.AddListener(OnNextTurnClicked);

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

    bool GameRunning => turnManager != null && turnManager.GameStarted;

    void RefreshTurnInfo()
    {
        if (!GameRunning) return;

        if (turnText != null)
            turnText.text = string.Format(turnFormat, turnManager.TurnNumber);

        if (cardsText != null)
            cardsText.text = string.Format(cardsFormat, turnManager.CardsPlacedThisTurn, turnManager.cardsPerTurn);

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
            handNameText.gameObject.SetActive(showHand);
            handNameText.text = scoreManager.HandName;
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
            totalScoreText.text = string.Format(totalScoreFormat, scoreManager.TotalScore);
    }

    void RefreshHint()
    {
        if (hintText != null)
            hintText.text = tower != null ? tower.HintText : "";
    }

    // Big text while a turn is starting / ended / game over
    void RefreshBanner()
    {
        if (bannerText == null) return;

        string text = GetBannerText();
        bannerText.text = text;
        bannerText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    void RefreshEndTurnButton()
    {
        if (endTurnButton == null) return;

        endTurnButton.gameObject.SetActive(GameRunning);
        endTurnButton.interactable = turnManager != null && turnManager.CanEndTurn;
    }

    void RefreshNextTurnButton()
    {
        if (nextTurnButton == null) return;

        nextTurnButton.gameObject.SetActive(turnManager != null && turnManager.WaitingForNextTurn);
    }

    void RefreshHand()
    {
        bool show = tower != null && tower.Hand != null;

        for (int i = 0; i < handSlots.Length; i++)
        {
            var slot = handSlots[i];
            if (slot == null || slot.button == null) continue;

            bool active = show && i < tower.Hand.Count;
            slot.button.gameObject.SetActive(active);
            if (!active) continue;

            CardData data = tower.Hand[i];
            bool isSelected = i == tower.SelectedIndex;

            slot.button.interactable = tower.CanAct;
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

    public void OnEndTurnClicked()
    {
        if (turnManager != null) turnManager.EndTurn();
    }

    public void OnNextTurnClicked()
    {
        if (turnManager != null) turnManager.StartNextTurn();
    }

    public void OnHandButtonClicked(int index)
    {
        if (tower != null) tower.SelectCard(index);
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    string GetStateLabel()
    {
        if (turnManager.IsGameOver) return gameOverLabel;

        switch (turnManager.State)
        {
            case TurnState.StartTurn:  return startingLabel;
            case TurnState.DuringTurn: return yourTurnLabel;
            default:                   return turnEndedLabel;
        }
    }

    string GetBannerText()
    {
        if (!GameRunning) return "";

        if (turnManager.IsGameOver && turnManager.State == TurnState.EndTurn) return gameOverLabel;
        if (turnManager.State == TurnState.StartTurn) return string.Format(bannerStartFormat, turnManager.TurnNumber);
        if (turnManager.State == TurnState.EndTurn) return string.Format(bannerEndFormat, turnManager.TurnNumber);
        return "";   // DuringTurn: no banner
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

    static bool IsBright(Color c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) > 0.6f;
}