using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The Broken strip (HUD Design §5.4): one cracked-card icon per card broken in the last shake,
/// and the Pot it cost. During the shake the icons appear as cards break; the penalty shows once it settles.
public class BrokenStripView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public HudTheme theme;

    [Header("Parts")]
    [Tooltip("Icon slots, left to right. Extra breaks beyond the slots still count in the penalty")]
    public List<Image> icons = new List<Image>();
    public TMP_Text penalty;

    [Header("Texts")]
    public string noneText = "none";
    public string settlingText = "...";
    [Tooltip("{0} = Pot percent lost (breaks multiply: 2 = 19%)")]
    public string penaltyFormat = "−{0:0.#}%";
    public string collapsedText = "lost all";

    int brokenThisShake;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void OnEnable()
    {
        GameEvents.PhaseChanged += OnPhaseChanged;
        GameEvents.CardBroken += OnCardBroken;
    }

    void OnDisable()
    {
        GameEvents.PhaseChanged -= OnPhaseChanged;
        GameEvents.CardBroken -= OnCardBroken;
    }

    void OnPhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Shake) brokenThisShake = 0;
    }

    void OnCardBroken(Card card)
    {
        if (flow != null && flow.Phase == GamePhase.Shake) brokenThisShake++;
    }

    void Update()
    {
        if (flow == null || !flow.IsReady || flow.Phase == GamePhase.None) return;

        if (flow.Phase == GamePhase.Shake) ShowShaking();
        else ShowLastShake(flow.LastShake);
    }

    void ShowShaking()
    {
        ShowIcons(brokenThisShake);
        ShowPenalty(settlingText, theme != null ? theme.danger : Color.white);
    }

    void ShowLastShake(ShakeResult shake)
    {
        int broken = shake != null ? shake.BrokenCount : 0;
        ShowIcons(broken);

        if (shake != null && shake.Collapsed) ShowPenalty(collapsedText, theme != null ? theme.danger : Color.white);
        else if (broken == 0) ShowPenalty(noneText, theme != null ? theme.safe : Color.white);
        else ShowPenalty(string.Format(penaltyFormat, flow.LastShakeLossPercent), theme != null ? theme.danger : Color.white);
    }

    void ShowIcons(int count)
    {
        for (int i = 0; i < icons.Count; i++)
            if (icons[i] != null) icons[i].gameObject.SetActive(i < count);
    }

    void ShowPenalty(string text, Color color)
    {
        if (penalty == null) return;
        penalty.text = text;
        penalty.color = color;
    }
}
