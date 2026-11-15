using System.Collections;
using UnityEngine;

/// The conductor: the ONLY script that decides what happens next (Master Plan rule 4).
///
///   Input sends commands here (RequestXxx). The conductor checks the phase,
///   asks a worker to do the job, and moves to the next phase.
///
/// One hand is: Draw, Swap, Commit, Build, Score, Shake (A2).
/// The run is still the old fixed number of hands. Bank / Ride arrive in A3, blinds and Glass in A4.
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
    public int HandsPerRun => config != null ? config.oldLoopTurnsPerRun : 0;
    public int CardsPlacedThisHand => tower != null ? tower.CardsPlacedThisHand : 0;
    public bool IsRunOver => Phase == GamePhase.RunEnd;

    /// The commit worker: the live hand name, why Play Hand is refused, the committed cards.
    public CommitManager Commit { get; private set; }

    // Which commands count right now (the HUD uses these to grey out buttons)
    public bool IsSwapping => Phase == GamePhase.Swap && !doneSwappingRequested;
    public bool IsCommitting => Phase == GamePhase.Commit && !playHandAccepted;
    public bool IsBuilding => Phase == GamePhase.Build;
    public bool CanClickHandCards => IsSwapping || IsCommitting || IsBuilding;
    public bool CanSwapSelectedCard => IsSwapping && swap != null && swap.CanDiscard;
    public bool CanPlayHand => IsCommitting && Commit.Problem == "";
    public bool CanUndo => IsBuilding && CardsPlacedThisHand > 0;

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
        if (!Commit.TryCommitSelectedCards()) return;       // worker says the commit is not legal
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

    /// Debug key F9 (until blinds exist, "the blind" is the whole run).
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
        tower.DisablePlacement();
        tower.SweepTower();
        hand.EmptyHand();
        deck.ResetDeck();
        score.ResetScore();
    }

    IEnumerator PlayRun()
    {
        while (HandNumber < HandsPerRun)
        {
            yield return PlayOneHand();
            if (NothingLeftToPlay()) break;                 // jump: deck and hand are both empty
        }

        yield return WaitForScoreReveal();
        SetPhase(GamePhase.RunEnd);
        flow = null;
    }

    IEnumerator PlayOneHand()
    {
        HandNumber++;
        SweepTowerIfFull();
        yield return DrawPhase();
        if (hand.Count == 0) yield break;                   // jump: nothing to play
        yield return SwapPhase();                           // until "Done Swapping" (or no swaps left)
        yield return CommitPhase();                         // until "Play Hand" is accepted
        yield return BuildPhase();                          // until every committed card is placed
        ScorePhase();
        yield return ShakePhase();
    }

    /// Temporary, until Bank exists (A3): a full tower would leave nothing to commit, so clear it.
    void SweepTowerIfFull()
    {
        if (tower.HasLegalPlacement()) return;
        Debug.Log("[Flow] the tower is full: sweeping it (Bank replaces this in A3)");
        tower.SweepTower();
    }

    IEnumerator DrawPhase()
    {
        SetPhase(GamePhase.Draw);
        score.ClearLastHand();
        Commit.Clear();
        if (swap != null) swap.ResetSwaps(config.swapsPerHand);
        hand.DrawCardsUntilHandHas(config.handSize);
        yield return new WaitForSeconds(config.oldLoopDrawSeconds);
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

    void ScorePhase()
    {
        SetPhase(GamePhase.Score);
        score.ScoreHand(Commit.CommittedCards);
    }

    IEnumerator ShakePhase()
    {
        SetPhase(GamePhase.Shake);
        tower.ReleasePhysics();
        yield return new WaitForSeconds(config.oldLoopShakeSeconds);
        tower.FreezeTower();
        tower.BreakFallenCards();                           // resolved now, never at the next draw
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
