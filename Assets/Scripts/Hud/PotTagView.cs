using TMPro;
using UnityEngine;

/// The Pot tag (HUD Design §7.3): a small pill floating above the tower with the raw Pot
/// "sitting on the tower". Shown whenever the Pot is not 0 during a hand; it rises as the tower grows.
public class PotTagView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public TowerBuilder tower;

    [Header("Parts")]
    [Tooltip("The pill that is moved and shown / hidden")]
    public RectTransform pill;
    public TMP_Text amount;

    [Header("Placement")]
    [Tooltip("World units above the highest card")]
    public float heightAboveTower = 0.35f;
    public string amountFormat = "{0:N0}";

    Camera cam;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (tower == null) tower = FindAnyObjectByType<TowerBuilder>();
        cam = Camera.main;
    }

    // LateUpdate: after the camera has moved this frame, so the tag does not lag behind an orbit
    void LateUpdate()
    {
        if (pill == null) return;

        bool show = ShouldShow();
        pill.gameObject.SetActive(show);
        if (!show) return;

        if (amount != null) amount.text = string.Format(amountFormat, flow.PotChips);
        FollowTower();
    }

    bool ShouldShow()
    {
        if (flow == null || !flow.IsReady || tower == null || cam == null) return false;
        if (flow.PotChips <= 0) return false;

        switch (flow.Phase)
        {
            case GamePhase.Draw:
            case GamePhase.Swap:
            case GamePhase.Commit:
            case GamePhase.Build:
            case GamePhase.Score:
            case GamePhase.Shake:
            case GamePhase.BankOrRide: return true;
            default:                   return false;
        }
    }

    // On a Screen Space - Overlay canvas a UI element's position is in screen pixels
    void FollowTower()
    {
        Vector3 screen = cam.WorldToScreenPoint(tower.TopPoint + Vector3.up * heightAboveTower);
        if (screen.z <= 0f)
        {
            pill.gameObject.SetActive(false);      // behind the camera
            return;
        }
        pill.position = new Vector3(screen.x, screen.y, 0f);
    }
}
