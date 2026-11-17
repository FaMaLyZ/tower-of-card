using TMPro;
using UnityEngine;

/// The breakdown rows under the Broken strip (HUD Design §5.5).
/// Shake: what the shake can cost. Bank / Ride: how the Bank payout is made. Other phases: empty.
/// Every value comes from the game; floor(raw pot x height x reward) is never worked out here.
public class BreakdownView : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public GameObject root;
        public TMP_Text name;
        public TMP_Text value;
    }

    [Header("Game references")]
    public GameFlowController flow;
    public HudTheme theme;

    [Header("Rows, top to bottom")]
    public Row[] rows = new Row[3];

    [Header("Texts")]
    public string breaksName = "Breaks";
    [Tooltip("{0} = Pot percent each broken card takes")]
    public string breaksValueFormat = "−{0}% each";
    public string collapseName = "Collapse";
    public string collapseValue = "lose all";
    public string rawPotName = "Raw pot";
    public string rawPotFormat = "{0:N0}";
    [Tooltip("{0} = standing levels")]
    public string heightNameFormat = "Height ({0} levels)";
    public string heightNameOneLevel = "Height (1 level)";
    [Tooltip("{0} = height bonus percent")]
    public string heightValueFormat = "+{0}%";
    public string rewardName = "Reward";
    [Tooltip("{0} = the run's reward multiplier")]
    public string rewardValueFormat = "×{0:0.0#}";

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void Update()
    {
        if (flow == null || !flow.IsReady || flow.Phase == GamePhase.None) return;

        if (flow.Phase == GamePhase.Shake) ShowShakeRisks();
        else if (flow.Phase == GamePhase.BankOrRide) ShowBankBreakdown();
        else HideFrom(0);
    }

    void ShowShakeRisks()
    {
        Color danger = theme != null ? theme.danger : Color.white;
        ShowRow(0, breaksName, string.Format(breaksValueFormat, flow.config.breakPercent), danger);
        ShowRow(1, collapseName, collapseValue, danger);
        HideFrom(2);
    }

    void ShowBankBreakdown()
    {
        int levels = flow.StandingLevels;
        string heightName = levels == 1 ? heightNameOneLevel : string.Format(heightNameFormat, levels);

        ShowRow(0, rawPotName, string.Format(rawPotFormat, flow.PotChips), theme != null ? theme.goldText : Color.white);
        ShowRow(1, heightName, string.Format(heightValueFormat, flow.HeightBonusPercent), theme != null ? theme.safe : Color.white);
        ShowRow(2, rewardName, string.Format(rewardValueFormat, flow.RewardMultiplier), theme != null ? theme.danger : Color.white);
    }

    void ShowRow(int index, string name, string value, Color valueColor)
    {
        if (index >= rows.Length || rows[index] == null) return;
        Row row = rows[index];
        if (row.root != null) row.root.SetActive(true);
        if (row.name != null) row.name.text = name;
        if (row.value != null)
        {
            row.value.text = value;
            row.value.color = valueColor;
        }
    }

    void HideFrom(int first)
    {
        for (int i = first; i < rows.Length; i++)
            if (rows[i] != null && rows[i].root != null) rows[i].root.SetActive(false);
    }
}
