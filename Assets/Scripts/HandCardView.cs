using UnityEngine;
using UnityEngine.EventSystems;

/// Added automatically by HandManager to every card in the hand.
/// Lets the player click a card to choose it.
///
/// For this to work:
///   - UI cards (on a Canvas): the card needs an Image with Raycast Target on.
///   - 3D cards: the card needs a Collider, and the Main Camera needs a Physics Raycaster component.
public class HandCardView : MonoBehaviour, IPointerClickHandler
{
    public HandManager hand;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (hand != null) hand.ClickCard(gameObject);
    }
}