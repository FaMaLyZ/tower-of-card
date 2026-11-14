using UnityEngine;

/// EMPTY SHELL. GameFlowController (the conductor) replaced this script in step A1.
///
/// The file stays until step A6 because the MainGame, Filly and Immer scenes each hold a
/// TurnManager component: deleting the file now would leave a Missing Script in them.
/// It does nothing and switches itself off, so there is only one flow owner.
public class TurnManager : MonoBehaviour
{
    void Awake()
    {
        enabled = false;
    }
}
