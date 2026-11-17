using System.Collections.Generic;
using UnityEngine;

/// Tower effect (C3): when a card breaks, shards of it burst out where it stood, tumble, bounce on the
/// table and shrink away, with a few specks of dust.
/// Put it on any object in the game scene. Listen-only: delete it and cards still break the same way.
public class BreakJuice : MonoBehaviour
{
    [Header("Shards")]
    public int shardsPerCard = 12;
    [Tooltip("Width and height of a shard, in world units")]
    public Vector2 shardSize = new Vector2(0.3f, 0.2f);
    [Range(0f, 1f)] public float shardSizeVariation = 0.5f;
    [Tooltip("How fast shards fly away from the card's middle")]
    public float shardSpeed = 3f;
    [Tooltip("Extra upward kick, so the burst pops up before it falls")]
    public float shardUpSpeed = 4f;
    [Tooltip("Fastest a shard tumbles, in degrees per second")]
    public float shardSpinDegrees = 720f;
    public float shardSeconds = 1.2f;
    public float gravity = 1.5f;
    [Tooltip("Shards bounce on the table and the tower instead of falling through")]
    public bool bounceOnTable = true;

    [Header("Shard colours")]
    public Color cardColor = new Color(0.96f, 0.92f, 0.82f);
    public Color redSuitColor = new Color(0.8f, 0.15f, 0.15f);
    public Color blackSuitColor = new Color(0.12f, 0.12f, 0.14f);
    [Tooltip("Share of shards in the suit's colour; the rest are the card's colour")]
    [Range(0f, 1f)] public float suitShardShare = 0.35f;

    [Header("Dust")]
    public int dustPerCard = 6;
    public float dustSize = 0.1f;
    public float dustSpeed = 1f;
    public float dustSeconds = 0.5f;
    public Color dustColor = new Color(0.75f, 0.7f, 0.62f);

    [Header("Limit")]
    [Tooltip("Most bursts in any one second, so a big collapse doesn't flood the screen")]
    public int maxBurstsPerSecond = 8;

    [Header("Look")]
    [Tooltip("Particle material (URP Particles/Unlit, culling off). Empty: one is made at start")]
    public Material particleMaterial;

    ParticleSystem shards;
    ParticleSystem dust;
    readonly Queue<float> recentBursts = new Queue<float>();

    void Awake()
    {
        Material material = particleMaterial != null ? particleMaterial : MakeFallbackMaterial();
        shards = ParticleBuilder.Create("Shards", transform, material, true, gravity, bounceOnTable);
        dust = ParticleBuilder.Create("Dust", transform, material, false, gravity * 0.1f, false);
    }

    void OnEnable() => GameEvents.CardBroken += OnCardBroken;
    void OnDisable() => GameEvents.CardBroken -= OnCardBroken;

    void OnCardBroken(Card card)
    {
        if (card == null || !TryTakeBurstSlot()) return;

        var box = card.GetComponent<BoxCollider>();
        if (box == null) return;

        Vector3 centre = card.transform.TransformPoint(box.center);
        Color suitColor = IsRedSuit(card) ? redSuitColor : blackSuitColor;

        for (int i = 0; i < shardsPerCard; i++) EmitShard(card.transform, box, centre, suitColor);
        for (int i = 0; i < dustPerCard; i++) EmitDust(centre);
    }

    // ---------- Limit ----------

    // A burst may play if fewer than the limit played in the last second
    bool TryTakeBurstSlot()
    {
        while (recentBursts.Count > 0 && Time.time - recentBursts.Peek() > 1f) recentBursts.Dequeue();
        if (recentBursts.Count >= maxBurstsPerSecond) return false;

        recentBursts.Enqueue(Time.time);
        return true;
    }

    // ---------- Shards ----------

    // One shard: starts somewhere on the card, flies out from the middle and up, tumbling
    void EmitShard(Transform card, BoxCollider box, Vector3 centre, Color suitColor)
    {
        Vector3 start = RandomPointOnCard(card, box);
        float size = 1f - Random.value * shardSizeVariation;

        var p = new ParticleSystem.EmitParams
        {
            position = start,
            velocity = OutwardVelocity(start, centre),
            startSize3D = new Vector3(shardSize.x * size, shardSize.y * size, 1f),
            rotation3D = Random.insideUnitSphere * 180f,
            angularVelocity3D = Random.insideUnitSphere * shardSpinDegrees,
            startLifetime = shardSeconds * Random.Range(0.75f, 1.1f),
            startColor = Random.value < suitShardShare ? suitColor : cardColor,
        };
        shards.Emit(p, 1);
    }

    // Anywhere inside the card's own (rotated) box
    static Vector3 RandomPointOnCard(Transform card, BoxCollider box)
    {
        var offset = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
        return card.TransformPoint(box.center + Vector3.Scale(offset, box.size));
    }

    Vector3 OutwardVelocity(Vector3 start, Vector3 centre)
    {
        Vector3 away = start - centre;
        if (away.sqrMagnitude < 0.0001f) away = Random.onUnitSphere;
        return away.normalized * shardSpeed * Random.Range(0.5f, 1f) +
               Vector3.up * shardUpSpeed * Random.Range(0.5f, 1.2f);
    }

    static bool IsRedSuit(Card card)
    {
        if (card.Data == null) return false;
        return card.Data.suit == CardSuit.Hearts || card.Data.suit == CardSuit.Diamonds;
    }

    // ---------- Dust ----------

    // A slow speck that drifts out and up from the middle
    void EmitDust(Vector3 centre)
    {
        Vector3 drift = Random.onUnitSphere;
        drift.y = Mathf.Abs(drift.y);

        var p = new ParticleSystem.EmitParams
        {
            position = centre + Random.insideUnitSphere * 0.1f,
            velocity = drift * dustSpeed * Random.Range(0.5f, 1f),
            startSize = dustSize * Random.Range(0.6f, 1.2f),
            startLifetime = dustSeconds * Random.Range(0.7f, 1.2f),
            startColor = dustColor,
        };
        dust.Emit(p, 1);
    }

    // ---------- Material ----------

    // Used only when no material is assigned (an empty Inspector slot should still show something)
    static Material MakeFallbackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = "BreakJuice (made at start)" };
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);    // shards show both sides
        return material;
    }
}

/// Builds an idle particle system that BreakJuice fills by hand with Emit (no emission, no shape),
/// so one system serves every broken card.
static class ParticleBuilder
{
    public static ParticleSystem Create(string name, Transform parent, Material material,
                                        bool tumblingShards, float gravity, bool collide)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // set up before it ever plays

        SetupMain(ps, gravity, tumblingShards);
        DisableAutoEmission(ps);
        ShrinkAtEnd(ps);
        if (collide) BounceOnWorld(ps);
        SetupRenderer(ps, material, tumblingShards);

        ps.Play();      // idle until Emit is called
        return ps;
    }

    static void SetupMain(ParticleSystem ps, float gravity, bool tumblingShards)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.gravityModifier = gravity;
        main.startSize3D = tumblingShards;
        main.startRotation3D = tumblingShards;
    }

    static void DisableAutoEmission(ParticleSystem ps)
    {
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
    }

    // Full size for most of its life, then shrinks to nothing (works with an opaque material)
    static void ShrinkAtEnd(ParticleSystem ps)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    static void BounceOnWorld(ParticleSystem ps)
    {
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.dampen = 0.4f;
        collision.bounce = 0.25f;
        collision.radiusScale = 0.5f;
    }

    // Shards are flat quads that tumble in 3D; dust faces the camera
    static void SetupRenderer(ParticleSystem ps, Material material, bool tumblingShards)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (!tumblingShards) return;
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        renderer.alignment = ParticleSystemRenderSpace.World;
    }
}
