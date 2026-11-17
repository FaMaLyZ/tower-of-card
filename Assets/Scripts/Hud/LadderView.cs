using TMPro;
using UnityEngine;

/// The tower height ladder (HUD Design §6): one dot per standing level, Level 1 at the bottom.
/// Only the highest lit level has a label: "Level 2 / +20% height". During the shake it reads "settling...".
public class LadderView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("Parts")]
    [Tooltip("Dots ordered bottom (Level 1) to top")]
    public PipRow dots;
    [Tooltip("Moves up and down beside the highest lit dot")]
    public RectTransform label;
    public TMP_Text levelText;
    public TMP_Text bonusText;

    [Header("Texts")]
    [Tooltip("{0} = standing levels")]
    public string levelFormat = "Level {0}";
    [Tooltip("{0} = height bonus percent")]
    public string bonusFormat = "+{0}% height";
    public string settlingText = "settling...";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void Update()
    {
        if (flow == null || !flow.IsReady || flow.Phase == GamePhase.None || dots == null) return;

        int levels = flow.StandingLevels;
        dots.Show(levels, dots.pips.Count);

        if (flow.Phase == GamePhase.Shake) ShowLabel(levels, settlingText, "");
        else if (levels > 0) ShowLabel(levels, string.Format(levelFormat, levels), string.Format(bonusFormat, flow.HeightBonusPercent));
        else HideLabel();
    }

    // The label sits beside the highest lit dot (or the bottom dot while settling with none lit)
    void ShowLabel(int levels, string level, string bonus)
    {
        if (label == null) return;
        label.gameObject.SetActive(true);

        int index = Mathf.Clamp(levels - 1, 0, dots.pips.Count - 1);
        RectTransform dot = dots.pips[index].rectTransform;
        label.position = new Vector3(label.position.x, dot.position.y, label.position.z);

        if (levelText != null) levelText.text = level;
        if (bonusText != null) bonusText.text = bonus;
    }

    void HideLabel()
    {
        if (label != null) label.gameObject.SetActive(false);
    }
}
