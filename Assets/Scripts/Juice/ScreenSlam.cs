using System;
using TMPro;
using UnityEngine;

/// Big text that crashes onto the middle of the screen, holds for a beat, then flies into a spot on the HUD
/// (the hand name into the plaque, the hand's score into its total).
///   1. Slam: drops in from huge to full size, then an impact (fragments and a camera shake).
///   2. Hold: sits there pulsing, so it can be read.
///   3. Fly: shrinks into the target text's place; the caller is told when it lands.
/// Every slam has a drama level from 0 (High Card) to 1 (Royal Flush): the higher it is, the bigger,
/// longer and harder the slam. Each setting below has a value for drama 0 and one for drama 1.
/// The impact shakes the camera, the HUD and the hand (every ShakeOffset); an epic slam shakes again on landing.
/// Put it on the HUD's full-screen effects layer (HudFx). It copies the target text's font and colour.
[RequireComponent(typeof(RectTransform))]
public class ScreenSlam : MonoBehaviour
{
    [Header("Look (drama 0 → drama 1)")]
    public Vector2 fontSize = new Vector2(120f, 270f);
    [Tooltip("How much bigger than full size it starts")]
    public Vector2 startScale = new Vector2(2.5f, 7f);
    [Tooltip("Where it lands on screen, in canvas pixels from the middle")]
    public Vector2 screenOffset = new Vector2(0f, 80f);

    [Header("Timing in seconds (drama 0 → drama 1)")]
    [Tooltip("A big hand falls slower, so it hits heavier")]
    public Vector2 slamSeconds = new Vector2(0.14f, 0.26f);
    public Vector2 holdSeconds = new Vector2(0.35f, 1.2f);
    public float flySeconds = 0.3f;

    [Header("Impact (drama 0 → drama 1)")]
    public Vector2Int impactShards = new Vector2Int(16, 220);
    public Vector2 impactShardStrength = new Vector2(1f, 3f);
    [Tooltip("Fragment size compared with the burst layer's own setting")]
    public Vector2 impactShardSize = new Vector2(1f, 3.5f);
    [Tooltip("How long fragments live compared with the burst layer's own setting")]
    public Vector2 impactShardLife = new Vector2(1f, 1.8f);
    [Tooltip("Sparks thrown off every second while the text holds on screen")]
    public Vector2 holdSparksPerSecond = new Vector2(0f, 90f);
    [Tooltip("Camera shake energy (1 = a full collapse shake)")]
    public Vector2 cameraBump = new Vector2(0.12f, 1f);
    [Tooltip("HUD and hand shake energy (every ShakeOffset; 1 = full)")]
    public Vector2 hudAndHandShake = new Vector2(0.15f, 1f);
    [Tooltip("Size of the pulse while it holds")]
    public Vector2 holdPulse = new Vector2(0.03f, 0.12f);

    [Header("Epic hands")]
    [Tooltip("From this drama up (the default Flush and stronger), the screen shakes again when the text lands")]
    [Range(0f, 1f)] public float epicFrom = 0.55f;
    [Tooltip("Landing shake for an epic slam, at drama 1 (smaller epics shake less)")]
    [Range(0f, 1f)] public float epicLandingShake = 0.6f;

    [Header("Optional effects")]
    public UiShardBurst burst;
    public CameraShaker cameraShaker;
    [Tooltip("Makes the hand cards hop and rock on the impact")]
    public HandJuice handJuice;
    [Tooltip("Makes the deck hop, rock and flash red on the impact")]
    public DeckJuice deckJuice;

    enum Stage { Idle, Slam, Hold, Fly }

    /// One slam in flight. Two can overlap, so a new slam never cuts the old one short.
    class Slam
    {
        public TextMeshProUGUI text;
        public Stage stage = Stage.Idle;
        public float stageStart;
        public TMP_Text target;
        public Color accent;
        public float drama;
        public float sparkDebt;             // part-sparks carried over between frames
        public Action landed;
    }

    readonly Slam[] slams = new Slam[2];

    void Awake()
    {
        if (burst == null) burst = GetComponent<UiShardBurst>();
        if (cameraShaker == null) cameraShaker = FindAnyObjectByType<CameraShaker>();
        if (handJuice == null) handJuice = FindAnyObjectByType<HandJuice>();
        if (deckJuice == null) deckJuice = FindAnyObjectByType<DeckJuice>();
        for (int i = 0; i < slams.Length; i++) slams[i] = new Slam { text = MakeText(i) };
    }

    TextMeshProUGUI MakeText(int index)
    {
        var go = new GameObject("Slam" + index, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(1800f, 300f);
        go.SetActive(false);
        return text;
    }

    /// Slams `words` onto the screen, then flies it into `target`. Drama 0 = mild, 1 = as big as it gets.
    /// `landed` runs when it arrives (also if it is cut short), so the caller can show the real text again.
    public void Play(string words, TMP_Text target, Color accent, float drama, Action landed = null)
    {
        Slam slam = TakeFreeSlam();
        slam.landed?.Invoke();                      // a reused slam lands its old text at once

        slam.target = target;
        slam.accent = accent;
        slam.drama = Mathf.Clamp01(drama);
        slam.sparkDebt = 0f;
        slam.landed = landed;
        CopyLook(slam, words);
        slam.text.gameObject.SetActive(true);
        slam.text.transform.SetAsLastSibling();
        StartStage(slam, Stage.Slam);
    }

    Slam TakeFreeSlam()
    {
        foreach (Slam slam in slams)
            if (slam.stage == Stage.Idle) return slam;
        return slams[0];
    }

    void CopyLook(Slam slam, string words)
    {
        TextMeshProUGUI text = slam.text;
        if (slam.target != null)
        {
            text.font = slam.target.font;
            text.fontSharedMaterial = slam.target.fontSharedMaterial;
            text.color = slam.target.color;
            text.fontStyle = slam.target.fontStyle;
        }
        text.fontSize = ByDrama(fontSize, slam);
        text.text = words;
    }

    // A setting's value for this slam: between its drama-0 (x) and drama-1 (y) values
    static float ByDrama(Vector2 range, Slam slam) => Mathf.Lerp(range.x, range.y, slam.drama);

    void StartStage(Slam slam, Stage stage)
    {
        slam.stage = stage;
        slam.stageStart = Time.unscaledTime;
        if (stage == Stage.Hold) Impact(slam);
    }

    void Impact(Slam slam)
    {
        if (burst != null)
        {
            int shards = Mathf.RoundToInt(Mathf.Lerp(impactShards.x, impactShards.y, slam.drama));
            EmitAcrossText(slam, shards, ByDrama(impactShardStrength, slam));
        }
        ShakeEverything(ByDrama(cameraBump, slam), ByDrama(hudAndHandShake, slam));
        ShakeOffset.JoltAll(slam.drama);
        if (handJuice != null) handJuice.PlaySlamJump(slam.drama);
        if (deckJuice != null) deckJuice.PlaySlam(slam.drama);
    }

    // The camera (the world), and every ShakeOffset (the HUD blocks and the hand)
    void ShakeEverything(float cameraEnergy, float hudEnergy)
    {
        if (cameraShaker != null) cameraShaker.AddShake(cameraEnergy);
        ShakeOffset.ShakeAll(hudEnergy);
    }

    // Fragments from all along the words (a long name sprays from end to end), sized and timed by drama
    void EmitAcrossText(Slam slam, int count, float strength)
    {
        float size = ByDrama(impactShardSize, slam);
        float life = ByDrama(impactShardLife, slam);
        for (int i = 0; i < count; i++)
            burst.Emit(RandomPointOnText(slam), 1, slam.text.color, slam.accent, strength, size, life);
    }

    // Somewhere on the words as drawn this frame (their real width, not the whole text box)
    static Vector3 RandomPointOnText(Slam slam)
    {
        RectTransform rect = slam.text.rectTransform;
        Bounds words = slam.text.textBounds;
        if (words.size == Vector3.zero) return rect.position;

        var local = new Vector3(UnityEngine.Random.Range(words.min.x, words.max.x), UnityEngine.Random.Range(words.min.y, words.max.y), 0f);
        return rect.TransformPoint(local);
    }

    // While it holds: a steady spray of small sparks, none for a mild hand, a stream for an epic one
    void EmitHoldSparks(Slam slam)
    {
        if (burst == null) return;
        slam.sparkDebt += ByDrama(holdSparksPerSecond, slam) * Time.unscaledDeltaTime;
        int sparks = Mathf.FloorToInt(slam.sparkDebt);
        if (sparks <= 0) return;

        slam.sparkDebt -= sparks;
        float size = ByDrama(impactShardSize, slam) * 0.6f;
        for (int i = 0; i < sparks; i++)
            burst.Emit(RandomPointOnText(slam), 1, slam.text.color, slam.accent, 0.5f, size, 1f);
    }

    // An epic slam hits the HUD a second time as it lands
    void Aftershock(Slam slam)
    {
        if (slam.drama < epicFrom) return;
        float energy = epicLandingShake * slam.drama;
        ShakeEverything(energy, energy);
        ShakeOffset.JoltAll(slam.drama * 0.5f);
        if (burst != null && slam.target != null)
            burst.Emit(MiddleOf(slam.target.rectTransform), Mathf.RoundToInt(impactShards.y * 0.3f * slam.drama),
                       slam.text.color, slam.accent, 1.2f, ByDrama(impactShardSize, slam) * 0.7f, 1f);
    }

    void Update()
    {
        foreach (Slam slam in slams)
            if (slam != null && slam.stage != Stage.Idle) Step(slam);     // null: scripts reloaded mid-play
    }

    void Step(Slam slam)
    {
        float elapsed = Time.unscaledTime - slam.stageStart;
        switch (slam.stage)
        {
            case Stage.Slam: StepSlam(slam, elapsed); break;
            case Stage.Hold: StepHold(slam, elapsed); break;
            case Stage.Fly:  StepFly(slam, elapsed); break;
        }
    }

    // Falls from huge onto the screen, speeding up, like it is thrown at the glass
    void StepSlam(Slam slam, float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / ByDrama(slamSeconds, slam));
        float eased = t * t;
        Place(slam, CentreOfScreen(), Mathf.Lerp(ByDrama(startScale, slam), 1f, eased), t);
        if (t >= 1f) StartStage(slam, Stage.Hold);
    }

    void StepHold(Slam slam, float elapsed)
    {
        float hold = ByDrama(holdSeconds, slam);
        float pulse = 1f + Mathf.Sin(elapsed * 18f) * ByDrama(holdPulse, slam) * Mathf.Clamp01(1f - elapsed / hold);
        Place(slam, CentreOfScreen(), pulse, 1f);
        EmitHoldSparks(slam);
        if (elapsed >= hold) StartStage(slam, Stage.Fly);
    }

    // Shrinks into the target text's middle, ending at the target's text size
    void StepFly(Slam slam, float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / flySeconds);
        float eased = t * t * (3f - 2f * t);
        Vector3 end = slam.target != null ? MiddleOf(slam.target.rectTransform) : CentreOfScreen();
        float endScale = slam.target != null ? Mathf.Max(0.05f, slam.target.fontSize / slam.text.fontSize) : 0.2f;

        Place(slam, Vector3.Lerp(CentreOfScreen(), end, eased), Mathf.Lerp(1f, endScale, eased), 1f);
        if (t >= 1f)
        {
            Aftershock(slam);
            Land(slam);
        }
    }

    void Land(Slam slam)
    {
        slam.stage = Stage.Idle;
        slam.text.gameObject.SetActive(false);
        Action landed = slam.landed;
        slam.landed = null;
        landed?.Invoke();
    }

    void Place(Slam slam, Vector3 worldPosition, float scale, float alpha)
    {
        RectTransform rect = slam.text.rectTransform;
        rect.position = worldPosition;
        rect.localScale = Vector3.one * scale;
        slam.text.alpha = alpha;
    }

    Vector3 CentreOfScreen()
    {
        var layer = (RectTransform)transform;
        return layer.TransformPoint(layer.rect.center + screenOffset);
    }

    static Vector3 MiddleOf(RectTransform rect)
    {
        return rect.TransformPoint(rect.rect.center);
    }

    void OnDisable()
    {
        foreach (Slam slam in slams)
            if (slam != null && slam.stage != Stage.Idle) Land(slam);      // null: switched off before Awake ran
    }
}
