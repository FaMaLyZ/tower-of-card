using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Pot sign effects (C3): when broken cards cost Pot, the sign jolts and its number flashes red.
/// On a collapse the sign shatters: a hard jolt, the bulbs pop off and fall, fragments burst out.
/// The bulbs come back when the next hand is drawn.
/// Put it on the PotSign object. Listen-only: delete it and the sign shows the same numbers.
public class PotSignJuice : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The sign's Visual wrapper: the jolt moves this")]
    public RectTransform visual;
    public TMP_Text number;
    public List<RectTransform> bulbs = new List<RectTransform>();
    public UiShardBurst burst;
    public HudTheme theme;

    [Header("Cards broke (Pot went down)")]
    public float lossShakePixels = 8f;
    public float lossShakeCycles = 4f;
    public float lossShakeSeconds = 0.35f;
    public float lossFlashSeconds = 0.6f;
    public int lossShards = 12;

    [Header("Collapse (Pot shattered)")]
    public float collapseShakePixels = 24f;
    public float collapseShakeCycles = 6f;
    public float collapseShakeSeconds = 0.6f;
    public float collapseFlashSeconds = 1.2f;
    public int collapseShards = 45;

    [Header("Bulbs popping off")]
    public float bulbPopSpeed = 380f;
    public float bulbGravity = 1500f;
    public float bulbSpinDegrees = 900f;
    [Tooltip("How long a popped bulb falls before it disappears")]
    public float bulbFallSeconds = 1.2f;
    [Tooltip("How long a bulb takes to grow back in with the next hand")]
    public float bulbReturnSeconds = 0.3f;

    struct Bulb
    {
        public RectTransform rect;
        public Vector2 restPosition;
        public Quaternion restRotation;
        public Vector2 velocity;
        public float spin;
    }

    enum BulbState { Resting, Popped, Returning }

    readonly List<Bulb> bulbParts = new List<Bulb>();
    BulbState bulbState = BulbState.Resting;
    float bulbStateStart;

    Vector2 visualRest;
    Color numberRestColor = Color.white;
    float shakeStart = -1f, shakePixels, shakeCycles, shakeSeconds;
    float flashStart = -1f, flashSeconds;

    void Awake()
    {
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();
        if (visual != null) visualRest = visual.anchoredPosition;
        if (number != null) numberRestColor = number.color;
        RememberBulbs();
    }

    void RememberBulbs()
    {
        foreach (RectTransform rect in bulbs)
        {
            if (rect == null) continue;
            bulbParts.Add(new Bulb { rect = rect, restPosition = rect.anchoredPosition, restRotation = rect.localRotation });
        }
    }

    void OnEnable()
    {
        GameEvents.PotChanged += OnPotChanged;
        GameEvents.TowerCollapsed += OnTowerCollapsed;
        GameEvents.PhaseChanged += OnPhaseChanged;
    }

    void OnDisable()
    {
        GameEvents.PotChanged -= OnPotChanged;
        GameEvents.TowerCollapsed -= OnTowerCollapsed;
        GameEvents.PhaseChanged -= OnPhaseChanged;
        ReturnToRest();
    }

    // ---------- Events ----------

    // Breaks that cost Pot without a collapse (a collapse plays the shatter instead)
    void OnPotChanged(long oldChips, long newChips, PotChangeReason reason, long bankQuote)
    {
        if (reason == PotChangeReason.ShakeResolved && newChips < oldChips) PlayLoss();
    }

    void OnTowerCollapsed(ShakeResult shake) => PlayShatter();

    void OnPhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Draw && bulbState == BulbState.Popped) StartBulbState(BulbState.Returning);
    }

    void PlayLoss()
    {
        StartShake(lossShakePixels, lossShakeCycles, lossShakeSeconds);
        StartFlash(lossFlashSeconds);
        EmitShards(lossShards, 1f);
    }

    void PlayShatter()
    {
        StartShake(collapseShakePixels, collapseShakeCycles, collapseShakeSeconds);
        StartFlash(collapseFlashSeconds);
        EmitShards(collapseShards, 1.8f);
        PopBulbs();
    }

    void StartShake(float pixels, float cycles, float seconds)
    {
        shakeStart = Time.unscaledTime;
        shakePixels = pixels;
        shakeCycles = cycles;
        shakeSeconds = seconds;
    }

    void StartFlash(float seconds)
    {
        flashStart = Time.unscaledTime;
        flashSeconds = seconds;
    }

    void EmitShards(int count, float strength)
    {
        if (burst == null || number == null) return;
        Color red = theme != null ? theme.danger : Color.red;
        Color gold = theme != null ? theme.gold : Color.yellow;
        burst.Emit(number.rectTransform.position, count, red, gold, strength);
    }

    // ---------- Bulbs ----------

    // Every bulb flies off outward from the sign's middle, spinning
    void PopBulbs()
    {
        for (int i = 0; i < bulbParts.Count; i++)
        {
            Bulb bulb = bulbParts[i];
            Vector2 away = bulb.restPosition.sqrMagnitude > 0.01f ? bulb.restPosition.normalized : Random.insideUnitCircle.normalized;
            bulb.velocity = (away + Random.insideUnitCircle * 0.4f) * bulbPopSpeed * Random.Range(0.6f, 1.2f) +
                            Vector2.up * bulbPopSpeed * 0.5f;
            bulb.spin = Random.Range(-1f, 1f) * bulbSpinDegrees;
            bulbParts[i] = bulb;
        }
        StartBulbState(BulbState.Popped);
    }

    void StartBulbState(BulbState state)
    {
        bulbState = state;
        bulbStateStart = Time.unscaledTime;
    }

    void UpdateBulbs()
    {
        float elapsed = Time.unscaledTime - bulbStateStart;
        if (bulbState == BulbState.Popped) FlyBulbs(elapsed);
        else if (bulbState == BulbState.Returning) GrowBulbsBack(elapsed);
    }

    // Falling under gravity from where they started; hidden once they have had time to drop off screen
    void FlyBulbs(float elapsed)
    {
        bool gone = elapsed >= bulbFallSeconds;
        foreach (Bulb bulb in bulbParts)
        {
            bulb.rect.gameObject.SetActive(!gone);
            if (gone) continue;

            Vector2 fall = bulb.velocity * elapsed + 0.5f * bulbGravity * elapsed * elapsed * Vector2.down;
            bulb.rect.anchoredPosition = bulb.restPosition + fall;
            bulb.rect.localRotation = bulb.restRotation * Quaternion.Euler(0f, 0f, bulb.spin * elapsed);
        }
    }

    void GrowBulbsBack(float elapsed)
    {
        float t = bulbReturnSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / bulbReturnSeconds);
        foreach (Bulb bulb in bulbParts)
        {
            bulb.rect.gameObject.SetActive(true);
            bulb.rect.anchoredPosition = bulb.restPosition;
            bulb.rect.localRotation = bulb.restRotation;
            bulb.rect.localScale = Vector3.one * t;
        }
        if (t >= 1f) bulbState = BulbState.Resting;
    }

    // ---------- Every frame ----------

    void LateUpdate()
    {
        ApplyShake();
        ApplyFlash();
        UpdateBulbs();
    }

    void ApplyShake()
    {
        if (visual == null) return;

        float offset = 0f;
        if (shakeStart >= 0f)
        {
            float t = (Time.unscaledTime - shakeStart) / shakeSeconds;
            if (t >= 1f) shakeStart = -1f;
            else offset = Mathf.Sin(t * shakeCycles * Mathf.PI * 2f) * (1f - t) * shakePixels;
        }
        visual.anchoredPosition = visualRest + new Vector2(offset, 0f);
    }

    // Red at once, fading back to the number's own colour
    void ApplyFlash()
    {
        if (number == null || flashStart < 0f) return;

        float t = (Time.unscaledTime - flashStart) / flashSeconds;
        if (t >= 1f) flashStart = -1f;
        Color red = theme != null ? theme.danger : Color.red;
        number.color = Color.Lerp(red, numberRestColor, Mathf.Clamp01(t));
    }

    void ReturnToRest()
    {
        shakeStart = flashStart = -1f;
        if (visual != null) visual.anchoredPosition = visualRest;
        if (number != null) number.color = numberRestColor;
        bulbState = BulbState.Resting;
        foreach (Bulb bulb in bulbParts)
        {
            if (bulb.rect == null) continue;
            bulb.rect.gameObject.SetActive(true);
            bulb.rect.anchoredPosition = bulb.restPosition;
            bulb.rect.localRotation = bulb.restRotation;
            bulb.rect.localScale = Vector3.one;
        }
    }
}
