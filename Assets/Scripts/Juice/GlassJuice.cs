using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Glass effects (C7):
///   Crack: the black crystal stays a moment while red shards appear in a ring around it and fly in from
///          every direction. As they meet, the red crystal grows out over the black one (which fades away
///          underneath), with a flash of fragments, a HUD hop and a camera shake.
///   Repair (a Boss win): the red crystal wobbles harder and harder, then breaks into red shards that fly out
///          in every direction, and the black crystal is simply there underneath, as if it always was.
/// GlassStarsView shows the crystals; this asks it to keep a crystal's old look until its moment comes.
/// Put it on the Glass object. Listen-only: delete it and the crystals simply change at once.
public class GlassJuice : MonoBehaviour
{
    [Header("Parts")]
    public GlassStarsView view;
    [Tooltip("The crystals, left to right (the same images as GlassStarsView)")]
    public List<Image> stars = new List<Image>();
    [Tooltip("Shard pictures that fly in; each shard picks one")]
    public List<Sprite> shardSprites = new List<Sprite>();
    public UiShardBurst burst;
    public CameraShaker cameraShaker;
    public HudTheme theme;

    [Header("Shards flying in")]
    [Range(3, 16)] public int shardCount = 8;
    [Tooltip("How far out the ring of shards starts, in canvas pixels")]
    public float startDistance = 150f;
    [Tooltip("Shard length in canvas pixels")]
    public float shardSize = 50f;
    [Tooltip("Time for the shards to appear in their ring")]
    public float appearSeconds = 0.12f;
    [Tooltip("Time from the ring to the middle; they speed up as they go")]
    public float flySeconds = 0.35f;
    [Tooltip("Shards leave the ring a little apart, up to this many seconds")]
    public float maxDelay = 0.06f;
    [Tooltip("Turn while flying in (0 = straight in, pointing at the middle the whole way)")]
    public float shardSpinDegrees = 0f;

    [Header("Red crystal forming")]
    [Tooltip("How long the red crystal takes to grow out over the black one")]
    public float formSeconds = 0.3f;
    [Tooltip("How far past full size it grows before settling (0.25 = 125%)")]
    public float formOvershoot = 0.25f;
    public int flashFragments = 18;
    [Range(0f, 1f)] public float hudJolt = 0.55f;
    [Range(0f, 1f)] public float cameraShake = 0.4f;

    [Header("At a blind's end")]
    [Tooltip("When a blind is failed (or a Boss won) the crack or heal waits this long, so the FAILED / WON! " +
             "slam lands first")]
    public float delayAtBlindEnd = 0.9f;

    [Header("Repair: wobble, then break apart")]
    [Tooltip("How long the red crystal wobbles before it breaks")]
    public float wobbleSeconds = 0.55f;
    [Tooltip("Widest rock just before it breaks, in degrees")]
    public float wobbleDegrees = 18f;
    [Tooltip("Rocks back and forth this many times while wobbling")]
    public float wobbleCycles = 4f;
    [Tooltip("How much it swells as the wobble builds (0.1 = 110%)")]
    public float wobbleSwell = 0.1f;
    [Tooltip("How far the red shards fly out, in canvas pixels")]
    public float breakDistance = 170f;
    public float breakSeconds = 0.45f;
    public int repairSparkles = 16;
    [Range(0f, 1f)] public float repairHudJolt = 0.3f;

    /// One shard flying in toward a crystal (a crack) or out from it (a repair)
    class Incoming
    {
        public Image image;
        public bool alive;
        public bool outward;
        public int star;
        public float start;
        public Vector2 fromOffset;      // in: where it starts; out: where it ends (from the crystal's middle, in pixels)
        public float baseAngle;
    }

    /// One crystal being healed: wobbles, then breaks into shards over the black crystal
    class Healing
    {
        public int star;
        public float start;
    }

    /// One crystal being formed: waits for its shards, then grows over a fading black copy
    class Forming
    {
        public int star;
        public float shardsLand;        // when the last shard arrives
        public float formStart = -1f;
        public Image blackGhost;
    }

    readonly List<Incoming> incoming = new List<Incoming>();
    readonly List<Forming> forming = new List<Forming>();
    readonly List<Healing> healing = new List<Healing>();
    readonly List<UiPunch> punches = new List<UiPunch>();
    RectTransform layer;

    void Awake()
    {
        if (view == null) view = GetComponent<GlassStarsView>();
        if (burst == null) burst = FindAnyObjectByType<UiShardBurst>();
        if (cameraShaker == null) cameraShaker = FindAnyObjectByType<CameraShaker>();
        layer = burst != null ? burst.transform as RectTransform : transform as RectTransform;
        foreach (Image star in stars) punches.Add(star != null ? new UiPunch(star.rectTransform) : null);
    }

    void OnEnable() => GameEvents.GlassChanged += OnGlassChanged;

    void OnDisable()
    {
        GameEvents.GlassChanged -= OnGlassChanged;
        FinishEverything();
    }

    // Crystals are whole from the left, so the ones between the new and old life counts are the ones that changed
    void OnGlassChanged(int oldLives, int newLives, GlassChangeReason reason)
    {
        bool blindEnd = reason == GlassChangeReason.BlindFailed || reason == GlassChangeReason.BossWon;
        float delay = blindEnd ? delayAtBlindEnd : 0f;
        for (int i = newLives; i < oldLives; i++) PlayCrack(i, delay);
        for (int i = oldLives; i < newLives; i++) PlayRepair(i, delay);
    }

    // ---------- Crack ----------

    /// The red shards fly in and form the red crystal, after `delay` seconds (public for the juice debug panel)
    public void PlayCrack(int index, float delay = 0f)
    {
        if (!IsStar(index)) return;
        if (view != null) view.HoldWhole(index);
        PlaySoundAfter(SoundType.GlassCrack, delay);

        float now = Time.unscaledTime + delay;
        float lastLanding = now;
        for (int i = 0; i < shardCount; i++)
        {
            float angle = (360f / shardCount) * i + Random.Range(-12f, 12f);
            float spread = Random.Range(0f, maxDelay);
            SpawnShard(index, angle, now + spread, false, startDistance);
            lastLanding = Mathf.Max(lastLanding, now + spread + appearSeconds + flySeconds);
        }
        forming.Add(new Forming { star = index, shardsLand = lastLanding });
    }

    void SpawnShard(int star, float angle, float start, bool outward, float distance)
    {
        Incoming shard = TakeShard();
        shard.alive = true;
        shard.outward = outward;
        shard.star = star;
        shard.start = start;
        shard.baseAngle = angle;
        shard.fromOffset = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right) * distance * Random.Range(0.85f, 1.15f);

        shard.image.sprite = shardSprites.Count > 0 ? shardSprites[Random.Range(0, shardSprites.Count)] : null;
        shard.image.color = theme != null ? theme.mult : Color.red;     // full alpha again (outward ones fade it)
        shard.image.rectTransform.sizeDelta = ShardSize(shard.image.sprite);
        shard.image.transform.SetAsLastSibling();
        shard.image.gameObject.SetActive(false);        // shown when its moment comes
    }

    // In its ring it grows in, then it flies straight to the middle, speeding up, its point aimed at the middle
    void StepShard(Incoming shard)
    {
        float age = Time.unscaledTime - shard.start;
        if (age < 0f) return;

        Image star = stars[shard.star];
        if (star == null) { Hide(shard); return; }
        shard.image.gameObject.SetActive(true);
        if (shard.outward) { StepShardOut(shard, age); return; }

        Vector3 middle = MiddleOf(shard.star);
        RectTransform rect = shard.image.rectTransform;
        float pointInward = shard.baseAngle + 90f;      // the shard pictures point up; turn them toward the middle

        if (age < appearSeconds)
        {
            float t = age / appearSeconds;
            rect.position = middle + Pixels(shard.fromOffset);
            rect.localRotation = Quaternion.Euler(0f, 0f, pointInward);
            rect.localScale = Vector3.one * (1f - (1f - t) * (1f - t));
            return;
        }

        float f = (age - appearSeconds) / Mathf.Max(0.01f, flySeconds);
        if (f >= 1f) { Hide(shard); return; }

        float eased = f * f;
        rect.position = middle + Pixels(shard.fromOffset * (1f - eased));
        rect.localRotation = Quaternion.Euler(0f, 0f, pointInward + shardSpinDegrees * eased);
        rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, eased);
    }

    // From near the middle straight out, slowing down, point first; fades away over the last 40%
    void StepShardOut(Incoming shard, float age)
    {
        float t = age / Mathf.Max(0.01f, breakSeconds);
        if (t >= 1f) { Hide(shard); return; }

        float eased = 1f - (1f - t) * (1f - t);
        RectTransform rect = shard.image.rectTransform;
        rect.position = MiddleOf(shard.star) + Pixels(shard.fromOffset * Mathf.Lerp(0.2f, 1f, eased));
        rect.localRotation = Quaternion.Euler(0f, 0f, shard.baseAngle - 90f);
        rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, eased);

        Color color = shard.image.color;
        color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        shard.image.color = color;
    }

    // ---------- Red crystal forming ----------

    void StepForming(Forming form)
    {
        float now = Time.unscaledTime;
        if (form.formStart < 0f)
        {
            if (now < form.shardsLand) return;
            BeginForming(form);
        }

        float t = (now - form.formStart) / Mathf.Max(0.01f, formSeconds);
        RectTransform starRect = stars[form.star].rectTransform;
        if (t >= 1f)
        {
            starRect.localScale = Vector3.one;
            RemoveGhost(form);
            form.star = -1;                             // done
            return;
        }

        // Grows from nothing past full size, then settles; the black copy underneath fades out
        float grow = t < 0.6f ? Mathf.Lerp(0f, 1f + formOvershoot, t / 0.6f) : Mathf.Lerp(1f + formOvershoot, 1f, (t - 0.6f) / 0.4f);
        starRect.localScale = Vector3.one * grow;
        if (form.blackGhost != null) form.blackGhost.color = new Color(1f, 1f, 1f, 1f - t);
    }

    // The shards have met: the red crystal takes over, with a black copy left underneath to fade
    void BeginForming(Forming form)
    {
        form.formStart = Time.unscaledTime;
        form.blackGhost = MakeBlackGhost(form.star);
        if (view != null) view.ReleaseWhole(form.star);

        Color red = theme != null ? theme.mult : Color.red;
        Color pale = theme != null ? theme.cream : Color.white;
        if (burst != null) burst.Emit(MiddleOf(form.star), flashFragments, red, pale, 1.3f, 1.2f, 0.9f);
        ShakeOffset.JoltAll(hudJolt);
        if (cameraShaker != null) cameraShaker.AddShake(cameraShake);
    }

    // A copy of the black crystal, placed just behind the star so the red one grows over it
    Image MakeBlackGhost(int index)
    {
        Image star = stars[index];
        var go = new GameObject("BlackGhost", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(star.transform.parent, false);
        go.transform.SetSiblingIndex(star.transform.GetSiblingIndex());

        var ghost = go.GetComponent<Image>();
        ghost.raycastTarget = false;
        ghost.preserveAspect = star.preserveAspect;
        ghost.sprite = view != null ? view.CurrentWholeSprite(index) : star.sprite;

        RectTransform from = star.rectTransform, to = ghost.rectTransform;
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta;
        to.anchoredPosition = from.anchoredPosition;
        return ghost;
    }

    static void RemoveGhost(Forming form)
    {
        if (form.blackGhost != null) Destroy(form.blackGhost.gameObject);
        form.blackGhost = null;
    }

    // ---------- Repair ----------

    /// The heal: wobble, then break apart over the black crystal, after `delay` seconds (public for the debug panel)
    public void PlayRepair(int index, float delay = 0f)
    {
        if (!IsStar(index)) return;
        if (view != null) view.HoldCracked(index);
        healing.Add(new Healing { star = index, start = Time.unscaledTime + delay });
        PlaySoundAfter(SoundType.GlassRepair, delay);
    }

    // The sound starts with its effect: right away, or after the same delay (real time, like the effects)
    void PlaySoundAfter(SoundType sound, float delay)
    {
        if (delay <= 0f || !isActiveAndEnabled) AudioManager.Instance.PlayOneShotSFX(sound);
        else StartCoroutine(PlaySoundLater(sound, delay));
    }

    IEnumerator PlaySoundLater(SoundType sound, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        AudioManager.Instance.PlayOneShotSFX(sound);
    }

    /// The run is lost: every crystal blows apart into red shards at once, with a big shake (RunEndJuice)
    public void ShatterAll()
    {
        AudioManager.Instance.PlayOneShotSFX(SoundType.GlassShatterAll);
        for (int i = 0; i < stars.Count; i++)
        {
            if (!IsStar(i)) continue;
            float now = Time.unscaledTime;
            for (int s = 0; s < shardCount * 2; s++)
                SpawnShard(i, (360f / (shardCount * 2)) * s + Random.Range(-10f, 10f), now, true, breakDistance * 1.6f);
            Color red = theme != null ? theme.mult : Color.red;
            if (burst != null) burst.Emit(MiddleOf(i), flashFragments, red, Color.white, 2f, 2f, 1.3f);
        }
        ShakeOffset.JoltAll(1f);
        if (cameraShaker != null) cameraShaker.AddShake(1f);
    }

    // Rocks back and forth, wider and swelling as it goes, like it is about to burst. True when it has broken.
    bool StepHealing(Healing heal)
    {
        float t = (Time.unscaledTime - heal.start) / Mathf.Max(0.01f, wobbleSeconds);
        if (t < 0f) return false;                       // still waiting for its moment
        RectTransform rect = stars[heal.star].rectTransform;
        if (t >= 1f)
        {
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            BreakApart(heal.star);
            return true;
        }

        float rock = Mathf.Sin(t * wobbleCycles * 2f * Mathf.PI);
        rect.localRotation = Quaternion.Euler(0f, 0f, wobbleDegrees * rock * t);
        rect.localScale = Vector3.one * (1f + wobbleSwell * t * Mathf.Abs(rock));
        return false;
    }

    // The red look goes, the black crystal is there at once, and the red shards fly out from where it was
    void BreakApart(int index)
    {
        if (view != null) view.ReleaseCracked(index);

        float now = Time.unscaledTime;
        for (int i = 0; i < shardCount; i++)
            SpawnShard(index, (360f / shardCount) * i + Random.Range(-12f, 12f), now, true, breakDistance);

        Color pale = theme != null ? theme.cream : Color.white;
        Color red = theme != null ? theme.mult : Color.red;
        if (burst != null) burst.Emit(MiddleOf(index), repairSparkles, pale, red, 1f, 1.2f, 0.9f);
        ShakeOffset.JoltAll(repairHudJolt);
    }

    // ---------- Every frame ----------

    void LateUpdate()
    {
        foreach (Incoming shard in incoming)
            if (shard.alive) StepShard(shard);

        for (int i = healing.Count - 1; i >= 0; i--)
            if (!IsStar(healing[i].star) || StepHealing(healing[i])) healing.RemoveAt(i);

        for (int i = forming.Count - 1; i >= 0; i--)
        {
            if (forming[i].star >= 0 && IsStar(forming[i].star)) StepForming(forming[i]);
            if (forming[i].star < 0) forming.RemoveAt(i);
        }

        // A punch only plays on a crystal that is not forming or wobbling (those set its pose themselves)
        for (int i = 0; i < punches.Count; i++)
            if (!IsForming(i) && !IsHealing(i)) punches[i]?.Apply();
    }

    bool IsForming(int index)
    {
        foreach (Forming form in forming)
            if (form.star == index && form.formStart >= 0f) return true;
        return false;
    }

    bool IsHealing(int index)
    {
        foreach (Healing heal in healing)
            if (heal.star == index) return true;
        return false;
    }

    // Switched off mid-effect: crystals back to normal, nothing left flying
    void FinishEverything()
    {
        foreach (Incoming shard in incoming) Hide(shard);
        foreach (Forming form in forming)
        {
            if (form.star < 0) continue;
            if (view != null) view.ReleaseWhole(form.star);
            if (IsStar(form.star)) stars[form.star].rectTransform.localScale = Vector3.one;
            RemoveGhost(form);
        }
        forming.Clear();
        foreach (Healing heal in healing)
        {
            if (view != null) view.ReleaseCracked(heal.star);
            if (!IsStar(heal.star)) continue;
            stars[heal.star].rectTransform.localRotation = Quaternion.identity;
            stars[heal.star].rectTransform.localScale = Vector3.one;
        }
        healing.Clear();
        foreach (UiPunch punch in punches) punch?.ReturnToRest();
    }

    // ---------- Helpers ----------

    Incoming TakeShard()
    {
        foreach (Incoming shard in incoming)
            if (!shard.alive) return shard;

        var go = new GameObject("GlassShard", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(layer, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        var created = new Incoming { image = image };
        incoming.Add(created);
        return created;
    }

    static void Hide(Incoming shard)
    {
        shard.alive = false;
        if (shard.image != null) shard.image.gameObject.SetActive(false);
    }

    Vector2 ShardSize(Sprite sprite)
    {
        if (sprite == null || sprite.rect.height <= 0f) return new Vector2(shardSize * 0.4f, shardSize);
        return new Vector2(shardSize * sprite.rect.width / sprite.rect.height, shardSize);
    }

    // Canvas pixels to world units on the effects layer (the canvas may be scaled)
    Vector3 Pixels(Vector2 pixels) => layer.TransformVector(pixels);

    bool IsStar(int index) => index >= 0 && index < stars.Count && stars[index] != null;

    Vector3 MiddleOf(int index)
    {
        RectTransform rect = stars[index].rectTransform;
        return rect.TransformPoint(rect.rect.center);
    }
}
