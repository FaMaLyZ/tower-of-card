using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// A marker on every free snap point while the player aims a card (green = where it will snap).
///
/// A view: it only reads TowerPlacementInput.
public class SnapMarkerView : MonoBehaviour
{
    [Header("Game references")]
    public TowerPlacementInput placement;
    public Canvas canvas;

    [Header("Markers")]
    public Image snapMarkerTemplate;
    public Color snapMarkerColor = new Color(1f, 1f, 1f, 0.6f);
    public Color snapMarkerBestColor = new Color(0.2f, 1f, 0.3f, 1f);

    Camera cam;
    readonly List<RectTransform> markers = new List<RectTransform>();
    readonly List<Image> markerImages = new List<Image>();

    void Awake()
    {
        if (placement == null) placement = FindAnyObjectByType<TowerPlacementInput>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();

        if (snapMarkerTemplate != null)
        {
            snapMarkerTemplate.raycastTarget = false;
            snapMarkerTemplate.gameObject.SetActive(false);
        }
    }

    // After TowerPlacementInput has found the free slots this frame
    void LateUpdate()
    {
        if (snapMarkerTemplate == null || canvas == null) return;
        if (cam == null) cam = Camera.main;

        int count = placement != null && cam != null && placement.ShowSlots ? placement.SlotCount : 0;
        while (markers.Count < count) CreateMarker();

        for (int i = 0; i < markers.Count; i++)
            PlaceMarker(i, i < count);
    }

    void PlaceMarker(int i, bool wanted)
    {
        Vector3 sp = wanted ? cam.WorldToScreenPoint(placement.GetSlotPosition(i)) : Vector3.zero;
        bool visible = wanted && sp.z > 0;      // not behind the camera
        markers[i].gameObject.SetActive(visible);
        if (!visible) return;

        var parent = (RectTransform)snapMarkerTemplate.transform.parent;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, uiCam, out Vector2 local);

        markers[i].anchoredPosition = local - parent.rect.center;
        markerImages[i].color = i == placement.BestSlotIndex ? snapMarkerBestColor : snapMarkerColor;
    }

    void CreateMarker()
    {
        var clone = Instantiate(snapMarkerTemplate.gameObject, snapMarkerTemplate.transform.parent);
        clone.name = "SnapMarker";

        var rt = (RectTransform)clone.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        markers.Add(rt);
        markerImages.Add(clone.GetComponent<Image>());
    }
}
