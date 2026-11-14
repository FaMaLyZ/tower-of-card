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
public class HandCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    HandManager hand;
    Vector3 visualRestPosition;
    Quaternion visualRestRotation;
    Vector3 visualRestScale;

    // ---------- Read-only info ----------
    public CardInstance Card { get; private set; }
    public int CardId => Card != null ? Card.Id : -1;
    public CardData Data => Card != null ? Card.Data : null;

    /// The child that effects are allowed to move.
    public Transform Visual { get; private set; }

    // ---------- Setup (called once by HandManager) ----------

    /// Remembers which card this is, and the Visual's normal pose so effects can return to it.
    public void Setup(HandManager owner, CardInstance card, Transform visual)
    {
        hand = owner;
        Card = card;
        Visual = visual;
        RememberVisualRestPose();
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
}
