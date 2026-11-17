using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// The deck's HUD (HUD Design §8): a count badge on the deck, the swap pips under it,
/// a blue "click me" glow when picked cards can be swapped, and a hover tooltip with the deck and discard counts.
/// The deck itself is a 3D object; these parts follow it on screen. A view: the deck click is SwapZone's job.
public class DeckHudView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;
    public DeckManager deckManager;
    public SwapManager swapManager;
    [Tooltip("The 3D deck (the object with SwapZone)")]
    public Transform deck;

    [Header("Parts")]
    [Tooltip("Moved to the deck's screen position every frame")]
    public RectTransform anchor;
    public TMP_Text countText;
    public PipRow swapPips;
    public GameObject swapGlow;
    public GameObject tooltip;
    public TMP_Text tooltipText;

    [Header("Texts")]
    [Tooltip("{0} = cards left in the draw pile, {1} = cards in the whole deck")]
    public string countFormat = "{0}<size=70%>/{1}</size>";
    [Tooltip("{0} = cards in the draw pile, {1} = cards in the discard pile")]
    public string tooltipFormat = "Deck {0} · Discard {1}";

    Camera cam;
    Renderer[] deckRenderers;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (swapManager == null) swapManager = FindAnyObjectByType<SwapManager>();
        if (deck == null)
        {
            SwapZone zone = FindAnyObjectByType<SwapZone>();
            if (zone != null) deck = zone.transform;
        }
        if (deck != null) deckRenderers = deck.GetComponentsInChildren<Renderer>();
        cam = Camera.main;
    }

    // LateUpdate: after the deck's own juice and the camera have moved this frame
    void LateUpdate()
    {
        if (flow == null || !flow.IsReady || deck == null || cam == null) return;

        Rect deckOnScreen = GetDeckScreenRect();
        FollowDeck(deckOnScreen);
        RefreshCount();
        RefreshSwapPips();
        RefreshGlow();
        RefreshTooltip(deckOnScreen);
    }

    void FollowDeck(Rect deckOnScreen)
    {
        if (anchor != null) anchor.position = new Vector3(deckOnScreen.center.x, deckOnScreen.center.y, 0f);
    }

    void RefreshCount()
    {
        if (countText != null && deckManager != null) countText.text = string.Format(countFormat, deckManager.DrawPileCount, deckManager.DeckSize);
    }

    void RefreshSwapPips()
    {
        if (swapPips == null || swapManager == null) return;
        int perHand = flow.config != null ? flow.config.swapsPerHand : swapPips.pips.Count;
        swapPips.Show(swapManager.SwapsLeft, perHand);
    }

    // Blue glow = "click me": cards are picked and the swap would be accepted
    void RefreshGlow()
    {
        if (swapGlow != null) swapGlow.SetActive(flow.CanSwapSelectedCard);
    }

    void RefreshTooltip(Rect deckOnScreen)
    {
        if (tooltip == null) return;

        bool hovering = Mouse.current != null && deckOnScreen.Contains(Mouse.current.position.ReadValue());
        tooltip.SetActive(hovering);
        if (hovering && tooltipText != null && deckManager != null)
            tooltipText.text = string.Format(tooltipFormat, deckManager.DrawPileCount, deckManager.DiscardCount);
    }

    // The deck's outline on screen, from its renderers' bounds
    Rect GetDeckScreenRect()
    {
        if (deckRenderers == null || deckRenderers.Length == 0)
        {
            Vector3 p = cam.WorldToScreenPoint(deck.position);
            return new Rect(p.x, p.y, 0f, 0f);
        }

        Bounds bounds = deckRenderers[0].bounds;
        foreach (Renderer r in deckRenderers) bounds.Encapsulate(r.bounds);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                (i & 4) == 0 ? bounds.min.z : bounds.max.z);
            Vector3 screen = cam.WorldToScreenPoint(corner);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
