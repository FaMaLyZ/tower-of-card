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
    /// A2 adds the live hand preview to this event.
    public static event Action<IReadOnlyList<int>> SelectionChanged;

    public static void RaiseSelectionChanged(IReadOnlyList<int> selectedCardIds) => SelectionChanged?.Invoke(selectedCardIds);

    /// Backstop for listeners that forgot to unsubscribe (Master Plan rule 8).
    /// Runs once when the game starts, so a stale listener from the last Play session can't survive.
    /// Do NOT call this from StartNewRun(): it would remove listeners that just subscribed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearAllListeners()
    {
        PhaseChanged = null;
        HandChanged = null;
        SelectionChanged = null;
    }
}
