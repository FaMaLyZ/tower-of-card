using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// The HUD's version of BreakJuice: little flat fragments that burst out, fall and shrink away.
/// Put it on a full-screen child of the HUD canvas, last in the list so fragments draw on top.
/// Other juice calls Emit; with this missing, those effects simply play without fragments.
[RequireComponent(typeof(RectTransform))]
public class UiShardBurst : MonoBehaviour
{
    [Tooltip("Fragment width and height, in canvas pixels")]
    public Vector2 size = new Vector2(9f, 6f);
    [Tooltip("How fast fragments fly out, in canvas pixels per second")]
    public float speed = 260f;
    [Tooltip("Extra upward kick, so the burst pops up before it falls")]
    public float upSpeed = 200f;
    public float gravity = 900f;
    public float spinDegrees = 720f;
    public float seconds = 0.6f;
    [Tooltip("Fragments kept ready; when all are in flight the oldest is reused")]
    public int poolSize = 400;

    class Shard
    {
        public RectTransform rect;
        public Image image;
        public Vector2 velocity;
        public float spin;
        public float start;
        public float life;
        public bool alive;
    }

    readonly List<Shard> shards = new List<Shard>();
    int next;

    void Awake()
    {
        for (int i = 0; i < poolSize; i++) shards.Add(MakeShard(i));
    }

    Shard MakeShard(int index)
    {
        var go = new GameObject("Shard" + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;            // fragments never block clicks
        go.SetActive(false);
        return new Shard { rect = (RectTransform)go.transform, image = image };
    }

    /// Bursts `count` fragments from a point on the canvas, half in each colour.
    /// Strength scales their speed (1 = normal, 2 = a big blast); sizeScale and lifeScale make them
    /// bigger and longer-lived (1 = the settings above).
    public void Emit(Vector3 worldPosition, int count, Color colorA, Color colorB, float strength = 1f,
                     float sizeScale = 1f, float lifeScale = 1f)
    {
        if (shards.Count == 0) return;
        for (int i = 0; i < count; i++)
            Launch(TakeShard(), worldPosition, Random.value < 0.5f ? colorA : colorB, strength, sizeScale, lifeScale);
    }

    Shard TakeShard()
    {
        Shard shard = shards[next];
        next = (next + 1) % shards.Count;
        return shard;
    }

    void Launch(Shard shard, Vector3 worldPosition, Color color, float strength, float sizeScale, float lifeScale)
    {
        shard.rect.position = worldPosition;
        shard.rect.sizeDelta = size * sizeScale * Random.Range(0.6f, 1.3f);
        shard.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        shard.rect.localScale = Vector3.one;
        shard.image.color = color;

        shard.velocity = Random.insideUnitCircle.normalized * speed * strength * Random.Range(0.4f, 1f) +
                         Vector2.up * upSpeed * strength * Random.Range(0.5f, 1.2f);
        shard.spin = Random.Range(-1f, 1f) * spinDegrees;
        shard.start = Time.unscaledTime;
        shard.life = seconds * lifeScale * Random.Range(0.75f, 1.15f);
        shard.alive = true;
        shard.rect.gameObject.SetActive(true);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        foreach (Shard shard in shards)
            if (shard.alive) Step(shard, dt);
    }

    // Falls, spins, and shrinks away over the last 40% of its life
    void Step(Shard shard, float dt)
    {
        float t = (Time.unscaledTime - shard.start) / shard.life;
        if (t >= 1f)
        {
            shard.alive = false;
            shard.rect.gameObject.SetActive(false);
            return;
        }

        shard.velocity.y -= gravity * dt;
        shard.rect.anchoredPosition += shard.velocity * dt;
        shard.rect.Rotate(0f, 0f, shard.spin * dt);
        shard.rect.localScale = Vector3.one * (t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
    }
}
