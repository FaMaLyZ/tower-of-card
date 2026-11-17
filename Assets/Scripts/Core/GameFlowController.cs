using System.Collections;
using UnityEngine;

/// The conductor: the ONLY script that decides what happens next.
/// Input sends commands here (RequestXxx); the conductor checks the phase, asks a worker, and moves on.
///
///   Hand:  Draw, Swap, Commit, Build, Score, Shake, then Bank or Ride.
///   Blind: a target and a few hands; the last hand banks by itself. Early target -> Continue / Leave.
///   Run:   Small, Big, Boss per Ante. A fail cracks the Glass; a Boss win or a Skip repairs it.
public class GameFlowController : MonoBehaviour
{
    [Header("Settings")]
    [RequiredField] public GameConfig config;

    [Header("Workers")]
    public HandManager hand;
    public DeckManager deck;
    public SwapManager swap;
    public TowerBuilder tower;
    public TowerPlacementInput placement;
    public ScoreManager score;

    // ---------- Read-only info (the HUD reads these) ----------
    public GamePhase Phase { get; private set; } = GamePhase.None;
    public float SecondsInPhase => Time.time - phaseStartTime;
    public int HandNumber { get; private set; }
    public int CardsPlacedThisHand => tower != null ? tower.CardsPlacedThisHand : 0;

    // The blind (the HUD reads these)
    public int HandsPerBlind => blind.HandsPerBlind;
    public int HandsLeft => blind.HandsLeft;
    public long Target => blind.Target;
    public bool IsBlindOver => Phase == GamePhase.BlindEnd;
    public BlindResult LastBlindResult { get; private set; }

    /// The blind that was just played (the run has already moved on when the result is shown).
    public BlindKind LastBlindKind { get; private set; }

    // The run (the HUD reads these)
    public int AnteNumber => run.AnteNumber;
    public int AntesPerRun => run.AntesPerRun;
    public BlindKind CurrentBlind => run.CurrentBlind;
    public int BlindNumber => (int)run.CurrentBlind + 1;       // 1 = Small, 2 = Big, 3 = Boss (for "Blind 1/3")
    public int BlindsPerAnte => RunState.BlindsPerAnte;
    public bool IsRetry => run.IsRetry;
    public long RunTotal => run.RunTotal;
    public decimal RewardMultiplier => run.RewardMultiplier;
    public decimal RewardMultiplierBeforeLastWin => run.RewardMultiplierBeforeLastWin;
    public int GlassLives => run.Glass.LivesRemaining;
    public int GlassMaxLives => run.Glass.MaxLives;
    public RunOutcome RunOutcome => run.Outcome;
    public bool IsRunOver => Phase == GamePhase.RunEnd;

    // The blind menu (only the current blind can be chosen; the other two are previews)
    public bool IsChoosingBlind => Phase == GamePhase.BlindMenu && blindMenuChoice == BlindMenuChoice.None;
    public bool CanSkipBlind => IsChoosingBlind && run.CanSkipCurrentBlind;
    public BlindRecord GetBlindRecord(BlindKind kind) => run.GetRecord(kind);
    public int GetBlindRewardPercent(BlindKind kind) => config.GetRewardPercent(kind);

    /// The multiplier a win of this blind gives, e.g. 1.2 (the blind plaque's "Reward x1.2").
    public decimal GetBlindReward(BlindKind kind) => PercentMath.ToRate(config.GetRewardPercent(kind));

    /// What a blind of the current Ante needs (the same formula BlindManager uses when it starts).
    public long GetBlindTarget(BlindKind kind) =>
        BlindManager.CalculateTarget(config.GetBaseTarget(kind), run.AnteNumber, config.targetGrowthPercent);

    enum BlindMenuChoice { None, Select, Skip }
    BlindMenuChoice blindMenuChoice;

    /// The commit worker: the live hand name, why Play Hand is refused, the committed cards.
    public CommitManager Commit { get; private set; }

    // The Pot (unsafe points). The HUD shows these numbers; it never redoes the maths.
    public long PotChips => pot.PotChips;
    public ShakeResult LastShake { get; private set; }
    public int StandingLevels => LastShake != null ? LastShake.StandingLevels : 0;
    public long BankQuote => pot.QuoteBankPayout(StandingLevels, RewardMultiplier);

    /// The height bonus a Bank would get now, as a percent: 2 standing levels at 10% = 20.
    public int HeightBonusPercent => config.heightPercent * StandingLevels;

    /// The share of the Pot the last shake's breaks took, as a percent (0 when nothing broke or it collapsed).
    public decimal LastShakeLossPercent =>
        LastShake != null && !LastShake.Collapsed ? pot.GetBreakLossPercent(LastShake.BrokenCount) : 0m;

    /// False from the moment a hand is scored until its shake is resolved: the Pot can still change.
    public bool IsPotSettled { get; private set; } = true;

    /// Points that are already safe. A collapse never touches them.
    public long SafePoints => blind.BlindScore;

    /// What Safe Points would be after a Bank now (the Safe bar preview and the drawer receipt).
    public long SafeAfterBank => blind.BlindScore + BankQuote;

    /// True once Safe Points meet the blind's target (the Safe bar turns gold).
    public bool IsTargetReached => blind.IsTargetReached();

    /// True if a Bank now would meet the blind's target (the receipt's tick).
    public bool BankReachesTarget => SafeAfterBank >= blind.Target;

    /// Safe Points above the target ("+36 over"); 0 while the target is not met.
    public long PointsOverTarget => blind.BlindScore > blind.Target ? blind.BlindScore - blind.Target : 0;

    // The Bank / Ride choice
    public bool IsChoosingBankOrRide => Phase == GamePhase.BankOrRide && bankOrRideChoice == BankOrRideChoice.None;

    /// Ride needs somewhere to put the next card. A full tower can only be Banked.
    public bool CanRide => IsChoosingBankOrRide && tower.HasLegalPlacement();

    enum BankOrRideChoice { None, Bank, Ride }
    BankOrRideChoice bankOrRideChoice;

    // The Continue / Leave Table choice (only after a Bank that reached the target with hands left)
    public bool IsChoosingContinueOrLeave => Phase == GamePhase.ContinueOrLeave && continueOrLeaveChoice == ContinueOrLeaveChoice.None;

    /// What Leave Table would pay now. The view shows it; it never redoes the maths.
    public long LeaveBonusQuote => blind.GetLeaveTableBonus();

    /// True if the blind just ended through Leave Table (the end screen can say so).
    public bool LeftTable => blind.HasLeftTable;
    public long LeaveBonusPaid => blind.LeaveBonusPaid;

    /// Safe Points that came from Banks this blind (Safe Points without the leave bonus), for the result panel.
    public long BankedThisBlind => blind.BlindScore - blind.LeaveBonusPaid;

    enum ContinueOrLeaveChoice { None, Continue, Leave }
    ContinueOrLeaveChoice continueOrLeaveChoice;

    // Which commands count right now (the HUD uses these to grey out buttons)
    public bool IsSwapping => Phase == GamePhase.Swap && !doneSwappingRequested;
    public bool IsCommitting => Phase == GamePhase.Commit && !playHandAccepted;
    public bool IsBuilding => Phase == GamePhase.Build;

    /// From Play Hand until the next hand starts: the committed cards are being placed or have been played,
    /// and the rest of the hand waits. The hand layout pulls the waiting cards down while this is true.
    public bool IsHandLockedIn =>
        Phase == GamePhase.Build || Phase == GamePhase.Score || Phase == GamePhase.Shake ||
        Phase == GamePhase.BankOrRide || Phase == GamePhase.ContinueOrLeave;
    public bool CanClickHandCards => IsSwapping || IsCommitting || IsBuilding;
    /// The hand order is only for the player's eyes, so it may change whenever the hand can be clicked.
    /// Reordering is for Swap and Commit. In Build a drag picks the card up to place it instead.
    public bool CanReorderHand => IsSwapping || IsCommitting;
    public bool CanSwapSelectedCard => IsSwapping && swap != null && swap.CanSwap;
    public bool CanPlayHand => IsCommitting && Commit.Problem == "";
    public bool CanUndo => IsBuilding && CardsPlacedThisHand > 0;

    PotManager pot;
    BlindManager blind;
    RunState run;

    /// False after a script reload in Play mode: the plain C# workers are gone until Play is restarted.
    public bool IsReady => blind != null && run != null && pot != null;

    Coroutine flow;
    float phaseStartTime;
    bool doneSwappingRequested;
    bool playHandAccepted;

    // =====================================================================
    //  Lifecycle
    // =====================================================================

    void Awake()
    {
        FindMissingWorkers();
        CreatePlainWorkers();
        WarnIfSecondConductorExists();
        AddDebugCheats();
    }

    void Start()
    {
        if (!HasEverythingItNeeds()) return;
        StartNewRun();
    }

    void OnDisable()
    {
        StopFlow();
    }

    void FindMissingWorkers()
    {
        if (hand == null) hand = FindAnyObjectByType<HandManager>();
        if (deck == null) deck = FindAnyObjectByType<DeckManager>();
        if (swap == null) swap = FindAnyObjectByType<SwapManager>();
        if (tower == null) tower = FindAnyObjectByType<TowerBuilder>();
        if (placement == null) placement = FindAnyObjectByType<TowerPlacementInput>();
        if (score == null) score = FindAnyObjectByType<ScoreManager>();
    }

    // Workers that are plain C# (not components) are made here
    void CreatePlainWorkers()
    {
        int maxCommitCards = config != null ? config.maxCommitCards : 5;
        Commit = new CommitManager(hand, tower, score, maxCommitCards);

        int breakPercent = config != null ? config.breakPercent : 10;
        int heightPercent = config != null ? config.heightPercent : 10;
        pot = new PotManager(breakPercent, heightPercent);
        int handsPerBlind = config != null ? config.handsPerBlind : 4;
        int targetGrowthPercent = config != null ? config.targetGrowthPercent : 150;
        int leaveBonusPercent = config != null ? config.leaveBonusPercent : 10;
        blind = new BlindManager(handsPerBlind, targetGrowthPercent, leaveBonusPercent);
        run = CreateRunState();
    }

    RunState CreateRunState()
    {
        int antesPerRun = config != null ? config.antesPerRun : 4;
        int glassMaxLives = config != null ? config.glassMaxLives : 3;
        return new RunState(antesPerRun, glassMaxLives);
    }

    void WarnIfSecondConductorExists()
    {
        int count = FindObjectsByType<GameFlowController>().Length;
        if (count > 1) Debug.LogError($"GameFlowController: {count} conductors in the scene. There must be exactly one.", this);
    }

    void AddDebugCheats()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<DebugCheats>() == null) gameObject.AddComponent<DebugCheats>();
#endif
    }

    /// Names every missing piece, so a broken scene says what is wrong instead of doing nothing.
    bool HasEverythingItNeeds()
    {
        bool ok = true;
        ok &= CheckExists(config, "GameConfig (drag Assets/ScriptableObject/GameConfig here)");
        ok &= CheckExists(hand, "HandManager");
        ok &= CheckExists(deck, "DeckManager");
        ok &= CheckExists(tower, "TowerBuilder");
        ok &= CheckExists(placement, "TowerPlacementInput (add it next to the TowerBuilder)");
        ok &= CheckExists(score, "ScoreManager");
        if (swap == null) Debug.LogWarning("GameFlowController: no SwapManager in the scene, so swapping is off.", this);
        return ok;
    }

    bool CheckExists(Object piece, string pieceName)
    {
        if (piece != null) return true;
        Debug.LogError($"GameFlowController: missing {pieceName}. The game cannot start.", this);
        return false;
    }

    // =====================================================================
    //  Commands (input calls these; a click in the wrong phase is ignored)
    // =====================================================================

    /// A hand card was clicked. The card is named by its ID, so the hand order does not matter.
    /// What the click means depends on the phase.
    public void RequestSelectCardById(int cardId)
    {
        if (hand.FindIndexOfCard(cardId) < 0) return;       // that card is no longer in the hand

        if (IsSwapping) PickCardToSwap(cardId);
        else if (IsCommitting) ToggleCardInCommit(cardId);
        else if (IsBuilding) HoldCommittedCard(cardId);
    }

    /// A hand card was dragged past a neighbour: it moves to that place in the hand.
    /// The order never changes the commit, the score or what has been placed.
    public void RequestMoveCard(int cardId, int newIndex)
    {
        if (!CanReorderHand) return;
        hand.MoveCard(cardId, newIndex);
    }

    /// A number key was pressed (numbered by hand position).
    public void RequestSelectCard(int handIndex)
    {
        RequestSelectCardById(hand.GetCardId(handIndex));
    }

    // Swap: pick as many cards as there are swaps left. Clicking a picked card again puts it down.
    void PickCardToSwap(int cardId)
    {
        if (swap == null) return;
        hand.ToggleSelected(cardId, swap.SwapsLeft);
    }

    // Commit: add the card to the selection or take it out, then refresh the hand name
    void ToggleCardInCommit(int cardId)
    {
        if (hand.ToggleSelected(cardId, config.maxCommitCards)) Commit.PreviewSelectedHand();
    }

    // Build: pick up one of the committed cards that is still waiting to be placed
    void HoldCommittedCard(int cardId)
    {
        placement.HoldCard(Commit.FindUnplacedCard(cardId));
    }

    /// The deck (swap zone) was clicked: swap every picked card for a new one, in one go.
    public void RequestSwapSelectedCard()
    {
        if (!IsReady || swap == null) return;

        if (!CanSwapSelectedCard)
        {
            GameEvents.RaiseSwapRejected(GetSwapRejectReason());
            return;
        }

        int cardCount = hand.GetSelectedCardIds().Count;
        if (swap.TrySwapSelected()) GameEvents.RaiseCardsSwapped(cardCount);
    }

    SwapRejectReason GetSwapRejectReason()
    {
        if (!IsSwapping) return SwapRejectReason.NotSwapPhase;
        if (swap.SwapsLeft <= 0) return SwapRejectReason.NoSwapsLeft;
        int picked = hand.GetSelectedCardIds().Count;
        if (picked == 0) return SwapRejectReason.NothingPicked;
        return SwapRejectReason.TooManyPicked;
    }

    /// The Done Swapping button.
    public void RequestDoneSwapping()
    {
        if (!IsSwapping) return;
        doneSwappingRequested = true;
    }

    /// The Play Hand button. A refused commit spends nothing: the player just reselects.
    public void RequestPlayHand()
    {
        if (!IsCommitting) return;                          // wrong phase or a double-click: ignore
        if (blind.HandsLeft <= 0) return;                   // no hand to spend (should not happen)
        if (!Commit.TryCommitSelectedCards()) return;       // worker says the commit is not legal
        blind.UseOneHand();                                 // the hand is spent here, exactly once
        playHandAccepted = true;                            // CommitPhase is waiting for this
    }

    /// The table or a snap point was clicked while building.
    public void RequestPlaceHeldCard()
    {
        if (!IsBuilding) return;

        // An empty table needs no card picked first: the click places the next committed card
        if (!placement.IsHoldingCard) placement.HoldCard(Commit.GetFirstUnplacedCard());

        CardInstance placed = placement.PlaceHeldCard();
        if (placed == null) return;

        Commit.MarkPlaced(placed);
        hand.RemoveCardById(placed.Id);
    }

    /// A dragged card was let go somewhere it cannot be placed: it goes back to the hand, still waiting.
    public void RequestDropHeldCard()
    {
        if (!IsBuilding) return;
        placement.DropHeldCard();
    }

    /// Undo (button or right-click): this hand's newest placement goes back to the hand.
    public void RequestUndo()
    {
        if (!IsBuilding) return;

        placement.DropHeldCard();
        CardInstance card = tower.UndoLastPlacement();
        if (card == null) return;

        Commit.MarkUnplaced(card);
        hand.ReturnCardToHand(card);
    }

    /// The Bank button.
    public void RequestBank()
    {
        if (!IsChoosingBankOrRide) return;                  // wrong phase or a double-click: ignore
        bankOrRideChoice = BankOrRideChoice.Bank;
    }

    /// The Ride button.
    public void RequestRide()
    {
        if (!CanRide) return;                               // wrong phase, double-click, or a full tower
        bankOrRideChoice = BankOrRideChoice.Ride;
    }

    /// The Continue button: keep playing this blind. No bonus.
    public void RequestContinue()
    {
        if (!IsChoosingContinueOrLeave) return;             // wrong phase or a double-click: ignore
        continueOrLeaveChoice = ContinueOrLeaveChoice.Continue;
    }

    /// The Leave Table button: take the unused-hand bonus and win the blind now.
    public void RequestLeaveTable()
    {
        if (!IsChoosingContinueOrLeave) return;             // wrong phase or a double-click: ignore
        continueOrLeaveChoice = ContinueOrLeaveChoice.Leave;
    }

    /// The Select button of the blind menu: play the current blind.
    public void RequestSelectBlind()
    {
        if (!IsChoosingBlind) return;                       // wrong phase or a double-click: ignore
        blindMenuChoice = BlindMenuChoice.Select;
    }

    /// The Skip button of the blind menu. A Boss can never be skipped.
    public void RequestSkipBlind()
    {
        if (!CanSkipBlind) return;                          // wrong phase, a Boss, or a double-click
        blindMenuChoice = BlindMenuChoice.Skip;
    }

    /// Debug key F2: put extra chips in the Pot.
    public void RequestCheatAddPotChips(long chips)
    {
        long oldChips = pot.PotChips;
        pot.AddHandScore(chips);
        AnnouncePotChanged(oldChips, PotChangeReason.Cheat);
    }

    /// Debug key F3: add Safe Points up to the target (a quick way to test a win).
    public void RequestCheatSafePointsToTarget()
    {
        blind.AddSafePoints(blind.Target - blind.BlindScore);
    }

    /// Debug key F4: crack the Glass once. Never the last life: only a failed blind may shatter it.
    public void RequestCheatCrackGlass()
    {
        if (run.Glass.LivesRemaining <= 1)
        {
            Debug.Log("[Cheat] F4: one life left, fail a blind to shatter the Glass");
            return;
        }

        int oldLives = run.Glass.LivesRemaining;
        run.Glass.Crack();
        GameEvents.RaiseGlassChanged(oldLives, run.Glass.LivesRemaining, GlassChangeReason.Cheat);
    }

    /// Debug key F5: the next blind will be the Boss.
    public void RequestCheatNextBlindBoss()
    {
        run.MakeNextBlindBoss();
    }

    /// Debug key F9: a brand new run.
    public void RequestRestartRun()
    {
        if (!HasEverythingItNeeds()) return;
        StartNewRun();
    }

    // =====================================================================
    //  The flow (reads top to bottom; every jump is visible here)
    // =====================================================================

    public void StartNewRun()
    {
        StopFlow();
        run = CreateRunState();
        flow = StartCoroutine(PlayRun());                   // the blind menu clears the table at once
    }

    void StopFlow()
    {
        if (flow != null) StopCoroutine(flow);
        flow = null;
        PresentationGate.Reset();
    }

    void ClearTheTable()
    {
        HandNumber = 0;
        Commit.Clear();
        ClearThePot();
        placement.DisableInput();
        tower.SweepTower();
        hand.EmptyHand();
        deck.ResetDeck();
        score.ResetScore();
    }

    /// The blind menu, then the chosen blind, until the run is Cleared or Shattered.
    IEnumerator PlayRun()
    {
        Debug.Log($"[Flow] run started: {run.AntesPerRun} Antes, {run.Glass.LivesRemaining} lives");
        while (!run.IsOver)
        {
            yield return BlindMenuPhase();
            if (blindMenuChoice == BlindMenuChoice.Skip) { SkipTheBlind(); continue; }   // jump: back to the menu

            yield return PlayBlind();
            yield return ShowBlindResult();
        }

        FinishRun();
        flow = null;
    }

    /// Ends when the player presses Select or Skip. The table is cleared first, so the menu sits on an empty table.
    IEnumerator BlindMenuPhase()
    {
        ClearTheTable();                                    // empty Pot, tower and hand; all 52 cards back
        SetPhase(GamePhase.BlindMenu);
        blindMenuChoice = BlindMenuChoice.None;
        GameEvents.RaiseBlindOffered(run.CurrentBlind, run.AnteNumber, GetBlindTarget(run.CurrentBlind));

        while (blindMenuChoice == BlindMenuChoice.None)
            yield return null;
    }

    /// Skip: no score, no reward, one crack repaired (never above the maximum), then the next blind.
    void SkipTheBlind()
    {
        BlindKind kind = run.CurrentBlind;
        int ante = run.AnteNumber;
        int oldLives = run.Glass.LivesRemaining;
        if (!run.SkipBlind()) return;                       // RunState says no (a Boss): nothing changes

        Debug.Log($"[Flow] Ante {ante} {kind} blind skipped, glass {oldLives} -> {run.Glass.LivesRemaining}");
        GameEvents.RaiseBlindSkipped(kind, ante);
        if (run.Glass.LivesRemaining != oldLives)
            GameEvents.RaiseGlassChanged(oldLives, run.Glass.LivesRemaining, GlassChangeReason.Skipped);
    }

    /// The table is already clear (the blind menu did it).
    IEnumerator PlayBlind()
    {
        StartBlind();
        while (blind.HandsLeft > 0 && !blind.HasLeftTable)
        {
            yield return PlayOneHand();
            if (NothingLeftToPlay()) break;                 // jump: deck and hand are both empty
        }

        yield return WaitForScoreReveal();
        FinishBlind();
    }

    /// Safe Points back to 0, all hands back, the target worked out for this blind and Ante.
    /// Run Total, reward and Glass carry over. A final-Boss retry gets the same target.
    void StartBlind()
    {
        BlindKind kind = run.CurrentBlind;
        run.StartBlind();
        blind.StartBlind(config.GetBaseTarget(kind), run.AnteNumber);

        string retry = run.IsRetry ? " (retry)" : "";
        Debug.Log($"[Flow] Ante {run.AnteNumber} {kind} blind{retry} started: target {blind.Target}, {blind.HandsLeft} hands");
        GameEvents.RaiseBlindStarted(kind, run.AnteNumber, blind.Target);
    }

    /// Judged on Safe Points only. Win: reward once (and a repair on a Boss). Fail: one crack.
    /// RunState then moves to the next blind, keeps the final Boss for a retry, or ends the run.
    void FinishBlind()
    {
        LastBlindKind = run.CurrentBlind;
        LastBlindResult = blind.GetResult();
        int oldLives = run.Glass.LivesRemaining;

        if (LastBlindResult == BlindResult.Won) run.RecordBlindWon(config.GetRewardPercent(LastBlindKind));
        else run.RecordBlindFailed();

        Debug.Log($"[Flow] {LastBlindKind} blind {LastBlindResult}: {blind.BlindScore} / {blind.Target}, " +
                  $"run total {run.RunTotal}, reward x{run.RewardMultiplier:0.###}, glass {run.Glass.LivesRemaining}/{run.Glass.MaxLives}");
        SetPhase(GamePhase.BlindEnd);                       // after the result is recorded: listeners may read it

        if (LastBlindResult == BlindResult.Won) GameEvents.RaiseBlindWon(blind.BlindScore, blind.Target);
        else GameEvents.RaiseBlindFailed(blind.BlindScore, blind.Target);
        AnnounceGlassChange(oldLives);
    }

    void AnnounceGlassChange(int oldLives)
    {
        int newLives = run.Glass.LivesRemaining;
        if (newLives == oldLives) return;

        GlassChangeReason reason = newLives < oldLives ? GlassChangeReason.BlindFailed : GlassChangeReason.BossWon;
        GameEvents.RaiseGlassChanged(oldLives, newLives, reason);
    }

    /// The blind result stays on screen for a moment before the blind menu opens.
    IEnumerator ShowBlindResult()
    {
        yield return new WaitForSeconds(config.blindEndPauseSeconds);
    }

    /// Cleared or Shattered. The end panel shows the Run Total.
    void FinishRun()
    {
        SetPhase(GamePhase.RunEnd);
        bool cleared = run.Outcome == RunOutcome.Cleared;
        Debug.Log($"[Flow] run {run.Outcome}: run total {run.RunTotal}");
        GameEvents.RaiseRunEnded(run.RunTotal, cleared);
    }

    IEnumerator PlayOneHand()
    {
        HandNumber++;
        yield return DrawPhase();
        if (hand.Count == 0) yield break;                   // jump: nothing to play
        yield return SwapPhase();                           // until "Done Swapping" (or no swaps left)
        yield return CommitPhase();                         // until "Play Hand" is accepted
        yield return BuildPhase();                          // until every committed card is placed
        ScorePhase();
        yield return ShakePhase();                          // physics -> one ShakeResult -> Pot updated once

        if (LastShake.Collapsed) yield break;               // jump: tower fell, the Pot is already lost
        if (blind.HandsLeft == 0) { BankThePot(); yield break; }   // jump: the last hand banks by itself

        yield return BankOrRidePhase();                     // Ride is disabled if the tower is full
        if (bankOrRideChoice == BankOrRideChoice.Ride) yield break;

        BankThePot();
        if (blind.CanLeaveTable()) yield return ContinueOrLeavePhase();   // target met, hands left
    }

    IEnumerator DrawPhase()
    {
        SetPhase(GamePhase.Draw);
        score.ClearLastHand();
        Commit.Clear();
        if (swap != null) swap.ResetSwaps(config.swapsPerHand);
        hand.DrawCardsUntilHandHas(config.handSize);
        yield return new WaitForSeconds(config.drawPauseSeconds);
        yield return PresentationGate.WaitUntilClear(config.maxPresentationWait);   // a draw effect still flying
    }

    /// Ends when Done Swapping is pressed or the swaps run out. Swapping is never forced.
    IEnumerator SwapPhase()
    {
        SetPhase(GamePhase.Swap);
        doneSwappingRequested = false;
        hand.ClearSelection();

        while (!doneSwappingRequested && HasSwapsLeft())
            yield return null;

        hand.ClearSelection();
    }

    bool HasSwapsLeft()
    {
        return swap != null && swap.SwapsLeft > 0;
    }

    /// Ends when Play Hand is accepted. That locks the committed cards.
    IEnumerator CommitPhase()
    {
        SetPhase(GamePhase.Commit);
        playHandAccepted = false;
        Commit.PreviewSelectedHand();                       // nothing selected yet: "Select 1 to 5 cards"

        while (!playHandAccepted)
            yield return null;
    }

    /// Ends when every committed card is placed. Until then Undo can take this hand's cards back.
    IEnumerator BuildPhase()
    {
        SetPhase(GamePhase.Build);
        tower.StartHandPlacements();
        placement.EnableInput();

        while (Commit.HasUnplacedCards())
            yield return null;

        placement.DisableInput();
        hand.ClearSelection();
        SpendCommittedCards();
    }

    // The committed cards are used up: they go to the discard pile
    void SpendCommittedCards()
    {
        foreach (CardInstance card in Commit.CommittedCards)
            deck.Discard(card);
    }

    /// The committed hand is scored once and goes into the Pot. From here the Pot is unsettled.
    void ScorePhase()
    {
        SetPhase(GamePhase.Score);
        ScoreContext result = score.ScoreHand(Commit.CommittedCards);
        if (result == null) return;

        long oldChips = pot.PotChips;
        pot.AddHandScore(result.finalScore);
        IsPotSettled = false;
        AnnouncePotChanged(oldChips, PotChangeReason.HandScored);
    }

    /// One shake, one ShakeResult, one Pot update. A collapse empties the Pot.
    IEnumerator ShakePhase()
    {
        SetPhase(GamePhase.Shake);
        yield return tower.ResolveShake(config);

        LastShake = tower.LastShakeResult;
        long oldChips = pot.PotChips;
        pot.ApplyShakeResult(LastShake);
        IsPotSettled = true;

        Debug.Log($"[Flow] shake {LastShake.ShakeId}: {LastShake.BrokenCount} broken, {LastShake.StandingLevels} levels, " +
                  $"collapsed {LastShake.Collapsed}, pot {oldChips} -> {pot.PotChips}");
        GameEvents.RaiseShakeResolved(LastShake);
        if (LastShake.Collapsed) GameEvents.RaiseTowerCollapsed(LastShake);
        AnnouncePotChanged(oldChips, LastShake.Collapsed ? PotChangeReason.Collapsed : PotChangeReason.ShakeResolved);
    }

    /// Ends when the player presses Bank or Ride.
    IEnumerator BankOrRidePhase()
    {
        SetPhase(GamePhase.BankOrRide);
        bankOrRideChoice = BankOrRideChoice.None;

        while (bankOrRideChoice == BankOrRideChoice.None)
            yield return null;
    }

    /// Bank: the Pot (plus the height bonus) becomes Safe Points, then the whole tower is cleared.
    /// The payout is final before the sweep, so sweeping can never change it.
    void BankThePot()
    {
        long oldChips = pot.PotChips;
        long payout = pot.BankPot(StandingLevels, RewardMultiplier);
        blind.AddSafePoints(payout);
        run.AddToRunTotal(payout);

        tower.SweepTower();
        LastShake = null;                                   // the tower it described is gone

        Debug.Log($"[Flow] banked {payout} (pot was {oldChips}), safe points {blind.BlindScore}");
        AnnouncePotChanged(oldChips, PotChangeReason.Banked);
        GameEvents.RaisePotBanked(payout, blind.BlindScore, run.RunTotal);
    }

    /// Ends when the player presses Continue or Leave Table. Leave pays the bonus once;
    /// PlayBlind then sees HasLeftTable and ends the blind as a win.
    IEnumerator ContinueOrLeavePhase()
    {
        SetPhase(GamePhase.ContinueOrLeave);
        continueOrLeaveChoice = ContinueOrLeaveChoice.None;

        while (continueOrLeaveChoice == ContinueOrLeaveChoice.None)
            yield return null;

        if (continueOrLeaveChoice == ContinueOrLeaveChoice.Leave) LeaveTheTable();
    }

    void LeaveTheTable()
    {
        long bonus = blind.LeaveTable();
        run.AddToRunTotal(bonus);
        Debug.Log($"[Flow] left the table: bonus {bonus}, safe points {blind.BlindScore}");
        GameEvents.RaiseTableLeft(bonus, blind.BlindScore);
    }

    void ClearThePot()
    {
        long oldChips = pot.PotChips;
        pot.Clear();
        LastShake = null;
        IsPotSettled = true;
        AnnouncePotChanged(oldChips, PotChangeReason.Cleared);
    }

    void AnnouncePotChanged(long oldChips, PotChangeReason reason)
    {
        GameEvents.RaisePotChanged(oldChips, pot.PotChips, reason, BankQuote);
    }

    /// Lets the last score finish counting before the game-over screen. Never waits forever (rule 13).
    IEnumerator WaitForScoreReveal()
    {
        float deadline = Time.time + config.maxPresentationWait;
        while (score.IsScoring && Time.time < deadline) yield return null;
    }

    bool NothingLeftToPlay()
    {
        return hand.Count == 0 && deck.TotalCards == 0;
    }

    /// Every phase starts here (rule 14). If the flow stops, the last logged phase shows where.
    void SetPhase(GamePhase newPhase)
    {
        Phase = newPhase;
        phaseStartTime = Time.time;
        Debug.Log($"[Flow] hand {HandNumber}: {newPhase}");
        GameEvents.RaisePhaseChanged(newPhase);
    }
}
