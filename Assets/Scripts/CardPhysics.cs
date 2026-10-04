using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class CardPhysics : MonoBehaviour
{
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void ActivatePhysics()
    {
        rb.isKinematic = false;
    }

    public void DeactivatePhysics()
    {
        rb.isKinematic = true;
    }
}