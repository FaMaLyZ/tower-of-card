using System.Collections.Generic;
using UnityEngine;

/// Shakes the object it is on (a block of the HUD, the hand) when a big moment happens. Two effects:
///   Jitter (ShakeAll):  a rough random tremble. Works with "shake energy" like CameraShaker:
///                       each call adds some, it drains away, and the object trembles in proportion.
///   Hop and rock (JoltAll): like a sign knocked on a table, the block hops up a little, tips, and rocks
///                       left-right as it settles, turning around its own middle. Bigger drama, bigger jolt.
/// It adds its offset on top of wherever the object already is and takes it back off the next frame, so it
/// never drifts. If another script moved the object in between, that script's position simply wins.
/// Put it on any HUD block (distances in canvas pixels) or 3D object (distances in world units).
/// Listen-only: delete it and that object just stays still.
[DefaultExecutionOrder(2000)]
public class ShakeOffset : MonoBehaviour
{
    [Header("Jitter")]
    [Tooltip("Furthest it moves at full shake: canvas pixels on the HUD, world units on 3D objects")]
    public float maxDistance = 22f;
    [Tooltip("Furthest it tilts at full shake, in degrees")]
    public float maxTiltDegrees = 1.5f;
    [Tooltip("How many jolts per second")]
    public float frequency = 22f;
    [Tooltip("Energy lost per second: 2 means a full shake is gone in half a second")]
    public float drainPerSecond = 2f;

    [Header("Hop and rock (drama 0 → drama 1)")]
    [Tooltip("Off for the hand: its cards already hop on their own")]
    public bool hopAndRock = true;
    [Tooltip("How high it hops, in canvas pixels")]
    public Vector2 hopHeight = new Vector2(6f, 34f);
    public Vector2 hopSeconds = new Vector2(0.16f, 0.3f);
    [Tooltip("Most it tips while in the air and rocks after landing, in degrees")]
    public Vector2 rockDegrees = new Vector2(1.5f, 6f);
    public Vector2 rockSeconds = new Vector2(0.35f, 0.8f);
    public float rockCycles = 2.5f;
    [Tooltip("Blocks start a little apart, up to this many seconds, so they don't move as one")]
    public float maxDelay = 0.08f;

    static readonly List<ShakeOffset> all = new List<ShakeOffset>();

    float energy;
    float time;
    float seed;

    // The hop and rock in progress; joltStart < 0 = none
    float joltStart = -1f;
    float joltHop, joltHopSeconds, joltRock, joltRockSeconds;

    // What this component added last frame, and the pose it left behind
    bool hasOffset;
    Vector3 offsetAdded;
    Quaternion turnAdded = Quaternion.identity;
    Vector3 positionLeft;
    Quaternion rotationLeft;

    /// Trembles every ShakeOffset in the scene (energy 1 = a full shake)
    public static void ShakeAll(float amount)
    {
        foreach (ShakeOffset shaker in all) shaker.AddShake(amount);
    }

    /// Makes every ShakeOffset hop and rock (drama 0 = a nudge, 1 = a hard knock)
    public static void JoltAll(float drama)
    {
        foreach (ShakeOffset shaker in all) shaker.Jolt(drama);
    }

    public void AddShake(float amount) => energy = Mathf.Clamp01(energy + amount);

    public void Jolt(float drama)
    {
        if (!hopAndRock) return;
        drama = Mathf.Clamp01(drama);

        joltStart = Time.unscaledTime + Random.Range(0f, maxDelay);
        joltHop = Mathf.Lerp(hopHeight.x, hopHeight.y, drama) * Random.Range(0.75f, 1.15f);
        joltHopSeconds = Mathf.Max(0.01f, Mathf.Lerp(hopSeconds.x, hopSeconds.y, drama));
        joltRock = Mathf.Lerp(rockDegrees.x, rockDegrees.y, drama) * (Random.value < 0.5f ? -1f : 1f);
        joltRockSeconds = Mathf.Max(0.01f, Mathf.Lerp(rockSeconds.x, rockSeconds.y, drama));
    }

    void OnEnable()
    {
        all.Add(this);
        seed = Random.value * 100f;
    }

    void OnDisable()
    {
        all.Remove(this);
        RemoveOffset();
        energy = 0f;
        joltStart = -1f;
    }

    // Runs after every other script has placed this object for the frame
    void LateUpdate()
    {
        RemoveOffset();

        Vector3 offset = Vector3.zero;
        float degrees = 0f;
        AddJitter(ref offset, ref degrees);
        AddHopAndRock(ref offset, ref degrees);

        if (offset == Vector3.zero && degrees == 0f) return;
        AddOffset(offset + KeepMiddleStill(degrees), Quaternion.Euler(0f, 0f, degrees));
    }

    void AddJitter(ref Vector3 offset, ref float degrees)
    {
        if (energy <= 0f) return;

        time += Time.unscaledDeltaTime * frequency;
        offset += new Vector3(Noise(0f), Noise(1f), 0f) * maxDistance * energy;
        degrees += Noise(2f) * maxTiltDegrees * energy;
        energy = Mathf.Max(0f, energy - drainPerSecond * Time.unscaledDeltaTime);
    }

    // Up and down like it was knocked from below, tipping as it goes; then rocking left-right, dying out
    void AddHopAndRock(ref Vector3 offset, ref float degrees)
    {
        if (joltStart < 0f) return;

        float t = (Time.unscaledTime - joltStart) / joltHopSeconds;
        if (t < 0f) return;                                     // still waiting for its turn

        if (t < 1f)
        {
            offset += Vector3.up * joltHop * 4f * t * (1f - t);
            degrees += joltRock * Mathf.Sin(t * Mathf.PI);
            return;
        }

        float u = (t - 1f) * joltHopSeconds / joltRockSeconds;
        if (u >= 1f) { joltStart = -1f; return; }
        degrees += -joltRock * Mathf.Sin(u * rockCycles * 2f * Mathf.PI) * (1f - u) * (1f - u);
    }

    // A HUD block turns around its own middle, not its pivot (which may be a corner)
    Vector3 KeepMiddleStill(float degrees)
    {
        if (degrees == 0f || !(transform is RectTransform rect)) return Vector3.zero;

        Vector2 middleFromPivot = Vector2.Scale(new Vector2(0.5f, 0.5f) - rect.pivot, rect.rect.size);
        Vector3 middle = Vector3.Scale(middleFromPivot, rect.localScale);
        return middle - Quaternion.Euler(0f, 0f, degrees) * middle;
    }

    void AddOffset(Vector3 offset, Quaternion turn)
    {
        transform.localPosition += offset;
        transform.localRotation *= turn;

        hasOffset = true;
        offsetAdded = offset;
        turnAdded = turn;
        positionLeft = transform.localPosition;
        rotationLeft = transform.localRotation;
    }

    // Takes last frame's offset back off, unless another script has placed the object since
    void RemoveOffset()
    {
        if (!hasOffset) return;
        hasOffset = false;

        if (transform.localPosition == positionLeft) transform.localPosition -= offsetAdded;
        if (transform.localRotation == rotationLeft) transform.localRotation *= Quaternion.Inverse(turnAdded);
    }

    // Smooth random wobble between -1 and 1; each channel is a different stream
    float Noise(float channel)
    {
        return Mathf.PerlinNoise(seed + channel * 17.3f, time) * 2f - 1f;
    }
}
