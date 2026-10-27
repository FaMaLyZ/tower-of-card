using System;
using UnityEngine;
using System.Collections;

public enum TurnState
{
    StartTurn,    // short intro ("Turn N"), player can't place yet
    DuringTurn,   // player places cards
    EndTurn       // turn finished, short pause before the next one
}

/// Single-player turn system. Knows nothing about cards or UI - other scripts
/// listen to the events below or read the public properties.
///
/// Flow:  StartTurn -> DuringTurn -> EndTurn -> (next) StartTurn -> ...
public class TurnManager : MonoBehaviour
{
    [Header("Rules")]
    public int cardsPerTurn = 5;
    [Tooltip("0 = endless. Otherwise the game ends after this many turns.")]
    public int maxTurns = 0;

    [Header("Timing")]
    public float startTurnDelay = 1f;        // length of the StartTurn state (seconds)
    public float endTurnDelay = 1.5f;        // length of the EndTurn pause (seconds)
    public bool autoStartNextTurn = true;    // false = UI must call StartNextTurn()
    public bool beginOnStart = true;         // start the game automatically

    // ---------- Events (subscribe from other scripts) ----------
    public event Action<TurnState> OnStateChanged;   // any state change
    public event Action<int> OnStartTurn;            // turn number
    public event Action<int> OnDuringTurn;           // turn number
    public event Action<int> OnEndTurn;              // turn number
    public event Action<int, int> OnCardPlaced;      // placed this turn, cardsPerTurn
    public event Action OnGameOver;

    // ---------- Read-only info ----------
    public TurnState State { get; private set; } = TurnState.EndTurn;
    public int TurnNumber { get; private set; }
    public int CardsPlacedThisTurn { get; private set; }
    public int TotalCardsPlaced { get; private set; }
    public bool GameStarted { get; private set; }
    public bool IsGameOver { get; private set; }

    public int CardsLeft => Mathf.Max(0, cardsPerTurn - CardsPlacedThisTurn);

    /// True only while the player is allowed to place a card.
    public bool CanPlaceCard => GameStarted && !IsGameOver && State == TurnState.DuringTurn && CardsLeft > 0;

    /// True while the End Turn button should work.
    public bool CanEndTurn => GameStarted && !IsGameOver && State == TurnState.DuringTurn;

    /// True when autoStartNextTurn is off and we are waiting for StartNextTurn().
    public bool WaitingForNextTurn =>
        GameStarted && !IsGameOver && State == TurnState.EndTurn && !autoStartNextTurn && timer <= 0f;

    float timer;

    // ---------- Lifecycle ----------

    void Start()
    {
        if (beginOnStart) StartGame();
    }

    void Update()
    {
        if (!GameStarted || IsGameOver) return;

        switch (State)
        {
            case TurnState.StartTurn:
                timer -= Time.deltaTime;
                if (timer <= 0f) SetState(TurnState.DuringTurn);
                break;

            case TurnState.EndTurn:
                if (timer > 0f) timer -= Time.deltaTime;
                else if (autoStartNextTurn) BeginTurn();
                break;
        }
    }

    // ---------- Public API (call these from other scripts) ----------

    public void StartGame()
    {
        TurnNumber = 0;
        CardsPlacedThisTurn = 0;
        TotalCardsPlaced = 0;
        IsGameOver = false;
        GameStarted = true;
        BeginTurn();
    }

    /// Call this every time the player places a card.
    public void RegisterCardPlaced()
    {
        if (!CanPlaceCard) return;

        CardsPlacedThisTurn++;
        TotalCardsPlaced++;
        OnCardPlaced?.Invoke(CardsPlacedThisTurn, cardsPerTurn);

        if (CardsPlacedThisTurn >= cardsPerTurn) EndTurn();
    }

    /// End Turn button (or automatic when all cards are placed).
    public void EndTurn()
    {
        if (!CanEndTurn) return;

        timer = endTurnDelay;
        SetState(TurnState.EndTurn);

        if (maxTurns > 0 && TurnNumber >= maxTurns)
        {
            StartCoroutine(DelayForCalSocre());
            OnGameOver?.Invoke();
        }
    }
    IEnumerator DelayForCalSocre()
    {
        yield return new WaitForSeconds(3f);    
        IsGameOver = true;
    }

    /// Only needed when autoStartNextTurn is off.
    public void StartNextTurn()
    {
        if (WaitingForNextTurn) BeginTurn();
    }

    // ---------- Internals ----------

    void BeginTurn()
    {
        TurnNumber++;
        CardsPlacedThisTurn = 0;
        timer = startTurnDelay;
        SetState(TurnState.StartTurn);

        if (startTurnDelay <= 0f) SetState(TurnState.DuringTurn);
    }

    void SetState(TurnState newState)
    {
        State = newState;
        OnStateChanged?.Invoke(State);

        switch (State)
        {
            case TurnState.StartTurn: OnStartTurn?.Invoke(TurnNumber); break;
            case TurnState.DuringTurn: OnDuringTurn?.Invoke(TurnNumber); break;
            case TurnState.EndTurn: OnEndTurn?.Invoke(TurnNumber); break;
        }
    }
}