using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// The Glass as three crystals (HUD Design §3.6): whole ones first, cracked ones on the right.
/// A whole life is a black crystal turning slowly; a cracked life is a red crystal turning fast.
/// With no frames assigned it shows the single whole / cracked sprites instead.
/// A view: it never cracks or repairs anything. The effects are GlassJuice, which may ask a crystal to keep
/// its old look for a moment: whole (HoldWhole) while red shards fly in, cracked (HoldCracked) while it wobbles.
public class GlassStarsView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("Stars, left to right")]
    public List<Image> stars = new List<Image>();
    [Tooltip("Optional crack line on each star, same order as the stars")]
    public List<GameObject> crackLines = new List<GameObject>();

    [Header("Sprites (used when there are no frames)")]
    public Sprite wholeSprite;
    public Sprite crackedSprite;

    [Header("Spinning crystals")]
    [Tooltip("One full turn of the whole (black) crystal, in order")]
    public List<Sprite> wholeFrames = new List<Sprite>();
    [Tooltip("One full turn of the cracked (red) crystal, in order")]
    public List<Sprite> crackedFrames = new List<Sprite>();
    [Tooltip("Frames per second for a whole crystal: slower turn. Below about 24 the steps start to show")]
    public float wholeFps = 30f;
    [Tooltip("Frames per second for a cracked crystal: fast turn")]
    public float crackedFps = 60f;
    [Tooltip("The last frame shows the same pose as the first (the turn closes on itself): skip it so the loop doesn't stutter")]
    public bool lastFrameRepeatsFirst = true;
    [Tooltip("Each crystal starts at a different point of its turn, so they don't spin in step")]
    public bool offsetEachStar = true;

    // Crystals an effect asked to keep their old look for now, even though their life has already changed
    readonly HashSet<int> heldWhole = new HashSet<int>();
    readonly HashSet<int> heldCracked = new HashSet<int>();

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
    }

    /// Keep this crystal looking whole (black) until ReleaseWhole, even if its life is lost. Presentation only.
    public void HoldWhole(int index) => heldWhole.Add(index);

    public void ReleaseWhole(int index) => heldWhole.Remove(index);

    /// Keep this crystal looking cracked (red) until ReleaseCracked, even if its life is back. Presentation only.
    public void HoldCracked(int index) => heldCracked.Add(index);

    public void ReleaseCracked(int index) => heldCracked.Remove(index);

    /// The frame a whole crystal shows right now (GlassJuice fades a copy of it out under the new red one)
    public Sprite CurrentWholeSprite(int index) => GetFrame(wholeFrames, wholeFps, index, wholeSprite);

    void OnDisable()
    {
        heldWhole.Clear();
        heldCracked.Clear();
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
            bool whole = (i < lives || heldWhole.Contains(i)) && !heldCracked.Contains(i);

            if (stars[i] != null)
            {
                stars[i].gameObject.SetActive(exists);
                stars[i].sprite = whole ? GetFrame(wholeFrames, wholeFps, i, wholeSprite)
                                        : GetFrame(crackedFrames, crackedFps, i, crackedSprite);
            }
            if (i < crackLines.Count && crackLines[i] != null) crackLines[i].SetActive(exists && !whole);
        }
    }

    // The frame this crystal is on right now (a still sprite when there are no frames)
    Sprite GetFrame(List<Sprite> frames, float fps, int starIndex, Sprite still)
    {
        if (frames == null || frames.Count == 0) return still;

        int count = lastFrameRepeatsFirst && frames.Count > 1 ? frames.Count - 1 : frames.Count;
        float offset = offsetEachStar ? starIndex * 0.37f * count : 0f;
        int frame = Mathf.FloorToInt(Time.unscaledTime * fps + offset) % count;
        return frames[frame];
    }
}
