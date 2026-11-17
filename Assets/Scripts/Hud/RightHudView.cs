using UnityEngine;

/// The right HUD panel itself (HUD Design §5): a warm gold glow around the frame during the
/// Bank / Ride choice, to show the decision is here. Its parts each have their own view.
public class RightHudView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("Parts")]
    public GameObject decisionGlow;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void Update()
    {
        if (decisionGlow == null) return;
        decisionGlow.SetActive(flow != null && flow.IsReady && flow.IsChoosingBankOrRide);
    }
}
