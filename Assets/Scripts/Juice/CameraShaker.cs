using UnityEngine;

/// Camera effect (C4): every broken card gives the camera a small jolt, and jolts stack: several cards
/// breaking close together build up into a bigger shake. A collapse shakes it the most.
/// Works with "shake energy" (0 to 1): each event adds some, it drains away over time, and the camera moves
/// in proportion to what is left.
/// Put it on the camera next to OrbitCamera. It runs after OrbitCamera each frame and adds a small offset
/// on top of the pose OrbitCamera just set, so it can never drift, and it never moves the table or the tower.
/// Listen-only: delete it and the camera works the same.
[DefaultExecutionOrder(1000)]
public class CameraShaker : MonoBehaviour
{
    [Header("How much each event adds (full shake = 1)")]
    [Range(0f, 1f)] public float energyPerBrokenCard = 0.3f;
    [Range(0f, 1f)] public float energyOnCollapse = 1f;

    [Header("Shake at full energy")]
    [Tooltip("Furthest the camera moves, in world units")]
    public float maxAmount = 0.4f;
    [Tooltip("Furthest the camera tilts, in degrees")]
    public float maxTiltDegrees = 2f;
    [Tooltip("How many jolts per second")]
    public float frequency = 25f;

    [Header("Fade")]
    [Tooltip("Energy lost per second: 2 means a full shake is gone in half a second")]
    public float drainPerSecond = 2f;

    float energy;
    float time;
    float seed;

    void Awake() => seed = Random.value * 100f;

    void OnEnable()
    {
        GameEvents.CardBroken += OnCardBroken;
        GameEvents.TowerCollapsed += OnTowerCollapsed;
    }

    void OnDisable()
    {
        GameEvents.CardBroken -= OnCardBroken;
        GameEvents.TowerCollapsed -= OnTowerCollapsed;
        energy = 0f;
    }

    void OnCardBroken(Card card) => AddShake(energyPerBrokenCard);
    void OnTowerCollapsed(ShakeResult shake) => AddShake(energyOnCollapse);

    /// Adds shake energy on top of what is still playing (never above a full shake)
    public void AddShake(float amount)
    {
        energy = Mathf.Clamp01(energy + amount);
    }

    // After OrbitCamera.LateUpdate has placed the camera for this frame
    void LateUpdate()
    {
        if (energy <= 0f) return;

        time += Time.unscaledDeltaTime * frequency;
        ApplyJolt();
        energy = Mathf.Max(0f, energy - drainPerSecond * Time.unscaledDeltaTime);
    }

    void ApplyJolt()
    {
        Vector2 jolt = new Vector2(Noise(0f), Noise(1f));
        transform.position += (transform.right * jolt.x + transform.up * jolt.y) * maxAmount * energy;
        transform.rotation *= Quaternion.Euler(0f, 0f, Noise(2f) * maxTiltDegrees * energy);
    }

    // Smooth random wobble between -1 and 1; each channel is a different stream
    float Noise(float channel)
    {
        return Mathf.PerlinNoise(seed + channel * 17.3f, time) * 2f - 1f;
    }
}
