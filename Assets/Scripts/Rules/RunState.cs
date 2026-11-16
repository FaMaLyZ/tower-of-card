/// The three blinds of an Ante, in the order they are played.
public enum BlindKind
{
    Small = 0,
    Big = 1,
    Boss = 2
}

/// What happened to a blind of the current Ante (for the blind menu).
public enum BlindRecord
{
    NotPlayed,
    Won,
    Failed,
    Skipped
}

/// Where the run stands.
public enum RunOutcome
{
    Playing,
    Cleared,
    Shattered
}

/// The whole run: which Ante and blind we are on, the run total, the reward multiplier and the Glass.
///
/// The conductor calls StartBlind, then exactly one of RecordBlindWon / RecordBlindFailed,
/// or SkipBlind instead of playing a Small or Big blind.
/// RunState then moves itself to the next blind, keeps the same one for a final-Boss retry,
/// or ends the run. It never changes a phase and never touches the table.
public class RunState
{
    const int BlindsPerAnte = 3;

    public int AntesPerRun { get; }
    public GlassLives Glass { get; }

    /// Counted from 1.
    public int AnteNumber { get; private set; } = 1;

    /// 0 = Small, 1 = Big, 2 = Boss.
    public int BlindIndex { get; private set; }

    public BlindKind CurrentBlind => (BlindKind)BlindIndex;

    /// Every banked point and every leave bonus, from won AND failed blinds.
    public long RunTotal { get; private set; }

    /// Multiplies future Banks. Starts at x1; each won blind multiplies it by that blind's reward.
    public decimal RewardMultiplier { get; private set; } = 1m;

    public RunOutcome Outcome { get; private set; } = RunOutcome.Playing;
    public bool IsOver => Outcome != RunOutcome.Playing;

    /// The last Ante's Boss. Winning it clears the run; failing it (with lives left) retries it.
    public bool IsFinalBoss => AnteNumber == AntesPerRun && CurrentBlind == BlindKind.Boss;

    /// True while the final Boss is being played again after a fail.
    public bool IsRetry { get; private set; }

    /// True between StartBlind and the blind's result. Guards "one result, one reward per blind".
    public bool IsBlindInProgress { get; private set; }

    /// Skip is allowed between blinds, on a Small or Big blind only. A Boss can never be skipped.
    public bool CanSkipCurrentBlind => !IsOver && !IsBlindInProgress && CurrentBlind != BlindKind.Boss;

    bool nextBlindIsBoss;
    readonly BlindRecord[] anteRecords = new BlindRecord[BlindsPerAnte];

    public RunState(int antesPerRun, int glassMaxLives)
    {
        AntesPerRun = antesPerRun < 1 ? 1 : antesPerRun;
        Glass = new GlassLives(glassMaxLives);
    }

    /// The current blind begins. Nothing else changes: Run Total, reward and Glass carry over.
    public void StartBlind()
    {
        if (IsOver) return;
        IsBlindInProgress = true;
    }

    /// Banked points and leave bonuses. Counted no matter how the blind ends.
    public void AddToRunTotal(long points)
    {
        if (points > 0) RunTotal += points;
    }

    /// Win: the reward is applied once; a Boss also repairs one crack.
    /// Then the final Boss clears the run, any other blind moves on.
    /// Returns false (and changes nothing) if no blind is in progress.
    public bool RecordBlindWon(int rewardPercent)
    {
        if (!IsBlindInProgress) return false;
        IsBlindInProgress = false;

        anteRecords[BlindIndex] = BlindRecord.Won;
        ApplyBlindReward(rewardPercent);
        if (CurrentBlind == BlindKind.Boss) Glass.Repair();

        if (IsFinalBoss) Outcome = RunOutcome.Cleared;
        else MoveToNextBlind();
        return true;
    }

    /// Fail: the Glass cracks once. No lives left: shattered.
    /// The final Boss with lives left is played again (same target); any other blind moves on.
    /// Returns false (and changes nothing) if no blind is in progress.
    public bool RecordBlindFailed()
    {
        if (!IsBlindInProgress) return false;
        IsBlindInProgress = false;

        anteRecords[BlindIndex] = BlindRecord.Failed;
        Glass.Crack();

        if (Glass.IsShattered) Outcome = RunOutcome.Shattered;
        else if (IsFinalBoss) IsRetry = true;
        else MoveToNextBlind();
        return true;
    }

    /// Skip: no score, no reward, one crack repaired (never above the maximum), then the next blind.
    /// Returns false (and changes nothing) if this blind cannot be skipped.
    public bool SkipBlind()
    {
        if (!CanSkipCurrentBlind) return false;

        Glass.Repair();
        anteRecords[BlindIndex] = BlindRecord.Skipped;
        MoveToNextBlind();
        return true;
    }

    /// Won, failed, skipped or not played yet, for a blind of the current Ante.
    public BlindRecord GetRecord(BlindKind kind)
    {
        return anteRecords[(int)kind];
    }

    /// Debug key F5: whatever comes next becomes a Boss (in this Ante, or the next one if we are on the Boss).
    public void MakeNextBlindBoss()
    {
        nextBlindIsBoss = true;
    }

    void ApplyBlindReward(int rewardPercent)
    {
        RewardMultiplier *= PercentMath.ToRate(rewardPercent);
    }

    void MoveToNextBlind()
    {
        IsRetry = false;
        BlindIndex++;
        if (BlindIndex >= BlindsPerAnte)
        {
            BlindIndex = 0;
            AnteNumber++;
            ClearAnteRecords();
        }

        if (nextBlindIsBoss)
        {
            BlindIndex = (int)BlindKind.Boss;
            nextBlindIsBoss = false;
        }
    }

    void ClearAnteRecords()
    {
        for (int i = 0; i < anteRecords.Length; i++)
            anteRecords[i] = BlindRecord.NotPlayed;
    }
}
