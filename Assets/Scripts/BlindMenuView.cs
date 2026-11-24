using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The blind menu (HUD Design §10.1): the three blinds of this Ante side by side, each with its status,
/// a big chip in the blind's colour with the target, and the reward. Only the current blind can be chosen
/// (Select, or Skip for Small / Big). Upcoming blinds stay fully bright; their status says they are upcoming.
/// A view: sends the click to the conductor.
public class BlindMenuView : MonoBehaviour
{
    // One card of the menu (Small, Big or Boss)
    [Serializable]
    public class BlindColumnUI
    {
        public TMP_Text statusText;
        public TMP_Text nameText;
        [Tooltip("Tinted with the blind's colour")]
        public Image chip;
        [Tooltip("The target, inside the chip")]
        public TMP_Text targetText;
        public TMP_Text scoreLineText;
        public TMP_Text rewardText;
        [Tooltip("Shown on blinds that are not current: whether they can be skipped")]
        public TMP_Text noteText;
        public Button selectButton;
        public Button skipButton;
        public TMP_Text skipLabel;
    }

    [Header("Game references")]
    public GameFlowController flow;
    public HudTheme theme;

    [Header("UI")]
    public GameObject panel;
    public TMP_Text headerText;
    public BlindColumnUI[] columns = new BlindColumnUI[3];

    [Header("Texts")]
    [Tooltip("{0} = Ante, {1} = Antes per run, {2} = retry label")]
    public string headerFormat = "Ante {0} / {1}{2}";
    public string retryLabel = " · Final Boss retry";
    public string smallName = "Small Blind";
    public string bigName = "Big Blind";
    public string bossName = "Boss Blind";
    public string targetFormat = "{0:N0}";
    [Tooltip("{0} = target")]
    public string scoreLineFormat = "Score at least {0:N0}";
    [Tooltip("{0} = reward as a multiplier, e.g. 1.2")]
    public string rewardFormat = "Reward ×{0:0.0#}";
    public string currentLabel = "Current";
    public string upcomingLabel = "Upcoming";
    public string wonLabel = "Won";
    public string failedLabel = "Failed";
    public string skippedLabel = "Skipped";
    public string skipRepairText = "Skip · repair 1 crack";
    public string skipGlassFullText = "Skip (Glass is whole)";
    public string canSkipNote = "Can be skipped";
    public string bossNote = "No skip · win repairs 1 crack";

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
        PanelSlideJuice.Show(panel, choosing);
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
        long target = flow.GetBlindTarget(kind);

        SetText(column.nameText, GetBlindName(kind));
        SetText(column.targetText, string.Format(targetFormat, target));
        SetText(column.scoreLineText, string.Format(scoreLineFormat, target));
        SetText(column.rewardText, string.Format(rewardFormat, flow.GetBlindReward(kind)));
        ShowStatus(column, kind, isCurrent);
        ShowNote(column, kind, isCurrent);
        if (column.chip != null && theme != null) column.chip.color = theme.GetBlindColor(kind);

        RefreshButtons(column, kind, isCurrent);
    }

    void ShowStatus(BlindColumnUI column, BlindKind kind, bool isCurrent)
    {
        if (column.statusText == null) return;
        BlindRecord record = flow.GetBlindRecord(kind);
        column.statusText.text = GetStatusLabel(record, isCurrent);
        if (theme != null) column.statusText.color = GetStatusColor(record, isCurrent);
    }

    // Upcoming blinds say whether they can be skipped; the current one has buttons instead
    void ShowNote(BlindColumnUI column, BlindKind kind, bool isCurrent)
    {
        if (column.noteText == null) return;
        bool upcoming = !isCurrent && flow.GetBlindRecord(kind) == BlindRecord.NotPlayed;
        column.noteText.gameObject.SetActive(upcoming);
        if (upcoming) column.noteText.text = kind == BlindKind.Boss ? bossNote : canSkipNote;
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

    string GetStatusLabel(BlindRecord record, bool isCurrent)
    {
        if (isCurrent) return currentLabel;

        switch (record)
        {
            case BlindRecord.Won: return wonLabel;
            case BlindRecord.Failed: return failedLabel;
            case BlindRecord.Skipped: return skippedLabel;
            default: return upcomingLabel;
        }
    }

    Color GetStatusColor(BlindRecord record, bool isCurrent)
    {
        if (isCurrent) return theme.safe;

        switch (record)
        {
            case BlindRecord.Won: return theme.goldText;
            case BlindRecord.Failed: return theme.danger;
            default: return theme.creamMuted;
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
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }

    public void OnSkipClicked()
    {
        if (flow != null) flow.RequestSkipBlind();
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
    }
}
