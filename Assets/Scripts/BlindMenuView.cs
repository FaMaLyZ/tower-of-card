using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The blind menu: the three blinds of this Ante side by side.
/// Only the current blind can be chosen (Select, or Skip for Small / Big).
/// A view: sends the click to the conductor.
public class BlindMenuView : MonoBehaviour
{
    // One column of the menu (Small, Big or Boss)
    [Serializable]
    public class BlindColumnUI
    {
        public TMP_Text nameText;
        public TMP_Text targetText;
        public TMP_Text rewardText;
        public TMP_Text statusText;
        public Image background;
        public Button selectButton;
        public Button skipButton;
        public TMP_Text skipLabel;
    }

    [Header("Game references")]
    public GameFlowController flow;

    [Header("UI")]
    public GameObject panel;
    public TMP_Text headerText;
    public BlindColumnUI[] columns = new BlindColumnUI[3];

    [Header("Colours")]
    public Color currentColor = new Color(0.15f, 0.35f, 0.6f, 0.95f);
    public Color previewColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

    [Header("Texts")]
    [Tooltip("{0} = Ante, {1} = Antes per run, {2} = retry label")]
    public string headerFormat = "Ante {0} / {1}{2}";
    public string retryLabel = " - Final Boss retry";
    public string smallName = "Small Blind";
    public string bigName = "Big Blind";
    public string bossName = "Boss Blind";
    public string targetFormat = "Target {0:N0}";
    [Tooltip("{0} = reward as a multiplier, e.g. 1.2")]
    public string rewardFormat = "Reward x{0:0.##}";
    public string currentLabel = "Current";
    public string upcomingLabel = "Upcoming";
    public string wonLabel = "Won";
    public string failedLabel = "Failed";
    public string skippedLabel = "Skipped";
    public string skipRepairText = "Skip (repair 1 crack)";
    public string skipGlassFullText = "Skip (Glass is whole)";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        HookButtons();
    }

    void HookButtons()
    {
        foreach (BlindColumnUI column in columns)
        {
            if (column == null) continue;
            if (column.selectButton != null) column.selectButton.onClick.AddListener(OnSelectClicked);
            if (column.skipButton != null) column.skipButton.onClick.AddListener(OnSkipClicked);
        }
    }

    void Update()
    {
        bool choosing = flow != null && flow.IsReady && flow.IsChoosingBlind;
        if (panel != null) panel.SetActive(choosing);
        if (!choosing) return;

        RefreshHeader();
        for (int i = 0; i < columns.Length && i < 3; i++)
            RefreshColumn(columns[i], (BlindKind)i);
    }

    void RefreshHeader()
    {
        if (headerText == null) return;
        headerText.text = string.Format(headerFormat, flow.AnteNumber, flow.AntesPerRun, flow.IsRetry ? retryLabel : "");
    }

    void RefreshColumn(BlindColumnUI column, BlindKind kind)
    {
        if (column == null) return;

        bool isCurrent = kind == flow.CurrentBlind;
        SetText(column.nameText, GetBlindName(kind));
        SetText(column.targetText, string.Format(targetFormat, flow.GetBlindTarget(kind)));
        SetText(column.rewardText, string.Format(rewardFormat, flow.GetBlindRewardPercent(kind) / 100f));
        SetText(column.statusText, GetStatusLabel(kind, isCurrent));
        if (column.background != null) column.background.color = isCurrent ? currentColor : previewColor;

        RefreshButtons(column, kind, isCurrent);
    }

    // Select on the current blind only; Skip on the current blind only, and never on the Boss
    void RefreshButtons(BlindColumnUI column, BlindKind kind, bool isCurrent)
    {
        if (column.selectButton != null) column.selectButton.gameObject.SetActive(isCurrent);

        bool showSkip = isCurrent && kind != BlindKind.Boss;
        if (column.skipButton != null)
        {
            column.skipButton.gameObject.SetActive(showSkip);
            column.skipButton.interactable = flow.CanSkipBlind;   // feedback only: the conductor checks again
        }

        bool glassWhole = flow.GlassLives >= flow.GlassMaxLives;
        SetText(column.skipLabel, glassWhole ? skipGlassFullText : skipRepairText);
    }

    string GetBlindName(BlindKind kind)
    {
        switch (kind)
        {
            case BlindKind.Small: return smallName;
            case BlindKind.Big: return bigName;
            default: return bossName;
        }
    }

    string GetStatusLabel(BlindKind kind, bool isCurrent)
    {
        if (isCurrent) return currentLabel;

        switch (flow.GetBlindRecord(kind))
        {
            case BlindRecord.Won: return wonLabel;
            case BlindRecord.Failed: return failedLabel;
            case BlindRecord.Skipped: return skippedLabel;
            default: return upcomingLabel;
        }
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    // ---------- Button actions (public, so they can also be wired in the Inspector) ----------

    public void OnSelectClicked()
    {
        if (flow != null) flow.RequestSelectBlind();
        AudioManager.UISFX(SoundType.UI_Button);
    }

    public void OnSkipClicked()
    {
        if (flow != null) flow.RequestSkipBlind();
        AudioManager.UISFX(SoundType.UI_Button);
    }
}
