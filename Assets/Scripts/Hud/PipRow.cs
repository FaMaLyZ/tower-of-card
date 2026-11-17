using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// A row of pips, some lit and some spent: Hands left, swaps left, ladder levels.
/// A view helper: it only shows the numbers it is given.
public class PipRow : MonoBehaviour
{
    public List<Image> pips = new List<Image>();
    public Sprite litSprite;
    public Sprite spentSprite;

    [Tooltip("Hide pips past the total (e.g. if the rules allow fewer hands than there are pips)")]
    public bool hideUnusedPips = true;

    /// The first "lit" pips are lit, the rest up to "total" are spent.
    public void Show(int lit, int total)
    {
        for (int i = 0; i < pips.Count; i++)
        {
            Image pip = pips[i];
            if (pip == null) continue;

            bool used = i < total;
            pip.gameObject.SetActive(used || !hideUnusedPips);
            pip.sprite = i < lit ? litSprite : spentSprite;
        }
    }
}
