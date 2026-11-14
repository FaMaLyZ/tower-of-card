using UnityEngine;
using UnityEngine.EventSystems;

/// Put this on any 3D object in the scene (a box, a bin, a table...) to make it a discard spot.
/// With a card selected, clicking the object discards that card.
///
/// Needs:  a Collider on the object, and a Physics Raycaster on the Main Camera.
public class DiscardZone : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public DiscardManager discardManager;        // no longer used: the click goes to the conductor
    public GameFlowController flow;              // auto-found if empty

    [Header("Hover feedback")]
    [Tooltip("The object grows by this factor while the mouse is over it and a discard is possible. 1 = no effect.")]
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
        // grow a little when clicking would discard the selected card
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