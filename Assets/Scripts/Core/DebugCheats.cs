#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// Developer shortcuts (Master Plan 7.3). Cheats change data through the workers; they never
/// start or skip a phase. Keys are added in the step that needs them:
/// A1 phase label, F1 push the tower over, F9 restart the blind; A3 F2, F3; A4 F4, F5.
public class DebugCheats : MonoBehaviour
{
}
#endif
