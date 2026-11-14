using System.Collections;
using UnityEngine;

/// The only flow object that juice may touch. An effect calls Hold() when it starts and
/// Release() when it ends; the conductor waits until nobody is holding.
///
/// The gate can only DELAY the game. It never gives points, moves cards or picks the next phase.
/// With no juice in the project nobody holds, so the wait ends at once.
public static class PresentationGate
{
    static int holds;

    public static bool IsHeld => holds > 0;

    public static void Hold() => holds++;

    public static void Release() => holds = Mathf.Max(0, holds - 1);

    /// Call on a new run and on scene exit.
    public static void Reset() => holds = 0;

    /// Wait until every effect has released, or until maxSeconds passes.
    /// An effect that forgot to Release can't freeze the game.
    public static IEnumerator WaitUntilClear(float maxSeconds)
    {
        float deadline = Time.time + maxSeconds;
        while (holds > 0 && Time.time < deadline) yield return null;
        holds = 0;
    }

    // Keeps holds from surviving into the next Play session when Domain Reload is turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad() => holds = 0;
}
