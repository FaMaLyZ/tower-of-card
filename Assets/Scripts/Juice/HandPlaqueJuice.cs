using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Hand plaque effects (C1, and the plaque part of C2):
///   - Every text on the plaque punches when it changes (the hand name as you pick cards, the chips, the mult).
///   - While the hand is being scored, each chip box punches harder, wiggles and sheds a few fragments.
///   - Play hand: the hand name slams across the screen, then flies into the plaque.
///   - Scoring done: the score slams across the screen, then flies into the plaque's total.
/// The game waits for the scoring and the score slam before the shake (it holds PresentationGate).
/// Put it on the HandPlaque object. Listen-only: delete it and the plaque shows the same numbers,
/// and the shake starts straight away as before.
public class HandPlaqueJuice : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public ScoreManager scoreManager;
    public HudTheme theme;

    [Header("Plaque parts")]
    public TMP_Text buildingName;
    public TMP_Text pokerName;
    public TMP_Text baseChipsNumber;
    public TMP_Text cardChipsNumber;
    public TMP_Text multNumber;
    public TMP_Text totalText;
    [Tooltip("Each chip box's Visual wrapper: base chips, card chips, mult. Punched instead of the bare number")]
    public RectTransform baseChipsBox;
    public RectTransform cardChipsBox;
    public RectTransform multBox;

    [Header("Screen slams")]
    public ScreenSlam slam;
    public UiShardBurst burst;

    [Header("Punch when a text changes")]
    public float punchSize = 0.25f;
    public float punchSeconds = 0.3f;

    [Header("Punch while scoring")]
    public float scoringPunchSize = 0.5f;
    public float scoringTwistDegrees = 10f;
    public float scoringPunchSeconds = 0.35f;
    public int scoringShards = 8;
    public float scoringShardStrength = 0.7f;

    [Header("Landing punch (a slam arrives), drama 0 → drama 1")]
    public Vector2 landPunchSize = new Vector2(0.35f, 0.8f);

    [Header("Drama by poker hand")]
    [Tooltip("Left = High Card, right = Straight Flush. Up = a bigger, harder slam and shake. " +
             "The default stays calm up to a Straight, then jumps at Flush and climbs from there")]
    public AnimationCurve dramaByHand = MakeDefaultDramaCurve();
    [Tooltip("Drama for a Royal Flush (above every other hand)")]
    [Range(0f, 1f)] public float royalFlushDrama = 1f;

    /// One watched text: what it said last frame and what to punch when it changes
    class Watched
    {
        public TMP_Text text;
        public UiPunch punch;
        public string lastText;
        public bool isScoreBox;
        public bool hiddenForSlam;
    }

    readonly List<Watched> watched = new List<Watched>();
    bool holdingGate;
    bool started;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        if (slam == null) slam = FindAnyObjectByType<ScreenSlam>();
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();

        Watch(buildingName, null, false);
        Watch(pokerName, null, false);
        Watch(baseChipsNumber, baseChipsBox, true);
        Watch(cardChipsNumber, cardChipsBox, true);
        Watch(multNumber, multBox, true);
        Watch(totalText, null, false);
    }

    void Watch(TMP_Text text, RectTransform box, bool isScoreBox)
    {
        if (text == null) return;
        RectTransform punched = box != null ? box : text.rectTransform;
        watched.Add(new Watched { text = text, punch = new UiPunch(punched), isScoreBox = isScoreBox });
    }

    void OnEnable()
    {
        GameEvents.HandCommitted += OnHandCommitted;
        GameEvents.PhaseChanged += OnPhaseChanged;
        if (scoreManager != null) scoreManager.OnTurnScored += OnTurnScored;
    }

    void OnDisable()
    {
        GameEvents.HandCommitted -= OnHandCommitted;
        GameEvents.PhaseChanged -= OnPhaseChanged;
        if (scoreManager != null) scoreManager.OnTurnScored -= OnTurnScored;

        ReleaseGate();
        foreach (Watched w in watched)
        {
            w.punch.ReturnToRest();
            ShowText(w, true);
        }
    }

    // ---------- Slams ----------

    // Play hand: the plaque's title (building or poker name) slams in, then lands back in its place
    void OnHandCommitted(IReadOnlyList<int> cardIds, PokerHandResult hand)
    {
        if (slam == null || buildingName == null || string.IsNullOrEmpty(buildingName.text)) return;
        float drama = DramaOf(hand);
        SlamInto(buildingName.text, buildingName, theme != null ? theme.goldLight : Color.yellow, drama);
    }

    // The game waits for the scoring and its slam before the tower shakes
    void OnPhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Score && slam != null) HoldGate();
        else if (phase != GamePhase.Score) ReleaseGate();
    }

    void OnTurnScored(ScoreContext score)
    {
        if (!holdingGate || slam == null || totalText == null)
        {
            ReleaseGate();
            return;
        }
        float drama = DramaOf(score.result);
        SlamInto(string.Format("{0:N0}", score.finalScore), totalText, theme != null ? theme.mult : Color.red, drama, ReleaseGate);
    }

    // The real text hides while its big copy is on screen, then punches as the copy lands on it
    void SlamInto(string words, TMP_Text target, Color accent, float drama, System.Action afterLanding = null)
    {
        Watched w = Find(target);
        if (w != null) ShowText(w, false);

        slam.Play(words, target, accent, drama, () =>
        {
            if (w != null)
            {
                ShowText(w, true);
                w.punch.Play(Mathf.Lerp(landPunchSize.x, landPunchSize.y, drama), 0f, punchSeconds);
            }
            afterLanding?.Invoke();
        });
    }

    // ---------- Debug panel ----------

    /// Plays the Play-hand slam and then the score slam for any hand, without playing one (JuiceDebugPanel).
    /// Display only: no game state changes, and the game never waits for it.
    public void DebugSlamHand(PokerHand hand, bool royal, long score)
    {
        var result = new PokerHandResult { hand = hand, isRoyal = royal };
        DebugSlamAtDrama(royal ? "Palace" : PokerHandEvaluator.GetShapeName(hand), DramaOf(result), score);
    }

    /// The same two slams at any drama (0 = High Card feel, 1 = Royal Flush feel)
    public void DebugSlamAtDrama(string title, float drama, long score)
    {
        if (slam == null || buildingName == null || totalText == null) return;
        Color gold = theme != null ? theme.goldLight : Color.yellow;
        Color red = theme != null ? theme.mult : Color.red;
        SlamInto(title, buildingName, gold, drama, () => SlamInto(string.Format("{0:N0}", score), totalText, red, drama));
    }

    /// What drama a hand gets, for the debug panel's table
    public float DebugDramaOf(PokerHand hand, bool royal) => DramaOf(new PokerHandResult { hand = hand, isRoyal = royal });

    // High Card = 0 ... Straight Flush = 1, shaped by the curve; a Royal Flush has its own value on top
    float DramaOf(PokerHandResult hand)
    {
        if (hand == null) return 0f;
        if (hand.isRoyal) return royalFlushDrama;

        int strongest = System.Enum.GetValues(typeof(PokerHand)).Length - 1;
        float rank = strongest > 0 ? (float)(int)hand.hand / strongest : 0f;
        return Mathf.Clamp01(dramaByHand.Evaluate(rank));
    }

    // One point per hand, High Card to Straight Flush, joined by straight lines
    static AnimationCurve MakeDefaultDramaCurve()
    {
        float[] drama = { 0f, 0.05f, 0.1f, 0.18f, 0.28f, 0.6f, 0.72f, 0.85f, 0.95f };
        float step = 1f / (drama.Length - 1);
        var keys = new Keyframe[drama.Length];
        for (int i = 0; i < drama.Length; i++)
        {
            float slopeIn = i > 0 ? (drama[i] - drama[i - 1]) / step : 0f;
            float slopeOut = i < drama.Length - 1 ? (drama[i + 1] - drama[i]) / step : 0f;
            keys[i] = new Keyframe(i * step, drama[i], slopeIn, slopeOut);
        }
        return new AnimationCurve(keys);
    }

    // ---------- Punches ----------

    // After HandPlaqueView.Update has written this frame's texts
    void LateUpdate()
    {
        bool scoring = scoreManager != null && scoreManager.IsScoring;
        foreach (Watched w in watched)
        {
            if (started && w.text.text != w.lastText) OnTextChanged(w, scoring);
            w.lastText = w.text.text;
            if (w.hiddenForSlam) w.text.alpha = 0f;         // the view may rewrite it; keep it hidden
            w.punch.Apply();
        }
        started = true;     // the first frame only records the texts, so nothing punches on load
    }

    void OnTextChanged(Watched w, bool scoring)
    {
        if (w.hiddenForSlam) return;

        if (scoring && w.isScoreBox)
        {
            w.punch.Play(scoringPunchSize, Random.Range(-1f, 1f) * scoringTwistDegrees, scoringPunchSeconds);
            EmitScoringShards(w);
        }
        else w.punch.Play(punchSize, 0f, punchSeconds);
    }

    void EmitScoringShards(Watched w)
    {
        if (burst == null) return;
        Color cream = theme != null ? theme.cream : Color.white;
        burst.Emit(w.punch.Rect.position, scoringShards, w.text.color, cream, scoringShardStrength);
    }

    // ---------- Helpers ----------

    Watched Find(TMP_Text text)
    {
        foreach (Watched w in watched)
            if (w.text == text) return w;
        return null;
    }

    static void ShowText(Watched w, bool shown)
    {
        w.hiddenForSlam = !shown;
        w.text.alpha = shown ? 1f : 0f;
    }

    void HoldGate()
    {
        if (holdingGate) return;
        holdingGate = true;
        PresentationGate.Hold();
    }

    void ReleaseGate()
    {
        if (!holdingGate) return;
        holdingGate = false;
        PresentationGate.Release();
    }
}
