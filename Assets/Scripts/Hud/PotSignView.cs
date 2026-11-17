using TMPro;
using UnityEngine;

/// The Pot sign (HUD Design §5.3): a label, a big number and an explanation line, changing by phase.
/// In the Bank / Ride choice the number is what a Bank pays, because that is what the player is deciding about.
/// The explanation numbers come from the PotChanged event (old and new Pot); this view never subtracts.
public class PotSignView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public ScoreManager scoreManager;

    [Header("Parts")]
    public TMP_Text label;
    public TMP_Text number;
    public TMP_Text line;

    [Header("Texts")]
    public string potLabel = "POT";
    public string ridingLabel = "Riding on the tower";
    public string unsettledLabel = "POT (unsettled)";
    public string bankNowLabel = "BANK NOW";
    public string numberFormat = "{0:N0}";
    public string clearedLine = "Tower cleared";
    [Tooltip("{0} = what the last Bank paid into Safe")]
    public string bankedLine = "Banked {0:N0} into Safe";
    public string collapsedLine = "The tower collapsed";
    public string rodeLine = "Rode from the last hand";
    [Tooltip("{0} = Pot before this hand, {1} = this hand's score")]
    public string scoredLine = "{0:N0} + {1:N0} from this hand";
    public string scoringLine = "Scoring...";
    public string settlingLine = "The tower is settling...";
    [Tooltip("{0} = Pot before the shake, {1} = broken cards, {2} = Pot after")]
    public string brokeOneLine = "{0:N0} − {1} broken card = {2:N0}";
    public string brokeManyLine = "{0:N0} − {1} broken cards = {2:N0}";
    public string nothingBrokeLine = "Nothing broke";

    long potBeforeLastChange;
    long potAfterLastChange;
    PotChangeReason lastEmptiedBy = PotChangeReason.Cleared;   // why the Pot last went to 0
    long lastBankPayout;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
    }

    void OnEnable()
    {
        GameEvents.PotChanged += OnPotChanged;
        GameEvents.PotBanked += OnPotBanked;
    }

    void OnDisable()
    {
        GameEvents.PotChanged -= OnPotChanged;
        GameEvents.PotBanked -= OnPotBanked;
    }

    void OnPotChanged(long oldChips, long newChips, PotChangeReason reason, long bankQuote)
    {
        potBeforeLastChange = oldChips;
        potAfterLastChange = newChips;
        if (newChips == 0) lastEmptiedBy = reason;
    }

    void OnPotBanked(long payout, long safePoints, long runTotal)
    {
        lastBankPayout = payout;
    }

    void Update()
    {
        if (flow == null || !flow.IsReady || flow.Phase == GamePhase.None) return;

        switch (flow.Phase)
        {
            case GamePhase.Draw:
            case GamePhase.Swap:
            case GamePhase.Commit:
            case GamePhase.Build:      ShowBeforeScoring(); break;
            case GamePhase.Score:      ShowScored(); break;
            case GamePhase.Shake:      Show(unsettledLabel, flow.PotChips, settlingLine); break;
            case GamePhase.BankOrRide: ShowBankQuote(); break;
            default:                   Show(potLabel, flow.PotChips, GetEmptyLine()); break;
        }
    }

    // A Ride carries the Pot into the next hand; otherwise the tower is empty
    void ShowBeforeScoring()
    {
        if (flow.PotChips > 0) Show(ridingLabel, flow.PotChips, rodeLine);
        else Show(potLabel, 0, GetEmptyLine());
    }

    // An empty Pot says where its points went, so a 0 never looks like lost score
    string GetEmptyLine()
    {
        switch (lastEmptiedBy)
        {
            case PotChangeReason.Banked:    return string.Format(bankedLine, lastBankPayout);
            case PotChangeReason.Collapsed: return collapsedLine;
            default:                        return clearedLine;
        }
    }

    // The Pot already holds the hand's score, but the plaque is still counting it up:
    // keep showing the old Pot until the count lands, so the two never disagree
    void ShowScored()
    {
        bool counting = scoreManager != null && scoreManager.IsScoring;
        if (counting)
        {
            Show(potLabel, potBeforeLastChange, scoringLine);
            return;
        }

        long handScore = scoreManager != null ? scoreManager.TurnScore : 0;
        Show(potLabel, flow.PotChips, string.Format(scoredLine, potBeforeLastChange, handScore));
    }

    void ShowBankQuote()
    {
        ShakeResult shake = flow.LastShake;
        int broken = shake != null ? shake.BrokenCount : 0;

        string explanation = broken == 0
            ? nothingBrokeLine
            : string.Format(broken == 1 ? brokeOneLine : brokeManyLine, potBeforeLastChange, broken, potAfterLastChange);
        Show(bankNowLabel, flow.BankQuote, explanation);
    }

    void Show(string labelText, long value, string lineText)
    {
        if (label != null) label.text = labelText;
        if (number != null) number.text = string.Format(numberFormat, value);
        if (line != null) line.text = lineText;
    }
}

