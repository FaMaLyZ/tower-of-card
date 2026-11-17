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
    public CardData cardData;
    public Card card;
    public float chipsAdded;
    public float multAdded;
    public float multFactor = 1f;
    public float chipsAfter;
    public float multAfter;
}

/// Everything about one scored turn. Other scripts can add effects to it (see ScoreManager.OnApplyEffects).
public class ScoreContext
{
    public PokerHandResult result;
    public List<CardData> cards;
    public List<Card> cardObjects;
    public float baseChips;                 // from the hand type alone (Pair = 10)
    public float cardChips;                 // added by the scoring cards (kickers add nothing)
    public float chips;
    public float mult = 1f;
    public long finalScore;
    public readonly List<ScoreStep> steps = new List<ScoreStep>();

    /// Played cards that are not part of the poker hand. Like real poker kickers, they add no chips.
    public int KickerCount => cards != null && result != null ? cards.Count - result.scoringIndices.Count : 0;

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

/// Balatro-style scoring. The conductor asks for it when building ends.
///
///   1. The cards you committed this hand are your "played hand".
///   2. The poker hand they make decides the base  chips x mult  (e.g. Pair = 10 x 2).
///   3. Every scoring card adds its chip value (2-10 = face value, J/Q/K = 10, Ace = 11).
///   4. Other scripts may add bonuses (+chips, +mult, x mult) - like Balatro jokers.
///   5. Turn score = chips x mult. The conductor puts it into the Pot; this script keeps no total.
///
/// The result is revealed step by step so the UI can animate it.
public class ScoreManager : MonoBehaviour
{
    [Header("References")]
    public TowerBuilder tower;

    [Header("Hand values")]
    public List<HandTypeScore> handTypes = DefaultTable();

    [Header("Rules")]
    public int maxScoringCards = 5;

    [Header("Reveal animation")]
    public float stepDelay = 0.35f;

    // ---------- Events (subscribe from other scripts / UI) ----------
    public event Action<ScoreContext> OnApplyEffects;
    public event Action<ScoreStep> OnScoreStep;
    public event Action<ScoreContext> OnTurnScored;

    // ---------- Read-only info (UI reads these) ----------
    public long TurnScore { get; private set; }
    public string HandName { get; private set; } = "";
    public string HandShapeName { get; private set; } = "";
    public float CurrentChips { get; private set; }         // base chips + card chips so far
    public float BaseChips { get; private set; }            // the hand type's chips (the plaque's first chip)
    public float CurrentCardChips { get; private set; }     // card chips so far (counts up during the reveal)
    public float CurrentMult { get; private set; }
    public bool IsScoring { get; private set; }
    public bool HasResult { get; private set; }             // a hand is being / has been shown this turn
    public ScoreContext LastContext { get; private set; }

    Coroutine sequence;

    // =====================================================================
    //  Lifecycle
    // =====================================================================

    void Awake()
    {
        if (tower == null) tower = FindAnyObjectByType<TowerBuilder>();
    }

    // =====================================================================
    //  Jobs the conductor asks for
    // =====================================================================

    /// New hand: finish any running reveal, then start with a clean slate.
    public void ClearLastHand()
    {
        if (IsScoring) FinishNow();
        ClearDisplay();
    }

    /// Building is over: score exactly the committed cards, once. Returns null if there is nothing to score.
    public ScoreContext ScoreHand(IReadOnlyList<CardInstance> committedCards)
    {
        if (committedCards == null || committedCards.Count == 0) return null;

        var data = new List<CardData>(committedCards.Count);
        var objects = new List<Card>(committedCards.Count);
        foreach (CardInstance card in committedCards)
        {
            data.Add(card.Data);
            objects.Add(tower != null ? tower.FindTowerCard(card.Id) : null);
        }

        ScoreContext ctx = BuildScore(data, objects, applyEffects: true);
        if (ctx == null) return null;

        LastContext = ctx;
        sequence = StartCoroutine(RevealSequence(ctx));
        return ctx;
    }

    /// What these cards would score if played now, for the live preview in the Commit phase.
    /// Pure: no reveal, no events, nothing stored. Bonus effects (OnApplyEffects) are not run,
    /// because they may need the cards on the tower. Returns null for no cards.
    public ScoreContext PreviewHand(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return null;

        var data = new List<CardData>(cards);
        var noObjects = new List<Card>(new Card[data.Count]);
        return BuildScore(data, noObjects, applyEffects: false);
    }

    // =====================================================================
    //  Score calculation
    // =====================================================================

    ScoreContext BuildScore(List<CardData> data, List<Card> objects, bool applyEffects)
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
            baseChips = entry.chips,
            chips = entry.chips,
            mult = entry.mult
        };

        // step 1: the hand type itself ("Pair  10 x 2")
        ctx.Record(result.displayName, 0f, 0f, 1f, null, null);

        // step 2: each scoring card, left to right
        foreach (int i in result.scoringIndices)
        {
            int chipValue = PokerHandEvaluator.ChipValue(data[i].rank);
            ctx.cardChips += chipValue;
            ctx.AddChips(chipValue, data[i].cardName, data[i], objects[i]);
        }

        // step 3: bonuses from other scripts (jokers, tower height, ...)
        if (applyEffects) OnApplyEffects?.Invoke(ctx);

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
        if (r == null) return 0;
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
        HandShapeName = ctx.result.shapeName;
        CurrentChips = 0f;
        BaseChips = ctx.baseChips;
        CurrentCardChips = 0f;
        CurrentMult = 0f;
        TurnScore = 0;

        foreach (ScoreStep step in ctx.steps)
        {
            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
            ScoreCulSound();
            CurrentChips = step.chipsAfter;
            CurrentCardChips = step.chipsAfter - ctx.baseChips;
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
        BaseChips = ctx.baseChips;
        CurrentCardChips = ctx.chips - ctx.baseChips;
        CurrentMult = ctx.mult;
        TurnScore = ctx.finalScore;

        OnTurnScored?.Invoke(ctx);
    }

    void ClearDisplay()
    {
        HasResult = false;
        HandName = "";
        HandShapeName = "";
        CurrentChips = 0f;
        BaseChips = 0f;
        CurrentCardChips = 0f;
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
        if (sequence != null) StopCoroutine(sequence);
        sequence = null;
        IsScoring = false;
        LastContext = null;
        ClearDisplay();
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

    }
    public void CompleteScoreCulSound()
    {
        //เสียงตอนคำนวนคะแนนเสร็จแล้ว
        AudioManager.SFX(SoundType.CalculateScore);
    }
}