using System.Collections;
using UnityEngine;

/// The conductor: the ONLY script that decides what happens next (Master Plan rule 4).
///
///   Input sends commands here (RequestXxx). The conductor checks the phase,
///   asks a worker to do the job, and moves to the next phase.
///
/// One hand is: Draw, Swap, Commit, Build, Score, Shake (A2).
/// Each hand's score goes into the Pot; the shake cuts it or empties it.
/// If the tower survives, the player Banks (Pot becomes Safe Points, tower cleared) or Rides.
/// A3c: the game is one blind with a fixed target and a few hands. The last hand banks by itself.
/// A4a: a Bank that reaches the target with hands left offers Continue / Leave Table.
/// The blind menu, Antes and Glass arrive in A4b / A4c.
public class GameFlowController : MonoBehaviour
{
    [Header("Settings")]
    [RequiredField] public GameConfig config;

    [Header("Workers (auto-found if empty)")]
    public HandManager hand;
    public DeckManager deck;
    public DiscardManager swap;
    public TowerBuilder tower;
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

    /// The commit worker: the live hand name, why Play Hand is refused, the committed cards.
    public CommitManager Commit { get; private set; }

    // The Pot (unsafe points). The HUD shows these numbers; it never redoes the maths.
    public long PotChips => pot.PotChips;
    public ShakeResult LastShake { get; private set; }
    public int StandingLevels => LastShake != null ? LastShake.StandingLevels : 0;
    public long BankQuote => pot.QuoteBankPayout(StandingLevels, RewardMultiplier);

    /// False from the moment a hand is scored until its shake is resolved: the Pot can still change.
    public bool IsPotSettled { get; private set; } = true;

    const decimal RewardMultiplier = 1m;        // blind rewards arrive in A4

    /// Points that are already safe. A collapse never touches them.
    public long SafePoints => blind.BlindScore;

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

    enum ContinueOrLeaveChoice { None, Continue, Leave }
    ContinueOrLeaveChoice continueOrLeaveChoice;

    // Which commands count right now (the HUD uses these to grey out buttons)
    public bool IsSwapping => Phase == GamePhase.Swap && !doneSwappingRequested;
    public bool IsCommitting => Phase == GamePhase.Commit && !playHandAccepted;
    public bool IsBuilding => Phase == GamePhase.Build;
    public bool CanClickHandCards => IsSwapping || IsCommitting || IsBuilding;
    public bool CanSwapSelectedCard => IsSwapping && swap != null && swap.CanDiscard;
    public bool CanPlayHand => IsCommitting && Commit.Problem == "";
    public bool CanUndo => IsBuilding && CardsPlacedThisHand > 0;

    PotManager pot;
    BlindManager blind;
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
        if (swap == null) swap = FindAnyObjectByType<DiscardManager>();
        if (tower == null) tower = FindAnyObjectByType<TowerBuilder>();
        if (score == null) score = FindAnyObjectByType<ScoreManager>();
    }

    // Workers that are plain C# (not components) are made here
    void CreatePlainWorkers()
    {
        int maxCommitCards = config != null ? config.maxCommitCards : 5;
        Commit = new CommitManager(hand, tower, maxCommitCards);

        int breakPercent = config != null ? config.breakPercent : 10;
        int heightPercent = config != null ? config.heightPercent : 10;
        pot = new PotManager(breakPercent, heightPercent);
        int handsPerBlind = config != null ? config.handsPerBlind : 4;
        int targetGrowthPercent = config != null ? config.targetGrowthPercent : 150;
        int leaveBonusPercent = config != null ? config.leaveBonusPercent : 10;
        blind = new BlindManager(handsPerBlind, targetGrowthPercent, leaveBonusPercent);
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
        ok &= CheckExists(score, "ScoreManager");
        if (swap == null) Debug.LogWarning("GameFlowController: no DiscardManager in the scene, so swapping is off.", this);
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

    /// A HUD card button or a number key was pressed (these are numbered by hand position).
    public void RequestSelectCard(int handIndex)
    {
        RequestSelectCardById(hand.GetCardId(handIndex));
    }

    // Swap: pick as many cards as there are swaps left. Clicking a picked card again puts it down.
    void PickCardToSwap(int cardId)
    {
        if (swap == null) return;
        hand.ToggleSelected(cardId, swap.DiscardsLeft);
    }

    // Commit: add the card to the selection or take it out, then refresh the hand name
    void ToggleCardInCommit(int cardId)
    {
        if (hand.ToggleSelected(cardId, config.maxCommitCards)) Commit.PreviewSelectedHand();
    }

    // Build: pick up one of the committed cards that is still waiting to be placed
    void HoldCommittedCard(int cardId)
    {
        tower.HoldCard(Commit.FindUnplacedCard(cardId));
    }

    /// The deck (discard zone) was clicked: swap every picked card for a new one, in one go.
    public void RequestSwapSelectedCard()
    {
        if (!CanSwapSelectedCard) return;
        swap.TryDiscardSelected();
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
        if (!tower.IsHoldingCard) tower.HoldCard(Commit.GetFirstUnplacedCard());

        CardInstance placed = tower.PlaceHeldCard();
        if (placed == null) return;

        Commit.MarkPlaced(placed);
        hand.RemoveCardById(placed.Id);
    }

    /// Undo (button or right-click): this hand's newest placement goes back to the hand.
    public void RequestUndo()
    {
        if (!IsBuilding) return;

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

    /// Debug key F9 (until A4, "the blind" is the whole run).
    public void RequestRestartBlind()
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
        ClearTheTable();
        flow = StartCoroutine(PlayRun());
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
        tower.DisablePlacement();
        tower.SweepTower();
        hand.EmptyHand();
        deck.ResetDeck();
        score.ResetScore();
    }

    /// A3c: the run is one blind. A4 puts the blind menu and the Antes around it.
    IEnumerator PlayRun()
    {
        yield return PlayBlind();
        flow = null;
    }

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

    /// Safe Points back to 0, all hands back, the target worked out. The table is already clear.
    void StartBlind()
    {
        blind.StartBlind(config.FirstBlindBaseTarget, 1);
        Debug.Log($"[Flow] blind started: target {blind.Target}, {blind.HandsLeft} hands");
        GameEvents.RaiseBlindStarted(blind.Target);
    }

    /// Judged on Safe Points only. Win or fail, the blind ends here.
    void FinishBlind()
    {
        LastBlindResult = blind.GetResult();                // set first: PhaseChanged listeners may read it
        SetPhase(GamePhase.BlindEnd);

        Debug.Log($"[Flow] blind {LastBlindResult}: {blind.BlindScore} / {blind.Target}");
        if (LastBlindResult == BlindResult.Won) GameEvents.RaiseBlindWon(blind.BlindScore, blind.Target);
        else GameEvents.RaiseBlindFailed(blind.BlindScore, blind.Target);
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
        return swap != null && swap.DiscardsLeft > 0;
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
        tower.EnablePlacement();

        while (Commit.HasUnplacedCards())
            yield return null;

        tower.DisablePlacement();
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

        tower.SweepTower();
        LastShake = null;                                   // the tower it described is gone

        Debug.Log($"[Flow] banked {payout} (pot was {oldChips}), safe points {blind.BlindScore}");
        AnnouncePotChanged(oldChips, PotChangeReason.Banked);
        GameEvents.RaisePotBanked(payout, blind.BlindScore);
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
