using System.Collections.Generic;
using UnityEngine;

/// Tower effect (C5): when the Pot is banked the tower is swept away. The game removes the real cards at once;
/// in that same frame this makes look-alike copies (meshes and materials only: no physics, no game logic).
/// Each copy, top cards first:
///   1. Wind-up: spins one full turn in place on the screen, speeding up.
///   2. Fling: carried by that momentum it swings away, sideways first and then up, still spinning,
///      and flies off the top of the screen.
/// Put it on any object in the game scene. Listen-only: delete it and the tower just vanishes as before.
public class TowerSweepJuice : MonoBehaviour
{
    [Header("Wind-up spin")]
    [Tooltip("One full turn in place before it flies; it speeds up as it turns")]
    public float spinSeconds = 0.35f;
    [Tooltip("Turns in the wind-up (1 = one full 360)")]
    public float spinTurns = 1f;

    [Header("Fling")]
    public float flySeconds = 0.55f;
    [Tooltip("Which way the swing curves: -1 = to the left, 1 = to the right, 0 = each card picks a side")]
    [Range(-1f, 1f)] public float swingSide = -1f;
    [Tooltip("How far sideways the swing reaches, as a share of the screen's height at the tower")]
    public float swingWidth = 0.6f;
    [Tooltip("How far above the top of the screen it ends, as a share of the screen's height")]
    public float flyAbove = 0.25f;
    [Tooltip("How much each card's path differs from the others")]
    [Range(0f, 1f)] public float spread = 0.3f;

    [Header("Order")]
    [Tooltip("Top cards go first; the bottom card goes this many seconds later")]
    public float stagger = 0.25f;
    [Tooltip("Most copies at once, so a huge tower stays cheap")]
    public int maxCards = 60;

    /// One flying copy of a card: where it starts, the curve it swings along, and how it turns
    class Flyer
    {
        public GameObject copy;
        public float start;
        public Vector3 from;
        public Vector3 bend;            // the curve's pull point: off to the side, so it swings out before rising
        public Vector3 to;              // above the top of the screen
        public Vector3 spinAxis;        // toward the camera: it turns flat on the screen
        public Quaternion startRotation;
    }

    readonly List<Flyer> flyers = new List<Flyer>();
    readonly List<Mesh> bakedMeshes = new List<Mesh>();

    void OnEnable() => GameEvents.PotBanked += OnPotBanked;

    void OnDisable()
    {
        GameEvents.PotBanked -= OnPotBanked;
        ClearAll();
    }

    // The real cards are already on their way out (Destroy waits for the end of this frame): copy them now
    void OnPotBanked(long payout, long safePoints, long runTotal) => SweepCards();

    /// Copies every card standing on the table and blows the copies away
    public void SweepCards()
    {
        List<Card> cards = FindTowerCards();
        if (cards.Count == 0) return;

        float lowest = float.MaxValue, highest = float.MinValue;
        foreach (Card card in cards)
        {
            lowest = Mathf.Min(lowest, card.transform.position.y);
            highest = Mathf.Max(highest, card.transform.position.y);
        }

        Camera cam = Camera.main;
        if (cam == null) return;
        foreach (Card card in cards)
        {
            float height = highest > lowest ? (card.transform.position.y - lowest) / (highest - lowest) : 1f;
            Launch(card, cam, (1f - height) * stagger);
        }
    }

    // Real tower cards only: the placement ghost has no deck id
    List<Card> FindTowerCards()
    {
        var found = new List<Card>();
        foreach (Card card in FindObjectsByType<Card>(FindObjectsInactive.Exclude))
        {
            if (card != null && card.CardId >= 0) found.Add(card);
            if (found.Count >= maxCards) break;
        }
        return found;
    }

    void Launch(Card card, Camera cam, float delay)
    {
        GameObject copy = CopyLooks(card.transform);
        if (copy == null) return;

        Vector3 from = copy.transform.position;
        float screenHeight = ScreenHeightAt(cam, from);
        float side = swingSide != 0f ? Mathf.Sign(swingSide) : (Random.value < 0.5f ? -1f : 1f);
        float vary = 1f + Random.Range(-spread, spread);
        Vector3 right = cam.transform.right * side;
        Vector3 up = cam.transform.up;

        flyers.Add(new Flyer
        {
            copy = copy,
            start = Time.time + delay,
            from = from,
            bend = from + right * swingWidth * screenHeight * vary,
            to = from + up * (1f + flyAbove) * screenHeight + right * swingWidth * 0.5f * screenHeight * vary,
            spinAxis = -cam.transform.forward * side,
            startRotation = copy.transform.rotation,
        });
    }

    // How tall the screen is, in world units, at this point's distance from the camera
    static float ScreenHeightAt(Camera cam, Vector3 point)
    {
        float depth = Vector3.Dot(point - cam.transform.position, cam.transform.forward);
        return 2f * Mathf.Max(0.1f, depth) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    // ---------- Copying a card's looks ----------

    // An empty object at the card's pose with one plain mesh per renderer (skinned meshes are baked as they are now)
    GameObject CopyLooks(Transform card)
    {
        var root = new GameObject("SweptCard");
        root.transform.SetPositionAndRotation(card.position, card.rotation);

        int parts = 0;
        foreach (Renderer source in card.GetComponentsInChildren<Renderer>())
            if (source.enabled && CopyRenderer(source, root.transform)) parts++;

        if (parts > 0) return root;
        Destroy(root);
        return null;
    }

    bool CopyRenderer(Renderer source, Transform parent)
    {
        Mesh mesh = GetMesh(source, out bool baked);
        if (mesh == null) return false;

        var part = new GameObject(source.name);
        part.transform.SetParent(parent, false);
        part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        part.transform.localScale = baked ? Vector3.one : LocalScaleUnder(parent, source.transform);

        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.shadowCastingMode = source.shadowCastingMode;
        return true;
    }

    Mesh GetMesh(Renderer source, out bool baked)
    {
        baked = false;
        if (source is SkinnedMeshRenderer skinned)
        {
            var mesh = new Mesh();
            skinned.BakeMesh(mesh, true);               // baked in world scale: the copy gets scale 1
            bakedMeshes.Add(mesh);
            baked = true;
            return mesh;
        }
        var filter = source.GetComponent<MeshFilter>();
        return source is MeshRenderer && filter != null ? filter.sharedMesh : null;
    }

    // The source's world size, expressed under the copy's (unscaled) root
    static Vector3 LocalScaleUnder(Transform parent, Transform source)
    {
        Vector3 world = source.lossyScale;
        Vector3 parentScale = parent.lossyScale;
        return new Vector3(world.x / parentScale.x, world.y / parentScale.y, world.z / parentScale.z);
    }

    // ---------- Every frame ----------

    void Update()
    {
        for (int i = flyers.Count - 1; i >= 0; i--)
            if (Step(flyers[i])) Remove(i);
    }

    // Wind-up, then the fling. True when it has left the screen.
    bool Step(Flyer flyer)
    {
        if (flyer.copy == null) return true;
        float age = Time.time - flyer.start;
        if (age < 0f) return false;                     // waiting for the cards above it to go

        float spinDegrees = 360f * spinTurns;
        float windUp = Mathf.Max(0.01f, spinSeconds);
        Transform t = flyer.copy.transform;

        if (age < windUp)
        {
            float w = age / windUp;
            Turn(flyer, spinDegrees * w * w);           // speeding up, like it is being wound
            return false;
        }

        float f = (age - windUp) / Mathf.Max(0.01f, flySeconds);
        if (f >= 1f) return true;

        // Leaves at the speed the wind-up reached and keeps turning that fast
        float spinSpeed = 2f * spinDegrees / windUp;
        Turn(flyer, spinDegrees + spinSpeed * (age - windUp));
        t.position = Bezier(flyer.from, flyer.bend, flyer.to, f * f);
        return false;
    }

    void Turn(Flyer flyer, float degrees)
    {
        flyer.copy.transform.rotation = Quaternion.AngleAxis(degrees, flyer.spinAxis) * flyer.startRotation;
    }

    static Vector3 Bezier(Vector3 a, Vector3 bend, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * bend + t * t * b;
    }

    void Remove(int index)
    {
        if (flyers[index].copy != null) Destroy(flyers[index].copy);
        flyers.RemoveAt(index);
        if (flyers.Count == 0) FreeBakedMeshes();
    }

    void ClearAll()
    {
        for (int i = flyers.Count - 1; i >= 0; i--) Remove(i);
        FreeBakedMeshes();
    }

    void FreeBakedMeshes()
    {
        foreach (Mesh mesh in bakedMeshes)
            if (mesh != null) Destroy(mesh);
        bakedMeshes.Clear();
    }
}
