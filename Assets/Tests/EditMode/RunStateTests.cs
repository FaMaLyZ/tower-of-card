using NUnit.Framework;

public class RunStateTests
{
    static RunState MakeRun() => new RunState(4, 3);       // 4 Antes, 3 lives

    static void WinBlind(RunState run, int rewardPercent = 100)
    {
        run.StartBlind();
        run.RecordBlindWon(rewardPercent);
    }

    static void FailBlind(RunState run)
    {
        run.StartBlind();
        run.RecordBlindFailed();
    }

    // Plays wins until the final Boss is the current blind (11 blinds)
    static void WinUpToFinalBoss(RunState run)
    {
        while (!run.IsFinalBoss) WinBlind(run);
    }

    [Test]
    public void StartsOnAnteOneSmallWithRewardOne()
    {
        RunState run = MakeRun();

        Assert.AreEqual(1, run.AnteNumber);
        Assert.AreEqual(BlindKind.Small, run.CurrentBlind);
        Assert.AreEqual(1m, run.RewardMultiplier);
        Assert.AreEqual(0L, run.RunTotal);
        Assert.AreEqual(RunOutcome.Playing, run.Outcome);
    }

    [Test]
    public void BlindsGoSmallBigBossThenTheNextAnte()
    {
        RunState run = MakeRun();

        WinBlind(run);
        Assert.AreEqual(BlindKind.Big, run.CurrentBlind);
        WinBlind(run);
        Assert.AreEqual(BlindKind.Boss, run.CurrentBlind);
        WinBlind(run);
        Assert.AreEqual(BlindKind.Small, run.CurrentBlind);
        Assert.AreEqual(2, run.AnteNumber);
    }

    [Test]
    public void RunTotalKeepsPointsFromAFailedBlind()
    {
        RunState run = MakeRun();
        run.StartBlind();
        run.AddToRunTotal(120);
        run.RecordBlindFailed();

        Assert.AreEqual(120L, run.RunTotal);
    }

    [Test]
    public void ARewardIsAppliedOncePerWonBlind()
    {
        RunState run = MakeRun();
        run.StartBlind();

        Assert.IsTrue(run.RecordBlindWon(120));
        Assert.IsFalse(run.RecordBlindWon(120));           // second result for the same blind: ignored

        Assert.AreEqual(1.2m, run.RewardMultiplier);
        Assert.AreEqual(BlindKind.Big, run.CurrentBlind);  // moved on once, not twice
    }

    [Test]
    public void RewardsMultiply()
    {
        RunState run = MakeRun();
        WinBlind(run, 120);
        WinBlind(run, 140);

        Assert.AreEqual(1.68m, run.RewardMultiplier);
    }

    [Test]
    public void AFailGivesNoRewardAndCracksOnce()
    {
        RunState run = MakeRun();
        FailBlind(run);

        Assert.AreEqual(1m, run.RewardMultiplier);
        Assert.AreEqual(2, run.Glass.LivesRemaining);
        Assert.AreEqual(BlindKind.Big, run.CurrentBlind);   // a failed Small moves on
    }

    [Test]
    public void ABossWinRepairsOneCrack()
    {
        RunState run = MakeRun();
        FailBlind(run);                                     // Small failed: 2 lives
        WinBlind(run);                                      // Big
        WinBlind(run);                                      // Boss: repair

        Assert.AreEqual(3, run.Glass.LivesRemaining);
    }

    [Test]
    public void ABossWinNeverRepairsAboveTheMaximum()
    {
        RunState run = MakeRun();
        WinBlind(run);
        WinBlind(run);
        WinBlind(run);                                      // Boss with full Glass

        Assert.AreEqual(3, run.Glass.LivesRemaining);
    }

    [Test]
    public void ThreeFailsShatterTheRun()
    {
        RunState run = MakeRun();
        FailBlind(run);
        FailBlind(run);
        FailBlind(run);

        Assert.AreEqual(RunOutcome.Shattered, run.Outcome);
        Assert.IsTrue(run.IsOver);
    }

    [Test]
    public void WinningTheFinalBossClearsTheRun()
    {
        RunState run = MakeRun();
        WinUpToFinalBoss(run);
        Assert.AreEqual(4, run.AnteNumber);

        WinBlind(run);

        Assert.AreEqual(RunOutcome.Cleared, run.Outcome);
    }

    [Test]
    public void FailingTheFinalBossWithLivesLeftRetriesTheSameBoss()
    {
        RunState run = MakeRun();
        WinUpToFinalBoss(run);
        run.StartBlind();
        run.AddToRunTotal(80);
        run.RecordBlindFailed();

        Assert.AreEqual(RunOutcome.Playing, run.Outcome);
        Assert.IsTrue(run.IsFinalBoss);
        Assert.IsTrue(run.IsRetry);
        Assert.AreEqual(2, run.Glass.LivesRemaining);
        Assert.AreEqual(80L, run.RunTotal);
    }

    [Test]
    public void FailingTheFinalBossOnTheLastLifeShatters()
    {
        RunState run = new RunState(4, 1);                  // one life only
        WinUpToFinalBoss(run);

        FailBlind(run);

        Assert.AreEqual(RunOutcome.Shattered, run.Outcome);
    }

    [Test]
    public void NoResultWithoutAStartedBlind()
    {
        RunState run = MakeRun();

        Assert.IsFalse(run.RecordBlindFailed());
        Assert.AreEqual(3, run.Glass.LivesRemaining);
    }

    [Test]
    public void MakeNextBlindBossSkipsToTheBoss()
    {
        RunState run = MakeRun();
        run.MakeNextBlindBoss();
        WinBlind(run);                                      // Small won: next is the Boss, not Big

        Assert.AreEqual(BlindKind.Boss, run.CurrentBlind);
        Assert.AreEqual(1, run.AnteNumber);
    }

    // ---------- Skip (A4c) ----------

    [Test]
    public void SkippingSmallRepairsOneCrackAndMovesToBig()
    {
        RunState run = MakeRun();
        run.Glass.Crack();                                  // 2 lives

        Assert.IsTrue(run.SkipBlind());

        Assert.AreEqual(3, run.Glass.LivesRemaining);
        Assert.AreEqual(BlindKind.Big, run.CurrentBlind);
        Assert.AreEqual(BlindRecord.Skipped, run.GetRecord(BlindKind.Small));
    }

    [Test]
    public void SkipGivesNoRewardAndNoScore()
    {
        RunState run = MakeRun();
        run.SkipBlind();

        Assert.AreEqual(1m, run.RewardMultiplier);
        Assert.AreEqual(0L, run.RunTotal);
    }

    [Test]
    public void SkipRepairNeverGoesAboveTheMaximum()
    {
        RunState run = MakeRun();                           // full Glass
        run.SkipBlind();
        run.SkipBlind();

        Assert.AreEqual(3, run.Glass.LivesRemaining);
    }

    [Test]
    public void TheBossCannotBeSkipped()
    {
        RunState run = MakeRun();
        run.SkipBlind();                                    // Small
        run.SkipBlind();                                    // Big
        run.Glass.Crack();

        Assert.IsFalse(run.CanSkipCurrentBlind);
        Assert.IsFalse(run.SkipBlind());
        Assert.AreEqual(BlindKind.Boss, run.CurrentBlind);
        Assert.AreEqual(2, run.Glass.LivesRemaining);       // no repair
    }

    [Test]
    public void ABlindInProgressCannotBeSkipped()
    {
        RunState run = MakeRun();
        run.StartBlind();

        Assert.IsFalse(run.SkipBlind());
        Assert.AreEqual(BlindKind.Small, run.CurrentBlind);
    }

    [Test]
    public void RecordsShowThisAnteOnlyAndClearOnTheNextAnte()
    {
        RunState run = MakeRun();
        run.SkipBlind();                                    // Small skipped
        FailBlind(run);                                     // Big failed

        Assert.AreEqual(BlindRecord.Skipped, run.GetRecord(BlindKind.Small));
        Assert.AreEqual(BlindRecord.Failed, run.GetRecord(BlindKind.Big));
        Assert.AreEqual(BlindRecord.NotPlayed, run.GetRecord(BlindKind.Boss));

        WinBlind(run);                                      // Boss won: Ante 2

        Assert.AreEqual(BlindRecord.NotPlayed, run.GetRecord(BlindKind.Small));
        Assert.AreEqual(BlindRecord.NotPlayed, run.GetRecord(BlindKind.Big));
    }

    [Test]
    public void RemembersTheRewardBeforeTheLastWin()
    {
        RunState run = MakeRun();
        Assert.AreEqual(1m, run.RewardMultiplierBeforeLastWin);

        WinBlind(run, 120);
        Assert.AreEqual(1m, run.RewardMultiplierBeforeLastWin);
        Assert.AreEqual(1.2m, run.RewardMultiplier);

        WinBlind(run, 140);
        Assert.AreEqual(1.2m, run.RewardMultiplierBeforeLastWin);
        Assert.AreEqual(1.68m, run.RewardMultiplier);
    }

    [Test]
    public void AFailDoesNotChangeTheRewardBeforeTheLastWin()
    {
        RunState run = MakeRun();
        WinBlind(run, 120);
        FailBlind(run);

        Assert.AreEqual(1m, run.RewardMultiplierBeforeLastWin);
    }
}
