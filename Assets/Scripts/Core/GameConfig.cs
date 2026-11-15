using UnityEngine;

/// Every tunable number lives here, never in code (Master Plan rule 9).
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
    [Tooltip("Small, Big, Boss base targets for Ante 1.")]
    public int[] baseTargets = { 300, 450, 600 };
    [Tooltip("Reward as a percent: 120 = x1.2. Small, Big, Boss.")]
    public int[] rewardPercents = { 120, 140, 160 };
    [Tooltip("Each Ante multiplies targets by this percent: 150 = x1.5.")]
    [Min(100)] public int targetGrowthPercent = 150;
    [Range(0, 100)] public int leaveBonusPercent = 10;

    [Header("Pot")]
    [Range(0, 100)] public int breakPercent = 10;
    [Range(0, 100)] public int heightPercent = 10;

    [Header("Glass")]
    [Min(1)] public int glassMaxLives = 3;

    [Header("Shake (Master Plan 3.3)")]
    [Min(0f)] public float shakeMinSeconds = 1.0f;
    [Min(0f)] public float shakeQuietSeconds = 0.5f;
    [Min(0f)] public float shakeSpeedLimit = 0.05f;
    [Min(0f)] public float shakeSpinLimit = 0.10f;
    [Min(0.1f)] public float shakeMaxSeconds = 6.0f;

    [Header("Presentation")]
    [Min(0f)] public float maxPresentationWait = 3.0f;

    [Header("Old turn loop (temporary: A3c replaces these)")]
    [Min(1)] public int oldLoopTurnsPerRun = 10;
    [Min(0f)] public float oldLoopDrawSeconds = 2.0f;

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
