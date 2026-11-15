using System;
using System.Collections.Generic;
using UnityEngine;

/// The steps of one run. Only GameFlowController changes the phase (Master Plan rule 4).
public enum GamePhase
{
    None,
    RunIntro,
    BlindMenu,
    Draw,
    Swap,
    Commit,
    Build,
    Score,
    Shake,
    BankOrRide,
    ContinueOrLeave,
    BlindEnd,
    RunEnd
}

/// Why the Glass changed (sent with GameEvents.GlassChanged).
public enum GlassChangeReason
{
    BlindFailed,
    BossWon,
    Skipped,
    Cheat
}

/// Why the Pot changed (sent with GameEvents.PotChanged).
public enum PotChangeReason
{
    HandScored,
    ShakeResolved,
    Collapsed,
    Banked,
    Cleared,
    Cheat
}

/// Announcements for the HUD, sound and juice. An event tells listeners what ALREADY
/// happened; it never moves the game forward (Master Plan rule 3).
///
/// Add each event in the step that first needs it (see Master Plan section 2.6).
public static class GameEvents
{
    public static event Action<GamePhase> PhaseChanged;

    public static void RaisePhaseChanged(GamePhase newPhase) => PhaseChanged?.Invoke(newPhase);

    /// The cards in the hand changed (drawn, played, swapped, emptied). Carries the card IDs, left to right.
    public static event Action<IReadOnlyList<int>> HandChanged;

    public static void RaiseHandChanged(IReadOnlyList<int> cardIdsInOrder) => HandChanged?.Invoke(cardIdsInOrder);

    /// The selected cards changed. Carries the selected card IDs (empty = nothing selected).
    /// The live hand name for these cards is CommitManager.Preview (the conductor refreshes it).
    public static event Action<IReadOnlyList<int>> SelectionChanged;

    public static void RaiseSelectionChanged(IReadOnlyList<int> selectedCardIds) => SelectionChanged?.Invoke(selectedCardIds);

    /// A card is breaking (raised just before it is destroyed). TowerBuilder counts it for the
    /// next ShakeResult; effects may play here. The Pot numbers come from ShakeResolved, never from this.
    public static event Action<Card> CardBroken;

    public static void RaiseCardBroken(Card card) => CardBroken?.Invoke(card);

    /// A shake is fully resolved: what broke, how many levels stand, collapsed or not.
    public static event Action<ShakeResult> ShakeResolved;

    public static void RaiseShakeResolved(ShakeResult result) => ShakeResolved?.Invoke(result);

    /// The Pot changed, told AFTER the change. Carries the old chips, the new chips, why, and what
    /// a Bank would pay now. Listeners show these numbers; they never redo the maths.
    public static event Action<long, long, PotChangeReason, long> PotChanged;

    public static void RaisePotChanged(long oldChips, long newChips, PotChangeReason reason, long bankQuote) =>
        PotChanged?.Invoke(oldChips, newChips, reason, bankQuote);

    /// A Bank was paid. Carries the payout, the Safe Points after it and the Run Total after it.
    public static event Action<long, long, long> PotBanked;

    public static void RaisePotBanked(long payout, long safePoints, long runTotal) => PotBanked?.Invoke(payout, safePoints, runTotal);

    /// The player took Leave Table. Carries the bonus paid and the blind score after it.
    public static event Action<long, long> TableLeft;

    public static void RaiseTableLeft(long bonus, long blindScore) => TableLeft?.Invoke(bonus, blindScore);

    /// The blind menu opened. Carries the blind that can be chosen, the Ante and its target.
    public static event Action<BlindKind, int, long> BlindOffered;

    public static void RaiseBlindOffered(BlindKind kind, int ante, long target) => BlindOffered?.Invoke(kind, ante, target);

    /// A Small or Big blind was skipped. Carries which blind and the Ante.
    public static event Action<BlindKind, int> BlindSkipped;

    public static void RaiseBlindSkipped(BlindKind kind, int ante) => BlindSkipped?.Invoke(kind, ante);

    /// A blind started. Carries which blind, the Ante and its target.
    public static event Action<BlindKind, int, long> BlindStarted;

    public static void RaiseBlindStarted(BlindKind kind, int ante, long target) => BlindStarted?.Invoke(kind, ante, target);

    /// A blind ended as a win. Carries the blind score and the target.
    public static event Action<long, long> BlindWon;

    public static void RaiseBlindWon(long blindScore, long target) => BlindWon?.Invoke(blindScore, target);

    /// A blind ended as a fail. Carries the blind score and the target.
    public static event Action<long, long> BlindFailed;

    public static void RaiseBlindFailed(long blindScore, long target) => BlindFailed?.Invoke(blindScore, target);

    /// The Glass gained or lost a life. Carries the old lives, the new lives and why.
    public static event Action<int, int, GlassChangeReason> GlassChanged;

    public static void RaiseGlassChanged(int oldLives, int newLives, GlassChangeReason reason) =>
        GlassChanged?.Invoke(oldLives, newLives, reason);

    /// The run is over. Carries the Run Total and whether it was cleared (false = shattered).
    public static event Action<long, bool> RunEnded;

    public static void RaiseRunEnded(long runTotal, bool cleared) => RunEnded?.Invoke(runTotal, cleared);

    /// Backstop for listeners that forgot to unsubscribe (Master Plan rule 8).
    /// Runs once when the game starts, so a stale listener from the last Play session can't survive.
    /// Do NOT call this from StartNewRun(): it would remove listeners that just subscribed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearAllListeners()
    {
        PhaseChanged = null;
        HandChanged = null;
        SelectionChanged = null;
        CardBroken = null;
        ShakeResolved = null;
        PotChanged = null;
        PotBanked = null;
        TableLeft = null;
        BlindStarted = null;
        BlindWon = null;
        BlindFailed = null;
        GlassChanged = null;
        BlindOffered = null;
        BlindSkipped = null;
        RunEnded = null;
    }
}
