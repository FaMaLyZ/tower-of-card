using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Run intro (C8). At the start of a run:
///   1. The HUD is hidden and the deck sits in the middle of the screen, bigger, and empty.
///   2. 52 cards fly in from every edge of the screen, curving and spinning, onto the deck. Each landing grows
///      the deck and makes it wobble; every few it squashes. The last one lands with a big punch and sparks.
///   3. The full deck travels down to its corner, then the whole HUD arrives at once, every piece from its own
///      side: left from the left, right from the right, top from the top, bottom from the bottom. The side
///      pieces lean as they come; all overshoot their place a little and snap back. Anything not in a list
///      (a tag, a tooltip) fades in meanwhile.
/// A click skips it. The flying cards are look-alike copies (mesh only); the real deck's cards are never touched.
/// The game waits for it (it holds PresentationGate during GamePhase.RunIntro, capped by the game).
/// Put it on any object in the game scene. Listen-only: delete it and the run starts straight away.
public class RunIntroJuice : MonoBehaviour
{
    [Header("Parts")]
    public DeckJuice deck;
    [Tooltip("A card of the deck: its mesh and material are copied for the flying cards")]
    public MeshRenderer cardLook;
    [Tooltip("The HUD canvas: hidden during the intro (the effects layer stays visible)")]
    public Canvas hud;
    public UiShardBurst burst;
    public CameraShaker cameraShaker;
    public HudTheme theme;

    [Header("Deck in the middle")]
    [Tooltip("How much bigger the deck is while it sits in the middle")]
    public float centreScale = 1.6f;

    [Header("Cards")]
    public int cardCount = 52;
    [Tooltip("Time from the first card leaving to the last card leaving")]
    public float launchSeconds = 1.1f;
    [Tooltip("Each card's flight time")]
    public float flySeconds = 0.45f;
    [Tooltip("How far outside the screen the cards start (0.5 = half a screen past the edge)")]
    public float startOutside = 0.3f;
    [Tooltip("How much each path bows to the side, as a share of its length")]
    public float curve = 0.35f;
    public float spinDegrees = 540f;
    [Tooltip("Every this many landings the deck also squashes (it wobbles on every one)")]
    public int squashEvery = 3;

    [Header("After the last card")]
    public int finishSparks = 40;
    [Range(0f, 1f)] public float finishCameraBump = 0.3f;
    [Tooltip("A beat with the full deck in the middle before it moves")]
    public float pauseSeconds = 0.25f;
    [Tooltip("Time for the deck to travel down to its corner")]
    public float travelSeconds = 0.45f;
    [Tooltip("How high the deck's path bows on the way, in world units")]
    public float travelArc = 0.3f;
    [Tooltip("HUD pieces that are in none of the lists below fade in over this time, while the others run in")]
    public float hudFadeSeconds = 0.3f;

    [Header("HUD running in")]
    [Tooltip("Blocks that run in from the left edge")]
    public List<RectTransform> fromLeft = new List<RectTransform>();
    [Tooltip("Blocks that run in from the right edge")]
    public List<RectTransform> fromRight = new List<RectTransform>();
    [Tooltip("Blocks that drop in from the top")]
    public List<RectTransform> fromTop = new List<RectTransform>();
    [Tooltip("Blocks that rise in from the bottom")]
    public List<RectTransform> fromBottom = new List<RectTransform>();
    public float entranceSeconds = 0.5f;
    [Tooltip("0 = every block arrives together. Above 0, each starts this much after the one before")]
    public float entranceStagger = 0f;
    [Tooltip("How far past its place each block goes before snapping back (share of the trip)")]
    [Range(0f, 0.3f)] public float entranceOvershoot = 0.12f;
    [Tooltip("How far the side blocks lean as they come in, in degrees")]
    public float entranceLean = 14f;
    [Tooltip("Extra distance off screen they start from, in canvas pixels")]
    public float entranceMargin = 60f;

    enum Stage { Idle, Cards, Pause, Travel, Reveal }

    /// One flying copy of a card
    class Flyer
    {
        public GameObject copy;
        public float start;
        public Vector3 from;
        public Vector3 bend;
        public Vector3 spinAxis;
        public Quaternion startRotation;
    }

    readonly List<Flyer> flyers = new List<Flyer>();
    Stage stage = Stage.Idle;
    float stageStart;
    bool holdingGate;
    int landed;

    Transform deckRoot;
    Vector3 deckRestPosition;           // its place in the corner (local to the camera)
    Vector3 deckRestScale;
    Vector3 deckCentrePosition;         // the middle of the screen at the same distance
    CanvasGroup hudGroup;
    readonly List<HudEntrance> sideEntrances = new List<HudEntrance>();
    readonly List<HudEntrance> topEntrances = new List<HudEntrance>();

    // Each running block gets its own visibility while the rest of the HUD is still hidden
    struct OwnFade
    {
        public CanvasGroup group;
        public bool ignoredBefore;
        public bool added;
    }
    readonly List<OwnFade> ownFades = new List<OwnFade>();

    void Awake()
    {
        if (deck == null) deck = FindAnyObjectByType<DeckJuice>();
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();
        if (cameraShaker == null) cameraShaker = FindAnyObjectByType<CameraShaker>();
        if (hud == null && burst != null) hud = burst.GetComponentInParent<Canvas>().rootCanvas;
        if (deck != null)
        {
            deckRoot = deck.transform;
            deckRestPosition = deckRoot.localPosition;
            deckRestScale = deckRoot.localScale;
        }
    }

    void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;

    void OnDisable()
    {
        GameEvents.PhaseChanged -= OnPhaseChanged;
        FinishNow(false);
    }

    void OnPhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.RunIntro) Play();
        else if (stage != Stage.Idle) FinishNow(false);     // the game moved on (the wait ran out): never stay half done
    }

    // ---------- Start ----------

    /// The whole intro (public for the juice debug panel)
    public void Play()
    {
        Camera cam = Camera.main;
        if (deck == null || cardLook == null || cam == null) return;

        FinishNow(false);
        HoldGate();
        landed = 0;

        PutDeckInTheMiddle(cam);
        HideHud();
        PrepareEntrances();
        LaunchAllCards(cam);
        Begin(Stage.Cards);
        AudioManager.Instance.PlayOneShotSFX(SoundType.Intro_CardFlyIntoMid);
    }

    // Same distance from the camera, straight ahead of it, bigger and empty
    void PutDeckInTheMiddle(Camera cam)
    {
        Vector3 local = deckRestPosition;
        deckCentrePosition = deckRoot.parent == cam.transform ? new Vector3(0f, 0f, local.z) : local;
        deckRoot.localPosition = deckCentrePosition;
        deckRoot.localScale = deckRestScale * centreScale;
        deck.SetIntroFill(0f);
    }

    void LaunchAllCards(Camera cam)
    {
        float now = Time.unscaledTime;
        Vector3 target = deckRoot.position;
        for (int i = 0; i < cardCount; i++)
            Launch(cam, target, now + launchSeconds * i / Mathf.Max(1, cardCount - 1));
    }

    void Launch(Camera cam, Vector3 target, float start)
    {
        GameObject copy = CopyCard();
        Vector3 from = PointOffScreen(cam, target);
        Vector3 side = Vector3.Cross(target - from, cam.transform.forward).normalized;
        copy.transform.position = from;
        copy.SetActive(false);                          // shown when its turn comes

        flyers.Add(new Flyer
        {
            copy = copy,
            start = start,
            from = from,
            bend = (from + target) * 0.5f + side * Vector3.Distance(from, target) * curve * Random.Range(-1f, 1f),
            spinAxis = Random.onUnitSphere,
            startRotation = Random.rotation,
        });
    }

    // A random point past the edge of the screen, at the deck's distance from the camera
    Vector3 PointOffScreen(Camera cam, Vector3 target)
    {
        float depth = Vector3.Dot(target - cam.transform.position, cam.transform.forward);
        Vector2 direction = Random.insideUnitCircle.normalized;
        Vector2 viewport = new Vector2(0.5f, 0.5f) + direction * (0.5f + startOutside) * 1.42f;
        return cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
    }

    // Only the card's looks: one mesh and its materials at the deck card's size (the deck is already enlarged)
    GameObject CopyCard()
    {
        var copy = new GameObject("IntroCard");
        copy.transform.localScale = cardLook.transform.lossyScale;
        copy.AddComponent<MeshFilter>().sharedMesh = cardLook.GetComponent<MeshFilter>().sharedMesh;
        var renderer = copy.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = cardLook.sharedMaterials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return copy;
    }

    // ---------- Every frame ----------

    void Begin(Stage next)
    {
        stage = next;
        stageStart = Time.unscaledTime;
    }

    void Update()
    {
        if (stage == Stage.Idle) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            FinishNow(true);                            // skipped: everything is just in place
            return;
        }

        float elapsed = Time.unscaledTime - stageStart;
        switch (stage)
        {
            case Stage.Cards:  StepCards(); break;
            case Stage.Pause:  if (elapsed >= pauseSeconds) Begin(Stage.Travel); break;
            case Stage.Travel: StepTravel(elapsed); break;
            case Stage.Reveal: StepReveal(elapsed); break;
        }
    }

    void StepCards()
    {
        Vector3 target = deckRoot.position;
        for (int i = flyers.Count - 1; i >= 0; i--)
            if (StepFlyer(flyers[i], target)) Land(i);

        if (flyers.Count > 0) return;
        Celebrate();
        Begin(Stage.Pause);
    }

    // Curves in, speeding up, spinning; true when it reaches the deck
    bool StepFlyer(Flyer flyer, Vector3 target)
    {
        float t = (Time.unscaledTime - flyer.start) / Mathf.Max(0.01f, flySeconds);
        if (t < 0f) return false;
        if (t >= 1f) return true;

        flyer.copy.SetActive(true);
        float eased = t * t;
        float u = 1f - eased;
        flyer.copy.transform.position = u * u * flyer.from + 2f * u * eased * flyer.bend + eased * eased * target;
        flyer.copy.transform.rotation = Quaternion.AngleAxis(spinDegrees * t, flyer.spinAxis) * flyer.startRotation;
        return false;
    }

    // The card joins the deck: it grows a little and wobbles, and squashes every few cards
    void Land(int index)
    {
        Destroy(flyers[index].copy);
        flyers.RemoveAt(index);
        landed++;

        deck.SetIntroFill(Mathf.Sqrt((float)landed / cardCount));      // fills fast at first, like a pile forming
        bool squash = squashEvery > 0 && landed % squashEvery == 0;
        deck.PlayIntroLanding(squash, landed >= cardCount);
    }

    // The deck goes down to its corner along a gentle arc, shrinking back to its normal size
    void StepTravel(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, travelSeconds));
        float eased = t * t * (3f - 2f * t);
        deckRoot.localPosition = Vector3.Lerp(deckCentrePosition, deckRestPosition, eased) +
                                 Vector3.up * travelArc * Mathf.Sin(t * Mathf.PI);
        deckRoot.localScale = Vector3.Lerp(deckRestScale * centreScale, deckRestScale, eased);
        if (t < 1f) return;

        PutDeckBack();
        PlaceEntrances(0f);                             // the blocks wait just off screen...
        GiveBlocksTheirOwnFade();                       // ...visible, while the rest of the HUD stays hidden
        Begin(Stage.Reveal);
    }

    // Every block runs in from its side while anything left over fades in
    void StepReveal(float elapsed)
    {
        float blocksDone = PlaceEntrances(elapsed);
        float fade = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hudFadeSeconds));
        SetHudAlpha(fade);
        if (elapsed >= blocksDone && fade >= 1f) FinishNow(false);
    }

    // ---------- HUD running in ----------

    // Remember each block's real place now, before anything moves it
    void PrepareEntrances()
    {
        ReturnEntrancesToRest();
        sideEntrances.Clear();
        topEntrances.Clear();
        foreach (RectTransform block in fromLeft) if (block != null) sideEntrances.Add(new HudEntrance(block, Vector2.left));
        foreach (RectTransform block in fromRight) if (block != null) sideEntrances.Add(new HudEntrance(block, Vector2.right));
        foreach (RectTransform block in fromTop) if (block != null) topEntrances.Add(new HudEntrance(block, Vector2.up));
        foreach (RectTransform block in fromBottom) if (block != null) topEntrances.Add(new HudEntrance(block, Vector2.down));
    }

    // Places every block for this moment of the reveal; returns when the last block has landed
    float PlaceEntrances(float elapsed)
    {
        float duration = Mathf.Max(0.01f, entranceSeconds);
        int order = 0;
        foreach (HudEntrance entrance in sideEntrances)
            entrance.Apply((elapsed - order++ * entranceStagger) / duration, entranceMargin, entranceOvershoot, entranceLean);
        foreach (HudEntrance entrance in topEntrances)
            entrance.Apply((elapsed - order++ * entranceStagger) / duration, entranceMargin, entranceOvershoot, entranceLean);
        return Mathf.Max(0, order - 1) * entranceStagger + duration;
    }

    // The blocks show on their own (they start off screen), while the rest of the HUD stays hidden
    void GiveBlocksTheirOwnFade()
    {
        RestoreBlockFades();
        foreach (RectTransform block in AllBlocks())
        {
            CanvasGroup group = block.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added) group = block.gameObject.AddComponent<CanvasGroup>();
            ownFades.Add(new OwnFade { group = group, ignoredBefore = group.ignoreParentGroups, added = added });
            group.ignoreParentGroups = true;
        }
    }

    void RestoreBlockFades()
    {
        foreach (OwnFade fade in ownFades)
        {
            if (fade.group == null) continue;
            if (fade.added) Destroy(fade.group);
            else fade.group.ignoreParentGroups = fade.ignoredBefore;
        }
        ownFades.Clear();
    }

    IEnumerable<RectTransform> AllBlocks()
    {
        foreach (RectTransform block in fromLeft) if (block != null) yield return block;
        foreach (RectTransform block in fromRight) if (block != null) yield return block;
        foreach (RectTransform block in fromTop) if (block != null) yield return block;
        foreach (RectTransform block in fromBottom) if (block != null) yield return block;
    }

    void ReturnEntrancesToRest()
    {
        foreach (HudEntrance entrance in sideEntrances) entrance.ReturnToRest();
        foreach (HudEntrance entrance in topEntrances) entrance.ReturnToRest();
    }

    void Celebrate()
    {
        if (cameraShaker != null) cameraShaker.AddShake(finishCameraBump);

        Camera cam = Camera.main;
        if (burst == null || cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(deckRoot.position);
        Color gold = theme != null ? theme.gold : Color.yellow;
        Color pale = theme != null ? theme.cream : Color.white;
        burst.Emit(new Vector3(screen.x, screen.y, 0f), finishSparks, gold, pale, 1.3f, 1.4f, 1.2f);
    }

    // ---------- Finish ----------

    // Everything back where it belongs and the game may go on. skipped: also the deck's landing punch.
    void FinishNow(bool skipped)
    {
        foreach (Flyer flyer in flyers)
            if (flyer.copy != null) Destroy(flyer.copy);
        flyers.Clear();

        if (stage != Stage.Idle)
        {
            PutDeckBack();
            if (deck != null)
            {
                deck.SetIntroFill(1f);
                if (skipped) deck.PlayIntroLanding(false, true);
            }
            SetHudAlpha(1f);
            ReturnEntrancesToRest();
            RestoreBlockFades();
        }
        stage = Stage.Idle;
        ReleaseGate();
    }

    void PutDeckBack()
    {
        if (deckRoot == null) return;
        deckRoot.localPosition = deckRestPosition;
        deckRoot.localScale = deckRestScale;
    }

    // ---------- HUD ----------

    // The HUD canvas fades as a whole; the effects layer ignores that, so sparks stay visible
    void HideHud()
    {
        if (hud == null) return;
        if (hudGroup == null) hudGroup = GetOrAdd<CanvasGroup>(hud.gameObject);
        if (burst != null)
        {
            CanvasGroup fx = GetOrAdd<CanvasGroup>(burst.gameObject);
            fx.ignoreParentGroups = true;
            fx.blocksRaycasts = false;
        }
        SetHudAlpha(0f);
    }

    // Plain null check: Unity's "missing component" placeholder is not a real null for the ?? operator
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T found = go.GetComponent<T>();
        return found != null ? found : go.AddComponent<T>();
    }

    void SetHudAlpha(float alpha)
    {
        if (hudGroup == null) return;
        hudGroup.alpha = alpha;
        hudGroup.blocksRaycasts = alpha >= 1f;
    }

    // ---------- Gate ----------

    void HoldGate()
    {
        if (holdingGate) return;
        holdingGate = true;
        PresentationGate.Hold();
    }

    void ReleaseGate()
    {
        if (!holdingGate) return;
        holdingGate = false;
        PresentationGate.Release();
    }
}
