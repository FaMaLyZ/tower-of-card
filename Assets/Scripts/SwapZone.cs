using UnityEngine;
using UnityEngine.EventSystems;

/// Put this on any 3D object (the deck, a bin...) to make it the swap spot.
/// With cards picked in the Swap phase, clicking the object swaps them.
///
/// Needs:  a Collider on the object, and a Physics Raycaster on the Main Camera.
public class SwapZone : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public GameFlowController flow;

    [Header("Hover feedback")]
    public float hoverScale = 1.1f;

    Vector3 baseScale;
    bool hovering;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        baseScale = transform.localScale;
    }

    void Update()
    {
        // grow a little when clicking would swap the picked cards
        bool highlight = hovering && flow != null && flow.CanSwapSelectedCard;
        Vector3 target = baseScale * (highlight ? hoverScale : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-15f * Time.deltaTime));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (flow != null) flow.RequestSwapSelectedCard();
    }

    public void OnPointerEnter(PointerEventData eventData) { hovering = true; }
    public void OnPointerExit(PointerEventData eventData) { hovering = false; }
}
