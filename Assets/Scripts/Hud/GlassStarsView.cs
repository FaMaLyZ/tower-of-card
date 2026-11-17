using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// The Glass as three stained-glass stars (HUD Design §3.6): whole stars first, cracked ones on the right.
/// A view: it never cracks or repairs anything. The "cracked / repaired" note stays on GlassView.
public class GlassStarsView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("Stars, left to right")]
    public List<Image> stars = new List<Image>();
    [Tooltip("Optional crack line on each star, same order as the stars")]
    public List<GameObject> crackLines = new List<GameObject>();

    [Header("Sprites")]
    public Sprite wholeSprite;
    public Sprite crackedSprite;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    void Update()
    {
        if (flow == null || !flow.IsReady) return;
        ShowLives(flow.GlassLives, flow.GlassMaxLives);
    }

    void ShowLives(int lives, int maxLives)
    {
        for (int i = 0; i < stars.Count; i++)
        {
            bool exists = i < maxLives;
            bool whole = i < lives;

            if (stars[i] != null)
            {
                stars[i].gameObject.SetActive(exists);
                stars[i].sprite = whole ? wholeSprite : crackedSprite;
            }
            if (i < crackLines.Count && crackLines[i] != null) crackLines[i].SetActive(exists && !whole);
        }
    }
}
