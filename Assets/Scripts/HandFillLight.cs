using UnityEngine;
using UnityEngine.SceneManagement;

/// A soft light that rides with the camera and lights ONLY the cards in the player's hand.
/// It is a directional light (no bright spot, the same strength everywhere) shining from just above the
/// camera, so the hand looks evenly brighter wherever the camera goes. It has no shadows.
///
/// It touches nothing else (table, tower, background) because of URP Rendering Layers:
///   the light only lights renderers on the "Hand Fill" rendering layer, and HandCardView puts the
///   hand cards on that layer (MarkHandCard). Needs "Light Layers" on in the URP asset (it is).
///
/// Put it on the Main Camera to tune it in the Inspector. If no camera has one, it adds itself to
/// the Main Camera in any scene with a HandManager.
public class HandFillLight : MonoBehaviour
{
    /// The rendering layer used for the hand (Project Settings > Tags and Layers > Rendering Layers)
    public const int HandRenderingLayer = 7;

    [Tooltip("How much brighter the hand gets")]
    [Range(0f, 3f)] public float intensity = 0.8f;
    public Color color = new Color(1f, 0.96f, 0.9f);
    [Tooltip("Degrees the light tilts down from the camera's view (0 = straight from the camera)")]
    [Range(0f, 60f)] public float tiltDown = 15f;

    Light fill;

    static uint HandLayerBit => 1u << HandRenderingLayer;

    // ---------- Set-up ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddToHandScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => AddIfHandScene();
        AddIfHandScene();
    }

    static void AddIfHandScene()
    {
        if (FindAnyObjectByType<HandFillLight>() != null) return;
        if (FindAnyObjectByType<HandManager>() == null) return;
        if (Camera.main == null) return;
        Camera.main.gameObject.AddComponent<HandFillLight>();
    }

    /// Puts these renderers on the hand rendering layer, so only they get the fill light.
    /// They keep their other layers, so the normal scene lights still light them too.
    public static void MarkHandCard(Renderer[] renderers)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers)
            if (r != null) r.renderingLayerMask |= HandLayerBit;
    }

    void Awake()
    {
        fill = CreateLight();
    }

    Light CreateLight()
    {
        var lightObject = new GameObject("Hand Fill Light");
        lightObject.transform.SetParent(transform, false);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.None;
        light.renderingLayerMask = (int)HandLayerBit;       // lights the hand and nothing else
        return light;
    }

    void OnEnable()
    {
        if (fill != null) fill.enabled = true;
    }

    void OnDisable()
    {
        if (fill != null) fill.enabled = false;
    }

    // Read every frame so changes in the Inspector show at once while playing
    void LateUpdate()
    {
        if (fill == null) return;
        fill.intensity = intensity;
        fill.color = color;
        fill.transform.localRotation = Quaternion.Euler(tiltDown, 0f, 0f);
    }
}
