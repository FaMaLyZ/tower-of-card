using UnityEngine;
using UnityEngine.SceneManagement;

/// The swirling skybox follows the run: each blind has its own colours and speed, and every Ante spins
/// a little faster. Colours glide over and the speed speeds up or slows down gradually.
///   Small: calm blue.  Big: purple and amber.  Boss: crimson.
///   Run end: gold when cleared, dark red and slow when shattered.
/// Adds itself to any scene whose skybox has the swirl settings (_Color1-3, _SpinSpeed, _SwirlAmount, _Contrast).
/// Works on a COPY of the skybox material, so the material asset is never changed.
/// Listen-only: delete it and the sky just spins as the material says.
public class BackgroundMood : MonoBehaviour
{
    /// One look of the sky. Keep the three colours apart (dark / mid / bright) so the layers read clearly.
    [System.Serializable]
    public class Mood
    {
        [Tooltip("Mid tone")] public Color color1;
        [Tooltip("Bright tone")] public Color color2;
        [Tooltip("Dark tone")] public Color color3;
        [Range(0f, 2f)] public float swirl = 0.5f;
        [Tooltip("Spin compared with the material's own speed (1 = unchanged)")]
        public float spin = 1f;

        public Mood(Color color1, Color color2, Color color3, float swirl, float spin)
        {
            this.color1 = color1; this.color2 = color2; this.color3 = color3;
            this.swirl = swirl; this.spin = spin;
        }
    }

    [Header("Moods")]
    public Mood small = new Mood(new Color(0.10f, 0.20f, 0.38f), new Color(0.35f, 0.65f, 0.85f), new Color(0.03f, 0.05f, 0.12f), 0.5f, 1f);
    public Mood big = new Mood(new Color(0.32f, 0.14f, 0.42f), new Color(0.95f, 0.60f, 0.25f), new Color(0.08f, 0.04f, 0.12f), 0.6f, 1.2f);
    public Mood boss = new Mood(new Color(0.45f, 0.06f, 0.08f), new Color(0.95f, 0.35f, 0.15f), new Color(0.06f, 0.01f, 0.02f), 0.8f, 1.5f);
    public Mood cleared = new Mood(new Color(0.30f, 0.10f, 0.28f), new Color(1.00f, 0.80f, 0.30f), new Color(0.10f, 0.05f, 0.02f), 0.6f, 1.2f);
    public Mood shattered = new Mood(new Color(0.25f, 0.03f, 0.05f), new Color(0.55f, 0.08f, 0.10f), new Color(0.02f, 0.00f, 0.01f), 0.3f, 0.4f);

    [Header("Look")]
    [Tooltip("Higher = sharper edges between the colour layers (the material's own value was 2)")]
    public float contrast = 3.5f;

    [Header("Easing")]
    [Tooltip("How fast colours and swirl glide to a new mood (higher = quicker)")]
    public float moodSpeed = 1.5f;
    [Tooltip("Seconds the spin takes to settle on a new speed (the sky speeds up or slows down gradually)")]
    public float spinChangeSeconds = 2.5f;

    [Header("Ante")]
    [Tooltip("Extra spin per Ante after the first (0.35 = Ante 4 spins about twice as fast)")]
    public float spinPerAnte = 0.35f;

    static readonly int Color1Id = Shader.PropertyToID("_Color1");
    static readonly int Color2Id = Shader.PropertyToID("_Color2");
    static readonly int Color3Id = Shader.PropertyToID("_Color3");
    static readonly int SpinId = Shader.PropertyToID("_SpinSpeed");
    static readonly int SwirlId = Shader.PropertyToID("_SwirlAmount");
    static readonly int ContrastId = Shader.PropertyToID("_Contrast");

    Material sky;                   // our copy of the skybox
    Mood start;                     // the material's own look (the menu keeps it)
    Mood target;
    Color color1, color2, color3;   // what is shown now
    float swirl;
    float baseSpin;                 // the material's own spin speed
    float spinNow, spinVelocity;
    float phase;                    // the spin so far (see ApplySpin)
    int ante = 1;

    // ---------- Set-up ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddToSwirlScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => AddIfSwirlSky();
        AddIfSwirlSky();
    }

    static void AddIfSwirlSky()
    {
        if (FindAnyObjectByType<BackgroundMood>() != null) return;
        if (!IsSwirlSky(RenderSettings.skybox)) return;
        new GameObject("BackgroundMood").AddComponent<BackgroundMood>();
    }

    static bool IsSwirlSky(Material material)
    {
        return material != null && material.HasProperty(Color1Id) && material.HasProperty(Color2Id) &&
               material.HasProperty(Color3Id) && material.HasProperty(SpinId) && material.HasProperty(SwirlId);
    }

    void Awake()
    {
        if (!IsSwirlSky(RenderSettings.skybox))
        {
            enabled = false;
            return;
        }
        sky = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (mood)" };
        RenderSettings.skybox = sky;

        baseSpin = sky.GetFloat(SpinId);
        start = new Mood(sky.GetColor(Color1Id), sky.GetColor(Color2Id), sky.GetColor(Color3Id), sky.GetFloat(SwirlId), 1f);
        ShowAtOnce(start);
        spinNow = baseSpin;
        phase = baseSpin * Time.timeSinceLevelLoad;       // carry on from where the sky already is
    }

    void OnEnable()
    {
        GameEvents.BlindOffered += OnBlindOffered;
        GameEvents.BlindStarted += OnBlindStarted;
        GameEvents.RunEnded += OnRunEnded;
    }

    void OnDisable()
    {
        GameEvents.BlindOffered -= OnBlindOffered;
        GameEvents.BlindStarted -= OnBlindStarted;
        GameEvents.RunEnded -= OnRunEnded;
    }

    void OnDestroy()
    {
        if (sky != null) Destroy(sky);
    }

    // ---------- Game events ----------

    void OnBlindOffered(BlindKind kind, int ante, long target) => SetBlind(kind, ante);
    void OnBlindStarted(BlindKind kind, int ante, long target) => SetBlind(kind, ante);
    void OnRunEnded(long runTotal, bool wasCleared) => SetMood(wasCleared ? cleared : shattered);

    // ---------- What the sky can do (public for the juice debug panel) ----------

    public void SetBlind(BlindKind kind, int ante)
    {
        this.ante = Mathf.Max(1, ante);
        SetMood(MoodOf(kind));
    }

    public void SetMood(Mood mood)
    {
        if (mood != null) target = mood;
    }

    Mood MoodOf(BlindKind kind)
    {
        switch (kind)
        {
            case BlindKind.Small: return small;
            case BlindKind.Big:   return big;
            default:              return boss;
        }
    }

    // ---------- Every frame ----------

    void Update()
    {
        if (sky == null) return;
        EaseTowardMood();
        ApplyLook();
        ApplySpin();
    }

    void EaseTowardMood()
    {
        Mood goal = target ?? start;
        float ease = 1f - Mathf.Exp(-moodSpeed * Time.deltaTime);
        color1 = Color.Lerp(color1, goal.color1, ease);
        color2 = Color.Lerp(color2, goal.color2, ease);
        color3 = Color.Lerp(color3, goal.color3, ease);
        swirl = Mathf.Lerp(swirl, goal.swirl, ease);
    }

    void ApplyLook()
    {
        sky.SetColor(Color1Id, color1);
        sky.SetColor(Color2Id, color2);
        sky.SetColor(Color3Id, color3);
        sky.SetFloat(SwirlId, swirl);
        if (sky.HasProperty(ContrastId)) sky.SetFloat(ContrastId, contrast);
    }

    // The shader spins by (time x _SpinSpeed). Changing _SpinSpeed directly would make the whole swirl jump,
    // so we add up the spin ourselves (phase) and set _SpinSpeed = phase / time: the shader then lands exactly
    // on our phase. SmoothDamp eases the speed in and out, so a change feels like the sky gathering pace.
    void ApplySpin()
    {
        spinNow = Mathf.SmoothDamp(spinNow, GetTargetSpin(), ref spinVelocity, Mathf.Max(0.01f, spinChangeSeconds * 0.5f));
        phase += spinNow * Time.deltaTime;
        sky.SetFloat(SpinId, phase / Mathf.Max(0.001f, Time.timeSinceLevelLoad));
    }

    float GetTargetSpin()
    {
        Mood goal = target ?? start;
        return baseSpin * goal.spin * (1f + spinPerAnte * (ante - 1));
    }

    void ShowAtOnce(Mood mood)
    {
        color1 = mood.color1;
        color2 = mood.color2;
        color3 = mood.color3;
        swirl = mood.swirl;
    }
}
