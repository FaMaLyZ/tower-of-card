using UnityEngine;
using UnityEngine.InputSystem;

/// Camera that orbits around the tower's focus point.
///   A = rotate left around the tower
///   D = rotate right around the tower
public class OrbitCamera : MonoBehaviour
{
    public TowerBuilder tower;              // auto-found if empty

    [Header("Camera")]
    public float distance = 10f;            // distance from the focus point
    public float pitch = 20f;               // look-down angle (degrees)
    public float orbitSpeed = 90f;          // degrees per second
    public Vector3 focusOffset = Vector3.zero;
    public float followSpeed = 4f;          // how fast the focus follows the tower as it grows

    [Header("Reset (when the whole tower is gone)")]
    public float resetSmoothing = 3f;       // how fast the camera swings back to its start view

    float yaw;
    float startYaw;                         // the view the game started with
    bool resetting;
    Vector3 focus;

    void Start()
    {
        if (tower == null) tower = FindAnyObjectByType<TowerBuilder>();

        yaw = startYaw = transform.eulerAngles.y;
        focus = GetFocusTarget();

        if (tower != null) tower.TowerReset += HandleTowerReset;
    }

    void OnDestroy()
    {
        if (tower != null) tower.TowerReset -= HandleTowerReset;
    }

    // All cards are gone: swing the camera back to the start view
    void HandleTowerReset()
    {
        resetting = true;
    }

    void LateUpdate()
    {
        HandleOrbitInput();
        UpdateReset();
        UpdateFocus();
        ApplyCameraTransform();
    }

    // A / D rotate the camera around the focus point
    void HandleOrbitInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f;
        if (kb.aKey.isPressed) h -= 1f;
        if (kb.dKey.isPressed) h += 1f;

        if (h != 0f) resetting = false;         // the player takes control again
        yaw += h * orbitSpeed * Time.deltaTime;
    }

    // Smoothly turns the camera back to the angle it started with
    void UpdateReset()
    {
        if (!resetting) return;

        yaw = Mathf.LerpAngle(yaw, startYaw, 1f - Mathf.Exp(-resetSmoothing * Time.deltaTime));

        if (Mathf.Abs(Mathf.DeltaAngle(yaw, startYaw)) < 0.05f)
        {
            yaw = startYaw;
            resetting = false;
        }
    }

    Vector3 GetFocusTarget()
    {
        Vector3 basePoint = tower != null ? tower.FocusPoint : Vector3.zero;
        return basePoint + focusOffset;
    }

    void UpdateFocus()
    {
        focus = Vector3.Lerp(focus, GetFocusTarget(), 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }

    void ApplyCameraTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = focus + rot * new Vector3(0f, 0f, -distance);
        transform.rotation = rot;
    }
}