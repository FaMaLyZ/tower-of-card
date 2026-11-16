#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// Developer shortcuts. Cheats go through the workers or the conductor; they never skip a phase.
/// The conductor adds this component to itself.
///
///   on-screen label  current phase and seconds in it
///   F1               push the tower over
///   F2               +1000 Pot chips
///   F3               Safe Points up to the target
///   F4               crack the Glass once (never the last life)
///   F5               the next blind is the Boss
///   F9               restart the run
public class DebugCheats : MonoBehaviour
{
    const float PushSpeed = 3f;
    const long CheatPotChips = 1000;

    GameFlowController flow;

    void Awake()
    {
        flow = GetComponent<GameFlowController>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || flow == null) return;

        if (keyboard.f1Key.wasPressedThisFrame) PushTowerOver();
        if (keyboard.f2Key.wasPressedThisFrame) AddPotChips();
        if (keyboard.f3Key.wasPressedThisFrame) FillSafePointsToTarget();
        if (keyboard.f4Key.wasPressedThisFrame) CrackGlass();
        if (keyboard.f5Key.wasPressedThisFrame) MakeNextBlindBoss();
        if (keyboard.f9Key.wasPressedThisFrame) RestartRun();
    }

    void PushTowerOver()
    {
        if (flow.tower == null) return;
        Debug.Log("[Cheat] F1: push the tower over (at the next shake if it is not shaking now)");
        flow.tower.PushTowerOver(PushSpeed);
    }

    void AddPotChips()
    {
        Debug.Log($"[Cheat] F2: +{CheatPotChips} Pot chips");
        flow.RequestCheatAddPotChips(CheatPotChips);
    }

    void FillSafePointsToTarget()
    {
        Debug.Log("[Cheat] F3: Safe Points up to the target");
        flow.RequestCheatSafePointsToTarget();
    }

    void CrackGlass()
    {
        Debug.Log("[Cheat] F4: crack the Glass");
        flow.RequestCheatCrackGlass();
    }

    void MakeNextBlindBoss()
    {
        Debug.Log("[Cheat] F5: the next blind is the Boss");
        flow.RequestCheatNextBlindBoss();
    }

    void RestartRun()
    {
        Debug.Log("[Cheat] F9: restart the run");
        flow.RequestRestartRun();
    }

    void OnGUI()
    {
        if (flow == null || !flow.IsReady || flow.Phase == GamePhase.None) return;
        GUI.Label(new Rect(10, 10, 900, 24), BuildPhaseLabel());
    }

    string BuildPhaseLabel()
    {
        return $"Phase: {flow.Phase}  ({flow.SecondsInPhase:0.0}s)   Ante {flow.AnteNumber} {flow.CurrentBlind}   hand {flow.HandNumber}/{flow.HandsPerBlind}   " +
               $"safe {flow.SafePoints}/{flow.Target}   glass {flow.GlassLives}/{flow.GlassMaxLives}   " +
               "F1 push  F2 +pot  F3 safe  F4 crack  F5 boss next  F9 new run";
    }
}
#endif
