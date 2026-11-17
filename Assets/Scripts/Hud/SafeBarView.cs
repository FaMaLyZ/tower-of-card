using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The Safe bar (HUD Design §5.2): Safe Points of this blind against its target.
/// In the Bank / Ride choice a striped "ghost" shows where the bar would reach after a Bank.
/// Only Bank and Leave Table change Safe; scoring a hand does not.
public class SafeBarView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("Parts")]
    public TMP_Text amount;
    [Tooltip("Solid fill: anchored to the left of the track, its width is set from Safe / target")]
    public RectTransform fill;
    public Image fillImage;
    [Tooltip("Striped fill behind the solid one: where the bar would reach after a Bank")]
    public RectTransform previewFill;
    public TMP_Text overTag;

    [Header("Sprites")]
    public Sprite belowTargetSprite;
    public Sprite targetMetSprite;

    [Header("Text formats")]
    [Tooltip("{0} = Safe Points, {1} = target. Rich text colours the two parts")]
    public string amountFormat = "<color=#7DFFB6>{0:N0}</color> <color=#BFA77A>/ {1:N0}</color>";
    [Tooltip("Bank / Ride choice. {0} = Safe now, {1} = Safe after a Bank, {2} = target")]
    public string previewAmountFormat = "<color=#BFA77A>{0:N0} »</color> <color=#7DFFB6>{1:N0}</color> <color=#BFA77A>/ {2:N0}</color>";
    [Tooltip("{0} = points over the target")]
    public string overFormat = "+{0:N0} over";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void Update()
    {
        if (flow == null || !flow.IsReady || flow.Phase == GamePhase.None) return;

        long target = flow.GetBlindTarget(flow.CurrentBlind);
        long safe = flow.Phase == GamePhase.BlindMenu ? 0 : flow.SafePoints;     // the next blind starts at 0

        bool previewing = flow.IsChoosingBankOrRide;
        SetText(amount, previewing
            ? string.Format(previewAmountFormat, safe, flow.SafeAfterBank, target)
            : string.Format(amountFormat, safe, target));
        SetWidth(fill, Share(safe, target));
        if (fillImage != null) fillImage.sprite = flow.IsTargetReached && safe > 0 ? targetMetSprite : belowTargetSprite;

        if (previewFill != null)
        {
            previewFill.gameObject.SetActive(previewing);
            if (previewing) SetWidth(previewFill, Share(flow.SafeAfterBank, target));
        }

        bool over = flow.Phase != GamePhase.BlindMenu && flow.PointsOverTarget > 0;
        if (overTag != null)
        {
            overTag.gameObject.SetActive(over);
            if (over) overTag.text = string.Format(overFormat, flow.PointsOverTarget);
        }
    }

    // How full the bar is drawn (0 to 1). Display only: the rules judge the blind on Safe Points, not on this.
    static float Share(long value, long target)
    {
        return target > 0 ? Mathf.Clamp01((float)value / target) : 0f;
    }

    static void SetWidth(RectTransform bar, float share)
    {
        if (bar == null) return;
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(share, 1f);
        bar.offsetMin = Vector2.zero;
        bar.offsetMax = Vector2.zero;
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }
}
