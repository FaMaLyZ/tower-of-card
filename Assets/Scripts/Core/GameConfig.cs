using UnityEngine;
using UnityEngine.Serialization;

/// Every tunable number lives here, never in code.
/// Percent values are whole numbers because the Inspector can't show decimal:
/// breakPercent = 10 means 10%, rewards use 120 for x1.2. Convert with PercentMath.ToRate.
[CreateAssetMenu(fileName = "GameConfig", menuName = "Tower of Cards/Game Config")]
public class GameConfig : ScriptableObject
{
    const int DeckSize = 52;

    [Header("Hand")]
    [Min(1)] public int handSize = 8;
    [Min(0)] public int swapsPerHand = 3;
    [Range(1, 5)] public int maxCommitCards = 5;

    [Header("Blind")]
    [Min(1)] public int handsPerBlind = 4;
    [Min(1)] public int antesPerRun = 4;
    public int[] baseTargets = { 150, 225, 300 };
    public int[] rewardPercents = { 120, 140, 160 };
    [Min(100)] public int targetGrowthPercent = 150;
    [Range(0, 100)] public int leaveBonusPercent = 10;

    [Header("Pot")]
    [Range(0, 100)] public int breakPercent = 10;
    [Range(0, 100)] public int heightPercent = 10;

    [Header("Glass")]
    [Min(1)] public int glassMaxLives = 3;

    [Header("Shake")]
    [Min(0f)] public float shakeMinSeconds = 1.0f;
    [Min(0f)] public float shakeQuietSeconds = 0.5f;
    [Min(0f)] public float shakeSpeedLimit = 0.05f;
    [Min(0f)] public float shakeSpinLimit = 0.10f;
    [Min(0.1f)] public float shakeMaxSeconds = 6.0f;

    [Header("Presentation")]
    [Min(0f)] public float maxPresentationWait = 3.0f;
    [Tooltip("Longest the shake waits for the score count-up and its effects (a big 5-card hand takes about 5.5 s)")]
    [Min(0f)] public float maxScoreRevealWait = 8.0f;
    [Tooltip("Longest the run's start waits for the intro (cards forming the deck)")]
    [Min(0f)] public float maxRunIntroWait = 5.0f;
    [Min(0f)] public float blindEndPauseSeconds = 2.5f;
    [FormerlySerializedAs("oldLoopDrawSeconds")]
    [Min(0f)] public float drawPauseSeconds = 2.0f;

    static readonly int[] DefaultBaseTargets = { 150, 225, 300 };
    static readonly int[] DefaultRewardPercents = { 120, 140, 160 };

    /// The Ante 1 target of a Small, Big or Boss blind (BlindManager grows it per Ante).
    public int GetBaseTarget(BlindKind kind) => PickForBlind(baseTargets, DefaultBaseTargets, kind);

    /// The reward of a Small, Big or Boss blind, as a percent (120 = x1.2).
    public int GetRewardPercent(BlindKind kind) => PickForBlind(rewardPercents, DefaultRewardPercents, kind);

    static int PickForBlind(int[] values, int[] fallback, BlindKind kind)
    {
        int index = (int)kind;
        return values != null && index < values.Length ? values[index] : fallback[index];
    }

    /// Most cards one blind can pull from the deck: first fill, a refill after each hand, and every swap.
    public int WorstCaseCardsDrawn =>
        handSize + (handsPerBlind - 1) * maxCommitCards + swapsPerHand * handsPerBlind;

    void OnValidate()
    {
        if (baseTargets == null || baseTargets.Length != 3)
        {
            Debug.LogWarning("GameConfig: baseTargets needs exactly 3 values (Small, Big, Boss).", this);
        }

        if (rewardPercents == null || rewardPercents.Length != 3)
        {
            Debug.LogWarning("GameConfig: rewardPercents needs exactly 3 values (Small, Big, Boss).", this);
        }

        if (WorstCaseCardsDrawn > DeckSize)
        {
            Debug.LogWarning(
                $"GameConfig: one blind can draw {WorstCaseCardsDrawn} cards but the deck has {DeckSize}. " +
                "The deck can run out.", this);
        }
    }
}
