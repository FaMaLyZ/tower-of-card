using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Rigidbody))]
public class Card : MonoBehaviour
{
    public CardData Data { get; private set; }

    Renderer rend;
    Collider col;
    Rigidbody rb;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        // thin cards can pass through each other when they fall fast
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public void Setup(CardData data)
    {
        Data = data;
        rend.material.color = data.tint;   // works with URP Lit (_BaseColor)
    }

    // Ghost = preview card following the mouse. Green tint = snapped, red = no slot nearby.
    public void SetGhost(bool snapped)
    {
        if (col != null) col.enabled = false;   // ghost has no collision

        rb.isKinematic = true;                  // ghost must not fall
        rb.useGravity = false;

        rend.material.color = snapped
            ? Color.Lerp(Data.tint, Color.green, 0.35f)
            : Color.Lerp(Data.tint, Color.red, 0.6f);
    }

    // Card is now part of the tower: solid, but frozen in place until physics is applied
    public void SetPlaced()
    {
        if (col != null) col.enabled = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        Freeze();

        rend.material.color = Data.tint;
    }

    // Locks position and rotation (used while the player is building)
    public void Freeze()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    // Releases the card so gravity and collisions move it (used when the turn ends)
    public void Unfreeze()
    {
        rb.constraints = RigidbodyConstraints.None;
        rb.WakeUp();
    }
}