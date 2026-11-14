#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// Developer shortcuts (Master Plan 7.3). Cheats change data through the workers or send a
/// command to the conductor; they never start or skip a phase themselves.
/// The conductor adds this component to itself, so it never has to be placed in a scene.
///
///   on-screen label  current phase and seconds in it
///   F1               push the tower over
///   F9               restart the blind
///   (A3 adds F2, F3; A4 adds F4, F5)
public class DebugCheats : MonoBehaviour
{
    const float PushSpeed = 3f;

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
        if (keyboard.f9Key.wasPressedThisFrame) RestartBlind();
    }

    void PushTowerOver()
    {
        if (flow.tower == null) return;
        Debug.Log("[Cheat] F1: push the tower over");
        flow.tower.PushTowerOver(PushSpeed);
    }

    void RestartBlind()
    {
        Debug.Log("[Cheat] F9: restart the blind");
        flow.RequestRestartBlind();
    }

    void OnGUI()
    {
        if (flow == null) return;
        GUI.Label(new Rect(10, 10, 400, 24), BuildPhaseLabel());
    }

    string BuildPhaseLabel()
    {
        return $"Phase: {flow.Phase}  ({flow.SecondsInPhase:0.0}s)   hand {flow.HandNumber}/{flow.HandsPerRun}   F1 push  F9 restart";
    }
}
#endif
