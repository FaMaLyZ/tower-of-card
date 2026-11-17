using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The blind plaque at the top of the right HUD (HUD Design §5.1): name, target and reward,
/// in the blind's colour. In the blind menu it already shows the blind being offered.
public class BlindPlaqueView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public HudTheme theme;

    [Header("Parts")]
    [Tooltip("Tinted with the blind's colour")]
    public Image plaque;
    public TMP_Text blindName;
    public TMP_Text target;
    public TMP_Text reward;

    [Header("Text formats")]
    [Tooltip("{0} = Small / Big / Boss")]
    public string nameFormat = "{0} Blind";
    public string targetFormat = "{0:N0}";
    [Tooltip("{0} = the reward multiplier a win gives")]
    public string rewardFormat = "Reward ×{0:0.0#}";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void Update()
    {
        if (flow == null || !flow.IsReady || flow.Phase == GamePhase.None) return;

        BlindKind kind = flow.CurrentBlind;
        SetText(blindName, string.Format(nameFormat, kind));
        SetText(target, string.Format(targetFormat, flow.GetBlindTarget(kind)));
        SetText(reward, string.Format(rewardFormat, flow.GetBlindReward(kind)));
        if (plaque != null && theme != null) plaque.color = theme.GetBlindColor(kind);
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }
}
