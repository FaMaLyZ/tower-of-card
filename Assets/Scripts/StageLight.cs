using UnityEngine;
using UnityEngine.SceneManagement;

/// A soft spotlight hanging above the middle of the table, pointing straight down: a pool of light
/// that makes the tower the stage. The edge of the pool fades out gently (Inner Angle is much smaller
/// than Spot Angle), and the tower casts soft shadows on the table.
///
/// It always hangs above TowerBuilder's tower spot, so moving the spot moves the light with it.
/// Put it on the TowerBuilder object to tune it in the Inspector. If none exists, it adds itself
/// there in any scene with a TowerBuilder.
public class StageLight : MonoBehaviour
{
    [Tooltip("How bright the pool of light is. Too high and the white cards go past the Bloom threshold and glow (look blurry).")]
    [Min(0f)] public float intensity = 35f;
    public Color color = new Color(1f, 0.92f, 0.78f);

    [Header("Shape")]
    [Tooltip("How high above the table the light hangs")]
    [Min(0.5f)] public float height = 5f;
    [Tooltip("Width of the whole pool of light, in degrees")]
    [Range(10f, 150f)] public float spotAngle = 76.6f;
    [Tooltip("Inside this angle it is fully bright; from here to Spot Angle it fades out (smaller = softer edge)")]
    [Range(0f, 150f)] public float innerAngle = 47.3f;

    [Header("Shadows")]
    public bool castShadows = true;
    [Range(0f, 1f)] public float shadowStrength = 0.1f;

    TowerBuilder tower;
    Light stage;

    // ---------- Set-up ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddToTowerScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => AddIfTowerScene();
        AddIfTowerScene();
    }

    static void AddIfTowerScene()
    {
        if (FindAnyObjectByType<StageLight>() != null) return;
        TowerBuilder builder = FindAnyObjectByType<TowerBuilder>();
        if (builder != null) builder.gameObject.AddComponent<StageLight>();
    }

    void Awake()
    {
        tower = GetComponent<TowerBuilder>();
        if (tower == null) tower = FindAnyObjectByType<TowerBuilder>();
        stage = CreateLight();
    }

    Light CreateLight()
    {
        var lightObject = new GameObject("Stage Light");
        lightObject.transform.SetParent(transform, false);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        return light;
    }

    void OnEnable()
    {
        if (stage != null) stage.enabled = true;
    }

    void OnDisable()
    {
        if (stage != null) stage.enabled = false;
    }

    // Read every frame so changes in the Inspector show at once while playing
    void LateUpdate()
    {
        if (stage == null) return;
        PlaceAboveTowerSpot();
        ApplySettings();
    }

    void PlaceAboveTowerSpot()
    {
        Vector3 spot = tower != null ? tower.TowerSpot : Vector3.zero;
        stage.transform.SetPositionAndRotation(spot + Vector3.up * height, Quaternion.Euler(90f, 0f, 0f));
    }

    void ApplySettings()
    {
        stage.intensity = intensity;
        stage.color = color;
        stage.range = height * 2.5f;                                // reaches the table with room to spare
        stage.spotAngle = spotAngle;
        stage.innerSpotAngle = Mathf.Min(innerAngle, spotAngle);
        stage.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        stage.shadowStrength = shadowStrength;
    }
}
