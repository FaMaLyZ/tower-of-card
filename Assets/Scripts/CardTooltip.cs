using TMPro;
using UnityEngine;

/// A small label above the hovered hand card: rank, suit, weight and chips (juice-plan 5.1, Track B5).
///
/// A view: it only reads the hand. The numbers are the real ones the game uses
/// (CardData.Weight, the weight the player is shown, and PokerHandEvaluator.ChipValue).
/// The fade is optional: set Fade Seconds to 0 and it simply appears.
/// The same details are also in the HUD hint text, so nothing is lost if this is removed.
public class CardTooltip : MonoBehaviour
{
    [Header("Game references (auto-found if empty)")]
    public HandManager hand;
    public Canvas canvas;

    [Header("UI")]
    public TMP_Text label;
    [Tooltip("Optional: faded in and out. Without it the label just shows and hides.")]
    public CanvasGroup group;

    [Header("Placement")]
    [Tooltip("Screen pixels above the card's centre.")]
    public Vector2 offsetPixels = new Vector2(0f, 140f);

    [Header("Text")]
    [Tooltip("{0} = rank, {1} = suit, {2} = weight, {3} = chips")]
    [TextArea(1, 3)]
    public string format = "{0} of {1}\nWeight {2}   Chips {3}";
    [Min(0f)] public float fadeSeconds = 0.12f;

    Camera cam;
    float alpha;
    int shownCardId = -1;

    void Awake()
    {
        if (hand == null) hand = FindAnyObjectByType<HandManager>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (label == null) label = GetComponent<TMP_Text>();
        if (group == null) group = GetComponent<CanvasGroup>();
        if (label != null) label.raycastTarget = false;
        if (group != null) { group.blocksRaycasts = false; group.interactable = false; }
        SetAlpha(0f);
    }

    void LateUpdate()
    {
        if (label == null || hand == null) return;
        if (cam == null) cam = Camera.main;

        HandCardView card = FindCardToShow();
        if (card != null) ShowFor(card);

        FadeTo(card != null ? 1f : 0f);
    }

    // The hovered card, unless a card is being dragged (then the label would only get in the way)
    HandCardView FindCardToShow()
    {
        if (hand.IsDragging || cam == null) return null;
        return hand.FindView(hand.GetCardId(hand.HoveredIndex));
    }

    void ShowFor(HandCardView card)
    {
        if (card.CardId != shownCardId)
        {
            CardData data = card.Data;
            label.text = string.Format(format, data.rank, data.suit, data.Weight, PokerHandEvaluator.ChipValue(data.rank));
            shownCardId = card.CardId;
        }
        PlaceAbove(card.transform.position);
    }

    void PlaceAbove(Vector3 worldPosition)
    {
        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f || canvas == null) return;

        var parent = (RectTransform)label.rectTransform.parent;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, (Vector2)screen + offsetPixels, uiCam, out Vector2 local);
        label.rectTransform.localPosition = local;
    }

    void FadeTo(float target)
    {
        float step = fadeSeconds <= 0f ? 1f : Time.deltaTime / fadeSeconds;
        SetAlpha(Mathf.MoveTowards(alpha, target, step));
        if (alpha <= 0f) shownCardId = -1;
    }

    void SetAlpha(float value)
    {
        alpha = value;
        if (group != null) group.alpha = value;
        else if (label != null) label.enabled = value > 0f;
    }
}
