using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =====================================================================
//  Data classes
// =====================================================================

/// Base value of one hand type (chips x mult). Balatro's starting values are filled in automatically.
[Serializable]
public class HandTypeScore
{
    public PokerHand hand;
    public int chips;
    public int mult;

    public HandTypeScore() { }
    public HandTypeScore(PokerHand hand, int chips, int mult)
    {
        this.hand = hand;
        this.chips = chips;
        this.mult = mult;
    }
}

/// One moment of the scoring sequence ("Pair 10 x 2", "+11 Ace of Spades", "x1.5 Bonus"...).
public class ScoreStep
{
    public string label;
    public CardData cardData;       // set when the step is a scoring card
    public Card card;               // the card in the tower (may be null / destroyed)
    public float chipsAdded;
    public float multAdded;
    public float multFactor = 1f;
    public float chipsAfter;        // running totals after this step
    public float multAfter;
}

/// Everything about one scored turn. Other scripts can add effects to it (see ScoreManager.OnApplyEffects).
public class ScoreContext
{
    public PokerHandResult result;
    public List<CardData> cards;            // the cards that were played
    public List<Card> cardObjects;
    public float chips;
    public float mult = 1f;
    public long finalScore;
    public readonly List<ScoreStep> steps = new List<ScoreStep>();

    public void AddChips(float amount, string label, CardData data = null, Card card = null)
    {
        chips += amount;
        Record(label, amount, 0f, 1f, data, card);
    }

    public void AddMult(float amount, string label)
    {
        mult += amount;
        Record(label, 0f, amount, 1f, null, null);
    }

    public void MultiplyMult(float factor, string label)
    {
        mult *= factor;
        Record(label, 0f, 0f, factor, null, null);
    }

    public void Record(string label, float chipsAdded, float multAdded, float multFactor, CardData data, Card card)
    {
        steps.Add(new ScoreStep
        {
            label = label, cardData = data, card = card,
            chipsAdded = chipsAdded, multAdded = multAdded, multFactor = multFactor,
            chipsAfter = chips, multAfter = mult
        });
    }
}

// =====================================================================
//  ScoreManager
// =====================================================================

/// Balatro-style scoring, calculated at the END of every turn.
///
///   1. The cards you placed this turn are your "played hand".
///   2. The poker hand they make decides the base  chips x mult  (e.g. Pair = 10 x 2).
///   3. Every scoring card adds its chip value (2-10 = face value, J/Q/K = 10, Ace = 11).
///   4. Other scripts may add bonuses (+chips, +mult, x mult) - like Balatro jokers.
///   5. Turn score = chips x mult, added to the total score.
///
/// The result is revealed step by step so the UI can animate it.
public class ScoreManager : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public TurnManager turnManager;
    public TowerBuilder tower;

    [Header("Hand values")]
    public List<HandTypeScore> handTypes = DefaultTable();

    [Header("Rules")]
    [Tooltip("A poker hand has at most this many cards. If a turn places more, the best group of this size scores.")]
    public int maxScoringCards = 5;

    [Header("Reveal animation")]
    [Tooltip("Seconds between steps. 0 = instant. Keep (steps x delay) shorter than TurnManager's End Turn Delay.")]
    public float stepDelay = 0.35f;

    // ---------- Events (subscribe from other scripts / UI) ----------
    public event Action<ScoreContext> OnApplyEffects;       // add jokers / bonuses here
    public event Action<ScoreStep> OnScoreStep;             // every revealed step
    public event Action<ScoreContext> OnTurnScored;         // the whole turn has been scored
    public event Action<long> OnTotalScoreChanged;

    // ---------- Read-only info (UI reads these) ----------
    public long TotalScore { get; private set; }
    public long TurnScore { get; private set; }
    public string HandName { get; private set; } = "";
    public float CurrentChips { get; private set; }
    public float CurrentMult { get; private set; }
    public bool IsScoring { get; private set; }
    public bool HasResult { get; private set; }             // a hand is being / has been shown this turn
    public ScoreContext LastContext { get; private set; }

    // cards placed during the current turn (in placing order)
    readonly List<CardData> playedData = new List<CardData>();
    readonly List<Card> playedCards = new List<Card>();

    Coroutine sequence;

    // =====================================================================
    //  Lifecycle
    // =====================================================================

    void Awake()
    {
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (tower == null) tower = FindFirstObjectByType<TowerBuilder>();

        if (turnManager != null)
        {
            turnManager.OnStartTurn += HandleStartTurn;
            turnManager.OnEndTurn += HandleEndTurn;
        }
        if (tower != null) tower.CardPlaced += HandleCardPlaced;
    }

    void OnDestroy()
    {
        if (turnManager != null)
        {
            turnManager.OnStartTurn -= HandleStartTurn;
            turnManager.OnEndTurn -= HandleEndTurn;
        }
        if (tower != null) tower.CardPlaced -= HandleCardPlaced;
    }

    // =====================================================================
    //  Turn events
    // =====================================================================

    // Remember every card placed this turn
    void HandleCardPlaced(CardData data, Card card)
    {
        playedData.Add(data);
        playedCards.Add(card);
    }

    // New turn: finish any running reveal, then start with a clean slate
    void HandleStartTurn(int turn)
    {
        if (IsScoring) FinishNow();

        playedData.Clear();
        playedCards.Clear();
        ClearDisplay();
    }

    // End of turn: score the played hand
    void HandleEndTurn(int turn)
    {
        if (playedData.Count == 0) return;

        ScoreContext ctx = BuildScore(new List<CardData>(playedData), new List<Card>(playedCards));
        if (ctx == null) return;

        LastContext = ctx;
        sequence = StartCoroutine(RevealSequence(ctx));
    }

    // =====================================================================
    //  Score calculation
    // =====================================================================

    ScoreContext BuildScore(List<CardData> data, List<Card> objects)
    {
        // too many cards for one poker hand: keep the best group
        if (data.Count > maxScoringCards)
        {
            List<int> keep = ChooseBestCards(data);
            data = keep.Select(i => data[i]).ToList();
            objects = keep.Select(i => objects[i]).ToList();
        }

        PokerHandResult result = PokerHandEvaluator.Evaluate(data);
        if (result == null) return null;

        HandTypeScore entry = GetEntry(result.hand);

        var ctx = new ScoreContext
        {
            result = result,
            cards = data,
            cardObjects = objects,
            chips = entry.chips,
            mult = entry.mult
        };

        // step 1: the hand type itself ("Pair  10 x 2")
        ctx.Record(result.displayName, 0f, 0f, 1f, null, null);

        // step 2: each scoring card, left to right
        foreach (int i in result.scoringIndices)
        {
            int chipValue = PokerHandEvaluator.ChipValue(data[i].rank);
            ctx.AddChips(chipValue, data[i].cardName, data[i], objects[i]);
        }

        // step 3: bonuses from other scripts (jokers, tower height, ...)
        OnApplyEffects?.Invoke(ctx);

        ctx.finalScore = (long)Math.Floor((double)ctx.chips * ctx.mult);
        return ctx;
    }

    // Best group of "maxScoringCards" cards (only used when a turn places more cards than that)
    List<int> ChooseBestCards(List<CardData> data)
    {
        List<int> best = null;
        double bestScore = -1;
        var current = new List<int>();

        void Recurse(int start)
        {
            if (current.Count == maxScoringCards)
            {
                double s = EstimateScore(current.Select(i => data[i]).ToList());
                if (s > bestScore) { bestScore = s; best = new List<int>(current); }
                return;
            }
            for (int i = start; i < data.Count; i++)
            {
                current.Add(i);
                Recurse(i + 1);
                current.RemoveAt(current.Count - 1);
            }
        }

        Recurse(0);
        return best ?? Enumerable.Range(0, Mathf.Min(maxScoringCards, data.Count)).ToList();
    }

    double EstimateScore(List<CardData> cards)
    {
        PokerHandResult r = PokerHandEvaluator.Evaluate(cards);
        HandTypeScore e = GetEntry(r.hand);
        double chips = e.chips;
        foreach (int i in r.scoringIndices) chips += PokerHandEvaluator.ChipValue(cards[i].rank);
        return chips * e.mult;
    }

    // =====================================================================
    //  Reveal animation
    // =====================================================================

    IEnumerator RevealSequence(ScoreContext ctx)
    {
        IsScoring = true;
        HasResult = true;
        HandName = ctx.result.displayName;
        CurrentChips = 0f;
        CurrentMult = 0f;
        TurnScore = 0;

        foreach (ScoreStep step in ctx.steps)
        {
            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
            ScoreCulSound();
            CurrentChips = step.chipsAfter;
            CurrentMult = step.multAfter;
            OnScoreStep?.Invoke(step);
        }

        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
        CompleteScoreCulSound();
        CompleteScoring(ctx);
    }

    // If the next turn starts before the reveal ends, show the result immediately
    void FinishNow()
    {
        if (sequence != null) StopCoroutine(sequence);
        if (LastContext != null) CompleteScoring(LastContext);
    }

    void CompleteScoring(ScoreContext ctx)
    {
        sequence = null;
        IsScoring = false;

        CurrentChips = ctx.chips;
        CurrentMult = ctx.mult;
        TurnScore = ctx.finalScore;
        TotalScore += ctx.finalScore;

        OnTurnScored?.Invoke(ctx);
        OnTotalScoreChanged?.Invoke(TotalScore);
    }

    void ClearDisplay()
    {
        HasResult = false;
        HandName = "";
        CurrentChips = 0f;
        CurrentMult = 0f;
        TurnScore = 0;
    }

    // =====================================================================
    //  Public API
    // =====================================================================

    public HandTypeScore GetEntry(PokerHand hand)
    {
        foreach (var e in handTypes)
            if (e.hand == hand) return e;

        // not in the list (e.g. deleted): create it from the defaults
        var fallback = DefaultTable().Find(e => e.hand == hand);
        handTypes.Add(fallback);
        return fallback;
    }

    public void ResetScore()
    {
        TotalScore = 0;
        ClearDisplay();
        OnTotalScoreChanged?.Invoke(TotalScore);
    }

    [ContextMenu("Reset hand values to defaults")]
    void ResetTable()
    {
        handTypes = DefaultTable();
    }

    // Balatro's starting values:  chips x mult
    static List<HandTypeScore> DefaultTable()
    {
        return new List<HandTypeScore>
        {
            new HandTypeScore(PokerHand.HighCard,      5,   1),
            new HandTypeScore(PokerHand.Pair,          10,  2),
            new HandTypeScore(PokerHand.TwoPair,       20,  2),
            new HandTypeScore(PokerHand.ThreeOfAKind,  30,  3),
            new HandTypeScore(PokerHand.Straight,      30,  4),
            new HandTypeScore(PokerHand.Flush,         35,  4),
            new HandTypeScore(PokerHand.FullHouse,     40,  4),
            new HandTypeScore(PokerHand.FourOfAKind,   60,  7),
            new HandTypeScore(PokerHand.StraightFlush, 100, 8),
        };
    }
    public void ScoreCulSound()
    {
        //เสียงตอนคำนวนคะแนน
    }
    public void CompleteScoreCulSound()
    {
        //เสียงตอนคำนวนคะแนนเสร็จแล้ว
    }
}