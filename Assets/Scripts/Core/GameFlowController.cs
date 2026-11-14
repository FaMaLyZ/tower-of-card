using System.Collections;
using UnityEngine;

/// The conductor: the ONLY script that decides what happens next (Master Plan rule 4).
///
///   Input sends commands here (RequestXxx). The conductor checks the phase,
///   asks a worker to do the job, and moves to the next phase.
///
/// A1 keeps the old turn loop (draw, build, score, shake) under the new phase names.
/// Swap and Commit arrive in A2, Bank / Ride in A3, blinds and Glass in A4.
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
    public int CardsPerHand => config != null ? config.maxCommitCards : 0;
    public int CardsPlacedThisHand => tower != null ? tower.CardsPlacedThisHand : 0;
    public bool IsRunOver => Phase == GamePhase.RunEnd;

    /// True while a click on a hand card, a tower slot or End Turn is allowed.
    public bool IsBuilding => Phase == GamePhase.Build && !endHandRequested;

    public bool CanSwapSelectedCard => IsBuilding && swap != null && swap.CanDiscard;

    Coroutine flow;
    float phaseStartTime;
    bool endHandRequested;

    // =====================================================================
    //  Lifecycle
    // =====================================================================

    void Awake()
    {
        FindMissingWorkers();
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
    public void RequestSelectCardById(int cardId)
    {
        if (!IsBuilding) return;

        int handIndex = hand.FindIndexOfCard(cardId);
        if (handIndex < 0) return;              // that card is no longer in the hand
        tower.SelectCard(handIndex);
    }

    /// A HUD card button was pressed (these are numbered by hand position).
    public void RequestSelectCard(int handIndex)
    {
        if (!IsBuilding) return;
        tower.SelectCard(handIndex);
    }

    /// The discard zone was clicked.
    public void RequestSwapSelectedCard()
    {
        if (!CanSwapSelectedCard) return;
        swap.TryDiscardSelected();
    }

    /// The End Turn button.
    public void RequestEndHand()
    {
        if (!IsBuilding) return;
        endHandRequested = true;
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
        yield return DrawPhase();
        if (hand.Count == 0) yield break;                   // jump: nothing to play
        yield return BuildPhase();
        ScorePhase();
        yield return ShakePhase();
    }

    IEnumerator DrawPhase()
    {
        SetPhase(GamePhase.Draw);
        score.ClearLastHand();
        if (swap != null) swap.ResetSwaps();
        hand.DrawCardsForNewHand();
        yield return new WaitForSeconds(config.oldLoopDrawSeconds);
    }

    /// Ends when every card for this hand is placed, the hand is empty, or End Turn is pressed.
    IEnumerator BuildPhase()
    {
        SetPhase(GamePhase.Build);
        endHandRequested = false;
        tower.EnablePlacement(CardsPerHand);

        while (!endHandRequested && tower.CanPlaceMoreCards && hand.Count > 0)
            yield return null;

        tower.DisablePlacement();
        hand.ClearSelection();
    }

    void ScorePhase()
    {
        SetPhase(GamePhase.Score);
        score.ScorePlayedCards();
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
