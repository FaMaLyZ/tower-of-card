using UnityEngine;
using UnityEngine.InputSystem;

// Middle mouse drag = orbit, scroll wheel = zoom. Follows the middle of the tower as it grows.
public class OrbitCamera : MonoBehaviour
{
    public TowerBuilder tower;
    public float distance = 8f;
    public float minDistance = 3f;
    public float maxDistance = 30f;
    public float orbitSpeed = 0.2f;
    public float zoomSpeed = 0.01f;
    public float followSpeed = 4f;

    float yaw, pitch;
    Vector3 focus;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
        focus = tower != null ? tower.FocusPoint : Vector3.zero;
    }

    void LateUpdate()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.middleButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                yaw += d.x * orbitSpeed;
                pitch = Mathf.Clamp(pitch - d.y * orbitSpeed, 5f, 85f);
            }
            distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * zoomSpeed, minDistance, maxDistance);
        }

        if (tower != null)
            focus = Vector3.Lerp(focus, tower.FocusPoint, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
        transform.position = focus + rot * new Vector3(0, 0, -distance);
        transform.rotation = rot;
    }
}