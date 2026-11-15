using UnityEngine;
using UnityEngine.EventSystems;

/// One card in the hand. HandManager builds it like this (the B0 hand contract):
///
///   HandCard #12 (Ace of Spades)   <- the ROOT: this script, the click collider, the real place in the hand
///     Visual                       <- the model the player sees
///
/// The rule (Master Plan rule 7):
///   - Only HandManager moves the ROOT. It is the card's true position and its click target.
///   - Effects (hover, punch, tilt...) move the VISUAL child only, then call ResetVisualPose() when done.
///   So an effect can never move where a card is clicked, and deleting every effect leaves the hand working.
///
/// For clicks to work:
///   - UI cards (on a Canvas): the card needs an Image with Raycast Target on.
///   - 3D cards: the Main Camera needs a Physics Raycaster component (the collider is added for you).
public class HandCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
                            IBeginDragHandler, IDragHandler, IEndDragHandler
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    HandManager hand;
    Renderer[] visualRenderers;
    MaterialPropertyBlock dimBlock;
    float dimAmount;                    // 0 = normal colour, 1 = fully dimmed
    Vector3 visualRestPosition;
    Quaternion visualRestRotation;
    Vector3 visualRestScale;

    // ---------- Read-only info ----------
    public CardInstance Card { get; private set; }
    public int CardId => Card != null ? Card.Id : -1;
    public CardData Data => Card != null ? Card.Data : null;

    /// The child that effects are allowed to move.
    public Transform Visual { get; private set; }

    /// The Visual's normal pose. Effects add their offsets to this, so they never drift.
    public Vector3 VisualRestPosition => visualRestPosition;
    public Quaternion VisualRestRotation => visualRestRotation;
    public Vector3 VisualRestScale => visualRestScale;

    // ---------- Setup (called once by HandManager) ----------

    /// Remembers which card this is, and the Visual's normal pose so effects can return to it.
    public void Setup(HandManager owner, CardInstance card, Transform visual)
    {
        hand = owner;
        Card = card;
        Visual = visual;
        RememberVisualRestPose();
        visualRenderers = visual != null ? visual.GetComponentsInChildren<Renderer>() : new Renderer[0];
    }

    /// Darkens the card (the cards waiting while the committed ones are played). "brightness" is the
    /// colour multiplier at full dim, e.g. 0.5. Done with a property block, so the material asset never changes.
    public float DimAmount => dimAmount;

    public void SetDim(float amount, float brightness)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(amount, dimAmount)) return;
        dimAmount = amount;

        if (dimBlock == null) dimBlock = new MaterialPropertyBlock();
        float multiplier = Mathf.Lerp(1f, brightness, amount);
        foreach (Renderer r in visualRenderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            if (amount <= 0f) { r.SetPropertyBlock(null); continue; }

            Material m = r.sharedMaterial;
            int id = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : -1;
            if (id < 0) continue;
            Color rest = m.GetColor(id);
            r.GetPropertyBlock(dimBlock);
            dimBlock.SetColor(id, new Color(rest.r * multiplier, rest.g * multiplier, rest.b * multiplier, rest.a));
            r.SetPropertyBlock(dimBlock);
        }
    }

    void RememberVisualRestPose()
    {
        if (Visual == null) return;
        visualRestPosition = Visual.localPosition;
        visualRestRotation = Visual.localRotation;
        visualRestScale = Visual.localScale;
    }

    /// Puts the Visual back exactly on the root. Effects call this when they finish or are switched off.
    public void ResetVisualPose()
    {
        if (Visual == null) return;
        Visual.localPosition = visualRestPosition;
        Visual.localRotation = visualRestRotation;
        Visual.localScale = visualRestScale;
    }

    // ---------- Mouse ----------

    // Mouse moves onto / off the card (used to show the card's details)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hand != null) hand.HoverCard(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hand != null) hand.UnhoverCard(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (hand != null) hand.ClickCard(this);
    }

    // ---------- Drag to reorder (B4) ----------
    // A press that moves past the EventSystem's Drag Threshold (10 px) is a drag, not a click.

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (hand != null) hand.BeginDragCard(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
        if (hand != null && cam != null) hand.DragCardTo(this, eventData.position, cam);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (hand != null) hand.EndDragCard(this);
    }
}
